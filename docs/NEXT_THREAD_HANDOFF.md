# Next Thread Handoff

Last updated: 2026-07-04 01:43 KST

## Start Here

1. Run `git status --short` first.
2. Read `AGENTS.md`.
3. Read this file.
4. Read `CODEX_NEXT_PROMPT.md`, `docs/WORK_TRACKING.md`, `docs/STABLE_VERIFIED_AREAS.md`, and `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`.
5. Inspect the current diff directly before deciding the next change.

## Current Priority

The active user priority is YOLO-based segmentation and anomaly detection integration, specifically YOLOv8/YOLO11 runtime, training, and inference behavior. Latest user direction is to prioritize YOLOv8 and segmentation where possible, and to keep YOLOv8 as a local runtime folder like the existing YOLOv5 setup.

Dataset interoperability WIP is no longer the immediate priority. It has been carried through CVAT segmentation import in the current worktree and is documented in the tracking/stable-area docs. Do not start another dataset format unless the user redirects priority back to interoperability.

## Repository State

- Workspace: `C:\Git\Labelling_Application`
- Product name: OpenVisionLab Labeling Studio
- Current branch at handoff: `main`
- Last completed local commit before this WIP: `fe0c514 refactor: centralize runtime unavailable text`
- There are many uncommitted WIP changes after that commit.
- Do not push unless the user explicitly says `push`.

## Latest Verified Runtime Work

Current local facts:

- Selected Python reports Ultralytics `8.0.132`.
- Cached `artifacts/model-runtime/ultralytics/yolo11n.pt` fails to load because the runtime lacks `C3k2`.
- No local `yolo11n-cls.pt` cache was found during the previous check.
- A real tiny YOLO11 classification smoke was attempted earlier and failed before training because `yolo11n-cls.pt` was not available.
- Local `C:\Git\yolov8` exists and contains `labelling_tcp_client.py`, `labeling_tcp_client.py`, `requirements.txt`, `data\train\images`, `.venv`, and `ultralyticsMaster`.
- `C:\Git\yolov8\ultralyticsMaster` is an official `ultralytics/ultralytics` checkout from `https://github.com/ultralytics/ultralytics.git`; latest verified source commit was `2c073adc0`.
- `C:\Git\yolov8\.venv` has an editable install from that local source. Import probe reported Ultralytics `8.4.86` from `C:\Git\yolov8\ultralyticsMaster\ultralytics\__init__.py`.
- `C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test` passes.
- `C:\Git\yolov8\yolov8n-seg.pt` was downloaded with explicit user approval and should be treated as a pretrained seed, not the final product model.
- Current `C:\Git\yolov8` worker reports YOLOv8 capability when `ultralytics` imports from the local source checkout and implements local-only `TrainYolo` start/status routing.

Implemented and verified behavior:

- `Runtime/Python/openvisionlab_ultralytics_worker.py` computes capabilities from the selected Python runtime.
- YOLOv8 is advertised when Ultralytics is installed.
- YOLO11 is advertised only when `yolo11_runtime_available()` finds `C3k2`.
- Health/capability payloads include `ultralyticsVersion`, `yolo11RuntimeAvailable`, and YOLO11-disabled `runtimeWarnings`.
- Unsupported YOLO11 detection/training errors include the installed Ultralytics version and missing `C3k2` reason.
- C# runtime adapter support no longer re-enables selected YOLO11 through static bundled-worker fallback when live worker capability lists exclude YOLO11.
- `PythonModelStatusProtocol`, `PythonCommunicationStatus`, and YOLO settings detail preserve/show runtime warnings plus segmentation/classification model capability lists.
- YOLOv8 runtime profile action now connects a local YOLOv8 worker folder through `PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection`, mapping `labelling_tcp_client.py`/`labeling_tcp_client.py`, local `.venv` Python when present, segmentation weights such as `yolov8n-seg.pt`, and `data/train/images`.
- Local `C:\Git\yolov8\labeling_tcp_client.py` now advertises YOLOv8 `trainingModels`, `detectionModels`, `segmentationModels`, and `classificationModels` when the selected Python runtime imports `ultralytics`.
- Local `C:\Git\yolov8\labeling_tcp_client.py` now prepends `ultralyticsMaster` before importing Ultralytics and reports `ultralyticsPath`, `localUltralyticsRoot`, and `localUltralyticsRootExists` diagnostics.
- Local `C:\Git\yolov8\labeling_tcp_client.py` now preserves segmentation `result.masks.xy` polygons on detection candidates as `segmentationType=polygon`, `polygonPoints`, and `normalizedPolygonPoints`.
- `PythonModelRuntimeAdapterSupportService` now treats a local YOLOv8 worker with installed Ultralytics and `TrainYolo` handling as training/inspection capable.
- Local `TrainYolo` now returns `TrainYoloResult`, streams `TrainingStatus`, passes task/data/epoch/image-size/batch values into local Ultralytics `YOLO(...).train(...)`, and refuses missing local weights rather than triggering implicit downloads.
- Local `StopTraining` now maps to `StopTask`, marks a running YOLOv8 training job as `stopping`, and uses Ultralytics trainer callbacks for cooperative cancellation. It is not a hard Python process kill.
- Local YOLOv8 folder connection now prefers trained weights over pretrained seeds: root `best.pt`, then latest `runs/segment/**/best.pt` or `runs/train/**/best.pt`, then `yolov8n-seg.pt`/`yolov8s-seg.pt`/`yolov8m-seg.pt`.
- Focused connection coverage now verifies that when a local YOLOv8 folder contains `yolov8n-seg.pt`, the historical `runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt`, the corrected `runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt`, and the newer app-generated `runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt`, the app selects the newest app-generated `runs\segment` trained model.
- WPF post-training best.pt selection now includes YOLOv8/Ultralytics `runs/segment/**/weights/best.pt` and reads `args.yaml` `data:` metadata, so a current-dataset YOLOv8 segmentation run is selected ahead of a newer foreign run.
- Bundled and local Ultralytics workers now route training output folders by task: `segment` to `runs/segment`, `classify` to `runs/classify`, and detect/default to `runs/train`.

