# CODEX_RECOVERY.md

작성일: 2026-06-29 (Asia/Seoul)

이 문서는 긴 Codex 대화에서 이어서 작업하기 위한 복구/인수인계 문서다. 다음 Codex 대화에서는 이 파일을 먼저 읽고 현재 상태를 파악한 뒤 진행하면 된다.

## 1. 현재 목표

라벨링 프로그램을 초보자도 이해하기 쉽고, 실제 산업 이미지 객체탐지/세그멘테이션/이상탐지 학습까지 연결 가능한 툴로 만든다.

핵심 방향:

- 라벨링은 빠르고 정확해야 한다.
- Viewer는 ROI/브러시/지우개/텍스처 이동/삭제/줌에서 멈칫거림이 없어야 한다.
- UI는 사용자가 현재 단계와 다음 작업을 바로 이해해야 한다.
- MVVM을 지향한다. View에는 XAML과 피할 수 없는 UI 어댑터만 남기고, Command/상태/워크플로우는 ViewModel/Service로 이동한다.
- 검증 완료된 성능 경로는 문서화하고 불필요하게 다시 건드리지 않는다.

## 2. 현재 작업트리 주의사항

작업트리는 많은 파일이 수정된 상태다. 다음 대화에서는 반드시 `git status --short`를 먼저 확인한다.

중요:

- 사용자가 직접 만진 파일이 섞여 있을 수 있으므로 임의로 revert하지 않는다.
- `OpenVisionLab.ImageCanvas` 성능 관련 변경은 이미 많은 패스에서 검증된 경로다. 성능 이슈 재현 없이 임의 수정하지 않는다.
- XAML 파일은 한글 인코딩 문제가 있었으므로, 새 한글 문구를 넣을 때 가능하면 기존 패턴처럼 XML numeric entity 또는 UTF-8 보존을 확인한다.
- `Get-Content` 출력에서 일부 한글이 깨져 보여도 실제 파일/빌드가 정상일 수 있다.

## 3. 주요 완료 영역

### 3.1 Viewer / ROI / 삭제 / 줌 / 브러시 성능

완료 또는 안정화된 영역:

- ROI 대량 성능 테스트: 50만 ROI 생성/이동/삭제/히트테스트/렌더링 focused 테스트를 반복 검증.
- 삭제 후 wheel zoom/pan 멈춤 개선:
  - 단일 오브젝트 삭제 후 즉시 전체 repaint가 입력을 막지 않도록 deferred refresh 경로 적용.
  - `DeleteOverlay(..., refreshImmediately: false)`와 `QueueRefreshGLAfterInput()` 계열 경로 사용.
- 브러시/지우개 MouseUp 멈칫거림 개선:
  - 이전 `C:\Git\Controls.Viewer2D`의 Toolkit2DViewerViewModel / DiagramControlViewer 방식을 참고.
  - CPU bitmap/mat 직접 갱신보다 OpenGL/FBO/overlay 경로 중심으로 개선.
- 텍스처 mouse move / pan 경로:
  - ViewModel MouseMove 이벤트를 줄이고 fast-path 유지.

관련 문서:

- `docs/STABLE_VERIFIED_AREAS.md`
- `docs/WORK_TRACKING.md`
- `docs/CODE_STRUCTURE.md`

주의:

- 이 영역은 사용자가 "속도는 이제 된 것 같다"고 판단한 뒤 UX 디테일로 넘어간 상태다.
- 재현 없는 성능 경로 수정은 피한다.

### 3.2 MVVM 구조 전환

완료/진행 상태:

- `OpenVisionLab.Mvvm` 기반 Observable/RelayCommand/Behavior 인프라가 추가되어 여러 View/ViewModel에서 사용 중이다.
- `WpfLabelingShellWindow`는 여러 partial로 쪼개졌지만, 아직 code-behind가 완전히 제거된 상태는 아니다.
- 주요 Panel ViewModel이 존재한다:
  - `WpfLabelingShellViewModel`
  - `WpfImageQueuePanelViewModel`
  - `WpfCanvasPanelViewModel`
  - `WpfObjectReviewPanelViewModel`
  - `WpfCandidateReviewPanelViewModel`
  - `WpfClassCatalogPanelViewModel`
  - `WpfLearningWorkflowPanelViewModel`
  - `WpfTrainingSettingsPanelViewModel`
  - `WpfTemplateMatchingAutoLabelViewModel`
- View 안에 ViewModel을 직접 new하지 않는 방향으로 진행했다.
  - `WpfLabelingShellViewModels`가 composition root 역할.

주의:

