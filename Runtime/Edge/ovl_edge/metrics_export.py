"""Prometheus metric definitions shared by the worker and the simulator.

Latency histograms use seconds (Prometheus convention). Bucket edges are dense
around the default inspection budget (OVL_BUDGET_MS, 50 ms) so the p99 estimate
near the budget is not dominated by bucket width. Request IDs never become
labels; they go to logs only (design M5).
"""

from __future__ import annotations

import os

from prometheus_client import Counter, Gauge, Histogram

BUDGET_MS = float(os.environ.get("OVL_BUDGET_MS", "50"))
_EDGES_MS = sorted({0.5, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 70, 80, 100, 150, 200, 300, 500, 1000, 2000, BUDGET_MS})
LATENCY_BUCKETS = tuple(ms / 1000.0 for ms in _EDGES_MS)

# Worker side (service time inside the container).
WORKER_STAGE_SECONDS = Histogram(
    "ovl_worker_stage_seconds",
    "Worker stage duration",
    ["stage", "runtime"],
    buckets=LATENCY_BUCKETS,
)
WORKER_RESULTS = Counter("ovl_worker_results_total", "Results written by the worker", ["code", "runtime", "tier"])
# Fallback chain (ovl_edge.fallback): which tier judges, whether that is not the configured primary, and each switch.
WORKER_ACTIVE_TIER = Gauge("ovl_worker_active_tier", "1 for the tier currently judging", ["tier"])
WORKER_DEGRADED = Gauge("ovl_worker_degraded", "1 while a fallback tier is active instead of the configured runtime")
WORKER_TIER_SWITCHES = Counter("ovl_worker_tier_switches_total", "Fallback tier switches", ["from_tier", "to_tier", "kind"])
WORKER_ERRORS = Counter("ovl_worker_errors_total", "Worker errors by kind", ["kind"])
# A stored result written again without re-inference: after a failed write
# (write_retry) or when the requester re-raised pending for the same request (repeat_request).
WORKER_RESULT_REWRITES = Counter("ovl_worker_result_rewrites_total", "Stored results written again without re-inference", ["reason"])
# Updated on every poll of the request slot: a worker that is up but stuck stops advancing it.
WORKER_HEARTBEAT = Gauge("ovl_worker_heartbeat_timestamp_seconds", "Unix time of the worker's last poll")
WORKER_INFO = Gauge("ovl_worker_info", "Static worker identity", ["runtime", "engine_sha256_prefix", "model_spec_sha256_prefix", "rule_sha256_prefix"])

# Release registry (ovl_edge.release_runtime): what runs, each installed pointer change, shadow comparison.
RELEASE_INFO = Gauge("ovl_release_info", "Installed release pointer (value 1)", ["active", "candidate", "mode"])
RELEASE_SWAPS = Counter("ovl_release_swaps_total", "Release pointer changes installed without restart", ["action"])
SHADOW_COMPARED = Counter("ovl_shadow_compared_total", "Shadow comparisons: candidate verdict vs active verdict", ["candidate", "outcome"])
SHADOW_DROPPED = Counter("ovl_shadow_dropped_total", "Shadow jobs skipped because the shadow queue was full", ["candidate"])
SHADOW_SECONDS = Histogram("ovl_shadow_seconds", "Candidate judge time in shadow", ["candidate"], buckets=LATENCY_BUCKETS)

# Requesting side (one monotonic clock: request issued -> verdict applied on the next tick).
INSPECTION_E2E_SECONDS = Histogram(
    "ovl_inspection_e2e_seconds",
    "Inspection latency from request to applied verdict (requester monotonic clock)",
    buckets=LATENCY_BUCKETS,
)
INSPECTION_PHASE_SECONDS = Histogram(
    "ovl_inspection_phase_seconds",
    "Requester-side phases: result_wait (request -> result observed), tick_wait (result observed -> applied), "
    "transport_queue (result_wait minus worker service: polling, register I/O and result detection)",
    ["phase"],
    buckets=LATENCY_BUCKETS,
)
INSPECTION_REQUESTS = Counter("ovl_inspection_requests_total", "Inspection requests issued")
INSPECTION_OUTCOMES = Counter("ovl_inspection_outcomes_total", "Inspection outcomes", ["outcome"])
# Verdict against ground truth, counted when the truth becomes known. In a plant that is the
# re-inspection of rejected parts (false rejects) and audit sampling / returns of passed parts
# (escapes), so escapes arrive late and only for a sample. The simulator knows the labels and
# reports every frame immediately. truth: ok | ng | unknown; verdict: pass | ng | hold | other.
INSPECTION_QUALITY = Counter("ovl_inspection_quality_total", "Applied verdicts by ground truth", ["verdict", "truth"])
INSPECTION_BUDGET_SECONDS = Gauge("ovl_inspection_budget_seconds", "Configured inspection time budget")
INSPECTION_BUDGET_SECONDS.set(BUDGET_MS / 1000.0)
