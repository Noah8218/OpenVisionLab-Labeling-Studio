"""Deterministic calibration / evaluation split for benchmark inputs.

Calibration images are never used for the accuracy columns (design M2 data rule).
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".bmp"}


def list_images(images_dir: str | Path) -> list[Path]:
    return sorted(p for p in Path(images_dir).iterdir() if p.suffix.lower() in IMAGE_SUFFIXES)


def split_images(images_dir: str | Path, calibration_count: int) -> tuple[list[Path], list[Path]]:
    images = list_images(images_dir)
    if calibration_count <= 0 or calibration_count >= len(images):
        raise ValueError(f"calibration_count must be in 1..{len(images) - 1}")
    return images[:calibration_count], images[calibration_count:]


def read_list(images_dir: str | Path, list_file: str | Path) -> list[Path]:
    """Image paths named in a split file (one name or relative path per line)."""
    names = [line.strip() for line in Path(list_file).read_text(encoding="utf-8").splitlines() if line.strip()]
    paths = [Path(images_dir) / Path(name).name for name in names]
    missing = [p for p in paths if not p.exists()]
    if missing:
        raise FileNotFoundError(f"{len(missing)} listed images are missing, first: {missing[0]}")
    return paths


def evenly(paths: list[Path], count: int) -> list[Path]:
    """Deterministic, evenly spaced subset so ordered lists (e.g. all NG first) stay mixed."""
    if count >= len(paths):
        return list(paths)
    step = len(paths) / count
    return [paths[int(i * step)] for i in range(count)]


def list_digest(paths: list[Path]) -> str:
    payload = json.dumps([p.name for p in paths]).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()
