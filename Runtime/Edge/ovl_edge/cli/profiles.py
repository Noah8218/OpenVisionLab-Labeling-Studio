"""Line profiles: render, compare, and propose per-class thresholds (ovl_edge.line_profile).

    python -m ovl_edge.cli.profiles render --root profiles --line L1 --out rule-L1.json
    python -m ovl_edge.cli.profiles diff old-rule.json new-rule.json
    python -m ovl_edge.cli.profiles tune --model-dir /model --images /data/images --labels /data/labels \
        --tune-list /data/lists/val.txt --check-list /data/lists/test.txt --base rule-L1.json --out rule-L1-tuned.json

tune runs the model once over the tuning images (ONNX Runtime CUDA), then for
each defect class searches the threshold that gives the fewest false rejects
without more escapes than the base rule on the tuning set (coordinate search,
two passes). It only proposes: the tuned rule goes through shadow, review and
the gate like any release. The check split is reported, never used for choosing.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np

from ovl_edge.decision import DecisionRule, ResultCode, decide
from ovl_edge.line_profile import diff, render
from ovl_edge.postprocess import Detections

GRID = [round(float(x), 2) for x in np.arange(0.05, 0.91, 0.05)]  # plain floats: written to JSON


def detect_all(model_dir: Path, images: str, labels: str, list_file: str):
    import cv2

    from ovl_edge.runners import Detector, OrtRunner
    from ovl_edge.spec import ModelSpec
    from ovl_edge.splits import read_list

    spec = ModelSpec.load(model_dir / "model_spec.json")
    detector = Detector(spec, OrtRunner(str(model_dir / spec.onnx_file), "cuda"))
    out = []
    for path in read_list(images, list_file):
        label = Path(labels) / (path.stem + ".txt")
        truth_ng = label.exists() and bool(label.read_text(encoding="utf-8").strip())
        dets, _ = detector.detect(cv2.imread(str(path), cv2.IMREAD_COLOR))
        out.append((path.name, truth_ng, dets))
    return out


def evaluate(samples, rule: DecisionRule) -> dict:
    esc = fr = ng = ok = 0
    for _, truth_ng, dets in samples:
        code = decide(dets, rule).code
        if truth_ng:
            ng += 1
            esc += code == ResultCode.PASS
        else:
            ok += 1
            fr += code == ResultCode.NG
    return {"escapes": esc, "ng": ng, "false_rejects": fr, "ok": ok}


def with_thresholds(base: dict, table: dict[int, float]) -> dict:
    """Only classes that differ from min_score are written, so an unchanged rule keeps its digest."""
    default = float(base.get("min_score", 0.25))
    out = {k: v for k, v in base.items() if k != "class_min_score"}
    entries = {str(c): v for c, v in sorted(table.items()) if abs(v - default) > 1e-9}
    if entries:
        out["class_min_score"] = entries
    return out


def tune(samples, base: dict, classes: list[int], max_escape_increase: int, min_gain: int = 3) -> tuple[dict, list]:
    """min_gain: a class threshold changes only if that change removes at least this many false
    rejects on the tuning set. On 2026-11-20 a change justified by a single image (min_gain 1)
    removed 1 false reject on val and added 2 escapes on test."""
    base_rule = DecisionRule.from_dict(base)
    limit = evaluate(samples, base_rule)["escapes"] + max_escape_increase
    table = {c: float(base.get("class_min_score", {}).get(str(c), base_rule.min_score)) for c in classes}
    trace = []
    for _ in range(2):
        for c in classes:
            current = evaluate(samples, DecisionRule.from_dict(with_thresholds(base, table)))
            best = (current, table[c])
            for t in GRID:
                trial = {**table, c: t}
                r = evaluate(samples, DecisionRule.from_dict(with_thresholds(base, trial)))
                if r["escapes"] <= limit and (r["false_rejects"], abs(t - base_rule.min_score)) < (best[0]["false_rejects"], abs(best[1] - base_rule.min_score)):
                    best = (r, t)
            gain = current["false_rejects"] - best[0]["false_rejects"]
            accepted = bool(best[1] != table[c] and gain >= min_gain)
            if accepted:
                table[c] = best[1]
            trace.append({"class": c, "candidate": best[1], "gain": gain, "accepted": accepted, **best[0]})
    return with_thresholds(base, table), trace


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("render")
    p.add_argument("--root", required=True)
    p.add_argument("--line", required=True)
    p.add_argument("--out", required=True)
    p = sub.add_parser("diff")
    p.add_argument("old")
    p.add_argument("new")
    p = sub.add_parser("tune")
    p.add_argument("--model-dir", required=True)
    p.add_argument("--images", required=True)
    p.add_argument("--labels", required=True)
    p.add_argument("--tune-list", required=True)
    p.add_argument("--check-list", default="")
    p.add_argument("--base", required=True, help="rendered rule to start from")
    p.add_argument("--max-escape-increase", type=int, default=0)
    p.add_argument("--min-gain", type=int, default=3, help="false rejects a class change must remove on the tuning set")
    p.add_argument("--out", required=True)
    args = parser.parse_args(argv)

    if args.cmd == "render":
        rule = render(Path(args.root), args.line)
        Path(args.out).write_text(json.dumps(rule, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"line": args.line, "digest": DecisionRule.from_dict(rule).digest()[:16], "sources": [s["file"] for s in rule["_profile"]["sources"]]}))
        return 0
    if args.cmd == "diff":
        old, new = (json.loads(Path(p).read_text(encoding="utf-8")) for p in (args.old, args.new))
        print("\n".join(diff(old, new)) or "no judgment changes")
        return 0

    base = json.loads(Path(args.base).read_text(encoding="utf-8"))
    model_dir = Path(args.model_dir)
    names = json.loads((model_dir / "model_spec.json").read_text(encoding="utf-8"))["class_names"]
    classes = [int(c) for c in base.get("ng_classes", [])] or list(range(len(names)))
    tune_set = detect_all(model_dir, args.images, args.labels, args.tune_list)
    tuned, trace = tune(tune_set, base, classes, args.max_escape_increase, args.min_gain)
    report = {"tune": {"base": evaluate(tune_set, DecisionRule.from_dict(base)), "tuned": evaluate(tune_set, DecisionRule.from_dict(tuned))}}
    if args.check_list:
        check_set = detect_all(model_dir, args.images, args.labels, args.check_list)
        report["check"] = {"base": evaluate(check_set, DecisionRule.from_dict(base)), "tuned": evaluate(check_set, DecisionRule.from_dict(tuned))}
    tuned["_tuning"] = {"tune_list": args.tune_list, "check_list": args.check_list or None, "max_escape_increase": args.max_escape_increase,
                        "min_gain": args.min_gain,
                        "class_names": names, "report": report, "trace": trace}
    Path(args.out).write_text(json.dumps(tuned, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=1))
    print("\n".join(diff(base, tuned)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