## Latest Verified Training/Inference Contract Work

- YOLOv8/YOLO11 segmentation inference polygons are preserved from Ultralytics masks and can be confirmed as saved segmentation labels.
- Explicit YOLO worker smoke-result parsing preserves segmentation candidate `segmentationType`, `polygonPoints`, and `normalizedPolygonPoints` instead of collapsing smoke candidates to bbox/classification metadata only.
- Bundled Ultralytics `--smoke-test` now receives the selected adapter through `--model`, and unsupported adapter/runtime combinations return `UnsupportedModel` before model load. On the current Ultralytics `8.0.132` environment, `--smoke-test --model yolo11` reports the missing `C3k2` blocker directly.
- WPF current-image smoke status now uses `WpfDetectionResultPresentationService.BuildSmokeStatus(...)`, so unsupported runtime blockers such as `UnsupportedModel` / missing YOLO11 `C3k2` stay visible in status text instead of becoming only a generic test failure.
- Segmentation training packets carry `task=segment`.
- Production-path segmentation packet coverage verifies:
  - `model=yolov8`, `weight=yolov8n-seg.pt`
  - `model=yolo11`, `weight=yolo11n-seg.pt`
- Worker fake-training self-test coverage verifies YOLOv8/YOLO11 `segment` and `classify` task defaults without downloading models:
  - `yolov8n-seg.pt`
  - `yolov8n-cls.pt`
  - `yolo11n-seg.pt`
  - `yolo11n-cls.pt`
- Worker training weight resolution now prefers a matching cached task weight from the worker model root when present, then falls back to the existing bare Ultralytics model name.
- Worker `TrainYoloResult` and `TrainingStatus` payloads now include resolved `trainingWeights`; C# status parsing preserves it into YOLO settings detail.
- Worker health/model status now reports task-weight cache inventory as `cachedTrainingWeights` and `missingTrainingWeights`; the current local cache has only `yolo11n.pt`.
- Worker status also reports `runtimeReadyTrainingWeights` and `runtimeBlockedTrainingWeights`; current local `yolo11n.pt` is runtime-blocked, leaving no runtime-ready task weights for a no-download segmentation/classification smoke.
- Worker status also reports `downloadRequiredTrainingWeights` and `runtimeBlockedMissingTrainingWeights`; current probe shows `yolov8n.pt`, `yolov8n-seg.pt`, and `yolov8n-cls.pt` as download-required under the current runtime, while `yolo11n-seg.pt` and `yolo11n-cls.pt` are missing and runtime-blocked.
- YOLO settings detail translates those weight diagnostics into operator-facing labels and should not expose raw protocol key fragments such as `runtime-blocked-missing`.
- Worker training start blocks uncached bare default weights with `TrainingWeightDownloadRequired` unless the request explicitly opts into download with `allowModelDownload`, `allowWeightDownload`, or `allowDownload`. Current C# workflow does not send those flags.
- Worker self-test now verifies `allowModelDownload=true`, `allowWeightDownload=true`, and `allowDownload=true` each bypass only the worker guard and reach fake YOLOv8 segmentation training without real downloads.
- C# status parsing/communication handling has a focused fixture for failed `TrainYoloResult` with `TrainingWeightDownloadRequired`, preserving `state=failed`, the error text, and resolved `trainingWeights` into `PythonCommunicationStatus`.
- YOLO settings detail translates `TrainingWeightDownloadRequired` into operator-readable guidance while the protocol/communication layers keep the raw code for diagnostics.
- WPF training progress/recovery text also translates `TrainingWeightDownloadRequired`, prefers failed-status `LastError` over the generic worker message, and keeps the blocked task-weight filename visible.
- YOLOv8 segmentation training-guide history now also preserves the translated download guard and blocked `yolov8n-seg.pt` filename instead of storing only the generic worker message.
- Focused packet tests verify C# `StartTraining` payloads do not include `allowModelDownload`, `allowWeightDownload`, or `allowDownload`.
- Anomaly classification training exports reviewed normal/abnormal images, sends `task=classify`, and production-path packet coverage verifies:
  - `model=yolov8`, `weight=yolov8n-cls.pt`
  - `model=yolo11`, `weight=yolo11n-cls.pt`
