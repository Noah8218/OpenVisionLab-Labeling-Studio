# Next Thread Handoff

Last updated: 2026-07-22 KST

This is the current operational handoff for C:\Git\Labelling_Application. It is intentionally shorter than the historical journal. Use it to choose the next task; use the linked records only for the detailed evidence behind a claim.

## 1. Mandatory Start Sequence

1. Run git status --short before any other project command.
2. Read AGENTS.md.
3. Read this file.
4. Read CODEX_NEXT_PROMPT.md, docs/WORK_TRACKING.md, docs/STABLE_VERIFIED_AREAS.md, and docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md.
5. Inspect the current diff directly. The dirty worktree is more authoritative than this handoff.
6. Before editing, state the immediate priority, remaining product priority, assumptions, and verification plan.

There is no separate C:\AGENTS.md or C:\Git\AGENTS.md in this workstation snapshot. AGENTS.md in this repository is the available project instruction source.

## 2. Repository Checkpoint

- Workspace: C:\Git\Labelling_Application
- Branch: main. The current origin/main reference is 2df6b3c feat: add model adapter catalog contracts.
- All reviewed code feature slices through 85d91e9 feat: add anomaly ok-ng image review workflow are separate local commits. This documentation checkpoint follows that code baseline.
- The only known non-document residual is a three-line indentation-only hunk in WpfLabelingShellWindow.xaml. It was deliberately excluded from the feature and documentation commits; inspect it before staging or reverting.
- GitHub Actions has not been rechecked for the local commits after origin/main. Do not cite older CI evidence as current CI evidence.
- The current focused passes directly verified Dataset Health, external native YOLO intake, model/anomaly comparison, the dedicated Model Center workspace, and the explicit model-adapter catalog slices. The image-queue slice also has a 50,081-image local warm-cache profile and a separate duplicate-file local 8K profile; neither is a network-share or production-camera result.
- Never push unless the user explicitly says push. A commit request means local commit only.

## 3. Product Identity and Direction

OpenVisionLab Labeling Studio is a local Windows workstation for industrial image workflows:

- create and review object-detection, segmentation, and anomaly-classification labels;
- prepare data, train through local YOLO workers, run inference, review candidates, and compare models;
- expose model quality, evidence identity, Takt, failure examples, and adoption guards to an operator;
- keep the full workflow local and reproducible.

Each recipe is the canonical source of image identity, class schema, annotations, splits, and evidence. Selected model adapters format that recipe data for their own input contracts, then normalize their results for the same candidate-review and evidence-comparison workflow.

"Multiple models" means verified adapters for explicitly supported local runtimes or repositories. It does not mean that every GitHub model is automatically compatible: a new adapter must define class, coordinate, split, training/inference, result, and evidence mappings before it is presented as supported.

The product is not currently a cloud collaboration, account, reviewer-assignment, deployment, fleet-management, or annotation-marketplace platform.

Current direction:

1. Make labeling and the role of classes, annotations, splits, and evidence understandable for a single operator.
2. Let one recipe's labeled data be reused across supported model formats instead of relabeling the same images per model.
3. Make training/inference/model comparison evidence-based rather than claim a winner from one metric.
4. Support local YOLOv5 and local-source YOLOv8 workflows without hiding runtime ownership, and add other model adapters only through explicit format and result contracts.
5. Prefer read-only data/model analysis screens over adding more text to the main workflow panel.
6. Keep current stable workflow areas intact unless a concrete defect is reproduced.

Commercial-product lessons already adopted:

- use task-local panels and tabs rather than one long all-purpose control column;
- keep image queue, canvas, current task, and model evidence visually distinct;
- show data readiness and model comparability explicitly;
- open dense analysis in separate windows rather than extending the left workflow panel;
- do not copy cloud collaboration or enterprise governance scope without a product decision.

The latest audit estimates focused single-operator workstation maturity at 4.0/5. This is a workflow-maturity estimate, not a model-accuracy percentage. General commercial-suite breadth and enterprise/team breadth remain materially lower. Source: docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md and docs/LABELING_STUDIO_COMMERCIAL_UX_GAP_REVIEW_20260710.md.

## 4. Non-Negotiable Engineering Rules

- Preserve MVVM: code-behind is a WPF adapter; commands, workflow state, presentation decisions, and persistence rules belong in ViewModel or service code when practical.
- Do not touch Viewer, OpenGL, ROI, brush, eraser, or overlay hot paths unless the user reports a specific defect. Add focused evidence when doing so.
- Do not download model weights, run pip/package upgrades, or change dependencies without explicit approval.
- YOLO11 detection and segmentation are verified only for the recorded local Ultralytics runtime, compatible task weights, and focused app paths. Do not generalize that evidence to every YOLO11 task, dataset, runtime, or production deployment.
- ONNX can support inference deployment, but does not replace local-source YOLOv8 training.
- UI changes require current 1920x1080 evidence and a README/tutorial-image relevance check.
- Completion requires a build when code changes, focused tests for the changed area, and git diff --check.
- The public README/tutorial must not receive local private paths or conversation notes.

## 5. Local Runtime Contract

YOLOv8 is local-source operated:

- root: C:\Git\yolov8
- source: ultralyticsMaster with the local ultralytics source checkout
- Python: C:\Git\yolov8\.venv\Scripts\python.exe
- adapter: C:\Git\yolov8\labeling_tcp_client.py
- install mode: editable local install
- pretrained seeds such as yolov8n-seg.pt and yolov8n-cls.pt are seeds only, never final production models.

