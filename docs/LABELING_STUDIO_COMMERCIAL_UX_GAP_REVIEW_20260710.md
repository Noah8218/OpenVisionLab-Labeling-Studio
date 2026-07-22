# Labeling Studio Commercial UX Gap Review

Date: 2026-07-10 KST

Purpose: define the next UI/UX development priorities for OpenVisionLab Labeling Studio by comparing the current local workstation product with established labeling tools.

This is a UI/UX planning document. It does not change runtime, model, dataset, or annotation behavior.

Superseding status note (2026-07-22): this document preserves the 2026-07-10
comparison as historical planning evidence. Its earlier YOLO11-blocked statement
is superseded by the scoped local YOLO11 detection and segmentation evidence in
`docs/NEXT_THREAD_HANDOFF.md` sections 7 and 16. Anomaly classification and
arbitrary external YOLO11 runtimes remain unverified.

## Sources Checked

Official and product documentation checked on 2026-07-10:

- CVAT editor and controls sidebar: https://docs.cvat.ai/docs/annotation/annotation-editor/ and https://docs.cvat.ai/docs/annotation/annotation-editor/controls-sidebar/
- CVAT objects sidebar and manual QA: https://docs.cvat.ai/docs/annotation/annotation-editor/objects-sidebar/ and https://docs.cvat.ai/docs/qa-analytics/manual-qa/
- CVAT product workflow: https://www.cvat.ai/
- Roboflow Annotate interface and Label Assist: https://docs.roboflow.com/annotate/use-roboflow-annotate and https://docs.roboflow.com/annotate/ai-labeling/model-assisted-labeling
- Roboflow product workflow and AI labeling: https://roboflow.com/annotate
- Label Studio ML backend and project model settings: https://labelstud.io/guide/ml and https://labelstud.io/guide/project_settings
- Labelbox Annotate overview, ontology, and pre-label import: https://docs.labelbox.com/docs/annotate-overview, https://docs.labelbox.com/docs/key-definitions, and https://docs.labelbox.com/docs/import-prelabels

Current local evidence checked:

- `docs/NEXT_THREAD_HANDOFF.md`
- `docs/WORK_TRACKING.md`
- `docs/STABLE_VERIFIED_AREAS.md`
- `docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md`
- Current visual evidence: `artifacts/ui/learning-workflow-guide-after.png`

## Current Self-Evaluation

OpenVisionLab Labeling Studio is now functionally strong for the local industrial SEG path, but the left workflow area still feels less commercial because it tries to be a task guide, help manual, tool list, model status panel, and beginner onboarding surface at the same time.

Functional maturity:

| Area | Current score | Reason |
| --- | ---: | --- |
| Local segmentation labeling tools | 4.0/5 | Brush, eraser, polygon, save, queue, candidate confirmation, and YOLO segment export are verified. |
| YOLOv8 SEG operation/model loop | 3.7/5 | Local source worker, training, comparison, candidate review, and first fixture-scoped promotable model exist. Broader operator data is still missing. |
| Anomaly classification operation | 3.2/5 | Runtime, classification adapter, training packet, and evaluation guard exist. Real operator-data smoke is still missing. |
| Beginner UI/UX | 2.6/5 | Density was reduced, but the mental model is still "open the big side panel and scroll". Commercial tools make the active task, tool, object list, and review state visually separate. |
| Review/QA workflow | 2.4/5 | Candidate review exists, but local issue marking, image-level review state, comments, and review completion flow are not yet productized. |
| Model-quality visibility | 3.2/5 | Comparison/adoption guards exist, but false positive/false negative examples and "what changed" are not consolidated into a single operator dashboard. |

Overall UX conclusion: the next work should not be another small collapse/scroll tweak. The next valuable slice is information architecture: make the left side behave like a commercial workbench with separate task surfaces.

## Commercial Patterns To Emulate

### CVAT

CVAT separates annotation into a central workspace, navigation/menu, controls sidebar, and objects sidebar. Its controls sidebar groups navigation, zoom, shapes, edit, and movement tools. Its objects sidebar lists current-frame objects with filtering, sorting, visibility, lock, and collapse controls. Review issues are represented as issue objects with comments, navigation, resolve/reopen states, and job workflow status.

What this means for us:

- Keep drawing tools and current object/class context close to the image.
- Keep object/candidate lists separate from long guide text.
- Add lightweight local issue/review state before considering any collaboration/server scope.

### Roboflow Annotate

Roboflow places the annotation toolbar on the side of the labeling interface, uses model assistance from a command icon, supports Smart Polygon/Label Assist, and treats upload -> assign -> review -> approve as a visible workflow. It also exposes dataset search, analytics, splits, and class filters as first-class workflow surfaces.

What this means for us:

- AI assistance should feel like a primary work mode, not buried in a guide panel.
- Dataset/model quality should have a scan-friendly summary with examples.
- The operator should not have to read instructions while labeling; the UI should present the next action.

### Label Studio

Label Studio integrates ML backends for pre-annotation, interactive labeling, model evaluation, and fine-tuning. Project settings expose prediction sources, connected models, manual training, test requests, and prediction views.

What this means for us:

- Our YOLOv8 local worker direction is aligned, but the UI should show "model connected / prediction source / run test / evaluate / train" as one coherent model workflow.
- Auto-label/prediction review should be a clear loop: run, review predictions, accept/correct, retrain/evaluate.

