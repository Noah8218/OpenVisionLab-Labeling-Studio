# Circular Disk Synthetic 1,000-Image Evidence

Date: 2026-07-20

## Closure

Status: Complete

Scope: Profile the supplied 500 OK / 500 NG circular-disk package, preserve its source tree, evaluate the existing anomaly candidate, train and compare one new anomaly candidate through the application/TCP path, prepare exact metadata-backed YOLO detection labels, prove one-epoch YOLOv5/YOLOv8 detection training connectivity, and complete a controlled 20-epoch YOLOv5s/YOLOv8n test benchmark. This scope does not adopt a model or claim independent production-camera accuracy.

## Supplied source and provenance

- Source: `D:\라벨테스트\circular_defect_labeling_dataset_v1_complete\Circular_Disk_OK500_NG500_Images`.
- Images: 1,000 JPEGs, 500 OK and 500 NG, all 512x512 and all decodable.
- Integrity: 1,000 unique contents, zero missing metadata rows, and zero metadata MD5 mismatches.
- Exact-file overlap with the earlier 100-image set: zero generated JPEG matches.
- Acquisition boundary: the packaged `source\OK_0001.png` is byte-identical to the earlier training source `images\OK\OK_0001.png`, SHA-256 `4689528FDD5731403FCFDCDA81EC2AD04D130D68B253793E869D3618A727E981`.
- The supplied summary says the 1,000 images were procedurally generated from that one OK image. This is a new synthetic variation set, not an independent camera/session holdout.
- The full supplied tree remained 1,009 files / 47,398,952 bytes with unchanged path+length+content fingerprint SHA-256 `A8F983571830588CF88D2EB64ED582725123115CFA479F79770EF3C08F05A1EF` before and after all work.

## Generator reproducibility boundary

The packaged generator was rerun in a separate artifact root. Its own validation passed and it generated a complete detection/segmentation/anomaly package, but all 1,000 regenerated JPEG hashes differed from the supplied JPEGs. The split and class schedule still matched 1,000/1,000 rows.

Therefore:

- Do not attach the regenerated masks or polygons to the supplied JPEGs.
- The supplied `metadata.csv` bounding boxes remain usable because every row is tied to the exact supplied image MD5.
- Exact supplied-image segmentation ground truth is unavailable in the image-only package.
- `generator_seed=20260719` is not sufficient evidence of byte-identical cross-environment regeneration. The exact environment-dependent cause was not proven.

The separate regenerated dataset is retained only as diagnostic evidence under `artifacts\datasets\circular-disk-synthetic-complete-20260719-235945`.

## Existing anomaly model on the supplied synthetic test

Existing model: `artifacts\verification\anomaly-retrain-20ep-20260719\training\best.pt`, SHA-256 `6C3F3910BB7ADBEE2CD21039E12DF063E328448CDB21FAFC2195472AD016FF3D`.

The metadata-defined anomaly test contains 100 normal `test_good` images and all 500 `test_ng` images. At minimum confidence 0.8:

- Thresholded correct: 111/600, 18.5%.
- Normal: 62/100; abnormal: 49/500.
- Automatic decisions: 320; unreviewed: 280.
- Automatic false OK: 209; automatic false NG: 0.
- Raw top-1 correct without abstention: 192/600; raw false OK: 408.
- Defect raw abnormal predictions: contamination 22/100, edge chip 23/100, foreign particle 46/100, ring deformation 0/100, scratch/crack 1/100.
- Result: `hold`.

Evidence: `artifacts\external-anomaly-evaluation\circular-disk-supplied-synthetic-1000-20260720-000459\evaluation-current-source-batch-timing\classification-evaluation-20260720-001826\classification-evaluation-summary.json`.

## New anomaly candidate

The existing application classification export, review-folder import, training workflow, TCP listener, and local YOLOv8 adapter trained a new 20-epoch candidate from all 1,000 reviewed images.

