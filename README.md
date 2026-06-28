# Labelling Application

.NET 8 WPF 기반으로 전환 중인 YOLO 라벨링 애플리케이션입니다. 기본 실행은 WPF 셸을 사용하며, 남은 WinForms 의존성은 OpenGL 캔버스 브리지처럼 단계적으로 교체 중인 내부 구현으로 제한합니다.

## 주요 구조

상위 수준 코드 구조와 변경 위치 선택 가이드는 `docs\CODE_STRUCTURE.md`에 정리되어 있습니다. 객체탐지 MVP 완료 기준은 `docs\OBJECT_DETECTION_MVP_COMPLETION.md`, YOLOv5 학습/결과 비교 기준은 `docs\YOLOV5_TRAINING_RESULT_WORKFLOW.md`, 세그멘테이션 UX 완료 기준은 `docs\SEGMENTATION_UX_COMPLETION.md`, 이상탐지 설계 기준은 `docs\ANOMALY_DETECTION_FLOW.md`, 이미 검증되어 보호해야 하는 성능/UX 경로는 `docs\STABLE_VERIFIED_AREAS.md`를 먼저 확인합니다.

- `MvcVisionSystem.csproj`: .NET 8 Windows 데스크톱 앱 프로젝트
- `0. UI`: WPF 라벨링 셸과 WPF 전용 화면
- `1. Core`: 프로젝트 데이터, 설정, 화면/레시피 상태, OpenVisionLab ImageSpace 기반 활성 이미지 컨텍스트, Dev 패턴을 축소 이식한 display host/store, 라벨링/검출/학습 workflow 서비스, Python 통신 lazy 초기화
- `2. Common`: 공용 유틸리티, OpenVisionLab 기반 `AppLog`, WPF 메시지 박스 어댑터
- `3. Communication\TCP`: YOLO 학습/검출 서버 통신, Python `ResultDefect` 레거시/v1 프로토콜 파싱, TCP 분할 수신 프레이밍, 생성/시작이 분리된 Python TCP listener, Python 연결/수신 상태 스냅샷
- `Yolo`: 클래스 카탈로그 관리, 학습 파라미터, YAML 생성, 학습 전 데이터셋/라벨 내용 검증, 데이터셋 통계/readiness report, deterministic train/valid/test split, YOLO txt 저장/재로드
- `Library\CViewer*.cs`: Dev `OpenVisionLab.ImageCanvas` 기반 OpenGL 라벨링 뷰어, 이미지 업로드, 렌더링, ROI/측정/검출 overlay 처리
- `Library\Viewer`: YOLO 저장 연동, ROI/좌표/측정/검출 overlay처럼 UI 없이 검증 가능한 뷰어 보조 로직
- `Library\DrawObject`: OpenVisionLab DrawObject 코어를 사용하는 라벨링 전용 ROI 래퍼
- `samples\python_protocol`: Python 모델 프로그램 개발용 프로토콜 예제와 mock YOLO client
- `WPF-UI`: WPF 셸의 `FluentWindow`, TitleBar, Fluent 테마와 WPF 전용 컨트롤 기준 라이브러리
- `MahApps.Metro.IconPacks.Material`: WPF 버튼에 쓰는 Material 스타일 아이콘 전용 패키지
- `0. UI\9) WPF\WpfLabelingShellWindow.xaml`: 기본 실행되는 WPF 라벨링 셸
- `tests\LabelingApplication.Tests`: UI 실행 없이 검증하는 smoke test

## YOLOv5 연동

실제 Python 모델 프로젝트는 기본값으로 `C:\Git\yolov5`를 사용합니다. 학습 또는 검출 버튼을 누르면 앱이 TCP listener를 먼저 열고, `ProjectSettings.PythonModel` 설정에 따라 `C:\Git\yolov5\labelling_tcp_client.py`를 백그라운드 프로세스로 자동 실행합니다. Python 쪽은 계속 학습/검출/가중치/GPU 런타임을 담당하고, C# 앱은 데이터셋과 라벨, TCP 프로토콜, 프로세스 수명만 관리합니다.

YOLO 설정 화면에서 Python 실행 파일, YOLOv5 프로젝트 루트, client script, weight 파일, 이미지 루트, 최소 검출 신뢰도, 최대 표시 후보 수, 검출 요청 시 Python worker 자동 시작 여부를 레시피별로 설정할 수 있습니다.
검출 실행 전에는 weight 파일 존재 여부를 검사합니다. 현재 기본 weight 경로는 `C:\Git\yolov5\best.pt`이며, 기본 이미지 루트는 `C:\Git\yolov5\data\train\images`입니다.

