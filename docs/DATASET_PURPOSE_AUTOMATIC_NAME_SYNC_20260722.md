# Dataset Purpose Automatic Name Sync (2026-07-22)

Status: Complete

## Goal

When the operator changes the dataset purpose in the creation wizard, keep the
untouched generated Recipe name and default storage path aligned with the new
purpose. Never overwrite a Recipe name or storage path that the operator edited.

## Contract

- The path service reports whether the initial Recipe name was generated or
  came from an available operator/current value.
- Automatic synchronization is enabled only for a genuinely generated initial
  name.
- A purpose change resolves a new collision-checked generated Recipe name.
- The default storage path follows the new generated name only while that path
  is also untouched.
- Once the operator edits the Recipe name or storage path, that field remains
  manual even if its text is later restored to the original generated value.
- Existing Recipe contents, source images, labels, model settings, and manually
  selected paths are not changed.

## Current-EXE Evidence

Before selecting anomaly purpose, the wizard retained a segmentation token:

`artifacts/ui/beginner-e2e-audit-20260722/03-anomaly-current-exe-after/screenshots/01b_dataset_purpose_selected.png`

After the fix, both generated fields identify `AnomalyDetection`:

`artifacts/ui/dataset-purpose-auto-name-20260722/after/screenshots/01b_dataset_purpose_selected.png`

The final current-EXE run then created Recipe
`codex_yolov8_anomaly_restart_20260722_185133`, saved its YOLOv8 profile,
closed and restarted the application, completed first inference with one
candidate, and persisted `Abnormal`.

Full run evidence:

- `artifacts/ui/dataset-purpose-auto-name-20260722/after/summary.txt`
- `artifacts/ui/dataset-purpose-auto-name-20260722/after/screenshots`

## Verification

- Isolated test-project Debug build: pass, 0 warnings / 0 errors.
- `--wpf-dataset-setup-ui`: pass; covers automatic name/path synchronization,
  operator-edited name preservation, edit-then-restore preservation, manually
  selected storage preservation, and manual initial-name preservation.
- `--wpf-dataset-setup-request`: pass.
- Current app Debug build: pass, 0 warnings / 0 errors.
- Final current-EXE `--exe-yolov8-anomaly-restart-smoke`: pass.
- Final focused shell/documentation checks and `git diff --check`: pass.

## Completion Record

Status: Complete

Scope: synchronize only untouched generated Recipe/storage defaults when the
dataset purpose changes and preserve every operator-edited value.

Acceptance criteria: automatic generated name follows selected purpose -> pass;
untouched default storage follows generated name -> pass; manual Recipe name ->
preserved; edit then restore original generated text -> preserved; manually
selected storage -> preserved; current-EXE create/restart/inference flow -> pass.

Evidence: this document and
`artifacts/ui/dataset-purpose-auto-name-20260722/after`.

Boundary / next dependency: this changes wizard defaults only. It does not
rename existing Recipes, move storage folders, modify data, change model
selection, or claim model accuracy. Do not reopen without a reproduced naming
or path-preservation regression.
