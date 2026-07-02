# Next Codex Prompt

아래 내용을 새 Codex 대화에 그대로 붙여 넣으세요.

```text
C:\Git\Labelling_Application 작업을 이어서 진행해주세요.

먼저 CODEX_RECOVERY.md를 읽고 현재 상태를 파악해주세요. 이 대화는 매우 긴 이전 작업의 후속입니다.

중요 원칙:
- git status --short를 먼저 확인하고, 사용자가 만든 변경 또는 이전 Codex 변경을 임의로 revert하지 마세요.
- 우리는 MVVM을 지향합니다. View code-behind에는 XAML로 불가능한 UI adapter만 남기고, Command/상태/워크플로우는 ViewModel/Service로 분리합니다.
- Viewer/OpenGL/ROI/브러시/지우개 성능 경로는 이미 여러 focused 테스트로 검증되었습니다. 재현 없이 임의로 수정하지 마세요.
- 새 사용자-facing 문구에는 OpenGL 같은 내부 기술 용어를 넣지 마세요.
- XAML 한글 인코딩에 주의하세요. 필요하면 기존처럼 XML numeric entity를 사용하세요.

현재 최신 작업:
- Lib.Noah 최신 소스의 Lib.OpenCV MatchingTool을 참조해 템플릿 매칭 기반 오토 라벨링을 도입했습니다.
- 현재 이미지 후보 찾기: 상단 템플릿 버튼.
- 큐 일괄 자동 라벨링: 이미지 큐의 템플릿 배치 버튼.
- 기존 라벨 파일이 있는 이미지는 덮어쓰지 않습니다.
- 템플릿 매칭 업무 로직은 Core service와 WpfTemplateMatchingAutoLabelViewModel로 분리했고, WpfLabelingShellWindow.TemplateMatchingCommands.cs는 host adapter 역할만 남겼습니다.

먼저 할 일:
1. `dotnet build .\MvcVisionSystem.csproj -c Debug -m:1 -v:minimal /nodeReuse:false /p:UseSharedCompilation=false`로 현재 빌드를 확인하세요.
2. `--mvvm-infra`, `--yolo-annotation-storage`, `--wpf-visual-smoke --roi-only --width 1280 --height 820`를 실행해 최근 변경 회귀를 확인하세요.
3. 가능하면 실제 `artifacts\run\Debug\MvcVisionSystem.exe`를 실행해서 템플릿 버튼과 이미지 큐의 템플릿 배치 버튼이 보이는지 확인하세요.
4. 실제 데이터셋에서 박스 하나를 그리고 템플릿 배치를 실행해, 라벨 없는 이미지에 YOLO txt가 생성되고 기존 라벨은 덮어쓰지 않는지 확인하세요.
5. 문제가 있으면 원인을 분석한 뒤 작은 단위로 수정하고, 수정 후 다시 focused 검증을 실행하세요.

다음 개발 우선순위:
1. 템플릿 배치 자동 라벨링 EXE 수동 검증
2. 템플릿 배치 저장/skip 동작 focused 테스트 추가
3. WpfLabelingShellWindow의 남은 큰 code-behind 중 DatasetSetupCommands/ImageQueueCommands/AnnotationMaskStrokeCommit 순서로 MVVM/Service 분리
4. 검증 완료 항목은 docs/STABLE_VERIFIED_AREAS.md 또는 docs/WORK_TRACKING.md에 기록

최종 보고에는 변경 파일, 검증 명령/결과, EXE 수동 검증 여부, 남은 리스크를 간단히 정리해주세요.
```
