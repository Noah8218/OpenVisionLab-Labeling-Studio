# Next Codex Prompt

## Current Git Rule

- Do not run `git push` unless the user explicitly asks for `push`.
- A commit request means create a local commit only. Pushing requires a separate explicit user request.
- If in doubt, stop after commit and report the commit hash.

아래 내용을 새 Codex 대화에 그대로 붙여 넣으세요.

```text
C:\Git\Labelling_Application 작업을 이어서 진행해주세요.

먼저 CODEX_RECOVERY.md를 읽고 현재 상태를 파악해주세요. 이 대화는 매우 긴 이전 작업의 후속입니다.

중요 원칙:
- git status --short를 먼저 확인하고, 사용자가 만든 변경 또는 이전 Codex 변경을 임의로 revert하지 마세요.
- 사용자가 명시적으로 `push`를 요청하지 않으면 `git push`를 실행하지 마세요. `커밋` 요청은 로컬 커밋 생성까지만 의미하며, 푸시는 별도 명시가 필요합니다.
- 우리는 MVVM을 지향합니다. View code-behind에는 XAML로 불가능한 UI adapter만 남기고, Command/상태/워크플로우는 ViewModel/Service로 분리합니다.
- Viewer/OpenGL/ROI/브러시/지우개 성능 경로는 이미 여러 focused 테스트로 검증되었습니다. 재현 없이 임의로 수정하지 마세요.
- 새 사용자-facing 문구에는 OpenGL 같은 내부 기술 용어를 넣지 마세요.
- 공개 README/튜토리얼에는 `포트폴리오`, 개인 대화 맥락, 저자만 알면 되는 사정, 로컬 PC 경로를 쓰지 마세요. 이런 내용은 내부 작업 문서에만 남기고, 공개 문서는 제품과 사용 흐름 중심으로 작성하세요.
- XAML 한글 인코딩에 주의하세요. 필요하면 기존처럼 XML numeric entity를 사용하세요.

현재 최신 작업:
- WPF 작업 배치는 왼쪽 작업 패널, 가운데 캔버스, 오른쪽 이미지 큐 구조로 정리되어 있습니다.
- README/tutorial 캡처 14장과 annotated 캡처 14장을 현재 UI 기준으로 갱신했고, standalone HTML은 동일한 14개 이미지를 base64로 내장합니다.
- 공개 README에는 첫 실행 순서와 최신 튜토리얼/standalone 튜토리얼 링크가 상단 캡처 아래에 정리되어 있습니다.
- 2026-07-03 재검수에서 README/tutorial/standalone 이미지가 현재 UI 기준인지 다시 확인했고, legacy 튜토리얼 이미지 6장도 최신 WPF 화면으로 교체했습니다.
- 이미지 큐 검색/선택/open path 판정은 `WpfImageQueueFilterService`, `WpfImageQueueSelectionService`, `WpfImageQueuePresenter`, `WpfDatasetImageRootResolver` 쪽으로 분리되었습니다.
- Dataset setup은 `WpfDatasetSetupPathService`, `WpfDatasetSetupDataService`, `WpfDatasetSetupPresentationService`로 recipe/output root, CData materialization, 사용자 안내 문구를 분리했습니다.
- 현재 데이터셋 헤더의 이름 fallback과 목적 표시 문구는 `WpfDatasetContextPresentationService`로 분리했고, Shell은 recipe/output/image/class count를 전달하는 adapter 역할만 유지합니다.
- 모델 설정 패널의 런타임 프로필 행은 `PythonModelRuntimeProfile.CapabilityText`로 지원 범위를 표시합니다. YOLOv5는 `학습 + 현재 검사`, YOLOv8/YOLO11은 `현재 검사 우선 / 학습은 worker 연결 필요`, ONNX는 `추론 전용`으로 구분됩니다.
- 단일 `현재 검사` 실행의 준비/완료/실패 command status, top inference status, 완료 로그, 실패 사유 clipping은 `WpfInferenceStatusPresentationService.BuildInteractive*` 메서드가 담당합니다. Shell은 target/worker/elapsed/path adapter 역할만 유지합니다.
- `RunWorkerDetectionForImageAsync` 내부의 이미지 누락/로드 실패, worker 준비/연결 실패/요청/timeout/success/cancel 문구도 `WpfInferenceStatusPresentationService.BuildWorker*` 메서드로 분리했습니다. Shell은 image path/model source/elapsed/candidate count를 넘기는 adapter 역할만 유지합니다.
- `Library/CViewer.cs`의 컨텍스트 메뉴 이미지 열기/저장은 제거된 `CUtil` helper 대신 WinForms 기본 파일 대화상자를 사용합니다. ROI/마우스/렌더링/브러시/지우개 경로는 변경하지 않았습니다.
- Shell code-behind는 아직 완전히 정리된 상태가 아닙니다. 다만 최근 변경은 Shell이 UI 후보 수집/화면 갱신 adapter 역할만 맡고, 정책/상태/문구는 service가 맡는 방향입니다.
- Viewer/OpenGL/ROI/brush/eraser 성능 경로는 이번 작업에서 건드리지 않았습니다.

먼저 할 일:
1. `git status --short`를 먼저 확인하고, dirty worktree의 기존 변경을 임의로 되돌리지 마세요.
2. `docs/WORK_TRACKING.md`, `docs/STABLE_VERIFIED_AREAS.md`의 2026-07-03 항목을 읽고 이미 끝난 UX/문서/서비스 분리를 중복하지 마세요.
3. 현재 기준 최소 검증으로 아래를 실행하세요.
   - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`
   - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-ui`
   - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-dataset-setup-request`
   - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-single-detection-path`
   - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-inference-status-presentation`
   - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --wpf-labeling-shell`
   - `dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll --priority-workflow-docs`
   - `git diff --check`
4. UI를 수정한다면 1920x1080 기준 screenshot을 만들고, README/tutorial 이미지를 건드린 경우 standalone HTML도 다시 생성하세요.
5. 공개 README/tutorial에는 개인 로컬 경로, 대화 흔적, `포트폴리오` 같은 개인 목적 문구를 넣지 마세요.

다음 개발 우선순위:
1. `DetectionExecution`/`DetectionWorkerExecution` 밖에 남은 runtime command/status 문구 중 이미 service 계약으로 보호되지 않은 작은 조합이 있는지 점검합니다.
2. ImageQueueCommands 쪽은 이미 filter/selection/presenter/service 분리가 많이 진행됐으므로, 중복 작업보다 실제 사용자 흐름에서 남은 혼란 지점을 먼저 확인합니다.
3. Shell code-behind의 남은 dataset/model/status adapter 중 아직 사용자-facing 문구나 정책을 직접 조합하는 곳이 있으면 작은 service로만 분리합니다.
4. README/tutorial은 현재 UI 기준을 유지합니다. UI 변경 시 최신 캡처와 문서가 같이 바뀌어야 합니다.
5. 검증 완료 항목은 docs/STABLE_VERIFIED_AREAS.md 또는 docs/WORK_TRACKING.md에 기록합니다.

최종 보고에는 변경 파일, 검증 명령/결과, UI 캡처 여부, 남은 리스크, 다음 진행 업무를 간단히 정리해주세요.
```
