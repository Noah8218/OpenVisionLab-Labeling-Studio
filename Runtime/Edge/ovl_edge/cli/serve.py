"""Edge worker: poll the Modbus request block, infer, judge, write the result block.

The worker is a Modbus TCP client of the requesting side (design E7). It never
builds engines or calibrates; it loads a prepared model package and refuses to
start when the TensorRT version or GPU recorded in engines.json differs from this
host, or when the engine does not deserialize.

Each request is inferred once. A result whose write failed is written again on
the next poll without re-inference and with the same commit value. When the
requester clears pending and raises it again for the same (session, seq), the
stored result is written again unchanged; the requester identifies it by the
session/seq in the result block, not by a new commit value.

    python -m ovl_edge.cli.serve --model-dir /model --runtime trt-fp16 --rule /model/rule.json \
        --input-root /frames --modbus-host machine-sim --modbus-port 5020 --metrics-port 9100
"""

from __future__ import annotations

import argparse
import hashlib
import json
import logging
import secrets
import sys
import time
from pathlib import Path

import cv2
import numpy as np
from prometheus_client import start_http_server

from ovl_edge.decision import DecisionRule, ResultCode, decide
from ovl_edge.metrics_export import WORKER_ERRORS, WORKER_INFO, WORKER_RESULT_REWRITES, WORKER_RESULTS, WORKER_STAGE_SECONDS
from ovl_edge.modbus_map import Request, Result, frame_relative_path
from ovl_edge.runners import Detector, OrtRunner
from ovl_edge.spec import ModelSpec, sha256_file

LOG = logging.getLogger("ovl_edge.serve")


def check_engine_tensorrt(manifest: dict, trt_version: str) -> None:
    """Refuse an engine package built with another TensorRT version (design M3)."""
    recorded = manifest.get("tensorrt")
    if not recorded:
        LOG.warning("engines.json has no TensorRT version; only deserialization is checked")
        return
    if recorded != trt_version:
        raise SystemExit(f"Engines were built with TensorRT {recorded}; this host has {trt_version}. Rebuild the engines here.")


def check_engine_gpu(recorded: dict | None, current: dict) -> None:
    """Refuse an engine package built on another GPU model (design M3).

    TensorRT plans are tied to the GPU model; on a different model with the same
    TensorRT version deserialization can succeed with only a warning, so it is
    not sufficient as the check. Packages built before the GPU was recorded get
    a warning instead.
    """
    if not recorded:
        LOG.warning("engines.json has no GPU record; the build GPU cannot be compared with %s", current.get("name"))
        return
    if str(recorded.get("compute_capability")) != str(current.get("compute_capability")):
        raise SystemExit(f"Engines were built for compute capability {recorded.get('compute_capability')}; this GPU is {current.get('compute_capability')}")
    if str(recorded.get("name", "")).strip().casefold() != str(current.get("name", "")).strip().casefold():
        raise SystemExit(f"Engines were built on '{recorded.get('name')}'; this GPU is '{current.get('name')}'. Rebuild the engines here.")


def load_runner(model_dir: Path, spec: ModelSpec, runtime: str):
    if runtime == "ort-cuda":
        path = model_dir / spec.onnx_file
        if sha256_file(path) != spec.onnx_sha256:
            raise SystemExit(f"ONNX hash mismatch for {path}")
        return OrtRunner(str(path), "cuda", use_tf32=False), spec.onnx_sha256
    if runtime.startswith("trt-"):
        manifest = json.loads((model_dir / "engines" / "engines.json").read_text(encoding="utf-8"))
        key = runtime[len("trt-"):]
        entry = manifest["engines"].get(key)
        if not entry:
            raise SystemExit(f"Engine '{key}' is not in engines.json")
        path = model_dir / "engines" / entry["engine"]
        if sha256_file(path) != entry["engine_sha256"]:
            raise SystemExit(f"Engine hash mismatch for {path}")

        import tensorrt

        from ovl_edge.trt_runner import TrtRunner, current_gpu

        check_engine_tensorrt(manifest, tensorrt.__version__)
        runner = TrtRunner(path, name=runtime)
        # Queried after the runner exists: the CUDA schedule flag must be set before the context.
        check_engine_gpu(entry.get("gpu"), current_gpu())
        return runner, entry["engine_sha256"]
    raise SystemExit(f"Unknown runtime {runtime}")


