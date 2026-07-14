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
- Latest pushed commit at handoff: `af643f7d feat: harden labeling QA and model adoption`
- GitHub Actions run `29154340127` passed for `af643f7d`.
- Worktree is expected to be dirty with local class-specific YOLOv8 SEG comparison, WPF request, focused test, and tracking-document changes unless the previous thread committed them.
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
- The beginner/current-work UI hierarchy pass is complete and should stay stable unless a specific UX defect is reproduced.
- The 2026-07-13 commercial reassessment rates the focused local workflow at about `4.0/5`, the general commercial-suite comparison at about `3.1/5`, and enterprise/team breadth at about `1.2/5`. These are workflow-maturity estimates, not accuracy percentages.
- Current technical priority is a read-only/dry-run remediation report for the 124 historical non-source SEG targets. Do not rewrite operator files without explicit approval. After approval, correct labels, retrain YOLOv8 SEG, and rerun the existing unchanged class-specific comparison.
- The first non-tiny circular-defect anomaly classification smoke is complete; the next anomaly gate is independent production-camera/cross-session held-out evidence.
- YOLO11 remains runtime-gated until the selected local Ultralytics runtime and weights actually support it.

Latest runtime/training correction:

- The SEG recipe already persisted YOLOv8 and the current trained `best.pt`; the training panel's `yolov5x` text was a stale presentation defect.
- Training settings now derive engine/task/start weight and show `YOLOv8 SEG / yolov8n-seg.pt` for SEG.
- Worker readiness verifies request ID, engine when reported, and normalized weight path. The listener keeps one active worker, and app/window shutdown stops the managed worker.
- Real current-weight smoke evidence is `artifacts\real-yolo-smoke\20260713-current-seg-worker-identity\summary.txt`; it used confidence `0.001`, so do not treat it as production model-quality evidence.
- Current 1920x1080 UI evidence is under `artifacts\ui\20260713-yolo-engine-worker-guard`.
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
- Optional AI Candidate Review empty state: Stage 3 reads `AI 후보(선택)`. With zero pending candidates, show only the optional-purpose guidance and label-save/next-image completion card; hide candidate summaries, role cards, selection/confidence controls, empty rows, and an unrun model-comparison dashboard. If candidates exist below the confidence filter, keep controls visible so confidence can be lowered. Latest evidence: `artifacts\ui\20260712-candidate-review-empty-ux\before-candidate-review-empty-1920.png`, `artifacts\ui\20260712-candidate-review-empty-ux\after-candidate-review-empty-final2-retry-1920.png`.
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
- Latest held-out comparison recheck for the same fine-tuned weight is `artifacts\yolo-model-comparison\yolov8-seg-20label-baseline-vs-finetune80-20260708-latest\20260708-185046\comparison-summary.json`; it still reports `promotion.recommendation=promote`, baseline UI candidates `0`, candidate UI candidates `2`, precision `1.0`, recall `0.589`, mAP50 `0.767`, and mAP50-95 `0.257`.
- Latest direct adapter sweep over all 11 held-out test images at confidence `0.25` produced zero OK candidates, one `NG` polygon candidate for `024_NG.png` at `0.5065`, one for `025_NG.png` at `0.7682`, and no candidates for the other three NG images. Treat this as usable but still limited circular-defect evidence, not broad production accuracy.
- Latest 40-label circular SEG evidence: EXE workflow `artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260708_213227` passed with `20/10/10` train/valid/test positive masks plus 20 OK/background labels, and fine-tuned candidate `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-finetune-80ep-img160-20260708\weights\best.pt` passed the corrected comparison gate at `artifacts\yolo-model-comparison\yolov8-seg-40label-baseline-vs-finetune80-20260708-predict-ui\20260708-215754`: precision `0.582`, recall `0.5`, mAP50 `0.683`, mAP50-95 `0.281`, UI candidates `2` at confidence `0.25`, `promotion.recommendation=promote`.
- `scripts\compare-yolo-models.ps1` now keeps YOLOv8 validation metrics and UI candidate evidence separate: metrics come from `model.val`, while `labelsPath`/candidate counts for Candidate Review come from `model.predict`, matching `labeling_tcp_client.py` behavior. Direct adapter sweep `artifacts\yolo-tcp-smoke\yolov8-seg-40label-finetune80-conf025-20260708\summary.json` found 2/10 positive images and 0/6 OK images with candidates at confidence `0.25`.
- The 40-label training run's Ultralytics `best.pt` remains `hold` under the image-level operating gate, but the same run's `last.pt` has a valid operating gap and was packaged byte-for-byte as `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-operating-selected-conf020-20260711\weights\best.pt` with a source/hash/comparison manifest.
- The packaged candidate comparison `artifacts\yolo-model-comparison\yolov8-seg-40label-operating-selected-conf020-20260711\20260711-220539\comparison-summary.json` is `promote` at confidence `0.20`: precision `0.7748`, recall `0.6891`, mAP50 `0.6900`, mAP50-95 `0.1988`, positive coverage `6/10`, background candidates `0/6`.
- Comparison confidence is now part of the adoption guard. Model Center blocks the candidate at confidence `0.25` and saves it at validated confidence `0.20`; real TCP smoke on `025_NG.png` confirms one polygon candidate at `0.6266` into label/segment/mask artifacts, while direct adapter smoke on `018_OK.png` returns zero candidates.
- Multi-class SEG operating evidence is class-specific: `scripts\compare-yolo-models.ps1 -SegmentationPositiveClassName NG` uses only `NG` answer/prediction lines for defect coverage and background rate, and WPF chooses `NG`, then `Defect`, then a sole class. Keep the selected class id/name in summary/report output.
- The combined circular+Teaching review candidate is `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-circular40-teaching125-multidomain-e60-img160-20260711\weights\best.pt`. At confidence `0.30`, fixed combined test evidence is precision `0.6671`, recall `1.0`, mAP50 `0.8799`, mAP50-95 `0.4385`, positive coverage `11/15`, backgrounds `0/27`, and `promotion.recommendation=review`; summary `artifacts\yolo-model-comparison\yolov8-seg-multidomain-best-combined-test-conf030-20260711\20260711-233546\comparison-summary.json`.
- Teaching-only fine-tuning was rejected because it regressed on circular data. The train-only circular-background 4x follow-up was also rejected because combined mAP50/mAP50-95 decreased. Do not select either experiment.
- Model Center saved the multi-domain candidate only inside a temporary smoke recipe at confidence `0.30`; capture `artifacts\ui\wpf-model-center-multidomain-best-review-conf030-20260711-1920.png`. Real TCP polygon-save evidence is under `artifacts\real-yolo-smoke\multidomain-best-conf030-current-image-024-ng-20260711`. The production/current user recipe was not changed.
- `D:\LabelingData\Test01` and `Test02` are hash-identical copies and are not independent sessions. The next SEG input must come from a new production-camera or cross-session acquisition; keep the existing 42-image combined test fixed for regression.
- Focused WPF Model Center adoption smoke for that 40-label candidate passed through `--wpf-model-center-real-candidate-save` and captured `artifacts\ui\wpf-model-center-real-40label-finetune-save-after-20260708-1920.png`.
- Candidate Review visual smoke for that 40-label comparison captured `artifacts\ui\wpf-model-comparison-40label-promote-after-20260709-1920.png`; it shows `차이 2개 이미지 / 예시 2개`, existing model `0`, new model `2`, and Korean promote guidance.
- Real current-image TCP smoke for that 40-label candidate wrote `artifacts\real-yolo-smoke\40label-current-image-025-ng-20260709\summary.txt`; `025_NG.png` returned one `NG` polygon candidate at confidence `0.3039`, then confirmation saved YOLO label text, segment JSON, mask PNG, and review status `Confirmed`.
- Candidate Review display for that promote summary is covered by `--wpf-visual-smoke --model-comparison-summary ...` and capture `artifacts\ui\wpf-model-comparison-finetune-promote-after-1920.png`; the display count must remain aligned with the summary (`기존 모델 0개`, `새 모델 2개`) and long SEG rows must not fall back to bbox confidence parsing.
- Candidate Review latest model-comparison lookup filters summaries by the current baseline/candidate weight paths, so stale summaries from other candidates cannot drive the current adoption state.
- Direct local adapter smoke for the same fine-tuned weight is stored under `artifacts\yolo-smoke\finetune80-20260707`: `025_NG` and `024_NG` each return one `NG` polygon candidate at confidence `0.25`, while `011_OK` returns zero candidates.
- Focused WPF Model Center adoption smoke for the same fine-tuned weight is covered by `--wpf-model-center-real-candidate-save`; capture `artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png`. It verifies the actual `C:\Git\yolov8` matching `runs\segment` candidate is staged, saved to recipe, and recorded as the current inspection model.
- Real YOLO current-image TCP smoke for the same fine-tuned weight is stored under `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260707`: `025_NG` returns one `NG` polygon candidate at confidence `0.7682`, then confirmation saves label text, segment JSON, and mask PNG.
- Latest real YOLO current-image TCP recheck for the same fine-tuned weight is stored under `artifacts\real-yolo-smoke\finetune80-current-image-025-ng-20260708-latest`: `025_NG` again returns one `NG` polygon candidate at confidence `0.7682`, then confirmation saves label text, segment JSON, and mask PNG. Latest Model Center save/adoption capture: `artifacts\ui\wpf-model-center-real-finetune-save-recheck-20260708-latest-1920.png`.
- Local YOLOv8 classification adapter smoke; `C:\Git\yolov8\labeling_tcp_client.py` now converts `result.probs` into an `imageClassification` candidate and `yolov8n-cls.pt` loads successfully through `--smoke-test`.
- WPF YOLOv8 anomaly classification runtime smoke; `--wpf-yolov8-anomaly-classification-runtime-smoke` calls the local adapter through `RunDetectionForImageAsync`, maps the returned top1 class through explicit anomaly settings, and persists the active image as `Abnormal`.
- YOLOv8 anomaly classification trained-fixture smoke; a no-download synthetic normal/abnormal dataset trained `artifacts\yolov8-cls-training-smoke\normal-abnormal-fixture\runs\yolov8n-cls-normal-abnormal-smoke-e10\weights\best.pt`, and that weight returns `abnormal`/`normal` class candidates through the local adapter plus WPF mapped-inference smoke.
- YOLOv8 circular-defect anomaly evidence; 100 unique local images (20 OK, 80 NG) were split deterministically. The first unbalanced run stayed `hold` at 11/15 overall and 1/5 normal. A second run balanced only train sampling by repeating the 10 original normal train images; validation/test stayed unchanged with zero cross-split duplicate hashes. Its `best.pt` is `artifacts\yolov8-cls-training-smoke\circular-defect-real-20260711\runs\yolov8n-cls-circular-defect-balanced-e20-img128-20260711\weights\best.pt`.
- The balanced circular-defect weight passed the existing evaluation guard at minimum confidence 0.8: 15/15, normal 5/5, abnormal 10/10, low-confidence class matches 0, `promotion.recommendation=adopt`. Summary: `artifacts\yolo-classification-evaluation\circular-defect-balanced-e20-minconf08-20260711\classification-evaluation-20260711-214135\classification-evaluation-summary.json`.
- WPF app-path smoke passed held-out `003_OK.png` -> `Normal` and `033_NG.png` -> `Abnormal`. The smoke supports expected class/state/image-size environment values while preserving its original default abnormal behavior.
- Anomaly classification evaluation guard; `AnomalyClassificationEvaluationService` blocks adoption below 10 held-out images, below 5 normal/5 abnormal images, below 0.9 overall accuracy, below 0.8 per-class accuracy, or when correct predictions fall below the configured minimum confidence.
- Anomaly classification evaluation runner; `scripts\evaluate-yolo-classification.ps1` runs the local YOLOv8 adapter over a held-out normal/abnormal split and writes `classification-evaluation-summary.json`. The current synthetic fixture remains `hold` with `4/10` images, `2/5` per class, `0.75/0.9` accuracy, and `0.5/0.8` abnormal accuracy at the default confidence gate. Latest default recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-rerun\classification-evaluation-20260707-231654\classification-evaluation-summary.json`. Latest `-MinimumConfidence 0.8` recheck artifact: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260707-minconf08\classification-evaluation-20260707-232709\classification-evaluation-summary.json`, which remains `hold` with `lowConfidenceClassMatchCount=2`, confidence-gated accuracy `0.25`, normal accuracy `0.5`, and abnormal accuracy `0`.
- Parsed anomaly summaries fail closed: only explicit `promotion.recommendation=adopt` with no hold reasons is adoptable; empty, missing-promotion, or explicit-hold summaries remain non-adoptable.
- Anomaly classification evaluation presentation; `AnomalyClassificationEvaluationService` can read `classification-evaluation-summary.json` back into a report, and `WpfAnomalyClassificationEvaluationPresentationService` converts reports into Korean recommendation/metrics/detail/action text. Model Center now exposes that presentation through a conditional `YoloAnomalyEvaluationPanel` and auto-loads a bounded summary from the active output root for anomaly-detection datasets, including the script's newest immediate `classification-evaluation-*` timestamp folder. The anomaly evaluation card keeps recommendation/metrics visible and defaults blocker detail/next action behind collapsed `YoloAnomalyEvaluationDetailExpander`; current-source captures are `artifacts\ui\wpf-model-center-anomaly-evaluation-baseline-no-summary-1920.png`, `artifacts\ui\wpf-model-center-anomaly-evaluation-after-1920.png`, and `artifacts\ui\wpf-model-center-anomaly-evaluation-compact-detail-after-20260708-1920.png`.
- Model Center also has explicit anomaly `평가 실행` and `평가 불러오기` actions. `평가 실행` exports a fresh classification-evaluation input split from reviewed normal/abnormal images and runs `scripts\evaluate-yolo-classification.ps1` through the local YOLOv8 adapter; `평가 불러오기` selects an existing `classification-evaluation-summary.json`. Latest capture `artifacts\ui\wpf-model-center-anomaly-evaluation-run-button-after-20260708-1920.png`. This adds the in-app generation/run path, but it still does not prove anomaly accuracy without real operator normal/abnormal held-out data.
- Historical synthetic anomaly runtime/evaluation recheck wrote `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-recheck\classification-evaluation-20260708-004229\classification-evaluation-summary.json` and capture `artifacts\ui\wpf-model-center-anomaly-evaluation-recheck-20260708-1920.png`; runtime path passed, but the four-image synthetic fixture remained `hold`.
- Historical synthetic minimum-confidence rechecks remain useful fail-closed evidence: `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-late-recheck\classification-evaluation-20260708-011412\classification-evaluation-summary.json` and `artifacts\yolo-classification-evaluation\normal-abnormal-fixture-20260708-explicit-adopt-guard-recheck\classification-evaluation-20260708-014426\classification-evaluation-summary.json` both remain `hold` with 4 images, normal 1/2, abnormal 0/2, 25% confidence-gated accuracy, and 2 low-confidence class matches.
- Latest focused regression sweep after the guide UX pass: `--wpf-model-comparison-heldout`, `--wpf-model-comparison-run-service`, `--wpf-candidate-polygon-training-flow`, `--wpf-segmentation-object-verification`, `--dataset-readiness-purpose`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-model-comparison-review-service`, `--anomaly-classification-evaluation`, `--wpf-yolov8-anomaly-classification-runtime-smoke`, YOLOv8 adapter compile/self-test, PowerShell parser checks for the YOLO comparison/evaluation scripts, solution build, output-copy check for checked-in Noah/OpenCvSharp DLL dependencies, and `--wpf-responsive-layout --width 1920 --height 1080` passed on 2026-07-08 KST.
- 2026-07-12 user-reported correction: expanded learning/template content now uses full-width wrapping rows and outer scrolling, and dataset-purpose badges use stable right-aligned columns. Real `Teaching_1001` inspection proved the brush mask PNG was preserved while legacy JSON/TXT used rectangles. Loading and the next YOLO export now recover managed contours from the mask, including concavities, cutouts, and disconnected regions. Current-build evidence is under `artifacts\ui\20260712-user-reported-ui-mask-regression`; operator files were not rewritten.
- 2026-07-12 follow-up: deep YOLO report/history text now wraps under the outer scroll, and native splitters resize workflow/canvas/image-queue columns while keeping the canvas at least 420px. SEG current-image template application now preserves registered polygons or raster masks just like batch application. Actual 97-image queue-detail scan improved from 3555.8ms to 2474.0ms after eliminating per-row training/model completion refresh. Current-build evidence is under `artifacts\ui\20260712-queue-template-resize`; public tutorial images were reviewed and not replaced.
- 2026-07-12 queue click regression fix: one row click previously called `imageQueueView.Refresh()` and reevaluated all 97 rows. Selection now opens the supplied item directly and the attached event fallback does not duplicate the bound ViewModel callback. Same-run instrumentation improved from 97 to 0 filter evaluations and 258.8ms to 36.1ms visible average; the fully detailed 125-image operator folder averages 35.5ms. No visible UI changed, so no screenshot/tutorial refresh was needed.
- 2026-07-12 legacy rectangular SEG mask consistency: legacy untyped axis-aligned rectangle JSON now uses matching sibling class-index mask pixels even when they fill a solid rectangle. This keeps OK-only and OK+NG images on the same raster-mask rendering path; explicit `GeometryType=Polygon` remains polygon. Actual-dataset evidence is under `artifacts\ui\20260712-seg-ok-template-consistency`. An initial visual run refreshed `review-status.json`, so it was restored to SHA-256 `A93ED5757F8E3C3728B48D3A8639AA0C76EE7BB052EDBCD427D3D51B8B05AFC2`; subsequent ROI-only existing-dataset visual smoke uses a temporary sidecar copy and leaves the operator hash unchanged.
- 2026-07-12 SEG mask class badge: visible committed raster masks now show compact `OK`/`NG` class badges without requiring selection and without adding rectangle bounds around unselected segmentation pixels. Selected masks keep their full edit marker. Current-build evidence is under `artifacts\ui\20260712-seg-mask-class-label`.
- Existing-data audit: all 125 inspected segment files are Version 1 and all 139 JSON records are untyped rectangles. `Teaching_0` alone has the circular OK brush mask; 110 other OK-only masks are solid rectangles, while 14 OK+NG images have rectangular OK masks with NG pixels replacing part of them. Current template tests prove raster source-shape transfer; historical target correction requires a separately approved backup/dry-run migration.
- Model candidate rejected-history guard and candidate decision presentation text.
- README/release/CI skeleton, checked-in `dll\Lib.Common.dll` / `dll\Lib.OpenCV.dll` dependency/output-copy contract, and NuGet-restored OpenCvSharp support DLL output-copy contract with local `packages` fallback.

Latest 2026-07-14 priority and implementation state:

- The immediate product priority is same-data YOLOv5 versus YOLOv8 object-detection comparison. Independent anomaly classification runtime/model evidence is next; historical SEG remediation is deferred unless the user explicitly returns to it.
- Training/Model now exposes `v5 vs v8 analysis` for object-detection recipes. It resolves separate local Python/source/weight settings for both engines, uses the same held-out `test` split, image size, and `batch=1`, and presents precision, recall, mAP50, mAP50-95, inference time, and model Takt in Candidate Review.
- Model Takt means validation preprocess + inference + postprocess per image. It excludes WPF/TCP, camera, PLC, and full equipment-cycle time.
- Cross-engine results are review-only because adoption must switch the engine runtime/profile and weight together. Model-task and label preflight fail closed.
- Focused code/UI tests and current-build 1920x1080 fixture presentation passed. The fixture proves workflow and layout only, not real model quality.
- `C:\Git\yolov8` currently has no YOLOv8 Detect weight. Do not download `yolov8n.pt`, upgrade dependencies, or claim measured v5/v8 readiness without explicit approval or an operator-provided compatible Detect weight.

Known remaining gaps:

- YOLOv8 SEG has a scoped circular operating candidate at confidence `0.20` and a stronger multi-domain `review` candidate at confidence `0.30`. The latter covers `11/15` positives with `0/27` backgrounds on the combined test but is not automatic production adoption.
- Existing SEG candidates were trained from the previous four-point exports. Keep their results as historical evidence until masks are re-exported as contours and training/held-out comparison is rerun.
- The representation fix does not infer a circular contour from a rectangular template source. Shape-accurate OK transfer still requires a brush/polygon source annotation.
- Do not bulk-rewrite the 124 historical template targets without explicit user approval. Preserve current operator files until a migration preview reports per-image old/new pixel counts and backup paths.
- `D:\LabelingData\Test01` and `Test02` are duplicate copies, not independent evidence.
- Anomaly classification has a scoped circular-defect `adopt` result, but it is only one source family with 5 normal / 10 abnormal held-out images. Train oversampling is not new evidence. Collect an independent production-camera/cross-session set before claiming broad production anomaly accuracy.
- YOLO11 is still blocked until compatible runtime and weights are verified.
- Workspace widths are remembered only for the current shell session. Queue detail refresh still takes about 2.5 seconds for the measured 97-image folder, while row switching is now verified at about 35ms visible average. Do not change Viewer/OpenGL without a new reproduced trace and focused evidence.
- Latest inspected GitHub Actions evidence is commit `af643f7d`, run `29154340127`, which completed with `success`.
- Updating bundled `Lib.Common.dll` or `Lib.OpenCV.dll` requires intentional binary refresh, EXE output-copy verification, and build verification.

Recommended next work:

1. Obtain explicit approval to cache a compatible YOLOv8 Detect seed such as `yolov8n.pt`, or use an operator-provided local Detect weight. Do not spend model/runtime work until this prerequisite exists.
2. Train YOLOv8 Detect on the same object-detection recipe and verify its `ObjectDetection` model-registry candidate and weight path.
3. Run `v5 vs v8 analysis` on the same held-out test split and retain exact weights, test image count, image size, `batch=1`, device, metrics, inference time, and model Takt.
4. Review detection examples and runtime-switch implications before choosing the engine; never use the UI fixture values as model evidence.
5. Then rerun anomaly classification on an independent production-camera/cross-session normal/abnormal set.
6. Keep completed SEG annotation, queue, splitter, and Viewer/OpenGL paths stable unless a reproduced defect requires focused work.

Useful verification commands:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-segmentation-object-verification
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-polygon-training-flow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-center-real-candidate-save --yolo-root C:\Git\yolov8 --data-yaml .\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260707_221147\dataset\data.yaml --baseline-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt --candidate-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-20label-finetune-80ep-img160-20260707\weights\best.pt --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-real-finetune-save-after-1920.png
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-center-real-candidate-save --yolo-root C:\Git\yolov8 --data-yaml .\artifacts\exe-circular-segmentation-workflow\circular_seg_exe_20260708_213227\dataset\data.yaml --baseline-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt --candidate-weights C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-40label-operating-selected-conf020-20260711\weights\best.pt --candidate-confidence 0.20 --width 1920 --height 1080 --output .\artifacts\ui\wpf-seg-operating-conf020-adopt-after-20260711-1920.png
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
