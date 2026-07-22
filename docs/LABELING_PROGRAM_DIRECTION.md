# Labeling Program Direction

## Current Source Of Truth (2026-07-22)

This section overrides older migration plans and dated priority notes later in this document. Use project guidance in this order when the documents disagree:

1. `AGENTS.md` for repository operating rules and completion gates.
2. `docs/NEXT_THREAD_HANDOFF.md` for the latest verified project state.
3. `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md` for current product scope, maturity, and commercial comparison.
4. `CODEX_NEXT_PROMPT.md` for the next bounded work item.

The product is a local Windows industrial labeling, training, inference, review, and model-evidence workstation. One recipe owns canonical images, classes, annotations, splits, and provenance; verified adapters prepare those same labels for YOLOv5, YOLOv8, YOLO11, U-Net, and related task-specific runtimes. Human review owns final labels and model adoption.

Current supported task paths are object detection, segmentation, and supervised OK/NG anomaly classification. The WPF shell is the current application shell. U-Net runs as a separate Python segmentation worker selected through the common model-profile boundary; it is not embedded as an in-process C# model runtime. MobileSAM is a bounded single-box smart-mask assistant whose candidates must be reviewed and corrected before save.

The product is not an arbitrary GitHub-model launcher and does not promise that any repository becomes compatible through a name or path alone. A model becomes supported only after its input/output mapping, class and split contract, runtime profile, focused tests, and reproducible evidence are present. Cloud collaboration, workforce management, camera/PLC control, and deployment orchestration remain outside the current product direction.

The dated completed-work list below remains useful evidence history. Any old sentence that says WPF, U-Net, anomaly classification, or a completed feature is still a future task is historical and must not be used to reopen that work.

## Product Role

This application should be the Windows image-AI labeling and tutorial workstation. It should help a first-time learner understand what labeling is, how object detection differs from segmentation and anomaly detection, how YOLO-style box labels and U-Net-style masks are created, and how trained models produce inference results.

It still has to work as a practical labeling tool. It should own image browsing, class management, annotation editing, YOLO label persistence, dataset folder creation, OpenGL visualization, detection overlays, and operator logs.

Python should own model-specific work: training, weight management, inference execution, GPU/runtime dependencies, and model upgrades. The C# application should communicate with Python through a small protocol and should not embed YOLO runtime logic.

## Education Role

- Teach image labeling concepts through the app workflow, not only through README text.
- Separate beginner learning modes for labeling basics, object detection, segmentation, anomaly detection, training, inference, and result review.
- Use checked-in or sibling sample data so a fresh GitHub checkout can immediately demonstrate label creation, model inference, candidate review, and saved outputs.
- Show the relationship between human labels, model predictions, confidence, overlap, false positives, false negatives, and final confirmed labels.
- Keep each model family honest about its annotation type: YOLO uses boxes, segmentation models use masks/polygons, anomaly detection may use normal/abnormal image-level or region-level examples depending on the lesson.
- Prefer operator actions that explain themselves through state, icons, examples, and immediate visual feedback instead of long instruction panels.

## Architecture Direction

- Keep the established WPF shell as the product UI. Remaining WinForms interop is a compatibility adapter, not a reason to restart a broad UI migration.
- Use OpenGL `CViewer`/ImageCanvas as the only image viewer path. Legacy ImageBox/OpenCvSharp UI viewers should stay removed.
- Exclude OpenGL/ImageCanvas from the remaining app-shell WPF cleanup because that viewer stack will be split into a separate project.
- Keep the C# services, Python boundary, and labeling data ownership stable while replacing the UI.
- Keep data ownership in C#: image list, active image state, class list, ROI objects, YOLO annotation files, train/valid dataset output, and data.yaml.
- Keep model ownership in Python: model selection, training loop, inference loop, hardware/runtime setup, and result scoring.
- Treat TCP packets as a compatibility boundary. C# sends training/image commands; Python returns typed detection result messages.
- Let C# own only the Python client process lifecycle for operator convenience. Python still owns the YOLOv5 runtime, weights, training loop, and inference implementation.
- Buffer Python result messages at the TCP boundary. A socket receive chunk is not a full application message.
- Keep Python TCP listener startup explicit enough for tests and tooling. The default app path may auto-start it, but construction alone should not have to bind a port.
- Track Python communication, client-process errors, and training/detection status in service-level snapshots before wiring them into a visible UI panel.
- Keep protocol parsing and DTO conversion outside WinForms controls so it can be tested without opening UI.
- Use display services (`CDisplayManager`, `DisplayLayerStore`, `LabelingWorkflowService`) instead of directly reaching into viewer fields from forms.
- Save YOLO annotations through the deterministic train/valid split policy instead of duplicating every image into both datasets.
- Treat Python detections as candidates first. Only an operator action should confirm Main candidate overlays into label ROI data.

## Communication Boundary

Current command direction:

- C# -> Python: `StartTraining`, `StopTraining`, `StartDefect`, `StopDefect`
- C# -> Python training payload: UTF-8 JSON built by `LearningProtocol`
- C# -> Python inference payload: command header plus PNG bytes
- Python -> C#: `ResultDefect` followed by JSON array or v1 JSON envelope of detection boxes
- Python -> C#: `TrainingStatus` / `DetectionStatus` v1 JSON envelope for progress and errors
- Python -> C#: current YOLO training worker messages `TrainYoloResult` and `TaskStatus` are also accepted and normalized into the same training-status snapshot.

The implemented protocol target is the following versioned message shape:

```json
{
  "type": "ResultDefect",
  "version": 1,
  "imageId": "sample-001",
  "items": [
    {
      "className": "NG",
      "confidence": 0.98,
      "x": 10,
      "y": 20,
      "width": 80,
      "height": 40
    }
  ]
}
```

The legacy `ResultDefect [...]` format remains supported through `PythonDetectionResultProtocol` for compatibility. Verified local workers emit v1 `DetectionStatus` and `ResultDefect` envelopes; public documentation must not depend on one developer-machine path.

## Current Detection Workflow