class Worker:
    # Request-slot state used by step(); class defaults keep test doubles built with __new__ valid.
    last_key: tuple[int, int] | None = None  # last request whose result was written
    last_result: Result | None = None  # that result, for a repeated request
    unwritten: Result | None = None  # inferred result whose write failed
    cleared: bool = False  # requester cleared pending after last_key was written

    def __init__(self, args):
        self.args = args
        self.model_dir = Path(args.model_dir)
        self.spec = ModelSpec.load(self.model_dir / "model_spec.json")
        self.spec_sha = sha256_file(self.model_dir / "model_spec.json")
        runner, self.engine_sha = load_runner(self.model_dir, self.spec, args.runtime)
        self.detector = Detector(self.spec, runner)
        rule_data = json.loads(Path(args.rule).read_text(encoding="utf-8")) if args.rule else None
        self.rule = DecisionRule.from_dict(rule_data)
        self.rule_sha = self.rule.digest() if self.rule else "0" * 64
        self.input_root = Path(args.input_root)
        # The requester detects a new result by a changed commit value. A fixed start
        # (e.g. 0 -> 1) could repeat the last value it saw before a worker restart.
        self.commit = secrets.randbelow(0xFFFF) + 1
        WORKER_INFO.labels(args.runtime, self.engine_sha[:12], self.spec_sha[:12], self.rule_sha[:12]).set(1)
        # Warm-up so the first request does not pay CUDA context / kernel selection cost.
        blank = np.full((self.spec.input_hw[0], self.spec.input_hw[1], 3), 114, np.uint8)
        for _ in range(args.warmup):
            self.detector.detect(blank)

    def observe(self, stage: str, seconds: float) -> None:
        WORKER_STAGE_SECONDS.labels(stage, self.args.runtime).observe(seconds)

    def process(self, request: Request) -> Result:
        t0 = time.perf_counter()
        code, decision = ResultCode.MODEL_ERROR, None
        frame_prefix = b""
        try:
            path = self.input_root / frame_relative_path(request.session, request.seq, getattr(self.args, "frame_ext", "png"))
            data = path.read_bytes()
            t1 = time.perf_counter()
            self.observe("frame_read", t1 - t0)
            digest = hashlib.sha256(data).digest()
            frame_prefix = digest[:8]
            if frame_prefix != request.frame_sha_prefix:
                code = ResultCode.INPUT_MISMATCH
                WORKER_ERRORS.labels("input_mismatch").inc()
            else:
                image = cv2.imdecode(np.frombuffer(data, np.uint8), cv2.IMREAD_COLOR)
                if image is None:
                    raise ValueError("frame decode failed")
                t2 = time.perf_counter()
                self.observe("decode", t2 - t1)
                detections, times = self.detector.detect(image)
                self.observe("preprocess", times.preprocess_ms / 1e3)
                self.observe("infer", times.infer_ms / 1e3)
                self.observe("postprocess", times.postprocess_ms / 1e3)
                decision = decide(detections, self.rule)
                code = decision.code
        except FileNotFoundError:
            code = ResultCode.INPUT_MISMATCH
            WORKER_ERRORS.labels("frame_missing").inc()
        except Exception:  # model or decode failure is reported, not raised
            LOG.exception("request %s/%s failed", request.session, request.seq)
            code = ResultCode.MODEL_ERROR
            WORKER_ERRORS.labels("model_error").inc()
        service_s = time.perf_counter() - t0
        self.observe("service", service_s)
        self.commit = (self.commit + 1) & 0xFFFF or 1
        WORKER_RESULTS.labels(code.name, self.args.runtime).inc()
        return Result(
            session=request.session,
            seq=request.seq,
            code=int(code),
            detections=decision.detections if decision else 0,
            qualifying=decision.qualifying if decision else 0,
            top_class=decision.top_class if decision else -1,
            top_score=decision.top_score if decision else 0.0,
            commit=self.commit,
            model_sha_prefix=bytes.fromhex(self.engine_sha[:16]),
            rule_sha_prefix=bytes.fromhex(self.rule_sha[:16]),
            frame_sha_prefix=frame_prefix,
            service_us=int(service_s * 1e6),
        )

    def make_transport(self):
        if self.args.transport == "opcua":
            from ovl_edge.transport import OpcUaTransport

            return OpcUaTransport(self.args.opcua_url)
        from ovl_edge.transport import ModbusTransport

        return ModbusTransport(self.args.modbus_host, self.args.modbus_port, self.args.device_id)

    def write(self, transport, result: Result) -> bool:
        t_write = time.perf_counter()
        written = transport.write_result(result)
        self.observe("result_write", time.perf_counter() - t_write)
        if not written:
            WORKER_ERRORS.labels(f"{transport.name}_write").inc()
        return written

    def step(self, transport) -> None:
        """One poll of the request slot."""
        poll_s = self.args.poll_ms / 1000.0
        if not transport.connect():
            WORKER_ERRORS.labels(f"{transport.name}_connect").inc()
            time.sleep(1.0)
            return
        request = transport.read_request()
        if request is None:
            WORKER_ERRORS.labels(f"{transport.name}_read").inc()
            time.sleep(poll_s)
            return
        key = (request.session, request.seq)

        if not request.pending:
            if key == self.last_key:
                self.cleared = True
            time.sleep(poll_s)
            return

        if key == self.last_key:
            if self.cleared and self.last_result is not None:
                # Same request raised again after the requester cleared it: return the stored result.
                if self.write(transport, self.last_result):
                    WORKER_RESULT_REWRITES.labels("repeat_request").inc()
                    self.cleared = False
            time.sleep(poll_s)
            return

        if self.unwritten is not None and (self.unwritten.session, self.unwritten.seq) == key:
            result, reason = self.unwritten, "write_retry"
        else:
            # The wait before this request was noticed is not measured here: the request carries
            # no issue time, and a requester that stalls its register server answers the stalled
            # read with the new request. The requester measures it (phase transport_queue).
            result, reason = self.process(request), None
        if not self.write(transport, result):
            self.unwritten = result
            time.sleep(poll_s)
            return
        if reason:
            WORKER_RESULT_REWRITES.labels(reason).inc()
        self.unwritten = None
        self.last_key, self.last_result, self.cleared = key, result, False
        LOG.debug("request session=%d seq=%d code=%d service_us=%d", request.session, request.seq, result.code, result.service_us)

    def run(self) -> int:
        transport = self.make_transport()
        LOG.info(
            "worker ready runtime=%s transport=%s engine=%s rule=%s cuda_schedule=%s",
            self.args.runtime,
            transport.name,
            self.engine_sha[:12],
            self.rule_sha[:12],
            getattr(self.detector.runner, "cuda_schedule", "n/a"),
        )
        while True:
            self.step(transport)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--runtime", default="trt-fp16", help="ort-cuda | trt-fp32 | trt-fp16 | trt-int8 | trt-int8_fp16")
    parser.add_argument("--rule", default="", help="decision rule JSON; missing -> every result is RuleMissing")
    parser.add_argument("--input-root", required=True)
    parser.add_argument("--frame-ext", default="png", choices=["png", "bmp"])
    parser.add_argument("--transport", default="modbus", choices=["modbus", "opcua"])
    parser.add_argument("--opcua-url", default="opc.tcp://127.0.0.1:4840/ovl/")
    parser.add_argument("--modbus-host", default="127.0.0.1")
    parser.add_argument("--modbus-port", type=int, default=5020)
    parser.add_argument("--device-id", type=int, default=1)
    parser.add_argument("--poll-ms", type=float, default=1.0)
    parser.add_argument("--metrics-port", type=int, default=9100)
    parser.add_argument("--warmup", type=int, default=20)
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args(argv)
    logging.basicConfig(level=args.log_level, format="%(asctime)s %(levelname)s %(name)s %(message)s")
    start_http_server(args.metrics_port)
    return Worker(args).run()


if __name__ == "__main__":
    sys.exit(main())
