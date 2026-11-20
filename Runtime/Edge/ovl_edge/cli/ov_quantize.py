"""OpenVINO INT8 with NNCF post-training quantization (runs in $OVL_NNCF_PYTHON).

    $OVL_NNCF_PYTHON -m ovl_edge.cli.ov_quantize --model-dir /out/model [--ignore-head]

Input: the package's FP32 ONNX and calibration.npy (written by build_engines from
the calibration split, N x 3 x H x W float32). Output: <onnx stem>.int8-ov.xml/.bin
and openvino.json with their hashes; runtime ov-cpu-int8 loads them.

--ignore-head keeps the detection-head arithmetic (Multiply, Subtract, Sigmoid,
Softmax) in floating point, the usual setting for YOLO heads, where box decoding
and scores are sensitive to 8-bit rounding.
This file imports only json/hashlib/pathlib/numpy besides nncf and openvino, so
it runs in the separate virtualenv without the runtime packages.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import numpy as np


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--ignore-head", action="store_true")
    parser.add_argument("--preset", default="mixed", choices=["mixed", "performance"])
    args = parser.parse_args(argv)

    import nncf
    import openvino as ov

    model_dir = Path(args.model_dir)
    spec = json.loads((model_dir / "model_spec.json").read_text(encoding="utf-8"))
    onnx_path = model_dir / spec["onnx_file"]
    if sha256(onnx_path) != spec["onnx_sha256"]:
        raise SystemExit(f"{onnx_path.name} does not match model_spec.json")
    calib_path = model_dir / "calibration.npy"
    calib = np.load(calib_path)
    model = ov.Core().read_model(str(onnx_path))
    ignored = nncf.IgnoredScope(types=["Multiply", "Subtract", "Sigmoid", "Softmax"]) if args.ignore_head else None
    preset = nncf.QuantizationPreset.MIXED if args.preset == "mixed" else nncf.QuantizationPreset.PERFORMANCE
    kwargs = {"preset": preset, "subset_size": len(calib)}
    if ignored is not None:
        kwargs["ignored_scope"] = ignored
    quantized = nncf.quantize(model, nncf.Dataset([calib[i : i + 1] for i in range(len(calib))]), **kwargs)
    suffix = ".int8-ov-head-fp" if args.ignore_head else ".int8-ov"
    xml = model_dir / (onnx_path.stem + suffix + ".xml")
    ov.save_model(quantized, str(xml), compress_to_fp16=False)
    manifest_path = model_dir / "openvino.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {"models": {}}
    key = "int8_head_fp" if args.ignore_head else "int8"
    manifest["models"][key] = {
        "xml": xml.name, "xml_sha256": sha256(xml), "bin": xml.with_suffix(".bin").name, "bin_sha256": sha256(xml.with_suffix(".bin")),
        "source_onnx_sha256": spec["onnx_sha256"], "calibration_npy_sha256": sha256(calib_path), "calibration_images": int(len(calib)),
        "nncf": nncf.__version__, "openvino": ov.__version__, "preset": args.preset, "ignored_types": ["Multiply", "Subtract", "Sigmoid", "Softmax"] if args.ignore_head else [],
    }
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: {k: manifest["models"][key][k] for k in ("xml", "nncf", "preset")}}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
