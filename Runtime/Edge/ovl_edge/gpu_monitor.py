"""Background sampler for GPU and worker-process resources (design M5, portfolio step 3).

Every `interval_s` it reads, through NVML (cuda.bindings.nvml, already part of
cuda-python), the GPU's memory use, temperature, power, utilization and SM
clock, and the worker process's resident memory from /proc. Values go to
Prometheus gauges; alert rules decide what is too hot or too full.

A failure to read NVML is logged once and reported as ovl_gpu_monitor_up 0, so
missing GPU data shows up as an alert instead of a silently empty panel.
"""

from __future__ import annotations

import logging
import threading
from pathlib import Path

from prometheus_client import Gauge

LOG = logging.getLogger("ovl_edge.gpu_monitor")

GPU_MONITOR_UP = Gauge("ovl_gpu_monitor_up", "1 while NVML readings succeed")
GPU_MEMORY_USED = Gauge("ovl_gpu_memory_used_bytes", "GPU memory in use (whole device)")
GPU_MEMORY_TOTAL = Gauge("ovl_gpu_memory_total_bytes", "GPU memory size")
GPU_TEMPERATURE = Gauge("ovl_gpu_temperature_celsius", "GPU core temperature")
GPU_POWER = Gauge("ovl_gpu_power_watts", "GPU board power draw")
GPU_UTILIZATION = Gauge("ovl_gpu_utilization_ratio", "Fraction of the last sample period the GPU was busy (0-1)")
GPU_SM_CLOCK = Gauge("ovl_gpu_sm_clock_mhz", "Current SM clock")
WORKER_RSS = Gauge("ovl_worker_rss_bytes", "Resident memory of the worker process")


def read_rss_bytes(status: Path = Path("/proc/self/status")) -> int | None:
    try:
        for line in status.read_text().splitlines():
            if line.startswith("VmRSS:"):
                return int(line.split()[1]) * 1024  # kB
    except OSError:
        return None
    return None


class GpuMonitor:
    def __init__(self, interval_s: float = 5.0, device_index: int = 0):
        self.interval_s = interval_s
        self.device_index = device_index
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, name="gpu-monitor", daemon=True)
        self._nvml = None
        self._handle = None
        self._warned = False

    def start(self) -> "GpuMonitor":
        self._thread.start()
        return self

    def stop(self) -> None:
        self._stop.set()

    def _open(self) -> None:
        from cuda.bindings import nvml

        nvml.init_v2()
        self._nvml = nvml
        self._handle = nvml.device_get_handle_by_index_v2(self.device_index)

    def sample(self) -> None:
        rss = read_rss_bytes()
        if rss is not None:
            WORKER_RSS.set(rss)
        from ovl_edge.opcua_security import refresh_expiry

        refresh_expiry()  # certificate days-left keep counting down in a long-running worker
        try:
            if self._nvml is None:
                self._open()
            nvml, h = self._nvml, self._handle
            memory = nvml.device_get_memory_info_v2(h)
            GPU_MEMORY_USED.set(memory.used)
            GPU_MEMORY_TOTAL.set(memory.total)
            GPU_TEMPERATURE.set(nvml.device_get_temperature_v(h, nvml.TemperatureSensors.GPU))
            GPU_POWER.set(nvml.device_get_power_usage(h) / 1000.0)  # mW -> W
            GPU_UTILIZATION.set(nvml.device_get_utilization_rates(h).gpu / 100.0)
            GPU_SM_CLOCK.set(nvml.device_get_clock_info(h, nvml.ClockType.CLOCK_SM))
            GPU_MONITOR_UP.set(1)
        except Exception as exc:  # NVML missing or a device error must not stop inference
            GPU_MONITOR_UP.set(0)
            self._nvml = None
            if not self._warned:
                LOG.warning("GPU readings unavailable: %s", exc)
                self._warned = True

    def _run(self) -> None:
        while not self._stop.is_set():
            self.sample()
            self._stop.wait(self.interval_s)