YOLO11 reuses the same local Ultralytics source/runtime root and the bundled
Ultralytics worker; it does not require a separate `C:\Git\yolo11` repository.
Actual detection and segmentation training/comparison evidence exists, but
task-specific compatible weights and live worker capability checks still gate
execution. A pretrained `yolo11n-seg.pt` is a seed, not an adopted model.

YOLOv5 remains a separately configured local runtime. Do not mix a weight, model engine, task, class list, or data.yaml across engines without explicit compatibility verification.

## 6. Completed and Protected Product Areas

Treat the following as completed/protected unless there is a reproduced defect:

- SEG brush-first labeling, brush/eraser performance, undo/redo, preview opacity, image-switch preview clearing, class recolor, pending-save navigation.
- Saved segmentation geometry: polygon/mask persistence, candidate review save, JSON/mask PNG/YOLO segment label export.
- YOLOv8 local worker training/inference plumbing and segmentation polygon transport. SEG inference presentation uses closed, unfilled contours with class/confidence labels; it must not fall back to a bounding box for a polygon candidate.
- Candidate Review, rejected-history guard, model-candidate decision safeguards, and separation between candidate evidence and adoption.
- Compact task-oriented shell structure: task tabs, current-work guidance, fixed canvas annotation-tool rail, and labeling-only hiding of duplicate viewer measurement chrome.
- Model-neutral comparison workspace: comparable-evidence guard, Takt/quality presentation, class and ground-truth error review, source-image/error geometry preview.
- Dataset quality audit and duplicate external-evaluation preflight.
- Per-machine splitter/layout persistence.
- Current image queue row state, keyboard navigation, light preview loading, lazy thumbnails, and 10K background catalog/detail indexing.
- Dataset Health is a separate read-only Model Center window, not another long left-panel block.
- External native YOLO data.yaml intake remains an explicitly selected training input and must not overwrite the recipe-owned exported dataset.
- The Model Center adapter catalog is read-only: it exposes only the declared recipe-format, YOLOv5, local-YOLOv8, ONNX inference-only, and verified-scope local-YOLO11 contracts. It must not imply that all GitHub models or all YOLO11 task/runtime combinations are executable.

For the exact contracts and required regression gates, read docs/STABLE_VERIFIED_AREAS.md before changing any protected area.

## 7. Committed Feature Slice Map

The former dirty worktree was split into the following reviewed local commits. Keep these ownership and verification boundaries separate when a future requirement changes one area.

### A0. Anomaly OK/NG Image Review and Folder-Name Consent - committed at 85d91e9

Files include:

- 1. Core/AnomalyImageReviewStatusService.cs
- 1. Core/AnomalyClassificationTrainingReadinessService.cs
- Yolo/AnomalyClassificationDatasetExportService.cs
- 0. UI/9) WPF/ViewModels/WpfImageQueuePanelViewModel.cs
- 0. UI/9) WPF/Views/WpfImageQueuePanel.xaml
- 0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueue.cs
- 0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.ImageQueue.cs
- 0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml.cs
- tests/LabelingApplication.Tests/Program.cs

Behavior and boundary:

- Anomaly labeling is an image-level `OK/NG 이미지 판정` workflow. The current-image actions are `정상(OK) → 다음`, `이상(NG) → 다음`, and `미판정으로 되돌리기`; they persist through the existing anomaly review status and do not require drawing.
- Anomaly purpose hides object/segmentation label-save, annotation-tool, saved-object/class, label-layer, and queue detection/batch surfaces. Its queue columns/badges show `판정`/`상태` and `OK`/`NG`/`미판정`; generic YOLO refreshes must not overwrite them. Leaving anomaly purpose restores the standard annotation workspace.
- Parent `OK`/`normal` and `NG`/`abnormal` folders are detected as an optional anomaly-review proposal, never as an implicit label. Opening a folder, checking training readiness, or exporting must leave unreviewed images unreviewed.
- The temporary Image Queue card offers `N장 일괄 판정` and `이미지별 확인`; it is not a permanent top-header action. Applying affects only unreviewed images and preserves saved manual decisions. Direct review hides the proposal for the same image-root session without persistence.
- If the selected anomaly root has no direct images, its child-folder paths are interleaved in the queue and the temporary card title reports the included total. Do not return to a path sort that makes every `NG` row appear before `OK` rows.
- When opening the automatic first nested file or selecting any queue row, retain the operator-selected image root. Never repopulate the queue from the clicked file's leaf `NG` or `OK` parent folder.
- Training readiness and classification export use saved/explicitly approved review states only. Do not restore automatic folder-state import without a new user decision and regression evidence.
- The exact staged tree for commit `85d91e9` passed the isolated 0-warning/0-error build, `--anomaly-folder-auto-review`, `--wpf-anomaly-purpose-flow`, `--anomaly-classification-dataset-export`, `--anomaly-classification-training-workflow`, `--wpf-labeling-shell`, `--priority-workflow-docs`, and diff checks. The dedicated command/persistence/purpose-switch and root-retention assertions are in `--anomaly-folder-auto-review`; UI evidence is `artifacts\ui\anomaly-ok-ng-review-20260719\before-anomaly-review-1920.png` and `after-staged-slice-1920.png`.

