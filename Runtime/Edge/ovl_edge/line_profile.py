"""Line parameter templates: one judgment rule per line, built from shared templates.

    profiles/templates/<name>.json   {"extends": "<parent>" (optional), "class_names": [...], "rule": {...}}
    profiles/lines/<line>.json       {"extends": "<template>", "line": "L2", "product": "...", "rule": {... only what differs ...}}

render() walks the extends chain from the root template to the line file and
merges the "rule" objects (later wins; class_min_score is merged per class), then
validates the result and writes the rule file the worker and the release
registry use. The rendered file records its source chain and their hashes, so a
rule in production can be traced back to the template and line edits that made it.

A rendered rule is a judgment change like a new model: register it as a new
release and take it through shadow, review and the gate.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from ovl_edge.decision import DecisionRule

RULE_KEYS = {"ng_classes", "min_score", "min_count", "no_detection_verdict", "class_min_score", "roi_xyxy", "min_box_area"}


def _load(path: Path) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _merge(base: dict, override: dict) -> dict:
    out = dict(base)
    for key, value in override.items():
        if key == "class_min_score":
            merged = dict(out.get("class_min_score") or {})
            merged.update({str(k): v for k, v in (value or {}).items()})
            out[key] = merged
        else:
            out[key] = value
    return out


def chain(root: Path, line: str) -> list[Path]:
    """Files from the root template down to the line file."""
    root = Path(root)
    files = [root / "lines" / f"{line}.json"]
    seen = set()
    data = _load(files[0])
    while data.get("extends"):
        name = data["extends"]
        if name in seen:
            raise ValueError(f"template cycle at {name}")
        seen.add(name)
        path = root / "templates" / f"{name}.json"
        files.insert(0, path)
        data = _load(path)
    return files


def validate(rule: dict, class_names: list[str] | None) -> list[str]:
    problems = []
    unknown = set(rule) - RULE_KEYS
    if unknown:
        problems.append(f"unknown rule keys {sorted(unknown)}")
    if rule.get("no_detection_verdict") not in ("Pass", "Ng"):
        problems.append("no_detection_verdict must be Pass or Ng (design E8)")
    if not 0 < float(rule.get("min_score", 0.25)) < 1:
        problems.append("min_score must be between 0 and 1")
    n = len(class_names) if class_names else None
    for c in rule.get("ng_classes", []):
        if n is not None and not 0 <= int(c) < n:
            problems.append(f"ng_classes has class {c}, model has {n}")
    for c, s in (rule.get("class_min_score") or {}).items():
        if n is not None and not 0 <= int(c) < n:
            problems.append(f"class_min_score has class {c}, model has {n}")
        if not 0 < float(s) < 1:
            problems.append(f"class_min_score[{c}]={s} must be between 0 and 1")
    roi = rule.get("roi_xyxy")
    if roi is not None and (len(roi) != 4 or not (0 <= roi[0] < roi[2] and 0 <= roi[1] < roi[3])):
        problems.append(f"roi_xyxy {roi} must be [x1, y1, x2, y2] with x1 < x2, y1 < y2, non-negative")
    if float(rule.get("min_box_area", 0)) < 0:
        problems.append("min_box_area must be >= 0")
    if int(rule.get("min_count", 1)) < 1:
        problems.append("min_count must be >= 1")
    return problems


def render(root: Path, line: str) -> dict:
    files = chain(root, line)
    rule: dict = {}
    class_names = None
    meta = {}
    for path in files:
        data = _load(path)
        rule = _merge(rule, data.get("rule", {}))
        class_names = data.get("class_names", class_names)
        meta.update({k: data[k] for k in ("line", "product") if k in data})
    problems = validate(rule, class_names)
    if problems:
        raise ValueError(f"line {line}: " + "; ".join(problems))
    if DecisionRule.from_dict(rule) is None:
        raise ValueError(f"line {line}: rule does not parse")
    sources = [{"file": f"{p.parent.name}/{p.name}", "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]
    return rule | {"_profile": meta | {"sources": sources, "class_names": class_names}}


def diff(old: dict, new: dict) -> list[str]:
    """Human-readable differences between two rendered rules (judgment keys only)."""
    out = []
    for key in sorted(RULE_KEYS):
        a, b = old.get(key), new.get(key)
        if key == "class_min_score":
            a, b = a or {}, b or {}
            for c in sorted(set(a) | set(b), key=int):
                if a.get(c) != b.get(c):
                    out.append(f"class_min_score[{c}]: {a.get(c, '(min_score)')} -> {b.get(c, '(min_score)')}")
        elif a != b:
            out.append(f"{key}: {a} -> {b}")
    return out
