# YOLO11 로컬 비교 실행 검증 (2026-07-21)

Status: Complete

## Scope

- 기존 객체 탐지 YOLOv5 ↔ YOLOv8 비교 흐름을 유지한다.
- 선택 모델이 YOLO11이면 같은 비교 실행이 YOLOv8 ↔ YOLO11로 전환되게 한다.
- YOLO11은 연결된 로컬 Ultralytics 런타임과 `yolo11n.pt` 시작 가중치를 사용한다.
- native `data.yaml` 원본과 라벨 트리를 변환하거나 영구 변경하지 않는다.

포함하지 않는 범위는 생산 모델 채택, 카메라/세션 독립 홀드아웃 검증, YOLO11 전용 데이터 형식 변환이다.

## 사용 흐름

1. 학습/모델 화면에서 모델 엔진을 `YOLO11`로 선택한다.
2. 로컬 Ultralytics 폴더 연결 상태와 시작/학습 완료 가중치를 확인한다.
3. 같은 객체 탐지 레시피와 외부 native `data.yaml`을 사용해 학습한다.
4. 학습 완료 영역의 `v8 vs v11 분석`을 실행한다.
5. 결과는 같은 test split, 이미지 크기, 배치, confidence 조건에서 비교하되 자동 채택하지 않는다.

Ultralytics의 YOLO11 detect train/validation/predict 지원 범위는 [공식 YOLO11 문서](https://docs.ultralytics.com/models/yolo11/)를 기준으로 한다.

## Acceptance criteria and evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| YOLO11 로컬 런타임 프로필과 시작 가중치가 선택된다 | Pass | `PythonModelRuntimeConnectionService.BuildYolo11FolderConnection` 및 `--python-model-runtime-connection` |
| 모델 센터가 YOLO11을 차단 상태가 아닌 로컬 런타임 계약으로 표시한다 | Pass | `Yolo/ModelAdapterCatalogService.cs`, `--model-adapter-catalog` |
| YOLO11 선택 시 비교 동작이 v8 ↔ v11로 전환된다 | Pass | `WpfModelComparisonRunService`, `--wpf-model-comparison-run-service`, `artifacts/ui/yolo11-engine-comparison-20260721/after-v8-v11-button-current.png` |
| 30-epoch YOLO11 객체 탐지 학습 결과가 있다 | Pass with final-tree boundary | `artifacts/real-external-yolo-dataset-training/circular-disk-supplied-1000-yolo11n-detect-e30-20260721-111230` |
| 새로운 런타임 정리 계약이 학습 뒤 원본 트리를 복원한다 | Pass | 1-epoch 실제 실행 `artifacts/real-external-yolo-dataset-training/circular-disk-supplied-1000-yolo11n-detect-e1-cache-contract-20260721-113900/summary.txt`; source file count 2,005 before/after, SHA-256 `573F0E76D2EB282A54BB136F1AC11C5F1584E68685095F312A0508444CC4FA60` before/after |
| 같은 test split으로 YOLOv8n과 YOLO11n을 비교한다 | Pass | `artifacts/yolo-model-comparison/circular-disk-supplied-1000-yolov8n-vs-yolo11n-e30-test-20260721-114300/20260721-114109/comparison-report.md` |
| 현재 Debug EXE에서 YOLO11 설정 저장·재시작·추론이 완료된다 | Pass | `artifacts/exe-yolo11-detect-restart-smoke/circular-disk-e30-20260721-115200-retry/summary.txt` 및 screenshots |

## Same-data test benchmark

Dataset contract: 700 train / 150 validation / 150 test images, 5 defect classes, image size 320, batch size 1 for the benchmark, UI confidence 0.25.

| Engine | Precision | Recall | mAP50 | mAP50-95 | Median model takt |
| --- | ---: | ---: | ---: | ---: | ---: |
| YOLOv8n (30 epochs) | 0.954 | 0.914 | 0.965 | 0.708 | 42.735 ms |
| YOLO11n (30 epochs) | 0.872 | 0.914 | 0.932 | 0.712 | 51.136 ms |

This is an `engine-benchmark` only. The current supplied dataset is generated from one source; it does not prove inspection quality, production latency, or a model-adoption decision.

## Verification

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-adapter-catalog
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-model-comparison-run-service
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-connection
dotnet build .\OpenVisionLab.LabelingStudio.sln -c Debug -p:Platform="Any CPU" /nr:false -m:1 /p:UseSharedCompilation=false
dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --exe-yolo11-detect-restart-smoke ...
dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --yolo11-engine-comparison ...
```

All commands above passed in the current task. `git diff --check` is the final source-format gate.

## Boundary

The original 30-epoch YOLO11 run produced two Ultralytics label-cache files before its old final-tree gate detected them. They were removed, and the final source manifest exactly matched its before manifest. The current worker now removes only newly created label-cache files; the 1-epoch actual contract run proves the final source tree restoration. It does not claim that Ultralytics never writes a temporary cache during a training process.
