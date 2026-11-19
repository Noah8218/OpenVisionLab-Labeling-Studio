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
WORKER_RESULTS = Counter("ovl_worker_results_total", "Results written by the worker", ["code", "runtime"])
WORKER_ERRORS = Counter("ovl_worker_errors_total", "Worker errors by kind", ["kind"])
# A stored result written again without re-inference: after a failed write
# (write_retry) or when the requester re-raised pending for the same request (repeat_request).
WORKER_RESULT_REWRITES = Counter("ovl_worker_result_rewrites_total", "Stored results written again without re-inference", ["reason"])
WORKER_INFO = Gauge("ovl_worker_info", "Static worker identity", ["runtime", "engine_sha256_prefix", "model_spec_sha256_prefix", "rule_sha256_prefix"])

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
INSPECTION_BUDGET_SECONDS = Gauge("ovl_inspection_budget_seconds", "Configured inspection time budget")
INSPECTION_BUDGET_SECONDS.set(BUDGET_MS / 1000.0)
