# WPF Autonomous Progress

This file tracks completed WPF migration loops and the next work queue so the same cleanup is not repeated.

## Completed

### 2026-06-21 - Class catalog ViewModel state

- `WpfClassCatalogPanelViewModel` now owns class input, selected class, class list, output-root path, and status text.
- `WpfClassCatalogPanel.xaml` binds those values instead of relying on shell-owned `TextBox` and `ListBox` state.
- `WpfLabelingShellWindow` now populates the class catalog through `ClassCatalogViewModel.SetClasses(...)` and reads output-root saves from the ViewModel first.
- Korean visible labels are represented with XML character entities so XAML stays encoding-safe while the UI renders Korean text.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-class-catalog-viewmodel-v2.log`
  - `artifacts\logs\tests-20260621-wpf-class-catalog-viewmodel-v2.log`
  - `artifacts\logs\visual-20260621-wpf-class-catalog-viewmodel-classes-tab-v1.log`
  - `artifacts\ui\wpf-class-catalog-viewmodel-classes-tab-v1.png`

### 2026-06-21 - YOLO status ViewModel state

- `WpfYoloStatusPanelViewModel` now owns settings summary/detail, command status, command progress visibility, command progress value, and indeterminate state.
- `WpfYoloStatusPanel.xaml` binds those values.
- Normal shell paths call `YoloStatusViewModel.SetCommandStatus(...)`, `SetCommandBusy(...)`, and `SetSettingsStatus(...)`; direct control updates remain only as compatibility fallback.
- Korean command labels are represented with XML character entities for safe source encoding.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-yolo-status-viewmodel-v1.log`
  - `artifacts\logs\tests-20260621-wpf-yolo-status-viewmodel-v1.log`
  - `artifacts\logs\visual-20260621-wpf-yolo-status-viewmodel-yolo-tab-v1.log`
  - `artifacts\ui\wpf-yolo-status-viewmodel-yolo-tab-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-viewmodel-panels.log`

### 2026-06-21 - Object review ViewModel state

- `WpfObjectReviewPanelViewModel` now owns object summary text, object rows, selected object, class options, selected class, and delete/apply enabled state.
- `WpfObjectReviewPanel.xaml` binds the object list and class selector instead of relying on shell-owned `ListBox.Items` and `ComboBox.Items` on the normal WPF path.
- `WpfLabelingShellWindow` keeps the existing delete/class-change service behavior, but reads selected object payloads and selected class names from the ViewModel first.
- The Object Review class ComboBox now uses the same dark-theme chrome pattern as the project recipe ComboBox, removing the bright default WPF control from the dark UI.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-object-review-viewmodel-v6.log`
  - `artifacts\logs\tests-20260621-wpf-object-review-viewmodel-v5.log`
  - `artifacts\logs\visual-20260621-wpf-object-review-viewmodel-objects-tab-v2.log`
  - `artifacts\ui\wpf-object-review-viewmodel-objects-tab-v2.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-object-review-viewmodel.log`

### 2026-06-21 - Candidate review ViewModel state

- `WpfCandidateReviewPanelViewModel` now owns confidence text, candidate detail text, candidate rows, selected candidate, action enabled state, and action tooltips.
- `WpfCandidateReviewPanel.xaml` binds candidate rows and selection through the ViewModel instead of relying on shell-owned `ListBox.Items` on the normal WPF path.
- `WpfLabelingShellWindow` now reads selected candidate payloads from the ViewModel first and updates candidate detail/action state through the ViewModel when the WPF panel is available.
- Candidate review tests now verify the bound row collection, selected state, duplicate warning state, action state, and comparison panel.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-candidate-review-viewmodel-v5.log`
  - `artifacts\logs\tests-20260621-wpf-candidate-review-viewmodel-v2.log`
  - `artifacts\logs\visual-20260621-wpf-candidate-review-viewmodel-candidates-tab-v2.log`
  - `artifacts\ui\wpf-candidate-review-viewmodel-candidates-tab-v2.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-candidate-review-viewmodel-v2.log`

### 2026-06-21 - Candidate review presenter

- `WpfCandidateReviewPresenter` now owns candidate row text, detail text, comparison panel text, icon/brush state, confirmability, duplicate-overlap policy, and disabled-action hints.
- `WpfLabelingShellWindow` still calculates current-label overlap from active manual/confirmed labels, but delegates candidate display decisions to the presenter.
- Candidate ViewModel rows, fallback list rows, canvas summary text, comparison card, and candidate action state now share the same display policy instead of rebuilding similar strings in separate shell methods.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-candidate-presenter-v1.log`
  - `artifacts\logs\tests-20260621-wpf-candidate-presenter-v1.log`
  - `artifacts\logs\visual-20260621-wpf-candidate-presenter-candidates-tab-v1.log`
  - `artifacts\ui\wpf-candidate-presenter-candidates-tab-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-candidate-presenter.log`

### 2026-06-21 - Object review presenter

- `WpfObjectReviewPresenter` now owns current-object summary text, empty text, manual ROI row text, manual ROI tooltip text, confirmed-AI row text, and object row construction.
- `WpfLabelingShellWindow` still owns annotation mutation, selection restore, class synchronization, and save coordination, but no longer hand-builds normal WPF object review rows.
- The fallback `ListBoxItem` path and normal ViewModel path share the same presenter text for object review rows.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-object-presenter-v1.log`
  - `artifacts\logs\tests-20260621-wpf-object-presenter-v1.log`
  - `artifacts\logs\visual-20260621-wpf-object-presenter-objects-tab-v1.log`
  - `artifacts\ui\wpf-object-presenter-objects-tab-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-object-presenter.log`

### 2026-06-21 - Non-viewer WPF fallback cleanup

- OpenGL/ImageCanvas work is excluded from this cleanup scope because it will be split into a separate project.
- Candidate Review now uses the WPF ViewModel path only; the shell no longer rebuilds candidate rows through `CandidateListBox.Items.Add` or direct candidate action button state.
- Object Review now uses the WPF ViewModel path only; the shell no longer rebuilds object rows through `ObjectListBox.Items.Add` or direct delete/apply button state.
- Removed dead helper methods that only supported the old direct `ListBoxItem` fallback path.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-nonviewer-fallback-cleanup-v2.log`
  - `artifacts\logs\tests-20260621-wpf-nonviewer-fallback-cleanup-v1.log`
  - `artifacts\logs\visual-20260621-wpf-nonviewer-fallback-cleanup-candidates-v1.log`
  - `artifacts\ui\wpf-nonviewer-fallback-cleanup-candidates-v1.png`
  - `artifacts\logs\visual-20260621-wpf-nonviewer-fallback-cleanup-objects-v1.log`
  - `artifacts\ui\wpf-nonviewer-fallback-cleanup-objects-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-nonviewer-fallback-cleanup.log`

### 2026-06-21 - Class catalog fallback cleanup

- OpenGL/ImageCanvas remains excluded from this cleanup scope because it will be split into a separate project.
- Class Catalog now uses the WPF ViewModel path for class list population, selected class lookup, output-root edits, and status text.
- Removed the remaining shell fallback that read selected classes directly from `ClassListBox.SelectedItem`.
- Regression tests now check that the shell does not read class selection directly from the list box.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-class-catalog-fallback-cleanup-v3.log`
  - `artifacts\logs\tests-20260621-wpf-class-catalog-fallback-cleanup-v2.log`
  - `artifacts\logs\visual-20260621-wpf-class-catalog-fallback-cleanup-classes-v1.log`
  - `artifacts\ui\wpf-class-catalog-fallback-cleanup-classes-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-class-catalog-fallback-cleanup.log`

