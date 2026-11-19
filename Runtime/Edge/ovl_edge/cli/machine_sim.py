"""Stand-in for the Machine Studio side of the Modbus contract (local tests only).

This is NOT Machine Studio. It plays the server role of design E7 so the worker,
the register map and the p99 dashboard can be exercised on one PC:
  - serves the holding-register map (ovl_edge.modbus_map) over Modbus TCP
  - writes each frame into the shared input folder BEFORE issuing the request
    (frame acquisition time is outside the inspection span)
  - issues one pending request per camera, then waits for the matching result
  - applies the verdict on the next simulation tick and writes the applied block
  - times request -> applied with ONE monotonic clock and exports it to Prometheus

    python -m ovl_edge.cli.machine_sim --images /data/images --frames-root /frames --count 2000 --metrics-port 9101
"""

from __future__ import annotations

import argparse
import asyncio
import csv
import hashlib
import inspect
import logging
import math
import random
import sys
import time
from pathlib import Path

import cv2
from prometheus_client import start_http_server

from ovl_edge.decision import ResultCode
from ovl_edge.metrics_export import BUDGET_MS, INSPECTION_E2E_SECONDS, INSPECTION_OUTCOMES, INSPECTION_PHASE_SECONDS, INSPECTION_REQUESTS
from ovl_edge.modbus_map import APPLIED_BASE, REQUEST_BASE, RESULT_BASE, RESULT_SIZE, Request, Result, frame_relative_path, u32_to_regs
from ovl_edge.splits import list_images

LOG = logging.getLogger("ovl_edge.machine_sim")


def outcome_name(code: int) -> str:
    try:
        return "applied_" + ResultCode(code).name.lower() if code in (ResultCode.PASS, ResultCode.NG) else "worker_" + ResultCode(code).name.lower()
    except ValueError:
        return "worker_unknown_code"


async def _io(value):
    # RegisterServer (Modbus) is synchronous; OpcUaBlockStore returns awaitables.
    return await value if inspect.isawaitable(value) else value


