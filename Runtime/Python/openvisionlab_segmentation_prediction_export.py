#!/usr/bin/env python3
"""Create app-owned raster prediction artifacts for segmentation comparison.

The source is always an OpenVisionLab U-Net export.  A selected U-Net or
Ultralytics segmentation runtime reads those immutable test images and writes
only an app-owned prediction manifest plus indexed PNG masks.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


IMAGE_EXTENSIONS = {".bmp", ".jpeg", ".jpg", ".png", ".tif", ".tiff"}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def classes_from_manifest(manifest: dict[str, Any]) -> list[str]:
    values = manifest.get("Classes") or manifest.get("classes")
    if not isinstance(values, list) or not values:
        raise ValueError("dataset manifest has no class contract")
    classes: list[str] = []
    for expected, item in enumerate(values, start=1):
        if not isinstance(item, dict):
            raise ValueError("dataset manifest has an invalid class contract item")
        index = int(item.get("Index", item.get("index", 0)))
        name = str(item.get("Name", item.get("name", ""))).strip()
        if index != expected or not name:
            raise ValueError("dataset manifest class indices must be contiguous and one-based")
        classes.append(name)
    return classes


def classes_from_file(path: Path) -> list[str]:
    values = read_json(path)
    if not isinstance(values, list):
        raise ValueError("classes.json must contain the canonical class array")
    return classes_from_manifest({"Classes": values})


def split_images(manifest: dict[str, Any], split: str) -> list[dict[str, Any]]:
    values = manifest.get("Splits") or manifest.get("splits")
    if not isinstance(values, list):
        raise ValueError("dataset manifest has no split contract")
    matched = [item for item in values if isinstance(item, dict) and str(item.get("Split", item.get("split", ""))).lower() == split.lower()]
    if len(matched) != 1:
        raise ValueError(f"dataset manifest must contain exactly one '{split}' split")
    images = matched[0].get("Images") or matched[0].get("images")
    if not isinstance(images, list) or not images:
        raise ValueError(f"dataset manifest split '{split}' has no images")
    return images


def contract_value(manifest: dict[str, Any], pascal: str, camel: str) -> str:
    value = str(manifest.get(pascal, manifest.get(camel, ""))).strip()
    if not value:
        raise ValueError(f"dataset manifest is missing {pascal}")
    return value


def normalized_model_names(value: Any) -> list[str]:
    if isinstance(value, dict):
        ordered = sorted(value.items(), key=lambda item: int(item[0]))
        return [str(item[1]) for item in ordered]
    if isinstance(value, (list, tuple)):
        return [str(item) for item in value]
    raise ValueError("model has no readable class names")


def assert_classes(expected: list[str], actual: list[str], adapter: str) -> None:
    if expected != actual:
        raise ValueError(
            f"{adapter} checkpoint class contract does not match the canonical dataset: "
            f"expected {expected}, got {actual}")


def load_unet_predictor(weights: Path, expected_classes: list[str], image_size: int, device_text: str):
    worker_root = Path(__file__).resolve().parent
    sys.path.insert(0, str(worker_root))
    from openvisionlab_unet_worker import build_unet, torch_dependencies

    _, torch, _, functional, Image = torch_dependencies()
    device = torch.device(device_text or ("cuda" if torch.cuda.is_available() else "cpu"))
    checkpoint = torch.load(weights, map_location=device)
    if checkpoint.get("format") != "openvisionlab-unet-v1":
        raise ValueError("checkpoint is not an OpenVisionLab U-Net v1 checkpoint")
    classes = checkpoint.get("classes")
    if not isinstance(classes, list):
        raise ValueError("U-Net checkpoint has no class contract")
    assert_classes(expected_classes, [str(item) for item in classes], "U-Net")
    model = build_unet(3, len(expected_classes) + 1, int(checkpoint.get("baseChannels", 16))).to(device)
    model.load_state_dict(checkpoint["stateDict"])
    model.eval()
    resolved_size = max(1, int(checkpoint.get("imageSize", image_size)))

    def predict(image_path: Path):
        import numpy as np
        image = Image.open(image_path).convert("RGB")
        width, height = image.size
        resized = image.resize((resolved_size, resolved_size), Image.Resampling.BILINEAR)
        image_tensor = torch.from_numpy(np.asarray(resized, dtype=np.float32) / 255.0).permute(2, 0, 1).unsqueeze(0)
        with torch.no_grad():
            labels = torch.softmax(model(image_tensor.to(device)), dim=1)
            labels = functional.interpolate(labels, size=(height, width), mode="bilinear", align_corners=False)[0].argmax(dim=0)
        return labels.cpu().numpy().astype("uint8"), width, height, str(device)

    return predict


def load_ultralytics_predictor(weights: Path, expected_classes: list[str], image_size: int, confidence: float, device_text: str):
    import numpy as np
    from PIL import Image, ImageDraw
    from ultralytics import YOLO

    model = YOLO(str(weights))
    assert_classes(expected_classes, normalized_model_names(model.names), "Ultralytics")
    device = device_text.strip()

    def predict(image_path: Path):
        source = Image.open(image_path).convert("RGB")
        width, height = source.size
        results = model.predict(
            source=str(image_path),
            imgsz=max(1, image_size),
            conf=max(0.0, min(1.0, confidence)),
            device=device or None,
            verbose=False)
        result = results[0]
        output = Image.new("L", (width, height), 0)
        if result.masks is not None and result.boxes is not None:
            masks = result.masks.xy
            classes = result.boxes.cls.tolist()
            scores = result.boxes.conf.tolist()
            ordered = sorted(range(min(len(masks), len(classes), len(scores))), key=lambda index: float(scores[index]))
            drawer = ImageDraw.Draw(output)
            for index in ordered:
                class_id = int(classes[index])
                if class_id < 0 or class_id >= len(expected_classes):
                    raise ValueError(f"Ultralytics prediction has an out-of-contract class index: {class_id}")
                points = [(float(point[0]), float(point[1])) for point in masks[index]]
                if len(points) >= 3:
                    drawer.polygon(points, fill=class_id + 1)
        return np.asarray(output, dtype="uint8"), width, height, device or "runtime-default"

    return predict


def relative_value(item: dict[str, Any], pascal: str, camel: str) -> str:
    value = str(item.get(pascal, item.get(camel, ""))).replace("\\", "/").strip("/")
    if not value:
        raise ValueError(f"dataset manifest image entry is missing {pascal}")
    return value


def export_predictions(args: argparse.Namespace) -> Path:
    from PIL import Image

    data_root = Path(args.data_root).expanduser().resolve()
    weights = Path(args.weights).expanduser().resolve()
    output_root = Path(args.output_root).expanduser().resolve()
    if not data_root.is_dir():
        raise FileNotFoundError(f"canonical U-Net export was not found: {data_root}")
    if not weights.is_file():
        raise FileNotFoundError(f"model checkpoint was not found: {weights}")
    if output_root.exists() and any(output_root.iterdir()):
        raise ValueError(f"prediction output must be a new or empty app-owned directory: {output_root}")
    manifest = read_json(data_root / "dataset-manifest.json")
    if not isinstance(manifest, dict):
        raise ValueError("dataset manifest must be a JSON object")
    classes = classes_from_manifest(manifest)
    assert_classes(classes, classes_from_file(data_root / "classes.json"), "canonical export")
    images = split_images(manifest, args.split)
    dataset_fingerprint = contract_value(manifest, "DatasetFingerprint", "datasetFingerprint")
    source_sha = contract_value(manifest, "SourceDataTreeSha256", "sourceDataTreeSha256")
    class_sha = contract_value(manifest, "ClassContractSha256", "classContractSha256")
    adapter = args.adapter.lower()
    predictor = load_unet_predictor(weights, classes, args.image_size, args.device) if adapter == "unet" else load_ultralytics_predictor(weights, classes, args.image_size, args.confidence, args.device)

    output_root.mkdir(parents=True, exist_ok=True)
    manifest_path = output_root / "prediction-manifest.jsonl"
    records: list[dict[str, Any]] = []
    for item in images:
        image_relative = relative_value(item, "ExportImageRelativePath", "exportImageRelativePath")
        source_relative = relative_value(item, "SourceRelativeImagePath", "sourceRelativeImagePath")
        source_sha256 = str(item.get("ImageSha256", item.get("imageSha256", ""))).strip()
        image_path = data_root / image_relative
        if image_path.suffix.lower() not in IMAGE_EXTENSIONS or not image_path.is_file():
            raise FileNotFoundError(f"canonical test image was not found: {image_path}")
        labels, width, height, device = predictor(image_path)
        if labels.shape != (height, width) or int(labels.max(initial=0)) > len(classes):
            raise ValueError(f"prediction mask violates the canonical image/class contract: {image_path}")
        prediction_relative = str(Path("predictions") / args.split / Path(image_relative).with_suffix(".png")).replace("\\", "/")
        prediction_path = output_root / prediction_relative
        prediction_path.parent.mkdir(parents=True, exist_ok=True)
        Image.fromarray(labels, mode="L").save(prediction_path)
        records.append({
            "version": 1,
            "adapterKey": adapter,
            "engine": args.engine.strip() or adapter,
            "datasetFingerprint": dataset_fingerprint,
            "sourceDataTreeSha256": source_sha,
            "classContractSha256": class_sha,
            "split": args.split,
            "checkpointSha256": sha256_file(weights),
            "checkpointPath": str(weights),
            "imageSize": args.image_size,
            "confidence": args.confidence,
            "device": device,
            "sourceRelativeImagePath": source_relative,
            "imageSha256": source_sha256,
            "imageWidth": width,
            "imageHeight": height,
            "predictionMaskRelativePath": prediction_relative,
            "predictionMaskSha256": sha256_file(prediction_path),
        })
    with manifest_path.open("w", encoding="utf-8", newline="\n") as stream:
        for record in records:
            stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")
    summary = {
        "version": 1,
        "adapterKey": adapter,
        "engine": args.engine.strip() or adapter,
        "datasetFingerprint": dataset_fingerprint,
        "sourceDataTreeSha256": source_sha,
        "classContractSha256": class_sha,
        "split": args.split,
        "checkpointPath": str(weights),
        "checkpointSha256": sha256_file(weights),
        "imageCount": len(records),
        "predictionManifest": manifest_path.name,
        "createdAtUtc": utc_now(),
    }
    (output_root / "run-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    return manifest_path


def self_test() -> int:
    with tempfile.TemporaryDirectory(prefix="openvisionlab-segmentation-prediction-") as temporary:
        root = Path(temporary)
        manifest = {
            "DatasetFingerprint": "dataset",
            "SourceDataTreeSha256": "source",
            "ClassContractSha256": "classes",
            "Classes": [{"Index": 1, "Name": "Defect"}],
            "Splits": [{"Split": "test", "Images": [{"ExportImageRelativePath": "images/test/a.png", "SourceRelativeImagePath": "test/images/a.png", "ImageSha256": "image"}]}],
        }
        (root / "dataset-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        assert classes_from_manifest(manifest) == ["Defect"]
        assert len(split_images(manifest, "test")) == 1
        print(json.dumps({"type": "SegmentationPredictionExportSelfTest", "ok": True}, separators=(",", ":")))
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="OpenVisionLab segmentation prediction export")
    parser.add_argument("--adapter", choices=("unet", "ultralytics"))
    parser.add_argument("--engine", default="")
    parser.add_argument("--data-root", default="")
    parser.add_argument("--weights", default="")
    parser.add_argument("--split", default="test", choices=("train", "valid", "test"))
    parser.add_argument("--output-root", default="")
    parser.add_argument("--image-size", type=int, default=320)
    parser.add_argument("--confidence", type=float, default=0.25)
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return args
    missing = [name for name in ("adapter", "data_root", "weights", "output_root") if not getattr(args, name)]
    if missing:
        parser.error("required arguments: " + ", ".join("--" + name.replace("_", "-") for name in missing))
    return args


def main() -> int:
    args = parse_args()
    if args.self_test:
        return self_test()
    manifest_path = export_predictions(args)
    print("OPENVISIONLAB_SEGMENTATION_PREDICTION_MANIFEST=" + str(manifest_path))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print("OPENVISIONLAB_SEGMENTATION_PREDICTION_ERROR=" + str(exc), file=sys.stderr)
        raise SystemExit(1)
