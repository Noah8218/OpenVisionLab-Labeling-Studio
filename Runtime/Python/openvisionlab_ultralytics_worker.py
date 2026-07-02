#!/usr/bin/env python3
"""Ultralytics TCP worker for OpenVisionLab Labeling Studio.

This worker is intentionally limited to inference. It exposes the same JSON
protocol shape as the existing YOLOv5 worker so the WPF app can switch model
adapters without silently falling back to YOLOv5.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import socket
import sys
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


PACKET_SEPARATOR = b"\n\n"
SUPPORTED_MODELS = ("yolov8", "yolo11")
TRAINING_MODELS: tuple[str, ...] = ()
DETECTION_MODELS = SUPPORTED_MODELS
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


def normalize_model(value: Any) -> str:
    lower = str(value or "").strip().lower()
    if lower in {"yolov8", "yolo8", "v8"}:
        return "yolov8"
    if lower in {"yolo11", "yolov11", "v11"}:
        return "yolo11"
    return lower


def ultralytics_available() -> bool:
    return importlib.util.find_spec("ultralytics") is not None


def capability_payload() -> dict[str, list[str]]:
    if not ultralytics_available():
        return {
            "supportedModels": [],
            "trainingModels": [],
            "detectionModels": [],
        }

    return {
        "supportedModels": list(SUPPORTED_MODELS),
        "trainingModels": list(TRAINING_MODELS),
        "detectionModels": list(DETECTION_MODELS),
    }


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
        "imageRoot": str(image_root),
        "imageRootExists": image_root.exists(),
        "ultralyticsInstalled": ultralytics_available(),
    }


class JsonResponseWriter:
    def __init__(self, sock: socket.socket):
        self.sock = sock

    def send(self, envelope: dict[str, Any]) -> None:
        envelope.setdefault("version", 1)
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
            return []

        names = getattr(result, "names", getattr(self.model, "names", {}))
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

            candidates.append(
                {
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
            )

        return candidates


class LabelingUltralyticsWorker:
    def __init__(self, detector: UltralyticsDetector, debug: bool = False):
        self.detector = detector
        self.debug = debug
        self.started_at_utc = utc_now()

    def handle(self, message: IncomingMessage) -> dict[str, Any]:
        try:
            if message.message_type == "HealthCheck":
                return self.handle_health_check(message)
            if message.message_type == "ModelStatus":
                return self.handle_model_status(message)
            if message.message_type == "DetectImage":
                return self.handle_detect_image(message)
            if message.message_type == "TrainYolo":
                return self.handle_train_yolo(message)
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
        capabilities = capability_payload()
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
        capabilities = capability_payload()
        return {
            "type": "ModelStatusResult",
            "requestId": message.request_id,
            "ok": status["state"] == "ready",
            "state": status["state"],
            "capabilities": capabilities,
            "model": status,
            "error": status["lastError"],
        }

    def handle_detect_image(self, message: IncomingMessage) -> dict[str, Any]:
        requested_model = normalize_model(get_first(message.payload, ["model", "adapter"], ""))
        if requested_model and requested_model not in DETECTION_MODELS:
            return {
                "type": "DetectImageResult",
                "requestId": message.request_id,
                "imageId": message.image_id,
                "ok": False,
                "candidates": [],
                "model": self.detector.status(),
                "error": make_error("UnsupportedModel", f"this worker does not support detection model: {requested_model}"),
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

    def handle_train_yolo(self, message: IncomingMessage) -> dict[str, Any]:
        return {
            "type": "TrainYoloResult",
            "requestId": message.request_id,
            "ok": False,
            "state": "failed",
            "error": make_error("TrainingNotSupported", "Ultralytics training is not wired in this worker yet. Use YOLOv5 for training or connect a training-capable adapter."),
        }

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
            print(compact_json({"type": "ModelStatusResult", "ok": True, "model": detector.status(), "capabilities": capability_payload()}).decode("utf-8"), flush=True)
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
            writer.send(worker.handle(message))
            if args.once and handled >= 1:
                return 0


def run_smoke_test(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    image_path_value = args.detect_file or args.image
    if not image_path_value:
        print(compact_json({"type": "SmokeTestResult", "ok": False, "error": make_error("MissingImagePath", "--smoke-test requires --image or --detect-file.")}).decode("utf-8"), flush=True)
        return 2

    image_path = resolve_image_path_for_cli(image_path_value, Path(args.image_root).resolve())
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
            "capabilities": capability_payload(),
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
            "capabilities": capability_payload(),
            "error": make_error("SmokeTestFailed", exc, include_trace=args.debug),
        }
        print(compact_json(result).decode("utf-8"), flush=True)
        return 1


def run_self_test() -> int:
    buffer = bytearray(
        b'{"type":"HealthCheck","requestId":"req-health"}\n'
        b'{"type":"ModelStatus","requestId":"req-model","ensureLoaded":false}\n'
        b'{"type":"DetectImage","requestId":"req-detect","imageId":"img-1","imagePath":"sample.bmp","model":"yolo11"}\n'
        b'StartTraining\n\n{"requestId":"req-train","model":"yolo11"}'
    )
    messages = list(parse_messages(buffer))
    assert len(messages) == 4, f"expected 4 messages, got {len(messages)}"
    assert messages[0].message_type == "HealthCheck"
    assert messages[1].message_type == "ModelStatus"
    assert messages[2].message_type == "DetectImage"
    assert messages[2].payload["model"] == "yolo11"
    assert messages[3].message_type == "TrainYolo"
    assert not buffer

    capabilities = capability_payload()
    assert "supportedModels" in capabilities
    assert "yolo11" in capabilities["detectionModels"]
    assert capabilities["trainingModels"] == []
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
