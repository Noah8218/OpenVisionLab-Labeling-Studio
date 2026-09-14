using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Windows.Media;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell-facing training status and readiness orchestration.
    /// Worker lifetime remains in TrainingRuntimeWorkflowService; WPF controls
    /// are updated through explicit callbacks supplied by the generated Window.
    /// </summary>
    internal sealed class TrainingRuntimeAdapter
    {
        private readonly TrainingRuntimeAdapterContext context;

        internal TrainingRuntimeAdapter(TrainingRuntimeAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.TrainingRuntimeWorkflowService);
            ArgumentNullException.ThrowIfNull(context.TrainingReadinessWorkflowService);
            ArgumentNullException.ThrowIfNull(context.TrainingSettingsViewModelProvider);
        }

        internal void UpdateTrainingProgressFromWorker()
            => UpdateTrainingProgressFromWorker(context.TrainingRuntimeWorkflowService.PollStatus());

        internal void UpdateTrainingProgressFromWorker(TrainingRuntimeStatusSnapshot snapshot)
        {
            snapshot ??= context.TrainingRuntimeWorkflowService.PollStatus();
            PythonCommunicationStatus status = snapshot.Status;
            bool hasCurrentStatus = snapshot.HasCurrentStatus;
            bool isLiveTraining = snapshot.IsLiveTraining;
            WpfTrainingSettingsPanelViewModel settingsViewModel = context.TrainingSettingsViewModelProvider();

            if (hasCurrentStatus && status.LastTrainingProgressPercent.HasValue)
            {
                SetTrainingProgressValue(Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100));
            }
            else if (settingsViewModel?.IsTrainingCommandRunning != true
                && !context.TrainingRuntimeWorkflowService.IsTrainingWorkflowRunning)
            {
                SetTrainingProgressValue(0);
            }

            if (hasCurrentStatus)
            {
                SetTrainingProgressStatus(
                    TrainingProgressPresentationService.BuildProgressSummary(status),
                    TrainingProgressPresentationService.BuildEpochSummary(status, isLiveTraining),
                    context.CurrentProgressValueProvider?.Invoke() ?? 0D,
                    isLiveTraining && !status.LastTrainingProgressPercent.HasValue);
                context.UpdateTrainingGuideTrainingHistory?.Invoke(status);
                if (TrainingWeightsService.IsCompletedTrainingState(status.LastTrainingState))
                {
                    context.TryApplyLatestTrainingWeightsFromProject?.Invoke(false);
                }
            }
            else if (context.TrainingRuntimeWorkflowService.IsTrainingWorkflowRunning)
            {
                SetTrainingProgressStatus(
                    TrainingProgressPresentationService.BuildAcceptedWorkerWaitProgressText(),
                    TrainingProgressPresentationService.BuildBeforeEpochText(),
                    context.CurrentProgressValueProvider?.Invoke() ?? 0D,
                    true);
            }
            else if (settingsViewModel?.IsTrainingCommandRunning != true)
            {
                SetTrainingProgressStatus(
                    TrainingProgressPresentationService.BuildIdleProgressText(),
                    string.Empty,
                    0D,
                    false);
            }

            UpdateTrainingStatusVisual(status, context.LastTrainingReadinessReportProvider?.Invoke());
            UpdateYoloTrainingRecoveryStatus(status);
            context.RefreshYoloTrainingStepCompletion?.Invoke();
            context.UpdateCommandState?.Invoke();
            if (snapshot.IsTerminal)
            {
                StopTrainingStatusPolling();
            }
        }

        internal void StartTrainingStatusPolling()
        {
            context.TrainingRuntimeWorkflowService.BeginStatusPolling();
            context.StartTrainingStatusTimer?.Invoke();
        }

        internal void StopTrainingStatusPolling()
        {
            context.TrainingRuntimeWorkflowService.StopStatusPolling();
            context.StopTrainingStatusTimer?.Invoke();
        }

        internal void HandleTrainingStatusPollTimerTick()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                StopTrainingStatusPolling();
                return;
            }

            TrainingRuntimeStatusSnapshot snapshot = context.TrainingRuntimeWorkflowService.PollStatus();
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
                SetTrainingProgressStatus(timeoutText, string.Empty, 0D, false);
                context.SetRecoveryStatus?.Invoke(recovery.Title, recovery.Detail, recovery.Action);
                StopTrainingStatusPolling();
            }
        }

        internal void SetTrainingReadinessStatus(string text)
            => TrainingSettingsViewModel?.SetTrainingReadinessText(text ?? string.Empty);

        internal void SetTrainingProgressStatus(string progressText, string epochText, double progressValue, bool isIndeterminate)
        {
            string normalizedProgress = progressText ?? string.Empty;
            string normalizedEpoch = epochText ?? string.Empty;
            context.SetModelCenterTrainingState?.Invoke(
                normalizedProgress,
                string.IsNullOrWhiteSpace(normalizedEpoch)
                    ? TrainingSettingsViewModel?.TrainingReadinessText ?? string.Empty
                    : normalizedEpoch);
            TrainingSettingsViewModel?.SetTrainingProgress(
                normalizedProgress,
                normalizedEpoch,
                progressValue,
                isIndeterminate);
        }

        internal void SetTrainingProgressValue(double value)
            => TrainingSettingsViewModel?.SetTrainingProgressValue(value);

        internal void SetTrainingStatusBrushes(Brush readinessBrush, Brush progressBrush)
            => TrainingSettingsViewModel?.SetTrainingStatusBrushes(readinessBrush, progressBrush);

        internal void RefreshTrainingReadinessPanel(bool refreshYaml)
        {
            if (context.IsModelWorkflowCreated?.Invoke() != true)
            {
                return;
            }

            TrainingReadinessWorkflowResult result = context.TrainingReadinessWorkflowService.Refresh(
                new TrainingReadinessWorkflowRequest
                {
                    Data = context.DataProvider(),
                    RefreshYaml = refreshYaml
                });
            if (result.UsesExternalDataset)
            {
                RefreshExternalTrainingReadinessPanel(result.ExternalReport);
                return;
            }

            YoloDatasetReadinessReport report = result.DatasetReport;
            LabelingProjectData data = context.DataProvider();
            string readinessText = TrainingReadinessPresentationService.BuildStatusText(data, report);
            SetTrainingReadinessStatus(readinessText);
            context.UpdateTrainingChecklist?.Invoke(report, refreshYaml);
            UpdateTrainingProgressFromWorker();
        }

        internal void RefreshExternalTrainingReadinessPanel(YoloExternalDatasetIntakeReport externalReport)
        {
            context.RefreshExternalYoloDatasetIntakePresentation?.Invoke();
            ExternalYoloDatasetSettings settings = context.DataProvider()?.ProjectSettings?.ExternalYoloDataset;
            (string readinessText, string checklistStatusText, string checklistDetailText, string checklistActionText) =
                TrainingReadinessPresentationService.BuildExternalTrainingReadinessPresentation(settings, externalReport);
            SetTrainingReadinessStatus(readinessText);
            context.SetTrainingChecklistText?.Invoke(checklistStatusText, checklistDetailText, checklistActionText);
            UpdateTrainingProgressFromWorker();
        }

        internal TrainingRuntimeCallbacks CreateTrainingRuntimeCallbacks()
        {
            return new TrainingRuntimeCallbacks
            {
                IsApplicationCloseApproved = () => context.IsApplicationCloseApproved?.Invoke() == true,
                HasConflictingCommand = () => WorkflowCommandStateService.HasActiveCommand(
                    context.IsYoloEnvironmentRunning?.Invoke() == true,
                    context.IsImageDetectionRunning?.Invoke() == true,
                    context.IsBatchDetectionRunning?.Invoke() == true,
                    context.IsTrainingCommandRunning?.Invoke() == true),
                EnsureModelRuntime = context.EnsureModelRuntimeForTraining,
                PrepareTraining = PrepareTrainingForCommand,
                CreateStartRequest = CreateTrainingStartRequest,
                RefreshReadiness = PrepareTrainingForCommand,
                PersistExternalDatasetSettings = PersistExternalDatasetSettingsForTraining,
                StartStatusPolling = StartTrainingStatusPolling,
                SetModelCenterTrainingState = context.SetModelCenterTrainingState,
                RefreshTrainingStatus = UpdateTrainingProgressFromWorker,
                UpdateCommandState = context.UpdateCommandState,
                RefreshYoloStatus = context.RefreshYoloStatus,
                AppendLog = context.AppendLog,
                SetRecoveryStatus = context.SetRecoveryStatus,
                ClearRecoveryStatus = context.ClearRecoveryStatus,
                ResolveInfoBrush = () => context.ResolveBrushResource?.Invoke("InfoBrush", Brushes.DodgerBlue) ?? Brushes.DodgerBlue
            };
        }

        private void PrepareTrainingForCommand()
        {
            context.SaveTrainingEditorFields?.Invoke();
            RefreshTrainingReadinessPanel(refreshYaml: true);
        }

        private void PersistExternalDatasetSettingsForTraining()
        {
            if (context.DataProvider()?.ProjectSettings?.ExternalYoloDataset?.HasSelection == true)
            {
                context.TrySaveExternalYoloDatasetSettings?.Invoke();
            }
        }

        private TrainingRuntimeStartRequest CreateTrainingStartRequest()
        {
            return new TrainingRuntimeStartRequest
            {
                WorkerReadyTimeoutMilliseconds = YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                    context.DataProvider()?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30),
                RecipeName = context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty
            };
        }

        internal void UpdateTrainingStatusVisual(PythonCommunicationStatus status, YoloDatasetReadinessReport report)
        {
            string readinessKey = report == null
                ? "SecondaryTextBrush"
                : report.IsReady
                    ? "SuccessBrush"
                    : "WarningBrush";
            string stateKey = TrainingProgressPresentationService.GetTrainingStateBrushResourceKey(status);
            Brush readinessBrush = ResolveBrushResource(readinessKey, Brushes.Gray);
            Brush stateBrush = ResolveBrushResource(
                stateKey,
                stateKey switch
                {
                    "SuccessBrush" => Brushes.LimeGreen,
                    "ErrorBrush" => Brushes.IndianRed,
                    "WarningBrush" => Brushes.DarkOrange,
                    "InfoBrush" => Brushes.DodgerBlue,
                    _ => Brushes.Gray
                });
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
                context.SetRecoveryStatus?.Invoke(recovery.Title, recovery.Detail, recovery.Action);
                return;
            }

            if (TrainingProgressPresentationService.ShouldClearRecoveryStatus(status))
            {
                context.ClearRecoveryStatus?.Invoke();
            }
        }

        private Brush ResolveBrushResource(string key, Brush fallback)
            => context.ResolveBrushResource?.Invoke(key, fallback) ?? fallback;

        private WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel
            => context.TrainingSettingsViewModelProvider?.Invoke();
    }

    internal sealed class TrainingRuntimeAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal TrainingRuntimeWorkflowService TrainingRuntimeWorkflowService { get; init; }
        internal TrainingReadinessWorkflowService TrainingReadinessWorkflowService { get; init; }
        internal Func<WpfTrainingSettingsPanelViewModel> TrainingSettingsViewModelProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsModelWorkflowCreated { get; init; }
        internal Func<bool> IsYoloEnvironmentRunning { get; init; }
        internal Func<bool> IsImageDetectionRunning { get; init; }
        internal Func<bool> IsBatchDetectionRunning { get; init; }
        internal Func<bool> IsTrainingCommandRunning { get; init; }
        internal Func<double> CurrentProgressValueProvider { get; init; }
        internal Func<YoloDatasetReadinessReport> LastTrainingReadinessReportProvider { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Func<bool> EnsureModelRuntimeForTraining { get; init; }
        internal Action SaveTrainingEditorFields { get; init; }
        internal Action TrySaveExternalYoloDatasetSettings { get; init; }
        internal Action StartTrainingStatusTimer { get; init; }
        internal Action StopTrainingStatusTimer { get; init; }
        internal Action<string, string> SetModelCenterTrainingState { get; init; }
        internal Action<PythonCommunicationStatus> UpdateTrainingGuideTrainingHistory { get; init; }
        internal Action<bool> TryApplyLatestTrainingWeightsFromProject { get; init; }
        internal Action<YoloDatasetReadinessReport, bool> UpdateTrainingChecklist { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal Action RefreshExternalYoloDatasetIntakePresentation { get; init; }
        internal Action<string, string, string> SetTrainingChecklistText { get; init; }
        internal Action<string, string, string> SetRecoveryStatus { get; init; }
        internal Action ClearRecoveryStatus { get; init; }
        internal Action UpdateCommandState { get; init; }
        internal Action RefreshYoloStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<string, Brush, Brush> ResolveBrushResource { get; init; }
    }
}
