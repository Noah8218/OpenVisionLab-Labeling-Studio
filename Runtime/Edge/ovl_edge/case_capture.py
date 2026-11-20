"""Field feedback, step 1: capture the frames worth a human look.

The worker records a case when
  - near_threshold: the top detection score is within `near_band` of the rule's
    min_score (a weak defect that may have been missed, or a marginal Ng),
  - hold / model_error / fallback: the verdict did not come from the primary model.
The requester side reports confirmed wrong verdicts separately (feedback.jsonl:
escape = truth Ng judged Pass, false_reject = truth OK judged Ng), the way a
plant's re-inspection and audit results arrive. ovl_edge.cli.cases joins both.

Captures are written by a background thread (frame bytes + JSON with the
verdict, detections and model hash) so disk speed never adds to the inspection
time. A per-minute limit and a total limit keep a drifting line from filling the
disk; skipped captures are counted.

Layout: <root>/<YYYYMMDD>/<session:08x>-<seq:010d>.<ext> and .json
"""

from __future__ import annotations

import json
import logging
import queue
import threading
import time
from collections import deque
from dataclasses import dataclass
from pathlib import Path

from prometheus_client import Counter

LOG = logging.getLogger("ovl_edge.case_capture")

CASES_CAPTURED = Counter("ovl_cases_captured_total", "Frames captured for review", ["reason"])
CASES_SKIPPED = Counter("ovl_cases_skipped_total", "Captures skipped", ["why"])


@dataclass(frozen=True)
class CapturePolicy:
    near_band: float = 0.10
    capture_fallback: bool = True
    max_per_minute: int = 120
    max_total: int = 20000


def case_reasons(top_score: float, min_score: float, code: int, tier: str, primary: str, policy: CapturePolicy) -> list[str]:
    from ovl_edge.decision import ResultCode

    reasons = []
    if top_score > 0 and abs(top_score - min_score) <= policy.near_band:
        reasons.append("near_threshold")
    if code == ResultCode.HOLD:
        reasons.append("hold")
    elif code == ResultCode.MODEL_ERROR:
        reasons.append("model_error")
    if policy.capture_fallback and tier and primary and tier != primary and "hold" not in reasons:
        reasons.append("fallback")
    return reasons


class CaseRecorder:
    def __init__(self, root: Path, policy: CapturePolicy = CapturePolicy(), clock=time.time):
        self.root = Path(root)
        self.policy = policy
        self.clock = clock
        self._recent: deque[float] = deque()
        self._total = sum(1 for _ in self.root.glob("*/*.json")) if self.root.exists() else 0
        self._queue: queue.Queue = queue.Queue(maxsize=256)
        self._thread = threading.Thread(target=self._run, name="case-writer", daemon=True)

    def start(self) -> "CaseRecorder":
        self._thread.start()
        return self

    def offer(self, reasons: list[str], frame: bytes, ext: str, record: dict) -> bool:
        """Queue a capture if a reason applies and the limits allow it. Never blocks."""
        if not reasons:
            return False
        now = self.clock()
        while self._recent and now - self._recent[0] > 60:
            self._recent.popleft()
        if len(self._recent) >= self.policy.max_per_minute:
            CASES_SKIPPED.labels("rate_limit").inc()
            return False
        if self._total >= self.policy.max_total:
            CASES_SKIPPED.labels("store_full").inc()
            return False
        try:
            self._queue.put_nowait((reasons, frame, ext, record | {"reasons": reasons, "captured_unix": now}))
        except queue.Full:
            CASES_SKIPPED.labels("writer_busy").inc()
            return False
        self._recent.append(now)
        self._total += 1
        for reason in reasons:
            CASES_CAPTURED.labels(reason).inc()
        return True

    def write(self, frame: bytes, ext: str, record: dict) -> Path:
        day = time.strftime("%Y%m%d", time.gmtime(record["captured_unix"]))
        stem = f"{record['session']:08x}-{record['seq']:010d}"
        folder = self.root / day
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{stem}.{ext}").write_bytes(frame)
        tmp = folder / f".{stem}.json.tmp"
        tmp.write_text(json.dumps(record | {"frame": f"{stem}.{ext}"}, ensure_ascii=False), encoding="utf-8")
        tmp.replace(folder / f"{stem}.json")
        return folder / f"{stem}.json"

    def _run(self) -> None:
        while True:
            _, frame, ext, record = self._queue.get()
            try:
                self.write(frame, ext, record)
            except OSError as exc:  # a full or missing disk must not stop inspection
                CASES_SKIPPED.labels("write_error").inc()
                LOG.warning("case write failed: %s", exc)
