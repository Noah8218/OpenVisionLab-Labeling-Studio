# Release Notes

## Unreleased

## 0.4.4 — 2026-11-22

- Label import: the split chosen when importing labels is now saved as each imported image's split assignment, together with the dataset split file. Before, reopening the recipe re-split the imported images by the validation and test percentages. All import formats share the step that applies this; a focused test covers the YOLO import. The fixed build was not re-checked in the running app.
- README: adds results on real PCB images from VisA PCB4 (CC BY 4.0): boxes from the dataset's defect masks, the dataset's own split, YOLOv8n and YOLO11n trained and compared in the app, and TensorRT engines built with the edge runtime. One dataset with 40 defective test images; not evidence of production accuracy.
- README: Korean wording reads more naturally, and lists and arrows in both READMEs are written out as sentences. The Korean requirements heading is now `설치와 요구 사항`.

## 0.4.3 — 2026-11-21

- Training queue: select several YOLOv8/YOLO11 models and train them one after another on the same captured data. While a model trains, the validation chart draws finished models of the same dataset version in grey; an epoch log of the latest eight epochs and a progress summary show the best value, the difference from a finished model at the same epoch, time per epoch, the remaining estimate including the queue, and the next model. The finished view lists the same-version test results in one table.
- Automatic evaluation: when the queue finishes, the new models are evaluated on the test split and the training and comparison views fill in without further action. A queue started without a recipe save records the dataset version it trains on.
- Model comparison: YOLOv8 and YOLO11 models are evaluated with their own engines on the same test split. The comparison shows the A→B difference, AP by class, F1 and recall by confidence, a speed/accuracy chart and per-image agreement filters, and recommends a model among those evaluated on the same dataset version.
- Inspection model: the chosen YOLOv8 or YOLO11 model can be set on either Ultralytics engine. The recipe engine switches with it, the confirmation names the switch, and Undo restores the previous model and engine. Inference uses the adopted model; Ctrl+I runs inference on the inference stage.
- Dataset: label sets can be imported from a YOLO `data.yaml` in the label import window, with progress during the pre-check; byte-identical image copies are accepted; the stratified split keeps the configured ratio.
- Fixes: inference image list thumbnails, a comparison header that moved when choosing A/B, a duplicate re-evaluation row after an engine-changing adoption, Korean status text in the English UI, and UI wording.
- Verified on the synthetic washer dataset only; field data has not been evaluated.

## 0.4.2 — 2026-11-20

- Edge runtime throughput: decode, pre-processing, inference and post-processing run as threads joined by bounded queues, with a pool of inference runners; a full first queue drops and counts frames instead of stalling the camera. `stream_bench` measures latency and drops at a fixed frame rate.
- Fallback chain: after repeated failures the worker moves from TensorRT to OpenVINO BF16 on the CPU (or ONNX Runtime) and then to a rule-based check, and probes higher tiers to return. A rule-tier Pass becomes `Hold` (manual inspection) by default. The judging tier is visible in the result's model hash and in metrics.
- Monitoring: quality counts by ground truth when known, worker heartbeat, GPU temperature, power, utilization and memory through NVML, and 14 Prometheus alert rules covered by `promtool` tests. The Grafana dashboard shows false-reject rate, confirmed escapes, availability and GPU state.
- Release registry: immutable releases with recorded hashes, a single atomic pointer with change history, model swaps between requests without restart, shadow comparison, canary routing, a promotion gate, forced promotion only with a review file that covers every disagreement, and rollback.
- Field feedback: capture of near-threshold, Hold and fallback frames, drift hints from image statistics, and export to YOLO folders for relabeling and retraining.
- OPC UA Basic256Sha256 SignAndEncrypt with application certificates, trust lists and certificate-expiry alerts.
- Per-line parameter templates (per-class thresholds, region of interest, minimum box area) with inheritance, and threshold tuning that accepts a change only with a minimum gain.
- OpenVINO CPU runtimes (FP32, BF16, NNCF INT8) and a native C++ TensorRT benchmark with a CUDA letterbox kernel that matches the Python pre-processing bit for bit.
- Fix: when the Modbus requester link drops, the worker reports a failed read or write and reconnects instead of exiting.
- Still tested only against the bundled simulator and synthetic data. The desktop application, Recipe, annotation, dataset and integration formats are unchanged.

## 0.4.1 — 2026-11-20

- Add a separate edge inference runtime under `Runtime/Edge`: ONNX export with a recorded model specification, one shared pre/post-processing path, and parity checks against the Ultralytics reference.
- Build TensorRT FP32, FP16, and calibrated INT8 (Q/DQ) engines in an explicit prepare step. Each engine records its build GPU and TensorRT version. The worker refuses to start on a different GPU model or TensorRT version, or when an engine hash does not match.
- Run the worker in a Docker Compose stack on Linux/WSL2 with the NVIDIA Container Toolkit. It sends verdicts over Modbus TCP or OPC UA, infers each request once, and writes a stored result again on retry. Prometheus and Grafana show inspection-latency p99 and per-stage timings.
- A bundled simulator stands in for the requesting machine during local tests. Machine Studio integration, field-data precision selection, and the Windows-to-WSL frame and network paths are not yet verified.
- The desktop application, Recipe, annotation, dataset, and integration formats are unchanged.

