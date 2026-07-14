# Next Thread Handoff

Last updated: 2026-07-13 KST

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
- Latest pushed commit at handoff: `af643f7d feat: harden labeling QA and model adoption`
- GitHub Actions run `29154340127` passed for `af643f7d`.
- Expected worktree at handoff creation: dirty local continuation work for class-specific YOLOv8 SEG comparison evidence, its WPF request path, focused tests, and tracking docs. Trust `git status --short` and the current diff.
- Do not run `git push` unless the user explicitly asks for `push`.
- A request to `commit` means local commit only unless `push` is also explicitly requested.

## Product Direction

OpenVisionLab Labeling Studio is a local Windows workstation for industrial image labeling and model workflow. The current product shape is local-first, not cloud/team collaboration.

The 2026-07-13 commercial reassessment rates the focused single-operator workflow at about `4.0/5` (80%), a general commercial image-labeling suite comparison at about `3.1/5` (62%), and enterprise/team-platform breadth at about `1.2/5` (24%). These are directional workflow-maturity estimates, not model-accuracy percentages. The authoritative strengths, gaps, and completion disposition are in `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`.

Current priority order from the user conversation:

1. Keep the completed beginner/current-work and optional AI Candidate Review empty-state hierarchy stable unless a specific UX defect is reproduced.
2. Produce a read-only/dry-run remediation report for the 124 historical non-source SEG targets. Report backup path and per-image old/new geometry, point, mask-pixel, and YOLO-label differences; do not rewrite operator files without explicit approval.
3. After approval, correct and visually audit representative labels, regenerate YOLO labels, retrain YOLOv8 SEG, and rerun the unchanged class-specific held-out comparison at the intended UI confidence.
4. Add independently acquired production-camera or cross-session YOLOv8 SEG evidence, especially circular normal backgrounds and currently missed circular defects.
5. Extend the completed circular-defect YOLOv8 anomaly/classification smoke with independent production-camera/cross-session held-out evidence.
6. YOLO11 only after the local Ultralytics runtime and weights actually support it. Dataset interoperability is not the current priority.

Latest YOLOv8 engine/worker correction:

- The current SEG recipe was already persisted as YOLOv8 with `openvisionlab-yolov8-seg-current-recipe-20260707\weights\best.pt`; the visible `yolov5x` values were stale legacy training presentation, not the actual training packet engine.
- Training settings now show the selected engine/task/start weight (`YOLOv8 SEG / yolov8n-seg.pt`) and keep YOLOv5-only selectors editable only for YOLOv5.
- Connection readiness now verifies a request-correlated model status engine/weight before training or inference. A single active TCP worker is retained, and WPF/process shutdown stops the managed worker.
- The two observed orphan processes using the older `openvisionlab-yolov8-segment\weights\best.pt` were removed.
- Real current-weight evidence: `artifacts\real-yolo-smoke\20260713-current-seg-worker-identity\summary.txt`. It proves YOLOv8 SEG polygon transport and artifact saving at diagnostic confidence `0.001`; it does not prove adoption quality at UI confidence.
- UI evidence: `artifacts\ui\20260713-yolo-engine-worker-guard\before-yolov8-seg-training-settings-1920.png` and `after-engine-aware-seg-training-settings-1920.png`.
- Tutorial images 09/10 remain valid YOLOv5 examples and were not replaced.

Latest Candidate Review UX evidence:

- Stage 3 is explicitly `AI 후보(선택)`. With no pending candidates, the panel explains that manual-label-only work can move to `4 학습/모델`, preserves saved labels, and hides empty candidate/model diagnostic controls.
- Pending candidates hidden only by confidence keep the confidence control and recovery guidance. A real model-comparison report still shows the model-quality dashboard.
- Valid 1920x1080 before/after evidence: `artifacts\ui\20260712-candidate-review-empty-ux\before-candidate-review-empty-1920.png`, `artifacts\ui\20260712-candidate-review-empty-ux\after-candidate-review-empty-final2-retry-1920.png`.
- Public tutorial 12 source/annotated images, tutorial wording, and standalone embedded image were refreshed from the current build.

Latest SEG inference contour evidence:

