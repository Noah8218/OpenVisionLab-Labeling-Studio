# Codex Recovery Notes

작성일: 2026-06-24 (Asia/Seoul)

## 작업 목표

라벨링 뷰어에서 ROI/오브젝트 삭제 직후 마우스 휠 줌 인/아웃이 잠시 멈췄다가 실행되는 현상을 구조적으로 진단하고 개선한다.

핵심 UX 목표:

- 단순 오브젝트 1개 삭제가 전체 OpenGL 장면 repaint나 전체 ROI 재계산을 유발하지 않아야 한다.
- 삭제 직후 들어오는 wheel zoom/pan 입력이 UI 큐에서 삭제 repaint 뒤로 밀리지 않아야 한다.
- 50만 ROI가 있어도 1개 삭제, 1개 이동, ROI resize, MouseMove, viewport culling이 bounded/incremental 경로로 동작해야 한다.
- 실제 EXE를 띄워 WPF 쉘이 정상 실행되는지도 확인한다.

## 수정한 파일

주요 변경 파일:

- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/Engine/ImageCanvasControl.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/OpenGL/OpenGlOverlayExtensions.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/ViewModel/RoiImageCanvasViewModel.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml.cs`
- `tests/LabelingApplication.Tests/Program.cs`

이전 패스에서 이미 관련된 파일:

- `0. UI/9) WPF/Services/WpfAnnotationHistoryService.cs`
- `Yolo/YoloImageReviewStatusService.cs`

## 변경 내용

### 삭제 후 줌 멈춤 원인

`DeleteOverlay()`가 오브젝트 1개 삭제 후에도 즉시 `RefreshGL()`을 호출했다. 사용자가 삭제 직후 wheel zoom을 하면, wheel zoom repaint가 삭제 repaint 뒤에 대기하면서 체감상 "삭제 후 잠깐 멈췄다가 줌"처럼 보였다.

### 적용한 구조 변경

- `ImageCanvasControl`에 input-friendly deferred repaint 경로를 추가했다.
  - `QueueRefreshGLAfterInput()`
  - `OnDeferredRefreshTimerTick(...)`
  - `CancelDeferredRefreshGL()`
- 삭제 repaint는 16ms 타이머로 지연한다.
- 일반 `RefreshGL()`이 먼저 들어오면 pending delete repaint를 취소하고 해당 refresh에 삭제 결과를 합친다.
- 이 구조 때문에 삭제 직후 wheel zoom이 들어오면 zoom refresh가 삭제 결과까지 포함해 한 번에 그린다.
- 관련 주석을 추가해, 삭제 repaint를 지연시키는 이유가 "wheel zoom 입력을 막지 않기 위함"임을 남겼다.

### OpenGL overlay 삭제 경로

`OpenGlOverlayExtensions.DeleteOverlay(...)`에 `refreshImmediately` 인자를 추가했다.

- 기본값은 기존 호환을 위해 `true`.
- ROI 단일 삭제 경로에서는 `false`를 사용한다.
- `false`일 때는 `canvasViewer.QueueRefreshGLAfterInput()`을 호출한다.
- display list release와 overlay manager remove는 즉시 수행한다.
- viewport visible cache에서는 삭제된 overlay만 제거한다.

### WPF/Canvas 삭제 호출 경로

- 객체 리뷰 패널 삭제 경로:
  - `WpfLabelingShellWindow.xaml.cs`
  - `OpenGlOverlayExtensions.DeleteOverlay(..., refreshImmediately: false)` 사용
- 캔버스 delete-key/ROI 삭제 경로:
  - `RoiImageCanvasViewModel.cs`
  - `_imageViewer.DeleteOverlay(..., refreshImmediately: false)` 사용

### 이전 패스의 관련 최적화

- ROI 삭제 undo snapshot이 mask/segment/candidate 전체를 불필요하게 clone하지 않도록 `CaptureManualRoiList` 기반 부분 snapshot 경로를 추가했다.
- 삭제 후 queue/review status refresh가 UI thread에서 파일 IO와 전체 상태 계산을 직접 하지 않도록 background refresh로 변경했다.
- `YoloImageReviewStatusService` 내부 status dictionary 접근을 lock/snapshot 기반으로 보강했다.

## 검증 결과

빌드:

- `dotnet build .\MvcVisionSystem.sln -c Debug -p:Platform=x64 -p:WpgCustomBuildEnabled=false -m:1 -nr:false`
- 최종 확인 로그: `artifacts/logs/build-20260624-delete-zoom-deferred-v16.log`
- 결과: 경고 0개, 오류 0개

집중 성능 검증:

- 50만 ROI 삭제:
  - `ROI_500K_SINGLE_DELETE_MS=6.680`
  - `DELETE_THEN_ZOOM_MS=23.722`
  - PASS
- 이후 전체 회귀 중 동일 경로 최신 측정:
  - `ROI_500K_SINGLE_DELETE_MS=3.961`
  - `DELETE_THEN_ZOOM_MS=20.495`
  - PASS
- WPF 객체 리뷰 삭제:
  - `WPF_OBJECT_REVIEW_DELETE_MS=3.307`
  - `REMAINING_ROIS=0`
  - `REMAINING_OVERLAYS=0`
  - `SELECTED_EMPTY=True`
  - PASS

실제 EXE 확인:

- 실행 파일: `artifacts/run/Debug/MvcVisionSystem.exe`
- 창 제목 확인: `OpenVisionLab Labeling Studio`
- 실행 확인 후 프로세스 종료 완료

전체 회귀:

- 삭제/ROI/브러시/500K 성능/객체 검증 구간은 반복 통과했다.
- 전체 회귀는 후반부 UI 문구 assertion에서 계속 멈췄다.
- 주 원인은 테스트 파일에 남아 있던 mojibake 기대 문자열과 실제 정상 한국어 문자열의 불일치였다.
- 마지막 확인 실패:
  - `WPF single detection avoids startup warm-up and keeps short interactive wait`
  - 원인: 수동 추론 안내 문구 비교가 깨진 문자열을 기대함
  - 조치: 정상 한국어 조각 `"추론은 사용자가 명시적으로 실행"` 기준으로 수정
  - 이후 빌드는 통과했지만, 전체 회귀 최종 green은 아직 한 번 더 확인 필요

## 남은 작업

1. 전체 회귀 최종 green 확인
   - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
   - 실패 시 UI 문구 assertion이 실제 정상 한국어를 기준으로 되어 있는지 확인한다.

2. 실제 EXE에서 수동 체감 테스트
   - ROI 1개 선택
   - 삭제 버튼 또는 Delete key 입력
   - 즉시 wheel zoom in/out
   - 기대: 삭제 직후 줌이 UI 큐에서 밀리지 않고 바로 반응

3. 테스트 파일 인코딩 정리
   - `tests/LabelingApplication.Tests/Program.cs`에 남아 있는 mojibake 기대 문자열을 정상 한국어 또는 구조 검증으로 정리해야 한다.
   - 문구 자체를 검증해야 하는 테스트는 Unicode escape 또는 리소스 기반 문자열로 고정하는 편이 좋다.

4. 삭제 repaint 지연 정책 추가 검토
   - 현재는 단일 ROI 삭제에만 `refreshImmediately: false`를 적용했다.
   - bulk clear/delete는 기존 immediate refresh를 유지한다.
   - 추후 bulk 삭제 UX에서도 멈춤이 보이면 batch remove 후 refresh 1회로 별도 경로를 만드는 것이 좋다.

5. 실제 사용자 테스트 후 추가 UX 개선
   - 삭제 후 선택 row/selection handle/객체 목록 highlight가 모두 즉시 사라지는지 확인
   - delete 직후 wheel zoom 외 pan, fit, class apply 같은 입력도 밀리지 않는지 확인

## 주의사항

- 이 작업의 핵심은 "삭제를 느리게 하지 않는 것"이 아니라 "삭제 repaint가 다음 입력을 막지 않게 하는 것"이다.
- `RefreshGL()` 자체는 여전히 필요한 전체 frame repaint이므로, 단일 객체 삭제 경로에서 즉시 호출하면 다시 같은 문제가 생길 수 있다.
- `DeleteOverlay(..., refreshImmediately: false)`는 단일 ROI/객체 삭제용이다. 모든 삭제에 무조건 적용하면 bulk 작업에서 화면 갱신 타이밍이 애매해질 수 있다.
- `QueueRefreshGLAfterInput()`의 16ms 타이머는 의도적으로 짧다. 길게 늘리면 삭제 반영이 늦어 보일 수 있고, 0ms로 줄이면 wheel zoom coalescing 효과가 사라질 수 있다.
- `CancelDeferredRefreshGL()`은 일반 `RefreshGL()` 초입에서 호출되어야 한다. 이 호출이 빠지면 delete repaint와 zoom repaint가 다시 분리될 수 있다.
- 테스트 파일은 이전 작업 중 mojibake 문자열이 많았다. 단순히 문자열을 다시 깨진 값으로 맞추기보다 실제 UX 문구가 정상 한국어인지 확인하는 방향이 맞다.
- 작업 전 `git status --short`를 확인하라. 이전 대화에서는 많은 MVVM/refactor 변경이 있었지만, 이 복구 문서 작성 시점에는 작업트리가 clean으로 확인되었다.

## 2026-06-26 추가 검증(현재 패스)

### 작업 목표

- YOLOv5 연동 테스트를 로컬 공개/산업 데이터셋 흐름으로 이어서 검증하고, 레이블링 성능 테스트를 정리된 상태로 확인한다.
- 핵심 성능/UX 회귀(`delete`-`zoom` 체감, 50만 ROI, 마스크/세그멘테이션/브러시 MouseMove, 텍스처 패닝/줌 성능) 재확인.
- `smoke` 루틴이 실제 호출 경로를 통과하는지 확인한다.

### 실행한 항목

- `dotnet build .\MvcVisionSystem.sln -c Debug -p:Platform=x64 -p:WpgCustomBuildEnabled=false -m:1 -nr:false`
- `dotnet tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --roi-500k-delete-performance`
- `dotnet tests\... --wpf-annotation-object-verification`
- `dotnet tests\... --wpf-labeling-session-smoke`
- `dotnet tests\... --real-yolo-smoke`
- `dotnet tests\... --roi-500k-mouse-event-performance`
- `dotnet tests\... --roi-500k-render`
- `dotnet tests\... --roi-500k-full-viewport-render`
- `dotnet tests\... --roi-large-spatial-index`
- `dotnet tests\... --roi-overlap-hit-test`
- `dotnet tests\... --detection-500k-hit-test`
- `dotnet tests\... --detection-500k-render`
- `dotnet tests\... --segmentation-overlay-render`
- `dotnet tests\... --texture-pan-performance`
- `dotnet tests\... --hover-mousemove-performance`
- `dotnet tests\... --roi-drawing-preview-performance`
- `dotnet tests\... --brush-hover-performance`
- `dotnet tests\... --wpf-mask-dirty-bounds`
- `dotnet tests\... --wpf-mask-drag-performance`
- `dotnet tests\... --wpf-learning-workflow-panel`
- `dotnet tests\... --wpf-annotation-purpose-scope`
- `dotnet tests\... --wpf-annotation-purpose-export`
- `dotnet tests\... --dataset-readiness-purpose`
- `powershell .\scripts\smoke-yolo-lifecycle.ps1 -Iterations 3 ...`
- `powershell .\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -ImagePath ... -MinDetections 0` (Kolektor 샘플)
- `powershell .\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3 -MinDetections 1` (기본 학습 모델 경로)

### 측정/결과

- 빌드: 성공 (경고 1건, 오류 0건). 경고는 `OpenVisionLab.Localization.dll` 파일 잠금으로 인한 copy-retry.
- `--roi-500k-delete-performance`: `PASS` (`DELETE_THEN_ZOOM_MS=6.429` / 객체 49.9999만 처리).
- `--wpf-annotation-object-verification`: `PASS` + `DEFERRED_AFTER_DELETE=True`.
- `--wpf-labeling-session-smoke`: 캡처 저장 확인.
- `--real-yolo-smoke`: `PASS`.
- 50만 ROI/overlay 성능군 전부 `PASS` (`mouse move`, `render`, `hit-test`, `brush hover` 등).
- 텍스처 패닝 `PASS` (`VIEWMODEL_MOUSEMOVE_EVENTS=0` 기반 fast-path 유지).
- `--dataset-readiness-purpose`, `--wpf-learning-workflow-panel`, `--wpf-annotation-purpose-*` 모두 `PASS`.
- DetectImage 1회 단일: `detectionCount=0`(현재 임시 학습/데이터셋 특성상 정상 범위로 판단).
- DetectImage 3회 반복: `detectionCount=3`, 모델 재로딩 없이 연결 유지 확인.

### 남은 리스크

- 산업용 공개 데이터셋은 라벨 형식 정규화(현재 일부는 YOLO box가 아닌 마스크/기타 형식) 때문에 학습 결과를 바로 비교하려면 변환 파이프라인이 필요.
- `hover-mousemove-performance`은 수치 자체는 합격이라도 절대 60ms 수준이라, 500K 밀집 장면에서 운영 UI에서 느껴지는 임계치와 UX 기준을 별도 지정 필요.
- Windows 빌드 시 `OpenVisionLab.Localization.dll` 잠금 경고는 CI에서는 재현 여부를 확인 필요(주로 백그라운드 실행중인 프로세스 잔여).

### 다음 프롬프트(권장)

```text
현재 목표를 "라벨링 UX 리팩토링 2차 검토"로 잡고 진행해주세요.