- Insufficient anomaly normal/abnormal examples have operator-facing readiness detail.
- Local YOLOv8 folder connection is verified for settings/self-test/current-inspection readiness when a generated local worker fixture has `ultralytics` installed and `yolov8n-seg.pt` present. Training remains blocked until worker capability reports `TrainYolo`.
- With explicit user approval, local YOLOv8 segmentation training now has real tiny smokes using `C:\Git\yolov8\yolov8n-seg.pt` as a pretrained seed and `C:\Git\yolov8\data\smoke-seg` as the generated tiny dataset. The first historical smoke produced `C:\Git\yolov8\runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt` before task-aware run-folder routing was corrected.
- After the task-aware run-folder fix, a follow-up local adapter `TrainYolo` smoke with `task=segment`, `epochs=1`, `imgSize=64`, `batchSize=1`, `workers=0`, and `runName=openvisionlab-yolov8-seg-runs-segment-smoke` completed with final `TrainingStatus.state=completed` and produced `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt` plus `last.pt`.
- The generated `runs\segment` `best.pt` loaded successfully through `C:\Git\yolov8\labeling_tcp_client.py --smoke-test` against the smoke validation image. This proves plumbing, not accuracy.
- Focused connection coverage verifies that when a local YOLOv8 folder contains both `yolov8n-seg.pt` and training-run `best.pt` outputs, the app selects the trained `best.pt`; the newest coverage specifically locks the app-generated `runs\segment` smoke ahead of the corrected older `runs\segment` smoke, the historical `runs\train` smoke, and the pretrained seed.
- Focused WPF training-weight coverage verifies that YOLOv8 segmentation run metadata in `args.yaml` is used when staging the post-training candidate model.
- Focused WPF training-weight coverage now also verifies that when a YOLOv8 segmentation `results.csv` contains both box `(B)` and mask `(M)` metrics plus `val/box_loss` and `val/seg_loss`, the parser prefers the segmentation mask metrics and segmentation loss for presentation.
- Focused WPF training-session coverage now verifies that a mocked completed training flow can stage `runs\segment\<run>\weights\best.pt` as the pending inspection-model candidate through `PythonModelSettings.WeightsPath`.
- App-generated segmentation datasets now export Ultralytics YOLO segmentation label text before training starts: `YoloSegmentationTrainingLabelService` converts saved `segments/*.json` polygons into `labels/*.txt` lines when `YoloTrainingWorkflowService` prepares a `Segmentation` dataset, and focused coverage verifies the generated train/valid label files are polygon labels.
- A real local YOLOv8 training smoke has now run against an app-generated segmentation fixture at `artifacts\yolov8-app-segmentation-dataset\data.yaml`; it completed one CPU epoch and produced `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt`, which loaded through the local adapter `--smoke-test`.
- Focused WPF training-weight coverage now also combines those shapes without depending on external `C:\Git\yolov8`: it generates an app segmentation dataset fixture, creates a synthetic local YOLOv8 `runs\segment\app-segmentation-fixture\weights\best.pt` with `args.yaml data:` pointing at that fixture `data.yaml`, and verifies WPF treats it as current-dataset completed segmentation training.
- Local adapter segmentation polygon serialization is covered by `C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py`, `C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test`, C# `--python-detection-result-protocol`, C# `--yolo-worker-smoke-service`, an app-fixture `--smoke-test` model-load check, and low-confidence app-fixture train/valid smokes that returned one real `Defect` polygon candidate with normalized polygon points at `--conf 0.01`; the valid-image smoke also passes with `--model yolov8`, matching the C# smoke service's adapter argument shape. This remains tiny fixture evidence, so real operator segmentation data is still the production gate.
- Focused `--yolo-worker-smoke-service` coverage now also locks the C# smoke process argument shape for local YOLOv8 low-confidence segmentation smokes: `--model yolov8`, `--conf 0.01`, and `--img-size 64`.
- WPF current-image smoke status now appends a mask count when successful smoke candidates contain segmentation polygons, owned by `WpfDetectionResultPresentationService` and covered by `--wpf-detection-result-presentation`.
- Real TCP workflow coverage now has an explicit YOLOv8 segmentation gate: `--real-yolo-smoke` with `LABELING_SMOKE_EXPECT_SEGMENTATION=true`, local `C:\Git\yolov8\labeling_tcp_client.py`, the app-fixture `runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt`, `--conf 0.01`, and `--img-size 64` passed. Artifact `artifacts\real-yolo-smoke\20260704-010540\summary.txt` recorded `polygonCandidateCount=1`, `segmentExists=True`, and `maskExists=True`; the focused smoke also asserted segment polygons/points and non-empty mask pixels.
- WPF Candidate Review confirmed AI polygon candidates are now included in segmentation persistence through `WpfLabelingShellWindow.AnnotationPersistence.cs`; `--wpf-segmentation-object-verification` covers the actual Candidate Review confirm command, segment JSON restoration, non-empty mask PNG output, `TryPrepareTrainingDataset` export into an Ultralytics YOLO segment label line, and a mocked YOLOv8 `StartTraining` packet with `task=segment` and `yolov8n-seg.pt`.
- `--wpf-candidate-polygon-training-flow` is the focused single-test switch for the same Candidate Review polygon -> artifact -> YOLOv8 segment training packet path.
- Final local adapter sanity recheck passed: `labeling_tcp_client.py` compiles, imports editable Ultralytics `8.4.86` from `C:\Git\yolov8\ultralyticsMaster`, passes `--self-test`, and the app-fixture `best.pt` smoke returns one `Defect` polygon candidate with `--model yolov8 --conf 0.01 --img-size 64`.

