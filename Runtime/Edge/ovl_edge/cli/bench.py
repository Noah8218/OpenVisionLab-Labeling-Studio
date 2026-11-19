"""M2 benchmark: ORT CUDA FP32 vs TensorRT FP32 / FP16 / INT8 on one GPU.

Method (revised after trials 1-3 on 2026-11-20 showed run-to-run drift of the
same path larger than the differences between paths):
  1. every path is loaded and warmed up first (load / warm-up recorded separately)
  2. sequential batch-1 latency is measured INTERLEAVED: for each repeat and each
     evaluation image, every path runs once in a rotating order, so clock or load
     drift affects all paths alike. infer span = host tensor ready -> H2D -> enqueue
     -> D2H -> stream sync (output readable); TensorRT uses pinned host buffers
  3. GPU compute only and maximum single-stream throughput come from trtexec
     (--loadEngine, data transfers off, CUDA graph on: TensorRT 11 defaults)
  4. accuracy: mAP50 / mAP50-95 on the evaluation split with val-style NMS
     (conf 0.001, iou 0.7); the judgment NMS (conf 0.25, iou 0.45) is not used for mAP
Percentiles come straight from the stored per-request samples, not from buckets.

    python -m ovl_edge.cli.bench --model-dir /out/model --images /data/images --labels /data/labels --out /out/m2_bench.json
"""

from __future__ import annotations

import argparse
import json
import platform
import re
import subprocess
import sys
import time
from dataclasses import replace
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.decision import DecisionRule, ResultCode, decide
from ovl_edge.metrics import load_yolo_labels, mean_average_precision
from ovl_edge.runners import Detector, OrtRunner
from ovl_edge.spec import ModelSpec, sha256_file
from ovl_edge.splits import list_digest, read_list, split_images

VAL_NMS = {"conf": 0.001, "iou": 0.7}
ENGINE_ROWS = (
    ("fp32", "TensorRT FP32"),
    ("fp16", "TensorRT FP16"),
    ("int8", "TensorRT INT8 (Q/DQ, 나머지 FP32)"),
    ("int8_fp16", "TensorRT INT8 (Q/DQ, 나머지 FP16)"),
)
TRTEXEC_STAT = re.compile(r"GPU Compute Time: min = ([\d.]+) ms, max = ([\d.]+) ms, mean = ([\d.]+) ms, median = ([\d.]+) ms.*?percentile\(99%\) = ([\d.]+) ms")
TRTEXEC_QPS = re.compile(r"Throughput: ([\d.]+) qps")


def _percentiles(samples: list[float]) -> dict:
    arr = np.asarray(samples, dtype=np.float64)
    return {
        "n": int(arr.size),
        "p50": float(np.percentile(arr, 50)),
        "p90": float(np.percentile(arr, 90)),
        "p99": float(np.percentile(arr, 99)),
        "max": float(arr.max()),
        "mean": float(arr.mean()),
    }


