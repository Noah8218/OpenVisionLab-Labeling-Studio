# Next Codex Prompt

Copy this into the next Codex chat when continuing `C:\Git\Labelling_Application`.

```text
Continue work in C:\Git\Labelling_Application.

Start order:

1. Run `git status --short` first.
2. Read `AGENTS.md`.
3. Read `docs/NEXT_THREAD_HANDOFF.md`.
4. Read `CODEX_NEXT_PROMPT.md`, `docs/WORK_TRACKING.md`, `docs/STABLE_VERIFIED_AREAS.md`, and `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`.
5. Inspect the current diff directly and continue from the actual worktree state.

Rules:

- Do not revert existing changes unless explicitly requested.
- Do not run `git push` unless the user explicitly asks for `push`.
- A commit request means a local commit only.
- Preserve MVVM boundaries. View code-behind should stay a UI adapter; command/state/workflow/presentation logic should live in ViewModel or Service classes where feasible.
- Do not touch Viewer/OpenGL/ROI/brush/eraser hot paths unless the task requires it and focused gates are included.
- Do not guess. Open files, tests, or logs when unsure.
- Completion requires build/focused tests/git diff --check evidence.
- Keep changes narrow.
- If no UI layout/visual styling changed, state that no screenshot is needed.
- If UI changes, capture 1920x1080 and check README/tutorial images.

Current priority:

- User priority is YOLO-based YOLOv8/YOLO11 segmentation and anomaly detection integration.
- Latest user direction: prioritize YOLOv8 and segmentation where possible.
- Dataset interoperability WIP is not the immediate priority. It is documented through CVAT segmentation import.
- Do not start model downloads or `pip install --upgrade` without explicit approval.

Latest verified YOLO runtime state:

- Local Ultralytics version observed: `8.0.132`.
- Cached `artifacts/model-runtime/ultralytics/yolo11n.pt` failed to load because the runtime lacks `C3k2`.
- No local `yolo11n-cls.pt` cache was found during the previous check.
- Latest user direction: YOLOv8 should follow the existing YOLOv5 pattern by using the local `C:\Git\yolov8` worker folder rather than an app-owned hidden weight cache.
- The app now has `PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection`, and the YOLOv8 runtime profile action maps a selected local YOLOv8 folder to `labelling_tcp_client.py`/`labeling_tcp_client.py`, local `.venv` Python when present, a segmentation weight candidate such as `yolov8n-seg.pt`, and `data/train/images`.
- Current `C:\Git\yolov8` has `.venv` and an official `ultralytics/ultralytics` checkout at `ultralyticsMaster`; the venv imports Ultralytics `8.4.86` from `C:\Git\yolov8\ultralyticsMaster\ultralytics\__init__.py`.
- Current `C:\Git\yolov8\yolov8n-seg.pt` was downloaded with explicit user approval and should be treated as a pretrained seed, not the final product model.
- Current `C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test` passes. The local YOLOv8 worker now prepends `ultralyticsMaster`, reports local source diagnostics, reports YOLOv8 capability when `ultralytics` imports, implements local-only `TrainYolo` start/status routing, and handles `StopTraining` cooperatively through Ultralytics callbacks.
- The local `C:\Git\yolov8\labeling_tcp_client.py` worker now preserves segmentation `result.masks.xy` polygons on detection candidates as `segmentationType=polygon`, `polygonPoints`, and `normalizedPolygonPoints`.
- Local YOLOv8 folder connection now prefers trained weights over pretrained seeds: root `best.pt`, then latest `runs/segment/**/best.pt` or `runs/train/**/best.pt`, then `yolov8n-seg.pt`/`yolov8s-seg.pt`/`yolov8m-seg.pt`.
- Focused `--python-model-runtime-connection` coverage verifies the local folder connection chooses the newest app-generated `runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt` over the corrected older `runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt`, the historical `runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt`, and pretrained `yolov8n-seg.pt`.
- WPF post-training best.pt selection now includes YOLOv8/Ultralytics `runs/segment/**/weights/best.pt` and reads `args.yaml` `data:` metadata, so a current-dataset YOLOv8 segmentation run is selected ahead of a newer foreign run.
- Bundled and local Ultralytics workers now route training output folders by task: `segment` to `runs/segment`, `classify` to `runs/classify`, and detect/default to `runs/train`.
- The worker now advertises YOLO11 only when `yolo11_runtime_available()` finds `C3k2`.
- Health/status payloads include `ultralyticsVersion`, `yolo11RuntimeAvailable`, and YOLO11-disabled `runtimeWarnings`.
- C# runtime adapter support does not re-enable YOLO11 through static fallback when live worker capability lists exclude YOLO11.
- Status parsing and YOLO settings detail preserve worker runtime warnings plus segmentation/classification capability lists.

Latest verified training/inference contract work:

- YOLOv8/YOLO11 segmentation inference polygons are preserved by the worker/protocol and can be confirmed as saved segmentation labels.
- Explicit YOLO worker smoke-result parsing now preserves segmentation candidate `segmentationType`, `polygonPoints`, and `normalizedPolygonPoints` instead of collapsing smoke candidates to bbox/classification metadata only.
- `YoloWorkerSmokeTestService` now passes the selected adapter into bundled Ultralytics `--smoke-test` with `--model`, and the worker returns `UnsupportedModel` before model load when the current runtime capability list excludes that adapter. On the current Ultralytics `8.0.132` environment, `--smoke-test --model yolo11` reports the missing `C3k2` blocker directly.
- WPF current-image smoke status is built by `WpfDetectionResultPresentationService.BuildSmokeStatus(...)` and preserves unsupported runtime blockers such as `UnsupportedModel` / missing YOLO11 `C3k2` instead of showing only a generic test failure.
- Segmentation training packets now carry `task=segment`.
- Production-path segmentation packet coverage verifies:
  - `model=yolov8`, `weight=yolov8n-seg.pt`
  - `model=yolo11`, `weight=yolo11n-seg.pt`
- Local `C:\Git\yolov8\labeling_tcp_client.py` now returns `TrainYoloResult`, streams `TrainingStatus`, passes task/data/epoch/image-size/batch values into local Ultralytics `YOLO(...).train(...)`, and refuses missing local weights instead of triggering implicit downloads.
- Local YOLOv8 segmentation training smokes have passed with the generated tiny dataset `C:\Git\yolov8\data\smoke-seg` and pretrained seed `C:\Git\yolov8\yolov8n-seg.pt`. The historical pre-routing-fix run produced `C:\Git\yolov8\runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt`; the current task-aware run produced `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt` and `last.pt`. The generated `runs\segment` `best.pt` loaded through `labeling_tcp_client.py --smoke-test`. This proves plumbing only, not production accuracy.
- Focused `--python-model-runtime-connection` coverage verifies that if the local YOLOv8 folder contains a pretrained seed plus training-run `best.pt` outputs, the app selects the trained `best.pt`; the current fixture specifically locks the corrected `runs/segment` smoke ahead of the older historical `runs/train` smoke.
- Focused `--wpf-training-weights-service` coverage verifies that YOLOv8 segmentation run metadata in `args.yaml` is used when staging the post-training candidate model.
- Focused `--wpf-training-weights-service` coverage also verifies that a YOLOv8 segmentation `results.csv` with both box `(B)` and mask `(M)` metrics plus `val/box_loss` and `val/seg_loss` uses the mask metrics and segmentation loss for presentation.
- Focused `--wpf-yolo-training-session` coverage verifies that a mocked completed WPF training flow can stage `runs/segment/<run>/weights/best.pt` as the pending inspection-model candidate through `PythonModelSettings.WeightsPath`.
- Focused `--dataset-readiness-purpose` coverage now verifies that before YOLOv8/YOLO11 segmentation `StartTraining`, app segment JSON polygons are converted into Ultralytics YOLO segmentation `labels/*.txt` lines through `YoloSegmentationTrainingLabelService`.
- Focused `--yolov8-segmentation-app-dataset-fixture` now leaves an ignored app-generated segmentation dataset under `artifacts/yolov8-app-segmentation-dataset`; a direct local YOLOv8 worker 1-epoch smoke against that fixture produced `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt`, and that `best.pt` loaded through `labeling_tcp_client.py --smoke-test`.
- Focused `--wpf-training-weights-service` now also generates an app segmentation dataset fixture and verifies a synthetic local YOLOv8 `runs/segment/app-segmentation-fixture/weights/best.pt` with `args.yaml data:` pointing at that app `data.yaml` is staged as current-dataset completed segmentation training.
- Local adapter segmentation polygon serialization is covered by local venv Python compile, local worker `--self-test`, C# `--python-detection-result-protocol`, C# `--yolo-worker-smoke-service`, an app-fixture `--smoke-test` model-load check, and low-confidence app-fixture train/valid smokes that returned one real `Defect` polygon candidate with normalized polygon points at `--conf 0.01`; the valid-image smoke also passes with `--model yolov8`, matching the C# smoke service's adapter argument shape. This is still tiny fixture evidence, so real operator segmentation data remains the production gate.
- Focused `--yolo-worker-smoke-service` coverage now also locks the C# smoke process argument shape for local YOLOv8 low-confidence segmentation smokes: `--model yolov8`, `--conf 0.01`, and `--img-size 64`.
- WPF current-image smoke status now appends a mask count when successful smoke candidates contain segmentation polygons, owned by `WpfDetectionResultPresentationService` and covered by `--wpf-detection-result-presentation`.
- Real TCP workflow coverage now has an explicit YOLOv8 segmentation gate: `--real-yolo-smoke` with `LABELING_SMOKE_EXPECT_SEGMENTATION=true`, local `C:\Git\yolov8\labeling_tcp_client.py`, the app-fixture `runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt`, `LABELING_SMOKE_CONFIDENCE=0.01`, and `LABELING_SMOKE_IMAGE_SIZE=64` passed. Artifact `artifacts\real-yolo-smoke\20260704-010540\summary.txt` recorded `polygonCandidateCount=1`, `segmentExists=True`, and `maskExists=True`; the focused smoke also asserted segment polygons/points and non-empty mask pixels.
- WPF Candidate Review confirmed AI polygon candidates now flow into segmentation persistence through `WpfLabelingShellWindow.AnnotationPersistence.cs`; `--wpf-segmentation-object-verification` covers the actual Candidate Review confirm command, segment JSON restoration, non-empty mask PNG output, `TryPrepareTrainingDataset` export into an Ultralytics YOLO segment label line, and a mocked YOLOv8 `StartTraining` packet with `task=segment` and `yolov8n-seg.pt`.
- `--wpf-candidate-polygon-training-flow` is the focused single-test switch for the same Candidate Review polygon -> artifact -> YOLOv8 segment training packet path.
- Final local adapter sanity recheck passed: `labeling_tcp_client.py` compiles, imports editable Ultralytics `8.4.86` from `C:\Git\yolov8\ultralyticsMaster`, passes `--self-test`, and the app-fixture `best.pt` smoke returns one `Defect` polygon candidate with `--model yolov8 --conf 0.01 --img-size 64`.
- Worker self-test fake-training coverage verifies YOLOv8/YOLO11 `segment` and `classify` task defaults without downloading models.
- Worker training weight resolution now prefers a matching cached task weight from the worker model root when present, then falls back to the existing bare Ultralytics model name.
- Worker `TrainYoloResult` and `TrainingStatus` payloads now include resolved `trainingWeights`; C# status parsing preserves it into YOLO settings detail.
- Worker health/model status now reports task-weight cache inventory as `cachedTrainingWeights` and `missingTrainingWeights`; current local cache has only `yolo11n.pt`.
- Worker status also reports `runtimeReadyTrainingWeights` and `runtimeBlockedTrainingWeights`; current local `yolo11n.pt` is runtime-blocked, so there are no runtime-ready task weights.
- Worker status also splits missing task weights into `downloadRequiredTrainingWeights` and `runtimeBlockedMissingTrainingWeights`; current probe shows YOLOv8 defaults require download/cache population, while missing YOLO11 segment/classify weights are runtime-blocked under Ultralytics `8.0.132`.
- YOLO settings detail translates those weight diagnostics into operator-facing labels and should not expose raw protocol key fragments such as `runtime-blocked-missing`.
- Worker training start now blocks uncached bare default weights with `TrainingWeightDownloadRequired` unless the request explicitly opts into download with `allowModelDownload`, `allowWeightDownload`, or `allowDownload`. Current C# workflow does not send those flags.
- Worker self-test now verifies `allowModelDownload=true`, `allowWeightDownload=true`, and `allowDownload=true` each bypass only the worker guard and reach fake YOLOv8 segmentation training without real downloads.
- C# status parsing/communication handling has a focused fixture for failed `TrainYoloResult` with `TrainingWeightDownloadRequired`, preserving `state=failed`, the error text, and resolved `trainingWeights` into `PythonCommunicationStatus`.
- YOLO settings detail translates `TrainingWeightDownloadRequired` into operator-readable guidance while the protocol/communication layers keep the raw code for diagnostics.
- WPF training progress/recovery text also translates `TrainingWeightDownloadRequired`, prefers the failed status `LastError` over the generic worker message, and keeps the blocked task weight filename visible.
- YOLOv8 segmentation training-guide history now also preserves the translated download guard and blocked `yolov8n-seg.pt` filename instead of storing only the generic worker message.
- Focused packet tests verify C# `StartTraining` payloads do not include `allowModelDownload`, `allowWeightDownload`, or `allowDownload`.
- Anomaly classification training exports reviewed normal/abnormal images, sends `task=classify`, and production-path packet coverage verifies:
  - `model=yolov8`, `weight=yolov8n-cls.pt`
  - `model=yolo11`, `weight=yolo11n-cls.pt`

Known WIP:

- Last completed local commit before this WIP: `fe0c514 refactor: centralize runtime unavailable text`.
- Many WIP changes remain uncommitted.
- Older WPF inference status WIP may still be present:
  - `0. UI/9) WPF/Services/WpfInferenceStatusPresentationService.cs`
  - `0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloRuntimeStatus.cs`
  - `tests/LabelingApplication.Tests/Program.cs`
- Do not claim that older WPF status WIP complete unless these pass:
  - `--wpf-labeling-shell`
  - `--wpf-inference-status-presentation`
  - `--mvvm-infra`

Recommended next work:

1. Re-run `git status --short` and inspect the diff.
2. If staying on YOLOv8/YOLO11 priority, run YOLOv8 segmentation training/inference against a real operator segmentation dataset and promote the generated `runs/segment/<run>/weights/best.pt` as the active inspection model only after focused evidence.
3. Do not run more downloads or package upgrades without explicit approval.
4. Verify narrow changes with build, focused tests, and `git diff --check`.
5. Record only passed work in tracking/stable/audit docs.

Useful verification commands:

```powershell
python .\Runtime\Python\openvisionlab_ultralytics_worker.py --self-test
python -m py_compile .\Runtime\Python\openvisionlab_ultralytics_worker.py
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
C:\Git\yolov8\.venv\Scripts\python.exe -c "import ultralytics, pathlib; print(ultralytics.__version__); print(pathlib.Path(ultralytics.__file__).resolve())"
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --smoke-test --weights C:\Git\yolov8\runs\train\openvisionlab-yolov8-seg-smoke\weights\best.pt --image C:\Git\yolov8\data\smoke-seg\images\val\part_val.png --model-root C:\Git\yolov8 --image-root C:\Git\yolov8\data\smoke-seg\images\val --device cpu --img-size 64
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --smoke-test --weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt --image C:\Git\yolov8\data\smoke-seg\images\val\part_val.png --model-root C:\Git\yolov8 --image-root C:\Git\yolov8\data\smoke-seg\images\val --device cpu --img-size 64
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --smoke-test --weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-app-seg-fixture-smoke\weights\best.pt --image C:\Git\Labelling_Application\artifacts\yolov8-app-segmentation-dataset\data\valid\images\purpose-valid.jpeg --model-root C:\Git\yolov8 --image-root C:\Git\Labelling_Application\artifacts\yolov8-app-segmentation-dataset\data\valid\images --device cpu --img-size 64
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
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-status-summaries
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-training-guide-history
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
git diff --check
```

Final report should include changed files, verification commands/results, screenshot need, remaining risk/unverified items, and next work.
```
