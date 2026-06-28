using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

            SetTrainingReadinessStatus(readinessText);
            UpdateYoloTrainingChecklist(report, recordHistory: refreshYaml);
            UpdateTrainingProgressFromWorker();
        }


    }
}
