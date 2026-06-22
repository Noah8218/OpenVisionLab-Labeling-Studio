# Work Tracking

Last updated: 2026-06-23

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

## 보류/제외

- C# 앱 안에 YOLO 학습 로직을 직접 넣지 않습니다.
- Python 모델 코드를 C#으로 재작성하지 않습니다.
- 전체 Material 테마 적용은 WPF 전환이 더 끝난 뒤 다시 판단합니다.
- WinForms/WPF hybrid 상태를 최종 제품 방향으로 보지 않습니다.
