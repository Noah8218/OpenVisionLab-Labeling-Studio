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

Current checkpoint:

- Branch expected at handoff: `main`
- Latest pushed commit at handoff: `84666293 build: use bundled noah library dlls`
- Worktree is expected to be dirty with local YOLOv8 SEG promotion-guard, anomaly classification runtime/evaluation, test, and docs changes unless the previous thread committed them.
- Always trust `git status --short` and the current diff over this prompt.

Rules:

- Do not revert existing changes unless explicitly requested.
- Do not run `git push` unless the user explicitly asks for `push`.
- A `commit` request means local commit only unless `push` is also requested.
- Preserve MVVM boundaries. View code-behind should stay a UI adapter; command/state/workflow/presentation logic should live in ViewModel or Service classes where feasible.
- Do not touch Viewer/OpenGL/ROI/brush/eraser hot paths unless the task requires it and focused gates are included.
- Do not run model downloads, `pip install --upgrade`, dependency upgrades, or package upgrades without explicit user approval.
- Do not guess. Open files, tests, logs, and current docs when unsure.
- Completion requires build/focused tests/`git diff --check` evidence.
- Keep changes narrow.
- If no UI layout/visual styling changed, state that no screenshot is needed.
- If UI changes, capture current 1920x1080 evidence and check README/tutorial screenshots.

Product direction:

- OpenVisionLab Labeling Studio is a local Windows workstation for industrial image labeling and model workflow.
- The user does not want cloud/team collaboration scope right now.
- Current user override is UI/UX first: simplify the left workflow panels and make beginner operation feel closer to commercial labeling tools.
- Next technical priority after the UI/UX pass is YOLOv8 segmentation model-quality work.
- Next priority after YOLOv8 SEG is anomaly classification real operator-data runtime/model smoke.
- YOLO11 remains runtime-gated until the selected local Ultralytics runtime and weights actually support it.
- Dataset interoperability is mostly covered and is not the current priority unless the user redirects.

YOLOv8 local-source agreement:

- YOLOv8 should follow the YOLOv5-style local source workflow.
- Use `C:\Git\yolov8` as the local worker folder.
- It should use local `ultralytics/ultralytics` source under `ultralyticsMaster`, local `.venv`, editable install, and `labeling_tcp_client.py`.
- ONNX is inference-only support and does not replace local Ultralytics source for training.
- `yolov8n-seg.pt` was downloaded earlier with explicit approval and is only a pretrained seed.
- `yolov8n-cls.pt` was downloaded with explicit approval on 2026-07-07 and is only a pretrained seed for classification runtime smoke.

Latest stable areas to avoid reworking:

