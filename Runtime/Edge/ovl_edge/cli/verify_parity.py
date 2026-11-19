"""M1 parity check: Ultralytics .pt / Ultralytics .onnx / shared host pipeline + ORT.

Pairs (thresholds are fixed here before running and recorded in the report):
  A  ultralytics_pt   vs ultralytics_onnx  -> export fidelity
  B  ultralytics_onnx vs ours_ort_cpu      -> host pre/post-processing fidelity
  C  ultralytics_pt   vs ours_ort_cpu      -> end to end
  D  ours_ort_cpu     vs ours_ort_cuda     -> provider difference (only with --cuda, TF32 off)
  A_rect (informational, not gated): ultralytics_pt with Model.predict's default
  rect=True vs ours_ort_cpu. Shows how much the minimum-padding input of the .pt
  path differs from the fixed 640x640 contract; it is reported, not passed/failed.
All gated references run with rect=False so every path sees the same fixed
letterbox input. The first M1 run (2026-11-20) used rect=True for the .pt
reference and a single-gain box restore; both failures are kept in its report.

    python -m ovl_edge.cli.verify_parity --model-dir /out/model --images /data/images --out /out/m1_parity.json
"""

from __future__ import annotations

import argparse
import json
import platform
import sys
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.matching import compare
from ovl_edge.runners import Detector, OrtRunner, UltralyticsReference
from ovl_edge.spec import ModelSpec, sha256_file

# Fixed before the first run. B and D must agree almost exactly because the same
# ONNX file and the same pre/post arithmetic are used. A and C allow for
# Ultralytics' .pt path using rectangular (minimum) padding instead of a fixed
# 640x640 letterbox, which changes the network input slightly.
TOLERANCES = {
    "A": {"match_iou": 0.5, "min_match_rate": 0.95, "max_score_diff": 0.05, "min_iou": 0.90},
    "B": {"match_iou": 0.5, "min_match_rate": 1.0, "max_score_diff": 1e-3, "min_iou": 0.999},
    "C": {"match_iou": 0.5, "min_match_rate": 0.95, "max_score_diff": 0.05, "min_iou": 0.90},
    "D": {"match_iou": 0.5, "min_match_rate": 0.98, "max_score_diff": 5e-3, "min_iou": 0.995},
}
IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".bmp"}


def _synthetic_cases() -> list[tuple[str, np.ndarray]]:
    # Empty-detection case and a strongly non-square frame.
    flat = np.full((480, 640, 3), 128, np.uint8)
    wide = np.zeros((200, 1000, 3), np.uint8)
    cv2.randu(wide, 0, 255)
    return [("synthetic/flat_gray_480x640", flat), ("synthetic/noise_200x1000", wide)]


