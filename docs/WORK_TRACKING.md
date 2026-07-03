# Work Tracking

Last updated: 2026-07-03

이 문서는 반복 작업을 막기 위한 작업 현황판입니다.
새 작업을 시작하기 전에는 이 문서를 먼저 보고, 작업을 마무리할 때 완료/진행 필요 항목을 갱신합니다.

## 마무리 규칙

작업을 마무리할 때 답변에는 아래 3가지를 짧게 포함합니다.

- 완료: 이번에 실제로 바뀐 내용
- 검증: 빌드, 테스트, first-run, UI 실행 중 확인한 것
- 다음 진행: 바로 이어서 하면 좋은 항목

문서 갱신 기준:

- 새 기능이 실제로 동작하고 검증까지 끝났으면 `완료 항목`으로 옮깁니다.
- 아직 구현 전이거나 일부만 된 일은 `진행 필요 항목`에 남깁니다.
- 더 이상 필요 없는 항목은 삭제하거나 `보류/제외`로 옮깁니다.
- 같은 항목을 다시 시작하기 전에 이 문서와 `docs/WPF_VIEW_MIGRATION.md`를 확인합니다.

## 완료 항목

### 2026-06-28 완료/보호 판정

아래 항목은 검증이 끝났으므로 일반 우선순위 목록에서 다시 끌고 오지 않습니다.
해당 코드를 건드릴 때만 `docs/STABLE_VERIFIED_AREAS.md`의 gate를 다시 실행합니다.

| 영역 | 판정 | 완료 근거 |
| --- | --- | --- |
| Viewer ROI 성능/선택 핸들 UX | 완료/보호 | 50만 ROI move/resize/delete/hit-test/render, 겹침 ROI 선택, 선택된 박스의 보이는 핸들 hit-zone, delete 후 zoom 검증 완료 |
| 브러시/지우개 입력 성능 | 완료/보호 | brush/eraser MouseMove, mask drag, partial texture update, cursor preview 검증 완료 |
| 텍스처 pan/zoom 체감 성능 | 완료/보호 | texture pan MouseMove와 wheel zoom 중앙화 검증 완료 |
| Object Review 삭제/선택 잔상 | 완료/보호 | 단일 object delete, delete 후 zoom, 선택 핸들 잔상 제거 검증 완료 |
| Candidate Review 기본 검토 | 완료/보호 | 후보/기존 라벨 비교, 중복 skip, 신규 confirm, 현재 라벨 focus, 실제 EXE focus smoke 검증 완료 |
| 객체탐지 라벨링 저장 루프 | 완료/보호 | 산업 이미지 기반 실제 EXE box 라벨 저장, 빈 정상 완료, reopen, dataset check 검증 완료 |
| 이미지 큐 현재 폴더 표시/열기 | 완료/보호 | 현재 로드된 이미지 폴더 경로 표시, 파일 탐색기 열기 버튼, ViewModel command binding 검증 완료 |
| YOLO 데이터셋 구조 안내 | 완료/보호 | Guide 첫 화면에서 `data.yaml`, `images`, `labels`, 같은 이름의 image/txt 관계 안내 검증 완료 |
| 객체탐지 MVP 다음 행동 안내 | 완료/보호 | Guide 대시보드의 `객체탐지 MVP 완료까지` 문구와 top next-action 정합성 검증 완료 |
| 클래스 편집 UX | 완료/보호 | 등록 클래스 이름 변경, 색상 프리셋 적용, Defect 특별 삭제 금지 제거, 마지막 1개 삭제 방어 검증 완료 |
| 클래스 패널 저장 폴더 분리 UX | 완료/보호 | 클래스 관리 안내를 상단에 두고 저장 폴더는 하단 `데이터셋 저장 폴더(고급)` 접힘 영역으로 분리. `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 클래스 탭 visual smoke 검증 완료 |
| 캔버스 활성 라벨 선택 UX | 완료/보호 | 캔버스 툴바에서 `OK`/`Defect` 같은 현재 라벨 칩을 바로 선택하고, 선택값이 실제 박스 저장 라벨과 동기화되는지 검증 완료 |
| ROI 복사/붙여넣기 클래스 보존 | 완료/보호 | OK ROI를 복사한 뒤 현재 활성 라벨을 NG로 바꿔 붙여넣어도 새 ROI가 OK 클래스와 OK 색상으로 저장/렌더링되는지 검증 완료 |
| 데이터셋/이미지 준비 단계 UX | 완료/보호 | 초보자 가이드와 YOLO 완료 체크를 `데이터셋 만들기 -> 이미지 불러오기 -> 클래스 등록 -> 라벨링` 순서로 분리하고 시각 스모크 검증 완료 |
| 캔버스 라벨 저장 UX | 완료/보호 | 라벨을 그린 직후 캔버스 툴바의 `라벨 저장` 버튼이 활성화되고, 저장 후 `저장 완료`로 비활성화되는 흐름을 ViewModel command와 visual smoke로 검증 완료 |
| 캔버스 라벨/AI 후보 작업 모드 UX | 완료/보호 | 캔버스 상단 상태를 `작업: 저장 라벨 편집`/`작업: AI 후보 검토`/`작업: 라벨+AI 비교`로 분리하고, 모드 버튼을 `라벨 편집`/`AI 검토`/`비교`로 정리. `--wpf-canvas-panel-commands`, `--wpf-detection-display-mode`, 1920x1080/1366x768 responsive, 1920 후보 검토 visual smoke 검증 완료 |
| 메인 현재 데이터셋 표시/선택 UX | 완료/보호 | 메인 상단에 현재 데이터셋 이름, 목적, 저장 경로, 이미지 폴더와 `데이터셋 선택/폴더 열기/이미지 변경` command를 고정 표시. `데이터셋 선택`은 기존 데이터셋 목록을 먼저 열고, 생성은 선택창의 `새 데이터셋 만들기`로만 진입하게 분리. EXE mouse smoke에서 선택 리스트 표시 확인 |
| 데이터셋 이미지 폴더 복원 | 완료/보호 | `dataset.manifest.json`에 이미지 폴더를 저장하고, 데이터셋 선택창에 이미지 폴더를 표시. 기존 recipe는 `VISION.xml`에서 fallback으로 읽으며, 데이터셋 재열기 시 산출물 train/valid/test 폴더보다 사용자가 선택한 이미지 폴더를 우선 복원 |
| 마지막 데이터셋 자동 복원 | 완료/보호 | recipe 적용/데이터셋 생성 시 `.last-opened-recipe`에 마지막 데이터셋을 저장하고, 앱 시작 시 해당 recipe와 이미지 폴더를 자동 복원하는지 WPF startup smoke로 검증 |
| 초보자 가이드 첫 화면 문구 | 완료/보호 | `YOLO 다음 액션`, `완주 체크` 같은 어색한 표시를 `다음 작업`, `완료 체크`로 정리하고 guide panel test 기준에 반영 |
| 모델 비교 기준 표시 | 완료/보호 | Guide 학습 결과 카드에 `비교 기준` 문구를 추가해 최종 검증 라벨 수, 권장 수, 교체 근거 강도를 클릭 전 확인 가능 |
| 모델 센터 현재/후보/적용 상태 UX | 완료/보호 | YOLO 학습/모델 센터에서 `검사 모델`, `학습 결과 모델`, `적용 상태`, `다음 작업`을 제목/값으로 분리. `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 모델 센터 visual smoke 검증 완료 |
| 모델 확정 버튼 상태 UX | 완료/보호 | 모델 센터 버튼을 `검사 모델로 확정`/`이미 적용됨`/`후보 검토 필요`/`후보 없음` 상태형 문구로 전환하고, 후보가 선택되고 recipe 저장이 가능할 때만 활성화. `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 모델 센터 visual smoke 검증 완료 |
| 모델센터 후보 검증 이동 UX | 완료/보호 | 모델센터에 `후보 검증` 버튼을 추가해 학습 결과 모델 후보가 있을 때 후보 검토 화면으로 바로 이동 가능하게 분리. `후보 검토 필요` 확정 버튼은 저장/확정 전용으로 유지. `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 모델센터 visual smoke 검증 완료 |
| 모델 적용 판단 카드 UX | 완료/보호 | YOLO 학습/모델 센터 상단에 `판단/근거/확정` 카드 추가. 현재 검사 모델, 학습 결과 후보, 지표 근거, 다음 확정 동작을 한 곳에서 확인하도록 ViewModel 상태와 XAML 바인딩으로 분리. `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 모델센터 visual smoke 검증 완료 |
| YOLO 실패/복구 현장 표시 UX | 완료/보호 | 학습 실패, 워커 연결 실패, 테스트/재시작 실패, 에폭 상태 응답 없음 상태를 YOLO 탭의 학습/모델 센터와 YOLO 상태 패널 안에 `문제/원인/다음 조치` 카드로 표시. 상태는 Shell/YoloStatus ViewModel에서 관리하고 하단 로그 의존도를 줄임. `dotnet build`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 실패 상태 visual smoke 검증 완료 |
| 후보 검토 모델 비교 상태 UX | 완료/보호 | 후보 검토 화면에서 모델 비교 영역을 항상 상태 카드로 표시. 결과 없음/대기/차이 없음/예시 있음 상태와 모델센터 `검사 모델로 확정` 동선을 분리 표시. `--wpf-candidate-review-panel`, `--mvvm-infra`, 1440x900 후보 검토 visual smoke 검증 완료 |
| 후보 검토/모델 검증 구분 UX | 완료/보호 | 후보 검토 탭에서 현재 이미지 후보 처리는 `현재 이미지 후보 검토`, 학습 결과 모델 판단은 `학습 모델 검증` 제목으로 분리. 모델 검증 상태는 첫 화면에 노출되도록 압축 배치. `--wpf-candidate-review-panel`, `--mvvm-infra`, `--wpf-labeling-shell`, 1440x900 후보 검토 visual smoke 검증 완료 |
| 후보 검토 완료 카드 압축 UX | 완료/보호 | 후보 검토 상태와 `완료 후 다음` 버튼을 한 줄 카드 안으로 압축해 오른쪽 패널 세로 공간을 확보. `--wpf-candidate-review-panel`, `--mvvm-infra`, `--wpf-labeling-shell`, 1440x900 후보 검토 visual smoke 검증 완료 |
| 후보 검토 조작 우선 UX | 완료/보호 | 우측 `AI 후보` 탭 상단에 현재 이미지 후보/학습 모델 검증 역할 카드를 추가하고, 현재 후보의 선택/확정/숨김 조작을 모델 검증 상세보다 위에 배치. 1440x900 기본 화면에서 `라벨 확정`, `전체 라벨화`, `후보 숨김` 버튼이 보이도록 조정. `--wpf-candidate-review-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 후보 검토 visual smoke 검증 완료 |
| 라벨링 툴 UX 벤치마크 | 완료/참고 | CVAT, Label Studio, Roboflow, Labelbox 공식 문서를 기준으로 초보자 온보딩/라벨 schema/AI 보조/리뷰 흐름을 비교하고 `docs/LABELING_UX_BENCHMARK.md`에 우선순위 기록 |
| 초보자 첫 실행 체크리스트 UX | 완료/보호 | Guide 탭의 데이터셋 준비 카드에 `처음 10분 시작` 6단계 체크리스트를 추가. 데이터셋/이미지/클래스/첫 박스/라벨 저장/학습 준비를 한 화면에서 따라가도록 구조화. `--wpf-learning-workflow-panel`, `--mvvm-infra`, `--wpf-labeling-shell`, 1440x900 Guide visual smoke 검증 완료 |
| 상단 툴바 단순화 UX | 완료/보호 | 기본 상단에는 `라벨 저장`, `현재 검사`, 톱니바퀴 도구 메뉴만 유지. 테마, 샘플, 중앙 박스, 템플릿, 작업 전환, YOLO 점검은 도구 Popup 안에서 화면/라벨 보조/작업 전환·환경 섹션으로 정리. `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 기본/메뉴 열림 visual smoke 검증 완료 |
| 템플릿 보조 라벨링 흐름 UX | 완료/보호 | 도구 메뉴에 `템플릿 흐름` 안내를 추가해 `기준 라벨 선택 -> 라벨 초안 생성 -> 위치 확인 -> 라벨 저장` 순서를 표시. 현재 이미지는 `라벨 초안`, 전체 이미지는 `자동 저장`으로 분리하고, 이미지 큐 `전체 자동 저장` 버튼을 텍스트 버튼으로 노출. `--template-guide-ux`, `--template-batch-autolabel-storage`, `--wpf-template-current-image-no-candidate`, `--wpf-image-queue-status`, 1920x1080 도구 메뉴 visual smoke 검증 완료 |
| 첫 사용자 실습 경로 UX | 완료/보호 | Guide/Tools 상단에 `처음 실습 경로` 카드 추가. 데이터셋 -> 이미지 -> 첫 라벨 -> 후보 확인 -> 학습 준비 1-5단계를 기본 1440x900 화면에서 한 번에 보이도록 구성. `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 Guide visual smoke 검증 완료 |
| 첫 사용자 실습 경로 단축 액션 UX | 완료/보호 | `처음 실습 경로` 5개 카드를 `시작/열기/라벨링/검토/점검` 버튼 카드로 전환. 각 카드 클릭은 ViewModel의 `FirstRunSamplePathCommand`를 거쳐 기존 YOLO 가이드 단계(1/2/4/7/5)로 이동하며, 기본 1440x900 화면에서 5개 카드가 모두 보이도록 압축. `dotnet build`, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 Guide visual smoke 검증 완료 |
| 후보/모델 검토 긴 목록 접기 UX | 완료/보호 | 후보 검토 패널의 모델 비교 예시와 검토 이력을 기본 접힘 요약 행으로 전환. 학습 모델 검증 요약은 우측 후보 검토 첫 화면에 올리고, 긴 예시/이력 목록은 필요할 때만 펼치도록 ViewModel 상태와 XAML Expander로 분리. `dotnet build`, `--wpf-candidate-review-layout`, `--wpf-candidate-review-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 후보 검토 visual smoke 검증 완료 |
| YOLO 검사 모델 설정 압축 UX | 완료/보호 | YOLO 탭의 `검사용 모델 설정`을 `현재 검사 설정` 요약 카드와 저장/기본값 버튼 중심으로 재배치. Python/프로젝트/스크립트/모델/이미지/신뢰도/후보 수 입력은 `고급 경로/추론 설정` 접힘 영역으로 이동해 기본 화면 세로 압박을 줄임. `dotnet build`, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 YOLO model visual smoke 검증 완료 |
| YOLO 학습 설정 압축 UX | 완료/보호 | 학습 설정 패널에 `현재 학습 설정` 요약 카드 추가. 이미지 크기/배치/에폭/모델/가중치/검증 분할 입력은 `고급 학습 파라미터` 접힘 영역으로 이동하고, 추천 적용/새로고침/시작/중지 버튼과 학습 상태는 기본 화면에 유지. `dotnet build`, `--wpf-training-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, 1440x900 training visual smoke 검증 완료 |
| 클래스 탭 밀도 압축 UX | 완료/보호 | 클래스 탭 상단에 등록 클래스 수/선택 클래스 요약과 다음 작업 안내 추가. 클래스 색상 프리셋은 `선택 클래스 색상(필요 시)` 접힘 영역으로 이동하고, 클래스 추가/변경/삭제와 레시피 클래스 목록은 기본 화면에서 더 우선 보이게 조정. `dotnet build`, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1440x900 class visual smoke 검증 완료 |
| 1920x1080 장비 기준 해상도 UX | 완료/보호 | WPF 셸 기본 크기와 visual smoke 기본 캡처 기준을 1920x1080으로 전환. 시작 시 현재 모니터 작업 영역보다 크면 1100x720 최소 기준을 유지하며 작업 영역 안으로 보정. 1920x1080 Guide/Training, 1366x768 Guide, 이전 1440x900 재현 캡처 검증 완료 |
| 첫 실행 데이터셋 시작 우선 UX | 완료/보호 | 작은 화면에서 `처음 실습 경로`가 데이터셋 생성/열기 액션보다 먼저 보이던 배치를 수정. Guide 오른쪽 패널에서 데이터셋 설명 다음에 `데이터셋 준비` 카드와 `새로 만들기`/`기존 열기`가 먼저 나오고, 실습 경로는 그 아래로 내려가도록 조정. `dotnet build`, `--wpf-learning-workflow-panel`, 1920x1080/1366x768 startup onboarding visual smoke 검증 완료 |
| 클래스/저장 폴더 역할 분리 UX | 완료/보호 | 공식 라벨링 툴 벤치마크 기준으로 클래스 탭을 라벨 스키마 관리 전용으로 정리. `데이터셋 저장 폴더(고급)` 편집 UI와 관련 패널 프록시/Command 노출을 제거하고, 저장 폴더는 데이터셋 홈/생성/선택 흐름에서 확인하도록 안내. `dotnet build`, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 class visual smoke 검증 완료 |
| 우측 라벨링 로컬 레일 UX | 완료/보호 | 라벨링 단계의 접힌 우측 레일은 `열기`/`라벨`/`도구`/`클래스`만 유지하고, `AI`/`모델` 단계 이동은 상단 워크플로우 레일로만 남김. `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 visual smoke 검증 완료 |
| 상단 워크플로우 레일 압축 UX | 완료/보호 | 상단 1-4 단계 버튼을 두 줄 설명 카드에서 한 줄 단계 이동 버튼으로 축소하고, 현재 단계의 긴 설명은 요약 패널 툴팁으로 이동. `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 visual smoke 검증 완료 |
| 현재 데이터셋 바 압축 UX | 완료/보호 | 메인 데이터셋 바를 48px로 줄이고, 기본 표시를 현재 데이터셋/작업 기준/데이터셋·저장 폴더·이미지 폴더 액션 중심으로 정리. 저장/이미지 경로 상세 카드는 바인딩을 유지한 채 기본 화면에서는 접힘. `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 visual smoke 검증 완료 |
| 상단 상태 행 운영 상태 전용 UX | 완료/보호 | 데이터셋 바 아래 상태 행을 30px로 줄이고, 워크플로우 단계/진행/다음 중복 텍스트는 숨김. 기본 화면에는 데이터셋 수량, 추론 상태, 라벨 저장 상태, 모델 상태만 남김. `--wpf-status-panels`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 visual smoke 검증 완료 |
| 캔버스 툴바 작업 집중 UX | 완료/보호 | 캔버스 툴바에서 선택 도구 중복 칩과 장문 보조 설명 줄을 기본 접힘으로 전환하고, 실제 작업 버튼/모드/저장/클래스 선택을 한 줄 우선 노출. 툴바 높이 보호값은 유지해 상단 잘림을 방지. `--wpf-canvas-panel-commands`, `--wpf-canvas-workflow-context`, `--wpf-detection-display-mode`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 visual smoke 검증 완료 |
| 이미지 큐/우측 작업 패널 맥락 UX | 완료/보호 | 이미지 큐 보조 컨트롤 행을 내용 기반 높이로 바꿔 `전체 자동 저장`/검사 버튼 줄바꿈 잘림을 방지. 이미지 큐 열은 `저장`/`검사`로 분리하고, 우측 작업 패널 제목은 선택된 하위 뷰(`저장 라벨`/`가이드/도구`/`클래스`/`추론 검토`)를 표시. `dotnet build`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 열린 우측 패널 visual smoke 검증 완료 |
| 접힌 우측 작업 레일 발견성 UX | 완료/보호 | 라벨링 기본 화면에서 접힌 우측 레일 상단에 `작업`/현재 뷰 배지를 추가해 오른쪽 영역이 라벨/도구/클래스 작업 패널임을 즉시 알 수 있게 함. 현재 뷰 텍스트는 `RightWorkflowRailCurrentViewText` ViewModel 상태로 계산. `dotnet build`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080/1366x768 visual smoke 검증 완료 |
| 1366 캔버스 툴바 밀도 UX | 완료/보호 | 1366x768 장비 폭에서 삭제 버튼이 다음 줄로 밀리지 않도록 클래스 관리는 현재 클래스 선택 옆 아이콘 버튼으로 압축하고, 중복 활성 라벨 카드와 `빠른 도구` 장식 라벨은 기본 접힘으로 유지. `dotnet build`, `--wpf-canvas-panel-commands`, `--wpf-canvas-workflow-context`, `--wpf-detection-display-mode`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1366x768 visual smoke 검증 완료 |
| 우측 이미지 큐 작업 필요 필터 UX | 완료/보호 | 오른쪽 이미지 큐 빠른 필터 첫 칸에 `작업 필요`를 추가해 라벨링/AI 후보/저장 필요/검사 실패 행을 먼저 볼 수 있게 함. 저장 필요가 있는 라벨 행은 완료가 아니라 작업 필요로 남도록 필터 판정 보정. `dotnet build`, `--wpf-image-queue-status`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080/1366x768 visual smoke 검증 완료 |
| 현재 이미지 작업 요약 UX | 완료/보호 | 이미지 큐 상단에 선택 이미지의 다음 작업 카드(`라벨 작업 필요`/`AI 후보 검토`/`검사 실패`/`저장 완료`)를 추가. 상태 문구는 `WpfImageQueuePanelViewModel`이 선택 행과 행 상태 변경을 구독해 계산하고, XAML은 바인딩 표시만 수행. `dotnet build`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-canvas-panel-commands`, 1920x1080/1366x768 responsive, 1920x1080/1366x768 visual smoke 검증 완료 |
| 오른쪽 작업 패널 헤더 역할 UX | 완료/보호 | 펼친 오른쪽 작업 패널 헤더를 제목만 표시하던 구조에서 현재 하위 뷰 역할 설명(`저장 라벨`/`도구`/`클래스`/`추론`/`모델`)을 함께 보여주는 구조로 변경. 설명 문구는 `RightWorkflowViewDetailText` ViewModel 상태로 계산하고 XAML은 바인딩 표시만 수행. `dotnet build`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080 오른쪽 패널 펼침 visual smoke 검증 완료 |
| 오른쪽 라벨링 작업 패널 전환 UX | 완료/보호 | 펼친 오른쪽 패널의 `라벨`/`도구`/`클래스` 전환 버튼을 헤더 끝의 작은 부속 버튼에서 `라벨링 작업 패널` 로컬 전환 영역으로 분리. 라벨링 단계에서만 표시하고 기존 `ShowSavedLabelsViewCommand`/`ShowLabelingGuideViewCommand`/`ShowClassCatalogViewCommand`와 active Tag 바인딩을 재사용. `dotnet build`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080/1366x768 열린 오른쪽 패널 visual smoke 검증 완료 |
| 저장 라벨 선택 작업 카드 UX | 완료/보호 | 오른쪽 `저장 라벨` 패널을 역할 설명/현재 선택 라벨/선택 라벨 수정/목록 순서로 재배치. 선택 라벨 상태와 저장 안내는 `WpfObjectReviewPanelViewModel`의 `SelectedObjectTask*Text` 파생 상태로 계산하고 XAML은 바인딩 표시만 수행. `dotnet build`, `--wpf-object-review-panel`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, 1920x1080/1366x768 responsive, 1920x1080/1366x768 열린 오른쪽 패널 visual smoke 검증 완료 |
| 클래스 현재 그릴 라벨 안내 UX | 완료/보호 | 오른쪽 `클래스` 패널 상단에 `현재 그릴 클래스` 안내를 추가해 선택 클래스가 다음 박스 라벨에 적용된다는 점을 명확히 표시. 안내 문구는 `WpfClassCatalogPanelViewModel.CurrentDrawingClass*Text`에서 계산하고, 빨간 오류처럼 보이던 작업 안내는 중립 텍스트 톤으로 조정. `dotnet build`, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 class responsive, 1920x1080/1366x768 열린 클래스 패널 visual smoke 검증 완료 |
| 오른쪽 가이드/도구 템플릿 작업 흐름 UX | 완료/보호 | 오른쪽 `가이드/도구` 패널에 `템플릿 반복 라벨링` 카드 추가. `기준 라벨 선택 -> 현재 이미지 라벨 초안 -> 전체 이미지 자동 저장 -> 위치 확인/저장`을 구조화된 단계로 표시하고, 현재 이미지 초안/전체 이미지 자동 저장 버튼을 `WpfLearningWorkflowPanelViewModel` 명령으로 노출해 Shell에서 기존 템플릿 ViewModel 명령을 주입. `dotnet build`, `--wpf-learning-workflow-panel`, `--template-guide-ux`, `--wpf-labeling-shell`, `--mvvm-infra`, 1920x1080/1366x768 guide responsive, 1920x1080 template guide visual smoke 검증 완료 |
| 반복 라벨링 Ctrl+C/V | 완료/보호 | 이전 이미지 ROI를 복사한 뒤 다음 이미지에서 붙여넣어도 예외 없이 같은 좌표에 새 ROI가 생성되고, 복사 버퍼가 유지되는지 WPF ROI 조작 검증에 추가 |
| true held-out 모델 비교 실행 경로 | 완료/보호 | COCO128을 train/valid/test로 물리 분리한 뒤 `compare-yolo-models.ps1 -Task test` 통과. 산업 OK/NG 채택 판단은 별도 진행 |
| 산업 Defect held-out 데이터 준비 | 완료/보호 | Kolektor `*_label.bmp`를 라벨링 이미지에서 제외하고 YOLO `Defect` 박스로 변환해 train/valid/test label 쌍 생성 |
| 산업 Defect 짧은 학습/비교 실행 | 완료/보류 | `Defect` 1클래스 baseline 1ep/candidate 3ep 학습과 held-out test 비교 완료. mAP/UI 후보가 0이라 모델 채택은 금지 |
| 산업 Defect oversampling 실험 | 완료/보류 | train positive oversampling 8배와 5ep 학습 완료. validation recall만 미세 개선, held-out test mAP/UI 후보는 0 |

최근 확인한 핵심 수치:

- `--roi-overlap-hit-test`: first hit `11.218ms`, repeat `6.909ms`, 50,000 overlapped ROI 중 가장 작은 ROI 선택.
- `--roi-500k-hit-test`: query `1.331ms`, hit `2.009ms`, 후보 325개.
- `--roi-500k-mouse-event-performance`: ROI 손잡이 14px 화면 기준 고정 후 move 1000회 `42.993ms`, resize 1000회 `9.119ms`, drag 중 display rebuild 0회.
- `--wpf-roi-object-verification`: 선택된 ROI의 보이는 핸들 바깥쪽 hit-zone, 겹침 선택, delete 후 zoom 상태 통과. object delete `11.304ms`, delete-then-zoom `1.798ms`, selected empty `True`.
- `--wpf-roi-object-verification`: cross-image Ctrl+C/Ctrl+V가 같은 좌표에 새 ROI를 만들고 복사 버퍼를 유지하는지 통과.
- `--wpf-roi-object-verification`: OK ROI 복사 후 활성 라벨이 NG인 상태에서 붙여넣어도 새 ROI가 OK로 저장되고 OK 색상으로 렌더링되는지 통과.
- `--exe-candidate-focus-smoke`: `recipeApplied=True`, `sampleLoaded=True`, `roiCreated=True`, `candidateVisible=True`, `focusClicked=True`, `objectSelected=True`, `focusClickMs=418.6`.
- `--wpf-image-queue-status`: 이미지 큐의 현재 폴더 경로 표시와 현재 폴더 열기 command binding 확인.
- `--wpf-candidate-review-panel`: 후보 검토 버튼 문구와 command binding 통과.
- `--wpf-visual-smoke --review-tab candidates`: 후보 검토 화면 캡처 확인.
- `--wpf-learning-workflow-panel` 및 `--wpf-visual-smoke --review-tab guide`: 초보자 가이드 첫 화면 문구와 배치 확인.
- `--wpf-canvas-workflow-context` 및 full `LabelingApplication.Tests`: 캔버스 활성 라벨 칩, 클래스 카탈로그 선택 동기화, 셸 등록 확인.
- `--wpf-learning-workflow-panel` 및 `--wpf-visual-smoke --review-tab guide`: 데이터셋 만들기와 이미지 불러오기 단계를 별도 필수 단계로 표시하는지 확인.
- `--wpf-visual-smoke --review-tab guide` 및 full `LabelingApplication.Tests`: 메인 상단 현재 데이터셋 컨텍스트 바, 기존 데이터셋 선택/폴더/이미지 command binding 확인.
- `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab classes --width 1440 --height 900`: 클래스 관리 안내, 레시피 클래스 목록, `데이터셋 저장 폴더(고급)` 접힘 영역 확인.
- `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900`: 학습/모델 센터에서 현재 검사 모델, 학습 결과 모델, 적용 상태, 다음 작업이 분리 표시되는지 확인.
- `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900`: 모델 센터 확정 버튼이 후보 상태와 recipe 저장 가능 여부에 따라 문구/활성화 상태를 바꾸는지 확인.
- `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900`: 모델센터의 `후보 검증` 이동 버튼이 후보가 있을 때 활성화되고, `후보 검토 필요` 확정 버튼과 역할이 분리되는지 확인.
- `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900`: 모델 적용 판단 카드의 `판단/근거/확정` 문구가 ViewModel 상태로 노출되고 YOLO 패널 상단에 표시되는지 확인.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900 --show-training-recovery-status`: YOLO 학습/모델 센터와 YOLO 상태 패널의 실패/복구 카드가 ViewModel 상태로 표시되고, 실패 원인과 다음 조치가 하단 로그 밖에서 보이는지 확인.
- `--wpf-candidate-review-panel`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab candidates --width 1440 --height 900`: 후보 검토 화면의 모델 비교 상태 카드가 결과 없음/대기/차이 없음/예시 있음 상태와 모델센터 확정 동선을 표시하는지 확인.
- `--wpf-candidate-review-panel`, `--mvvm-infra`, `--wpf-labeling-shell`, `--wpf-visual-smoke --review-tab candidates --width 1440 --height 900`: 후보 검토 탭에서 현재 이미지 후보 처리와 학습 모델 검증 제목이 분리되어 첫 화면에 보이는지 확인.
- `--wpf-candidate-review-panel`, `--mvvm-infra`, `--wpf-labeling-shell`, `--wpf-visual-smoke --review-tab candidates --width 1440 --height 900`: 후보 검토 완료 상태와 완료 버튼이 한 줄 카드 안에 압축되어 오른쪽 패널 세로 공간을 덜 차지하는지 확인.
- `--wpf-learning-workflow-panel`, `--mvvm-infra`, `--wpf-labeling-shell`, `--wpf-visual-smoke --review-tab guide --width 1440 --height 900`: Guide 탭의 데이터셋 준비 카드에서 `처음 10분 시작` 6단계 체크리스트가 첫 화면에 표시되는지 확인.
- `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900`, `--wpf-visual-smoke --review-tab yolo --width 1440 --height 900 --open-header-tools-menu`: 상단 기본 화면은 저장/검사/톱니바퀴만 보이고, 도구 메뉴 내부가 화면/라벨 보조/작업 전환·환경 섹션으로 열리는지 확인.
- `--template-guide-ux`, `--template-batch-autolabel-storage`, `--wpf-template-current-image-no-candidate`, `--wpf-image-queue-status`, `--wpf-visual-smoke --review-tab yolo --width 1920 --height 1080 --open-header-tools-menu`: 템플릿 보조 라벨링이 라벨 초안 생성, 위치 확인, 라벨 저장 순서로 설명되고 전체 이미지 템플릿 실행은 자동 저장 버튼으로 보이는지 확인.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-learning-workflow-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab guide --width 1440 --height 900`: `처음 실습 경로` 카드가 ViewModel command로 연결되고, 데이터셋/이미지/첫 라벨/후보 확인/학습 준비 단축 액션 5개가 기본 EXE 크기에서 모두 보이는지 확인. 캡처: `tests\artifacts\ui\wpf-first-run-shortcuts-before.png`, `tests\artifacts\ui\wpf-first-run-shortcuts-after.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-candidate-review-layout`, `--wpf-candidate-review-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --show-candidate-disclosure`: 후보 검토 첫 화면에 학습 모델 검증 요약과 접힌 `검증 예시` 행이 표시되고, 긴 예시/이력 목록이 기본 펼침으로 공간을 차지하지 않는지 확인. 캡처: `tests\artifacts\ui\wpf-candidate-progressive-disclosure-before.png`, `tests\artifacts\ui\wpf-candidate-progressive-disclosure-after.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-yolo-model-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab yolo-model --width 1440 --height 900`: YOLO 모델 설정을 열었을 때 긴 경로 입력 대신 현재 검사 설정 요약, 저장 버튼, 접힌 고급 경로/추론 설정이 보이는지 확인. 캡처: `tests\artifacts\ui\wpf-yolo-settings-density-before.png`, `tests\artifacts\ui\wpf-yolo-settings-density-after.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-training-settings-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-yolo-training-session-smoke`, `--wpf-visual-smoke --review-tab training --width 1440 --height 900`: 학습 설정을 열었을 때 긴 파라미터 입력 대신 현재 학습 설정 요약, 추천 적용, 접힌 고급 학습 파라미터, 시작/중지 버튼이 한 화면에 들어오는지 확인. 캡처: `tests\artifacts\ui\wpf-training-settings-density-before.png`, `tests\artifacts\ui\wpf-training-settings-density-after.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-visual-smoke --review-tab classes --width 1440 --height 900`: 클래스 탭에서 등록 클래스 수/선택 클래스 요약이 보이고, 색상 프리셋은 기본 접힘 영역으로 내려가 클래스 추가/변경/삭제와 레시피 클래스 목록이 우선 보이는지 확인. 캡처: `tests\artifacts\ui\wpf-class-catalog-density-before.png`, `tests\artifacts\ui\wpf-class-catalog-density-after.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-learning-workflow-panel`, `--wpf-visual-smoke --review-tab guide --width 1920 --height 1080`, `--wpf-visual-smoke --review-tab training --width 1920 --height 1080`, `--wpf-visual-smoke --review-tab guide --width 1366 --height 768`: 장비 기준 1920x1080에서 가이드/학습 화면이 눌리지 않고, 1366x768 축소 화면에서는 상단 툴바/단계 레일이 잘리지 않으며 오른쪽 패널은 스크롤로 대응하는지 확인. 캡처: `tests\artifacts\ui\wpf-resolution-guide-old-1440.png`, `tests\artifacts\ui\wpf-resolution-guide-base-1920.png`, `tests\artifacts\ui\wpf-resolution-guide-small-1366.png`, `tests\artifacts\ui\wpf-resolution-training-base-1920.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1920 --height 1080`: 좌측 이미지 큐 버튼 줄바꿈 잘림이 사라지고, 큐 열이 `저장`/`검사`로 구분되며, 열린 우측 패널 제목이 `데이터셋 홈`이 아니라 `저장 라벨`로 표시되는지 확인. 캡처: `artifacts\ui\wpf-image-queue-right-panel-after-1920-expanded.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080`, `--wpf-visual-smoke --roi-only --review-tab objects --width 1366 --height 768`: 접힌 우측 작업 레일 상단에 현재 작업 배지가 보이고 작은 폭에서도 캔버스/레일이 화면 밖으로 밀리지 않는지 확인. 캡처: `artifacts\ui\wpf-right-rail-context-before-1920.png`, `artifacts\ui\wpf-right-rail-context-after-1920.png`, `artifacts\ui\wpf-right-rail-context-after-1366.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-canvas-panel-commands`, `--wpf-canvas-workflow-context`, `--wpf-detection-display-mode`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --width 1366 --height 768`: 1366 폭에서 캔버스 툴바의 삭제 버튼이 다음 줄로 밀리지 않고 클래스 관리가 현재 클래스 옆 아이콘 버튼으로 유지되는지 확인. 캡처: `artifacts\ui\wpf-canvas-toolbar-density-before-1366.png`, `artifacts\ui\wpf-canvas-toolbar-density-after-1366-final.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-canvas-panel-commands`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080`, `--wpf-visual-smoke --roi-only --review-tab objects --width 1366 --height 768`: 이미지 큐 상단의 현재 이미지 작업 카드가 선택 행 상태를 요약하고, 라벨링 화면의 목록/캔버스 툴바를 깨지 않는지 확인. 캡처: `artifacts\ui\wpf-current-task-clarity-before-1920.png`, `artifacts\ui\wpf-current-task-clarity-after-1920.png`, `artifacts\ui\wpf-current-task-clarity-after-1366.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --width 1920 --height 1080`, `--wpf-responsive-layout --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1920 --height 1080`: 펼친 오른쪽 작업 패널 헤더에서 현재 패널 역할 설명이 보이고 패널 콘텐츠를 가리지 않는지 확인. 캡처: `artifacts\ui\wpf-right-workflow-task-before-1920.png`, `artifacts\ui\wpf-right-workflow-task-after-1920.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-responsive-layout --review-tabs objects --width 1920 --height 1080`, `--wpf-responsive-layout --review-tabs objects --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1920 --height 1080`, `--wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1366 --height 768`: 펼친 오른쪽 패널에서 `라벨링 작업 패널` 로컬 전환 영역이 보이고, 라벨/도구/클래스 이동 버튼이 패널 내용과 겹치지 않는지 확인. 캡처: `artifacts\ui\wpf-right-workflow-local-switcher-before-1920.png`, `artifacts\ui\wpf-right-workflow-local-switcher-after-1920.png`, `artifacts\ui\wpf-right-workflow-local-switcher-after-1366.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-object-review-panel`, `--wpf-labeling-shell`, `--wpf-image-queue-status`, `--mvvm-infra`, `--wpf-responsive-layout --review-tabs objects --width 1920 --height 1080`, `--wpf-responsive-layout --review-tabs objects --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1920 --height 1080`, `--wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1366 --height 768`: 오른쪽 `저장 라벨` 패널에서 현재 선택 라벨과 수정/저장 안내가 목록보다 먼저 보이고, 1366 폭에서도 클래스 적용/삭제 영역과 목록이 겹치지 않는지 확인. 캡처: `artifacts\ui\wpf-object-review-task-card-before-1920.png`, `artifacts\ui\wpf-object-review-task-card-after-1920.png`, `artifacts\ui\wpf-object-review-task-card-after-1366.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-class-catalog-panel`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --review-tabs classes --width 1920 --height 1080`, `--wpf-responsive-layout --review-tabs classes --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab classes --right-workflow-expanded --width 1920 --height 1080`, `--wpf-visual-smoke --roi-only --review-tab classes --right-workflow-expanded --width 1366 --height 768`: 오른쪽 `클래스` 패널에서 선택 클래스가 다음 새 박스에 적용된다는 안내가 보이고, 작업 안내가 오류처럼 빨갛게 보이지 않으며, 1366 폭에서도 클래스 입력/목록이 화면 안에 남는지 확인. 캡처: `artifacts\ui\wpf-class-current-label-task-before-1920.png`, `artifacts\ui\wpf-class-current-label-task-after-1920.png`, `artifacts\ui\wpf-class-current-label-task-after-1366.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`, `--wpf-learning-workflow-panel`, `--template-guide-ux`, `--wpf-labeling-shell`, `--mvvm-infra`, `--wpf-responsive-layout --review-tabs guide --width 1920 --height 1080`, `--wpf-responsive-layout --review-tabs guide --width 1366 --height 768`, `--wpf-visual-smoke --roi-only --review-tab guide --right-workflow-expanded --expand-learning-concepts --focus-template-workflow --width 1920 --height 1080`: 오른쪽 `가이드/도구` 패널에서 템플릿 반복 라벨링 순서와 현재/전체 실행 버튼이 보이는지 확인. 캡처: `artifacts\ui\wpf-guide-tools-flow-before-1920.png`, `artifacts\ui\wpf-guide-tools-template-flow-after-1920.png`.
- `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--wpf-learning-workflow-panel`, `--wpf-startup-onboarding-visual --width 1920 --height 1080`, `--wpf-startup-onboarding-visual --width 1366 --height 768`: 첫 실행 Guide 화면에서 데이터셋 생성/열기 액션이 실습 경로보다 먼저 보이는지 확인. 캡처: `tests\artifacts\ui\wpf-startup-onboarding-1920.png`, `tests\artifacts\ui\wpf-startup-onboarding-1366.png`, `tests\artifacts\ui\wpf-startup-onboarding-after-1920.png`, `tests\artifacts\ui\wpf-startup-onboarding-after-1366.png`.
- Full `LabelingApplication.Tests` 및 `--wpf-image-queue-status`: 데이터셋 manifest의 `imageRootPath`, 선택창 이미지 경로 표시, 기존 VISION.xml fallback, 재열기 시 operator image folder 우선순위 확인.
- `--wpf-startup-dataset-restore`: 마지막으로 적용한 recipe를 `.last-opened-recipe`에서 읽어 WPF 셸 시작 시 recipe, 클래스, 이미지 폴더, 이미지 큐가 복원되는지 확인.
- `--wpf-model-comparison-heldout`: held-out 최종 검증 라벨 수에 따라 모델 비교/교체 기준 문구가 달라지는지 확인.
- `compare-yolo-models.ps1 -Task test`: COCO128 true held-out artifact 기준 train 96/valid 16/test 16 라벨 쌍으로 통과. `yolov5m.pt` mAP50-95 `0.657`, `yolov5s.pt` mAP50-95 `0.561`.
- `prepare-industrial-dataset.ps1`: Kolektor held-out artifact 기준 train 238/valid 102/test 59 이미지/라벨 쌍 생성, defect label 52, empty label 347, label BMP 이미지 복사 0, data.yaml UTF-8 no BOM.
- `compare-yolo-models.ps1 -Task test`: 산업 Kolektor `Defect` 1클래스 baseline 1ep vs candidate 3ep 비교 통과. 둘 다 precision/recall/mAP `0`, UI 후보 `0/17700`.
- `compare-yolo-models.ps1 -Task test`: oversampling 5ep 모델 비교 통과. validation recall `0.0588`까지는 움직였지만 held-out test는 precision/recall/mAP `0`, UI 후보 `0/17700`.

현재 남은 작업은 위 항목의 재작업이 아니라, 아래 세 갈래입니다.

1. 클래스 탭에서 저장 폴더 편집을 제거한 뒤 1366x768 class visual smoke를 확인했을 때 오른쪽 검토/설정 패널이 화면 밖으로 밀리는 문제가 보였습니다. 다음 우선순위는 작은 폭에서 오른쪽 패널을 보존하는 responsive layout 구조 수정입니다.
2. 산업 `Defect` 모델 품질을 올립니다. 짧은 1ep/3ep와 oversampling 5ep는 파이프라인 검증/보류로 완료됐고, 다음은 image size 상향 또는 마스크 박스 padding처럼 입력 표현을 바꾸는 실험입니다.
3. 세그멘테이션/이상탐지는 객체탐지 MVP를 흔들지 않는 별도 완료 기준으로 진행합니다.

1. WPF 셸이 기본 실행 화면입니다.
   - `MvcVisionSystem.exe` 기본 실행은 WPF 셸입니다.
   - `FormMainFrame.cs`, `FormMainFrame.Designer.cs`, `FormMainFrame.resx`는 삭제됐고, `--winforms-shell`/`--legacy-winforms` fallback도 제거했습니다.

2. WPF 이미지 큐가 동작합니다.
   - Root, Folder, Refresh, Next를 지원합니다.
   - 상태 필터, 파일명 검색, label/AI 상태 표시가 들어갔습니다.
   - 선택 이미지 로드, 단일 Detect, visible Batch, failed Retry, Stop, 진행률 표시가 들어갔습니다.

3. 실제 YOLO 샘플 추론이 WPF에서 가능합니다.
   - `C:\Git\yolov5\best.pt`
   - `C:\Git\yolov5\data\train\images`
   - WPF `AI 검출`, 이미지 큐 `Detect`, YOLO 탭 `Test`에서 확인했습니다.

4. WPF Object Review 기본 흐름이 들어갔습니다.
   - AI 후보 목록, 후보 상세, 선택 확정, 전체 확정, 스킵이 가능합니다.
   - 신뢰도 slider가 들어갔습니다.
   - `Enter`, `Delete/Backspace`, `Ctrl+A` 키보드 조작이 들어갔습니다.

5. WPF YOLO 환경 탭이 들어갔습니다.
   - 첫 점검, 설치, 테스트, 재시작, 중지를 지원합니다.
   - Python executable, YOLO project, client script, weights, image root, requirements, worker 상태를 표시합니다.

6. WPF YOLO 모델 설정 편집이 들어갔습니다.
   - Python path, project root, client script, weights, image root, 신뢰도, 시간 제한, auto start를 WPF에서 수정/저장할 수 있습니다.

7. WPF 기본 학습 설정/제어가 들어갔습니다.
   - image size, batch, epoch, cfg, weight, validation percent, split seed를 WPF에서 수정할 수 있습니다.
   - dataset readiness 확인, 학습 시작/중지 명령 전송이 가능합니다.

8. 배치 추론은 worker 우선 경로를 사용합니다.
   - WPF batch는 TCP Python worker를 먼저 시도합니다.
   - 일반 batch 작업에서는 느린 smoke-process fallback을 반복하지 않고 실패 행으로 표시합니다.
   - smoke-process fallback은 YOLO 테스트/진단 흐름에서만 사용합니다.

9. WPF 버튼 아이콘을 적용했습니다.
   - `MahApps.Metro.IconPacks.Material`을 사용합니다.
   - 전체 Material 테마가 아니라 아이콘만 사용하는 방향입니다.

10. first-run 검증이 보강됐습니다.
    - `scripts\verify-first-run.ps1`은 기존 build/test/YOLO smoke를 확인합니다.
    - `-RunWpfSmoke` 옵션으로 WPF 창 실행까지 확인할 수 있습니다.

11. 검증된 명령
    - `dotnet build MvcVisionSystem.csproj -c Debug`
    - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
    - `powershell -ExecutionPolicy Bypass -File .\scripts\verify-first-run.ps1 -RunWpfSmoke`
    - `powershell -ExecutionPolicy Bypass -File .\scripts\verify-first-run.ps1 -SkipBuild -SkipTests -RunWpfSmoke`

12. WPF 다크/라이트 테마 전환 기반을 넣었습니다.
    - 상단 `테마` 버튼으로 다크/라이트 팔레트를 전환합니다.
    - 전체 Material 테마가 아니라 기존 WPF 셸 리소스 색상만 바꾸는 방식입니다.
    - 이미지 큐 표 헤더/행/선택색도 테마에 맞게 정리했습니다.

13. 공식 `WPF-UI` 라이브러리 적용을 시작했습니다.
    - `WPF-UI 4.3.0`을 WPF 셸의 Fluent 테마/컨트롤 기준 라이브러리로 추가했습니다.
    - 상단 `테마`, `샘플`, `ROI`, `저장`, `라벨링`, `추론 검토`, `YOLO`, 후보 확정/스킵 버튼은 `Wpf.Ui.Controls.Button`으로 전환했습니다.
    - YOLO 탭의 `첫 점검`, `설치`, `테스트`, `재시작`, `중지`, 모델 설정 저장/초기화, 학습 준비/시작/중지 버튼도 `Wpf.Ui.Controls.Button`으로 전환했습니다.
    - 좌측 이미지 큐의 root/folder/refresh/next/detect/batch/retry/stop compact 버튼도 `Wpf.Ui.Controls.Button` 기반으로 전환했습니다.
    - 주요 작업 버튼에는 작업자가 기능을 바로 이해할 수 있도록 짧은 툴팁을 추가했습니다.
    - WPF 셸 루트를 `Wpf.Ui.Controls.FluentWindow`로 전환하고 `Wpf.Ui.Controls.TitleBar`를 추가했습니다.
    - 다크 모드에서 WPF-UI toolbar button 배경/글자 대비를 조정하고 `artifacts/ui/wpf-ui-toolbar-dark.png`, `artifacts/ui/wpf-ui-toolbar-light.png`로 확인했습니다.
    - YOLO 탭 내부 버튼 배열은 `artifacts/ui/wpf-yolo-tab-wpfui-buttons-dark.png`로 확인했습니다.
    - 이미지 큐 compact 버튼 배열은 `artifacts/ui/wpf-image-queue-wpfui-buttons-dark.png`로 확인했습니다.
    - 기존 WinForms UI는 최종 제품 방향이 아니며, WPF 화면으로 계속 치환합니다.

14. AI 후보 리뷰 문구를 작업자가 바로 판단하기 쉽게 정리했습니다.
    - 후보 목록 좌표를 `크기 47x42 / 위치 x=30, y=32` 형식으로 표시합니다.
    - 후보 상세에는 신뢰도, 기준, 좌표, 상태를 분리해 표시합니다.
    - 빈 후보/필터 통과 후보 없음 상태를 한국어로 표시하고 후보 항목에는 상세 툴팁을 추가했습니다.
    - 실제 `AI 검출` 실행 후 `artifacts/ui/wpf-candidates-after-ai-detect.png`로 후보 표시를 확인했습니다.

15. WPF 셸 로그 영역을 `OpenVisionLab.Logging.Controls` DLL의 WPF 로그 패널로 교체했습니다.
    - 기존 단순 `TextBox` 로그 대신 `OpenVisionLab.Logging.Controls.View.LogPanelView`를 사용합니다.
    - `AppendLog`는 `OVLog.Write`로 연결해 기존 로그 DLL의 runtime stream, 파일 로그, 필터 UI를 함께 사용합니다.
    - 실제 화면은 `artifacts/ui/wpf-logpanel-dll-dark.png`로 확인했습니다.

16. WPF `Classes` 탭에 기본 클래스 편집 흐름을 추가했습니다.
    - 클래스 이름 입력, Enter/Add 추가, 선택 Delete 삭제를 지원합니다.
    - 기존 `ClassCatalogService`와 `SaveYoloDataYaml`, `SaveConfig` 흐름을 그대로 사용합니다.
    - 클래스 목록에는 라벨 색상 swatch를 같이 보여줍니다.
    - Add/Delete 버튼에는 Material 아이콘을 추가했습니다.
    - 실제 화면은 `artifacts/ui/wpf-classes-editor-dark.png`, `artifacts/ui/wpf-classes-editor-icons-dark.png`로 확인했습니다.

17. WPF 셸의 이미지 폴더 선택 대화상자에서 WinForms 의존을 제거했습니다.
    - `System.Windows.Forms.FolderBrowserDialog` 대신 WPF `Microsoft.Win32.OpenFolderDialog`를 사용합니다.
    - 이미지 root 선택 후 기존 Python model image root와 WPF image queue load 흐름은 그대로 유지합니다.

18. WPF `YOLO > Model Settings` 경로 입력 UX를 개선했습니다.
    - Python executable, YOLO project folder, client script, weights, image root에 browse 버튼을 추가했습니다.
    - 파일 선택은 WPF `OpenFileDialog`, 폴더 선택은 WPF `OpenFolderDialog`를 사용합니다.
    - 경로 선택 후 `저장`, `첫 점검`으로 이어지도록 YOLO command status에 다음 동작을 표시합니다.
    - 긴 경로 입력칸에는 전체 경로 툴팁을 추가했습니다.
    - 실제 화면은 `artifacts/ui/wpf-yolo-model-settings-browse-buttons.png`로 확인했습니다.

19. WPF YOLO/학습 숫자 입력칸 방어를 추가했습니다.
    - confidence는 소수 입력만, timeout/image size/batch/epoch/validation/split seed는 정수 입력만 받도록 했습니다.
    - 각 숫자 입력칸에 작업 범위 툴팁을 추가했습니다.
    - `WPF numeric editors declare input guards` 테스트로 XAML 연결 상태를 확인합니다.

20. WPF 이미지 큐 행 상태를 스캔하기 쉽게 개선했습니다.
    - 파일 컬럼에 상태 아이콘과 두 줄 요약을 추가했습니다.
    - 후보/확정/요청/실패/검출없음 상태를 색상 아이콘으로 구분합니다.
    - 실패 상태는 짧은 한국어 원인 요약과 상세 툴팁을 제공합니다.
    - 좁은 큐 폭에서도 `Size` 값이 보이도록 컬럼 폭을 재조정했습니다.
    - 실제 화면은 `artifacts/ui/wpf-image-queue-status-icons-window-dark.png`로 확인했습니다.

21. WPF 이미지 큐 모델을 셸 code-behind에서 분리했습니다.
    - `WpfImageQueueItem`, `WpfImageQueueFilterOption`, `WpfImageQueueFilter`, `WpfImageQueueDetail`을 `WpfImageQueueModels.cs`로 옮겼습니다.
    - XAML 바인딩과 테스트에서 쓰는 공개 타입 이름은 유지했습니다.
    - WPF 셸 본체는 큐 동작/이벤트에 집중하고, 행 표시 상태는 별도 모델 파일에서 관리합니다.

22. WPF 학습 진행 상태 표시를 보강했습니다.
    - worker가 보내는 training state/progress/message를 짧은 한 줄 summary로 표시합니다.
    - epoch 정보는 우측 보조 텍스트로 분리해 `Epoch 2/5`처럼 바로 보이게 했습니다.
    - 학습 명령 준비 중에는 진행 문구가 즉시 바뀌고, worker 상태가 들어오면 실제 진행률로 갱신됩니다.
    - `WPF training status summaries are operator-readable` 테스트로 표시 문구를 검증합니다.

23. WPF 학습 중 중복 명령 방지를 보강했습니다.
    - training command 실행 중에는 첫 점검, 검출, 학습 시작 등 충돌 가능한 명령을 비활성화합니다.
    - StopTraining은 학습 중에도 누를 수 있도록 별도 조건으로 유지합니다.
    - idle/completed/failed 상태에서는 StopTraining을 비활성화해 불필요한 중지 명령을 막습니다.
    - `WPF training command disables conflicting actions` 테스트로 버튼 상태를 검증합니다.

24. WPF Object Review의 현재 라벨 표시를 정리했습니다.
    - Objects 탭 상단에 현재 이미지 객체 수 summary를 추가했습니다.
    - 수동 ROI는 `Defect / 박스 / 크기 35x35 / 위치 x=36, y=36`처럼 크기와 위치 의미가 바로 보이는 형식으로 표시합니다.
    - 비어 있는 상태는 비활성 안내 행으로 표시합니다.
    - 각 라벨 행에는 출처/클래스/좌표 툴팁을 제공합니다.
    - `WPF object review summarizes current labels` 테스트로 표시 상태를 검증합니다.

25. WPF Object Review에서 현재 객체 삭제 흐름을 추가했습니다.
   - Objects 탭에서 수동 ROI 또는 확정된 AI 객체를 선택하면 Delete 버튼이 활성화됩니다.
   - `Delete`/`Backspace` 키로도 선택한 현재 객체를 삭제할 수 있습니다.
   - 삭제 후 목록 summary, 캔버스 ROI, 이미지 큐 상태를 즉시 갱신합니다.
   - idle/빈 상태에서는 Delete 버튼이 비활성화되어 잘못된 삭제를 막습니다.
   - `WPF object review summarizes current labels` 테스트에서 선택/삭제 흐름까지 검증합니다.

26. WPF Object Review에서 현재 객체 클래스 변경 흐름을 추가했습니다.
   - Objects 탭에서 수동 ROI 또는 확정된 AI 객체를 선택하면 클래스 콤보와 Apply 버튼으로 클래스를 바꿀 수 있습니다.
   - 수동 ROI도 객체별 클래스 이름을 따로 보관해 저장 시 변경한 클래스가 YOLO annotation에 반영됩니다.
   - 클래스 목록은 `Classes` 탭과 같은 `ClassCatalogService` 기준을 사용하며, 새 클래스 추가 후 Object Review 콤보도 동기화됩니다.
   - `WPF object review summarizes current labels` 테스트에서 클래스 변경, 저장용 ROI 반영, 삭제 흐름을 함께 검증합니다.

27. WPF AI 후보 목록을 더 빠르게 스캔할 수 있게 개선했습니다.
   - Candidates 탭 후보 행을 한 줄 문자열에서 아이콘 + 두 줄 정보 구조로 바꿨습니다.
   - confidence, bounds, 확정 가능/검토 필요 상태가 목록에서 바로 보입니다.
   - 상태 아이콘 색상은 확정 가능, 검토 필요, 이미지 밖 상태를 구분합니다.
   - 후보 상세 툴팁에는 현재 라벨과 가장 크게 겹치는 객체 및 IoU를 표시해 중복 확정 여부를 판단할 수 있게 했습니다.
   - `WPF candidate rows show visual review status` 테스트로 후보 행 구조와 상태 문구를 검증합니다.

28. WPF 시작 시 전체 이미지 상태 스캔을 막았습니다.
   - 앱 시작 샘플 로드는 이미지 큐 shell만 만들고 현재 이미지 1장의 상태만 갱신합니다.
   - 모든 이미지의 크기/라벨 상태 스캔은 사용자가 Root, Folder, Refresh를 눌렀을 때만 실행합니다.
   - 시작 시 전체 이미지 YOLO 추론은 실행하지 않습니다.
   - `WPF startup image load does not scan every queue image` 테스트로 시작 경로가 전체 큐를 훑지 않는지 검증합니다.

29. WPF 라벨링 모드와 추론 검토 모드를 분리했습니다.
   - 기본값은 라벨링 모드입니다.
   - 이미지 큐 클릭/이미지 로드는 이미지만 바꾸고 YOLO 추론을 실행하지 않습니다.
   - 현재 이미지 검사, 선택 이미지 검사, 배치 검사, 실패 재시도는 추론 검토 모드에서만 활성화됩니다.
   - 검사 버튼 핸들러에도 모드 방어 코드를 넣어 우회 호출되어도 자동 검사가 실행되지 않게 했습니다.
   - `WPF workflow mode separates labeling and inference` 테스트로 기본 모드, 이미지 로드, 추론 검토 모드 전환을 검증합니다.

30. WPF 이미지 큐 탐색과 추론 표시를 개선했습니다.
   - 이미지 큐 단일 클릭은 캔버스 로드 대신 왼쪽 미리보기만 갱신합니다.
   - 캔버스 로드는 더블클릭 또는 Open 버튼으로만 실행합니다.
   - 미리보기는 메모리 디코딩 방식이라 이미지 파일을 잠그지 않습니다.
   - pending AI 후보는 편집 ROI로 추가하지 않고, 캔버스의 읽기 전용 detection overlay로 표시합니다.
   - detection overlay에는 `AI 번호 / 클래스 / confidence` 라벨과 이중 박스가 표시되어 수동 ROI와 구분됩니다.
   - 캔버스 좌상단에 `AI 검출 결과` 패널을 띄워 현재 이미지, 후보 수, 기준 confidence, 선택 후보와 후보 요약을 바로 확인할 수 있게 했습니다.
   - 후보 목록에서 선택한 항목은 캔버스 overlay 색상/두께가 바뀌어 오른쪽 목록과 중앙 이미지가 서로 연결되어 보입니다.
   - OpenGL 캔버스 위에 WPF 요소를 겹치면 보이지 않는 문제가 있어, 결과 패널은 Canvas 헤더 아래의 별도 WPF 행에 배치했습니다.
   - 이미지 위 검출 마커는 두꺼운 전체 사각형 대신 코너 브래킷 + 선택 후보 라벨 배지 + 비선택 후보 번호 칩으로 정리했습니다.
   - `--wpf-visual-smoke` 테스트 모드는 실제 YOLO 샘플 이미지를 우선 사용하고, 없을 때만 합성 이미지를 fallback으로 사용합니다.
   - 실제 WPF 창을 띄워 `artifacts/ui/wpf-detection-overlay-visual-check.png` 캡처를 확인했습니다.
   - WPF 단일 이미지 로드 시 이전 이미지의 OpenGL texture group을 모두 지우고 새 이미지를 올리도록 변경해, 여러 이미지 텍스처가 뷰어에 겹쳐 남는 문제를 막았습니다.
   - 검출 overlay가 이미지 좌표계에서 줌 배율과 함께 커지며 라벨 배경/그림자가 뭉개지던 문제를 정리했습니다.
   - OpenGL은 유지하되 검출 박스, 코너 브래킷, 후보 배지는 화면 픽셀 좌표계에서 따로 그리도록 바꿔 확대 상태에서도 선 두께와 배지 크기가 일정합니다.
   - 확대/축소 시 검출 overlay 위치가 이미지와 따로 움직이지 않도록 이미지 픽셀 좌표를 실제 OpenGL texture bounds 기준으로 화면 좌표에 매핑합니다.
   - WPF 단일 이미지 추론 버튼도 batch와 같은 Python worker 우선 경로를 사용합니다.
   - WPF 시작 후 자동 worker warm-up은 하지 않고, 단일/선택/일괄 검출은 사용자가 명시적으로 누를 때만 실행합니다.
   - 일반 단일/선택/일괄 검출은 느린 smoke-process fallback을 타지 않으며, worker 실패 시 원인을 로그와 상태에 남깁니다.
   - 단일 추론 버튼은 interactive worker 연결 확인을 짧게 수행하고 elapsed time을 로그에 남깁니다.
   - `--wpf-visual-smoke --zoom-steps=3` 옵션으로 확대 상태의 검출 overlay 캡처를 만들 수 있습니다.
   - WPF/WinFormsHost 혼합 상태에서 백그라운드 추론 결과가 OpenGL `RefreshGL()`을 직접 건드리며 나던 cross-thread 예외를 막았습니다.
   - OpenGL refresh, reshape, texture update/delete는 실제 `openGLControl` 소유 스레드를 기준으로 실행합니다.
   - 선택 후보의 전체 사각형 outline을 제거하고 코너 브래킷 + 라벨 배지만 남겨 수동 ROI와 시각적으로 구분되게 했습니다.
   - 선택 후보 라벨 배지는 진한 클래스색 사각형 대신 반투명 어두운 결과 칩 + 컬러 스트립으로 바꿔, 확대 상태에서도 ROI 박스처럼 보이지 않게 정리했습니다.
   - 후보 코너 브래킷은 화면 좌표 기준의 얇은 선으로 유지하고, 선택/비선택 후보 모두 배율에 따라 선 두께가 커지지 않게 했습니다.
   - 상단 WinForms 툴바 테스트는 YOLO 연결 상태 갱신 타이밍에 따라 `YOLO/시작/대기/실행중/연결됨/오류`로 바뀔 수 있는 상태 버튼으로 검증하도록 안정화했습니다.
   - `WPF image queue click previews without loading canvas`, `WPF detection candidates render as detection overlays` 테스트로 검증합니다.
   - `WPF image load replaces previous viewer textures` 테스트로 서로 다른 이미지를 연속 로드해도 texture group이 하나만 남는지 검증합니다.
   - `OpenGL detection overlay screen bounds stay anchored to image pixels` 테스트로 detection overlay가 texture bounds 기준 이미지 픽셀 좌표에 고정되는지 검증합니다.
   - `OpenGL refresh marshals to the child control thread` 테스트로 cross-thread refresh 회귀를 검증합니다.
   - `WPF single detection uses worker warm-up and short interactive wait` 테스트로 단일 추론 latency 경로를 검증합니다.
   - real YOLO TCP smoke는 고정 `Teaching_0.bmp` 대신 실제 존재하는 샘플 이미지를 resolver로 찾아 사용하며, `Teaching_0.jpeg` 기준 후보/라벨 저장까지 통과했습니다.
   - `--wpf-visual-smoke --zoom-steps=3` 캡처로 확대 상태의 overlay 위치와 가독성을 직접 확인했습니다.

31. WPF Object Review의 AI 후보 비교 UX를 개선했습니다.
   - Candidates 탭에 선택 AI 후보와 현재 라벨을 나란히 보여주는 비교 패널을 추가했습니다.
   - 비교 패널은 AI 후보의 클래스/신뢰도/좌표, 가장 많이 겹치는 현재 라벨의 출처/클래스/좌표, IoU를 함께 표시합니다.
   - 후보 선택이 바뀌거나 후보가 없을 때 비교 패널 상태가 즉시 갱신됩니다.
   - 1차 캡처 자체평가에서 후보 리스트와 비교 패널이 같은 Grid row에 겹치는 문제가 발견되어, 전용 row를 추가하고 IoU 칸 폭을 넓혀 다시 캡처 확인했습니다.
   - IoU 50% 이상 후보는 `중복`으로 표시하고 IoU 칸/패널 테두리를 warning 색으로 강조해 중복 확정 위험을 바로 볼 수 있게 했습니다.
   - 고겹침 후보는 Candidates 목록에서도 warning 아이콘/색으로 표시해, 비교 패널을 보기 전에도 중복 가능성을 놓치지 않게 했습니다.
   - `WPF candidate rows show visual review status` 테스트에 비교 패널 표시, AI bounds, current label bounds, high-overlap flag, overlap ratio 검증을 추가했습니다.
   - `artifacts/ui/wpf-object-review-comparison-check.png` 캡처로 실제 화면에서 패널이 보이는지 확인했습니다.

32. WPF 이미지 큐 운영 상태 요약을 개선했습니다.
   - 하단 dataset status에 후보/실패/확정 개수를 함께 표시해 batch 추론 후 검토해야 할 행을 빠르게 파악할 수 있게 했습니다.
   - 상태 요약은 값이 있는 항목만 표시하므로 빈 큐나 정상 대기 상태에서는 불필요하게 길어지지 않습니다.
   - `WPF image queue presents row status with icons` 테스트에 queue review count summary 검증을 추가했습니다.
   - `artifacts/ui/wpf-queue-summary-check.png` 캡처로 하단 상태바 가독성을 확인했습니다.

33. WPF 작은 창 기준 상단 툴바와 후보 처리 UX를 다시 정리했습니다.
   - `--wpf-visual-smoke --width=1100 --height=720` 캡처에서 상단 오른쪽 버튼이 잘리는 문제를 확인했습니다.
   - 후보 확정/전체 확정/스킵은 상단에서 제거하고 Candidates 탭의 후보 비교 카드 안으로 옮겼습니다.
   - 후보가 보이면 라벨링/추론 검토 모드 상태와 관계없이 선택/전체/스킵 버튼을 바로 누를 수 있게 했습니다.
   - 후보 리스트와 버튼이 겹쳐 보이는 1차 배치 문제를 캡처로 확인한 뒤, 버튼을 비교 카드 내부로 다시 옮겨 정리했습니다.
   - 활성 모드 버튼은 비활성 회색 버튼처럼 보이지 않도록 선택 배경과 accent 테두리로 표시합니다.
   - 이미지 큐 검색창에는 검색 아이콘과 한국어 툴팁을 넣고, 큐 버튼 툴팁도 작업자 용어로 정리했습니다.
   - 오른쪽 객체 검토 탭/버튼/빈 상태 문구를 한국어로 맞춰 영어와 한국어가 섞여 보이는 부분을 줄였습니다.
   - WPF visual smoke 캡처 직전에 앱 창을 OS 전경/최상단으로 다시 올려, 외부 창이 캡처에 섞이지 않게 했습니다.
   - `WPF candidate rows show visual review status` 테스트에 후보 액션 버튼 활성 검증을 추가했습니다.
   - `artifacts/ui/wpf-small-window-check.png` 캡처로 상단 툴바, 후보 비교 카드, 후보 액션 버튼 배치를 확인했습니다.

34. WPF 이미지 큐에 빠른 상태 필터를 추가했습니다.
   - 상태 콤보만으로는 후보/실패/확정 행을 빠르게 보기 어려워, 왼쪽 큐에 `전체`, `후보`, `실패`, `확정` 빠른 필터 버튼을 추가했습니다.
   - 빠른 필터 버튼은 기존 상태 콤보와 같은 필터 상태를 공유하므로, 버튼과 콤보가 서로 다른 상태를 만들지 않습니다.
   - 후보/실패/확정 버튼에는 현재 개수를 함께 표시하고, 100개 이상은 `99+`로 줄여 작은 창에서도 버튼 폭이 터지지 않게 했습니다.
   - 빠른 필터 선택 시 하단 dataset status도 `필터 후보`, `필터 실패`처럼 현재 필터를 한국어로 표시합니다.
   - `WPF image queue presents row status with icons` 테스트에 빠른 필터 버튼 생성, 필터 전환, visible count/status 문구 검증을 추가했습니다.
   - `artifacts/ui/wpf-queue-quick-filter-check.png` 캡처로 1100x720 작은 창에서도 필터 버튼과 미리보기가 겹치지 않는지 확인했습니다.

35. YOLO 탭의 `Test` 버튼 모드 UX를 정리했습니다.
   - 현재 이미지/큐 검사는 계속 추론 검토 모드에서만 활성화하지만, YOLO 탭의 `Test`는 환경 확인용 명시 실행이므로 라벨링 모드에서도 누를 수 있게 했습니다.
   - `Test`를 누르면 자동으로 추론 검토 모드로 전환한 뒤 smoke 검사를 실행합니다.
   - `WPF workflow mode separates labeling and inference` 테스트에서 `Test` 버튼은 명시 실행으로 유지되고, 현재 이미지/큐 검사 버튼은 라벨링 모드에서 막히는지 검증합니다.
   - `artifacts/ui/wpf-yolo-tab-check.png` 캡처로 작은 창에서 `Test` 버튼이 회색 비활성처럼 보이지 않는지 확인했습니다.

36. WPF 라이트 테마 자체평가 경로를 추가하고 대비를 보정했습니다.
   - `--wpf-visual-smoke --theme=light` 옵션으로 라이트 테마 캡처를 직접 만들 수 있게 했습니다.
   - 라이트 테마 전환 후 로컬로 칠한 모드/필터 버튼이 다크 브러시를 계속 잡는 문제를 막기 위해 테마 적용 뒤 버튼 상태를 다시 계산합니다.
   - 활성 모드 버튼은 라이트/다크 모두 accent 배경과 흰 글자로 표시합니다.
   - 후보 비교 카드는 하드코딩된 다크 배경 대신 테마 `CanvasBrush`를 사용해 라이트 테마에서도 글자 대비가 유지됩니다.
   - `artifacts/ui/wpf-light-theme-check.png` 캡처로 1100x720 라이트 테마에서 상단 버튼, 큐 필터, 후보 비교 카드가 읽히는지 확인했습니다.

37. WPF 검사 버튼 비활성 안내 UX를 보강했습니다.
   - 비활성 버튼도 툴팁이 표시되도록 공통 버튼 스타일에 `ToolTipService.ShowOnDisabled`를 적용했습니다.
   - 라벨링 모드에서 현재 이미지/큐/배치 검사 버튼은 `추론 검토 모드`가 필요하다는 안내를 보여줍니다.
   - 추론 검토 모드로 전환하면 같은 버튼 툴팁이 실제 실행 동작으로 바뀝니다.
   - 하단 모드 상태 문구를 `모드: 라벨링`, `모드: 추론 검토`로 한글화했습니다.
   - `WPF workflow mode separates labeling and inference` 테스트에 비활성 버튼 안내와 모드 전환 후 안내 변경 검증을 추가했습니다.

38. WPF OpenGL 추론 overlay 배지 겹침을 줄였습니다.
   - 검출 후보 라벨 배지가 같은 화면 위치를 쓰면 아래쪽 빈 위치로 이동하도록 화면 좌표 기준 배치 함수를 추가했습니다.
   - 검출 박스 좌표는 건드리지 않고 라벨 배지만 이동하므로 확대/축소 시 박스 위치 안정성에는 영향을 주지 않습니다.
   - `WPF detection overlay badges avoid overlap` 테스트로 같은 위치의 후보 2개가 같은 배지 영역을 쓰지 않는지 검증합니다.

39. WPF 이미지 큐 배치 결과 요약을 확장했습니다.
   - 하단 dataset 상태 요약에 후보/실패/확정뿐 아니라 `스킵`, `검출없음` 개수도 표시합니다.
   - 빠른 버튼을 더 늘려 좌측 패널을 복잡하게 만들지 않고, 전체 결과 분포를 먼저 보여주는 방향으로 정리했습니다.
   - `WPF image queue presents row status with icons` 테스트에 스킵/검출없음 요약 검증을 추가했습니다.

40. WPF 학습 영역의 1차 한글화와 캡처 경로를 추가했습니다.
   - YOLO 탭 안의 학습 Expander, 주요 입력 라벨, `점검/시작/중지` 버튼, 학습 대기/진행/에폭 문구를 한글 기준으로 정리했습니다.
   - `--wpf-visual-smoke --review-tab=training`으로 학습 Expander를 펼친 캡처를 만들 수 있게 했습니다.
   - 설정용 ComboBox 템플릿을 보정해 다크 테마에서 `Cfg`, `가중치` 선택 박스가 밝게 뜨지 않도록 했습니다.
   - `WPF training status summaries`와 WPF shell 생성 테스트에 한글 버튼/상태 문구 검증을 반영했습니다.
   - `artifacts/ui/wpf-training-tab-check.png` 캡처로 1100x720 다크 테마에서 학습 입력/버튼/상태 문구가 읽히는지 확인했습니다.
   - `artifacts/ui/wpf-training-light-check.png` 캡처로 라이트 테마에서도 학습 입력과 ComboBox 대비를 확인했습니다.

41. README의 현재 WPF UI 안내를 갱신했습니다.
   - 이미지 큐 하단 상태바가 후보/실패/확정/스킵/검출없음 개수를 보여준다는 설명을 추가했습니다.
   - 학습 영역 안내를 현재 UI의 `학습`, `점검`, `시작`, `중지` 문구와 맞췄습니다.

42. WPF YOLO 탭의 주요 작업 버튼과 상태 문구를 한글화했습니다.
   - `First Check`, `Install`, `Test`, `Restart`, `Stop`, `Model Settings`, `Save`, `Defaults`를 각각 `첫 점검`, `설치`, `테스트`, `재시작`, `중지`, `모델 설정`, `저장`, `기본값`으로 맞췄습니다.
   - YOLO 명령 상태 문구도 `YOLO 명령 대기`, `YOLO 설정 준비 완료`, `설치 실패`처럼 화면에서 읽히는 문장은 한글 기준으로 정리했습니다.
   - README 처음 실행 안내도 새 버튼명과 맞췄습니다.

43. WPF visual smoke 캡처의 모드 일관성을 맞췄습니다.
   - 테스트용 AI 후보를 캔버스에 올리기 전에 `추론 검토` 모드를 먼저 선택해, 캡처 화면에서 라벨링 모드와 추론 결과가 섞여 보이지 않도록 했습니다.

44. WPF 셸의 고정 섹션 제목을 한글 기준으로 정리했습니다.
   - 상단 제목, 짧은 보조 문구, 이미지 큐, 캔버스, 로그, 이미지 큐 컬럼명, 배치 대기 문구를 한국어 UI 흐름에 맞췄습니다.

45. WPF 후보 검토 상세 문구를 한글화했습니다.
   - 후보 상세 tooltip의 `confidence`, `Bounds`, `Status`, `Current labels` 문구를 `신뢰도`, `좌표`, `상태`, `현재 라벨` 기준으로 정리했습니다.
   - 후보 비교 카드의 고중복 표시를 `High` 대신 `중복`으로 보여줍니다.

46. WPF 객체 검토 목록의 남은 영어 표시를 정리했습니다.
   - 수동 ROI summary의 `Manual`을 `수동`으로 바꿨습니다.
   - 객체 tooltip의 `Source`, `Class`, `Bounds`를 `출처`, `클래스`, `좌표` 기준으로 맞췄습니다.
   - 후보 중복 비교의 현재 라벨도 `수동 Defect`처럼 화면 언어와 동일하게 표시합니다.

47. WPF 화면/로그의 남은 주요 영어 표시를 한글 기준으로 정리했습니다.
   - 테마 버튼을 `테마: 다크/라이트`로 바꿨습니다.
   - YOLO 모델 설정의 `Confidence`, `Timeout`, `Project`, `Client`, `Weights`, `Images`, `Auto start`를 현재 화면 기준 문구로 바꿨습니다.
   - Python worker 준비, 단일/일괄 추론, 후보 로드/확정/스킵, 저장/라벨 경로 로그를 작업자가 읽기 쉬운 한글 문장으로 정리했습니다.
   - 학습 입력 tooltip, 상태바 dataset 요약, YOLO 패키지 점검 요약도 한글 기준으로 맞췄습니다.
   - 객체 탭 visual smoke가 빈 상태만 캡처하지 않도록 수동 ROI가 있는 상태를 검증하게 했습니다.

48. WPF 한글화 이후 YOLO TCP smoke를 재검증했습니다.
   - `scripts\smoke-yolo-tcp.ps1` 기준 실제 샘플 `Teaching_0.jpeg`에서 후보 1개, 첫 클래스 `OK`를 확인했습니다.
   - UI 문구 정리 후에도 Python TCP 추론 경로가 깨지지 않는 것을 확인했습니다.

49. GitHub checkout 후 실행 흐름을 다시 확인했습니다.
   - `scripts\verify-first-run.ps1 -SkipTests -SkipYoloSmoke -RunWpfSmoke`로 PowerShell script 문법, Debug build, WPF shell open smoke를 확인했습니다.
   - 빌드 경고 0개, 오류 0개로 통과했습니다.

50. WPF 객체 검토 탭의 첫 객체 선택 UX를 개선했습니다.
   - 객체 목록을 갱신할 때 기존 선택을 복원하고, 선택이 없으면 첫 객체를 자동 선택합니다.
   - 객체가 1개만 있을 때도 `삭제`, `적용` 버튼이 바로 활성화되어 사용자가 한 번 더 클릭하지 않아도 됩니다.
   - 객체 탭 visual smoke 캡처 직전 보조 창 정리 순서를 강화해, OpenVisionLab 보조 창이 캡처를 가리는 문제를 줄였습니다.
   - `artifacts\ui\wpf-object-autoselect-check2.png`로 실제 화면에서 첫 객체 선택과 버튼 활성화를 확인했습니다.

51. WPF 클래스 탭 안내 문구를 버튼명과 맞췄습니다.
   - `Add`로 남아 있던 안내 문구를 `추가`로 바꿨습니다.
   - `artifacts\ui\wpf-classes-tab-check2.png`로 실제 클래스 탭 화면을 확인했습니다.

52. WPF 이미지 큐 빠른 필터 가독성을 개선했습니다.
   - `후보1`, `실패0`, `확정0`처럼 붙어 보이던 빠른 필터 카운트를 `후보 1`, `실패 0`, `확정 0` 형식으로 바꿨습니다.
   - `artifacts\ui\wpf-queue-filter-spacing-check.png`로 실제 화면에서 좌측 필터 가독성을 확인했습니다.
   - visual smoke는 빌드 DLL 잠금을 피하기 위해 단독 실행하는 흐름으로 확인했습니다.

53. WPF 후보 탭의 확정 버튼 문구를 명확하게 바꿨습니다.
   - 후보 카드 버튼을 `선택`, `전체`, `스킵`에서 `확정`, `전체 확정`, `스킵`으로 바꿨습니다.
   - `artifacts\ui\wpf-candidate-actions-copy-check.png`로 버튼 폭과 문구가 깨지지 않는지 확인했습니다.

54. WPF 후보/객체/클래스 ListBox 선택 행 대비를 보강했습니다.
   - 공통 `ReviewListBoxItemStyle`을 추가해 선택 행 배경/테두리/foreground를 테마 리소스 기준으로 고정했습니다.
   - 후보 행 TextBlock의 직접 foreground 지정은 제거해 선택 foreground가 적용되도록 했습니다.
   - `artifacts\ui\wpf-light-candidate-selected-contrast2.png`로 라이트 테마 선택 후보 행이 구분되는지 확인했습니다.

55. WPF OpenGL overlay 확대 상태를 재확인했습니다.
   - `--zoom-steps=3` visual smoke로 확대 상태에서도 후보 박스와 배지가 이미지 픽셀 위치에 고정되는지 확인했습니다.
   - `artifacts\ui\wpf-overlay-zoom-pass2.png` 기준으로 overlay가 이미지와 따로 밀리는 증상은 보이지 않았습니다.

56. WPF 시작 시 자동 worker 예열을 제거했습니다.
   - 시작 시 샘플 이미지는 로드하되, Python worker/model 준비는 자동으로 돌리지 않게 했습니다.
   - 상태바와 로그에 `추론 실행 대기`, `추론은 사용자가 명시적으로 실행할 때만 시작합니다.`를 표시해 시작 직후 동작을 분명하게 했습니다.
   - `검출 시 자동 시작` 문구로 설정 의미를 바꿔, 앱 시작 옵션이 아니라 검출 요청 시 worker 자동 시작 옵션임을 구분했습니다.
   - 자동 예열 전용 함수와 timeout도 제거해 다시 시작 예열 흐름으로 돌아가지 않게 했습니다.

57. WPF 이미지 큐 미리보기 클릭 반응성을 개선했습니다.
   - 같은 이미지를 다시 클릭할 때 디스크에서 매번 다시 읽지 않도록 80개 제한 미리보기 캐시를 추가했습니다.
   - 리스트 선택은 계속 캔버스 로드/추론을 하지 않고 미리보기만 갱신합니다.

58. WPF AI 검출 overlay 선 렌더링을 다시 다듬었습니다.
   - OpenGL fractional line 대신 화면 픽셀에 스냅된 사각 스트립으로 후보 코너와 배지 테두리를 그립니다.
   - `artifacts\ui\wpf-overlay-pixel-strip-check.png`로 확대 상태에서 후보 박스, 배지, 우측 후보 리스트를 직접 확인했습니다.
   - 전체 테스트는 `artifacts\logs\tests-20260620-manual-inference-preview-overlay-rerun.log` 기준 통과했습니다.

59. WPF 일반 검출에서 느린 smoke fallback을 분리했습니다.
   - `현재 검사`, `선택 검사`, `일괄 검사`는 TCP worker 경로만 사용하고 worker 실패 시 빠르게 실패 상태를 표시합니다.
   - `YOLO 테스트` 버튼만 smoke-process fallback을 허용해 진단용 느린 경로와 실제 작업 경로를 분리했습니다.
   - 내부 메서드도 `RunInteractiveDetectionAsync`로 바꿔 일반 작업 검출과 smoke 진단을 구분했습니다.
   - 전체 테스트는 `artifacts\logs\tests-20260620-worker-only-normal-detection-pass.log` 기준 통과했습니다.
   - TCP YOLO smoke는 `artifacts\logs\smoke-yolo-tcp-20260620-worker-only-normal-detection.log` 기준 통과했습니다.
   - 첫 실행 WPF smoke는 `artifacts\logs\verify-first-run-20260620-no-startup-worker-wpf.log` 기준 통과했습니다.

60. WPF 상단 툴바의 모드/실행 구분을 개선했습니다.
   - 기본 작업, 모드 전환, YOLO/추론 실행 버튼 사이에 얇은 구분선을 넣었습니다.
   - `라벨 편집`은 `라벨링`, `AI 검출`은 `추론 검토`, 현재 이미지 실행 명령은 `현재 검사`로 바꿔 모드와 실행 명령을 더 분명하게 구분했습니다.
   - 상태바/로그/툴팁 문구도 `라벨링`, `추론 검토` 기준으로 맞췄습니다.
   - 전체 테스트는 `artifacts\logs\tests-20260620-toolbar-mode-copy-pass.log` 기준 통과했습니다.
   - `artifacts\ui\wpf-toolbar-mode-copy-1100.png`로 1100x720 작은 창에서도 상단 버튼이 잘리지 않는지 확인했습니다.
   - `artifacts\ui\wpf-toolbar-mode-copy-light-1100.png`로 라이트 테마에서도 버튼 대비와 폭을 확인했습니다.

61. WPF 이미지 큐 상세 갱신의 UI refresh 빈도를 줄였습니다.
   - 이미지 한 장 상태를 읽을 때마다 `imageQueueView.Refresh()`를 호출하던 흐름을 첫 장/20장/마지막 장 단위로 묶었습니다.
   - 행 데이터는 `INotifyPropertyChanged`로 갱신되므로 전체 view refresh 없이도 표시 텍스트가 바뀝니다.
   - 필터 반영과 상태 카운트는 묶음 refresh 시점에 맞춰 갱신해 좌측 리스트 선택/스크롤 중 끊김을 줄였습니다.
   - 전체 테스트는 `artifacts\logs\tests-20260620-queue-refresh-throttle-pass.log` 기준 통과했습니다.
   - `artifacts\ui\wpf-queue-refresh-throttle-check.png`로 좌측 큐와 미리보기 화면이 정상 표시되는지 확인했습니다.

62. WPF 첫 추론 worker 연결 대기를 현실적으로 조정했습니다.
   - 이미 Python worker가 연결되어 있으면 1.5초만 확인해 빠르게 추론으로 넘어갑니다.
   - worker가 아직 연결 전이면 설정된 검출 timeout의 절반을 사용하되 5~12초로 제한해 첫 클릭 실패를 줄였습니다.
   - 느린 smoke-process fallback은 계속 일반 검출에서 사용하지 않습니다.
   - 전체 테스트는 `artifacts\logs\tests-20260620-interactive-worker-timeout-pass.log` 기준 통과했습니다.
   - TCP YOLO smoke는 `artifacts\logs\smoke-yolo-tcp-20260620-interactive-worker-timeout.log` 기준 통과했습니다.

63. README의 WPF 실행/추론 설명을 현재 UI에 맞췄습니다.
   - `AI 검출`, 후보 `선택/전체`처럼 예전 버튼명을 `추론 검토`, `확정/전체 확정`으로 바꿨고, 현재 이미지 실행 명령은 `현재 검사`로 정리했습니다.
   - 시작 시 샘플 이미지만 로드하고 검출은 실행하지 않는다고 명시했습니다.
   - `자동 실행`은 앱 시작이 아니라 검출 요청 시 Python worker 자동 시작 설정임을 분명히 했습니다.

64. Debug publish 산출물 검증을 통과했습니다.
   - `scripts\publish-win-x64.ps1 -Configuration Debug`로 publish 산출물을 생성했습니다.
   - `MvcVisionSystem.exe`, `MvcVisionSystem.dll`, `OpenVisionLab.ImageCanvas.dll`, `SharpGL.dll`, `SharpGL.WinForms.dll` 필수 파일 검증을 통과했습니다.
   - `publish-manifest.txt`가 생성됐고, 산출물 텍스트 파일에 DEV 경로 문자열이 남지 않는지 확인했습니다.
   - 검증 로그는 `artifacts\logs\publish-debug-20260620-wpf-worker-ui.log`입니다.

65. Release publish 산출물 검증을 통과했습니다.
   - `scripts\publish-win-x64.ps1 -Configuration Release`로 실제 배포 구성을 검증했습니다.
   - 필수 실행 파일/DLL, manifest 생성, DEV 경로 문자열 검사를 통과했습니다.
   - `Lib.Common` 외부 프로젝트의 기존 미사용 변수 경고 5개가 있었지만 publish exit code는 0입니다.
   - 검증 로그는 `artifacts\logs\publish-release-20260620-wpf-worker-ui.log`입니다.
   - Release publish exe가 실제 WPF 셸을 여는지도 `artifacts\logs\publish-release-wpf-open-20260620.log`로 확인했습니다.

66. first-run 검증에 runtime config 경로 점검을 추가했습니다.
   - `verify-first-run.ps1`이 빌드 전에 Python 실행 경로, YOLO project root, client script, `best.pt`, image root, 샘플 이미지를 확인합니다.
   - 빠른 검증은 `artifacts\logs\verify-first-run-runtime-config-20260620.log` 기준 통과했습니다.
   - WPF smoke 포함 검증은 `artifacts\logs\verify-first-run-20260620-runtime-config-wpf.log` 기준 통과했습니다.
   - README의 first-run 설명도 runtime config 검증을 포함하도록 갱신했습니다.

67. first-run 스크립트에 publish WPF smoke 옵션을 추가했습니다.
   - `verify-first-run.ps1 -RunPublishWpfSmoke`로 config의 publish executable을 실제로 열어 볼 수 있습니다.
   - `artifacts\logs\verify-first-run-publish-wpf-smoke-20260620.log` 기준 Release publish WPF 셸 열기 검증을 통과했습니다.
   - README에 publish 후 실행 예시를 추가했습니다.

68. 실제 YOLO C# workflow smoke를 재검증했습니다.
   - `scripts\smoke-yolo-workflow.ps1`가 빌드 후 `Real YOLO TCP workflow detects, overlays, confirms, and saves labels`를 통과했습니다.
   - 검증 로그는 `artifacts\logs\smoke-yolo-workflow-20260620-wpf-worker-ui.log`입니다.

69. WPF 후보 중복 확정 방지 UX를 보강했습니다.
   - 기존 수동 라벨과 50% 이상 겹치는 AI 후보는 선택 확정에서 제외하고, 전체 확정에서도 자동으로 건너뛰도록 했습니다.
   - 후보 상세, 우측 비교 카드, 캔버스 결과 요약이 모두 `중복 가능` 상태를 경고 톤으로 보여주도록 정리했습니다.
   - 선택 후보가 중복일 때 `확정` 버튼은 비활성화되고, `스킵` 또는 다른 확정 가능 후보 처리 흐름만 남도록 했습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-duplicate-candidate-guard-warning.log`입니다.
   - 실제 WPF 캡처 검증은 `artifacts\ui\wpf-duplicate-candidate-guard-warning.png`, 로그는 `artifacts\logs\visual-20260620-duplicate-candidate-guard-warning.log`입니다.

70. OpenGL 추론 overlay의 오프스크린 배지 표시를 방지했습니다.
   - 확대/이동 중 후보 박스가 화면 밖으로 완전히 나갔는데 라벨 배지만 남는 상황을 막기 위해 viewport 교차 판정을 추가했습니다.
   - 화면에 일부라도 보이는 후보는 기존처럼 유지하고, 경계선만 닿거나 완전히 밖인 후보는 그리지 않습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-overlay-viewport-guard.log`입니다.
   - 확대 화면 캡처 검증은 `artifacts\ui\wpf-zoom-overlay-viewport-guard.png`, 로그는 `artifacts\logs\visual-20260620-zoom-overlay-viewport-guard.log`입니다.

71. WPF 이미지 큐 미리보기의 클릭 체감 속도를 보강했습니다.
   - 큐 항목을 빠르게 훑을 때 불필요한 썸네일 디코드가 바로 시작되지 않도록 120ms 디바운스를 추가했습니다.
   - 썸네일 생성은 `ReadAllBytes` 대신 파일 스트림 기반으로 바꿔 큰 이미지에서 메모리 부담을 줄였습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-queue-preview-debounce.log`입니다.
   - WPF 화면 캡처 검증은 `artifacts\ui\wpf-queue-preview-debounce-check.png`, 로그는 `artifacts\logs\visual-20260620-queue-preview-debounce.log`입니다.

72. 추론 실행 중 상태 피드백을 세분화했습니다.
   - 단일 추론은 `추론 준비 중`, `worker 연결 확인 중`, `AI 추론 요청 중`, `추론 완료/실패 + elapsed` 순서로 상태가 바뀌도록 했습니다.
   - 배치 추론도 시작/완료/중지 상태를 YOLO 명령 상태에 남기고 busy 상태가 남지 않도록 정리했습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-inference-status-feedback.log`입니다.
   - YOLO 탭 화면 캡처 검증은 `artifacts\ui\wpf-inference-status-feedback-check.png`, 로그는 `artifacts\logs\visual-20260620-inference-status-feedback.log`입니다.

73. 상태 피드백 변경 후 실제 YOLO workflow smoke를 재검증했습니다.
   - `scripts\smoke-yolo-workflow.ps1` 기준 빌드 경고 0개, 오류 0개로 통과했습니다.
   - 결과는 `PASS Real YOLO TCP workflow detects, overlays, confirms, and saves labels`입니다.
   - 검증 로그는 `artifacts\logs\smoke-yolo-workflow-20260620-after-status-feedback.log`입니다.

74. WPF visual smoke 종료 후 빌드 잠금 가능성을 줄였습니다.
   - visual smoke 전용 경로에서 캡처 후 WPF `Application`을 명시적으로 종료하도록 정리했습니다.
   - smoke 직후 남은 `dotnet` 프로세스를 확인했고, MSBuild nodeReuse 빌드 서버는 `dotnet build-server shutdown`으로 정리했습니다.
   - WPF 캡처 검증은 `artifacts\ui\wpf-visual-smoke-shutdown-check.png`, 로그는 `artifacts\logs\visual-20260620-smoke-shutdown-check.log`입니다.
   - 빌드 서버 종료 로그는 `artifacts\logs\dotnet-build-server-shutdown-20260620-visual-smoke.log`입니다.

75. UX 개선 루프 후 Release publish와 first-run publish smoke를 재검증했습니다.
   - `scripts\publish-win-x64.ps1 -Configuration Release`가 성공했고, publish manifest 생성 및 DEV 경로 검출 검사를 통과했습니다.
   - `verify-first-run.ps1 -SkipBuild -SkipTests -SkipYoloSmoke -RunPublishWpfSmoke`가 runtime config와 publish WPF shell open을 통과했습니다.
   - Release publish 로그는 `artifacts\logs\publish-release-20260620-after-ux-loops.log`입니다.
   - first-run publish smoke 로그는 `artifacts\logs\verify-first-run-publish-wpf-smoke-20260620-after-ux-loops.log`입니다.

76. 추론 상태 피드백 회귀 방지 테스트를 추가했습니다.
   - 단일 추론의 worker 연결 대기, AI 요청 중, 후보 수 포함 완료 문구가 빠지지 않도록 소스 검증을 보강했습니다.
   - 배치 추론 완료 시 busy 명령 상태를 해제하는 문구도 테스트로 고정했습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-inference-status-regression-assertions.log`입니다.

77. WPF 수동 smoke 체크리스트를 추가했습니다.
   - 사람이 직접 UI를 볼 때 확인할 시작, 큐 미리보기, 추론 상태, 중복 후보, 확대/축소 overlay, 저장, YOLO 탭 흐름을 한 문서에 정리했습니다.
   - 문서는 `docs\WPF_MANUAL_SMOKE_CHECKLIST.md`입니다.

78. README에서 WPF 수동 smoke 체크리스트 위치를 바로 찾을 수 있게 연결했습니다.
   - first-run/publish smoke 안내 아래에 `docs\WPF_MANUAL_SMOKE_CHECKLIST.md` 참조를 추가했습니다.

79. runtime example config를 형제 `yolov5` 저장소 기준으로 portable하게 바꿨습니다.
   - `config\labeling-runtime.example.json`의 YOLO 경로를 `C:/Git/yolov5` 고정값에서 `${repoParent}/yolov5` 토큰 기준으로 변경했습니다.
   - `start-labeling-workbench.ps1`와 `verify-first-run.ps1`가 `${repoRoot}`, `${repoParent}` 토큰을 해석하도록 했습니다.
   - 두 저장소를 같은 부모 폴더에 clone하면 example config를 그대로 쓸 수 있고, 개인 경로는 기존처럼 `labeling-runtime.local.json`으로 덮어씁니다.
   - 빠른 first-run 검증 로그는 `artifacts\logs\verify-first-run-token-config-syntax-20260620.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-portable-runtime-config.log`입니다.

80. WPF 배치 추론에서 매 이미지 OpenGL 캔버스 로드를 피하도록 개선했습니다.
   - 단일 추론은 기존처럼 캔버스에 이미지를 로드하고 overlay를 표시합니다.
   - 배치 추론은 이미지 크기만 읽고 `DetectImage` 경로 기반 요청으로 보내며, 결과는 큐 상태 갱신용 후보 이벤트로 받습니다.
   - 캔버스 이미지가 없는 path-only 결과도 timeout으로 빠지지 않고 완료 이벤트와 후보 목록을 남기도록 `DetectionResultApplicationService`를 보강했습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-path-based-batch-detection.log`입니다.
   - 실제 YOLO workflow 회귀 검증 로그는 `artifacts\logs\smoke-yolo-workflow-20260620-path-based-batch.log`입니다.

81. 실제 Python TCP smoke에서 `DetectImage` 경로 기반 요청도 검증하도록 넓혔습니다.
   - `scripts\smoke-yolo-tcp.ps1 -UseDetectImage`는 이미지 파일 경로를 보내고 Python의 `DetectImageResult` 후보 응답을 확인합니다.
   - 기존 `StartDefect` PNG 전송 smoke도 그대로 통과해 단일 검출 회귀를 같이 확인했습니다.
   - `DetectImage` 검증 로그는 `artifacts\logs\smoke-yolo-tcp-detect-image-20260620.log`입니다.
   - `StartDefect` 검증 로그는 `artifacts\logs\smoke-yolo-tcp-start-defect-20260620.log`입니다.

82. OpenGL 검출 overlay와 텍스처 로드 안정성을 보강했습니다.
   - `Refresh()` 직접 호출을 안전한 repaint 요청으로 바꿔 WPF 호스트 수명/resize 타이밍에서 예외가 튀는 가능성을 줄였습니다.
   - 0 크기 resize 상태에서 줌/좌표 계산을 건너뛰고, 최소 control size를 1 이상으로 보정했습니다.
   - 검출 overlay와 화면 텍스트 드로잉은 GL matrix/attrib 상태를 `finally`에서 복구하도록 바꿨습니다.
   - 이미지 파일 로더는 외부 `DataPointer`를 감싼 Mat을 반환하지 않고, alpha PNG는 3채널 Mat으로 변환합니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-opengl-stability.log`입니다.
   - 최종 자동 테스트 로그는 `artifacts\logs\tests-20260620-opengl-stability-after-visual-tweak.log`입니다.
   - 실제 YOLO workflow 로그는 `artifacts\logs\smoke-yolo-workflow-20260620-opengl-stability-after-visual-tweak.log`입니다.
   - WPF smoke 로그는 `artifacts\logs\verify-first-run-wpf-smoke-20260620-opengl-stability.log`입니다.
   - 확대 상태 WPF 캡처는 `artifacts\ui\wpf-opengl-stability-check.png`입니다.

83. WPF 배치 추론 로그에 이미지별 소요 시간을 추가했습니다.
   - 각 이미지가 끝날 때 `일괄 검사 항목 완료/실패`, 진행 번호, 파일명, 후보 수, elapsed가 로그에 남습니다.
   - 배치 진행 중 상태바에는 최근 처리 이미지의 elapsed가 같이 표시됩니다.
   - 배치 완료/중지 로그에는 전체 시간과 평균 처리 시간이 남습니다.
   - 각 이미지 시작 시 `요청중` 상태는 UI에만 반영하고, 디스크 저장은 최종 후보/실패/검출없음 결과에서만 수행하도록 줄였습니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-batch-timing-save-throttle.log`입니다.

84. Python TCP smoke에 연결 유지 반복 추론 검증을 추가했습니다.
   - `scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3`은 같은 Python worker 연결에서 `DetectImage` 요청 3개를 연속으로 보내고 `DetectImageResult` 3개를 확인합니다.
   - 반복 추론 검증 결과는 `artifacts\python-smoke-detect-image-repeat\yolo-tcp-summary.json` 기준 `requestCount: 3`, `resultCount: 3`, `detectionCount: 3`, 첫 클래스 `OK`입니다.
   - 반복 검증 로그는 `artifacts\logs\smoke-yolo-tcp-detect-image-repeat-20260620-after-parser.log`입니다.
   - 기존 단일 `StartDefect`와 단일 `DetectImage` 검증도 각각 `artifacts\logs\smoke-yolo-tcp-start-defect-20260620-after-repeat.log`, `artifacts\logs\smoke-yolo-tcp-detect-image-20260620-after-repeat.log` 기준 다시 통과했습니다.

85. WPF 배치 추론 중 리스트/디스크 갱신 빈도를 줄였습니다.
   - 배치 중에는 row 속성 갱신만 즉시 반영하고, 전체 `ICollectionView.Refresh()`는 배치 종료 시점으로 미뤘습니다.
   - `review-status.json` 저장은 매 이미지마다 하지 않고 10개 단위와 배치 종료 시점에 묶어서 저장합니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-batch-ui-throttle.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-batch-ui-throttle.log`입니다.
   - 실제 YOLO workflow 로그는 `artifacts\logs\smoke-yolo-workflow-20260620-batch-ui-throttle.log`입니다.
   - WPF 화면 캡처 확인은 `artifacts\ui\wpf-batch-ui-throttle-visual-check.png`, 로그는 `artifacts\logs\visual-20260620-batch-ui-throttle.log`입니다.

86. OpenGL 검출 후보 overlay 표현을 한 번 더 다듬었습니다.
   - 후보 박스에 옅은 내부 tint와 얇은 전체 outline을 추가하고, 코너 강조선과 배지 border alpha를 조정했습니다.
   - 픽셀 확인을 방해하지 않도록 선택 후보 내부 tint는 낮은 alpha로 제한했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-overlay-polish-final-retry.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-overlay-polish-final.log`입니다.
   - 최종 WPF 캡처는 `artifacts\ui\wpf-overlay-polish-final-check.png`, 로그는 `artifacts\logs\visual-20260620-overlay-polish-final-retry.log`입니다.

87. WPF 이미지 큐 클릭 미리보기 체감을 개선했습니다.
   - 새 이미지 미리보기 debounce를 120ms에서 60ms로 줄였습니다.
   - 미리보기 decode는 background에서 유지하되, cache 갱신은 UI continuation에서 처리해 background thread의 `Dispatcher.Invoke` 대기를 없앴습니다.
   - 같은 이미지를 다시 선택하면 미리보기 decode를 반복하지 않고 현재 preview를 유지합니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-queue-preview-fast.log`입니다.
   - 자동 테스트는 `artifacts\logs\tests-20260620-overlay-compact-label-rebuilt.log` 기준 preview assertion까지 포함해 통과했습니다.
   - WPF 캡처는 `artifacts\ui\wpf-queue-preview-fast-check.png`, 로그는 `artifacts\logs\visual-20260620-queue-preview-fast.log`입니다.

88. 중간 통합 검증을 완료했습니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-final-loop.log` 기준 통과했습니다.
   - 실제 YOLO workflow 검증은 `artifacts\logs\smoke-yolo-workflow-20260620-final-loop.log` 기준 통과했습니다.
   - Python worker 연결 유지 반복 추론 검증은 `artifacts\logs\smoke-yolo-tcp-detect-image-repeat-20260620-final.log` 기준 통과했습니다.

89. 비선택 AI 후보 overlay 배지에 클래스가 보이도록 바꿨습니다.
   - 기존 작은 후보 배지는 번호만 보일 수 있어 `#2 NG`처럼 후보 번호와 클래스를 같이 표시합니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-overlay-compact-label.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-overlay-compact-label-rebuilt.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-overlay-compact-label-check.png`, 로그는 `artifacts\logs\visual-20260620-overlay-compact-label.log`입니다.

90. WPF 상단 현재 이미지 실행 버튼을 `현재 검사`로 정리했습니다.
   - 모드 버튼 `추론 검토`와 실행 버튼이 헷갈리지 않도록 `현재 추론` 문구를 `현재 검사`로 바꿨습니다.
   - 실행 버튼 아이콘도 모드 버튼과 같은 로봇 아이콘 대신 `ImageSearch`로 바꿨습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-current-inspect-button.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-current-inspect-button-retry.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-current-inspect-button-check.png`, 로그는 `artifacts\logs\visual-20260620-current-inspect-button.log`입니다.

91. UI polish 이후 최종 통합 검증을 다시 완료했습니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-final-after-ui-polish.log` 기준 통과했습니다.
   - 실제 YOLO workflow 검증은 `artifacts\logs\smoke-yolo-workflow-20260620-final-after-ui-polish.log` 기준 통과했습니다.
   - Python worker 연결 유지 반복 추론 검증은 `artifacts\logs\smoke-yolo-tcp-detect-image-repeat-20260620-final-after-ui-polish.log` 기준 통과했습니다.

92. WPF 이미지 큐 row에 상태 배지를 추가했습니다.
   - 파일명 옆에 `후보 2`, `실패`, `요청중`, `확정`, `스킵`, `검출없음` 같은 작은 배지를 표시합니다.
   - 기존 상태 아이콘 색을 배지 border/text에도 사용해 긴 queue에서 상태를 더 빨리 훑을 수 있게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-queue-status-badge.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-queue-status-badge-retry.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-queue-status-badge-check.png`, 로그는 `artifacts\logs\visual-20260620-queue-status-badge.log`입니다.

93. WPF 이미지 큐 빠른 필터를 스킵/검출없음까지 확장했습니다.
   - 빠른 필터 줄에 `스킵`, `없음` 버튼을 추가했습니다.
   - 좌측 300px 폭 안에 들어가도록 quick filter 버튼 폭과 글자 크기를 줄였습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-queue-filter-badges.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-queue-filter-badges-retry.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-queue-filter-badges-check.png`, 로그는 `artifacts\logs\visual-20260620-queue-filter-badges.log`입니다.

94. WPF 이미지 큐 표시 로직을 presenter로 분리했습니다.
   - `WpfImageQueuePresenter`를 추가해 라벨/AI 상태 문구, 아이콘, 색상, 배지, row 요약, 툴팁 detail 조립을 WPF 셸 밖으로 옮겼습니다.
   - 기존 `WpfLabelingShellWindow` private helper 이름은 얇은 wrapper로 남겨 기존 테스트와 호출 흐름을 유지했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-image-queue-presenter.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-image-queue-presenter.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-image-queue-presenter.log` 기준 통과했습니다.

95. WPF 이미지 큐 상세 로딩을 loader로 분리했습니다.
   - `WpfImageQueueDetailLoader`를 추가해 이미지 크기 읽기, row dimension 문구, review status 갱신을 WPF 셸 밖으로 옮겼습니다.
   - 기존 private helper는 wrapper로 남겨 기존 호출 흐름과 reflection 테스트 호환성을 유지했습니다.
   - 실제 상세 로딩은 `AppImageLoader.LoadBitmap` 경로를 유지해 이전의 locked-file/느린 `Image.FromFile` 경로로 돌아가지 않게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-image-queue-detail-loader.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-image-queue-detail-loader.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-image-queue-detail-loader-check.png`, 로그는 `artifacts\logs\visual-20260620-image-queue-detail-loader.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-image-queue-detail-loader.log` 기준 통과했습니다.

96. WPF 이미지 큐 필터/상태 계산을 service로 분리했습니다.
   - `WpfImageQueueFilterService`를 추가해 검색어 필터, review state 필터, quick filter count, 데이터셋 상태바 문구 생성을 WPF 셸 밖으로 옮겼습니다.
   - WPF 셸은 선택된 필터를 읽고 버튼 색상만 갱신하는 쪽으로 얇아졌습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-image-queue-filter-service.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-image-queue-filter-service.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-image-queue-filter-service-check.png`, 로그는 `artifacts\logs\visual-20260620-image-queue-filter-service.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-image-queue-filter-service.log` 기준 통과했습니다.

97. WPF 이미지 큐를 `UserControl`로 분리했습니다.
   - `WpfImageQueuePanel.xaml`과 code-behind를 추가해 좌측 이미지 큐 UI, 빠른 필터, 미리보기, batch 버튼, DataGrid를 WPF 셸 XAML 밖으로 옮겼습니다.
   - 기존 셸 로직은 `ImageQueuePanelControl` 이벤트에 연결하고, 기존 테스트가 쓰던 `FindName("ImageQueueGrid")` 이름은 셸 namescope에 다시 등록했습니다.
   - UserControl 이동 후 비활성 queue 버튼이 밝게 뜨는 문제를 disabled style로 보정했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-image-queue-usercontrol-style.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-image-queue-usercontrol-final.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-image-queue-usercontrol-style-check.png`, 로그는 `artifacts\logs\visual-20260620-image-queue-usercontrol-style.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-image-queue-usercontrol.log` 기준 통과했습니다.

98. WPF 중앙 캔버스 영역을 `UserControl`로 분리했습니다.
   - `WpfCanvasPanel.xaml`과 code-behind를 추가해 캔버스 header, AI 검출 결과 overlay, `RoiImageCanvasView` host를 WPF 셸 XAML 밖으로 옮겼습니다.
   - 셸은 `CanvasPanelControl` proxy를 통해 기존 `MainCanvasView`, `DetectionResultOverlay`, `DetectionOverlaySummaryText` 이름을 계속 사용합니다.
   - 기존 테스트가 쓰던 overlay 이름은 셸 namescope에 다시 등록했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-canvas-usercontrol.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-canvas-usercontrol.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-canvas-usercontrol-check.png`, 로그는 `artifacts\logs\visual-20260620-canvas-usercontrol.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-canvas-usercontrol.log` 기준 통과했습니다.

99. WPF 현재 객체 검토 탭을 `UserControl`로 분리했습니다.
   - `WpfObjectReviewPanel.xaml`과 code-behind를 추가해 현재 라벨 객체 요약, 클래스 변경, 삭제, 객체 목록 UI를 WPF 셸 XAML 밖으로 옮겼습니다.
   - 셸은 `ObjectReviewPanelControl` 이벤트를 기존 객체 검토 핸들러로 전달하고, 기존 `FindName("ObjectListBox")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-object-review-usercontrol.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-object-review-usercontrol-retry.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-object-review-usercontrol-check.png`, 로그는 `artifacts\logs\visual-20260620-object-review-usercontrol.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-object-review-usercontrol.log` 기준 통과했습니다.

100. WPF View/ViewModel 네이밍과 폴더 구조를 정리했습니다.
   - WPF 화면 파일은 `0. UI\9) WPF\Views` 아래로 이동했습니다.
   - ViewModel 파일은 `0. UI\9) WPF\ViewModels` 아래에 두고 모두 `ViewModel` suffix를 붙였습니다.
   - `WpfLabelingShellWindow`, `WpfImageQueuePanel`, `WpfCanvasPanel`, `WpfObjectReviewPanel`은 각각 대응되는 `...ViewModel` 공개 속성을 가집니다.
   - 이미지 큐 모델은 `Models`, 큐 표시/상세/필터 서비스는 `Services`, WinForms 호환 canvas host는 `Interop`로 분리했습니다.
   - 테스트는 이동된 `Views` 경로와 각 ViewModel 생성 여부를 확인하도록 갱신했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-wpf-viewmodel-folders.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-wpf-viewmodel-folders.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-viewmodel-folders-check.png`, 로그는 `artifacts\logs\visual-20260620-wpf-viewmodel-folders.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-wpf-viewmodel-folders.log` 기준 통과했습니다.

101. WPF AI 후보 검토 탭을 `UserControl`로 분리했습니다.
   - `WpfCandidateReviewPanel.xaml`과 code-behind를 추가해 신뢰도 슬라이더, 후보 상세, 현재 라벨 비교 카드, 확정/전체 확정/스킵 버튼, 후보 목록 UI를 WPF 셸 XAML 밖으로 옮겼습니다.
   - `WpfCandidateReviewPanelViewModel`을 추가하고 `WpfCandidateReviewPanel.ViewModel`로 연결했습니다.
   - 셸은 `CandidateReviewPanelControl` 이벤트를 기존 후보 검토 핸들러로 전달하고, 기존 `FindName("CandidateListBox")`, `FindName("ConfirmSelectedCandidateButton")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - 후보 패널 내부 style은 부모 Window `StaticResource`에 의존하지 않도록 UserControl 내부 리소스로 분리했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-candidate-review-usercontrol-retry.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-candidate-review-usercontrol-retry.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-candidate-review-usercontrol-check.png`, 로그는 `artifacts\logs\visual-20260620-candidate-review-usercontrol.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-candidate-review-usercontrol.log` 기준 통과했습니다.

102. WPF 클래스 탭을 `UserControl`로 분리했습니다.
   - `WpfClassCatalogPanel.xaml`과 code-behind를 추가해 클래스 이름 입력, 추가/삭제 버튼, 상태 문구, 클래스 목록 UI를 WPF 셸 XAML 밖으로 옮겼습니다.
   - `WpfClassCatalogPanelViewModel`을 추가하고 `WpfClassCatalogPanel.ViewModel`로 연결했습니다.
   - 셸은 `ClassCatalogPanelControl` 이벤트를 기존 클래스 추가/삭제/선택 핸들러로 전달하고, 기존 `FindName("ClassNameBox")`, `FindName("ClassListBox")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - 클래스 패널 내부 style은 부모 Window `StaticResource`에 의존하지 않도록 UserControl 내부 리소스로 분리했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-class-catalog-usercontrol.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-class-catalog-usercontrol.log`입니다.
   - 기본 WPF 캡처는 `artifacts\ui\wpf-class-catalog-usercontrol-check.png`, 클래스 탭 직접 캡처는 `artifacts\ui\wpf-class-catalog-tab-check.png`입니다.
   - WPF visual 로그는 `artifacts\logs\visual-20260620-class-catalog-usercontrol.log`, 클래스 탭 직접 로그는 `artifacts\logs\visual-20260620-class-catalog-tab.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-class-catalog-usercontrol.log` 기준 통과했습니다.

103. WPF YOLO 상태/명령 영역을 `UserControl`로 분리했습니다.
   - `WpfYoloStatusPanel.xaml`과 code-behind를 추가해 YOLO 설정 요약/상세, 첫 점검, 설치, 테스트, 재시작, 중지, 명령 상태, 진행바 UI를 WPF 셸 XAML 밖으로 옮겼습니다.
   - `WpfYoloStatusPanelViewModel`을 추가하고 `WpfYoloStatusPanel.ViewModel`로 연결했습니다.
   - 셸은 `YoloStatusPanelControl` 이벤트를 기존 YOLO 점검/설치/테스트/worker 제어 핸들러로 전달하고, 기존 `FindName("FirstCheckYoloButton")`, `FindName("YoloCommandStatusText")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - YOLO 상태 패널 내부 style은 부모 Window `StaticResource`에 의존하지 않도록 UserControl 내부 리소스로 분리했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-yolo-status-usercontrol.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-yolo-status-usercontrol.log`입니다.
   - YOLO 탭 직접 WPF 캡처는 `artifacts\ui\wpf-yolo-status-usercontrol-check.png`, 로그는 `artifacts\logs\visual-20260620-yolo-status-usercontrol.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-yolo-status-usercontrol.log` 기준 통과했습니다.

104. WPF YOLO 모델 설정 영역을 `UserControl`로 분리했습니다.
   - `WpfYoloModelSettingsPanel.xaml`과 code-behind를 추가해 Python/프로젝트/클라이언트/가중치/이미지 경로, 신뢰도, 시간 제한, 자동 시작, 저장/기본값 UI를 WPF 셸 XAML 밖으로 옮겼습니다.
   - `WpfYoloModelSettingsPanelViewModel`을 추가하고 `WpfYoloModelSettingsPanel.ViewModel`로 연결했습니다.
   - 셸은 `YoloModelSettingsPanelControl` 이벤트를 기존 경로 선택/저장/기본값/숫자 입력 핸들러로 전달하고, 기존 `FindName("YoloProjectRootBox")`, `FindName("SaveYoloSettingsButton")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - 모델 설정 패널 내부 style은 부모 Window `StaticResource`에 의존하지 않도록 UserControl 내부 리소스로 분리했습니다.
   - visual smoke에 `--review-tab=model` 옵션을 추가해 모델 설정 Expander를 직접 펼쳐 캡처할 수 있게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-yolo-model-settings-usercontrol-retry.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-yolo-model-settings-usercontrol-retry.log`입니다.
   - 모델 설정 확장 캡처는 `artifacts\ui\wpf-yolo-model-settings-expanded-check.png`, 로그는 `artifacts\logs\visual-20260620-yolo-model-settings-expanded.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260620-yolo-model-settings-usercontrol.log` 기준 통과했습니다.

105. WPF 학습 설정 영역을 `UserControl`로 분리했습니다.
   - `WpfTrainingSettingsPanel.xaml`과 code-behind를 추가해 이미지 크기, 배치, 에폭, cfg, 가중치, 검증 비율, split seed, 점검/시작/중지, 학습 준비/진행 상태 UI를 WPF 셸 XAML 밖으로 옮겼습니다.
   - `WpfTrainingSettingsPanelViewModel`을 추가하고 `WpfTrainingSettingsPanel.ViewModel`로 연결했습니다.
   - 셸은 `TrainingSettingsPanelControl` 이벤트를 기존 학습 준비 점검/시작/중지/숫자 입력 핸들러로 전달하고, 기존 `FindName("TrainingImageSizeBox")`, `FindName("StartTrainingButton")`, `FindName("TrainingProgressText")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - 학습 패널 내부 style은 부모 Window `StaticResource`에 의존하지 않도록 UserControl 내부 리소스로 분리했고, 다크 테마에서 ComboBox가 밝게 뜨지 않도록 템플릿을 맞췄습니다.
   - 빌드 로그는 `artifacts\logs\build-20260620-training-settings-usercontrol-style.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260620-training-settings-usercontrol-style.log`입니다.
   - 학습 설정 확장 캡처는 `artifacts\ui\wpf-training-settings-usercontrol-style-check.png`, 로그는 `artifacts\logs\visual-20260620-training-settings-usercontrol-style.log`입니다.

106. WPF 로그/상태바 영역을 `UserControl`로 분리했습니다.
   - `WpfStatusBarPanel.xaml`과 code-behind를 추가해 하단 dataset/Python/model 상태 표시를 WPF 셸 XAML 밖으로 옮겼습니다.
   - `WpfShellLogPanel.xaml`과 code-behind를 추가해 기존 `OpenVisionLab.Logging.Controls.View.LogPanelView` 로그 DLL UI를 별도 패널로 감쌌습니다.
   - `WpfStatusBarPanelViewModel`, `WpfShellLogPanelViewModel`을 추가해 새 WPF view에도 대응 ViewModel 규칙을 적용했습니다.
   - 셸은 기존 `SetDatasetStatus`, `SetPythonStatus`, `SetModelStatus`, `AppendLog` 흐름을 유지하고, 기존 `FindName("DatasetStatusText")`, `FindName("PythonStatusText")`, `FindName("ModelStatusText")`, `FindName("ShellLogPanel")` 테스트 호환 이름을 namescope에 다시 등록합니다.
   - 상태바 긴 문구는 작은 창에서 겹치지 않도록 `TextTrimming`을 적용했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-log-status-usercontrol.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-log-status-usercontrol.log`입니다.
   - WPF 화면 캡처는 `artifacts\ui\wpf-log-status-usercontrol-check.png`, 로그는 `artifacts\logs\visual-20260621-log-status-usercontrol.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-log-status-usercontrol.log` 기준 통과했습니다.

107. WPF 클래스 탭에 YOLO 출력 경로 편집을 추가했습니다.
   - `WpfClassCatalogPanel`에 `저장 경로` 입력칸, 경로 선택 버튼, 저장 버튼을 추가해 WinForms `FormVision_ClassMenu`의 output root 설정 흐름을 WPF에서 처리할 수 있게 했습니다.
   - 경로 선택/저장은 기존 데이터 API인 `CData.ConfigureOutputRoot`, `SaveYoloDataYaml`, `SaveConfig`를 그대로 사용합니다.
   - 출력 경로를 저장하면 학습 준비 상태와 하단 dataset 상태를 즉시 갱신하고, 로그에 적용 경로를 남깁니다.
   - 첫 화면 캡처에서 경로 줄의 의미가 약해 보여 `클래스`와 `저장 경로` 라벨을 분리한 뒤 다시 검증했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-output-root-wpf-polish.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-output-root-wpf-polish-retry.log`입니다.
   - 클래스 탭 WPF 캡처는 `artifacts\ui\wpf-output-root-class-tab-polish-check.png`, 로그는 `artifacts\logs\visual-20260621-output-root-class-tab-polish.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-output-root-wpf.log` 기준 통과했습니다.

108. WinForms 호환 화면의 YOLO 설정 버튼을 WPF 설정 탭으로 연결했습니다.
   - `WpfLabelingShellLauncher`를 추가해 기존 WinForms 화면에서 설정 버튼을 누르면 `WpfLabelingShellWindow`를 열고 YOLO 탭, 모델 설정, 학습 설정 영역을 바로 펼치도록 했습니다.
   - `FormTrainingPanel`의 모델 설정 버튼과 `FormMainFrame`의 Python 설정 버튼은 더 이상 `FormVision_Yolov5ParamSetting`을 직접 띄우지 않습니다.
   - 회귀 방지를 위해 두 WinForms 호출부가 WPF 런처를 사용하는지, WPF 설정 탭 포커스가 실제로 동작하는지 자동 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-settings-launcher.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-settings-launcher.log`입니다.
   - YOLO 탭 WPF 캡처는 `artifacts\ui\wpf-settings-launcher-yolo-tab-check.png`, 로그는 `artifacts\logs\visual-20260621-wpf-settings-launcher-yolo-tab.log`입니다.

109. WinForms 호환 화면의 클래스 설정 버튼을 WPF 클래스 탭으로 연결했습니다.
   - `WpfLabelingShellLauncher.ShowClassCatalog()`와 `WpfLabelingShellWindow.FocusClassCatalogTab()`를 추가해 기존 `FormMainFrame`의 클래스 버튼이 WPF 클래스 탭을 열도록 했습니다.
   - `FormMainFrame`은 더 이상 `FormVision_ClassMenu`를 직접 생성하지 않습니다.
   - WPF 런처의 탭 포커스 동작은 WPF Dispatcher 안에서 실행되도록 정리해 WinForms/WPF 혼합 호출에서도 UI 스레드 경계를 지킵니다.
   - 회귀 방지를 위해 클래스 설정 버튼이 WPF 런처를 쓰는지, WPF 클래스 탭 포커스가 실제로 동작하는지 자동 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-class-launcher.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-class-launcher.log`입니다.
   - 클래스 탭 WPF 캡처는 `artifacts\ui\wpf-class-launcher-tab-check.png`, 로그는 `artifacts\logs\visual-20260621-wpf-class-launcher-tab.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-class-launcher.log` 기준 통과했습니다.

110. 라이트 테마의 AI 검출 결과 오버레이 가독성을 개선했습니다.
   - `WpfCanvasPanel`의 검출 결과 오버레이 색상을 하드코딩에서 `DetectionOverlay...Brush` 테마 리소스로 변경했습니다.
   - `ApplyTheme`에서 라이트/다크 각각의 오버레이 배경, 테두리, 제목, 요약, 선택 행, 상세 텍스트 색상을 갱신합니다.
   - 라이트 테마 자체평가 캡처에서 밝은 배경 위 흰 글씨가 흐려 보이던 문제를 확인했고, 수정 후 라이트/다크 양쪽 캡처로 재검증했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-light-overlay-theme.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-light-overlay-theme.log`입니다.
   - 라이트 테마 캡처는 `artifacts\ui\wpf-light-overlay-theme-check.png`, 다크 테마 회귀 캡처는 `artifacts\ui\wpf-dark-overlay-theme-regression-check.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-light-overlay-theme.log` 기준 통과했습니다.

111. WPF YOLO 탭에 recipe/config 저장 위치 패널을 추가했습니다.
   - `WpfProjectConfigPanel`과 `WpfProjectConfigPanelViewModel`을 추가해 현재 recipe 이름, `VISION.xml` 저장 위치, 설정 저장, 폴더 열기 버튼을 WPF에서 볼 수 있게 했습니다.
   - recipe 이름이 비어 있으면 저장 버튼을 비활성화하고, 잘못된 빈 recipe 저장을 하지 않도록 상태 문구로 안내합니다.
   - WPF의 YOLO 모델 설정 저장 버튼은 이제 모델 설정뿐 아니라 학습 설정 값까지 반영한 뒤 recipe XML 저장 흐름을 호출합니다.
   - 자체평가에서 패널이 접혀 있으면 경로 확인 목적이 약해 보여 프로젝트 패널만 기본 펼침으로 바꿨습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-project-config-panel-expanded.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-project-config-panel-expanded.log`입니다.
   - 다크 테마 캡처는 `artifacts\ui\wpf-project-config-panel-expanded-check.png`, 라이트 테마 캡처는 `artifacts\ui\wpf-project-config-panel-light-check.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-project-config-panel.log` 기준 통과했습니다.

112. WPF 프로젝트 패널에서 recipe 이름 적용/전환을 시작할 수 있게 했습니다.
   - `ProjectRecipeNameBox`를 입력 가능하게 바꾸고 `ApplyProjectRecipeButton`을 추가했습니다.
   - 적용 시 recipe 이름의 파일 시스템 금지 문자를 검사하고, 기존 `CRecipe.Name` 흐름을 통해 recipe 디렉터리 생성/설정 로드를 수행합니다.
   - recipe 적용 후 프로젝트 패널, YOLO 경로, 학습 설정, 클래스 목록, 후보/객체 목록, 학습 준비 상태를 다시 갱신합니다.
   - 다크/라이트 자체평가에서 적용 버튼과 입력칸이 좁은 우측 패널에서도 잘리지 않는지 확인했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-recipe-apply.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-recipe-apply.log`입니다.
   - 다크 테마 캡처는 `artifacts\ui\wpf-recipe-apply-yolo-tab-check.png`, 라이트 테마 캡처는 `artifacts\ui\wpf-recipe-apply-light-check.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-recipe-apply.log` 기준 통과했습니다.

113. Recipe가 없을 때 YOLO 설정 저장 문구가 과장되지 않도록 수정했습니다.
   - `SaveProjectConfigFromPanel`이 실제 XML 저장 성공 여부를 반환하도록 바꿨습니다.
   - recipe가 없는 상태에서 YOLO 설정을 눌렀을 때는 `저장 완료`가 아니라 `Recipe 적용 후 설정 저장이 필요`하다고 로그를 남깁니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-recipe-save-status.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-recipe-save-status.log`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-recipe-save-status.log` 기준 통과했습니다.

114. WPF 프로젝트 패널에서 기존 recipe 목록 선택/갱신 UX를 추가했습니다.
   - `ProjectRecipeListBox`와 `RefreshProjectRecipeListButton`을 추가해 `RECIPE` 폴더의 기존 recipe 디렉터리를 WPF에서 바로 확인할 수 있게 했습니다.
   - 목록 선택은 recipe 이름 입력칸과 config 경로 미리보기만 갱신하고, 실제 전환은 `적용` 버튼으로만 실행되게 해서 클릭 즉시 무거운 재로딩이 일어나지 않도록 했습니다.
   - 다크/라이트 테마 캡처 기준 우측 프로젝트 패널의 행 간격, 버튼 잘림, 입력 대비를 자체 확인했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-recipe-list-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-recipe-list-v2.log`입니다.
   - 다크 테마 캡처는 `artifacts\ui\wpf-recipe-list-yolo-tab-check-v2.png`, 라이트 테마 캡처는 `artifacts\ui\wpf-recipe-list-yolo-tab-light-check-v2.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-recipe-list.log` 기준 통과했습니다.

115. `FormMainFrame` WinForms shell을 삭제했습니다.
   - `0. UI/0) MENU/FormMainFrame.cs`, `FormMainFrame.Designer.cs`, `FormMainFrame.resx`를 제거했습니다.
   - `Program.cs`에서 `RunWinFormsApplication`, `ShouldRunWinFormsShell`, `StartupSplashHost`, `--winforms-shell`, `--legacy-winforms` 경로를 제거하고 WPF shell만 실행하도록 정리했습니다.
   - `MvcVisionSystem.csproj`의 FormMainFrame Designer 연결 항목을 삭제했습니다.
   - FormMainFrame 직접 생성 테스트는 제거하고, Program이 WPF shell만 실행하는지 확인하는 테스트로 바꿨습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-delete-formmainframe-v6.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-delete-formmainframe-v3.log`입니다.
   - WPF 화면 캡처는 `artifacts\ui\wpf-after-formmainframe-delete-yolo-tab-v3.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-delete-formmainframe-v2.log` 기준 통과했습니다.

116. 기존 WinForms 라벨링 패널을 삭제했습니다.
   - `FormTeachingVision`, `FormImageList`, `FormClassList`, `FormTrainingPanel`, `FormDetectionReviewPanel`, `FormLog`, `FormLayerDisplay`와 해당 Designer/resx 파일을 제거했습니다.
   - `DisplayDockHost`를 제거하고 `DisplayLayerDocument`를 추가해 `CDisplayManager`, `LabelingWorkflowService`, `DetectionResultApplicationService`가 WinForms 문서 창 없이 이미지/ROI/검출 상태를 유지하도록 바꿨습니다.
   - `MvcVisionSystem.csproj`에서 `DockPanelSuite`, `DockPanelSuite.ThemeVS2015` 패키지를 제거했습니다.
   - 삭제된 Form 직접 생성 테스트는 제거하거나 WPF/서비스/문서 상태 검증으로 바꿨습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-delete-winforms-panels-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-delete-winforms-panels-v1.log`입니다.
   - WPF 화면 캡처는 `artifacts\ui\wpf-after-delete-winforms-panels-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-delete-winforms-panels.log` 기준 통과했습니다.

117. 남은 레거시 WinForms 설정/클래스 다이얼로그를 삭제했습니다.
   - `FormVision_Yolov5ParamSetting`, `FormVision_ClassMenu`, `FormVision_NewPanel`와 해당 Designer/resx 파일을 제거했습니다.
   - `MvcVisionSystem.csproj`에서 세 다이얼로그의 WinForms `Compile Update` 항목을 제거했습니다.
   - 삭제된 다이얼로그 직접 생성 테스트를 WPF 클래스/YOLO/학습 패널 문구 검증으로 전환하고, 해당 파일이 다시 생기지 않도록 회귀 테스트를 추가했습니다.
   - `WpfClassCatalogPanel`, `WpfYoloModelSettingsPanel`, `WpfTrainingSettingsPanel`의 주요 사용자 문구를 정상 한국어 기준으로 정리했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-delete-legacy-dialogs-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-delete-legacy-dialogs-v1.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-after-delete-legacy-dialogs-yolo-tab.png`, `artifacts\ui\wpf-after-delete-legacy-dialogs-classes-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-delete-legacy-dialogs.log` 기준 통과했습니다.

118. YOLO/학습 설정 패널의 ViewModel 바인딩을 실제 편집 상태로 전환했습니다.
   - `WpfObservableViewModel`을 추가하고 `WpfYoloModelSettingsPanelViewModel`, `WpfTrainingSettingsPanelViewModel`에 편집 속성, `LoadFrom`, `ApplyTo`를 추가했습니다.
   - `WpfYoloModelSettingsPanel.xaml`, `WpfTrainingSettingsPanel.xaml` 입력칸/체크박스/콤보박스를 ViewModel에 TwoWay 바인딩했습니다.
   - `WpfLabelingShellWindow`의 YOLO/학습 설정 초기화와 저장은 TextBox 직접 읽기 대신 ViewModel round-trip을 사용합니다.
   - 회귀 방지를 위해 XAML 바인딩 선언 테스트와 ViewModel round-trip 테스트를 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-settings-viewmodels-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-settings-viewmodels-v1.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-settings-viewmodels-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-settings-viewmodels.log` 기준 통과했습니다.

119. 앱 경로의 남은 WinForms 보조 라이브러리 의존성을 제거했습니다.
   - `CCommon`의 OpenVisionLab WinForms 메시지 박스 사용을 WPF `MessageBox` 경로로 바꿨습니다.
   - `Program.cs`에서 RJ WinForms appearance 설정 로드를 제거했습니다.
   - `CData`, `CRecipe`, `WpfLabelingShellWindow`의 레시피/데이터 경로 기준을 `Application.StartupPath`에서 `AppContext.BaseDirectory`로 바꿨습니다.
   - `ScreenCaptureService`는 WinForms `Screen` 대신 Win32 `GetSystemMetrics`로 primary screen 크기를 조회합니다.
   - `FormScreenPlacement.cs`를 삭제했습니다.
   - `CViewer`의 RJControls/FontAwesome 전용 드롭다운 메뉴를 기본 `ContextMenuStrip`/`ToolStripMenuItem`으로 바꿨습니다.
   - `MvcVisionSystem.csproj`와 `MvcVisionSystem.sln`에서 `RJControls`, `OpenVisionLab.MessageBox`, `OpenVisionLab.Controls.Init`, `FontAwesome.Sharp` 참조를 제거했습니다.
   - 과거 `Library\RJControls`, `Library\MvcVisionSystem.Controls.Init`에 남아 있던 빌드 산출물 폴더를 삭제했습니다.
   - 회귀 방지를 위해 프로젝트/솔루션/공통 팝업/뷰어 메뉴에 레거시 보조 라이브러리 이름이 다시 들어오지 않는지 검사하는 테스트를 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-remove-legacy-winforms-libs-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-remove-legacy-winforms-libs-v1.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-remove-legacy-winforms-libs-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-remove-legacy-winforms-libs.log` 기준 통과했습니다.

120. `CViewer`의 WinForms 디자이너/리소스 의존을 제거했습니다.
   - `Library/CViewer.Designer.cs`와 `Library/CViewer.resx`를 삭제했습니다.
   - `CViewer`의 우클릭 메뉴는 디자이너 필드 대신 코드에서 생성하는 작은 `ContextMenuStrip`으로 정리했습니다.
   - 10ms `System.Windows.Forms.Timer`로 drag mode를 강제하던 경로를 제거했습니다. `onlyDragmode`는 기존 `CurrentMode`/mode setter guard로 유지됩니다.
   - `CRectangleObject`는 더 이상 WinForms `Cursor`를 반환하지 않고 ROI 선택 geometry만 제공합니다. 커서 매핑은 OpenGL host를 가진 `CViewer` 안으로 옮겼습니다.
   - `CViewer.Rendering`의 overlay label bitmap 생성은 WinForms `TextRenderer` 대신 GDI+ `Graphics.MeasureString/DrawString`을 사용합니다.
   - 회귀 방지를 위해 `CViewer.Designer.cs`, `CViewer.resx`, `timer1`, ROI 객체의 WinForms cursor 의존이 다시 들어오지 않는지 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-remove-cviewer-designer-v3.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-remove-cviewer-designer-v3.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-remove-cviewer-designer-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-remove-cviewer-designer-v2.log` 기준 통과했습니다.

121. 사용되지 않는 WinForms 브리지/스레드 헬퍼를 삭제했습니다.
   - `0. UI/9) WPF/Interop/WpfRoiCanvasHost.cs`를 삭제했습니다. WPF 셸은 이미 `WpfCanvasPanel.xaml`에서 `RoiImageCanvasView`를 직접 호스팅합니다.
   - `2. Common/UIThreadInvokeClass.cs`를 삭제했습니다. 현재 OpenGL refresh marshaling은 ImageCanvas 쪽 전용 경로로 처리되고, 앱 공통 extension helper는 더 이상 참조되지 않습니다.
   - 기존 `WpfRoiCanvasHost` 생성 테스트는 WPF canvas panel이 `RoiImageCanvasView`를 `WindowsFormsHost`/`ElementHost` 없이 직접 포함하는지 확인하는 테스트로 바꿨습니다.
   - 앱 소스 기준 `System.Windows.Forms` 직접 사용은 `CViewer` host/input API 범위로 좁혀졌습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-remove-unused-winforms-bridges-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-remove-unused-winforms-bridges-v1.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-remove-unused-winforms-bridges-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-remove-unused-winforms-bridges.log` 기준 통과했습니다.

122. `CViewer`의 raw WinForms host attach API를 어댑터로 분리했습니다.
   - 외부에서 `viewer.AttachTo(Control)`를 직접 호출하지 않도록 `CViewerWinFormsHostAdapter`를 추가했습니다.
   - `CViewer` 내부 attach 메서드는 `AttachToWinFormsHost`로 이름을 낮춰 임시 OpenGL/SharpGL WinForms host 경계임을 명확히 했습니다.
   - 오래된 adapter를 dispose해도 새로 붙은 active canvas를 떼지 않도록 현재 canvas 참조를 확인하는 수명 주기 guard를 추가했습니다.
   - 회귀 방지를 위해 raw `public ImageCanvasControl AttachTo(...)` API가 다시 생기지 않는지, WinForms host 경계가 adapter에만 노출되는지 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-cviewer-host-adapter-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-cviewer-host-adapter-v1.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-cviewer-host-adapter-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-cviewer-host-adapter.log` 기준 통과했습니다.

123. ImageCanvas 입력 이벤트의 WinForms 타입을 ViewModel 밖으로 분리했습니다.
   - `CanvasPointerButton`, `CanvasKeyboardEventArgs`, `RoiImageCanvasInputAdapter`를 추가했습니다.
   - `RoiImageCanvasViewModel`은 더 이상 `System.Windows.Forms.MouseButtons`, `System.Windows.Forms.Keys`, `System.Windows.Forms.KeyEventArgs`에 직접 분기하지 않고 neutral canvas 입력 타입만 봅니다.
   - WinForms 입력 이벤트 변환은 `RoiImageCanvasInputAdapter`가 맡도록 해서 다음 단계의 WPF native canvas/input 전환 지점을 좁혔습니다.
   - 회귀 방지를 위해 ViewModel에 WinForms mouse/key 타입이 다시 들어오지 않는지 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-imagecanvas-input-adapter-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-imagecanvas-input-adapter-v2.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-imagecanvas-input-adapter-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-imagecanvas-input-adapter.log` 기준 통과했습니다.

124. `ImageCanvasControl`의 WinForms 디자이너/리소스 의존을 제거했습니다.
   - `OpenVisionLab.ImageCanvas\Engine\ImageCanvasControl.designer.cs`와 `ImageCanvasControl.resx`를 삭제했습니다.
   - `ImageCanvasOpenGlHostAdapter`를 추가해 `SharpGL.OpenGLControl` 생성, 속성 설정, OpenGL/마우스/키 이벤트 연결을 한 곳으로 모았습니다.
   - `ImageCanvasControl`은 이제 `InitializeComponent()` 대신 코드 생성된 OpenGL host를 추가하며, 디자이너 파일 없이 OpenGL canvas를 구성합니다.
   - 회귀 방지를 위해 `ImageCanvasControl` 디자이너/resx가 다시 생기지 않는지, OpenGL draw event wiring이 host adapter 안에 있는지 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-imagecanvas-opengl-host-adapter-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-imagecanvas-opengl-host-adapter-v1.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-imagecanvas-opengl-host-adapter-yolo-tab.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-imagecanvas-opengl-host-adapter.log` 기준 통과했습니다.

125. WPF 프로젝트 패널 상태를 ViewModel/서비스로 옮기고 다크 테마 ComboBox를 보정했습니다.
   - `WpfProjectConfigPanelViewModel`이 recipe 이름, recipe 목록, 선택 recipe, config 경로, 상태 문구를 소유하도록 바꿨습니다.
   - `WpfProjectRecipeService`를 추가해 recipe root, config 경로 preview, recipe 목록 읽기, recipe 이름 검증을 셸 code-behind 밖으로 분리했습니다.
   - `WpfProjectConfigPanel.xaml`의 recipe/config 필드를 ViewModel에 바인딩하고, 셸은 더 이상 `ProjectRecipeNameBox.Text`, `ProjectRecipeListBox.Items`, `ProjectConfigPathBox.Text`를 직접 갱신하지 않습니다.
   - 자체평가 캡처에서 프로젝트 `목록` ComboBox가 밝게 보이고 너무 좁아지는 문제를 확인해 다크 템플릿과 최소 너비를 적용했습니다.
   - 회귀 방지를 위해 XAML 바인딩, ViewModel round-trip, recipe service 사용, ComboBox 다크 chrome을 테스트에 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-project-config-combobox-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-project-config-combobox-v3.log`입니다.
   - WPF 캡처는 `artifacts\ui\wpf-project-config-combobox-yolo-tab-v2.png`입니다.
   - first-run 검증은 `artifacts\logs\verify-first-run-20260621-wpf-project-config-combobox-v2.log` 기준 통과했습니다.

126. 캔버스 조작, 후보 검토, 이미지 전환, 교육 가이드 UX를 보강했습니다.
   - `WpfCanvasPanel`에 Fit, 1:1, Pan, 선택 후보 Focus, AI 후보 표시 지우기 버튼을 추가했습니다.
   - `ImageCanvasControl`에 `ZoomToActualSize()`를 추가해 WPF 캔버스 1:1 명령이 실제 OpenGL 뷰어 동작으로 연결됩니다.
   - `WpfCandidateReviewPanel`에 이전/초점/다음 후보 버튼을 추가하고, 후보 리스트에는 `N`, `P`, `F` 키 흐름을 추가했습니다.
   - 선택 후보 Focus는 후보 이미지 픽셀 좌표를 OpenGL rect로 변환해 `FitToRect`로 이동하므로 확대/축소 상태와 분리됩니다.
   - 이미지 큐 클릭 속도 보강을 위해 현재 행 주변 이미지를 백그라운드에서 미리 디코드하는 bounded cache를 추가했습니다.
   - `WpfLearningWorkflowPanelViewModel`은 YOLO, U-Net, Anomaly, 학습/추론/검토 설명과 브러시 크기/마스크 농도 상태를 소유합니다.
   - 박스 도구는 기존 드로잉 모드, 이동 도구는 Pan, 삭제 도구는 선택 객체 삭제 흐름에 연결했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-ux-commands-v5.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-ux-commands-v3.log`입니다.
   - WPF 캡처는 `tests\artifacts\ui\wpf-detection-overlay-visual-check.png` 기준으로 확인했습니다.
   - 이미지 큐 클릭 성능 로그는 `artifacts\logs\perf-20260621-wpf-ux-commands-v2.log`이며, cold 1회를 제외한 평균은 약 48.8ms입니다. 대부분 30~60ms 구간이고 한 번 82.6ms 스파이크가 있었습니다.

127. U-Net 구현을 보류하고 YOLO 후보 검토 흐름을 더 다듬었습니다.
   - U-Net runtime/model 구현은 이번 범위에서 제외했습니다. 별도 Python 프로젝트로 만들지 여부를 먼저 결정합니다.
   - AI 후보 확정/스킵 후 첫 후보로 튕기지 않고 다음 visible 후보를 선택하도록 했습니다.
   - 확정 후 후보가 남아 있으면 Candidate Review에 머물며 다음 후보를 Focus하고, 후보가 없을 때만 Object Review로 이동합니다.
   - 캔버스 Fit/1:1/Pan/Focus/AI Reset 버튼은 이미지/후보/검출 상태에 따라 활성화됩니다.
   - adjacent decode cache는 큰 이미지를 캐시에 보관하지 않도록 pixel limit을 추가했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-yolo-review-flow-v3.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-yolo-review-flow-v3.log`입니다.
   - WPF 캡처 로그는 `artifacts\logs\visual-20260621-yolo-review-flow-v1.log`이고 캡처는 `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`입니다.
   - 이미지 큐 클릭 성능 로그는 `artifacts\logs\perf-20260621-yolo-review-flow-v1.log`이며, cold 1회를 제외한 평균은 약 46.9ms입니다.

128. YOLOv5 학습 흐름을 초보자 기준 가이드로 정리했습니다.
   - 사용자가 처음 보는 순서를 `이미지 폴더 열기 -> 클래스 등록 -> 박스 라벨링 -> 저장과 데이터셋 점검 -> YOLOv5 학습 -> 학습 결과 추론 검토`로 고정했습니다.
   - 이 순서를 오른쪽 `가이드` 탭 최상단에 배치해서 첫 화면에서 1~6단계가 바로 보이게 했습니다.
   - 각 단계는 짧은 행동 문구로 보이게 하고, 단계별 결과 설명은 툴팁으로 남겼습니다.
   - U-Net runtime/model 구현은 계속 보류합니다. 이번 작업은 YOLOv5 training UX에만 집중했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-yolo-training-workflow-guide-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-yolo-training-workflow-guide-v2.log`입니다.
   - WPF 가이드 탭 캡처 로그는 `artifacts\logs\visual-20260621-yolo-training-guide-v3.log`이고 캡처는 `tests\artifacts\ui\wpf-yolo-training-guide-check.png`입니다.

129. YOLOv5 학습 가이드를 실제 WPF 동작과 연결했습니다.
   - 가이드 단계 row를 클릭할 수 있게 했고, 클릭 시 관련 화면으로 이동합니다.
   - 1단계는 이미지 폴더 선택, 2단계는 클래스 탭, 3단계는 라벨링/박스 도구, 4단계는 저장 후 데이터셋 점검, 5단계는 YOLO 학습 설정과 시작 버튼, 6단계는 학습 결과 추론 검토로 연결했습니다.
   - 학습 시작은 자동 실행하지 않습니다. 가이드 클릭은 `시작` 버튼으로 포커스를 보내고, 사용자가 명시적으로 시작하도록 유지합니다.
   - 가이드 상단에 데이터셋 readiness 카드를 추가했고, YOLO 탭의 점검 결과와 같은 상태를 보여줍니다.
   - 학습 완료 상태가 들어오면 `runs/train/*/weights/best.pt`와 project/output root의 `best.pt` 중 최신 후보를 YOLO weight 경로로 반영합니다.
   - 학습 후 추론 단계는 inference mode와 후보 검토 탭으로 이동하고 `현재 검사` 버튼에 포커스를 줍니다.
   - 박스/Pan/Delete는 기존 검증 경로로 유지하고, 당시 원/타원/폴리곤/브러시/지우개/Undo/Redo는 아직 실제 drawing path 검증 전 상태로 남겼습니다. 원/타원은 이후 141번에서 연결했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-yolo-training-workflow-actions-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-yolo-training-workflow-actions-v1.log`입니다.
   - WPF 가이드 탭 캡처 로그는 `artifacts\logs\visual-20260621-yolo-training-workflow-actions-v1.log`이고 캡처는 `tests\artifacts\ui\wpf-yolo-training-workflow-actions-check.png`입니다.

130. YOLOv5 학습 가이드의 완료 표시와 문제 해결 UX를 추가했습니다.
   - 1~6단계 row에 `완료`, `점검 필요`, `진행 중`, `추론 필요` 같은 상태 배지를 붙였습니다.
   - readiness 카드 아래에 `클래스`, `라벨`, `점검` 해결 버튼을 추가했고, 각각 클래스 탭, 박스 라벨링, 데이터셋 점검 흐름으로 연결했습니다.
   - 학습 완료 후 새 `best.pt`를 적용하면 YOLO 설정 탭과 `설정 저장` 버튼으로 포커스를 보내고, recipe 저장 필요 상태를 명확히 표시합니다.
   - 학습 readiness/progress 텍스트와 progress bar는 대기/진행/완료/실패 상태별 색상으로 구분합니다.
   - 당시 원/타원/폴리곤/브러시/지우개는 아직 실제 drawing path 검증 전이므로 가짜 동작을 넣지 않고, 상태 문구로 미연결 상태를 명확히 알렸습니다. 원/타원은 이후 141번에서 연결했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-yolo-training-workflow-polish-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-yolo-training-workflow-polish-v1.log`입니다.
   - WPF 가이드 탭 캡처 로그는 `artifacts\logs\visual-20260621-yolo-training-workflow-polish-v1.log`이고 캡처는 `tests\artifacts\ui\wpf-yolo-training-workflow-polish-check.png`입니다.

131. YOLOv5 학습 가이드 이력 저장과 문제별 안내 분리를 추가했습니다.
   - `LabelingProjectSettings.TrainingGuide`에 마지막 데이터셋 점검, 학습 상태, 적용된 `best.pt`, recipe 저장 여부를 남깁니다.
   - 새 `best.pt` 적용 후에는 recipe에 자동 저장하지 않고 `recipe 미저장`으로 표시하며, 사용자가 `설정 저장`을 성공했을 때만 `recipe 저장됨`으로 기록합니다.
   - readiness 오류를 클래스 없음, 라벨 없음, valid 이미지 없음, data.yaml 문제, 라벨 형식 문제, 출력 경로 문제, 이미지 폴더 문제로 분류해 행동 문구를 다르게 보여줍니다.
   - 가이드 카드에 `해야 할 행동`과 `최근 이력` 문구를 추가했습니다.
   - 당시 WPF 중앙 캔버스와 legacy `CViewer`의 드로잉 API 경로가 달라 원/타원/폴리곤/브러시/지우개를 억지로 연결하지 않았습니다. 원/타원은 이후 141번에서 WPF/ImageCanvas 경로로 연결했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-yolo-training-history-issues-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-yolo-training-history-issues-v1.log`입니다.
   - WPF 가이드 탭 캡처 로그는 `artifacts\logs\visual-20260621-yolo-training-history-issues-v1.log`이고 캡처는 `tests\artifacts\ui\wpf-yolo-training-history-issues-check.png`입니다.

132. YOLOv5 학습 가이드에 최근 실행 목록을 추가했습니다.
   - `TrainingGuide.RunHistory`에 데이터셋 점검, 학습 종료 상태, weight 적용/recipe 저장 상태를 최대 8개까지 보관합니다.
   - 가이드 카드에는 최근 5개만 짧게 보여서 사용자가 방금 무엇을 했는지 확인할 수 있게 했습니다.
   - 시작 시 자동 점검 기록을 쌓지 않고, 명시적인 데이터셋 점검/학습 상태/weight 적용 흐름에서만 이력을 남깁니다.
   - `best.pt`를 적용한 뒤 recipe 저장 전/후 상태가 같은 weight 이력에서 갱신됩니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-yolo-training-run-history-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-yolo-training-run-history-v1.log`입니다.
   - WPF 가이드 탭 캡처 로그는 `artifacts\logs\visual-20260621-yolo-training-run-history-v1.log`이고 캡처는 `tests\artifacts\ui\wpf-yolo-training-run-history-check.png`입니다.

133. 좌측 빠른 필터, 가이드 기본 화면, 전역 추론 상태 배지를 정리했습니다.
   - 이미지 큐 빠른 필터를 6개 1줄에서 3x2 버튼으로 바꿔 아이콘과 한글 텍스트가 잘리지 않게 했습니다.
   - 지적된 하단 눌림 문제를 반영해 필터 행 높이와 버튼 하단 여백을 늘렸습니다.
   - 가이드 탭은 YOLOv5 학습 순서와 현재 해야 할 일 중심으로 보이게 하고, 모드/흐름/도구 설명은 접힌 `추가 개념` 영역으로 내렸습니다.
   - readiness 해결 버튼 문구를 `클래스 등록`, `라벨링 시작`, `데이터셋 점검`처럼 바로 행동이 보이게 바꿨습니다.
   - 상단에 `추론 상태` 배지를 추가해 YOLO 탭을 열지 않아도 단일/일괄 추론 진행, 완료, 실패 상태를 볼 수 있게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-queue-guide-inference-status-v3.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-queue-guide-inference-status-v3.log`입니다.
   - WPF 가이드 탭 캡처 로그는 `artifacts\logs\visual-20260621-queue-guide-inference-status-v2.log`이고 캡처는 `tests\artifacts\ui\wpf-queue-guide-inference-status-check-v2.png`입니다.

134. 추론 결과 표시 후 캔버스 센터링과 상단 진행 표시를 보강했습니다.
   - AI 후보가 적용되는 순간 `ZoomToFit()`을 즉시 호출하고, WPF 렌더 타이밍과 idle 타이밍에 한 번씩 더 맞춰 결과 이미지가 중앙 fit 상태로 돌아오게 했습니다.
   - 상단 `추론 상태` 진행바는 WPF 기본 indeterminate 애니메이션 대신 render-priority 타이머 기반 pulse로 바꿔 상태 문구 갱신 때마다 진행 표시가 리셋되는 느낌을 줄였습니다.
   - 창 종료 시 진행 pulse 타이머를 정리해 다음 실행/테스트에 남는 UI 타이머가 없게 했습니다.
   - `--wpf-visual-smoke --show-busy-inference-status` 검증 옵션을 추가해 실제 진행 중 상태 칩을 캡처할 수 있게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-inference-center-progress-v4.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-inference-center-progress-v3.log`입니다.
   - WPF 결과 캡처 로그는 `artifacts\logs\visual-20260621-inference-center-progress-v1.log`, 캡처는 `tests\artifacts\ui\wpf-inference-center-progress-check-v1.png`입니다.
   - WPF 진행 중 캡처 로그는 `artifacts\logs\visual-20260621-inference-center-progress-busy-v2.log`, 캡처는 `tests\artifacts\ui\wpf-inference-center-progress-busy-check-v2.png`입니다.

135. 처음 사용자용 튜토리얼 진입점과 HTML 튜토리얼을 추가했습니다.
   - 오른쪽 `가이드` 탭 최상단에 `처음 10분 튜토리얼` 카드를 추가했습니다.
   - 튜토리얼 카드는 이미지 열기, 클래스 등록, 박스 라벨링, 데이터셋 점검, YOLOv5 학습, 추론 후보 검토 순서로 요약됩니다.
   - HTML 튜토리얼은 `docs\tutorial\labeling-workbench-tutorial.html`에 만들었고, 실제 WPF 캡처 이미지는 `docs\tutorial\images` 아래에 저장했습니다.
   - README의 `튜토리얼` 섹션에서 앱 안 가이드 탭과 HTML 문서 위치를 바로 찾을 수 있게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-tutorial-ui-html-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-tutorial-ui-html-v1.log`입니다.
   - WPF 캡처 로그는 `artifacts\logs\visual-20260621-tutorial-html-captures-v1.log`입니다.

136. 튜토리얼 단계가 실제 WPF 동작으로 연결되는지 side-effect 테스트를 추가했습니다.
   - `샘플` 단계가 임시 샘플 이미지를 로드하고 이미지 큐를 채우는지 확인합니다.
   - `라벨` 단계가 박스 도구를 선택하고 실제 ROI를 추가하며 객체 검토로 이동하는지 확인합니다.
   - `추론` 단계가 현재 이미지 검사 버튼을 활성화하는지 확인합니다.
   - `리뷰` 단계가 AI 후보 검토 탭으로 이동하는지 확인합니다.
   - `저장` 단계가 기존 YOLO 저장 경로를 통해 `data\train\labels\tutorial-sample.txt`를 만드는지 확인합니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-tutorial-side-effects-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-tutorial-side-effects-v2.log`입니다.

137. 가이드 탭에서 HTML 튜토리얼을 바로 열 수 있게 했습니다.
   - `처음 10분 튜토리얼` 카드에 `HTML 열기` 버튼을 추가했습니다.
   - 버튼은 WPF 패널 이벤트를 통해 셸로 전달되고, 셸은 `docs\tutorial\labeling-workbench-tutorial.html`을 현재 실행 경로와 상위 폴더에서 찾아 기본 브라우저로 엽니다.
   - HTML 경로 텍스트는 좁은 오른쪽 패널에서 잘려 보이지 않도록 말줄임과 툴팁을 적용했습니다.
   - 테스트는 버튼 생성, 이벤트 발생, 셸 등록, 실제 HTML 경로 해석을 확인합니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-tutorial-open-html-v4.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-tutorial-open-html-v3.log`입니다.
   - WPF 캡처 로그는 `artifacts\logs\visual-20260621-tutorial-open-html-guide-v3.log`입니다.
   - 캡처 이미지는 `artifacts\ui\wpf-tutorial-open-html-guide-v3.png`입니다.

138. WPF 라벨링 도구 팔레트에 실제 연결 상태를 표시했습니다.
   - `WpfAnnotationToolCapabilityService`를 추가해 Select, Box, Pan, Delete는 `가능`, Ellipse/Polygon/Brush/Eraser/Undo/Redo는 `대기`로 분리했습니다.
   - 가이드 탭 도구 팔레트에 `가능/대기` 배지를 표시해 사용자가 아직 연결되지 않은 도구를 실제 기능처럼 오해하지 않게 했습니다.
   - 미검증 도구를 클릭하면 기존 깨진 문자열 분기로 내려가지 않고, capability 서비스의 WPF 경로 대기 사유를 상태/로그에 남깁니다.
   - 자동 테스트는 상태표 개수, 배지 바인딩, 버튼 클릭 시 박스는 드로잉 모드 진입, 브러시는 드로잉 모드 미진입을 확인합니다.
   - visual smoke에 `--expand-learning-concepts` 옵션을 추가해 접힌 가이드 도구 영역도 캡처할 수 있게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-annotation-tool-capabilities-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-annotation-tool-capabilities-v2.log`입니다.
   - WPF 캡처 로그는 `artifacts\logs\visual-20260621-annotation-tool-capabilities-v2.log`입니다.
   - 캡처 이미지는 `artifacts\ui\wpf-annotation-tool-capabilities-v2.png`입니다.

139. WPF 라벨링 도구별 실제 연결 검증 매트릭스를 문서와 테스트로 고정했습니다.
   - 검증 기준은 WPF 입력, visible canvas 반영, object review 반영, 저장 포맷, 자동 테스트까지 이어지는지로 정했습니다.
   - `docs\WPF_ANNOTATION_TOOL_VALIDATION.md`에 Select/Box/Pan/Delete는 연결 완료, Ellipse/Polygon/Brush/Eraser/Undo/Redo는 대기 사유를 기록했습니다.
   - 확인 결과 `CanvasInteractionMode`는 `None/Drawing/Edit/Move/Drag/Measure`뿐이며, WPF `Drawing`은 `RoiInteractionMouseUp.AddRectangleToOverlay`로 박스만 생성합니다.
   - `ImageCanvasControl`과 `OpenGlShapeDrawing`에는 Circle/Polygon/Pen 렌더링 primitive가 있지만, WPF 입력/리뷰/저장 경로와 연결된 것은 아닙니다.
   - `CViewer`에는 세그멘테이션 브러시/지우개/Undo/Redo가 있으나, WPF 팔레트 action을 legacy `CViewer`로 우회 연결하지 않는 것으로 검증했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-annotation-tool-validation-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-annotation-tool-validation-v1.log`입니다.

140. WPF 라벨링 도구의 다음 구현 방향을 이미지 픽셀 좌표 기준으로 정리했습니다.
   - 대기 상태는 불가능이 아니라 WPF 입력/리뷰/저장/테스트 경로가 아직 이어지지 않았다는 의미로 정정했습니다.
   - 원/타원/폴리곤/브러시/지우개는 OpenGL 좌표가 아니라 원본 이미지 픽셀 좌표의 annotation model을 원본 데이터로 삼습니다.
   - OpenGL은 현재 zoom/pan/fit 상태에 맞춰 픽셀 annotation을 화면에 렌더링하는 계층으로 둡니다.
   - YOLO detection에서는 그려진 픽셀 영역의 bounding box를 내보내고, segmentation에서는 polygon/mask를 내보내는 방향으로 정리했습니다.
   - `docs\WPF_ANNOTATION_TOOL_VALIDATION.md`에 `Pixel-Space Annotation Direction`을 추가했고, 자동 테스트가 해당 방향 문구를 검증합니다.

141. WPF 원/타원 라벨링 도구를 픽셀 좌표 기준으로 1차 연결했습니다.
   - `CanvasRect<float>`에 `CanvasRoiShapeKind`를 추가해 박스와 타원이 같은 픽셀 bounding box 모델을 공유합니다.
   - WPF 팔레트에서 원/타원을 선택하면 `DrawingShapeKind = Ellipse`로 전환되고, 드래그 완료 시 `AddEllipseToOverlay`가 호출됩니다.
   - OpenGL overlay compiler는 `ShapeKind=Ellipse`인 ROI를 반투명 내부 채움 + 외곽선 타원으로 렌더링합니다.
   - WPF shell은 실제 canvas `RoiAdded` 이벤트를 `manualRois`에 연결해, 사용자가 직접 그린 박스/타원이 object review와 YOLO 저장 bounds로 이어지게 했습니다.
   - 수동 ROI별 shape와 overlay id를 같이 저장해 refresh 후에도 타원이 박스로 바뀌지 않게 했습니다.
   - YOLO detection 저장은 원/타원의 bounding box를 사용합니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-ellipse-annotation-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-ellipse-annotation-v1.log`입니다.

142. WPF 원/타원 라벨 내부 채움을 명시적으로 보정했습니다.
   - 원/타원 ROI 생성과 redraw 복원 시 `IsFill`이 켜지도록 했습니다.
   - OpenGL 타원 렌더링은 내부를 반투명 alpha로 먼저 채우고 외곽선을 다시 그립니다.
   - 원본 이미지를 가리지 않도록 내부 alpha는 최대 0.22로 제한했습니다.
   - 자동 테스트가 `IsFill`과 반투명 fill 렌더링 문구를 검증합니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-ellipse-filled-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-ellipse-filled-v1.log`입니다.

143. WPF 박스 라벨링 UX와 캔버스 기본 표시를 1차 보정했습니다.
   - YOLOv5 가이드의 `박스 라벨링` 단계가 실제 박스 도구를 선택하고, 접힌 보조 개념 영역을 열어 도구 팔레트 위치로 이동하도록 했습니다.
   - 가이드 패널 내부 ListBox 위에서도 마우스 휠이 부모 ScrollViewer를 움직이도록 해서, 항목 위에 커서가 있어도 스크롤이 멈추지 않게 했습니다.
   - 보조 개념 영역은 `모드 -> 도구 -> 흐름` 순서로 바꿔, 라벨링을 하려는 사용자가 먼저 도구를 보게 했습니다.
   - WPF 라벨링 캔버스 기본값에서 디버그성 `Module` 이름, ROI item number, 초록색 group bounds를 숨겼습니다.
   - `RoiImageCanvasViewModel`의 1ms refresh timer를 16ms 단발성 debounce timer로 바꿔 resize/reshape 요청이 과하게 쌓이지 않게 했습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-box-label-ux-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-box-label-ux-v2.log`입니다.
   - 화면 확인 로그는 `artifacts\logs\visual-20260621-box-label-ux-guide-v1.log`, 캡처는 `artifacts\ui\wpf-box-label-ux-guide-v1.png`입니다.

144. WPF annotation object 검증 프로세스를 추가하고 사각형/원 내부 클릭 동작을 보정했습니다.
   - 기존 ROI 위에서 MouseDown이 시작되면 새 ROI 생성 경로를 막고, 선택/이동/리사이즈 경로를 우선하도록 했습니다.
   - 사각형/원 내부 클릭은 기존 ROI를 선택하고 edit handle을 유지하며, 새 ROI를 추가하지 않도록 자동 테스트로 고정했습니다.
   - 빈 공간 드래그는 사각형/원 각각 새 ROI를 정확히 1개만 생성하는지 검증합니다.
   - 마우스 Down마다 `Reshape` timer를 예약하던 경로를 제거해 사각형 클릭 시작 시 불필요한 렌더 재계산을 줄였습니다.
   - 새 문서 `docs\WPF_ANNOTATION_OBJECT_VERIFICATION.md`에 rectangle, ellipse/circle, polygon, brush, eraser의 검증 기준을 정리했습니다.
   - 새 스크립트 `scripts\verify-wpf-annotation-objects.ps1`는 빌드, 전체 테스트, WPF visual smoke 캡처를 한 번에 실행합니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-annotation-object-hit-v2.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-annotation-object-hit-v2.log`입니다.
   - 검증 스크립트 로그는 `artifacts\logs\verify-wpf-annotation-objects-script-20260621-v1.log`입니다.
   - 검증 캡처는 `artifacts\ui\verify-wpf-annotation-objects-20260621-223611.png`입니다.

145. 사각형/원 이동 및 리사이즈 좌표 검증을 추가했습니다.
   - 실제 WPF mouse-event 경로로 기존 ROI 내부 드래그가 같은 오브젝트를 이동하는지 확인합니다.
   - 실제 WPF mouse-event 경로로 코너 드래그가 같은 오브젝트를 리사이즈하는지 확인합니다.
   - 검증은 단순 표시 여부가 아니라 image-pixel 좌표 delta를 직접 비교합니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-annotation-object-edit-v6.log`입니다.
   - 검증 스크립트 로그는 `artifacts\logs\verify-wpf-annotation-objects-script-20260621-v2.log`입니다.
   - 검증 캡처는 `artifacts\ui\verify-wpf-annotation-objects-20260621-225533.png`입니다.

146. WPF 폴리곤 annotation의 image-pixel 저장 모델을 추가했습니다.
   - `WpfPolygonAnnotationService`가 점 추가, 시작점 근처 클릭 닫기, 이미지 경계 클램프, `LabelingSegmentationObject` 변환을 담당합니다.
   - 폴리곤 객체는 기존 `YoloSegmentationAnnotationService` 저장/로드 경로로 검증했습니다.
   - 이후 2026-06-21 추가 완료 항목에서 WPF 뷰어 클릭 입력/렌더링/저장까지 연결되어 폴리곤 도구는 `가능` 상태로 전환되었습니다.
   - 빌드 로그는 `artifacts\logs\build-20260621-wpf-polygon-draft-v1.log`입니다.
   - 자동 테스트 로그는 `artifacts\logs\tests-20260621-wpf-polygon-draft-v2.log`입니다.
   - 화면 확인 로그는 `artifacts\logs\visual-20260621-wpf-polygon-draft-v2.log`, 캡처는 `artifacts\ui\wpf-polygon-draft-guide-20260621.png`입니다.

147. ROI 내부 클릭/AI 후보 박스 클릭 경로를 분리했습니다.
   - 실제 라벨 ROI 내부 MouseDown은 기존 ROI 선택/이동/리사이즈 경로를 우선합니다.
   - AI 후보 검출 박스 내부 MouseDown은 후보 선택 이벤트로 처리하고 새 라벨 ROI 생성을 차단합니다.
   - `RoiImageCanvasDetectionOverlay`에 후보 index를 보관해 캔버스 클릭 시 우측 후보 목록 선택과 드로잉 하이라이트가 함께 갱신되도록 했습니다.
   - 자동 테스트 `WPF annotation object verification covers rectangle and ellipse hit behavior`에 AI 후보 박스 드래그 시 ROI가 추가되지 않는 회귀 검증을 포함했습니다.
   - 검증: `dotnet build .\MvcVisionSystem.csproj -c Debug`, `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`.
   - WPF visual smoke 캡처는 `artifacts\ui\verify-wpf-annotation-objects-20260622-211508.png`입니다.

148. WPF ROI 객체 조작 검증 시스템을 추가하고 작은 ROI 내부 클릭 문제를 수정했습니다.
   - 새 focused gate `--wpf-roi-object-verification`을 추가했습니다.
   - 새 스크립트 `scripts\verify-wpf-roi-object-interactions.ps1`는 빌드, ROI 객체 조작 테스트, WPF visual smoke 캡처를 한 번에 실행합니다.
   - 검증 matrix는 사각형 내부 이동, 좌/우/상/하 resize, 4개 corner resize, 빈 공간 사각형 생성, 빈 공간 원/타원 생성, 원/타원 내부 이동/resize를 포함합니다.
   - 작은 ROI에서 handle hit tolerance가 ROI 내부 전체를 덮어 가운데 드래그가 이동이 아니라 resize로 오판되던 문제를 수정했습니다.
   - ROI를 이미지 밖으로 이동할 때 멈추거나 크기가 바뀌지 않고, 이미지 경계까지 크기 유지 상태로 clamp되도록 수정했습니다.
   - WPF 셸의 `manualRois`, Object Review, export ROI가 이동/resize 후 동일한 image-pixel geometry를 받는지 자동 검증합니다.
   - 검증: `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-roi-object-verification`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`, `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-roi-object-interactions.ps1`.
   - 스크립트 로그는 `artifacts\logs\verify-wpf-roi-objects-tests-20260622-212617.log`, 캡처는 `artifacts\ui\verify-wpf-roi-objects-20260622-212617.png`입니다.

149. WPF 폴리곤/브러시/마스크 객체 조작 검증 시스템을 추가했습니다.
   - 새 focused gate `--wpf-segmentation-object-verification`을 추가했습니다.
   - ROI와 세그멘테이션을 같이 확인하는 통합 gate `--wpf-annotation-object-verification`을 추가했습니다.
   - 새 스크립트 `scripts\verify-wpf-segmentation-object-interactions.ps1`, `scripts\verify-wpf-annotation-object-interactions.ps1`를 추가했습니다.
   - 폴리곤은 꼭지점 이동뿐 아니라 내부 드래그로 전체 폴리곤을 이동할 수 있게 했습니다.
   - 폴리곤 전체 이동은 image-pixel bounds 기준으로 이미지 경계에 clamp됩니다.
   - 세그멘테이션 검증 matrix는 폴리곤 point 이동, 폴리곤 body 이동, 경계 clamp, 브러시 mask 생성, mask 이동, mask 경계 clamp, eraser, Object Review 선택, export geometry 반영을 포함합니다.
   - 검증: `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-segmentation-object-verification`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-annotation-object-verification`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`, `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-annotation-object-interactions.ps1`, `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-segmentation-object-interactions.ps1`.
   - 통합 스크립트 로그는 `artifacts\logs\verify-wpf-annotation-objects-focused-tests-20260622-213723.log`, 캡처는 `artifacts\ui\verify-wpf-annotation-objects-focused-20260622-213723.png`입니다.
   - 세그멘테이션 전용 스크립트 로그는 `artifacts\logs\verify-wpf-segmentation-objects-tests-20260622-213858.log`, 캡처는 `artifacts\ui\verify-wpf-segmentation-objects-20260622-213858.png`입니다.

150. WPF annotation 객체 검증 gate를 완료 기준으로 강제 연결했습니다.
   - 기존 `scripts\verify-wpf-annotation-objects.ps1`가 전체 테스트만 돌리는 대신 `--wpf-annotation-object-verification` focused gate를 실행하도록 변경했습니다.
   - `scripts\verify-first-run.ps1`의 PowerShell syntax check 목록에 ROI/segmentation/annotation object 검증 스크립트를 추가했습니다.
   - `TestWpfAnnotationToolVerificationMatrix`가 검증 문서와 스크립트가 focused gates를 호출하는지 확인합니다.
   - annotation interaction, Object Review, OpenGL annotation overlay, ROI geometry, polygon/mask editing, save/export 변경 시 `scripts\verify-wpf-annotation-object-interactions.ps1` 통과 전 완료 보고 금지 규칙을 문서화했습니다.
   - 검증: `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-annotation-object-verification`, `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-annotation-objects.ps1`, `powershell -ExecutionPolicy Bypass -File .\scripts\verify-first-run.ps1 -SkipBuild -SkipTests -SkipYoloSmoke`.
   - 스크립트 로그는 `artifacts\logs\verify-wpf-annotation-objects-focused-tests-20260622-214756.log`, 캡처는 `artifacts\ui\verify-wpf-annotation-objects-20260622-214756.png`입니다.

151. WPF 후보 검토 패널에 짧은 검토 이력을 추가했습니다.
   - 후보 로드, 스킵, 확정, 중복 제외 결과를 최근 4개까지 Candidate Review 탭 안에서 바로 볼 수 있게 했습니다.
   - `PostActionPolicyText` 기본 문구를 정상 한글로 보정했습니다.
   - 새 추론 결과가 들어오면 이전 검토 이력은 초기화되어 다른 이미지의 기록과 섞이지 않습니다.
   - 검증: `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-annotation-object-verification`.
   - 후보 검토 캡처는 `artifacts\ui\wpf-candidate-review-history-20260622-220529.png`입니다.

152. ROI 내부 클릭이 외곽 resize로 오판되는 hit-test 문제를 수정했습니다.
   - 기존에는 zoom/handle size 기준 tolerance가 ROI 내부까지 넓게 잡혀, 내부 클릭도 edge cursor와 resize로 판정될 수 있었습니다.
   - corner hit tolerance와 edge hit tolerance를 분리하고, edge resize band를 얇게 제한했습니다.
   - 내부 클릭은 `Move2D`/`EditingType.Move`로 남고, 실제 외곽 가까이 클릭했을 때만 `VSplit`/`HSplit` resize가 잡히도록 회귀 테스트를 추가했습니다.
   - 검증: `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nodeReuse:false`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-roi-object-verification`, `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`, `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-roi-object-interactions.ps1`.
   - ROI 검증 캡처는 `artifacts\ui\verify-wpf-roi-objects-20260622-221905.png`입니다.

## 진행 필요 항목

아래 목록은 오래된 TODO를 포함합니다. 먼저 위의 `2026-06-28 완료/보호 판정`을 적용합니다.
완료/보호로 승격된 항목은 일반 작업으로 다시 잡지 않고, 관련 파일을 직접 수정할 때만 focused gate를 다시 실행합니다.

1. WinForms 화면 추가 제거
   - 클래스 설정 화면 WPF 전환은 클래스 추가/삭제/출력 경로/호환 버튼 연결 기준 1차 완료
   - WinForms 호환 설정 버튼은 WPF YOLO 탭으로 연결 완료했고 `FormVision_Yolov5ParamSetting`, `FormVision_ClassMenu`, `FormVision_NewPanel`은 삭제 완료
   - 공용 메시지 창은 WPF `MessageBox`로 전환했고, `OpenVisionLab.MessageBox`, `OpenVisionLab.Controls.Init`, `RJControls`는 앱 프로젝트/솔루션 참조에서 제거 완료
   - `CViewer` 디자이너/resx, timer, `CRectangleObject` WinForms cursor 의존은 제거 완료
   - `WpfRoiCanvasHost`, `UIThreadInvokeClass`는 삭제 완료
   - `CViewer` raw WinForms attach API는 `CViewerWinFormsHostAdapter`로 분리 완료
   - `RoiImageCanvasViewModel`의 WinForms mouse/key 직접 분기는 `RoiImageCanvasInputAdapter`로 분리 완료
   - `ImageCanvasControl` 디자이너/resx는 삭제했고 `SharpGL.OpenGLControl` 생성/이벤트 연결은 `ImageCanvasOpenGlHostAdapter`로 분리 완료
   - 남은 WinForms 표면은 앱 레거시 라벨링 화면이 아니라 OpenGL canvas 호환 계층의 host/input API와 `RoiImageCanvasView` 내부 `WindowsFormsHost` 범위입니다.
   - 세부 학습 옵션이 추가로 필요하면 새 WinForms dialog가 아니라 WPF 패널/Window로 추가합니다.
   - recipe/config 위치 확인, 저장, recipe 이름 적용/전환, 기존 recipe 목록 선택/갱신은 WPF 프로젝트 패널 기준 1차 완료. 다음은 recipe 삭제/복사/공유 UX
   - 다음은 `RoiImageCanvasViewModel`에 남은 `OpenGLControl` cursor 직접 접근을 adapter로 더 줄이고, 그 다음 `WindowsFormsHost`를 대체할 WPF 친화 canvas surface를 검토합니다.

2. WPF 셸 분리
   - 현재 `WpfLabelingShellWindow.xaml.cs`가 커졌습니다.
   - ImageQueue 모델/표시/상세/필터/UserControl, 중앙 CanvasPanel, 현재 객체 검토 UserControl, AI 후보 검토 UserControl, 클래스 탭 UserControl, YOLO 상태/명령 UserControl, YOLO 모델 설정 UserControl, 학습 설정 UserControl, 로그/상태바 UserControl 분리는 1차 완료.
   - WPF `Views`, `ViewModels`, `Models`, `Services`, `Interop` 폴더 분리는 1차 완료.
   - 새 WPF 화면을 추가할 때는 같은 이름의 `...ViewModel`을 `ViewModels` 폴더에 같이 추가합니다.
   - YOLO 모델 설정과 학습 설정 ViewModel은 실제 편집 값 round-trip까지 1차 완료. 다음은 클래스/프로젝트/후보 패널의 상태도 ViewModel로 더 옮깁니다.
   - 프로젝트 패널의 recipe 이름/목록/config path/status 상태는 `WpfProjectConfigPanelViewModel`과 `WpfProjectRecipeService`로 분리 완료.
   - 다음 분리 대상: WPF 셸 code-behind 서비스 분리와 공용 확인/설정 Window의 WPF-UI 적용.
   - WPF-UI 적용 대상: 새로 추가하는 dialogs/windows.

3. Object Review 추가 개선
   - 후보 검토 탭의 확정 전/확정 후 후보 변경 이력은 151번에서 1차 완료.
   - 다음은 Object Review 쪽에서도 확정된 AI 후보, 수동 라벨, 삭제/클래스 변경 이력을 더 잘 구분해 보여주는 UX
   - 비교 패널에서 실제 중복 확정 방지 동작을 더 강하게 연결하는 UX

4. 배치 추론 운영 UX 개선
   - 긴 queue에서 실제 처리 시간을 측정하고 병목을 로그로 남기는 기능은 1차 완료
   - Python worker 연결 유지 반복 추론 smoke와 WPF 배치 중 view refresh/save 줄이기는 완료. 다음은 실제 장수별 처리 시간 기준으로 남은 병목을 비교합니다.
   - 중간 중지 후 재시작 UX
   - 후보/실패/확정/스킵/검출없음 빠른 필터와 row 상태 배지는 1차 완료. 다음은 상태바 클릭 필터가 실제로 필요한지 실사용 기준으로 판단합니다.
   - 전역 `추론 상태` 배지와 busy 캡처 검증은 1차 완료. 실제 긴 추론에서도 멈춤이 보이면 다음은 worker 요청 전송/응답 대기 구간의 UI thread 점유 시간을 프로파일링합니다.

5. 학습 UX 완성
   - YOLOv5 학습 순서 가이드와 단계별 화면 이동은 1차 완료.
   - 처음 사용자용 `처음 10분 튜토리얼` 카드와 HTML 화면 캡처 튜토리얼은 1차 완료.
   - 튜토리얼의 `샘플/라벨/추론/리뷰/저장` 단계 side-effect 테스트는 1차 완료.
   - 가이드 탭의 `HTML 열기` 버튼과 경로 해석 테스트는 1차 완료.
   - 도구 팔레트의 `가능/대기` 상태 표시와 미검증 도구 차단은 1차 완료.
   - 도구별 실제 연결 검증 매트릭스 문서화와 자동 테스트는 1차 완료.
   - 데이터셋 readiness 카드와 `클래스/라벨/점검` 해결 버튼은 1차 완료.
   - 학습 로그/오류 표시 개선은 상태 색상과 문제별 행동 문구 기준 1차 완료. 다음은 긴 오류 목록을 접고 펼치는 상세 패널입니다.
   - 학습 상태별 색상과 단계별 상태 배지는 1차 완료.
   - 학습 완료 후 weight 경로 갱신은 최신 `best.pt` 후보 반영 및 설정 저장 유도 기준 1차 완료.
   - 학습 시작 전 dataset 문제 해결 안내는 클래스/라벨/valid/data.yaml/라벨 형식/출력 경로/이미지 폴더 분리 기준 1차 완료.
   - 학습 가이드 최근 이력 저장과 최근 실행 목록은 1차 완료. 다음은 필요할 때만 이력 상세/비교/삭제 UX를 추가합니다.

6. 배포 검증 강화
   - Release publish 산출물 검증은 1차 완료
   - 누락 DLL 검증은 publish 스크립트 기준 1차 완료
   - sample recipe/runtime config 검증은 first-run 스크립트 기준 1차 완료
   - GitHub checkout 후 실행 순서 재점검

7. WPF UX polish
   - 참고 이미지처럼 좌측 작업 메뉴, 중앙 라벨링 캔버스, 상단 테마 버튼이 더 분명하게 보이도록 전체 레이아웃을 계속 정리
   - 작은 창/고해상도 화면 확인을 계속 반복
   - 버튼/탭 간격 정리
   - 상태 문구 한글화 정리
   - 반복 작업 단축키 정리
   - 좌측 이미지 큐 미리보기 debounce/cache 개선은 1차 완료. 다음은 실제 수천 장 폴더에서 preview cache 크기와 스크롤 체감을 조정합니다.
   - 이미지 큐 클릭 경로의 adjacent decode cache는 1차 완료. 다음은 실제 대용량 이미지 폴더에서 cache capacity와 메모리 상한을 조정합니다.
   - 캔버스 Fit/1:1/Pan/후보 Focus/AI Reset 명령은 1차 완료. 다음은 버튼 활성 상태와 툴바 밀도 조정입니다.
   - 후보 이전/다음/초점 이동과 `N`/`P`/`F` 키는 1차 완료. 다음은 확정/스킵 후 자동 다음 후보 선택 정책을 실사용 기준으로 정합니다.
   - 교육 가이드의 YOLO/U-Net/Anomaly 설명과 브러시/마스크 컨트롤은 접힌 `추가 개념` 영역으로 이동 완료. 다음은 실제 세그멘테이션/이상탐지 샘플과 연결할 때 별도 화면으로 분리할지 판단합니다.
   - U-Net runtime/model 구현은 보류합니다. 별도 Python 프로젝트 방향이 정해지기 전까지는 C# WPF 앱에서 실행 통합을 시작하지 않습니다.
   - YOLOv5 학습 순서 가이드는 가이드 탭 첫 화면 표시, 단계별 버튼 이동, 각 단계 완료/미완료 배지, 최근 점검/학습/weight 이력, 최근 실행 목록, 접힌 보조 개념 영역까지 1차 완료. 다음은 실제 학습 반복 후 이력 상세/비교가 필요한지 판단합니다.
   - 추론 결과 overlay 라벨 위치 충돌 방지와 확대/축소별 가독성은 1차 안정화 완료. 다음은 실제 작업 화면 기준 색/두께/배지 크기를 더 다듬습니다.
   - 확대 화면에서 큰 후보가 viewport 밖으로 걸릴 때 배지가 경계에 붙는 경우는 offscreen 방지까지 완료. 다음은 부분 노출 후보의 배지 위치를 더 자연스럽게 보정합니다.
   - 후보 tint/outline/배지 alpha는 1차 개선 완료. 다음은 실제 불량 이미지들에서 클래스별 색과 선택 상태 대비를 맞춥니다.
   - 추론 결과 적용 후 캔버스 `ZoomToFit` 재센터링은 1차 완료. 다음은 실제 큰 이미지/다양한 화면 비율에서 결과 위치를 계속 확인합니다.
   - 박스 라벨링 단계에서 도구 팔레트를 열고 박스 도구를 선택하는 흐름은 1차 완료. 다음은 실제 드래그 중 체감 지연이 남는지 큰 폴더/다수 ROI 기준으로 계측합니다.
   - WPF 캔버스 기본 표시에서 `Module` 디버그 텍스트/그룹 박스는 숨김 완료. 다음은 사용자가 켜고 싶을 때만 별도 디버그 토글로 제공할지 판단합니다.
   - 사각형/원 내부 클릭 선택, 빈 공간 생성, move/resize 좌표 변경 검증은 자동화 완료.
   - 폴리곤은 image-pixel 생성/저장, WPF 뷰어 클릭 입력, 프리뷰 렌더링, 선택 점 이동까지 완료했습니다.
   - 마스크는 brush/eraser 생성/삭제, 선택 강조, 선택 마스크 이동, OpenGL 부분 texture update까지 완료했습니다.

## 2026-06-21 추가 완료 - WPF 폴리곤 라벨링 기본 연결

- 완료:
  - 폴리곤 도구를 `가능` 상태로 전환했습니다.
  - OpenGL/ImageCanvas에서 이미지 픽셀 클릭 이벤트(`ImagePointClicked`)를 WPF Shell로 전달합니다.
  - 폴리곤 초안/확정 오버레이를 검출 박스와 같은 화면 오버레이 방식으로 렌더링합니다.
  - 시작점 근처 클릭 또는 더블클릭으로 폴리곤을 완료하고, 오른쪽 클릭으로 초안을 취소합니다.
  - 완료된 폴리곤은 Object Review에 `Polygon` 행으로 표시되고 클래스 변경/삭제 경로에 포함됩니다.
  - 저장 시 `BuildAnnotationSegments`를 통해 세그멘테이션 라벨로 저장됩니다.
- 검증:
  - `artifacts\logs\build-20260621-wpf-polygon-connected-v3.log`
  - `artifacts\logs\tests-20260621-wpf-polygon-connected-v2.log`
  - `artifacts\ui\wpf-polygon-connected-objects-20260621.png`
  - `artifacts\ui\wpf-polygon-connected-guide-20260621.png`
- 다음:
  - 폴리곤 점 선택/이동/삭제는 기본 생성 흐름이 충분히 쓰이는지 확인한 뒤 추가합니다.
  - 브러시/지우개 마스크 버퍼는 이후 추가 완료 항목에서 1차 연결되었습니다. 다음 우선순위는 Undo/Redo 명령 이력, Object Review 세그먼트 상세 UX, 마스크 프리뷰 품질 개선입니다.

## 2026-06-21 추가 완료 - WPF 브러시/지우개 raster mask 1차 연결

- 완료:
  - 브러시와 지우개 도구를 `가능` 상태로 전환했습니다.
  - `RoiImageCanvasViewModel.ImagePointMoved`를 추가해 WPF/OpenGL 드래그 입력을 이미지 픽셀 좌표로 Shell에 전달합니다.
  - `WpfMaskAnnotationService`를 추가해 원형 브러시, 드래그 보간, raster mask paint/erase, dirty bounds, empty-mask 제거를 담당하게 했습니다.
  - WPF Shell에서 브러시/지우개 선택 시 ROI drawing mode가 아니라 image-pixel mask input mode로 들어가도록 연결했습니다.
  - 생성된 mask는 Object Review에 `Mask` 행으로 표시되고, OpenGL에는 mask region overlay로 1차 프리뷰됩니다.
  - `BuildAnnotationSegments`가 raster mask를 버리지 않고 세그멘테이션 저장 경로로 넘기도록 수정했습니다.
- 검증:
  - `artifacts\logs\build-20260621-wpf-mask-tools-v2.log`
  - `artifacts\logs\tests-20260621-wpf-mask-tools-v1.log`
  - `artifacts\logs\tests-20260621-wpf-mask-tools-v2.log`
  - `artifacts\logs\visual-20260621-wpf-mask-tools-guide-v1.log`
  - `artifacts\ui\wpf-mask-tools-guide-20260621.png`
  - `TestWpfMaskAnnotationService`
  - `TestWpfBrushEraserShellInputCreatesMaskSegmentation`
- 다음:
  - 당시 mask 프리뷰는 region polygon overlay 기반이었습니다. 이 항목은 2026-06-22 OpenGL mask texture preview 전환 작업으로 해소되었습니다.
  - brush/eraser undo/redo는 이후 2026-06-22 추가 완료 항목에서 WPF edit history로 1차 연결되었습니다.
  - mask object 선택/상세 편집 UX는 기본 paint/erase 흐름을 실제로 사용해 본 뒤 필요한 조작만 추가합니다.

## 2026-06-22 추가 완료 - WPF Undo/Redo 편집 이력 1차 연결

- 완료:
  - Undo/Redo 도구를 `가능` 상태로 전환했습니다.
  - `WpfAnnotationHistoryService`를 추가해 manual ROI, ROI class/shape, polygon/mask segment, pending AI 후보, confirmed AI 후보를 스냅샷으로 복사/복원합니다.
  - WPF Shell에 `UndoWpfAnnotationHistory`와 `RedoWpfAnnotationHistory`를 연결했습니다.
  - ROI 생성/삭제/이동/리사이즈, polygon 추가, mask paint/erase, AI 후보 확정/스킵, Object Review 클래스 변경/삭제 전에 history snapshot을 쌓습니다.
  - Undo/Redo 후 캔버스 ROI, mask/polygon overlay, Object Review, Candidate Review, 이미지 큐 상태를 다시 갱신합니다.
  - Ctrl+Z / Ctrl+Y 단축키를 연결했습니다. 텍스트 입력 중에는 텍스트 편집 자체 Undo를 방해하지 않습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-undo-redo-v3.log`
  - `artifacts\logs\tests-20260622-wpf-undo-redo-v1.log`
  - `artifacts\logs\visual-20260622-wpf-undo-redo-guide-v1.log`
  - `artifacts\ui\wpf-undo-redo-guide-20260622.png`
  - `TestWpfAnnotationHistoryService`
  - `TestWpfShellUndoRedoRestoresAnnotationState`
- 다음:
  - Undo/Redo 정책은 WPF 편집 상태 복원 후 사용자가 명시적으로 저장하는 구조로 유지합니다.
  - history 상태를 도구 팔레트에 표시하는 작업은 2026-06-22 후속 작업으로 완료되었습니다.
  - mask 전용 texture preview와 mask object 상세 편집은 별도 작업으로 유지합니다.

## 2026-06-22 추가 완료 - WPF Undo/Redo runtime state UX

- 완료:
  - Guide 탭 도구 팔레트의 Undo/Redo 항목이 edit history stack 상태에 따라 `가능`/`없음`으로 바뀝니다.
  - Undo history가 없으면 Undo가 비활성화되고, Undo 실행 후 redo stack이 생기면 Redo가 활성화됩니다.
  - Undo/Redo tooltip에는 다음에 실행될 history action 이름을 표시합니다.
  - `WpfAnnotationToolItem`이 runtime availability를 ViewModel state로 들고, XAML은 `IsActionEnabled`와 `DisplayCapabilityText`를 바인딩합니다.
  - 정책은 명시 저장 방식으로 정리했습니다. Undo/Redo는 WPF 편집 상태만 복원하고 label 파일은 사용자가 저장할 때 반영됩니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-undo-redo-runtime-state-v1.log`
  - `artifacts\logs\tests-20260622-wpf-undo-redo-runtime-state-v1.log`
  - `artifacts\logs\visual-20260622-wpf-undo-redo-runtime-state-v1.log`
  - `artifacts\ui\wpf-undo-redo-runtime-state-20260622.png`
  - `TestWpfShellUndoRedoRestoresAnnotationState`
  - `TestWpfLearningWorkflowPanelDeclaresEducationModesAndTools`
- 다음:
  - toolbar 상단에 별도 Undo/Redo 버튼을 둘지는 보류합니다. 현재는 Guide 도구 팔레트와 Ctrl+Z/Ctrl+Y로 충분한지 실제 사용에서 확인합니다.

## 2026-06-22 추가 완료 - WPF annotation dirty/saved status

- 완료:
  - 하단 상태바에 현재 라벨 저장 상태 칩을 추가했습니다.
  - ROI/마스크/폴리곤/클래스 변경/삭제/Undo/Redo 같은 실제 annotation 변경은 `라벨 저장 필요`로 표시됩니다.
  - 저장 성공 후에는 `라벨 저장됨`으로 돌아오며, 이미지 로드 시에는 clean 상태로 시작합니다.
  - AI 후보 스킵은 라벨 파일 변경이 아니므로 dirty 상태로 표시하지 않습니다.
  - 상태 표현은 `WpfStatusBarPanelViewModel`에 두고, WPF status bar XAML은 `IsAnnotationDirty`, `AnnotationSaveStatusText`, `AnnotationSaveStatusToolTip`에 바인딩합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-annotation-save-state-v2.log`
  - `artifacts\logs\tests-20260622-wpf-annotation-save-state-v2.log`
  - `artifacts\logs\visual-20260622-wpf-annotation-save-state-v2.log`
  - `artifacts\ui\wpf-annotation-save-state-20260622.png`
  - `TestWpfShellUndoRedoRestoresAnnotationState`
  - `TestWpfStatusAndLogPanelsDeclareControls`
- 다음:
  - 실제 라벨링 중 사용자가 `저장됨`과 `저장 필요`를 충분히 알아보는지 보고, 필요하면 문구와 색만 조정합니다.

## 2026-06-22 추가 완료 - WPF status bar ViewModel binding

- 완료:
  - 하단 상태바의 Dataset/Python/Model 텍스트를 `WpfStatusBarPanelViewModel` 속성으로 이관했습니다.
  - `WpfStatusBarPanel.xaml`은 `DatasetStatusText`, `PythonStatusText`, `ModelStatusText`를 바인딩으로 표시합니다.
  - 기존 shell의 `SetDatasetStatus`, `SetPythonStatus`, `SetModelStatus` 호출부는 유지하고, 내부 구현만 ViewModel 갱신 우선으로 바꿨습니다.
  - 테스트가 XAML 바인딩과 런타임 ViewModel/TextBlock 동기화를 확인합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-statusbar-viewmodel-binding-v2.log`
  - `artifacts\logs\tests-20260622-wpf-statusbar-viewmodel-binding-v2.log`
  - `artifacts\logs\visual-20260622-wpf-statusbar-viewmodel-binding-v1.log`
  - `artifacts\ui\wpf-statusbar-viewmodel-binding-20260622.png`
  - `TestWpfStatusAndLogPanelsDeclareControls`
- 다음:
  - 남은 직접 UI 갱신은 상태바처럼 작은 단위로 ViewModel/service로 옮깁니다.

## 2026-06-22 추가 완료 - WPF training status ViewModel binding

- 완료:
  - 학습 준비/진행/에포크 상태 텍스트와 진행률/진행 색상을 `WpfTrainingSettingsPanelViewModel` 속성으로 이관했습니다.
  - `WpfTrainingSettingsPanel.xaml`은 `TrainingReadinessText`, `TrainingProgressText`, `TrainingEpochStatusText`, `TrainingProgressValue`, `TrainingProgressIsIndeterminate`, `TrainingReadinessForeground`, `TrainingProgressForeground` 바인딩으로 표시합니다.
  - 학습 시작/중지/준비도 갱신/worker 진행률 갱신 경로가 TextBlock 직접 갱신 대신 ViewModel 갱신 헬퍼를 우선 사용하도록 정리했습니다.
  - 바인딩이 깨졌을 때도 shell helper가 다시 바인딩을 보강하도록 하여 WPF 상태 표시가 한 경로로 유지됩니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-training-status-viewmodel-v2.log`
  - `artifacts\logs\tests-20260622-wpf-training-status-viewmodel-v4.log`
  - `artifacts\logs\visual-20260622-wpf-training-status-viewmodel-v1.log`
  - `artifacts\ui\wpf-training-status-viewmodel-20260622.png`
  - `TestWpfTrainingSettingsPanelDeclaresControls`
  - `TestWpfSettingsViewModelsRoundTrip`
  - `TestWpfTrainingStatusSummariesAreOperatorReadable`
  - `TestWpfTrainingCommandDisablesConflictingActions`
- 다음:
  - 남은 shell 직접 UI 갱신은 후보/객체 선택 상태, command enabled 상태처럼 사용자 체감이 큰 흐름부터 작은 단위로 ViewModel/service로 옮깁니다.

## 2026-06-22 추가 완료 - WPF YOLO status command availability binding

- 완료:
  - YOLO 상태 패널의 `첫 점검`, `설치`, `테스트`, `재시작`, `중지` 버튼 enabled 상태를 `WpfYoloStatusPanelViewModel` 속성으로 이관했습니다.
  - `WpfYoloStatusPanel.xaml`은 각 버튼의 `IsEnabled`를 `IsFirstCheckEnabled`, `IsInstallRequirementsEnabled`, `IsRunSmokeEnabled`, `IsRestartWorkerEnabled`, `IsStopWorkerEnabled`에 바인딩합니다.
  - shell의 `UpdateYoloCommandButtons`는 기존 `WpfWorkflowCommandStateService` 판단 결과를 `YoloStatusViewModel.ApplyWorkflowCommandState`로 전달합니다.
  - 테스트는 ViewModel 상태와 실제 WPF binding 반영을 같이 확인합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-yolo-status-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-yolo-status-command-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-yolo-status-command-vm-v1.log`
  - `artifacts\ui\wpf-yolo-status-command-vm-20260622.png`
  - `TestWpfYoloStatusPanelDeclaresCommandControls`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - 프로젝트 설정 버튼, YOLO 모델 설정 버튼, 학습 설정 버튼의 enabled 상태도 같은 방식으로 각 패널 ViewModel에 분리합니다.

## 2026-06-22 추가 완료 - WPF project config command availability binding

- 완료:
  - 프로젝트 설정 패널의 `적용`, `갱신`, `설정 저장`, `폴더` 버튼 enabled 상태를 `WpfProjectConfigPanelViewModel` 속성으로 이관했습니다.
  - `WpfProjectConfigPanel.xaml`은 `IsApplyRecipeEnabled`, `IsRefreshRecipeListEnabled`, `IsSaveProjectConfigEnabled`, `IsOpenProjectConfigFolderEnabled`에 바인딩합니다.
  - shell의 `UpdateYoloCommandButtons`는 `WpfWorkflowCommandStateService` 결과를 `ProjectConfigViewModel.ApplyWorkflowCommandState`로 전달합니다.
  - 저장 버튼은 입력 중인 recipe 텍스트가 아니라 현재 적용된 recipe와 busy 상태를 기준으로 켜집니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-project-command-vm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-project-command-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-project-command-vm-v1.log`
  - `artifacts\ui\wpf-project-command-vm-20260622.png`
  - `TestWpfProjectConfigPanelDeclaresRecipeControls`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - YOLO 모델 설정 버튼과 학습 설정 버튼의 enabled 상태도 같은 방식으로 각 패널 ViewModel에 분리합니다.

## 2026-06-22 추가 완료 - WPF YOLO model settings command availability binding

- 완료:
  - YOLO 모델 설정 패널의 경로 찾아보기 버튼 5개와 `저장`, `기본값` 버튼 enabled 상태를 `WpfYoloModelSettingsPanelViewModel` 속성으로 이관했습니다.
  - `WpfYoloModelSettingsPanel.xaml`은 `IsBrowsePythonEnabled`, `IsBrowseProjectRootEnabled`, `IsBrowseClientScriptEnabled`, `IsBrowseWeightsEnabled`, `IsBrowseImageRootEnabled`, `IsSaveSettingsEnabled`, `IsResetSettingsEnabled`에 바인딩합니다.
  - shell의 `UpdateYoloCommandButtons`는 `WpfWorkflowCommandStateService` 결과를 `YoloModelSettingsViewModel.ApplyWorkflowCommandState`로 전달합니다.
  - 추론/학습/환경 명령 실행 중에는 모델 경로 변경과 설정 저장/초기화 버튼이 같이 잠기도록 정리했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-yolo-model-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-yolo-model-command-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-yolo-model-command-vm-v1.log`
  - `artifacts\ui\wpf-yolo-model-command-vm-20260622.png`
  - `TestWpfYoloModelSettingsPanelDeclaresPathEditors`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - 학습 설정 버튼의 enabled 상태도 `WpfTrainingSettingsPanelViewModel`으로 분리합니다.

## 2026-06-22 추가 완료 - WPF training settings command availability binding

- 완료:
  - 학습 설정 패널의 `새로고침`, `시작`, `중지` 버튼 enabled 상태를 `WpfTrainingSettingsPanelViewModel` 속성으로 이관했습니다.
  - `WpfTrainingSettingsPanel.xaml`은 `IsRefreshReadinessEnabled`, `IsStartTrainingEnabled`, `IsStopTrainingEnabled`에 바인딩합니다.
  - shell의 `UpdateYoloCommandButtons`는 `WpfWorkflowCommandStateService` 결과를 `TrainingSettingsViewModel.ApplyWorkflowCommandState`로 전달합니다.
  - 학습 실행 중에는 시작/새로고침을 잠그고, 중지 버튼만 켜지는 상태를 ViewModel과 실제 WPF binding 모두에서 검증했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-training-command-vm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-training-command-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-training-command-vm-v1.log`
  - `artifacts\ui\wpf-training-command-vm-20260622.png`
  - `TestWpfTrainingSettingsPanelDeclaresControls`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - 남은 직접 command-state UI 갱신은 추론/큐 버튼 그룹입니다. 이쪽은 ImageQueue/Canvas/top-toolbar 소유권을 나눠서 처리합니다.

## 2026-06-22 추가 완료 - WPF detection and queue command availability binding

- 완료:
  - 상단 `현재 검사` 버튼의 enabled 상태를 `WpfLabelingShellViewModel.IsCurrentImageDetectionEnabled`로 이관했습니다.
  - 이미지 큐의 `선택 검사`, `일괄 검사`, `실패 재시도`, `일괄 중지` 버튼 enabled 상태를 `WpfImageQueuePanelViewModel` 속성으로 이관했습니다.
  - `WpfLabelingShellWindow.xaml`과 `WpfImageQueuePanel.xaml`은 각 버튼 `IsEnabled`를 ViewModel에 바인딩합니다.
  - shell의 `UpdateYoloCommandButtons`는 `WpfWorkflowCommandStateService` 결과를 shell/queue ViewModel로 전달하고, 일반 경로에서 직접 버튼을 켜고 끄지 않습니다.
  - 라벨링 모드에서는 추론 버튼이 잠기고, 추론 검토 모드에서는 현재/큐 추론 버튼이 열리며, 배치 실행 중에는 중지 버튼만 열리는 상태를 테스트로 고정했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-detection-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-detection-command-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-detection-command-vm-v1.log`
  - `artifacts\ui\wpf-detection-command-vm-20260622.png`
  - `TestWpfLabelingShellWindowConstructs`
  - `TestWpfTrainingCommandButtonState`
  - `TestWpfWorkflowModeSeparatesLabelingAndInference`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - 남은 command-state 직접 UI 갱신은 캔버스 보조 버튼, YOLO fix 버튼, 일부 선택 상태 버튼입니다. viewer/OpenGL 코드는 제외하고 app-shell WPF ViewModel 경계만 계속 좁힙니다.

## 2026-06-22 추가 완료 - WPF canvas command availability binding

- 완료:
  - 캔버스 보조 버튼 `Fit`, `1:1`, `Pan`, `Focus`, `AI Reset`의 enabled 상태를 `WpfCanvasPanelViewModel`로 이관했습니다.
  - `WpfCanvasPanel.xaml`은 `IsFitEnabled`, `IsActualSizeEnabled`, `IsPanEnabled`, `IsFocusCandidateEnabled`, `IsResetAiOverlayEnabled`에 바인딩합니다.
  - shell의 `UpdateCanvasCommandButtons`는 이미지/선택 후보/AI 후보 상태를 계산한 뒤 `CanvasPanelControl.ViewModel.SetCommandAvailability`로 전달합니다.
  - 일반 경로에서 캔버스 보조 버튼을 직접 `.IsEnabled`로 켜고 끄지 않도록 테스트를 추가했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-canvas-command-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-canvas-command-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-canvas-command-vm-v1.log`
  - `artifacts\ui\wpf-canvas-command-vm-20260622.png`
  - `TestWpfCanvasPanelDeclaresViewerCommands`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - 남은 직접 command-state UI 갱신은 YOLO guide fix 버튼과 일부 선택/모드 상태 버튼입니다. viewer/OpenGL/ImageCanvas는 계속 제외합니다.

## 2026-06-22 추가 완료 - WPF YOLO guide fix command availability binding

- 완료:
  - Guide 탭 YOLO 보정 버튼 `클래스 등록`, `라벨링 시작`, `데이터셋 점검`의 enabled 상태를 `WpfLearningWorkflowPanelViewModel`로 이관했습니다.
  - `WpfLearningWorkflowPanel.xaml`은 `IsYoloFixClassesEnabled`, `IsYoloFixLabelsEnabled`, `IsYoloFixDatasetEnabled`에 바인딩합니다.
  - shell의 `RefreshYoloTrainingStepCompletion`은 이미지 존재 여부를 계산한 뒤 `LearningWorkflowViewModel.SetYoloFixActionAvailability`로 전달합니다.
  - 기존 정책은 유지했습니다. 클래스/데이터셋 보정은 항상 가능하고, 라벨링 시작은 이미지가 있을 때만 가능합니다.
  - 일반 경로에서 guide fix 버튼을 직접 `.IsEnabled`로 켜고 끄지 않도록 테스트를 추가했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-yolo-guide-fix-command-vm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-yolo-guide-fix-command-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-yolo-guide-fix-command-vm-v1.log`
  - `artifacts\ui\wpf-yolo-guide-fix-command-vm-20260622.png`
  - `TestWpfLearningWorkflowPanelDeclaresEducationModesAndTools`
- 다음:
  - 남은 직접 UI 상태 제어는 이미지 큐의 선택 열기 버튼, 상단 모드 버튼 상태, 일부 tab/selection helper입니다. 기능 변화 없이 ViewModel 경계를 계속 좁힙니다.

## 2026-06-22 추가 완료 - WPF image queue selected-open command binding

- 완료:
  - 이미지 큐의 `선택 이미지를 다시 열기` 버튼 enabled 상태를 `WpfImageQueuePanelViewModel.IsOpenSelectedImageEnabled`로 이관했습니다.
  - `WpfImageQueuePanel.xaml`은 `OpenSelectedQueueImageButton.IsEnabled`를 ViewModel에 바인딩합니다.
  - shell의 `UpdateSelectedQueueImageButton`은 선택 행의 이미지 경로와 파일 존재 여부를 계산한 뒤 `ImageQueuePanelControl.ViewModel.SetSelectedImageAvailability`로 전달합니다.
  - 실제 큐 선택 흐름에서 선택 이미지가 열리고 버튼이 ViewModel 바인딩으로 활성화되는지 테스트했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-image-queue-selected-open-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-image-queue-selected-open-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-image-queue-selected-open-vm-v1.log`
  - `artifacts\ui\wpf-image-queue-selected-open-vm-20260622.png`
  - `TestWpfLabelingShellWindowConstructs`
  - `TestWpfSettingsViewModelsRoundTrip`
  - `TestWpfImageQueueClickLoadsCanvas`
- 다음:
  - 남은 직접 UI 상태 제어는 상단 라벨링/추론 모드 버튼 상태와 일부 fallback/selection helper입니다. 기능 변화 없이 ViewModel 경계를 계속 좁힙니다.

## 2026-06-22 추가 완료 - WPF raster mask OpenGL texture preview 전환

- 완료:
  - WPF raster mask preview를 mask-region polygon 변환에서 전용 OpenGL texture overlay로 바꿨습니다.
  - `RoiImageCanvasMaskOverlay`를 추가해 mask byte buffer, image-pixel bounds, color, opacity, render version을 캔버스 ViewModel로 전달합니다.
  - `RoiImageCanvasViewModel`이 mask buffer를 RGBA `GL_TEXTURE_2D`로 업로드하고, 기존 AI detection overlay와 같은 이미지 픽셀 좌표 변환 기준으로 그립니다.
  - 이미지 전환 시 detection/polygon뿐 아니라 mask overlay도 비워 이전 이미지 texture가 남는 상황을 막았습니다.
  - visual smoke seed에 raster mask를 추가해 실제 캡처에서 채워진 mask texture를 확인할 수 있게 했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-mask-texture-v3.log`
  - `artifacts\logs\build-20260622-wpf-mask-texture-v4.log`
  - `artifacts\logs\tests-20260622-wpf-mask-texture-v2.log`
  - `artifacts\logs\visual-20260622-wpf-mask-texture-objects-v1.log`
  - `artifacts\ui\wpf-mask-texture-objects-20260622.png`
  - `TestWpfBrushEraserShellInputCreatesMaskSegmentation`
  - `TestWpfAnnotationToolVerificationMatrix`
- 다음:
  - mask object 선택/상세 편집 UX는 아직 별도 작업입니다. 우선 실제 brush/eraser 사용 흐름을 보고 필요한 조작만 추가합니다.
  - dirty bounds 기반 `TexSubImage2D` 최적화는 2026-06-22 후속 작업으로 완료되었습니다.
  - brush cursor/preview radius 표시는 2026-06-22 후속 작업으로 추가 완료되었습니다.

## 2026-06-22 추가 완료 - WPF brush/eraser cursor radius preview

- 완료:
  - WPF/OpenGL 캔버스에 image-pixel hover 이벤트(`ImagePointHovered`)를 추가했습니다.
  - 브러시/지우개 선택 시 마우스 위치에 현재 반경을 원형 preview로 표시합니다.
  - 브러시는 현재 선택 클래스 색을 사용하고, 지우개는 주황색 preview로 구분합니다.
  - preview는 OpenGL 화면 좌표에 그리지만 중심/반경은 원본 이미지 픽셀 좌표에서 계산합니다.
  - 마우스가 이미지 밖으로 나가거나 mask tool을 종료하면 preview가 즉시 사라집니다.
  - visual smoke에 `--annotation-tool=brush` 옵션을 추가해 브러시 preview 렌더링을 캡처할 수 있게 했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-brush-cursor-preview-v2.log`
  - `artifacts\logs\build-20260622-wpf-brush-cursor-preview-v3.log`
  - `artifacts\logs\tests-20260622-wpf-brush-cursor-preview-v2.log`
  - `artifacts\logs\tests-20260622-wpf-brush-cursor-preview-v3.log`
  - `artifacts\logs\visual-20260622-wpf-brush-cursor-preview-v1.log`
  - `artifacts\ui\wpf-brush-cursor-preview-20260622.png`
  - `TestWpfBrushEraserShellInputCreatesMaskSegmentation`
  - `TestWpfAnnotationToolVerificationMatrix`
- 다음:
  - mask object 선택 표시는 2026-06-22 후속 작업으로 추가 완료되었습니다.
  - dirty bounds 기반 `TexSubImage2D` 최적화는 2026-06-22 후속 작업으로 완료되었습니다.

## 2026-06-22 추가 완료 - WPF mask object selection highlight

- 완료:
  - Object Review에서 raster mask 행을 선택하면 `RoiImageCanvasMaskOverlay`에 선택 상태와 캔버스 배지 라벨을 전달합니다.
  - 선택 변경 시 캔버스 mask/polygon overlay를 즉시 갱신합니다.
  - mask texture는 기존대로 이미지 위에 반투명 texture로 먼저 그리고, 선택 코너/배지는 별도 상위 overlay pass에서 다시 그려 AI/폴리곤/ROI 표시 사이에 묻히지 않게 했습니다.
  - mask/polygon 같은 non-ROI 객체를 선택할 때 남아 있던 ROI 편집 핸들을 정리할 수 있도록 `ClearRoiSelection`을 추가했습니다.
  - 캔버스 배지 번호는 Object Review 표시 순번과 맞춥니다. 예: Object Review `3. Mask` 선택 시 캔버스도 `MASK 3`으로 표시합니다.
  - visual smoke에 `--select-mask-object` 옵션을 추가해 mask 선택 강조 화면을 반복 캡처할 수 있게 했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-mask-selection-v1.log`
  - `artifacts\logs\tests-20260622-wpf-mask-selection-v1.log`
  - `artifacts\logs\visual-20260622-wpf-mask-selection-v1.log`
  - `artifacts\ui\wpf-mask-selected-object-20260622-v3.png`
  - `TestWpfBrushEraserShellInputCreatesMaskSegmentation`
  - `TestWpfAnnotationToolVerificationMatrix`
- 다음:
  - mask 이동과 dirty bounds 기반 partial texture update는 2026-06-22 후속 작업으로 완료되었습니다.
  - mask 리사이즈/형상 편집은 아직 정의하지 않습니다. 실제 교육 흐름에서 필요한 조작이 확인되면 그때 추가합니다.

## 2026-06-22 추가 완료 - WPF segment object edit and mask partial texture update

- 완료:
  - Object Review에서 선택한 raster mask를 Select 도구로 클릭/드래그하면 원본 이미지 픽셀 좌표 기준으로 이동합니다.
  - 이동 시작 시 history snapshot을 쌓고, mouse release에서 한 번만 edit history를 확정합니다.
  - 폴리곤 세그먼트는 선택한 점을 image-pixel hit-test로 잡아 드래그 이동할 수 있습니다.
  - 선택된 폴리곤 점은 OpenGL 캔버스에서 더 큰 노란 point marker와 halo로 표시됩니다.
  - `RoiImageCanvasViewModel.ImagePointReleased` 이벤트를 추가해 brush stroke와 selected-object drag commit을 분리했습니다.
  - raster mask overlay가 `DirtyBounds`를 들고, 같은 크기/위치/color/opacity texture에서는 `glTexSubImage2D`로 변경 영역만 갱신합니다.
  - mask bounds가 바뀌거나 texture 조건이 달라지는 경우에는 기존처럼 전체 texture upload로 fallback합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-segment-edit-v1.log`
  - `artifacts\logs\tests-20260622-wpf-segment-edit-v1.log`
  - `artifacts\logs\visual-20260622-wpf-segment-edit-v1.log`
  - `artifacts\ui\wpf-segment-edit-20260622.png`
  - `TestWpfPolygonAnnotationService`
  - `TestWpfPolygonShellInputCreatesSegmentation`
  - `TestWpfMaskAnnotationService`
  - `TestWpfBrushEraserShellInputCreatesMaskSegmentation`
  - `TestWpfAnnotationToolVerificationMatrix`
- 다음:
  - polygon point delete/add-on-edge, mask reshape/scale은 아직 넣지 않습니다. 실제 튜토리얼 라벨링에서 꼭 필요한 조작이 확인될 때 추가합니다.
  - 큰 이미지/수천 장 폴더에서 brush drag 체감이 다시 느려지면 UI thread 구간과 OpenGL upload 구간을 분리해 계측합니다.

## 2026-06-22 추가 완료 - WPF workflow mode button ViewModel binding

- 완료:
  - 상단 `라벨링` / `추론 검토` 버튼의 enabled/active 상태를 `WpfLabelingShellViewModel`로 이동했습니다.
  - 활성 모드 색상은 코드비하인드에서 브러시를 직접 칠하지 않고 `WorkflowModeToolbarButtonStyle`의 WPF DataTrigger로 처리합니다.
  - `UpdateWorkflowModeUi()`는 현재 모드와 작업 중 여부만 ViewModel에 전달합니다.
  - 작업 중에는 현재 활성 모드 버튼은 유지하고, 반대 모드 버튼만 잠겨 사용자가 현재 위치를 잃지 않게 했습니다.
  - 코드비하인드의 `ApplyWorkflowModeButtonState` 직접 UI 조작 경로를 제거했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-workflow-mode-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-workflow-mode-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-workflow-mode-vm-v1.log`
  - `artifacts\ui\wpf-workflow-mode-vm-20260622.png`
  - `TestWpfLabelingShellWindowConstructs`
  - `TestWpfSettingsViewModelsRoundTrip`
  - `TestWpfWorkflowModeSeparatesLabelingAndInference`
- 다음:
  - top-level mode button 직접 상태 조작은 완료되었습니다.
  - 다음 WPF migration cleanup은 후보/객체 검토 영역의 남은 직접 텍스트/선택 갱신을 작은 단위로 ViewModel/Presenter에 넘깁니다.

## 2026-06-22 추가 완료 - WPF candidate comparison ViewModel binding

- 완료:
  - 후보 검토 탭의 `AI 후보 / 현재 라벨 / IoU` 비교 카드 표시 상태와 텍스트를 `WpfCandidateReviewPanelViewModel`로 이동했습니다.
  - shell 코드비하인드는 `WpfCandidateReviewPresenter.BuildComparison(...)` 결과를 `CandidateReviewViewModel.SetComparison(...)`에 전달만 합니다.
  - 선택 후보가 없을 때는 `CandidateReviewViewModel.ClearComparison()`으로 비교 카드를 숨깁니다.
  - 중복 후보 경계선/IoU 강조 색은 코드비하인드 브러시 직접 지정 대신 `WpfCandidateReviewPanel.xaml`의 DataTrigger로 처리합니다.
  - shell의 `CandidateComparisonPanel.Visibility`, `CandidateCompare*Text.Text`, `CandidateCompareOverlapText.Foreground`, `CandidateComparisonPanel.BorderBrush` 직접 갱신 경로를 제거했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-candidate-comparison-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-candidate-comparison-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-candidate-comparison-vm-v1.log`
  - `artifacts\ui\wpf-candidate-comparison-vm-20260622.png`
  - `TestWpfCandidateRowsShowVisualStatus`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - 후보 비교 카드 직접 UI 갱신은 완료되었습니다.
  - 다음 후보/객체 검토 cleanup은 Object Review의 선택/클래스 편집 상태 또는 candidate overlay summary text helper를 Presenter/ViewModel로 더 분리하는 쪽입니다.

## 2026-06-22 추가 완료 - WPF detection result overlay ViewModel binding

- 완료:
  - 캔버스 상단 `AI 검출 결과` 오버레이의 표시 여부, 요약 문구, 선택 후보 문구, 상세 후보 목록, 상태 키를 `WpfCanvasPanelViewModel`로 이동했습니다.
  - shell 코드비하인드는 후보 목록을 계산한 뒤 `CanvasPanelControl.ViewModel.SetDetectionOverlay(...)` 또는 `ClearDetectionOverlay()`만 호출합니다.
  - 확정 가능/중복/검토 필요 상태별 배경, 경계선, 요약 강조색, 선택 후보 chip 색상은 `WpfCanvasPanel.xaml` DataTrigger로 처리합니다.
  - shell의 `DetectionResultOverlay.Visibility`, `DetectionOverlay*Text.Text`, `DetectionOverlaySummaryText.Foreground`, `DetectionOverlaySelectedBorder` 색상 직접 갱신 경로를 제거했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-detection-overlay-vm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-detection-overlay-vm-v2.log`
  - `artifacts\logs\visual-20260622-wpf-detection-overlay-vm-v1.log`
  - `artifacts\ui\wpf-detection-overlay-vm-20260622.png`
  - `TestWpfCanvasDetectionOverlayUsesThemeResources`
  - `TestWpfDetectionCandidatesRenderAsDetectionOverlays`
- 다음:
  - AI 검출 결과 오버레이의 직접 UI 갱신은 완료되었습니다.
  - 다음 WPF cleanup은 Object Review 선택/클래스 편집 상태 또는 Queue filter 버튼 활성/선택 색상 직접 조작을 ViewModel 바인딩으로 더 넘기는 쪽입니다.

## 2026-06-22 추가 완료 - WPF image queue quick filter ViewModel binding

- 완료:
  - 좌측 이미지 큐 빠른 필터의 카운트 텍스트와 선택 활성 상태를 `WpfImageQueuePanelViewModel`로 이동했습니다.
  - `전체/후보/실패/확정/스킵/없음` 버튼은 shell 코드비하인드가 브러시를 직접 칠하지 않고, `QueueQuickFilterButtonStyle`의 `Tag` 바인딩과 DataTrigger로 선택 강조를 표시합니다.
  - `UpdateQueueQuickFilterButtons()`는 현재 필터와 상태별 카운트만 계산해 `SetQuickFilterState(...)`로 전달합니다.
  - shell의 `ApplyQueueQuickFilterButtonState` 직접 UI 조작 경로를 제거했습니다.
  - 테스트가 XAML 바인딩, shell 직접 조작 제거, 실제 WPF 창 클릭 후 `Tag` 활성 상태와 카운트 텍스트 변경을 함께 검증합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-queue-filter-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-queue-filter-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-queue-filter-vm-v1.log`
  - `artifacts\ui\wpf-queue-filter-vm-20260622.png`
  - `TestWpfSettingsViewModelsRoundTrip`
  - `TestWpfImageQueuePresentsRowStatusWithIcons`
- 다음:
  - 이미지 큐 빠른 필터의 텍스트/선택 강조 직접 UI 갱신은 완료되었습니다.
  - 다음 WPF cleanup은 Object Review 선택/클래스 편집 상태 또는 후보/객체 리뷰의 남은 표시 helper를 ViewModel/Presenter로 더 분리하는 쪽입니다.

## 2026-06-22 추가 완료 - WPF object review class editor ViewModel sync

- 완료:
  - Object Review 클래스 콤보의 `SelectionChanged` 이벤트 경로를 제거했습니다.
  - 클래스 콤보는 `SelectedClassName` TwoWay 바인딩만으로 상태를 갱신하고, 적용 버튼 enabled 상태는 `WpfObjectReviewPanelViewModel.RefreshActionState()`에서 처리합니다.
  - 선택한 객체의 현재 클래스 동기화는 shell helper가 직접 `SelectedClassName`을 고르지 않고 `WpfObjectReviewPanelViewModel.SetSelectedObjectClass(...)`로 이동했습니다.
  - shell의 `ObjectClassBox_SelectionChanged`와 `SelectObjectClass` 직접 에디터 선택 helper를 제거했습니다.
  - 테스트가 XAML 이벤트 제거, shell helper 제거, ViewModel 클래스 동기화 API, 실제 Object Review 탭 화면을 함께 검증합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-object-review-class-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-object-review-class-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-object-review-class-vm-v1.log`
  - `artifacts\ui\wpf-object-review-class-vm-20260622.png`
  - `TestWpfSettingsViewModelsRoundTrip`
  - `TestWpfObjectReviewSummarizesLabels`
- 다음:
  - Object Review 클래스 에디터의 ComboBox 선택 이벤트 의존은 제거되었습니다.
  - 다음 WPF cleanup은 Object Review의 표시 row 구성/선택 후속 동작을 Presenter/ViewModel로 더 이동하거나, Candidate/Object Review 공통 표시 helper를 정리하는 쪽입니다.

## 2026-06-22 추가 완료 - WPF candidate selection review ViewModel sync

- 완료:
  - Candidate Review에서 후보 선택 시 상세 문구와 비교 카드가 따로 갱신되던 경로를 `WpfCandidateReviewPanelViewModel.ApplySelectionReview(...)` 하나로 묶었습니다.
  - 후보가 없으면 상세 문구와 비교 카드 숨김을 한 번에 적용하고, 후보가 있으면 같은 후보 기준으로 상세/비교 텍스트를 함께 갱신합니다.
  - shell의 `SetCandidateDetailText`와 `UpdateCandidateComparisonPanel` helper를 제거했습니다.
  - `GetSelectedCandidate()`는 `CandidateReviewViewModel.SelectedCandidate`만 원천으로 사용하게 하고, `CandidateListBox.SelectedItem` 직접 fallback을 제거했습니다.
  - 테스트가 ViewModel 선택 리뷰 적용, shell helper 제거, ListBox fallback 제거, 실제 Candidate Review 탭 화면을 함께 검증합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-candidate-selection-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-candidate-selection-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-candidate-selection-vm-v1.log`
  - `artifacts\ui\wpf-candidate-selection-vm-20260622.png`
  - `TestWpfCandidateRowsShowVisualStatus`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - Candidate Review 선택 상세/비교 상태의 분리 갱신은 완료되었습니다.
  - 다음 WPF cleanup은 Candidate/Object Review row presentation helper를 더 Presenter 쪽으로 모으거나, 후보 navigation enabled 상태를 별도 ViewModel 속성으로 분리하는 쪽입니다.

## 2026-06-22 추가 완료 - WPF candidate navigation state ViewModel split

- 완료:
  - Candidate Review의 `이전`, `다음`, `초점` 버튼 enabled 상태를 `IsSkipSelectedEnabled`에서 분리했습니다.
  - `WpfCandidateReviewPanelViewModel`에 `IsPreviousCandidateEnabled`, `IsNextCandidateEnabled`, `IsFocusCandidateEnabled`와 `SetNavigationState(...)`를 추가했습니다.
  - `이전/다음`은 표시 후보가 2개 이상이고 선택 후보가 있을 때만 enabled가 됩니다.
  - `초점`은 선택 후보가 있으면 enabled가 되고, `스킵`은 기존대로 선택 후보 스킵 가능 여부만 표현합니다.
  - 키보드 `N/P` 이동도 후보가 1개뿐일 때는 다른 후보 이동을 시도하지 않게 정리했습니다.
  - 테스트가 XAML 바인딩, ViewModel navigation state, 실제 후보 2개 상태에서 이전/다음/초점 enabled를 함께 검증합니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-candidate-navigation-vm-v1.log`
  - `artifacts\logs\tests-20260622-wpf-candidate-navigation-vm-v1.log`
  - `artifacts\logs\visual-20260622-wpf-candidate-navigation-vm-v1.log`
  - `artifacts\ui\wpf-candidate-navigation-vm-20260622.png`
  - `TestWpfCandidateReviewPanelDeclaresNavigation`
  - `TestWpfCandidateRowsShowVisualStatus`
  - `TestWpfSettingsViewModelsRoundTrip`
- 다음:
  - Candidate Review navigation enabled 상태 분리는 완료되었습니다.
  - 다음 WPF cleanup은 Candidate/Object Review row presentation helper를 더 Presenter 쪽으로 모으거나, Review 패널 공통 행 스타일/상태 표시를 정리하는 쪽입니다.

## 2026-06-22 추가 완료 - WPF image memory ownership cleanup

- 완료:
  - WPF 이미지 로드 경로에서 OpenGL 업로드 후 `CDisplayManager.ImageSrc = imageMat.Clone()`으로 Mat를 한 번 더 복제하던 경로를 제거했습니다.
  - `RoiImageCanvasViewModel.LoadImage(...)`는 Mat를 보관하지 않고 texture 업로드에만 사용하므로, 이후 `imageMat` 자체를 `CDisplayManager`에 넘기고 shell의 로컬 소유권을 `imageMat = null`로 비웁니다.
  - `ImageSpace`는 기존 설계대로 활성 `Bitmap` 참조를 보관합니다. WPF shell은 활성 Bitmap 하나를 소유하고, ImageSpace는 같은 객체를 참조합니다.
  - adjacent decode cache에 총 메모리 예산 `ImageDecodeCacheMaxBytes = 64MB`를 추가했습니다.
  - cache 항목은 Bitmap+Mat 예상 바이트를 계산하고, count 제한 또는 총량 제한을 넘으면 오래된 항목부터 Dispose합니다.
  - `CDisplayManager.ImageSrc` ownership 테스트를 clone 기반에서 직접 소유권 이전 기반으로 갱신했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-image-memory-v1.log`
  - `artifacts\logs\tests-20260622-wpf-image-memory-v1.log`
  - `artifacts\logs\visual-20260622-wpf-image-memory-v1.log`
  - `artifacts\ui\wpf-image-memory-20260622.png`
  - `TestWpfImageQueueClickUsesLightweightLoadPath`
  - `TestWpfImageQueuePreloadsAdjacentDecodes`
  - `TestDisplayManagerImageSourceOwnership`
- 다음:
  - 앱 shell의 확실한 중복 Mat clone은 제거되었습니다.
  - `CanvasImageLoader.UploadMatAsTexture(...)` 내부의 tile `Mat.Clone()`은 OpenGL 포인터 안정성과 stride/continuity 문제가 있어 별도 계측 후 조심해서 최적화합니다.
  - 큰 실제 이미지 폴더에서 cache 64MB 예산이 너무 작거나 크면 체감 속도와 working set 기준으로 조정합니다.

## 2026-06-22 추가 완료 - OpenGL texture upload conditional Mat clone

- 완료:
  - `CanvasImageLoader.UploadMatAsTexture(...)`의 OpenGL texture upload clone 정책을 `UseContinuousMatForTextureUpload(...)` helper로 한 곳에 모았습니다.
  - 전체 이미지처럼 연속 메모리인 `Mat`은 OpenGL 업로드 시 별도 `Clone()` 없이 원본 버퍼를 바로 사용합니다.
  - `SubMat`처럼 stride가 있는 비연속 버퍼는 `glTexSubImage2D`에 넘기기 전 compact buffer가 필요하므로 clone을 유지합니다.
  - 이 경로는 `TryReplaceSingleTexture`/`AddTexture` 호출 즉시 OpenGL로 업로드하고, helper가 만든 임시 compact clone만 즉시 Dispose합니다.
  - WPF 이미지 클릭 경로의 중복 Mat clone 제거와 OpenGL 업로드 clone 축소가 서로 섞이지 않도록 각각 테스트로 고정했습니다.
- 검증:
  - `artifacts\logs\build-20260622-canvas-upload-clone-v1.log`
  - `artifacts\logs\tests-20260622-canvas-upload-clone-v1.log`
  - `artifacts\logs\visual-20260622-canvas-upload-clone-v1.log`
  - `artifacts\ui\canvas-upload-clone-20260622.png`
  - `TestCanvasImageLoaderUsesContinuousUploadMatWithoutClone`
  - `TestWpfImageQueueClickUsesLightweightLoadPath`
- 다음:
  - OpenGL 업로드 쪽의 불필요한 full-image clone은 제거되었습니다.
  - tile/SubMat clone은 stride safety 때문에 의도적으로 남겼습니다. 실제 대형 이미지에서 memory/latency 병목으로 확인될 때 `GL_UNPACK_ROW_LENGTH` 또는 PBO upload 같은 OpenGL 전용 최적화를 별도 실험합니다.
  - 다음 메모리 점검은 실제 큰 폴더에서 working set, image switch latency, decode cache hit/miss를 함께 보는 쪽입니다.

## 2026-06-22 추가 완료 - WPF real-folder memory/perf smoke diagnostics

- 완료:
  - `WpfLabelingShellWindow.GetImageDecodeCacheDiagnostics()`를 추가해 이미지 decode cache의 count, bytes, hits, misses, stores, evictions를 확인할 수 있게 했습니다.
  - cache hit/miss/store/eviction 카운터를 WPF 이미지 로드/cache 경로에 추가했습니다.
  - `--wpf-queue-click-perf` 스모크가 `--folder`, `--count`, `--settle-ms` 인자를 받아 실제 이미지 폴더를 바로 재생할 수 있게 했습니다.
  - 성능 스모크 출력에 queue click min/avg/max, sample list, process working set start/end/peak/delta, decode cache diagnostics를 추가했습니다.
  - 테스트 인자 파서가 `--name=value`와 `--name value` 형식을 모두 처리하도록 보강했습니다.
- 실제 폴더 측정:
  - 명령: `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --folder "C:\Git\yolov5\data\train\images" --count 24 --settle-ms 80`
  - 결과: 23회 전환, min 25.1 ms, avg 39.2 ms, max 131.0 ms
  - 첫 전환 131.0 ms 이후 대부분 25~49 ms 범위였고, 한 번 64.8 ms가 있었습니다.
  - working set: 198.6 MB -> 216.3 MB, delta +17.7 MB
  - decode cache: count 8/8, bytes 495.5 KB/64 MB, hits 23, misses 2, stores 48, evictions 17
- 검증:
  - `artifacts\logs\build-20260622-wpf-memory-perf-v2.log`
  - `artifacts\logs\tests-20260622-wpf-memory-perf-v2.log`
  - `artifacts\logs\perf-20260622-wpf-real-folder-memory-v2.log`
  - `artifacts\logs\visual-20260622-wpf-memory-perf-v1.log`
  - `artifacts\ui\wpf-memory-perf-20260622.png`
  - `TestWpfImageQueuePreloadsAdjacentDecodes`
- 다음:
  - 현재 샘플 폴더 기준 평균 전환은 50 ms 아래입니다.
  - max 131 ms 첫 전환은 OpenGL/queue/cache warm-up 영향으로 보입니다. 다음 성능 루프에서는 cold switch와 warm switch를 분리해 리포트하는 것이 좋습니다.
  - 실제 대형 이미지 폴더에서도 같은 명령으로 측정하고, working set delta가 계속 누적되는지 확인합니다.

## 2026-06-22 추가 완료 - WPF queue cold/warm switch perf split

- 완료:
  - `--wpf-queue-click-perf` 출력에 첫 전환 `first` 값을 추가했습니다.
  - 첫 전환을 제외한 반복 사용 구간을 `WPF queue warm perf`로 따로 출력합니다.
  - working set `peak` 계산에 마지막 `end` 값을 반영해 `end > peak`처럼 보이는 계측 표시 버그를 수정했습니다.
  - 테스트가 `firstSwitchElapsed`와 `WPF queue warm perf:` 출력 포맷을 확인하도록 보강했습니다.
- 실제 폴더 측정:
  - 명령: `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --folder "C:\Git\yolov5\data\train\images" --count 24 --settle-ms 80`
  - 전체: 23회 전환, first 124.1 ms, min 22.7 ms, avg 38.4 ms, max 124.1 ms
  - warm: 22회 전환, min 22.7 ms, avg 34.5 ms, max 49.7 ms
  - working set: 198.6 MB -> 212.4 MB, peak 212.4 MB, delta +13.8 MB
  - decode cache: count 8/8, bytes 495.5 KB/64 MB, hits 23, misses 2, stores 48, evictions 17
- 검증:
  - `artifacts\logs\build-20260622-wpf-perf-cold-warm-v2.log`
  - `artifacts\logs\tests-20260622-wpf-perf-cold-warm-v2.log`
  - `artifacts\logs\perf-20260622-wpf-real-folder-cold-warm-v2.log`
  - `TestWpfImageQueuePreloadsAdjacentDecodes`
- 다음:
  - 반복 클릭 구간은 현재 샘플 폴더 기준 50 ms 이하 목표를 만족합니다.
  - 남은 병목은 첫 전환 124.1 ms입니다. 다음 루프에서는 첫 전환에서 OpenGL texture replace, queue selection binding, first cache take, layout/update 중 어디가 큰지 단계별 stopwatch를 넣어 좁힙니다.

## 2026-06-22 추가 완료 - WPF queue image switch step timers

- 완료:
  - `WpfLabelingShellWindow.LastImageLoadDiagnostics`를 추가해 마지막 이미지 로드의 단계별 시간을 확인할 수 있게 했습니다.
  - `TryLoadImage(...)` 내부를 cache/decode, OpenGL upload, OpenGL refresh, active state transfer, annotation reset, queue populate, review/status refresh, preload schedule 단계로 나눠 기록합니다.
  - `--wpf-queue-click-perf` 스모크가 첫 전환과 가장 느린 warm 전환의 세부 단계 시간을 출력합니다.
  - 성능 스모크에 `image commit perf`를 추가했습니다. 이 값은 `grid.SelectedItem = item`이 끝나 active image가 바뀐 시점입니다.
  - dispatcher pump/render settle 시간도 `selection-set`과 `dispatcher-pump`로 분리해 출력합니다.
- 실제 폴더 측정:
  - 명령: `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --folder "C:\Git\yolov5\data\train\images" --count 24 --settle-ms 80`
  - 전체 settled: 23회, first 136.6 ms, avg 39.1 ms, max 136.6 ms
  - warm settled: 22회, min 20.6 ms, avg 34.7 ms, max 53.9 ms
  - image commit: first 30.6 ms, warm avg 14.5 ms, warm max 22.6 ms
  - 첫 전환 내부 로드: total 28.3 ms, cache hit, decode 0.3 ms, upload 8.0 ms, review/status refresh 19.6 ms
  - 첫 전환 외부: selection-set 30.6 ms, dispatcher-pump 106.0 ms
  - slow warm 내부 로드: total 11.0 ms, upload 0.4 ms, review/status refresh 10.3 ms
  - slow warm 외부: selection-set 12.3 ms, dispatcher-pump 41.5 ms
  - working set: 198.7 MB -> 212.9 MB, delta +14.2 MB
  - decode cache: count 8/8, bytes 495.5 KB/64 MB, hits 23, misses 2, stores 48, evictions 17
- 검증:
  - `artifacts\logs\build-20260622-wpf-perf-step-timers-v3.log`
  - `artifacts\logs\tests-20260622-wpf-perf-step-timers-v3.log`
  - `artifacts\logs\perf-20260622-wpf-real-folder-step-timers-v3.log`
  - `TestWpfImageQueuePreloadsAdjacentDecodes`
- 결론:
  - 이미지 전환 commit 자체는 현재 샘플 폴더 기준 첫 전환 30.6 ms, warm max 22.6 ms로 50 ms 목표를 만족합니다.
  - 기존 100 ms 이상 수치는 대부분 dispatcher pump/render/layout settle 시간입니다.
  - 다음 최적화는 실제 사용자 화면에서 settle 지연이 보이는지 확인한 뒤, 보이면 DataGrid selection visual/layout 또는 review/status refresh 바인딩 비용을 줄이는 방향입니다.

## 2026-06-22 추가 완료 - WPF queue visible/settled perf split

- 완료:
  - `--wpf-queue-click-perf` 스모크의 기본 click perf를 `metric=visible` 기준으로 정리했습니다.
  - visible 기준은 `grid.SelectedItem`으로 active image가 바뀌고 `Render` 우선순위 drain이 끝난 시점입니다.
  - full dispatcher settled 기준은 별도 `WPF queue settled perf`로 분리했습니다.
  - dispatcher drain은 `render-drain`, `background-drain`, `idle-drain`으로 나눠 출력합니다.
- 실제 폴더 측정:
  - 명령: `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-build -- --wpf-queue-click-perf --folder "C:\Git\yolov5\data\train\images" --count 24 --settle-ms 80`
  - visible: 23회 전환, first 44.4 ms, min 17.9 ms, avg 25.1 ms, max 44.4 ms
  - warm visible: 22회 전환, min 17.9 ms, avg 24.2 ms, max 36.1 ms
  - settled: first 152.1 ms, warm avg 67.1 ms, warm max 160.0 ms
  - image commit: first 37.4 ms, warm avg 18.1 ms, warm max 28.4 ms
  - 첫 전환 outer: selection-set 37.4 ms, render-drain 7.0 ms, background-drain 104.2 ms, idle-drain 3.0 ms
  - slow warm outer: selection-set 24.6 ms, render-drain 11.6 ms, background-drain 43.6 ms, idle-drain 1.2 ms
  - working set: 199.8 MB -> 214.0 MB, delta +14.2 MB
  - decode cache: count 8/8, bytes 495.5 KB/64 MB, hits 23, misses 2, stores 48, evictions 17
- 검증:
  - `artifacts\logs\build-20260622-wpf-perf-visible-settled-v1.log`
  - `artifacts\logs\tests-20260622-wpf-perf-visible-settled-v1.log`
  - `artifacts\logs\perf-20260622-wpf-real-folder-visible-settled-v1.log`
  - `TestWpfImageQueuePreloadsAdjacentDecodes`
- 결론:
  - 이미지 전환 visible 기준은 현재 샘플 폴더에서 first/warm 모두 50 ms 목표를 만족합니다.
  - settled 지연은 대부분 Background 우선순위 drain입니다. 실제 화면에서 눈에 보이는 이미지 전환 병목은 아닙니다.
  - 이 상태에서 image decode/OpenGL upload를 더 건드릴 필요는 낮습니다. 다음은 실제 사용 중 배경 drain이 연속 클릭/스크롤 체감에 영향을 주는지 확인하는 쪽입니다.

## 2026-06-22 추가 완료 - WPF 1~5 labeling session verification loop

- 완료:
  - 10분 라벨링 세션에 가까운 자동 스모크를 추가했습니다. 이미지 로드, 박스, 채워진 원/타원, 폴리곤, 브러시 마스크, 지우개, 저장, AI 후보 스킵/확정을 한 흐름으로 검증합니다.
  - 좌측 이미지 큐의 빠른 필터 버튼 폭/배치를 조정해 아이콘과 한국어 라벨이 잘리지 않도록 했고, 선택 행이 현재 뷰어 이미지와 더 분명하게 연결되도록 유지했습니다.
  - Guide 기본 화면을 YOLOv5 학습 순서 중심으로 정리하고, 긴 체크리스트는 접힌 상세 영역으로 옮겼습니다.
  - Candidate Review에 선택 후보 요약 영역을 추가해 사용자가 지금 확정/스킵하려는 AI 후보를 먼저 볼 수 있게 했습니다.
  - 실제 `C:\Git\yolov5\data\train\images` 125장 폴더에서 큐 클릭 성능을 다시 측정했습니다.
- 검증:
  - `artifacts\logs\build-20260622-wpf-session-flow-v6.log`
  - `artifacts\logs\tests-20260622-wpf-session-flow-v4.log`
  - `artifacts\logs\visual-20260622-wpf-labeling-session-smoke-v1.log`
  - `artifacts\logs\visual-20260622-wpf-guide-flow-v2.log`
  - `artifacts\logs\visual-20260622-wpf-candidate-review-summary-v1.log`
  - `artifacts\logs\perf-20260622-wpf-real-folder-session-v1.log`
  - `artifacts\logs\verify-wpf-annotation-objects-20260622-session-v1.log`
  - `artifacts\ui\wpf-labeling-session-smoke-20260622.png`
  - `artifacts\ui\wpf-guide-flow-20260622-v2.png`
  - `artifacts\ui\wpf-candidate-review-summary-20260622.png`
  - `artifacts\ui\verify-wpf-annotation-objects-20260622-163520.png`
  - 세부 기록: `docs\WPF_LABELING_SESSION_VERIFICATION_20260622.md`
- 실제 폴더 측정:
  - visible: first 26.5 ms, avg 19.5 ms, max 26.5 ms
  - image commit: first 19.6 ms, warm avg 14.2 ms, warm max 18.6 ms
  - settled: first 157.0 ms, warm avg 62.4 ms, warm max 91.1 ms
  - working set: 199.3 MB -> 215.4 MB, delta +16.1 MB
  - decode cache: count 8/8, bytes 495.5 KB/64 MB, hits 23, misses 2, stores 48, evictions 17
- 자체평가:
  - 사용자가 보는 이미지 전환은 50 ms 목표를 만족합니다.
  - 전체 dispatcher idle 정리는 아직 50 ms를 넘는 구간이 있으므로, 빠른 스크롤/연속 클릭에서 체감 문제가 재현되면 background-priority 작업을 먼저 좁힙니다.
  - Guide는 많이 정리됐지만, 교육 목적 앱답게 다음 실제 YOLO 학습 세션에서 사용자가 막히는 지점을 다시 기록해야 합니다.

## 2026-06-22 추가 완료 - WPF YOLO training session priority pass

- 완료:
  - WPF 학습 세션 자동 스모크를 추가했습니다. train/valid 이미지를 각각 라벨링하고 저장한 뒤, 데이터셋 readiness, `StartTraining` TCP 패킷, worker 완료 상태, 최신 `best.pt` 적용, 후보 검토 이동까지 한 흐름으로 검증합니다.
  - Python worker가 현재 보내는 `TrainYoloResult`, `TaskStatus` 학습 메시지를 C#의 training status로 해석하도록 보강했습니다.
  - 학습 시작 후 WPF training panel이 terminal training state까지 worker 상태를 polling해 진행률과 완료 상태를 갱신합니다.
  - Candidate Review의 확정/스킵 후 다음 후보 이동 정책을 UI 문구로 노출했습니다.
  - Guide의 `추가 개념`을 `심화 개념`으로 바꾸고 기본 초보자 경로와 더 분리했습니다.
  - `docs\tutorial\images\01-guide.png`부터 `06-inference-review.png`까지 현재 WPF UI 기준으로 순차 캡처해 갱신했습니다.
- 검증:
  - `artifacts\logs\build-20260622-priority-pass-final-v1.log`
  - `artifacts\logs\tests-20260622-priority-pass-final-v1.log`
  - `artifacts\logs\visual-20260622-wpf-yolo-training-session-smoke-v1.log`
  - `artifacts\logs\visual-20260622-wpf-candidate-policy-v1.log`
  - `artifacts\logs\visual-20260622-wpf-guide-deep-concepts-v2.log`
  - `artifacts\logs\perf-20260622-wpf-real-yolov5-80-v1.log`
  - `artifacts\logs\perf-20260622-wpf-generated-240-v1.log`
  - `artifacts\ui\wpf-yolo-training-session-smoke-20260622.png`
  - `artifacts\ui\wpf-candidate-post-action-policy-20260622.png`
  - `artifacts\ui\wpf-guide-deep-concepts-collapsed-20260622-v2.png`
  - 세부 기록: `docs\WPF_YOLO_TRAINING_SESSION_VERIFICATION_20260622.md`
- 실제 폴더 측정:
  - `C:\Git\yolov5\data\train\images` 125장 중 80회 기준 visible 평균 21.4 ms, max 42.8 ms, image commit warm 평균 16.9 ms, working set +27.6 MB.
  - 생성 240장 큐 기준 visible 평균 18.8 ms, max 33.1 ms, full settled warm 평균 49.4 ms, working set +30.3 MB.
- 자체평가:
  - 초보자 YOLOv5 학습 경로는 이제 앱 레벨에서 클릭, 저장, 데이터셋 점검, 학습 명령, worker 완료, `best.pt` 적용까지 검증됩니다.
  - 이번 학습 검증은 빠르고 결정적인 mock worker 기반입니다. 실제 YOLOv5 `train.py` 장시간 학습은 충분한 실제 라벨을 만든 뒤 별도 검증해야 합니다.
  - Candidate Review 후속 정책은 자동 다음 후보 이동으로 유지합니다. 사용 중 혼란이 있으면 정책 자체보다 문구/포커스 표시를 먼저 조정합니다.

## 2026-06-22 추가 완료 - Real YOLO model comparison pass

- 완료:
  - 기존 사용자 학습 모델 `C:\Git\yolov5\best.pt`와 Codex 실험 학습 모델을 같은 데이터셋/같은 검증 조건으로 비교했습니다.
  - YOLOv5의 현재 패키지 호환 문제를 보정했습니다.
    - `C:\Git\yolov5\yolov5Master\utils\metrics.py`: NumPy `trapz`/`trapezoid` 차이 대응
    - `C:\Git\yolov5\yolov5Master\utils\plots.py`: Pillow `getsize`/`getbbox` 차이 대응
  - `C:\Git\yolov5\data\train`과 `data\valid`가 이미지 기준 125장 모두 같은 내용임을 확인했습니다.
  - 기존 사용자 모델은 현재 샘플 기준 `mAP50 0.995`, `mAP50-95 0.961`로 매우 높게 나왔습니다.
  - Codex CPU 10 epoch 실험 모델은 `mAP50 0.389`, `mAP50-95 0.215`이며, 실제 UI confidence 25% 기준으로는 후보가 살아남지 않아 채택하지 않습니다.
  - 상세 비교 문서를 `docs\YOLO_MODEL_COMPARISON_20260622.md`에 추가했습니다.
- 검증:
  - `C:\Git\Labelling_Application\artifacts\yolo_compare_data_20260622.yaml`
  - `C:\Git\Labelling_Application\artifacts\logs\yolo-val-user-best-20260622-172731-v4.log`
  - `C:\Git\Labelling_Application\artifacts\logs\yolo-train-codex-compare-20260622-172731.log`
  - `C:\Git\Labelling_Application\artifacts\logs\yolo-val-codex-compare-best-20260622-172731.log`
  - `C:\Git\yolov5\runs\val\user_best_20260622_172731`
  - `C:\Git\yolov5\runs\val\codex_compare_best_20260622_172731`
- 자체평가:
  - 현재 앱 기준 모델은 기존 사용자 `best.pt`를 유지해야 합니다.
  - 새 학습을 더 돌리기 전에 train/valid/test 분리와 NG 샘플 보강이 우선입니다.
  - 기존 모델도 후보가 과하게 많이 나오는 면이 있어, 후보 표시 threshold/top-k/NMS 정책은 앱 UX에서 더 정리해야 합니다.

## 2026-06-22 추가 완료 - YOLO dataset guard, comparison script, candidate cap

- 완료:
  - `YoloDatasetValidator`가 train/valid 이미지의 파일명 중복뿐 아니라 실제 이미지 내용 중복도 검사합니다. 같은 이미지가 train과 valid에 동시에 들어가면 학습 readiness에서 막고, 예시 파일명을 보여줍니다.
  - `scripts\compare-yolo-models.ps1`를 추가해 기존 모델과 새 학습 모델을 같은 `data.yaml`, 같은 image size, 같은 UI confidence 기준으로 반복 비교할 수 있게 했습니다.
  - WPF `YOLO > 모델 설정`에 `Max candidates` 입력을 추가했습니다. 기본값은 20개이며, 1~200 범위로 검증됩니다.
  - 검출 결과 서비스가 후보가 너무 많을 때만 confidence 상위 N개로 줄입니다. 후보가 상한 이하이면 Python이 준 순서를 유지해 기존 후보 인덱스/리뷰 흐름이 흔들리지 않게 했습니다.
  - `verify-first-run.ps1`가 새 비교 스크립트 문법도 함께 확인합니다.
- 검증:
  - `dotnet build MvcVisionSystem.csproj -c Debug`
  - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\compare-yolo-models.ps1 -BaselineWeights "C:\Git\yolov5\best.pt" -CandidateWeights "C:\Git\yolov5\runs\train\codex_compare_20260622_172731\weights\best.pt" -OutputDirectory "artifacts\yolo-model-comparison"`
  - 비교 산출물: `artifacts\yolo-model-comparison\20260622-182629\comparison-report.md`
- 자체평가:
  - 현재 샘플은 train/valid가 같은 이미지였기 때문에 새 guard가 앞으로의 학습 준비 단계에서 반드시 필요합니다.
  - 모델 비교는 명령 하나로 재현 가능해졌지만, 아직 WPF 안에서 보여주는 화면은 없습니다. 다음에는 학습 완료 후 "기존 모델 vs 새 모델"을 앱에서 읽을 수 있게 연결하는 쪽이 좋습니다.
  - 후보 상한은 UI 폭주를 막는 1차 안전장치입니다. NMS/클래스별 표시 정책은 실제 결함 샘플을 더 본 뒤 조정해야 합니다.

## 2026-06-22 추가 완료 - YOLO train/valid/test split support

- 완료:
  - YOLO 데이터셋 출력 구조에 `data\test\images`, `data\test\labels`를 추가했습니다.
  - `YoloDatasetSplitService`가 `Validation %`와 `Test %`를 함께 사용해 이미지가 train, valid, test 중 하나에만 들어가도록 확장했습니다. 기본 `Test %`는 0이라 기존 프로젝트는 그대로 동작합니다.
  - `data.yaml`에 `test:` 경로를 함께 기록합니다.
  - 박스 라벨과 세그멘테이션 라벨 저장/로드 후보 경로가 train/valid/test를 모두 이해합니다.
  - 데이터셋 readiness와 통계가 test 이미지/라벨/객체 수를 포함합니다. test는 선택 세트라 비어 있어도 기존 학습 준비를 막지 않습니다.
  - split 중복 검사는 train/valid, train/test, valid/test 사이의 이미지 내용 중복을 모두 확인합니다.
  - WPF `학습` 설정에 `Test %` 입력을 추가했습니다.
  - `scripts\compare-yolo-models.ps1`에 `-Task val|test` 옵션을 추가해, test 세트가 준비되면 같은 비교 스크립트로 실제 평가 세트 기준 비교가 가능합니다.
- 검증:
  - `dotnet build MvcVisionSystem.csproj -c Debug`
  - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
  - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-visual-smoke --review-tab=training --output="artifacts\ui\wpf-training-split-policy-check-20260622.png"`
  - 화면 확인: `artifacts\ui\wpf-training-split-policy-check-20260622.png`
- 자체평가:
  - 현재는 test split을 저장/검증/비교할 수 있는 기반입니다.
  - 실제 샘플 데이터는 아직 train/valid/test가 물리적으로 충분히 분리된 상태가 아니므로, 다음 단계는 실제 이미지 큐에서 test 샘플을 확보하고 모델 비교를 `-Task test`로 돌리는 것입니다.
  - 학습 UI의 split 입력은 기능적으로 연결됐지만, 초보자가 `Validation %`와 `Test %` 차이를 바로 이해하도록 가이드 문구를 더 다듬을 필요가 있습니다.

## 2026-06-22 추가 완료 - Learner-facing YOLO split and class-quality diagnostics

- 완료:
  - WPF `학습` 설정 패널에 `Validation %`와 `Test %`의 용도를 설명하는 안내 문구를 추가했습니다.
  - YOLO 데이터셋 diagnostics report가 준비 완료 상태에서도 split 상세, Validation/Test 용도, test split 비어 있음 경고를 출력합니다.
  - 클래스별 객체 수를 한 줄로 보여주고, 클래스 샘플이 너무 적으면 경고합니다. 특히 `NG` 같은 결함 클래스가 0개인 상태를 학습 전 확인할 수 있습니다.
  - 클래스 간 객체 수가 크게 치우치면 class balance 경고를 남기도록 했습니다.
- 검증:
  - `dotnet build MvcVisionSystem.csproj -c Debug`
  - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
- 자체평가:
  - 이제 test split 기능이 단순 설정값이 아니라 학습 전 판단 문맥까지 제공합니다.
  - 실제 모델 교체 판단에는 여전히 실제 NG 샘플과 물리적으로 분리된 test 이미지가 필요합니다.
  - 다음에는 충분한 라벨을 만든 뒤 `scripts\compare-yolo-models.ps1 -Task test`로 기존 `best.pt`와 새 모델을 비교해야 합니다.

## 2026-06-22 추가 완료 - Batch inference completion and fast image-size default

- 완료:
  - WPF 배치 추론에서 현재 캔버스 이미지와 다른 path-only 결과도 완료 이벤트를 올리도록 바꿨습니다. stale 결과는 캔버스에 그리지 않지만 큐 후보/완료 카운트에는 반영됩니다.
  - 배치 시작 시 Python worker 연결 확인을 한 번만 수행하고, 이미지마다 반복 readiness 확인을 하지 않도록 줄였습니다.
  - 배치 진행률과 하단 dataset 상태는 현재 처리 중인 이미지를 포함해 표시하도록 바꿔 `0/125`처럼 멈춘 것처럼 보이는 시간을 줄였습니다.
  - 배치 처리 중인 이미지가 캔버스에 표시되고, 좌측 이미지 큐 선택/스크롤도 현재 item을 따라가도록 했습니다.
  - 현재 표시 중인 배치 이미지의 결과가 도착하면 후보 overlay와 후보 목록을 갱신하되, 이미지별 후보 없음 로그를 반복해서 남기지는 않습니다.
  - 배치 결과 적용 순서를 보강했습니다. 이미지가 따라간 뒤 결과가 오면 같은 item에 대해 캔버스 후보 오버레이, 우측 후보 패널, 상단 검사 결과 카드를 다시 적용합니다.
  - 후보가 0개이거나 worker 실패가 난 배치 item도 빈 화면처럼 보이지 않게 `검출 후보 없음`/`검사 실패` 결과 카드를 표시합니다.
  - 다음 item으로 넘어가기 전 WPF render/background dispatcher를 한 번 양보해 결과 카드와 오버레이가 실제 화면에 반영될 시간을 보장합니다.
  - YOLO 추론 입력 크기 설정 `InferenceImageSize`를 추가했습니다. 기본값은 `320`이며 WPF 모델 설정, C# worker 실행, smoke test, 런처 스크립트, YOLO EXE 런처, Python 클라이언트 기본값이 같은 값을 사용합니다.
  - 앱에서 시작하는 Python worker와 first-run YOLO 런처는 `--preload`를 사용합니다. 모델 로딩 시간은 첫 이미지 추론 시간이 아니라 worker 준비 시간으로 분리됩니다.
  - `scripts\start-labeling-workbench.ps1`가 EXE 런처를 사용할 때도 경로/가중치/이미지루트/입력크기/장치 설정을 전달하도록 고쳤습니다.
- 검증:
  - `dotnet build MvcVisionSystem.csproj -c Debug`
  - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
  - `dotnet build tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
  - `dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\verify-first-run.ps1 -SkipBuild -SkipTests -SkipYoloSmoke`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\smoke-yolo-tcp.ps1 -UseDetectImage -Repeat 3 -ImgSize 320 -OutputDirectory artifacts\python-smoke-detect-image-320`
  - 추가 직접 테스트: `WPF batch detection result displays on canvas`가 후보 있음, 후보 없음, worker 실패 케이스를 WPF 창/캔버스 ViewModel 기준으로 검증합니다.
- 자체평가:
  - 일괄 추론이 “결과가 들어왔는데도 완료되지 않는” 직접 원인은 stale image guard가 batch 완료 이벤트까지 막던 점이었습니다.
  - 한 장 1초대 추론은 작은 원본 이미지 크기와 별개로 YOLO 입력이 640으로 들어가던 영향이 컸습니다. CPU 환경에서는 320 기본값이 더 현실적인 작업 기본값입니다.
  - 320 입력 실측에서 첫 요청은 모델 로딩 `26659ms`가 포함되어 `26971ms`였고, 같은 worker의 warm 추론은 `444ms`, `703ms`였습니다. 사용자가 봐야 하는 항목별 추론 시간은 warm 구간 기준으로 판단해야 합니다.
  - 실제 125장 배치 시간은 Python worker가 이미 떠 있는 상태와 CPU/GPU 장치에 따라 달라지므로, 다음에는 동일 worker 연결에서 반복 `DetectImage` 배치 시간을 따로 측정해야 합니다.

## 2026-06-22 추가 완료 - ROI 내부 이동 hit-test 재검증

- 완료:
  - 이전 ROI 검증은 부족했습니다. “ROI 내부인데 외곽 커서/리사이즈로 잡히는지”를 직접 검증하지 못했습니다.
  - ROI hit-test 규칙을 바꿨습니다. `strict interior` 좌표는 코너/외곽보다 먼저 `Move`로 판정하고, 리사이즈는 경계선 또는 바깥쪽 핸들 영역에서만 잡히게 했습니다.
  - 이동 커서를 `NoMove2D`에서 `SizeAll`로 바꿔 내부 이동 상태가 사용자에게 더 명확히 보이게 했습니다.
  - 같은 문제가 다른 소스 루트에서 다시 들어오지 않도록 `C:\Git\OpenVisionLab_Dev\Library\OpenVisionLab.ImageCanvas`의 `CanvasCompatibility.cs`, `RoiInteractionCursor.cs`도 동일하게 동기화했습니다.
  - 테스트에 ROI 내부 1px 지점, 코너 근처 내부 지점, 정확한 경계 리사이즈, hover 커서 판정을 추가했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nodeReuse:false`
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-roi-object-verification`
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`
  - `dotnet build C:\Git\OpenVisionLab_Dev\Library\OpenVisionLab.ImageCanvas\OpenVisionLab.ImageCanvas.csproj -c Debug /nodeReuse:false`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-roi-object-interactions.ps1`
  - 캡처 확인: `artifacts\ui\verify-wpf-roi-objects-20260622-223247.png`
- 자체평가:
  - 이번 검증은 이전보다 신뢰할 수 있습니다. 실제 마우스 이벤트 경로에서 내부 이동, 내부 코너 이동, 경계 리사이즈, 커서 상태, 새 ROI 생성 방지를 함께 확인했습니다.
  - 사용 중인 실행 파일이 오래된 `OpenVisionLab.ImageCanvas.dll`을 물고 있으면 같은 증상이 남을 수 있습니다. 이 경우 앱을 완전히 종료한 뒤 새로 빌드된 `artifacts\run\Debug` 출력으로 실행해야 합니다.

## 2026-06-22 추가 완료 - 원/타원 ROI 초기 표시 및 OpenGL 채움 결함 수정

- 완료:
  - 원/타원 ROI가 생성 직후 보이지 않고 클릭 후에야 보이는 문제를 수정했습니다.
  - ROI 생성 마우스업 경로에서 overlay 추가 후 `RefreshGL()`을 호출하도록 했습니다.
  - 새 overlay가 추가되면 display list를 즉시 컴파일하도록 `OpenGlOverlayExtensions`를 보강했습니다.
  - 드래그 중인 ROI는 overlay list에 들어가기 전에도 직접 프리뷰로 그리도록 `DrawActiveDrawingRoiPreview`를 추가했습니다.
  - 타원 내부 채움의 `GL_TRIANGLE_FAN` 마지막 점을 닫아 중심에서 오른쪽으로 벌어지는 빈틈을 제거했습니다.
  - 타원 ROI를 그린 뒤 사각형 채움을 다시 덮는 중복 렌더링을 제거했습니다.
  - Dev ImageCanvas에도 공통 생성 직후 display list 생성과 mouse-up refresh를 동기화했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /m:1 /nodeReuse:false`
  - `dotnet build C:\Git\OpenVisionLab_Dev\Library\OpenVisionLab.ImageCanvas\OpenVisionLab.ImageCanvas.csproj -c Debug /m:1 /nodeReuse:false`
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-roi-object-verification`
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build -- --wpf-annotation-object-verification`
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --no-build`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-roi-object-interactions.ps1`
  - 타원 직접 캡처 확인: `artifacts\ui\wpf-ellipse-roi-render-check-20260622-v2.png`
  - ROI 스크립트 캡처 확인: `artifacts\ui\verify-wpf-roi-objects-20260622-225101.png`
- 자체평가:
  - 이번에는 사용자 지적 화면과 같은 축의 결함을 실제 캡처로 확인했습니다.
  - 타원 ROI는 생성 직후 화면에 표시되고, 내부 채움에 중심-우측 방향 빈틈이 보이지 않습니다.
  - 다음 UX 개선 후보는 타원 선택 핸들을 사각형 박스처럼 보이지 않게 더 세련된 선택 표시로 바꾸는 것입니다.

## 2026-06-23 추가 완료 - Viewer MouseMove/ROI hover 병목 제거

- 완료:
  - 단순 hover MouseMove에서도 OpenGL repaint 요청이 발생하던 경로를 차단했습니다. hover는 cursor/status만 바꾸고, ROI geometry가 바뀌는 pan/draw/move/resize만 frame-limit repaint를 요청합니다.
  - 미선택 ROI 위 hover마다 `FindOverlayAtPosition`을 호출하던 cursor 경로를 제거했습니다. ROI 선택 hit-test는 MouseDown에서 즉시 수행하고, hover cursor는 현재 선택된 ROI 핸들에만 반응합니다.
  - 레거시 `CViewer` 경로도 같은 원칙으로 정리했습니다. pointer-up hover는 status/cursor만 갱신하고 `Canvas.RefreshGL()`을 호출하지 않으며, cursor 계산은 현재 선택 ROI cache(`_SelectedClass`, `_SelectROiIndex`)를 먼저 사용합니다.
  - 왜 이 경로를 줄였는지 `ImageCanvasControl`과 `RoiImageCanvasViewModel`에 코드 주석으로 남겼습니다.
  - `--hover-mousemove-performance` 성능 스모크를 추가했습니다. 50만 ROI를 올린 뒤 ROI 위 hover MouseMove 1000회를 실행하고, spatial candidate 수와 status PropertyChanged throttle을 함께 검증합니다.
  - `--brush-hover-performance` 성능 스모크를 추가했습니다. 50만 ROI 상태에서 brush/mask image-point hover MouseMove 1000회를 실행하고, brush cursor preview와 status PropertyChanged가 frame-limit/throttle을 지키는지 검증합니다.
  - `--wpf-mask-drag-performance` 성능 스모크를 추가했습니다. brush/eraser drag MouseMove 1000회 동안 undo history와 object review list 갱신이 발생하지 않고, mouse-up commit에서 한 번만 발생하는지 검증합니다.
  - `--mask-move-performance` 성능 스모크를 추가했습니다. 4096x4096 raster mask에서 작은 선택 mask를 1000회 이동할 때 full image mask buffer를 매번 새로 만들지 않고 active bounds만 복사하는지 검증합니다.
  - brush/mask overlay 갱신 API를 `SetSegmentationOverlays`로 묶었습니다. MouseMove 중 polygon overlay와 mask overlay를 따로 set하면서 OpenGL repaint를 두 번 요청하지 않고, 두 render index를 먼저 갱신한 뒤 한 번만 repaint합니다.
  - `--roi-500k-mouse-event-performance` 성능 스모크를 추가했습니다. 내부 helper가 아니라 실제 `ImageCanvasControl.OnMouseDown/OnMouseMove/OnMouseUp` 경로로 50만 ROI 중 하나를 이동/리사이즈해 MouseMove 중 display-list rebuild/callback 폭주가 없는지 검증합니다.
  - `--roi-500k-render` 성능 스모크를 추가했습니다. 50만 ROI 상태에서 viewport spatial query와 visible overlay cache rebuild가 전체 오브젝트를 유지하지 않는지 검증합니다.
  - 레거시 `CViewer` 후보 선택 색상도 정리했습니다. 클릭했다고 후보/클래스 색을 노란색으로 바꾸지 않고, 기존 색상 위에 투명도/선 굵기/코너 마커로만 선택을 표현합니다.
  - ROI 선택 핸들을 빈 점선 사각형에서 dark halo + 채워진 파란 핸들 + 흰 테두리로 바꿨습니다. 클래스 색상은 객체 의미, 파란색 핸들은 현재 편집 선택이라는 역할을 분리했습니다.
  - 우측 객체 목록은 `수동` 접두어를 줄이고 `Defect / 박스 / 크기 35x35 / 위치 x=36, y=36`처럼 크기를 먼저 보이게 했습니다. 가로 스크롤은 끄고 tooltip에는 상세 정보를 유지합니다.
  - 상태바 픽셀 hover readback은 30Hz 제한을 유지하되, 1px RGBA 버퍼를 재사용하도록 바꿨습니다. 장시간 MouseMove 중 `byte[4]` 할당이 계속 생기지 않게 하기 위한 작은 구조 정리입니다.
  - 겹친 ROI 선택 우선순위를 보강했습니다. 여러 ROI가 같은 점에 걸리면 더 작은 실제 결함 ROI를 우선 선택하고, 같은 크기일 때 중심 거리와 hit type으로 tie-break합니다.
  - raster mask 이동의 scratch buffer를 `ArrayPool<byte>`로 바꿨고, 큰 활성 mask 이동은 픽셀 단위 non-zero 검사 대신 행 단위 `Buffer.BlockCopy`로 복사합니다. MouseMove마다 멀티 MB 배열을 새로 만들거나 큰 mask를 픽셀 루프로 훑지 않게 하기 위한 수정입니다.
  - `CanvasOverlaySpatialIndex`의 큰 ROI fallback을 coarse cell bucket으로 바꿨습니다. 너무 큰 ROI가 fine 64px cell 한도를 넘더라도 `_globalItems` 전체 순회로 떨어지지 않고 4096px coarse cell에서 후보를 찾습니다.
  - 같은 지점에 ROI가 많이 겹친 경우를 위해 `FindBestInteractiveRectAtPoint` best-hit 경로를 추가했습니다. 클릭 선택은 더 이상 후보 `List`를 크게 만들지 않고, spatial bucket의 최소 면적 ROI를 먼저 평가해 작은 결함 박스 선택을 빠르게 끝냅니다.
  - 줌아웃 상태에서 한 화면에 수십만 ROI가 들어오는 경우를 위해 visible overlay cache에 1만 shape LOD cap을 추가했습니다. hit-test와 편집은 전체 spatial index를 계속 쓰고, 시각 표시만 bounded cache로 제한합니다.
  - 일반 overlay frame도 static overlay scene display list를 사용하도록 바꿨습니다. cache가 유효한 동안 매 프레임 수천~수만 개 display list를 반복 호출하지 않고, 정적 overlay scene 한 번과 live ROI만 그립니다.
  - LOD cap이 걸린 경우 하단 canvas 상태바에 `표시 ROI: 10,000+` 배지를 표시합니다. 성능을 위해 시각 표시가 제한된 상태임을 숨기지 않되, 선택/편집은 전체 ROI 기준으로 유지합니다.
  - 확장 사각형처럼 ROI 하나가 여러 shape로 표시되는 경우도 같은 LOD budget을 쓰도록 보강했습니다. 성능 보호 cap이 overlay item 수가 아니라 실제 그릴 shape 수 기준으로 동작하게 하기 위한 정리입니다.
  - pan 중에도 cached ROI scene은 유지하도록 바꿨습니다. texture 이동을 위해 detection/mask/text 같은 보조 overlay는 settled repaint까지 미루지만, ROI 맥락은 화면에서 사라지지 않게 하고 viewport cache 갱신은 20Hz로 제한합니다.
  - zoom/view-state 적용 시 visible overlay cache를 명시적으로 invalidation합니다. viewport bounds가 바뀌었는데 이전 ROI cache를 재사용하면 줌 직후 표시가 늦거나 stale하게 보일 수 있기 때문입니다.
  - WPF mouse wheel zoom도 `ImageCanvasControl.ZoomAt` 중앙 경로를 쓰도록 정리했습니다. toolbar/테스트/휠 입력이 같은 viewport/cache invalidation 로직을 타게 하기 위한 구조 정리입니다.
  - 확장 사각형이 있는 ROI도 `notify:false` drag 중에는 callback을 호출하지 않도록 `CanvasRect` notify 전파를 보강했습니다. main ROI만 suppress되고 extended rectangle이 display-list rebuild를 유발하는 숨은 MouseMove 경로를 막기 위한 수정입니다.
  - 예전 hit-test tie-break helper는 `CanvasOverlaySpatialIndex.FindBestRectAtPoint`로 책임이 이동했으므로 dead code를 제거했습니다. 선택 정책이 두 군데에 흩어져 보이지 않게 하기 위한 정리입니다.
- 검증:
  - 수정 전 자체 재현: `HOVER_500K_1000_MOUSEMOVE_MS=1559.077 CANDIDATES=625 STATUS_PROPERTY_CHANGED=190`
  - 수정 후 focused: `HOVER_500K_1000_MOUSEMOVE_MS=41.211 CANDIDATES=625 STATUS_PROPERTY_CHANGED=5`
  - 전체 테스트 내 재검증: `HOVER_500K_1000_MOUSEMOVE_MS=25.799 CANDIDATES=625 STATUS_PROPERTY_CHANGED=5`
  - 실제 mouse-event focused: `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=74.878 DISPLAY_REBUILDS_DURING_MOVE=0 EDIT_CALLBACKS_DURING_MOVE=0 DISPLAY_REBUILDS_TOTAL=2 EDIT_CALLBACKS_TOTAL=1`
  - 실제 mouse-event 전체 테스트 내 재검증: `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=62.342 DISPLAY_REBUILDS_DURING_MOVE=0 EDIT_CALLBACKS_DURING_MOVE=0`
  - CViewer 레거시 hover 보강 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=20.477`, `HOVER_500K_1000_MOUSEMOVE_MS=19.958`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=221.865`
  - ROI resize 실제 이벤트 경로 추가 후 focused: `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=13.248`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.875`, 둘 다 MouseMove 중 rebuild/callback 0
  - ROI resize 실제 이벤트 경로 전체 테스트 내 재검증: `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=13.736`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.230`, 둘 다 MouseMove 중 rebuild/callback 0
  - 선택 유지 보강 후 전체 테스트 내 재검증: rectangle resize 후, ellipse/circle move 후, ellipse/circle resize 후 모두 `IsEditing` 유지 assert 통과
  - ROI render/culling focused: `ROI_500K_RENDER_CANDIDATES=10201`, `VISIBLE_SHAPES=10000`, `ROI_500K_RENDER_QUERY_MS=5.417`, `ROI_500K_RENDER_REBUILD_MS=7.656`
  - ROI render/culling 전체 테스트 내 재검증: `ROI_500K_RENDER_CANDIDATES=10201`, `VISIBLE_SHAPES=10000`, `ROI_500K_RENDER_QUERY_MS=4.855`, `ROI_500K_RENDER_REBUILD_MS=7.325`
  - brush hover 50만 ROI focused: `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=28.462 HOVER_EVENTS=1000 BRUSH_PROPERTY_CHANGED=3 STATUS_PROPERTY_CHANGED=5`
  - 최신 focused 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=2.302`, `HOVER_500K_1000_MOUSEMOVE_MS=41.599`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=28.462`
  - mask drag focused: `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=34.460 SEGMENTS=1 UNDO_DURING=0 UNDO_AFTER=1 COLLECTION_CHANGED_DURING=0 COLLECTION_CHANGED_AFTER=1`
  - mask move focused: `WPF_RASTER_MASK_MOVE_4096_1000_MS=37.369 ALLOCATED_MB=3.929 SAME_BUFFER=True BOUNDS={X=200,Y=200,Width=64,Height=64}`
  - 최신 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=16.684`, `HOVER_500K_1000_MOUSEMOVE_MS=20.093`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=7.375`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=21.338`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=4.895`
  - mask drag 개선 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=1.274`, `HOVER_500K_1000_MOUSEMOVE_MS=21.164`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.056`, `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=36.761`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=10.270`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=4.752`
  - mask move 개선 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=1.436`, `HOVER_500K_1000_MOUSEMOVE_MS=22.613`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.222`, `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=29.094`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=11.110`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.063`
  - brush visual smoke: `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --annotation-tool=brush`
  - brush visual smoke 캡처 확인: `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`에서 mask brush tool, 원형 brush preview, ROI/AI overlay, 후보 검토 패널이 함께 정상 표시됨
  - `TEXTURE_PAN_1000_MOUSEMOVE_MS=2.542 VIEWMODEL_MOUSEMOVE_EVENTS=0 OFFSET_X=1000.0`
  - `ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS=17.293 ADDED=0 PREVIEW_EMPTY=False`
  - `ROI_500K_SINGLE_MOVE_MS=0.056`, `ROI_500K_INCREMENTAL_ADD_MS=13.237`
  - `ROI_500K_HIT_MS=0.724`, `DETECTION_500K_RENDER_CULL_MS=0.230`, `SEGMENTATION_RENDER ... POLYGON_CULL_MS=0.876 MASK_CULL_MS=0.411`
  - `SetSegmentationOverlays` 적용 후 focused 재검증: `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=32.800`, `WPF_RASTER_MASK_MOVE_4096_1000_MS=37.623 ALLOCATED_MB=3.929 SAME_BUFFER=True`
  - 최종 빌드 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=1.395`, `HOVER_500K_1000_MOUSEMOVE_MS=20.211`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.218`, `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=27.499`, `ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS=4.380`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=10.600`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.036`
  - 선택 UX 개선 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=19.031`, `HOVER_500K_1000_MOUSEMOVE_MS=20.538`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.259`, `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=30.464`, `ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS=3.962`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=14.120`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=4.945`
  - pixel readback/겹친 ROI 선택 개선 후 focused 재검증:
    - `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=13.229`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.538`, MouseMove 중 rebuild/callback 0
    - `HOVER_500K_1000_MOUSEMOVE_MS=44.392 CANDIDATES=625 STATUS_PROPERTY_CHANGED=5`
    - `WPF_RASTER_MASK_MOVE_4096_1000_MS=39.173 ALLOCATED_MB=3.929 SAME_BUFFER=True`
    - `ROI_500K_INDEXED_LOAD_MS=1945.3 ROI_500K_CANDIDATES=325 ROI_500K_QUERY_MS=0.871 ROI_500K_HIT_MS=0.741`
  - pixel readback/겹친 ROI 선택 개선 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=1.287`, `HOVER_500K_1000_MOUSEMOVE_MS=19.848`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.331`, `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=32.167`, `ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS=4.016`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=10.539`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.522`, `ROI_500K_HIT_MS=0.705`
  - 큰 활성 mask 이동 수정 전 자체 재현: `WPF_RASTER_MASK_LARGE_ACTIVE_MOVE_2048_100_MS=1985.668 ALLOCATED_MB=4.000`
  - 큰 활성 mask 이동 수정 후 focused: `WPF_RASTER_MASK_MOVE_4096_1000_MS=5.649 ALLOCATED_MB=0.006`, `WPF_RASTER_MASK_LARGE_ACTIVE_MOVE_2048_100_MS=36.134 ALLOCATED_MB=4.000`
  - 최신 focused 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=1.669`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=13.684`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.677`, `ROI_500K_INDEXED_LOAD_MS=2064.6`, `ROI_500K_HIT_MS=0.786`, `ROI_500K_RENDER_REBUILD_MS=8.725`
  - 최신 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=17.180`, `HOVER_500K_1000_MOUSEMOVE_MS=20.382`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.314`, `WPF_MASK_BRUSH_DRAG_1000_MOVE_MS=26.126`, `ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS=4.270`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=10.281`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=4.988`, `ROI_500K_HIT_MS=0.871`, `ROI_500K_RENDER_REBUILD_MS=7.619`
  - 큰 ROI coarse index focused: `ROI_LARGE_INDEX_LOAD_MS=2011.7 ROI_LARGE_CANDIDATES=1 ROI_LARGE_QUERY_MS=0.005 ROI_LARGE_UPDATE_MS=0.657 LARGE_CELLS=493728 GLOBAL_ITEMS=0`
  - 큰 ROI coarse index 전체 테스트 내 재검증: `ROI_LARGE_INDEX_LOAD_MS=1987.6 ROI_LARGE_CANDIDATES=1 ROI_LARGE_QUERY_MS=0.003 ROI_LARGE_UPDATE_MS=0.046 LARGE_CELLS=493728 GLOBAL_ITEMS=0`
  - 겹침 ROI 수정 전 자체 재현: `ROI_OVERLAP_HIT_MS=29.402 OBJECTS=50000 SELECTED_SIZE=5.0`
  - 겹침 ROI focused: `ROI_OVERLAP_FIRST_HIT_MS=11.455 ROI_OVERLAP_REPEAT_HIT_MS=7.426 OBJECTS=50000 SELECTED_SIZE=5.0`
  - 겹침 ROI 전체 테스트 내 재검증: `ROI_OVERLAP_FIRST_HIT_MS=7.887 ROI_OVERLAP_REPEAT_HIT_MS=7.656 OBJECTS=50000 SELECTED_SIZE=5.0`
  - 겹침 ROI 보강 후 최신 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=17.183`, `HOVER_500K_1000_MOUSEMOVE_MS=20.695`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.251`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=12.369`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.363`, `ROI_500K_HIT_MS=1.373`, `ROI_500K_RENDER_REBUILD_MS=8.031`
  - full viewport 50만 ROI LOD focused: `ROI_500K_FULL_VIEW_REBUILD_MS=10.585 VISIBLE_SHAPES=10000 LIMIT=10000`
  - full viewport 50만 ROI LOD 전체 테스트 내 재검증: `ROI_500K_FULL_VIEW_REBUILD_MS=5.312 VISIBLE_SHAPES=10000 LIMIT=10000`
  - LOD 적용 후 최신 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=1.298`, `HOVER_500K_1000_MOUSEMOVE_MS=19.877`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.511`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=11.881`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.178`, `ROI_500K_HIT_MS=0.747`, `ROI_500K_RENDER_REBUILD_MS=6.843`, `ROI_OVERLAP_FIRST_HIT_MS=4.938`
  - LOD 상태 배지 추가 후 focused: `ROI_500K_FULL_VIEW_REBUILD_MS=10.459 VISIBLE_SHAPES=10000 LIMIT=10000`
  - LOD 상태 배지 추가 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=19.496`, `HOVER_500K_1000_MOUSEMOVE_MS=20.373`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.346`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=11.696`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.126`, `ROI_500K_FULL_VIEW_REBUILD_MS=5.329`, `ROI_OVERLAP_FIRST_HIT_MS=6.364`
  - LOD shape-budget 보강 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=19.959`, `HOVER_500K_1000_MOUSEMOVE_MS=20.383`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.503`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=11.631`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.110`, `ROI_500K_HIT_MS=0.727`, `ROI_500K_RENDER_REBUILD_MS=6.768`, `ROI_500K_FULL_VIEW_REBUILD_MS=5.317`, `ROI_OVERLAP_FIRST_HIT_MS=5.494`
  - 50만 ROI pan cache 보강 후 focused: `TEXTURE_PAN_1000_MOUSEMOVE_MS=9.768 VIEWMODEL_MOUSEMOVE_EVENTS=0 ROI_OBJECTS=500000 PAN_VISIBLE_SHAPES=10000`
  - 50만 ROI pan cache 보강 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=14.499`, `HOVER_500K_1000_MOUSEMOVE_MS=24.770`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=7.418`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=15.373`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.959`, `ROI_500K_FULL_VIEW_REBUILD_MS=10.153`, `ROI_OVERLAP_FIRST_HIT_MS=7.682`
  - zoom/view-state cache 보강 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=11.489`, `HOVER_500K_1000_MOUSEMOVE_MS=34.731`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=6.716`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=19.848`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=7.198`, `ROI_500K_HIT_MS=1.498`, `ROI_500K_RENDER_REBUILD_MS=8.948`, `ROI_500K_FULL_VIEW_REBUILD_MS=6.207`, `ROI_OVERLAP_FIRST_HIT_MS=4.731`
  - WPF wheel zoom 중앙화 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=8.309`, `HOVER_500K_1000_MOUSEMOVE_MS=21.257`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=5.899`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=12.000`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.490`, `ROI_500K_HIT_MS=0.735`, `ROI_500K_RENDER_REBUILD_MS=7.119`, `ROI_500K_FULL_VIEW_REBUILD_MS=5.441`, `ROI_OVERLAP_FIRST_HIT_MS=5.270`
  - 확장 사각형 callback suppression 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=8.317`, `HOVER_500K_1000_MOUSEMOVE_MS=19.275`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=5.929`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=11.987`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=5.051`, `ROI_500K_HIT_MS=0.796`, `ROI_500K_RENDER_REBUILD_MS=7.545`, `ROI_500K_FULL_VIEW_REBUILD_MS=5.241`, `ROI_OVERLAP_FIRST_HIT_MS=4.690`
  - hit-test dead code 정리 후 전체 테스트 내 재검증: `TEXTURE_PAN_1000_MOUSEMOVE_MS=9.175`, `HOVER_500K_1000_MOUSEMOVE_MS=18.926`, `BRUSH_HOVER_500K_1000_MOUSEMOVE_MS=5.875`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=11.820`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=4.983`, `ROI_500K_HIT_MS=0.742`, `ROI_500K_RENDER_REBUILD_MS=6.932`, `ROI_500K_FULL_VIEW_REBUILD_MS=5.273`, `ROI_OVERLAP_FIRST_HIT_MS=4.678`
  - `dotnet build tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 /p:UseSharedCompilation=false`
  - `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug`
  - `powershell -ExecutionPolicy Bypass -File scripts\verify-first-run.ps1 -Configuration Debug -SkipBuild -SkipTests -SkipYoloSmoke -SkipScriptSyntax -RunWpfSmoke`
  - WPF visual smoke 캡처 확인: `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`
  - box visual smoke 캡처 확인: `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`에서 ROI 선택 핸들이 dark halo/파란 채움/흰 테두리로 보이고, 우측 객체 목록이 가로 스크롤 없이 `크기 35x35 / 위치 x=36, y=36` 형태로 표시됨
  - ROI 상호작용 스크립트 재검증: `powershell -ExecutionPolicy Bypass -File scripts\verify-wpf-roi-object-interactions.ps1`, 캡처 `artifacts\ui\verify-wpf-roi-objects-20260623-032746.png`
  - box visual smoke 재캡처 확인: `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`
  - pan cache 보강 후 EXE/UI 재검증: `scripts\verify-wpf-roi-object-interactions.ps1`, 캡처 `artifacts\ui\verify-wpf-roi-objects-20260623-044007.png`
  - 통합 annotation object EXE/UI 재검증: `scripts\verify-wpf-annotation-object-interactions.ps1`, 캡처 `artifacts\ui\verify-wpf-annotation-objects-focused-20260623-044106.png`
  - segmentation object EXE/UI 재검증: `scripts\verify-wpf-segmentation-object-interactions.ps1`, 캡처 `artifacts\ui\verify-wpf-segmentation-objects-20260623-044159.png`
  - 기본 annotation object EXE/UI 재검증: `scripts\verify-wpf-annotation-objects.ps1`, 캡처 `artifacts\ui\verify-wpf-annotation-objects-20260623-044803.png`
  - zoom visual smoke 재검증: `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --annotation-tool=box --zoom-steps=3`, 캡처 `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`
- 자체평가:
  - 이번 병목은 "전체 overlay repaint" 하나만이 아니라 hover cursor hit-test도 같이 있었습니다. repaint를 끊은 뒤에도 1559 ms가 남았고, 미선택 ROI hover hit-test 제거 후 25~41 ms로 내려갔습니다.
  - 선택 UX는 유지됩니다. ROI 선택은 hover가 아니라 MouseDown에서 spatial index로 즉시 수행하며, 클릭 후 선택 핸들은 계속 유지됩니다.
  - brush/mask image-point hover도 ROI cursor hit-test와 분리되어 50만 ROI 상태에서 1000회 30 ms 안팎으로 유지됩니다.
  - brush/mask drag는 MouseMove 중 mask pixel/overlay dirty rect만 갱신하고, undo history/object review/image queue 갱신은 mouse-up에서 한 번만 수행합니다. 이전 구조처럼 stroke 중 매 이벤트마다 history snapshot과 side list rebuild를 만들지 않습니다.
  - 큰 이미지에서 raster mask를 이동할 때도 full mask buffer를 새로 만들지 않고 active bounds만 복사합니다. 4096x4096 이미지 기준 1000회 move가 37 ms, 할당 3.9 MB로 제한됐습니다.
  - 큰 활성 mask는 할당 문제가 없어도 픽셀 단위 검사만으로 100회 이동이 2초 가까이 걸릴 수 있었습니다. 행 단위 복사로 바꾼 뒤 같은 케이스가 36 ms로 내려갔으므로, 앞으로 MouseMove 경로에서는 "할당 제거"와 "per-pixel 루프 제거"를 함께 확인합니다.
  - segmentation overlay는 polygon/mask 목록을 한 번에 교체해 repaint를 합칩니다. brush stroke처럼 매 이벤트가 들어오는 경로에서 동일 프레임을 두 번 요청하던 구조적 낭비를 줄였습니다.
  - 후보/비교/상태 메시지의 bounds 표기를 `47x42 @ 30,32`에서 `크기 47x42 / 위치 x=30, y=32` 형태로 바꿨습니다. 사용자가 우측 목록에서 무엇을 표시하는지 바로 알 수 있게 하기 위한 UX 수정입니다.
  - 후보 검토 visual smoke 캡처 확인: `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`에서 상단 AI 검출 결과, 우측 후보 카드, 후보 목록, 하단 상태바 모두 새 bounds 표기로 표시됨
  - 선택 상태는 색을 바꾸는 방식이 아니라 편집 affordance로 분리했습니다. 선택된 객체가 다른 클래스처럼 보이는 혼란을 줄이고, 클릭 후 선택 유지도 캔버스/우측 목록 양쪽에서 확인됩니다.
  - 상태바 픽셀 readback은 hover 샘플링 자체가 남아 있지만, 드래그 중에는 읽지 않고 30Hz로 제한되며 1px 버퍼를 재사용합니다. 실제 장시간 라벨링에서도 GC 압박은 더 낮아졌습니다.
  - 겹친 ROI에서는 작은 결함 박스를 우선 선택하도록 바꿨습니다. 중심이 같은 큰/작은 ROI를 실제 ViewModel mouse event 경로로 클릭하는 테스트를 추가해, 큰 배경 ROI가 선택을 빼앗지 않도록 고정했습니다.
  - 큰 ROI가 많지만 서로 떨어진 경우는 coarse index로 해소했습니다. 남은 관찰 항목은 많은 큰 ROI가 같은 화면/같은 지점에 실제로 겹치는 극단 케이스입니다. 이 경우 후보 자체가 많아지므로 선택 정책과 렌더링 LOD 정책으로 다뤄야 합니다.
  - 많은 ROI가 같은 지점에 겹친 클릭 선택은 best-hit 경로로 해소했습니다. 다만 같은 화면에 실제로 수십만 개 ROI를 모두 동시에 그려야 하는 데이터는 렌더링 후보 자체가 많으므로, 이후에는 화면 LOD/라벨 숨김 정책을 별도 UX 결정으로 다룹니다.
  - 한 화면에 수십만 ROI가 보이는 줌아웃 화면은 1만 shape LOD로 bounded 처리합니다. 개별 ROI를 정확히 선택/이동하는 기능은 spatial index full-fidelity 경로를 유지하므로, 작업자는 확대하거나 클릭해서 실제 객체를 정확히 다룰 수 있습니다.
  - LOD 상태는 숨기지 않고 canvas 상태바에만 짧게 표시합니다. 일반 이미지처럼 ROI가 적을 때는 배지가 숨겨져, 기본 라벨링 UX를 방해하지 않습니다.
  - texture pan은 50만 ROI가 있어도 MouseMove 1000회가 15 ms 안쪽으로 유지됩니다. pan 중 ROI 맥락은 cached scene으로 보이고, 비용 큰 보조 overlay만 release/settled repaint로 미룹니다.

## 2026-06-23 객체 삭제 병목 추가 수정

- 작업:
  - 우측 객체 검토 패널 삭제가 `RedrawReviewRois()`로 전체 ROI를 지우고 다시 추가하던 경로를 분리했습니다. 수동 ROI 삭제는 저장된 overlay id로 해당 canvas overlay 하나만 제거합니다.
  - `WpfObjectReviewItemRef`에 수동 ROI `SourceId`를 추가했습니다. 큰 목록에서는 삭제 직후 모든 행을 즉시 재번호하지 않아도 다음 클래스 변경/삭제가 올바른 ROI를 찾도록 하기 위한 식별자입니다.
  - `WpfObjectReviewPanelViewModel.TryRemoveObject`를 추가했습니다. 큰 객체 목록에서는 WPF `Reset`이 아니라 단일 `Remove` 이벤트만 내보내 side list 전체 재생성을 피합니다.
  - 작은 목록(1만 개 이하)은 삭제 후 전체 목록을 refresh해 표시 번호를 즉시 정확히 맞추고, 큰 목록은 단일 remove fast-path를 사용합니다.
  - OpenGL `DeleteOverlay`/`AddOverlay`에서 큰 visible group bounds를 즉시 재계산하지 않도록 제한했습니다. 그룹 외곽 박스는 보조 표시이므로, 단일 ROI 작업이 50만 child bounds scan을 유발하지 않게 하기 위한 구조적 방어입니다.
  - overlay id가 없는 오래된 상태에서는 안전하게 `RedrawReviewRois()`로 fallback합니다.
  - 추가 확인에서 우측 삭제 후 canvas `_selectedRect` live selection이 남아 핸들만 계속 그려지는 버그를 수정했습니다. 삭제된 overlay는 이미 manager에서 빠졌으므로, `ClearDeletedRoiSelection`은 기존 shape callback을 다시 호출하지 않고 live selection 참조만 비웁니다.
  - 삭제 끝의 이미지 큐/라벨 상태 갱신은 `QueueActiveImageQueueStatusRefresh`로 background priority에 넘겼습니다. 라벨 파일 recount, review-state JSON 저장, image queue refresh가 삭제 버튼 응답을 막지 않게 하기 위한 조치입니다.
  - detection overlay hit-test도 `QueryPoint` 후보 `List` 생성 대신 `FindTopmostContainingPoint`로 spatial bucket을 allocation 없이 뒤에서부터 탐색하도록 바꿨습니다. 기존 0.270 ms max에는 reflection/할당 비용이 섞여 있었으므로, 진단용 direct API로 실제 경로를 측정합니다.
- 검증:
  - `dotnet build tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj --no-restore` 통과. 기존 미사용 변수 warning과 DLL copy retry warning만 확인.
  - focused 삭제: `ROI_500K_SINGLE_DELETE_MS=8.941 ROI_500K_DELETE_LOAD_MS=3561.5 VISIBLE_CACHE_DIRTY=False VISIBLE_SHAPES=0 OBJECTS=499999`
  - focused ROI move/resize: `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=21.772`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=7.073`, MouseMove 중 rebuild/callback 0
  - focused texture pan: `TEXTURE_PAN_1000_MOUSEMOVE_MS=12.305 VIEWMODEL_MOUSEMOVE_EVENTS=0 PAN_VISIBLE_SHAPES=10000`
  - focused detection: `DETECTION_500K_HIT_MS=0.205`, `DETECTION_500K_RENDER_CULL_MS=0.336`
  - WPF visual smoke 통과: `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`
  - WPF ROI object verification 통과: annotation object, ROI matrix, shell state 모두 PASS
  - 전체 회귀 통과. 전체 테스트 내 주요 수치: `ROI_500K_SINGLE_DELETE_MS=8.623`, `TEXTURE_PAN_1000_MOUSEMOVE_MS=9.891`, `HOVER_500K_1000_MOUSEMOVE_MS=53.159`, `ROI_500K_RENDER_REBUILD_MS=6.851`, `ROI_500K_FULL_VIEW_REBUILD_MS=8.674`, `DETECTION_500K_HIT_MS=0.237`, `DETECTION_500K_RENDER_CULL_MS=0.288`
  - 선택 잔상 수정 후 focused WPF ROI object verification: `WPF_OBJECT_REVIEW_DELETE_MS=6.128 REMAINING_ROIS=0 REMAINING_OVERLAYS=0 SELECTED_EMPTY=True`
  - 선택 잔상 수정 후 focused 50만 ROI 삭제: `ROI_500K_SINGLE_DELETE_MS=12.220 VISIBLE_CACHE_DIRTY=False VISIBLE_SHAPES=0 OBJECTS=499999`
  - detection direct hit-test 추가 최적화 후 focused: `DETECTION_500K_HIT_MS=0.002 DETECTION_500K_HIT_MAX_MS=0.058 HIT_INDEX=499999`
  - 최종 전체 회귀 통과. 주요 수치: `ROI_500K_SINGLE_DELETE_MS=3.786`, `WPF_OBJECT_REVIEW_DELETE_MS=3.362 SELECTED_EMPTY=True`, `TEXTURE_PAN_1000_MOUSEMOVE_MS=10.417`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=28.203`, `ROI_500K_MOUSE_EVENT_RESIZE_1000_MS=7.762`, `DETECTION_500K_HIT_MS=0.001`, `DETECTION_500K_HIT_MAX_MS=0.130`, `DETECTION_500K_RENDER_CULL_MS=0.262`
- 자체평가:
  - 이번 삭제 병목의 직접 원인은 "삭제"가 아니라 삭제 후 전체 캔버스 ROI 재생성과 side list 전체 Reset, 큰 group bounds scan이 한 경로에 묶인 구조였습니다.
  - 단일 ROI 삭제는 이제 overlay manager/spatial index/visible cache에서 해당 객체만 빠지는 구조입니다.
  - 50만 개 목록에서 작은 목록처럼 즉시 전체 재번호를 강제하지 않습니다. 대신 SourceId로 후속 명령 정확성을 유지하고, 다음 전체 refresh 시 표시 번호가 정규화됩니다.
  - 실제 사용자 화면처럼 "우측 목록은 비었지만 선택 핸들이 남는" 상태는 `_selectedRect`가 overlay 생명주기와 분리되어 있었기 때문입니다. 앞으로 side-panel 삭제는 반드시 canvas live selection까지 함께 정리합니다.

## 2026-06-28 dataset wizard initial classes UX

- `WpfDatasetSetupWizardWindow` now labels the initial class field as multi-class capable and shows an example: `Defect, OK, NG`.
- `WpfDatasetSetupWizardViewModel.ClassSummaryText` shows the parsed class count and names next to the input. The existing parser still accepts comma, semicolon, and newline separators and removes case-insensitive duplicates.
- Verified: build passed, `--wpf-dataset-wizard-smoke` captured `tests\artifacts\ui\wpf-dataset-setup-wizard.png`, and full `LabelingApplication.Tests` regression passed. The regression now checks `Defect, OK, NG` creates three class names.

## 2026-06-28 class catalog rename and color UX

- `ClassCatalogService` now supports preserving a class item while renaming its `Text`, plus changing its `DrawColor` without changing YOLO class order.
- `WpfClassCatalogPanel` now exposes `변경`, color preset chips (`정상`, `불량`, `주의`, `검토`, `세그`, `이물`), and `색 적용`.
- The shell no longer treats `Defect` as undeletable. Deletion is blocked only when it would leave zero classes; users can rename `Defect` to `NG`/`이물`, or add another class and then delete `Defect`.
- Active manual ROI, segmentation, and confirmed candidate labels are synchronized when a class is renamed so the current image does not show stale class names.
- Verified: build passed, `--wpf-visual-smoke --review-tab classes` captured `tests\artifacts\ui\wpf-detection-overlay-visual-check.png`, and full `LabelingApplication.Tests` regression passed.

## 2026-06-28 ROI copy/paste and nested-class drawing UX

- `Ctrl+V` paste now calls an immediate canvas repaint after adding the copied ROI, so the pasted box is visible without waiting for another viewer update.
- The canvas ViewModel exposes a class-aware draw-over predicate owned by the WPF shell. Same-class ROI clicks still select/edit the existing box, but dragging with a different active class starts a new nested box inside the broad label.
- This is protected in `docs\STABLE_VERIFIED_AREAS.md`; do not remove the immediate paste repaint or class-aware nested drawing behavior when adjusting ROI hit-testing.
- Verified: build passed; `--wpf-roi-object-verification` passed with `WPF_OBJECT_REVIEW_DELETE_MS=7.644` and `WPF_OBJECT_REVIEW_DELETE_THEN_ZOOM_MS=1.543`; `--roi-geometry` passed; full `LabelingApplication.Tests` regression passed. Full-regression samples included `TEXTURE_PAN_1000_MOUSEMOVE_MS=6.757`, `ROI_500K_MOUSE_EVENT_MOVE_1000_MS=16.318`, `ROI_500K_SINGLE_DELETE_MS=3.352`, and `ROI_OVERLAP_REPEAT_HIT_MS=4.649`. Real EXE ROI smoke also passed: `EXE_ROI_TOOLS_SMOKE seed=260628 boxes=5 boxAvgMs=200.0 deleteThenWheelUiMs=18.5 boxSelected=True rowVisible=True deleteEnabled=True`.

## 2026-06-29 training settings beginner guidance

- `WpfTrainingSettingsPanel` now shows readable Korean labels, a visible recommendation summary, and per-field explanations for image size, batch, epoch, model structure, weights, validation ratio, final validation ratio, and split seed.
- Added `ApplyFastTrainingPresetButton` bound to `WpfTrainingSettingsPanelViewModel.ApplyFastRecommendationCommand`.
- The fast first-training preset applies: image size `320`, batch `16`, epoch `50`, model structure `yolov5s`, weights `yolov5s`, validation `20%`, final validation `0%`, split seed `17`.
- The recommendation button follows workflow command state and disables while training is running.
- This is protected in `docs\STABLE_VERIFIED_AREAS.md`; do not revert the training panel to unexplained numeric inputs.
- Verified: build passed; `--wpf-visual-smoke --review-tab yolo` passed; full `LabelingApplication.Tests` regression passed; `git diff --check` passed with only LF-to-CRLF warnings.

## 2026-06-29 template batch auto label storage and EXE check

- Added a focused regression flag: `--template-batch-autolabel-storage`.
- The test creates synthetic PNG images, runs the real `TemplateMatchingBatchAutoLabelService`/`MatchingTool` path, saves YOLO labels for an unlabeled queued image, reloads the generated label as the selected `Part` class, and verifies the PNG source extension is preserved.
- The same test verifies existing `.txt` labels are skipped both by `BuildUnlabeledImagePathQueue` and by `MatchAndSaveImage`, and that the existing label file contents are not overwritten.
- Added `--exe-template-batch-autolabel-smoke`. The real EXE applies a temporary recipe, loads a controlled three-image dataset, draws a box on the active template image with native mouse input, clears the queue filter, clicks `템플릿 배치`, and verifies the unlabeled target gets one YOLO object while the active source and existing-label image are skipped.
- Added stable AutomationIds for the template candidate and template batch buttons: `TemplateMatchingButton` and `TemplateBatchQueueButton`.
- The batch template score threshold is `0.7`. The previous `0.9` setting produced `No candidates found` in the real EXE smoke with a visually identical template/target pair, while the lower threshold still keeps the existing-label skip and active-image exclusion safeguards.
- Verified: `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`, `--mvvm-infra`, `--yolo-annotation-storage`, `--wpf-visual-smoke --roi-only --width 1280 --height 820`, `--wpf-image-queue-status`, `--template-batch-autolabel-storage`, and `--exe-template-batch-autolabel-smoke` all passed.
- Remaining: broader validation on a real industrial image folder is still useful before calling template batch auto labeling fully production-ready, but the real EXE operator-style path is now covered by automation.

## 2026-06-29 TEST dataset training failure diagnosis

- Investigated `TEST_Dataset_ObjectDetection_20260628_212353`, which maps to `artifacts\run\Debug\DATA\Dataset_ObjectDetection_20260628_212353\data.yaml`.
- Dataset structure was valid for the current object detection export: train 97 images/97 label files/112 box lines, valid 28 images/28 label files/29 box lines, test 0; no missing label files, no orphan label files, and no malformed YOLO label lines.
- The apparent `125 images / 141 labels` count is expected because the UI summary reports 141 box objects, not 141 label files.
- The actual training failure is in the YOLOv5 process before dataset loading. The log shows `torch.load(C:\Git\yolov5\best.pt)` failing with PyTorch's `weights_only` checkpoint restriction: `_pickle.UnpicklingError: Weights only load failed` and `Unsupported global: GLOBAL models.yolo.DetectionModel`.
- The failing command used `C:\Git\yolov5\.venv\Scripts\python.exe`, `C:\Git\yolov5\yolov5Master\train.py`, weights `C:\Git\yolov5\best.pt`, cfg `C:\Git\yolov5\yolov5Master\models\yolov5x.yaml`, and the TEST dataset `data.yaml`.
- No application code or dataset files were changed for this diagnosis.

## 2026-06-29 YOLOv5 PyTorch checkpoint load compatibility fix

- Added `C:\Git\yolov5\yolov5Master\utils\torch_compat.py` with `torch_load_checkpoint`, which explicitly uses `weights_only=False` on PyTorch versions that default checkpoint loads to weights-only mode.
- Updated YOLOv5 object detection training, segmentation training, optimizer stripping, experimental model loading, and hub custom model loading to use the compatibility loader.
- This fix is intentionally limited to trusted local YOLOv5 checkpoint loading. It does not alter exported dataset files, labels, or the WPF app workflow.
- Verified direct loading of `C:\Git\yolov5\best.pt` through `torch_load_checkpoint`.
- Verified the previous failure point is cleared by running `train.py` with the same `best.pt` and `yolov5x.yaml`; the run reached `Transferred 82/745 items from C:\Git\yolov5\best.pt`.
- Verified end-to-end with a temporary 1-image train/valid mini dataset for 1 epoch on CPU. Training completed, `last.pt` and `best.pt` were stripped, and final validation completed.

## 2026-06-29 YOLO worker startup timeout adjustment

- Investigated the UI message `YOLO Python client did not connect within 30000ms`.
- The worker did not fail. The log shows timeout at `12:10:21`, then the same Python process loaded `C:\Git\yolov5\best.pt` and connected at `12:10:37` with `loadMs=43274`.
- Updated `GetWorkerConnectTimeoutMilliseconds` so first worker startup waits for model-load grace time: configured detection timeout plus 90 seconds, clamped to 120-300 seconds.
- This keeps interactive reconnects short when a client is already connected, but prevents slow CPU model preload from being reported as a connection failure.
- Verified: app build passed, `--mvvm-infra` passed, and the WPF single-detection source regression now checks the startup grace-time rule.

## 2026-06-29 post-training model UX cleanup

- Clarified the training/result flow after a YOLO run:
  - Training settings now call the pretrained starting point `학습 시작 가중치`.
  - Model settings now call the inference file `검사 모델(.pt)`.
  - Status text distinguishes `새 학습 모델 후보`, `현재 검사 모델`, and missing `results.csv` metrics.
- Fixed the post-training comparison baseline path. When a completed run finds a newer `best.pt`, the app still stages that file in model settings, but it now remembers the pre-training inspection model and passes it as `baselineWeightsOverride` to model comparison. This prevents `기존 모델` and `새 모델` from becoming the same file immediately after auto-staging.
- Rewrote corrupted Korean user-facing strings in the YOLO model settings panel, training settings panel, training progress status, YOLO runtime status, and training result comparison service.
- Manual model-file browsing now clears the pending training baseline so a deliberate operator model change does not leave stale comparison state.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj -- --mvvm-infra` passed.
  - `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj -- --wpf-model-comparison-heldout` passed.
  - `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj -- --wpf-yolo-training-session-smoke` passed and captured `tests\artifacts\ui\wpf-yolo-training-session-smoke.png`.
  - `dotnet run --project tests/LabelingApplication.Tests/LabelingApplication.Tests.csproj -- --wpf-learning-workflow-panel` passed.
- Full `LabelingApplication.Tests` was not clean because it stopped later at `OpenGL mouse pan avoids per-event pixel readback: ROI canvas display cap status should use a compact operator-readable label`. This is outside the post-training UX change and was not modified.

## 2026-06-29 YOLO training live-state UX clarification

- Diagnosed the current TEST training run after the UI showed `학습 started / YOLO training accepted by worker`.
- Runtime evidence: the latest `runs\train\exp4` had `labels.jpg`/`labels_correlogram.jpg`, but no `results.csv`, `weights\best.pt`, or `weights\last.pt`; no active `train.py` parent process was present. This means the run was not completed, and the UI text should not imply completion.
- Updated WPF training state handling so `started`/`running` worker status keeps the training session busy after the start command returns. `시작` remains disabled and `중지` remains available while the worker reports a live training state.
- Progress text now distinguishes command acceptance and pre-epoch initialization: accepted worker status is shown as `학습 명령 수락됨` with `에폭 시작 전`, not as a completed model.
- Added focused regressions for accepted worker status, stop availability, command gating, and training panel button state.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=C:\Git\Labelling_Application\artifacts\codex-build\Debug\` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=C:\Git\Labelling_Application\artifacts\codex-test-build\Debug\` passed.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe --mvvm-infra` passed.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe --wpf-yolo-training-session-smoke` passed.
  - Default `dotnet build .\MvcVisionSystem.csproj ...` was blocked because the currently running app process `MvcVisionSystem (30144)` and Visual Studio held files under `artifacts\run\Debug`; the app was not closed automatically.

## 2026-06-29 YOLO training epoch-wait hang diagnosis and worker fix

- Diagnosed the later TEST training run while the UI showed `에폭 대기`.
- Runtime evidence: the latest `C:\Git\yolov5\yolov5Master\runs\train\exp5` had `labels.jpg` and `labels_correlogram.jpg`, but no `results.csv`, `weights\best.pt`, or `weights\last.pt`. The log reached `Using 8 dataloader workers` and the epoch header, but no epoch row was emitted. The `train.py` parent process was no longer present and Python CPU counters were not increasing.
- Root cause conclusion: this was not an active epoch run. The YOLOv5 training process was stuck or died around Windows dataloader worker startup, and inherited stdout handles could keep the worker monitor waiting instead of reporting failure.
- Updated `C:\Git\yolov5\labeling_tcp_client.py` so generated training commands default to `--workers 0` unless the caller explicitly supplies a workers value. This avoids the Windows multiprocessing dataloader path for labeling-app initiated training.
- Updated the Python worker monitor to read training stdout on a daemon thread and wait on the training process separately. If the parent process exits while child handles keep stdout open, the worker can still report completed/failed/stopped status instead of waiting indefinitely.
- Updated the WPF pre-epoch status text from `에폭 대기` to `에폭 시작 전` so operators can tell that no real epoch row has started yet.
- Verified:
  - `C:\Git\yolov5\.venv\Scripts\python.exe -m py_compile C:\Git\yolov5\labeling_tcp_client.py C:\Git\yolov5\labelling_tcp_client.py` passed.
  - `C:\Git\yolov5\.venv\Scripts\python.exe C:\Git\yolov5\labelling_tcp_client.py --self-test` passed.
  - Direct `_build_train_command` verification produced a command ending with `--workers 0`.
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=C:\Git\Labelling_Application\artifacts\codex-build\Debug\` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=C:\Git\Labelling_Application\artifacts\codex-test-build\Debug\` passed.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe --mvvm-infra` passed.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe --wpf-yolo-training-session-smoke` passed.

## 2026-06-29 TEST dataset CPU training size failure diagnosis

- Diagnosed the next TEST training failure after the UI received `TaskStatus state=failed`.
- The failed UI run was `C:\Git\yolov5\yolov5Master\runs\train\exp6`; `opt.yaml` confirmed `workers: 0`, so the previous Windows dataloader worker path was no longer the cause.
- `exp6` produced `labels.jpg` and `labels_correlogram.jpg`, but no `results.csv`, `weights\best.pt`, or `weights\last.pt`.
- Reproduced the failing setting directly with `yolov5x.yaml`, `imgsz 640`, `batch-size 16`, `workers 0`, and the TEST dataset. The dataset loaded successfully, auto-anchor completed, and the run reached the first training progress line `0/7`; then the process exited with code `1` before any epoch result or Python traceback.
- The failed model summary was YOLOv5x: 86,224,543 parameters and 204.6 GFLOPs on CPU.
- Control run on the same dataset completed with `yolov5s.yaml`, `imgsz 320`, `batch-size 4`, `workers 0`, and `epochs 1`. It wrote `results.csv`, `weights\last.pt`, and `weights\best.pt` under `artifacts\codex-yolo-diagnose\safe-small-repro`.
- Conclusion: the TEST dataset is trainable; the failure is the selected CPU training size (`yolov5x` + `640` + `batch 16`) being too heavy for the current machine/process state. Use the fast preset (`yolov5s`, `320`, small batch) for this dataset unless a CUDA GPU path is explicitly available and verified.

## 2026-06-29 TEST dataset training completion and active-model visibility

- After the app was closed, verified that no `MvcVisionSystem`, YOLO worker client, or `train.py` process was still running.
- Verified the completed TEST run at `C:\Git\yolov5\yolov5Master\runs\train\exp7`: `results.csv`, `weights\best.pt`, and `weights\last.pt` exist. `opt.yaml` records `data: C:\Git\Labelling_Application\artifacts\run\Debug\DATA\Dataset_ObjectDetection_20260628_212353\data.yaml`, `cfg: yolov5s.yaml`, `imgsz: 320`, `batch_size: 4`, `epochs: 100`, and `workers: 0`.
- The saved recipe still showed `C:\Git\yolov5\best.pt`, so the user could not reliably tell from the UI whether inspection was using the newly trained model or the old inspection model unless the staged model setting was saved.
- Updated `WpfTrainingWeightsService` so post-training model discovery also searches `ProjectRoot\yolov5Master\runs\train`, prefers runs whose `opt.yaml data:` points at the current dataset `data.yaml`, and treats current-dataset training as complete only when a matched `best.pt` has readable `results.csv` metrics.
- UI status now formats trained weights as `exp7\best.pt` instead of only `best.pt`, so the user can distinguish the current inspection model from a newly trained run with the same file name.
- The YOLO training workflow step now marks training complete from persisted run artifacts, not only from live worker status, so reopening the app after a completed run can still show that the current dataset already has a completed training result.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=C:\Git\Labelling_Application\artifacts\codex-build\Debug\` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=C:\Git\Labelling_Application\artifacts\codex-test-build\Debug\` passed.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe` passed through `WPF training weights service selects latest best.pt` and stopped later at the pre-existing `Project settings persist dataset purpose in recipe config: Expected '1', got '0'`.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe --wpf-yolo-training-session-smoke` passed.
  - `artifacts\codex-test-build\Debug\LabelingApplication.Tests.exe --mvvm-infra` passed.
  - `C:\Git\yolov5\.venv\Scripts\python.exe -m py_compile C:\Git\yolov5\labeling_tcp_client.py C:\Git\yolov5\labelling_tcp_client.py` passed.
  - `C:\Git\yolov5\.venv\Scripts\python.exe C:\Git\yolov5\labelling_tcp_client.py --self-test` passed.

## 2026-06-29 canvas label/inference display mode separation

- Added an explicit canvas display selector with three modes:
  - `라벨`: show manual labels, confirmed labels, polygons, and masks only.
  - `추론`: show AI inference candidates and the inference result card only.
  - `모두`: show labels and AI candidates together for comparison.
- The selector is ViewModel-bound through `WpfCanvasPanelViewModel.DisplayModes`, `SelectedDisplayMode`, and `DisplayModeSelectionChangedCommand`; the panel code-behind only exposes the control for shell composition/visual smoke registration.
- `RedrawReviewRois()` now applies the display policy instead of always drawing manual labels, confirmed labels, and pending AI candidates together.
- Result cards are hidden in `라벨` mode so users do not confuse labeling state with inference results.
- New inference results, batch inference results, and model-comparison examples switch to `추론` mode. Labeling workflow/tool selection and AI overlay reset switch back to `라벨` mode.
- Focus actions are mode-aware: candidate focus switches to `추론`, and current-label focus switches to `라벨`.
- Fixed the canvas toolbar clipping introduced by the display selector: the toolbar now uses a wrapping layout when the default EXE width leaves less horizontal room in the canvas column, so `선택`/`박스` and `라벨/추론` controls move to the next line instead of being clipped.
- Fixed OpenGL canvas badge mojibake: detection/model-comparison labels drawn directly on the canvas now use ASCII-safe badge text, while WPF result cards and review panels keep Korean user-facing text.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-canvas-panel-commands` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-detection-display-mode` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-canvas-detection-overlay` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-current-image-smoke-preserve-labels` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1430 --height 900` passed and the captured toolbar was not clipped.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1100 --height 720` passed and the captured `보기/라벨/추론/모두` toolbar controls were not clipped at the top.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --width 1430 --height 900` passed and the captured `선택`/`박스` quick-tool buttons were single-line and not clipped at the top.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --width 1440 --height 900 --output tests\artifacts\ui\wpf-toolbar-wrap-1440.png` passed and the default-width toolbar wrapped instead of clipping.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --width 1100 --height 720 --output tests\artifacts\ui\wpf-toolbar-wrap-1100.png` passed and the narrow-width toolbar wrapped instead of clipping.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-canvas-detection-overlay` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-detection-display-mode` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1100 --height 720 --output tests\artifacts\ui\wpf-canvas-ascii-detection-badge.png` passed and the canvas badges rendered as `#1 OK 95.9%` / `#2 NG` without mojibake.
- Full `LabelingApplication.Tests` was not clean because it stopped later at `OpenGL mouse pan avoids per-event pixel readback: ROI canvas display cap status should use a compact operator-readable label`. This is outside the canvas display-mode change and was not modified.
- `git diff --check` for the files changed by this display-mode task reported only LF-to-CRLF warnings. Repository-wide `git diff --check` is still blocked by an existing `CODEX_RECOVERY.md:330: new blank line at EOF` issue.

## 2026-06-29 reused image folder dataset isolation

- Diagnosed the issue where creating a new dataset with the same image folder could show labels from the previous dataset.
- Root cause: YOLO label lookup checked external `Images` sibling `labels` and image sidecar `.txt` paths before the active dataset output root, so stale labels beside the reused source image folder could be treated as labels for the new dataset.
- Updated active-dataset lookup so box labels are loaded from the current dataset output root (`data/train|valid|test/labels`) and external sibling/sidecar labels are ignored while a dataset is active.
- Applied the same isolation rule to segmentation JSON lookup (`data/train|valid|test/segments`) so reused image folders cannot leak old segmentation labels into a new dataset either.
- Legacy standalone lookup without an active `CData` still supports sibling/sidecar labels for compatibility.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --yolo-label-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --segmentation-annotation-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --yolo-annotation-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --template-batch-autolabel-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-current-image-smoke-preserve-labels` passed.

## 2026-06-29 dataset creation output-root reuse fix

- Verified the user's live `TEST2_Dataset_ObjectDetection_20260628_212353` recipe was still showing labels because it pointed at the same active output root as `TEST_Dataset_ObjectDetection_20260628_212353`: `artifacts\run\Debug\DATA\Dataset_ObjectDetection_20260628_212353`.
- That shared output root contained 97 train label files and 28 valid label files, so Test2 was reading real labels from its configured dataset root, not from the external `D:\LabelingData\Test01\Images` folder.
- Updated new dataset creation so the wizard no longer defaults to the currently opened dataset's output root. Existing recipe names are skipped for new setup defaults, and the default output root is generated under `DATA\<new recipe name>`.
- Added a guard in `ApplyDatasetSetupRequest` that rejects a new dataset if its requested output root is already used by another recipe, preventing accidental shared label stores.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj` passed. Existing running processes caused transient copy retry warnings, but final errors were 0.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj` passed with the existing MSIL/AMD64 warning.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-dataset-setup-request` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --yolo-label-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --segmentation-annotation-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --yolo-annotation-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-current-image-smoke-preserve-labels` passed.

## 2026-06-29 template matching first-use guide

- Improved the first-use UX for the top `템플릿` action and image-queue template batch action.
- Previously, missing prerequisites such as no loaded image, no selected source box, or no unlabeled queue images were only written to the lower log, which was easy to miss.
- `WpfTemplateMatchingAutoLabelViewModel` now asks the shell host to show an actionable guide when the operator tries template matching without the required image/source box/queue context.
- The shell keeps the actual WPF `MessageBox` as a View adapter through `IWpfTemplateMatchingAutoLabelHost.ShowAutoLabelGuide`, while the ViewModel owns the workflow-specific guide text and state updates.
- The guide explains the direct sequence: draw/select one source box, run the top template candidate search for the current image, or use the image-queue template batch for unlabeled images.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with the existing MSIL/AMD64 warning.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --template-guide-ux` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --template-batch-autolabel-storage` passed.

## 2026-06-29 reusable WPF message dialog and class selector wording

- Clarified the canvas toolbar class selector so the chip row now reads `클래스 <class>` instead of the ambiguous `라벨 <class>`.
- The object review panel remains the source of truth for actual current-image objects. If it says `현재 이미지 객체 없음`, the `NG`/`OK` chips are not existing labels; they are the active class options used when adding a new box.
- Added a reusable WPF message dialog library project at `OpenVisionLab\Library\OpenVisionLab.Wpf.MessageDialogs`.
- The library contains a WPF `UserControl`, host `Window`, result/buttons/kind options, and a static `WpfMessageDialog` service so other WPF projects can reuse the same dialog without depending on the legacy WinForms `OpenVisionLab.MessageBox`.
- Updated template matching first-use guidance to use `WpfMessageDialog.ShowInfo` through the shell host adapter. The workflow text remains owned by `WpfTemplateMatchingAutoLabelViewModel`.
- Added the new library project to `MvcVisionSystem.sln` and referenced it from `MvcVisionSystem.csproj`.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-canvas-panel-commands` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --template-guide-ux` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --width 1280 --height 820 --output tests\artifacts\ui\wpf-class-selector-label.png` passed, and the captured toolbar showed `클래스 Defect` without clipping.

## 2026-06-29 output-root change reloads active labels

- Clarified the class tab semantics after the user asked why labels remained after changing the save path.
- Class definitions (`NG`, `OK`, etc.) are recipe metadata and intentionally remain with the current recipe when the output root changes.
- Image annotations are output-root data and must follow the selected save path (`data/train|valid|test/labels`).
- Fixed `SaveOutputRootFromEditor()` so changing the save path reloads the active image from the new output root immediately. If the new root has no label file for that image, the canvas/object review no longer keeps labels loaded from the previous root.
- Added a dirty-edit guard: if the current image has unsaved annotation edits, the output-root change is blocked until the user saves the labels first.
- Added a `레시피 클래스` header above the class list and a tooltip explaining that class definitions stay in the recipe even if the save path changes.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-output-root-reload` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --yolo-label-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab classes --width 1280 --height 820 --output tests\artifacts\ui\wpf-recipe-class-header.png` passed and the captured class tab showed the new `레시피 클래스` header.
- A mistakenly broad test run with `--wpf-class-catalog-panel` entered the default full suite and stopped at the existing `OpenGL mouse pan avoids per-event pixel readback: ROI canvas display cap status should use a compact operator-readable label` failure. This is the same pre-existing issue noted above and is outside this output-root change.

## 2026-06-29 template current-image no-candidate status

- Diagnosed the case where drawing a source label and clicking the top `템플릿` button could mark the current image as `실패`.
- Root cause: the current-image template service correctly returned a successful result with zero candidates when no additional matching object was found, but the ViewModel forwarded that zero-count result as `succeeded: false` to the shell host.
- Updated `WpfTemplateMatchingAutoLabelViewModel.RunCurrentImage()` so true template execution failures still mark failure, while successful zero-candidate results are applied as `succeeded: true`. The existing shell review-status path then classifies the image as `검출없음` instead of `실패`.
- Corrected corrupted Korean guide strings in the same template ViewModel so missing-image/missing-source-box guidance appears readable in the popup and status text.
- Follow-up UX fix after live use: successful zero-position template runs now replace stale prerequisite warnings with a visible `템플릿 초안 없음 - 기준 박스를 제외한 추가 위치 없음` status.
- Split the shell host's template current-image zero-candidate path from the general YOLO detection result path. A saved/labeled current image is no longer overwritten as `검출없음` just because the template search found no extra candidate beyond the source box.
- Corrected the top `템플릿` workflow to match the operator expectation:
  - On a labeled source image, selecting one label box and pressing `템플릿` registers that box as the template.
  - On another image, pressing `템플릿` uses the registered template to find the matching position and materializes the result as a manual label on the current image.
  - The new label is not auto-saved; the UI marks `라벨 저장 필요` so the operator can verify and save.
  - Near-duplicate template hits are skipped instead of creating stacked duplicate labels.
- Extended the template batch path so a registered template can be applied across the full image queue once. The source image used to register the template is skipped, and images that already have label files are not overwritten.
- Updated the image queue `템플릿 배치` tooltip to state that it checks the full image list and saves labels only for unlabeled images.
- Added a focused regression test that uses a full-image source template to force the "successful run, zero candidates" path and verifies that it is not reported as a failed detection.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --template-guide-ux` passed, including source-template registration, shifted target-image label creation, and registered-template batch save across the queue.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --template-batch-autolabel-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-template-current-image-no-candidate` passed, including direct materialization of a template candidate into a manual label.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-detection-display-mode` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-current-image-smoke-preserve-labels` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --mvvm-infra` passed.

## 2026-06-30 startup dataset onboarding UX pass

- Audited the first user path from app startup to dataset setup.
- UX finding: the app had dataset setup and selector flows, but when no dataset/image was restored the right panel could remain on the empty object-review tab, forcing the operator to infer the next action.
- Updated startup behavior so an empty/no-restored-dataset shell opens the `가이드/도구` tab and focuses the dataset setup action.
- Updated the dataset selection dialog with an in-list empty state. When no existing dataset is available, the list area now directly presents `새 데이터셋 만들기` instead of relying only on the bottom status text.
- Follow-up UX finding from live labeling/training/inference use: the first guide card still offered only a new-dataset action, while the existing-dataset path was available only in the top header.
- Added an explicit `기존 열기` action beside `새로 만들기` in the guide's dataset setup card. The two buttons now express the operator decision directly: create a new isolated dataset root or open an already saved dataset.
- Changed the primary setup button text from purpose-only wording such as `박스 데이터셋` to the action wording `새로 만들기`; the selected dataset purpose remains visible in the purpose buttons and guide text.
- Added tooltips to both setup actions so hovering explains that `새로 만들기` creates separated image/label storage and `기존 열기` opens a saved dataset.
- Captured before/after UI comparison at the default EXE size:
  - Before: `tests\artifacts\ui\wpf-dataset-onboarding-before-open-existing.png`.
  - After: `tests\artifacts\ui\wpf-dataset-onboarding-after-open-existing.png`.
- Kept the changes inside WPF ViewModel/UI-adapter boundaries:
  - `WpfDatasetSelectionWindowViewModel` owns empty-state visibility.
  - `WpfLearningWorkflowPanelViewModel` owns the new existing-dataset command and action wording.
  - `WpfLearningWorkflowPanel` exposes only a UI adapter for scrolling/focusing the dataset setup action.
  - `WpfLabelingShellWindow` only chooses the initial side-panel focus when no active image is available.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-startup-onboarding` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-startup-dataset-restore` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-dataset-setup-request` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-learning-workflow-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --yolo-annotation-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab guide --width 1440 --height 900 --output tests\artifacts\ui\wpf-startup-guide-ux.png` produced a guide-tab capture matching the default EXE window size.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab guide --width 1440 --height 900 --output tests\artifacts\ui\wpf-dataset-onboarding-after-open-existing.png` produced the after capture showing `새로 만들기` and `기존 열기` in the guide setup card without clipping.

## 2026-06-30 workflow stage rail UX pass

- UX finding after reviewing common annotation-tool flows: the shell still presented dataset setup, labeling, inference review, and training/model work as peer toolbar buttons instead of a first-level operator workflow.
- Added a persistent top workflow rail with four stages:
  - `1. 데이터셋 홈`: create/open dataset and verify storage.
  - `2. 라벨링 워크벤치`: show labels-only work and save ground truth.
  - `3. 추론 검토`: show model candidates-only review and accept/reject.
  - `4. 학습/모델 센터`: check dataset readiness, run training, and verify/apply model candidates.
- Kept canvas display mode and workflow stage as separate shell ViewModel state. This avoids treating the training/model center as a canvas mode while still showing the operator's current high-level step.
- Wired the new stage buttons through `WpfLabelingShellViewModel` commands. The WPF shell remains an adapter that focuses the existing dataset guide, annotation tools, candidate review, or YOLO/training overview.
- Added a focused `--wpf-labeling-shell` test entry so future shell layout/command wiring changes can be verified without running the full suite.
- Captured before/after UI comparison at the default EXE size:
  - Before: `tests\artifacts\ui\wpf-shell-workflow-stage-rail-before.png`.
  - After: `tests\artifacts\ui\wpf-shell-workflow-stage-rail-after.png`.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-startup-onboarding` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-dataset-setup-request` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-detection-display-mode` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-current-image-smoke-preserve-labels` passed.
  - `dotnet .\tests\LabelingApplication.Tests\bin\Debug\net8.0-windows\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab guide --width 1440 --height 900 --output tests\artifacts\ui\wpf-shell-workflow-stage-rail-after.png` produced the after capture with the stage rail visible and not clipped.

## 2026-06-30 training/model center UX pass

- UX finding from the live training/inference flow: after training completed, the YOLO tab still scattered the important answers across status text, logs, model settings, and training settings. The operator could not immediately tell:
  - whether training is running, waiting, failed, or completed;
  - which model is currently configured for inspection;
  - whether a newly trained `best.pt` is only a candidate or already saved to recipe settings;
  - which action should be taken next.
- Added a `학습/모델 센터` summary panel at the top of the YOLO tab. It shows training status, progress, training detail/readiness, current inspection model, newly trained candidate model, adoption state, next action, and the core actions `점검`, `학습 시작`, `중지`, `모델 저장`.
- Kept the detailed YOLO panels below the summary so the top area answers operator questions first and the lower expanders remain for advanced settings.
- Added model-center state to `WpfLabelingShellViewModel` and bound the summary panel to ViewModel properties. The shell code-behind only adapts existing training/model-comparison state into these ViewModel properties.
- Wired the summary refresh to:
  - training progress/status updates;
  - YOLO status refresh;
  - manual model-file selection;
  - training-completed best.pt candidate discovery;
  - model settings save/reset.
- Added regression coverage to `--wpf-labeling-shell` for the summary panel bindings and ShellViewModel state setters.
- Captured before/after UI comparison at the default EXE size:
  - Before: `tests\artifacts\ui\wpf-model-center-dashboard-before.png`.
  - After: `tests\artifacts\ui\wpf-model-center-dashboard-after.png`.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke` passed and confirmed `best.pt` candidate registration.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-model-center-dashboard-before.png` produced the before capture.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-model-center-dashboard-after.png` produced the after capture showing the new summary panel at the top of the YOLO tab.
- Next planned work: continue the structural UX pass on the labeling/inference boundary so manual labels, template-created labels, and AI inference candidates are harder to confuse in the main canvas and side panel.

## 2026-06-30 canvas label/inference layer separation UX pass

- UX finding from the live labeling/inference workflow: the app already had display filters, but `라벨`, `추론`, `모두` were too terse. Operators could still confuse saved/manual labels, template-added labels, and AI inference candidates because the canvas did not clearly state which layer was currently visible.
- Renamed the canvas display modes to operator-facing meanings:
  - `라벨만`: saved/manual/template-added labels only.
  - `AI후보`: model inference candidates only.
  - `비교`: labels and AI candidates together for overlap/position review.
- Added a dedicated canvas layer status strip between the workflow guide strip and quick-tool toolbar. It shows:
  - current visible layer mode;
  - layer-specific guidance;
  - label count and whether label changes are still unsaved;
  - AI candidate count and whether the candidate layer is hidden or visible.
- Kept the layer summary in `WpfCanvasPanelViewModel` through `SetLayerVisibilityState`. The shell only calculates counts from the current canvas state and pushes them to the ViewModel.
- Kept the OpenGL/ROI path unchanged. The existing `RedrawReviewRois`, `ShouldShowLabelOverlays`, and `ShouldShowInferenceOverlays` behavior remains the rendering source of truth; this pass only made the selected layer state visible and testable.
- Captured before/after UI comparison at the default EXE size:
  - Before: `tests\artifacts\ui\wpf-layer-separation-before.png`.
  - After: `tests\artifacts\ui\wpf-layer-separation-after.png`.
- Verified:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-detection-display-mode` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-detection-overlay` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-layer-separation-before.png` produced the before capture.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-layer-separation-after.png` produced the after capture showing `보기: AI 후보만`, label hidden count, and visible AI candidate count.
- Note: an accidental broad test run without a recognized single-test flag still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` issue. The focused tests for this pass passed.
- Next planned work: apply the same explicit separation to the right-side review panel by splitting the object list wording into `저장 라벨` and `AI 후보`, and by making candidate confirmation explain when it will create a saved label versus only skip/hide a candidate.

## 2026-07-01 canvas work-mode wording follow-up

- UX finding from the 1920x1080 candidate-review capture: the app had separate display layers, but `라벨만`/`AI후보`/`비교` still read like filters rather than the operator's current task. This left room for confusion between editing saved labels and reviewing inference candidates.
- Renamed the canvas mode buttons to task-oriented labels:
  - `라벨 편집`: saved labels only; AI candidates hidden.
  - `AI 검토`: inference candidates only; saved labels hidden until candidates are confirmed.
  - `비교`: saved labels and AI candidates overlaid for overlap/missing-object review.
- Updated the canvas layer strip title/detail to state the active work mode:
  - `작업: 저장 라벨 편집`
  - `작업: AI 후보 검토`
  - `작업: 라벨+AI 비교`
- Bound the mode selector tooltip to the same ViewModel-owned layer summary so the XAML remains declarative and the code-behind does not own workflow text.
- Clarified the `객체 없음` action tooltip so it says an empty YOLO label file will be saved.
- Captured before/after comparison at the equipment baseline:
  - Before: `artifacts\ui\wpf-canvas-label-ai-mode-before-1920.png`.
  - After: `artifacts\ui\wpf-canvas-label-ai-mode-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-detection-display-mode` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output .\artifacts\ui\wpf-canvas-label-ai-mode-after-1920.png` produced the after capture.
- Note: a mistakenly broad test run with an unrecognized single-test flag entered the default suite and stopped at `Template auto label shows actionable guide: registered template batch should complete`. The focused tests for this pass passed.
- Next planned work: review the main workbench layout by task mode. In labeling mode, keep image queue + canvas dominant and make the right review/settings area feel like an on-demand dock rather than a permanent competing panel.

## 2026-06-30 right review panel label/candidate separation UX pass

- UX finding from reviewing the post-inference workflow: the canvas now separates label and AI-candidate layers, but the right review panel still used broad wording such as `객체` and `후보`. Operators could not immediately tell whether they were editing saved labels or only reviewing AI candidates.
- Renamed the right review area from `객체 검토` to `라벨/후보 검토`.
- Renamed the side tabs:
  - `저장 라벨`: saved/manual/template/confirmed labels only.
  - `AI 후보`: model-detected candidates that still need confirmation or hiding.
- Added a saved-label role card to the `저장 라벨` tab. It explains that deletion and class changes apply to the real saved label set.
- Added an AI-candidate role card to the `AI 후보` tab. It explains that confirmation creates a saved label and skip hides only the candidate.
- Changed candidate action button text from terse `확정`/`스킵` wording to action-result wording:
  - `라벨 확정`
  - `전체 라벨화`
  - `후보 숨김`
- Kept the new status/guide text inside `WpfObjectReviewPanelViewModel` and `WpfCandidateReviewPanelViewModel`; XAML only binds to those properties.
- Kept code-behind as a UI adapter. Candidate mutation, object edit, selection, and presentation services remain the workflow owners.
- Captured before/after UI comparison at the default EXE size:
  - Candidate before: `tests\artifacts\ui\wpf-review-panel-candidates-before.png`.
  - Candidate after: `tests\artifacts\ui\wpf-review-panel-candidates-after.png`.
  - Saved-label before: `tests\artifacts\ui\wpf-review-panel-objects-before.png`.
  - Saved-label after: `tests\artifacts\ui\wpf-review-panel-objects-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-candidate-review-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-object-review-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-detection-display-mode` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-review-panel-candidates-after.png` produced the final candidate-panel capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab objects --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-review-panel-objects-after.png` produced the final saved-label-panel capture.
- Note: the first attempt to run the candidate/object panel tests in parallel hit a build-output file lock on `obj\Debug\MvcVisionSystem.dll`. Re-running with a single build and sequential tests passed.
- Next planned work: audit the inference review completion flow after `라벨 확정`/`후보 숨김`, especially how the queue status, save-needed state, and next-image action communicate whether the image is finished.

## 2026-06-30 candidate review completion/next-action UX pass

- UX finding from the live inference-review flow: after `라벨 확정` or `후보 숨김`, the operator still had to infer whether the image could be completed, whether labels would be saved, and what the next button would actually do.
- Added a visible completion state card in the `AI 후보` tab. It now tells the operator:
  - whether there are still pending AI candidates;
  - whether the current labels are unsaved;
  - whether the next action is `라벨 확정/후보 숨김`, `저장 후 다음`, `다음 이미지`, or `객체 없음 완료`.
- Moved that completion/next-action card to the top of the candidate tab, directly under the AI-candidate role card, so it remains visible at the default EXE window size without scrolling.
- Changed the finish button from a static `이미지 완료` label into state-specific button text:
  - `후보 검토 필요` while candidates remain.
  - `저장 후 다음` after candidates are resolved and labels need saving.
  - `다음 이미지` after resolved labels are already saved.
  - `객체 없음 완료` when the reviewed image has no labels/candidates.
- Kept MVVM boundaries:
  - `WpfCandidateReviewCompletionPresentationService` owns the completion wording and enablement policy.
  - `WpfCandidateReviewPanelViewModel` owns the bound completion state and button text.
  - `WpfCandidateReviewPanel.xaml` only binds the top completion card and action button.
  - The shell passes current facts only: active image, detection state, pending candidate count, current label count, and dirty state.
- Kept the viewer/OpenGL/ROI/brush/eraser performance paths unchanged. The only shared canvas helper change was a count helper used for UI state text.
- Captured before/after UI comparison at the default EXE size:
  - Before: `tests\artifacts\ui\wpf-candidate-completion-before.png`.
  - After: `tests\artifacts\ui\wpf-candidate-completion-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-candidate-review-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-session-smoke` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-detection-display-mode` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-candidate-completion-after.png` produced the final after capture showing the top completion card and disabled `후보 검토 필요` state while AI candidates remain.
- Next planned work: audit the left image queue wording/status so `후보`, `확정`, `스킵`, `검출없음`, and saved-label completion states use the same `저장 라벨` / `AI 후보` language as the canvas and right panel.

## 2026-06-30 top header tools compaction UX pass

- UX finding from the default EXE-size header review: the top header still exposed occasional actions (`테마`, `샘플`, `박스`, `라벨링 시작`, `추론 검토`, `YOLO`, `템플릿`) as peer buttons. This duplicated the workflow stage rail and used prime horizontal space for actions that are not needed every minute.
- Kept the top header focused on frequent actions:
  - `라벨 저장`
  - `현재 검사`
  - `도구`
  - inference status
- Moved occasional commands behind the `도구` gear menu:
  - theme toggle
  - sample load
  - centered box add
  - labeling mode
  - inference review mode
  - YOLO check
  - template candidate search
- Kept command ownership unchanged. Existing commands still bind through `WpfLabelingShellViewModel`; the shell XAML only changes placement/presentation.
- Preserved stable control names and AutomationIds for existing focused EXE smoke tests, including sample load, centered box add, current inspection, and template candidate search.
- Captured comparison at the default EXE size, 1440x900:
  - Before: `tests\artifacts\ui\wpf-top-header-tools-before.png`.
  - After: `tests\artifacts\ui\wpf-top-header-tools-after.png`.
  - Menu open: `tests\artifacts\ui\wpf-top-header-tools-menu-open.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-top-header-tools-after.png` produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --open-header-tools-menu --output .\tests\artifacts\ui\wpf-top-header-tools-menu-open.png` produced the gear-menu capture.
- Note: a temporary 1100x720 narrow-width capture was used only as a stress check, but the recorded UX comparison for this pass is the default EXE size, 1440x900.
- Next planned work: continue with the left image queue wording/status audit so queue rows and filters use the same `저장 라벨` / `AI 후보` terminology as the canvas, right panel, and completion flow.

## 2026-06-30 left image queue terminology/status UX pass

- UX finding from the default EXE-size queue review: the left image queue still used terse mixed status words (`후보`, `확정`, `스킵`, `검출없음`) while the canvas and right panel now separate saved labels from AI candidates. This made it too easy to confuse a saved label with a model candidate.
- Changed queue quick filters to use the same language as the rest of the workflow:
  - `AI후보`
  - `저장됨`
  - `숨김`
  - `객체없음`
- Changed row status presentation:
  - The saved-label column is now headed `저장`, not generic `라벨`.
  - The AI column continues to show AI candidate state, such as `AI후보 2`.
  - Row summaries and tooltips now spell out `저장 라벨 ... / AI ...` so the compact columns still have an explicit explanation.
- Changed dataset status summaries and filter dropdown display names to use the same terms.
- Kept MVVM boundaries:
  - `WpfImageQueuePresenter` owns row badge, summary, detail, and review-count wording.
  - `WpfImageQueuePanelViewModel` owns quick-filter count text and active state.
  - `WpfImageQueuePanel.xaml` only binds those properties and adjusts labels/column widths.
  - `WpfLabelingShellWindow.ImageQueuePresentation` remains an adapter that forwards item counts and active filter state.
- Captured comparison at the default EXE size, 1440x900:
  - Before: `tests\artifacts\ui\wpf-image-queue-terminology-before.png`.
  - After: `tests\artifacts\ui\wpf-image-queue-terminology-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-image-queue-status` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-image-queue-terminology-after.png` produced the final after capture showing the `저장`/`AI` columns and `AI후보` quick filter wording.
- Next planned work: audit the workflow-stage rail and primary actions next, so first-time users know whether they should pick a dataset, label, run inference, train, or apply a model without reading the bottom log.

## 2026-06-30 workflow stage rail current-action UX pass

- UX finding from the default EXE-size workflow review: the 4 stage buttons existed, but the active step only appeared as a red button. A first-time operator still had to infer the current task and next action from small button subtitles or the bottom log.
- Added a current-stage summary panel to the right side of the top workflow rail. It now shows:
  - current progress, such as `3/4 추론`;
  - the active stage name;
  - the current stage's operational scope;
  - the next expected action.
- Added `WpfWorkflowStagePresentationService` to own stage wording and keep the ViewModel free of hard-coded rail layout logic.
- Added bound Shell ViewModel state:
  - `WorkflowStageProgressText`
  - `WorkflowStageTitleText`
  - `WorkflowStageDetailText`
  - `WorkflowStageNextActionText`
- Kept MVVM boundaries:
  - `WpfWorkflowStagePresentationService` builds the stage presentation.
  - `WpfLabelingShellViewModel.SetWorkflowStage` updates active stage state and the stage summary text.
  - `WpfLabelingShellWindow.xaml` only binds the summary panel and existing stage buttons.
  - The shell command partials still only route stage changes and focus the relevant panel.
- Captured comparison at the default EXE size, 1440x900:
  - Before: `tests\artifacts\ui\wpf-workflow-stage-rail-before.png`.
  - After: `tests\artifacts\ui\wpf-workflow-stage-rail-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-workflow-stage-rail-after.png` produced the final after capture showing the current-stage summary on the workflow rail.
- Next planned work: audit the dataset entry and storage-path flow, especially how `데이터셋 선택`, `저장 폴더`, and `이미지 폴더` are explained before labeling starts.

## 2026-06-30 dataset context storage/image role UX pass

- UX finding from the default EXE-size dataset bar review: the header showed `저장: ... / 이미지: ...` in one line and used ambiguous buttons such as `폴더 열기` and `이미지 변경`. This did not clearly explain that saved labels/recipes live under the dataset storage folder while source images are read from a separate image folder.
- Split the dataset context bar into explicit role areas:
  - `저장 폴더`: shows `라벨/레시피 저장: ...`.
  - `이미지 폴더`: shows `원본 이미지 폴더: ...`.
- Renamed the action buttons in the bar:
  - `폴더 열기` -> `저장 폴더`.
  - `이미지 변경` -> `이미지 폴더`.
- Added `WpfDatasetContextPresentationService` so storage/image wording and path shortening are owned outside the XAML and code-behind.
- Added bound Shell ViewModel state:
  - `CurrentDatasetStoragePathText`
  - `CurrentDatasetImageRootText`
- Kept the existing combined `CurrentDatasetPathText` for compatibility, but the visible UI now uses the split storage/image fields.
- Kept MVVM boundaries:
  - `WpfDatasetContextPresentationService` builds the display text and tooltip.
  - `WpfLabelingShellViewModel.SetDatasetContext` applies the presentation.
  - `WpfLabelingShellWindow.xaml` only binds the dataset identity, storage card, image card, and existing commands.
  - Shell command partials still only route dataset selection, storage-folder open, and image-folder selection.
- Captured comparison at the default EXE size, 1440x900:
  - Before: `tests\artifacts\ui\wpf-dataset-context-before.png`.
  - After: `tests\artifacts\ui\wpf-dataset-context-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-dataset-context-after.png` produced the final after capture showing separate storage/image cards and explicit action buttons.
- Next planned work: inspect the dataset selection/create dialogs themselves. The main bar is now clearer, but first-time users still need the selection and creation windows to explain when a new isolated storage folder is created versus when an existing dataset is opened.

## 2026-06-30 dataset selection/create isolation UX pass

- UX finding from the dataset entry flow: the main header now separates storage and image folders, but the selection and creation dialogs still made the operator infer whether they were opening existing labels or creating a new isolated storage folder.
- Changed the dataset selection dialog:
  - Added separate action cards for `기존 데이터셋 열기` and `새 저장 폴더 만들기`.
  - Existing-dataset rows now label `라벨/Recipe 저장` and `원본 이미지 폴더` separately.
  - Row status now includes whether the row is already open or should be selected before opening.
  - The dialog size is wider so the storage/image roles are visible without relying on the bottom log.
- Changed the dataset creation wizard:
  - Increased the default dialog width.
  - The wizard now explains that a new dataset creates a new storage folder and connects source images separately.
  - The selected start-data/source-image role is shown directly under `시작 데이터`.
  - The storage folder section explains that labels, recipe, and training files are created there.
  - The preview now uses Korean workflow wording and includes the isolation rule: same source images can be reused safely when the storage folder is new.
- Kept MVVM boundaries:
  - `WpfDatasetSetupWizardViewModel` owns storage, image-source, isolation, and preview text.
  - `WpfDatasetSelectionWindowViewModel.WpfDatasetSelectionItem` owns row display text for storage and source image roles.
  - `WpfDatasetSetupWizardWindow.xaml` and `WpfDatasetSelectionWindow.xaml` only bind these values and arrange the controls.
  - Shell dataset setup code remains the composition/persistence adapter.
- Captures:
  - Wizard before: `tests\artifacts\ui\wpf-dataset-setup-wizard-before.png`.
  - Wizard after: `tests\artifacts\ui\wpf-dataset-setup-wizard-after.png`.
  - Dataset selector after: `tests\artifacts\ui\wpf-dataset-selection-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-setup-ui` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-setup-request` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-wizard-smoke --output .\tests\artifacts\ui\wpf-dataset-setup-wizard-after.png` produced the wizard after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-selection-smoke --output .\tests\artifacts\ui\wpf-dataset-selection-after.png` produced the selector capture.
- Next planned work: audit the class/catalog and label-save flow next. The dataset entry path is clearer, but first-time users still need less ambiguity around when a drawn box is saved, when class changes apply, and when unsaved labels remain only on the canvas.

## 2026-06-30 canvas label-save/class application UX pass

- UX finding from the default EXE-size labeling view: the canvas already had a local `라벨 저장` button, but the file-save state and the "next drawn class" were mixed into the toolbar. It was still easy to miss whether an edit was only on the current canvas or already written to the label file.
- Changed the canvas toolbar:
  - Added `CanvasAnnotationSaveStateCard` beside the save button.
  - The card now shows `저장 필요`, `파일 저장됨`, or `이미지 대기` as ViewModel state.
  - Added `CanvasActiveLabelClassCard` so the operator can see which class the next box/mask will use.
  - The class detail explicitly says existing object classes are changed in the right object-review panel, not by changing the next-label chip.
- Changed object-review edit behavior:
  - Applying a different class to a selected object now marks the current image as needing label save.
  - Deleting a selected object now marks the current image as needing label save.
  - If a dirty image has zero remaining objects, the shared save path now uses the existing empty-label save routine instead of leaving the save action with no file write.
- Updated object-review guidance:
  - The right panel now explains that delete/class changes affect the current image immediately, but require `라벨 저장` to persist to file.
- Kept MVVM boundaries:
  - `WpfCanvasPanelViewModel` owns save-state and active-class presentation text.
  - `WpfCanvasPanel.xaml` only binds the new cards and keeps commands unchanged.
  - `WpfLabelingShellWindow.ObjectReviewCommands.cs` remains the command adapter that mutates current labels and marks them dirty.
  - `WpfLabelingShellWindow.AnnotationPersistence.cs` keeps file persistence in the existing save service path.
- Captured comparison at the default EXE size, 1440x900:
  - Before: `tests\artifacts\ui\wpf-canvas-label-save-before.png`.
  - After: `tests\artifacts\ui\wpf-canvas-label-save-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-object-review-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1440 --height 900 --output .\tests\artifacts\ui\wpf-canvas-label-save-after.png` produced the after capture.
- Note: an accidental broad test run was triggered with the wrong flag and stopped at the pre-existing `Template auto label shows actionable guide: registered template batch should complete` failure. The focused object-review flag was then run separately and passed.
- Next planned work: audit the class catalog panel layout itself. It still combines class editing with output-root editing, so the next pass should decide whether storage-path controls belong in a dataset/settings area rather than in the class list panel.

## 2026-06-30 first-run dataset-start priority pass

- UX finding from the 1366x768 startup onboarding capture: the beginner `처음 실습 경로` card was visible before the actual dataset start actions, so a first-time operator could see the lesson path but not immediately see where to create or open a dataset.
- Changed the Guide right-panel order:
  - `데이터셋 준비` now appears directly after the dataset-purpose explanation.
  - `새로 만들기` and `기존 열기` are visible before the beginner sample path on smaller startup screens.
  - `처음 실습 경로` remains available below the start actions instead of competing with them for the first visible decision.
- Kept MVVM boundaries:
  - Existing dataset setup commands remain on `WpfLearningWorkflowPanelViewModel`.
  - `WpfLearningWorkflowPanel.xaml` only changes layout order and bindings.
  - The shell code-behind was not touched for this pass.
- Captured comparison:
  - 1366 before: `tests\artifacts\ui\wpf-startup-onboarding-1366.png`.
  - 1366 after: `tests\artifacts\ui\wpf-startup-onboarding-after-1366.png`.
  - 1920 before: `tests\artifacts\ui\wpf-startup-onboarding-1920.png`.
  - 1920 after: `tests\artifacts\ui\wpf-startup-onboarding-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-learning-workflow-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-startup-onboarding-visual --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-startup-onboarding-after-1920.png` produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-startup-onboarding-visual --width 1366 --height 768 --output .\tests\artifacts\ui\wpf-startup-onboarding-after-1366.png` produced the after capture.
- Next planned work: tomorrow, continue with the class/catalog and storage-path placement audit, then resume the first-run-to-training sweep at 1920x1080 and 1366x768.

## 2026-07-01 labeling-tool benchmark refresh and class schema pass

- Rechecked the UX direction against official documentation for CVAT, Label Studio, Roboflow, and Labelbox. The recurring pattern is that dataset/task setup, label schema or ontology setup, image import, annotation, and AI-assisted review are separate operator decisions.
- UX finding: our class tab still exposed `데이터셋 저장 폴더(고급)`, which made class/schema editing look like the place to change project storage. That conflicts with the dataset-home flow already added for `새로 만들기`, `기존 열기`, and storage/image role separation.
- Changed the class catalog panel:
  - Removed the `데이터셋 저장 폴더(고급)` Expander from `WpfClassCatalogPanel.xaml`.
  - Removed output-root UI proxies from `WpfClassCatalogPanel.xaml.cs`, `WpfLabelingShellWindow.PanelAccessors.cs`, and panel name registration.
  - Removed the class ViewModel's output-root browse/save command exposure because no class-tab control invokes it now.
  - Updated class-panel guide text to route storage-folder decisions to the dataset home/create-open flow.
- Kept MVVM boundaries:
  - Class name/color/list presentation remains in `WpfClassCatalogPanelViewModel`.
  - `WpfClassCatalogPanel.xaml` only binds class schema UI.
  - Shell code-behind still owns legacy output-root persistence helpers, but they are no longer exposed through the class tab.
- Captured comparison:
  - 1920 before: `tests\artifacts\ui\wpf-class-catalog-schema-before-1920.png`.
  - 1920 after: `tests\artifacts\ui\wpf-class-catalog-schema-after-1920.png`.
  - 1366 before: `tests\artifacts\ui\wpf-class-catalog-schema-before-1366.png`.
  - 1366 after: `tests\artifacts\ui\wpf-class-catalog-schema-after-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-class-catalog-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab classes --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-class-catalog-schema-after-1920.png` produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab classes --width 1366 --height 768 --output .\tests\artifacts\ui\wpf-class-catalog-schema-after-1366.png` produced the after capture.
- Next planned work: verify and guard the 1366x768 responsive layout where the right review/settings panel was reported as at risk, then continue the 1920x1080 first-run-to-training sweep.

## 2026-07-01 1366x768 responsive layout regression guard

- UX finding from the current 1366x768 sweep: the previously documented right-panel risk did not reproduce in the current shell layout for `클래스`, `AI 후보`, `YOLO`, or `학습` views. The right `라벨/후보 검토` panel stayed visible, while the 1920x1080 equipment baseline remains the primary target.
- Changed the test harness only:
  - Added `--wpf-responsive-layout`.
  - The smoke opens the WPF shell at the requested size, loads the visual-smoke image, visits `objects,candidates,guide,classes,yolo,training`, and asserts the review/settings panel remains inside the window with usable width.
  - This is a regression guard, not a Viewer/OpenGL/ROI behavior change.
- Captured current 1366 comparison evidence:
  - `tests\artifacts\ui\wpf-responsive-classes-before-1366.png`.
  - `tests\artifacts\ui\wpf-responsive-candidates-current-1366.png`.
  - `tests\artifacts\ui\wpf-responsive-yolo-current-1366.png`.
  - `tests\artifacts\ui\wpf-responsive-training-current-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -- --wpf-visual-smoke --review-tab classes --width 1366 --height 768 --output tests\artifacts\ui\wpf-responsive-classes-before-1366.png` produced the current class capture.
  - `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -- --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output tests\artifacts\ui\wpf-responsive-candidates-current-1366.png` produced the current candidate capture.
  - `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -- --wpf-visual-smoke --review-tab yolo --width 1366 --height 768 --output tests\artifacts\ui\wpf-responsive-yolo-current-1366.png` produced the current YOLO capture.
  - `dotnet run --no-build --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -- --wpf-visual-smoke --review-tab training --width 1366 --height 768 --output tests\artifacts\ui\wpf-responsive-training-current-1366.png` produced the current training capture.
- Next planned work: resume the first-run-to-training sweep at 1920x1080 and check whether training/model terminology can be understood without the bottom log.

## 2026-07-01 training readiness wording UX pass

- UX finding from the 1366/1920 training screen sweep: when `data.yaml` did not match the current class list, the training readiness text showed the validator's raw English message (`class count does not match...`). This still forced the user to ask what the failure meant.
- Changed the WPF presentation layer:
  - Added `WpfTrainingReadinessPresentationService`.
  - The service maps validator errors to operator-facing cause/action text while preserving train/valid/test/object/class counts.
  - `RefreshTrainingReadinessPanel` now sends the readiness report through the presentation service before updating the TrainingSettings ViewModel text.
  - The validator and YOLO data contracts remain unchanged.
- Captured comparison evidence:
  - Before/current technical wording: `tests\artifacts\ui\wpf-responsive-training-current-1366.png`.
  - 1366 after: `tests\artifacts\ui\wpf-training-readiness-friendly-after-1366.png`.
  - 1920 after: `tests\artifacts\ui\wpf-training-readiness-friendly-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-training-readiness-presentation` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab training --width 1366 --height 768 --output tests\artifacts\ui\wpf-training-readiness-friendly-after-1366.png` produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab training --width 1920 --height 1080 --output tests\artifacts\ui\wpf-training-readiness-friendly-after-1920.png` produced the after capture.
- Next planned work: audit model confirmation terminology so the current inspection model, newly trained candidate, and final confirmation action are visually distinct without reading the bottom log.

## 2026-07-01 top inference model visibility UX pass

- UX finding from the model-confirmation sweep: the YOLO tab could show the current inspection model, but the always-visible top inference status still read like a generic state (`대기`) and did not continuously answer "which model am I inspecting with?"
- Changed the WPF presentation layer:
  - Added `WpfInferenceStatusPresentationService`.
  - The top inference status now appends `검사 모델 best.pt` or `모델 후보 ...` to the current status text.
  - The tooltip keeps the full model path.
  - `RefreshYoloStatus` refreshes the top inference card on startup and model path changes.
- Captured comparison evidence:
  - Before audit: `tests\artifacts\ui\wpf-model-confirmation-audit-before-1920.png`.
  - After: `tests\artifacts\ui\wpf-inference-status-model-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-inference-status-presentation` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-training-readiness-presentation` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output tests\artifacts\ui\wpf-inference-status-model-after-1920.png` produced the after capture.
- Next planned work: audit model confirmation button wording and disabled states so users know when a candidate model is selected, saved to recipe, and used by the next inference run.

## 2026-07-01 model-center save wording UX pass

- UX finding from the post-training/model-confirmation sweep and the official-tool benchmark refresh: model-assisted labeling tools separate model output, human review, and final adoption. Our model center still used `검사 모델로 확정`, which did not clearly say whether the model was merely selected, saved to the recipe, or used by the next inference run.
- Changed the WPF presentation wording:
  - The pending trained-model action now shows `검사 모델로 저장`.
  - The next-action text says the selected model is saved to `recipe` and used from the next inference run.
  - Candidate-review guidance now uses the same `검사 모델로 저장` wording so the candidate-review panel and model center do not disagree.
  - The first-run training checklist now says the new `best.pt` is saved as the inspection model after training.
- Additional UX finding from the 1920x1080 model-center capture: the model center showed the staged `exp\best.pt` candidate, but the always-visible top inference card still showed the old inspection model. This made it look like two different models were active.
- Additional UX finding from the same capture: `학습 지표 없음(results.csv 없음)` appeared inside the model adoption evidence and could be read as a training failure, even though the run had completed and only metric comparison data was missing.
- Added synchronization:
  - Staging a new trained-model candidate now refreshes the top inference status to `모델 후보 ...`.
  - Saving the candidate to recipe now refreshes the top inference status back to the normal inspection-model wording.
  - If a model candidate exists but the save button is disabled, the ViewModel tooltip now explains whether the user is waiting for another command or missing a recipe.
- Clarified missing metrics wording:
  - Missing `results.csv` now appears as `지표 없음: 학습 실패 아님, 후보 검증 후 저장 판단(results.csv 없음)`.
  - The model-center smoke asserts this wording contains `실패 아님` so missing metrics cannot silently regress into failure-like text.
- Kept MVVM boundaries:
  - Model-center state text remains generated in the WPF model-center presentation partial and published through `WpfLabelingShellViewModel`.
  - Candidate-review and learning-workflow wording remain in their ViewModels.
  - XAML bindings and Viewer/OpenGL/ROI paths were not changed.
- Added verification support:
  - `--wpf-yolo-training-session-smoke --model-center` now moves the completed training session into the model center and asserts the visible save-button text contains `저장`.
  - The same model-center smoke asserts the top inference status contains `모델 후보` before recipe save, so the top card and model center cannot silently diverge.
  - The model-center visual capture can now be generated at the 1920x1080 equipment baseline.
- Captured comparison evidence:
  - Before wording basis: existing model-center test state used `검사 모델로 확정` and tooltip `선택한 모델을 현재 검사 모델로 확정하고 recipe에 저장합니다.`
  - After 1920 capture: `tests\artifacts\ui\wpf-model-center-save-wording-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-inference-status-presentation` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-training-readiness-presentation` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-model-center-save-wording-after-1920.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
- Next planned work: continue the first-run-to-training sweep and check whether the workflow rail still leaves too much competing information visible while a user is labeling or reviewing candidates.

## 2026-07-01 YOLO runtime controls disclosure pass

- UX finding from the final 1920x1080 model-center capture: after training, the right YOLO panel still showed model-center decisions, dataset readiness, and all runtime management commands at once. The runtime commands are occasional maintenance actions, while the user usually needs to inspect the candidate and save the inspection model.
- Changed the YOLO status panel layout:
  - `첫 점검`, `설치`, `테스트`, `재시작`, and `중지` now live under a collapsed `실행기 관리` expander.
  - Summary, command status, recovery, and progress remain visible so failures are not hidden.
  - The commands and enablement still come from `WpfYoloStatusPanelViewModel`; the code-behind only exposes a read-only UI adapter property for tests.
- Captured comparison evidence:
  - Before: `tests\artifacts\ui\wpf-model-center-save-wording-after-1920.png`.
  - After: `tests\artifacts\ui\wpf-model-center-runtime-controls-collapsed-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-status-panels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-model-center-runtime-controls-collapsed-after-1920.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
- Note: `--wpf-yolo-status-panel` is not a focused route; an accidental broad run stopped at the pre-existing template auto-label guide failure. The correct focused route is `--wpf-status-panels`.
- Next planned work: at the next pass, continue the same disclosure pattern for the dataset readiness card in model-center mode so users see one primary decision at a time.

## 2026-07-01 model-center dataset readiness disclosure pass

- Rechecked the direction against official labeling-tool docs:
  - Label Studio keeps project/data setup, labeling configuration, and predictions/pre-annotations as separate concepts.
  - Roboflow separates annotation assistance, training, and model deployment/adoption decisions.
  - CVAT separates task setup/specification from annotation workspace actions.
  - Labelbox treats ontology/class schema as a first-class setup concept before editor work.
- UX finding from the 1920x1080 model-center sweep: after a trained candidate exists, the user should mainly decide whether to review the candidate and save it as the inspection model. The always-visible dataset readiness card repeated setup information and competed with that decision.
- Changed the YOLO tab layout:
  - `YoloDatasetReadinessQuickPanel` is now a collapsed `Expander` named `데이터셋 점검 상세`.
  - The existing readiness text and `점검` command stay available inside the expander.
  - The model-center card remains the visible first decision area, with `후보 검증` and `검사 모델로 저장` ahead of repeated setup checks.
- Kept MVVM boundaries:
  - `RefreshReadinessCommand`, readiness text, and colors still come from `WpfTrainingSettingsPanelViewModel`.
  - The shell XAML only changes visual disclosure; no Viewer/OpenGL/ROI/brush/eraser path was touched.
- Captured comparison evidence:
  - Before: `tests\artifacts\ui\wpf-model-center-runtime-controls-collapsed-after-1920.png`.
  - After: `tests\artifacts\ui\wpf-model-center-dataset-readiness-collapsed-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-model-center-dataset-readiness-collapsed-after-1920.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
- Next planned work: audit whether the YOLO model path/settings panel should become a compact `current model + change` card, with advanced path fields behind disclosure, so the post-training model-center remains one-decision-at-a-time.

## 2026-07-01 YOLO inspection-model picker disclosure pass

- UX finding from the 1920x1080 YOLO model-settings sweep: the panel summary said the inspection model can be changed, but the actual `.pt` picker was inside the advanced path grid together with Python executable, project root, client script, image root, and tuning values. A user who only wants to confirm or replace the active inspection model still had to scan runtime environment fields.
- Changed the YOLO model settings panel:
  - Added `YoloInspectionModelQuickPanel` directly below the current-model summary.
  - Moved `YoloWeightsPathBox` and `BrowseYoloWeightsButton` into that visible panel.
  - Kept `저장` and `기본값` directly below the visible model picker.
  - Renamed the advanced header to `실행 환경 상세` and left Python/project/script/image root/confidence/timeout/image-size/max-candidate fields there.
- Kept MVVM boundaries:
  - Existing `WeightsPath`, `BrowseWeightsCommand`, `SaveSettingsCommand`, and enablement bindings remain on `WpfYoloModelSettingsPanelViewModel`.
  - Shell panel wiring only gained the same name-registration proxy pattern used by the existing YOLO model controls.
  - No Viewer/OpenGL/ROI/brush/eraser path was touched.
- Captured comparison evidence:
  - Before: `tests\artifacts\ui\wpf-sweep-yolo-model-1920.png`.
  - After: `tests\artifacts\ui\wpf-yolo-model-settings-compact-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab model --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-yolo-model-settings-compact-after-1920.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
- Next planned work: stop treating the right side as one always-visible tab stack. Design and start a mode-based shell layout where `데이터셋 홈`, `라벨링 워크벤치`, `추론 검토`, `학습/모델 센터`, and `환경/고급 설정` swap the active right-side view according to the workflow stage.

## 2026-07-01 workflow-stage scoped right-panel pass

- UX finding from the mode-structure audit: the right panel still behaved like one large tab stack. This made saved human labels, AI inference candidates, guide/tools, class schema, and YOLO/model settings look like sibling tasks even though they belong to different workflow stages.
- Changed the first structural pass:
  - `WpfLabelingShellViewModel` now owns right-side view visibility flags for saved labels, AI candidates, guide/tools, class catalog, and YOLO/model center.
  - `SetWorkflowStage(...)` now applies the right-side visibility map:
    - dataset: guide/tools and class catalog
    - labeling: saved labels, guide/tools, and class catalog
    - inference: AI candidates only
    - training/model: YOLO/model center only
  - `WpfLabelingShellWindow.xaml` binds the right tabs' `Visibility` to those ViewModel flags and binds the right-panel title to the active workflow stage title.
  - Existing code-behind focus adapters now set the workflow stage before selecting a tab, so hidden tabs are not selected directly.
- Kept MVVM boundaries:
  - Workflow state and view visibility live in `WpfLabelingShellViewModel`.
  - Shell partials only act as UI adapters for focus/navigation into an already-selected workflow.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before model-center tab stack: `tests\artifacts\ui\wpf-yolo-model-settings-compact-after-1920.png`.
  - After model-center scoped panel: `tests\artifacts\ui\wpf-mode-scoped-right-panel-yolo-after-1920.png`.
  - After inference scoped panel: `tests\artifacts\ui\wpf-mode-scoped-right-panel-candidates-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-mode-scoped-right-panel-yolo-after-1920.png` passed and produced the model-center capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-mode-scoped-right-panel-candidates-after-1920.png` passed and produced the inference capture.
- Next planned work: replace the right `TabControl` with explicit workflow-stage view hosts once the visibility pass is stable, so the UI reads as real modes rather than a tab control with hidden groups.

## 2026-07-01 right workflow view-host disclosure pass

- UX finding from the mode-scoped right-panel capture: inference review and model center each had only one relevant right-side view, but still showed a single tab header (`AI 후보` or `YOLO`). That looked like a leftover tab stack rather than a focused workflow mode.
- Changed the next structural step:
  - `WpfLabelingShellViewModel` now exposes `IsRightWorkflowSubNavigationVisible`.
  - The right-panel subnavigation stays visible only when the active workflow stage has more than one relevant right-side view.
  - Inference review and model center collapse the single tab header and read as direct mode surfaces.
  - Labeling and dataset stages keep subnavigation because saved labels, guide/tools, and class schema are still distinct related tasks.
  - All direct right-tab selection calls were routed through shell adapter methods (`ShowSavedLabelsWorkflowView`, `ShowCandidateReviewWorkflowView`, `ShowGuideToolsWorkflowView`, `ShowClassCatalogWorkflowView`, `ShowYoloModelCenterWorkflowView`) so the next ViewHost migration has one switching path.
- Kept MVVM boundaries:
  - Stage and subnavigation visibility state live in `WpfLabelingShellViewModel`.
  - The shell methods are WPF UI adapters only; they select the current view and call the ViewModel stage state.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before model-center scoped panel with single `YOLO` tab: `tests\artifacts\ui\wpf-mode-scoped-right-panel-yolo-after-1920.png`.
  - After model-center view-host surface: `tests\artifacts\ui\wpf-right-viewhost-yolo-after-1920.png`.
  - After inference view-host surface: `tests\artifacts\ui\wpf-right-viewhost-candidates-after-1920.png`.
  - After labeling multi-view subnavigation retained: `tests\artifacts\ui\wpf-right-viewhost-labeling-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-right-viewhost-yolo-after-1920.png` passed and produced the model-center capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-right-viewhost-candidates-after-1920.png` passed and produced the inference capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab objects --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-right-viewhost-labeling-after-1920.png` passed and produced the labeling capture.
- Next planned work: simplify the labeling-stage right panel itself. Keep saved-label review as the primary surface, but convert guide/tools and class schema access into compact local actions or disclosure so the labeling stage does not feel like another mini-tab product.

## 2026-07-01 labeling right-panel compact shortcuts pass

- UX finding from the labeling 1920x1080 capture: the right panel still showed `저장 라벨 / 가이드·도구 / 클래스` as full tab headers. That kept the labeling stage looking like a nested tab product instead of a primary saved-label review surface with occasional helper access.
- Changed the labeling-stage right panel:
  - Saved-label review remains the primary right-side surface.
  - The full labeling tab strip collapses.
  - The right header now shows compact shortcut buttons for `라벨`, `도구`, and `클래스` only during the labeling stage.
  - The shortcut buttons are bound to `WpfLabelingShellViewModel` commands: `ShowSavedLabelsViewCommand`, `ShowLabelingGuideViewCommand`, and `ShowClassCatalogViewCommand`.
  - The shell still handles the actual WPF view switch through adapter methods, keeping the WPF control selection out of the ViewModel.
- Kept MVVM boundaries:
  - Visibility and commands live in `WpfLabelingShellViewModel`.
  - `WpfLabelingShellWindow` only wires ViewModel commands to existing right-view adapter methods.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before labeling tab strip: `tests\artifacts\ui\wpf-right-viewhost-labeling-after-1920.png`.
  - After compact labeling shortcuts: `tests\artifacts\ui\wpf-labeling-right-shortcuts-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab objects --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-labeling-right-shortcuts-after-1920.png` passed and produced the after capture.
- Next planned work: add active-state feedback to the compact labeling shortcuts and review whether the shortcut bar should remain visible when the guide/classes subview is active.

## 2026-07-01 labeling shortcut active-state pass

- UX finding from the compact shortcut capture: the `라벨 / 도구 / 클래스` buttons made the right panel smaller, but users still needed a clear visual cue for which subview was active.
- Changed the active-state behavior:
  - `WpfLabelingShellViewModel` now exposes `IsSavedLabelsShortcutActive`, `IsLabelingGuideShortcutActive`, and `IsClassCatalogShortcutActive`.
  - Right workflow shortcut buttons bind their `Tag` to the active-state properties.
  - `RightWorkflowShortcutButtonStyle` highlights the active shortcut with the app accent color and stronger text weight.
  - Shell right-view adapter methods update the ViewModel active shortcut whenever they switch to labels, tools, or classes.
  - Visual smoke helper routing now also sets the active shortcut state for label/class captures.
- Kept MVVM boundaries:
  - Active-state flags live in `WpfLabelingShellViewModel`.
  - XAML only binds to command and active-state properties.
  - Shell code-behind remains a UI adapter for selecting the hosted WPF view.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before active-state pass: `tests\artifacts\ui\wpf-labeling-right-shortcuts-after-1920.png`.
  - After saved-label active state: `tests\artifacts\ui\wpf-labeling-right-shortcuts-active-after-1920.png`.
  - After class active state: `tests\artifacts\ui\wpf-labeling-right-shortcuts-class-active-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab objects --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-labeling-right-shortcuts-active-after-1920.png` passed and produced the saved-label active capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab classes --width 1920 --height 1080 --output .\tests\artifacts\ui\wpf-labeling-right-shortcuts-class-active-after-1920.png` passed and produced the class active capture.
- Next planned work: audit first-time labeling when no class exists. If a user enters labeling before class setup, the UI should guide them to class creation without making the main labeling panel feel blocked.

## 2026-07-01 canvas active-label class management pass

- UX finding from the first-time labeling audit: the app safely falls back to a default `Defect` class in several save paths, but the canvas did not give the user a direct way to understand or change the class that will be applied to the next drawn label.
- Changed the canvas class affordance:
  - `WpfCanvasPanelViewModel` now exposes `OpenClassCatalogCommand`, `ActiveLabelClassActionText`, `ActiveLabelClassActionToolTip`, and `IsLabelClassSetupMissing`.
  - The active-label class card now includes a compact `클래스 관리` action that opens the right-side class schema panel.
  - When the class list is empty, the same card changes to a class-registration prompt instead of silently reading like a normal drawing state.
  - The shell injects the class-panel navigation action through `ConfigureLabelClassSelection(...)`, keeping the WPF button free of click handlers.
- Kept MVVM boundaries:
  - The action command and presentation state live in `WpfCanvasPanelViewModel`.
  - `WpfCanvasPanel.xaml` only declares bindings.
  - `WpfLabelingShellWindow.PanelWiring.Canvas.cs` remains the shell adapter that connects the command to the existing class-view switch.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before class action: `tests\artifacts\ui\wpf-labeling-right-shortcuts-active-after-1920.png`.
  - After canvas class action: `tests\artifacts\ui\wpf-canvas-class-management-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --width 1920 --height 1080 --review-tab objects --output .\tests\artifacts\ui\wpf-canvas-class-management-after-1920.png` passed and produced the after capture.
- Next planned work: review the dataset-home to labeling transition and make sure a new user can see whether they are using an existing recipe class list, a copied dataset, or a fresh class schema before they start drawing.

## 2026-07-01 dataset source-context header pass

- UX finding from the dataset-home to labeling transition audit: the header showed the dataset storage folder and image folder, but it did not explicitly say that classes come from the current Recipe while labels are loaded from and saved to the dataset storage folder. This was the same confusion behind the earlier "image folder changed but labels remained" reports.
- Changed the dataset context header:
  - Added `CurrentDatasetSourceText` to `WpfLabelingShellViewModel`.
  - `WpfDatasetContextPresentationService` now builds a visible source summary such as `클래스: 레시피 1개 / 라벨: 저장 폴더 기준`.
  - Added a new `작업 기준` card to the top dataset context bar next to storage folder and image folder.
  - The dataset tooltip now explains the rule directly: class list is saved in the Recipe, label files are read/written in `data/*/labels`, and changing the image folder only changes the source image list.
- Kept MVVM boundaries:
  - Source wording lives in `WpfDatasetContextPresentationService`.
  - Shell ViewModel exposes state only through bindable properties.
  - Shell code-behind only computes the current recipe class count and passes it into `SetDatasetContext(...)`.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before source context card: `tests\artifacts\ui\wpf-canvas-class-management-after-1920.png`.
  - After source context card: `tests\artifacts\ui\wpf-dataset-source-context-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --width 1920 --height 1080 --review-tab objects --output .\tests\artifacts\ui\wpf-dataset-source-context-after-1920.png` passed and produced the after capture.
- Next planned work: audit the dataset selection window and creation wizard text so the same storage/image/class-source rule is visible before the user opens the workbench, not only after selection.

## 2026-07-01 dataset selection and creation source-rule pass

- UX finding from the dataset selector and creation wizard audit: the workbench header now explains storage/image/class source rules, but the user could still reach the workbench without seeing that labels are keyed by the dataset storage folder while the image folder is only the source image list.
- Changed the dataset selection window:
  - Replaced broken static Korean guide strings with ViewModel-backed guide text.
  - Added a top `작업 기준은 저장 폴더입니다` rule card before the dataset list.
  - Kept the two first decisions explicit: open an existing dataset and load its labels/history, or create a new storage folder so labels do not carry over even when the same image folder is reused.
  - Increased the default selector size to `900x720` for the 1920x1080 equipment baseline.
- Changed the dataset creation wizard:
  - Added a source-rule card explaining the distinct roles of storage folder and image folder before the purpose/sample/class fields.
  - Changed the summary and storage help text to state that the storage folder is the label/Recipe/training baseline.
  - Increased the default wizard size to `900x820` so storage path and preview are visible without feeling cramped on the target equipment resolution.
- Kept MVVM boundaries:
  - Guide text lives in `WpfDatasetSelectionWindowViewModel` and `WpfDatasetSetupWizardViewModel`.
  - XAML only binds to ViewModel properties and commands.
  - Dataset creation/opening workflow remains in the existing shell adapter and request DTO path.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before pre-workbench source-rule pass: `tests\artifacts\ui\wpf-dataset-source-context-after-1920.png`.
  - After dataset selector source-rule pass: `tests\artifacts\ui\wpf-dataset-selection-source-rule-after.png`.
  - After dataset wizard source-rule pass: `tests\artifacts\ui\wpf-dataset-wizard-source-rule-after.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-setup-ui` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-setup-request` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-selection-smoke --output .\tests\artifacts\ui\wpf-dataset-selection-source-rule-after.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-dataset-wizard-smoke --output .\tests\artifacts\ui\wpf-dataset-wizard-source-rule-after.png` passed and produced the after capture.
- Next planned work: run a first-use workbench sweep from newly created dataset to first label save. Focus on whether the canvas, active class, save state, and image queue clearly tell the user what to do next after the dataset opens.

## 2026-07-01 first-label draw-save-next loop pass

- UX finding from the first-use workbench sweep: after a dataset opens, the canvas already showed the current step and save state, but the repeatable operator loop was implicit. A new user had to infer that the expected rhythm is draw a label, save it, then move to the next image.
- Changed the canvas first-label guidance:
  - Added `FirstLabelLoopText` to `WpfCanvasPanelViewModel`.
  - Added a compact always-visible sequence chip to the canvas workflow strip: `순서: 그리기 -> 라벨 저장 -> 다음 이미지`.
  - Tightened the canvas next-action text so rectangle/ellipse/brush flows explicitly end at `라벨 저장`.
  - Changed the empty-image guidance to start from the left image queue, matching the actual first-use screen structure.
- Kept MVVM boundaries:
  - The draw/save/next loop text lives in `WpfCanvasPanelViewModel`.
  - `WpfCanvasPanel.xaml` only binds to ViewModel state.
  - `WpfLabelingShellWindow.PanelWiring.Canvas.cs` remains a shell adapter that composes current workflow context from existing state.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before first-label loop chip: `tests\artifacts\ui\wpf-first-label-workbench-before.png`.
  - After first-label loop chip: `tests\artifacts\ui\wpf-first-label-loop-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-workflow-context` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --width 1920 --height 1080 --review-tab objects --output .\tests\artifacts\ui\wpf-first-label-loop-after-1920.png` passed and produced the after capture.
- Next planned work: continue the first-use sweep into the image queue after save. Check whether saved/unsaved/no-object queue states and the `다음` action are clear enough when moving across multiple images.

## 2026-07-01 post-save image-queue loop pass

- UX finding from the post-save queue sweep: the queue navigation logic already skipped saved, skipped, and no-object rows, but the primary queue button still read like a generic `다음` action. A new user could not tell whether it opens the next file or the next image that still needs labeling/review.
- Changed the image queue guidance:
  - Added `NextUnlabeledActionText` and `NextUnlabeledToolTip` to `WpfImageQueuePanelViewModel`.
  - Changed the primary queue navigation button to bind to the ViewModel text and tooltip, with visible text `다음 미완료`.
  - The tooltip now states that `저장됨` and `객체없음` images are skipped and the next label-needed image is opened.
  - Updated `WpfImageQueuePresenter.BuildStatusSummary(...)` so confirmed, no-candidate, and skipped rows read as completed work instead of generic saved-label/AI status.
- Kept MVVM boundaries:
  - Queue action wording lives in `WpfImageQueuePanelViewModel`.
  - Queue row summary wording lives in `WpfImageQueuePresenter`.
  - `WpfImageQueuePanel.xaml` only binds to ViewModel state.
  - Existing `TryFindNextUnlabeled(...)` skip behavior was verified and left intact.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before post-save queue wording: `tests\artifacts\ui\wpf-first-label-loop-after-1920.png`.
  - After post-save queue wording: `tests\artifacts\ui\wpf-post-save-queue-loop-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-image-queue-status` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --width 1920 --height 1080 --review-tab objects --output .\tests\artifacts\ui\wpf-post-save-queue-loop-after-1920.png` passed and produced the after capture.
- Next planned work: continue the first-use sweep into the no-object path. Verify whether a user can intentionally mark an image as object-free without drawing a box, and whether that completion state is obvious in the queue and training readiness counts.

## 2026-07-01 no-object completion path pass

- UX finding from the no-object path sweep: the app could already write an empty YOLO label file, and candidate review had a completion button for images with no accepted candidates. However, the main labeling canvas did not expose a direct "this image has no object" action, so a first-time labeler had to infer the path or enter the AI-candidate review surface.
- Changed the canvas completion flow:
  - Added a canvas toolbar `객체 없음` button beside `라벨 저장`.
  - Added `CompleteNoObjectCommand`, `IsNoObjectCompletionEnabled`, `NoObjectCompletionActionText`, and `NoObjectCompletionToolTip` to `WpfCanvasPanelViewModel`.
  - The button is enabled only when an image is open and there are no saved/manual labels and no pending AI candidates.
  - When clicked, it saves an empty YOLO label file, persists the queue row as `객체없음`, refreshes training readiness state, and moves to the next incomplete image.
  - If labels or AI candidates exist, the disabled tooltip explains what must be resolved first, preventing accidental overwrite of real labels.
- Fixed queue status persistence:
  - `YoloImageReviewStatusService.RefreshLabelStatusAndReviewState(...)` now treats an existing empty label file as `NoCandidate` instead of dropping it back to unreviewed.
  - Added `MarkActiveImageNoCandidate()` so both candidate-review completion and canvas no-object completion share the same status refresh/save path.
- Kept MVVM boundaries:
  - Button text, enablement, and tooltip live in `WpfCanvasPanelViewModel`.
  - `WpfCanvasPanel.xaml` only binds to ViewModel command/state.
  - Shell code-behind remains the command adapter that calls existing persistence services and queue status services.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before no-object canvas command: `tests\artifacts\ui\wpf-post-save-queue-loop-after-1920.png`.
  - After no-object canvas command: `tests\artifacts\ui\wpf-no-object-completion-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --yolo-image-review-status` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-canvas-panel-commands` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-image-queue-status` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --width 1920 --height 1080 --review-tab objects --output .\tests\artifacts\ui\wpf-no-object-completion-after-1920.png` passed and produced the after capture.
- Next planned work: continue the first-use sweep from labeling completion into training readiness and model-center confirmation. Focus on whether the user can tell when the dataset is trainable, what still blocks training, and which model will be used after training.

## 2026-07-01 training/model lifecycle summary pass

- UX finding from the training/model sweep: the model-center already separated current inspection model, trained candidate, adoption decision, and recipe save action, but the learning guide did not show the same lifecycle state in its first-visible training area. A user could still miss whether `best.pt` was only a candidate or already the model used for inspection.
- Changed the training/model guide:
  - Added `TrainingModelLifecycleCurrentText`, `TrainingModelLifecycleCandidateText`, `TrainingModelLifecycleDecisionText`, and `TrainingModelLifecycleNextActionText` to `WpfLearningWorkflowPanelViewModel`.
  - Added `SetTrainingModelLifecycleState(...)` so model-center state is stripped into short guide-friendly values without duplicating labels.
  - Added a `모델 확인` summary card to `WpfLearningWorkflowPanel.xaml` after dataset readiness and before the next-action button.
  - `RefreshModelCenterDashboard(...)` now fans out the same current/candidate/adoption/next-action state to both `ShellViewModel` and `LearningWorkflowViewModel`.
- Kept MVVM boundaries:
  - Model lifecycle display text is owned by `WpfLearningWorkflowPanelViewModel`.
  - `WpfLearningWorkflowPanel.xaml` only binds to ViewModel state.
  - `WpfLabelingShellWindow.ModelCenterDashboard.cs` remains the shell adapter that shares already-computed model-center state.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before training/model lifecycle summary: `tests\artifacts\ui\wpf-no-object-completion-after-1920.png`.
  - After model-center confirmation view: `tests\artifacts\ui\wpf-model-lifecycle-after-1920.png`.
  - After training settings view: `tests\artifacts\ui\wpf-model-lifecycle-training-after-1920.png`.
  - After guide-stage smoke capture: `tests\artifacts\ui\wpf-model-lifecycle-guide-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-learning-workflow-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output C:\Git\Labelling_Application\tests\artifacts\ui\wpf-model-lifecycle-after-1920.png` passed and produced the model-center capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --width 1920 --height 1080 --output C:\Git\Labelling_Application\tests\artifacts\ui\wpf-model-lifecycle-training-after-1920.png` passed and produced the training view capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --width 1920 --height 1080 --review-tab guide --output C:\Git\Labelling_Application\tests\artifacts\ui\wpf-model-lifecycle-guide-after-1920.png` passed and produced the guide-stage capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
- Verification note:
  - An accidental run with non-existent flags `--wpf-learning-workflow` and `--wpf-model-center-dashboard` fell through to the broader smoke suite and stopped at the existing template batch case `Template auto label shows actionable guide: registered template batch should complete`. The focused tests above cover this change and passed.
- Next planned work: continue the post-training UX sweep by making the model candidate review/save action easier to reach from the top workflow rail and training settings panel, then verify the same flow at 1366x768.

## 2026-07-01 post-training action reachability pass

- UX finding from the post-training confirmation sweep: the model-center had the correct candidate review and recipe-save actions, but the user could still miss them after seeing the training-complete state in the top workflow rail or training settings panel.
- Changed the post-training action reachability:
  - Added compact `후보 검증` and `검사 모델로 저장` actions to the top workflow summary when the active stage is `학습/모델 센터`.
  - Added a `학습 완료 후 작업` card to the training settings panel, bound to the same trained-candidate/current-model/adoption state as the model center.
  - Moved that card next to the training progress result so it stays visible after completion instead of being hidden near the top of the settings scroll.
  - The top workflow actions share `ShellViewModel.ReviewCandidateModelCommand` and `YoloModelSettingsViewModel.SaveSettingsCommand`; the training settings card receives the existing shell adapter commands through `WpfTrainingSettingsPanelViewModel.ConfigureCommands(...)`.
- Kept MVVM boundaries:
  - Button text, enablement, and tooltip state for the training settings card live in `WpfTrainingSettingsPanelViewModel`.
  - `WpfTrainingSettingsPanel.xaml` and `WpfLabelingShellWindow.xaml` only bind to ViewModel state and commands.
  - `WpfLabelingShellWindow.ModelCenterDashboard.cs` remains the adapter that computes model-center state once and fans it out to shell, learning workflow, and training settings ViewModels.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before: `tests\artifacts\ui\wpf-model-lifecycle-after-1920.png` and `tests\artifacts\ui\wpf-model-lifecycle-training-after-1920.png`.
  - After model-center at equipment baseline: `artifacts\ui\wpf-post-training-actions-after-1920.png`.
  - After model-center at small-width guard: `artifacts\ui\wpf-post-training-actions-after-1366.png`.
  - After training settings at small-width guard: `artifacts\ui\wpf-post-training-settings-after-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-training-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-post-training-actions-after-1920.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-post-training-actions-after-1366.png` passed and produced the after capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --width 1366 --height 768 --output .\artifacts\ui\wpf-post-training-settings-after-1366.png` passed and produced the after capture.
- Verification note:
  - A broader accidental run with `--wpf-shell-mvvm` fell through to the full suite and stopped at the existing template case `Template auto label shows actionable guide: registered template batch should complete`. The focused tests above cover this change and passed.
- Next planned work: continue the post-training sweep into the actual candidate-review comparison screen. Verify whether a user can judge "new trained candidate vs current inspection model" from validation examples and then confidently save or reject the candidate without reading the bottom log.

## 2026-07-01 model-profile terminology pass

- UX finding from the broader product self-evaluation: the application is not intended to be YOLO-only. Object-detection labels should be reusable across multiple model adapters, and post-training inspection should distinguish dataset labels, trained model candidates, current inspection model, and runtime profile.
- Changed the first user-facing terminology pass:
  - Model settings summary now reads as the current `모델 프로필` and selected `검사 모델`, while Python/project/script paths stay under `모델 실행 환경 상세`.
  - Main header and tools menu now describe the third work surface as `모델`, not `YOLO`.
  - Runtime status and recovery messages now use `모델 실행 환경`, `모델 실행기`, and `모델 테스트` instead of `YOLO 설정`, `YOLO 탭`, and `YOLO 테스트`.
  - Dataset creation and learning guide copy now describe object detection as a `박스 라벨 데이터셋` that can be reused by multiple object-detection models.
  - First-run and training guide steps now say `박스 라벨 파일`, `모델 학습`, and `학습 결과 후보` instead of `YOLO txt`, `YOLO 학습`, and `best.pt 후보`.
  - Empty-object completion now reports `빈 라벨 파일` instead of `빈 YOLO 라벨`.
- Kept MVVM boundaries:
  - User-facing label/status text remains in ViewModels where possible.
  - Shell partial classes only adapt existing Python/worker command state into ViewModel status text.
  - Internal YOLO adapter type names, protocol names, save-model classes, and training implementation names were left intact for this first pass.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before model-profile terminology pass: `artifacts\ui\wpf-model-profile-before-1366.png`.
  - After model-profile terminology pass: `artifacts\ui\wpf-model-profile-after-1366.png`.
  - Equipment-baseline after capture: `artifacts\ui\wpf-model-profile-after-1920.png`.
- Verification target:
  - Build and focused WPF/MVVM smoke tests cover the changed ViewModel text, status defaults, and responsive model settings surface.
- Next planned work: formalize the model domain as first-class product concepts (`ModelProfile`, `TrainingRun`, `ModelCandidate`, and inspection-model adoption history) so multiple model adapters can be registered and compared without relying on YOLO-named shell surfaces.

## 2026-07-01 model-registry presentation pass

- UX finding from the first-class model concept sweep: the model center had current/candidate/adoption fields, but the user still had to infer the product-level relationship between model profile, training run, trained candidate, and current inspection model. That is the exact confusion that appeared after training: "was training complete, and which model am I inspecting with?"
- Changed the first model-registry presentation pass:
  - Added `WpfModelRegistryPresentationService` to combine existing `PythonModelSettings`, training weight comparison, and training history into one model-registry summary without changing the persisted recipe schema yet.
  - Added `ModelRegistryProfileText`, `ModelRegistryTrainingRunText`, `ModelRegistryCandidateModelText`, `ModelRegistryInspectionModelText`, and `ModelRegistryActionText` to `WpfLabelingShellViewModel`.
  - Added a `모델 레지스트리` summary section near the top of the model center so the user can see:
    - model profile / execution adapter,
    - latest training run state,
    - latest trained candidate model and metric context,
    - current inspection model saved in the recipe,
    - next action when a trained candidate must be saved as the inspection model.
  - Updated the training/model workflow stage detail so it explicitly mentions `best.pt` candidate review versus the current inspection model.
- Kept MVVM boundaries:
  - Model-registry text is produced by a service and exposed by the shell ViewModel.
  - XAML only binds to ViewModel state.
  - `WpfLabelingShellWindow.ModelCenterDashboard.cs` remains the adapter that reads existing training/model state and updates the ViewModel.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before model-registry section: `artifacts\ui\wpf-model-profile-before-1366.png`.
  - After model-registry section at small-width guard: `artifacts\ui\wpf-model-registry-training-after-1366.png`.
  - After model-registry section at equipment baseline: `artifacts\ui\wpf-model-registry-training-after-1920.png`.
  - Additional general model-tab captures: `artifacts\ui\wpf-model-registry-after-1366.png`, `artifacts\ui\wpf-model-registry-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --width 1366 --height 768 --output .\artifacts\ui\wpf-model-registry-after-1366.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-registry-after-1920.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-registry-training-after-1366.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-registry-training-after-1920.png` passed and produced the capture.
- Next planned work: persist the model registry/history concept instead of only presenting it. The next pass should introduce durable model profile/training run/candidate/adoption records that can later support YOLO, ONNX, segmentation, anomaly detection, and side-by-side model comparison.

## 2026-07-01 model-registry persistence pass

- UX/product finding from the multi-model self-evaluation: showing the model registry was useful, but it was still derived at runtime from YOLO settings and training history. For a product that compares multiple model adapters, the model profile, training run, candidate, and inspection-model adoption must be durable recipe data, not only UI text.
- Changed the recipe-backed model registry:
  - Added `LabelingProjectSettings.ModelRegistry`.
  - Added XML-serializable records: `ModelProfile`, `TrainingRun`, `ModelCandidate`, and `InspectionModelAdoption`.
  - Added `ModelRegistryService` to upsert model profiles, staged training candidates, current inspection models, and adoption history.
  - Connected the existing trained-weight candidate path so `UpdateAppliedTrainingWeightsHistory(... savedToRecipe:false)` records a staged candidate, and `savedToRecipe:true` records an inspection-model adoption.
  - Extended `WpfModelRegistryPresentationService` so the model center now reads persisted registry counts/state in addition to live training comparison data.
- Kept MVVM boundaries:
  - Persistent model records are core project settings.
  - Registry mutation is handled by `ModelRegistryService`.
  - The WPF shell only adapts existing training-weight events into the service call.
  - XAML remains bound to `ShellViewModel` text.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured comparison evidence:
  - Before persisted registry: `artifacts\ui\wpf-model-registry-training-after-1366.png`.
  - After persisted registry at small-width guard: `artifacts\ui\wpf-model-registry-persisted-after-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-registry-persisted-after-1366.png` passed and produced the capture.
- Next planned work: build the actual model-candidate comparison/adoption screen on top of the persisted registry. The user should be able to open a candidate, see validation examples and metrics, then explicitly save or reject the candidate with the registry adoption result visible.

## 2026-07-01 model-candidate decision pass

- UX/product finding from the model-candidate comparison pass: the app could stage a trained `best.pt` candidate and save it as the inspection model, but the user's final decision was still too implicit. There was no local save/reject decision surface in Candidate Review, and rejected candidates were not persisted as model history.
- Changed the registry-backed candidate decision flow:
  - Added XML-serializable `ModelCandidateDecision` records and candidate-level `Decision`, `DecisionUtc`, and `DecisionSummary` fields.
  - Extended `ModelRegistryService` with pending/adopted/rejected candidate decisions, latest decision lookup, and decision-history trimming.
  - Kept adoption history for saved inspection models, while adding separate saved/rejected decision history for model candidates.
  - Added a Candidate Review `후보 결정` card with `검사 모델로 저장` and `후보 거절` commands.
  - Wired save through the existing model-settings save path and reject through a new shell adapter that records the rejection, restores the baseline inspection model path, and persists the recipe when possible.
  - Updated the model registry summary so model-center history now includes decision-history count and latest candidate decision context.
- Kept MVVM boundaries:
  - Candidate decision status, enabled state, tooltips, and commands live on `WpfCandidateReviewPanelViewModel`.
  - XAML binds to ViewModel state only.
  - Shell partial code remains the adapter between current training-weight state and core `ModelRegistryService`.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Candidate Review decision card at equipment baseline: `artifacts\ui\wpf-candidate-model-decision-1920.png`.
  - Candidate Review visual-smoke capture at smaller requested size: `artifacts\ui\wpf-candidate-model-decision-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-candidate-review-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-candidate-review-layout` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output .\artifacts\ui\wpf-candidate-model-decision-1920.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output .\artifacts\ui\wpf-candidate-model-decision-1366.png` passed and produced the capture.
- Next planned work: move from a single latest-candidate decision card to a model history/comparison list that can show multiple training runs and multiple model adapters side by side. That is the next requirement for YOLO/ONNX/segmentation/anomaly model comparison.

## 2026-07-01 model-history comparison list pass

- UX/product finding from the latest-candidate decision pass: the user can now save or reject the current candidate, but a model-centered product also needs visible model history. Otherwise the user still cannot compare prior training runs or understand how the current inspection model relates to older candidates.
- Changed the model-center history view:
  - Added `WpfModelRegistryHistoryItem` rows to `WpfModelRegistryPresentationService`.
  - Built recent model-history rows from persisted `ModelCandidate`, `TrainingRun`, `ModelProfile`, and `ModelCandidateDecision` records.
  - Added `ModelRegistryHistoryItems`, header text, summary text, and visibility state to `WpfLabelingShellViewModel`.
  - Added a `최근 모델 이력` list inside the model registry summary. Each row shows whether it is the current inspection model or a trained candidate, the model file, profile/run context, metric summary, and decision state.
  - Kept the first pass display read-only. It is a comparison/history surface, not yet a row-click adoption workflow.
- Kept MVVM boundaries:
  - History row formatting is owned by `WpfModelRegistryPresentationService`.
  - The shell ViewModel exposes collection state for XAML binding.
  - XAML is presentation-only with an `ItemsControl`.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Model history list at equipment baseline: `artifacts\ui\wpf-model-history-list-1920.png`.
  - Model history list at smaller guard size: `artifacts\ui\wpf-model-history-list-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-history-list-1920.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-history-list-1366.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
- Next planned work: add row-level model-history actions. The useful next step is selecting a historical candidate to inspect its details, compare it against the current model, and intentionally promote it back to the inspection model when appropriate.

## 2026-07-01 right-workflow dock pass

- UX/product finding from the full-workbench review: the fixed three-column layout kept the right-side settings/review panel visible during labeling even when the operator mostly needs the image queue and the canvas. That made the screen read as one dense surface instead of a task-focused workbench.
- Changed the right-side workflow layout:
  - Added ViewModel-owned dock state to `WpfLabelingShellViewModel`.
  - The dataset, inference review, and training/model stages keep the right workflow panel expanded because those stages need setup/review/model controls.
  - The labeling stage collapses the right workflow panel by default to a narrow rail, giving the canvas more room at the 1920x1080 equipment baseline.
  - The collapsed rail keeps direct icon buttons for open panel, saved labels, tools, classes, AI candidate review, and model center so the user does not lose access to task panels.
  - Existing right workflow tab selection remains a shell UI adapter; state and commands stay on the ViewModel.
- Kept MVVM boundaries:
  - Dock width, expanded/collapsed state, rail visibility, and toggle command live on `WpfLabelingShellViewModel`.
  - XAML binds `RightWorkflowColumn.Width` and panel visibility to ViewModel state.
  - Shell code-behind only expands the panel when an existing task adapter opens a specific right workflow tab.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Before fixed right panel: `artifacts\ui\wpf-right-dock-before-labeling-1920.png`.
  - After labeling-stage collapsed rail: `artifacts\ui\wpf-right-dock-after-labeling-collapsed-1920.png`.
  - After inference-stage expanded review panel: `artifacts\ui\wpf-right-dock-after-inference-expanded-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-right-dock-after-labeling-collapsed-1920.png` passed and produced the capture.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output .\artifacts\ui\wpf-right-dock-after-inference-expanded-1920.png` passed and produced the capture.
- Next planned work: continue task-surface separation by adding bottom log collapse/notification behavior, because normal labeling still should not require a permanent log area unless an error or detailed inspection is active.

## 2026-07-01 right-workflow dock rail label follow-up

- UX finding from the 1920x1080 labeling capture: the right workflow rail was collapsed, but icon-only buttons did not clearly read as an on-demand dock. A first-time user could see a thin icon strip without knowing it opens saved labels, tools, or class setup.
- Updated the collapsed rail from a 48px icon-only strip to a 72px icon+short-label strip.
- The rail now shows stable text labels:
  - `열기`
  - `라벨`
  - `도구`
  - `클래스`
  - `AI`
  - `모델`
- Kept the command ownership unchanged:
  - dock width and expanded/collapsed state stay on `WpfLabelingShellViewModel`;
  - rail buttons still bind to existing ShellViewModel commands;
  - no Viewer/OpenGL/ROI/brush/eraser path was changed.
- Captured before/after comparison:
  - Before: `artifacts\ui\wpf-labeling-right-dock-before-1920.png`.
  - After: `artifacts\ui\wpf-labeling-right-dock-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-labeling-right-dock-after-1920.png` produced the after capture.
- Next planned work: continue the mode-scoped workbench pass by reviewing whether the top workflow rail and right rail duplicate navigation too much during labeling. If duplication still distracts, keep cross-stage movement only in the top rail and reserve the right rail for labeling-local panels.

## 2026-07-01 right-workflow local rail cleanup

- UX finding from the labeling-mode navigation review: the collapsed right rail still exposed `AI` and `모델`, even though cross-stage movement already belongs to the top workflow rail (`추론 검토`, `학습/모델 센터`). This made the labeling workbench look like it had two competing navigation systems.
- Changed the collapsed right rail to be labeling-local only:
  - kept `열기`, `라벨`, `도구`, and `클래스`;
  - removed the collapsed-rail `AI` and `모델` buttons;
  - kept inference review and model-center movement in the top workflow rail.
- Kept MVVM boundaries:
  - right-rail commands still bind to existing `WpfLabelingShellViewModel` commands;
  - no command/workflow state was moved into code-behind;
  - no Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured before/after comparison:
  - Before: `artifacts\ui\wpf-labeling-right-dock-after-1920.png`.
  - After: `artifacts\ui\wpf-labeling-local-right-rail-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-labeling-local-right-rail-after-1920.png` produced the after capture.
- Next planned work: continue the same structure pass on the top workflow rail itself. The next check is whether top-stage labels/actions can be reduced to stage movement plus current critical actions, while detailed tools stay in the stage-specific panel or tools menu.

## 2026-07-01 top workflow rail density cleanup

- UX finding from the 1920x1080 labeling capture: after the right rail became local-only, the top workflow rail still looked heavier than necessary. Each stage button contained a title and a second-line explanation, while the adjacent current-stage summary and the stage panels already explained what each stage does.
- Changed the top workflow rail:
  - reduced the rail height from 68px to 54px;
  - changed the four stage buttons from two-line cards to one-line stage movement buttons;
  - kept the current-stage progress/title/next action visible;
  - moved the longer current-stage explanation to the summary panel tooltip through the existing ViewModel binding.
- Kept MVVM boundaries:
  - stage state and wording remain in `WpfLabelingShellViewModel` and `WpfWorkflowStagePresentationService`;
  - XAML only changes visual density and binding placement;
  - no command/workflow state was moved into code-behind;
  - no Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured before/after comparison:
  - Before: `artifacts\ui\wpf-top-workflow-rail-before-1920.png`.
  - After: `artifacts\ui\wpf-top-workflow-rail-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-top-workflow-rail-after-1920.png` produced the after capture.
- Next planned work: audit the current-dataset context bar directly below the workflow rail. It still carries storage folder, image folder, source summary, and three commands in one dense row; the next pass should decide what must remain always visible versus what can move into dataset home/details.

## 2026-07-01 current dataset context bar cleanup

- UX finding from the 1920x1080 labeling capture: after the workflow rail was compressed, the current-dataset bar still behaved like a dense details row. Storage folder, image folder, work basis, and three commands competed for the same horizontal attention.
- Changed the current-dataset bar:
  - reduced the bar height from 56px to 48px;
  - kept the always-visible essentials: current dataset name, purpose, work basis, and dataset/storage/image actions;
  - collapsed the storage-folder and image-folder detail cards from the default row while keeping their text bindings and automation targets available for details/tests;
  - kept `데이터셋`, `저장 폴더`, and `이미지 폴더` as direct actions because users still need fast recovery when a dataset or image root is wrong.
- Kept MVVM boundaries:
  - dataset context values and commands remain on `WpfLabelingShellViewModel`;
  - XAML only changes which bound detail cards are visible by default;
  - no command/workflow state was moved into code-behind;
  - no Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured before/after comparison:
  - Before: `artifacts\ui\wpf-dataset-context-bar-before-1920.png`.
  - After: `artifacts\ui\wpf-dataset-context-bar-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-dataset-context-bar-after-1920.png` produced the after capture.
- Next planned work: audit the thin status row below the dataset context bar. It currently repeats dataset count/progress/stage/next/inference state, some of which now duplicates the workflow rail and canvas strip.

## 2026-07-01 top status row operations cleanup

- UX finding from the 1920x1080 labeling capture: the thin status row under the dataset context bar repeated workflow information already shown in the top workflow rail (`단계`, `진행`, `다음`). That made the top chrome look like multiple competing progress bars before the user reached the canvas.
- Changed the top status row:
  - reduced the status row height from 36px to 30px;
  - kept visible operational state only: dataset queue summary, inference state, annotation-save state, and model state;
  - kept workflow stage/progress/next bindings and automation targets available, but collapsed them visually so tests and ViewModel fanout remain stable.
- Kept MVVM boundaries:
  - state still flows through `WpfStatusBarPanelViewModel`;
  - XAML only changes visibility and density;
  - no command/workflow state was moved into code-behind;
  - no Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured before/after comparison:
  - Before: `artifacts\ui\wpf-status-row-before-1920.png`.
  - After: `artifacts\ui\wpf-status-row-after-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-status-panels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-status-row-after-1920.png` produced the after capture.
- Next planned work: audit the canvas header/toolbar density now that top chrome is lighter. The canvas still has a title row, save/tool row, workflow strip, quick tools, mode selector, save state, class selector, and right-side action chips; the next pass should decide which controls are always needed while drawing.

## 2026-07-01 bottom-log collapse pass

- UX/product finding from the docked-workbench review: after the right panel was collapsed in labeling mode, the bottom log still consumed a permanent 160px strip. That kept the canvas smaller even though normal labeling should only need log awareness when something happens or the user asks for details.
- Changed the bottom log layout:
  - Converted `WpfShellLogPanelViewModel` into the owner of bottom-log state.
  - The log starts collapsed at a 42px summary height and shows latest-log text, log count, and a `로그 열기` command.
  - Opening the log expands the bottom row to the detailed `OpenVisionLab.Logging.Controls` log panel.
  - `AppendLog(...)` now updates the collapsed summary ViewModel before writing to the existing logging backend.
  - The shell grid binds the bottom row and separator height to `ShellLogViewModel`, instead of hard-coding the row to 160px.
- Kept MVVM boundaries:
  - Log row height, collapsed/expanded visibility, latest text, count text, and toggle command live on `WpfShellLogPanelViewModel`.
  - `WpfShellLogPanel.xaml` only binds to ViewModel state and keeps the existing WPF log control for details.
  - Shell code-behind remains a logging adapter via `AppendLog(...)`.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Before bottom-log collapse: `artifacts\ui\wpf-right-dock-after-labeling-collapsed-1920.png`.
  - After bottom-log collapse: `artifacts\ui\wpf-bottom-log-collapsed-labeling-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-status-panels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output .\artifacts\ui\wpf-bottom-log-collapsed-labeling-1920.png` passed and produced the capture.
- Next planned work: make model-history rows actionable so a user can inspect older trained candidates, compare them with the current inspection model, and promote a selected candidate intentionally.

## 2026-07-01 model-history action pass

- UX/product finding from the post-training workflow review: the model registry showed recent trained/current/rejected rows, but the rows were read-only. A user could see that multiple models existed but could not intentionally select an older candidate and make it the inspection model from the same surface.
- Changed the model-history surface:
  - `WpfModelRegistryPresentationService` now includes candidate identity, weights path, current-model state, and action availability in each history row.
  - `WpfLabelingShellViewModel` now owns the selected model-history row, selected-row detail text, selected-row apply button state, and a `PromoteSelectedModelHistoryCommand`.
  - `WpfLabelingShellWindow.xaml` changed the history list from a read-only `ItemsControl` to a selectable `ListBox`, with a selected-detail card and an explicit `검사 모델로 적용` action.
  - `WpfLabelingShellWindow.ModelHistoryCommands.cs` applies a selected historical candidate only after checking that the weights file exists and that it is not already the current inspection model. The command records the model-registry adoption decision, updates recipe model settings, and refreshes the model center.
- Kept MVVM boundaries:
  - Row formatting and action eligibility stay in the presentation service.
  - Selection, detail, and button enablement stay in `WpfLabelingShellViewModel`.
  - Shell code-behind is limited to the recipe/model-registry persistence adapter.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Before actionable rows: `artifacts\ui\wpf-model-history-list-1920.png`.
  - After selectable/actionable rows: `artifacts\ui\wpf-model-history-action-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-status-panels` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-history-action-1920.png` passed and produced the capture.
  - `git diff --check -- "0. UI/9) WPF/Services/WpfModelRegistryPresentationService.cs" "0. UI/9) WPF/ViewModels/WpfLabelingShellViewModel.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml" "0. UI/9) WPF/Views/WpfLabelingShellWindow.PanelWiring.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.ModelHistoryCommands.cs" tests/LabelingApplication.Tests/Program.cs` passed with only LF-to-CRLF warnings.
- Next planned work: continue the first-use sweep through dataset creation -> label save -> training -> candidate decision -> inspection with the selected model, then remove any remaining places where users must infer state from the bottom log.

## 2026-07-01 model-center current-inspection action pass

- UX/product finding from the first-use sweep: after training/model selection, the model-center stage exposed candidate review and recipe save, but the next inspection action was only obvious if the user noticed the separate global `현재 검사` button. That made the post-save path feel disconnected from the model decision area.
- Changed the model-center action path:
  - Added ViewModel-owned current-inspection action text, tooltip, and enablement state to `WpfLabelingShellViewModel`.
  - Added `현재 검사` to the top training/model workflow action panel beside `후보 검증` and `검사 모델로 저장`.
  - Added the same `현재 검사` action to the model-center lifecycle action row.
  - Reused the existing `DetectCurrentImageCommand`; no inference, viewer, OpenGL, ROI, brush, or eraser runtime path was changed.
- Kept MVVM boundaries:
  - Button text, tooltip, and enablement are calculated in `WpfLabelingShellViewModel`.
  - XAML only binds to existing commands and ViewModel state.
  - The shell keeps no new workflow logic for this action.
- Captured UI evidence:
  - Before current-inspection action in the model center: `artifacts\ui\wpf-model-history-action-1920.png`.
  - After current-inspection action in the model center: `artifacts\ui\wpf-model-center-inspect-action-1920.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-status-panels` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-inspect-action-1920.png` passed and produced the capture.
- Next planned work: continue the same first-use sweep at the transition from inference results back to saved labels, so users can tell whether they are reviewing AI candidates or editing committed labels without relying on bottom-log messages.

## 2026-07-02 guide/tools helper-role pass

- UX/product finding from the Guide/Tools self-audit: the template repeat-labeling card explained the steps, but the card still read like a primary labeling workflow. A beginner could run template matching and miss that it only creates label candidates until the candidate is reviewed and `라벨 저장` is pressed.
- Changed the Guide/Tools surface:
  - Added a compact Guide/Tools role card near the top of `WpfLearningWorkflowPanel` that separates the primary work (`라벨 그리기 -> 라벨 저장 -> 다음 이미지`) from helper tools.
  - Added a visible helper-role line inside `TemplateWorkflowPanel` so the template feature itself says it is a candidate-generation helper and that final training data still requires review plus label save.
  - Kept the text in `WpfLearningWorkflowPanelViewModel`; XAML only binds the text and exposes stable automation ids.
- Kept MVVM boundaries:
  - New workflow wording lives in `WpfLearningWorkflowPanelViewModel`.
  - `WpfLearningWorkflowPanel.xaml.cs` only exposes the named WPF elements for tests.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Before helper-role line: `artifacts\ui\wpf-guide-tools-helper-role-before-1920.png`.
  - After helper-role line at 1920x1080: `artifacts\ui\wpf-guide-tools-helper-role-after-1920.png`.
  - After helper-role line at 1366x768: `artifacts\ui\wpf-guide-tools-helper-role-after-1366.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-learning-workflow-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --template-guide-ux` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --review-tabs guide --width 1920 --height 1080` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --review-tabs guide --width 1366 --height 768` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --roi-only --review-tab guide --right-workflow-expanded --expand-learning-concepts --focus-template-workflow --width 1920 --height 1080 --output .\artifacts\ui\wpf-guide-tools-helper-role-after-1920.png` produced the after capture.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --roi-only --review-tab guide --right-workflow-expanded --expand-learning-concepts --focus-template-workflow --width 1366 --height 768 --output .\artifacts\ui\wpf-guide-tools-helper-role-after-1366.png` produced the after capture.
- Next planned work: audit the saved-label vs AI-candidate transition in the right-side review panels again, specifically the first time a user switches from labeling to inference review, so the mode change is visible without relying on the bottom log.

## 2026-07-02 image-queue clipping and right-tab theme pass

- UX/product finding from the user screenshots: the left `Image Queue` control area still clipped top controls at the default EXE size when the first action row wrapped, and the right workflow sub-tabs still used default white WPF tab chrome inside an otherwise dark workbench.
- Changed the UI surface:
  - `WpfImageQueuePanel.xaml` now lets the primary action, current-task, and batch-progress rows auto-size with compact minimum heights, so narrow widths can wrap controls without clipping them.
  - `ImageQueuePrimaryActionsPanel` now reserves vertical spacing before the folder/path row.
  - `WpfLabelingShellWindow.xaml` now defines a dark `WorkflowViewHostTabItemStyle` template for the right workflow tabs, including selected/hover/disabled states from app brushes.
  - Added source-level regression assertions for the queue row sizing and dark tab template.
  - Added WPF/WinForms type aliases in WPF adapter/code-behind files to unblock build verification without changing workflow logic.
- Kept MVVM boundaries:
  - No new command, state, or workflow logic was added to View code-behind.
  - XAML only handles layout/theme presentation.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Before user screenshot: `C:\Users\user\AppData\Local\Temp\codex-clipboard-fbf83517-285d-48b2-97a4-4c6b34f0f798.png`.
  - Before user screenshot for right tabs: `C:\Users\user\AppData\Local\Temp\codex-clipboard-8245ab6f-a663-46d5-89f6-f9fd21c2ea55.png`.
  - After 1366x768 queue/tabs: `artifacts\ui\wpf-left-queue-tabs-after-1366x768.png`.
  - After 1366x768 right tabs: `artifacts\ui\wpf-right-tabs-after-1366x768.png`.
  - After 1920x1080 baseline: `artifacts\ui\wpf-left-queue-tabs-after-1920x1080.png`.
- Verified:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-image-queue-status` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --roi-only --review-tab objects --width 1366 --height 768 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-left-queue-tabs-after-1366x768.png"` produced the after capture.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --roi-only --review-tab guide --width 1366 --height 768 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-right-tabs-after-1366x768.png"` produced the after capture.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --roi-only --review-tab guide --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-left-queue-tabs-after-1920x1080.png"` produced the 1920 baseline capture.
- Next planned work: continue with the saved-label versus AI-candidate transition audit, but do not repeat the already completed queue clipping, canvas-toolbar density, right-tab theme, or guide/tools helper-role passes.

## 2026-07-02 saved-label vs AI-candidate mode badge pass

- UX/product finding from the saved-label versus AI-candidate transition audit: the canvas display modes were already separated, but the right review panels still required users to read body text to know whether they were editing committed labels or only reviewing unsaved AI candidates.
- Changed the review-panel surface:
  - `WpfCandidateReviewPanelViewModel` now owns an explicit `AI 후보 검토` mode badge and `확정 전에는 저장 라벨 아님` scope text.
  - `WpfObjectReviewPanelViewModel` now owns an explicit `저장 라벨만` mode badge and `AI 후보 표시 안 함` scope text.
  - `WpfCandidateReviewPanel.xaml` and `WpfObjectReviewPanel.xaml` bind those texts into the first visible mode card with stable automation ids.
  - Focused tests assert the new bindings, automation ids, and ViewModel-owned wording so the distinction does not regress into bottom-log-only messaging.
- Kept MVVM boundaries:
  - Mode wording is computed in the panel ViewModels.
  - XAML only binds and styles the mode badges.
  - No command/workflow state was moved into View code-behind.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Candidate panel before: `artifacts\ui\wpf-label-vs-candidate-before-1920.png`.
  - Candidate panel after: `artifacts\ui\wpf-label-vs-candidate-after-1920.png`.
  - Candidate panel before crop: `artifacts\ui\wpf-label-vs-candidate-before-1920-right-crop.png`.
  - Candidate panel after crop: `artifacts\ui\wpf-label-vs-candidate-after-1920-right-crop.png`.
  - Saved-label panel after expanded crop: `artifacts\ui\wpf-saved-label-panel-after-1920-expanded-right-crop.png`.
- Verified:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-candidate-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-object-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
- Next planned work: audit the moment after an AI candidate is confirmed, specifically whether the UI makes the transition to `label save required` and then `saved label` visible across the canvas, queue, and right panel without relying on the bottom log.

## 2026-07-02 confirmed-candidate saved-label clarity pass

- UX/product finding from the post-candidate-confirmation audit: candidate confirmation already writes the accepted boxes into the current image label file, but the right saved-label panel and review history still made those objects look like generic AI candidates. That forced users to infer whether the accepted candidate was still pending or had become training data.
- Changed the confirmed-candidate surface:
  - confirmed AI candidates now appear in Object Review as `확정 라벨` rows, not generic `AI` rows;
  - confirmed rows include tooltip/source detail that the object came from `AI 후보 확정` and was reflected as a saved label;
  - confirming AI candidates now registers the confirmed candidate classes in the class catalog before saving, then re-syncs the object-class editor after the class-list refresh so the selected row and class combo stay aligned;
  - Candidate Review completion wording now separates `라벨 저장 필요` from `저장 완료`, matching whether file persistence is still required;
  - Candidate Review/Object Review mode text now states that unconfirmed AI candidates are not saved labels, while confirmed candidates become saved labels;
  - the right workflow review `TabControl` tab strip is explicitly placed at the top so hidden/local tabs do not reserve a left-side blank area over the saved-label content.
- Kept MVVM boundaries:
  - candidate/object review wording stays in the panel ViewModels and presentation services;
  - `WpfLabelingShellWindow.xaml` only changes the review tab layout property;
  - the visual-smoke-only `--confirm-all-candidates` switch lives in the test harness;
  - no command/workflow state was moved into View code-behind;
  - no Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- Captured UI evidence:
  - Before right-panel overlap/fuzzy saved-label state: `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920.png`.
  - Before right-panel crop: `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-right-panel-crop.png`.
  - After 1920x1080 confirmed-candidate saved-label state: `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed.png`.
  - After right-panel crop: `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed-right-panel-crop.png`.
- Verified:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-candidate-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-object-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-session-smoke` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --review-tab objects --right-workflow-expanded --confirm-all-candidates --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed.png"` passed and produced the after capture.
- Next planned work: audit class-change/delete after confirmed labels, especially whether changing a confirmed label's class or deleting it clearly moves the canvas, queue row, and right panel back to `저장 필요` until the user writes the file.

## 2026-07-02 confirmed-label edit save-required pass

- 점검 결과: 오른쪽 저장 라벨 패널에서 확정/저장된 라벨을 수정하거나 삭제해도, 왼쪽 이미지 큐가 메모리 상태를 다시 읽으면서 저장됨처럼 보일 수 있었습니다. 캔버스의 저장 버튼을 못 보면 사용자는 수정한 라벨이 이미 파일에 반영된 것으로 오해할 수 있었습니다.
- 수정 내용:
  - `WpfImageQueueItem` now carries an explicit `IsSaveRequired` flag, separate from label count and review state.
  - `WpfImageQueuePanelViewModel` prioritizes `IsSaveRequired` in the selected-image task card, so the left queue says `라벨 저장 필요` until the operator presses `라벨 저장`.
  - `MarkAnnotationsDirty` now updates the active image queue row immediately to `저장 필요` when a saved/confirmed label class is changed, a label is deleted, or another dirty annotation action occurs.
  - `ApplyReviewStatusToItem` clears the flag for normal refreshes but reapplies it for the active image while `annotationDirtyReason` is still set, preventing async queue refresh from overwriting the dirty state.
  - `WpfObjectReviewPanelViewModel` now says that class/delete edits become `저장 필요` and must be written with `라벨 저장`.
- 구조:
  - Save-required state lives on the queue item model and is rendered by the image queue ViewModel.
  - Right-panel action wording stays in `WpfObjectReviewPanelViewModel`.
  - Shell code-behind only adapts the existing active-image dirty state to the active queue item.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- 화면 증거:
  - Before confirmed saved-label state: `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed.png`.
  - Before right-panel crop: `artifacts\ui\wpf-confirmed-candidate-saved-label-after-1920-fixed-right-panel-crop.png`.
  - After confirmed-label edit save-required state: `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920.png`.
  - After left queue crop: `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-left-crop.png`.
  - After right saved-label crop: `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-right-crop.png`.
  - After top status crop: `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920-top-crop.png`.
- 검증:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-session-smoke` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-image-queue-status` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-object-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --review-tab objects --right-workflow-expanded --confirm-all-candidates --edit-confirmed-label-class --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920.png"` passed and produced the after capture.
- Next planned work: audit the save-after-edit recovery path, specifically that pressing `라벨 저장` after class/delete clears the canvas, left queue, current-task card, and right saved-label panel back to `저장됨` without stale `저장 필요` badges.

## 2026-07-02 README and tutorial commit-readiness pass

- 점검 결과: `README.md`와 HTML 튜토리얼은 있었지만, 처음 보는 사람이 데이터셋 생성부터 라벨링, 학습, 추론 검토, 모델 적용까지의 흐름을 바로 잡기에는 개발자용 설명이 많았습니다.
- 문서 변경:
  - `README.md`를 프로젝트 소개 문서로 다시 정리했습니다: 지원 범위, 처음 10분, 상태 의미, 실행 명령, YOLOv5 연결, 작업 흐름도, 개발 문서 목록, 검증 명령, 커밋 전 체크 기준.
  - Added `docs/tutorial/README.md` as a GitHub-readable tutorial lecture covering dataset setup, image queue, class registration, box labeling, save-required states, dataset check, YOLO training, inspection-model application, inference review, model comparison, and common confusion points.
  - Linked the HTML tutorial back to the new Markdown tutorial so users can choose text-first or screenshot-first guidance.
  - Added `docs/tutorial/labeling-workbench-tutorial-standalone.html`, a self-contained copy of the HTML tutorial with all six PNG captures embedded as base64 data URIs, so users can copy one file to another PC/folder without broken images.
  - Kept required links to `YOLOV5_TRAINING_RESULT_WORKFLOW.md`, `SEGMENTATION_UX_COMPLETION.md`, and `ANOMALY_DETECTION_FLOW.md`.
- 변경 범위:
  - Documentation-only change.
  - No WPF code-behind, ViewModel, service, Viewer/OpenGL/ROI/brush/eraser path was changed in this pass.
- Verified:
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --priority-workflow-docs` passed.
  - Local README/tutorial link check passed for `README.md`, `docs/tutorial/README.md`, and `docs/tutorial/labeling-workbench-tutorial.html`.
  - Standalone tutorial check passed: six embedded `data:image/png;base64` images were found and no `src="images/...` references remained.
  - `git diff --check -- README.md "docs/tutorial/README.md" "docs/tutorial/labeling-workbench-tutorial.html"` passed with only LF-to-CRLF warnings.
- Next planned work: continue the save-after-edit recovery audit, then prepare a commit scope summary that separates pre-existing dirty worktree changes from the README/tutorial documentation pass and the latest save-required UX pass.

## 2026-07-02 confirmed-label edit save-recovery pass

- 점검 결과: 이전 작업으로 클래스 변경/삭제가 `저장 필요`로 바뀌는 것은 보였지만, 오른쪽 저장 라벨 패널 자체에는 파일 저장 상태가 따로 보이지 않았습니다. 사용자가 `라벨 저장`을 눌러도 오른쪽 패널은 다음 편집 안내만 보여서 저장 완료 여부를 한눈에 확인하기 어려웠습니다.
- 수정 내용:
  - `WpfObjectReviewPanelViewModel` now owns `LabelSaveStateKey`, `LabelSaveBadgeText`, and `LabelSaveDetailText`.
  - `WpfObjectReviewPanel.xaml` shows a compact save-state badge in the selected-image task card: `라벨 대기`, `저장 필요`, or `저장됨`.
  - `MarkAnnotationsDirty`, `MarkAnnotationsSaved`, and `SetAnnotationSaveStatusWaiting` now fan out the same save state to the right saved-label panel.
  - The WPF visual smoke harness can now capture the path `확정 라벨 수정 -> 라벨 저장` using `--save-after-confirmed-label-edit`.
- 구조:
  - Right-panel save-state text lives in `WpfObjectReviewPanelViewModel`.
  - XAML only binds and styles the state.
  - Shell code-behind remains the active-image persistence adapter and only fans out the existing dirty/saved/waiting state.
  - No Viewer/OpenGL/ROI/brush/eraser performance path was changed.
- 화면 증거:
  - Before save-required state: `artifacts\ui\wpf-confirmed-label-edit-save-required-after-1920.png`.
  - After save recovery: `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png`.
  - After left queue crop: `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-left-crop.png`.
  - After right saved-label crop: `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-right-crop.png`.
  - After top status crop: `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920-top-crop.png`.
- 검증:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-session-smoke` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-object-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-image-queue-status` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --review-tab objects --right-workflow-expanded --confirm-all-candidates --edit-confirmed-label-class --save-after-confirmed-label-edit --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png"` passed and produced the after capture.
- Next planned work: prepare a commit scope summary that separates pre-existing dirty worktree changes from the latest UX/documentation passes, then decide whether to stage one coherent documentation+UX commit or split documentation and app UX into separate commits.

## 2026-07-02 saved-label edit focused rerun pass

- 다시 확인한 범위: 저장 라벨을 수정한 뒤 `저장 필요`가 보이고, `라벨 저장` 후 `저장됨`으로 돌아오는 경로.
- 검증:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed with 0 warnings and 0 errors.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-session-smoke` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-object-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-image-queue-status` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-visual-smoke --review-tab objects --right-workflow-expanded --confirm-all-candidates --edit-confirmed-label-class --save-after-confirmed-label-edit --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png"` passed.
- 화면 증거: `artifacts\ui\wpf-confirmed-label-edit-save-recovered-after-1920.png`.
- 다음 작업: Scope B를 그대로 커밋하기에는 이미지 큐/오른쪽 패널 파일에 다른 UX 변경도 섞여 있으므로, 커밋 전에는 분리 가능한 파일과 섞인 파일을 다시 나눕니다.

## 2026-07-02 tutorial realism and large-capture pass

- 점검 결과: 첫 튜토리얼은 실제 사용 절차보다 개요 설명에 가까웠고, 화면 예시도 여섯 장뿐이라 실제 작업자가 보며 따라가기에는 부족했습니다.
- 문서 변경:
  - `docs/tutorial/README.md`를 데이터셋 준비부터 모델 적용까지 따라가는 작업 가이드로 다시 작성했습니다.
  - `docs/tutorial/labeling-workbench-tutorial.html`은 넓은 화면에서 보는 캡처 중심 가이드로 다시 구성했습니다.
  - `docs/tutorial/images`에 실제 화면 흐름을 보여주는 캡처 14장을 추가했습니다: 전체 화면, 데이터셋 확인, 라벨링, 저장 라벨, 라벨/후보 비교, 템플릿, 저장 필요/완료, 모델 센터, 학습 완료, 모델 이력, 추론 검토, 모델 판단, 모델 검사.
  - `docs/tutorial/labeling-workbench-tutorial-standalone.html`은 표시 이미지 14장이 모두 포함되도록 다시 생성했습니다.
  - 기존 튜토리얼 테스트 계약 때문에 `images/01-guide.png`, `images/06-inference-review.png` 링크는 유지하고, 실제로 보이는 가이드는 큰 캡처로 교체했습니다.
- 변경 범위:
  - 문서와 이미지 자산만 변경했습니다.
  - WPF code-behind, ViewModel, Service, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- Verified:
  - Standalone tutorial generation check found 14 `src` image references in the HTML, 14 embedded `data:image/png;base64` images in the standalone HTML, and 0 remaining `src="images/...` references.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --priority-workflow-docs` passed.
  - Local HTML image check passed: 14 displayed image paths exist, legacy `images/01-guide.png` and `images/06-inference-review.png` references remain for the existing tutorial contract, and standalone has 0 remaining image-file `src` paths.
  - `git diff --check -- "docs/tutorial/README.md" "docs/tutorial/labeling-workbench-tutorial.html" "docs/tutorial/labeling-workbench-tutorial-standalone.html" "docs/WORK_TRACKING.md" "docs/STABLE_VERIFIED_AREAS.md"` passed with only LF-to-CRLF warnings.
  - Chrome headless rendered the HTML guide at 1920x1080 and captured `artifacts\ui\tutorial-expanded-html-after-1920.png`, confirming the guide opens with a large actual workbench screenshot instead of the previous small example layout.
- Next planned work: return to the commit scope summary and separate this expanded tutorial pass from the pre-existing dirty worktree changes before any staging decision.

## 2026-07-02 commit-scope split pass

- Maintenance finding: the worktree now has documentation updates, saved-label UX changes, broader workflow/model-center/template changes, and protected Viewer/OpenGL edits all present at once. A broad stage/commit would mix unrelated risk levels and make later review difficult.
- Added `docs/COMMIT_SCOPE_20260702.md` to split the current dirty tree into:
  - Scope A: README/tutorial documentation and large-capture tutorial assets.
  - Scope B: saved-label edit save-required/save-recovery UX.
  - Scope C: broader accumulated UX/MVVM/model/template work that needs separate audit.
  - Do-not-stage defaults for Codex handoff notes, artifacts, and protected Viewer/OpenGL paths.
- Kept code boundaries:
  - Documentation-only change.
  - No WPF code-behind, ViewModel, service, Viewer/OpenGL/ROI/brush/eraser path was changed in this pass.
- Verified:
  - `git status --short` was checked first.
  - Worktree status summary was recorded as 146 modified files, 38 untracked paths, and 1 deleted path.
  - `CODEX_NEXT_PROMPT.md` and `CODEX_RECOVERY.md` were read for current handoff context.
  - `git diff --check -- docs/WORK_TRACKING.md docs/STABLE_VERIFIED_AREAS.md` passed with only LF-to-CRLF warnings.
  - Custom trailing-whitespace check passed for `docs/COMMIT_SCOPE_20260702.md` and `docs/WORK_TRACKING.md`.
- Next planned work: decide with the user whether to stage Scope A alone, Scope B alone, or continue UX/MVVM audit without committing yet.

## 2026-07-02 staging-candidate inspection pass

- Maintenance finding: Scope A can be reviewed as a coherent product-documentation commit, but Scope B cannot be safely staged file-by-file because the relevant test file and several WPF files contain accumulated changes from earlier UX passes.
- Updated `docs/COMMIT_SCOPE_20260702.md` with a staging-candidate inspection:
  - Scope A is the safest first commit candidate.
  - Scope B requires `git add -p` and cached-diff review; no whole-file staging recommendation remains for the saved-label UX slice.
  - Scope C remains audit-only and should not be mixed into documentation or saved-label UX commits.
  - `docs/COMMIT_SCOPE_20260702.md` itself is treated as an optional internal scope-note commit, not part of the product tutorial commit by default.
- Kept code boundaries:
  - Documentation-only change.
  - No WPF code-behind, ViewModel, service, Viewer/OpenGL/ROI/brush/eraser path was changed in this pass.
- Verified:
  - `git status --short` was checked first.
  - Scope A/B diff stats were inspected: documentation changed across `README.md`, tutorial HTML, and tracking docs; saved-label UX touches 11 tracked files with `tests/LabelingApplication.Tests/Program.cs` carrying a 5,299-line accumulated diff.
  - Recent tracking/stable line markers were identified for patch-staging: tutorial sections, saved-label sections, and the standalone tutorial contract.
  - `git diff --check -- docs/WORK_TRACKING.md docs/STABLE_VERIFIED_AREAS.md docs/COMMIT_SCOPE_20260702.md` passed with only LF-to-CRLF warnings.
  - Custom trailing-whitespace check passed for `docs/COMMIT_SCOPE_20260702.md` and `docs/WORK_TRACKING.md`.
- Next planned work: if the user wants a commit, stage Scope A first with patch-staged tracking/stable records, then run cached diff review before committing.

## 2026-07-02 README and tutorial portfolio documentation pass

- 점검 결과: README와 튜토리얼에 필요한 정보는 있었지만, 첫인상이 기능 목록 위주였습니다. 포트폴리오에 쓰려면 데이터셋 준비, 저장 라벨, 학습, 추론 검토, 모델 적용이 이어지는 프로그램의 정체성이 먼저 보여야 합니다.
- 문서 변경:
  - `README.md`는 프로그램의 목적, 지원 흐름, 설계 기준, 구조, 실행, 검증, 관련 문서가 먼저 보이도록 다시 정리했습니다.
  - `docs/tutorial/README.md`는 작업자가 바로 따라갈 수 있는 말투로 다듬고, 데이터셋 준비부터 라벨링, 템플릿 보조, 학습, 추론 검토, 모델 판단까지의 흐름을 유지했습니다.
  - HTML 튜토리얼은 이미지 폴더와 저장 폴더, 저장 라벨과 AI 후보, 학습 완료와 현재 검사 모델의 차이가 바로 보이도록 문장을 다시 다듬었습니다.
  - 수정한 HTML을 기준으로 `docs/tutorial/labeling-workbench-tutorial-standalone.html`을 다시 생성했습니다.
- 변경 범위:
  - 문서만 변경했습니다.
  - WPF code-behind, ViewModel, Service, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- Verified:
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --priority-workflow-docs` passed.
  - Local README/tutorial link and image check passed for `README.md`, `docs/tutorial/README.md`, and `docs/tutorial/labeling-workbench-tutorial.html`.
  - Standalone tutorial check passed: 14 embedded `data:image/png;base64` images and 0 remaining `src="images/...` references.
  - `git diff --check -- README.md docs/tutorial/README.md docs/tutorial/labeling-workbench-tutorial.html docs/tutorial/labeling-workbench-tutorial-standalone.html docs/WORK_TRACKING.md` passed with only LF-to-CRLF warnings.
  - Custom trailing-whitespace check passed for the updated README/tutorial/tracking files.
  - Chrome headless rendered the HTML guide at 1920x1080.
- Next planned work: review the README/tutorial wording once more from the portfolio reader's perspective, then stage Scope A only if the user asks to prepare a commit.

## 2026-07-02 tutorial annotated-screenshot and latest-image rule pass

- 점검 결과: 캡처가 크다는 설명만으로는 부족했습니다. 사용자가 이미지를 먼저 보고 따라갈 수 있도록 캡처 안에 번호와 화살표가 필요했고, UI가 바뀐 뒤에도 예전 캡처를 그대로 두지 않는 규칙이 필요했습니다.
- 문서 변경:
  - `docs/tutorial/images/annotated` 아래 캡처에 빨간 번호와 화살표를 넣었습니다.
  - `README.md`, `docs/tutorial/README.md`, `docs/tutorial/labeling-workbench-tutorial.html`이 annotated 캡처를 사용하도록 바꿨습니다.
  - README/튜토리얼 이미지는 최신 EXE UI 캡처 기준으로 갱신한다는 규칙을 `README.md`, `docs/tutorial/README.md`, HTML 튜토리얼, `docs/STABLE_VERIFIED_AREAS.md`에 남겼습니다.
  - annotated 최신 캡처가 포함되도록 `docs/tutorial/labeling-workbench-tutorial-standalone.html`을 다시 생성했습니다.
- 변경 범위:
  - 문서와 이미지 자산만 변경했습니다.
  - WPF code-behind, ViewModel, Service, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- Verified:
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --priority-workflow-docs` passed.
  - Local README/tutorial image check passed for `README.md`, `docs/tutorial/README.md`, and `docs/tutorial/labeling-workbench-tutorial.html`.
  - Latest-image rule text exists in `README.md`, `docs/tutorial/README.md`, `docs/tutorial/labeling-workbench-tutorial.html`, and `docs/STABLE_VERIFIED_AREAS.md`.
  - HTML tutorial has 14 `src="images/annotated/...` references and 0 non-annotated image `src` references.
  - Standalone tutorial has 14 embedded `data:image/png;base64` images and 0 remaining `src="images/...` references.
  - The old `1920 x 1080 기준 캡처` / `해상도 기준` wording is no longer present in the visible tutorial image path/alt checks.
  - `git diff --check -- README.md docs/tutorial/README.md docs/tutorial/labeling-workbench-tutorial.html docs/tutorial/labeling-workbench-tutorial-standalone.html docs/STABLE_VERIFIED_AREAS.md docs/WORK_TRACKING.md` passed with only LF-to-CRLF warnings.
  - Chrome headless rendered the HTML guide at 1920x1080 and captured `artifacts\ui\tutorial-annotated-guide-after-1920.png`.
- Next planned work: review Scope A documentation as a single staging candidate, then continue the app UX/MVVM audit only after the documentation slice is cleanly separated from the broader dirty worktree.

## 2026-07-02 segmentation save native fallback pass

- 점검 결과: clean HEAD 기반 검증 worktree에서는 WPF 라벨링 세션 저장 중 raster mask를 polygon JSON으로 변환하는 경로가 OpenCV `FindContours` native AccessViolation으로 중단됐습니다. 이 오류는 뷰어/브러시 입력 hot path가 아니라 YOLO segmentation 저장/export 경로에서 발생했습니다.
- 수정 내용:
  - `YoloSegmentationAnnotationService.BuildPolygonRecords`의 raster mask 저장 경로에서 `SegmentationGeometry.RasterMaskToRegions` 호출을 제거했습니다.
  - raster mask의 실제 `Bounds`를 `SegmentationGeometry.RectangleToPolygon`으로 변환해 segment JSON에 남깁니다.
  - polygon 기반 세그먼트 저장, mask PNG 저장, View/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
  - 기존 `--segmentation-annotation-storage` smoke 안에 raster mask 저장 검증을 추가했습니다.
- 검증:
  - 임시 HEAD 기반 worktree `C:\Git\Labelling_Application_opencv_stage`에서 `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\"` passed with 0 warnings and 0 errors.
  - `dotnet "C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\LabelingApplication.Tests.dll" --segmentation-annotation-storage` passed.
  - `dotnet "C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-session-smoke` passed and captured `C:\artifacts\ui\wpf-labeling-session-smoke.png`.
  - `dotnet "C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-annotation-purpose-export` passed.
  - `dotnet "C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-object-review-panel` passed.
  - `dotnet "C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-image-queue-status` passed.
  - `dotnet "C:\Git\Labelling_Application_opencv_stage\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `git diff --check` passed with only LF-to-CRLF warnings.
- 다음 작업: 실제 UI에서 학습 완료 모델과 현재 검사 모델을 구분해 보여주는 상태/모델 선택 UX를 이어서 점검합니다.

## 2026-07-02 inspection model status badge and model-center evidence pass

- 점검 결과: 학습 완료 후 상단 상태와 모델 센터가 모두 "모델"이라는 말로만 보이면 사용자가 `새 학습 후보`와 `현재 검사 모델`을 구분하기 어렵습니다. 특히 후보를 선택했지만 recipe 저장 전이면 다음 추론에는 아직 기존 검사 모델을 쓰는 상태라서, 이 차이를 한눈에 보여줘야 합니다.
- 수정 내용:
  - `WpfStatusBarPanelViewModel`에 `InspectionModelStatusText` / `InspectionModelStatusToolTip`을 추가해 현재 검사 모델 상태를 transient 작업 상태와 분리했습니다.
  - `WpfStatusBarPanel.xaml` 상태줄에 `검사 모델: ...` 전용 배지를 추가했습니다. 기존 `ModelStatusText`는 작업 단계/자동화 상태 용도로 유지합니다.
  - YOLO runtime status refresh에서 현재 저장된 검사 모델과 저장 대기 후보를 구분해 전용 배지에 반영하도록 연결했습니다.
  - 모델 센터의 적용 근거 문장에 `학습 실패 아님`을 남겨, 지표 보류/후보 검증 상태가 학습 실패처럼 읽히지 않게 했습니다.
  - WPF 모델 센터 smoke가 실제 YOLO run 형태처럼 `best.pt`와 같은 run의 `results.csv`를 함께 생성하도록 보정해, 모델 이력 비교에서 `mAP50-95` 지표가 유지되는지 확인했습니다.
- 검증:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed with 0 warnings and 0 errors.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-status-panels` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-inspection-model-status-after-1920.png"` passed.
  - `git diff --check --` 대상 변경 파일 passed with only LF-to-CRLF warnings.
- 캡처:
  - 전체 화면: `artifacts\ui\wpf-inspection-model-status-after-1920.png`
  - 상태줄 확대: `artifacts\ui\wpf-inspection-model-status-row-crop.png`
- 다음 작업: 모델 센터의 후보 적용 카드와 상단 상태 영역을 더 줄여서, "후보 검증 -> 검사 모델로 저장 -> 현재 검사" 흐름이 1366 폭에서도 같은 우선순위로 보이는지 확인합니다.

## 2026-07-02 model-center priority card pass

- 점검 결과: 1366x768 화면에서 학습/모델 센터의 첫 화면은 모델 레지스트리 설명이 먼저 길게 보이고, 실제 사용자가 눌러야 할 `후보 검증 -> 검사 모델로 저장 -> 현재 검사` 버튼 흐름은 아래쪽으로 밀렸습니다.
- 수정 내용:
  - 학습/모델 센터 상단에 `모델 적용 순서` 우선순위 카드를 추가했습니다.
  - 카드 안에 `현재 검사`, `학습 후보`, `다음`을 한 줄 흐름으로 보여주고, 바로 아래에 `후보 검증`, `검사 모델로 저장`, `현재 검사` 버튼을 배치했습니다.
  - 새 카드는 기존 `ShellViewModel` 상태와 명령을 그대로 바인딩합니다. XAML은 표시만 하고, 상태/워크플로우 판단은 ViewModel/Service에 남겼습니다.
  - `WpfModelRegistryPresentationService.BuildInspectionModelText`에서 저장 대기 후보를 현재 검사 모델로 잘못 표시하지 않도록 수정했습니다. pending 상태에서도 현재 검사 모델 행은 실제 registry의 현재 모델을 우선 표시합니다.
  - `BuildModelCenterCandidateModelText`는 pending 상태에서 settings에 후보 경로가 올라와 있어도 `현재 검사 모델과 같음`으로 축약하지 않고 후보 `best.pt`를 계속 보여주도록 조정했습니다.
- 검증:
  - `dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c Debug --nologo -v:minimal /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir="C:\Git\Labelling_Application\artifacts\isolated-out\"` passed with 0 warnings and 0 errors.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-labeling-shell` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --mvvm-infra` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-status-panels` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-priority-after-1366.png"` passed.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output "C:\Git\Labelling_Application\artifacts\ui\wpf-model-center-priority-after-1920.png"` passed.
  - `git diff --check --` 대상 변경 파일 passed with only LF-to-CRLF warnings.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-model-center-priority-before-1366.png`
  - After 1366: `artifacts\ui\wpf-model-center-priority-after-1366.png`
  - Before/after right-panel crop: `artifacts\ui\wpf-model-center-priority-before-1366-right-crop.png`, `artifacts\ui\wpf-model-center-priority-after-1366-right-crop.png`
  - After 1920: `artifacts\ui\wpf-model-center-priority-after-1920.png`
- 다음 작업: 모델 센터 카드의 지표 문장이 길 때 `mAP50-95`, `mAP50`, `precision/recall` 핵심만 먼저 보이고 전체 지표는 상세로 내려가도록 줄입니다.

## 2026-07-02 Test dataset end-to-end tutorial pass

- 요청 배경: 사용자가 만든 `Test` 데이터셋은 이미 라벨링, 학습, 추론까지 진행한 실제 데이터셋이므로, 튜토리얼도 예시 화면이 아니라 이 데이터셋을 기준으로 작성해야 합니다.
- 확인한 실제 데이터:
  - 레시피: `TEST_Dataset_ObjectDetection_20260628_212353`
  - 이미지 폴더: `D:\LabelingData\Test01\Images`
  - 저장 폴더: `C:\Git\Labelling_Application\artifacts\run\Debug\DATA\Dataset_ObjectDetection_20260628_212353`
  - 클래스: `OK`, `NG`
  - 저장 라벨: 125개 이미지 파일, box label 141개
  - 현재 검사 모델: `best.pt`
  - 학습 결과 모델 후보: `exp7\weights\best.pt`
- 실행/캡처:
  - `artifacts\run\Debug\MvcVisionSystem.exe`를 직접 실행했습니다.
  - project recipe UI에서 `TEST_Dataset_ObjectDetection_20260628_212353`를 적용했습니다.
  - 레시피 적용, 데이터셋 홈, 라벨링/저장 라벨, 클래스, 학습/모델 센터, 추론 검토, 현재 검사 실행 후 화면을 1920x1080으로 캡처했습니다.
  - `현재 검사`는 `DetectButton`을 실제로 눌렀고, 버튼 비활성화/재활성화까지 확인한 뒤 AI 후보 1개가 표시된 화면을 캡처했습니다.
  - 원본 캡처: `docs\tutorial\images\test-workflow\*.png`
  - 번호/화살표 캡처: `docs\tutorial\images\test-workflow\annotated\*.png`
- 문서 반영:
  - `docs/tutorial/README.md` 상단에 `Test 데이터셋으로 실제 흐름 따라가기` 장을 추가했습니다.
  - `README.md`의 대표 화면을 실제 Test workflow 캡처로 교체하고 튜토리얼 장으로 연결했습니다.
  - `docs/tutorial/labeling-workbench-tutorial.html` 맨 앞에 Test 실습 섹션을 추가했습니다.
  - `docs/tutorial/labeling-workbench-tutorial-standalone.html`을 다시 생성해 표시 이미지 21장을 모두 base64로 포함했습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - Standalone generation check: standard HTML image refs 21, standalone embedded data images 21, standalone file image refs 0.
  - `git status --short docs\tutorial artifacts\run\Debug\RECIPE\TEST_Dataset_ObjectDetection_20260628_212353 artifacts\run\Debug\DATA\Dataset_ObjectDetection_20260628_212353` showed only the new tutorial image folder; Test recipe/data folders were not modified by the tutorial capture pass.
  - `dotnet "C:\Git\Labelling_Application\artifacts\isolated-out\LabelingApplication.Tests.dll" --priority-workflow-docs` passed.
  - Local tutorial image check passed: standard HTML image refs 21, standalone embedded data images 21, standalone file image refs 0, all referenced image files exist.
  - in-app browser render was not completed because the browser URL policy blocked direct `file:///...` navigation. Per browser policy, no workaround attempt was made.
- 다음 작업: 문서 diff/경로 검증을 마무리하고, 이후에는 모델 센터 카드의 지표 문장 축약 UX를 이어서 진행합니다.

## 2026-07-02 model-center compact metric summary pass

- 점검 결과: 학습/모델 센터의 첫 표시 후보 줄이 `mAP50-95`, `mAP50`, `precision`, `recall`, `box loss`를 한 문장에 모두 붙여 보여 주면 사용자가 후보 모델 파일과 다음 버튼을 먼저 보기 어렵습니다.
- 수정 내용:
  - `WpfModelRegistryPresentationService.BuildCompactMetricSummary`를 추가해 첫 표시 후보 줄에는 `mAP50-95`, `mAP50`, `P/R`만 남기도록 했습니다.
  - slash(`/`)로 구분된 지표와 실제 화면에서 나온 comma(`,`) 구분 지표를 모두 compact 처리합니다.
  - 모델 센터 우선순위 카드의 학습 후보 문장도 같은 formatter를 사용합니다.
  - 전체 지표 문장은 모델 이력과 선택 모델 비교/근거 영역에 남겨, 핵심 요약과 상세 근거를 분리했습니다.
- 구조:
  - 지표 축약은 presentation service에서 처리합니다.
  - XAML은 기존 ViewModel binding을 그대로 사용합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-center-metrics-compact-after-1920.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --settings-viewmodels` was attempted but that is not a registered single-test flag in this test runner, so it fell through into the broader suite and stopped at existing `Template auto label shows actionable guide: registered template batch should complete`.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-priority-after-1920.png`
  - After 1920: `artifacts\ui\wpf-model-center-metrics-compact-after-1920.png`
- 다음 작업: 모델 센터 오른쪽 패널의 이력 카드가 여전히 좁은 폭에서 글줄이 많으므로, 이력 목록은 접힌 요약/선택 상세 구조로 더 줄이는 방안을 검토합니다.

## 2026-07-02 model-history compact list pass

- 점검 결과: 모델 이력 리스트 row가 `제목/결정/실행 상세/전체 지표`를 모두 반복해 보여 주고 있었습니다. 바로 아래에 선택 상세 카드가 이미 있으므로 같은 정보가 중복되고, 오른쪽 패널에서 주요 버튼과 선택 상세가 밀렸습니다.
- 수정 내용:
  - `ModelRegistryHistoryItems` 리스트 row를 `모델 요약 + 결정 상태` 한 줄 구조로 줄였습니다.
  - `DetailText`와 `MetricText`는 리스트 row에서 제거하고, 기존 `SelectedModelHistoryDetailText`, `SelectedModelHistoryMetricText`, `SelectedModelHistoryComparisonMetricText` 선택 상세 영역에만 남겼습니다.
  - 이력 리스트에 `MaxHeight=104`와 세로 스크롤을 적용해 이력이 늘어도 선택 상세 카드가 계속 보이도록 했습니다.
  - 리스트 row에는 `ModelRegistryHistoryItemSummaryText`, `ModelRegistryHistoryItemDecisionText` automation id를 추가했습니다.
- 구조:
  - XAML은 기존 `WpfModelRegistryHistoryItem`과 `ShellViewModel.SelectedModelRegistryHistoryItem` binding만 사용합니다.
  - 상태/선택/적용 명령은 기존 ViewModel에 그대로 있습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-history-compact-list-after-1920.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-history-compact-list-after-1366.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-metrics-compact-after-1920.png`
  - After 1920: `artifacts\ui\wpf-model-history-compact-list-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-history-compact-list-after-1366.png`
- 다음 작업: 1366 화면에서는 이력 row는 줄었지만 그 위의 모델 레지스트리 설명이 여전히 길어 이력/선택 상세가 아래로 밀립니다. 다음 pass에서는 모델 레지스트리 summary를 접힌 상세 또는 핵심 2줄 구조로 줄입니다.

## 2026-07-02 model-registry compact summary pass

- 점검 결과: `모델 레지스트리` 영역이 프로필, 학습 실행, 후보 모델, 현재 검사 모델, 다음 구조 설명을 모두 펼쳐 보여 주면서 1366 화면에서 최근 이력과 선택 상세가 아래로 밀렸습니다.
- 수정 내용:
  - `WpfModelRegistryPresentationService`에 `SummaryPrimaryText`, `SummarySecondaryText`를 추가했습니다.
  - 첫 줄은 `현재 검사 모델 / 학습 후보 모델`, 둘째 줄은 `모델 계열 / 최근 학습 / 이력 수 / recipe 저장 상태`만 보입니다.
  - 기존 상세 5줄(`Profile`, `TrainingRun`, `Candidate`, `Inspection`, `Action`)은 `ModelRegistryDetailExpander` 안으로 이동했고 기본은 접힘 상태입니다.
  - 기존 상세 TextBlock AutomationId와 binding은 그대로 유지해 상세 확인과 자동화 검증은 계속 가능합니다.
- 구조:
  - 표시 문장 생성은 presentation service가 담당합니다.
  - Shell ViewModel은 compact summary 상태를 노출하고, XAML은 binding만 사용합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-registry-summary-collapsed-after-1366.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-registry-summary-collapsed-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-model-history-compact-list-after-1366.png`
  - After 1366: `artifacts\ui\wpf-model-registry-summary-collapsed-after-1366.png`
  - After 1920: `artifacts\ui\wpf-model-registry-summary-collapsed-after-1920.png`
- 다음 작업: 모델 센터 선택 상세 카드가 아직 카드 안에 비교 카드가 중첩된 구조라 시선이 무겁습니다. 다음 pass에서는 선택 상세의 현재/선택 모델 비교를 더 얇은 2열 요약 행으로 줄입니다.

## 2026-07-02 selected-model-history thin comparison pass

- 점검 결과: 모델 이력에서 항목을 선택하면 선택 상세 카드 안에 다시 비교 카드가 들어가 있었습니다. 오른쪽 패널 폭이 좁은 상황에서는 카드 안 카드 구조가 시선을 무겁게 만들고, 비교 문장이 높이를 많이 차지했습니다.
- 수정 내용:
  - `SelectedModelHistoryComparisonPanel`을 `Border` 기반 중첩 카드에서 얇은 `Grid` 비교 행으로 변경했습니다.
  - 현재 검사 모델과 선택 모델은 2열 요약 행으로 표시하고, 긴 경로는 ellipsis와 tooltip으로 처리합니다.
  - 지표 비교도 한 줄 ellipsis로 표시해 선택 상세 카드 높이를 줄였습니다.
  - 기존 AutomationId와 ViewModel binding은 유지했습니다.
- 구조:
  - ViewModel 상태와 명령은 변경하지 않았습니다.
  - XAML은 기존 `SelectedModelHistory*` binding만 사용합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-selected-history-thin-after-1366.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-selected-history-thin-after-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-registry-summary-collapsed-after-1920.png`
  - After 1920: `artifacts\ui\wpf-model-selected-history-thin-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-selected-history-thin-after-1366.png`
- 다음 작업: 모델 센터 하단의 `모델 적용 판단` 카드가 아직 길고, 현재 선택 후보 저장/검사 흐름과 일부 중복됩니다. 다음 pass에서는 적용 판단을 기본 1줄 판단 + 접힌 근거로 줄입니다.

## 2026-07-02 model-adoption decision collapsed-detail pass

- 점검 결과: `모델 적용 판단` 카드가 판단 요약, 근거, 저장 설명을 모두 펼쳐 보여 주면서 위쪽 `모델 적용 순서` 카드와 역할이 일부 중복됐습니다. 기본 화면에서는 지금 적용해야 하는 판단만 먼저 보이면 됩니다.
- 수정 내용:
  - `YoloModelAdoptionDecisionSummaryText`는 한 줄 ellipsis로 표시합니다.
  - `YoloModelAdoptionDecisionEvidenceText`와 `YoloModelAdoptionDecisionActionText`는 `YoloModelAdoptionDecisionDetailExpander` 안으로 이동했고 기본은 접힘 상태입니다.
  - 기존 AutomationId와 ViewModel binding은 유지해, 필요하면 근거/저장 상세를 펼쳐 확인할 수 있습니다.
- 구조:
  - ViewModel 상태와 명령은 변경하지 않았습니다.
  - XAML은 기존 `ModelCenterDecision*` binding만 사용합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-decision-collapsed-after-1366.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-decision-collapsed-after-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-selected-history-thin-after-1920.png`
  - After 1920: `artifacts\ui\wpf-model-decision-collapsed-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-decision-collapsed-after-1366.png`
- 다음 작업: 모델 센터의 오른쪽 패널은 상당히 정리됐으므로, 다음 pass에서는 실제 사용자 흐름 기준으로 `후보 검증 -> 검사 모델로 저장 -> 현재 검사` 버튼의 enable/disabled 이유가 버튼 근처에서 충분히 설명되는지 점검합니다.

## 2026-07-02 model-center action-state inline pass

- 점검 결과: 모델 센터의 핵심 버튼(`후보 검증`, `검사 모델로 저장`, `현재 검사`)은 보이지만, 버튼이 비활성일 때 왜 대기인지가 툴팁이나 주변 설명을 찾아야 알 수 있었습니다. 학습/모델 단계에서는 사용자가 이 세 버튼 중 무엇을 먼저 눌러야 하는지 즉시 판단해야 합니다.
- 수정 내용:
  - `WpfLabelingShellViewModel`에 `ModelCenterActionStateText`를 추가했습니다.
  - 후보 검증, 검사 모델 저장, 현재 검사 버튼의 enabled 상태와 tooltip 사유를 한 줄 상태 문구로 요약합니다.
  - `WpfLabelingShellWindow.xaml`의 모델 적용 순서 버튼 바로 아래에 `ModelCenterPriorityButtonStateText`를 배치했습니다.
  - 후보 없음, 저장할 후보 없음, 검사 모델 저장 필요, 현재 이미지/모델 확인 같은 대기 사유를 버튼 근처에서 볼 수 있게 했습니다.
- 구조:
  - 상태 판단은 Shell ViewModel에서 계산합니다.
  - XAML은 `ShellViewModel.ModelCenterActionStateText` 바인딩만 표시합니다.
  - View code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output .\artifacts\ui\wpf-model-actions-state-after-1366.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-actions-state-after-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-decision-collapsed-after-1920.png`
  - After 1920: `artifacts\ui\wpf-model-actions-state-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-actions-state-after-1366.png`
- 다음 작업: 모델 센터의 구조 정리는 중복 없이 충분히 진행됐으므로, 다음 pass에서는 학습 완료 후 `검사 모델로 저장`과 `현재 검사`가 실제 사용자 데이터셋 흐름에서 어떻게 이어지는지, 결과 비교/모델 비교 화면의 전환이 자연스러운지 점검합니다.

## 2026-07-02 optional model-runtime pass

- 점검 결과: 앱이 YOLOv5가 로컬에 설치되어 있다는 전제에 너무 많이 기대고 있었습니다. 사용자가 프로그램만 받은 상태에서는 라벨링은 할 수 있어야 하지만, 학습/현재 검사는 모델 실행기 설치 또는 경로 연결이 필요하다는 점이 명확히 보여야 합니다.
- 수정 내용:
  - `PythonModelRuntimeState`를 추가해 모델 실행기 상태를 `미설치 / 설정 확인 필요 / 준비 완료`로 분리했습니다.
  - 모델 실행기 미설치 상태에서는 라벨링 UI는 계속 열리고, 학습 시작/현재 검사/모델 테스트/재시작 같은 모델 실행 명령만 비활성 또는 guard 처리됩니다.
  - 미설치 상태가 상단 상태줄, 학습/모델 센터, 모델 레지스트리, 학습 완료 후 액션 카드에서 같은 메시지로 보이도록 맞췄습니다.
  - 사용자가 지정한 커스텀 모델 실행기 경로가 비어 있거나 준비되지 않았을 때 `C:\Git\yolov5`로 조용히 바뀌지 않도록 자동 복구 범위를 줄였습니다.
  - 모델 프로필 선택에 `YOLO11`을 추가했습니다. 아직 실행 어댑터 설치가 없어도 같은 라벨 데이터셋으로 나중에 YOLOv8/YOLO11을 연결할 수 있는 선택지입니다.
- 구조:
  - 런타임 판정은 `PythonModelSettingsValidator`가 담당합니다.
  - 명령 가능/불가능 상태는 `WpfWorkflowCommandStateService`가 계산하고, ViewModel은 그 상태를 표시합니다.
  - Shell code-behind는 기존처럼 장기 실행 명령과 ViewModel fan-out adapter 역할만 합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --model-registry` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-optional-before-1920.png` passed.
  - `dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-missing-after-1920.png` passed.
  - `git diff --check` on the changed runtime/UI/test files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. This failure is outside the model-runtime optional path and remains a separate item.
- 캡처:
  - Runtime available comparison: `artifacts\ui\wpf-model-runtime-optional-before-1920.png`
  - Runtime missing after: `artifacts\ui\wpf-model-runtime-missing-after-1920.png`
- 다음 작업: 모델 실행기 설치/연결 센터를 별도 UX로 설계합니다. YOLOv5 외부 repo 연결, Ultralytics 기반 YOLOv8/YOLO11 venv 설치, 기존 경로 연결, self-test를 한 흐름으로 묶어야 합니다.

## 2026-07-02 model-runtime profile list pass

- 점검 결과: 모델 실행기가 없을 때 라벨링은 가능하다는 메시지는 보이기 시작했지만, 사용자는 여전히 `YOLOv5`, `YOLOv8`, `YOLO11`, `ONNX` 중 무엇이 현재 선택됐고 무엇을 설치하거나 연결해야 하는지 한눈에 알기 어려웠습니다. 특히 오픈소스/포트폴리오 배포 관점에서는 YOLOv5를 미리 받아둔 개발 PC가 아니어도 앱을 켤 수 있어야 합니다.
- 수정 내용:
  - `PythonModelRuntimeProfileService`를 추가해 모델 실행기 후보를 `YOLOv5 repo`, `Ultralytics`, `ONNX Runtime` 계열로 분리했습니다.
  - YOLO 설정 패널에 `모델 실행기 연결 상태` 목록을 추가했습니다. 이제 `YOLOv5`, `YOLOv8`, `YOLO11`, `ONNX`가 같은 카드 안에 보이고, 현재 선택된 엔진은 `선택됨 / 학습·검사 가능`, `선택됨 / 설정 확인 필요`, `선택됨 / 실행기 미설치`처럼 validator 결과를 그대로 보여줍니다.
  - 선택되지 않은 엔진도 `YOLOv5 폴더 연결 대기`, `Ultralytics 설치/연결 대기`, `ONNX 추론 연결 대기`처럼 다음 행동을 알 수 있게 정리했습니다.
  - `WpfYoloModelSettingsPanelViewModel`이 프로필 목록을 만들고, XAML은 `RuntimeProfileItems`만 바인딩합니다. View code-behind에 실행기 판단 로직을 넣지 않았습니다.
- 구조:
  - 실행기 상태 판정은 기존 `PythonModelSettingsValidator`를 재사용합니다.
  - 프로필 목록 구성은 `PythonModelRuntimeProfileService`가 담당합니다.
  - WPF 패널은 ViewModel 컬렉션을 표시하는 역할만 합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-profile-after-1920.png` passed.
  - `git diff --check` on the changed runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. The new model-runtime profile tests are covered by the focused settings-panel gates above.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-profile-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-profile-after-1920.png`
- 다음 작업: 이 프로필 목록을 실제 설치/연결 액션으로 연결합니다. 우선순위는 `기존 YOLOv5 repo 연결`, `Ultralytics venv 생성/설치`, `YOLOv8/YOLO11 self-test`, `검사 모델 선택 저장` 순서입니다.

## 2026-07-02 model-runtime profile action pass

- 점검 결과: `YOLOv5 / YOLOv8 / YOLO11 / ONNX` 실행기 상태가 보이기 시작했지만, 목록이 읽기 전용이라 사용자가 어떤 모델을 선택했는지, 선택 후 다음 작업이 무엇인지 바로 확인하기 어려웠습니다.
- 수정 내용:
  - `PythonModelRuntimeProfile`에 `PrimaryActionText`를 추가했습니다. 선택되지 않은 프로필은 `선택`, 선택됐지만 실행기가 준비되지 않은 프로필은 `연결`, 준비 완료 프로필은 `확인`으로 표시됩니다.
  - `WpfYoloModelSettingsPanelViewModel`에 `RuntimeProfileActionCommand`, `RuntimeProfileActionStatusText`, `IsRuntimeProfileActionEnabled`를 추가했습니다.
  - 프로필 row의 버튼을 누르면 해당 모델 엔진이 선택되고, 패널 하단에 `Ultralytics 실행기 설치/연결`, `.onnx 모델 선택`, `YOLOv5 폴더 연결` 같은 다음 작업 안내가 즉시 표시됩니다.
  - 장기 실행 중에는 모델 경로 편집 버튼과 같이 프로필 action도 비활성화됩니다.
- 구조:
  - 실행기 row 상태는 Core의 `PythonModelRuntimeProfileService`가 만들고, 클릭 동작은 ViewModel command가 처리합니다.
  - XAML DataTemplate은 `RuntimeProfileActionCommand`와 `PrimaryActionText`만 바인딩합니다.
  - Shell code-behind나 Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-action-after-1920.png` passed.
  - `git diff --check` on the changed runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-action-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-action-after-1920.png`
- 다음 작업: 버튼을 실제 설치/연결 마법사로 확장합니다. 먼저 `YOLOv5 폴더 연결`은 폴더 선택과 self-test까지 묶고, `YOLOv8/YOLO11`은 Ultralytics venv 생성/패키지 설치/버전 확인을 별도 서비스로 분리합니다.

## 2026-07-02 model-runtime profile connect adapter pass

- 점검 결과: 실행기 row의 `선택/연결` 버튼은 생겼지만, 버튼을 누른 뒤 사용자가 경로 입력란을 직접 찾아야 하면 연결 흐름이 다시 끊깁니다.
- 수정 내용:
  - `WpfLabelingShellWindow.PanelWiring.SettingsPanels`에서 `RuntimeProfileActionCommand`에 shell adapter를 연결했습니다.
  - `ExecuteRuntimeProfileActionCommand`를 추가해 프로필 버튼 클릭 시 고급 실행 환경 영역을 열고, 모델 계열에 맞는 입력란으로 포커스를 이동합니다.
  - `YOLOv5`는 프로젝트 폴더 입력란, `YOLOv8/YOLO11`은 Python 실행 파일 입력란, `ONNX`는 검사 모델 파일 입력란으로 이동합니다.
  - 상태줄과 로그에도 선택한 모델 계열과 다음 연결 작업을 남기도록 했습니다.
- 구조:
  - 선택/상태 텍스트는 여전히 `WpfYoloModelSettingsPanelViewModel`이 담당합니다.
  - shell code-behind는 기존 파일/폴더 picker와 focus 이동을 담당하는 UI adapter 역할만 합니다.
  - 외부 설치 실행, pip install, repo clone은 아직 수행하지 않습니다. 다음 pass에서 별도 서비스로 분리합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-connect-after-1920.png` passed.
  - `git diff --check` on the changed runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-connect-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-connect-after-1920.png`
- 다음 작업: 실제 설치/점검 흐름을 별도 서비스로 만듭니다. 첫 범위는 외부 작업 없이 `YOLOv5 폴더 self-test`부터 시작해, 선택된 폴더가 학습/추론에 필요한 파일을 갖췄는지 UI에서 즉시 보여주는 것입니다.

## 2026-07-02 model-runtime self-test pass

- 점검 결과: 모델 실행기 연결 버튼을 눌러도 사용자는 여전히 `무엇이 준비됐고 무엇이 빠졌는지`를 직접 추론해야 했습니다. 특히 YOLOv5 repo를 이미 갖고 있는 개발 PC와, 앱만 처음 받은 PC의 상태 차이가 화면에 충분히 드러나지 않았습니다.
- 수정 내용:
  - `PythonModelRuntimeSelfTestService`를 추가했습니다.
  - 선택된 모델 실행기에 대해 프로젝트 폴더, YOLOv5 모델 루트, Python 실행 파일, 실행 스크립트, 검사 모델 파일, 이미지 폴더를 항목별로 점검합니다.
  - 학습은 가능하지만 검사 모델 파일이 없어 현재 검사는 불가능한 상태를 warning으로 분리했습니다.
  - YOLO 설정 패널에 `선택 실행기 점검` 요약과 체크 리스트를 추가했습니다. 이제 사용자는 로그를 보지 않아도 `학습 가능 / 검사 가능 / 누락 항목`을 오른쪽 패널에서 바로 볼 수 있습니다.
  - 새 focused gate `--python-model-runtime-self-test`를 추가해 전체 회귀가 기존 템플릿 테스트에서 중단돼도 이 서비스만 별도로 검증할 수 있게 했습니다.
- 구조:
  - 경로/파일 판정은 Core의 `PythonModelRuntimeSelfTestService`가 담당합니다.
  - `WpfYoloModelSettingsPanelViewModel`은 self-test report를 `RuntimeSelfTestItems`와 요약 텍스트로 노출합니다.
  - XAML은 ViewModel 바인딩만 사용합니다.
  - 외부 설치, pip install, git clone, Python 프로세스 실행은 아직 수행하지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-selftest-after-1920.png` passed.
  - `git diff --check` on the changed self-test/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-selftest-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-selftest-after-1920.png`
- 다음 작업: self-test를 실제 연결 action과 합칩니다. 우선 `YOLOv5 폴더 연결` 버튼을 누르면 폴더 선택 -> self-test refresh -> 저장 안내까지 한 흐름으로 이어지게 합니다.

## 2026-07-02 YOLOv5 runtime folder connection pass

- 점검 결과: `YOLOv5` row가 self-test 상태를 보여주기 시작했지만, 사용자가 실제 폴더를 연결하려면 여전히 고급 설정을 열고 여러 경로를 직접 맞춰야 했습니다. 연결 action은 폴더 선택 후 바로 점검 결과가 바뀌어야 합니다.
- 수정 내용:
  - `PythonModelRuntimeConnectionService`를 추가했습니다.
  - `YOLOv5` 연결 대상 폴더에서 프로젝트 root, `yolov5Master`, `.venv\Scripts\python.exe`, `labelling_tcp_client.py`, `best.pt`, `data\train\images`를 찾아 설정 후보로 채웁니다.
  - 사용자가 `yolov5Master` 폴더를 직접 선택해도 상위 YOLOv5 프로젝트 폴더를 프로젝트 root로 잡도록 했습니다.
  - `WpfYoloModelSettingsPanelViewModel.ApplyRuntimeConnectionResult`를 추가해 연결 결과 적용과 self-test refresh를 ViewModel 안에서 처리합니다.
  - shell adapter의 `YOLOv5` 프로필 action은 이제 폴더 선택 -> Core 연결 서비스 -> ViewModel 적용 -> 상태/로그 안내 순서로 동작합니다.
  - `YOLOv5` row의 primary action 문구를 `선택`에서 `연결`로 바꿨습니다.
- 구조:
  - 폴더 구조 판정과 후보 경로 계산은 Core의 `PythonModelRuntimeConnectionService`가 담당합니다.
  - ViewModel은 연결 결과를 받아 화면 상태를 갱신합니다.
  - shell code-behind는 폴더 picker, focus, status/log adapter 역할만 합니다.
  - 외부 설치, pip install, git clone, Python 실행은 아직 수행하지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-yolov5-connect-after-1920.png` passed.
  - `git diff --check` on the changed runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-yolov5-connect-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-yolov5-connect-after-1920.png`
- 다음 작업: Ultralytics 계열도 같은 구조로 확장합니다. 우선 YOLOv8/YOLO11은 설치를 바로 실행하지 않고, Python 실행 파일 또는 venv 폴더 연결 -> `ultralytics` 패키지 존재 여부 self-test -> 설치 필요 상태 표시까지 구현합니다.

## 2026-07-02 Ultralytics runtime connection/self-test pass

- 점검 결과: `YOLOv8`과 `YOLO11`은 프로필 row에는 보였지만, 버튼을 눌러도 연결된 Python이 실제로 Ultralytics 실행 환경인지 사용자가 확인할 수 없었습니다. 설치를 바로 실행하기 전에 `기존 venv 연결 -> ultralytics 패키지 확인 -> 설치 필요 상태 표시`가 먼저 필요했습니다.
- 수정 내용:
  - `PythonModelRuntimeSelfTestService`가 `YOLOv8`/`YOLO11` 선택 시 venv의 `Lib\site-packages`에서 `ultralytics` 패키지 또는 `ultralytics-*.dist-info`를 확인하도록 확장했습니다.
  - `PythonModelRuntimeConnectionService.BuildUltralyticsPythonConnection`을 추가해 Python 실행 파일 또는 venv/Scripts 폴더를 연결하고, 곧바로 self-test report를 만들도록 했습니다.
  - `YOLOv8`과 `YOLO11` 프로필 row의 primary action을 `연결`로 정리했습니다.
  - shell adapter에서 `YOLOv8`/`YOLO11` 프로필 action을 Python 선택 창 -> Core 연결 서비스 -> ViewModel `ApplyRuntimeConnectionResult` -> 상태/로그 안내 흐름으로 연결했습니다.
  - 연결된 Python에 Ultralytics가 있으면 `Ultralytics 실행기 연결 확인`, 없으면 `Ultralytics 설치 필요`로 보입니다.
- 구조:
  - 패키지/경로 판정은 Core service가 담당합니다.
  - ViewModel은 기존 `ApplyRuntimeConnectionResult`와 self-test 바인딩으로 화면 상태를 갱신합니다.
  - shell code-behind는 Python picker, focus, status/log adapter 역할만 합니다.
  - 이번 pass에서는 외부 설치, `pip install`, Python 실행, git clone을 수행하지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - 1920x1080 visual smoke capture generated: `artifacts\ui\wpf-model-runtime-ultralytics-connect-after-1920.png`.
  - `git diff --check` on the changed Ultralytics runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. The new Ultralytics runtime connection/self-test gates passed independently.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-ultralytics-connect-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-ultralytics-connect-after-1920.png`
- 다음 작업: 설치 필요 상태에서 바로 `pip`를 실행하지 않고, 설치 대상 venv, 설치 명령, 예상 변경사항을 먼저 보여주는 안전한 Ultralytics install action/service를 만듭니다.

## 2026-07-02 Ultralytics install plan preview pass

- 점검 결과: `Ultralytics 설치 필요` 상태는 보이지만, 사용자는 어떤 venv에 어떤 명령이 들어갈지 알 수 없었습니다. 바로 설치 버튼을 붙이면 사용자 환경을 바꾸는 동작이 너무 갑작스럽기 때문에, 먼저 설치 대상과 명령을 읽기 전용으로 보여주는 단계가 필요했습니다.
- 수정 내용:
  - `PythonModelRuntimeInstallPlanService`를 추가했습니다.
  - `YOLOv8`/`YOLO11` 선택 시 venv root, `ultralytics` 설치 여부, 미리보기 명령을 계산합니다.
  - 패키지가 없으면 `python.exe -m pip install --upgrade ultralytics`, 이미 있으면 `python.exe -m pip show ultralytics`를 미리보기로 보여줍니다.
  - YOLO 설정 패널의 `모델 실행기 연결 상태` 영역 안에 `YOLO11 Ultralytics 설치 전 확인` 패널을 추가했습니다.
  - 1920x1080 visual smoke fixture는 fake venv를 연결하되 `ultralytics`는 없는 상태로 만들어, 설치 필요/대상/명령이 첫 화면에 보이도록 했습니다.
- 구조:
  - 설치 계획 계산은 Core의 `PythonModelRuntimeInstallPlanService`가 담당합니다.
  - `WpfYoloModelSettingsPanelViewModel`은 `RuntimeInstallPlan*` 속성으로 읽기 전용 상태를 노출합니다.
  - XAML은 바인딩만 사용합니다.
  - 이번 pass에서도 실제 `pip install`, Python 실행, git clone은 수행하지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-install-plan-after-1920.png` passed.
  - `git diff --check` on the changed install-plan/runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. This pass's focused gates passed before that failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-install-plan-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-install-plan-after-1920.png`
- 다음 작업: 이 preview를 실제 action으로 확장합니다. 다음 범위는 설치 실행 전 확인 dialog/service, stdout/stderr 진행 상태, 실패 사유 표시, 설치 후 self-test 자동 재실행입니다.

## 2026-07-02 Ultralytics install/uninstall action pass

- 점검 결과: 설치 명령 미리보기는 보이지만, 사용자가 테스트를 반복하려면 `ultralytics`를 제거하는 방법도 같은 화면에 있어야 했습니다. 제거가 설치 버튼 안에 섞이면 위험하므로 별도 옵션으로 분리했습니다.
- 수정 내용:
  - `PythonEnvironmentService.InstallPackageAsync`와 `UninstallPackageAsync`를 추가했습니다.
  - `PythonModelRuntimeInstallPlanService`가 설치 명령과 제거 명령을 따로 계산하고, 실행 가능 여부를 ViewModel에 전달합니다.
  - YOLO/model 설정 패널의 install plan 카드에 `설치 실행`과 `제거` 버튼을 추가했습니다.
  - 1920x1080 화면에서 버튼이 하단에 밀리지 않도록 install plan 카드 상단에 배치했습니다.
  - Shell adapter는 버튼 클릭 시 현재 ViewModel 설정 snapshot을 사용해 `ultralytics`만 설치/제거하고, stdout/stderr tail과 결과를 로그/상태에 남깁니다.
- 구조:
  - pip 실행은 Core의 `PythonEnvironmentService`가 담당합니다.
  - `WpfYoloModelSettingsPanelViewModel`은 command/state/status만 노출합니다.
  - shell code-behind는 UI adapter로서 command 시작/종료, 로그, status 갱신만 담당합니다.
  - 테스트는 실제 pip install/uninstall을 실행하지 않고, 명령 생성/버튼 바인딩/adapter 연결을 검증합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-install-uninstall-after-1920.png` passed.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. The model-runtime install/uninstall gates passed before that failure.
  - `git diff --check` on the changed runtime/UI/test/doc files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-install-uninstall-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-install-uninstall-after-1920.png`
- 다음 작업: 설치/제거 실행 전에 확인 dialog를 추가하고, 설치가 끝난 뒤 self-test와 install plan을 자동 재점검하는 흐름을 더 명확히 만듭니다.

## 2026-07-02 Ultralytics install/uninstall confirmation pass

- 점검 결과: `설치 실행`과 `제거` 버튼은 분리됐지만, 버튼을 누른 순간 venv가 변경되면 사용자가 대상과 명령을 다시 확인할 기회가 없습니다. 특히 `제거`는 테스트 반복용 옵션이므로 실행 전 확인이 필요했습니다.
- 수정 내용:
  - `ExecuteUltralyticsPackageCommandAsync`가 먼저 install plan을 만들고, 실행 가능 상태와 확인 dialog를 통과한 뒤에만 package command를 시작하도록 바꿨습니다.
  - 확인 dialog는 재사용 가능한 `OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialog.Confirm`을 사용합니다. stock `MessageBox.Show`는 쓰지 않습니다.
  - dialog에는 대상 venv, 실제 실행 명령, 실행 후 self-test/install 상태를 다시 확인한다는 문구를 표시합니다.
  - 사용자가 취소하면 상태/로그에 취소 사유를 남기고 venv를 변경하지 않습니다.
  - 설치/제거 버튼 아래에 `대상 venv와 명령을 확인한 뒤 실행`한다는 힌트를 추가했습니다.
  - 설치/제거 성공 후 상태 문구를 `Self-test를 다시 확인했습니다`로 바꿔, 실행 후 패널 상태가 갱신된다는 점을 명확히 했습니다.
- 구조:
  - package command 실행은 계속 Core의 `PythonEnvironmentService`가 담당합니다.
  - ViewModel은 버튼 command/state/status를 노출합니다.
  - shell code-behind는 dialog 표시, command lifecycle, status/log adapter만 담당합니다.
  - 확인 dialog를 자동 테스트에서 실제 클릭하지는 않고, static/source gate와 패널 binding gate로 검증합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-confirm-after-1920.png` passed.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. This pass's focused gates passed before that failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-confirm-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-confirm-after-1920.png`
- 다음 작업: 설치/제거 확인 dialog 다음 단계로, 실행 결과를 별도 `최근 실행 결과` 카드에 남겨 stdout/stderr 요약과 마지막 성공/실패 시간을 패널에서 바로 확인하게 만듭니다.

## 2026-07-02 Ultralytics package recent-result card pass

- 점검 결과: 설치/제거는 확인 dialog와 하단 로그에만 결과가 남았습니다. 사용자가 로그를 열지 않으면 마지막 실행이 성공했는지, 취소했는지, 어떤 venv/명령으로 실행했는지 다시 확인하기 어렵습니다.
- 수정 내용:
  - `WpfYoloModelSettingsPanelViewModel`에 `RuntimePackageResultTitleText`, `RuntimePackageResultSummaryText`, `RuntimePackageResultDetailText`, `SetRuntimePackageOperationResult`를 추가했습니다.
  - YOLO/model 설정 패널의 Ultralytics install plan 카드 안에 `최근 실행 결과` 카드를 추가했습니다.
  - 기본 상태는 `아직 실행 기록 없음`으로 표시됩니다.
  - 설치/제거 성공, 실패, 취소 시 Shell adapter가 마지막 시간, 대상 venv, 명령, ExitCode, 상태, stdout/stderr 첫 줄을 ViewModel 카드로 전달합니다.
  - 1920x1080에서 카드 제목과 요약이 잘리지 않도록 첫 줄에 `최근 실행 결과`와 요약을 같이 배치했습니다.
- 구조:
  - 결과 상태는 ViewModel 속성으로 노출하고 XAML은 바인딩만 사용합니다.
  - package 실행은 계속 `PythonEnvironmentService`가 담당합니다.
  - shell code-behind는 결과 요약을 만드는 UI adapter 역할만 합니다.
  - 실제 pip install/uninstall은 자동 테스트에서 실행하지 않습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-result-card-after-1920.png` passed.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure. This pass's focused gates passed before that failure.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-result-card-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-result-card-after-1920.png`
- 다음 작업: 설치/제거 결과 카드 다음 단계로, YOLOv8/YOLO11 adapter가 실제 학습/추론 요청에서 어떤 실행기를 쓰는지 모델 센터 상단의 `현재 검사 모델/실행기` 요약에 연결합니다.

## 2026-07-02 Model center runtime summary pass

- 점검 결과: 모델 센터는 현재 검사 모델과 학습 후보 모델은 보여줬지만, 그 모델을 어떤 실행기/어댑터로 검사하는지 첫 화면에서 바로 확인하기 어려웠습니다. 사용자는 `best.pt`가 보이더라도 YOLOv5 repo인지, YOLO11 Ultralytics인지 따로 YOLO 설정 탭을 열어 확인해야 했습니다.
- 수정 내용:
  - `WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText`를 추가해 선택된 런타임을 `YOLOv5 repo / 검사 가능`, `YOLO11 Ultralytics / 설치/연결 필요`처럼 한 줄로 요약합니다.
  - 모델 레지스트리의 현재 검사 모델 행에 `실행기 ...` 정보를 붙였습니다.
  - 모델 센터 compact summary도 같은 런타임 요약을 사용하게 바꿨습니다.
  - 모델 센터의 현재 검사 모델 상태(`SetModelCenterModelState`로 전달되는 문구)에도 런타임 요약을 포함해 버튼 tooltip/학습 워크플로우 상태와 일관되게 했습니다.
- 구조:
  - 런타임 프로필 판정은 기존 Core `PythonModelRuntimeProfileService`와 `PythonModelSettingsValidator` 결과를 재사용합니다.
  - UI 문구 조합은 `WpfModelRegistryPresentationService`가 담당합니다.
  - shell code-behind는 기존 dashboard refresh에서 서비스 결과를 전달하는 adapter 역할만 합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-labeling-shell` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-runtime-summary-after-1920.png` passed.
  - `git diff --check` on the changed model-center runtime summary files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure after the model-registry and dataset/Yolo gates pass.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-runtime-summary-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-center-runtime-summary-after-1920.png`
- 다음 작업: 모델 런타임 센터의 프로필 선택을 실제 학습/추론 adapter 선택과 더 명확히 연결합니다. 특히 YOLOv8/YOLO11 선택 시 "학습은 어떤 실행 명령으로 나가는지", "현재 검사는 어떤 adapter로 나가는지"를 self-test 아래에 별도 실행 경로 요약으로 노출합니다.

## 2026-07-02 Runtime execution route summary pass

- 점검 결과: 런타임 프로필과 self-test는 보이지만, 사용자는 실제로 학습/검사 버튼을 눌렀을 때 어떤 Python worker, 어떤 TCP 요청, 어떤 adapter key로 나가는지 한눈에 확인하기 어려웠습니다. 특히 YOLO11 선택 시 `YOLO11`이 선택됐다는 사실과 실제 `DetectImage(model=yolo11)` 요청이 연결되어 보이지 않았습니다.
- 수정 내용:
  - `PythonModelRuntimeExecutionSummaryService`를 추가했습니다.
  - 실행 경로를 `Worker`, `학습`, `현재 검사` 3줄로 분리했습니다.
  - Worker 줄은 Python 실행 파일, TCP client script, 작업 폴더를 보여줍니다.
  - 학습 줄은 현재 구조에 맞게 `TCP StartTraining -> 모델 루트 / data.yaml + 학습 설정`으로 표시합니다.
  - 현재 검사 줄은 `TCP DetectImage(model=yolo11)`처럼 실제 adapter key와 검사 가중치/이미지 경로를 표시합니다.
  - YOLO/model 설정 패널의 런타임 프로필 목록 아래에 읽기 전용 `실제 학습/검사 실행 경로` 카드를 추가했습니다.
- 구조:
  - 실행 경로 계산은 Core service가 담당합니다.
  - `WpfYoloModelSettingsPanelViewModel`은 `RuntimeExecution*` 속성만 노출합니다.
  - XAML은 바인딩만 사용하며, 경로가 길어지는 값은 한 줄로 줄이고 tooltip으로 전체 값을 보게 했습니다.
  - shell code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output .\artifacts\ui\wpf-model-runtime-execution-route-after-1920.png` passed.
  - `git diff --check` on the changed execution-route files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure after the earlier gates pass.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-runtime-execution-route-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-runtime-execution-route-after-1920.png`
- 다음 작업: 현재 worker 구조상 YOLOv8/YOLO11 학습은 아직 YOLOv5와 같은 `StartTraining` 패킷 표시만 갖고 있습니다. 다음 단계는 Python worker protocol/adapter 쪽에서 YOLOv8/YOLO11 학습 요청에 필요한 engine key가 명시적으로 전달되는지 확인하고, 필요하면 protocol에 안전하게 확장합니다.

## 2026-07-02 Training adapter key + YOLOv5 inference smoke pass

- 점검 결과: 검사 요청은 이미 `DetectImage(model=...)`로 adapter key를 worker에 전달했지만, 학습 요청은 `StartTraining` legacy command 안의 payload에 선택 모델 정보가 없었습니다. 이 상태에서는 YOLOv8/YOLO11 프로필을 선택해도 학습 패킷만 보면 어떤 adapter 의도로 시작했는지 추적하기 어렵습니다.
- Python worker 확인:
  - `C:\Git\yolov5\labelling_tcp_client.py`는 호환용 wrapper이고 실제 구현은 `C:\Git\yolov5\labeling_tcp_client.py`입니다.
  - worker는 legacy `StartTraining`을 내부 `TrainYolo`로 매핑합니다.
  - 학습 command 생성은 payload dict에서 필요한 키만 `get_first(...)`로 읽기 때문에, C# payload에 `model`을 추가해도 기존 YOLOv5 worker와 호환됩니다.
- 수정 내용:
  - `LearningProtocol.BuildTrainingPacket`에 optional `model` 인자를 추가하고 기본값을 `yolov5`로 유지했습니다.
  - `YoloTrainingRequest` payload에 `model` 필드를 추가했습니다.
  - `CCommunicationLearning.SendTrainingData`가 model key를 받을 수 있게 확장했습니다.
  - `YoloTrainingWorkflowService.TryStartTraining`이 `data.ProjectSettings.PythonModel.GetProtocolModelName()` 값을 학습 패킷으로 전달합니다.
  - 실행 경로 카드의 학습 줄을 `TCP StartTraining(model=yolo11)` 형식으로 바꿔, 학습과 검사 모두 선택 adapter key가 보이게 했습니다.
  - focused tests는 기본 학습 패킷 `model=yolov5`, 명시 학습 패킷 `model=yolo11`, WPF 학습 session mock worker 수신 패킷의 `model=yolov5`, 실행 경로 summary의 `StartTraining(model=yolo11)`를 확인합니다.
- 구조:
  - adapter key 결정은 기존 `PythonModelSettings.GetProtocolModelName()`을 재사용합니다.
  - 패킷 생성은 `LearningProtocol`, workflow 전달은 `YoloTrainingWorkflowService`, 화면 요약은 `PythonModelRuntimeExecutionSummaryService`가 담당합니다.
  - WPF code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-yolo-training-model-key-after-1920.png` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output artifacts\ui\wpf-yolo-training-model-key-after-settings-1920.png` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --real-yolo-smoke` passed. Actual YOLOv5 route: `C:\Git\yolov5\.venv\Scripts\python.exe`, `C:\Git\yolov5\labelling_tcp_client.py`, `C:\Git\yolov5\best.pt`, image `Teaching_0.jpeg`; candidateCount=1, committedCount=1.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure at `tests\LabelingApplication.Tests\Program.cs:10798`.
  - `git diff --check` on the changed protocol/workflow/test files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 캡처/산출물:
  - Before 1920: `artifacts\ui\wpf-yolo-training-model-key-before-1920.png`
  - After settings 1920: `artifacts\ui\wpf-yolo-training-model-key-after-settings-1920.png`
  - Training session 1920: `artifacts\ui\wpf-yolo-training-model-key-after-1920.png`
  - Real YOLOv5 smoke summary: `artifacts\real-yolo-smoke\20260702-204427\summary.txt`
- 다음 작업: YOLOv8/YOLO11은 이제 학습 패킷에 adapter key가 실립니다. 다음 단계는 Ultralytics-family worker/adapter가 `model=yolov8/yolo11` 학습 요청을 받아 YOLOv5 `train.py`가 아닌 Ultralytics 실행 경로로 분기할 수 있게 별도 service/worker contract를 설계하고, 그 전까지는 UI에서 YOLOv8/YOLO11 학습이 "설치/연결 준비 단계"인지 "실제 실행 가능"인지 더 명확히 구분합니다.

## 2026-07-02 Ultralytics execution guard pass

- 점검 결과: 이전 패스에서 `StartTraining(model=yolo11)`과 `DetectImage(model=yolo11)` 키는 보이게 됐지만, 실제 연결된 worker는 아직 YOLOv5 TCP worker입니다. 이 상태에서 YOLO11을 `학습/검사 가능`으로 보여주면 사용자는 YOLO11로 실행했다고 믿지만 실제로는 YOLOv5 경로가 돌 수 있습니다.
- 수정 내용:
  - `PythonModelRuntimeAdapterSupportService`를 추가했습니다.
  - YOLOv5는 현재 TCP worker로 학습/검사 실행 가능 상태를 유지합니다.
  - YOLOv8/YOLO11은 설치/경로 점검과 프로필 선택은 가능하지만, 실제 실행은 `Ultralytics 실행 연결 필요`로 차단합니다.
  - ONNX도 현재는 추론 전용 profile 준비 단계로 두고, 실제 검사 실행 연결이 구현되기 전까지 실행 차단 상태로 표시합니다.
  - `PythonModelSettingsValidator.GetRuntimeState`가 adapter support를 반영해 YOLOv8/YOLO11을 `CanRunTraining=false`, `CanRunInference=false`로 반환하게 했습니다.
  - self-test에 `실행 연결` 항목을 추가해, 패키지가 설치되어 있어도 worker 실행 연결이 없으면 blocking check로 보이게 했습니다.
  - 실행 경로 카드의 YOLOv8/YOLO11 학습/검사 줄은 adapter key를 유지하되 `실행 차단`과 `YOLOv5 worker로 대체 실행하지 않음`을 표시합니다.
- 구조:
  - 실행 지원 여부는 Core service가 담당합니다.
  - ViewModel은 기존 `RuntimeSelfTest*`, `RuntimeExecution*`, `RuntimeProfileItems` 바인딩만 갱신합니다.
  - shell code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output artifacts\ui\wpf-ultralytics-execution-block-after-1920.png` passed, and screenshot review confirmed YOLO11 shows execution blocked instead of executable.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --real-yolo-smoke` passed again. Actual YOLOv5 route: `C:\Git\yolov5\.venv\Scripts\python.exe`, `C:\Git\yolov5\labelling_tcp_client.py`, `C:\Git\yolov5\best.pt`, image `Teaching_0.jpeg`; candidateCount=1, committedCount=1.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure at `tests\LabelingApplication.Tests\Program.cs:10798`.
  - `git diff --check` on the changed adapter-support files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 캡처/산출물:
  - Before 1920: `artifacts\ui\wpf-ultralytics-execution-block-before-1920.png`
  - After 1920: `artifacts\ui\wpf-ultralytics-execution-block-after-1920.png`
  - Real YOLOv5 smoke summary: `artifacts\real-yolo-smoke\20260702-205410\summary.txt`
- 다음 작업: 실제 Ultralytics worker 분기 구현입니다. `model=yolov8/yolo11` 요청을 받았을 때 별도 Python runner가 `ultralytics` package로 학습/검사를 수행하고, 결과 protocol은 기존 `DetectImageResult`/`TrainYoloResult`와 호환되게 유지하는 방향으로 진행합니다.

## 2026-07-02 Worker capability handshake pass

- 점검 결과: YOLOv8/YOLO11을 안전하게 실행하려면 앱이 `패키지 설치 여부`만 보는 것이 아니라, 현재 연결된 Python worker가 어떤 adapter를 실제로 처리할 수 있는지 알아야 합니다. 이전 패스는 YOLOv8/YOLO11 실행을 차단했지만, 실제 Ultralytics worker가 붙었을 때 그 차단을 해제할 handshake가 없었습니다.
- 수정 내용:
  - `PythonModelStatusProtocol`이 `HealthCheckResult`/`ModelStatusResult`의 capability 필드를 파싱하도록 확장했습니다.
  - 지원 schema:
    - root 또는 `capabilities` 또는 `worker.capabilities`
    - `supportedModels`/`models`/`adapters`
    - `trainingModels`/`trainModels`/`training`/`train`
    - `detectionModels`/`detectModels`/`inspectionModels`/`detect`/`inspection`
  - `PythonCommunicationStatus`에 `WorkerSupportedModels`, `WorkerTrainingModels`, `WorkerDetectionModels`를 추가했습니다.
  - `CCommunicationLearning`이 health/model status 수신 시 capability를 status snapshot에 저장합니다.
  - `PythonModelRuntimeAdapterSupportService`가 capability 목록을 받으면 `yolov8/yolo11` 실행 가능 여부를 계산하도록 확장했습니다.
  - `PythonModelSettingsValidator.GetRuntimeState`에 capability overload를 추가했습니다. capability가 없으면 기존처럼 YOLOv8/YOLO11 실행은 차단됩니다.
  - WPF shell command gating의 `GetPythonModelRuntimeState()`가 현재 Python communication status의 capability를 넘기도록 연결했습니다.
  - focused 테스트용 `--python-model-status-protocol` 스위치를 추가했습니다.
- 구조:
  - worker capability 파싱/저장은 Communication 계층에 둡니다.
  - 실행 가능 판정은 Core service/validator가 담당합니다.
  - Shell은 현재 status snapshot을 넘기는 adapter 역할만 합니다.
  - View code-behind에 YOLOv8/YOLO11 판단 로직을 직접 넣지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --mvvm-infra` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-status-protocol` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --real-yolo-smoke` passed. Actual YOLOv5 route remains unchanged: `C:\Git\yolov5\.venv\Scripts\python.exe`, `C:\Git\yolov5\labelling_tcp_client.py`, `C:\Git\yolov5\best.pt`, image `Teaching_0.jpeg`; candidateCount=1, committedCount=1.
  - Full `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug` still stops at the existing `Template auto label shows actionable guide: registered template batch should complete` failure at `tests\LabelingApplication.Tests\Program.cs:10803`.
  - `git diff --check` on the changed capability files returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 산출물:
  - Real YOLOv5 smoke summary: `artifacts\real-yolo-smoke\20260702-210425\summary.txt`
- 다음 작업: 실제 Ultralytics worker script/runner를 추가합니다. 새 worker는 health/model status에 capability를 내보내고, `model=yolov8/yolo11`의 `DetectImage`와 `StartTraining`을 `ultralytics` package로 처리해야 합니다.

## 2026-07-02 Tutorial public-doc cleanup pass

- 점검 결과: 튜토리얼/README에 특정 실습 데이터셋, 개인 PC 경로, 이전 대화에서 정한 문서 작성 지침이 사용자 가이드 본문처럼 섞여 있었습니다. standalone HTML도 이전 Test 전용 섹션을 포함하고 있어, 단일 HTML만 복사했을 때 오래된 내용이 계속 보일 수 있었습니다.
- 수정 내용:
  - `docs/tutorial/README.md`를 실제 사용자 작업 순서 중심으로 정리하고, 문서 작성 기준 섹션은 튜토리얼 본문에서 제거했습니다.
  - `docs/tutorial/labeling-workbench-tutorial.html`에서 내부 관리 문구, Test 전용 호환 문구, `최신` 반복 alt 문구를 제거했습니다.
  - `README.md`의 문서 관리 기준을 사용자에게 보이는 튜토리얼 문구가 아닌 프로젝트 문서 기준으로 정리했습니다.
  - `docs/STABLE_VERIFIED_AREAS.md`의 Test 데이터셋 전용 튜토리얼 계약을 public-document 계약으로 교체했습니다.
  - 튜토리얼 캡처 14장에서 왼쪽 이미지 폴더 입력칸과 데이터셋 예시 화면의 로그 경로 노출을 좁게 마스킹했습니다.
  - `docs/tutorial/labeling-workbench-tutorial-standalone.html`을 다시 생성해 현재 HTML의 14개 PNG를 모두 포함하도록 했습니다.
- 검증:
  - `rg -n "D:\\|C:\\|TEST_Dataset|이번 확인|사용한 데이터|제가|내가|소통|물어|artifacts\\run|Debug EXE|Test 데이터셋|README와 튜토리얼|테스트 호환|최신 실행|문서를 수정" README.md docs\tutorial\README.md docs\tutorial\labeling-workbench-tutorial.html docs\tutorial\labeling-workbench-tutorial-standalone.html` returned no matches.
  - HTML image count check: standard HTML has 14 `src="images/...` references, standalone HTML has 14 embedded `src="data:image/png;base64` images and 0 file-based image references.
  - Visual check: `01-overview`, `02-dataset-wizard`, and `03-labeling-workbench` screenshots no longer expose the local image/runtime path in the visible guide area.
  - `git diff --check -- README.md docs/tutorial/README.md docs/tutorial/labeling-workbench-tutorial.html docs/tutorial/labeling-workbench-tutorial-standalone.html docs/STABLE_VERIFIED_AREAS.md docs/WORK_TRACKING.md` returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 다음 작업: 모델 런타임 개발로 돌아가려면 실제 Ultralytics worker script/runner를 추가하고, UI에서는 YOLOv8/YOLO11이 설치/연결 전에는 실행 가능처럼 보이지 않도록 계속 막아야 합니다.

## 2026-07-02 Bundled Ultralytics worker connection pass

- 점검 결과: YOLOv8/YOLO11 Python 연결 서비스는 선택한 Python 실행 파일만 바꾸고, `ProjectRootPath`와 `ClientScriptPath`는 기존 설정을 유지했습니다. 이 상태에서는 사용자가 Ultralytics venv를 연결해도 내부 실행 경로가 이전 YOLOv5 TCP client로 남을 수 있어, 이후 추론/학습 검증 결과를 신뢰하기 어렵습니다.
- 수정 내용:
  - `PythonModelRuntimeConnectionService.BuildUltralyticsPythonConnection`이 `PythonModelRuntimeBundledWorkerService`를 통해 `Runtime\Python\openvisionlab_ultralytics_worker.py`를 project root/client script로 설정하도록 변경했습니다.
  - `MvcVisionSystem.csproj`에 번들 워커를 `CopyToOutputDirectory`/`CopyToPublishDirectory` 대상으로 등록했습니다.
  - `PythonModelRuntimeAdapterSupportService`가 operation-specific capability를 우선 해석하도록 바꿨습니다. `detectionModels=yolo11`, `trainingModels=[]`인 worker는 검사만 가능하고 학습 가능으로 보이지 않습니다. `supportedModels`만 내려주는 legacy worker 호환은 유지했습니다.
  - `openvisionlab_ultralytics_worker.py` self-test가 detection capability와 training 미지원 상태를 확인하도록 보강했습니다.
  - focused test `--python-ultralytics-worker`를 추가하고, 기존 `--python-model-runtime-connection` 테스트가 Ultralytics 연결 후 번들 워커 경로를 확인하도록 강화했습니다.
- 구조:
  - 실행 경로 결정은 Core service에 둡니다.
  - capability 해석은 Core adapter-support service에 둡니다.
  - Python worker는 현재 추론 전용입니다. 학습 요청은 `TrainingNotSupported`로 명시 실패합니다.
  - WPF code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-ultralytics-worker` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-status-protocol` passed.
  - `python Runtime\Python\openvisionlab_ultralytics_worker.py --self-test` passed.
  - `python -m py_compile Runtime\Python\openvisionlab_ultralytics_worker.py` passed.
  - `Test-Path artifacts\run\Debug\Runtime\Python\openvisionlab_ultralytics_worker.py` returned `True`.
  - `git diff --check -- '1. Core/PythonModelRuntimeConnectionService.cs' '1. Core/PythonModelRuntimeAdapterSupportService.cs' 'MvcVisionSystem.csproj' 'Runtime/Python/openvisionlab_ultralytics_worker.py' 'tests/LabelingApplication.Tests/Program.cs'` returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 다음 작업: 실제 Ultralytics 추론 smoke입니다. 테스트용 YOLOv8/YOLO11 weights와 image를 지정해 `openvisionlab_ultralytics_worker.py --smoke-test`가 `DetectImageResult` 호환 후보를 내는지 확인하고, 그 다음 WPF 현재 검사 버튼까지 연결합니다. 학습은 아직 미구현이므로 별도 training-capable worker pass로 분리합니다.

## 2026-07-02 YOLO11 Ultralytics inference smoke pass

- 점검 결과:
  - 기존 `C:\Git\yolov5\best.pt`는 Ultralytics worker에서 로드되지 않았습니다. Ultralytics가 `YOLOv5 model originally trained with https://github.com/ultralytics/yolov5`라며 YOLOv8/YOLO11 forward compatibility가 없다고 명시했습니다. 따라서 YOLOv5 학습 weights는 기존 YOLOv5 worker로 유지하고, YOLOv8/YOLO11은 Ultralytics 계열 weights를 별도로 사용해야 합니다.
  - `yolo11n.pt`를 smoke용으로 받아 `openvisionlab_ultralytics_worker.py --smoke-test`를 실행했습니다.
  - 사용자 데이터 이미지 `Teaching_0.jpeg`에서는 모델 로드/프로토콜은 성공했고 후보는 0개였습니다. COCO 기본 모델이 사용자 결함 이미지를 학습하지 않았기 때문에 정상적인 결과입니다.
  - Ultralytics 패키지 샘플 `bus.jpg`에서는 후보 5개가 반환되어 `DetectImageResult` 호환 후보 좌표/정규화 좌표 경로가 확인됐습니다.
- 수정 내용:
  - `PythonModelRuntimeBundledWorkerService.IsUltralyticsWorkerScriptPath`를 추가해 번들 Ultralytics worker script를 판별할 수 있게 했습니다.
  - `PythonModelRuntimeAdapterSupportService`가 번들 Ultralytics worker + 설치된 ultralytics package를 검사 가능 상태로 해석합니다. 학습은 계속 `CanTrain=false`입니다.
  - `PythonModelSettingsValidator.GetRuntimeState`가 `CanRunInference=true`, `CanRunTraining=false`인 partial-ready 상태를 `라벨링 가능 / 현재 검사 가능`으로 표현하도록 보강했습니다.
  - focused tests가 번들 worker 판별, detection-only partial-ready, YOLOv5 script 오인 방지를 확인하도록 강화됐습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - First parallel app build hit a transient `obj\Debug\MvcVisionSystem.dll` lock because another build was still compiling. Re-running app build alone passed with 0 warnings and 0 errors.
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-ultralytics-worker` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-status-protocol` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - YOLO11 worker smoke with Test01 image passed: `artifacts\model-runtime\ultralytics\yolo11n-test01-smoke.json`, `ok=true`, image `106x106`, candidates `0`.
  - YOLO11 worker smoke with Ultralytics `bus.jpg` passed: `artifacts\model-runtime\ultralytics\yolo11n-bus-smoke.json`, `ok=true`, image `810x1080`, candidates `5`, first classes included `person` and `bus`.
  - `git diff --check -- '1. Core/PythonModelRuntimeBundledWorkerService.cs' '1. Core/PythonModelRuntimeAdapterSupportService.cs' '1. Core/PythonModelSettingsValidator.cs' 'tests/LabelingApplication.Tests/Program.cs' 'docs/WORK_TRACKING.md' 'docs/STABLE_VERIFIED_AREAS.md'` returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
  - `git status --short -- artifacts/model-runtime/ultralytics` returned no tracked/untracked files, so downloaded smoke weights/results are ignored artifacts and not commit candidates.
- 다음 작업: WPF 실제 현재 검사 flow smoke입니다. 번들 Ultralytics worker + `yolo11n.pt` + sample image settings를 WPF shell에 주입해 `현재 검사`가 worker start -> `DetectImage(model=yolo11)` -> 후보 overlay/review panel까지 이어지는지 확인합니다.

## 2026-07-02 YOLO11 Ultralytics TCP workflow smoke pass

- 점검 결과: worker 단독 smoke는 통과했지만, 앱의 실제 TCP workflow가 `model=yolo11`로 요청을 보내고 결과를 overlay/라벨 저장까지 이어가는지는 별도 확인이 필요했습니다. 기존 real YOLO smoke는 모델 엔진을 기본 YOLOv5로만 두고 있어 Ultralytics worker에 `model=yolov5`를 보내는 문제가 있었습니다.
- 수정 내용:
  - `--real-yolo-smoke` 설정에 `LABELING_SMOKE_MODEL_ENGINE` 환경변수를 추가했습니다.
  - smoke summary에 `modelEngine=...`을 기록하도록 했습니다.
  - 테스트 본문에서 `data.ProjectSettings.PythonModel.ModelEngine`을 smoke 설정값으로 채워 `DetectImage(model=yolo11)` 요청이 나가도록 했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `LABELING_SMOKE_MODEL_ENGINE=YOLO11` plus bundled worker/`yolo11n.pt`/Ultralytics `bus.jpg` settings로 `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --real-yolo-smoke` passed.
  - Evidence summary: `artifacts\real-yolo-smoke\20260702-215604\summary.txt`
  - Summary key results: `modelEngine=YOLO11`, client `Runtime\Python\openvisionlab_ultralytics_worker.py`, `candidateCount=5`, `committedCount=5`, label file `dataset\data\train\labels\bus.txt`.
- 다음 작업: WPF shell의 버튼/상태 UX smoke입니다. Core/TCP workflow는 YOLO11로 동작하므로, 이제 실제 WPF 현재 검사 버튼이 partial-ready 상태에서 열리고, 검사 결과가 후보 패널에 자연스럽게 표시되는지 1920x1080 UI 기준으로 확인합니다.

## 2026-07-02 WPF YOLO11 current-image smoke pass

- 점검 결과: Core/TCP workflow는 YOLO11로 동작했지만, WPF shell이 같은 Ultralytics smoke 결과를 현재 이미지 후보 패널과 canvas overlay에 표시하는지 별도 확인이 필요했습니다.
- 수정 내용:
  - focused test `--wpf-ultralytics-current-image-smoke`를 추가했습니다.
  - 기본 전체 테스트에는 넣지 않고, `artifacts\model-runtime\ultralytics\yolo11n.pt`와 Ultralytics package sample image가 준비된 환경에서만 실행하는 opt-in smoke로 분리했습니다.
  - 테스트는 WPF shell에 YOLO11 + 번들 worker + `yolo11n.pt` + `bus.jpg`를 주입하고 `RunDetectionForImageAsync(..., applyToCanvas:true)` 결과가 Candidate Review state, candidate panel row, canvas detection overlay로 들어오는지 확인합니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-ultralytics-worker` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-ultralytics-current-image-smoke` passed.
- 다음 작업: WPF 화면에서 partial-ready 상태가 실제 operator에게 어떻게 보이는지 1920x1080 visual smoke로 확인합니다. 특히 YOLO 설정/모델 센터에서 `현재 검사 가능`, `학습 미지원`, `YOLOv5 best.pt는 Ultralytics에 사용 불가`가 한눈에 읽히는지 점검합니다.

## 2026-07-02 WPF YOLO11 partial-ready visibility pass

- 점검 결과: YOLO11 번들 Ultralytics worker는 현재 검사까지는 가능한 상태가 되었지만, 1920 화면에서 런타임 프로필 카드와 실행 경로 요약이 여전히 `설정 확인 필요`처럼 읽힐 수 있었습니다. 이 상태는 사용자가 `현재 검사`를 눌러도 되는지, 학습은 왜 안 되는지 판단하기 어렵습니다.
- 수정 내용:
  - `PythonModelRuntimeExecutionSummaryService`가 `CanRunInference=true`, `CanRunTraining=false` 상태를 `현재 검사 가능 / 학습 미지원`으로 표시하도록 보강했습니다.
  - 같은 상태의 학습 경로는 `학습: 미지원 / 현재 연결된 worker는 검사만 지원`으로 표시하고, 검사 경로는 `DetectImage(model=yolo11)`을 유지합니다.
  - `PythonModelRuntimeProfileService`가 선택된 YOLO11 프로필 카드에 `선택됨 / 현재 검사 가능·학습 미지원`을 표시하고, 이 경우 primary action을 `확인`으로 둡니다.
  - `--wpf-visual-smoke`에 `--ultralytics-runtime-ready` fixture를 추가해 YOLO11 + 번들 worker + 설치된 Ultralytics package + weights 존재 상태를 1920 화면에서 재현할 수 있게 했습니다.
- 구조:
  - 상태 판단/문구는 Core service에 남겼습니다.
  - WPF ViewModel은 기존 바인딩 값을 갱신하고, XAML은 변경하지 않았습니다.
  - shell code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - Parallel app/test build once hit a transient `obj\Debug\MvcVisionSystem.dll` lock. Re-running the app build alone passed with 0 warnings and 0 errors.
  - `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-connection` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-yolo-model-settings-panel` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-ultralytics-worker` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --python-model-runtime-self-test` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-settings-viewmodels` passed.
  - `dotnet run --no-build --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -- --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --ultralytics-runtime-ready --width 1920 --height 1080 --output artifacts\ui\wpf-ultralytics-partial-ready-after-1920.png` passed.
  - `git diff --check -- '1. Core/PythonModelRuntimeExecutionSummaryService.cs' '1. Core/PythonModelRuntimeProfileService.cs' tests/LabelingApplication.Tests/Program.cs` returned no whitespace errors; Git only reported the existing LF/CRLF normalization warning for `Program.cs`.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-ultralytics-partial-ready-before-1920.png`
  - After 1920: `artifacts\ui\wpf-ultralytics-partial-ready-after-1920.png`
- 다음 작업: Model Center 쪽에서도 같은 partial-ready 상태가 첫 화면에서 `현재 검사 가능`, `학습 미지원`, `현재 검사 모델 yolo11n.pt`로 같이 읽히는지 1920 visual smoke를 추가로 확인합니다. 필요하면 모델 센터 summary만 보강하고, 실제 추론 경로는 건드리지 않습니다.

## 2026-07-02 WPF side-panel balance pass

- 점검 결과: 사용자가 지적한 것처럼 라벨링 작업 중에는 이미지 리스트를 계속 훑어야 하고, 저장 라벨/도구/클래스 같은 작업 패널은 필요할 때만 펼쳐 보는 성격이 강합니다. 기존 배치는 이미지 큐가 왼쪽을 고정 점유하고 작업 패널 레일이 오른쪽 끝에 있어, 캔버스와 작업 패널의 관계가 잘 들어오지 않았습니다.
- 수정 내용:
  - `WpfLabelingShellWindow.xaml`의 메인 작업 그리드를 `작업 패널 / 캔버스 / 이미지 큐` 순서로 재배치했습니다.
  - 이미지 큐는 오른쪽 고정 컬럼으로 이동했고, 작업 패널은 왼쪽 접힘/펼침 패널로 이동했습니다.
  - 왼쪽 패널 접힘/펼침 방향에 맞게 chevron 아이콘 방향과 접근성 이름을 조정했습니다.
  - 하단 실행 로그는 이미지 큐를 덮지 않도록 왼쪽 작업 패널+캔버스 영역 아래에 배치했습니다.
  - `AssertWpfMainLayoutKeepsReviewPanelVisible` 검증을 새 구조에 맞춰, 왼쪽 작업 패널과 중앙 캔버스, 오른쪽 이미지 큐가 서로 겹치지 않는지 확인하도록 변경했습니다.
- 구조:
  - ViewModel/Command 바인딩은 유지했습니다.
  - 변경 범위는 WPF shell XAML 배치와 테스트 좌표 검증에 한정했습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1366 --height 768` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --review-tab objects --width 1920 --height 1080 --output artifacts\ui\wpf-layout-side-swap-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --review-tab objects --width 1366 --height 768 --output artifacts\ui\wpf-layout-side-swap-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --roi-only --review-tab objects --right-workflow-expanded --width 1920 --height 1080 --output artifacts\ui\wpf-layout-side-swap-after-expanded-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-layout-side-swap-before-1920.png`
  - After 1920 collapsed: `artifacts\ui\wpf-layout-side-swap-after-1920.png`
  - After 1920 expanded: `artifacts\ui\wpf-layout-side-swap-after-expanded-1920.png`
  - After 1366: `artifacts\ui\wpf-layout-side-swap-after-1366.png`
- 다음 작업: 새 좌측 작업 패널 구조에서 패널 이름/내부 ViewModel 타입에 남아 있는 `RightWorkflow` 명칭은 내부 구현명이라 당장 사용자에게 보이지는 않지만, 이후 리팩터링 때 `WorkflowDock` 같은 위치 중립 이름으로 천천히 정리할 수 있습니다. 다음 UX 우선순위는 모델 센터 partial-ready 첫 화면과 좌측 작업 패널 확장 상태에서의 밀도 점검입니다.

## 2026-07-02 WPF model-center partial-ready summary pass

- 점검 결과:
  - YOLO11 Ultralytics runtime은 현재 검사까지 가능하고 학습은 아직 미지원인 partial-ready 상태입니다.
  - 설정 패널에서는 이 상태가 보강되어 있었지만, 모델센터 첫 화면의 레지스트리 요약은 `검사 가능` 중심으로 읽혀 학습 미지원 여부가 덜 명확했습니다.
  - `--wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready`는 임시 visual-smoke weights 파일이 사라진 상태로 모델 히스토리를 다시 계산해 `검사 모델로 적용` 가능한 행을 찾지 못했습니다.
- 수정 내용:
  - `WpfModelRegistryPresentationService.BuildRuntimeReadinessText`가 검사+학습 가능, 현재 검사만 가능, 학습만 가능 상태를 구분해서 표시하도록 변경했습니다.
  - YOLO11 검사 전용 partial-ready 상태는 모델센터 요약에서 `현재 검사 가능 / 학습 미지원`으로 표시됩니다.
  - model-center visual smoke가 임시 모델 히스토리 weights 파일을 보장한 뒤 dashboard를 다시 계산하도록 보강했습니다.
  - 실패 시 모델 히스토리 행의 current/promote/file-exists/path 상태가 메시지에 포함되도록 테스트 진단을 추가했습니다.
- 구조:
  - 런타임 표시 문구는 `WpfModelRegistryPresentationService`에 유지했습니다.
  - smoke fixture 안정화는 `tests/LabelingApplication.Tests/Program.cs`에 한정했습니다.
  - WPF XAML, shell code-behind, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-ultralytics-partial-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-registry` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-settings-viewmodels` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-connection` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-self-test` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-runtime-summary-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-center-ultralytics-partial-after-1920.png`
- 다음 작업: 좌측 작업 패널 확장 상태에서 모델센터의 빨간 테두리/강조가 너무 많아 `위험`과 `다음 작업`의 구분이 약합니다. 다음 패스에서는 실제 오류가 아닌 후보/대기 상태를 덜 공격적인 색상으로 분리하고, 모델센터 첫 화면에서 “현재 검사 모델 / 학습 후보 / 런타임 상태 / 다음 버튼”만 남기는 밀도 정리를 검토합니다.

## 2026-07-02 WPF model-center visual density pass

- 점검 결과:
  - 모델센터의 `모델 적용 순서`, 선택된 모델 이력, `모델 적용 판단` 카드가 모두 전역 빨강 강조를 써서 실패/오류 상태처럼 보였습니다.
  - 실제 오류/복구는 빨강을 유지해야 하지만, 학습 후보 선택과 적용 판단은 정상적인 다음 작업 상태이므로 별도 시각 언어가 필요했습니다.
- 수정 내용:
  - `WpfLabelingShellWindow.xaml`에 모델센터 전용 후보색/판단색 리소스를 추가했습니다.
  - `모델 적용 순서`와 선택된 모델 이력은 후보 모델을 뜻하는 파랑 계열 강조로 바꿨습니다.
  - 모델 적용 판단, 비교 지표, 다음 저장 판단은 호박색 계열 강조로 바꿨습니다.
  - 모델 히스토리 목록은 공용 `ReviewListBoxItemStyle` 대신 `ModelRegistryHistoryListBoxItemStyle`을 쓰도록 분리해, 선택 행이 빨간 테두리로 보이지 않게 했습니다.
  - 실제 실패/복구 패널인 `YoloModelRecoveryPanel`의 빨강은 그대로 유지했습니다.
- 구조:
  - 변경 범위는 WPF shell XAML과 XAML 구조 테스트에 한정했습니다.
  - 상태/명령/판단 문구는 기존 ViewModel/Service 바인딩을 유지했습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-registry` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-density-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready --width 1366 --height 768 --output artifacts\ui\wpf-model-center-density-after-1366.png` passed.
  - `git diff --check -- '0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml' tests/LabelingApplication.Tests/Program.cs docs/WORK_TRACKING.md docs/STABLE_VERIFIED_AREAS.md` returned no whitespace errors; Git only reported the existing LF/CRLF normalization warnings.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-ultralytics-partial-after-1920.png`
  - After 1920: `artifacts\ui\wpf-model-center-density-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-center-density-after-1366.png`
- 다음 작업: 모델센터에서 색상 의미는 분리됐지만, `검사 모델로 저장` 이후 “현재 검사 모델이 실제로 바뀌었다”는 완료 피드백이 아직 더 선명해야 합니다. 다음 우선순위는 모델 적용 완료 상태의 상단 배지/모델센터 카드/현재 검사 버튼 흐름을 한 번 더 점검하는 것입니다.

## 2026-07-02 WPF model-center confirm-save feedback pass

- 점검 결과:
  - `검사 모델로 저장`을 누른 뒤 상단 검사 모델 배지는 current로 바뀔 수 있었지만, 모델센터의 저장 버튼/다음 작업은 계속 `후보 검토 필요`로 남을 수 있었습니다.
  - 원인은 저장 성공 후 `hasPendingTrainingWeightsRecipeSave`만 내리고 `pendingTrainingBaselineWeightsPath`를 비우지 않아, 모델센터 비교 기준이 이전 모델 기준으로 남는 점이었습니다.
  - visual smoke fixture도 창 초기화 중 이전 recipe config가 섞일 수 있어, 저장 완료 검증 직전에 recipe/output root/project root를 테스트 데이터 기준으로 다시 맞추도록 보강했습니다.
- 수정 내용:
  - `ExecuteSaveYoloSettingsCommand` 성공 분기에서 pending 후보 저장이 완료되면 `pendingTrainingBaselineWeightsPath`를 비웁니다.
  - 같은 성공 분기에서 `RefreshModelCenterDashboard()` 대신 `RefreshYoloStatus()`를 호출해 상단 검사 모델 배지와 모델센터 상태를 pending 해제 후 기준으로 다시 계산합니다.
  - 저장 성공 안내 문구를 `검사 모델 적용 완료: ... 다음 현재 검사부터 이 모델을 사용합니다.`로 바꿨습니다.
  - latest weights가 현재 검사 모델과 같을 때 모델센터 버튼 문구를 `이미 적용됨`에서 `적용 완료`로 바꿨습니다.
  - `--wpf-yolo-training-session-smoke`에 `--confirm-model-save` 옵션을 추가해 실제 저장 명령 후 상단 검사 모델 배지, 모델센터 저장 버튼, 워크플로우 저장 버튼, 다음 작업 문구가 완료 상태로 바뀌는지 검증합니다.
- 구조:
  - 저장 명령 실행은 기존 shell adapter에 유지했습니다.
  - 상태 표시와 버튼 enablement는 기존 ViewModel 바인딩을 유지했습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-confirm-save-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-confirm-save-before-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1366 --height 768 --output artifacts\ui\wpf-model-center-confirm-save-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-registry` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-settings-viewmodels` passed.
  - `git diff --check -- '0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloEnvironmentBrowseCommands.cs' '0. UI/9) WPF/Views/WpfLabelingShellWindow.ModelCenterDashboard.cs' tests/LabelingApplication.Tests/Program.cs docs/WORK_TRACKING.md docs/STABLE_VERIFIED_AREAS.md` returned no whitespace errors; Git only reported existing LF/CRLF normalization warnings.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-confirm-save-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-center-confirm-save-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-center-confirm-save-after-1366.png`
- 다음 작업: 현재 검사 모델 적용 완료는 명확해졌습니다. 다음 우선순위는 모델센터에서 “후보 검증”과 “현재 검사”가 실제로 어떤 이미지/모델로 실행되는지 실행 전 확인 문구를 더 줄이고, 클릭 후 결과 후보가 어디에 나타나는지 초보자가 바로 알 수 있는지 점검하는 것입니다.

## 2026-07-03 WPF model-center action target pass

- 점검 결과:
  - 모델센터 우선 카드의 버튼 상태 줄이 `가능/대기` 중심이라 사용자가 `후보 검증`을 누르면 검출이 실행되는지, `현재 검사` 결과가 어디에 나타나는지 바로 알기 어려웠습니다.
  - 실제 흐름상 `후보 검증`은 학습 후보를 바로 실행하는 버튼이 아니라 후보 검토 탭을 여는 버튼이고, 현재 이미지 검출은 `현재 검사`에서 실행됩니다.
- 수정 내용:
  - `WpfLabelingShellViewModel.ModelCenterActionStateText` 앞에 실행 경로를 붙여 `후보 검증=학습 후보 탭 열기`, `현재 검사=검사 모델+현재 이미지 -> AI 후보/캔버스`를 직접 표시합니다.
  - `현재 검사` 버튼 툴팁을 결과 위치까지 포함하도록 보강했습니다.
  - `후보 검증` 툴팁과 실행 상태 메시지를 후보 검토 탭 이동으로 명확히 바꿔, 검출 실행 버튼처럼 읽히지 않게 했습니다.
  - 모델센터 우선 카드의 상태 텍스트는 ellipsis로 자르지 않고 줄바꿈해 표시합니다.
  - 모델센터 visual smoke가 `현재 이미지`, `AI 후보`, `캔버스` 문구를 실제 화면 텍스트에서 검증하도록 보강했습니다.
- 구조:
  - 실행 경로 문구와 버튼 상태는 `WpfLabelingShellViewModel` 상태로 유지했습니다.
  - XAML은 텍스트 바인딩과 줄바꿈 표시만 담당합니다.
  - 기존 shell command는 UI adapter 범위의 상태/로그 문구만 조정했습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-action-target-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-action-target-confirmed-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output artifacts\ui\wpf-model-center-action-target-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-registry` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-confirm-save-before-1920.png`
  - After 1920: `artifacts\ui\wpf-model-center-action-target-after-1920.png`
  - After confirmed 1920: `artifacts\ui\wpf-model-center-action-target-confirmed-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-center-action-target-after-1366.png`
- 다음 작업: 모델센터의 실행 대상 안내는 보강됐습니다. 다음 우선순위는 실제 `현재 검사` 실행 후 후보 결과가 우측 이미지 큐/캔버스/후보 검토 상태에 어떻게 반영되는지, 사용자에게 저장 전 후보인지 확정 라벨인지 더 선명하게 보이는지 확인하는 것입니다.

## 2026-07-03 WPF inference status readable text pass

- 점검 결과:
  - `현재 검사` 실행 전후 상단 `추론 상태`는 사용자가 모델과 결과 위치를 확인하는 핵심 피드백인데, 기본 대기/검사 모델/모델 후보/모델 없음 문구 일부가 깨진 한글로 남아 있었습니다.
  - 사용자는 로그를 보지 않고도 지금 검사 모델이 무엇인지, 학습 후보가 아직 저장 전인지 알아야 합니다.
- 수정 내용:
  - `WpfInferenceStatusPresentationService`를 정상 한글 출력 계약으로 재작성했습니다.
  - 기본 대기 상태, 검사 모델, 모델 후보, 검사 모델 없음, 툴팁의 추론 상태/전체 모델 경로 문구를 모두 정상화했습니다.
  - `SetGlobalInferenceStatus` UI adapter의 기본 대기 문구도 정상화했습니다.
  - 기존 깨진 기대값 테스트 대신 정상 한글 계약을 검증하는 `TestWpfInferenceStatusPresentationServiceReadable`를 추가하고 테스트 라우팅을 교체했습니다.
- 구조:
  - 표시 문구 생성은 `WpfInferenceStatusPresentationService`에 유지했습니다.
  - shell code-behind는 상태 애니메이션과 바인딩 대상 갱신만 담당합니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-inference-status-presentation` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-inference-status-readable-after-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-model-center-action-target-after-1920.png`
  - After 1920: `artifacts\ui\wpf-inference-status-readable-after-1920.png`
- 다음 작업: 상단 추론 상태의 깨진 기본 문구는 정리됐습니다. 다음 우선순위는 `현재 검사` 실행 직후 생성된 AI 후보가 우측 이미지 큐와 후보 검토 패널에서 “저장 전 후보”로 더 분명하게 보이는지 확인하는 것입니다.

## 2026-07-03 WPF AI-candidate unsaved wording pass

- 점검 결과:
  - 이미지 큐는 이미 `AI 후보 n개 검토 필요`와 `저장 완료`를 나누고 있었지만, 캔버스/후보 오버레이 presentation은 `검출 결과`, `후보`처럼 저장 라벨과의 차이가 약한 문구를 쓰고 있었습니다.
  - 사용자는 현재 보이는 박스가 자동 검출 후보인지, 이미 저장된 정답 라벨인지 즉시 구분해야 합니다.
- 수정 내용:
  - `WpfCandidateReviewPresentationService`의 오버레이 제목/요약/선택/상세 문구를 `AI 후보(저장 전)`, `AI 후보 n개`, `저장 전` 중심으로 바꿨습니다.
  - 후보 없음/필터 통과 없음 상태도 `AI 후보 없음`으로 통일했습니다.
  - `WpfDetectionResultPresentationService`의 후보 로드 기록과 후보 없음 카드도 `AI 후보`와 `저장 전`을 명시하도록 바꿨습니다.
  - `--wpf-candidate-review-presentation`, `--wpf-detection-result-presentation` focused CLI 플래그를 추가해 이 계약만 따로 검증할 수 있게 했습니다.
- 구조:
  - 문구 생성은 presentation service에 유지했습니다.
  - 캔버스 오버레이 렌더링, hit-test, OpenGL/Viewer 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-presentation` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-detection-result-presentation` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-detection-display-mode` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --width 1920 --height 1080 --output artifacts\ui\wpf-ai-candidate-unsaved-after-1920.png` passed.
  - A mistaken full-suite route was triggered before adding the focused flags and stopped at the existing unrelated `Project settings persist dataset purpose in recipe config: Expected '1', got '0'` issue.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-inference-status-readable-after-1920.png`
  - After 1920: `artifacts\ui\wpf-ai-candidate-unsaved-after-1920.png`
- 다음 작업: AI 후보/저장 라벨 구분 문구는 좋아졌습니다. 다음 우선순위는 후보 확정 이후 `저장 완료`, `라벨 저장됨`, `다음 미완료`가 이미지 큐/워크플로우 상단/후보 검토 패널에서 같은 언어로 연결되는지 점검하는 것입니다.

## 2026-07-03 WPF candidate completion next-unfinished pass

- 점검 결과:
  - 후보 확정 후 완료 카드의 설명은 `다음 미완료 이미지`를 말하고 있었지만, 버튼과 다음 작업 문구는 일부 `다음 이미지`로만 표시되어 목적지가 덜 명확했습니다.
  - `객체 없음 저장 후 다음`처럼 긴 버튼 문구는 1920x1080에서도 왼쪽 패널 폭 안에서 잘릴 수 있었습니다.
- 수정 내용:
  - `WpfCandidateReviewCompletionPresentationService`에서 저장 완료 제목을 `라벨 저장 완료`로 정리했습니다.
  - 저장된 라벨 상태의 다음 작업/버튼은 `다음 미완료`를 명시합니다.
  - 저장 필요 상태는 `라벨 저장 후 다음`으로 줄이고, 상세/툴팁에서 다음 미완료 이미지 이동을 설명합니다.
  - 객체 없음 완료 상태는 버튼을 `객체 없음 저장`으로 줄여 잘림을 피하고, 다음 작업 줄에서 `객체 없음 저장 후 다음 미완료 이미지`를 설명합니다.
  - `--wpf-labeling-session-smoke`에 `--width`/`--height` 옵션을 추가해 1920x1080 캡처 검증이 가능하게 했습니다.
- 구조:
  - 완료 문구 생성은 `WpfCandidateReviewCompletionPresentationService`에 유지했습니다.
  - ViewModel은 presentation payload를 반영만 하고, View code-behind/Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-session-smoke --width 1920 --height 1080 --output artifacts\ui\wpf-candidate-complete-next-unfinished-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
- 캡처:
  - Before reference 1920: `artifacts\ui\wpf-ai-candidate-unsaved-after-1920.png`
  - After 1920: `artifacts\ui\wpf-candidate-complete-next-unfinished-after-1920.png`
- 다음 작업: 후보 확정 후 완료 흐름은 더 명확해졌습니다. 다음 우선순위는 오른쪽 이미지 큐의 현재 작업 카드와 상단 workflow next-action이 완료/저장/다음 미완료 상태를 같은 표현으로 유지하는지 추가 점검하는 것입니다.

## 2026-07-03 WPF queue/top next-unfinished consistency pass

- 점검 결과:
  - 후보 완료 카드에서는 `다음 미완료`를 말하지만, 오른쪽 이미지 큐 현재 작업 카드와 상단 작업 흐름 요약은 완료 상태 이후 목적지를 덜 명확하게 표현했습니다.
  - 특히 `저장 완료`, `객체 없음 완료` 상태에서 사용자가 다음 버튼을 눌러도 저장된 이미지를 다시 도는지, 미완료 이미지만 찾는지 즉시 알기 어려웠습니다.
- 수정 내용:
  - 상단 상태바의 완료 후 next-action을 `다음: 다음 미완료 이미지`로 정리했습니다.
  - 오른쪽 이미지 큐 현재 작업 카드의 저장 완료/객체 없음 완료/저장 라벨 있음 설명에 `다음 미완료` 이동을 명시했습니다.
  - 객체 없음 완료 배지는 `없음` 대신 `객체없음`으로 바꿔 상태 의미를 더 분명하게 했습니다.
  - 상단 workflow stage의 추론 단계 next-action도 이후 `AI 후보 확정/숨김 후 다음 미완료, 완료되면 학습/모델 센터`로 정리했습니다.
- 구조:
  - 이미지 큐 현재 작업 문구는 `WpfImageQueuePanelViewModel`에 유지했습니다.
  - workflow stage 문구는 `WpfWorkflowStagePresentationService`에 유지했습니다.
  - shell code-behind는 기존 상태 계산/바인딩 어댑터만 유지했고, Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-session-smoke --width 1920 --height 1080 --output artifacts\ui\wpf-queue-top-next-unfinished-after-1920.png` passed.
- 캡처:
  - Before reference 1920: `artifacts\ui\wpf-candidate-complete-next-unfinished-after-1920.png`
  - After 1920: `artifacts\ui\wpf-queue-top-next-unfinished-after-1920.png`
- 다음 작업: 저장 완료 이후 목적지 표현은 큐/상단/후보 완료 카드에서 맞춰졌습니다. 다음 우선순위는 추론 검토 패널의 후보 결정 카드가 `저장 전 후보`, `현재 이미지 저장 필요`, `학습 모델 검증`을 한 화면에서 너무 강한 빨간색으로 동시에 보여 주는 문제를 줄이는 것입니다.

## 2026-07-03 WPF candidate-review non-error color pass

- 점검 결과:
  - 추론 검토 패널의 AI 후보 배지, 현재 이미지 후보 역할 카드, 안내 문구가 전역 `AccentBrush`를 써서 저장 필요/오류와 같은 빨간 계열로 보였습니다.
  - 사용자는 `저장 전 AI 후보` 안내와 실제 오류/저장 필요를 색만 보고도 구분할 수 있어야 합니다.
- 수정 내용:
  - `WpfCandidateReviewPanel.xaml`에 AI 후보 전용 청록 계열 브러시와 모델 검증 전용 파랑 계열 브러시를 추가했습니다.
  - AI 후보 배지, 현재 이미지 후보 역할 카드, AI 후보 안내 문구는 전용 AI 후보 브러시를 사용하도록 바꿨습니다.
  - 학습 모델 검증 안내는 모델 검증 전용 브러시로 분리했습니다.
  - 실제 저장 필요/오류/주요 실행 버튼에 쓰이는 전역 AccentBrush는 그대로 유지했습니다.
- 구조:
  - 색상과 시각 강조는 XAML 리소스로만 분리했습니다.
  - CandidateReview ViewModel의 상태/명령/워크플로우는 변경하지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-candidate-review-color-after-1920.png` passed.
- 캡처:
  - Before reference 1920: `artifacts\ui\wpf-queue-top-next-unfinished-after-1920.png`
  - After 1920: `artifacts\ui\wpf-candidate-review-color-after-1920.png`
- 다음 작업: 후보 검토의 비오류 안내 색상은 분리됐습니다. 다음 우선순위는 같은 화면에서 후보 검토 하단 액션 버튼과 완료 카드가 좁은 패널에서도 한 줄씩 읽히는지, 1366 폭에서 다시 확인하는 것입니다.

## 2026-07-03 WPF candidate-review compact action priority pass

- 점검 결과:
  - 1366x768 후보 검토 화면에서 학습 모델 검증 카드가 현재 이미지 후보 액션보다 위에 있어 `후보 위치`, `라벨 확정`, `전체 라벨화`, `후보 숨김` 버튼이 아래로 밀렸습니다.
  - 사용자가 현재 이미지의 AI 후보를 검토하는 중에는 후보 액션이 모델 검증보다 먼저 보여야 합니다.
- 수정 내용:
  - 모델 후보 결정 카드는 `ModelCandidateDecisionVisibility`를 통해 실제 저장/거절이 가능한 상태에서만 표시되도록 했습니다.
  - 후보 요약과 후보 액션 패널을 모델 검증 카드보다 위 행으로 올렸습니다.
  - 모델 검증 요약은 유지하되, 현재 이미지 후보 검토의 핵심 버튼을 1366x768에서도 보이게 했습니다.
- 구조:
  - 결정 카드 표시 여부는 `WpfCandidateReviewPanelViewModel` 상태로 분리했습니다.
  - XAML은 `ModelCandidateDecisionVisibility`와 행 배치만 바인딩합니다.
  - CandidateReview command/selection workflow와 Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-candidate-review-compact-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-candidate-review-compact-after-1920.png` passed.
  - A mistaken non-existent `--wpf-model-comparison-example-click` flag was attempted and fell through to the broad regression path, stopping at the existing unrelated `Project settings persist dataset purpose in recipe config: Expected '1', got '0'` issue.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-candidate-review-color-after-1366.png`
  - After 1366: `artifacts\ui\wpf-candidate-review-compact-after-1366.png`
  - After 1920: `artifacts\ui\wpf-candidate-review-compact-after-1920.png`
- 다음 작업: 후보 검토의 현재 이미지 액션 우선순위는 정리됐습니다. 다음 우선순위는 우측 이미지 큐의 후보/저장/객체없음 필터와 현재 검토 패널의 용어가 더 긴 실제 파일명에서도 깨지지 않는지 확인하는 것입니다.

## 2026-07-03 WPF image-queue current-task wrap pass

- 점검 결과:
  - 우측 이미지 큐 현재 작업 카드의 설명이 한 줄 말줄임으로 잘려 AI 후보 검토 행동이 온전히 보이지 않았습니다.
  - 툴팁도 파일명과 상태 요약만 담고 있어, 카드 설명이 잘린 경우 전체 작업 안내를 확인하기 어려웠습니다.
- 수정 내용:
  - `CurrentImageTaskDetailText`를 2줄까지 래핑하도록 바꾸고 현재 작업 카드 행의 최소 높이를 조정했습니다.
  - 현재 작업 카드 툴팁에 파일명, 카드 제목, 상세 행동, 상태 요약을 모두 넣도록 `BuildCurrentImageTaskToolTip`를 추가했습니다.
  - 이미지 큐 ViewModel 테스트에 긴 상태/후보 안내가 tooltip에 남는지 검증을 추가했습니다.
- 구조:
  - 표시 문구/툴팁 조합은 `WpfImageQueuePanelViewModel`에 유지했습니다.
  - XAML은 래핑/높이 바인딩만 담당합니다.
  - 이미지 로딩, 필터링, Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-image-queue-current-task-wrap-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-image-queue-current-task-wrap-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-candidate-review-compact-after-1366.png`
  - After 1366: `artifacts\ui\wpf-image-queue-current-task-wrap-after-1366.png`
  - After 1920: `artifacts\ui\wpf-image-queue-current-task-wrap-after-1920.png`
- 다음 작업: 우측 이미지 큐 현재 작업 카드의 핵심 행동 가독성은 개선됐습니다. 다음 우선순위는 이미지 큐 하단 목록의 `저장/검사/크기` 열이 긴 파일명과 상태 텍스트에서 필요한 정보를 우선순위대로 보여 주는지 점검하는 것입니다.

## 2026-07-03 WPF image-queue row summary tooltip pass

- 점검 결과:
  - 오른쪽 이미지 큐 하단 목록은 폭이 좁을 때 긴 파일명과 `저장/검사/크기` 열이 말줄임으로 보입니다.
  - 기존 툴팁은 `Detail` 중심이라 파일명, 저장 상태, 검사 상태, 크기, 실패 원인을 한 번에 확인하기 어려웠습니다.
- 수정 내용:
  - `WpfImageQueueItem`에 `QueueRowToolTip`, `QueueRowAccessibleName` 계산 속성을 추가했습니다.
  - 행 요약에는 파일명, 저장 상태, 검사 상태, 크기, 상태 요약, 상세 원인을 포함합니다.
  - `LabelStatus`, `DetectStatus`, `Dimensions`, `Detail`, `QueueStatusSummary` 변경 시 행 요약 툴팁/자동화 이름도 갱신되도록 했습니다.
  - 이미지 큐 DataGrid의 파일 셀과 상태 열 툴팁을 `QueueRowToolTip`으로 바꾸고, 파일 셀 자동화 이름을 `QueueRowAccessibleName`에 연결했습니다.
- 구조:
  - 행 요약 생성은 `WpfImageQueueItem` 모델에 두고, XAML은 바인딩만 담당합니다.
  - 이미지 로딩, 필터링, Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-image-queue-row-summary-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-image-queue-row-summary-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-image-queue-current-task-wrap-after-1366.png`
  - After 1366: `artifacts\ui\wpf-image-queue-row-summary-after-1366.png`
  - After 1920: `artifacts\ui\wpf-image-queue-row-summary-after-1920.png`
- 다음 작업: 이미지 큐 하단 목록의 잘림 보조 정보는 정리됐습니다. 다음 우선순위는 후보 검토 패널의 선택 후보/현재 라벨 정보가 긴 텍스트에서도 핵심 판단 버튼보다 공간을 과하게 차지하지 않는지 점검하는 것입니다.

## 2026-07-03 WPF candidate-review text cap pass

- 점검 결과:
  - 후보 검토 패널은 핵심 조작 버튼을 위로 올렸지만, 선택 후보 요약과 후보/현재 라벨 비교 텍스트가 길어지면 다시 후보 목록과 아래 검토 정보를 밀 수 있는 구조였습니다.
  - 실제 조작 중에는 `이전 후보`, `후보 위치`, `기존 라벨`, `다음 후보`, `라벨 확정`, `전체 라벨화`, `후보 숨김` 버튼이 항상 먼저 살아 있어야 합니다.
- 수정 내용:
  - `SelectedCandidateSummaryText`, `CandidateCompareCandidateText`, `CandidateCompareCurrentText`, `CandidateDetailText`를 2-3줄 높이로 제한했습니다.
  - `CandidateComparisonDecisionTextStyle`에도 높이 제한과 전체 문구 툴팁을 추가했습니다.
  - 잘린 텍스트는 각 TextBlock의 `ToolTip`에서 전체 내용을 확인할 수 있게 했습니다.
- 구조:
  - 후보 선택/확정/스킵 workflow와 ViewModel command는 변경하지 않았습니다.
  - XAML은 표시 높이와 툴팁 바인딩만 담당합니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-candidate-review-text-cap-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-candidate-review-text-cap-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-image-queue-row-summary-after-1366.png`
  - After 1366: `artifacts\ui\wpf-candidate-review-text-cap-after-1366.png`
  - After 1920: `artifacts\ui\wpf-candidate-review-text-cap-after-1920.png`
- 다음 작업: 후보 검토의 긴 텍스트 밀림은 줄였습니다. 다음 우선순위는 모델 센터/런타임 탭에서 현재 검사 가능 상태와 학습 미지원 상태가 초보자에게 같은 성공 상태처럼 보이지 않는지 다시 확인하는 것입니다.

## 2026-07-03 WPF runtime summary status pass

- 점검 결과:
  - 1366x768 YOLO 모델 설정 화면에서 YOLO11이 `현재 검사 가능 / 학습 미지원`인 상태여도, 첫 카드에는 모델 파일/추론 파라미터만 먼저 보였습니다.
  - 선택 런타임의 실제 가능 범위는 아래 `모델 실행기 연결 상태` 목록이나 실행 경로 카드까지 내려가야 보여서, 초보자는 현재 검사는 가능한지 학습까지 가능한지 바로 구분하기 어려웠습니다.
- 수정 내용:
  - `WpfYoloModelSettingsPanelViewModel.SettingsSummaryRuntimeStatusText`를 추가해 기존 `RuntimeExecutionSummaryText`를 첫 요약 카드에서도 노출했습니다.
  - YOLO 모델 설정 첫 카드에 `YoloModelSettingsSummaryRuntimeStatusText`를 추가했습니다.
  - 해당 상태줄은 전역 오류/강조색 대신 `YoloRuntimeStatusBrush`를 사용해 런타임 가능 범위를 오류처럼 보이지 않게 했습니다.
- 구조:
  - 런타임 가능/미지원 판단은 기존 `PythonModelRuntimeExecutionSummaryService` 결과를 재사용합니다.
  - XAML은 새 상태줄 바인딩만 담당하고, 런타임 판정/연결/설치 workflow는 변경하지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-settings-viewmodels` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --ultralytics-runtime-ready --width 1366 --height 768 --output artifacts\ui\wpf-runtime-summary-status-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --ultralytics-runtime-ready --width 1920 --height 1080 --output artifacts\ui\wpf-runtime-summary-status-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-runtime-partial-ready-audit-before-1366.png`
  - After 1366: `artifacts\ui\wpf-runtime-summary-status-after-1366.png`
  - After 1920: `artifacts\ui\wpf-runtime-summary-status-after-1920.png`
- 다음 작업: 선택 런타임의 현재 검사/학습 가능 범위는 첫 카드에서 보이게 됐습니다. 다음 우선순위는 모델 센터 쪽에서도 같은 상태가 `현재 검사` 버튼 주변에서 일관되게 보이는지 점검하는 것입니다.

## 2026-07-03 WPF model-center runtime action-state pass

- 점검 결과:
  - 모델 센터 요약 카드와 레지스트리에는 YOLO11 partial-ready 상태가 보였지만, 실제 사용자가 누르는 `후보 검증`, `검사 모델로 저장`, `현재 검사` 버튼 바로 아래 상태 줄은 `버튼 상태` 중심으로만 읽혔습니다.
  - 1366x768 화면에서는 왼쪽 학습/모델 센터 패널 폭이 제한되기 때문에, 버튼 근처에서 `현재 검사 가능 / 학습 미지원`을 바로 보지 못하면 사용자가 현재 검사를 눌러도 되는지 다시 설정 탭을 찾아가야 했습니다.
- 수정 내용:
  - `WpfLabelingShellViewModel`에 `ModelCenterRuntimeActionText` 상태를 추가했습니다.
  - 모델 센터 대시보드 refresh adapter가 `WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings)` 결과를 `SetModelCenterModelState`로 전달하게 했습니다.
  - `ModelCenterActionStateText`를 `실행기: YOLO11 Ultralytics / 현재 검사 가능 / 학습 미지원 / 실행: ... / 버튼 상태: ...` 순서로 구성해 버튼 주변에서 런타임 범위와 버튼 가능 상태를 같이 읽도록 했습니다.
  - 현재 검사 버튼 툴팁도 런타임 요약을 보존하되, 현재 모델 텍스트에 같은 요약이 이미 포함된 경우 중복으로 붙이지 않게 했습니다.
- 구조:
  - 런타임 가능/미지원 판단은 기존 `WpfModelRegistryPresentationService`와 runtime profile service 결과를 재사용합니다.
  - XAML은 기존 `ModelCenterPriorityButtonStateText -> ShellViewModel.ModelCenterActionStateText` 바인딩을 그대로 사용합니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-registry` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready --width 1366 --height 768 --output artifacts\ui\wpf-model-center-runtime-action-state-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --ultralytics-runtime-ready --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-runtime-action-state-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-model-center-density-after-1366.png`
  - Before 1920: `artifacts\ui\wpf-model-center-ultralytics-partial-after-1920.png`
  - After 1366: `artifacts\ui\wpf-model-center-runtime-action-state-after-1366.png`
  - After 1920: `artifacts\ui\wpf-model-center-runtime-action-state-after-1920.png`
- 다음 작업: 모델 센터 버튼 주변의 런타임 범위는 정리됐습니다. 다음 우선순위는 현재 검사 실행 후 후보 검토 패널로 이어질 때 `AI 후보`, `라벨 저장`, `다음 미완료 이미지` 흐름이 좌측 모델 센터/우측 이미지 큐에서 같은 용어로 이어지는지 확인하는 것입니다.

## 2026-07-03 WPF image-queue AI-candidate spacing pass

- 점검 결과:
  - 후보 검토/워크플로우 쪽은 `AI 후보`를 사용하지만, 이미지 큐 빠른 필터와 큐 요약 일부는 `AI후보`처럼 띄어쓰기 없이 표시했습니다.
  - 같은 오른쪽 이미지 큐 안에서도 필터, 행 상태, 데이터셋 상태 문구가 서로 달라 보이면 초보 사용자는 별도 상태처럼 읽을 수 있습니다.
- 수정 내용:
  - `WpfImageQueuePresenter.BuildReviewCountSummary`와 `FormatDetectionStatus`가 `AI 후보`를 사용하도록 바꿨습니다.
  - `WpfImageQueueFilterOption.GetDisplayName(WpfImageQueueFilter.Candidate)`을 `AI 후보`로 바꿨습니다.
  - `WpfImageQueuePanelViewModel`의 빠른 필터 버튼 기본/갱신 라벨도 `AI 후보`로 맞췄습니다.
- 구조:
  - 큐 필터링, 로딩, 검출 결과 저장 로직은 변경하지 않았습니다.
  - ViewModel과 presentation service의 표시 문자열만 변경했습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-image-queue-row-summary-after-1366.png`
  - After 1366: `artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1366.png`
  - After 1920: `artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1920.png`
- 다음 작업: 이미지 큐의 `AI 후보` 용어는 맞췄습니다. 다음 우선순위는 후보 검토 패널 내부에 남아 있는 `검출 후보` 문구가 의도된 학습자 용어인지, 아니면 저장 전 `AI 후보`와 충돌하는지 기존 안정화 계약을 보고 정리 여부를 판단하는 것입니다.

## 2026-07-03 WPF AI-candidate wording consistency pass

- 점검 결과:
  - 이미지 큐는 `AI 후보`로 정리됐지만, 후보 검토 버튼 툴팁/상태 메시지/학습 워크플로우 안내에는 아직 `검출 후보`가 남아 있었습니다.
  - 현재 검사 결과는 저장 전까지 정답 라벨이 아니므로, 사용자가 `라벨`과 `추론 결과`를 헷갈리지 않게 저장 전 검출 결과는 일관되게 `AI 후보`로 표시해야 합니다.
- 수정 내용:
  - 캔버스 후보 이동/기준 라벨/후보 지움/후보 확정/후보 숨김 툴팁의 `검출 후보` 문구를 `AI 후보`로 통일했습니다.
  - 후보 검토 shell adapter의 선택/이동/확정/스킵/로드 상태 메시지를 `AI 후보` 기준으로 바꿨습니다.
  - 학습 워크플로우 패널의 검토 단계 안내도 `AI 후보`를 보고 확정/스킵한다는 표현으로 맞췄습니다.
  - 튜토리얼 테스트가 더 이상 사용하지 않는 옛 이미지 경로를 기대하던 부분을 최신 주석 이미지 경로로 갱신했습니다.
- 구조:
  - 후보 검토 workflow, 저장, 이미지 큐 필터링, detection overlay 렌더링 로직은 변경하지 않았습니다.
  - shell code-behind 변경은 기존 UI adapter 메시지/툴팁 문구 정리에 한정했습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `rg -n "검출 후보" "0. UI/9) WPF" tests\LabelingApplication.Tests\Program.cs` 결과 WPF/test 범위 잔여 문구 없음.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-canvas-panel-commands` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-canvas-detection-overlay` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-ai-candidate-wording-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-ai-candidate-wording-after-1920.png` passed.
- 캡처:
  - Before 1366: `artifacts\ui\wpf-image-queue-ai-candidate-spacing-after-1366.png`
  - After 1366: `artifacts\ui\wpf-ai-candidate-wording-after-1366.png`
  - After 1920: `artifacts\ui\wpf-ai-candidate-wording-after-1920.png`
- 다음 작업: `AI 후보`와 `저장 라벨`의 용어 충돌은 줄었습니다. 다음 우선순위는 후보 검토에서 확정 후 저장이 필요한 상태가 이미지 큐/캔버스/상단 상태에서 같은 강도로 드러나는지 점검하는 것입니다.

## 2026-07-03 WPF candidate auto-save guidance pass

- 점검 결과:
  - 기존 테스트 계약상 AI 후보를 `라벨 확정`하면 해당 라벨은 즉시 저장 라벨 목록으로 이동하고 파일 저장 상태도 `파일 저장됨`으로 유지됩니다.
  - 하지만 이미지 큐 현재 작업 카드와 상단 추론 단계 안내가 AI 후보 확정 후에도 별도 저장을 해야 하는 흐름처럼 읽혀, 실제 동작과 어긋날 수 있었습니다.
- 수정 내용:
  - 이미지 큐의 `AI 후보 검토` 카드 상세를 `후보를 확정하거나 숨기세요. 확정하면 저장 라벨에 자동 반영됩니다.`로 변경했습니다.
  - 상단 워크플로우 추론 단계의 다음 작업 문구를 `AI 후보 확정/숨김 후 다음 미완료`로 변경했습니다.
  - 이미지 큐 상태 테스트와 셸 구조 테스트에 수동 저장 오해를 막는 assertion을 추가했습니다.
- 구조:
  - 후보 확정/숨김/저장 동작은 변경하지 않았습니다.
  - 문구는 `WpfImageQueuePanelViewModel`과 `WpfWorkflowStagePresentationService`에만 유지했고, View code-behind로 상태 판단을 옮기지 않았습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1366 --height 768 --output artifacts\ui\wpf-candidate-autosave-guidance-after-1366.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-candidate-autosave-guidance-after-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-ai-candidate-wording-after-1920.png`
  - After 1366: `artifacts\ui\wpf-candidate-autosave-guidance-after-1366.png`
  - After 1920: `artifacts\ui\wpf-candidate-autosave-guidance-after-1920.png`
- 다음 작업: 후보 확정의 저장 동작 안내는 실제 동작과 맞췄습니다. 템플릿 초안/자동 저장 용어 정리는 다음 패스에서 완료했습니다.

## 2026-07-03 WPF template draft-label wording pass

- 점검 결과:
  - 템플릿 현재 이미지 실행은 AI 추론 후보와 달리 후보 검토 패널에 남는 값이 아니라, 현재 이미지에 저장 전 라벨 초안을 추가하고 사용자가 위치 확인 후 `라벨 저장`을 눌러야 합니다.
  - 반대로 전체 이미지 템플릿 실행은 라벨 없는 이미지에 바로 라벨 파일을 저장합니다.
  - 기존 문구는 두 경로를 같은 성격의 후보 작업처럼 보여 AI 후보 검토 흐름과 헷갈릴 수 있었습니다.
- 수정 내용:
  - 템플릿 현재 이미지 실행 문구를 `현재 이미지 라벨 초안 생성`, `템플릿 라벨 초안`, `저장 전 초안` 기준으로 정리했습니다.
  - 전체 이미지 템플릿 실행 문구를 `전체 이미지 자동 저장`으로 바꿔 라벨 없는 이미지에 바로 저장되는 경로임을 드러냈습니다.
  - 상단 도구 메뉴의 템플릿 흐름을 `기준 라벨 선택 -> 라벨 초안 생성 -> 위치 확인 -> 라벨 저장`으로 변경했습니다.
  - 오른쪽 `가이드/도구`의 템플릿 반복 라벨링 카드도 현재 이미지 초안과 전체 이미지 자동 저장을 분리해 설명합니다.
  - 이미지 큐의 템플릿 일괄 실행 버튼도 `전체 자동 저장`으로 바꿔 버튼만 봐도 라벨 없는 이미지에 바로 저장되는 작업임을 알 수 있게 했습니다.
- 구조:
  - 템플릿 매칭/저장 알고리즘은 변경하지 않았습니다.
  - 상태와 가이드 문구는 `WpfTemplateMatchingAutoLabelViewModel`, `WpfLearningWorkflowPanelViewModel`, shell UI adapter에만 두었습니다.
  - Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` passed with 0 warnings and 0 errors.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --template-guide-ux` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --template-batch-autolabel-storage` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-template-current-image-no-candidate` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-learning-workflow-panel` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab guide --right-workflow-expanded --expand-learning-concepts --focus-template-workflow --width 1920 --height 1080 --output artifacts\ui\wpf-template-draft-label-guidance-after-1920-clean.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab yolo --open-header-tools-menu --width 1920 --height 1080 --output artifacts\ui\wpf-template-draft-header-tools-after-1920.png` passed.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab guide --right-workflow-expanded --expand-learning-concepts --focus-template-workflow --width 1920 --height 1080 --output artifacts\ui\wpf-template-auto-save-queue-button-after-1920.png` passed.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-guide-tools-template-flow-after-1920.png`
  - After guide 1920: `artifacts\ui\wpf-template-draft-label-guidance-after-1920-clean.png`
  - After tools menu 1920: `artifacts\ui\wpf-template-draft-header-tools-after-1920.png`
  - After image queue 1920: `artifacts\ui\wpf-template-auto-save-queue-button-after-1920.png`
- 다음 작업: 템플릿과 AI 후보 용어는 분리했습니다. 다음 우선순위는 오른쪽 이미지 큐에서 `AI 후보`, `저장 필요`, `저장됨`, `숨김`이 실제 작업 순서대로 정렬/강조되는지 점검하는 것입니다.

## 2026-07-03 WPF image queue work-needed filter pass

- 점검 결과:
  - 좌측 작업 패널/우측 이미지 큐 배치는 이미 적용되어 있었고, 반복 작업 대상이 아니었습니다.
  - 우측 이미지 큐의 빠른 필터는 `전체`, `AI 후보`, `실패`, `저장됨`, `숨김`, `객체없음`이 같은 무게로 보여서 사용자가 먼저 처리할 행을 바로 고르기 어려웠습니다.
  - 저장된 라벨을 수정해 `저장 필요`가 된 행은 아직 파일 반영이 필요하므로 완료 필터가 아니라 작업 필요 흐름에 남아야 합니다.
- 수정 내용:
  - 이미지 큐 빠른 필터 첫 칸에 `작업 필요`를 추가했습니다.
  - 기존 `Unlabeled` 필터 표시명을 `작업 필요`로 바꿔 라벨링/AI 후보/저장 필요/검사 실패처럼 아직 처리할 행을 모아 보게 했습니다.
  - 저장 필요 행은 `IsLabeled`가 true여도 완료로 계산하지 않도록 `WpfImageQueueFilterService.IsCompletedQueueItem`을 보정했습니다.
  - 빠른 필터 영역은 3x2 고정에서 2열 자동 행으로 바꿔 1920/1366 폭에서 `작업 필요`와 완료 상태 필터가 같이 보이도록 했습니다.
- 구조:
  - 필터 판정은 기존 `WpfImageQueueFilterService`를 재사용했습니다.
  - 빠른 필터 문구/활성 상태/명령은 `WpfImageQueuePanelViewModel`에 추가했고, Shell은 기존 `SetImageQueueFilter(WpfImageQueueFilter.Unlabeled)` 경로만 호출합니다.
  - View code-behind에는 UserControl 내부 컨트롤 접근자만 추가했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-image-queue-status`, `--wpf-labeling-shell`, `--mvvm-infra` 통과.
  - `--wpf-responsive-layout --review-tabs objects --width 1920 --height 1080` 통과.
  - `--wpf-responsive-layout --review-tabs objects --width 1366 --height 768` 통과.
  - 1920 캡처: `artifacts\ui\wpf-image-queue-work-needed-filter-after-1920.png`.
  - 1366 캡처: `artifacts\ui\wpf-image-queue-work-needed-filter-after-1366.png`.
- 다음 작업:
  - 후보 검토 긴 텍스트 밀림은 이미 `2026-07-03 WPF candidate review text cap pass`에서 완료된 보호 항목이므로 반복하지 않습니다.
  - 다음 우선순위는 학습/추론/모델 비교 흐름에서 아직 보호 항목으로 묶이지 않은 실제 사용자 혼동 지점을 새로 잡습니다.

## 2026-07-03 WPF image queue AI-candidate test-contract sync

- 점검 결과:
  - 이미지 큐 UI와 presenter/service 계약은 `AI 후보` 띄어쓰기 기준으로 정리되어 있었지만, ViewModel 단위 테스트 한 곳이 여전히 `AI후보`를 기대했습니다.
  - 이 상태에서는 이후 회귀 테스트가 실제 UI 용어와 다른 방향으로 고정될 수 있습니다.
- 수정 내용:
  - `tests/LabelingApplication.Tests/Program.cs`의 이미지 큐 빠른 필터 ViewModel 기대값을 `AI 후보 2`로 맞췄습니다.
  - 후보 검토/모델 센터/Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `--wpf-image-queue-status` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--mvvm-infra` 통과.
  - `rg -n "AI\\uD6C4|AI후보|AI 후보" tests\LabelingApplication.Tests\Program.cs "0. UI/9) WPF/ViewModels/WpfImageQueuePanelViewModel.cs" "0. UI/9) WPF/Services/WpfImageQueuePresenter.cs" "0. UI/9) WPF/Models/WpfImageQueueModels.cs"`로 테스트/표시 경로의 `AI 후보` 기준을 확인했습니다.
- 다음 작업:
  - 신규 구현은 기존 완료 항목을 반복하지 말고, 모델 런타임/모델 비교/추론 결과 확인 흐름 중 아직 테스트 계약이 약한 지점을 먼저 선정합니다.

## 2026-07-03 WPF dataset-purpose runtime-boundary pass

- 점검 결과:
  - 객체탐지 외 목적도 첫 화면에서 선택할 수 있지만, 세그멘테이션/이상탐지 설명은 라벨링 도구만 말하고 모델 학습/검사가 별도 실행기 연결 뒤 진행된다는 점은 바로 보이지 않았습니다.
  - 이 상태에서는 사용자가 목적만 바꾸면 YOLO 객체탐지와 같은 학습/추론 흐름이 즉시 제공된다고 오해할 수 있습니다.
- 수정 내용:
  - `WpfLearningWorkflowPanelViewModel`의 세그멘테이션 목적 설명에 `모델 학습/검사는 세그멘테이션 실행기 연결 후 진행` 문구를 추가했습니다.
  - 이상탐지 목적 설명에도 `모델 학습/검사는 이상탐지 실행기 연결 후 진행` 문구를 추가했습니다.
  - 교육 모드 상세도 특정 모델명 고정 대신 목적별 실행기 연결을 전제로 설명하도록 바꿨습니다.
  - 관련 테스트 계약에서 오래된 `U-Net` 고정 기대값과 `AI 후보` 금지 기대값을 제거했습니다.
- 구조:
  - 표시 문구는 `WpfLearningWorkflowPanelViewModel`에 유지했고, XAML/code-behind로 판단을 옮기지 않았습니다.
  - 세그멘테이션/이상탐지 저장, Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-learning-workflow-panel` 통과.
  - `--mvvm-infra` 통과.
  - `--wpf-visual-smoke --review-tab guide --right-workflow-expanded --width 1920 --height 1080 --output artifacts\ui\wpf-purpose-runtime-boundary-after-1920.png` 통과.
  - `git diff --check` 통과. LF/CRLF 경고만 있음.
- 캡처:
  - After 1920: `artifacts\ui\wpf-purpose-runtime-boundary-after-1920.png`
- 다음 작업:
  - 목적별 안내는 정리됐습니다. 다음 우선순위는 모델 비교/모델 이력 쪽에서 여러 모델을 나중에 추가했을 때 실행기/목적/현재 검사 모델이 한 줄에서 헷갈리지 않는지 점검합니다.

## 2026-07-03 WPF learning-guide readable Korean pass

- 점검 결과:
  - 가이드/도구 ViewModel 안에 일부 한글 설명 문자열이 깨진 상태로 남아 있었습니다.
  - 이 영역은 초보 사용자가 데이터셋 목적, 라벨링 도구, 학습/추론 단계를 처음 읽는 위치라 로그보다 우선적으로 깨짐을 막아야 합니다.
- 수정 내용:
  - `WpfLearningWorkflowPanelViewModel`의 깨진 과거 literal switch를 제거하고, 데이터셋 목적/학습 모드/단계/도구 설명을 readable resolver로 통합했습니다.
  - 데이터셋 목적, 학습 모드 설명, 단계 설명, 도구 설명이 항상 읽을 수 있는 한글로 계산되도록 했습니다.
  - 세그멘테이션/이상탐지는 계속 `모델 학습/검사는 해당 실행기 연결 후 진행` 기준을 유지합니다.
  - `--wpf-learning-workflow-panel` 테스트에 표시 문자열이 replacement character 또는 Hanja-range mojibake artifact를 포함하지 않는다는 검증을 추가했습니다.
- 구조:
  - ViewModel 표시 문자열만 변경했습니다.
  - XAML/code-behind, Viewer/OpenGL/ROI/brush/eraser 성능 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-learning-workflow-panel` 통과.
  - `--mvvm-infra` 통과.
  - `--wpf-visual-smoke --review-tab guide --right-workflow-expanded --width 1920 --height 1080 --output artifacts\ui\wpf-learning-guide-readable-source-clean-after-1920.png` 통과.
  - `rg -n "硫|紐|寃|異|釉|媛|怨|瑜|瑗|諛|댁|곗씠|숈뒿|멸렇|뺤" "0. UI/9) WPF/ViewModels/WpfLearningWorkflowPanelViewModel.cs"` 결과 없음.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-purpose-runtime-boundary-after-1920.png`
  - After 1920: `artifacts\ui\wpf-learning-guide-readable-source-clean-after-1920.png`
- 다음 작업:
  - 가이드 한글 깨짐과 소스 literal 정리는 완료했습니다. 다음 우선순위는 목적별 실행기 선택 후 학습/검사 버튼이 왜 가능한지 또는 불가능한지 한 패널 안에서 즉시 이해되는지 점검하는 것입니다.

## 2026-07-03 WPF runtime package action status text pass

- 점검 결과:
  - 모델 실행기 설정에서 Ultralytics 설치/제거 버튼을 누른 직후 표시되는 상태 문구가 깨진 한글로 남아 있었습니다.
  - 이 지점은 사용자가 YOLOv8/YOLO11 설치 테스트와 제거 테스트를 반복할 때 바로 보는 피드백이므로 로그보다 우선해서 읽을 수 있어야 합니다.
- 수정 내용:
  - `WpfYoloModelSettingsPanelViewModel`의 설치 실행 상태 문구를 `Ultralytics 설치를 시작합니다...` 기준으로 정리했습니다.
  - 제거 실행 상태 문구도 `Ultralytics 제거를 시작합니다...` 기준으로 정리하고, 제거 후 self-test를 다시 확인한다는 흐름을 문구에 포함했습니다.
  - `--wpf-yolo-model-settings-panel` 테스트에 설치/제거 클릭 직후 상태 문구가 읽을 수 있는 한글이며 replacement/Hanja-range 깨짐 artifact가 없다는 검증을 추가했습니다.
- 구조:
  - ViewModel의 사용자 상태 문구와 테스트만 변경했습니다.
  - 실제 pip 설치/제거 실행, shell adapter, 확인 다이얼로그, Python 실행 서비스, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-yolo-model-settings-panel` 통과.
  - `--wpf-settings-viewmodels` 통과.
  - `--mvvm-infra` 통과.
  - `rg -n "硫|紐|寃|異|釉|媛|怨|瑜|瑗|諛|댁|곗씠|숈뒿|멸렇|뺤|쒓|ㅼ튂|쒖옉|理" "0. UI/9) WPF/ViewModels/WpfYoloModelSettingsPanelViewModel.cs"` 결과 없음.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output artifacts\ui\wpf-runtime-package-action-status-after-1920.png` 통과.
- 캡처:
  - After 1920: `artifacts\ui\wpf-runtime-package-action-status-after-1920.png`
- 다음 작업:
  - 설치/제거 직후 상태 문구는 고정했습니다. 다음 우선순위는 목적별 실행기 선택 후 학습/현재 검사 버튼이 왜 활성/비활성인지 같은 패널 안에서 즉시 이해되는지 점검하는 것입니다.

## 2026-07-03 WPF workflow command tooltip readable pass

- 점검 결과:
  - 현재 검사/선택 이미지 검사/일괄 검사/실패 재시도/일괄 중지 버튼의 command-state 툴팁 일부가 깨진 한글로 남아 있었습니다.
  - 사용자는 버튼이 비활성화됐을 때 “다른 작업 중인지”, “추론 검토 모드가 아닌지”, “일괄 검사 중에만 가능한지”를 툴팁으로 확인해야 하므로 이 영역은 로그보다 직접적인 안내입니다.
- 수정 내용:
  - `WpfWorkflowCommandStateService`의 검사 버튼 활성/비활성 툴팁을 읽을 수 있는 한글로 정리했습니다.
  - `--wpf-workflow-command-state` 단독 테스트 플래그를 추가했습니다.
  - command-state 테스트에 활성/비활성/작업 중/일괄 검사 중 상태 툴팁이 읽을 수 있는 한글이며 replacement/Hanja-range 깨짐 artifact가 없다는 검증을 추가했습니다.
- 구조:
  - 버튼 활성화 판정은 기존 서비스 계산을 유지했습니다.
  - Shell fanout/code-behind, 실제 검사 실행, 모델 런타임 판정, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-workflow-command-state` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--mvvm-infra` 통과.
  - `rg -n "硫|紐|寃|異|釉|媛|怨|瑜|瑗|諛|댁|곗씠|숈뒿|멸렇|뺤|쒓|ㅼ튂|쒖옉|理|좏깮|대\?|쒖떆|쇨큵|ㅽ뙣" "0. UI/9) WPF/Services/WpfWorkflowCommandStateService.cs"` 결과 없음.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output artifacts\ui\wpf-workflow-command-tooltips-after-1920.png` 통과.
- 캡처:
  - After 1920: `artifacts\ui\wpf-workflow-command-tooltips-after-1920.png`
- 다음 작업:
  - 비활성 버튼 툴팁 문구는 정리했습니다. 다음 우선순위는 실제 런타임 준비 상태 카드와 상단 `현재 검사` 버튼의 비활성 사유가 한 문장으로 같은 원인을 말하는지 점검하는 것입니다.

## 2026-07-03 WPF YOLO runtime status readable pass

- 점검 결과:
  - `WpfLabelingShellWindow.YoloRuntimeStatus.cs`에 상단/상태바로 직접 전달되는 `추론 준비 완료`, `검사 모델 없음`, `모델 후보`, `검사 모델` 문구 일부가 깨진 상태로 남아 있었습니다.
  - 사용자가 현재 검사 모델이 있는지 확인하는 위치이므로, 모델 설정 카드와 상단 상태가 같은 용어로 읽혀야 합니다.
- 수정 내용:
  - 경로 선택 직후 상태 문구를 `선택됨. 저장을 눌러 설정에 반영하세요.`로 정리했습니다.
  - 추론 준비 상태를 `추론: 준비 완료`로 정리했습니다.
  - 검사 모델 미설정/후보/적용 상태 문구를 `검사 모델: 없음`, `모델 후보: ...`, `검사 모델: ...`로 정리했습니다.
  - shell 구성 테스트에 해당 source가 읽을 수 있는 한글 문구를 포함하고 replacement/Hanja-range 깨짐 artifact가 없다는 검증을 추가했습니다.
- 구조:
  - Shell UI adapter의 표시 문자열만 변경했습니다.
  - 런타임 판정, 검사 실행, 모델 저장/후보 결정, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-inference-status-presentation` 통과.
  - `--wpf-workflow-command-state` 통과.
  - `rg -n "硫|紐|寃|異|釉|媛|怨|瑜|瑗|諛|댁|곗씠|숈뒿|멸렇|뺤|쒓|ㅼ튂|쒖옉|理|좏깮|대\?|쒖떆|쇨큵|ㅽ뙣|鍮||꾨즺|놁쓬" "0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloRuntimeStatus.cs"` 결과 없음.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output artifacts\ui\wpf-yolo-runtime-status-readable-after-1920.png` 통과.
- 캡처:
  - After 1920: `artifacts\ui\wpf-yolo-runtime-status-readable-after-1920.png`
- 다음 작업:
  - 상단/상태바의 모델 상태 문구는 정리했습니다. 다음 우선순위는 `PythonModelSettingsValidator.Validate`의 영어 오류 메시지가 사용자 화면에 그대로 노출되는 경로를 한국어 요약으로 감싸는 것입니다.

## 2026-07-03 Python model validator Korean error pass

- 점검 결과:
  - `PythonModelSettingsValidator.Validate`가 모델 실행기 경로/가중치/이미지 폴더/숫자 설정 오류를 영어 문장으로 만들고 있었습니다.
  - 이 메시지는 런타임 상태 카드, 툴팁, 로그로 이어질 수 있으므로 초보 사용자가 바로 조치할 수 있는 한국어 원인 문구가 필요합니다.
- 수정 내용:
  - YOLO 프로젝트 폴더, TCP 클라이언트 스크립트, Python 실행 파일, YOLO 가중치 파일, 이미지 폴더 누락 메시지를 한국어로 정리했습니다.
  - 신뢰도, 검사 시간 제한, 최대 후보 수, 추론 이미지 크기 범위 오류도 한국어로 정리했습니다.
  - `--python-model-settings-validator` 단독 테스트 플래그를 추가하고, validator 테스트 기대값을 한국어 원인 문구 기준으로 갱신했습니다.
- 구조:
  - 오류 생성 문구만 변경했습니다.
  - 런타임 상태 판정, 경로 검증 조건, self-test, connection service, 실제 Python/worker 실행 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--python-model-settings-validator` 통과.
  - `--python-model-runtime-self-test` 통과.
  - `--python-model-runtime-connection` 통과.
  - `rg -n "was not found|must be between|confidence|timeout|Maximum detection candidates|Inference image size" "1. Core/PythonModelSettingsValidator.cs"` 결과 없음.
- 캡처:
  - 레이아웃 변경 없음. 관련 모델 런타임 패널 1920 캡처는 `artifacts\ui\wpf-yolo-runtime-status-readable-after-1920.png`에서 유지 확인했습니다.
- 다음 작업:
  - validator 원문 오류는 한국어화했습니다. 다음 우선순위는 모델 런타임 self-test 항목의 설치 필요/실행 연결/가중치 파일 상태가 beginner-friendly 문구로 모두 이어지는지 점검하는 것입니다.

## 2026-07-03 runtime self-test actionable detail pass

- 점검 결과:
  - 모델 실행기 self-test 카드는 이미 보였지만, 누락 상태의 detail이 `경로 미설정` 또는 원본 경로 중심이라 초보 사용자가 다음 행동을 바로 알기 어려웠습니다.
  - 특히 검사 모델 누락과 Ultralytics 패키지 누락은 버튼/작업 흐름으로 이어져야 합니다.
- 수정 내용:
  - 누락된 프로젝트/모델 루트/이미지 폴더 detail에 `경로를 다시 선택`, `YOLO 프로젝트 폴더 연결`, `검사할 이미지 폴더 선택` 같은 다음 행동을 붙였습니다.
  - 누락된 실행 스크립트와 검사 모델 파일 detail에 `worker 스크립트 연결`, `학습 완료 후 검사 모델로 저장하거나 .pt 파일 선택` 안내를 추가했습니다.
  - Ultralytics 패키지 누락 detail에 `설치 실행 버튼으로 설치한 뒤 다시 점검` 안내를 추가했습니다.
  - self-test 테스트에 missing weights와 missing Ultralytics detail이 해당 행동 문구를 포함하고, self-test report 전체에 깨짐 artifact가 없다는 검증을 추가했습니다.
- 구조:
  - 경로/파일 존재 판정은 그대로 유지했습니다.
  - 외부 설치, Python 실행, worker 실행, shell adapter, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--python-model-runtime-self-test` 통과.
  - `--python-model-runtime-connection` 통과.
  - `--wpf-yolo-model-settings-panel` 통과.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --missing-model-runtime --width 1920 --height 1080 --output artifacts\ui\wpf-runtime-selftest-actionable-detail-after-1920.png` 통과.
- 캡처:
  - After 1920: `artifacts\ui\wpf-runtime-selftest-actionable-detail-after-1920.png`
- 다음 작업:
  - self-test 누락 detail은 action-oriented로 정리했습니다. 다음 우선순위는 설치/연결 결과 카드와 self-test 카드의 용어가 같은지 점검하는 것입니다.

## 2026-07-03 YOLO requirements install status readable pass

- 점검 결과:
  - legacy 요구 패키지 설치 경로의 상태 문구 중 `설치 건너뜀`, `설치 실패`가 깨진 한글로 남아 있었습니다.
  - 이 경로는 최신 Ultralytics 설치 카드와 별개지만, 사용자가 모델 실행 환경 설치를 눌렀을 때 바로 보이는 상태이므로 읽을 수 있어야 합니다.
- 수정 내용:
  - `ExecuteInstallRequirementsCommand`의 오류/실패 상태를 `설치 건너뜀: ...`, `설치 실패: ...`로 정리했습니다.
  - `--wpf-yolo-model-settings-panel` 테스트에서 해당 source가 readable status 문구를 포함하고 깨짐 artifact가 없다는 계약을 추가했습니다.
- 구조:
  - 설치 조건, requirements check, 실제 install 호출, Python 실행 경로는 변경하지 않았습니다.
  - Shell UI adapter의 표시 문자열과 테스트만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-yolo-model-settings-panel` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--mvvm-infra` 통과.
  - `rg -n "硫|紐|寃|異|釉|媛|怨|瑜|瑗|諛|댁|곗씠|숈뒿|멸렇|뺤|쒓|ㅼ튂|쒖옉|理|좏깮|대\?|쒖떆|쇨큵|ㅽ뙣|鍮|꾨즺|놁쓬|먮룞|붾줎|곸슜" "0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloEnvironmentRuntimeCommands.cs"` 결과 없음.
- 캡처:
  - 상태 문구 정리이며 레이아웃 변경 없음. 관련 모델 런타임 패널 1920 캡처는 `artifacts\ui\wpf-runtime-selftest-actionable-detail-after-1920.png`에서 유지 확인했습니다.
- 다음 작업:
  - 설치 상태 문구는 정리했습니다. 다음 우선순위는 설치/제거 최근 결과 카드의 `ExitCode`, stdout/stderr 같은 개발자 용어를 사용자용 요약과 상세로 분리할지 점검하는 것입니다.

## 2026-07-03 Ultralytics package result detail readable pass

- 점검 결과:
  - Ultralytics 설치/제거 최근 결과 detail이 `ExitCode:` 같은 raw 개발자 라벨을 그대로 보여주고 있었습니다.
  - 실패 원인 확인에는 종료 코드와 명령이 필요하지만, 첫 줄은 사용자가 바로 읽는 결과 요약이어야 합니다.
- 수정 내용:
  - `BuildUltralyticsPackageOperationDetail`의 첫 줄을 `결과: ...`로 바꿨습니다.
  - `ExitCode:` 라벨을 `종료 코드:`로 바꾸고, 출력 요약은 `로그 요약:`으로 표시하도록 정리했습니다.
  - 관련 테스트에서 recent result detail이 `결과`, `종료 코드`, `로그 요약`을 포함하고 raw `ExitCode:`를 노출하지 않는지 검증합니다.
- 구조:
  - 패키지 설치/제거 실행, 확인 dialog, stdout/stderr 수집, shell adapter 흐름은 변경하지 않았습니다.
  - 결과 detail 포맷과 테스트만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-yolo-model-settings-panel` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--mvvm-infra` 통과.
  - `rg -n "ExitCode:|결과:|종료 코드|로그 요약" "0. UI/9) WPF/Views/WpfLabelingShellWindow.YoloEnvironmentRuntimeCommands.cs" tests\LabelingApplication.Tests\Program.cs`에서 source에는 raw `ExitCode:` 없음.
- 캡처:
  - 상태 detail 포맷 변경이며 레이아웃 변경 없음. 관련 모델 런타임 패널 1920 캡처는 `artifacts\ui\wpf-runtime-selftest-actionable-detail-after-1920.png`에서 유지 확인했습니다.
- 다음 작업:
  - 최근 결과 detail 포맷은 정리했습니다. 다음 우선순위는 `PythonEnvironmentService`의 requirements check/install 결과 Summary가 영어로 남아 UI에 흘러나오는지 점검하는 것입니다.

## 2026-07-03 Python environment summary Korean pass

- 점검 결과:
  - `PythonEnvironmentService`의 requirements 점검/설치 결과 Summary가 `Missing Python packages`, `Python environment is ready`, `Python requirements installed successfully` 같은 영어 문구를 그대로 반환하고 있었습니다.
  - 이 Summary는 YOLO/model settings 패널의 설치 상태, 최근 실행 결과, 로그 요약으로 이어지므로 초보 사용자가 실패 원인과 완료 여부를 바로 읽을 수 있어야 합니다.
- 수정 내용:
  - 누락 패키지, 준비 완료, requirements 설치 완료, requirements.txt 누락, pip 목록 확인 실패, 패키지 목록 비어 있음, 패키지 이름 오류, Python 프로세스 시작 실패, 명령 시간 초과 문구를 한국어로 정리했습니다.
  - `--python-environment-summaries` 단독 테스트 플래그를 추가했습니다.
  - Summary 객체와 source 문자열을 함께 검증해 이전 영어 고정 문구와 깨짐 artifact가 다시 들어오지 않도록 했습니다.
- 구조:
  - requirements 파싱, pip 실행, install/uninstall 실행 조건은 변경하지 않았습니다.
  - package execution은 계속 `PythonEnvironmentService`가 담당하고, WPF shell/view는 해당 Summary를 표시하는 adapter 역할만 유지합니다.
  - Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--python-environment-summaries` 통과.
  - `--wpf-yolo-model-settings-panel` 통과.
  - `--python-model-runtime-self-test` 통과.
  - `rg -n "Missing Python packages|Python environment is ready|Python requirements installed successfully|No package names were found|Could not inspect installed Python packages|Python package list was empty|requirements.txt was not found|Invalid Python package name|Python process did not start|Python command timed out|Python environment command failed" "1. Core/PythonEnvironmentService.cs" tests\LabelingApplication.Tests\Program.cs`에서 source에는 이전 영어 고정 문구 없음.
- 캡처:
  - 레이아웃 변경 없음. 관련 모델 런타임 패널 1920 캡처는 `artifacts\ui\wpf-runtime-selftest-actionable-detail-after-1920.png`에서 유지 확인했습니다.
- 다음 작업:
  - Python environment Summary는 한국어화했습니다. 다음 우선순위는 모델 런타임 패널/모델 센터에서 runtime-family별 지원 범위(학습 가능, 현재 검사 가능, 설치 필요)가 같은 용어로 표시되는지 점검하는 것입니다.

## 2026-07-03 runtime command failure Korean pass

- 점검 결과:
  - 검사/학습 명령이 전송되지 않을 때 Core 서비스가 `DetectImage was not sent...`, `StartTraining was not sent...`, `YOLO detection timed out...` 같은 영어 실패 문구를 `LastError`와 로그로 남기고 있었습니다.
  - 사용자가 실제 실행 중 실패 원인을 확인하는 경로이므로 로그나 상태 요약에 영어 내부 문장이 그대로 남으면 문제 해결 흐름이 끊깁니다.
- 수정 내용:
  - 현재 검사 요청 미전송, 검사 이미지 누락, 이미지 크기 없음, 검사 통신/결과 서비스 미초기화, 검사 시간 초과, AI 후보 건너뛰기 실패/완료 문구를 한국어로 정리했습니다.
  - 학습 시작/중지 명령 미전송, 학습 통신 미초기화, 학습 준비 점검 실패 문구를 한국어로 정리했습니다.
  - `--runtime-command-failure-messages` 단독 테스트 플래그를 추가하고, 이전 영어 고정 문구가 Core source에 남지 않는지와 `LastError`가 한국어 원인을 포함하는지 검증했습니다.
- 구조:
  - TCP packet, worker 호출, 학습/검사 실행 조건, 후보 overlay 처리, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
  - Core workflow/service의 실패 메시지만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--runtime-command-failure-messages` 통과.
  - `--python-model-status-protocol` 통과.
  - `--wpf-workflow-command-state` 통과.
  - `rg -n "YOLO detection communication is not initialized|YOLO detection result service is not initialized|YOLO detection data is not initialized|StartDefect skipped|DetectImage skipped|DetectImage was not sent|Selected detection candidate cannot|YOLO detection timed out after|YOLO training communication is not initialized|StartTraining was not sent|StopTraining was not sent|YOLO training validation failed" "1. Core/DetectionResultApplicationService.cs" "1. Core/YoloDetectionWorkflowService.cs" "1. Core/YoloTrainingWorkflowService.cs" tests\LabelingApplication.Tests\Program.cs`에서 Core source에는 이전 영어 문구 없음.
- 캡처:
  - 레이아웃 변경 없음. 관련 검사/학습 버튼 상태 1920 캡처는 `artifacts\ui\wpf-workflow-command-tooltips-after-1920.png`에서 유지 확인했습니다.
- 다음 작업:
  - 명령 실패 원인 문구는 한국어화했습니다. 다음 우선순위는 실제 작업 중인 상태에서 로그 하단까지 보지 않아도 상단/패널 상태로 실패 원인을 확인할 수 있는지, 특히 current inspection failure card 쪽을 점검하는 것입니다.

## 2026-07-03 current inspection failure summary pass

- 점검 결과:
  - Core 실패 원인이 `YoloWorkerSmokeTestResult.Summary`로 올라와도, 단일 현재 검사 UI는 실패 시 `추론 실패: 경과시간`과 `실패: 경과시간`만 보여 원인을 상단 상태에서 확인하기 어려웠습니다.
  - 1920 화면에서도 상단 추론 상태 칩이 250px 고정 폭이었고, 숨겨진 progress column이 계속 60px를 차지해 긴 실패 원인이 더 빨리 잘렸습니다.
- 수정 내용:
  - `RunInteractiveDetectionAsync` 실패 상태가 `BuildInteractiveDetectionFailureSummary(result)`를 통해 `Summary/Error/Errors`의 첫 원인을 명령 상태, 상단 추론 상태, 로그에 같이 표시하도록 했습니다.
  - 긴 실패 원인은 상단 상태 칩에서 80자 기준으로 줄이고 tooltip에는 기존 `SetGlobalInferenceStatus` detail을 유지합니다.
  - 상단 inference status column을 250px에서 360px로 늘리고, progress column은 `Auto`로 바꿔 숨김 상태에서 텍스트 공간을 차지하지 않게 했습니다.
  - visual smoke에 `--show-failed-inference-status` 옵션을 추가해 실패 상태 캡처를 만들 수 있게 했습니다.
- 구조:
  - 실제 추론 실행, worker 연결, TCP packet, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
  - WPF shell의 상태 표시와 테스트/visual-smoke fixture만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-single-detection-path` 통과.
  - `--runtime-command-failure-messages` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --show-failed-inference-status --width 1920 --height 1080 --output artifacts\ui\wpf-current-inspection-failure-summary-after-1920.png` 통과.
- 캡처:
  - After 1920: `artifacts\ui\wpf-current-inspection-failure-summary-after-1920.png`
- 다음 작업:
  - 현재 검사 실패 원인은 상단 상태와 로그에 함께 표시됩니다. 다음 우선순위는 일괄 검사 실패에서도 같은 원인 요약이 이미지 큐 행/상단 상태/결과 카드에 같은 용어로 표시되는지 확인하는 것입니다.

## 2026-07-03 batch inspection failure summary pass

- 점검 결과:
  - 일괄 검사는 실패 command/log에는 원인 요약을 갖고 있었지만, 상단 추론 상태는 `일괄 실패: N/M`만 보여 원인을 바로 확인하기 어려웠습니다.
  - 실제 사용자는 하단 로그를 열지 않고도 왜 일괄 검사가 멈췄는지 알아야 하므로 상단 상태에도 같은 실패 원인이 필요합니다.
- 수정 내용:
  - `WpfBatchDetectionProgressService.BuildFailureInferenceStatus`가 실패 원인 요약을 받아 상단 상태에 같이 표시하도록 변경했습니다.
  - 상단 상태가 너무 길어지지 않도록 실패 원인은 80자 기준으로 줄입니다.
  - 일괄 검사 실패 상태 focused 테스트 인자 `--wpf-batch-detection-progress`를 추가했습니다.
  - visual smoke에 `--show-batch-failed-inference-status`를 추가해 1920x1080 상태 표시를 캡처할 수 있게 했습니다.
- 구조:
  - 실제 일괄 검사 실행, worker 연결, TCP packet, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
  - 상태 표시 서비스, shell adapter 호출부, 테스트 fixture만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-batch-detection-progress` 통과.
  - `--wpf-single-detection-path` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --show-batch-failed-inference-status --width 1920 --height 1080 --output artifacts\ui\wpf-batch-inspection-failure-summary-after-1920.png` 통과.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-batch-inspection-failure-summary-before-1920.png`
  - After 1920: `artifacts\ui\wpf-batch-inspection-failure-summary-after-1920.png`
- 다음 작업:
  - 일괄 실패 상단 상태는 실패 원인을 표시합니다. 다음 우선순위는 일괄 검사 중 개별 이미지 행/결과 카드의 실패 사유가 같은 용어와 tooltip으로 이어지는지 점검하는 것입니다.

## 2026-07-03 batch failure result detail pass

- 점검 결과:
  - 일괄 검사 실패가 개별 이미지에 남을 때 이미지 큐 detail/tooltip은 `Detection request failed.` 같은 원문 영어를 그대로 표시할 수 있었습니다.
  - 캔버스 실패 결과 카드도 worker summary를 그대로 selected text에 넣어, 이미 상단 상태에서 정리한 `요청 실패` 같은 사용자용 용어와 맞지 않았습니다.
- 수정 내용:
  - `WpfImageQueuePresenter.BuildDetailText`가 `LastDetectionMessage`를 그대로 붙이지 않고 `TranslateDetectionMessage`를 거쳐 표시하도록 변경했습니다.
  - `WpfDetectionResultPresentationService.BuildFailureOverlay`가 실패 카드 제목을 `검사 실패`로 맞추고, selected/detail에 `결과: ...`, `실패 원인: ...` 형태로 사용자용 실패 이유를 표시하도록 변경했습니다.
  - `--wpf-batch-detection-result` focused 테스트 인자를 추가했습니다.
- 구조:
  - 실제 일괄 검사 실행, review status 저장, 후보 overlay 생성, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
  - 실패 메시지 표시 서비스와 테스트 fixture만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-batch-detection-result` 통과.
  - `--wpf-detection-result-presentation` 통과.
  - `--wpf-image-queue-status` 통과.
- 캡처:
  - `artifacts\ui\wpf-batch-failure-result-card-after-1920.png` 생성은 성공했지만, 현재 visual-smoke fixture에서는 실패 결과 카드가 육안으로 드러나지 않아 이번 항목의 주 검증 근거로 쓰지 않았습니다.
- 다음 작업:
  - 개별 실패 결과의 용어는 정리했습니다. 다음 우선순위는 실패 결과 카드가 visual-smoke/실제 화면에서 명확히 드러나도록 overlay 표시 조건과 카드 위치 fixture를 점검하는 것입니다.

## 2026-07-03 canvas result card visibility pass

- 점검 결과:
  - 캔버스 결과 카드는 WPF `DetectionResultOverlay`로 `WindowsFormsHost` 기반 OpenGL 캔버스 위에 떠 있도록 배치되어 있었습니다.
  - WPF/WinForms airspace 제약 때문에 ViewModel 값은 `Visible`이어도 실제 EXE/캡처에서는 카드가 OpenGL 영역 뒤로 가려질 수 있었습니다.
  - 실패 결과 카드에는 후보 이동/확정/스킵 버튼도 같이 보여, 실패 상태에서 할 수 없는 동작처럼 보이는 문제가 있었습니다.
- 수정 내용:
  - `WpfCanvasPanel.xaml`에서 결과 카드를 OpenGL 캔버스와 같은 row에 띄우지 않고, 캔버스 바로 위 별도 `Auto` row로 이동했습니다.
  - `WpfCanvasPanelViewModel.DetectionOverlayActionsVisibility`를 추가해 `Confirmable/Duplicate` 후보 카드에서만 action 버튼을 보이고, `Review` 상태인 실패/객체없음 결과 카드에서는 숨기도록 했습니다.
  - XAML의 legacy mojibake 제목 텍스트가 ViewModel title을 가리지 않도록 `WpfCanvasPanel.xaml.cs`에서 `DetectionOverlayTitleText` 바인딩을 UI adapter로 연결했습니다.
- 구조:
  - Viewer/OpenGL/ROI/brush/eraser 렌더링 경로는 변경하지 않았습니다.
  - 결과 카드의 배치와 ViewModel 표시 상태만 변경했습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-canvas-detection-overlay` 통과.
  - `--wpf-batch-detection-result` 통과.
  - `--wpf-detection-display-mode` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --show-batch-failure-result-card --width 1920 --height 1080 --output artifacts\ui\wpf-batch-failure-result-card-after-1920.png` 통과.
- 캡처:
  - Before 1920: `artifacts\ui\wpf-batch-failure-result-card-before-1920.png`
  - After 1920: `artifacts\ui\wpf-batch-failure-result-card-after-1920.png`
- 다음 작업:
  - 실패 결과 카드는 실제 화면에서 보이게 됐습니다. 다음 우선순위는 같은 결과 카드가 1366x768 같은 좁은 장비 화면에서도 캔버스 공간을 과도하게 밀지 않는지 확인하는 것입니다.

## 2026-07-03 canvas result card 1366 layout verification

- 점검 결과:
  - 1366x768 화면에서 일괄 검사 실패 결과 카드가 캔버스 상단의 별도 행에 보이고, OpenGL 캔버스 뒤로 가려지지 않았습니다.
  - 실패 카드의 action 버튼은 숨겨져 있어 실패 상태에서 `후보 이동/확정/스킵` 같은 불가능한 동작으로 오해할 여지가 없었습니다.
  - 좌측 학습/모델 패널, 중앙 캔버스, 우측 이미지 큐가 모두 화면 안에 들어왔고, 이미지 큐 상단 컨트롤도 잘리지 않았습니다.
- 수정 내용:
  - 이번 항목은 직전 결과 카드 위치 수정의 1366x768 검증 pass입니다. 추가 코드 변경은 하지 않았습니다.
- 구조:
  - Viewer/OpenGL/ROI/brush/eraser 렌더링 경로는 변경하지 않았습니다.
- 검증:
  - `--wpf-responsive-layout --width 1366 --height 768` 통과.
  - `--wpf-canvas-detection-overlay` 통과.
  - `--wpf-batch-detection-result` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --show-batch-failure-result-card --width 1366 --height 768 --output artifacts\ui\wpf-batch-failure-result-card-after-1366.png` 통과.
- 캡처:
  - 1366 검증: `artifacts\ui\wpf-batch-failure-result-card-after-1366.png`
- 다음 작업:
  - 결과 카드 1920/1366 표시 문제는 검증 완료로 보고, 다음 우선순위는 학습/모델 센터에서 YOLOv5 현재 검사와 Ultralytics 계열 미지원 상태가 같은 용어로 표시되는지 이어서 점검합니다.

## 2026-07-03 inspection model runtime chip pass

- 점검 결과:
  - 1366x768 화면에서 상단 추론 상태와 전용 검사 모델 칩은 모델 파일명만 보여 `best.pt`가 YOLOv5인지 YOLO11인지 즉시 알기 어려웠습니다.
  - 좌측 학습/모델 패널을 열면 런타임이 보이지만, 실제 작업 중에는 상단 상태만 보고 현재 검사 모델을 판단하는 경우가 많습니다.
- 수정 내용:
  - `WpfInferenceStatusPresentationService`가 검사 모델/후보 표시 문자열에 런타임 패밀리(`YOLOv5`, `YOLOv8`, `YOLO11`, `ONNX`)를 같이 넣도록 변경했습니다.
  - 상단 전용 검사 모델 칩도 같은 presentation service 값을 사용하도록 변경해 `검사 모델: YOLOv5 / best.pt` 형태로 보이게 했습니다.
  - tooltip에는 추론 상태, 런타임, 전체 모델 파일 경로를 같이 표시합니다.
- 구조:
  - 런타임/모델 표시 규칙은 service에 두고, WPF shell은 ViewModel에 전달하는 UI adapter 역할만 유지했습니다.
  - 실제 추론 실행, worker 연결, TCP packet, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-inference-status-presentation` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-status-panels` 통과.
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --width 1366 --height 768 --output artifacts\ui\wpf-inspection-model-runtime-chip-after-1366.png` 통과.
- 캡처:
  - After 1366: `artifacts\ui\wpf-inspection-model-runtime-chip-after-1366.png`
- 다음 작업:
  - 상단 검사 모델 표시에는 런타임명이 붙었습니다. 다음 우선순위는 YOLOv8/YOLO11 선택 상태에서 실행 차단 사유가 상단/모델 센터/설정 패널에서 같은 단어로 이어지는지 점검하는 것입니다.

## 2026-07-03 YOLO11 runtime state audit

- 점검 결과:
  - `--ultralytics-runtime-ready` 1366x768 캡처에서 상단 추론 상태와 전용 검사 모델 칩이 `YOLO11 / yolo11n.pt`를 표시했습니다.
  - 좌측 학습/모델 패널의 현재 검사 모델 프로필도 `YOLO11 Ultralytics / adapter-yolov11 / 현재 검사 가능 / 학습 미지원`을 보여, 사용자가 현재 검사는 가능하고 학습은 아직 미지원임을 같은 화면에서 확인할 수 있었습니다.
- 수정 내용:
  - 이번 항목은 방금 추가한 런타임명 표시 개선의 YOLO11 상태 검증입니다. 추가 코드 변경은 하지 않았습니다.
- 검증:
  - `--wpf-visual-smoke --review-tab yolo-model --right-workflow-expanded --ultralytics-runtime-ready --width 1366 --height 768 --output artifacts\ui\wpf-ultralytics-execution-guard-audit-1366.png` 통과.
- 캡처:
  - 1366 audit: `artifacts\ui\wpf-ultralytics-execution-guard-audit-1366.png`
- 다음 작업:
  - YOLO11 현재 검사 가능/학습 미지원 상태의 첫 화면 표시는 현재 기준으로 충분합니다. 다음 우선순위는 실제 `현재 검사` 버튼 실행 결과가 YOLO11 adapter key와 결과 로그/후보 표시까지 일관되게 이어지는지 확인하는 것입니다.

## 2026-07-03 DetectImage adapter-key regression pass

- 점검 결과:
  - `LearningProtocol.BuildDetectImagePacket`은 이미 `model` 필드를 지원하고 있었고, `DetectionResultApplicationService`도 `PythonModelSettings.GetProtocolModelName()` 값을 `SendDetectImage(..., model)`로 넘기고 있었습니다.
  - 기존 TCP round-trip 테스트는 현재 이미지 bitmap fallback인 `StartDefect` 경로를 검증하고 있어, 파일 경로 기반 `DetectImage` JSON 요청의 adapter key는 직접 보호하지 못했습니다.
- 수정 내용:
  - `--yolo-detection-workflow-validation` focused 테스트 플래그를 추가했습니다.
  - 같은 테스트 안에서 mock TCP client가 `DetectImage` JSON line을 읽고 `"model":"yolo11"`이 실제 요청에 포함되는지 검증하도록 했습니다.
- 구조:
  - 실제 프로토콜 생성, TCP 전송, worker 실행, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
  - 테스트가 WPF guard와 Core packet 경계를 명확히 나눕니다. WPF는 실행 가능 여부를 막고, Core packet은 선택된 protocol model key를 잃지 않아야 합니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--yolo-detection-workflow-validation` 통과.
  - `--python-model-runtime-self-test` 통과.
  - `--wpf-yolo-model-settings-panel` 통과.
  - `--wpf-inference-status-presentation` 통과.
  - `--wpf-labeling-shell` 통과.
- 캡처:
  - UI 변경 없음. 직전 검증 캡처 `artifacts\ui\wpf-inspection-model-runtime-chip-after-1366.png`와 `artifacts\ui\wpf-ultralytics-execution-guard-audit-1366.png`의 표시 흐름을 코드 테스트로 보강했습니다.
- 다음 작업:
  - 현재 검사 요청의 adapter key는 보호됐습니다. 다음 우선순위는 실제 결과 로그/후보 리스트에 `YOLOv5/YOLO11` 같은 실행 모델 출처가 남는지 점검하는 것입니다.

## 2026-07-03 detection result model-source summary pass

- 점검 결과:
  - `DetectImage` 요청에는 선택된 adapter key가 들어가지만, 성공 결과 요약은 `추론 완료. 후보:N` 형태라 로그/결과만 나중에 보면 어떤 런타임/모델 파일 결과인지 알기 어려웠습니다.
  - 여러 모델을 비교하는 프로그램 방향에서는 검사 결과 요약과 로그에도 `YOLOv5 / best.pt`, `YOLO11 / yolo11n.pt` 같은 실행 모델 출처가 남아야 합니다.
- 수정 내용:
  - `WpfInferenceStatusPresentationService.BuildRuntimeModelLabel`을 추가해 현재 런타임 패밀리와 모델 파일명을 `YOLOv5 / exp7\best.pt`, `YOLO11 / yolo11n.pt` 형태로 재사용할 수 있게 했습니다.
  - `RunWorkerDetectionForImageAsync`가 추론 시작 로그, 성공 `YoloWorkerSmokeTestResult.Summary`, 완료 Python status, 추론 시간 로그에 같은 모델 출처를 포함하도록 했습니다.
- 구조:
  - 모델 출처 표시 문자열은 service에서 만들고, WPF shell은 실행 흐름에서 그 값을 붙이는 adapter 역할만 유지했습니다.
  - 실제 detection request, TCP packet, worker 실행, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-single-detection-path` 통과.
  - `--wpf-inference-status-presentation` 통과.
  - `--yolo-detection-workflow-validation` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-responsive-layout --width 1366 --height 768` 통과.
- 캡처:
  - UI 배치 변경 없음. 상태 칩 시각 기준은 `artifacts\ui\wpf-inspection-model-runtime-chip-after-1366.png` 유지.
- 다음 작업:
  - 요청 adapter key와 결과 요약 모델 출처는 보호됐습니다. 다음 우선순위는 일괄 검사 완료/실패 행의 모델 출처 표시도 같은 기준으로 맞출지 점검하는 것입니다.

## 2026-07-03 batch detection model-source log pass

- 점검 결과:
  - 단일/현재 검사 결과 summary에는 모델 출처가 들어가도록 보강했지만, 일괄 검사 시작/항목 완료/항목 실패/전체 완료 로그는 여전히 범위, 개수, 후보 수, 시간만 표시했습니다.
  - 여러 모델을 번갈아 일괄 검사하면 하단 로그만 보고 어떤 모델 결과인지 구분하기 어렵습니다.
- 수정 내용:
  - `WpfBatchDetectionProgressService`의 시작/항목 완료/항목 실패/전체 완료 로그 생성 메서드가 선택적으로 `modelSourceText`를 받아 `모델:YOLOv5 / best.pt` 형태를 붙이도록 했습니다.
  - `RunBatchDetectionAsync`가 `WpfInferenceStatusPresentationService.BuildRuntimeModelLabel`로 같은 모델 출처 라벨을 만들고 batch progress service에 전달하도록 했습니다.
- 구조:
  - 일괄 로그 문구는 service에 유지했고, shell은 현재 설정에서 모델 라벨을 한 번 구해 전달하는 adapter 역할만 합니다.
  - 실제 batch detection 실행, TCP packet, worker 실행, queue status 저장, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-batch-detection-progress` 통과.
  - `--wpf-single-detection-path` 통과.
  - `--wpf-inference-status-presentation` 통과.
  - `--yolo-detection-workflow-validation` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-responsive-layout --width 1366 --height 768` 통과.
- 캡처:
  - UI 배치 변경 없음. 로그 문구/테스트 계약만 변경했습니다.
- 다음 작업:
  - 검사 요청, 단일 결과, 일괄 로그의 모델 출처는 같은 기준으로 맞췄습니다. 다음 우선순위는 모델 비교/후보 검토 화면에서 이 출처가 사용자에게 충분히 이어지는지 점검하는 것입니다.

## 2026-07-03 candidate review model-comparison source pass

- 점검 결과:
  - 검사 요청, 단일 검사 결과, 일괄 검사 로그에는 모델 출처가 남지만, Candidate Review의 `학습 모델 검증` 카드는 모델 차이 예시와 다음 행동만 보여서 어떤 현재 검사 모델과 어떤 학습 후보를 비교했는지 바로 확인하기 어려웠습니다.
  - 모델을 여러 개 비교하는 제품 방향에서는 후보 검증 카드 안에서 `현재 검사 모델 -> 학습 후보` 흐름이 직접 보여야 합니다.
- 수정 내용:
  - `WpfInferenceStatusPresentationService.BuildModelComparisonSourceText`를 추가해 런타임 패밀리와 현재/후보 모델 파일을 한 줄 비교 대상으로 표시하도록 했습니다.
  - `WpfCandidateReviewPanelViewModel.ModelComparisonSourceText`와 `SetModelComparisonSourceText`를 추가하고, `WpfCandidateReviewPanel.xaml`의 `ModelComparisonReviewPanel` 제목 바로 아래에 바인딩했습니다.
  - `UpdateCandidateModelComparisonReviewPanel`이 `WpfTrainingWeightsComparison`의 current/latest weights를 service에 넘겨 Candidate Review ViewModel에 반영하도록 했습니다.
- 구조:
  - 비교 대상 문구 생성은 service가 담당하고, ViewModel은 상태를 보관하며, shell은 현재 비교 경로를 전달하는 adapter 역할만 합니다.
  - 모델 비교 실행, worker/TCP 추론 요청, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-candidate-review-panel` 통과.
  - `--wpf-inference-status-presentation` 통과.
  - `--wpf-labeling-shell` 통과.
  - `--wpf-responsive-layout --width 1920 --height 1080` 통과.
  - `--wpf-visual-smoke --review-tab candidates --width 1920 --height 1080` 통과.
- 캡처:
  - 이전: `artifacts\ui\wpf-candidate-review-compact-after-1920.png`
  - 이후: `artifacts\ui\wpf-candidate-model-source-after-1920.png`
- 다음 작업:
  - 후보 검증 카드의 모델 출처는 보강됐습니다. 다음 우선순위는 모델 채택/거절 이후 이력 목록과 상단 검사 모델 배지가 같은 모델 파일을 가리키는지 end-to-end로 확인하는 것입니다.

## 2026-07-03 model adoption consistency recheck

- 점검 결과:
  - 후보 모델을 검사 모델로 저장한 뒤 상단 검사 모델 배지, 학습/모델 센터의 현재 검사 모델 카드, 모델 레지스트리 이력은 같은 `exp\best.pt`를 가리키는지 기존 스모크로 재검증했습니다.
  - 이 영역은 이미 model-center confirm-save 계약으로 보호되어 있어, 이번 패스에서는 코드 수정 없이 재검증만 수행했습니다.
- 검증:
  - `--wpf-yolo-training-session-smoke --model-center --confirm-model-save --width 1920 --height 1080` 통과.
- 캡처:
  - `artifacts\ui\wpf-model-adoption-consistency-verified-1920.png`
- 다음 작업:
  - 저장/채택 흐름은 재검증됐습니다. 다음 우선순위는 거절 흐름도 같은 수준으로 상단 상태/모델 센터/후보 검증 카드가 일관되게 유지되는지 확인하는 것입니다.

## 2026-07-03 model rejection consistency smoke

- 점검 결과:
  - 기존 스모크는 후보 저장/채택 후 상태 일관성은 확인했지만, 후보 거절 후 기준 검사 모델로 되돌아가는 UI 흐름은 같은 수준으로 직접 확인하지 않았습니다.
- 수정 내용:
  - `--wpf-yolo-training-session-smoke`에 `--reject-model-candidate` 옵션을 추가했습니다.
  - 학습 후보가 생성된 뒤 후보 거절 커맨드를 실행하고, 상단 검사 모델 배지와 모델 센터 현재 검사 모델이 기준 `old.pt`로 돌아오며 후보 결정 카드가 `거절` 상태를 표시하는지 검증합니다.
- 구조:
  - 테스트 옵션만 추가했습니다. 실제 후보 거절 로직, recipe 저장, 모델 레지스트리 기록, detection/Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `--wpf-yolo-training-session-smoke --model-center --reject-model-candidate --width 1920 --height 1080 --output artifacts\ui\wpf-model-reject-consistency-after-1920.png` 통과.
- 캡처:
  - `artifacts\ui\wpf-model-reject-consistency-after-1920.png`
- 다음 작업:
  - 후보 저장과 거절의 모델 상태 일관성은 스모크로 보호됐습니다. 다음 우선순위는 모델센터/YOLO 설정/후보 검증 카드에서 사용자가 같은 행동 버튼을 중복으로 보지 않도록 버튼 우선순위를 더 정리하는 것입니다.

## 2026-07-03 model-center duplicate top action cleanup

- 점검 결과:
  - 학습/모델 센터가 오른쪽 작업 패널의 주 화면으로 열린 상태에서도 상단 workflow summary 오른쪽에 `후보 검증`, `검사 모델로 저장`, `현재 검사` 보조 버튼이 한 번 더 표시됐습니다.
  - 같은 모델 행동이 모델 센터 우선순위 카드, 상단 보조 영역, lifecycle 영역에 반복되면 사용자가 어느 버튼을 눌러야 하는지 불필요하게 다시 판단해야 합니다.
- 수정 내용:
  - `WpfLabelingShellViewModel.IsWorkflowStageModelActionPanelVisible`을 추가했습니다.
  - 모델 센터가 열린 학습/모델 단계에서는 상단 `WorkflowStageModelActionPanel`을 접고, 모델 행동은 모델 센터 카드 안의 버튼으로 집중되도록 했습니다.
  - lifecycle 영역의 중복 모델 행동 버튼 3개는 숨겼습니다. 학습 시작/중지 버튼은 그대로 유지했습니다.
  - XAML의 단계 조건 DataTrigger를 제거하고 ViewModel visibility 바인딩으로 변경했습니다.
- 구조:
  - 표시 조건은 ViewModel 상태로 관리합니다.
  - View는 `BooleanToVisibilityConverter`로 상태를 표시만 하며, 모델 후보 검토/저장/현재 검사 커맨드 자체는 변경하지 않았습니다.
  - 학습 실행, 모델 비교 실행, 추론 실행, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-no-duplicate-top-actions-after-1920.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-single-action-zone-after-1920.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080` 통과.
  - `git diff --check -- "0. UI/9) WPF/ViewModels/WpfLabelingShellViewModel.cs" "0. UI/9) WPF/Views/WpfLabelingShellWindow.xaml" tests/LabelingApplication.Tests/Program.cs` 통과. LF/CRLF 경고만 표시됐습니다.
- 캡처:
  - 이전: `artifacts\ui\wpf-model-reject-consistency-after-1920.png`
  - 이후: `artifacts\ui\wpf-model-center-single-action-zone-after-1920.png`
- 다음 작업:
  - 모델 작업 버튼은 한 곳으로 모였습니다. 다음 우선순위는 모델 센터의 이력/비교 설명이 길어질 때 “현재 검사 모델 / 학습 후보 / 선택 이력”이 한눈에 비교되는지 점검하는 것입니다.

## 2026-07-03 model-history comparison role labels

- 점검 결과:
  - 모델 센터의 모델 이력 상세에는 현재 검사 모델과 선택 이력 모델의 비교 문구가 있었지만, 두 열의 역할 라벨이 별도로 없어 긴 모델 경로가 보일 때 어느 쪽이 현재 모델이고 어느 쪽이 선택한 이력인지 한 번 더 읽어야 했습니다.
- 수정 내용:
  - `SelectedModelHistoryComparisonPanel` 안에 `현재 검사 모델`, `선택 이력 모델` 역할 라벨을 추가했습니다.
  - 중첩 카드나 추가 패널을 만들지 않고 기존 얇은 비교 영역 안에서 역할만 분리했습니다.
  - 좁은 화면에서 비교가 이력 리스트 아래로 밀리지 않도록 선택 모델 비교 영역을 모델 이력 리스트 위로 올리고, 이력 리스트 최대 높이를 낮췄습니다.
  - ViewModel 비교 문구의 `선택 모델` 표현을 `선택 이력`으로 통일했습니다.
- 구조:
  - ViewModel의 모델 이력 상태와 비교 문구 생성은 그대로 유지했습니다.
  - XAML은 기존 ViewModel 바인딩을 표시하고 역할 라벨만 추가합니다.
  - 모델 레지스트리 저장/적용, 학습 실행, 추론 실행, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --model-registry` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-history-role-labels-after-1920.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1366 --height 768` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output artifacts\ui\wpf-model-history-comparison-above-list-after-1366.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-history-comparison-above-list-after-1920.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1366 --height 768 --output artifacts\ui\wpf-model-history-selected-history-wording-after-1366.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-history-selected-history-wording-after-1920.png` 통과.
- 캡처:
  - 이전: `artifacts\ui\wpf-model-center-single-action-zone-after-1920.png`
  - 이후: `artifacts\ui\wpf-model-history-selected-history-wording-after-1920.png`
  - 좁은 화면: `artifacts\ui\wpf-model-history-selected-history-wording-after-1366.png`
- 다음 작업:
  - 모델 이력 비교는 좁은 화면에서도 먼저 보이도록 정리됐습니다. 다음 우선순위는 모델 센터의 상태 문구가 길어질 때 상단 상태/좌측 센터/로그가 같은 모델 파일명을 일관되게 줄여 보여주는지 확인하는 것입니다.

## 2026-07-03 dataset context default text encoding guard

- 점검 결과:
  - 데이터셋을 아직 열지 않은 첫 화면에서 저장 위치와 이미지 폴더 안내 문구가 ViewModel 기본값으로 먼저 노출됩니다.
  - 해당 기본 문구는 사용자가 처음 보는 데이터셋 맥락 문구라서, 콘솔/소스 인코딩 차이로 깨진 문자열이 다시 들어가면 첫 화면 신뢰도가 바로 떨어집니다.
- 수정 내용:
  - `WpfLabelingShellViewModel`의 데이터셋 저장 위치/원본 이미지 폴더 기본 문구를 소스 인코딩에 덜 민감한 `\u` 이스케이프 문자열로 고정했습니다.
  - `--wpf-labeling-shell` 테스트에 기본 저장 위치/이미지 루트 문구가 읽을 수 있는 한국어 라벨을 포함하는지 확인하는 가드를 유지했습니다.
- 구조:
  - 상태와 문구 기본값은 ViewModel에 남겼습니다.
  - View/XAML/code-behind, 데이터셋 로딩, 라벨 저장, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
- 캡처:
  - UI 배치 변경이 아니라 기본 문자열/테스트 가드 변경이므로 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 모델 센터와 상단 상태/로그가 같은 모델 파일명을 어떻게 줄여 보여주는지 일관성 점검입니다.

## 2026-07-03 model file display-name regression guard

- 점검 결과:
  - 상단 검사 모델 배지와 모델 센터는 이미 `FormatWeightsDisplayPath` 기준으로 긴 학습 결과 경로를 `exp7\best.pt`처럼 줄여 보여주고 있습니다.
  - 다만 모델 레지스트리 테스트는 `best.pt` 포함 여부만 확인하고 있어, 여러 학습 run이 모두 `best.pt`를 만들 때 run 폴더가 사라지는 퇴행을 충분히 막지 못했습니다.
- 수정 내용:
  - `--wpf-labeling-shell` 테스트에서 모델 레지스트리 후보 행과 compact summary가 `exp7\best.pt` 형태의 run 폴더 포함 표시명을 유지하는지 확인하도록 보강했습니다.
  - 같은 후보 경로에 대해 `WpfInferenceStatusPresentationService.BuildRuntimeModelLabel`도 `YOLOv5 / exp7\best.pt`로 표시하는지 함께 확인해, 상단 상태와 모델 센터가 같은 축약 기준을 쓰도록 잠갔습니다.
- 구조:
  - 프로덕션 코드는 변경하지 않았습니다.
  - 모델 레지스트리 표시, 상단 검사 모델 표시, 학습 결과 경로 축약은 기존 ViewModel/Service 경계를 유지합니다.
  - 학습 실행, 추론 실행, 후보 overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
- 캡처:
  - 테스트 가드 보강만 수행했으므로 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 모델 파일 축약 기준은 테스트로 보호했습니다. 다음 우선순위는 08:00 이후 이어서 모델센터/튜토리얼 문서의 남은 레거시 깨진 문자열과 공개 문서 문구를 정리하는 것입니다.

## 2026-07-03 public tutorial path/privacy recheck

- 점검 결과:
  - 공개 튜토리얼/README에 개인 PC 로컬 경로, 특정 Test 데이터셋명, 대화 흔적, 임시 검증 메모가 다시 들어가면 포트폴리오 문서로 쓰기 어렵습니다.
  - PowerShell 기본 출력은 UTF-8 문서를 깨진 글자로 보여줄 수 있으므로, 문서 내용은 `-Encoding UTF8` 기준으로 확인했습니다.
- 확인 내용:
  - `docs\tutorial\README.md`를 UTF-8로 읽었을 때 제목과 본문이 정상 한국어로 표시됨을 확인했습니다.
  - `README.md`, `docs\tutorial\README.md`, `docs\tutorial\labeling-workbench-tutorial.html`, `docs\tutorial\labeling-workbench-tutorial-standalone.html`에서 `C:\`, `D:\`, `Test01`, `TEST_`, `제가`, `당신`, `Codex`, `이번 확인`, `사용한 데이터`, `artifacts\run`, `LabelingData` 패턴이 잡히지 않았습니다.
- 수정 내용:
  - 공개 문서 본문 수정은 필요하지 않았습니다.
  - 이번 재검수 결과만 작업 추적에 기록했습니다.
- 검증:
  - `Select-String -Encoding UTF8 -Path docs\tutorial\README.md,docs\tutorial\labeling-workbench-tutorial.html,docs\tutorial\labeling-workbench-tutorial-standalone.html,README.md -Pattern "C:\\|D:\\|Test01|TEST2|TEST_|제가|당신|Codex|AI가|소통|이번 확인|사용한 데이터|Debug\\DATA|artifacts\\run|LabelingData"` 결과 없음.
  - `Get-Content -Encoding UTF8 -Path docs\tutorial\README.md -TotalCount 80` 정상 한국어 출력 확인.
- 다음 작업:
  - 공개 튜토리얼 본문은 현재 경로/대화 흔적 기준으로 깨끗합니다. 이후 문서 캡처나 HTML을 다시 만들 때도 같은 검색을 반복해야 합니다.

## 2026-07-03 readable test-string cleanup

- 점검 결과:
  - WPF/Core/Runtime 소스에는 실제 깨진 한글 조각이 잡히지 않았습니다.
  - `tests\LabelingApplication.Tests\Program.cs`에는 과거 레거시 테스트 단언 몇 개가 `繞벿살탮`, `熬곣뫁`, `嶺뚣끉` 같은 깨진 문자열을 기대하거나 금지하는 형태로 남아 있었습니다.
  - 테스트 기대값이 깨진 문자열이면 이후 UI 문구를 다시 손볼 때 잘못된 기준이 될 수 있습니다.
- 수정 내용:
  - 배치 진행 source-negative 단언을 `일괄 검사 항목 완료`, `일괄 검사 항목 실패`, `최근` 같은 읽을 수 있는 한국어 기준으로 바꿨습니다.
  - 후보 검토 중복/비교 단언을 `현재 라벨`, `중복 가능`, `크기`, `위치`, `중복` 기준으로 바꿨습니다.
  - 이미지 로드 status source-negative 단언을 `데이터셋`, `모델` 기준으로 바꿨습니다.
- 구조:
  - 테스트 기대값만 정리했습니다.
  - ViewModel/Service 생산 문구, 후보 검토 워크플로우, 배치 검사 실행, 이미지 로드 실행, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - WPF/Core/Runtime/테스트 범위 UTF-8 스캔에서 `\uFFFD`, Hanja-range mojibake 문자가 더 이상 잡히지 않음.
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-candidate-review-panel` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-batch-detection-progress` 통과.
- 캡처:
  - 테스트 문자열 정리만 수행했으므로 신규 UI 캡처는 만들지 않았습니다.
- 다음 작업:
  - 깨진 테스트 기대값은 정리됐습니다. 08:00 이후에는 실제 화면 캡처가 필요한 UI 변경만 선별해서 진행합니다.

## 2026-07-03 public tutorial documentation guard

- 점검 결과:
  - 공개 README/튜토리얼은 한 번 수동으로 정리해도 이후 캡처 갱신이나 HTML 재생성 중 개인 PC 경로, 특정 테스트 데이터셋명, 대화 흔적이 다시 들어올 수 있습니다.
  - standalone HTML은 다른 PC에 복사해서 볼 수 있어야 하므로, 캡처 이미지가 상대 파일 경로에 의존하면 안 됩니다.
- 수정 내용:
  - `--priority-workflow-docs`에 공개 문서 금지 패턴 검사를 추가했습니다.
  - 검사 대상은 `README.md`, `docs/tutorial/README.md`, 일반 튜토리얼 HTML, standalone 튜토리얼 HTML입니다.
  - 금지 기준은 `C:\`, `D:\`, `LabelingData`, `Test01`, `TEST_`, `artifacts\run`, `AppData`, `Codex`, `제가`, `당신`, `소통`, `이번 확인`, `사용한 데이터` 같은 로컬/대화/임시 검증 흔적입니다.
  - 일반 HTML의 `src="images/..."` 캡처 수와 standalone HTML의 `src="data:image..."` 임베드 수가 같고, 최소 10장 이상인지 확인합니다.
  - 일반 HTML의 모든 캡처가 `images/annotated/` 아래 `*-annotated.png`를 참조하고, alt 문구에 `번호와 화살표`가 포함되는지 확인합니다.
  - 참조한 튜토리얼 캡처 파일이 실제로 존재하고, 1920 기준 캡처 또는 실제 데이터셋 설정 화면 캡처 이름을 유지하는지 확인합니다.
- 구조:
  - 문서 본문은 이번 패스에서 다시 바꾸지 않았습니다.
  - 공개 문서 품질 기준만 테스트로 고정했습니다.
  - 앱 UI, ViewModel/Service, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
- 캡처:
  - 문서 검수 테스트 추가만 수행했으므로 신규 UI 캡처는 만들지 않았습니다.
- 다음 작업:
  - 공개 문서의 경로/대화 흔적과 standalone 이미지 임베드는 테스트로 보호됐습니다. 다음 우선순위는 08:00 이후 실제 화면 기준 UI 변경만 선별해 진행하는 것입니다.

## 2026-07-03 1920 final UX visual audit before 08:00

- 점검 결과:
  - 08:00 전 마지막 시각 점검으로 학습/모델 센터와 후보 검토 화면을 1920x1080 기준으로 다시 캡처했습니다.
  - 모델 센터 화면은 상단 중복 모델 액션이 접힌 상태를 유지하고, 현재 검사 모델/학습 후보/모델 적용 판단/모델 레지스트리 정보가 왼쪽 학습/모델 패널 안에서 확인됩니다.
  - 후보 검토 화면은 `AI 후보`, `저장 전`, `라벨 확정`, `전체 라벨화`, `후보 숨김`, 현재/학습 후보 모델 출처가 첫 화면에서 보입니다.
  - 오른쪽 이미지 큐는 저장/AI 후보/다음 미완료 상태와 전체 자동 저장 버튼을 노출합니다.
- 수정 내용:
  - 이번 항목은 시각 점검만 수행했습니다. 08:00 직전 새 레이아웃 변경을 시작하지 않았습니다.
- 구조:
  - 앱 코드, ViewModel/Service, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-training-session-smoke --model-center --width 1920 --height 1080 --output artifacts\ui\wpf-model-center-final-audit-1920.png` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-visual-smoke --review-tab candidates --width 1920 --height 1080 --output artifacts\ui\wpf-candidate-review-final-audit-1920.png` 통과.
- 캡처:
  - `artifacts\ui\wpf-model-center-final-audit-1920.png`
  - `artifacts\ui\wpf-candidate-review-final-audit-1920.png`
- 다음 작업:
  - 다음 세션에서는 새 구조 변경을 시작하기 전에 이 두 캡처를 기준으로 실제 사용자가 오래 머무는 후보 검토/모델 센터의 문구 밀도와 버튼 우선순위를 다시 판단합니다.

## 2026-07-03 public README wording rule

- 점검 결과:
  - 공개 README에 `포트폴리오`처럼 작성자 개인 목적을 드러내는 섹션명이 남아 있었습니다.
  - 공개 GitHub 문서는 제품과 사용 흐름을 설명해야 하며, 작성자만 알면 되는 사정이나 이전 대화 맥락은 내부 작업 문서에만 남겨야 합니다.
- 수정 내용:
  - `README.md`의 `포트폴리오에서 보여주고 싶은 부분` 섹션명을 `프로젝트의 핵심 흐름`으로 바꿨습니다.
  - `--priority-workflow-docs` 공개 문서 가드에 `포트폴리오`, `내가`, `저만` 금지 기준을 추가했습니다. `나만`은 `하나만` 같은 정상 안내 문구를 잘못 잡을 수 있어 제외했습니다.
  - `CODEX_NEXT_PROMPT.md`에 공개 README/튜토리얼 작성 시 개인 대화 맥락, 저자만 아는 사정, 로컬 PC 경로를 쓰지 말라는 지침을 추가했습니다.
- 구조:
  - 공개 문서 문구와 문서 테스트만 수정했습니다.
  - 앱 UI, ViewModel/Service, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
- 다음 작업:
  - 공개 문서 수정 후에는 반드시 같은 가드를 돌려 README/튜토리얼에 개인 맥락이 다시 들어가지 않았는지 확인합니다.

## 2026-07-03 dataset image-root resolver service split

- 점검 결과:
  - 데이터셋 전환 시 어느 이미지 폴더를 다시 열지 결정하는 순수 로직이 `WpfLabelingShellWindow.DatasetSetupCommands.cs` 안에 남아 있었습니다.
  - 이 로직은 UI adapter보다 데이터셋/큐 상태 판단에 가까워 View code-behind에 계속 두면 이후 Test/Test2처럼 저장 경로와 원본 이미지 폴더가 갈리는 상황을 다시 수정하기 어렵습니다.
- 수정 내용:
  - `WpfDatasetImageRootResolver`를 추가해 명시 이미지 폴더 우선, 데이터셋 내부 train/valid/test 이미지 폴더 fallback, 기본 이미지 루트 판정을 서비스로 분리했습니다.
  - Shell은 현재 `CData`, 설정된 이미지 루트, 큐 이미지 존재 여부 adapter만 넘기고 반환된 폴더를 로드하도록 축소했습니다.
  - `--wpf-image-queue-selection-service` focused 옵션을 추가하고, 명시 폴더 우선 및 데이터셋 split fallback을 직접 검증했습니다.
  - 기존 `나만` 공개 문서 금지 기준 기록은 `하나만` 같은 정상 문구를 잘못 잡는 false positive라 작업 문서에서 정정했습니다.
- 구조:
  - 데이터셋 이미지 루트 선택 판단은 서비스로 이동했습니다.
  - 실제 이미지 로드, 라벨 저장/조회, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
- 캡처:
  - UI 배치 변경이 아니라 서비스 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `ImageQueueCommands` 안의 필터/검색/선택 상태 조합 중 ViewModel 또는 서비스로 더 뺄 수 있는 작은 단위를 고르는 것입니다.

## 2026-07-03 image queue search single-match service split

- 점검 결과:
  - 이미지 큐의 필터/검색 표시 여부는 이미 `WpfImageQueueFilterService`가 맡고 있었습니다.
  - 하지만 `FindSingleSearchMatchedQueueItem`, 단일 visible row fallback, 검색 일치 개수 진단은 Shell code-behind에서 `Take(2)`/`ShouldShow` 조합을 직접 반복하고 있었습니다.
  - 이 판단은 UI control adapter가 아니라 큐 필터 정책에 가까워 서비스로 모으는 편이 이후 검색/필터 UX 변경 시 안전합니다.
- 수정 내용:
  - `WpfImageQueueFilterService.FindSingleItem`, `FindSingleSearchMatch`, `CountSearchMatches`를 추가했습니다.
  - Shell은 현재 view를 refresh하고, 서비스가 반환한 단일 row를 선택/스크롤/버튼 상태 갱신하는 역할만 남겼습니다.
  - source guard 테스트가 Shell에 필터 검색 정책이 다시 중복되지 않도록 확인하게 바꿨습니다.
- 구조:
  - 큐 검색/필터 ambiguity 정책은 서비스로 이동했습니다.
  - 실제 이미지 로드, 저장 라벨 조회, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service` 통과.
- 캡처:
  - UI 배치 변경이 아니라 서비스 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `ImageQueueCommands` 안의 selected queue open 실패 메시지/가용성 갱신을 더 작은 presentation/service 단위로 뺄 수 있는지 점검하는 것입니다.

## 2026-07-03 image queue open failure presentation split

- 점검 결과:
  - 선택 이미지 열기 실패 시 표시/검색일치/선택 row 정보를 포함한 안내 문구를 Shell code-behind가 직접 조립하고 있었습니다.
  - 이 문구는 큐 표시/진단 정책이라 `WpfImageQueuePresenter` 쪽에 두는 편이 이후 메시지 UX를 바꿀 때 안전합니다.
- 수정 내용:
  - `WpfImageQueuePresenter.BuildOpenSelectionFailureMessage`와 `FormatLimitedQueueCount`를 추가했습니다.
  - Shell은 검색 텍스트, visible count, 검색 일치 count, 선택 row 이름만 수집하고 문구 조립은 presenter에 맡기도록 바꿨습니다.
  - source guard 테스트가 Shell에 큐 진단 count formatting이 다시 생기지 않도록 확인하게 했습니다.
- 구조:
  - 큐 실패 안내 문구는 presenter로 이동했습니다.
  - 실제 이미지 로드, 저장 라벨 조회, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service` 통과.
- 캡처:
  - UI 배치 변경이 아니라 presenter 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `ImageQueueCommands`의 선택 가능 여부 갱신과 open path resolution 호출을 더 줄일 수 있는지 점검하는 것입니다.

## 2026-07-03 image queue open-selection resolution split

- 점검 결과:
  - `GetOpenSelectedQueueItem`은 DataGrid 선택, ViewModel 선택, 검색 단일 결과, visible 단일 row를 순서대로 확인하면서 각 후보마다 `TryResolveOpenImagePath`를 호출했습니다.
  - 이후 실제 open 단계에서 같은 path resolution을 다시 호출했습니다.
  - saved split image fallback까지 포함된 open 가능 여부 판단은 `WpfImageQueueSelectionService`가 한 번에 맡는 편이 더 명확합니다.
- 수정 내용:
  - `WpfImageQueueOpenSelection`과 `WpfImageQueueSelectionService.ResolveOpenSelection`을 추가했습니다.
  - Shell은 UI 후보 목록을 순서대로 수집하고, 서비스가 첫 open 가능 후보와 실제 open path를 같이 반환하게 바꿨습니다.
  - 기존 `TryOpenSelectedQueueImage(WpfImageQueueItem)` 호출 경로도 서비스 결과를 사용하도록 연결했습니다.
- 구조:
  - 선택 후보 우선순위와 open path resolution은 selection service로 이동했습니다.
  - Shell은 DataGrid/ViewModel/search/visible row candidate를 수집하는 adapter 역할만 유지합니다.
  - 실제 이미지 로드, 저장 라벨 조회, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-selection-service` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-click-load-path` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-image-queue-status` 통과.
- 캡처:
  - UI 배치 변경이 아니라 service boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 이미지 큐 command file에서 남은 작은 workflow/presentation 조합을 더 분리할지, 아니면 다음 code-behind 후보인 DatasetSetupCommands의 생성/적용 흐름으로 넘어갈지 판단하는 것입니다.

## 2026-07-03 dataset setup path service split

- 점검 결과:
  - `DatasetSetupCommands` 안에 새 데이터셋 recipe 이름 선택, 출력 폴더 suffix, 다른 recipe의 동일 저장 경로 충돌 탐지, `VISION.xml` output root 읽기가 남아 있었습니다.
  - 이 로직은 View adapter가 아니라 dataset setup 정책이므로 Shell code-behind에 둘수록 Test/Test2 같은 저장 경로 혼동 이슈를 다시 고치기 어렵습니다.
- 수정 내용:
  - `WpfDatasetSetupPathService`를 추가했습니다.
  - `ResolveRecipeName`, `ResolveOutputRoot`, `CanUseRecipeNameForNewDataset`, `BuildUniqueRecipeName`, `TryFindDatasetUsingOutputRoot`, `TryReadRecipeOutputRoot`, `PathsEqual`을 서비스로 이동했습니다.
  - Shell은 현재 panel recipe/current recipe/base output root를 서비스에 넘기고, request 적용과 UI 상태 갱신만 유지합니다.
  - dataset setup focused 테스트가 output-root suffix와 기존 recipe 충돌 탐지를 서비스에서 직접 검증하게 했습니다.
- 구조:
  - 데이터셋 생성 경로/recipe 판단은 서비스로 이동했습니다.
  - 데이터셋 생성 적용, 샘플 적용, image queue loading 연결은 기존 Shell orchestration을 유지했습니다.
  - 실제 라벨 저장/조회, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-ui` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-request` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
- 캡처:
  - UI 배치 변경이 아니라 service boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `DatasetSetupCommands`의 request 적용 중 실제 파일 생성/샘플 적용/패널 refresh orchestration을 더 작게 나눌지 점검하는 것입니다.

## 2026-07-03 dataset setup data materialization service split

- 점검 결과:
  - `ApplyDatasetSetupRequest` 안에 output root 설정, class list clear/rebuild, YOLO output directory 생성이 직접 남아 있었습니다.
  - 이 부분은 UI adapter가 아니라 accepted request를 `CData`에 materialize하는 정책입니다.
- 수정 내용:
  - `WpfDatasetSetupDataService`를 추가했습니다.
  - `ApplyOutputRootAndClasses`가 class 이름 정규화, 중복 제거, 기본 `Defect` fallback, `CData.ConfigureOutputRoot`, `ClassNamedList` materialization, YOLO output directory 생성을 맡습니다.
  - Shell은 active recipe/purpose 설정, 샘플 preset 적용, config/YAML 저장, panel refresh orchestration만 유지합니다.
  - focused 테스트가 `OK`, `ok`, 빈 값, `NG` 입력이 `OK`/`NG` 두 클래스로 정규화되고 빈 class list는 `Defect`로 fallback되는지 확인합니다.
- 구조:
  - dataset setup accepted request의 CData materialization은 서비스로 이동했습니다.
  - 샘플 적용/저장/화면 갱신 orchestration은 기존 Shell에 남겼습니다.
  - 실제 라벨 저장/조회, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-ui` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-request` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
- 캡처:
  - UI 배치 변경이 아니라 service boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 dataset setup sample 적용 실패/성공 presentation을 더 분리할지, 아니면 model runtime/설치 UX 쪽 남은 항목으로 전환할지 판단하는 것입니다.

## 2026-07-03 tutorial current UI screenshot refresh

- 점검 결과:
  - README와 튜토리얼 문서가 이전 배치인 `왼쪽 이미지 큐 / 오른쪽 작업 패널` 설명을 일부 유지하고 있었습니다.
  - 실제 현재 UI는 왼쪽 작업 패널, 가운데 캔버스, 오른쪽 이미지 큐 구조입니다.
  - 단독 HTML은 최신 주석 이미지가 포함되어야 복사해서 열어도 이미지가 빠지지 않습니다.
- 수정 내용:
  - `README.md`, `docs/tutorial/README.md`, `docs/tutorial/labeling-workbench-tutorial.html`의 UI 배치 설명을 현재 구조로 맞췄습니다.
  - 튜토리얼 원본 캡처 14장과 주석 캡처 14장을 현재 UI 기준으로 갱신했습니다.
  - `docs/tutorial/labeling-workbench-tutorial-standalone.html`을 일반 HTML 기준으로 다시 생성해 14개 주석 이미지를 모두 내장했습니다.
- 검증:
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
  - `git diff --check` 통과.
  - 일반 HTML의 `images/annotated` 참조 14개와 단독 HTML의 `data:image/png;base64` 내장 14개를 확인했습니다.
  - 주요 주석 캡처 01/02/03/05/09/12/14를 시각 확인했습니다.
- 캡처:
  - `docs\tutorial\images\annotated\01-overview-1920-annotated.png`
  - `docs\tutorial\images\annotated\03-labeling-workbench-1920-annotated.png`
  - `docs\tutorial\images\annotated\09-model-center-1920-annotated.png`
  - `docs\tutorial\images\annotated\12-inference-dock-1920-annotated.png`
- 다음 작업:
  - 다음 우선순위는 dataset setup presentation service split에 focused test를 추가하거나, 공개 README의 빠른 시작 흐름을 튜토리얼과 더 짧게 연결하는 것입니다.

## 2026-07-03 dataset setup presentation service test guard

- 점검 결과:
  - `WpfDatasetSetupPresentationService`가 dataset setup 성공/실패 안내 문구를 담당하도록 분리됐지만 focused test가 아직 이 서비스 위임과 문구 결과를 직접 보호하지 않았습니다.
  - 데이터셋 생성에서 저장 폴더 중복, sample preset 실패, 준비 완료 상태는 사용자가 바로 보는 안내이므로 shell code-behind에 다시 흩어지지 않게 보호할 필요가 있습니다.
- 수정 내용:
  - `--wpf-dataset-setup-ui` 검증에 `WpfDatasetSetupPresentationService` 존재, shell 위임, duplicate/ready status 메서드 소유 확인을 추가했습니다.
  - duplicate output root 안내, invalid recipe 안내, sample preset fallback, ready status, dataset-ready status, creation log 문구 결과를 직접 검증했습니다.
  - UI/Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-ui` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-request` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `git diff --check` 통과.
- 캡처:
  - 테스트 보강만 진행했으므로 신규 UI 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 공개 README 첫 화면에서 사용자가 바로 튜토리얼/단독 HTML로 이동할 수 있게 빠른 시작 링크와 현재 UI 기준 요약을 더 정리하는 것입니다.

## 2026-07-03 README first-run entry update

- 점검 결과:
  - README 상단에 최신 튜토리얼 링크는 있었지만, 앱을 처음 실행한 사용자가 현재 UI에서 어느 영역부터 보면 되는지 짧게 잡아주는 안내가 부족했습니다.
  - 현재 UI는 왼쪽 작업 패널, 가운데 캔버스, 오른쪽 이미지 큐 구조이므로 README 첫 화면도 이 기준을 따라야 합니다.
- 수정 내용:
  - `README.md` 상단 캡처 아래에 `처음 실행할 때 보는 순서`를 추가했습니다.
  - `1 데이터셋`, 왼쪽 작업 패널, 오른쪽 이미지 큐, 가운데 캔버스, `4 학습/모델` 순서로 처음 보는 흐름을 정리했습니다.
  - 화면 캡처 중심 튜토리얼과 이미지 포함 단독 튜토리얼 링크를 같은 위치에 다시 연결했습니다.
- 검증:
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
  - 공개 README/tutorial 금칙어 검색 통과.
  - `git diff --check` 통과.
- 캡처:
  - 문서 문구 변경만 진행했으므로 신규 UI 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 남은 WPF shell code-behind 중 dataset setup orchestration의 작은 상태 갱신 단위를 추가로 서비스화할지 점검하는 것입니다.

## 2026-07-03 dataset switch/open-folder presentation split

- 점검 결과:
  - `ClearImageQueueAfterDatasetSwitch`와 `ExecuteOpenDatasetRootFolderCommand` 안에 이미지 폴더 누락, 저장 경로 미설정, 데이터셋 폴더 열기 실패 안내 문구가 직접 남아 있었습니다.
  - 해당 메서드들은 큐 초기화와 폴더 열기 같은 UI adapter 역할은 유지해야 하지만, 사용자 안내 문구 조합까지 Shell code-behind가 소유할 필요는 없습니다.
- 수정 내용:
  - `WpfDatasetSetupPresentationService`에 missing image root, missing output root, open-folder failure status/log builder를 추가했습니다.
  - Shell은 dataset status와 log 대상으로 문구를 전달만 하고, 문구 조합은 presentation service에 위임하도록 바꿨습니다.
  - focused test가 service 메서드 존재, Shell 위임, 사용자-facing 문구 결과를 직접 검증하도록 보강했습니다.
  - 실제 dataset switching, image queue clearing, folder open, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-ui` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-request` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `git diff --check` 통과.
- 캡처:
  - UI 배치 변경이 아니라 presentation boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `RefreshShellDatasetContext`의 dataset name/purpose/path presentation을 별도 service로 분리할 가치가 있는지 점검하는 것입니다.

## 2026-07-03 dataset context name/purpose presentation split

- 점검 결과:
  - `RefreshShellDatasetContext`가 현재 데이터셋 이름 fallback과 데이터셋 목적 표시 문구를 직접 조합하고 있었습니다.
  - 이미 `WpfDatasetContextPresentationService`가 데이터셋 헤더 문구를 담당하고 있었으므로 새 서비스를 만들지 않고 기존 서비스에 작은 책임만 추가하는 편이 맞았습니다.
- 수정 내용:
  - `WpfDatasetContextPresentationService`에 dataset name fallback과 purpose display formatter를 추가했습니다.
  - `RefreshShellDatasetContext`는 recipe/output/image/class count를 모아 ViewModel에 전달하는 adapter 역할만 유지하고, 이름/목적 표시 문구는 service로 위임했습니다.
  - `--wpf-labeling-shell`에 Shell code-behind가 `FormatShellDatasetPurposeName`을 다시 소유하지 않는지, dataset context service가 name/purpose 표시를 소유하는지 source guard를 추가했습니다.
  - 실제 dataset loading, label persistence, image queue, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-ui` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-request` 통과.
  - `git diff --check` 통과.
- 캡처:
  - UI 배치 변경이 아니라 presentation boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 모델 런타임/모델 비교 UX에서 현재 검사 모델, 학습 후보 모델, 선택 이력 모델의 구분이 모든 화면에서 일관적인지 점검하는 것입니다.

## 2026-07-03 runtime profile capability row

- 점검 결과:
  - 모델 실행기 연결 상태 카드에는 YOLOv5/YOLOv8/YOLO11/ONNX 프로필과 연결 상태가 보였지만, 각 프로필이 `학습+검사`, `현재 검사 우선`, `추론 전용` 중 어디에 해당하는지 행 안에서 바로 구분하기는 부족했습니다.
  - 모델을 여러 런타임으로 비교하는 방향에서는 사용자가 설치/연결 전에도 각 프로필의 사용 범위를 먼저 알아야 합니다.
- 수정 내용:
  - `PythonModelRuntimeProfile`에 `CapabilityText`를 추가해 프로필별 지원 범위를 Core profile service가 제공합니다.
  - WPF 모델 설정 패널의 런타임 프로필 행에 `지원 범위` 줄을 추가했습니다.
  - YOLOv5는 `학습 + 현재 검사`, YOLOv8/YOLO11은 `현재 검사 우선 / 학습은 worker 연결 필요`, ONNX는 `추론 전용`으로 구분됩니다.
  - 빌드 blocker였던 `CViewer` 컨텍스트 메뉴의 사라진 `CUtil.LoadImageFilePath/SaveImageFilePath` 호출을 WinForms 기본 파일 대화상자로 대체했습니다. Viewer의 ROI/마우스/렌더링/브러시/지우개 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-yolo-model-settings-panel` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-connection` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --python-model-runtime-self-test` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-responsive-layout --width 1920 --height 1080` 통과.
  - `git diff --check` 통과.
- 캡처:
  - `artifacts\ui\wpf-runtime-profile-capability-after-1920.png`
- 다음 작업:
  - 다음 우선순위는 runtime profile 선택 후 실제 `현재 검사` 실행 경로에서 실패/성공 결과 카드가 같은 `지원 범위` 용어와 충돌하지 않는지 점검하는 것입니다.

## 2026-07-03 interactive detection presentation service split

- 점검 결과:
  - runtime profile 지원 범위 표시 이후, 실제 `현재 검사` 단일 실행 경로의 준비/완료/실패 상태와 로그 문구 일부가 `RunInteractiveDetectionAsync` 안에서 직접 조합되고 있었습니다.
  - 이 상태로 두면 모델 런타임 표시와 검사 결과 표시가 다시 어긋나거나, 실패 사유 길이 제한 규칙이 Shell code-behind에 남게 됩니다.
- 수정 내용:
  - `WpfInferenceStatusPresentationService`에 단일 검사 준비 상태, 완료/실패 command status, top inference status, 완료/실패 로그, 실패 사유 clipping formatter를 추가했습니다.
  - `RunInteractiveDetectionAsync`는 target image 결정, worker 실행, elapsed/path 전달만 유지하고 사용자-facing 문구 조합은 service로 위임했습니다.
  - focused test가 Shell에 `failureSummary` 지역 변수가 다시 생기지 않고, 단일 검사 상태/로그가 service 메서드를 통해 만들어지는지 검증하게 했습니다.
  - TCP 요청, worker 실행, candidate overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-single-detection-path` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-inference-status-presentation` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` 통과.
  - `git diff --check` 통과. 줄끝 변환 경고만 있었고 공백 오류는 없었습니다.
- 캡처:
  - UI 배치 변경이 아니라 presentation boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `RunWorkerDetectionForImageAsync` 내부의 worker 준비/요청/timeout/cancel 문구 중 아직 Shell에 남은 presentation 조합을 같은 방식으로 줄일지 점검하는 것입니다.

## 2026-07-03 worker detection presentation service split

- 점검 결과:
  - `RunWorkerDetectionForImageAsync` 안에 이미지 누락/로드 실패, worker 준비, 연결 실패, 요청 중, 요청 실패, timeout, success summary, cancel summary 문구가 직접 남아 있었습니다.
  - 이 메서드는 worker 실행 순서와 canvas 적용 경로를 조정하는 adapter 역할을 해야 하므로, 사용자-facing 문구와 길이/파일명 formatting은 presentation service 쪽이 더 적합합니다.
- 수정 내용:
  - `WpfInferenceStatusPresentationService`에 `BuildWorker*` 메서드를 추가해 worker 준비/요청/실패/완료/cancel 상태와 로그 문구를 담당하게 했습니다.
  - `RunWorkerDetectionForImageAsync`는 image path, model source, elapsed, candidate count 같은 값만 service에 넘기도록 정리했습니다.
  - focused test가 worker request/cancel/status/success summary 문구가 Shell에 inline으로 다시 들어오지 않는지, service 출력이 모델 소스와 파일명을 유지하는지 검증하게 했습니다.
  - TCP 요청, worker 실행, candidate overlay, Viewer/OpenGL/ROI/brush/eraser 경로는 변경하지 않았습니다.
- 검증:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\` 통과. 오류 0개, 기존 외부 `C:\Git\Library-Noah\Lib.Common` unused 변수 경고 4개.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-single-detection-path` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-inference-status-presentation` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --mvvm-infra` 통과.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
  - `git diff --check` 통과. 줄끝 변환 경고만 있었고 공백 오류는 없었습니다.
- 캡처:
  - UI 배치 변경이 아니라 presentation boundary 분리라 신규 캡처는 만들지 않았습니다.
- 다음 작업:
  - 다음 우선순위는 `DetectionExecution`/`DetectionWorkerExecution` 밖에 남은 runtime command/status 문구 중 이미 service 계약으로 보호되지 않은 작은 조합이 있는지 점검하거나, 실제 사용자 흐름 기준으로 모델 비교/검사 결과 패널의 잔여 혼란 지점을 확인하는 것입니다.

## 2026-07-03 README/tutorial current UI screenshot recheck

- 점검 결과:
  - 사용자가 README와 튜토리얼에 예전 UI가 남아 있다고 다시 지적했습니다.
  - 공개 문서가 보여주는 화면은 현재 WPF 구조인 `왼쪽 작업 패널 / 가운데 캔버스 / 오른쪽 이미지 큐` 기준이어야 합니다.
  - 공개 README에는 내부 문서 작성 규칙처럼 보이는 `공개 문서에는 개인 로컬 경로...` 문장이 남아 있어 소개 문서 문맥과 맞지 않았습니다.
- 수정 내용:
  - `docs\tutorial\images`의 원본 캡처 14장과 legacy 호환 이미지 6장을 최신 WPF 캡처 기준으로 교체했습니다.
  - `docs\tutorial\images\annotated`의 주석 캡처 14장을 최신 WPF 캡처 기준으로 다시 만들었습니다.
  - 데이터셋 생성 캡처의 저장 경로 입력칸은 특정 테스트 PC 경로가 보이지 않도록 일반 `새 데이터셋 저장 폴더` 문구로 마스킹했습니다.
  - `docs\tutorial\labeling-workbench-tutorial-standalone.html`을 다시 생성해 최신 주석 이미지 14장을 모두 base64로 내장했습니다.
  - `README.md`의 문서 섹션은 최신 UI 캡처 유지 원칙과 standalone 튜토리얼 안내만 남기고, 내부 작성 규칙처럼 보이는 문장은 제거했습니다.
- 검증:
  - UTF-8 읽기 확인: `README.md`, `docs/tutorial/README.md` 모두 정상 한국어이며 replacement character 없음.
  - 공개 문서 금칙어 검색 통과: `D:\`, `C:\`, `Test01`, `TEST_`, `제가`, `내가`, `저만`, `당신`, `Codex`, `소통`, `포트폴리오`, `로컬 경로` 결과 없음.
  - 이미지 참조 수 확인: README 1개, tutorial README 15개, tutorial HTML 14개 주석 이미지 참조.
  - standalone 확인: 14개 `src="data:image/png;base64"` 포함, `src="images/` 참조 0개.
  - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs` 통과.
  - `git diff --check -- README.md docs\tutorial\README.md docs\tutorial\labeling-workbench-tutorial.html docs\tutorial\labeling-workbench-tutorial-standalone.html docs\tutorial\images docs\WORK_TRACKING.md docs\STABLE_VERIFIED_AREAS.md CODEX_NEXT_PROMPT.md` 통과. 줄끝 변환 경고만 있었고 공백 오류는 없었습니다.
- 캡처:
  - `docs\tutorial\images\annotated\01-overview-1920-annotated.png`
  - `docs\tutorial\images\annotated\03-labeling-workbench-1920-annotated.png`
  - `docs\tutorial\images\annotated\09-model-center-1920-annotated.png`
  - `docs\tutorial\images\annotated\12-inference-dock-1920-annotated.png`
  - `docs\tutorial\images\annotated\14-model-inspect-1920-annotated.png`
- 다음 작업:
  - 다음 우선순위는 실제 사용자 흐름 기준으로 모델 비교/검사 결과 패널의 잔여 혼란 지점을 확인하거나, Shell code-behind에 남은 작은 runtime/status 문구 조합을 service 계약으로 줄이는 것입니다.

## 2026-07-03 public README cleanup after rendered review

- 점검 결과:
  - README 렌더링 확인 중 예전 대표 이미지가 계속 보일 수 있는 파일명 재사용 문제가 남아 있었습니다.
  - README 하단에는 `문서`, `커밋 전 기준`, `git status`, `WORK_TRACKING`, `STABLE_VERIFIED_AREAS`처럼 내부 협업/검증용 기준이 사용자용 문서처럼 노출되어 있었습니다.
- 수정 내용:
  - README 대표 이미지를 `docs\tutorial\images\annotated\readme-current-workflow-20260703.png` 새 파일명으로 분리해 stale image/cache 가능성을 줄였습니다.
  - README 본문에서 `이미지 리스트` 표현을 현재 UI 용어인 `이미지 큐`로 바꿨습니다.
  - README에서 내부 작업 기준 섹션과 내부 문서 링크 목록을 제거했습니다.
  - README에는 제품 소개, 처음 실행 흐름, 주요 기능, 사용 흐름, 용어 구분, 실행 방법만 남겼습니다.
- 검증:
  - README 이미지 참조가 새 파일을 가리키고 파일이 존재함을 확인했습니다.
  - `readme-current-workflow-20260703.png`를 직접 열어 현재 UI 구조인 `왼쪽 작업 패널 / 가운데 캔버스 / 오른쪽 이미지 큐`가 보이는지 확인했습니다.
  - README에서 `커밋`, `git status`, `WORK_TRACKING`, `STABLE_VERIFIED`, `CODEX`, `내부`, `작업 이력`, `검증 로그`, `View code-behind`, `MVVM`, `focused`, `포트폴리오`, `제가`, `내가`, `저만`, `당신`, `소통` 검색 결과 없음.
  - README에서 `D:\`, `C:\`, `Test01`, `TEST_`, `Debug\DATA`, `artifacts\run`, `LabelingData` 검색 결과 없음.
- 다음 작업:
  - README를 추가로 수정할 때는 먼저 렌더링된 첫 화면 기준으로 사용자용 문서인지 확인하고, 내부 협업 기준은 `CODEX_NEXT_PROMPT.md`, `docs\WORK_TRACKING.md`, `docs\STABLE_VERIFIED_AREAS.md`에만 남깁니다.

## 보류/제외

- C# 앱 안에 YOLO 학습 로직을 직접 넣지 않습니다.
- Python 모델 코드를 C#으로 재작성하지 않습니다.
- 전체 Material 테마 적용은 WPF 전환이 더 끝난 뒤 다시 판단합니다.
- WinForms/WPF hybrid 상태를 최종 제품 방향으로 보지 않습니다.
