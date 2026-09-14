using MvcVisionSystem._1._Core;
using System;
using System.Windows;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the lifetime of shell-owned auxiliary windows.
    /// The shell supplies state snapshots and navigation callbacks; this adapter
    /// owns creation, reuse, theme propagation, close handling, and disposal.
    /// </summary>
    internal sealed class ShellAuxiliaryWindowHost : IDisposable
    {
        private readonly Window owner;
        private readonly RuntimeDiagnosticsService diagnosticsService;
        private readonly Func<PythonModelSettings> capturePythonSettings;
        private readonly Action openModelSettings;
        private readonly Func<string> preferredModelBenchmarkSourcePath;
        private WpfEnvironmentSetupCenterWindow environmentSetupCenterWindow;
        private WpfModelBenchmarkWindow modelBenchmarkWindow;
        private bool isDisposed;

        internal ShellAuxiliaryWindowHost(
            Window owner,
            RuntimeDiagnosticsService diagnosticsService,
            Func<PythonModelSettings> capturePythonSettings,
            Action openModelSettings,
            Func<string> preferredModelBenchmarkSourcePath)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            this.capturePythonSettings = capturePythonSettings ?? throw new ArgumentNullException(nameof(capturePythonSettings));
            this.openModelSettings = openModelSettings ?? throw new ArgumentNullException(nameof(openModelSettings));
            this.preferredModelBenchmarkSourcePath = preferredModelBenchmarkSourcePath ?? (() => string.Empty);
        }

        internal void ShowEnvironmentSetupCenter()
        {
            if (isDisposed)
            {
                return;
            }

            if (environmentSetupCenterWindow == null)
            {
                var viewModel = new WpfEnvironmentSetupCenterViewModel(
                    diagnosticsService,
                    capturePythonSettings,
                    () =>
                    {
                        CloseEnvironmentSetupCenter();
                        openModelSettings();
                    });
                environmentSetupCenterWindow = new WpfEnvironmentSetupCenterWindow(viewModel)
                {
                    Owner = owner
                };
                environmentSetupCenterWindow.Closed += EnvironmentSetupCenterWindow_Closed;
                environmentSetupCenterWindow.ApplyThemeFrom(owner);
                environmentSetupCenterWindow.Show();
            }
            else
            {
                environmentSetupCenterWindow.ViewModel?.Refresh();
                environmentSetupCenterWindow.ApplyThemeFrom(owner);
                if (environmentSetupCenterWindow.WindowState == WindowState.Minimized)
                {
                    environmentSetupCenterWindow.WindowState = WindowState.Normal;
                }
            }

            environmentSetupCenterWindow.Activate();
        }

        internal void ShowModelBenchmark()
        {
            if (isDisposed)
            {
                return;
            }

            string preferredSourcePath = preferredModelBenchmarkSourcePath() ?? string.Empty;
            if (modelBenchmarkWindow == null)
            {
                var viewModel = new WpfModelBenchmarkViewModel(
                    repositoryRoot: ModelBenchmarkCatalogService.FindRepositoryRoot(),
                    preferredSourcePath: preferredSourcePath);
                modelBenchmarkWindow = new WpfModelBenchmarkWindow(viewModel)
                {
                    Owner = owner
                };
                modelBenchmarkWindow.Closed += ModelBenchmarkWindow_Closed;
                modelBenchmarkWindow.ApplyThemeFrom(owner);
                modelBenchmarkWindow.Show();
            }
            else
            {
                modelBenchmarkWindow.ViewModel?.Refresh(preferredSourcePath);
                modelBenchmarkWindow.ApplyThemeFrom(owner);
                if (modelBenchmarkWindow.WindowState == WindowState.Minimized)
                {
                    modelBenchmarkWindow.WindowState = WindowState.Normal;
                }
            }

            modelBenchmarkWindow.Activate();
        }

        internal void RefreshTheme()
        {
            if (isDisposed)
            {
                return;
            }

            environmentSetupCenterWindow?.ApplyThemeFrom(owner);
            modelBenchmarkWindow?.ApplyThemeFrom(owner);
        }

        internal void CloseAll()
        {
            environmentSetupCenterWindow?.Close();
            modelBenchmarkWindow?.Close();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            CloseAll();
            DetachEnvironmentSetupCenterWindow();
            DetachModelBenchmarkWindow();
        }

        private void EnvironmentSetupCenterWindow_Closed(object sender, EventArgs e)
        {
            if (sender is WpfEnvironmentSetupCenterWindow closedWindow
                && ReferenceEquals(environmentSetupCenterWindow, closedWindow))
            {
                DetachEnvironmentSetupCenterWindow();
            }
        }

        private void ModelBenchmarkWindow_Closed(object sender, EventArgs e)
        {
            if (sender is WpfModelBenchmarkWindow closedWindow
                && ReferenceEquals(modelBenchmarkWindow, closedWindow))
            {
                DetachModelBenchmarkWindow();
            }
        }

        private void CloseEnvironmentSetupCenter()
        {
            environmentSetupCenterWindow?.Close();
        }

        private void DetachEnvironmentSetupCenterWindow()
        {
            if (environmentSetupCenterWindow == null)
            {
                return;
            }

            environmentSetupCenterWindow.Closed -= EnvironmentSetupCenterWindow_Closed;
            environmentSetupCenterWindow = null;
        }

        private void DetachModelBenchmarkWindow()
        {
            if (modelBenchmarkWindow == null)
            {
                return;
            }

            modelBenchmarkWindow.Closed -= ModelBenchmarkWindow_Closed;
            modelBenchmarkWindow = null;
        }
    }
}
