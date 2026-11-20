"""Worker side of the release registry: hot swap, canary routing and shadow comparison.

ReleaseManager
  A watcher thread reads pointer.json every `poll_s`. When the revision changes it
  loads the releases that are not loaded yet (in the watcher thread, while the
  worker keeps serving) and parks the result as the pending snapshot. Only the
  worker thread installs it, between two requests (apply_pending), so a request
  never sees its model change half way. Replaced releases are closed after
  `retire_after_s`, because a shadow job may still hold the old candidate.
  A release that fails to load (hash mismatch, engine refused) leaves the running
  snapshot in place; that pointer revision is reported and not retried.

ShadowRunner
  After the active result is written, the same image is judged by the candidate
  on a separate thread. Shadow verdicts never reach the line. The runner counts
  agreement, "active Ng / candidate Pass" flips and candidate latency, appends
  each disagreement to a JSONL file for review, and writes the report the
  promotion gate reads. A full shadow queue drops the job (counted) instead of
  delaying the next request.
"""

from __future__ import annotations

import json
import logging
import queue
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

import numpy as np

from ovl_edge.decision import ResultCode
from ovl_edge.fallback import FallbackChain
from ovl_edge.metrics_export import RELEASE_INFO, RELEASE_SWAPS, SHADOW_COMPARED, SHADOW_DROPPED, SHADOW_SECONDS
from ovl_edge.pipeline import percentile
from ovl_edge.release import Release, load_release, read_pointer, shadow_report_path, verify_release, write_json_atomic

LOG = logging.getLogger("ovl_edge.release_runtime")


@dataclass(frozen=True)
class Slot:
    release: Release
    chain: FallbackChain
    rule_sha: str
    min_score: float = 0.25  # judgment rule threshold (case capture uses it for the near-threshold band)


@dataclass(frozen=True)
class Snapshot:
    revision: int
    active: Slot
    candidate: Slot | None
    mode: str
    canary_percent: int

    def routes_to_candidate(self, seq: int) -> bool:
        """Deterministic canary split: the same seq always goes the same way."""
        return self.mode == "canary" and self.candidate is not None and (seq % 100) < self.canary_percent


class ReleaseManager:
    def __init__(self, root: Path, load: Callable[[Path, Release], Slot], poll_s: float = 2.0, retire_after_s: float = 10.0):
        self.root = Path(root)
        self.load = load
        self.poll_s = poll_s
        self.retire_after_s = retire_after_s
        self._current: Snapshot | None = None
        self._pending: Snapshot | None = None
        self._lock = threading.Lock()
        self._seen_revision = -1
        self._retired: list[tuple[float, Slot]] = []
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._watch, name="release-watch", daemon=True)

    def start(self) -> "ReleaseManager":
        self.poll_once()
        if not self.apply_pending():
            raise SystemExit(f"no loadable active release in {self.root / 'pointer.json'}")
        self._thread.start()
        return self

    def stop(self) -> None:
        self._stop.set()

    @property
    def current(self) -> Snapshot:
        return self._current  # type: ignore[return-value]

    def _slot(self, rel_id: str | None, reuse: dict[str, Slot]) -> Slot | None:
        if not rel_id:
            return None
        if rel_id in reuse:
            return reuse[rel_id]
        release = load_release(self.root, rel_id)
        bad = verify_release(self.root, release)
        if bad:
            raise ValueError(f"release {rel_id} files changed since registration: {bad[:3]}")
        t0 = time.perf_counter()
        slot = self.load(self.root, release)
        LOG.info("release %s loaded in %.1f s", rel_id, time.perf_counter() - t0)
        return slot

    def poll_once(self) -> None:
        pointer = read_pointer(self.root)
        if pointer.revision == self._seen_revision:
            return
        self._seen_revision = pointer.revision
        with self._lock:
            base = self._pending or self._current
        reuse = {}
        if base:
            reuse[base.active.release.id] = base.active
            if base.candidate:
                reuse[base.candidate.release.id] = base.candidate
        try:
            active = self._slot(pointer.active, reuse)
            if active is None:
                raise ValueError("pointer has no active release")
            candidate = self._slot(pointer.candidate, reuse) if pointer.mode in ("shadow", "canary") else None
        except (Exception, SystemExit) as exc:
            LOG.error("pointer revision %d not applied: %s", pointer.revision, exc)
            RELEASE_SWAPS.labels("load_failed").inc()
            return
        action = pointer.history[-1]["action"] if pointer.history else "activate"
        with self._lock:
            self._pending = Snapshot(pointer.revision, active, candidate, pointer.mode, pointer.canary_percent)
            self._pending_action = action

    def apply_pending(self) -> bool:
        """Worker thread only, between requests."""
        with self._lock:
            pending, self._pending = self._pending, None
            action = getattr(self, "_pending_action", "activate")
        if pending is None:
            return False
        old, self._current = self._current, pending
        if old:
            keep = {id(pending.active), id(pending.candidate)}
            for slot in (old.active, old.candidate):
                if slot is not None and id(slot) not in keep:
                    self._retired.append((time.monotonic(), slot))
        RELEASE_INFO.clear()
        RELEASE_INFO.labels(pending.active.release.id, pending.candidate.release.id if pending.candidate else "", pending.mode).set(1)
        RELEASE_SWAPS.labels(action).inc()
        LOG.warning("release revision %d installed: active=%s candidate=%s mode=%s", pending.revision, pending.active.release.id,
                    pending.candidate.release.id if pending.candidate else "-", pending.mode)
        return True

    def _close_retired(self) -> None:
        now = time.monotonic()
        keep = []
        for retired_at, slot in self._retired:
            if now - retired_at >= self.retire_after_s:
                slot.chain.close()
                LOG.info("release %s closed", slot.release.id)
            else:
                keep.append((retired_at, slot))
        self._retired = keep

    def _watch(self) -> None:
        while not self._stop.wait(self.poll_s):
            try:
                self.poll_once()
                self._close_retired()
            except Exception:  # the watcher must survive a bad pointer edit
                LOG.exception("release watcher error")


