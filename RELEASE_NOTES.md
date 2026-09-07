# Release Notes

## 0.3.2 — 2026-09-07

- Consolidated the WPF Shell, queue, review, runtime, and model workflow
  responsibilities into concrete owners and canonical code-only contracts;
  framework-bound Window/XAML names remain where their composition contract
  requires them.
- Strengthened async close, cancellation, callback, and resource-lifetime
  boundaries while preserving the existing annotation, Recipe, Dataset
  Version, model-state, Python-process, and public file-format contracts.
- Added focused structural regression coverage for the refactored owners.
  Runtime WPF/DPI/GPU/field-quality evidence remains a separate gate.

## 0.3.1 — 2026-09-04

- Continued the WPF Shell refactor by moving independently testable presentation,
  catalog, persistence-adapter, and workflow policy slices to concrete owners;
  the remaining Canvas pointer/rendering and Window lifecycle code stays in the
  View where WPF requires it.
- Added close-entry protection for long-running commands and late Canvas/ROI/
  detection callbacks so shutdown cannot start new mutation work.
- Preserved the existing annotation, Recipe, Dataset Version, model-state,
  Python-process, and public file-format contracts. This is a compatible PATCH
  candidate; runtime WPF/DPI/GPU/field-quality evidence remains a separate gate.

## 0.3.0 — 2026-08-31

- Restored and versioned the integrated OpenVisionLab Labeling Studio workflow
  across dataset setup, labeling, candidate review, training, and model
  comparison while keeping the existing save and explicit-action boundaries.
- Completed the WPF review ownership wave: image quality, anomaly review, and
  Object Review now have concrete domain workflow owners; the queue presenter
  retains row icon/color/text decisions and the Shell retains UI and async
  orchestration.
- Preserved the existing annotation/Recipe persistence contracts, queue and
  anomaly transitions, close guards, and the protected `301/301` local
  regression baseline. This is a compatible MINOR candidate; production
  field-quality, GPU-target, installer/signing, tag, Release, and deployment
  evidence remain separate gates.

## 0.2.1 — 2026-08-25

- Fixed the supported Minimum `1100x720` WPF workspace so the image queue
  remains inside the window with the required right-side safety margin.
- Preserved the existing panel-width persistence contract, `맞춤` / `1:1`
  viewer actions, annotation commands, and saved-label behavior.
- This is a compatible PATCH candidate; no annotation format, Recipe schema,
  dependency, model, or deployment contract changed.

## 0.2.0 — 2026-08-24

- Versioned Public promotion candidate for the bounded WPF presentation
  release wave. The existing main-view `한국어` / `English` selector now
  refreshes the audited nested and representative auxiliary presentation
  surfaces without changing command, selection, or workflow state.
- The exercised 125% layouts keep the canvas tool rail reachable and expose
  the existing Object Review group selection, save, navigation, and reopen
  behavior through the COCO128 workflow.
- The candidate retains the protected `272/272` regression baseline and the
  existing portable self-contained release contract. Full application
  localization, exhaustive dynamic-value translation, and 100%, 150%, 175%,
  and 200% DPI coverage remain outside this candidate's verified claim.

## Unreleased

- The active `Lib.Common.dll` / `Lib.OpenCV.dll` integration has been replaced
  by OpenVisionLab Vision SDK `3.0.0` (`OpenVisionLab.Core` and
  `OpenVisionLab.Vision2D`). Template matching now uses the SDK-owned property
  and execution-result contracts, UI Bitmap/Mat conversion uses the aligned
  OpenCvSharp extension package, and the old or unused binaries are no longer
  redistributed. The complete `272/272` protected regression remains green.
- Annotation saves now fail closed across the complete image transaction.
  Existing images, detection labels, segmentation masks/JSON, Object Review
  metadata, split ownership, and changed dataset YAML are restored together
  when a later write fails. A different source image with the same filename
  stem and corrupt segmentation data are rejected before replacement.
- Dataset interchange imports now convert into an isolated staging area and
  modify the active dataset only after every item succeeds. Failed or skipped
  imports leave the previous files and class list intact. Current COCO,
  Pascal VOC, Label Studio, and CVAT detection/segmentation round trips retain
  the exercised pixel geometry, image dimensions, and non-square DPI metadata.
- Dataset readiness and Dataset Health now reject unreadable images,
  same-split filename-stem collisions, orphan annotation artifacts, and
  segmentation image/mask dimension mismatches without modifying source data.
- Large raster-mask Undo/Redo stores only the changed region, and canonical
  mask save writes bounded foreground runs instead of one pixel at a time.
  Existing save/reopen, class-index, polygon/cutout, and transaction behavior
  remains unchanged.
- Saved Recipe and dataset state can now be exported as one validated portable
  project archive and imported into new, non-overwriting targets. Export is
  blocked while annotations, mask commits, AI candidates, or named work remain
  pending; import verifies the manifest, entry set, sizes, and SHA-256 hashes,
  rebases dataset-owned paths, and leaves Recipe activation as an explicit
  operator action. External model/runtime references are disclosed but are not
  embedded in the archive.
- A bounded current-image crash-recovery journal now offers explicit
  `편집 복구` or `초안 폐기` on the next launch. Restore returns box,
  segmentation, and persistent Object Review metadata as dirty in-memory state;
  it never confirms pending AI/Smart Mask candidates or saves label files.
  Journals are atomic, checksum-verified, context-validated, limited to seven
  days, and removed after explicit label save, discard, or normal close.
