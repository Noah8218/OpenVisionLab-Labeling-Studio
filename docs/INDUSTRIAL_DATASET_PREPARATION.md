# 산업용 공개 데이터셋 빠른 준비 가이드

라벨링 워크플로우를 바로 시작하려면 먼저 이미지를 라벨링 앱에 바로 쓰기 좋은 형태로 준비합니다.

## 사용 스크립트

- 위치: `scripts\prepare-industrial-dataset.ps1`
- 목적:
  - 공개 데이터셋(또는 수동 소스)을 내려받거나 복사
  - YOLO 데이터셋(`data/train|valid|test/images`) 구조로 평탄화 복사
  - Kolektor `*_label.bmp` 마스크를 YOLO `Defect` 박스로 변환
  - 필요 시 UTF-8 no BOM `data.yaml` 생성

## 기본 권장 흐름 (YOLOv5)

1. 산업용 샘플 데이터셋을 준비해 로컬에 둡니다.
2. 아래처럼 앱에서 바로 읽을 수 있는 레이아웃으로 변환합니다.
3. 생성된 `KolektorSDD\app\data\train|valid|test` 폴더를 YOLOv5 학습/검증/최종 검증에 사용합니다.

## 실행 예시

```powershell
# 예시 A: KolektorSDD를 직접 다운로드해서 바로 app 레이아웃으로 준비 (검증용 split 80/20)
.\scripts\prepare-industrial-dataset.ps1 `
  -Dataset KolektorSDD `
  -Download `
  -ForAppLayout `
  -TrainSplitRatio 0.8 `
  -CreateDataYaml

# 예시 A-2: Kolektor 마스크를 YOLO Defect 박스로 바꾸고 true held-out test split까지 생성
.\scripts\prepare-industrial-dataset.ps1 `
  -WorkspaceRoot .\artifacts\industrial-datasets\kolektor-yolo-heldout `
  -Dataset KolektorSDD `
  -ArchivePath C:\temp\kolektor_test\KolektorSDD\raw\expanded `
  -ForAppLayout `
  -TrainSplitRatio 0.70 `
  -TestSplitRatio 0.15 `
  -CreateDataYaml `
  -CreateYoloLabelsFromKolektorMasks `
  -UseAbsoluteYamlPath `
  -CleanOutput `
  -TrainPositiveOversampleFactor 8

# 예시 B: 수동으로 준비한 폴더/압축에서 준비
.\scripts\prepare-industrial-dataset.ps1 `
  -Dataset Manual `
  -ArchivePath C:\Data\MyIndustrialSet `
  -ForAppLayout `
  -TrainSplitRatio 0.8 `
  -CreateDataYaml

# 예시 C: 압축 파일 직접 지정
.\scripts\prepare-industrial-dataset.ps1 `
  -Dataset Manual `
  -ArchivePath C:\Data\downloads\industrial_dataset.zip `
  -ForAppLayout
```

## 현재 지원 데이터셋

- `KolektorSDD` (기본 제공 다운로드 URL 사용)
- `VisA` (Kaggle 데이터가 있을 때 `-UseKaggle` 조합 필요)
- `Severstal` (Kaggle competition 데이터)
- `Manual` (로컬 폴더 또는 zip 파일을 수동 지정)

## 현재 앱 검증 상태

- 로컬 검증 원본: `C:\temp\kolektor_test\KolektorSDD\raw\expanded`
- 앱 프리셋: 데이터셋 생성 wizard에서 객체탐지 목적 선택 후 `Kolektor 산업 이미지(박스)`
- 검증 명령:

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --exe-industrial-object-labeling-smoke --seed 260626 --label-count 10
```

- 최신 검증 결과: 317장 복사, 10장 랜덤 박스 라벨 저장, 저장 label 재오픈 확인, 데이터셋 체크 통과.
- 2026-06-28 held-out 생성 검증: `artifacts\industrial-datasets\kolektor-yolo-heldout-20260628-153341\KolektorSDD\app\data.yaml`
  - train: 이미지 238 / label txt 238 / defect label 29 / empty label 209
  - valid: 이미지 102 / label txt 102 / defect label 17 / empty label 85
  - test: 이미지 59 / label txt 59 / defect label 6 / empty label 53
  - `*_label.bmp`는 라벨링 이미지로 복사되지 않음.
  - `data.yaml`은 UTF-8 no BOM이며 class는 `Defect` 1개.
  - `data.yaml`의 `path`는 YOLOv5 외부 실행에서도 바로 읽을 수 있게 absolute path로 생성함.
  - 현재 운영 `best.pt`는 2클래스, `yolov5s.pt`는 80클래스라서 이 1클래스 데이터셋과 직접 비교가 preflight에서 차단됨.
  - 2026-06-28 짧은 학습 검증: 같은 `Defect` class list로 baseline 1 epoch와 candidate 3 epoch를 학습하고 held-out `test` 비교까지 완료. 두 모델 모두 mAP50-95 `0`, UI confidence 25% 후보 `0`개라 모델 채택은 보류.
  - 2026-06-28 oversampling 검증: `-TrainPositiveOversampleFactor 8`로 train positive를 29개에서 232개로 늘린 artifact `artifacts\industrial-datasets\kolektor-yolo-oversample-20260628-1605\KolektorSDD\app\data.yaml`를 생성. 5 epoch 학습 후 validation recall은 `0.0588`까지 움직였지만 held-out test mAP와 UI 후보는 여전히 `0`이라 모델 채택은 보류.

## 출력 구조

- 루트: `${WorkspaceRoot}\${Dataset}`
- 앱 학습용 이미지:
  - `${WorkspaceRoot}\${Dataset}\app\data\train\images`
  - `${WorkspaceRoot}\${Dataset}\app\data\valid\images`
  - `${WorkspaceRoot}\${Dataset}\app\data\test\images`
- YOLO 라벨:
  - `${WorkspaceRoot}\${Dataset}\app\data\train\labels`
  - `${WorkspaceRoot}\${Dataset}\app\data\valid\labels`
  - `${WorkspaceRoot}\${Dataset}\app\data\test\labels`
- YAML:
  - `${WorkspaceRoot}\${Dataset}\app\data.yaml`

## 주의사항

- `*_label.bmp`, `*_mask.*`, `*_gt.*` 같은 라벨/마스크 이미지는 기본적으로 라벨링할 이미지에서 제외합니다. 원본 확인이 목적일 때만 `-IncludeLabelImages`를 사용합니다.
- `-CreateYoloLabelsFromKolektorMasks`는 결함 마스크의 bounding box를 `Defect` YOLO label로 만들고, 결함이 없으면 빈 txt를 생성합니다.
- 같은 출력 폴더에 다시 생성할 때는 `-CleanOutput`을 사용합니다. 그렇지 않으면 기존 파일과 새 파일이 섞여 검증 수량이 틀어질 수 있습니다.
- 앱 밖의 YOLOv5 `train.py`/`val.py`에서 바로 사용할 데이터셋은 `-UseAbsoluteYamlPath`로 생성합니다.
- 결함이 너무 적은 학습 실험에는 `-TrainPositiveOversampleFactor`를 사용할 수 있습니다. 이 옵션은 train split의 비어 있지 않은 label만 복제하고 valid/test는 건드리지 않습니다.
- 기존 라벨 파일이 이미 존재하는 일반 수동 데이터셋은 별도 변환 규칙이 필요합니다.
- VisA/Severstal은 Kaggle CLI 로그인 상태가 필요합니다.
