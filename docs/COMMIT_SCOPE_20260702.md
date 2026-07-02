# 2026-07-02 Commit Scope Split

This file records the current dirty-worktree split before any staging or commit.

Current status snapshot:

- `git status --short` first check completed on 2026-07-02.
- Worktree is intentionally dirty: 146 modified files, 38 untracked paths, 1 deleted path.
- Do not run broad `git add .`.
- Do not revert unrelated existing changes.
- Do not include Viewer/OpenGL/ROI/brush/eraser changes in a UX/documentation commit unless they are separately audited and intentionally verified.

## Scope A: Tutorial and README Documentation

Purpose:

- Make the tutorial read like a practical work guide instead of a broad feature overview.
- Replace the small six-image tutorial feel with large, actual workbench screenshots.
- Use numbered callouts/arrows on the visible tutorial screenshots so a beginner can follow the image itself.
- Keep README/tutorial screenshots tied to the latest current EXE UI instead of stale layouts.
- Keep a copyable standalone HTML tutorial whose images still render on another PC.

Files:

- `README.md`
- `docs/tutorial/README.md`
- `docs/tutorial/labeling-workbench-tutorial.html`
- `docs/tutorial/labeling-workbench-tutorial-standalone.html`
- `docs/tutorial/images/01-overview-1920.png`
- `docs/tutorial/images/02-dataset-wizard-actual.png`
- `docs/tutorial/images/03-labeling-workbench-1920.png`
- `docs/tutorial/images/04-saved-label-panel-1920.png`
- `docs/tutorial/images/05-label-vs-ai-1920.png`
- `docs/tutorial/images/06-template-batch-1920.png`
- `docs/tutorial/images/07-save-required-1920.png`
- `docs/tutorial/images/08-save-done-1920.png`
- `docs/tutorial/images/09-model-center-1920.png`
- `docs/tutorial/images/10-training-complete-1920.png`
- `docs/tutorial/images/11-model-history-1920.png`
- `docs/tutorial/images/12-inference-dock-1920.png`
- `docs/tutorial/images/13-model-decision-1920.png`
- `docs/tutorial/images/14-model-inspect-1920.png`
- `docs/tutorial/images/annotated/01-overview-1920-annotated.png`
- `docs/tutorial/images/annotated/02-dataset-wizard-actual-annotated.png`
- `docs/tutorial/images/annotated/03-labeling-workbench-1920-annotated.png`
- `docs/tutorial/images/annotated/04-saved-label-panel-1920-annotated.png`
- `docs/tutorial/images/annotated/05-label-vs-ai-1920-annotated.png`
- `docs/tutorial/images/annotated/06-template-batch-1920-annotated.png`
- `docs/tutorial/images/annotated/07-save-required-1920-annotated.png`
- `docs/tutorial/images/annotated/08-save-done-1920-annotated.png`
- `docs/tutorial/images/annotated/09-model-center-1920-annotated.png`
- `docs/tutorial/images/annotated/10-training-complete-1920-annotated.png`
- `docs/tutorial/images/annotated/11-model-history-1920-annotated.png`
- `docs/tutorial/images/annotated/12-inference-dock-1920-annotated.png`
- `docs/tutorial/images/annotated/13-model-decision-1920-annotated.png`
- `docs/tutorial/images/annotated/14-model-inspect-1920-annotated.png`
- `docs/WORK_TRACKING.md`
- `docs/STABLE_VERIFIED_AREAS.md`

Staging caution:

- `docs/WORK_TRACKING.md` and `docs/STABLE_VERIFIED_AREAS.md` also contain records for app UX work. Use patch staging if this documentation commit is split away from the saved-label UX commit.
- `docs/tutorial/labeling-workbench-tutorial-standalone.html` is large because it embeds 14 screenshots as base64. That is intentional for copy-to-another-PC viewing, but it should be accepted explicitly.
- The unannotated 14 screenshots are source/reference captures. The visible README/tutorial paths should use the annotated versions.
- The rendered verification screenshot `artifacts/ui/tutorial-expanded-html-after-1920.png` is evidence only. Do not commit it unless the project intentionally tracks artifact screenshots.
- The rendered verification screenshot `artifacts/ui/tutorial-annotated-guide-after-1920.png` is evidence only. Do not commit it unless the project intentionally tracks artifact screenshots.

Verified:

