# Image Queue Action Worklist

Status: `Complete`

Date: 2026-07-22

## Goal

Keep a 10,000-image local queue actionable by exposing one visible list of images that still require operator attention. The Worklist reuses the existing queue state and filter; it does not create a second queue, database, or review workflow.

## Included scope

- Show a visible `확인 필요 Worklist` entry in the existing Image Queue panel.
- Include images that are unreviewed, not yet saved, awaiting AI-candidate review, failed inspection, or marked `수정 필요`.
- In anomaly mode, describe the same filter as unreviewed OK/NG images.
- Remove a completed row from the filtered view through WPF live filtering without resetting or rebuilding the 10,000-row source collection.
- Compute queue counts in one pass and reuse the summary for the Worklist badge and dataset status.
- When a normal annotation is saved while the Worklist filter is active, explicitly load and select the next incomplete image from the completed image's position.

## Excluded scope

- Accounts, reviewer assignment, comments, collaboration, or server synchronization.
- A new per-object history database, paging system, thumbnail cache, or duplicate queue.
- Automatic correction, automatic model adoption, or changes to image/label persistence.
- Changes to Viewer, OpenGL, ROI, brush, or eraser paths.

## Completion contract

| Criterion | Result and evidence |
| --- | --- |
| A visible entry exists | `QueueWorklistButton` is visible in the current Image Queue panel and retains an accessible name. |
| Only actionable work appears | The existing `IsCompletedQueueItem` contract is reused. Saved labels, confirmed/skipped/no-candidate rows, and completed anomaly OK/NG decisions are excluded; save-required, candidate, failed, needs-fix, requested, and unreviewed rows remain. The current EXE searched each seeded candidate/failed/needs-fix/requested row to `1/125` under the Worklist and a completed-label row to `0/125`. |
| Existing data is preserved | The 10K test keeps all 10,000 original row instances and changes only the filtered view. |
| One completion does not reload the queue | The focused test records no collection-view `Reset`, one filter evaluation, and 4 remaining visible rows after completing one of 5 Worklist rows. |
| Filter count is not stale | The visible count is derived from the same one-pass summary as the Worklist badge when there is no text search. It no longer reads the collection view before live filtering has removed the completed row. |
| Worklist focus is deterministic | The current EXE saves `queue-local-000.jpg`, changes Worklist `120 -> 119` and completion `5 -> 6`, then explicitly loads and selects `queue-local-001.jpg`. Two consecutive runs produced the same result. |
| 10K interaction stays bounded | Final run: one-pass summary `4.3ms`; single-row completion plus status update `113.3ms`; thresholds are `<250ms` and `<500ms` respectively. These are local synthetic timing gates, not shared-storage throughput claims. |
| Existing workflows remain intact | Queue status, local label-save update, click load, keyboard navigation, anomaly decision persistence, candidate presentation, and shell construction all passed. |
| Current UI remains usable | Fresh current-source captures passed at 1920x1080 and 1366x768. The Worklist does not obscure the queue rows, canvas, or existing `다음 미완료` action. |

## Verification

- Isolated test build: 0 warnings, 0 errors.
- Current app isolated-output build: 0 warnings, 0 errors.
- `--wpf-image-queue-worklist-10k`: passed with `TOTAL=10000`, `VISIBLE=4`, `FILTER_EVALUATIONS=1`, summary `4.3ms`, and update `113.3ms`.
- `--wpf-image-queue-status`: passed.
- `--wpf-image-queue-save-local-update`: passed.
- `--wpf-image-queue-click-loads-canvas`: passed.
- `--wpf-image-queue-keyboard-navigation`: passed.
- `--wpf-anomaly-purpose-flow`: passed.
- `--wpf-candidate-review-presentation`: passed.
- `--wpf-labeling-shell`: passed.
- `--exe-image-queue-worklist-smoke`: passed twice consecutively against the current isolated-build EXE after seeding candidate, failed, needs-fix, requested, and completed-label states through the existing review service. Both runs reported `images=125`, `completed=5->6`, `worklist=120->119`, `invalidations=0`, `bulkChanges=0`, and `next=queue-local-001.jpg`.
- Verified EXE SHA-256: `B62AFCDF5B7820632CACF22C185DFC23E47E9F6844F7DAC30A79B5CBE531A70D`.
- `--priority-workflow-docs`: passed.
- `git diff --check`: passed.

## UI evidence

- Before: `artifacts/ui/image-queue-worklist-20260722/before/image-queue-before-1920.png`
- After 1920x1080: `artifacts/ui/image-queue-worklist-20260722/after/image-queue-worklist-after-1920.png`
- After 1366x768: `artifacts/ui/image-queue-worklist-20260722/after/image-queue-worklist-after-1366.png`
- Actual EXE mixed Recipe: `artifacts/exe-image-queue-worklist/current-exe-20260722-categories-repeat/screenshots/01_mixed_recipe_before_worklist.png`
- Actual EXE filtered `120/125`: `artifacts/exe-image-queue-worklist/current-exe-20260722-categories-repeat/screenshots/02_worklist_filtered_120_of_125.png`
- Actual EXE save-required row retained: `artifacts/exe-image-queue-worklist/current-exe-20260722-categories-repeat/screenshots/03_save_required_remains_in_worklist.png`
- Actual EXE saved and focused next row: `artifacts/exe-image-queue-worklist/current-exe-20260722-categories-repeat/screenshots/04_saved_row_removed_and_next_focused.png`

README/tutorial image relevance was checked. The public task screenshots still document labeling actions rather than this review-filter detail, so they were not replaced. The tutorial troubleshooting text now names the Worklist directly.

## Boundary and reopening rule

This proves local queue filtering, discoverability, persisted mixed-category membership, and row-local transition behavior with a synthetic 10,000-row fixture plus a real current-EXE 125-image mixed-state Recipe. It does not prove network-share latency, multi-user coordination, or operator productivity in production. Do not add a second review system or repeat this slice unless a current Recipe reproduces a missed work category, incorrect completion transition, or unacceptable interaction delay.
