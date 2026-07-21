# Floppy Disk / Hexagon 데이터셋 테스트 기록 (2026-07-21)

Status: Complete

## Scope

- 사용자가 제공한 Floppy Disk 500장 객체탐지 native YOLO 패킷을 YOLOv5s, YOLOv8n, YOLO11n으로 실제 학습·비교한다.
- 비ASCII Windows 경로의 비교 런타임 호환성을 검증하되, 원본 데이터 트리를 변경하지 않는다.
- 현재 hexagon 패킷이 바로 외부 native YOLO intake로 들어가는지 preflight한다.

이 기록은 파이프라인 통합 검증이다. 1-epoch 학습 결과를 모델 품질 또는 생산 채택 근거로 사용하지 않는다.

## Floppy Disk native YOLO packet

- Source: `D:\라벨테스트\EasyMatch_Floppy_Disk_500(1)\EasyMatch_Floppy_Disk_500\object_detection\data.yaml`
- Contract: 500 unique synthetic images, 360 train / 80 val / 60 test, 5 defect classes, matching YOLO bbox label files.
- Source `data.yaml` SHA-256: `5FDDE223194CF28E42564EE705C85250BF492942EC69396C7A1069C466E9FAA3`.
- Source tree: 1,004 files, SHA-256 `E0F44C01C74947E8C8F3DF9BB3CA4CDE2AA89AF4123803F78BCD900DA1C24E8B` before and after every completed training run.

## Completed runs

| Engine | Run | Result |
| --- | --- | --- |
| YOLOv5s | `openvisionlab-floppy-disk-500-yolov5s-detect-e1-20260721-123554` | completed; `best.pt` SHA-256 `65D7A6B0DED04FDAA0A58590C35B553D57934F33EE3763F1D52DDD827CEA05AE` |
| YOLOv8n | `openvisionlab-floppy-disk-500-yolov8n-detect-e1-20260721-123819` | completed; `best.pt` SHA-256 `E5C2C8E3ADAABF1C14B3703C22A0BCBC7A786ED84733A35ACA21AA678AFABCB1` |
| YOLO11n | `openvisionlab-floppy-disk-500-yolo11n-detect-e1-20260721-123931` | completed; `best.pt` SHA-256 `3F513B3D3E7B133EF34C72B7D663CD65B9E835B9E06B4B8E16B5AF3A4931B00D` |

An initial 30-epoch YOLOv5 attempt was intentionally stopped after the command-host 6-minute limit exposed CPU-only throughput. Its partial run is not used as evidence. The completed 1-epoch runs are the bounded integration test.

## Same-test comparison results

All comparisons used the same 60-image test split, image size 320, batch 1, UI confidence 0.25, and one timing sample.

| Pair | Model | Precision | Recall | mAP50 | mAP50-95 | Model takt |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| v5 ↔ v8 | YOLOv5 | 0.000 | 0.025 | 0.000 | 0.000 | 86.800 ms |
| v5 ↔ v8 | YOLOv8 | 0.002 | 0.508 | 0.125 | 0.081 | 43.598 ms |
| v8 ↔ v11 | YOLOv8 | 0.002 | 0.508 | 0.125 | 0.081 | 41.445 ms |
| v8 ↔ v11 | YOLO11 | 0.000 | 0.206 | 0.049 | 0.019 | 45.146 ms |

- v5 ↔ v8 evidence: `artifacts\yolo-model-comparison\floppy-disk-500-yolov5s-vs-yolov8n-e1-test-20260721-124558\20260721-124609\comparison-report.md`.
- v8 ↔ v11 evidence: `artifacts\yolo-model-comparison\floppy-disk-500-yolov8n-vs-yolo11n-e1-test-20260721-125103\20260721-125108\comparison-report.md`.
- Every comparison decision is `engine-benchmark`; no model was automatically selected.

## Windows non-ASCII path contract

YOLOv5 validation failed on the original Korean path because its OpenCV loader could not open the image. `scripts/compare-yolo-models.ps1` now creates an artifact-local ASCII copy only when the source path is non-ASCII. Both validation and candidate-review prediction use that copy; source `data.yaml`, images, labels, source tree SHA-256, and source cache count remain unchanged. The comparison report records the exact runtime copy path.

## Actual EXE result

The current Debug EXE created a new object-detection recipe, activated the actual Floppy Disk external `data.yaml`, saved the YOLO11 1-epoch profile, restarted, and completed inference. Saved/reopened/inferred recipe SHA-256 was `C04B649846D7536A0BB1412F22A86ED40F0177E1BDD5258913062B42174A07AC`.

The result was `모델 YOLO11 ... 후보 0`. The explicit `--allow-empty-candidates` smoke option accepts that only for a known weak test model while still requiring inference completion, engine/weight identity, persistence, and no runtime failure. Default smoke behavior still requires one or more UI-threshold candidates.

