# Stable Verified Areas

This document records code paths that have already been performance- or UX-verified and should not be casually refactored. Treat these areas as protected product behavior: change them only when the user reports a new issue in that exact path, or when a focused verification gate proves the change is necessary.

## Native YOLOv5/YOLOv8 Comparison Source-Immutability Contract

Status: stable as of 2026-07-20 for cross-engine native test comparison cache cleanup and benchmark-only reporting.

- `scripts\compare-yolo-models.ps1` must preserve any pre-existing YOLOv5 cache artifact and remove only `labels.cache`/`.npy` files created by its own current run, including after a validation failure.
- A cross-engine comparison is always `engine-benchmark`, including an independent `test` split. Its report may compare metrics and timing but must never issue a candidate promotion or automatically replace the inspection model.
- The exact 150-image circular-disk test rerun recorded one generated `labels\test\NG.cache` cleanup and finished with the same 2,005-file source-tree SHA-256 `573F0E76D2EB282A54BB136F1AC11C5F1584E68685095F312A0508444CC4FA60` and zero remaining cache files.
- Covered by the required isolated build, `--wpf-model-comparison-run-service`, PowerShell parse, and `git diff --check`. Runtime evidence: `artifacts\yolo-model-comparison\circular-disk-supplied-1000-yolov5-vs-yolov8-e20-test-fixed-20260720-085900\20260720-082213\comparison-summary.json`.

## Persistent-Adapter Anomaly Classification Evaluation

Status: stable for large local YOLOv8 classification evaluation as of 2026-07-20. This is evaluator runtime and evidence-contract proof, not model-adoption proof.

Protected behavior:

- `scripts\evaluate-yolo-classification.ps1` must preserve the selected local adapter as the candidate-mapping authority while loading its detector only once by default.
- Keep `-UseLegacyPerImageWorker` as an explicit equivalence/debug fallback; do not return to one Python/model start per image for normal dataset evaluation.
- Preserve `classification-evaluation-summary.json`, weights SHA-256, class/image content fingerprint, thresholds, samples, promotion reasons, `evaluationMode`, `evaluationElapsedMs`, and average duration.
- Confidence-gated class matches below the minimum remain incorrect evidence and must keep adoption on hold.

Required gates:

```powershell
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile .\Runtime\Python\openvisionlab_yolo_classification_batch.py
C:\Git\yolov8\.venv\Scripts\python.exe .\Runtime\Python\openvisionlab_yolo_classification_batch.py --self-test
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
git diff --check
```

Latest evidence:

```text
PASS Two supplied images: persistent-adapter and legacy per-image paths returned identical class names and confidence values.
PASS 600-image persistent-adapter run: 15,214ms total, 25.36ms/image.
Evidence: artifacts\external-anomaly-evaluation\circular-disk-supplied-synthetic-1000-20260720-000459\evaluation-current-source-batch-timing\classification-evaluation-20260720-001826\classification-evaluation-summary.json
```

## Task-Aware Model Runtime Profile Transition

Status: stable for current-build profile selection, safe runtime-path mapping, and anomaly training summary as of 2026-07-19. This is runtime configuration evidence, not model-quality or model-adoption evidence.

Protected behavior:

- A user profile choice and the profile-card action use the same transition path. Programmatic recipe loading must not open a folder picker.
- Reuse an already-selected valid target folder or a valid sibling named `yolov8`/`yolov5`; otherwise request one explicit folder selection.
- When anomaly training is started from a legacy YOLOv5 recipe and a train-ready sibling `yolov8` exists, connect and persist that runtime automatically, then allow the existing worker-signature restart path to replace the stale YOLOv5 process.
- Never send `task=classify` with the YOLOv5 adapter. The workflow safety guard must block before export/TCP even if UI routing regresses.
- Change only the model engine, Python executable, model runtime folder, and worker script during a runtime transition.
- Preserve the recipe image root and current inspection-model path. Never auto-select a `runs/**/best.pt` or model-root image folder merely because a runtime was connected.
- Derive the visible training task and seed from dataset purpose: Detect/`yolov8n.pt`, SEG/`yolov8n-seg.pt`, or Classify/`yolov8n-cls.pt`.
- Keep a trained `best.pt` as a candidate until the existing explicit review/adoption workflow confirms it.
- Rebuild generated `classification/train|valid|test` splits on each preparation; repeated starts must not duplicate prior export files.
- Present anomaly readiness from reviewed Normal/Abnormal images and their split ownership, not from object-detection box-label counts.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-connection
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-settings-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
PASS Current image and inspection model survive runtime switching even when newer unrelated runtime assets exist: --python-model-runtime-connection
PASS User dropdown transition, model settings bindings, and current WPF shell: --wpf-yolo-model-settings-panel and --wpf-labeling-shell
PASS Detect/SEG/Classify task-aware summaries and real classify training packet: --wpf-training-settings-panel and --anomaly-classification-training-workflow
PASS Repeated anomaly export replacement and image-level readiness: --anomaly-classification-dataset-export and --dataset-readiness-purpose
PASS Current local YOLOv8 worker: labeling_tcp_client.py --self-test
PASS Supplied 100-image real 1-epoch YOLOv8 classify run: artifacts\verification\anomaly-training-fix-20260719\real-yolov8-anomaly-1epoch\summary.txt
PASS Current-source 1920x1080 captures: artifacts\ui\model-runtime-profile-ux-20260719\after-runtime-details-1920.png and after-training-1920.png
```

## Anomaly OK/NG Image-Level Review Workspace

Status: stable for current-build manual image-level anomaly review, explicit folder consent, persistence, and purpose-specific UI as of 2026-07-19. This is a labeling/review workflow contract, not model-quality evidence.

Protected behavior:

- Describe the operator task as `OK/NG 이미지 판정`: the image as a whole is Normal/Abnormal, and the operator does not draw a box, polygon, or mask.
- In anomaly purpose, hide object/segmentation annotation tools, label-save actions, class/object editors, label-layer controls, and queue detection/batch controls. Restore them when the purpose returns to object detection or segmentation.
- Keep the three current-image actions in Image Queue: `정상(OK) → 다음`, `이상(NG) → 다음`, and `미판정으로 되돌리기`. A saved OK/NG decision advances to the next unreviewed image; an explicit later decision overrides the earlier one.
- Present anomaly rows as `판정` and `상태` with `OK`, `NG`, or `미판정` badges. Generic YOLO label/detection refreshes and inference candidates must not overwrite this image-level queue presentation.
- Persist only reviewed states in the existing `anomaly-review-status.json`. Training readiness, manifest, dashboard, and classification export continue to consume that saved state; the dedicated buttons do not create YOLO annotation files.
- Persist `anomaly-review-status.json` immediately, but do not rescan the dataset to rebuild the derived manifest on each decision. The existing recipe-save path owns manifest regeneration.
- Update a reviewed row through the configured live-filter properties and load the next image from the existing queue. Do not call `ICollectionView.Refresh()` or repopulate the queue in the per-decision path.
- Treat `OK`/`normal` and `NG`/`abnormal` parent names as a proposal only. `N장 일괄 판정` affects unreviewed images only, `이미지별 확인` leaves them unreviewed, and saved manual decisions always win.
- Preserve the operator-selected image root when the first nested image or a later row is opened.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-anomaly-queue-focus
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-folder-auto-review
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-anomaly-purpose-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose anomaly --review-tab labeling-guide --anomaly-review-only --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\anomaly-ok-ng-review-20260719\after-staged-slice-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
PASS Dedicated OK/NG command, next-unreviewed, override, clear, restart persistence, selected-root, bulk-manual precedence, and purpose restore: --anomaly-folder-auto-review
PASS Generated 100-image/18-decision queue-local transition: zero view resets, one filter evaluation, zero queue population, 160.0ms median, 244.1ms maximum, persisted 9 Normal/9 Abnormal, and zero resource warnings: --wpf-anomaly-queue-focus
PASS Supplied 100-image 512x512 circular dataset: 223.4ms median, 290.5ms maximum, same queue-local invariants, and unchanged image-tree SHA-256 99D310FCC1CCB36F8CE3D2363ACE21F117C17F54B18A4CEF80E75B92DB3B43E7
PASS Existing anomaly status, manifest, dashboard, completion, and inference mapping contract: --wpf-anomaly-purpose-flow
PASS Exact staged-tree before/after 1920x1080 captures for commit 85d91e9: artifacts\ui\anomaly-ok-ng-review-20260719\before-anomaly-review-1920.png and after-staged-slice-1920.png
```

## Dataset Health Separate Read-Only Analysis

Status: stable for current-build purpose-aware dataset-health aggregation and its separate Model Center analysis window as of 2026-07-17. This is data-readiness visibility, not a model-quality or adoption approval.

Protected behavior:

- Open `데이터셋 상태 분석` from `Model Center > 데이터 > 분석` as an owned `FluentWindow`; do not add these detailed tables back into the left workflow panel.
- Reuse `YoloDatasetReadinessService`, `YoloDatasetQualityAuditService`, `YoloDatasetDiagnosticsService`, and `AnomalyClassificationTrainingReadinessService` as read-only inputs. Do not change queue navigation, annotation save, mask generation, worker, profile, or adoption paths while refreshing this view.
- Preserve purpose semantics: object detection uses box labels, segmentation uses saved segment/mask artifacts and segment class counts, and anomaly classification uses image-level normal/abnormal review state instead of a fabricated YOLO label table.
- For segmentation, never infer `라벨 품질: 정상` merely because the box-label audit has no rows. Reuse readiness validation to distinguish `정상`, a missing/corrupt SEG annotation problem count, and `미확인` when configuration or image coverage prevents evaluation; expose SEG missing/corrupt counts in the matching split row.
- Keep external native `data.yaml` outside this report. It is an explicit training-input profile and has its own validation workflow.
- Keep the model-evidence boundary visible: healthy saved data does not prove accuracy, Takt, held-out comparability, or model adoption.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-health
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-health-window
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-quality-audit
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-readiness-purpose
git diff --check
```

Latest evidence:

```text
PASS Current-build 1920x1080 normal-state capture: artifacts\ui\dataset-health-20260717\after-dataset-health-populated-ready-1920.png
PASS Object detection, segmentation, and anomaly report fixture coverage, including valid/missing/corrupt/not-evaluated SEG quality states: --dataset-health
PASS Separate owned WPF FluentWindow, three tabs, four metrics, split/class grids, and refresh binding: --wpf-dataset-health-window
PASS Existing readiness and quality-audit contracts: --dataset-readiness-purpose and --dataset-quality-audit
PASS Current-source corrupt-SEG capture shows `라벨 품질: 1`, not `정상`: artifacts\ui\dataset-health-seg-quality-false-normal-after.png
PASS Focused current-source recheck: 0-warning/0-error isolated build plus --dataset-health, --wpf-dataset-health-window, --dataset-readiness-purpose, --dataset-quality-audit, and --wpf-labeling-shell
PASS Fresh 1920x1080 current-source no-data capture shows `라벨 품질: 미확인`, not `정상`, without clipping: artifacts\ui\dataset-health-20260717-current-review\dataset-health-current-1920.png
```

## YOLOv8 Anomaly Classification Candidate Runtime and Quality Boundary

Status: stable for current-build YOLOv8 classification profile persistence, restart, image-level candidate transport, and review-state mapping as of 2026-07-18. This does not make the Washer candidate adoptable.

Protected behavior:

- A saved anomaly recipe retains the selected YOLOv8 engine, exact classification `best.pt`, local Python adapter, normal/abnormal mapping, and confidence settings through EXE close/reopen.
- The first image-level classification inference identifies the selected run, returns a candidate, and maps an eligible abnormal result to `AnomalyImageReviewState.Abnormal` without replacing a different model profile.
- Candidate runtime success and quality evaluation remain separate. The external circular regression set is evaluation-only and must not be copied into Washer training or used to auto-promote a weight.

Required gates before reporting this path complete again:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolov8-anomaly-classification-runtime-smoke
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py Runtime\Python\openvisionlab_ultralytics_worker.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
git diff --check
```

Latest evidence:

```text
PASS Current Debug EXE close/reopen/first inference: artifacts\exe-yolov8-anomaly-restart-smoke\washer300-candidate-runtime-20260717\summary.txt
PASS Exact weight SHA-256: 1A1003635756E1052B7361DCB116EC807F5B16BC555E114138F7AE595B8D2D9F
PASS Restart status identifies YOLOv8 and the selected Washer run; candidate 1 persisted as Abnormal
HOLD External circular 60-image regression: 0/60 at confidence 0.8; artifact: artifacts\external-anomaly-evaluation\washer300-vs-circular-holdout-20260717\classification-evaluation-20260717-152409\classification-evaluation-summary.json
HOLD External MultiIndustry synthetic native test: 14/75 at confidence 0.8, normal 14/44, abnormal 0/31; artifact: artifacts\external-anomaly-evaluation\multiindustry-vs-washer-native-test-20260717\evaluation\classification-evaluation-20260717-185132\classification-evaluation-summary.json
PASS 2026-07-18 focused current-source review: isolated 0-warning/0-error build; --anomaly-folder-auto-review; --anomaly-classification-training-workflow; --wpf-yolov8-anomaly-classification-runtime-smoke; --anomaly-classification-evaluation; --python-ultralytics-worker; local adapter/worker py_compile; and adapter --self-test.
```

## Cross-Engine Native Detection Comparison Manifest Mapping

Status: stable for the local YOLOv5/Ultralytics object-detection comparison path as of 2026-07-18; the report is benchmark evidence, not automatic model adoption.

Protected behavior:

- Read valid native `data.yaml` class definitions from `nc` when present or from indexed/list `names` when `nc` is omitted.
- Recurse nested split folders and map each image under `images` to the corresponding relative path under `labels`.
- Create a deterministic source manifest for prediction. Reject duplicate image stems because flat prediction-label folders cannot safely represent them.
- When Ultralytics receives that manifest as a Python list and emits `imageN.txt`, rename only the generated prediction labels back to their source stems before the comparison's ground-truth review. Never rename answer labels, images, weights, recipes, registry state, or adoption history.
- Keep YOLOv5 and YOLOv8 native validation metric/Takt collection separate from the saved prediction-label review. The same split/image/label fingerprint must be stored in the report.
- Materialize an artifact-only runtime `data.yaml` with an absolute root when a native supplied YAML uses a relative `path:`. Pass that one file to both engines, record it in the report, and never edit the supplied YAML.

Required gates before reporting this path complete again:

```powershell
dotnet build .\\tests\\LabelingApplication.Tests\\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\\isolated-out\\
dotnet .\\tests\\LabelingApplication.Tests\\artifacts\\isolated-out\\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
PowerShell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\compare-yolo-models.ps1 ... -Task test -ModelTask detect -BenchmarkRepeatCount 5
git diff --check
```

Latest evidence:

```text
Controlled report: artifacts\\yolo-model-comparison\\easy-match-die-array-500-v5s-v8n-e20-mapped-20260717\\20260717-145639\\comparison-summary.json
PASS Native 360/80/60 source split; held-out test 60 images / 60 labels
PASS YOLOv5s seed 0, 20 epochs, image 320, batch 4; five-run median Takt 74.0ms
PASS YOLOv8n seed 0, 20 epochs, image 320, batch 4; five-run median Takt 48.960ms
PASS YOLOv8n UI review labels use 59 unique original stems; TP/FP/FN 30/6/8 at confidence 0.25
PASS Original supplied source image/label fingerprint unchanged; source .cache files 0
PASS 2026-07-18 focused current-source review: isolated 0-warning/0-error build, --wpf-model-comparison-run-service, and PowerShell parser check for scripts\compare-yolo-models.ps1.
PASS 2026-07-18 user-authorized Switch Housing cross-product test: `artifacts\cross-domain-switch-object-detection-20260718-002742\comparison\20260718-003624\comparison-summary.json`; 60 staged test images, five repeats, and artifact-only runtime YAML provenance. YOLOv5s mAP50/Takt `0.325/64.4ms`; YOLOv8n `0.258/36.608ms`; candidate decision `hold`.
```

## External YOLOv5 Unicode-Path Training Staging

Status: stable for native folder-based YOLOv5 detection input staging and cleanup as of 2026-07-17; this is not a model-quality or adoption approval.

Protected behavior:

- A native data YAML with `path: .` resolves relative to its own directory, not the YOLOv5 source checkout.
- When the resolved source root is non-ASCII, copy only its `images` and `labels` folders to a temporary ASCII training directory before invoking YOLOv5. This protects the installed OpenCV loader from a Windows Unicode-path limitation.
- Route YOLOv5 label-cache files to the same temporary directory. Do not create `.cache` files in the externally selected source labels folder.
- Delete the temporary YAML, cache, and staged dataset before sending the terminal worker state, then perform the same cleanup defensively in `finally`.
- Do not mutate source YAML, images, labels, recipe model registration, or candidate adoption state as part of this compatibility path.

Required gates before reporting this path complete again:

```powershell
C:\Git\yolov5\.venv\Scripts\python.exe -m py_compile C:\Git\yolov5\labeling_tcp_client.py
C:\Git\yolov5\.venv\Scripts\python.exe C:\Git\yolov5\labeling_tcp_client.py --self-test
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --external-yolo-dataset-intake
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-external-yolo-dataset-training --engine yolov5 --purpose detection --epochs 1 --image-size 320 --batch 4
git -C C:\Git\yolov5 diff --check
git diff --check
```

Latest evidence:

```text
Real app/TCP smoke: artifacts\real-external-yolo-dataset-training\20260717-100835\summary.txt
Source root: D:\라벨테스트\EasyMatch_Die_Array_500(1)\EasyMatch_Die_Array_500\object_detection
PASS Worker completed; best.pt exists (14,302,767 bytes)
PASS Source tree SHA-256 4adea22d806390a706d0c70b82704916606d75285c09218b355bf59e3c84986b before and after
PASS Source .cache files 0 and remaining temporary training directories 0
```

## External YOLOv8 Label-Cache Isolation

Status: stable for local YOLOv8 external folder-based detection/segmentation cache isolation as of 2026-07-17; this is not a model-quality or adoption approval.

Protected behavior:

- The local Ultralytics dataset loader honors `OPENVISIONLAB_ULTRALYTICS_LABEL_CACHE_DIR` for normal YOLO label caches. Without that environment variable, its upstream cache behavior remains unchanged.
- The local YOLOv8 adapter creates the cache directory only around `model.train`, restores the previous environment value, and deletes the temporary cache directory afterward.
- An externally selected source must not retain `labels\\*.cache` after a completed local YOLOv8 training request.
- YOLOv8 detect runs are stored under `runs\\train`; external training verification must inspect that worker-owned location rather than inventing a `runs\\detect` folder.
- Do not alter source YAML, images, labels, recipe model registration, or candidate adoption state as part of cache isolation.

Required gates before reporting this path complete again:

```powershell
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py C:\Git\yolov8\ultralyticsMaster\ultralytics\data\dataset.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --external-yolo-dataset-intake
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-external-yolo-dataset-training --engine yolov8 --purpose detection --epochs 1 --image-size 320 --batch 4
git diff --check
```

Latest evidence:

```text
Real app/TCP smoke: artifacts\real-external-yolo-dataset-training\20260717-102109\summary.txt
Source root: D:\라벨테스트\EasyMatch_Die_Array_500(1)\EasyMatch_Die_Array_500\object_detection
PASS Worker completed; best.pt exists (6,204,778 bytes)
PASS Source tree SHA-256 4adea22d806390a706d0c70b82704916606d75285c09218b355bf59e3c84986b before and after
PASS Source .cache files 0 and remaining temporary cache directories 0
```

## YOLOv8 SEG Contour-Only Inference Overlay

Status: stable for worker polygon preservation and unfilled contour rendering as of 2026-07-13.

Protected behavior:

- Preserve `segmentationType=polygon`, pixel polygon points, and normalized polygon points from the YOLOv8 worker through candidate review and overlay creation.
- Draw valid SEG candidates as closed contour lines only. Do not fill the polygon and do not replace it with a bounding rectangle or corner handles.
- Keep class/confidence badges visible so overlapping candidates remain identifiable.
- Keep object-detection candidates on the existing rectangle overlay path.
- A segmentation result without at least three valid points must not silently appear as a precise rectangular SEG result.
- Keep the WPF and legacy OpenGL overlay contracts aligned.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-polygon-training-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-detection-display-mode
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-yolo-smoke
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
git diff --check
```

Latest evidence:

```text
Real model: artifacts\real-yolo-smoke\seg-contour-render-20260713-verified\summary.txt
Before: C:\Git\Labelling_Application\artifacts\ui\20260713-seg-inference-contour\before-seg-inference-box-1920.png
After: C:\Git\Labelling_Application\artifacts\ui\20260713-seg-inference-contour\after-seg-inference-contour-full-1920.png
PASS Real YOLO TCP workflow detects, overlays, confirms, and saves labels
PASS WPF candidate review confirms polygon candidates through YOLOv8 segment training packet
PASS WPF detection candidates render as detection overlays
PASS WPF segmentation object verification matrix
```

## Corrected-Contour YOLOv8 SEG Candidate Evaluation

Status: verified local candidate execution and fail-closed promotion behavior as of 2026-07-16; this is not a production/adoption approval.

Protected behavior:

- The corrected-contour candidate `openvisionlab-yolov8-seg-contour124-retrain-20260716\weights\best.pt` remains a candidate. Do not write it into the current user recipe, model registry adoption record, or inspection runtime from the validation result alone.
- Compare it with the former-rectangle baseline only against the same declared split, model task, positive class, UI confidence, image size, batch, and runtime conditions. The current evidence is `Task=val`, `NG`, confidence `0.25`, 320 pixels, batch 1, CPU, and five repeats.
- Preserve the evaluation evidence and weight identities. The comparison report is `hold` because the 28-image validation split contains one NG positive label/image, below the five-label/five-image promotion threshold.
- A successful TCP polygon save proves transport and persistence only; it must not override the evidence/adoption guard.

Latest evidence:

```text
Candidate SHA-256: 9A741059A4A19B0928BD9530D9DC9312ADB52268503D3420BE23BDA13E429A85
Comparison: artifacts\yolo-model-comparison\yolov8-seg-contour124-baseline-vs-retrain-valid-20260716\20260716-211012
Runtime smoke: artifacts\real-yolo-smoke\contour124-retrain-valid-ng-20260716\summary.txt
PASS Real YOLO TCP workflow detects, overlays, confirms, and saves labels
```

## Optional AI Candidate Review Empty State

Status: stable for the optional Stage 3 purpose and zero-candidate information hierarchy as of 2026-07-12.

Protected behavior:

- Stage 3 must identify AI Candidate Review as optional. A user who completed labels manually can skip it and move to `4 학습/모델` without changing saved labels.
- With zero pending candidates, do not show candidate summary cards, role cards, selected-candidate detail, confidence controls, an empty candidate list, or an unrun model-comparison dashboard.
- Keep the label-save/next-image completion card visible so the current image still has one concrete next action.
- If pending candidates exist but the confidence filter hides all of them, keep candidate controls visible and tell the operator that lowering confidence restores them.
- Show model-quality comparison cards only for a real comparison report. Existing candidate confirm/hide, segmentation persistence, and model-adoption guards remain unchanged.
- Saved manual-label completion text must not claim that AI Candidate Review was completed.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-layout
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
Before: C:\Git\Labelling_Application\artifacts\ui\20260712-candidate-review-empty-ux\before-candidate-review-empty-1920.png
After: C:\Git\Labelling_Application\artifacts\ui\20260712-candidate-review-empty-ux\after-candidate-review-empty-final2-retry-1920.png
PASS WPF candidate review supports navigation and focus commands
PASS WPF candidate rows show visual review status
PASS WPF labeling shell can be constructed without the WinForms shell
PASS MVVM infrastructure observable and command helpers
PASS Priority workflow docs cover YOLOv5, segmentation, and anomaly flows
Standalone Candidate Review image SHA256 matches the annotated PNG
git diff --check passed with line-ending warnings only
```

## Local Detection/Segmentation Image Quality Review

Status: stable for image-level local QA state, short issue reasons, and local report export as of 2026-07-16.

Protected behavior:

- Human QA state is independent from AI candidate review state and label-file save state.
- Detection/Segmentation images support `미검토`, `수정 필요`, and `검수 완료`; anomaly review remains separate.
- `검수 완료` requires saved/completed label work.
- Editing a reviewed label returns QA to `미검토`; editing a `수정 필요` image must not silently clear the issue.
- `review-status.json` stores the quality state while remaining backward compatible with rows that do not contain the new fields.
- A `수정 필요` image can store one current issue reason of at most 200 characters. Newlines are normalized, and resolving or clearing the issue removes the current reason.
- The image queue `수정 필요` filter shows issue images, and those images are not counted as QA complete even when their label files are saved.
- `QA 보고서` writes `label-quality-review.md` under the configured output root with aggregate QA counts and a needs-fix table. It must escape Markdown cells and must not expose an absolute dataset path.
- Visible report feedback should show the file name and counts rather than a machine-specific absolute path.
- The quality visual smoke must persist the active image's labels before entering `needs-fix`, then prove that `reviewed` remains available. Do not satisfy this by weakening the saved-label guard.
- Do not replace this local state with account, assignment, collaboration, or server concepts unless product scope changes explicitly.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --yolo-image-review-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --label-quality-review-report
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-object-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab objects --quality-dashboard --show-quality-needs-fix --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
Before: C:\Git\Labelling_Application\artifacts\ui\wpf-label-quality-qa-before-20260711-1920.png
After: C:\Git\Labelling_Application\artifacts\ui\wpf-label-quality-qa-after-20260711-1920.png
Reason/report before: C:\Git\Labelling_Application\artifacts\ui\wpf-label-quality-note-before-20260711-1920.png
Reason/report after: C:\Git\Labelling_Application\artifacts\ui\wpf-label-quality-note-after-20260711-1920.png
Current rework smoke: C:\Git\Labelling_Application\artifacts\ui\20260716-review-rework-audit\quality-needs-fix-saved-1920.png
PASS YOLO image review status tracks labels, candidates, and next unlabeled image
PASS YOLO image quality review exports local markdown report
PASS WPF image queue presents row status with icons
PASS WPF object review summarizes current labels
PASS WPF labeling shell can be constructed without the WinForms shell
PASS WPF visual smoke persists the active saved label before needs-fix and keeps reviewed available
WPF responsive layout passed: 1920x1080, tabs=objects
WPF responsive layout passed: 1366x768, tabs=objects
```

## Current-Work Default Information Hierarchy

Status: stable for the compact labeling-stage current-work surface as of 2026-07-11.

Protected behavior:

- The first-visible current-work card should show the current image/mode, immediate action, and completion flow without a nested role-card dashboard.
- Tool, object/AI, and quality context should remain available under the collapsed `작업 세부 보기` disclosure.
- The immediate next flow must not be duplicated inside the optional context section.
- Training and inspection detail should remain in the separate collapsed `필요할 때만: 학습·검사 세부` section.
- This presentation contract must not change annotation, Viewer/OpenGL/ROI, brush/eraser, training, or inference behavior.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --review-tabs guide --width 1920 --height 1080
git diff --check
```

Latest evidence:

```text
Before: C:\Git\Labelling_Application\artifacts\ui\wpf-workbench-ia-before-20260711-1920.png
After: C:\Git\Labelling_Application\artifacts\ui\wpf-workbench-ia-after-20260711-1920.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF responsive layout passed: 1920x1080, tabs=guide
WPF responsive layout passed: 1366x768, tabs=guide
```

## Beginner Labeling Start View

Status: stable for compact beginner entry after dataset setup and top `2 라벨링` navigation as of 2026-07-07.

Protected behavior:

- Completing dataset setup should land in labeling mode without forcing the full class-management panel open.
- The top `2 라벨링` workflow button should use the same compact labeling start view.
- The side workflow panel should collapse to the narrow rail, saved-label review should be selected, and class management should remain available from the rail instead of becoming the first visible panel.
- Explicit labeling-mode command paths that intentionally open the guide/tools panel should still use the guide-opening behavior.
- Public tutorial text should describe the compact default entry and keep expanded-panel screenshots only as task-level drawing examples.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
Before: C:\Git\Labelling_Application\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_194809\screenshots\03_dataset_created.png
After: C:\Git\Labelling_Application\artifacts\ui\wpf-beginner-labeling-compact-after-1920.png
PASS WPF labeling shell can be constructed without the WinForms shell
PASS WPF learning workflow panel declares education modes and annotation tools
```

## Canvas Workflow Image Queue Guidance

Status: stable for neutral image-queue wording in the visible canvas/guide/onboarding workflow as of 2026-07-08.

Protected behavior:

- The canvas workflow action text should not hard-code `left image queue` or `left queue`; the queue can be docked on the right or collapsed to the rail.
- Startup/sample and saved-label guidance should refer to the `image queue` generically.
- Guide/onboarding current-task, first-run, and YOLO image-load guidance should not hard-code `left image queue` or `left-side image queue`.
- Generic object-review guidance should say `label position/class`, not box-only wording, so the same text fits box, polygon, and brush workflows.
- No-object completion tooltip should explain saving an empty YOLO label without box-only wording.
- This is a wording/presentation contract only; it must not change annotation save, next-image navigation, candidate review, or viewer hot paths.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-startup-onboarding-visual --width 1920 --height 1080 --output .\artifacts\ui\wpf-startup-image-queue-neutral-guide-after-1920.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\wpf-startup-image-queue-neutral-after-1920.png
After guide follow-up: C:\Git\Labelling_Application\artifacts\ui\wpf-startup-image-queue-neutral-guide-after-1920.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF startup onboarding visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-startup-image-queue-neutral-guide-after-1920.png
```

## AI Candidate Current Task Guidance

Status: stable for pending-candidate review guidance across guide/canvas/queue, including the compact guide checklist, as of 2026-07-08.

Protected behavior:

- When inference mode has pending AI candidates, the effective workflow step should be `Review`, not `Infer`.
- The left guide current-task card and canvas workflow strip should guide the operator to confirm, confirm all, or skip candidates instead of asking them to run inspection again.
- The guide current-task checklist should be ViewModel-owned and follow the live workflow state:
  - sample/default: `1 이미지`, `2 열기`, `3 라벨`;
  - labeling: `1 그리기`, `2 저장`, `3 다음`;
  - inference: `1 검사`, `2 확인`, `3 검토`;
  - AI candidate review: `1 확인`, `2 확정`, `3 스킵`.
- `ApplyDetectionCandidates` should refresh the canvas/guide workflow context after loading candidates and opening Candidate Review.
- `ShowCandidateReviewWorkflowView` should enter inference mode; `ShowSavedLabelsWorkflowView` should enter labeling mode so visible right-workflow view and canvas current-task mode stay aligned.
- This presentation change must not alter candidate state ownership, confirmation, skip, save, or overlay rendering behavior.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-labeling-guide-ai-candidate-checklist-after-1920.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-ai-candidate-review-task-after-1920.png
After checklist: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-ai-candidate-checklist-after-1920.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-ai-candidate-checklist-after-1920.png
```

## AI Candidate Review First-Class Summary

Status: stable for first-visible AI-review mode summary as of 2026-07-10.

Protected behavior:

- Candidate Review should read as an AI-review workbench mode, not only as a loose list of buttons.
- `CandidateReviewWorkbenchSummaryGrid` should stay near the top of `WpfCandidateReviewPanel.xaml`, before lower-priority detail cards and before the confidence slider.
- The summary grid should expose candidate count, confidence threshold, selected candidate, and next completion state through ViewModel bindings.
- Candidate count should come from `WpfCandidateReviewPanelViewModel.CandidateCountSummaryText`, derived from existing candidate rows in `SetCandidates`.
- The primary action group should keep the visible `후보 이동 / 결정` label above navigation, focus, confirm, confirm-all, and skip controls.
- This is WPF presentation only. It must not change candidate state ownership, confirm/skip behavior, segment JSON/mask PNG save behavior, YOLO label export, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab candidates --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-ai-candidate-review-first-class-after-20260710-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
Before: C:\Git\Labelling_Application\artifacts\ui\wpf-ai-candidate-review-first-class-before-20260710-1920.png
After: C:\Git\Labelling_Application\artifacts\ui\wpf-ai-candidate-review-first-class-after-20260710-1920.png
Tutorial: C:\Git\Labelling_Application\docs\tutorial\images\annotated\12-inference-dock-1920-annotated.png
PASS WPF candidate review supports navigation and focus commands
PASS WPF labeling shell can be constructed without the WinForms shell
WPF responsive layout passed: 1920x1080, tabs=objects,candidates,guide,classes,yolo,training
PASS Priority workflow docs cover YOLOv5, segmentation, and anomaly flows
git diff --check passed
```

## Model Quality Summary Cards

Status: stable for scan-friendly model validation summary in Candidate Review as of 2026-07-10.

Protected behavior:

- `ModelQualitySummaryGrid` should stay inside `ModelComparisonReviewPanel` and before `ModelCandidateDecisionPanel`.
- The summary grid should expose status, evidence/source, decision, and next action through existing ViewModel-owned strings.
- `ModelQualityStatusText` binds to `ModelComparisonStatusText`.
- `ModelQualitySourceText` binds to `ModelComparisonSourceText`.
- `ModelQualityDecisionText` binds to `ModelComparisonDecisionText`, so the card shows `교체 추천/보류/예시 검토` from the held-out report instead of the separate candidate workflow state.
- `ModelQualityNextActionText` binds to `ModelComparisonActionText`.
- A `hold` report must direct the operator to improve data/model quality and rerun validation, not to save the candidate.
- The old long source/status lines should not be the visible primary model-quality UI.
- Candidate workflow status and save/reject controls remain in `ModelCandidateDecisionPanel`; do not merge those states back into the comparison decision card.
- This summary must not change YOLOv8 runtime behavior, claim YOLO11 readiness, or touch Viewer/OpenGL/ROI/brush/eraser paths.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab candidates --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-quality-summary-after-20260710-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
Before: C:\Git\Labelling_Application\artifacts\ui\wpf-model-quality-summary-before-20260710-1920.png
After: C:\Git\Labelling_Application\artifacts\ui\wpf-model-quality-summary-after-20260710-1920.png
Tutorial: C:\Git\Labelling_Application\docs\tutorial\images\annotated\12-inference-dock-1920-annotated.png
PASS WPF candidate review supports navigation and focus commands
PASS WPF labeling shell can be constructed without the WinForms shell
WPF responsive layout passed: 1920x1080, tabs=objects,candidates,guide,classes,yolo,training
PASS Priority workflow docs cover YOLOv5, segmentation, and anomaly flows
git diff --check passed
```

## YOLOv8 SEG Image-Level Promotion Guard

Status: stable for fail-closed image-level segmentation adoption evidence and confidence-coupled model save as of 2026-07-11.

Protected behavior:

- `compare-yolo-models.ps1` keeps aggregate validation metrics and UI inference evidence separate.
- Multi-class segmentation comparisons with a selected defect class must use that class for both answer and prediction evidence. The WPF request path prefers `NG`, then `Defect`, then the sole catalog class; detection requests must not receive this segmentation-only option. An ambiguous multi-class catalog must not be described as class-specific evidence until `-SegmentationPositiveClassName` is supplied explicitly.
- When a positive segmentation class is configured, label-line counts, positive/background image classification, UI candidate counts, threshold sweeps, coverage, and background-candidate rates must all use that same class. An outer `OK` polygon must not count as defect evidence.
- Comparison summaries and Markdown reports must preserve the selected segmentation positive class id/name so later Model Center review can audit the evidence basis.
- Segmentation comparison summaries must include `uiPositiveImageCount`, `uiPositiveImagesWithCandidates`, `uiPositiveImageCoverage`, `uiBackgroundImageCount`, `uiBackgroundImagesWithCandidates`, and `uiBackgroundCandidateRate`.
- Promotion requires at least `5` background held-out images, positive-image coverage `>= 0.5`, and background-candidate rate `<= 0.1` at the selected UI confidence.
- A missing image-level operating result must fail closed as `hold`; do not silently fall back to total candidate count.
- The comparison summary `uiConfidence` must match the current inspection confidence. A missing or different value must become `hold` and direct the operator to rerun comparison at the current confidence.
- `WpfModelComparisonReviewReport.PromotionDecision` and `RecommendationText` must preserve the report recommendation for Candidate Review.
- A held pending candidate must have Candidate Review and Model Center save disabled while reject remains available when a valid baseline exists.
- Both `ExecuteSaveModelCandidateCommand` and pending-weight `ExecuteSaveYoloSettingsCommand` must refuse held candidate adoption.
- This guard does not improve model accuracy and must not be described as production readiness.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-review-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-heldout
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-center-real-candidate-save --yolo-root C:\Git\yolov8 --data-yaml .\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260708_213227\dataset\data.yaml --baseline-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt --candidate-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-operating-selected-conf020-20260711\weights\best.pt --candidate-confidence 0.20
git diff --check
```

Latest evidence:

```text
0.25 summary: artifacts\yolo-model-comparison\yolov8-seg-40label-operating-guard-20260710\20260710-181821\comparison-summary.json
hold: positive coverage 2/10 = 0.2, background-candidate rate 0/6 = 0
0.10 summary: artifacts\yolo-model-comparison\yolov8-seg-40label-operating-guard-conf010-20260710\20260710-181924\comparison-summary.json
hold: positive coverage 9/10 = 0.9, background-candidate rate 6/6 = 1.0
Operating-selected summary: artifacts\yolo-model-comparison\yolov8-seg-40label-operating-selected-conf020-20260711\20260711-220539\comparison-summary.json
promote at 0.20: precision 0.7748, recall 0.6891, mAP50 0.6900, positive coverage 6/10 = 0.6, background-candidate rate 0/6 = 0
Candidate: C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-operating-selected-conf020-20260711\weights\best.pt
Confidence mismatch before: artifacts\ui\wpf-seg-operating-confidence-mismatch-before-20260711-1920.png
Confidence mismatch after hold: artifacts\ui\wpf-seg-operating-confidence-mismatch-after-20260711-1920.png
Validated 0.20 adoption: artifacts\ui\wpf-seg-operating-conf020-adopt-after-20260711-1920.png
Real TCP save: artifacts\real-yolo-smoke\40label-operating-selected-conf020-current-image-025-ng-20260711\summary.txt
Class-specific Teaching summary: artifacts\yolo-model-comparison\yolov8-seg-multidomain-best-teaching-test-conf020-20260711\20260711-232609\comparison-summary.json
Class-specific circular summary: artifacts\yolo-model-comparison\yolov8-seg-multidomain-best-circular-test-conf020-20260711\20260711-232716\comparison-summary.json
Combined review summary at 0.30: artifacts\yolo-model-comparison\yolov8-seg-multidomain-best-combined-test-conf030-20260711\20260711-233546\comparison-summary.json
review at 0.30: precision 0.6671, recall 1.0, mAP50 0.8799, positive coverage 11/15, background-candidate rate 0/27
Temporary Model Center save evidence: artifacts\ui\wpf-model-center-multidomain-best-review-conf030-20260711-1920.png
Real TCP save: artifacts\real-yolo-smoke\multidomain-best-conf030-current-image-024-ng-20260711\summary.txt
Before: artifacts\ui\wpf-seg-operating-guard-before-20260710-1920.png
After: artifacts\ui\wpf-seg-operating-guard-after-20260710-1920.png
```

## Learning Guide Training Flow Details Collapsed

Status: stable for compact labeling-mode guide density as of 2026-07-09.

Protected behavior:

- In labeling mode, the guide panel should prioritize the current task card and hide deep training-flow detail by default.
- `LabelingGuideTrainingFlowExpander` inside `WpfLearningWorkflowPanel.xaml` remains collapsed by default to reduce left-panel cognitive load.
- This change is a presentation density control; it must not alter annotation commands, brush/eraser runtime, canvas interaction, or model execution paths.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-labeling-guide-training-flow-collapsed-1920.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-training-flow-collapsed-1920.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-training-flow-collapsed-1920.png
```

## Learning Guide Tabbed Layout

Status: stable for guide/tools density reduction and secondary detail separation as of 2026-07-09.

Protected behavior:

- In labeling mode, the high-frequency workflow guidance should remain visible without forcing users through a single deep scrolling stack.
- `LabelingGuideWorkflowToolsExpander` + workflow/명령 blocks and 템플릿/튜토리얼 blocks should remain available, but separated by explicit tabs so beginners can stay in the primary path by default.
- This is presentation structure only; it should not alter annotation commands, brush/eraser behavior, viewer interaction, navigation, training execution, or model inference.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false /m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\learning-workflow-guide-tabs-1920x1080.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\learning-workflow-guide-tabs-1920x1080.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\learning-workflow-guide-tabs-1920x1080.png
```

## Learning Guide Workflow Guide Expander

Status: stable as of 2026-07-09 for collapsing secondary workflow/tutorial content behind an explicit expander.

Protected behavior:

- `LabelingGuideWorkflowGuideExpander` should keep high-frequency current-task guidance visible while moving template/tutorial workflow and command guidance behind one collapsed step.
- This should remain a presentation-density control only: no changes to annotation command ownership, brush/eraser runtime, candidate review flow, training execution, or model inference.
- `LabelingGuideIntroText` and `LabelingGuideWorkflowTabs` must remain bound/visible inside the expander content, preserving their previous guidance text and bindings.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false /m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-labeling-guide-workflow-guide-expander-latest-1920.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-workflow-guide-expander-latest-1920.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-labeling-guide-workflow-guide-expander-latest-1920.png
```

## Learning Guide Template/Tutorial Subtabs

Status: stable for template/tutorial secondary density reduction as of 2026-07-09.

Protected behavior:

- In the "템플릿 / 튜토리얼" section of the labeling guide, the high-frequency template workflow and tutorial content are now separated into sibling subtabs.
- A separate default first-use path remains unchanged: template workflow can still be started from the existing `TemplateCurrentImageCommand` and `TemplateBatchCommand`.
- This is presentation structure only; it must not alter annotation commands, canvas interaction, image loading, training execution, or model inference.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false /m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\learning-workflow-guide-tabs-template-tutorial-subtabs-1920x1080.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\learning-workflow-guide-tabs-template-tutorial-subtabs-1920x1080.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\learning-workflow-guide-tabs-template-tutorial-subtabs-1920x1080.png
```

## Learning Guide Workflow Command List Compacting

Status: stable as of 2026-07-09 for command-list density and first-view readability.

Protected behavior:

- In labeling mode, `학습 단계`, `라벨링 도구`, and `작업 명령` lists use bounded-height, one-column vertical stacks with vertical scrolling.
- Command labels/icons and selection bindings remain unchanged; only layout density is adjusted.
- This keeps the visible panel focused and avoids crowding without altering annotation commands, canvas/ROI paths, training execution, or inference logic.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false /m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\learning-workflow-guide-command-list-compact-1920.png
git diff --check
```

Latest evidence:

```text
After: C:\Git\Labelling_Application\artifacts\ui\learning-workflow-guide-command-list-compact-1920.png
PASS WPF learning workflow panel declares education modes and annotation tools
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\learning-workflow-guide-command-list-compact-1920.png
```

## WPF Visual Smoke Capture

Status: stable for current-source WPF render capture as of 2026-07-07.

Protected behavior:

- `--wpf-visual-smoke` should render the WPF window visual tree to PNG, not copy desktop pixels with `CopyFromScreen`.
- Captures must remain usable when the desktop is locked, obscured, or focused elsewhere.
- 1920x1080 visual smoke requests should produce a 1920x1080 PNG when the WPF window is sized to that capture target.
- Treat this as current-source WPF view evidence, not direct EXE UI Automation evidence.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab candidates --width 1920 --height 1080 --output .\artifacts\ui\wpf-render-capture-candidate-review-after-1920.png
git diff --check
```

Latest evidence:

```text
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-render-capture-candidate-review-after-1920.png
IMAGE_SIZE=1920x1080
Manual image inspection: WPF Candidate Review screen, not Windows lock/spotlight screen.
```

## Anomaly Folder-Name Suggestion and Classification Export

Status: stable for conventional image-level anomaly input folders as of 2026-07-19.

Protected behavior:

- The nearest matching ancestor folder is detected as a proposed mapping: `OK`/`normal` -> `Normal`, and `NG`/`abnormal` -> `Abnormal`. This supports both direct folders and `OK/<product>` / `NG/<product>` layouts; the closest matching folder wins.
- Detection is non-mutating. Loading an anomaly image folder, checking training readiness, or exporting a classification dataset must not save or infer review states from a parent folder name.
- When matching unreviewed images exist, Image Queue shows one temporary `폴더명으로 초기 판정을 제안합니다` card. It is not a permanent header control or a new dataset action.
- When an anomaly root has no direct images and falls back to child folders, the card title must state `하위 폴더 포함: 총 N장 불러옴`, and the queue must interleave top-level child folders instead of showing every `NG` path before the `OK` paths.
- `N장 일괄 판정` applies the proposal only to `Unreviewed` images and preserves every saved manual Normal or Abnormal decision. `이미지별 확인` leaves the images unreviewed and hides the proposal for that image-root session.
- The first nested image opened after selecting the image collection root, and every later queue-row selection, must retain that selected root. A nested `NG` file may never replace the queue root with its parent `NG` folder; both `NG` and `OK` rows must remain available.
- The configured image collection may contain nested class folders. Training readiness and classification export discover those files, but consume only saved/explicitly approved review states.
- Operators must set the image collection directory itself, not a dataset-package parent that contains masks, previews, or metadata files.
- This prepares the existing image-level `classify` flow only. It must not claim defect localization, contour output, model training completion, production accuracy, or YOLO11 readiness.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-folder-auto-review
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-dataset-export
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-anomaly-purpose-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
git diff --check
```

Latest evidence:

```text
PASS Anomaly folder names need explicit review approval and preserve manual decisions
PASS Anomaly classification dataset export writes reviewed image folders
PASS Anomaly classification training workflow sends classify dataset
PASS WPF anomaly purpose flow persists image-level review state
Current-source before: artifacts\ui\anomaly-folder-status-consent-20260718\before-anomaly-folder-auto-1920.png
Current-source after: artifacts\ui\anomaly-folder-status-consent-20260718\after-anomaly-folder-suggestion-1920.png
PASS 2026-07-19 nested-root retention: --anomaly-folder-auto-review opens an `NG` first file from an `images` root, then selects an `OK` row, and asserts the root and all five rows remain intact.
Current-source WPF shell capture: artifacts\ui\anomaly-image-root-retention-20260719\after-root-retention-1920.png
Manual image inspection: the current shell remains unclipped at 1920x1080. The nested-root behavior itself is proven by the focused fixture assertion above, not inferred from this general shell capture.
```

## YOLOv8 Anomaly Classification Circular-Defect Evidence

Status: stable for the scoped local circular-defect train/infer/evaluate/app-mapping loop as of 2026-07-16.

Protected behavior:

- The local workflow continues to use `C:\Git\yolov8`, its `.venv`, editable source, `labeling_tcp_client.py`, and cached `yolov8n-cls.pt`; no implicit model or package download is part of this gate.
- Classification candidates remain image-level `imageClassification` results and map only through explicit normal/abnormal class settings.
- The focused WPF runtime smoke must retain its default abnormal behavior and may use `LABELING_YOLOV8_CLASSIFICATION_SMOKE_EXPECTED_CLASS`, `LABELING_YOLOV8_CLASSIFICATION_SMOKE_EXPECTED_STATE`, and `LABELING_YOLOV8_CLASSIFICATION_SMOKE_IMAGE_SIZE` for real held-out normal/abnormal checks.
- Adoption remains fail-closed through `AnomalyClassificationEvaluationService`; a matching class below the configured minimum confidence still counts as incorrect.
- The current `adopt` result applies only to the existing circular-defect source family. Do not turn it into a broad production-readiness or YOLO11-readiness claim.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolov8-anomaly-classification-runtime-smoke
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
git diff --check
```

Latest evidence:

```text
Weight: C:\Git\Labelling_Application\artifacts\yolov8-cls-training-smoke\circular-defect-real-20260711\runs\yolov8n-cls-circular-defect-balanced-e20-img128-20260711\weights\best.pt
Evaluation: C:\Git\Labelling_Application\artifacts\yolo-classification-evaluation\circular-defect-balanced-e20-minconf08-20260711\classification-evaluation-20260711-214135\classification-evaluation-summary.json
Result: adopt, 15/15, normal 5/5, abnormal 10/10, minimum confidence 0.8, low-confidence class matches 0
WPF app mapping: held-out normal -> Normal; held-out abnormal -> Abnormal
Capture: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-anomaly-circular-adopt-20260711-1920.png
Scope limit: one source family; 5 normal and 10 abnormal held-out images; train-only normal oversampling is not new evidence
```

Latest app-managed folder run, separate from the earlier balanced evidence:

```text
Weight: C:\Git\yolov8\runs\classify\openvisionlab-yolov8-classify\weights\best.pt
Weight SHA-256: DE847F3646A637B664D7D5C0CF65409BF24243BDEDBC124C8E178F6978EC106C
App export: 69 train / 14 validation / 17 test images from 20 OK and 80 NG source images
Evaluation: artifacts\real-yolov8-anomaly-folder-training\20260716-234738\classification-evaluation\classification-evaluation-20260716-235151\classification-evaluation-summary.json
Result: hold, 16/17 overall, normal 3/4, abnormal 13/13, one low-confidence class match at minimum confidence 0.8
Hold reasons: only four held-out normal images; normal confidence-gated accuracy 0.75 is below the 0.80 gate
WPF app mapping: held-out normal -> Normal; held-out abnormal -> Abnormal
EXE restart: saved YOLOv8 profile restored the supplied run folder and first NG inference persisted Abnormal with one candidate
Current-build captures: artifacts\exe-yolov8-anomaly-restart-smoke\folder-auto-review-20260716\screenshots\03_restarted_recipe_restored.png and 04_first_abnormal_inference_after_restart.png
Scope limit: same source family, no independent production/cross-session data, not adoptable
```

Latest external synthetic hold-out, separate from all prior training data:

```text
Package: D:\라벨테스트\circular_defect_labeling_dataset_v1_complete\circular_defect_labeling_dataset_v1
Input: supplied anomaly_test_ok.txt (10) + anomaly_test_ng.txt (50); package verify script passed
Content check: 0 SHA-256 overlaps with the previous 100-image anomaly source; 0 duplicates inside the new package
Weight: C:\Git\yolov8\runs\classify\openvisionlab-yolov8-classify\weights\best.pt
Evaluation: artifacts\external-anomaly-evaluation\circular-defect-v1-complete-20260717\evaluation\classification-evaluation-20260717-004535\classification-evaluation-summary.json
Result: hold, 35/60 at minimum confidence 0.8, normal 0/10, abnormal 35/50, 15 low-confidence class matches
Scope limit: the package declares synthetic=true; it is clean external synthetic evidence, not production-camera/cross-session evidence
Data protection: keep all 60 selected images outside training and use them only for future regression/evaluation
```

## Long YOLOv8 Training Completion and Supplied-Dataset Candidate Evidence

Status: stable for run isolation and long local-worker completion as of 2026-07-17; candidate quality remains separately gated.

Protected behavior:

- `YoloTrainingWorkflowService`, `CCommunicationLearning`, and `LearningProtocol` carry an optional single-folder `runName` into the local YOLOv8 adapter. Empty callers retain the existing default run-name behavior.
- `C:\\Git\\yolov8\\labeling_tcp_client.py` clears the connect-only socket timeout before entering its command receive loop and tolerates a transient receive timeout. A run longer than the prior 60-second connect timeout can therefore send its terminal training status through the original connection.
- The local and bundled Ultralytics training calls use `plots=False`; model weights, `args.yaml`, and `results.csv` remain the evidence artifacts.
- The real 20-epoch Washer app-service/TCP classification run completed with `workerTrainingState=completed`, not merely with a generated weight file.

Latest candidate evidence:

```text
Washer classification weight: artifacts\real-yolov8-anomaly-folder-training\washer300-completed-20260717\best.pt
Washer SHA-256: 1A1003635756E1052B7361DCB116EC807F5B16BC555E114138F7AE595B8D2D9F
Washer held-out result: hold, 11/30 confidence-gated correct, normal 0/13, abnormal 11/17, six low-confidence class matches
EasyMatch segmentation weight: C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-easymatch300-e50-img320-20260717-r2\weights\best.pt
EasyMatch SHA-256: E50B5DDF284ADE73AE6B44FACEA060718CC33CFAA510267ACA5FF9F289AF75FB
EasyMatch original test: 36 images / 23 defect instances; box mAP50 0.682 and mask mAP50 0.660
TCP smoke: exact EasyMatch weight returned named polygon candidates for a held-out NG image
Scope limit: both supplied packages are synthetic; neither is registered, adopted, or a production-quality claim
```

## External Native YOLO data.yaml Intake

Status: stable and current-source focused-reviewed for explicit local object-detection and segmentation source selection as of 2026-07-17; source-data and model quality remain separately gated.

Protected behavior:

- `ExternalYoloDatasetSettings` remains separate from recipe-owned `YoloDatasetSettings`, so activating a native source YAML cannot redirect or refresh the app-managed export tree.
- `YoloExternalDatasetIntakeService` reads and validates a folder-based Ultralytics YAML without copying or changing its source images, labels, or YAML. It resolves `path: .` relative to the YAML, requires `train` plus `val`, accepts optional `test`, supports mapping/list class names, validates rows for the selected detection/segmentation purpose, and rejects split overlap.
- Selection records a SHA-256 source identity over the YAML plus referenced images and labels. The operator must explicitly activate it for the next training run; activation and training preparation revalidate it. A valid-but-changed source disables the external input and blocks every training request until the profile is explicitly revalidated and activated again (or cleared), so it cannot silently fall back to the recipe export dataset.
- Ordinary WPF status refreshes use the persisted validation snapshot, avoiding another full external-source scan. Explicit refresh and training preparation still validate the source again.
- The local-source and bundled Ultralytics workers temporarily run YAML-file training from the YAML parent and restore their prior directory afterward. Native `path: .` Detection/Segmentation sources therefore resolve correctly without rewriting the source YAML; directory-based classification training is unchanged.
- The local worker receives the original YAML path with the matching `detect` or `segment` task. A sent request records the source SHA-256, YAML, model, task, run name, requested weight, resolved local weight path/SHA-256, Python path, and worker-script SHA-256 in the separate profile. This path does not auto-register/adopt a model or relax the existing comparison gates.
- Bundled Ultralytics label caches use a temporary OpenVisionLab-owned directory and clean it up afterward; source folders do not receive worker cache files.

Latest runtime evidence (2026-07-17): the supplied EasyMatch segmentation YAML completed a real app-service/TCP YOLOv8 SEG run for one epoch at image size `320` and batch `4`. The app copied `best.pt` only into `artifacts\\real-external-yolo-dataset-training\\20260717-215833\\best.pt`; it did not register, adopt, or score that weight. The external source manifest was exactly unchanged before/after: `1,207` files and aggregate SHA-256 `B137A8EE8F2CAB265AA660874CC3B23C1BFA07D59CDBA0A2B74FD1DE26F98E2D`. Its pre-existing three `.cache` files and zero temporary training directories were also unchanged. The profile recorded source fingerprint `45BAF4F3562A96DC0FB36646E94A9BFBA73849112C82AB7285745B601B6771DB`, original YAML, `yolov8`/`segment`, request `yolov8n-seg.pt`, resolved `C:\\Git\\yolov8\\yolov8n-seg.pt`, seed SHA-256 `A7CD8F929E1903D78A12A48EFECAB430209F18DC46CB96C3599A5980C63C423C`, and worker-script SHA-256 `69BDAB2898993E309939603728776A2D5E41DAFA240D095EEC08BC56D8EC2C46`. This proves runtime routing, provenance, and source immutability only; it is not a quality or adoption claim.

Scope limit: directory-valued Detection/Segmentation YAML splits only. List-file split syntax, external classification YAML, manual operator UI execution, source-data quality, candidate adoption, and YOLO11 readiness remain outside this verified slice.

Focused contract checks: `--external-yolo-dataset-intake` covers source identity persistence, unchanged source after a mock training request, a valid label change blocked before sending, prevention of internal-dataset fallback, and explicit revalidation. `--real-external-yolo-dataset-training --engine yolov8 --purpose segmentation --epochs 1 --image-size 320 --batch 4` captures the opt-in runtime evidence above. `Runtime\Python\openvisionlab_ultralytics_worker.py --self-test` covers temporary label-cache cleanup.

Current-source review completion: an isolated build passed with 0 warnings / 0 errors; `--external-yolo-dataset-intake`, `--wpf-labeling-shell`, worker `py_compile`, and worker `--self-test` passed. The current-source Model Center Data capture `artifacts\ui\external-yolo-intake-20260717-current-review\external-yolo-intake-current-1920.png` shows the separate source card with select, explicit next-training use, and clear actions without clipping. The existing one-epoch source-manifest artifact remains valid runtime evidence; it was not repeated because no acceptance criterion or source contract changed.

## Anomaly Classification Evaluation Model Center Surface

Status: stable for bounded summary-loaded Model Center evaluation visibility as of 2026-07-07.

Protected behavior:

- `AnomalyClassificationEvaluationService` should keep owning adopt/hold metrics and summary JSON loading.
- `WpfAnomalyClassificationEvaluationPresentationService` should keep owning Korean recommendation, metrics, detail, and action text.
- `WpfLabelingShellViewModel` should only hold the current Model Center anomaly-evaluation presentation state and clear it when no summary is active.
- `YoloAnomalyEvaluationPanel` should stay hidden until an anomaly evaluation presentation is set.
- For anomaly-detection datasets, the Model Center dashboard may load `classification-evaluation-summary.json` only from the active output root or its direct `classification-evaluation\` child. Do not add recursive image-folder scans to this refresh path.
- For anomaly-detection datasets, Model Center should expose an explicit `평가 실행` action that runs only through the local YOLOv8 adapter path. It should export a fresh `classification-evaluation-input\...` dataset from reviewed normal/abnormal images, run `scripts\evaluate-yolo-classification.ps1`, and immediately parse the generated `classification-evaluation-summary.json` through the same evaluation/presentation services.
- For anomaly-detection datasets, Model Center should expose an explicit `평가 불러오기` action that lets the operator select an existing `classification-evaluation-summary.json`. The selected summary may live outside the active output root, but it must still be parsed through `AnomalyClassificationEvaluationService` and presented through `WpfAnomalyClassificationEvaluationPresentationService`.
- The in-app anomaly evaluation runner must not claim YOLO11 readiness; non-YOLOv8 engines should fail validation instead of being routed through the YOLOv8 evaluation script.
- A manually selected anomaly evaluation summary should stay active until the dataset context changes or the selected file becomes unavailable. Invalid or missing JSON must fail closed by hiding/clearing the evaluation card, not by showing an adoptable state.
- When visible, the panel should show recommendation, metrics, blocker detail, and next action inside the Model Center decision flow, above the collapsed lifecycle detail table.
- The panel is visibility/presentation only; it must not change training, inference, evaluation thresholds, model registry persistence, or annotation hot paths.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose anomaly --review-tab yolo --right-workflow-expanded --anomaly-classification-evaluation-summary .\artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-late-recheck\classification-evaluation-20260708-011412\classification-evaluation-summary.json --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-anomaly-evaluation-run-button-after-20260708-1920.png
git diff --check
```

Latest evidence:

```text
Baseline/no-summary: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-anomaly-evaluation-baseline-no-summary-1920.png
After/summary-loaded: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-anomaly-evaluation-after-1920.png
PASS Anomaly classification evaluation blocks weak adoption evidence
PASS WPF labeling shell can be constructed without the WinForms shell
WPF visual smoke anomaly evaluation: hold with 4 images, 25% confidence-gated accuracy, and 2 low-confidence class matches
```

Latest runtime/evaluation recheck:

```text
Python adapter compile: passed
Python adapter --self-test: passed
PASS WPF YOLOv8 anomaly classification runtime smoke maps image-level candidates
Evaluation summary: C:\Git\Labelling_Application\artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-recheck\classification-evaluation-20260708-004229\classification-evaluation-summary.json
After: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-anomaly-evaluation-recheck-20260708-1920.png
Result: hold, 4 images, normal 1/2, abnormal 0/2, confidence-gated accuracy 25%, low-confidence class matches 2
Manual load action after: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-anomaly-evaluation-load-summary-after-20260708-1920.png
Run/load actions after: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-anomaly-evaluation-run-button-after-20260708-1920.png
```

## Segmentation Label Navigation Auto-Save

Status: stable for focused WPF SEG navigation coverage as of 2026-07-07.

Protected behavior:

- Loading a different image must first save dirty annotations for the previous active image.
- Pending Brush/Eraser mask strokes must be flushed before the active image state is cleared.
- A pending brush mask must leave both segment JSON and a non-empty mask PNG for the previous image.
- Moved segmentation polygons must persist their updated coordinates before image navigation.
- DataGrid queue-click navigation must use the same save-before-load boundary; clicking another row after a brush stroke must save the previous image and reload it when returning.
- Keyboard queue navigation must use the same save-before-load boundary; moving with `Down` after a brush stroke must save the previous image and reload it when returning.
- If the current annotations cannot be saved, image navigation should stop instead of silently discarding edits.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-loads-canvas
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-keyboard-navigation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
git diff --check
```

Latest evidence:

```text
PASS WPF image navigation auto-saves pending brush masks
PASS WPF image navigation auto-saves moved segmentation labels
PASS WPF image queue click loads canvas and preserves pending labels
PASS WPF image queue click uses the lightweight load path
PASS WPF image queue arrow keys load adjacent images
```

## YOLOv8 SEG Fine-Tune Model Center Adoption

Status: stable for focused WPF adoption evidence of the circular-defect fine-tune candidate as of 2026-07-08.

Protected behavior:

- `WpfTrainingWeightsService` should select a matching `runs\segment\...\weights\best.pt` candidate when that run's `args.yaml` points at the current dataset `data.yaml`.
- The fine-tuned candidate should be staged as a pending inspection-model candidate before recipe save.
- Saving through the existing Model Center/model-settings command should persist the candidate to recipe config and record it as the current inspection model in model registry adoption history.
- The focused smoke should use the actual `C:\Git\yolov8` run and circular SEG artifact, not a synthetic mock `best.pt`.
- The same fine-tuned weight should continue to pass the real YOLO current-image TCP workflow smoke with segmentation polygon output that can be confirmed into label text, segment JSON, and mask PNG.

Required gates before reporting this path complete again:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-center-real-candidate-save --yolo-root C:\Git\yolov8 --data-yaml .\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data.yaml --baseline-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt --candidate-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png
$env:LABELING_SMOKE_PROJECT_ROOT='C:\Git\yolov8'; $env:LABELING_SMOKE_CLIENT_SCRIPT='C:\Git\yolov8\labeling_tcp_client.py'; $env:LABELING_SMOKE_MODEL_ENGINE='YOLOv8'; $env:LABELING_SMOKE_MODEL_ROOT='C:\Git\yolov8\ultralyticsMaster'; $env:LABELING_SMOKE_PYTHON_EXE='C:\Git\yolov8\.venv\Scripts\python.exe'; $env:LABELING_SMOKE_WEIGHTS='C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt'; $env:LABELING_SMOKE_IMAGE_ROOT='C:\Git\Labelling_Application\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data\test\images'; $env:LABELING_SMOKE_IMAGE_PATH='C:\Git\Labelling_Application\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data\test\images\025_NG.png'; $env:LABELING_SMOKE_IMAGE_SIZE='160'; $env:LABELING_SMOKE_CONFIDENCE='0.25'; $env:LABELING_SMOKE_IOU='0.7'; $env:LABELING_SMOKE_EXPECT_SEGMENTATION='true'; $env:LABELING_SMOKE_ARTIFACT_ROOT='C:\Git\Labelling_Application\artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260707'; dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-yolo-smoke
git diff --check
```

Latest evidence:

```text
WPF model-center real candidate save: candidate=C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt
WPF model-center real candidate save captured: C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-real-finetune-save-recheck-20260708-latest-1920.png
PASS Real YOLO TCP workflow detects, overlays, confirms, and saves labels
summary: C:\Git\Labelling_Application\artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260708-latest\summary.txt
latest comparison: C:\Git\Labelling_Application\artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260708-latest\20260708-185046\comparison-summary.json
latest adapter sweep: OK images 0 candidates; 024_NG 1 NG candidate at 0.5065; 025_NG 1 NG candidate at 0.7682; 022_NG/023_NG/032_NG 0 candidates at confidence 0.25.
```

## YOLOv8 SEG Real EXE Workflow

Status: stable for the local circular-defect EXE workflow after a fresh 2026-07-07 built-EXE pass.

Protected behavior:

- The EXE can create a segmentation recipe from the dataset workflow.
- The EXE can select the parent `images` folder that contains `OK` and `NG` child folders.
- NG brush labels must save segment JSON and mask PNG artifacts.
- OK child-folder images must contribute empty/background label files for segmentation training.
- The local `C:\Git\yolov8` worker path must be able to train YOLOv8 SEG, apply the resulting `best.pt`, and run current-image inference from the trained model.
- EXE automation helpers may retry stale UIA combo/dialog states, but should not change product runtime behavior.

Required gates before reporting this workflow complete again:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --exe-circular-segmentation-workflow --exe "C:\Git\Labelling_Application\artifacts\run\Debug\OpenVisionLab.LabelingStudio.exe" --image-root "D:\circular_defect_labeling_dataset_v1\images" --yolov8-root "C:\Git\yolov8" --label-count 12
git diff --check
```

Latest evidence:

```text
EXE_CIRCULAR_SEGMENTATION_WORKFLOW recipe=circular_seg_exe_20260707_010325
EXE_CIRCULAR_SEGMENTATION_WORKFLOW trainedWeights=C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt
EXE_CIRCULAR_SEGMENTATION_WORKFLOW trainSegments=6 validSegments=3 testSegments=3 backgroundLabels=20
EXE_CIRCULAR_SEGMENTATION_WORKFLOW inferenceStatus=후보: NG 1.3%  크기 104x105 / 위치 x=1, y=0 / 추론: 완료  모델 YOLOv8 / openvisionlab-yolov8-segment\best.pt / 후보 1
```

## Canvas Brush-Size Toolbar and Candidate Review Wording

Status: stable for canvas brush-size discoverability and Stage 3 `AI 후보 검토` wording after focused WPF command, candidate-review, MVVM, public tutorial, and 1920x1080 visual smoke coverage.

Protected behavior:

- The canvas toolbar shows brush-size controls only for Brush/Eraser tools.
- The toolbar controls update the shared `WpfLearningWorkflowPanelViewModel.BrushSize`; brush/eraser rendering must continue to use that existing workflow value.
- The canvas brush-size text must stay synced when the workflow panel slider changes.
- Brush-size controls must not introduce new brush/eraser commit work during pointer movement.
- Candidate Review and Stage 3 guidance should use `AI 후보 검토` consistently for model-generated candidates.
- The top Stage 3 workflow button must decode to `3 AI 후보`, the inference mode switcher must decode to `AI 후보 검토`, and decoded XAML must not contain the ambiguous `추론 검토` label.
- Candidate Review guidance must distinguish action semantics: confirm adds a saved label, while skip hides only the AI candidate.
- Public README/tutorial copy and the 12번 candidate-review screenshot should not reintroduce the ambiguous `추론 검토` label for the operator-facing review step.
- Candidate Review current-image controls must stay above model-validation controls by actual `Grid.Row` placement, not source declaration order.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-canvas-panel-commands
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-layout
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-presentation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-mask-drag-performance
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --annotation-tool brush --review-tab guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-brush-size-toolbar-after-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab candidates --width 1920 --height 1080 --output .\artifacts\ui\wpf-stage3-ai-candidate-button-after-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Latest evidence:

```text
PASS WPF canvas panel declares viewer commands
PASS WPF candidate review supports navigation and focus commands
PASS WPF candidate rows show visual review status
PASS WPF candidate review presentation marks AI candidates as unsaved
PASS WPF labeling shell can be constructed without the WinForms shell
PASS MVVM infrastructure observable and command helpers
PASS WPF mask brush drag commit performance
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-brush-size-toolbar-after-1920.png
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-stage3-ai-candidate-button-after-1920.png
```

## Image Queue Row Thumbnails

Status: stable for compact visible-row thumbnails after focused WPF structure and 1920x1080 visual smoke coverage.

Protected behavior:

- Queue row thumbnails are small, per-row previews before the status icon and filename.
- `WpfImageQueueItem.ThumbnailSource` must load lazily and return a frozen image source so visible rows can bind safely.
- Thumbnail decode should stay small, currently `DecodePixelWidth = 42`.
- Thumbnail binding should remain asynchronous from XAML and must not replace the normal queue shell load path.
- Queue row virtualization, row status icon/badge, tooltips, automation names, click loading, and arrow-key navigation must stay intact.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-keyboard-navigation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --annotation-tool brush --review-tab guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-image-queue-thumbnail-after-1920.png
git diff --check
```

Latest evidence:

```text
PASS WPF image queue presents row status with icons
PASS WPF image queue arrow keys load adjacent images
PASS WPF image queue click uses the lightweight load path
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-image-queue-thumbnail-after-1920.png
```

## Image Queue Large-Folder Thumbnail Performance

Status: stable for bulk queue replacement, visible-row lazy thumbnails, and thumbnail file-handle release after focused 1200-item WPF fixture coverage.

Protected behavior:

- Loading an image folder into the queue should replace shell rows with one bulk reset notification, not one notification per image.
- Queue load must not create thumbnails for every image path.
- Thumbnail creation should remain lazy and should start only when a row thumbnail is requested.
- Thumbnail bitmaps should be loaded with `OnLoad`, frozen, and released from their source file stream so the image file is not locked by preview display.
- Existing row status, click loading, arrow-key navigation, quick filters, and row virtualization must stay intact.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-large-folder-performance
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-keyboard-navigation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
git diff --check
```

Latest evidence:

```text
LARGE_QUEUE_LOAD_MS=132.8; ITEMS=1200; COLLECTION_ACTIONS=1
PASS WPF image queue large folder keeps bulk and lazy thumbnail behavior
PASS WPF image queue presents row status with icons
PASS WPF image queue arrow keys load adjacent images
PASS WPF image queue click uses the lightweight load path
PASS WPF image queue selection service owns queue state decisions
PASS WPF labeling shell can be constructed without the WinForms shell
```

## Image Queue Compact Filter Layout

Status: stable for the current three-column quick-filter layout after focused WPF structure and 1920x1080 visual smoke coverage.

Protected behavior:

- The image queue quick filters use three columns so the queue list starts higher in the right panel.
- All seven quick filters remain visible and bound to the existing `WpfImageQueuePanelViewModel` commands/state.
- The selected quick-filter active styling, count text, and tooltips remain unchanged.
- The current-image task card and queue DataGrid bindings remain unchanged.
- This layout change must not alter queue click loading, arrow-key navigation, or row virtualization.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-keyboard-navigation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --annotation-tool brush --review-tab guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-image-queue-density-after-1920.png
git diff --check
```

Latest evidence:

```text
PASS WPF image queue presents row status with icons
PASS WPF image queue arrow keys load adjacent images
PASS WPF image queue click uses the lightweight load path
WPF visual smoke captured: C:\Git\Labelling_Application\artifacts\ui\wpf-image-queue-density-after-1920.png
```

## Image Queue Keyboard Navigation

Status: stable for shell-level adjacent image navigation after focused WPF coverage.

Protected behavior:

- `Down` and `Right` open the next visible queue image.
- `Up` and `Left` open the previous visible queue image.
- Navigation uses the current filtered/searched queue view, not hidden rows.
- Arrow navigation must reuse the normal queue image-load path so dirty-label auto-save and lightweight image loading still apply.
- Arrow navigation must not steal key input from text boxes, combo boxes, or sliders.
- Navigation does not wrap at the first or last visible item.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-keyboard-navigation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service
git diff --check
```

Latest evidence:

```text
PASS WPF image queue arrow keys load adjacent images
PASS WPF image queue click uses the lightweight load path
PASS WPF image queue selection service owns queue state decisions
```

## SEG Label Edit Navigation Persistence

Status: stable for moved manual segmentation labels and dirty image navigation after focused WPF coverage.

Protected behavior:

- Moving a selected manual SEG polygon, polygon point, or raster mask must mark the current image dirty before the operator leaves the image.
- Image navigation must save the currently loaded dirty annotations before clearing the canvas and loading the next image.
- Pending brush-stroke work must be flushed through the normal save path before image navigation when the current image has an active path.
- If the pre-navigation save fails, navigation must stop so the current unsaved labels stay visible.
- The save must target the previous active image name/path, not the next image being loaded.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-mask-drag-performance
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-mask-dirty-bounds
git diff --check
```

Latest evidence:

```text
PASS WPF image navigation auto-saves moved segmentation labels
PASS WPF segmentation object manipulation verification matrix passes
PASS WPF segmentation object manipulation updates shell state
PASS WPF mask brush drag commit performance
PASS WPF mask overlay dirty bounds reset
```

## YOLOv8 Segmentation Model Comparison Validation Path

Status: stable for request generation, preflight validation, and script syntax after focused service coverage.

Protected behavior:

- YOLOv5 model comparison continues to use the existing local `val.py` runner.
- YOLOv8/YOLO11 local project roots may resolve `ultralyticsMaster` or a direct `ultralytics` package checkout for validation.
- Segmentation projects must pass `ModelTask=segment` separately from the held-out split `Task=val|test`.
- The comparison script must accept both YOLOv5 `val.py` and local Ultralytics source roots.
- Segmentation prediction labels written with `save-conf` must count confidence from the final label token.
- YOLOv8/YOLO11 segmentation model comparison must reject held-out splits that only have empty OK/background labels, bbox-only labels, or malformed segment labels; at least one positive YOLO segment label line with paired coordinates is required before validation launches.
- YOLOv8/YOLO11 model comparison must reject same-count but different-name class lists. `data.yaml`, baseline weights, and candidate weights must have the same ordered class names before validation launches.
- Segmentation readiness diagnostics must warn when the held-out test split contains OK/background images but no positive NG mask image.
- Comparison summaries and reports must include a promotion recommendation. Low-precision candidates, currently below `0.10`, must be marked `hold` so they are not mistaken for production-ready promotion evidence.
- Comparison summaries and reports must include held-out evidence counts, and promotion must stay `hold` below the 10 labeled-image recommendation even if the candidate metrics improve.
- Segmentation promotion must also stay `hold` below 5 positive held-out segmentation label lines, so OK/background labels cannot satisfy promotion evidence by themselves.
- Segmentation promotion must also stay `hold` below 5 distinct positive held-out segmentation images, so one image with several polygons cannot satisfy promotion evidence by itself.
- Promotion must stay `hold` when the candidate produces zero UI-threshold candidates at the configured UI confidence, even if precision and mAP improve.
- Promotion summaries must preserve multiple hold blockers in `promotion.reasons` when more than one adoption risk applies, such as weak held-out evidence plus zero UI-threshold candidates.
- Candidate confidence summaries should preserve `thresholdSweep` counts so operators can see whether lower review thresholds produce a usable number of candidates or a noisy flood, without changing the default promotion threshold.
- The WPF Candidate Review model-comparison detail must surface the promotion recommendation from `comparison-summary.json`.
- The WPF Candidate Review latest model-comparison lookup must match the current baseline and candidate weight paths, so stale summaries from other candidates cannot drive the current adoption state.
- The WPF Candidate Review model-comparison detail must not expose raw script promotion reasons such as `Candidate precision`; low-precision hold reasons should be translated into operator-facing Korean while preserving the evidence values.
- The WPF Candidate Review model-comparison detail should translate weak held-out evidence and zero UI-threshold candidate hold reasons into operator-facing Korean while preserving counts/confidence values, including when those reasons are reported together.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-review-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-heldout
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-weights-service
git diff --check
```

Latest evidence:

```text
PASS WPF model comparison run service builds script requests
PASS WPF model comparison button requires held-out test split
PASS WPF model comparison review service builds disagreement examples and surfaces promotion hold recommendations
PASS WPF training weights service selects latest best.pt
PASS WPF training readiness presentation is operator-readable
PASS YOLOv8 segmentation comparison rejects empty-background-only and bbox-only held-out labels
EXPECTED_BLOCK Model comparison cannot start: dataset labels=1 [NG], baseline labels=1 [Defect], candidate labels=1 [NG].
PASS YOLOv8 SEG same-class val comparison wrote comparison-summary.json for NG baseline-vs-candidate; both models scored mAP50=0.0 and UI candidates=0, so this is workflow evidence only.
PASS YOLOv8 SEG same-class true-test comparison wrote comparison-summary.json for `circular_seg_exe_20260706_183245`; the 30ep/img128 candidate improved over baseline to recall=1.0, mAP50=0.105, mAP50-95=0.044, but precision=0.016 keeps this as held-out evidence only, not promotion evidence.
PASS YOLOv8 SEG promotion recommendation comparison wrote `promotion.recommendation=hold` for the same 30ep/img128 candidate because precision=0.016 is below the 0.10 minimum.
PASS YOLOv8 SEG promotion evidence count guard keeps `promotion.recommendation=hold` below 10 held-out labeled images and writes `evidence.comparisonLabelCount` to `comparison-summary.json`.
PASS WPF model comparison review service translates low-precision promotion hold reasons and blocks raw `Candidate precision` text from Candidate Review detail.
PASS YOLOv8 SEG userseed true-test comparison wrote precision=0.121, recall=0.333, mAP50=0.223, mAP50-95=0.064, UI candidates=0, and `promotion.recommendation=hold` because real held-out evidence is 9/10.
PASS YOLOv8 SEG zero-UI-candidate guard fixture wrote evidence=10/10, UI candidates=0, and `promotion.recommendation=hold` with the UI-threshold candidate reason.
PASS WPF model comparison review service translates weak-evidence and zero-UI-candidate promotion hold reasons without exposing raw script text.
PASS YOLOv8 SEG userseed multi-reason rerun wrote `promotion.reasons` with both 9/10 held-out evidence and 0 UI-threshold candidates, and the Markdown report listed both blockers plus `UI candidates: 0 / required 1`.
PASS YOLOv8 SEG userseed threshold-sweep rerun wrote candidate counts `0.25=0`, `0.10=0`, `0.05=3`, and `0.01=259`, while keeping `promotion.recommendation=hold`.
PASS YOLOv8 SEG userseed positive-evidence rerun wrote `positiveSegmentationLabelLineCount=3`, `minimumPositiveSegmentationLabelLineCount=5`, and kept `promotion.recommendation=hold`.
PASS YOLOv8 SEG userseed positive-image rerun wrote `positiveSegmentationImageCount=3`, `minimumPositiveSegmentationImageCount=5`, and kept `promotion.recommendation=hold`.
```

## YOLOv8 Segmentation OK/NG Local Folder Dataset Prep

Status: stable after focused parent-folder queue coverage and direct EXE parent OK/NG segmentation training workflow verification.

Protected behavior:

- Selecting a local image parent with no direct images but child folders such as `OK` and `NG` should populate the image queue from supported images under those children.
- Direct image folders keep their direct-folder behavior; recursive fallback is only used when the selected parent has no direct supported images.
- During segmentation training preparation, images under an `OK` child folder are copied into the YOLO split output as background/negative samples with empty `labels/*.txt` files.
- When split settings change, generated OK background copies and empty labels must be removed from old non-selected split folders while preserving any real segment JSON or mask PNG artifacts.
- Segmentation readiness accepts maskless images only when an empty background label file exists; unlabeled images without segment/mask artifacts and without an empty label remain blocking.
- YOLOv8 segmentation training preparation must keep at least one train split polygon label and one valid split polygon label before reporting ready.
- The real EXE parent-folder workflow can select `D:\circular_defect_labeling_dataset_v1\images`, label NG masks, include OK background labels, train YOLOv8 segmentation, apply the trained `best.pt`, and reach inference review.
- After a trained `best.pt` is saved as the inspection model, current-image inference must restart/reload the Python worker when the previous connected worker was started with different weights, and the next EXE inference result must come from the trained `NG` class rather than COCO seed classes.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-readiness-purpose
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --yolov8-segmentation-app-dataset-fixture
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-readiness-presentation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --exe-circular-segmentation-workflow --exe .\artifacts\isolated-out\OpenVisionLab.LabelingStudio.exe --image-root D:\circular_defect_labeling_dataset_v1\images --yolov8-root C:\Git\yolov8 --label-count 8
git diff --check
```

Latest evidence:

```text
PASS WPF image queue click uses the lightweight load path
PASS YOLO dataset readiness follows selected dataset purpose
PASS YOLOv8 segmentation app dataset fixture exports polygon labels
PASS WPF training readiness presentation is operator-readable
PASS OK background split cleanup removes stale empty train/valid files when settings move those samples to test
EXE_CIRCULAR_SEGMENTATION_WORKFLOW recipe=circular_seg_exe_20260706_105323 trainSegments=6 validSegments=2 backgroundLabels=20
EXE_CIRCULAR_SEGMENTATION_WORKFLOW recipe=circular_seg_exe_20260706_183245 trainSegments=6 validSegments=3 testSegments=3 backgroundLabels=20 inferenceStatus="후보: NG 1.3% ... / 추론: 완료  모델 YOLOv8 / openvisionlab-yolov8-segment\best.pt / 후보 1"
Screenshots: artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260706_105323\screenshots
Screenshots: artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260706_183245\screenshots
```

## YOLOv8 Segmentation EXE Workflow

Status: stable after direct EXE recipe, labeling, training, model-apply, and inference-review verification.

Protected behavior:

- A local `C:\Git\yolov8` Ultralytics checkout with `labeling_tcp_client.py` can train YOLOv8 segmentation from the EXE-generated segmentation dataset.
- WPF training progress must recover from `ModelStatusResult.training` polling if an async `TrainingStatus` frame is missed.
- The real EXE can create a segmentation recipe, load the parent OK/NG image folder, save NG brush masks/segment JSON, include OK empty-label background samples, train YOLOv8 segmentation, apply the trained `best.pt`, and show inference review evidence.
- Applying a trained `best.pt` must invalidate a stale connected Python worker before current-image inference; the next inference may wait for model preload and must report the trained dataset class, not a stale COCO class.

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-status-protocol
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --exe-circular-segmentation-workflow --exe .\artifacts\isolated-out\OpenVisionLab.LabelingStudio.exe --image-root D:\circular_defect_labeling_dataset_v1\images --yolov8-root C:\Git\yolov8 --label-count 8
git diff --check
```

Latest evidence:

```text
EXE_CIRCULAR_SEGMENTATION_WORKFLOW recipe=circular_seg_exe_20260706_105323
EXE_CIRCULAR_SEGMENTATION_WORKFLOW trainedWeights=C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt
EXE_CIRCULAR_SEGMENTATION_WORKFLOW trainSegments=6 validSegments=2 backgroundLabels=20
EXE_CIRCULAR_SEGMENTATION_WORKFLOW recipe=circular_seg_exe_20260706_183245
EXE_CIRCULAR_SEGMENTATION_WORKFLOW inferenceStatus=후보: NG 1.3% ... / 추론: 완료  모델 YOLOv8 / openvisionlab-yolov8-segment\best.pt / 후보 1
Screenshots: artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260706_105323\screenshots
Screenshots: artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260706_183245\screenshots
```

## Annotation Undo/Redo Shortcuts

Status: stable after focused WPF command and direct EXE verification.

Protected behavior:

- Ctrl+Z routes to annotation undo from the shell preview-key command.
- Ctrl+Shift+Z routes to annotation redo, not another undo.
- Ctrl+Y remains a redo shortcut.
- Shortcut handling must ignore text-editing controls so normal text editing keeps native undo/redo behavior.
- Pending Brush/Eraser strokes enable Undo immediately while CPU mask/history materialization remains deferred.
- Undoing a pending brush stroke must clear the FBO mask-stroke preview texture and rebuild committed segmentation overlays from the restored snapshot.

Do not change these paths casually:

- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ShellInputCommands.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationHistory.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationToolSelectionCommands.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-undo-redo-shortcuts
dotnet build .\OpenVisionLab.LabelingStudio.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\run\Debug\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --exe-undo-redo-smoke --exe .\artifacts\run\Debug\OpenVisionLab.LabelingStudio.exe --output .\artifacts\ui\exe-undo-redo-smoke.png
```

Latest evidence:

```text
PASS WPF shell undo/redo keyboard shortcuts restore annotation state
WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=5.231 WPF_MASK_BRUSH_RELEASE_MS=4.861 WPF_MASK_BRUSH_EXISTING_DRAG_1000_MOVE_MS=2.623 WPF_MASK_BRUSH_EXISTING_RELEASE_MS=2.878 WPF_MASK_TOOL_END_CALL_MS=0.241 SEGMENTS=1 UNDO_AFTER=0 UNDO_BUTTON_IMMEDIATE=True UNDO_BUTTON_AFTER=True SECOND_UNDO_BUTTON_IMMEDIATE=True SECOND_UNDO_BUTTON_AFTER=True UNDO_AFTER_TOOL_END=2
EXE_UNDO_REDO_PIXEL_CHECK createdDiff=6236 undoResidual=524 redoDiff=6236 redoOverlap=6236 createdRedoDelta=0 probe=1060,699,102,95
EXE_UNDO_REDO_SMOKE recipeApplied=False imageLoaded=True roiCreated=True brushRoute=True randomBrushStrokes=4 undoButtonAfterDraw=True ctrlZRemoved=True ctrlShiftZRestored=True createdDiff=6236 undoResidual=524 redoDiff=6236 redoOverlap=6236 createdRedoDelta=0 ctrlZMs=267.7 ctrlShiftZMs=255.3
```

## Segmentation Polygon Selection and Drag Editing

Status: stable after focused WPF template-source and segmentation object manipulation coverage.

Protected behavior:

- A selected manual segmentation object from Object Review must be accepted as a template-matching source, using the current SEG object bounds and class name.
- A single manual segmentation object should be usable as the template source when no manual box exists.
- Whole-image template auto-save in a segmentation-purpose dataset must create `segments/*.json` and `masks/*.png`, not only YOLO box `labels/*.txt`.
- When the template source is a manual SEG polygon, whole-image template auto-save should transfer that source polygon shape into matched target positions instead of saving rectangle fallback polygons.
- When the template source is a manual raster-mask SEG object, whole-image template auto-save should snapshot the source mask and transfer a non-rectangular target mask outline without touching brush/eraser MouseMove hot paths.
- Template-batch generated SEG artifacts must export to normalized YOLOv8 segmentation `labels/*.txt` lines and keep app-dataset readiness passing.
- Existing segment JSON files should block duplicate SEG template batch saves; stale bbox-only txt labels should not block creating real SEG artifacts.
- SEG queue label status should count segment JSON objects first. Bbox-only txt lines without segment JSON are stale/invalid for SEG, while empty txt files can still mark reviewed background images.
- Selected polygon point/body and raster-mask drags must not rebuild Object Review or recount queue status on every MouseMove.
- Object Review and queue status may refresh once on MouseUp when the selected SEG object actually changed.

Do not change these files casually:

- `0. UI/9) WPF/Views/WpfLabelingShellWindow.TemplateMatchingCommands.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationSegmentEdit.cs`
- `1. Core/TemplateMatchingBatchAutoLabelService.cs`
- `Yolo/YoloImageLabelStatusService.cs`
- `0. UI/9) WPF/Services/WpfPolygonAnnotationService.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/ViewModel/RoiImageCanvasViewModel.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --template-batch-autolabel-storage
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --template-guide-ux
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --yolov8-segmentation-app-dataset-fixture
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --yolo-label-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-template-current-image-no-candidate
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
git diff --check
```

Latest evidence:

```text
PASS WPF template source accepts selected manual segmentation objects
PASS WPF template no-candidate result preserves saved label status
PASS Template batch auto label saves segmentation artifacts for SEG datasets
PASS Template batch auto label transfers raster mask source shape for SEG datasets
PASS Template auto label shows actionable guide
PASS YOLOv8 segmentation app dataset fixture exports polygon labels
PASS Local YOLOv8 segment train accepts the template-batch app fixture and writes best.pt/last.pt/results.csv under artifacts\yolov8-seg-training-smoke\template-batch-fixture
PASS YOLO label lookup stays isolated to the active dataset
PASS WPF segmentation object manipulation verification matrix passes
PASS WPF segmentation object manipulation updates shell state
```

## Brush and Eraser Performance

Status: stable after focused verification.

Protected behavior:

- Brush and eraser MouseMove use the OpenGL/FBO preview path for visible feedback.
- CPU mask/history work is queued and committed after MouseUp instead of running for every MouseMove.
- Active Brush/Eraser drag must not materialize CPU MaskData/history/Object Review mask rows; the FBO preview owns live feedback.
- MouseUp itself must not block on Object Review or image-queue presentation work. After the quiet idle window, the completed stroke may materialize one mask label row/history entry and queue save-state refresh even while Brush/Eraser remains selected.
- The materialized raster mask row is selected after quiet idle so operators can immediately apply a class; it must not appear or steal focus during active drag/MouseUp.
- Changing the selected raster mask class from the real EXE Object Review panel must update both the row class and the visible canvas mask color.
- MouseUp immediately marks the annotation save state as dirty/pending while the FBO preview remains visible; do not wait for delayed CPU materialization before showing "save needed".
- Brush stroke preview fill must use the same mask opacity as committed raster mask overlays.
- Preview FBO base refresh must copy committed mask texture alpha without applying an extra blend pass; otherwise older/materialized mask areas look more transparent than the newest stroke.
- Object Review and undo history for real mask labels are updated once per completed stroke at quiet-idle/tool-end flush time, not continuously during drag.
- Committed mask rows are auto-selected only after quiet-idle materialization; handles/labels must not pop over the brush path during active painting.
- Raster-mask overlay updates use dirty bounds and `glTexSubImage2D` when the texture shape is unchanged.
- Large mask move/copy paths avoid per-pixel full-image loops on MouseMove.

Do not change these files casually:

- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationMask*.cs`
- `0. UI/9) WPF/Services/WpfMaskAnnotationService.cs`
- `0. UI/9) WPF/Services/WpfMaskEditStateService.cs`
- `0. UI/9) WPF/Services/WpfMaskStrokeHistoryDraftService.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/ViewModel/RoiImageCanvasViewModel.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/ViewModel/RoiImageCanvasMaskOverlay.cs`

Required gates before reporting a change complete:

```powershell
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --brush-hover-performance
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-mask-drag-performance
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mask-move-performance
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --exe-mask-tools-smoke --seed 260626 --brush-strokes 8 --eraser-strokes 4
powershell -ExecutionPolicy Bypass -File scripts\verify-wpf-segmentation-object-interactions.ps1
```

Latest evidence:

```text
WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=5.589 WPF_MASK_BRUSH_RELEASE_MS=4.641 WPF_MASK_BRUSH_EXISTING_DRAG_1000_MOVE_MS=11.558 WPF_MASK_BRUSH_EXISTING_RELEASE_MS=3.954 WPF_MASK_TOOL_END_CALL_MS=0.243 SEGMENTS=1 UNDO_DURING=0 UNDO_AFTER=0 UNDO_AFTER_SECOND=0 UNDO_AFTER_TOOL_END=2 COLLECTION_CHANGED_AFTER=0 SECOND_COLLECTION_CHANGED_AFTER=0 QUEUE_CHANGED_AFTER=0 SECOND_QUEUE_CHANGED_AFTER=0 QUEUE_CHANGED_AFTER_TOOL_END=45 MODEL_STATUS_CHANGED=6 RELEASE_ACTIONS= SECOND_RELEASE_ACTIONS= TOOL_END_ACTIONS=Replace
WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=3.792 WPF_MASK_BRUSH_RELEASE_MS=5.080 WPF_MASK_BRUSH_EXISTING_DRAG_1000_MOVE_MS=2.782 WPF_MASK_BRUSH_EXISTING_RELEASE_MS=3.179 WPF_MASK_TOOL_END_CALL_MS=0.388 SEGMENTS=1 UNDO_DURING=0 UNDO_AFTER=1 UNDO_AFTER_SECOND=2 UNDO_AFTER_TOOL_END=2 OBJECT_ROWS_AFTER=1 SELECTED_AFTER=True SELECTED_AFTER_SECOND=True COLLECTION_CHANGED_AFTER=1 SECOND_COLLECTION_CHANGED_AFTER=2 QUEUE_CHANGED_AFTER=45 SECOND_QUEUE_CHANGED_AFTER=30 PREVIEW_AFTER=True PREVIEW_AFTER_TOOL_END=False
EXE_MASK_TOOLS_SMOKE seed=260704 brushStrokes=10 eraserStrokes=5 brushInputMs=2662.3 brushAvgMs=266.2 brushMaxMs=319.1 brushSwitchUiMs=70.7 brushCommitWaitMs=0.0 eraserInputMs=895.8 eraserAvgMs=179.2 eraserMaxMs=208.0 eraserCommitWaitMs=534.6 eraserInternalWaitMs=284.6 eraserToolEndWaitMs=0.0 eraserInternalQueueMs=22.7 selectExitMs=91.4 eraserImmediateWheelUiMs=71.6 brushSelected=True eraserSelected=True
EXE_MASK_CLASS_RECOLOR_SMOKE seed=260705 brushStrokes=5 sourceClass=(unknown) targetClass=OK canvasDiff=35018 rowUpdated=True applyEnabled=True
EXE_UNDO_REDO_SMOKE recipeApplied=False imageLoaded=True roiCreated=True brushRoute=True randomBrushStrokes=4 undoButtonAfterDraw=True ctrlZRemoved=True ctrlShiftZRestored=True createdDiff=5997 undoResidual=524 redoDiff=5997 redoOverlap=5997 createdRedoDelta=0
EXE_MASK_TOOLS_SMOKE seed=260709 brushStrokes=10 eraserStrokes=5 brushInputMs=2517.7 brushAvgMs=251.8 brushMaxMs=320.0 brushSwitchUiMs=75.3 brushCommitWaitMs=0.0 eraserInputMs=801.5 eraserAvgMs=160.3 eraserMaxMs=175.2 eraserCommitWaitMs=1048.8 eraserImmediateWheelUiMs=17.7 selectExitMs=49.2 brushSelected=True eraserSelected=True
WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=9.272 WPF_MASK_BRUSH_RELEASE_MS=23.608 WPF_MASK_BRUSH_EXISTING_DRAG_1000_MOVE_MS=20.039 WPF_MASK_BRUSH_EXISTING_RELEASE_MS=26.848 WPF_MASK_TOOL_END_CALL_MS=0.375 SEGMENTS=1 UNDO_DURING=0 UNDO_AFTER=0 UNDO_AFTER_SECOND=0 UNDO_AFTER_TOOL_END=2 COLLECTION_CHANGED_AFTER=0 SECOND_COLLECTION_CHANGED_AFTER=0 COLLECTION_CHANGED_AFTER_TOOL_END=1 RELEASE_ACTIONS= SECOND_RELEASE_ACTIONS= TOOL_END_ACTIONS=Replace
EXE_MASK_TOOLS_SMOKE seed=260708 brushStrokes=10 eraserStrokes=5 brushInputMs=2239.0 brushAvgMs=223.9 brushMaxMs=304.1 brushSwitchUiMs=117.0 brushCommitWaitMs=0.0 eraserInputMs=824.7 eraserAvgMs=164.9 eraserMaxMs=176.0 eraserCommitWaitMs=1234.1 eraserImmediateWheelUiMs=18.9 selectExitMs=108.0 brushSelected=True eraserSelected=True
WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=3.504 WPF_MASK_BRUSH_RELEASE_MS=4.597 WPF_MASK_BRUSH_EXISTING_DRAG_1000_MOVE_MS=2.905 WPF_MASK_BRUSH_EXISTING_RELEASE_MS=3.453 WPF_MASK_TOOL_END_CALL_MS=0.305 UNDO_BUTTON_IMMEDIATE=True UNDO_BUTTON_AFTER=True COLLECTION_CHANGED_AFTER=0 QUEUE_CHANGED_AFTER=0
EXE_UNDO_REDO_SMOKE recipeApplied=False imageLoaded=True roiCreated=True brushRoute=True randomBrushStrokes=4 undoButtonAfterDraw=True ctrlZRemoved=True ctrlShiftZRestored=True createdDiff=6236 undoResidual=524 redoDiff=6236 redoOverlap=6236 createdRedoDelta=0
WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=5.693 WPF_MASK_BRUSH_RELEASE_MS=9.883 WPF_MASK_BRUSH_EXISTING_DRAG_1000_MOVE_MS=3.017 WPF_MASK_BRUSH_EXISTING_RELEASE_MS=3.442 WPF_MASK_TOOL_END_CALL_MS=0.799
BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=33.362 HOVER_EVENTS=1000 BRUSH_PROPERTY_CHANGED=3 STATUS_PROPERTY_CHANGED=5
EXE_MASK_TOOLS_SMOKE seed=260626 brushStrokes=8 eraserStrokes=4 brushInputMs=1846.3 brushAvgMs=230.8 brushMaxMs=300.1 brushSwitchUiMs=20.1 eraserImmediateWheelUiMs=19.1 brushSelected=True eraserSelected=True
```

## ROI and Texture MouseMove Performance

Status: stable after 500k-object and texture-pan focused checks.

Protected behavior:

- ROI MouseMove must not rebuild every ROI/object overlay.
- Texture pan/zoom must keep the cached ROI scene and throttle expensive overlay work.
- Single ROI movement/deletion must update only the affected object state where possible.
- ROI viewport rendering must use the spatial index and capped visible-shape cache; list-returning viewport queries pre-size their collections to avoid GC/rehash spikes after 500K-object runs.
- Rectangle drawing must test the mouse-down point in image pixel coordinates, not raw OpenGL/world coordinates; black viewer/letterbox space must not start a box.
- Selected ROI handles may be grabbed on the visible handle halo just outside the rectangle, but general overlay selection must stay strict and must not select a ROI from outside the labeled rectangle.
- ROI handle hit tolerances and rendered handle sizes use the same screen-pixel-to-world conversion. Handles are screen-pixel affordances, currently 14px by default, and must not shrink on zoom-in. Do not reintroduce `handleSize / zoomScale` or a fixed world-unit cap.
- Cross-image ROI `Ctrl+C`/`Ctrl+V` is a repeat-labeling workflow: if the copied ROI does not exist in the current image overlay manager, paste a new ROI at the same image coordinates and keep the copy buffer for the next image.
- ROI `Ctrl+V` paste must force an immediate canvas repaint. The pasted box should be visible without waiting for a mouse move, zoom, or any other viewer update.
- ROI `Ctrl+C`/`Ctrl+V` must preserve the source ROI class and class color. A copied `OK` box must paste/save as `OK` even when the currently selected drawing label is `NG`.
- In box drawing mode, same-class ROI hits remain select/edit actions. If the active class differs from the clicked manual ROI class, the drag starts a new nested box so operators can label NG/foreign/defect regions inside a broad OK/background label.
- Dataset-purpose tool filtering is enforced for direct/programmatic tool selection as well as visible toolbar items. Hidden brush/eraser tools must not silently enter object-detection mode, and hidden box tools must not enter segmentation mode.

Do not change these paths casually:

- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/Canvas`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/Engine/ImageCanvasControl.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/Overlays/CanvasOverlaySpatialIndex.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/OpenGL`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/RoiInteraction/RoiInteractionMouseMove.cs`
- `0. UI/9) WPF/ViewModels/RoiImageCanvasViewModel.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationToolSelectionCommands.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.WorkflowTrainingGuideCommands.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ObjectReview*.cs`

Required gates before reporting a change complete:

```powershell
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --roi-500k-mouse-event-performance
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --texture-pan-performance
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --roi-500k-delete-performance
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --roi-500k-render
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --roi-geometry
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-roi-object-verification
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-annotation-purpose-scope
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --exe-purpose-scope-smoke --seed 260627 --brush-strokes 2
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
```

Latest evidence:

```text
ROI_500K_MOUSE_EVENT_MOVE_1000_MS=32.880 DISPLAY_REBUILDS_DURING_MOVE=0 EDIT_CALLBACKS_DURING_MOVE=0 DISPLAY_REBUILDS_TOTAL=2 EDIT_CALLBACKS_TOTAL=1
ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=9.573 DISPLAY_REBUILDS_DURING_RESIZE=0 EDIT_CALLBACKS_DURING_RESIZE=0 DISPLAY_REBUILDS_RESIZE_TOTAL=2 EDIT_CALLBACKS_RESIZE_TOTAL=1
ROI_500K_MOUSE_EVENT_MOVE_1000_MS=42.993 DISPLAY_REBUILDS_DURING_MOVE=0 EDIT_CALLBACKS_DURING_MOVE=0 DISPLAY_REBUILDS_TOTAL=2 EDIT_CALLBACKS_TOTAL=1
ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=9.119 DISPLAY_REBUILDS_DURING_RESIZE=0 EDIT_CALLBACKS_DURING_RESIZE=0 DISPLAY_REBUILDS_RESIZE_TOTAL=2 EDIT_CALLBACKS_RESIZE_TOTAL=1
ROI_500K_INDEXED_LOAD_MS=3456.5 ROI_500K_CANDIDATES=325 ROI_500K_QUERY_MS=1.331 ROI_500K_HIT_MS=2.009
ROI_OVERLAP_LOAD_MS=6102.0 ROI_OVERLAP_FIRST_HIT_MS=11.218 ROI_OVERLAP_REPEAT_HIT_MS=6.909 OBJECTS=50000 SELECTED_SIZE=5.0
WPF ROI object manipulation verification matrix passes.
WPF ROI object manipulation verification matrix covers cross-image Ctrl+C/Ctrl+V paste at the same image coordinates with the copy buffer retained.
WPF ROI object manipulation verification matrix covers immediate paste repaint and different-class nested box drawing over an existing broad ROI.
WPF ROI copy/paste preserves source class: OK source ROI pasted while active label was NG still saved and rendered as OK.
ROI_500K_RENDER_LOAD_MS=3224.4 ROI_500K_RENDER_CANDIDATES=10201 ROI_500K_RENDER_QUERY_MS=5.793 ROI_500K_RENDER_REBUILD_MS=5.876 VISIBLE_SHAPES=10000
EXE_PURPOSE_SCOPE_SMOKE seed=260627 brushStrokes=3 brushInputMs=653.3 outsideBoxMs=189.3 insideBoxMs=296.4 hidden=True restored=True outsideCreated=False insideCreated=True
Full LabelingApplication.Tests regression passed after the viewport query pre-sizing fix.
```

## Annotation Save and Reopen Contract

Status: stable for rectangle, ellipse, polygon, raster mask, candidate confirm/skip, and undo/redo in the WPF labeling-session verification.

Protected behavior:

- Object detection projects export box labels.
- Segmentation projects export polygon/mask labels.
- Object-detection mode hides stale segmentation annotations instead of showing masks from a previous purpose.
- Undo/redo restores in-memory WPF edit state and waits for an explicit save before rewriting label files.
- The canvas-local "라벨 저장" action must reuse the same shell save command as the top toolbar, become enabled when annotations are dirty, and return to "저장 완료" after persistence succeeds.

Required gates before reporting a change complete:

```powershell
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-session-smoke
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-annotation-object-verification
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-segmentation-object-verification
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-workflow-context
```

## Object Detection Queue Reopen and Real EXE Labeling Flow

Status: stable for the industrial object-detection EXE workflow as of 2026-06-27.

Protected behavior:

- Queue Open must not depend only on `DataGrid.SelectedItem`; it also checks the ViewModel-selected row, a unique search match, and a single visible filtered row.
- The image queue panel must show the currently loaded image-folder path and expose an Open Folder command that opens that same loaded folder without changing the queue root.
- After saving a label, a queue row may still point to the original staging image path while the saved image copy lives under `data/train|valid|test/images`. Reopen must recover that saved split image instead of reporting "select an image".
- Queue search in real-EXE automation should use the same keyboard/paste input route as a user, not only UIAutomation `ValuePattern.SetValue`.
- The object-detection real-use smoke must verify draw, save, reopen, empty-normal completion, artifact existence, duplicate-stem absence, and dataset readiness in one run.
- The main shell header must always show the current dataset name, purpose, output folder, and image folder, with command-bound actions to select an existing dataset, open the dataset folder, and change the image folder.
- The dataset-selection action must open an existing-dataset selector first. Do not send "change dataset" directly into the creation wizard; creation stays behind the selector's separate "new dataset" command.
- Dataset manifests store the selected image folder, and the dataset selector shows that image folder. When reopening a dataset, the operator-selected image folder from the recipe must win over dataset-owned train/valid/test copies; generated dataset folders are fallback only.
- App startup must restore the explicitly last-opened dataset recipe from the recent-state file. Do not infer "last opened" from manifest or folder timestamps because saving labels can update datasets that the operator did not most recently open.

Do not change these paths casually:

- `0. UI/9) WPF/Services/WpfImageQueueSelectionService.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueue*.cs`
- `0. UI/9) WPF/ViewModels/WpfImageQueuePanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfImageQueuePanel.xaml`
- `Yolo/YoloAnnotationService.cs`
- `Yolo/YoloDatasetSplitService.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\MvcVisionSystem.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-image-queue-status
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-startup-dataset-restore
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab guide
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260627 --label-count 10 --empty-completion-count 2
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
```

Latest evidence:

```text
EXE_INDUSTRIAL_OBJECT_LABELING_SMOKE seed=260627 imagesCopied=317 imagesLabeled=10 emptyCompletion=True emptyCompletionTarget=2 emptyLabelFilesSaved=2 imageFilesAfterSave=317 labelFilesAfterSave=12 labelFilesSaved=10 duplicateImageStems=0 selectedMissingImages=0 selectedMissingLabels=0 boxAvgMs=219.6 boxMaxMs=262.5 reopenVerified=True datasetCheck=True datasetCheckMs=3591.9
Full LabelingApplication.Tests regression passed after saved-split queue reopen fallback.
```

## Image Queue and Right Workflow Context UX

Status: stable after the 1920x1080/1366x768 WPF layout check on 2026-07-02.

Protected behavior:

- The image queue control block must not use fixed secondary row heights that clip wrapped buttons such as `전체 자동 저장`, selected-image inspection, retry, or stop.
- The primary queue action row keeps enough height for 32px icon buttons, and secondary rows auto-size to content.
- The image queue grid uses separate `저장` and `검사` columns. Do not relabel detection/review status as `AI` when row values are saved/queued/checking states.
- The expanded right workflow panel title follows the selected right-side view. In labeling stage, saved labels must show `저장 라벨`, guide/current-work must show `현재 작업`, and class management must show `클래스`; it must not keep the broader `데이터셋 홈` title.
- The collapsed right workflow rail must keep a compact context badge that identifies the area as the current work panel and shows the active local view (`라벨`, `작업`, `클래스`). Do not leave the collapsed rail as only anonymous buttons.
- The expanded labeling-stage right workflow panel must keep a local task switcher titled `라벨링 작업 패널`, with saved-label, guide/tool, and class buttons bound to the existing shell commands. Do not hide these task switches in the header-only chrome.
- The right workflow title state belongs in `WpfLabelingShellViewModel`; the view only binds to that state.

Do not change these paths casually:

- `0. UI/9) WPF/Views/WpfImageQueuePanel.xaml`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml`
- `0. UI/9) WPF/ViewModels/WpfLabelingShellViewModel.cs`
- `tests/LabelingApplication.Tests/Program.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-labeling-shell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-image-queue-status
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-responsive-layout --width 1920 --height 1080
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-responsive-layout --width 1366 --height 768
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-image-queue-right-panel-after-1920-expanded.png
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-right-rail-context-after-1920.png
```

Latest evidence:

```text
dotnet build tests/LabelingApplication.Tests passed with isolated OutDir: warnings=0 errors=0.
PASS WPF labeling shell can be constructed without the WinForms shell
PASS WPF image queue presents row status with icons
PASS MVVM infrastructure observable and command helpers
WPF responsive layout passed: 1920x1080, tabs=objects,candidates,guide,classes,yolo,training
WPF responsive layout passed: 1366x768, tabs=objects,candidates,guide,classes,yolo,training
WPF visual smoke captured: artifacts/ui/wpf-image-queue-right-panel-after-1920-expanded.png
WPF visual smoke captured: artifacts/ui/wpf-right-rail-context-after-1920.png
WPF visual smoke captured: artifacts/ui/wpf-right-rail-context-after-1366.png
Right workflow local task switcher verified with isolated build, --wpf-labeling-shell, --wpf-image-queue-status, --mvvm-infra, 1920x1080/1366x768 responsive layout, and screenshots artifacts/ui/wpf-right-workflow-local-switcher-after-1920.png + artifacts/ui/wpf-right-workflow-local-switcher-after-1366.png.
```

## Class Catalog and Active Label Selection Contract

Status: stable for object-detection labeling UX as of 2026-06-28.

Protected behavior:

- The Class tab is for class management: add, rename, delete, color, and output path.
- The canvas toolbar shows the active class chips used for the next drawn annotation.
- The class-management panel must explicitly show the current drawing class and explain that a newly drawn box uses that selected class. Do not leave this meaning only in the class list selection.
- Selecting `OK`, `Defect`, or another class on the canvas must update the class catalog selection because ROI creation reads `GetSelectedClassName()`.
- Selecting a class in the Class tab must update the canvas chip, so the visible drawing context and saved label stay aligned.
- If the selected class is removed, the remaining first class is selected instead of leaving drawing in an empty-label state.
- `Defect` is not special. It can be renamed, recolored, or deleted as long as at least one class remains.

Do not change these paths casually:

- `0. UI/9) WPF/ViewModels/WpfCanvasPanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfCanvasPanel.xaml`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ClassCatalog.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.Canvas.cs`
- `0. UI/9) WPF/ViewModels/WpfClassCatalogPanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfClassCatalogPanel.xaml`
- `Yolo/ClassCatalogService.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-canvas-workflow-context
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab guide
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
```

Latest evidence:

```text
WPF canvas workflow context follows active labeling state: PASS
WPF canvas panel declares viewer commands: PASS
WPF visual smoke captured canvas toolbar active-label chip.
Full LabelingApplication.Tests regression passed after active-label canvas chip sync.
```

## YOLOv5 Runtime and Dataset Contract

Status: stable for the local YOLOv5 smoke path as of 2026-06-26.

Protected behavior:

- The active operational model remains `C:\Git\yolov5\best.pt` until a measured comparison proves a new model is better.
- App-generated `data.yaml` must be UTF-8 without BOM so YOLOv5 reads the first key correctly.
- `data.yaml` train/val/test paths must match the current project output paths before training starts.
- `data.yaml` class count and class names must match the current project class catalog.
- Real Python worker inference through TCP must keep returning candidates that the C# workflow can render, confirm, and save.
- A short real `train.py` run is only a pipeline smoke unless train/valid/test are actually separated and enough NG examples exist.
- Training-result comparison reads YOLO `results.csv`, shows metric deltas, exposes a simple verdict (`latest better`, `current better`, or tie), and renders a structured Guide report before the operator applies `best.pt`.
- The Guide separates "training ready" from "model replacement ready"; an empty test split can still allow training, but must keep `best.pt` replacement on hold.
- The Guide model-comparison button must follow the latest dataset readiness report: disabled before dataset check, disabled for blocking readiness errors, disabled when the held-out test split is empty, and enabled only when training readiness passes with at least one test image.
- The Guide model-comparison basis sentence must stay visible beside the button. It should show the final-verification label count, recommended count, and whether replacement evidence is weak or ready before the operator clicks `모델 비교`.
- A model-replacement comparison must use a physically separated held-out test split. Public sample data can verify the comparison path, but industrial OK/NG model adoption still requires industrial OK/NG test labels.
- Industrial Kolektor preparation must not copy `*_label.bmp` files as labeling images. Those files are label masks; when `-CreateYoloLabelsFromKolektorMasks` is used, they become 1-class `Defect` YOLO boxes and normal images become empty txt labels.

Do not change these paths casually:

- `Yolo/CYolov5.cs`
- `Yolo/YoloDatasetValidator.cs`
- `Yolo/YoloDatasetReadinessService.cs`
- `1. Core/YoloTrainingWorkflowService.cs`
- `3. Communication/TCP/LearningProtocol.cs`
- `3. Communication/TCP/PythonModelStatusProtocol.cs`
- `scripts/smoke-yolo-tcp.ps1`
- `scripts/compare-yolo-models.ps1`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.WorkflowCommandStateFanout.cs`
- `0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-learning-workflow-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-yolo-training-session-smoke
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --real-yolo-smoke
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3
```

Reference record:

- `docs/YOLOV5_REAL_WORKFLOW_VERIFICATION_20260626.md`

## Object Detection Candidate Review Completion

Status: stable for the WPF object-detection review loop as of 2026-06-26.

Protected behavior:

- Candidate Review exposes `이미지 완료` as a ViewModel command, not a code-behind click handler.
- The action is enabled only when the active image is loaded, detection is idle, and no AI candidates remain pending.
- If labels exist, completion saves the current YOLO annotations and marks the image confirmed.
- If no labels exist, completion saves an empty YOLO label file and marks the image as `NoCandidate`; reviewed normal images must not reappear in the next-unlabeled queue.
- `Confirmed`, `Skipped`, and `NoCandidate` review states are treated as reviewed queue states even when `IsLabeled` is false.
- `--wpf-labeling-session-smoke` covers skip, confirm, labeled-image completion, next-image navigation, and empty-image completion.

Do not change these paths casually:

- `0. UI/9) WPF/Views/WpfCandidateReviewPanel.xaml`
- `0. UI/9) WPF/ViewModels/WpfCandidateReviewPanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.CandidateReview*.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationPersistence.cs`
- `Yolo/YoloImageReviewStatusService.cs`

Required gates before reporting a change complete:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
```

## Beginner Guide YOLO Completion Chips

Status: stable for the WPF Guide tab and real-EXE click path as of 2026-06-26.

Protected behavior:

- The Guide tab first-visible YOLO area shows compact completion chips for image loading, class registration, box labeling, dataset check, training, and inference review.
- The first-visible guide caption uses learner-facing wording: `다음 작업` and `완료 체크`. Do not revert it to `YOLO 다음 액션` or `완주 체크`.
- Each chip routes to `YoloTrainingWorkflowStepCommand` with the clicked workflow step as the command parameter.
- The chip and chip title expose per-step AutomationIds so real-EXE UIAutomation can click a concrete step, not only inspect static XAML.
- Clicking chips 2-6 in the real EXE reaches the expected class catalog, box tool, dataset checklist, training settings, and candidate-review targets through the same command path used by the full YOLO workflow rows.
- Chip 1 opens an OS folder picker and should be verified in the dataset/project setup flow rather than in the dialog-free guide-chip smoke.

Do not change these paths casually:

- `0. UI/9) WPF/Views/WpfLearningWorkflowPanel.xaml`
- `0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.WorkflowTrainingGuideCommands.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.LearningWorkflow.cs`

Required gates before reporting a change complete:

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-learning-workflow-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-guide-workflow-chip-smoke
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab guide
```

## Guide Dataset Status Dashboard

Status: stable for the WPF Guide tab readiness summary and dialog-free real-EXE dashboard shortcuts as of 2026-06-26.

Protected behavior:

- The dashboard uses the same `YoloDatasetReadinessReport` as the YOLO training checklist, so the compact cards and detailed checklist cannot disagree.
- The first-visible Guide area shows image, split, box/segment label, label-file, class, and duplicate-content metric cards.
- The dashboard also shows a labeling progress card (`completed label files / total images`) so operators can see how far the dataset is from being train-ready without doing mental math.
- Ready datasets can still show non-blocking quality warnings, such as an empty test split, in the issue list.
- Empty test split also appears as a model-replacement warning card; do not collapse this back into a generic train-ready message.
- Blocking duplicate train/valid/test image content is marked as a problem metric and as a plain operator action.
- The dashboard metric cards are real `Button.Command` surfaces, not passive borders with mouse event handlers.
- The dashboard exposes action-based AutomationIds for visual smoke and real-EXE checks.
- Dialog-free dashboard shortcuts route by `WpfDatasetDashboardActionKind`, not by localized card title text.
- Clicking class, label-tool, and dataset-settings cards in the real EXE reaches the class catalog, box labeling tool, and training/settings target.
- The image-folder card can open an OS folder picker and belongs in dataset setup/manual-dialog verification, not in dialog-free dashboard smoke.
- The compact Guide layout keeps the YOLO completion chips clickable while the dataset dashboard remains visible in the same first-visible action area.
- The dashboard primary action chip is shown before metric cards so beginners see the immediate object-detection next step before interpreting diagnostics. Empty action text collapses the chip rather than leaving a blank guide element.

Do not change these paths casually:

- `0. UI/9) WPF/Views/WpfLearningWorkflowPanel.xaml`
- `0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.TrainingGuideStatus.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.LearningWorkflow.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.WorkflowTrainingGuideCommands.cs`

Required gates before reporting a change complete:

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-learning-workflow-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab guide
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-guide-workflow-chip-smoke
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-guide-dashboard-card-smoke
```

Latest real-EXE evidence:

```text
2026-06-28: EXE_GUIDE_DASHBOARD_CARD_SMOKE cards=OpenClassCatalog,OpenLabelingTool,OpenDatasetSettings totalClickVerifyMs=4820.7 maxClickVerifyMs=1992.5 cardsFound=3 cardsVerified=3 status="OpenClassCatalog:class-catalog | OpenLabelingTool:라벨링: 필요한 라벨 도구로 이동했습니다. | OpenDatasetSettings:training-settings"
```

Latest focused evidence:

```text
2026-06-28: dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false passed with 0 warnings and 0 errors.
2026-06-28: --wpf-learning-workflow-panel, --wpf-canvas-workflow-context, --wpf-image-queue-status, and --wpf-visual-smoke --review-tab guide passed after moving the dataset dashboard action above the metric cards.
2026-06-28: first-visible guide caption wording verified as `다음 작업` and `완료 체크`; old `YOLO 다음 액션` / `완주 체크` wording is protected against reintroduction.
```

## Object Detection Real-EXE Labeling Loop

Status: stable for the current object-detection box labeling loop as of 2026-06-27.

MVP scope and completion criteria: `docs/OBJECT_DETECTION_MVP_COMPLETION.md`

Protected behavior:

- In object-detection purpose, the visible toolset focuses on selection, box drawing, and movement; segmentation paint tools are not part of the normal box-labeling path.
- Multiple boxes can be drawn at random image-safe positions in the real EXE.
- Drawing boxes creates Object Review rows without requiring a full project reload.
- Clicking a drawn ROI keeps selection state long enough for the delete command to become available.
- When manual ROI boxes overlap, a body click still selects the smallest containing concrete ROI, but an outline/handle click must select the outlined ROI the operator targeted. The OpenGL spatial index keeps a capped edge-hit index for this; do not replace it with an all-object scan.
- Deleting one selected ROI must not block immediate wheel/UIAutomation responsiveness.
- Manual label rectangles keep the semantic class color, but the viewer adds a non-geometric dark halo and light fill tint so green box labels stay readable on real grayscale/industrial images.
- COCO128 dataset setup creates the recipe manifest, `VISION.xml`, `data.yaml`, train image folder, and train label folder.
- Saved YOLO boxes load into Object Review immediately after opening the generated sample project.
- Existing loaded box labels are deleted through the user-visible object row selection path, then saved.
- A new box drawn on an empty-label image is saved and still visible after reopening that image.
- The industrial `Kolektor 산업 이미지(박스)` object-detection preset copies the locally prepared Kolektor images and starts a real box-labeling workflow with class `Defect`.
- Real-EXE industrial object labeling verifies 10 images with random valid box input, save, split-aware YOLO label-file detection across `train|valid|test`, reopen visibility, and dataset-check completion.
- The status bar keeps the active image file visible while queue detail loading updates the summary; EXE smokes use this to avoid mistaking a stale selected row for the active canvas image.
- YOLO box save preserves the active source image extension when the app loaded a concrete image path, removes stale same-stem sibling image copies such as `.jpg` plus `.jpeg`, and keeps a same-stem image/label pair in only the selected split.
- The image queue exposes the next-image action as a visible `다음` icon+text button, not only as an icon, so beginner object-labeling sessions can move forward without hunting through tooltips.
- After labels are saved, the always-visible canvas workflow strip points operators to the left queue `다음` button with natural `이어서 작업` wording. Do not use awkward phrases such as `라벨 없는 다음 이미지` in operator-facing guidance.
- The canvas workflow strip and helper buttons should use beginner-visible terms such as `맞춤`, `이동`, `후보`, `후보 지움`, `검토`, `저장 필요`, `미완료`, and `이어서 작업`. The 2026-07-03 inference wording pass supersedes the older "no AI 후보" wording: pending detection results should now be called `AI 후보` when the UI must distinguish them from saved labels. Do not reintroduce `Fit`, `Pan`, `Focus`, `AI Reset`, `미라벨`, or `라벨 없는 다음 이미지` in operator-facing guidance.
- User-facing tool labels, tooltips, object-review rows, status text, and log text must use labeling terms such as `박스`, `폴리곤`, `마스크`, `되돌리기`, and `다시 적용`. Do not expose implementation terms such as OpenGL, FBO, GPU, CPU, ROI, raster mask, or bounding box in operator-facing text.
- Candidate Review, model settings, training settings, dataset dashboard, and status/log text must use beginner-facing model terms such as `겹침`, `추론 실행기`, `모델 파일`, `학습 설정`, `최종 검증`, `기존 모델`, and `새 모델`. Do not expose `IoU`, `Python worker`, `best.pt`, `data.yaml`, `test split`, `baseline`, or `candidate` in operator-facing text unless the view is explicitly showing a file path or developer diagnostic.
- Empty YOLO label files are shown as reviewed no-object completion, currently `객체 없음 완료` in summaries and `객체없음` in compact badges/filters, not as ambiguous `없음`.
- The no-candidate quick filter is labeled `객체없음`, not `없음`, so the queue does not confuse reviewed normal images with unknown/unlabeled images.
- Candidate Review exposes `이미지 완료` outside the candidate comparison panel through `CompleteImageAndNextButton`; the action stays visible when there are no AI candidates and saves an empty label file before moving to the next unfinished image or dataset check.
- The top status bar exposes `단계`, `진행`, and `다음` fields. Keep these beginner-facing and cheap to update; do not calculate them from MouseMove paths.
- Empty project first-run guidance starts at `단계: 데이터셋 준비` and `다음: 데이터셋 시작`; the Guide dataset setup card should show the selected purpose's first action before deeper tutorial content.
- The canvas `검출 결과` HUD mirrors Candidate Review actions (`이전`, `후보`, `기준`, `다음`, `확정`, `스킵`) in a compact top-left overlay. It must not stretch across the canvas or render the long candidate list over the image; long detail stays available through the tooltip and the right Candidate Review panel.
- Candidate Review, canvas helper buttons, guide text, status, and logs should use `AI 후보` when naming unsaved inference results, while compact action labels may still use `후보`, `이동`, and `후보 지움`. Do not reintroduce `검출 후보` as a separate user-facing state, or implementation terms such as `Pan`, `Focus`, or `AI Reset`.
- Candidate Review selected-candidate summary must show the selected AI candidate, any overlapping current label, and the recommended action before the confirm/skip buttons.
- Canvas detection overlay labels use compact AI labels such as `AI 1 OK` to avoid long Korean overlays on the image; the surrounding panels must still explain that these are unsaved `AI 후보`.
- When an AI candidate overlaps a manual ROI, a canvas click selects the AI candidate first. The `기준`/current-label action is the explicit path for selecting the underlying manual ROI during review.
- When AI candidate boxes overlap each other, the canvas click selects the smallest containing candidate first; selected state and draw order are tie-breakers only.
- The canvas detection-result card floats inside the canvas row and must not take a separate layout row that reduces the image inspection area.
- Candidate Review model-difference examples are ViewModel-owned and fed from `WpfModelComparisonReviewService`; do not rebuild baseline-vs-candidate text directly in shell code-behind.
- Model-difference examples come from saved YOLO comparison label txt outputs and currently classify `CandidateOnly`, `BaselineOnly`, and `ClassChanged` using normalized-box IoU.
- Model-difference examples resolve their source image path from comparison `dataYaml` + `task`, and the row click opens the image through `ModelComparisonExampleCommand` rather than a view event handler.
- The Guide `모델 비교` button runs the comparison through `WpfModelComparisonRunService`, not through inline shell process-building code. It uses `compare-yolo-models.ps1 -Task test` and refreshes Candidate Review after completion.
- `WpfModelComparisonRunService` validates the requested `data.yaml` split before launching Python. Empty `test`/`val` image folders and identical baseline/candidate `best.pt` paths are blocked early with operator-readable status text.
- The Guide training-result card owns the visible `교체 판단:` conclusion in the ViewModel. Keep this as a beginner-facing decision sentence: latest model wins only as a candidate until final verification examples are inspected; missing metrics or failed comparison must stay on hold.
- Model comparison can run with at least one final-verification image, but model replacement evidence is intentionally marked weak below 10 final-verification images. Keep the `근거 부족`/`주의` signal separate from command availability so users can smoke-test the pipeline without mistaking it for production model-adoption proof.
- Candidate Review model-difference examples must use learner-facing `모델 차이 예시` wording and each example must include a visible `확인:` action hint. New-model-only examples should tell the user to check false positives, baseline-only examples should warn about replacement hold/missed objects, and class-changed examples should point back to the label rule.
- Clicking a Candidate Review model-difference example must open the source image, draw one selected difference overlay, focus the viewer on that box, and update the review panel/result card with beginner-facing position text. Do not reduce the image inspection area or require the operator to find the artifact image manually.
- Candidate Review model-difference examples must live in a compact vertical scroll area. Multiple examples should remain accessible without clipping rows or expanding the canvas/image inspection area.
- After model comparison, Candidate Review must show an explicit next-action sentence: click a model-difference example, confirm the image location, then return to the Guide replacement decision. Model-comparison run/complete/failure status strings must be readable Korean, not mojibake.
- 2026-06-27 verified run: `compare-yolo-models.ps1 -Task test` completed against `artifacts/yolo-model-comparison/test-routing-data.yaml`, which maps `test` to the existing valid split only to verify the test-mode execution path. Do not treat that temporary routing run as true model-adoption evidence.
- Object Review exposes stable AutomationIds for summary, object list, class selector, delete, and apply actions so real-EXE UX checks can verify selection/edit/delete state without depending on localized layout text.
- Object Review must keep the selected-label task summary before the editable list: current image summary, selected label detail, class/delete actions, then the object list. The selected-label summary is ViewModel-derived state, not code-behind text assembly.
- The long random real-EXE industrial loop is verified with 30 random box-labeled images plus 5 empty normal completions; expect this smoke to need a long timeout because it drives the visible EXE end to end. This path is a protected MVP gate, so do not refactor queue open/save/reopen behavior without rerunning the long smoke.

Do not change these paths casually:

- `0. UI/9) WPF/Views/WpfLabelingShellWindow.Annotation*.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ObjectReview*.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueue*.cs`
- `0. UI/9) WPF/Views/WpfCandidateReviewPanel.xaml`
- `0. UI/9) WPF/Views/WpfStatusBarPanel.xaml`
- `0. UI/9) WPF/ViewModels/WpfObjectReviewPanelViewModel.cs`
- `0. UI/9) WPF/Services/WpfImageQueuePresenter.cs`
- `0. UI/9) WPF/Services/WpfModelComparisonRunService.cs`
- `0. UI/9) WPF/Services/WpfModelComparisonReviewService.cs`
- `0. UI/9) WPF/Services/WpfObjectReview*.cs`
- `0. UI/9) WPF/Services/WpfDatasetSamplePresetService.cs`
- `0. UI/9) WPF/Services/WpfImageQueueFilterService.cs`
- `0. UI/9) WPF/Views/WpfDatasetSetupWizardWindow.xaml`
- `Yolo/YoloAnnotationService.cs`

Required gates before reporting a change complete:

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-drawing-preview-performance
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-roi-object-verification
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-500k-hit-test
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-overlap-hit-test
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-500k-mouse-event-performance
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-500k-delete-performance
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-roi-tools-smoke --seed 260626 --box-count 12
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-dataset-wizard-smoke
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --yolo-annotation-storage
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-image-queue-status
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-candidate-review-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-canvas-detection-overlay
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --detection-500k-hit-test
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-object-review-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-canvas-workflow-context
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab guide
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260626 --label-count 30
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260626 --label-count 3 --empty-completion
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260626 --label-count 12 --empty-completion-count 3
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260626 --label-count 30 --empty-completion-count 5
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
```

Latest long-loop evidence:

```text
EXE_INDUSTRIAL_OBJECT_LABELING_SMOKE recipe=codex_exe_industrial_object_3d40d09f5b5441338d7751b447fc566d imagesCopied=317 imagesLabeled=30 emptyCompletion=True emptyCompletionTarget=5 emptyLabelFilesSaved=5 imageFilesAfterSave=317 labelFilesAfterSave=35 labelFilesSaved=30 duplicateImageStems=0 selectedMissingImages=0 selectedMissingLabels=0 boxInputMs=7102.9 boxAvgMs=236.8 boxMaxMs=292.6 reopenVerified=True datasetCheck=True datasetCheckMs=4205.1
```

Latest real-EXE evidence:

```text
ROI_500K_INDEXED_LOAD_MS=3947.6 ROI_500K_CANDIDATES=325 ROI_500K_QUERY_MS=1.559 ROI_500K_HIT_MS=2.127
ROI_OVERLAP_LOAD_MS=6716.2 ROI_OVERLAP_FIRST_HIT_MS=16.105 ROI_OVERLAP_REPEAT_HIT_MS=7.559 OBJECTS=50000 SELECTED_SIZE=5.0
ROI_500K_MOUSE_EVENT_MOVE_1000_MS=18.503 DISPLAY_REBUILDS_DURING_MOVE=0 EDIT_CALLBACKS_DURING_MOVE=0 DISPLAY_REBUILDS_TOTAL=2 EDIT_CALLBACKS_TOTAL=1
ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=10.266 DISPLAY_REBUILDS_DURING_RESIZE=0 EDIT_CALLBACKS_DURING_RESIZE=0 DISPLAY_REBUILDS_TOTAL=2 EDIT_CALLBACKS_TOTAL=1
ROI_500K_SINGLE_DELETE_MS=10.432 ROI_500K_DELETE_LOAD_MS=4285.4 VISIBLE_CACHE_DIRTY_AFTER_DELETE=False VISIBLE_CACHE_DIRTY_AFTER_ZOOM=True VIEWPORT_REFRESH_PENDING_AFTER_ZOOM=True VISIBLE_SHAPES=0 DELETE_THEN_ZOOM_MS=3.279 OBJECTS=499999
WPF_OBJECT_REVIEW_DELETE_MS=9.502 WPF_OBJECT_REVIEW_DELETE_THEN_ZOOM_MS=1.440 DEFERRED_AFTER_DELETE=True DEFERRED_AFTER_ZOOM=False VIEWPORT_REFRESH_PENDING_AFTER_ZOOM=True VISIBLE_CACHE_DIRTY_AFTER_ZOOM=True REMAINING_ROIS=0 REMAINING_OVERLAYS=0 SELECTED_EMPTY=True
EXE_ROI_TOOLS_SMOKE seed=260628 boxes=5 boxInputMs=951.4 boxAvgMs=190.3 boxMaxMs=210.0 selectToDeleteEnabledMs=788.5 deleteThenWheelUiMs=16.3 boxSelected=True rowVisible=True deleteEnabled=True remaining=True
WPF canvas detection overlay compact HUD: `--wpf-canvas-detection-overlay`, `--wpf-candidate-review-panel`, and `--wpf-visual-smoke --review-tab candidates` passed; screenshot captured at `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`.
ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS=14.256 ADDED=0 PREVIEW_EMPTY=False
WPF_OBJECT_REVIEW_DELETE_MS=10.960 WPF_OBJECT_REVIEW_DELETE_THEN_ZOOM_MS=1.710 DEFERRED_AFTER_DELETE=True DEFERRED_AFTER_ZOOM=False VIEWPORT_REFRESH_PENDING_AFTER_ZOOM=True VISIBLE_CACHE_DIRTY_AFTER_ZOOM=True REMAINING_ROIS=0 REMAINING_OVERLAYS=0 SELECTED_EMPTY=True
EXE_ROI_TOOLS_SMOKE seed=260628 boxes=5 boxInputMs=982.7 boxAvgMs=196.5 boxMaxMs=212.2 selectToDeleteEnabledMs=955.4 deleteThenWheelUiMs=20.5 boxSelected=True rowVisible=True deleteEnabled=True remaining=True
DETECTION_500K_INDEXED_LOAD_MS=173.5 DETECTION_500K_HIT_MS=0.003 DETECTION_500K_HIT_MAX_MS=0.047 HIT_INDEX=499999; overlapping candidate hit-test selected the smaller concrete candidate.
EXE_CANDIDATE_FOCUS_SMOKE recipe=codex_exe_candidate_focus_63f981d320374f748d0cd9a0068bd577 recipeApplied=True sampleLoaded=True roiCreated=True candidateVisible=True focusClicked=True objectSelected=True yoloSmokeMs=10006.3 focusClickMs=430.0
EXE_CANDIDATE_FOCUS_SMOKE recipe=codex_exe_candidate_focus_f83a47cdfe354913872893dec2c63f04 recipeApplied=True sampleLoaded=True roiCreated=True candidateVisible=True focusClicked=True objectSelected=True yoloSmokeMs=9692.8 focusClickMs=440.3
EXE_ROI_TOOLS_SMOKE seed=260626 boxes=12 boxInputMs=2542.9 boxAvgMs=211.9 boxMaxMs=255.9 selectToDeleteEnabledMs=858.9 deleteThenWheelUiMs=16.7 boxSelected=True rowVisible=True deleteEnabled=True remaining=True
EXE_DATASET_WIZARD_SMOKE recipe=codex_exe_dataset_wizard_774990ea89d547538e32e55484a75bd4
EXE_INDUSTRIAL_OBJECT_LABELING_SMOKE recipe=codex_exe_industrial_object_d4ffd819c9c24053a7f8837020ef8b79 imagesCopied=317 imagesLabeled=30 imageFilesAfterSave=317 labelFilesAfterSave=30 labelFilesSaved=30 duplicateImageStems=0 selectedMissingImages=0 selectedMissingLabels=0 boxInputMs=7079.0 boxAvgMs=236.0 boxMaxMs=288.2 reopenVerified=True datasetCheck=True datasetCheckMs=3744.7
EXE_INDUSTRIAL_OBJECT_LABELING_SMOKE recipe=codex_exe_industrial_object_2c3af13e9c59447ab1b1bb3236bdbda9 imagesCopied=317 imagesLabeled=3 emptyCompletion=True emptyLabelFilesSaved=1 imageFilesAfterSave=317 labelFilesAfterSave=4 labelFilesSaved=3 duplicateImageStems=0 selectedMissingImages=0 selectedMissingLabels=0 boxInputMs=705.3 boxAvgMs=235.1 boxMaxMs=272.6 reopenVerified=True datasetCheck=True datasetCheckMs=2937.3
EXE_INDUSTRIAL_OBJECT_LABELING_SMOKE recipe=codex_exe_industrial_object_b2bd510a40a34a9183cf95632a05d7fd imagesCopied=317 imagesLabeled=12 emptyCompletion=True emptyCompletionTarget=3 emptyLabelFilesSaved=3 imageFilesAfterSave=317 labelFilesAfterSave=15 labelFilesSaved=12 duplicateImageStems=0 selectedMissingImages=0 selectedMissingLabels=0 boxInputMs=2870.4 boxAvgMs=239.2 boxMaxMs=286.6 reopenVerified=True datasetCheck=True datasetCheckMs=3492.5
```

Latest focused evidence:

```text
YOLO annotation storage policy is covered by --yolo-annotation-storage: source .jpg remains .jpg, stale .jpeg siblings are removed, and same-stem artifacts move from train to valid when split policy changes.
WPF image queue UX is covered by --wpf-image-queue-status, --wpf-canvas-workflow-context, and --wpf-visual-smoke --review-tab guide: the visible next-image button remains in the narrow left queue panel, and save completion points to that button.
2026-06-28 image queue current-folder UX is covered by `--wpf-image-queue-status`: loading a folder pushes the current folder path into `WpfImageQueuePanelViewModel`, the panel shows that path, and `OpenCurrentImageFolderCommand` is bound to the visible folder-open button.
2026-06-28 dataset image-folder restore is covered by the full `LabelingApplication.Tests` regression and `--wpf-image-queue-status`: `dataset.manifest.json` includes `imageRootPath`, the selector shows it with a VISION.xml fallback for older recipes, and dataset reopen prefers the saved operator image folder over generated train/valid/test image copies.
2026-07-02 image queue current-task summary contract: the left queue must show a compact selected-image task card above the filters so users can see whether the current image needs labeling, AI-candidate review, failed-inspection handling, or is already saved. The text and state key are computed by `WpfImageQueuePanelViewModel` from `SelectedQueueItem` and selected-row property changes; XAML should remain binding-only and must not move this state into panel code-behind. The unlabeled/needs-label task detail must use generic label wording that works for box, polygon, and brush workflows; do not restore box-only wording in this shared queue card. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-canvas-panel-commands`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-current-task-clarity-after-1920.png`, `artifacts\ui\wpf-current-task-clarity-after-1366.png`, and `artifacts\ui\wpf-image-queue-current-task-generic-label-after-1920.png`.
Candidate empty-completion UX is covered by --wpf-candidate-review-panel, --wpf-image-queue-status, --wpf-object-review-panel, and --exe-industrial-object-labeling-smoke --empty-completion-count: no-candidate images keep a visible finish-and-next action, save zero-object YOLO labels, move to the next unfinished image, and show as `객체 없음 완료`/`객체없음` instead of an unknown state.
2026-06-28 user-facing terminology cleanup: `--wpf-learning-workflow-panel`, `--wpf-object-review-panel`, `--wpf-segmentation-object-verification`, `--wpf-labeling-session-smoke`, and `--wpf-visual-smoke --review-tab guide` passed after replacing OpenGL/ROI/raster/bounding-box wording with labeling terms.
2026-06-28 expanded user-facing terminology cleanup: Candidate Review overlap text, model/training settings, dataset dashboard, inference status/logs, model comparison cards, and quality warnings now use beginner labeling terms. `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, focused WPF smokes, and the full default `LabelingApplication.Tests` regression passed.
2026-06-28 documentation guard update: `WpfTrainingSettingsPanel.xaml` no longer keeps mojibake legacy localization-probe text, and `docs\LABELING_PROGRAM_DIRECTION.md` records beginner-facing terminology as protected product behavior. Future UI work should keep visible labels in labeling terms and leave implementation terms in diagnostics, code, or developer documentation only.
2026-06-28 candidate-review wording cleanup was later superseded by the 2026-07-03 AI-candidate wording pass. Current WPF/test scope should use `AI 후보` for unsaved inference results, while compact buttons may still use `이동`, `후보`, and `후보 지움`. Do not restore `검출 후보` as a separate user-facing state. Covered by `--wpf-candidate-review-panel`, `--wpf-canvas-workflow-context`, `--wpf-learning-workflow-panel`, `--wpf-canvas-panel-commands`, and 2026-07-03 visual smokes.
2026-06-28 canvas workflow wording cleanup: the top canvas fit action now says `맞춤`, the detection overlay title says `검출 결과`, review step text says `검토`, dirty-label guidance asks the operator to save the current image labels, and save-complete guidance points to the left queue `다음` button with natural `이어서 작업` wording. `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, `--wpf-canvas-workflow-context`, `--wpf-canvas-detection-overlay`, `--wpf-labeling-session-smoke`, `--wpf-visual-smoke --review-tab guide`, and `--wpf-visual-smoke --review-tab candidates` passed.
2026-06-28 completion-flow wording cleanup: the queue filter moved from `미라벨` to a completion-oriented unfinished concept, empty normal completions no longer remain in the unfinished filter, save completion no longer says `라벨 없는 다음 이미지`, and completing the last queue image refreshes dataset readiness so users can proceed to the next step. Verified with `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, `--wpf-canvas-workflow-context`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-learning-workflow-panel`, `--wpf-labeling-session-smoke`, `--wpf-canvas-detection-overlay`, `--wpf-visual-smoke --review-tab guide`, `--wpf-visual-smoke --review-tab candidates`, and `--exe-industrial-object-labeling-smoke --seed 260626 --label-count 3 --empty-completion`.

2026-07-03 image queue work-needed filter contract: the right-side image queue quick filters must expose `작업 필요` as the first filter. It is the operator's priority filter for unfinished labeling, AI candidate review, failed inspection, and label edits that still need save. `WpfImageQueueFilter.Unlabeled` is displayed as `작업 필요`; do not relabel it back to `미라벨` or hide it only in the combo box. A queue item with `IsSaveRequired == true` must not count as completed even if it already has saved labels. Covered by isolated build, `--wpf-image-queue-status`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive checks, and captures `artifacts\ui\wpf-image-queue-work-needed-filter-after-1920.png` plus `artifacts\ui\wpf-image-queue-work-needed-filter-after-1366.png`.
2026-06-28 top workflow status update: the top status bar now exposes current `단계`, completed/remaining `진행`, and the immediate `다음` action; Candidate Review visible completion text is `이미지 완료`. Verified with `--wpf-status-panels`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-canvas-workflow-context`, `--wpf-learning-workflow-panel`, `--wpf-labeling-session-smoke`, `--wpf-canvas-detection-overlay`, both `--wpf-visual-smoke` review tabs, and `--exe-industrial-object-labeling-smoke --seed 260626 --label-count 3 --empty-completion`.
2026-06-28 first-run dataset guidance: empty projects start at `단계: 데이터셋 준비` / `다음: 데이터셋 시작`, and the Guide panel shows a selected-purpose first action. The older `검출 후보` wording note is superseded; current guide/review text uses `AI 후보` when naming unsaved inference results. Verified with `--wpf-learning-workflow-panel`, `--wpf-status-panels`, `--wpf-visual-smoke --review-tab guide`, and later 2026-07-03 AI-candidate wording gates.
2026-06-28 overlap selection UX: Candidate Review selected-candidate summary states the selected AI candidate, the overlapping current label, and the recommended action (`기존 라벨 확인 후 같으면 스킵` or `맞으면 확정`). Verified with `--wpf-candidate-review-panel`, `--wpf-canvas-detection-overlay`, `--wpf-visual-smoke --review-tab candidates`, and `--wpf-labeling-session-smoke`.
2026-06-28 model adoption decision UX: the Guide training-result card now shows a direct `교체 판단:` sentence before detailed metrics. Verified with `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, `--wpf-learning-workflow-panel`, `--wpf-candidate-review-panel`, and `--wpf-visual-smoke --review-tab guide`.
2026-06-28 detection overlay label wording: canvas detection labels and result cards now use `후보`/`검출 결과` instead of `AI` wording. Verified with `--wpf-canvas-detection-overlay`, both visual smoke review tabs, and the full default `LabelingApplication.Tests` regression.
2026-06-28 model replacement evidence strength: Guide and dataset dashboard now show `근거 부족`/`주의` when final-verification images are below 10, while keeping model comparison enabled once at least one final-verification image exists. Verified with build, `--wpf-learning-workflow-panel`, `--priority-workflow-docs`, and the full default `LabelingApplication.Tests` regression.
2026-06-28 model comparison basis visibility: Guide training-result card now exposes `비교 기준` with held-out label count and recommendation threshold before running comparison. Verified with build, `--wpf-model-comparison-heldout`, `--wpf-learning-workflow-panel`, and `--wpf-visual-smoke --review-tab guide`.
2026-06-28 model-difference example actions: Candidate Review model-comparison examples now show Korean `모델 차이 예시` status and per-example `확인:` action hints for false positives, missed objects, and class changes. Verified with build, `--wpf-candidate-review-panel`, `--priority-workflow-docs`, and the full default `LabelingApplication.Tests` regression.
2026-06-28 model-difference click focus: Candidate Review model-comparison examples now carry source-image coordinates and click through to a selected canvas overlay/result-card focus state, so the disagreement location is visible immediately. Verified with build and the full default `LabelingApplication.Tests` regression.
2026-06-28 model-difference example list scrolling: Candidate Review model-comparison examples now sit inside a compact vertical scroll area, so six or more examples stay reachable without clipping rows or shrinking the image viewer. Verified with build, `--wpf-candidate-review-panel`, and `--wpf-visual-smoke --review-tab candidates`.
2026-06-28 model-comparison next-action guidance: Candidate Review now tells operators to click an example, inspect the image position, then return to the Guide replacement decision; model-comparison run status strings were normalized to readable Korean. Verified with build, `--wpf-learning-workflow-panel`, `--wpf-candidate-review-panel`, and visual smokes.
```

Canvas candidate-review UX is covered by `--detection-500k-hit-test`, `--wpf-canvas-detection-overlay`, `--wpf-roi-object-verification`, `--wpf-canvas-workflow-context`, `--wpf-candidate-review-panel`, `--wpf-visual-smoke`, and `--exe-candidate-focus-smoke`: overlapping AI candidates win canvas clicks over manual ROIs, overlapping AI boxes choose the smallest concrete candidate, and the canvas result card exposes review actions without reducing the image inspection row.

2026-06-26 Candidate Review verification snapshot:

```text
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
PASS: warnings 0, errors 0

dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-candidate-review-panel
PASS: Candidate Review navigation, focus commands, existing-label action wording, and ViewModel-bound command contracts.

dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-current-image-smoke-preserve-labels
PASS: same-image smoke inference preserves manual ROI state for duplicate/current-label focus.

dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab candidate --seed-duplicate
PASS: captured tests\artifacts\ui\wpf-detection-overlay-visual-check.png.

dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
PASS: full regression.

git diff --check
PASS: whitespace check passed; only existing LF-to-CRLF warnings were reported.
```

Latest full-regression performance samples:

```text
TEXTURE_PAN_1000_MOUSEMOVE_MS=6.469
BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=5.285
ROI_500K_SINGLE_DELETE_MS=3.537
DELETE_THEN_ZOOM_MS=0.670
WPF_OBJECT_REVIEW_DELETE_MS=6.043
WPF_OBJECT_REVIEW_DELETE_THEN_ZOOM_MS=0.892
```

Candidate comparison decision guidance is covered by `--wpf-candidate-review-panel`: duplicate candidates are marked as duplicate in the ViewModel and visible card text, while non-overlapping candidates are described as new candidates without shell code-behind text writes.
Candidate Review model-difference examples are covered by `--wpf-candidate-review-panel`, `--wpf-learning-workflow-panel`, and the full regression: the run service builds the app-visible `compare-yolo-models.ps1 -Task test` request, the review service parses baseline/candidate YOLO comparison label txt files, filters by comparison confidence, resolves source images from `data.yaml`, feeds ViewModel-bound example rows that open through a command, and focuses a one-box canvas overlay on the clicked disagreement location.
Candidate Review model-candidate decisions are covered by `--model-registry`, `--wpf-candidate-review-panel`, `--wpf-candidate-review-layout`, and `--wpf-labeling-shell`: the decision card routes save/reject through ViewModel commands, rejected candidates are persisted as `ModelCandidateDecision` records, and saved candidates remain recorded as inspection-model adoptions. Do not replace this with bottom-log-only messaging.
Model-center model history is covered by `--model-registry`, `--wpf-settings-viewmodels`, `--wpf-labeling-shell`, and `--wpf-yolo-training-session-smoke --model-center`: the registry summary must keep a recent model-history list showing profile/run context, metrics, and current/saved/rejected decision state. Do not collapse this back into a single latest-candidate text row.
Candidate current-label focus is covered by `--wpf-candidate-review-panel`: an overlapping AI candidate enables the Candidate Review `Label` action, and the actual bound `FocusCurrentLabelButton` command selects the matching Object Review row instead of requiring the operator to search manually.
Candidate Review action wording is intentionally split between `AI candidate` and `existing label`: the focus buttons read as candidate/existing-label actions, and duplicate guidance tells the operator to inspect the existing label before skipping the duplicate.
Candidate Review exposes `FocusCurrentLabelButton` as a stable automation id so later real-EXE UIAutomation checks can click the same `Label` action without relying on text lookup.
2026-06-28 Candidate Review button wording is protected by `--wpf-candidate-review-panel`: navigation and focus actions must remain explicit (`previous candidate`, `candidate location`, `existing label`, `next candidate`) and use stable automation ids. Do not shorten these back to bare nouns such as `candidate` or `existing`; that made overlapped-box review unclear for new users.
2026-06-28 overlapping ROI selection was rechecked with `--roi-overlap-hit-test` and `--wpf-roi-object-verification`: dense overlap selected the smallest concrete ROI at the clicked point, edge clicks selected the outlined ROI the operator targeted, candidate overlays won over underlying manual ROI clicks, and measured samples were first hit 16.702 ms, repeat hit 6.875 ms, object-review delete 12.747 ms, delete-then-zoom 2.823 ms.
Real EXE Candidate Review current-label focus is covered by `--exe-candidate-focus-smoke`: a temporary YOLO smoke client returns an overlapping AI candidate, `FocusCurrentLabelButton` is invoked by automation id, and Object Review selection becomes editable.
2026-06-28 latest real-EXE candidate focus sample: `--exe-candidate-focus-smoke` passed with recipeApplied=True, sampleLoaded=True, roiCreated=True, candidateVisible=True, focusClicked=True, objectSelected=True, yoloSmokeMs=10030.6, focusClickMs=418.6. Captured image: `artifacts/ui/exe-candidate-focus-smoke.png`.
Current-image inference must preserve existing manual labels. Worker and smoke detection now skip image reload when the result targets the already-active image, so Candidate Review can compare AI candidates with the operator's current ROI/mask state.
The same preservation rule is covered by `--wpf-current-image-smoke-preserve-labels`: WPF loads an image, creates a manual ROI, runs a temporary smoke client against that same image, and verifies the ROI remains available for high-overlap/current-label focus.
Canvas ROI selection by overlay id is covered by the full WPF ROI object manipulation regression: side-panel actions can light up the matching OpenGL ROI edit handles without rebuilding all overlays.
Model comparison label-list preflight is now protected in `scripts/compare-yolo-models.ps1`: the script reads the dataset label count and both weights files before launching YOLO validation, then stops early when the counts differ. 2026-06-28 measured checks: a 1-label dataset vs 2-label models stopped before `val.py`; the matching 2-label `val` smoke completed on 125 images at `artifacts/yolo-model-comparison/preflight-success-check/20260628-110826/comparison-summary.json`. Treat that 125-image run as pipeline evidence only, not model-replacement evidence.
Model comparison final-verification readiness is label-aware: the Guide button and `WpfModelComparisonRunService` require test images and matching answer label files before launching comparison. A test split with images but no labels must keep replacement on hold, and the 10-image recommendation is judged by labeled final-verification count.
2026-06-28 labeled test fixture run: `artifacts/yolo-model-comparison/20260628-labeled-test-fixture/data.yaml` copies 10 labeled images from `C:\Git\yolov5\data\valid` into a `test` split and verifies the app/script `-Task test` path. Output: `artifacts/yolo-model-comparison/labeled-test-fixture-run/20260628-135515/comparison-summary.json`. Baseline mAP50-95 `0.94`, candidate mAP50-95 `0.0265`; keep this as pipeline evidence only because the source images came from the existing valid split.
2026-06-28 true held-out comparison path: COCO128 was copied into `artifacts/yolo-model-comparison/coco128-true-heldout-20260628-151254` with physically separated `train` 96, `valid` 16, and `test` 16 image/label pairs. `compare-yolo-models.ps1 -Task test` passed at `artifacts/yolo-model-comparison/coco128-true-heldout-run/20260628-151453/comparison-summary.json`; `yolov5m.pt` scored mAP50-95 `0.657` versus `yolov5s.pt` `0.561`. Treat this as true held-out pipeline evidence on public COCO data, not as industrial OK/NG replacement evidence.
2026-06-28 industrial Kolektor held-out prep: `scripts/prepare-industrial-dataset.ps1 -CreateYoloLabelsFromKolektorMasks -TestSplitRatio 0.15` generated `artifacts/industrial-datasets/kolektor-yolo-heldout-20260628-153341/KolektorSDD/app/data.yaml` with train 238, valid 102, and test 59 image/label pairs. Label BMP images copied as labeling images: 0. Defect labels: 52. Empty normal labels: 347. `data.yaml` first bytes were `112,97,116`, confirming no UTF-8 BOM. Comparison preflight correctly blocked current weights because dataset labels=1, operational `best.pt` labels=2, and `yolov5s.pt` labels=80.
2026-06-28 industrial `Defect` short-train comparison: `codex_kolektor_defect_baseline_20260628_1ep` and `codex_kolektor_defect_candidate_20260628_3ep` were trained with the same 1-class label list and compared on held-out `test` at `artifacts/yolo-model-comparison/kolektor-defect-heldout-run/20260628-155827/comparison-summary.json`. The pipeline passed, but both models scored precision/recall/mAP `0` and UI candidates `0/17700`, so these weights are not adoption candidates.
2026-06-28 industrial `Defect` oversampling comparison: `scripts/prepare-industrial-dataset.ps1 -TrainPositiveOversampleFactor 8` created `artifacts/industrial-datasets/kolektor-yolo-oversample-20260628-1605/KolektorSDD/app/data.yaml` with train positive labels increased from 29 to 232 while valid/test stayed unchanged. `codex_kolektor_defect_oversample_20260628_5ep` moved validation recall to `0.0588`, but held-out `test` comparison at `artifacts/yolo-model-comparison/kolektor-defect-oversample-vs-short-run/20260628-162519/comparison-summary.json` still scored precision/recall/mAP `0` and UI candidates `0/17700`. Treat oversampling alone as tested but insufficient.

Guide YOLO dataset structure lesson is covered by `--wpf-learning-workflow-panel`: the first-visible Guide area now shows `data.yaml`, image folders, label folders, same-stem image/txt pairing, and one txt-row meaning through ViewModel-bound UI. Do not remove it while refining beginner workflow wording unless the same concept remains visible in the app.

Guide object-detection MVP next-action summary is covered by `--wpf-learning-workflow-panel`: the dataset dashboard exposes a visible `객체탐지 MVP 완료까지` line derived from the same dashboard action text. Keep this summary aligned with the dashboard action and top workflow next-action wording so beginners do not receive competing next steps.

Dataset setup initial-class input is covered by the full `LabelingApplication.Tests` regression and `--wpf-dataset-wizard-smoke`: the wizard must keep explaining that comma, semicolon, or newline separated entries create multiple classes. `Defect, OK, NG` is a protected example and must build three class names, with the parsed count visible through `ClassSummaryText`.

Class catalog edit UX is covered by the full `LabelingApplication.Tests` regression and `--wpf-visual-smoke --review-tab classes`: registered classes can be renamed without changing YOLO class order, class colors can be changed through visible semantic color chips, and `Defect` must not be hard-coded as undeletable. Only deleting the final remaining class is blocked, because the labeling workflow needs at least one available class.

Main dataset switching is covered by the full `LabelingApplication.Tests` regression and an EXE mouse-click smoke: the shell header exposes `데이터셋 선택`, the change command opens `WpfDatasetSelectionWindow`, existing recipes can be opened without recreating them, and new dataset creation remains a separate selector command. 2026-06-28 EXE check: `EXE_DATASET_SELECTION_MOUSE_SMOKE selectionList=True createWizardDirect=False`.

Training settings beginner guidance is covered by the full `LabelingApplication.Tests` regression and `--wpf-visual-smoke --review-tab yolo`: the training panel must keep visible per-field explanations, concrete recommended values, and the `ApplyFastTrainingPresetButton` command that applies the fast first-training preset (`image 320`, `batch 4`, `epoch 50`, `yolov5s`, validation `20%`, final test `0%`, seed `17`). Do not revert this panel to unexplained numeric inputs.

Current-dataset YOLO training completion visibility is covered by `WPF training weights service selects latest best.pt` and `--wpf-yolo-training-session-smoke`: model discovery must include `ProjectRoot\yolov5Master\runs\train`, prefer runs whose `opt.yaml data:` points at the active dataset `data.yaml`, show run-scoped names such as `exp7\best.pt`, and keep distinguishing a staged trained-model candidate from the saved inspection model.

Canvas label/inference display separation is covered by `--wpf-canvas-panel-commands`, `--wpf-detection-display-mode`, `--wpf-canvas-detection-overlay`, and `--wpf-current-image-smoke-preserve-labels`: the canvas display selector must keep `라벨`, `추론`, and `모두` modes distinct, hide AI candidates/result cards in label-only mode, restore candidates in inference/both modes, and leave manual/current labels intact while switching views.

2026-07-01 current canvas work-mode contract: the canvas display selector must keep `라벨 편집`, `AI 검토`, and `비교` modes distinct; the layer strip must state `작업: 저장 라벨 편집`, `작업: AI 후보 검토`, or `작업: 라벨+AI 비교`; label-only mode must hide AI candidates/result cards, inference/both modes must restore candidates, and manual/current labels must remain intact while switching views. Verified with `--wpf-canvas-panel-commands`, `--wpf-detection-display-mode`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, and `--wpf-visual-smoke --review-tab candidates --width 1920 --height 1080`. Before/after captures: `artifacts\ui\wpf-canvas-label-ai-mode-before-1920.png`, `artifacts\ui\wpf-canvas-label-ai-mode-after-1920.png`.

Right-workflow dock behavior is covered by `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, and 1920 visual-smoke captures. The labeling stage must default to a collapsed right rail so the image queue and canvas dominate the workbench, while dataset, inference review, and training/model stages keep the right panel expanded. Keep the collapsed rail buttons command-bound through `WpfLabelingShellViewModel`; do not replace this with code-behind-only panel visibility toggles.

Bottom-log collapse behavior is covered by `--wpf-status-panels`, `--wpf-labeling-shell`, the 1920/1366 responsive-layout checks, and `--wpf-visual-smoke --roi-only --review-tab objects`. The bottom log must start as a 42px latest-log summary, keep the detailed `OpenVisionLab.Logging.Controls` panel available through `로그 열기`, and bind row height through `WpfShellLogPanelViewModel`.

Model-history row actions are covered by `--wpf-status-panels`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke --model-center`, and the 1920/1366 responsive-layout checks. The model center must keep history rows selectable, show selected-row details, disable action for the current or missing-file model, and expose `검사 모델로 적용` only for an existing non-current candidate through `WpfLabelingShellViewModel.PromoteSelectedModelHistoryCommand`.

Model-center current-inspection reachability is covered by `--wpf-status-panels`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke --model-center`, and the 1920/1366 responsive-layout checks. The training/model stage must expose `현재 검사` next to candidate review and inspection-model save in both the top workflow action panel and the model-center lifecycle action row, bound to `WpfLabelingShellViewModel.DetectCurrentImageCommand` and `IsModelCenterInspectCurrentImageEnabled`.

2026-07-01 right-workflow rail label check, updated 2026-07-08 for current-task naming: the collapsed labeling rail is intentionally an icon+short-label dock (`열기`, `라벨`, `작업`, `클래스`, `AI`, `모델`) rather than a 48px icon-only strip, so first-time users can identify the on-demand panels. Verified with `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and `--wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080`; the current-task naming follow-up is covered by `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, and capture `artifacts\ui\wpf-labeling-current-work-icon-after-20260708-1920.png`. Historical before/after captures: `artifacts\ui\wpf-labeling-right-dock-before-1920.png`, `artifacts\ui\wpf-labeling-right-dock-after-1920.png`.

2026-07-01 right-workflow local rail contract, updated 2026-07-08 for current-task naming: in the labeling stage, the collapsed right rail is local to the labeling workbench. It must expose only the open/saved-labels/current-work/classes panel buttons (`열기`, `라벨`, `작업`, `클래스`). Do not add inference-review or model-center stage navigation back to this rail; those cross-stage actions belong to the top workflow rail. Covered by `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and the 1920 visual smoke capture `artifacts\ui\wpf-labeling-local-right-rail-after-1920.png`; current naming is additionally covered by `artifacts\ui\wpf-labeling-current-work-icon-after-20260708-1920.png`.

2026-07-01 top workflow rail density contract: the top workflow rail should remain stage navigation plus the current critical next action, not a second documentation row. Stage buttons must stay one-line movement buttons, the rail height should remain 54px, and the longer current-stage explanation should stay on the summary tooltip binding. Covered by `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and the 1920 visual smoke capture `artifacts\ui\wpf-top-workflow-rail-after-1920.png`.

2026-07-02 right-workflow expanded-header contract: when the right workflow panel is expanded, its header must show both the active view title and a one-line role description from `WpfLabelingShellViewModel.RightWorkflowViewDetailText`. Keep the detail text computed in the ViewModel for dataset, labeling shortcuts, inference review, and training/model stages; XAML should only bind and display it. Do not attach hover tooltips to this header that can obscure the panel content during visual inspection. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-right-workflow-task-before-1920.png`, `artifacts\ui\wpf-right-workflow-task-after-1920.png`.

2026-07-02 guide/tools template workflow contract: the right `Guide/Tools` panel must keep a visible, structured `template repeat labeling` flow that tells the operator to select a source label, generate current-image candidates, generate whole-list candidates, then review/save. The step text and shortcut commands belong to `WpfLearningWorkflowPanelViewModel`; the shell may only inject the existing template auto-label ViewModel commands through `ConfigureLearningWorkflowPanelCommands`. Do not move this back into the header tools popup only, because users miss occasional tools when the popup is closed. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-learning-workflow-panel`, `--template-guide-ux`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --review-tabs guide --width 1920 --height 1080`, `--wpf-responsive-layout --review-tabs guide --width 1366 --height 768`, and visual captures `artifacts\ui\wpf-guide-tools-flow-before-1920.png`, `artifacts\ui\wpf-guide-tools-template-flow-after-1920.png`.

2026-07-02 guide/tools helper-role contract: the right `Guide/Tools` panel must explicitly separate primary labeling work from helper tools. The primary path is draw label -> label save -> next image. Template repeat labeling is a helper that creates candidates; it must still tell the operator to review and press label save before training data is updated. Keep this wording in `WpfLearningWorkflowPanelViewModel`, with XAML limited to binding and stable automation ids. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-learning-workflow-panel`, `--template-guide-ux`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --review-tabs guide --width 1920 --height 1080`, `--wpf-responsive-layout --review-tabs guide --width 1366 --height 768`, and visual captures `artifacts\ui\wpf-guide-tools-helper-role-before-1920.png`, `artifacts\ui\wpf-guide-tools-helper-role-after-1920.png`, `artifacts\ui\wpf-guide-tools-helper-role-after-1366.png`.

2026-07-01 current dataset context bar contract: the dataset bar should remain a compact 48px context/action row, not a full storage details panel. Keep the current dataset name, purpose, work-basis summary, and dataset/storage/image actions visible; keep storage/image path detail bindings available but collapsed by default. Covered by `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and the 1920 visual smoke capture `artifacts\ui\wpf-dataset-context-bar-after-1920.png`.

2026-07-01 top status row operations contract: the row under the dataset context bar is for live operational state, not workflow-stage documentation. It should stay 30px high and visibly carry dataset queue summary, inference state, annotation-save state, and model state. Keep workflow stage/progress/next backing bindings available for automation, but collapsed by default because the workflow rail owns that visible guidance. Covered by `--wpf-status-panels`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and the 1920 visual smoke capture `artifacts\ui\wpf-status-row-after-1920.png`.

2026-07-01 canvas toolbar focus contract: the canvas toolbar should prioritize the controls used while labeling: active drawing tool, display mode, local label save/no-object completion, active class, class management, undo/redo/delete. Keep longer mode/save/class explanations bound for tooltips and automation but collapsed by default, and keep the selected-tool summary chip collapsed because the selected tool is already visible in the tool selector. Do not lower the toolbar `MinHeight="37"` guard; it protects against the top clipping reported in EXE-sized windows. Covered by `--wpf-canvas-panel-commands`, `--wpf-canvas-workflow-context`, `--wpf-detection-display-mode`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and the 1920 visual smoke capture `artifacts\ui\wpf-canvas-toolbar-after-1920.png`.

2026-07-02 canvas toolbar density contract: at 1366x768 the canvas toolbar must keep the working controls on one row, including delete. The class-management action belongs beside the active class selector as a compact command button; the duplicate active-label detail card and decorative quick-tools label stay collapsed by default. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-canvas-panel-commands`, `--wpf-canvas-workflow-context`, `--wpf-detection-display-mode`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, and `--wpf-visual-smoke --roi-only --review-tab objects --width 1366 --height 768`. Before/after captures: `artifacts\ui\wpf-canvas-toolbar-density-before-1366.png`, `artifacts\ui\wpf-canvas-toolbar-density-after-1366-final.png`.

2026-07-02 image queue control clipping contract: the left `Image Queue` control area must not clip the top folder/refresh/next/open buttons when the 300px panel wraps at EXE-sized widths. The primary action, current-task, and batch-progress rows must auto-size with compact minimum heights rather than relying on fixed rows that hide wrapped controls. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-image-queue-status`, `--wpf-labeling-shell`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-left-queue-tabs-after-1366x768.png`, `artifacts\ui\wpf-left-queue-tabs-after-1920x1080.png`.

2026-07-02 right workflow tab theme contract: right workflow sub-tabs (`Guide/Tools`, `Class`, and other local views) must use the app dark tab template and selected/hover states from app brushes. Do not rely on the default WPF `TabItem` template because it renders white chrome inside the dark workbench. Covered by `--wpf-labeling-shell`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-right-tabs-after-1366x768.png`, `artifacts\ui\wpf-left-queue-tabs-after-1920x1080.png`.

2026-07-02 saved-label vs AI-candidate mode badge contract: the right saved-label review panel must start with a visible `저장 라벨만` / `AI 후보 표시 안 함` mode signal, and the right candidate-review panel must start with a visible `AI 후보 검토` / `확정 전에는 저장 라벨 아님` mode signal. Keep these texts owned by `WpfObjectReviewPanelViewModel` and `WpfCandidateReviewPanelViewModel`; XAML should only bind and style them. Do not replace this with log-only messaging, because users need to know whether they are editing committed labels or reviewing unsaved candidates while looking at the panel. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-candidate-review-panel`, `--wpf-object-review-panel`, `--wpf-labeling-shell`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-label-vs-candidate-after-1920.png`, `artifacts\ui\wpf-label-vs-candidate-after-1920-right-crop.png`, `artifacts\ui\wpf-saved-label-panel-after-1920-expanded-right-crop.png`.

2026-07-02 confirmed AI candidate saved-label contract: once an AI candidate is confirmed, the right saved-label panel must present it as `확정 라벨`, not as a pending AI candidate. The row detail/tooltip must identify the source as `AI 후보 확정` and say it was reflected as a saved label. Candidate Review completion text must distinguish `라벨 저장 필요` from `저장 완료`, matching actual file persistence state. Confirmed candidate class names must be registered in the class catalog before saving, and the selected saved-label class combo must stay aligned with the selected confirmed-label row after class-list refresh. The right workflow `ReviewTabControl` must keep `TabStripPlacement="Top"` so local tabs do not reserve a left-side blank area that covers saved-label content. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-candidate-review-panel`, `--wpf-object-review-panel`, `--wpf-labeling-session-smoke`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed.png`, `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed-right-panel-crop.png`.

2026-07-02 confirmed saved-label edit dirty-state contract: changing the class of a confirmed/saved label or deleting it must immediately move the active image back to `저장 필요` across the canvas save state, left image queue row, selected-image task card, and right saved-label panel action text. Queue refresh must not overwrite this dirty state while `annotationDirtyReason` remains set. Keep the dirty-state source in the queue item model/ViewModel and use shell code-behind only as the active-image persistence adapter; do not move this into view-only text or the bottom log. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-session-smoke`, `--wpf-image-queue-status`, `--wpf-object-review-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920.png`, `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-left-crop.png`, `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-right-crop.png`, `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-top-crop.png`.

2026-07-02 confirmed saved-label edit save-recovery contract: after a confirmed/saved label class edit or delete has moved the active image to `저장 필요`, pressing `라벨 저장` must clear the dirty state across the canvas save button, left image queue row, selected-image task card, top status row, and right saved-label panel. The right saved-label panel must keep its save-state badge in `WpfObjectReviewPanelViewModel` (`라벨 대기`, `저장 필요`, `저장됨`) and XAML must only bind/style it. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-session-smoke`, `--wpf-object-review-panel`, `--wpf-image-queue-status`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png`, `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-left-crop.png`, `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-right-crop.png`, `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-top-crop.png`.

2026-07-02 saved-label edit focused rerun: the saved-label edit/save-recovery path was rechecked after the tutorial/documentation commit. `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors, and `--wpf-labeling-session-smoke`, `--wpf-object-review-panel`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-labeling-shell`, and the 1920x1080 save-recovery visual smoke passed. Evidence capture: `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png`.

2026-07-02 tutorial standalone image contract: the user-facing HTML tutorial should be a practical 작업 가이드, use large real workbench captures instead of a few small examples, and keep the copyable standalone HTML self-contained. The standard HTML may reference `docs/tutorial/images`, but the standalone HTML must embed every displayed PNG as `data:image/png;base64` and leave zero `src="images/...` references. The tutorial should continue explaining image folder vs save folder, saved label vs AI candidate, training complete vs inspection-model application, template batch labeling, and multi-model comparison. Public tutorial copy and screenshots must not expose personal local paths, temporary verification notes, or one-off conversation context.

2026-07-02 README/tutorial latest-image contract: whenever `README.md`, `docs/tutorial/README.md`, or the HTML tutorial is updated, the visible screenshots must be refreshed from the latest current EXE UI rather than reused from stale layouts. Korean rule: README/튜토리얼 화면 이미지는 반드시 최신 UI 캡처 기준으로 작성한다. If the UI changed after the previous capture, replace the capture, re-apply numbered callouts/arrows so a beginner can follow from the image alone, and regenerate `docs/tutorial/labeling-workbench-tutorial-standalone.html` so the latest images are embedded.

2026-07-02 segmentation save fallback contract: YOLO segmentation export must not depend on OpenCV contour extraction for raster mask JSON save. Raster mask labels are saved as mask PNG plus a managed bounding rectangle polygon in segment JSON, while polygon labels keep the existing polygon/cutout export path. This is a save/export fallback only; Viewer/OpenGL/ROI/brush/eraser performance paths remain protected. Verified in clean worktree `C:\Git\Labelling_Application_opencv_stage` with build 0 warnings/errors, `--segmentation-annotation-storage`, `--wpf-labeling-session-smoke`, `--wpf-annotation-purpose-export`, `--wpf-object-review-panel`, `--wpf-image-queue-status`, `--mvvm-infra`, and `git diff --check`.

2026-07-02 inspection-model status contract: the top status row must keep a dedicated `InspectionModelStatusText` badge for the model that will be used by current inspection/inference, separate from transient workflow/model messages. The model center must continue to distinguish current inspection model, newly trained candidate, pending recipe save, and model-history comparison metrics. Candidate adoption evidence must not make missing or pending metrics look like a training failure. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-status-panels`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`, and visual captures `artifacts\ui\wpf-inspection-model-status-after-1920.png`, `artifacts\ui\wpf-inspection-model-status-row-crop.png`.

2026-07-02 tutorial public-document contract: the main tutorial must describe the full app workflow without binding the reader to a personal Test recipe, local image folder, or temporary verification run. It should still cover recipe selection, image queue/storage state, class catalog, saved labels, training/model center, inference review, and a completed `현재 검사` flow. Do not present training completion as inspection-model application; the tutorial must show current inspection model and trained candidate separately. When HTML changes, regenerate `docs/tutorial/labeling-workbench-tutorial-standalone.html` and verify that every displayed image is embedded in the standalone copy.

2026-07-02 model-center compact metric contract: the first-visible trained-candidate row should show only the core metrics needed for quick model judgment: `mAP50-95`, `mAP50`, and combined `P/R`. Keep detailed metrics such as `box loss` in model history, selected-model comparison, or decision evidence, not in the first candidate row. The compact formatter must handle both slash-separated and comma-separated metric summaries. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--model-registry`, and `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-metrics-compact-after-1920.png`.

2026-07-02 model-history compact list contract: the model-center history list is a selector, not the full detail view. Each history row should show only the model summary and decision state; run detail, metrics, current-vs-selected comparison, and apply action belong in the selected-history detail panel below the list. Keep the history list height bounded so selected details remain reachable when more model runs exist. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--model-registry`, and `--wpf-yolo-training-session-smoke --model-center` at 1920x1080 and 1366x768.

2026-07-02 model-registry compact summary contract: the model-center registry summary should show only the current inspection model, trained candidate, model family, latest training state, history count, and recipe-save state by default. Detailed profile/run/candidate/inspection/action rows stay available behind `ModelRegistryDetailExpander` and must remain collapsed by default so model history and selected details are reachable on 1366x768 equipment screens. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--model-registry`, and `--wpf-yolo-training-session-smoke --model-center` at 1920x1080 and 1366x768.

2026-07-02 selected-model-history comparison contract: the selected model-history detail should not contain a nested comparison card. Keep `SelectedModelHistoryComparisonPanel` as a thin two-column summary row with current inspection model, selected model, and one-line metric comparison. Long paths and metrics should trim with tooltips rather than increasing panel height. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--model-registry`, and `--wpf-yolo-training-session-smoke --model-center` at 1920x1080 and 1366x768.

2026-07-02 model-adoption decision contract: the model-center adoption decision card should default to one visible decision summary line. Evidence and exact save/action wording belong behind `YoloModelAdoptionDecisionDetailExpander`, collapsed by default, while keeping the existing evidence/action AutomationIds and ViewModel bindings for diagnostics and automation. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--model-registry`, and `--wpf-yolo-training-session-smoke --model-center` at 1920x1080 and 1366x768.

2026-07-02 model-center action-state contract: the model-center must show a concise inline state/reason directly under the core actions `candidate review -> save as inspection model -> inspect current image`. The reason text belongs in `WpfLabelingShellViewModel.ModelCenterActionStateText`; XAML should only bind it through `ModelCenterPriorityButtonStateText`. Do not move this back to tooltip-only or log-only messaging, because the operator needs to know why a button is waiting while staying in the model center. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--model-registry`, and `--wpf-yolo-training-session-smoke --model-center` at 1920x1080 and 1366x768.

2026-07-02 optional model-runtime contract: the workbench must remain usable for dataset creation and labeling when no YOLO/Python model runtime is installed or connected. Missing runtime must not silently rewrite a user-selected/custom runtime path to `C:\Git\yolov5`; it should be shown as `라벨링 가능 / 모델 실행기 미설치`, with model actions disabled or guarded and the next action pointing to runtime installation or path connection. The same state must be reflected in the top status row, training/model center, model registry, and post-training action card. YOLO11 must remain selectable as a future Ultralytics-family profile even before the adapter is installed. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--mvvm-infra`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--wpf-labeling-shell`, `--model-registry`, `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`, and `--wpf-visual-smoke --review-tab yolo --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-optional-before-1920.png`, `artifacts\ui\wpf-model-runtime-missing-after-1920.png`.

2026-07-02 model-runtime profile list contract: the YOLO/model settings panel must keep a visible `모델 실행기 연결 상태` list for `YOLOv5`, `YOLOv8`, `YOLO11`, and `ONNX`. The selected engine state must come from `PythonModelSettingsValidator`, unselected YOLOv8/YOLO11 profiles must remain clearly identified as Ultralytics-family install/connect targets, and XAML should only bind `RuntimeProfileItems` from `WpfYoloModelSettingsPanelViewModel`. Do not move runtime-family selection or readiness logic into view code-behind. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-profile-before-1920.png`, `artifacts\ui\wpf-model-runtime-profile-after-1920.png`.

2026-07-02 model-runtime profile action contract: each runtime profile row must keep a compact primary action button driven by `RuntimeProfileActionCommand` on `WpfYoloModelSettingsPanelViewModel`. Clicking a profile action should select the target engine and update `RuntimeProfileActionStatusText` with the next operator step; it must not require log inspection to understand what changed. Keep action availability synchronized with `ApplyWorkflowCommandState` through `IsRuntimeProfileActionEnabled`. XAML should bind `PrimaryActionText`, `RuntimeProfileActionCommand`, and `RuntimeProfileActionStatusText`; do not move this action state into view code-behind. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-action-before-1920.png`, `artifacts\ui\wpf-model-runtime-action-after-1920.png`.

2026-07-02 model-runtime profile connect-adapter contract: runtime profile actions may use shell code-behind only as a UI adapter for expanding the advanced runtime settings panel, focusing the relevant path editor, and updating operator-facing status/log text. The action state and selected engine must remain in `WpfYoloModelSettingsPanelViewModel`; do not move model-runtime readiness or workflow decisions into shell code-behind. YOLOv5 should focus the project-root editor, YOLOv8/YOLO11 should focus the Python runtime editor, and ONNX should focus the inspection-model editor until a dedicated installer/self-test service owns those workflows. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-connect-before-1920.png`, `artifacts\ui\wpf-model-runtime-connect-after-1920.png`.

2026-07-02 model-runtime self-test contract: selected model-runtime readiness must be visible as an operator-facing checklist in the YOLO/model settings panel, not only as log text or a disabled button. Keep path/file checks in `PythonModelRuntimeSelfTestService`; keep presentation state in `WpfYoloModelSettingsPanelViewModel` through `RuntimeSelfTestItems`, `RuntimeSelfTestSummaryText`, and `RuntimeSelfTestDetailText`; keep XAML as bindings only. Missing inspection weights may be a warning when training is still possible, but missing project/script runtime inputs must remain blocking for model execution. Do not run external installers, Python commands, git clone, or pip from this self-test service. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-selftest-before-1920.png`, `artifacts\ui\wpf-model-runtime-selftest-after-1920.png`.

2026-07-02 YOLOv5 runtime folder connection contract: YOLOv5 runtime connection must flow through folder picker -> `PythonModelRuntimeConnectionService.BuildYoloV5FolderConnection` -> `WpfYoloModelSettingsPanelViewModel.ApplyRuntimeConnectionResult` -> visible self-test refresh. The core service owns mapping a selected folder to project root, model root, venv Python, client script, weights, and image root. Shell code-behind may only show the folder picker, focus the project-root editor, and write status/log adapter messages. Selecting `yolov5Master` must resolve to the parent project root. Do not run external installers, Python commands, git clone, or pip from the YOLOv5 folder connection path. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-yolov5-connect-before-1920.png`, `artifacts\ui\wpf-model-runtime-yolov5-connect-after-1920.png`.

2026-07-02 Ultralytics runtime connection/self-test contract: YOLOv8/YOLO11 runtime connection must flow through Python picker -> `PythonModelRuntimeConnectionService.BuildUltralyticsPythonConnection` -> `WpfYoloModelSettingsPanelViewModel.ApplyRuntimeConnectionResult` -> visible self-test refresh. The self-test should inspect the selected venv's `Lib\site-packages` for `ultralytics` or `ultralytics-*.dist-info` and report `설치 필요` when the package is missing. Shell code-behind may only show the Python picker, focus the Python editor, and write status/log adapter messages. Do not run external installers, Python commands, git clone, or pip from this connection/self-test path. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and the 1920 visual capture `artifacts\ui\wpf-model-runtime-ultralytics-connect-after-1920.png`.

2026-07-02 Ultralytics install plan preview contract: YOLOv8/YOLO11 install readiness must first show a read-only plan before any environment-changing command is allowed. Keep venv/package/command calculation in `PythonModelRuntimeInstallPlanService`; keep WPF state in `WpfYoloModelSettingsPanelViewModel.RuntimeInstallPlan*`; keep XAML as bindings only. Missing packages may preview `python.exe -m pip install --upgrade ultralytics`, and installed packages may preview `python.exe -m pip show ultralytics`, but this preview path must not execute Python, pip, git, or external installers. The visual smoke missing-runtime fixture may use a fake venv with no `ultralytics` package so the install-needed state is visible at 1920x1080. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-install-plan-before-1920.png`, `artifacts\ui\wpf-model-runtime-install-plan-after-1920.png`.

2026-07-02 Ultralytics install/uninstall action contract: YOLOv8/YOLO11 package setup must keep install and uninstall as separate visible actions in the install-plan card. Install may run `python.exe -m pip install --upgrade ultralytics`; uninstall may run `python.exe -m pip uninstall -y ultralytics` for repeatable setup tests. Keep package command execution in `PythonEnvironmentService`, keep button command/state in `WpfYoloModelSettingsPanelViewModel`, and keep shell code-behind as the UI adapter for command lifecycle, status, and log tail output. The UI must show both `설치 실행` and `제거` without scrolling at 1920x1080. Automated tests must not run real pip install/uninstall; they should verify command generation, bindings, and adapter routing. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, full regression up to the existing template auto-label failure, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-install-uninstall-before-1920.png`, `artifacts\ui\wpf-model-runtime-install-uninstall-after-1920.png`.

2026-07-02 Ultralytics package confirmation contract: install/uninstall buttons must not mutate a venv immediately. The shell adapter must build the current `PythonModelRuntimeInstallPlan`, show `WpfMessageDialog.Confirm` with the target venv and exact pip command, and continue only on `WpfMessageDialogResult.Yes`. Cancel must leave the environment unchanged and write an operator-visible status/log message. Do not replace this with stock `MessageBox.Show`. After a successful install or uninstall, reload the same settings snapshot into `WpfYoloModelSettingsPanelViewModel` so the self-test/install-plan state is recalculated. Keep a visible hint under the buttons that the target venv and command will be confirmed before execution. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, full regression up to the existing template auto-label failure, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-confirm-before-1920.png`, `artifacts\ui\wpf-model-runtime-confirm-after-1920.png`.

2026-07-02 Ultralytics package recent-result contract: the YOLO/model settings panel must show a `최근 실행 결과` card inside the Ultralytics install-plan area so operators do not need the bottom log to know the last package action outcome. The default state must say no run has happened. Install/uninstall success, failure, and cancel should update `WpfYoloModelSettingsPanelViewModel.RuntimePackageResultSummaryText` and `RuntimePackageResultDetailText` with time, target venv, command, ExitCode, status, and a short stdout/stderr summary where available. Shell code-behind may format this as a UI adapter, but package execution must remain in `PythonEnvironmentService`. The card title and summary must remain visible at 1920x1080. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, full regression up to the existing template auto-label failure, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-result-card-before-1920.png`, `artifacts\ui\wpf-model-runtime-result-card-after-1920.png`.

2026-07-02 model-center runtime summary contract: the model center must show not only the current inspection weights but also the selected runtime family/readiness in the first-visible registry summary and current-inspection row. Keep the runtime summary derived from `PythonModelRuntimeProfileService` via `WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText`; do not duplicate model-runtime readiness decisions in XAML or shell code-behind. The model-center dashboard may pass the summary into `SetModelCenterModelState`, but shell code-behind should remain a refresh adapter. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-settings-viewmodels`, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, and `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-center-runtime-summary-before-1920.png`, `artifacts\ui\wpf-model-center-runtime-summary-after-1920.png`.

2026-07-02 runtime execution route contract: the YOLO/model settings panel must show a read-only execution route summary that separates Python worker launch, training request route, and current-inspection request route. Keep route calculation in `PythonModelRuntimeExecutionSummaryService`; keep panel state in `WpfYoloModelSettingsPanelViewModel.RuntimeExecution*`; keep XAML as bindings only. The inspection route must include the actual adapter key sent to the worker, such as `DetectImage(model=yolo11)`, so operators can verify that selecting YOLO11 changes the inspection request path. Do not move this route formatting into shell code-behind. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-self-test`, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, full regression up to the existing template auto-label failure, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-model-runtime-execution-route-before-1920.png`, `artifacts\ui\wpf-model-runtime-execution-route-after-1920.png`.

2026-07-02 training adapter-key protocol contract: YOLO training requests must include the selected adapter key in the `StartTraining` payload as `model`, defaulting to `yolov5` for backward compatibility. `YoloTrainingWorkflowService` should derive the key from `PythonModelSettings.GetProtocolModelName()`, `LearningProtocol.BuildTrainingPacket` should serialize it, and `PythonModelRuntimeExecutionSummaryService` should show the same key as `StartTraining(model=...)`. The current YOLOv5 worker ignores unknown training payload fields and still maps legacy `StartTraining` to `TrainYolo`, so this extension must stay compatible with the existing YOLOv5 route. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-self-test`, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`, `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`, and `--real-yolo-smoke`. Real YOLOv5 smoke evidence: `artifacts\real-yolo-smoke\20260702-204427\summary.txt` with `candidateCount=1` and `committedCount=1`. Captures: `artifacts\ui\wpf-yolo-training-model-key-before-1920.png`, `artifacts\ui\wpf-yolo-training-model-key-after-settings-1920.png`, `artifacts\ui\wpf-yolo-training-model-key-after-1920.png`.

2026-07-02 Ultralytics execution guard contract: YOLOv8/YOLO11 profile selection and package/path self-test must not imply that training or current inspection is executable through the existing YOLOv5 TCP worker. Keep execution-support decisions in `PythonModelRuntimeAdapterSupportService`; have `PythonModelSettingsValidator.GetRuntimeState` return `CanRunTraining=false` and `CanRunInference=false` for YOLOv8/YOLO11 until a real Ultralytics worker route is connected. The settings panel should still show `StartTraining(model=yolo11)` and `DetectImage(model=yolo11)` as the intended adapter key, but the route text must say execution is blocked and that it will not silently fall back to the YOLOv5 worker. Self-test must include a blocking `실행 연결` item. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-self-test`, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080`, `--real-yolo-smoke`, and full regression up to the existing template auto-label failure. Real YOLOv5 smoke evidence: `artifacts\real-yolo-smoke\20260702-205410\summary.txt` with `candidateCount=1` and `committedCount=1`. Captures: `artifacts\ui\wpf-ultralytics-execution-block-before-1920.png`, `artifacts\ui\wpf-ultralytics-execution-block-after-1920.png`.

2026-07-02 worker capability handshake contract: connected Python workers may unlock YOLOv8/YOLO11 execution only by reporting explicit capabilities through `HealthCheckResult` or `ModelStatusResult`. `PythonModelStatusProtocol` must parse root/`capabilities`/`worker.capabilities` fields for `supportedModels`, training model keys, and detection/inspection model keys; `CCommunicationLearning` must store them in `PythonCommunicationStatus`; `WpfLabelingShellWindow.GetPythonModelRuntimeState` may pass the current status snapshot into `PythonModelSettingsValidator.GetRuntimeState`. Without these capabilities, YOLOv8/YOLO11 remain blocked even when the `ultralytics` package folder exists. This prevents silent fallback to the YOLOv5 worker. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-self-test`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, `--python-model-status-protocol`, `--real-yolo-smoke`, and full regression up to the existing template auto-label failure. Real YOLOv5 smoke evidence: `artifacts\real-yolo-smoke\20260702-210425\summary.txt` with `candidateCount=1` and `committedCount=1`.

2026-07-02 bundled Ultralytics worker connection contract: YOLOv8/YOLO11 Python connection must not keep the legacy YOLOv5 `labelling_tcp_client.py` path. `PythonModelRuntimeConnectionService.BuildUltralyticsPythonConnection` must resolve `Runtime\Python\openvisionlab_ultralytics_worker.py` through `PythonModelRuntimeBundledWorkerService`, and the project file must copy that script to build and publish output. The bundled worker currently supports detection only: `detectionModels` may unlock inspection, but empty `trainingModels` must not unlock training even when `supportedModels` lists the adapter for backward-compatible discovery. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-ultralytics-worker`, `--python-model-runtime-self-test`, `--python-model-status-protocol`, `python Runtime\Python\openvisionlab_ultralytics_worker.py --self-test`, `python -m py_compile Runtime\Python\openvisionlab_ultralytics_worker.py`, and output copy verification at `artifacts\run\Debug\Runtime\Python\openvisionlab_ultralytics_worker.py`.

2026-07-02 YOLO11 Ultralytics inference-smoke contract: YOLOv8/YOLO11 current inspection must use Ultralytics-family weights, not YOLOv5 repo-trained `best.pt`. The app may keep YOLOv5 weights on the YOLOv5 worker path, but the bundled Ultralytics worker should surface a clear failure if a YOLOv5 repo weight is supplied. When the selected runtime points to `openvisionlab_ultralytics_worker.py`, the selected venv has `ultralytics` installed, and a weights file exists, runtime state may enable current inspection while keeping training disabled. This partial-ready state must be shown as current-inspection available, not as generic model-settings failure. Covered by `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-model-runtime-connection`, `--python-ultralytics-worker`, `--python-model-runtime-self-test`, `--python-model-status-protocol`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, and real worker smoke evidence `artifacts\model-runtime\ultralytics\yolo11n-test01-smoke.json` plus `artifacts\model-runtime\ultralytics\yolo11n-bus-smoke.json` with `ok=true` and 5 candidates on `bus.jpg`.

2026-07-02 YOLO11 Ultralytics TCP workflow contract: the existing real YOLO TCP smoke must be able to run with a non-YOLOv5 engine by setting `LABELING_SMOKE_MODEL_ENGINE`. When run with `YOLO11`, bundled `openvisionlab_ultralytics_worker.py`, `yolo11n.pt`, and Ultralytics `bus.jpg`, the C# detection workflow must send the selected adapter key, receive `DetectImageResult`, render candidates, confirm them, and save labels/review status. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` and `--real-yolo-smoke` with `LABELING_SMOKE_MODEL_ENGINE=YOLO11`. Evidence: `artifacts\real-yolo-smoke\20260702-215604\summary.txt` with `candidateCount=5`, `committedCount=5`, and `modelEngine=YOLO11`.

2026-07-02 WPF YOLO11 current-image smoke contract: the WPF shell must be able to apply a YOLO11 Ultralytics smoke result to the current image without relying on the YOLOv5 worker. The opt-in `--wpf-ultralytics-current-image-smoke` test should configure the shell with bundled `openvisionlab_ultralytics_worker.py`, `yolo11n.pt`, and Ultralytics `bus.jpg`, then verify that `RunDetectionForImageAsync(..., applyToCanvas:true)` produces candidates, loads them into Candidate Review state/panel rows, and renders canvas detection overlays. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--python-ultralytics-worker`, and `--wpf-ultralytics-current-image-smoke`.

2026-07-02 WPF YOLO11 partial-ready visibility contract: when the selected runtime is YOLO11, the bundled Ultralytics worker exists, `ultralytics` is installed in the selected venv, and weights exist, the UI must show the runtime as current-inspection ready while keeping training unsupported. The selected runtime profile should read `선택됨 / 현재 검사 가능·학습 미지원`, the execution summary should read `현재 검사 가능 / 학습 미지원`, the training route should say `학습: 미지원`, and the inspection route must keep `DetectImage(model=yolo11)`. Keep this wording derived from `PythonModelRuntimeProfileService` and `PythonModelRuntimeExecutionSummaryService`; XAML should remain binding-only. Covered by app/test builds, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--python-ultralytics-worker`, `--python-model-runtime-self-test`, `--wpf-settings-viewmodels`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --ultralytics-runtime-ready --width 1920 --height 1080`. Captures: `artifacts\ui\wpf-ultralytics-partial-ready-before-1920.png`, `artifacts\ui\wpf-ultralytics-partial-ready-after-1920.png`.

2026-07-02 side-panel balance contract: the main labeling workspace should read left-to-right as workflow tools, canvas, image queue. Keep the workflow dock in the left column, the canvas in the center star column, and the image queue in the right 300px column. The image queue may span the log row so the operator can keep browsing images while the bottom log remains under the workflow/canvas area. Responsive layout checks must verify that the left workflow panel or collapsed rail, the canvas, and the right image queue stay visible and non-overlapping at 1920x1080 and 1366x768. Covered by isolated test build, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, `--mvvm-infra`, `--wpf-labeling-shell`, and visual captures `artifacts\ui\wpf-layout-side-swap-before-1920.png`, `artifacts\ui\wpf-layout-side-swap-after-1920.png`, `artifacts\ui\wpf-layout-side-swap-after-expanded-1920.png`, `artifacts\ui\wpf-layout-side-swap-after-1366.png`.

2026-07-02 model-center partial-ready runtime summary contract: the model center registry summary must distinguish a runtime that can run current inspection from one that can also train. If the selected YOLO11/Ultralytics runtime can inspect but cannot train, the first-visible model registry summary and current-inspection row should include `현재 검사 가능 / 학습 미지원`, not only `검사 가능`. Keep this wording in `WpfModelRegistryPresentationService`; XAML and shell code-behind should only consume the presentation text. The visual smoke for this state must keep temporary model-history weight files present before recalculating the dashboard so an applicable history row remains visible. Covered by isolated test build, `--wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready --width 1920 --height 1080`, `--model-registry`, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, and `--wpf-labeling-shell`. Captures: `artifacts\ui\wpf-model-center-runtime-summary-before-1920.png`, `artifacts\ui\wpf-model-center-ultralytics-partial-after-1920.png`.

2026-07-02 model-center non-error emphasis contract: model-center candidate/review/decision states must not reuse the global red `AccentBrush`, because users read that as failure or recovery. Keep actual error/recovery red on `YoloModelRecoveryPanel`; use `ModelCenterCandidateBrush` for trained-candidate selection/selected model-history state and `ModelCenterDecisionBrush` for adoption, comparison, and save/apply decision text. The model history list must use `ModelRegistryHistoryListBoxItemStyle` so selected model-history rows do not inherit the global review-list red selection border. Covered by isolated test build, `--wpf-labeling-shell`, `--model-registry`, `--mvvm-infra`, and `--wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready` at 1920x1080 and 1366x768. Captures: `artifacts\ui\wpf-model-center-ultralytics-partial-after-1920.png`, `artifacts\ui\wpf-model-center-density-after-1920.png`, `artifacts\ui\wpf-model-center-density-after-1366.png`.

2026-07-02 model-center confirm-save feedback contract: after a trained candidate is saved as the inspection model, the UI must no longer read as pending candidate review. `ExecuteSaveYoloSettingsCommand` must clear both `hasPendingTrainingWeightsRecipeSave` and `pendingTrainingBaselineWeightsPath`, then refresh through `RefreshYoloStatus()` so the top inspection-model badge, model-center priority card, workflow-stage save button, and model-center save button all read as current inspection model / `적용 완료`. The pending state must still be visible before recipe save. Covered by isolated test build, `--wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1920 --height 1080`, `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`, `--wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1366 --height 768`, `--wpf-labeling-shell`, `--model-registry`, `--mvvm-infra`, `--wpf-yolo-model-settings-panel`, and `--wpf-settings-viewmodels`. Captures: `artifacts\ui\wpf-model-center-confirm-save-before-1920.png`, `artifacts\ui\wpf-model-center-confirm-save-after-1920.png`, `artifacts\ui\wpf-model-center-confirm-save-after-1366.png`.

2026-07-03 model-center action target contract: the model-center priority card must state the execution target and result destination before the user clicks model actions. `WpfLabelingShellViewModel.ModelCenterActionStateText` should explain that `후보 검증` opens the trained-candidate review tab, while `현재 검사` runs the current inspection model on the current image and shows results as AI candidates on the canvas. XAML may wrap this text but should not hide it behind tooltip-only/log-only messaging. Covered by isolated test build, `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`, `--wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1920 --height 1080`, `--wpf-yolo-training-session-smoke --model-center --width 1366 --height 768`, `--wpf-labeling-shell`, `--mvvm-infra`, `--model-registry`, and `--wpf-yolo-model-settings-panel`. Captures: `artifacts\ui\wpf-model-center-action-target-after-1920.png`, `artifacts\ui\wpf-model-center-action-target-confirmed-after-1920.png`, `artifacts\ui\wpf-model-center-action-target-after-1366.png`.

2026-07-03 inference-status readable-text contract: the top inference status must never expose mojibake for the default state, current inspection model, pending model candidate, or missing model state. Keep the wording in `WpfInferenceStatusPresentationService`; shell code-behind may only pass status text/settings and update the UI animation. The status and tooltip should distinguish `검사 모델`, `모델 후보`, and `검사 모델 없음`, and the tooltip should keep the full model path. Covered by isolated test build, `--wpf-inference-status-presentation`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-model-settings-panel`, and `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`. Capture: `artifacts\ui\wpf-inference-status-readable-after-1920.png`.

2026-07-03 AI-candidate unsaved wording contract: detected boxes shown after current inspection are AI candidates until the operator confirms/saves them. Candidate review and detection-result presentation services must use `AI 후보` and `저장 전` wording for pending detections and `AI 후보 없음` for empty results, while image queue saved-label state remains separate. Do not change the verified canvas/OpenGL detection overlay rendering path for this wording contract. Covered by isolated test build, `--wpf-candidate-review-presentation`, `--wpf-detection-result-presentation`, `--wpf-image-queue-status`, `--wpf-detection-display-mode`, and `--wpf-visual-smoke --width 1920 --height 1080`. Capture: `artifacts\ui\wpf-ai-candidate-unsaved-after-1920.png`.

2026-07-03 candidate-completion next-unfinished contract: after AI candidates are resolved, the candidate review completion card must distinguish `라벨 저장 필요`, `라벨 저장 완료`, and no-object completion while pointing the operator to the next unfinished image. Keep this wording in `WpfCandidateReviewCompletionPresentationService`. Buttons should stay short enough for the left workflow panel, with the full `다음 미완료 이미지` destination in next-action text and tooltip. Covered by isolated test build, `--wpf-labeling-session-smoke --width 1920 --height 1080`, `--wpf-candidate-review-panel`, `--wpf-image-queue-status`, and `--mvvm-infra`. Capture: `artifacts\ui\wpf-candidate-complete-next-unfinished-after-1920.png`.

2026-07-03 queue/top next-unfinished consistency contract: when the current image is saved, confirmed, or completed as no-object, the image queue current-task card and workflow next-action text must say that the next route is the next unfinished image, not a generic next image. Keep image-queue wording in `WpfImageQueuePanelViewModel`, keep stage-level wording in `WpfWorkflowStagePresentationService`, and do not move these presentation decisions into view code-behind. The no-object current-task badge should read `객체없음` instead of ambiguous `없음`. Covered by isolated test build, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--mvvm-infra`, and `--wpf-labeling-session-smoke --width 1920 --height 1080`. Capture: `artifacts\ui\wpf-queue-top-next-unfinished-after-1920.png`.

2026-07-03 candidate-review non-error color contract: AI-candidate guidance in `WpfCandidateReviewPanel.xaml` must not reuse the global red `AccentBrush` for ordinary `저장 전 AI 후보` information. Keep AI-candidate role/badge/action-guide styling on `CandidateReviewAi*` brushes, keep model-validation guidance on `CandidateReviewModelTextBrush`, and reserve global accent/error colors for real save-required/error/primary-action emphasis. Covered by isolated test build, `--wpf-candidate-review-panel`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates --width 1920 --height 1080`. Capture: `artifacts\ui\wpf-candidate-review-color-after-1920.png`.

2026-07-03 candidate-review compact action priority contract: current-image candidate actions must remain above trained-model validation in `WpfCandidateReviewPanel.xaml`, so narrow 1366x768 layouts keep `후보 위치`, `라벨 확정`, `전체 라벨화`, and `후보 숨김` visible before model-validation details. Keep inactive model-candidate decision controls collapsed through `WpfCandidateReviewPanelViewModel.ModelCandidateDecisionVisibility`; show them only when save/reject is actually available. Covered by isolated test build, `--wpf-candidate-review-panel`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-candidate-review-compact-after-1366.png`, `artifacts\ui\wpf-candidate-review-compact-after-1920.png`.

2026-07-03 image-queue current-task wrap contract: the right image queue current-task card must show two compact lines for the operator action instead of forcing a one-line ellipsis. Keep tooltip composition in `WpfImageQueuePanelViewModel.BuildCurrentImageTaskToolTip`, including full filename, title, detail, and queue status summary. Keep XAML limited to row height and wrapping properties. Covered by isolated test build, `--wpf-image-queue-status`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-image-queue-current-task-wrap-after-1366.png`, `artifacts\ui\wpf-image-queue-current-task-wrap-after-1920.png`.

2026-07-03 image-queue row summary tooltip contract: narrow image-queue rows may visually trim long filenames and status columns, but each row must expose a full summary through `WpfImageQueueItem.QueueRowToolTip` and `QueueRowAccessibleName`: filename, saved-label status, inspection status, size, status summary, and detailed failure/action text where present. XAML should bind row/tool column tooltips and automation name to these properties; do not rebuild the row summary in view code-behind. Covered by isolated test build, `--wpf-image-queue-status`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-image-queue-row-summary-after-1366.png`, `artifacts\ui\wpf-image-queue-row-summary-after-1920.png`.

2026-07-03 candidate-review text cap contract: the current-image candidate review panel must keep its primary review controls visible before long selected-candidate, comparison, or detail text. `SelectedCandidateSummaryText`, `ComparisonCandidateText`, `ComparisonCurrentText`, `ComparisonDecisionText`, and `DetailText` may be height-capped in XAML, but each capped text block must expose the full bound text through a tooltip. Do not move candidate selection/confirmation workflow into XAML or shell text helpers. Covered by isolated test build, `--wpf-candidate-review-panel`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-candidate-review-text-cap-after-1366.png`, `artifacts\ui\wpf-candidate-review-text-cap-after-1920.png`.

2026-07-03 runtime summary status contract: the YOLO/model settings first summary card must show the selected runtime execution summary before the full runtime-profile list. `WpfYoloModelSettingsPanelViewModel.SettingsSummaryRuntimeStatusText` should reuse `RuntimeExecutionSummaryText`, including states such as `현재 검사 가능 / 학습 미지원`, and XAML should bind it through `YoloModelSettingsSummaryRuntimeStatusText` using a dedicated runtime-status brush instead of the global error/accent color. Keep runtime readiness decisions in the existing runtime services. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --ultralytics-runtime-ready` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-runtime-summary-status-after-1366.png`, `artifacts\ui\wpf-runtime-summary-status-after-1920.png`.

2026-07-03 model-center runtime action-state contract: the model-center priority card must show the selected runtime readiness next to the actual model action buttons, not only in the registry summary or YOLO settings tab. `WpfLabelingShellViewModel.ModelCenterActionStateText` should start with `실행기: ...` when a runtime summary is supplied, including partial-ready states such as `현재 검사 가능 / 학습 미지원`, then continue with the action target/result route and button availability. The dashboard may pass `WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings)` into the ViewModel, but XAML should keep using the existing `ModelCenterPriorityButtonStateText` binding. Covered by isolated test build, `--wpf-labeling-shell`, `--model-registry`, `--mvvm-infra`, and `--wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-model-center-runtime-action-state-after-1366.png`, `artifacts\ui\wpf-model-center-runtime-action-state-after-1920.png`.

2026-07-03 image-queue AI-candidate spacing contract: image queue quick filters, queue summaries, row detection text, dataset status text, and their tests must spell the pending detection state as `AI 후보`, not `AI후보`. Keep this wording in `WpfImageQueuePresenter`, `WpfImageQueueFilterOption`, and `WpfImageQueuePanelViewModel`; filtering and review-state logic must remain unchanged. Covered by isolated test build, `--wpf-image-queue-status`, `--wpf-labeling-shell`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1366.png`, `artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1920.png`.

2026-07-03 AI-candidate wording consistency contract: current-inspection detections must be described as `AI 후보` in canvas candidate tooltips, candidate-review shell status/log messages, and learning-workflow guidance. Do not reintroduce `검출 후보` as a separate user-facing term in WPF/test scope unless a future UX decision defines a distinct state for it. Keep confirmation/skip/navigation workflow in the existing ViewModel/service/shell-adapter boundaries, and do not change the verified canvas/OpenGL detection overlay rendering path for wording-only work. Covered by isolated test build, `--wpf-canvas-panel-commands`, `--wpf-canvas-detection-overlay`, `--wpf-candidate-review-panel`, `--wpf-learning-workflow-panel`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-ai-candidate-wording-after-1366.png`, `artifacts\ui\wpf-ai-candidate-wording-after-1920.png`.

2026-07-03 candidate auto-save guidance contract: AI candidate confirmation is presented as `확정하면 저장 라벨에 자동 반영` and must not be described as a separate `AI 후보 저장` step. Keep current-image task wording in `WpfImageQueuePanelViewModel` and workflow-stage next-action wording in `WpfWorkflowStagePresentationService`; do not move this presentation state into shell code-behind. The existing confirmation workflow may continue to save confirmed AI candidates immediately, while later edits to confirmed labels still use the normal `라벨 저장 필요` state. Covered by isolated test build, `--wpf-image-queue-status`, `--wpf-labeling-shell`, `--wpf-candidate-review-panel`, `--mvvm-infra`, and `--wpf-visual-smoke --review-tab candidates` at 1366x768 and 1920x1080. Captures: `artifacts\ui\wpf-candidate-autosave-guidance-after-1366.png`, `artifacts\ui\wpf-candidate-autosave-guidance-after-1920.png`.

2026-07-03 template draft-label wording contract: template matching must not be described like AI candidate review. Current-image template matching creates `라벨 초안`/`저장 전 초안` that the operator checks and saves with the normal label-save workflow; whole-image template matching is `전체 이미지 자동 저장` and saves only unlabeled images directly. Keep this distinction in `WpfTemplateMatchingAutoLabelViewModel`, `WpfLearningWorkflowPanelViewModel`, the shell template UI adapter, and the image queue template batch button. Do not move template workflow state into view code-behind and do not touch Viewer/OpenGL/ROI/brush/eraser paths for wording-only changes. Covered by isolated test build, `--template-guide-ux`, `--template-batch-autolabel-storage`, `--wpf-template-current-image-no-candidate`, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, and 1920x1080 visual smokes for Guide/Tools, the header tools menu, and the image queue. Captures: `artifacts\ui\wpf-template-draft-label-guidance-after-1920-clean.png`, `artifacts\ui\wpf-template-draft-header-tools-after-1920.png`, `artifacts\ui\wpf-template-auto-save-queue-button-after-1920.png`.

2026-07-03 dataset-purpose runtime-boundary contract: the Guide/Tools dataset-purpose explanation must distinguish labeling support from model-runtime support. Object detection may continue through the current YOLO/model-center flow, but segmentation and anomaly detection descriptions must state that model training/inspection proceeds after a matching runtime is connected. Keep this wording in `WpfLearningWorkflowPanelViewModel`; XAML should remain binding-only. Do not hard-code a single future model such as U-Net as the only segmentation path, and do not touch Viewer/OpenGL/ROI/brush/eraser paths for this wording contract. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--mvvm-infra`, and 1920x1080 guide visual smoke `artifacts\ui\wpf-purpose-runtime-boundary-after-1920.png`.

2026-07-03 learning-guide readable Korean contract: the Guide/Tools user-facing ViewModel text must not expose mojibake artifacts such as replacement characters, compatibility-Hanja fragments, or Hanja-range corrupt Korean in dataset-purpose summaries, learning-mode detail, workflow-step detail, or annotation-tool detail. Keep the final presentation strings owned by readable resolver methods in `WpfLearningWorkflowPanelViewModel`; XAML and code-behind should remain binding/adapters only. Segmentation and anomaly text must still keep the model-runtime boundary wording from the dataset-purpose contract. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--mvvm-infra`, source artifact search, and 1920x1080 guide visual smoke `artifacts\ui\wpf-learning-guide-readable-source-clean-after-1920.png`.

2026-07-03 runtime package action status text contract: the YOLO/model settings panel must show readable Korean status text immediately after Ultralytics install and uninstall actions are requested. Keep these click-result status strings in `WpfYoloModelSettingsPanelViewModel`; the shell adapter may still handle confirmation dialogs, command lifecycle, and logs, and `PythonEnvironmentService` remains the only package execution path. The install/uninstall ViewModel status text must not contain replacement characters or Hanja-range mojibake artifacts. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, source artifact search, and 1920x1080 YOLO model visual smoke `artifacts\ui\wpf-runtime-package-action-status-after-1920.png`.

2026-07-03 workflow command tooltip readable contract: current-image inspection, selected-image inspection, batch inspection, retry-failed, and stop-batch command tooltips must be readable in enabled, disabled-by-mode, disabled-by-busy, and batch-running states. Keep this state and wording in `WpfWorkflowCommandStateService`; shell code-behind may only fan out enabled state and tooltip values to controls. Do not change runtime readiness decisions or actual detection execution for wording-only fixes. Covered by isolated test build, `--wpf-workflow-command-state`, `--wpf-labeling-shell`, `--mvvm-infra`, source artifact search, and 1920x1080 YOLO model visual smoke `artifacts\ui\wpf-workflow-command-tooltips-after-1920.png`.

2026-07-03 YOLO runtime status readable contract: top/status-bar model runtime text must use readable Korean for path selection, inference-ready state, missing inspection model, pending model candidate, and current inspection model. Keep these shell-adapter status strings in `WpfLabelingShellWindow.YoloRuntimeStatus.cs`; runtime readiness and execution decisions must remain in the core validator/services. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-inference-status-presentation`, `--wpf-workflow-command-state`, source artifact search, and 1920x1080 YOLO model visual smoke `artifacts\ui\wpf-yolo-runtime-status-readable-after-1920.png`.

2026-07-03 Python model validator Korean error contract: `PythonModelSettingsValidator.Validate` must return operator-facing Korean messages for missing project folder, TCP client script, Python executable, YOLO weights, image folder, confidence, timeout, maximum candidates, and inference image size. Keep validation conditions unchanged and update tests instead of reintroducing English raw errors into UI-facing runtime summaries. Covered by isolated test build, `--python-model-settings-validator`, `--python-model-runtime-self-test`, `--python-model-runtime-connection`, and source search for the previous English phrases.

2026-07-03 runtime self-test actionable detail contract: missing model-runtime self-test rows must include the next operator action, not only `경로 미설정` or a raw missing path. Project/model/image directory rows should point to reconnecting the relevant folder, missing worker scripts should point to reconnecting the worker script, missing inspection weights should mention training completion or choosing a `.pt` file, and missing Ultralytics packages should point to the install action. Keep existence checks inside `PythonModelRuntimeSelfTestService` and do not run Python, pip, worker, or shell commands from this self-test path. Covered by isolated test build, `--python-model-runtime-self-test`, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, and 1920x1080 visual smoke `artifacts\ui\wpf-runtime-selftest-actionable-detail-after-1920.png`.

2026-07-03 YOLO requirements install status readable contract: legacy requirements-install command status in `WpfLabelingShellWindow.YoloEnvironmentRuntimeCommands.cs` must show readable Korean for skip and failure states, including `설치 건너뜀` and `설치 실패`. Keep install decision and package execution flow unchanged; only shell-adapter status text should change for wording fixes. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, and source artifact search.

2026-07-03 Ultralytics package result detail readable contract: recent package-result detail must start with an operator-facing `결과:` line and use Korean labels such as `종료 코드:` and `로그 요약:` instead of raw `ExitCode:`. Keep command line and exit code available in detail for troubleshooting, but do not make the raw developer label the primary UI text. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, and source/test search for `ExitCode:`.

2026-07-03 Python environment summary Korean contract: `PythonEnvironmentService` must return operator-facing Korean Summary text for missing packages, ready environment, requirements install success, missing requirements.txt, pip-list inspection failure, empty package list, invalid package name, Python process start failure, and command timeout. Do not reintroduce raw fixed English phrases such as `Missing Python packages`, `Python environment is ready`, `Python requirements installed successfully`, `Could not inspect installed Python packages`, or `Python command timed out` into UI-facing runtime summaries. Covered by isolated test build, `--python-environment-summaries`, `--wpf-yolo-model-settings-panel`, `--python-model-runtime-self-test`, and source search for the previous English phrases.

2026-07-03 runtime command failure Korean contract: runtime command failure text from `DetectionResultApplicationService`, `YoloDetectionWorkflowService`, and `YoloTrainingWorkflowService` must be operator-facing Korean for missing/empty inspection images, disconnected Python model client, uninitialized detection/training services, detection timeout, AI-candidate skip state, and training validation failure. Do not reintroduce raw English fixed messages such as `DetectImage was not sent because...`, `StartTraining was not sent...`, or `YOLO detection timed out after...` into Core source that can flow to `LastError`, status panels, or logs. Covered by isolated test build, `--runtime-command-failure-messages`, `--python-model-status-protocol`, `--wpf-workflow-command-state`, and source search for the previous English phrases.

2026-07-03 current inspection failure summary contract: single current-image inspection failure must surface the actionable failure reason from `YoloWorkerSmokeTestResult.Summary/Error/Errors` in the command status, top inference status, and log instead of showing elapsed time only. Keep the status chip compact by clipping long summaries, but reserve enough header width at 1920 and avoid hidden progress-bar columns consuming text width. Covered by isolated test build, `--wpf-single-detection-path`, `--runtime-command-failure-messages`, `--wpf-labeling-shell`, and 1920 visual smoke `artifacts\ui\wpf-current-inspection-failure-summary-after-1920.png`.

2026-07-03 batch inspection failure summary contract: batch inspection failure must surface the same actionable failure reason in the top inference status that already appears in batch command/log text. `WpfBatchDetectionProgressService.BuildFailureInferenceStatus` owns the compact status wording and clips long summaries; shell code-behind should only pass the captured failure summary into that service. Do not change batch worker execution, TCP packet flow, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this status-only contract. Covered by isolated test build, `--wpf-batch-detection-progress`, `--wpf-single-detection-path`, `--wpf-labeling-shell`, and 1920 visual smoke `artifacts\ui\wpf-batch-inspection-failure-summary-after-1920.png`.

2026-07-03 batch failure result detail contract: batch failure details shown in image queue rows/tooltips and canvas failure result cards must use the same operator-facing translated reason text as status summaries, such as `요청 실패`, instead of exposing fixed raw English messages like `Detection request failed.`. Keep translation in `WpfImageQueuePresenter.TranslateDetectionMessage` and failure-card wording in `WpfDetectionResultPresentationService`; shell code-behind should only pass worker summaries into these services. Covered by isolated test build, `--wpf-batch-detection-result`, `--wpf-detection-result-presentation`, and `--wpf-image-queue-status`.

2026-07-03 canvas result card visibility contract: the canvas result card must not rely on WPF-over-`WindowsFormsHost` z-ordering because the OpenGL canvas uses a WinForms host. Keep `DetectionResultOverlay` in a separate Auto row directly above the star-sized canvas row, and keep candidate action buttons visible only for `Confirmable` or `Duplicate` result states through `WpfCanvasPanelViewModel.DetectionOverlayActionsVisibility`. Do not modify Viewer/OpenGL/ROI/brush/eraser rendering paths to solve this UI card visibility issue. Covered by isolated test build, `--wpf-canvas-detection-overlay`, `--wpf-batch-detection-result`, `--wpf-detection-display-mode`, `--wpf-labeling-shell`, 1366 responsive-layout verification, and visual smokes `artifacts\ui\wpf-batch-failure-result-card-after-1920.png` plus `artifacts\ui\wpf-batch-failure-result-card-after-1366.png`.

2026-07-03 inspection model runtime chip contract: top inference status and the dedicated inspection-model chip must show the selected runtime family beside the weights file, for example `검사 모델: YOLOv5 / best.pt`, so operators can tell which adapter/model profile is active without opening the model settings panel. Keep this wording in `WpfInferenceStatusPresentationService`; shell code-behind should only pass the service result into the status ViewModel. Tooltip text must keep the full model path and runtime family. Do not change detection execution, TCP packet routing, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only contract. Covered by isolated test build, `--wpf-inference-status-presentation`, `--wpf-labeling-shell`, `--wpf-status-panels`, and 1366 visual smoke `artifacts\ui\wpf-inspection-model-runtime-chip-after-1366.png`.

2026-07-03 DetectImage adapter-key contract: file-path based current/selected image inspection must keep the selected protocol model key all the way into the TCP `DetectImage` JSON request, such as `"model":"yolo11"`. `DetectionResultApplicationService` should continue deriving this from `PythonModelSettings.GetProtocolModelName()` when calling `CCommunicationLearning.SendDetectImage`; `LearningProtocol.BuildDetectImagePacket` should continue serializing that value as `model`. WPF may guard unsupported runtimes before execution, but the Core packet path must not silently fall back to `yolov5` after the user selected another runtime. Covered by isolated test build and `--yolo-detection-workflow-validation`, which uses a mock TCP client to read the actual `DetectImage` JSON line.

2026-07-03 detection result model-source summary contract: single/current image inspection result summaries and logs must include the runtime/model source, such as `YOLOv5 / exp7\best.pt` or `YOLO11 / yolo11n.pt`, before the candidate count. Keep the reusable label in `WpfInferenceStatusPresentationService.BuildRuntimeModelLabel`; `RunWorkerDetectionForImageAsync` may attach that label to start logs, success summary, Python status, and elapsed-time logs. Do not change TCP request routing, worker execution, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only result traceability work. Covered by isolated test build, `--wpf-single-detection-path`, `--wpf-inference-status-presentation`, `--yolo-detection-workflow-validation`, `--wpf-labeling-shell`, and 1366 responsive-layout verification.

2026-07-03 batch detection model-source log contract: batch inspection logs must include the same runtime/model source used by single inspection summaries. `WpfBatchDetectionProgressService` should own the wording for start, per-item completed/failed, and final completion logs, accepting an optional model-source label; `RunBatchDetectionAsync` should pass the label from `WpfInferenceStatusPresentationService.BuildRuntimeModelLabel`. Do not change batch execution, TCP request routing, queue review status persistence, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this log traceability work. Covered by isolated test build, `--wpf-batch-detection-progress`, `--wpf-single-detection-path`, `--wpf-inference-status-presentation`, `--yolo-detection-workflow-validation`, `--wpf-labeling-shell`, and 1366 responsive-layout verification.

2026-07-03 candidate review model-comparison source contract: Candidate Review's trained-model validation card must show the compared model source in the card itself, using a `현재 검사 ... -> 학습 후보 ...` line that includes runtime family and model file display names. Keep the text generation in `WpfInferenceStatusPresentationService.BuildModelComparisonSourceText`; `WpfCandidateReviewPanelViewModel.ModelComparisonSourceText` owns the panel state, and shell code-behind should only pass the current/latest weights from `WpfTrainingWeightsComparison`. Do not change model-comparison execution, detection execution, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only traceability work. Covered by isolated test build, `--wpf-candidate-review-panel`, `--wpf-inference-status-presentation`, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, and visual smoke `artifacts\ui\wpf-candidate-model-source-after-1920.png`. Before capture: `artifacts\ui\wpf-candidate-review-compact-after-1920.png`.

2026-07-03 model-candidate rejection consistency contract: after rejecting a staged trained model candidate, the top inspection-model badge and model-center current model must return to the baseline inspection model, and Candidate Review must show the candidate decision as rejected with save/reject actions disabled. Keep rejection execution in `ExecuteRejectModelCandidateCommand`, keep the model-registry decision record through `ModelRegistryService.RecordCandidateDecision`, and protect the UI route with `--wpf-yolo-training-session-smoke --model-center --reject-model-candidate`. Do not change training execution, detection execution, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this consistency smoke. Capture: `artifacts\ui\wpf-model-reject-consistency-after-1920.png`.

2026-07-03 model-center duplicate action visibility contract: when the training/model center is the active right-workflow view, the top `WorkflowStageModelActionPanel` must remain collapsed and the lifecycle area's duplicate review/save/inspect buttons must stay hidden so model actions are concentrated in the dedicated model-center priority card. Keep the top-panel visibility decision in `WpfLabelingShellViewModel.IsWorkflowStageModelActionPanelVisible`; XAML should bind to that state and not recreate the condition with local triggers. Do not remove the underlying review/save/inspect commands, and do not change training, model comparison, detection, candidate overlay, or Viewer/OpenGL/ROI/brush/eraser execution paths for this presentation cleanup. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080`, and `--wpf-responsive-layout --width 1920 --height 1080`. Before capture: `artifacts\ui\wpf-model-reject-consistency-after-1920.png`; after capture: `artifacts\ui\wpf-model-center-single-action-zone-after-1920.png`.

2026-07-03 model-history comparison role-label contract: the selected model-history comparison area must label the two sides as current inspection model and selected history model before showing their bound model summary text, and it must appear before the dense model-history list so short 1366x768 model-center panels expose the comparison before lower detail text. Keep the comparison state in `WpfLabelingShellViewModel.SelectedModelHistory*`; use `선택 이력` wording consistently for history-row comparison, not the more ambiguous `선택 모델`. XAML may add role labels and adjust list height but must not create nested cards inside `ModelRegistrySelectedHistoryPanel`. Do not change model registry persistence, model promotion, training, detection, candidate overlay, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only comparison clarity work. Covered by isolated test build, `--wpf-labeling-shell`, `--model-registry`, responsive-layout checks at 1366x768 and 1920x1080, plus model-center visual smokes `artifacts\ui\wpf-model-history-selected-history-wording-after-1366.png` and `artifacts\ui\wpf-model-history-selected-history-wording-after-1920.png`. Before capture: `artifacts\ui\wpf-model-center-single-action-zone-after-1920.png`.

2026-07-03 dataset context default text encoding contract: first-run dataset context text must show readable Korean before any dataset is opened. `WpfLabelingShellViewModel.CurrentDatasetStoragePathText` should include the storage/recipe label, and `CurrentDatasetImageRootText` should include the original image-folder label. Keep the defaults in the ViewModel and protect them with `--wpf-labeling-shell`; do not move this first-run state into XAML or shell code-behind. Do not change dataset loading, label persistence, Viewer/OpenGL/ROI/brush/eraser paths for this encoding guard.

2026-07-03 model file display-name regression contract: model-center registry rows, compact model summaries, and top inference runtime/model labels must preserve the training run folder for YOLO training outputs, for example `exp7\best.pt`, instead of collapsing every trained model to only `best.pt`. Keep the shared shortening rule in `WpfTrainingWeightsService.FormatWeightsDisplayPath` and guard it through `--wpf-labeling-shell`; do not duplicate path formatting in XAML or shell code-behind. Do not change training execution, inference execution, candidate overlay, Viewer/OpenGL/ROI/brush/eraser paths for this display-name guard.

2026-07-03 readable test-string contract: WPF/Core/Runtime-facing regression tests must not expect or ban mojibake fragments such as replacement characters, Hanja-range corrupt Korean, or legacy broken strings. When a test protects UI wording, use the readable current operator terms, for example `현재 라벨`, `중복 가능`, `크기`, `위치`, `일괄 검사 항목 완료`, and `데이터셋`. Covered by UTF-8 source scan for `\uFFFD`/Hanja-range artifacts, isolated test build, `--wpf-labeling-shell`, `--wpf-candidate-review-panel`, and `--wpf-batch-detection-progress`. Do not change Viewer/OpenGL/ROI/brush/eraser paths for test-string cleanup.

2026-07-03 public tutorial documentation contract: public README/tutorial documents must not contain private local paths, specific local test dataset names, conversation traces, personal-only context, or temporary run-artifact paths such as `C:\`, `D:\`, `LabelingData`, `Test01`, `TEST_`, `artifacts\run`, `AppData`, `Codex`, `포트폴리오`, `제가`, `내가`, `저만`, `당신`, `소통`, `이번 확인`, or `사용한 데이터`. The normal tutorial HTML should keep the annotated screenshot walkthrough through `src="images/annotated/..."`, every linked capture should be an existing `*-annotated.png` file with `번호와 화살표` alt text, and the standalone HTML must embed the same screenshot count through `src="data:image..."` without local image-file references. Covered by isolated test build and `--priority-workflow-docs`.

2026-07-03 current tutorial screenshot contract: README and tutorial captures must reflect the current WPF layout: left workflow/task panel, center canvas, and right image queue. Do not reintroduce older public-doc wording that describes the image queue as left-side or the workflow task panel as right-side. When screenshots are refreshed, update both raw `docs\tutorial\images\*.png` captures and `docs\tutorial\images\annotated\*-annotated.png` callout images, then regenerate the standalone HTML so it embeds the same 14 annotated images. Covered by `--priority-workflow-docs`, `git diff --check`, image reference count checks, and visual review of annotated captures 01/02/03/05/09/12/14.

2026-07-03 README first-run entry contract: the public README should give a short first-run path that matches the current WPF layout: select `1 데이터셋`, use the left workflow/task panel, check the right image queue, label on the center canvas, and review `4 학습/모델` before treating trained weights as an inspection model. Keep the normal tutorial and standalone tutorial links visible near the top-level screenshot. Public README/tutorial text must still pass the existing no-local-path/no-conversation-trace checks. Covered by `--priority-workflow-docs`, public-doc forbidden-text search, and `git diff --check`.

2026-07-03 README/tutorial current-UI recheck contract: the public README and tutorial should not show the pre-docking layout after the WPF workbench has moved to left task panel, center canvas, and right image queue. The legacy tutorial image files `docs\tutorial\images\01-guide.png` through `06-inference-review.png` are also refreshed as compatibility assets so stale UI does not reappear if an older link or test fixture reads them. Standalone HTML must be regenerated from the same current annotated images after every screenshot refresh. Covered by `--priority-workflow-docs`, public-doc forbidden-text search, image reference count checks, standalone embedded-image count checks, `git diff --check`, and visual review of annotated captures 01/03/09/12/14.

2026-07-03 public README audience contract: `README.md` is a public product/readme page, not an internal coordination checklist. Do not include commit rules, `git status` instructions, Codex handoff notes, work-tracking links, stable-verification links, or private collaboration rules in the README. Keep those in `CODEX_NEXT_PROMPT.md`, `docs\WORK_TRACKING.md`, or `docs\STABLE_VERIFIED_AREAS.md`. The README hero image should use a fresh filename when refreshed so rendered previews do not keep showing stale cached UI. Covered by README forbidden-text search and direct visual review of `docs\tutorial\images\annotated\readme-current-workflow-20260703.png`.

2026-07-03 dataset image-root resolver contract: dataset switching should prefer an explicitly configured operator image folder, but fall back to the dataset-owned train/valid/test image folders when the configured root is missing or only the implicit default. Keep this decision in `WpfDatasetImageRootResolver`; `WpfLabelingShellWindow` should only pass the current `CData` and queue-image existence adapter, then load the returned folder. Do not move this logic back into shell code-behind or change label persistence, image loading, Viewer/OpenGL/ROI/brush/eraser paths for this routing contract. Covered by isolated test build, `--wpf-image-queue-selection-service`, and `--wpf-labeling-shell`.

2026-07-03 image queue search single-match contract: image queue search/open fallback should resolve a row only when the current search text and filter produce exactly one queue item. Keep this ambiguity policy in `WpfImageQueueFilterService.FindSingleSearchMatch`, use `CountSearchMatches` for open-failure diagnostics, and keep the operator-facing selected-open failure wording in `WpfImageQueuePresenter.BuildOpenSelectionFailureMessage` so shell code-behind does not duplicate filter/search or presentation rules. Shell code may still refresh the current `ICollectionView`, select the returned row, scroll it into view, and update the open-button state as UI adapter work. Do not change image loading, saved-label lookup, Viewer/OpenGL/ROI/brush/eraser paths for this queue-selection contract. Covered by isolated test build, `--wpf-image-queue-status`, `--wpf-image-queue-click-load-path`, and `--wpf-image-queue-selection-service`.

2026-07-03 image queue open-selection resolution contract: selected queue open should resolve candidate priority and the actual openable image path together through `WpfImageQueueSelectionService.ResolveOpenSelection`. This keeps saved split-image fallback and open-candidate priority in one service call instead of repeatedly calling path resolution from shell code-behind. Shell code may still collect UI candidates in order from DataGrid selection, ViewModel selection, unique search match, and unique visible row, then hand them to the service. Do not change image loading, saved-label lookup, Viewer/OpenGL/ROI/brush/eraser paths for this queue-selection contract. Covered by isolated test build, `--wpf-image-queue-selection-service`, `--wpf-image-queue-click-load-path`, and `--wpf-image-queue-status`.

2026-07-03 dataset setup path service contract: dataset creation should keep recipe-name selection, dataset output-root suffixing, existing-output-root collision detection, and recipe-config output-root reading in `WpfDatasetSetupPathService`. `WpfLabelingShellWindow` may open the wizard, pass current panel/current recipe state into the service, and apply the accepted request, but it should not scan recipe configs or generate unique recipe names directly. Do not change dataset label persistence, image queue loading, Viewer/OpenGL/ROI/brush/eraser paths for this dataset setup routing contract. Covered by isolated test build, `--wpf-dataset-setup-ui`, `--wpf-dataset-setup-request`, and `--wpf-labeling-shell`.

2026-07-03 dataset setup data materialization contract: dataset creation should materialize the accepted output root and normalized class catalog through `WpfDatasetSetupDataService.ApplyOutputRootAndClasses`. The service owns class-name normalization, duplicate class removal, default `Defect` fallback, output-root configuration, and creation of YOLO output directories. `WpfLabelingShellWindow` may still set the active recipe/purpose, apply optional sample presets, persist config/YAML, and refresh panels, but it should not clear or rebuild `ClassNamedList` inline inside `ApplyDatasetSetupRequest`. Do not change label persistence, image queue loading, Viewer/OpenGL/ROI/brush/eraser paths for this dataset setup materialization contract. Covered by isolated test build, `--wpf-dataset-setup-ui`, `--wpf-dataset-setup-request`, and `--wpf-labeling-shell`.

2026-07-03 dataset setup presentation contract: dataset creation operator-facing status text should remain in `WpfDatasetSetupPresentationService`, including invalid recipe-name guidance, duplicate output-root guidance, sample preset failure fallback, ready status, dataset-ready status, and creation log formatting. `WpfLabelingShellWindow.ApplyDatasetSetupRequest` may route status/log strings to the UI, but should not rebuild these messages inline. Do not change label persistence, image queue loading, Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only contract. Covered by isolated test build, `--wpf-dataset-setup-ui`, `--wpf-dataset-setup-request`, `--wpf-labeling-shell`, and `git diff --check`.

2026-07-03 dataset switch/open-folder presentation contract: dataset switch/open-folder operator-facing text should remain in `WpfDatasetSetupPresentationService`, including missing image-root status/log text, missing output-root status/log text, and open-dataset-folder failure status/log text. `WpfLabelingShellWindow` may still clear the queue, set dataset status, append logs, focus the onboarding tab, and launch the folder as UI adapter work, but should not rebuild these messages inline. Do not change dataset switching, image queue clearing, folder launch, label persistence, Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only contract. Covered by isolated test build, `--wpf-dataset-setup-ui`, `--wpf-dataset-setup-request`, `--wpf-labeling-shell`, and `git diff --check`.

2026-07-03 dataset context name/purpose presentation contract: current dataset header display-name fallback and dataset-purpose display wording should remain in `WpfDatasetContextPresentationService`. `WpfLabelingShellWindow.RefreshShellDatasetContext` may collect recipe name, output root, image root, and class count, but should not rebuild dataset-name fallback or purpose labels inline. Do not change dataset loading, label persistence, image queue loading, Viewer/OpenGL/ROI/brush/eraser paths for this presentation-only contract. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-dataset-setup-ui`, `--wpf-dataset-setup-request`, and `git diff --check`.

2026-07-03 runtime profile capability-row contract: YOLO/model settings runtime profile rows must show each profile's support scope through `PythonModelRuntimeProfile.CapabilityText`, bound in `WpfYoloModelSettingsPanel` as `RuntimeProfileCapabilityText`. YOLOv5 should show training plus current inspection support, YOLOv8/YOLO11 should show current-inspection-first support with training requiring a worker connection, and ONNX should show inference-only intent. Keep this as profile presentation state; do not change worker execution, TCP routing, training, detection, candidate overlay, or Viewer/OpenGL/ROI/brush/eraser paths for this display row. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920 responsive-layout verification, and visual smoke `artifacts\ui\wpf-runtime-profile-capability-after-1920.png`.

2026-07-03 viewer context-menu file dialog compile contract: `Library/CViewer.cs` no longer depends on removed `CUtil.LoadImageFilePath` or `CUtil.SaveImageFilePath` helpers for the context-menu image open/save actions. Keep those menu actions on local WinForms file dialogs unless the shared utility API is restored deliberately. This is a compile-compatibility fix only; do not change Viewer/OpenGL/ROI/brush/eraser interaction or rendering paths for file-dialog work. Covered by isolated test build and `git diff --check`.

2026-07-03 interactive current-inspection presentation contract: single current-image inspection preparing, completion, failure, and completion-log wording should be built through `WpfInferenceStatusPresentationService.BuildInteractive*` methods. `WpfLabelingShellWindow.RunInteractiveDetectionAsync` may resolve the target image, execute the worker/smoke path, compute elapsed time, and pass the formatted execution path, but it should not reintroduce local `failureSummary` formatting or inline command/top-status/log message templates. Do not change worker execution, TCP routing, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-single-detection-path`, `--wpf-inference-status-presentation`, `--wpf-labeling-shell`, `--mvvm-infra`, and `git diff --check`.

2026-07-03 worker current-inspection presentation contract: `RunWorkerDetectionForImageAsync` should not inline operator-facing worker status templates for missing/loading images, preparation, connection failure, request state, timeout, success summary, elapsed log, or cancellation. Keep those strings in `WpfInferenceStatusPresentationService.BuildWorker*` methods while the shell passes image path, model source, elapsed text, and candidate count. Do not change worker startup, TCP routing, detection candidate application, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-single-detection-path`, `--wpf-inference-status-presentation`, `--wpf-labeling-shell`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 license attribution contract: MIT licensing should remain explicit in `LICENSE`, public README, `NOTICE`, package metadata, and core AssemblyInfo metadata. Commercial use is allowed, but copied or redistributed versions should retain the copyright notice, license text, NOTICE file, and project/package metadata attribution for `최노아 (Noah-Choi)`. Keep attribution metadata in `Directory.Build.props` and keep short SPDX headers in the core AssemblyInfo files when those files are touched. Covered by direct attribution search, Debug x64 build, and `git diff --check`.

2026-07-03 product naming contract: public/product-facing project identity should be `OpenVisionLab Labeling Studio`; build and runtime artifact identity should be `OpenVisionLab.LabelingStudio`. Keep the app project file named `OpenVisionLab.LabelingStudio.csproj`, solution project display name `OpenVisionLab.LabelingStudio`, output files `OpenVisionLab.LabelingStudio.exe/.dll`, and runtime config/script/test references aligned. Do not treat the remaining `MvcVisionSystem` namespace as public product identity; namespace rename is a separate high-blast-radius XAML/partial-class migration. Covered by app project build, test project build, solution build, focused WPF gates, generated output check, and `git diff --check`.

2026-07-03 code-structure product-name guard contract: `docs/CODE_STRUCTURE.md` should introduce the codebase as `OpenVisionLab Labeling Studio`, not the repository folder name `Labelling_Application`. Keep the `OpenVisionLab.LabelingStudio.csproj` `RootNamespace` comment explaining why the legacy `MvcVisionSystem` namespace remains until a separate XAML partial-class migration. Guard this with `--priority-workflow-docs`; do not perform a broad namespace rename as part of small product-naming cleanup work.

2026-07-03 template auto-label presentation contract: template registration, current-image template apply status, batch template auto-save start/progress/completion status, and the reusable guide body should be generated by `WpfTemplateMatchingAutoLabelPresentationService`. `WpfTemplateMatchingAutoLabelViewModel` may still own template registration workflow, batch loop orchestration, and host calls, but it should not inline the long batch status templates. Do not change template matching score calculation, batch save policy, image queue filtering, label persistence, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--template-guide-ux`, `--template-batch-autolabel-storage`, `--wpf-template-current-image-no-candidate`, `--wpf-labeling-shell`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 repository metadata URL contract: package and repository metadata should point to `https://github.com/Noah8218/OpenVisionLab-Labeling-Studio` after the GitHub repository rename. Keep local `origin` on the new `OpenVisionLab-Labeling-Studio.git` URL so pushes do not rely on GitHub redirect compatibility. Historical verification documents may still mention local workspace paths, but public/package metadata should not point to the old `Noah8218/Labelling_Application` repository.

2026-07-03 training command recovery presentation contract: training start/stop command status and recovery title/detail/action text should be generated by `WpfTrainingCommandPresentationService`. `WpfLabelingShellWindow.ExecuteStartTrainingCommand` and `ExecuteStopTrainingCommand` may still execute the workflow and set UI adapter status, but each command should apply `SetYoloRecoveryStatus` only once from a `WpfTrainingRecoveryStatus` DTO in `finally`. Do not change training packet construction, Python worker connection, training polling, model registry, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-labeling-shell`, `--mvvm-infra`, model-center training smokes at 1366x768 and 1920x1080, visual captures `artifacts\ui\wpf-training-command-presentation-after-1366.png` and `artifacts\ui\wpf-training-command-presentation-after-1920.png`, and `git diff --check`.

2026-07-03 model candidate decision presentation contract: Candidate Review model-candidate decision status/detail/tooltips and model-candidate reject command result text should be generated by `WpfModelCandidateDecisionPresentationService`. `WpfLabelingShellWindow.ModelCandidateDecisionCommands` may still restore the baseline weights, record the registry decision, refresh the model center, and adapt the presentation DTO into `CandidateReviewViewModel`, but it should not inline pending/rejected/saved/review/no-candidate operator wording. Do not change training execution, model registry persistence, inference execution, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-candidate-review-panel`, `--model-registry`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 YOLO environment command presentation contract: model-runtime first-check and legacy requirements-install command status/log wording should be generated by `WpfYoloEnvironmentCommandPresentationService`. `WpfLabelingShellWindow.YoloEnvironmentRuntimeCommands` may still query runtime state, run Python environment checks/installs, refresh the model settings panel, and append already-built detail lines, but it should not inline the check-ready/check-needed/requirements-ready/missing-package install wording. `BeginYoloEnvironmentCommand` should also use the same service for duplicate-command busy text. Do not change Python package execution, worker startup, TCP routing, training, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--runtime-command-failure-messages`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 Ultralytics package operation presentation contract: Ultralytics install/uninstall availability, cancel/running/result statuses, confirmation dialog text, and recent package-operation detail text should be generated by `WpfYoloEnvironmentCommandPresentationService`. `WpfLabelingShellWindow.YoloEnvironmentRuntimeCommands` may still build the install plan, call `WpfMessageDialog.Confirm`, run `PythonEnvironmentService.InstallPackageAsync` or `UninstallPackageAsync`, append stdout/stderr tails, and push the final strings into the settings ViewModel, but it should not inline confirmation body text or `결과/대상/명령/종료 코드/로그 요약` labels. Do not change package execution, venv selection, worker startup, TCP routing, training, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--runtime-command-failure-messages`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 model runtime test/restart/stop presentation contract: model-test mode-switch/start/completed/failure text, model-test failure recovery, inference-worker restart status/recovery text, and inference-worker stop status text should be generated by `WpfYoloEnvironmentCommandPresentationService` and `WpfYoloEnvironmentRecoveryPresentation`. `WpfLabelingShellWindow.YoloEnvironmentRuntimeCommands` may still set inference mode, run the model test, restart/stop the Python model client, refresh panels, append logs, and adapt the returned presentation into `SetYoloCommandStatus`/`SetYoloRecoveryStatus`, but it should not inline those operator-facing command/recovery strings. Do not change model-test execution, Python worker restart/stop, TCP routing, training, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-yolo-model-settings-panel`, `--runtime-command-failure-messages`, `--python-model-runtime-connection`, `--python-model-runtime-self-test`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 git push policy contract: do not run `git push` unless the user explicitly asks for `push`. A `commit` request means create a local commit only; pushing requires a separate explicit request. Keep this rule in `CODEX_NEXT_PROMPT.md` and do not move internal collaboration rules into the public README/tutorial. Covered by direct document update and commit-only closeout.

2026-07-03 training progress presentation contract: training progress summaries, epoch summaries, state/message translations, no-response status/recovery, failed-training recovery guidance, and first-batch CPU-size recommendation text should be generated by `WpfTrainingProgressPresentationService`. `WpfLabelingShellWindow.TrainingProgressStatus` may still poll `PythonCommunicationStatus`, update progress values, update brushes, start/stop polling, and adapt service-built text into training/status/recovery UI, but it should not inline those operator-facing status/recovery strings. Do not change training packet construction, Python worker polling, model registry, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-training-status-summaries`, `--wpf-labeling-shell`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 model runtime unavailable presentation contract: unavailable model-runtime UI wording should be generated by `WpfModelRuntimeUnavailablePresentationService` and carried through `WpfModelRuntimeUnavailablePresentation`. `WpfLabelingShellWindow.YoloRuntimeStatus` may still read runtime state, update existing UI status methods, and adapt the DTO into shell/training/model-center ViewModels, but it should not inline unavailable-runtime current-model, inspection-status, model-status, recovery, readiness, registry, or candidate-review text. Do not change runtime validation, worker startup, TCP routing, training, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-training-status-summaries`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 runtime-ready inference status presentation contract: runtime-ready Python status and missing inspection-model status/tooltip text from `RefreshYoloStatus` should be generated by `WpfInferenceStatusPresentationService`, including `BuildRuntimePythonStatus`, `BuildInspectionModelStatusText`, and `BuildInspectionModelToolTip`. `WpfLabelingShellWindow.YoloRuntimeStatus` may still read runtime state, validate settings, call existing UI status adapters, and apply service-built text, but it should not inline `추론: 준비 완료` or `검사 모델: 없음` wording. Do not change runtime validation, worker startup, TCP routing, training, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-inference-status-presentation`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 YOLO settings status detail presentation contract: YOLO/model settings detail text for executable/project/script/model/image paths, confidence/timeout, validation warnings/errors, package checks, and worker/model/training status should be generated by `WpfYoloSettingsPanelStatusPresentationService`. `WpfLabelingShellWindow.YoloSettingsPanelStatus` may still run `PythonEnvironmentService.CheckRequirementsAsync`, read runtime/communication/process state, and apply the returned detail text to `YoloStatusViewModel` or fallback UI elements, but it should not rebuild operator-facing detail lines with inline `detail.AppendLine(...)` or `AppendPythonWorkerStatus`. Do not change package checking, worker startup, TCP routing, training, detection, candidate overlay rendering, or Viewer/OpenGL/ROI/brush/eraser paths for this presentation-boundary work. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 product completeness audit contract: `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md` is the current self-evaluation baseline against CVAT, Label Studio, Roboflow, and Labelbox. Treat object-detection MVP, queue/save/reopen, empty-normal completion, candidate review, model center, verified viewer/ROI/brush/eraser paths, and current runtime/status presentation boundaries as complete unless a focused gate fails or a specific defect is reproduced. The next product-development gaps are export/import interoperability and anomaly image-level normal/abnormal workflow; do not reframe completed local-object-detection work as unfinished general labeling-platform work. Covered by isolated test build, `--priority-workflow-docs`, `--wpf-labeling-shell`, `--wpf-inference-status-presentation`, `--wpf-yolo-model-settings-panel`, `--mvvm-infra`, `git diff --check`, and a trailing-whitespace check for the new audit document.

2026-07-03 COCO detection export contract: `CocoDetectionExportService` is the first export/import interoperability slice. It should remain a non-UI service that reads existing YOLO box dataset artifacts from `data/<split>/images` and `labels`, includes empty-label images in COCO `images`, skips invalid label lines, maps YOLO class index `0` to COCO category id `1`, and writes relative image paths. Do not change annotation save/reopen, queue completion, candidate review, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build and `--coco-detection-export`.

2026-07-03 anomaly image-level review-status contract: `AnomalyImageReviewStatusService` owns the first anomaly workflow data contract. It should persist reviewed `Normal` and `Abnormal` image states to `anomaly-review-status.json`, restore missing entries as `Unreviewed`, provide next-unreviewed selection for the normal-completion loop, and keep anomaly manifest summaries on `image-level-normal-abnormal` counts. This is not the complete visible anomaly workflow; dashboard distribution, runtime, and model workflow still need separate gates. Do not touch Viewer/OpenGL/ROI/brush/eraser paths for anomaly state persistence work. Covered by isolated test build and `--wpf-anomaly-purpose-flow`.

2026-07-03 WPF anomaly normal-completion routing contract: when the dataset purpose is `AnomalyDetection`, `ExecuteCompleteNoObjectAndNextCommand` should keep the existing empty YOLO label compatibility file, mark the active anomaly image as `Normal`, persist `anomaly-review-status.json`, and choose the next image using `AnomalyImageReviewStatusService.TryFindNextUnreviewed`. Saving labels should mark the active anomaly image as `Abnormal`; detection candidate/skipped status updates should not change image-level anomaly state. Keep this as shell adapter wiring and do not change Viewer/OpenGL/ROI/brush/eraser interaction paths. Covered by isolated test build and `--wpf-anomaly-purpose-flow`.

2026-07-03 anomaly dashboard distribution contract: `WpfAnomalyDashboardPresentationService` should own the normal/abnormal/unreviewed dashboard metric and anomaly next-action issue text. `WpfLabelingShellWindow.TrainingGuideStatus` may load `AnomalyImageReviewStatusService` summary and pass it into the existing dashboard builder, but it should not inline the anomaly distribution wording. This is dashboard/status presentation only; runtime/model workflow and backend selection remain separate. Covered by isolated test build and `--wpf-anomaly-purpose-flow`.

2026-07-03 export capability inventory contract: `DatasetExportCapabilityService` should be the code-owned inventory for dataset export/import targets. It must keep the verified YOLO, COCO, Pascal VOC, Label Studio, and CVAT detection/segmentation export/import slices marked implemented through `cvat-segmentation-import`, with each implemented item carrying its matching focused verification switch. It should expose exactly one recommended next interoperability target, currently `labelbox-ndjson-detection-import`, and must not mark planned formats implemented until an importer/exporter and focused gate exist. Covered by isolated test build and `--export-capability-inventory`.

2026-07-03 Pascal VOC detection export contract: `PascalVocDetectionExportService` should remain a non-UI export service that reads existing YOLO box dataset artifacts from `data/<split>/images` and `labels`, writes one XML file per image under the requested output directory, includes empty-label images with no `<object>` entries, skips invalid label lines/classes, writes relative image paths, and converts saved pixel rectangles into 1-based inclusive Pascal VOC `bndbox` coordinates. Do not change annotation save/reopen, queue completion, candidate review, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build and `--pascal-voc-detection-export`.

2026-07-03 Label Studio detection JSON export contract: `LabelStudioDetectionExportService` should remain a non-UI export service that reads existing YOLO box dataset artifacts from `data/<split>/images` and `labels`, writes raw Label Studio task JSON, stores image paths in `data.image`, emits `RectangleLabels` results with `from_name` `bbox`, `to_name` `image`, `type` `rectanglelabels`, `original_width`, `original_height`, `image_rotation`, and `value.x/y/width/height` as percentages of the image dimensions, includes reviewed empty-label images as annotations with an empty `result` array, and skips invalid label lines/classes. Do not change annotation save/reopen, queue completion, candidate review, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build and `--label-studio-detection-export`.

2026-07-03 CVAT image task archive export contract: `CvatImageTaskArchiveExportService` should remain a non-UI export service that reads existing YOLO box dataset artifacts from `data/<split>/images` and `labels`, writes a CVAT for images 1.1 zip archive with root `annotations.xml` plus image files under `images/<split>/`, emits `<image>` entries with width/height and relative split names, writes bounding boxes as `<box label xtl ytl xbr ybr occluded="0" z_order="0">`, includes empty-label images with no `<box>` entries, and skips invalid label lines/classes. Do not change annotation save/reopen, queue completion, candidate review, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build and `--cvat-image-export`.

2026-07-03 dataset quality audit contract: `YoloDatasetQualityAuditService` should remain a non-UI dataset artifact report service that reads existing YOLO output folders and returns split-level image counts, label-file counts, missing label counts, reviewed empty-label counts, invalid label line/class counts, object counts, and class distribution. It should treat empty label files as reviewed empty-normal images, missing label files as unfinished images, and invalid non-empty label lines as quality problems without counting them as objects. Do not change annotation save/reopen, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for audit-report work. Covered by isolated test build and `--dataset-quality-audit`.

2026-07-03 WPF dataset quality dashboard contract: WPF guide/dashboard quality-audit wording and metric construction should be generated by `WpfDatasetQualityAuditPresentationService` from `YoloDatasetQualityAuditReport`. `WpfLabelingShellWindow.TrainingGuideStatus` may load the audit report and pass it into the existing dashboard builder, but it should not inline the quality metric/issue wording. The dashboard should show missing-label and invalid-label problems as a `품질` problem card while keeping duplicate split, model replacement, and labeling-progress metrics separate. Do not change annotation save/reopen, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for dashboard audit work. Covered by isolated test build, `--wpf-training-dashboard-quality`, `--wpf-model-comparison-heldout`, `--dataset-quality-audit`, and 1920x1080 visual smoke capture `artifacts\ui\wpf-dataset-quality-dashboard-after-1920.png`.

2026-07-03 dataset quality audit Markdown export contract, updated 2026-07-15: `YoloDatasetQualityAuditExportService` remains a non-UI export service that takes an existing `YoloDatasetQualityAuditReport` and writes a standalone Markdown report with total images, label files, missing labels, empty labels, invalid label lines, objects, a split table, and a class distribution table. The WPF `품질 보고서` dashboard card may call this service and save the default `dataset-quality-audit.md` under the active dataset root; it must report success/failure through the existing shell status/log path and must not change dataset artifacts other than that report. Do not change annotation save/reopen, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for audit export work. Covered by the isolated test build, `--dataset-quality-audit-export`, `--wpf-training-dashboard-quality`, `--wpf-labeling-shell`, and 1920x1080 evidence under `artifacts\ui\20260715-dataset-quality-report-action`.

2026-07-03 COCO segmentation export contract: `CocoSegmentationExportService` should remain a non-UI export service that reads existing segmentation artifacts from `data/<split>/segments` beside images under `data/<split>/images`, writes COCO-style `images`, `categories`, and polygon `annotations`, includes empty-label images, uses relative image paths, maps local class index `0` to category id `1`, and skips invalid segment records. This exporter consumes saved polygon JSON artifacts; do not change annotation save/reopen, mask/brush/eraser materialization, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build, `--coco-segmentation-export`, and `--export-capability-inventory`.

2026-07-03 Label Studio segmentation JSON export contract: `LabelStudioSegmentationExportService` should remain a non-UI export service that reads existing segmentation artifacts from `data/<split>/segments` beside images under `data/<split>/images`, writes raw Label Studio task JSON, stores image paths in `data.image`, emits `PolygonLabels` results with `from_name` `polygon`, `to_name` `image`, `type` `polygonlabels`, `original_width`, `original_height`, `image_rotation`, and percent-based `value.points`, includes images without segment files as tasks without reviewed annotations, and skips invalid segment records. Do not change annotation save/reopen, mask/brush/eraser materialization, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build, `--label-studio-segmentation-export`, and `--export-capability-inventory`.

2026-07-03 CVAT segmentation archive export contract: `CvatSegmentationArchiveExportService` should remain a non-UI export service that reads existing segmentation artifacts from `data/<split>/segments` beside images under `data/<split>/images`, writes a CVAT for images 1.1 zip archive with root `annotations.xml` plus image files under `images/<split>/`, emits label metadata with `type` `polygon`, writes per-image `<polygon label points occluded="0" z_order="0">` entries, includes images without segment files with no polygon entries, and skips invalid segment records. Do not change annotation save/reopen, mask/brush/eraser materialization, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for export-format work. Covered by isolated test build, `--cvat-segmentation-export`, and `--export-capability-inventory`.

2026-07-03 COCO detection import contract: `CocoDetectionImportService` should remain a non-UI import service that reads COCO detection `images`, `categories`, and box `annotations`, creates/extends the local class catalog, copies source images into the requested local split under `data/<split>/images`, writes YOLO label files under `data/<split>/labels`, writes empty label files for imported images with no annotations, skips invalid annotations, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--coco-detection-import`, and `--export-capability-inventory`.

2026-07-03 Pascal VOC detection import contract: `PascalVocDetectionImportService` should remain a non-UI import service that reads Pascal VOC XML files, resolves source images from the supplied image root or rooted XML path, creates/extends the local class catalog, copies images into the requested local split under `data/<split>/images`, converts 1-based inclusive VOC `bndbox` values into YOLO label lines under `data/<split>/labels`, writes empty label files for XMLs with no objects, skips invalid objects/XMLs, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--pascal-voc-detection-import`, and `--export-capability-inventory`.

2026-07-03 Label Studio detection import contract: `LabelStudioDetectionImportService` should remain a non-UI import service that reads Label Studio task JSON, resolves source images from `data.image`, creates/extends the local class catalog from `RectangleLabels`, copies images into the requested local split under `data/<split>/images`, converts percent-based rectangle values into YOLO label lines under `data/<split>/labels`, writes empty label files for tasks with no valid results, skips invalid tasks/results, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--label-studio-detection-import`, and `--export-capability-inventory`.

2026-07-03 CVAT detection import contract: `CvatDetectionImportService` should remain a non-UI import service that reads CVAT image-task zip archives, loads root `annotations.xml`, extracts bundled images from `images/...`, creates/extends the local class catalog from `<box label>`, copies images into the requested local split under `data/<split>/images`, converts `xtl/ytl/xbr/ybr` boxes into YOLO label lines under `data/<split>/labels`, writes empty label files for images with no boxes, skips invalid images/boxes, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--cvat-detection-import`, and `--export-capability-inventory`.

2026-07-03 COCO segmentation import contract: `CocoSegmentationImportService` should remain a non-UI import service that reads COCO segmentation `images`, `categories`, and polygon `annotations`, creates/extends the local class catalog, copies source images into the requested local split under `data/<split>/images`, writes local `segments/*.json` and `masks/*.png` via `YoloSegmentationAnnotationService`, skips invalid annotations, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, mask/brush/eraser interaction behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--coco-segmentation-import`, and `--export-capability-inventory`.

2026-07-03 Label Studio segmentation import contract: `LabelStudioSegmentationImportService` should remain a non-UI import service that reads Label Studio PolygonLabels task JSON, resolves source images from `data.image`, creates/extends the local class catalog from polygon labels, copies images into the requested local split under `data/<split>/images`, converts percent-based polygon points into local `segments/*.json` and `masks/*.png` via `YoloSegmentationAnnotationService`, skips invalid tasks/results, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, mask/brush/eraser interaction behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--label-studio-segmentation-import`, and `--export-capability-inventory`.

2026-07-03 CVAT segmentation import contract: `CvatSegmentationImportService` should remain a non-UI import service that reads CVAT image-task zip archives, loads root `annotations.xml`, extracts bundled images from `images/...`, creates/extends the local class catalog from valid `<polygon label>` entries, copies images into the requested local split under `data/<split>/images`, converts polygon points into local `segments/*.json` and `masks/*.png` via `YoloSegmentationAnnotationService`, skips invalid images/polygons without adding invalid polygon classes, and saves `data.yaml`. Do not change annotation editor save/reopen behavior, mask/brush/eraser interaction behavior, image queue loading, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for import-format work. Covered by isolated test build, `--cvat-segmentation-import`, and `--export-capability-inventory`.

2026-07-03 YOLOv8/YOLO11 segmentation inference contract: `Runtime/Python/openvisionlab_ultralytics_worker.py` should preserve Ultralytics segmentation mask polygons from `result.masks.xy` on each `DetectImageResult` candidate as `segmentationType=polygon`, `polygonPoints`, and `normalizedPolygonPoints` while keeping the existing bbox fields. `PythonDetectionResultProtocol.DefectInfo` should preserve those fields, and `PythonModelStatusProtocol` should preserve `segmentationModels` from worker capabilities. This is a runtime/protocol contract only; do not change candidate overlay rendering, label-confirm behavior, mask materialization, Viewer/OpenGL/ROI/brush/eraser paths under this contract without a separate focused gate. Covered by `python .\Runtime\Python\openvisionlab_ultralytics_worker.py --self-test`, `python -m py_compile .\Runtime\Python\openvisionlab_ultralytics_worker.py`, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--python-detection-result-protocol`.

2026-07-03 YOLO worker smoke segmentation polygon metadata contract: `YoloWorkerSmokeTestService` should preserve `segmentationType`, `polygonPoints`, and `normalizedPolygonPoints` from explicit YOLO `--smoke-test` candidate JSON on `YoloWorkerSmokeCandidate`, alongside existing bbox and classification metadata. This is diagnostic smoke-result parsing only; do not change worker execution, label confirmation, candidate overlay rendering, mask materialization, Viewer/OpenGL/ROI/brush/eraser paths under this contract. Covered by isolated test build, `--yolo-worker-smoke-service`, `--python-detection-result-protocol`, and `--python-ultralytics-worker`.

2026-07-03 YOLOv8/YOLO11 segmentation polygon confirmation contract: when `DetectionResultApplicationService.CommitLastDetectionToMainLabels(..., createSegmentationFromBoxes: true)` confirms a candidate with `DefectInfo.PolygonPoints`, it should save the model polygon as a segmentation object instead of converting only the bbox to a rectangle mask. Detection-only candidates without polygon points must keep the existing rectangle-derived segmentation fallback. Keep this preference inside the detection-result application service and the thin `DisplayLayerDocument.AddSegmentationPolygon` adapter; do not change viewer polygon normalization, mask rendering, brush/eraser interaction, or candidate overlay rendering for this behavior. Covered by isolated test build and `--detection-result-segmentation-confirm`.

2026-07-03 YOLOv8/YOLO11 classification/anomaly result contract: the bundled Ultralytics worker should preserve classification-only `result.probs` output as an image-level candidate with `candidateType=imageClassification`, `predictionType=classification`, `imageLevel=true`, class id/name, and confidence while leaving bbox fields empty. `PythonDetectionResultProtocol.DefectInfo`, `YoloWorkerSmokeCandidate`, and `PythonModelStatusProtocol` should preserve the matching candidate metadata and `classificationModels` capability. This contract does not automatically map class names to anomaly `Normal`/`Abnormal` review state; add that only behind an explicit configurable mapping and focused gate. Covered by Python worker self-test, Python compile, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--python-detection-result-protocol`.

2026-07-07 local YOLOv8 classification adapter smoke contract: `C:\Git\yolov8\labeling_tcp_client.py` should mirror the bundled worker's classification result shape by converting Ultralytics `result.probs` into a single image-level candidate with `candidateType=imageClassification`, `predictionType=classification`, `imageLevel=true`, class id/name, confidence, and zero-sized bbox fields when no detection boxes are present. The adapter may load `yolov8n-cls.pt` as an approved pretrained seed for runtime smoke only; do not treat ImageNet class output as industrial anomaly accuracy or as an adoptable normal/abnormal model. Covered by local Python compile, local adapter self-test, and local `--smoke-test --model yolov8 --weights C:\Git\yolov8\yolov8n-cls.pt` returning a non-empty classification candidate.

2026-07-07 WPF YOLOv8 anomaly classification runtime smoke contract: `--wpf-yolov8-anomaly-classification-runtime-smoke` should use the local YOLOv8 TCP adapter and `yolov8n-cls.pt` to run current-image inference through `WpfLabelingShellWindow.RunDetectionForImageAsync`, load the returned image-level classification candidate into Candidate Review, and persist the active anomaly image state only through explicit `AnomalyClassificationSettings` class mapping. This remains a runtime/state smoke with a pretrained ImageNet seed; it must not be reported as normal/abnormal model accuracy or adoption readiness. Covered by isolated test build and the focused WPF runtime smoke switch.

2026-07-07 YOLOv8 anomaly classification trained-fixture smoke contract: the local YOLOv8 source workflow should be able to train a classification `best.pt` from a normal/abnormal folder dataset without additional downloads, load that trained weight through `C:\Git\yolov8\labeling_tcp_client.py`, preserve the trained class names on `imageClassification` candidates, and pass the WPF mapped-inference runtime smoke when the returned class is explicitly configured in anomaly settings. This is tiny synthetic fixture evidence only; validation top1 `0.5` is not production accuracy and must not unlock model adoption. Covered by local `yolo.exe classify train`, local adapter smokes for both `abnormal` and `normal` test images, and `--wpf-yolov8-anomaly-classification-runtime-smoke` with the trained fixture `best.pt`.

2026-07-07 anomaly classification evaluation adoption guard contract: `AnomalyClassificationEvaluationService` should keep normal/abnormal model adoption blocked unless held-out evidence has at least 10 total images, at least 5 normal and 5 abnormal images, overall accuracy >= 0.9, per-class accuracy >= 0.8, and correct predictions meet the configured minimum confidence. `scripts\evaluate-yolo-classification.ps1` should run the local YOLOv8 adapter over a held-out `normal`/`abnormal` split, accept `-MinimumConfidence`, count low-confidence class matches as incorrect evidence, and write `classification-evaluation-summary.json` with the same adopt/hold evidence. Tiny fixtures, including the current synthetic trained-fixture smoke and `artifacts\real-smoke\yolo11-cls`, must remain evidence-only even when runtime paths pass. Covered by isolated test build, `--anomaly-classification-evaluation`, PowerShell parser check, and the synthetic trained-fixture evaluation summaries under `artifacts\yolo-classification-evaluation`.

2026-07-08 anomaly classification Model Center evaluation run contract: `WpfAnomalyClassificationEvaluationRunService` should build the app-level run request for YOLOv8 only, export reviewed normal/abnormal images into a fresh classification-evaluation input dataset, pass the local adapter, weights, dataset root, and configured minimum confidence into `scripts\evaluate-yolo-classification.ps1`, and leave non-YOLOv8 engines blocked by validation. The generated summary should be applied through the existing `AnomalyClassificationEvaluationService` and `WpfAnomalyClassificationEvaluationPresentationService`; this runner must not change thresholds, model adoption, training, inference, model registry, or annotation hot paths. Covered by isolated test build, `--anomaly-classification-evaluation`, `--wpf-labeling-shell`, and a current 1920x1080 Model Center visual smoke capture.

2026-07-07 anomaly classification evaluation presentation contract: `AnomalyClassificationEvaluationService` should read `classification-evaluation-summary.json` back into `AnomalyClassificationEvaluationReport`, and `WpfAnomalyClassificationEvaluationPresentationService` should translate that report into operator-facing Korean recommendation, metrics, detail, and action text. It must present adoptable only when the core evaluation report is adoptable, and hold details should include insufficient evidence counts, overall/per-class accuracy blockers, and low-confidence class-match blockers. This is presentation/loading only and does not alter evaluation thresholds, training, inference, model registry, or annotation hot paths. Covered by isolated test build and `--anomaly-classification-evaluation`.

2026-07-03 anomaly classification decision contract: `AnomalyClassificationDecisionService` should map image-level classification candidates to `Normal` or `Abnormal` only through explicit `AnomalyClassificationDecisionOptions` class-name lists and a configured confidence threshold. It must leave unknown classes, ambiguous classes mapped to both states, low-confidence results, missing configuration, and non-image-level detections unmapped as `Unreviewed`. Do not hardcode `Normal`, `OK`, `NG`, `Defect`, or similar class-name guesses into runtime anomaly state changes. Covered by isolated test build and `--anomaly-classification-decision`.

2026-07-03 anomaly classification settings contract: `LabelingProjectSettings.AnomalyClassification` should persist the explicit normal/abnormal class-name mappings and confidence threshold needed by `AnomalyClassificationDecisionService`. Defaults must keep both class lists empty, invalid thresholds must normalize into `0..1`, and save/reopen through recipe `VISION.xml` must preserve configured mappings before any WPF auto-apply path marks images `Normal` or `Abnormal`. Covered by isolated test build and `--anomaly-classification-decision`.

2026-07-03 anomaly classification WPF configuration contract: anomaly normal/abnormal class mapping inputs should live in `WpfYoloModelSettingsPanelViewModel` and be displayed by `WpfYoloModelSettingsPanel.xaml` as bindings only. Shell code-behind may only adapt load/save by passing `ProjectSettings.AnomalyClassification` into the ViewModel; parsing, summary text, class-list normalization, and threshold clamping must remain in the ViewModel/core settings. This config UI does not automatically mark images after inference; that workflow needs a separate focused gate. Covered by isolated test build, `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, `--mvvm-infra`, `--wpf-labeling-shell`, and a 1920x1080 `--wpf-visual-smoke --review-tab yolo-model-anomaly` capture.

2026-07-03 anomaly classification review-state auto-apply contract: current-image and batch inference may mark anomaly images `Normal` or `Abnormal` only when `AnomalyClassificationDecisionService` maps image-level classification candidates through explicit project settings. Unknown classes, low-confidence candidates, ambiguous normal/abnormal mappings, non-image-level detections, and conflicting aggregate candidates must remain unmapped. The shell may adapt the mapped decision into `AnomalyImageReviewStatusService`, but class-name parsing and decision rules must stay in the service/settings layer. Do not hardcode OK/NG/Normal/Defect heuristics or change candidate overlay rendering, ROI, brush, eraser, or segmentation mask interaction paths for this workflow. Covered by isolated test build, `--anomaly-classification-decision`, `--wpf-anomaly-purpose-flow`, and `--wpf-labeling-shell`.

2026-07-03 YOLOv8/YOLO11 segmentation training request contract: `LearningProtocol.YoloTrainingRequest` should carry a task-aware `task` field, defaulting to `detect`; `YoloTrainingWorkflowService` should send `segment` only for `LabelingDatasetPurpose.Segmentation` and keep object/anomaly YOLO-label datasets on `detect` until a separate classification dataset contract exists. The bundled Ultralytics worker should advertise YOLOv8/YOLO11 training capability, accept `TrainYolo`, choose task-appropriate pretrained defaults such as `yolo11n-seg.pt`, start `model.train(...)` on a background thread, and emit `TrainingStatus` messages that the existing WPF parser can consume. The worker self-test should keep a fake `YOLO.train` path that verifies the `segment` task, epoch callback, and completed status without requiring a real model download. Do not change annotation save/reopen, candidate overlay rendering, segmentation mask editing, ROI, brush, or eraser paths for training-contract work. Covered by Python worker self-test, Python compile, isolated test build, `--learning-protocol`, `--python-ultralytics-worker`, `--python-model-status-protocol`, `--python-model-runtime-self-test`, `--python-model-runtime-connection`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 anomaly classification dataset export contract: `AnomalyClassificationDatasetExportService` should remain a non-UI bridge from reviewed `AnomalyImageReviewStatusService` state to an Ultralytics classification folder dataset. It should copy only existing reviewed `Normal` and `Abnormal` images into `classification/<split>/normal` and `classification/<split>/abnormal`, reuse `YoloDatasetSplitService`, and skip unreviewed or missing images. Do not change annotation save/reopen, image queue review behavior, detection candidate application, Viewer/OpenGL/ROI/brush/eraser paths, or training command routing for this export-only slice. Covered by isolated test build, `--anomaly-classification-dataset-export`, and `--wpf-anomaly-purpose-flow`.

2026-07-03 anomaly classification training workflow contract: `YoloTrainingWorkflowService` should prepare anomaly classification training by exporting reviewed normal/abnormal images through `AnomalyClassificationDatasetExportService`, require at least one normal and one abnormal image, send the exported classification root as the training data path, and send `task=classify` in `StartTraining`. For YOLOv8/YOLO11, the packet should carry the matching classification default weights (`yolov8n-cls.pt` / `yolo11n-cls.pt`). Segmentation training should continue to send `task=segment`; object detection should keep `task=detect`. Do not change WPF layout, annotation save/reopen, detection candidate application, Viewer/OpenGL/ROI/brush/eraser paths, or real model execution behavior for this workflow-routing slice. Covered by isolated test build, `--anomaly-classification-training-workflow`, `--anomaly-classification-dataset-export`, and `--learning-protocol`.

2026-07-03 YOLOv8/YOLO11 training capability status contract: the YOLO model settings runtime profile and execution summary should use live worker capability lists when available. Static settings-only fallback may still show bundled Ultralytics as detection-ready/training-unsupported, but after `WorkerTrainingModels` and `WorkerDetectionModels` include the selected adapter, the ViewModel must show YOLOv8/YOLO11 training and inspection as available instead of `학습: 미지원` or `실행 차단`. Keep this as a status/profile capability refresh only; do not change model execution, layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths, or real model training behavior for this contract. Covered by isolated test build, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--python-model-status-protocol`, and `--wpf-labeling-shell`.

2026-07-03 anomaly classification training readiness contract: anomaly classification training readiness should be checked through `AnomalyClassificationTrainingReadinessService` before dataset export or `StartTraining`. It should count source images, saved reviewed `Normal`/`Abnormal` states, and train-split normal/abnormal images; require at least one reviewed normal and one reviewed abnormal image in the training split; and let WPF readiness/start-failure presentation translate that reason into operator-facing text with current counts. Do not change training execution, WPF layout, annotation save/reopen, detection candidate application, Viewer/OpenGL/ROI/brush/eraser paths, or real model execution behavior for this readiness-only contract. Covered by isolated test build, `--anomaly-classification-training-workflow`, `--wpf-training-readiness-presentation`, `--wpf-training-status-summaries`, `--wpf-labeling-shell`, and `--wpf-anomaly-purpose-flow`.

2026-07-03 YOLOv8/YOLO11 fake-training self-test contract: `openvisionlab_ultralytics_worker.py --self-test` should verify both segmentation and classification fake-training paths without downloading models. For YOLOv8 and YOLO11, invalid legacy YOLOv5 weight names should resolve to task-specific defaults (`yolov8n-seg.pt`/`yolov8n-cls.pt` and `yolo11n-seg.pt`/`yolo11n-cls.pt`), prefer a matching cached file from the worker model root when present, and emit a completed `TrainingStatus` for each fake `YOLO.train(...)` task. Do not change real training execution behavior, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this self-test coverage contract. Covered by Python self-test, Python compile, and `--python-ultralytics-worker`.

2026-07-03 Ultralytics resolved training weight status contract: the bundled Ultralytics worker should include the resolved cached-or-bare training weight in `TrainYoloResult` and every `TrainingStatus` as `trainingWeights`, and C# status parsing should preserve it as `PythonCommunicationStatus.LastTrainingWeightsPath` so existing YOLO settings detail can show which weight was actually passed to `YOLO(...)`. This is a diagnostics/status contract only; do not change real training execution behavior, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this slice. Covered by Python self-test, Python compile, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 Ultralytics task-weight cache inventory contract: health/model/smoke/preload status from `openvisionlab_ultralytics_worker.py` should report known YOLOv8/YOLO11 default task weights under `cachedTrainingWeights` and `missingTrainingWeights` based on the active worker `modelRoot`. C# status parsing should preserve those lists as `PythonCommunicationStatus.WorkerCachedTrainingWeights` and `WorkerMissingTrainingWeights`, and the existing YOLO settings detail should show the inventory so cache-safe smoke decisions do not require filesystem inspection. This is diagnostics/status only; do not change real training execution behavior, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this slice. Covered by Python self-test, Python compile, cache probe, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 Ultralytics runtime-ready weight inventory contract: cached task weights should be split into `runtimeReadyTrainingWeights` and `runtimeBlockedTrainingWeights` according to the worker's runtime-supported model families. A cached YOLO11 weight must be reported as runtime-blocked when the selected Ultralytics package lacks YOLO11 support, even if the file exists. C# status parsing should preserve those lists and the existing YOLO settings detail should show them. This is diagnostics/status only; do not change real training execution behavior, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this slice. Covered by Python self-test, Python compile, cache probe, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 Ultralytics missing weight download/blocker inventory contract: missing YOLOv8/YOLO11 task weights should be split into `downloadRequiredTrainingWeights` for model families supported by the selected runtime and `runtimeBlockedMissingTrainingWeights` for model families the selected runtime cannot run. This lets the app distinguish "download/cache needed" from "runtime upgrade needed" without triggering downloads. C# status parsing should preserve both lists and the existing YOLO settings detail should show them. This is diagnostics/status only; do not change real training execution behavior, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this slice. Covered by Python self-test, Python compile, cache probe, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 Ultralytics weight diagnostic readable-detail contract: YOLO settings detail should translate task-weight cache/runtime/download diagnostics into operator-facing labels while preserving the filenames and decisions from `WorkerCachedTrainingWeights`, `WorkerMissingTrainingWeights`, `WorkerRuntimeReadyTrainingWeights`, `WorkerRuntimeBlockedTrainingWeights`, `WorkerDownloadRequiredTrainingWeights`, and `WorkerRuntimeBlockedMissingTrainingWeights`. Visible detail text should not expose raw protocol key fragments such as `runtime-blocked-missing`. This is presentation text only; do not change worker status fields, real training execution, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this slice. Covered by isolated test build and `--wpf-yolo-model-settings-panel`.

2026-07-03 Ultralytics implicit training download guard contract: the bundled Ultralytics worker must not pass uncached bare default YOLOv8/YOLO11 training weights such as `yolov8n-seg.pt` to `YOLO(...)` unless the request explicitly opts into model downloads with `allowModelDownload`, `allowWeightDownload`, or `allowDownload`. Without that opt-in, `TrainYolo` should fail before starting the background training thread with `TrainingWeightDownloadRequired` and preserve the resolved `trainingWeights` in the failed result/status. This is a worker runtime-safety guard only; do not add UI download approval, package upgrades, model downloads, or real training execution under this contract. Covered by Python self-test, Python compile, isolated test build, and `--python-ultralytics-worker`.

2026-07-03 training download opt-in worker self-test contract: when a `TrainYolo` request explicitly includes `allowModelDownload=true`, `allowWeightDownload=true`, or `allowDownload=true`, the bundled Ultralytics worker may bypass the uncached bare-weight guard and proceed into the normal training path. The self-test should prove this only with fake `YOLO.train(...)`, preserving `trainingWeights` and completed status without performing a real model download. This does not authorize the C# app to send approval flags; C# packet tests should continue verifying they are absent until a user-approved download workflow exists. Covered by Python self-test, Python compile, isolated test build, and `--python-ultralytics-worker`.

2026-07-03 training download guard parser contract: `PythonModelStatusProtocol` should parse failed `TrainYoloResult` payloads from the Ultralytics download guard as training status messages, keep `state=failed`, mark them as errors, preserve the `TrainingWeightDownloadRequired` error code text, and preserve the resolved `trainingWeights` value such as `yolov8n-seg.pt`. `CCommunicationLearning` should carry that failed result into `PythonCommunicationStatus.LastTrainingState`, `LastTrainingWeightsPath`, and `LastError` so existing status-detail presentation can show the blocked weight. This is protocol/communication coverage only; do not change worker execution, UI approval behavior, model downloads, or package upgrades under this contract. Covered by isolated test build and `--python-model-status-protocol`.

2026-07-03 training download guard presentation contract: YOLO settings detail should translate `PythonCommunicationStatus.LastError` values containing `TrainingWeightDownloadRequired` into operator-readable guidance to cache the training weight or explicitly approve download, while protocol and communication snapshots continue preserving the raw error code for diagnostics. This is presentation text only; do not add UI download approval, model downloads, package upgrades, worker execution changes, WPF layout changes, or Viewer/OpenGL/ROI/brush/eraser changes under this contract. Covered by isolated test build and `--wpf-yolo-model-settings-panel`.

2026-07-03 training download guard progress-status contract: live WPF training progress and recovery text should use the full `PythonCommunicationStatus` for failed/error training states so worker download guards preserved in `LastError` are visible to the operator. `TrainingWeightDownloadRequired` should be translated into training-weight cache/download-approval guidance, and the blocked task-weight filename such as `yolov8n-seg.pt` should remain visible, while protocol/communication layers keep the raw code for diagnostics. This is presentation text only; do not add UI download approval, model downloads, package upgrades, worker execution changes, WPF layout changes, or Viewer/OpenGL/ROI/brush/eraser changes under this contract. Covered by isolated test build, `--wpf-training-status-summaries`, and `--python-model-status-protocol`.

2026-07-03 YOLOv8 segmentation download guard training-history contract: failed/error training-guide history should prefer `PythonCommunicationStatus.LastError` over the generic worker training message so the YOLOv8 segmentation download guard is not lost after `yolov8n-seg.pt` fails the no-download cache check. The stored history message and compact history summary should translate `TrainingWeightDownloadRequired` through `WpfTrainingProgressPresentationService.FormatTrainingMessage` and preserve the blocked weight filename while avoiding raw worker-code display. This is history/status text only; do not add settings schema fields, UI download approval, model downloads, package upgrades, worker execution changes, WPF layout changes, or Viewer/OpenGL/ROI/brush/eraser changes under this contract. Covered by isolated test build, `--wpf-training-guide-history`, `--wpf-training-status-summaries`, and `--python-model-status-protocol`.

2026-07-03 training download approval packet guard contract: C# `StartTraining` packets should not include `allowModelDownload`, `allowWeightDownload`, or `allowDownload` until an explicit user-approved download workflow exists. The worker may understand those opt-in fields, but the app training workflow must not silently send them from segmentation or anomaly classification training packet paths. This is packet-test coverage only; do not add UI download approval, model downloads, package upgrades, worker execution changes, WPF layout changes, or Viewer/OpenGL/ROI/brush/eraser changes under this contract. Covered by isolated test build, `--learning-protocol`, `--dataset-readiness-purpose`, and `--anomaly-classification-training-workflow`.

2026-07-03 YOLO11 task-specific training weight packet contract: `YoloTrainingWorkflowService` should send task-specific Ultralytics default weight names for YOLOv8/YOLO11 training packets instead of relying on the worker to reinterpret legacy YOLOv5 weight names. For YOLO11, `detect` should send `yolo11n.pt`, `segment` should send `yolo11n-seg.pt`, and `classify` should send `yolo11n-cls.pt`; YOLOv8 should use the matching `yolov8n*` defaults. Keep YOLOv5 fallback behavior unchanged. Do not change real training execution, WPF layout, annotation save/reopen, candidate overlay rendering, Viewer/OpenGL/ROI/brush/eraser paths for this packet contract. Covered by isolated test build, `--dataset-readiness-purpose`, `--anomaly-classification-training-workflow`, `--learning-protocol`, and `--python-ultralytics-worker`.

2026-07-03 dynamic YOLO11 runtime capability contract: the bundled Ultralytics worker should compute capability payloads from the selected Python runtime instead of always advertising YOLO11. YOLOv8 remains the baseline when Ultralytics is installed, but YOLO11 detection/training/segmentation/classification capability should be advertised only when `yolo11_runtime_available()` finds the YOLO11 `C3k2` module. Health/capability payloads should expose `ultralyticsVersion`, `yolo11RuntimeAvailable`, and a YOLO11-disabled runtime warning when the selected runtime lacks YOLO11 support; unsupported YOLO11 detection/training errors should include the installed Ultralytics version and missing `C3k2` reason, and the worker self-test should exercise those unsupported request paths when YOLO11 is unavailable. C# status parsing and `PythonCommunicationStatus` should preserve those runtime warnings plus segmentation/classification model lists so the YOLO settings detail can show them, and capability refresh should replace old lists with the latest payload so empty capability updates clear stale YOLO11 support. On the current Ultralytics `8.0.132` environment this correctly reports YOLOv8-only capability, and C# runtime adapter support must not re-enable the selected YOLO11 profile through the static bundled-worker fallback after live worker capability lists exclude YOLO11. The selected profile/runtime state should explicitly explain when the connected worker did not report the requested adapter. Do not restore fixed `("yolov8", "yolo11")` capability lists without a real runtime gate. Covered by Python worker self-test, Python compile, local capability probe, isolated test build, `--python-ultralytics-worker`, `--python-model-status-protocol`, `--python-model-runtime-connection`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 YOLO worker smoke adapter runtime-gate contract: `YoloWorkerSmokeTestService` should pass the selected protocol adapter into the bundled Ultralytics worker `--smoke-test` CLI with `--model`, and smoke-test mode should return an `UnsupportedModel` JSON result before model load when the current runtime capability list excludes that adapter. The C# smoke parser should preserve the error code, so YOLO11 runtime blockers such as installed Ultralytics `8.0.132` missing `C3k2` remain distinguishable from generic model-load failures. This is diagnostics/preflight behavior only; it does not authorize downloads, package upgrades, or claims that real YOLOv8/YOLO11 segmentation/anomaly execution passed. Covered by Python worker self-test, Python compile, direct `--smoke-test --model yolo11` CLI probe, isolated test build, `--yolo-worker-smoke-service`, and `--python-ultralytics-worker`.

2026-07-03 WPF YOLO smoke failure status-detail contract: current-image worker smoke status text should be built by `WpfDetectionResultPresentationService.BuildSmokeStatus(...)`, not directly in `WpfLabelingShellWindow.DetectionSmokeExecution.cs`. Failure status should preserve actionable worker blocker details such as `UnsupportedModel` and missing YOLO11 `C3k2`, while success status should keep the candidate count. This is status text only; do not change WPF layout, detection overlay rendering, candidate confirmation, Viewer/OpenGL/ROI/brush/eraser paths, downloads, or package upgrades under this contract. Covered by isolated test build, `--wpf-detection-result-presentation`, `--yolo-worker-smoke-service`, and `--python-ultralytics-worker`.

2026-07-03 YOLOv8 local worker folder connection contract: YOLOv8 runtime profile connection should follow the existing YOLOv5 local-folder model when the operator selects a local worker folder such as `C:\Git\yolov8`. `PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection` owns mapping that folder to `labelling_tcp_client.py` or `labeling_tcp_client.py`, local `.venv` Python when present, YOLOv8 segmentation weights such as `yolov8n-seg.pt`, and `data/train/images`; WPF shell code-behind may only adapt the folder picker result into the service and apply the ViewModel result. A local YOLOv8 worker with installed `ultralytics` may enable current inspection, but training must remain blocked until the worker reports `TrainYolo` capability. This does not change Viewer/OpenGL/ROI/brush/eraser paths, model downloads, package installs, or real training execution. Covered by `python C:\Git\yolov8\labeling_tcp_client.py --self-test`, isolated test build, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--python-model-runtime-self-test`, `--mvvm-infra`, and `git diff --check`.

2026-07-03 YOLOv8 local worker TrainYolo capability contract: the local `C:\Git\yolov8\labeling_tcp_client.py` worker should advertise YOLOv8 training/detection/segmentation/classification capability only when the selected Python runtime imports `ultralytics`, should return `TrainYoloResult` and stream `TrainingStatus` for accepted training starts, and should call local Ultralytics `YOLO(...).train(...)` with the packet task/data/epoch/image-size/batch values. Requested/default weights must resolve to an existing local file such as `yolov8n-seg.pt`; the worker must not trigger implicit model downloads by passing missing bare default names to Ultralytics. This does not prove real training execution, add package installs, add downloads, alter WPF layout, or change Viewer/OpenGL/ROI/brush/eraser paths. Covered by Python compile, `python C:\Git\yolov8\labeling_tcp_client.py --self-test`, local capability/weight probe, isolated test build, `--python-model-runtime-connection`, `--python-model-status-protocol`, `--python-model-runtime-self-test`, and `--wpf-yolo-model-settings-panel`.

2026-07-03 YOLOv8 local Ultralytics source runtime contract: local YOLOv8 should follow the existing YOLOv5 source-owned runtime model. `C:\Git\yolov8` may contain a local `ultralyticsMaster` checkout from `ultralytics/ultralytics` plus a local `.venv` editable install; the TCP worker should prepend that source root before importing `ultralytics` and report the resolved `ultralyticsPath`/source-root diagnostics. The app's local YOLOv8 folder connection and adapter-support path may treat a local worker with installed Ultralytics plus `TrainYolo` handling as training/inspection-capable, but real segmentation execution still requires a local segmentation weight such as `yolov8n-seg.pt`. Do not replace this with implicit app-owned downloads or package upgrades without explicit approval, and do not change Viewer/OpenGL/ROI/brush/eraser paths under this contract. Covered by venv Python compile/self-test for `C:\Git\yolov8\labeling_tcp_client.py`, local import probe showing Ultralytics `8.4.86` from `C:\Git\yolov8\ultralyticsMaster\ultralytics\__init__.py`, clean `C:\Git\yolov8\ultralyticsMaster` source status, isolated test build, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, `--python-model-runtime-self-test`, and `--mvvm-infra`.

2026-07-03 YOLOv8 local segmentation training/inference smoke contract: with explicit user approval to download the pretrained seed, `C:\Git\yolov8\yolov8n-seg.pt` may be used only as a starting weight or smoke-test seed, not as the final product model. The local YOLOv8 worker should accept `TrainYolo` with `task=segment`, local `data.yaml`, `workers=0`, and local weights, stream `TrainingStatus`, and produce trained `best.pt`/`last.pt` outputs. The first historical smoke produced `runs/train/.../weights/best.pt` before task-aware run-folder routing was corrected; current segmentation smokes should produce `runs/segment/.../weights/best.pt`. The generated `best.pt` must load through the same local adapter `--smoke-test` inference path. `StopTraining` should map to `StopTask`, mark running training as `stopping`, and rely on Ultralytics callbacks for cooperative cancellation; it should not be described as a hard process kill. This smoke proves local training/inference plumbing only, not production accuracy. Covered by venv Python compile/self-test for `C:\Git\yolov8\labeling_tcp_client.py`, a 1-epoch local adapter `TrainYolo` segmentation smoke on `C:\Git\yolov8\data\smoke-seg`, generated `C:\Git\yolov8\runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt`, local adapter `--smoke-test` loading that `best.pt`, isolated test build, `--learning-protocol`, `--python-model-status-protocol`, and `--wpf-training-status-summaries`.

2026-07-03 YOLOv8 trained best.pt connection contract: `PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection` should prefer operator-owned trained weights over pretrained seeds when mapping a local YOLOv8 folder. Root `best.pt` stays first, trained outputs under `runs/segment/**/best.pt` or `runs/train/**/best.pt` come next, and `yolov8n-seg.pt`/`yolov8s-seg.pt`/`yolov8m-seg.pt` are fallback seed weights. A local folder containing the historical `runs/train/openvisionlab-yolov8-seg-smoke/weights/best.pt`, the corrected `runs/segment/openvisionlab-yolov8-seg-runs-segment-smoke/weights/best.pt`, and `yolov8n-seg.pt` should select the corrected `runs/segment` trained model for inspection. This is path selection only; it does not judge model quality, change training execution, or change WPF layout. Covered by isolated test build, `--python-model-runtime-connection`, `--wpf-yolo-model-settings-panel`, and `--python-model-runtime-self-test`; the explicit historical-train-vs-corrected-segment fixture is covered by `--python-model-runtime-connection`.

2026-07-03 YOLOv8 segmentation best.pt current-dataset matching contract: WPF post-training best.pt selection should include Ultralytics segmentation run folders under `runs/segment/**/weights/best.pt` and should read both YOLOv5 `opt.yaml` and YOLOv8/Ultralytics `args.yaml` `data:` entries before deciding whether a trained `best.pt` belongs to the current dataset. When a newer foreign YOLOv8 segmentation run exists, a current-dataset run with matching `args.yaml data:` should be preferred for candidate staging. This is model-result selection only; it does not judge model quality, run training, change WPF layout, or alter Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build and `--wpf-training-weights-service`.

2026-07-03 YOLOv8 segmentation training-session apply contract: after a completed WPF training session, the post-training candidate path should accept Ultralytics segmentation outputs under `runs/segment/<run>/weights/best.pt` and stage that path as the current pending inspection-model candidate. The focused WPF training-session fixture uses segmentation-style `results.csv` and verifies `PythonModelSettings.WeightsPath` moves to the `runs/segment` `best.pt`. This is a mocked completion/apply gate only; it does not run real training, judge model accuracy, change WPF layout, or alter Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-yolo-training-session`, and `--wpf-training-weights-service`.

2026-07-03 YOLOv8 segmentation mask metric contract: WPF training-result metric parsing should prefer Ultralytics segmentation mask `(M)` metrics over box `(B)` metrics when both are present in a YOLOv8 segmentation `results.csv`. Precision, recall, mAP50, and mAP50-95 shown for a segmentation run should therefore reflect mask quality when the file includes both box and mask columns. When both `val/box_loss` and `val/seg_loss` are present, the parsed loss should use `val/seg_loss`, and the UI-facing comparison label should stay neutral as `loss`. This is post-training metric parsing only; it does not judge model accuracy, run training, change WPF layout, or alter Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build and `--wpf-training-weights-service`.

2026-07-03 YOLOv8 task-aware training run-folder contract: bundled and local Ultralytics workers should send training outputs to the task-specific Ultralytics project folder: `segment` under `runs/segment`, `classify` under `runs/classify`, and detect/default under `runs/train`. This keeps new YOLOv8 segmentation `best.pt` outputs aligned with WPF discovery and avoids mixing segmentation runs into detection folders. Existing older smoke artifacts under `runs/train` may remain as historical outputs. This is Python worker routing only; it does not run a new real training job, judge model quality, change WPF layout, or alter Viewer/OpenGL/ROI/brush/eraser paths. Covered by Python compile/self-test for `Runtime/Python/openvisionlab_ultralytics_worker.py`, venv Python compile/self-test for `C:\Git\yolov8\labeling_tcp_client.py`, isolated test build, `--python-ultralytics-worker`, `--python-model-runtime-connection`, `--wpf-training-weights-service`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-03 YOLOv8 runs/segment smoke contract: the local `C:\Git\yolov8` worker should produce real segmentation training outputs under `C:\Git\yolov8\runs\segment\<run>\weights\best.pt` after task-aware run-folder routing. The generated `best.pt` should load through the same local adapter `--smoke-test` path with the local venv and local Ultralytics source checkout. This is a tiny local plumbing smoke only; it does not prove production dataset accuracy, WPF active-model promotion, or YOLO11 readiness. Covered by direct local adapter `TrainYolo` smoke with final `TrainingStatus.state=completed`, generated `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt`, and local adapter `--smoke-test` loading that generated `best.pt`.

2026-07-04 YOLOv8 app segmentation training-label contract: before `YoloTrainingWorkflowService` sends `StartTraining` for a `Segmentation` dataset, saved app segment JSON polygons should be converted into Ultralytics YOLO segmentation `labels/*.txt` lines. The conversion belongs in `YoloSegmentationTrainingLabelService` and should run after existing readiness passes, leaving annotation save/reopen, mask editing, ROI, brush, eraser, and WPF layout paths unchanged. The focused gate should verify both YOLO11 and YOLOv8 segmentation packets still carry `task=segment` and task-specific weights, and that generated train/valid label files contain polygon coordinates rather than empty detection-label files. This is a training-data preparation contract only; it does not claim a new real training run or production accuracy. Covered by isolated test build, `--dataset-readiness-purpose`, `--anomaly-classification-training-workflow`, `--learning-protocol`, and `--wpf-yolo-training-session`.

2026-07-04 YOLOv8 app-generated segmentation smoke contract: the focused fixture `--yolov8-segmentation-app-dataset-fixture` should create an ignored app-generated dataset under `artifacts/yolov8-app-segmentation-dataset` using the same C# dataset/annotation/training-prep services that the product uses. Local `C:\Git\yolov8\labeling_tcp_client.py` should then be able to train `task=segment` for one CPU epoch with local `yolov8n-seg.pt`, produce `runs/segment/openvisionlab-yolov8-app-seg-fixture-smoke/weights/best.pt`, and load that `best.pt` through `--smoke-test`. This proves app-generated YOLOv8 segmentation plumbing only; it does not prove production accuracy, broad dataset quality, or YOLO11 readiness. Covered by isolated test build, `--yolov8-segmentation-app-dataset-fixture`, direct local adapter training, direct local adapter `--smoke-test`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-04 YOLOv8 app segmentation best.pt staging contract: WPF training-weight selection should recognize a local YOLOv8 `runs/segment/<run>/weights/best.pt` as current-dataset training output when that run's `args.yaml data:` points at an app-prepared segmentation `data.yaml`. The app-prepared fixture should be generated through `YoloTrainingWorkflowService.TryPrepareTrainingDataset`, so saved `segments/*.json` polygons are converted into Ultralytics YOLO segmentation `labels/*.txt` before the synthetic `best.pt` is staged. This is post-training candidate selection and metric parsing only; it does not train on a real operator dataset, judge accuracy, change WPF layout, or alter Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-training-weights-service`, and `--yolov8-segmentation-app-dataset-fixture`.

2026-07-04 YOLOv8 local app-fixture best.pt preference contract: local YOLOv8 folder connection should prefer operator-trained `best.pt` outputs over pretrained segmentation seed weights, and among trained outputs should select the newest `runs/segment/**/weights/best.pt`. The focused connection fixture should include the historical `runs/train` smoke, the corrected `runs/segment` smoke, the app-generated `runs/segment/openvisionlab-yolov8-app-seg-fixture-smoke/weights/best.pt`, and `yolov8n-seg.pt`, and should select the app-generated segmentation run when it is newest. This is local path selection only; it does not judge model quality, run training, or change WPF layout. Covered by isolated test build, `--python-model-runtime-connection`, and `--wpf-yolo-model-settings-panel`.

2026-07-04 YOLOv8 local adapter segmentation polygon contract: the local `C:\Git\yolov8\labeling_tcp_client.py` adapter should preserve Ultralytics segmentation masks from `result.masks.xy` on each detection candidate as `segmentationType=polygon`, `polygonPoints`, and `normalizedPolygonPoints`, matching the bundled worker/C# protocol contract. Detection-only candidates without masks should keep the existing bbox-only fields. This is local worker candidate serialization only; it does not change C# label confirmation, WPF layout, model training, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by local venv Python compile, local worker `--self-test` fake-mask assertion, C# `--python-detection-result-protocol`, C# `--yolo-worker-smoke-service`, and a local app-fixture `--smoke-test` model-load/execution check. Note: `C:\Git\yolov8` is not a git repository, so this local file is outside Labelling_Application git tracking.

2026-07-04 YOLOv8 app-fixture non-empty polygon smoke contract: the local `C:\Git\yolov8` adapter should emit real segmentation polygon candidates from the app-generated segmentation fixture when the trained `runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt` is run at a low smoke confidence. The train and valid fixture images each returned one `Defect` candidate with `segmentationType=polygon`, `polygonPoints`, and `normalizedPolygonPoints` at `--conf 0.01`, and the valid-image smoke also passed with `--model yolov8`, matching the C# smoke service's adapter argument shape. This proves non-empty local YOLOv8 polygon serialization on a tiny fixture only; it does not prove production accuracy or replace a real operator dataset gate. Covered by direct local adapter `--smoke-test` commands for the app-generated train and valid images.

2026-07-04 YOLOv8 smoke service argument contract: `YoloWorkerSmokeTestService` should pass the selected adapter and configured confidence/image-size into worker `--smoke-test` execution. The focused smoke-service coverage now locks the YOLOv8 local-source argument shape as `--model yolov8`, `--conf 0.01`, and `--img-size 64`, matching the verified local adapter app-fixture smoke. This is C# process-argument coverage only; it does not prove model accuracy or replace a real operator dataset gate. Covered by isolated test build, `--yolo-worker-smoke-service`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-04 YOLOv8 smoke status mask-count contract: successful current-image smoke status should distinguish segmentation polygon candidates from bbox-only results by appending a mask count when `YoloWorkerSmokeTestResult.Candidates` contains `segmentationType=polygon` or at least three polygon points. The wording belongs in `WpfDetectionResultPresentationService`; shell code-behind should only apply the built status. This is status text only and does not change WPF layout, detection execution, label confirmation, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-detection-result-presentation`, `--yolo-worker-smoke-service`, `--priority-workflow-docs`, and `git diff --check`.

2026-07-04 YOLOv8 real TCP segmentation smoke contract: the existing `--real-yolo-smoke` harness should keep its default YOLOv5-compatible behavior, but when `LABELING_SMOKE_EXPECT_SEGMENTATION=true` is explicitly set it should assert that the real local YOLOv8 TCP result preserves polygon candidates and that confirmation saves non-empty segment JSON plus mask PNG artifacts. This is a tiny app-fixture plumbing gate only; it does not judge model accuracy, run new training, or replace a real operator dataset gate. Covered by isolated test build, local-source YOLOv8 `--real-yolo-smoke` with `C:\Git\yolov8\labeling_tcp_client.py`, `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt`, `LABELING_SMOKE_CONFIDENCE=0.01`, `LABELING_SMOKE_IMAGE_SIZE=64`, artifact `artifacts\real-yolo-smoke\20260704-010540\summary.txt` recording `polygonCandidateCount=1`, `segmentExists=True`, and `maskExists=True`, plus content assertions for segment polygons/points, non-empty mask pixels, `--detection-result-segmentation-confirm`, `--python-detection-result-protocol`, and `--yolo-worker-smoke-service`.

2026-07-04 WPF confirmed AI polygon save/training contract: in segmentation-purpose datasets, confirmed AI candidates from Candidate Review that include at least three polygon points should be saved as segmentation artifacts as well as their existing box ROI representation when bbox bounds are present. `BuildAnnotationSegments()` owns this conversion, clamps/normalizes candidate polygon points to the active image size, and leaves manual polygon/raster-mask handling unchanged. The focused WPF gate covers both a direct save-path fixture and the actual Candidate Review confirm command, writes segment JSON and mask PNG artifacts, reloads the saved polygon point, asserts the mask is non-empty, runs `YoloTrainingWorkflowService.TryPrepareTrainingDataset` so the confirmed polygon exports as an Ultralytics YOLO segment `labels/*.txt` line, and sends a mocked YOLOv8 training packet with `task=segment`, `weight=yolov8n-seg.pt`, and the fixture `data.yaml`. This is WPF persistence, training-label preparation, and packet coverage only; it does not change detection execution, candidate overlay rendering, WPF layout, or Viewer/OpenGL/ROI/brush/eraser hot paths. Covered by isolated test build, `--wpf-candidate-polygon-training-flow`, `--wpf-segmentation-object-verification`, and `--detection-result-segmentation-confirm`.

2026-07-04 YOLOv8 local adapter current-state recheck: the local `C:\Git\yolov8` source workflow still compiles `labeling_tcp_client.py`, imports editable Ultralytics `8.4.86` from `C:\Git\yolov8\ultralyticsMaster`, passes adapter `--self-test`, and returns one app-fixture `Defect` polygon candidate from `runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt` at `--model yolov8 --conf 0.01 --img-size 64`. This is tiny-fixture runtime evidence only; it does not replace a real operator dataset gate.

2026-07-04 YOLOv8 real app-saved SEG artifact smoke: a real app-saved SEG artifact from `artifacts\run\Debug\DATA\Segmentagtion_Dataset_ObjectDetection_20260704_065650` was staged into an ignored minimal train/valid dataset and trained through the local `C:\Git\yolov8\labeling_tcp_client.py` TCP `StartTraining` path with `task=segment`, `epochs=1`, `imgSize=64`, `batchSize=1`, `workers=0`, and local `yolov8n-seg.pt`. The run produced `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-real-app-segmentation-smoke\weights\best.pt` and `last.pt`; the generated `best.pt` loaded through local adapter `--smoke-test`, and at `--conf 0.001` returned polygon candidates with normalized polygon points. This is local YOLOv8 training/model-load plumbing evidence with a real app-saved SEG artifact, not production accuracy; the source operator dataset still needs enough non-duplicated train/valid segmentation labels.

2026-07-04 YOLOv8 SEG training readiness guidance: `WpfTrainingReadinessPresentationService` should keep segmentation-purpose train/valid blockers operator-readable. When SEG training lacks saved train/valid mask labels, the status explains that both splits need saved mask labels instead of exposing raw validator text. When YOLOv8 segment-label export lacks polygon segment JSON, the failure summary guides the operator back to saving brush/polygon labels. Covered by isolated test build, `--wpf-training-readiness-presentation`, `--dataset-readiness-purpose`, and `git diff --check`.

2026-07-04 SEG brush preview image-switch reset: loading a new image from the WPF labeling shell must clear the previous image's GPU/FBO mask stroke preview as well as manual segments, queued stroke commits, mask overlays, and stroke-tracking state. This prevents an unsaved brush preview from visually carrying over to the next image while preserving the existing brush MouseMove/MouseUp performance path. Covered by isolated test build, `--wpf-mask-drag-performance`, `--wpf-segmentation-object-verification`, `--wpf-image-queue-click-load-path`, and `git diff --check`.

2026-07-04 SEG purpose tool visibility contract: a segmentation-purpose dataset should expose brush-first tool scope in both the learning guide and canvas toolbar: `Brush`, `Eraser`, `Polygon`, `Select`, `PanZoom`, and should select `Brush` when entering segmentation from the default select state. Object detection should still expose `Select`, `Rectangle`, `PanZoom`. Purpose changes should refresh canvas workflow context, annotation visibility, readiness, and YOLO training-step completion. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-segmentation-object-verification`, `--wpf-labeling-shell`, and 1920x1080 visual smoke capture `artifacts\ui\wpf-seg-purpose-brush-1920.png`.

2026-07-06 workflow stage top subnavigation contract: the top workflow subnavigation rail should remain available across all four workflow stages, with stage-specific shortcuts only. Dataset and labeling keep their existing shortcuts; inference review exposes `AI 후보` and `현재 검사`; training/model exposes `학습/모델`, `후보 검토`, and `현재 검사`. These shortcuts should bind to the existing ShellViewModel commands instead of introducing code-behind workflow logic. This is WPF navigation/layout only and does not touch Viewer/OpenGL/ROI/brush/eraser hot paths. Covered by isolated test build, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --width 1920 --height 1080`, four 1920x1080 current captures under `artifacts\ui\top-subnav-current`, representative public tutorial/README image refreshes under `docs\tutorial\images`, standalone tutorial image embedding verification, and `git diff --check`.

2026-07-06 EXE top subnavigation click-through contract: the real EXE should let an operator click all four top workflow stage buttons and see only that stage's compact `하위 작업` shortcuts. Dataset exposes dataset/class shortcuts, labeling exposes label/tool/class shortcuts, inference exposes AI-candidate/current-inspection shortcuts, and training/model exposes model/candidate/current-inspection shortcuts. Enabled shortcuts should be invokable without the subnavigation disappearing. Covered by isolated test build and `--exe-top-subnavigation-smoke` with capture `artifacts\ui\exe-top-subnavigation-smoke.png`.

2026-07-06 YOLOv8 SEG readiness coverage-warning contract: `YoloDatasetDiagnosticsService.BuildQualityWarnings` should warn, without blocking training, when a segmentation-purpose dataset has fewer than five positive train or valid mask images, or when OK/background empty labels are at least three times the positive mask images in either split. The warnings use `YoloDatasetStatistics` split-level empty-label counts and flow through the existing WPF readiness/dashboard warning path. This is dataset readiness diagnostics only and does not run training, judge accuracy, change WPF layout, or touch Viewer/OpenGL/ROI/brush/eraser hot paths. Covered by isolated test build, `--dataset-readiness-purpose`, `--wpf-training-readiness-presentation`, and `--wpf-training-dashboard-quality`.

2026-07-06 public tutorial screenshot contract: README and tutorial screenshots that represent the current workflow should be generated from current 1920x1080 WPF visual-smoke captures, and public screenshot path fields should be redacted to non-local placeholder text. The standalone tutorial HTML should embed the refreshed PNGs as base64 rather than retaining stale image data. This is documentation evidence only; it does not change product runtime behavior. Covered by isolated test build, WPF visual-smoke captures under `artifacts\ui\tutorial-refresh`, `--priority-workflow-docs`, standalone image-embed count checks, and `git diff --check`.

2026-07-07 image queue compact layout contract: the WPF image queue should prioritize the file list over duplicated secondary controls. The repeated folder path row and quick-filter shortcut grid stay collapsed by default because the primary folder buttons plus status filter combo/search already cover normal operation. The queue column may use 320px at 1920x1080, but must not cause canvas toolbar wrapping. This is WPF layout only and does not change queue loading, keyboard navigation, label persistence, Viewer/OpenGL/ROI/brush/eraser paths, training, or inference execution. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--wpf-image-queue-keyboard-navigation`, `--wpf-image-queue-click-load-path`, `--wpf-image-queue-click-loads-canvas`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 AI candidate left-panel compact layout contract: the WPF Candidate Review panel should keep operator-critical state and actions visible while hiding repeated explanatory detail text by default. The panel should still expose AI-candidate scope, role-card titles/results, selected candidate summary, confirm/skip/navigation actions, model validation status, confidence slider, and review history. Panel detail text, role-card detail text, action guide text, and model-comparison detail/action text stay bound for accessibility/diagnostics but are collapsed in the default layout. This is WPF layout/presentation only and does not change candidate selection, confirmation, saving, training, inference, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-labeling-shell`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 saved-label left-panel compact layout contract: the WPF Object Review/Saved Label panel should keep save state, selected object details, class editing controls, and the object list visible while hiding repeated explanatory detail text by default. Mode detail, action guide, and selected-task action detail text stay bound for accessibility/diagnostics but are collapsed in the default layout. This is WPF layout/presentation only and does not change object selection, label saving/reopening, template labeling, training, inference, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-object-review-panel`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 guide/tools left-panel compact layout contract: the WPF Learning Workflow guide/tools panel should keep dataset purpose selection, dataset setup/open actions, current-step cards, tutorial access, YOLO dataset structure, and workflow cards available while hiding repeated helper text and first-run shortcut tile lists by default. Dataset-purpose tool summary, guide-tools helper detail, first-run checklist tiles, first-run sample-path summary, and first-run sample-path shortcut tiles stay bound and named for accessibility/diagnostics but are collapsed in the default layout. This is WPF layout/presentation only and does not change dataset creation/opening, annotation tool selection, template labeling commands, training, inference, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 guide/tools current-task panel contract: the labeling-stage WPF guide/tools panel should show a compact current-task card first, fed by the live canvas workflow step/tool/next-action text. Dataset purpose/setup/storage-structure content should be visible in dataset onboarding, not mixed into the default labeling guide. YOLO progress, tutorial, workflow-step, and annotation-tool details should remain available behind the collapsed `LabelingGuideDetailsExpander` instead of filling the first viewport. The live task context must distinguish real annotation tools from workflow modes: brush/box/polygon editing may show `도구: ...`, while image queue/current inspection/AI review states should show `모드: ...` and must not keep a stale annotation tool name next to a non-labeling next action. This is information-architecture/layout only and does not change dataset creation/opening, annotation tool execution, template labeling commands, training, inference, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, 1920x1080 before/after visual smoke captures `artifacts\ui\wpf-guide-tools-task-panel-before-1920.png`, `artifacts\ui\wpf-guide-tools-task-panel-after-1920.png`, and `artifacts\ui\wpf-labeling-guide-current-task-after-1920.png`, plus `git diff --check`.

2026-07-08 labeling guide current-task naming contract: in the labeling stage, the guide/tools shortcut should read as current work, not as a broad tool/manual bucket. The top shortcut and collapsed rail should show `작업` with a clipboard/current-work icon, while the expanded right workflow title for `WpfRightWorkflowShortcut.LabelingGuide` should show `현재 작업`. Tooltips/detail text should point to the current-image next action. Public tutorial 03 screenshots and the standalone embedded image should not show the older `도구` shortcut or stale `3 추론 검토` stage wording. This is visible wording/information architecture only and does not change commands, annotation tools, template labeling, training, inference, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, `--priority-workflow-docs`, standalone image hash check, and 1920x1080 captures `artifacts\ui\wpf-labeling-current-work-guide-rename-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-work-guide-rename-after-20260708-1920.png`, and `artifacts\ui\wpf-labeling-current-work-icon-after-20260708-1920.png`.

2026-07-08 labeling guide optional-details header contract: in the labeling-stage current-task guide, the secondary collapsed details expander should read `필요할 때만: 학습·검사 세부` and stay collapsed by default. Do not relabel it back to a broad `도움말/세부 도구` bucket, because that weakens the current-task-first information architecture. This is header wording only and does not change the detailed training/check panels inside the expander. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, and 1920x1080 captures `artifacts\ui\wpf-labeling-details-expander-header-before-20260708-1920.png` and `artifacts\ui\wpf-labeling-details-expander-header-after-20260708-1920.png`.

2026-07-08 labeling guide current-task flow-hint contract: in the labeling-stage current-task guide, the state-specific task sequence should render as a quiet single flow line, not as three bordered boxes that look like extra action buttons. Keep `CurrentLabelingTaskChecklistSummaryText` ViewModel-owned and derived from the same live state as the existing checklist values: sample/default `흐름: 이미지 > 열기 > 라벨`, labeling `흐름: 그리기 > 저장 > 다음`, inference `흐름: 검사 > 확인 > 검토`, and AI candidate review `흐름: 확인 > 확정 > 스킵`. This is first-viewport information hierarchy only and does not change commands, annotation tools, training, inference, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, and 1920x1080 captures `artifacts\ui\wpf-labeling-current-task-flow-before-20260708-1920.png` and `artifacts\ui\wpf-labeling-current-task-flow-after-20260708-1920.png`.

2026-07-08 labeling guide current-task action-emphasis contract: in the labeling-stage current-task card, the step badge may use the accent color, but the explanatory next-action sentence should use `PrimaryTextBrush`, not the global `AccentBrush`, so ordinary task guidance does not read as a warning or error. This is visual hierarchy only and does not change commands, annotation tools, training, inference, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, and 1920x1080 captures `artifacts\ui\wpf-labeling-current-task-action-emphasis-before-20260708-1920.png` and `artifacts\ui\wpf-labeling-current-task-action-emphasis-after-20260708-1920.png`.

2026-07-08 labeling guide current-image card-caption contract: in the labeling-stage current-task guide, the expanded panel title should remain `현재 작업`, but the first task card caption should read `현재 이미지` so the same label is not repeated and the card scope is clear. This is wording hierarchy only and does not change commands, annotation tools, training, inference, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, and 1920x1080 captures `artifacts\ui\wpf-labeling-current-task-card-caption-before-20260708-1920.png` and `artifacts\ui\wpf-labeling-current-task-card-caption-after-20260708-1920.png`.

2026-07-08 labeling guide current-task card-border contract: in the labeling-stage current-task guide, keep the card border neutral with `BorderBrushDark`; reserve the global red accent for the step badge and actual action controls. This prevents ordinary current-image guidance from reading as a warning/error block. This is visual hierarchy only and does not change commands, annotation tools, training, inference, model comparison, or Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, and 1920x1080 captures `artifacts\ui\wpf-labeling-current-task-card-border-before-20260708-1920.png` and `artifacts\ui\wpf-labeling-current-task-card-border-after-20260708-1920.png`.

2026-07-07 labeling-stage guide visual-smoke contract: use `--review-tab labeling-guide` or `--review-tab guide-labeling` when visual evidence must represent the real `2 라벨링` guide/tools current-task state. Keep `--review-tab guide` as dataset-onboarding evidence. The labeling-stage alias should set `WpfShellWorkflowStage.Labeling`, activate `WpfRightWorkflowShortcut.LabelingGuide`, and select `LearningReviewTab`, so UI captures do not accidentally validate dataset setup when the user is reviewing annotation-stage guidance. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, and 1920x1080 capture `artifacts\ui\wpf-labeling-guide-current-task-after-1920.png`.

2026-07-07 class setup left-panel compact layout contract: the WPF Class Catalog panel should keep guide title, class count/selection summary, current drawing class title, class name input, add/rename/delete controls, color expander, edit status, and recipe class list visible while hiding repeated explanatory detail text by default. Class guide detail, current drawing class detail, and class next-action detail stay bound and named for accessibility/diagnostics but are collapsed in the default layout. This is WPF layout/presentation only and does not change class add/rename/delete behavior, class color application, label saving, training, inference, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 training settings left-panel compact layout contract: the WPF Training Settings panel should keep training readiness, current settings, preset action, advanced parameter controls, training commands, progress/status, and post-training candidate actions available while hiding repeated guide text by default. The recommendation paragraph, advanced-parameter guide lines, and split-policy hint stay bound and named for accessibility/diagnostics but are collapsed in the default layout; input controls retain their existing tooltips. This is WPF layout/presentation only and does not change training readiness rules, training execution, model selection, inference, annotation persistence, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-training-settings-panel`, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 model center left-panel compact layout contract: the WPF model-center panel should keep the training status, priority model-application card, model registry summary, adoption decision card/detail expander, dataset-check expander, training buttons, and runtime-management expanders available while hiding the duplicated lifecycle detail table by default. `YoloModelLifecycleDetailPanel` stays named and its child text bindings remain intact for diagnostics/accessibility, but it is collapsed in the default layout because the same current/candidate/adoption/next-action state is already visible in higher-priority cards. This is WPF layout/presentation only and does not change model registry state, model adoption commands, training execution, inference, annotation persistence, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 YOLOv8 SEG model comparison polygon-example contract: `WpfModelComparisonReviewService` should parse YOLO segmentation polygon label rows (`class x1 y1 x2 y2 ...`) with optional trailing confidence and convert their normalized extents into the existing Candidate Review focus-box examples. This lets held-out YOLOv8 SEG model comparisons surface candidate-only/baseline-only polygon changes instead of silently ignoring non-box rows. This is comparison-result parsing only; it does not run real training/inference, change promotion thresholds, alter WPF layout, or touch Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-model-comparison-review-service`, `--wpf-model-comparison-heldout`, and `--wpf-model-comparison-run-service`.

2026-07-07 model history rejected-candidate apply guard contract: model history should keep rejected candidates visible for traceability, but a rejected non-current candidate must not be directly promotable to the current inspection model from the history action even when its `best.pt` still exists. Non-current, non-rejected candidates with existing weights should remain intentionally promotable. This is model-history presentation/action gating only; it does not change registry persistence schema, run training/inference, alter WPF layout, or touch Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-labeling-shell`, and `--model-registry`.

2026-07-07 model candidate decision wording contract: model-candidate save/reject/rejected/saved/no-candidate and reject-result text should stay owned by `WpfModelCandidateDecisionPresentationService`, not shell code-behind. The rejected state should be readable Korean guidance that the candidate was not adopted as the inspection model, and `WpfCandidateReviewPanel.xaml` should keep `ModelValidationRoleDetailText` collapsed by default while preserving the binding for accessibility/diagnostics. This is presentation and compact-layout protection only; it does not run training/inference, change registry persistence schema, or touch Viewer/OpenGL/ROI/brush/eraser paths. Covered by isolated test build, `--wpf-labeling-shell`, `--model-registry`, `--wpf-candidate-review-panel`, `--mvvm-infra`, 1920x1080 Candidate Review visual smoke captures under `artifacts\ui`, and `git diff --check`.

2026-07-07 checked-in Noah DLL output-copy contract, updated 2026-07-15 for clean runtime alignment: `OpenVisionLab.LabelingStudio.csproj` must reference and copy `dll\Lib.Common.dll` and `dll\Lib.OpenCV.dll` into the EXE output after build. `Directory.Build.props` owns `OpenCvSharpVersion=4.5.5.20211231`, and the app, tests, ImageCanvas, and Display.Core must use matching `OpenCvSharp4` and `OpenCvSharp4.runtime.win` package references rather than checked-in OpenCvSharp HintPaths. The app's post-build copy may place only the matching native runtime and ffmpeg assets at the root; it must not overwrite the managed package output with the old netstandard binary. This prevents both clean-checkout compile failures and the managed-4.4/native-4.5.5 `Cv2.FindContours` access violation reproduced in isolated output. Covered by the required isolated build, package/output hash checks, `--priority-workflow-docs`, segmentation focused tests, and the complete no-argument suite.

2026-07-07 circular SEG 20-label EXE rerun contract: after a current `artifacts\run\Debug` build, the real EXE circular segmentation workflow should load `D:\circular_defect_labeling_dataset_v1\images`, save 20 NG segmentation masks plus 20 OK/background empty labels, split positives into at least 10 train, 5 valid, and 5 test segment artifacts, train YOLOv8 SEG through the local `C:\Git\yolov8` adapter, apply `openvisionlab-yolov8-segment\weights\best.pt`, and complete current-image inference with a visible AI candidate. This is EXE operating-path and data-evidence coverage only; it does not claim production model accuracy or adoption readiness. Covered by app build, checked-in/output `Lib.OpenCV.dll` hash match, and `--exe-circular-segmentation-workflow --label-count 20` artifact `artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147`.

2026-07-07 YOLOv8 SEG 20-label held-out comparison contract: use `artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data.yaml` when a comparison needs a current EXE-generated circular SEG test split with enough evidence. The `yolov8-seg-20label-baseline-vs-userseed-20260707\20260707-222120` comparison closed the quantity blockers (`11/10` labeled images, `5/5` positive segment labels, `5/5` positive segment images) but still kept promotion on `hold` because candidate precision was `0.098 < 0.1` and UI-threshold candidates at confidence `0.25` were `0`. This is adoption-safety evidence, not a production-ready model approval.

2026-07-07 YOLOv8 SEG 20-label fine-tune promotion contract: the local YOLOv8 fine-tune run `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt` should be treated as the first promotable circular SEG candidate from the current EXE artifact, not as broad production accuracy. The held-out comparison `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260707\20260707-222900\comparison-summary.json` passed the evidence gates (`11/10` labels, `5/5` positive segment labels, `5/5` positive segment images), scored precision `1.0`, recall `0.589`, mAP50 `0.767`, mAP50-95 `0.257`, produced UI-threshold candidates `2` at confidence `0.25`, and wrote `promotion.recommendation=promote`. Before saving it as the inspection model, review its candidate examples in the UI because the held-out set is still small.

2026-07-07 YOLOv8 SEG promotable comparison Candidate Review contract: Candidate Review should translate a `promotion.recommendation=promote` reason into operator-facing Korean that preserves the mAP/precision/recall evidence and points to reviewing examples before saving as the inspection model. YOLO bbox fallback parsing must stay limited to 5- or 6-token box rows so long segmentation-like rows cannot be misread as boxes with coordinate values as confidence. The visual-smoke summary injection path should allow a real `comparison-summary.json` to be displayed in Candidate Review and should keep displayed counts aligned with the summary, including `baseline.uiCandidateCount=0` and `candidate.uiCandidateCount=2` for `yolov8-seg-20label-baseline-vs-finetune80-20260707\20260707-222900`. Covered by isolated test build, `--wpf-model-comparison-review-service`, `--wpf-model-comparison-heldout`, and current-source 1920x1080 capture `artifacts\ui\wpf-model-comparison-finetune-promote-after-1920.png`.

2026-07-07 YOLOv8 SEG fine-tune direct adapter smoke contract: the promotable fine-tuned weight `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt` should load through the local `C:\Git\yolov8\labeling_tcp_client.py --smoke-test` path with `classNames=["NG"]`. At confidence `0.25`, the held-out `025_NG.png` and `024_NG.png` smoke images should each return one `NG` polygon candidate, while `011_OK.png` should return zero candidates. Covered by JSON artifacts under `artifacts\yolo-smoke\finetune80-20260707`; this proves adapter/model-load behavior for selected examples, not recipe adoption or broad production accuracy.

2026-07-08 YOLOv8 SEG comparison predict-label contract: for local Ultralytics YOLOv8 comparisons, `scripts\compare-yolo-models.ps1` should keep validation metrics and UI review evidence separate. `model.val` remains the metric source, but `labelsPath`, `confidence.uiCandidateCount`, `confidence.thresholdSweep`, and Candidate Review examples should come from a separate `model.predict` label export for the selected split because that matches the app's `labeling_tcp_client.py` inference path. The corrected 40-label comparison `artifacts\yolo-model-comparison\yolov8-seg-40label-baseline-vs-finetune80-20260708-predict-ui\20260708-215754\comparison-summary.json` reports candidate precision `0.582`, recall `0.5`, mAP50 `0.683`, mAP50-95 `0.281`, UI candidates `2` at confidence `0.25`, and `promotion.recommendation=promote`; direct adapter sweep `artifacts\yolo-tcp-smoke\yolov8-seg-40label-finetune80-conf025-20260708\summary.json` agrees with 2 positive images and 0 OK images producing candidates. Focused Model Center save/adoption smoke then saved that candidate as the current inspection model and captured `artifacts\ui\wpf-model-center-real-40label-finetune-save-after-20260708-1920.png`; Candidate Review visual smoke captured `artifacts\ui\wpf-model-comparison-40label-promote-after-20260709-1920.png` with the same 2 new-model examples and promote guidance. Real current-image TCP smoke `artifacts\real-yolo-smoke\40label-current-image-025-ng-20260709\summary.txt` then confirmed `025_NG.png` into YOLO label text, segment JSON, mask PNG, and review status `Confirmed`. This is fixture-scoped circular SEG evidence only, not broad production accuracy.

2026-07-08 anomaly evaluation timestamp-summary lookup contract: Model Center anomaly evaluation lookup should match the evaluation script's normal output shape by checking a direct `classification-evaluation-summary.json`, a fixed `classification-evaluation\classification-evaluation-summary.json`, and the newest immediate `classification-evaluation-*` summary folder. Missing or inaccessible roots should leave the optional evaluation card hidden, not throw into the UI. This is lookup/presentation stability only; it does not train or promote an anomaly model. Covered by isolated test build, `--anomaly-classification-evaluation`, `--wpf-labeling-shell`, and 1920x1080 Model Center capture `artifacts\ui\wpf-model-center-anomaly-evaluation-timestamp-lookup-after-20260708-1920.png`.

2026-07-08 anomaly evaluation compact-detail contract: Model Center anomaly evaluation should keep recommendation and metrics visible, but blocker detail and next action should default behind collapsed `YoloAnomalyEvaluationDetailExpander` so the left Model Center panel does not become another always-expanded instruction stack. This is WPF presentation only; it does not change evaluation logic, model training, or adoption rules. Covered by isolated test build, `--wpf-labeling-shell`, `--anomaly-classification-evaluation`, `--wpf-responsive-layout --width 1920 --height 1080`, and 1920x1080 capture `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`.

2026-07-08 anomaly evaluation explicit-adopt guard contract: when reading `classification-evaluation-summary.json`, the report should be adoptable only when `promotion.recommendation=adopt` is explicit and no hold reasons are present. Empty reports, missing `promotion`, or `promotion.recommendation=hold` remain non-adoptable even if metrics look strong. This keeps manual/older/incomplete summaries fail-closed. Covered by isolated test build, `--anomaly-classification-evaluation`, and `--wpf-labeling-shell`.

2026-07-12 expanded current-work layout contract: learning progress chips, diagnostic metrics, comparison/status text, and template workflow steps should use the available panel width, wrap rather than ellipsize essential text, and remain reachable through the outer panel scroll. Dataset-purpose badges should occupy stable right-aligned status columns in dataset selection and the current-dataset shell header. Covered by isolated test build, `--wpf-learning-workflow-panel`, `--wpf-dataset-setup-ui`, `--wpf-labeling-shell`, responsive layout checks at 1920x1080 and 1366x768, and current-build before/after captures under `artifacts\ui\20260712-user-reported-ui-mask-regression`.

2026-07-12 raster-mask persistence/export contract: a segmentation object saved as a raster mask must reopen from its sibling class-index PNG without collapsing to the JSON bounding rectangle, and the next YOLO segment-label export must use managed mask contours. For legacy untyped axis-aligned rectangle JSON, matching class pixels in the sibling PNG are authoritative whether they are irregular or fill a solid rectangle; explicit `GeometryType=Polygon` records remain polygons. This prevents the same OK label from switching between polygon and mask merely because an NG class punches a hole in its mask. Concavities, cutouts, disconnected regions, and the solid-rectangle legacy case are covered by `--segmentation-annotation-storage`, with object reload and label-flow coverage from `--wpf-segmentation-object-verification` and `--wpf-candidate-polygon-training-flow`. Existing operator sidecars are not rewritten implicitly.

2026-07-12 existing-dataset visual-smoke safety contract: ROI-only WPF visual smoke with `--dataset-output-root` must work from an image-free temporary copy of dataset sidecars rather than writing review or annotation state into the supplied operator root. The focused actual-dataset run completed with identical before/after `review-status.json` SHA-256 values. This is test-harness data protection only and does not change production dataset loading or image-queue behavior.

2026-07-12 SEG raster-mask class-badge contract: every visible committed raster mask must keep its class identifiable on the canvas without requiring object selection. Unselected masks show a compact class badge but no bounding rectangle; selected masks keep the existing full edit marker. Mask labels use `SEG <index> <class>`, and compact rendering preserves the class name rather than truncating it to the `SE` prefix. Covered by isolated build, `--wpf-segmentation-object-verification`, shared detection/ROI overlay regressions, and 1920x1080 current-build evidence under `artifacts\ui\20260712-seg-mask-class-label`.

2026-07-12 legacy template-geometry evidence: the inspected 125-image operator dataset contains 125 Version 1 segment files and 139 untyped rectangle records. `Teaching_0` alone preserves the original circular OK brush mask; 110 other OK-only masks are solid rectangles and 14 OK+NG masks are rectangular OK regions with NG pixels replacing part of them. Do not describe those historical files as a current display conversion. Current template matching preserves registered polygon/raster source geometry, while any existing-data rewrite requires a separate backed-up dry-run migration decision.

2026-07-16 historical SEG remediation dry-run contract: `YoloSegmentationHistoricalRemediationAuditService.Build` must remain read-only for all operator annotation, mask, YOLO label, recipe, and model artifacts. It reuses the normal legacy-mask compatibility and YOLO label conversion rules to report, per legacy candidate image, old/proposed geometry, point counts, mask pixels, a proposed-but-not-created backup directory, and an exact label diff. `ExportMarkdown` may write only `reports\segmentation-remediation-dry-run.md`; it must not create the proposed backup root or rewrite any dataset sidecar. The `SEG review` dashboard action starts that report through the normal status/log path. A current in-memory template source may be excluded; if none is available, the report must disclose that no source exclusion was applied. Covered by the isolated build, `--segmentation-historical-remediation-audit`, `--wpf-labeling-shell`, `--wpf-learning-workflow-panel`, `--wpf-segmentation-object-verification`, `--dataset-readiness-purpose`, `git diff --check`, and current-build evidence `artifacts\ui\20260716-segmentation-remediation-audit\after-1920.png`.

Actual active-data report evidence (2026-07-16): with `Teaching_0` excluded, the report at `artifacts\run\Debug\DATA\Segmentagtion_Dataset_ObjectDetection_20260704_065650\reports\segmentation-remediation-dry-run.md` found 124 images / 138 records, 0 unresolved records, and 0 YOLO label differences. All source artifacts kept their SHA-256 identities. This proves current training labels already follow the stored masks; it does not establish that every historical rectangular mask has the intended business geometry. Do not convert the report into an automatic migration or retraining trigger. Require an operator semantic decision and a selected-image visual audit before any data rewrite.

2026-07-12 workspace-resize and deep-report layout contract: the WPF shell exposes native column splitters between workflow/canvas and canvas/image queue. The workflow pane is bounded to 72-640px, the queue to 260-640px, and the canvas keeps at least 420px. Collapsing and reopening the workflow pane restores its last expanded width for the current session. Deep YOLO report/history text wraps and remains reachable through the outer guide scroll. Covered by isolated build, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, responsive checks at 1920x1080 and 1366x768, and current-build captures under `artifacts\ui\20260712-queue-template-resize`.

2026-07-12 SEG template shape-transfer contract: template matching determines location, while the registered source annotation determines output geometry. Object-detection sources remain rectangles; SEG polygon sources are transformed polygons; SEG brush sources are transformed raster masks. Rectangle fallback is allowed only when registered SEG source geometry is unavailable. Current-image and batch paths share `TemplateMatchingBatchAutoLabelService.BuildSegmentsByClass`. Covered by `--wpf-template-current-image-no-candidate`, `--template-batch-autolabel-storage`, `--template-guide-ux`, `--segmentation-annotation-storage`, and `--wpf-segmentation-object-verification`.

2026-07-12 image-queue detail-refresh contract: background detail loading must not recalculate the full YOLO training/model completion state for every queue row. Apply row state without that refresh and perform one completion refresh after the scan. On the actual 97-image SEG train folder this reduced the measured detail scan from 3555.8ms to 2474.0ms; the 1200-item construction gate remained 142.8ms with one collection reset and lazy thumbnails. The later row-selection contract below separately closes the click-triggered filtered-view refresh.

2026-07-12 image-queue row-selection contract: selecting an existing DataGrid row must open the supplied `WpfImageQueueItem` directly and must not call `ICollectionView.Refresh`. The attached selection command is an event-order fallback only and must not duplicate the bound ViewModel callback. Runtime instrumentation must report zero filter evaluations per click. Current evidence is 36.1ms average on the 97-item SEG queue and 35.5ms average after all 125 `D:\LabelingData\Test01\Images` detail rows loaded. Covered by the queue performance smoke, click/canvas, keyboard, status/filter, selection-service, shell, labeling-session, SEG navigation-save, and large-folder gates.

2026-07-13 Python worker identity contract: training or inference must not accept a TCP connection solely because it arrived after process start. Readiness sends a request-correlated `ModelStatus` and requires the configured normalized weight path plus the configured engine when the worker reports it. The listener keeps one active worker, mismatched workers are dropped, and WPF/application shutdown stops the managed process. Covered by `--python-model-status-protocol`, `--wpf-yolo-training-session-smoke`, the required isolated build, and current local YOLOv8 SEG smoke evidence under `artifacts\real-yolo-smoke\20260713-current-seg-worker-identity`.

2026-07-13 training model presentation contract: the training panel must describe the actual engine and dataset task. YOLOv8 segmentation displays `YOLOv8 SEG` with `yolov8n-seg.pt`; YOLO11 remains only a selected-profile presentation and must not imply runtime readiness; ONNX remains inference-only. YOLOv5 structure/start-weight selectors remain editable only under a YOLOv5 profile. Covered by `--wpf-training-settings-panel`, `--wpf-yolo-training-session-smoke`, and 1920x1080 evidence under `artifacts\ui\20260713-yolo-engine-worker-guard`.

## 2026-07-13 Completion Disposition After Commercial Reassessment

Treat the following intended local-workstation areas as complete and protected. Do not reopen them for general polish or competitor parity; require a reproduced defect, a failed focused gate, or an explicit product-direction change:

- Dataset/purpose setup, class catalog, detection boxes, and current supported detection/segmentation import-export formats.
- SEG brush/eraser/polygon tools, raster/polygon persistence, managed contour export, mask class badges, and contour-only SEG candidate rendering.
- Pending-save navigation, save/reopen, queue click/keyboard behavior, measured row-switch performance, and current-session pane splitters.
- Current-image and batch template transfer when valid source SEG geometry exists.
- Candidate Review, image-level QA state/filter/reason/report, model comparison, rejected-history guard, confidence-match guard, and current/candidate/history adoption flow.
- Local YOLOv8 source/venv/TCP-adapter training and inference plumbing, task-aware run/weight selection, engine/task-aware training presentation, worker identity, and managed-worker shutdown.
- Current-work/left-panel information architecture, optional empty Candidate Review state, clipping corrections, public workflow documentation, CI checks, and bundled DLL output-copy contract.

This closure does not include historical operator-data correction, SEG or anomaly production accuracy, independent domain-shift evidence, YOLO11 readiness, foundation-model assistance, collaboration/workforce, video/tracking/keypoints/3D, or a broad API/platform. In particular, stable SEG annotation mechanics must not be cited as proof that models trained from the predominantly four-point historical labels are production-ready.

2026-07-14 cross-engine object-detection comparison contract: Training/Model compares YOLOv5 Detect and YOLOv8 Detect with independent local Python/source/weight settings, the same image size, and `batch=1`. It prefers `test`; when test is empty it may use `val` only as an engine benchmark, and both the raw summary/report and Candidate Review must say training validation is not model-replacement evidence. Precision, recall, mAP50, mAP50-95, inference time, and model Takt remain visible; model Takt is preprocess + inference + postprocess per image, not WPF/TCP, camera, PLC, or equipment-cycle time. Label/task mismatch fails closed, cross-engine save/reject remains hidden, and YOLOv8 folder connection resolves Detect/SEG/Classification weights by dataset purpose. Covered by the required isolated build, runtime-connection and comparison focused tests, Candidate Review/shell tests, real local adapter smoke, and current-build 1920x1080 evidence under `artifacts\ui\20260714-yolov5-yolov8-real-validation`. Real validation metrics exist, but the empty test split and one-NG validation set keep model adoption and broad readiness unverified.

2026-07-14 object-detection purpose and YOLOv8 restart contract: generic project, model, candidate, and runtime settings saves must preserve the authoritative `ProjectSettings.DatasetPurpose`; only explicit dataset-purpose selection may change it. An actual current-build EXE smoke creates an ObjectDetection recipe, saves the local YOLOv8 editable runtime and trained Detect `best.pt`, closes the EXE, reopens the saved recipe, verifies the visible engine/weight, and completes first inference with one UI-threshold candidate. Covered by `--wpf-dataset-setup-request`, `--wpf-startup-dataset-restore`, `--wpf-labeling-shell`, and `--exe-yolov8-detect-restart-smoke`; evidence is under `artifacts\exe-yolov8-detect-restart-smoke\codex_yolov8_detect_restart_20260714_193745`. This is a runtime-persistence contract, not independent model-quality or adoption evidence.

2026-07-14 dataset isolation and anomaly-classification restart contract: creating a blank recipe must not clone the previous recipe's model registry, weights, or image root. A queue root switch may retain a selected image only when that exact path belongs to the newly enumerated queue; an empty root clears the active workspace image, display-manager image, canvas texture/overlays, annotation state, and candidate state. Switching from SEG to AnomalyDetection defaults to Select so a stale Brush/Eraser tool cannot rewrite the purpose. An actual current-build EXE smoke saves the local YOLOv8 classification runtime, classification `best.pt`, image size 128, normal/abnormal class mapping, and mapping threshold 0.8; after close/reopen, first inference returns one `abnormal` candidate and persists `Abnormal`. Covered by `--wpf-dataset-setup-request`, `--wpf-image-queue-root-switch`, `--wpf-learning-workflow-panel`, `--wpf-startup-dataset-restore`, `--wpf-segmentation-object-verification`, `--wpf-labeling-shell`, and `--exe-yolov8-anomaly-restart-smoke`; evidence is under `artifacts\exe-yolov8-anomaly-restart-smoke\codex_yolov8_anomaly_restart_20260714_204940`. This closes state isolation and restart/runtime behavior for the tested profile, not independent production-camera model quality.

2026-07-14 repeated cross-engine model-Takt contract: built-in YOLOv5-versus-YOLOv8 analysis uses five native validation timing samples per engine at the same image size and `batch=1`. The first run owns accuracy and Candidate Review candidates; later runs collect timing only. JSON and Markdown preserve every sample, median, range, and count, Candidate Review displays the median/range/`n`, and a missing requested timing sample fails closed. Same-engine candidate validation remains single-run. Covered by the required isolated build, PowerShell parse, model-comparison run/review, Candidate Review, shell gates, an actual final-source five-repeat local run under `artifacts\yolo-model-comparison\real-test01-controlled-v5s-v8n-app-repeat5-final-20260714\20260714-220448`, and 1920x1080 before/after evidence under `artifacts\ui\20260714-yolo-engine-repeat-takt`. This closes built-in repeatability/presentation only; absolute CPU timing remains load-sensitive, and the empty test split plus one-NG validation set still block adoption claims.

2026-07-15 WPF dataset quality report action contract: the dashboard quality metric is an explicit `품질 보고서` action while retaining the current problem count and `정리 필요`/`확인` state. Clicking it builds a fresh audit from the active dataset and overwrites only `dataset-quality-audit.md` under that dataset root. The action does not modify labels, images, split settings, training state, or model settings. Covered by the required isolated build, `--dataset-quality-audit-export`, `--wpf-training-dashboard-quality`, `--wpf-labeling-shell`, and current 1920x1080 before/after captures under `artifacts\ui\20260715-dataset-quality-report-action`.

2026-07-15 final-verification split preset contract: `WpfTrainingSettingsPanelViewModel.ApplyFinalVerificationPresetCommand` changes only the editable split fields to validation `0%` and final verification `100%`; it must not move or rewrite existing data when clicked. The existing normal settings apply/save path remains authoritative, and a subsequently saved independent image must select only `test` through `YoloDatasetSplitService`. `빠른 추천 적용` restores `20%` validation and `0%` final verification. Keep this command disabled while general training-setting commands are busy, and do not change the verified same-stem split migration behavior in `YoloAnnotationService`. Covered by the required isolated build, training-settings/MVVM/settings/shell focused gates, the 1366x768 training responsive-layout gate, current 1920x1080 evidence under `artifacts\ui\20260715-final-verification-preset`, and refreshed public tutorial image 09/standalone HTML.

2026-07-15 Training/Model task-navigation contract: `YoloModelCenterTaskTabs` keeps exactly four first-level choices, `현황`, `데이터`, `학습/비교`, and `실행기`, above the existing model-center scroll. The choices must reuse and contextually reveal the existing dashboard, readiness/project, training/comparison, and runtime/settings controls rather than duplicating workflow state. `FocusYoloSettingsTab`, `FocusYoloTrainingSettingsTab`, and `FocusYoloModelSettingsTab` must select the matching task and expose its first useful detail. Selection may reset the panel scroll and expander presentation, but it must not alter dataset, training, inference, comparison, model registry, or persisted recipe state. Covered by the required isolated build, `--wpf-labeling-shell`, training/model-settings panel gates, responsive layout checks at 1366x768 and 1920x1080, current-build evidence under `artifacts\ui\20260715-training-subtask-navigation`, and refreshed tutorial image 09/standalone HTML.

2026-07-15 compact Training/Model comparison-summary contract: `YoloModelComparisonSummaryPanel` is conditional on both the Training/Comparison task and an existing matching `WpfModelComparisonReviewReport`. It must reuse the Candidate Review ViewModel state and show the service-owned adoption boundary plus engine metrics, model Takt median/range/count, evidence split/count, and disagreement summary. `OpenModelComparisonExamplesButton` routes to the existing Candidate Review workflow; it must not duplicate parsing, inference, comparison, or adoption state. First entry may restore the latest artifact matching the current baseline/candidate weight paths, while malformed external artifacts must not block task navigation. Covered by the required isolated build, `--wpf-labeling-shell`, `--wpf-model-comparison-review-service`, responsive checks at 1366x768 and 1920x1080, and current-build before/after evidence under `artifacts\ui\20260715-model-comparison-summary`. This is result readability only and does not change the empty-test/one-NG adoption boundary.

2026-07-15 per-machine workspace-width contract: persist only the expanded workflow pane width and image-queue width to `%LOCALAPPDATA%\OpenVisionLab\LabelingStudio\workspace-layout.json`. Clamp workflow to 280-640px and queue to 260-640px when loading or saving; missing, malformed, and out-of-range settings must fall back safely. `Reset panel widths` in settings/tools restores 340px/320px and persists that reset. Window position, dock state, canvas, recipe, and Viewer/OpenGL state remain outside this setting. Tests must isolate the storage path through `OPENVISIONLAB_LABELING_WORKSPACE_LAYOUT_PATH`. Covered by the required isolated build, `--wpf-workspace-layout`, shell/MVVM gates, responsive checks at 1366x768 and 1920x1080, and current-build before/after evidence under `artifacts\ui\20260715-workspace-layout-persistence`.

2026-07-15 model-comparison execution-history contract: `YoloModelComparisonHistoryBox` lists at most eight newest valid comparison artifacts matching both the current baseline and candidate weight paths. It must reuse `WpfModelComparisonReviewService` and Candidate Review state, skip malformed or unrelated artifacts, and keep only the newest matching run authoritative for adoption. Older selections are read-only reference views and must hold model promotion until the latest run is selected again. The selector must not rewrite comparison artifacts, weights, recipes, labels, or model-registry history. Covered by the required isolated build, model-comparison review/run, Candidate Review, shell/MVVM gates, responsive checks at 1366x768 and 1920x1080, and current-build evidence under `artifacts\ui\20260715-model-comparison-history`.

2026-07-15 compact workflow-context and left-panel-tab contract: `WorkflowContextHeader` owns a fixed 82px two-row surface. Its 48px first row keeps the four existing workflow-stage commands and stage summary; its 34px second row gives the current-dataset name, work basis, purpose, and folder actions the full header width. The existing dataset/label/work/class/candidate/model choices belong directly below the expanded left workflow-panel title in `WorkflowStageSubNavigationRail`, use one responsive `UniformGrid` row, and continue to invoke the same ViewModel commands and `ReviewTabControl` views. Active views use the panel-tab underline state; current-inspection and model-candidate actions keep the distinct action-button style. Do not restore the previous independent 54px + 36px + 48px stack, detach the task tabs back into the global header, or duplicate their commands. This layout change must not alter workflow-stage state, panel routing, dataset state, Viewer/OpenGL, ROI, brush, or eraser behavior. Covered by the required isolated build, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080 and 1366x768 responsive gates, minimum-280px panel evidence, and current-build captures under `artifacts\ui\20260715-supervisely-panel-slice`. After operator acceptance, public raw/annotated images 01 and 03 plus the README hero were refreshed from the current 1920x1080 shell. The standalone HTML was regenerated; normal and standalone renders matched and every embedded PNG hash matched its annotated source.

2026-07-15 fixed canvas annotation-tool rail contract: purpose-filtered selectable annotation tools belong in the fixed 46px `CanvasAnnotationToolRail` beside the ROI canvas, not in the wrapping save/mode/class action bar. The rail reuses `WpfCanvasPanelViewModel.AnnotationTools`, `SelectedAnnotationTool`, and the existing selection command; tool icons expose bound automation name/help text and hover tooltips. Undo, redo, and delete remain one-shot commands grouped below the selectable tools, while brush size, display mode, save, no-object completion, and active class stay in the horizontal action bar. Do not duplicate tool state, add a docking dependency for this rail, or move annotation/Viewer/OpenGL/ROI/brush/eraser processing into the view. Covered by the required isolated build, `--wpf-labeling-shell`, `--wpf-segmentation-object-verification`, 1920x1080 and object-detection/segmentation 1366x768 visual evidence under `artifacts\ui\20260715-annotation-tool-rail`, and refreshed public tutorial images 01/03 plus the README hero.

2026-07-15 labeling-only viewer-toolbar visibility contract: Labeling Studio's `MainCanvasView` hides the shared `RoiImageCanvasView` ROI/measurement toolbar because the labeling-owned canvas-left rail is the authoritative annotation-tool entry point. The hidden `ToolBarTray` column must collapse from 35px to zero so the canvas receives the width. `RoiImageCanvasView.IsViewerToolBarVisible` defaults to `true`; other shared-viewer consumers must keep the existing toolbar unless they opt out explicitly. This is viewer-chrome visibility only and must not remove or alter ROI/measurement commands, OpenGL rendering, annotation persistence, or brush/eraser behavior. Covered by the required isolated build, `--wpf-canvas-panel-commands`, `--wpf-labeling-shell`, `--wpf-segmentation-object-verification`, responsive checks at 1920x1080 and 1366x768, current-build before/after evidence under `artifacts\ui\20260715-viewer-legacy-tools`, refreshed public tutorial images 01/03 and README hero, and standalone embedded-image hash verification.

2026-07-15 model-neutral performance-comparison workspace contract: Training/Comparison keeps only a compact entry and opens one reusable modeless `WpfModelBenchmarkWindow`; comparison metrics must not return to the narrow left panel. The read-only catalog normalizes the repository's current pairwise model-comparison and anomaly-classification evaluation summaries, supports search/task filtering, allows at most six selected runs, and keeps exactly one selected baseline. Accuracy deltas require matching task, split, image count, and evaluation identity: new reports use the content fingerprint, while legacy reports without one use the exact normalized evaluation path and may not be mixed with fingerprinted reports for a quality delta. Takt deltas additionally require matching timing source, image size, batch, and repeat conditions. Mixed tasks, missing timing, and mismatched evidence must show an explicit non-comparability reason rather than a winner. Candidate Review and model adoption remain separately owned, and no report, model, recipe, label, or registry state is rewritten. Covered by the required isolated build, `--wpf-model-benchmark-window`, `--wpf-labeling-shell`, current-build 1920x1080 pair/mixed-task captures and 1366x768 responsive evidence under `artifacts\ui\20260715-model-benchmark-window`, plus the true prior inline-panel capture under `artifacts\ui\20260715-model-comparison-summary`. This closes comparison-workspace phase 1 only; new report adapters, a versioned generic schema, charts, and independent model-quality evidence remain separate needs.

2026-07-15 model-comparison evidence-fingerprint contract: every new `compare-yolo-models.ps1` report must preserve `evidence.dataYamlSha256`, `evidence.fingerprintAlgorithm=sha256-image-label-pairs-v1`, `evidence.fingerprintSha256`, and both models' `weightsSha256`. The evidence fingerprint is path-independent, order-independent, and built from each evaluated image hash paired with its answer-label hash or an explicit missing-label marker. Every new `evaluate-yolo-classification.ps1` summary must likewise preserve `weightsSha256` plus `evidence.fingerprintAlgorithm=sha256-class-image-pairs-v1` and the normal/abnormal class-image fingerprint. The benchmark catalog accepts only valid 64-hex SHA-256 values, displays compact values under the existing path cells, and must not compare a fingerprinted run against an un-fingerprinted run. Historical reports remain readable through the legacy path/split/count fallback. This is audit identity only and must not rewrite existing reports, weights, datasets, recipes, labels, or adoption state. Covered by the required isolated build, `--wpf-model-benchmark-window`, `--wpf-model-comparison-run-service`, `--anomaly-classification-evaluation`, `--wpf-labeling-shell`, parser/function checks, and current-build before/after evidence under `artifacts\ui\20260715-model-benchmark-fingerprints`.

2026-07-15 evidence-fingerprint real-runtime generation contract: the local YOLOv5/YOLOv8 comparison and YOLOv8 anomaly-classification evaluator must generate the fingerprint fields during their normal end-to-end execution, not only in source/parser tests. Object-detection proof is `artifacts\yolo-model-comparison\sha-end-to-end-runtime-20260715\20260715-202821`; its data YAML and both weight hashes match the evaluated files and its Markdown includes the identities. Anomaly proof is `artifacts\yolo-classification-evaluation\sha-end-to-end-runtime-20260715\classification-evaluation-20260715-203149`; its weight hash matches and its scoped 15-image result remains 15/15 with zero hold reasons. `WpfModelBenchmarkWindow` must load both reports and show their compact SHA values without treating mixed tasks as comparable; current evidence is `artifacts\ui\20260715-model-benchmark-fingerprints\actual-runtime-reports-conditions-1920x1080.png`. This closes report-generation integration only and must not be cited as independent object-detection or anomaly production-quality evidence.

2026-07-15 delayed startup-image preservation contract: the application-idle startup sample loader must be a no-op when the workspace already has an active bitmap or image path. It must never replace an operator-selected image or clear annotations created before the delayed callback executes. The ten-minute labeling-session smoke invokes that delayed path after two labels exist and verifies both remain; three consecutive focused runs and the complete no-argument suite passed.

2026-07-15 large-overlay status localization contract: when the existing 10,000-shape visual LOD cap is active, the canvas status bar shows `표시 ROI: 10,000+`; when inactive, it remains hidden. This is a visible-text change only and must not alter full-index selection/editing or overlay rendering behavior. Covered by the complete suite and current-build 1920x1080 evidence under `artifacts\ui\20260715-roi-lod-status-regression`.

2026-07-16 configured inspection-model profile contract: saving an existing weight from Model/Runtime settings must register that engine/purpose/project-root profile and weight in `ModelRegistry` even when the weight did not come from the immediately preceding training session. Before applying a changed engine or weight, the settings save path preserves the previously configured model so YOLOv5 and YOLOv8 ObjectDetection weights can coexist for `BuildYoloV5YoloV8DetectionRequest`. This configured-model registration must not fabricate a `TrainingRun`; it records the selected inspection model and leaves actual training history separately owned. Covered by the required app and isolated builds, `--model-registry`, `--wpf-model-comparison-run-service`, `--wpf-labeling-shell`, and the actual save/restart/first-inference EXE smoke under `artifacts\exe-yolov8-detect-restart-smoke\configured-model-registry-20260716`.

2026-07-16 Test01 same-data engine-benchmark contract: the 125-image app dataset is filename/dimension equivalent to `D:\LabelingData\Test01\Images` with only negligible JPEG re-encoding differences. The existing YOLOv5 and YOLOv8 Detect runs both use the same `data.yaml`, image size 320, batch 4, epoch 100, and seed 0. Fresh runtime evidence is `artifacts\yolo-model-comparison\test01-existing-v5-v8-current-smoke-20260716\20260716-083344`; it reproduces YOLOv5 mAP50-95 `0.464` / Takt `76.60ms` and YOLOv8 mAP50-95 `0.9213` / Takt `61.51ms`. Keep this result labeled `benchmark`: the test split is empty and validation has only one NG object, so it must not authorize model replacement.

2026-07-16 deterministic detection-benchmark snapshot and controlled-holdout contract: `scripts\create-yolo-detection-benchmark-snapshot.ps1` reads the existing app YOLO dataset, rejects missing/invalid labels, duplicate stems/content, impossible NG holdout counts, and an existing output root, then writes a versioned copied dataset plus `benchmark-dataset.manifest.json` with per-item SHA-256 and deterministic split fingerprints. Actual seed `20260716` evidence under `artifacts\yolo-detection-benchmark-datasets\test01-controlled-seed20260716-v1` preserves 125 unique image/label pairs as train/valid/test `85/20/20`, with NG-containing images `8/3/5`; a repeated generation produced identical mappings and copied hashes. Fresh 100-epoch YOLOv5s and local-source YOLOv8n runs both reference this snapshot. The five-repeat test report under `artifacts\yolo-model-comparison\test01-controlled-holdout-v5s-v8n-repeat5-20260716\20260716-102355` has matching test fingerprint `bc7a310eb81244ff8410cf9a261748095a2dda987fbc7d63b1c28aae188901fa`: YOLOv5 scored recall `0.491`, mAP50-95 `0.399`, NG recall `0.000`, and median Takt `74.3ms`; YOLOv8 scored recall `0.9785`, mAP50-95 `0.6844`, NG recall `0.957`, and median Takt `47.146ms`. Candidate Review and the model-neutral benchmark window loaded the actual report; required build and comparison/review/benchmark/shell gates passed. Keep the result at manual `review`: this closes controlled same-acquisition engine comparison, not independent production accuracy or automatic adoption.

2026-07-16 detection per-class and ground-truth review contract: new `compare-yolo-models.ps1` detection reports preserve each engine's native class precision/recall/mAP values, generate separate review predictions for both engines with the app-default NMS IoU `0.45`, and calculate confidence `0.25` same-class answer matches at disclosed IoU `0.5`, including per-class TP/FP/FN and bounded error examples. SEG remains on its existing polygon/mask evidence path. Historical reports without these fields must remain readable. `WpfModelBenchmarkWindow` presents the data in the model-neutral `클래스/오류` tab and does not rewrite reports, labels, weights, recipes, registry state, or adoption history. Actual proof is `artifacts\yolo-model-comparison\test01-controlled-holdout-v5s-v8n-repeat5-20260716\20260716-112133`: YOLOv5 produced TP/FP/FN `20/0/5` and missed all five NG answers, while YOLOv8 produced `25/1/0` and found all five NG answers. Covered by the required isolated build, `--wpf-model-benchmark-window`, `--wpf-model-comparison-run-service`, parser/direct-label checks, and true before/current-build after 1920x1080 evidence under `artifacts\ui\20260716-model-benchmark-per-class`. This closes current-report class/error drill-down only; independent acquisition evidence remains required.

2026-07-16 model-comparison error-image-preview contract: `WpfModelBenchmarkViewModel.SelectedGroundTruthExample` owns selection for current detection missed/extra examples, and `PreviewSource` lazily decodes only that source image with a bounded 640px decode. Missing, moved, or unreadable source paths return no bitmap and the view shows an explicit unavailable status. `WpfModelBenchmarkWindow` keeps the raw source image in a right-hand detail preview and must not manufacture boxes or contours because the current report schema stores no prediction/ground-truth geometry. Selecting a different error must update only the detail pane; it must not rerun comparison or rewrite report/label/model/recipe/registry/adoption state. The window's selected `DataGridCell` colors must use the existing selected-row resources so selected text stays readable. Covered by the required isolated build, `--wpf-model-benchmark-window` selection/preview coverage, XAML XML parsing, and true before/current-build after 1920x1080 plus 1366px responsive evidence under `artifacts\ui\20260716-model-benchmark-error-preview`. This closes raw source-image review for current detection reports; geometry overlay is a separate schema decision.

2026-07-16 external evaluation data duplicate-preflight contract: Model Center > Data keeps the SHA-256 external-folder comparison inside the existing collapsed dataset-check detail. The shell owns folder browsing and captures the active train/valid/test directory strings before `YoloExternalEvaluationDataAuditService` performs its read-only background comparison. The service reports supported-image counts, byte-identical content overlap, one example, and filename overlap without treating same names as duplicate content. A nonempty error, an empty external folder, or any matching content fails the independent-evidence interpretation; a no-overlap result only clears the byte-identical-copy check and still requires label-quality and NG-coverage review. The action must not mutate labels, datasets, recipes, weights, training history, model registry, or adoption state. A single in-flight guard prevents concurrent scans. Covered by the required isolated build, `--external-evaluation-data-audit`, `--wpf-learning-workflow-panel`, `--dataset-readiness-purpose`, `--wpf-labeling-shell`, and current-build 1920x1080 evidence at `artifacts\ui\20260716-external-evaluation-audit\after-model-center-data-1920.png`. The local `Test01`/`Test02` evidence is 125/125 same SHA-256 contents and is therefore not an independent hold-out.

2026-07-16 external evaluation data EXE folder-picker contract: the current-source EXE smoke `--exe-external-evaluation-data-audit` creates only an artifact-local ObjectDetection recipe, writes one temporary reference image plus one byte-identical and one byte-different external image, opens the native `OpenFolderDialog` for both folders, and requires the visible duplicate-content result `reference 1 / external 1 / identical content 1` followed by `identical content 0`. The no-overlap branch must retain the label-quality and NG-coverage follow-up wording; it does not certify independent production evidence. The smoke compares the settled recipe `VISION.xml` SHA-256 before and after both audit actions, proving the picker/audit route does not rewrite recipe state. Evidence is `artifacts\exe-external-evaluation-data-audit\duplicate-and-independent-20260716`, including `screenshots\03_duplicate_content_blocked.png` and `04_independent_content_clear.png`. The smoke restores the test EXE's last-opened-recipe marker and removes its temporary recipe; it does not access user recipes or `D:\LabelingData`.

2026-07-16 YOLOv8 anomaly-classification regression contract: the existing local flow must keep sending the task-aware `classify` training request, map an adapter image-level classification candidate into the WPF anomaly review state, and fail closed when evaluation evidence is weak or incomplete. Current-source evidence is the required isolated build plus `--anomaly-classification-training-workflow`, `--wpf-yolov8-anomaly-classification-runtime-smoke`, `--anomaly-classification-evaluation`, and local `labeling_tcp_client.py` compile/self-test. This protects runtime behavior only; its deterministic same-source fixture is not independent production-camera/cross-session accuracy or adoption evidence.

2026-07-16 model-benchmark overview dashboard contract: the separate `WpfModelBenchmarkWindow` overview must keep the four compact evidence/metric/Takt/decision cards, native WPF quality-versus-Takt position chart, raw TP/FP/FN report rows, and the existing selected-run detail table. `WpfModelBenchmarkViewModel` owns only derived read-only presentation data. The view adapter may render a point only when there are at least two selected runs sharing the baseline's primary metric, `AreQualityComparable` identity, and `AreTimingComparable` protocol; mixed tasks, mismatched fingerprints/legacy identities, missing timing, or different timing conditions must show the existing comparison reason and no position-chart ranking. TP/FP/FN remains per-report raw evidence, not a new adoption rule. The dashboard must not reread artifacts on selection, run model inference, or modify reports, labels, models, recipes, registry state, or adoption history. ScottPlot.WPF 5.1.59 was deliberately not adopted after it produced a `NU1701` .NET Framework fallback warning in this `net8.0-windows` application; the chart uses existing WPF only. Covered by the isolated build, `--wpf-model-benchmark-window` comparable-pair/exclusion coverage, `--wpf-model-comparison-run-service`, `--wpf-labeling-shell`, `git diff --check`, and true before/current-build 1920x1080 plus current-build 1366x768 evidence under `artifacts\ui\20260716-model-benchmark-dashboard`. This closes current-schema overview readability only and does not alter the `4.0/5` focused-workstation estimate or independent-evidence requirements.

2026-07-16 detection ground-truth review v2 threshold contract: every new `compare-yolo-models.ps1` detection report must preserve `groundTruthReview.schemaVersion=2`, `schema=detection-ground-truth-review-v2`, `geometryCoordinateSystem=normalized-xyxy-v1`, and a stored threshold sweep for the existing review thresholds. Each threshold row carries ground-truth/prediction counts, TP/FP/FN, precision, recall, and F1 calculated from the comparison's already saved prediction labels; selecting it in WPF must not rerun inference. Bounded false-positive/false-negative examples retain only their applicable normalized prediction and/or answer box. This is report evidence for a later overlay decision, not an instruction to draw rectangles, contours, or masks in the current raw-source preview. `WpfModelBenchmarkCatalogService` must ignore malformed optional v2 threshold rows or boxes and keep historical reports readable as v1. `WpfModelBenchmarkWindow` keeps `클래스/오류` at tab index 2 and `실행 조건` at index 3, then exposes v2 rows in the end-position `임계값` tab; reports without usable v2 rows must show the explicit rerun-object-detection-comparison state. The tab must not rewrite reports, labels, weights, recipes, registry state, or adoption history. Covered by the isolated build, PowerShell syntax parse, a read-only direct Test01 saved-label smoke that emitted four threshold rows for each engine and both answer/prediction geometry examples, `--wpf-model-benchmark-window` v2/legacy fixture coverage, `--wpf-model-comparison-run-service`, `--wpf-labeling-shell`, `--priority-workflow-docs`, and current-build 1920x1080/1366x768 evidence under `artifacts\ui\20260716-model-benchmark-threshold-review`. A fresh normal comparison invocation is still required to prove end-to-end v2 report generation.

2026-07-16 v2 detection ground-truth runtime-generation contract: `compare-yolo-models.ps1` must serialize `groundTruthReview.examples` as an array even when one bounded error exists. `WpfModelBenchmarkCatalogService` must still accept a historical singleton object defensively so a malformed prior report does not hide a valid review example. A normal five-repeat local YOLOv5/YOLOv8 Test01 comparison now proves end-to-end output at `artifacts\yolo-model-comparison\v2-threshold-review-runtime-fixed-20260716\20260716-160614`: both reviews have `schemaVersion=2`, `detection-ground-truth-review-v2`, `normalized-xyxy-v1`, four threshold rows, and array-shaped examples. The current-source WPF model benchmark opens that actual pair and shows all eight threshold rows at 1920x1080 and 1366x768 under `artifacts\ui\20260716-model-benchmark-threshold-runtime`. Covered by the required isolated build, PowerShell parser, singleton-example `--wpf-model-benchmark-window` coverage, and the normal runtime report. It remains same-acquisition comparison evidence and must not authorize automatic model adoption or independent production-quality claims.

2026-07-16 model-benchmark error geometry-overlay contract: when a v2 detection error example has valid normalized `xyxy` geometry, `WpfModelBenchmarkGroundTruthExampleViewModel.PreviewSource` must compose its lazy bounded source bitmap with read-only outlines from the stored report: solid green for ground truth and dashed blue for prediction. Missing, out-of-range, zero-size, or unavailable boxes must not render. A miss therefore has only the answer outline; an example holding both boxes has both outlines. `WpfModelBenchmarkWindow` shows the matching conditional legend below the existing source preview. Row selection must remain read-only: no inference, report rewrite, label/model/recipe/registry mutation, or adoption change. Covered by the required isolated build, `--wpf-model-benchmark-window` composed-drawing coverage for one- and two-outline cases, `--wpf-labeling-shell`, `--wpf-model-comparison-run-service`, and true before/current-source after 1920x1080 plus 1366x768 evidence under `artifacts\ui\20260716-model-benchmark-error-overlay`. This is detection-box auditability only, not SEG contour rendering or new model-quality evidence.

2026-07-16 YOLOv5 label-cache collision contract: when local YOLOv5 participates in `compare-yolo-models.ps1`, a requested split is allowed to clear only its exact derived cache pair if the stale transient `labels.cache.npy` exists: the sidecar and its sibling `labels.cache`. The script derives the label directory from supported evaluation images, performs no recursive delete, and must not touch images, label `.txt` files, weights, recipes, registry state, or adoption state. It records the removed derived paths in `runtimePreflight.yoloV5StaleLabelCacheArtifactsRemoved` and the Markdown report records a nonzero cleanup count. A final copied-20-image local v5/v8 test smoke under `artifacts\yolo-cache-preflight-runtime-smoke\results-20260716\20260716-175932` removed two cache artifacts, regenerated only `labels.cache`, and had no `WinError 183` cache-rename warning. Covered by PowerShell syntax parsing, the required isolated build, `--wpf-model-comparison-run-service`, and the actual local smoke. This is cache/runtime stability only; it is not new quality, timing, or adoption evidence.

2026-07-16 approved historical SEG template-contour migration contract: after the operator approved the complete actual target set, all 124 legacy OK rectangle targets received the transformed Teaching_0 raster contour. artifacts\run\Debug\DATA\Segmentagtion_Dataset_ObjectDetection_20260704_065650\backups\segmentation-template-contour-migration-approved-20260716-124targets\migration-manifest.json is Applied; its originals and staging trees each contain 372 JSON/mask/YOLO artifacts. Every migrated OK record is explicit RasterMask geometry with 118 contour points and a non-rectangle YOLO segment line. The 14 NG-containing images retain their NG records and identical NG pixel positions, while all four Teaching_0 source artifacts retain their manifest SHA-256 identities. This is an approved data migration, not a renderer change or model-quality proof. Covered by the isolated build, --segmentation-template-contour-migration, --template-batch-autolabel-storage, --segmentation-historical-remediation-audit, --wpf-segmentation-object-verification, --dataset-readiness-purpose, post-apply artifact inspection, and git diff --check.

2026-07-16 SEG model-benchmark evidence-interpretation contract: when every selected benchmark run is `segmentation` and none has a stored box `groundTruthReview`, the overview and class/error detail must explain that polygon/mask metrics are the applicable evidence and that box TP/FP/FN review belongs to object-detection comparison. Do not present this absence as a missing rerun or a model error. Object-detection rows and future reports with actual ground-truth review remain unchanged. Covered by the required isolated build, `--wpf-model-benchmark-window`, and true current-build before/after 1920x1080 evidence at `artifacts\ui\20260716-model-benchmark-current-audit\model-benchmark-overview-1920.png` and `artifacts\ui\20260716-model-benchmark-seg-evidence-copy\after-model-benchmark-overview-1920.png`. This changes presentation only; reports, weights, labels, recipes, registry state, adoption, and the 4.0/5 focused-workstation estimate remain unchanged.

2026-07-16 evaluation-data-evidence surface contract: `Model Center > Data` reuses the existing `YoloDatasetReadinessQuickPanel`, `TrainingSettingsViewModel.TrainingReadinessText`, refresh command, external-folder SHA-256 audit command, and in-memory audit result. The visible surface must show the active dataset purpose, saved dataset readiness, and external image-content independence separately. Its wording must state that SHA-256 no-overlap is not model-adoption evidence. The panel remains read-only except for the existing readiness refresh and folder-selection audit action; it must not create a window, chart dependency, model runner, persisted state owner, recipe/label/model write, training, inference, or adoption path. Covered by the required isolated build, `--wpf-labeling-shell`, `--dataset-readiness-purpose`, `--external-evaluation-data-audit`, and true current-build before/after 1920x1080 plus 1366px responsive evidence under `artifacts\ui\20260716-evaluation-data-evidence`. This closes discoverability only; independent label-quality, NG-coverage, and held-out model evidence remain required.

2026-07-18 Model Center dedicated-workspace contract: `TrainingModel` is a presentation-only workspace mode. It expands the existing Model Center to the available shell width and hides the inactive canvas, image queue, splitters, and dock-collapse control. Returning to Dataset, Labeling, or Inference restores the normal canvas/queue layout and the saved queue width. This transition must not modify recipe settings, labels, queue selection, model runtime/profile, training state, inference/candidate state, comparison evidence, or adoption history. Covered by the required isolated build, `--wpf-labeling-shell`, `--wpf-responsive-layout`, `--wpf-training-settings-panel`, and current-build 1920x1080 plus 1366x768 captures under `artifacts\ui\model-workspace-20260718`.

2026-07-18 model-adapter catalog contract: `ModelAdapterCatalogService` must expose only five read-only contracts—implemented recipe interchange formats, YOLOv5 detection, local YOLOv8, ONNX inference-only, and blocked YOLO11—and every item must declare task, data, runtime, evidence, and next action. The interchange list derives from `DatasetExportCapabilityService.BuildImplementedCapabilities()` so format availability cannot silently exceed the real exporter inventory. The WPF Model Center binds `ModelAdapterCatalogItems` and `ModelAdapterCatalogBoundaryText` from `WpfYoloModelSettingsPanelViewModel`; XAML and shell code-behind must not decide model readiness or add a runtime action. Data conversion is not executable-runtime evidence, ONNX remains inference-only, and YOLO11 remains blocked until a compatible local runtime, weight, transport mapping, and focused smoke are proven. Covered by the required isolated build, `--model-adapter-catalog`, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--wpf-responsive-layout`, and 1920x1080/1366x768 current-source captures under `artifacts\ui\model-adapter-catalog-20260718`. This is not generic GitHub model support, model quality evidence, model adoption, or ONNX training.

2026-07-19 label-create/save queue-locality contract: creating or saving an annotation for the active image must update that existing `WpfImageQueueItem` through its live-filtered properties and must not call `imageQueueView.Refresh()`, reset the source/view, rebuild row instances, change the selected root or active path, or increment the asynchronous catalog generation. Explicit refreshes owned by user filter changes, batch/quality/anomaly workflows remain outside this contract. The canonical source-level gate is `--wpf-image-queue-save-local-update`, which performs two real `RoiAdded` event-path creates and saves on 125 rows and requires `SOURCE_RESETS=0`, `VIEW_RESETS=0`, `FILTER_EVALUATIONS=1`, and an unchanged catalog version for every phase. The canonical packaged-app gate is `--exe-label-create-queue-locality-smoke`: it starts the current Debug EXE with an isolated 125-image recipe, scrolls the real queue to 55%, draws a box by native mouse input, saves it, and requires zero UI Automation invalidations/bulk changes plus unchanged queue count, active image, visible rows, and scroll position. Verified EXE SHA-256 `2701EAAA58F3700B67B5F3AE56888D5424C6E0821996C218C388238F8B73BBD0`; reusable evidence is `artifacts\exe-label-create-queue-locality\label_create_queue_locality_20260719_171810`. The queue status/click/canvas/keyboard/root-switch/1,200-row/10K/shell gates also pass.

2026-07-19 anomaly OK/NG queue-focus contract: after either anomaly decision-and-next command succeeds, the next-unreviewed path must be the same object held by the active canvas path, DataGrid `SelectedItem`, `WpfImageQueuePanelViewModel.SelectedQueueItem`, and DataGrid `CurrentCell.Item`; the corresponding row must be realized, selected, and scrolled into view. The command must not steal keyboard focus from the OK/NG controls or add another collection-view refresh. The canonical gate is `--wpf-anomaly-queue-focus`, which deterministically invokes the commands bound to the visible OK/NG buttons, alternates nine OK and nine NG decisions in a 100-image queue, and verifies the full selection/current-row chain plus persisted 9/9 review counts. Historical native-mouse evidence remains under `artifacts\ui\anomaly-resource-warnings-20260719`; the current queue-local latency closure and evidence are recorded at the top of this document.

2026-07-19 anomaly application-theme resource contract: `WpfImageQueuePanel` styles and WPF-UI button/DataGrid visual states may resolve the shared palette outside the immediate window resource tree while controls are focused, clicked, or re-realized. `WpfLabelingShellWindow` therefore promotes the same existing palette objects plus its merged WPF-UI theme/control dictionaries to `Application.Resources`; dark/light changes must update the window and application entries with the same brush. Do not suppress ResourceDictionary tracing or define a second palette. The canonical `--wpf-anomaly-queue-focus` gate first requires application lookup for the seven keys reported by the operator, then invokes the commands bound to the visible OK/NG buttons for 18 decisions and requires `ANOMALY_QUEUE_RESOURCE_WARNINGS=0`, synchronized active/grid/ViewModel/current-row state, and persisted 9/9 counts. The earlier current-source capture at `artifacts\ui\anomaly-resource-warnings-20260719\after-native-click-1920.png` remains separate native-mouse evidence. A running old EXE must be restarted to load the corrected resource scope; the later synchronous save/view-refresh latency issue is now closed by the queue-local transition recorded above.

## 2026-07-20 external detection comparison and trained-model identity contract

Status: stable for the verified external-YAML object-detection comparison path.

Protected behavior:

- A single recipe owns the selected dataset purpose and explicit external-YAML activation. Switching YOLOv5/YOLOv8 is a model-profile choice, not a requirement to clone the recipe or alter the native split.
- The native external class schema is stored separately from recipe class labels. Report-provided native class metrics take precedence in comparison review so a report cannot be displayed with unrelated local class names.
- Runtime and inference status must display the engine and a distinguishable training-run identity. Conventional `run/weights/best.pt` paths display `run/best.pt`; exported `run/best.pt` paths keep the direct run-folder name instead of showing an ambiguous bare `best.pt`.
- Engine benchmark reports remain non-adopting evidence: `comparisonKind=engine-benchmark` and `promotion.recommendation=benchmark` must not replace the inspection model.

Coverage: required isolated build; `--wpf-inference-status-presentation`; `--external-yolo-dataset-intake`; `--wpf-model-comparison-review-service`; `--wpf-model-comparison-run-service`; and current Debug EXE `--exe-yolov8-detect-restart-smoke` evidence under `artifacts\ui\circular-disk-yolov8-beginner-e30-current-final4-20260720`. The EXE flow proves object-purpose persistence, native YAML activation, saved-profile restart, and trained YOLOv8 inference candidate display. This does not prove production accuracy or permit model adoption without independent acquisition data.

## Refactor Rule

When working near a protected path, prefer adding a small adapter or a new higher-level service instead of rewriting the verified hot path. If the hot path must change, document the reason in the final response and include the focused gate results.

Do not run real-EXE UIAutomation smokes in parallel. They share desktop focus and can produce false failures when two EXE windows compete for foreground input.

2026-07-17 image queue 10K responsiveness contract: user-initiated image-root, recipe-root, and refresh commands must start a cancellable background catalog load rather than enumerate files and create metadata on the WPF dispatcher. A newer root must cancel/invalidate an older catalog and detail request so no stale items replace the current queue. The queue receives one catalog reset and retains lazy thumbnail creation. Detail dimensions/review states must use at most four background workers, apply in 64-row dispatcher batches, update the visible `status check loaded/total` state, and perform only one final full collection-view refresh; native live filtering handles individual item status changes. The synchronous loader remains for deterministic internal callers only, not interactive folder commands. Current-source coverage: required isolated build; `--wpf-image-queue-10k-responsive` (`13.8ms` command return, 10,000 rows, stale replacement rejected); `--wpf-image-queue-10k-detail-responsive` (10,000 valid rows, input `65.8ms` while active, one `10,000`-row filter pass); status, keyboard, root-switch, selection-service, large-folder, click-performance, and shell gates. Local full detail scanning took `63,772.2ms` on a synthetic temporary-disk fixture and is not a shared-storage performance promise. Current-build 1920x1080 before/after evidence is under `artifacts\\ui\\image-queue-10k-20260717`; no README/tutorial image update is required because public layout did not change.

Latest review recheck: `--wpf-image-queue-10k-responsive` returned in `16.1ms`; `--wpf-image-queue-10k-detail-responsive` scanned the same 10,000-row synthetic fixture in `70,504.7ms` while dispatcher input completed in `78.9ms`, with one final `10,000`-row filter evaluation. The elapsed scan time is environment-sensitive and does not change the scheduling contract or establish a production throughput claim.

2026-07-17 local operator-folder profile: a current-source warm-cache WPF profile of the user-provided mixed `D:\라벨테스트` root completed `50,081` images / `1,470,992,535` bytes with catalog return `13.9ms`, catalog completion `11,705.7ms`, catalog/detail dispatcher input `142.0ms`/`84.9ms`, DataGrid scroll dispatch `148.2ms`, middle/final selection `207.4ms`/`318.3ms`, and detail completion `406,505.9ms` after catalog. It reported zero empty dimensions and working set `167.3MB` -> `1,030.7MB` -> `1,036.8MB`. The profile uses a temporary test output root and retained the same before/after extension inventory count/bytes; that check is not a source-tree hash proof. Evidence: `artifacts\image-queue-operator-profile\20260717-225226-warm-cache`. This confirms responsive background scheduling on one large local mixed synthetic root, not network-share or production-camera throughput.

2026-07-17 local 8K duplicate-file queue profile: a current-source WPF profile of `D:\새 폴더` used an explicit `--minimum-images 8000` override; the default operator-profile regression threshold stays 10,000. It completed `8,000` JPG paths / `476,177,088` bytes with catalog return `12.8ms`, catalog completion `2,264.8ms`, catalog/detail dispatcher input `131.1ms`/`69.9ms`, DataGrid scroll dispatch `27.2ms`, middle/final selection `182.8ms`/`121.7ms`, and detail completion `80,183.5ms` after catalog. It reported zero empty dimensions and working set `167.4MB` -> `303.1MB` -> `365.3MB`. The before/after metadata manifest SHA-256 remained `072643A7ED96F109E245271AC6BDAF85D26A174BE9A1203D16B245CF462F76F9`. A complete content SHA-256 audit found exactly `250` contents, each copied `32` times; the metadata manifest remains distinct from a before/after content-tree hash. Evidence: `artifacts\image-queue-operator-profile\20260717-231924-local-8k-production-sample`. This is duplicate-file local scheduling evidence only: `D:` is fixed local storage and the source is not a production-data proxy.

2026-07-21 U-Net canonical-segmentation runtime contract: the `UNet` profile owns a dedicated `C:\Git\unet` PyTorch environment and the bundled `openvisionlab_unet_worker.py`; selecting it must use those automatic defaults rather than ask the operator to locate a project or worker script. Training is valid only for a recipe-owned segmentation export (`images/<split>`, `masks/<split>`, `classes.json`, manifest); it must reject non-segmentation and external native-YOLO input instead of converting bbox labels into fabricated masks. A test-host application workflow must prove one epoch, preserve the recipe-source SHA-256, write `best.pt`, then restart the worker, load that exact checkpoint, and finish inference. Canonical evidence is `artifacts\real-unet-segmentation-runtime-20260721-173045\summary.txt`; focused gates are the isolated build, worker compile/self-test, `--unet-segmentation-export`, `--dataset-health`, `--priority-workflow-docs`, and `--real-unet-segmentation-runtime --timeout-seconds 90`. This contract proves reproducibility and integration only—not segmentation quality, candidate quality, model adoption, or a valid U-Net-versus-YOLO metric comparison. The first one-epoch smoke returned zero components, so metric normalization plus a real held-out mask evaluation remains required before comparison or adoption.

2026-07-21 normalized segmentation-comparison artifact contract: `openvisionlab_segmentation_prediction_export.py` accepts only an explicit `unet` or `ultralytics` adapter plus a canonical export, validates the same `classes.json`/manifest/checkpoint class contract, and writes only app-owned `prediction-manifest.jsonl`, indexed prediction masks, and run summary. `SegmentationMaskComparisonService` may score two runs only when dataset fingerprint, recipe-source SHA-256, class-contract SHA-256, split, image SHA-256/dimensions, and prediction-mask SHA-256 all match the same canonical export. It then reports per-class Dice, IoU/mIoU, and component TP/FP/FN; it must not combine these numbers with YOLO mAP or promote a model. Canonical focused coverage is the isolated build, Python exporter compile/self-test, `--segmentation-mask-comparison`, `--real-unet-segmentation-runtime --timeout-seconds 90`, and `--real-ultralytics-segmentation-prediction-export`. Evidence: `artifacts\real-unet-segmentation-runtime-20260721-173045\summary.txt` and `artifacts\real-ultralytics-segmentation-prediction-export-20260721-173053\summary.txt`. The adapter smokes use intentionally different class contracts, so they prove normalized artifact generation rather than valid cross-model quality; a same-canonical-export paired training run and Model Center review surface remain separate work.

2026-07-21 Model Center U-Net/YOLO-seg launch-and-review contract: the dedicated `SegmentationAdapterComparisonPanel` appears only for a segmentation recipe. It binds independent U-Net and YOLOv8/YOLO11 checkpoint paths through `WpfTrainingSettingsPanelViewModel`; the View code-behind only opens file dialogs and adapts run results. `WpfSegmentationAdapterComparisonRunService` owns canonical export, both raw prediction exports, and `SegmentationMaskComparisonService` invocation. It keeps an already configured U-Net runtime root when valid; YOLO11 uses the existing `yolov8` Ultralytics runtime rather than requiring a separate `yolo11` repository. The flow selects no model automatically and treats any run failure as non-adopting. User copy must say that common raster-mask Dice/IoU/component TP/FP/FN is separate from YOLO mAP. Execution is disabled until two paths exist and during the run. Coverage: required isolated build, `--wpf-segmentation-adapter-comparison`, `--unet-segmentation-export`, `--segmentation-mask-comparison`, `--dataset-health`, `--priority-workflow-docs`, `git diff --check`, and current-source 1920x1080 before/after visual smoke `artifacts\ui\unet-yoloseg-comparison-before-20260721.png` / `artifacts\ui\unet-yoloseg-comparison-after-20260721.png`. This is a comparison entry point, not same-data model-quality evidence, retraining, automatic adoption, or a production claim.

2026-07-21 external native YOLO segmentation canonical-mask contract: an explicitly activated and source-identity-matched native YOLO segmentation `data.yaml` may now feed U-Net training and the U-Net/YOLO-seg common-mask comparison. `YoloExternalDatasetIntakeService` remains the only native source parser and exposes validated read-only source entries; `ExternalYoloSegmentationCanonicalExportService` writes the derived image/mask/class/manifest contract only below the recipe-owned `artifacts\unet-ext` root. It maps native `val` to U-Net `valid`, preserves native class index order as one-based mask values, calculates source fingerprint before/after, and reuses only a matching provenance artifact. It rejects duplicate image content across splits and pixels covered by different classes. External U-Net training sends this canonical root while retaining the selected `data.yaml` and source fingerprint in profile provenance; source change disables the activation. The comparison context names the external source and applies the same identity gate. Coverage: required isolated 0-warning/0-error build; `--external-yolo-segmentation-canonical-export`, `--external-yolo-dataset-intake`, `--external-yolo-list-split-intake`, `--unet-segmentation-export`, `--wpf-segmentation-adapter-comparison`, `--segmentation-mask-comparison`, and `--dataset-health`. This is deterministic format/provenance evidence only; a real external one-epoch pair on the same held-out split remains required before any model-quality statement.

2026-07-21 external native YOLO segmentation runtime evidence: the opt-in `--real-unet-segmentation-runtime` smoke accepts `--external-data-yaml`, `--image-size`, and `--batch` without changing its default recipe-owned fixture behavior. On the selected supplied EasyMatch Die Array native segmentation source, it created only a separate app artifact, completed one CPU epoch at 32px/batch 64, wrote `best.pt`, restarted the worker, loaded that checkpoint, and completed one inference. The external source SHA-256 was equal before/after (`C0FDB11C644F1D705EC3B033FDFB5205E0DE72129559624699C85D5B64CCEF53`). Evidence: `artifacts\real-external-unet-native-yolo-20260721-195129\summary.txt`. This is reproducibility/runtime evidence, not U-Net quality, an external-data quality audit, a same-data YOLO-seg comparison, model adoption, or production evidence.

2026-07-21 external native YOLO runtime-copy and paired-comparison contract: every selected external native YOLO source, including a normal `images`/`labels` tree, is now materialized beneath the app output before YOLO training. This removes the possibility that an Ultralytics cache changes the selected source root; `LastTrainingDataYamlFilePath` retains the selected source and `LastTrainingRuntimeDataYamlFilePath` records the separate runtime copy. The current real 1-epoch CPU pairing on the supplied 360/80/60 EasyMatch Die Array segmentation packet preserved the full 2,004-file native source tree SHA-256 `5819E2ED72E402D3F06C32CF4F1FB3481A2DF1D70BD8CB8C00B97CE9E28199C2`. A U-Net and YOLOv8-seg checkpoint then completed the actual Model Center common-mask orchestration across the same 60-image test split; the native source fingerprint stayed `C0FDB11C644F1D705EC3B033FDFB5205E0DE72129559624699C85D5B64CCEF53`. Evidence: `artifacts\real-external-yolov8-die-array-20260721-195557\summary.txt` and `artifacts\real-external-seg-adapter-compare-20260721-200228\summary.txt`. The one-epoch 32px scores are smoke evidence only, must not rank/adopt a model, and need a separately approved multi-epoch benchmark with fixed quality gates.

2026-07-21 controlled external native segmentation 30-epoch benchmark: the approved same-data training pair is now complete. U-Net trained on CUDA and YOLOv8-seg on the installed CPU-only runtime; both used the same external 360/80/60 native packet, five-class contract, image size 320, batch 4, and 30 epochs. U-Net completed training/restart/prediction with weight SHA-256 `487EF0EE70FD3A37260F4D9CB17C12994FC747575D1984EC8C64BD65967C6F72`; YOLO completed through an app-owned runtime copy with weight SHA-256 `0AF2A2C937C349C11B2021491ADA586B48DAF7DC5E2AE504D8073A0E112B7CBF`. The original full source tree remained 2,004 files and SHA-256 `5819E2ED72E402D3F06C32CF4F1FB3481A2DF1D70BD8CB8C00B97CE9E28199C2`; Model Center then compared both canonical test predictions on 60 held-out images with no report errors and the same native source fingerprint `C0FDB11C644F1D705EC3B033FDFB5205E0DE72129559624699C85D5B64CCEF53`. Common mask mean Dice/IoU was U-Net `0.243091` / `0.156165`, YOLOv8-seg `0.079059` / `0.044103`. This supports a manual U-Net-follow-up investigation only: U-Net still scored zero Dice in two classes and YOLO false positives are excessive; elapsed time is not comparable across CUDA/CPU. Evidence: `artifacts\benchmark-external-unet-die-array-e30-20260721-203302\summary.txt`, `artifacts\benchmark-external-yolov8-die-array-e30-20260721-203302\summary.txt`, and `artifacts\benchmark-external-seg-adapter-compare-e30-20260721-203302\summary.txt`.

2026-07-21 segmentation benchmark confidence-path diagnosis: `Complete`. The 30-epoch external native segmentation benchmark remains a non-adopting same-source result. Its YOLOv8-seg raw-mask test report used the evidence runner's `confidence=0.00`, not the Model Center profile default. A read-only `valid`-only replay of the identical checkpoint at the product fallback `0.25` reduced the all-image false-positive flood and yielded class Dice `0.782156`-`0.854240`; the 60-image held-out test was not used to select that value. U-Net class remediation is deliberately separate: two zero-Dice classes have train support, so a confidence-only change cannot be claimed as a U-Net fix. Evidence and boundary: `docs\SEGMENTATION_E30_ERROR_ANALYSIS_20260721.md` and `artifacts\segmentation-e30-error-analysis-20260721`.

2026-07-22 YOLOv8-seg confidence-selected test contract: the opt-in external paired-comparison runner now accepts `--yolo-confidence` (default `0.25`, fail-closed outside `[0,1]`) and records it in the app-owned evidence summary. Its `0.25` value was chosen only on the prior 80-image `valid` replay. One final unchanged 60-image test replay then completed with the same native source fingerprint before/after and recorded YOLOv8-seg mean Dice/IoU `0.721702` / `0.570198` against U-Net `0.243091` / `0.156165`. This is a fixed same-source model-comparison result, not automatic adoption, production accuracy, independent camera evidence, a U-Net remedy, or comparable CUDA/CPU latency evidence. See `docs\SEGMENTATION_E30_CONFIDENCE025_TEST_EVIDENCE_20260722.md`.

2026-07-22 U-Net class-confusion diagnostic contract: a fixed U-Net checkpoint may be exported only to a separate app-owned validation artifact for diagnosis. On the five-class EasyMatch Die Array packet, background accounts for 99.7823% of train-mask pixels; unweighted cross entropy selected by validation loss produced no validation prediction pixels for supported `contamination_spot` or `foreign_particle`. U-Net raw argmax does not share the YOLO confidence filter, so the YOLO `0.25` result must not be presented as a U-Net repair. The next controlled U-Net hypothesis is train-mask-derived class-weighted cross entropy with a valid-only foreground macro-Dice/no-collapse gate; it must not use the held-out test split until a validation decision is recorded. Evidence: `docs\UNET_E30_CLASS_CONFUSION_ANALYSIS_20260722.md`.

2026-07-22 U-Net class-weighted train/valid experiment: `Complete` as an evidence slice, rejected as a default behavior. One 30-epoch CUDA train/valid-only run used temporary foreground-balanced cross entropy and a no-collapse/macro-Dice selection contract. On the identical 80-image valid raster masks, macro Dice/IoU fell from the unweighted baseline `0.164849` / `0.097209` to `0.074135` / `0.040426`; both failed no-collapse and `contamination_spot` remained unpredicted. The train/valid inputs remained unchanged before/after. The worker is restored to unweighted cross entropy and validation-loss selection; no held-out test was run. See `docs\UNET_E30_CLASS_CONFUSION_ANALYSIS_20260722.md`.

2026-07-22 U-Net foreground-crop experiment: `Complete` as an evidence slice, not adopted. The temporary train-only policy retained 180 normal full frames, generated 164 foreground-centered 320px crops, and used 16 full-frame foreground fallbacks. Its 30-epoch CUDA output was evaluated only on the same 80-image full-frame valid split; the validation-loss-selected checkpoint predicted background only and produced macro Dice/IoU `0.0` / `0.0`. This is not evidence that crop geometry is harmful, because the background-favoring selection metric confounds the experiment. The crop code was removed and no test split was accessed. See `docs\UNET_E30_CLASS_CONFUSION_ANALYSIS_20260722.md`.

2026-07-22 U-Net foreground-quality selector boundary: the internal opt-in selector is verified as an evidence harness, not a default training policy. On unchanged full-frame/unweighted training it selected epoch 29 and improved identical-valid raster macro Dice/IoU from `0.164849` / `0.097209` to `0.204437` / `0.127053` without an all-image class flood. Its quality gate remains incomplete because `contamination_spot` and `foreign_particle` still have zero overlap. Normal TCP training continues to select by validation loss, and no held-out test was used. See `docs\UNET_E30_CLASS_CONFUSION_ANALYSIS_20260722.md`.
