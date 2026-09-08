using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
using ProgressBar = System.Windows.Controls.ProgressBar;
using MvcVisionSystem._1._Core;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: training runtime status and commands.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region TrainingProgressStatus
        // Worker progress polling is isolated from readiness binding so live YOLO state updates can be changed without touching setup UI.
        private void UpdateTrainingProgressFromWorker()
            => UpdateTrainingProgressFromWorker(trainingRuntimeWorkflowService.PollStatus());

        private void UpdateTrainingProgressFromWorker(TrainingRuntimeStatusSnapshot snapshot)
        {
            snapshot ??= trainingRuntimeWorkflowService.PollStatus();
            PythonCommunicationStatus status = snapshot.Status;
            bool hasCurrentStatus = snapshot.HasCurrentStatus;
            bool isLiveTraining = snapshot.IsLiveTraining;

            if (hasCurrentStatus && status.LastTrainingProgressPercent.HasValue)
            {
                SetTrainingProgressValue(Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100));
            }
            else if (!isTrainingCommandRunning && !trainingRuntimeWorkflowService.IsTrainingWorkflowRunning)
            {
                SetTrainingProgressValue(0);
            }

            if (hasCurrentStatus)
            {
                SetTrainingProgressStatus(
                    TrainingProgressPresentationService.BuildProgressSummary(status),
                    TrainingProgressPresentationService.BuildEpochSummary(status, isLiveTraining),
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: isLiveTraining && !status.LastTrainingProgressPercent.HasValue);
                UpdateYoloTrainingGuideTrainingHistory(status);
                if (TrainingWeightsService.IsCompletedTrainingState(status.LastTrainingState))
                {
                    TryApplyLatestTrainingWeightsFromProject(logIfUnchanged: false);
                }
            }
            else if (trainingRuntimeWorkflowService.IsTrainingWorkflowRunning)
            {
                SetTrainingProgressStatus(
                    TrainingProgressPresentationService.BuildAcceptedWorkerWaitProgressText(),
                    TrainingProgressPresentationService.BuildBeforeEpochText(),
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: true);
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressStatus(TrainingProgressPresentationService.BuildIdleProgressText(), string.Empty, 0D, isIndeterminate: false);
            }

            UpdateTrainingStatusVisual(status, lastYoloTrainingReadinessReport);
            UpdateYoloTrainingRecoveryStatus(status);
            RefreshYoloTrainingStepCompletion();
            UpdateYoloCommandButtons();
            if (snapshot.IsTerminal)
            {
                StopTrainingStatusPolling();
            }
        }

        private void StartTrainingStatusPolling()
        {
            trainingRuntimeWorkflowService.BeginStatusPolling();
            if (!shellTimers.TrainingStatusPoll.IsEnabled)
            {
                shellTimers.TrainingStatusPoll.Start();
            }
        }

        private void StopTrainingStatusPolling()
        {
            trainingRuntimeWorkflowService.StopStatusPolling();
            if (shellTimers.TrainingStatusPoll.IsEnabled)
            {
                shellTimers.TrainingStatusPoll.Stop();
            }
        }

        private void TrainingStatusPollTimer_Tick(object sender, EventArgs e)
        {
            if (isApplicationCloseApproved)
            {
                StopTrainingStatusPolling();
                return;
            }

            TrainingRuntimeStatusSnapshot snapshot = trainingRuntimeWorkflowService.PollStatus();
            UpdateTrainingProgressFromWorker(snapshot);
            if (snapshot.IsTerminal)
            {
                StopTrainingStatusPolling();
                return;
            }

            if (snapshot.TimedOut)
            {
                string timeoutText = TrainingProgressPresentationService.BuildStatusNoResponseText();
                TrainingRecoveryStatus recovery = TrainingProgressPresentationService.BuildStatusNoResponseRecovery(timeoutText);
                SetTrainingProgressStatus(timeoutText, string.Empty, 0D, isIndeterminate: false);
                SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
                StopTrainingStatusPolling();
            }
        }

        private void UpdateTrainingStatusVisual(PythonCommunicationStatus status, YoloDatasetReadinessReport report = null)
        {
            MediaBrush readinessBrush = report == null
                ? ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray)
                : report.IsReady
                    ? ResolveBrushResource("SuccessBrush", MediaBrushes.LimeGreen)
                    : ResolveBrushResource("WarningBrush", MediaBrushes.DarkOrange);
            MediaBrush stateBrush = ResolveTrainingStateBrush(status);
            SetTrainingStatusBrushes(readinessBrush, stateBrush);
        }

        private void UpdateYoloTrainingRecoveryStatus(PythonCommunicationStatus status)
        {
            if (!TrainingProgressPresentationService.HasTrainingStatus(status))
            {
                return;
            }

            TrainingRecoveryStatus recovery = TrainingProgressPresentationService.ResolveRecoveryStatus(status);
            if (recovery != null)
            {
                SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
                return;
            }

            if (TrainingProgressPresentationService.ShouldClearRecoveryStatus(status))
            {
                ClearYoloRecoveryStatus();
            }
        }

        private MediaBrush ResolveTrainingStateBrush(PythonCommunicationStatus status)
        {
            string resourceKey = TrainingProgressPresentationService.GetTrainingStateBrushResourceKey(status);
            MediaBrush fallback = resourceKey switch
            {
                "SuccessBrush" => MediaBrushes.LimeGreen,
                "ErrorBrush" => MediaBrushes.IndianRed,
                "WarningBrush" => MediaBrushes.DarkOrange,
                "InfoBrush" => MediaBrushes.DodgerBlue,
                _ => MediaBrushes.Gray
            };
            return ResolveBrushResource(resourceKey, fallback);
        }

        private MediaBrush ResolveBrushResource(string key, MediaBrush fallback)
        {
            return TryFindResource(key) as MediaBrush ?? fallback;
        }
        #endregion

        #region TrainingStatus
        private bool BeginTrainingCommand(string statusText)
        {
            if (isApplicationCloseApproved)
            {
                return false;
            }

            if (WorkflowCommandStateService.HasActiveCommand(
                    yoloEnvironmentWorkflowService.IsRunning,
                    imageDetectionWorkflowService.IsDetecting,
                    batchDetectionWorkflowService.IsRunning,
                    isTrainingCommandRunning))
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
                ResolveBrushResource("InfoBrush", MediaBrushes.DodgerBlue));
            UpdateYoloCommandButtons();
            return true;
        }

        private void EndTrainingCommand()
        {
            isTrainingCommandRunning = false;
            if (isApplicationCloseApproved)
            {
                return;
            }

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
            if (!viewModels.IsModelWorkflowCreated)
            {
                return;
            }

            TrainingReadinessWorkflowResult result = trainingReadinessWorkflowService.Refresh(
                new TrainingReadinessWorkflowRequest
                {
                    Data = global.Data,
                    RefreshYaml = refreshYaml
                });
            if (result.UsesExternalDataset)
            {
                RefreshExternalTrainingReadinessPanel(result.ExternalReport);
                return;
            }

            YoloDatasetReadinessReport report = result.DatasetReport;
            string readinessText = TrainingReadinessPresentationService.BuildStatusText(global.Data, report);
            SetTrainingReadinessStatus(readinessText);
            UpdateYoloTrainingChecklist(report, recordHistory: refreshYaml);
            UpdateTrainingProgressFromWorker();
        }

        private void RefreshExternalTrainingReadinessPanel(YoloExternalDatasetIntakeReport externalReport)
        {
            RefreshExternalYoloDatasetIntakePresentation();
            ExternalYoloDatasetSettings settings = global?.Data?.ProjectSettings?.ExternalYoloDataset;
            (string readinessText, string checklistStatusText, string checklistDetailText, string checklistActionText) = TrainingReadinessPresentationService.BuildExternalTrainingReadinessPresentation(
                settings,
                externalReport);
            SetTrainingReadinessStatus(readinessText);
            if (LearningWorkflowViewModel != null)
            {
                LearningWorkflowViewModel.SetTrainingChecklistText(
                    checklistStatusText,
                    checklistDetailText,
                    checklistActionText);
            }

            UpdateTrainingProgressFromWorker();
        }
        #endregion

        #region YoloTrainingCommands
        // Training commands are separated from inference commands because their status and cancellation paths differ.
        private void ExecuteRefreshTrainingReadinessCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            SaveTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: true);
        }

        private void ExecuteStartTrainingCommand()
        {
            _ = ExecuteStartTrainingCommandAsync();
        }

        private async Task ExecuteStartTrainingCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (trainingRuntimeWorkflowService.IsTrainingStopAvailable())
            {
                string alreadyRunningText = TrainingCommandPresentationService.BuildAlreadyRunningStatus();
                SetTrainingReadinessStatus(alreadyRunningText);
                AppendLog(alreadyRunningText);
                UpdateYoloCommandButtons();
                return;
            }

            if (!EnsureModelRuntimeForTraining())
            {
                UpdateYoloCommandButtons();
                return;
            }

            if (!BeginTrainingCommand(TrainingCommandPresentationService.BuildPreparingDatasetStatus()))
            {
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            trainingCommandCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            TrainingRecoveryStatus pendingRecovery = null;
            try
            {
                SaveTrainingEditorFields();
                RefreshTrainingReadinessPanel(refreshYaml: true);
                TrainingRuntimeStartResult startResult = await trainingRuntimeWorkflowService.StartAsync(
                    new TrainingRuntimeStartRequest
                    {
                        WorkerReadyTimeoutMilliseconds = YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                            global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30),
                        RecipeName = GetCurrentRecipeName()
                    },
                    cancellationToken)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!startResult.WorkerReady)
                {
                    string readinessText = YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                        startResult.Status,
                        startResult.WorkerError);
                    SetTrainingReadinessStatus(readinessText);
                    pendingRecovery = TrainingCommandPresentationService.BuildWorkerConnectionFailureRecovery(readinessText);
                    AppendLog(readinessText);
                    return;
                }

                bool started = startResult.Started;
                if (global?.Data?.ProjectSettings?.ExternalYoloDataset?.HasSelection == true)
                {
                    TrySaveExternalYoloDatasetSettings();
                }
                string startText = TrainingCommandPresentationService.BuildStartCommandResultStatus(
                    started,
                    startResult.PreparationFailureMessage);
                SetTrainingReadinessStatus(startText);
                if (!started)
                {
                    pendingRecovery = TrainingCommandPresentationService.BuildStartFailureRecovery(startText);
                }

                AppendLog(startText);
                if (started)
                {
                    SetTrainingProgressStatus(TrainingCommandPresentationService.BuildTrainingAcceptedProgressText(), string.Empty, 0D, isIndeterminate: true);
                    StartTrainingStatusPolling();
                    UpdateYoloCommandButtons();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (isApplicationCloseApproved)
                {
                    return;
                }

                string errorText = TrainingCommandPresentationService.BuildStartExceptionStatus(ex.Message);
                SetTrainingReadinessStatus(errorText);
                pendingRecovery = TrainingCommandPresentationService.BuildStartExceptionRecovery(errorText);
                AppendLog(errorText);
            }
            finally
            {
                if (ReferenceEquals(trainingCommandCts, cancellation))
                {
                    trainingCommandCts = null;
                }

                cancellation.Dispose();
                EndTrainingCommand();
                if (!isApplicationCloseApproved && pendingRecovery != null)
                {
                    SetYoloRecoveryStatus(pendingRecovery.Title, pendingRecovery.Detail, pendingRecovery.Action);
                }
            }
        }

        private void ExecuteStopTrainingCommand()
        {
            _ = ExecuteStopTrainingCommandAsync();
        }

        private async Task ExecuteStopTrainingCommandAsync()
        {
            if (!BeginTrainingCommand(TrainingCommandPresentationService.BuildStoppingStatus()))
            {
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            trainingCommandCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            TrainingRecoveryStatus pendingRecovery = null;
            try
            {
                TrainingRuntimeStopResult stopResult = await trainingRuntimeWorkflowService
                    .StopAsync(cancellationToken)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                bool stopped = stopResult.Stopped;
                string stopText = TrainingCommandPresentationService.BuildStopCommandResultStatus(stopped);

                SetTrainingReadinessStatus(stopText);
                if (!stopped)
                {
                    pendingRecovery = TrainingCommandPresentationService.BuildStopFailureRecovery(stopText);
                }

                AppendLog(stopText);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (isApplicationCloseApproved)
                {
                    return;
                }

                string errorText = TrainingCommandPresentationService.BuildStopExceptionStatus(ex.Message);
                SetTrainingReadinessStatus(errorText);
                pendingRecovery = TrainingCommandPresentationService.BuildStopExceptionRecovery(errorText);
                AppendLog(errorText);
            }
            finally
            {
                if (ReferenceEquals(trainingCommandCts, cancellation))
                {
                    trainingCommandCts = null;
                }

                cancellation.Dispose();
                EndTrainingCommand();
                if (!isApplicationCloseApproved && pendingRecovery != null)
                {
                    SetYoloRecoveryStatus(pendingRecovery.Title, pendingRecovery.Detail, pendingRecovery.Action);
                }
            }
        }
        #endregion

    }
}
