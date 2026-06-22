# WPF Annotation Object Verification

Date: 2026-06-22

This is the required verification process before saying an annotation tool is done.

## Rule

An annotation tool is not verified by drawing something once.

It is verified only when all of these pass:

1. Empty-space drag creates one new object.
2. Existing-object inside click selects the object and does not create another object.
3. Existing-object edge/corner drag resizes the same object.
4. Existing-object inside drag moves the same object.
5. The selected object keeps visible edit handles after click.
6. Zoom, pan, and fit do not change the stored image-pixel geometry.
7. Object Review and save/export receive the same object geometry.
8. Automated tests and visual smoke capture are updated.

The key UX sentence is: inside click selects, empty drag creates.

## Current Required Cases

| Tool | Empty Drag | Inside Click | Move | Resize | Fill | Save/Export |
| --- | --- | --- | --- | --- | --- | --- |
| Rectangle | creates box | inside click selects | required | required | none | YOLO bounding box |
| Ellipse/Circle | creates filled ellipse | inside click selects | required | required | translucent fill | YOLO bounding box |
| Polygon | click points to draft polygon | near-start/double-click closes draft | selected point move verified | point delete/add-on-edge pending | translucent polygon fill | segmentation polygon through `BuildAnnotationSegments` |
| Brush | click/drag paints raster mask | selected mask appears in Object Review and receives a topmost canvas marker | selected mask move verified | brush size changes mask radius and hover preview; mask reshape pending | translucent OpenGL mask texture preview plus cursor radius | raster mask through `BuildAnnotationSegments` |
| Eraser | click/drag erases raster mask pixels | selected mask remains in Object Review unless empty | not applicable | brush size changes erase radius | removes fill pixels | updated or removed raster mask |

## Automated Verification

The test `TestWpfAnnotationObjectVerificationProcess` covers the first WPF object interaction gate:

- Rectangle inside click selects the existing ROI while box drawing mode is active.
- Rectangle inside click does not add a new ROI.
- Rectangle move/resize coordinate delta is checked through the real mouse-event path.
- Empty-space rectangle drag adds exactly one rectangle ROI.
- Empty-space ellipse/circle drag adds exactly one filled ellipse ROI.
- Ellipse/circle inside click selects the existing ROI and does not add a new ROI.
- Ellipse/circle move/resize coordinate delta is checked through the real mouse-event path.
- The case runs with the debug group frame hidden, matching the WPF shell default.
- `TestWpfPolygonShellInputCreatesSegmentation` covers WPF polygon click input, draft preview overlay, near-start completion, object review row, selected point movement, and segmentation export dictionary.
- `TestWpfPolygonAnnotationService` covers image-pixel clamping, close distance, segmentation object creation, point hit-test, point move, and save/load through the segmentation annotation path.
- `TestWpfMaskAnnotationService` covers raster-mask brush paint, drag interpolation, changed bounds, selected mask move, center-pixel erase, and empty-mask removal.
- `TestWpfBrushEraserShellInputCreatesMaskSegmentation` covers WPF brush/eraser image-point input, OpenGL mask texture preview overlay, selected mask marker state, selected mask move, Object Review `Mask` row, and `BuildAnnotationSegments` raster-mask export.

The focused ROI object gate is `--wpf-roi-object-verification`.

It covers:

- Rectangle inside move.
- Rectangle left/right/top/bottom edge resize.
- Rectangle left-top/right-top/right-bottom/left-bottom corner resize.
- Empty-space rectangle draw.
- Empty-space filled ellipse/circle draw.
- Ellipse/circle inside move and corner resize.
- Small ROI inside drag stays move, not resize.
- ROI move clamps to image bounds without changing size.
- WPF shell `manualRois`, Object Review state, and export ROI receive the edited geometry.

Run only this gate:

```powershell
dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration Debug --no-build -- --wpf-roi-object-verification
```

The focused segmentation object gate is `--wpf-segmentation-object-verification`.

It covers:

- Polygon point hit-test and point movement.
- Polygon body hit-test and whole-polygon movement.
- Polygon movement clamps to image bounds.
- Polygon Object Review selection routes body drags to the selected object.
- Polygon export receives the edited image-pixel geometry.
- Brush paint creates a raster mask and OpenGL texture overlay.
- Mask movement preserves mask size and clamps to image bounds.
- Eraser removes raster-mask pixels and empty masks disappear.
- Mask Object Review selection routes image-pixel drags to the selected mask.
- Mask export receives the edited image-pixel geometry.

Run only this gate:

```powershell
dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration Debug --no-build -- --wpf-segmentation-object-verification
```

The combined annotation object gate is `--wpf-annotation-object-verification`.

Run ROI and segmentation gates together:

```powershell
dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration Debug --no-build -- --wpf-annotation-object-verification
```

## Manual Smoke

Run:

```powershell
scripts\verify-wpf-annotation-objects.ps1
```

For the focused ROI object interaction gate, run:

```powershell
scripts\verify-wpf-roi-object-interactions.ps1
```

For the focused segmentation object interaction gate, run:

```powershell
scripts\verify-wpf-segmentation-object-interactions.ps1
```

For the combined ROI and segmentation object gate, run:

```powershell
scripts\verify-wpf-annotation-object-interactions.ps1
```

Then manually check the captured WPF screen and, when needed, the app itself:

1. Select `박스`.
2. Drag on empty canvas: one rectangle appears.
3. Click inside that rectangle: handles remain on the same rectangle; no new rectangle appears.
4. Drag inside that rectangle: the same rectangle moves.
5. Drag a corner/edge: the same rectangle resizes.
6. Select `원/타원`.
7. Drag on empty canvas: one filled ellipse appears.
8. Click inside that ellipse: handles remain on the same ellipse; no new ellipse appears.
9. Select `폴리곤`.
10. Click three or more image boundary points: a blue draft polygon appears.
11. Click near the first point or double-click: the polygon becomes a confirmed object and appears in Object Review.
12. Select `브러시`.
13. Drag on the image: a mask region appears and Object Review lists a `Mask` row.
14. Click the `Mask` row in Object Review: the same mask receives a visible canvas marker, and stale ROI handles should not remain as the active selection.
15. Select `지우개`.
16. Drag across the mask: the painted pixels are removed; an empty mask disappears from Object Review.
17. Select a `Mask` row and use the Select tool: dragging inside the mask moves the same raster mask instead of creating a new object.
18. Select a polygon row and use the Select tool: dragging a polygon point moves that point in image coordinates.

If any step fails, the tool stays unfinished.

For any code change that touches annotation interaction, object review, OpenGL annotation overlays, ROI geometry, polygon/mask editing, or annotation save/export, the completion gate is:

```powershell
scripts\verify-wpf-annotation-object-interactions.ps1
```

Reporting the work as done before this gate passes is not allowed. If the focused gate fails, fix the behavior first and rerun the gate.

## Completion Rule

Do not mark a new annotation tool as `가능` until this file and the automated tests include that tool's create/select/move/resize/save behavior.
