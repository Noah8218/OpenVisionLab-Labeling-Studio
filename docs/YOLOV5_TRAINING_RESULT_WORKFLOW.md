# YOLOv5 Training Result Workflow

## 2026-06-27 Update - Trainable vs Replaceable

Completed: the Guide now separates dataset training readiness from model replacement readiness.

- `TrainingChecklistStatusText` can say training is possible when train/valid are usable.
- `ModelReplacementStatusText` stays on hold when `TestImageCount == 0`.
- `DatasetDashboardMetrics` includes a replacement card, so the operator can see that `best.pt` replacement is blocked even if training can run.
- Rule: a YOLOv5 smoke/train run without a held-out test split is useful for pipeline validation, but it is not enough evidence to replace the operational `best.pt`.

## 2026-06-27 Update - Candidate Review Model Difference Examples

Completed: Candidate Review can now consume the latest `artifacts/yolo-model-comparison/**/comparison-summary.json` and show example-level differences from saved YOLO `labels` outputs.

- `WpfModelComparisonReviewService` reads baseline/current and candidate/latest label txt files from the comparison summary.
- The service flags `CandidateOnly`, `BaselineOnly`, and `ClassChanged` examples using normalized-box IoU, so image dimensions are not required.
- `WpfCandidateReviewPanelViewModel` owns the model-comparison status, detail text, and example list.
- The shell only feeds the latest comparison artifact into the Candidate Review ViewModel; it does not rebuild model-comparison text in code-behind.
- The service resolves the comparison `dataYaml` and `task` image folder, so example rows carry the actual source image path.
- Candidate Review example rows are full-row command buttons. Clicking a disagreement opens that image through the existing WPF image-load path.
- The Guide training-result card now exposes a `모델 비교` command that runs `scripts/compare-yolo-models.ps1 -Task test` from the app, refreshes the latest comparison artifact, and then updates Candidate Review examples.
- `WpfModelComparisonRunService` now blocks two invalid comparison states before launching Python: an empty requested split such as a blank `test` folder, and identical baseline/candidate `best.pt` paths.
- `compare-yolo-models.ps1` now preflights label-list compatibility before launching YOLO validation. It rejects a dataset/model label-count mismatch early so the app can report a clear setup issue instead of waiting for `val.py` to fail.
- Model comparison now also requires answer label files in the final-verification split. Images alone are not enough for replacement evidence; `test/images` without matching `test/labels` keeps comparison disabled and replacement on hold.
- The Guide model-comparison button now reflects that same rule before the user clicks it: it is disabled before dataset check, disabled for blocking dataset readiness errors, disabled when `test split` is empty, disabled when final-verification labels are missing, and enabled only when the checked dataset has at least one labeled held-out test image.
- 2026-06-27 pipeline check: `compare-yolo-models.ps1 -Task test` completed with a temporary routing YAML that maps `test` to `C:/Git/yolov5/data/valid/images` so the app's test-mode execution path is verified. Output: `artifacts/yolo-model-comparison/test-routing-run/20260627-151828/comparison-summary.json`.
- 2026-06-28 pipeline check: the new label-count preflight blocked a 1-label dataset against 2-label OK/NG models before YOLO validation. A matching 2-label 125-image `val` comparison then completed at `artifacts/yolo-model-comparison/preflight-success-check/20260628-110826/comparison-summary.json`; keep this as pipeline evidence, not replacement evidence.
- 2026-06-28 labeled test fixture check: `artifacts/yolo-model-comparison/20260628-labeled-test-fixture/data.yaml` copied 10 labeled images from `C:\Git\yolov5\data\valid` into a `test` split and completed `compare-yolo-models.ps1 -Task test`. Output: `artifacts/yolo-model-comparison/labeled-test-fixture-run/20260628-135515/comparison-summary.json`. Baseline remains stronger; this is still not replacement evidence because the source images are from the existing validation set.
- 2026-06-28 true held-out public-data check: COCO128 was copied into a physically separated artifact split (`train` 96 images/labels, `valid` 16 images/labels, `test` 16 images/labels) and `compare-yolo-models.ps1 -Task test` completed against `yolov5s.pt` baseline and `yolov5m.pt` candidate. Output: `artifacts/yolo-model-comparison/coco128-true-heldout-run/20260628-151453/comparison-summary.json`. Candidate mAP50-95 was `0.657` vs baseline `0.561`, so the true held-out comparison path is verified. This is still public COCO evidence, not industrial OK/NG replacement evidence.
- 2026-06-28 industrial held-out data prep: KolektorSDD source images were converted into `artifacts/industrial-datasets/kolektor-yolo-heldout-20260628-153341/KolektorSDD/app/data.yaml` with physically separated `train` 238, `valid` 102, and `test` 59 image/label pairs. `*_label.bmp` files are no longer copied as labeling images; they are converted to 1-class `Defect` YOLO boxes. Current `best.pt`/COCO weights are not comparable because their label counts are 2/80 while this dataset is 1-class.
- 2026-06-28 industrial short-train comparison: baseline `codex_kolektor_defect_baseline_20260628_1ep` and candidate `codex_kolektor_defect_candidate_20260628_3ep` were trained with the same 1-class `Defect` list and compared on the industrial held-out `test` split. Output: `artifacts/yolo-model-comparison/kolektor-defect-heldout-run/20260628-155827/comparison-summary.json`. Both models scored precision/recall/mAP `0` and produced `0` UI candidates at confidence 25%, so the workflow is verified but model adoption is blocked.
- 2026-06-28 industrial oversampling attempt: `artifacts/industrial-datasets/kolektor-yolo-oversample-20260628-1605/KolektorSDD/app/data.yaml` increased train positive labels from 29 to 232 while keeping valid/test unchanged. The 5 epoch model `codex_kolektor_defect_oversample_20260628_5ep` reached validation recall `0.0588`, but held-out `test` comparison against the previous 3 epoch model still scored precision/recall/mAP `0` with `0/17700` UI candidates. Output: `artifacts/yolo-model-comparison/kolektor-defect-oversample-vs-short-run/20260628-162519/comparison-summary.json`.
- Remaining step: improve the industrial `Defect` training recipe and/or label coverage, then rerun the same held-out comparison until test recall and UI candidate quality are usable.

