# Image Queue 10K Responsiveness Review Slice

Status: Complete as of 2026-07-18. This slice keeps image-queue cataloging and detail refresh responsive for large local folders. It is not a database, paging, network-share, or production-throughput guarantee.

## Why This Slice Exists

The queue must return control quickly when an operator changes an image root, even when the eventual folder contains many files. A late result from an older root must not overwrite a newer request, and background detail work must not monopolize the WPF dispatcher.

Included scope:

- cancellable background catalog enumeration and metadata snapshotting;
- latest-request-wins protection for catalog and detail work;
- path-indexed queue rows, lazy thumbnails, and first-image selection after the catalog is applied;
- four-worker detail indexing, 64-row dispatcher batches, visible progress, and one final exact filter refresh.

Excluded scope:

- databases, paging, persistent thumbnail caches, network-share performance promises, and a new queue architecture;
- annotation persistence, model/training/runtime behavior, Dataset Health, and external native YOLO intake.

## Review Contract

1. User-root, recipe-root, and refresh commands call the asynchronous catalog path. The old synchronous entry point remains only for deterministic callers and focused tests.
2. A new catalog request cancels both prior catalog and prior detail work. A canceled or older request cannot replace the active queue because the request version, cancellation source, and token must still match.
3. Background cataloging enumerates paths and creates lightweight `WpfImageQueueCatalogEntry` records. Thumbnail decoding is deferred until a row is realized.
4. The UI applies one replacement collection and builds an ordinal-ignore-case path index. Detail work uses that index instead of per-row linear lookup.
5. Detail work reads dimensions/review state on four background workers, applies at most 64 changed rows per dispatcher batch, updates progress, and refreshes the filtered view once at completion. Cancellation means no late row or status update is applied.
6. The measured 10K evidence is a local synthetic temporary-disk workload. It proves the stated scheduling/selection contract only; profile a representative operator folder before adding infrastructure.

## Review File Set

### Direct modifications

- `0. UI/9) WPF/Models/WpfImageQueueModels.cs` — lightweight catalog entry and shell-row construction.
- `0. UI/9) WPF/Services/WpfImageQueueSelectionService.cs` — cancellation-aware enumeration/catalog creation.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueue.cs` — catalog request lifecycle, version guard, path index, and single queue replacement.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueueCommands.cs` and `DatasetSetupCommands.cs` — operator commands use asynchronous catalog loading.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueueDetailRefresh.cs` and `ImageQueueDetailRefreshLifecycle.cs` — bounded background detail batches, progress, cancellation, and final refresh.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueuePresentation.cs`, `PanelWiring.ImageQueue.cs`, `ShellLifecycle.cs`, and `WpfLabelingShellWindow.xaml.cs` — status/panel wiring and close-time cleanup.

### Shared files: review only the marked image-queue hunks

- `tests/LabelingApplication.Tests/Program.cs` contains other active features. Limit review to `--wpf-image-queue-10k-responsive`, `--wpf-image-queue-10k-detail-responsive`, `--wpf-image-queue-operator-profile`, and `TestWpfImageQueueTenThousand*` markers, plus existing queue-specific switches used below.
- `tests/LabelingApplication.Tests/Program.ImageQueueOperatorProfile.cs` is test-only evidence tooling. It reads an explicitly supplied image root, routes recipe/review output to a temporary test directory, and can write its summary only to an explicitly supplied artifact path.
- `DatasetSetupCommands.cs`, `ShellLifecycle.cs`, panel wiring, and shell partials also contain unrelated dirty-worktree changes. Limit review to `LoadImageQueueFromRootAsync`, `CancelImageQueueCatalogLoad`, `CancelImageQueueDetailRefresh`, `imageQueueItemsByPath`, and `ImageQueue*` markers.
- `docs/NEXT_THREAD_HANDOFF.md`, `docs/STABLE_VERIFIED_AREAS.md`, and `docs/WORK_TRACKING.md` contain broad historical records. Limit review to their image-queue 10K sections.

## Acceptance Evidence

