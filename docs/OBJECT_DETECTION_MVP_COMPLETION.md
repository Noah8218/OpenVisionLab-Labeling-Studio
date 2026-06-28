# Object Detection MVP Completion Criteria

## 2026-06-27 Object-Detection EXE Verification Addendum

Verified as complete:

- Industrial object-detection real-use long smoke labels 30 images, saves 30 non-empty YOLO label files, completes 5 empty-normal images, reopens saved labels, and runs dataset readiness successfully.
- Queue reopen recovers saved split image copies from `data/train|valid|test/images` when the original staging queue path no longer exists after save.
- Queue Open is protected against stale UI selection by resolving the DataGrid-selected item, ViewModel-selected item, unique search match, and single visible filtered row before loading.
- Beginner dashboard now surfaces a first `Next:` action for object detection readiness: load images, register class, draw first box, complete incomplete images, split duplicates, add valid/test images, or proceed when ready.

Measured gate:

```text
--exe-industrial-object-labeling-smoke --seed 260626 --label-count 30 --empty-completion-count 5
imagesCopied=317 imagesLabeled=30 emptyLabelFilesSaved=5 selectedMissingImages=0 selectedMissingLabels=0 duplicateImageStems=0 reopenVerified=True datasetCheck=True boxAvgMs=236.8 boxMaxMs=292.6
```

이 문서는 라벨링 프로그램의 첫 번째 제품 완성 기준을 `객체탐지 박스 라벨링`으로 고정하기 위한 체크리스트입니다. 목표는 완벽한 전체 제품이 아니라, 초보자와 작업자가 실제로 이미지 폴더를 열고 박스를 그리고 YOLO 후보를 검토해 학습용 데이터셋을 만들 수 있는 최소 완성 흐름을 정의하는 것입니다.

## MVP 목표

객체탐지 MVP는 아래 흐름이 끊기지 않으면 완료로 봅니다.

1. 프로젝트 또는 데이터셋 목적을 객체탐지로 설정한다.
2. 이미지 폴더를 큐에 로드한다.
3. 첫 이미지를 캔버스에서 확인한다.
4. 박스 도구로 결함/대상 객체를 라벨링한다.
5. Object Review에서 라벨을 선택, 삭제, class 변경할 수 있다.
6. 저장하면 YOLO box label 파일과 이미지 복사본이 split 정책에 맞게 생성된다.
7. 다음 미완료 이미지로 이동할 수 있다.
8. YOLOv5 추론을 실행하면 검출 후보가 Candidate Review에 표시된다.
9. 검출 후보는 확정, 스킵, 기존 라벨 확인, 이미지 완료가 가능하다.
10. 빈 정상 이미지는 빈 라벨 파일로 완료 처리되어 다시 미완료 목록으로 돌아오지 않는다.
11. 데이터셋 대시보드와 readiness check가 현재 저장 상태를 설명한다.
12. 상단 상태바가 현재 단계, 완료 수/남은 수, 다음 행동을 계속 보여준다.

## 현재 완료로 보는 영역

아래 항목은 이미 검증된 영역으로 보고, 새 이슈가 없으면 재작성하지 않습니다.

| 영역 | 완료 판단 |
| --- | --- |
| ROI 박스 그리기/선택/삭제 | 50만 ROI 성능 테스트와 real-EXE 랜덤 박스 smoke가 통과했습니다. |
| Texture pan/zoom | MouseMove fast path와 delete-then-zoom 성능 샘플이 보호 문서에 기록되어 있습니다. |
| Object Review | 선택, 삭제, class 적용, 저장 후 재열기 경로가 검증되어 있습니다. |
| YOLO box 저장 | 원본 확장자 보존, stale same-stem 이미지 제거, split 단일 소유 정책이 검증되어 있습니다. |
| Candidate Review | 검출 후보/기존 라벨 비교, 선택 요약, 중복 안내, 기존 라벨 선택, 이미지 완료가 검증되어 있습니다. |
| Empty normal completion | 빈 라벨 파일 저장과 검출없음 상태 유지가 검증되어 있습니다. |
| 실제 EXE 라벨링 루프 | 산업 이미지 30장 랜덤 박스 + 5장 empty completion long smoke 기록이 있습니다. |
| 상단 진행 상태 | 현재 단계, 완료 수/남은 수, 다음 행동이 상단 상태바에 표시됩니다. |
| 첫 시작 안내 | 빈 상태에서는 상단 상태바가 `데이터셋 준비`와 `데이터셋 시작`을 먼저 보여주고, 가이드 패널은 선택한 목적별 첫 행동을 표시합니다. |

## 2026-06-28 Top Workflow UX Addendum

완료 처리와 다음 단계 흐름은 아래 기준으로 봅니다.

