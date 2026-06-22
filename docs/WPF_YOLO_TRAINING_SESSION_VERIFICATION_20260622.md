# WPF YOLO Training Session Verification

Date: 2026-06-22

This record covers the latest priority pass for the beginner YOLOv5 path.

## Scope

1. Check the learner flow from image loading to labels, dataset readiness, training start, worker status, `best.pt` application, and inference review.
2. Make the app understand the current Python worker training status messages.
3. Keep the Guide tab focused on the actionable path and move deeper concepts out of the default route.
4. Make Candidate Review post-action behavior explicit.
5. Refresh the HTML tutorial screenshots with the current WPF UI.

## Results

| Area | Result | Evidence |
| --- | --- | --- |
| App-driven YOLO training session | Pass | WPF smoke creates train/valid labels, sends `StartTraining`, receives worker status, applies latest `best.pt`, and moves to candidate review. |
| Python training status parsing | Pass | `TrainingStatus`, `TrainYoloResult`, and `TaskStatus` training messages are parsed into the same C# training-status snapshot. |
| Training progress UI | Pass | After start, the WPF training panel polls worker status until terminal state and shows `학습 완료 / 100%`. |
| Candidate Review policy | Pass | The UI now states that confirm/skip moves to the next candidate. This matches the existing review flow. |
| Guide separation | Pass | The right Guide tab keeps the beginner YOLOv5 path visible, with deeper concept controls under `심화 개념`. |
| Queue performance | Pass | Real 125-image folder: visible avg 21.4 ms, max 42.8 ms. Generated 240-image queue: visible avg 18.8 ms, max 33.1 ms. |
| Tutorial screenshots | Pass | `docs\tutorial\images\01-guide.png` through `06-inference-review.png` were regenerated sequentially to avoid overlapping WPF windows. |

## Latest Commands

```powershell
dotnet build tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj *> artifacts/logs/build-20260622-priority-pass-final-v1.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build *> artifacts/logs/tests-20260622-priority-pass-final-v1.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-yolo-training-session-smoke --output="artifacts/ui/wpf-yolo-training-session-smoke-20260622.png" *> artifacts/logs/visual-20260622-wpf-yolo-training-session-smoke-v1.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --folder "C:\Git\yolov5\data\train\images" --count 80 --settle-ms 40 *> artifacts/logs/perf-20260622-wpf-real-yolov5-80-v1.log
dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --count 240 --settle-ms 40 *> artifacts/logs/perf-20260622-wpf-generated-240-v1.log
```

## Captures

- `artifacts\ui\wpf-yolo-training-session-smoke-20260622.png`
- `artifacts\ui\wpf-candidate-post-action-policy-20260622.png`
- `artifacts\ui\wpf-guide-deep-concepts-collapsed-20260622-v2.png`
- `docs\tutorial\images\01-guide.png`
- `docs\tutorial\images\02-image-queue.png`
- `docs\tutorial\images\03-classes.png`
- `docs\tutorial\images\04-labeling.png`
- `docs\tutorial\images\05-yolo-settings.png`
- `docs\tutorial\images\06-inference-review.png`

## Self Evaluation

- Good: the app-level training path is now tested from WPF button click through TCP worker response and `best.pt` application.
- Good: the Python worker's current `TaskStatus` and `TrainYoloResult` messages no longer fall through as unknown messages.
- Good: Candidate Review no longer hides the auto-next policy.
- Good: the main workspace stays focused; the beginner guide and tutorial live in the right Guide tab and HTML page.
- Watch: this pass used a mock training worker for a fast deterministic UI/protocol check. A long real YOLOv5 `train.py` run should be performed after enough real labels exist.
- Watch: full dispatcher settled time can still exceed 50 ms, but visible image switching remains under the target on the checked folders.

## Next Checks

1. Run a real YOLOv5 training session with the actual Python project and enough labels to produce a meaningful `best.pt`.
2. Add a small training-run history/detail view only if the compact status and log are not enough during that real run.
3. Keep refreshing tutorial screenshots sequentially whenever the right-side tabs change.
4. Re-run the 125-image and generated large-queue performance smoke after any queue, preview, or viewer change.
