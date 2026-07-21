#!/usr/bin/env python3
"""Ultralytics TCP worker for OpenVisionLab Labeling Studio.

This worker exposes Ultralytics YOLOv8/YOLO11 inference and training through
the same JSON protocol shape as the existing YOLOv5 worker so the WPF app can
switch model adapters without silently falling back to YOLOv5.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import importlib.util
import json
import os
import shutil
import socket
import sys
import tempfile
import threading
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


PACKET_SEPARATOR = b"\n\n"
BASE_SUPPORTED_MODELS = ("yolov8",)
YOLO11_MODEL = "yolo11"
_YOLO11_RUNTIME_AVAILABLE: bool | None = None
_ULTRALYTICS_VERSION: str | None = None
TRAINING_WEIGHT_DEFAULTS = (
    "yolov8n.pt",
    "yolov8n-seg.pt",
    "yolov8n-cls.pt",
    "yolo11n.pt",
    "yolo11n-seg.pt",
    "yolo11n-cls.pt",
)
LEGACY_TYPE_MAP = {
    "StartDefect": "DetectImage",
    "StartTraining": "TrainYolo",
    "StopDefect": "StopTask",
    "StopTraining": "StopTask",
}


@dataclass
class IncomingMessage:
    message_type: str
    request_id: str = ""
    image_id: str = ""
    payload: dict[str, Any] = field(default_factory=dict)
    binary_payload: bytes = b""
    raw_type: str = ""
    legacy: bool = False


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def compact_json(data: dict[str, Any]) -> bytes:
    return json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def make_error(code: str, message: str | Exception, include_trace: bool = False) -> dict[str, Any]:
    if isinstance(message, Exception):
        error: dict[str, Any] = {
            "code": code,
            "message": str(message),
            "exceptionType": type(message).__name__,
        }
        if include_trace:
            error["trace"] = traceback.format_exc()
        return error

    return {"code": code, "message": str(message)}


def get_first(payload: dict[str, Any], names: Iterable[str], default: Any = None) -> Any:
    for name in names:
        value = payload.get(name)
        if value is not None and value != "":
            return value
    return default


def as_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in {"1", "true", "yes", "y", "on"}
    return bool(value)


@contextmanager
def data_yaml_working_directory(data_path: Path):
    """Resolve native YAML-relative paths from the YAML's directory without changing source files."""
    if not data_path.is_file():
        yield
        return

    previous = Path.cwd()
    os.chdir(data_path.parent)
    try:
        yield
    finally:
        os.chdir(previous)


@contextmanager
def label_cache_directory():
    """Keep transient Ultralytics label caches out of source datasets."""
    environment_name = "OPENVISIONLAB_ULTRALYTICS_LABEL_CACHE_DIR"
    previous = os.environ.get(environment_name)
    directory = Path(tempfile.mkdtemp(prefix="openvisionlab-ultralytics-label-cache-"))
    os.environ[environment_name] = str(directory)
    try:
        yield directory
    finally:
        if previous is None:
            os.environ.pop(environment_name, None)
        else:
            os.environ[environment_name] = previous
        shutil.rmtree(directory, ignore_errors=True)


@contextmanager
def remove_created_source_label_caches(data_path: Path | None):
    """Remove only label-cache files created by this training run from a native source tree."""
    source_root = data_path.parent if data_path is not None and data_path.is_file() else None
    label_root = source_root / "labels" if source_root is not None else None
    before = set(label_root.rglob("*.cache")) if label_root is not None and label_root.is_dir() else set()
    try:
        yield
    finally:
        if label_root is None or not label_root.is_dir():
            return
        for cache_path in label_root.rglob("*.cache"):
            if cache_path not in before:
                try:
                    cache_path.unlink()
                except OSError:
                    pass


def normalize_model(value: Any) -> str:
    lower = str(value or "").strip().lower()
    if lower in {"yolov8", "yolo8", "v8"}:
        return "yolov8"
    if lower in {"yolo11", "yolov11", "v11"}:
        return "yolo11"
    return lower


def ultralytics_available() -> bool:
    try:
        return importlib.util.find_spec("ultralytics") is not None
    except (ImportError, ValueError):
        return "ultralytics" in sys.modules


def yolo11_runtime_available() -> bool:
    global _YOLO11_RUNTIME_AVAILABLE
    if _YOLO11_RUNTIME_AVAILABLE is not None:
        return _YOLO11_RUNTIME_AVAILABLE

    if not ultralytics_available():
        _YOLO11_RUNTIME_AVAILABLE = False
        return False
    try:
        from ultralytics.nn.modules import block

        _YOLO11_RUNTIME_AVAILABLE = hasattr(block, "C3k2")
    except Exception:
        _YOLO11_RUNTIME_AVAILABLE = False

    return _YOLO11_RUNTIME_AVAILABLE


def ultralytics_version() -> str:
    global _ULTRALYTICS_VERSION
    if _ULTRALYTICS_VERSION is not None:
        return _ULTRALYTICS_VERSION

    try:
        from importlib.metadata import version

        _ULTRALYTICS_VERSION = version("ultralytics")
    except Exception:
        _ULTRALYTICS_VERSION = ""

    return _ULTRALYTICS_VERSION


def runtime_supported_models() -> tuple[str, ...]:
    if not ultralytics_available():
        return ()

    models = list(BASE_SUPPORTED_MODELS)
    if yolo11_runtime_available():
        models.append(YOLO11_MODEL)
    return tuple(models)


def runtime_training_models() -> tuple[str, ...]:
    return runtime_supported_models()


def runtime_detection_models() -> tuple[str, ...]:
    return runtime_supported_models()


def runtime_segmentation_models() -> tuple[str, ...]:
    return runtime_supported_models()


def runtime_classification_models() -> tuple[str, ...]:
    return runtime_supported_models()


def default_runtime_model() -> str:
    models = runtime_supported_models()
    return YOLO11_MODEL if YOLO11_MODEL in models else "yolov8"


def unsupported_model_message(operation: str, model: str) -> str:
    detail = f"this worker does not support {operation} model: {model}"
    if model == YOLO11_MODEL and ultralytics_available() and not yolo11_runtime_available():
        version = ultralytics_version() or "unknown"
        detail += f"; installed ultralytics {version} does not expose C3k2."
    return detail


def training_weight_cache_payload(model_root: Path | None) -> dict[str, list[str]]:
    if model_root is None:
        return {}

    cached = [
        name
        for name in TRAINING_WEIGHT_DEFAULTS
        if (model_root / name).exists()
    ]
    missing = [name for name in TRAINING_WEIGHT_DEFAULTS if name not in cached]
    runtime_models = set(runtime_supported_models())
    runtime_ready = [
        name
        for name in cached
        if training_weight_model_name(name) in runtime_models
    ]
    runtime_blocked = [
        name
        for name in cached
        if training_weight_model_name(name) not in runtime_models
    ]
    download_required = [
        name
        for name in missing
        if training_weight_model_name(name) in runtime_models
    ]
    runtime_blocked_missing = [
        name
        for name in missing
        if training_weight_model_name(name) not in runtime_models
    ]
    return {
        "cachedTrainingWeights": cached,
        "missingTrainingWeights": missing,
        "runtimeReadyTrainingWeights": runtime_ready,
        "runtimeBlockedTrainingWeights": runtime_blocked,
        "downloadRequiredTrainingWeights": download_required,
        "runtimeBlockedMissingTrainingWeights": runtime_blocked_missing,
    }


