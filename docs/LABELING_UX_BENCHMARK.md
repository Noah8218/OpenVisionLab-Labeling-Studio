# Labeling UX Benchmark

Last updated: 2026-07-01

This note records a lightweight UX benchmark against current labeling tools. It is intended to guide UI work, not to copy another product.

## Sources Checked

- CVAT documentation: `https://docs.cvat.ai/docs/workspace/tasks-page/`
- Label Studio quick start: `https://labelstud.io/guide/quick_start`
- Roboflow AI Labeling: `https://docs.roboflow.com/annotate/ai-labeling`
- Labelbox image editor and ontology docs: `https://docs.labelbox.com/docs/image-editor`, `https://docs.labelbox.com/docs/labelbox-ontology`
- 2026-07-01 refresh: the same official docs still support the main product pattern: dataset/task setup, label schema or ontology setup, image import, annotation, and AI-assisted review should be separate decisions.
- 2026-07-01 model-assistance refresh: Label Studio documents predictions/pre-annotations as model output that users review/correct; Roboflow Label Assist requires explicit model selection before assisted labeling; CVAT keeps task labels/specification and annotation workspace actions distinct. This supports keeping "trained candidate", "inspection model", and "save to recipe for next inference" as separate visible states.
- 2026-07-01 post-training refresh: Label Studio, Roboflow, CVAT, and Labelbox all keep repeated setup or configuration decisions separate from the immediate review/adoption action. This supports progressive disclosure in the YOLO tab after a trained model candidate exists.
- 2026-07-01 training/model lifecycle refresh: Label Studio ML backend and Data Manager docs, CVAT automatic annotation docs, and Roboflow Label Assist docs all treat model output as a separate review/apply state. This supports showing "current inspection model", "trained model candidate", and "next inference after recipe save" together near the training decision.
- 2026-07-01 post-training action reachability refresh: the same tools keep model-assisted output close to review/adoption actions. This supports exposing candidate review and recipe-save actions from the top workflow rail, the model center, and the training-complete settings surface.
- 2026-07-01 model-profile terminology refresh: the same tools treat label data, model-assisted predictions, and model/runtime configuration as separate product concepts. This supports presenting YOLO as one model adapter/profile rather than the whole product vocabulary.

## Common Product Patterns

1. First-run flow is explicit: create project/task, choose label schema, import images, then annotate.
2. Label schema is a first-class concept. CVAT uses task labels; Labelbox uses ontologies; Label Studio uses labeling interface configuration.
3. AI assistance is separated from human review. Roboflow separates Auto Label/Label Assist from review/editing; Label Studio imports predictions as pre-annotations.
4. Review actions stay near the object being reviewed. Confirm, skip, focus, and next actions are visible near candidate context.
5. Export/integration is part of the product promise. CVAT makes dataset formats and API/CLI integration visible.

## Current Application Assessment

Strengths:

