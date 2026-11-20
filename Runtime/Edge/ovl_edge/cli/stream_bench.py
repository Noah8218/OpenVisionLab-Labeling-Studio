"""Stream benchmark: sequential vs staged pipeline on a simulated camera stream.

A simulated camera emits encoded frames (held in memory, so file I/O is not
measured) at a fixed rate, or as fast as the pipeline accepts them (--fps 0).
Each frame goes decode -> preprocess -> infer -> postprocess -> judgment.

  sequential : one worker runs the whole chain per frame
  pipelined  : one thread per stage (decode/preprocess may use several workers),
               bounded queues between stages, one inference thread (one CUDA stream)

Latency is measured per frame from its arrival to its verdict with one clock.
A frame that arrives while the first queue is full is dropped and counted.

    python -m ovl_edge.cli.stream_bench --model-dir /model --runtime trt-fp16 --images /data/images \
        --list /data/lists/test.txt --rule /deploy/rule.hexagon-defect8.json --fps 0 100 200 --out /out/stream.json
"""

from __future__ import annotations

import argparse
import json
import queue
import sys
import time
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.cli.serve import load_runner
from ovl_edge.decision import DecisionRule, decide
from ovl_edge.pipeline import Item, Pipeline, Stage, percentile
from ovl_edge.postprocess import decode
from ovl_edge.preprocess import preprocess
from ovl_edge.spec import ModelSpec
from ovl_edge.splits import read_list


def build_stages(spec: ModelSpec, runners: list, rule, mode: str, decode_workers: int, pre_workers: int, infer_workers: int = 1) -> list[Stage]:
    pad = int(spec.letterbox["pad_value"])
    runner = runners[0]
    # Each runner owns its execution context, CUDA stream and pinned buffers, so two
    # inference workers never share a buffer; a worker borrows one runner per frame.
    pool: queue.Queue = queue.Queue()
    for r in runners[: max(1, infer_workers)]:
        pool.put(r)

    def s_decode(data: bytes):
        image = cv2.imdecode(np.frombuffer(data, np.uint8), cv2.IMREAD_COLOR)
        if image is None:
            raise ValueError("frame decode failed")
        return image

    def s_pre(image):
        return preprocess(image, spec.input_hw, dtype=runner.input_dtype, pad_value=pad)

    def s_infer(value):
        tensor, info = value
        borrowed = pool.get()
        try:
            return borrowed.infer(tensor), info
        finally:
            pool.put(borrowed)

    def s_post(value):
        raw, info = value
        detections = decode(raw, spec.num_classes, spec.nms, info)
        return decide(detections, rule).code

    if mode == "sequential":
        return [Stage("all", lambda data: s_post(s_infer(s_pre(s_decode(data)))))]
    return [
        Stage("decode", s_decode, decode_workers),
        Stage("preprocess", s_pre, pre_workers),
        Stage("infer", s_infer, max(1, infer_workers)),
        Stage("postprocess", s_post, 1),
    ]


def _summary(values: list[float]) -> dict:
    return {"p50": percentile(values, 0.5), "p99": percentile(values, 0.99), "max": max(values) if values else float("nan")}