def training_weight_model_name(file_name: str) -> str:
    name = (file_name or "").lower()
    if name.startswith("yolo11"):
        return "yolo11"
    if name.startswith("yolov8"):
        return "yolov8"
    return ""


def capability_payload(model_root: Path | None = None) -> dict[str, list[str]]:
    runtime_warnings: list[str] = []
    if ultralytics_available() and not yolo11_runtime_available():
        runtime_warnings.append("YOLO11 disabled: installed ultralytics runtime does not expose C3k2.")

    payload = {
        "supportedModels": list(runtime_supported_models()),
        "trainingModels": list(runtime_training_models()),
        "detectionModels": list(runtime_detection_models()),
        "segmentationModels": list(runtime_segmentation_models()),
        "classificationModels": list(runtime_classification_models()),
        "runtimeWarnings": runtime_warnings,
    }
    payload.update(training_weight_cache_payload(model_root))
    return payload


def collect_environment(weights: Path, image_root: Path, model_root: Path) -> dict[str, Any]:
    return {
        "pythonExecutable": sys.executable,
        "pythonVersion": sys.version.split()[0],
        "cwd": os.getcwd(),
        "scriptRoot": str(Path(__file__).resolve().parent),
        "modelRoot": str(model_root),
        "modelRootExists": model_root.exists(),
        "weightsPath": str(weights),
        "weightsExists": weights.exists(),
        **training_weight_cache_payload(model_root),
        "imageRoot": str(image_root),
        "imageRootExists": image_root.exists(),
        "ultralyticsInstalled": ultralytics_available(),
        "ultralyticsVersion": ultralytics_version(),
        "yolo11RuntimeAvailable": yolo11_runtime_available(),
    }


class JsonResponseWriter:
    def __init__(self, sock: socket.socket):
        self.sock = sock
        self._lock = threading.Lock()

    def send(self, envelope: dict[str, Any]) -> None:
        envelope.setdefault("version", 1)
        with self._lock:
            self.sock.sendall(compact_json(envelope) + b"\n")


class UltralyticsDetector:
    def __init__(self, weights: Path, model_root: Path, image_root: Path, device: str, img_size: int, conf: float, iou: float, debug: bool = False):
        self.weights = weights
        self.model_root = model_root
        self.image_root = image_root
        self.device = device
        self.img_size = img_size
        self.conf = conf
        self.iou = iou
        self.debug = debug
        self.model: Any = None
        self.state = "notLoaded"
        self.last_error: dict[str, Any] | None = None
        self.loaded_at_utc: str | None = None
        self.load_started_at_utc: str | None = None
        self.load_ms: int | None = None
        self.class_names: list[str] = []

    def status(self) -> dict[str, Any]:
        return {
            "state": self.state,
            "loaded": self.model is not None,
            "modelRoot": str(self.model_root),
            "weightsPath": str(self.weights),
            "weightsExists": self.weights.exists(),
            "device": self.device,
            "imgSize": self.img_size,
            "conf": self.conf,
            "iou": self.iou,
            "loadedAtUtc": self.loaded_at_utc,
            "loadStartedAtUtc": self.load_started_at_utc,
            "loadMs": self.load_ms,
            "classNames": self.class_names,
            "lastError": self.last_error,
        }

    def load(self) -> Any:
        if self.model is not None:
            return self.model

        self.state = "loading"
        self.last_error = None
        self.load_started_at_utc = utc_now()
        started = time.perf_counter()
        try:
            if not ultralytics_available():
                raise ImportError("ultralytics package is not installed in the selected Python environment.")
            if not self.weights.exists():
                raise FileNotFoundError(f"weights file not found: {self.weights}")

            from ultralytics import YOLO

            model = YOLO(str(self.weights))
            if self.device:
                model.to(self.device)
            self.model = model
            self.state = "ready"
            self.loaded_at_utc = utc_now()
            self.load_ms = int((time.perf_counter() - started) * 1000)
            self.class_names = self._read_class_names(model)
            return model
        except Exception as exc:
            self.model = None
            self.state = "error"
            self.load_ms = int((time.perf_counter() - started) * 1000)
            self.last_error = make_error("ModelLoadFailed", exc, include_trace=self.debug)
            raise

    def detect_path(self, image_path: Path, confidence: float | None = None) -> tuple[list[dict[str, Any]], dict[str, Any]]:
        if not image_path.exists():
            raise FileNotFoundError(f"image file not found: {image_path}")

        model = self.load()
        kwargs: dict[str, Any] = {
            "source": str(image_path),
            "conf": self._clamp_confidence(confidence),
            "imgsz": self.img_size,
            "verbose": False,
        }
        if self.iou > 0:
            kwargs["iou"] = self.iou
        if self.device:
            kwargs["device"] = self.device

        results = model.predict(**kwargs)
        if not results:
            return [], {"path": str(image_path), "width": 0, "height": 0}

        result = results[0]
        height, width = self._read_shape(result)
        return self._build_candidates(result, width, height), {
            "path": str(image_path),
            "width": width,
            "height": height,
        }

    def _clamp_confidence(self, confidence: float | None) -> float:
        value = self.conf if confidence is None else confidence
        return max(0.0, min(1.0, float(value)))

    @staticmethod
    def _read_shape(result: Any) -> tuple[int, int]:
        shape = getattr(result, "orig_shape", None)
        if isinstance(shape, (list, tuple)) and len(shape) >= 2:
            return int(shape[0]), int(shape[1])
        return 0, 0

    @staticmethod
    def _read_class_names(model: Any) -> list[str]:
        names = getattr(model, "names", [])
        if isinstance(names, dict):
            return [str(names[key]) for key in sorted(names.keys())]
        if isinstance(names, (list, tuple)):
            return [str(name) for name in names]
        return []

    @staticmethod
    def _class_name(names: Any, class_id: int) -> str:
        if isinstance(names, dict):
            return str(names.get(class_id, class_id))
        if isinstance(names, (list, tuple)) and 0 <= class_id < len(names):
            return str(names[class_id])
        return str(class_id)

    def _build_candidates(self, result: Any, image_width: int, image_height: int) -> list[dict[str, Any]]:
        boxes = getattr(result, "boxes", None)
        if boxes is None:
            return self._build_classification_candidates(result)

        names = getattr(result, "names", getattr(self.model, "names", {}))
        masks = getattr(result, "masks", None)
        candidates: list[dict[str, Any]] = []
        for index, box in enumerate(boxes):
            xyxy_values = getattr(box, "xyxy", None)
            if xyxy_values is None:
                continue

            xyxy = xyxy_values[0].tolist()
            x1 = max(0.0, float(xyxy[0]))
            y1 = max(0.0, float(xyxy[1]))
            x2 = max(0.0, float(xyxy[2]))
            y2 = max(0.0, float(xyxy[3]))
            width = max(0.0, x2 - x1)
            height = max(0.0, y2 - y1)
            class_id = int(getattr(box, "cls")[0].item()) if getattr(box, "cls", None) is not None else -1
            confidence = float(getattr(box, "conf")[0].item()) if getattr(box, "conf", None) is not None else 0.0
            class_name = self._class_name(names, class_id)

            candidate = {
                "candidateId": f"det-{index}",
                "classId": class_id,
                "className": class_name,
                "confidence": confidence,
                "x": x1,
                "y": y1,
                "width": width,
                "height": height,
                "bbox": {"x": x1, "y": y1, "width": width, "height": height},
                "normalizedBbox": {
                    "x": x1 / image_width if image_width else 0,
                    "y": y1 / image_height if image_height else 0,
                    "width": width / image_width if image_width else 0,
                    "height": height / image_height if image_height else 0,
                },
            }

            polygon_points = self._read_mask_polygon(masks, index)
            if len(polygon_points) >= 3:
                candidate["segmentationType"] = "polygon"
                candidate["polygonPoints"] = polygon_points
                candidate["normalizedPolygonPoints"] = [
                    {
                        "x": point["x"] / image_width if image_width else 0,
                        "y": point["y"] / image_height if image_height else 0,
                    }
                    for point in polygon_points
                ]

            candidates.append(candidate)

        return candidates if candidates else self._build_classification_candidates(result)

    def _build_classification_candidates(self, result: Any) -> list[dict[str, Any]]:
        probs = getattr(result, "probs", None)
        if probs is None:
            return []

        class_id = self._read_scalar(getattr(probs, "top1", None), default=-1)
        if class_id is None or int(class_id) < 0:
            return []

        confidence = self._read_scalar(getattr(probs, "top1conf", None), default=0.0)
        names = getattr(result, "names", getattr(self.model, "names", {}))
        class_id_int = int(class_id)
        confidence_value = float(confidence or 0.0)
        return [
            {
                "candidateId": "cls-0",
                "candidateType": "imageClassification",
                "predictionType": "classification",
                "imageLevel": True,
                "classId": class_id_int,
                "className": self._class_name(names, class_id_int),
                "confidence": confidence_value,
                "x": 0,
                "y": 0,
                "width": 0,
                "height": 0,
            }
        ]

    @staticmethod
    def _read_scalar(value: Any, default: Any = None) -> Any:
        if value is None:
            return default
        if hasattr(value, "item"):
            return value.item()
        if isinstance(value, (list, tuple)):
            return value[0] if value else default
        return value

    @staticmethod
    def _read_mask_polygon(masks: Any, index: int) -> list[dict[str, float]]:
        if masks is None:
            return []

        polygons = getattr(masks, "xy", None)
        if polygons is None or index < 0 or index >= len(polygons):
            return []

        raw_points = polygons[index]
        if hasattr(raw_points, "tolist"):
            raw_points = raw_points.tolist()

        points: list[dict[str, float]] = []
        for raw_point in raw_points:
            if hasattr(raw_point, "tolist"):
                raw_point = raw_point.tolist()
            if not isinstance(raw_point, (list, tuple)) or len(raw_point) < 2:
                continue
            points.append({"x": float(raw_point[0]), "y": float(raw_point[1])})

        return points