1. The operator loads the configured image root or a chosen folder into the image list, then loads an image into `Main`.
2. Detection can be started from the top toolbar or the image list's selected-row `Detect` command.
3. The C# app starts/listens for the Python YOLO client and waits briefly for the TCP client connection.
4. `StartDefect` sends the current image as a PNG packet.
5. Python returns a v1 `ResultDefect` envelope.
6. C# validates that the result still belongs to the same active image.
7. Candidate boxes are shown on `Main` through the OpenGL overlay path.
8. The operator can confirm the selected candidate, confirm all candidates, skip a selected candidate, or use keyboard review actions in the candidate panel.
9. Low-confidence candidates below the configured minimum confidence are skipped during confirmation, and the confirm command uses the same threshold when deciding whether it can run.
10. Missing model classes are added to the labeling class list during confirmation.
11. The top class selector stays synchronized with class-list changes and applies the selected class to the Main viewer.
12. Confirmed boxes become Main ROI labels and are saved through the YOLO annotation workflow.
13. The image list shows saved label status and active detection candidate count, then recalculates the active row after saved annotation changes.
14. Image review state is persisted in `review-status.json` under the YOLO output root so `Candidate`, `Confirmed`, `Skipped`, `No Candidate`, and `Failed` survive reloads. The file keeps the legacy numeric `ReviewState` and a readable `ReviewStateName`.
15. The image list can be filtered by review state (`All`, `Unlabeled`, `Requested`, `Candidate`, `Confirmed`, `Skipped`, `No Candidate`, and `Failed`) for batch review.
16. The image list can run YOLO detection sequentially over the currently visible filtered rows, or retry all failed rows from the current image set. The queue advances only after a `ResultCompleted` event, and timeout/stop cancels the pending result to avoid applying late Python results to the wrong image.
17. Review status persistence includes detection attempt count and the latest detection message, so failed rows retain retry count and failure reason across reloads.
18. The image-list status strip shows the selected image's detection detail when review history exists, including retry count and failure reason.
19. `scripts\smoke-yolo-tcp.ps1` validates the real Python TCP client by sending both the legacy `StartDefect` PNG packet and the path-based `DetectImage` request used by WPF batch inference.
20. When candidates arrive, the right review panel switches to the `AI 후보` tab and shows candidate count, confirmable count, selected state, and rejected candidate count.
21. `scripts\smoke-yolo-workflow.ps1` validates the full C# workflow with the real Python client: `StartDefect`, OpenGL candidate overlays, candidate confirmation, saved YOLO label output, and persisted `review-status.json`.
22. The training/preparation panel has a `첫점검` command that checks the YOLO project path, `best.pt`, sample image root, Python package readiness, and automatic worker start setting before the operator tries inference.
23. The same panel has a `테스트` command that runs the real Python YOLO worker against the configured sample image and reports candidate count, first class/confidence, elapsed time, and failure output.
24. `scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3` verifies that the Python worker can keep one TCP connection alive and process repeated path-based inference requests without relaunching the model.
25. WPF batch inference updates row properties immediately, but defers full queue refresh and batches review-status file writes so long queues do less UI and disk work per image.
26. OpenGL AI candidate overlays use a light interior tint, full outline, corner emphasis, and bounded badges so candidates read as inference results rather than manual ROI boxes. Compact badges show both candidate number and class, such as `#2 NG`.
27. Image queue single-click opens the clicked image directly in the Main canvas. The old top preview area is removed, and the selected row is visually emphasized so the active image is clear.
28. Image queue rows show compact status badges beside the filename so candidate, failed, requested, confirmed, skipped, and no-candidate rows are visible while scanning.
29. Image queue quick filters cover candidate, failed, confirmed, skipped, and no-candidate states without opening the filter combo box.
30. Image queue presentation text, icons, brushes, badges, row summaries, and tooltip detail are isolated in `WpfImageQueuePresenter` instead of living directly in the shell window.
31. Image queue detail loading is isolated in `WpfImageQueueDetailLoader`, including image size reading, dimension text, and label/review status refresh.
32. Image queue filtering and dataset status text are isolated in `WpfImageQueueFilterService`, so search/filter/count rules can move into a WPF UserControl without rewriting behavior.
33. The left image queue UI is hosted by `WpfImageQueuePanel`, while the shell keeps the workflow actions and review state coordination.
34. The center canvas UI is hosted by `WpfCanvasPanel`, while the shell keeps image loading, detection overlay data, and annotation workflow coordination.
35. The current-object review tab is hosted by `WpfObjectReviewPanel`, while the shell keeps annotation mutation and class-list coordination.
36. WPF files are organized under `Views`, `ViewModels`, `Models`, `Services`, and `Interop`; every WPF view added in this migration should have a matching `...ViewModel` type.
37. The AI-candidate review tab is hosted by `WpfCandidateReviewPanel`, while the shell keeps candidate confirmation, skip, duplicate checks, and canvas overlay coordination.
38. The class catalog tab is hosted by `WpfClassCatalogPanel`, while the shell keeps class persistence, YOLO output-root saving, YAML/config saving, and class synchronization with object editors.
39. The YOLO status/command area is hosted by `WpfYoloStatusPanel`, while the shell keeps settings validation, requirement installation, smoke inference, and worker lifecycle coordination.
40. The YOLO model settings area is hosted by `WpfYoloModelSettingsPanel`, while the shell keeps path selection, settings persistence, and numeric validation coordination.
41. The training settings area is hosted by `WpfTrainingSettingsPanel`, while the shell keeps training readiness checks, training start/stop commands, and training status coordination.
42. The log and bottom status strip are hosted by `WpfShellLogPanel` and `WpfStatusBarPanel`, while the shell keeps status text updates and application log routing.
43. Legacy WinForms compatibility buttons that used to open YOLO/Python settings now open the WPF shell directly on the YOLO settings tab.
44. Legacy WinForms compatibility buttons that used to open class settings now open the WPF shell directly on the class catalog tab.
45. Object Review class lookup, class application, manual ROI deletion, and confirmed-AI candidate deletion are isolated in `WpfObjectReviewEditService`, while the shell keeps refresh/save/redraw coordination.
46. The WPF learning workflow panel hosts the first education-mode structure and annotation tool palette in the right-side Guide tab without changing OpenGL/ImageCanvas internals or crowding the main workspace.

Detection candidates are removed after confirmation or skip, and the remaining candidates are reindexed so the same model result cannot be confirmed repeatedly into duplicate labels.

## UI Direction

- The default main shell is WPF. `FormMainFrame` has been removed; remaining WinForms surfaces are temporary migration scaffolding until WPF parity is complete.
- Keep the visual direction close to the OpenVisionLab workbench style only where it supports labeling: compact command buttons, restrained chrome, and a workbench-style dark image area. Avoid carrying over inspection-machine UI concepts that make labeling feel like layer/document management.
- Keep labeling-specific controls in the shell only when they are global workflow controls. Image-list review and detection state should stay in services where possible.
- Present the main workspace as a fixed labeling workbench: left dataset queue, center OpenGL labeling canvas, and right object review panel. This layout is the target for the WPF shell too.
- During migration, keep DockPanel only inside the center canvas host for the internal `Main` display. The left and right work areas should be ordinary hosted panels, not layer/document panes.
- Present the center viewer as a single fixed labeling canvas. The internal `Main` display may still be implemented through DockPanel, but the operator-facing caption should be `작업 캔버스`, not a Dev-style layer name.
- Treat the left panel as the dataset/image queue, not a generic file explorer.
- Treat the right panel as object review. Current objects, AI candidates, and the class catalog should be separate tabs instead of one mixed layer/class list, and the panel should show selected item details such as class, confidence, bounds, and confirmability.
- Keep logs available through the Dev log control, but do not let the log pane consume the default labeling workspace.
- Add a clear annotation tool palette for beginner and operator work: select, rectangle, ellipse/circle, polygon, brush, eraser, pan/zoom, undo, redo, delete, and class color preview.
- Do not try to expose every learning mode, model detail, and annotation option directly on the main workspace. The main view should stay focused on the current image queue, canvas, active review, and core commands.
- Put teaching flows, detailed model concepts, and less frequent tool controls into task-specific tabs, panels, or dialogs so the main workspace remains usable for real labeling.
- Drawing tools should feel closer to a precise paint/annotation app than a debug ROI editor: visible active tool, cursor feedback, stable handles, predictable resize/move behavior, brush size/opacity controls, and no surprise inference when selecting images.
- Detection, segmentation, and anomaly detection should be distinct modes or lessons, not ambiguous toolbar states. The UI should make it obvious whether the user is creating ground truth labels, running a model, reviewing predictions, or saving confirmed labels.
- Every teaching mode should have a small sample-first path: load sample, draw label, run inference or save label, review result, and explain what changed.
- U-Net runtime is implemented as a separate Python project and is selected through the common model-profile boundary. Keep U-Net training/inference outside the C# process and preserve the canonical mask export used for cross-model comparison.

## Current Change Policy

1. Do not reopen a completed feature unless its contract, source, environment, or evidence has changed, or a reproducible operator defect invalidates an acceptance criterion.
2. Keep WPF View code-behind as a UI adapter; move command, state, workflow, and presentation logic to ViewModels or services only when a concrete slice benefits.
3. Preserve the established task navigation and annotation-tool rail. Add controls only for a reproduced workflow need.
4. Keep OpenGL/ImageCanvas performance paths unchanged unless the selected task explicitly requires them and includes focused regression evidence.
5. Keep synthetic feature completion separate from optional field adoption. Lack of production-camera data does not reopen a completed deterministic feature gate.

## Completed Work

