# OpenVisionLab Labeling Studio Completeness Audit

Date: 2026-07-06

This audit answers one product question: when compared with established labeling tools, which parts of OpenVisionLab Labeling Studio are mature enough to stop revisiting, and which gaps should drive the next development work.

The score below is product completeness, not code quality. A lower score can still be acceptable when the feature is outside the current product goal.

## Sources Checked

Official competitor documentation checked on 2026-07-03:

- CVAT overview: https://docs.cvat.ai/docs/getting_started/overview/
- CVAT dataset formats: https://docs.cvat.ai/docs/dataset_management/formats/
- Label Studio ML backend: https://labelstud.io/guide/ml
- Label Studio pre-annotations: https://labelstud.io/guide/predictions
- Label Studio export formats: https://labelstud.io/guide/export
- Roboflow AI Labeling: https://docs.roboflow.com/annotate/ai-labeling
- Roboflow team collaboration: https://docs.roboflow.com/annotate/team-collaboration
- Labelbox ontologies: https://docs.labelbox.com/docs/labelbox-ontology
- Labelbox pre-label import: https://docs.labelbox.com/docs/import-prelabels

Internal evidence checked:

- `docs/LABELING_UX_BENCHMARK.md`
- `docs/OBJECT_DETECTION_MVP_COMPLETION.md`
- `docs/ANOMALY_DETECTION_FLOW.md`
- `docs/STABLE_VERIFIED_AREAS.md`
- `docs/WORK_TRACKING.md`

## Scoring Scale

- 0: absent
- 1: direction or design only
- 2: partial implementation
- 3: usable but not yet broad or deeply verified
- 4: verified workflow for the intended local use case
- 5: competitive with established tools in breadth, polish, and operational workflow

## Executive Conclusion

OpenVisionLab Labeling Studio is already strong as a local Windows workstation for industrial object-detection labeling, YOLOv5 training, current-image/batch inference review, and model-candidate decision work. That part is roughly 4/5, or about 80 percent of a focused single-operator product.

It is not yet a general CVAT/Label Studio/Roboflow/Labelbox replacement. Against full labeling-suite expectations, it is roughly 3.0/5, or about 60 percent. The main missing areas are import interoperability, collaboration/review operations, video/tracking/keypoints/3D, foundation-model assist, and production-grade anomaly workflow.

Against enterprise/cloud labeling platforms, it is intentionally much lower, roughly 1/5 to 1.5/5. The app currently has no multi-user assignment, role-based review, comments, consensus, cloud storage, hosted workforce workflow, API surface, or enterprise administration.

## Competitor Baseline

CVAT sets a broad platform baseline: image, video, and 3D data; rectangle, polygon, point, ellipse, cuboid, skeleton, brush, tags, track mode; QA; automation; team collaboration; dataset import/export; API/SDK/CLI.

Label Studio sets an integration baseline: configurable labeling interfaces, import/export, project storage, ML backends, static pre-annotations, interactive pre-annotations, API, SDK, and model-evaluation loops.

Roboflow sets an AI-assisted image-labeling baseline: manual boxes/polygons, Label Assist, Smart Polygon with SAM, box prompting, Auto Label with foundation models, team assignments, labeling instructions, progress boards, comments, review, approve/reject.

Labelbox sets an ontology and workforce baseline: reusable ontologies, projects, batches, model-assisted pre-labels, human review, and data-row based import/export workflows.

## Capability Assessment

