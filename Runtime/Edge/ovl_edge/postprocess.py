"""Host post-processing for raw YOLO detect heads (no NMS inside the graph).

Input is the raw head tensor of shape (1, 4 + nc, N): cx, cy, w, h in
letterboxed input pixels followed by per-class scores. The steps follow
ultralytics.utils.nms.non_max_suppression (multi_label=False) and
ultralytics.utils.ops.scale_boxes (per-axis gain, Ultralytics 8.4), so the same
code serves ORT and TensorRT.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from .preprocess import LetterboxInfo


@dataclass(frozen=True)
class Detections:
    boxes: np.ndarray  # (n, 4) float32 xyxy in original-image pixels
    scores: np.ndarray  # (n,) float32
    classes: np.ndarray  # (n,) int64

    @staticmethod
    def empty() -> "Detections":
        return Detections(np.zeros((0, 4), np.float32), np.zeros((0,), np.float32), np.zeros((0,), np.int64))

    def __len__(self) -> int:
        return int(self.scores.shape[0])


def box_iou_one_to_many(box: np.ndarray, boxes: np.ndarray) -> np.ndarray:
    x1 = np.maximum(box[0], boxes[:, 0])
    y1 = np.maximum(box[1], boxes[:, 1])
    x2 = np.minimum(box[2], boxes[:, 2])
    y2 = np.minimum(box[3], boxes[:, 3])
    inter = np.clip(x2 - x1, 0, None) * np.clip(y2 - y1, 0, None)
    area_a = (box[2] - box[0]) * (box[3] - box[1])
    area_b = (boxes[:, 2] - boxes[:, 0]) * (boxes[:, 3] - boxes[:, 1])
    union = area_a + area_b - inter
    return np.where(union > 0, inter / np.maximum(union, 1e-12), 0.0)


def nms(boxes: np.ndarray, scores: np.ndarray, iou_threshold: float) -> np.ndarray:
    # Same suppression rule as torchvision.ops.nms: drop a box when IoU > threshold.
    order = np.argsort(-scores, kind="stable")
    keep: list[int] = []
    while order.size > 0:
        current = int(order[0])
        keep.append(current)
        if order.size == 1:
            break
        ious = box_iou_one_to_many(boxes[current], boxes[order[1:]])
        order = order[1:][ious <= iou_threshold]
    return np.asarray(keep, dtype=np.int64)


def decode(raw: np.ndarray, num_classes: int, nms_cfg: dict, info: LetterboxInfo) -> Detections:
    if raw.ndim != 3 or raw.shape[0] != 1 or raw.shape[1] != 4 + num_classes:
        raise ValueError(f"Unexpected raw head shape {raw.shape} for {num_classes} classes")
    pred = raw[0].astype(np.float32, copy=False).T  # (N, 4 + nc)
    class_scores = pred[:, 4:]
    scores = class_scores.max(axis=1)
    keep = scores > float(nms_cfg["conf"])
    if not np.any(keep):
        return Detections.empty()
    pred, scores = pred[keep], scores[keep]
    classes = class_scores[keep].argmax(axis=1).astype(np.int64)
    cx, cy, w, h = pred[:, 0], pred[:, 1], pred[:, 2], pred[:, 3]
    boxes = np.stack([cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2], axis=1)

    max_nms = int(nms_cfg.get("max_nms", 30000))
    if boxes.shape[0] > max_nms:
        top = np.argsort(-scores, kind="stable")[:max_nms]
        boxes, scores, classes = boxes[top], scores[top], classes[top]

    offset = 0.0 if nms_cfg.get("agnostic", False) else float(nms_cfg.get("max_wh", 7680))
    kept = nms(boxes + (classes[:, None].astype(np.float32) * offset), scores, float(nms_cfg["iou"]))
    kept = kept[: int(nms_cfg.get("max_det", 300))]
    boxes, scores, classes = boxes[kept], scores[kept], classes[kept]

    boxes = boxes.copy()
    boxes[:, [0, 2]] -= info.pad_left
    boxes[:, [1, 3]] -= info.pad_top
    boxes[:, [0, 2]] /= info.gain_x
    boxes[:, [1, 3]] /= info.gain_y
    boxes[:, [0, 2]] = boxes[:, [0, 2]].clip(0, info.src_width)
    boxes[:, [1, 3]] = boxes[:, [1, 3]].clip(0, info.src_height)
    return Detections(boxes.astype(np.float32), scores.astype(np.float32), classes)
