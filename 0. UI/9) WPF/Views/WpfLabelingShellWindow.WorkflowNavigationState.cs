using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.Canvas;
using System;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System.Windows;
using System.Windows.Controls;
using Control = System.Windows.Controls.Control;

namespace MvcVisionSystem
{
    // Responsibility group: canvas workflow navigation and command state fanout.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region CanvasWorkflowCommands
        // Canvas workflow commands wrap view movement, candidate focus, overlay reset, display filtering, and mode switching.
        private void ExecuteFitCanvasCommand()
        {
            MainCanvasViewModel.ImageViewer.ZoomToFit();
        }

        private void ExecuteActualSizeCanvasCommand()
        {
            MainCanvasViewModel.ImageViewer.ZoomToActualSize();
        }

        private void ExecutePanCanvasCommand()
        {
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);
            AppendLog("\uCEA0\uBC84\uC2A4 \uC774\uB3D9 \uBAA8\uB4DC");
        }

        private void ExecuteFocusCandidateCommand()
        {
            FocusSelectedCandidateInViewer(logIfMissing: true);
        }

        private void ExecuteCanvasDisplayModeSelectionChanged(object selectedItem)
        {
            WpfCanvasDisplayModeItem displayModeItem = selectedItem as WpfCanvasDisplayModeItem
                ?? CanvasPanelViewModel?.SelectedDisplayMode;
            if (displayModeItem == null)
            {
                return;
            }

            ApplyCanvasDisplayMode(displayModeItem.Mode, redraw: true, logChange: true);
        }

        private void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange)
        {
            bool changed = CurrentCanvasDisplayMode != mode;
            CanvasPanelViewModel?.SetDisplayMode(mode);
            RefreshCanvasLayerVisibilityState();

            if (redraw)
            {
                RedrawReviewRois();
                UpdateDetectionResultOverlay();
                UpdateCanvasCommandButtons();
            }

            if (logChange && changed)
            {
                string modeText = CanvasWorkflowContextPresentationService.FormatDisplayMode(mode);
                SetModelStatus($"\uCEA0\uBC84\uC2A4 \uBCF4\uAE30: {modeText}");
                AppendLog($"\uCEA0\uBC84\uC2A4 \uBCF4\uAE30: {modeText}");
            }
        }

        private bool ShouldShowLabelOverlays()
            => CurrentCanvasDisplayMode != WpfCanvasDisplayMode.InferenceOnly;

        private bool ShouldShowInferenceOverlays()
            => CurrentCanvasDisplayMode != WpfCanvasDisplayMode.LabelsOnly;

        private WpfCanvasDisplayMode CurrentCanvasDisplayMode
            => CanvasPanelViewModel?.CurrentDisplayMode ?? WpfCanvasDisplayMode.LabelsOnly;

        private void RefreshCanvasLayerVisibilityState()
        {
            int labelCount = GetCanvasLabelObjectCount();
            int candidateCount = pendingDetectionCandidates?.Count ?? 0;
            CanvasPanelViewModel?.SetLayerVisibilityState(
                CurrentCanvasDisplayMode,
                labelCount,
                candidateCount,
                annotationDirtyState.IsDirty);
            CanvasPanelViewModel?.SetNoObjectCompletionState(
                activeImageBitmap != null && !activeImageSize.IsEmpty,
                labelCount > 0,
                candidateCount > 0);
        }

        private void ExecuteResetAiOverlayCommand()
        {
            int removedCount = candidateReviewState.ClearPendingCandidates();
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            RefreshCandidateList();
            RedrawReviewRois();
            UpdateDetectionResultOverlay();
            SetPythonStatus("\uCD94\uB860: AI \uD6C4\uBCF4 \uD45C\uC2DC \uC9C0\uC6C0");
            AppendLog($"AI \uD6C4\uBCF4 \uD45C\uC2DC \uC9C0\uC6C0: {removedCount}\uAC1C");
        }

        private void ExecuteLabelingModeCommand()
        {
            EnterLabelingMode(openGuidePanel: true);
            AppendLog("\uB77C\uBCA8\uB9C1 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4. \uCEA0\uBC84\uC2A4\uB294 \uB77C\uBCA8\uB9CC \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        }

        private void EnterLabelingMode(bool openGuidePanel)
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            if (openGuidePanel)
            {
                FocusAnnotationToolsTab();
            }

            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true)
            {
                if (!MainCanvasViewModel.IsTeachingMode)
                {
                    MainCanvasViewModel.TeachingCommand.Execute(null);
                }
            }
        }

        private void ExecuteInferenceModeCommand()
        {
            SetWorkflowMode(WorkflowMode.Inference);
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true && MainCanvasViewModel.IsTeachingMode)
            {
                MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            AppendLog("\uCD94\uB860 \uAC80\uD1A0 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4. \uCEA0\uBC84\uC2A4\uB294 AI \uCD94\uB860 \uD6C4\uBCF4\uB9CC \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        }

        private void SetWorkflowMode(WorkflowMode mode)
        {
            currentWorkflowMode = mode;
            ApplyCanvasDisplayMode(
                mode == WorkflowMode.Inference
                    ? WpfCanvasDisplayMode.InferenceOnly
                    : WpfCanvasDisplayMode.LabelsOnly,
                redraw: true,
                logChange: false);
            UpdateYoloCommandButtons();
            UpdateCandidateActionState();
            SetModelStatus(mode == WorkflowMode.Inference
                ? "모드: AI 후보 검토"
                : "모드: 라벨링");
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
            ShellViewModel?.SetWorkflowStage(
                mode == WorkflowMode.Inference
                    ? WpfShellWorkflowStage.Inference
                    : WpfShellWorkflowStage.Labeling);
        }

        private void UpdateWorkflowModeUi()
        {
            bool canSwitchMode = !imageDetectionWorkflowService.IsDetecting && !batchDetectionWorkflowService.IsRunning;
            ShellViewModel?.SetWorkflowModeState(
                currentWorkflowMode == WorkflowMode.Inference,
                canSwitchMode);
        }

        private bool EnsureInferenceModeForDetection()
        {
            if (isApplicationCloseApproved)
            {
                return false;
            }

            if (currentWorkflowMode == WorkflowMode.Inference)
            {
                return true;
            }

            SetPythonStatus("AI 후보: 검토 모드 필요");
            SetGlobalInferenceStatus("AI 후보 검토 모드 필요", isBusy: false, isWarning: true);
            AppendLog("검출 건너뜀. 먼저 AI 후보 검토 모드로 전환하세요.");
            UpdateYoloCommandButtons();
            return false;
        }

        // Stage-rail and learning-mode routing use the same workflow-mode owner
        // as Canvas display state, so their transitions stay in this adapter.
        private void ExecuteDatasetHomeCommand()
        {
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
            FocusDatasetOnboardingTab();
            SetModelStatus("작업 단계: 데이터셋 홈");
            AppendLog("작업 단계 이동: 데이터셋 홈");
        }

        private void ExecuteLabelingWorkbenchCommand()
        {
            EnterLabelingWorkbenchStartView();
            SetModelStatus("작업 단계: 라벨링 워크벤치");
            AppendLog("작업 단계 이동: 라벨링 워크벤치");
        }

        private void EnterLabelingWorkbenchStartView()
        {
            LearningWorkflowViewModel?.ShowLabelingTask();
            EnterLabelingMode(openGuidePanel: false);
            ShellViewModel?.SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            ShellViewModel?.SetRightWorkflowDockExpanded(false);
            SelectRightWorkflowView(ObjectsReviewTab);
        }

        private void ExecuteInferenceReviewCommand()
        {
            ExecuteInferenceModeCommand();
            ShowCandidateReviewWorkflowView();
            SetModelStatus("작업 단계: 추론 검토");
            AppendLog("작업 단계 이동: 추론 검토");
        }

        private void ExecuteTrainingModelCenterCommand()
        {
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            FocusYoloSettingsTab();
            SetModelStatus("작업 단계: 학습/모델 센터");
            AppendLog("작업 단계 이동: 학습/모델 센터");
        }

        private void ExecuteReviewCandidateModelCommand()
        {
            ExecuteInferenceModeCommand();
            UpdateTrainingComparisonViewModel(BuildCurrentTrainingWeightsComparison());
            ShowCandidateReviewWorkflowView();
            SetModelStatus("후보 모델 검증: 학습 후보 검토 탭");
            SetYoloCommandStatus("학습 후 후보 검토 탭으로 이동했습니다. 현재 이미지 검사는 현재 검사 버튼에서 실행합니다.", isBusy: false);
            AppendLog("작업 단계 이동: 학습 후보 검토");
        }

        private void DatasetPurposeListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningModeItem selectedPurposeItem = selectedItem as WpfLearningModeItem;
            if (sender is System.Windows.Controls.ListBox purposeListBox
                && selectedPurposeItem != null
                && !ReferenceEquals(purposeListBox.SelectedItem, selectedPurposeItem))
            {
                // The selected-item command is queued by WPF. Ignore an older
                // callback if the ListBox has already moved to the current
                // recipe's canonical purpose.
                return;
            }

            if (selectedPurposeItem != null && !ReferenceEquals(LearningWorkflowViewModel?.SelectedDatasetPurposeMode, selectedPurposeItem))
            {
                LearningWorkflowViewModel.SelectedDatasetPurposeMode = selectedPurposeItem;
            }

            ApplyWorkflowDatasetPurposeToProjectSettings();
            RefreshCanvasAnnotationToolScope();
            ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
            RefreshCanvasWorkflowContext();
            RefreshAnnotationVisibilityForDatasetPurpose(notifyOperator: true);
            RefreshTrainingReadinessPanel(refreshYaml: false);
            RefreshYoloTrainingStepCompletion();
        }

        private void LearningWorkflowModeListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningModeItem selectedModeItem = selectedItem as WpfLearningModeItem;
            if (selectedModeItem != null && !ReferenceEquals(LearningWorkflowViewModel?.SelectedMode, selectedModeItem))
            {
                LearningWorkflowViewModel.SelectedMode = selectedModeItem;
            }

            WpfLearningMode? mode = selectedModeItem?.Mode ?? LearningWorkflowViewModel?.SelectedMode?.Mode;
            if (!mode.HasValue)
            {
                return;
            }

            WpfLearningModeWorkflowAction action = AnnotationWorkflowService.ResolveModeAction(mode.Value);
            switch (action)
            {
                case WpfLearningModeWorkflowAction.Inference:
                    SetWorkflowMode(WorkflowMode.Inference);
                    break;

                case WpfLearningModeWorkflowAction.LabelingAndFocusYoloSettings:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusYoloSettingsTab();
                    break;

                default:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
                    break;
            }
        }
        #endregion

        #region WorkflowCommandStateFanout
        private void UpdateYoloCommandButtons()
        {
            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            WorkflowCommandState state = WorkflowCommandStateService.Build(
                isInferenceMode: currentWorkflowMode == WorkflowMode.Inference,
                isYoloEnvironmentCommandRunning: yoloEnvironmentWorkflowService.IsRunning || modelComparisonWorkflowService.IsModelComparisonRunning || modelComparisonWorkflowService.IsSegmentationComparisonRunning || anomalyClassificationEvaluationWorkflowService.IsRunning,
                isDetecting: imageDetectionWorkflowService.IsDetecting,
                isBatchDetectionRunning: batchDetectionWorkflowService.IsRunning,
                isTrainingCommandRunning: isTrainingCommandRunning || trainingRuntimeWorkflowService.IsTrainingWorkflowRunning,
                isTrainingStopAvailable: TrainingProgressPresentationService.IsTrainingStopAvailable(global.GetPythonCommunicationStatusSnapshot()),
                hasCurrentRecipeName: !string.IsNullOrWhiteSpace(GetCurrentRecipeName()),
                canRunModelTraining: runtimeState.CanRunTraining,
                canRunModelInference: runtimeState.CanRunInference,
                modelRuntimeUnavailableHint: runtimeState.NextActionText);

            // Keep the transitional fallback controls beside the shared state fan-out so command gating stays consistent while panels move to view models.
            ApplyYoloStatusCommandState(state);
            ApplyProjectConfigCommandState(state);
            ApplyYoloModelSettingsCommandState(state);
            ApplyTrainingSettingsCommandState(state);
            ApplyLearningWorkflowCommandState(state);
            ApplyShellCommandState(state);
            ApplyImageQueueCommandState(state);
            UpdateDetectionCommandHints(state);
            UpdateWorkflowModeUi();
        }

        private void ApplyLearningWorkflowCommandState(WorkflowCommandState state)
        {
            ModelComparisonCommandState comparisonState = ModelComparisonCommandStateService.Build(
                modelComparisonWorkflowService.IsModelComparisonRunning,
                state,
                lastYoloTrainingReadinessReport);
            LearningWorkflowViewModel?.SetModelComparisonRunState(
                comparisonState.IsEnabled,
                comparisonState.ActionText,
                comparisonState.ToolTipText,
                comparisonState.BasisText);
        }

        private void ApplyShellCommandState(WorkflowCommandState state)
        {
            if (ShellViewModel != null)
            {
                ShellViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            SetControlEnabled(DetectButton, state?.CanRunInference == true);
        }

        private void ApplyImageQueueCommandState(WorkflowCommandState state)
        {
            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunInference = state?.CanRunInference == true;
            SetControlEnabled(DetectSelectedQueueButton, canRunInference);
            SetControlEnabled(BatchDetectQueueButton, canRunInference);
            SetControlEnabled(RetryFailedQueueButton, canRunInference);
            SetControlEnabled(StopBatchQueueButton, state?.CanStopBatchDetection == true);
        }

        private void ApplyTrainingSettingsCommandState(WorkflowCommandState state)
        {
            if (viewModels.IsModelWorkflowCreated && TrainingSettingsViewModel != null)
            {
                TrainingSettingsViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(RefreshTrainingReadinessButton, canRunGeneralCommands);
            SetControlEnabled(StartTrainingButton, canRunGeneralCommands);
            SetControlEnabled(StopTrainingButton, state?.CanStopTraining == true);
        }

        private void ApplyYoloModelSettingsCommandState(WorkflowCommandState state)
        {
            if (viewModels.IsModelWorkflowCreated && YoloModelSettingsViewModel != null)
            {
                YoloModelSettingsViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(BrowseYoloPythonButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloProjectRootButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloClientScriptButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloWeightsButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloImageRootButton, canRunGeneralCommands);
            SetControlEnabled(SaveYoloSettingsButton, canRunGeneralCommands);
            SetControlEnabled(ResetYoloSettingsButton, canRunGeneralCommands);
        }

        private void ApplyProjectConfigCommandState(WorkflowCommandState state)
        {
            if (ProjectConfigViewModel != null)
            {
                ProjectConfigViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(ApplyProjectRecipeButton, canRunGeneralCommands);
            SetControlEnabled(RefreshProjectRecipeListButton, canRunGeneralCommands);
            SetControlEnabled(SaveProjectConfigButton, state?.CanSaveProjectConfig == true);
            SetControlEnabled(OpenProjectConfigFolderButton, canRunGeneralCommands);
        }

        private void ApplyYoloStatusCommandState(WorkflowCommandState state)
        {
            if (viewModels.IsModelWorkflowCreated && YoloStatusViewModel != null)
            {
                YoloStatusViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(FirstCheckYoloButton, canRunGeneralCommands);
            SetControlEnabled(InstallRequirementsButton, canRunGeneralCommands);
            SetControlEnabled(RunYoloSmokeButton, canRunGeneralCommands);
            SetControlEnabled(RestartPythonWorkerButton, canRunGeneralCommands);
            SetControlEnabled(StopPythonWorkerButton, canRunGeneralCommands);
        }

        private static void SetControlEnabled(Control control, bool isEnabled)
        {
            if (control != null)
            {
                control.IsEnabled = isEnabled;
            }
        }

        private void UpdateDetectionCommandHints(WorkflowCommandState state)
        {
            SetControlToolTip(DetectButton, state?.CurrentImageDetectionToolTip);
            SetControlToolTip(DetectSelectedQueueButton, state?.SelectedQueueDetectionToolTip);
            SetControlToolTip(BatchDetectQueueButton, state?.BatchDetectionToolTip);
            SetControlToolTip(RetryFailedQueueButton, state?.RetryFailedToolTip);
            SetControlToolTip(StopBatchQueueButton, state?.StopBatchToolTip);
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
}