- ViewModel도 너무 커지면 안 된다. `WpfLearningWorkflowPanelViewModel`은 이미 큰 편이므로 추가 기능을 무조건 여기에 넣지 말 것.
- View code-behind를 줄이되, 단순히 ViewModel로 복사하면 `God ViewModel`이 된다. 서비스/Coordinator/ViewModel 역할을 나눠야 한다.

### 3.3 Dataset / Class / YOLO UX

진행된 UX 방향:

- 데이터셋 생성/선택/변경 흐름을 사용자가 이해하기 쉽게 개선하는 방향으로 작업.
- 현재 열려 있는 데이터셋명, 저장 경로, 이미지 폴더 경로, 폴더 열기 버튼이 메인에서 보이도록 개선.
- 데이터셋 선택 시 최신순 정렬, 마지막 데이터셋 복원, 이미지 경로/클래스명/저장 경로가 데이터셋 단위로 따라와야 한다는 요구가 있었다.
- 클래스 관리:
  - Defect 고정 전제를 제거해야 한다는 사용자 피드백이 있었다.
  - 클래스 추가/변경/삭제/색상 적용 흐름이 들어갔다.
  - OK/NG/Defect/이물 등 사용자 정의 클래스가 자연스럽게 가능해야 한다.
- YOLO 설명:
  - YAML, 이미지 1장과 txt 1개 대응 구조를 UI에서 이해시키자는 요구가 있었다.
  - 기술 용어는 필요한 곳만 쓰고, 일반 라벨/설명에서는 OpenGL 같은 내부 기술명은 제거하는 방향.

YOLO 방향:

- 현재는 YOLOv5 기반 검증을 우선한다.
- YOLOv8 연동은 가능하지만 나중에 진행하기로 했다.
- Python vs ONNX 논의 결과:
  - 학습/튜닝/검증은 Python 중심이 현실적.
  - 배포/추론 안정화 단계에서 ONNX 고려.

### 3.4 Auto Labeling / Template Matching

최근 작업의 핵심이다.

사용자 요청:

- 오토 라벨링 도입.
- 템플릿 매칭 사용.
- 최신 `C:\Git\Library-Noah`의 `Lib.OpenCV` 참고.
- MVVM 기준을 지키며 View code-behind에 업무 로직을 쌓지 말 것.

구현 상태:

- `MvcVisionSystem.csproj`
  - `$(LibraryNoahSourceRoot)\Lib.OpenCV\Lib.OpenCV.csproj` ProjectReference 추가.
  - 조건부 참조이므로 해당 경로가 있을 때만 사용.
- `1. Core/TemplateMatchingAutoLabelService.cs`
  - `Lib.OpenCV.Tool.MatchingTool` 기반 템플릿 매칭 서비스.
  - obsolete `CVMatching` 대신 최신 `MatchingTool` 사용.
  - 현재 이미지에서 선택 ROI를 템플릿으로 후보 탐색.
  - 외부 template bitmap으로 임의 이미지 매칭 가능.
  - 후보 dedupe, source ROI 제외, score normalize 포함.
- `1. Core/TemplateMatchingBatchAutoLabelService.cs`
  - 큐 이미지 대상으로 템플릿 매칭 후 YOLO 라벨 저장.
  - 기존 라벨 파일이 있는 이미지는 덮어쓰지 않음.
  - 이미지 원본 확장자 보존을 위해 `YoloAnnotationService.SaveAnnotations(..., sourceImagePath)` 사용.
- `Yolo/YoloAnnotationService.cs`
  - `SaveAnnotations`에 `sourceImagePath` optional parameter 추가.
  - 배치 저장 시 현재 active image가 아닌 대상 이미지의 확장자를 유지.
- `0. UI/9) WPF/ViewModels/WpfTemplateMatchingAutoLabelViewModel.cs`
  - 현재 이미지 템플릿 후보 찾기 Command.
  - 큐 템플릿 배치 Command.
  - 배치 루프, 진행 상태 집계, 로그 흐름 담당.
  - `IWpfTemplateMatchingAutoLabelHost`를 통해 View 접근부와 분리.
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.TemplateMatchingCommands.cs`
  - 기존에는 업무 로직이 많았으나 현재는 host adapter 역할.
  - 캔버스/큐/상태바/로그 등 View 접근이 필요한 부분만 남김.
- UI:
  - 상단 `템플릿` 버튼: 현재 이미지에서 선택 박스 기준으로 후보 찾기.
  - 이미지 큐의 `템플릿 배치` 버튼: 라벨 없는 큐 이미지에 같은 모양 라벨 저장.

주의:

- `템플릿 배치`는 후보 표시만 하는 기능이 아니라 라벨 파일을 실제 저장하는 기능이다.
- 안전을 위해 기존 라벨 파일이 있는 이미지는 건너뛴다.
- 아직 실제 EXE에서 여러 이미지 대상으로 템플릿 배치 수동 테스트가 필요하다.
- 템플릿 매칭 임계값은 현재 배치에서 `MinimumScore = 0.9`, 현재 이미지 후보에서 `0.82` 사용.

## 4. 최근 검증 결과

최근 최종 확인:

```powershell
dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false
```

결과:

- 성공
- 경고 0개
- 오류 0개

추가 확인:

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --mvvm-infra
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --yolo-annotation-storage
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --roi-only --width 1280 --height 820
```

