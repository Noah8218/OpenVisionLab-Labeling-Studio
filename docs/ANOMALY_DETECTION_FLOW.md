# Anomaly Detection Flow

이 문서는 5순위 과제인 `이상탐지 플로우`의 제품 기준입니다.

이상탐지는 객체탐지나 세그멘테이션과 목표가 다릅니다. 사용자가 "어디에 어떤 물체가 있다"를 항상 라벨링하는 것이 아니라, 정상/비정상 데이터의 기준을 만들고 모델이 낯선 결함을 찾도록 돕는 흐름이 중심입니다.

## 제품 원칙

- 이상탐지는 별도 dataset purpose입니다.
- 객체탐지 박스 도구와 세그멘테이션 브러시를 재사용할 수는 있지만, 화면 설명은 `정상/비정상 기준 만들기`가 먼저입니다.
- 초보자에게는 image-level 정상/비정상 분류를 먼저 보여주고, region-level 결함 표시를 선택 기능으로 둡니다.
- Python runtime/model은 아직 확정하지 않습니다. C# 앱은 데이터셋 목적, 라벨 타입, 저장 상태, 튜토리얼 UX를 먼저 안정화합니다.

## 권장 플로우

1. 데이터셋 목적에서 `이상탐지`를 선택합니다.
2. 앱은 먼저 이미지별 상태를 `정상`, `비정상`, `미검토`로 분리해 보여줍니다.
3. 정상 이미지는 빠르게 완료 처리할 수 있어야 합니다.
4. 비정상 이미지는 필요에 따라 박스 또는 mask로 결함 위치를 보조 표시합니다.
5. dashboard는 정상/비정상 이미지 수, split별 분포, 비정상 부족, test split 부족을 먼저 보여줍니다.
6. 학습 단계에서는 선택한 anomaly backend가 아직 없으면 export/readiness까지만 안내합니다.
7. inference 단계에서는 anomaly score, threshold, heatmap, false positive/false negative review가 들어갈 자리를 별도 영역으로 둡니다.

## 라벨 타입

| 타입 | 용도 | 우선순위 |
| --- | --- | --- |
| image-level normal/abnormal | 이상탐지의 기본 학습 단위 | 1순위 |
| region box | 결함 위치를 대략적으로 가르치거나 review 설명에 사용 | 2순위 |
| region mask | 결함 영역이 작고 경계가 중요한 경우 | 2순위 |
| class catalog | 불량 종류를 나누는 보조 정보 | 3순위 |

## 완료 기준

| 영역 | 완료 조건 |
| --- | --- |
| 목적 선택 | anomaly purpose를 선택해도 object detection/segmentation의 상태가 섞여 보이지 않습니다. |
| 정상 완료 UX | 정상 이미지를 빠르게 완료하고 다음 이미지로 넘어갈 수 있습니다. |
| 비정상 표시 UX | 박스 또는 mask를 보조 라벨로 추가할 수 있지만, 필수 입력처럼 보이지 않습니다. |
| dashboard | 정상/비정상 분포와 split 부족이 첫 화면에서 드러납니다. |
| 저장 계약 | manifest에 anomaly purpose와 image-level 상태가 남습니다. |
| 학습 handoff | Python backend가 정해지기 전에는 runtime을 넣지 않고 export/readiness만 제공합니다. |
| 검증 | 목적 전환, 저장/재열기, 정상 완료, 보조 region label이 자동화됩니다. |

## 현재 코드 기준

| 책임 | 위치 |
| --- | --- |
| dataset purpose enum | `1. Core/LabelingProjectSettings.cs` |
| purpose별 manifest profile/tools | `1. Core/LabelingDatasetManifestService.cs` |
| guide mode | `0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs` |
| purpose 적용 | `0. UI/9) WPF/Views/WpfLabelingShellWindow.DatasetSetupCommands.cs`, `ShellProjectSettings.cs` |
| readiness wording | `0. UI/9) WPF/Views/WpfLabelingShellWindow.TrainingGuideStatus.cs` |
| box/mask 보조 annotation | 기존 ROI와 segmentation 서비스 |

## 아직 구현하지 않는 것

- C# 내부 anomaly model 학습
- ONNX anomaly runtime
- U-Net/segmentation runtime을 anomaly로 억지 재사용
- 자동 heatmap UI
- threshold tuning UI

이 항목들은 실제 Python backend가 정해진 뒤 별도 설계로 진행합니다.

## 필수 게이트 초안

아직 anomaly 전용 gate는 부족합니다. 구현을 시작하면 최소 아래 gate를 추가해야 합니다.

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-anomaly-purpose-flow
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-anomaly-normal-completion-smoke
```

gate가 생기기 전에는 anomaly 기능을 `완료`로 표시하지 않습니다. 현재는 purpose/manifest/guide의 기반만 있는 설계 단계입니다.

## 다음 구현

1. image-level normal/abnormal 상태 모델을 정합니다.
2. anomaly purpose에서 normal completion button과 next-image loop를 만듭니다.
3. manifest/review-status에 image-level anomaly 상태를 저장합니다.
4. dashboard에 정상/비정상/test split 분포를 추가합니다.
5. Python backend 후보가 정해지면 export format과 inference result DTO를 설계합니다.
