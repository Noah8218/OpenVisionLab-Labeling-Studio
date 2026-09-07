using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.Views;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using TextBox = System.Windows.Controls.TextBox;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows.Data;

namespace MvcVisionSystem
{
    // Responsibility group: panel composition and UI accessor registration.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region PanelAccessors
        // UI element proxies are centralized while panels finish moving to pure ViewModel bindings; this keeps legacy name lookups out of command logic.
        // The shell is the composition root: keep UserControls ViewModel-free and wire panel commands here.
        public WpfLabelingShellViewModel ShellViewModel => viewModels.ShellViewModel;

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => viewModels.LearningWorkflowViewModel;

        public WpfImageQueuePanelViewModel ImageQueueViewModel => viewModels.ImageQueueViewModel;

        public WpfTemplateMatchingAutoLabelViewModel TemplateMatchingAutoLabelViewModel => viewModels.TemplateMatchingAutoLabelViewModel;

        public WpfCanvasPanelViewModel CanvasPanelViewModel => viewModels.CanvasPanelViewModel;

        public WpfObjectReviewPanelViewModel ObjectReviewViewModel => viewModels.ObjectReviewViewModel;

        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.CandidateReviewViewModel;
            }
        }

        public WpfCandidateReviewPanelViewModel ExistingCandidateReviewViewModel =>
            viewModels.CandidateReviewViewModel;

        public WpfClassCatalogPanelViewModel ClassCatalogViewModel => viewModels.ClassCatalogViewModel;

        public WpfYoloStatusPanelViewModel YoloStatusViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.YoloStatusViewModel;
            }
        }

        public WpfYoloStatusPanelViewModel ExistingYoloStatusViewModel =>
            viewModels.YoloStatusViewModel;

        public WpfProjectConfigPanelViewModel ProjectConfigViewModel => viewModels.ProjectConfigViewModel;

        public WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.YoloModelSettingsViewModel;
            }
        }

        public WpfYoloModelSettingsPanelViewModel ExistingYoloModelSettingsViewModel =>
            viewModels.YoloModelSettingsViewModel;

        public WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.TrainingSettingsViewModel;
            }
        }

        public WpfTrainingSettingsPanelViewModel ExistingTrainingSettingsViewModel =>
            viewModels.TrainingSettingsViewModel;

        public WpfStatusBarPanelViewModel StatusBarViewModel => viewModels.StatusBarViewModel;

        public WpfShellLogPanelViewModel ShellLogViewModel => viewModels.ShellLogViewModel;

        public WpfRuntimeDiagnosticsViewModel RuntimeDiagnosticsViewModel => viewModels.RuntimeDiagnosticsViewModel;

        public RoiImageCanvasViewModel MainCanvasViewModel => viewModels.MainCanvasViewModel;

        private ListBox DatasetPurposeListBox => LearningWorkflowPanelControl?.DatasetPurposeList;
        private TextBlock DatasetPurposeSummaryText => LearningWorkflowPanelControl?.DatasetPurposeSummary;
        private TextBlock DatasetPurposeToolSummaryText => LearningWorkflowPanelControl?.DatasetPurposeToolSummary;
        private Border FirstRunSamplePathPanel => LearningWorkflowPanelControl?.FirstRunSamplePathPanelControl;
        private TextBlock FirstRunSamplePathTitleText => LearningWorkflowPanelControl?.FirstRunSamplePathTitle;
        private TextBlock FirstRunSamplePathSummaryText => LearningWorkflowPanelControl?.FirstRunSamplePathSummary;
        private TextBlock FirstRunSamplePathPrimaryActionText => LearningWorkflowPanelControl?.FirstRunSamplePathPrimaryAction;
        private ItemsControl FirstRunSamplePathItemsControl => LearningWorkflowPanelControl?.FirstRunSamplePathList;
        private Button DatasetSetupStartButton => LearningWorkflowPanelControl?.DatasetSetupStart;
        private Button DatasetOpenExistingButton => LearningWorkflowPanelControl?.DatasetOpenExisting;
        private TextBlock DatasetSetupStatusText => LearningWorkflowPanelControl?.DatasetSetupStatus;
        private TextBlock CurrentWorkflowActionText => LearningWorkflowPanelControl?.CurrentWorkflowAction;
        private ListBox LearningModeListBox => LearningWorkflowPanelControl?.ModeList;
        private ListBox AnnotationToolListBox => LearningWorkflowPanelControl?.ToolList;
        private ListBox LearningStepListBox => LearningWorkflowPanelControl?.StepList;
        private Expander LearningConceptsExpander => LearningWorkflowPanelControl?.LearningConcepts;
        private TextBlock GroundTruthChipText => LearningWorkflowPanelControl?.GroundTruthTextBlock;
        private TextBlock PredictionChipText => LearningWorkflowPanelControl?.PredictionTextBlock;
        private ItemsControl YoloTrainingWorkflowItemsControl => LearningWorkflowPanelControl?.YoloTrainingWorkflowList;
        private ItemsControl YoloCurrentTrainingProgressItemsControl => LearningWorkflowPanelControl?.YoloCurrentTrainingProgressList;
        private TextBlock YoloTrainingWorkflowSummaryText => LearningWorkflowPanelControl?.YoloTrainingWorkflowSummary;
        private TextBlock YoloTrainingChecklistStatusText => LearningWorkflowPanelControl?.YoloTrainingChecklistStatus;
        private TextBlock YoloTrainingChecklistDetailText => LearningWorkflowPanelControl?.YoloTrainingChecklistDetail;
        private TextBlock YoloTrainingChecklistActionText => LearningWorkflowPanelControl?.YoloTrainingChecklistAction;
        private TextBlock DatasetDashboardStatusText => LearningWorkflowPanelControl?.DatasetDashboardStatus;
        private TextBlock DatasetDashboardSummaryText => LearningWorkflowPanelControl?.DatasetDashboardSummary;
        private TextBlock DatasetDashboardActionText => LearningWorkflowPanelControl?.DatasetDashboardAction;
        private ItemsControl DatasetDashboardMetricItemsControl => LearningWorkflowPanelControl?.DatasetDashboardMetricList;
        private ItemsControl DatasetDashboardIssueItemsControl => LearningWorkflowPanelControl?.DatasetDashboardIssueList;
        private TextBlock YoloTrainingHistoryText => LearningWorkflowPanelControl?.YoloTrainingHistory;
        private Button YoloRunModelComparisonButton => LearningWorkflowPanelControl?.YoloRunModelComparison;
        private ItemsControl YoloTrainingRunHistoryItemsControl => LearningWorkflowPanelControl?.YoloTrainingRunHistoryList;
        private Border TemplateWorkflowPanel => LearningWorkflowPanelControl?.TemplateWorkflow;
        private TextBlock TemplateWorkflowTitleText => LearningWorkflowPanelControl?.TemplateWorkflowTitle;
        private TextBlock TemplateWorkflowSummaryText => LearningWorkflowPanelControl?.TemplateWorkflowSummary;
        private ItemsControl TemplateWorkflowItemsControl => LearningWorkflowPanelControl?.TemplateWorkflowStepList;
        private Button TemplateCurrentImageGuideButton => LearningWorkflowPanelControl?.TemplateCurrentImage;
        private Button TemplateBatchGuideButton => LearningWorkflowPanelControl?.TemplateBatch;
        private Button YoloFixClassesButton => LearningWorkflowPanelControl?.YoloFixClasses;
        private Button YoloFixLabelsButton => LearningWorkflowPanelControl?.YoloFixLabels;
        private Button YoloFixDatasetButton => LearningWorkflowPanelControl?.YoloFixDataset;
        private Button TutorialOpenHtmlGuideButton => LearningWorkflowPanelControl?.TutorialOpenHtmlGuide;
        private ComboBox ImageQueueFilterBox => ImageQueuePanelControl?.FilterBox;
        private TextBox ImageQueueSearchBox => ImageQueuePanelControl?.SearchBox;
        private DataGrid ImageQueueGrid => ImageQueuePanelControl?.QueueGrid;
        private TextBlock BatchStatusText => ImageQueuePanelControl?.BatchStatusTextBlock;
        private ProgressBar BatchProgressBar => ImageQueuePanelControl?.BatchProgress;
        private TextBlock CurrentImageFolderPathText => ImageQueuePanelControl?.CurrentFolderPathTextBlock;
        private Wpf.Ui.Controls.Button OpenCurrentImageFolderButton => ImageQueuePanelControl?.OpenCurrentFolderButton;
        private Wpf.Ui.Controls.Button OpenSelectedQueueImageButton => ImageQueuePanelControl?.OpenSelectedButton;
        private Wpf.Ui.Controls.Button DetectSelectedQueueButton => ImageQueuePanelControl?.DetectSelectedButton;
        private Wpf.Ui.Controls.Button BatchDetectQueueButton => ImageQueuePanelControl?.BatchDetectButton;
        private Wpf.Ui.Controls.Button TemplateBatchQueueButton => ImageQueuePanelControl?.TemplateBatchButton;
        private Wpf.Ui.Controls.Button RetryFailedQueueButton => ImageQueuePanelControl?.RetryFailedButton;
        private Wpf.Ui.Controls.Button StopBatchQueueButton => ImageQueuePanelControl?.StopBatchButton;
        private Wpf.Ui.Controls.Button QueueFilterUnfinishedButton => ImageQueuePanelControl?.QueueFilterUnfinished;
        private Wpf.Ui.Controls.Button QueueFilterAllButton => ImageQueuePanelControl?.QueueFilterAll;
        private Wpf.Ui.Controls.Button QueueFilterCandidateButton => ImageQueuePanelControl?.QueueFilterCandidate;
        private Wpf.Ui.Controls.Button QueueFilterFailedButton => ImageQueuePanelControl?.QueueFilterFailed;
        private Wpf.Ui.Controls.Button QueueFilterConfirmedButton => ImageQueuePanelControl?.QueueFilterConfirmed;
        private Wpf.Ui.Controls.Button QueueFilterSkippedButton => ImageQueuePanelControl?.QueueFilterSkipped;
        private Wpf.Ui.Controls.Button QueueFilterNoCandidateButton => ImageQueuePanelControl?.QueueFilterNoCandidate;
        private TextBlock QueueFilterUnfinishedText => ImageQueuePanelControl?.QueueFilterUnfinishedTextBlock;
        private TextBlock ImageQueuePanelTitleText => ImageQueuePanelControl?.PanelTitleTextBlock;
        private TextBlock QueueFilterAllText => ImageQueuePanelControl?.QueueFilterAllTextBlock;
        private TextBlock QueueFilterCandidateText => ImageQueuePanelControl?.QueueFilterCandidateTextBlock;
        private TextBlock QueueFilterFailedText => ImageQueuePanelControl?.QueueFilterFailedTextBlock;
        private TextBlock QueueFilterConfirmedText => ImageQueuePanelControl?.QueueFilterConfirmedTextBlock;
        private TextBlock QueueFilterSkippedText => ImageQueuePanelControl?.QueueFilterSkippedTextBlock;
        private TextBlock QueueFilterNoCandidateText => ImageQueuePanelControl?.QueueFilterNoCandidateTextBlock;
        private RoiImageCanvasView MainCanvasView => CanvasPanelControl?.MainCanvas;
        private ListBox CanvasAnnotationToolListBox => CanvasPanelControl?.AnnotationToolList;
        private ListBox CanvasLabelClassListBox => CanvasPanelControl?.LabelClassList;
        private ListBox CanvasDisplayModeListBox => CanvasPanelControl?.DisplayModeList;
        private Border CanvasWorkflowContextStrip => CanvasPanelControl?.WorkflowContextStrip;
        private TextBlock CanvasCurrentStepText => CanvasPanelControl?.CurrentStepText;
        private TextBlock CanvasCurrentToolText => CanvasPanelControl?.CurrentToolText;
        private TextBlock CanvasNextActionText => CanvasPanelControl?.NextActionText;
        private Border CanvasLayerVisibilityStrip => CanvasPanelControl?.LayerVisibilityStrip;
        private TextBlock CanvasLayerModeTitleText => CanvasPanelControl?.LayerModeTitleText;
        private TextBlock CanvasLayerModeDetailText => CanvasPanelControl?.LayerModeDetailText;
        private TextBlock CanvasLabelLayerText => CanvasPanelControl?.LabelLayerText;
        private TextBlock CanvasInferenceLayerText => CanvasPanelControl?.InferenceLayerText;
        private Wpf.Ui.Controls.Button CanvasSaveAnnotationButton => CanvasPanelControl?.SaveAnnotationButton;
        private Wpf.Ui.Controls.Button CanvasCreateSmartMaskButton => CanvasPanelControl?.CreateSmartMaskButton;
        private Wpf.Ui.Controls.Button CanvasCompleteNoObjectButton => CanvasPanelControl?.CompleteNoObjectButton;
        private Border CanvasAnnotationSaveStateCard => CanvasPanelControl?.AnnotationSaveStateCard;
        private TextBlock CanvasAnnotationSaveStatusTitleText => CanvasPanelControl?.AnnotationSaveStatusTitleTextBlock;
        private TextBlock CanvasAnnotationSaveStatusDetailText => CanvasPanelControl?.AnnotationSaveStatusDetailTextBlock;
        private Border CanvasActiveLabelClassCard => CanvasPanelControl?.ActiveLabelClassCard;
        private TextBlock CanvasActiveLabelClassTitleText => CanvasPanelControl?.ActiveLabelClassTitleTextBlock;
        private TextBlock CanvasActiveLabelClassDetailText => CanvasPanelControl?.ActiveLabelClassDetailTextBlock;
        private Wpf.Ui.Controls.Button CanvasOpenClassCatalogButton => CanvasPanelControl?.OpenClassCatalogButton;
        private Wpf.Ui.Controls.Button FitCanvasButton => CanvasPanelControl?.FitButton;
        private Wpf.Ui.Controls.Button ActualSizeCanvasButton => CanvasPanelControl?.ActualSizeButton;
        private Wpf.Ui.Controls.Button PanCanvasButton => CanvasPanelControl?.PanButton;
        private Wpf.Ui.Controls.Button DisplayAdjustmentCanvasButton => CanvasPanelControl?.DisplayAdjustmentButton;
        private System.Windows.Controls.Primitives.Popup DisplayAdjustmentPopup => CanvasPanelControl?.DisplayAdjustmentFlyout;
        private Wpf.Ui.Controls.Button FocusCandidateCanvasButton => CanvasPanelControl?.FocusCandidateButton;
        private Wpf.Ui.Controls.Button ResetAiOverlayCanvasButton => CanvasPanelControl?.ResetAiOverlayButton;
        private Border DetectionResultOverlay => CanvasPanelControl?.ResultOverlay;
        private TextBlock DetectionOverlayTitleText => CanvasPanelControl?.OverlayTitleText;
        private TextBlock DetectionOverlaySummaryText => CanvasPanelControl?.OverlaySummaryText;
        private Border DetectionOverlaySelectedBorder => CanvasPanelControl?.OverlaySelectedBorder;
        private TextBlock DetectionOverlaySelectedText => CanvasPanelControl?.OverlaySelectedText;
        private TextBlock DetectionOverlayDetailText => CanvasPanelControl?.OverlayDetailText;
        private TextBlock ObjectReviewSummaryText => ObjectReviewPanelControl?.SummaryTextBlock;
        private Border ObjectReviewLabelSaveBadge => ObjectReviewPanelControl?.LabelSaveBadge;
        private TextBlock ObjectReviewLabelSaveBadgeText => ObjectReviewPanelControl?.LabelSaveBadgeTextBlock;
        private TextBlock ObjectReviewLabelSaveDetailText => ObjectReviewPanelControl?.LabelSaveDetailTextBlock;
        private Wpf.Ui.Controls.Button DeleteObjectButton => ObjectReviewPanelControl?.DeleteButton;
        private ComboBox ObjectClassBox => ObjectReviewPanelControl?.ClassBox;
        private Wpf.Ui.Controls.Button ApplyObjectClassButton => ObjectReviewPanelControl?.ApplyClassButton;
        private TextBlock MergeSelectionText => ObjectReviewPanelControl?.MergeSelectionTextBlock;
        private Wpf.Ui.Controls.Button MergeSelectedSegmentsButton => ObjectReviewPanelControl?.MergeSegmentsButton;
        private Wpf.Ui.Controls.Button BeginVerticalSplitButton => ObjectReviewPanelControl?.VerticalSplitButton;
        private Wpf.Ui.Controls.Button BeginHorizontalSplitButton => ObjectReviewPanelControl?.HorizontalSplitButton;
        private Wpf.Ui.Controls.Button CancelSplitButton => ObjectReviewPanelControl?.SplitCancelButton;
        private TextBlock SplitStatusText => ObjectReviewPanelControl?.SplitStatusTextBlock;
        private Wpf.Ui.Controls.Button BeginAddHoleButton => ObjectReviewPanelControl?.AddHoleButton;
        private Wpf.Ui.Controls.Button BeginRemoveHoleButton => ObjectReviewPanelControl?.RemoveHoleButton;
        private Wpf.Ui.Controls.Button CancelHoleEditButton => ObjectReviewPanelControl?.HoleEditCancelButton;
        private TextBlock HoleEditStatusText => ObjectReviewPanelControl?.HoleEditStatusTextBlock;
        private Wpf.Ui.Controls.Button BeginInsertVertexButton => ObjectReviewPanelControl?.InsertVertexButton;
        private Wpf.Ui.Controls.Button BeginDeleteVertexButton => ObjectReviewPanelControl?.DeleteVertexButton;
        private Wpf.Ui.Controls.Button CancelVertexEditButton => ObjectReviewPanelControl?.VertexEditCancelButton;
        private TextBlock VertexEditStatusText => ObjectReviewPanelControl?.VertexEditStatusTextBlock;
        private Wpf.Ui.Controls.Button BeginIntelligentScissorsButton => ObjectReviewPanelControl?.IntelligentScissorsBeginButton;
        private Wpf.Ui.Controls.Button ApplyIntelligentScissorsButton => ObjectReviewPanelControl?.IntelligentScissorsApplyButton;
        private Wpf.Ui.Controls.Button CancelIntelligentScissorsButton => ObjectReviewPanelControl?.IntelligentScissorsCancelButton;
        private TextBlock IntelligentScissorsStatusText => ObjectReviewPanelControl?.IntelligentScissorsStatusTextBlock;
        private Wpf.Ui.Controls.Button SendSegmentationToBackButton => ObjectReviewPanelControl?.SegmentationToBackButton;
        private Wpf.Ui.Controls.Button SendSegmentationBackwardButton => ObjectReviewPanelControl?.SegmentationBackwardButton;
        private Wpf.Ui.Controls.Button BringSegmentationForwardButton => ObjectReviewPanelControl?.SegmentationForwardButton;
        private Wpf.Ui.Controls.Button BringSegmentationToFrontButton => ObjectReviewPanelControl?.SegmentationToFrontButton;
        private TextBlock ZOrderStatusText => ObjectReviewPanelControl?.SegmentationZOrderStatusTextBlock;
        private Wpf.Ui.Controls.Button PreviewRemoveUnderlyingButton => ObjectReviewPanelControl?.RemoveUnderlyingPreviewButton;
        private Wpf.Ui.Controls.Button ApplyRemoveUnderlyingButton => ObjectReviewPanelControl?.RemoveUnderlyingApplyButton;
        private Wpf.Ui.Controls.Button CancelRemoveUnderlyingButton => ObjectReviewPanelControl?.RemoveUnderlyingCancelButton;
        private TextBlock RemoveUnderlyingStatusText => ObjectReviewPanelControl?.RemoveUnderlyingStatusTextBlock;
        private Wpf.Ui.Controls.Button ToggleObjectHiddenButton => ObjectReviewPanelControl?.ObjectHiddenButton;
        private Wpf.Ui.Controls.Button ToggleObjectLockedButton => ObjectReviewPanelControl?.ObjectLockedButton;
        private Wpf.Ui.Controls.Button ToggleObjectPinnedButton => ObjectReviewPanelControl?.ObjectPinnedButton;
        private TextBlock ObjectSessionStateStatusText => ObjectReviewPanelControl?.ObjectSessionStateStatusTextBlock;
        private Expander ObjectMetadataExpander => ObjectReviewPanelControl?.ObjectMetadataEditor;
        private Wpf.Ui.Controls.Button TogglePersistentOccludedButton => ObjectReviewPanelControl?.PersistentOccludedButton;
        private ComboBox ObjectMetadataTagBox => ObjectReviewPanelControl?.MetadataTagBox;
        private Wpf.Ui.Controls.Button TogglePersistentTagButton => ObjectReviewPanelControl?.PersistentTagButton;
        private Wpf.Ui.Controls.Button ToggleOccludedFilterButton => ObjectReviewPanelControl?.OccludedFilterButton;
        private ComboBox ObjectMetadataTagFilterBox => ObjectReviewPanelControl?.MetadataTagFilterBox;
        private Wpf.Ui.Controls.Button ResetObjectMetadataFilterButton => ObjectReviewPanelControl?.MetadataFilterResetButton;
        private Wpf.Ui.Controls.Button ResetRecipeMetadataTagsButton => ObjectReviewPanelControl?.RecipeMetadataTagsResetButton;
        private Wpf.Ui.Controls.Button BeginObjectGroupSelectionButton => ObjectReviewPanelControl?.GroupSelectionBeginButton;
        private Wpf.Ui.Controls.Button CreateObjectGroupButton => ObjectReviewPanelControl?.GroupCreateButton;
        private Wpf.Ui.Controls.Button CancelObjectGroupSelectionButton => ObjectReviewPanelControl?.GroupSelectionCancelButton;
        private TextBlock ObjectGroupSelectionStatusText => ObjectReviewPanelControl?.GroupSelectionStatusTextBlock;
        private TextBlock SelectedObjectGroupText => ObjectReviewPanelControl?.SelectedGroupTextBlock;
        private Wpf.Ui.Controls.Button RemoveObjectFromGroupButton => ObjectReviewPanelControl?.GroupMemberRemoveButton;
        private Wpf.Ui.Controls.Button DissolveObjectGroupButton => ObjectReviewPanelControl?.GroupDissolveButton;
        private Wpf.Ui.Controls.Button ToggleObjectGroupOccludedButton => ObjectReviewPanelControl?.GroupOccludedButton;
        private Wpf.Ui.Controls.Button ToggleObjectGroupTagButton => ObjectReviewPanelControl?.GroupTagButton;
        private ComboBox ObjectGroupFilterBox => ObjectReviewPanelControl?.GroupFilterBox;
        private Expander ObjectQualityReviewExpander => ObjectReviewPanelControl?.QualityReviewEditor;
        private Expander SegmentationAdvancedEditExpander => ObjectReviewPanelControl?.SegmentationAdvancedEditor;
        private ListBox ObjectListBox => ObjectReviewPanelControl?.ObjectList;
        private Slider CandidateConfidenceSlider => CandidateReviewPanelControl?.ConfidenceSlider;
        private Grid CandidateReviewRoleSplitPanel => CandidateReviewPanelControl?.RoleSplitPanel;
        private Border CurrentImageCandidateRoleCard => CandidateReviewPanelControl?.CurrentImageRoleCard;
        private Border ModelValidationRoleCard => CandidateReviewPanelControl?.ModelValidationCard;
        private TextBlock CurrentImageReviewRoleTitleText => CandidateReviewPanelControl?.CurrentImageRoleTitle;
        private TextBlock CurrentImageReviewRoleDetailText => CandidateReviewPanelControl?.CurrentImageRoleDetail;
        private TextBlock CurrentImageReviewRoleResultText => CandidateReviewPanelControl?.CurrentImageRoleResult;
        private TextBlock ModelValidationRoleTitleText => CandidateReviewPanelControl?.ModelValidationRoleTitle;
        private TextBlock ModelValidationRoleDetailText => CandidateReviewPanelControl?.ModelValidationRoleDetail;
        private TextBlock ModelValidationRoleResultText => CandidateReviewPanelControl?.ModelValidationRoleResult;
        private TextBlock CandidateConfidenceText => CandidateReviewPanelControl?.ConfidenceTextBlock;
        private TextBlock CandidateDetailText => CandidateReviewPanelControl?.DetailTextBlock;
        private Border ModelCandidateDecisionPanel => CandidateReviewPanelControl?.ModelCandidateDecision;
        private TextBlock ModelCandidateDecisionStatusText => CandidateReviewPanelControl?.ModelCandidateDecisionStatus;
        private TextBlock ModelCandidateDecisionDetailText => CandidateReviewPanelControl?.ModelCandidateDecisionDetail;
        private Border SelectedCandidateSummaryPanel => CandidateReviewPanelControl?.SelectedCandidateSummary;
        private TextBlock SelectedCandidateSummaryText => CandidateReviewPanelControl?.SelectedCandidateSummaryTextBlock;
        private Border CandidateComparisonPanel => CandidateReviewPanelControl?.ComparisonPanel;
        private TextBlock CandidateCompareCandidateText => CandidateReviewPanelControl?.CompareCandidateText;
        private TextBlock CandidateCompareCurrentText => CandidateReviewPanelControl?.CompareCurrentText;
        private TextBlock CandidateCompareOverlapText => CandidateReviewPanelControl?.CompareOverlapText;
        private TextBlock CandidateCompareDecisionText => CandidateReviewPanelControl?.CompareDecisionText;
        private Wpf.Ui.Controls.Button ConfirmSelectedCandidateButton => CandidateReviewPanelControl?.ConfirmSelectedButton;
        private Wpf.Ui.Controls.Button ConfirmAllCandidatesButton => CandidateReviewPanelControl?.ConfirmAllButton;
        private Wpf.Ui.Controls.Button SkipSelectedCandidateButton => CandidateReviewPanelControl?.SkipSelectedButton;
        private Wpf.Ui.Controls.Button CompleteImageAndNextButton => CandidateReviewPanelControl?.CompleteImageAndNext;
        private Wpf.Ui.Controls.Button SaveModelCandidateButton => CandidateReviewPanelControl?.SaveModelCandidate;
        private Wpf.Ui.Controls.Button RejectModelCandidateButton => CandidateReviewPanelControl?.RejectModelCandidate;
        private Wpf.Ui.Controls.Button PreviousCandidateButton => CandidateReviewPanelControl?.PreviousCandidate;
        private Wpf.Ui.Controls.Button NextCandidateButton => CandidateReviewPanelControl?.NextCandidate;
        private Wpf.Ui.Controls.Button FocusCandidateButton => CandidateReviewPanelControl?.FocusCandidate;
        private Wpf.Ui.Controls.Button FocusCurrentLabelButton => CandidateReviewPanelControl?.FocusCurrentLabel;
        private ListBox CandidateListBox => CandidateReviewPanelControl?.CandidateList;
        private Border ClassCatalogGuidePanel => ClassCatalogPanelControl?.GuidePanel;
        private TextBlock ClassCatalogGuideTitleText => ClassCatalogPanelControl?.GuideTitleTextBlock;
        private TextBlock ClassCatalogGuideDetailText => ClassCatalogPanelControl?.GuideDetailTextBlock;
        private TextBlock ClassCatalogSummaryText => ClassCatalogPanelControl?.SummaryTextBlock;
        private TextBlock CurrentDrawingClassTitleText => ClassCatalogPanelControl?.CurrentDrawingClassTitleTextBlock;
        private TextBlock CurrentDrawingClassDetailText => ClassCatalogPanelControl?.CurrentDrawingClassDetailTextBlock;
        private TextBlock ClassCatalogActionText => ClassCatalogPanelControl?.ActionTextBlock;
        private TextBlock ClassSectionLabelText => ClassCatalogPanelControl?.ClassSectionLabelTextBlock;
        private TextBox ClassNameBox => ClassCatalogPanelControl?.ClassNameTextBox;
        private Wpf.Ui.Controls.Button AddClassButton => ClassCatalogPanelControl?.AddClass;
        private Wpf.Ui.Controls.Button RenameClassButton => ClassCatalogPanelControl?.RenameClass;
        private Wpf.Ui.Controls.Button RemoveClassButton => ClassCatalogPanelControl?.RemoveClass;
        private ListBox ClassColorBox => ClassCatalogPanelControl?.ClassColor;
        private Expander ClassColorAdvancedPanel => ClassCatalogPanelControl?.ClassColorAdvanced;
        private Wpf.Ui.Controls.Button ApplyClassColorButton => ClassCatalogPanelControl?.ApplyClassColor;
        private TextBlock ClassEditStatusText => ClassCatalogPanelControl?.StatusTextBlock;
        private ListBox ClassListBox => ClassCatalogPanelControl?.ClassList;
        private TextBlock YoloSettingsSummaryText => YoloStatusPanelControl?.SummaryTextBlock;
        private Expander YoloRuntimeDetailsExpander => YoloStatusPanelControl?.RuntimeDetailsExpander;
        private TextBlock YoloSettingsDetailText => YoloStatusPanelControl?.DetailTextBlock;
        private Wpf.Ui.Controls.Button FirstCheckYoloButton => YoloStatusPanelControl?.FirstCheckButton;
        private Wpf.Ui.Controls.Button InstallRequirementsButton => YoloStatusPanelControl?.InstallRequirements;
        private Wpf.Ui.Controls.Button RunYoloSmokeButton => YoloStatusPanelControl?.RunSmokeButton;
        private Wpf.Ui.Controls.Button RestartPythonWorkerButton => YoloStatusPanelControl?.RestartWorkerButton;
        private Wpf.Ui.Controls.Button StopPythonWorkerButton => YoloStatusPanelControl?.StopWorkerButton;
        private TextBlock YoloCommandStatusText => YoloStatusPanelControl?.CommandStatusTextBlock;
        private ProgressBar YoloCommandProgressBar => YoloStatusPanelControl?.CommandProgress;
        private Expander ProjectConfigExpander => ProjectConfigPanelControl?.SettingsExpander;
        private TextBox ProjectRecipeNameBox => ProjectConfigPanelControl?.RecipeNameBox;
        private ComboBox ProjectRecipeListBox => ProjectConfigPanelControl?.RecipeListBox;
        private TextBox ProjectConfigPathBox => ProjectConfigPanelControl?.ConfigPathBox;
        private TextBox ProjectManifestPathBox => ProjectConfigPanelControl?.ManifestPathBox;
        private TextBox ProjectDatasetVersionBox => ProjectConfigPanelControl?.DatasetVersionBox;
        private TextBox ProjectDatasetVersionDetailBox => ProjectConfigPanelControl?.DatasetVersionDetailBox;
        private TextBlock ProjectConfigStatusText => ProjectConfigPanelControl?.StatusTextBlock;
        private Wpf.Ui.Controls.Button ApplyProjectRecipeButton => ProjectConfigPanelControl?.ApplyRecipeButton;
        private Wpf.Ui.Controls.Button RefreshProjectRecipeListButton => ProjectConfigPanelControl?.RefreshRecipeListButton;
        private Wpf.Ui.Controls.Button SaveProjectConfigButton => ProjectConfigPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button OpenProjectConfigFolderButton => ProjectConfigPanelControl?.OpenFolderButton;
        private Border YoloInspectionModelQuickPanel => YoloModelSettingsPanelControl?.InspectionModelQuickPanel;
        private TextBox YoloPythonPathBox => YoloModelSettingsPanelControl?.PythonPathBox;
        private ComboBox YoloModelEngineBox => YoloModelSettingsPanelControl?.ModelEngineBox;
        private TextBox YoloProjectRootBox => YoloModelSettingsPanelControl?.ProjectRootBox;
        private TextBox YoloClientScriptBox => YoloModelSettingsPanelControl?.ClientScriptBox;
        private TextBox YoloWeightsPathBox => YoloModelSettingsPanelControl?.WeightsPathBox;
        private TextBox YoloImageRootBox => YoloModelSettingsPanelControl?.ImageRootBox;
        private TextBox YoloConfidenceBox => YoloModelSettingsPanelControl?.ConfidenceBox;
        private TextBox YoloInferenceImageSizeBox => YoloModelSettingsPanelControl?.InferenceImageSizeBox;
        private TextBox YoloMaxCandidatesBox => YoloModelSettingsPanelControl?.MaxCandidatesBox;
        private TextBox YoloTimeoutBox => YoloModelSettingsPanelControl?.TimeoutBox;
        private CheckBox YoloAutoStartCheckBox => YoloModelSettingsPanelControl?.AutoStartCheckBox;
        private Wpf.Ui.Controls.Button BrowseYoloPythonButton => YoloModelSettingsPanelControl?.BrowsePythonButton;
        private Wpf.Ui.Controls.Button BrowseYoloProjectRootButton => YoloModelSettingsPanelControl?.BrowseProjectRootButton;
        private Wpf.Ui.Controls.Button BrowseYoloClientScriptButton => YoloModelSettingsPanelControl?.BrowseClientScriptButton;
        private Wpf.Ui.Controls.Button BrowseYoloWeightsButton => YoloModelSettingsPanelControl?.BrowseWeightsButton;
        private Wpf.Ui.Controls.Button BrowseYoloImageRootButton => YoloModelSettingsPanelControl?.BrowseImageRootButton;
        private Wpf.Ui.Controls.Button SaveYoloSettingsButton => YoloModelSettingsPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button ResetYoloSettingsButton => YoloModelSettingsPanelControl?.ResetButton;
        private Wpf.Ui.Controls.Button YoloRuntimeInstallPackageButton => YoloModelSettingsPanelControl?.RuntimeInstallPackageButton;
        private Wpf.Ui.Controls.Button YoloRuntimeUninstallPackageButton => YoloModelSettingsPanelControl?.RuntimeUninstallPackageButton;
        private Expander TrainingSettingsExpander => TrainingSettingsPanelControl?.SettingsExpander;
        private Border PostTrainingModelActionPanel => TrainingSettingsPanelControl?.PostTrainingActionPanel;
        private TextBlock PostTrainingModelStatusText => TrainingSettingsPanelControl?.PostTrainingModelStatus;
        private TextBlock PostTrainingModelDetailText => TrainingSettingsPanelControl?.PostTrainingModelDetail;
        private Wpf.Ui.Controls.Button ReviewTrainedModelButton => TrainingSettingsPanelControl?.ReviewTrainedModel;
        private Wpf.Ui.Controls.Button ConfirmTrainedModelButton => TrainingSettingsPanelControl?.ConfirmTrainedModel;
        private Wpf.Ui.Controls.Button RunYoloEngineComparisonButton => TrainingSettingsPanelControl?.RunYoloEngineComparison;
        private Border SegmentationAdapterComparisonPanel => TrainingSettingsPanelControl?.SegmentationAdapterComparison;
        private TextBox SegmentationUnetCheckpointPathBox => TrainingSettingsPanelControl?.SegmentationUnetCheckpointPath;
        private TextBox SegmentationYoloCheckpointPathBox => TrainingSettingsPanelControl?.SegmentationYoloCheckpointPath;
        private Wpf.Ui.Controls.Button BrowseSegmentationUnetCheckpointButton => TrainingSettingsPanelControl?.BrowseSegmentationUnetCheckpoint;
        private Wpf.Ui.Controls.Button BrowseSegmentationYoloCheckpointButton => TrainingSettingsPanelControl?.BrowseSegmentationYoloCheckpoint;
        private Wpf.Ui.Controls.Button RunSegmentationAdapterComparisonButton => TrainingSettingsPanelControl?.RunSegmentationAdapterComparison;
        private TextBox TrainingImageSizeBox => TrainingSettingsPanelControl?.ImageSizeBox;
        private TextBox TrainingBatchBox => TrainingSettingsPanelControl?.BatchBox;
        private TextBox TrainingEpochBox => TrainingSettingsPanelControl?.EpochBox;
        private ComboBox TrainingCfgBox => TrainingSettingsPanelControl?.CfgBox;
        private ComboBox TrainingWeightBox => TrainingSettingsPanelControl?.WeightBox;
        private TextBox TrainingValidationPercentBox => TrainingSettingsPanelControl?.ValidationPercentBox;
        private TextBox TrainingTestPercentBox => TrainingSettingsPanelControl?.TestPercentBox;
        private TextBox TrainingSplitSeedBox => TrainingSettingsPanelControl?.SplitSeedBox;
        private TextBlock TrainingSplitPolicyHintText => TrainingSettingsPanelControl?.SplitPolicyHintTextBlock;
        private Wpf.Ui.Controls.Button ApplyFastTrainingPresetButton => TrainingSettingsPanelControl?.ApplyFastPresetButton;
        private Wpf.Ui.Controls.Button RefreshTrainingReadinessButton => TrainingSettingsPanelControl?.RefreshReadinessButton;
        private Wpf.Ui.Controls.Button StartTrainingButton => TrainingSettingsPanelControl?.StartButton;
        private Wpf.Ui.Controls.Button StopTrainingButton => TrainingSettingsPanelControl?.StopButton;
        private TextBlock TrainingReadinessText => TrainingSettingsPanelControl?.ReadinessTextBlock;
        private ProgressBar TrainingProgressBar => TrainingSettingsPanelControl?.Progress;
        private TextBlock TrainingProgressText => TrainingSettingsPanelControl?.ProgressTextBlock;
        private TextBlock TrainingEpochText => TrainingSettingsPanelControl?.EpochTextBlock;
        private TextBlock DatasetStatusText => StatusBarPanelControl?.DatasetStatusTextBlock;
        private TextBlock WorkflowStageText => StatusBarPanelControl?.WorkflowStageTextBlock;
        private TextBlock WorkflowProgressText => StatusBarPanelControl?.WorkflowProgressTextBlock;
        private TextBlock WorkflowNextActionText => StatusBarPanelControl?.WorkflowNextActionTextBlock;
        private TextBlock PythonStatusText => StatusBarPanelControl?.PythonStatusTextBlock;
        private TextBlock AnnotationSaveStatusText => StatusBarPanelControl?.AnnotationSaveStatusTextBlock;
        private TextBlock InspectionModelStatusText => StatusBarPanelControl?.InspectionModelStatusTextBlock;
        private TextBlock ModelStatusText => StatusBarPanelControl?.ModelStatusTextBlock;
        private FrameworkElement ShellLogPanel => ShellLogPanelControl?.LogPanel;
        #endregion

        #region PanelWiring
        private void ComposePanelViewModels()
        {
            // Keep ViewModels out of UserControls; the shell composes data contexts so each View can be constructed standalone.
            LearningWorkflowPanelControl.DataContext = LearningWorkflowViewModel;
            ImageQueuePanelControl.DataContext = ImageQueueViewModel;
            CanvasPanelControl.DataContext = CanvasPanelViewModel;
            ObjectReviewPanelControl.DataContext = ObjectReviewViewModel;
            ClassCatalogPanelControl.DataContext = ClassCatalogViewModel;
            ProjectConfigPanelControl.DataContext = ProjectConfigViewModel;
            StatusBarPanelControl.DataContext = StatusBarViewModel;
            ShellLogPanelControl.DataContext = ShellLogViewModel;
            EnsureModelWorkflowPanelsComposed();
        }

        private void EnsureModelWorkflowPanelsComposed()
        {
            if (modelWorkflowPanelsComposed)
            {
                return;
            }

            modelWorkflowPanelsComposed = true;
            CandidateReviewPanelControl.DataContext = viewModels.ModelWorkflowViewModels.CandidateReviewViewModel;
            YoloStatusPanelControl.DataContext = viewModels.ModelWorkflowViewModels.YoloStatusViewModel;
            YoloModelSettingsPanelControl.DataContext = viewModels.ModelWorkflowViewModels.YoloModelSettingsViewModel;
            TrainingSettingsPanelControl.DataContext = viewModels.ModelWorkflowViewModels.TrainingSettingsViewModel;

            InitializeYoloEditorPanel();
            RegisterCandidateReviewPanelNames();
            RegisterYoloStatusPanelNames();
            RegisterYoloModelSettingsPanelNames();
            RegisterTrainingSettingsPanelNames();
        }

        private static void RefreshAttachedCommandBindings(DependencyObject target, params DependencyProperty[] properties)
        {
            if (target == null || properties == null)
            {
                return;
            }

            // Command ViewModels are injected after InitializeComponent; refresh attached-event bindings before the first user input.
            foreach (DependencyProperty property in properties)
            {
                BindingOperations.GetBindingExpression(target, property)?.UpdateTarget();
            }
        }

        private void SelectRightWorkflowView(TabItem tab)
        {
            if (tab == null)
            {
                return;
            }

            if (ReviewTabControl != null)
            {
                ReviewTabControl.SelectedItem = tab;
            }

            tab.IsSelected = true;
        }

        private void ShowSavedLabelsWorkflowView()
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Labeling);
            ShellViewModel?.SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(ObjectsReviewTab);
        }

        private void ShowCandidateReviewWorkflowView()
        {
            SetWorkflowMode(WorkflowMode.Inference);
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Inference);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(CandidatesReviewTab);
        }

        private void ShowGuideToolsWorkflowView(WpfShellWorkflowStage stage)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                LearningWorkflowViewModel?.ShowDatasetOnboarding();
            }
            else if (stage == WpfShellWorkflowStage.Labeling)
            {
                LearningWorkflowViewModel?.ShowLabelingTask();
            }

            ShellViewModel?.SetWorkflowStage(stage);
            ShellViewModel?.SetRightWorkflowShortcut(stage == WpfShellWorkflowStage.Labeling
                || stage == WpfShellWorkflowStage.Dataset
                ? WpfRightWorkflowShortcut.LabelingGuide
                : WpfRightWorkflowShortcut.None);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(LearningReviewTab);
        }

        private void ShowClassCatalogWorkflowView(WpfShellWorkflowStage stage)
        {
            ShellViewModel?.SetWorkflowStage(stage);
            ShellViewModel?.SetRightWorkflowShortcut(stage == WpfShellWorkflowStage.Labeling
                || stage == WpfShellWorkflowStage.Dataset
                ? WpfRightWorkflowShortcut.ClassCatalog
                : WpfRightWorkflowShortcut.None);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(ClassesReviewTab);
        }

        private void ShowYoloModelCenterWorkflowView()
        {
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(YoloSettingsReviewTab);
        }

        private void ConfigureLabelingCanvasDefaults()
        {
            MainCanvasViewModel.ShowGroupNames = false;
            MainCanvasViewModel.ShowRoiItemNames = false;
            MainCanvasViewModel.ShowGroupBounds = false;
            MainCanvasViewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
            MainCanvasViewModel.ShouldDrawOverExistingRoi = ShouldDrawOverExistingRoiForCurrentClass;
        }

        private void SeedImageQueueInputCommands()
        {
            // The shell is the composition root; seed behavior commands so pre-Loaded queue selection uses the same path as real clicks.
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueFilterBox, ImageQueueViewModel.FilterSelectionChangedCommand);
            InputCommandBehaviors.SetTextInputCommand(ImageQueueSearchBox, ImageQueueViewModel.SearchTextChangedCommand);
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueGrid, ImageQueueViewModel.QueueSelectionChangedCommand);
            InputCommandBehaviors.SetMouseDoubleClickInputCommand(ImageQueueGrid, ImageQueueViewModel.QueueMouseDoubleClickCommand);
        }


        public void FocusYoloSettingsTab()
        {
            ShowYoloModelCenterWorkflowView();
            if (YoloModelCenterTaskTabs != null)
            {
                YoloModelCenterTaskTabs.SelectedItem = YoloModelCenterOverviewTaskTab;
            }
            CollapseYoloAdvancedSettingsForOverview();
            UpdateLayout();
            YoloSettingsScrollViewer?.ScrollToTop();
        }

        private void FocusYoloModelSettingsTab()
        {
            ShowYoloModelCenterWorkflowView();
            if (YoloModelCenterTaskTabs != null)
            {
                YoloModelCenterTaskTabs.SelectedItem = YoloModelCenterRuntimeTaskTab;
            }
            CollapseYoloAdvancedSettingsForOverview();
            YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            UpdateLayout();
            YoloModelSettingsPanelControl?.BringIntoView();
        }

        private void FocusYoloTrainingSettingsTab()
        {
            ShowYoloModelCenterWorkflowView();
            if (YoloModelCenterTaskTabs != null)
            {
                YoloModelCenterTaskTabs.SelectedItem = YoloModelCenterTrainingTaskTab;
            }
            CollapseYoloAdvancedSettingsForOverview();
            TrainingSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            UpdateLayout();
            TrainingSettingsPanelControl?.BringIntoView();
        }

        private void YoloModelCenterTaskTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, YoloModelCenterTaskTabs))
            {
                return;
            }

            // The tab control raises SelectionChanged while InitializeComponent
            // is still constructing the hidden model center. Do not compose the
            // model workflow as a side effect of that startup event.
            if (!modelWorkflowPanelsComposed)
            {
                return;
            }

            YoloSettingsScrollViewer?.ScrollToTop();
            if (YoloModelCenterOverviewTaskTab?.IsSelected == true)
            {
                CollapseYoloAdvancedSettingsForOverview();
                YoloDatasetReadinessQuickPanel?.SetCurrentValue(Expander.IsExpandedProperty, false);
            }
            else if (YoloModelCenterDataTaskTab?.IsSelected == true)
            {
                YoloDatasetReadinessQuickPanel?.SetCurrentValue(Expander.IsExpandedProperty, true);
            }
            else if (YoloModelCenterTrainingTaskTab?.IsSelected == true)
            {
                if (CandidateReviewViewModel?.ModelComparisonVisibility != Visibility.Visible)
                {
                    try
                    {
                        UpdateTrainingComparisonViewModel(BuildCurrentTrainingWeightsComparison());
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC694\uC57D \uBD88\uB7EC\uC624\uAE30 \uC2E4\uD328: {ex.Message}");
                    }
                }
                TrainingSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            }
            else if (YoloModelCenterRuntimeTaskTab?.IsSelected == true)
            {
                YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            }
        }

        private void CollapseYoloAdvancedSettingsForOverview()
        {
            // Open the YOLO tab as an operator overview. Expanding every editor hides
            // the next action, so specific workflows opt into only the panel they need.
            YoloRuntimeDetailsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            ProjectConfigPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            TrainingSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
        }

        public void FocusAnnotationToolsTab()
        {
            ShowGuideToolsWorkflowView(WpfShellWorkflowStage.Labeling);
            UpdateLayout();
            LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
        }

        private void FocusCurrentStageGuideToolsTab()
        {
            if (ShellViewModel?.IsDatasetStageActive == true)
            {
                FocusDatasetOnboardingTab();
                return;
            }

            FocusAnnotationToolsTab();
        }

        private void FocusDatasetOnboardingTab()
        {
            ShowGuideToolsWorkflowView(WpfShellWorkflowStage.Dataset);
            UpdateLayout();
            LearningWorkflowPanelControl?.ShowDatasetSetupStart();
        }

        private void FocusDatasetOnboardingTabIfNoActiveImage()
        {
            if (activeImageBitmap != null && imageQueueItems.Count > 0)
            {
                return;
            }

            FocusDatasetOnboardingTab();
        }

        private void FocusLabelingSidePanelForTool(WpfAnnotationTool tool)
        {
            // Tool selection is a low-frequency UX event. Keep this out of brush/ROI
            // MouseMove/MouseUp paths so the side panel helps orientation without
            // competing with drawing performance or forcing tab changes per stroke.
            if (currentWorkflowMode != WorkflowMode.Labeling)
            {
                return;
            }

            switch (tool)
            {
                case WpfAnnotationTool.Rectangle:
                case WpfAnnotationTool.Ellipse:
                case WpfAnnotationTool.Polygon:
                case WpfAnnotationTool.Brush:
                case WpfAnnotationTool.Eraser:
                case WpfAnnotationTool.PanZoom:
                    FocusAnnotationToolsTab();
                    break;

                case WpfAnnotationTool.Select:
                case WpfAnnotationTool.Delete:
                    if (HasCanvasLabelObjects())
                    {
                        ShowSavedLabelsWorkflowView();
                    }
                    else
                    {
                        FocusAnnotationToolsTab();
                    }
                    break;
            }
        }

        private void InitializeYoloEditorPanel()
        {
            if (!modelWorkflowPanelsComposed)
            {
                return;
            }

            EnsureProjectSettings();
            PopulateProjectConfigPanelFields();
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            double configuredConfidence = global.Data.ProjectSettings.PythonModel.MinimumDetectionConfidence;
            CandidateConfidenceSlider.Value = Math.Clamp(configuredConfidence, 0D, 1D);
            UpdateCandidateConfidenceText();
        }

        private void PopulateYoloEditorFields()
        {
            EnsureProjectSettings();
            YoloModelSettingsViewModel?.LoadFrom(global.Data.ProjectSettings.PythonModel, global.Data.ProjectSettings.AnomalyClassification);
        }

        private void PopulateTrainingEditorFields()
        {
            EnsureProjectSettings();
            TrainingSettingsViewModel?.LoadFrom(
                global.Data.GetTrainingSettings(),
                global.Data.ProjectSettings.YoloDataset,
                global.Data.ProjectSettings.PythonModel,
                global.Data.ProjectSettings.DatasetPurpose,
                global.Data.ProjectSettings.ExternalYoloDataset);
            RefreshExternalYoloDatasetIntakePresentation();
            RefreshSegmentationAdapterComparisonState();
        }

        private void ConfigureShellCommands()
        {
            ShellViewModel.ConfigureCommands(
                ExecuteToggleThemeCommand,
                ExecuteLoadSampleCommand,
                ExecuteAddSampleRoiCommand,
                ExecuteSaveAnnotationsCommand,
                ExecuteLabelingModeCommand,
                ExecuteInferenceModeCommand,
                ExecuteCheckYoloCommand,
                ExecuteDetectCurrentImageCommand,
                TemplateMatchingAutoLabelViewModel.RunCurrentImage,
                ExecuteChangeDatasetCommand,
                ExecuteOpenDatasetRootFolderCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteLoadedCommand,
                ExecuteClosedCommand,
                ExecuteShellPreviewKeyDownCommand,
                ExecuteDatasetHomeCommand,
                ExecuteLabelingWorkbenchCommand,
                ExecuteInferenceReviewCommand,
                ExecuteTrainingModelCenterCommand,
                ExecuteReviewCandidateModelCommand,
                ShowSavedLabelsWorkflowView,
                FocusCurrentStageGuideToolsTab,
                FocusClassCatalogTab,
                ExecutePromoteSelectedModelHistoryCommand,
                ExecuteRunAnomalyEvaluationCommand,
                ExecuteLoadAnomalyEvaluationSummaryCommand,
                ExecuteResetWorkspaceLayoutCommand,
                ExecuteOpenModelBenchmarkCommand,
                ExecuteOpenDatasetHealthCommand,
                ExecuteOpenDatasetInterchangeCommand);
            RefreshAttachedCommandBindings(
                this,
                WindowLifecycleCommandBehavior.LoadedCommandProperty,
                WindowLifecycleCommandBehavior.ClosedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
            // XAML bindings can keep the same command reference while the shell
            // replaces its configured delegates. Reattach the lifecycle handlers
            // so the first Loaded/Closed event cannot be missed.
            WindowLifecycleCommandBehavior.RefreshLoadedCommand(this);
            WindowLifecycleCommandBehavior.RefreshClosedCommand(this);
            RefreshAttachedCommandBindings(DatasetHomeStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(LabelingWorkbenchStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(InferenceReviewStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(TrainingModelStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(WorkflowStageReviewCandidateModelButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(WorkflowStageSaveModelSettingsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(WorkflowStageInspectCurrentImageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowDockToggleButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowDatasetHomeButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowSavedLabelsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowGuideToolsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowClassCatalogButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowInferenceCandidatesButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowInferenceInspectButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowTrainingModelButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowTrainingReviewCandidateButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowTrainingInspectButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailOpenButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailSavedLabelsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailGuideToolsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailClassCatalogButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
        }
        #endregion

    }
}