1. `wpf-learning-workflow-panel` 기준 교육형 워크플로우 가이드(객체탐지/세그/비정상탐지) 라우팅이 실제 데이터셋 목적 토글과 맞는지 UX 점검
2. 실데이터(산업용 폴더)로 1개~2개 모델(예: OK/NG) 라벨링 후 저장/재오픈 일관성 재검증
3. YOLO 튜토리얼 워크플로우(학습 시작~추론 저장)에서 사용자 조작 스텝이 자연스러운지 UI 흐름 점검
4. 남은 실패/경고 사항이 재발하면 로그 기반으로 원인 분리
```

## 다음 프롬프트

아래 프롬프트로 이어서 작업하면 된다.

```text
CODEX_RECOVERY.md를 기준으로 이어서 진행해주세요.

목표는 삭제 직후 wheel zoom/pan 멈춤이 완전히 사라졌는지 최종 검증하는 것입니다.

1. `dotnet build .\MvcVisionSystem.sln -c Debug -p:Platform=x64 -p:WpgCustomBuildEnabled=false -m:1 -nr:false`를 실행해 빌드를 확인하세요.
2. `--roi-500k-delete-performance`, `--wpf-annotation-object-verification`, `--wpf-labeling-session-smoke` focused 테스트를 실행하세요.
3. 전체 회귀를 다시 실행하고, 실패하면 삭제/ROI 성능과 관련 있는지 먼저 분리하세요.
4. 실패가 UI 문구 mojibake assertion이면 실제 정상 한국어/구조 검증 기준으로 테스트만 정리하세요.
5. 실제 `artifacts\run\Debug\MvcVisionSystem.exe`를 띄워 창 생성 확인 후 프로세스를 정리하세요.
6. 최종 보고에는 단일 삭제 시간, delete→zoom 시간, WPF 객체 삭제 시간, EXE 실행 여부, 남은 리스크를 포함하세요.
```