1. Main labeling workspace is centered on the OpenGL `Main` canvas, with image queue and object review panels around it.
2. YOLO rectangle labels, segmentation masks, deterministic train/valid output, and `data.yaml` generation are owned by the C# app.
3. Python model execution stays outside the C# app and is connected through the TCP worker protocol.
4. Real pretrained YOLO inference works through `C:\Git\yolov5\best.pt` and the sample image folder under `C:\Git\yolov5\data\train\images`.
5. Candidate overlays, confirmation, duplicate-result protection, review status persistence, failed-row retry, and batch queue detection are implemented.
6. First-run operator checks now cover YOLO paths, weights, sample images, Python packages, and worker smoke inference from the app UI.
7. CLI smoke scripts cover protocol-level TCP inference, full C# workflow inference, Python worker lifecycle checks, and first-run verification through `scripts\verify-first-run.ps1`.
8. The default main shell is WPF, with WPF image queue controls for selected detect, visible batch detect, failed retry, stop, and progress.
9. The WPF object review panel shows selected AI candidate details and supports selected/all confirmation and skip.
10. The WPF YOLO tab shows path, weight, image root, requirements, package readiness, confidence, timeout, worker connection, model, and training status before inference.
11. The WPF YOLO tab can install missing requirements, run the smoke test, restart the Python worker, and stop the Python worker.
12. The WPF YOLO tab can edit/save model paths, detection confidence, timeout, basic training parameters, validation split, and can send training start/stop commands.
13. WPF candidate review supports confidence filtering and keyboard review shortcuts.
14. WPF normal detection uses the reusable TCP Python worker path; the slower smoke-process path is kept for YOLO test/diagnostic use only.
15. WPF candidate review blocks high-overlap duplicate candidates from accidental confirmation and shows the duplicate state in the list, comparison card, and canvas result summary.
16. OpenGL detection overlay drawing skips fully offscreen candidates so zoom/pan does not leave detached labels at the viewport edge.
17. WPF image queue selection opens the clicked image in the canvas without rebuilding the queue, and the selected row stays visually connected to the active image.
18. WPF inference commands now show staged status feedback for preparation, worker connection, request execution, and elapsed completion/failure.
19. A WPF manual smoke checklist lives in `docs\WPF_MANUAL_SMOKE_CHECKLIST.md` so repeated UI checks follow the same sequence.
20. The committed runtime example config uses `${repoParent}/yolov5`, and the launcher/first-run scripts resolve `${repoRoot}` and `${repoParent}` tokens for portable sibling checkouts.
21. WPF batch inference uses path-based `DetectImage` requests and no longer reloads every batch image into the OpenGL canvas just to get candidates.
22. The real TCP smoke now covers both `StartDefect` and `DetectImage`, so single-image overlay detection and path-only batch detection are checked separately.
23. OpenGL detection overlay drawing now avoids synchronous refresh calls, guards zero-size resize states, restores GL state after screen overlays/text, and decodes image files into owned Mat buffers.
24. WPF batch inference logs per-image elapsed time, latest item timing, total elapsed time, and average elapsed time.
25. WPF shell, image queue, canvas, and object review views now each expose a matching `...ViewModel`, and WPF files are split into `Views`, `ViewModels`, `Models`, `Services`, and `Interop`.
26. The WPF AI-candidate review UI now has a matching `WpfCandidateReviewPanelViewModel` and is split out of the shell XAML.
27. The WPF class catalog UI now has a matching `WpfClassCatalogPanelViewModel` and is split out of the shell XAML.
28. The WPF YOLO status/command UI now has a matching `WpfYoloStatusPanelViewModel` and is split out of the shell XAML.
29. The WPF YOLO model settings UI now has a matching `WpfYoloModelSettingsPanelViewModel` and is split out of the shell XAML.
30. The WPF training settings UI now has a matching `WpfTrainingSettingsPanelViewModel` and is split out of the shell XAML.
31. The WPF log/status UI now has matching `WpfShellLogPanelViewModel` and `WpfStatusBarPanelViewModel` types and is split out of the shell XAML.
32. The WPF class catalog can edit the YOLO output root path, save data.yaml/config, and update training readiness without opening the legacy class settings form.
33. WinForms compatibility entry points for model/Python settings now route to the WPF YOLO settings tab instead of instantiating the legacy `FormVision_Yolov5ParamSetting` dialog.
34. The WinForms compatibility class settings button now routes to the WPF class catalog tab instead of instantiating the legacy `FormVision_ClassMenu` dialog.
35. The WPF detection-result summary overlay uses theme resources, so light and dark themes keep readable AI result text.
36. The WPF YOLO tab shows the active recipe/config file location and can save the current project settings without opening a WinForms dialog.
37. The WPF project panel can apply a typed recipe name through the existing `CRecipe` load/create path and refresh the visible settings.
38. WPF settings save feedback distinguishes between in-memory editor updates and an actual recipe XML save.
39. The WPF project panel can list existing recipe folders, copy a selected recipe into the apply field, and refresh the list without auto-switching projects.
40. `FormMainFrame` and its designer/resource files were removed, and startup now runs the WPF shell without a legacy WinForms shell fallback.
41. The legacy WinForms teaching shell panels were removed: `FormTeachingVision`, `FormImageList`, `FormClassList`, `FormTrainingPanel`, `FormDetectionReviewPanel`, `FormLog`, and `FormLayerDisplay`. `CDisplayManager` now stores `DisplayLayerDocument` state instead of WinForms display windows, and DockPanelSuite was removed from the app project.
42. The remaining legacy labeling/settings dialogs were removed: `FormVision_Yolov5ParamSetting`, `FormVision_ClassMenu`, and `FormVision_NewPanel`. Their direct UI tests now target WPF class, YOLO model, and training panels instead.
43. The WPF YOLO model and training settings ViewModels now own editable state and round-trip values into `PythonModelSettings`, `TrainingSettings`, and `YoloDatasetSettings` instead of leaving the shell to read every TextBox directly.
44. App startup, common message boxes, recipe/data path resolution, screen capture, and viewer context menus no longer depend on `RJControls`, `OpenVisionLab.MessageBox`, `OpenVisionLab.Controls.Init`, `FontAwesome.Sharp`, or `Application.StartupPath`. Those legacy support projects were also removed from the solution.
45. `CViewer.Designer.cs` and `CViewer.resx` were removed. The viewer no longer uses a WinForms component timer, ROI draw objects no longer return WinForms cursors, and overlay label bitmaps no longer use WinForms `TextRenderer`.
46. The unused reverse WPF-to-WinForms `WpfRoiCanvasHost` bridge and the unused WinForms `UIThreadInvokeClass` extension helper were removed. The WPF shell hosts `RoiImageCanvasView` directly through `WpfCanvasPanel`.
47. Raw `CViewer.AttachTo(Control)` usage was moved behind `CViewerWinFormsHostAdapter`, so the remaining WinForms/OpenGL host boundary is explicit and easier to replace later.
48. `RoiImageCanvasViewModel` now receives neutral canvas mouse/key input through `RoiImageCanvasInputAdapter` instead of branching directly on WinForms mouse buttons and key event args.
49. `ImageCanvasControl.designer.cs` and `ImageCanvasControl.resx` were removed. `ImageCanvasOpenGlHostAdapter` now creates the `SharpGL.OpenGLControl` and owns its event wiring.
50. The WPF project panel now binds recipe name, recipe list, selected recipe, config path, and status text through `WpfProjectConfigPanelViewModel`; recipe listing/path logic lives in `WpfProjectRecipeService`.
51. The WPF class catalog now binds class name, selected class, class list, output root, and status through `WpfClassCatalogPanelViewModel`.
52. The WPF YOLO status panel now binds settings summary/detail, command status, and command progress through `WpfYoloStatusPanelViewModel`.
53. The WPF object review panel now binds object summary, rows, selected object, class options, selected class, and action enabled state through `WpfObjectReviewPanelViewModel`.
54. The WPF AI-candidate review panel now binds confidence text, detail text, candidate rows, selected candidate, action enabled state, and action tooltips through `WpfCandidateReviewPanelViewModel`.
55. Candidate review display policy now lives in `WpfCandidateReviewPresenter`: row text, detail text, comparison text, icon/brush state, confirmability, duplicate-overlap policy, and disabled-action hints.
56. Object review display policy now lives in `WpfObjectReviewPresenter`: summary text, empty text, manual ROI row/tooltip text, confirmed-AI row text, and object row construction.
57. Candidate Review and Object Review no longer keep direct `ListBoxItem` row creation or direct action-button fallback state in the shell; the normal path is ViewModel-only.
58. Class Catalog no longer reads selected classes, output-root edits, or status state from direct shell fallbacks; the normal path is `WpfClassCatalogPanelViewModel`.
59. Image queue row selection now loads the clicked image into the Main canvas, removes the old top preview block, and makes the selected row clearer with an accent line and selected background.
60. Workflow command availability and detection tooltips now live in `WpfWorkflowCommandStateService`, with direct service tests covering idle, inference, busy, project-save, and training-stop states.
61. Object Review class lookup, apply, and delete behavior now lives in `WpfObjectReviewEditService`, with direct service tests covering manual ROI and confirmed-AI candidate edits.
62. Object Review selection is now ViewModel-only on the normal WPF path; the shell no longer falls back to reading selected objects directly from the ListBox.
63. The WPF learning workflow panel now defines beginner modes for labeling, object detection, segmentation, anomaly detection, training, inference, and review, plus an annotation palette for select, box, ellipse/circle, polygon, brush, eraser, pan/zoom, undo, redo, and delete.
64. The beginner sample flow now has clickable Sample, Label, Infer, Review, and Save steps connected to safe existing shell commands without making image selection auto-run inference.
65. The center canvas now exposes recovery commands for Fit, 1:1, Pan, selected-candidate Focus, and AI candidate overlay reset.
66. AI candidate review now supports previous/next/focus controls and `N`/`P`/`F` keyboard review flow.
67. WPF queue image switching now has a bounded adjacent-image decode cache so nearby rows can open without repeating the full disk decode path.
68. The education guide panel now shows concise YOLO, U-Net segmentation, anomaly, training, inference, and review explanations without crowding the main workspace.
69. The education guide panel now exposes brush size and mask opacity state as the first controls for segmentation/anomaly teaching workflows.
70. The Guide tab now starts with the YOLOv5 training path: load images, register classes, draw box labels, save/check the dataset, train YOLOv5, and review inference with the trained `best.pt`.
71. The YOLOv5 training path is now actionable: guide rows move to image loading, class registration, box labeling, dataset readiness, training settings, and post-training inference review. The guide also shows dataset readiness and can apply the latest trained `best.pt` candidate.
72. The YOLOv5 training path now shows per-step state badges, direct class/label/dataset fix buttons, trained-`best.pt` recipe-save guidance, and colored training readiness/progress feedback.
73. The YOLOv5 guide now remembers the latest dataset check, training status, applied `best.pt`, and recipe-save state, and it splits dataset blockers into clearer class, label, valid-image, `data.yaml`, label-format, output-root, and image-folder actions.
74. The YOLOv5 guide now keeps a bounded recent-run list for explicit dataset checks, completed training states, and weight apply/save events, so learners can see the immediate train/check/apply trail without reading the log pane.
75. The Guide tab default view is now the actionable YOLOv5 flow only. Secondary mode, flow, tool, brush, and mask concepts stay collapsed under an extra-concepts area so labeling work is not hidden behind lesson controls.
76. Inference progress is visible in the top bar through a global `추론 상태` chip, not only inside the YOLO settings tab.
77. After inference results are applied, the canvas returns to centered fit view and the top inference progress uses a stable pulse, so learners can see both working and result states without hunting for the image.
78. First-time users now have an in-app `처음 10분 튜토리얼` card and a matching HTML tutorial at `docs\tutorial\labeling-workbench-tutorial.html` with current WPF screenshots.
79. Beginner tutorial steps now have side-effect tests: sample image load, box label creation, inference mode activation, candidate review selection, and YOLO label save.
80. The Guide tab now has a direct HTML tutorial open button, with clone/execution-folder path resolution and a visually safe truncated path display.
81. Annotation tools now have an explicit connected/pending capability map. The Guide tab shows `가능` for connected tools and `대기` for tools whose WPF/OpenGL path is not verified yet.
82. WPF annotation tool validation is documented and tested. Rendering primitives and legacy `CViewer` APIs are explicitly not treated as complete WPF tool support.
83. Pending WPF annotation tools are not blocked by OpenGL itself. The implementation direction is image-pixel annotation first, OpenGL rendering second: shapes store source-image pixel geometry, masks store source-image-size raster buffers, then export YOLO boxes or segmentation data depending on the lesson mode.
84. WPF ellipse/circle annotation is now connected as a pixel-space ROI shape. The canvas renders it as a translucent filled ellipse with an outline, object review preserves the shape label, and YOLO detection export uses the same bounding box.
85. The YOLOv5 box-labeling step now leads to the actual WPF box tool instead of leaving the user to hunt through the guide. The tool palette is revealed, box drawing is selected, the canvas hides debug `Module` group chrome by default, and the viewer refresh timer is bounded to a frame-rate debounce instead of a 1 ms loop.
86. Annotation tools now have an explicit object verification gate: inside click selects an existing object, empty drag creates a new object, selected handles stay visible, and rectangle plus ellipse/circle are covered by automated tests and `scripts\verify-wpf-annotation-objects.ps1`.
87. Rectangle and ellipse/circle verification now checks move and resize coordinate deltas through the real WPF mouse-event path, so viewer changes must preserve image-pixel geometry instead of only producing a visible drawing.
88. WPF polygon annotation now has a verified image-pixel draft/export service that creates `LabelingSegmentationObject` instances and uses the existing segmentation save/load path.
89. WPF polygon annotation is now connected to the visible WPF/OpenGL viewer: image-pixel click input, near-start/double-click completion, right-click cancel, draft/confirmed OpenGL overlays, Object Review rows, class edit/delete, and segmentation save dictionary are verified.
90. WPF brush and eraser annotation now have a verified image-pixel raster-mask path: drag input, mask paint/erase, Object Review `Mask` rows, OpenGL mask texture preview, empty-mask removal, and segmentation save dictionary are covered by tests.
91. WPF Undo/Redo now has a verified first edit-history path for ROI, polygon, mask, AI candidate confirm/skip, class change, and delete. It restores WPF editing state and refreshes the visible canvas/review panels without routing through legacy `CViewer`.
92. WPF raster-mask preview no longer converts masks into coarse polygon regions. `RoiImageCanvasViewModel` now owns a dedicated `RoiImageCanvasMaskOverlay` path that uploads mask pixels as an OpenGL texture and draws it in image-pixel coordinates.
93. WPF brush and eraser now show an OpenGL image-pixel cursor preview before painting. The circle uses the current brush radius and class/eraser color, clears on tool exit or mouse leave, and is covered by automated tests plus visual smoke.
94. WPF raster-mask selection is now visible from Object Review to canvas: selected mask rows carry `IsSelected`, draw a topmost marker using the same polished OpenGL marker style as AI detections, clear stale ROI handles, and keep the canvas badge number aligned with the Object Review row.
95. WPF selected raster masks can now move through image-pixel drag with history committed on release, so mask labels behave like editable annotation objects instead of one-way paint output.
96. WPF selected polygon points can now move through image-pixel drag, and raster-mask texture updates use dirty-bounds `glTexSubImage2D` when the texture shape is unchanged.
97. WPF Undo/Redo now communicates runtime availability in the Guide tool palette: empty stacks show `없음`, available stacks show `가능`, and tooltips name the next edit action.
98. Undo/Redo persistence policy is explicit: it restores the in-memory WPF edit state and waits for the user's save action before rewriting YOLO/segmentation label files.
99. WPF annotation save state is now visible in the bottom status bar. Real edits show `라벨 저장 필요`, successful saves and image loads return to `라벨 저장됨`, and Undo/Redo after save correctly marks the image dirty again.

