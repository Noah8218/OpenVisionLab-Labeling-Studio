# Beginner End-To-End UX Audit — Current EXE (2026-07-22)

Status: Complete

## Goal

Verify, from a first-time operator's point of view, that the current built EXE
exposes a usable labeling path for object detection, segmentation, and anomaly
detection. Record current-build evidence and fix only defects reproduced by
those paths.

This is a workflow audit. It is not a model-accuracy, production-adoption, or
enterprise-platform claim.

## Documentation Authority Used

1. `AGENTS.md`: operating and completion rules.
2. `docs/NEXT_THREAD_HANDOFF.md`: current project handoff.
3. `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`: current scope, maturity, and
   commercial comparison.
4. `CODEX_NEXT_PROMPT.md`: bounded next action after the three documents above.

The product identity is a local Windows industrial labeling, training,
inference, review, and model-evidence workstation. A recipe owns canonical
images, classes, annotations, splits, and evidence; explicit adapters translate
that data into verified model contracts. The operator remains responsible for
final review.

## Included And Excluded Scope

Included:

- build the current WPF application;
- exercise object-detection box labeling in the current EXE;
- exercise segmentation brush/eraser labeling in the current EXE;
- exercise anomaly close/restart/first-inference and persisted OK/NG state in
  the current EXE;
- correct reproduced accessibility and task-language defects;
- retain screenshots and focused regression checks.

Excluded:

- retraining or selecting a model;
- changing dataset contents or adoption decisions;
- broad UI redesign;
- cloud collaboration, accounts, reviewer assignment, deployment, and
  enterprise governance.

## Current-EXE Results

| Workflow | Result | Measured evidence | Screenshot |
|---|---|---|---|
| Object detection | Pass | Four boxes created; average input `209.3 ms`, maximum `233.4 ms`; selected row visible and delete enabled; delete/wheel UI response `58.3 ms`. | `artifacts/ui/beginner-e2e-audit-20260722/01-object-detection-current-exe.png` |
| Segmentation | Pass | Four brush strokes and two eraser strokes; brush average `252.2 ms`, eraser average `172.6 ms`; immediate wheel UI response `18.0 ms`. | `artifacts/ui/beginner-e2e-audit-20260722/02-segmentation-current-exe.png` |
| Anomaly | Pass | YOLOv8 profile restored after close/restart; first inference returned one image-level candidate; `Abnormal` persisted. | `artifacts/ui/beginner-e2e-audit-20260722/03-anomaly-current-exe-after/screenshots/04_first_abnormal_inference_after_restart.png` |

All three primary screenshots are `1920x1080` and were produced from the
current built application for this audit.

## Reproduced Findings And Corrections

### 1. Canvas tool automation name

The visible box and brush tools did not reliably expose their names from the
selectable `ListBoxItem`, so the current-EXE beginner smoke could not find the
controls even though they were visible. The item container now exposes its
bound text and tooltip through WPF automation properties. A focused XAML
contract prevents removal.

### 2. Anomaly candidate used object-detection geometry language

The first anomaly result was correctly persisted as `Abnormal`, but its review
row said `이미지 밖`, `겹침 0%`, and described a skipped outside-image box. That
made a successful image-level classification look like an error. Image-level
candidates now say `이미지 전체 판정`, identify `OK/NG` review, mark geometry as
not applicable, and direct the operator to the right-side decision controls.
Object-detection candidate behavior is unchanged.

Before:
`artifacts/ui/beginner-e2e-audit-20260722/03-anomaly-current-exe/screenshots/04_first_abnormal_inference_after_restart.png`

After:
`artifacts/ui/beginner-e2e-audit-20260722/03-anomaly-current-exe-after/screenshots/04_first_abnormal_inference_after_restart.png`

### 3. Completed follow-up: generated Recipe name synchronization

The reproduced dataset-wizard naming issue is complete. Changing purpose now
updates only an untouched generated Recipe name and its untouched default
storage path. A Recipe name or storage path remains manual after any operator
edit, including edit-then-restore to the original text. Current-EXE evidence
and the exact preservation contract are in
`docs/DATASET_PURPOSE_AUTOMATIC_NAME_SYNC_20260722.md`.

## Verification

- Current app Debug build: pass, 0 warnings / 0 errors.
- Isolated test-project Debug build: pass, 0 warnings / 0 errors.
- Current-EXE object-detection ROI-tools smoke: pass.
- Current-EXE segmentation mask-tools smoke: pass.
- Current-EXE YOLOv8 anomaly close/restart/first-inference smoke: pass before
  and after the wording correction.
- Focused canvas, candidate-review, shell, anomaly, and documentation tests:
  pass.
- `git diff --check`: pass.

## Completion Record

Status: Complete

Scope: synchronized the active documentation baseline and audited the three
beginner task workflows against the current EXE, including two narrowly
reproduced UX corrections.

Acceptance criteria: one explicit documentation authority order -> pass; stale
active contradictions removed or marked historical -> pass; object detection,
segmentation, and anomaly current-EXE flows captured -> pass; reproduced
accessibility and anomaly-language defects covered by focused checks -> pass.

Evidence: this report and
`artifacts/ui/beginner-e2e-audit-20260722`.

Boundary / next dependency: workflow maturity remains `4.0/5` (about 80%) for
the focused single-operator workstation; this is not model accuracy. General
commercial labeling-suite parity remains `3.1/5` (about 62%), while intentional
enterprise/team-platform parity remains `1.2/5` (about 24%). The next bounded
UX naming follow-up is complete; production adoption still requires approved
independent camera/session evidence.