def _evaluate(pair: str, rows: list[dict]) -> dict:
    tol = TOLERANCES[pair]
    total_a = sum(r["count_a"] for r in rows)
    total_b = sum(r["count_b"] for r in rows)
    matched = sum(r["matched"] for r in rows)
    denom = max(total_a, total_b)
    match_rate = 1.0 if denom == 0 else matched / denom
    score_diffs = [r["max_score_diff"] for r in rows if r["max_score_diff"] is not None]
    ious = [r["min_iou"] for r in rows if r["min_iou"] is not None]
    worst_score = max(score_diffs) if score_diffs else 0.0
    worst_iou = min(ious) if ious else 1.0
    passed = match_rate >= tol["min_match_rate"] and worst_score <= tol["max_score_diff"] and worst_iou >= tol["min_iou"]
    return {
        "tolerance": tol,
        "detections_a": total_a,
        "detections_b": total_b,
        "matched": matched,
        "match_rate": round(match_rate, 6),
        "worst_score_diff": worst_score,
        "worst_matched_iou": worst_iou,
        "images_with_count_mismatch": sum(1 for r in rows if r["count_a"] != r["count_b"]),
        "passed": bool(passed),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--images", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--list", default="", help="split file naming the images to use")
    parser.add_argument("--cuda", action="store_true", help="also run ORT CUDA and compare (pair D)")
    args = parser.parse_args(argv)

    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    onnx_path = model_dir / spec.onnx_file
    pt_path = model_dir / spec.source_weights
    if sha256_file(onnx_path) != spec.onnx_sha256 or sha256_file(pt_path) != spec.source_sha256:
        raise SystemExit("Model files do not match model_spec.json hashes")

    if args.list:
        from ovl_edge.splits import read_list

        image_paths = read_list(args.images, args.list)
    else:
        image_paths = sorted(p for p in Path(args.images).iterdir() if p.suffix.lower() in IMAGE_SUFFIXES)
    if args.limit:
        image_paths = image_paths[: args.limit]
    cases: list[tuple[str, np.ndarray]] = []
    for path in image_paths:
        image = cv2.imread(str(path), cv2.IMREAD_COLOR)
        if image is None:
            raise SystemExit(f"Cannot decode {path}")
        cases.append((path.name, image))
    cases.extend(_synthetic_cases())

    ref_pt = UltralyticsReference(str(pt_path), spec, device="cpu", rect=False)
    ref_pt_rect = UltralyticsReference(str(pt_path), spec, device="cpu", rect=True)
    ref_onnx = UltralyticsReference(str(onnx_path), spec, device="cpu", rect=False)
    ours_cpu = Detector(spec, OrtRunner(str(onnx_path), "cpu"))
    ours_cuda = Detector(spec, OrtRunner(str(onnx_path), "cuda", use_tf32=False)) if args.cuda else None

    pairs = {"A": [], "B": [], "C": []}
    if ours_cuda:
        pairs["D"] = []
    informational: list[dict] = []
    per_image = []
    for name, image in cases:
        d_pt = ref_pt.detect(image)
        d_onnx = ref_onnx.detect(image)
        d_cpu, _ = ours_cpu.detect(image)
        row = {"image": name, "shape_hw": list(image.shape[:2])}
        for key, (a, b) in {"A": (d_pt, d_onnx), "B": (d_onnx, d_cpu), "C": (d_pt, d_cpu)}.items():
            result = compare(a, b, TOLERANCES[key]["match_iou"]).to_dict()
            pairs[key].append(result)
            row[key] = result
        rect_result = compare(ref_pt_rect.detect(image), d_cpu, 0.5).to_dict()
        informational.append(rect_result)
        row["A_rect"] = rect_result
        if ours_cuda:
            d_cuda, _ = ours_cuda.detect(image)
            result = compare(d_cpu, d_cuda, TOLERANCES["D"]["match_iou"]).to_dict()
            pairs["D"].append(result)
            row["D"] = result
        per_image.append(row)

    import onnxruntime
    import ultralytics

    summary = {key: _evaluate(key, rows) for key, rows in pairs.items()}
    rect_info = _evaluate("A", informational)
    rect_info.pop("passed")
    rect_info.pop("tolerance")
    report = {
        "check": "M1 YOLO->ONNX parity",
        "model_spec": json.loads((model_dir / "model_spec.json").read_text(encoding="utf-8")),
        "images_dir": str(args.images),
        "image_count": len(image_paths),
        "synthetic_cases": [c[0] for c in _synthetic_cases()],
        "non_square_images": sum(1 for _, im in cases if im.shape[0] != im.shape[1]),
        "runtime": {
            "python": platform.python_version(),
            "onnxruntime": onnxruntime.__version__,
            "ultralytics": ultralytics.__version__,
            "ort_cpu_providers": ours_cpu.runner.active_providers,
            "ort_cuda_providers": ours_cuda.runner.active_providers if ours_cuda else None,
        },
        "summary": summary,
        "informational": {"A_rect": rect_info},
        "passed": all(v["passed"] for v in summary.values()),
        "per_image": per_image,
    }
    Path(args.out).write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "summary": summary, "informational": report["informational"]}, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
