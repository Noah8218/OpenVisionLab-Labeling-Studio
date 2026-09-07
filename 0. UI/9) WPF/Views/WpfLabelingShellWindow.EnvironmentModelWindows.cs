using MvcVisionSystem._1._Core;
using System;
using System.Windows;
using System.IO;

namespace MvcVisionSystem
{
    // Responsibility group: environment setup and model benchmark windows.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region EnvironmentSetupCenter
        private WpfEnvironmentSetupCenterWindow environmentSetupCenterWindow;

        private void ExecuteOpenEnvironmentSetupCenterCommand()
        {
            HeaderToolsPopup.IsOpen = false;
            if (environmentSetupCenterWindow == null)
            {
                var viewModel = new WpfEnvironmentSetupCenterViewModel(
                    RuntimeDiagnosticsViewModel.DiagnosticsService,
                    CaptureEnvironmentSetupPythonSettings,
                    ExecuteOpenModelSettingsFromEnvironmentSetupCenter);
                environmentSetupCenterWindow = new WpfEnvironmentSetupCenterWindow(viewModel)
                {
                    Owner = this
                };
                environmentSetupCenterWindow.Closed += EnvironmentSetupCenterWindow_Closed;
                environmentSetupCenterWindow.ApplyThemeFrom(this);
                environmentSetupCenterWindow.Show();
            }
            else
            {
                environmentSetupCenterWindow.ViewModel?.Refresh();
                environmentSetupCenterWindow.ApplyThemeFrom(this);
                if (environmentSetupCenterWindow.WindowState == WindowState.Minimized)
                {
                    environmentSetupCenterWindow.WindowState = WindowState.Normal;
                }
            }

            environmentSetupCenterWindow.Activate();
        }

        private PythonModelSettings CaptureEnvironmentSetupPythonSettings()
            => global.Data.ProjectSettings?.PythonModel ?? new PythonModelSettings();

        private void ExecuteOpenModelSettingsFromEnvironmentSetupCenter()
        {
            environmentSetupCenterWindow?.Close();
            FocusYoloModelSettingsTab();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
        }

        private void EnvironmentSetupCenterWindow_Closed(object sender, EventArgs e)
        {
            if (environmentSetupCenterWindow != null)
            {
                environmentSetupCenterWindow.Closed -= EnvironmentSetupCenterWindow_Closed;
                environmentSetupCenterWindow = null;
            }
        }

        private void CloseEnvironmentSetupCenterWindow()
        {
            environmentSetupCenterWindow?.Close();
        }

        private void RefreshEnvironmentSetupCenterWindowTheme()
        {
            environmentSetupCenterWindow?.ApplyThemeFrom(this);
        }
        #endregion

        #region ModelBenchmark
        private WpfModelBenchmarkWindow modelBenchmarkWindow;

        private void ExecuteOpenModelBenchmarkCommand()
        {
            string preferredSourcePath = ResolvePreferredModelBenchmarkSourcePath();
            if (modelBenchmarkWindow == null)
            {
                var viewModel = new WpfModelBenchmarkViewModel(
                    repositoryRoot: ModelBenchmarkCatalogService.FindRepositoryRoot(),
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
        #endregion

    }
}
