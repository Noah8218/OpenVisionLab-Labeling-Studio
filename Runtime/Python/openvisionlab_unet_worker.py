#!/usr/bin/env python3
"""Small, dependency-light U-Net TCP worker for OpenVisionLab Labeling Studio.

The worker deliberately consumes the app-owned U-Net export (images/<split>,
masks/<split>, classes.json) instead of a third-party repository format.  It
uses the existing StartTraining/DetectImage protocol so a recipe can switch
between YOLO segmentation and U-Net without changing its canonical labels.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import socket
import sys
import threading
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

PACKET_SEPARATOR = b"\n\n"
LEGACY_TYPE_MAP = {
    "StartTraining": "TrainYolo",
    "StopTraining": "StopTask",
    "StartDefect": "DetectImage",
    "StopDefect": "StopTask",
}


@dataclass
class IncomingMessage:
    message_type: str
    request_id: str = ""
    image_id: str = ""
    payload: dict[str, Any] = field(default_factory=dict)
    raw_type: str = ""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def compact_json(value: dict[str, Any]) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def make_error(code: str, error: str | Exception, include_trace: bool = False) -> dict[str, Any]:
    result: dict[str, Any] = {"code": code, "message": str(error)}
    if isinstance(error, Exception):
        result["exceptionType"] = type(error).__name__
    if include_trace:
        result["trace"] = traceback.format_exc()
    return result


def first_value(payload: dict[str, Any], names: Iterable[str], default: Any = None) -> Any:
    for name in names:
        value = payload.get(name)
        if value is not None and value != "":
            return value
    return default


def positive_int(value: Any, default: int) -> int:
    try:
        return max(1, int(float(value)))
    except (TypeError, ValueError):
        return default


def normalize_model(value: Any) -> str:
    model = str(value or "").strip().lower().replace("-", "")
    return "unet" if model in {"unet", "u net"} else model


def normalize_task(value: Any) -> str:
    task = str(value or "segment").strip().lower()
    return "segment" if task in {"seg", "segment", "segmentation"} else task


def torch_dependencies() -> tuple[Any, Any, Any, Any, Any]:
    try:
        import numpy as np
        import torch
        import torch.nn as nn
        import torch.nn.functional as functional
        from PIL import Image
        return np, torch, nn, functional, Image
    except Exception as exc:  # ImportError is not enough for broken CUDA DLLs.
        raise RuntimeError("U-Net worker requires torch, numpy, and Pillow in the selected Python environment.") from exc


def runtime_available() -> bool:
    try:
        torch_dependencies()
        return True
    except Exception:
        return False


def capability_payload() -> dict[str, list[str]]:
    return {
        "supportedModels": ["unet"],
        "trainingModels": ["unet"],
        "detectionModels": ["unet"],
        "segmentationModels": ["unet"],
        "classificationModels": [],
    }


class JsonResponseWriter:
    def __init__(self, sock: socket.socket):
        self.sock = sock
        self.lock = threading.Lock()

    def send(self, envelope: dict[str, Any]) -> None:
        with self.lock:
            self.sock.sendall(compact_json(envelope) + b"\n")


def build_unet(in_channels: int, class_count: int, base_channels: int):
    _, torch, nn, functional, _ = torch_dependencies()

    class ConvBlock(nn.Module):
        def __init__(self, source_channels: int, target_channels: int):
            super().__init__()
            self.layers = nn.Sequential(
                nn.Conv2d(source_channels, target_channels, kernel_size=3, padding=1),
                nn.ReLU(inplace=True),
                nn.Conv2d(target_channels, target_channels, kernel_size=3, padding=1),
                nn.ReLU(inplace=True),
            )

        def forward(self, value):
            return self.layers(value)

    class TinyUnet(nn.Module):
        def __init__(self):
            super().__init__()
            self.enc1 = ConvBlock(in_channels, base_channels)
            self.enc2 = ConvBlock(base_channels, base_channels * 2)
            self.bridge = ConvBlock(base_channels * 2, base_channels * 4)
            self.pool = nn.MaxPool2d(2)
            self.up2 = nn.ConvTranspose2d(base_channels * 4, base_channels * 2, kernel_size=2, stride=2)
            self.dec2 = ConvBlock(base_channels * 4, base_channels * 2)
            self.up1 = nn.ConvTranspose2d(base_channels * 2, base_channels, kernel_size=2, stride=2)
            self.dec1 = ConvBlock(base_channels * 2, base_channels)
            self.output = nn.Conv2d(base_channels, class_count, kernel_size=1)

        def forward(self, value):
            first = self.enc1(value)
            second = self.enc2(self.pool(first))
            bridge = self.bridge(self.pool(second))
            up_second = self.up2(bridge)
            if up_second.shape[-2:] != second.shape[-2:]:
                up_second = functional.interpolate(up_second, size=second.shape[-2:], mode="bilinear", align_corners=False)
            decoded_second = self.dec2(torch.cat([second, up_second], dim=1))
            up_first = self.up1(decoded_second)
            if up_first.shape[-2:] != first.shape[-2:]:
                up_first = functional.interpolate(up_first, size=first.shape[-2:], mode="bilinear", align_corners=False)
            return self.output(self.dec1(torch.cat([first, up_first], dim=1)))

    return TinyUnet()


def load_classes(data_root: Path) -> list[str]:
    path = data_root / "classes.json"
    if not path.is_file():
        raise FileNotFoundError(f"classes.json was not found: {path}")
    # The C# export writes UTF-8 with BOM; utf-8-sig also accepts UTF-8 without it.
    raw = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(raw, list):
        raise ValueError("classes.json must contain the app-owned U-Net class array.")
    classes: list[str] = []
    for expected_index, entry in enumerate(raw, start=1):
        if not isinstance(entry, dict):
            raise ValueError("classes.json contains an invalid class contract entry.")
        index = int(entry.get("Index", entry.get("index", 0)))
        name = str(entry.get("Name", entry.get("name", ""))).strip()
        if index != expected_index or not name:
            raise ValueError("classes.json must use contiguous one-based class indices and non-empty names.")
        classes.append(name)
    if not classes:
        raise ValueError("classes.json must contain at least one foreground class.")
    return classes


class SegmentationDataset:
    def __init__(self, data_root: Path, split: str, image_size: int, class_count: int):
        self.data_root = data_root
        self.split = split
        self.image_size = image_size
        self.class_count = class_count
        image_root = data_root / "images" / split
        mask_root = data_root / "masks" / split
        image_extensions = {".bmp", ".jpeg", ".jpg", ".png", ".tif", ".tiff"}
        self.entries: list[tuple[Path, Path]] = []
        for image_path in sorted(path for path in image_root.rglob("*") if path.suffix.lower() in image_extensions):
            relative = image_path.relative_to(image_root).with_suffix(".png")
            mask_path = mask_root / relative
            if not mask_path.is_file():
                raise FileNotFoundError(f"missing U-Net mask for {image_path}: {mask_path}")
            self.entries.append((image_path, mask_path))
        if not self.entries:
            raise ValueError(f"U-Net export has no images for split '{split}': {image_root}")

    def __len__(self) -> int:
        return len(self.entries)

    def __getitem__(self, index: int):
        np, torch, _, _, Image = torch_dependencies()
        image_path, mask_path = self.entries[index]
        resampling = Image.Resampling
        image = Image.open(image_path).convert("RGB").resize((self.image_size, self.image_size), resampling.BILINEAR)
        mask = Image.open(mask_path).convert("L").resize((self.image_size, self.image_size), resampling.NEAREST)
        image_array = np.asarray(image, dtype=np.float32) / 255.0
        mask_array = np.asarray(mask, dtype=np.int64)
        if int(mask_array.max(initial=0)) > self.class_count:
            raise ValueError(f"mask includes a class index above the class contract: {mask_path}")
        return torch.from_numpy(image_array).permute(2, 0, 1), torch.from_numpy(mask_array)


def resolve_data_root(value: Any, image_root: Path) -> Path:
    candidate = Path(str(value or "")).expanduser()
    if not str(candidate):
        return Path()
    if candidate.is_absolute():
        return candidate.resolve()
    if candidate.exists():
        return candidate.resolve()
    return (image_root / candidate).resolve()


def resolve_image_path(value: Any, image_root: Path) -> Path:
    candidate = Path(str(value or "")).expanduser()
    if candidate.is_absolute() or candidate.exists():
        return candidate.resolve()
    return (image_root / candidate).resolve()


def evaluate_foreground_quality(model: Any, loader: Any, loss_function: Any, device: Any, class_count: int) -> tuple[float, float, bool, Any, Any, Any, Any, Any]:
    np, torch, _, _, _ = torch_dependencies()
    validation_loss = 0.0
    true_counts = np.zeros(class_count + 1, dtype=np.int64)
    predicted_counts = np.zeros(class_count + 1, dtype=np.int64)
    intersections = np.zeros(class_count + 1, dtype=np.int64)
    predicted_image_counts = np.zeros(class_count + 1, dtype=np.int64)
    with torch.no_grad():
        for images, masks in loader:
            images = images.to(device)
            masks = masks.to(device)
            logits = model(images)
            validation_loss += float(loss_function(logits, masks).detach().cpu())
            predictions = logits.argmax(dim=1)
            for class_index in range(1, class_count + 1):
                targets_for_class = masks == class_index
                predictions_for_class = predictions == class_index
                true_counts[class_index] += int(targets_for_class.sum().item())
                predicted_counts[class_index] += int(predictions_for_class.sum().item())
                intersections[class_index] += int((targets_for_class & predictions_for_class).sum().item())
                predicted_image_counts[class_index] += int(predictions_for_class.flatten(start_dim=1).any(dim=1).sum().item())
    validation_loss /= max(1, len(loader))
    if np.any(true_counts[1:] <= 0):
        raise ValueError("foreground-quality selection requires valid support for every configured U-Net class.")
    class_dice = np.zeros(class_count, dtype=np.float64)
    for index in range(class_count):
        denominator = true_counts[index + 1] + predicted_counts[index + 1]
        class_dice[index] = 0.0 if denominator == 0 else (2.0 * intersections[index + 1]) / denominator
    macro_dice = float(class_dice.mean())
    all_classes_have_overlap = bool(np.all(intersections[1:] > 0))
    return validation_loss, macro_dice, all_classes_have_overlap, class_dice, true_counts, predicted_counts, intersections, predicted_image_counts


def is_better_foreground_candidate(
    candidate_all_classes_have_overlap: bool,
    candidate_macro_dice: float,
    candidate_loss: float,
    best_all_classes_have_overlap: bool,
    best_macro_dice: float,
    best_loss: float,
) -> bool:
    if candidate_all_classes_have_overlap != best_all_classes_have_overlap:
        return candidate_all_classes_have_overlap
    if candidate_macro_dice != best_macro_dice:
        return candidate_macro_dice > best_macro_dice
    return candidate_loss <= best_loss


def train_dataset(
    data_root: Path,
    model_root: Path,
    run_name: str,
    epochs: int,
    batch_size: int,
    image_size: int,
    device_text: str,
    progress: callable | None = None,
    foreground_quality_selection: bool = False,
) -> dict[str, Any]:
    np, torch, _, _, _ = torch_dependencies()
    from torch.utils.data import DataLoader

    classes = load_classes(data_root)
    train_data = SegmentationDataset(data_root, "train", image_size, len(classes))
    valid_root = data_root / "images" / "valid"
    valid_data = SegmentationDataset(data_root, "valid", image_size, len(classes)) if valid_root.is_dir() and any(valid_root.rglob("*")) else None
    if foreground_quality_selection and valid_data is None:
        raise ValueError("foreground-quality selection requires a canonical valid split.")
    torch.manual_seed(17)
    np.random.seed(17)
    device = torch.device(device_text if device_text else ("cuda" if torch.cuda.is_available() else "cpu"))
    model = build_unet(3, len(classes) + 1, 16).to(device)
    optimizer = torch.optim.Adam(model.parameters(), lr=0.001)
    loss_function = torch.nn.CrossEntropyLoss()
    train_loader = DataLoader(train_data, batch_size=max(1, min(batch_size, len(train_data))), shuffle=True, num_workers=0)
    valid_loader = None if valid_data is None else DataLoader(valid_data, batch_size=max(1, min(batch_size, len(valid_data))), shuffle=False, num_workers=0)
    run_root = model_root / "runs" / "segment" / run_name
    weights_root = run_root / "weights"
    weights_root.mkdir(parents=True, exist_ok=True)
    metrics: list[tuple[Any, ...]] = []
    best_loss = float("inf")
    best_foreground_macro_dice = float("-inf")
    best_all_classes_have_overlap = False
    best_path = weights_root / "best.pt"
    last_path = weights_root / "last.pt"
    for epoch in range(1, epochs + 1):
        model.train()
        train_loss = 0.0
        for images, masks in train_loader:
            images = images.to(device)
            masks = masks.to(device)
            optimizer.zero_grad(set_to_none=True)
            loss = loss_function(model(images), masks)
            loss.backward()
            optimizer.step()
            train_loss += float(loss.detach().cpu())
        train_loss /= max(1, len(train_loader))
        validation_loss = train_loss
        validation_foreground_macro_dice = 0.0
        validation_all_classes_have_overlap = False
        validation_class_dice = None
        validation_true_counts = None
        validation_predicted_counts = None
        validation_intersections = None
        validation_predicted_image_counts = None
        if valid_loader is not None:
            model.eval()
            if foreground_quality_selection:
                (
                    validation_loss,
                    validation_foreground_macro_dice,
                    validation_all_classes_have_overlap,
                    validation_class_dice,
                    validation_true_counts,
                    validation_predicted_counts,
                    validation_intersections,
                    validation_predicted_image_counts,
                ) = evaluate_foreground_quality(model, valid_loader, loss_function, device, len(classes))
            else:
                validation_loss = 0.0
                with torch.no_grad():
                    for images, masks in valid_loader:
                        validation_loss += float(loss_function(model(images.to(device)), masks.to(device)).detach().cpu())
                validation_loss /= max(1, len(valid_loader))
        checkpoint = {
            "format": "openvisionlab-unet-v1",
            "classes": classes,
            "imageSize": image_size,
            "baseChannels": 16,
            "stateDict": model.state_dict(),
            "epoch": epoch,
            "trainLoss": train_loss,
            "validationLoss": validation_loss,
        }
        if foreground_quality_selection:
            checkpoint.update({
                "selectionMetric": "valid-all-class-overlap-then-foreground-macro-dice-v1",
                "validationForegroundMacroDice": validation_foreground_macro_dice,
                "validationAllClassesHaveOverlap": validation_all_classes_have_overlap,
                "validationClassDice": [float(value) for value in validation_class_dice],
                "validationTruePixelCounts": [int(value) for value in validation_true_counts],
                "validationPredictedPixelCounts": [int(value) for value in validation_predicted_counts],
                "validationIntersectionPixelCounts": [int(value) for value in validation_intersections],
                "validationPredictedImageCounts": [int(value) for value in validation_predicted_image_counts],
            })
        torch.save(checkpoint, last_path)
        select_best = is_better_foreground_candidate(
            validation_all_classes_have_overlap,
            validation_foreground_macro_dice,
            validation_loss,
            best_all_classes_have_overlap,
            best_foreground_macro_dice,
            best_loss) if foreground_quality_selection else validation_loss <= best_loss
        if select_best:
            best_loss = validation_loss
            best_foreground_macro_dice = validation_foreground_macro_dice
            best_all_classes_have_overlap = validation_all_classes_have_overlap
            torch.save(checkpoint, best_path)
        metrics.append((epoch, train_loss, validation_loss, validation_foreground_macro_dice, int(validation_all_classes_have_overlap)) if foreground_quality_selection else (epoch, train_loss, validation_loss))
        if progress is not None:
            progress(epoch, epochs, train_loss, validation_loss)
    with (run_root / "results.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(["epoch", "train/loss", "val/loss", "val/foreground_macro_dice", "val/all_classes_have_overlap"] if foreground_quality_selection else ["epoch", "train/loss", "val/loss"])
        writer.writerows(metrics)
    result = {
        "weightsPath": str(best_path.resolve()),
        "lastWeightsPath": str(last_path.resolve()),
        "runPath": str(run_root.resolve()),
        "classes": classes,
        "trainLoss": metrics[-1][1],
        "validationLoss": metrics[-1][2],
    }
    if foreground_quality_selection:
        result["selectionMetric"] = "valid-all-class-overlap-then-foreground-macro-dice-v1"
        result["validationForegroundMacroDice"] = metrics[-1][3]
        result["validationAllClassesHaveOverlap"] = bool(metrics[-1][4])
    return result


class UnetDetector:
    def __init__(self, weights: Path, model_root: Path, image_root: Path, image_size: int, confidence: float, device_text: str):
        self.weights = weights
        self.model_root = model_root
        self.image_root = image_root
        self.image_size = image_size
        self.confidence = confidence
        self.device_text = device_text
        self.model = None
        self.classes: list[str] = []
        self.last_error = ""

    def status(self) -> dict[str, Any]:
        return {
            "engine": "unet",
            "state": "ready" if self.model is not None else "unconfigured" if not self.weights.is_file() else "notLoaded",
            "loaded": self.model is not None,
            "weightsPath": str(self.weights),
            "lastError": self.last_error,
            "classes": self.classes,
        }

    def load(self) -> None:
        _, torch, _, _, _ = torch_dependencies()
        if not self.weights.is_file():
            raise FileNotFoundError(f"U-Net checkpoint was not found: {self.weights}")
        device = torch.device(self.device_text if self.device_text else ("cuda" if torch.cuda.is_available() else "cpu"))
        checkpoint = torch.load(self.weights, map_location=device)
        if checkpoint.get("format") != "openvisionlab-unet-v1":
            raise ValueError("checkpoint is not an OpenVisionLab U-Net v1 checkpoint.")
        classes = checkpoint.get("classes")
        if not isinstance(classes, list) or not classes:
            raise ValueError("checkpoint does not include a valid U-Net class contract.")
        self.classes = [str(value) for value in classes]
        self.image_size = positive_int(checkpoint.get("imageSize"), self.image_size)
        self.model = build_unet(3, len(self.classes) + 1, positive_int(checkpoint.get("baseChannels"), 16)).to(device)
        self.model.load_state_dict(checkpoint["stateDict"])
        self.model.eval()
        self.last_error = ""

    def detect_path(self, image_path: Path, confidence: float | None) -> tuple[list[dict[str, Any]], dict[str, Any]]:
        np, torch, _, functional, Image = torch_dependencies()
        if self.model is None:
            self.load()
        if not image_path.is_file():
            raise FileNotFoundError(f"image was not found: {image_path}")
        image = Image.open(image_path).convert("RGB")
        width, height = image.size
        resized = image.resize((self.image_size, self.image_size), Image.Resampling.BILINEAR)
        image_tensor = torch.from_numpy(np.asarray(resized, dtype=np.float32) / 255.0).permute(2, 0, 1).unsqueeze(0)
        device = next(self.model.parameters()).device
        with torch.no_grad():
            probability = torch.softmax(self.model(image_tensor.to(device)), dim=1)
            probability = functional.interpolate(probability, size=(height, width), mode="bilinear", align_corners=False)[0].cpu().numpy()
        labels = probability.argmax(axis=0)
        threshold = self.confidence if confidence is None else max(0.0, min(1.0, confidence))
        candidates: list[dict[str, Any]] = []
        for class_index, class_name in enumerate(self.classes, start=1):
            for component in connected_components(labels == class_index):
                ys, xs = component
                score = float(probability[class_index, ys, xs].mean())
                if score < threshold:
                    continue
                left, right = int(xs.min()), int(xs.max())
                top, bottom = int(ys.min()), int(ys.max())
                candidates.append({
                    "className": class_name,
                    "confidence": score,
                    "x": float(left),
                    "y": float(top),
                    "width": float(right - left + 1),
                    "height": float(bottom - top + 1),
                    "candidateType": "segmentation",
                    "predictionType": "unet",
                    "segmentationType": "polygon",
                    "polygonPoints": [
                        {"x": float(left), "y": float(top)},
                        {"x": float(right + 1), "y": float(top)},
                        {"x": float(right + 1), "y": float(bottom + 1)},
                        {"x": float(left), "y": float(bottom + 1)},
                    ],
                })
        candidates.sort(key=lambda item: item["confidence"], reverse=True)
        return candidates, {"path": str(image_path.resolve()), "width": width, "height": height}


def connected_components(mask):
    np, _, _, _, _ = torch_dependencies()
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    minimum_area = max(1, (height * width) // 10000)
    for start_y, start_x in zip(*np.where(mask & ~visited)):
        if visited[start_y, start_x]:
            continue
        stack = [(int(start_y), int(start_x))]
        visited[start_y, start_x] = True
        points: list[tuple[int, int]] = []
        while stack:
            y, x = stack.pop()
            points.append((y, x))
            for next_y, next_x in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= next_y < height and 0 <= next_x < width and mask[next_y, next_x] and not visited[next_y, next_x]:
                    visited[next_y, next_x] = True
                    stack.append((next_y, next_x))
        if len(points) >= minimum_area:
            coordinates = np.asarray(points, dtype=np.int32)
            yield coordinates[:, 0], coordinates[:, 1]


class UnetWorker:
    def __init__(self, detector: UnetDetector, debug: bool):
        self.detector = detector
        self.debug = debug
        self.started_at = utc_now()
        self.training_lock = threading.Lock()
        self.training_thread: threading.Thread | None = None
        self.training_status: dict[str, Any] = {"type": "TrainingStatus", "state": "idle", "message": "training is idle"}

    def handle(self, message: IncomingMessage, writer: JsonResponseWriter | None = None) -> dict[str, Any]:
        try:
            if message.message_type == "HealthCheck":
                return self.health(message)
            if message.message_type == "ModelStatus":
                return self.model_status(message)
            if message.message_type == "DetectImage":
                return self.detect(message)
            if message.message_type == "TrainYolo":
                return self.train(message, writer)
            if message.message_type == "StopTask":
                return {"type": "StopTaskResult", "requestId": message.request_id, "ok": True, "state": "idle"}
            return {"type": "Error", "requestId": message.request_id, "ok": False, "error": make_error("UnknownMessageType", message.raw_type or message.message_type)}
        except Exception as exc:
            return {"type": "Error", "requestId": message.request_id, "ok": False, "error": make_error("UnhandledWorkerError", exc, self.debug)}

    def health(self, message: IncomingMessage) -> dict[str, Any]:
        ready = runtime_available()
        result = {
            "type": "HealthCheckResult",
            "requestId": message.request_id,
            "ok": ready,
            "state": "ready" if ready else "error",
            "worker": {"name": "openvisionlab-unet-worker", "pid": os.getpid(), "startedAtUtc": self.started_at, "nowUtc": utc_now()},
            "model": self.detector.status(),
        }
        result.update(capability_payload())
        if not ready:
            result["error"] = make_error("TorchMissing", "torch, numpy, or Pillow is unavailable in the selected Python environment.")
        return result

    def model_status(self, message: IncomingMessage) -> dict[str, Any]:
        if bool(first_value(message.payload, ["load", "ensureLoaded"], False)):
            try:
                self.detector.load()
            except Exception as exc:
                self.detector.last_error = str(exc)
        status = self.detector.status()
        result = {"type": "ModelStatusResult", "requestId": message.request_id, "ok": status["state"] == "ready", "state": status["state"], "model": status, "training": dict(self.training_status)}
        result.update(capability_payload())
        return result

    def detect(self, message: IncomingMessage) -> dict[str, Any]:
        requested = normalize_model(first_value(message.payload, ["model", "adapter"], ""))
        if requested and requested != "unet":
            return {"type": "DetectImageResult", "requestId": message.request_id, "imageId": message.image_id, "ok": False, "candidates": [], "error": make_error("UnsupportedModel", f"U-Net worker cannot run model '{requested}'.")}
        started = time.perf_counter()
        try:
            image_path = resolve_image_path(first_value(message.payload, ["imagePath", "path", "filePath"], ""), self.detector.image_root)
            confidence = first_value(message.payload, ["confidence", "conf", "threshold"], None)
            try:
                confidence = None if confidence is None else float(confidence)
            except (TypeError, ValueError):
                confidence = None
            candidates, image = self.detector.detect_path(image_path, confidence)
            return {"type": "DetectImageResult", "requestId": message.request_id, "imageId": message.image_id or image_path.stem, "ok": True, "elapsedMs": int((time.perf_counter() - started) * 1000), "model": self.detector.status(), "image": image, "candidates": candidates}
        except Exception as exc:
            return {"type": "DetectImageResult", "requestId": message.request_id, "imageId": message.image_id, "ok": False, "candidates": [], "model": self.detector.status(), "error": make_error("DetectImageFailed", exc, self.debug)}

    def train(self, message: IncomingMessage, writer: JsonResponseWriter | None) -> dict[str, Any]:
        requested = normalize_model(first_value(message.payload, ["model", "adapter"], "unet"))
        task = normalize_task(first_value(message.payload, ["task", "trainingTask"], "segment"))
        data_root = resolve_data_root(first_value(message.payload, ["dataYaml", "dataYamlPath", "data"], ""), self.detector.image_root)
        if requested != "unet":
            return self.training_failure(message, "UnsupportedTrainingModel", f"U-Net worker cannot train model '{requested}'.")
        if task != "segment":
            return self.training_failure(message, "UnsupportedTrainingTask", "U-Net worker only supports segmentation training.")
        if not data_root.is_dir():
            return self.training_failure(message, "TrainingDataNotFound", f"U-Net dataset export was not found: {data_root}")
        if writer is None:
            return self.training_failure(message, "TrainingWriterUnavailable", "TrainYolo requires a TCP response writer.")
        if not runtime_available():
            return self.training_failure(message, "TorchMissing", "torch, numpy, or Pillow is unavailable in the selected Python environment.")
        with self.training_lock:
            if self.training_thread is not None and self.training_thread.is_alive():
                return self.training_failure(message, "TrainingAlreadyRunning", "a U-Net training job is already running.", state="running")
            payload = dict(message.payload)
            payload.update({"model": "unet", "task": "segment", "dataYaml": str(data_root)})
            self.training_status = self.training_status_message(message.request_id, "started", "U-Net segmentation training accepted.", 0, 0, positive_int(payload.get("epoch"), 1))
            self.training_thread = threading.Thread(target=self.train_job, args=(message.request_id, payload, writer), daemon=True, name="openvisionlab-unet-training")
            self.training_thread.start()
        return {"type": "TrainYoloResult", "requestId": message.request_id, "ok": True, "state": "started", "taskType": "TrainYolo", "trainingTask": "segment", "model": "unet", "progressPercent": 0}

    def train_job(self, request_id: str, payload: dict[str, Any], writer: JsonResponseWriter) -> None:
        epochs = positive_int(first_value(payload, ["epoch", "epochs"], 1), 1)
        image_size = positive_int(first_value(payload, ["imgSize", "imageSize", "imgsz"], self.detector.image_size), self.detector.image_size)
        batch = positive_int(first_value(payload, ["batch", "batchSize"], 1), 1)
        data_root = resolve_data_root(payload.get("dataYaml"), self.detector.image_root)
        run_name = str(first_value(payload, ["runName", "name"], "") or "").strip() or "openvisionlab-unet-segmentation"

        def report(epoch: int, total: int, train_loss: float, validation_loss: float) -> None:
            progress = int(round((epoch / max(total, 1)) * 100))
            status = self.training_status_message(request_id, "running", f"U-Net epoch {epoch}/{total} (train loss {train_loss:.4f}, val loss {validation_loss:.4f})", progress, epoch, total)
            self.training_status = status
            writer.send(status)

        try:
            self.training_status = self.training_status_message(request_id, "running", "U-Net segmentation training started.", 0, 0, epochs)
            writer.send(self.training_status)
            result = train_dataset(data_root, self.detector.model_root, run_name, epochs, batch, image_size, self.detector.device_text, report)
            self.training_status = self.training_status_message(request_id, "completed", f"U-Net segmentation training completed. {result['runPath']}", 100, epochs, epochs, result["weightsPath"])
            writer.send(self.training_status)
        except Exception as exc:
            self.training_status = self.training_status_message(request_id, "failed", "U-Net segmentation training failed.", None, None, epochs, error=make_error("TrainingFailed", exc, self.debug))
            try:
                writer.send(self.training_status)
            except OSError:
                pass

    def training_failure(self, message: IncomingMessage, code: str, detail: str, state: str = "failed") -> dict[str, Any]:
        error = make_error(code, detail)
        self.training_status = self.training_status_message(message.request_id, state, detail, error=error)
        return {"type": "TrainYoloResult", "requestId": message.request_id, "ok": False, "state": state, "taskType": "TrainYolo", "error": error}

    @staticmethod
    def training_status_message(request_id: str, state: str, message: str, progress: int | None = None, epoch: int | None = None, total: int | None = None, weights: str = "", error: dict[str, Any] | None = None) -> dict[str, Any]:
        result: dict[str, Any] = {"type": "TrainingStatus", "requestId": request_id, "taskType": "TrainYolo", "state": state, "message": message, "trainingTask": "segment", "model": "unet", "updatedAtUtc": utc_now()}
        if progress is not None:
            result["progressPercent"] = max(0, min(100, int(progress)))
        if epoch is not None:
            result["epoch"] = max(0, int(epoch))
        if total is not None:
            result["totalEpochs"] = max(0, int(total))
        if weights:
            result["trainingWeights"] = weights
            result["weightsPath"] = weights
        if error is not None:
            result["error"] = error
        return result


def parse_messages(buffer: bytearray) -> Iterable[IncomingMessage]:
    while True:
        while buffer and buffer[0] in b"\r\n\t ":
            del buffer[0]
        if not buffer:
            return
        if buffer.startswith(b"{"):
            end = buffer.find(b"\n")
            if end < 0:
                return
            payload = bytes(buffer[:end])
            del buffer[:end + 1]
            yield parse_json_message(payload)
            continue
        separator = buffer.find(PACKET_SEPARATOR)
        if separator < 0:
            return
        payload_start = separator + len(PACKET_SEPARATOR)
        payload_end = find_json_end(buffer, payload_start)
        if payload_end < 0:
            return
        command = bytes(buffer[:separator]).decode("ascii", errors="replace").strip()
        payload = bytes(buffer[payload_start:payload_end])
        del buffer[:payload_end]
        yield parse_legacy_message(command, payload)


def parse_json_message(payload_bytes: bytes) -> IncomingMessage:
    try:
        payload = json.loads(payload_bytes.decode("utf-8"))
        if not isinstance(payload, dict):
            raise ValueError("JSON message must be an object.")
        raw_type = str(first_value(payload, ["type", "messageType", "command", "action"], ""))
        if not raw_type:
            raise ValueError("JSON message requires type.")
        return IncomingMessage(LEGACY_TYPE_MAP.get(raw_type, raw_type), str(payload.get("requestId", "")), str(payload.get("imageId", "")), payload, raw_type)
    except Exception as exc:
        return IncomingMessage("InvalidMessage", payload={"error": make_error("InvalidJson", exc)}, raw_type="InvalidMessage")


def parse_legacy_message(command: str, payload_bytes: bytes) -> IncomingMessage:
    try:
        payload = json.loads(payload_bytes.decode("utf-8")) if payload_bytes else {}
        if not isinstance(payload, dict):
            raise ValueError("legacy JSON payload must be an object.")
    except Exception as exc:
        payload = {"_parseError": make_error("InvalidLegacyPayload", exc)}
    return IncomingMessage(LEGACY_TYPE_MAP.get(command, command), str(payload.get("requestId", "")), str(payload.get("imageId", "")), payload, command)


def find_json_end(buffer: bytearray, start: int) -> int:
    depth = 0
    string = False
    escaped = False
    opened = False
    for index in range(start, len(buffer)):
        char = chr(buffer[index])
        if string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                string = False
            continue
        if char.isspace() and not opened:
            continue
        if char == '"':
            string = True
        elif char == "{":
            opened = True
            depth += 1
        elif char == "}":
            depth -= 1
            if opened and depth == 0:
                return index + 1
    return -1


def build_detector(args: argparse.Namespace) -> UnetDetector:
    return UnetDetector(Path(args.weights).expanduser().resolve(), Path(args.model_root).expanduser().resolve(), Path(args.image_root).expanduser().resolve(), args.img_size, args.conf, args.device)


def run_client(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    if args.preload and detector.weights.is_file():
        try:
            detector.load()
            print(compact_json({"type": "ModelStatusResult", "ok": True, "model": detector.status()}).decode("utf-8"), flush=True)
        except Exception as exc:
            print(compact_json({"type": "ModelStatusResult", "ok": False, "model": detector.status(), "error": make_error("ModelLoadFailed", exc)}).decode("utf-8"), flush=True)
    while True:
        try:
            with socket.create_connection((args.host, args.port), timeout=args.timeout) as sock:
                sock.settimeout(args.timeout)
                worker = UnetWorker(detector, args.debug)
                writer = JsonResponseWriter(sock)
                buffer = bytearray()
                handled = 0
                while True:
                    try:
                        chunk = sock.recv(65536)
                    except socket.timeout:
                        continue
                    if not chunk:
                        return 0
                    buffer.extend(chunk)
                    for message in parse_messages(buffer):
                        handled += 1
                        writer.send(worker.handle(message, writer))
                        if args.once and handled >= 1:
                            return 0
        except OSError as exc:
            if not args.retry:
                print(f"connect failed: {exc}", flush=True)
                return 1
            time.sleep(args.retry_delay)


def run_train_smoke(args: argparse.Namespace) -> int:
    try:
        data_root = Path(args.data_root).expanduser().resolve()
        result = train_dataset(
            data_root,
            Path(args.model_root).resolve(),
            args.run_name or "openvisionlab-unet-smoke",
            args.epochs,
            args.batch,
            args.img_size,
            args.device,
            foreground_quality_selection=args.foreground_quality_selection)
        print(compact_json({"type": "UnetTrainSmokeResult", "ok": True, **result}).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        print(compact_json({"type": "UnetTrainSmokeResult", "ok": False, "error": make_error("UnetTrainSmokeFailed", exc, args.debug)}).decode("utf-8"), flush=True)
        return 1


def run_smoke_test(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    try:
        image_path = resolve_image_path(args.detect_file or args.image, detector.image_root)
        candidates, image = detector.detect_path(image_path, args.conf)
        print(compact_json({"type": "SmokeTestResult", "ok": True, "weightsPath": str(detector.weights), "model": detector.status(), "image": image, "candidates": candidates}).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        print(compact_json({"type": "SmokeTestResult", "ok": False, "weightsPath": str(detector.weights), "error": make_error("SmokeTestFailed", exc, args.debug)}).decode("utf-8"), flush=True)
        return 1


def run_self_test() -> int:
    np, torch, _, _, _ = torch_dependencies()
    message = parse_json_message(b'{"type":"HealthCheck","requestId":"health"}')
    assert message.message_type == "HealthCheck" and message.request_id == "health"
    legacy = parse_legacy_message("StartTraining", b'{"model":"unet","task":"segment"}')
    assert legacy.message_type == "TrainYolo"
    model = build_unet(3, 3, 4)
    with torch.no_grad():
        output = model(torch.zeros((1, 3, 32, 32)))
    assert tuple(output.shape) == (1, 3, 32, 32)
    assert list(capability_payload()["trainingModels"]) == ["unet"]
    assert np.zeros((1,), dtype=np.uint8).shape == (1,)
    assert is_better_foreground_candidate(True, 0.1, 2.0, False, 0.9, 1.0)
    assert is_better_foreground_candidate(False, 0.6, 2.0, False, 0.5, 1.0)
    assert not is_better_foreground_candidate(False, 0.4, 0.1, False, 0.5, 1.0)
    print(compact_json({"type": "UnetSelfTestResult", "ok": True, "worker": "openvisionlab-unet-worker"}).decode("utf-8"), flush=True)
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="U-Net TCP worker for OpenVisionLab Labeling Studio.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--weights", default="")
    parser.add_argument("--model-root", default=str(Path(__file__).resolve().parent))
    parser.add_argument("--image-root", default=str(Path.cwd()))
    parser.add_argument("--img-size", type=int, default=320)
    parser.add_argument("--conf", type=float, default=0.25)
    parser.add_argument("--device", default="")
    parser.add_argument("--retry", action="store_true")
    parser.add_argument("--retry-delay", type=float, default=1)
    parser.add_argument("--once", action="store_true")
    parser.add_argument("--preload", action="store_true")
    parser.add_argument("--debug", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--train-smoke", action="store_true")
    parser.add_argument("--data-root", default="")
    parser.add_argument("--epochs", type=int, default=1)
    parser.add_argument("--batch", type=int, default=1)
    parser.add_argument("--run-name", default="")
    parser.add_argument("--foreground-quality-selection", action="store_true")
    parser.add_argument("--smoke-test", action="store_true")
    parser.add_argument("--detect-file", default="")
    parser.add_argument("--image", default="")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()
    if args.train_smoke:
        return run_train_smoke(args)
    if args.smoke_test or args.detect_file or args.image:
        return run_smoke_test(args)
    return run_client(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
