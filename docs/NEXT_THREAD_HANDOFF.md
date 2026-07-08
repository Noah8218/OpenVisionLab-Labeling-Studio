# Next Thread Handoff

Last updated: 2026-07-08 KST

This is the current handoff for `C:\Git\Labelling_Application`. Treat older July 3-4 handoff notes as historical unless the current worktree or tracking docs prove the same item is still active.

## Start Here

1. Run `git status --short` first.
2. Read `AGENTS.md`.
3. Read this file.
4. Read `CODEX_NEXT_PROMPT.md`, `docs/WORK_TRACKING.md`, `docs/STABLE_VERIFIED_AREAS.md`, and `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`.
5. Inspect the actual diff before selecting work. If the worktree is dirty, the diff is the source of truth.

## Repository Checkpoint

- Workspace: `C:\Git\Labelling_Application`
- Branch at handoff: `main`
- Tracking state at handoff: `main...origin/main`
- Latest pushed commit at handoff: `84666293 build: use bundled noah library dlls`
- Expected worktree at handoff creation: dirty local continuation work for YOLOv8 SEG promotion guards, anomaly classification runtime/evaluation, docs, and tests. Trust `git status --short` and the current diff.
- Do not run `git push` unless the user explicitly asks for `push`.
- A request to `commit` means local commit only unless `push` is also explicitly requested.

## Product Direction

OpenVisionLab Labeling Studio is a local Windows workstation for industrial image labeling and model workflow. The current product shape is local-first, not cloud/team collaboration.

Current priority order from the user conversation:

1. Current user override: UI/UX first, especially simplifying the left workflow panels so beginners see the current task before detailed guides.
2. YOLOv8 segmentation operation and model-quality workflow after the current UI/UX pass.
3. YOLOv8 anomaly/classification real operator-data runtime/model smoke after segmentation workflow is stable.
4. YOLO11 only after the local Ultralytics runtime and weights actually support it.
5. Dataset interoperability only when the model/runtime priority is paused or the user redirects.

Keep the existing MVVM direction. View code-behind may adapt WPF events and controls, but workflow state, commands, presentation text, and status decisions should live in ViewModel or service classes where feasible.

## User Agreements Captured In This Thread