@dataclass
class _Stats:
    candidate: str
    active: str
    compared: int = 0
    agree: int = 0
    ng_to_pass: int = 0
    pass_to_ng: int = 0
    other: int = 0
    dropped: int = 0
    errors: int = 0

    def __post_init__(self):
        self.ms: list[float] = []


class ShadowRunner:
    def __init__(self, root: Path, queue_size: int = 8, report_every: int = 200, report_interval_s: float = 5.0):
        self.root = Path(root)
        self.jobs: queue.Queue = queue.Queue(maxsize=queue_size)
        self.report_every = report_every
        self.report_interval_s = report_interval_s
        self.stats: dict[str, _Stats] = {}
        self._last_report = 0.0
        self._thread = threading.Thread(target=self._run, name="shadow", daemon=True)

    def start(self) -> "ShadowRunner":
        self._thread.start()
        return self

    def _stats(self, candidate: str, active: str) -> _Stats:
        if candidate not in self.stats:
            self.stats[candidate] = _Stats(candidate, active)
        return self.stats[candidate]

    def submit(self, candidate: Slot, active_id: str, image: np.ndarray, active_code: int, session: int, seq: int, frame_sha: str) -> bool:
        try:
            self.jobs.put_nowait((candidate, active_id, image, active_code, session, seq, frame_sha))
            return True
        except queue.Full:
            self._stats(candidate.release.id, active_id).dropped += 1
            SHADOW_DROPPED.labels(candidate.release.id).inc()
            return False

    @staticmethod
    def classify(active_code: int, candidate_code: int) -> str:
        if active_code == candidate_code:
            return "agree"
        if active_code == ResultCode.NG and candidate_code == ResultCode.PASS:
            return "ng_to_pass"
        if active_code == ResultCode.PASS and candidate_code == ResultCode.NG:
            return "pass_to_ng"
        return "other"

    def run_job(self, job) -> str:
        candidate, active_id, image, active_code, session, seq, frame_sha = job
        rel_id = candidate.release.id
        stats = self._stats(rel_id, active_id)
        t0 = time.perf_counter()
        try:
            verdict = candidate.chain.judge(image)
        except Exception as exc:
            stats.errors += 1
            LOG.warning("shadow %s failed on %s/%s: %s", rel_id, session, seq, exc)
            return "error"
        ms = (time.perf_counter() - t0) * 1e3
        stats.ms.append(ms)
        SHADOW_SECONDS.labels(rel_id).observe(ms / 1e3)
        outcome = self.classify(int(active_code), int(verdict.decision.code))
        stats.compared += 1
        setattr(stats, outcome, getattr(stats, outcome) + 1)
        SHADOW_COMPARED.labels(rel_id, outcome).inc()
        if outcome != "agree":
            line = {"session": session, "seq": seq, "frame_sha_prefix": frame_sha, "active": active_id, "active_code": int(active_code),
                    "candidate": rel_id, "candidate_code": int(verdict.decision.code), "candidate_tier": verdict.tier,
                    "candidate_top_class": verdict.decision.top_class, "candidate_top_score": round(verdict.decision.top_score, 4), "outcome": outcome}
            path = self.root / "reports" / f"disagreements-{rel_id}.jsonl"
            path.parent.mkdir(parents=True, exist_ok=True)
            with open(path, "a", encoding="utf-8") as handle:
                handle.write(json.dumps(line) + "\n")
        now = time.monotonic()
        if stats.compared % self.report_every == 0 or now - self._last_report >= self.report_interval_s:
            self.write_report(rel_id)
            self._last_report = now
        return outcome

    def report(self, rel_id: str) -> dict:
        s = self.stats[rel_id]
        return {"candidate": s.candidate, "active": s.active, "compared": s.compared, "agree": s.agree, "ng_to_pass": s.ng_to_pass,
                "pass_to_ng": s.pass_to_ng, "other": s.other, "dropped": s.dropped, "errors": s.errors,
                "candidate_ms": {"p50": percentile(s.ms, 0.5), "p99": percentile(s.ms, 0.99)} if s.ms else {},
                "updated_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}

    def write_report(self, rel_id: str) -> None:
        write_json_atomic(shadow_report_path(self.root, rel_id), self.report(rel_id))

    def _run(self) -> None:
        while True:
            job = self.jobs.get()
            try:
                self.run_job(job)
            except Exception:
                LOG.exception("shadow job failed")