class LabelingUltralyticsWorker:
    def __init__(self, detector: UltralyticsDetector, debug: bool = False):
        self.detector = detector
        self.debug = debug
        self.started_at_utc = utc_now()
        self.training_lock = threading.Lock()
        self.training_thread: threading.Thread | None = None
        self.training_status: dict[str, Any] = {
            "type": "TrainingStatus",
            "state": "idle",
            "message": "training is idle",
        }

    def handle(self, message: IncomingMessage, writer: JsonResponseWriter | None = None) -> dict[str, Any]:
        try:
            if message.message_type == "HealthCheck":
                return self.handle_health_check(message)
            if message.message_type == "ModelStatus":
                return self.handle_model_status(message)
            if message.message_type == "DetectImage":
                return self.handle_detect_image(message)
            if message.message_type == "TrainYolo":
                return self.handle_train_yolo(message, writer)
            if message.message_type == "StopTask":
                return self.handle_stop_task(message)
            return {
                "type": "Error",
                "requestId": message.request_id,
                "imageId": message.image_id,
                "ok": False,
                "error": make_error("UnknownMessageType", f"unsupported message type: {message.raw_type or message.message_type}"),
            }
        except Exception as exc:
            return {
                "type": "Error",
                "requestId": message.request_id,
                "imageId": message.image_id,
                "ok": False,
                "error": make_error("UnhandledWorkerError", exc, include_trace=self.debug),
            }

    def handle_health_check(self, message: IncomingMessage) -> dict[str, Any]:
        capabilities = capability_payload(self.detector.model_root)
        has_package = ultralytics_available()
        envelope = {
            "type": "HealthCheckResult",
            "requestId": message.request_id,
            "ok": has_package,
            "state": "ready" if has_package else "error",
            "worker": {
                "name": "openvisionlab-ultralytics-worker",
                "pid": os.getpid(),
                "startedAtUtc": self.started_at_utc,
                "nowUtc": utc_now(),
                "capabilities": capabilities,
            },
            "capabilities": capabilities,
            "environment": collect_environment(self.detector.weights, self.detector.image_root, self.detector.model_root),
            "model": self.detector.status(),
        }
        if not has_package:
            envelope["error"] = make_error("UltralyticsMissing", "ultralytics package is not installed in the selected Python environment.")
        return envelope

    def handle_model_status(self, message: IncomingMessage) -> dict[str, Any]:
        if as_bool(message.payload.get("load", False)) or as_bool(message.payload.get("ensureLoaded", False)):
            try:
                self.detector.load()
            except Exception:
                pass

        status = self.detector.status()
        capabilities = capability_payload(self.detector.model_root)
        return {
            "type": "ModelStatusResult",
            "requestId": message.request_id,
            "ok": status["state"] == "ready",
            "state": status["state"],
            "capabilities": capabilities,
            "model": status,
            "training": dict(self.training_status),
            "error": status["lastError"],
        }

    def handle_detect_image(self, message: IncomingMessage) -> dict[str, Any]:
        requested_model = normalize_model(get_first(message.payload, ["model", "adapter"], ""))
        if requested_model and ultralytics_available() and requested_model not in runtime_detection_models():
            return {
                "type": "DetectImageResult",
                "requestId": message.request_id,
                "imageId": message.image_id,
                "ok": False,
                "candidates": [],
                "model": self.detector.status(),
                "error": make_error("UnsupportedModel", unsupported_model_message("detection", requested_model)),
            }

        image_id = message.image_id or str(get_first(message.payload, ["imageId"], ""))
        confidence = self._resolve_confidence(message.payload)
        started = time.perf_counter()
        try:
            image_path_value = get_first(message.payload, ["imagePath", "path", "filePath"], "")
            if not image_path_value:
                image_bytes_b64 = get_first(message.payload, ["imageBytesBase64", "imageBase64"], "")
                if image_bytes_b64 or message.binary_payload:
                    raise ValueError("binary image detection is not supported by the Ultralytics worker yet; send imagePath.")
                raise ValueError("DetectImage requires imagePath.")

            image_path = self._resolve_image_path(image_path_value)
            if not image_id:
                image_id = image_path.stem

            detections, image_info = self.detector.detect_path(image_path, confidence=confidence)
            return {
                "type": "DetectImageResult",
                "requestId": message.request_id,
                "imageId": image_id,
                "ok": True,
                "elapsedMs": int((time.perf_counter() - started) * 1000),
                "image": image_info,
                "model": self.detector.status(),
                "candidates": detections,
            }
        except Exception as exc:
            return {
                "type": "DetectImageResult",
                "requestId": message.request_id,
                "imageId": image_id,
                "ok": False,
                "candidates": [],
                "model": self.detector.status(),
                "error": make_error("DetectImageFailed", exc, include_trace=self.debug),
            }

    def handle_train_yolo(self, message: IncomingMessage, writer: JsonResponseWriter | None) -> dict[str, Any]:
        requested_model = normalize_model(get_first(message.payload, ["model", "adapter"], ""))
        if requested_model and ultralytics_available() and requested_model not in runtime_training_models():
            return self._training_start_failure(
                message,
                "UnsupportedTrainingModel",
                unsupported_model_message("training", requested_model),
            )

        model_name = requested_model or default_runtime_model()
        task = self._resolve_training_task(message.payload)
        if task not in {"detect", "segment", "classify"}:
            return self._training_start_failure(
                message,
                "UnsupportedTrainingTask",
                f"unsupported Ultralytics training task: {task}",
            )

        data_yaml = self._resolve_training_data_path(message.payload)
        if not data_yaml:
            return self._training_start_failure(
                message,
                "MissingTrainingData",
                "TrainYolo requires dataYaml/data path.",
            )

        if not data_yaml.exists():
            return self._training_start_failure(
                message,
                "TrainingDataNotFound",
                f"training data file was not found: {data_yaml}",
            )

        if writer is None:
            return self._training_start_failure(
                message,
                "TrainingWriterUnavailable",
                "TrainYolo requires a TCP response writer for asynchronous status updates.",
            )

        if not ultralytics_available():
            return self._training_start_failure(
                message,
                "UltralyticsMissing",
                "ultralytics package is not installed in the selected Python environment.",
            )

        with self.training_lock:
            if self.training_thread is not None and self.training_thread.is_alive():
                return self._training_start_failure(
                    message,
                    "TrainingAlreadyRunning",
                    "an Ultralytics training job is already running.",
                    state="running",
                )

            training_weights = self._resolve_training_weights(message.payload, model_name, task)
            if self._training_weight_requires_download(training_weights) and not self._allows_model_download(message.payload):
                return self._training_start_failure(
                    message,
                    "TrainingWeightDownloadRequired",
                    (
                        f"training weight '{training_weights}' is not cached under {self.detector.model_root} "
                        "and would require a model download; cache the file or send allowModelDownload=true after user approval."
                    ),
                    training_weights=training_weights,
                )

            payload = dict(message.payload)
            payload["model"] = model_name
            payload["task"] = task
            payload["dataYaml"] = str(data_yaml)
            payload["trainingWeights"] = training_weights
            self.training_status = self._build_training_status(
                message.request_id,
                state="started",
                message=f"Ultralytics {model_name} {task} training accepted.",
                task=task,
                model=model_name,
                progress=0,
                training_weights=training_weights,
            )
            self.training_thread = threading.Thread(
                target=self._run_training_job,
                args=(message.request_id, payload, writer),
                daemon=True,
                name="openvisionlab-ultralytics-training",
            )
            self.training_thread.start()

        return {
            "type": "TrainYoloResult",
            "requestId": message.request_id,
            "ok": True,
            "state": "started",
            "taskType": "TrainYolo",
            "trainingTask": task,
            "model": model_name,
            "trainingWeights": training_weights,
            "progressPercent": 0,
        }

    def _run_training_job(self, request_id: str, payload: dict[str, Any], writer: JsonResponseWriter) -> None:
        model_name = normalize_model(payload.get("model")) or default_runtime_model()
        task = self._resolve_training_task(payload)
        data_yaml = self._resolve_training_data_path(payload)
        epochs = self._resolve_positive_int(payload, ["epoch", "epochs"], 50)
        image_size = self._resolve_positive_int(payload, ["imgSize", "imageSize", "imgsz"], self.detector.img_size)
        batch = self._resolve_positive_int(payload, ["batch", "batchSize"], 16)
        training_weights = str(payload.get("trainingWeights") or self._resolve_training_weights(payload, model_name, task))
        run_name = self._resolve_training_run_name(payload, model_name, task)
        project_path = self._resolve_training_project_path(task)

        def send_status(state: str, message: str, progress: int | None = None, epoch: int | None = None, error: dict[str, Any] | None = None) -> None:
            status = self._build_training_status(
                request_id,
                state=state,
                message=message,
                task=task,
                model=model_name,
                progress=progress,
                epoch=epoch,
                total_epochs=epochs,
                error=error,
                training_weights=training_weights,
            )
            self.training_status = status
            try:
                writer.send(status)
            except OSError:
                pass

        try:
            from ultralytics import YOLO

            send_status("running", f"Ultralytics {model_name} {task} training started.", progress=0, epoch=0)
            model = YOLO(training_weights)

            def on_train_epoch_end(trainer: Any) -> None:
                current_epoch = int(getattr(trainer, "epoch", 0)) + 1
                total_epochs = int(getattr(trainer, "epochs", epochs) or epochs)
                progress = int(max(0, min(100, round((current_epoch / max(total_epochs, 1)) * 100))))
                send_status("running", f"Epoch {current_epoch}/{total_epochs}", progress=progress, epoch=current_epoch)

            if hasattr(model, "add_callback"):
                model.add_callback("on_train_epoch_end", on_train_epoch_end)

            with remove_created_source_label_caches(data_yaml), data_yaml_working_directory(data_yaml), label_cache_directory():
                result = model.train(
                    data=str(data_yaml),
                    task=task,
                    epochs=epochs,
                    imgsz=image_size,
                    batch=batch,
                    project=str(project_path),
                    name=run_name,
                    exist_ok=True,
                    device=self.detector.device or None,
                    plots=False,
                )
            save_dir = str(getattr(result, "save_dir", "") or "")
            send_status("completed", f"Ultralytics {model_name} {task} training completed. {save_dir}".strip(), progress=100, epoch=epochs)
        except Exception as exc:
            send_status("failed", "Ultralytics training failed.", error=make_error("TrainingFailed", exc, include_trace=self.debug))

    def _training_start_failure(
        self,
        message: IncomingMessage,
        code: str,
        detail: str,
        state: str = "failed",
        training_weights: str = "",
    ) -> dict[str, Any]:
        error = make_error(code, detail)
        self.training_status = self._build_training_status(
            message.request_id,
            state=state,
            message=detail,
            task=self._resolve_training_task(message.payload),
            model=normalize_model(get_first(message.payload, ["model", "adapter"], "")) or default_runtime_model(),
            error=error,
            training_weights=training_weights,
        )
        result = {
            "type": "TrainYoloResult",
            "requestId": message.request_id,
            "ok": False,
            "state": state,
            "taskType": "TrainYolo",
            "error": error,
        }
        if training_weights:
            result["trainingWeights"] = training_weights
        return result

    @staticmethod
    def _build_training_status(
        request_id: str,
        state: str,
        message: str,
        task: str,
        model: str,
        progress: int | None = None,
        epoch: int | None = None,
        total_epochs: int | None = None,
        error: dict[str, Any] | None = None,
        training_weights: str = "",
    ) -> dict[str, Any]:
        status: dict[str, Any] = {
            "type": "TrainingStatus",
            "requestId": request_id,
            "taskType": "TrainYolo",
            "state": state,
            "message": message,
            "trainingTask": task,
            "model": model,
            "updatedAtUtc": utc_now(),
        }
        if training_weights:
            status["trainingWeights"] = training_weights
        if progress is not None:
            status["progressPercent"] = max(0, min(100, int(progress)))
        if epoch is not None:
            status["epoch"] = max(0, int(epoch))
        if total_epochs is not None:
            status["totalEpochs"] = max(0, int(total_epochs))
        if error is not None:
            status["error"] = error
        return status

    def _resolve_training_data_path(self, payload: dict[str, Any]) -> Path | None:
        value = get_first(payload, ["dataYaml", "dataYamlPath", "data"], "")
        if not value:
            return None

        path = Path(str(value)).expanduser()
        if path.is_absolute():
            return path.resolve()
        if path.exists():
            return path.resolve()
        return (self.detector.image_root / path).resolve()

    @staticmethod
    def _resolve_training_task(payload: dict[str, Any]) -> str:
        raw = str(get_first(payload, ["task", "trainingTask", "datasetPurpose"], "detect") or "detect").strip().lower()
        if raw in {"seg", "segment", "segmentation"}:
            return "segment"
        if raw in {"cls", "classify", "classification"}:
            return "classify"
        return "detect"

    @staticmethod
    def _resolve_positive_int(payload: dict[str, Any], names: Iterable[str], default: int) -> int:
        try:
            return max(1, int(float(get_first(payload, names, default))))
        except (TypeError, ValueError):
            return max(1, int(default))

    def _resolve_training_weights(self, payload: dict[str, Any], model_name: str, task: str) -> str:
        value = str(get_first(payload, ["weight", "weights", "weightsPath", "pretrained"], "") or "").strip()
        if value:
            path = Path(value).expanduser()
            if path.is_absolute() and path.exists():
                return str(path.resolve())
            if path.exists():
                return str(path.resolve())
            normalized = value.lower().replace("\\", "/").split("/")[-1]
            if normalized.startswith(("yolov8", "yolo11")):
                return self._resolve_cached_training_weight(normalized) or value

        base = "yolo11n" if model_name == "yolo11" else "yolov8n"
        suffix = "-seg" if task == "segment" else "-cls" if task == "classify" else ""
        default_name = f"{base}{suffix}.pt"
        return self._resolve_cached_training_weight(default_name) or default_name

    def _resolve_training_project_path(self, task: str) -> Path:
        run_kind = "segment" if task == "segment" else "classify" if task == "classify" else "train"
        return self.detector.model_root / "runs" / run_kind

    def _resolve_cached_training_weight(self, file_name: str) -> str:
        if not file_name:
            return ""
        candidate = (self.detector.model_root / Path(file_name).name).resolve()
        return str(candidate) if candidate.exists() else ""

    @staticmethod
    def _allows_model_download(payload: dict[str, Any]) -> bool:
        return as_bool(get_first(payload, ["allowModelDownload", "allowWeightDownload", "allowDownload"], False))

    @staticmethod
    def _training_weight_requires_download(training_weights: str) -> bool:
        value = str(training_weights or "").strip()
        if not value:
            return False

        path = Path(value).expanduser()
        if path.exists() or path.is_absolute() or len(path.parts) > 1:
            return False

        return Path(value).name in TRAINING_WEIGHT_DEFAULTS

    @staticmethod
    def _resolve_training_run_name(payload: dict[str, Any], model_name: str, task: str) -> str:
        value = str(get_first(payload, ["name", "runName"], "") or "").strip()
        if value:
            return value
        return f"openvisionlab-{model_name}-{task}"

    def handle_stop_task(self, message: IncomingMessage) -> dict[str, Any]:
        return {
            "type": "StopTaskResult",
            "requestId": message.request_id,
            "ok": True,
            "state": "idle",
        }

    def _resolve_image_path(self, value: Any) -> Path:
        path = Path(str(value)).expanduser()
        if path.is_absolute():
            return path.resolve()
        if path.exists():
            return path.resolve()
        return (self.detector.image_root / path).resolve()

    @staticmethod
    def _resolve_confidence(payload: dict[str, Any]) -> float | None:
        value = get_first(payload, ["confidence", "conf", "threshold", "minimumConfidence"], None)
        if value is None:
            return None
        try:
            return max(0.0, min(1.0, float(value)))
        except (TypeError, ValueError):
            return None


