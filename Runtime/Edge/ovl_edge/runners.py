"""Tensor runners and the shared host pipeline.

A runner only maps an input tensor to the raw head tensor. Pre/post-processing
lives in Detector so every runtime uses the same host code (design E2).
"""

from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Protocol

import numpy as np

from .postprocess import Detections, decode
from .preprocess import preprocess
from .spec import ModelSpec

ORT_PROVIDER_ALIASES = {
    "cpu": ["CPUExecutionProvider"],
    "cuda": ["CUDAExecutionProvider", "CPUExecutionProvider"],
}


class TensorRunner(Protocol):
    name: str
    input_dtype: np.dtype

    def infer(self, tensor: np.ndarray) -> np.ndarray: ...


class OrtRunner:
    def __init__(self, onnx_path: str, provider: str = "cpu", intra_op_threads: int | None = None, use_tf32: bool = False):
        import onnxruntime as ort

        if provider not in ORT_PROVIDER_ALIASES:
            raise ValueError(f"Unknown ORT provider alias: {provider}")
        options = ort.SessionOptions()
        if intra_op_threads:
            options.intra_op_num_threads = intra_op_threads
        providers: list = list(ORT_PROVIDER_ALIASES[provider])
        if provider == "cuda":
            # ORT CUDA enables TF32 convolutions by default on Ampere+; strict FP32
            # comparisons (design M2) keep it off unless asked for.
            providers[0] = ("CUDAExecutionProvider", {"use_tf32": "1" if use_tf32 else "0"})
        self.use_tf32 = bool(use_tf32) if provider == "cuda" else None
        # The session is created once per runner (worker start), never per request.
        self.session = ort.InferenceSession(onnx_path, sess_options=options, providers=providers)
        self.requested_provider = ORT_PROVIDER_ALIASES[provider][0]
        self.active_providers = self.session.get_providers()
        if self.active_providers[0] != self.requested_provider:
            # A silent fallback would make GPU numbers meaningless; fail loudly.
            raise RuntimeError(f"ORT fell back from {self.requested_provider} to {self.active_providers}")
        meta = self.session.get_inputs()[0]
        self.input_name = meta.name
        self.input_dtype = np.float16 if meta.type == "tensor(float16)" else np.float32
        self.output_name = self.session.get_outputs()[0].name
        self.name = f"ort-{provider}"
        self.ort_version = ort.__version__

    def infer(self, tensor: np.ndarray) -> np.ndarray:
        return self.session.run([self.output_name], {self.input_name: tensor})[0]


@dataclass(frozen=True)
class StageTimes:
    preprocess_ms: float
    infer_ms: float
    postprocess_ms: float


class Detector:
    def __init__(self, spec: ModelSpec, runner: TensorRunner):
        self.spec = spec
        self.runner = runner

    def detect(self, image_bgr: np.ndarray) -> tuple[Detections, StageTimes]:
        t0 = time.perf_counter()
        tensor, info = preprocess(image_bgr, self.spec.input_hw, dtype=self.runner.input_dtype, pad_value=int(self.spec.letterbox["pad_value"]))
        t1 = time.perf_counter()
        raw = self.runner.infer(tensor)
        t2 = time.perf_counter()
        detections = decode(raw, self.spec.num_classes, self.spec.nms, info)
        t3 = time.perf_counter()
        return detections, StageTimes((t1 - t0) * 1e3, (t2 - t1) * 1e3, (t3 - t2) * 1e3)


class UltralyticsReference:
    """Ultralytics' own predict pipeline, used only as the parity reference."""

    def __init__(self, weights: str, spec: ModelSpec, device: str = "cpu", rect: bool = False):
        from ultralytics import YOLO

        self.model = YOLO(weights, task="detect")
        self.spec = spec
        self.device = device
        # Model.predict defaults to rect=True, which gives .pt checkpoints a
        # minimum-padding input instead of the fixed letterbox the ONNX contract uses.
        self.rect = rect
        self.name = f"ultralytics:{weights.rsplit('.', 1)[-1]}{':rect' if rect else ''}"

    def detect(self, image_bgr: np.ndarray) -> Detections:
        nms_cfg = self.spec.nms
        result = self.model.predict(
            source=image_bgr,
            imgsz=list(self.spec.input_hw),
            conf=float(nms_cfg["conf"]),
            iou=float(nms_cfg["iou"]),
            max_det=int(nms_cfg["max_det"]),
            agnostic_nms=bool(nms_cfg["agnostic"]),
            device=self.device,
            rect=self.rect,
            verbose=False,
        )[0]
        boxes = result.boxes
        if boxes is None or len(boxes) == 0:
            return Detections.empty()
        return Detections(
            boxes.xyxy.cpu().numpy().astype(np.float32),
            boxes.conf.cpu().numpy().astype(np.float32),
            boxes.cls.cpu().numpy().astype(np.int64),
        )