async def drive(args, store) -> None:
    images = list_images(args.images)
    if not images:
        raise SystemExit("no images")
    session = args.session or random.SystemRandom().randint(1, 0xFFFFFFFF)
    frames_root = Path(args.frames_root)
    tick_s = args.tick_ms / 1000.0
    timeout_s = args.timeout_ms / 1000.0
    csv_path = Path(args.csv) if args.csv else None
    writer = None
    if csv_path:
        csv_path.parent.mkdir(parents=True, exist_ok=True)
        handle = open(csv_path, "w", newline="", encoding="utf-8")
        writer = csv.writer(handle)
        writer.writerow(["seq", "image", "outcome", "code", "e2e_ms", "result_wait_ms", "tick_wait_ms", "worker_service_us"])
    await asyncio.sleep(args.start_delay_s)
    sim_t0 = time.monotonic()
    last_commit = (await _io(store.get(RESULT_BASE, RESULT_SIZE)))[9]
    LOG.info("session %08x, %d requests, tick %.1f ms, budget %.1f ms", session, args.count, args.tick_ms, BUDGET_MS)
    for seq in range(1, args.count + 1):
        source = images[(seq - 1) % len(images)]
        frame = cv2.imread(str(source), cv2.IMREAD_COLOR)
        ok, encoded = cv2.imencode("." + args.frame_ext, frame)
        data = encoded.tobytes()
        path = frames_root / frame_relative_path(session, seq, args.frame_ext)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        prefix = hashlib.sha256(data).digest()[:8]

        request = Request(session=session, seq=seq, camera=1, pending=True, frame_sha_prefix=prefix)
        t_request = time.monotonic()
        await _io(store.set(REQUEST_BASE, request.encode()))
        INSPECTION_REQUESTS.inc()

        result = None
        while time.monotonic() - t_request < timeout_s:
            regs = await _io(store.get(RESULT_BASE, RESULT_SIZE))
            if regs[9] != last_commit:
                candidate = Result.decode(regs)
                last_commit = regs[9]
                if (candidate.session, candidate.seq) == (session, seq):
                    result = candidate
                    break
                INSPECTION_OUTCOMES.labels("stale_result").inc()  # previous session/request: rejected
            await asyncio.sleep(args.poll_ms / 1000.0)
        t_observed = time.monotonic()
        if result is None:
            outcome, code, service_us = "timeout", int(ResultCode.TIMEOUT), 0
            INSPECTION_OUTCOMES.labels(outcome).inc()
            await _io(store.set(REQUEST_BASE, Request(session, seq, 1, False, prefix).encode()))
            if writer:
                writer.writerow([seq, source.name, outcome, code, "", "", "", ""])
            continue
        if result.frame_sha_prefix != prefix:
            outcome = "frame_hash_mismatch"
        else:
            outcome = outcome_name(result.code)
        # Apply on the next simulation tick boundary.
        ticks = math.ceil((t_observed - sim_t0) / tick_s)
        t_tick = sim_t0 + ticks * tick_s
        delay = t_tick - time.monotonic()
        if delay > 0:
            await asyncio.sleep(delay)
        t_applied = time.monotonic()
        await _io(store.set(APPLIED_BASE, u32_to_regs(session) + u32_to_regs(seq)))
        await _io(store.set(REQUEST_BASE, Request(session, seq, 1, False, prefix).encode()))
        e2e = t_applied - t_request
        INSPECTION_E2E_SECONDS.observe(e2e)
        INSPECTION_PHASE_SECONDS.labels("result_wait").observe(t_observed - t_request)
        INSPECTION_PHASE_SECONDS.labels("tick_wait").observe(t_applied - t_observed)
        # Worker service is a duration, so subtracting it needs no shared clock.
        INSPECTION_PHASE_SECONDS.labels("transport_queue").observe(max(0.0, (t_observed - t_request) - result.service_us / 1e6))
        INSPECTION_OUTCOMES.labels(outcome).inc()
        if writer:
            writer.writerow([seq, source.name, outcome, result.code, f"{e2e * 1e3:.3f}", f"{(t_observed - t_request) * 1e3:.3f}", f"{(t_applied - t_observed) * 1e3:.3f}", result.service_us])
        if args.interval_ms:
            await asyncio.sleep(args.interval_ms / 1000.0)
    if writer:
        handle.close()
    LOG.info("done: %d requests", args.count)


async def amain(args) -> None:
    if args.transport == "opcua":
        from ovl_edge.opcua_server import OpcUaBlockStore

        store = OpcUaBlockStore()
        await store.start(args.host, args.opcua_port)
    else:
        from ovl_edge.modbus_server import RegisterServer

        store = RegisterServer(400)
        await store.start(args.host, args.port)
    try:
        await drive(args, store)
        if args.linger_s:
            await asyncio.sleep(args.linger_s)  # keep metrics available for the last scrape
    finally:
        await store.stop()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--images", required=True)
    parser.add_argument("--frames-root", required=True)
    parser.add_argument("--frame-ext", default="png", choices=["png", "bmp"])
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=5020)
    parser.add_argument("--transport", default="modbus", choices=["modbus", "opcua"])
    parser.add_argument("--opcua-port", type=int, default=4840)
    parser.add_argument("--count", type=int, default=1000)
    parser.add_argument("--session", type=int, default=0)
    parser.add_argument("--tick-ms", type=float, default=10.0)
    parser.add_argument("--timeout-ms", type=float, default=1000.0)
    parser.add_argument("--poll-ms", type=float, default=0.5)
    parser.add_argument("--interval-ms", type=float, default=0.0)
    parser.add_argument("--start-delay-s", type=float, default=5.0)
    parser.add_argument("--linger-s", type=float, default=30.0)
    parser.add_argument("--csv", default="")
    parser.add_argument("--metrics-port", type=int, default=9101)
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args(argv)
    logging.basicConfig(level=args.log_level, format="%(asctime)s %(levelname)s %(name)s %(message)s")
    start_http_server(args.metrics_port)
    asyncio.run(amain(args))
    return 0


if __name__ == "__main__":
    sys.exit(main())
