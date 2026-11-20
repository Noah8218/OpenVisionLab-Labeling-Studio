"""Rule-based (OpenCV) inspection used as the last fallback when no model can run.

It is deliberately simple and explainable:
  1. Object mask: blur, Otsu threshold, keep the connected region under the image
     centre (works for both dark-on-bright and bright-on-dark parts).
  2. Shrink the mask by `erode_px` so the part outline is not read as a defect.
  3. Local deviation: |gray - median_blur(gray)| inside the mask. Pixels above
     `diff_threshold` are candidates; the verdict is Ng when the largest
     connected candidate region has at least `min_area` pixels.

Thresholds are fitted on the training split only (ovl_edge.cli.rule_calibrate)
and evaluated on the test split. Defects on the part boundary (edge chips,
extra material) are largely outside the eroded mask, so the rule is expected to
miss them; the calibration report gives per-class recall so the cost of running
on this fallback is known before it is needed.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import asdict, dataclass

import cv2
import numpy as np

from ovl_edge.decision import ResultCode


@dataclass(frozen=True)
class RuleParams:
    smooth_px: int = 5  # Gaussian blur before Otsu (odd)
    median_px: int = 21  # background estimate window (odd, larger than the defects)
    erode_px: int = 7  # mask shrink to skip the outline
    diff_threshold: int = 30  # gray-level deviation for a candidate pixel
    min_area: int = 25  # pixels in the largest candidate region for Ng

    def digest(self) -> str:
        return hashlib.sha256(json.dumps(asdict(self), sort_keys=True).encode()).hexdigest()

    @staticmethod
    def from_dict(data: dict) -> "RuleParams":
        return RuleParams(**{k: int(v) for k, v in data.items() if k in RuleParams.__dataclass_fields__})


@dataclass(frozen=True)
class RuleResult:
    code: ResultCode
    largest_area: int


def part_mask(gray: np.ndarray, params: RuleParams) -> np.ndarray:
    smooth = cv2.GaussianBlur(gray, (params.smooth_px, params.smooth_px), 0)
    _, binary = cv2.threshold(smooth, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    h, w = gray.shape
    cy, cx = h // 2, w // 2
    if binary[cy, cx] == 0:  # the part is the class under the centre, whatever its polarity
        binary = cv2.bitwise_not(binary)
    count, labels = cv2.connectedComponents(binary)
    part = (labels == labels[cy, cx]).astype(np.uint8) * 255 if count > 1 else binary
    if params.erode_px > 0:
        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * params.erode_px + 1, 2 * params.erode_px + 1))
        part = cv2.erode(part, kernel)
    return part


def deviation(gray: np.ndarray, params: RuleParams) -> np.ndarray:
    background = cv2.medianBlur(gray, params.median_px)
    return cv2.absdiff(gray, background)


def largest_area(dev: np.ndarray, mask: np.ndarray, threshold: int) -> int:
    candidates = ((dev > threshold) & (mask > 0)).astype(np.uint8)
    count, _, stats, _ = cv2.connectedComponentsWithStats(candidates, connectivity=8)
    return int(stats[1:, cv2.CC_STAT_AREA].max()) if count > 1 else 0


def to_gray(image: np.ndarray) -> np.ndarray:
    return cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if image.ndim == 3 else image


def inspect(image: np.ndarray, params: RuleParams) -> RuleResult:
    gray = to_gray(image)
    area = largest_area(deviation(gray, params), part_mask(gray, params), params.diff_threshold)
    return RuleResult(ResultCode.NG if area >= params.min_area else ResultCode.PASS, area)