def _gpu_name() -> str:
    try:
        return subprocess.run(["nvidia-smi", "--query-gpu=name,driver_version", "--format=csv,noheader"], capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return "unknown"


def trtexec_compute(engine: Path, iterations: int, warmup_ms: int) -> dict:
    proc = subprocess.run(
        ["trtexec", f"--loadEngine={engine}", f"--iterations={iterations}", f"--warmUp={warmup_ms}", "--duration=0"],
        capture_output=True,
        text=True,
    )
    text = proc.stdout + proc.stderr
    stat, qps = TRTEXEC_STAT.search(text), TRTEXEC_QPS.search(text)
    if proc.returncode != 0 or not stat or not qps:
        return {"error": f"trtexec exit {proc.returncode}", "tail": text.splitlines()[-5:]}
    return {
        "gpu_compute_ms": {"min": float(stat.group(1)), "max": float(stat.group(2)), "mean": float(stat.group(3)), "p50": float(stat.group(4)), "p99": float(stat.group(5))},
        "throughput_qps": float(qps.group(1)),
        "iterations": iterations,
        "note": "trtexec defaults in TensorRT 11: no H2D/D2H transfers, CUDA graph enabled",
    }


def judge_images(detector: Detector, rule: DecisionRule, images, gts) -> dict:
    """Image-level judgment with the serving NMS; an image is NG when its label has any box."""
    ng = ok = missed_ng = false_ng = 0
    other: dict[str, int] = {}
    missed_names, false_names = [], []
    for (name, image), gt in zip(images, gts):
        code = decide(detector.detect(image)[0], rule).code
        is_ng = gt.classes.size > 0
        if code not in (ResultCode.PASS, ResultCode.NG):
            other[code.name] = other.get(code.name, 0) + 1
            continue
        if is_ng:
            ng += 1
            if code == ResultCode.PASS:
                missed_ng += 1
                missed_names.append(name)
        else:
            ok += 1
            if code == ResultCode.NG:
                false_ng += 1
                false_names.append(name)
    return {
        "rule": {"ng_classes": list(rule.ng_classes), "min_score": rule.min_score, "min_count": rule.min_count, "no_detection_verdict": rule.no_detection_verdict},
        "ng_images": ng,
        "ok_images": ok,
        "missed_ng": missed_ng,
        "false_ng": false_ng,
        "miss_rate": missed_ng / ng if ng else None,
        "false_ng_rate": false_ng / ok if ok else None,
        "other_codes": other,
        "missed_ng_images": missed_names,
        "false_ng_images": false_names,
    }


class PathUnderTest:
    def __init__(self, label: str, kind: str, path: Path, spec: ModelSpec):
        self.label, self.kind, self.path = label, kind, path
        t0 = time.perf_counter()
        if kind == "ort-cuda":
            self.runner = OrtRunner(str(path), "cuda", use_tf32=False)
        else:
            from ovl_edge.trt_runner import TrtRunner

            self.runner = TrtRunner(path, name=kind)
        self.load_ms = (time.perf_counter() - t0) * 1e3
        self.detector = Detector(spec, self.runner)
        self.val_detector = Detector(replace(spec, nms={**spec.nms, **VAL_NMS}), self.runner)
        self.pre: list[float] = []
        self.infer: list[float] = []
        self.post: list[float] = []
        self.total: list[float] = []
        self.warmup_ms = 0.0

    def measure(self, image: np.ndarray) -> None:
        t0 = time.perf_counter()
        _, times = self.detector.detect(image)
        self.total.append((time.perf_counter() - t0) * 1e3)
        self.pre.append(times.preprocess_ms)
        self.infer.append(times.infer_ms)
        self.post.append(times.postprocess_ms)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--images", required=True)
    parser.add_argument("--labels", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--calibration-count", type=int, default=64)
    parser.add_argument("--eval-list", default="", help="split file naming the evaluation images")
    parser.add_argument("--rule", default="", help="judgment rule JSON for missed-NG / false-NG rates")
    parser.add_argument("--repeats", type=int, default=30)
    parser.add_argument("--warmup", type=int, default=100)
    parser.add_argument("--trtexec-iterations", type=int, default=2000)
    args = parser.parse_args(argv)

    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    manifest = json.loads((model_dir / "engines" / "engines.json").read_text(encoding="utf-8"))
    if sha256_file(model_dir / spec.onnx_file) != spec.onnx_sha256:
        raise SystemExit(f"{spec.onnx_file} does not match model_spec.json")
    if args.eval_list:
        eval_paths = read_list(args.images, args.eval_list)
    else:
        _, eval_paths = split_images(args.images, args.calibration_count)
    rule = None
    if args.rule:
        rule = DecisionRule.from_dict(json.loads(Path(args.rule).read_text(encoding="utf-8")))
        if rule is None:
            raise SystemExit("Judgment rule is missing no_detection_verdict")
    if list_digest(eval_paths) == manifest.get("calibration_list_sha256"):
        raise SystemExit("Evaluation list equals calibration list")
    if set(p.name for p in eval_paths) & set(manifest.get("calibration_images", [])):
        raise SystemExit("Evaluation and calibration images overlap")
    images, gts = [], []
    for path in eval_paths:
        image = cv2.imread(str(path), cv2.IMREAD_COLOR)
        images.append((path.name, image))
        gts.append(load_yolo_labels(Path(args.labels) / (path.stem + ".txt"), image.shape[1], image.shape[0]))

    paths = [PathUnderTest("ONNX Runtime CUDA FP32", "ort-cuda", model_dir / spec.onnx_file, spec)]
    for key, label in ENGINE_ROWS:
        engine = manifest["engines"].get(key)
        if engine:
            paths.append(PathUnderTest(label, f"trt-{key}", model_dir / "engines" / engine["engine"], spec))

    for put in paths:
        t0 = time.perf_counter()
        for i in range(args.warmup):
            put.detector.detect(images[i % len(images)][1])
        put.warmup_ms = (time.perf_counter() - t0) * 1e3

    t_start = time.perf_counter()
    rotation = 0
    for _ in range(args.repeats):
        for _, image in images:
            order = paths[rotation % len(paths):] + paths[: rotation % len(paths)]
            rotation += 1
            for put in order:
                put.measure(image)
    interleaved_s = time.perf_counter() - t_start

    results = []
    for put in paths:
        pairs = [(put.val_detector.detect(image)[0], gt) for (_, image), gt in zip(images, gts)]
        row = {
            "label": put.label,
            "kind": put.kind,
            "artifact": put.path.name,
            "artifact_sha256": sha256_file(put.path),
            "input_dtype": str(np.dtype(put.runner.input_dtype)),
            "load_ms": put.load_ms,
            "warmup_iterations": args.warmup,
            "warmup_ms": put.warmup_ms,
            "requests": len(put.total),
            "host_total_sum_s": sum(put.total) / 1e3,
            "throughput_seq_batch1_per_s": len(put.total) / (sum(put.total) / 1e3),
            "infer_ms": _percentiles(put.infer),
            "preprocess_ms": _percentiles(put.pre),
            "postprocess_ms": _percentiles(put.post),
            "host_total_ms": _percentiles(put.total),
            "accuracy": mean_average_precision(pairs),
        }
        if hasattr(put.runner, "active_providers"):
            row["providers"] = put.runner.active_providers
        if rule is not None:
            row["judgment"] = judge_images(put.detector, rule, images, gts)
        results.append(row)
    for put in paths:
        if hasattr(put.runner, "close"):
            put.runner.close()
    for row, put in zip(results, paths):
        if put.kind.startswith("trt-"):
            row["trtexec"] = trtexec_compute(put.path, args.trtexec_iterations, 500)

    import onnxruntime

    try:
        import tensorrt

        trt_version = tensorrt.__version__
    except ImportError:
        trt_version = None
    report = {
        "check": "M2 runtime/precision benchmark (interleaved)",
        "gpu": _gpu_name(),
        "python": platform.python_version(),
        "onnxruntime": onnxruntime.__version__,
        "tensorrt": trt_version,
        "model_spec_sha256": sha256_file(model_dir / "model_spec.json"),
        "evaluation_images": len(eval_paths),
        "evaluation_list_sha256": list_digest(eval_paths),
        "calibration_list_sha256": manifest.get("calibration_list_sha256"),
        "repeats": args.repeats,
        "interleaved_wall_s": interleaved_s,
        "val_nms": VAL_NMS,
        "judgment_nms": {k: spec.nms[k] for k in ("conf", "iou")},
        "engine_manifest": manifest,
        "results": results,
    }
    Path(args.out).write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    header = "| 실행 경로 | 추론 p50 / p99 (ms) | 호스트 전체 p99 (ms) | 순차 처리량 (장/s) | GPU 계산 p50 / p99 (ms, trtexec) | 최대 처리량 (qps, trtexec) | mAP50 | mAP50–95 | 불량 미검출률 / 정상 오검출률 |"
    lines = [header, "| --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for r in results:
        t = r.get("trtexec", {})
        gpu = f"{t['gpu_compute_ms']['p50']:.3f} / {t['gpu_compute_ms']['p99']:.3f}" if "gpu_compute_ms" in t else "—"
        qps = f"{t['throughput_qps']:.0f}" if "throughput_qps" in t else "—"
        j = r.get("judgment")
        judged = f"{j['missed_ng']}/{j['ng_images']} / {j['false_ng']}/{j['ok_images']}" if j else "—"
        lines.append(
            f"| {r['label']} | {r['infer_ms']['p50']:.3f} / {r['infer_ms']['p99']:.3f} | {r['host_total_ms']['p99']:.3f} | "
            f"{r['throughput_seq_batch1_per_s']:.1f} | {gpu} | {qps} | {r['accuracy']['map50']:.4f} | {r['accuracy']['map50_95']:.4f} | {judged} |"
        )
    table = "\n".join(lines)
    Path(args.out).with_suffix(".md").write_text(table + "\n", encoding="utf-8")
    print(table)
    return 0


if __name__ == "__main__":
    sys.exit(main())
