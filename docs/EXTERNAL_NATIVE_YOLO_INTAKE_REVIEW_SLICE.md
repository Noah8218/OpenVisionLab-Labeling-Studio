# External Native YOLO Intake Review Slice

Status: focused-reviewed and complete for this intake slice as of 2026-07-17. This is a review boundary only; nothing in this document stages, commits, registers, or adopts a model.

## Why This Slice Exists

The application owns recipe-exported datasets, but an operator may need to test the same labeling workflow against an already-native Ultralytics Detection or Segmentation `data.yaml`. This slice makes that input explicit and safe:

- validate the native source without copying or changing it;
- make the operator explicitly activate it for the next training run;
- fail closed if its YAML/images/labels change after activation;
- send the original YAML path to the local worker with a reproducible provenance record;
- keep outputs and transient caches outside the user-owned source tree.

It is not a source-data quality assessment, a model-quality evaluation, a model-registration/adoption flow, an external classification-YAML feature, or a general filesystem-import framework.

## Review Contract

1. `ExternalYoloDatasetSettings` is separate from the recipe-owned `YoloDatasetSettings`; selecting/activating external YAML must never change the internal export `data.yaml` or annotations.
2. `YoloExternalDatasetIntakeService` accepts directory-valued Detection/Segmentation YAML paths, resolves YAML-relative paths from the YAML directory, validates class names/splits/labels, and rejects overlap or malformed labels.
3. Source identity is SHA-256 over the YAML and referenced images/labels. Training revalidates it; a changed source deactivates the selection and blocks silent fallback until explicit revalidation and activation (or explicit clear).
4. A training request records original YAML, source fingerprint, engine/task/run, requested weight, resolved seed-weight path/SHA-256, Python path, and worker-script SHA-256. It does not register or adopt the resulting weight.
5. The local worker runs YAML training in the YAML-parent context and isolates transient Ultralytics label caches. It must restore the prior working directory and clean its temporary cache directory.

## Review File Set

### New, slice-owned files

- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ExternalYoloDatasetIntake.cs`
- `Yolo/YoloExternalDatasetIntakeService.cs`
- `tests/LabelingApplication.Tests/Program.ExternalYoloDatasetIntake.cs`
- `tests/LabelingApplication.Tests/Program.RealExternalYoloDatasetTraining.cs`

### Direct modifications

- `1. Core/LabelingProjectSettings.cs` — persisted external profile and provenance fields.
- `1. Core/YoloTrainingWorkflowService.cs` — explicit external preparation, source-identity fail-closed check, and provenance recording.
- `3. Communication/TCP/CCommunicationLearning.cs` and `3. Communication/TCP/LearningProtocol.cs` — native YAML/task/run packet fields.
- `0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs` and `WpfTrainingSettingsPanelViewModel.cs` — explicit selection/activation presentation and external split wording.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.cs`, `PanelWiring.LearningWorkflow.cs`, `TrainingStatus.cs`, `YoloTrainingCommands.cs`, `WpfLabelingShellWindow.xaml`, and `WpfLabelingShellWindow.xaml.cs` — Model Center data card, command wiring, persisted status refresh, and save boundary.
- `Runtime/Python/openvisionlab_ultralytics_worker.py` — `data_yaml_working_directory` and `label_cache_directory` only.

### Shared files: review only the marked intake hunks

- `tests/LabelingApplication.Tests/Program.cs` contains unrelated test work. Limit review to `--external-yolo-dataset-intake`, `--real-external-yolo-dataset-training`, their registry entries, and assertions named `YoloExternalYoloDataset*`.
- `Runtime/Python/openvisionlab_ultralytics_worker.py`, the WPF shell/XAML, panel wiring, and viewmodels also carry other dirty-worktree changes. Do not treat their complete diff as part of this slice; use the names in the direct-modifications list to select the relevant hunks.
- `docs/NEXT_THREAD_HANDOFF.md`, `docs/STABLE_VERIFIED_AREAS.md`, and `docs/WORK_TRACKING.md` contain broad historical/current records. Limit review to the external native intake sections and the 2026-07-17 reproducibility entry.

## Acceptance Evidence

Required focused checks:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --external-yolo-dataset-intake
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile .\Runtime\Python\openvisionlab_ultralytics_worker.py
C:\Git\yolov8\.venv\Scripts\python.exe .\Runtime\Python\openvisionlab_ultralytics_worker.py --self-test
C:\Git\yolov8\.venv\Scripts\python.exe -m py_compile C:\Git\yolov8\labeling_tcp_client.py C:\Git\yolov8\ultralyticsMaster\ultralytics\data\dataset.py
C:\Git\yolov8\.venv\Scripts\python.exe C:\Git\yolov8\labeling_tcp_client.py --self-test
git diff --check
```

Opt-in runtime evidence, not a default regression gate:

```powershell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --real-external-yolo-dataset-training --engine yolov8 --purpose segmentation --epochs 1 --image-size 320 --batch 4
```

The latest successful run is `artifacts\real-external-yolo-dataset-training\20260717-215833`. It is historical runtime evidence from the local `C:\Git\yolov8\labeling_tcp_client.py` adapter, not a replacement for a rerun with the current bundled worker. Its `summary.txt`, before/after manifests, and artifact-local `best.pt` prove that the supplied external SEG source remained exactly unchanged: 1,207 files, aggregate SHA-256 `B137A8EE8F2CAB265AA660874CC3B23C1BFA07D59CDBA0A2B74FD1DE26F98E2D`, three pre-existing `.cache` files, and zero temporary training directories before/after.

## Completion Record

Status: `Complete`

Scope: explicit native Detection/Segmentation `data.yaml` selection, validation, separate activation/persistence, source-identity fail-closed training preparation, worker cache isolation, and provenance recording. It excludes external classification YAML, source-data quality, model comparison, candidate registration/adoption, and a new training run.

Acceptance criteria:

- external YAML remains separate from recipe export and requires explicit activation: passed by `--external-yolo-dataset-intake`;
- changed source identity deactivates selection and blocks internal-dataset fallback until explicit revalidation: passed by `--external-yolo-dataset-intake`;
- original YAML/task/run/weight/Python/worker provenance is recorded and worker cache/working-directory cleanup is isolated: passed by the focused C# test and Python `--self-test`;
- Model Center Data card exposes selection, next-training activation, and clear actions without layout regression: passed by `--wpf-labeling-shell` and the current-source 1920x1080 capture `artifacts\ui\external-yolo-intake-20260718-commit\after-external-yolo-intake-1920.png`;
- real runtime source immutability remains evidenced without rerunning an unchanged one-epoch task: the historical artifact `artifacts\real-external-yolo-dataset-training\20260717-215833` contains matching 1,207-file before/after manifests and the recorded aggregate SHA-256; this is not a new current-worker reproduction.

Verification (2026-07-18 closure): isolated current-source build passed with 0 warnings / 0 errors; `--external-yolo-dataset-intake`, `--wpf-labeling-shell`, and `--priority-workflow-docs` passed; bundled worker `py_compile` and `--self-test` passed; the active local runtime adapter plus its cache-path source compiled and `labeling_tcp_client.py --self-test` passed; `git diff --check` passed.

Boundary / next dependency: this completion proves intake interoperability, provenance, and source safety only. It does not prove source-data quality, independent model accuracy, license suitability, model adoption, or YOLO11 readiness.

## Review Decision

Completion decision: the stated acceptance criteria and required focused checks are satisfied without including unrelated dirty hunks. This review is ready for the independent local feature commit; no model adoption follows from this completed review.
