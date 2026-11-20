"""Check the native C++/CUDA path (native/trt_native_bench) against the Python worker path.

    python -m ovl_edge.cli.native_check frames --images /data/images --list /data/lists/test.txt --out /out/frames.bin
    # ... run native/trt_native_bench in cpu and gpu mode with --out-raw and --dump-input ...
    python -m ovl_edge.cli.native_check compare --model-dir /model --images /data/images --list /data/lists/test.txt \
        --rule /deploy/rule.hexagon-defect8.json --native gpu=/out/raw_gpu.bin:/out/in_gpu.bin cpu=/out/raw_cpu.bin:/out/in_cpu.bin \
        --out /out/native_check.json

compare reports, per native mode: input tensor difference from ovl_edge.preprocess (gray
levels), raw head difference from the Python TensorRT run of the same engine, verdict
agreement, and the Python path's preprocess + inference time for the same frames.
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
import time
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.cli.serve import load_runner
from ovl_edge.decision import DecisionRule, decide
from ovl_edge.pipeline import percentile
from ovl_edge.postprocess import decode
from ovl_edge.preprocess import preprocess
from ovl_edge.spec import ModelSpec
from ovl_edge.splits import read_list


def load_images(args) -> list[np.ndarray]:
    images = [cv2.imread(str(p), cv2.IMREAD_COLOR) for p in read_list(args.images, args.list)]
    if args.scale_to:  # imitate a higher-resolution camera with the same content
        w, h = (int(v) for v in args.scale_to.lower().split("x"))
        images = [cv2.resize(img, (w, h), interpolation=cv2.INTER_LINEAR) for img in images]
    return images


def cmd_frames(args) -> int:
    images = load_images(args)
    h, w = images[0].shape[:2]
    if any(img.shape != images[0].shape for img in images):
        raise SystemExit("frames file needs one image size")
    with open(args.out, "wb") as handle:
        handle.write(b"OVLF" + struct.pack("<iii", len(images), h, w))
        for img in images:
            handle.write(np.ascontiguousarray(img).tobytes())
    print(json.dumps({"frames": len(images), "height": h, "width": w}))
    return 0


def cmd_compare(args) -> int:
    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    rule = DecisionRule.from_dict(json.loads(Path(args.rule).read_text(encoding="utf-8")))
    runner, _ = load_runner(model_dir, spec, args.runtime)
    images = load_images(args)
    pad = int(spec.letterbox["pad_value"])

    py_tensors, py_raw, py_info, py_codes, py_ms = [], [], [], [], []
    for img in images[:20]:
        runner.infer(preprocess(img, spec.input_hw, pad_value=pad)[0])  # warm-up
    for rep in range(args.python_repeats):
        for img in images:
            t0 = time.perf_counter()
            tensor, info = preprocess(img, spec.input_hw, pad_value=pad)
            raw = runner.infer(tensor)
            py_ms.append((time.perf_counter() - t0) * 1e3)
            if rep == 0:
                py_tensors.append(tensor) if len(py_tensors) < args.dump_count else None
                py_raw.append(raw)
                py_info.append(info)
                py_codes.append(decide(decode(raw, spec.num_classes, spec.nms, info), rule).code)

    report = {"python": {"path": "ovl_edge.preprocess + TrtRunner.infer (Python)", "ms": {"p50": percentile(py_ms, 0.5), "p99": percentile(py_ms, 0.99)}}}
    out_elems = int(np.prod(py_raw[0].shape))
    in_elems = int(np.prod(py_tensors[0].shape))
    for item in args.native:
        mode, paths = item.split("=", 1)
        raw_path, dump_path = paths.split(":")
        raw = np.fromfile(raw_path, np.float32).reshape(-1, *py_raw[0].shape)
        if len(raw) != len(images):
            raise SystemExit(f"{raw_path}: {len(raw)} outputs for {len(images)} frames")
        dump = np.fromfile(dump_path, np.float32).reshape(-1, *py_tensors[0].shape)
        gray = np.concatenate([np.abs(dump[i] - py_tensors[i]).ravel() for i in range(len(dump))]) * 255.0
        codes = [decide(decode(raw[i], spec.num_classes, spec.nms, py_info[i]), rule).code for i in range(len(images))]
        agree = sum(a == b for a, b in zip(codes, py_codes)) / len(codes)
        raw_diff = max(float(np.max(np.abs(raw[i] - py_raw[i]))) for i in range(len(images)))
        score_diff = max(float(np.max(np.abs(raw[i][0, 4:] - py_raw[i][0, 4:]))) for i in range(len(images)))
        report[mode] = {"input_gray_level_diff": {"max": float(gray.max()), "share_nonzero": float((gray > 0.5).mean()), "tensors": len(dump)},
                        "raw_head_max_abs_diff": raw_diff, "score_max_abs_diff": score_diff, "verdict_agreement": agree,
                        "verdict_differences": int(sum(a != b for a, b in zip(codes, py_codes)))}
        print(f"{mode}: input max diff {gray.max():.1f} gray levels ({(gray > 0.5).mean():.4%} of values differ), "
              f"score max diff {score_diff:.5f}, verdict agreement {agree:.4f}", flush=True)
    print(f"python path: preprocess + infer p50 {report['python']['ms']['p50']:.3f} ms p99 {report['python']['ms']['p99']:.3f} ms")
    Path(args.out).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("frames")
    p.add_argument("--images", required=True)
    p.add_argument("--list", required=True)
    p.add_argument("--scale-to", default="", help="WxH: resize frames first (higher-resolution camera)")
    p.add_argument("--out", required=True)
    p = sub.add_parser("compare")
    p.add_argument("--scale-to", default="")
    p.add_argument("--model-dir", required=True)
    p.add_argument("--runtime", default="trt-fp16")
    p.add_argument("--images", required=True)
    p.add_argument("--list", required=True)
    p.add_argument("--rule", required=True)
    p.add_argument("--native", nargs="+", required=True, help="mode=raw.bin:dump.bin")
    p.add_argument("--dump-count", type=int, default=50)
    p.add_argument("--python-repeats", type=int, default=3)
    p.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    return {"frames": cmd_frames, "compare": cmd_compare}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