## 2026-06-28 Update - Visible Comparison Basis

Completed: the Guide training-result card now shows the model-comparison basis before the user runs comparison.

- `ModelComparisonBasisText` explains whether comparison is waiting for dataset check, blocked by training readiness, missing final-verification images/labels, weak because fewer than 10 final-verification labels exist, or ready for replacement judgment.
- The `모델 비교` button state and the basis sentence are fed by the same checked readiness report, so the operator does not have to infer the rule from tooltip text.
- Focused gate: `--wpf-model-comparison-heldout` verifies the blocked, weak-evidence, and recommended-evidence states.

이 문서는 3순위 과제인 `실제 YOLOv5 학습/결과 비교 플로우`의 제품 기준입니다.

목표는 사용자가 라벨링 앱 안에서 데이터셋을 만들고, YOLOv5를 학습하고, 새 `best.pt`를 현재 운영 모델과 비교한 뒤 적용 여부를 판단할 수 있게 만드는 것입니다. C# 앱은 라벨/데이터셋/상태/비교 표시를 맡고, Python 프로젝트는 실제 학습과 추론 runtime을 계속 맡습니다.

## 현재 상태

| 항목 | 상태 | 근거 |
| --- | --- | --- |
| YOLOv5 경로/worker 점검 | 완료 | `YOLO` 탭 첫 점검, `scripts\verify-first-run.ps1`, `scripts\smoke-yolo-tcp.ps1` |
| 앱 레벨 학습 세션 smoke | 완료 | `--wpf-yolo-training-session-smoke`, `docs\WPF_YOLO_TRAINING_SESSION_VERIFICATION_20260622.md` |
| 실제 YOLOv5 train.py smoke | 완료 | `docs\YOLOV5_REAL_WORKFLOW_VERIFICATION_20260626.md` |
| 기존 모델 vs 새 모델 비교 스크립트 | 완료 | `scripts\compare-yolo-models.ps1`, `docs\YOLO_MODEL_COMPARISON_20260622.md` |
| results.csv 기반 guide verdict | 완료 | `WpfTrainingWeightsService`가 mAP/precision/recall/loss와 간단한 판정을 만듭니다. |
| 학습 결과 리포트 UI | 완료 | Guide 탭에서 판정, 핵심 지표, 최신 모델, 현재 모델을 구조화된 항목으로 보여줍니다. |
| 모델 비교 기준 표시 | 완료 | Guide 탭에서 최종 검증 라벨 수, 권장 수, 교체 근거 강도를 `비교 기준` 문구로 보여줍니다. |
| true held-out 비교 실행 경로 | 완료 | COCO128 물리 분리 test split으로 `compare-yolo-models.ps1 -Task test` 통과. 산업 OK/NG 채택 판단은 별도 필요. |
| 산업 Defect held-out 데이터 준비 | 완료 | Kolektor 마스크를 YOLO `Defect` 박스로 변환하고 train/valid/test labels 생성. 같은 class list 모델 학습 후 비교 가능. |
| 산업 Defect 짧은 학습/비교 | 완료/보류 | 1ep vs 3ep weight 비교는 끝까지 통과했으나 test mAP와 UI 후보가 모두 0이라 채택 금지. |
| 산업 Defect oversampling 실험 | 완료/보류 | train positive 29->232, 5ep 학습 완료. validation recall은 미세하게 생겼지만 held-out test mAP/UI 후보는 0이라 채택 금지. |