- The workflow rail now exposes dataset, labeling, inference review, and training/model steps in one app.
- Dataset identity, storage folder, and image folder are visible in the main header.
- Label and AI-candidate display modes are separated on the canvas.
- The app can label, train YOLO, compare candidates, and run inference without leaving the tool.
- Model-center state now separates current inspection model, trained candidate, review, final confirmation, and a compact adoption decision card.
- The top toolbar now keeps only frequent actions visible and groups occasional settings/helper commands behind a compact tools menu.
- Template-assisted labeling now states the sequence explicitly: choose a source label, generate candidates, review, then save labels.
- Guide/Tools now exposes a first-user sample path from dataset creation through first label, candidate review, and training readiness, with command-backed shortcuts for each unambiguous step.
- Candidate review now prioritizes current-image accept/skip actions before trained-model validation details, with separate role cards for each task.
- Candidate review now keeps trained-model validation examples and review history behind collapsed summary rows, so long lists do not consume the first review viewport.
- YOLO model settings now show the current inspection setup and save/reset actions before advanced path and inference fields, reducing vertical pressure when the user only needs to confirm which model is active.
- YOLO training settings now show the current training setup, recommended start path, and run controls before advanced parameter editors.
- Class management now prioritizes class add/rename/delete and the recipe class list, while color presets and storage-folder settings stay collapsed until needed.
- YOLO training/runtime failures now surface near the YOLO controls and model center as problem/cause/next-action cards instead of relying only on the bottom log.
- The default shell and visual smoke baseline now target the common 1920x1080 equipment resolution, with a 1366x768 small-screen smoke check kept as a regression guard.
- Startup onboarding now places dataset creation/opening actions before the beginner practice path, so small screens still show the first required operator decision.
- Class management now behaves more like a label-schema or ontology panel: it exposes class name/color/list management only, and storage-folder decisions are routed back to the dataset home/create-open flow.
- The 1366x768 responsive layout now has an automated regression guard that checks the review/settings panel remains visible across saved-label, AI-candidate, guide, class, YOLO, and training tabs.
- Training readiness now converts technical validator errors such as `data.yaml` class mismatches into an operator-facing cause and next action near the training controls.
- The top inference status now includes the current inspection model or pending model candidate, so users can see which model is being used without opening the YOLO tab.
- Model confirmation wording now uses `검사 모델로 저장` and explicitly says the choice is saved to recipe and used from the next inference run.
- When a trained model candidate is staged after training, the top inference status now changes to `모델 후보 ...` immediately, keeping the always-visible status consistent with the model center until recipe save completes.
- Missing `results.csv` metrics now read as `지표 없음: 학습 실패 아님...` so a completed training run without metrics is not mistaken for a failed training run.
- YOLO runtime management buttons (`첫 점검`, `설치`, `테스트`, `재시작`, `중지`) are now collapsed under `실행기 관리` by default, so the model-center decision and recipe-save action stay visually dominant after training.
- The model-center dataset readiness detail is now collapsed by default. The visible first decision after training remains candidate review / inspection-model save, while repeated dataset checks stay available when explicitly expanded.
- YOLO model settings now expose the inspection-model `.pt` picker directly below the current-model summary, while Python/project/script and inference tuning stay behind `실행 환경 상세`.
- The right-side work area now starts the mode-based split: dataset/labeling, inference review, and model center expose only their relevant review tab group instead of keeping saved labels, AI candidates, guide/tools, classes, and YOLO visible together.
- Single-view stages now read as mode surfaces instead of redundant one-tab panels: inference review and model center collapse the right-side subnavigation, while labeling keeps its saved-label/guide/class subnavigation because those are still related labeling tasks.
- The labeling stage now treats saved-label review as the primary right-side surface. Guide/tools and class schema remain available as compact header shortcuts instead of full-width tab headers.
- The compact labeling shortcuts now show active-state feedback, so users can tell whether the right side is showing labels, tools, or class schema without reading a tab strip.
- The canvas active-label card now includes a direct class-management action, so a first-time labeler can see and change the class applied to the next drawn box without hunting through the right panel.
- The top dataset context now separates storage folder, image folder, and work source: classes are shown as Recipe-backed and labels as storage-folder-backed, reducing confusion when the same image folder is reused across datasets.
- Dataset selection and dataset creation now show the same storage/image source rule before the workbench opens, so a user can choose between continuing an existing labeled dataset and creating a new isolated storage folder before drawing labels.
- The canvas now keeps the first-label loop visible as a compact `그리기 -> 라벨 저장 -> 다음 이미지` sequence beside the current next action, so a new user does not have to infer the save/navigation rhythm from separate controls.
- The image queue primary navigation now says `다음 미완료` and explains that saved/no-object images are skipped, so post-save movement matches the real queue behavior instead of looking like a generic next-file command.
- The canvas now exposes `객체 없음` beside label save, so a user can intentionally finish an object-free image without entering the AI-candidate review surface.
- The learning guide now mirrors the model-center lifecycle state in a first-visible model summary: current inspection model, trained candidate, adoption decision, and next action are visible before the next training action button.
- Post-training candidate review and inspection-model save are now reachable from the top workflow rail, model center, and training-complete settings surface, with 1920x1080 and 1366x768 smoke captures guarding visibility.
- Main header, tools menu, model settings, runtime status, dataset setup, and first-run learning copy now use model-agnostic product terms (`모델`, `모델 프로필`, `검사 모델`, `모델 실행기`, `학습 결과 후보`, `박스 라벨 파일`) while retaining the current YOLO adapter underneath.

- The model center now has a first-pass model registry summary that separates model profile, latest training run, trained candidate, current inspection model, and the next adoption action.

- The recipe now persists a first-class model registry with model profiles, training runs, model candidates, and inspection-model adoption history.

- Candidate Review now exposes a local model-candidate decision card, and the recipe persists saved/rejected candidate decisions separately from inspection-model adoption history.

- The model center now shows a recent model-history list with profile/run context, metric summary, and saved/rejected/current decision state for multiple candidates.

- Model-history rows can now be selected. The selected candidate shows details and can be intentionally applied as the inspection model when its weights file still exists and it is not already current.

- The model-center stage now keeps `현재 검사` beside candidate review and inspection-model save, so the path from saved/selected model to the next inspection stays in the same decision surface.

- The right workflow panel now behaves like a task dock: labeling defaults to a narrow rail so the queue and canvas dominate, while dataset setup, inference review, and training/model work keep the right panel expanded.

- The bottom log now starts as a compact latest-log summary and only opens the detailed log list when the user asks for it.

Gaps:

- First-run onboarding now has a visible path, but later training/model terms still need local explanations where the user makes a decision.
- The 1920x1080 equipment baseline is usable and 1366x768 panel visibility is guarded; the next gap is a full first-run-to-training sweep that checks whether a new user can complete model candidate review and recipe save without relying on the bottom log.
- Progressive disclosure has reduced clutter, and the right panel now hides irrelevant tab groups by workflow stage. The next structural gap is the full first-use sweep: verify that a new user can go from dataset creation through label save, training, model-candidate decision, and inspection with the selected model without needing the bottom log.
- The app still has many internal and some surface names tied to YOLO because the current adapter is YOLOv5. The next architectural UX gap is a first-class model registry/history surface that lets users compare YOLO, ONNX, segmentation, anomaly, or future adapters by profile and run result, not by implementation name.

