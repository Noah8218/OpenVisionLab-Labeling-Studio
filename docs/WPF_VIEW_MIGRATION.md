# WPF View Migration

The labeling tool should move from WinForms to WPF completely. The work can take time, but the target is not a hybrid product. Hybrid hosting is only a way to keep the app usable while each screen is moved.

## Target

- WPF owns the main shell, title/status bars, workspace layout, image queue, object review, training controls, model settings, and dialogs.
- C# services keep owning labels, classes, image state, dataset writing, review state, and Python process/client lifecycle.
- Python keeps owning YOLO training, inference, weights, and runtime dependencies.
- OpenGL/ImageCanvas is excluded from the remaining app-shell WPF cleanup because it will be split into a separate project. The labeling shell should keep treating it as a viewer boundary.
- `WPF-UI` is the WPF shell's Fluent theme and control baseline. `MahApps.Metro.IconPacks.Material` remains a temporary icon layer for icons while the remaining panels and dialogs are moved across.

## Current Start

- `WpfLabelingShellWindow` is now the default app shell.
- Run `OpenVisionLab.LabelingStudio.exe` normally to open the WPF shell.
- `FormMainFrame`, `FormTeachingVision`, `FormImageList`, `FormClassList`, `FormTrainingPanel`, `FormDetectionReviewPanel`, `FormLog`, `FormLayerDisplay`, `FormVision_Yolov5ParamSetting`, `FormVision_ClassMenu`, and `FormVision_NewPanel` have been removed.
- `CDisplayManager` now stores `DisplayLayerDocument` state instead of owning WinForms document windows.
- The WPF shell now resolves the configured YOLO sample image on startup, loads it into the WPF ROI canvas, fills the WPF image queue, and keeps `CGlobal` image state in sync.
- Startup loading only builds image queue shells and refreshes the active image status; full queue status scanning is reserved for explicit Root, Folder, or Refresh actions.
- The default workflow mode is label edit. Image selection never runs YOLO inference, and inspection commands stay disabled until the operator explicitly switches to AI detection mode.
- Image queue single-click now opens that image directly in the Main canvas. The old top preview area has been removed, and the selected row uses a stronger left accent and selected background.
- Pending AI candidates are rendered as read-only detection overlays, not editable ROI windows, so inference results remain visually separate from labeling objects.
- The canvas shows an `AI 검출 결과` summary panel after inspection, including active image, candidate count, confidence threshold, selected candidate, and compact candidate lines.
- The inference summary panel is placed in a WPF row above the OpenGL canvas rather than overlaid on top of it, avoiding Win32/OpenGL airspace clipping.
- Selecting a candidate in the review list highlights the matching canvas overlay with stronger color and stroke weight so the list and image stay connected.
- Detection markers now use corner brackets, a full label badge only for the selected candidate, and compact number chips for non-selected candidates to reduce canvas clutter.
- Detection overlay drawing now runs in an OpenGL screen-space pass instead of the image-coordinate pass, so zoomed images do not scale the candidate lines, labels, or badge backgrounds into chunky ROI-looking shapes.
- Detection overlay positions are mapped through the active OpenGL texture bounds before converting to screen coordinates, so zoom/pan keeps candidates anchored to the same image pixels.
- Detection overlay selection uses corner brackets and a compact badge without a full rectangle outline, keeping AI candidates visually separate from editable manual ROIs.
- The selected detection badge is now a dark translucent result chip with a class-color strip, while non-selected detections use compact number chips. This keeps the canvas readable without making AI candidates look like manually drawn ROIs.
- A regression test now checks the screen-space detection bounds calculation directly, including a zoomed view state.
- OpenGL refresh/reshape/texture updates marshal through the actual `openGLControl` thread, avoiding WPF/WinFormsHost cross-thread exceptions when detection results arrive from worker callbacks.
- Single-image WPF canvas loads clear previous OpenGL texture groups before uploading the next image, so old image textures do not remain stacked in the viewer.
- The left image queue is now a WPF `DataGrid` with configured-root load, folder load, refresh, next-unlabeled navigation, review-state filtering, file-name search, async label/status refresh, selected-image loading, selected-image detection, visible-row batch detection, failed-row retry, stop, and progress feedback.
- The WPF shell has working commands for sample reload, center ROI insertion, manual YOLO label save, teaching mode toggle, YOLO settings check, YOLO smoke inference, candidate display, selected candidate detail, selected/all candidate confirmation, and candidate skip.
- The WPF `Classes` tab can add and delete classes through the existing `ClassCatalogService`, then saves the same YOLO data yaml and recipe config used by the removed legacy class menu.
- The WPF log area uses `OpenVisionLab.Logging.Controls.View.LogPanelView` from the existing logging DLL instead of a local text log box.
- The WPF image folder picker now uses `Microsoft.Win32.OpenFolderDialog` instead of `System.Windows.Forms.FolderBrowserDialog`.
- The WPF `YOLO > 모델 설정` panel replaces the old YOLO parameter dialog and has browse buttons for Python executable, YOLO project folder, client script, weights, and image root, with full-path tooltips on long path fields.
- The WPF YOLO model and training settings panels now bind their editable fields to ViewModels. The shell calls `LoadFrom` and `ApplyTo` rather than reading every input field directly.
- The WPF project config panel now binds recipe name, recipe list, selected recipe, config path, and status text to `WpfProjectConfigPanelViewModel`; recipe listing and path calculation are isolated in `WpfProjectRecipeService`.
- Common app message boxes now use WPF `MessageBox` instead of `OpenVisionLab.MessageBox`, and startup no longer loads RJ WinForms appearance settings.
- `RJControls`, `OpenVisionLab.MessageBox`, `OpenVisionLab.Controls.Init`, and `FontAwesome.Sharp` were removed from the app project and solution. The viewer context menu now uses a standard temporary `ContextMenuStrip` while the OpenGL bridge still exists.
- Recipe/config/data path resolution now uses `AppContext.BaseDirectory` instead of WinForms `Application.StartupPath`.
- `CViewer.Designer.cs` and `CViewer.resx` have been removed. `CViewer` builds its small temporary context menus in code, no longer uses a WinForms component timer, and keeps cursor mapping inside the viewer instead of inside ROI draw objects.
- OpenGL overlay label textures now use GDI+ text drawing instead of WinForms `TextRenderer`.
- The WPF YOLO and training numeric fields guard integer/decimal input and show short range tooltips, with a construction-time XAML test covering the bindings.
- The right `YOLO` tab now shows the active Python executable, YOLO project, client script, weights, image root, requirements file, confidence, timeout, validation result, missing package summary, worker connection state, model state, and training status when the worker reports it.
- The same `YOLO` tab can run first check, edit/save model paths and detection settings, install missing requirements, run the smoke test, restart the Python worker, stop the Python worker, edit basic training parameters, check dataset readiness, and send training start/stop.
- The YOLO tab smoke `Test` remains available as an explicit operator action in label edit mode; pressing it switches to AI detection mode before running the check, while current-image and queue inspection buttons still require AI detection mode.
- The training panel now separates dataset readiness from live worker progress, showing a compact progress summary plus epoch text when the worker reports training status.
- WPF command buttons now treat training as a busy state, preventing duplicate YOLO/detection/training commands while keeping StopTraining available only during active training.
- The WPF candidate panel has a confidence slider and keyboard review shortcuts: Enter confirms the selected candidate, Delete/Backspace skips it, and Ctrl+A confirms the visible filtered candidates.
- The WPF `객체` tab now shows current object counts, readable manual ROI bounds, empty state text, 출처/클래스/좌표 tooltips, guarded Delete/Delete-key actions, and selected-object class changes for current objects.
- Candidate rows use status icons, 신뢰도, compact `width x height @ x,y` bounds, and confirm/review state text; the selected candidate detail separates 신뢰도, threshold, bounds, status, and current-label overlap so operators can decide whether to confirm without parsing raw coordinates.
- The `후보` tab now has a side-by-side comparison panel for the selected AI candidate and the closest current label, including bounds and IoU. High-overlap candidates are flagged with warning states in both the row and comparison panel, and the panel was verified by visual smoke after a first pass revealed row overlap with the candidate list.
- Review lists now share an explicit selected-row style so candidates, objects, and classes remain readable in both dark and light themes. The object tab restores the previous object selection and selects the first object when no previous selection exists.
- Candidate action buttons now read `확정`, `전체 확정`, and `스킵`, matching the operator action more directly than the previous shorter labels.
- WPF batch detection now uses the reusable TCP Python worker path only for normal operator work; worker failures become failed rows instead of launching the slower smoke-process fallback repeatedly.
- WPF single-image and selected-image detection also use the TCP Python worker path for normal operator work. The slower smoke-process fallback is kept only behind the YOLO test/diagnostic button.
- The WPF shell no longer starts Python worker/model warm-up after launch. It loads a sample image for context, then waits for an explicit inference command before worker/model work begins.
- The top toolbar separates basic image actions, workflow modes, and YOLO/inference commands with thin dividers. Mode labels now read `라벨링` and `추론 검토`, while the current-image command reads `현재 추론`.
- Single-image inference logs elapsed time and uses a bounded interactive worker wait: already-connected workers get a short check, while first-start connections can wait up to 5-12 seconds before reporting worker failure directly.
- Real YOLO TCP smoke resolves an existing sample image from the configured image root instead of assuming `Teaching_0.bmp`, so the checked-in JPEG sample can drive end-to-end inference verification.
- Visual smoke is run with zoom steps against the sample image to verify that detection overlays stay anchored and readable after OpenGL scaling.
- The WPF toolbar and image queue now use Material-style icons with text on primary actions and compact icon buttons in the narrow image queue.
- The WPF image queue rows now show status icons, compact status summaries, short failure reasons, and full detail tooltips so operators can scan large batches quickly.
- The dataset status bar now includes non-zero candidate, failed, confirmed, skipped, and no-candidate counts so batch results are visible even before changing the queue filter.
- The image queue now exposes quick filters for all, candidate, failed, and confirmed rows next to the existing status filter, with compact candidate/failed/confirmed counts so batch review does not require opening the combo box for the most common states.
- A 1100x720 visual smoke pass found that the top toolbar clipped the rightmost candidate action buttons. Candidate confirm/confirm-all/skip actions now live inside the Candidates comparison panel, keeping the top bar focused on workflow and inference commands.
- Candidate action buttons now enable whenever visible candidates exist, so an operator can confirm or skip directly from the review panel without first toggling modes.
- The WPF image queue item/filter/detail models have been split into `WpfImageQueueModels.cs`; the shell still owns queue commands until the next UserControl extraction.
- The WPF shell references `WPF-UI 4.3.0`, loads its theme/control resources, uses `Wpf.Ui.Controls.FluentWindow`, and uses WPF-UI buttons with shell-owned dark/light contrast for the top workflow toolbar, image queue compact actions, YOLO commands, model settings commands, and training commands. Primary action buttons have short tooltips so operators can confirm intent without extra visible instruction text.
- Visual smoke supports `--theme=light`; light-theme capture is used to verify that mode buttons, queue filters, and candidate comparison panels keep enough contrast after theme changes.
- Disabled WPF inference buttons still show tooltips, and the tooltip text changes from “AI detection mode required” guidance to the concrete action after the operator switches to AI detection mode.
- Detection overlay badges are placed in screen coordinates and avoid already-used badge areas, while the actual detection box remains anchored to image coordinates. The candidate corners and badge borders are drawn as pixel-snapped screen-space strips for cleaner OpenGL zoom rendering.
- Image queue selection is the normal canvas-open action; the old preview cache path is no longer part of the compiled WPF workflow.
- The WPF learning workflow panel now lives in the right-side Guide tab with beginner modes for labeling, object detection, segmentation, anomaly detection, training, inference, and review.
- The same panel owns the first annotation tool palette state for select, box, ellipse/circle, polygon, brush, eraser, pan/zoom, undo, redo, and delete.
- The sample learning steps are clickable and route to existing safe shell actions for Sample, Label, Infer, Review, and Save.
- Ground-truth label and AI prediction states are now visible as separate chips in the shell, keeping human annotations distinct from model results.
- The main workspace stays focused on queue, canvas, active review, logs, and core commands instead of always showing every education/tool option.
- Visual smoke supports `--review-tab=training`, and the WPF training expander now uses Korean labels for the main readiness/start/stop flow.
- The WPF YOLO tab uses Korean primary command labels for first check, package install, test inference, worker restart/stop, model settings save/reset, and command status.
- Settings ComboBoxes use the shell input colors instead of the default WPF chrome, so model/training selectors no longer flash light controls in the dark theme.
- The image queue table uses shell-owned dark/light row, header, hover, and selected-state colors instead of the default WPF table chrome.
- Image queue selection now uses a lightweight canvas-switch path: no queue rebuild, no detail reload, no review-status save, and no log append for ordinary item clicks.
- Small same-size queue images reuse the existing OpenGL texture instead of deleting and recreating it on every click.
- Viewer pan now avoids per-event OpenGL pixel readback and throttles visible-overlay recalculation; future viewer changes should preserve this responsiveness rule.
- Confirmed WPF candidates and manual ROIs reuse the existing YOLO annotation save path, so label txt/image output is written by the same service as the legacy workflow.
- The unused reverse `WpfRoiCanvasHost` bridge has been removed. `WpfCanvasPanel` hosts `RoiImageCanvasView` directly in WPF.
- The WPF class catalog now binds class input, selected class, class list, output-root path, and status text through `WpfClassCatalogPanelViewModel`; the shell only calls catalog services and saves.
- The WPF YOLO status panel now binds settings summary/detail, command text, and command progress through `WpfYoloStatusPanelViewModel`; command progress is no longer updated directly on the normal WPF path.
- The WPF object review panel now binds object summary, object rows, selected object, class options, selected class, and action enabled state through `WpfObjectReviewPanelViewModel`.
- Object review summary text, empty text, manual ROI row/tooltip text, confirmed-AI row text, and row construction are now centralized in `WpfObjectReviewPresenter`.
- Object review class lookup, class application, manual ROI deletion, and confirmed-AI candidate deletion are now centralized in `WpfObjectReviewEditService`.
- The WPF candidate review panel now binds confidence text, detail text, candidate rows, selected candidate, action enabled state, and action tooltips through `WpfCandidateReviewPanelViewModel`.
- Candidate review row text, detail text, comparison text, icon/brush state, confirmability, duplicate-overlap policy, and disabled-action hints are now centralized in `WpfCandidateReviewPresenter`.
- Candidate Review and Object Review now use ViewModel-only normal paths; their old direct `ListBoxItem` row creation and direct action button fallback updates have been removed from the shell.
- Object Review selected-object lookup now uses `WpfObjectReviewPanelViewModel.SelectedObject` only; the remaining direct ListBox selected-item fallback has been removed.
- Class Catalog now uses a ViewModel-only normal path for class list population, selected class lookup, output-root edits, and status text; the old direct list-box selected-item fallback has been removed from the shell.
- Workflow command availability and detection command tooltips are now calculated by `WpfWorkflowCommandStateService`; the shell applies the state to controls instead of owning the command rules inline.

