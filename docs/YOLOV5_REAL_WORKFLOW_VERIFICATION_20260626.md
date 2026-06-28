# YOLOv5 Real Workflow Verification - 2026-06-26

## Scope

This pass verifies the object-detection path beyond UI labeling:

- Python worker environment and parser self-test
- Real TCP inference from the C# workflow
- Real YOLOv5 `train.py` execution on the local `C:\Git\yolov5` dataset
- Baseline-vs-new-model comparison on the same validation set

## Environment

- Python: `C:\Git\yolov5\.venv\Scripts\python.exe`
- Python version: `3.11.4`
- Torch: `2.12.1+cpu`
- CUDA: `False`
- YOLOv5 source: `C:\Git\yolov5\yolov5Master`
- Baseline weights: `C:\Git\yolov5\best.pt`
- Dataset root: `C:\Git\yolov5\data`

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Python worker self-test | Pass | `labeling_tcp_client.py --self-test` returned `self-test passed`. |
| Real TCP inference | Pass | `scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3` returned 3 results and 3 detections. |
| C# real YOLO workflow | Pass | `--real-yolo-smoke` detected, rendered overlays, confirmed candidates, and saved labels. |
| Real `train.py` smoke | Pass | 1 epoch completed on CPU and produced `best.pt`. |
| Model comparison | Pass | Baseline remains clearly better than the 1-epoch smoke model. |

## Commands

```powershell
& "C:\Git\yolov5\.venv\Scripts\python.exe" "C:\Git\yolov5\labeling_tcp_client.py" --self-test

powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\smoke-yolo-tcp.ps1" `
  -UseDetectImage -Repeat 3 -TimeoutSeconds 180 `
  -OutputDirectory "artifacts\python-smoke\yolo-tcp-20260626-101950"

dotnet run --project ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" `
  -c Debug --no-build -- --real-yolo-smoke

& "C:\Git\yolov5\.venv\Scripts\python.exe" "C:\Git\yolov5\yolov5Master\train.py" `
  --img 320 --batch-size 4 --epochs 1 `
  --data "C:\Git\Labelling_Application\artifacts\yolo-training\yolov5_real_train_data_20260626-102322.yaml" `
  --weights "C:\Git\yolov5\yolov5s.pt" `
  --device cpu --workers 0 `
  --project "C:\Git\Labelling_Application\artifacts\yolo-training" `
  --name "yolov5_real_train_smoke_20260626-102322" --exist-ok
```

## Model Comparison

Validation data: `C:\Git\Labelling_Application\artifacts\yolo-training\yolov5_real_train_data_20260626-102322.yaml`

| Model | Precision | Recall | mAP50 | mAP50-95 | UI candidates | Max confidence |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline `C:\Git\yolov5\best.pt` | 0.997 | 1.000 | 0.995 | 0.961 | 152 / 566 | 0.982 |
| 1-epoch smoke model | 0.003 | 0.603 | 0.024 | 0.004 | 0 / 37500 | 0.055 |

Conclusion: keep `C:\Git\yolov5\best.pt` as the active operational model. The new 1-epoch model proves the training pipeline runs, but it is not suitable for use.

## Artifacts

- TCP smoke: `C:\Git\Labelling_Application\artifacts\python-smoke\yolo-tcp-20260626-101950`
- C# real YOLO smoke: `C:\Git\Labelling_Application\artifacts\real-yolo-smoke\20260626-102055`
- Training run: `C:\Git\Labelling_Application\artifacts\yolo-training\yolov5_real_train_smoke_20260626-102322`
- Training log: `C:\Git\Labelling_Application\artifacts\yolo-training\yolov5_real_train_smoke_20260626-102322.log`
- New smoke weights: `C:\Git\Labelling_Application\artifacts\yolo-training\yolov5_real_train_smoke_20260626-102322\weights\best.pt`
- Comparison report: `C:\Git\Labelling_Application\artifacts\yolo-model-comparison\20260626-102622\comparison-report.md`

## Findings

- `C:\Git\yolov5\data.yaml` uses `path: .`; when `train.py` runs from `yolov5Master`, YOLOv5 resolves that relative path under `yolov5Master` and fails to find `data\valid\images`.
- A PowerShell-generated UTF-8 BOM caused YOLOv5 to parse the first key as `\ufeffpath`, so `path` was ignored. The app's `CYolov5.CreateYaml` already writes UTF-8 without BOM, and a regression test now checks that contract.
- Train and valid currently contain the same 125 images and labels. This is acceptable for smoke testing, but it is not a meaningful generalization test.

## Next

1. Keep the baseline `best.pt` active in the app.
2. When the app exports a new dataset, use the app-generated `data.yaml`, not the legacy `C:\Git\yolov5\data.yaml`.
3. For real model improvement, create a separated train/valid/test split and add more NG examples before longer training.
4. Add a UI-visible model comparison summary only if users need to choose between multiple trained `best.pt` files inside the app.

## Follow-Up: Dataset Contract Hardening

Completed in the next pass:

- `YoloDatasetValidator.ValidateTrainingFiles` now validates the `data.yaml` contract before training.
- The validator rejects UTF-8 BOM in `data.yaml`.
- The validator rejects stale `train`, `val`, and `test` paths that do not match the current project output.
- The validator rejects class count/name mismatches between `data.yaml` and the project class catalog.
- `docs\STABLE_VERIFIED_AREAS.md` now marks the YOLOv5 runtime and dataset contract as a protected verified area.