- The local YOLOv8 adapter already returns `result.masks.xy` as polygon points. WPF candidate adaptation and both WPF/legacy overlay models now preserve that geometry.
- SEG candidates render as closed, unfilled contours with class/confidence badges and no bounding rectangle; object-detection candidates still use boxes.
- Real trained-model TCP evidence: `artifacts\real-yolo-smoke\seg-contour-render-20260713-verified\summary.txt` (`candidateCount=1`, `polygonCandidateCount=1`, segment/mask saved).
- Current-source 1920x1080 evidence: `artifacts\ui\20260713-seg-inference-contour\before-seg-inference-box-1920.png` and `artifacts\ui\20260713-seg-inference-contour\after-seg-inference-contour-full-1920.png`.
- Public tutorial image 12 and standalone embedded image now show contours instead of stale SEG boxes.

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
- Latest held-out comparison recheck for that same fine-tuned weight wrote `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260708-latest\20260708-185046\comparison-summary.json`; it matches the prior promote result with precision `1.0`, recall `0.589`, mAP50 `0.767`, mAP50-95 `0.257`, baseline UI candidates `0`, candidate UI candidates `2`, and `promotion.recommendation=promote`.
- Latest direct adapter sweep over all 11 held-out test images at UI confidence `0.25` returned zero candidates for all 6 OK images, one `NG` polygon candidate for `024_NG.png` at confidence `0.5065`, one `NG` polygon candidate for `025_NG.png` at confidence `0.7682`, and zero candidates for `022_NG.png`, `023_NG.png`, and `032_NG.png`. This supports the promote decision but also confirms recall remains limited.
- Latest 40-label EXE circular SEG workflow wrote `artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260708_213227` with `trainSegments=20`, `validSegments=10`, `testSegments=10`, and `backgroundLabels=20`.
- A follow-up 80-epoch fine-tune wrote `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-finetune-80ep-img160-20260708\weights\best.pt`.
- `scripts\compare-yolo-models.ps1` was corrected for local Ultralytics YOLOv8 so metrics still come from `model.val`, but UI candidate counts and Candidate Review example labels come from a separate `model.predict` run. The corrected 40-label comparison wrote `artifacts\yolo-model-comparison\yolov8-seg-40label-baseline-vs-finetune80-20260708-predict-ui\20260708-215754\comparison-summary.json`: candidate precision `0.582`, recall `0.5`, mAP50 `0.683`, mAP50-95 `0.281`, UI candidates `2` at confidence `0.25`, and `promotion.recommendation=promote`.
- Direct adapter sweep for the same 40-label candidate wrote `artifacts\yolo-tcp-smoke\yolov8-seg-40label-finetune80-conf025-20260708\summary.json`: at confidence `0.25`, 2 of 10 positive test images produced candidates, 0 of 6 OK test images produced candidates. At confidence `0.10`, recall improved but all 6 OK images produced candidates, so do not lower the default threshold without review.
- The 2026-07-10 image-level operating gate supersedes the historical aggregate `promote` result above. The training run's Ultralytics `best.pt` remains `hold`: at `0.25` positive coverage is `2/10` with background candidates `0/6`, while at `0.10` coverage is `9/10` but background candidates are `6/6`.
- The same run's `last.pt` has an operating gap. It was packaged byte-for-byte as `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-operating-selected-conf020-20260711\weights\best.pt`, with source/hash/comparison metadata under `operating-selection`. SHA256 is `E659268ECD8BDCCE58E54E7C6A710421AD250BA1E18BC31820A79DE665B4D67F` for both source and package.
- Full comparison for the packaged path is `artifacts\yolo-model-comparison\yolov8-seg-40label-operating-selected-conf020-20260711\20260711-220539\comparison-summary.json`: confidence `0.20`, precision `0.7748`, recall `0.6891`, mAP50 `0.6900`, mAP50-95 `0.1988`, positive coverage `6/10`, background candidates `0/6`, and `promotion.recommendation=promote`.
- Adoption now couples comparison confidence to the current inspection confidence. Model Center blocks the package at `0.25` with no adoption-history record and saves it at validated confidence `0.20`; captures are `artifacts\ui\wpf-seg-operating-confidence-mismatch-after-20260711-1920.png` and `artifacts\ui\wpf-seg-operating-conf020-adopt-after-20260711-1920.png`.
- Real TCP current-image smoke at `0.20` returned one `NG` polygon candidate on `025_NG.png` at `0.6266`, confirmed it, and wrote label text, segment JSON, mask PNG, and review status under `artifacts\real-yolo-smoke\40label-operating-selected-conf020-current-image-025-ng-20260711`. Direct adapter smoke returned zero candidates on `018_OK.png`.
- The app-saved Teaching dataset has both `OK` outer polygons and `NG` defect polygons. `scripts\compare-yolo-models.ps1` now accepts `-SegmentationPositiveClassName`, filters answer/prediction/image-level evidence to the same class, and records the selected class in summary/report output. `WpfModelComparisonRunService` chooses `NG`, then `Defect`, then a sole segmentation class.
- The previous circular operating model covered only `1/14` Teaching positives at confidence `0.20`. A Teaching-only fine-tune passed Teaching (`5/5`, backgrounds `0/21`) but failed circular regression (`10/10`, backgrounds `1/6`, precision `0.0018`) and was rejected.
- The combined run is `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-circular40-teaching125-multidomain-e60-img160-20260711\weights\best.pt`. At confidence `0.20`, Teaching test passed with precision `0.9891`, recall `1.0`, mAP50 `0.9950`, coverage `5/5`, and backgrounds `0/21`; circular test remained `hold` because backgrounds were `2/6` despite mAP50 `0.7473`.
- At confidence `0.30`, the combined fixed test summary `artifacts\yolo-model-comparison\yolov8-seg-multidomain-best-combined-test-conf030-20260711\20260711-233546\comparison-summary.json` reports precision `0.6671`, recall `1.0`, mAP50 `0.8799`, mAP50-95 `0.4385`, coverage `11/15`, backgrounds `0/27`, and `review`. The previous operating model covered `4/15` at the same threshold.
- Direct adapter smoke returned one `NG` polygon for `circular__024_NG.png` at `0.4652` and zero candidates for `circular__018_OK.png` at confidence `0.30`. Real TCP confirmation artifacts are under `artifacts\real-yolo-smoke\multidomain-best-conf030-current-image-024-ng-20260711`.
- Model Center saved the multi-domain candidate only to a temporary smoke recipe at confidence `0.30`; capture `artifacts\ui\wpf-model-center-multidomain-best-review-conf030-20260711-1920.png`. The production/current user recipe was not changed.
- A train-only circular-background 4x sampling experiment removed background candidates but lowered combined mAP50 from `0.8799` to `0.8537` and mAP50-95 from `0.4385` to `0.3910`; it was rejected. The multi-domain `best.pt` remains a review candidate, not an automatic production adoption.
- Focused Model Center save/adoption smoke for the same 40-label candidate passed through `--wpf-model-center-real-candidate-save`, saved it as the current inspection model, and captured `artifacts\ui\wpf-model-center-real-40label-finetune-save-after-20260708-1920.png`.
- Candidate Review visual smoke for the same 40-label summary captured `artifacts\ui\wpf-model-comparison-40label-promote-after-20260709-1920.png`, showing `차이 2개 이미지 / 예시 2개`, existing model `0`, new model `2`, confidence `25%`, and Korean promote guidance.
- Real current-image TCP confirm/save smoke for the same 40-label candidate wrote `artifacts\real-yolo-smoke\40label-current-image-025-ng-20260709\summary.txt`: held-out `025_NG.png` returned one `NG` polygon candidate at confidence `0.3039`, confirmation saved YOLO label text, segment JSON, mask PNG, and review status `Confirmed`.
- Direct local adapter smoke for that fine-tuned weight wrote `artifacts\yolo-smoke\finetune80-20260707`: `025_NG` returned one `NG` polygon candidate at confidence `0.768`, `024_NG` returned one `NG` polygon candidate at confidence `0.507`, and `011_OK` returned zero candidates at confidence `0.25`.
- Focused WPF Model Center adoption smoke now stages and saves that same fine-tuned weight as the current inspection model using the actual `C:\Git\yolov8` run and circular SEG `data.yaml`; capture `artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png`, test recipe config under `tests\LabelingApplication.Tests\artifacts\isolated-out\RECIPE\real_model_center_smoke_*\VISION.xml`.
- Current-image TCP workflow smoke for the same fine-tuned weight wrote `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260707`: `025_NG` produced one `NG` polygon candidate at confidence `0.7682`, then confirmation saved YOLO label text, segment JSON, and mask PNG.
- Latest current-image TCP recheck for the same fine-tuned weight wrote `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260708-latest`: `025_NG` again produced one `NG` polygon candidate at confidence `0.7682`, and confirmation saved label text, segment JSON, and mask PNG. Latest Model Center save/adoption capture: `artifacts\ui\wpf-model-center-real-finetune-save-recheck-20260708-latest-1920.png`.

