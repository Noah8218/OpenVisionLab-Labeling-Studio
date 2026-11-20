"""Recipe judgment: detections -> Pass / Ng, plus explicit non-judgment states.

The no-detection verdict is mandatory (design E8): defect detection treats an
empty result as Pass, a presence check treats it as Ng. A rule without it is
rejected as RuleMissing instead of guessing.
"""

from __future__ import annotations

import enum
import hashlib
import json
from dataclasses import asdict, dataclass

import numpy as np

from .postprocess import Detections


class ResultCode(enum.IntEnum):
    NONE = 0
    PASS = 1
    NG = 2
    MODEL_ERROR = 3
    INPUT_MISMATCH = 4
    RULE_MISSING = 5
    TIMEOUT = 6  # set by the requesting side, never by the worker
    HOLD = 7  # degraded fallback could not clear the part; route it to manual inspection


@dataclass(frozen=True)
class DecisionRule:
    ng_classes: tuple[int, ...]
    min_score: float
    min_count: int
    no_detection_verdict: str  # "Pass" or "Ng"
    # Optional line settings (ovl_edge.line_profile). Empty values keep the original behaviour.
    class_min_score: tuple[tuple[int, float], ...] = ()  # per-class threshold overriding min_score
    roi_xyxy: tuple[float, float, float, float] | None = None  # pixels; a detection counts if its box centre is inside
    min_box_area: float = 0.0  # pixels^2; smaller detections do not count

    @staticmethod
    def from_dict(data: dict | None) -> "DecisionRule | None":
        if not data or data.get("no_detection_verdict") not in ("Pass", "Ng"):
            return None
        roi = data.get("roi_xyxy")
        return DecisionRule(
            ng_classes=tuple(int(c) for c in data.get("ng_classes", [])),
            min_score=float(data.get("min_score", 0.25)),
            min_count=int(data.get("min_count", 1)),
            no_detection_verdict=str(data["no_detection_verdict"]),
            class_min_score=tuple(sorted((int(k), float(v)) for k, v in (data.get("class_min_score") or {}).items())),
            roi_xyxy=tuple(float(v) for v in roi) if roi else None,
            min_box_area=float(data.get("min_box_area", 0.0)),
        )

    def digest(self) -> str:
        # Rules without the optional settings keep the digest they had before these fields existed.
        data = asdict(self)
        for key, empty in (("class_min_score", ()), ("roi_xyxy", None), ("min_box_area", 0.0)):
            if data[key] in (empty, [], ()):
                data.pop(key)
        return hashlib.sha256(json.dumps(data, sort_keys=True).encode("utf-8")).hexdigest()

    def thresholds(self, classes: np.ndarray) -> np.ndarray:
        if not self.class_min_score:
            return np.full(len(classes), self.min_score, np.float32)
        table = dict(self.class_min_score)
        return np.array([table.get(int(c), self.min_score) for c in classes], np.float32)


@dataclass(frozen=True)
class Decision:
    code: ResultCode
    detections: int
    qualifying: int
    top_class: int
    top_score: float


def decide(detections: Detections, rule: DecisionRule | None) -> Decision:
    if rule is None:
        return Decision(ResultCode.RULE_MISSING, len(detections), 0, -1, 0.0)
    top_class, top_score = -1, 0.0
    if len(detections):
        best = int(np.argmax(detections.scores))
        top_class, top_score = int(detections.classes[best]), float(detections.scores[best])
    if len(detections) == 0:
        code = ResultCode.PASS if rule.no_detection_verdict == "Pass" else ResultCode.NG
        return Decision(code, 0, 0, top_class, top_score)
    mask = detections.scores >= rule.thresholds(detections.classes)
    if rule.ng_classes:
        mask &= np.isin(detections.classes, np.asarray(rule.ng_classes))
    if rule.roi_xyxy is not None:
        x1, y1, x2, y2 = rule.roi_xyxy
        cx = (detections.boxes[:, 0] + detections.boxes[:, 2]) / 2
        cy = (detections.boxes[:, 1] + detections.boxes[:, 3]) / 2
        mask &= (cx >= x1) & (cx <= x2) & (cy >= y1) & (cy <= y2)
    if rule.min_box_area > 0:
        area = (detections.boxes[:, 2] - detections.boxes[:, 0]) * (detections.boxes[:, 3] - detections.boxes[:, 1])
        mask &= area >= rule.min_box_area
    qualifying = int(mask.sum())
    code = ResultCode.NG if qualifying >= rule.min_count else ResultCode.PASS
    return Decision(code, len(detections), qualifying, top_class, top_score)