## Migration Order

1. WPF shell scaffold
   - Match the real workbench shape: image queue, center canvas, object review, log/status area, and workflow commands.
   - Keep the old shell available only for comparison while this is incomplete.

2. Canvas parity
   - Verify zoom, pan, rectangle drawing, segmentation display, delete/copy/paste, candidate overlays, confirmation, skip, and YOLO label save.
   - Only then switch the normal launch path to the WPF canvas.

3. Image queue
   - Image browsing, filters, selected-image status, root refresh, next-unlabeled navigation, selected detection, visible-row batch detection, failed-row retry, stop, and progress are now in the WPF shell.
   - Next work is mostly polish: quick result filters, faster worker reuse, and moving the queue into a small WPF UserControl once the shell settles.
   - Keep queue state in services, not in visual controls.

4. Object review
   - AI candidates, selected candidate details, selected/all confirm, skip, confidence filtering, and keyboard review are now in WPF.
   - Current-label delete and class changes are now in WPF, backed by the same YOLO annotation save path.
   - Side-by-side current label vs AI candidate comparison is now in the Candidates tab.
   - Current-object class synchronization and apply/delete coordination now live in `WpfObjectReviewEditService`; candidate decision history, duplicate-confirm prevention polish, and remaining selection fallback cleanup are next.
   - Keep detection result application in services.

