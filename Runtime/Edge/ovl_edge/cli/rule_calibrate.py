"""Fit the rule-based fallback on the training split, report it on the test split.

Selection policy (fixed before looking at test results): among all parameter
combinations, keep those whose training false-reject rate (OK judged Ng) is at
most --max-false-reject, then take the lowest training escape rate (Ng judged
OK); ties go to the lower false-reject rate. The chosen parameters are then run
once on the test split, with per-defect-class recall.

    python -m ovl_edge.cli.rule_calibrate --images /data/images --labels /data/labels \
        --train-list /data/lists/train.txt --test-list /data/lists/test.txt --out /out/rule_calibration.json
"""

from __future__ import annotations

import argparse
import itertools
import json
import sys
import time
from dataclasses import asdict, replace
from pathlib import Path

import cv2
import numpy as np

from ovl_edge.decision import ResultCode
from ovl_edge.rule_inspector import RuleParams, deviation, inspect, largest_area, part_mask
from ovl_edge.splits import read_list


def label_classes(labels_dir: Path, image: Path) -> list[int]:
    path = labels_dir / (image.stem + ".txt")
    lines = path.read_text(encoding="utf-8").split("\n") if path.exists() else []
    return sorted({int(line.split()[0]) for line in lines if line.strip()})


def rates(areas_ok: np.ndarray, areas_ng: np.ndarray, min_area: int) -> tuple[float, float]:
    false_reject = float((areas_ok >= min_area).mean()) if len(areas_ok) else 0.0
    escape = float((areas_ng < min_area).mean()) if len(areas_ng) else 0.0
    return false_reject, escape


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--images", required=True)
    parser.add_argument("--labels", required=True)
    parser.add_argument("--train-list", required=True)
    parser.add_argument("--test-list", required=True)
    parser.add_argument("--max-false-reject", type=float, default=0.05)
    parser.add_argument("--median", type=int, nargs="+", default=[15, 21, 31])
    parser.add_argument("--erode", type=int, nargs="+", default=[3, 7])
    parser.add_argument("--thresholds", type=int, nargs="+", default=list(range(10, 65, 5)))
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    labels_dir = Path(args.labels)

    def load(list_file: str):
        items = []
        for path in read_list(args.images, list_file):
            gray = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
            items.append((path, gray, label_classes(labels_dir, path)))
        return items

    train = load(args.train_list)
    candidates = []
    for median, erode in itertools.product(args.median, args.erode):
        base = RuleParams(median_px=median, erode_px=erode)
        feats = [(deviation(g, base), part_mask(g, base), bool(c)) for _, g, c in train]
        for threshold in args.thresholds:
            areas = np.array([largest_area(d, m, threshold) for d, m, _ in feats])
            is_ng = np.array([ng for _, _, ng in feats])
            ok_areas, ng_areas = areas[~is_ng], areas[is_ng]
            for min_area in sorted(set(int(a) for a in areas if a > 0)):
                fr, esc = rates(ok_areas, ng_areas, min_area)
                if fr <= args.max_false_reject:
                    candidates.append((esc, fr, replace(base, diff_threshold=threshold, min_area=min_area)))
    if not candidates:
        raise SystemExit("no parameter combination meets the false-reject limit on the training split")
    candidates.sort(key=lambda c: (c[0], c[1]))
    train_escape, train_fr, params = candidates[0]

    test = load(args.test_list)
    per_class: dict[int, list[int]] = {}
    ok_total = ok_rejected = ng_total = ng_escaped = 0
    elapsed = []
    for path, gray, classes in test:
        t0 = time.perf_counter()
        result = inspect(gray, params)
        elapsed.append((time.perf_counter() - t0) * 1e3)
        is_ng_pred = result.code == ResultCode.NG
        if classes:
            ng_total += 1
            ng_escaped += int(not is_ng_pred)
            for c in classes:
                per_class.setdefault(c, [0, 0])
                per_class[c][0] += 1
                per_class[c][1] += int(is_ng_pred)
        else:
            ok_total += 1
            ok_rejected += int(is_ng_pred)

    report = {
        "policy": {"max_false_reject_train": args.max_false_reject, "objective": "min train escape, then min false reject"},
        "grid": {"median": args.median, "erode": args.erode, "thresholds": args.thresholds},
        "params": asdict(params),
        "params_sha256": params.digest(),
        "train": {"images": len(train), "false_reject": train_fr, "escape": train_escape},
        "test": {
            "images": len(test),
            "ok": ok_total, "false_rejects": ok_rejected, "false_reject_rate": ok_rejected / max(ok_total, 1),
            "ng": ng_total, "escapes": ng_escaped, "escape_rate": ng_escaped / max(ng_total, 1),
            "recall_by_class": {str(c): {"images": n, "detected": d, "recall": d / n} for c, (n, d) in sorted(per_class.items())},
            "ms_per_image": {"p50": float(np.percentile(elapsed, 50)), "p99": float(np.percentile(elapsed, 99))},
        },
    }
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"params": report["params"], "train": report["train"], "test": {k: v for k, v in report["test"].items() if k != "recall_by_class"}}, indent=1))
    print("recall_by_class", {c: round(v["recall"], 3) for c, v in report["test"]["recall_by_class"].items()})
    return 0


if __name__ == "__main__":
    sys.exit(main())