### A. Image Queue 10K Responsiveness - committed at 93c6bfb

Files include:

- 0. UI/9) WPF/Models/WpfImageQueueModels.cs
- 0. UI/9) WPF/Services/WpfImageQueueSelectionService.cs
- 0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueue*.cs
- DatasetSetupCommands.cs, ImageQueueCommands.cs, ImageQueuePresentation.cs, PanelWiring.ImageQueue.cs, ShellLifecycle.cs, WpfLabelingShellWindow.xaml.cs
- tests/LabelingApplication.Tests/Program.cs

Behavior:

- interactive root/recipe/refresh commands create a cancellable background catalog;
- stale catalog and detail results cannot replace a newer folder;
- row lookup is path-indexed;
- detail scan uses four background workers and 64-row UI batches;
- live filtering handles row state changes, then one final full view refresh completes exact counts;
- thumbnails remain lazy.

Direct current-session evidence:

- isolated test-project build passed with 0 warnings and 0 errors;
- latest 10K catalog recheck: 16.1ms, one collection reset, stale replacement rejected;
- latest 10K valid-image detail recheck: 70.5s on a synthetic local temporary disk, while UI input completed in 78.9ms and filtering evaluated 10,000 rows once;
- current-source warm-cache profile of the user-provided local mixed root: 50,081 images / 1.47GB; catalog return 13.9ms, catalog completion 11.7s, detail completion 406.5s after catalog, catalog/detail dispatcher input 142.0ms/84.9ms, middle/final selection 207.4ms/318.3ms, no empty dimensions, and 1,036.8MB working set after detail; evidence: `artifacts\image-queue-operator-profile\20260717-225226-warm-cache`;
- current-source local 8K duplicate-file profile of `D:\새 폴더`: 8,000 JPG paths / 476.2MB; catalog return 12.8ms, catalog completion 2.3s, detail completion 80.2s after catalog, catalog/detail dispatcher input 131.1ms/69.9ms, middle/final selection 182.8ms/121.7ms, no empty dimensions, and 365.3MB working set after detail. The before/after metadata manifest SHA-256 remained `072643A7ED96F109E245271AC6BDAF85D26A174BE9A1203D16B245CF462F76F9`. A complete content SHA-256 audit found 250 unique images, each copied 32 times; evidence: `artifacts\image-queue-operator-profile\20260717-231924-local-8k-production-sample`;
- queue status, keyboard, root switch, selection service, 1,200-item lazy-thumbnail, 125-item click-performance, and shell tests passed;
- true before/current-source after 1920x1080 captures: artifacts\ui\image-queue-10k-20260717.

Boundary: the 70.5s synthetic recheck, 50K local warm-cache profile, and local duplicate-file 8K profile are not network-share throughput promises. The 8K folder's production-camera provenance was not supplied; it has only 250 distinct contents repeated 32 times, and its before/after metadata manifest is not a content-tree hash. The local profiles retained an interactive dispatcher while full detail indexing took 6.8 minutes and 80.2 seconds respectively; do not add a database, paging system, or another image cache without a representative network-share or provenance-confirmed production-camera measurement.

Review the isolated file ownership, latest-request-wins contract, shared-file hunk boundaries, and acceptance commands in `docs/IMAGE_QUEUE_10K_REVIEW_SLICE.md` before staging or changing this slice.

### B. Dataset Health - committed at 4f65f08

Committed files include:

- 0. UI/9) WPF/ViewModels/WpfDatasetHealthViewModel.cs
- 0. UI/9) WPF/Views/WpfDatasetHealthWindow.xaml
- 0. UI/9) WPF/Views/WpfDatasetHealthWindow.xaml.cs
- 0. UI/9) WPF/Views/WpfLabelingShellWindow.DatasetHealth.cs
- Yolo/YoloDatasetHealthService.cs
- tests/LabelingApplication.Tests/Program.DatasetHealth.cs

Modified shell/XAML/ViewModel files wire the read-only Model Center entry.

Recorded scope:

- purpose-aware overview for object detection, segmentation, and anomaly classification;
- separate FluentWindow with data summary, split/label state, and class distribution;
- it reuses readiness/quality services and must not modify labels, training, inference, model registry, or adoption;
- it intentionally excludes externally selected native data.yaml aggregation.

Recorded gates are --dataset-health, --wpf-dataset-health-window, --dataset-readiness-purpose, --dataset-quality-audit, and --wpf-labeling-shell. A current-source focused recheck passed all five after an isolated 0-warning/0-error build; its fresh 1920×1080 capture is `artifacts\ui\dataset-health-20260717-current-review\dataset-health-current-1920.png`. The detailed evidence is in docs/STABLE_VERIFIED_AREAS.md. Rerun these gates before changing or committing this slice.

Review the isolated file ownership, SEG false-normal contract, shared-file hunk boundaries, and acceptance commands in `docs/DATASET_HEALTH_REVIEW_SLICE.md` before staging or changing this slice.

### C. External Native YOLO data.yaml Intake - committed at d1ce5fc

Committed files include:

- 0. UI/9) WPF/Views/WpfLabelingShellWindow.ExternalYoloDatasetIntake.cs
- Yolo/YoloExternalDatasetIntakeService.cs
- tests/LabelingApplication.Tests/Program.ExternalYoloDatasetIntake.cs
- tests/LabelingApplication.Tests/Program.RealExternalYoloDatasetTraining.cs

