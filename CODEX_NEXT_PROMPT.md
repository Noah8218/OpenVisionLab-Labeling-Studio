# Next Codex Prompt

Copy the text below into the next Codex chat when continuing C:\Git\Labelling_Application.

~~~text
Continue work in C:\Git\Labelling_Application.

Read and follow AGENTS.md first.

Required start order:
1. Run git status --short first.
2. Read AGENTS.md.
3. Read docs/NEXT_THREAD_HANDOFF.md. It is the current project handoff and source-of-truth summary.
4. Read this CODEX_NEXT_PROMPT.md, docs/WORK_TRACKING.md, docs/STABLE_VERIFIED_AREAS.md, and docs/LABELING_STUDIO_COMPLETENESS_AUDIT.md.
5. Inspect the actual current diff before selecting work. The dirty worktree is authoritative over documentation.
6. Before editing or running follow-up commands, state:
   - immediate priority;
   - remaining product priority;
   - product identity;
   - current maturity estimate and source;
   - relevant commercial-product lesson;
   - out-of-scope platform breadth.

Current checkpoint:
- Workspace: C:\Git\Labelling_Application
- Branch: main; `ed50831 feat: add model adapter comparison workflows` is pushed to `origin/main`.
- The current uncommitted slice completes external native YOLO *segmentation* intake and its approved 30-epoch U-Net / YOLOv8-seg same-data benchmark. It has current local build, focused-test, Python self-test, runtime, source-immutability, and 60-image Model Center evidence, but must be inspected and intentionally committed only when the operator asks.
- Read `docs/WORK_TRACKING.md` section `2026-07-21 external native YOLO segmentation canonical-mask intake` and `docs/STABLE_VERIFIED_AREAS.md` section `external native YOLO runtime-copy and paired-comparison contract` before selecting work. These newer records supersede stale priorities below.

Product direction:
- Build a local Windows industrial labeling, training, inference, review, and model-evidence workstation.
- Treat each recipe as canonical image/class/annotation/split/evidence data, then use verified adapters to format it for selected model input contracts and compare their normalized evidence.
- Teach the operator what labeling data means and how the same data changes model quality, while keeping model testing reproducible.
- Do not treat every GitHub model as automatically supported; require an explicit adapter contract for class, coordinate, split, training/inference, result, and evidence mapping.
- Keep a single-operator task-oriented UI. Use separate read-only analysis windows for dense data/model information.
- Do not expand into cloud collaboration, accounts, reviewer assignment, deployment, or enterprise governance.
- The focused single-operator maturity estimate remains 4.0/5. It is workflow maturity, not model accuracy.
- The highest real product-quality blockers are independent NG-rich object-detection data and independent production-camera/cross-session anomaly data.
- The supplied circular-disk 500 OK / 500 NG package is complete synthetic workflow evidence: exact metadata-backed 5-class detection data, YOLOv5/YOLOv8 one-epoch connectivity, a controlled 20-epoch 150-image test benchmark, and a new 20-epoch anomaly candidate. The anomaly candidate remains `hold`; the detection benchmark favors YOLOv8n (`mAP50/mAP50-95 0.955/0.678`, 27.575ms) over YOLOv5s (`0.900/0.567`, 52.45ms) but is explicitly `engine-benchmark`, not adoption. The fixed comparison cleanup preserves the exact source-tree SHA-256. The package is derived from one earlier OK source image; do not present it as independent camera evidence. Read `docs/CIRCULAR_DISK_SYNTHETIC_1000_EVIDENCE_20260720.md`.

