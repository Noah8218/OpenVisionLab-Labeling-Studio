# Labelling Application

.NET 8 WinForms 기반 YOLO 라벨링 애플리케이션입니다. 현재 구조는 이미지 폴더 로드, 클래스 관리, YOLO 데이터셋 폴더 생성, `data.yaml` 생성, 학습 서버 전송 흐름을 중심으로 정리되어 있습니다.

## 주요 구조

- `MvcVisionSystem.csproj`: .NET 8 Windows 데스크톱 앱 프로젝트
- `0. UI`: Dev `FormMainFrame` 기준으로 정리한 WinForms 메인 쉘, 도킹 패널, 데이터셋/Python 상태바 표시, 학습/split 설정 화면
- `1. Core`: 프로젝트 데이터, 설정, 화면/레시피 상태, OpenVisionLab ImageSpace 기반 활성 이미지 컨텍스트, Dev 패턴을 축소 이식한 display host/store, 라벨링/검출/학습 workflow 서비스, Python 통신 lazy 초기화
- `2. Common`: 공용 유틸리티, OpenVisionLab 기반 `AppLog`, OpenVisionLab 메시지박스 어댑터
- `3. Communication\TCP`: YOLO 학습/검출 서버 통신, Python `ResultDefect` 레거시/v1 프로토콜 파싱, TCP 분할 수신 프레이밍, 생성/시작이 분리된 Python TCP listener, Python 연결/수신 상태 스냅샷
- `Yolo`: 클래스 카탈로그 관리, 학습 파라미터, YAML 생성, 학습 전 데이터셋/라벨 내용 검증, 데이터셋 통계/readiness report, deterministic train/valid split, YOLO txt 저장/재로드
- `Library\CViewer*.cs`: Dev `OpenVisionLab.ImageCanvas` 기반 OpenGL 라벨링 뷰어, 이미지 업로드, 렌더링, ROI/측정/검출 overlay 처리
- `Library\Viewer`: YOLO 저장 연동, ROI/좌표/측정/검출 overlay처럼 UI 없이 검증 가능한 뷰어 보조 로직
- `Library\DrawObject`: OpenVisionLab DrawObject 코어를 사용하는 라벨링 전용 ROI 래퍼
- `samples\python_protocol`: Python 모델 프로그램 개발용 프로토콜 예제와 mock YOLO client
- `RJControls`: Dev 공통 WinForms 컨트롤 라이브러리
- `OpenVisionLab.Controls.Init`: Dev 공통 시작 화면 컨트롤
- `0. UI\0) MENU\FormMainFrame.cs`: 라벨링 메인 쉘. 레거시 프레임 명칭은 제거하고 Dev gray-blue 작업대 톤을 기준으로 구성
- `1. Core\FormScreenPlacement.cs`: 라벨링 앱용 시작 화면/메인 화면 배치 보정
- `tests\LabelingApplication.Tests`: UI 실행 없이 검증하는 smoke test

## YOLOv5 연동

실제 Python 모델 프로젝트는 기본값으로 `C:\Git\yolov5`를 사용합니다. 학습 또는 검출 버튼을 누르면 앱이 TCP listener를 먼저 열고, `ProjectSettings.PythonModel` 설정에 따라 `C:\Git\yolov5\labelling_tcp_client.py`를 백그라운드 프로세스로 자동 실행합니다. Python 쪽은 계속 학습/검출/가중치/GPU 런타임을 담당하고, C# 앱은 데이터셋과 라벨, TCP 프로토콜, 프로세스 수명만 관리합니다.

YOLO 설정 화면의 `Python Model` 탭에서 Python 실행 파일, YOLOv5 프로젝트 루트, client script, weight 파일, 이미지 루트, 최소 검출 confidence, 자동 실행 여부를 레시피별로 설정할 수 있습니다.
검출 실행 전에는 weight 파일 존재 여부를 검사합니다. 현재 기본 weight 경로는 `C:\Git\yolov5\best.pt`이며, 기본 이미지 루트는 `C:\Git\py\KtemData`입니다.

Python client는 `TrainingStatus`와 `DetectionStatus` JSON envelope를 C#으로 보낼 수 있습니다. C#은 학습 시작/완료/실패, 진행률, 검출 오류를 구조적으로 파싱해 상태바와 로그에 반영합니다.
검출 결과는 `Main`과 `Detect` 레이어에 OpenGL overlay로 표시되며, 상단 `확정` 버튼 또는 `Ctrl+Enter`로 현재 검출 후보를 `Main` 라벨 ROI로 추가 저장할 수 있습니다. Overlay 색상은 라벨링 클래스 색상을 우선 사용합니다. 확정 시 최소 confidence 미만 후보는 저장하지 않고, `확정` 버튼 활성화도 같은 최소 confidence와 이미지 bounds 기준을 따릅니다. 모델 클래스가 클래스 목록에 없으면 자동으로 추가합니다. 클래스 목록이 바뀌면 상단 클래스 콤보도 갱신되며, 콤보 선택은 Main 뷰어의 현재 라벨 클래스로 적용됩니다. 검출 중 다른 이미지로 이동한 경우 stale 결과는 무시되고, 성공적으로 확정된 후보는 다시 중복 저장되지 않도록 자동으로 비워집니다.
ROI 티칭은 사각형 YOLO 박스와 세그먼테이션 polygon을 함께 지원합니다. 사각형 ROI는 기존처럼 `data\<train|valid>\labels\*.txt` YOLO box 형식으로 저장되고, 세그먼트 드래그는 U-Net류 학습을 위해 `data\<train|valid>\masks\*.png` 클래스 인덱스 마스크와 `data\<train|valid>\segments\*.json` 외곽점 원본을 함께 저장합니다. 즉, 사용자는 외곽 영역을 드래그해서 Defect 학습 영역을 만들고, Python 학습 쪽에서는 필요에 따라 polygon 원본 또는 픽셀 마스크를 사용할 수 있습니다.
이미지 리스트는 `Root` 버튼으로 설정된 이미지 루트를 바로 로드할 수 있고, 각 이미지의 YOLO 라벨 파일 상태를 `Label` 컬럼에 표시합니다. 선택 행의 `Detect` 버튼으로 해당 이미지를 Main에 로드하고 같은 YOLO 검출 workflow를 실행할 수 있습니다. 검출 후보가 생성되면 `Detect` 컬럼에 후보 수를 표시합니다. 확정 저장 후 현재 이미지 행의 라벨 상태를 즉시 다시 계산하고 후보 상태를 비웁니다.
KTEM pretrained 모델 연동 분석은 `docs\KTEM_PRETRAINED_YOLO_INTEGRATION.md`에 정리되어 있습니다.