Python client는 `TrainingStatus`와 `DetectionStatus` JSON envelope를 C#으로 보낼 수 있습니다. C#은 학습 시작/완료/실패, 진행률, 검출 오류를 구조적으로 파싱해 상태바와 로그에 반영합니다.
검출 결과는 `Main`과 `Detect` 레이어에 OpenGL overlay로 표시되며, 오른쪽 `후보` 패널의 `확정`/`전체 확정` 버튼 또는 후보 목록 단축키(`Enter`, `Ctrl+A`)로 현재 검출 후보를 `Main` 라벨 ROI로 추가 저장할 수 있습니다. Overlay 색상은 라벨링 클래스 색상을 우선 사용합니다. 확정 시 최소 신뢰도 미만 후보는 저장하지 않고, 확정 버튼 활성화도 같은 최소 신뢰도와 이미지 좌표 기준을 따릅니다. 후보가 너무 많이 들어오면 설정된 최대 후보 수만큼 confidence 상위 후보만 리뷰 대상으로 유지합니다. 모델 클래스가 클래스 목록에 없으면 자동으로 추가합니다. 클래스 목록이 바뀌면 클래스 콤보도 갱신되며, 콤보 선택은 Main 뷰어의 현재 라벨 클래스로 적용됩니다. 검출 중 다른 이미지로 이동한 경우 stale 결과는 무시되고, 성공적으로 확정된 후보는 다시 중복 저장되지 않도록 자동으로 비워집니다.
ROI 티칭은 사각형 YOLO 박스와 세그먼테이션 polygon을 함께 지원합니다. 사각형 ROI는 `data\<train|valid|test>\labels\*.txt` YOLO box 형식으로 저장되고, 세그먼트 드래그는 U-Net류 학습을 위해 `data\<train|valid|test>\masks\*.png` 클래스 인덱스 마스크와 `data\<train|valid|test>\segments\*.json` 외곽점 원본을 함께 저장합니다. 즉, 사용자는 외곽 영역을 드래그해서 Defect 학습 영역을 만들고, Python 학습 쪽에서는 필요에 따라 polygon 원본 또는 픽셀 마스크를 사용할 수 있습니다.
이미지 리스트는 `Root` 버튼으로 설정된 이미지 루트를 바로 로드할 수 있고, 각 이미지의 YOLO 라벨 파일 상태를 `Label` 컬럼에 표시합니다. 기본 WPF 셸의 왼쪽 이미지 큐는 WPF `DataGrid`로 바뀌었고, `Root`, `Folder`, `Refresh`, `Next`, 상태 필터, 파일명 검색을 바로 사용할 수 있습니다. 자주 보는 상태는 `전체`, `후보 1`, `실패 0`, `확정 0`처럼 개수가 붙은 빠른 필터 버튼으로 바로 바꿉니다. 하단 상태바에는 후보/실패/확정/스킵/검출없음 개수가 함께 표시됩니다. 이미지 행을 한 번 클릭하면 그 이미지가 바로 Main 캔버스에 열리고, 선택 행은 왼쪽 강조선과 선택 배경으로 표시됩니다. `선택 검사`는 선택 이미지 1장, `일괄 검사`는 현재 필터에 보이는 이미지, `재시도`는 실패 이미지들을 순서대로 다시 검사합니다. 배치 중에는 진행률과 `중지`로 중지 상태를 볼 수 있고, 검출/확정/스킵/저장 뒤에는 현재 이미지 행의 라벨과 AI 상태를 다시 계산합니다.
KTEM pretrained 모델 연동 분석은 `docs\KTEM_PRETRAINED_YOLO_INTEGRATION.md`에 정리되어 있습니다.

## 처음 실행

두 저장소를 같은 부모 폴더에 둡니다.

```text
C:\Git\Labelling_Application
C:\Git\yolov5
```

앱을 빌드하고 실행합니다.

```powershell
dotnet build .\MvcVisionSystem.sln -c Debug -p:Platform=x64
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
```

앱이 열리면 오른쪽 `YOLO` 탭에서 `첫 점검`을 누릅니다. Python 실행 파일, YOLO 프로젝트, client script, `best.pt`, 샘플 이미지, requirements 패키지, worker 상태가 한 번에 확인됩니다.
패키지가 빠져 있으면 같은 `YOLO` 탭의 `설치`를 누르고, 그 다음 `테스트`를 누르거나 상단 `추론 검토`를 켠 뒤 `현재 검사` 또는 이미지 큐의 `선택 검사`를 눌러 실제 `C:\Git\yolov5\data\train\images` 샘플 이미지로 검출까지 확인합니다. Python worker를 다시 붙여야 할 때는 `재시작`, 정리해야 할 때는 `중지`를 사용합니다.
모델 경로, 신뢰도, 시간 제한은 `YOLO` 탭의 `모델 설정`에서 바로 수정하고 `저장`을 누릅니다. 학습 이미지 크기, 배치, 에폭, cfg, weight, 검증 split, 테스트 split은 `학습`에서 수정하며, `점검`, `시작`, `중지`로 학습 준비 상태와 worker 명령을 확인합니다.

