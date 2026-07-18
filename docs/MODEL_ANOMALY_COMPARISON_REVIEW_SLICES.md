# Model, Anomaly, and Comparison Review Slices

Status: Complete as of 2026-07-18. This document separates the model/anomaly/comparison work into two independently committed review slices. Slice A is local commit `6f202db`; Slice B is committed with this review record. Neither slice runs a new training job, registers a weight, or adopts a model.

## Shared Product and Evidence Boundary

OpenVisionLab Labeling Studio uses a recipe as the canonical source for labels, splits, and evidence. Local YOLO adapters may train, infer, and compare through explicit runtime contracts, but runtime success and benchmark metrics are not model-adoption decisions.

The recorded Washer anomaly candidate is runtime- and restart-verified but remains `hold`: it scored 0/60 on the preserved circular synthetic regression and 14/75 on the separate MultiIndustry synthetic native test at the current confidence gate. The recorded EasyMatch YOLOv5s/YOLOv8n comparison is controlled synthetic benchmark evidence only. Neither record authorizes retraining on an evaluation set, automatic registration/adoption, or a production-quality claim.

See `docs/STABLE_VERIFIED_AREAS.md` and the 2026-07-17 through 2026-07-18 entries in `docs/WORK_TRACKING.md` for the recorded artifacts and quality boundary.

## Slice A — Nested Anomaly Review and Reproducible Classification Run

### Goal

Import a conventional anomaly folder layout such as `images\\OK\\product\\image.png` or `images\\NG\\product\\image.png` without losing an operator's saved review decision, and make the opt-in real classification runner use a uniquely named output directory.

### Included files and exact ownership

Direct files:

- `1. Core/AnomalyImageReviewStatusService.cs`: only the parent-folder traversal in `TryInferReviewStateFromParentFolder`.
- `3. Communication/TCP/CCommunicationLearning.cs`: only the optional `runName` forwarding in `SendTrainingData`.
- `3. Communication/TCP/LearningProtocol.cs`: only the optional `runName` field in the normalized training packet.
- `tests/LabelingApplication.Tests/Program.RealYoloV8AnomalyFolderTraining.cs`: only run-name input, single-folder/no-overwrite assertions, TCP request forwarding, and artifact summary identity.

Shared files; review only these hunks:

- `1. Core/YoloTrainingWorkflowService.cs`: optional `runName` argument and forwarding it to `CCommunicationLearning.SendTrainingData`. The external-native-data profile handling in the same file belongs to `EXTERNAL_NATIVE_YOLO_INTAKE_REVIEW_SLICE.md`.
- `Runtime/Python/openvisionlab_ultralytics_worker.py`: `plots=False` in `model.train`, which skips optional plot generation before terminal training status. Its YAML working-directory and label-cache contexts belong to the external-intake slice, not this one.
- `tests/LabelingApplication.Tests/Program.cs`: dispatch and assertions for anomaly-folder import, classification workflow, runtime candidate mapping, evaluation hold guard, and the worker's `plots=False` contract. Do not review unrelated Dataset Health, image-queue, external-intake, or comparison assertions as part of this slice.

### Contract

1. Folder inference walks from an image's direct parent toward the root and uses the first `OK`/`normal` or `NG`/`abnormal` folder it finds. This preserves direct-child behavior and supports nested product folders.
2. Import assigns only unreviewed images. A saved manual Normal/Abnormal state remains authoritative.
3. An anomaly training request remains `classify`; the caller may provide one single-folder run name. The real opt-in runner rejects a pre-existing runtime output directory and does not overwrite an earlier run.
4. `plots=False` may reduce terminal-status delay, but it changes neither inputs, labels, weight selection, metrics, candidate mapping, nor quality/adoption policy.
5. A successful runtime or restart proves only transport and persistence. The existing external evaluation results remain `hold` and remain outside training/tuning.

### Explicit exclusions

- No external `data.yaml` activation, identity, relative-path, cache, or source-immutability behavior.
- No UI layout, tutorial, runtime dependency, weight download, model-profile registration, or adoption change.
- No new real training run or quality claim. The existing synthetic evidence is recorded evidence, not a current-pass result.