- `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --priority-workflow-docs` passed.
- HTML image check passed: 14 displayed image paths exist.
- Standalone check passed: 14 embedded `data:image/png;base64` images, 0 remaining `src="images/...` references.
- Chrome headless rendered the HTML at 1920x1080 and produced `artifacts\ui\tutorial-expanded-html-after-1920.png`.
- Chrome headless rendered the annotated HTML at 1920x1080 and produced `artifacts\ui\tutorial-annotated-guide-after-1920.png`.
- Latest-image rule text was verified in `README.md`, `docs/tutorial/README.md`, `docs/tutorial/labeling-workbench-tutorial.html`, and `docs/STABLE_VERIFIED_AREAS.md`.
- HTML now has 14 visible `src="images/annotated/...` references and 0 non-annotated image `src` references.
- `git diff --check` passed for the tutorial/docs files with only LF-to-CRLF warnings.

Recommended commit title:

```text
docs: expand labeling studio operator tutorial
```

## Scope B: Saved-Label Edit Save-State UX

Purpose:

- When a user edits or deletes an already confirmed/saved label, the app must clearly return the active image to `저장 필요`.
- After pressing `라벨 저장`, the top status, canvas, left image queue, selected-image task card, and right saved-label panel must all recover to `저장됨`.
- This directly addresses the confusion between saved labels, AI candidates, and file persistence after post-confirmation edits.

Files:

- `0. UI/9) WPF/Models/WpfImageQueueModels.cs`
- `0. UI/9) WPF/ViewModels/WpfImageQueuePanelViewModel.cs`
- `0. UI/9) WPF/ViewModels/WpfObjectReviewPanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfObjectReviewPanel.xaml`
- `0. UI/9) WPF/Views/WpfObjectReviewPanel.xaml.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationPersistence.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueueDetailRefresh.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueueReviewStatus.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelAccessors.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.ReviewPanels.cs`
- `tests/LabelingApplication.Tests/Program.cs`
- `docs/WORK_TRACKING.md`
- `docs/STABLE_VERIFIED_AREAS.md`

Staging caution:

- `tests/LabelingApplication.Tests/Program.cs` has a very large accumulated diff. Patch staging is required if this is split from earlier UX/test additions.
- `docs/WORK_TRACKING.md` and `docs/STABLE_VERIFIED_AREAS.md` include multiple recent passes. Patch staging is required if Scope A and Scope B are committed separately.
- The code-behind edits are adapter fan-out for existing active-image persistence state; workflow/state text remains in ViewModels.

Verified:

- Build of `tests\LabelingApplication.Tests` to `artifacts\isolated-out` passed.
- `--wpf-labeling-session-smoke` passed.
- `--wpf-object-review-panel` passed.
- `--wpf-image-queue-status` passed.
- `--mvvm-infra` passed.
- `--wpf-labeling-shell` passed.
- `--wpf-responsive-layout --width 1920 --height 1080` passed.
- `--wpf-responsive-layout --width 1366 --height 768` passed.
- `--wpf-visual-smoke --review-tab objects --right-workflow-expanded --confirm-all-candidates --edit-confirmed-label-class --save-after-confirmed-label-edit --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png"` passed.

Evidence captures:

- `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920.png`
- `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png`
- `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-left-crop.png`
- `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-right-crop.png`
- `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-left-crop.png`
- `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-right-crop.png`

Recommended commit title:

```text
fix: show saved-label edits as save-required until persisted
```

## Scope C: Broader UX and MVVM Work Already in the Tree

Purpose:

- This is the large accumulated work from previous UX iterations: workflow rail, right dock, model center, dataset context, candidate review, class management, training/model guidance, template matching, and model registry.

Examples of files in this broader scope:

- Many `0. UI/9) WPF/Services/*.cs`
- Many `0. UI/9) WPF/ViewModels/*.cs`
- Many `0. UI/9) WPF/Views/WpfLabelingShellWindow.*.cs`
- `1. Core/ModelRegistryService.cs`
- `1. Core/TemplateMatchingAutoLabelService.cs`
- `1. Core/TemplateMatchingBatchAutoLabelService.cs`
- `Yolo/*.cs`
- `scripts/*.ps1`
- `docs/LABELING_UX_BENCHMARK.md`
- `MvcVisionSystem.csproj`
- `MvcVisionSystem.sln`