| Area | Score | Current decision |
| --- | ---: | --- |
| Dataset/project setup | 4 | Verified local workflow. Do not revisit unless a gate fails or a new import/export target requires it. |
| Class schema/catalog | 4 | Good local ontology equivalent for object classes. Stop polishing unless needed for multi-format export or anomaly labels. |
| Object-detection box labeling | 4.5 | Protected MVP. Real EXE smoke and save/reopen gates exist. Avoid changing ROI/viewer paths without rerunning focused gates. |
| Image queue, completion, reopen | 4 | Verified and protected. Empty-normal completion is done for object-detection use. |
| Object review and candidate review | 4 | Strong local review workflow. Continue only when tied to model comparison, anomaly, or export evidence. |
| YOLOv5 training and current inference | 3.8 | Usable and verified for local workflow. Not multi-backend parity yet. |
| Model registry/history/comparison | 3.8 | Strong local model-decision surface, now including a YOLOv8/YOLO11 Ultralytics segmentation validation path for baseline-vs-candidate comparison and Candidate Review focus examples for YOLO segmentation polygon label rows. Still not a general model registry service for all future backends. |
| Segmentation annotation | 4.2 | Polygon/brush/eraser save/export foundations are present and verified, COCO, Label Studio, plus CVAT polygon exports/imports now exist, YOLOv8/YOLO11 segmentation inference polygons can be preserved by worker/protocol and explicit smoke-result parsing, and model polygons can be confirmed as saved segmentation labels. The local `C:\Git\yolov8` adapter now also preserves `result.masks.xy` as candidate polygon metadata, matching the bundled worker contract. The Ultralytics training request contract now sends segmentation datasets as `segment` training with task-specific `yolov8n-seg.pt` and `yolo11n-seg.pt` packet plus fake-worker training coverage, and app segment JSON polygons are now converted into Ultralytics YOLO segmentation `labels/*.txt` lines before training starts. The worker now prefers matching cached task weights from the model root, reports resolved training weights plus runtime-ready/runtime-blocked and download-required/runtime-blocked-missing task-weight cache inventory through status/detail diagnostics, blocks implicit training weight downloads without explicit request approval, verifies all explicit worker opt-in aliases through fake training only, focused C# packet tests verify no download approval flag is sent, reports YOLO11 only when the selected Ultralytics runtime supports it, and the bundled smoke-test CLI now reports unsupported adapter/runtime blockers such as missing YOLO11 `C3k2` before model load. The local YOLOv8 worker has now completed tiny 1-epoch segmentation training smokes, including one app-generated dataset fixture and the corrected `runs/segment/.../weights/best.pt` output path, loaded generated `best.pt` files through its inference smoke path, the app connection path now prefers trained `best.pt` over pretrained seed weights including the newest app-generated `runs/segment` smoke, post-training selection reads YOLOv8 `args.yaml` current-dataset metadata for segmentation runs, including an app-generated segmentation `data.yaml` staging fixture, a real TCP workflow smoke verifies app-fixture polygon candidate confirmation saves non-empty segment JSON plus mask PNG artifacts, the model comparison runner can now validate local Ultralytics segmentation candidates against a held-out split, and the real EXE parent-folder workflow now selects the operator `images` folder with `OK`/`NG` children, labels NG masks, includes OK empty-label background samples, trains YOLOv8 SEG, applies `best.pt`, restarts stale Python workers when weights change, and completes current-image inference with an `NG` candidate from the trained model. Production accuracy remains a separate gate. |
| Template-assisted labeling | 3.5 | Useful local automation. It is not the same as foundation-model auto-labeling. |
| Runtime/status presentation and MVVM boundaries | 4.1 | Many high-value status paths are now service-owned and test guarded, and the YOLO settings profile now reflects live YOLOv8/YOLO11 worker training capability when the connected worker reports it. Runtime warnings, segmentation/classification capability lists, resolved Ultralytics training weights, runtime-ready/runtime-blocked plus download-required/runtime-blocked-missing task-weight cache inventory, download-guard failures from the worker in YOLO settings, training progress/recovery, YOLOv8 segmentation training-history surfaces, current-image smoke unsupported-runtime blockers, and the local YOLOv8 source/venv connection state are preserved into status/detail surfaces with operator-readable labels. Continue only narrowly. |
| Anomaly detection | 3.6 | Purpose/manifest/guide foundations exist, image-level normal/abnormal persistence is implemented, WPF normal-completion advances to the next unreviewed anomaly image, dashboard distribution is visible, YOLOv8/YOLO11 classification results are preserved as image-level runtime candidates, explicit class mappings persist in project settings with WPF configuration inputs, mapped classification results update anomaly review state after inference, reviewed images can be exported to an Ultralytics normal/abnormal classification dataset, anomaly training routes `task=classify` to that dataset with YOLOv8/YOLO11 task-specific classification packet coverage, and insufficient normal/abnormal examples now produce operator-facing readiness detail. YOLO11 availability is runtime-gated; real model training/evaluation smoke still needs separate gates. |
| Export/import interoperability | 4.4 | YOLO-centered output exists, object-detection exports/imports now cover COCO detection JSON, Pascal VOC XML, Label Studio detection JSON, and CVAT image task archive, segmentation has COCO, Label Studio, and CVAT polygon exports/imports, and CVAT segmentation import now writes local segment/mask artifacts. NDJSON interoperability is not productized yet. |
| Quality control and audit metrics | 2.9 | Dataset readiness exists, the WPF guide/dashboard surfaces dataset quality audit counts, and the audit report can be exported as Markdown. Labeler QA, issue review, consensus, comments, and deeper annotation analytics are still missing. |
| Collaboration/workforce | 0.5 | Not implemented. This is outside the current local workstation MVP. |
| Video/tracking/keypoints/3D | 0.5 | Not implemented beyond some annotation shape foundations. |
| Foundation-model assist | 1 | Template matching and YOLO candidates exist, but SAM/Grounding-DINO-style assisted annotation is missing. |
| Public docs/tutorials | 3.8 | Strong for local onboarding, but product-level compatibility claims must stay conservative. |

## Stop Reworking

Do not spend more development time on these areas unless a specific defect is reproduced or a listed verification gate fails:

- Object-detection MVP: dataset setup, queue, box drawing, save/reopen, empty-normal completion, dataset readiness.
- Viewer/OpenGL/ROI/brush/eraser performance paths.
- Candidate review current-image accept/skip and saved-label review flow.
- Model center current/candidate/history decision surface.
- Runtime unavailable, runtime ready, settings detail, training progress, and related presentation-boundary refactors that are already listed in `docs/STABLE_VERIFIED_AREAS.md`.
- Public tutorial cleanup that is already guarded by `--priority-workflow-docs`, unless the UI visibly changes.

## Development Required

Priority order:

Current user priority override: as of 2026-07-03, continue YOLOv8/YOLO11 segmentation and anomaly integration before adding more dataset-interoperability formats. The first segmentation runtime slices are complete: the bundled Ultralytics worker advertises segmentation capability from the selected runtime, returns mask polygons on `DetectImageResult` candidates, the C# TCP protocols and explicit worker smoke-result parser preserve those fields, candidate confirmation saves model polygons as segmentation labels while keeping the bbox fallback, segmentation training requests now carry `task=segment` into the Ultralytics worker, the C# production-path packet gate verifies `task=segment` with `model=yolov8`/`weight=yolov8n-seg.pt` and `model=yolo11`/`weight=yolo11n-seg.pt`, the worker fake-training self-test verifies YOLOv8/YOLO11 segment/classify defaults, cached task-weight preference, and the explicit `allowModelDownload=true`/`allowWeightDownload=true`/`allowDownload=true` worker opt-in aliases without real downloads, resolved training weights are preserved through `trainingWeights` status/detail diagnostics, runtime-ready/runtime-blocked task-weight cache inventory is visible through worker status, missing task weights are split into download-required vs runtime-blocked-missing diagnostics, implicit training weight downloads are blocked unless the request explicitly opts in, focused C# packet tests verify that no download approval flag is sent, the resulting download guard is shown in YOLO settings detail, training progress/recovery text, and YOLOv8 segmentation training history as operator-readable guidance, the bundled smoke-test CLI passes the selected adapter with `--model` and reports unsupported adapter/runtime blockers before model load, and the model settings profile reflects live worker training capability when reported. With explicit user approval, `yolov8n-seg.pt` was downloaded as a pretrained seed into `C:\Git\yolov8`; the local YOLOv8 adapter then completed a historical 1-epoch `task=segment` training smoke under `runs/train` before the run-folder correction, and a follow-up 1-epoch smoke now produces `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-seg-runs-segment-smoke\weights\best.pt`. The generated `best.pt` loads through the local inference smoke path. The local folder connection now promotes trained `best.pt` outputs ahead of pretrained seed weights, and WPF post-training selection reads YOLOv8 `args.yaml` metadata plus `runs/segment` folders so current-dataset segmentation runs win over newer foreign runs. A direct EXE workflow now selects the real parent `D:\circular_defect_labeling_dataset_v1\images` folder, labels NG masks, includes OK empty-label background samples, trains YOLOv8 segmentation, applies the resulting `best.pt`, restarts the Python worker when the connected worker was started with stale weights, and completes current-image inference with an `NG` candidate from the trained model. The comparison runner now has a repeatable local Ultralytics segmentation validation path for baseline-vs-candidate evidence before model promotion, but a real held-out comparison still must be run before claiming production accuracy. The first anomaly runtime slices are also complete: the worker advertises classification capability from the selected runtime, preserves classification-only `result.probs` output as image-level candidates, a decision service maps only explicitly configured classes to `Normal`/`Abnormal`, those class mappings persist in project settings with WPF configuration inputs, mapped inference results now update anomaly review state, reviewed images can be exported to a normal/abnormal classification dataset, anomaly training now sends `task=classify` with that dataset, the production-path packet gate verifies `model=yolov8`/`weight=yolov8n-cls.pt` and `model=yolo11`/`weight=yolo11n-cls.pt`, insufficient normal/abnormal examples now show operator-facing readiness detail, and the settings profile can show a connected YOLO11 worker as training-capable only when the worker reports YOLO11 capability. The current bundled Ultralytics `8.0.132` environment still reports YOLOv8-only capability because YOLO11 modules are missing, and that runtime warning is preserved into the settings detail. The next runtime slice should run/record actual held-out SEG model comparison evidence and then continue anomaly runtime hardening; real accuracy remains unclaimed from tiny smokes.

2026-07-06 local industrial update: the active scope is local industrial image workflows, not collaboration/cloud parity. The parent `images` folder shape with `OK` and `NG` child folders now has focused service coverage and a direct EXE workflow gate. The EXE selected `D:\circular_defect_labeling_dataset_v1\images`, loaded child-folder images, saved NG brush segment artifacts, exported 6 train and 2 valid segmentation labels, added 20 OK background empty labels, trained YOLOv8 SEG, applied `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt`, and reached inference review. This closes the first OK/NG folder-based integration gate; the next priority is data-quality/readiness hardening and repeatable evaluation evidence, not production accuracy claims.

2026-07-07 local industrial recheck: the built EXE workflow was rerun as `circular_seg_exe_20260707_010325` after hardening transient UI Automation combo/dialog handling. It created a segmentation recipe, selected `D:\circular_defect_labeling_dataset_v1\images`, saved 6 train, 3 valid, and 3 test NG segment masks, included 20 OK/background empty labels, trained and applied `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-segment\weights\best.pt`, and completed current-image YOLOv8 inference with one `NG 1.3%` candidate. This is a fresh runtime wiring gate, still not a production accuracy claim.

2026-07-06 split-cleanup update: YOLOv8 SEG training-data preparation now removes generated OK/background empty-label copies from stale train/valid/test folders when split settings move those images, while preserving real segment JSON or mask PNG artifacts. This is focused non-EXE data-integrity coverage, not a new model-accuracy gate.

2026-07-06 labeling UX update: the local single-operator image flow now has focused coverage for SEG moved-label auto-save before navigation, arrow-key queue navigation, denser queue filters, row thumbnails, canvas brush-size controls, and Stage 3 `AI 후보` wording. These close several workstation usability gaps without changing the verified brush/eraser performance path. Remaining UX work is tutorial screenshot refresh and any real large-folder queue scroll performance evidence.