### 2026-06-21 - Image queue one-click canvas loading

- OpenGL/ImageCanvas internals were not changed.
- Image queue single-click now loads the clicked image into the Main canvas using `populateQueue: false`, so the queue is not rebuilt on every click.
- The old top queue preview block was removed from the WPF panel, giving more room to the image list.
- Selected queue rows now show a stronger left accent line and selected-row background so the active item is clearer.
- Regression tests now verify that a queue click changes the active image path and that the preview controls are absent.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-queue-click-load-v1.log`
  - `artifacts\logs\tests-20260621-wpf-queue-click-load-v1.log`
  - `artifacts\logs\visual-20260621-wpf-queue-click-load-v1.log`
  - `artifacts\ui\wpf-queue-click-load-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-queue-click-load.log`

### 2026-06-21 - Workflow command-state service

- OpenGL/ImageCanvas internals were not changed.
- Removed the disabled legacy queue preview code block from the WPF shell.
- Added `WpfWorkflowCommandStateService` so workflow mode, busy state, inference availability, training stop availability, project-save availability, and detection tooltips are calculated outside the shell.
- `WpfLabelingShellWindow` now applies the calculated command state to buttons instead of rebuilding the same booleans inline.
- `UpdateBatchDetectionControls` no longer writes queue detection button enabled state before immediately delegating to the workflow command update.
- Regression tests now cover the command-state service rules directly.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-command-state-service-v1.log`
  - `artifacts\logs\tests-20260621-wpf-command-state-service-v1.log`
  - `artifacts\logs\visual-20260621-wpf-command-state-service-v1.log`
  - `artifacts\ui\wpf-command-state-service-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-command-state-service.log`

### 2026-06-21 - Object review edit service

- OpenGL/ImageCanvas internals were not changed.
- Added `WpfObjectReviewEditService` and `WpfObjectReviewItemRef` so Object Review class lookup, class application, manual ROI deletion, and confirmed-AI candidate deletion are no longer implemented as shell-local mutation helpers.
- `WpfLabelingShellWindow` now delegates apply/delete behavior to the service while keeping refresh, annotation save, and canvas redraw coordination in the shell.
- Removed the old shell helper methods that directly changed manual ROI class names or deleted manual/current objects.
- Regression tests now cover manual ROI class application, confirmed-AI class application, manual deletion, confirmed-AI deletion, and invalid delete rejection.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-object-review-edit-service-v2.log`
  - `artifacts\logs\tests-20260621-wpf-object-review-edit-service-v1.log`
  - `artifacts\logs\visual-20260621-wpf-object-review-edit-service-v1.log`
  - `artifacts\ui\wpf-object-review-edit-service-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-object-review-edit-service.log`

### 2026-06-21 - Object review ViewModel-only selection

- OpenGL/ImageCanvas internals were not changed.
- Removed the remaining Object Review selected-list fallback from `WpfLabelingShellWindow`; selected objects are now read from `WpfObjectReviewPanelViewModel.SelectedObject` only.
- Regression tests now verify that the WPF ListBox selection flows through the ViewModel binding before class apply/delete behavior runs.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-object-review-selection-viewmodel-v1.log`
  - `artifacts\logs\tests-20260621-wpf-object-review-selection-viewmodel-v1.log`
  - `artifacts\logs\visual-20260621-wpf-object-review-selection-viewmodel-v1.log`
  - `artifacts\ui\wpf-object-review-selection-viewmodel-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-object-review-selection-viewmodel.log`

### 2026-06-21 - Learning workflow and annotation palette

