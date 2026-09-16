#!/usr/bin/env python3
"""PatchCore-style one-class anomaly worker for OpenVisionLab Labeling Studio.

The worker learns only from reviewed normal images.  It keeps ImageNet feature
patches in a bounded coreset, scores a new image by nearest-neighbour distance,
and returns both an image-level OK/NG decision and review-only localization
candidates.  It implements the existing HealthCheck/TrainYolo/DetectImage TCP
contract so no annotation is saved automatically.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import socket
import sys
import threading
from urllib.parse import urlparse
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

PACKET_SEPARATOR = b"\n\n"
RUN_ID_MAX_LENGTH = 64
IMAGE_EXTENSIONS = {".bmp", ".jpeg", ".jpg", ".png", ".tif", ".tiff"}
LEGACY_TYPE_MAP = {
    "StartTraining": "TrainYolo",
    "StopTraining": "StopTask",
    "StartDefect": "DetectImage",
    "StopDefect": "StopTask",
}
PATCHCORE_BACKBONE_PATH_ENV = "OPENVISIONLAB_PATCHCORE_BACKBONE_PATH"
PATCHCORE_BACKBONE_SHA256_ENV = "OPENVISIONLAB_PATCHCORE_BACKBONE_SHA256"
PATCHCORE_BACKBONE_HASH_MANIFEST_SUFFIX = ".sha256"


class PatchCoreBackboneError(RuntimeError):
    def __init__(
        self,
        code: str,
        message: str,
        path: Path | None = None,
        expected_sha256: str = "",
        actual_sha256: str = "",
        file_name: str = "",
        hash_source: str = "",
    ):
        super().__init__(message)
        self.code = code
        self.path = str(path.resolve()) if path is not None else ""
        self.expected_sha256 = expected_sha256
        self.actual_sha256 = actual_sha256
        self.file_name = file_name or (path.name if path is not None else "")
        self.hash_source = hash_source

    def details(self) -> dict[str, Any]:
        path = Path(self.path) if self.path else None
        return {
            "offline": True,
            "downloadAttempted": False,
            "fileName": self.file_name,
            "path": self.path,
            "location": str(path.parent) if path is not None else "",
            "expectedSha256": self.expected_sha256,
            "sha256": self.actual_sha256,
            "hashSource": self.hash_source,
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
    if isinstance(error, PatchCoreBackboneError):
        result["causeCode"] = error.code
        result["backbone"] = error.details()
    if include_trace:
        result["trace"] = traceback.format_exc()
    return result


def first_value(payload: dict[str, Any], names: Iterable[str], default: Any = None) -> Any:
    for name in names:
        value = payload.get(name)
        if value is not None and value != "":
            return value
    return default


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


def positive_int(value: Any, default: int) -> int:
    try:
        return max(1, int(float(value)))
    except (TypeError, ValueError):
        return default


def bounded_float(value: Any, default: float, minimum: float, maximum: float) -> float:
    try:
        return max(minimum, min(maximum, float(value)))
    except (TypeError, ValueError):
        return default


def normalize_model(value: Any) -> str:
    return str(value or "").strip().lower().replace("-", "").replace("_", "")


def normalize_task(value: Any) -> str:
    task = str(value or "anomaly").strip().lower()
    return "anomaly" if task in {"anomaly", "anomalydetection", "oneclass"} else task


def dependencies() -> tuple[Any, Any, Any, Any, Any, Any]:
    try:
        import numpy as np
        import torch
        import torch.nn.functional as functional
        from PIL import Image
        from torchvision import transforms
        from torchvision.models import Wide_ResNet50_2_Weights, wide_resnet50_2
        return np, torch, functional, Image, transforms, (Wide_ResNet50_2_Weights, wide_resnet50_2)
    except Exception as exc:
        raise RuntimeError("PatchCore worker requires torch, torchvision, numpy, and Pillow.") from exc


def normalize_sha256(value: Any) -> str:
    text = str(value or "").strip().lower()
    if text.startswith("sha256:"):
        text = text[7:].strip()
    if text and not re.fullmatch(r"[0-9a-f]{8,64}", text):
        raise ValueError("SHA-256 must contain 8 to 64 hexadecimal characters")
    return text


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def backbone_hash_manifest_path(path: Path) -> Path:
    return Path(str(path) + PATCHCORE_BACKBONE_HASH_MANIFEST_SUFFIX)


def read_backbone_hash_manifest(path: Path) -> str:
    manifest = backbone_hash_manifest_path(path)
    if not manifest.is_file():
        return ""
    try:
        match = re.search(r"(?i)(?<![0-9a-f])([0-9a-f]{8,64})(?![0-9a-f])", manifest.read_text(encoding="utf-8"))
        return normalize_sha256(match.group(1)) if match else ""
    except (OSError, UnicodeError, ValueError):
        return ""


def write_backbone_hash_manifest(path: Path, sha256: str) -> Path:
    manifest = backbone_hash_manifest_path(path)
    temporary = Path(str(manifest) + f".tmp-{os.getpid()}")
    temporary.write_text(f"{sha256}  {path.name}\n", encoding="ascii")
    os.replace(temporary, manifest)
    return manifest


def _backbone_url_hash_prefix(url: str) -> str:
    file_name = Path(urlparse(url).path).name
    match = re.search(r"-([0-9a-f]{8,64})(?:\.[^.]+)?$", file_name.lower())
    return match.group(1) if match else ""


def resolve_pretrained_backbone_spec(
    torch: Any,
    weights_type: Any,
    backbone_path: Path | None = None,
    expected_sha256: str = "",
) -> dict[str, Any]:
    try:
        weights = weights_type.IMAGENET1K_V2
        url = str(weights.url)
        file_name = Path(urlparse(url).path).name
        if not file_name:
            raise ValueError("torchvision did not provide a PatchCore backbone filename")
        path_value = str(backbone_path or os.environ.get(PATCHCORE_BACKBONE_PATH_ENV, "")).strip()
        path = Path(path_value).expanduser().resolve() if path_value else Path(torch.hub.get_dir()).expanduser().resolve() / "checkpoints" / file_name
        supplied_hash = normalize_sha256(expected_sha256 or os.environ.get(PATCHCORE_BACKBONE_SHA256_ENV, ""))
        manifest_hash = read_backbone_hash_manifest(path) if not supplied_hash else ""
        url_hash = _backbone_url_hash_prefix(url)
        expected = supplied_hash or manifest_hash or (url_hash if path.name == file_name else "")
        return {
            "weights": weights,
            "url": url,
            "fileName": file_name,
            "path": path,
            "expectedSha256": expected,
            "hashSource": "argument/environment" if supplied_hash else "sidecar" if manifest_hash else "torchvision URL prefix" if url_hash and path.name == file_name else "",
        }
    except PatchCoreBackboneError:
        raise
    except Exception as exc:
        path = Path(backbone_path).expanduser().resolve() if backbone_path is not None else None
        raise PatchCoreBackboneError(
            "PatchCoreBackboneSpecUnavailable",
            f"PatchCore pretrained backbone specification is unavailable: {exc}",
            path=path,
        ) from exc


def inspect_pretrained_backbone_cache(
    path: Path,
    expected_sha256: str = "",
    file_name: str = "",
    hash_source: str = "",
) -> dict[str, Any]:
    resolved_path = Path(path).expanduser().resolve()
    try:
        expected = normalize_sha256(expected_sha256)
    except ValueError as exc:
        raise PatchCoreBackboneError("PatchCoreBackboneHashInvalid", str(exc), path=resolved_path, file_name=file_name, hash_source=hash_source) from exc
    expected_display = expected or "(provide --backbone-sha256 or a .sha256 sidecar)"
    if not resolved_path.is_file():
        raise PatchCoreBackboneError(
            "PatchCoreBackboneMissing",
            f"PatchCore pretrained backbone is missing for offline execution: expected '{file_name or resolved_path.name}' at '{resolved_path}'. Expected SHA-256 '{expected_display}'. Place the file under '{resolved_path.parent}' or run --prepare-backbone --allow-backbone-download after explicit approval.",
            path=resolved_path,
            expected_sha256=expected,
            file_name=file_name or resolved_path.name,
            hash_source=hash_source,
        )
    try:
        actual = sha256_file(resolved_path)
    except OSError as exc:
        raise PatchCoreBackboneError(
            "PatchCoreBackboneUnreadable",
            f"PatchCore pretrained backbone cannot be read offline: '{resolved_path}'. Check the file and location, then verify SHA-256 '{expected_display}'.",
            path=resolved_path,
            expected_sha256=expected,
            file_name=file_name or resolved_path.name,
            hash_source=hash_source,
        ) from exc
    if not expected:
        raise PatchCoreBackboneError(
            "PatchCoreBackboneHashRequired",
            f"PatchCore pretrained backbone was found at '{resolved_path}' but no trusted SHA-256 was supplied. Record the expected hash with --backbone-sha256 or '{backbone_hash_manifest_path(resolved_path)}'; observed '{actual}'.",
            path=resolved_path,
            expected_sha256="",
            actual_sha256=actual,
            file_name=file_name or resolved_path.name,
            hash_source=hash_source,
        )
    if not actual.startswith(expected):
        raise PatchCoreBackboneError(
            "PatchCoreBackboneHashMismatch",
            f"PatchCore pretrained backbone SHA-256 mismatch at '{resolved_path}': expected '{expected}', observed '{actual}'. Do not load or download implicitly; replace it only through the approved preparation action.",
            path=resolved_path,
            expected_sha256=expected,
            actual_sha256=actual,
            file_name=file_name or resolved_path.name,
            hash_source=hash_source,
        )
    return {
        "offline": True,
        "downloadAttempted": False,
        "hashVerified": True,
        "fileName": file_name or resolved_path.name,
        "path": str(resolved_path),
        "location": str(resolved_path.parent),
        "expectedSha256": expected,
        "sha256": actual,
        "hashSource": hash_source,
    }


def require_pretrained_backbone(spec: dict[str, Any]) -> dict[str, Any]:
    return inspect_pretrained_backbone_cache(
        spec["path"],
        spec.get("expectedSha256", ""),
        spec.get("fileName", ""),
        spec.get("hashSource", ""),
    )


def prepare_pretrained_backbone(torch: Any, weights_type: Any, backbone_path: Path | None = None, expected_sha256: str = "") -> dict[str, Any]:
    spec = resolve_pretrained_backbone_spec(torch, weights_type, backbone_path, expected_sha256)
    destination = spec["path"]
    destination.parent.mkdir(parents=True, exist_ok=True)
    try:
        cached = inspect_pretrained_backbone_cache(destination, spec["expectedSha256"], spec["fileName"], spec["hashSource"])
        cached["prepared"] = False
        cached["preparationAction"] = "already-cached"
        return cached
    except PatchCoreBackboneError as exc:
        if exc.code not in {"PatchCoreBackboneMissing", "PatchCoreBackboneHashMismatch", "PatchCoreBackboneHashRequired"}:
            raise
    expected = spec.get("expectedSha256", "")
    if not expected:
        raise PatchCoreBackboneError(
            "PatchCoreBackboneHashRequired",
            f"An expected SHA-256 is required before downloading a custom PatchCore backbone path '{destination}'. Supply --backbone-sha256 with explicit approval.",
            path=destination,
            file_name=spec["fileName"],
            hash_source=spec.get("hashSource", ""),
        )
    temporary = Path(str(destination) + f".download-{os.getpid()}")
    try:
        torch.hub.download_url_to_file(spec["url"], str(temporary), hash_prefix=expected, progress=True)
        if not temporary.is_file():
            raise OSError(f"download did not create '{temporary}'")
        os.replace(temporary, destination)
        verified = inspect_pretrained_backbone_cache(destination, expected, spec["fileName"], spec.get("hashSource", ""))
        manifest = write_backbone_hash_manifest(destination, verified["sha256"])
        verified.update({"prepared": True, "preparationAction": "approved-download", "hashManifestPath": str(manifest.resolve()), "downloadAttempted": True})
        return verified
    except PatchCoreBackboneError:
        raise
    except Exception as exc:
        raise PatchCoreBackboneError(
            "PatchCoreBackbonePreparationFailed",
            f"Approved PatchCore backbone preparation failed for '{destination}': {exc}",
            path=destination,
            expected_sha256=expected,
            file_name=spec["fileName"],
            hash_source=spec.get("hashSource", ""),
        ) from exc
    finally:
        if temporary.exists():
            try:
                temporary.unlink()
            except OSError:
                pass


def runtime_available() -> bool:
    try:
        dependencies()
        return True
    except Exception:
        return False


def capability_payload() -> dict[str, list[str]]:
    return {
        "supportedModels": ["patchcore"],
        "trainingModels": ["patchcore"],
        "detectionModels": ["patchcore"],
        "segmentationModels": [],
        "classificationModels": [],
        "anomalyModels": ["patchcore"],
    }


def enumerate_images(root: Path) -> list[Path]:
    if not root.is_dir():
        return []
    return sorted(path for path in root.rglob("*") if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS)


def resolve_normal_roots(data_root: Path) -> tuple[Path, Path | None]:
    train_candidates = [data_root / "train" / "normal", data_root / "normal", data_root / "train"]
    train_root = next((path for path in train_candidates if enumerate_images(path)), train_candidates[0])
    valid_candidates = [data_root / "val" / "normal", data_root / "valid" / "normal", data_root / "validation" / "normal"]
    valid_root = next((path for path in valid_candidates if enumerate_images(path)), None)
    return train_root, valid_root


class FeatureExtractor:
    def __init__(
        self,
        image_size: int,
        device_text: str,
        pretrained: bool,
        state_dict: dict[str, Any] | None = None,
        backbone_path: Path | None = None,
        backbone_sha256: str = "",
    ):
        _, torch, _, _, transforms, model_api = dependencies()
        weights_type, model_factory = model_api
        self.image_size = max(64, int(image_size))
        self.device = torch.device(device_text if device_text else ("cuda" if torch.cuda.is_available() else "cpu"))
        if pretrained:
            backbone_spec = resolve_pretrained_backbone_spec(torch, weights_type, backbone_path, backbone_sha256)
            backbone = require_pretrained_backbone(backbone_spec)
            try:
                local_state_dict = torch.load(backbone["path"], map_location="cpu")
                if isinstance(local_state_dict, dict) and isinstance(local_state_dict.get("state_dict"), dict):
                    local_state_dict = local_state_dict["state_dict"]
                if not isinstance(local_state_dict, dict):
                    raise ValueError("backbone file did not contain a state-dict mapping")
            except Exception as exc:
                if isinstance(exc, PatchCoreBackboneError):
                    raise
                raise PatchCoreBackboneError(
                    "PatchCoreBackboneLoadFailed",
                    f"PatchCore pretrained backbone passed SHA-256 verification but could not be loaded locally: '{backbone['path']}'. Keep network disabled and replace it only through the approved preparation action.",
                    path=Path(backbone["path"]),
                    expected_sha256=backbone.get("expectedSha256", ""),
                    actual_sha256=backbone.get("sha256", ""),
                    file_name=backbone.get("fileName", ""),
                    hash_source=backbone.get("hashSource", ""),
                ) from exc
            self.model = model_factory(weights=None)
            try:
                self.model.load_state_dict(local_state_dict)
            except Exception as exc:
                raise PatchCoreBackboneError(
                    "PatchCoreBackboneLoadFailed",
                    f"PatchCore pretrained backbone at '{backbone['path']}' has an incompatible state-dict; replace it only through the approved preparation action.",
                    path=Path(backbone["path"]),
                    expected_sha256=backbone.get("expectedSha256", ""),
                    actual_sha256=backbone.get("sha256", ""),
                    file_name=backbone.get("fileName", ""),
                    hash_source=backbone.get("hashSource", ""),
                ) from exc
            self.backbone = backbone
        else:
            self.model = model_factory(weights=None)
        if state_dict is not None:
            self.model.load_state_dict(state_dict)
        self.model.to(self.device).eval()
        for parameter in self.model.parameters():
            parameter.requires_grad_(False)
        self.activations: dict[str, Any] = {}
        self.model.layer2.register_forward_hook(lambda _module, _inputs, output: self.activations.__setitem__("layer2", output))
        self.model.layer3.register_forward_hook(lambda _module, _inputs, output: self.activations.__setitem__("layer3", output))
        self.transform = transforms.Compose([
            transforms.Resize((self.image_size, self.image_size)),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
        ])

    def extract(self, image: Any) -> tuple[Any, tuple[int, int]]:
        _, torch, functional, _, _, _ = dependencies()
        self.activations.clear()
        tensor = self.transform(image).unsqueeze(0).to(self.device)
        with torch.no_grad():
            _ = self.model(tensor)
            layer2 = functional.avg_pool2d(self.activations["layer2"], kernel_size=3, stride=1, padding=1)
            layer3 = functional.avg_pool2d(self.activations["layer3"], kernel_size=3, stride=1, padding=1)
            layer3 = functional.interpolate(layer3, size=layer2.shape[-2:], mode="bilinear", align_corners=False)
            embedding = torch.cat([layer2, layer3], dim=1)
            grid = (int(embedding.shape[-2]), int(embedding.shape[-1]))
            patches = embedding.permute(0, 2, 3, 1).reshape(-1, embedding.shape[1])
            patches = functional.normalize(patches, p=2, dim=1)
        return patches.detach(), grid


def nearest_distances(patches: Any, memory_bank: Any, chunk_size: int = 2048) -> Any:
    _, torch, _, _, _, _ = dependencies()
    values = []
    for start in range(0, int(patches.shape[0]), chunk_size):
        distances = torch.cdist(patches[start:start + chunk_size], memory_bank)
        values.append(distances.min(dim=1).values)
    return torch.cat(values)


def build_coreset(features: Any, ratio: float, maximum: int, seed: int) -> Any:
    _, torch, functional, _, _, _ = dependencies()
    count = int(features.shape[0])
    minimum = 2 if count > 1 else 1
    target = max(minimum, min(maximum, count, int(round(count * ratio))))
    if target >= count:
        return features.detach().cpu()
    generator = torch.Generator(device="cpu").manual_seed(seed)
    projection = torch.randn((int(features.shape[1]), min(128, int(features.shape[1]))), generator=generator)
    projected = functional.normalize(features.detach().cpu() @ projection, p=2, dim=1)
    selected = [int(torch.linalg.vector_norm(projected, dim=1).argmax())]
    minimum = torch.cdist(projected, projected[selected]).squeeze(1)
    for _ in range(1, target):
        index = int(minimum.argmax())
        selected.append(index)
        distance = torch.cdist(projected, projected[index:index + 1]).squeeze(1)
        minimum = torch.minimum(minimum, distance)
    return features.detach().cpu()[selected]


def quantile(values: list[float], probability: float) -> float:
    if not values:
        raise ValueError("at least one normal calibration score is required")
    ordered = sorted(float(value) for value in values)
    position = max(0.0, min(1.0, probability)) * (len(ordered) - 1)
    low = int(position)
    high = min(len(ordered) - 1, low + 1)
    fraction = position - low
    return ordered[low] * (1.0 - fraction) + ordered[high] * fraction


def train_patchcore(
    data_root: Path,
    model_root: Path,
    run_name: str,
    image_size: int,
    device_text: str,
    coreset_ratio: float,
    max_coreset: int,
    threshold_quantile: float,
    seed: int,
    backbone_path: str = "",
    backbone_sha256: str = "",
) -> dict[str, Any]:
    _, torch, _, Image, _, _ = dependencies()
    train_root, valid_root = resolve_normal_roots(data_root)
    train_images = enumerate_images(train_root)
    valid_images = enumerate_images(valid_root) if valid_root is not None else []
    if len(train_images) < 2:
        raise ValueError(f"PatchCore needs at least two reviewed normal train images: {train_root}")
    extractor = FeatureExtractor(
        image_size,
        device_text,
        pretrained=True,
        backbone_path=Path(backbone_path).expanduser().resolve() if str(backbone_path).strip() else None,
        backbone_sha256=backbone_sha256,
    )
    feature_batches = []
    grid_size = (0, 0)
    for image_path in train_images:
        with Image.open(image_path) as source:
            features, grid_size = extractor.extract(source.convert("RGB"))
        feature_batches.append(features.cpu())
    all_features = torch.cat(feature_batches, dim=0)
    memory_bank = build_coreset(all_features, coreset_ratio, max_coreset, seed)
    device_memory = memory_bank.to(extractor.device)
    calibration_images = valid_images if valid_images else train_images
    calibration_scores: list[float] = []
    for image_path in calibration_images:
        with Image.open(image_path) as source:
            patches, _ = extractor.extract(source.convert("RGB"))
        distances = nearest_distances(patches, device_memory)
        if not valid_images and float(distances.max().item()) <= 1e-8 and int(device_memory.shape[0]) > 1:
            distances = torch.cdist(patches, device_memory).topk(k=2, largest=False, dim=1).values[:, 1]
        calibration_scores.append(float(distances.max().item()))
    threshold = max(1e-6, quantile(calibration_scores, threshold_quantile) * 1.05)
    run_path = model_root / "runs" / "anomaly" / run_name
    weights_path = run_path / "weights" / "best.pt"
    weights_path.parent.mkdir(parents=True, exist_ok=True)
    checkpoint = {
        "format": "openvisionlab-patchcore-v1",
        "backbone": "wide_resnet50_2_imagenet1k_v2",
        "pretrainedBackbonePath": extractor.backbone["path"],
        "pretrainedBackboneSha256": extractor.backbone["sha256"],
        "backboneStateDict": {key: value.detach().cpu() for key, value in extractor.model.state_dict().items()},
        "memoryBank": memory_bank,
        "threshold": threshold,
        "thresholdQuantile": threshold_quantile,
        "calibrationScores": calibration_scores,
        "imageSize": image_size,
        "gridSize": list(grid_size),
        "coresetRatio": coreset_ratio,
        "trainNormalCount": len(train_images),
        "calibrationNormalCount": len(calibration_images),
        "usedIndependentCalibration": bool(valid_images),
        "createdAtUtc": utc_now(),
    }
    torch.save(checkpoint, weights_path)
    profile_path = run_path / "patchcore-profile.json"
    profile_path.write_text(json.dumps({key: value for key, value in checkpoint.items() if key not in {"backboneStateDict", "memoryBank", "calibrationScores"}}, ensure_ascii=False, indent=2), encoding="utf-8")
    return {
        "runPath": str(run_path.resolve()),
        "weightsPath": str(weights_path.resolve()),
        "profilePath": str(profile_path.resolve()),
        "trainNormalCount": len(train_images),
        "calibrationNormalCount": len(calibration_images),
        "usedIndependentCalibration": bool(valid_images),
        "memoryBankSize": int(memory_bank.shape[0]),
        "featureDimension": int(memory_bank.shape[1]),
        "threshold": threshold,
    }


def connected_components(mask: Any, minimum_area: int) -> list[tuple[Any, Any]]:
    np, _, _, _, _, _ = dependencies()
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    result = []
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
            result.append((coordinates[:, 0], coordinates[:, 1]))
    return result


class PatchCoreDetector:
    def __init__(
        self,
        weights: Path,
        model_root: Path,
        image_root: Path,
        image_size: int,
        device_text: str,
        maximum_candidates: int,
        backbone_path: Path | None = None,
        backbone_sha256: str = "",
    ):
        self.weights = weights
        self.model_root = model_root
        self.image_root = image_root
        self.image_size = image_size
        self.device_text = device_text
        self.maximum_candidates = max(1, maximum_candidates)
        self.backbone_path = backbone_path
        self.backbone_sha256 = backbone_sha256
        self.extractor: FeatureExtractor | None = None
        self.memory_bank = None
        self.threshold = 0.0
        self.metadata: dict[str, Any] = {}
        self.last_error = ""

    def status(self) -> dict[str, Any]:
        return {
            "engine": "patchcore",
            "state": "ready" if self.extractor is not None else "unconfigured" if not self.weights.is_file() else "notLoaded",
            "loaded": self.extractor is not None,
            "weightsPath": str(self.weights),
            "backbonePath": str(self.backbone_path) if self.backbone_path is not None else "",
            "backboneSha256": self.backbone_sha256,
            "threshold": self.threshold,
            "trainNormalCount": int(self.metadata.get("trainNormalCount", 0)),
            "calibrationNormalCount": int(self.metadata.get("calibrationNormalCount", 0)),
            "lastError": self.last_error,
        }

    def load(self) -> None:
        _, torch, _, _, _, _ = dependencies()
        if not self.weights.is_file():
            raise FileNotFoundError(f"PatchCore checkpoint was not found: {self.weights}")
        checkpoint = torch.load(self.weights, map_location="cpu")
        if checkpoint.get("format") != "openvisionlab-patchcore-v1":
            raise ValueError("checkpoint is not an OpenVisionLab PatchCore v1 checkpoint")
        memory_bank = checkpoint.get("memoryBank")
        state_dict = checkpoint.get("backboneStateDict")
        if memory_bank is None or state_dict is None or float(checkpoint.get("threshold", 0.0)) <= 0:
            raise ValueError("checkpoint is missing the memory bank, backbone, or threshold contract")
        self.image_size = positive_int(checkpoint.get("imageSize"), self.image_size)
        self.extractor = FeatureExtractor(self.image_size, self.device_text, pretrained=False, state_dict=state_dict)
        self.memory_bank = memory_bank.to(self.extractor.device)
        self.threshold = float(checkpoint["threshold"])
        self.metadata = checkpoint
        self.last_error = ""

    def detect_path(self, image_path: Path, requested_threshold: float | None = None, heatmap_output: Path | None = None) -> tuple[list[dict[str, Any]], dict[str, Any]]:
        np, torch, functional, Image, _, _ = dependencies()
        if self.extractor is None:
            self.load()
        if not image_path.is_file():
            raise FileNotFoundError(f"image was not found: {image_path}")
        with Image.open(image_path) as source:
            image = source.convert("RGB")
            width, height = image.size
            patches, grid = self.extractor.extract(image)
            distances = nearest_distances(patches, self.memory_bank)
            anomaly_map = distances.reshape(1, 1, grid[0], grid[1])
            anomaly_map = functional.interpolate(anomaly_map, size=(height, width), mode="bilinear", align_corners=False)[0, 0].detach().cpu().numpy()
            score = float(anomaly_map.max())
            threshold = self.threshold if requested_threshold is None or requested_threshold <= 0 else requested_threshold
            is_anomalous = score > threshold
            decision_confidence = max(0.5, min(1.0, 0.5 + abs(score - threshold) / max(2.0 * threshold, 1e-6)))
            normalized_map = np.clip(anomaly_map / max(threshold, 1e-6), 0.0, 2.0) / 2.0
            if heatmap_output is None:
                heatmap_output = self.weights.parent.parent / "heatmaps" / f"{image_path.stem}-patchcore.png"
            heatmap_output.parent.mkdir(parents=True, exist_ok=True)
            image_array = np.asarray(image, dtype=np.float32)
            color = np.zeros_like(image_array)
            color[:, :, 0] = normalized_map * 255.0
            color[:, :, 1] = np.clip(1.0 - np.abs(normalized_map - 0.5) * 2.0, 0.0, 1.0) * 160.0
            color[:, :, 2] = (1.0 - normalized_map) * 96.0
            overlay = np.clip(image_array * 0.55 + color * 0.45, 0, 255).astype(np.uint8)
            Image.fromarray(overlay, mode="RGB").save(heatmap_output)
        candidates: list[dict[str, Any]] = []
        if is_anomalous:
            region_mask = anomaly_map >= threshold
            minimum_area = max(1, (width * height) // 5000)
            regions = connected_components(region_mask, minimum_area)
            if not regions:
                peak_y, peak_x = np.unravel_index(int(anomaly_map.argmax()), anomaly_map.shape)
                radius = max(2, min(width, height) // 50)
                ys = np.asarray([max(0, peak_y - radius), min(height - 1, peak_y + radius)])
                xs = np.asarray([max(0, peak_x - radius), min(width - 1, peak_x + radius)])
                regions = [(ys, xs)]
            for ys, xs in regions:
                left, right = int(xs.min()), int(xs.max())
                top, bottom = int(ys.min()), int(ys.max())
                region_score = float(anomaly_map[ys, xs].max()) if len(xs) > 2 else score
                candidates.append({
                    "className": "abnormal",
                    "confidence": decision_confidence,
                    "anomalyScore": region_score,
                    "anomalyThreshold": threshold,
                    "heatmapPath": str(heatmap_output.resolve()),
                    "x": float(left), "y": float(top),
                    "width": float(max(1, right - left + 1)), "height": float(max(1, bottom - top + 1)),
                    "candidateType": "anomalyLocalization",
                    "predictionType": "patchcore",
                    "imageLevel": True,
                    "segmentationType": "polygon",
                    "polygonPoints": [
                        {"x": float(left), "y": float(top)}, {"x": float(right + 1), "y": float(top)},
                        {"x": float(right + 1), "y": float(bottom + 1)}, {"x": float(left), "y": float(bottom + 1)},
                    ],
                })
            candidates.sort(key=lambda item: item["anomalyScore"], reverse=True)
            candidates = candidates[:self.maximum_candidates]
        else:
            candidates.append({
                "className": "normal", "confidence": decision_confidence,
                "anomalyScore": score, "anomalyThreshold": threshold,
                "heatmapPath": str(heatmap_output.resolve()),
                "x": 0.0, "y": 0.0, "width": 0.0, "height": 0.0,
                "candidateType": "imageClassification", "predictionType": "patchcore", "imageLevel": True,
            })
        return candidates, {
            "path": str(image_path.resolve()), "width": width, "height": height,
            "anomalyScore": score, "anomalyThreshold": threshold, "isAnomalous": is_anomalous,
            "heatmapPath": str(heatmap_output.resolve()),
        }


class JsonResponseWriter:
    def __init__(self, sock: socket.socket):
        self.sock = sock
        self.lock = threading.Lock()

    def send(self, envelope: dict[str, Any]) -> None:
        with self.lock:
            self.sock.sendall(compact_json(envelope) + b"\n")


class PatchCoreWorker:
    def __init__(self, detector: PatchCoreDetector, debug: bool):
        self.detector = detector
        self.debug = debug
        self.started_at = utc_now()
        self.training_lock = threading.Lock()
        self.training_thread: threading.Thread | None = None
        self.training_status: dict[str, Any] = {"type": "TrainingStatus", "state": "idle", "message": "training is idle"}

    def handle(self, message: IncomingMessage, writer: JsonResponseWriter | None = None) -> dict[str, Any]:
        try:
            if message.message_type == "HealthCheck":
                result = {"type": "HealthCheckResult", "requestId": message.request_id, "ok": runtime_available(), "state": "ready" if runtime_available() else "error", "worker": {"name": "openvisionlab-patchcore-worker", "pid": os.getpid(), "startedAtUtc": self.started_at}, "model": self.detector.status()}
                result.update(capability_payload())
                return result
            if message.message_type == "ModelStatus":
                if bool(first_value(message.payload, ["load", "ensureLoaded"], False)):
                    self.detector.load()
                result = {"type": "ModelStatusResult", "requestId": message.request_id, "ok": self.detector.status()["state"] == "ready", "model": self.detector.status(), "training": dict(self.training_status)}
                result.update(capability_payload())
                return result
            if message.message_type == "DetectImage":
                return self.detect(message)
            if message.message_type == "TrainYolo":
                return self.train(message, writer)
            if message.message_type == "StopTask":
                with self.training_lock:
                    running = self.training_thread is not None and self.training_thread.is_alive()
                # The host confirms termination and owns the bounded process-stop fallback.
                return {"type": "StopTaskResult", "requestId": message.request_id, "ok": not running,
                        "state": "stopping" if running else "stopped", "taskType": "TrainYolo",
                        "error": "Training is still running; host process termination is required." if running else ""}
            return {"type": "Error", "requestId": message.request_id, "ok": False, "error": make_error("UnknownMessageType", message.raw_type or message.message_type)}
        except Exception as exc:
            return {"type": "Error", "requestId": message.request_id, "ok": False, "error": make_error("UnhandledWorkerError", exc, self.debug)}

    def detect(self, message: IncomingMessage) -> dict[str, Any]:
        requested = normalize_model(first_value(message.payload, ["model", "adapter"], "patchcore"))
        if requested != "patchcore":
            return {"type": "DetectImageResult", "requestId": message.request_id, "imageId": message.image_id, "ok": False, "candidates": [], "error": make_error("UnsupportedModel", f"PatchCore worker cannot run model '{requested}'.")}
        started = time.perf_counter()
        try:
            value = first_value(message.payload, ["imagePath", "path", "filePath"], "")
            image_path = Path(str(value)).expanduser()
            if not image_path.is_absolute():
                image_path = self.detector.image_root / image_path
            threshold_value = first_value(message.payload, ["anomalyThreshold", "threshold"], None)
            threshold = None if threshold_value is None else float(threshold_value)
            candidates, image = self.detector.detect_path(image_path.resolve(), threshold)
            return {"type": "DetectImageResult", "requestId": message.request_id, "imageId": message.image_id or image_path.stem, "ok": True, "elapsedMs": int((time.perf_counter() - started) * 1000), "model": self.detector.status(), "image": image, "candidates": candidates}
        except Exception as exc:
            return {"type": "DetectImageResult", "requestId": message.request_id, "imageId": message.image_id, "ok": False, "candidates": [], "model": self.detector.status(), "error": make_error("DetectImageFailed", exc, self.debug)}

    def train(self, message: IncomingMessage, writer: JsonResponseWriter | None) -> dict[str, Any]:
        requested = normalize_model(first_value(message.payload, ["model", "adapter"], "patchcore"))
        task = normalize_task(first_value(message.payload, ["task", "trainingTask"], "anomaly"))
        data_root = Path(str(first_value(message.payload, ["dataYaml", "dataYamlPath", "data"], ""))).expanduser().resolve()
        if requested != "patchcore" or task != "anomaly":
            return self.training_failure(message, "UnsupportedTrainingContract", "PatchCore supports only model=patchcore and task=anomaly.")
        if not data_root.is_dir():
            return self.training_failure(message, "TrainingDataNotFound", f"PatchCore dataset export was not found: {data_root}")
        if writer is None:
            return self.training_failure(message, "TrainingWriterUnavailable", "TrainYolo requires a TCP response writer.")
        try:
            run_id = normalize_run_id(first_value(message.payload, ["runId", "trainingRunId"], ""))
        except ValueError as exc:
            return self.training_failure(message, "InvalidTrainingRunId", str(exc))
        with self.training_lock:
            if self.training_thread is not None and self.training_thread.is_alive():
                return self.training_failure(message, "TrainingAlreadyRunning", "a PatchCore training job is already running.")
            payload = dict(message.payload)
            payload["dataYaml"] = str(data_root)
            payload["runId"] = run_id
            self.training_thread = threading.Thread(target=self.train_job, args=(message.request_id, payload, writer), daemon=True, name="openvisionlab-patchcore-training")
            self.training_thread.start()
        result = {"type": "TrainYoloResult", "requestId": message.request_id, "ok": True, "state": "started", "taskType": "TrainYolo", "trainingTask": "anomaly", "model": "patchcore", "progressPercent": 0}
        if run_id:
            result["runId"] = run_id
        return result

    def train_job(self, request_id: str, payload: dict[str, Any], writer: JsonResponseWriter) -> None:
        try:
            run_id = normalize_run_id(payload.get("runId"))
            display_run_name = str(first_value(payload, ["runName", "name"], "") or "").strip() or "openvisionlab-patchcore"
            run_name = compose_training_run_name(display_run_name, run_id, "openvisionlab-patchcore")
            self.training_status = self.training_status_message(request_id, "running", "PatchCore normal-only memory-bank training started.", 10)
            if run_id:
                self.training_status["runId"] = run_id
                self.training_status["runName"] = display_run_name
                self.training_status["outputRunName"] = run_name
            writer.send(self.training_status)
            result = train_patchcore(
                Path(payload["dataYaml"]), self.detector.model_root,
                run_name,
                positive_int(first_value(payload, ["imgSize", "imageSize", "imgsz"], self.detector.image_size), self.detector.image_size),
                self.detector.device_text,
                bounded_float(payload.get("coresetRatio"), 0.01, 0.001, 1.0),
                positive_int(payload.get("maxCoreset"), 10000),
                bounded_float(payload.get("thresholdQuantile"), 0.99, 0.5, 1.0),
                positive_int(payload.get("seed"), 17),
                str(first_value(payload, ["backbonePath", "pretrainedBackbonePath"], self.detector.backbone_path or "")),
                str(first_value(payload, ["backboneSha256", "pretrainedBackboneSha256"], self.detector.backbone_sha256)),
            )
            self.training_status = self.training_status_message(request_id, "completed", f"PatchCore training completed. {result['runPath']}", 100, result["weightsPath"])
            if run_id:
                self.training_status["runId"] = run_id
                self.training_status["runName"] = display_run_name
                self.training_status["outputRunName"] = run_name
            writer.send(self.training_status)
        except Exception as exc:
            self.training_status = self.training_status_message(request_id, "failed", "PatchCore training failed.", error=make_error("TrainingFailed", exc, self.debug))
            try:
                writer.send(self.training_status)
            except OSError:
                pass

    def training_failure(self, message: IncomingMessage, code: str, detail: str) -> dict[str, Any]:
        error = make_error(code, detail)
        self.training_status = self.training_status_message(message.request_id, "failed", detail, error=error)
        return {"type": "TrainYoloResult", "requestId": message.request_id, "ok": False, "state": "failed", "taskType": "TrainYolo", "error": error}

    @staticmethod
    def training_status_message(request_id: str, state: str, message: str, progress: int | None = None, weights: str = "", error: dict[str, Any] | None = None) -> dict[str, Any]:
        result: dict[str, Any] = {"type": "TrainingStatus", "requestId": request_id, "taskType": "TrainYolo", "state": state, "message": message, "trainingTask": "anomaly", "model": "patchcore", "updatedAtUtc": utc_now()}
        if progress is not None:
            result["progressPercent"] = max(0, min(100, int(progress)))
        if weights:
            result["trainingWeights"] = weights
            result["weightsPath"] = weights
        if error is not None:
            result["error"] = error
        return result


def parse_json_message(payload_bytes: bytes) -> IncomingMessage:
    try:
        payload = json.loads(payload_bytes.decode("utf-8"))
        if not isinstance(payload, dict):
            raise ValueError("JSON message must be an object")
        raw_type = str(first_value(payload, ["type", "messageType", "command", "action"], ""))
        if not raw_type:
            raise ValueError("JSON message requires type")
        return IncomingMessage(LEGACY_TYPE_MAP.get(raw_type, raw_type), str(payload.get("requestId", "")), str(payload.get("imageId", "")), payload, raw_type)
    except Exception as exc:
        return IncomingMessage("InvalidMessage", payload={"error": make_error("InvalidJson", exc)}, raw_type="InvalidMessage")


def find_json_end(buffer: bytearray, start: int) -> int:
    depth, string, escaped, opened = 0, False, False, False
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
            opened, depth = True, depth + 1
        elif char == "}":
            depth -= 1
            if opened and depth == 0:
                return index + 1
    return -1


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
        payload_bytes = bytes(buffer[payload_start:payload_end])
        del buffer[:payload_end]
        try:
            payload = json.loads(payload_bytes.decode("utf-8")) if payload_bytes else {}
        except Exception as exc:
            payload = {"_parseError": make_error("InvalidLegacyPayload", exc)}
        yield IncomingMessage(LEGACY_TYPE_MAP.get(command, command), str(payload.get("requestId", "")), str(payload.get("imageId", "")), payload, command)


def build_detector(args: argparse.Namespace) -> PatchCoreDetector:
    backbone_path = str(args.backbone_path).strip()
    return PatchCoreDetector(
        Path(args.weights).expanduser().resolve(),
        Path(args.model_root).expanduser().resolve(),
        Path(args.image_root).expanduser().resolve(),
        args.img_size,
        args.device,
        args.max_candidates,
        Path(backbone_path).expanduser().resolve() if backbone_path else None,
        args.backbone_sha256,
    )


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
                worker, writer, buffer = PatchCoreWorker(detector, args.debug), JsonResponseWriter(sock), bytearray()
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


def run_backbone_preflight(args: argparse.Namespace) -> int:
    try:
        explicit_path = str(args.backbone_path).strip()
        if explicit_path:
            path = Path(explicit_path).expanduser().resolve()
            supplied_hash = normalize_sha256(args.backbone_sha256 or os.environ.get(PATCHCORE_BACKBONE_SHA256_ENV, ""))
            manifest_hash = read_backbone_hash_manifest(path) if not supplied_hash else ""
            result = inspect_pretrained_backbone_cache(
                path,
                supplied_hash or manifest_hash,
                path.name,
                "argument/environment" if supplied_hash else "sidecar" if manifest_hash else "",
            )
        else:
            _, torch, _, _, _, model_api = dependencies()
            weights_type, _ = model_api
            result = require_pretrained_backbone(resolve_pretrained_backbone_spec(torch, weights_type, expected_sha256=args.backbone_sha256))
        print(compact_json({"type": "PatchCoreBackbonePreflightResult", "ok": True, "backbone": result}).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        code = exc.code if isinstance(exc, PatchCoreBackboneError) else "PatchCoreBackbonePreflightFailed"
        print(compact_json({"type": "PatchCoreBackbonePreflightResult", "ok": False, "error": make_error(code, exc, args.debug)}).decode("utf-8"), flush=True)
        return 1


def run_prepare_backbone(args: argparse.Namespace) -> int:
    if not args.allow_backbone_download:
        path = Path(args.backbone_path).expanduser().resolve() if str(args.backbone_path).strip() else None
        try:
            expected = normalize_sha256(args.backbone_sha256 or os.environ.get(PATCHCORE_BACKBONE_SHA256_ENV, ""))
        except ValueError:
            expected = ""
        error = PatchCoreBackboneError(
            "PatchCoreBackboneDownloadApprovalRequired",
            "PatchCore backbone preparation is disabled until the operator explicitly supplies --allow-backbone-download; no network or file download was attempted.",
            path=path,
            expected_sha256=expected,
            file_name=path.name if path is not None else "wide_resnet50_2-<torchvision-hash>.pth",
            hash_source="argument/environment" if expected else "",
        )
        print(compact_json({"type": "PatchCoreBackbonePreparationResult", "ok": False, "error": make_error(error.code, error)}).decode("utf-8"), flush=True)
        return 1
    try:
        _, torch, _, _, _, model_api = dependencies()
        weights_type, _ = model_api
        result = prepare_pretrained_backbone(torch, weights_type, Path(args.backbone_path).expanduser().resolve() if str(args.backbone_path).strip() else None, args.backbone_sha256)
        print(compact_json({"type": "PatchCoreBackbonePreparationResult", "ok": True, "backbone": result}).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        code = exc.code if isinstance(exc, PatchCoreBackboneError) else "PatchCoreBackbonePreparationFailed"
        print(compact_json({"type": "PatchCoreBackbonePreparationResult", "ok": False, "error": make_error(code, exc, args.debug)}).decode("utf-8"), flush=True)
        return 1


def run_train_smoke(args: argparse.Namespace) -> int:
    try:
        result = train_patchcore(
            Path(args.data_root).resolve(),
            Path(args.model_root).resolve(),
            args.run_name or "openvisionlab-patchcore-smoke",
            args.img_size,
            args.device,
            args.coreset_ratio,
            args.max_coreset,
            args.threshold_quantile,
            args.seed,
            args.backbone_path,
            args.backbone_sha256,
        )
        print(compact_json({"type": "PatchCoreTrainSmokeResult", "ok": True, **result}).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        print(compact_json({"type": "PatchCoreTrainSmokeResult", "ok": False, "error": make_error("PatchCoreTrainSmokeFailed", exc, args.debug)}).decode("utf-8"), flush=True)
        return 1


def run_smoke_test(args: argparse.Namespace) -> int:
    detector = build_detector(args)
    try:
        image_path = Path(args.detect_file or args.image).expanduser().resolve()
        heatmap = Path(args.heatmap_output).expanduser().resolve() if args.heatmap_output else None
        candidates, image = detector.detect_path(image_path, heatmap_output=heatmap)
        print(compact_json({"type": "SmokeTestResult", "ok": True, "model": detector.status(), "image": image, "candidates": candidates}).decode("utf-8"), flush=True)
        return 0
    except Exception as exc:
        print(compact_json({"type": "SmokeTestResult", "ok": False, "error": make_error("SmokeTestFailed", exc, args.debug)}).decode("utf-8"), flush=True)
        return 1


def run_self_test(args: argparse.Namespace) -> int:
    np, torch, _, Image, _, _ = dependencies()
    message = parse_json_message(b'{"type":"HealthCheck","requestId":"health"}')
    assert message.message_type == "HealthCheck" and message.request_id == "health"
    assert capability_payload()["anomalyModels"] == ["patchcore"]
    assert abs(quantile([1.0, 2.0, 3.0], 0.5) - 2.0) < 1e-9
    mask = np.zeros((8, 8), dtype=bool)
    mask[2:5, 3:6] = True
    assert len(connected_components(mask, 2)) == 1
    extractor = FeatureExtractor(64, args.device, pretrained=False)
    patches, grid = extractor.extract(Image.new("RGB", (80, 70), "gray"))
    assert patches.shape[0] == grid[0] * grid[1] and torch.isfinite(patches).all()
    print(compact_json({"type": "PatchCoreSelfTestResult", "ok": True, "worker": "openvisionlab-patchcore-worker", "device": str(extractor.device), "featureDimension": int(patches.shape[1])}).decode("utf-8"), flush=True)
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="PatchCore one-class anomaly worker for OpenVisionLab Labeling Studio.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--weights", default="")
    parser.add_argument("--model", default="patchcore")
    parser.add_argument("--model-root", default=str(Path(__file__).resolve().parent))
    parser.add_argument("--image-root", default=str(Path.cwd()))
    parser.add_argument("--img-size", type=int, default=224)
    parser.add_argument("--device", default="")
    parser.add_argument("--max-candidates", type=int, default=20)
    parser.add_argument("--conf", type=float, default=0.25)
    parser.add_argument("--preload", action="store_true")
    parser.add_argument("--retry", action="store_true")
    parser.add_argument("--retry-delay", type=float, default=1)
    parser.add_argument("--once", action="store_true")
    parser.add_argument("--debug", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--train-smoke", action="store_true")
    parser.add_argument("--data-root", default="")
    parser.add_argument("--run-name", default="")
    parser.add_argument("--coreset-ratio", type=float, default=0.01)
    parser.add_argument("--max-coreset", type=int, default=10000)
    parser.add_argument("--threshold-quantile", type=float, default=0.99)
    parser.add_argument("--seed", type=int, default=17)
    parser.add_argument("--backbone-path", default="")
    parser.add_argument("--backbone-sha256", default="")
    parser.add_argument("--preflight-backbone", action="store_true")
    parser.add_argument("--prepare-backbone", action="store_true")
    parser.add_argument("--allow-backbone-download", action="store_true")
    parser.add_argument("--smoke-test", action="store_true")
    parser.add_argument("--detect-file", default="")
    parser.add_argument("--image", default="")
    parser.add_argument("--heatmap-output", default="")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.preflight_backbone:
        return run_backbone_preflight(args)
    if args.prepare_backbone:
        return run_prepare_backbone(args)
    if args.self_test:
        return run_self_test(args)
    if args.train_smoke:
        return run_train_smoke(args)
    if args.smoke_test or args.detect_file or args.image:
        return run_smoke_test(args)
    return run_client(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
