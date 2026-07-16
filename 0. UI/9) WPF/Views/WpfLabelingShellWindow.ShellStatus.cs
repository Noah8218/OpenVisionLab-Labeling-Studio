using OpenVisionLab.Logging;
using System;
using System.Linq;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUiApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfUiApplicationThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;
using WpfUiWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private void SetDatasetStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetDatasetStatus(normalized);
                RefreshShellDatasetContext();
                UpdateWorkflowProgressStatus();
                return;
            }

            if (DatasetStatusText != null)
            {
                DatasetStatusText.Text = normalized;
            }
            RefreshShellDatasetContext();
        }

        private void SetPythonStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetPythonStatus(normalized);
                return;
            }

            if (PythonStatusText != null)
            {
                PythonStatusText.Text = normalized;
            }
        }

        private void UpdateWorkflowProgressStatus()
        {
            if (StatusBarViewModel == null)
            {
                return;
            }

            int totalCount = imageQueueItems?.Count ?? 0;
            int completedCount = imageQueueItems?.Count(WpfImageQueueFilterService.IsCompletedQueueItem) ?? 0;
            int remainingCount = Math.Max(0, totalCount - completedCount);
            string stageText = ResolveWorkflowStageText(totalCount);
            string progressText = totalCount > 0
                ? $"진행: {completedCount}/{totalCount} 완료 · {remainingCount} 남음"
                : "진행: 이미지 없음";
            string nextActionText = ResolveWorkflowNextActionText(totalCount, remainingCount);
            StatusBarViewModel.SetWorkflowStatus(stageText, progressText, nextActionText);
        }

        private string ResolveWorkflowStageText(int totalCount)
        {
            if (currentWorkflowMode == WorkflowMode.Inference)
            {
                return pendingDetectionCandidates.Count > 0
                    ? "단계: AI 후보 검토"
                    : "단계: AI 후보 대기";
            }

            if (!string.IsNullOrWhiteSpace(annotationDirtyReason))
            {
                return "단계: 저장 필요";
            }

            if (lastYoloTrainingReadinessReport?.IsReady == true)
            {
                return "단계: 학습 준비";
            }

            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return totalCount > 0
                    ? "단계: 이미지 선택"
                    : "단계: 데이터셋 준비";
            }

            return activeImageBitmap != null && !activeImageSize.IsEmpty
                ? "단계: 라벨링"
                : "단계: 준비";
        }

        private string ResolveWorkflowNextActionText(int totalCount, int remainingCount)
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return totalCount > 0
                    ? "다음: 이미지 선택"
                    : "다음: 데이터셋 시작";
            }

            if (pendingDetectionCandidates.Count > 0)
            {
                return "다음: AI 후보 확정/스킵";
            }

            if (!string.IsNullOrWhiteSpace(annotationDirtyReason))
            {
                return "다음: 저장";
            }

            if (totalCount > 0 && remainingCount > 0)
            {
                return "다음: 다음 미완료 이미지";
            }

            if (totalCount > 0)
            {
                return lastYoloTrainingReadinessReport?.IsReady == true
                    ? "다음: 학습 시작"
                    : "다음: 데이터셋 점검";
            }

            return "다음: 이미지 폴더";
        }

        private void SetModelStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetModelStatus(normalized);
                return;
            }

            if (ModelStatusText != null)
            {
                ModelStatusText.Text = normalized;
            }
        }

        private void SetInspectionModelStatus(string text, string toolTip = null)
        {
            string normalized = string.IsNullOrWhiteSpace(text)
                ? "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : text.Trim();
            string normalizedToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? normalized
                : toolTip.Trim();
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetInspectionModelStatus(normalized, normalizedToolTip);
                return;
            }

            if (InspectionModelStatusText != null)
            {
                InspectionModelStatusText.Text = normalized;
                InspectionModelStatusText.ToolTip = normalizedToolTip;
            }
        }

        private void ExecuteToggleThemeCommand()
        {
            ApplyTheme(currentTheme == ShellTheme.Dark ? ShellTheme.Light : ShellTheme.Dark);
            AppendLog(currentTheme == ShellTheme.Dark ? "테마 변경: 다크" : "테마 변경: 라이트");
        }

        private void ApplyTheme(ShellTheme theme)
        {
            currentTheme = theme;
            WpfUiApplicationThemeManager.Apply(ToWpfUiTheme(theme), WpfUiWindowBackdropType.None, updateAccent: true);
            WpfUiApplicationThemeManager.Apply(this);

            // Theme resources stay centralized so split view partials do not introduce conflicting palette keys.
            if (theme == ShellTheme.Light)
            {
                SetThemeBrush("AppBackgroundBrush", "#F4F6F8");
                SetThemeBrush("FrameBrush", "#FFFFFF");
                SetThemeBrush("PanelBrush", "#FFFFFF");
                SetThemeBrush("PanelHeaderBrush", "#F1F3F6");
                SetThemeBrush("CanvasBrush", "#EDF2F8");
                SetThemeBrush("StatusBarBrush", "#FFFFFF");
                SetThemeBrush("BorderBrushDark", "#D8DEE8");
                SetThemeBrush("PrimaryTextBrush", "#151922");
                SetThemeBrush("SecondaryTextBrush", "#566170");
                SetThemeBrush("AccentBrush", "#E53935");
                SetThemeBrush("ToolbarButtonBrush", "#F7F9FC");
                SetThemeBrush("ToolbarButtonBorderBrush", "#CBD3DF");
                SetThemeBrush("ToolbarButtonHoverBrush", "#E8EEF7");
                SetThemeBrush("ToolbarButtonPressedBrush", "#DCE5F0");
                SetThemeBrush("ToolbarButtonDisabledBrush", "#EBEFF4");
                SetThemeBrush("ToolbarButtonDisabledBorderBrush", "#D5DDE8");
                SetThemeBrush("DisabledTextBrush", "#97A0AE");
                SetThemeBrush("InputBrush", "#FFFFFF");
                SetThemeBrush("InputBorderBrush", "#CAD2DD");
                SetThemeBrush("GridLineBrush", "#D6DEE8");
                SetThemeBrush("GridHeaderBrush", "#F1F3F6");
                SetThemeBrush("RowHoverBrush", "#E8EEF7");
                SetThemeBrush("SelectedRowBrush", "#DCEBFF");
                SetThemeBrush("SelectedRowTextBrush", "#101820");
                SetThemeBrush("DetectionOverlayBackgroundBrush", "#EAF7EF");
                SetThemeBrush("DetectionOverlayBorderBrush", "#3C22A65A");
                SetThemeBrush("DetectionOverlayTitleTextBrush", "#12351F");
                SetThemeBrush("DetectionOverlaySummaryTextBrush", "#157F3A");
                SetThemeBrush("DetectionOverlaySelectedBackgroundBrush", "#D9F4E2");
                SetThemeBrush("DetectionOverlaySelectedTextBrush", "#0E3B20");
                SetThemeBrush("DetectionOverlayDetailTextBrush", "#2E5A3D");
            }
            else
            {
                SetThemeBrush("AppBackgroundBrush", "#0C0D0F");
                SetThemeBrush("FrameBrush", "#0A0B0D");
                SetThemeBrush("PanelBrush", "#171717");
                SetThemeBrush("PanelHeaderBrush", "#1F1F1F");
                SetThemeBrush("CanvasBrush", "#101820");
                SetThemeBrush("StatusBarBrush", "#0F1115");
                SetThemeBrush("BorderBrushDark", "#303030");
                SetThemeBrush("PrimaryTextBrush", "#F7F7F7");
                SetThemeBrush("SecondaryTextBrush", "#B7B7B7");
                SetThemeBrush("AccentBrush", "#E53935");
                SetThemeBrush("ToolbarButtonBrush", "#252525");
                SetThemeBrush("ToolbarButtonBorderBrush", "#3A3A3A");
                SetThemeBrush("ToolbarButtonHoverBrush", "#333333");
                SetThemeBrush("ToolbarButtonPressedBrush", "#1D1D1D");
                SetThemeBrush("ToolbarButtonDisabledBrush", "#20242A");
                SetThemeBrush("ToolbarButtonDisabledBorderBrush", "#2B3038");
                SetThemeBrush("DisabledTextBrush", "#69707A");
                SetThemeBrush("InputBrush", "#242424");
                SetThemeBrush("InputBorderBrush", "#3A3A3A");
                SetThemeBrush("GridLineBrush", "#2A2A2A");
                SetThemeBrush("GridHeaderBrush", "#202020");
                SetThemeBrush("RowHoverBrush", "#222A33");
                SetThemeBrush("SelectedRowBrush", "#26384F");
                SetThemeBrush("SelectedRowTextBrush", "#FFFFFF");
                SetThemeBrush("DetectionOverlayBackgroundBrush", "#F00B1320");
                SetThemeBrush("DetectionOverlayBorderBrush", "#5524D366");
                SetThemeBrush("DetectionOverlayTitleTextBrush", "#FFFFFF");
                SetThemeBrush("DetectionOverlaySummaryTextBrush", "#BEEBD0");
                SetThemeBrush("DetectionOverlaySelectedBackgroundBrush", "#1F24D366");
                SetThemeBrush("DetectionOverlaySelectedTextBrush", "#FFFFFF");
                SetThemeBrush("DetectionOverlayDetailTextBrush", "#C9D4E2");
            }

            ThemeToggleText.Text = theme == ShellTheme.Dark ? "테마: 다크" : "테마: 라이트";
            if (FindResource("AppBackgroundBrush") is MediaBrush backgroundBrush)
            {
                Background = backgroundBrush;
            }

            RefreshModelBenchmarkWindowTheme();
            UpdateWorkflowModeUi();
            UpdateQueueQuickFilterButtons();
        }

        private void SetThemeBrush(string key, string color)
        {
            Resources[key] = new MediaSolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(color));
        }

        private static WpfUiApplicationTheme ToWpfUiTheme(ShellTheme theme)
        {
            return theme == ShellTheme.Light ? WpfUiApplicationTheme.Light : WpfUiApplicationTheme.Dark;
        }

        private void AppendLog(string message)
        {
            ShellLogViewModel?.RecordLog(message);
            OVLog.Write(LogCategory.Main, LogLevel.Info, message);
        }
    }
}