100. WPF bottom status bar Dataset/Python/Model text now uses `WpfStatusBarPanelViewModel` bindings, while the shell keeps the existing status update methods as a compatibility wrapper.

101. WPF training readiness/progress/epoch status now uses `WpfTrainingSettingsPanelViewModel` bindings, while the shell keeps compatibility helper methods for existing training command paths.

102. WPF YOLO status-panel command availability now uses `WpfYoloStatusPanelViewModel` bindings for first check, install, smoke test, restart, and stop-worker buttons.

103. WPF project config command availability now uses `WpfProjectConfigPanelViewModel` bindings for apply, refresh, save, and folder buttons.

104. WPF YOLO model settings command availability now uses `WpfYoloModelSettingsPanelViewModel` bindings for browse, save, and reset buttons.

105. WPF training settings command availability now uses `WpfTrainingSettingsPanelViewModel` bindings for refresh, start, and stop buttons.

106. WPF current-image inference and image-queue inference command availability now use `WpfLabelingShellViewModel` and `WpfImageQueuePanelViewModel` bindings. Labeling mode locks inference, inference-review mode enables it, and running batch detection leaves only stop available.

107. WPF canvas helper command availability now uses `WpfCanvasPanelViewModel` bindings for the visible `맞춤`, `1:1`, `이동`, selected detection-candidate focus, and candidate-clear actions.