## 0.4.0 — 2026-11-09

- Combine the published Smart Mask selection, keyboard navigation, model/run identity, canvas handling, and fresh-source build/storage fixes with current development changes.
- Keep export scrollbars clear of actions, enlarge the recipe-information close icon, show one missing-rule status in model assignment, summarize long model paths while preserving editable values, and localize ROI array captions.
- Add an opt-in TCP inbox for received-image label review, with separate acknowledgement, editing, label save, result publication, cancellation, and recovery steps.
- Preserve the approved bilingual README structure and media, current launcher behavior, existing Recipe/annotation/dataset formats, and earlier version history.


## 0.3.8 — 2026-10-26

- Show readable dataset version names and model roles across dataset, training, and model comparison screens, with explicit version and run identifiers available in details.
- Keep model comparison selections stable while refreshing version lists and retain table headings while scrolling.
- Align labeling and model-workflow terminology, queue saved-count hints, and dataset setup layout with the current application.
- Preserve training run identity, inference-model selection, and Smart Mask/canvas state handling.
- Align the English/Korean README and public build/runtime guidance with this product version while preserving the approved structure, media, limitations, and earlier release history.
- Existing Recipe, annotation, dataset, and integration formats remain compatible.

## 0.3.7 — 2026-10-26

- Reorganize the Korean and English README around supported tasks, the operator workflow, quick start, source builds, and known limitations.
- Replace representative media with actual washer boxes, film-bubble masks, procedural hexagon OK/NG decisions, and Korean/English Smart Mask GIF and MP4 clips.
- Documentation-only PATCH: product logic, dependencies, annotation/Recipe/Dataset Version/model formats, and CLI contracts are unchanged.

## 0.3.6 — 2026-10-24

- Preserve the selected Smart Mask object while regenerating its candidate; application and label saving remain explicit.
- Restore item hosts in content-only review tabs to prevent the observed Ctrl+Tab/Ctrl+Shift+Tab container exception; retain bounded fatal diagnostics.
- Refresh bilingual workflow documentation and actual industrial-image media. Public file formats remain unchanged.

## 0.3.5 — 2026-09-16

- Harden annotation load/save, crash recovery, Object Review, detection, and training state transitions.
- Add source-group and split-quality validation, model/runtime-lock provenance, and safer Python process and diagnostic failure handling.
- Preserve annotation, Recipe, Dataset Version, model-state, and public file formats.

## 0.3.4 — 2026-09-15

- Separate WPF workflow, ViewModel, adapter, and lifetime responsibilities while preserving bindings and operator actions.
- Harden transactional annotation saves, dataset import/readiness checks, bounded mask history, archive/recovery behavior, and explicit Object Review metadata/group saving.
- Use Vision SDK 3.0.0 with the aligned OpenCvSharp runtime; retain graphics diagnostics, bounded user diagnostics, and source-build notices.

## 0.3.3 — 2026-09-08

- Clarify ownership across Shell, Canvas, image queue, anomaly review, model, dataset, Object Review, and ImageCanvas dialog workflows.
- Document the solution entry point and project-reference navigation.
- Preserve annotation, Recipe, Dataset Version, model-state, Python-process, and public file-format contracts.

## 0.3.2 — 2026-09-07

- Consolidate Shell, queue, review, runtime, and model workflow responsibilities in concrete owners.
- Strengthen asynchronous close, cancellation, callbacks, and resource lifetimes.
- Preserve annotation, Recipe, Dataset Version, model-state, Python-process, and public file-format contracts.

## 0.3.1 — 2026-09-04

- Separate presentation, catalog, persistence-adapter, and workflow policy responsibilities.
- Guard close entry and late callbacks so shutdown does not begin new annotation work.
- Preserve existing persistence, model-state, process, and public file-format contracts.

## 0.3.0 — 2026-08-31

- Integrate dataset setup, labeling, candidate review, training, and model comparison with explicit save/action boundaries.
- Clarify image-quality, anomaly-review, Object Review, queue-presentation, and Shell orchestration responsibilities.
- Preserve annotation/Recipe persistence, queue and anomaly transitions, and close guards.

## 0.2.1 — 2026-08-25

- Keep the image queue inside the supported minimum 1100×720 workspace with its right-side margin.
- Preserve panel widths, Fit/1:1 viewer actions, annotation commands, and saved-label behavior.
- Keep annotation formats, Recipe schemas, dependencies, and model contracts unchanged.

## 0.2.0 — 2026-08-24

- Refresh supported nested and auxiliary presentation surfaces through the existing Korean/English selector without changing command, selection, or workflow state.
- Keep the exercised 125% canvas tool rail reachable and retain Object Review selection, save, navigation, and reopen behavior.
- Preserve the existing self-contained source-build contract; exhaustive localization, DPI coverage, and field quality remain outside this version's verified scope.