Do not overstate this as broad production-ready accuracy. Training/inference, direct adapter, temporary Model Center save, and current-image TCP paths work. The old circular operating model passes only its circular fixture, while the new multi-domain model is `review` at confidence `0.30`; both still need independently acquired product/camera/lighting/defect evidence.

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
- A non-tiny local circular-defect run now stages 100 unique source images (20 OK, 80 NG) into deterministic train/validation/test splits. The first unbalanced 15-epoch model stayed `hold` at 11/15 overall and 1/5 normal.
- A second 20-epoch run repeats only the 10 original normal training images to balance train sampling at 60 normal / 60 abnormal; validation and test remain unchanged with zero cross-split duplicate hashes. Its weight is `artifacts\yolov8-cls-training-smoke\circular-defect-real-20260711\runs\yolov8n-cls-circular-defect-balanced-e20-img128-20260711\weights\best.pt`. Oversampling is not additional evidence.
- Held-out evaluation at minimum confidence 0.8 returned `promotion.recommendation=adopt`, 15/15 correct, normal 5/5, abnormal 10/10, and zero low-confidence class matches. Summary: `artifacts\yolo-classification-evaluation\circular-defect-balanced-e20-minconf08-20260711\classification-evaluation-20260711-214135\classification-evaluation-summary.json`.
- The WPF runtime smoke accepts expected class/state/image-size environment values and passed both held-out `003_OK.png` -> `Normal` and `033_NG.png` -> `Abnormal`; its original default abnormal behavior also still passes.
- `AnomalyClassificationEvaluationService` now blocks adoption unless held-out normal/abnormal evidence has at least 10 total images, at least 5 per class, overall accuracy >= 0.9, per-class accuracy >= 0.8, and correct predictions meet the configured minimum confidence.
- `scripts\evaluate-yolo-classification.ps1` now runs the local YOLOv8 adapter over a held-out normal/abnormal split and writes `classification-evaluation-summary.json`; the current synthetic fixture remains `hold` with `4/10` images, `2/5` per class, `0.75/0.9` accuracy, and `0.5/0.8` abnormal accuracy at the default confidence gate.
- Minimum-confidence recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-minconf08\classification-evaluation-20260707-232709\classification-evaluation-summary.json`; with `-MinimumConfidence 0.8`, the same fixture remains `hold` with `lowConfidenceClassMatchCount=2`, confidence-gated accuracy `0.25`, normal accuracy `0.5`, and abnormal accuracy `0`.
- `AnomalyClassificationEvaluationService` can read `classification-evaluation-summary.json` back into a report, and `WpfAnomalyClassificationEvaluationPresentationService` converts that report into Korean recommendation/metrics/detail/action text.
- Parsed anomaly summaries fail closed: only explicit `promotion.recommendation=adopt` with no hold reasons is adoptable; empty, missing-promotion, or explicit-hold summaries remain non-adoptable.
- Model Center now exposes that anomaly evaluation presentation through a visible conditional `YoloAnomalyEvaluationPanel`. For anomaly-detection datasets it also auto-loads a bounded summary from the active output root when `classification-evaluation-summary.json` exists at the root, direct `classification-evaluation\` child, or newest immediate `classification-evaluation-*` timestamp folder. The anomaly evaluation card keeps recommendation/metrics visible and defaults blocker detail/next action behind collapsed `YoloAnomalyEvaluationDetailExpander`. Current-source evidence: baseline/no-summary capture `artifacts\ui\wpf-model-center-anomaly-evaluation-baseline-no-summary-1920.png`; summary-loaded after capture `artifacts\ui\wpf-model-center-anomaly-evaluation-after-1920.png`; compact latest capture `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`.
- Model Center now also has explicit anomaly `평가 실행` and `평가 불러오기` actions. `평가 실행` exports a fresh classification-evaluation input split from reviewed normal/abnormal images and runs `scripts\evaluate-yolo-classification.ps1` through the local YOLOv8 adapter; `평가 불러오기` selects an existing `classification-evaluation-summary.json`. Latest capture `artifacts\ui\wpf-model-center-anomaly-evaluation-run-button-after-20260708-1920.png`. This adds the in-app generation/run path, but real operator normal/abnormal held-out data is still required before any accuracy/adoption claim.
- Historical synthetic recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-rerun\classification-evaluation-20260707-231654\classification-evaluation-summary.json`.
- Historical 2026-07-08 synthetic runtime/evaluation recheck: adapter compile/self-test, `--wpf-yolov8-anomaly-classification-runtime-smoke`, evaluation script, `--anomaly-classification-evaluation`, and Model Center visual smoke passed. Summary `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-recheck\classification-evaluation-20260708-004229\classification-evaluation-summary.json` remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy. Capture `artifacts\ui\wpf-model-center-anomaly-evaluation-recheck-20260708-1920.png`.
- Historical synthetic minimum-confidence recheck: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-late-recheck\classification-evaluation-20260708-011412\classification-evaluation-summary.json` remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches. Model Center captures: `artifacts\ui\wpf-model-center-anomaly-evaluation-late-recheck-20260708-1920.png`, `artifacts\ui\wpf-model-center-anomaly-evaluation-timestamp-lookup-after-20260708-1920.png`, and `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`.
- Historical synthetic explicit-adopt guard recheck: `--wpf-yolov8-anomaly-classification-runtime-smoke` passed, and `scripts\evaluate-yolo-classification.ps1 -MinimumConfidence 0.8` wrote `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-explicit-adopt-guard-recheck\classification-evaluation-20260708-014426\classification-evaluation-summary.json`; it remains `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches.