5. Training and first-run tools
   - Python path checks, package readiness, package install, sample smoke inference, worker restart/stop, worker/training status display, basic training parameter editing, dataset readiness, and training start/stop commands are now visible from the WPF shell.
   - Move the remaining detailed training option dialogs and broader shell command state into WPF ViewModels/services next.
   - Keep the existing first-run checks and smoke scripts as the truth source.

6. Dialogs and settings
   - Class settings, model settings, output path, and recipe/config editing are now in WPF panels. New confirmation/settings popups should be WPF windows.
   - Keep local-only runtime paths out of committed files.

7. Retire WinForms
   - The legacy main shell, teaching view, image queue, class list, training panel, detection-review panel, log panel, layer display form, YOLO settings dialog, class settings dialog, and new-panel dialog have been removed.
   - DockPanelSuite, `RJControls`, `OpenVisionLab.MessageBox`, `OpenVisionLab.Controls.Init`, and `FontAwesome.Sharp` have been removed from the application project.
   - Remaining WinForms/OpenGL/ImageCanvas work is tracked outside the app-shell WPF cleanup scope because ImageCanvas will be split into a separate project.

## Exit Criteria

- GitHub checkout can build, publish, launch, run first check, run sample inference, confirm labels, save YOLO files, and retry failed rows from the WPF app.
- Automated tests cover services and WPF construction paths.
- Manual UX verification covers desktop resolution, smaller windows, keyboard flow, YOLO unavailable state, WPF first launch with `scripts\verify-first-run.ps1 -RunWpfSmoke`, visual capture with `--wpf-visual-smoke` on real sample images, and shutdown cleanup.
