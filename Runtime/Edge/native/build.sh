#!/usr/bin/env bash
# Build the native TensorRT bench inside ovl-edge-runtime:dev (nvcc + TensorRT headers):
#   docker run --rm -v "$PWD":/src -w /src/native ovl-edge-runtime:dev bash build.sh
set -euo pipefail
nvcc -O3 -std=c++17 -arch=native -o trt_native_bench trt_native_bench.cu -lnvinfer -lcudart
echo "built $(pwd)/trt_native_bench"