Staging caution:

- This scope should not be combined with Scope A or Scope B unless the goal is one very large checkpoint commit.
- Template matching has its own older recovery prompt and verification history. If committed, it should be reviewed as its own feature slice.
- The deleted `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/OpenGL/OpenGlDrawing.cs` and other Viewer/OpenGL changes must not be bundled into general UX commits without focused performance-path review.

Recommended split if continuing:

1. Template matching auto-labeling feature.
2. Dataset/storage/source separation UX.
3. Right dock/workflow rail progressive disclosure.
4. Model registry/model center/history UX.
5. Training/runtime problem-state presentation.
6. Viewer/OpenGL/ROI performance changes only with their own verification record.

## Do Not Stage by Default

- `CODEX_NEXT_PROMPT.md`: local handoff prompt, untracked. Commit only if the repository intentionally tracks Codex handoff prompts.
- `CODEX_RECOVERY.md`: modified recovery note. Commit only with explicit documentation intent.
- `artifacts/**`: generated evidence captures and build/test outputs. Do not commit unless the project has decided to track selected UI evidence images.
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/**`: protected performance area. Do not stage with UX/docs.

## 2026-07-02 Staging Candidate Inspection

Inspection result:

- Scope A is the safest first staging candidate because it is product documentation and screenshot assets.
- Scope B should not be staged file-by-file yet. `tests/LabelingApplication.Tests/Program.cs` alone has a 5,299-line accumulated diff, so patch staging is mandatory.
- Scope C is not ready for staging. It includes many unrelated WPF workflow/model/template changes and protected Viewer/OpenGL paths.
- No staging was performed during this inspection.

Scope A recommended split:

1. Product tutorial commit:

   - Stage full files:
     - `README.md`
     - `docs/tutorial/README.md`
     - `docs/tutorial/labeling-workbench-tutorial.html`
     - `docs/tutorial/labeling-workbench-tutorial-standalone.html`
     - `docs/tutorial/images/01-overview-1920.png`
     - `docs/tutorial/images/02-dataset-wizard-actual.png`
     - `docs/tutorial/images/03-labeling-workbench-1920.png`
     - `docs/tutorial/images/04-saved-label-panel-1920.png`
     - `docs/tutorial/images/05-label-vs-ai-1920.png`
     - `docs/tutorial/images/06-template-batch-1920.png`
     - `docs/tutorial/images/07-save-required-1920.png`
     - `docs/tutorial/images/08-save-done-1920.png`
     - `docs/tutorial/images/09-model-center-1920.png`
     - `docs/tutorial/images/10-training-complete-1920.png`
     - `docs/tutorial/images/11-model-history-1920.png`
     - `docs/tutorial/images/12-inference-dock-1920.png`
     - `docs/tutorial/images/13-model-decision-1920.png`
     - `docs/tutorial/images/14-model-inspect-1920.png`
     - `docs/tutorial/images/annotated/01-overview-1920-annotated.png`
     - `docs/tutorial/images/annotated/02-dataset-wizard-actual-annotated.png`
     - `docs/tutorial/images/annotated/03-labeling-workbench-1920-annotated.png`
     - `docs/tutorial/images/annotated/04-saved-label-panel-1920-annotated.png`
     - `docs/tutorial/images/annotated/05-label-vs-ai-1920-annotated.png`
     - `docs/tutorial/images/annotated/06-template-batch-1920-annotated.png`
     - `docs/tutorial/images/annotated/07-save-required-1920-annotated.png`
     - `docs/tutorial/images/annotated/08-save-done-1920-annotated.png`
     - `docs/tutorial/images/annotated/09-model-center-1920-annotated.png`
     - `docs/tutorial/images/annotated/10-training-complete-1920-annotated.png`
     - `docs/tutorial/images/annotated/11-model-history-1920-annotated.png`
     - `docs/tutorial/images/annotated/12-inference-dock-1920-annotated.png`
     - `docs/tutorial/images/annotated/13-model-decision-1920-annotated.png`
     - `docs/tutorial/images/annotated/14-model-inspect-1920-annotated.png`
   - Patch-stage only the relevant documentation records:
     - `docs/WORK_TRACKING.md` sections:
       - `2026-07-02 README and tutorial commit-readiness pass`
       - `2026-07-02 tutorial realism and large-capture pass`
       - `2026-07-02 README and tutorial portfolio documentation pass`
       - `2026-07-02 tutorial annotated-screenshot and latest-image rule pass`
     - `docs/STABLE_VERIFIED_AREAS.md` line:
       - `2026-07-02 tutorial standalone image contract`
       - `2026-07-02 README/tutorial latest-image contract`

2. Optional internal scope-note commit:

   - `docs/COMMIT_SCOPE_20260702.md`
   - `docs/WORK_TRACKING.md` section:
     - `2026-07-02 commit-scope split pass`

Scope B required patch-staging markers:

- `WpfImageQueueItem.IsSaveRequired`
- `WpfImageQueuePanelViewModel` selected-image task state for `저장 필요`
- `WpfObjectReviewPanelViewModel.LabelSaveStateKey`
- `WpfObjectReviewPanelViewModel.SetLabelSaveState(...)`
- `WpfObjectReviewPanel.xaml` save-state badge and detail binding
- `WpfLabelingShellWindow.AnnotationPersistence` dirty/saved/waiting fan-out
- `WpfLabelingShellWindow.ImageQueueReviewStatus` active queue save-required helpers
- `Program.cs` visual smoke flag `--save-after-confirmed-label-edit`
- `Program.cs` assertions for dirty state and post-save recovery state
- `docs/WORK_TRACKING.md` sections:
  - `2026-07-02 confirmed-label edit save-required pass`
  - `2026-07-02 confirmed-label edit save-recovery pass`
- `docs/STABLE_VERIFIED_AREAS.md` lines:
  - `2026-07-02 confirmed saved-label edit dirty-state contract`
  - `2026-07-02 confirmed saved-label edit save-recovery contract`

Scope B should be staged only after running an index review such as:

```powershell
git diff --cached --stat
git diff --cached --name-status
```

## Practical Next Step

If committing is requested, use one of these approaches:

1. Documentation-only product commit:

```powershell
git add README.md docs/tutorial/README.md docs/tutorial/labeling-workbench-tutorial.html docs/tutorial/labeling-workbench-tutorial-standalone.html
git add docs/tutorial/images/01-overview-1920.png docs/tutorial/images/02-dataset-wizard-actual.png docs/tutorial/images/03-labeling-workbench-1920.png docs/tutorial/images/04-saved-label-panel-1920.png docs/tutorial/images/05-label-vs-ai-1920.png docs/tutorial/images/06-template-batch-1920.png docs/tutorial/images/07-save-required-1920.png docs/tutorial/images/08-save-done-1920.png docs/tutorial/images/09-model-center-1920.png docs/tutorial/images/10-training-complete-1920.png docs/tutorial/images/11-model-history-1920.png docs/tutorial/images/12-inference-dock-1920.png docs/tutorial/images/13-model-decision-1920.png docs/tutorial/images/14-model-inspect-1920.png
git add docs/tutorial/images/annotated/*.png
git add -p docs/WORK_TRACKING.md docs/STABLE_VERIFIED_AREAS.md
```

2. Optional internal scope-note commit:

```powershell
git add docs/COMMIT_SCOPE_20260702.md
git add -p docs/WORK_TRACKING.md
```

3. Saved-label UX only:

```powershell
git add -p "0. UI/9) WPF/Models/WpfImageQueueModels.cs" "0. UI/9) WPF/ViewModels/WpfImageQueuePanelViewModel.cs" "0. UI/9) WPF/ViewModels/WpfObjectReviewPanelViewModel.cs" "0. UI/9) WPF/Views/WpfObjectReviewPanel.xaml" "0. UI/9) WPF/Views/WpfObjectReviewPanel.xaml.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.AnnotationPersistence.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueueDetailRefresh.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.ImageQueueReviewStatus.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelAccessors.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.ReviewPanels.cs" tests/LabelingApplication.Tests/Program.cs docs/WORK_TRACKING.md docs/STABLE_VERIFIED_AREAS.md
git diff --cached --stat
git diff --cached --name-status
```

4. Broader checkpoint:

```powershell
# Not recommended without a final review pass because this includes many unrelated or protected-path changes.
git status --short
```

Recommended next work before any commit:

- Review Scope A in the browser once more if the tutorial wording needs more human editing.
- Then decide whether to stage Scope A alone or Scope A plus Scope B.
- Leave Scope C for a separate audit.