2026-07-06 large-folder queue update: image queue loading now has focused 1200-item fixture coverage for one bulk collection reset, no full-folder thumbnail creation during load, lazy visible-row thumbnail access, and thumbnail file-handle release. This reduces the local workstation large-folder usability risk, but it is still focused test evidence rather than a direct EXE manual scroll timing pass on the operator's real folder.

2026-07-06 held-out comparison update: YOLOv8/YOLO11 segmentation model comparison now requires at least one positive YOLO segment label line in the selected held-out `val` or `test` split. Empty OK/background labels and bbox-only labels are blocked before validation launches, and readiness diagnostics warn earlier when the segmentation test split has OK/background images but no positive NG mask image. This prevents false model-promotion evidence; it does not replace a real same-class baseline comparison.

2026-07-06 model-evaluation update: the new comparison path correctly blocked comparing the one-class circular SEG dataset against the COCO `yolov8n-seg.pt` baseline because the label counts differ: dataset labels 1, baseline labels 80, candidate labels 1. Candidate-only Ultralytics `val` on `circular_seg_exe_20260706_105323` completed with box and mask mAP values at 0.0, confirming that the tiny one-epoch candidate is workflow evidence only, not an accuracy-ready production model. The next promotion gate needs a same-class previous baseline and a labeled held-out test split.

2026-07-06 same-class comparison update: YOLOv8/YOLO11 model comparison now checks ordered class names, not only class counts, before launching validation. The local mismatch case was blocked correctly: the circular dataset and latest candidate are `1 [NG]`, while an older one-class smoke baseline is `1 [Defect]`. This prevents false promotion evidence from same-count but different-meaning labels. The next promotion gate still needs a distinct previous `NG` baseline plus a labeled held-out split.

2026-07-06 same-class val comparison update: a distinct local YOLOv8 SEG `NG` candidate was trained for one CPU epoch and compared against the previous `openvisionlab-yolov8-segment` `NG` baseline on the app-generated `val` split from `circular_seg_exe_20260706_105323`. The comparison path completed and wrote `artifacts\yolo-model-comparison\yolov8-seg-ng-baseline-vs-candidate-20260706\20260706-174515\comparison-summary.json`, but both baseline and candidate reported precision, recall, mAP50, and mAP50-95 as `0.0` with zero UI-threshold candidates. This proves same-class comparison plumbing, not production readiness. The next model-quality gate is a labeled positive `test` split plus a stronger YOLOv8 SEG training run.

2026-07-06 true-test comparison update: `circular_seg_exe_20260706_183245` supplied a held-out `test` split with positive `NG` masks. The existing 1ep same-class candidate, a local 5ep candidate, and a local 30ep/img128 YOLOv8 SEG candidate all completed `scripts\compare-yolo-models.ps1 -Task test -ModelTask segment` against the active `openvisionlab-yolov8-segment` baseline. The 30ep candidate wrote `artifacts\yolo-model-comparison\yolov8-seg-ng-test-baseline-vs-30ep-img128-20260706\20260706-185303\comparison-summary.json` and improved over baseline to recall `1.0`, mAP50 `0.105`, and mAP50-95 `0.044`, but precision is only `0.016` with many low-confidence UI candidates. This proves the true held-out comparison gate and shows the training direction is moving, but the current tiny models are not production-accuracy-ready.

2026-07-06 promotion recommendation update: model-comparison summaries and Markdown reports now include a promotion recommendation, and the WPF Candidate Review model-comparison detail surfaces that recommendation. Re-running the 30ep/img128 YOLOv8 SEG held-out comparison wrote `promotion.recommendation=hold` because candidate precision `0.016` is below the conservative `0.10` minimum. This reduces accidental promotion risk; it does not make the model quality acceptable.

2026-07-07 promotion evidence update: model-comparison summaries and Markdown reports now include held-out evidence counts, and promotion recommendations stay `hold` below the existing 10 labeled-image recommendation even when metrics improve. This aligns the script artifact with the WPF weak-evidence warning and further reduces accidental inspection-model promotion from tiny test splits.

2026-07-07 promotion reason wording update: the WPF Candidate Review model-comparison detail now translates the raw low-precision hold reason from `comparison-summary.json` into Korean operator guidance while preserving the metric value, and focused coverage blocks reintroducing `Candidate precision` text. This improves model-replacement clarity only; it does not improve YOLOv8 SEG accuracy or change promotion thresholds.

2026-07-07 userseed comparison update: an existing YOLOv8 SEG userseed/img160 candidate was compared on the same real held-out `test` split from `circular_seg_exe_20260706_183245`. It improved over the baseline to precision `0.121`, recall `0.333`, mAP50 `0.223`, and mAP50-95 `0.064`, but still produced `0` UI-threshold candidates at confidence `0.25` and has only `9/10` real held-out evidence images. The comparison script now keeps promotion on `hold` when UI-threshold candidates are zero, and WPF translates that hold reason for operators. This is progress evidence, not an inspection-model adoption approval.

2026-07-07 label-preservation update: focused WPF segmentation coverage now verifies that pending brush masks and moved segmentation polygons are auto-saved before image navigation clears the active canvas, and both DataGrid queue-click navigation and keyboard `Down` movement now prove the same save-before-load boundary. This reduces the highest local labeling data-loss risk, but it is still focused WPF evidence rather than a manual EXE pass on the operator's full dataset.