Related committed files include:

- WpfLearningWorkflowPanelViewModel.cs, WpfTrainingSettingsPanelViewModel.cs, TrainingStatus.cs, panel wiring, shell XAML, LabelingProjectSettings.cs, YoloTrainingWorkflowService.cs, LearningProtocol.cs, CCommunicationLearning.cs, Runtime/Python/openvisionlab_ultralytics_worker.py, and Program.cs.

Recorded behavior:

- validate a native object-detection or segmentation data.yaml, including paths, names, split separation, labels, and normalized coordinates;
- persist a separate external profile and require explicit activation for the next training run;
- persist a SHA-256 identity for the YAML plus referenced images/labels; revalidate it immediately before training, require explicit reactivation when it changes, block any silent fallback to the internal recipe dataset until reactivation or explicit clearing, send the original YAML path to the worker, and preserve the internal recipe export unchanged;
- resolve YAML-relative paths from the YAML directory;
- isolate YOLOv5 Unicode-path staging/cache and YOLOv8 cache cleanup from source data;
- latest opt-in runtime evidence: the EasyMatch SEG source completed one YOLOv8 epoch at image size 320/batch 4, while the full 1,207-file source manifest remained exactly unchanged (aggregate SHA-256 `B137A8EE8F2CAB265AA660874CC3B23C1BFA07D59CDBA0A2B74FD1DE26F98E2D`); the artifact-local copied `best.pt` is not registered or adopted;
- do not support external anomaly-classification YAML intake in this slice.

This is source-data interoperability and runtime safety, not source-data quality, model adoption, or a license to mutate user-supplied data. The detailed current evidence and gates are in docs/STABLE_VERIFIED_AREAS.md.

Completion record: this intake review is complete. The current-source isolated build passed with 0 warnings / 0 errors; `--external-yolo-dataset-intake`, `--wpf-labeling-shell`, Python worker compile, and worker `--self-test` passed. Current-source UI evidence is `artifacts\ui\external-yolo-intake-20260717-current-review\external-yolo-intake-current-1920.png`. The existing opt-in 1-epoch artifact remains the source-immutability runtime proof and was not rerun because its completed scope and evidence remain valid. No model was registered or adopted.

Review the isolated file ownership, shared-file hunk boundaries, contract, and acceptance commands in `docs/EXTERNAL_NATIVE_YOLO_INTAKE_REVIEW_SLICE.md` before staging or changing this slice.

### D. Model and Anomaly Workflow Changes - committed at 6f202db and cdad0be

Committed files include:

- 1. Core/AnomalyImageReviewStatusService.cs
- 1. Core/YoloTrainingWorkflowService.cs
- 3. Communication/TCP/CCommunicationLearning.cs
- 3. Communication/TCP/LearningProtocol.cs
- Runtime/Python/openvisionlab_ultralytics_worker.py
- scripts/compare-yolo-models.ps1
- tests/LabelingApplication.Tests/Program.RealYoloV8AnomalyFolderTraining.cs
- tests/LabelingApplication.Tests/Program.cs

Recorded outcomes:

- Washer synthetic anomaly candidate completed runtime persistence/restart evidence but is hold on external circular and MultiIndustry synthetic checks at the current confidence gate.
- EasyMatch native segmentation training/inference and native detection YOLOv5-versus-YOLOv8 comparison completed as controlled synthetic evidence. The 60-image native test favors YOLOv8n for the disclosed conditions, but neither model is auto-registered/adopted.
- Prediction manifests prevent nested path/stem collisions and restore Ultralytics generated prediction labels to original source stems for review.
- The current source packages remain unchanged according to recorded hash/cleanup evidence.

Completion record: this focused review is complete. The isolated build passed with 0 warnings / 0 errors; anomaly folder/training/runtime/evaluation gates, bundled-worker contract, local adapter/worker compile, adapter self-test, comparison run-service contract, and PowerShell parser check all passed. No real training or five-repeat comparison was rerun because it is outside this review scope. Recorded runtime/benchmark evidence must not be reported as independent quality evidence or adoption approval.

Review `docs/MODEL_ANOMALY_COMPARISON_REVIEW_SLICES.md` before staging or changing this group. It separates the nested anomaly/runtime and native comparison-manifest changes, names their shared-file hunks, excludes external-intake behavior, and states the acceptance gates.

### E. Documentation Checkpoint

The current source-of-truth documentation set includes:

- docs/NEXT_THREAD_HANDOFF.md
- CODEX_NEXT_PROMPT.md
- docs/WORK_TRACKING.md
- docs/STABLE_VERIFIED_AREAS.md
- docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md

Read the final diff before changing these records. Historical entries remain evidence journals; update only the current checkpoint and a contract whose source or acceptance criteria changed.

## 8. Model Evidence: What Is Complete and What Is Not

### Segmentation

Completed:

- historical 124-target contour correction was approved, backed up, and retrained;
- current local YOLOv8 SEG runtime returns polygon/mask artifacts;
- corrected candidate comparison and runtime smoke exist.

Not complete:

- corrected contour candidate remains hold because its fixed validation set has one NG positive image;
- no independent cross-session/production-camera segmentation test set with sufficient NG masks exists;
- do not perform another historical geometry rewrite without a new source contour, preflight, and explicit user approval.

### Object Detection