## 사용자 플로우

1. 데이터셋 목적을 `객체탐지`로 둡니다.
2. 이미지 폴더를 열고, 클래스 목록을 등록합니다.
3. 박스 라벨을 만들거나 정상 이미지를 빈 라벨로 완료 처리합니다.
4. `점검`으로 train/valid/test 이미지 수, 라벨 수, 클래스 불균형, 빈 test split을 확인합니다.
5. YOLO 학습 설정에서 image size, batch, epochs, cfg, weights, validation/test split을 확인합니다.
6. `시작`을 눌러 Python YOLOv5 학습을 실행합니다.
7. 학습 중에는 상태, epoch/progress, 마지막 메시지, 실패 사유가 상단/YOLO 패널에 보여야 합니다.
8. 학습 완료 후 최신 `best.pt` 후보를 찾고, `results.csv` 지표를 읽습니다.
9. 새 모델과 현재 모델을 같은 split, 같은 confidence/IoU 기준으로 비교합니다.
10. 새 모델이 명확히 좋을 때만 `best.pt`를 적용하고 recipe 저장을 유도합니다.
11. 적용 후 현재 이미지 또는 test split에서 추론을 실행하고 Candidate Review로 실제 작업량을 확인합니다.

## 비교 기준

새 모델 적용 전 최소 비교 항목은 아래입니다.

| 기준 | 이유 |
| --- | --- |
| mAP50-95 | 모델 품질의 주 판단 지표입니다. |
| mAP50 | 대략적 위치 검출 성공률을 봅니다. |
| precision/recall | 과검출과 미검출의 균형을 봅니다. |
| class별 지표 | OK/NG처럼 소수 클래스가 망가지는지 봅니다. |
| UI confidence 기준 후보 수 | 실제 앱에서 사용자가 검토해야 하는 후보량입니다. |
| missed/extra candidates | 초보자에게 모델 차이를 설명할 때 가장 직관적입니다. |
| test split 결과 | 모델 교체 판단은 train/valid가 아니라 test split 기준이어야 합니다. |

## 적용 금지 조건

아래 중 하나라도 해당하면 새 `best.pt`를 기본 모델로 바꾸지 않습니다.

- train/valid/test 이미지가 같은 파일로 섞여 있습니다.
- test split이 비어 있는데 운영 모델 교체를 판단하려고 합니다.
- NG/불량 클래스 샘플 수가 너무 적어 class별 지표를 믿기 어렵습니다.
- 새 모델의 UI confidence 기준 후보가 거의 없거나 과도하게 많습니다.
- `results.csv`가 없거나 mAP/precision/recall을 읽을 수 없습니다.
- 비교 스크립트가 baseline과 candidate를 같은 data.yaml, task, confidence 기준으로 실행하지 않았습니다.

## 코드 위치

| 책임 | 위치 |
| --- | --- |
| 학습 설정 ViewModel | `0. UI/9) WPF/ViewModels/WpfTrainingSettingsPanelViewModel.cs` |
| 학습 weight/results.csv 검색과 비교 문구 | `0. UI/9) WPF/Services/WpfTrainingWeightsService.cs` |
| 학습 guide history | `0. UI/9) WPF/Services/WpfTrainingGuideHistoryService.cs` |
| 학습 command orchestration | `0. UI/9) WPF/Views/WpfLabelingShellWindow.Training*.cs`, `ProjectTrainingWeights.cs` |
| 데이터셋 readiness/diagnostics | `Yolo/YoloDatasetReadinessService.cs`, `Yolo/YoloDatasetDiagnosticsService.cs`, `Yolo/YoloDatasetValidator.cs` |
| 모델 비교 자동화 | `scripts/compare-yolo-models.ps1` |

## 필수 게이트

YOLOv5 학습/결과 비교를 건드렸다면 아래를 우선 확인합니다.

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-yolo-training-session-smoke
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-model-comparison-heldout
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --real-yolo-smoke
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3
powershell -ExecutionPolicy Bypass -File .\scripts\compare-yolo-models.ps1 -Task test
```

`-Task test`는 실제 test split이 있을 때 운영 모델 교체 판단용으로 사용합니다. test split이 없다면 smoke는 가능하지만 모델 채택 근거로 쓰지 않습니다.

## 다음 구현

1. Kolektor `Defect` 1클래스 기준으로 다음 학습 recipe를 개선합니다. oversampling 단독은 부족했으므로 image size 상향, 마스크 기반 박스 padding, 더 긴 epoch, augmentation 조정을 우선 검토합니다.
2. 같은 `Defect` class list를 가진 현재/신규 weight로 산업 true held-out test split 비교를 반복하고, test recall/UI 후보 수가 개선될 때만 채택 후보로 올립니다.
3. 리포트 UI에 class별 OK/NG 품질과 false positive/false negative 예시를 추가합니다.
