using OpenVisionLab.Mvvm.Behaviors;
using System.Windows;
using OpenVisionLab;
using System.ComponentModel;
using System.Windows.Data;

namespace MvcVisionSystem
{
    // Responsibility group: panel-specific command and name wiring.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region PanelWiring.Canvas
        // Canvas panel wiring owns toolbar commands and workflow context text only.
        private void ConfigureCanvasPanelCommands()
        {
            CanvasPanelViewModel.ConfigureCommands(
                ExecuteFitCanvasCommand,
                ExecuteActualSizeCanvasCommand,
                ExecutePanCanvasCommand,
                ExecuteFocusCandidateCommand,
                ExecuteResetAiOverlayCommand);
            CanvasPanelViewModel.ConfigureDisplayAdjustment(
                ScheduleDisplayAdjustmentRefresh);
            CanvasPanelViewModel.ConfigureCandidateReviewCommands(
                ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand,
                ExecuteFocusCurrentLabelCommand,
                ExecuteConfirmSelectedCandidateCommand,
                ExecuteSkipSelectedCandidateCommand);
            CanvasPanelViewModel.ConfigureAnnotationTools(
                LearningWorkflowViewModel.VisibleAnnotationTools,
                LearningWorkflowViewModel.SelectedTool,
                ExecuteCanvasAnnotationToolSelectionChanged);
            CanvasPanelViewModel.ConfigureAnnotationCommands(
                ExecuteUndoAnnotationCommand,
                ExecuteRedoAnnotationCommand,
                ExecuteDeleteObjectCommand);
            CanvasPanelViewModel.ConfigureAnnotationSaveCommand(
                ExecuteSaveAnnotationsCommand);
            CanvasPanelViewModel.ConfigureNoObjectCompletionCommand(
                ExecuteCompleteNoObjectAndNextCommand);
            CanvasPanelViewModel.ConfigureLabelClassSelection(
                selected => CanvasLabelClass_SelectionChanged(CanvasLabelClassListBox, selected),
                () => ShowClassCatalogWorkflowView(WpfShellWorkflowStage.Labeling));
            CanvasPanelViewModel.ConfigureDisplayModeSelection(
                ExecuteCanvasDisplayModeSelectionChanged);
            CanvasPanelViewModel.ConfigureBoxDrawingMethod(
                ExecuteSetBoxDrawingMethod);
            CanvasPanelViewModel.ConfigureBrushSizeCommands(
                ExecuteDecreaseBrushSizeCommand,
                ExecuteIncreaseBrushSizeCommand);
            CanvasPanelViewModel.ConfigureSmartMaskCommands(
                ExecuteCreateSmartMaskCandidateCommand,
                () => ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode.Positive),
                () => ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode.Negative),
                ExecuteUndoSmartMaskPointCommand,
                ExecuteClearSmartMaskPointsCommand,
                ExecuteCancelSmartMaskGenerationCommand,
                ExecuteNextSmartMaskInstanceCommand,
                () => ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion.Initial),
                () => ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion.Latest),
                ExecuteSetSmartMaskAutoContourMode,
                ExecuteSetSmartMaskPolygonDetailCommand);
            SyncCanvasBrushSizeFromWorkflow();
            RefreshCanvasAnnotationToolScope();
            RefreshCanvasWorkflowContext();
            RefreshAttachedCommandBindings(
                CanvasAnnotationToolListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(
                CanvasLabelClassListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(
                CanvasDisplayModeListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RefreshCanvasAnnotationToolScope()
        {
            bool isAnomalyImageReview = IsAnomalyDatasetPurpose();
            ShellViewModel?.SetAnomalyImageReviewMode(isAnomalyImageReview);
            CanvasPanelViewModel?.SetAnomalyImageReviewMode(isAnomalyImageReview);
            ImageQueueViewModel?.SetAnomalyImageReviewMode(isAnomalyImageReview);
            RefreshImageQueuePurposePresentation();
            if (isAnomalyImageReview
                && ShellViewModel?.IsLabelingStageActive == true
                && LearningReviewTab != null)
            {
                ReviewTabControl.SelectedItem = LearningReviewTab;
            }
            CanvasPanelViewModel?.ConfigureAnnotationTools(
                LearningWorkflowViewModel?.VisibleAnnotationTools,
                LearningWorkflowViewModel?.SelectedTool,
                ExecuteCanvasAnnotationToolSelectionChanged);
        }

        private void RefreshCanvasWorkflowContext()
        {
            RefreshSmartMaskCommandState();
            WpfLearningStepItem selectedStep = LearningWorkflowViewModel?.SelectedStep;
            WpfAnnotationToolItem selectedTool = CanvasPanelViewModel?.SelectedAnnotationTool
                ?? LearningWorkflowViewModel?.SelectedTool;
            CanvasWorkflowContext context = CanvasWorkflowContextPresentationService.Build(
                isInferenceMode: currentWorkflowMode == WorkflowMode.Inference,
                isAnomalyDatasetPurpose: IsAnomalyDatasetPurpose(),
                hasActiveImage: !activeImageSize.IsEmpty,
                hasUnsavedAnnotations: annotationDirtyState.IsDirty,
                hasCanvasLabelObjects: HasCanvasLabelObjects(),
                pendingCandidateCount: pendingDetectionCandidates?.Count ?? 0,
                selectedStep: selectedStep?.Step,
                selectedStepText: selectedStep?.Text,
                selectedTool: selectedTool?.Tool,
                selectedToolText: selectedTool?.Text,
                activeAnnotationTool: activeAnnotationTool,
                selectedBoxDrawingMethod: CanvasPanelViewModel?.SelectedBoxDrawingMethod?.Method);
            CanvasPanelViewModel?.SetWorkflowContext(context);
            LearningWorkflowViewModel?.SetLiveLabelingTask(context);
        }

        private bool HasCanvasLabelObjects()
            => GetCanvasLabelObjectCount() > 0;

        private int GetCanvasLabelObjectCount()
            => manualRois.Count + GetVisibleManualSegmentCount() + confirmedDetectionCandidates.Count;

        private void RegisterCanvasPanelNames()
        {
            ConfigureCanvasPanelCommands();
            RegisterCanvasName(nameof(MainCanvasView), MainCanvasView);
            RegisterCanvasName(nameof(CanvasAnnotationToolListBox), CanvasAnnotationToolListBox);
            RegisterCanvasName(nameof(CanvasLabelClassListBox), CanvasLabelClassListBox);
            RegisterCanvasName(nameof(CanvasDisplayModeListBox), CanvasDisplayModeListBox);
            RegisterCanvasName(nameof(CanvasWorkflowContextStrip), CanvasWorkflowContextStrip);
            RegisterCanvasName(nameof(CanvasCurrentStepText), CanvasCurrentStepText);
            RegisterCanvasName(nameof(CanvasCurrentToolText), CanvasCurrentToolText);
            RegisterCanvasName(nameof(CanvasNextActionText), CanvasNextActionText);
            RegisterCanvasName(nameof(CanvasLayerVisibilityStrip), CanvasLayerVisibilityStrip);
            RegisterCanvasName(nameof(CanvasLayerModeTitleText), CanvasLayerModeTitleText);
            RegisterCanvasName(nameof(CanvasLayerModeDetailText), CanvasLayerModeDetailText);
            RegisterCanvasName(nameof(CanvasLabelLayerText), CanvasLabelLayerText);
            RegisterCanvasName(nameof(CanvasInferenceLayerText), CanvasInferenceLayerText);
            RegisterCanvasName(nameof(CanvasSaveAnnotationButton), CanvasSaveAnnotationButton);
            RegisterCanvasName(nameof(CanvasCreateSmartMaskButton), CanvasCreateSmartMaskButton);
            RegisterCanvasName(nameof(CanvasCompleteNoObjectButton), CanvasCompleteNoObjectButton);
            RegisterCanvasName(nameof(CanvasAnnotationSaveStateCard), CanvasAnnotationSaveStateCard);
            RegisterCanvasName(nameof(CanvasAnnotationSaveStatusTitleText), CanvasAnnotationSaveStatusTitleText);
            RegisterCanvasName(nameof(CanvasAnnotationSaveStatusDetailText), CanvasAnnotationSaveStatusDetailText);
            RegisterCanvasName(nameof(CanvasActiveLabelClassCard), CanvasActiveLabelClassCard);
            RegisterCanvasName(nameof(CanvasActiveLabelClassTitleText), CanvasActiveLabelClassTitleText);
            RegisterCanvasName(nameof(CanvasActiveLabelClassDetailText), CanvasActiveLabelClassDetailText);
            RegisterCanvasName(nameof(CanvasOpenClassCatalogButton), CanvasOpenClassCatalogButton);
            RegisterCanvasName(nameof(FitCanvasButton), FitCanvasButton);
            RegisterCanvasName(nameof(ActualSizeCanvasButton), ActualSizeCanvasButton);
            RegisterCanvasName(nameof(PanCanvasButton), PanCanvasButton);
            RegisterCanvasName(nameof(DisplayAdjustmentCanvasButton), DisplayAdjustmentCanvasButton);
            RegisterCanvasName(nameof(DisplayAdjustmentPopup), DisplayAdjustmentPopup);
            RegisterCanvasName(nameof(FocusCandidateCanvasButton), FocusCandidateCanvasButton);
            RegisterCanvasName(nameof(ResetAiOverlayCanvasButton), ResetAiOverlayCanvasButton);
            RegisterCanvasName(nameof(DetectionResultOverlay), DetectionResultOverlay);
            RegisterCanvasName(nameof(DetectionOverlayTitleText), DetectionOverlayTitleText);
            RegisterCanvasName(nameof(DetectionOverlaySummaryText), DetectionOverlaySummaryText);
            RegisterCanvasName(nameof(DetectionOverlaySelectedBorder), DetectionOverlaySelectedBorder);
            RegisterCanvasName(nameof(DetectionOverlaySelectedText), DetectionOverlaySelectedText);
            RegisterCanvasName(nameof(DetectionOverlayDetailText), DetectionOverlayDetailText);
        }

        private void RegisterCanvasName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }
        #endregion

        #region PanelWiring.ImageQueue
        // Image queue panel wiring stays beside its filter/selection command bindings.
        private void InitializeImageQueuePanel()
        {
            ConfigureImageQueuePanelCommands();
            ImageQueueFilterBox.ItemsSource = WpfImageQueueFilterOption.CreateDefaults();
            ImageQueueFilterBox.SelectedIndex = 0;
            imageQueueView = CollectionViewSource.GetDefaultView(imageQueueItems);
            imageQueueView.Filter = item => ShouldShowImageQueueItem(item as WpfImageQueueItem);
            ConfigureImageQueueLiveFiltering();
            ImageQueueGrid.ItemsSource = imageQueueView;
            UpdateQueueQuickFilterButtons();
        }

        private void ConfigureImageQueueLiveFiltering()
        {
            if (!(imageQueueView is ICollectionViewLiveShaping liveShaping)
                || !liveShaping.CanChangeLiveFiltering)
            {
                return;
            }

            liveShaping.LiveFilteringProperties.Clear();
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.FileName));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsLabeled));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsSaveRequired));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.ReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.QualityReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.AnomalyReviewState));
            liveShaping.IsLiveFiltering = true;
        }

        private void RefreshImageQueueViewAfterItemStateChange()
        {
            if (imageQueueView is ICollectionViewLiveShaping liveShaping
                && liveShaping.IsLiveFiltering == true)
            {
                return;
            }

            imageQueueView?.Refresh();
        }

        private void ConfigureImageQueuePanelCommands()
        {
            ImageQueueViewModel.ConfigureCommands(
                ExecuteLoadImageRootQueueCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteOpenCurrentImageFolderCommand,
                ExecuteRefreshImageQueueCommand,
                ExecuteNextUnlabeledQueueCommand,
                ExecuteOpenSelectedQueueImageCommand,
                ExecuteDetectSelectedQueueCommand,
                ExecuteBatchDetectQueueCommand,
                TemplateMatchingAutoLabelViewModel.RunBatch,
                ExecuteRetryFailedQueueCommand,
                ExecuteStopBatchQueueCommand,
                ExecuteQueueFilterUnfinishedCommand,
                ExecuteQueueFilterAllCommand,
                ExecuteQueueFilterCandidateCommand,
                ExecuteQueueFilterFailedCommand,
                ExecuteQueueFilterConfirmedCommand,
                ExecuteQueueFilterSkippedCommand,
                ExecuteQueueFilterNoCandidateCommand,
                ExecuteSelectedQueueItemChanged,
                selected => ImageQueueFilterBox_SelectionChanged(ImageQueueFilterBox, selected),
                text => ImageQueueSearchBox_TextChanged(ImageQueueSearchBox, text),
                selected => ImageQueueGrid_SelectionChanged(ImageQueueGrid, selected),
                () => ImageQueueGrid_MouseDoubleClick(ImageQueueGrid),
                ExecuteApplyAnomalyFolderStateSuggestionCommand,
                ExecuteDismissAnomalyFolderStateSuggestionCommand,
                ExecuteMarkActiveAnomalyNormalAndNextCommand,
                ExecuteMarkActiveAnomalyAbnormalAndNextCommand,
                ExecuteClearActiveAnomalyReviewCommand);
            RefreshAttachedCommandBindings(ImageQueueFilterBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(ImageQueueSearchBox, InputCommandBehaviors.TextInputCommandProperty);
            RefreshAttachedCommandBindings(
                ImageQueueGrid,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.MouseDoubleClickInputCommandProperty);
            SeedImageQueueInputCommands();
        }

        private void RegisterImageQueuePanelNames()
        {
            RegisterImageQueueName(nameof(ImageQueueFilterBox), ImageQueueFilterBox);
            RegisterImageQueueName(nameof(ImageQueuePanelTitleText), ImageQueuePanelTitleText);
            RegisterImageQueueName(nameof(ImageQueueSearchBox), ImageQueueSearchBox);
            RegisterImageQueueName(nameof(ImageQueueGrid), ImageQueueGrid);
            RegisterImageQueueName(nameof(BatchStatusText), BatchStatusText);
            RegisterImageQueueName(nameof(BatchProgressBar), BatchProgressBar);
            RegisterImageQueueName(nameof(CurrentImageFolderPathText), CurrentImageFolderPathText);
            RegisterImageQueueName(nameof(OpenCurrentImageFolderButton), OpenCurrentImageFolderButton);
            RegisterImageQueueName(nameof(OpenSelectedQueueImageButton), OpenSelectedQueueImageButton);
            RegisterImageQueueName(nameof(DetectSelectedQueueButton), DetectSelectedQueueButton);
            RegisterImageQueueName(nameof(BatchDetectQueueButton), BatchDetectQueueButton);
            RegisterImageQueueName(nameof(TemplateBatchQueueButton), TemplateBatchQueueButton);
            RegisterImageQueueName(nameof(RetryFailedQueueButton), RetryFailedQueueButton);
            RegisterImageQueueName(nameof(StopBatchQueueButton), StopBatchQueueButton);
            RegisterImageQueueName(nameof(QueueFilterUnfinishedButton), QueueFilterUnfinishedButton);
            RegisterImageQueueName(nameof(QueueFilterAllButton), QueueFilterAllButton);
            RegisterImageQueueName(nameof(QueueFilterCandidateButton), QueueFilterCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterFailedButton), QueueFilterFailedButton);
            RegisterImageQueueName(nameof(QueueFilterConfirmedButton), QueueFilterConfirmedButton);
            RegisterImageQueueName(nameof(QueueFilterSkippedButton), QueueFilterSkippedButton);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateButton), QueueFilterNoCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterUnfinishedText), QueueFilterUnfinishedText);
            RegisterImageQueueName(nameof(QueueFilterAllText), QueueFilterAllText);
            RegisterImageQueueName(nameof(QueueFilterCandidateText), QueueFilterCandidateText);
            RegisterImageQueueName(nameof(QueueFilterFailedText), QueueFilterFailedText);
            RegisterImageQueueName(nameof(QueueFilterConfirmedText), QueueFilterConfirmedText);
            RegisterImageQueueName(nameof(QueueFilterSkippedText), QueueFilterSkippedText);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateText), QueueFilterNoCandidateText);
        }

        private void RegisterImageQueueName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }
        #endregion

        #region PanelWiring.LearningWorkflow
        // Learning workflow panel wiring is isolated so guide-step commands do not mix with other panel registrations.
        private void ConfigureLearningWorkflowPanelCommands()
        {
            LearningWorkflowViewModel.ConfigureCommands(
                selected => DatasetPurposeListBox_SelectionChanged(DatasetPurposeListBox, selected),
                selected => ExecuteStartDatasetSetupCommand(selected),
                selected => LearningWorkflowModeListBox_SelectionChanged(LearningModeListBox, selected),
                selected => AnnotationToolListBox_SelectionChanged(AnnotationToolListBox, selected),
                selected => LearningStepListBox_SelectionChanged(LearningStepListBox, selected),
                step => ExecuteYoloTrainingWorkflowStep(step?.Order ?? 0, LearningWorkflowPanelControl),
                ExecuteOpenTutorialHtmlGuideCommand,
                ExecuteFixYoloClassesCommand,
                ExecuteFixYoloLabelsCommand,
                ExecuteFixYoloDatasetCommand,
                ExecuteDatasetDashboardMetricCommand,
                ExecuteRunModelComparisonCommand,
                ExecuteChangeDatasetCommand,
                ExecuteFirstRunSamplePathCommand,
                TemplateMatchingAutoLabelViewModel.RunCurrentImage,
                TemplateMatchingAutoLabelViewModel.RunBatch,
                ExecuteExternalEvaluationDataAuditCommand,
                ExecuteSelectExternalYoloDatasetCommand,
                ExecuteActivateExternalYoloDatasetCommand,
                ExecuteClearExternalYoloDatasetCommand);
            RefreshAttachedCommandBindings(DatasetPurposeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(LearningModeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(AnnotationToolListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(LearningStepListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterLearningWorkflowPanelNames()
        {
            ConfigureLearningWorkflowPanelCommands();
            RegisterLearningWorkflowName(nameof(DatasetPurposeListBox), DatasetPurposeListBox);
            RegisterLearningWorkflowName(nameof(DatasetPurposeSummaryText), DatasetPurposeSummaryText);
            RegisterLearningWorkflowName(nameof(DatasetPurposeToolSummaryText), DatasetPurposeToolSummaryText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathPanel), FirstRunSamplePathPanel);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathTitleText), FirstRunSamplePathTitleText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathSummaryText), FirstRunSamplePathSummaryText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathPrimaryActionText), FirstRunSamplePathPrimaryActionText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathItemsControl), FirstRunSamplePathItemsControl);
            RegisterLearningWorkflowName(nameof(DatasetSetupStartButton), DatasetSetupStartButton);
            RegisterLearningWorkflowName(nameof(DatasetOpenExistingButton), DatasetOpenExistingButton);
            RegisterLearningWorkflowName(nameof(DatasetSetupStatusText), DatasetSetupStatusText);
            RegisterLearningWorkflowName(nameof(CurrentWorkflowActionText), CurrentWorkflowActionText);
            RegisterLearningWorkflowName(nameof(LearningModeListBox), LearningModeListBox);
            RegisterLearningWorkflowName(nameof(AnnotationToolListBox), AnnotationToolListBox);
            RegisterLearningWorkflowName(nameof(LearningStepListBox), LearningStepListBox);
            RegisterLearningWorkflowName(nameof(LearningConceptsExpander), LearningConceptsExpander);
            RegisterLearningWorkflowName(nameof(GroundTruthChipText), GroundTruthChipText);
            RegisterLearningWorkflowName(nameof(PredictionChipText), PredictionChipText);
            RegisterLearningWorkflowName(nameof(YoloTrainingWorkflowItemsControl), YoloTrainingWorkflowItemsControl);
            RegisterLearningWorkflowName(nameof(YoloCurrentTrainingProgressItemsControl), YoloCurrentTrainingProgressItemsControl);
            RegisterLearningWorkflowName(nameof(YoloTrainingWorkflowSummaryText), YoloTrainingWorkflowSummaryText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistStatusText), YoloTrainingChecklistStatusText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistDetailText), YoloTrainingChecklistDetailText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistActionText), YoloTrainingChecklistActionText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardStatusText), DatasetDashboardStatusText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardSummaryText), DatasetDashboardSummaryText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardActionText), DatasetDashboardActionText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardMetricItemsControl), DatasetDashboardMetricItemsControl);
            RegisterLearningWorkflowName(nameof(DatasetDashboardIssueItemsControl), DatasetDashboardIssueItemsControl);
            RegisterLearningWorkflowName(nameof(YoloTrainingHistoryText), YoloTrainingHistoryText);
            RegisterLearningWorkflowName(nameof(YoloTrainingRunHistoryItemsControl), YoloTrainingRunHistoryItemsControl);
            RegisterLearningWorkflowName(nameof(YoloRunModelComparisonButton), YoloRunModelComparisonButton);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowPanel), TemplateWorkflowPanel);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowTitleText), TemplateWorkflowTitleText);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowSummaryText), TemplateWorkflowSummaryText);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowItemsControl), TemplateWorkflowItemsControl);
            RegisterLearningWorkflowName(nameof(TemplateCurrentImageGuideButton), TemplateCurrentImageGuideButton);
            RegisterLearningWorkflowName(nameof(TemplateBatchGuideButton), TemplateBatchGuideButton);
            RegisterLearningWorkflowName(nameof(TutorialOpenHtmlGuideButton), TutorialOpenHtmlGuideButton);
            RegisterLearningWorkflowName(nameof(YoloFixClassesButton), YoloFixClassesButton);
            RegisterLearningWorkflowName(nameof(YoloFixLabelsButton), YoloFixLabelsButton);
            RegisterLearningWorkflowName(nameof(YoloFixDatasetButton), YoloFixDatasetButton);
            RegisterLearningWorkflowName(nameof(YoloExternalEvaluationAuditButton), YoloExternalEvaluationAuditButton);
            RegisterLearningWorkflowName(nameof(YoloExternalEvaluationAuditStatusText), YoloExternalEvaluationAuditStatusText);
            RegisterLearningWorkflowName(nameof(YoloExternalEvaluationAuditDetailText), YoloExternalEvaluationAuditDetailText);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetSelectButton), YoloExternalYoloDatasetSelectButton);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetActivateButton), YoloExternalYoloDatasetActivateButton);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetClearButton), YoloExternalYoloDatasetClearButton);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetStatusText), YoloExternalYoloDatasetStatusText);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetDetailText), YoloExternalYoloDatasetDetailText);
        }

        private void RegisterLearningWorkflowName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }
        #endregion

        #region PanelWiring.ReviewPanels
        // Object and candidate review wiring is grouped because both panels operate on the current label selection.
        private void ConfigureObjectReviewPanelCommands()
        {
            ObjectReviewViewModel.ConfigureCommands(
                ExecuteDeleteObjectCommand,
                ExecuteApplyObjectClassCommand,
                ExecuteMarkQualityUnreviewedCommand,
                ExecuteMarkQualityNeedsFixCommand,
                ExecuteMarkQualityReviewedCommand,
                ExecuteExportQualityReviewReportCommand,
                ExecuteObjectSelectionChangedCommand,
                ExecuteObjectPreviewKeyDownCommand,
                ExecuteMergeSelectedSegmentsCommand,
                mergeSelectionChanged: null,
                beginVerticalSplit: ExecuteBeginVerticalSegmentationSplitCommand,
                beginHorizontalSplit: ExecuteBeginHorizontalSegmentationSplitCommand,
                cancelSplit: ExecuteCancelSegmentationSplitCommand,
                beginAddHole: ExecuteBeginAddSegmentationHoleCommand,
                beginRemoveHole: ExecuteBeginRemoveSegmentationHoleCommand,
                cancelHoleEdit: ExecuteCancelSegmentationHoleEditCommand,
                beginInsertVertex: ExecuteBeginInsertPolygonVertexCommand,
                beginDeleteVertex: ExecuteBeginDeletePolygonVertexCommand,
                cancelVertexEdit: ExecuteCancelPolygonVertexEditCommand,
                beginIntelligentScissors: ExecuteBeginIntelligentScissorsCommand,
                applyIntelligentScissors: ExecuteApplyIntelligentScissorsCommand,
                cancelIntelligentScissors: ExecuteCancelIntelligentScissorsCommand,
                sendToBack: ExecuteSendSegmentationToBackCommand,
                sendBackward: ExecuteSendSegmentationBackwardCommand,
                bringForward: ExecuteBringSegmentationForwardCommand,
                bringToFront: ExecuteBringSegmentationToFrontCommand,
                previewRemoveUnderlying: ExecutePreviewSegmentationRemoveUnderlyingCommand,
                applyRemoveUnderlying: ExecuteApplySegmentationRemoveUnderlyingCommand,
                cancelRemoveUnderlying: ExecuteCancelSegmentationRemoveUnderlyingCommand,
                toggleObjectHidden: ExecuteToggleObjectHiddenCommand,
                toggleObjectLocked: ExecuteToggleObjectLockedCommand,
                toggleObjectPinned: ExecuteToggleObjectPinnedCommand,
                togglePersistentOccluded: ExecuteTogglePersistentOccludedCommand,
                togglePersistentTag: ExecuteTogglePersistentTagCommand,
                resetRecipeMetadataTags: ExecuteResetRecipeMetadataTagsCommand,
                beginGroupSelection: ExecuteBeginObjectGroupSelectionCommand,
                cancelGroupSelection: ExecuteCancelObjectGroupSelectionCommand,
                createGroup: ExecuteCreateObjectGroupCommand,
                groupSelectionChanged: ExecuteObjectGroupSelectionChangedCommand,
                removeSelectedFromGroup: ExecuteRemoveSelectedObjectFromGroupCommand,
                dissolveSelectedGroup: ExecuteDissolveSelectedObjectGroupCommand,
                toggleGroupOccluded: ExecuteToggleObjectGroupOccludedCommand,
                toggleGroupTag: ExecuteToggleObjectGroupTagCommand);
            RefreshAttachedCommandBindings(
                ObjectListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }

        private void RegisterObjectReviewPanelNames()
        {
            ConfigureObjectReviewPanelCommands();
            RegisterObjectReviewName(nameof(ObjectReviewSummaryText), ObjectReviewSummaryText);
            RegisterObjectReviewName(nameof(ObjectReviewLabelSaveBadge), ObjectReviewLabelSaveBadge);
            RegisterObjectReviewName(nameof(ObjectReviewLabelSaveBadgeText), ObjectReviewLabelSaveBadgeText);
            RegisterObjectReviewName(nameof(ObjectReviewLabelSaveDetailText), ObjectReviewLabelSaveDetailText);
            RegisterObjectReviewName(nameof(DeleteObjectButton), DeleteObjectButton);
            RegisterObjectReviewName(nameof(ObjectClassBox), ObjectClassBox);
            RegisterObjectReviewName(nameof(ApplyObjectClassButton), ApplyObjectClassButton);
            RegisterObjectReviewName(nameof(MergeSelectionText), MergeSelectionText);
            RegisterObjectReviewName(nameof(MergeSelectedSegmentsButton), MergeSelectedSegmentsButton);
            RegisterObjectReviewName(nameof(BeginVerticalSplitButton), BeginVerticalSplitButton);
            RegisterObjectReviewName(nameof(BeginHorizontalSplitButton), BeginHorizontalSplitButton);
            RegisterObjectReviewName(nameof(CancelSplitButton), CancelSplitButton);
            RegisterObjectReviewName(nameof(SplitStatusText), SplitStatusText);
            RegisterObjectReviewName(nameof(BeginAddHoleButton), BeginAddHoleButton);
            RegisterObjectReviewName(nameof(BeginRemoveHoleButton), BeginRemoveHoleButton);
            RegisterObjectReviewName(nameof(CancelHoleEditButton), CancelHoleEditButton);
            RegisterObjectReviewName(nameof(HoleEditStatusText), HoleEditStatusText);
            RegisterObjectReviewName(nameof(BeginInsertVertexButton), BeginInsertVertexButton);
            RegisterObjectReviewName(nameof(BeginDeleteVertexButton), BeginDeleteVertexButton);
            RegisterObjectReviewName(nameof(CancelVertexEditButton), CancelVertexEditButton);
            RegisterObjectReviewName(nameof(VertexEditStatusText), VertexEditStatusText);
            RegisterObjectReviewName(nameof(BeginIntelligentScissorsButton), BeginIntelligentScissorsButton);
            RegisterObjectReviewName(nameof(ApplyIntelligentScissorsButton), ApplyIntelligentScissorsButton);
            RegisterObjectReviewName(nameof(CancelIntelligentScissorsButton), CancelIntelligentScissorsButton);
            RegisterObjectReviewName(nameof(IntelligentScissorsStatusText), IntelligentScissorsStatusText);
            RegisterObjectReviewName(nameof(SendSegmentationToBackButton), SendSegmentationToBackButton);
            RegisterObjectReviewName(nameof(SendSegmentationBackwardButton), SendSegmentationBackwardButton);
            RegisterObjectReviewName(nameof(BringSegmentationForwardButton), BringSegmentationForwardButton);
            RegisterObjectReviewName(nameof(BringSegmentationToFrontButton), BringSegmentationToFrontButton);
            RegisterObjectReviewName(nameof(ZOrderStatusText), ZOrderStatusText);
            RegisterObjectReviewName(nameof(PreviewRemoveUnderlyingButton), PreviewRemoveUnderlyingButton);
            RegisterObjectReviewName(nameof(ApplyRemoveUnderlyingButton), ApplyRemoveUnderlyingButton);
            RegisterObjectReviewName(nameof(CancelRemoveUnderlyingButton), CancelRemoveUnderlyingButton);
            RegisterObjectReviewName(nameof(RemoveUnderlyingStatusText), RemoveUnderlyingStatusText);
            RegisterObjectReviewName(nameof(ToggleObjectHiddenButton), ToggleObjectHiddenButton);
            RegisterObjectReviewName(nameof(ToggleObjectLockedButton), ToggleObjectLockedButton);
            RegisterObjectReviewName(nameof(ToggleObjectPinnedButton), ToggleObjectPinnedButton);
            RegisterObjectReviewName(nameof(ObjectSessionStateStatusText), ObjectSessionStateStatusText);
            RegisterObjectReviewName(nameof(ObjectMetadataExpander), ObjectMetadataExpander);
            RegisterObjectReviewName(nameof(TogglePersistentOccludedButton), TogglePersistentOccludedButton);
            RegisterObjectReviewName(nameof(ObjectMetadataTagBox), ObjectMetadataTagBox);
            RegisterObjectReviewName(nameof(TogglePersistentTagButton), TogglePersistentTagButton);
            RegisterObjectReviewName(nameof(ToggleOccludedFilterButton), ToggleOccludedFilterButton);
            RegisterObjectReviewName(nameof(ObjectMetadataTagFilterBox), ObjectMetadataTagFilterBox);
            RegisterObjectReviewName(nameof(ResetObjectMetadataFilterButton), ResetObjectMetadataFilterButton);
            RegisterObjectReviewName(nameof(ResetRecipeMetadataTagsButton), ResetRecipeMetadataTagsButton);
            RegisterObjectReviewName(nameof(BeginObjectGroupSelectionButton), BeginObjectGroupSelectionButton);
            RegisterObjectReviewName(nameof(CreateObjectGroupButton), CreateObjectGroupButton);
            RegisterObjectReviewName(nameof(CancelObjectGroupSelectionButton), CancelObjectGroupSelectionButton);
            RegisterObjectReviewName(nameof(ObjectGroupSelectionStatusText), ObjectGroupSelectionStatusText);
            RegisterObjectReviewName(nameof(SelectedObjectGroupText), SelectedObjectGroupText);
            RegisterObjectReviewName(nameof(RemoveObjectFromGroupButton), RemoveObjectFromGroupButton);
            RegisterObjectReviewName(nameof(DissolveObjectGroupButton), DissolveObjectGroupButton);
            RegisterObjectReviewName(nameof(ToggleObjectGroupOccludedButton), ToggleObjectGroupOccludedButton);
            RegisterObjectReviewName(nameof(ToggleObjectGroupTagButton), ToggleObjectGroupTagButton);
            RegisterObjectReviewName(nameof(ObjectGroupFilterBox), ObjectGroupFilterBox);
            RegisterObjectReviewName(nameof(ObjectQualityReviewExpander), ObjectQualityReviewExpander);
            RegisterObjectReviewName(nameof(SegmentationAdvancedEditExpander), SegmentationAdvancedEditExpander);
            RegisterObjectReviewName(nameof(ObjectListBox), ObjectListBox);
        }

        private void RegisterObjectReviewName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureCandidateReviewPanelCommands()
        {
            CandidateReviewViewModel.ConfigureCommands(
                ExecuteCandidateConfidenceChangedCommand,
                ExecuteConfirmSelectedCandidateCommand,
                ExecuteConfirmAllCandidatesCommand,
                ExecuteSkipSelectedCandidateCommand,
                ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand,
                ExecuteFocusCandidateCommand,
                ExecuteFocusCurrentLabelCommand,
                ExecuteCandidateSelectionChangedCommand,
                ExecuteCandidatePreviewKeyDownCommand,
                ExecuteCompleteImageAndNextCommand,
                ExecuteOpenModelComparisonExampleCommand,
                ExecuteSaveModelCandidateCommand,
                ExecuteRejectModelCandidateCommand,
                ExecuteModelComparisonHistorySelectionChangedCommand,
                ExecuteTogglePatchCoreHeatmapCommand);
            RefreshAttachedCommandBindings(CandidateConfidenceSlider, InputCommandBehaviors.ValueInputCommandProperty);
            RefreshAttachedCommandBindings(
                CandidateListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }

        private void RegisterCandidateReviewPanelNames()
        {
            ConfigureCandidateReviewPanelCommands();
            RegisterCandidateReviewName(nameof(CandidateConfidenceSlider), CandidateConfidenceSlider);
            RegisterCandidateReviewName(nameof(CandidateReviewRoleSplitPanel), CandidateReviewRoleSplitPanel);
            RegisterCandidateReviewName(nameof(CurrentImageCandidateRoleCard), CurrentImageCandidateRoleCard);
            RegisterCandidateReviewName(nameof(ModelValidationRoleCard), ModelValidationRoleCard);
            RegisterCandidateReviewName(nameof(CurrentImageReviewRoleTitleText), CurrentImageReviewRoleTitleText);
            RegisterCandidateReviewName(nameof(CurrentImageReviewRoleDetailText), CurrentImageReviewRoleDetailText);
            RegisterCandidateReviewName(nameof(CurrentImageReviewRoleResultText), CurrentImageReviewRoleResultText);
            RegisterCandidateReviewName(nameof(ModelValidationRoleTitleText), ModelValidationRoleTitleText);
            RegisterCandidateReviewName(nameof(ModelValidationRoleDetailText), ModelValidationRoleDetailText);
            RegisterCandidateReviewName(nameof(ModelValidationRoleResultText), ModelValidationRoleResultText);
            RegisterCandidateReviewName(nameof(ModelCandidateDecisionPanel), ModelCandidateDecisionPanel);
            RegisterCandidateReviewName(nameof(ModelCandidateDecisionStatusText), ModelCandidateDecisionStatusText);
            RegisterCandidateReviewName(nameof(ModelCandidateDecisionDetailText), ModelCandidateDecisionDetailText);
            RegisterCandidateReviewName(nameof(SaveModelCandidateButton), SaveModelCandidateButton);
            RegisterCandidateReviewName(nameof(RejectModelCandidateButton), RejectModelCandidateButton);
            RegisterCandidateReviewName(nameof(CandidateConfidenceText), CandidateConfidenceText);
            RegisterCandidateReviewName(nameof(CandidateDetailText), CandidateDetailText);
            RegisterCandidateReviewName(nameof(SelectedCandidateSummaryPanel), SelectedCandidateSummaryPanel);
            RegisterCandidateReviewName(nameof(SelectedCandidateSummaryText), SelectedCandidateSummaryText);
            RegisterCandidateReviewName(nameof(CandidateComparisonPanel), CandidateComparisonPanel);
            RegisterCandidateReviewName(nameof(CandidateCompareCandidateText), CandidateCompareCandidateText);
            RegisterCandidateReviewName(nameof(CandidateCompareCurrentText), CandidateCompareCurrentText);
            RegisterCandidateReviewName(nameof(CandidateCompareOverlapText), CandidateCompareOverlapText);
            RegisterCandidateReviewName(nameof(CandidateCompareDecisionText), CandidateCompareDecisionText);
            RegisterCandidateReviewName(nameof(ConfirmSelectedCandidateButton), ConfirmSelectedCandidateButton);
            RegisterCandidateReviewName(nameof(ConfirmAllCandidatesButton), ConfirmAllCandidatesButton);
            RegisterCandidateReviewName(nameof(SkipSelectedCandidateButton), SkipSelectedCandidateButton);
            RegisterCandidateReviewName(nameof(CompleteImageAndNextButton), CompleteImageAndNextButton);
            RegisterCandidateReviewName(nameof(PreviousCandidateButton), PreviousCandidateButton);
            RegisterCandidateReviewName(nameof(NextCandidateButton), NextCandidateButton);
            RegisterCandidateReviewName(nameof(FocusCandidateButton), FocusCandidateButton);
            RegisterCandidateReviewName(nameof(FocusCurrentLabelButton), FocusCurrentLabelButton);
            RegisterCandidateReviewName(nameof(CandidateListBox), CandidateListBox);
        }

        private void RegisterCandidateReviewName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }
        #endregion

        #region PanelWiring.SettingsPanels
        // Settings/status panel wiring is kept apart from workflow wiring to make configuration commands easier to audit.
        private void ConfigureClassCatalogPanelCommands()
        {
            ClassCatalogViewModel.ConfigureCommands(
                args => ClassNameBox_KeyDown(ClassNameBox, args),
                ExecuteAddClassCommand,
                ExecuteRenameClassCommand,
                ExecuteArchiveClassCommand,
                ExecuteApplyClassColorCommand,
                selected => ClassListBox_SelectionChanged(ClassListBox, selected));
            RefreshAttachedCommandBindings(ClassNameBox, InputCommandBehaviors.PreviewKeyInputCommandProperty);
            RefreshAttachedCommandBindings(ClassListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterClassCatalogPanelNames()
        {
            ConfigureClassCatalogPanelCommands();
            RegisterClassCatalogName(nameof(ClassCatalogGuidePanel), ClassCatalogGuidePanel);
            RegisterClassCatalogName(nameof(ClassCatalogGuideTitleText), ClassCatalogGuideTitleText);
            RegisterClassCatalogName(nameof(ClassCatalogGuideDetailText), ClassCatalogGuideDetailText);
            RegisterClassCatalogName(nameof(ClassCatalogSummaryText), ClassCatalogSummaryText);
            RegisterClassCatalogName(nameof(CurrentDrawingClassTitleText), CurrentDrawingClassTitleText);
            RegisterClassCatalogName(nameof(CurrentDrawingClassDetailText), CurrentDrawingClassDetailText);
            RegisterClassCatalogName(nameof(ClassCatalogActionText), ClassCatalogActionText);
            RegisterClassCatalogName(nameof(ClassSectionLabelText), ClassSectionLabelText);
            RegisterClassCatalogName(nameof(ClassNameBox), ClassNameBox);
            RegisterClassCatalogName(nameof(AddClassButton), AddClassButton);
            RegisterClassCatalogName(nameof(RenameClassButton), RenameClassButton);
            RegisterClassCatalogName(nameof(RemoveClassButton), RemoveClassButton);
            RegisterClassCatalogName(nameof(ClassColorBox), ClassColorBox);
            RegisterClassCatalogName(nameof(ClassColorAdvancedPanel), ClassColorAdvancedPanel);
            RegisterClassCatalogName(nameof(ApplyClassColorButton), ApplyClassColorButton);
            RegisterClassCatalogName(nameof(ClassEditStatusText), ClassEditStatusText);
            RegisterClassCatalogName(nameof(ClassListBox), ClassListBox);
        }

        private void RegisterClassCatalogName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureYoloStatusPanelCommands()
        {
            YoloStatusViewModel.ConfigureCommands(
                ExecuteCheckYoloCommand,
                ExecuteInstallRequirementsCommand,
                ExecuteRunYoloSmokeCommand,
                ExecuteRestartPythonWorkerCommand,
                ExecuteStopPythonWorkerCommand);
        }

        private void RegisterYoloStatusPanelNames()
        {
            ConfigureYoloStatusPanelCommands();
            RegisterYoloStatusName(nameof(YoloSettingsSummaryText), YoloSettingsSummaryText);
            RegisterYoloStatusName(nameof(YoloRuntimeDetailsExpander), YoloRuntimeDetailsExpander);
            RegisterYoloStatusName(nameof(YoloSettingsDetailText), YoloSettingsDetailText);
            RegisterYoloStatusName(nameof(FirstCheckYoloButton), FirstCheckYoloButton);
            RegisterYoloStatusName(nameof(InstallRequirementsButton), InstallRequirementsButton);
            RegisterYoloStatusName(nameof(RunYoloSmokeButton), RunYoloSmokeButton);
            RegisterYoloStatusName(nameof(RestartPythonWorkerButton), RestartPythonWorkerButton);
            RegisterYoloStatusName(nameof(StopPythonWorkerButton), StopPythonWorkerButton);
            RegisterYoloStatusName(nameof(YoloCommandStatusText), YoloCommandStatusText);
            RegisterYoloStatusName(nameof(YoloCommandProgressBar), YoloCommandProgressBar);
        }

        private void RegisterYoloStatusName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureProjectConfigPanelCommands()
        {
            ProjectConfigViewModel.ConfigureCommands(
                ExecuteApplyProjectRecipeCommand,
                ExecuteRefreshProjectRecipeListCommand,
                ExecuteSaveProjectConfigCommand,
                ExecuteOpenProjectConfigFolderCommand,
                ExecuteExportProjectArchiveCommand,
                ExecuteImportProjectArchiveCommand,
                selected => ProjectRecipeListBox_SelectionChanged(ProjectRecipeListBox, selected));
            RefreshAttachedCommandBindings(ProjectRecipeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterProjectConfigPanelNames()
        {
            ConfigureProjectConfigPanelCommands();
            RegisterProjectConfigName(nameof(ProjectConfigExpander), ProjectConfigExpander);
            RegisterProjectConfigName(nameof(ProjectRecipeNameBox), ProjectRecipeNameBox);
            RegisterProjectConfigName(nameof(ProjectRecipeListBox), ProjectRecipeListBox);
            RegisterProjectConfigName(nameof(ProjectConfigPathBox), ProjectConfigPathBox);
            RegisterProjectConfigName(nameof(ProjectManifestPathBox), ProjectManifestPathBox);
            RegisterProjectConfigName(nameof(ProjectDatasetVersionBox), ProjectDatasetVersionBox);
            RegisterProjectConfigName(nameof(ProjectDatasetVersionDetailBox), ProjectDatasetVersionDetailBox);
            RegisterProjectConfigName(nameof(ProjectConfigStatusText), ProjectConfigStatusText);
            RegisterProjectConfigName(nameof(ApplyProjectRecipeButton), ApplyProjectRecipeButton);
            RegisterProjectConfigName(nameof(RefreshProjectRecipeListButton), RefreshProjectRecipeListButton);
            RegisterProjectConfigName(nameof(SaveProjectConfigButton), SaveProjectConfigButton);
            RegisterProjectConfigName(nameof(OpenProjectConfigFolderButton), OpenProjectConfigFolderButton);
        }

        private void RegisterProjectConfigName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureYoloModelSettingsPanelCommands()
        {
            YoloModelSettingsViewModel.ConfigureCommands(
                ExecuteBrowseYoloPythonCommand,
                ExecuteBrowseYoloProjectRootCommand,
                ExecuteBrowseYoloClientScriptCommand,
                ExecuteBrowseYoloWeightsCommand,
                ExecuteBrowseYoloImageRootCommand,
                ExecuteSaveYoloSettingsCommand,
                ExecuteResetYoloSettingsCommand,
                ExecuteRuntimeProfileActionCommand,
                ExecuteInstallUltralyticsPackageCommand,
                ExecuteUninstallUltralyticsPackageCommand,
                PopulateYoloEditorFields);
        }

        private void RegisterYoloModelSettingsPanelNames()
        {
            ConfigureYoloModelSettingsPanelCommands();
            RegisterYoloModelSettingsName(nameof(YoloInspectionModelQuickPanel), YoloInspectionModelQuickPanel);
            RegisterYoloModelSettingsName(nameof(YoloPythonPathBox), YoloPythonPathBox);
            RegisterYoloModelSettingsName(nameof(YoloModelEngineBox), YoloModelEngineBox);
            RegisterYoloModelSettingsName(nameof(YoloProjectRootBox), YoloProjectRootBox);
            RegisterYoloModelSettingsName(nameof(YoloClientScriptBox), YoloClientScriptBox);
            RegisterYoloModelSettingsName(nameof(YoloWeightsPathBox), YoloWeightsPathBox);
            RegisterYoloModelSettingsName(nameof(YoloImageRootBox), YoloImageRootBox);
            RegisterYoloModelSettingsName(nameof(YoloConfidenceBox), YoloConfidenceBox);
            RegisterYoloModelSettingsName(nameof(YoloInferenceImageSizeBox), YoloInferenceImageSizeBox);
            RegisterYoloModelSettingsName(nameof(YoloMaxCandidatesBox), YoloMaxCandidatesBox);
            RegisterYoloModelSettingsName(nameof(YoloTimeoutBox), YoloTimeoutBox);
            RegisterYoloModelSettingsName(nameof(YoloAutoStartCheckBox), YoloAutoStartCheckBox);
            RegisterYoloModelSettingsName(nameof(BrowseYoloPythonButton), BrowseYoloPythonButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloProjectRootButton), BrowseYoloProjectRootButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloClientScriptButton), BrowseYoloClientScriptButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloWeightsButton), BrowseYoloWeightsButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloImageRootButton), BrowseYoloImageRootButton);
            RegisterYoloModelSettingsName(nameof(SaveYoloSettingsButton), SaveYoloSettingsButton);
            RegisterYoloModelSettingsName(nameof(ResetYoloSettingsButton), ResetYoloSettingsButton);
            RegisterYoloModelSettingsName(nameof(YoloRuntimeInstallPackageButton), YoloRuntimeInstallPackageButton);
            RegisterYoloModelSettingsName(nameof(YoloRuntimeUninstallPackageButton), YoloRuntimeUninstallPackageButton);
        }

        private void RegisterYoloModelSettingsName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureTrainingSettingsPanelCommands()
        {
            TrainingSettingsViewModel.ConfigureCommands(
                ExecuteRefreshTrainingReadinessCommand,
                ExecuteStartTrainingCommand,
                ExecuteStopTrainingCommand,
                ExecuteReviewCandidateModelCommand,
                ExecuteSaveYoloSettingsCommand,
                ExecuteRunYoloEngineComparisonCommand,
                ExecuteBrowseSegmentationUnetCheckpointCommand,
                ExecuteBrowseSegmentationYoloCheckpointCommand,
                ExecuteRunSegmentationAdapterComparisonCommand);
        }

        private void RegisterTrainingSettingsPanelNames()
        {
            ConfigureTrainingSettingsPanelCommands();
            RegisterTrainingSettingsName(nameof(TrainingSettingsExpander), TrainingSettingsExpander);
            RegisterTrainingSettingsName(nameof(PostTrainingModelActionPanel), PostTrainingModelActionPanel);
            RegisterTrainingSettingsName(nameof(PostTrainingModelStatusText), PostTrainingModelStatusText);
            RegisterTrainingSettingsName(nameof(PostTrainingModelDetailText), PostTrainingModelDetailText);
            RegisterTrainingSettingsName(nameof(ReviewTrainedModelButton), ReviewTrainedModelButton);
            RegisterTrainingSettingsName(nameof(ConfirmTrainedModelButton), ConfirmTrainedModelButton);
            RegisterTrainingSettingsName(nameof(RunYoloEngineComparisonButton), RunYoloEngineComparisonButton);
            RegisterTrainingSettingsName(nameof(SegmentationAdapterComparisonPanel), SegmentationAdapterComparisonPanel);
            RegisterTrainingSettingsName(nameof(SegmentationUnetCheckpointPathBox), SegmentationUnetCheckpointPathBox);
            RegisterTrainingSettingsName(nameof(SegmentationYoloCheckpointPathBox), SegmentationYoloCheckpointPathBox);
            RegisterTrainingSettingsName(nameof(BrowseSegmentationUnetCheckpointButton), BrowseSegmentationUnetCheckpointButton);
            RegisterTrainingSettingsName(nameof(BrowseSegmentationYoloCheckpointButton), BrowseSegmentationYoloCheckpointButton);
            RegisterTrainingSettingsName(nameof(RunSegmentationAdapterComparisonButton), RunSegmentationAdapterComparisonButton);
            RegisterTrainingSettingsName(nameof(TrainingImageSizeBox), TrainingImageSizeBox);
            RegisterTrainingSettingsName(nameof(TrainingBatchBox), TrainingBatchBox);
            RegisterTrainingSettingsName(nameof(TrainingEpochBox), TrainingEpochBox);
            RegisterTrainingSettingsName(nameof(TrainingCfgBox), TrainingCfgBox);
            RegisterTrainingSettingsName(nameof(TrainingWeightBox), TrainingWeightBox);
            RegisterTrainingSettingsName(nameof(TrainingValidationPercentBox), TrainingValidationPercentBox);
            RegisterTrainingSettingsName(nameof(TrainingTestPercentBox), TrainingTestPercentBox);
            RegisterTrainingSettingsName(nameof(TrainingSplitSeedBox), TrainingSplitSeedBox);
            RegisterTrainingSettingsName(nameof(TrainingSplitPolicyHintText), TrainingSplitPolicyHintText);
            RegisterTrainingSettingsName(nameof(ApplyFastTrainingPresetButton), ApplyFastTrainingPresetButton);
            RegisterTrainingSettingsName(nameof(RefreshTrainingReadinessButton), RefreshTrainingReadinessButton);
            RegisterTrainingSettingsName(nameof(StartTrainingButton), StartTrainingButton);
            RegisterTrainingSettingsName(nameof(StopTrainingButton), StopTrainingButton);
            RegisterTrainingSettingsName(nameof(TrainingReadinessText), TrainingReadinessText);
            RegisterTrainingSettingsName(nameof(TrainingProgressBar), TrainingProgressBar);
            RegisterTrainingSettingsName(nameof(TrainingProgressText), TrainingProgressText);
            RegisterTrainingSettingsName(nameof(TrainingEpochText), TrainingEpochText);
        }

        private void RegisterTrainingSettingsName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void RegisterStatusBarPanelNames()
        {
            RegisterStatusBarName(nameof(DatasetStatusText), DatasetStatusText);
            RegisterStatusBarName(nameof(WorkflowStageText), WorkflowStageText);
            RegisterStatusBarName(nameof(WorkflowProgressText), WorkflowProgressText);
            RegisterStatusBarName(nameof(WorkflowNextActionText), WorkflowNextActionText);
            RegisterStatusBarName(nameof(PythonStatusText), PythonStatusText);
            RegisterStatusBarName(nameof(AnnotationSaveStatusText), AnnotationSaveStatusText);
            RegisterStatusBarName(nameof(InspectionModelStatusText), InspectionModelStatusText);
            RegisterStatusBarName(nameof(ModelStatusText), ModelStatusText);
        }

        private void RegisterStatusBarName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void RegisterShellLogPanelNames()
        {
            RegisterShellLogName(nameof(ShellLogPanel), ShellLogPanel);
        }

        private void RegisterShellLogName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }

        }
        #endregion

    }
}
