# KTEM Pretrained YOLO Integration

## Source Layout

The current local assets for pretrained detection are under:

- C# project: `C:\Git\Labelling_Application`
- YOLO runtime project: `C:\Git\yolov5`
- Detection images: `C:\Git\yolov5\data\train\images`
- Trained weights: `C:\Git\yolov5\best.pt`

The labeling app uses `C:\Git\yolov5\best.pt` as the default runtime weight.

## Current App Defaults

New or legacy-default Python model settings resolve to:

- `ProjectRootPath`: `C:\Git\yolov5`
- `ClientScriptPath`: `C:\Git\yolov5\labelling_tcp_client.py`
- `WeightsPath`: `C:\Git\yolov5\best.pt`
- `ImageRootPath`: `C:\Git\yolov5\data\train\images`

The image list folder picker starts from `ImageRootPath` when it exists.

## Runtime Client

`C:\Git\yolov5\labelling_tcp_client.py` is a headless TCP client for the current C# labeling app.

It receives `StartDefect` packets, runs the local pretrained `best.pt` model through `yolov5Master`, and returns a v1 `ResultDefect` envelope:

```json
{
  "type": "ResultDefect",
  "version": 1,
  "imageId": "",
  "items": [
    {
      "className": "NG",
      "confidence": 0.95,
      "x": 10,
      "y": 20,
      "width": 80,
      "height": 40
    }
  ]
}
```

It also emits `DetectionStatus` envelopes for status/error reporting.

## C# Labeling App Integration

- The app starts the Python TCP client in the background and waits for the client connection before sending training or detection packets.
- `StartDefect` requests are tied to the active image name/path/size before the packet is sent.
- A returned `ResultDefect` is ignored if the operator has moved to another image before the result arrives.
- Detection candidates are rendered on `Main` through the OpenGL overlay path.
- `확정` / `Ctrl+Enter` converts the current candidates into Main ROI labels and saves YOLO txt/image output.
- After a successful confirmation, the candidate state is cleared so the same result cannot be saved repeatedly.
- The Python client process receives the configured minimum detection confidence through `--conf`; C# still applies the same threshold when deciding which candidates can be confirmed.

## Verification

Parser and packet smoke test:

```powershell
& "C:\Git\yolov5\.venv\Scripts\python.exe" "C:\Git\yolov5\labelling_tcp_client.py" --self-test
```

Single-image model smoke test, when the selected Python has `torch`, `torchvision`, and YOLOv5 requirements installed:

```powershell
& "C:\Git\yolov5\.venv\Scripts\python.exe" "C:\Git\yolov5\labelling_tcp_client.py" --smoke-test --weights "C:\Git\yolov5\best.pt" --model-root "C:\Git\yolov5\yolov5Master" --image "C:\Git\yolov5\data\train\images\Teaching_0.jpeg"
```

Protocol-level TCP smoke test from the C# workspace:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-yolo-tcp.ps1
```

This starts a loopback TCP listener, launches the real `labelling_tcp_client.py`, sends a `StartDefect` PNG packet, and requires a `ResultDefect` response.

Full C# labeling workflow smoke test from the C# workspace:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-yolo-workflow.ps1
```

This starts the C# TCP listener, launches the real Python client against an available local port, sends the active Main image through `StartDefect`, applies the returned boxes as OpenGL candidates, confirms them, and verifies the saved YOLO label file plus `review-status.json` under `artifacts\real-yolo-smoke\`.

Verified local smoke result after installing the venv:

```json
{"type":"ResultDefect","version":1,"imageId":"","items":[{"className":"OK","confidence":0.975796639919281,"x":24.475740432739258,"y":23.23061180114746,"width":55.47608757019043,"height":55.56083869934082}]}
```

Latest TCP smoke artifact:

```json
{"image":"C:\\Git\\yolov5\\data\\train\\images\\Teaching_0.jpeg","detectionCount":1,"firstClass":"OK","responsePath":"C:\\Git\\Labelling_Application\\artifacts\\python-smoke\\yolo-tcp-response.txt"}
```

Latest C# workflow smoke artifact:

```text
candidateCount=1
committedCount=1
candidate[1]=OK,0.9758,{X=24,Y=23,Width=55,Height=56}
label[1]=OK,{X=24,Y=23,Width=55,Height=56}
reviewStatusPath=C:\Git\Labelling_Application\artifacts\real-yolo-smoke\...\dataset\review-status.json
ReviewStateName=Confirmed
```

The current Python environment is CPU-only (`torch=2.8.0+cpu`). `setuptools` must stay below 81 because this YOLOv5 code imports `pkg_resources`.