- Packaged startup now keeps logs, localization state, structured diagnostics,
  and explicit support ZIP files under a bounded current-user writable root.
  `설정/도구 -> 진단/지원` provides a model-free environment self-test and a
  one-action allow-list support export that excludes images, labels, weights,
  raw configuration, and credentials by default.
- Current-source environment diagnostics now probe the actual Main Viewer
  OpenGL context, report its renderer/version and required framebuffer
  functions, and include the result in explicit support evidence. A definite
  unsupported result blocks image opening before annotation-state changes and
  directs the operator to a local GPU/OpenGL-capable Windows session.
- A versioned `0.1.1` self-contained engineering package now carries the
  graphics preflight. Its real packaged EXE passed all eight environment
  checks on the current GTX 1060 host, and post-launch manifest verification
  remains clean. Untouched startup queues no longer create an empty
  `DATA/review-status.json` beside the executable; existing dataset review
  caches remain explicitly clearable.
- Release engineering retains .NET SDK `8.0.421` and the canonical product
  version owner in `Directory.Build.props`, emits a deterministic versioned
  self-contained `win-x64` folder, ships
  project and third-party notices, verifies every payload SHA-256
  fail-closed, and publishes the verified folder as a CI artifact.
- The current distribution unit is a portable ZIP containing the verified
  self-contained folder. It is not an installer or a code-signed package;
  extract it to a new folder and verify the bundled manifest before launch.

- Object Review now supports persistent per-object `가림` and Recipe-defined
  tags for saved manual boxes and segmentation objects, with row badges,
  combined review filters, explicit reset paths, and a separate per-image
  sidecar written only through `라벨 저장`.
- Saved manual objects on one image can now form a persistent Object Review
  group with a dedicated selection preview, group badges/filtering, member
  removal/dissolve, and group-level `가림` or Recipe-tag actions. Schema-v2
  sidecars remain backward-compatible with v1 and write only on explicit
  label save.
- Integration hardening: never-loaded startup/test shells no longer open an
  operator close dialog, while loaded main windows retain safe close.
  Non-segmentation object-review row replacement/removal also preserves
  unchanged segmentation aggregate state instead of rescanning very large
  lists.

Current focus:

- Local industrial labeling workflow for object detection and segmentation.
- Independent object-detection test evidence for YOLOv5/YOLOv8 accuracy and model-Takt comparison.
- Independent production/cross-session anomaly-classification runtime evidence.
- YOLOv8 segmentation data/model operating quality.
- Clear separation between saved labels, AI candidates, trained model candidates, and the current inspection model.
- Compact WPF workflow layout for dataset, labeling, candidate review, training, and model center screens.

Recent verified areas:

- Dataset Health Visual QA canonical class filter: `전체` plus Recipe-ordered
  `index · name` classes compose with split and `문제만`; selecting a class
  rebuilds a read-only catalog bounded at 500 matching images while the
  unfiltered view keeps its existing 48-item balanced sample policy.
- Dataset Health Visual QA existing-data split filter: `전체` plus only actual
  train/valid/test values compose with `문제만`, refresh safely preserves or
  resets the selection, and balanced healthy sampling prevents a large train
  split from hiding valid/test within the bounded catalog.
- Recipe-scoped segmentation `자동 윤곽` mode: enable it once, then each new
  rectangle starts a Smart Mask candidate automatically. Candidate approval
  remains explicit; `확정` saves the displayed candidate through the canonical
  label-save path, while generation, comparison, restore, and skip do not
  write that candidate.
- Smart Mask operator guidance in README, tutorial, MobileSAM guide, and F1
  help now uses the same auto-first correction, previous/current comparison,
  explicit confirm/skip, and Recipe restore contract.
- Canvas layout auto-fit after side-panel collapse, expansion, or other
  viewport-size changes; ordinary operator zoom and pan remain unchanged.
- Local YOLOv5/YOLOv8 Detect comparison with separate runtimes, test-preferred/validation-reference split handling, and Candidate Review metrics/Takt presentation.
- Dataset-purpose-aware YOLOv8 Detect/SEG/Classification weight selection when connecting a local runtime folder.
- Segmentation brush/polygon save, reopen, and training-export paths.
- YOLOv8 segmentation local runtime plumbing and model-comparison safeguards.
- Image queue usability and save-before-navigation protection.
- Candidate Review wording and rejected-model adoption guard.
- README, release-note, CI, and known-limitations documentation skeleton.
- OpenVisionLab Vision SDK `3.0.0` now owns the active common geometry and
  OpenCV tool contracts through checked-in, hash-recorded SDK assemblies; the
  app no longer references or ships the superseded `Lib.*` binaries.

Not a release claim:

- Automatic contour is an assisted-labeling workflow, not automatic candidate
  approval or model-accuracy evidence.
- The current object-detection comparison uses validation with one NG object because the test split is empty; it is not model-adoption evidence.
- Production YOLOv8 segmentation accuracy still requires held-out evaluation on real labeled datasets.
- Anomaly detection remains an active workflow area, not a completed product mode.
- Updating the checked-in Vision SDK assemblies requires an intentional,
  versioned SDK build, hash refresh, focused integration tests, and complete
  regression verification.
