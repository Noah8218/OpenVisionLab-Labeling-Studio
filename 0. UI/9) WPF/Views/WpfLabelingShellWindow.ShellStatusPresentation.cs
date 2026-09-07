using OpenVisionLab.Logging;
using System;
using System.Linq;
using MediaBrush = System.Windows.Media.Brush;
using WpfUiApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfUiApplicationThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;
using WpfUiWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace MvcVisionSystem
{
    // Responsibility group: shell status and theme presentation.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ShellStatus
        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private void SetDatasetStatus(string text)
        {
            string normalized = text ?? string.Empty;
            StatusBarViewModel.SetDatasetStatus(normalized);
            RefreshShellDatasetContext();
            UpdateWorkflowProgressStatus();
        }

        private void SetPythonStatus(string text)
        {
            string normalized = text ?? string.Empty;
            StatusBarViewModel.SetPythonStatus(normalized);
        }

        private void UpdateWorkflowProgressStatus()
        {
            if (StatusBarViewModel == null)
            {
                return;
            }

            int totalCount = imageQueueItems?.Count ?? 0;
            int completedCount = imageQueueItems?.Count(ImageQueueFilterService.IsCompletedQueueItem) ?? 0;
            ShellWorkflowStatus status = ShellWorkflowStatusPresentationService.Build(
                new ShellWorkflowStatusContext(
                    isInferenceMode: currentWorkflowMode == WorkflowMode.Inference,
                    totalImageCount: totalCount,
                    completedImageCount: completedCount,
                    hasPendingCandidates: pendingDetectionCandidates.Count > 0,
                    hasUnsavedAnnotationChanges: annotationDirtyState.IsDirty,
                    isTrainingReady: lastYoloTrainingReadinessReport?.IsReady == true,
                    hasActiveImage: activeImageBitmap != null && !activeImageSize.IsEmpty));
            StatusBarViewModel.SetWorkflowStatus(
                status.StageText,
                status.ProgressText,
                status.NextActionText);
        }

        private void SetModelStatus(string text)
        {
            string normalized = text ?? string.Empty;
            StatusBarViewModel.SetModelStatus(normalized);
        }

        private void SetInspectionModelStatus(string text, string toolTip = null)
        {
            string normalized = string.IsNullOrWhiteSpace(text)
                ? "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : text.Trim();
            string normalizedToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? normalized
                : toolTip.Trim();
            StatusBarViewModel.SetInspectionModelStatus(normalized, normalizedToolTip);
        }

        private void ExecuteToggleThemeCommand()
        {
            ApplyTheme(ShellTheme.Dark);
            AppendLog("테마 고정: 다크");
        }

        private void ApplyTheme(ShellTheme theme)
        {
            // Theme selection is intentionally hidden for the focused workstation product.
            // Keep legacy callers safe by treating every request as the supported dark theme.
            theme = ShellTheme.Dark;
            currentTheme = theme;
            WpfUiApplicationThemeManager.Apply(WpfUiApplicationTheme.Dark, WpfUiWindowBackdropType.None, updateAccent: true);
            WpfUiApplicationThemeManager.Apply(this);

            ThemePalette.ApplyDark(Resources, System.Windows.Application.Current?.Resources);

            if (FindResource("AppBackgroundBrush") is MediaBrush backgroundBrush)
            {
                Background = backgroundBrush;
            }

            RefreshModelBenchmarkWindowTheme();
            RefreshDatasetHealthWindowTheme();
            RefreshDatasetInterchangeWindowTheme();
            RefreshEnvironmentSetupCenterWindowTheme();
            UpdateWorkflowModeUi();
            UpdateQueueQuickFilterButtons();
        }

        private void AppendLog(string message)
        {
            ShellLogViewModel?.RecordLog(message);
            OVLog.Write(LogCategory.Main, LogLevel.Info, message);
        }
        #endregion

    }
}