### Acceptance evidence before staging this slice

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-folder-auto-review
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolov8-anomaly-classification-runtime-smoke
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-ultralytics-worker
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py Runtime\Python\openvisionlab_ultralytics_worker.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
git diff --check
```

No 1920x1080 screenshot is required for this slice because it changes no visible layout or text. If a future change modifies anomaly UI presentation, capture new before/after evidence from the current build.

## Slice B — Native Cross-Engine Comparison Manifest Mapping

### Goal

Keep YOLOv5 and Ultralytics predictions correctly joined to their ground-truth image/label pairs when a native dataset omits `nc`, has nested split folders, or requires a list-file prediction source.

### Included files and exact ownership

Direct file:

- `scripts/compare-yolo-models.ps1`: names-based class-count fallback; nested image-to-label mapping; recursive image collection; deterministic prediction manifest; duplicate-stem rejection; Ultralytics `imageN.txt` restoration to the manifest source stem; and invariant numeric parsing of metrics.

Shared file; review only these hunks:

- `tests/LabelingApplication.Tests/Program.cs`: the model-comparison run-service/script contract assertions. Do not include unrelated WPF, runtime, Dataset Health, external-intake, or image-queue tests in a comparison-only review.

### Contract

1. A valid native YAML may declare classes through `nc` or through `names`; a `names` declaration is not rejected solely because `nc` is absent.
2. An image below `images\\...` maps to the same relative directory below `labels\\...`.
3. Prediction receives a stable, sorted manifest. If two recursive source paths share one file stem, the script fails closed before a flat labels folder can pair either answer with the wrong prediction.
4. When Ultralytics predicts from that manifest and writes `imageN.txt`, only its generated prediction label is restored to the matching unique source stem. Images, answer labels, weights, recipes, registry history, and adoption history are not renamed or changed.
5. Both engines retain separate validation metrics/Takt collection. A report needs a matching split/image/label fingerprint; a report recommendation is still not automatic adoption.
6. A native `data.yaml` with a relative `path:` value is copied only into the comparison artifact as `comparison-runtime-data.yaml`, where that root is absolute. Both engines receive the artifact YAML; the supplied YAML remains unchanged and the report records both paths.

### Explicit exclusions

- No new YOLOv5/YOLOv8 training, model download, dependency change, profile registration, or adoption. The user-authorized benchmark record below is evaluation only.
- No external `data.yaml` training-intake behavior; its source-data immutability/cache contract remains in `EXTERNAL_NATIVE_YOLO_INTAKE_REVIEW_SLICE.md`.
- No claim that the existing synthetic comparison measures production-camera accuracy.

### Acceptance evidence before staging this slice

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
$tokens = $null; $errors = $null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path .\scripts\compare-yolo-models.ps1), [ref]$tokens, [ref]$errors) | Out-Null
if ($errors.Count) { throw ($errors | Out-String) }
git diff --check
```

Run an actual five-repeat comparison only when the user specifically authorizes its runtime/data cost. It must use a documented held-out split and must record source identity and report provenance; it is not needed merely to review this code slice.

## Review/Commit Decision Rule

The two slices share current evidence history but not source ownership. Do not stage or commit them together by default. Select one slice, inspect only its direct files and named shared-file hunks, run its acceptance commands, and keep the other slice unstaged. A local commit requires the user's explicit request; push remains a separate explicit request.

## Focused Current-Source Completion Record (2026-07-18)

Status: Complete

Scope: Slice A nested anomaly review/run isolation and Slice B native cross-engine comparison-manifest mapping only. These are separately committed local code slices, not a runtime benchmark, training run, model registration, or adoption decision.

Acceptance criteria:

- Nested `OK`/`NG` auto-review preserves an already saved manual Normal/Abnormal state, exports the intended classification folders, and keeps the anomaly request task as `classify`.
- A caller-provided single-folder run name travels through the workflow, TCP packet, and opt-in runner; a pre-existing run directory is rejected before it can be overwritten.
- The bundled worker's `plots=False` optimization remains limited to optional plot output and preserves the existing runtime and quality/adoption contracts.
- A names-only YAML, nested split labels, a sorted prediction manifest, duplicate-stem rejection, and Ultralytics `imageN.txt` restoration remain covered by the comparison run-service contract.
- The comparison script parses successfully and preserves the report boundary: matching fingerprint/provenance is required, and a report recommendation does not automatically adopt a model.

Verification (2026-07-18 closure):

- PASS isolated test-project build: 0 warnings, 0 errors.
- PASS `--anomaly-folder-auto-review`.
- PASS `--anomaly-classification-training-workflow`.
- PASS `--wpf-yolov8-anomaly-classification-runtime-smoke`.
- PASS `--anomaly-classification-evaluation`.
- PASS `--python-ultralytics-worker`.
- PASS local YOLOv8 adapter and bundled worker `py_compile`; adapter `--self-test` reported `self-test passed`.
- PASS `--wpf-model-comparison-run-service`.
- PASS PowerShell parser check for `scripts/compare-yolo-models.ps1`.