108. WPF YOLO guide fix command availability now uses `WpfLearningWorkflowPanelViewModel` bindings for class registration, label start, and dataset check actions.

109. WPF image queue selected-open command availability now uses `WpfImageQueuePanelViewModel` binding, so a valid selected image row controls the open button without shell direct UI writes.

110. WPF top Labeling/Inference mode button active and enabled states now use `WpfLabelingShellViewModel` binding plus WPF style triggers, so the shell no longer directly paints or enables those mode buttons.

111. WPF candidate comparison card state now uses `WpfCandidateReviewPanelViewModel` bindings for visibility, AI text, current-label text, IoU text, and high-overlap styling. The shell only passes presenter output to the ViewModel.

112. WPF canvas AI detection result overlay state now uses `WpfCanvasPanelViewModel` bindings for visibility, summary text, selected-candidate text, detail rows, and confirmable/duplicate/review styling.

113. WPF image queue quick filter text and active styling now use `WpfImageQueuePanelViewModel` bindings. The shell only passes the selected filter and per-state counts, while XAML DataTriggers handle the selected quick-filter look.

114. WPF Object Review class editor sync now uses `WpfObjectReviewPanelViewModel.SetSelectedObjectClass`. The class ComboBox relies on TwoWay binding and no longer routes a selection event back to the shell just to refresh apply-state.

115. WPF Candidate Review selection detail and comparison-card state now update through `WpfCandidateReviewPanelViewModel.ApplySelectionReview`. Candidate selection lookup no longer falls back to `CandidateListBox.SelectedItem`; the ViewModel selection is the normal source.

116. WPF Candidate Review navigation state now uses dedicated ViewModel properties for previous, next, and focus. Skip-state no longer doubles as navigation-state, and single-candidate lists no longer pretend previous/next movement is available.

117. WPF image load memory ownership is clearer: the decoded Mat is transferred to `CDisplayManager.ImageSrc` without cloning after OpenGL upload, while ImageSpace keeps the active Bitmap reference. Adjacent decode cache also has a 64 MB total memory budget.

118. OpenGL texture upload clone policy is now explicit: continuous Mats upload without `Clone()`, while non-continuous tile/SubMat buffers still use compact clones for stride-safe `glTexSubImage2D` upload.

119. WPF queue-click performance can now be measured against real image folders. The smoke reports click latency, working set start/end/peak/delta, and decode-cache hit/miss/store/eviction counts.

120. WPF queue-click performance now separates the first cold switch from warm repeated switches. The latest YOLO sample-folder smoke shows warm repeated switches at 34.5 ms average and 49.7 ms max.

121. WPF queue-click performance now separates image commit timing from dispatcher/render settling. Latest sample-folder smoke shows image commit at first 30.6 ms, warm average 14.5 ms, and warm max 22.6 ms.

122. WPF queue-click performance now uses visible image switching as the main user-facing metric and reports full dispatcher settled timing separately. Latest sample-folder visible switching is first 44.4 ms, warm average 24.2 ms, and warm max 36.1 ms.

123. A realistic WPF labeling-session verification now runs the beginner path end to end: image load, box, filled ellipse/circle, polygon, brush mask, eraser, save, AI duplicate skip, and selected AI confirm.

124. The left image queue quick filters and selected-row presentation were tuned so the current image and filter state are visible without the old top preview block.

125. The Guide default view now keeps the actionable YOLOv5 training flow separate from the long detailed checklist, so learners can find labeling and training steps faster.

126. Candidate Review now shows a selected-candidate summary before confirm/skip actions, making the current AI review target explicit.

127. The latest real-folder queue performance pass on `C:\Git\yolov5\data\train\images` shows visible switching at 19.5 ms average and 26.5 ms max, with image commit at 14.2 ms average.

128. The WPF YOLO training session is now covered by an automated app-level smoke: train/valid images are labeled and saved, dataset readiness passes, `StartTraining` is sent to the TCP worker, worker completion is parsed, the latest `best.pt` is applied, and the workflow moves to candidate review.

129. The C# protocol parser now accepts the current Python worker's `TrainYoloResult` and `TaskStatus` training messages, not only the older `TrainingStatus` envelope.

130. The WPF training panel now polls worker training status after Start, so progress and completion are visible without requiring a manual refresh.

131. Candidate Review post-action policy is settled for now: confirm and skip advance to the next candidate. The panel states this directly under the action buttons.

132. The HTML tutorial screenshots were refreshed sequentially against the current WPF UI to avoid stale or overlapping captures.

133. The current baseline YOLO model remains the user-trained `C:\Git\yolov5\best.pt`. A direct 2026-06-22 comparison showed `mAP50 0.995` and `mAP50-95 0.961` on the current sample set.

134. A Codex CPU 10 epoch YOLOv5m experiment was recorded but not adopted. It reached only `mAP50 0.389`, `mAP50-95 0.215`, and produced no usable candidates at the app's normal 25% confidence threshold.

135. The current YOLO sample dataset has a serious evaluation caveat: train and valid images are the same 125 files. Treat current metrics as sample-fit checks, not generalization proof.

136. The detailed model comparison record lives in `docs\YOLO_MODEL_COMPARISON_20260622.md`.

137. YOLO readiness now blocks duplicate image content between train and valid, not just duplicate file names. This keeps future training comparisons from accidentally validating on the same images used for training.

138. Model comparison can now be rerun with `scripts\compare-yolo-models.ps1`. The script writes JSON and Markdown reports under `artifacts\yolo-model-comparison`.

139. Detection candidate review now has a recipe-level maximum candidate cap. The default is 20, configurable from WPF `YOLO > Model Settings`, and only trims by confidence when the raw result exceeds the cap.

140. The first-run verifier now includes the model-comparison script syntax check so release/clone validation covers the new workflow utility.

141. YOLO dataset output now supports train/valid/test. `Test %` is optional and defaults to 0, so existing training flows continue, while real evaluation sets can be separated before adopting a new model.

142. YOLO annotation save/load, segmentation save/load, data.yaml generation, readiness statistics, and split duplicate detection all understand the test split.

143. The model comparison script now accepts `-Task val|test`, so a prepared test split can be used for final model adoption checks.

144. The WPF training settings panel now explains the Validation/Test split distinction directly beside the split settings.

145. YOLO diagnostics now reports split detail, empty test-set warning, class object balance, low-sample class warnings, and skewed class-balance warnings even when the dataset is technically ready.

146. Training readiness remains permissive for an empty test split so older projects still work, but the operator report now makes it clear that final model comparison needs a real test set.

