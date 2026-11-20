// Native TensorRT path: CUDA letterbox pre-processing + TensorRT inference in C++.
//
// Purpose: measure what the Python worker costs on top of the same engine, and what
// moving pre-processing to the GPU saves. Both modes produce the same input tensor
// as ovl_edge/preprocess.py (letterbox -> BGR->RGB -> CHW -> /255):
//   --mode cpu : letterbox on the CPU (same integer bilinear as below), upload float32 NCHW
//   --mode gpu : upload the raw uint8 BGR frame, letterbox + normalise in one CUDA kernel
// The resize reproduces cv2.resize(INTER_LINEAR) for uint8: half-pixel source
// coordinates, 11-bit fixed-point weights, rounding shift of 22 bits. OpenCV's SIMD
// path can still differ by one gray level; the Python checker measures that.
//
// Frames come from a file written by ovl_edge.cli.native_check (header "OVLF", count,
// height, width, then BGR uint8 frames). Raw head outputs can be written for the
// checker to decode with the Python post-processing and compare verdicts.
//
// Build: native/build.sh (nvcc, -lnvinfer). Usage:
//   trt_native_bench --engine fp16.plan --frames frames.bin --mode gpu --repeats 3 --out-raw raw.bin --out-times times.csv

#include <NvInfer.h>
#include <cuda_runtime.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