산업용 공개 데이터셋(오프라인 테스트용)을 먼저 가져오려면 `scripts\prepare-industrial-dataset.ps1`를 사용하세요.
사용 예시와 지원 소스(KolektorSDD/VisA/Severstal/Manual), YAML/폴더 출력 규칙은 `docs\INDUSTRIAL_DATASET_PREPARATION.md`를 참고합니다.

학습 전 `점검` 결과에는 train/valid/test 개수, `Validation %`와 `Test %` 용도, test split 비어 있음, 클래스별 라벨 수 부족, 클래스 불균형 경고가 함께 나옵니다. 특히 새 모델을 믿고 바꾸려면 OK뿐 아니라 NG 같은 결함 클래스도 실제 샘플로 라벨링하고, test split을 따로 확보한 뒤 비교해야 합니다.

현재 기본 WPF 셸은 실행 시 설정된 YOLO 샘플 이미지를 찾아 중앙 캔버스와 WPF 이미지 큐에 올립니다. 시작만으로 검출은 실행하지 않습니다. 상단 `테마` 버튼으로 다크/라이트 화면을 바꿀 수 있고, `샘플`, `ROI`, `저장`, `라벨링`, `추론 검토`, `YOLO`, `현재 검사` 버튼으로 WPF 전환 중인 기본 흐름을 바로 확인할 수 있습니다. 검출 후보는 캔버스와 오른쪽 `후보` 목록에 표시되고, 후보를 선택하면 클래스, 신뢰도, 좌표, 현재 라벨과의 겹침을 바로 볼 수 있습니다. 후보 `확정`, `전체 확정`, `스킵` 처리는 오른쪽 `후보` 패널 안에서 합니다. 확정한 후보와 수동 ROI는 기존 YOLO 라벨 저장 서비스로 저장됩니다.

처음 받은 PC에서 전체 상태를 한 번에 확인하려면 아래 명령을 먼저 돌립니다.

```powershell
.\scripts\verify-first-run.ps1
```

이 명령은 PowerShell 스크립트 문법, runtime config의 YOLO 경로/weights/샘플 이미지, 앱 빌드, 테스트, YOLO smoke 추론을 순서대로 확인합니다.
WPF 창이 실제로 뜨는지까지 확인하려면 사람이 보는 PC에서 아래처럼 실행합니다.

```powershell
.\scripts\verify-first-run.ps1 -RunWpfSmoke
```

Release publish 산출물까지 열어 보려면 publish 후 아래처럼 실행합니다.

```powershell
.\scripts\verify-first-run.ps1 -SkipBuild -SkipTests -SkipYoloSmoke -RunPublishWpfSmoke
```

WPF 화면을 사람이 직접 볼 때는 `docs\WPF_MANUAL_SMOKE_CHECKLIST.md` 순서대로 확인합니다.

## 튜토리얼

처음 쓰는 사용자는 앱 오른쪽 `가이드` 탭의 `처음 10분 튜토리얼`을 먼저 보면 됩니다. 같은 흐름을 화면 캡처와 함께 보는 HTML 문서는 `docs\tutorial\labeling-workbench-tutorial.html`입니다.

HTML 튜토리얼은 이미지 열기, 클래스 등록, 박스 라벨링, 저장/점검, YOLO 설정, 추론 후보 검토 순서로 구성되어 있습니다.

## 실행 런처

라벨링 앱 기준 통합 실행은 다음 스크립트를 사용합니다.

```powershell
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
.\scripts\start-labeling-workbench.ps1 -AppMode Publish
.\scripts\start-labeling-workbench.ps1 -AppMode Debug -StartYolo
.\scripts\start-labeling-workbench.ps1 -AppMode Debug -CheckYolo
```

기본 설정은 `config\labeling-runtime.example.json`입니다. 이 파일은 `${repoParent}\yolov5` 기준이라 `Labelling_Application`과 `yolov5`를 같은 부모 폴더에 두면 그대로 동작합니다. 개인 PC 경로를 바꿔야 하면 같은 형식으로 `config\labeling-runtime.local.json`을 만들면 됩니다.

## 설정 정책

커밋되는 기본 설정은 형제 폴더의 샘플 YOLO 저장소 기준입니다. 예를 들어 두 저장소를 `C:\Git` 아래에 같이 두면 아래처럼 해석됩니다.

