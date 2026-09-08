using MvcVisionSystem._1._Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // PL-0037 owns remaining environment execution, not the already-complete Core,
    // presentation or inference owners. Reuse these boundaries; reopening requires
    // boundary-specific evidence recorded in STABLE_VERIFIED_AREAS and the task ledger.
    // Admission and callbacks run on the caller's UI context; dependencies stay lazy.
    public sealed class YoloEnvironmentWorkflowService : IDisposable
    {
        private readonly Func<PythonModelSettings> settingsAccessor;
        private readonly Func<PythonModelSettings> draftSettingsAccessor;
        private readonly Func<PythonModelRuntimeState> runtimeStateAccessor;
        private readonly Func<int, CancellationToken, Task<bool>> restartWorkerAsync;
        private readonly Func<CancellationToken, Task> stopWorkerAsync;
        private readonly Action<string> queryWorkerStatus;
        private readonly Func<string> workerFailureAccessor;
        private readonly Func<PythonModelSettings, Task<PythonEnvironmentCheckResult>> checkRequirementsAsync;
        private readonly Func<PythonModelSettings, Task<PythonPackageInstallResult>> installRequirementsAsync;
        private readonly Func<PythonModelSettings, bool, Task<PythonPackageInstallResult>> runPackageAsync;
        private CancellationTokenSource workerCancellation;
        private bool disposed;

        public YoloEnvironmentWorkflowService(
            Func<PythonModelSettings> settingsAccessor,
            Func<PythonModelSettings> draftSettingsAccessor,
            Func<PythonModelRuntimeState> runtimeStateAccessor,
            Func<int, CancellationToken, Task<bool>> restartWorkerAsync,
            Func<CancellationToken, Task> stopWorkerAsync,
            Action<string> queryWorkerStatus,
            Func<string> workerFailureAccessor,
            Func<PythonModelSettings, Task<PythonEnvironmentCheckResult>> checkRequirementsAsync = null,
            Func<PythonModelSettings, Task<PythonPackageInstallResult>> installRequirementsAsync = null,
            Func<PythonModelSettings, bool, Task<PythonPackageInstallResult>> runPackageAsync = null)
        {
            this.settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
            this.draftSettingsAccessor = draftSettingsAccessor ?? throw new ArgumentNullException(nameof(draftSettingsAccessor));
            this.runtimeStateAccessor = runtimeStateAccessor ?? throw new ArgumentNullException(nameof(runtimeStateAccessor));
            this.restartWorkerAsync = restartWorkerAsync ?? throw new ArgumentNullException(nameof(restartWorkerAsync));
            this.stopWorkerAsync = stopWorkerAsync ?? throw new ArgumentNullException(nameof(stopWorkerAsync));
            this.queryWorkerStatus = queryWorkerStatus ?? throw new ArgumentNullException(nameof(queryWorkerStatus));
            this.workerFailureAccessor = workerFailureAccessor ?? throw new ArgumentNullException(nameof(workerFailureAccessor));
            this.checkRequirementsAsync = checkRequirementsAsync ?? (settings => PythonEnvironmentService.CheckRequirementsAsync(settings));
            this.installRequirementsAsync = installRequirementsAsync ?? (settings => PythonEnvironmentService.InstallRequirementsAsync(settings));
            this.runPackageAsync = runPackageAsync ?? ((settings, uninstall) => uninstall
                ? PythonEnvironmentService.UninstallPackageAsync(settings, "ultralytics")
                : PythonEnvironmentService.InstallPackageAsync(settings, "ultralytics"));
        }

        public bool IsRunning { get; private set; }

        public async Task CheckAsync(YoloEnvironmentCallbacks view)
        {
            if (!Begin(view, YoloEnvironmentCommandPresentationService.BuildEnvironmentCheckStartingStatus()))
            {
                return;
            }

            try
            {
                PythonModelSettings settings = settingsAccessor();
                PythonModelRuntimeState runtimeState = runtimeStateAccessor();
                PythonModelValidationResult result = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                    ? new PythonModelValidationResult(new[] { runtimeState.NextActionText }, Array.Empty<string>())
                    : PythonModelSettingsValidator.Validate(settings, requireWeights: true);
                view.RefreshStatus();
                view.ShowModelCenter();
                await view.RefreshSettingsAsync(result).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }

                if (result.IsValid)
                {
                    string readyStatus = YoloEnvironmentCommandPresentationService.BuildEnvironmentReadyStatus();
                    view.SetCommandStatus(readyStatus, false);
                    view.AppendLog(readyStatus);
                    return;
                }

                view.SetCommandStatus(YoloEnvironmentCommandPresentationService.BuildEnvironmentNeedsAttentionStatus(), false);
                view.AppendLog(YoloEnvironmentCommandPresentationService.BuildEnvironmentNeedsAttentionLogHeader());
                foreach (string line in result.Errors.Concat(result.Warnings))
                {
                    view.AppendLog($"- {line}");
                }
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    string failureStatus = YoloEnvironmentCommandPresentationService.BuildEnvironmentCheckFailureStatus(ex.Message);
                    view.SetCommandStatus(failureStatus, false);
                    view.AppendLog(failureStatus);
                }
            }
            finally
            {
                End(view);
            }
        }

        public async Task InstallRequirementsAsync(YoloEnvironmentCallbacks view)
        {
            if (!Begin(view, YoloEnvironmentCommandPresentationService.BuildRequirementsCheckStartingStatus()))
            {
                return;
            }

            try
            {
                PythonModelSettings settings = settingsAccessor();
                PythonModelRuntimeState runtimeState = runtimeStateAccessor();
                if (!runtimeState.IsRuntimeInstalled)
                {
                    view.ShowRuntimeUnavailable(runtimeState.NextActionText, runtimeState);
                    return;
                }

                PythonEnvironmentCheckResult check = await checkRequirementsAsync(settings)
                    .ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }

                RequirementsCheckPresentation checkPresentation =
                    YoloEnvironmentCommandPresentationService.BuildRequirementsCheckPresentation(check);
                view.SetCommandStatus(checkPresentation.StatusText, checkPresentation.IsBusy);

                if (!checkPresentation.ShouldInstallRequirements)
                {
                    await view.RefreshSettingsAsync(null).ConfigureAwait(true);
                    if (disposed)
                    {
                        return;
                    }
                    view.AppendLog(checkPresentation.LogText);
                    return;
                }

                view.AppendLog(checkPresentation.LogText);
                PythonPackageInstallResult install = await installRequirementsAsync(settings)
                    .ConfigureAwait(true);
                // A completed install must not enter the disposed settings-refresh owner.
                if (disposed) return;

                await view.RefreshSettingsAsync(null).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }
                view.SetCommandStatus(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallResultStatus(install), false);
                view.AppendLog(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallResultLog(install));
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    view.SetCommandStatus(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallFailureStatus(ex.Message), false);
                    view.AppendLog(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallFailureLog(ex.Message));
                }
            }
            finally
            {
                End(view);
            }
        }

        public async Task RunPackageAsync(bool uninstall, YoloEnvironmentCallbacks view)
        {
            if (disposed)
            {
                return;
            }

            string operationName = YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationName(uninstall);
            PythonModelSettings settings = draftSettingsAccessor();
            PythonModelRuntimeInstallPlan plan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
            bool canRun = uninstall ? plan.CanRunUninstall : plan.CanRunInstall;
            if (!plan.IsVisible || !canRun)
            {
                string status = YoloEnvironmentCommandPresentationService.BuildUltralyticsUnavailableStatus(operationName, plan);
                view.SetCommandStatus(status, false);
                view.SetRuntimeActionStatus(status);
                view.AppendLog(YoloEnvironmentCommandPresentationService.BuildUltralyticsSkippedLog(operationName, status));
                return;
            }

            if (!view.ConfirmPackage(uninstall, plan))
            {
                string canceledText = YoloEnvironmentCommandPresentationService.BuildUltralyticsCanceledStatus(operationName);
                view.SetCommandStatus(canceledText, false);
                view.SetRuntimeActionStatus(canceledText);
                view.SetPackageResult(
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, "\uCDE8\uC18C"),
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, null, canceledText));
                view.AppendLog(canceledText);
                return;
            }

            if (!Begin(view, YoloEnvironmentCommandPresentationService.BuildUltralyticsRunningStatus(operationName)))
            {
                return;
            }

            try
            {
                view.AppendLog(YoloEnvironmentCommandPresentationService.BuildUltralyticsStartLog(operationName, plan));
                PythonPackageInstallResult result = await runPackageAsync(settings, uninstall).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }

                foreach (string line in YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationLogLines(operationName, result))
                {
                    view.AppendLog(line);
                }
                view.LoadSettings(settings);
                view.RefreshStatus();

                string statusText = YoloEnvironmentCommandPresentationService.BuildUltralyticsResultStatus(uninstall, operationName, result);
                view.SetCommandStatus(statusText, false);
                view.SetRuntimeActionStatus(statusText);
                view.SetPackageResult(
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, result.Succeeded ? "\uC131\uACF5" : "\uC2E4\uD328"),
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, result, statusText));
                view.AppendLog(statusText);
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    string statusText = YoloEnvironmentCommandPresentationService.BuildUltralyticsFailureStatus(operationName, ex.Message);
                    view.SetCommandStatus(statusText, false);
                    view.SetRuntimeActionStatus(statusText);
                    view.SetPackageResult(
                        YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, "\uC2E4\uD328"),
                        YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, null, statusText));
                    view.AppendLog(statusText);
                }
            }
            finally
            {
                End(view);
            }
        }

        public async Task RunModelTestAsync(YoloEnvironmentCallbacks view)
        {
            if (!Begin(view, YoloEnvironmentCommandPresentationService.BuildModelTestStartingStatus()))
            {
                return;
            }

            try
            {
                await view.RunDiagnosticAsync().ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }
                await view.RefreshSettingsAsync(null).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }
                view.SetCommandStatus(YoloEnvironmentCommandPresentationService.BuildModelTestCompletedStatus(), false);
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    string errorText = YoloEnvironmentCommandPresentationService.BuildModelTestFailureStatus(ex.Message);
                    YoloEnvironmentRecoveryPresentation recovery = YoloEnvironmentCommandPresentationService.BuildModelTestFailureRecovery(errorText);
                    view.SetCommandStatus(errorText, false);
                    view.SetRecovery(recovery);
                    view.AppendLog(errorText);
                }
            }
            finally
            {
                End(view);
            }
        }

        public async Task RestartWorkerAsync(YoloEnvironmentCallbacks view)
        {
            if (!Begin(view, YoloEnvironmentCommandPresentationService.BuildWorkerRestartStartingStatus()))
            {
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            workerCancellation = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            try
            {
                PythonModelRuntimeState runtimeState = runtimeStateAccessor();
                if (!runtimeState.IsRuntimeInstalled)
                {
                    view.ShowRuntimeUnavailable(runtimeState.NextActionText, runtimeState);
                    return;
                }

                bool connected = await restartWorkerAsync(
                        YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                            settingsAccessor()?.DetectionTimeoutSeconds ?? 30),
                        cancellationToken)
                    .ConfigureAwait(true);
                if (disposed || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (connected)
                {
                    string requestId = YoloRuntimePresentationService.CreateRequestId();
                    queryWorkerStatus(requestId);
                }

                await view.RefreshSettingsAsync(null).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }
                view.ApplyWorkerPresentation(
                    YoloEnvironmentCommandPresentationService.BuildWorkerRestartResult(
                        connected,
                        workerFailureAccessor()));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    view.ApplyWorkerPresentation(
                        YoloEnvironmentCommandPresentationService.BuildWorkerRestartFailure(ex.Message));
                }
            }
            finally
            {
                if (ReferenceEquals(workerCancellation, cancellation))
                {
                    workerCancellation = null;
                }

                cancellation.Dispose();
                End(view);
            }
        }

        public async Task StopWorkerAsync(YoloEnvironmentCallbacks view)
        {
            if (!Begin(view, YoloEnvironmentCommandPresentationService.BuildWorkerStopStartingStatus()))
            {
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            workerCancellation = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            try
            {
                await stopWorkerAsync(cancellationToken).ConfigureAwait(true);
                if (disposed || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await view.RefreshSettingsAsync(null).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }
                view.ApplyWorkerPresentation(
                    YoloEnvironmentCommandPresentationService.BuildWorkerStopCompleted());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    view.ApplyWorkerPresentation(
                        YoloEnvironmentCommandPresentationService.BuildWorkerStopFailure(ex.Message));
                }
            }
            finally
            {
                if (ReferenceEquals(workerCancellation, cancellation))
                {
                    workerCancellation = null;
                }

                cancellation.Dispose();
                End(view);
            }
        }

        private bool Begin(YoloEnvironmentCallbacks view, string statusText)
        {
            if (disposed || IsRunning || view.HasConflictingCommand())
            {
                if (!disposed) view.AppendLog(YoloEnvironmentCommandPresentationService.BuildBusyCommandLog());
                return false;
            }

            IsRunning = true;
            view.ClearRecovery();
            view.SetCommandStatus(statusText, true);
            view.RefreshCommands();
            return true;
        }

        private void End(YoloEnvironmentCallbacks view)
        {
            IsRunning = false;
            if (disposed) return;
            view.SetCommandBusy(false);
            view.RefreshCommands();
            view.RefreshStatus();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            workerCancellation?.Cancel();
            workerCancellation?.Dispose();
            workerCancellation = null;
            IsRunning = false;
        }
    }

    // Visual and composition ports keep Window/control references out of the workflow.
    public sealed class YoloEnvironmentCallbacks
    {
        public Func<bool> HasConflictingCommand { get; init; }
        public Action ClearRecovery { get; init; }
        public Action<string, bool> SetCommandStatus { get; init; }
        public Action<bool> SetCommandBusy { get; init; }
        public Action RefreshCommands { get; init; }
        public Action RefreshStatus { get; init; }
        public Func<PythonModelValidationResult, Task> RefreshSettingsAsync { get; init; }
        public Action ShowModelCenter { get; init; }
        public Action<string, PythonModelRuntimeState> ShowRuntimeUnavailable { get; init; }
        public Action<string> AppendLog { get; init; }
        public Func<bool, PythonModelRuntimeInstallPlan, bool> ConfirmPackage { get; init; }
        public Action<string> SetRuntimeActionStatus { get; init; }
        public Action<string, string> SetPackageResult { get; init; }
        public Action<PythonModelSettings> LoadSettings { get; init; }
        public Func<Task> RunDiagnosticAsync { get; init; }
        public Action<YoloEnvironmentRecoveryPresentation> SetRecovery { get; init; }
        public Action<YoloWorkerCommandPresentation> ApplyWorkerPresentation { get; init; }
    }
}