2026-07-07 Stage 3 wording update: the operator-facing Stage 3 workflow now uses `AI 후보 검토` instead of the ambiguous `추론 검토`, and the candidate-review panel explicitly states that confirm creates a saved label while skip hides only the candidate. The public README/tutorial copy and candidate-review tutorial screenshot were refreshed from current 1920x1080 WPF evidence. This improves workflow comprehension only; it does not change model accuracy or inference execution.

2026-07-07 Stage 3 top-button follow-up: the XML-entity encoded top workflow button and inference mode switcher now also use `3 AI 후보` / `AI 후보 검토`, and the XAML contract test decodes the file before rejecting any reintroduced `추론 검토` label. The standalone tutorial HTML embeds the matching refreshed AI-candidate screenshot. This closes a UI wording consistency gap only.

2026-07-06 template-batch fixture training update: the app-generated YOLOv8 segmentation fixture that includes template-batch polygon and raster-mask transferred SEG artifacts now also passes a direct local Ultralytics `segment train` smoke. The run used `C:\Git\yolov8\.venv\Scripts\yolo.exe`, cached `C:\Git\yolov8\yolov8n-seg.pt`, `epochs=1`, `imgsz=64`, `batch=1`, `device=cpu`, and wrote `artifacts\yolov8-seg-training-smoke\template-batch-fixture\weights\best.pt`, `last.pt`, `args.yaml`, and `results.csv`. This closes the generated-fixture training-compatibility gate; production accuracy still depends on larger real labeled datasets and held-out evaluation.

2026-07-06 trained-model inference update: the EXE circular segmentation workflow now verifies the complete path from applying YOLOv8 SEG `best.pt` to current-image inference completion. `CGlobal.EnsurePythonModelClientReady` checks the current auto-start process settings before accepting an existing TCP connection, so a stale `yolov8n-seg.pt` worker is restarted when `best.pt` becomes the inspection model. The verified EXE run `circular_seg_exe_20260706_183245` completed with `후보: NG 1.3% ... / 추론: 완료  모델 YOLOv8 / openvisionlab-yolov8-segment\best.pt / 후보 1`. This is runtime wiring evidence, not model-quality evidence.

2026-07-03 late update: WPF training-result metric parsing now prefers YOLOv8 segmentation mask `(M)` precision/recall/mAP values and `val/seg_loss` over box `(B)` / `val/box_loss` values when both are present in Ultralytics `results.csv`. This improves post-training segmentation score presentation only; it is not a new real-training or accuracy gate.

2026-07-03 later update: the WPF training-session completion fixture now verifies that a completed run can stage `runs/segment/<run>/weights/best.pt` as the pending inspection-model candidate. This is a mocked completion/apply gate, not a real project training or accuracy gate.

2026-07-04 update: app segmentation training preparation now converts saved `segments/*.json` polygons into Ultralytics YOLO segment `labels/*.txt` files before `StartTraining`. Focused coverage verifies the generated train/valid files contain polygon label coordinates for the YOLOv8/YOLO11 segmentation packet path. A direct local YOLOv8 worker smoke then trained for one CPU epoch against the app-generated fixture and loaded the resulting `best.pt` through the same adapter. WPF training-weight coverage now also stages a synthetic local YOLOv8 `runs/segment` `best.pt` whose `args.yaml data:` points at that app-generated `data.yaml`, verifying it is treated as current-dataset completed segmentation training. This is still tiny fixture/staging evidence, not production accuracy.

2026-07-04 later update: the local `C:\Git\yolov8\labeling_tcp_client.py` adapter now preserves segmentation mask polygons from `result.masks.xy` in the same candidate fields used by the bundled worker and C# protocol. The default-confidence app-fixture smoke originally produced zero candidates, but low-confidence app-fixture smokes on both train and valid images now return one real `Defect` polygon candidate with normalized polygon points. A follow-up real TCP workflow smoke with `LABELING_SMOKE_EXPECT_SEGMENTATION=true` verified that the same app-fixture polygon candidate passes through the local YOLOv8 TCP worker, C# `ResultDefect` parser, overlay/confirm path, and saves non-empty segment JSON plus mask PNG artifacts. This is still tiny fixture evidence, so a real operator dataset/model gate is required before claiming production segmentation accuracy.

2026-07-04 real app-saved SEG artifact update: a recent EXE-saved SEG artifact from `artifacts\run\Debug\DATA\Segmentagtion_Dataset_ObjectDetection_20260704_065650` was staged into an ignored minimal YOLOv8 segmentation dataset and trained through the local `C:\Git\yolov8` TCP `StartTraining` path for one CPU epoch. The run produced `C:\Git\yolov8\runs\segment\openvisionlab-yolov8-real-app-segmentation-smoke\weights\best.pt`, which loaded through `labeling_tcp_client.py --smoke-test`; at `--conf 0.001` it returned polygon candidates with normalized polygon points. This moves beyond synthetic-only fixtures by using a real app-saved SEG artifact, but it is still a one-image duplicated train/valid smoke and does not claim production accuracy. The actual operator dataset still needs enough saved segmentation labels in train and valid splits before production model promotion.