def run_stream(stages: list[Stage], frames: list[bytes], count: int, fps: float, queue_size: int) -> dict:
    pipeline = Pipeline(stages, queue_size=queue_size).start()
    period = 1.0 / fps if fps > 0 else 0.0
    dropped = 0
    late_emits = 0
    t0 = time.perf_counter()
    for k in range(count):
        if period:
            target = t0 + k * period
            now = time.perf_counter()
            if target > now:
                time.sleep(target - now)
            elif now - target > period:
                late_emits += 1  # the source itself fell behind its schedule
        item = Item(seq=k, t_arrival=time.perf_counter(), value=frames[k % len(frames)])
        if not pipeline.submit(item, block=(period == 0.0)):
            dropped += 1
    pipeline.close()
    done = list(pipeline.drain())
    pipeline.join(5.0)

    errors = [i for i in done if i.error is not None]
    ok = [i for i in done if i.error is None]
    latency_ms = [(i.t_done - i.t_arrival) * 1e3 for i in ok]
    span = max(i.t_done for i in done) - min(i.t_arrival for i in done) if done else float("nan")
    stage_names = [s.name for s in stages]
    return {
        "frames": count,
        "completed": len(ok),
        "errors": len(errors),
        "first_error": repr(errors[0].error) if errors else None,
        "dropped": dropped,
        "source_late_emits": late_emits,
        "throughput_fps": len(done) / span if span and span > 0 else float("nan"),
        "latency_ms": {"p50": percentile(latency_ms, 0.5), "p99": percentile(latency_ms, 0.99), "max": max(latency_ms) if latency_ms else float("nan")},
        # Per-stage percentiles locate a latency tail: the stage whose own p99/max is large is the stall.
        "service_ms": {n: _summary([i.service_s[n] * 1e3 for i in ok]) for n in stage_names},
        "wait_ms": {n: _summary([i.wait_s[n] * 1e3 for i in ok]) for n in stage_names},
        "slowest_frames": [
            {"seq": i.seq, "latency_ms": round((i.t_done - i.t_arrival) * 1e3, 2),
             "service_ms": {n: round(v * 1e3, 2) for n, v in i.service_s.items()},
             "wait_ms": {n: round(v * 1e3, 2) for n, v in i.wait_s.items()}}
            for i in sorted(ok, key=lambda i: i.t_done - i.t_arrival, reverse=True)[:5]
        ],
        "verdicts": {str(code): sum(1 for i in ok if int(i.value) == code) for code in sorted({int(i.value) for i in ok})},
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--runtime", default="trt-fp16")
    parser.add_argument("--images", required=True)
    parser.add_argument("--list", default="", help="split file naming the frames to stream")
    parser.add_argument("--rule", required=True)
    parser.add_argument("--count", type=int, default=2000, help="frames per run")
    parser.add_argument("--fps", type=float, nargs="+", default=[0.0], help="camera rates to test; 0 = as fast as accepted")
    parser.add_argument("--modes", nargs="+", default=["sequential", "pipelined"], choices=["sequential", "pipelined"])
    parser.add_argument("--decode-workers", type=int, default=1)
    parser.add_argument("--pre-workers", type=int, default=1)
    parser.add_argument("--infer-workers", type=int, nargs="+", default=[1], help="inference workers to test; each gets its own runner (context, stream, buffers)")
    parser.add_argument("--queue-size", type=int, default=4)
    parser.add_argument("--budget-ms", type=float, default=50.0, help="cycle-time budget per frame (arrival -> verdict)")
    parser.add_argument("--warmup", type=int, default=100)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)

    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    runners, engine_sha = [], ""
    for _ in range(max(args.infer_workers)):
        r, engine_sha = load_runner(model_dir, spec, args.runtime)
        runners.append(r)
    rule = DecisionRule.from_dict(json.loads(Path(args.rule).read_text(encoding="utf-8")))
    paths = read_list(args.images, args.list) if args.list else sorted(p for p in Path(args.images).iterdir() if p.suffix.lower() in {".png", ".bmp", ".jpg"})
    frames = [p.read_bytes() for p in paths]

    blank = np.full((*spec.input_hw, 3), 114, np.uint8)
    for r in runners:
        for _ in range(args.warmup):
            r.infer(preprocess(blank, spec.input_hw, dtype=r.input_dtype)[0])

    runs = []
    for fps in args.fps:
        for mode in args.modes:
            for infer_workers in (args.infer_workers if mode == "pipelined" else [1]):
                stages = build_stages(spec, runners, rule, mode, args.decode_workers, args.pre_workers, infer_workers)
                result = run_stream(stages, frames, args.count, fps, args.queue_size)
                result.update({"mode": mode, "fps": fps, "decode_workers": args.decode_workers if mode == "pipelined" else 1,
                               "pre_workers": args.pre_workers if mode == "pipelined" else 1, "infer_workers": infer_workers})
                result["meets_budget"] = bool(result["dropped"] == 0 and result["errors"] == 0 and result["latency_ms"]["p99"] <= args.budget_ms)
                runs.append(result)
                infer = result["service_ms"].get("infer", result["service_ms"].get("all"))
                print(f"{mode:10s} infer_w={infer_workers} fps={fps:6.1f} done={result['completed']:5d} drop={result['dropped']:5d} "
                      f"thr={result['throughput_fps']:7.1f}/s p50={result['latency_ms']['p50']:7.2f} p99={result['latency_ms']['p99']:7.2f} ms "
                      f"infer/all p50={infer['p50']:.2f} p99={infer['p99']:.2f} budget={'OK' if result['meets_budget'] else 'NO'}", flush=True)

    report = {
        "runtime": args.runtime,
        "engine_sha256": engine_sha,
        "frames_source": {"images": len(frames), "list": args.list or None},
        "queue_size": args.queue_size,
        "budget_ms": args.budget_ms,
        "runs": runs,
    }
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
