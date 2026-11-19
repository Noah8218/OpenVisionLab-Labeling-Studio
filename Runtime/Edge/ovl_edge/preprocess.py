"""Host pre-processing: letterbox, BGR->RGB, HWC->CHW, 0..1 scaling.

The arithmetic mirrors Ultralytics LetterBox(auto=False) and
ultralytics.utils.ops.scale_boxes so that box coordinates restored by
postprocess.py land on the same original-image pixels.
"""

from __future__ import annotations

from dataclasses import dataclass

import cv2
import numpy as np


@dataclass(frozen=True)
class LetterboxInfo:
    gain: float
    # Per-axis scale of the resize actually performed (rounded size / source size).
    # Ultralytics 8.4 scale_boxes restores boxes with these, not with `gain`;
    # using `gain` alone shifted corners by up to ~0.3 px in the first M1 run.
    gain_x: float
    gain_y: float
    pad_left: int
    pad_top: int
    src_width: int
    src_height: int


def letterbox(image_bgr: np.ndarray, size_hw: tuple[int, int], pad_value: int = 114, scaleup: bool = True) -> tuple[np.ndarray, LetterboxInfo]:
    if image_bgr.ndim != 3 or image_bgr.shape[2] != 3:
        raise ValueError(f"Expected an HxWx3 BGR image, got shape {image_bgr.shape}")
    src_h, src_w = image_bgr.shape[:2]
    dst_h, dst_w = size_hw
    gain = min(dst_h / src_h, dst_w / src_w)
    if not scaleup:
        gain = min(gain, 1.0)
    new_w, new_h = max(int(round(src_w * gain)), 1), max(int(round(src_h * gain)), 1)
    dw, dh = (dst_w - new_w) / 2, (dst_h - new_h) / 2
    resized = image_bgr
    if (src_w, src_h) != (new_w, new_h):
        resized = cv2.resize(image_bgr, (new_w, new_h), interpolation=cv2.INTER_LINEAR)
    top, bottom = int(round(dh - 0.1)), int(round(dh + 0.1))
    left, right = int(round(dw - 0.1)), int(round(dw + 0.1))
    padded = cv2.copyMakeBorder(resized, top, bottom, left, right, cv2.BORDER_CONSTANT, value=(pad_value, pad_value, pad_value))
    return padded, LetterboxInfo(gain=gain, gain_x=new_w / src_w, gain_y=new_h / src_h, pad_left=left, pad_top=top, src_width=src_w, src_height=src_h)


def to_input_tensor(letterboxed_bgr: np.ndarray, dtype: np.dtype = np.float32) -> np.ndarray:
    chw_rgb = letterboxed_bgr[..., ::-1].transpose(2, 0, 1)
    tensor = np.ascontiguousarray(chw_rgb, dtype=np.float32) / np.float32(255.0)
    return tensor[None].astype(dtype, copy=False)


def preprocess(image_bgr: np.ndarray, size_hw: tuple[int, int], dtype: np.dtype = np.float32, pad_value: int = 114) -> tuple[np.ndarray, LetterboxInfo]:
    padded, info = letterbox(image_bgr, size_hw, pad_value=pad_value)
    return to_input_tensor(padded, dtype), info