Evidence: `artifacts\exe-yolo11-detect-restart-smoke\floppy-disk-e1-allow-empty-20260721-125714\summary.txt`.

## Hexagon defect 8-class list-split intake (completed)

The user selected **defect 8 classes**, not the unused ten-shape object contract. The source is:

`D:\라벨테스트\multishape_defect_labeling_dataset_v3_hexagon_500\multishape_defect_labeling_dataset_v3_hexagon_500\defect_dataset.yaml`

The packet uses `splits/detection/{train,val,test}.txt` rather than image directories. It contains both `labels/yolo_defect_detection` and `labels/yolo_defect_segmentation`; intake now chooses the purpose-matching `yolo_defect_detection` root for ObjectDetection and fails closed if the matching root is still ambiguous. It then materializes an app-owned standard YOLO copy only when training starts:

`<recipe-output>\external-yolo-runtime\external-yolo-runtime-<source-fingerprint>\data.yaml`

The selected source YAML remains the persisted profile source, while the actual runtime YAML is recorded separately in training provenance. No extra UI button was added: the existing external `data.yaml` select/activate flow remains the entry point.

### Source contract and immutability

- Classes: `scratch`, `crack`, `dark_contamination`, `bright_contamination`, `discoloration`, `edge_chip`, `extra_material`, `rectangular_void`.
- Split: train 334 / val 66 / test 100; labels 500; annotations 438.
- `defect_dataset.yaml` SHA-256: `D70BEC12C9D3CF02779C9B71A4CFEF53F6FEDAFD0965D2A59E8B937CD142A69E`.
- Source tree: 4,525 files, SHA-256 `4E511A2E08F2ED609B78B40D6B789DE691C968E71ED5A298B76A1E7CA1FB52A8` before and after every completed run; zero source cache files and zero source temporary runtime directories.

### Completed bounded runtime runs

These are one-epoch compatibility runs, not model-quality or adoption evidence.

| Engine | Run | Best weight SHA-256 | Evidence |
| --- | --- | --- | --- |
| YOLOv5s | `openvisionlab-hexagon-defect8-yolov5s-e1-20260721-134400` | `B6CD0F13A6EAE9BA7D5CDAAC1B280EAA2D8A76BA4BA474CB702921F473FD5425` | `artifacts\real-external-yolo-dataset-training\hexagon-defect8-yolov5s-e1-20260721-134400\summary.txt` |
| YOLOv8n | `openvisionlab-hexagon-defect8-yolov8n-e1-20260721-134100` | `2E33E1A93370D5979B7217B3C8EF03A3250A069AE060C996F034C3E6E50773E0` | `artifacts\real-external-yolo-dataset-training\hexagon-defect8-yolov8n-e1-20260721-134100\summary.txt` |
| YOLO11n | `openvisionlab-hexagon-defect8-yolo11n-e1-20260721-134700` | `A6736F15BF139297254DCA067A3DD40D36437BDCAA36BEE8EE9B496170EC63F2` | `artifacts\real-external-yolo-dataset-training\hexagon-defect8-yolo11n-e1-20260721-134700\summary.txt` |

Each completed worker received an app-owned runtime `data.yaml`; each profile preserves both the original YAML and the runtime YAML path. All three workers reported `completed` and produced `best.pt` outside the user source tree.

### Current EXE proof

The freshly built Debug EXE selected and activated the actual `defect_dataset.yaml`, persisted it across restart, and completed YOLO11 inference using the 1-epoch weight. The saved, reopened, and inferred `VISION.xml` SHA-256 values were all `A65A54FC64F52BB1B560BCE9179392375620F7AC2B52A13E210CF1E14BAC8414`.

- UI screenshot: `artifacts\exe-yolo11-detect-restart-smoke\hexagon-defect8-e1-20260721-135200\screenshots\02b_external_yolo_data_activated.png`.
- Summary: `artifacts\exe-yolo11-detect-restart-smoke\hexagon-defect8-e1-20260721-135200\summary.txt`.
- The inference completed with `후보 0`; that is accepted only by this explicit weak 1-epoch smoke option and is not a quality claim.

## Hexagon 8-class GPU 30-epoch comparison (completed)

All three models completed real training with the same source, 30 epochs, image size 320, batch 4, and CUDA device 0 (GTX 1060 3GB). Each completed runtime summary records the original source YAML, the app-owned runtime YAML, the worker executable, the model seed and the trained `best.pt`. The original source remained unchanged in every run: 4,525 files and SHA-256 `4E511A2E08F2ED609B78B40D6B789DE691C968E71ED5A298B76A1E7CA1FB52A8` before and after.