147. The Guide tab first-visible YOLO area now includes a compact clickable completion checklist for image loading, class registration, box labeling, dataset check, training, and inference review, so a beginner can see and enter the whole object-detection path without scrolling into the long tutorial.

148. The compact YOLO completion checklist is now real-EXE verified: step chips expose per-step AutomationIds, and clicking the box-labeling chip through UIAutomation selects the box tool through the shared workflow command path.

149. The Guide chip real-EXE smoke now verifies dialog-free YOLO steps 2-6: class catalog, box labeling, dataset checklist, training settings, and candidate review. Step 1 remains tied to the folder picker and belongs in dataset setup smoke coverage.

150. The Guide tab now includes a dataset status dashboard driven by the same YOLO readiness report as the training checklist. It surfaces image/split/label/class/duplicate metrics and a short operator issue list, so beginners can see what blocks training without reading the long diagnostic text.

151. The Guide YOLO first-visible area now keeps the completion chips and dataset status dashboard together in a compact layout. The chip command targets remain real-EXE verified, while the dashboard stays visible enough for beginners to notice readiness blockers without leaving the Guide tab.

152. The Guide dataset dashboard metric cards are now real button-command shortcuts. Dialog-free real-EXE smoke verifies class catalog, box-labeling tool, and dataset-settings card navigation. The image-folder card remains outside this smoke because it opens an OS folder picker.

153. The object-detection real-EXE box loop is now protected as a stable area: random multi-box drawing, Object Review row creation, ROI click selection, delete enablement, and delete-then-wheel responsiveness are verified by `--exe-roi-tools-smoke --seed 260626 --box-count 12`.

154. The COCO128 real-EXE dataset setup/edit loop is now verified: the wizard creates manifest/data files, loads existing YOLO boxes, deletes a selected loaded box through Object Review, saves, draws a new box on an empty-label image, saves, and reopens the image with the new object visible.

155. The Kolektor industrial object-detection preset and real-EXE labeling loop are verified: the wizard copies 317 prepared industrial images, labels 10 images with random valid boxes, saves split-aware YOLO txt files, reopens saved labels, and completes the dataset-check dashboard flow.

156. The status bar now preserves the active image filename while queue summary/detail loading updates continue, so real operators and EXE smoke tests can tell which image is actually loaded on the canvas.

157. The OpenGL ROI 500K viewport-render regression is fixed: spatial-index list queries now pre-size result and seen collections, and the full regression passes with ROI viewport query/rebuild both under 6 ms in the measured run.

158. YOLO box annotation save now preserves the active source image extension, removes stale same-stem image siblings, and enforces one same-stem image/label pair per selected split so industrial `.jpg` datasets do not accumulate confusing `.jpeg` copies.

159. The industrial object-detection real-EXE labeling loop is now extended to 30 random images. The smoke verifies 317 copied images remain 317 image artifacts after save, 30 label files are produced, duplicate image stems are 0, and selected image/label missing counts are 0.

160. The image queue now exposes the next-image action as a visible `다음` icon+text button in the narrow left panel. This keeps the beginner object-labeling loop visible after save: label current image, save, then move to the next unfinished image.

161. Save completion now updates the always-visible canvas workflow strip to point to the left queue `다음` button with natural `이어서 작업` wording. The beginner loop is therefore visible in-place: draw label, save, click next.

162. Empty object-detection completion is now verified in the real EXE. Candidate Review keeps an `이미지 완료` action visible even when there are no 검출 후보, saves one zero-object label file, advances to the next unfinished image, and the image queue shows the reviewed image as `빈완료`/`검출없음` instead of an unknown unfinished state.

163. Object-detection 실사용 UX 검증이 한 단계 확장되었습니다. The queue no-candidate quick filter now says `검출없음`, Object Review exposes stable AutomationIds for future real-EXE selection/edit/delete checks, and the industrial EXE smoke verifies a mixed loop of 12 random box labels plus 3 empty completions with split-safe artifacts and dataset check.

164. Brush/eraser completion UX is now locked with verification: MouseUp immediately marks annotations as `save needed` while the OpenGL/FBO preview remains visible and CPU MaskData/history materialization stays deferred. The focused WPF mask drag, dirty-bounds, 500K hover, and real-EXE mask-tools smokes pass with measured input responsiveness.

165. Dataset readiness dashboard now shows labeling progress as a first-visible metric (`completed label files / total images`) with a dedicated tool shortcut. This makes the learner path clearer: load images, create/save labels until progress is complete, then run dataset check and training.

166. Training result comparison now gives a learner-readable verdict before the metric list. `results.csv` still supplies mAP/precision/recall/loss deltas, but the Guide can now say whether the latest model is better, the current model is better, tied, or undecidable.

167. The real-use object-detection verification loop now passes a longer visible-EXE run: 30 random industrial box labels plus 5 empty normal completions, 317 copied images preserved, 35 label files saved, no duplicate image stems, reopen verified, and dataset check completed. Use a long timeout for this smoke because it drives the full UI.

168. The status strip is now part of the top chrome instead of the bottom edge. Dataset progress, Python/model state, and annotation save state should be visible before the operator starts canvas work, while deeper logs remain in the lower log area.

169. Candidate Review now shows learner-readable decision guidance inside the AI-vs-current-label comparison card. Duplicate, partially overlapping, out-of-image, and new candidates should be explained in the ViewModel-bound presentation payload instead of being rebuilt in shell code-behind.

170. Candidate Review can now jump from a selected AI candidate to the overlapping current label. The `Label` action selects the matching Object Review row and moves the side panel there, so duplicate candidates can be checked or edited without hunting through the object list.

171. Candidate Review current-label focus now also selects the overlapping manual ROI on the canvas. The Object Review row and OpenGL ROI edit handles should light up together, so the operator can immediately resize, move, or reclassify the existing label.

172. Candidate Review `Label` now has a stable `FocusCurrentLabelButton` automation id and its regression test executes the actual bound button command. Future EXE tests should use this automation id when a repeatable candidate-producing setup is available.

173. Real EXE Candidate Review focus is now verified by `--exe-candidate-focus-smoke`. The smoke applies a temporary recipe, loads a sample image, creates a manual ROI, runs a temporary YOLO smoke client, clicks `FocusCurrentLabelButton`, and confirms the overlapping Object Review row becomes selected. Current-image inference must not reload the same image before applying candidates, because that erases the user's labels and breaks duplicate/current-label comparison.

174. Current-image smoke inference label preservation is now covered by `--wpf-current-image-smoke-preserve-labels`. The test creates a manual ROI, runs a temporary YOLO smoke client on the already-active image, and verifies the manual ROI remains present and usable for Candidate Review high-overlap/current-label focus.

175. Candidate Review wording now distinguishes `AI candidate` from `existing label` in the comparison actions. Duplicate-candidate guidance should point operators to inspect the existing label first, then skip if it is the same object; new-candidate guidance should say confirm when correct and skip otherwise.

176. `docs/CODE_STRUCTURE.md` now includes a quick reading order, current product-stage map, prioritized remaining architecture work, and completion-check commands. Future refactors should consult it together with `docs/STABLE_VERIFIED_AREAS.md` before touching verified viewer or Candidate Review paths.

177. Object-detection MVP completion criteria are now documented in `docs/OBJECT_DETECTION_MVP_COMPLETION.md`. The MVP scope is project/dataset setup, image queue, box labeling, save/reopen, YOLOv5 candidate review, empty-normal completion, and readiness feedback; YOLOv8, ONNX, U-Net runtime, and anomaly detection stay outside this MVP.

178. YOLOv5 training/result-comparison completion criteria are now documented in `docs/YOLOV5_TRAINING_RESULT_WORKFLOW.md`. The flow keeps Python responsible for training/runtime, keeps C# responsible for dataset/readiness/comparison state, and requires test-split comparison before replacing the active `best.pt`.

