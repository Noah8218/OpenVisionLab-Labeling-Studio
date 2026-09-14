using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell's YOLO runtime state projection and the WPF status strip.
    /// Runtime execution remains in the existing environment/training services;
    /// this adapter only joins their results to the existing ViewModels and controls.
    /// </summary>
    internal sealed class YoloRuntimeStatusAdapter : IDisposable
    {
        private readonly YoloRuntimeStatusAdapterContext context;
        private readonly Stopwatch inferenceStatusPulseStopwatch = new Stopwatch();
        private readonly YoloSettingsPanelRefreshCoordinator settingsRefreshCoordinator;
        private bool disposed;

        #region YoloRuntimeStatus
        internal YoloRuntimeStatusAdapter(YoloRuntimeStatusAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.CommunicationStatusProvider);
            settingsRefreshCoordinator = CreateYoloSettingsPanelRefreshCoordinator();
        }

        internal void NotifyYoloPathSelected(string label, string selectedPath)
        {
            context.SetYoloCommandStatus?.Invoke($"{label} 선택됨. 저장을 눌러 설정에 반영하세요.", false);
            context.AppendLog?.Invoke($"{label} 선택: {selectedPath}");
        }

        internal void RefreshYoloStatus()
        {
            PythonModelSettings settings = GetPythonModelSettings();
            context.RefreshModelCenterDashboard?.Invoke();
            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            if (runtimeState.State == PythonModelRuntimeStateKind.NotInstalled)
            {
                ModelRuntimeUnavailablePresentation presentation =
                    ModelRuntimeUnavailablePresentationService.Build(runtimeState);
                context.SetGlobalInferenceStatus?.Invoke(string.Empty, false, true);
                context.SetPythonStatus?.Invoke(runtimeState.SummaryText);
                context.SetInspectionModelStatus?.Invoke(presentation.InspectionStatusText, presentation.InspectionStatusToolTip);
                context.SetModelStatus?.Invoke(presentation.ModelStatusText);
                context.SetYoloCommandStatus?.Invoke(presentation.CommandStatusText, false);
                ApplyModelRuntimeUnavailablePresentation(presentation);
                return;
            }

            PythonModelValidationResult result = PythonModelSettingsValidator.Validate(settings, requireWeights: true);
            context.SetGlobalInferenceStatus?.Invoke(string.Empty, false, !result.IsValid);
            context.SetPythonStatus?.Invoke(InferenceStatusPresentationService.BuildRuntimePythonStatus(result, runtimeState));

            string weightsPath = settings.WeightsPath;
            if (!File.Exists(weightsPath))
            {
                string inspectionModelStatus = InferenceStatusPresentationService.BuildInspectionModelStatusText(
                    settings,
                    context.HasPendingTrainingWeightsRecipeSaveProvider?.Invoke() == true);
                context.SetInspectionModelStatus?.Invoke(
                    inspectionModelStatus,
                    InferenceStatusPresentationService.BuildInspectionModelToolTip(
                        settings,
                        context.HasPendingTrainingWeightsRecipeSaveProvider?.Invoke() == true));
                context.SetModelStatus?.Invoke(inspectionModelStatus);
                return;
            }

            bool hasPendingTrainingWeightsRecipeSave = context.HasPendingTrainingWeightsRecipeSaveProvider?.Invoke() == true;
            context.SetInspectionModelStatus?.Invoke(
                InferenceStatusPresentationService.BuildInspectionModelStatusText(settings, hasPendingTrainingWeightsRecipeSave),
                InferenceStatusPresentationService.BuildInspectionModelToolTip(settings, hasPendingTrainingWeightsRecipeSave));
            context.SetModelStatus?.Invoke(
                InferenceStatusPresentationService.BuildInspectionModelStatusText(settings, hasPendingTrainingWeightsRecipeSave));
        }

        internal PythonModelRuntimeState GetPythonModelRuntimeState()
        {
            PythonModelSettings settings = GetPythonModelSettings();
            PythonCommunicationStatus status = context.CommunicationStatusProvider();
            return PythonModelSettingsValidator.GetRuntimeState(
                settings,
                status?.WorkerSupportedModels,
                status?.WorkerTrainingModels,
                status?.WorkerDetectionModels);
        }

        internal bool EnsureModelRuntimeForTraining()
        {
            if (TryAutoConnectAnomalyTrainingRuntime())
            {
                return true;
            }

            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            if (runtimeState.CanRunTraining)
            {
                return true;
            }

            ShowModelRuntimeUnavailable(
                "학습 시작 대기: 모델 실행기 설치 또는 경로 연결 필요",
                runtimeState);
            return false;
        }

        internal bool EnsureModelRuntimeForInference()
        {
            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            if (runtimeState.CanRunInference)
            {
                return true;
            }

            ShowModelRuntimeUnavailable(
                "현재 검사 대기: 검사 모델 파일 필요",
                runtimeState);
            return false;
        }

        internal void ShowModelRuntimeUnavailable(string statusText, PythonModelRuntimeState runtimeState)
        {
            runtimeState ??= GetPythonModelRuntimeState();
            ModelRuntimeUnavailablePresentation presentation =
                ModelRuntimeUnavailablePresentationService.Build(runtimeState, statusText);

            context.SetYoloCommandStatus?.Invoke(presentation.CommandStatusText, false);
            context.SetYoloRecoveryStatus?.Invoke(presentation.RecoveryTitle, presentation.RecoveryDetail, presentation.RecoveryAction);
            SetTrainingReadinessStatus(presentation.ReadinessText);
            context.SetPythonStatus?.Invoke(runtimeState.SummaryText);
            ApplyModelRuntimeUnavailablePresentation(presentation);
            context.AppendLog?.Invoke(presentation.LogText);
        }

        internal void ApplyModelRuntimeUnavailablePresentation(PythonModelRuntimeState runtimeState, string statusText = null)
        {
            runtimeState ??= GetPythonModelRuntimeState();
            ModelRuntimeUnavailablePresentation presentation =
                ModelRuntimeUnavailablePresentationService.Build(runtimeState, statusText);
            ApplyModelRuntimeUnavailablePresentation(presentation);
        }

        internal Task RefreshSettingsAsync(PythonModelValidationResult validation = null)
            => settingsRefreshCoordinator.RefreshAsync(validation);

        internal void SetGlobalInferenceStatus(string text, bool isBusy, bool isWarning = false)
        {
            TextBlock statusTextControl = context.InferenceStatusTextProvider?.Invoke();
            Border statusBorder = context.InferenceStatusBorderProvider?.Invoke();
            if (statusTextControl == null || statusBorder == null)
            {
                return;
            }

            bool hasPendingTrainingWeightsRecipeSave = context.HasPendingTrainingWeightsRecipeSaveProvider?.Invoke() == true;
            string statusText = string.IsNullOrWhiteSpace(text) ? "대기" : text;
            PythonModelSettings settings = context.DataProvider()?.ProjectSettings?.PythonModel;
            statusTextControl.Text = InferenceStatusPresentationService.BuildStatusText(
                statusText,
                settings,
                hasPendingTrainingWeightsRecipeSave);
            statusBorder.ToolTip = InferenceStatusPresentationService.BuildToolTip(
                statusText,
                settings,
                hasPendingTrainingWeightsRecipeSave);

            ProgressBar progressBar = context.InferenceStatusProgressBarProvider?.Invoke();
            if (progressBar != null)
            {
                progressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
                progressBar.IsIndeterminate = false;
            }

            if (isBusy)
            {
                StartInferenceStatusPulse();
            }
            else
            {
                StopInferenceStatusPulse();
            }

            PackIconMaterial icon = context.InferenceStatusIconProvider?.Invoke();
            if (icon != null)
            {
                icon.Kind = isBusy
                    ? PackIconMaterialKind.ProgressClock
                    : isWarning
                        ? PackIconMaterialKind.AlertCircleOutline
                        : PackIconMaterialKind.RobotIndustrial;
            }

            statusBorder.SetResourceReference(
                Border.BackgroundProperty,
                isBusy ? "DetectionOverlaySelectedBackgroundBrush" : "ToolbarButtonBrush");
            statusBorder.SetResourceReference(
                Border.BorderBrushProperty,
                isBusy || isWarning ? "AccentBrush" : "BorderBrushDark");
        }

        internal void StartInferenceStatusPulse()
        {
            DispatcherTimer timer = GetInferenceStatusPulseTimer();
            ProgressBar progressBar = context.InferenceStatusProgressBarProvider?.Invoke();
            if (timer == null || progressBar == null)
            {
                return;
            }

            if (!timer.IsEnabled)
            {
                inferenceStatusPulseStopwatch.Restart();
                progressBar.Value = 8D;
                timer.Start();
            }
        }

        internal void StopInferenceStatusPulse()
        {
            GetInferenceStatusPulseTimer()?.Stop();
            inferenceStatusPulseStopwatch.Reset();
            ProgressBar progressBar = context.InferenceStatusProgressBarProvider?.Invoke();
            if (progressBar != null)
            {
                progressBar.Value = 0D;
            }
        }

        internal void HandleInferenceStatusPulseTimerTick(object sender, EventArgs e)
            => InferenceStatusPulseTimer_Tick(sender, e);

        private void InferenceStatusPulseTimer_Tick(object sender, EventArgs e)
        {
            bool isApplicationCloseApproved = context.IsApplicationCloseApproved?.Invoke() == true;
            if (isApplicationCloseApproved)
            {
                StopInferenceStatusPulse();
                return;
            }

            ProgressBar progressBar = context.InferenceStatusProgressBarProvider?.Invoke();
            if (progressBar == null || progressBar.Visibility != Visibility.Visible)
            {
                StopInferenceStatusPulse();
                return;
            }

            const double cycleMilliseconds = 1400D;
            double elapsed = inferenceStatusPulseStopwatch.Elapsed.TotalMilliseconds;
            double phase = (elapsed % cycleMilliseconds) / cycleMilliseconds;
            progressBar.Value = 8D + (phase * 84D);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopInferenceStatusPulse();
            settingsRefreshCoordinator.Dispose();
        }

        private PythonModelSettings GetPythonModelSettings()
        {
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider();
            data.ProjectSettings.PythonModel ??= new PythonModelSettings();
            return data.ProjectSettings.PythonModel;
        }

        private bool TryAutoConnectAnomalyTrainingRuntime()
        {
            LabelingProjectData data = context.DataProvider();
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                return false;
            }

            PythonModelSettings settings = GetPythonModelSettings();
            if (!PythonModelRuntimeConnectionService.TryBuildAnomalyTrainingConnection(
                    settings,
                    out PythonModelRuntimeConnectionResult result))
            {
                return false;
            }

            data.ProjectSettings.PythonModel = result.Settings;
            context.YoloModelSettingsViewModelProvider?.Invoke()?.LoadFrom(
                result.Settings,
                data.ProjectSettings.AnomalyClassification);
            context.TrainingSettingsViewModelProvider?.Invoke()?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            context.SaveProjectConfigFromPanel?.Invoke();
            context.SetYoloCommandStatus?.Invoke(
                "이상탐지 분류 학습에 맞는 YOLOv8 실행기를 자동 연결했습니다.",
                false);
            context.AppendLog?.Invoke($"이상탐지 학습 실행기 자동 연결: {result.Settings.ProjectRootPath}");
            return true;
        }

        private void ApplyModelRuntimeUnavailablePresentation(ModelRuntimeUnavailablePresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            ModelCenterDashboardState state = ModelRuntimeUnavailablePresentationService.BuildDashboardState(presentation);
            SetTrainingReadinessStatus(presentation.ReadinessText);
            SetTrainingProgressStatus(
                TrainingProgressPresentationService.BuildIdleProgressText(),
                string.Empty,
                0D,
                false);
            context.LearningWorkflowViewModelProvider?.Invoke()?.SetTrainingModelLifecycleState(
                presentation.CurrentModelText,
                presentation.CandidateModelText,
                presentation.AdoptionText,
                presentation.NextActionText);
            context.ShellViewModelProvider?.Invoke()?.ApplyModelCenterModelState(state);
            context.ShellViewModelProvider?.Invoke()?.SetModelCenterCandidateReviewState(
                state.ReviewCandidateButtonText,
                state.ReviewCandidateButtonToolTip,
                state.CanReviewCandidate);
            context.ShellViewModelProvider?.Invoke()?.SetModelRegistryState(state.RegistryPresentation);
            context.ShellViewModelProvider?.Invoke()?.SetModelCenterRecoveryState(
                presentation.RecoveryTitle,
                presentation.RecoveryDetail,
                presentation.NextActionText);
            context.TrainingSettingsViewModelProvider?.Invoke()?.SetPostTrainingModelActionState(
                presentation.CurrentModelText,
                presentation.CandidateModelText,
                presentation.AdoptionText,
                presentation.NextActionText,
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                false,
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                false);
        }

        private void SetTrainingReadinessStatus(string text)
            => context.TrainingSettingsViewModelProvider?.Invoke()?.SetTrainingReadinessText(text ?? string.Empty);

        private void SetTrainingProgressStatus(string progressText, string epochText, double progressValue, bool isIndeterminate)
        {
            string normalizedProgress = progressText ?? string.Empty;
            string normalizedEpoch = epochText ?? string.Empty;
            context.ShellViewModelProvider?.Invoke()?.SetModelCenterTrainingState(
                normalizedProgress,
                string.IsNullOrWhiteSpace(normalizedEpoch)
                    ? context.TrainingSettingsViewModelProvider?.Invoke()?.TrainingReadinessText ?? string.Empty
                    : normalizedEpoch);
            context.TrainingSettingsViewModelProvider?.Invoke()?.SetTrainingProgress(
                normalizedProgress,
                normalizedEpoch,
                progressValue,
                isIndeterminate);
        }

        private void ApplyYoloSettingsPanelRefresh(YoloSettingsPanelRefreshResult result)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true || result == null)
            {
                return;
            }

            string detail = YoloSettingsPanelStatusPresentationService.BuildDetail(
                result.Settings,
                result.Validation,
                result.RuntimeState,
                result.CommunicationStatus,
                result.PythonClientProcessRunning,
                result.Environment,
                result.EnvironmentCheckError);
            context.YoloStatusViewModelProvider?.Invoke()?.SetSettingsStatus(result.RuntimeState.SummaryText, detail);
        }

        private DispatcherTimer GetInferenceStatusPulseTimer()
            => context.ShellTimersProvider?.Invoke()?.InferenceStatusPulse;

        private YoloSettingsPanelRefreshCoordinator CreateYoloSettingsPanelRefreshCoordinator()
        {
            return new YoloSettingsPanelRefreshCoordinator(
                GetPythonModelSettings,
                context.CommunicationStatusProvider,
                context.PythonClientProcessRunningProvider ?? (() => false),
                communicationStatus => context.ApplyRuntimeCapabilities?.Invoke(communicationStatus),
                ApplyYoloSettingsPanelRefresh);
        }
        #endregion
    }

    internal sealed class YoloRuntimeStatusAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<PythonCommunicationStatus> CommunicationStatusProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> PythonClientProcessRunningProvider { get; init; }
        internal Func<bool> HasPendingTrainingWeightsRecipeSaveProvider { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action RefreshModelCenterDashboard { get; init; }
        internal Action<PythonCommunicationStatus> ApplyRuntimeCapabilities { get; init; }
        internal Action SaveProjectConfigFromPanel { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string, string> SetInspectionModelStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string, string, string> SetYoloRecoveryStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<WpfYoloModelSettingsPanelViewModel> YoloModelSettingsViewModelProvider { get; init; }
        internal Func<WpfTrainingSettingsPanelViewModel> TrainingSettingsViewModelProvider { get; init; }
        internal Func<WpfLearningWorkflowPanelViewModel> LearningWorkflowViewModelProvider { get; init; }
        internal Func<WpfLabelingShellViewModel> ShellViewModelProvider { get; init; }
        internal Func<WpfYoloStatusPanelViewModel> YoloStatusViewModelProvider { get; init; }
        internal Func<ShellTimerSet> ShellTimersProvider { get; init; }
        internal Func<TextBlock> InferenceStatusTextProvider { get; init; }
        internal Func<Border> InferenceStatusBorderProvider { get; init; }
        internal Func<ProgressBar> InferenceStatusProgressBarProvider { get; init; }
        internal Func<PackIconMaterial> InferenceStatusIconProvider { get; init; }
    }

}