Key changed files in these slices:

- `Runtime/Python/openvisionlab_ultralytics_worker.py`
- `C:\Git\yolov8\labeling_tcp_client.py`
- `C:\Git\yolov8\README.md`
- `1. Core/YoloTrainingWorkflowService.cs`
- `Yolo/YoloSegmentationTrainingLabelService.cs`
- `1. Core/PythonModelRuntimeAdapterSupportService.cs`
- `1. Core/PythonModelRuntimeConnectionService.cs`
- `1. Core/PythonModelRuntimeProfileService.cs`
- `3. Communication/TCP/PythonModelStatusProtocol.cs`
- `3. Communication/TCP/PythonCommunicationStatus.cs`
- `3. Communication/TCP/CCommunicationLearning.cs`
- `0. UI/9) WPF/Services/WpfTrainingProgressPresentationService.cs`
- `0. UI/9) WPF/Services/WpfTrainingGuideHistoryService.cs`
- `0. UI/9) WPF/Services/WpfYoloSettingsPanelStatusPresentationService.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationPersistence.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloEnvironmentBrowseCommands.cs`
- `tests/LabelingApplication.Tests/Program.cs`
- `docs/WORK_TRACKING.md`
- `docs/STABLE_VERIFIED_AREAS.md`
- `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`
- `CODEX_NEXT_PROMPT.md`
- `docs/NEXT_THREAD_HANDOFF.md`

Latest verification commands that passed:

