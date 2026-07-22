#!/usr/bin/env python3
"""Create one reviewable MobileSAM mask from an operator-drawn box prompt."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
from pathlib import Path
from typing import Any


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def compact_json(value: dict[str, Any]) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--weights", default="")
    parser.add_argument("--image", default="")
    parser.add_argument("--x", type=int, default=0)
    parser.add_argument("--y", type=int, default=0)
    parser.add_argument("--width", type=int, default=0)
    parser.add_argument("--height", type=int, default=0)
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def self_test() -> int:
    prompt = [10, 20, 40, 60]
    assert prompt[2] > prompt[0] and prompt[3] > prompt[1]
    print(compact_json({"success": True, "mode": "self-test", "promptBox": prompt}))
    return 0


def run(args: argparse.Namespace) -> int:
    weights = Path(args.weights).expanduser().resolve()
    image = Path(args.image).expanduser().resolve()
    if not weights.is_file():
        raise FileNotFoundError(f"MobileSAM weights not found: {weights}")
    if not image.is_file():
        raise FileNotFoundError(f"prompt image not found: {image}")
    if args.width <= 0 or args.height <= 0:
        raise ValueError("prompt box width and height must be positive")

    from ultralytics import SAM, __version__ as ultralytics_version
    import torch

    left = max(0, args.x)
    top = max(0, args.y)
    right = left + args.width
    bottom = top + args.height
    started = time.perf_counter()
    model = SAM(str(weights))
    results = model.predict(
        str(image),
        bboxes=[left, top, right, bottom],
        device=args.device,
        verbose=False,
    )
    elapsed_ms = (time.perf_counter() - started) * 1000.0
    result = results[0] if results else None
    masks = getattr(result, "masks", None)
    polygons = list(getattr(masks, "xy", []) or [])
    if not polygons:
        raise RuntimeError("MobileSAM returned no mask for the prompt box")

    polygon = max(polygons, key=lambda points: len(points))
    points = [
        {"x": round(float(point[0]), 3), "y": round(float(point[1]), 3)}
        for point in polygon
    ]
    if len(points) < 3:
        raise RuntimeError("MobileSAM returned a mask contour with fewer than three points")

    xs = [point["x"] for point in points]
    ys = [point["y"] for point in points]
    mask_area = int(masks.data[0].sum().item()) if getattr(masks, "data", None) is not None else 0
    original_shape = list(getattr(result, "orig_shape", []) or [])
    image_height = int(original_shape[0]) if len(original_shape) > 0 else 0
    image_width = int(original_shape[1]) if len(original_shape) > 1 else 0
    output = {
        "success": True,
        "mode": "box-prompt",
        "model": "MobileSAM",
        "weightsPath": str(weights),
        "weightsSha256": file_sha256(weights),
        "imagePath": str(image),
        "imageWidth": image_width,
        "imageHeight": image_height,
        "promptBox": [left, top, right, bottom],
        "bounds": {
            "x": min(xs),
            "y": min(ys),
            "width": max(xs) - min(xs),
            "height": max(ys) - min(ys),
        },
        "polygon": points,
        "maskArea": mask_area,
        "elapsedMs": round(elapsed_ms, 3),
        "device": args.device,
        "ultralyticsVersion": ultralytics_version,
        "torchVersion": torch.__version__,
    }
    print(compact_json(output))
    return 0


def main() -> int:
    args = parse_args()
    if args.self_test:
        return self_test()
    try:
        return run(args)
    except Exception as error:
        print(
            compact_json(
                {
                    "success": False,
                    "errorCode": type(error).__name__,
                    "error": str(error),
                }
            ),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