Completed:

- YOLOv5 versus YOLOv8 comparison plumbing, evidence fingerprints, model-neutral report, class/error review, and native external-YAML compatibility paths;
- controlled same-source Test01 and recent synthetic EasyMatch comparisons provide engine/regression evidence.
- user-authorized Switch Housing synthetic cross-product test: 60 held-out native object-detection images, five repeats, artifact-only relative-YAML materialization, and a candidate `hold`; the 300-image Switch Housing anomaly evaluation also holds the existing Washer classifier.

Not complete:

- independent NG-rich industrial camera/session test data is missing;
- Test01/Test02 duplication cannot count as independent evidence;
- synthetic packages do not authorize a production model choice.

### Anomaly Classification

Completed:

- image-level normal/abnormal review semantics, local YOLOv8 classification training/runtime mapping, evaluation guard, model profile persistence, and restart smoke;
- external synthetic evaluation paths demonstrate current candidate failure across domain changes.

Not complete:

- no balanced independent production-camera/cross-session normal and abnormal held-out set;
- current Washer candidate is hold, not adopt;
- evaluation data must remain outside training when used as an external regression set.

### YOLO11

Verified for the recorded local Ultralytics detection and segmentation paths.
The detection 30-epoch benchmark/restart smoke is recorded in
`docs\YOLO11_ENGINE_COMPARISON_20260721.md`; the segmentation 30-epoch
training and normalized three-model comparison are recorded in
`docs\SEGMENTATION_E30_THREE_MODEL_COMPARISON_20260722.md`. Keep compatible
task weights, worker capability, source identity, and non-adoption guards
explicit. Anomaly-classification and arbitrary external YOLO11 runtimes remain
unverified unless their own focused evidence exists.

## 9. Known Gaps, Risks, and TODO Scan

- The former dirty feature slices are committed and pushed through `58166f8`. Do not repeat their focused reviews unless source, requirements, environment, or evidence validity changes.
- GitHub Actions CI #22 passed for pushed commit `58166f8`. The current persistent-adapter/synthetic-data slice is uncommitted and therefore has no CI result yet.
- Image-queue behavior on shared/network storage and provenance-confirmed production-camera folders is unverified. Mixed local 50K warm-cache and local duplicate-file 8K profiles exist, but neither is a network result; the 8K source has only 250 distinct contents copied 32 times and is not a production-data proxy. The operator removed this unavailable profile from the active priority list on 2026-07-18; retain the risk record without treating it as next work.
- Model quality remains data-limited, not implementation-limited.
- The supplied circular-disk 500 OK / 500 NG package now has completed synthetic anomaly and exact metadata-backed object-detection evidence. It does not satisfy the independent production-camera requirement. Full record: `docs\CIRCULAR_DISK_SYNTHETIC_1000_EVIDENCE_20260720.md`.
- Collaboration, reviewer assignment, cloud sync, account management, deployment, and enterprise governance are out of scope.
- A repository source scan for TODO, FIXME, and HACK excluding artifacts/bin/obj/tutorial outputs returned no hits in this handoff pass. This does not mean every product gap is complete; use the explicit gaps above.

## 10. Next Priorities

1. Keep all completed workflow slices stable and resume implementation only for a reproduced regression or a newly approved adapter/data workflow. This prevents another broad UI pass or repeated verification of already closed contracts. It excludes speculative polish and recombining independent commits.
   Prerequisite: a reproduced defect, changed requirement, or explicit new adapter/data request.
   Recommended model: none until the prerequisite exists
   Reasoning effort: n/a

2. Convert an independent NG-rich object-detection camera/session source into a labeled held-out test split, then rerun the controlled YOLOv5 versus YOLOv8 test comparison. The `D:\라벨테스트` Switch Housing synthetic cross-product result is complete and remains `hold`; do not repeat it unless its source, weights, threshold, or acceptance criteria change. The discovered `D:\기타이미지\2022.11.16_SIT 이미지` candidate has 10,447 JPEGs (`OK` 5,950 / `NG` 4,497) but no YAML or annotation files, so it is not yet an object-detection evaluation set. This establishes separate quality evidence with provenance and no content overlap; it excludes treating image-level folder names as boxes or promoting a model from the existing synthetic report.
   The 2026-07-20 read-only audit found 9,996 unique contents, 111 duplicate-content groups, and 18 duplicate groups with conflicting OK/NG labels. No exact duplicate crosses PC1 and PC2. The source is suitable only as an image-level candidate until the operator supplies defect classes and bounding boxes; any eventual split must remove/adjudicate conflicting groups and prove content-hash separation. Full record: `docs\WORK_TRACKING.md` (`2026-07-20 independent production-camera object-detection holdout data audit`).
   The 2026-07-20 circular-disk package now also has a completed 20-epoch CPU YOLOv5s/YOLOv8n comparison on its 150-image test split: YOLOv8n measured mAP50/mAP50-95 `0.955/0.678` and 27.575ms median native takt, versus YOLOv5s `0.900/0.567` and 52.45ms. The fixed comparison preserved the 2,005-file source SHA-256 after cleaning its generated YOLOv5 cache and is explicitly `engine-benchmark`, not model adoption. The source remains single-image synthetic data, so do not count it as this independent-camera priority.
   Prerequisite: operator confirmation that this is the intended source, object classes and bounding-box labeling rules, and a completed labeled held-out split with no content overlap with training/validation data.
   Recommended model: none until the data is available
   Reasoning effort: n/a