def parse_messages(buffer: bytearray) -> Iterable[IncomingMessage]:
    while True:
        while buffer and buffer[0] in b"\r\n\t ":
            del buffer[0]
        if not buffer:
            return

        if buffer.startswith(b"{"):
            line_break = buffer.find(b"\n")
            if line_break < 0:
                return
            line = bytes(buffer[:line_break]).strip()
            del buffer[: line_break + 1]
            if line:
                yield parse_json_line_message(line)
            continue

        separator_index = buffer.find(PACKET_SEPARATOR)
        if separator_index < 0:
            return

        command = buffer[:separator_index].decode("ascii", errors="replace").strip()
        payload_start = separator_index + len(PACKET_SEPARATOR)
        packet_length = find_legacy_packet_end(buffer, payload_start)
        if packet_length < 0:
            return

        payload = bytes(buffer[payload_start:packet_length])
        del buffer[:packet_length]
        yield parse_legacy_message(command, payload)


def parse_json_line_message(line: bytes) -> IncomingMessage:
    try:
        payload = json.loads(line.decode("utf-8"))
        if not isinstance(payload, dict):
            raise ValueError("JSON message must be an object.")
        raw_type = str(get_first(payload, ["type", "messageType", "command", "action"], ""))
        if not raw_type:
            raise ValueError("JSON message requires type.")
        message_type = LEGACY_TYPE_MAP.get(raw_type, raw_type)
        return IncomingMessage(
            message_type=message_type,
            request_id=str(get_first(payload, ["requestId"], "")),
            image_id=str(get_first(payload, ["imageId"], "")),
            payload=payload,
            raw_type=raw_type,
            legacy=raw_type in LEGACY_TYPE_MAP,
        )
    except Exception as exc:
        return IncomingMessage(
            message_type="InvalidMessage",
            payload={"error": make_error("InvalidJson", exc)},
            raw_type="InvalidMessage",
        )