## 실행 런처

라벨링 앱 기준 통합 실행은 다음 스크립트를 사용합니다.

```powershell
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
.\scripts\start-labeling-workbench.ps1 -AppMode Publish
.\scripts\start-labeling-workbench.ps1 -AppMode Debug -StartYolo
```

기본 설정은 `config\labeling-runtime.example.json`입니다. 개인 PC 경로를 바꿔야 하면 같은 형식으로 `config\labeling-runtime.local.json`을 만들면 됩니다.

## 빌드

```powershell
dotnet build .\MvcVisionSystem.sln -c Debug
```

앱 빌드 산출물은 `artifacts\run\<Configuration>` 아래에 생성됩니다.

## 테스트

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
```

현재 테스트는 YOLO 경로 정규화, 클래스 카탈로그 중복 방지, 데이터셋 폴더/YAML 생성, 학습 전 데이터셋 검증과 통계/readiness/diagnostics report, YOLO 라벨 파일 내용 검증, 이미지별 라벨 상태 계산, train/valid split 저장, 라벨 txt 저장/재로드, 라벨링 workflow 저장 commit, `TrainingParam` 호환 alias, 학습 설정 미러링, UTF-8 TCP 학습 패킷 생성, Python TCP listener 지연 시작과 미연결 전송 실패, Python 설정 검증, Python 메시지 분할 수신 복원, Python `ResultDefect` 레거시/v1 파싱, Python 학습 상태 프로토콜, TCP 수신 큐 UTF-8 디코딩, OpenVisionLab ImageSpace 연동, OpenVisionLab 로그 어댑터, 화면 캡처 저장 경로, 이미지 목록 필터링, OpenGL 좌표 변환, ROI 선택/삭제, OpenGL viewer 수명 관리, Main 이미지 workspace 소유권, 이미지 소스 Mat 소유권, Dev식 display layer catalog API, 라벨링 workflow의 클래스 선택/ROI 목록 반영과 저장 라벨 복원, 검출 결과 레이어 반영과 Main 라벨 확정, 모델 클래스 자동 추가, stale 검출 결과 방어, 중복 확정 방지, 측정 거리 계산, 검출 결과 overlay 변환 기본 동작을 검증합니다.

## 게시

```powershell
.\scripts\publish-win-x64.ps1 -Configuration Release
```

게시 산출물은 `artifacts\publish\Release\win-x64` 아래에 생성됩니다.
게시 완료 후 `publish-manifest.txt`가 생성되어 포함 파일과 크기를 추적합니다. 게시 스크립트는 산출물에 `OpenVisionLab_Dev` 같은 DEV 경로 문자열이 남아 있으면 실패합니다.

YOLO 시작/중지 반복 확인:

```powershell
.\scripts\smoke-yolo-lifecycle.ps1 -Iterations 3
```

## 외부 의존성

기본적으로 다음 소스 루트를 참조합니다.

- `LibraryNoahSourceRoot`: 기본값 `..\Library-Noah`, 현재 `Lib.Common`만 프로젝트 참조로 사용
- `OpenVisionLabSourceRoot`: 기본값 `.\OpenVisionLab`, 라벨링 프로젝트 내부에서 관리하는 OpenVisionLab 라이브러리 복사본

라벨링 프로젝트는 다음 OpenVisionLab 라이브러리를 내부 복사본으로 직접 관리합니다.

- `OpenVisionLab.Logging`
- `OpenVisionLab.Logging.Controls`
- `OpenVisionLab.MessageBox`
- `OpenVisionLab.ImageSpace.Core`
- `OpenVisionLab.ImageCanvas`
- `OpenVisionLab.Controls.Init`
- `OpenVisionLab.DrawObject` 소스 링크
- `RJControls`

기존 DEV 폴더는 더 이상 기본 빌드 참조가 아닙니다. 기존 Cyotek `ImageBox`, Manina `ImageListView`, Emgu/OpenCV UI runtime, WPF PropertyGrid 의존성과 사용되지 않던 팝업 뷰어는 제거되어 메인 표시 경로는 OpenGL 캔버스로, 이미지 목록은 내부 `RJDataGridView` 기반 썸네일 그리드로 통일되어 있습니다.

다른 위치를 사용할 경우 MSBuild 속성으로 지정할 수 있습니다.

```powershell
dotnet build .\MvcVisionSystem.sln -p:LibraryNoahSourceRoot=C:\path\to\Library-Noah -p:OpenVisionLabSourceRoot=C:\path\to\OpenVisionLab
```