179. Segmentation UX completion criteria are now documented in `docs/SEGMENTATION_UX_COMPLETION.md`. The document separates verified polygon/brush/eraser hot paths from remaining beginner-flow work and protects the OpenGL/FBO mask preview path from synchronous MouseMove/MouseUp regressions.

180. Anomaly detection flow criteria are now documented in `docs/ANOMALY_DETECTION_FLOW.md`. Anomaly detection stays a separate dataset purpose centered on image-level normal/abnormal state first, with optional box/mask region labels and no C# anomaly runtime until a Python backend is chosen.

181. YOLOv5 training result comparison now has a structured learner-facing report in the Guide tab. The report separates verdict, key metric, latest model, and current model rows so operators do not have to parse one long `results.csv` summary before deciding whether to apply a new `best.pt`.

182. Candidate Review model-difference examples now resolve source image paths from the comparison `data.yaml` and open selected disagreement images through a ViewModel command, so old-vs-new model differences can be inspected from the review panel without hunting through artifact folders.

183. The Guide training-result card now exposes a user-visible `모델 비교` command. The command builds and runs `scripts\compare-yolo-models.ps1 -Task test` through `WpfModelComparisonRunService`, then refreshes Candidate Review with the latest disagreement examples.

184. Model comparison now performs preflight validation before launching Python: the requested `data.yaml` split must contain images, and baseline/candidate `best.pt` paths must differ. The `-Task test` execution path was verified on 2026-06-27 with `artifacts\yolo-model-comparison\test-routing-data.yaml`, which maps `test` to the existing valid split only for pipeline verification.

185. Industrial object-detection real-EXE labeling now verifies the beginner MVP path with 317 copied images, 10 random box labels, 2 empty-normal completions, saved-label reopen, duplicate-stem checks, and dataset readiness. Queue Open now resolves stale selection and recovers saved split image copies from `data/train|valid|test/images` when the original staging path is gone after save.

186. The Guide model-comparison button now follows dataset readiness before the user clicks it. It is disabled before dataset check, disabled while readiness has blocking errors, disabled when the held-out test split is empty, and enabled only after a checked dataset has at least one test image.

187. The object-detection MVP long real-EXE loop was reverified on 2026-06-27: 317 industrial images copied, 30 random box labels saved, 5 empty-normal completions saved, duplicate stems 0, selected missing image/label counts 0, reopen verified, and dataset check completed. Treat the queue open/save/reopen path as protected unless this long smoke is rerun.

188. Beginner-facing terminology cleanup is now protected. Candidate Review, model/training settings, dataset status, inference status, model comparison, object review, and tool labels should avoid exposing implementation terms such as OpenGL, FBO, GPU, CPU, ROI, raster mask, bounding box, Python worker, best.pt, data.yaml, test split, baseline, candidate, or IoU in operator-facing text unless the view is explicitly showing a file path or developer diagnostic.

189. Candidate Review and canvas helper wording now use learner-facing detection terms. Visible controls and logs should say `검출 후보`, `이동`, `후보`, and `후보 지움` instead of `AI 후보`, `Pan`, `Focus`, or `AI Reset`, so post-inference review reads as a labeling task rather than an internal viewer/debug operation.

190. The object-detection canvas workflow strip and detection-result overlay now use beginner-readable guidance. The visible fit button says `맞춤`, the review step says `검토`, the overlay title says `검출 결과`, dirty-label guidance tells the operator to save the current image labels, and save completion points to the left queue `다음` button with natural `이어서 작업` wording.
191. The image queue completion language now uses `미완료` for remaining work. Empty normal images that are completed with an empty label file are treated as done in the queue filter, and finishing the last image refreshes dataset readiness so the workflow can move on to dataset check/training instead of staying in labeling.
192. The top status bar now summarizes `단계`, `진행`, and `다음` so operators can see the current task, completed/remaining image count, and next action without looking at the bottom log or reading side-panel details.
193. Candidate Review completion wording now uses `이미지 완료`. The action means "finish this image, save the current label state or normal-empty completion, then move to the next task"; it should not be described as `완료 후 다음` in visible operator UI.

194. The Guide now includes a first-visible YOLO dataset structure lesson. It explains `data.yaml`, image folders, label folders, same-stem image/txt pairing, and the meaning of one txt row through ViewModel-bound UI instead of burying the concept in text-only documentation.

195. The Guide dataset dashboard now repeats the object-detection MVP remaining action as a compact `객체탐지 MVP 완료까지` line derived from the same dashboard action state. This keeps beginner next-step guidance tied to the object-detection completion criteria instead of becoming another separate checklist.

## Historical Work List (superseded)

The numbered items in this section record how the product reached the current state. They are not an active backlog. Select work only from the source order defined at the top of this document and do not repeat an item already closed in `docs/STABLE_VERIFIED_AREAS.md` or `docs/WORK_TRACKING.md`.

1. Keep new settings/confirmation surfaces WPF-only. The main shell, teaching view, image queue, class list, training panel, detection-review panel, log panel, display-layer form, YOLO settings dialog, class settings dialog, new-panel dialog, common message boxes, and legacy support library references are now removed.
2. Split the WPF shell into focused UserControls/ViewModels after the current flow settles: image queue models, presentation, detail loading, filtering, queue panel, canvas panel, current-object review, AI-candidate review, class catalog, YOLO status/commands, YOLO model settings, training settings, project config state/service, log/status strip, candidate/object display presenters, workflow command-state service, object-review edit service, Object Review ViewModel-only selection, learning workflow panel, status bar binding, training status binding, YOLO status command binding, project config command binding, YOLO model command binding, training command binding, detection/queue command binding, image-queue selected-open binding, canvas command binding, YOLO guide fix command binding, top workflow mode button binding, candidate comparison card binding, canvas detection result overlay binding, image queue quick filter binding, Object Review class editor sync, Candidate Review selection detail/comparison sync, Candidate Review navigation state binding, and the first ViewModel folder split are done. OpenGL/ImageCanvas is excluded because it will move to a separate project. Next continue moving Object/Candidate Review row presentation helpers into ViewModels or presenters.
3. Keep improving batch inference speed with measured long-queue runs: worker reuse, row refresh throttling, and save throttling are covered; next compare Python inference time against remaining WPF UI/log overhead.
4. Continue per-row queue polish after the first status badge and quick-filter pass: tune compact error text for long failure messages and validate the layout with larger real folders.
5. Improve candidate review ergonomics further with current-label editing beside AI candidates, clearer class editing in WPF, and class-specific overlay color/thickness tuning based on real defect screenshots.
6. Add training-run detail, comparison, delete, or export only if the compact recent-run list is not enough during real use.
7. Keep Undo/Redo as in-memory WPF edit restoration until explicit save. The dirty/saved status chip is now in place; tune wording only after real labeling sessions.
8. Keep the completed U-Net integration on its separate Python-project boundary; do not duplicate the runtime inside the C# process.
9. Tune the viewer command strip after real use: compact labels and icon density. Top workflow mode active/disabled states are now ViewModel-bound.
10. Tune the global inference status chip after a real long-running detection run if the top-bar signal is still not obvious enough, and profile UI-thread request work if the pulse still visibly pauses.
11. Tune the adjacent-image decode cache against large real folders: capacity, max pixel count, eviction timing, and memory pressure behavior.
12. Profile memory with real image folders. The WPF shell no longer clones the active Mat for `CDisplayManager`, and continuous OpenGL uploads now skip clone; non-continuous tile clones remain for pointer/stride safety until measured data proves a row-stride OpenGL path is needed.
13. Check whether Background-priority dispatcher drain affects rapid repeated queue clicking or scrolling. Image decode/OpenGL upload should not be optimized further unless a larger real dataset pushes visible switching above 50 ms.
14. Keep OpenGL/ImageCanvas cleanup tracked separately from the app-shell WPF migration.
15. Keep `samples/python_protocol`, `mock_yolo_client.py`, `scripts\smoke-yolo-tcp.ps1`, `scripts\smoke-yolo-workflow.ps1`, and `C:\Git\yolov5\labelling_tcp_client.py` updated together whenever the protocol changes.
16. Extend first-run verification toward release packaging: Release/Debug publish, artifact manifest checks, the manual WPF smoke checklist, and portable sibling runtime config are covered; next is a clean-checkout sample recipe run on a separate folder.
17. Decide how recipes should be shared between PCs: local-only config for private paths, committed sample recipe for the bundled YOLO sample set.
18. Expand operator documentation only around real workflows: load images, run first check, draw labels, run detection, review predictions, confirm, save, retry failed images.
19. Refresh the HTML tutorial screenshots whenever the beginner path or right-side tabs change enough that the old images would mislead users.
20. Keep validating rectangle drawing with real mouse drags after each viewer change. If stutter remains, measure visible-overlay recalculation and object-list refresh separately before adding any new rendering abstraction.
21. Improve mask UX after real use: selection highlight, move, and partial texture update are done; next decide whether raster masks need reshape/detail editing.