Required focused checks:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-10k-responsive
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-10k-detail-responsive
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-keyboard-navigation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-root-switch
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-large-folder-performance
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-queue-click-perf --count 125 --measure-detail-refresh --settle-ms 60
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
git diff --check
```

Current closure recheck (2026-07-18): the 10K catalog command returned in `14.2ms`, used one collection reset, left thumbnails unloaded, and rejected a stale catalog in favor of a three-image replacement root. The valid-image 10K detail scan finished in `64,554.2ms` on local temporary disk; input dispatcher work completed in `53.1ms` while it was active, and filtering performed one final 10K-row evaluation. The 1,200-item lazy-thumbnail shell load was `796.0ms`; the 125-image visible click average was `30.4ms`. Detail-scan elapsed time is environment-sensitive and is recorded as a synthetic scheduling check, not a throughput target.

Representative local-folder profile: the current-source WPF test profiled the user-provided mixed local root `D:\라벨테스트` after the first pass had warmed the OS file cache. It enumerated `50,081` supported images (`1,470,992,535` bytes): command return `13.9ms`, catalog completion `11,705.7ms`, catalog input probe `142.0ms`, DataGrid scroll dispatch `148.2ms`, middle/final selection `207.4ms`/`318.3ms`, and detail completion `406,505.9ms` after catalog. Detail input completed in `84.9ms` while detail work was active, all rows had dimensions, and process working set was `167.3MB` before, `1,030.7MB` after catalog, and `1,036.8MB` after detail. Evidence: `artifacts\image-queue-operator-profile\20260717-225226-warm-cache\profile-summary.txt` and `stdout.txt`.

This is a local mixed synthetic-package stress profile, not a network-share or production-camera throughput promise. The temporary test output root and the before/after extension inventory retained the same `50,081` images and `1,470,992,535` bytes; this count/byte check is not a full source-tree hash proof.

Additional local 8K duplicate-file queue profile: the current-source WPF test profiled `D:\새 폴더` with an explicit `--minimum-images 8000` override; the default 10K regression threshold remains unchanged. It cataloged `8,000` JPG paths (`476,177,088` bytes): command return `12.8ms`, catalog completion `2,264.8ms`, catalog input `131.1ms`, DataGrid scroll dispatch `27.2ms`, middle/final selection `182.8ms`/`121.7ms`, and detail completion `80,183.5ms` after catalog. Detail input completed in `69.9ms` while detail work was active, all rows had dimensions, and working set was `167.4MB` before, `303.1MB` after catalog, and `365.3MB` after detail. The before/after metadata manifest SHA-256 was unchanged: `072643A7ED96F109E245271AC6BDAF85D26A174BE9A1203D16B245CF462F76F9`. A complete post-profile content SHA-256 audit found exactly `250` unique image contents, each represented by `32` identical files. Evidence: `artifacts\image-queue-operator-profile\20260717-231924-local-8k-production-sample\profile-summary.txt` and `stdout.txt`.

This 8K source is on a local fixed drive and its production-camera provenance was not supplied. The metadata manifest covers relative file identity, byte length, and last-write time; it is not a before/after content-tree hash. The 250-content / 32-copy structure and a 16-file read-only sample (all 512x512 with no camera, capture-time, or software EXIF fields) mean it is not an 8,000-image independent production sample. It therefore adds duplicate-file local scheduling evidence only, not network-share latency, image-diversity, or production-camera throughput evidence.

The true historical before capture is `artifacts\ui\image-queue-10k-20260717\before-1920.png`; the fresh current-source after capture is `artifacts\ui\image-queue-10k-20260718-commit\after-1920.png`. The Image Queue remains visible without clipping at 1920x1080. The historical local 50K and duplicate-file 8K operator profiles above are retained as non-gating scheduling evidence and were not repeated for this closure.

## Completion Record

Status: `Complete`

Scope: cancellable background image catalog creation, latest-request-wins catalog/detail invalidation, one bulk queue replacement, lazy thumbnails, path-indexed detail updates, bounded four-worker/64-row detail batches, and focused test-only profiling support. This excludes database/paging/cache infrastructure, network-share promises, model/training changes, Dataset Health, and external native YOLO intake.

Acceptance criteria:

- a 10K catalog returns control promptly, applies one queue reset, retains lazy thumbnails, and rejects a stale root: passed by `--wpf-image-queue-10k-responsive`;
- a 10K valid-image detail scan leaves the input dispatcher responsive and performs one final filter refresh: passed by `--wpf-image-queue-10k-detail-responsive`;
- existing queue status, keyboard navigation, root switching, selection service, large-folder, click-performance, and shell behavior remain intact: passed by the focused commands above;
- the visible Image Queue has no 1920x1080 clipping regression: passed by the current-source after capture named above, compared with the true historical before capture.

Verification (2026-07-18 closure): isolated test-project build passed with 0 warnings / 0 errors; all focused queue and shell commands listed above passed; `git diff --check` passed. The optional real-folder profiling command remains intentionally separate and requires an operator-approved source; its prior 50K/8K records do not establish production-camera or network-share performance.

Boundary / next dependency: this proves local scheduling and selection behavior, not full-detail throughput on shared storage or a non-duplicated production-camera corpus. Obtain a representative approved folder before considering any indexing or cache expansion.

## Review Decision

Completion decision: the focused checks passed and the staged file/hunk review is limited to this Image Queue slice. The measured local 50K and duplicate-file 8K profiles prove responsive dispatcher/selection behavior but show that full detail completion remains a background operation; collect a representative network-share or provenance-confirmed production-camera profile with non-duplicated content before deciding whether indexing or cache work is justified.
