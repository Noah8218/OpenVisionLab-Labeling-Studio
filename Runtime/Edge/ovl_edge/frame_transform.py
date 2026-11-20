"""Deterministic image changes that imitate line drift (tests and drift studies only).

Spec string, comma separated, applied in this order:
    gain=0.6      multiply intensities (lighting dimmer < 1 < brighter)
    offset=-20    add a constant
    gamma=1.4     out = 255 * (in/255) ** gamma
    blur=5        Gaussian blur, odd kernel size (focus loss)
    noise=8       Gaussian noise sigma, seeded per frame name (sensor noise)

    apply(image, "gain=0.55,blur=5", seed_key="cross_NG_201.png")
"""

from __future__ import annotations

import hashlib

import cv2
import numpy as np

ORDER = ("gain", "offset", "gamma", "blur", "noise")


def parse(spec: str) -> dict[str, float]:
    out: dict[str, float] = {}
    for part in (spec or "").split(","):
        part = part.strip()
        if not part:
            continue
        key, _, value = part.partition("=")
        if key not in ORDER:
            raise ValueError(f"unknown transform {key!r}; use {', '.join(ORDER)}")
        out[key] = float(value)
    return out


def apply(image: np.ndarray, spec: str | dict, seed_key: str = "") -> np.ndarray:
    params = parse(spec) if isinstance(spec, str) else spec
    if not params:
        return image
    img = image.astype(np.float32)
    if "gain" in params:
        img *= params["gain"]
    if "offset" in params:
        img += params["offset"]
    if "gamma" in params:
        img = 255.0 * np.power(np.clip(img, 0, 255) / 255.0, params["gamma"])
    if "blur" in params and params["blur"] >= 3:
        k = int(params["blur"]) | 1
        img = cv2.GaussianBlur(img, (k, k), 0)
    if params.get("noise", 0) > 0:
        seed = int.from_bytes(hashlib.sha256(seed_key.encode()).digest()[:4], "little")
        img += np.random.default_rng(seed).normal(0, params["noise"], img.shape).astype(np.float32)
    return np.clip(img + 0.5, 0, 255).astype(np.uint8)