- App split seed: 17.
- Train: 677 (normal 328 / abnormal 349).
- Validation: 219 (normal 118 / abnormal 101).
- Test: 104 (normal 54 / abnormal 50).
- SHA-256 content overlap between train/validation/test: zero.
- Training: YOLOv8 classify, 20 epochs, image size 128, batch 16.
- Best validation epoch: 17; top-1 0.90868; validation loss 0.24494.
- Candidate SHA-256: `636D1AC7892C252594909AECF4C2E4F71753204F1C3DDE06FEA78F77BE782B95`.

Identical 104-image test comparison at confidence 0.8:

| Model | Thresholded | Normal | Abnormal | Raw top-1 | False OK | Unreviewed | Result |
|---|---:|---:|---:|---:|---:|---:|---|
| Earlier 100-image candidate | 34/104 (32.7%) | 29/54 | 5/50 | 60/104 | 21 | 49 | hold |
| New 1,000-image candidate | 90/104 (86.5%) | 52/54 | 38/50 | 94/104 | 7 | 7 | hold |

All seven confidence-0.8 false OK cases are `ring_deformation`. The new candidate remains `hold` because overall thresholded accuracy is below 0.9, abnormal accuracy is 0.76 below 0.8, and four correct class matches fall below confidence 0.8. A diagnostic threshold of 0.9 removes false automatic decisions on this test but reduces automatic coverage to 39/104; this is not an adopted threshold because it was observed on the held-out test.

Evidence root: `artifacts\real-yolov8-anomaly-folder-training\circular-disk-supplied-1000-e20-20260720-000642`.

## Exact supplied-image object detection dataset

`metadata.csv` contains one complete bounding box and one of five class IDs for every NG image. An exact YOLO dataset was built from the supplied JPEGs after validating every image MD5.

- Artifact: `artifacts\datasets\circular-disk-supplied-1000-yolo-detect-20260720-000412`.
- Train: 700 images / 700 label files (350 OK empty labels / 350 NG boxes).
- Validation: 150 / 150 (75 OK / 75 NG).
- Test: 150 / 150 (75 OK / 75 NG).
- Classes: contamination spot, scratch/crack, edge chip, foreign particle, ring deformation.
- Invalid YOLO labels: zero.
- Artifact fingerprint at preparation: `849C5619EA1C6B94DB28A179D071CB2A33BFA81F5FBB68421D9248E8E5DBE167` before the later provenance file was appended.

One-epoch application/TCP training proofs used the same native `data.yaml`, image size 128, and batch 16:

| Engine | Training result | best.pt SHA-256 | Data source before/after |
|---|---|---|---|
| YOLOv8 Detect | completed | `810003C29BB1840FB5D5265B6A25A40B151EFB3705F81E1520E42A3F1CD0F231` | 2,005 files; SHA-256 unchanged `573F0E76D2EB282A54BB136F1AC11C5F1584E68685095F312A0508444CC4FA60` |
| YOLOv5 Detect | completed | `97B96ECB337BEB5EE7D120DA4984D7DE2ED5F5702F2EB27093867D6CD13EDC7E` | same 2,005 files and SHA-256 |

Both one-epoch models scored zero mAP in their training validation output. They prove intake, formatting, TCP dispatch, engine-specific training, weight creation, source immutability, and provenance only. They are not quality candidates and cannot be ranked from this run.

Evidence:

- `artifacts\real-external-yolo-dataset-training\circular-disk-supplied-1000-yolov8-detect-e1-20260720-001354\summary.txt`
- `artifacts\real-external-yolo-dataset-training\circular-disk-supplied-1000-yolov5-detect-e1-20260720-001500\summary.txt`

## Controlled 20-epoch YOLOv5s versus YOLOv8n detection benchmark

Status: Complete