Evidence: Slice A local commit `6f202db`, the Slice B local commit containing this record, the current focused gates above, and the preserved historical artifacts in `docs/STABLE_VERIFIED_AREAS.md` / `docs/WORK_TRACKING.md`.

Boundary / next dependency: the current Washer anomaly candidate remains `hold` on the recorded synthetic evaluations, and the controlled YOLOv5/YOLOv8 report remains synthetic benchmark evidence. Neither warrants retraining, tuning, registration, or adoption. Independent production-camera/cross-session anomaly data and independent NG-rich object-detection data are still required for quality decisions; a new five-repeat comparison needs explicit user authorization and documented held-out provenance.

## User-Authorized `D:\라벨테스트` Three-Step Evidence Record (2026-07-18)

Status: Complete for input-contract validation and cross-product evaluation. The outcome is `hold`, not model adoption.

### 1. Native labeled-data contract

- Selected package: `EasyMatch_Switch_Housing_500` under `D:\라벨테스트`. Its README identifies it as synthetic test data, so this is format/generalization evidence only, not production-camera evidence.
- Object-detection contract: native `data.yaml` has `path: .`, `train`/`val`/`test` splits of 360/80/60 images, and the five ordered classes `contamination_spot`, `scratch_crack`, `missing_material`, `foreign_particle`, and `extra_material_bridge`.
- The object source was copied once to `artifacts\cross-domain-switch-object-detection-20260718-002742\input`. The original has 1,004 files, zero `.cache` files, and currently matches the clean artifact copy after excluding only the staged `labels\test\NG.cache` created by YOLOv5.
- The anomaly source has 300 images: 50 `good` mapped to `normal`, plus 250 images from five NG folders mapped to `abnormal`. The renamed evaluation copy at `artifacts\cross-domain-switch-anomaly-20260718-002743\dataset\test` has the same 300-image SHA-256 content set as the source.
- Earlier console-only aggregate values had no retained manifest and could not be reproduced using the current canonical tree aggregation. They are not used as before/after evidence. This record instead preserves the clean staged-copy equality, source file counts, zero source caches, report fingerprints, and complete evaluation artifacts.

### 2. Five-repeat cross-engine object-detection comparison

- Artifact: `artifacts\cross-domain-switch-object-detection-20260718-002742\comparison\20260718-003624\comparison-summary.json`.
- Baseline: existing EasyMatch Die Array YOLOv5s; candidate: existing EasyMatch Die Array YOLOv8n. Both match the five-class contract and were evaluated on Switch Housing's untouched staged `test` split (60 images, evidence SHA-256 `e776d31595fa0e4b642fdccdf7a870e692eaf696351c680ab23e80866fa607c8`).
- The report records the original staged YAML and an artifact-only runtime YAML with absolute `path:`. The source YAML was not changed.
- Five-repeat median result: YOLOv5s precision/recall/mAP50/mAP50-95 `0.641/0.297/0.325/0.176`, native Takt `64.4 ms/image`; YOLOv8n `0.326/0.428/0.258/0.181`, native Takt `36.608 ms/image`.
- Decision: `hold`. The candidate is faster but has lower mAP50 and more UI-threshold false positives (`312` vs `206`); it is not registered, adopted, or promoted.

### 3. Independent anomaly classification evaluation

- Artifact: `artifacts\cross-domain-switch-anomaly-20260718-002743\evaluation\classification-evaluation-20260718-010859\classification-evaluation-summary.json`.
- Evaluated the existing Washer YOLOv8 classification weight (SHA-256 `1a1003635756e1052b7361dcb116ec807f5b16bc555e114138f7ae595b8d2d9f`) at image size 128 and minimum confidence 0.8 against all 300 staged Switch Housing images. Dataset evidence SHA-256 is `de54bfd47c3b709557a149617384d70a9e65358ed5dc79a31bfb9bb1ee09b4c1`.
- Result: `1/300` correct (`0.0033` accuracy); normal `1/50` (`0.02`), abnormal `0/250`; 116 class-matching predictions were below the confidence gate.
- Decision: `hold`. The Washer weight must not be used, tuned, retrained, registered, or adopted for Switch Housing from this evidence.

### Closure and next dependency

The three requested steps have reproducible artifacts, explicit input mapping, evidence fingerprints, and a non-adoption decision. The next quality gate remains independently acquired production-camera/cross-session data with documented provenance; synthetic packages remain regression/format evidence only.