### Labelbox

Labelbox centers projects around data rows, ontology, model-assisted pre-labels, workflow review settings, and model runs with versioned data snapshots and performance comparison.

What this means for us:

- Class schema and task purpose should be visible as the current project contract.
- Model comparison should tie each candidate to data split, evidence count, review examples, and adopt/reject decision.
- Pre-label import/model output should stay traceable to an ontology/class mapping.

## Missing UX Capabilities

### P0 - Left Workbench Information Architecture

Problem: the current left/right side panels still expose too many concepts inside one guide/tool surface. More expanders reduce immediate clutter but preserve the same cognitive model.

Development item:

- Replace the guide/tools mental model with a role-based workbench split:
  - Current task: one card, one primary action, one next step.
  - Tools: active annotation tool and mode-specific tool choices only.
  - Objects: saved labels or AI candidates depending on mode.
  - Quality/model: warnings, comparison/adoption, and evaluation summaries.
  - Help: tutorials and workflow explanations outside the default labeling surface.

Acceptance checks:

- A beginner can identify the current image, active tool, selected class, save/next action, and AI candidate state without opening a long expander.
- The first visible side panel contains no tutorial/manual paragraph unless it is explicitly opened.
- The same workflow works for box, polygon, brush, and anomaly review without box-only wording.

### P1 - AI Candidate Review As A First-Class Mode

Problem: AI candidates exist and are saved correctly, but the UX still competes with normal labeling controls.

Development item:

- Add a compact AI Review mode surface:
  - candidate count
  - confidence threshold
  - selected candidate class/confidence
  - confirm, confirm all, skip, hide/show candidates
  - clear "save before next image" state

Acceptance checks:

- When candidates exist, the primary task is candidate review, not drawing.
- Confirm/skip actions are visually grouped and not mixed with training/model help text.
- Saved segment JSON/mask PNG/YOLO export behavior stays unchanged.

### P2 - Local QA And Issue Marking

Problem: commercial tools treat review/QA as a visible object workflow. Our app has review panels, but no simple local issue state for "needs correction", "reviewed", "accepted", or "rejected" beyond model-candidate decisions.

Development item:

- Add local-only issue/review status:
  - per image: unreviewed, needs fix, reviewed
  - per label/candidate: accepted, corrected, skipped, issue
  - optional short note field stored in project metadata

Acceptance checks:

- No account, server, assignment, or collaboration feature is introduced.
- Image queue can filter by issue/review state.
- Review state exports into the existing Markdown audit or a new local QA report.

### P3 - Model Quality Dashboard

Problem: model comparison and anomaly evaluation are functional, but commercial tools surface quality deltas and examples more directly.

Development item:

- Build a compact model-quality dashboard:
  - current model vs candidate
  - held-out image/positive/negative evidence counts
  - UI-threshold candidate counts
  - false positive/false negative thumbnails or example rows
  - adopt/reject decision and reasons

Acceptance checks:

- The dashboard does not claim YOLO11 readiness unless runtime/weights prove it.
- The dashboard keeps fixture-scoped evidence labeled as such.
- Candidate adoption remains blocked unless existing promotion guards pass.

### P4 - Foundation-Assisted Segmentation Feasibility

Problem: Roboflow and CVAT now set expectations around SAM-style one-click polygon/mask assistance. We currently have brush/polygon plus YOLO candidates, not foundation-model interactive segmentation.

Development item:

- Plan only after approval:
  - local SAM/SAM2 feasibility
  - model size/download approval
  - CPU/GPU/runtime requirements
  - offline fallback behavior

Acceptance checks:

- No model download or dependency upgrade without explicit approval.
- If added, it must be optional and local-first.

## Priority Decision

Immediate next development should be P0, not more model accuracy work and not more minor expander tweaks.

Recommended order:

1. P0-1: Create a concrete left-workbench layout contract and update the WPF panel around one role-based default view.
2. P0-2: Make the default labeling view show only current task, active tool/class, save/next action, and object/candidate status.
3. P1-1: Make AI Candidate Review a clear first-class mode surface.
4. P3-1: Consolidate model comparison/evaluation into a model-quality dashboard.
5. P2-1: Add local-only QA issue/review state once the default workbench is stable.
6. P4-1: Revisit foundation-assisted segmentation only with explicit model/dependency approval.

Deferred by design:

- Cloud/team collaboration, roles, assignment, RBAC, SSO, and workforce management remain outside the current local workstation product.
- More dataset interoperability is lower priority unless the runtime/model work is paused or the user redirects.
- Historical 2026-07-10 boundary: YOLO11 was blocked at the time of this review. See the superseding status note above for the current verified scope.

## Next Implementation Slice

The smallest useful code slice is P0-1:

- Do not rewrite annotation tools.
- Do not touch Viewer/OpenGL/ROI/brush/eraser hot paths.
- Keep existing ViewModel bindings where possible.
- Change only the side workbench layout/presentation so the default view stops acting like a manual.

Suggested verification:

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --dataset-purpose segmentation --review-tab labeling-guide --right-workflow-expanded --width 1920 --height 1080 --output .\artifacts\ui\wpf-left-workbench-p0-after-1920.png
git diff --check
```