Current immediate priority:
- The external native YOLO segmentation source contract and approved 30-epoch same-data benchmark are complete: selected `data.yaml` source is read-only, U-Net receives a recipe-owned canonical raster-mask export, and every YOLO training request receives an app-owned runtime copy so cache files cannot mutate the source. U-Net CUDA versus YOLOv8-seg CPU completed 30 epochs at image 320/batch 4 on the same 360/80/60 packet; the 60-image common-mask report favors U-Net (`Dice/IoU 0.243091/0.156165`) over YOLOv8-seg (`0.079059/0.044103`), without selecting either model.
- Do not repeat the 30-epoch benchmark unless source, runtime, behavior, acceptance criteria, or a deliberate hyperparameter decision changes. The controlled error analysis and one final held-out replay are complete: the runner now records an explicit YOLO `0.25` confidence, which was selected on `valid` only; the unchanged test replay measured YOLOv8-seg Dice/IoU `0.721702` / `0.570198` versus U-Net `0.243091` / `0.156165`. Do not adopt either model automatically. Keep U-Net's two zero-Dice classes as a separate class-confusion/training hypothesis; independent camera/session data remains required for any production claim. See `docs\SEGMENTATION_E30_CONFIDENCE025_TEST_EVIDENCE_20260722.md`.
- The previously dirty Dataset Health, external native YOLO data.yaml intake, Image Queue responsiveness, anomaly isolation, model-comparison manifest, and anomaly OK/NG workspace slices are independently reviewed and committed. Keep their documented contracts stable unless a focused regression fails or requirements change.
- The operator deferred the unavailable real camera/network Image Queue profile. Keep the 10,000-image queue path stable and do not add paging, a DB, or another thumbnail cache from the existing local profiles.
- `D:\라벨테스트` now supplies completed synthetic cross-product evidence: native Switch Housing object detection held the YOLOv8n candidate after a five-repeat test comparison, and its 300-image anomaly mapping held the Washer classifier. Preserve the artifacts and do not rerun them unless source, weight, threshold, or acceptance criteria change.
- The next evidence path is still independent NG-rich production-camera object detection. Read-only preflight found `D:\기타이미지\2022.11.16_SIT 이미지`: 10,447 JPEGs under image-level `OK` (5,950) and `NG` (4,497) folders, with zero YAML or annotation files. Do not treat it as an object-detection test split until the operator confirms the source and supplies object classes/bounding-box labeling rules; then require a labeled held-out split with no content overlap before a five-repeat comparison.
- The detailed 2026-07-20 SIT audit found 9,996 unique image contents, 111 duplicate-content groups, and 18 groups carrying conflicting OK/NG labels; no duplicate group crosses PC1/PC2. Remove or adjudicate those conflicts and prove content-hash split separation after box labeling. See `docs\WORK_TRACKING.md` for the reusable audit record.
- Do not start another broad UI redesign without a reproduced operator defect. The current task tabs, Dataset Health window, model-comparison workspace, Model Center workspace, and model-adapter catalog are completed contracts.
- The narrow user-approved Model Center workspace slice is complete: stage 4 is now full-width and presentation-only, with current-build evidence under `artifacts\ui\model-workspace-20260718`. Do not reopen it for general polish; reopen only for a reproduced layout or state-preservation defect.
- The explicit Model Adapter Catalog/Contract slice is complete: the Model Center now shows the declared recipe-format, YOLOv5, local-YOLOv8, ONNX inference-only, and blocked-YOLO11 boundaries with task/data/runtime/evidence/next-action contracts. Do not reopen it for generic GitHub download/run breadth; reopen only for a reproduced contract, inventory, binding, or clipping defect.
- The anomaly `OK`/`NG` folder-name intake is complete as explicit review consent, not auto-labeling: opening folders, readiness, and export must keep images unreviewed until the temporary Image Queue card's `N장 일괄 판정` action is chosen. `이미지별 확인` stays unreviewed for that image-root session, saved manual decisions always win, and the header must not gain permanent buttons. Evidence and exact regression gates are in `docs/STABLE_VERIFIED_AREAS.md` and `docs/NEXT_THREAD_HANDOFF.md` section A0.

- The anomaly labeling workspace is complete as an image-level `OK/NG 이미지 판정` flow: no drawing/label-save/class-object controls are shown for anomaly purpose; the queue owns `정상(OK) → 다음`, `이상(NG) → 다음`, and `미판정으로 되돌리기`, shows `OK`/`NG`/`미판정`, persists through `anomaly-review-status.json`, and restores the standard annotation workspace when the purpose changes. The folder-consent buttons are now `N장 일괄 판정` and `이미지별 확인`; manual decisions still win. Do not reopen this slice unless a focused regression fails or the saved-state/training contract changes.

- Reproduced anomaly image-root defect fixed: after selecting an `images` root with nested `NG` and `OK` folders, automatic first-file loading and later queue-row clicks retain `images` as the queue root. The exact regression is in `--anomaly-folder-auto-review`; do not reintroduce leaf-folder queue repopulation.

Runtime rules:
- YOLOv8 uses C:\Git\yolov8, local ultralyticsMaster, local .venv, editable install, and labeling_tcp_client.py.
- ONNX is inference-only support, not a replacement for local training.
- Do not download weights, upgrade pip/packages, or alter dependencies without explicit approval.
- YOLO11 remains blocked until actual local runtime and compatible weights are proven.
- Preserve MVVM. Avoid Viewer/OpenGL/ROI/brush/eraser changes unless a specific defect requires them.
- Do not push unless the user explicitly says push. A commit request is local only.

Evidence rules:
- Code changes require isolated build, focused tests, and git diff --check.
- UI changes require current 1920x1080 evidence and a README/tutorial image relevance check.
- Do not claim model adoption, independent accuracy, or production readiness from synthetic, duplicate, validation-only, or same-source data.

Read docs/NEXT_THREAD_HANDOFF.md sections 7 through 11 before selecting focused tests. It lists:
- completed/protected areas;
- committed feature-slice map;
- current model evidence boundaries;
- unverified/blocked risks;
- ordered next priorities and matching verification commands.

Final report must include changed files, command results, screenshot requirement, remaining risk/unverified status, and the next priority with recommended model and reasoning effort.
~~~