결과:

- `PASS MVVM infrastructure observable and command helpers`
- `PASS YOLO annotation save preserves source image extension and split ownership`
- WPF visual smoke screenshot 생성:
  - `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`

이전 중요 검증:

- 50만 ROI 삭제/렌더/마우스 이벤트 focused 테스트 통과.
- 텍스처 pan, hover mousemove, brush hover, segmentation overlay focused 테스트 통과.
- 실제 EXE 실행 스모크를 과거 패스에서 확인.

## 5. 현재 주요 파일

Auto labeling / template matching:

- `MvcVisionSystem.csproj`
- `1. Core/TemplateMatchingAutoLabelService.cs`
- `1. Core/TemplateMatchingBatchAutoLabelService.cs`
- `Yolo/YoloAnnotationService.cs`
- `0. UI/9) WPF/ViewModels/WpfTemplateMatchingAutoLabelViewModel.cs`
- `0. UI/9) WPF/ViewModels/WpfLabelingShellViewModels.cs`
- `0. UI/9) WPF/ViewModels/WpfLabelingShellViewModel.cs`
- `0. UI/9) WPF/ViewModels/WpfImageQueuePanelViewModel.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.TemplateMatchingCommands.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.ImageQueue.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelAccessors.cs`
- `0. UI/9) WPF/Views/WpfImageQueuePanel.xaml`
- `0. UI/9) WPF/Views/WpfImageQueuePanel.xaml.cs`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml`
- `0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml.cs`

MVVM / command / behavior:

- `OpenVisionLab/Library/OpenVisionLab.Mvvm/RelayCommand.cs`
- `OpenVisionLab/Library/OpenVisionLab.Mvvm/Behaviors/InputCommandBehaviors.cs`

Viewer/OpenGL 성능:

- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/Engine/ImageCanvasControl.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/ViewModel/RoiImageCanvasViewModel.cs`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/OpenGL/*`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/Overlays/*`
- `OpenVisionLab/Library/OpenVisionLab.ImageCanvas/RoiInteraction/*`

문서:

- `docs/STABLE_VERIFIED_AREAS.md`
- `docs/WORK_TRACKING.md`
- `docs/CODE_STRUCTURE.md`
- `docs/LABELING_PROGRAM_DIRECTION.md`
- `docs/OBJECT_DETECTION_MVP_COMPLETION.md`

## 6. 남은 작업 / 다음 우선순위

우선순위 1: 실제 EXE에서 템플릿 매칭 자동 라벨링 검증

- 데이터셋 열기.
- OK 또는 NG 박스 1개 라벨링.
- 상단 `템플릿` 버튼으로 현재 이미지 후보 확인.
- 이미지 큐의 `템플릿 배치` 버튼 실행.
- 라벨 없는 이미지에 `.txt`가 생성되는지 확인.
- 기존 라벨 있는 이미지는 덮어쓰지 않는지 확인.
- 저장 후 재오픈 시 클래스/박스/색상이 유지되는지 확인.

우선순위 2: 템플릿 매칭 테스트 추가

- synthetic image 2~3장을 만들어 동일 패턴 매칭/저장 검증.
- 기존 라벨 파일이 있는 이미지는 skip되는지 테스트.
- source image extension 보존 테스트는 이미 있음. 템플릿 배치 저장에도 별도 테스트 추가 가능.

우선순위 3: MVVM 리팩토링 계속

다음 후보:

- `WpfLabelingShellWindow.DatasetSetupCommands.cs`
- `WpfLabelingShellWindow.ImageQueueCommands.cs`
- `WpfLabelingShellWindow.AnnotationMaskStrokeCommit.cs`
- `WpfLabelingShellWindow.TrainingGuideStatus.cs`

기준:

- 단순히 ViewModel로 복사하지 말 것.
- 파일 IO, OpenCV, YOLO 저장, 데이터셋 manifest, training readiness는 Service/Core로 이동.
- ViewModel은 command, 상태, 사용자 의도, 진행 상태만 담당.
- View는 XAML과 unavoidable adapter만 담당.

우선순위 4: UX 검토

- 초보자가 데이터셋 생성 -> 이미지 불러오기 -> 클래스 등록 -> 라벨 저장 -> YOLO 학습까지 단계적으로 이해하는지 확인.
- 기술 용어(OpenGL 등)는 사용자-facing label에서 제거.
- 현재 데이터셋/이미지 폴더/저장 위치가 항상 보이는지 확인.
- "다음", "저장 필요", "라벨 저장됨" 같은 표현이 자연스러운지 점검.

## 7. 다음 대화에서 바로 붙여 넣을 프롬프트

아래 프롬프트를 새 Codex 대화에 그대로 붙여 넣으면 된다.

```text
C:\Git\Labelling_Application 작업을 이어서 진행해주세요.

먼저 CODEX_RECOVERY.md를 읽고 현재 상태를 파악해주세요. 이 대화는 매우 긴 이전 작업의 후속입니다.

중요 원칙:
- git status --short를 먼저 확인하고, 사용자가 만든 변경 또는 이전 Codex 변경을 임의로 revert하지 마세요.
- 우리는 MVVM을 지향합니다. View code-behind에는 XAML로 불가능한 UI adapter만 남기고, Command/상태/워크플로우는 ViewModel/Service로 분리합니다.
- Viewer/OpenGL/ROI/브러시/지우개 성능 경로는 이미 여러 focused 테스트로 검증되었습니다. 재현 없이 임의로 수정하지 마세요.
- 새 사용자-facing 문구에는 OpenGL 같은 내부 기술 용어를 넣지 마세요.
- XAML 한글 인코딩에 주의하세요. 필요하면 기존처럼 XML numeric entity를 사용하세요.

현재 최신 작업:
- Lib.Noah 최신 소스의 Lib.OpenCV MatchingTool을 참조해 템플릿 매칭 기반 오토 라벨링을 도입했습니다.
- 현재 이미지 후보 찾기: 상단 템플릿 버튼.
- 큐 일괄 자동 라벨링: 이미지 큐의 템플릿 배치 버튼.
- 기존 라벨 파일이 있는 이미지는 덮어쓰지 않습니다.
- 템플릿 매칭 업무 로직은 Core service와 WpfTemplateMatchingAutoLabelViewModel로 분리했고, WpfLabelingShellWindow.TemplateMatchingCommands.cs는 host adapter 역할만 남겼습니다.

먼저 할 일:
1. `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`로 현재 빌드를 확인하세요.
2. `--mvvm-infra`, `--yolo-annotation-storage`, `--wpf-visual-smoke --roi-only --width 1280 --height 820`를 실행해 최근 변경 회귀를 확인하세요.
3. 가능하면 실제 `artifacts\run\Debug\MvcVisionSystem.exe`를 실행해서 템플릿 버튼과 이미지 큐의 템플릿 배치 버튼이 보이는지 확인하세요.
4. 실제 데이터셋에서 박스 하나를 그리고 템플릿 배치를 실행해, 라벨 없는 이미지에 YOLO txt가 생성되고 기존 라벨은 덮어쓰지 않는지 확인하세요.
5. 문제가 있으면 원인을 분석한 뒤 작은 단위로 수정하고, 수정 후 다시 focused 검증을 실행하세요.

다음 개발 우선순위:
1. 템플릿 배치 자동 라벨링 EXE 수동 검증
2. 템플릿 배치 저장/skip 동작 focused 테스트 추가
3. WpfLabelingShellWindow의 남은 큰 code-behind 중 DatasetSetupCommands/ImageQueueCommands/AnnotationMaskStrokeCommit 순서로 MVVM/Service 분리
4. 검증 완료 항목은 docs/STABLE_VERIFIED_AREAS.md 또는 docs/WORK_TRACKING.md에 기록

최종 보고에는 변경 파일, 검증 명령/결과, EXE 수동 검증 여부, 남은 리스크를 간단히 정리해주세요.
```

## 8. 빠른 상태 점검 명령

```powershell
git status --short
dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --mvvm-infra
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --yolo-annotation-storage
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --roi-only --width 1280 --height 820
```

## 9. 완료 판단 기준

템플릿 자동 라벨링은 다음 조건을 만족해야 완료로 볼 수 있다.

- 현재 이미지에서 선택 박스 기준 후보가 표시된다.
- 큐 템플릿 배치가 라벨 없는 이미지만 처리한다.
- 기존 라벨 txt를 덮어쓰지 않는다.
- 생성된 YOLO txt가 재오픈 시 정상 박스로 로드된다.
- 클래스명/색상이 선택 템플릿의 클래스와 일치한다.
- UI가 멈추지 않고 배치 진행 상태가 표시된다.
- EXE 수동 검증과 focused 테스트가 모두 통과한다.