def parse_legacy_message(command: str, payload_bytes: bytes) -> IncomingMessage:
    message_type = LEGACY_TYPE_MAP.get(command, command)
    payload: dict[str, Any] = {}
    binary_payload = b""
    if payload_bytes:
        if command == "StartDefect":
            binary_payload = payload_bytes
        else:
            try:
                decoded = json.loads(payload_bytes.decode("utf-8"))
                if isinstance(decoded, dict):
                    payload = decoded
            except Exception as exc:
                payload = {"_parseError": make_error("InvalidLegacyPayload", exc)}

    return IncomingMessage(
        message_type=message_type,
        request_id=str(get_first(payload, ["requestId"], "")),
        image_id=str(get_first(payload, ["imageId"], "")),
        payload=payload,
        binary_payload=binary_payload,
        raw_type=command,
        legacy=True,
    )


def find_legacy_packet_end(buffer: bytearray, start: int) -> int:
    if start >= len(buffer):
        return start
    return find_json_packet_end(buffer, start)


def find_json_packet_end(buffer: bytearray, start: int) -> int:
    depth = 0
    in_string = False
    escaped = False
    saw_open = False
    for index in range(start, len(buffer)):
        char = chr(buffer[index])
        if in_string:
            if escaped:
                escaped = False
                continue
            if char == "\\":
                escaped = True
                continue
            if char == '"':
                in_string = False
            continue

        if char.isspace() and not saw_open:
            continue
        if char == '"':
            in_string = True
            continue
        if char == "{":
            saw_open = True
            depth += 1
            continue
        if char == "}":
            depth -= 1
            if depth == 0:
                return index + 1
    return -1


