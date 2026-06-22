# YOLO Model Comparison - 2026-06-22

이 문서는 현재 샘플 데이터 기준으로 기존 학습 모델과 Codex 실험 학습 모델을 같은 조건에서 비교한 기록입니다.

## 비교 대상

- 사용자 기존 모델: `C:\Git\yolov5\best.pt`
- Codex 실험 모델: `C:\Git\yolov5\runs\train\codex_compare_20260622_172731\weights\best.pt`
- 데이터 설정: `C:\Git\Labelling_Application\artifacts\yolo_compare_data_20260622.yaml`
- 클래스: `OK`, `NG`
- 이미지 크기: `320`
- 실행 환경: CPU, `C:\Git\yolov5\.venv`

## 데이터셋 상태

- train images: 125
- train labels: 125
- valid images: 125
- valid labels: 125
- train objects: 143개, `OK` 125개, `NG` 18개
- valid objects: 143개, `OK` 125개, `NG` 18개
- train/valid 이미지 파일은 125장 모두 같은 내용입니다.
- label 파일도 대부분 겹칩니다.

현재 수치는 일반화 성능이라기보다 "현재 샘플 데이터에 얼마나 잘 맞는가"를 보는 용도입니다. 교육용 샘플로는 괜찮지만, 실제 모델 품질 비교용으로는 train/valid/test 분리가 먼저 필요합니다.

## 검증 결과

| 모델 | Precision | Recall | mAP50 | mAP50-95 | OK mAP50 | NG mAP50 | CPU inference |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 사용자 기존 `best.pt` | 0.997 | 1.000 | 0.995 | 0.961 | 0.995 | 0.995 | 155.8 ms/image |
| Codex 10 epoch 실험 | 0.749 | 0.428 | 0.389 | 0.215 | 0.778 | 0.00028 | 150.7 ms/image |

## UI confidence 기준 재확인

YOLO mAP 평가는 아주 낮은 confidence 후보도 포함합니다. 실제 앱 UI는 보통 `25%` 이상 후보를 보여주므로, 저장된 prediction label을 다시 읽어 `confidence >= 0.25`, `IoU >= 0.5` 기준으로 확인했습니다.

| 모델 | OK TP/FN | NG TP/FN | 해석 |
| --- | ---: | ---: | --- |
| 사용자 기존 `best.pt` | 125 / 0 | 18 / 0 | 현재 샘플의 정답 위치는 모두 잡음. 다만 후보가 과하게 많이 나와 후보 정리 정책이 필요함. |
| Codex 10 epoch 실험 | 0 / 125 | 0 / 18 | confidence 25% 이상 후보가 없어 실제 UI 추론용으로 부적합함. |

confidence 분포도 차이가 큽니다.

- 사용자 기존 모델: 상위 후보가 약 `0.97 ~ 0.98`까지 올라옵니다.
- Codex 실험 모델: 최고 confidence가 `0.237` 수준이라 UI 기준 후보로 살아남지 못합니다.

## 결론

현재 기준 모델은 사용자 기존 `C:\Git\yolov5\best.pt`를 유지하는 것이 맞습니다.

Codex가 방금 돌린 학습은 CPU 10 epoch 실험이며, 결과가 충분히 올라오지 않아 중단했습니다. 지금 이 모델을 앱에 적용하면 사용자는 "추론이 안 된다"고 느낄 가능성이 큽니다.

사용자 기존 모델은 현재 샘플에는 매우 잘 맞습니다. 대신 train/valid가 같은 이미지라 실제 현장 이미지까지 잘 맞는지는 아직 증명되지 않았습니다. 다음 단계는 새 모델을 또 오래 학습하는 것보다, 데이터셋 분리와 라벨 품질 점검을 먼저 하는 쪽이 맞습니다.

## 수정/호환 처리

현재 Python 패키지 버전에서 기존 YOLOv5 코드가 그대로 돌지 않는 부분이 있어 아래 파일을 보정했습니다.

- `C:\Git\yolov5\yolov5Master\utils\metrics.py`
  - NumPy의 `trapz`/`trapezoid` 차이를 흡수했습니다.
- `C:\Git\yolov5\yolov5Master\utils\plots.py`
  - Pillow의 `getsize`/`getbbox` 차이를 흡수했습니다.

trusted local checkpoint를 읽기 위해 검증 명령에서는 `TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD=1`을 사용했습니다.

## 실행 기록

- 사용자 모델 검증 로그: `C:\Git\Labelling_Application\artifacts\logs\yolo-val-user-best-20260622-172731-v4.log`
- Codex 학습 로그: `C:\Git\Labelling_Application\artifacts\logs\yolo-train-codex-compare-20260622-172731.log`
- Codex 모델 검증 로그: `C:\Git\Labelling_Application\artifacts\logs\yolo-val-codex-compare-best-20260622-172731.log`
- 사용자 모델 검증 결과: `C:\Git\yolov5\runs\val\user_best_20260622_172731`
- Codex 모델 검증 결과: `C:\Git\yolov5\runs\val\codex_compare_best_20260622_172731`
- Codex 학습 run: `C:\Git\yolov5\runs\train\codex_compare_20260622_172731`

## 다음 할 일

1. train/valid/test를 실제로 분리합니다. 현재처럼 같은 이미지가 train과 valid에 모두 있으면 학습 품질을 착각하기 쉽습니다.
2. NG 샘플을 늘립니다. 현재 NG 18개는 너무 적어 새 학습 모델이 NG를 안정적으로 배우기 어렵습니다.
3. 앱 안에 모델 비교 리포트를 넣습니다. 사용자가 "기존 모델 vs 새 학습 모델"의 mAP, confidence, 누락/과검출을 바로 볼 수 있어야 합니다.
4. 후보 과다 생성 정리 정책을 만듭니다. 기존 모델은 잘 잡지만 낮은 confidence 후보가 많이 저장되므로 UI 후보 표시, NMS, threshold, top-k 정책을 명확히 해야 합니다.
5. 새 학습은 데이터 분리 후 GPU 또는 충분한 시간으로 다시 돌립니다. CPU 10 epoch 결과만으로 새 모델을 채택하지 않습니다.
