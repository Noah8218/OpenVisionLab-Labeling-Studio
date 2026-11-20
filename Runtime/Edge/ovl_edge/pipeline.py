"""Staged frame pipeline: worker threads per stage, bounded queues between stages.

A frame passes decode -> preprocess -> infer -> postprocess. Run sequentially,
one frame occupies the whole chain, so throughput is 1 / (sum of stage times).
With one thread per stage, stages work on different frames at the same time and
throughput is bounded by the slowest stage instead; per-frame latency does not
shrink and grows by any time spent waiting in a queue.

Rules this module keeps:
  - Queues are bounded. When the first queue is full a camera source cannot wait,
    so submit(block=False) drops the frame and reports it (no unbounded backlog).
  - A stage function receives the previous stage's value and returns a new one;
    it must not keep references to buffers it does not own. The TensorRT stage
    copies its output out of the pinned buffer for this reason (trt_runner.infer).
  - A stage that needs a thread-bound resource (one CUDA stream / execution
    context) runs with workers=1.
  - A stage failure is carried to the end as item.error; later stages skip it, so
    the caller still gets one result per accepted frame.
  - Results may complete out of order when a stage has several workers; each
    item keeps its seq, which is how a result is matched to its part.

Threads are enough here: OpenCV decode, NumPy kernels and the CUDA synchronize
call release the GIL while they work.
"""

from __future__ import annotations

import queue
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Callable, Iterator

_STOP = object()


@dataclass
class Item:
    seq: int
    t_arrival: float  # perf_counter() when the frame became available
    value: Any
    error: BaseException | None = None
    t_done: float = 0.0
    service_s: dict[str, float] = field(default_factory=dict)  # time inside each stage
    wait_s: dict[str, float] = field(default_factory=dict)  # time queued before each stage
    _t_enqueued: float = 0.0


@dataclass(frozen=True)
class Stage:
    name: str
    fn: Callable[[Any], Any]
    workers: int = 1


class Pipeline:
    def __init__(self, stages: list[Stage], queue_size: int = 4):
        if not stages:
            raise ValueError("at least one stage")
        if queue_size < 1:
            raise ValueError("queue_size must be >= 1")
        self.stages = stages
        self.queues: list[queue.Queue] = [queue.Queue(maxsize=queue_size) for _ in stages]
        self.results: queue.Queue = queue.Queue()  # unbounded: the consumer drains it
        self._alive = [s.workers for s in stages]
        self._lock = threading.Lock()
        self._threads: list[threading.Thread] = []
        self._closed = False

    def start(self) -> "Pipeline":
        for index, stage in enumerate(self.stages):
            for worker in range(stage.workers):
                thread = threading.Thread(target=self._work, args=(index,), name=f"{stage.name}-{worker}", daemon=True)
                thread.start()
                self._threads.append(thread)
        return self

    def submit(self, item: Item, block: bool = True) -> bool:
        """Queue a frame. With block=False a full first queue drops it and returns False."""
        if self._closed:
            raise RuntimeError("pipeline is closed")
        item._t_enqueued = time.perf_counter()
        try:
            self.queues[0].put(item, block=block)
        except queue.Full:
            return False
        return True

    def close(self) -> None:
        """No more frames; workers finish the queued ones, then stop."""
        if self._closed:
            return
        self._closed = True
        for _ in range(self.stages[0].workers):
            self.queues[0].put(_STOP)

    def drain(self) -> Iterator[Item]:
        """Yield results until every accepted frame has come out (call after close())."""
        while True:
            item = self.results.get()
            if item is _STOP:
                return
            yield item

    def join(self, timeout: float | None = None) -> None:
        for thread in self._threads:
            thread.join(timeout)

    def _forward(self, index: int, item: Item) -> None:
        item._t_enqueued = time.perf_counter()
        if index + 1 < len(self.stages):
            self.queues[index + 1].put(item)  # blocks when full: backpressure inside the pipeline
        else:
            item.t_done = item._t_enqueued
            self.results.put(item)

    def _work(self, index: int) -> None:
        stage, inbox = self.stages[index], self.queues[index]
        while True:
            item = inbox.get()
            if item is _STOP:
                with self._lock:
                    self._alive[index] -= 1
                    last = self._alive[index] == 0
                if last:  # the last worker of a stage passes the stop downstream once per next worker
                    if index + 1 < len(self.stages):
                        for _ in range(self.stages[index + 1].workers):
                            self.queues[index + 1].put(_STOP)
                    else:
                        self.results.put(_STOP)
                return
            t_start = time.perf_counter()
            item.wait_s[stage.name] = t_start - item._t_enqueued
            if item.error is None:
                try:
                    item.value = stage.fn(item.value)
                except BaseException as exc:  # reported per frame, never kills the worker
                    item.error = exc
            item.service_s[stage.name] = time.perf_counter() - t_start
            self._forward(index, item)


def percentile(values: list[float], q: float) -> float:
    """Linear-interpolation percentile (same method as the requester CSV analysis)."""
    if not values:
        return float("nan")
    ordered = sorted(values)
    k = (len(ordered) - 1) * q
    lo = int(k)
    hi = min(lo + 1, len(ordered) - 1)
    return ordered[lo] + (ordered[hi] - ordered[lo]) * (k - lo)