- YOLOv8 should follow the existing YOLOv5-style local source workflow, not a hidden app-owned runtime cache.
- The local YOLOv8 folder is `C:\Git\yolov8`.
- `C:\Git\yolov8` should use a local `ultralytics/ultralytics` checkout, local `.venv`, editable install, and `labeling_tcp_client.py` TCP adapter.
- ONNX is useful for inference-only deployment, but it does not replace the local Ultralytics source workflow for training.
- Do not run model downloads, package upgrades, or dependency upgrades without explicit user approval.
- `yolov8n-seg.pt` was downloaded earlier with explicit approval and should be treated as a pretrained seed, not a final production model.
- `yolov8n-cls.pt` was downloaded with explicit approval on 2026-07-07 and should also be treated as a pretrained seed for runtime smoke, not a trained anomaly model.
- The brush/eraser/Viewer/OpenGL/ROI performance path is protected. Touch it only for a specific bug and include focused tests or EXE evidence when requested.
- Completion must be proven with build, focused tests, and `git diff --check`; do not claim completion from wording alone.
- If UI layout or visible workflow changes, capture current 1920x1080 evidence and refresh public tutorial/README images when relevant.
- If no UI visual/layout change happened, say screenshot is not needed.
- Latest UI wording evidence: canvas/guide/onboarding workflow text now uses neutral `image queue` wording instead of `left image queue` or `left-side image queue`; captures `artifacts\ui\wpf-startup-image-queue-neutral-after-1920.png` and `artifacts\ui\wpf-startup-image-queue-neutral-guide-after-1920.png`.
- Latest AI-candidate guidance evidence: pending AI candidates now resolve the guide/canvas current task as candidate review, not run-inspection. The compact guide checklist is now state-specific (`이미지/열기/라벨`, `그리기/저장/다음`, `검사/확인/검토`, or `확인/확정/스킵`); capture `artifacts\ui\wpf-labeling-guide-ai-candidate-checklist-after-1920.png`.
- Latest labeling guide naming evidence: the labeling-stage guide/tools shortcut now reads `작업`, uses a clipboard/current-work icon, and the expanded panel title reads `현재 작업`, so the panel presents the current-image next action instead of a broad all-in-one guide/tool bucket. Before/after captures: `artifacts\ui\wpf-labeling-current-work-guide-rename-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-work-guide-rename-after-20260708-1920.png`, and `artifacts\ui\wpf-labeling-current-work-icon-after-20260708-1920.png`. Public tutorial 03 source/annotated PNGs and the standalone embedded image were refreshed to show `작업` instead of `도구`.
- Latest labeling guide optional-details evidence: the secondary collapsed details section now reads `필요할 때만: 학습·검사 세부` instead of `도움말/세부 도구`, and it remains collapsed by default. Captures: `artifacts\ui\wpf-labeling-details-expander-header-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-details-expander-header-after-20260708-1920.png`.
- Latest labeling guide flow-hint evidence: the current-task sequence now renders as one low-emphasis line such as `흐름: 확인 > 확정 > 스킵` instead of three bordered boxes that looked like extra action buttons. Captures: `artifacts\ui\wpf-labeling-current-task-flow-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-flow-after-20260708-1920.png`.
- Latest labeling guide action-emphasis evidence: the current-task explanatory sentence now uses primary text instead of the global red accent, while the step badge and actual action controls keep their emphasis. Captures: `artifacts\ui\wpf-labeling-current-task-action-emphasis-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-action-emphasis-after-20260708-1920.png`.
- Latest labeling guide card-caption evidence: the expanded panel title stays `현재 작업`, but the first task card caption now reads `현재 이미지` to avoid repeating the same label. Captures: `artifacts\ui\wpf-labeling-current-task-card-caption-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-card-caption-after-20260708-1920.png`.
- Latest labeling guide card-border evidence: the current-image task card now uses a neutral border, reserving the red accent for the step badge and actual action controls. Captures: `artifacts\ui\wpf-labeling-current-task-card-border-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-card-border-after-20260708-1920.png`.
- Latest no-object tooltip evidence: canvas no-object completion now says no label/empty YOLO label file instead of no box; covered by `--wpf-labeling-shell`.
- Latest right-workflow mode evidence: Candidate Review now enters inference mode and Saved Labels enters labeling mode; covered by `--wpf-labeling-shell`.
- Latest focused regression sweep after the guide UX pass: `--wpf-model-comparison-heldout`, `--wpf-model-comparison-run-service`, `--wpf-candidate-polygon-training-flow`, `--wpf-segmentation-object-verification`, `--dataset-readiness-purpose`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-model-comparison-review-service`, `--anomaly-classification-evaluation`, `--wpf-yolov8-anomaly-classification-runtime-smoke`, YOLOv8 adapter compile/self-test, PowerShell parser checks for the YOLO comparison/evaluation scripts, solution build, output-copy check for checked-in Noah/OpenCvSharp DLL dependencies, and `--wpf-responsive-layout --width 1920 --height 1080` passed on 2026-07-08 KST.

## Completed And Verified Work

### YOLOv8 Local Segmentation Workflow

- `C:\Git\yolov8` is connected as a local source worker folder.
- The local worker imports Ultralytics from `C:\Git\yolov8\ultralyticsMaster` through the local `.venv`.
- The worker reports YOLOv8 training/detection/segmentation/classification capability when Ultralytics imports.
- Local `TrainYolo` routes `task=segment` to Ultralytics training and refuses missing local weights instead of triggering implicit downloads.
- Training output is task-aware: segmentation runs use `runs/segment`.
- Local folder connection prefers trained `best.pt` outputs over pretrained seeds.
- App segmentation artifacts are exported into YOLO segmentation label text before training.
- YOLOv8 segmentation candidates preserve mask polygons through the worker/protocol.
- Candidate Review confirmation can save AI polygon candidates as segment JSON and mask PNG, then export them into YOLO segmentation labels.
- Real TCP tiny-fixture smoke passed for local YOLOv8 segmentation polygon candidate -> C# parser -> confirm -> segment JSON/mask PNG.
- Held-out comparison on the circular SEG dataset ran, but it is model-quality evidence only and not production accuracy.
- The current EXE circular segmentation rerun after the DLL output fix passed with `--label-count 20`: artifact `artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147`, `trainSegments=10`, `validSegments=5`, `testSegments=5`, `backgroundLabels=20`, and final YOLOv8 inference produced one low-confidence `NG 1.3%` candidate.
- A follow-up same-class YOLOv8 SEG held-out comparison on that 20-label artifact wrote `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-userseed-20260707\20260707-222120\comparison-summary.json`. Evidence counts now pass (`11/10` labels, `5/5` positive labels, `5/5` positive images), but promotion remains `hold` because candidate precision is `0.098 < 0.1` and UI-threshold candidates at confidence `0.25` are still `0`.
- A follow-up 80-epoch local fine-tune from that userseed weight wrote `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt`. Its held-out comparison wrote `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260707\20260707-222900\comparison-summary.json`, passed the evidence gates, scored precision `1.0`, recall `0.589`, mAP50 `0.767`, mAP50-95 `0.257`, produced `2` UI-threshold candidates at confidence `0.25`, and returned `promotion.recommendation=promote`.
- Direct local adapter smoke for that fine-tuned weight wrote `artifacts\yolo-smoke\finetune80-20260707`: `025_NG` returned one `NG` polygon candidate at confidence `0.768`, `024_NG` returned one `NG` polygon candidate at confidence `0.507`, and `011_OK` returned zero candidates at confidence `0.25`.
- Focused WPF Model Center adoption smoke now stages and saves that same fine-tuned weight as the current inspection model using the actual `C:\Git\yolov8` run and circular SEG `data.yaml`; capture `artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png`, test recipe config under `tests\LabelingApplication.Tests\artifacts\isolated-out\RECIPE\real_model_center_smoke_*\VISION.xml`.
- Current-image TCP workflow smoke for the same fine-tuned weight wrote `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260707`: `025_NG` produced one `NG` polygon candidate at confidence `0.7682`, then confirmation saved YOLO label text, segment JSON, and mask PNG.
- Latest current-image TCP recheck for the same fine-tuned weight wrote `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260708-recheck`: `025_NG` again produced one `NG` polygon candidate at confidence `0.7682`, and confirmation saved label text, segment JSON, and mask PNG. Latest Model Center save/adoption capture: `artifacts\ui\wpf-model-center-real-finetune-save-recheck-20260708-1920.png`.

Do not overstate this as broad production-ready accuracy. The circular SEG artifact now has a promotable candidate, direct adapter evidence, focused Model Center save/adoption evidence, and one current-image TCP workflow smoke, but real deployment still needs more operator data for other product/lighting/defect variation.

### Anomaly Workflow

- Image-level anomaly review state exists: normal, abnormal, unreviewed.
- Reviewed anomaly images can export to Ultralytics classification layout.
- Training packets can send `task=classify` with YOLOv8/YOLO11 classification default weights.
- Readiness guidance exists for insufficient normal/abnormal examples.
- Local YOLOv8 anomaly/classification runtime smoke now loads `C:\Git\yolov8\yolov8n-cls.pt` through `C:\Git\yolov8\labeling_tcp_client.py` and returns a non-empty image-level classification candidate.
- The local adapter now mirrors the bundled worker's classification candidate shape: `candidateType=imageClassification`, `predictionType=classification`, `imageLevel=true`, class id/name, confidence, and zero-sized bbox fields.
- WPF app-level YOLOv8 anomaly classification runtime smoke now calls the local adapter through `RunDetectionForImageAsync`, maps the returned top1 class through explicit anomaly settings, loads the candidate into Candidate Review, and persists the active image as `Abnormal`.
- A no-download synthetic YOLOv8 normal/abnormal classification train smoke now produces `artifacts\yolov8-cls-training-smoke\normal-abnormal-fixture\runs\yolov8n-cls-normal-abnormal-smoke-e10\weights\best.pt`.
- That trained fixture `best.pt` loads through the local adapter, preserves class names `abnormal`/`normal`, returns `abnormal` for `test\abnormal\abnormal-0.png` and `normal` for `test\normal\normal-0.png`, and passes the WPF mapped-inference smoke when pointed at the abnormal test image.
- `AnomalyClassificationEvaluationService` now blocks adoption unless held-out normal/abnormal evidence has at least 10 total images, at least 5 per class, overall accuracy >= 0.9, per-class accuracy >= 0.8, and correct predictions meet the configured minimum confidence.
- `scripts\evaluate-yolo-classification.ps1` now runs the local YOLOv8 adapter over a held-out normal/abnormal split and writes `classification-evaluation-summary.json`; the current synthetic fixture remains `hold` with `4/10` images, `2/5` per class, `0.75/0.9` accuracy, and `0.5/0.8` abnormal accuracy at the default confidence gate.
- Minimum-confidence recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-minconf08\classification-evaluation-20260707-232709\classification-evaluation-summary.json`; with `-MinimumConfidence 0.8`, the same fixture remains `hold` with `lowConfidenceClassMatchCount=2`, confidence-gated accuracy `0.25`, normal accuracy `0.5`, and abnormal accuracy `0`.
- `AnomalyClassificationEvaluationService` can read `classification-evaluation-summary.json` back into a report, and `WpfAnomalyClassificationEvaluationPresentationService` converts that report into Korean recommendation/metrics/detail/action text.
- Parsed anomaly summaries fail closed: only explicit `promotion.recommendation=adopt` with no hold reasons is adoptable; empty, missing-promotion, or explicit-hold summaries remain non-adoptable.
- Model Center now exposes that anomaly evaluation presentation through a visible conditional `YoloAnomalyEvaluationPanel`. For anomaly-detection datasets it also auto-loads a bounded summary from the active output root when `classification-evaluation-summary.json` exists at the root, direct `classification-evaluation\` child, or newest immediate `classification-evaluation-*` timestamp folder. The anomaly evaluation card keeps recommendation/metrics visible and defaults blocker detail/next action behind collapsed `YoloAnomalyEvaluationDetailExpander`. Current-source evidence: baseline/no-summary capture `artifacts\ui\wpf-model-center-anomaly-evaluation-baseline-no-summary-1920.png`; summary-loaded after capture `artifacts\ui\wpf-model-center-anomaly-evaluation-after-1920.png`; compact latest capture `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`. The production app still needs a deliberate command or workflow step to generate/select a real anomaly evaluation summary.
- Latest recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-rerun\classification-evaluation-20260707-231654\classification-evaluation-summary.json`.
- Latest 2026-07-08 runtime/evaluation recheck: adapter compile/self-test, `--wpf-yolov8-anomaly-classification-runtime-smoke`, evaluation script, `--anomaly-classification-evaluation`, and Model Center visual smoke passed. Summary `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-recheck\classification-evaluation-20260708-004229\classification-evaluation-summary.json` remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy. Capture `artifacts\ui\wpf-model-center-anomaly-evaluation-recheck-20260708-1920.png`.
- Latest late evaluation script recheck: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-late-recheck\classification-evaluation-20260708-011412\classification-evaluation-summary.json` remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches. Latest Model Center captures: `artifacts\ui\wpf-model-center-anomaly-evaluation-late-recheck-20260708-1920.png`, `artifacts\ui\wpf-model-center-anomaly-evaluation-timestamp-lookup-after-20260708-1920.png`, and `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`.
- Latest explicit-adopt guard recheck: `--wpf-yolov8-anomaly-classification-runtime-smoke` passed, and `scripts\evaluate-yolo-classification.ps1 -MinimumConfidence 0.8` wrote `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-explicit-adopt-guard-recheck\classification-evaluation-20260708-014426\classification-evaluation-summary.json`; it remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches.

Missing: real operator normal/abnormal training data and held-out inference evidence that passes the adoption guard. Do not treat ImageNet pretrained `yolov8n-cls.pt` output or the tiny synthetic fixture `best.pt` as production anomaly accuracy.

### YOLO11 Runtime

- Current bundled Ultralytics runtime observed earlier lacked YOLO11 `C3k2`, so YOLO11 is correctly runtime-blocked.
- Worker capability reporting gates YOLO11 instead of always advertising it.
- Do not claim YOLO11 readiness until the selected runtime and local weights prove it.

### Segmentation Labeling UX

The following areas were stabilized and should not be casually rewritten:

- SEG purpose selects brush-first tooling instead of starting with rectangle drawing.
- Brush/eraser MouseUp hitch was reduced by deferring CPU/Object Review work.
- Brush materialization creates a label row after the quiet idle window without reintroducing MouseUp stall.
- Undo/redo for brush strokes was directly EXE-smoked with Ctrl+Z and Ctrl+Shift+Z.
- Live brush preview opacity now matches committed mask opacity.
- Existing brush mask class/color changes recolor the mask.
- Loading another image clears stale brush preview/mask state.
- Pending labels are committed before image navigation when required.
- Image queue click-load and keyboard/navigation UX were improved.

Treat these as protected contracts. If a future request touches them, run focused brush/eraser/image-switch/undo tests and use EXE evidence when the user asks for direct executable verification.

### Layout And Operator UX

- Top workflow subnavigation was added for stage-specific shortcuts.
- Image queue layout was compacted to give more vertical room to the list.
- Image queue thumbnails/preview support and keyboard navigation were added.
- Guide/onboarding image-queue guidance now avoids fixed left-side location wording so it still fits the current right-docked/rail queue layout.
- Left panels were compacted by hiding repeated explanatory text while keeping operator-critical actions visible:
  - AI candidate review
  - saved-label/object review
  - guide/tools
  - class setup
  - training settings
  - model center
- The guide/tools panel now has a labeling-stage current-task mode: dataset setup/purpose/storage structure stays in onboarding, while YOLO/tutorial/tool details sit behind a collapsed `필요할 때만: 학습·검사 세부` expander. The current-task card distinguishes real annotation tools from workflow modes, so non-labeling actions such as current inspection or AI review should not show a stale brush/box tool label.
- In the labeling stage, the guide/tools shortcut is now presented to operators as `작업` / `현재 작업` with a clipboard/current-work icon, so the first read is the current action, not a broad tool/manual area.
- The labeling-stage current-task guide keeps secondary training/inspection details behind the collapsed `필요할 때만: 학습·검사 세부` expander.
- The guide/tools current-task flow hint is now bound to `WpfLearningWorkflowPanelViewModel` and follows the live state: sample/default shows `흐름: 이미지 > 열기 > 라벨`, labeling shows `흐름: 그리기 > 저장 > 다음`, inference shows `흐름: 검사 > 확인 > 검토`, and AI candidate review shows `흐름: 확인 > 확정 > 스킵`.
- The current-task explanatory sentence should use primary text instead of the global red accent, so normal guidance does not read as an error state.
- The first task card caption should read `현재 이미지` under the expanded `현재 작업` panel title.
- The current-image task card border should stay neutral; the red accent belongs on the step badge and actual action controls.
- The image-queue current-task card now uses generic label guidance for unlabeled images, so the shared queue panel does not tell segmentation brush/polygon users to draw a box.
- Visual-smoke evidence for that exact `2 라벨링` guide/tools state should use `--review-tab labeling-guide`; the older `--review-tab guide` route remains dataset-onboarding evidence.
- Dataset setup completion and the top `2 라벨링` workflow button now land on a compact beginner start view: labeling mode is active, saved-label review is selected, and the side workflow panel is collapsed to the rail instead of opening class management first.
- Public tutorial screenshots were refreshed from current 1920x1080 visual-smoke captures after the related UI changes.
  The current beginner-start pass updated tutorial wording but kept the expanded-panel task screenshots because they still document the drawing workflow, not the default entry state.

### Model Review And Adoption Safety

- Model comparison examples can parse YOLO segmentation polygon label rows.
- Model-comparison promotion summaries preserve multiple hold reasons when weak evidence and zero UI-threshold candidates overlap.
- Model-comparison summaries include threshold-sweep counts; the current userseed candidate has `0.25=0`, `0.10=0`, `0.05=3`, `0.01=259` and remains on hold.
- Segmentation promotion also requires enough positive held-out mask evidence; the current userseed candidate has only `3/5` positive segment labels and remains on hold.
- Segmentation promotion also requires enough distinct positive held-out images; the current userseed candidate has only `3/5` positive segment images and remains on hold.
- Candidate Review now translates promote reasons into operator-facing Korean and avoids treating malformed long SEG rows as bbox rows; the actual fine-tune comparison display shows `기존 모델 0개, 새 모델 2개` and `교체 추천` from `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260707\20260707-222900`.
- Model Center actual-candidate save smoke verifies the fine-tuned YOLOv8 SEG `best.pt` can be selected from the matching `runs\segment` metadata, saved to recipe, and recorded as the current inspection model.
- Real YOLO current-image TCP smoke verifies that the saved fine-tune model path can produce and confirm a polygon candidate into segment JSON/mask PNG for `025_NG`.
- Rejected model-history candidates remain visible but are not directly promotable.
- Candidate save/reject/rejected-state text is owned by `WpfModelCandidateDecisionPresentationService`.
- Candidate Review model-validation detail remains collapsed by default for compactness.

### Dataset Interoperability

Object detection and segmentation import/export coverage is broad enough for the current local workstation MVP:

- COCO, Pascal VOC, Label Studio, and CVAT detection slices.
- COCO, Label Studio, and CVAT segmentation slices.
- CVAT segmentation import skips invalid polygons and writes local segment/mask artifacts.

Current priority is not more interoperability unless the user asks. The next likely interop slice would be Labelbox NDJSON detection import.

### Documentation, CI, And Library Dependency

- `README.md` now includes 1-minute summary, install/run, sample data, build command, smoke command, CI, release notes, roadmap, and known limitations.
- `RELEASE_NOTES.md` exists.
- `.github/workflows/ci.yml` builds the focused test project, runs `--priority-workflow-docs`, and runs `git diff --check`.
- `Library-Noah` is no longer referenced as a sibling source project.
- The app now references and force-copies checked-in DLLs into the EXE output:
  - `dll\Lib.Common.dll`
  - `dll\Lib.OpenCV.dll`
- The app restores OpenCvSharp support through NuGet (`OpenCvSharp4` 4.4.0.20200915, `OpenCvSharp4.runtime.win` 4.5.5.20211231, and legacy support assemblies) and force-copies the restored DLLs into the EXE output, with existing local `packages` files used only as a fallback when present.
- The focused docs smoke asserts the README/release/CI sections and the bundled DLL dependency contract.

Latest verified commands for the DLL/CI/docs slice:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.sln -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\sln-isolated-out\
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
git diff --check
```