#define CHECK(call)                                                                              \
  do {                                                                                           \
    cudaError_t e = (call);                                                                      \
    if (e != cudaSuccess) throw std::runtime_error(std::string(#call) + ": " + cudaGetErrorString(e)); \
  } while (0)

class Logger : public nvinfer1::ILogger {
  void log(Severity s, const char* msg) noexcept override {
    if (s <= Severity::kWARNING) std::cerr << "[trt] " << msg << "\n";
  }
};

struct Geometry {  // mirrors ovl_edge.preprocess.letterbox
  int src_w, src_h, dst_w, dst_h, new_w, new_h, left, top;
};

static Geometry letterbox_geometry(int src_w, int src_h, int dst_w, int dst_h) {
  Geometry g{src_w, src_h, dst_w, dst_h, 0, 0, 0, 0};
  double gain = std::min(double(dst_h) / src_h, double(dst_w) / src_w);
  g.new_w = std::max(int(std::nearbyint(src_w * gain)), 1);  // Python round(): banker's rounding = nearbyint
  g.new_h = std::max(int(std::nearbyint(src_h * gain)), 1);
  double dw = (dst_w - g.new_w) / 2.0, dh = (dst_h - g.new_h) / 2.0;
  g.left = int(std::nearbyint(dw - 0.1));
  g.top = int(std::nearbyint(dh - 0.1));
  return g;
}

// cv2 INTER_LINEAR source index and fixed-point weights for one destination coordinate.
// As in OpenCV resize: fx = float((dx + 0.5) * inv_scale - 0.5) with a double scale, then
// each weight is rounded on its own: round((1 - a) * 2048), round(a * 2048).
__host__ __device__ inline void linear_coeffs(int d, int src_len, int new_len, int& s0, int& s1, int& w0, int& w1) {
  if (src_len == new_len) { s0 = s1 = d; w0 = 2048; w1 = 0; return; }
  double scale = double(src_len) / double(new_len);
  float f = float((d + 0.5) * scale - 0.5);
  int s = int(floorf(f));
  float a = f - s;
  if (s < 0) { s = 0; a = 0.f; }
  if (s >= src_len - 1) { s = src_len - 1; a = 0.f; }
  s0 = s;
  s1 = s + 1 < src_len ? s + 1 : src_len - 1;
  w0 = int(lrintf((1.f - a) * 2048.f));
  w1 = int(lrintf(a * 2048.f));
}

// Vertical step as OpenCV's x86 SIMD path computes it for uint8 (VResizeLinearVec_32s8u):
// rows are shifted right by 4 to int16, multiplied by the 11-bit weight keeping the high 16
// bits, summed, then (+2) >> 2. The exact formula (r0*w0 + r1*w1 + 2^21) >> 22 differed by
// one gray level in 11.9% of values on the 512 -> 320 test frames.
__host__ __device__ inline unsigned char sample(const unsigned char* src, int sw, int c, int x0, int x1, int y0, int y1, int wx0, int wx1, int wy0, int wy1) {
  int r0 = src[(y0 * sw + x0) * 3 + c] * wx0 + src[(y0 * sw + x1) * 3 + c] * wx1;
  int r1 = src[(y1 * sw + x0) * 3 + c] * wx0 + src[(y1 * sw + x1) * 3 + c] * wx1;
  int t0 = ((r0 >> 4) * wy0) >> 16, t1 = ((r1 >> 4) * wy1) >> 16;
  int v = (t0 + t1 + 2) >> 2;
  return (unsigned char)(v < 0 ? 0 : (v > 255 ? 255 : v));
}

// One thread per destination pixel: letterbox resize, pad, BGR->RGB, HWC->CHW, /255.
__global__ void letterbox_kernel(const unsigned char* __restrict__ src, float* __restrict__ dst, Geometry g, float pad) {
  int x = blockIdx.x * blockDim.x + threadIdx.x;
  int y = blockIdx.y * blockDim.y + threadIdx.y;
  if (x >= g.dst_w || y >= g.dst_h) return;
  int plane = g.dst_w * g.dst_h, o = y * g.dst_w + x;
  int lx = x - g.left, ly = y - g.top;
  if (lx < 0 || ly < 0 || lx >= g.new_w || ly >= g.new_h) {
    dst[o] = dst[plane + o] = dst[2 * plane + o] = pad;
    return;
  }
  int x0, x1, wx0, wx1, y0, y1, wy0, wy1;
  linear_coeffs(lx, g.src_w, g.new_w, x0, x1, wx0, wx1);
  linear_coeffs(ly, g.src_h, g.new_h, y0, y1, wy0, wy1);
  for (int c = 0; c < 3; ++c)  // output channel c (RGB) reads source channel 2 - c (BGR)
    dst[c * plane + o] = sample(src, g.src_w, 2 - c, x0, x1, y0, y1, wx0, wx1, wy0, wy1) * (1.f / 255.f);
}

static void letterbox_cpu(const unsigned char* src, float* dst, const Geometry& g, float pad) {
  int plane = g.dst_w * g.dst_h;
  for (int y = 0; y < g.dst_h; ++y) {
    for (int x = 0; x < g.dst_w; ++x) {
      int o = y * g.dst_w + x, lx = x - g.left, ly = y - g.top;
      if (lx < 0 || ly < 0 || lx >= g.new_w || ly >= g.new_h) {
        dst[o] = dst[plane + o] = dst[2 * plane + o] = pad;
        continue;
      }
      int x0, x1, wx0, wx1, y0, y1, wy0, wy1;
      linear_coeffs(lx, g.src_w, g.new_w, x0, x1, wx0, wx1);
      linear_coeffs(ly, g.src_h, g.new_h, y0, y1, wy0, wy1);
      for (int c = 0; c < 3; ++c) dst[c * plane + o] = sample(src, g.src_w, 2 - c, x0, x1, y0, y1, wx0, wx1, wy0, wy1) * (1.f / 255.f);
    }
  }
}

static std::string arg(int argc, char** argv, const char* name, const char* fallback) {
  for (int i = 1; i + 1 < argc; ++i)
    if (std::strcmp(argv[i], name) == 0) return argv[i + 1];
  return fallback;
}

int main(int argc, char** argv) try {
  std::string engine_path = arg(argc, argv, "--engine", ""), frames_path = arg(argc, argv, "--frames", "");
  std::string mode = arg(argc, argv, "--mode", "gpu"), out_raw = arg(argc, argv, "--out-raw", ""), out_times = arg(argc, argv, "--out-times", "");
  int repeats = std::stoi(arg(argc, argv, "--repeats", "3")), warmup = std::stoi(arg(argc, argv, "--warmup", "50"));
  std::string dump_input = arg(argc, argv, "--dump-input", "");  // first-pass input tensors, for pixel parity
  int dump_count = std::stoi(arg(argc, argv, "--dump-count", "50"));
  if (engine_path.empty() || frames_path.empty() || (mode != "gpu" && mode != "cpu")) throw std::runtime_error("usage: --engine E --frames F --mode gpu|cpu");

  // frames
  std::ifstream ff(frames_path, std::ios::binary);
  char magic[4]; int32_t count, fh, fw;
  ff.read(magic, 4); ff.read((char*)&count, 4); ff.read((char*)&fh, 4); ff.read((char*)&fw, 4);
  if (!ff || std::memcmp(magic, "OVLF", 4) != 0) throw std::runtime_error("bad frames file");
  size_t frame_bytes = size_t(fh) * fw * 3;
  std::vector<unsigned char> frames(frame_bytes * count);
  ff.read((char*)frames.data(), frames.size());

  // engine
  Logger logger;
  std::ifstream ef(engine_path, std::ios::binary);
  std::vector<char> blob((std::istreambuf_iterator<char>(ef)), std::istreambuf_iterator<char>());
  auto runtime = nvinfer1::createInferRuntime(logger);
  auto engine = runtime->deserializeCudaEngine(blob.data(), blob.size());
  if (!engine) throw std::runtime_error("engine did not deserialize (GPU / TensorRT version?)");
  auto context = engine->createExecutionContext();
  const char *in_name = nullptr, *out_name = nullptr;
  for (int i = 0; i < engine->getNbIOTensors(); ++i) {
    const char* n = engine->getIOTensorName(i);
    (engine->getTensorIOMode(n) == nvinfer1::TensorIOMode::kINPUT ? in_name : out_name) = n;
  }
  auto in_dims = engine->getTensorShape(in_name), out_dims = engine->getTensorShape(out_name);
  if (engine->getTensorDataType(in_name) != nvinfer1::DataType::kFLOAT || engine->getTensorDataType(out_name) != nvinfer1::DataType::kFLOAT)
    throw std::runtime_error("expected float32 engine I/O");
  int dst_h = in_dims.d[2], dst_w = in_dims.d[3];
  size_t in_elems = size_t(3) * dst_h * dst_w, out_elems = 1;
  for (int i = 0; i < out_dims.nbDims; ++i) out_elems *= out_dims.d[i];
  Geometry g = letterbox_geometry(fw, fh, dst_w, dst_h);

  cudaStream_t stream; CHECK(cudaStreamCreate(&stream));
  unsigned char *d_frame, *h_frame; float *d_in, *d_out, *h_in, *h_out;
  CHECK(cudaMalloc(&d_frame, frame_bytes)); CHECK(cudaMallocHost(&h_frame, frame_bytes));
  CHECK(cudaMalloc(&d_in, in_elems * 4)); CHECK(cudaMallocHost(&h_in, in_elems * 4));
  CHECK(cudaMalloc(&d_out, out_elems * 4)); CHECK(cudaMallocHost(&h_out, out_elems * 4));
  context->setTensorAddress(in_name, d_in);
  context->setTensorAddress(out_name, d_out);
  dim3 block(16, 16), grid((dst_w + 15) / 16, (dst_h + 15) / 16);
  const float pad = 114.f / 255.f;

  auto run = [&](const unsigned char* frame) {
    if (mode == "gpu") {
      std::memcpy(h_frame, frame, frame_bytes);  // camera buffer -> pinned staging (a real driver can DMA into pinned memory directly)
      CHECK(cudaMemcpyAsync(d_frame, h_frame, frame_bytes, cudaMemcpyHostToDevice, stream));
      letterbox_kernel<<<grid, block, 0, stream>>>(d_frame, d_in, g, pad);
      CHECK(cudaGetLastError());
    } else {
      letterbox_cpu(frame, h_in, g, pad);
      CHECK(cudaMemcpyAsync(d_in, h_in, in_elems * 4, cudaMemcpyHostToDevice, stream));
    }
    if (!context->enqueueV3(stream)) throw std::runtime_error("enqueueV3 failed");
    CHECK(cudaMemcpyAsync(h_out, d_out, out_elems * 4, cudaMemcpyDeviceToHost, stream));
    CHECK(cudaStreamSynchronize(stream));
  };

  for (int i = 0; i < warmup; ++i) run(frames.data() + frame_bytes * (i % count));
  std::ofstream raw, dump;
  if (!out_raw.empty()) raw.open(out_raw, std::ios::binary);
  if (!dump_input.empty()) dump.open(dump_input, std::ios::binary);
  std::vector<double> ms;
  for (int r = 0; r < repeats; ++r)
    for (int i = 0; i < count; ++i) {
      auto t0 = std::chrono::steady_clock::now();
      run(frames.data() + frame_bytes * i);
      ms.push_back(std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - t0).count());
      if (r == 0 && raw) raw.write((const char*)h_out, out_elems * 4);
      if (r == 0 && dump && i < dump_count) {  // outside the timed span
        if (mode == "gpu") CHECK(cudaMemcpy(h_in, d_in, in_elems * 4, cudaMemcpyDeviceToHost));
        dump.write((const char*)h_in, in_elems * 4);
      }
    }
  if (!out_times.empty()) {
    std::ofstream tf(out_times);
    for (double v : ms) tf << v << "\n";
  }
  std::vector<double> s = ms;
  std::sort(s.begin(), s.end());
  auto pct = [&](double q) { double k = (s.size() - 1) * q; size_t f = size_t(k), c = std::min(f + 1, s.size() - 1); return s[f] + (s[c] - s[f]) * (k - f); };
  std::printf("{\"mode\":\"%s\",\"frames\":%d,\"timed\":%zu,\"src\":[%d,%d],\"dst\":[%d,%d],\"p50_ms\":%.4f,\"p99_ms\":%.4f,\"mean_ms\":%.4f}\n",
              mode.c_str(), count, ms.size(), fw, fh, dst_w, dst_h, pct(0.5), pct(0.99), s.empty() ? 0.0 : [&] { double a = 0; for (double v : s) a += v; return a / s.size(); }());
  return 0;
} catch (const std::exception& e) {
  std::cerr << "error: " << e.what() << "\n";
  return 1;
}
