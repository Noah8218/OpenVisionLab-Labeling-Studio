# MobileSAM 8개 결함 클래스 박스 프롬프트 사용성 평가

Status: Complete

Field validation: Not evaluated

이 기록은 `multishape_defect_labeling_dataset_v3_hexagon_500` 합성 데이터의
8개 결함 클래스에서 현재 MobileSAM 단일 박스 프롬프트가 검토 가능한
세그멘테이션 후보를 만드는지 확인한 고정 평가입니다. 모델의 생산 정확도나
현장 일반화 성능을 주장하지 않습니다.

## 고정 평가 계약

- 표본: `train`, `val`, `test`에서 클래스별 단일 결함 이미지 1장씩, 총 24장.
- 선택: 각 split과 클래스에서 파일명 순 첫 표본. 선택 목록과 입력 해시는
  실행 전에 `selection-manifest.json`으로 고정.
- 입력 프롬프트: 합성 메타데이터의 정답 tight bounding box.
- 정답: 데이터셋의 binary mask.
- `사용 가능`: IoU `>= 0.50`.
- `편집 필요`: IoU `>= 0.25`이고 `< 0.50`.
- `스킵`: IoU `< 0.25` 또는 worker 실패.
- 박스-only 완료 게이트: 전체 사용 가능률 `>= 75%`이고 모든 클래스의
  중앙 IoU `>= 0.50`.
- 원본 불변성: 실행 전후 전체 원본 트리 파일 수와 SHA-256이 같아야 함.

## 결과

전체 24개 worker 실행이 성공했고 24개 모두 `사용 가능`으로 분류됐습니다.
편집 필요와 스킵은 각각 0개였습니다.

| 클래스 | 사용 가능 / 편집 / 스킵 | 중앙 IoU | 중앙 Dice | 중앙 실행 시간 |
| --- | ---: | ---: | ---: | ---: |
| scratch | 3 / 0 / 0 | 0.8054 | 0.8922 | 3024.2 ms |
| crack | 3 / 0 / 0 | 0.7129 | 0.8324 | 3018.4 ms |
| dark_contamination | 3 / 0 / 0 | 0.8005 | 0.8892 | 3081.5 ms |
| bright_contamination | 3 / 0 / 0 | 0.8710 | 0.9311 | 3055.0 ms |
| discoloration | 3 / 0 / 0 | 0.8874 | 0.9403 | 3015.3 ms |
| edge_chip | 3 / 0 / 0 | 0.9307 | 0.9641 | 3033.1 ms |
| extra_material | 3 / 0 / 0 | 0.8333 | 0.9091 | 3034.8 ms |
| rectangular_void | 3 / 0 / 0 | 0.9480 | 0.9733 | 3113.6 ms |

- 전체 사용 가능률: `100%` (`24/24`).
- 전체 중앙 IoU: `0.8562`.
- worker 중앙 / P95 실행 시간: `3033.1 ms` / `3168.4 ms`.
- 최저 표본: `crack` train `hexagon_NG_026.png`, IoU `0.6667`.
- 최고 표본: `rectangular_void` test `hexagon_NG_224.png`, IoU `0.9494`.
- 박스-only 완료 게이트: `Pass`.

## 재현성과 원본 불변성

- 선택 지문 SHA-256:
  `4735226832BF80A0E8EBE47C74A8887200FC08845EE175316C6675B3A5C37A8D`.
- 원본 파일 수 전/후: `4,525 / 4,525`.
- 원본 트리 SHA-256 전/후:
  `4E511A2E08F2ED609B78B40D6B789DE691C968E71ED5A298B76A1E7CA1FB52A8`.
- 런타임: `MobileSAM / Ultralytics 8.4.101 / Torch 2.12.1+cpu / cpu`.
- 가중치 SHA-256:
  `6DBB90523A35330FEDD7F1D3DFC66F995213D81B29A5CA8108DBCDD4E37D6C2F`.
- 증거:
  `artifacts/mobile-sam-usability-matrix/20260722-153003/summary.json`,
  `selection-manifest.json`, `sample-results.jsonl`, `summary.md`,
  `predicted-masks/`.

## 제품 결정

현재 단일 박스 프롬프트와 기존 폴리곤·브러시 수동 보정 흐름을 유지합니다.
점/음성 프롬프트는 지금 구현하지 않습니다. 정확한 박스 조건에서 모든
클래스가 완료 게이트를 통과했기 때문에 추가 프롬프트 UI와 상태 관리를
도입할 근거가 없습니다.

다음 중 하나가 재현될 때만 이 결정을 다시 엽니다.

- 실제 사용자의 대략적인 박스에서 반복적인 `편집 필요` 또는 `스킵`이 발생함.
- 고정 box-jitter 평가에서 전체 사용 가능률이 `75%` 미만이 됨.
- 어느 클래스든 중앙 IoU가 `0.50` 미만이 됨.

## 증거 경계

이 평가는 합성 이미지와 정답 메타데이터의 정확한 tight box를 사용했습니다.
따라서 대략적으로 그린 사용자 박스의 이동·확장 내성, 실제 카메라 노이즈,
조명 변화, 새로운 결함 형상, 생산 임계값은 평가하지 않았습니다. 실행 시간은
현재 구현의 요청당 worker/model 준비 비용을 포함한 로컬 CPU 관측값이며 다른
장비의 처리량 보장이 아닙니다.

## 완료 기록

Status: Complete

Scope: 8개 합성 결함 클래스의 고정 MobileSAM 단일 박스 프롬프트 사용성,
런타임 provenance, 원본 불변성 평가.

Acceptance criteria: 24개 고정 표본 실행 `24/24` 성공; 전체 사용 가능률
`100%`; 모든 클래스 중앙 IoU `>= 0.50`; 원본 파일 수와 트리 SHA-256 전후
동일; 런타임과 가중치 SHA-256 기록.

Verification: 격리 Debug 빌드, `--mobile-sam-box-prompt`,
`--real-mobile-sam-usability-matrix`, 문서 게이트와 `git diff --check`.

Evidence: `artifacts/mobile-sam-usability-matrix/20260722-153003`.

Boundary / next dependency: 정확한 메타데이터 박스 기반 합성 증거입니다.
대략적인 사용자 박스 실패나 현장 데이터가 새로 확보되기 전에는 점/음성
프롬프트 작업을 반복하지 않습니다.

## 후속 box-jitter 결과

고정 20% 확대, 10% 축소, 좌상/우하 10% 이동을 적용한 96회 후속 평가도
96/96 사용 가능으로 통과했습니다. 작은 결정론적 박스 오차 범위는 더 이상
미평가 상태가 아닙니다. 상세 근거는
`docs/MOBILE_SAM_BOX_JITTER_MATRIX_20260722.md`와
`artifacts/mobile-sam-box-jitter-matrix/20260722-165800`에 있습니다.