- `라벨 없는 다음 이미지` 같은 설명형 문구는 사용하지 않습니다.
- `YOLO 다음 액션`, `완주 체크`처럼 개발자식 또는 번역투 표현은 사용하지 않고, 첫 화면에서는 `다음 작업`, `완료 체크`를 씁니다.
- 큐 필터와 안내 문구는 `미완료`, `다음 이미지`, `이어서 작업`, `이미지 완료`를 기준으로 합니다.
- `이미지 완료`는 현재 이미지의 라벨 저장 또는 객체 없음 완료를 의미합니다.
- 마지막 이미지에서 `이미지 완료`를 누르면 데이터셋 점검 상태가 갱신되어 라벨링 단계에 머물지 않습니다.
- 상단 상태바는 항상 `단계`, `진행`, `다음`을 보여야 합니다.
- 객체가 없는 정상 이미지는 빈 라벨 파일을 저장한 뒤 완료된 이미지로 계산합니다.
- 가이드 데이터셋 대시보드는 `객체탐지 MVP 완료까지` 문구로 남은 작업을 한 번 더 요약해야 합니다. 이 문구는 대시보드의 즉시 행동과 같은 판단을 사용해야 하며, 별도 추측 로직으로 갈라지면 안 됩니다.

Measured gate:

```text
--wpf-status-panels
--wpf-image-queue-status
--wpf-candidate-review-panel
--wpf-canvas-workflow-context
--wpf-learning-workflow-panel
--wpf-labeling-session-smoke
--wpf-canvas-detection-overlay
--wpf-visual-smoke --review-tab guide
--wpf-visual-smoke --review-tab candidates
--exe-industrial-object-labeling-smoke --seed 260626 --label-count 3 --empty-completion
imagesCopied=317 imagesLabeled=3 emptyCompletion=True emptyLabelFilesSaved=1 reopenVerified=True datasetCheck=True boxAvgMs=232.4 boxMaxMs=276.1 datasetCheckMs=3350.6
```

## 2026-06-28 First-Run Guide Addendum

빈 프로젝트나 이미지가 없는 상태에서는 사용자가 먼저 해야 할 일을 아래 기준으로 보여줍니다.

- 상단 상태바 단계는 `단계: 데이터셋 준비`입니다.
- 상단 상태바 다음 행동은 `다음: 데이터셋 시작`입니다.
- 가이드 패널의 데이터셋 준비 영역은 선택한 목적에 맞춰 첫 행동을 설명합니다.
- 검출 후보 안내는 `AI 후보` 대신 `검출 후보`를 사용합니다.

Measured gate:

```text
--wpf-learning-workflow-panel
--wpf-status-panels
--wpf-visual-smoke --review-tab guide
--wpf-dataset-wizard-smoke
```

## 2026-06-28 Overlap Selection UX Addendum

겹친 후보와 기존 라벨이 같이 보일 때는 아래 기준을 완료로 봅니다.

- Candidate Review 선택 요약은 선택한 검출 후보, 겹친 현재 라벨, 다음 조치를 한 줄 흐름으로 보여줍니다.
- 중복 가능 후보는 기존 라벨 확인 후 같은 객체면 스킵하라고 안내합니다.
- 새 후보는 현재 라벨과 겹침 없음, 맞으면 확정하라고 안내합니다.
- 캔버스의 검출 결과 HUD 제목은 `검출 결과`를 사용합니다.
- 캔버스 후보 라벨은 `후보 1 OK` 형식으로 표시하고 `AI 1 OK` 같은 내부 표현을 쓰지 않습니다.

Measured gate:

```text
--wpf-candidate-review-panel
--wpf-canvas-detection-overlay
--wpf-visual-smoke --review-tab candidates
--wpf-labeling-session-smoke
```

## 2026-06-28 Model Adoption Decision Addendum

2026-06-28 update: final-verification images alone are not enough. Model comparison requires matching answer label files under the final-verification split; use labeled final-verification count for the 10-image evidence recommendation.

학습 결과 비교 화면은 아래 기준까지 완료로 봅니다.

- Guide의 학습 결과 카드가 `교체 판단:` 한 줄 결론을 먼저 보여줍니다.
- 새 학습 모델 지표가 우세하면 `새 모델 후보 우세 - 최종 검증 예시 확인 후 적용`으로 표시해, 바로 교체가 아니라 예시 확인 후 적용 흐름을 안내합니다.
- 최신 결과가 없거나 지표가 없거나 비교가 실패하면 `보류` 또는 `비교 필요` 상태로 표시합니다.
- 이미 현재 모델로 사용 중인 결과는 `이미 현재 모델로 사용 중`이라고 표시해 중복 적용을 피합니다.
- 최종 검증 이미지가 1장 이상이면 비교는 가능하지만, 10장 미만이면 `근거 부족`/`주의`로 표시해 운영 모델 교체 근거가 약하다는 점을 분리해서 보여줍니다.
- Candidate Review의 모델 차이 예시는 `확인:` 조치 문구를 함께 보여주어 과검출, 누락, 클래스 차이를 바로 검토 행동으로 연결합니다.
- 모델 차이 예시를 클릭하면 원본 이미지가 열리고, 차이 위치가 캔버스의 선택된 박스와 검토 카드 위치 문구로 바로 표시됩니다.
- 비교가 끝나면 Candidate Review가 `예시 클릭 -> 위치 확인 -> Guide 교체 판단 확인` 흐름을 직접 안내해야 합니다.

