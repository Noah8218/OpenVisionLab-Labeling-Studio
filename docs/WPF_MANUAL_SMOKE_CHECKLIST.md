# WPF Manual Smoke Checklist

Current image queue rule: one click on a row must open that image in the Main canvas, the selected row must be clearly highlighted, and the old top preview block should not be visible.

WPF 화면을 직접 띄워 보는 날에는 아래 순서만 확인합니다.

## 실행

```powershell
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
```

Release publish 산출물을 볼 때는 먼저 publish 후 실행합니다.

```powershell
.\scripts\publish-win-x64.ps1 -Configuration Release
.\scripts\start-labeling-workbench.ps1 -AppMode Publish
```

## 화면 확인

1. 시작 직후 중앙 캔버스에 샘플 이미지가 보이고, 추론은 자동 실행되지 않아야 합니다.
2. 왼쪽 이미지 큐에서 한 번 클릭하면 미리보기만 바뀌고, 중앙 캔버스 이미지는 바뀌지 않아야 합니다.
3. 더블클릭 또는 `열기` 버튼을 누르면 중앙 캔버스 이미지가 바뀌어야 합니다.
4. `테마` 버튼으로 다크/라이트 전환 시 버튼, 패널, 로그, 상태바 글자가 읽혀야 합니다.
5. `추론 검토` 모드에서 `현재 추론`을 누르면 상태가 준비, worker 연결, 요청, 완료 순서로 바뀌어야 합니다.
6. 추론 후보는 캔버스와 오른쪽 `후보` 탭에 같이 보여야 합니다.
7. 후보 선택 시 클래스, 신뢰도, 좌표, 현재 라벨과의 겹침이 보여야 합니다.
8. 현재 라벨과 많이 겹치는 후보는 `중복 가능`으로 보이고 `확정`이 비활성화되어야 합니다.
9. `전체 확정`은 확정 가능한 후보만 저장하고 중복 후보는 건너뛰어야 합니다.
10. 확대/축소 후 추론 박스와 라벨이 이미지 위치를 따라가야 하며, 화면 밖 후보 라벨만 따로 남으면 안 됩니다.
11. `저장` 후 YOLO label 파일과 이미지 큐의 라벨/AI 상태가 갱신되어야 합니다.
12. `YOLO` 탭의 `첫 점검`, `테스트`, `재시작`, `중지` 버튼이 현재 상태에 맞게 활성화되어야 합니다.

## 자동 확인

```powershell
dotnet run --project .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug
.\scripts\smoke-yolo-workflow.ps1
.\scripts\verify-first-run.ps1 -SkipBuild -SkipTests -SkipYoloSmoke -RunPublishWpfSmoke
```

## 기록

확인한 결과는 `docs\WORK_TRACKING.md`의 완료 항목에 번호를 이어서 남깁니다.