3. Acquire balanced independent production-camera/cross-session normal and abnormal anomaly data, keep it outside training initially, and rerun the anomaly evaluation guard. This distinguishes a repeatable classifier runtime from generalizable anomaly quality; it excludes tuning against the preserved circular or MultiIndustry synthetic evaluation sets.
   The 2026-07-20 circular synthetic 1,000-image candidate remains `hold` at 90/104 confidence-gated test accuracy with seven false OK ring-deformation cases. Do not tune the threshold against that test or substitute it for new acquisition evidence.
   Prerequisite: new normal and abnormal images with provenance and representative operating conditions.
   Recommended model: none until the data is available
   Reasoning effort: n/a

Do not start another broad UI redesign without a reproduced operator defect. The task tabs, Dataset Health window, model-comparison workspace, Model Center workspace, and adapter catalog are completed contracts.

## 11. Focused Verification Menu

Use only the relevant commands for the slice being changed.

~~~powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\

dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs

dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-10k-responsive
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-10k-detail-responsive
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-root-switch
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-queue-click-perf --count 125 --measure-detail-refresh --settle-ms 60

dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-health
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-health-window
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --external-yolo-dataset-intake
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-readiness-purpose
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-quality-audit
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-adapter-catalog
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel

dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-training-workflow
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --anomaly-classification-evaluation
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolov8-anomaly-classification-runtime-smoke

C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py Runtime\Python\openvisionlab_ultralytics_worker.py
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile Runtime\Python\openvisionlab_yolo_classification_batch.py
C:\Git\yolov8\.venv\Scripts\python.exe Runtime\Python\openvisionlab_yolo_classification_batch.py --self-test
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test

git diff --check
~~~

Run real external-data training, real EXE, or model-comparison commands only when the user has approved the data/runtime cost and the task specifically needs that evidence.

## 12. 2026-07-18 Model Center Workspace Slice

- Status: `Complete` for the approved stage-4 layout scope.
- `TrainingModel` now uses a dedicated full-width Model Center; it hides the inactive canvas, image queue, splitters, and dock-collapse control only while the model stage is active. Returning to Dataset, Labeling, or Inference restores the normal workspace and its saved image-queue width.
- Current-source evidence: required isolated 0-warning/0-error build; `--wpf-labeling-shell`, `--wpf-responsive-layout`, and `--wpf-training-settings-panel` passed. Before/after evidence is `artifacts\ui\model-workspace-20260718\before-model-center-1920.png`, `after-model-workspace-1920.png`, and `after-model-workspace-1366.png`.
- Boundary: this is presentation-only. It does not add a model engine, a training/inference path, or a model-quality/adoption claim. The separately completed model-adapter catalog retains the existing YOLOv5/YOLOv8 evidence boundaries.

## 13. 2026-07-18 Model Adapter Catalog Slice

- Status: `Complete` for the declared-contract scope.
- The full-width Model Center now presents five Korean, read-only cards: implemented recipe interchange formats, YOLOv5 object detection, the local YOLOv8 worker, ONNX inference-only, and verified-scope local YOLO11. Each card declares 작업, 데이터, 실행기, 근거, and 다음 행동. YOLO11 detection and segmentation are executable only within the runtime/weight/app paths recorded in sections 7 and 16; anomaly classification and arbitrary external YOLO11 runtimes remain unverified.
- `ModelAdapterCatalogService` derives the format inventory from the implemented export capability service. It does not create a generic GitHub download/run path, alter a runtime, install a package, modify data, start training/inference, register a model, or make a quality/adoption claim.
- Current-source evidence: required isolated 0-warning/0-error build; `--model-adapter-catalog`, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, and `--wpf-responsive-layout` passed. Current captures are `artifacts\ui\model-adapter-catalog-20260718\after-model-adapter-catalog-1920.png` and `after-model-adapter-catalog-1366.png`. The closest pre-catalog baseline is `artifacts\ui\model-workspace-20260718\after-model-workspace-1920.png`; it is not a catalog-specific runtime-tab before capture.
- Boundary: do not reopen for generic model-platform breadth. Reopen only for an incorrect/missing declared contract, stale export inventory, binding failure, or reproduced layout defect. Data-dependent quality priorities remain unchanged.

## 14. Final Reporting Contract

When finishing a future task, report:

- changed files;
- exact verification commands and results;
- whether a 1920x1080 screenshot was required and the before/after paths when it was;
- what remains unverified, blocked, or risky;
- the next priority with model and reasoning-effort guidance;
- no claim of model adoption, independent accuracy, YOLO11 scope beyond its recorded evidence, or CI success without current evidence.

## 15. 2026-07-21 Latest Checkpoint: External Native Segmentation Pair

Status: `Complete` for the runtime/provenance feature slice and the controlled YOLO confidence selection plus one held-out replay; production quality remains intentionally unclaimed.