```text
C:\Git\yolov5
C:\Git\yolov5\best.pt
C:\Git\yolov5\data\train\images
```

개인 PC에서 경로가 다르면 `config\labeling-runtime.local.json`을 만들어 바꿉니다. 이 파일은 개인 설정이라 Git에 올리지 않습니다.
runtime json 경로에는 `${repoRoot}`와 `${repoParent}`를 쓸 수 있습니다.
앱 설정창에서 바꾼 Python Model 값은 현재 라벨링 프로젝트/레시피 설정에 저장되고, 런처 스크립트는 `config`의 runtime json을 사용합니다.

## 빌드

```powershell
dotnet build .\MvcVisionSystem.sln -c Debug
```

앱 빌드 산출물은 `artifacts\run\<Configuration>` 아래에 생성됩니다.

## 테스트

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
```

현재 테스트는 YOLO 경로 정규화, 클래스 카탈로그 중복 방지, 데이터셋 폴더/YAML 생성, 학습 전 데이터셋 검증과 통계/readiness/diagnostics report, YOLO 라벨 파일 내용 검증, 이미지별 라벨 상태 계산, train/valid split 저장, 라벨 txt 저장/재로드, 라벨링 workflow 저장 commit, `TrainingParam` 호환 alias, 학습 설정 미러링, UTF-8 TCP 학습 패킷 생성, Python TCP listener 지연 시작과 미연결 전송 실패, Python 설정 검증, Python worker smoke 점검과 후보 좌표 변환, Python 메시지 분할 수신 복원, Python `ResultDefect` 레거시/v1 파싱, Python 학습 상태 프로토콜, TCP 수신 큐 UTF-8 디코딩, OpenVisionLab ImageSpace 연동, OpenVisionLab 로그 어댑터, 화면 캡처 저장 경로, 이미지 목록 필터링, WPF 셸 생성, WPF 이미지 큐 로딩, OpenGL 좌표 변환, ROI 선택/삭제, OpenGL viewer 수명 관리, Main 이미지 workspace 소유권, 이미지 소스 Mat 소유권, Dev식 display layer catalog API, 라벨링 workflow의 클래스 선택/ROI 목록 반영과 저장 라벨 복원, 검출 결과 레이어 반영과 Main 라벨 확정, 모델 클래스 자동 추가, stale 검출 결과 방어, 중복 확정 방지, 측정 거리 계산, 검출 결과 overlay 변환 기본 동작을 검증합니다.

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

Python TCP 추론 확인:

```powershell
.\scripts\smoke-yolo-tcp.ps1
.\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -OutputDirectory artifacts\python-smoke-detect-image
.\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3 -OutputDirectory artifacts\python-smoke-detect-image-repeat
```

첫 실행 전체 확인:

```powershell
.\scripts\verify-first-run.ps1
```

## 외부 의존성

기본적으로 다음 소스 루트를 참조합니다.

- `LibraryNoahSourceRoot`: 기본값 `..\Library-Noah`, 현재 `Lib.Common`만 프로젝트 참조로 사용
- `OpenVisionLabSourceRoot`: 기본값 `.\OpenVisionLab`, 라벨링 프로젝트 내부에서 관리하는 OpenVisionLab 라이브러리 복사본

라벨링 프로젝트는 다음 OpenVisionLab 라이브러리를 내부 복사본으로 직접 관리합니다.

- `OpenVisionLab.Logging`
- `OpenVisionLab.Logging.Controls`
- `OpenVisionLab.ImageSpace.Core`
- `OpenVisionLab.ImageCanvas`
- `OpenVisionLab.DrawObject` 소스 링크

WPF 아이콘은 NuGet `MahApps.Metro.IconPacks.Material`을 사용합니다. 전체 Material 테마를 적용하지 않고, 버튼/도구 아이콘만 얹는 방식입니다.

기존 DEV 폴더는 더 이상 기본 빌드 참조가 아닙니다. 기존 Cyotek `ImageBox`, Manina `ImageListView`, Emgu/OpenCV UI runtime, WPF PropertyGrid 의존성과 사용되지 않던 팝업 뷰어는 제거되어 메인 표시 경로는 OpenGL 캔버스로 정리되었습니다. 기본 실행 화면의 이미지 목록은 WPF `DataGrid`이며, 남은 WinForms 의존성은 OpenGL 캔버스 호환 브리지 안으로 제한합니다.

다른 위치를 사용할 경우 MSBuild 속성으로 지정할 수 있습니다.

```powershell
dotnet build .\MvcVisionSystem.sln -p:LibraryNoahSourceRoot=C:\path\to\Library-Noah -p:OpenVisionLabSourceRoot=C:\path\to\OpenVisionLab
```
