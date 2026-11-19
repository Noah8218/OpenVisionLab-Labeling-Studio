"""M2 preparation: build TensorRT FP32 / FP16 / INT8 engines from one exported ONNX.

TensorRT 11 builds strongly typed networks: precision comes from the ONNX
tensor types, not from builder flags (design E9).
  FP32 : the exported FP32 ONNX
  FP16 : the same graph converted to FP16 by Model Optimizer AutoCast (I/O kept FP32)
  INT8 : explicit Q/DQ ONNX from Model Optimizer post-training quantization,
         calibrated on the calibration split only. Ops left unquantized stay FP32
         ('int8') or are cast to FP16 ('int8_fp16', ModelOpt's default).
Model Optimizer runs in $OVL_MODELOPT_PYTHON (separate virtualenv, see Dockerfile).
Writes engines/engines.json with hashes, build times, calibration list digest
and the per-precision layer histogram exported by trtexec.

    python -m ovl_edge.cli.build_engines --model-dir /out/model --calib-images /data/images --calibration-count 64
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
from collections import Counter
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.preprocess import preprocess
from ovl_edge.spec import ModelSpec, sha256_file
from ovl_edge.splits import evenly, list_digest, read_list, split_images

DTYPE_PATTERN = re.compile(r"\b(FP32|FP16|BF16|Int8|INT8|FP8|Int32|Int64|Bool)\b")


def _run(cmd: list[str], log_path: Path) -> float:
    t0 = time.perf_counter()
    with open(log_path, "w", encoding="utf-8") as log:
        proc = subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT, text=True)
    seconds = time.perf_counter() - t0
    if proc.returncode != 0:
        tail = log_path.read_text(encoding="utf-8", errors="replace").splitlines()[-30:]
        raise SystemExit(f"Command failed ({proc.returncode}): {' '.join(cmd)}\n" + "\n".join(tail))
    return seconds


TRT_DATATYPE_NAMES = {"Float": "FP32", "Half": "FP16", "BFloat16": "BF16", "Int8": "INT8", "FP8": "FP8", "Int32": "INT32", "Int64": "INT64", "Bool": "BOOL"}


def layer_precision_histogram(layers_json: Path) -> dict:
    """Count the output datatype of each engine layer from trtexec --exportLayerInfo.

    TensorRT 11 writes Outputs[i].Datatype as "Float" / "Half" / "Int8". Reformat
    (NoOp) layers only convert layouts or types, so they are counted separately.
    """
    data = json.loads(layers_json.read_text(encoding="utf-8"))
    layers = data.get("Layers", data) if isinstance(data, dict) else data
    compute: Counter = Counter()
    reformat: Counter = Counter()
    for layer in layers:
        if isinstance(layer, dict):
            outputs = layer.get("Outputs") or []
            raw = outputs[0].get("Datatype") if outputs and isinstance(outputs[0], dict) else None
            if raw is None:
                found = DTYPE_PATTERN.findall(json.dumps(outputs[0] if outputs else layer))
                raw = found[0] if found else "unknown"
            dtype = TRT_DATATYPE_NAMES.get(raw, str(raw).upper())
            target = reformat if layer.get("LayerType") == "NoOp" else compute
        else:
            dtype, target = "unknown", compute
        target[dtype] += 1
    return {
        "layers": sum(compute.values()) + sum(reformat.values()),
        "compute_layer_output_dtypes": dict(compute),
        "reformat_layer_output_dtypes": dict(reformat),
    }


def build_engine(onnx_path: Path, engine_path: Path, extra: list[str]) -> dict:
    layers_path = engine_path.with_suffix(".layers.json")
    log_path = engine_path.with_suffix(".build.log")
    cmd = [
        "trtexec",
        f"--onnx={onnx_path}",
        f"--saveEngine={engine_path}",
        f"--exportLayerInfo={layers_path}",
        "--profilingVerbosity=detailed",
        "--skipInference",
        *extra,
    ]
    seconds = _run(cmd, log_path)
    from ovl_edge.trt_runner import current_gpu

    return {
        # Per engine, because --only rebuilds a subset; serve.py refuses another GPU model (design M3).
        "gpu": current_gpu(),
        "onnx": onnx_path.name,
        "onnx_sha256": sha256_file(onnx_path),
        "engine": engine_path.name,
        "engine_sha256": sha256_file(engine_path),
        "build_seconds": round(seconds, 2),
        "command": cmd,
        "layer_precision": layer_precision_histogram(layers_path),
    }


def calibration_tensor(paths: list[Path], spec: ModelSpec) -> np.ndarray:
    batches = []
    for path in paths:
        image = cv2.imread(str(path), cv2.IMREAD_COLOR)
        tensor, _ = preprocess(image, spec.input_hw, pad_value=int(spec.letterbox["pad_value"]))
        batches.append(tensor)
    return np.concatenate(batches, axis=0)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--calib-images", required=True)
    parser.add_argument("--calibration-count", type=int, default=64)
    parser.add_argument("--calib-list", default="", help="split file to calibrate from (evenly spaced subset); default: first N of the sorted folder")
    parser.add_argument("--calibration-method", default="entropy", choices=["entropy", "max"])
    parser.add_argument("--only", nargs="*", default=["fp32", "fp16", "int8"])
    parser.add_argument("--strict-fp32", action="store_true", help="disable TF32 for the FP32 engine")
    parser.add_argument("--refresh-layer-info", action="store_true", help="only recompute layer histograms of existing engines")
    parser.add_argument(
        "--int8-high-precision",
        nargs="+",
        default=["fp32", "fp16"],
        choices=["fp32", "fp16"],
        help="dtype for ops left unquantized; fp32 -> engine 'int8', fp16 -> engine 'int8_fp16'",
    )
    args = parser.parse_args(argv)

    import tensorrt

    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    onnx_path = model_dir / spec.onnx_file
    out = model_dir / "engines"
    out.mkdir(exist_ok=True)
    manifest_path = out / "engines.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {"engines": {}}
    manifest.update({"tensorrt": tensorrt.__version__, "model_spec_sha256": sha256_file(model_dir / "model_spec.json")})
    if args.refresh_layer_info:
        for entry in manifest["engines"].values():
            entry["layer_precision"] = layer_precision_histogram(out / Path(entry["engine"]).with_suffix(".layers.json").name)
        args.only = []

    if args.only and sha256_file(onnx_path) != spec.onnx_sha256:
        raise SystemExit(f"{onnx_path.name} does not match model_spec.json; re-export before building engines")

    if "fp32" in args.only:
        extra = ["--noTF32"] if args.strict_fp32 else []
        manifest["engines"]["fp32"] = {**build_engine(onnx_path, out / "fp32.plan", extra), "tf32_disabled": bool(args.strict_fp32)}

    modelopt_python = os.environ.get("OVL_MODELOPT_PYTHON", sys.executable)

    def ensure_source_unchanged(step: str) -> None:
        if sha256_file(onnx_path) != spec.onnx_sha256:
            raise SystemExit(f"{onnx_path.name} changed during {step}; results are not reproducible")

    def transform(mode: str, dst: Path, extra: list[str]) -> dict:
        # Model Optimizer 0.48 writes shape-inference results back into the ONNX
        # file it is given (observed 2026-11-20). It only ever sees a private copy.
        work = model_dir / ".work"
        work.mkdir(exist_ok=True)
        private = work / f"{onnx_path.stem}.{mode}.input.onnx"
        shutil.copyfile(onnx_path, private)
        log = dst.with_suffix(".transform.log")
        try:
            seconds = _run([modelopt_python, "-m", "ovl_edge.cli.modelopt_transform", mode, "--onnx", str(private), "--out", str(dst), *extra], log)
        finally:
            shutil.rmtree(work, ignore_errors=True)
        ensure_source_unchanged(f"{mode} transform")
        info = json.loads(log.read_text(encoding="utf-8").strip().splitlines()[-1])
        return {"transform_seconds": round(seconds, 2), "transform": info, "source_onnx_sha256": spec.onnx_sha256}

    if "fp16" in args.only:
        fp16_onnx = model_dir / (onnx_path.stem + ".fp16.onnx")
        meta = transform("fp16", fp16_onnx, [])
        manifest["engines"]["fp16"] = {**build_engine(fp16_onnx, out / "fp16.plan", []), **meta}

    if "int8" in args.only:
        if args.calib_list:
            calib_paths = evenly(read_list(args.calib_images, args.calib_list), args.calibration_count)
        else:
            calib_paths, _ = split_images(args.calib_images, args.calibration_count)
        calib = calibration_tensor(calib_paths, spec)
        calib_npy = model_dir / "calibration.npy"
        np.save(calib_npy, calib)
        manifest["calibration_list_sha256"] = list_digest(calib_paths)
        manifest["calibration_images"] = [p.name for p in calib_paths]
        manifest["calibration_npy_sha256"] = sha256_file(calib_npy)
        for high in args.int8_high_precision:
            key = "int8" if high == "fp32" else f"int8_{high}"
            int8_onnx = model_dir / (onnx_path.stem + f".int8-{high}.qdq.onnx")
            meta = transform("int8", int8_onnx, ["--calib", str(calib_npy), "--method", args.calibration_method, "--high-precision", high])
            manifest["engines"][key] = {**build_engine(int8_onnx, out / f"{key}.plan", []), **meta}

    if args.only:
        ensure_source_unchanged("engine builds")
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: {"build_seconds": v["build_seconds"], "layer_precision": v["layer_precision"]} for k, v in manifest["engines"].items()}, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
