"""Export an Ultralytics YOLO detect checkpoint to ONNX and write model_spec.json.

The checkpoint is copied into the output directory first so Ultralytics writes
the ONNX file there and the source location stays untouched (read-only reuse).

    python -m ovl_edge.cli.export_onnx --weights /in/yolo11n.pt --out /out/model --imgsz 640
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

from ovl_edge.spec import ModelSpec, sha256_file


def _dims(value_info) -> list[int]:
    return [int(d.dim_value) for d in value_info.type.tensor_type.shape.dim]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--weights", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--opset", type=int, default=0, help="0 lets the exporter choose")
    args = parser.parse_args(argv)

    import onnx
    import ultralytics
    from ultralytics import YOLO

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    source = Path(args.weights)
    local_weights = out_dir / source.name
    if local_weights.resolve() != source.resolve():
        shutil.copy2(source, local_weights)

    model = YOLO(str(local_weights), task="detect")
    if model.task != "detect":
        raise SystemExit(f"Only detect checkpoints are supported, got task={model.task}")
    export_kwargs = {"format": "onnx", "imgsz": args.imgsz, "dynamic": False, "simplify": False, "half": False, "nms": False, "batch": 1}
    if args.opset:
        export_kwargs["opset"] = args.opset
    onnx_path = Path(model.export(**export_kwargs))

    graph_model = onnx.load(str(onnx_path))
    onnx.checker.check_model(graph_model)
    inputs = [i for i in graph_model.graph.input if i.name not in {t.name for t in graph_model.graph.initializer}]
    if len(inputs) != 1 or len(graph_model.graph.output) != 1:
        raise SystemExit(f"Expected one input and one output, got {len(inputs)} / {len(graph_model.graph.output)}")
    names = model.names
    class_names = [names[i] for i in sorted(names)] if isinstance(names, dict) else list(names)
    opset = max(o.version for o in graph_model.opset_import if o.domain in ("", "ai.onnx"))

    spec = ModelSpec(
        family="ultralytics-yolo-detect",
        source_weights=source.name,
        source_sha256=sha256_file(local_weights),
        onnx_file=onnx_path.name,
        onnx_sha256=sha256_file(onnx_path),
        opset=int(opset),
        input_name=inputs[0].name,
        input_shape=_dims(inputs[0]),
        input_dtype="float32",
        output_name=graph_model.graph.output[0].name,
        output_shape=_dims(graph_model.graph.output[0]),
        class_names=class_names,
        exporter={"ultralytics": ultralytics.__version__, "onnx": onnx.__version__, "options": export_kwargs},
    )
    expected_out = [1, 4 + spec.num_classes]
    if spec.output_shape[:2] != expected_out:
        raise SystemExit(f"Raw head expected {expected_out}xN, got {spec.output_shape}; NMS may be embedded")
    spec.save(out_dir / "model_spec.json")
    print(json.dumps({"onnx": str(onnx_path), "spec": str(out_dir / "model_spec.json"), "input": spec.input_shape, "output": spec.output_shape, "opset": spec.opset}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