- The external-native-segmentation foundation was committed before this checkpoint. The later YOLO11 comparison extension is independently committed as `687e553`; the synthetic evidence contract is `0b05986`; MobileSAM labeling is `549a7d4`. These commits are local and not pushed.
- An explicitly activated external native YOLO segmentation `data.yaml` is now parsed only by `YoloExternalDatasetIntakeService`; `ExternalYoloSegmentationCanonicalExportService` derives recipe-owned image/mask/class artifacts under `artifacts\unet-ext`. It maps native `val` to canonical `valid`, preserves the native class order, rejects duplicate cross-split content and different-class pixel overlap, and verifies source identity before/after export.
- U-Net training and U-Net/YOLO-seg Model Center comparison use that canonical export. Any external YOLO training, even a conventional `images`/`labels` source, uses a separate app-owned runtime copy so training caches cannot change the selected source. Persisted provenance keeps both the selected source and actual runtime path distinct.
- Current actual evidence: the approved 30-epoch same-source run is complete. U-Net (CUDA) and YOLOv8-seg (installed CPU-only runtime) both used the 360/80/60 EasyMatch Die Array packet, five-class contract, image size 320, and batch 4. The original 2,004-file source tree SHA-256 and native source fingerprint remained unchanged. The 60-image Model Center common-mask report measured U-Net Dice/IoU `0.243091` / `0.156165` and YOLOv8-seg `0.079059` / `0.044103`. See `artifacts\benchmark-external-unet-die-array-e30-20260721-203302\summary.txt`, `artifacts\benchmark-external-yolov8-die-array-e30-20260721-203302\summary.txt`, and `artifacts\benchmark-external-seg-adapter-compare-e30-20260721-203302\summary.txt`.
- The saved test prediction manifests prove the original paired evidence runner deliberately used `confidence=0.00`. The actual Model Center service passes the profile confidence and falls back to `0.25`. A read-only replay of the fixed YOLOv8-seg checkpoint on the 80-image `valid` split at `0.25` (not test) reduced the all-image false-positive flood and yielded per-class Dice `0.782156`-`0.854240`. U-Net's two zero-Dice classes have train support and remain a separate class-confusion/training question. Evidence: `docs\SEGMENTATION_E30_ERROR_ANALYSIS_20260721.md` and `artifacts\segmentation-e30-error-analysis-20260721`.
- The opt-in runner now exposes `--yolo-confidence`, defaults it to `0.25`, rejects values outside `[0,1]`, and records the value in its summary. The selected `0.25` then ran exactly once on unchanged test data: U-Net Dice/IoU `0.243091` / `0.156165`; YOLOv8-seg `0.721702` / `0.570198`; source fingerprint unchanged before/after. Evidence: `docs\SEGMENTATION_E30_CONFIDENCE025_TEST_EVIDENCE_20260722.md` and `artifacts\benchmark-external-seg-adapter-compare-e30-confidence025-test-20260722`.
- U-Net class-confusion, class-weighted, crop, foreground-quality selector, and CE plus foreground soft-Dice experiments are recorded on `train`/`valid`. The selector baseline reached valid macro Dice/IoU `0.204437` / `0.127053`. The soft-Dice run recovered `foreign_particle` but left `contamination_spot` at zero overlap and reduced macro Dice/IoU to `0.189220` / `0.111142`, so it is rejected and its temporary code is removed. The selector remains an internal opt-in evidence harness; normal TCP training still uses unweighted cross-entropy and validation-loss selection. No held-out test was used by the loss experiment. Evidence: `docs\UNET_E30_CLASS_CONFUSION_ANALYSIS_20260722.md`.
- Boundary: this is an engine/model evidence result, not automatic selection or production quality. CUDA/CPU elapsed times cannot be compared. Do not rerun this held-out split unless source, runtime, acceptance criteria, or a deliberately new hypothesis changes.

Production adoption remains blocked on independently acquired camera/session data with trustworthy object-detection boxes, segmentation masks, or balanced anomaly OK/NG decisions. Product feature work is not blocked: synthetic evidence may close a feature under `docs\SYNTHETIC_EVIDENCE_CONTRACT.md`. `D:\라벨테스트` remains synthetic or lacks acceptable acquisition provenance, and the operator-excluded `D:\기타이미지\2022.11.16_SIT` path must not be inspected or used.

## 16. 2026-07-22 Latest Checkpoint: YOLO11 Segmentation and Three-Model Evidence

Status: `Complete` for the declared local runtime, fixed synthetic source, and
normalized held-out comparison. No model was adopted.

- YOLO11-seg completed 30 CPU epochs through the app's real TCP training path
  at image `320`, batch `4`, using the fixed five-class `360/80/60` native
  packet and an app-owned runtime copy.
- The source stayed 2,004 files with identical before/after tree SHA-256
  `5819E2ED72E402D3F06C32CF4F1FB3481A2DF1D70BD8CB8C00B97CE9E28199C2`.
  The YOLO11 checkpoint SHA-256 is
  `4A09B5F668B8F2AA2DAF9FEDB9ADDA4954A607D61CB08C96379AE8CA82462ECA`.
- The same 60 canonical test masks at YOLO confidence `0.25` measured mean
  Dice/IoU U-Net `0.243091/0.156165`, YOLOv8-seg `0.721702/0.570198`, and
  YOLO11-seg `0.773711/0.636553`. This is a synthetic same-source engine
  benchmark, not production evidence or automatic selection.
- The comparison runner now records an explicit YOLOv8/YOLO11 engine. A
  reproduced Windows long-path failure is closed by compact artifact names
  while manifests retain the full SHA-256 identities; the final deliberately
  long real comparison passed.