23. Keep the rectangle and ellipse/circle coordinate-delta tests passing whenever OpenGL hit tolerance, zoom/pan math, or ROI edit code changes.

24. Polygon point move is done. Add point delete/add-on-edge only after the basic polygon workflow shows the exact UX need.

25. Run a long real YOLOv5 `train.py` session as a learner after enough real labels exist: load many images, register classes, label samples, save/check the dataset, start training, apply `best.pt`, infer, and review. Record where the user hesitates before adding more UI.

26. Keep the 1~5 verification loop as the gate for future UX changes: full tests, labeling-session smoke, YOLO-training-session smoke, Guide visual smoke, Candidate Review visual smoke, annotation-object verification, and real-folder queue performance.

27. Before adopting any newly trained YOLO model, split train/valid/test into distinct images and compare it against the current `best.pt` inside the app workflow: mAP, confidence distribution, missed labels, extra candidates, and review workload.

28. Add a learner-facing model comparison/report view after real training: old model vs new model, class-by-class OK/NG quality, false positives, false negatives, and example images where the two models disagree.

29. Add more real NG examples before the next meaningful training pass. The current comparison shows the user's baseline is strong on the sample set, but the data still does not prove generalization.

30. Use the new `Test %` path with real images and rerun model comparison using `scripts\compare-yolo-models.ps1 -Task test` before replacing the current `best.pt`.

31. Use the new diagnostics while building the next real dataset: make sure NG samples are present, class counts are not heavily skewed, and the test split is non-empty before trusting a new model.

32. Guide now separates dataset training readiness from model replacement readiness. If test split is empty, training can still be used as a pipeline check, but replacing the operational `best.pt` remains on hold.

33. Candidate Review now has a ViewModel-bound model-difference example panel fed by `WpfModelComparisonReviewService`. It reads the latest YOLO model-comparison summary and saved label txt outputs, then surfaces `CandidateOnly`, `BaselineOnly`, and `ClassChanged` disagreement examples. The examples resolve source images from `data.yaml` and open through a ViewModel command. The Guide can run the comparison from the app; remaining work is to exercise it on a real held-out test split after enough labels exist.

34. Empty project startup now treats dataset preparation as the first user action. The top status bar shows `단계: 데이터셋 준비` / `다음: 데이터셋 시작`, and the Guide dataset setup card explains the selected purpose's first action before deeper tutorial content.

35. Candidate Review selected-candidate summary now states the selected 검출 후보, any overlapping current label, and the recommended action before the confirm/skip buttons. Duplicate candidates should say to inspect the existing label and skip when it is the same object; new candidates should say to confirm when correct.

36. The Guide training-result card now exposes a direct `교체 판단:` sentence before detailed metrics. Keep it as the beginner-facing decision summary: new model wins are still described as candidates until final verification examples are inspected, while missing metrics, failed comparison, or no held-out evidence must stay in a hold/recheck state.

37. Detection overlay labels now use candidate wording such as `후보 1 OK` and result-card titles such as `검출 결과`. Do not reintroduce `AI 1 OK` or `AI 검사 결과` in operator-facing canvas/review text.

38. Model comparison and model replacement are separate UX states. Comparison may run with at least one final-verification image, but below 10 final-verification images the Guide and dataset dashboard must say `근거 부족`/`주의` so users understand the result is pipeline evidence, not strong production replacement proof.

39. Candidate Review model-difference examples must read as review tasks, not report rows. The panel should say `모델 차이 예시`, and each example should include a visible `확인:` hint explaining whether the operator is checking a possible false positive, a missed object, or a class-rule difference.

40. Clicking a model-difference example should immediately answer "where do I look?" by opening the source image, drawing a selected difference box on the canvas, focusing the viewer to that box, and keeping the action hint visible in Candidate Review.

41. Model-difference example lists should scroll vertically inside the Candidate Review card. Adding more examples must not clip rows or make the image inspection area smaller.

42. Model comparison must keep the next step visible after completion: Candidate Review tells the user to click an example, inspect the image location, then return to the Guide replacement decision. Run, completion, and failure messages must be readable Korean because this is an operator workflow, not a developer log.

43. Model comparison must preflight label-list compatibility before launching YOLO validation. If the verification dataset label count does not match either the current or new model label count, stop before `val.py` and tell the operator to use models and verification data trained with the same label list. The 2026-06-28 125-image `val` run only verifies the comparison pipeline; it is not production replacement evidence because it is not a true held-out `test` split.

44. Model comparison readiness now requires final-verification answer labels, not only final-verification images. A dataset with `test/images` but no matching `test/labels` is a dataset-check issue and must keep model comparison disabled until labeled final-verification files exist. Use the smaller of test image count and test label-file count when judging the 10-image replacement-evidence recommendation.

45. The 2026-06-28 labeled test fixture verifies the app/script `-Task test` path with 10 labeled images, but it is intentionally classified as pipeline evidence only because the images were copied from the existing YOLO validation split. Do not use `artifacts/yolo-model-comparison/labeled-test-fixture-run/20260628-135515/comparison-summary.json` as production model-replacement evidence.

46. The 2026-06-28 COCO128 true held-out run verifies the model-comparison path with a physically separated public-data `test` split. It proves the `-Task test` workflow can compare two YOLO weights on held-out labels, but it is still not industrial OK/NG model-adoption evidence. Production adoption needs a real industrial OK/NG held-out split.

47. Kolektor industrial preparation now treats `*_label.bmp` as mask labels, not as user-labeling images. The verified held-out artifact has 238 train, 102 valid, and 59 test image/label pairs with 52 `Defect` boxes and 347 empty normal labels. Do not compare this 1-class dataset with 2-class operational weights or 80-class COCO weights; train matching `Defect` models first.

48. The first industrial `Defect` short-train comparison is a pipeline verification, not a usable model result. Baseline 1 epoch and candidate 3 epoch both scored mAP `0` and produced no 25% UI candidates on held-out `test`; improve the training recipe before any adoption decision.

49. Positive oversampling alone is not enough for Kolektor `Defect`. The 8x oversampled 5 epoch run produced a tiny validation recall signal but still scored held-out test mAP `0` and no 25% UI candidates. The next training attempt should change image representation, such as larger image size or padded defect boxes, before spending longer CPU time.

50. Candidate Review action wording is now part of the object-detection MVP UX: buttons should say clear actions such as previous candidate, candidate location, existing label, and next candidate. Do not shorten them back to ambiguous bare nouns when refining the right panel.

## Non-Goals

- Do not treat partial WPF hosting as the final product direction.
- Do not rewrite annotation storage, Python protocol, or YOLO training as part of the WPF migration.
- Do not reintroduce the legacy ImageBox viewer path.
- Do not put YOLO training logic directly into the C# WinForms project.
- Do not move the verified external U-Net Python runtime into the C# process or create a second competing U-Net execution path.
- Do not make UI popups part of automated verification.