```powershell
python .\Runtime\Python\openvisionlab_ultralytics_worker.py --self-test
python -m py_compile .\Runtime\Python\openvisionlab_ultralytics_worker.py
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
C:\Git\yolov8\.venv\Scripts\python.exe -c "import ultralytics, pathlib; print(ultralytics.__version__); print(pathlib.Path(ultralytics.__file__).resolve())"
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --smoke-test --weights C:\Git\yolov8\runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt --image C:\Git\yolov8\data\smoke-seg\images\val\part_val.png --model-root C:\Git\yolov8 --image-root C:\Git\yolov8\data\smoke-seg\images\val --device cpu --img-size 64
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --smoke-test --weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt --image C:\Git\yolov8\data\smoke-seg\images\val\part_val.png --model-root C:\Git\yolov8 --image-root C:\Git\yolov8\data\smoke-seg\images\val --device cpu --img-size 64
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --smoke-test --weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt --image C:\Git\Labelling_Application\artifacts\yolov8-app-segmentation-dataset\data\valid\images\purpose-valid.jpeg --model-root C:\Git\yolov8 --image-root C:\Git\Labelling_Application\artifacts\yolov8-app-segmentation-dataset\data\valid\images --device cpu --img-size 64
python .\Runtime\Python\openvisionlab_ultralytics_worker.py --smoke-test --model yolo11 --weights .\Runtime\Python\openvisionlab_ultralytics_worker.py --image .\Runtime\Python\openvisionlab_ultralytics_worker.py --model-root .\Runtime\Python
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-ultralytics-worker
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --yolo-worker-smoke-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-detection-result-presentation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-yolo-smoke # with LABELING_SMOKE_EXPECT_SEGMENTATION=true and local YOLOv8 env vars
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-polygon-training-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --detection-result-segmentation-confirm
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-readiness-purpose
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --yolov8-segmentation-app-dataset-fixture
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --learning-protocol
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-status-protocol
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-connection
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-weights-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-self-test
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-status-summaries
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-guide-history
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

`git diff --check` passed with LF-to-CRLF warnings only.

## Remaining Risk

- A real YOLO11 segmentation training smoke has not run.
- A real YOLOv8/YOLO11 anomaly classification training/evaluation smoke has not run.
- Tiny local YOLOv8 segmentation training/inference smokes have run, including one app-generated dataset fixture, and local connection now prefers trained `best.pt`, but they are not production dataset or accuracy gates.
- The local adapter now serializes segmentation polygons when masks exist, and low-confidence app-fixture train/valid smokes produce one real polygon candidate each. Production readiness still needs a real operator segmentation dataset/model gate.
- The real TCP segmentation smoke now proves the tiny app-fixture polygon candidate can pass through the local YOLOv8 TCP worker, C# `ResultDefect` parser, overlay/confirm path, and save segment JSON plus mask PNG artifacts. It is still low-confidence tiny-fixture evidence, not production accuracy.
- The WPF Candidate Review save path now preserves confirmed AI polygon candidates as segment JSON/mask PNG artifacts, exports them into YOLOv8 segment label txt during training preparation, and sends the expected YOLOv8 segmentation training packet, but this is still focused fixture evidence, not production model accuracy.
- The final local adapter sanity recheck still uses the tiny app-fixture model/image. It is not a production accuracy gate.
- The earlier tiny smoke artifact remains under `runs\train` because it was produced before task-aware run-folder routing; the corrected smoke now exists under `runs\segment`.
- The local YOLOv8 folder connection and local worker `TrainYolo` start/status path are implemented, and `C:\Git\yolov8` now has a local `.venv`, editable `ultralyticsMaster`, `yolov8n-seg.pt` pretrained seed, and generated smoke `best.pt`.
- YOLOv8 training cancellation is cooperative via Ultralytics callbacks; it is not a YOLOv5-style subprocess termination path or hard process kill.
- Current bundled-worker task-weight cache still blocks YOLO11 and anomaly-classification smokes because missing YOLO11 task weights are runtime-blocked and no local `yolov8n-cls.pt` classification seed has been approved/downloaded yet. The worker will fail before implicit Ultralytics downloads unless a request explicitly opts in.
- The current local YOLO11 runtime is correctly blocked until the Ultralytics runtime and/or model cache supports YOLO11.
- Do not claim broad YOLO11 execution readiness from fake-training or packet tests.

## Older WPF Status WIP

There is still older WPF inference-status WIP in the worktree from before the YOLO runtime priority took over:

- `0. UI/9) WPF/Services/WpfInferenceStatusPresentationService.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloRuntimeStatus.cs`
- `tests/LabelingApplication.Tests/Program.cs`

Original intent:

- Move remaining `RefreshYoloStatus` runtime-ready and missing-inspection-model wording into `WpfInferenceStatusPresentationService`.
- Keep `WpfLabelingShellWindow.YoloRuntimeStatus.cs` as runtime-state/UI adapter.

Do not claim this older WPF WIP complete unless its own focused gates pass:

```powershell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-inference-status-presentation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra
```

## Next Recommended Work

1. Continue YOLOv8/YOLO11 segmentation/anomaly runtime integration.
2. Run YOLOv8 segmentation training/inference against a real operator segmentation dataset, then promote the generated `runs\segment\<run>\weights\best.pt` as the active inspection model if acceptable.
3. Continue with the next segmentation/anomaly runtime gap that strengthens real project training, inference, or operator diagnostics.
4. Keep changes narrow and verify with build, focused tests, and `git diff --check`.

No screenshot is required for runtime/status text, Python worker changes, or packet-test-only changes unless WPF layout/visual styling changes.