- Evidence: `docs\SEGMENTATION_E30_THREE_MODEL_COMPARISON_20260722.md`,
  `artifacts\benchmark-external-yolo11-die-array-e30-20260722\summary.txt`, and
  `artifacts\benchmark-external-seg-adapter-compare-yolo11-e30-confidence025-test-pathfix2-20260722\summary.txt`.

The subsequent U-Net CE plus foreground soft-Dice valid-only hypothesis is now
complete and rejected: same-valid macro Dice/IoU fell to `0.189220/0.111142`
and `contamination_spot` remained zero-overlap. The temporary loss path was
removed and the 60-image test set stayed closed. Production-readiness remains
blocked on independently acquired camera/session evidence, but product feature
work continues under the synthetic evidence contract.

## 17. 2026-07-22 Latest Checkpoint: MobileSAM Box Smart Mask

Status: `Complete` for the bounded local labeling-assist feature. Field
validation is `Not evaluated`; no production accuracy is claimed.

- In a segmentation recipe, the last operator-drawn rectangle can invoke
  `박스 → 스마트 마스크`. The app reuses the existing local Ultralytics runtime
  and `mobile_sam.pt`, shows one polygon as an unconfirmed AI candidate, and
  requires the normal confirm/skip flow. Confirmation invokes the existing
  canonical annotation save path.
- The assist preserves confirmed candidates, does not auto-save, and fails
  closed if the current image or prompt changes during inference. The prompt
  rectangle is removed only after its candidate is accepted into review state.
- A real synthetic defect prompt `[369,226,43,18]` produced a 44-point contour
  and 540-pixel mask through MobileSAM / Ultralytics `8.4.101` / Torch
  `2.12.1+cpu`. Confirmation wrote canonical segment JSON and mask PNG. The
  source image SHA-256 stayed
  `92202A4CBC1A6C5949FC0AE7AF9918304288FD1CC8863214010AC843EBA611D4`.
- Weight SHA-256:
  `6DBB90523A35330FEDD7F1D3DFC66F995213D81B29A5CA8108DBCDD4E37D6C2F`.
- Evidence: `docs\MOBILE_SAM_SMART_MASK.md`,
  `artifacts\mobile-sam-box-prompt\20260722-150938\mobile-sam-evidence.json`,
  and current-build prompt/candidate captures under
  `artifacts\ui\smart-mask-20260722`.
- Boundary: box prompt only. Point/negative/text prompts, multi-object automatic
  labeling, MobileSAM training, automatic confirmation, and field accuracy are
  excluded. Do not reopen for broader infrastructure unless operator use
  exposes a concrete failure of the box workflow.

## 18. 2026-07-22 Latest Checkpoint: MobileSAM 8-Class Usability Matrix

Status: `Complete` for the fixed synthetic exact-box evaluation. Field
validation and approximate operator-box tolerance are `Not evaluated`.

- One single-defect image per class was fixed from each train/valid/test split:
  24 unique images, three per each of eight defect classes.
- All 24 real MobileSAM calls produced candidates with IoU `>= 0.50`. Overall
  median IoU was `0.8562`; the lowest class median was `crack 0.7129`.
- Runtime was MobileSAM / Ultralytics `8.4.101` / Torch `2.12.1+cpu` / CPU.
  Weight SHA-256 stayed
  `6DBB90523A35330FEDD7F1D3DFC66F995213D81B29A5CA8108DBCDD4E37D6C2F`.
- The source remained 4,525 files with tree SHA-256
  `4E511A2E08F2ED609B78B40D6B789DE691C968E71ED5A298B76A1E7CA1FB52A8`
  before and after.
- Decision: keep the current box-only plus polygon/brush fallback. Point and
  negative prompts are not the next implementation priority. Reopen only after
  a real operator failure or fixed box-jitter regression crosses the recorded
  gate.
- Evidence: `docs\MOBILE_SAM_8_CLASS_USABILITY_MATRIX_20260722.md` and
  `artifacts\mobile-sam-usability-matrix\20260722-153003`.

## 19. 2026-07-22 Latest Checkpoint: Feature-Slice Commit Review

Status: `Complete`. The mixed worktree was split without discarding or
overwriting existing changes.

- `687e553 feat: add YOLO11 segmentation comparison evidence`: YOLO11 engine
  selection, compact collision-checked prediction paths, and the controlled
  three-model report. Detached-worktree isolated build passed with 0 warnings
  and 0 errors; canonical export, segmentation comparison, and Python exporter
  self-test passed.
- `0b05986 feat: define synthetic evidence completion contract`: synthetic
  completion/field-validation boundary, rejected U-Net experiment record, and
  model-comparison wording. Detached-worktree isolated build, model-comparison
  review, and priority-doc tests passed.
- `549a7d4 feat: add MobileSAM smart-mask labeling`: local box-prompt worker,
  review-first WPF flow, canonical save integration, fixed 8-class matrix,
  public tutorial, and current captures. Independent review added a missing
  prompt-coordinate equality guard so an edited prompt cannot accept a stale
  result. Detached-worktree isolated build, Python compile/self-test,
  MobileSAM contract, polygon save, WPF shell, and priority-doc tests passed.
- `origin/main` remains at `4dda0d9`. No push was performed.

Boundary / next dependency: these feature slices are complete and locally
committed. Push requires an explicit operator request. Do not repeat their
training/evaluation merely to produce another result; reopen only for changed
source/runtime/contracts or a focused regression.
