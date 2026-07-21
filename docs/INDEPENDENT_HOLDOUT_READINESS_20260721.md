# 독립 객체탐지 홀드아웃 준비 상태 (2026-07-21)

Status: Blocked

## Scope

현재 원형 디스크 5개 결함 클래스 YOLOv5/YOLOv8/YOLO11 모델을, 학습에 사용하지 않은 별도 카메라 또는 별도 촬영 세션 이미지에서 비교할 수 있는지 확인한다.

## Required acceptance criteria

1. 평가 이미지는 학습/validation/test 원본과 SHA-256이 하나도 겹치지 않는다.
2. 현재 5개 객체탐지 클래스와 같은 클래스 정의 및 YOLO bbox 라벨이 있다.
3. 폴더 또는 `data.yaml`이 독립 촬영/세션 출처임을 식별할 수 있다.
4. 평가 결과는 세 엔진에 같은 이미지와 같은 라벨을 사용한다.

## Read-only evidence

| Candidate | Result | Reason |
| --- | --- | --- |
| `D:\라벨테스트\circular_defect_labeling_dataset_v1_complete\Circular_Disk_OK500_NG500_Images` | Rejected | 객체탐지 학습 패킷의 1,000 이미지와 SHA-256이 1,000장 모두 일치한다. 독립 데이터가 아니다. |
| `D:\새 폴더` | Rejected | JPG 8,000개 중 SHA-256 고유 이미지는 250장뿐이며 라벨/YOLO metadata 파일이 없다. 복사본 기반 이미지이므로 홀드아웃으로 사용하면 누수가 발생한다. |
| 다른 `D:\라벨테스트` 산업군 폴더 | Not selected | 원형 디스크 5개 결함 클래스와 다른 제품/라벨 계약이므로 현재 모델의 품질 검증에 섞지 않는다. |

## 2026-07-21 추가 후보 감사: `card_real_corner`

다음 두 위치는 독립 홀드아웃 후보가 아니다.

- `D:\라벨테스트\Card_Crosspoint_500_Full\crosspoint_industries\card_real_corner`
- `D:\라벨테스트\Industrial_Crosspoint_8Types_500_Each_4000_Full\crosspoint_industries\card_real_corner`

읽기 전용 SHA-256 검사 결과 두 위치의 JPG 500장은 500장 모두 일치하고, `labels_yolo_defect`도 상대 경로별로 500개 전부 일치하며, train/validation/test 분할 파일 3개도 모두 일치한다. 따라서 두 폴더는 별도 카메라·별도 세션이 아니라 같은 데이터의 복제본이다.

각 폴더의 결함 라벨은 500개(빈 OK 라벨 250개, bbox가 있는 NG 라벨 250개)이며 클래스 ID는 `0` 하나뿐이다. 이는 현재 원형 디스크 5개 결함 클래스와 육각형 8개 결함 클래스 모두의 계약과 다르다. 또한 상위 README는 이 데이터를 `Card.bmp` 예시를 기반으로 한 **테스트용 합성·증강 데이터**라고 명시한다.

결론: 이 데이터는 로더·라벨·분할 회귀 테스트에는 재사용할 수 있지만, 기존 모델의 실제 현장 FP/FN, 재현성, 또는 채택 결정을 위한 홀드아웃으로는 사용하지 않는다. 이 후보로 재학습·모델 비교를 실행하지 않았다.

## Exact prerequisite to resume

사용자가 원형 디스크의 별도 카메라 또는 별도 촬영 세션 폴더를 지정해야 한다. 해당 폴더에는 동일한 5개 클래스의 객체탐지 라벨과 이미지가 있어야 하며, 제공 후 SHA-256 중복 검사와 native `data.yaml` 계약 검사를 먼저 통과해야 한다.

이 조건이 충족되면 같은 홀드아웃에서 YOLOv5, YOLOv8, YOLO11의 precision, recall, mAP, class metrics, UI threshold TP/FP/FN, 추론 시간 비교를 한 번 실행한다. 그 전에는 모델을 다시 학습하거나 다른 산업군 데이터를 섞지 않는다.
