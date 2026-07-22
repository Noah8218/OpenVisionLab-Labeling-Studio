using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private async void ExecuteCreateSmartMaskCandidateCommand()
        {
            if (isCreatingSmartMask)
            {
                return;
            }

            int promptIndex = FindSmartMaskPromptIndex();
            if (promptIndex < 0 || activeImageSize.IsEmpty || string.IsNullOrWhiteSpace(activeImagePath))
            {
                RefreshSmartMaskCommandState("결함 둘레에 사각형 박스를 먼저 그린 뒤 다시 누르세요.");
                AppendLog("스마트 마스크: 결함 둘레에 사각형 박스를 먼저 그리세요.");
                return;
            }

            string promptOverlayId = GetManualRoiOverlayId(promptIndex);
            Rectangle promptBounds = manualRois[promptIndex];
            string className = GetManualRoiClassName(promptIndex);
            int classIdValue = global.Data.ClassNamedList.FindIndex(item =>
                string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
            int? classId = classIdValue >= 0 ? classIdValue : null;
            WpfMobileSamBoxPromptRequest request = mobileSamBoxPromptService.BuildRequest(
                global.Data.ProjectSettings?.PythonModel,
                activeImagePath,
                promptBounds,
                classId,
                className);
            if (!request.IsValid)
            {
                string error = string.Join(" ", request.Errors);
                RefreshSmartMaskCommandState(error);
                AppendLog("스마트 마스크 준비 실패: " + error);
                return;
            }

            string requestedImagePath = activeImagePath;
            isCreatingSmartMask = true;
            RefreshSmartMaskCommandState("MobileSAM이 박스 안의 경계를 찾고 있습니다. UI는 계속 사용할 수 있습니다.");
            SetYoloCommandStatus("스마트 마스크 후보 생성 중...", isBusy: true);
            WpfMobileSamBoxPromptResult result = await mobileSamBoxPromptService.RunAsync(request, CancellationToken.None);
            isCreatingSmartMask = false;

            if (!string.Equals(requestedImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                RefreshSmartMaskCommandState("이미지가 바뀌어 이전 스마트 마스크 결과를 적용하지 않았습니다.");
                AppendLog("스마트 마스크 결과 무시: 실행 중 이미지가 변경되었습니다.");
                return;
            }
            if (!result.Succeeded || result.Candidate == null)
            {
                RefreshSmartMaskCommandState(result.Error);
                SetYoloCommandStatus("스마트 마스크 실패: " + result.Error, isBusy: false);
                AppendLog("스마트 마스크 실패: " + result.Error);
                return;
            }

            int currentPromptIndex = FindManualRoiIndexByOverlayId(promptOverlayId);
            if (currentPromptIndex < 0 || manualRois[currentPromptIndex] != promptBounds)
            {
                RefreshSmartMaskCommandState("프롬프트 박스가 변경되어 후보를 적용하지 않았습니다.");
                AppendLog("스마트 마스크 결과 무시: 프롬프트 박스가 변경되었습니다.");
                return;
            }

            RegisterAnnotationHistoryBeforeChange("박스를 스마트 마스크 후보로 변환");
            manualRois.RemoveAt(currentPromptIndex);
            RemoveAtIfPresent(manualRoiClassNames, currentPromptIndex);
            RemoveAtIfPresent(manualRoiShapeKinds, currentPromptIndex);
            RemoveAtIfPresent(manualRoiOverlayIds, currentPromptIndex);
            ApplyDetectionCandidatesPreservingConfirmed(new[] { result.Candidate }, succeeded: true);
            SetYoloCommandStatus(result.Summary + " / 확정 전 후보", isBusy: false);
            AppendLog($"{result.Summary} / {result.RuntimeSummary} / mask area {result.MaskArea}");
            RefreshSmartMaskCommandState("후보 경계를 확인한 뒤 확정 또는 스킵하세요. 후보는 확정 전까지 저장되지 않습니다.");
        }

        private int FindSmartMaskPromptIndex()
        {
            EnsureManualRoiMetadataCount();
            for (int index = manualRois.Count - 1; index >= 0; index--)
            {
                if (GetManualRoiShapeKind(index) == CanvasRoiShapeKind.Rectangle
                    && !manualRois[index].IsEmpty)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RefreshSmartMaskCommandState(string detail = "")
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            bool isVisible = global.Data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            int promptIndex = isVisible ? FindSmartMaskPromptIndex() : -1;
            string effectiveDetail = detail;
            bool isReady = false;
            if (isVisible && !isCreatingSmartMask && promptIndex >= 0 && !activeImageSize.IsEmpty)
            {
                WpfMobileSamBoxPromptRequest request = mobileSamBoxPromptService.BuildRequest(
                    global.Data.ProjectSettings?.PythonModel,
                    activeImagePath,
                    manualRois[promptIndex],
                    null,
                    GetManualRoiClassName(promptIndex));
                isReady = request.IsValid;
                if (string.IsNullOrWhiteSpace(effectiveDetail) && !isReady)
                {
                    effectiveDetail = string.Join(" ", request.Errors);
                }
            }

            if (string.IsNullOrWhiteSpace(effectiveDetail))
            {
                effectiveDetail = promptIndex < 0
                    ? "결함 둘레에 사각형 박스를 그리면 MobileSAM 후보 마스크를 만들 수 있습니다."
                    : "마지막 사각형을 프롬프트로 사용합니다. 결과는 확정 전 후보이며 원본 이미지와 기존 라벨은 바꾸지 않습니다.";
            }

            CanvasPanelViewModel.SetSmartMaskState(isVisible, isReady, isCreatingSmartMask, effectiveDetail);
        }
    }
}
