"""Detection-list comparison used by parity checks (not an accuracy metric)."""

from __future__ import annotations

from dataclasses import asdict, dataclass

import numpy as np

from .postprocess import Detections, box_iou_one_to_many


@dataclass(frozen=True)
class PairComparison:
    count_a: int
    count_b: int
    matched: int
    unmatched_a: int
    unmatched_b: int
    min_iou: float | None
    max_score_diff: float | None
    max_corner_diff_px: float | None

    def to_dict(self) -> dict:
        return asdict(self)


def compare(a: Detections, b: Detections, iou_threshold: float = 0.5) -> PairComparison:
    """Greedy same-class matching by descending IoU."""
    pairs: list[tuple[int, int, float]] = []
    for cls in np.union1d(a.classes, b.classes):
        ia = np.flatnonzero(a.classes == cls)
        ib = np.flatnonzero(b.classes == cls)
        if ia.size == 0 or ib.size == 0:
            continue
        candidates = []
        for i in ia:
            ious = box_iou_one_to_many(a.boxes[i], b.boxes[ib])
            for k, j in enumerate(ib):
                if ious[k] >= iou_threshold:
                    candidates.append((float(ious[k]), int(i), int(j)))
        used_a: set[int] = set()
        used_b: set[int] = set()
        for iou, i, j in sorted(candidates, reverse=True):
            if i in used_a or j in used_b:
                continue
            used_a.add(i)
            used_b.add(j)
            pairs.append((i, j, iou))
    if pairs:
        ia = np.array([p[0] for p in pairs])
        ib = np.array([p[1] for p in pairs])
        min_iou = float(min(p[2] for p in pairs))
        max_score = float(np.abs(a.scores[ia] - b.scores[ib]).max())
        max_corner = float(np.abs(a.boxes[ia] - b.boxes[ib]).max())
    else:
        min_iou = max_score = max_corner = None
    return PairComparison(
        count_a=len(a),
        count_b=len(b),
        matched=len(pairs),
        unmatched_a=len(a) - len(pairs),
        unmatched_b=len(b) - len(pairs),
        min_iou=min_iou,
        max_score_diff=max_score,
        max_corner_diff_px=max_corner,
    )
