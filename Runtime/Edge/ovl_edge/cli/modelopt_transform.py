"""ONNX precision transforms with NVIDIA Model Optimizer (runs in /opt/venv-modelopt).

Imports only numpy, onnx and modelopt so it can run in the isolated virtualenv
whose onnx/onnxruntime versions differ from the runtime pins.

    $OVL_MODELOPT_PYTHON -m ovl_edge.cli.modelopt_transform fp16 --onnx in.onnx --out out.onnx
    $OVL_MODELOPT_PYTHON -m ovl_edge.cli.modelopt_transform int8 --onnx in.onnx --out out.onnx --calib calib.npy \
        --method entropy --high-precision fp32
"""

from __future__ import annotations

import argparse
import json
import sys


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["fp16", "int8"])
    parser.add_argument("--onnx", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--calib")
    parser.add_argument("--method", default="entropy", choices=["entropy", "max"])
    parser.add_argument("--high-precision", default="fp32", choices=["fp32", "fp16"], help="dtype for ops left unquantized")
    args = parser.parse_args(argv)

    import modelopt
    import numpy as np
    import onnx

    info = {"modelopt": modelopt.__version__, "onnx": onnx.__version__, "mode": args.mode}
    if args.mode == "fp16":
        from modelopt.onnx.autocast import convert_to_mixed_precision

        # keep_io_types: the host pipeline keeps feeding FP32 tensors to every engine.
        model = convert_to_mixed_precision(onnx_path=args.onnx, low_precision_type="fp16", keep_io_types=True, providers=["cpu"])
        onnx.save(model, args.out)
        info["keep_io_types"] = True
    else:
        from modelopt.onnx.quantization import quantize

        if not args.calib:
            raise SystemExit("int8 requires --calib")
        calib = np.load(args.calib)
        # CPU calibration: the venv's onnxruntime-gpu 1.30 targets older CUDA/TensorRT
        # libraries than this image; CPU keeps the calibration statistics deterministic.
        quantize(
            onnx_path=args.onnx,
            quantize_mode="int8",
            calibration_data=calib,
            calibration_method=args.method,
            calibration_eps=["cpu"],
            high_precision_dtype=args.high_precision,
            output_path=args.out,
        )
        info.update({"calibration_method": args.method, "calibration_samples": int(calib.shape[0]), "high_precision_dtype": args.high_precision, "calibration_eps": ["cpu"]})
    print(json.dumps(info))
    return 0


if __name__ == "__main__":
    sys.exit(main())
