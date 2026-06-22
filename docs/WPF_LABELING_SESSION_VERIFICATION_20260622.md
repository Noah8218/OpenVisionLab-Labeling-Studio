# WPF Labeling Session Verification

Date: 2026-06-22

This is the current verification record for the requested 1~5 loop.

## Scope

1. Run a realistic first labeling session instead of checking only isolated methods.
2. Verify annotation-tool UX through actual WPF canvas paths.
3. Make the YOLO learning guide clearer and less crowded.
4. Measure real image-folder queue switching performance.
5. Improve and verify candidate review confirmation UX.

## Results

| Area | Result | Evidence |
| --- | --- | --- |
| 10-minute labeling session | Pass | `--wpf-labeling-session-smoke`; creates rectangle, filled ellipse, polygon, brush mask, eraser edit, saves labels, skips duplicate AI candidate, confirms selected AI candidate. |
| Annotation tool UX gate | Pass | `scripts\verify-wpf-annotation-objects.ps1`; rectangle/ellipse selection and object behavior, segmentation object paths, visible canvas smoke. |
| YOLO guide clarity | Pass | Guide default view now keeps the actionable YOLOv5 flow visible and moves the long checklist into collapsed detail. |
| Queue click performance | Pass for visible switching | Real folder `C:\Git\yolov5\data\train\images`, 125 images. Visible avg 19.5 ms, max 26.5 ms. Image commit avg 14.2 ms. |
| Candidate review UX | Pass | Candidate Review shows selected-candidate summary, duplicate/confirmable state, selected detail, navigation, skip, and confirm state. |

## Latest Commands

```powershell
dotnet build tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj *> artifacts/logs/build-20260622-wpf-session-flow-v6.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build *> artifacts/logs/tests-20260622-wpf-session-flow-v4.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-labeling-session-smoke --output="artifacts/ui/wpf-labeling-session-smoke-20260622.png" *> artifacts/logs/visual-20260622-wpf-labeling-session-smoke-v1.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-visual-smoke --scenario guide --output="artifacts/ui/wpf-guide-flow-20260622-v2.png" *> artifacts/logs/visual-20260622-wpf-guide-flow-v2.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-visual-smoke --scenario candidate-review --output="artifacts/ui/wpf-candidate-review-summary-20260622.png" *> artifacts/logs/visual-20260622-wpf-candidate-review-summary-v1.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --folder "C:\Git\yolov5\data\train\images" --count 24 --settle-ms 80 *> artifacts/logs/perf-20260622-wpf-real-folder-session-v1.log
powershell -ExecutionPolicy Bypass -File scripts/verify-wpf-annotation-objects.ps1 *> artifacts/logs/verify-wpf-annotation-objects-20260622-session-v1.log
```

## Captures

- `artifacts\ui\wpf-labeling-session-smoke-20260622.png`
- `artifacts\ui\wpf-guide-flow-20260622-v2.png`
- `artifacts\ui\wpf-candidate-review-summary-20260622.png`
- `artifacts\ui\verify-wpf-annotation-objects-20260622-163520.png`

## Self Evaluation

- Good: image queue item selection is visually clearer, the old top preview is gone, and a single click changes the viewer through the lightweight path.
- Good: beginner guide no longer hides the labeling path under a long mixed checklist.
- Good: candidate review now says which AI candidate is selected before the user confirms or skips.
- Good: real-folder visible image switching is comfortably under the 50 ms target in this dataset.
- Watch: full dispatcher idle settling still averages above 50 ms because background-priority work continues after the image is already visible. Keep measuring this during rapid scroll/click sessions.
- Watch: the Guide right panel still contains many secondary concepts. Keep the default path lean and move deeper teaching content into tutorial/detail pages.
- Watch: real YOLO training UX is still only partially instructional. The app guides dataset readiness and inference review, but training-run comparison/export should wait until the first actual training session exposes the needed surface.

## Next Checks

1. Repeat the same queue performance command on a larger real customer-style folder.
2. Run a real YOLO training session from the Guide and record where the learner hesitates.
3. Refresh tutorial HTML screenshots after any Guide/Candidate Review layout change.
4. Keep `scripts\verify-wpf-annotation-objects.ps1` as the gate before claiming annotation-tool completion.
5. If rapid queue scrolling still feels delayed, profile background dispatcher work rather than image decode/OpenGL upload first.
