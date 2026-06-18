# Python Protocol Samples

The C# labeling app owns dataset files and annotation state. Python owns training and inference. Keep this folder in sync when the TCP protocol changes.

## Current Training Packet

C# sends command text, two newline separators, then a UTF-8 JSON payload:

```text
StartTraining

{"imgSize":"640","batch":"8","epoch":"100","cfg":"yolov5m.yaml","weight":"yolov5m.pt","dataYaml":"C:/data/data.yaml"}
```

The separator is `\n\n`.

## Current Inference Packet

C# sends the command text, two newline separators, then PNG bytes:

```text
StartDefect

<png bytes>
```

## Current Detection Result

Python returns `ResultDefect` followed by a JSON array. Coordinates are pixel-space rectangles on the source image.

```text
ResultDefect [{"ClassName":"NG","Confidence":0.98,"X":10,"Y":20,"Width":80,"Height":40}]
```

The C# parser for this legacy shape is `PythonDetectionResultProtocol`.
TCP chunks may split this text in the middle of JSON. The C# receiver buffers split chunks with `PythonMessageFramer`, so Python may send the message in one or more socket writes as long as the JSON object or array is complete in order.
The C# TCP communication object can also be constructed without immediately opening the listener for tests and tooling; the production default still starts the listener automatically.

## Current V1 Detection Result

The real YOLOv5 client sends a versioned JSON object:

```json
{
  "type": "ResultDefect",
  "version": 1,
  "imageId": "sample-001",
  "items": [
    {
      "className": "NG",
      "confidence": 0.98,
      "x": 10,
      "y": 20,
      "width": 80,
      "height": 40
    }
  ]
}
```

`PythonDetectionResultProtocol` already accepts both the current legacy message and this target object shape.
If the envelope contains an `error` field, C# surfaces that as a detection error instead of silently ignoring it.

## Current Status Messages

Python may also send model status envelopes. C# parses these with `PythonModelStatusProtocol` and reflects them in logs/status UI.

```json
{
  "type": "TrainingStatus",
  "version": 1,
  "state": "started",
  "message": "YOLOv5 training started.",
  "progressPercent": 0
}
```

`DetectionStatus` uses the same shape for inference start/failure messages.

## Mock Python Client

Use `mock_yolo_client.py` to test the C# app without a real YOLO runtime. Start the labeling app first, then run:

```powershell
python .\samples\python_protocol\mock_yolo_client.py --result-format v1 --split-response
```

The mock connects to the C# TCP listener at `127.0.0.1:5000`, prints `StartTraining` or `StartDefect` packets, and returns deterministic detection boxes. `--split-response` intentionally sends the result in two socket writes so the C# TCP framer path is exercised.

The script also has a local parser smoke test:

```powershell
python .\samples\python_protocol\mock_yolo_client.py --self-test
```

## Real YOLOv5 Client

The real Python-side project is expected at `C:\Git\yolov5`. The C# app can auto-start the client when training or inference is requested, using `ProjectSettings.PythonModel`.

Manual startup is still useful for Python-side debugging. Start the C# labeling app first, then run from that folder:

```powershell
py .\labelling_tcp_client.py --retry --weights "C:\Git\yolov5\best.pt" --image-root "C:\Git\py\KtemData"
```

The client receives the same packets as the mock client, calls `yolov5Master\yolov5Train.py` for training, calls `yolov5Master\yolov5Defect.py` for inference, sends `TrainingStatus` / `DetectionStatus`, and returns the v1 `ResultDefect` envelope.
