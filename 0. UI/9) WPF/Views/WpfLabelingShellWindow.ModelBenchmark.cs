using System;
using System.IO;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private WpfModelBenchmarkWindow modelBenchmarkWindow;

        private void ExecuteOpenModelBenchmarkCommand()
        {
            string preferredSourcePath = ResolvePreferredModelBenchmarkSourcePath();
            if (modelBenchmarkWindow == null)
            {
                var viewModel = new WpfModelBenchmarkViewModel(
                    repositoryRoot: WpfModelBenchmarkCatalogService.FindRepositoryRoot(),
                    preferredSourcePath: preferredSourcePath);
                modelBenchmarkWindow = new WpfModelBenchmarkWindow(viewModel)
                {
                    Owner = this
                };
                modelBenchmarkWindow.Closed += ModelBenchmarkWindow_Closed;
                modelBenchmarkWindow.ApplyThemeFrom(this);
                modelBenchmarkWindow.Show();
            }
            else
            {
                modelBenchmarkWindow.ViewModel?.Refresh(preferredSourcePath);
                modelBenchmarkWindow.ApplyThemeFrom(this);
                if (modelBenchmarkWindow.WindowState == WindowState.Minimized)
                {
                    modelBenchmarkWindow.WindowState = WindowState.Normal;
                }
            }

            modelBenchmarkWindow.Activate();
        }

        private string ResolvePreferredModelBenchmarkSourcePath()
        {
            if (global.Data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                string anomalySummaryPath = ResolveModelCenterAnomalyEvaluationSummaryPath(global.Data.OutputRootPath);
                if (!string.IsNullOrWhiteSpace(anomalySummaryPath) && File.Exists(anomalySummaryPath))
                {
                    return anomalySummaryPath;
                }
            }

            return CandidateReviewViewModel?.SelectedModelComparisonHistoryItem?.SourcePath ?? string.Empty;
        }

        private void ModelBenchmarkWindow_Closed(object sender, EventArgs e)
        {
            if (modelBenchmarkWindow != null)
            {
                modelBenchmarkWindow.Closed -= ModelBenchmarkWindow_Closed;
                modelBenchmarkWindow = null;
            }
        }

        private void CloseModelBenchmarkWindow()
        {
            modelBenchmarkWindow?.Close();
        }

        private void RefreshModelBenchmarkWindowTheme()
        {
            modelBenchmarkWindow?.ApplyThemeFrom(this);
        }
    }
}
