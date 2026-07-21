using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void RefreshSegmentationAdapterComparisonState()
        {
            EnsureProjectSettings();
            if (TrainingSettingsViewModel == null)
            {
                return;
            }

            WpfSegmentationAdapterComparisonContext context = segmentationAdapterComparisonRunService.BuildContext(
                global.Data,
                global.Data.ProjectSettings.ModelRegistry,
                global.Data.ProjectSettings.PythonModel);
            TrainingSettingsViewModel.SetSegmentationAdapterComparisonContext(context);
        }

        private void ExecuteBrowseSegmentationUnetCheckpointCommand()
        {
            string currentPath = TrainingSettingsViewModel?.SegmentationUnetWeightsPath ?? string.Empty;
            if (!TryPickFile(
                    "U-Net segmentation checkpoint 선택",
                    "PyTorch checkpoint (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                    currentPath,
                    out string selectedPath))
            {
                return;
            }

            TrainingSettingsViewModel.SegmentationUnetWeightsPath = selectedPath;
            RefreshSegmentationAdapterComparisonState();
        }

        private void ExecuteBrowseSegmentationYoloCheckpointCommand()
        {
            string currentPath = TrainingSettingsViewModel?.SegmentationYoloWeightsPath ?? string.Empty;
            if (!TryPickFile(
                    "YOLO segmentation checkpoint 선택",
                    "Ultralytics checkpoint (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                    currentPath,
                    out string selectedPath))
            {
                return;
            }

            TrainingSettingsViewModel.SegmentationYoloWeightsPath = selectedPath;
            RefreshSegmentationAdapterComparisonState();
        }

        private async void ExecuteRunSegmentationAdapterComparisonCommand()
        {
            if (isSegmentationAdapterComparisonRunning || TrainingSettingsViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            SaveTrainingEditorFields();
            WpfSegmentationAdapterComparisonRunRequest request = segmentationAdapterComparisonRunService.BuildRequest(
                global.Data,
                global.Data.ProjectSettings.PythonModel,
                TrainingSettingsViewModel.SegmentationUnetWeightsPath,
                TrainingSettingsViewModel.SegmentationYoloWeightsPath,
                TrainingSettingsViewModel.SelectedSegmentationYoloEngine);
            IReadOnlyList<string> validationErrors = segmentationAdapterComparisonRunService.ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = "U-Net vs YOLO-seg 비교 실행 불가: " + string.Join(" / ", validationErrors.Take(3));
                TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                    isRunning: false,
                    statusText: "준비 필요",
                    detailText: message,
                    actionText: "checkpoint와 각 실행기의 Python 경로를 확인한 뒤 다시 실행하세요.");
                SetYoloCommandStatus(message, isBusy: false);
                AppendLog(message);
                return;
            }

            isSegmentationAdapterComparisonRunning = true;
            TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                isRunning: true,
                statusText: "공통 마스크 비교 실행 중",
                detailText: "recipe 원본은 읽기 전용으로 유지합니다. canonical export와 두 raw prediction artifact를 생성하고 있습니다.",
                actionText: "완료 후 macro Dice/IoU와 결함 component TP/FP/FN을 확인하세요. 검사 모델 자동 교체는 하지 않습니다.");
            UpdateYoloCommandButtons();
            SetYoloCommandStatus("U-Net vs YOLO-seg 공통 마스크 비교 실행 중...", isBusy: true);
            AppendLog($"Segmentation adapter comparison started: unet={Path.GetFileName(request.UnetSettings.WeightsPath)}, yolo={Path.GetFileName(request.YoloSettings.WeightsPath)}, engine={request.YoloEngine}");

            try
            {
                WpfSegmentationAdapterComparisonRunResult result = await segmentationAdapterComparisonRunService
                    .RunAsync(request)
                    .ConfigureAwait(true);
                if (!result.Succeeded)
                {
                    string errorText = "U-Net vs YOLO-seg 비교 실패: " + FirstLine(result.Error);
                    TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                        isRunning: false,
                        statusText: "결과 확인 필요",
                        detailText: errorText,
                        actionText: "원본 레시피와 현재 검사 모델은 변경되지 않았습니다. 실행기, checkpoint, canonical export 조건을 확인하세요.");
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                    return;
                }

                SegmentationMaskComparisonResult comparison = result.Comparison;
                string resultText = $"test {comparison.Baseline.ImageCount}장 / macro Dice U-Net {comparison.Baseline.MeanDice:0.000} · {request.YoloEngine} {comparison.Candidate.MeanDice:0.000} / macro IoU U-Net {comparison.Baseline.MeanIoU:0.000} · {request.YoloEngine} {comparison.Candidate.MeanIoU:0.000}";
                string reportPath = comparison.ReportPath ?? string.Empty;
                string completeText = "U-Net vs YOLO-seg 비교 완료";
                TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                    isRunning: false,
                    statusText: completeText,
                    detailText: resultText,
                    actionText: "결과 artifact: " + reportPath + " / 이 결과만으로 검사 모델을 자동 교체하지 않습니다. 클래스별 Dice/IoU와 component FP/FN을 검토한 뒤 별도로 채택하세요.");
                SetYoloCommandStatus(completeText, isBusy: false);
                AppendLog(completeText + ": " + reportPath);
            }
            catch (Exception ex)
            {
                string errorText = "U-Net vs YOLO-seg 비교 실패: " + FirstLine(ex.Message);
                TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                    isRunning: false,
                    statusText: "결과 확인 필요",
                    detailText: errorText,
                    actionText: "원본 레시피와 현재 검사 모델은 변경되지 않았습니다. 실행 로그와 각 checkpoint를 확인하세요.");
                SetYoloCommandStatus(errorText, isBusy: false);
                AppendLog(errorText);
            }
            finally
            {
                isSegmentationAdapterComparisonRunning = false;
                UpdateYoloCommandButtons();
            }
        }

        private static string FirstLine(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "실행 상세가 없습니다.";
        }
    }
}
