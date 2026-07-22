# MobileSAM 박스 오차 강건성 평가

Status: Complete

Field validation: Not evaluated

이 평가는 정확한 정답 박스에서 통과한 MobileSAM 단일 박스 프롬프트가
초보 사용자의 작은 박스 오차에도 유지되는지 확인한 고정 합성 평가입니다.
점/negative 프롬프트를 미리 구현하지 않고, 실제로 추가 입력 방식이 필요한지
결정하기 위한 근거로 사용합니다.

## 고정 범위

- 입력: 8개 결함 클래스의 `train`, `val`, `test` 단일 결함 이미지 1장씩, 총 24장.
- 원본 박스별 변형 4개:
  - `expand-20pct`: 가로·세로 각 방향으로 원본 크기의 20%만큼 확대.
  - `shrink-10pct`: 가로·세로 각 방향으로 원본 크기의 10%만큼 축소.
  - `shift-negative-10pct`: 좌상 방향으로 원본 크기의 10%만큼 이동.
  - `shift-positive-10pct`: 우하 방향으로 원본 크기의 10%만큼 이동.
- 총 실제 MobileSAM 추론: 96회.
- 사용 가능 기준: 후보 mask IoU `>= 0.50`.
- box-only gate: 전체 사용 가능 비율 `>= 75%`이며 모든 클래스 중앙 IoU
  `>= 0.50`.
- 재학습, UI 변경, 자동 저장, 모델 채택은 범위에서 제외했습니다.

## 결과

- 사용 가능 / 편집 필요 / 건너뜀: `96 / 0 / 0`.
- 전체 사용 가능 비율: `100%`.
- 전체 중앙 IoU: `0.856132`.
- 가장 낮은 클래스 중앙 IoU: `crack 0.704918`.
- 가장 낮은 변형 중앙 IoU: `shrink-10pct 0.850117`.
- 가장 낮은 단일 결과: `crack/train/shrink-10pct 0.658824`.
- worker 중앙 / P95 시간: `3145.577 ms / 3577.896 ms`.
- box-only gate: `Pass`.

| 변형 | 사용 가능 / 편집 / 건너뜀 | 중앙 IoU | 중앙 Dice |
| --- | ---: | ---: | ---: |
| `expand-20pct` | 24 / 0 / 0 | 0.862745 | 0.926316 |
| `shrink-10pct` | 24 / 0 / 0 | 0.850117 | 0.918987 |
| `shift-negative-10pct` | 24 / 0 / 0 | 0.855104 | 0.921893 |
| `shift-positive-10pct` | 24 / 0 / 0 | 0.856132 | 0.922490 |

## 재현성과 원본 불변성

- 런타임: `MobileSAM / Ultralytics 8.4.101 / Torch 2.12.1+cpu / cpu`.
- weight SHA-256:
  `6DBB90523A35330FEDD7F1D3DFC66F995213D81B29A5CA8108DBCDD4E37D6C2F`.
- 원본 파일 수 전/후: `4,525 / 4,525`.
- 원본 tree SHA-256 전/후:
  `4E511A2E08F2ED609B78B40D6B789DE691C968E71ED5A298B76A1E7CA1FB52A8`.
- 증거:
  `artifacts/mobile-sam-box-jitter-matrix/20260722-165800/summary.json`,
  `selection-manifest.json`, `sample-results.jsonl`, `summary.md`,
  `predicted-masks/`.

## 제품 결정

현재의 작은 확대·축소·이동 범위에서는 box-only 프롬프트가 모든 고정 표본에서
사용 가능 기준을 통과했습니다. 따라서 점/negative 프롬프트와 추가 상태 관리는
현재 다음 개발 우선순위가 아닙니다. 실제 작업에서 반복 실패가 재현되거나 이보다
큰 오차 범위를 별도 계약으로 승인할 때만 입력 방식 확장을 다시 검토합니다.

이 결과는 합성 이미지 기반 라벨링 보조 기능 근거입니다. 실제 카메라 영상,
복합 결함, 박스가 결함을 놓치는 경우, 생산 정확도는 평가하지 않았습니다.

## 완료 기록

Status: Complete

Scope: 24개 고정 합성 이미지에 대한 4종 결정론적 박스 오차, 총 96회 실제
MobileSAM 추론과 원본 불변성 검증.

Acceptance criteria: 96회 worker 성공; 전체 사용 가능 비율 `100%`; 모든 클래스
중앙 IoU `>= 0.50`; 원본 파일 수와 tree SHA-256 전후 동일; runtime/weight
provenance 기록.

Verification: 격리 Debug 빌드, `--mobile-sam-box-prompt`,
`--real-mobile-sam-box-jitter-matrix`, 문서 게이트, `git diff --check`.

Evidence: `artifacts/mobile-sam-box-jitter-matrix/20260722-165800`.

Boundary / next dependency: field validation은 `Not evaluated`. 실제 사용자의
반복 가능한 박스 실패가 생기기 전에는 point/negative 입력을 구현하지 않습니다.
