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


@dataclass(frozen=True)
class DecisionRule:
    ng_classes: tuple[int, ...]
    min_score: float
    min_count: int
    no_detection_verdict: str  # "Pass" or "Ng"

    @staticmethod
    def from_dict(data: dict | None) -> "DecisionRule | None":
        if not data or data.get("no_detection_verdict") not in ("Pass", "Ng"):
            return None
        return DecisionRule(
            ng_classes=tuple(int(c) for c in data.get("ng_classes", [])),
            min_score=float(data.get("min_score", 0.25)),
            min_count=int(data.get("min_count", 1)),
            no_detection_verdict=str(data["no_detection_verdict"]),
        )

    def digest(self) -> str:
        return hashlib.sha256(json.dumps(asdict(self), sort_keys=True).encode("utf-8")).hexdigest()


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
    mask = detections.scores >= rule.min_score
    if rule.ng_classes:
        mask &= np.isin(detections.classes, np.asarray(rule.ng_classes))
    qualifying = int(mask.sum())
    code = ResultCode.NG if qualifying >= rule.min_count else ResultCode.PASS
    return Decision(code, len(detections), qualifying, top_class, top_score)
