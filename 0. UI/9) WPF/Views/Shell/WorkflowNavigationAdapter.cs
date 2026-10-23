using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Linq;
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
        private bool isTemporaryPanActive;
        private CanvasInteractionMode temporaryPanPreviousMode;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal WorkflowNavigationAdapter(WorkflowNavigationAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.CommunicationStatusProvider);
        }

        #region CanvasWorkflowCommands
        internal bool IsInferenceWorkflowActive => context.ShellViewModel?.IsInferenceModeActive == true;

        internal bool IsTemporaryPanActive => isTemporaryPanActive;

        internal void ExecuteFitCanvasCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true) return;
            if (context.TryFitInferenceCanvas?.Invoke() == true) return;
            if (context.TryFitLabelingCanvas?.Invoke() == true) return;
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

        internal bool BeginTemporaryPan()
        {
            if (isTemporaryPanActive || context.MainCanvasViewModel?.ImageViewer == null)
            {
                return false;
            }

            temporaryPanPreviousMode = context.MainCanvasViewModel.ImageViewer.GetViewMode();
            isTemporaryPanActive = true;
            context.MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);
            return true;
        }

        internal bool EndTemporaryPan()
        {
            if (!isTemporaryPanActive || context.MainCanvasViewModel?.ImageViewer == null)
            {
                return false;
            }

            isTemporaryPanActive = false;
            context.MainCanvasViewModel.ImageViewer.SetViewMode(temporaryPanPreviousMode);
            return true;
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
            if (!SetWorkflowMode(isInferenceMode: false))
            {
                return;
            }

            context.ShellViewModel?.ShowSavedLabelsWorkflow();
            SelectRightWorkflowView(context.ObjectsReviewTab);
        }

        internal void ShowCandidateReviewWorkflowView()
        {
            if (context.ShellViewModel?.IsLabelingStageActive == true)
            {
                context.ShellViewModel.ShowCandidateReviewWorkflow(keepLabelingStage: true);
                SelectRightWorkflowView(context.ObjectsReviewTab);
                ApplyCanvasDisplayMode(WpfCanvasDisplayMode.Both, redraw: true, logChange: false);
                return;
            }
            if (!SetWorkflowMode(isInferenceMode: true))
            {
                return;
            }

            context.ShellViewModel?.ShowCandidateReviewWorkflow();
            SelectRightWorkflowView(context.CandidatesReviewTab);
        }

        internal void ShowGuideToolsWorkflowView(WpfShellWorkflowStage stage)
        {
            if (!TryApproveStageChange(stage))
            {
                return;
            }

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
            context.ShellViewModel?.OpenClassCatalogDialog();
        }

        internal void ShowYoloModelCenterWorkflowView()
        {
            if (!TryApproveStageChange(WpfShellWorkflowStage.TrainingModel))
            {
                return;
            }

            context.ShellViewModel?.SetRecipeLibraryVisible(false);
            context.ShellViewModel?.ShowModelCenterWorkflow();
            SelectRightWorkflowView(context.YoloSettingsReviewTab);
        }

        internal void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange)
        {
            if (context.LearningWorkflowViewModel != null && context.CanvasPanelViewModel != null)
                context.LearningWorkflowViewModel.MaskOpacity = context.CanvasPanelViewModel.OverlayOpacity;
            context.RefreshReferenceDisplay?.Invoke();
            bool changed = context.CanvasPanelViewModel?.CurrentDisplayMode != mode;
            context.CanvasPanelViewModel?.SetDisplayMode(mode);
            RefreshCanvasLayerVisibilityState();
            context.RefreshCanvasWorkflowContext?.Invoke();

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

        internal void ApplyTemporaryOriginalViewState(bool _)
        {
            context.RefreshReferenceDisplay?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.UpdateDetectionResultOverlay?.Invoke();
            context.UpdateCanvasCommandButtons?.Invoke();
        }

        internal void RefreshCanvasLayerVisibilityState()
        {
            context.RefreshReferenceDisplay?.Invoke();
            int labelCount = context.CanvasLabelObjectCountProvider?.Invoke() ?? 0;
            int candidateCount = context.PendingCandidateCountProvider?.Invoke() ?? 0;
            context.CanvasPanelViewModel?.SetLayerVisibilityState(
                context.CanvasPanelViewModel?.CurrentDisplayMode ?? WpfCanvasDisplayMode.LabelsOnly,
                labelCount,
                candidateCount,
                context.AnnotationDirtyState?.IsDirty == true);
            context.CanvasPanelViewModel?.SetNoObjectCompletionState(
                context.ActiveImageAvailableProvider?.Invoke() == true && context.ShellViewModel?.IsAnnotationWorkflowVisible != false,
                labelCount > 0,
                candidateCount > 0,
                context.ActiveImagePathProvider?.Invoke());
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

        internal bool ExecuteLabelingModeCommand()
        {
            if (!EnterLabelingMode(openGuidePanel: true))
            {
                return false;
            }

            context.AppendLog?.Invoke("라벨링 모드로 전환했습니다. 캔버스는 라벨만 표시합니다.");
            return true;
        }

        internal bool EnterLabelingMode(bool openGuidePanel)
        {
            if (!SetWorkflowMode(isInferenceMode: false))
            {
                return false;
            }

            if (context.ShellViewModel != null)
            {
                context.ShellViewModel.IsInferenceInspectionOnly = false;
            }

            context.ApplyAnnotationToolSelection?.Invoke(context.SelectedAnnotationToolProvider?.Invoke());

            if (openGuidePanel)
            {
                context.FocusAnnotationToolsTab?.Invoke();
            }

            return true;
        }

        internal bool TrySendInferenceResultsToLabeling(InferenceResultReviewEntry entry, double displayConfidence)
        {
            if (entry?.Result?.Succeeded != true || string.IsNullOrWhiteSpace(entry.Result.Source?.ImagePath) || context.CandidateReviewState == null
                || context.ShellViewModel?.IsInferenceStageActive != true || context.ShellViewModel.IsAnomalyImageReviewMode
                || context.IsApplicationCloseApproved?.Invoke() == true || context.ImageDetectionWorkflowService.IsDetecting || context.BatchDetectionWorkflowService.IsRunning
                || context.AnnotationDirtyState?.IsDirty == true || !CanStartLabeling()
                || !double.IsFinite(displayConfidence) || displayConfidence < 0 || displayConfidence > 1
                || entry.Result.Candidates?.Any(candidate => candidate?.ImageLevel == true) == true
                || context.CandidateReviewState?.HasPendingCandidates == true && !context.ShellViewModel.IsInferenceResultReadOnly
                || !string.Equals(entry.Result.Source?.ImagePath, context.ActiveImagePathProvider?.Invoke(), StringComparison.OrdinalIgnoreCase)) return false;
            if (!EnterLabelingMode(openGuidePanel: false)) return false;
            if (context.CandidateReviewState?.LoadInferenceResultForLabeling(entry, displayConfidence, projectData?.ClassNamedList) != true) return false;
            context.ShellViewModel.RefreshInferenceResultPresentation();
            if (!context.ShellViewModel.IsLabelingAssistPanelExpanded) context.ShellViewModel.ToggleLabelingAssistPanelCommand.Execute(null);
            context.ShellViewModel.ShowCandidateReviewWorkflow(keepLabelingStage: true);
            SelectRightWorkflowView(context.ObjectsReviewTab);
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.Both, redraw: false, logChange: false);
            context.RefreshCandidateList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.UpdateDetectionResultOverlay?.Invoke();
            context.UpdateCandidateActionState?.Invoke();
            string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Transfer.Done"), context.CandidateReviewState.PendingCount);
            context.SetModelStatus?.Invoke(message); context.AppendLog?.Invoke(message);
            return true;
        }

        internal bool ExecuteInferenceModeCommand()
        {
            if (!SetWorkflowMode(isInferenceMode: true))
            {
                return false;
            }

            if (context.MainCanvasViewModel?.TeachingCommand?.CanExecute(null) == true
                && context.MainCanvasViewModel.IsTeachingMode)
            {
                context.MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.WorkflowNavigationAdapter.InferenceMode"));
            return true;
        }

        internal bool SetWorkflowMode(bool isInferenceMode)
        {
            WpfShellWorkflowStage targetStage = isInferenceMode
                ? WpfShellWorkflowStage.Inference
                : WpfShellWorkflowStage.Labeling;
            if (!TryApproveStageChange(targetStage))
            {
                return false;
            }

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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Mode.InferenceResults")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Mode.Labeling"));
            context.RefreshCanvasWorkflowContext?.Invoke();
            context.UpdateWorkflowProgressStatus?.Invoke();
            context.ShellViewModel?.SetWorkflowStage(
                isInferenceMode ? WpfShellWorkflowStage.Inference : WpfShellWorkflowStage.Labeling);
            context.RefreshReviewModeCommandState?.Invoke();
            return true;
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

            if (IsInferenceWorkflowActive || context.ShellViewModel?.IsLabelingAssistPanelExpanded == true)
            {
                return true;
            }

            context.SetPythonStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Inference.NeedsView.Status"));
            context.SetGlobalInferenceStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Inference.NeedsView.GlobalStatus"), false, true);
            context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Inference.NeedsView.Log"));
            UpdateYoloCommandButtons();
            return false;
        }

        internal void ExecuteDatasetHomeCommand()
        {
            if (!TryApproveStageChange(WpfShellWorkflowStage.Dataset))
            {
                return;
            }

            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
            context.RefreshReviewModeCommandState?.Invoke();
            context.FocusDatasetOnboardingTab?.Invoke();
            context.SetModelStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.Dataset.Status"));
            context.AppendLog?.Invoke("작업 단계 이동: 데이터셋 홈");
        }

        internal bool CanStartLabeling() => string.IsNullOrWhiteSpace(context.LabelingStartUnavailableHintProvider?.Invoke());

        internal void ExecuteLabelingWorkbenchCommand()
        {
            string unavailableHint = context.LabelingStartUnavailableHintProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(unavailableHint))
            {
                context.SetModelStatus?.Invoke(unavailableHint);
                context.AppendLog?.Invoke(unavailableHint);
                return;
            }

            if (!EnterLabelingWorkbenchStartView())
            {
                return;
            }

            context.SetModelStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.Labeling.Status"));
            context.AppendLog?.Invoke("작업 단계 이동: 라벨링 워크벤치");
        }

        internal bool EnterLabelingWorkbenchStartView()
        {
            if (!EnterLabelingMode(openGuidePanel: false))
            {
                return false;
            }

            context.LearningWorkflowViewModel?.ShowLabelingTask();
            context.ShellViewModel?.SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            context.ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(context.ObjectsReviewTab);
            return true;
        }

        internal void ExecuteInferenceReviewCommand()
        {
            bool enteringInference = context.ShellViewModel?.IsInferenceStageActive != true;
            if (!ExecuteInferenceModeCommand())
            {
                return;
            }

            if (enteringInference && context.ShellViewModel != null)
            {
                context.ShellViewModel.IsInferenceInspectionOnly = true;
            }
            ShowCandidateReviewWorkflowView();
            context.SetModelStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.InferenceResults.Status"));
            context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.InferenceResults.Log"));
        }

        internal void ExecuteTrainingModelCenterCommand()
        {
            if (!TryApproveStageChange(WpfShellWorkflowStage.TrainingModel))
            {
                return;
            }

            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            context.RefreshReviewModeCommandState?.Invoke();
            context.FocusYoloSettingsTab?.Invoke();
            context.SetModelStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.Training.Status"));
            context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.Training.Log"));
        }

        internal void ExecuteReviewCandidateModelCommand()
        {
            if (context.OpenModelComparisonWorkspace != null)
            {
                ExecuteModelComparisonCommand();
                return;
            }
            if (!ExecuteInferenceModeCommand())
            {
                return;
            }

            WpfTrainingWeightsComparison comparison = context.BuildCurrentTrainingWeightsComparison?.Invoke();
            if (comparison != null)
            {
                context.UpdateTrainingComparisonViewModel?.Invoke(comparison);
            }

            ShowCandidateReviewWorkflowView();
            context.SetModelStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.TrainedModelCheck.Status"));
            context.SetYoloCommandStatus?.Invoke(
                OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.TrainedModelCheck.Detail"),
                false);
            context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Navigation.Stage.TrainedModelCheck.Log"));
        }

        internal void ExecuteModelComparisonCommand()
        {
            if (!TryApproveStageChange(WpfShellWorkflowStage.ModelComparison)) return;
            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.ModelComparison);
            context.RefreshReviewModeCommandState?.Invoke();
            context.OpenModelComparisonWorkspace?.Invoke();
            context.SetModelStatus?.Invoke(OpenVisionLanguageService.T("WpfComparisonWorkspace.Title"));
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
            if (IsStageActive(WpfShellWorkflowStage.Labeling))
            {
                context.ApplyAnnotationToolSelection?.Invoke(context.SelectedAnnotationToolProvider?.Invoke());
            }
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
                    if (!SetWorkflowMode(isInferenceMode: false))
                    {
                        return;
                    }

                    context.FocusYoloSettingsTab?.Invoke();
                    break;

                default:
                    if (!SetWorkflowMode(isInferenceMode: false))
                    {
                        return;
                    }

                    context.ApplyAnnotationToolSelection?.Invoke(context.SelectedAnnotationToolProvider?.Invoke());
                    break;
            }
        }

        private bool TryApproveStageChange(WpfShellWorkflowStage targetStage)
        {
            if (context.IsComparisonConfirmationOpen?.Invoke() == true) return false;
            if (IsStageActive(targetStage))
            {
                return true;
            }

            WorkflowTransitionApproval approval = context.WorkflowTransitionAdapter?.TryApprove(
                WorkflowStagePresentationService.Build(targetStage).TitleText,
                replacesRecipe: false, allowCapturedTrainingBrowse: targetStage != WpfShellWorkflowStage.Labeling) ?? WorkflowTransitionApproval.Allowed;
            return approval.IsAllowed
                && (approval.DiscardAnnotations || approval.DiscardSettings
                    ? context.WorkflowTransitionAdapter?.ApplyDiscard(approval) == true
                    : true);
        }

        private bool IsStageActive(WpfShellWorkflowStage stage)
            => stage switch
            {
                WpfShellWorkflowStage.Dataset => context.ShellViewModel?.IsDatasetStageActive == true,
                WpfShellWorkflowStage.Labeling => context.ShellViewModel?.IsLabelingStageActive == true,
                WpfShellWorkflowStage.Inference => context.ShellViewModel?.IsInferenceStageActive == true,
                WpfShellWorkflowStage.TrainingModel => context.ShellViewModel?.IsTrainingModelStageActive == true,
                WpfShellWorkflowStage.ModelComparison => context.ShellViewModel?.IsModelComparisonStageActive == true,
                _ => false
            };
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
                    || context.TrainingRuntimeWorkflowService.IsTrainingWorkflowRunning
                    || context.TrainingRuntimeWorkflowService.IsQueueBusy || context.TrainingRuntimeWorkflowService.HasPendingQueueCandidate,
                isTrainingStopAvailable: TrainingProgressPresentationService.IsTrainingStopAvailable(
                    context.CommunicationStatusProvider()),
                hasCurrentRecipeName: !string.IsNullOrWhiteSpace(context.GetCurrentRecipeName?.Invoke()),
                canRunModelTraining: runtimeState?.CanRunTraining == true,
                canRunModelInference: runtimeState?.CanRunInference == true,
                modelRuntimeUnavailableHint: runtimeState?.NextActionText,
                canRunTrialModelInference: context.ShellViewModel?.SelectedInferenceModelHistoryItem?.CanRunInferenceTrial == true,
                canRunInferenceTargetFromLabelingAssist: context.ShellViewModel?.IsLabelingAssistPanelExpanded == true);
            WpfModelRegistryHistoryItem selectedInferenceModel = context.ShellViewModel?.SelectedInferenceModelHistoryItem;
            PythonModelSettings currentModel = context.DataProvider?.Invoke()?.ProjectSettings?.PythonModel;
            bool hasSelectedInferenceCandidate = selectedInferenceModel?.CanRunInferenceTrial == true
                && !string.Equals(
                    selectedInferenceModel.WeightsPath?.Trim(),
                    currentModel?.WeightsPath?.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            bool selectedInferenceCandidateUsesCurrentRuntime = hasSelectedInferenceCandidate
                && string.Equals(
                    PythonModelSettings.NormalizeModelEngine(selectedInferenceModel.ModelEngine),
                    PythonModelSettings.NormalizeModelEngine(currentModel?.ModelEngine),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    selectedInferenceModel.ProjectRootPath?.Trim().TrimEnd('\\', '/'),
                    currentModel?.ProjectRootPath?.Trim().TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);

            context.ApplyWorkflowCommandState?.Invoke(
                state,
                ModelComparisonCommandStateService.Build(
                    context.ModelComparisonWorkflowService.IsModelComparisonRunning,
                    state,
                    context.LastTrainingReadinessReportProvider?.Invoke(),
                    isInferenceStageActive: context.ShellViewModel?.IsInferenceStageActive == true,
                    hasSelectedInferenceCandidate: hasSelectedInferenceCandidate,
                    selectedInferenceCandidateUsesCurrentRuntime: selectedInferenceCandidateUsesCurrentRuntime));
            UpdateDetectionCommandHints(state);
            context.ShellViewModel?.RefreshInferenceReviewState();
            context.RefreshInferenceQueuePresentation?.Invoke();
            context.RefreshModelErrorReviewCommands?.Invoke();
            context.RefreshReviewModeCommandState?.Invoke();
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
        internal Func<bool> IsComparisonConfirmationOpen { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal WorkflowTransitionAdapter WorkflowTransitionAdapter { get; init; }
        internal Func<PythonCommunicationStatus> CommunicationStatusProvider { get; init; }
        internal WpfLabelingShellViewModel ShellViewModel { get; init; }
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal RoiImageCanvasViewModel MainCanvasViewModel { get; init; }
        internal Func<bool> TryFitInferenceCanvas { get; init; }
        internal Func<bool> TryFitLabelingCanvas { get; init; }
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
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<string> LabelingStartUnavailableHintProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<WpfAnnotationToolItem> SelectedAnnotationToolProvider { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<bool, bool> FocusSelectedCandidateInViewer { get; init; }
        internal Action RefreshReferenceDisplay { get; init; }
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
        internal Action RefreshModelErrorReviewCommands { get; init; }
        internal Action RefreshInferenceQueuePresentation { get; init; }
        internal Action RefreshReviewModeCommandState { get; init; }
        internal Action OpenModelComparisonWorkspace { get; init; }
        internal FrameworkElement DetectButton { get; init; }
        internal FrameworkElement DetectSelectedQueueButton { get; init; }
        internal FrameworkElement BatchDetectQueueButton { get; init; }
        internal FrameworkElement RetryFailedQueueButton { get; init; }
        internal FrameworkElement StopBatchQueueButton { get; init; }
    }
}
