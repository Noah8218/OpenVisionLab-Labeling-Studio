# WPF Annotation Tool Validation

Date: 2026-06-22

This file records what is actually verified for the WPF labeling tool palette.

- OpenGL drawing primitives alone do not make a complete labeling tool.
- A tool is `Done` only when WPF input, canvas display, object review, persistence, and tests agree.
- Object interaction verification lives in `docs/WPF_ANNOTATION_OBJECT_VERIFICATION.md`.

## Validation Criteria

1. WPF input path exists for the tool.
2. The visible WPF/OpenGL canvas reflects the edit.
3. The edit updates object review or candidate review state.
4. The edit can be saved in the expected label format.
5. Automated tests cover the action or deliberately keep the tool pending.

## Current Matrix

| Tool | Badge | Validation Result | Evidence |
| --- | --- | --- | --- |
| Select | Done | Connected for WPF object selection/edit flow. | ROI edit/move uses the existing ROI interaction path. Selected raster masks move through image-pixel drag. Selected polygon points move through image-pixel drag. |
| Box | Done | Connected and tested as the primary YOLO label tool. | `CanvasInteractionMode.Drawing` creates rectangles through `RoiInteractionMouseUp.AddRectangleToOverlay`; `RoiAdded` feeds WPF label state and YOLO save tests. |
| Ellipse/Circle | Done | Connected through the same pixel-space ROI path as box labels. | WPF sets `DrawingShapeKind = CanvasRoiShapeKind.Ellipse`; OpenGL renders a filled translucent ellipse; YOLO export uses the bounding box. |
| Pan | Done | Connected as viewer movement. | WPF shell routes Pan to `ImageViewer.SetViewMode(CanvasInteractionMode.Drag)`. Pan tests confirm it avoids per-event pixel readback. |
| Delete | Done | Connected through object review selection. | `WpfObjectReviewEditService.TryDelete` updates manual/confirmed object collections and tests cover deletion state. |
| Polygon | Done | Connected as an image-pixel segmentation polygon tool. | Click input, draft/confirmed overlays, near-start/double-click close, object review rows, class edit/delete, selected-point move, and segmentation save are tested. |
| Brush | Done | Connected as an image-pixel raster-mask paint tool. | Brush click/drag, hover cursor preview, raster mask creation, OpenGL texture preview, selected-mask marker, selected-mask move, and segmentation save are tested. |
| Eraser | Done | Connected as an image-pixel raster-mask erase tool. | The same image-point input path erases mask pixels, recalculates bounds, removes empty masks, refreshes Object Review, and keeps save state aligned. |
| Undo | Done | Connected through the WPF annotation history stack. | `WpfAnnotationHistoryService` snapshots ROI, polygon/mask segments, pending AI candidates, and confirmed AI candidates before edits. The Guide palette disables Undo when the stack is empty. |
| Redo | Done | Connected through the WPF annotation history stack. | `RedoWpfAnnotationHistory` reapplies WPF snapshots without calling legacy `CViewer.RedoAnnotationChange`. The Guide palette disables Redo when the stack is empty. |

## Verification Notes

- `CanvasInteractionMode` currently has `None`, `Drawing`, `Edit`, `Move`, `Drag`, and `Measure`.
- While a drawing tool is active, existing ROI hit-test has priority over new drawing: inside click selects, empty drag creates.
- Selected raster masks and selected polygon points are edited in source image pixels; zoom/pan/fit only changes screen projection.
- Raster-mask OpenGL preview uses full texture upload for new/shape-changing masks and `glTexSubImage2D` for same-bounds dirty-region updates.
- All palette tools currently have a verified first WPF path.
- Do not route a WPF palette action through legacy `CViewer` unless the visible WPF canvas, review panel, and save path are updated by the same action.

## Pixel-Space Annotation Direction

The source of truth must be the original image coordinate system, not the OpenGL screen coordinate system.

1. Mouse input is converted from viewer/screen coordinates into image pixel coordinates.
2. Annotation models store pixel-space geometry:
   - box: `x`, `y`, `width`, `height`
   - ellipse/circle: bounding rectangle
   - polygon: ordered image points
   - brush/eraser: a mask buffer with the same width and height as the source image
3. OpenGL renders those models by applying the current zoom/pan transform each frame.
4. Zoom, pan, and fit never modify annotation geometry.
5. Save/export converts the pixel-space model into the target format.

## Next Verification Order

1. Polygon point delete/add-on-edge: add only if the tutorial flow needs it after point move is used.
2. Mask object UX: move is connected. Decide only the needed reshape/detail editing behavior after real brush/eraser use.
3. Dirty/saved wording/color: tune only if real labeling sessions show users miss the bottom status chip.

## Latest Verification

- Build: `artifacts\logs\verify-wpf-annotation-objects-build-20260622-163520.log`
- Tests: `artifacts\logs\verify-wpf-annotation-objects-tests-20260622-163520.log`
- Visual smoke: `artifacts\ui\verify-wpf-annotation-objects-20260622-163520.png`
- Full session smoke: `artifacts\ui\wpf-labeling-session-smoke-20260622.png`
- Session verification record: `docs\WPF_LABELING_SESSION_VERIFICATION_20260622.md`
- Previous save-state smoke: `artifacts\ui\wpf-annotation-save-state-20260622.png`
- Previous segment edit smoke: `artifacts\ui\wpf-segment-edit-20260622.png`
- Undo/Redo runtime smoke: `artifacts\ui\wpf-undo-redo-runtime-state-20260622.png`
- Previous visual smoke coverage:
  - `artifacts\ui\wpf-mask-selected-object-20260622-v3.png`
  - `artifacts\ui\wpf-brush-cursor-preview-20260622.png`
  - `artifacts\ui\wpf-mask-texture-objects-20260622.png`
  - `artifacts\ui\wpf-polygon-connected-objects-20260621.png`
  - `artifacts\ui\wpf-polygon-connected-guide-20260621.png`
  - `artifacts\ui\wpf-mask-tools-guide-20260621.png`
  - `artifacts\ui\wpf-undo-redo-guide-20260622.png`
