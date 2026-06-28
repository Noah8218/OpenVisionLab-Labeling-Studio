# Stable Verified Areas

This document records code paths that have already been performance- or UX-verified and should not be casually refactored. Treat these areas as protected product behavior: change them only when the user reports a new issue in that exact path, or when a focused verification gate proves the change is necessary.

## Brush and Eraser Performance

Status: stable after focused verification.

Protected behavior:

- Brush and eraser MouseMove use the OpenGL/FBO preview path for visible feedback.
- CPU mask/history work is queued and committed after MouseUp instead of running for every MouseMove.
- MouseUp immediately marks the annotation save state as dirty/pending while the FBO preview remains visible; do not wait for delayed CPU materialization before showing "save needed".
- Object Review and undo history are updated once per completed stroke, not continuously during drag.
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
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-annotation-purpose-scope
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --exe-purpose-scope-smoke --seed 260627 --brush-strokes 2
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
```

Latest evidence:

```text
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

Required gates before reporting a change complete:

```powershell
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-session-smoke
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-annotation-object-verification
dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-segmentation-object-verification
```

## Object Detection Queue Reopen and Real EXE Labeling Flow

Status: stable for the industrial object-detection EXE workflow as of 2026-06-27.

Protected behavior:

- Queue Open must not depend only on `DataGrid.SelectedItem`; it also checks the ViewModel-selected row, a unique search match, and a single visible filtered row.
- After saving a label, a queue row may still point to the original staging image path while the saved image copy lives under `data/train|valid|test/images`. Reopen must recover that saved split image instead of reporting "select an image".
- Queue search in real-EXE automation should use the same keyboard/paste input route as a user, not only UIAutomation `ValuePattern.SetValue`.
- The object-detection real-use smoke must verify draw, save, reopen, empty-normal completion, artifact existence, duplicate-stem absence, and dataset readiness in one run.

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
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260627 --label-count 10 --empty-completion-count 2
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
```

Latest evidence:

```text
EXE_INDUSTRIAL_OBJECT_LABELING_SMOKE seed=260627 imagesCopied=317 imagesLabeled=10 emptyCompletion=True emptyCompletionTarget=2 emptyLabelFilesSaved=2 imageFilesAfterSave=317 labelFilesAfterSave=12 labelFilesSaved=10 duplicateImageStems=0 selectedMissingImages=0 selectedMissingLabels=0 boxAvgMs=219.6 boxMaxMs=262.5 reopenVerified=True datasetCheck=True datasetCheckMs=3591.9
Full LabelingApplication.Tests regression passed after saved-split queue reopen fallback.
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
- The canvas workflow strip, helper buttons, and detection-result overlay should use beginner-visible terms: `맞춤`, `이동`, `후보`, `후보 지움`, `검토`, `검출 결과`, `저장 필요`, `미완료`, and `이어서 작업`. Do not reintroduce `Fit`, `Pan`, `Focus`, `AI Reset`, `AI 후보`, `AI 검출 결과`, `미라벨`, or `라벨 없는 다음 이미지` in operator-facing canvas guidance.
- User-facing tool labels, tooltips, object-review rows, status text, and log text must use labeling terms such as `박스`, `폴리곤`, `마스크`, `되돌리기`, and `다시 적용`. Do not expose implementation terms such as OpenGL, FBO, GPU, CPU, ROI, raster mask, or bounding box in operator-facing text.
- Candidate Review, model settings, training settings, dataset dashboard, and status/log text must use beginner-facing model terms such as `겹침`, `추론 실행기`, `모델 파일`, `학습 설정`, `최종 검증`, `기존 모델`, and `새 모델`. Do not expose `IoU`, `Python worker`, `best.pt`, `data.yaml`, `test split`, `baseline`, or `candidate` in operator-facing text unless the view is explicitly showing a file path or developer diagnostic.
- Empty YOLO label files are shown as `빈완료`, not `빈값` or `없음`, so reviewed normal images remain distinct from unknown/unlabeled images.
- The no-candidate quick filter is labeled `검출없음`, not `없음`, so the queue does not confuse reviewed normal images with unlabeled images.
- Candidate Review exposes `이미지 완료` outside the candidate comparison panel through `CompleteImageAndNextButton`; the action stays visible when there are no 검출 후보 and saves an empty label file before moving to the next unfinished image or dataset check.
- The top status bar exposes `단계`, `진행`, and `다음` fields. Keep these beginner-facing and cheap to update; do not calculate them from MouseMove paths.
- Empty project first-run guidance starts at `단계: 데이터셋 준비` and `다음: 데이터셋 시작`; the Guide dataset setup card should show the selected purpose's first action before deeper tutorial content.
- The canvas `검출 결과` HUD mirrors Candidate Review actions (`이전`, `후보`, `기준`, `다음`, `확정`, `스킵`) in a compact top-left overlay. It must not stretch across the canvas or render the long candidate list over the image; long detail stays available through the tooltip and the right Candidate Review panel.
- Candidate Review, canvas helper buttons, guide text, status, and logs should use learner-facing wording such as `검출 후보`, `이동`, `후보`, and `후보 지움`; do not reintroduce `AI 후보`, `Pan`, `Focus`, or `AI Reset` as visible operator text.
- Candidate Review selected-candidate summary must show the selected 검출 후보, any overlapping current label, and the recommended action before the confirm/skip buttons.
- Canvas detection overlay labels must show candidate wording such as `후보 1 OK`, not `AI 1 OK`; the overlay title/result cards should stay on `검출 결과`/`검출 실패`.
- When a 검출 후보 overlaps a manual ROI, a canvas click selects the 검출 후보 first. The `기준`/current-label action is the explicit path for selecting the underlying manual ROI during review.
- When 검출 후보 boxes overlap each other, the canvas click selects the smallest containing candidate first; selected state and draw order are tie-breakers only.
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
Candidate empty-completion UX is covered by --wpf-candidate-review-panel, --wpf-image-queue-status, --wpf-object-review-panel, and --exe-industrial-object-labeling-smoke --empty-completion-count: no-candidate images keep a visible finish-and-next action, save zero-object YOLO labels, move to the next unlabeled image, and show as 빈완료/검출없음 instead of an unknown state.
2026-06-28 user-facing terminology cleanup: `--wpf-learning-workflow-panel`, `--wpf-object-review-panel`, `--wpf-segmentation-object-verification`, `--wpf-labeling-session-smoke`, and `--wpf-visual-smoke --review-tab guide` passed after replacing OpenGL/ROI/raster/bounding-box wording with labeling terms.
2026-06-28 expanded user-facing terminology cleanup: Candidate Review overlap text, model/training settings, dataset dashboard, inference status/logs, model comparison cards, and quality warnings now use beginner labeling terms. `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, focused WPF smokes, and the full default `LabelingApplication.Tests` regression passed.
2026-06-28 documentation guard update: `WpfTrainingSettingsPanel.xaml` no longer keeps mojibake legacy localization-probe text, and `docs\LABELING_PROGRAM_DIRECTION.md` records beginner-facing terminology as protected product behavior. Future UI work should keep visible labels in labeling terms and leave implementation terms in diagnostics, code, or developer documentation only.
2026-06-28 candidate-review wording cleanup: canvas helper buttons and Candidate Review status/log/guide wording now use `검출 후보`, `이동`, `후보`, and `후보 지움` instead of `AI 후보`, `Pan`, `Focus`, and `AI Reset`. `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, `--wpf-candidate-review-panel`, `--wpf-canvas-workflow-context`, `--wpf-learning-workflow-panel`, and `--wpf-visual-smoke --review-tab candidates` passed.
2026-06-28 canvas workflow wording cleanup: the top canvas fit action now says `맞춤`, the detection overlay title says `검출 결과`, review step text says `검토`, dirty-label guidance asks the operator to save the current image labels, and save-complete guidance points to the left queue `다음` button with natural `이어서 작업` wording. `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, `--wpf-canvas-workflow-context`, `--wpf-canvas-detection-overlay`, `--wpf-labeling-session-smoke`, `--wpf-visual-smoke --review-tab guide`, and `--wpf-visual-smoke --review-tab candidates` passed.
2026-06-28 completion-flow wording cleanup: the queue filter now shows `미완료` instead of `미라벨`, empty normal completions no longer remain in the unfinished filter, save completion no longer says `라벨 없는 다음 이미지`, and completing the last queue image refreshes dataset readiness so users can proceed to the next step. Verified with `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false`, `--wpf-canvas-workflow-context`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-learning-workflow-panel`, `--wpf-labeling-session-smoke`, `--wpf-canvas-detection-overlay`, `--wpf-visual-smoke --review-tab guide`, `--wpf-visual-smoke --review-tab candidates`, and `--exe-industrial-object-labeling-smoke --seed 260626 --label-count 3 --empty-completion`.
2026-06-28 top workflow status update: the top status bar now exposes current `단계`, completed/remaining `진행`, and the immediate `다음` action; Candidate Review visible completion text is `이미지 완료`. Verified with `--wpf-status-panels`, `--wpf-image-queue-status`, `--wpf-candidate-review-panel`, `--wpf-canvas-workflow-context`, `--wpf-learning-workflow-panel`, `--wpf-labeling-session-smoke`, `--wpf-canvas-detection-overlay`, both `--wpf-visual-smoke` review tabs, and `--exe-industrial-object-labeling-smoke --seed 260626 --label-count 3 --empty-completion`.
2026-06-28 first-run dataset guidance: empty projects now start at `단계: 데이터셋 준비` / `다음: 데이터셋 시작`, the Guide panel shows a selected-purpose first action, and guide wording uses `검출 후보` instead of `AI 후보`. Verified with `--wpf-learning-workflow-panel`, `--wpf-status-panels`, `--wpf-visual-smoke --review-tab guide`, and `--wpf-dataset-wizard-smoke`.
2026-06-28 overlap selection UX: Candidate Review selected-candidate summary now states the selected 검출 후보, the overlapping current label, and the recommended action (`기존 라벨 확인 후 같으면 스킵` or `맞으면 확정`). Verified with `--wpf-candidate-review-panel`, `--wpf-canvas-detection-overlay`, `--wpf-visual-smoke --review-tab candidates`, and `--wpf-labeling-session-smoke`.
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

## Refactor Rule

When working near a protected path, prefer adding a small adapter or a new higher-level service instead of rewriting the verified hot path. If the hot path must change, document the reason in the final response and include the focused gate results.

Do not run real-EXE UIAutomation smokes in parallel. They share desktop focus and can produce false failures when two EXE windows compete for foreground input.
