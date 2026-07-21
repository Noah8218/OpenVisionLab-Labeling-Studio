# External native segmentation 30-epoch error analysis (2026-07-21)

Status: Complete

## Scope

Read-only diagnosis of the completed EasyMatch Die Array 30-epoch U-Net versus YOLOv8-seg benchmark. The goal was to separate the U-Net zero-Dice classes from the YOLO false-positive flood and choose one controlled next hypothesis. It does not retrain either model, edit the external source, change recipe settings, select a model, or tune against the held-out `test` split.

## Fixed inputs

- Native source: `D:\라벨테스트\EasyMatch_Die_Array_500(1)\EasyMatch_Die_Array_500\segmentation\data.yaml`.
- Source fingerprint: `C0FDB11C644F1D705EC3B033FDFB5205E0DE72129559624699C85D5B64CCEF53`.
- Canonical split contract: 360 train / 80 valid / 60 held-out test images; five foreground classes.
- U-Net checkpoint: `C:\Git\unet\runs\segment\openvisionlab-unet-external-die-array-e30-20260721-203302\weights\best.pt`.
- YOLOv8-seg checkpoint: `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-external-die-array-e30-20260721-203302\weights\best.pt`.

## Evidence and findings

1. The 60-image test comparison artifact was deliberately created by the evidence runner with `confidence=0.0` for both adapters. Its YOLO manifest records that value. This is not the product-default Model Center path: `WpfSegmentationAdapterComparisonRunService` passes the selected profile's `MinimumDetectionConfidence`, whose fallback is `0.25`.
2. The Python Ultralytics exporter passes that confidence to `model.predict(conf=...)`, whereas the U-Net exporter returns the raw softmax argmax class map and does not use the same confidence operation. Therefore a single threshold is not a common calibration control for both adapters.
3. U-Net's `contamination_spot` and `foreign_particle` test Dice were zero, but their train support is not absent (46 and 48 positive images respectively). Their typical connected-component sizes (train medians 1,022 and 576 pixels) also show that a blanket "all targets are too small" conclusion is not supported. This needs a separate U-Net class-confusion or training-remediation study.
4. A validation-only YOLO replay was run twice on the same 80 images and the same checkpoint, once at `0.00` and once at the existing profile fallback `0.25`. No source or test artifact was modified. At `0.00`, every class occurred in all 80 prediction masks, producing large false-positive areas. At `0.25`, predicted-image counts and pixel Dice were:

   | Class | Predicted images at 0.00 | Dice at 0.00 | Predicted images at 0.25 | Dice at 0.25 |
   | --- | ---: | ---: | ---: | ---: |
   | contamination_spot | 80 | 0.041837 | 11 | 0.831769 |
   | scratch_crack | 80 | 0.028456 | 13 | 0.782156 |
   | missing_material | 80 | 0.188650 | 10 | 0.854240 |
   | foreign_particle | 80 | 0.000685 | 9 | 0.837950 |
   | extra_material_bridge | 80 | 0.050911 | 5 | 0.789878 |

   The corresponding prediction artifacts are:
   - `artifacts\segmentation-e30-error-analysis-20260721\yolov8-valid-confidence-000\prediction-manifest.jsonl`
   - `artifacts\segmentation-e30-error-analysis-20260721\yolov8-valid-confidence-025\prediction-manifest.jsonl`

## One controlled remediation hypothesis

**The YOLOv8-seg false-positive flood in the earlier common-mask report is primarily a raw-export `confidence=0.00` artifact.** Keep the checkpoint and canonical data fixed; make the comparison runner expose the YOLO confidence explicitly, select it only on `valid` (starting at the product profile default `0.25`), and then perform one final, unchanged `test` replay.

## Boundary and next dependency

This establishes a confidence-path diagnosis, not a model-adoption result and not a U-Net remedy. The held-out test split was not used to choose `0.25`. The future implementation must preserve that rule, keep the external source read-only, and keep the U-Net investigation as a separate hypothesis rather than combining unrelated changes.

## Commands actually run

```powershell
& C:\Git\yolov8\.venv\Scripts\python.exe Runtime\Python\openvisionlab_segmentation_prediction_export.py `
  --adapter ultralytics --engine YOLOv8 --data-root <existing canonical export> `
  --weights <fixed YOLOv8-seg best.pt> --split valid --output-root <new app artifact> `
  --image-size 320 --confidence 0.00 --device cpu

& C:\Git\yolov8\.venv\Scripts\python.exe Runtime\Python\openvisionlab_segmentation_prediction_export.py `
  --adapter ultralytics --engine YOLOv8 --data-root <existing canonical export> `
  --weights <fixed YOLOv8-seg best.pt> --split valid --output-root <new app artifact> `
  --image-size 320 --confidence 0.25 --device cpu
```
