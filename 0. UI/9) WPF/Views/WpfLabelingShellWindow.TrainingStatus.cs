using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
using ProgressBar = System.Windows.Controls.ProgressBar;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool BeginTrainingCommand(string statusText)
        {
            if (isTrainingCommandRunning || isYoloEnvironmentCommandRunning || isDetecting || isBatchDetectionRunning)
            {
                AppendLog("YOLO 또는 학습 명령이 이미 실행 중입니다.");
                return false;
            }

            isTrainingCommandRunning = true;
            ClearYoloRecoveryStatus();
            SetTrainingReadinessStatus(statusText);
            SetTrainingProgressStatus(
                string.IsNullOrWhiteSpace(statusText) ? "\uD559\uC2B5 \uBA85\uB839 \uC2E4\uD589 \uC911" : statusText,
                string.Empty,
                0D,
                isIndeterminate: true);
            SetTrainingStatusBrushes(
                TrainingSettingsViewModel?.TrainingReadinessForeground ?? TrainingReadinessText?.Foreground,
                MediaBrushes.DodgerBlue);
            UpdateYoloCommandButtons();
            return true;
        }

        private void EndTrainingCommand()
        {
            isTrainingCommandRunning = false;
            SyncTrainingReadinessFromTextBlockIfBindingWasBroken();
            SetTrainingProgressBusy(false);
            UpdateTrainingProgressFromWorker();
            UpdateYoloCommandButtons();
            RefreshYoloStatus();
        }

        private void SetTrainingReadinessStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingReadinessText(normalized);
                return;
            }

            if (TrainingReadinessText != null)
            {
                TrainingReadinessText.Text = normalized;
            }
        }

        private string GetTrainingReadinessStatus()
        {
            return TrainingReadinessText?.Text
                ?? TrainingSettingsViewModel?.TrainingReadinessText
                ?? string.Empty;
        }

        private void SyncTrainingReadinessFromTextBlockIfBindingWasBroken()
        {
            if (TrainingSettingsViewModel == null
                || TrainingReadinessText == null
                || BindingOperations.GetBindingExpressionBase(TrainingReadinessText, TextBlock.TextProperty) != null)
            {
                return;
            }

            SetTrainingReadinessStatus(TrainingReadinessText.Text);
        }

        private void SetTrainingProgressStatus(string progressText, string epochText, double progressValue, bool isIndeterminate)
        {
            string normalizedProgress = progressText ?? string.Empty;
            string normalizedEpoch = epochText ?? string.Empty;
            ShellViewModel?.SetModelCenterTrainingState(
                normalizedProgress,
                string.IsNullOrWhiteSpace(normalizedEpoch)
                    ? TrainingSettingsViewModel?.TrainingReadinessText
                    : normalizedEpoch);
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingProgress(normalizedProgress, normalizedEpoch, progressValue, isIndeterminate);
                return;
            }

            if (TrainingProgressText != null)
            {
                TrainingProgressText.Text = normalizedProgress;
            }

            if (TrainingEpochText != null)
            {
                TrainingEpochText.Text = normalizedEpoch;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.Value = Math.Clamp(progressValue, 0D, 100D);
                TrainingProgressBar.IsIndeterminate = isIndeterminate;
            }
        }

        private void SetTrainingProgressValue(double value)
        {
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingProgressValue(value);
                return;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.Value = Math.Clamp(value, 0D, 100D);
            }
        }

        private void SetTrainingProgressBusy(bool isBusy)
        {
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingProgressBusy(isBusy);
                return;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.IsIndeterminate = isBusy;
            }
        }

        private void SetTrainingStatusBrushes(MediaBrush readinessBrush, MediaBrush progressBrush)
        {
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingStatusBrushes(readinessBrush, progressBrush);
                return;
            }

            if (TrainingReadinessText != null)
            {
                TrainingReadinessText.Foreground = readinessBrush;
            }

            if (TrainingProgressText != null)
            {
                TrainingProgressText.Foreground = progressBrush;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.Foreground = progressBrush;
            }
        }

        private void EnsureTrainingStatusBindings()
        {
            if (TrainingSettingsViewModel == null)
            {
                return;
            }

            // These fallback bindings protect the MVVM migration path when legacy name proxies are still registered.
            EnsureBinding(TrainingReadinessText, TextBlock.TextProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingReadinessText));
            EnsureBinding(TrainingReadinessText, TextBlock.ForegroundProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingReadinessForeground));
            EnsureBinding(TrainingProgressText, TextBlock.TextProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressText));
            EnsureBinding(TrainingProgressText, TextBlock.ForegroundProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressForeground));
            EnsureBinding(TrainingEpochText, TextBlock.TextProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingEpochStatusText));
            EnsureBinding(TrainingProgressBar, ProgressBar.ValueProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressValue));
            EnsureBinding(TrainingProgressBar, ProgressBar.IsIndeterminateProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressIsIndeterminate));
            EnsureBinding(TrainingProgressBar, ProgressBar.ForegroundProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressForeground));
        }

        private static void EnsureBinding(DependencyObject target, DependencyProperty property, string path)
        {
            if (target == null || BindingOperations.GetBindingExpressionBase(target, property) != null)
            {
                return;
            }

            BindingOperations.SetBinding(target, property, new Binding(path) { Mode = BindingMode.OneWay });
        }

        private void RefreshTrainingReadinessPanel(bool refreshYaml)
        {
            EnsureProjectSettings();
            ExternalYoloDatasetSettings externalDataset = global.Data.ProjectSettings.ExternalYoloDataset;
            if (externalDataset?.UseForTraining == true)
            {
                // A normal UI refresh uses the persisted snapshot. The explicit refresh action and training start still scan the source again.
                YoloExternalDatasetIntakeReport externalReport = null;
                if (refreshYaml)
                {
                    externalReport = YoloExternalDatasetIntakeService.Build(
                        externalDataset.DataYamlFilePath,
                        externalDataset.DatasetPurpose);
                    if (externalReport.IsReady
                        && !YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(externalDataset, externalReport, out string identityError))
                    {
                        YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, externalReport);
                        YoloExternalDatasetIntakeService.MarkSourceIdentityRequiresReactivation(externalDataset, identityError);
                    }
                    else
                    {
                        YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, externalReport);
                    }
                }

                RefreshExternalTrainingReadinessPanel(externalReport);
                return;
            }

            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(global.Data, refreshYaml);
            string readinessText;
            if (report.IsReady)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                IReadOnlyList<string> warnings = YoloDatasetDiagnosticsService.BuildQualityWarnings(global.Data, statistics);
                string readyPrefix = warnings.Count > 0 ? "학습 준비 완료(주의)" : "학습 준비 완료";
                string segmentText = statistics.TotalSegmentationObjectCount > 0
                    ? $" / 세그먼트 {statistics.TotalSegmentationObjectCount}"
                    : string.Empty;
                readinessText =
                    $"{readyPrefix}. 학습 {statistics.TrainImageCount} / 검증 {statistics.ValidImageCount} / 테스트 {statistics.TestImageCount} / 객체 {statistics.TotalObjectCount}{segmentText} / 클래스 {global.Data.ClassNamedList.Count}";
            }
            else
            {
                YoloDatasetStatistics statistics = report.Statistics;
                string segmentText = statistics.TotalSegmentationObjectCount > 0
                    ? $" / 세그먼트 {statistics.TotalSegmentationObjectCount}"
                    : string.Empty;
                readinessText =
                    $"학습 데이터 확인 필요: {report.Errors.FirstOrDefault() ?? "원인 미확인"} / 학습 {statistics.TrainImageCount} / 검증 {statistics.ValidImageCount} / 테스트 {statistics.TestImageCount}{segmentText}";
            }

            readinessText = WpfTrainingReadinessPresentationService.BuildStatusText(global.Data, report);
            SetTrainingReadinessStatus(readinessText);
            UpdateYoloTrainingChecklist(report, recordHistory: refreshYaml);
            UpdateTrainingProgressFromWorker();
        }

        private void RefreshExternalTrainingReadinessPanel(YoloExternalDatasetIntakeReport externalReport)
        {
            RefreshExternalYoloDatasetIntakePresentation();
            ExternalYoloDatasetSettings settings = global?.Data?.ProjectSettings?.ExternalYoloDataset;
            bool isReady = settings?.LastValidationSucceeded == true;
            LabelingDatasetPurpose purpose = externalReport?.Purpose ?? settings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
            int trainCount = externalReport?.Train.ImageCount ?? settings?.TrainImageCount ?? 0;
            int validCount = externalReport?.Valid.ImageCount ?? settings?.ValidImageCount ?? 0;
            int testCount = externalReport?.Test.ImageCount ?? settings?.TestImageCount ?? 0;
            int annotationCount = externalReport?.TotalAnnotationCount ?? settings?.AnnotationCount ?? 0;
            int classCount = externalReport?.ClassNames.Count ?? settings?.ClassCount ?? 0;
            string validationDetail = isReady && externalReport?.IsReady == true
                ? externalReport.Summary
                : externalReport != null && !externalReport.IsReady
                    ? string.Join(" ", externalReport.Errors.Take(2))
                    : settings?.LastValidationSummary ?? string.Empty;
            string statusPrefix = externalReport == null ? "외부 YOLO data.yaml 마지막 검증" : "외부 YOLO data.yaml 준비";
            string externalReadinessText = isReady
                ? $"{statusPrefix} 완료: {YoloExternalDatasetIntakeService.FormatPurpose(purpose)} / 학습 {trainCount} / 검증 {validCount} / 테스트 {testCount} / 객체 {annotationCount} / 클래스 {classCount}"
                : "외부 YOLO data.yaml 확인 필요: " + (string.IsNullOrWhiteSpace(validationDetail) ? "원인을 확인하세요." : validationDetail);
            SetTrainingReadinessStatus(externalReadinessText);
            if (LearningWorkflowViewModel != null)
            {
                LearningWorkflowViewModel.TrainingChecklistStatusText = isReady
                    ? externalReport == null
                        ? "데이터셋: 외부 YOLO data.yaml 마지막 검증 통과"
                        : "데이터셋: 외부 YOLO data.yaml 준비 완료"
                    : "데이터셋: 외부 YOLO data.yaml 확인 필요";
                LearningWorkflowViewModel.TrainingChecklistDetailText = validationDetail;
                LearningWorkflowViewModel.TrainingChecklistActionText = isReady
                    ? externalReport == null
                        ? "다음: 학습 시작 시 원본 외부 data.yaml을 다시 검증합니다."
                        : "다음: 학습/모델 탭에서 시작합니다. 원본 외부 데이터는 변경하지 않습니다."
                    : "다음: data.yaml 경로, train/val 분할, names와 라벨 형식을 수정한 뒤 다시 확인합니다.";
            }

            UpdateTrainingProgressFromWorker();
        }


    }
}
