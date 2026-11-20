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
import os
import secrets
import sys
import time
from pathlib import Path

import cv2
import numpy as np
from prometheus_client import start_http_server

from ovl_edge.case_capture import CapturePolicy, CaseRecorder, case_reasons
from ovl_edge.decision import DecisionRule, ResultCode
from ovl_edge.fallback import AllTiersFailed, FallbackChain, FaultInjector, Tier, model_tier, rule_tier
from ovl_edge.metrics_export import (
    WORKER_ACTIVE_TIER,
    WORKER_DEGRADED,
    WORKER_ERRORS,
    WORKER_HEARTBEAT,
    WORKER_INFO,
    WORKER_RESULT_REWRITES,
    WORKER_RESULTS,
    WORKER_STAGE_SECONDS,
    WORKER_TIER_SWITCHES,
)
from ovl_edge.modbus_map import Request, Result, frame_relative_path
from ovl_edge.release import Release
from ovl_edge.release_runtime import ReleaseManager, ShadowRunner, Slot, Snapshot
from ovl_edge.rule_inspector import RuleParams
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
    """ort-cuda | ort-cpu | ov-cpu (FP32) | ov-cpu-bf16 | ov-cpu-int8 (the INT8 Q/DQ ONNX) | trt-<engine>."""
    if runtime in ("ort-cuda", "ort-cpu", "ov-cpu", "ov-cpu-bf16"):
        path = model_dir / spec.onnx_file
        if sha256_file(path) != spec.onnx_sha256:
            raise SystemExit(f"ONNX hash mismatch for {path}")
        if runtime.startswith("ov-cpu"):
            from ovl_edge.runners import OvRunner

            return OvRunner(str(path), "CPU", precision="bf16" if runtime.endswith("bf16") else "f32"), spec.onnx_sha256
        return OrtRunner(str(path), runtime.split("-")[1], use_tf32=False), spec.onnx_sha256
    if runtime in ("ov-cpu-int8", "ov-cpu-int8h"):
        # NNCF-quantized OpenVINO IR (ovl_edge.cli.ov_quantize); int8h keeps the head in float.
        key = "int8_head_fp" if runtime == "ov-cpu-int8h" else "int8"
        manifest_path = model_dir / "openvino.json"
        entry = json.loads(manifest_path.read_text(encoding="utf-8"))["models"].get(key) if manifest_path.exists() else None
        if not entry:
            raise SystemExit(f"{runtime} needs openvino.json '{key}' (run ovl_edge.cli.ov_quantize)")
        xml, weights = model_dir / entry["xml"], model_dir / entry["bin"]
        if sha256_file(xml) != entry["xml_sha256"] or sha256_file(weights) != entry["bin_sha256"]:
            raise SystemExit(f"OpenVINO IR hash mismatch for {xml}")
        from ovl_edge.runners import OvRunner

        return OvRunner(str(xml), "CPU"), entry["bin_sha256"]
    if runtime == "ov-cpu-qdq":
        # Kept to reproduce a known problem: the Model Optimizer Q/DQ ONNX (TensorRT INT8 input)
        # runs on OpenVINO but missed 320 of 500 defects (64%) on the 1,000-image test split
        # (67 of 100 in the first 200-image check; ORT CPU on the same file: 1 of 100).
        entry = json.loads((model_dir / "engines" / "engines.json").read_text(encoding="utf-8"))["engines"]["int8"]
        path = model_dir / entry["onnx"]
        if sha256_file(path) != entry["onnx_sha256"]:
            raise SystemExit(f"Q/DQ ONNX hash mismatch for {path}")
        from ovl_edge.runners import OvRunner

        return OvRunner(str(path), "CPU"), entry["onnx_sha256"]
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
    releases: ReleaseManager | None = None  # set when the worker runs from a release registry
    shadow: ShadowRunner | None = None
    _shadow_job: tuple | None = None  # (image, active code) of the last request, for shadow comparison
    cases: CaseRecorder | None = None  # field-feedback capture (ovl_edge.case_capture)
    rule_min_score: float = 0.25

    def __init__(self, args):
        self.args = args
        self.input_root = Path(args.input_root)
        # The requester detects a new result by a changed commit value. A fixed start
        # (e.g. 0 -> 1) could repeat the last value it saw before a worker restart.
        self.commit = secrets.randbelow(0xFFFF) + 1
        self.runtime_label = args.runtime
        if getattr(args, "registry", ""):
            # Model, runtime, fallback tiers and rule come from the active release (ovl_edge.release).
            root = Path(args.registry)
            self.releases = ReleaseManager(root, self.load_release, poll_s=args.registry_poll_s).start()
            self.shadow = ShadowRunner(root).start()
            self.install(self.releases.current)
        else:
            model_dir = Path(args.model_dir)
            spec = ModelSpec.load(model_dir / "model_spec.json")
            rule = DecisionRule.from_dict(json.loads(Path(args.rule).read_text(encoding="utf-8")) if args.rule else None)
            self.rule_sha = rule.digest() if rule else "0" * 64
            self.rule_min_score = rule.min_score if rule else 0.25
            self.chain = self.build_chain(model_dir, spec, args.runtime, args.fallback, rule, args.rule_params)
            self.engine_sha = self.chain.tiers[0].sha
            WORKER_INFO.labels(args.runtime, self.engine_sha[:12], sha256_file(model_dir / "model_spec.json")[:12], self.rule_sha[:12]).set(1)
        if getattr(args, "cases_dir", ""):
            policy = CapturePolicy(near_band=args.case_near_band, max_per_minute=args.case_max_per_minute)
            self.cases = CaseRecorder(Path(args.cases_dir), policy).start()
        self.publish_tier()

    def capture_case(self, request: Request, data: bytes, image, verdict, chain: FallbackChain, rule_sha: str) -> None:
        if self.cases is None:
            return
        decision = verdict.decision
        reasons = case_reasons(decision.top_score, self.rule_min_score, int(decision.code), verdict.tier, chain.primary, self.cases.policy)
        if not reasons:
            return
        dets = verdict.detections
        boxes = []
        if dets is not None and len(dets):
            boxes = [[*map(lambda v: round(float(v), 1), b), round(float(s), 4), int(c)] for b, s, c in zip(dets.boxes, dets.scores, dets.classes)]
        record = {"session": request.session, "seq": request.seq, "code": int(decision.code), "tier": verdict.tier,
                  "model_sha_prefix": verdict.sha[:16], "rule_sha_prefix": rule_sha[:16], "top_class": decision.top_class,
                  "top_score": round(decision.top_score, 4), "min_score": self.rule_min_score, "image_hw": list(image.shape[:2]),
                  "detections_xyxy_score_class": boxes,
                  "release": self.releases.current.active.release.id if self.releases is not None else None}
        self.cases.offer(reasons, data, getattr(self.args, "frame_ext", "png"), record)

    def load_release(self, root: Path, release: Release) -> Slot:
        """Called by the release watcher thread; the worker keeps serving meanwhile."""
        base = release.dir(root)
        package = base / "package"
        spec = ModelSpec.load(package / "model_spec.json")
        rule = DecisionRule.from_dict(json.loads((base / "rule.json").read_text(encoding="utf-8")))
        rule_params = base / "rule_params.json"
        chain = self.build_chain(package, spec, release.runtime, list(release.fallback), rule, str(rule_params) if rule_params.exists() else "")
        return Slot(release, chain, rule.digest() if rule else "0" * 64, rule.min_score if rule else 0.25)

    def install(self, snapshot: Snapshot) -> None:
        """Worker thread, between requests: make the snapshot's active release the one that judges."""
        self.chain = snapshot.active.chain
        self.rule_sha = snapshot.active.rule_sha
        self.rule_min_score = snapshot.active.min_score
        self.runtime_label = snapshot.active.release.runtime
        self.engine_sha = self.chain.tiers[0].sha
        WORKER_INFO.clear()
        WORKER_INFO.labels(self.runtime_label, self.engine_sha[:12], snapshot.active.release.digest()[:12], self.rule_sha[:12]).set(1)

    def build_chain(self, model_dir: Path, spec: ModelSpec, runtime: str, fallback: list[str], rule, rule_params: str) -> FallbackChain:
        """`runtime` first, then the fallback tiers. A tier that cannot load is skipped and logged."""
        args = self.args
        names = [runtime] + [n for n in fallback if n != runtime]
        blank = np.full((spec.input_hw[0], spec.input_hw[1], 3), 114, np.uint8)
        tiers: list[Tier] = []
        for name in names:
            try:
                if name == "rule":
                    params = RuleParams.from_dict(json.loads(Path(rule_params).read_text(encoding="utf-8")))
                    tiers.append(rule_tier(params, args.rule_pass_verdict))
                    continue
                runner, sha = load_runner(model_dir, spec, name)
                detector = Detector(spec, runner)
                # Warm-up so the first request does not pay CUDA context / kernel selection cost.
                for _ in range(args.warmup):
                    detector.detect(blank)
                tiers.append(model_tier(name, sha, detector, rule))
            except (SystemExit, Exception) as exc:  # e.g. engine built for another GPU: start on the next tier
                LOG.error("inference tier %s unavailable at start: %s", name, exc)
                WORKER_TIER_SWITCHES.labels(name, "-", "load_failed").inc()
        for spec in getattr(args, "inject_fault", []) or []:
            name, start_s, duration_s = spec.split(":")
            for tier in tiers:
                if tier.name == name:
                    LOG.warning("TEST fault injection on %s: fails %s s..%s s after its first call", name, start_s, float(start_s) + float(duration_s))
                    FaultInjector(tier, float(start_s), float(duration_s))
        if not tiers:
            raise SystemExit("no inference tier could be loaded")
        return FallbackChain(tiers, runtime, args.fail_threshold, args.probe_interval_s, on_switch=self.on_tier_switch)

    def on_tier_switch(self, old: str, new: str, reason: str) -> None:
        WORKER_TIER_SWITCHES.labels(old, new, "recover" if reason == "probe succeeded" else "failover").inc()
        self.publish_tier()

    def publish_tier(self) -> None:
        for tier in self.chain.tiers:
            WORKER_ACTIVE_TIER.labels(tier.name).set(1 if tier is self.chain.active_tier else 0)
        WORKER_DEGRADED.set(1 if self.chain.degraded else 0)

    def observe(self, stage: str, seconds: float, runtime: str | None = None) -> None:
        WORKER_STAGE_SECONDS.labels(stage, runtime or getattr(self, "runtime_label", self.args.runtime)).observe(seconds)

    def select_chain(self, request: Request) -> tuple[FallbackChain, str, bool]:
        """(chain, rule sha, is_canary) for this request."""
        if self.releases is not None:
            snapshot = self.releases.current
            if snapshot.routes_to_candidate(request.seq):
                return snapshot.candidate.chain, snapshot.candidate.rule_sha, True
        return self.chain, self.rule_sha, False

    def process(self, request: Request) -> Result:
        t0 = time.perf_counter()
        code, decision = ResultCode.MODEL_ERROR, None
        frame_prefix = b""
        self._shadow_job = None
        chain, rule_sha, canary = self.select_chain(request)
        tier_name, tier_sha = chain.active_tier.name, chain.active_tier.sha
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
                verdict = chain.judge(image)
                tier_name, tier_sha = verdict.tier, verdict.sha
                for stage, ms in verdict.stage_ms.items():
                    self.observe(stage, ms / 1e3, verdict.tier)
                decision = verdict.decision
                code = decision.code
                if not canary:
                    self._shadow_job = (image, int(code))
                self.capture_case(request, data, image, verdict, chain, rule_sha)
        except FileNotFoundError:
            code = ResultCode.INPUT_MISMATCH
            WORKER_ERRORS.labels("frame_missing").inc()
        except AllTiersFailed:
            LOG.error("request %s/%s: every inference tier failed", request.session, request.seq)
            code = ResultCode.MODEL_ERROR
            WORKER_ERRORS.labels("all_tiers_failed").inc()
        except Exception:  # model or decode failure is reported, not raised
            LOG.exception("request %s/%s failed", request.session, request.seq)
            code = ResultCode.MODEL_ERROR
            WORKER_ERRORS.labels("model_error").inc()
        service_s = time.perf_counter() - t0
        self.observe("service", service_s)
        self.commit = (self.commit + 1) & 0xFFFF or 1
        WORKER_RESULTS.labels(code.name, getattr(self, "runtime_label", self.args.runtime), tier_name + (":canary" if canary else "")).inc()
        return Result(
            session=request.session,
            seq=request.seq,
            code=int(code),
            detections=decision.detections if decision else 0,
            qualifying=decision.qualifying if decision else 0,
            top_class=decision.top_class if decision else -1,
            top_score=decision.top_score if decision else 0.0,
            commit=self.commit,
            # The hash of the tier that actually judged, so the requester can see a fallback verdict.
            model_sha_prefix=bytes.fromhex(tier_sha[:16]),
            rule_sha_prefix=bytes.fromhex(rule_sha[:16]),
            frame_sha_prefix=frame_prefix,
            service_us=int(service_s * 1e6),
        )

    def make_transport(self):
        if self.args.transport == "opcua":
            from ovl_edge.transport import OpcUaTransport

            secure = None
            if getattr(self.args, "opcua_security", "none") == "sign_encrypt":
                from ovl_edge.opcua_security import SecureSettings

                secure = SecureSettings.worker(Path(self.args.pki_dir))
            return OpcUaTransport(self.args.opcua_url, secure=secure)
        from ovl_edge.transport import ModbusTransport

        return ModbusTransport(self.args.modbus_host, self.args.modbus_port, self.args.device_id)

    def write(self, transport, result: Result) -> bool:
        t_write = time.perf_counter()
        written = transport.write_result(result)
        self.observe("result_write", time.perf_counter() - t_write)
        if not written:
            WORKER_ERRORS.labels(f"{transport.name}_write").inc()
        return written

    def submit_shadow(self, result: Result) -> None:
        """After the active result is written: hand the same image to the shadow candidate (never delays the line)."""
        job, self._shadow_job = self._shadow_job, None
        if job is None or self.releases is None or self.shadow is None:
            return
        snapshot = self.releases.current
        if snapshot.mode == "shadow" and snapshot.candidate is not None:
            image, code = job
            self.shadow.submit(snapshot.candidate, snapshot.active.release.id, image, code, result.session, result.seq, result.frame_sha_prefix.hex())

    def step(self, transport) -> None:
        """One poll of the request slot."""
        poll_s = self.args.poll_ms / 1000.0
        WORKER_HEARTBEAT.set_to_current_time()
        # Between requests: install a release change the watcher has finished loading.
        if self.releases is not None and self.releases.apply_pending():
            self.install(self.releases.current)
            self.publish_tier()
        if not transport.connect():
            WORKER_ERRORS.labels(f"{transport.name}_connect").inc()
            error = getattr(transport, "last_error", "")
            if error and error != getattr(self, "_last_connect_error", ""):
                LOG.warning("%s connect failed: %s", transport.name, error)  # e.g. BadCertificateUntrusted
            self._last_connect_error = error
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
        self.submit_shadow(result)
        self.unwritten = None
        self.last_key, self.last_result, self.cleared = key, result, False
        LOG.debug("request session=%d seq=%d code=%d service_us=%d", request.session, request.seq, result.code, result.service_us)

    def run(self) -> int:
        transport = self.make_transport()
        LOG.info(
            "worker ready runtime=%s tiers=%s active=%s degraded=%s transport=%s engine=%s rule=%s",
            self.args.runtime,
            ",".join(t.name for t in self.chain.tiers),
            self.chain.active_tier.name,
            self.chain.degraded,
            transport.name,
            self.engine_sha[:12],
            self.rule_sha[:12],
        )
        while True:
            self.step(transport)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", default="", help="model package; not used with --registry")
    parser.add_argument("--runtime", default="trt-fp16", help="ort-cuda | trt-fp32 | trt-fp16 | trt-int8 | trt-int8_fp16")
    parser.add_argument("--rule", default="", help="decision rule JSON; missing -> every result is RuleMissing")
    parser.add_argument("--input-root", required=True)
    parser.add_argument("--frame-ext", default="png", choices=["png", "bmp"])
    parser.add_argument("--transport", default="modbus", choices=["modbus", "opcua"])
    parser.add_argument("--opcua-url", default="opc.tcp://127.0.0.1:4840/ovl/")
    parser.add_argument("--opcua-security", default=os.environ.get("OVL_OPCUA_SECURITY", "none"), choices=["none", "sign_encrypt"],
                        help="sign_encrypt: Basic256Sha256 with certificates from --pki-dir (ovl_edge.opcua_security)")
    parser.add_argument("--pki-dir", default=os.environ.get("OVL_PKI_DIR", "/pki"))
    parser.add_argument("--modbus-host", default="127.0.0.1")
    parser.add_argument("--modbus-port", type=int, default=5020)
    parser.add_argument("--device-id", type=int, default=1)
    parser.add_argument("--poll-ms", type=float, default=1.0)
    parser.add_argument("--metrics-port", type=int, default=9100)
    parser.add_argument("--warmup", type=int, default=20)
    # Fallback settings also read OVL_* environment variables so Compose can set them.
    env = os.environ.get
    parser.add_argument("--fallback", nargs="*", default=env("OVL_FALLBACK", "").split(), help="tiers after --runtime, e.g. ort-cuda rule (see ovl_edge.fallback)")
    parser.add_argument("--rule-params", default=env("OVL_RULE_PARAMS", ""), help="rule-based tier parameters JSON (from ovl_edge.cli.rule_calibrate)")
    parser.add_argument("--rule-pass-verdict", default=env("OVL_RULE_PASS_VERDICT", "Hold"), choices=["Hold", "Pass"], help="what a rule-tier Pass becomes (plant policy)")
    parser.add_argument("--fail-threshold", type=int, default=int(env("OVL_FAIL_THRESHOLD", "3")), help="consecutive failures before switching tier")
    parser.add_argument("--probe-interval-s", type=float, default=float(env("OVL_PROBE_INTERVAL_S", "30")), help="how often a degraded worker retries higher tiers")
    parser.add_argument("--inject-fault", nargs="*", default=env("OVL_INJECT_FAULT", "").split(),
                        help="TEST ONLY: tier:start_s:duration_s makes that tier fail for a time window after its first call")
    parser.add_argument("--registry", default=env("OVL_REGISTRY", ""),
                        help="release registry (ovl_edge.release): model, runtime, fallback and rule come from its active release")
    parser.add_argument("--registry-poll-s", type=float, default=float(env("OVL_REGISTRY_POLL_S", "2")))
    parser.add_argument("--cases-dir", default=env("OVL_CASES_DIR", ""), help="capture frames for review (ovl_edge.case_capture); empty = off")
    parser.add_argument("--case-near-band", type=float, default=float(env("OVL_CASE_NEAR_BAND", "0.10")))
    parser.add_argument("--case-max-per-minute", type=int, default=int(env("OVL_CASE_MAX_PER_MINUTE", "120")))
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args(argv)
    if not args.registry and not args.model_dir:
        parser.error("--model-dir or --registry is required")
    logging.basicConfig(level=args.log_level, format="%(asctime)s %(levelname)s %(name)s %(message)s")
    start_http_server(args.metrics_port)
    from ovl_edge.gpu_monitor import GpuMonitor

    GpuMonitor(interval_s=5.0).start()
    return Worker(args).run()


if __name__ == "__main__":
    sys.exit(main())
