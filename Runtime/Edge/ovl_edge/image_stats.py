"""Image statistics for drift hints: is a failing frame darker, flatter, noisier or blurrier than training data?

    brightness  mean gray level
    contrast    p95 - p5 gray level
    noise       std of (gray - 3x3 median), high-frequency residue: grows with sensor gain
    sharpness   variance of the Laplacian: drops with defocus (and rises with noise)

A reference is the 1/5/50/95/99th percentiles of each statistic over training
images. A case outside the 1-99 range gets a hint; hints point to the camera and
lighting before the model is blamed.
"""

from __future__ import annotations

import cv2
import numpy as np

STATS = ("brightness", "contrast", "noise", "sharpness")
PERCENTILES = (1, 5, 50, 95, 99)


def image_stats(image: np.ndarray) -> dict[str, float]:
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if image.ndim == 3 else image
    p5, p95 = np.percentile(gray, (5, 95))
    residue = gray.astype(np.float32) - cv2.medianBlur(gray, 3).astype(np.float32)
    return {
        "brightness": float(gray.mean()),
        "contrast": float(p95 - p5),
        "noise": float(residue.std()),
        "sharpness": float(cv2.Laplacian(gray, cv2.CV_32F).var()),
    }


def reference(stats: list[dict[str, float]]) -> dict[str, dict[str, float]]:
    return {s: {f"p{p}": float(np.percentile([x[s] for x in stats], p)) for p in PERCENTILES} for s in STATS}


def hints(stat: dict[str, float], ref: dict[str, dict[str, float]]) -> list[str]:
    out = []
    if stat["brightness"] < ref["brightness"]["p1"]:
        out.append("brightness_low")
    elif stat["brightness"] > ref["brightness"]["p99"]:
        out.append("brightness_high")
    if stat["contrast"] < ref["contrast"]["p1"]:
        out.append("contrast_low")
    if stat["noise"] > ref["noise"]["p99"]:
        out.append("noise_high")
    if stat["sharpness"] < ref["sharpness"]["p1"]:
        out.append("blur")
    return out
