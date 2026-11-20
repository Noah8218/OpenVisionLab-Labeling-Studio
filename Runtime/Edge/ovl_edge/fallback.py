"""Fallback chain: keep judging when the primary inference path fails.

Tiers are tried in order, for example TensorRT -> ONNX Runtime (same ONNX
model, same accuracy, slower) -> rule-based OpenCV inspection (much weaker).

  - Per frame: if the active tier raises, the same frame is retried on the next
    tier, so one failure never costs a verdict.
  - Switching: after `fail_threshold` consecutive failures the next tier becomes
    active, so later frames stop paying for a broken tier first.
  - Recovery: while degraded, every `probe_interval_s` the frame is first tried
    on the higher tiers; the first one that succeeds becomes active again.
  - Every verdict names the tier and its model/rule hash, and the owner sees each
    switch through on_switch (metrics, logs). Running degraded is never silent.

The rule tier cannot clear parts reliably (it misses about half of the defects
on the synthetic multishape test set), so by default its Pass becomes HOLD
(manual inspection): defects are not released, at the cost of holding good
parts while degraded. That is a plant policy; `rule_pass_verdict` sets it.
"""

from __future__ import annotations

import logging
import time
from dataclasses import dataclass, field
from typing import Callable

import numpy as np

from ovl_edge.decision import Decision, DecisionRule, ResultCode, decide
from ovl_edge.rule_inspector import RuleParams, inspect

LOG = logging.getLogger("ovl_edge.fallback")


@dataclass(frozen=True)
class Verdict:
    decision: Decision
    tier: str
    sha: str  # hex: engine / ONNX / rule-parameter hash of the tier that judged
    stage_ms: dict[str, float] = field(default_factory=dict)
    detections: object | None = None  # postprocess.Detections from a model tier (case capture, pre-annotation)


@dataclass
class Tier:
    name: str
    sha: str
    judge: Callable[[np.ndarray], tuple[Decision, dict[str, float]]]
    close: Callable[[], None] | None = None  # frees GPU buffers when a release is retired


class AllTiersFailed(RuntimeError):
    pass


def model_tier(name: str, sha: str, detector, rule: DecisionRule | None) -> Tier:
    def judge(image: np.ndarray):
        detections, times = detector.detect(image)
        stage_ms = {"preprocess": times.preprocess_ms, "infer": times.infer_ms, "postprocess": times.postprocess_ms}
        return decide(detections, rule), stage_ms, detections

    return Tier(name, sha, judge, getattr(getattr(detector, "runner", None), "close", None))


def rule_tier(params: RuleParams, rule_pass_verdict: str = "Hold") -> Tier:
    if rule_pass_verdict not in ("Hold", "Pass"):
        raise ValueError("rule_pass_verdict must be Hold or Pass")

    def judge(image: np.ndarray):
        t0 = time.perf_counter()
        result = inspect(image, params)
        code = result.code
        if code == ResultCode.PASS and rule_pass_verdict == "Hold":
            code = ResultCode.HOLD
        return Decision(code, 0, int(result.code == ResultCode.NG), -1, 0.0), {"rule": (time.perf_counter() - t0) * 1e3}

    return Tier("rule", params.digest(), judge)


class FaultInjector:
    """Test aid: make a tier raise during [start_s, start_s + duration_s) after its first call.

    Time based, not call based: once a tier is bypassed it is only called by
    recovery probes, so a call-count window would never end.
    """

    def __init__(self, tier: Tier, start_s: float, duration_s: float, clock: Callable[[], float] = time.monotonic):
        self.tier, self.start_s, self.duration_s, self.clock = tier, start_s, duration_s, clock
        self.t_first: float | None = None
        inner = tier.judge

        def judge(image):
            now = self.clock()
            if self.t_first is None:
                self.t_first = now
            elapsed = now - self.t_first
            if self.start_s <= elapsed < self.start_s + self.duration_s:
                raise RuntimeError(f"injected fault in {tier.name} at {elapsed:.1f} s")
            return inner(image)

        tier.judge = judge


class FallbackChain:
    def __init__(
        self,
        tiers: list[Tier],
        primary: str,
        fail_threshold: int = 3,
        probe_interval_s: float = 30.0,
        clock: Callable[[], float] = time.monotonic,
        on_switch: Callable[[str, str, str], None] | None = None,
    ):
        if not tiers:
            raise ValueError("no tier could be loaded")
        self.tiers = tiers
        self.primary = primary  # configured first choice; may be missing if it failed to load
        self.fail_threshold = max(1, fail_threshold)
        self.probe_interval_s = probe_interval_s
        self.clock = clock
        self.on_switch = on_switch
        self.active = 0
        self.consecutive_failures = [0] * len(tiers)
        self._last_probe = clock()

    def close(self) -> None:
        for tier in self.tiers:
            if tier.close:
                try:
                    tier.close()
                except Exception as exc:  # freeing must not take the worker down
                    LOG.warning("closing tier %s failed: %s", tier.name, exc)

    @property
    def active_tier(self) -> Tier:
        return self.tiers[self.active]

    @property
    def degraded(self) -> bool:
        return self.active_tier.name != self.primary

    def _switch(self, to: int, reason: str) -> None:
        old = self.tiers[self.active].name
        self.active = to
        self._last_probe = self.clock()
        LOG.warning("inference tier %s -> %s (%s)", old, self.tiers[to].name, reason)
        if self.on_switch:
            self.on_switch(old, self.tiers[to].name, reason)

    def judge(self, image: np.ndarray) -> Verdict:
        start = self.active
        if self.active > 0 and self.clock() - self._last_probe >= self.probe_interval_s:
            self._last_probe = self.clock()
            start = 0  # probe the higher tiers with this frame
        for index in range(start, len(self.tiers)):
            tier = self.tiers[index]
            try:
                result = tier.judge(image)  # (decision, stage_ms) or (decision, stage_ms, detections)
            except Exception as exc:  # a tier failure is handled here, never raised to the caller
                self.consecutive_failures[index] += 1
                LOG.debug("tier %s failed: %s", tier.name, exc)
                if index == self.active and self.consecutive_failures[index] >= self.fail_threshold and index + 1 < len(self.tiers):
                    self._switch(index + 1, f"{self.consecutive_failures[index]} consecutive failures: {exc}")
                continue
            self.consecutive_failures[index] = 0
            if index < self.active:
                self._switch(index, "probe succeeded")
            return Verdict(result[0], tier.name, tier.sha, result[1], result[2] if len(result) > 2 else None)
        raise AllTiersFailed("every inference tier failed for this frame")