| Engine | 30-epoch run | Best weight SHA-256 | Evidence |
| --- | --- | --- | --- |
| YOLOv5s | `openvisionlab-hexagon-defect8-yolov5s-gpu-e30-20260721-143400` | `DBC7983C80CB9BBC7D3422D922A05D6AB350FC56D52D7D0E5CECAB2D12FB2BF6` | `artifacts\real-external-yolo-dataset-training\hexagon-defect8-yolov5s-gpu-e30-20260721-143400\summary.txt` |
| YOLOv8n | `openvisionlab-hexagon-defect8-yolov8n-gpu-e30-20260721-144600` | `E1954775F941232A7A8D29BCAADB2F3A8AFE8B05D3364389D408EB3708291D0C` | `artifacts\real-external-yolo-dataset-training\hexagon-defect8-yolov8n-gpu-e30-20260721-144600\summary.txt` |
| YOLO11n | `openvisionlab-hexagon-defect8-yolo11n-gpu-e30-20260721-145200` | `457B14261003D3557E4D961771F02EAB30374726682AA851A693562B773F65DC` | `artifacts\real-external-yolo-dataset-training\hexagon-defect8-yolo11n-gpu-e30-20260721-145200\summary.txt` |

The standard system Python had CUDA-capable Torch but an older Ultralytics package that could not deserialize YOLO11. To avoid a global package upgrade, the run created `C:\Git\yolov8\.venv-gpu` with Ultralytics 8.4.101 and shared CUDA Torch 2.0.1+cu117. The local YOLOv5 worker also had a NumPy 1.25 compatibility defect in `utils/metrics.py`; its eager fallback to `np.trapezoid` was replaced by a compatible `np.trapezoid`/`np.trapz` selection before the final completed run.

### Held-out test comparison

Both pairs evaluated the same 100-image held-out test split with image size 320, batch 4, UI threshold 0.25, and three native-validation timing samples on CUDA:0. The comparison runtime YAML and any cache were app artifacts; no source image, label, YAML, cache, or temporary source directory was retained or changed.

| Model | Precision | Recall | mAP50 | mAP50-95 | Median takt ms/image | Interpretation |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| YOLOv5s | 0.432 | 0.709 | 0.594 | 0.439 | 6.700 | Baseline engine benchmark only |
| YOLOv8n | 0.854 | 0.795 | 0.869 | 0.740 | 4.621 / 4.640 | Best overall precision and mAP on this packet |
| YOLO11n | 0.672 | 0.881 | 0.783 | 0.644 | 4.610 | Slightly lower median takt, but more false positives at UI threshold |

- YOLOv5s ↔ YOLOv8n: `artifacts\yolo-model-comparison\hexagon-defect8-yolov5s-vs-yolov8n-gpu-e30-test-20260721-145800\20260721-145326\comparison-report.md`.
- YOLOv8n ↔ YOLO11n: `artifacts\yolo-model-comparison\hexagon-defect8-yolov8n-vs-yolo11n-gpu-e30-test-20260721-150100\20260721-145548\comparison-report.md`.
- Every report decision is `engine-benchmark`. The results do **not** automatically replace a user's active model. For this synthetic packet, YOLOv8n is the evidence-backed default comparison recommendation; a production choice still requires real held-out camera data and an agreed false-positive/false-negative acceptance gate.

### Current EXE proof with the 30-epoch YOLO11 weight

After a fresh Debug EXE build, the actual EXE created an object-detection recipe, selected and activated the original external `defect_dataset.yaml`, saved the GPU runtime and YOLO11 30-epoch weight, restarted, and completed inference on held-out `hexagon_NG_201.png`. No `--allow-empty-candidates` exception was used.

- Saved/reopened/inferred `VISION.xml` SHA-256: `E345DB4BD7D2CEDF28FEB8562BFE85EDBA4817EDC9E616178BFA6B7AC9A422E5`.
- Inference status: `모델 YOLO11 ... 후보 1`; the shown candidate is `scratch` at 97.1% confidence.
- UI evidence: `artifacts\exe-yolo11-detect-restart-smoke\hexagon-defect8-gpu-e30-20260721-150000\screenshots\04_first_inference_after_restart.png`.
- Summary: `artifacts\exe-yolo11-detect-restart-smoke\hexagon-defect8-gpu-e30-20260721-150000\summary.txt`.

## Boundary / next dependency

Status: Complete

This completes the agreed synthetic Hexagon 8-class 30-epoch training, held-out engine comparison, and actual EXE inference proof. Floppy Disk and Hexagon remain synthetic pipeline datasets, not production-quality evidence. The next distinct priority is to repeat the same recipe with a real labeled camera holdout and an explicit acceptable false-positive/false-negative rate; only then can a model be adopted for a production recipe.
