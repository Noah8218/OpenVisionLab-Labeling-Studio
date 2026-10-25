# OpenVisionLab Labeling Studio

한 PC에서 라벨링부터 학습·모델 비교·추론까지 수행하는 Windows 데스크톱 도구입니다.

[![버전 0.3.7](https://img.shields.io/badge/version-0.3.7-blue)](Directory.Build.props)
![Windows 10/11 x64](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue)
[![MIT 라이선스](https://img.shields.io/badge/license-MIT-green)](LICENSE)

[English](README.en.md) | [한국어](README.md)

![Smart Mask 시작 박스·후보 검토·확정·저장 후 다음 이미지 이동](docs/tutorial/images/readme/ko/hero-smart-mask.gif)

시작 박스 → MobileSAM 후보 검토 → 마스크 적용 → 저장하고 다음 이미지로 이동합니다. [MP4 보기](docs/tutorial/images/readme/ko/hero-smart-mask.mp4). (대기 구간 생략)

## 지원 작업

| 객체 탐지 | 세그멘테이션 | 이상 탐지 |
| --- | --- | --- |
| [![와셔 박스 라벨](docs/tutorial/images/readme/ko/detection-washer.png)](#객체-탐지) | [![기포 마스크 라벨](docs/tutorial/images/readme/ko/segmentation-bubble.png)](#세그멘테이션과-smart-mask) | [![육각형 OK/NG 판정](docs/tutorial/images/readme/ko/anomaly-grid-disk.png)](#이상-탐지) |
| **정답:** 축 정렬 박스 | **정답:** 폴리곤과 픽셀 마스크 | **정답:** 검수한 이미지 단위 OK/NG 판정 |
| **모델:** YOLOv5, YOLOv8, YOLO11 | **모델:** YOLOv8-seg, YOLO11-seg, U-Net | **모델:** YOLOv8/YOLO11 분류, PatchCore pilot |

## 작업 흐름

**데이터셋 → 라벨링 → 학습 → 모델 비교 → 추론**

레시피에서 이미지 폴더, 클래스와 분할을 설정합니다. 라벨링에서 정답을 저장하고 선택한 실행 환경으로 학습한 뒤, 같은 평가 조건으로 결과를 비교합니다. 검사 모델을 명시적으로 지정한 다음 추론을 실행합니다.

## AI는 후보만 만든다

AI 결과는 검토할 후보입니다.<br>
사용자가 결과를 확정하고, 라벨 저장을 별도로 수행합니다.<br>
검사 모델 지정은 별도 단계이며 학습 완료만으로 검사 모델이 바뀌지 않습니다.

## 작업별 상세

### 객체 탐지

![와셔 4개와 불량 와셔 1개를 표시한 실제 검사 화면](docs/tutorial/images/readme/ko/detection-washer.png)

실제 앱에서 와셔 4개와 불량 와셔 1개를 구분하고 박스 5개를 저장한 화면입니다.

실제 객체나 결함을 감싸는 박스를 그리고 클래스와 영역을 확인한 뒤 **라벨 저장**을 누릅니다.
2점 드래그 또는 4점 극점 입력을 선택합니다. 4점 입력은 위·아래·왼쪽·오른쪽 순서로 일반 축 정렬 박스를 만듭니다.
대상이 없는 이미지는 **객체 없음**으로 명시합니다.
단축키: **1–9**로 클래스를 선택하고, **Ctrl+Enter**로 저장하고 다음 이미지로 이동합니다.

### 세그멘테이션과 Smart Mask

![기포 영역 9개를 마스크로 저장한 실제 앱 화면](docs/tutorial/images/readme/ko/segmentation-bubble.png)

필름 검사 예제에서 기포 영역 9개를 마스크로 저장한 화면입니다.

폴리곤, 브러시, 지우개로 경계를 만듭니다.
세그멘테이션 레시피의 **라벨링 옵션**에서 **자동 윤곽**을 한 번 켜면 시작 박스를 그릴 때 MobileSAM 후보가 생성됩니다.
보정이 필요하면 포함점이나 제외점을 한 번에 하나씩 추가하고 **후보 다시 생성**을 누른 뒤 **이전 후보 보기**와 **현재 후보 보기**로 비교합니다.
표시된 후보가 맞으면 **확정**, 버리려면 **건너뛰기**를 선택합니다. 확정은 작업 중인 라벨에 반영하며, **라벨 저장**이 정답 파일에 기록합니다. 후보 생성이나 전환만으로는 파일을 저장하지 않습니다. **Ctrl+Enter**로 저장하고 다음으로 이동합니다.

### 이상 탐지

![정상 4장·이상 4장·미판정 4장을 표시한 육각형 그리드](docs/tutorial/images/readme/ko/anomaly-grid-disk.png)

절차적으로 생성한 육각형 예제입니다. 사용자가 정상 4장과 이상 4장을 판정했고, 4장은 아직 판정하지 않았습니다.

**정상(OK) → 다음** 또는 **이상(NG) → 다음**을 누르면 이미지 판정을 저장하고 다음 미판정 이미지로 이동합니다.
박스와 마스크는 선택 사항입니다. **G**로 이미지 목록을 그리드 보기로 전환합니다.
YOLOv8/YOLO11 분류는 OK와 NG 예시를 모두 사용하며, PatchCore는 정상으로 검수한 이미지로 학습합니다.
PatchCore는 이미지 판정, 점수/임계값, 위치 후보와 히트맵을 반환합니다. **히트맵 보기**는 라벨·후보·모델을 변경하지 않는 읽기 전용 확인 창을 엽니다.

## 빠른 시작

1. 레시피를 만들고 객체 탐지, 세그멘테이션, 이상 탐지 중 목적을 선택합니다.
2. 원본 이미지 폴더와 별도의 레시피 출력 폴더를 지정하고 클래스 이름과 순서를 확인합니다.
3. 라벨링 화면에서 이미지를 라벨링하거나 검수한 뒤 라벨 또는 OK/NG 판정을 저장합니다.
4. 학습 화면에서 모델 실행 환경을 선택하고 데이터셋 점검과 학습 설정 확인을 마친 뒤 학습을 시작합니다.
5. 같은 평가 조건으로 학습 결과를 비교합니다. 검사 모델로 명시적으로 지정하고 설정을 저장합니다.
6. 추론 화면에서 모델과 이미지를 확인한 뒤 추론을 실행합니다. AI 후보를 검토하며, 후보 확정과 라벨 저장은 별도로 수행합니다.

전체 작업 순서는 [단계별 사용자 매뉴얼](docs/tutorial/README.md), [화면 가이드](docs/tutorial/labeling-workbench-tutorial.html), [단일 파일 가이드](docs/tutorial/labeling-workbench-tutorial-standalone.html)를 참고하세요.

## 설치·요구 사항

- Windows 10/11 x64, .NET 8과 WPF, 로컬 단일 작업자 환경입니다.
- 소스 빌드에는 [global.json](global.json)이 고정한 .NET SDK **8.0.421**, 제공 스크립트에는 PowerShell이 필요합니다.
- SharpGL 뷰어가 요구하는 framebuffer 함수를 지원하는 그래픽 드라이버가 필요합니다.
- 학습·추론·MobileSAM 보조 기능에는 모델별 로컬 Python 실행 환경과 가중치가 필요합니다.

아래 명령으로 소스를 빌드합니다. 저장소의 `dll`, 로컬 `OpenVisionLab` 라이브러리와 `third_party` 자산 및 버전을 맞춘 OpenCvSharp 관리·네이티브 실행 파일을 유지하세요. [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)를 참고하세요. 개인 실행 환경 경로는 커밋하지 않는 `config/labeling-runtime.local.json`에서 관리합니다.

## 하나의 레시피, 여러 모델

레시피는 원본 이미지, 기준 클래스 순서, 정답, train/valid/test 소속과 데이터·설정·모델 출처를 여러 어댑터에서 공유합니다.
레시피 저장 또는 학습 시작 시 내용 기반 `dsv2-...` 데이터 버전과 SHA-256 식별값을 기록하며, 내용이 같으면 버전을 재사용하고 라벨이나 분할이 바뀌면 새 버전을 만듭니다.
데이터셋 화면은 기록된 버전을 표시하고 학습 모델 이력은 사용한 버전을 기록합니다.
**학습 → 실행 환경**에서 등록된 모델을 선택하면 Python 실행 파일, 실행 환경 폴더와 통신 스크립트가 함께 전환됩니다. 최초 자동 탐색에 실패하면 실행 환경 폴더를 지정합니다.
데이터 식별값, 클래스, 분할과 평가 설정이 같은 모델을 비교하며, 조건이 다르면 우열 대신 차이를 표시합니다.

## 개발자용

<details>
<summary>빌드·실행·배포 검증·환경 점검·CI</summary>

### 빌드 명령

Visual Studio에서 `OpenVisionLab.LabelingStudio.sln`을 열고 `OpenVisionLab.LabelingStudio`를 시작 프로젝트로 선택한 뒤 x64로 빌드합니다. 다른 솔루션 프로젝트는 공용 라이브러리입니다.

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.csproj -c Release -p:Platform=x64
```

### 실행

Debug 빌드 실행:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.sln -c Debug -p:Platform=x64
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
```

로컬 Release 번들 게시 및 실행:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Configuration Release
.\scripts\start-labeling-workbench.ps1 -AppMode Publish
```

버전별 self-contained 폴더는 `artifacts\publish\Release\win-x64\0.3.7`입니다. release·publish manifest는 소스·빌드 식별 정보와 전체 payload의 SHA-256을 기록합니다.

### 배포 검증

기존 패키지를 변경하지 않고 검증합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Configuration Release -ReleaseVersion 0.3.7 -VerifyOnly
```

### 환경 점검 CLI

```text
OpenVisionLab.LabelingStudio.exe --environment-self-test --json
```

읽기 전용 점검은 WPF 시작 전에 실행하며 JSON 문서 하나를 출력합니다. PowerShell 자동화에서는 표준 출력을 파일로 돌리고 `Start-Process -Wait`로 기다립니다. 종료 코드는 `0`(준비됨), `2`(확인 필요), `64`(잘못된 인자), `70`(점검 실패)입니다.

### CI

[Windows CI](.github/workflows/ci.yml)는 한영 README, 애플리케이션 빌드, 제품 버전과 공백을 검사합니다. 버전별 self-contained 번들도 빌드하며 pull request가 아닌 실행에서는 임시 workflow artifact를 보관합니다.

샘플 안내: `datasets/object-detection/coco128/coco128/README.txt`, [Python TCP 프로토콜과 mock client 예제](samples/python_protocol/README.md).

</details>

## 알려진 한계

- 설치 프로그램과 코드 서명 패키지가 없으며, 깨끗한 PC의 설치·업그레이드·제거 검증을 마치지 않았습니다. self-contained 번들은 .NET을 포함하지만 호환되는 그래픽 환경과 선택한 모델의 실행 환경은 필요합니다. 기본 또는 가상 디스플레이는 호환되지 않을 수 있습니다.
- 모델 실행 환경과 가중치는 별도 준비가 필요할 수 있으며 승인 없이 다운로드하지 않습니다. 등록된 어댑터와 호환되는 클래스·분할·출력 매핑이 있어야 지원하며, GitHub의 임의 모델을 이름만으로 지원하지는 않습니다.
- 로컬 기능 확인은 생산 정확도, 장시간 안정성, takt time을 입증하지 않습니다. CI 정의만으로 원격 실행 성공을 입증하지는 않습니다. 매뉴얼에 표시된 과거 화면의 기준일은 현재 빌드의 화면을 뜻하지 않습니다.
- 진단은 로컬 사용자 저장소와 명시적 support bundle 내보내기를 사용합니다. 텔레메트리, 클라우드 지원과 자동 업로드는 제공하지 않습니다. 환경 점검 CLI는 Main Viewer를 열지 않아 해당 뷰어의 그래픽 컨텍스트를 검증할 수 없으며, 학습·추론 명령을 제공하지 않습니다.
- 레시피 아카이브는 마지막으로 저장한 레시피와 전체 데이터셋을 SHA-256 검증과 함께 내보냅니다. 미저장 라벨, 미확정 후보나 진행 중 작업이 있으면 내보내기를 차단합니다. 가져오기는 기존 데이터를 덮어쓰지 않는 새 위치를 사용하며 레시피를 자동 적용하지 않습니다. 데이터 버전 기록은 원본 데이터셋 전체를 복사하지 않습니다.
- 비정상 종료 복구 범위는 현재 이미지 한 장의 미저장 박스·세그멘테이션·검수 메타데이터입니다. 복구·폐기를 명시적으로 선택하며 후보는 제외하고 라벨 저장도 별도로 해야 합니다. 7일이 지난 초안이나 레시피·데이터셋·이미지 식별 정보가 다른 초안은 사용하지 않습니다.
- 명시적 다중 파일 저장이 중단되면 다음 시작에서 마지막 완료 상태를 복구합니다. 복구 기록이 손상되거나 접근할 수 없으면 작업 화면 진입을 중단하고 로그를 남깁니다. 프로세스 종료 검사는 전원 차단이나 저장 장치 장애에 대한 내성을 입증하지 않습니다.
- 이상 탐지 AI 판정은 미확정 후보입니다. 수동 OK/NG 검수 또는 명시적으로 승인한 폴더 검수 결과만 학습 정답으로 저장합니다. 이전 검수 파일에 없는 AI·사용자 출처는 복원할 수 없습니다.
- PatchCore는 정상 전용 학습을 제공하는 제한된 pilot입니다. 합성 또는 소규모 기능 검사로 현장 품질을 입증할 수 없으며, 학습·검증에 사용하지 않은 생산 데이터를 별도로 평가해야 합니다.
- MobileSAM은 객체마다 시작 박스 하나와 여러 포함·제외점을 지원합니다. 제한된 검사에서 정확한 메타데이터 박스, 20% 확대, 10% 축소와 10% 대각선 이동을 확인했습니다. 더 큰 오차, 임의 형상, 자동 다중 객체 분리와 현장 정확도는 미검증입니다. 자동 확정·저장과 텍스트·음성 기반 라벨링은 지원하지 않습니다. 레시피를 다시 열면 옵션이 복원됩니다. 복원 자체가 추론, 후보 확정, 라벨 저장을 실행하지는 않습니다.
- 클라우드 계정, 팀 권한, 배포 파이프라인과 카메라·PLC 제어는 현재 제품 범위가 아닙니다.

## 버전

현재 버전: **0.3.7**. 단일 기준은 [Directory.Build.props](Directory.Build.props)의 `VersionPrefix`입니다. 버전 이력과 사용자 변경 사항은 [RELEASE_NOTES.md](RELEASE_NOTES.md)를 참고하세요.

이 프로젝트는 제품 버전을 명시적으로 관리합니다.

### 최근 버전 이력

#### v0.3.7 (2026-10-26)

- 한영 README 구성을 정리하고 실제 라벨링 미디어를 교체했습니다.

#### v0.3.6 (2026-10-24)

- Smart Mask 후보 재생성 중 선택을 유지하고 검수 탭의 키보드 이동을 수정했습니다.

#### v0.3.5 (2026-09-16)

- 라벨 저장·복구와 검수·학습 상태 전환을 보완했습니다.

#### v0.3.4 (2026-09-15)

- WPF 작업 책임을 분리하고 기존 바인딩과 명시적 실행을 유지했습니다.

#### v0.3.3 (2026-09-08)

- 라벨링·검수·모델·데이터셋 작업의 소유 책임을 정리했습니다.

#### v0.3.2 (2026-09-07)

- Shell·이미지 목록·실행 환경·검수·모델 작업의 소유 책임을 통합했습니다.

#### v0.3.1 (2026-09-04)

- 표시·카탈로그·저장 어댑터의 책임을 분리했습니다.

#### v0.3.0 (2026-08-31)

- 데이터셋·라벨링·학습·모델 비교를 통합한 작업 흐름을 버전으로 관리했습니다.

#### v0.2.1 (2026-08-25)

- 지원 최소 크기 1100×720에서 이미지 목록이 창 안에 표시되도록 수정했습니다.

#### v0.2.0 (2026-08-24)

- 작업 상태를 바꾸지 않고 지원 화면에 한국어·영어 전환을 반영했습니다.

## 라이선스

[MIT License](LICENSE)로 배포합니다. 소프트웨어를 복사하거나 배포할 때 [LICENSE](LICENSE), [NOTICE](NOTICE), [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)의 저작권·라이선스 고지를 유지하세요.

Copyright (c) 2026 최노아 (Noah-Choi)
