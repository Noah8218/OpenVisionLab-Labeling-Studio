"""Compare runtimes per image on one model: latency (preprocess + infer + postprocess) and verdicts.

    python -m ovl_edge.cli.cpu_bench --model-dir /model --images /data/images --labels /data/labels \
        --list /data/lists/test.txt --rule /deploy/rule.hexagon-defect8.json \
        --runtimes ort-cpu ov-cpu ov-cpu-bf16 ov-cpu-int8 trt-fp16 --out /out/cpu_bench.json

Frames are decoded once beforehand, so file and decode time are not included.
Verdicts are compared with the labels (escapes, false rejects) and with the first
runtime in the list (agreement), which should be the FP32 reference.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.cli.serve import load_runner
from ovl_edge.decision import DecisionRule, ResultCode, decide
from ovl_edge.pipeline import percentile
from ovl_edge.runners import Detector
from ovl_edge.spec import ModelSpec
from ovl_edge.splits import read_list


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--images", required=True)
    parser.add_argument("--labels", required=True)
    parser.add_argument("--list", required=True)
    parser.add_argument("--rule", required=True)
    parser.add_argument("--runtimes", nargs="+", required=True)
    parser.add_argument("--warmup", type=int, default=30)
    parser.add_argument("--repeats", type=int, default=2)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)

    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    rule = DecisionRule.from_dict(json.loads(Path(args.rule).read_text(encoding="utf-8")))
    paths = read_list(args.images, args.list)
    images = [cv2.imread(str(p), cv2.IMREAD_COLOR) for p in paths]
    truth = [bool((Path(args.labels) / (p.stem + ".txt")).read_text(encoding="utf-8").strip()) for p in paths]

    reference = None
    rows = []
    for runtime in args.runtimes:
        runner, sha = load_runner(model_dir, spec, runtime)
        detector = Detector(spec, runner)
        for img in images[: args.warmup]:
            detector.detect(img)
        times, codes = [], []
        for rep in range(args.repeats):
            for img in images:
                t0 = time.perf_counter()
                dets, _ = detector.detect(img)
                times.append((time.perf_counter() - t0) * 1e3)
                if rep == 0:
                    codes.append(decide(dets, rule).code)
        esc = sum(1 for c, t in zip(codes, truth) if t and c == ResultCode.PASS)
        fr = sum(1 for c, t in zip(codes, truth) if not t and c == ResultCode.NG)
        if reference is None:
            reference = codes
        agree = sum(1 for a, b in zip(codes, reference) if a == b) / len(codes)
        row = {"runtime": runtime, "precision": getattr(runner, "precision", None), "images": len(images), "timed": len(times),
               "ms": {"p50": percentile(times, 0.5), "p99": percentile(times, 0.99), "mean": float(np.mean(times))},
               "throughput_seq_per_s": 1000.0 / float(np.mean(times)),
               "escapes": esc, "ng": sum(truth), "false_rejects": fr, "ok": len(truth) - sum(truth), "agreement_with_first": agree}
        rows.append(row)
        print(f"{runtime:12s} p50 {row['ms']['p50']:6.2f} p99 {row['ms']['p99']:6.2f} ms  {row['throughput_seq_per_s']:6.1f}/s  "
              f"escape {esc}/{row['ng']} false_reject {fr}/{row['ok']}  agree {agree:.4f}", flush=True)
        close = getattr(runner, "close", None)
        if close:
            close()
    Path(args.out).write_text(json.dumps({"model_spec": spec.onnx_sha256, "rule": rule.digest(), "runs": rows}, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
