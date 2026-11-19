"""Write the reproduction bundle record for one prepared model package (design M3).

Records, in one JSON next to the package: container image id (passed in, since a
container cannot read its own image digest), model spec / ONNX / engine hashes,
judgment rule digest, GPU / driver and TensorRT / ONNX Runtime versions.
A package plus its bundle.json is the unit to roll back to.

    python -m ovl_edge.cli.bundle_manifest --model-dir /model --rule /config/rule.json --image-id sha256:... --out /model/bundle.json
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import subprocess
import sys
from pathlib import Path

from ovl_edge.decision import DecisionRule
from ovl_edge.spec import ModelSpec, sha256_file


def _gpu() -> dict:
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=name,driver_version,compute_cap", "--format=csv,noheader"], capture_output=True, text=True, check=True
        ).stdout.strip()
        name, driver, cc = [part.strip() for part in out.split(",")]
        return {"name": name, "driver": driver, "compute_capability": cc}
    except (OSError, subprocess.CalledProcessError, ValueError):
        return {"name": "unknown"}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--rule", default="")
    parser.add_argument("--image-id", default="", help="docker image id or digest of the runtime image")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)

    import onnxruntime

    try:
        import tensorrt

        trt_version = tensorrt.__version__
    except ImportError:
        trt_version = None
    model_dir = Path(args.model_dir)
    spec = ModelSpec.load(model_dir / "model_spec.json")
    engines_path = model_dir / "engines" / "engines.json"
    engines = json.loads(engines_path.read_text(encoding="utf-8")) if engines_path.exists() else {"engines": {}}
    verified = {}
    for key, entry in engines.get("engines", {}).items():
        path = model_dir / "engines" / entry["engine"]
        actual = sha256_file(path) if path.exists() else None
        verified[key] = {"engine": entry["engine"], "engine_sha256": entry["engine_sha256"], "matches_file": actual == entry["engine_sha256"]}
    rule_info = None
    if args.rule:
        rule = DecisionRule.from_dict(json.loads(Path(args.rule).read_text(encoding="utf-8")))
        rule_info = {"file": Path(args.rule).name, "file_sha256": sha256_file(args.rule), "rule_digest": rule.digest() if rule else None}
    record = {
        "created_utc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "runtime_image": args.image_id or None,
        "gpu": _gpu(),
        "tensorrt": trt_version,
        "engines_built_with_tensorrt": engines.get("tensorrt"),
        "onnxruntime": onnxruntime.__version__,
        "model_spec_sha256": sha256_file(model_dir / "model_spec.json"),
        "source_weights": {"file": spec.source_weights, "sha256": spec.source_sha256},
        "onnx": {"file": spec.onnx_file, "sha256": spec.onnx_sha256, "matches_file": sha256_file(model_dir / spec.onnx_file) == spec.onnx_sha256},
        "calibration_list_sha256": engines.get("calibration_list_sha256"),
        "engines": verified,
        "rule": rule_info,
    }
    consistent = record["onnx"]["matches_file"] and all(v["matches_file"] for v in verified.values()) and engines.get("tensorrt") == trt_version
    record["consistent"] = bool(consistent)
    Path(args.out).write_text(json.dumps(record, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"out": args.out, "consistent": record["consistent"]}))
    return 0 if consistent else 1


if __name__ == "__main__":
    sys.exit(main())
