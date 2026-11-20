"""Field feedback, steps 2-3: triage captured cases and export them for relabeling / retraining.

    # once per model: what the training images look like
    python -m ovl_edge.cli.cases reference --images /data/images --list /data/lists/train.txt --out /cases/reference.json
    # join worker captures with requester feedback, add drift hints, summarize
    python -m ovl_edge.cli.cases triage --cases /cases --reference /cases/reference.json --frames-root /frames --out /cases/triage.json
    # YOLO dataset (images/train|val, labels/train|val, data.yaml) for Labeling Studio or training
    python -m ovl_edge.cli.cases export-yolo --triage /cases/triage.json --names-from /model/model_spec.json --out /export/cases-yolo

export-yolo writes the model's detections as candidate labels (pre-annotations
to correct), or with --labels-from the reviewed labels for each source image.
--source-map (the simulator's request CSV) maps a frame back to its source image
name; it exists for tests with known ground truth, not for a plant.
"""

from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import json
import shutil
import sys
from pathlib import Path

import cv2
import numpy as np

from ovl_edge import image_stats
from ovl_edge.modbus_map import frame_relative_path
from ovl_edge.splits import read_list


def cmd_reference(args) -> int:
    paths = read_list(args.images, args.list) if args.list else sorted(Path(args.images).glob("*.png"))
    step = max(1, len(paths) // args.max_images)
    stats = [image_stats.image_stats(cv2.imread(str(p), cv2.IMREAD_COLOR)) for p in paths[::step]]
    ref = {"images": len(stats), "source": args.list or args.images, "stats": image_stats.reference(stats)}
    Path(args.out).write_text(json.dumps(ref, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(ref["stats"], indent=1))
    return 0


def cmd_triage(args) -> int:
    root = Path(args.cases)
    ref = json.loads(Path(args.reference).read_text(encoding="utf-8"))["stats"]
    cases: dict[tuple[int, int], dict] = {}
    for meta in sorted(root.glob("*/*.json")):
        record = json.loads(meta.read_text(encoding="utf-8"))
        record["frame_path"] = str(meta.parent / record["frame"])
        cases[(record["session"], record["seq"])] = record
    feedback_file = root / "feedback.jsonl"
    feedback = [json.loads(l) for l in feedback_file.read_text(encoding="utf-8").splitlines() if l.strip()] if feedback_file.exists() else []
    for fb in feedback:
        key = (fb["session"], fb["seq"])
        case = cases.get(key)
        if case is None:  # confirmed error the worker did not capture (e.g. an escape with no detection at all)
            frame = Path(args.frames_root) / frame_relative_path(key[0], key[1], args.frame_ext) if args.frames_root else None
            if frame is None or not frame.exists():
                continue
            case = cases[key] = {"session": key[0], "seq": key[1], "reasons": [], "frame_path": str(frame), "detections_xyxy_score_class": []}
        case["feedback"] = fb["outcome"]
        case["truth"] = fb["truth"]
        case["source_image"] = fb.get("image")
    seen: set[str] = set()
    rows = []
    for key in sorted(cases):
        case = cases[key]
        data = Path(case["frame_path"]).read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        case["frame_sha256"] = digest
        case["duplicate"] = digest in seen  # the same frame content seen before (a repeated part)
        seen.add(digest)
        stat = image_stats.image_stats(cv2.imdecode(np.frombuffer(data, np.uint8), cv2.IMREAD_COLOR))
        case["stats"] = {k: round(v, 2) for k, v in stat.items()}
        case["hints"] = image_stats.hints(stat, ref)
        rows.append(case)
    count = collections.Counter
    summary = {
        "cases": len(rows),
        "unique_frames": len(seen),
        "by_reason": dict(count(r for c in rows for r in c.get("reasons", []))),
        "by_feedback": dict(count(c.get("feedback", "none") for c in rows)),
        "by_hint": dict(count(h for c in rows for h in c["hints"])),
        "cases_with_hint": sum(1 for c in rows if c["hints"]),
        "stats_median": {s: round(float(np.median([c["stats"][s] for c in rows])), 2) for s in image_stats.STATS} if rows else {},
        "reference_p50": {s: round(ref[s]["p50"], 2) for s in image_stats.STATS},
    }
    Path(args.out).write_text(json.dumps({"summary": summary, "cases": rows}, indent=1) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=1))
    return 0


def yolo_lines(case: dict, names_count: int) -> list[str]:
    h, w = case.get("image_hw") or [0, 0]
    out = []
    for x1, y1, x2, y2, _score, cls in case.get("detections_xyxy_score_class", []):
        if not (w and h) or not 0 <= cls < names_count:
            continue
        out.append(f"{int(cls)} {(x1 + x2) / 2 / w:.6f} {(y1 + y2) / 2 / h:.6f} {(x2 - x1) / w:.6f} {(y2 - y1) / h:.6f}")
    return out


def cmd_export(args) -> int:
    triage = json.loads(Path(args.triage).read_text(encoding="utf-8"))
    names = json.loads(Path(args.names_from).read_text(encoding="utf-8"))["class_names"]
    source_map = {}
    if args.source_map:
        with open(args.source_map, encoding="utf-8") as handle:
            source_map = {int(r["seq"]): r["image"] for r in csv.DictReader(handle)}
    wanted = set(args.include)
    out = Path(args.out)
    if out.exists():
        shutil.rmtree(out)
    exported = collections.Counter()
    for case in triage["cases"]:
        if case.get("duplicate"):
            continue
        tags = set(case.get("reasons", [])) | ({case["feedback"]} if case.get("feedback") else set())
        if not tags & wanted:
            continue
        split = "val" if int(case["frame_sha256"][:8], 16) % 5 == 0 else "train"  # stable 80/20 by content
        stem = f"{case['session']:08x}-{case['seq']:010d}"
        ext = Path(case["frame_path"]).suffix
        (out / "images" / split).mkdir(parents=True, exist_ok=True)
        (out / "labels" / split).mkdir(parents=True, exist_ok=True)
        shutil.copyfile(case["frame_path"], out / "images" / split / f"{stem}{ext}")
        if args.labels_from:
            source = case.get("source_image") or source_map.get(case["seq"])
            if not source:
                exported["skipped_no_source"] += 1
                (out / "images" / split / f"{stem}{ext}").unlink()
                continue
            label = Path(args.labels_from) / (Path(source).stem + ".txt")
            lines = label.read_text(encoding="utf-8").split("\n") if label.exists() else []
            lines = [l for l in lines if l.strip()]
            exported["reviewed_labels"] += 1
        else:
            lines = yolo_lines(case, len(names))
            exported["model_prelabels"] += 1
        (out / "labels" / split / f"{stem}.txt").write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
        exported[split] += 1
    yaml = "path: .\ntrain: images/train\nval: images/val\nnames:\n" + "".join(f"  {i}: {n}\n" for i, n in enumerate(names))
    (out / "data.yaml").write_text(yaml, encoding="utf-8")
    print(json.dumps(dict(exported)))
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("reference")
    p.add_argument("--images", required=True)
    p.add_argument("--list", default="")
    p.add_argument("--max-images", type=int, default=600)
    p.add_argument("--out", required=True)
    p = sub.add_parser("triage")
    p.add_argument("--cases", required=True)
    p.add_argument("--reference", required=True)
    p.add_argument("--frames-root", default="")
    p.add_argument("--frame-ext", default="png")
    p.add_argument("--out", required=True)
    p = sub.add_parser("export-yolo")
    p.add_argument("--triage", required=True)
    p.add_argument("--names-from", required=True, help="model_spec.json (class order)")
    p.add_argument("--include", nargs="+", default=["escape", "false_reject", "hold_ng", "hold_ok", "near_threshold"])
    p.add_argument("--labels-from", default="", help="reviewed YOLO labels per source image; default: model detections as candidates")
    p.add_argument("--source-map", default="", help="TEST ONLY: simulator request CSV to map seq -> source image")
    p.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    return {"reference": cmd_reference, "triage": cmd_triage, "export-yolo": cmd_export}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