The exact metadata-backed detection dataset was trained through the application/TCP route with both program-default profiles: YOLOv5s and YOLOv8n. Both runs used the same native YAML, train/validation/test split, seed-weight family, CPU execution, 20 epochs, image size 320, and batch 4. The dataset artifact was 2,005 files before and after each training run; its runner-compatible tree SHA-256 remained `573F0E76D2EB282A54BB136F1AC11C5F1584E68685095F312A0508444CC4FA60`.

| Engine / profile | Training artifact | best.pt SHA-256 |
|---|---|---|
| YOLOv5s | `artifacts\real-external-yolo-dataset-training\circular-disk-supplied-1000-yolov5-detect-e20-20260720-071900` | `C9D5E29F1DBFA7038042BC09CB357724A4F1B43E5FF23A2F096E9964354BB1BC` |
| YOLOv8n | `artifacts\real-external-yolo-dataset-training\circular-disk-supplied-1000-yolov8-detect-e20-20260720-065500` | `68D396967A21EE3311C79B478C235C80525A38C854C287EECC6CFE33B1994340` |

The final comparison used the untouched 150-image test split (75 labeled NG boxes / 75 empty-label OK images), test image-label fingerprint `C841BDE81B17DADC7AB1AABE640DC096A6B7006AA7FCBD6A54C81C4372BE06D6`, confidence 0.25, prediction NMS IoU 0.45, ground-truth IoU 0.5, and two native validation timing samples per engine.

| Engine | Precision | Recall | mAP50 | mAP50-95 | Median model takt | UI-threshold TP / FP / FN |
|---|---:|---:|---:|---:|---:|---:|
| YOLOv5s | 0.879 | 0.852 | 0.900 | 0.567 | 52.45 ms/image (52.0-52.9) | 61 / 5 / 14 |
| YOLOv8n | 0.885 | 0.926 | 0.955 | 0.678 | 27.575 ms/image (27.410-27.739) | 67 / 7 / 8 |

The comparison script initially exposed a reproducibility defect: YOLOv5 validation left `labels\test\NG.cache` under the source tree. The generated cache was removed, the exact original 2,005-file manifest was restored, and the script was corrected to retain any pre-existing cache but remove only cache artifacts created by its own run in `finally`. The fixed rerun recorded the one removed generated cache, left no cache file, and ended with the same source-tree SHA-256. Cross-engine `test` reports now use `comparisonKind=engine-benchmark` and `promotion.recommendation=benchmark`; they explicitly do not auto-replace the inspection model.

Final comparison evidence:

- `artifacts\yolo-model-comparison\circular-disk-supplied-1000-yolov5-vs-yolov8-e20-test-fixed-20260720-085900\20260720-082213\comparison-summary.json`
- `artifacts\yolo-model-comparison\circular-disk-supplied-1000-yolov5-vs-yolov8-e20-test-fixed-20260720-085900\20260720-082213\comparison-report.md`

## Large anomaly evaluation performance

`scripts\evaluate-yolo-classification.ps1` now defaults to `Runtime\Python\openvisionlab_yolo_classification_batch.py`, which imports the selected local YOLO adapter and reuses one loaded detector. `-UseLegacyPerImageWorker` preserves the old smoke-test path for equivalence checks.

- Legacy measured cold path: 5.42s and 5.16s for two images, dominated by about 4.3s model loading each time.
- New persistent-adapter path: 600 images in 15,214ms, average 25.36ms/image.
- Two-image class names and confidence values matched the legacy path exactly, with zero confidence delta.

## Boundary and next dependency

- Anomaly candidate adoption remains blocked by false OK ring-deformation cases and by the lack of independent production-camera/cross-session data.
- The controlled YOLOv5s versus YOLOv8n comparison is complete only as same-source synthetic engine evidence. It uses different default model capacities and one procedurally generated acquisition source, so it must not select a production inspection model.
- Production-quality claims still require independent real camera/session images. The supplied synthetic set closes a model-format and training-path evidence gap, not the independent-data gap.

