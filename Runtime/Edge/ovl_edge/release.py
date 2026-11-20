"""Release registry: pinned model releases, the active pointer, shadow / canary, promotion gate, rollback.

Layout (one directory shared by the operator CLI and the worker):

    <root>/releases/<id>/release.json   id, runtime, fallback tiers, file hashes, note
    <root>/releases/<id>/package/       model package (model_spec.json, ONNX, engines/)
    <root>/releases/<id>/rule.json      judgment rule
    <root>/releases/<id>/rule_params.json  (optional) rule-based fallback parameters
    <root>/pointer.json                 active, candidate, mode, canary_percent, revision, history
    <root>/reports/shadow-<id>.json     written by the worker while <id> runs in shadow
    <root>/reports/disagreements-<id>.jsonl

Rules kept here:
  - A release is immutable: register copies the package, hashes every file, and
    refuses an existing id. The worker verifies the hashes before loading.
  - pointer.json is the only switch. It is replaced atomically (temp file +
    os.replace), so a reader never sees half a file, and every change is appended
    to its history (who/when/what), which is the change record for audits.
  - Promotion goes through gate(): enough shadow comparisons, high agreement,
    no "active Ng / candidate Pass" flips (each one is a potential escape), and
    candidate latency within budget. --force exists but is recorded as forced.
"""

from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import shutil
from dataclasses import asdict, dataclass, field
from pathlib import Path

from ovl_edge.spec import sha256_file

MODES = ("none", "shadow", "canary")


def _now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")


