# Segmentation UX Completion

이 문서는 4순위 과제인 `세그멘테이션 UX를 객체탐지 수준으로 올리는 작업`의 완료 기준입니다.

세그멘테이션은 박스 라벨링의 변형이 아닙니다. 사용자는 결함 영역을 픽셀 단위 마스크나 폴리곤으로 표시해야 하며, 저장 결과도 YOLO box txt가 아니라 mask/png와 segment/json 중심이어야 합니다.

## 현재 상태

| 항목 | 상태 | 근거 |
| --- | --- | --- |
| polygon 생성/선택/이동/저장 | 완료 | `--wpf-segmentation-object-verification`, `docs\WPF_ANNOTATION_OBJECT_VERIFICATION.md` |
| brush/eraser raster mask 생성 | 완료 | `TestWpfBrushEraserShellInputCreatesMaskSegmentation` |
| OpenGL mask texture preview | 완료 | `RoiImageCanvasMaskOverlay`, FBO/texture preview 관련 안정화 기록 |
| brush/eraser MouseUp UX | 보호 대상 | `docs\STABLE_VERIFIED_AREAS.md`의 Brush/Eraser 항목 |
| dataset purpose별 도구/저장 정책 | 진행됨 | `LabelingDatasetPurpose.Segmentation`, `LabelingDatasetManifestService` |
| 객체탐지 수준의 beginner flow | 남음 | 샘플, 저장/재열기, dashboard, 학습 handoff가 한 흐름으로 더 묶여야 합니다. |

## 사용자 플로우

1. 데이터셋 목적에서 `세그멘테이션`을 선택합니다.
2. 앱은 기본 도구를 `select`, `polygon`, `brush`, `eraser`, `pan/zoom`, `undo`, `redo`, `save` 중심으로 보여줍니다.
3. 이미지 폴더를 열고 클래스 색상을 선택합니다.
4. 폴리곤 또는 브러시로 결함 영역을 만듭니다.
5. Object Review에는 `Polygon` 또는 `Mask` 객체가 선택 가능한 항목으로 보여야 합니다.
6. 선택한 polygon point 또는 mask body를 움직이면 같은 객체가 수정되어야 합니다.
7. 지우개로 mask를 지우면 비어 있는 mask는 Object Review에서 사라집니다.
8. 저장하면 `data/<split>/masks/*.png`와 `data/<split>/segments/*.json`가 만들어집니다.
9. 이미지를 다시 열면 이전 mask/polygon이 캔버스와 Object Review에 복원됩니다.
10. dataset dashboard는 box progress가 아니라 segmentation label/mask progress를 먼저 보여줍니다.

## 완료 기준

| 영역 | 완료 조건 |
| --- | --- |
| 목적 선택 | segmentation project에서는 box-only UX가 기본 흐름을 방해하지 않습니다. |
| 도구 discoverability | brush/eraser 크기, mask opacity, polygon 완료/취소가 한눈에 보입니다. |
| 편집 UX | 생성, 선택, 이동, 지우기, Undo/Redo, 저장 필요 표시가 일관됩니다. |
| 저장/재열기 | mask/png와 segment/json이 split 정책에 맞게 저장되고 재열기 됩니다. |
| 검증 UX | readiness가 segmentation label 부족, mask 없음, class 없음, image 없음 문제를 분리해서 말합니다. |
| 성능 | MouseMove와 MouseUp에서 화면 멈춤이 보이면 미완료입니다. |
| 문서 | verified hot path는 `STABLE_VERIFIED_AREAS.md`에 남기고, 새 변경은 gate를 통과한 뒤에만 완료로 표시합니다. |

## 보호 규칙

브러시/지우개 속도 개선 경로는 이미 민감한 성능 경로입니다. 아래 구조는 UX 정리 명목으로 다시 동기식 처리로 되돌리지 않습니다.

- drag 중에는 OpenGL/FBO/texture preview를 우선 보여줍니다.
- CPU MaskData/history materialization은 MouseUp 이후 queue/delta 중심으로 처리합니다.
- dirty bounds가 있는 경우 전체 mask texture를 다시 올리지 않고 부분 갱신을 우선합니다.
- Object Review list rebuild와 undo snapshot은 MouseMove마다 실행하지 않습니다.
- brush/eraser 변경은 반드시 실제 EXE 또는 focused input smoke로 확인합니다.

## 코드 위치

| 책임 | 위치 |
| --- | --- |
| 목적 선택과 tool summary | `0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs` |
| 목적 적용/annotation visibility | `0. UI/9) WPF/Views/WpfLabelingShellWindow.LearningWorkflowModeCommands.cs`, `ShellProjectSettings.cs` |
| polygon 편집 | `0. UI/9) WPF/Services/WpfPolygonAnnotationService.cs` |
| brush/eraser mask 편집 | `0. UI/9) WPF/Services/WpfMaskAnnotationService.cs`, `WpfMaskEditStateService.cs`, `WpfMaskStrokeCommitSession.cs` |
| annotation history | `0. UI/9) WPF/Services/WpfAnnotationHistoryService.cs`, `WpfMaskStrokeHistoryDraftService.cs` |
| save/load | `Yolo/YoloSegmentationAnnotationService.cs`, `1. Core/LabelingWorkflowService.cs` |
| OpenGL mask overlay | `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/ViewModel/RoiImageCanvasViewModel*.cs` |

## 필수 게이트

세그멘테이션 도구, 저장, Object Review, overlay, dataset purpose를 건드렸다면 아래를 우선 실행합니다.

```powershell
dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-segmentation-object-verification
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-mask-drag-performance
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-mask-dirty-bounds
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-mask-tools-smoke --seed 260626 --brush-strokes 8 --eraser-strokes 4
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-segmentation-object-interactions.ps1
```

## 다음 구현

1. 세그멘테이션 전용 첫 실행 샘플 플로우를 만듭니다.
2. dataset dashboard에서 segmentation progress를 box progress보다 먼저 보여줍니다.
3. 저장 후 재열기 EXE smoke를 객체탐지 수준으로 확장합니다.
4. mask detail editing 요구가 실제 사용에서 나오면 reshape/add/subtract UX를 설계합니다.
5. U-Net runtime은 별도 Python 프로젝트 방향이 확정될 때까지 C# 앱 안에 넣지 않습니다.
