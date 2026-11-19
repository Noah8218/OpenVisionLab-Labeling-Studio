"""Model specification shared by export, verification, benchmark and serving.

The spec fixes every input/output convention that two runtimes must agree on.
ORT and TensorRT results are only comparable when both are driven from the same
spec and the same host pre/post-processing code (design decision E2).
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

SCHEMA_VERSION = 1


def sha256_file(path: str | Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def default_letterbox() -> dict[str, Any]:
    # Matches ultralytics.data.augment.LetterBox with auto=False (fixed input size).
    return {"pad_value": 114, "center": True, "scaleup": True, "interpolation": "INTER_LINEAR"}


def default_nms() -> dict[str, Any]:
    # NMS runs once on the host for every runtime; the ONNX graph has no NMS node.
    # conf/iou follow the existing Labeling Studio worker defaults (--conf 0.25, --iou 0.45).
    return {
        "location": "host-postprocess",
        "conf": 0.25,
        "iou": 0.45,
        "max_det": 300,
        "max_nms": 30000,
        "agnostic": False,
        "max_wh": 7680,
    }


@dataclass(frozen=True)
class ModelSpec:
    family: str
    source_weights: str
    source_sha256: str
    onnx_file: str
    onnx_sha256: str
    opset: int
    input_name: str
    input_shape: list[int]
    input_dtype: str
    output_name: str
    output_shape: list[int]
    class_names: list[str]
    color_order: str = "RGB"
    normalize: str = "divide_255"
    letterbox: dict[str, Any] = field(default_factory=default_letterbox)
    nms: dict[str, Any] = field(default_factory=default_nms)
    exporter: dict[str, Any] = field(default_factory=dict)
    schema: int = SCHEMA_VERSION

    @property
    def input_hw(self) -> tuple[int, int]:
        return int(self.input_shape[2]), int(self.input_shape[3])

    @property
    def num_classes(self) -> int:
        return len(self.class_names)

    def save(self, path: str | Path) -> None:
        Path(path).write_text(json.dumps(asdict(self), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    @classmethod
    def load(cls, path: str | Path) -> "ModelSpec":
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        if data.get("schema") != SCHEMA_VERSION:
            raise ValueError(f"Unsupported model spec schema: {data.get('schema')}")
        return cls(**data)
