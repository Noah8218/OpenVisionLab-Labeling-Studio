#!/usr/bin/env python3
"""Ultralytics TCP worker for OpenVisionLab Labeling Studio.

This worker exposes Ultralytics YOLOv8/YOLO11 inference and training through
the same JSON protocol shape as the existing YOLOv5 worker so the WPF app can
switch model adapters without silently falling back to YOLOv5.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import csv
import hashlib
import importlib.util
import json
import math
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
RUN_ID_MAX_LENGTH = 64
BASE_SUPPORTED_MODELS = ("yolov8",)
YOLO11_MODEL = "yolo11"
_YOLO11_RUNTIME_AVAILABLE: bool | None = None
_ULTRALYTICS_VERSION: str | None = None
TRAINING_WEIGHT_DEFAULTS = (
    "yolov8n.pt",
    "yolov8s.pt",
    "yolov8n-seg.pt",
    "yolov8s-seg.pt",
    "yolov8n-cls.pt",
    "yolov8s-cls.pt",
    "yolo11n.pt",
    "yolo11s.pt",
    "yolo11m.pt",
    "yolo11n-seg.pt",
    "yolo11s-seg.pt",
    "yolo11n-cls.pt",
    "yolo11s-cls.pt",
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


def checkpoint_artifact_id(checkpoint_path: Path) -> str:
    digest = hashlib.sha256()
    with checkpoint_path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return f"sha256:{digest.hexdigest()}"


def checkpoint_epoch(checkpoint_path: Path, results_csv_path: Path) -> int | None:
    if not checkpoint_path.is_file():
        return None

    try:
        import torch

        checkpoint = torch.load(str(checkpoint_path), map_location="cpu")
        raw_epoch = checkpoint.get("epoch") if isinstance(checkpoint, dict) else None
        if raw_epoch is None:
            return None
        raw_epoch = int(raw_epoch)
    except Exception:
        return None

    result_epochs: set[int] = set()
    if results_csv_path.is_file():
        try:
            with results_csv_path.open("r", newline="", encoding="utf-8-sig") as stream:
                for row in csv.DictReader(stream):
                    try:
                        result_epochs.add(int(float(str(row.get("epoch", "")).strip())))
                    except (TypeError, ValueError):
                        continue
        except (OSError, UnicodeError):
            pass

    # Ultralytics stores zero-based epochs; stripped checkpoints contain -1.
    epoch = raw_epoch + 1
    return epoch if raw_epoch >= 0 and epoch in result_epochs else None


def write_checkpoint_metadata(
    checkpoint_path: Path,
    checkpoint_role: str,
    results_csv_path: Path,
    run_name: str,
    run_id: str = "",
    observed_epoch: int | None = None,
) -> dict[str, Any] | None:
    epoch = observed_epoch if observed_epoch is not None else checkpoint_epoch(checkpoint_path, results_csv_path)
    if epoch is None:
        return None
    if observed_epoch is not None:
        if not checkpoint_path.is_file() or epoch < 1 or not results_csv_path.is_file():
            return None
        with results_csv_path.open("r", newline="", encoding="utf-8-sig") as stream:
            if not any(str(row.get("epoch", "")).strip() == str(epoch) for row in csv.DictReader(stream)):
                return None

    metadata_path = Path(f"{checkpoint_path}.metadata.json")
    temporary_path = Path(f"{metadata_path}.tmp")
    metadata = {
        "schemaVersion": 1,
        "format": "openvisionlab-training-checkpoint-v1",
        "checkpointRole": checkpoint_role,
        "epoch": int(epoch),
        "artifactId": checkpoint_artifact_id(checkpoint_path),
        "resultsCsvPath": str(results_csv_path.resolve()),
        "runName": run_name,
        "runId": run_id,
    }
    try:
        temporary_path.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        temporary_path.replace(metadata_path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()
    return metadata


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


def training_epoch_metrics(trainer: Any, task: str) -> dict[str, Any]:
    def finite(value: Any) -> float | None:
        if isinstance(value, bool):
            return None
        try:
            number = float(value)
            return number if math.isfinite(number) and number >= 0 else None
        except (TypeError, ValueError, OverflowError):
            return None

    result: dict[str, Any] = {}
    metrics = getattr(trainer, "metrics", None) or {}
    try:
        losses = trainer.label_loss_items(trainer.tloss, prefix="train") if getattr(trainer, "tloss", None) is not None else {}
        for name, values in (("trainLoss", losses), ("validationLoss", metrics)):
            prefix = "train/" if name == "trainLoss" else "val/"
            components = [finite(value) for key, value in values.items() if key.startswith(prefix) and "loss" in key.lower()]
            if components and all(value is not None for value in components):
                total = sum(components)
                if math.isfinite(total):
                    result[name] = total
        source_key, key = {"detect": ("metrics/mAP50(B)", "map50"), "segment": ("metrics/mAP50(M)", "maskMap50"),
                           "classify": ("metrics/accuracy_top1", "top1Accuracy")}.get(task, ("", ""))
        value = finite(metrics.get(source_key))
        if key and value is not None and value <= 1:
            result.update({"validationMetricKey": key, "validationMetricValue": value})
    except (AttributeError, TypeError, ValueError, RuntimeError):
        return {}
    return result


def normalize_run_id(value: Any) -> str:
    text = str(value or "").strip()
    if not text:
        return ""
    if len(text) > RUN_ID_MAX_LENGTH:
        raise ValueError(f"runId must be at most {RUN_ID_MAX_LENGTH} characters")
    for character in text:
        if not ("a" <= character <= "z" or "A" <= character <= "Z" or "0" <= character <= "9" or character in "-_"):
            raise ValueError("runId may contain only ASCII letters, digits, '-' or '_'")
    return text


def compose_training_run_name(display_name: str, run_id: str, fallback: str) -> str:
    display = str(display_name or "").strip()
    safe_display = "".join(
        character if ("a" <= character <= "z" or "A" <= character <= "Z" or "0" <= character <= "9" or character in "-_") else "-"
        for character in display
    ).strip("-_")[:80]
    safe_display = safe_display or fallback
    return f"{safe_display}-{run_id}" if run_id else safe_display


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


def nms_candidate_multiplier(predictor_type: type, nms_function: Any, class_count: int) -> int:
    """Resolve the inspected predictor's actual single/multi-label NMS contract.

    Unknown argument expansion or a dynamic multi_label value fails closed.
    """
    import ast
    import inspect
    import textwrap
    signature = inspect.signature(nms_function)
    parameter = signature.parameters.get("multi_label")
    if parameter is None:
        return 0
    for owner in predictor_type.__mro__:
        method = owner.__dict__.get("postprocess")
        if method is None:
            continue
        try:
            tree = ast.parse(textwrap.dedent(inspect.getsource(method)))
        except (OSError, TypeError, SyntaxError):
            return 0
        calls = [node for node in ast.walk(tree) if isinstance(node, ast.Call)
                 and (isinstance(node.func, ast.Attribute) and node.func.attr == nms_function.__name__
                      or isinstance(node.func, ast.Name) and node.func.id == nms_function.__name__)]
        if not calls:
            continue
        if len(calls) != 1 or any(isinstance(value, ast.Starred) for value in calls[0].args) or any(keyword.arg is None for keyword in calls[0].keywords):
            return 0
        call = calls[0]
        labels = signature.parameters.get("labels")
        if labels is not None:
            label_position = list(signature.parameters).index("labels")
            label_expression = next((keyword.value for keyword in call.keywords if keyword.arg == "labels"), None)
            if label_expression is None and len(call.args) > label_position:
                label_expression = call.args[label_position]
            if label_expression is not None and not (isinstance(label_expression, (ast.Tuple, ast.List)) and not label_expression.elts):
                return 0
            if label_expression is None and labels.default != ():
                return 0
        position = list(signature.parameters).index("multi_label")
        expression = next((keyword.value for keyword in call.keywords if keyword.arg == "multi_label"), None)
        if expression is None and len(call.args) > position:
            expression = call.args[position]
        value = parameter.default if expression is None else expression.value if isinstance(expression, ast.Constant) else None
        if not isinstance(value, bool):
            return 0
        return max(1, class_count) if value else 1
    return 0


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

        capture = getattr(self, "decision_evaluation", False)
        capture_state = {"complete": False}
        if getattr(model, "task", "") in ("detect", "segment"):
            import inspect
            import logging
            from ultralytics.utils import LOGGER
            try:
                from ultralytics.utils.nms import non_max_suppression
            except ImportError:
                from ultralytics.utils.ops import non_max_suppression
            limit = inspect.signature(non_max_suppression).parameters.get("max_nms")
            proposal_limit = int(limit.default) if limit is not None else 0
            predictor_type = model.task_map[model.task]["predictor"]
            candidate_multiplier = nms_candidate_multiplier(predictor_type, non_max_suppression, len(self.class_names))

            capture_owner = self
            self.capture_state = capture_state
            class DecisionCapturePredictor(predictor_type):
                def postprocess(self, preds, img, orig_imgs, **options):
                    tensor = preds[0] if isinstance(preds, (tuple, list)) else preds
                    shape = tuple(getattr(tensor, "shape", ()))
                    # A single image with fewer raw proposals than max_nms cannot
                    # hit the pre-NMS candidate cap. Unknown layouts fail closed.
                    capture_owner.capture_state["complete"] = (len(shape) == 3 and shape[0] == 1
                                                  and (model.task == "detect" and shape[1] == 4 + len(capture_owner.class_names)
                                                       or model.task == "segment" and shape[1] > 4 + len(capture_owner.class_names))
                                                  and shape[-1] != 6 and not getattr(getattr(model, "model", None), "end2end", False)
                                                  and hasattr(tensor, "isfinite") and bool(tensor.isfinite().all().item())
                                                  and proposal_limit > 0 and max(shape[1:]) < proposal_limit
                                                  and candidate_multiplier > 0 and shape[-1] * candidate_multiplier < proposal_limit)
                    return super().postprocess(preds, img, orig_imgs, **options)

            class CaptureWarnings(logging.Handler):
                def emit(self, record):
                    if "NMS" in record.getMessage() and ("time" in record.getMessage().lower() or "limit" in record.getMessage().lower()):
                        capture_state["complete"] = False
            warning_handler = CaptureWarnings()
            if capture:
                kwargs.update(conf=0.0, max_det=proposal_limit or 30000)
            else:
                from ultralytics.cfg import DEFAULT_CFG_DICT
                kwargs["max_det"] = int(getattr(getattr(model, "predictor", None), "args", None).max_det) if getattr(model, "predictor", None) is not None else int(model.overrides.get("max_det", DEFAULT_CFG_DICT["max_det"]))
            if not hasattr(model, "_decision_capture_predictor_type"):
                model._decision_capture_predictor_type = DecisionCapturePredictor
                model.predictor = None
            kwargs["predictor"] = model._decision_capture_predictor_type
            LOGGER.addHandler(warning_handler)
            try:
                results = model.predict(**kwargs)
            finally:
                LOGGER.removeHandler(warning_handler)
        else:
            results = model.predict(**kwargs)
        if not results:
            raise ValueError(f"image decoding produced no result: {image_path}")

        result = results[0]
        height, width = self._read_shape(result)
        if width <= 0 or height <= 0:
            raise ValueError(f"image decoding produced an invalid size: {image_path}")
        candidates = self._build_candidates(result, width, height)
        return candidates, {
            "path": str(image_path),
            "width": width,
            "height": height,
            "captureConfidenceFloor": 0.0 if capture else kwargs["conf"],
            "hasCompletePredictions": capture_state["complete"] and len(candidates) < kwargs.get("max_det", 0),
            "captureCandidateLimit": kwargs.get("max_det"),
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
        self.training_stop_requested = threading.Event()
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

        try:
            run_id = normalize_run_id(get_first(message.payload, ["runId", "trainingRunId"], ""))
        except ValueError as exc:
            return self._training_start_failure(message, "InvalidTrainingRunId", str(exc))

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
            payload["runId"] = run_id
            self.training_stop_requested.clear()
            self.training_status = self._build_training_status(
                message.request_id,
                state="started",
                message=f"Ultralytics {model_name} {task} training accepted.",
                task=task,
                model=model_name,
                progress=0,
                training_weights=training_weights,
            )
            if run_id:
                self.training_status["runId"] = run_id
                self.training_status["runName"] = self._resolve_training_run_name(message.payload, model_name, task)
            self.training_thread = threading.Thread(
                target=self._run_training_job,
                args=(message.request_id, payload, writer),
                daemon=True,
                name="openvisionlab-ultralytics-training",
            )
            self.training_thread.start()

        result = {
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
        if run_id:
            result["runId"] = run_id
        return result

    def _run_training_job(self, request_id: str, payload: dict[str, Any], writer: JsonResponseWriter) -> None:
        model_name = normalize_model(payload.get("model")) or default_runtime_model()
        task = self._resolve_training_task(payload)
        data_yaml = self._resolve_training_data_path(payload)
        epochs = self._resolve_positive_int(payload, ["epoch", "epochs"], 50)
        image_size = self._resolve_positive_int(payload, ["imgSize", "imageSize", "imgsz"], self.detector.img_size)
        batch = self._resolve_positive_int(payload, ["batch", "batchSize"], 16)
        training_weights = str(payload.get("trainingWeights") or self._resolve_training_weights(payload, model_name, task))
        run_id = normalize_run_id(payload.get("runId"))
        display_run_name = self._resolve_training_run_name(payload, model_name, task)
        run_name = compose_training_run_name(display_run_name, run_id, f"openvisionlab-{model_name}-{task}")
        project_path = self._resolve_training_project_path(task)

        def send_status(
            state: str,
            message: str,
            progress: int | None = None,
            epoch: int | None = None,
            error: dict[str, Any] | None = None,
            checkpoint_metadata: dict[str, Any] | None = None,
            metrics: dict[str, Any] | None = None,
            result_weights_path: str = "",
        ) -> None:
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
                checkpoint_metadata=checkpoint_metadata,
            )
            status.update(metrics or {})
            if state == "completed" and result_weights_path:
                status["weightsPath"] = result_weights_path
            if run_id:
                status["runId"] = run_id
                status["runName"] = display_run_name
                status["outputRunName"] = run_name
            self.training_status = status
            try:
                writer.send(status)
            except OSError:
                pass

        try:
            from ultralytics import YOLO

            send_status("running", f"Ultralytics {model_name} {task} training started.", progress=0, epoch=0)
            model = YOLO(training_weights)
            last_metric_epoch = 0
            last_training_epoch = 0
            saved_checkpoint_epochs: dict[str, int] = {}

            def on_train_epoch_end(trainer: Any) -> None:
                nonlocal last_training_epoch
                current_epoch = int(getattr(trainer, "epoch", 0)) + 1
                last_training_epoch = current_epoch
                total_epochs = int(getattr(trainer, "epochs", epochs) or epochs)
                progress = int(max(0, min(100, round((current_epoch / max(total_epochs, 1)) * 100))))
                if self.training_stop_requested.is_set():
                    setattr(trainer, "stop", True)
                    send_status("stopping", "Training stop requested.", progress=progress, epoch=current_epoch)
                    return

                send_status("running", f"Epoch {current_epoch}/{total_epochs}", progress=progress, epoch=current_epoch)

            def on_train_batch_end(trainer: Any) -> None:
                if self.training_stop_requested.is_set():
                    setattr(trainer, "stop", True)

            def on_fit_epoch_end(trainer: Any) -> None:
                nonlocal last_metric_epoch
                current_epoch = int(getattr(trainer, "epoch", 0)) + 1
                # Final best-model validation increments trainer.epoch without training.
                if current_epoch != last_training_epoch or current_epoch <= last_metric_epoch:
                    return
                last_metric_epoch = current_epoch
                total_epochs = int(getattr(trainer, "epochs", epochs) or epochs)
                progress = int(max(0, min(100, round(current_epoch / max(total_epochs, 1) * 100))))
                send_status("stopping" if self.training_stop_requested.is_set() else "running", f"Epoch {current_epoch}/{total_epochs}",
                            progress=progress, epoch=current_epoch, metrics=training_epoch_metrics(trainer, task))

            def on_model_save(trainer: Any) -> None:
                current_epoch = int(getattr(trainer, "epoch", -1)) + 1
                if current_epoch != last_training_epoch or current_epoch < 1:
                    return
                if Path(trainer.last).is_file():
                    saved_checkpoint_epochs["last"] = current_epoch
                if trainer.best_fitness == trainer.fitness and Path(trainer.best).is_file():
                    saved_checkpoint_epochs["best"] = current_epoch

            if hasattr(model, "add_callback"):
                model.add_callback("on_train_epoch_end", on_train_epoch_end)
                model.add_callback("on_train_batch_end", on_train_batch_end)
                model.add_callback("on_fit_epoch_end", on_fit_epoch_end)
                model.add_callback("on_model_save", on_model_save)

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
            checkpoint_metadata = {}
            if save_dir:
                save_dir_path = Path(save_dir).expanduser().resolve()
                results_csv_path = save_dir_path / "results.csv"
                for checkpoint_role in ("best", "last"):
                    metadata = write_checkpoint_metadata(
                        save_dir_path / "weights" / f"{checkpoint_role}.pt",
                        checkpoint_role,
                        results_csv_path,
                        display_run_name,
                        run_id,
                        saved_checkpoint_epochs.get(checkpoint_role))
                    if metadata is not None:
                        checkpoint_metadata[checkpoint_role] = metadata
            if self.training_stop_requested.is_set():
                send_status("stopped", f"Ultralytics {model_name} {task} training stopped. {save_dir}".strip(), epoch=0)
            else:
                completed_message = f"Ultralytics {model_name} {task} training completed. {save_dir}".strip()
                best_weights = Path(save_dir).expanduser().resolve() / "weights" / "best.pt" if save_dir else None
                result_weights_path = str(best_weights) if best_weights is not None and best_weights.is_file() else ""
                send_status("completed", completed_message, progress=100, epoch=last_metric_epoch or None,
                            checkpoint_metadata=checkpoint_metadata, result_weights_path=result_weights_path)
        except Exception as exc:
            if self.training_stop_requested.is_set():
                send_status("stopped", "Ultralytics training stopped.")
            else:
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
        checkpoint_metadata: dict[str, Any] | None = None,
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
        if checkpoint_metadata:
            status["checkpointMetadata"] = checkpoint_metadata
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
        with self.training_lock:
            is_training = self.training_thread is not None and self.training_thread.is_alive()
            if is_training:
                self.training_stop_requested.set()
                training_request_id = str(self.training_status.get("requestId", ""))
                self.training_status = self._build_training_status(
                    training_request_id,
                    state="stopping",
                    message="Training stop requested.",
                    task=str(self.training_status.get("trainingTask", "detect")),
                    model=str(self.training_status.get("model", default_runtime_model())),
                    epoch=self.training_status.get("epoch"),
                    total_epochs=self.training_status.get("totalEpochs"),
                    training_weights=str(self.training_status.get("trainingWeights", "")),
                )

        return {
            "type": "StopTaskResult",
            "requestId": message.request_id,
            "ok": True,
            "state": "stopping" if is_training else "idle",
            "message": "Training stop requested." if is_training else "No training task is running.",
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
    detector = UltralyticsDetector(
        weights=Path(args.weights).resolve(),
        model_root=Path(args.model_root).resolve(),
        image_root=Path(args.image_root).resolve(),
        device=args.device,
        img_size=args.img_size,
        conf=args.conf,
        iou=args.iou,
        debug=args.debug,
    )
    detector.decision_evaluation = getattr(args, "decision_evaluation", False)
    return detector


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


def emit_smoke_test_result(args: argparse.Namespace, result: dict[str, Any]) -> None:
    payload = compact_json(result).decode("utf-8")
    result_json = getattr(args, "result_json", "")
    if result_json:
        with Path(result_json).open("x", encoding="utf-8", newline="\n") as result_file:
            result_file.write(payload + "\n")
        if result.get("ok"):
            print(f"SmokeTestResult saved: {result_json}", flush=True)
            return
    print(payload, flush=True)


def run_smoke_test(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    image_path_value = args.detect_file or args.image
    if not image_path_value:
        emit_smoke_test_result(args, {"type": "SmokeTestResult", "ok": False, "error": make_error("MissingImagePath", "--smoke-test requires --image or --detect-file.")})
        return 2

    image_path = resolve_image_path_for_cli(image_path_value, Path(args.image_root).resolve())
    requested_model = normalize_model(getattr(args, "model", ""))
    if requested_model and ultralytics_available() and requested_model not in runtime_detection_models():
        emit_smoke_test_result(args, {
            "type": "SmokeTestResult",
            "requestId": "smoke-test",
            "imageId": image_path.stem,
            "ok": False,
            "weightsPath": str(Path(args.weights).resolve()),
            "image": {"path": str(image_path)},
            "model": detector.status(),
            "capabilities": capability_payload(detector.model_root),
            "error": make_error("UnsupportedModel", unsupported_model_message("detection", requested_model)),
        })
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
        emit_smoke_test_result(args, result)
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
        emit_smoke_test_result(args, result)
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
        orig_shape = (80, 100)
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

    class FakePredictionModel:
        results: list[Any] = []

        def predict(self, **kwargs: Any) -> list[Any]:
            return self.results

    class EmptyPrediction:
        orig_shape = (80, 100)
        boxes: list[Any] = []

    detector.model = FakePredictionModel()
    decode_worker = LabelingUltralyticsWorker(detector)
    decode_message = IncomingMessage("DetectImage", request_id="req-decode", payload={"imagePath": str(source_file)})
    try:
        for results in ([], [type("InvalidPrediction", (), {"orig_shape": (0, 100)})()], [type("InvalidPrediction", (), {"orig_shape": (80, 0)})()]):
            detector.model.results = results
            decoded = decode_worker.handle_detect_image(decode_message)
            assert decoded["ok"] is False
            assert decoded["error"]["code"] == "DetectImageFailed"
            assert decoded["requestId"] == "req-decode"
            assert str(source_file) in decoded["error"]["message"]
        detector.model.results = [EmptyPrediction()]
        decoded = decode_worker.handle_detect_image(decode_message)
        assert decoded["ok"] is True and not decoded["candidates"]
        assert decoded["image"]["width"] == 100 and decoded["image"]["height"] == 80
        detector.model.results = [FakeResult()]
        decoded = decode_worker.handle_detect_image(decode_message)
        assert decoded["ok"] is True and len(decoded["candidates"]) == 1
    finally:
        detector.model = None

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
    worker.training_status = worker._build_training_status(
        "req-running",
        "running",
        "training",
        "segment",
        "yolov8",
        progress=25,
        epoch=1,
        total_epochs=4,
    )

    class LiveTrainingThread:
        def is_alive(self) -> bool:
            return True

    worker.training_thread = LiveTrainingThread()  # type: ignore[assignment]
    stop_result = worker.handle_stop_task(IncomingMessage("StopTask", request_id="req-stop"))
    assert stop_result["state"] == "stopping"
    assert worker.training_stop_requested.is_set()
    worker.training_thread = None

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
        workspace_weights = ("yolov8s.pt", "yolov8s-seg.pt", "yolov8s-cls.pt", "yolo11s.pt", "yolo11m.pt", "yolo11s-seg.pt", "yolo11s-cls.pt")
        for name in workspace_weights:
            assert name in cache_payload["missingTrainingWeights"]
            assert worker._training_weight_requires_download(name)
            (cached_weight_root / name).write_text("weights", encoding="utf-8")
        prepared = training_weight_cache_payload(cached_weight_root)
        for name in workspace_weights:
            assert name in prepared["cachedTrainingWeights"] and name not in prepared["missingTrainingWeights"]
            readiness_key = "runtimeReadyTrainingWeights" if training_weight_model_name(name) in runtime_supported_models() else "runtimeBlockedTrainingWeights"
            assert name in prepared[readiness_key]
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
            save_dir = Path(kwargs["project"]) / kwargs["name"]
            (save_dir / "weights").mkdir(parents=True, exist_ok=True)
            for role in ("best", "last"):
                (save_dir / "weights" / f"{role}.pt").write_bytes(b"owned-trained-checkpoint")
            (save_dir / "results.csv").write_text("epoch,train/box_loss\n1,0.5\n2,0.4\n", encoding="utf-8")

            class FakeTrainer:
                epoch = 0
                epochs = 1
                tloss = [0.4, 0.2]
                metrics = {"val/seg_loss": 0.5, "val/cls_loss": 0.2, "metrics/mAP50(M)": 0.73}
                def label_loss_items(self, values: Any, prefix: str = "train") -> dict[str, float]:
                    return {f"{prefix}/seg_loss": values[0], f"{prefix}/cls_loss": values[1]}

            trainer = FakeTrainer()
            trainer.epochs = kwargs["epochs"]
            trainer.best, trainer.last = save_dir / "weights" / "best.pt", save_dir / "weights" / "last.pt"
            trainer.best_fitness = trainer.fitness = 0.73
            for epoch in range(min(trainer.epochs, 2)):
                trainer.epoch = epoch
                callbacks["on_train_epoch_end"](trainer)
                callbacks["on_fit_epoch_end"](trainer)
                callbacks["on_model_save"](trainer)
            callbacks["on_fit_epoch_end"](trainer)  # duplicate callback for an already published epoch
            trainer.epoch += 1  # installed Ultralytics final_eval advances the logging step
            trainer.metrics = {"val/seg_loss": 99, "metrics/mAP50(M)": 0.99}
            callbacks["on_fit_epoch_end"](trainer)  # final best-model evaluation is not a new epoch

            class FakeResult:
                pass

            result = FakeResult()
            result.save_dir = str(save_dir)
            return result

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
            completed = next(item for item in sent_statuses if item["state"] == "completed")
            assert completed["trainingWeights"] == "yolov8n-seg.pt"
            assert Path(completed["weightsPath"]).is_file()
            assert completed["weightsPath"] != completed["trainingWeights"]
            assert completed["checkpointMetadata"]["best"]["artifactId"] == checkpoint_artifact_id(Path(completed["weightsPath"]))
            assert worker.training_status["weightsPath"] == completed["weightsPath"]
            assert worker.handle_model_status(IncomingMessage("ModelStatus"))["training"]["weightsPath"] == completed["weightsPath"]
            assert all("weightsPath" not in item for item in sent_statuses if item["state"] != "completed")
            received_metrics = [item for item in sent_statuses if "validationMetricValue" in item]
            assert len(received_metrics) == 1 and received_metrics[0]["validationMetricKey"] == "maskMap50"
            assert math.isclose(received_metrics[0]["trainLoss"], 0.6) and math.isclose(received_metrics[0]["validationLoss"], 0.7)
            assert received_metrics[0]["validationMetricValue"] == 0.73
            assert [item["epoch"] for item in sent_statuses if item["state"] == "completed"] == [1]

        sent_statuses.clear()
        worker._run_training_job("req-partial-epochs", {"model": "yolov8", "task": "segment",
                                 "dataYaml": str(Path(__file__).resolve()), "allowModelDownload": True, "epochs": 50}, FakeWriter())
        received_metrics = [item for item in sent_statuses if "validationMetricValue" in item]
        assert [item["epoch"] for item in received_metrics] == [1, 2]
        assert [item["epoch"] for item in sent_statuses if item["state"] == "completed"] == [2]

        checkpoint = download_guard_root / "last.pt"
        checkpoint.write_bytes(b"owned-checkpoint-bytes-after-strip")
        results_csv = download_guard_root / "results.csv"
        results_csv.write_text("epoch,train/box_loss\n1,0.5\n2,0.4\n", encoding="utf-8")
        metadata = write_checkpoint_metadata(checkpoint, "last", results_csv, "observed-save", "run-proof", observed_epoch=2)
        assert metadata["epoch"] == 2 and metadata["artifactId"] == checkpoint_artifact_id(checkpoint)
        assert metadata["runId"] == "run-proof"
        assert write_checkpoint_metadata(checkpoint, "last", results_csv, "observed-save", observed_epoch=3) is None
        assert write_checkpoint_metadata(checkpoint, "last", results_csv, "observed-save", observed_epoch=0) is None
        assert write_checkpoint_metadata(download_guard_root / "absent.pt", "last", results_csv, "observed-save", observed_epoch=2) is None
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


def run_capture_self_test() -> int:
    """Focused fake-predictor proof checks; no model, image inference or network."""
    from types import SimpleNamespace
    from ultralytics.utils import LOGGER

    def non_max_suppression(prediction, conf_thres=0.25, iou_thres=0.45, classes=None, agnostic=False, multi_label=False, labels=(), max_det=300):
        return prediction

    class SingleLabelPredictor:
        def __init__(self, args):
            self.args = args

        def postprocess(self, preds, img, orig_imgs, **options):
            return non_max_suppression(preds, self.args.conf, self.args.iou, None, False, max_det=self.args.max_det)

    class MultiLabelPredictor(SingleLabelPredictor):
        def postprocess(self, preds, img, orig_imgs, **options):
            return non_max_suppression(preds, self.args.conf, self.args.iou, None, False, multi_label=True, max_det=self.args.max_det)

    class UnknownLabelPredictor(SingleLabelPredictor):
        def postprocess(self, preds, img, orig_imgs, **options):
            return non_max_suppression(preds, self.args.conf, self.args.iou, None, False, multi_label=self.args.agnostic_nms, max_det=self.args.max_det)

    class FakeTensor:
        def __init__(self, shape, finite=True):
            self.shape = shape
            self.finite = finite

        def isfinite(self):
            return SimpleNamespace(all=lambda: SimpleNamespace(item=lambda: self.finite))

    class FakeModel:
        task = "detect"

        def __init__(self, predictor_type=SingleLabelPredictor):
            self.task_map = {"detect": {"predictor": predictor_type}}
            self.names = {index: f"fixture-class-{index}" for index in range(80)}
            self.model = SimpleNamespace(end2end=False)
            self.predictor = None
            self.overrides = {}
            self.tensor = FakeTensor((1, 84, 2100))
            self.count = 452
            self.warn = False

        def predict(self, **options):
            args = SimpleNamespace(conf=options["conf"], iou=options["iou"], max_det=options["max_det"], agnostic_nms=False)
            if self.predictor is None:
                self.predictor = options["predictor"](args)
            else:
                self.predictor.args = args
            self.predictor.postprocess(self.tensor, None, None)
            if self.warn:
                LOGGER.warning("NMS time limit 2.050s exceeded")
            return [SimpleNamespace(orig_shape=(220, 260))]

    def detector_for(model, image):
        detector = UltralyticsDetector(image, image.parent, image.parent, "cpu", 320, 0.25, 0.45)
        detector.model = model
        detector.class_names = detector._read_class_names(model)
        detector.decision_evaluation = True
        detector._build_candidates = lambda result, width, height: [{} for _ in range(model.count)]
        return detector

    checks = []
    with tempfile.TemporaryDirectory(prefix="capture-proof-", dir=os.environ.get("TEMP")) as directory:
        image = Path(directory) / "owned-fake-input.bmp"
        image.write_bytes(b"Fake predictor marker, not an image inference fixture.")
        model = FakeModel()
        detector = detector_for(model, image)
        handler_count = len(LOGGER.handlers)
        _, metadata = detector.detect_path(image)
        assert metadata["hasCompletePredictions"] and metadata["captureConfidenceFloor"] == 0
        checks.append("80-class single-label capture is complete below actual NMS caps")
        model.tensor = FakeTensor((1, 84, 30001))
        assert not detector.detect_path(image)[1]["hasCompletePredictions"]
        checks.append("pre-NMS proposal overflow is unavailable")
        limited_model = FakeModel(); limited_model.count = 300
        limited_detector = detector_for(limited_model, image); limited_detector.decision_evaluation = False
        limited_metadata = limited_detector.detect_path(image)[1]
        assert not limited_metadata["hasCompletePredictions"] and limited_metadata["captureCandidateLimit"] == 300 and limited_metadata["captureConfidenceFloor"] == 0.25
        checks.append("post-NMS output at candidate limit is unavailable")
        model.tensor = FakeTensor((1, 84, 2100)); model.warn = True
        assert not detector.detect_path(image)[1]["hasCompletePredictions"]
        checks.append("NMS time warning invalidates otherwise complete capture")
        model.warn = False
        assert detector.detect_path(image)[1]["hasCompletePredictions"] and len(LOGGER.handlers) == handler_count
        checks.append("valid recovery reuses predictor and releases warning handlers")
        model.tensor = FakeTensor((1, 2100, 84))
        assert not detector.detect_path(image)[1]["hasCompletePredictions"]
        model.tensor = FakeTensor((1, 300, 6))
        assert not detector.detect_path(image)[1]["hasCompletePredictions"]
        checks.append("unknown layout and end-to-end output remain unavailable")
        model.tensor = FakeTensor((1, 84, 2100), finite=False)
        assert not detector.detect_path(image)[1]["hasCompletePredictions"]
        checks.append("non-finite raw outputs remain unavailable")
        assert not detector_for(FakeModel(MultiLabelPredictor), image).detect_path(image)[1]["hasCompletePredictions"]
        checks.append("actual multi-label expansion retains conservative pre-NMS bound")
        assert not detector_for(FakeModel(UnknownLabelPredictor), image).detect_path(image)[1]["hasCompletePredictions"]
        checks.append("dynamic multi-label configuration fails closed")
    print(compact_json({"type": "CaptureProofSelfTestResult", "ok": True, "checks": checks}).decode("utf-8"), flush=True)
    return 0


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
    parser.add_argument("--capture-self-test", action="store_true")
    parser.add_argument("--smoke-test", action="store_true")
    parser.add_argument("--decision-evaluation", action="store_true")
    parser.add_argument("--result-json", default="")
    parser.add_argument("--detect-file", default="")
    parser.add_argument("--image", default="")
    args = parser.parse_args(argv)
    if args.result_json and not args.decision_evaluation:
        parser.error("--result-json requires --decision-evaluation.")
    return args


def run_yolov5_decision_capture(args: argparse.Namespace) -> int:
    """Explicit raw capture through the configured local YOLOv5 source APIs.

    No external client is edited, no hub/network model download is requested,
    and the normal YOLOv5 application runtime remains unchanged.
    """
    image_path = Path(args.image).resolve()
    started = time.perf_counter()
    try:
        import inspect
        import logging
        import re
        import cv2
        import numpy as np
        import torch
        root = Path(args.model_root).resolve()
        if not (root / "models" / "common.py").is_file():
            root = root / "yolov5Master"
        if not (root / "models" / "common.py").is_file():
            raise FileNotFoundError("Configured YOLOv5 source does not contain models/common.py.")
        sys.path.insert(0, str(root))
        from models.common import DetectMultiBackend
        from utils.augmentations import letterbox
        from utils import general
        from utils.torch_utils import select_device
        device = select_device(args.device or "cpu")
        model = DetectMultiBackend(args.weights, device=device)
        source = cv2.imread(str(image_path))
        if source is None:
            raise ValueError("Evaluation image could not be decoded.")
        height, width = source.shape[:2]
        stride = int(model.stride)
        size = int(math.ceil(args.img_size / stride) * stride)
        resized = letterbox(source, size, stride=stride, auto=False)[0]
        tensor = torch.from_numpy(np.ascontiguousarray(resized.transpose((2, 0, 1))[::-1])).to(device).float() / 255.0
        tensor = tensor.unsqueeze(0)
        with torch.no_grad():
            predictions = model(tensor)
        predictions = predictions[0] if isinstance(predictions, (tuple, list)) else predictions
        nms_source = inspect.getsource(general.non_max_suppression)
        cap_match = re.search(r"max_nms\s*=\s*(\d+)", nms_source)
        signature_cap = inspect.signature(general.non_max_suppression).parameters.get("max_nms")
        proposal_limit = int(signature_cap.default) if signature_cap is not None else int(cap_match.group(1)) if cap_match else 0
        state = {"complete": proposal_limit > 0 and predictions.ndim == 3 and predictions.shape[0] == 1
                            and predictions.shape[1] < proposal_limit and bool(predictions.isfinite().all().item())}

        class CaptureWarnings(logging.Handler):
            def emit(self, record):
                if "NMS" in record.getMessage() and ("time" in record.getMessage().lower() or "limit" in record.getMessage().lower()):
                    state["complete"] = False
        warning_handler = CaptureWarnings()
        general.LOGGER.addHandler(warning_handler)
        try:
            detections = general.non_max_suppression(predictions, conf_thres=0.0, iou_thres=args.iou, max_det=proposal_limit or 30000)[0]
        finally:
            general.LOGGER.removeHandler(warning_handler)
        scale = getattr(general, "scale_boxes", None) or getattr(general, "scale_coords")
        if len(detections):
            detections[:, :4] = scale(tensor.shape[2:], detections[:, :4], source.shape).round()
        candidates = [{"index": index + 1, "classId": int(row[5]), "className": str(model.names[int(row[5])]),
                       "confidence": float(row[4]), "x": float(row[0]), "y": float(row[1]),
                       "width": float(row[2] - row[0]), "height": float(row[3] - row[1])}
                      for index, row in enumerate(detections.detach().cpu().tolist())]
        emit_smoke_test_result(args, {"type": "SmokeTestResult", "ok": True, "weightsPath": str(Path(args.weights).resolve()),
                            "elapsedMs": int((time.perf_counter() - started) * 1000), "candidates": candidates,
                            "image": {"path": str(image_path), "width": width, "height": height, "captureConfidenceFloor": 0.0,
                                      "captureCandidateLimit": proposal_limit or 30000,
                                      "hasCompletePredictions": state["complete"] and len(candidates) < (proposal_limit or 30000)}})
        return 0
    except Exception as exc:
        emit_smoke_test_result(args, {"type": "SmokeTestResult", "ok": False, "error": make_error("DecisionCaptureFailed", exc, include_trace=args.debug)})
        return 1


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()
    if args.capture_self_test:
        return run_capture_self_test()
    if args.decision_evaluation and normalize_model(args.model) == "yolov5":
        return run_yolov5_decision_capture(args)
    if args.smoke_test or args.detect_file or args.image:
        return run_smoke_test(args)
    return run_client(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