## Next UX Priorities

Implemented:

1. Add a first-run beginner checklist that starts with dataset creation and reaches label saving/training readiness.
2. Compress the candidate review panel so current-image candidate handling and trained-model validation are visually distinct but both visible at 1440x900.
3. Add a model adoption decision card that summarizes the current inspection model, trained candidate, validation evidence, and exact confirmation action.
4. Move infrequent top-toolbar actions behind a compact tools menu after the core workflow is stable.
5. Keep template/model-assisted flows explicit: generate candidates first, then review, then save/confirm.
6. Add a guided sample path equivalent to "create project -> import data -> label first image -> train/check".
7. Put current-image candidate review actions before trained-model validation details in the right review panel.
8. Make YOLO failure and recovery states local to the controls/model center instead of only the bottom log.
9. Turn the first-user path into clickable shortcuts only where the target action is unambiguous and already command-backed.
10. Add progressive disclosure for long model-validation example lists and candidate review history.
11. Compress YOLO inspection-model settings so current model identity and save action appear before advanced path fields.
12. Compress YOLO training settings so current run setup and start controls appear before advanced parameters.
13. Compress class management so class add/rename/list actions appear before color and storage advanced controls.
14. Move the default app and visual-smoke baseline from 1440x900 to 1920x1080 while preserving a smaller-window check for responsive behavior.
15. Move first-run dataset setup actions above the sample/practice path so a new user can immediately create or open a dataset before reading lessons.
16. Remove dataset storage-folder editing from the class catalog panel so class management remains a label schema task, not a project storage task.
17. Add a 1366x768 responsive-layout regression guard for the review/settings panel across the main work tabs.
18. Convert WPF training readiness validator errors into user-facing cause/action text near the training controls.
19. Add current inspection-model visibility to the top inference status card.
20. Change model-adoption wording from vague confirmation to explicit recipe save and next-inference use.
21. Keep top inference status synchronized with pending trained-model candidates and recipe-save completion.
22. Clarify missing training metrics as a verification limitation, not as a training failure.
23. Collapse occasional YOLO runtime management commands by default in the model-center view.
24. Collapse repeated dataset-readiness details in the model-center view so post-training review/save remains the primary visible decision.
25. Move the inspection-model `.pt` picker out of advanced execution settings so model changes do not require scanning Python/project fields.
26. Scope the right-side panel by workflow stage: saved labels for labeling, AI candidates for inference review, and YOLO/model controls for model-center mode.
27. Collapse right-side subnavigation when the active stage has only one relevant view, so inference review and model center no longer show a redundant single tab.
28. Replace the labeling-stage right tab strip with compact header shortcuts for saved labels, tools, and class schema.
29. Add active-state feedback to the compact labeling shortcuts.
30. Add a direct class-management action to the canvas active-label card and expose missing-class setup state in the canvas ViewModel.
31. Add a top dataset work-source card that states classes come from the Recipe and labels come from the dataset storage folder.
32. Add pre-workbench source-rule cards to dataset selection and creation so storage folder, image folder, and label carry-over behavior are clear before opening a dataset.
33. Add a first-label canvas loop chip that keeps `draw -> save label -> next image` visible while the user labels.
34. Rename the queue primary navigation to `다음 미완료` and bind its tooltip through the ViewModel so saved/no-object skip behavior is visible after label save.
35. Add a canvas `객체 없음` completion command that saves an empty YOLO label, persists the queue row as no-object complete, and advances to the next incomplete image.
36. Mirror model-center lifecycle state in the learning guide so current inspection model, trained candidate, adoption decision, and next action are visible in the training first-visible area.
37. Expose post-training candidate review and inspection-model recipe save from the top workflow rail and the training-complete settings surface, not only inside the model center.
38. Replace the first visible YOLO-specific setup/runtime/training labels with model-agnostic terms while leaving the current adapter implementation stable.

39. Add a model-registry summary in the model center that presents model profile, training run, candidate model, current inspection model, and next adoption action as separate concepts.

40. Persist model profile, training run, model candidate, and inspection-model adoption records in recipe project settings.

41. Add a Candidate Review model-candidate decision card with explicit save/reject commands and persisted saved/rejected decision history.

42. Add a model-center recent model-history list that compares current/candidate model rows with profile, run, metrics, and decision state.
43. Collapse the right workflow panel by default in labeling mode while keeping a command-bound rail for labels, tools, classes, inference review, and model center.
44. Collapse the bottom log into a latest-log summary by default while preserving the detailed log panel behind a command.
45. Add row-level actions to the model-history list so a user can select a past candidate, inspect details, and intentionally apply it as the inspection model.
46. Keep the post-training `현재 검사` action visible inside the model-center decision surface, not only as a separate global toolbar button.

Next:

1. Continue the first-use sweep at the transition from inference results back to saved labels, so users can tell whether they are reviewing AI candidates or editing committed labels.
2. Add side-by-side validation-detail drilldown for historical model candidates, using the existing comparison output when available.
3. Use the existing panels as migration units first, then split or merge panels only where a workflow needs a clearer task surface.
