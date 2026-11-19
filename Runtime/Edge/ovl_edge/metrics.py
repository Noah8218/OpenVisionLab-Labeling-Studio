"""Box mAP (COCO-style IoU 0.50:0.95, 101-point interpolation) for benchmark tables.

This follows the same AP definition Ultralytics val uses (method='interp');
values are comparable between runtimes evaluated here, not a replacement for
the official COCO evaluator.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np

from .postprocess import Detections, box_iou_one_to_many

IOU_THRESHOLDS = np.linspace(0.5, 0.95, 10)


@dataclass(frozen=True)
class GroundTruth:
    boxes: np.ndarray  # (n, 4) xyxy pixels
    classes: np.ndarray  # (n,)


def load_yolo_labels(label_path: Path, width: int, height: int) -> GroundTruth:
    if not label_path.exists() or label_path.stat().st_size == 0:
        return GroundTruth(np.zeros((0, 4), np.float32), np.zeros((0,), np.int64))
    rows = np.loadtxt(label_path, ndmin=2, dtype=np.float64)
    cls = rows[:, 0].astype(np.int64)
    cx, cy, w, h = rows[:, 1] * width, rows[:, 2] * height, rows[:, 3] * width, rows[:, 4] * height
    boxes = np.stack([cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2], axis=1).astype(np.float32)
    return GroundTruth(boxes, cls)


def _match_image(det: Detections, gt: GroundTruth) -> np.ndarray:
    """Return (n_det, 10) bool: detection i is a TP at IoU threshold t."""
    correct = np.zeros((len(det), IOU_THRESHOLDS.size), dtype=bool)
    if len(det) == 0 or gt.classes.size == 0:
        return correct
    iou = np.stack([box_iou_one_to_many(g, det.boxes) for g in gt.boxes], axis=0)  # (n_gt, n_det)
    same_class = gt.classes[:, None] == det.classes[None, :]
    for t_index, threshold in enumerate(IOU_THRESHOLDS):
        gi, di = np.nonzero((iou >= threshold) & same_class)
        if gi.size == 0:
            continue
        order = np.argsort(-iou[gi, di], kind="stable")
        gi, di = gi[order], di[order]
        used_g: set[int] = set()
        used_d: set[int] = set()
        for g, d in zip(gi.tolist(), di.tolist()):
            if g in used_g or d in used_d:
                continue
            used_g.add(g)
            used_d.add(d)
            correct[d, t_index] = True
    return correct


def _ap(recall: np.ndarray, precision: np.ndarray) -> float:
    mrec = np.concatenate(([0.0], recall, [1.0]))
    mpre = np.concatenate(([1.0], precision, [0.0]))
    mpre = np.flip(np.maximum.accumulate(np.flip(mpre)))
    x = np.linspace(0, 1, 101)
    trapezoid = getattr(np, "trapezoid", None) or np.trapz  # numpy < 2.0 only has trapz
    return float(trapezoid(np.interp(x, mrec, mpre), x))


def mean_average_precision(pairs: list[tuple[Detections, GroundTruth]]) -> dict:
    tps, confs, pred_cls, gt_cls = [], [], [], []
    for det, gt in pairs:
        tps.append(_match_image(det, gt))
        confs.append(det.scores)
        pred_cls.append(det.classes)
        gt_cls.append(gt.classes)
    tp = np.concatenate(tps) if tps else np.zeros((0, 10), bool)
    conf = np.concatenate(confs) if confs else np.zeros((0,))
    pcls = np.concatenate(pred_cls) if pred_cls else np.zeros((0,), np.int64)
    gcls = np.concatenate(gt_cls) if gt_cls else np.zeros((0,), np.int64)
    order = np.argsort(-conf, kind="stable")
    tp, pcls = tp[order], pcls[order]
    classes = np.unique(gcls)
    ap = np.zeros((classes.size, IOU_THRESHOLDS.size))
    for ci, c in enumerate(classes):
        mask = pcls == c
        n_gt = int((gcls == c).sum())
        if mask.sum() == 0 or n_gt == 0:
            continue
        tpc = tp[mask].cumsum(0)
        fpc = (~tp[mask]).cumsum(0)
        recall = tpc / n_gt
        precision = tpc / (tpc + fpc)
        for t in range(IOU_THRESHOLDS.size):
            ap[ci, t] = _ap(recall[:, t], precision[:, t])
    return {
        "map50": float(ap[:, 0].mean()) if classes.size else 0.0,
        "map50_95": float(ap.mean()) if classes.size else 0.0,
        "classes_with_gt": int(classes.size),
        "gt_boxes": int(gcls.size),
        "detections": int(conf.size),
    }
