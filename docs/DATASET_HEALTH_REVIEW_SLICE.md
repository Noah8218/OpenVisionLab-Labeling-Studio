# Dataset Health Review Slice

Status: focused-reviewed and current-source verified as of 2026-07-17. This is a read-only Dataset Health boundary; it neither stages/commits code nor changes labels, training, inference, model profiles, registry records, or adoption state.

## Why This Slice Exists

Dataset Health gives an operator an on-demand summary of the recipe-owned dataset without turning the Model Center into another long editing panel. The important segmentation safety rule is that an empty box-label audit must never imply `라벨 품질: 정상`: SEG quality comes from saved segment/mask readiness, not from Detection box rows.

Included scope:

- a separate owned `데이터셋 상태 분석` FluentWindow opened from `Model Center > 데이터 > 분석`;
- read-only aggregation of existing readiness, audit, diagnostics, and anomaly-readiness services;
- purpose-specific Detection, Segmentation, and anomaly presentation;
- SEG `정상` / numeric problem count / `미확인` state and matching split-row counts.

Excluded scope:

- external native YOLO `data.yaml` aggregation (its explicit intake profile remains separate);
- annotation editing, queue navigation, output generation, training, inference, runtime changes, or model adoption;
- model accuracy, Takt, held-out comparability, and production-quality claims.

## Review Contract

1. The window reads recipe-owned saved data only. Refresh uses `YoloDatasetReadinessService`, `YoloDatasetQualityAuditService`, `YoloDatasetDiagnosticsService`, and `AnomalyClassificationTrainingReadinessService`; it must not write labels or alter workflow state.
2. Detection uses YOLO box labels and `YoloDatasetQualityAuditService` missing/invalid counts. Anomaly classification uses reviewed normal/abnormal image state and does not invent a YOLO label table.
3. Segmentation uses saved segment JSON/mask artifacts and `SegmentationObjectCountByClass`, never the box-object audit as its primary source.
4. SEG quality is `정상` only when configuration and image coverage can be evaluated and no missing/corrupt SEG annotation issue exists. A missing annotation or corrupt JSON produces a numeric problem count and a matching split-row count. Missing configuration or image coverage produces `미확인`, which is an attention state, never `정상`.
5. The Dataset Health window is owned by the shell, refreshes through its ViewModel, follows shell theme changes, and closes when its owner closes.

## Review File Set

### New, slice-owned files

- `Yolo/YoloDatasetHealthService.cs`
- `0. UI/9) WPF/ViewModels/WpfDatasetHealthViewModel.cs`
- `0. UI/9) WPF/Views/WpfDatasetHealthWindow.xaml`
- `0. UI/9) WPF/Views/WpfDatasetHealthWindow.xaml.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.DatasetHealth.cs`
- `tests/LabelingApplication.Tests/Program.DatasetHealth.cs`

### Direct modifications

- `0. UI/9) WPF/ViewModels/WpfLabelingShellViewModel.cs` — opens the owned analysis window through a command.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.cs` — command binding.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ShellStatus.cs` and `ShellLifecycle.cs` — theme propagation and owner-close cleanup.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml` — Model Center `데이터 > 분석` entry only.
- `tests/LabelingApplication.Tests/Program.cs` — `--dataset-health`, `--wpf-dataset-health-window`, and the Dataset Health visual-smoke option only.

### Shared files: review only the marked Dataset Health hunks

- The shell XAML, shell ViewModel, panel wiring, shell-status/lifecycle files, and `Program.cs` also contain unrelated dirty-worktree work. Do not approve their complete diff as this slice; limit review to `DatasetHealth`, `OpenDatasetHealthWindowButton`, `OpenDatasetHealthCommand`, and `--open-dataset-health` markers.
- `docs/NEXT_THREAD_HANDOFF.md`, `docs/STABLE_VERIFIED_AREAS.md`, and `docs/WORK_TRACKING.md` include unrelated history. Limit review to their Dataset Health sections.

## Acceptance Evidence

Required focused checks:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-health
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-health-window
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-readiness-purpose
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --dataset-quality-audit
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
git diff --check
```

The focused Dataset Health test covers Detection, normal SEG, missing SEG annotation, corrupt SEG JSON, unevaluated SEG, and anomaly fixtures. In particular, missing/corrupt SEG asserts a numeric quality problem and `미확인` asserts an attention state, preventing a false-normal presentation.

Current-source UI evidence is `artifacts\ui\dataset-health-20260717\after-dataset-health-populated-ready-1920.png`. The focused corrupt-SEG evidence is `artifacts\ui\dataset-health-seg-quality-false-normal-after.png`, which shows `라벨 품질: 1`, not `정상`.

Focused review recheck: the isolated current-source build passed with 0 warnings / 0 errors, and `--dataset-health`, `--wpf-dataset-health-window`, `--dataset-readiness-purpose`, `--dataset-quality-audit`, and `--wpf-labeling-shell` all passed. A fresh 1920×1080 current-source capture is `artifacts\ui\dataset-health-20260717-current-review\dataset-health-current-1920.png`; its no-data fixture shows `라벨 품질: 미확인`, not `정상`, and the overview has no clipping. This supplements the corrupt-SEG test assertion; it does not turn the capture into a model-quality claim.

## Review Decision

Approve this slice only if the read-only and purpose-specific contract holds and the focused checks pass without absorbing unrelated shared-file hunks. A later commit requires separate user authorization and must not be treated as model-quality or adoption approval.
