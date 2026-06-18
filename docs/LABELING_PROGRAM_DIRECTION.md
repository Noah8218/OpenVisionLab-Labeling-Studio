# Labeling Program Direction

## Product Role

This application should be the Windows labeling workstation. It should own image browsing, class management, annotation editing, YOLO label persistence, dataset folder creation, OpenGL visualization, detection overlays, and operator logs.

Python should own model-specific work: training, weight management, inference execution, GPU/runtime dependencies, and model upgrades. The C# application should communicate with Python through a small protocol and should not embed YOLO runtime logic.

## Architecture Direction

- Keep the app as WinForms and use the labeling-owned `OpenVisionLab` library copy where it fits the existing UI model.
- Use OpenGL `CViewer` as the only image viewer path. Legacy ImageBox/OpenCvSharp UI viewers should stay removed.
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

The next protocol target is a versioned message shape:

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

Until Python is updated, the current `ResultDefect [...]` format remains supported through `PythonDetectionResultProtocol`.
The current `C:\Git\yolov5\labelling_tcp_client.py` emits v1 `DetectionStatus` and `ResultDefect` envelopes for the pretrained KTEM model path.

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
19. `scripts\smoke-yolo-tcp.ps1` validates the real Python TCP client by sending a `StartDefect` PNG packet and requiring a v1 `ResultDefect` response from the pretrained KTEM model.
20. When candidates arrive, the right review panel switches to the `AI 후보` tab and shows candidate count, confirmable count, selected state, and rejected candidate count.
21. `scripts\smoke-yolo-workflow.ps1` validates the full C# workflow with the real Python client: `StartDefect`, OpenGL candidate overlays, candidate confirmation, saved YOLO label output, and persisted `review-status.json`.

Detection candidates are removed after confirmation or skip, and the remaining candidates are reindexed so the same model result cannot be confirmed repeatedly into duplicate labels.

## UI Direction

- The main shell is `FormMainFrame`; do not reintroduce legacy frame naming for the product frame.
- Keep the visual direction close to the OpenVisionLab workbench style only where it supports labeling: compact command buttons, restrained chrome, and a workbench-style dark image area. Avoid carrying over inspection-machine UI concepts that make labeling feel like layer/document management.
- Keep labeling-specific controls in the shell only when they are global workflow controls. Image-list review and detection state should stay in services where possible.
- Present the main workspace as a fixed labeling workbench: left dataset queue, center OpenGL labeling canvas, and right object review panel.
- Keep DockPanel only inside the center canvas host for the internal `Main` display. The left and right work areas should be ordinary hosted panels, not layer/document panes.
- Present the center viewer as a single fixed labeling canvas. The internal `Main` display may still be implemented through DockPanel, but the operator-facing caption should be `작업 캔버스`, not a Dev-style layer name.
- Treat the left panel as the dataset/image queue, not a generic file explorer.
- Treat the right panel as object review. Current objects, AI candidates, and the class catalog should be separate tabs instead of one mixed layer/class list, and the panel should show selected item details such as class, confidence, bounds, and confirmability.
- Keep logs available through the Dev log control, but do not let the log pane consume the default labeling workspace.

## Near-Term Refactoring Queue

1. Expand the status bar into a fuller operator panel only if the compact text becomes insufficient during use.
2. Continue replacing direct form/viewer field access with service methods.
3. Keep `samples/python_protocol`, `mock_yolo_client.py`, `scripts\smoke-yolo-tcp.ps1`, `scripts\smoke-yolo-workflow.ps1`, and `C:\Git\yolov5\labelling_tcp_client.py` updated so the Python side and full C# workflow can be tested independently.
4. Keep the `Python Model` settings tab focused on process/path configuration. Do not move model execution or dependency management into the C# app.
5. Use `C:\Git\yolov5\best.pt` as the current pretrained KTEM detection default and `C:\Git\py\KtemData` as the current image-root default.
6. Add batch-assisted labeling for the image list: run detection per selected image, cache candidate status, and let the operator review image by image.
7. Continue improving operator-visible candidate filtering and batch-assisted review status in the right panel.
8. Extend batch detection with richer visual progress affordances such as per-row progress icons or a dedicated batch summary strip.
9. Keep release publish validation and fallback-DLL validation as required checks.

## Non-Goals

- Do not migrate to WPF for the main product shell.
- Do not reintroduce the legacy ImageBox viewer path.
- Do not put YOLO training logic directly into the C# WinForms project.
- Do not make UI popups part of automated verification.