- OpenGL/ImageCanvas internals were not changed.
- Added `WpfLearningWorkflowPanel` and `WpfLearningWorkflowPanelViewModel` as a first WPF education UX layer.
- The new panel defines beginner-facing modes: Labeling, Object Detection, Segmentation, Anomaly Detection, Train, Infer, and Review.
- The new annotation tool palette defines Select, Box, Ellipse/Circle, Polygon, Brush, Eraser, Pan/Zoom, Undo, Redo, and Delete as ViewModel state.
- The shell hosts the learning workflow panel inside the right `Guide` tab instead of pinning all education/tool information to the main workspace.
- Object Detection, Infer, and Review still map to the existing inference workflow mode while labeling tools stay in label-edit mode.
- The sample learning steps are clickable: Sample loads the sample image, Label switches to labeling and adds the current safe box ROI, Infer switches to inference mode, Review opens the candidate review tab, and Save uses the existing annotation save path.
- Ground-truth labels and AI predictions are shown as distinct chips so the UI does not blur human labels with model results.
- Visual smoke was checked in dark and light themes.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-learning-guide-tab-v2.log`
  - `artifacts\logs\tests-20260621-wpf-learning-guide-tab-v2.log`
  - `artifacts\logs\visual-20260621-wpf-learning-guide-tab-main-v1.log`
  - `artifacts\ui\wpf-learning-guide-tab-main-v1.png`
  - `artifacts\logs\visual-20260621-wpf-learning-guide-tab-v1.log`
  - `artifacts\ui\wpf-learning-guide-tab-v1.png`
  - `artifacts\logs\visual-20260621-wpf-learning-guide-tab-light-v1.log`
  - `artifacts\ui\wpf-learning-guide-tab-light-v1.png`
  - `artifacts\logs\verify-first-run-20260621-wpf-learning-guide-tab.log`

### 2026-06-21 - Image click and viewer pan performance pass

- OpenGL/ImageCanvas internals were changed for this pass because the active user issue was viewer pan stutter and queue-click latency.
- Root cause found: queue selection used the same heavy image-load path as explicit file loading, including active review-status refresh/save, log append, repeated canvas refreshes, and a second bitmap-to-Mat conversion.
- Root cause found: mouse move performed OpenGL pixel readback for gray/color values on every event. GPU readback can stall pan even for a 1-pixel read.
- Root cause found: `RefreshGL()` and mouse move recalculated visible overlays with `Parallel.ForEach`, LINQ `ToArray`, and `Min/Max` on every frame; for normal ROI counts this is more overhead than a direct loop.
- `TryOpenSelectedQueueImage` now uses a lightweight `TryLoadImage` path: no queue rebuild, no queue detail reload, no review-status save, and no log-panel append for ordinary item clicks.
- `TryLoadImage` now batches canvas image/ROI/overlay clearing under `SuppressRefresh()` and reuses the decoded `imageMat.Clone()` for `CDisplayManager.ImageSrc` instead of converting the bitmap twice.
- `RedrawReviewRois` now batches clear/add/set-overlay operations and performs one final OpenGL refresh.
- `CanvasImageLoader.UploadMatAsTexture` now runs zoom-to-fit inside the refresh suppression scope and performs a single final refresh.
- Small same-size single-tile images now reuse the existing OpenGL texture with `TryReplaceSingleTexture` instead of deleting and recreating the texture on every queue click.
- `ImageCanvasControl.OnMouseMove` now skips pixel readback while dragging, throttles hover readback, reads gray/color from one `ReadPixels` call, and throttles visible-overlay recalculation during pan.
- `CalculatorVisibleOverlays` now uses a direct loop instead of per-frame `Parallel.ForEach` and LINQ allocation.
- Regression tests were added for the lightweight queue-click path and the OpenGL mouse-pan/readback guard.
- Final local Debug queue-click smoke: `min 29.8 ms / avg 49.7 ms / max 109.3 ms` across 11 switches. The first cold switch was the max; the following 10 switches averaged about 43.8 ms.

### 2026-06-21 - Inference result recentering

- After AI candidates are applied, the WPF shell now calls `ZoomToFit()` so the current image returns to the centered fit view.
- This runs through the shared `ApplyDetectionCandidates` path, so worker inference and smoke/diagnostic inference get the same viewer behavior.
- Regression coverage now checks that WPF inference keeps the post-detection center/fit action in place.

### 2026-06-21 - Canvas commands, candidate navigation, adjacent decode cache, and education guide polish

- Added a compact canvas command strip in `WpfCanvasPanel`: Fit, 1:1, Pan, selected-candidate Focus, and AI Reset.
- Added `ImageCanvasControl.ZoomToActualSize()` so the WPF 1:1 command is backed by the OpenGL viewer instead of being a cosmetic button.
- Added candidate review navigation controls in `WpfCandidateReviewPanel`: previous, focus, and next. The candidate list also supports `N`, `P`, and `F` keyboard review.
- Selected-candidate Focus converts image-pixel candidate bounds to OpenGL world bounds and calls `FitToRect`, so it stays stable across zoom/pan states.
- Added a bounded adjacent-image decode cache around queue image switching. The current row's nearby images are decoded on a background task and consumed by the next lightweight `TryLoadImage` call.
- Expanded `WpfLearningWorkflowPanelViewModel` with concise beginner guidance for YOLO, U-Net segmentation, anomaly detection, training, inference, and review.
- Added brush size and mask opacity state to the education panel. These controls are now visible in the Guide tab instead of crowding the main workspace.
- Connected the first palette actions to verified existing paths: Box enters drawing mode, Pan enters viewer drag mode, and Delete routes to selected-object deletion.
- Verified with:
  - `artifacts\logs\build-20260621-wpf-ux-commands-v5.log`
  - `artifacts\logs\tests-20260621-wpf-ux-commands-v3.log`
  - `artifacts\logs\visual-20260621-wpf-ux-commands-v2.log`
  - `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`
  - `artifacts\logs\perf-20260621-wpf-ux-commands-v2.log`

### 2026-06-21 - YOLO-first UX continuation and U-Net deferral

- U-Net runtime/model implementation is explicitly deferred. It may become a separate Python project, so this pass did not add U-Net execution or protocol work.
- Candidate confirm/skip now preserves review flow by selecting the next visible candidate instead of jumping back to the first row.
- If candidates remain after confirmation, the shell stays on Candidate Review and focuses the next candidate in the viewer. If no candidates remain, it moves to Object Review.
- Canvas command buttons now follow state: Fit/1:1/Pan require an active image, Focus requires a selected candidate, and AI Reset requires pending AI candidates.
- The adjacent-image decode cache now skips images larger than the pixel limit so large folders do not keep oversized Bitmap/Mat pairs in memory.
- Verified with:
  - `artifacts\logs\build-20260621-yolo-review-flow-v3.log`
  - `artifacts\logs\tests-20260621-yolo-review-flow-v3.log`
  - `artifacts\logs\visual-20260621-yolo-review-flow-v1.log`
  - `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`
  - `artifacts\logs\perf-20260621-yolo-review-flow-v1.log`

### 2026-06-21 - YOLOv5 training workflow guide

- Reframed the beginner path around the real YOLOv5 training sequence: load image folder, register classes, draw box labels, save/check dataset, train YOLOv5, then review inference with the trained `best.pt`.
- Moved that six-step path to the top of the Guide tab so a new user sees the training route before the general education modes/tools.
- Kept the main workspace focused on queue, canvas, and active review. The training explanation stays in the task-specific Guide tab instead of adding more always-visible controls.
- Kept U-Net runtime/model work deferred. This pass only improves the YOLOv5 training UX.
- Verified with:
  - `artifacts\logs\build-20260621-yolo-training-workflow-guide-v2.log`
  - `artifacts\logs\tests-20260621-yolo-training-workflow-guide-v2.log`
  - `artifacts\logs\visual-20260621-yolo-training-guide-v3.log`
  - `tests\artifacts\ui\wpf-yolo-training-guide-check.png`

### 2026-06-21 - YOLOv5 training workflow actions

- Made the six training-guide rows clickable and routed each row to the relevant existing WPF surface.
- Step 1 opens the image-folder picker, step 2 focuses the class catalog, step 3 selects labeling/box mode, step 4 saves current labels when possible and refreshes dataset readiness, step 5 focuses the YOLO training settings and explicit Start button, and step 6 prepares inference review.
- Added a dataset readiness card to the Guide tab so the user can see whether the saved YOLO dataset is trainable without hunting through the YOLO tab.
- Added latest-`best.pt` candidate discovery for post-training review. The app checks project/output roots and `runs/train/*/weights/best.pt`, then applies the newest usable weight path.
- Kept training start explicit. The guide does not auto-run long training; it moves the user to the Start button.
- Kept U-Net runtime/model work deferred.
- Verified with:
  - `artifacts\logs\build-20260621-yolo-training-workflow-actions-v1.log`
  - `artifacts\logs\tests-20260621-yolo-training-workflow-actions-v1.log`
  - `artifacts\logs\visual-20260621-yolo-training-workflow-actions-v1.log`
  - `tests\artifacts\ui\wpf-yolo-training-workflow-actions-check.png`

### 2026-06-21 - YOLOv5 training workflow polish

- Added per-step state badges to the YOLOv5 guide: image load, class registration, box labeling, dataset check, training, and inference review now show completed/waiting/problem states.
- Added direct fix buttons under the readiness card for the common blockers: classes, labels, and dataset check.
- Applying a newly trained `best.pt` now moves the operator to the YOLO settings save action and marks that the active recipe still needs saving.
- Training readiness/progress now gets color treatment for ready, waiting, running, completed, stopped, and failed/error states.
- Kept then-unverified drawing tools honest. At this checkpoint, ellipse, polygon, brush, eraser, undo, and redo showed clear pending status instead of pretending to draw.
- Verified with:
  - `artifacts\logs\build-20260621-yolo-training-workflow-polish-v1.log`
  - `artifacts\logs\tests-20260621-yolo-training-workflow-polish-v1.log`
  - `artifacts\logs\visual-20260621-yolo-training-workflow-polish-v1.log`
  - `tests\artifacts\ui\wpf-yolo-training-workflow-polish-check.png`

### 2026-06-21 - YOLOv5 training guide history and issue split

- Added `YoloTrainingGuideHistory` to project settings so the guide can remember the last dataset check, training status, applied `best.pt`, and whether that weight path has been saved to the active recipe.
- Split dataset blockers into clearer operator actions: missing classes, missing labels, missing valid images, invalid `data.yaml`, invalid label content, output root problems, and image-folder problems.
- Added separate action text and recent-history text to the Guide tab readiness card.
- Preserved the explicit-save behavior for newly applied `best.pt`; the guide shows `recipe 미저장` until the YOLO settings save succeeds.
- Verified that the remaining drawing tools should not be wired through legacy `CViewer` from the WPF canvas path yet. They stay as pending status until the actual WPF viewer command path is verified.
- Verified with:
  - `artifacts\logs\build-20260621-yolo-training-history-issues-v1.log`
  - `artifacts\logs\tests-20260621-yolo-training-history-issues-v1.log`
  - `artifacts\logs\visual-20260621-yolo-training-history-issues-v1.log`
  - `tests\artifacts\ui\wpf-yolo-training-history-issues-check.png`

### 2026-06-21 - YOLOv5 training run history list

- Added a bounded `TrainingGuide.RunHistory` list to keep the recent dataset-check, training-terminal, and weight-apply/save events.
- The Guide tab now renders a compact recent-run list from that stored history without adding startup auto-check noise.
- Weight records are updated when the same `best.pt` moves from `recipe 미저장` to `recipe 저장됨`, so the user sees whether the active recipe actually points at the trained weight.
- Verified with:
  - `artifacts\logs\build-20260621-yolo-training-run-history-v1.log`
  - `artifacts\logs\tests-20260621-yolo-training-run-history-v1.log`
  - `artifacts\logs\visual-20260621-yolo-training-run-history-v1.log`
  - `tests\artifacts\ui\wpf-yolo-training-run-history-check.png`

### 2026-06-21 - Queue filter, guide focus, and global inference status

- Changed the image-queue quick filters from one cramped six-button row to a 3x2 layout with larger cells, fixing clipped icons/text and the bottom-border squeeze.
- Kept the Guide tab focused on the actionable YOLOv5 training sequence; secondary mode/flow/tool concept controls are now collapsed under `추가 개념`.
- Renamed the readiness shortcut buttons to direct actions: `클래스 등록`, `라벨링 시작`, and `데이터셋 점검`.
- Added a top-level `추론 상태` chip so single-image and batch inference progress is visible without opening the YOLO settings tab.
- Verified with:
  - `artifacts\logs\build-20260621-queue-guide-inference-status-v3.log`
  - `artifacts\logs\tests-20260621-queue-guide-inference-status-v3.log`
  - `artifacts\logs\visual-20260621-queue-guide-inference-status-v2.log`
  - `tests\artifacts\ui\wpf-queue-guide-inference-status-check-v2.png`

### 2026-06-21 - Inference result centering and top progress pulse

- Re-centered the canvas after AI candidates are applied by calling `ZoomToFit()` immediately, after render, and again at idle.
- Replaced the top inference chip's default indeterminate progress animation with a render-priority pulse timer so status text updates do not restart the animation.
- Added cleanup for the pulse timer when the WPF shell closes.
- Added a `--wpf-visual-smoke --show-busy-inference-status` capture option for checking the busy top-bar state directly.
- Verified with:
  - `artifacts\logs\build-20260621-inference-center-progress-v4.log`
  - `artifacts\logs\tests-20260621-inference-center-progress-v3.log`
  - `artifacts\logs\visual-20260621-inference-center-progress-v1.log`
  - `tests\artifacts\ui\wpf-inference-center-progress-check-v1.png`
  - `artifacts\logs\visual-20260621-inference-center-progress-busy-v2.log`
  - `tests\artifacts\ui\wpf-inference-center-progress-busy-check-v2.png`

### 2026-06-21 - First-user tutorial and HTML guide

- Added a `처음 10분 튜토리얼` card at the top of the Guide tab.
- The card summarizes the first usable path: load images, register classes, draw box labels, check the dataset, train YOLOv5, and review inference candidates.
- Added `docs\tutorial\labeling-workbench-tutorial.html` with step-by-step WPF screenshots from the current program.
- Captured tutorial images under `docs\tutorial\images`.
- README now points first-time users to both the in-app Guide tab and the HTML tutorial.
- Verified with:
  - `artifacts\logs\build-20260621-tutorial-ui-html-v1.log`
  - `artifacts\logs\tests-20260621-tutorial-ui-html-v1.log`
  - `artifacts\logs\visual-20260621-tutorial-html-captures-v1.log`

### 2026-06-21 - Tutorial step side-effect coverage

- Added WPF shell coverage for the beginner tutorial path.
- The test now verifies that Sample loads an image, Label adds a box ROI, Infer enables current-image inference, Review selects the candidate tab, and Save writes a YOLO label file through the existing persistence path.
- The test uses a temporary sample image root and output root, so it does not depend on the user's real YOLO folder or write tutorial-test labels into the working dataset.
- Verified with:
  - `artifacts\logs\build-20260621-tutorial-side-effects-v2.log`
  - `artifacts\logs\tests-20260621-tutorial-side-effects-v2.log`

### 2026-06-21 - Direct HTML tutorial entry

- Added a direct `HTML 열기` button to the first-user tutorial card in the Guide tab.
- The WPF panel raises a shell-routable event; the shell resolves `docs\tutorial\labeling-workbench-tutorial.html` from the clone or execution folder and opens it with the default browser.
- The guide path text now uses ellipsis plus tooltip so it does not look clipped in the narrow right panel.
- Verified with:
  - `artifacts\logs\build-20260621-tutorial-open-html-v4.log`
  - `artifacts\logs\tests-20260621-tutorial-open-html-v3.log`
  - `artifacts\logs\visual-20260621-tutorial-open-html-guide-v3.log`
  - `artifacts\ui\wpf-tutorial-open-html-guide-v3.png`

### 2026-06-21 - Annotation tool capability gating

- Added `WpfAnnotationToolCapabilityService` so the education palette has one source of truth for connected vs pending annotation tools.
- The Guide tab now shows `가능` for Select, Box, Pan, and Delete, and `대기` for Ellipse, Polygon, Brush, Eraser, Undo, and Redo.
- Pending tools no longer fall through to the old broken-string switch branches. Selecting one leaves WPF drawing mode and reports the WPF path gap through status/log text.
- Extended the visual smoke harness with `--expand-learning-concepts`, so collapsed guide sections such as the tool palette can be captured directly.
- Verified with:
  - `artifacts\logs\build-20260621-annotation-tool-capabilities-v2.log`
  - `artifacts\logs\tests-20260621-annotation-tool-capabilities-v2.log`
  - `artifacts\logs\visual-20260621-annotation-tool-capabilities-v2.log`
  - `artifacts\ui\wpf-annotation-tool-capabilities-v2.png`

### 2026-06-21 - Annotation tool path validation

- Added `docs\WPF_ANNOTATION_TOOL_VALIDATION.md`.
- Verification criteria are now explicit: WPF input, visible WPF/OpenGL canvas result, object review state, persistence, and tests.
- Confirmed that WPF `CanvasInteractionMode.Drawing` currently creates rectangles through `RoiInteractionMouseUp.AddRectangleToOverlay`.
- Confirmed that circle/polygon/pen rendering primitives and legacy `CViewer` segmentation APIs exist, but they are not complete WPF labeling tool paths.
- Added a regression test that keeps pending tools pending until WPF input/review/save paths are actually verified.
- Verified with:
  - `artifacts\logs\build-20260621-annotation-tool-validation-v1.log`
  - `artifacts\logs\tests-20260621-annotation-tool-validation-v1.log`

### 2026-06-21 - Box labeling guide and canvas defaults

- Changed the Guide tab so `박스 라벨링` sends the learner to the real rectangle tool: the tool palette opens, the box tool is selected, and WPF drawing mode is active.
- Reordered secondary guide controls to show annotation tools before the extra lesson flow controls.
- Fixed guide scrolling so mouse wheel input over the mode/tool/flow chip lists still scrolls the parent guide panel.
- Hid debug-oriented canvas chrome by default in the WPF shell: `Module` group name, ROI item numbers, and the green group bounds.
- Replaced the ROI view model's 1 ms refresh timer with a 16 ms single-shot debounce timer.
- Verified with:
  - `artifacts\logs\build-20260621-box-label-ux-v2.log`
  - `artifacts\logs\tests-20260621-box-label-ux-v2.log`
  - `artifacts\logs\visual-20260621-box-label-ux-guide-v1.log`
  - `artifacts\ui\wpf-box-label-ux-guide-v1.png`

### 2026-06-21 - Annotation object verification process

- Fixed the active drawing-tool interaction rule: existing ROI hit-test wins over new drawing, so inside click selects and empty drag creates.
- Kept selected rectangle/ellipse handles visible after click so the user can tell which object is active.
- Removed the MouseDown `Reshape` timer request that made simple rectangle clicks heavier than needed.
- Added `docs\WPF_ANNOTATION_OBJECT_VERIFICATION.md` as the required create/select/move/resize/save verification gate for rectangle, ellipse/circle, polygon, brush, and eraser.
- Added `scripts\verify-wpf-annotation-objects.ps1` to run build, tests, and WPF visual smoke capture as one repeatable verification process.
- Added automated coverage for rectangle and ellipse/circle:
  - existing object inside click selects and does not create another ROI
  - empty-space drag creates exactly one ROI
  - ellipse/circle remains filled
  - hidden debug group frame does not break child ROI hit-test
- Verified with:
  - `artifacts\logs\build-20260621-annotation-object-hit-v2.log`
  - `artifacts\logs\tests-20260621-annotation-object-hit-v2.log`
  - `artifacts\logs\verify-wpf-annotation-objects-script-20260621-v1.log`
  - `artifacts\ui\verify-wpf-annotation-objects-20260621-223611.png`

### 2026-06-22 - Segment object edit and mask partial texture update

- Connected selected raster-mask movement through the WPF image-pixel select path.
- Connected selected polygon point movement through image-pixel hit-test and drag.
- Added image-point release events so brush strokes and selected-object drags commit history at the correct time.
- Selected polygon points now render with a larger visible marker instead of looking like a normal vertex.
- `RoiImageCanvasMaskOverlay` now carries dirty image-pixel bounds.
- Same-bounds raster mask changes use `glTexSubImage2D`; new textures, moved masks, resized bounds, or color/opacity changes still use the full upload fallback.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-segment-edit-v1.log`
  - `artifacts\logs\tests-20260622-wpf-segment-edit-v1.log`
  - `artifacts\logs\visual-20260622-wpf-segment-edit-v1.log`
  - `artifacts\ui\wpf-segment-edit-20260622.png`

### 2026-06-22 - Undo/Redo runtime state UX

- Kept the edit-history policy explicit: Undo/Redo restores in-memory WPF editing state; label files are rewritten only when the user saves.
- `WpfAnnotationToolItem` now owns runtime availability and display-state text in addition to static connected/pending capability.
- The Guide tab disables Undo when there is no undo stack and disables Redo when there is no redo stack.
- Undo/Redo badges now show `가능` or `없음`, and tooltips name the next edit action when available.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-undo-redo-runtime-state-v1.log`
  - `artifacts\logs\tests-20260622-wpf-undo-redo-runtime-state-v1.log`
  - `artifacts\logs\visual-20260622-wpf-undo-redo-runtime-state-v1.log`
  - `artifacts\ui\wpf-undo-redo-runtime-state-20260622.png`

### 2026-06-22 - Annotation dirty/saved status

- Added a bottom status-bar chip for annotation save state.
- Real annotation edits mark the current image as `라벨 저장 필요`.
- Successful save and image load return the status to `라벨 저장됨`/clean state.
- Undo/Redo also mark the current annotation state dirty because the in-memory edit state can differ from files on disk.
- AI candidate skip does not mark dirty because it changes review state, not saved labels.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-annotation-save-state-v2.log`
  - `artifacts\logs\tests-20260622-wpf-annotation-save-state-v2.log`
  - `artifacts\logs\visual-20260622-wpf-annotation-save-state-v2.log`
  - `artifacts\ui\wpf-annotation-save-state-20260622.png`

### 2026-06-22 - Status bar ViewModel binding

- Moved Dataset/Python/Model status text into `WpfStatusBarPanelViewModel`.
- `WpfStatusBarPanel.xaml` now binds the status texts instead of using fixed TextBlock values.
- The shell keeps the existing `SetDatasetStatus`, `SetPythonStatus`, and `SetModelStatus` call sites, but those methods now update the status ViewModel first.
- Tests now verify both XAML bindings and runtime ViewModel/TextBlock synchronization.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-statusbar-viewmodel-binding-v2.log`
  - `artifacts\logs\tests-20260622-wpf-statusbar-viewmodel-binding-v2.log`
  - `artifacts\logs\visual-20260622-wpf-statusbar-viewmodel-binding-v1.log`
  - `artifacts\ui\wpf-statusbar-viewmodel-binding-20260622.png`

### 2026-06-22 - Training status ViewModel binding

- Moved training readiness text, progress text, epoch text, progress value, indeterminate state, and status colors into `WpfTrainingSettingsPanelViewModel`.
- `WpfTrainingSettingsPanel.xaml` now binds the visible training status surface instead of relying on shell-owned TextBlock values.
- Training start/stop, readiness refresh, and worker progress updates now use shell helper methods that update the ViewModel first and only fall back to direct controls if the ViewModel is unavailable.
- The binding helper restores one-way bindings if a legacy path breaks them during transition work.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-training-status-viewmodel-v2.log`
  - `artifacts\logs\tests-20260622-wpf-training-status-viewmodel-v4.log`
  - `artifacts\logs\visual-20260622-wpf-training-status-viewmodel-v1.log`
  - `artifacts\ui\wpf-training-status-viewmodel-20260622.png`
  - `TestWpfTrainingSettingsPanelDeclaresControls`
  - `TestWpfSettingsViewModelsRoundTrip`
  - `TestWpfTrainingStatusSummariesAreOperatorReadable`
  - `TestWpfTrainingCommandDisablesConflictingActions`

### 2026-06-22 - YOLO status command availability binding

- Moved the YOLO status panel command availability for First Check, Install, Test, Restart, and Stop Worker into `WpfYoloStatusPanelViewModel`.
- `WpfYoloStatusPanel.xaml` now binds those buttons' `IsEnabled` values instead of relying on normal shell paths assigning controls directly.
- `UpdateYoloCommandButtons` keeps using `WpfWorkflowCommandStateService`, but now pushes the YOLO status-panel part through `YoloStatusViewModel.ApplyWorkflowCommandState`.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-yolo-status-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-yolo-status-command-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-yolo-status-command-vm-v1.log`
  - `artifacts\ui\wpf-yolo-status-command-vm-20260622.png`
  - `TestWpfYoloStatusPanelDeclaresCommandControls`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`

### 2026-06-22 - Project config command availability binding

- Moved the project config panel command availability for Apply, Refresh, Save Config, and Folder into `WpfProjectConfigPanelViewModel`.
- `WpfProjectConfigPanel.xaml` now binds those buttons' `IsEnabled` values instead of relying on normal shell paths assigning controls directly.
- `UpdateYoloCommandButtons` keeps using `WpfWorkflowCommandStateService`, but now pushes the project-config part through `ProjectConfigViewModel.ApplyWorkflowCommandState`.
- Save remains based on the currently applied recipe and busy state, not only text typed into the recipe editor.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-project-command-vm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-project-command-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-project-command-vm-v1.log`
  - `artifacts\ui\wpf-project-command-vm-20260622.png`
  - `TestWpfProjectConfigPanelDeclaresRecipeControls`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`

### 2026-06-22 - YOLO model settings command availability binding

- Moved the YOLO model settings command availability for five browse buttons, Save, and Reset into `WpfYoloModelSettingsPanelViewModel`.
- `WpfYoloModelSettingsPanel.xaml` now binds those buttons' `IsEnabled` values instead of relying on normal shell paths assigning controls directly.
- `UpdateYoloCommandButtons` keeps using `WpfWorkflowCommandStateService`, but now pushes the model-settings part through `YoloModelSettingsViewModel.ApplyWorkflowCommandState`.
- Browse/save/reset are disabled during inference, batch detection, training, and YOLO environment commands so paths cannot be changed mid-command.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-yolo-model-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-yolo-model-command-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-yolo-model-command-vm-v1.log`
  - `artifacts\ui\wpf-yolo-model-command-vm-20260622.png`
  - `TestWpfYoloModelSettingsPanelDeclaresPathEditors`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`

### 2026-06-22 - Training settings command availability binding

- Moved the training settings command availability for Refresh, Start, and Stop into `WpfTrainingSettingsPanelViewModel`.
- `WpfTrainingSettingsPanel.xaml` now binds those buttons' `IsEnabled` values instead of relying on normal shell paths assigning controls directly.
- `UpdateYoloCommandButtons` keeps using `WpfWorkflowCommandStateService`, but now pushes the training-settings part through `TrainingSettingsViewModel.ApplyWorkflowCommandState`.
- Start/refresh disable during a training command while Stop remains enabled when stop is available.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-training-command-vm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-training-command-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-training-command-vm-v1.log`
  - `artifacts\ui\wpf-training-command-vm-20260622.png`
  - `TestWpfTrainingSettingsPanelDeclaresControls`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`

### 2026-06-22 - Detection and queue command availability binding

- Moved the top-bar current-image detection command availability into `WpfLabelingShellViewModel`.
- Moved image-queue detection command availability into `WpfImageQueuePanelViewModel` for selected detect, batch detect, failed retry, and batch stop.
- `WpfLabelingShellWindow.xaml` and `WpfImageQueuePanel.xaml` now bind those buttons' `IsEnabled` values.
- `UpdateYoloCommandButtons` still builds one `WpfWorkflowCommandState`, but it now pushes the shell and queue parts through ViewModels instead of directly assigning the normal buttons.
- Verified labeling mode keeps inference locked, inference-review mode enables current/queue inference, and batch-running state enables only the batch stop command.
- Verified with:
  - `artifacts\logs\build-20260622-wpf-detection-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-detection-command-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-detection-command-vm-v1.log`
  - `artifacts\ui\wpf-detection-command-vm-20260622.png`
  - `TestWpfLabelingShellWindowConstructs`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfWorkflowModeSeparatesLabelingAndInference`
  - `TestWpfSettingsViewModelsRoundTrip`

## Self Evaluation

### 2026-06-21 - Education UX direction

- Product target is not only an inspection labeling utility. It should become an image-AI tutorial workbench for beginners learning labeling, object detection, segmentation, anomaly detection, YOLO, U-Net-style masks, and model inference.
- Current strength: WPF shell, image queue, sample inference, candidate review, class catalog, YOLO settings, label save paths, and the first education-mode/annotation-palette panel are now in place.
- Current UX rule: main workspace should stay focused on queue, canvas, active review, and core commands; detailed education/tool information belongs in a guide panel or task-specific view.
- Current gap: the first sample flow is wired to safe existing commands, but individual drawing tools beyond box/ROI still need verified viewer command paths.
- Current gap: detection, segmentation, and anomaly detection need distinct learning modes with sample images, expected outputs, and explanations embedded in the workflow instead of hidden in documentation.
- Current gap: drawing operations need more UX polish: visible active tool, cursor feedback, shape handles, undo/redo, brush size/opacity, class color preview, keyboard shortcuts, and confirmation states.
- Current gap: model families need a common teaching structure: what the model predicts, what label format it needs, how annotation quality affects training, how inference results differ from ground truth.

### 2026-06-21 - Performance self evaluation

- Completed: removed the obvious UI-thread stalls from queue click and pan paths.
- Completed: queue click is now semantically an image switch, not a status-save/logging operation.
- Completed: viewer pan no longer performs OpenGL color/gray readback on every mouse event.
- Completed: adjacent queue images now have a small decoded cache, so normal next/previous item switching can avoid repeated disk decode.
- Completed: canvas recovery commands are now visible next to the viewer instead of hidden in mouse/implicit behavior.
- Completed: candidate review can now move/focus through AI candidates without making the user hunt on the canvas.
- Completed: confirm/skip continues to the next candidate, which is the calmer review flow for YOLO object detection work.
- Completed: U-Net work is marked as deferred so the next development loop does not accidentally begin model integration before the Python-project decision.
- Completed: the Guide tab now starts with a compact YOLOv5 training checklist, so the first-time path is visible without asking users to infer it from scattered controls.
- Completed: the YOLOv5 checklist now moves the user to real WPF actions and updates dataset readiness in the same Guide tab.
- Completed: post-training review can pick up a newly produced `best.pt` candidate without asking the user to browse blindly.
- Completed: the training guide now remembers recent run events in a bounded list so repeated train/check/apply actions are visible without opening logs.
- Completed: the guide default view now avoids mixing the clickable YOLO training sequence with secondary lesson controls.
- Completed: inference progress has a global top-bar status chip instead of living only in the YOLO tab.
- Completed: left queue quick filters have enough width and height for Korean labels and icons.
- Completed: inference results now force the canvas back to a centered fit state after candidate overlays are applied.
- Completed: the top inference progress bar now uses a stable pulse instead of the default indeterminate animation.
- Completed: the Guide tab now has a first-user tutorial entry point and a matching HTML guide with current WPF screenshots.
- Completed: beginner tutorial steps now have direct side-effect coverage for image load, label creation, inference mode, review tab selection, and YOLO label save.
- Completed: the Guide tab can now open the HTML tutorial directly, and the narrow path display no longer appears clipped.
- Completed: annotation tools now show explicit connected/pending status, and pending tools are blocked from entering WPF drawing mode until their viewer path is verified.
- Completed: annotation tool verification is now documented and tested, so rendering primitives or legacy `CViewer` APIs cannot be mistaken for complete WPF tool support.
- Completed: ellipse/circle is now connected as a pixel-space ROI shape. It renders as an ellipse in OpenGL and exports to YOLO through its bounding box.
- Completed: the YOLO box-labeling guide row now opens the tool palette and selects the actual rectangle drawing tool.
- Completed: the WPF canvas no longer shows the debug `Module` group label/bounds by default, which makes the labeling surface read less like an internal overlay debugger.
- Completed: the ROI refresh timer is no longer a 1 ms loop; reshape refresh is now debounced at frame-rate scale.
- Completed: rectangle and ellipse/circle object hit behavior is now tested with the rule `inside click selects, empty drag creates`.
- Completed: rectangle and ellipse/circle move/resize now have coordinate-delta assertions through the real WPF mouse-event path.
- Completed: a repeatable annotation-object verification script now exists.
- Completed: polygon image-pixel draft/export service now creates segmentation objects and saves/loads through the existing segmentation annotation path.
- Completed: polygon is now connected to WPF image-pixel click input, OpenGL draft/confirmed overlay rendering, Object Review rows, class edit/delete, and segmentation save dictionaries.
- Completed: polygon connection was verified with build, full tests, object-tab visual smoke, and guide-tab visual smoke.
- Completed: brush and eraser are now connected as WPF image-pixel raster-mask tools instead of legacy `CViewer` shortcuts.
- Completed: mask paint/erase uses a source-image-size buffer, drag interpolation, bounds updates, Object Review `Mask` rows, segmentation save dictionaries, and automated shell/service tests.
- Completed: Undo and Redo are now connected through WPF-owned annotation history instead of legacy `CViewer` shortcuts.
- Completed: WPF annotation history snapshots and restores ROI, polygon/mask segments, pending AI candidates, confirmed AI candidates, class changes, delete operations, and basic Ctrl+Z/Ctrl+Y shortcuts.
- Completed: Undo/Redo runtime availability is visible in the Guide tool palette, and empty history commands are disabled instead of looking clickable.
- Completed: Undo/Redo persistence policy is settled for now: restore WPF edit state in memory, then write label files only on explicit save.
- Completed: the bottom status bar now shows whether the current annotation edit state is saved or needs saving, including Undo/Redo after a save.
- Completed: Dataset/Python/Model status text in the bottom status bar now flows through `WpfStatusBarPanelViewModel` bindings instead of fixed TextBlock text.
- Completed: training readiness/progress/epoch status now flows through `WpfTrainingSettingsPanelViewModel` bindings instead of normal shell paths writing directly into training TextBlocks.
- Completed: YOLO status-panel command availability now flows through `WpfYoloStatusPanelViewModel` bindings for first check, install, test, restart, and stop-worker commands.
- Completed: project config command availability now flows through `WpfProjectConfigPanelViewModel` bindings for apply, refresh, save, and folder commands.
- Completed: YOLO model settings command availability now flows through `WpfYoloModelSettingsPanelViewModel` bindings for browse, save, and reset commands.
- Completed: training settings command availability now flows through `WpfTrainingSettingsPanelViewModel` bindings for refresh, start, and stop commands.
- Completed: current-image inference and image-queue inference command availability now flow through `WpfLabelingShellViewModel` and `WpfImageQueuePanelViewModel` bindings.
- Completed: canvas helper command availability now flows through `WpfCanvasPanelViewModel` bindings for Fit, 1:1, Pan, Focus, and AI Reset.
- Completed: YOLO guide fix command availability now flows through `WpfLearningWorkflowPanelViewModel` bindings for class registration, label start, and dataset check.
- Completed: image queue selected-open command availability now flows through `WpfImageQueuePanelViewModel` bindings.
- Completed: top workflow mode button active/enabled state now flows through `WpfLabelingShellViewModel`; the shell no longer directly paints or enables the Labeling/Inference mode buttons.
- Completed: candidate comparison card visibility/text/high-overlap state now flows through `WpfCandidateReviewPanelViewModel`; the shell no longer directly writes the candidate comparison TextBlocks or border colors.
- Completed: canvas AI detection result overlay visibility/text/status styling now flows through `WpfCanvasPanelViewModel`; the shell no longer directly writes or paints that overlay.
- Completed: image queue quick filter count text and active styling now flow through `WpfImageQueuePanelViewModel`; the shell no longer directly writes the filter TextBlocks or paints quick-filter buttons.
- Completed: Object Review class selection now syncs through `WpfObjectReviewPanelViewModel.SetSelectedObjectClass`; the class ComboBox no longer uses a shell `SelectionChanged` event to refresh apply state.
- Completed: Candidate Review selection detail and comparison-card state now update together through `WpfCandidateReviewPanelViewModel.ApplySelectionReview`; selected candidate lookup no longer falls back to `CandidateListBox.SelectedItem`.
- Completed: Candidate Review previous/next/focus enabled state now has dedicated `WpfCandidateReviewPanelViewModel` properties instead of reusing skip-state.
- Completed: WPF image load now transfers the decoded Mat to `CDisplayManager.ImageSrc` without cloning after OpenGL upload, and adjacent decode cache now has a 64 MB total memory budget.
- Completed: OpenGL texture upload now skips `Mat.Clone()` for continuous Mats and keeps compact clones only for non-continuous `SubMat` buffers that need stride-safe upload.
- Completed: WPF queue-click performance smoke can now run against a real image folder and reports working set plus decode-cache hit/miss/store/eviction diagnostics.
- Completed: WPF queue-click performance smoke now reports the first cold switch separately from warm repeated switches.
- Completed: WPF queue-click performance smoke now reports image-load step timings, image-commit timing, and dispatcher-pump settling time separately.
- Completed: WPF queue-click performance smoke now separates visible image switching from full dispatcher settled timing.
- Completed: WPF raster-mask preview now uses a dedicated OpenGL texture overlay instead of converting masks into coarse polygon regions.
- Completed: WPF brush/eraser tools now show an image-pixel OpenGL cursor radius preview before painting or erasing.
- Completed: WPF raster-mask rows selected in Object Review now draw a topmost canvas marker, keep the marker number aligned with the review row, and clear stale ROI handles for non-ROI selections.
- Completed: selected raster masks can now move through image-pixel drag, with one history commit on release.
- Completed: selected polygon points can now move through image-pixel drag and keep visible selected-point feedback.
- Completed: raster mask texture updates now use dirty-bounds `glTexSubImage2D` when the texture bounds are unchanged.
- Remaining risk: exact 50 ms image-switch timing depends on real UI/OpenGL handle state. The latest local Debug smoke was 55.2 ms average including the first cold switch, and about 48.8 ms average after excluding that cold switch.
- Remaining note: the latest real-folder smoke on `C:\Git\yolov5\data\train\images` loaded 24 images with 23 switches at 39.2 ms average, but the first switch still spiked to 131.0 ms. Treat cold and warm switch timings separately.
- Remaining note: after splitting the metrics, the same real-folder smoke shows warm repeated switches at 34.5 ms average and 49.7 ms max. The cold first switch is still 124.1 ms and should be profiled separately.
- Remaining note: after step timing, image commit is fast: first 30.6 ms, warm average 14.5 ms, warm max 22.6 ms. The larger settled timing is mostly dispatcher pump/render/layout, not image decode/OpenGL upload.
- Remaining note: after visible/settled split, real-folder visible switching is first 44.4 ms, warm average 24.2 ms, warm max 36.1 ms. Settled timing is larger because Background-priority drain can still run after the visible image update.
- Remaining risk: strict max-under-50 ms for every switch still requires real-folder tuning. The cache is bounded, but capacity and memory policy should be adjusted against large real datasets.
- Remaining risk: very large ROI counts may need a cached spatial index for visible overlay calculation, but that should wait until real data proves it is needed.
- Remaining risk: if a future real inference path does heavy synchronous work on the UI thread, any top-bar animation can still pause. Profile request send/wait timing if the pulse still stalls in a real run.
- Remaining risk: if rectangle dragging still stutters on a real dataset, measure visible-overlay recalculation and object review refresh separately before changing OpenGL drawing primitives.
- Remaining risk: `CanvasImageLoader.UploadMatAsTexture(...)` still clones non-continuous tile/SubMat buffers by design. Remove that only after proving a row-stride OpenGL upload path with focused pixel/stride tests and visual smoke.
- Remaining risk: polygon point move is implemented. Point delete/add-on-edge should wait until the tutorial workflow needs it.
- Remaining risk: Undo/Redo saves remain explicit by design. The dirty/saved chip now makes this visible, but real labeling sessions should confirm the wording is obvious enough.
- Remaining risk: brush/eraser basic paint and erase are connected, texture preview, cursor radius preview, mask selection highlight, mask move, and partial texture update are in place. Mask reshape/detail editing should wait until real tutorial use proves which operation is actually needed.

### 2026-06-22 - 1~5 verification loop self evaluation

- Completed: a realistic WPF labeling-session smoke now covers the learner path from image load through box, filled ellipse/circle, polygon, brush mask, eraser, save, duplicate AI skip, and selected AI confirm.
- Completed: the left image queue quick filters were resized/reflowed so icon and Korean text no longer clip in the checked viewport.
- Completed: the Guide default view now separates the clickable YOLOv5 training flow from the long detailed checklist, which is collapsed by default.
- Completed: Candidate Review now exposes a selected-candidate summary before confirm/skip actions, so the current review target is visible without reading only the list row.
- Completed: direct visual smokes were captured for the labeling session, Guide flow, Candidate Review summary, and annotation object verification.
- Completed: real-folder queue switching was measured again on `C:\Git\yolov5\data\train\images` with 125 images. Visible switching averaged 19.5 ms, max 26.5 ms; image commit averaged 14.2 ms.
- Completed: the repeatable verification record lives in `docs\WPF_LABELING_SESSION_VERIFICATION_20260622.md`.
- Remaining note: full dispatcher idle settle still averaged 62.4 ms in the latest real-folder run. This is not the visible image switch, but it should be watched during rapid scroll/click sessions.
- Remaining note: YOLO training UX still needs a real training-session pass. Dataset readiness and review flow are present; training-run comparison/export should wait for evidence from use.

### 2026-06-22 - YOLO training-session priority pass self evaluation

- Completed: the WPF YOLO training session now has a deterministic app-level smoke covering train/valid label save, dataset readiness, `StartTraining` TCP send, worker completion status, latest `best.pt` application, and candidate-review transition.
- Completed: `TrainYoloResult` and `TaskStatus` messages from the current Python worker are normalized into the existing C# training-status path.
- Completed: the training panel polls worker status after Start, so completion is visible without a manual refresh.
- Completed: Candidate Review now states that confirm/skip moves to the next candidate. That policy remains the default because it keeps review momentum for object-detection candidate triage.
- Completed: the Guide's deeper concept area is labeled `심화 개념`, keeping the beginner YOLOv5 path separate from secondary controls.
- Completed: the HTML tutorial screenshots were regenerated sequentially, not in parallel, so WPF windows do not overlap each other during capture.
- Completed: real 125-image queue switching stayed under the visible 50 ms target: average 21.4 ms, max 42.8 ms.
- Completed: generated 240-image queue switching also stayed under the visible 50 ms target: average 18.8 ms, max 33.1 ms.
- Remaining note: this training check uses a mock worker for deterministic UI/protocol verification. A long real YOLOv5 training run should be done once there are enough meaningful labels.
- Remaining note: full dispatcher settle can still go beyond 50 ms. Treat visible image switching as the user-facing metric, and only optimize background drain if rapid scroll/click use shows a real pause.

## Next Queue

- Keep viewer/OpenGL/ImageCanvas code out of broad WPF migration cleanup scope; only touch it for explicit viewer correctness/performance issues.
- Keep the main workspace sparse. Add new education/model details to task-specific tabs or dialogs, not the always-visible top area.
- Continue connecting annotation palette actions incrementally only for new tools. Box, Ellipse/Circle, Polygon, Brush, Eraser, Pan, Delete, Undo, and Redo now have verified first WPF paths.
- For polygon, point move is now connected. Only add point delete/add-on-edge after real use proves it is needed.
- Use `scripts\verify-wpf-annotation-objects.ps1` before reporting annotation tool work complete.
- Defer U-Net runtime integration. Keep only generic segmentation/annotation UI until the separate Python project direction is decided.
- Add training-run detail, compare, delete, or export only after real use shows the compact recent list is not enough.
- Undo/Redo empty-stack state is now visible in the Guide tool palette. Decide later whether separate top-toolbar buttons are worth the extra chrome.
- Keep the new dirty/saved status chip stable during real labeling. Tune wording/color only if users miss it.
- Keep the WPF mask texture, cursor preview, selected-mask marker, selected-mask move, and dirty-rectangle `TexSubImage2D` paths stable during real brush/eraser use. Only add mask reshape/detail editing if real use proves it is needed.
- Measure queue item click and pan responsiveness with the WPF visual smoke harness after each viewer-related change.
- Refresh `docs\tutorial\images` sequentially whenever the right Guide tab, class tab, YOLO tab, or candidate review layout changes materially.
- Tune the adjacent-image decode cache against larger real folders: capacity, max pixel count, eviction timing, and memory pressure behavior.
- Track real working set while switching large folders. The cache now has a 64 MB budget, but the right value should be tuned against real image sizes and user-visible switch latency.
- Track OpenGL upload memory on large tiled images. Continuous full-image uploads now skip clone; non-continuous tile clones remain for stride safety unless measured data proves they are the next bottleneck.
- Check whether Background-priority dispatcher drain affects rapid repeated queue clicking or scroll feel. Do not optimize image decode/OpenGL upload further unless a larger real dataset shows visible switching above 50 ms.
- Tune the compact viewer command strip after real use: button density and icon/label balance. The top Labeling/Inference mode active and disabled states now use ViewModel bindings.
- Watch the global inference status chip during real long-running inference and tune color/text only if it is still too subtle.
- Verify post-inference center fit on large images and unusual monitor ratios after the next real dataset run.
- Keep Candidate Review's current post-action policy: confirm/skip moves to the next candidate. Revisit only if real reviewers find it disorienting.
- Run a long real YOLOv5 training session after enough actual labels exist; use it to decide whether training-run comparison/export needs a UI surface.
- Move remaining candidate summary/overlay text helpers into the presenter after the review-panel cleanup is stable.
- Continue moving the remaining command-state UI application into focused WPF ViewModels once the shell command rules settle. Detection/queue, selected-open, canvas helper, YOLO guide fix, top workflow mode button states, candidate comparison card state, canvas detection result overlay state, image queue quick filter active styling, Object Review class editor sync, Candidate Review selection detail/comparison sync, and candidate navigation enabled state are done; next focus on Object/Candidate Review row presentation helpers outside the viewer project.
- Continue replacing direct shell `TextBlock.Text`, `Items.Add`, and `SelectedItem` updates with WPF ViewModels/services in small verified loops.
- Keep every loop paired with build, tests, visual smoke when UI changes, and this progress document update.
