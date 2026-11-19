# OpenVisionLab edge inference images.
#   runtime : serving image (TensorRT + ONNX Runtime GPU + host pre/post + I/O + metrics)
#   prepare : explicit preparation steps (ONNX export, parity check, INT8 Q/DQ, engine build)
# Build context: Runtime/Edge
#   docker build -f deploy/docker/Dockerfile --target prepare -t ovl-edge-prepare:dev .
# Base image tag requires NVIDIA driver >= 620.70 (CUDA_DRIVER_VERSION of 26.11-py3).
ARG TRT_IMAGE=nvcr.io/nvidia/tensorrt:26.11-py3

FROM ${TRT_IMAGE} AS runtime
ENV PIP_ROOT_USER_ACTION=ignore \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/opt/ovl-edge
COPY deploy/docker/requirements-runtime.txt /tmp/requirements-runtime.txt
RUN pip install -r /tmp/requirements-runtime.txt
COPY ovl_edge /opt/ovl-edge/ovl_edge
WORKDIR /work

FROM runtime AS prepare
# CPU torch is enough for export and the Ultralytics reference; GPU inference
# under test always goes through ORT or TensorRT, never torch.
# YOLO_AUTOINSTALL=false makes a missing exporter dependency fail instead of
# silently pip-installing an unpinned package at run time.
ENV YOLO_AUTOINSTALL=false \
    YOLO_OFFLINE=true
COPY deploy/docker/requirements-prepare.txt deploy/docker/constraints.txt /tmp/
# Ultralytics pulls the GUI build of OpenCV, which needs X11 libraries absent in
# this image; replace it with the headless build that shares the cv2 module.
RUN pip install --extra-index-url https://download.pytorch.org/whl/cpu -c /tmp/constraints.txt -r /tmp/requirements-prepare.txt \
 && pip uninstall -y opencv-python \
 && pip install --force-reinstall --no-deps "$(pip freeze | grep -i '^opencv-python-headless==')"
# Model Optimizer lives in its own virtualenv because its ONNX extra pins onnx and
# onnxruntime-gpu to versions older than the runtime pins (see requirements-modelopt.txt).
# It only produces FP16 / Q-DQ ONNX files; trtexec in the system env builds the engines.
COPY deploy/docker/requirements-modelopt.txt /tmp/
RUN pip install virtualenv \
 && python3 -m virtualenv /opt/venv-modelopt \
 && /opt/venv-modelopt/bin/pip install --extra-index-url https://download.pytorch.org/whl/cpu -r /tmp/requirements-modelopt.txt
ENV OVL_MODELOPT_PYTHON=/opt/venv-modelopt/bin/python
# Unit tests are not baked in; run them with the source mounted:
#   docker run --rm -v "$PWD":/src -w /src -e PYTHONPATH=/src ovl-edge-prepare:dev python -m pytest -q tests