2026-07-04 SEG readiness guidance update: WPF training-readiness presentation now translates the current real blocker into operator wording: YOLOv8 segmentation training needs saved mask labels in both train and valid splits. The same presentation layer maps missing polygon segment JSON export failures back to saving brush/polygon labels. This is status guidance only, not new model accuracy or training evidence.

2026-07-04 SEG teaching input update: focused WPF coverage now verifies that switching/restoring a segmentation purpose applies the visible Brush tool to the actual canvas input state instead of leaving stale rectangle drawing active. A corrected follow-up keeps CPU MaskData/history/Object Review materialization deferred while Brush/Eraser remains selected, removes pending Object Review row updates from MouseUp, avoids successful MouseUp model-status text churn, detaches the stroke center buffer instead of copying it, and defers image queue save-status refresh until save/tool-end flush. The latest mask-drag gate simulates a 125-row queue and verifies `WPF_MASK_BRUSH_RELEASE_MS=4.641`, `WPF_MASK_BRUSH_EXISTING_RELEASE_MS=3.954`, `COLLECTION_CHANGED_AFTER=0`, `SECOND_COLLECTION_CHANGED_AFTER=0`, `QUEUE_CHANGED_AFTER=0`, `SECOND_QUEUE_CHANGED_AFTER=0`, `UNDO_AFTER=0`, and `UNDO_AFTER_TOOL_END=2`. A direct `OpenVisionLab.LabelingStudio.exe` smoke verified brush/eraser input against the built EXE with `brushCommitWaitMs=0.0` and `eraserImmediateWheelUiMs=17.7`. Real mask labels are still flushed on save/tool-end. This fixes SEG teaching usability and a brush hitch regression only; it is not a new model-training or accuracy gate.

2026-07-04 SEG undo/redo follow-up: pending Brush/Eraser strokes now enable Undo immediately without MouseUp materialization, and undo restore clears the pending FBO mask preview before rebuilding segmentation overlays. Focused WPF coverage keeps `UNDO_AFTER=0` with `UNDO_BUTTON_IMMEDIATE=True`, and a direct EXE smoke draws 4 seeded-random brush strokes then verifies the latest stroke disappears/restores through Ctrl+Z/Ctrl+Shift+Z pixel checks. This is a labeling UI correctness/stability gate, not a production model-accuracy gate.

2026-07-04 SEG preview opacity follow-up: the Brush preview FBO now copies committed mask alpha without an extra blend pass and uses the same mask opacity for live brush fill as committed raster overlays. This removes the visible old-vs-new stroke opacity split while keeping the deferred FBO preview path; focused WPF coverage and a direct EXE undo/redo smoke remain passing. This is a labeling UI rendering consistency gate, not a model-training or accuracy gate.

2026-07-04 SEG brush materialization follow-up: this supersedes the earlier save/tool-end-only materialization note. Active Brush/Eraser drag and MouseUp still avoid synchronous CPU/Object Review work, but completed strokes now materialize one mask label, undo entry, selected Object Review row, and queue save-state update after the quiet idle window even while Brush/Eraser remains selected. This fixes the operator-visible "mask drawn but no label row" issue without reintroducing MouseUp hitching. Raster mask class changes now carry the catalog class item/color and invalidate the mask overlay so NG -> OK recolors existing brush masks. Lightweight image queue clicks now commit the image first and defer side refresh, with warm visible queue clicks verified at `94.0ms` average and warm image commit at `41.6ms` average on `D:\LabelingData\Test01\Images`. Direct EXE smokes verified brush/eraser input plus Ctrl+Z/Ctrl+Shift+Z restore; this is a labeling UI correctness/performance gate, not model-training or accuracy evidence.

2026-07-03 update: the active YOLOv8 direction is now local-worker first, matching the existing YOLOv5 pattern. The app can map a selected `C:\Git\yolov8`-style folder through `BuildYoloV8FolderConnection` to the local TCP worker, local venv, segmentation weight candidate, and image root. `C:\Git\yolov8` now contains an official `ultralytics/ultralytics` checkout at `ultralyticsMaster` plus a local `.venv` editable install; the worker resolves Ultralytics from that source tree and reports the source path in diagnostics. The local worker reports YOLOv8 training/detection/segmentation/classification capability when `ultralytics` imports, and `TrainYolo` routes local-only requests to Ultralytics `YOLO.train(...)` while refusing missing local weights instead of triggering downloads. After explicit approval, `yolov8n-seg.pt` was cached locally as the pretrained seed, tiny local segmentation training smokes produced `best.pt`/`last.pt`, and the generated `best.pt` loaded through local inference smoke. The latest smoke verifies current `segment` training output under `runs/segment` rather than the detection-style `runs/train` folder. This proves local YOLOv8 segmentation training/inference plumbing, but not production dataset accuracy.

After the 2026-07-03 object-detection import/export slices, segmentation export slices, and COCO/Label Studio/CVAT segmentation imports, object-detection interoperability is good enough for the local workstation MVP and segmentation import coverage is broad. The immediate next interoperability priority is NDJSON detection import, unless reviewer handoff through a WPF quality-audit export action is selected instead.