Measured gate:

```text
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
--wpf-learning-workflow-panel
--wpf-candidate-review-panel
--wpf-visual-smoke --review-tab guide
--wpf-visual-smoke --review-tab candidates
full default LabelingApplication.Tests regression
```

## 남은 UX 리스크

아래는 아직 완료로 보지 않는 UX 항목입니다.

1. 새 사용자가 빈 프로젝트에서 시작해 실제 이미지 폴더와 class를 넣는 전체 EXE wizard 흐름은 더 긴 사용자 테스트가 필요합니다.
2. 후보 검토에서 매우 많은 후보가 한 지점에 겹친 경우의 실제 EXE 장시간 조작성은 추가 확인이 필요합니다.
3. 학습 결과 비교와 교체 근거 강도 표시는 완료했지만, 실제 held-out test 이미지로 장시간 비교한 모델 교체 근거는 아직 추가 검증이 필요합니다.
4. 세그멘테이션과 이상탐지는 객체탐지 MVP 수준의 실제 EXE 완료 루프가 아직 부족합니다.
5. 문서와 내부 테스트명에는 `YOLO`, `Candidate`, `AI` 같은 개발 용어가 남아 있을 수 있으나, 사용자 화면에는 라벨링 중심 용어를 우선합니다.

근거 문서:

- `docs/STABLE_VERIFIED_AREAS.md`
- `docs/LABELING_PROGRAM_DIRECTION.md`
- `docs/YOLOV5_REAL_WORKFLOW_VERIFICATION_20260626.md`

## MVP 필수 게이트

객체탐지 MVP 관련 변경을 완료했다고 보고하기 전에는 아래 중 변경 범위에 맞는 게이트를 통과해야 합니다.

### 빠른 구조/UX 게이트

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-object-review-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-candidate-review-panel
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-image-queue-status
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-current-image-smoke-preserve-labels
git diff --check
```

### Viewer 성능 게이트

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-500k-mouse-event-performance
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --roi-500k-delete-performance
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --texture-pan-performance
```

### 실제 EXE 게이트

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-roi-tools-smoke --seed 260626 --box-count 12
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260626 --label-count 30 --empty-completion-count 5
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-candidate-focus-smoke
```

### 전체 회귀 게이트

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build
```

## MVP에서 제외하는 것

아래는 중요하지만 객체탐지 MVP 완료 조건에는 넣지 않습니다.

- YOLOv8 전환
- ONNX runtime inference
- U-Net/segmentation 학습 runtime
- anomaly detection 전용 학습/평가 UI
- 모델 성능 자동 비교 리포트의 고급 시각화
- 다중 사용자/서버형 프로젝트 관리

이 항목들은 객체탐지 MVP가 흔들리지 않는 상태에서 별도 우선순위로 진행합니다.

## 변경 시 주의할 파일군

| 목적 | 먼저 볼 위치 |
| --- | --- |
| 박스 그리기/선택/삭제 | `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/RoiInteraction`, `WpfLabelingShellWindow.Annotation*.cs`, `WpfLabelingShellWindow.ObjectReview*.cs` |
| Object Review UI | `WpfObjectReviewPanel.xaml`, `WpfObjectReviewPanelViewModel.cs`, `WpfObjectReview*Service.cs` |
| Candidate Review UI | `WpfCandidateReviewPanel.xaml`, `WpfCandidateReviewPanelViewModel.cs`, `WpfCandidateReview*Service.cs` |
| 이미지 큐/다음 이미지 | `WpfImageQueuePanelViewModel.cs`, `WpfImageQueue*Service.cs`, `WpfLabelingShellWindow.ImageQueue*.cs` |
| YOLO box 저장 | `YoloAnnotationService.cs`, `YoloDatasetSplitService.cs`, `YoloImageReviewStatusService.cs` |
| 추론 후보 적용 | `DetectionResultApplicationService.cs`, `WpfLabelingShellWindow.Detection*.cs` |
| 데이터셋 점검 | `YoloDatasetValidator.cs`, `YoloDatasetReadinessService.cs`, `YoloDatasetDiagnosticsService.cs` |

## 남은 보강 항목

객체탐지 MVP를 더 단단하게 만들기 위한 다음 보강은 아래 순서가 좋습니다.

1. 실제 YOLOv5 모델을 쓰는 후보 검토 smoke를 fake client와 분리해 정례화한다.
2. 처음 10분 튜토리얼에서 객체탐지 MVP 완료 흐름을 실제 EXE 기준으로 다시 캡처한다.
3. 데이터셋 대시보드의 “MVP 완료까지 남은 작업”과 상단 상태바의 `다음` 문구가 같은 행동을 가리키는지 계속 검증한다.
4. Candidate Review에서 클래스 변경/기존 라벨 편집 흐름을 더 빠르게 만든다.
5. 모델 학습 결과 비교는 객체탐지 MVP 밖의 다음 단계로 분리하되, readiness check와 연결한다.