Missing: independent production-camera or cross-session normal/abnormal held-out evidence. The current adopt result is scoped to one circular-defect source family with only 5 normal / 10 abnormal test images; do not treat it, ImageNet `yolov8n-cls.pt`, or the tiny synthetic fixture as broad production anomaly accuracy.

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
- Candidate Review latest model-comparison lookup now filters summaries by the current baseline/candidate weight paths before showing adoption detail, so a newer summary from another candidate cannot drive the current review state.
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
- Latest inspected GitHub Actions evidence is commit `af643f7d`: run `29154340127` completed with `success` on 2026-07-11 KST.

Latest verified commands for the DLL/CI/docs slice:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.sln -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\sln-isolated-out\
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
git diff --check
```

## 2026-07-12 UI And SEG Persistence Correction

- Expanded learning/template details now use full-width wrapping rows and the outer panel scroll; fixed 86px/42px cards and clipping inner height caps were removed.
- Dataset-purpose badges now stay in separate right-aligned status columns in dataset selection and the current-dataset shell header.
- Real operator artifact inspection proved the brush data was not lost: `Teaching_1001` retained its non-rectangular NG region in `masks\Teaching_1001.png`, while legacy JSON and YOLO TXT contained rectangles.
- The segmentation loader now restores raster objects from the PNG, new saves record `RasterMask`/`Polygon`, and training export traces the mask boundary through the managed `RasterMaskPolygonService`.
- The current turn did not rewrite operator files. Existing sidecars upgrade on normal save/export; an immediate bulk migration requires a separate backup/dry-run decision.
- Focused storage, object verification, candidate training flow, WPF panel/shell/setup, and 1920x1080 plus 1366x768 responsive tests passed. Visual evidence is under `artifacts\ui\20260712-user-reported-ui-mask-regression`.
- Public README/tutorial images were reviewed and not replaced because they do not instruct the changed dataset-result row or expanded detail states; no numbered callout depends on the incidental badge location.

## 2026-07-12 Workspace Resize, SEG Template Shape, And Queue Follow-up

- The remaining deep YOLO report/history caps were removed; essential values and paths wrap under the outer guide scroll.
- Native WPF splitters now resize workflow/canvas and canvas/image-queue columns. Bounds are workflow 72-640px, queue 260-640px, canvas minimum 420px. Workflow expanded width survives collapse/reopen in the current shell session but is not persisted across restarts.
- SEG template matching now uses location from the matcher and geometry from the registered source. Current-image and batch paths both preserve polygon or raster-mask shapes; rectangle fallback remains only when SEG source geometry is unavailable. The current matcher does not rotate or deform source shapes.
- Actual 97-image queue-detail measurement improved from `3555.8ms` to `2474.0ms` (`30.4%`) by moving the full YOLO training/model completion refresh out of the per-row loop. The current 1200-item construction gate is `142.8ms`. The later row-selection fix below supersedes the intermediate render-latency conclusion.
- Current-build evidence: `artifacts\ui\20260712-queue-template-resize\after-yolo-report-unclipped-screen-1920.png` and `after-workspace-resized-screen-1920.png`. Public tutorial images were reviewed and not replaced because they do not teach the deep report or splitter interaction.
- Required isolated build and focused learning/shell/responsive, queue, template, SEG storage/object/training-flow tests passed. Run `--priority-workflow-docs` and `git diff --check` again after any handoff edit.

## 2026-07-12 Image Queue Click Regression Fix

- Root cause was proven: selecting one DataGrid row called `imageQueueView.Refresh()` through the generic open-selection fallback and reevaluated all 97 rows. The bound property and attached selection command could also enter the same orchestration path.
- Row selection now opens its supplied item directly, while the attached command only fills an event-order gap. Generic open/adjacent/failure paths reuse the current filtered view.
- Before/after on the same 97-item SEG queue: filter evaluations `97 -> 0`, visible average `258.8ms -> 36.1ms`. After all 125 `D:\LabelingData\Test01\Images` details loaded: visible average `35.5ms`, warm full-settle average `96.2ms`, filter evaluations `0`.
- Focused queue click/canvas, keyboard, status/filter, selection-service, shell, labeling-session, SEG navigation-save, 1200-item virtualization, required build, docs, and diff gates cover this path.
- No visible UI changed, so no new screenshot or README/tutorial image update is required.

## 2026-07-12 Legacy Rectangular SEG Mask Consistency

- Root cause: legacy untyped rectangle JSON reopened as a polygon when its class-index mask was a solid rectangle, but reopened as a raster mask when another class punched a hole in the same mask. `Teaching_1000` and `Teaching_1001` exposed this class-overlap-dependent behavior.
- Legacy untyped axis-aligned rectangles now use matching sibling mask pixels even when they fill the rectangle. Explicit `GeometryType=Polygon` remains polygon, so current intentional polygons are unchanged.
- Focused storage, WPF segmentation object, candidate training-flow, template-batch, output-root reload, and shell gates pass. Current-build 1920x1080 evidence is under `artifacts\ui\20260712-seg-ok-template-consistency`.
- An initial actual-dataset capture refreshed `review-status.json`; segment JSON, mask PNG, and YOLO label files were unchanged. The status file was restored to SHA-256 `A93ED5757F8E3C3728B48D3A8639AA0C76EE7BB052EDBCD427D3D51B8B05AFC2`, and the final visual smoke uses a temporary sidecar copy and proves the operator status hash stays unchanged.
- This fixes representation consistency, not object-boundary inference. A rectangular template source still produces a rectangular mask; use a brush/polygon source when the OK contour itself must be circular.

## 2026-07-12 SEG Mask Class Badge And Existing-Data Audit

- Raster masks carried `SEG <index> <class>` metadata but only rendered a marker while selected. Every visible committed mask now shows a compact class badge; unselected masks do not draw rectangular bounds.
- Shared SEG, detection-overlay, ROI-overlay, current-image template, and batch-template gates pass. Current-build 1920x1080 evidence is under `artifacts\ui\20260712-seg-mask-class-label`, and actual operator review-status hashes stayed unchanged during capture.
- The active dataset has 125 Version 1 segment files and 139 untyped rectangle records. `Teaching_0` is the only circular OK source mask; 110 other OK-only masks are solid rectangles, and 14 OK+NG images are rectangular OK masks with NG pixels replacing part of them.
- Current template code is already guarded to transfer a registered raster source shape. Correcting the 124 historical targets is a data migration, not another renderer fix. Do not rewrite them without explicit approval, backup, and dry-run evidence.

## 2026-07-14 YOLOv5 Versus YOLOv8 Detection Benchmark Workflow

- Same-data YOLOv5 versus YOLOv8 object-detection analysis is now implemented and has real local validation evidence. The next model-quality priority is an independent, class-balanced object-detection test split, followed by independent anomaly classification runtime/model evidence.
- Training/Model exposes `v5 vs v8 analysis` for object-detection recipes. It prefers the independent `test` split and falls back to `val` only as an engine-performance reference. Candidate Review explicitly labels `val` as training validation and not model-replacement evidence.
- Model Takt is preprocess + inference + postprocess per image from each engine's validation output. It is not WPF/TCP latency, camera time, PLC time, or full equipment Takt time.
- Task and label preflight fail closed so a YOLOv8 SEG weight cannot be used as the YOLOv8 Detect side. Cross-engine save/reject controls stay hidden because engine adoption requires changing the runtime/profile as well as the weight. YOLOv8 folder connection is now dataset-purpose aware and resolves `runs\detect`, `runs\segment`, or `runs\classify` weights without crossing tasks.
- Approved seed `C:\Git\yolov8\yolov8n.pt` (SHA-256 `F59B3D833E2FF32E194B5BB8E08D211DC7C5BDF144B90D2C8412C47CCFC83B36`) was downloaded without a dependency upgrade. A 100-epoch CPU Detect run on the app's 97 train / 28 validation split produced `best.pt` SHA-256 `92CBA615854EBCCC18449AD09CA67452386340C7673AEB627729FC9810553449` under `C:\Git\yolov8\runs\detect\openvisionlab-yolov8n-detect-test01-e100-img320-20260714`.
- Controlled YOLOv5s-versus-YOLOv8n validation used the same data, image size 320, `batch=1`, CPU, and confidence 0.25. Metrics were stable across five runs: YOLOv5s `P 0.999 / R 0.500 / mAP50 0.505 / mAP50-95 0.464`; YOLOv8n `P 0.980 / R 1.000 / mAP50 0.995 / mAP50-95 0.921`. Model-Takt medians were `74.6ms` and `47.946ms` respectively, a 35.7% reduction for YOLOv8n. The YOLOv8 range included one `87.83ms` CPU outlier.
- The operating-reference YOLOv5m comparison measured `P 0.819 / R 1.000 / mAP50-95 0.692 / Takt 146.6ms` versus YOLOv8n `P 0.980 / R 1.000 / mAP50-95 0.921 / Takt 43.255ms`; this is not a controlled architecture comparison because the YOLOv5m training recipe and exact split differ.
- Required isolated build and focused runtime-connection, comparison, Candidate Review, and shell tests pass. Real current-build 1920x1080 evidence is under `artifacts\ui\20260714-yolov5-yolov8-real-validation`.
- The saved-profile restart gate is also complete. A generic settings save had overwritten an authoritative `ObjectDetection` purpose from stale workflow presentation; dataset purpose is now changed only by explicit purpose selection. The actual EXE saved YOLOv8 plus the new Detect `best.pt`, closed, reopened, showed the same engine/weight, and returned one `OK` candidate at confidence `0.982` on first inference. Evidence: `artifacts\exe-yolov8-detect-restart-smoke\codex_yolov8_detect_restart_20260714_193745`.
- The anomaly-classification saved-profile restart gate is complete. New blank recipes no longer clone the previous recipe's model registry, weight, or image root; queue root switching only honors a selected image present in the new root; and an empty root clears the previous canvas/annotation state. The actual EXE saved YOLOv8 classification settings plus normal/abnormal mapping, closed, reopened, returned one `abnormal 99.8%` candidate on first inference, and persisted `Abnormal`. Evidence: `artifacts\exe-yolov8-anomaly-restart-smoke\codex_yolov8_anomaly_restart_20260714_204940`.

## Known Remaining Gaps

- The object-detection dataset has no test images. Validation contains 28 OK objects and only one NG object, so NG recall changes between 0% and 100% on one result. The measured comparison is useful engine evidence but cannot authorize model adoption or broad accuracy claims.
- Takt is currently presented from one native validation run in the UI. Five manual controlled repeats established the first median/range evidence; a repeat-count/median UI option is a future reliability improvement if operators need formal benchmark reports.
- YOLOv8 SEG has a scoped circular operating candidate at confidence `0.20` and a stronger cross-domain review candidate at confidence `0.30`. The latter covers `11/15` positives with `0/27` backgrounds on the combined test but is still `review`, not automatic `promote`.
- Those model results used the then-current four-point SEG exports. Treat them as historical operating evidence until the preserved masks are re-exported as contours, the candidate is retrained, and the unchanged held-out comparison is rerun.
- Legacy solid rectangular class masks now reopen consistently as raster masks even without an overlapping NG class. Existing sidecars are not bulk rewritten; explicit geometry metadata is added on the next normal save.
- Historical template-generated masks remain rectangular at the pixel level. A future migration may transform the `Teaching_0` circular source mask into those target bounds, but it needs explicit data-rewrite approval first.
- `D:\LabelingData\Test01` and `Test02` are hash-identical 125-image copies. They must not be counted as independent sessions.
- Need the next operator loop: collect independently acquired and labeled SEG train/valid/test images for broader accuracy and collect an independent anomaly production-camera/cross-session test set.
- Anomaly classification now has local YOLOv8 pretrained-seed runtime, WPF mapped-inference, a synthetic trained fixture, a core adoption guard, and a 100-image circular-defect run whose unchanged 15-image test split passes at minimum confidence 0.8. The remaining evidence gap is an independent production-camera/cross-session set; the current five-normal/ten-abnormal held-out result is not broad production readiness.
- Anomaly classification has an app-level Model Center `평가 실행` path for held-out evaluation summaries plus manual `평가 불러오기`. Auto-display still works from the active output-root direct, fixed child, or timestamped evaluation summary location. The latest scoped adopt summary is `artifacts\yolo-classification-evaluation\circular-defect-balanced-e20-minconf08-20260711\classification-evaluation-20260711-214135\classification-evaluation-summary.json`.
- YOLO11 remains blocked until a compatible local runtime and weights are verified.
- Workspace widths are session-only; add persistence only if operators request stable per-machine layouts.
- The measured 97-image queue detail pass still takes about 2.5 seconds in the background. Current row switching is verified separately at about 35ms visible average; do not modify Viewer/OpenGL without a new reproduced trace and focused evidence.
- The 125-image real-folder detail pass measured about 3.47 seconds in the background. Row clicks no longer trigger that scan or a full filtered-view refresh; investigate further only if a current build still reproduces blocking while the background pass is active.
- Latest inspected CI is green for `af643f7d` in GitHub Actions run `29154340127`.
- Updating `dll\Lib.Common.dll` or `dll\Lib.OpenCV.dll` now requires intentional binary refresh, EXE output-copy verification, and build verification.
- Any new visible UI change needs fresh current-build capture evidence and tutorial/README image review.

## Recommended Next Work

1. Acquire and label an independent object-detection test set with enough NG examples; do not reuse hash-identical `Test02` as new evidence.
2. Populate the test split, rerun `v5 vs v8 analysis`, review class-specific misses/examples, and record repeated Takt median/range before any engine/model adoption.
3. Then collect an independent production-camera or cross-session normal/abnormal set and rerun the unchanged anomaly classification evaluation guard.
4. Keep completed SEG annotation, template-transfer, queue, splitter, Viewer/OpenGL, and YOLOv8 Detect restart paths stable unless a specific defect is reproduced. Historical SEG remediation remains deferred pending explicit return to that work and data-rewrite approval.

## Useful Verification Commands

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-review-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-heldout
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --exe-yolov8-detect-restart-smoke
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-root-switch
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --exe-yolov8-anomaly-restart-smoke
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-center-real-candidate-save --yolo-root C:\Git\yolov8 --data-yaml .\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260708_213227\dataset\data.yaml --baseline-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt --candidate-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-operating-selected-conf020-20260711\weights\best.pt --candidate-confidence 0.20
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
