using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // WPF adapter for workflow navigation, canvas display mode, and command state.
    // Workflow policy remains in the existing ViewModels/services; this adapter
    // only composes those owners with the shell's presentation callbacks.
    internal sealed class WorkflowNavigationAdapter
    {
        private readonly WorkflowNavigationAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal WorkflowNavigationAdapter(WorkflowNavigationAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.CommunicationStatusProvider);
        }

        #region CanvasWorkflowCommands
        internal bool IsInferenceWorkflowActive => context.ShellViewModel?.IsInferenceModeActive == true;

        internal void ExecuteFitCanvasCommand()
        {
            context.MainCanvasViewModel?.ImageViewer?.ZoomToFit();
        }

        internal void ExecuteActualSizeCanvasCommand()
        {
            context.MainCanvasViewModel?.ImageViewer?.ZoomToActualSize();
        }

        internal void ExecutePanCanvasCommand()
        {
            if (context.MainCanvasViewModel == null)
            {
                return;
            }

            context.MainCanvasViewModel.IsTeachingMode = false;
            context.MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);
            context.AppendLog?.Invoke("캔버스 이동 모드");
        }

        internal void ExecuteFocusCandidateCommand()
        {
            context.FocusSelectedCandidateInViewer?.Invoke(true);
        }

        internal void SelectRightWorkflowView(TabItem tab)
        {
            if (tab == null)
            {
                return;
            }

            if (context.ReviewTabControl != null)
            {
                context.ReviewTabControl.SelectedItem = tab;
            }

            tab.IsSelected = true;
        }

        internal void ShowSavedLabelsWorkflowView()
        {
            SetWorkflowMode(isInferenceMode: false);
            context.ShellViewModel?.ShowSavedLabelsWorkflow();
            SelectRightWorkflowView(context.ObjectsReviewTab);
        }

        internal void ShowCandidateReviewWorkflowView()
        {
            SetWorkflowMode(isInferenceMode: true);
            context.ShellViewModel?.ShowCandidateReviewWorkflow();
            SelectRightWorkflowView(context.CandidatesReviewTab);
        }

        internal void ShowGuideToolsWorkflowView(WpfShellWorkflowStage stage)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                context.LearningWorkflowViewModel?.ShowDatasetOnboarding();
            }
            else if (stage == WpfShellWorkflowStage.Labeling)
            {
                context.LearningWorkflowViewModel?.ShowLabelingTask();
            }

            context.ShellViewModel?.ShowGuideToolsWorkflow(stage);
            SelectRightWorkflowView(context.LearningReviewTab);
        }

        internal void ShowClassCatalogWorkflowView(WpfShellWorkflowStage stage)
        {
            context.ShellViewModel?.ShowClassCatalogWorkflow(stage);
            SelectRightWorkflowView(context.ClassesReviewTab);
        }

        internal void ShowYoloModelCenterWorkflowView()
        {
            context.ShellViewModel?.ShowModelCenterWorkflow();
            SelectRightWorkflowView(context.YoloSettingsReviewTab);
        }

        internal void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange)
        {
            bool changed = context.CanvasPanelViewModel?.CurrentDisplayMode != mode;
            context.CanvasPanelViewModel?.SetDisplayMode(mode);
            RefreshCanvasLayerVisibilityState();

            if (redraw)
            {
                context.RedrawReviewRois?.Invoke();
                context.UpdateDetectionResultOverlay?.Invoke();
                context.UpdateCanvasCommandButtons?.Invoke();
            }

            if (logChange && changed)
            {
                string modeText = CanvasWorkflowContextPresentationService.FormatDisplayMode(mode);
                context.SetModelStatus?.Invoke($"캔버스 보기: {modeText}");
                context.AppendLog?.Invoke($"캔버스 보기: {modeText}");
            }
        }

        internal void RefreshCanvasLayerVisibilityState()
        {
            int labelCount = context.CanvasLabelObjectCountProvider?.Invoke() ?? 0;
            int candidateCount = context.PendingCandidateCountProvider?.Invoke() ?? 0;
            context.CanvasPanelViewModel?.SetLayerVisibilityState(
                context.CanvasPanelViewModel?.CurrentDisplayMode ?? WpfCanvasDisplayMode.LabelsOnly,
                labelCount,
                candidateCount,
                context.AnnotationDirtyState?.IsDirty == true);
            context.CanvasPanelViewModel?.SetNoObjectCompletionState(
                context.ActiveImageAvailableProvider?.Invoke() == true,
                labelCount > 0,
                candidateCount > 0);
        }

        internal void ExecuteResetAiOverlayCommand()
        {
            int removedCount = context.CandidateReviewState?.ClearPendingCandidates() ?? 0;
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            context.RefreshCandidateList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.UpdateDetectionResultOverlay?.Invoke();
            context.SetPythonStatus?.Invoke("추론: AI 후보 표시 지움");
            context.AppendLog?.Invoke($"AI 후보 표시 지움: {removedCount}개");
        }

        internal void ExecuteLabelingModeCommand()
        {
            EnterLabelingMode(openGuidePanel: true);
            context.AppendLog?.Invoke("라벨링 모드로 전환했습니다. 캔버스는 라벨만 표시합니다.");
        }

        internal void EnterLabelingMode(bool openGuidePanel)
        {
            SetWorkflowMode(isInferenceMode: false);
            if (openGuidePanel)
            {
                context.FocusAnnotationToolsTab?.Invoke();
            }

            if (context.MainCanvasViewModel?.TeachingCommand?.CanExecute(null) == true
                && !context.MainCanvasViewModel.IsTeachingMode)
            {
                context.MainCanvasViewModel.TeachingCommand.Execute(null);
            }
        }

        internal void ExecuteInferenceModeCommand()
        {
            SetWorkflowMode(isInferenceMode: true);
            if (context.MainCanvasViewModel?.TeachingCommand?.CanExecute(null) == true
                && context.MainCanvasViewModel.IsTeachingMode)
            {
                context.MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            context.AppendLog?.Invoke("추론 검토 모드로 전환했습니다. 캔버스는 AI 추론 후보만 표시합니다.");
        }

        internal void SetWorkflowMode(bool isInferenceMode)
        {
            context.ShellViewModel?.SetWorkflowModeState(
                isInferenceMode,
                canSwitchMode: !context.ImageDetectionWorkflowService.IsDetecting
                    && !context.BatchDetectionWorkflowService.IsRunning);
            ApplyCanvasDisplayMode(
                isInferenceMode ? WpfCanvasDisplayMode.InferenceOnly : WpfCanvasDisplayMode.LabelsOnly,
                redraw: true,
                logChange: false);
            UpdateYoloCommandButtons();
            context.UpdateCandidateActionState?.Invoke();
            context.SetModelStatus?.Invoke(isInferenceMode
                ? "모드: AI 후보 검토"
                : "모드: 라벨링");
            context.RefreshCanvasWorkflowContext?.Invoke();
            context.UpdateWorkflowProgressStatus?.Invoke();
            context.ShellViewModel?.SetWorkflowStage(
                isInferenceMode ? WpfShellWorkflowStage.Inference : WpfShellWorkflowStage.Labeling);
        }

        internal void UpdateWorkflowModeUi()
        {
            bool canSwitchMode = !context.ImageDetectionWorkflowService.IsDetecting
                && !context.BatchDetectionWorkflowService.IsRunning;
            context.ShellViewModel?.SetWorkflowModeState(IsInferenceWorkflowActive, canSwitchMode);
        }

        internal bool EnsureInferenceModeForDetection()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return false;
            }

            if (IsInferenceWorkflowActive)
            {
                return true;
            }

            context.SetPythonStatus?.Invoke("AI 후보: 검토 모드 필요");
            context.SetGlobalInferenceStatus?.Invoke("AI 후보 검토 모드 필요", false, true);
            context.AppendLog?.Invoke("검출 건너뜀. 먼저 AI 후보 검토 모드로 전환하세요.");
            UpdateYoloCommandButtons();
            return false;
        }

        internal void ExecuteDatasetHomeCommand()
        {
            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
            context.FocusDatasetOnboardingTab?.Invoke();
            context.SetModelStatus?.Invoke("작업 단계: 데이터셋 홈");
            context.AppendLog?.Invoke("작업 단계 이동: 데이터셋 홈");
        }

        internal void ExecuteLabelingWorkbenchCommand()
        {
            EnterLabelingWorkbenchStartView();
            context.SetModelStatus?.Invoke("작업 단계: 라벨링 워크벤치");
            context.AppendLog?.Invoke("작업 단계 이동: 라벨링 워크벤치");
        }

        internal void EnterLabelingWorkbenchStartView()
        {
            context.LearningWorkflowViewModel?.ShowLabelingTask();
            EnterLabelingMode(openGuidePanel: false);
            context.ShellViewModel?.SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            context.ShellViewModel?.SetRightWorkflowDockExpanded(false);
            SelectRightWorkflowView(context.ObjectsReviewTab);
        }

        internal void ExecuteInferenceReviewCommand()
        {
            ExecuteInferenceModeCommand();
            ShowCandidateReviewWorkflowView();
            context.SetModelStatus?.Invoke("작업 단계: 추론 검토");
            context.AppendLog?.Invoke("작업 단계 이동: 추론 검토");
        }

        internal void ExecuteTrainingModelCenterCommand()
        {
            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            context.FocusYoloSettingsTab?.Invoke();
            context.SetModelStatus?.Invoke("작업 단계: 학습/모델 센터");
            context.AppendLog?.Invoke("작업 단계 이동: 학습/모델 센터");
        }

        internal void ExecuteReviewCandidateModelCommand()
        {
            ExecuteInferenceModeCommand();
            WpfTrainingWeightsComparison comparison = context.BuildCurrentTrainingWeightsComparison?.Invoke();
            if (comparison != null)
            {
                context.UpdateTrainingComparisonViewModel?.Invoke(comparison);
            }

            ShowCandidateReviewWorkflowView();
            context.SetModelStatus?.Invoke("후보 모델 검증: 학습 후보 검토 탭");
            context.SetYoloCommandStatus?.Invoke(
                "학습 후 후보 검토 탭으로 이동했습니다. 현재 이미지 검사는 현재 검사 버튼에서 실행합니다.",
                false);
            context.AppendLog?.Invoke("작업 단계 이동: 학습 후보 검토");
        }

        internal void ApplyWorkflowDatasetPurposeSelection(LabelingDatasetPurpose purpose)
        {
            context.EnsureProjectSettings?.Invoke();
            if (projectData?.ProjectSettings == null)
            {
                return;
            }

            projectData.ProjectSettings.DatasetPurpose = purpose;
            context.RefreshCanvasAnnotationToolScope?.Invoke();
            context.ApplyAnnotationToolSelection?.Invoke(context.SelectedAnnotationToolProvider?.Invoke());
            context.RefreshCanvasWorkflowContext?.Invoke();
            context.RefreshAnnotationVisibilityForDatasetPurpose?.Invoke(true);
            context.RefreshTrainingReadinessPanel?.Invoke(false);
            context.RefreshYoloTrainingStepCompletion?.Invoke();
        }

        internal void ApplyLearningModeWorkflowAction(WpfLearningModeWorkflowAction action)
        {
            switch (action)
            {
                case WpfLearningModeWorkflowAction.Inference:
                    SetWorkflowMode(isInferenceMode: true);
                    break;

                case WpfLearningModeWorkflowAction.LabelingAndFocusYoloSettings:
                    SetWorkflowMode(isInferenceMode: false);
                    context.FocusYoloSettingsTab?.Invoke();
                    break;

                default:
                    SetWorkflowMode(isInferenceMode: false);
                    context.ApplyAnnotationToolSelection?.Invoke(context.SelectedAnnotationToolProvider?.Invoke());
                    break;
            }
        }
        #endregion

        #region WorkflowCommandStateFanout
        internal void UpdateYoloCommandButtons()
        {
            PythonModelRuntimeState runtimeState = context.GetPythonModelRuntimeState?.Invoke();
            WorkflowCommandState state = WorkflowCommandStateService.Build(
                isInferenceMode: IsInferenceWorkflowActive,
                isYoloEnvironmentCommandRunning: context.YoloEnvironmentWorkflowService.IsRunning
                    || context.ModelComparisonWorkflowService.IsModelComparisonRunning
                    || context.ModelComparisonWorkflowService.IsSegmentationComparisonRunning
                    || context.AnomalyClassificationEvaluationWorkflowService.IsRunning,
                isDetecting: context.ImageDetectionWorkflowService.IsDetecting,
                isBatchDetectionRunning: context.BatchDetectionWorkflowService.IsRunning,
                isTrainingCommandRunning: context.TrainingCommandLifecycleService.IsRunning
                    || context.TrainingRuntimeWorkflowService.IsTrainingWorkflowRunning,
                isTrainingStopAvailable: TrainingProgressPresentationService.IsTrainingStopAvailable(
                    context.CommunicationStatusProvider()),
                hasCurrentRecipeName: !string.IsNullOrWhiteSpace(context.GetCurrentRecipeName?.Invoke()),
                canRunModelTraining: runtimeState?.CanRunTraining == true,
                canRunModelInference: runtimeState?.CanRunInference == true,
                modelRuntimeUnavailableHint: runtimeState?.NextActionText);

            context.ApplyWorkflowCommandState?.Invoke(
                state,
                ModelComparisonCommandStateService.Build(
                    context.ModelComparisonWorkflowService.IsModelComparisonRunning,
                    state,
                    context.LastTrainingReadinessReportProvider?.Invoke()));
            UpdateDetectionCommandHints(state);
            UpdateWorkflowModeUi();
        }

        private void UpdateDetectionCommandHints(WorkflowCommandState state)
        {
            SetControlToolTip(context.DetectButton, state?.CurrentImageDetectionToolTip);
            SetControlToolTip(context.DetectSelectedQueueButton, state?.SelectedQueueDetectionToolTip);
            SetControlToolTip(context.BatchDetectQueueButton, state?.BatchDetectionToolTip);
            SetControlToolTip(context.RetryFailedQueueButton, state?.RetryFailedToolTip);
            SetControlToolTip(context.StopBatchQueueButton, state?.StopBatchToolTip);
        }

        private static void SetControlToolTip(FrameworkElement element, string text)
        {
            if (element != null)
            {
                element.ToolTip = text;
            }
        }
        #endregion
    }

    internal sealed class WorkflowNavigationAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<PythonCommunicationStatus> CommunicationStatusProvider { get; init; }
        internal WpfLabelingShellViewModel ShellViewModel { get; init; }
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal RoiImageCanvasViewModel MainCanvasViewModel { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal ImageDetectionWorkflowService ImageDetectionWorkflowService { get; init; }
        internal BatchDetectionWorkflowService BatchDetectionWorkflowService { get; init; }
        internal YoloEnvironmentWorkflowService YoloEnvironmentWorkflowService { get; init; }
        internal ModelComparisonWorkflowService ModelComparisonWorkflowService { get; init; }
        internal AnomalyClassificationEvaluationWorkflowService AnomalyClassificationEvaluationWorkflowService { get; init; }
        internal TrainingCommandLifecycleService TrainingCommandLifecycleService { get; init; }
        internal TrainingRuntimeWorkflowService TrainingRuntimeWorkflowService { get; init; }
        internal Func<PythonModelRuntimeState> GetPythonModelRuntimeState { get; init; }
        internal Func<string> GetCurrentRecipeName { get; init; }
        internal Func<YoloDatasetReadinessReport> LastTrainingReadinessReportProvider { get; init; }
        internal Action<WorkflowCommandState, ModelComparisonCommandState> ApplyWorkflowCommandState { get; init; }
        internal Func<int> CanvasLabelObjectCountProvider { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Func<bool> ActiveImageAvailableProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<WpfAnnotationToolItem> SelectedAnnotationToolProvider { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<bool, bool> FocusSelectedCandidateInViewer { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action UpdateDetectionResultOverlay { get; init; }
        internal Action UpdateCanvasCommandButtons { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action UpdateCandidateActionState { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action UpdateWorkflowProgressStatus { get; init; }
        internal Action FocusAnnotationToolsTab { get; init; }
        internal Action FocusDatasetOnboardingTab { get; init; }
        internal Action FocusYoloSettingsTab { get; init; }
        internal TabControl ReviewTabControl { get; init; }
        internal TabItem ObjectsReviewTab { get; init; }
        internal TabItem CandidatesReviewTab { get; init; }
        internal TabItem LearningReviewTab { get; init; }
        internal TabItem ClassesReviewTab { get; init; }
        internal TabItem YoloSettingsReviewTab { get; init; }
        internal Func<WpfTrainingWeightsComparison> BuildCurrentTrainingWeightsComparison { get; init; }
        internal Action<WpfTrainingWeightsComparison> UpdateTrainingComparisonViewModel { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action RefreshCanvasAnnotationToolScope { get; init; }
        internal Action<WpfAnnotationToolItem> ApplyAnnotationToolSelection { get; init; }
        internal Action<bool> RefreshAnnotationVisibilityForDatasetPurpose { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal FrameworkElement DetectButton { get; init; }
        internal FrameworkElement DetectSelectedQueueButton { get; init; }
        internal FrameworkElement BatchDetectQueueButton { get; init; }
        internal FrameworkElement RetryFailedQueueButton { get; init; }
        internal FrameworkElement StopBatchQueueButton { get; init; }
    }
}
