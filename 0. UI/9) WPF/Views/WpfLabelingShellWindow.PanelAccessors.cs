using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.Views;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // UI element proxies are centralized while panels finish moving to pure ViewModel bindings; this keeps legacy name lookups out of command logic.
        // The shell is the composition root: keep UserControls ViewModel-free and wire panel commands here.
        public WpfLabelingShellViewModel ShellViewModel => viewModels.ShellViewModel;

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => viewModels.LearningWorkflowViewModel;

        public WpfImageQueuePanelViewModel ImageQueueViewModel => viewModels.ImageQueueViewModel;

        public WpfCanvasPanelViewModel CanvasPanelViewModel => viewModels.CanvasPanelViewModel;

        public WpfObjectReviewPanelViewModel ObjectReviewViewModel => viewModels.ObjectReviewViewModel;

        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel => viewModels.CandidateReviewViewModel;

        public WpfClassCatalogPanelViewModel ClassCatalogViewModel => viewModels.ClassCatalogViewModel;

        public WpfYoloStatusPanelViewModel YoloStatusViewModel => viewModels.YoloStatusViewModel;

        public WpfProjectConfigPanelViewModel ProjectConfigViewModel => viewModels.ProjectConfigViewModel;

        public WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel => viewModels.YoloModelSettingsViewModel;

        public WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel => viewModels.TrainingSettingsViewModel;

        public WpfStatusBarPanelViewModel StatusBarViewModel => viewModels.StatusBarViewModel;

        public WpfShellLogPanelViewModel ShellLogViewModel => viewModels.ShellLogViewModel;

        public RoiImageCanvasViewModel MainCanvasViewModel => viewModels.MainCanvasViewModel;

        private ListBox DatasetPurposeListBox => LearningWorkflowPanelControl?.DatasetPurposeList;
        private TextBlock DatasetPurposeSummaryText => LearningWorkflowPanelControl?.DatasetPurposeSummary;
        private TextBlock DatasetPurposeToolSummaryText => LearningWorkflowPanelControl?.DatasetPurposeToolSummary;
        private Button DatasetSetupStartButton => LearningWorkflowPanelControl?.DatasetSetupStart;
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
        private Button YoloFixClassesButton => LearningWorkflowPanelControl?.YoloFixClasses;
        private Button YoloFixLabelsButton => LearningWorkflowPanelControl?.YoloFixLabels;
        private Button YoloFixDatasetButton => LearningWorkflowPanelControl?.YoloFixDataset;
        private Button TutorialOpenHtmlGuideButton => LearningWorkflowPanelControl?.TutorialOpenHtmlGuide;
        private ComboBox ImageQueueFilterBox => ImageQueuePanelControl?.FilterBox;
        private TextBox ImageQueueSearchBox => ImageQueuePanelControl?.SearchBox;
        private DataGrid ImageQueueGrid => ImageQueuePanelControl?.QueueGrid;
        private TextBlock BatchStatusText => ImageQueuePanelControl?.BatchStatusTextBlock;
        private ProgressBar BatchProgressBar => ImageQueuePanelControl?.BatchProgress;
        private Wpf.Ui.Controls.Button OpenSelectedQueueImageButton => ImageQueuePanelControl?.OpenSelectedButton;
        private Wpf.Ui.Controls.Button DetectSelectedQueueButton => ImageQueuePanelControl?.DetectSelectedButton;
        private Wpf.Ui.Controls.Button BatchDetectQueueButton => ImageQueuePanelControl?.BatchDetectButton;
        private Wpf.Ui.Controls.Button RetryFailedQueueButton => ImageQueuePanelControl?.RetryFailedButton;
        private Wpf.Ui.Controls.Button StopBatchQueueButton => ImageQueuePanelControl?.StopBatchButton;
        private Wpf.Ui.Controls.Button QueueFilterAllButton => ImageQueuePanelControl?.QueueFilterAll;
        private Wpf.Ui.Controls.Button QueueFilterCandidateButton => ImageQueuePanelControl?.QueueFilterCandidate;
        private Wpf.Ui.Controls.Button QueueFilterFailedButton => ImageQueuePanelControl?.QueueFilterFailed;
        private Wpf.Ui.Controls.Button QueueFilterConfirmedButton => ImageQueuePanelControl?.QueueFilterConfirmed;
        private Wpf.Ui.Controls.Button QueueFilterSkippedButton => ImageQueuePanelControl?.QueueFilterSkipped;
        private Wpf.Ui.Controls.Button QueueFilterNoCandidateButton => ImageQueuePanelControl?.QueueFilterNoCandidate;
        private TextBlock QueueFilterAllText => ImageQueuePanelControl?.QueueFilterAllTextBlock;
        private TextBlock QueueFilterCandidateText => ImageQueuePanelControl?.QueueFilterCandidateTextBlock;
        private TextBlock QueueFilterFailedText => ImageQueuePanelControl?.QueueFilterFailedTextBlock;
        private TextBlock QueueFilterConfirmedText => ImageQueuePanelControl?.QueueFilterConfirmedTextBlock;
        private TextBlock QueueFilterSkippedText => ImageQueuePanelControl?.QueueFilterSkippedTextBlock;
        private TextBlock QueueFilterNoCandidateText => ImageQueuePanelControl?.QueueFilterNoCandidateTextBlock;
        private RoiImageCanvasView MainCanvasView => CanvasPanelControl?.MainCanvas;
        private ListBox CanvasAnnotationToolListBox => CanvasPanelControl?.AnnotationToolList;
        private Border CanvasWorkflowContextStrip => CanvasPanelControl?.WorkflowContextStrip;
        private TextBlock CanvasCurrentStepText => CanvasPanelControl?.CurrentStepText;
        private TextBlock CanvasCurrentToolText => CanvasPanelControl?.CurrentToolText;
        private TextBlock CanvasNextActionText => CanvasPanelControl?.NextActionText;
        private Wpf.Ui.Controls.Button FitCanvasButton => CanvasPanelControl?.FitButton;
        private Wpf.Ui.Controls.Button ActualSizeCanvasButton => CanvasPanelControl?.ActualSizeButton;
        private Wpf.Ui.Controls.Button PanCanvasButton => CanvasPanelControl?.PanButton;
        private Wpf.Ui.Controls.Button FocusCandidateCanvasButton => CanvasPanelControl?.FocusCandidateButton;
        private Wpf.Ui.Controls.Button ResetAiOverlayCanvasButton => CanvasPanelControl?.ResetAiOverlayButton;
        private Border DetectionResultOverlay => CanvasPanelControl?.ResultOverlay;
        private TextBlock DetectionOverlayTitleText => CanvasPanelControl?.OverlayTitleText;
        private TextBlock DetectionOverlaySummaryText => CanvasPanelControl?.OverlaySummaryText;
        private Border DetectionOverlaySelectedBorder => CanvasPanelControl?.OverlaySelectedBorder;
        private TextBlock DetectionOverlaySelectedText => CanvasPanelControl?.OverlaySelectedText;
        private TextBlock DetectionOverlayDetailText => CanvasPanelControl?.OverlayDetailText;
        private TextBlock ObjectReviewSummaryText => ObjectReviewPanelControl?.SummaryTextBlock;
        private Wpf.Ui.Controls.Button DeleteObjectButton => ObjectReviewPanelControl?.DeleteButton;
        private ComboBox ObjectClassBox => ObjectReviewPanelControl?.ClassBox;
        private Wpf.Ui.Controls.Button ApplyObjectClassButton => ObjectReviewPanelControl?.ApplyClassButton;
        private ListBox ObjectListBox => ObjectReviewPanelControl?.ObjectList;
        private Slider CandidateConfidenceSlider => CandidateReviewPanelControl?.ConfidenceSlider;
        private TextBlock CandidateConfidenceText => CandidateReviewPanelControl?.ConfidenceTextBlock;
        private TextBlock CandidateDetailText => CandidateReviewPanelControl?.DetailTextBlock;
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
        private Wpf.Ui.Controls.Button PreviousCandidateButton => CandidateReviewPanelControl?.PreviousCandidate;
        private Wpf.Ui.Controls.Button NextCandidateButton => CandidateReviewPanelControl?.NextCandidate;
        private Wpf.Ui.Controls.Button FocusCandidateButton => CandidateReviewPanelControl?.FocusCandidate;
        private Wpf.Ui.Controls.Button FocusCurrentLabelButton => CandidateReviewPanelControl?.FocusCurrentLabel;
        private ListBox CandidateListBox => CandidateReviewPanelControl?.CandidateList;
        private TextBox ClassNameBox => ClassCatalogPanelControl?.ClassNameTextBox;
        private Wpf.Ui.Controls.Button AddClassButton => ClassCatalogPanelControl?.AddClass;
        private Wpf.Ui.Controls.Button RemoveClassButton => ClassCatalogPanelControl?.RemoveClass;
        private TextBox OutputRootPathBox => ClassCatalogPanelControl?.OutputRootPathTextBox;
        private Wpf.Ui.Controls.Button BrowseOutputRootButton => ClassCatalogPanelControl?.BrowseOutputRoot;
        private Wpf.Ui.Controls.Button SaveOutputRootButton => ClassCatalogPanelControl?.SaveOutputRoot;
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
        private TextBlock ProjectConfigStatusText => ProjectConfigPanelControl?.StatusTextBlock;
        private Wpf.Ui.Controls.Button ApplyProjectRecipeButton => ProjectConfigPanelControl?.ApplyRecipeButton;
        private Wpf.Ui.Controls.Button RefreshProjectRecipeListButton => ProjectConfigPanelControl?.RefreshRecipeListButton;
        private Wpf.Ui.Controls.Button SaveProjectConfigButton => ProjectConfigPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button OpenProjectConfigFolderButton => ProjectConfigPanelControl?.OpenFolderButton;
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
        private Expander TrainingSettingsExpander => TrainingSettingsPanelControl?.SettingsExpander;
        private TextBox TrainingImageSizeBox => TrainingSettingsPanelControl?.ImageSizeBox;
        private TextBox TrainingBatchBox => TrainingSettingsPanelControl?.BatchBox;
        private TextBox TrainingEpochBox => TrainingSettingsPanelControl?.EpochBox;
        private ComboBox TrainingCfgBox => TrainingSettingsPanelControl?.CfgBox;
        private ComboBox TrainingWeightBox => TrainingSettingsPanelControl?.WeightBox;
        private TextBox TrainingValidationPercentBox => TrainingSettingsPanelControl?.ValidationPercentBox;
        private TextBox TrainingTestPercentBox => TrainingSettingsPanelControl?.TestPercentBox;
        private TextBox TrainingSplitSeedBox => TrainingSettingsPanelControl?.SplitSeedBox;
        private TextBlock TrainingSplitPolicyHintText => TrainingSettingsPanelControl?.SplitPolicyHintTextBlock;
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
        private TextBlock ModelStatusText => StatusBarPanelControl?.ModelStatusTextBlock;
        private FrameworkElement ShellLogPanel => ShellLogPanelControl?.LogPanel;

    }
}