1. Export/import interoperability.
   - Why: every competitor treats export/import or API integration as a core product promise.
   - First completed slice: COCO detection JSON export from the existing YOLO box dataset is covered by `--coco-detection-export`.
   - Second completed slice: `DatasetExportCapabilityService` declares implemented exports and planned targets, with Pascal VOC XML selected as the next object-detection export target.
   - Third completed slice: Pascal VOC XML export writes one XML file per image, includes empty-label images, and is covered by `--pascal-voc-detection-export`.
   - Fourth completed slice: Label Studio detection JSON export writes image tasks plus RectangleLabels results in Label Studio percent units and is covered by `--label-studio-detection-export`.
   - Fifth completed slice: CVAT image task archive export writes `annotations.xml` plus image files in a `CVAT for images 1.1` zip and is covered by `--cvat-image-export`.
   - Sixth completed slice: COCO segmentation JSON export writes image/category/polygon annotation records from saved `segments/*.json` artifacts and is covered by `--coco-segmentation-export`.
   - Seventh completed slice: Label Studio segmentation JSON export writes image tasks plus `PolygonLabels` results in Label Studio percent units and is covered by `--label-studio-segmentation-export`.
   - Eighth completed slice: CVAT segmentation archive export writes `annotations.xml` polygon entries plus image files in a `CVAT for images 1.1` zip and is covered by `--cvat-segmentation-export`.
   - Ninth completed slice: COCO detection import reads COCO image/category/box records, copies source images, writes YOLO labels, creates empty label files, and is covered by `--coco-detection-import`.
   - Tenth completed slice: Pascal VOC detection import reads XML object/bndbox records, copies source images, writes YOLO labels, creates empty label files, and is covered by `--pascal-voc-detection-import`.
   - Eleventh completed slice: Label Studio detection import reads RectangleLabels task JSON, copies source images, writes YOLO labels, creates empty label files, and is covered by `--label-studio-detection-import`.
   - Twelfth completed slice: CVAT detection import reads image-task archive boxes and bundled images, writes YOLO labels, creates empty label files, and is covered by `--cvat-detection-import`.
   - Thirteenth completed slice: COCO segmentation import reads polygon annotations, copies source images, writes local segment JSON/mask artifacts, and is covered by `--coco-segmentation-import`.
   - Fourteenth completed slice: Label Studio segmentation import reads PolygonLabels task JSON, copies source images, writes local segment JSON/mask artifacts, and is covered by `--label-studio-segmentation-import`.
   - Fifteenth completed slice: CVAT segmentation import reads polygon annotations and bundled images from a CVAT archive, writes local segment JSON/mask artifacts, skips invalid polygons, and is covered by `--cvat-segmentation-import`.
   - Runtime/save-path note: YOLOv8 real TCP segmentation smoke now proves a tiny local polygon candidate can be parsed, confirmed, and saved as segment JSON/mask PNG; WPF Candidate Review confirm also preserves confirmed AI polygon candidates as segment JSON/mask PNG artifacts, training-prep YOLO segment label txt, and the expected YOLOv8 segmentation training packet. A final local adapter sanity recheck also returned one app-fixture polygon candidate through the editable local Ultralytics source workflow. These are plumbing gates, not production accuracy gates.
   - Next interoperability slice: Labelbox NDJSON detection import into the local YOLO dataset layout.
   - Completion gate: `--export-capability-inventory`, a Labelbox NDJSON import tiny-dataset test, `--priority-workflow-docs`, and `git diff --check`.

2. Anomaly image-level workflow.
   - Why: the product already exposes anomaly as a purpose, but the docs correctly say it is not complete.
   - First completed slice: image-level `Normal`, `Abnormal`, `Unreviewed` state persists beside the dataset, manifest summary includes anomaly review counts, and `--wpf-anomaly-purpose-flow` covers save/reopen plus next-unreviewed selection.
   - Second completed slice: the WPF normal-completion command writes `Normal`, saves the empty compatibility label file, and advances to the next unreviewed anomaly image through the same focused gate.
   - Third completed slice: the dataset dashboard shows normal/abnormal/unreviewed anomaly distribution through the same focused gate.
   - Fourth completed slice: YOLOv8/YOLO11 classification-only worker results are preserved as image-level candidates and covered by `--python-ultralytics-worker`, `--python-model-status-protocol`, and `--python-detection-result-protocol`.
   - Fifth completed slice: explicit anomaly class mappings persist in project settings, build `AnomalyClassificationDecisionOptions`, and are covered by `--anomaly-classification-decision`.
   - Sixth completed slice: the YOLO/model settings panel exposes normal/abnormal class mapping and threshold inputs through the ViewModel, covered by `--wpf-yolo-model-settings-panel`, `--wpf-settings-viewmodels`, and a 1920x1080 visual smoke capture.
   - Seventh completed slice: mapped image-level classification results update `AnomalyImageReviewStatusService` after current-image and batch inference, covered by `--anomaly-classification-decision` and `--wpf-anomaly-purpose-flow`.
   - Eighth completed slice: reviewed anomaly images export to `classification/<split>/normal` and `classification/<split>/abnormal` for Ultralytics classification training, covered by `--anomaly-classification-dataset-export`.
   - Ninth completed slice: anomaly training exports the reviewed classification dataset and sends `task=classify` with the classification root in `StartTraining`, covered by `--anomaly-classification-training-workflow`.
   - Tenth completed slice: operator-facing readiness detail for insufficient normal/abnormal anomaly examples is covered by focused readiness and shell gates.
   - Next slice: add a real model/runtime smoke when a stable fixture/model cache is available.
   - Completion gate: keep `--wpf-anomaly-purpose-flow` and `--anomaly-classification-decision`; add a real model/runtime smoke before claiming broad anomaly model workflow complete.

