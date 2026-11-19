"""TensorRT engine runner (TensorRT 10/11 tensor-address API, cuda-python).

infer() measures nothing itself; the caller times the whole call, which covers
H2D copy -> enqueue -> D2H copy -> stream synchronize. That interval is the
design's "inference" span: input tensor ready -> output tensor readable.
"""

from __future__ import annotations

import ctypes
from pathlib import Path

import numpy as np

try:  # cuda-python >= 12.6
    from cuda.bindings import runtime as cudart
except ImportError:  # older cuda-python layout
    from cuda import cudart  # type: ignore[no-redef]


def _check(result):
    err = result[0] if isinstance(result, tuple) else result
    if int(err) != 0:
        raise RuntimeError(f"CUDA runtime error {err}")
    if isinstance(result, tuple):
        return result[1] if len(result) == 2 else result[1:]
    return None


CUDA_SCHEDULE_FLAGS = {
    "spin": "cudaDeviceScheduleSpin",
    "yield": "cudaDeviceScheduleYield",
    "blocking": "cudaDeviceScheduleBlockingSync",
}


def configure_cuda_schedule(mode: str | None) -> str:
    """Set how the host thread waits for GPU work; must run before the CUDA context exists.

    Opt-in (OVL_CUDA_SCHEDULE). With 30 ms idle gaps between requests, 'yield'
    halved the infer-span p99 in a probe on 2026-11-20 (WSL2, RTX 4070 SUPER);
    the default ('auto') is kept unless the setting is given.
    """
    if not mode or mode == "auto":
        return "auto"
    flag = getattr(cudart, CUDA_SCHEDULE_FLAGS[mode])
    err = cudart.cudaSetDeviceFlags(int(flag))[0]
    if int(err) != 0:
        raise RuntimeError(f"cudaSetDeviceFlags({mode}) failed with {err}; the CUDA context already exists")
    return mode


def current_gpu() -> dict:
    """Name and compute capability of the current CUDA device (recorded in and compared with engines.json)."""
    device = _check(cudart.cudaGetDevice())
    props = _check(cudart.cudaGetDeviceProperties(device))
    name = props.name.decode(errors="replace") if isinstance(props.name, (bytes, bytearray)) else str(props.name)
    return {"name": name.split("\x00", 1)[0].strip(), "compute_capability": f"{props.major}.{props.minor}"}


class TrtRunner:
    def __init__(self, engine_path: str | Path, name: str | None = None, cuda_schedule: str | None = None):
        import os

        self.cuda_schedule = configure_cuda_schedule(cuda_schedule or os.environ.get("OVL_CUDA_SCHEDULE"))
        import tensorrt as trt

        self.trt_version = trt.__version__
        self.logger = trt.Logger(trt.Logger.WARNING)
        self.runtime = trt.Runtime(self.logger)
        engine_bytes = Path(engine_path).read_bytes()
        self.engine = self.runtime.deserialize_cuda_engine(engine_bytes)
        if self.engine is None:
            # Raised for an engine built on another GPU/TensorRT version (design M3 rule).
            raise RuntimeError(f"Cannot deserialize engine {engine_path}; GPU or TensorRT version mismatch")
        self.context = self.engine.create_execution_context()
        self.stream = _check(cudart.cudaStreamCreate())
        self.name = name or f"trt:{Path(engine_path).stem}"

        self.inputs: list[tuple[str, np.ndarray, int]] = []
        self.outputs: list[tuple[str, np.ndarray, int]] = []
        self._pinned: list[int] = []
        for index in range(self.engine.num_io_tensors):
            tensor_name = self.engine.get_tensor_name(index)
            shape = tuple(self.engine.get_tensor_shape(tensor_name))
            if any(d < 0 for d in shape):
                raise RuntimeError(f"Dynamic shape {shape} for {tensor_name}; first scope uses fixed shapes")
            dtype = np.dtype(trt.nptype(self.engine.get_tensor_dtype(tensor_name)))
            nbytes = int(np.prod(shape)) * dtype.itemsize
            # Page-locked host buffers: async copies from pageable memory are staged
            # by the driver and showed large latency jitter under WSL2 (M2 trials 1-3).
            pinned = int(_check(cudart.cudaMallocHost(nbytes)))
            self._pinned.append(pinned)
            host = np.ctypeslib.as_array((ctypes.c_byte * nbytes).from_address(pinned)).view(dtype).reshape(shape)
            device = _check(cudart.cudaMalloc(nbytes))
            self.context.set_tensor_address(tensor_name, int(device))
            entry = (tensor_name, host, int(device))
            if self.engine.get_tensor_mode(tensor_name) == trt.TensorIOMode.INPUT:
                self.inputs.append(entry)
            else:
                self.outputs.append(entry)
        if len(self.inputs) != 1 or len(self.outputs) != 1:
            raise RuntimeError(f"Expected 1 input / 1 output, got {len(self.inputs)} / {len(self.outputs)}")
        self.input_dtype = self.inputs[0][1].dtype

    def infer(self, tensor: np.ndarray) -> np.ndarray:
        name, host_in, dev_in = self.inputs[0]
        if tensor.shape != host_in.shape:
            raise ValueError(f"Input shape {tensor.shape} != engine {host_in.shape}")
        np.copyto(host_in, tensor, casting="same_kind")
        h2d = cudart.cudaMemcpyKind.cudaMemcpyHostToDevice
        d2h = cudart.cudaMemcpyKind.cudaMemcpyDeviceToHost
        _check(cudart.cudaMemcpyAsync(dev_in, host_in.ctypes.data, host_in.nbytes, h2d, self.stream))
        if not self.context.execute_async_v3(int(self.stream)):
            raise RuntimeError("execute_async_v3 failed")
        _, host_out, dev_out = self.outputs[0]
        _check(cudart.cudaMemcpyAsync(host_out.ctypes.data, dev_out, host_out.nbytes, d2h, self.stream))
        _check(cudart.cudaStreamSynchronize(self.stream))
        return host_out.astype(np.float32, copy=True)

    def close(self) -> None:
        for _, _, device in self.inputs + self.outputs:
            cudart.cudaFree(device)
        for pinned in self._pinned:
            cudart.cudaFreeHost(pinned)
        cudart.cudaStreamDestroy(self.stream)
        self.inputs, self.outputs, self._pinned = [], [], []