## Controlled 30-epoch YOLOv5s versus YOLOv8n detection benchmark and EXE walkthrough

Status: Complete

One object-detection recipe and one exact native `data.yaml` were used for both profiles. This is intentional: the recipe owns the user's workflow and the external source contract, while the selected model profile owns the YOLOv5/YOLOv8 runtime. Creating separate per-engine recipes would make the split, label schema, or source identity easier to diverge and would not make the comparison fair.

The shared dataset contains 700 train, 150 validation, and 150 untouched test images; the five native defect classes are `contamination_spot`, `scratch_crack`, `edge_chip`, `foreign_particle`, and `ring_deformation`. The native YAML SHA-256 is `C620DE2E9A32AD5D1817B9F6AE37309796A242B06B0040E3DF8F00D66AF03983`; the held-out image/label fingerprint is `C841BDE81B17DADC7AB1AABE640DC096A6B7006AA7FCBD6A54C81C4372BE06D6`.

| Profile | Epochs | Precision | Recall | mAP50 | mAP50-95 | Native median model takt |
|---|---:|---:|---:|---:|---:|---:|
| YOLOv5s | 30 | 0.915 | 0.872 | 0.916 | 0.618 | 91.550 ms/image |
| YOLOv8n | 30 | 0.954 | 0.914 | 0.965 | 0.708 | 43.941 ms/image |

- YOLOv5s artifact: `artifacts\real-external-yolo-dataset-training\circular-disk-supplied-1000-yolov5s-detect-e30-20260720-131140`; `best.pt` SHA-256 `D60B111A3B2522D6EDBAEC24666F78C0C88D94CB2507268BAEE991E0570D31F2`.
- YOLOv8n artifact: `artifacts\real-external-yolo-dataset-training\circular-disk-supplied-1000-yolov8n-detect-e30-20260720-122444`; `best.pt` SHA-256 `09DCE10B3F1531837AB1CE8530BB177B368DD483989D68C672F57D4FD2BC904F`.
- Both training summaries record the same 2,005-file formatted-data tree SHA-256 before and after training: `573F0E76D2EB282A54BB136F1AC11C5F1584E68685095F312A0508444CC4FA60`. No source cache or temporary training directory remained.
- The controlled test comparison is `comparisonKind=engine-benchmark` and its recommendation is `benchmark`, not automatic model replacement. The comparison runner removed exactly one YOLOv5 cache file that it created and left no cache artifact in the source tree.
- Evidence: `artifacts\yolo-model-comparison\circular-disk-supplied-1000-yolov5s-vs-yolov8n-e30-test-20260720-143520\20260720-143309\comparison-summary.json` and `comparison-report.md`.

### First-user EXE walkthrough

The current Debug EXE was exercised from a new object-detection recipe: create recipe, select and explicitly activate the native YAML, save the YOLOv8 profile, load the configured image root, restart the EXE, enter AI candidate review, and run the first inspection. The saved, reopened, and post-inference recipe SHA-256 values were identical. The first supplied NG image produced one candidate through the trained YOLOv8n model.

The status now identifies the engine and the real training run rather than only the ambiguous filename: `YOLOv8 / circular-disk-supplied-1000-yolov8n-detect-e30-20260720-122444\\best.pt / 후보 1`. The native class schema is persisted separately from the recipe class catalog and is used by comparison review when a report supplies its own class metrics.

- EXE summary: `artifacts\ui\circular-disk-yolov8-beginner-e30-current-final4-20260720\summary.txt`.
- Current EXE captures: `screenshots\01d_created_dataset_purpose_state.png`, `02b_external_yolo_data_activated.png`, and `04_first_inference_after_restart.png` under the same artifact root.

Boundary: YOLOv8n is better on this controlled synthetic holdout, including the weakest `ring_deformation` class, but this is still a single procedurally generated acquisition source. It is suitable for engine/profile learning and repeatable UI verification, not production model adoption.
