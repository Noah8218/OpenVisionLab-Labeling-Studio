from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path
from types import ModuleType
from typing import Any, Iterable


SUPPORTED_IMAGE_SUFFIXES = {".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"}


def load_worker_module(worker_script: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location("openvisionlab_runtime_adapter", worker_script)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load YOLO adapter: {worker_script}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def class_images(dataset_root: Path, split: str, class_name: str) -> Iterable[Path]:
    class_root = dataset_root / split / class_name
    if not class_root.is_dir():
        return []
    return sorted(
        path.resolve()
        for path in class_root.iterdir()
        if path.is_file() and path.suffix.lower() in SUPPORTED_IMAGE_SUFFIXES
    )


def first_classification_candidate(candidates: list[dict[str, Any]]) -> dict[str, Any] | None:
    for candidate in candidates:
        if candidate.get("imageLevel") is True and candidate.get("candidateType") == "imageClassification":
            return candidate
    return candidates[0] if candidates else None


def run(args: argparse.Namespace) -> int:
    worker_module = load_worker_module(Path(args.worker_script).resolve())
    detector_args = argparse.Namespace(
        weights=str(Path(args.weights).resolve()),
        model_root=str(Path(args.model_root).resolve()),
        device=args.device,
        img_size=args.img_size,
        conf=args.conf,
        iou=args.iou,
        debug=args.debug,
    )
    detector = worker_module.build_detector(detector_args)
    detector.load()

    dataset_root = Path(args.dataset_root).resolve()
    for expected_class_name in ("normal", "abnormal"):
        for image_path in class_images(dataset_root, args.split, expected_class_name):
            candidates, _ = detector.detect_path(image_path)
            candidate = first_classification_candidate(candidates)
            if candidate is None:
                raise RuntimeError(f"YOLO adapter returned no candidate for {image_path}")
            print(
                json.dumps(
                    {
                        "imagePath": str(image_path),
                        "expectedClassName": expected_class_name,
                        "predictedClassName": str(candidate.get("className") or ""),
                        "confidence": float(candidate.get("confidence") or 0.0),
                    },
                    ensure_ascii=True,
                    separators=(",", ":"),
                ),
                flush=True,
            )
    return 0


def self_test() -> int:
    preferred = first_classification_candidate(
        [
            {"className": "ignored"},
            {
                "candidateType": "imageClassification",
                "imageLevel": True,
                "className": "normal",
                "confidence": 0.91,
            },
        ]
    )
    assert preferred is not None
    assert preferred["className"] == "normal"
    assert first_classification_candidate([]) is None
    print("self-test passed", flush=True)
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Evaluate YOLO classification images with one persistent adapter model load.")
    parser.add_argument("--worker-script", default="")
    parser.add_argument("--weights", default="")
    parser.add_argument("--model-root", default="")
    parser.add_argument("--dataset-root", default="")
    parser.add_argument("--split", default="test")
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--img-size", type=int, default=64)
    parser.add_argument("--conf", type=float, default=0.0)
    parser.add_argument("--iou", type=float, default=0.45)
    parser.add_argument("--debug", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return self_test()
    return run(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
