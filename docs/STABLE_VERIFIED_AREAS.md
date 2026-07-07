# Stable Verified Areas

This document records code paths that have already been performance- or UX-verified and should not be casually refactored. Treat these areas as protected product behavior: change them only when the user reports a new issue in that exact path, or when a focused verification gate proves the change is necessary.

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
- Promotion must stay `hold` when the candidate produces zero UI-threshold candidates at the configured UI confidence, even if precision and mAP improve.
- The WPF Candidate Review model-comparison detail must surface the promotion recommendation from `comparison-summary.json`.
- The WPF Candidate Review model-comparison detail must not expose raw script promotion reasons such as `Candidate precision`; low-precision hold reasons should be translated into operator-facing Korean while preserving the evidence values.
- The WPF Candidate Review model-comparison detail should translate weak held-out evidence and zero UI-threshold candidate hold reasons into operator-facing Korean while preserving counts/confidence values.

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
- The expanded right workflow panel title follows the selected right-side view. In labeling stage, saved labels must show `저장 라벨`, guide/tools must show `가이드/도구`, and class management must show `클래스`; it must not keep the broader `데이터셋 홈` title.
- The collapsed right workflow rail must keep a compact context badge that identifies the area as the current work panel and shows the active local view (`라벨`, `도구`, `클래스`). Do not leave the collapsed rail as only anonymous buttons.
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
2026-07-02 image queue current-task summary contract: the left queue must show a compact selected-image task card above the filters so users can see whether the current image needs labeling, AI-candidate review, failed-inspection handling, or is already saved. The text and state key are computed by `WpfImageQueuePanelViewModel` from `SelectedQueueItem` and selected-row property changes; XAML should remain binding-only and must not move this state into panel code-behind. Covered by `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-canvas-panel-commands`, 1920/1366 responsive-layout checks, and visual captures `artifacts\ui\wpf-current-task-clarity-after-1920.png`, `artifacts\ui\wpf-current-task-clarity-after-1366.png`.
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

2026-07-01 right-workflow rail label check: the collapsed labeling rail is intentionally an icon+short-label dock (`열기`, `라벨`, `도구`, `클래스`, `AI`, `모델`) rather than a 48px icon-only strip, so first-time users can identify the on-demand panels. Verified with `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and `--wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080`. Before/after captures: `artifacts\ui\wpf-labeling-right-dock-before-1920.png`, `artifacts\ui\wpf-labeling-right-dock-after-1920.png`.

2026-07-01 right-workflow local rail contract: in the labeling stage, the collapsed right rail is local to the labeling workbench. It must expose only the open/saved-labels/tools/classes panel buttons (`열기`, `라벨`, `도구`, `클래스`). Do not add inference-review or model-center stage navigation back to this rail; those cross-stage actions belong to the top workflow rail. Covered by `--wpf-labeling-shell`, `--mvvm-infra`, 1920/1366 responsive-layout checks, and the 1920 visual smoke capture `artifacts\ui\wpf-labeling-local-right-rail-after-1920.png`.

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

2026-07-03 dataset quality audit Markdown export contract: `YoloDatasetQualityAuditExportService` should remain a non-UI export service that takes an existing `YoloDatasetQualityAuditReport` and writes a standalone Markdown report with total images, label files, missing labels, empty labels, invalid label lines, objects, a split table, and a class distribution table. This is not a WPF save action yet. Do not change annotation save/reopen, dataset split assignment, training execution, model runtime, or Viewer/OpenGL/ROI/brush/eraser paths for audit export work. Covered by isolated test build and `--dataset-quality-audit-export`.

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

2026-07-07 class setup left-panel compact layout contract: the WPF Class Catalog panel should keep guide title, class count/selection summary, current drawing class title, class name input, add/rename/delete controls, color expander, edit status, and recipe class list visible while hiding repeated explanatory detail text by default. Class guide detail, current drawing class detail, and class next-action detail stay bound and named for accessibility/diagnostics but are collapsed in the default layout. This is WPF layout/presentation only and does not change class add/rename/delete behavior, class color application, label saving, training, inference, Viewer/OpenGL/ROI/brush/eraser paths, or model comparison logic. Covered by isolated test build, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--wpf-responsive-layout --width 1920 --height 1080`, 1920x1080 before/after visual smoke captures under `artifacts\ui`, and `git diff --check`.

## Refactor Rule

When working near a protected path, prefer adding a small adapter or a new higher-level service instead of rewriting the verified hot path. If the hot path must change, document the reason in the final response and include the focused gate results.

Do not run real-EXE UIAutomation smokes in parallel. They share desktop focus and can produce false failures when two EXE windows compete for foreground input.