- SEG brush-first tooling, brush/eraser performance, undo/redo, preview opacity, image-switch preview clearing, class recolor, pending-save navigation, image queue click/keyboard UX.
- YOLOv8 local worker training/inference plumbing and polygon preservation.
- Candidate Review polygon save -> segment JSON/mask PNG -> YOLO segment label export.
- EXE circular SEG operating path after DLL output fix: `circular_seg_exe_20260707_221147` passed `--label-count 20` with 10 train, 5 valid, and 5 test segment masks plus 20 OK/background labels, then completed YOLOv8 inference. Treat this as workflow/data-evidence coverage, not production accuracy.
- Compact left-panel and image-queue layout passes.
- Compact beginner labeling start view after dataset setup and top `2 라벨링`; class/tools panels remain available from the rail instead of opening first.
- Compact guide/tools task-panel mode: labeling-stage guide now shows a current-task card first and keeps dataset setup/YOLO/tutorial/tool details behind onboarding or a collapsed details expander. The current-task card should distinguish real annotation tools from workflow modes, so current inspection or AI review does not show a stale brush/box tool label.
- Labeling-stage guide naming: the guide/tools shortcut should read `작업`, use a clipboard/current-work icon, and the expanded panel title should read `현재 작업`. Keep this current-action wording unless a later UX pass replaces the whole navigation model. Latest before/after captures: `artifacts\ui\wpf-labeling-current-work-guide-rename-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-work-guide-rename-after-20260708-1920.png`, and `artifacts\ui\wpf-labeling-current-work-icon-after-20260708-1920.png`; public tutorial 03 source/annotated PNGs and the standalone embedded image should show `작업`, not `도구`.
- Labeling-stage guide secondary details: the collapsed optional section should read `필요할 때만: 학습·검사 세부` and stay collapsed by default, so the first read remains the current task rather than another broad help/tool bucket. Latest captures: `artifacts\ui\wpf-labeling-details-expander-header-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-details-expander-header-after-20260708-1920.png`.
- Labeling-stage current-task flow hint: the current-task sequence should render as one quiet line such as `흐름: 확인 > 확정 > 스킵`, not as three bordered boxes that look like extra action buttons. Latest captures: `artifacts\ui\wpf-labeling-current-task-flow-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-flow-after-20260708-1920.png`.
- Labeling-stage current-task action emphasis: keep the step badge/action buttons accented, but the current-task explanatory sentence should use primary text rather than the global red accent. Latest captures: `artifacts\ui\wpf-labeling-current-task-action-emphasis-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-action-emphasis-after-20260708-1920.png`.
- Labeling-stage current-task card caption: the expanded panel title stays `현재 작업`, while the first task card caption should read `현재 이미지` to avoid repeating the same title. Latest captures: `artifacts\ui\wpf-labeling-current-task-card-caption-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-card-caption-after-20260708-1920.png`.
- Labeling-stage current-task card border: keep the card border neutral and reserve the red accent for the step badge and actual action controls. Latest captures: `artifacts\ui\wpf-labeling-current-task-card-border-before-20260708-1920.png`, `artifacts\ui\wpf-labeling-current-task-card-border-after-20260708-1920.png`.
- Image-queue current-task generic label guidance: unlabeled images should say to make a label and save it, not to draw a box, because the same queue card is used for box, polygon, and brush workflows.
- Canvas/guide workflow image-queue guidance: visible startup/sample/saved-label actions and guide/onboarding steps should say `image queue`, not `left image queue` or `left-side image queue`; latest captures `artifacts\ui\wpf-startup-image-queue-neutral-after-1920.png` and `artifacts\ui\wpf-startup-image-queue-neutral-guide-after-1920.png`.
- No-object completion tooltip should say no label/empty YOLO label file, not no box, so it fits segmentation brush/polygon work.
- AI-candidate current-task guidance: when pending AI candidates exist, guide/canvas current-task should say candidate review/confirm/skip, not run inspection again. The compact guide flow hint is ViewModel-owned and must switch by state: sample/default `이미지/열기/라벨`, labeling `그리기/저장/다음`, inference `검사/확인/검토`, AI review `확인/확정/스킵`; latest capture `artifacts\ui\wpf-labeling-current-task-flow-after-20260708-1920.png`.
- Right-workflow mode alignment: Candidate Review should enter inference mode, and Saved Labels should enter labeling mode, so current-task text does not keep stale run-inspection guidance after confirmed/saved labels.
- Use `--review-tab labeling-guide` for current `2 라벨링` guide/tools visual-smoke evidence; keep `--review-tab guide` for dataset-onboarding evidence.
- Model comparison promotion guards, including multi-reason hold summaries for weak evidence plus zero UI-threshold candidates.
- Model comparison threshold-sweep reporting; current userseed candidate remains hold with `0.25=0`, `0.10=0`, `0.05=3`, `0.01=259`.
- Model comparison positive SEG evidence guard; current userseed candidate remains hold with positive mask evidence `3/5`.
- Model comparison positive SEG image guard; current userseed candidate remains hold with positive mask images `3/5`.
- New 20-label circular SEG held-out comparison closed quantity blockers (`11/10` labels, `5/5` positive labels, `5/5` positive images), but the userseed candidate still remains `hold` with precision `0.098 < 0.1` and UI-threshold candidates `0` at confidence `0.25`; artifact `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-userseed-20260707\20260707-222120`.
- Fine-tuned YOLOv8 SEG 20-label candidate `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt` is the first circular-defect candidate to pass the comparison gate: artifact `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260707\20260707-222900`, precision `1.0`, recall `0.589`, mAP50 `0.767`, mAP50-95 `0.257`, UI candidates `2` at confidence `0.25`, `promotion.recommendation=promote`. Review examples before saving it as the inspection model.
- Candidate Review display for that promote summary is covered by `--wpf-visual-smoke --model-comparison-summary ...` and capture `artifacts\ui\wpf-model-comparison-finetune-promote-after-1920.png`; the display count must remain aligned with the summary (`기존 모델 0개`, `새 모델 2개`) and long SEG rows must not fall back to bbox confidence parsing.
- Direct local adapter smoke for the same fine-tuned weight is stored under `artifacts\yolo-smoke\finetune80-20260707`: `025_NG` and `024_NG` each return one `NG` polygon candidate at confidence `0.25`, while `011_OK` returns zero candidates.
- Focused WPF Model Center adoption smoke for the same fine-tuned weight is covered by `--wpf-model-center-real-candidate-save`; capture `artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png`. It verifies the actual `C:\Git\yolov8` matching `runs\segment` candidate is staged, saved to recipe, and recorded as the current inspection model.
- Real YOLO current-image TCP smoke for the same fine-tuned weight is stored under `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260707`: `025_NG` returns one `NG` polygon candidate at confidence `0.7682`, then confirmation saves label text, segment JSON, and mask PNG.
- Latest real YOLO current-image TCP recheck for the same fine-tuned weight is stored under `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260708-recheck`: `025_NG` again returns one `NG` polygon candidate at confidence `0.7682`, then confirmation saves label text, segment JSON, and mask PNG. Latest Model Center save/adoption capture: `artifacts\ui\wpf-model-center-real-finetune-save-recheck-20260708-1920.png`.
- Local YOLOv8 classification adapter smoke; `C:\Git\yolov8\labeling_tcp_client.py` now converts `result.probs` into an `imageClassification` candidate and `yolov8n-cls.pt` loads successfully through `--smoke-test`.
- WPF YOLOv8 anomaly classification runtime smoke; `--wpf-yolov8-anomaly-classification-runtime-smoke` calls the local adapter through `RunDetectionForImageAsync`, maps the returned top1 class through explicit anomaly settings, and persists the active image as `Abnormal`.
- YOLOv8 anomaly classification trained-fixture smoke; a no-download synthetic normal/abnormal dataset trained `artifacts\yolov8-cls-training-smoke\normal-abnormal-fixture\runs\yolov8n-cls-normal-abnormal-smoke-e10\weights\best.pt`, and that weight returns `abnormal`/`normal` class candidates through the local adapter plus WPF mapped-inference smoke.
- Anomaly classification evaluation guard; `AnomalyClassificationEvaluationService` blocks adoption below 10 held-out images, below 5 normal/5 abnormal images, below 0.9 overall accuracy, below 0.8 per-class accuracy, or when correct predictions fall below the configured minimum confidence.
- Anomaly classification evaluation runner; `scripts\evaluate-yolo-classification.ps1` runs the local YOLOv8 adapter over a held-out normal/abnormal split and writes `classification-evaluation-summary.json`. The current synthetic fixture remains `hold` with `4/10` images, `2/5` per class, `0.75/0.9` accuracy, and `0.5/0.8` abnormal accuracy at the default confidence gate. Latest default recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-rerun\classification-evaluation-20260707-231654\classification-evaluation-summary.json`. Latest `-MinimumConfidence 0.8` recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-minconf08\classification-evaluation-20260707-232709\classification-evaluation-summary.json`, which remains `hold` with `lowConfidenceClassMatchCount=2`, confidence-gated accuracy `0.25`, normal accuracy `0.5`, and abnormal accuracy `0`.
- Parsed anomaly summaries fail closed: only explicit `promotion.recommendation=adopt` with no hold reasons is adoptable; empty, missing-promotion, or explicit-hold summaries remain non-adoptable.
- Anomaly classification evaluation presentation; `AnomalyClassificationEvaluationService` can read `classification-evaluation-summary.json` back into a report, and `WpfAnomalyClassificationEvaluationPresentationService` converts reports into Korean recommendation/metrics/detail/action text. Model Center now exposes that presentation through a conditional `YoloAnomalyEvaluationPanel` and auto-loads a bounded summary from the active output root for anomaly-detection datasets, including the script's newest immediate `classification-evaluation-*` timestamp folder. The anomaly evaluation card keeps recommendation/metrics visible and defaults blocker detail/next action behind collapsed `YoloAnomalyEvaluationDetailExpander`; current-source captures are `artifacts\ui\wpf-model-center-anomaly-evaluation-baseline-no-summary-1920.png`, `artifacts\ui\wpf-model-center-anomaly-evaluation-after-1920.png`, and `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`. The production app still needs a deliberate command or workflow step to generate/select a real anomaly evaluation summary.
- Latest anomaly runtime/evaluation recheck wrote `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-recheck\classification-evaluation-20260708-004229\classification-evaluation-summary.json` and capture `artifacts\ui\wpf-model-center-anomaly-evaluation-recheck-20260708-1920.png`; runtime path passes, but the synthetic fixture remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches.
- Latest late anomaly evaluation script recheck wrote `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-late-recheck\classification-evaluation-20260708-011412\classification-evaluation-summary.json`; it remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches. Latest Model Center captures: `artifacts\ui\wpf-model-center-anomaly-evaluation-late-recheck-20260708-1920.png`, `artifacts\ui\wpf-model-center-anomaly-evaluation-timestamp-lookup-after-20260708-1920.png`, and `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`.
- Latest explicit-adopt guard recheck: `--wpf-yolov8-anomaly-classification-runtime-smoke` passed, and `scripts\evaluate-yolo-classification.ps1 -MinimumConfidence 0.8` wrote `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-explicit-adopt-guard-recheck\classification-evaluation-20260708-014426\classification-evaluation-summary.json`; it remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches.
- Latest focused regression sweep after the guide UX pass: `--wpf-model-comparison-heldout`, `--wpf-model-comparison-run-service`, `--wpf-candidate-polygon-training-flow`, `--wpf-segmentation-object-verification`, `--dataset-readiness-purpose`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-model-comparison-review-service`, `--anomaly-classification-evaluation`, `--wpf-yolov8-anomaly-classification-runtime-smoke`, YOLOv8 adapter compile/self-test, PowerShell parser checks for the YOLO comparison/evaluation scripts, solution build, output-copy check for checked-in Noah/OpenCvSharp DLL dependencies, and `--wpf-responsive-layout --width 1920 --height 1080` passed on 2026-07-08 KST.
- Model candidate rejected-history guard and candidate decision presentation text.
- README/release/CI skeleton, checked-in `dll\Lib.Common.dll` / `dll\Lib.OpenCV.dll` dependency/output-copy contract, and NuGet-restored OpenCvSharp support DLL output-copy contract with local `packages` fallback.

Known remaining gaps:

- YOLOv8 SEG has a first promotable circular-defect candidate by the current local held-out comparison gate plus focused WPF save/adoption and current-image TCP smoke evidence, but broad production accuracy is not established. Need more varied operator data before claiming deployment readiness beyond this fixture.
- Anomaly classification has local YOLOv8 pretrained-seed runtime, WPF mapped-inference smoke, a synthetic trained-fixture `best.pt` smoke, and a core adoption evaluation guard, but still needs real operator normal/abnormal training data and held-out inference evidence. Do not treat ImageNet `yolov8n-cls.pt` output or the tiny synthetic fixture `best.pt` as production anomaly accuracy.
- YOLO11 is still blocked until compatible runtime and weights are verified.
- GitHub Actions run result after the latest push has not been inspected in this handoff.
- Updating bundled `Lib.Common.dll` or `Lib.OpenCV.dll` requires intentional binary refresh, EXE output-copy verification, and build verification.

Recommended next work:

1. Re-orient from current status/diff/docs.
2. Continue the UI/UX review with the same commercial-tool pattern: stage-specific current-task panels first, secondary help/details behind explicit expanders, no all-in-one scrolling guide panels.
3. Then return to YOLOv8 SEG operating quality: continue broader real saved SEG data and held-out model quality work.
4. Then add anomaly classification real operator-data training/evaluation when enough reviewed normal/abnormal images are available.

Useful verification commands:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-polygon-training-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-center-real-candidate-save --yolo-root C:\Git\yolov8 --data-yaml .\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data.yaml --baseline-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt --candidate-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png
$env:LABELING_SMOKE_PROJECT_ROOT='C:\Git\yolov8'; $env:LABELING_SMOKE_CLIENT_SCRIPT='C:\Git\yolov8\labeling_tcp_client.py'; $env:LABELING_SMOKE_MODEL_ENGINE='YOLOv8'; $env:LABELING_SMOKE_MODEL_ROOT='C:\Git\yolov8\ultralyticsMaster'; $env:LABELING_SMOKE_PYTHON_EXE='C:\Git\yolov8\.venv\Scripts\python.exe'; $env:LABELING_SMOKE_WEIGHTS='C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt'; $env:LABELING_SMOKE_IMAGE_ROOT='C:\Git\Labelling_Application\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data\test\images'; $env:LABELING_SMOKE_IMAGE_PATH='C:\Git\Labelling_Application\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data\test\images\025_NG.png'; $env:LABELING_SMOKE_IMAGE_SIZE='160'; $env:LABELING_SMOKE_CONFIDENCE='0.25'; $env:LABELING_SMOKE_IOU='0.7'; $env:LABELING_SMOKE_EXPECT_SEGMENTATION='true'; $env:LABELING_SMOKE_ARTIFACT_ROOT='C:\Git\Labelling_Application\artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260707'; dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-yolo-smoke
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

Final report should include changed files, verification commands/results, screenshot need, remaining risk/unverified items, and next work.
```