3. Dataset quality and review audit.
   - Why: established tools expose review, QA, and progress metrics; our app has readiness but not enough audit reporting.
   - First completed slice: `YoloDatasetQualityAuditService` reports per-split image count, label count, empty-normal count, class distribution, missing label count, and invalid label count and is covered by `--dataset-quality-audit`.
   - Second completed slice: WPF guide/dashboard shows a quality metric and next-action issue from the same audit report and is covered by `--wpf-training-dashboard-quality` plus a 1920x1080 visual smoke capture.
   - Third completed slice: `YoloDatasetQualityAuditExportService` writes a Markdown audit report and is covered by `--dataset-quality-audit-export`.
   - Fourth completed slice: YOLOv8 SEG readiness diagnostics warn when train/valid positive mask coverage is too small or OK/background empty labels dominate positives, covered by `--dataset-readiness-purpose`, `--wpf-training-readiness-presentation`, and `--wpf-training-dashboard-quality`.
   - Next slice: add reviewer-facing issue status or a WPF save action for the existing Markdown export without changing annotation hot paths.

4. Foundation-model assisted labeling.
   - Why: Roboflow/CVAT/Label Studio now set user expectations around SAM or prompt-based assistance.
   - First shippable slice: a runtime-agnostic assist interface and UI wording, not a hardcoded future model.

5. Collaboration/workforce.
   - Why: important for enterprise parity, but it is a different product shape from the current local workstation.
   - First shippable slice only if needed: local reviewer status/comments without accounts or server sync.

## Product Positioning

Current honest positioning:

> OpenVisionLab Labeling Studio is a local Windows labeling and model-workflow workstation for industrial image datasets, strongest today for object detection with YOLOv5 training/inference review and model-candidate decisions.

Do not claim:

- Full CVAT replacement.
- Full Label Studio replacement.
- Cloud labeling platform.
- Team/workforce labeling product.
- Complete anomaly detection product.
- Broad import/export compatibility.

## Next Development Recommendation

Start with export/import interoperability if the goal is competitor parity. Start with anomaly image-level state if the goal is completing already visible product modes.

For the current codebase, the safer visible-mode implementation was anomaly image-level state because the purpose, guide, manifest, and missing gates were already documented. For market completeness, the higher-impact export slices were CVAT image task archive export, COCO segmentation export, Label Studio segmentation export, and CVAT segmentation archive export because COCO, Pascal VOC, Label Studio detection, and CVAT detection exports were already covered by focused gates.

The recommended sequence after the COCO/Pascal VOC/Label Studio/CVAT detection export/import slices, segmentation export slices, COCO/Label Studio/CVAT segmentation imports, anomaly image-level workflow, dataset quality-audit slices, and the first YOLOv8/YOLO11 segmentation/anomaly runtime contracts is:

1. Continue YOLOv8 segmentation quality work on larger real labeled datasets and held-out comparisons; the tiny generated-fixture training smoke is now closed.
2. Add an anomaly classification real model/runtime smoke when stable fixtures are available.
3. Implement Labelbox NDJSON detection import only after the runtime priority is paused or completed.
4. Add a WPF save action for the existing Markdown audit export if reviewer handoff becomes the active priority.

## Latest Verified UI Correction

2026-07-04: The segmentation-purpose labeling UI now refreshes its purpose-dependent tool scope when a SEG dataset/purpose is applied. The verified contract is brush-first segmentation tools (`Brush`, `Eraser`, `Polygon`, `Select`, `PanZoom`) in the guide and canvas toolbar, with `Brush` selected by default when entering segmentation from select mode. This was verified by the isolated WPF test build, `--wpf-learning-workflow-panel`, `--wpf-segmentation-object-verification`, `--wpf-labeling-shell`, and a 1920x1080 visual smoke capture at `artifacts\ui\wpf-seg-purpose-brush-1920.png`.

2026-07-06: The public README/tutorial representative screenshots were refreshed from current 1920x1080 WPF visual-smoke captures and the standalone HTML was regenerated with matching embedded images. The refreshed screenshots now show the current brush-size toolbar, image-queue thumbnails/filter layout, Stage 3 AI-candidate wording, and redacted public path text. This is documentation evidence only; it does not change the product completeness ranking above.

2026-07-07: The image queue, AI-candidate left panel, saved-label left panel, guide/tools left panel, class setup left panel, training settings left panel, and model center left panel received focused compact-layout passes. The verified UI direction is to keep operator-critical state/actions visible while collapsing repeated explanatory text, duplicate detail tables, and first-run shortcut tiles by default, giving more room to the image list, object list, class list, workflow content, training/model status, and canvas. Covered by focused WPF tests and 1920x1080 visual-smoke captures; this is a layout/usability improvement only and does not change the product completeness ranking above.

2026-07-07: Model-candidate review received a small safety/readability pass: rejected model-history candidates remain visible but are no longer directly promotable, and candidate save/reject/rejected-state text is owned by the presentation service with focused WPF/MVVM coverage. This reduces accidental model adoption risk and mojibake/readability friction only; it does not change YOLOv8 SEG model accuracy or the completeness ranking above.