def write_json_atomic(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    os.replace(tmp, path)  # atomic on the same file system


@dataclass(frozen=True)
class Release:
    id: str
    runtime: str
    fallback: tuple[str, ...]
    files: dict  # relative path -> sha256
    note: str = ""
    created_utc: str = ""

    def dir(self, root: Path) -> Path:
        return Path(root) / "releases" / self.id

    def digest(self) -> str:
        return hashlib.sha256(json.dumps(self.files, sort_keys=True).encode()).hexdigest()


@dataclass
class Pointer:
    active: str | None = None
    candidate: str | None = None
    mode: str = "none"
    canary_percent: int = 0
    revision: int = 0
    history: list = field(default_factory=list)


def release_path(root: Path, rel_id: str) -> Path:
    return Path(root) / "releases" / rel_id


def load_release(root: Path, rel_id: str) -> Release:
    data = json.loads((release_path(root, rel_id) / "release.json").read_text(encoding="utf-8"))
    return Release(data["id"], data["runtime"], tuple(data.get("fallback", [])), data["files"], data.get("note", ""), data.get("created_utc", ""))


def verify_release(root: Path, release: Release) -> list[str]:
    """Paths whose bytes no longer match the registered hash (empty list = intact)."""
    base = release.dir(root)
    return [rel for rel, digest in release.files.items() if not (base / rel).exists() or sha256_file(base / rel) != digest]


def register(root: Path, rel_id: str, package: Path, rule: Path, runtime: str, fallback: list[str],
             rule_params: Path | None = None, note: str = "") -> Release:
    if not rel_id or "/" in rel_id or rel_id.startswith("."):
        raise ValueError(f"invalid release id {rel_id!r}")
    target = release_path(root, rel_id)
    if target.exists():
        raise FileExistsError(f"release {rel_id} already exists; releases are immutable, use a new id")
    staging = target.with_name(f".{rel_id}.staging")
    shutil.rmtree(staging, ignore_errors=True)
    shutil.copytree(package, staging / "package", ignore=shutil.ignore_patterns(".work", "*.log", "calibration.npy"))
    shutil.copyfile(rule, staging / "rule.json")
    if rule_params:
        shutil.copyfile(rule_params, staging / "rule_params.json")
    files = {p.relative_to(staging).as_posix(): sha256_file(p) for p in sorted(staging.rglob("*")) if p.is_file()}
    release = Release(rel_id, runtime, tuple(fallback), files, note, _now())
    write_json_atomic(staging / "release.json", asdict(release) | {"fallback": list(fallback)})
    os.replace(staging, target)
    return release


def read_pointer(root: Path) -> Pointer:
    path = Path(root) / "pointer.json"
    if not path.exists():
        return Pointer()
    data = json.loads(path.read_text(encoding="utf-8"))
    return Pointer(**{k: data[k] for k in Pointer.__dataclass_fields__ if k in data})


def _commit(root: Path, pointer: Pointer, action: str, note: str = "", **extra) -> Pointer:
    pointer.revision += 1
    pointer.history.append({"at": _now(), "revision": pointer.revision, "action": action, "active": pointer.active,
                            "candidate": pointer.candidate, "mode": pointer.mode, "canary_percent": pointer.canary_percent,
                            "note": note, **extra})
    write_json_atomic(Path(root) / "pointer.json", asdict(pointer))
    return pointer


def _require(root: Path, rel_id: str) -> Release:
    release = load_release(root, rel_id)
    bad = verify_release(root, release)
    if bad:
        raise ValueError(f"release {rel_id} failed its hash check: {bad[:5]}")
    return release


def activate(root: Path, rel_id: str, note: str = "") -> Pointer:
    _require(root, rel_id)
    pointer = read_pointer(root)
    pointer.active, pointer.candidate, pointer.mode, pointer.canary_percent = rel_id, None, "none", 0
    return _commit(root, pointer, "activate", note)


def set_candidate(root: Path, rel_id: str, mode: str, canary_percent: int = 0, note: str = "") -> Pointer:
    if mode not in ("shadow", "canary"):
        raise ValueError("mode must be shadow or canary")
    if mode == "canary" and not 1 <= canary_percent <= 50:
        raise ValueError("canary_percent must be 1-50")
    _require(root, rel_id)
    pointer = read_pointer(root)
    if pointer.active is None or pointer.active == rel_id:
        raise ValueError("a candidate needs a different active release")
    pointer.candidate, pointer.mode, pointer.canary_percent = rel_id, mode, canary_percent if mode == "canary" else 0
    return _commit(root, pointer, mode, note)


def clear_candidate(root: Path, note: str = "") -> Pointer:
    pointer = read_pointer(root)
    pointer.candidate, pointer.mode, pointer.canary_percent = None, "none", 0
    return _commit(root, pointer, "clear_candidate", note)


@dataclass(frozen=True)
class GateCriteria:
    min_compared: int = 1000
    min_agreement: float = 0.99
    max_ng_to_pass: int = 0  # active Ng, candidate Pass: each one is a potential escape
    max_candidate_p99_ms: float = 50.0
    max_dropped_ratio: float = 0.05  # shadow jobs skipped because the shadow queue was full


def gate(report: dict | None, criteria: GateCriteria) -> list[str]:
    """Reasons the candidate may not be promoted (empty list = pass)."""
    if not report:
        return ["no shadow report for the candidate"]
    reasons = []
    compared = int(report.get("compared", 0))
    if compared < criteria.min_compared:
        reasons.append(f"only {compared} shadow comparisons (need {criteria.min_compared})")
    if compared:
        agreement = report.get("agree", 0) / compared
        if agreement < criteria.min_agreement:
            reasons.append(f"agreement {agreement:.4f} < {criteria.min_agreement}")
    if report.get("ng_to_pass", 0) > criteria.max_ng_to_pass:
        reasons.append(f"{report['ng_to_pass']} cases active Ng -> candidate Pass (max {criteria.max_ng_to_pass})")
    p99 = report.get("candidate_ms", {}).get("p99")
    if p99 is None or p99 > criteria.max_candidate_p99_ms:
        reasons.append(f"candidate p99 {p99} ms over {criteria.max_candidate_p99_ms} ms")
    submitted = compared + int(report.get("dropped", 0))
    if submitted and report.get("dropped", 0) / submitted > criteria.max_dropped_ratio:
        reasons.append(f"{report['dropped']} of {submitted} shadow jobs dropped")
    return reasons


def shadow_report_path(root: Path, rel_id: str) -> Path:
    return Path(root) / "reports" / f"shadow-{rel_id}.json"


def promote(root: Path, criteria: GateCriteria, force: bool = False, note: str = "", review: Path | None = None) -> Pointer:
    """Promote the candidate if the gate passes.

    Overriding a failed gate needs a review file: the shadow disagreements checked
    against ground truth (which side was right). Its content and hash go into the
    history. Without it --force is refused: a 2026-11-20 test promoted on --force
    alone, and the later review showed the candidate added 3 escapes.
    """
    pointer = read_pointer(root)
    if not pointer.candidate:
        raise ValueError("no candidate to promote")
    path = shadow_report_path(root, pointer.candidate)
    report = json.loads(path.read_text(encoding="utf-8")) if path.exists() else None
    reasons = gate(report, criteria)
    if reasons and not force:
        raise PermissionError("promotion gate failed: " + "; ".join(reasons))
    review_record = None
    if reasons:
        if review is None or not Path(review).is_file():
            raise ValueError("overriding the gate needs --review <file> with the disagreement review against ground truth")
        content = json.loads(Path(review).read_text(encoding="utf-8"))
        # Shadow keeps running while people review; the review must cover every disagreement
        # in the report as it stands now (a 2026-11-20 test promoted with 14 of 145 unreviewed).
        disagreements = sum(int((report or {}).get(k, 0)) for k in ("ng_to_pass", "pass_to_ng", "other"))
        reviewed = int(content.get("reviewed_disagreements", -1))
        if reviewed < disagreements:
            raise ValueError(f"review covers {reviewed if reviewed >= 0 else 'an unknown number of'} disagreements, "
                             f"the shadow report now has {disagreements}; review again before promoting")
        review_record = {"file": Path(review).name, "sha256": sha256_file(review), "content": content}
    previous = pointer.active
    pointer.active, pointer.candidate, pointer.mode, pointer.canary_percent = pointer.candidate, None, "none", 0
    return _commit(root, pointer, "promote", note, previous=previous, gate=asdict(criteria),
                   gate_failures=reasons, forced=bool(reasons), shadow_report=report, review=review_record)


def rollback(root: Path, to: str | None = None, note: str = "") -> Pointer:
    """Return to `to`, or to the release that was active before the current one."""
    pointer = read_pointer(root)
    if to is None:
        for entry in reversed(pointer.history):
            if entry["action"] in ("promote", "rollback") and entry.get("previous") and entry["active"] == pointer.active:
                to = entry["previous"]
                break
    if not to:
        raise ValueError("no previous release in the history; pass --to")
    _require(root, to)
    previous = pointer.active
    pointer.active, pointer.candidate, pointer.mode, pointer.canary_percent = to, None, "none", 0
    return _commit(root, pointer, "rollback", note, previous=previous)