## Known Remaining Gaps

- YOLOv8 SEG now has a first promotable circular-defect candidate by the local held-out comparison gate plus focused WPF save/adoption and current-image TCP smoke evidence, but broad production accuracy is not established.
- Need the next operator loop: collect more varied train/valid/test labels for broader accuracy, or switch to anomaly classification real operator-data evaluation.
- Anomaly classification has local YOLOv8 pretrained-seed runtime, WPF mapped-inference smoke, a synthetic trained-fixture `best.pt` smoke, and a core adoption evaluation guard, but still needs real normal/abnormal operator-data training and held-out inference evidence.
- Anomaly classification also still needs an app-level command/workflow for generating or selecting the held-out evaluation summary; Model Center can display it once the summary is in the active output-root direct, fixed child, or timestamped evaluation summary location.
- YOLO11 remains blocked until a compatible local runtime and weights are verified.
- CI workflow has been added and pushed, but the GitHub Actions run result was not inspected in this handoff.
- Updating `dll\Lib.Common.dll` or `dll\Lib.OpenCV.dll` now requires intentional binary refresh, EXE output-copy verification, and build verification.
- Any new visible UI change needs fresh current-build capture evidence and tutorial/README image review.

## Recommended Next Work

1. Re-orient with `git status --short`, current docs, and current diff.
2. If the user asks to continue UI/UX, keep using the commercial-tool pattern: current task and primary controls first, secondary help/details behind explicit expanders, no all-in-one scrolling guide panels.
3. Then continue YOLOv8 SEG operating quality: use the promotable fine-tune/adoption/current-image evidence as the circular-defect baseline, collect more varied real SEG labels, and rerun held-out comparison when data changes.
4. Then do anomaly classification real operator-data training/evaluation when enough reviewed normal/abnormal images are available.
5. Only after runtime/model workflow is stable, return to lower-priority interoperability or documentation polish.

## Useful Verification Commands

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-review-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-heldout
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-polygon-training-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-readiness-purpose
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose anomaly --review-tab yolo --right-workflow-expanded --anomaly-classification-evaluation-summary .\artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-minconf08\classification-evaluation-20260707-232709\classification-evaluation-summary.json --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-anomaly-evaluation-after-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolov8-anomaly-classification-runtime-smoke
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\evaluate-yolo-classification.ps1 -Weights <normal-abnormal-best.pt> -DatasetRoot <classification-dataset-root> -Split test -ImageSize 64 -Confidence 0.0
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
git diff --check
```

Final reports should include changed files, verification commands/results, screenshot need, remaining risks, and the next priority.