def build_detector(args: argparse.Namespace) -> UltralyticsDetector:
    return UltralyticsDetector(
        weights=Path(args.weights).resolve(),
        model_root=Path(args.model_root).resolve(),
        image_root=Path(args.image_root).resolve(),
        device=args.device,
        img_size=args.img_size,
        conf=args.conf,
        iou=args.iou,
        debug=args.debug,
    )


def run_client(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    if args.preload:
        try:
            detector.load()
            print(compact_json({"type": "ModelStatusResult", "ok": True, "model": detector.status(), "capabilities": capability_payload(detector.model_root)}).decode("utf-8"), flush=True)
        except Exception as exc:
            print(compact_json({"type": "ModelStatusResult", "ok": False, "model": detector.status(), "error": make_error("ModelLoadFailed", exc)}).decode("utf-8"), flush=True)

    while True:
        try:
            with socket.create_connection((args.host, args.port), timeout=args.timeout) as sock:
                print(f"connected to labeling app at {args.host}:{args.port}", flush=True)
                sock.settimeout(args.timeout)
                writer = JsonResponseWriter(sock)
                worker = LabelingUltralyticsWorker(detector, debug=args.debug)
                return read_loop(sock, args, worker, writer)
        except OSError as exc:
            if not args.retry:
                print(f"connect failed: {exc}", flush=True)
                return 1
            print(f"connect failed: {exc}; retrying in {args.retry_delay}s", flush=True)
            time.sleep(args.retry_delay)


def read_loop(sock: socket.socket, args: argparse.Namespace, worker: LabelingUltralyticsWorker, writer: JsonResponseWriter) -> int:
    buffer = bytearray()
    handled = 0
    while True:
        try:
            chunk = sock.recv(65536)
        except socket.timeout:
            continue
        if not chunk:
            print("labeling app closed connection", flush=True)
            return 0

        buffer.extend(chunk)
        for message in parse_messages(buffer):
            handled += 1
            writer.send(worker.handle(message, writer))
            if args.once and handled >= 1:
                return 0


def run_smoke_test(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    image_path_value = args.detect_file or args.image
    if not image_path_value:
        print(compact_json({"type": "SmokeTestResult", "ok": False, "error": make_error("MissingImagePath", "--smoke-test requires --image or --detect-file.")}).decode("utf-8"), flush=True)
        return 2

    image_path = resolve_image_path_for_cli(image_path_value, Path(args.image_root).resolve())
    requested_model = normalize_model(getattr(args, "model", ""))
    if requested_model and ultralytics_available() and requested_model not in runtime_detection_models():
        print(compact_json({
            "type": "SmokeTestResult",
            "requestId": "smoke-test",
            "imageId": image_path.stem,
            "ok": False,
            "weightsPath": str(Path(args.weights).resolve()),
            "image": {"path": str(image_path)},
            "model": detector.status(),
            "capabilities": capability_payload(detector.model_root),
            "error": make_error("UnsupportedModel", unsupported_model_message("detection", requested_model)),
        }).decode("utf-8"), flush=True)
        return 1

    started = time.perf_counter()
    try:
        detections, image_info = detector.detect_path(image_path)
        result = {
            "type": "SmokeTestResult",
            "requestId": "smoke-test",
            "imageId": image_path.stem,
            "ok": True,
            "elapsedMs": int((time.perf_counter() - started) * 1000),
            "weightsPath": str(Path(args.weights).resolve()),
            "image": image_info,
            "model": detector.status(),
            "capabilities": capability_payload(detector.model_root),
            "candidates": detections,
        }
        print(compact_json(result).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        result = {
            "type": "SmokeTestResult",
            "requestId": "smoke-test",
            "imageId": image_path.stem,
            "ok": False,
            "weightsPath": str(Path(args.weights).resolve()),
            "image": {"path": str(image_path)},
            "model": detector.status(),
            "capabilities": capability_payload(detector.model_root),
            "error": make_error("SmokeTestFailed", exc, include_trace=args.debug),
        }
        print(compact_json(result).decode("utf-8"), flush=True)
        return 1


def run_self_test() -> int:
    original_working_directory = Path.cwd()
    source_file = Path(__file__).resolve()
    with data_yaml_working_directory(source_file):
        assert Path.cwd() == source_file.parent
    assert Path.cwd() == original_working_directory
    with data_yaml_working_directory(source_file.parent):
        assert Path.cwd() == original_working_directory

    cache_environment_name = "OPENVISIONLAB_ULTRALYTICS_LABEL_CACHE_DIR"
    previous_cache_directory = os.environ.get(cache_environment_name)
    with label_cache_directory() as temporary_cache_directory:
        assert temporary_cache_directory.is_dir()
        assert os.environ.get(cache_environment_name) == str(temporary_cache_directory)
        (temporary_cache_directory / "labels.cache").write_text("cache", encoding="utf-8")
    assert not temporary_cache_directory.exists()
    if previous_cache_directory is None:
        assert cache_environment_name not in os.environ
    else:
        assert os.environ.get(cache_environment_name) == previous_cache_directory

    source_cache_root = Path(tempfile.mkdtemp(prefix="openvisionlab-source-cache-contract-"))
    try:
        source_data_yaml = source_cache_root / "data.yaml"
        source_data_yaml.write_text("train: images/train\n", encoding="utf-8")
        source_label_directory = source_cache_root / "labels" / "train"
        source_label_directory.mkdir(parents=True)
        preexisting_cache = source_label_directory / "preexisting.cache"
        preexisting_cache.write_text("keep", encoding="utf-8")
        with remove_created_source_label_caches(source_data_yaml):
            (source_label_directory / "created.cache").write_text("remove", encoding="utf-8")
        assert preexisting_cache.exists()
        assert not (source_label_directory / "created.cache").exists()
    finally:
        shutil.rmtree(source_cache_root, ignore_errors=True)

    buffer = bytearray(
        b'{"type":"HealthCheck","requestId":"req-health"}\n'
        b'{"type":"ModelStatus","requestId":"req-model","ensureLoaded":false}\n'
        b'{"type":"DetectImage","requestId":"req-detect","imageId":"img-1","imagePath":"sample.bmp","model":"yolo11"}\n'
        b'StartTraining\n\n{"requestId":"req-train","model":"yolo11","task":"segment","dataYaml":"data.yaml"}'
    )
    messages = list(parse_messages(buffer))
    assert len(messages) == 4, f"expected 4 messages, got {len(messages)}"
    assert messages[0].message_type == "HealthCheck"
    assert messages[1].message_type == "ModelStatus"
    assert messages[2].message_type == "DetectImage"
    assert messages[2].payload["model"] == "yolo11"
    assert messages[3].message_type == "TrainYolo"
    assert not buffer
    smoke_args = parse_args(["--smoke-test", "--image", "sample.bmp", "--model", "yolo11"])
    assert smoke_args.model == "yolo11"

    capabilities = capability_payload()
    assert "supportedModels" in capabilities
    if ultralytics_available():
        assert "yolov8" in capabilities["detectionModels"]
        assert "yolov8" in capabilities["segmentationModels"]
        assert "yolov8" in capabilities["classificationModels"]
        assert "yolov8" in capabilities["trainingModels"]
    if yolo11_runtime_available():
        assert "yolo11" in capabilities["detectionModels"]
        assert "yolo11" in capabilities["segmentationModels"]
        assert "yolo11" in capabilities["classificationModels"]
        assert "yolo11" in capabilities["trainingModels"]
    else:
        assert "yolo11" not in capabilities["supportedModels"]
        if ultralytics_available():
            assert capabilities["runtimeWarnings"]
            assert "C3k2" in capabilities["runtimeWarnings"][0]
            assert ultralytics_version()
            unsupported_message = unsupported_model_message("training", "yolo11")
            assert ultralytics_version() in unsupported_message
            assert "C3k2" in unsupported_message

    class FakeTensor:
        def __init__(self, value: Any):
            self.value = value

        def __getitem__(self, index: int) -> "FakeTensor":
            return FakeTensor(self.value[index])

        def tolist(self) -> Any:
            return self.value

        def item(self) -> Any:
            return self.value

    class FakeBox:
        xyxy = FakeTensor([[10, 20, 30, 40]])
        cls = FakeTensor([1])
        conf = FakeTensor([0.9])

    class FakeMasks:
        xy = [FakeTensor([[10, 20], [30, 20], [30, 40], [10, 40]])]

    class FakeResult:
        boxes = [FakeBox()]
        masks = FakeMasks()
        names = {1: "Defect"}

    detector = UltralyticsDetector(
        weights=Path("missing.pt"),
        model_root=Path(".").resolve(),
        image_root=Path(".").resolve(),
        device="",
        img_size=64,
        conf=0.25,
        iou=0.45,
    )
    candidates = detector._build_candidates(FakeResult(), 100, 80)
    assert candidates[0]["className"] == "Defect"
    assert candidates[0]["segmentationType"] == "polygon"
    assert candidates[0]["polygonPoints"][2]["x"] == 30.0
    assert candidates[0]["normalizedPolygonPoints"][2]["y"] == 0.5

    class FakeProbs:
        top1 = FakeTensor(0)
        top1conf = FakeTensor(0.88)

    class FakeClassificationResult:
        boxes = None
        probs = FakeProbs()
        names = {0: "Normal", 1: "Abnormal"}

    classification_candidates = detector._build_candidates(FakeClassificationResult(), 100, 80)
    assert classification_candidates[0]["candidateType"] == "imageClassification"
    assert classification_candidates[0]["imageLevel"] is True
    assert classification_candidates[0]["className"] == "Normal"
    assert classification_candidates[0]["confidence"] == 0.88
    assert LabelingUltralyticsWorker._resolve_training_task({"task": "segmentation"}) == "segment"
    assert LabelingUltralyticsWorker._resolve_training_task({"task": "classification"}) == "classify"
    worker = LabelingUltralyticsWorker(detector)
    if ultralytics_available() and not yolo11_runtime_available():
        unsupported_detect = worker.handle_detect_image(parse_json_line_message(
            b'{"type":"DetectImage","requestId":"req-unsupported-detect","imagePath":"missing.bmp","model":"yolo11"}'
        ))
        assert unsupported_detect["ok"] is False
        assert "C3k2" in unsupported_detect["error"]["message"]
        unsupported_train = worker.handle_train_yolo(parse_json_line_message(
            b'{"type":"TrainYolo","requestId":"req-unsupported-train","model":"yolo11","task":"classify"}'
        ), None)
        assert unsupported_train["ok"] is False
        assert "C3k2" in unsupported_train["error"]["message"]
    assert worker._resolve_training_weights({"weight": "yolov5x.pt"}, "yolo11", "segment") == "yolo11n-seg.pt"
    assert worker._resolve_training_weights({"weight": "yolov5x.pt"}, "yolo11", "classify") == "yolo11n-cls.pt"
    assert worker._resolve_training_weights({"weight": "yolov5x.pt"}, "yolov8", "segment") == "yolov8n-seg.pt"
    assert worker._resolve_training_weights({"weight": "yolov5x.pt"}, "yolov8", "classify") == "yolov8n-cls.pt"
    cached_weight_root = Path(tempfile.mkdtemp(prefix="ovl-ultralytics-cache-"))
    try:
        detector.model_root = cached_weight_root
        cached_seg = cached_weight_root / "yolov8n-seg.pt"
        cached_cls = cached_weight_root / "yolov8n-cls.pt"
        cached_seg.write_text("weights", encoding="utf-8")
        cached_cls.write_text("weights", encoding="utf-8")
        cache_payload = training_weight_cache_payload(cached_weight_root)
        assert "yolov8n-seg.pt" in cache_payload["cachedTrainingWeights"]
        assert "yolov8n-cls.pt" in cache_payload["cachedTrainingWeights"]
        if "yolov8" in runtime_supported_models():
            assert "yolov8n-seg.pt" in cache_payload["runtimeReadyTrainingWeights"]
            assert "yolov8n-cls.pt" in cache_payload["runtimeReadyTrainingWeights"]
            assert "yolov8n.pt" in cache_payload["downloadRequiredTrainingWeights"]
        assert "yolo11n-seg.pt" in cache_payload["missingTrainingWeights"]
        if "yolo11" not in runtime_supported_models():
            assert "yolo11n-seg.pt" in cache_payload["runtimeBlockedMissingTrainingWeights"]
        assert training_weight_model_name("yolo11n-cls.pt") == "yolo11"
        assert worker._resolve_training_weights({"weight": "yolov8n-seg.pt"}, "yolov8", "segment") == str(cached_seg.resolve())
        assert worker._resolve_training_weights({"weight": "yolov5x.pt"}, "yolov8", "classify") == str(cached_cls.resolve())
    finally:
        shutil.rmtree(cached_weight_root, ignore_errors=True)
    status = worker._build_training_status(
        "req-train",
        "running",
        "Epoch 1/2",
        "segment",
        "yolo11",
        progress=50,
        epoch=1,
        total_epochs=2,
        training_weights="yolo11n-seg.pt",
    )
    assert status["type"] == "TrainingStatus"
    assert status["progressPercent"] == 50
    assert status["trainingTask"] == "segment"
    assert status["trainingWeights"] == "yolo11n-seg.pt"
    detector.model_root = Path(".").resolve()
    detector.img_size = 64
    detector.device = ""
    sent_statuses: list[dict[str, Any]] = []
    captured_train_kwargs: dict[str, Any] = {}
    captured_training_working_directories: list[Path] = []
    captured_model_weights: list[str] = []
    callbacks: dict[str, Any] = {}

    class FakeWriter:
        def send(self, envelope: dict[str, Any]) -> None:
            sent_statuses.append(envelope)

    class FakeYolo:
        def __init__(self, weights: str):
            self.weights = weights
            captured_model_weights.append(weights)

        def add_callback(self, name: str, callback: Any) -> None:
            callbacks[name] = callback

        def train(self, **kwargs: Any) -> Any:
            captured_train_kwargs.update(kwargs)
            captured_training_working_directories.append(Path.cwd())

            class FakeTrainer:
                epoch = 0
                epochs = 1

            callbacks["on_train_epoch_end"](FakeTrainer())

            class FakeResult:
                save_dir = "fake-run"

            return FakeResult()

    fake_ultralytics = type(sys)("ultralytics")
    fake_ultralytics.YOLO = FakeYolo
    previous_ultralytics = sys.modules.get("ultralytics")
    download_guard_root = Path(tempfile.mkdtemp(prefix="ovl-ultralytics-download-guard-"))
    try:
        detector.model_root = download_guard_root
        sys.modules["ultralytics"] = fake_ultralytics
        download_guard = worker.handle_train_yolo(
            IncomingMessage(
                "TrainYolo",
                request_id="req-download-guard",
                payload={
                    "model": "yolov8",
                    "task": "segment",
                    "dataYaml": str(Path(__file__).resolve()),
                },
            ),
            FakeWriter(),
        )
        assert download_guard["ok"] is False
        assert download_guard["error"]["code"] == "TrainingWeightDownloadRequired"
        assert download_guard["trainingWeights"] == "yolov8n-seg.pt"
        assert worker.training_status["trainingWeights"] == "yolov8n-seg.pt"

        for flag_name in ("allowModelDownload", "allowWeightDownload", "allowDownload"):
            sent_statuses.clear()
            captured_train_kwargs.clear()
            callbacks.clear()
            download_allowed = worker.handle_train_yolo(
                IncomingMessage(
                    "TrainYolo",
                    request_id=f"req-download-allowed-{flag_name}",
                    payload={
                        "model": "yolov8",
                        "task": "segment",
                        "dataYaml": str(Path(__file__).resolve()),
                        flag_name: True,
                        "epoch": "1",
                        "imgSize": "64",
                        "batch": "1",
                    },
                ),
                FakeWriter(),
            )
            assert download_allowed["ok"] is True
            assert download_allowed["trainingWeights"] == "yolov8n-seg.pt"
            assert worker.training_thread is not None
            worker.training_thread.join(timeout=5)
            assert not worker.training_thread.is_alive()
            assert captured_model_weights[-1] == "yolov8n-seg.pt"
            assert captured_train_kwargs["task"] == "segment"
            assert captured_training_working_directories[-1] == Path(__file__).resolve().parent
            assert Path.cwd() == original_working_directory
            assert any(item["state"] == "completed" for item in sent_statuses)
    finally:
        if previous_ultralytics is None:
            sys.modules.pop("ultralytics", None)
        else:
            sys.modules["ultralytics"] = previous_ultralytics
        shutil.rmtree(download_guard_root, ignore_errors=True)

    fake_weight_root = Path(tempfile.mkdtemp(prefix="ovl-ultralytics-fake-train-cache-"))
    try:
        detector.model_root = fake_weight_root
        cached_fake_cls = fake_weight_root / "yolov8n-cls.pt"
        cached_fake_cls.write_text("weights", encoding="utf-8")
        for request_id, model_name, task_name, data_path, expected_weight in (
            ("req-train-yolo11-seg", "yolo11", "segment", str(Path(__file__).resolve()), "yolo11n-seg.pt"),
            ("req-train-yolo11-cls", "yolo11", "classify", str(Path(__file__).resolve().parent), "yolo11n-cls.pt"),
            ("req-train-yolov8-seg", "yolov8", "segment", str(Path(__file__).resolve()), "yolov8n-seg.pt"),
            ("req-train-yolov8-cls", "yolov8", "classify", str(Path(__file__).resolve().parent), str(cached_fake_cls.resolve())),
        ):
            sent_statuses.clear()
            captured_train_kwargs.clear()
            callbacks.clear()
            sys.modules["ultralytics"] = fake_ultralytics
            try:
                worker._run_training_job(
                    request_id,
                    {
                        "model": model_name,
                        "task": task_name,
                        "dataYaml": data_path,
                        "epoch": "1",
                        "imgSize": "64",
                        "batch": "1",
                    },
                    FakeWriter(),
                )
            finally:
                if previous_ultralytics is None:
                    del sys.modules["ultralytics"]
                else:
                    sys.modules["ultralytics"] = previous_ultralytics

            assert captured_train_kwargs["task"] == task_name
            assert captured_train_kwargs["epochs"] == 1
            assert captured_train_kwargs["imgsz"] == 64
            expected_run_kind = "segment" if task_name == "segment" else "classify" if task_name == "classify" else "train"
            assert Path(captured_train_kwargs["project"]).name == expected_run_kind
            assert captured_model_weights[-1] == expected_weight
            expected_working_directory = Path(data_path).resolve().parent if Path(data_path).is_file() else original_working_directory
            assert captured_training_working_directories[-1] == expected_working_directory
            assert Path.cwd() == original_working_directory
            assert sent_statuses[0]["state"] == "running"
            assert sent_statuses[0]["trainingWeights"] == expected_weight
            assert any(
                item["state"] == "completed"
                and item["trainingTask"] == task_name
                and item["progressPercent"] == 100
                and item["trainingWeights"] == expected_weight
                for item in sent_statuses
            )
    finally:
        detector.model_root = Path(".").resolve()
        shutil.rmtree(fake_weight_root, ignore_errors=True)
    print("self-test passed", flush=True)
    return 0


def resolve_image_path_for_cli(value: Any, image_root: Path) -> Path:
    path = Path(str(value)).expanduser()
    if path.is_absolute():
        return path.resolve()
    if path.exists():
        return path.resolve()
    return (image_root / path).resolve()


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Ultralytics TCP worker for OpenVisionLab Labeling Studio.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--weights", default="")
    parser.add_argument("--model", default="")
    parser.add_argument("--model-root", default=str(Path(__file__).resolve().parent))
    parser.add_argument("--image-root", default=str(Path.cwd()))
    parser.add_argument("--data-yaml", default="")
    parser.add_argument("--device", default="")
    parser.add_argument("--img-size", type=int, default=320)
    parser.add_argument("--conf", type=float, default=0.25)
    parser.add_argument("--iou", type=float, default=0.45)
    parser.add_argument("--retry", action="store_true")
    parser.add_argument("--retry-delay", type=float, default=3)
    parser.add_argument("--once", action="store_true")
    parser.add_argument("--preload", action="store_true")
    parser.add_argument("--debug", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--smoke-test", action="store_true")
    parser.add_argument("--detect-file", default="")
    parser.add_argument("--image", default="")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()
    if args.smoke_test or args.detect_file or args.image:
        return run_smoke_test(args)
    return run_client(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
