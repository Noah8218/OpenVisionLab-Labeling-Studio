using System;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    /// <summary>
    /// Projects a shell state snapshot into the status-bar ViewModel. The
    /// adapter owns status presentation only; the Window remains responsible
    /// for the WPF theme and logging sinks.
    /// </summary>
    internal sealed class ShellStatusPresentationAdapter
    {
        private readonly WpfStatusBarPanelViewModel statusBarViewModel;
        private readonly Func<ShellWorkflowStatusContext> workflowStatusContextProvider;
        private readonly Action refreshDatasetContext;

        internal ShellStatusPresentationAdapter(
            WpfStatusBarPanelViewModel statusBarViewModel,
            Func<ShellWorkflowStatusContext> workflowStatusContextProvider,
            Action refreshDatasetContext)
        {
            this.statusBarViewModel = statusBarViewModel
                ?? throw new ArgumentNullException(nameof(statusBarViewModel));
            this.workflowStatusContextProvider = workflowStatusContextProvider
                ?? throw new ArgumentNullException(nameof(workflowStatusContextProvider));
            this.refreshDatasetContext = refreshDatasetContext
                ?? throw new ArgumentNullException(nameof(refreshDatasetContext));
        }

        #region StatusProjection
        internal void SetDatasetStatus(string text)
        {
            string normalized = text ?? string.Empty;
            statusBarViewModel.SetDatasetStatus(normalized);
            refreshDatasetContext();
            UpdateWorkflowProgressStatus();
        }

        internal void SetPythonStatus(string text)
        {
            statusBarViewModel.SetPythonStatus(text ?? string.Empty);
        }

        internal void UpdateWorkflowProgressStatus()
        {
            ShellWorkflowStatusContext context = workflowStatusContextProvider();
            if (context == null)
            {
                return;
            }

            ShellWorkflowStatus status = ShellWorkflowStatusPresentationService.Build(context);
            statusBarViewModel.SetWorkflowStatus(
                status.StageText,
                status.ProgressText,
                status.NextActionText);
        }

        internal void SetModelStatus(string text)
        {
            statusBarViewModel.SetModelStatus(text ?? string.Empty);
        }

        internal void SetInspectionModelStatus(string text, string toolTip = null)
        {
            string normalized = string.IsNullOrWhiteSpace(text)
                ? "검사 모델: 없음"
                : text.Trim();
            string normalizedToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? normalized
                : toolTip.Trim();
            statusBarViewModel.SetInspectionModelStatus(normalized, normalizedToolTip);
        }
        #endregion
    }
}
