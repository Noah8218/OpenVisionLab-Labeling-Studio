using OpenVisionLab.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public enum WpfShellWorkflowStage
    {
        Dataset,
        Labeling,
        Inference,
        TrainingModel
    }

    public enum WpfRightWorkflowShortcut
    {
        None,
        SavedLabels,
        LabelingGuide,
        ClassCatalog
    }

    public sealed class WpfLabelingShellViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private const string ModelCenterActionRouteText = "\uC2E4\uD589: \uD6C4\uBCF4 \uAC80\uC99D=\uD559\uC2B5 \uD6C4\uBCF4 \uD0ED \uC5F4\uAE30, \uD604\uC7AC \uAC80\uC0AC=\uAC80\uC0AC \uBAA8\uB378+\uD604\uC7AC \uC774\uBBF8\uC9C0 -> AI \uD6C4\uBCF4/\uCE94\uBC84\uC2A4";
        private static readonly GridLength RightWorkflowExpandedPaneGridLengthValue = new GridLength(340D);
        private static readonly GridLength RightWorkflowCollapsedPaneGridLengthValue = new GridLength(72D);
        private WpfShellWorkflowStage currentWorkflowStage = WpfShellWorkflowStage.Dataset;

        private bool isCurrentImageDetectionEnabled;
        private bool isLabelingModeActive = true;
        private bool isInferenceModeActive;
        private bool isDatasetStageActive = true;
        private bool isLabelingStageActive;
        private bool isInferenceStageActive;
        private bool isTrainingModelStageActive;
        private bool isWorkflowStageModelActionPanelVisible;
        private bool isSavedLabelsViewVisible;
        private bool isCandidateReviewViewVisible;
        private bool isGuideToolsViewVisible = true;
        private bool isClassCatalogViewVisible = true;
        private bool isYoloModelCenterViewVisible;
        private bool isRightWorkflowSubNavigationVisible;
        private bool isRightWorkflowShortcutBarVisible = true;
        private bool isRightWorkflowDockExpanded = true;
        private bool isRightWorkflowDockRailVisible;
        private GridLength rightWorkflowPaneGridLength = RightWorkflowExpandedPaneGridLengthValue;
        private string rightWorkflowDockToggleText = "\uC811\uAE30";
        private string rightWorkflowDockToggleToolTip = "\uC624\uB978\uCABD \uC791\uC5C5 \uD328\uB110\uC744 \uC811\uC2B5\uB2C8\uB2E4.";
        private string rightWorkflowViewTitleText = "\uB370\uC774\uD130\uC14B \uD648";
        private string rightWorkflowViewDetailText = "\uB370\uC774\uD130\uC14B \uC900\uBE44\uC640 \uC774\uBBF8\uC9C0/\uD074\uB798\uC2A4 \uC0C1\uD0DC\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.";
        private string rightWorkflowRailCurrentViewText = "\uD648";
        private bool isSavedLabelsShortcutActive;
        private bool isLabelingGuideShortcutActive = true;
        private bool isClassCatalogShortcutActive;
        private string workflowStageProgressText = WpfWorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).ProgressText;
        private string workflowStageTitleText = WpfWorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).TitleText;
        private string workflowStageDetailText = WpfWorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).DetailText;
        private string workflowStageNextActionText = WpfWorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).NextActionText;
        private bool isLabelingModeButtonEnabled = true;
        private bool isInferenceModeButtonEnabled = true;
        private bool isOpenDatasetFolderEnabled;
        private string currentDatasetName = "\uB370\uC774\uD130\uC14B \uBBF8\uC120\uD0DD";
        private string currentDatasetPurposeText = "\uBAA9\uC801 \uBBF8\uC120\uD0DD";
        private string currentDatasetPathText = "\uB370\uC774\uD130\uC14B\uC744 \uB9CC\uB4E4\uAC70\uB098 \uC5F4\uC5B4\uC8FC\uC138\uC694.";
        private string currentDatasetStoragePathText = "\uB77C\uBCA8/\uB808\uC2DC\uD53C \uC800\uC7A5: \uB370\uC774\uD130\uC14B\uC744 \uC5F4\uAC70\uB098 \uC0C8\uB85C \uB9CC\uB4DC\uC138\uC694";
        private string currentDatasetImageRootText = "\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354: \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694";
        private string currentDatasetSourceText = "\uD074\uB798\uC2A4/\uB77C\uBCA8 \uAE30\uC900 \uD655\uC778 \uD544\uC694";
        private string currentDatasetToolTip = "\uD604\uC7AC \uC791\uC5C5 \uB370\uC774\uD130\uC14B\uC744 \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string modelCenterTrainingStatusText = "\uD559\uC2B5 \uC0C1\uD0DC \uBBF8\uD655\uC778";
        private string modelCenterTrainingDetailText = "\uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694.";
        private string modelCenterCurrentModelText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C";
        private string modelCenterCandidateModelText = "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C";
        private string modelCenterAdoptionText = "\uBAA8\uB378 \uC801\uC6A9: \uB300\uAE30";
        private string modelCenterNextActionText = "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694.";
        private string modelCenterCurrentModelDetailText = "\uC5C6\uC74C";
        private string modelCenterCandidateModelDetailText = "\uC5C6\uC74C";
        private string modelCenterAdoptionDetailText = "\uB300\uAE30";
        private string modelCenterNextActionDetailText = "\uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694.";
        private string modelCenterDecisionSummaryText = "\uD310\uB2E8: \uD559\uC2B5 \uACB0\uACFC \uBE44\uAD50 \uC804";
        private string modelCenterDecisionEvidenceText = "\uADFC\uAC70: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uD559\uC2B5 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.";
        private string modelCenterDecisionActionText = "\uC800\uC7A5: \uD6C4\uBCF4 \uAC80\uD1A0 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5";
        private bool isModelCenterAnomalyEvaluationVisible;
        private string modelCenterAnomalyEvaluationRecommendationText = string.Empty;
        private string modelCenterAnomalyEvaluationMetricsText = string.Empty;
        private string modelCenterAnomalyEvaluationDetailText = string.Empty;
        private string modelCenterAnomalyEvaluationActionText = string.Empty;
        private string modelRegistrySummaryPrimaryText = "\uD604\uC7AC \uAC80\uC0AC: \uC5C6\uC74C / \uD559\uC2B5 \uD6C4\uBCF4: \uC5C6\uC74C";
        private string modelRegistrySummarySecondaryText = "YOLOv5 / \uCD5C\uADFC \uD559\uC2B5 \uC5C6\uC74C / \uC774\uB825 0\uAC74 / \uD6C4\uBCF4 \uC5C6\uC74C";
        private string modelRegistryProfileText = "\uBAA8\uB378 \uD504\uB85C\uD544: \uBBF8\uC124\uC815";
        private string modelRegistryTrainingRunText = "\uCD5C\uADFC \uD559\uC2B5 \uC2E4\uD589: \uC5C6\uC74C";
        private string modelRegistryCandidateModelText = "\uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C";
        private string modelRegistryInspectionModelText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C";
        private string modelRegistryActionText = "\uAD6C\uC870: \uBAA8\uB378 \uD504\uB85C\uD544 -> \uD559\uC2B5 \uC2E4\uD589 -> \uD6C4\uBCF4 \uBAA8\uB378 -> \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378";
        private string modelRegistryHistoryHeaderText = "\uCD5C\uADFC \uBAA8\uB378 \uC774\uB825 0\uAC74";
        private string modelRegistryHistorySummaryText = "\uD559\uC2B5 \uD6C4\uBCF4\uB97C \uC800\uC7A5\uD558\uAC70\uB098 \uAC70\uC808\uD558\uBA74 \uC5EC\uAE30\uC5D0 \uCD5C\uADFC \uC774\uB825\uC774 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private WpfModelRegistryHistoryItem selectedModelRegistryHistoryItem;
        private string selectedModelHistoryTitleText = "\uBAA8\uB378 \uC774\uB825 \uC120\uD0DD \uC5C6\uC74C";
        private string selectedModelHistoryDetailText = "\uC774\uB825\uC744 \uC120\uD0DD\uD558\uBA74 \uAC00\uC911\uCE58, \uC9C0\uD45C, \uC801\uC6A9 \uAC00\uB2A5 \uC5EC\uBD80\uB97C \uD655\uC778\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
        private string selectedModelHistoryMetricText = string.Empty;
        private string selectedModelHistoryDecisionText = string.Empty;
        private string selectedModelHistoryComparisonTitleText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uC120\uD0DD \uC774\uB825 \uBAA8\uB378";
        private string selectedModelHistoryCurrentModelText = string.Empty;
        private string selectedModelHistorySelectedModelText = string.Empty;
        private string selectedModelHistoryComparisonMetricText = string.Empty;
        private string selectedModelHistoryActionText = "\uC801\uC6A9 \uBD88\uAC00";
        private string selectedModelHistoryActionToolTip = "\uC801\uC6A9\uD560 \uBAA8\uB378 \uC774\uB825\uC744 \uBA3C\uC800 \uC120\uD0DD\uD558\uC138\uC694.";
        private bool isModelRegistryHistoryVisible;
        private bool isSelectedModelHistoryVisible;
        private bool isSelectedModelHistoryActionAvailable;
        private bool isSelectedModelHistoryActionEnabled;
        private bool isModelCenterRecoveryVisible;
        private string modelCenterRecoveryTitleText = string.Empty;
        private string modelCenterRecoveryDetailText = string.Empty;
        private string modelCenterRecoveryActionText = string.Empty;
        private string modelCenterConfirmModelButtonText = "\uD6C4\uBCF4 \uC5C6\uC74C";
        private string modelCenterConfirmModelButtonToolTip = "\uD655\uC815\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string modelCenterConfirmModelBaseToolTip = "\uD655\uC815\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string modelCenterConfirmModelUnavailableToolTip = string.Empty;
        private bool isModelCenterConfirmModelEnabled;
        private bool isModelCenterConfirmModelAvailable;
        private string modelCenterInspectCurrentImageButtonText = "\uD604\uC7AC \uAC80\uC0AC";
        private string modelCenterInspectCurrentImageButtonToolTip = "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uCD94\uB860\uC744 \uC2E4\uD589\uD569\uB2C8\uB2E4.";
        private bool isModelCenterInspectCurrentImageEnabled;
        private string modelCenterReviewCandidateButtonText = "\uD6C4\uBCF4 \uC5C6\uC74C";
        private string modelCenterReviewCandidateButtonToolTip = "\uAC80\uD1A0\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private bool isModelCenterReviewCandidateEnabled;
        private bool isModelCenterReviewCandidateAvailable;
        private string modelCenterRuntimeActionText = string.Empty;
        private string modelCenterActionStateText = "\uBC84\uD2BC \uC0C1\uD0DC: \uD6C4\uBCF4 \uAC80\uC99D \uB300\uAE30 / \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5 \uB300\uAE30 / \uD604\uC7AC \uAC80\uC0AC \uB300\uAE30";
        private bool canRunModelCenterCommands = true;
        private bool canRunModelCenterReviewCommands = true;
        private ICommand loadedCommand = new RelayCommand(NoOpCommand);
        private ICommand closedCommand = new RelayCommand(NoOpCommand);
        private ICommand previewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand toggleThemeCommand = new RelayCommand(NoOpCommand);
        private ICommand loadSampleCommand = new RelayCommand(NoOpCommand);
        private ICommand addSampleRoiCommand = new RelayCommand(NoOpCommand);
        private ICommand saveAnnotationsCommand = new RelayCommand(NoOpCommand);
        private ICommand labelingModeCommand = new RelayCommand(NoOpCommand);
        private ICommand inferenceModeCommand = new RelayCommand(NoOpCommand);
        private ICommand datasetHomeCommand = new RelayCommand(NoOpCommand);
        private ICommand labelingWorkbenchCommand = new RelayCommand(NoOpCommand);
        private ICommand inferenceReviewCommand = new RelayCommand(NoOpCommand);
        private ICommand trainingModelCenterCommand = new RelayCommand(NoOpCommand);
        private ICommand reviewCandidateModelCommand = new RelayCommand(NoOpCommand);
        private ICommand promoteSelectedModelHistoryCommand = new RelayCommand(NoOpCommand);
        private ICommand showSavedLabelsViewCommand = new RelayCommand(NoOpCommand);
        private ICommand showLabelingGuideViewCommand = new RelayCommand(NoOpCommand);
        private ICommand showClassCatalogViewCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleRightWorkflowDockCommand;
        private ICommand checkYoloCommand = new RelayCommand(NoOpCommand);
        private ICommand detectCurrentImageCommand = new RelayCommand(NoOpCommand);
        private ICommand runTemplateMatchingCommand = new RelayCommand(NoOpCommand);
        private ICommand changeDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand openDatasetFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand changeImageFolderCommand = new RelayCommand(NoOpCommand);

        public ObservableCollection<WpfModelRegistryHistoryItem> ModelRegistryHistoryItems { get; } = new ObservableCollection<WpfModelRegistryHistoryItem>();

        public string ViewName => nameof(WpfLabelingShellWindow);

        public bool IsCurrentImageDetectionEnabled
        {
            get => isCurrentImageDetectionEnabled;
            private set => SetProperty(ref isCurrentImageDetectionEnabled, value);
        }

        public bool IsLabelingModeActive
        {
            get => isLabelingModeActive;
            private set => SetProperty(ref isLabelingModeActive, value);
        }

        public bool IsInferenceModeActive
        {
            get => isInferenceModeActive;
            private set => SetProperty(ref isInferenceModeActive, value);
        }

        public bool IsDatasetStageActive
        {
            get => isDatasetStageActive;
            private set => SetProperty(ref isDatasetStageActive, value);
        }

        public bool IsLabelingStageActive
        {
            get => isLabelingStageActive;
            private set => SetProperty(ref isLabelingStageActive, value);
        }

        public bool IsInferenceStageActive
        {
            get => isInferenceStageActive;
            private set => SetProperty(ref isInferenceStageActive, value);
        }

        public bool IsTrainingModelStageActive
        {
            get => isTrainingModelStageActive;
            private set => SetProperty(ref isTrainingModelStageActive, value);
        }

        public bool IsWorkflowStageModelActionPanelVisible
        {
            get => isWorkflowStageModelActionPanelVisible;
            private set => SetProperty(ref isWorkflowStageModelActionPanelVisible, value);
        }

        public bool IsSavedLabelsViewVisible
        {
            get => isSavedLabelsViewVisible;
            private set => SetProperty(ref isSavedLabelsViewVisible, value);
        }

        public bool IsCandidateReviewViewVisible
        {
            get => isCandidateReviewViewVisible;
            private set => SetProperty(ref isCandidateReviewViewVisible, value);
        }

        public bool IsGuideToolsViewVisible
        {
            get => isGuideToolsViewVisible;
            private set => SetProperty(ref isGuideToolsViewVisible, value);
        }

        public bool IsClassCatalogViewVisible
        {
            get => isClassCatalogViewVisible;
            private set => SetProperty(ref isClassCatalogViewVisible, value);
        }

        public bool IsYoloModelCenterViewVisible
        {
            get => isYoloModelCenterViewVisible;
            private set => SetProperty(ref isYoloModelCenterViewVisible, value);
        }

        public bool IsRightWorkflowSubNavigationVisible
        {
            get => isRightWorkflowSubNavigationVisible;
            private set => SetProperty(ref isRightWorkflowSubNavigationVisible, value);
        }

        public bool IsRightWorkflowShortcutBarVisible
        {
            get => isRightWorkflowShortcutBarVisible;
            private set => SetProperty(ref isRightWorkflowShortcutBarVisible, value);
        }

        public bool IsRightWorkflowDockExpanded
        {
            get => isRightWorkflowDockExpanded;
            private set => SetProperty(ref isRightWorkflowDockExpanded, value);
        }

        public bool IsRightWorkflowDockRailVisible
        {
            get => isRightWorkflowDockRailVisible;
            private set => SetProperty(ref isRightWorkflowDockRailVisible, value);
        }

        public GridLength RightWorkflowPaneGridLength
        {
            get => rightWorkflowPaneGridLength;
            private set => SetProperty(ref rightWorkflowPaneGridLength, value);
        }

        public string RightWorkflowDockToggleText
        {
            get => rightWorkflowDockToggleText;
            private set => SetProperty(ref rightWorkflowDockToggleText, value ?? string.Empty);
        }

        public string RightWorkflowDockToggleToolTip
        {
            get => rightWorkflowDockToggleToolTip;
            private set => SetProperty(ref rightWorkflowDockToggleToolTip, value ?? string.Empty);
        }

        public bool IsSavedLabelsShortcutActive
        {
            get => isSavedLabelsShortcutActive;
            private set => SetProperty(ref isSavedLabelsShortcutActive, value);
        }

        public bool IsLabelingGuideShortcutActive
        {
            get => isLabelingGuideShortcutActive;
            private set => SetProperty(ref isLabelingGuideShortcutActive, value);
        }

        public bool IsClassCatalogShortcutActive
        {
            get => isClassCatalogShortcutActive;
            private set => SetProperty(ref isClassCatalogShortcutActive, value);
        }

        public string WorkflowStageProgressText
        {
            get => workflowStageProgressText;
            private set => SetProperty(ref workflowStageProgressText, value ?? string.Empty);
        }

        public string WorkflowStageTitleText
        {
            get => workflowStageTitleText;
            private set => SetProperty(ref workflowStageTitleText, value ?? string.Empty);
        }

        public string RightWorkflowViewTitleText
        {
            get => rightWorkflowViewTitleText;
            private set => SetProperty(ref rightWorkflowViewTitleText, value ?? string.Empty);
        }

        public string RightWorkflowViewDetailText
        {
            get => rightWorkflowViewDetailText;
            private set => SetProperty(ref rightWorkflowViewDetailText, value ?? string.Empty);
        }

        public string RightWorkflowRailCurrentViewText
        {
            get => rightWorkflowRailCurrentViewText;
            private set => SetProperty(ref rightWorkflowRailCurrentViewText, value ?? string.Empty);
        }

        public string WorkflowStageDetailText
        {
            get => workflowStageDetailText;
            private set => SetProperty(ref workflowStageDetailText, value ?? string.Empty);
        }

        public string WorkflowStageNextActionText
        {
            get => workflowStageNextActionText;
            private set => SetProperty(ref workflowStageNextActionText, value ?? string.Empty);
        }

        public bool IsLabelingModeButtonEnabled
        {
            get => isLabelingModeButtonEnabled;
            private set => SetProperty(ref isLabelingModeButtonEnabled, value);
        }

        public bool IsInferenceModeButtonEnabled
        {
            get => isInferenceModeButtonEnabled;
            private set => SetProperty(ref isInferenceModeButtonEnabled, value);
        }

        public bool IsOpenDatasetFolderEnabled
        {
            get => isOpenDatasetFolderEnabled;
            private set => SetProperty(ref isOpenDatasetFolderEnabled, value);
        }

        public string CurrentDatasetName
        {
            get => currentDatasetName;
            private set => SetProperty(ref currentDatasetName, value ?? string.Empty);
        }

        public string CurrentDatasetPurposeText
        {
            get => currentDatasetPurposeText;
            private set => SetProperty(ref currentDatasetPurposeText, value ?? string.Empty);
        }

        public string CurrentDatasetPathText
        {
            get => currentDatasetPathText;
            private set => SetProperty(ref currentDatasetPathText, value ?? string.Empty);
        }

        public string CurrentDatasetStoragePathText
        {
            get => currentDatasetStoragePathText;
            private set => SetProperty(ref currentDatasetStoragePathText, value ?? string.Empty);
        }

        public string CurrentDatasetImageRootText
        {
            get => currentDatasetImageRootText;
            private set => SetProperty(ref currentDatasetImageRootText, value ?? string.Empty);
        }

        public string CurrentDatasetSourceText
        {
            get => currentDatasetSourceText;
            private set => SetProperty(ref currentDatasetSourceText, value ?? string.Empty);
        }

        public string CurrentDatasetToolTip
        {
            get => currentDatasetToolTip;
            private set => SetProperty(ref currentDatasetToolTip, value ?? string.Empty);
        }

        public string ModelCenterTrainingStatusText
        {
            get => modelCenterTrainingStatusText;
            private set => SetProperty(ref modelCenterTrainingStatusText, value ?? string.Empty);
        }

        public string ModelCenterTrainingDetailText
        {
            get => modelCenterTrainingDetailText;
            private set => SetProperty(ref modelCenterTrainingDetailText, value ?? string.Empty);
        }

        public string ModelCenterCurrentModelText
        {
            get => modelCenterCurrentModelText;
            private set => SetProperty(ref modelCenterCurrentModelText, value ?? string.Empty);
        }

        public string ModelCenterCurrentModelTitleText => "\uAC80\uC0AC \uBAA8\uB378";

        public string ModelCenterCurrentModelDetailText
        {
            get => modelCenterCurrentModelDetailText;
            private set => SetProperty(ref modelCenterCurrentModelDetailText, value ?? string.Empty);
        }

        public string ModelCenterCandidateModelText
        {
            get => modelCenterCandidateModelText;
            private set => SetProperty(ref modelCenterCandidateModelText, value ?? string.Empty);
        }

        public string ModelCenterCandidateModelTitleText => "\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378";

        public string ModelCenterCandidateModelDetailText
        {
            get => modelCenterCandidateModelDetailText;
            private set => SetProperty(ref modelCenterCandidateModelDetailText, value ?? string.Empty);
        }

        public string ModelCenterAdoptionText
        {
            get => modelCenterAdoptionText;
            private set => SetProperty(ref modelCenterAdoptionText, value ?? string.Empty);
        }

        public string ModelCenterAdoptionTitleText => "\uC801\uC6A9 \uC0C1\uD0DC";

        public string ModelCenterAdoptionDetailText
        {
            get => modelCenterAdoptionDetailText;
            private set => SetProperty(ref modelCenterAdoptionDetailText, value ?? string.Empty);
        }

        public string ModelCenterNextActionText
        {
            get => modelCenterNextActionText;
            private set => SetProperty(ref modelCenterNextActionText, value ?? string.Empty);
        }

        public string ModelCenterNextActionTitleText => "\uB2E4\uC74C \uC791\uC5C5";

        public string ModelCenterNextActionDetailText
        {
            get => modelCenterNextActionDetailText;
            private set => SetProperty(ref modelCenterNextActionDetailText, value ?? string.Empty);
        }

        public string ModelCenterDecisionTitleText => "\uBAA8\uB378 \uC801\uC6A9 \uD310\uB2E8";

        public string ModelCenterDecisionSummaryText
        {
            get => modelCenterDecisionSummaryText;
            private set => SetProperty(ref modelCenterDecisionSummaryText, value ?? string.Empty);
        }

        public string ModelCenterDecisionEvidenceText
        {
            get => modelCenterDecisionEvidenceText;
            private set => SetProperty(ref modelCenterDecisionEvidenceText, value ?? string.Empty);
        }

        public string ModelCenterDecisionActionText
        {
            get => modelCenterDecisionActionText;
            private set => SetProperty(ref modelCenterDecisionActionText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationTitleText => "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00";

        public bool IsModelCenterAnomalyEvaluationVisible
        {
            get => isModelCenterAnomalyEvaluationVisible;
            private set => SetProperty(ref isModelCenterAnomalyEvaluationVisible, value);
        }

        public string ModelCenterAnomalyEvaluationRecommendationText
        {
            get => modelCenterAnomalyEvaluationRecommendationText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationRecommendationText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationMetricsText
        {
            get => modelCenterAnomalyEvaluationMetricsText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationMetricsText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationDetailText
        {
            get => modelCenterAnomalyEvaluationDetailText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationDetailText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationActionText
        {
            get => modelCenterAnomalyEvaluationActionText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationActionText, value ?? string.Empty);
        }

        public string ModelRegistryTitleText => "\uBAA8\uB378 \uB808\uC9C0\uC2A4\uD2B8\uB9AC";

        public string ModelRegistrySummaryPrimaryText
        {
            get => modelRegistrySummaryPrimaryText;
            private set => SetProperty(ref modelRegistrySummaryPrimaryText, value ?? string.Empty);
        }

        public string ModelRegistrySummarySecondaryText
        {
            get => modelRegistrySummarySecondaryText;
            private set => SetProperty(ref modelRegistrySummarySecondaryText, value ?? string.Empty);
        }

        public string ModelRegistryProfileText
        {
            get => modelRegistryProfileText;
            private set => SetProperty(ref modelRegistryProfileText, value ?? string.Empty);
        }

        public string ModelRegistryTrainingRunText
        {
            get => modelRegistryTrainingRunText;
            private set => SetProperty(ref modelRegistryTrainingRunText, value ?? string.Empty);
        }

        public string ModelRegistryCandidateModelText
        {
            get => modelRegistryCandidateModelText;
            private set => SetProperty(ref modelRegistryCandidateModelText, value ?? string.Empty);
        }

        public string ModelRegistryInspectionModelText
        {
            get => modelRegistryInspectionModelText;
            private set => SetProperty(ref modelRegistryInspectionModelText, value ?? string.Empty);
        }

        public string ModelRegistryActionText
        {
            get => modelRegistryActionText;
            private set => SetProperty(ref modelRegistryActionText, value ?? string.Empty);
        }

        public string ModelRegistryHistoryHeaderText
        {
            get => modelRegistryHistoryHeaderText;
            private set => SetProperty(ref modelRegistryHistoryHeaderText, value ?? string.Empty);
        }

        public string ModelRegistryHistorySummaryText
        {
            get => modelRegistryHistorySummaryText;
            private set => SetProperty(ref modelRegistryHistorySummaryText, value ?? string.Empty);
        }

        public bool IsModelRegistryHistoryVisible
        {
            get => isModelRegistryHistoryVisible;
            private set => SetProperty(ref isModelRegistryHistoryVisible, value);
        }

        public WpfModelRegistryHistoryItem SelectedModelRegistryHistoryItem
        {
            get => selectedModelRegistryHistoryItem;
            set
            {
                if (SetProperty(ref selectedModelRegistryHistoryItem, value))
                {
                    RefreshSelectedModelHistoryState();
                }
            }
        }

        public bool IsSelectedModelHistoryVisible
        {
            get => isSelectedModelHistoryVisible;
            private set => SetProperty(ref isSelectedModelHistoryVisible, value);
        }

        public string SelectedModelHistoryTitleText
        {
            get => selectedModelHistoryTitleText;
            private set => SetProperty(ref selectedModelHistoryTitleText, value ?? string.Empty);
        }

        public string SelectedModelHistoryDetailText
        {
            get => selectedModelHistoryDetailText;
            private set => SetProperty(ref selectedModelHistoryDetailText, value ?? string.Empty);
        }

        public string SelectedModelHistoryMetricText
        {
            get => selectedModelHistoryMetricText;
            private set => SetProperty(ref selectedModelHistoryMetricText, value ?? string.Empty);
        }

        public string SelectedModelHistoryDecisionText
        {
            get => selectedModelHistoryDecisionText;
            private set => SetProperty(ref selectedModelHistoryDecisionText, value ?? string.Empty);
        }

        public string SelectedModelHistoryComparisonTitleText
        {
            get => selectedModelHistoryComparisonTitleText;
            private set => SetProperty(ref selectedModelHistoryComparisonTitleText, value ?? string.Empty);
        }

        public string SelectedModelHistoryCurrentModelText
        {
            get => selectedModelHistoryCurrentModelText;
            private set => SetProperty(ref selectedModelHistoryCurrentModelText, value ?? string.Empty);
        }

        public string SelectedModelHistorySelectedModelText
        {
            get => selectedModelHistorySelectedModelText;
            private set => SetProperty(ref selectedModelHistorySelectedModelText, value ?? string.Empty);
        }

        public string SelectedModelHistoryComparisonMetricText
        {
            get => selectedModelHistoryComparisonMetricText;
            private set => SetProperty(ref selectedModelHistoryComparisonMetricText, value ?? string.Empty);
        }

        public string SelectedModelHistoryActionText
        {
            get => selectedModelHistoryActionText;
            private set => SetProperty(ref selectedModelHistoryActionText, value ?? string.Empty);
        }

        public string SelectedModelHistoryActionToolTip
        {
            get => selectedModelHistoryActionToolTip;
            private set => SetProperty(ref selectedModelHistoryActionToolTip, value ?? string.Empty);
        }

        public bool IsSelectedModelHistoryActionEnabled
        {
            get => isSelectedModelHistoryActionEnabled;
            private set => SetProperty(ref isSelectedModelHistoryActionEnabled, value);
        }

        public bool IsModelCenterRecoveryVisible
        {
            get => isModelCenterRecoveryVisible;
            private set => SetProperty(ref isModelCenterRecoveryVisible, value);
        }

        public string ModelCenterRecoveryTitleText
        {
            get => modelCenterRecoveryTitleText;
            private set => SetProperty(ref modelCenterRecoveryTitleText, value ?? string.Empty);
        }

        public string ModelCenterRecoveryDetailText
        {
            get => modelCenterRecoveryDetailText;
            private set => SetProperty(ref modelCenterRecoveryDetailText, value ?? string.Empty);
        }

        public string ModelCenterRecoveryActionText
        {
            get => modelCenterRecoveryActionText;
            private set => SetProperty(ref modelCenterRecoveryActionText, value ?? string.Empty);
        }

        public string ModelCenterConfirmModelButtonText
        {
            get => modelCenterConfirmModelButtonText;
            private set => SetProperty(ref modelCenterConfirmModelButtonText, value ?? string.Empty);
        }

        public string ModelCenterConfirmModelButtonToolTip
        {
            get => modelCenterConfirmModelButtonToolTip;
            private set => SetProperty(ref modelCenterConfirmModelButtonToolTip, value ?? string.Empty);
        }

        public bool IsModelCenterConfirmModelEnabled
        {
            get => isModelCenterConfirmModelEnabled;
            private set => SetProperty(ref isModelCenterConfirmModelEnabled, value);
        }

        public string ModelCenterInspectCurrentImageButtonText
        {
            get => modelCenterInspectCurrentImageButtonText;
            private set => SetProperty(ref modelCenterInspectCurrentImageButtonText, value ?? string.Empty);
        }

        public string ModelCenterInspectCurrentImageButtonToolTip
        {
            get => modelCenterInspectCurrentImageButtonToolTip;
            private set => SetProperty(ref modelCenterInspectCurrentImageButtonToolTip, value ?? string.Empty);
        }

        public bool IsModelCenterInspectCurrentImageEnabled
        {
            get => isModelCenterInspectCurrentImageEnabled;
            private set => SetProperty(ref isModelCenterInspectCurrentImageEnabled, value);
        }

        public string ModelCenterReviewCandidateButtonText
        {
            get => modelCenterReviewCandidateButtonText;
            private set => SetProperty(ref modelCenterReviewCandidateButtonText, value ?? string.Empty);
        }

        public string ModelCenterReviewCandidateButtonToolTip
        {
            get => modelCenterReviewCandidateButtonToolTip;
            private set => SetProperty(ref modelCenterReviewCandidateButtonToolTip, value ?? string.Empty);
        }

        public bool IsModelCenterReviewCandidateEnabled
        {
            get => isModelCenterReviewCandidateEnabled;
            private set => SetProperty(ref isModelCenterReviewCandidateEnabled, value);
        }

        public string ModelCenterRuntimeActionText
        {
            get => modelCenterRuntimeActionText;
            private set => SetProperty(ref modelCenterRuntimeActionText, value ?? string.Empty);
        }

        public string ModelCenterActionStateText
        {
            get => modelCenterActionStateText;
            private set => SetProperty(ref modelCenterActionStateText, value ?? string.Empty);
        }

        public ICommand LoadedCommand
        {
            get => loadedCommand;
            private set => SetProperty(ref loadedCommand, value);
        }

        public ICommand ClosedCommand
        {
            get => closedCommand;
            private set => SetProperty(ref closedCommand, value);
        }

        public ICommand PreviewKeyDownCommand
        {
            get => previewKeyDownCommand;
            private set => SetProperty(ref previewKeyDownCommand, value);
        }

        public ICommand ToggleThemeCommand
        {
            get => toggleThemeCommand;
            private set => SetProperty(ref toggleThemeCommand, value);
        }

        public ICommand LoadSampleCommand
        {
            get => loadSampleCommand;
            private set => SetProperty(ref loadSampleCommand, value);
        }

        public ICommand AddSampleRoiCommand
        {
            get => addSampleRoiCommand;
            private set => SetProperty(ref addSampleRoiCommand, value);
        }

        public ICommand SaveAnnotationsCommand
        {
            get => saveAnnotationsCommand;
            private set => SetProperty(ref saveAnnotationsCommand, value);
        }

        public ICommand LabelingModeCommand
        {
            get => labelingModeCommand;
            private set => SetProperty(ref labelingModeCommand, value);
        }

        public ICommand InferenceModeCommand
        {
            get => inferenceModeCommand;
            private set => SetProperty(ref inferenceModeCommand, value);
        }

        public ICommand DatasetHomeCommand
        {
            get => datasetHomeCommand;
            private set => SetProperty(ref datasetHomeCommand, value);
        }

        public ICommand LabelingWorkbenchCommand
        {
            get => labelingWorkbenchCommand;
            private set => SetProperty(ref labelingWorkbenchCommand, value);
        }

        public ICommand InferenceReviewCommand
        {
            get => inferenceReviewCommand;
            private set => SetProperty(ref inferenceReviewCommand, value);
        }

        public ICommand TrainingModelCenterCommand
        {
            get => trainingModelCenterCommand;
            private set => SetProperty(ref trainingModelCenterCommand, value);
        }

        public ICommand ReviewCandidateModelCommand
        {
            get => reviewCandidateModelCommand;
            private set => SetProperty(ref reviewCandidateModelCommand, value);
        }

        public ICommand PromoteSelectedModelHistoryCommand
        {
            get => promoteSelectedModelHistoryCommand;
            private set => SetProperty(ref promoteSelectedModelHistoryCommand, value);
        }

        public ICommand ShowSavedLabelsViewCommand
        {
            get => showSavedLabelsViewCommand;
            private set => SetProperty(ref showSavedLabelsViewCommand, value);
        }

        public ICommand ShowLabelingGuideViewCommand
        {
            get => showLabelingGuideViewCommand;
            private set => SetProperty(ref showLabelingGuideViewCommand, value);
        }

        public ICommand ShowClassCatalogViewCommand
        {
            get => showClassCatalogViewCommand;
            private set => SetProperty(ref showClassCatalogViewCommand, value);
        }

        public ICommand ToggleRightWorkflowDockCommand
        {
            get
            {
                if (toggleRightWorkflowDockCommand == null)
                {
                    toggleRightWorkflowDockCommand = new RelayCommand(ToggleRightWorkflowDock);
                }

                return toggleRightWorkflowDockCommand;
            }
        }

        public ICommand CheckYoloCommand
        {
            get => checkYoloCommand;
            private set => SetProperty(ref checkYoloCommand, value);
        }

        public ICommand DetectCurrentImageCommand
        {
            get => detectCurrentImageCommand;
            private set => SetProperty(ref detectCurrentImageCommand, value);
        }

        public ICommand RunTemplateMatchingCommand
        {
            get => runTemplateMatchingCommand;
            private set => SetProperty(ref runTemplateMatchingCommand, value);
        }

        public ICommand ChangeDatasetCommand
        {
            get => changeDatasetCommand;
            private set => SetProperty(ref changeDatasetCommand, value);
        }

        public ICommand OpenDatasetFolderCommand
        {
            get => openDatasetFolderCommand;
            private set => SetProperty(ref openDatasetFolderCommand, value);
        }

        public ICommand ChangeImageFolderCommand
        {
            get => changeImageFolderCommand;
            private set => SetProperty(ref changeImageFolderCommand, value);
        }

        public void ConfigureCommands(
            Action toggleTheme,
            Action loadSample,
            Action addSampleRoi,
            Action saveAnnotations,
            Action labelingMode,
            Action inferenceMode,
            Action checkYolo,
            Action detectCurrentImage,
            Action runTemplateMatching,
            Action changeDataset,
            Action openDatasetFolder,
            Action changeImageFolder,
            Action loaded,
            Action closed,
            Action<KeyInputCommandArgs> previewKeyDown,
            Action datasetHome = null,
            Action labelingWorkbench = null,
            Action inferenceReview = null,
            Action trainingModelCenter = null,
            Action reviewCandidateModel = null,
            Action showSavedLabelsView = null,
            Action showLabelingGuideView = null,
            Action showClassCatalogView = null,
            Action promoteSelectedModelHistory = null)
        {
            // Shell lifecycle and toolbar commands are injected; key commands use a DTO so this ViewModel avoids WPF EventArgs.
            ToggleThemeCommand = new RelayCommand(toggleTheme ?? NoOpCommand);
            LoadSampleCommand = new RelayCommand(loadSample ?? NoOpCommand);
            AddSampleRoiCommand = new RelayCommand(addSampleRoi ?? NoOpCommand);
            SaveAnnotationsCommand = new RelayCommand(saveAnnotations ?? NoOpCommand);
            LabelingModeCommand = new RelayCommand(labelingMode ?? NoOpCommand);
            InferenceModeCommand = new RelayCommand(inferenceMode ?? NoOpCommand);
            DatasetHomeCommand = new RelayCommand(datasetHome ?? NoOpCommand);
            LabelingWorkbenchCommand = new RelayCommand(labelingWorkbench ?? labelingMode ?? NoOpCommand);
            InferenceReviewCommand = new RelayCommand(inferenceReview ?? inferenceMode ?? NoOpCommand);
            TrainingModelCenterCommand = new RelayCommand(trainingModelCenter ?? NoOpCommand);
            ReviewCandidateModelCommand = new RelayCommand(reviewCandidateModel ?? inferenceReview ?? inferenceMode ?? NoOpCommand);
            PromoteSelectedModelHistoryCommand = new RelayCommand(promoteSelectedModelHistory ?? NoOpCommand);
            ShowSavedLabelsViewCommand = new RelayCommand(showSavedLabelsView ?? labelingMode ?? NoOpCommand);
            ShowLabelingGuideViewCommand = new RelayCommand(showLabelingGuideView ?? labelingMode ?? NoOpCommand);
            ShowClassCatalogViewCommand = new RelayCommand(showClassCatalogView ?? labelingMode ?? NoOpCommand);
            CheckYoloCommand = new RelayCommand(checkYolo ?? NoOpCommand);
            DetectCurrentImageCommand = new RelayCommand(detectCurrentImage ?? NoOpCommand);
            RunTemplateMatchingCommand = new RelayCommand(runTemplateMatching ?? NoOpCommand);
            ChangeDatasetCommand = new RelayCommand(changeDataset ?? NoOpCommand);
            OpenDatasetFolderCommand = new RelayCommand(openDatasetFolder ?? NoOpCommand);
            ChangeImageFolderCommand = new RelayCommand(changeImageFolder ?? NoOpCommand);
            LoadedCommand = new RelayCommand(loaded ?? NoOpCommand);
            ClosedCommand = new RelayCommand(closed ?? NoOpCommand);
            PreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(previewKeyDown ?? NoOpKeyCommand);
        }

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            IsCurrentImageDetectionEnabled = state?.CanRunInference == true;
            canRunModelCenterCommands = state?.CanSaveProjectConfig == true;
            canRunModelCenterReviewCommands = state?.CanRunGeneralCommands == true;
            modelCenterConfirmModelUnavailableToolTip = state?.CanSaveProjectConfigUnavailableHint ?? string.Empty;
            RefreshModelCenterConfirmModelEnabled();
            RefreshModelCenterReviewCandidateEnabled();
            RefreshModelCenterInspectCurrentImageEnabled();
            RefreshSelectedModelHistoryActionEnabled();
        }

        public void SetWorkflowModeState(bool isInferenceMode, bool canSwitchMode)
        {
            IsLabelingModeActive = !isInferenceMode;
            IsInferenceModeActive = isInferenceMode;
            IsLabelingModeButtonEnabled = IsLabelingModeActive || canSwitchMode;
            IsInferenceModeButtonEnabled = IsInferenceModeActive || canSwitchMode;
        }

        public void SetWorkflowStage(WpfShellWorkflowStage stage)
        {
            currentWorkflowStage = stage;
            IsDatasetStageActive = stage == WpfShellWorkflowStage.Dataset;
            IsLabelingStageActive = stage == WpfShellWorkflowStage.Labeling;
            IsInferenceStageActive = stage == WpfShellWorkflowStage.Inference;
            IsTrainingModelStageActive = stage == WpfShellWorkflowStage.TrainingModel;
            ApplyWorkflowStageViewVisibility(stage);
            RefreshWorkflowStageModelActionPanelVisibility();
            ApplyWorkflowStageShortcutState(stage);
            ApplyRightWorkflowDockPreset(stage);

            WpfWorkflowStagePresentation presentation = WpfWorkflowStagePresentationService.Build(stage);
            WorkflowStageProgressText = presentation.ProgressText;
            WorkflowStageTitleText = presentation.TitleText;
            WorkflowStageDetailText = presentation.DetailText;
            WorkflowStageNextActionText = presentation.NextActionText;
            RightWorkflowViewTitleText = BuildRightWorkflowViewTitle(stage, GetActiveRightWorkflowShortcut());
            RightWorkflowViewDetailText = BuildRightWorkflowViewDetail(stage, GetActiveRightWorkflowShortcut());
            RightWorkflowRailCurrentViewText = BuildRightWorkflowRailCurrentViewText(stage, GetActiveRightWorkflowShortcut());
        }

        public void SetRightWorkflowShortcut(WpfRightWorkflowShortcut shortcut)
        {
            if (currentWorkflowStage == WpfShellWorkflowStage.Dataset && shortcut == WpfRightWorkflowShortcut.SavedLabels)
            {
                shortcut = WpfRightWorkflowShortcut.LabelingGuide;
            }

            if (currentWorkflowStage != WpfShellWorkflowStage.Dataset
                && currentWorkflowStage != WpfShellWorkflowStage.Labeling)
            {
                shortcut = WpfRightWorkflowShortcut.None;
            }

            IsSavedLabelsShortcutActive = shortcut == WpfRightWorkflowShortcut.SavedLabels;
            IsLabelingGuideShortcutActive = shortcut == WpfRightWorkflowShortcut.LabelingGuide;
            IsClassCatalogShortcutActive = shortcut == WpfRightWorkflowShortcut.ClassCatalog;
            RightWorkflowViewTitleText = BuildRightWorkflowViewTitle(currentWorkflowStage, shortcut);
            RightWorkflowViewDetailText = BuildRightWorkflowViewDetail(currentWorkflowStage, shortcut);
            RightWorkflowRailCurrentViewText = BuildRightWorkflowRailCurrentViewText(currentWorkflowStage, shortcut);
        }

        private WpfRightWorkflowShortcut GetActiveRightWorkflowShortcut()
        {
            if (IsSavedLabelsShortcutActive)
            {
                return WpfRightWorkflowShortcut.SavedLabels;
            }

            if (IsLabelingGuideShortcutActive)
            {
                return WpfRightWorkflowShortcut.LabelingGuide;
            }

            return IsClassCatalogShortcutActive
                ? WpfRightWorkflowShortcut.ClassCatalog
                : WpfRightWorkflowShortcut.None;
        }

        private static string BuildRightWorkflowViewTitle(WpfShellWorkflowStage stage, WpfRightWorkflowShortcut shortcut)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                return shortcut == WpfRightWorkflowShortcut.ClassCatalog
                    ? "\uD074\uB798\uC2A4"
                    : "\uB370\uC774\uD130\uC14B \uD648";
            }

            if (stage == WpfShellWorkflowStage.Labeling)
            {
                return shortcut switch
                {
                    WpfRightWorkflowShortcut.LabelingGuide => "\uD604\uC7AC \uC791\uC5C5",
                    WpfRightWorkflowShortcut.ClassCatalog => "\uD074\uB798\uC2A4",
                    _ => "\uC800\uC7A5 \uB77C\uBCA8"
                };
            }

            return stage switch
            {
                WpfShellWorkflowStage.Inference => "AI 후보 검토",
                WpfShellWorkflowStage.TrainingModel => "\uD559\uC2B5/\uBAA8\uB378",
                _ => "\uB370\uC774\uD130\uC14B \uD648"
            };
        }

        private static string BuildRightWorkflowViewDetail(WpfShellWorkflowStage stage, WpfRightWorkflowShortcut shortcut)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                return shortcut == WpfRightWorkflowShortcut.ClassCatalog
                    ? "\uB370\uC774\uD130\uC14B \uD559\uC2B5 \uBAA9\uC801\uC5D0 \uB9DE\uB294 \uD074\uB798\uC2A4 \uC774\uB984\uACFC \uC0C9\uC0C1\uC744 \uAD00\uB9AC\uD569\uB2C8\uB2E4."
                    : "\uB370\uC774\uD130\uC14B \uC900\uBE44\uC640 \uC774\uBBF8\uC9C0/\uD074\uB798\uC2A4 \uC0C1\uD0DC\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.";
            }

            if (stage == WpfShellWorkflowStage.Labeling)
            {
                return shortcut switch
                {
                    WpfRightWorkflowShortcut.LabelingGuide => "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0\uC11C \uD655\uC778, \uD655\uC815, \uC2A4\uD0B5 \uC911 \uB2E4\uC74C \uD589\uB3D9\uC744 \uC55E\uC5D0 \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.",
                    WpfRightWorkflowShortcut.ClassCatalog => "\uB77C\uBCA8 \uD074\uB798\uC2A4 \uC774\uB984\uACFC \uC0C9\uC0C1\uC744 \uAD00\uB9AC\uD569\uB2C8\uB2E4.",
                    _ => "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uC800\uC7A5 \uB77C\uBCA8\uC744 \uD655\uC778\uD558\uACE0 \uC218\uC815\uD569\uB2C8\uB2E4."
                };
            }

            return stage switch
            {
                WpfShellWorkflowStage.Inference => "AI \uD6C4\uBCF4\uB97C \uC800\uC7A5 \uB77C\uBCA8\uB85C \uD655\uC815\uD558\uAC70\uB098 \uC228\uAE41\uB2C8\uB2E4.",
                WpfShellWorkflowStage.TrainingModel => "\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uACFC \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.",
                _ => "\uB370\uC774\uD130\uC14B \uC900\uBE44\uC640 \uC774\uBBF8\uC9C0/\uD074\uB798\uC2A4 \uC0C1\uD0DC\uB97C \uD655\uC778\uD569\uB2C8\uB2E4."
            };
        }

        private static string BuildRightWorkflowRailCurrentViewText(WpfShellWorkflowStage stage, WpfRightWorkflowShortcut shortcut)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                return shortcut == WpfRightWorkflowShortcut.ClassCatalog
                    ? "\uD074\uB798\uC2A4"
                    : "\uD648";
            }

            if (stage == WpfShellWorkflowStage.Labeling)
            {
                return shortcut switch
                {
                    WpfRightWorkflowShortcut.LabelingGuide => "\uC791\uC5C5",
                    WpfRightWorkflowShortcut.ClassCatalog => "\uD074\uB798\uC2A4",
                    _ => "\uB77C\uBCA8"
                };
            }

            return stage switch
            {
                WpfShellWorkflowStage.Inference => "AI 후보",
                WpfShellWorkflowStage.TrainingModel => "\uBAA8\uB378",
                _ => "\uD648"
            };
        }

        private void ApplyWorkflowStageShortcutState(WpfShellWorkflowStage stage)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                SetRightWorkflowShortcut(IsClassCatalogShortcutActive
                    ? WpfRightWorkflowShortcut.ClassCatalog
                    : WpfRightWorkflowShortcut.LabelingGuide);
                return;
            }

            if (stage != WpfShellWorkflowStage.Labeling)
            {
                SetRightWorkflowShortcut(WpfRightWorkflowShortcut.None);
                return;
            }

            if (!IsSavedLabelsShortcutActive
                && !IsClassCatalogShortcutActive)
            {
                SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            }
        }

        private void ApplyWorkflowStageViewVisibility(WpfShellWorkflowStage stage)
        {
            IsSavedLabelsViewVisible = stage == WpfShellWorkflowStage.Labeling;
            IsCandidateReviewViewVisible = stage == WpfShellWorkflowStage.Inference;
            IsGuideToolsViewVisible = stage == WpfShellWorkflowStage.Dataset
                || stage == WpfShellWorkflowStage.Labeling;
            IsClassCatalogViewVisible = stage == WpfShellWorkflowStage.Dataset
                || stage == WpfShellWorkflowStage.Labeling;
            IsYoloModelCenterViewVisible = stage == WpfShellWorkflowStage.TrainingModel;
            int visibleViewCount = 0;
            visibleViewCount += IsSavedLabelsViewVisible ? 1 : 0;
            visibleViewCount += IsCandidateReviewViewVisible ? 1 : 0;
            visibleViewCount += IsGuideToolsViewVisible ? 1 : 0;
            visibleViewCount += IsClassCatalogViewVisible ? 1 : 0;
            visibleViewCount += IsYoloModelCenterViewVisible ? 1 : 0;
            IsRightWorkflowShortcutBarVisible = true;
            IsRightWorkflowSubNavigationVisible = visibleViewCount > 1 && !IsRightWorkflowShortcutBarVisible;
        }

        private void RefreshWorkflowStageModelActionPanelVisibility()
        {
            IsWorkflowStageModelActionPanelVisible = IsTrainingModelStageActive && !IsYoloModelCenterViewVisible;
        }

        public void SetRightWorkflowDockExpanded(bool isExpanded)
        {
            IsRightWorkflowDockExpanded = isExpanded;
            IsRightWorkflowDockRailVisible = !isExpanded;
            RightWorkflowPaneGridLength = isExpanded
                ? RightWorkflowExpandedPaneGridLengthValue
                : RightWorkflowCollapsedPaneGridLengthValue;
            RightWorkflowDockToggleText = isExpanded ? "\uC811\uAE30" : "\uC5F4\uAE30";
            RightWorkflowDockToggleToolTip = isExpanded
                ? "\uC624\uB978\uCABD \uC791\uC5C5 \uD328\uB110\uC744 \uC811\uACE0 \uCE94\uBC84\uC2A4 \uACF5\uAC04\uC744 \uB113\uD799\uB2C8\uB2E4."
                : "\uC624\uB978\uCABD \uC791\uC5C5 \uD328\uB110\uC744 \uC5F4\uC5B4 \uB77C\uBCA8, \uB3C4\uAD6C, \uBAA8\uB378 \uC124\uC815\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.";
        }

        private void ToggleRightWorkflowDock()
        {
            SetRightWorkflowDockExpanded(!IsRightWorkflowDockExpanded);
        }

        private void ApplyRightWorkflowDockPreset(WpfShellWorkflowStage stage)
        {
            SetRightWorkflowDockExpanded(stage != WpfShellWorkflowStage.Labeling);
        }

        public void SetModelCenterTrainingState(string statusText, string detailText)
        {
            ModelCenterTrainingStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "\uD559\uC2B5 \uC0C1\uD0DC \uBBF8\uD655\uC778"
                : statusText.Trim();
            ModelCenterTrainingDetailText = string.IsNullOrWhiteSpace(detailText)
                ? "\uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694."
                : detailText.Trim();
        }

        public void SetModelCenterRecoveryState(string titleText, string detailText, string actionText)
        {
            ModelCenterRecoveryTitleText = (titleText ?? string.Empty).Trim();
            ModelCenterRecoveryDetailText = (detailText ?? string.Empty).Trim();
            ModelCenterRecoveryActionText = (actionText ?? string.Empty).Trim();
            IsModelCenterRecoveryVisible = !string.IsNullOrWhiteSpace(ModelCenterRecoveryTitleText)
                || !string.IsNullOrWhiteSpace(ModelCenterRecoveryDetailText)
                || !string.IsNullOrWhiteSpace(ModelCenterRecoveryActionText);
        }

        public void ClearModelCenterRecoveryState()
        {
            SetModelCenterRecoveryState(string.Empty, string.Empty, string.Empty);
        }

        public void SetModelCenterAnomalyEvaluationState(WpfAnomalyClassificationEvaluationPresentation presentation)
        {
            if (presentation == null)
            {
                ClearModelCenterAnomalyEvaluationState();
                return;
            }

            ModelCenterAnomalyEvaluationRecommendationText = (presentation.RecommendationText ?? string.Empty).Trim();
            ModelCenterAnomalyEvaluationMetricsText = (presentation.MetricsText ?? string.Empty).Trim();
            ModelCenterAnomalyEvaluationDetailText = (presentation.DetailText ?? string.Empty).Trim();
            ModelCenterAnomalyEvaluationActionText = (presentation.ActionText ?? string.Empty).Trim();
            IsModelCenterAnomalyEvaluationVisible =
                !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationRecommendationText)
                || !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationMetricsText)
                || !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationDetailText)
                || !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationActionText);
        }

        public void ClearModelCenterAnomalyEvaluationState()
        {
            ModelCenterAnomalyEvaluationRecommendationText = string.Empty;
            ModelCenterAnomalyEvaluationMetricsText = string.Empty;
            ModelCenterAnomalyEvaluationDetailText = string.Empty;
            ModelCenterAnomalyEvaluationActionText = string.Empty;
            IsModelCenterAnomalyEvaluationVisible = false;
        }

        public void SetModelCenterModelState(
            string currentModelText,
            string candidateModelText,
            string adoptionText,
            string nextActionText,
            string confirmModelButtonText = null,
            string confirmModelButtonToolTip = null,
            bool canConfirmModel = false,
            string decisionSummaryText = null,
            string decisionEvidenceText = null,
            string decisionActionText = null,
            string runtimeActionText = null)
        {
            ModelCenterCurrentModelText = string.IsNullOrWhiteSpace(currentModelText)
                ? "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : currentModelText.Trim();
            ModelCenterCandidateModelText = string.IsNullOrWhiteSpace(candidateModelText)
                ? "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C"
                : candidateModelText.Trim();
            ModelCenterAdoptionText = string.IsNullOrWhiteSpace(adoptionText)
                ? "\uBAA8\uB378 \uC801\uC6A9: \uB300\uAE30"
                : adoptionText.Trim();
            ModelCenterNextActionText = string.IsNullOrWhiteSpace(nextActionText)
                ? "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694."
                : nextActionText.Trim();
            ModelCenterCurrentModelDetailText = StripModelCenterPrefix(
                ModelCenterCurrentModelText,
                "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378:",
                "\uAC80\uC0AC \uBAA8\uB378 \uD6C4\uBCF4:");
            ModelCenterCandidateModelDetailText = StripModelCenterPrefix(
                ModelCenterCandidateModelText,
                "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4:");
            ModelCenterAdoptionDetailText = StripModelCenterPrefix(
                ModelCenterAdoptionText,
                "\uBAA8\uB378 \uC801\uC6A9:");
            ModelCenterNextActionDetailText = StripModelCenterPrefix(
                ModelCenterNextActionText,
                "\uB2E4\uC74C:");
            ModelCenterDecisionSummaryText = string.IsNullOrWhiteSpace(decisionSummaryText)
                ? "\uD310\uB2E8: " + ModelCenterAdoptionDetailText
                : decisionSummaryText.Trim();
            ModelCenterDecisionEvidenceText = string.IsNullOrWhiteSpace(decisionEvidenceText)
                ? $"\uADFC\uAC70: \uAC80\uC0AC \uBAA8\uB378 {ModelCenterCurrentModelDetailText} / \uD559\uC2B5 \uACB0\uACFC {ModelCenterCandidateModelDetailText}"
                : decisionEvidenceText.Trim();
            ModelCenterDecisionActionText = string.IsNullOrWhiteSpace(decisionActionText)
                ? $"\uD655\uC815: {ModelCenterNextActionDetailText}"
                : decisionActionText.Trim();
            ModelCenterRuntimeActionText = (runtimeActionText ?? string.Empty).Trim();
            RefreshModelCenterInspectCurrentImageState();
            ModelCenterConfirmModelButtonText = string.IsNullOrWhiteSpace(confirmModelButtonText)
                ? "\uD6C4\uBCF4 \uC5C6\uC74C"
                : confirmModelButtonText.Trim();
            modelCenterConfirmModelBaseToolTip = string.IsNullOrWhiteSpace(confirmModelButtonToolTip)
                ? "\uD655\uC815\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4."
                : confirmModelButtonToolTip.Trim();
            isModelCenterConfirmModelAvailable = canConfirmModel;
            RefreshModelCenterConfirmModelEnabled();
        }

        public void SetModelCenterCandidateReviewState(
            string reviewCandidateButtonText,
            string reviewCandidateButtonToolTip,
            bool canReviewCandidate)
        {
            ModelCenterReviewCandidateButtonText = string.IsNullOrWhiteSpace(reviewCandidateButtonText)
                ? "\uD6C4\uBCF4 \uC5C6\uC74C"
                : reviewCandidateButtonText.Trim();
            ModelCenterReviewCandidateButtonToolTip = string.IsNullOrWhiteSpace(reviewCandidateButtonToolTip)
                ? "\uAC80\uD1A0\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4."
                : reviewCandidateButtonToolTip.Trim();
            isModelCenterReviewCandidateAvailable = canReviewCandidate;
            RefreshModelCenterReviewCandidateEnabled();
        }

        public void SetModelRegistryState(WpfModelRegistryPresentation presentation)
        {
            ModelRegistrySummaryPrimaryText = string.IsNullOrWhiteSpace(presentation?.SummaryPrimaryText)
                ? "\uD604\uC7AC \uAC80\uC0AC: \uC5C6\uC74C / \uD559\uC2B5 \uD6C4\uBCF4: \uC5C6\uC74C"
                : presentation.SummaryPrimaryText.Trim();
            ModelRegistrySummarySecondaryText = string.IsNullOrWhiteSpace(presentation?.SummarySecondaryText)
                ? "YOLOv5 / \uCD5C\uADFC \uD559\uC2B5 \uC5C6\uC74C / \uC774\uB825 0\uAC74 / \uD6C4\uBCF4 \uC5C6\uC74C"
                : presentation.SummarySecondaryText.Trim();
            ModelRegistryProfileText = string.IsNullOrWhiteSpace(presentation?.ProfileText)
                ? "\uBAA8\uB378 \uD504\uB85C\uD544: \uBBF8\uC124\uC815"
                : presentation.ProfileText.Trim();
            ModelRegistryTrainingRunText = string.IsNullOrWhiteSpace(presentation?.TrainingRunText)
                ? "\uCD5C\uADFC \uD559\uC2B5 \uC2E4\uD589: \uC5C6\uC74C"
                : presentation.TrainingRunText.Trim();
            ModelRegistryCandidateModelText = string.IsNullOrWhiteSpace(presentation?.CandidateModelText)
                ? "\uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C"
                : presentation.CandidateModelText.Trim();
            ModelRegistryInspectionModelText = string.IsNullOrWhiteSpace(presentation?.InspectionModelText)
                ? "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : presentation.InspectionModelText.Trim();
            ModelRegistryActionText = string.IsNullOrWhiteSpace(presentation?.ActionText)
                ? "\uAD6C\uC870: \uBAA8\uB378 \uD504\uB85C\uD544 -> \uD559\uC2B5 \uC2E4\uD589 -> \uD6C4\uBCF4 \uBAA8\uB378 -> \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uBD84\uB9AC\uD574 \uAD00\uB9AC\uD569\uB2C8\uB2E4."
                : presentation.ActionText.Trim();
            SetModelRegistryHistoryItems(presentation?.HistoryItems);
        }

        private void SetModelRegistryHistoryItems(System.Collections.Generic.IEnumerable<WpfModelRegistryHistoryItem> items)
        {
            string selectedCandidateId = SelectedModelRegistryHistoryItem?.CandidateId ?? string.Empty;
            string selectedWeightsPath = SelectedModelRegistryHistoryItem?.WeightsPath ?? string.Empty;
            ModelRegistryHistoryItems.Clear();
            foreach (WpfModelRegistryHistoryItem item in items ?? Array.Empty<WpfModelRegistryHistoryItem>())
            {
                if (item != null)
                {
                    ModelRegistryHistoryItems.Add(item);
                }
            }

            int count = ModelRegistryHistoryItems.Count;
            IsModelRegistryHistoryVisible = count > 0;
            ModelRegistryHistoryHeaderText = count <= 0
                ? "\uCD5C\uADFC \uBAA8\uB378 \uC774\uB825 0\uAC74"
                : $"\uCD5C\uADFC \uBAA8\uB378 \uC774\uB825 {count}\uAC74";
            ModelRegistryHistorySummaryText = count <= 0
                ? "\uD559\uC2B5 \uD6C4\uBCF4\uB97C \uC800\uC7A5\uD558\uAC70\uB098 \uAC70\uC808\uD558\uBA74 \uC5EC\uAE30\uC5D0 \uCD5C\uADFC \uC774\uB825\uC774 \uD45C\uC2DC\uB429\uB2C8\uB2E4."
                : "\uD559\uC2B5 run, \uD6C4\uBCF4 \uBAA8\uB378, \uC9C0\uD45C, \uCC44\uD0DD/\uAC70\uC808 \uACB0\uC815\uC744 \uD568\uAED8 \uBE44\uAD50\uD569\uB2C8\uB2E4.";
            SelectedModelRegistryHistoryItem = FindModelRegistryHistorySelection(selectedCandidateId, selectedWeightsPath)
                ?? ModelRegistryHistoryItems.FirstOrDefault();
        }

        private WpfModelRegistryHistoryItem FindModelRegistryHistorySelection(string candidateId, string weightsPath)
        {
            if (!string.IsNullOrWhiteSpace(candidateId))
            {
                WpfModelRegistryHistoryItem byCandidate = ModelRegistryHistoryItems.FirstOrDefault(item =>
                    string.Equals(item?.CandidateId, candidateId, StringComparison.Ordinal));
                if (byCandidate != null)
                {
                    return byCandidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(weightsPath))
            {
                return ModelRegistryHistoryItems.FirstOrDefault(item =>
                    string.Equals(item?.WeightsPath, weightsPath, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private void RefreshSelectedModelHistoryState()
        {
            WpfModelRegistryHistoryItem item = SelectedModelRegistryHistoryItem;
            IsSelectedModelHistoryVisible = item != null;
            if (item == null)
            {
                SelectedModelHistoryTitleText = "\uBAA8\uB378 \uC774\uB825 \uC120\uD0DD \uC5C6\uC74C";
                SelectedModelHistoryDetailText = "\uC774\uB825\uC744 \uC120\uD0DD\uD558\uBA74 \uAC00\uC911\uCE58, \uC9C0\uD45C, \uC801\uC6A9 \uAC00\uB2A5 \uC5EC\uBD80\uB97C \uD655\uC778\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                SelectedModelHistoryMetricText = string.Empty;
                SelectedModelHistoryDecisionText = string.Empty;
                RefreshSelectedModelHistoryComparisonState(null);
                SelectedModelHistoryActionText = "\uC801\uC6A9 \uBD88\uAC00";
                SelectedModelHistoryActionToolTip = "\uC801\uC6A9\uD560 \uBAA8\uB378 \uC774\uB825\uC744 \uBA3C\uC800 \uC120\uD0DD\uD558\uC138\uC694.";
                isSelectedModelHistoryActionAvailable = false;
                RefreshSelectedModelHistoryActionEnabled();
                return;
            }

            SelectedModelHistoryTitleText = string.IsNullOrWhiteSpace(item.TitleText)
                ? "\uBAA8\uB378 \uC774\uB825"
                : item.TitleText.Trim();
            SelectedModelHistoryDetailText = string.IsNullOrWhiteSpace(item.DetailText)
                ? item.WeightsPath ?? string.Empty
                : item.DetailText.Trim();
            SelectedModelHistoryMetricText = item.MetricText ?? string.Empty;
            SelectedModelHistoryDecisionText = item.DecisionText ?? string.Empty;
            RefreshSelectedModelHistoryComparisonState(item);
            SelectedModelHistoryActionText = string.IsNullOrWhiteSpace(item.ActionText)
                ? "\uC801\uC6A9 \uBD88\uAC00"
                : item.ActionText.Trim();
            SelectedModelHistoryActionToolTip = string.IsNullOrWhiteSpace(item.ActionToolTip)
                ? "\uC120\uD0DD\uD55C \uBAA8\uB378 \uC774\uB825\uC744 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4."
                : item.ActionToolTip.Trim();
            isSelectedModelHistoryActionAvailable = item.CanPromoteToInspectionModel;
            RefreshSelectedModelHistoryActionEnabled();
        }

        private void RefreshSelectedModelHistoryComparisonState(WpfModelRegistryHistoryItem selected)
        {
            SelectedModelHistoryComparisonTitleText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uC120\uD0DD \uC774\uB825 \uBAA8\uB378";
            if (selected == null)
            {
                SelectedModelHistoryCurrentModelText = string.Empty;
                SelectedModelHistorySelectedModelText = string.Empty;
                SelectedModelHistoryComparisonMetricText = string.Empty;
                return;
            }

            WpfModelRegistryHistoryItem current = ModelRegistryHistoryItems.FirstOrDefault(item => item?.IsCurrentInspectionModel == true);
            SelectedModelHistoryCurrentModelText = current == null
                ? "\uD604\uC7AC \uAC80\uC0AC: \uB4F1\uB85D\uB41C \uC774\uB825 \uC5C6\uC74C"
                : BuildModelHistoryComparisonRowText("\uD604\uC7AC \uAC80\uC0AC", current);
            SelectedModelHistorySelectedModelText = selected.IsCurrentInspectionModel
                ? "\uC120\uD0DD \uC774\uB825: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uAC19\uC74C"
                : BuildModelHistoryComparisonRowText("\uC120\uD0DD \uC774\uB825", selected);
            SelectedModelHistoryComparisonMetricText = BuildModelHistoryComparisonMetricText(current, selected);
        }

        private static string BuildModelHistoryComparisonRowText(string titleText, WpfModelRegistryHistoryItem item)
        {
            if (item == null)
            {
                return $"{titleText}: \uC5C6\uC74C";
            }

            string title = string.IsNullOrWhiteSpace(item.TitleText)
                ? item.WeightsPath ?? string.Empty
                : item.TitleText.Trim();
            string decision = string.IsNullOrWhiteSpace(item.DecisionText)
                ? "\uACB0\uC815 \uBBF8\uD655\uC778"
                : item.DecisionText.Trim();
            return $"{titleText}: {title} / {decision}";
        }

        private static string BuildModelHistoryComparisonMetricText(WpfModelRegistryHistoryItem current, WpfModelRegistryHistoryItem selected)
        {
            if (selected == null)
            {
                return string.Empty;
            }

            if (current == null)
            {
                return "\uC9C0\uD45C \uBE44\uAD50: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uC774\uB825\uC774 \uC5C6\uC5B4 \uC120\uD0DD \uC774\uB825\uB9CC \uD655\uC778\uD569\uB2C8\uB2E4.";
            }

            bool isSameCandidate = !string.IsNullOrWhiteSpace(current.CandidateId)
                && string.Equals(current.CandidateId, selected.CandidateId, StringComparison.Ordinal);
            bool isSameWeights = !string.IsNullOrWhiteSpace(current.WeightsPath)
                && string.Equals(current.WeightsPath, selected.WeightsPath, StringComparison.OrdinalIgnoreCase);
            if (selected.IsCurrentInspectionModel || isSameCandidate || isSameWeights)
            {
                return "\uC9C0\uD45C \uBE44\uAD50: \uC120\uD0DD \uC774\uB825\uC774 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC785\uB2C8\uB2E4.";
            }

            string currentMetric = string.IsNullOrWhiteSpace(current.MetricText)
                ? "\uC9C0\uD45C \uC5C6\uC74C"
                : current.MetricText.Trim();
            string selectedMetric = string.IsNullOrWhiteSpace(selected.MetricText)
                ? "\uC9C0\uD45C \uC5C6\uC74C"
                : selected.MetricText.Trim();
            return $"\uC9C0\uD45C \uBE44\uAD50: \uD604\uC7AC {currentMetric} / \uC120\uD0DD \uC774\uB825 {selectedMetric}";
        }

        private void RefreshSelectedModelHistoryActionEnabled()
        {
            IsSelectedModelHistoryActionEnabled = isSelectedModelHistoryActionAvailable && canRunModelCenterCommands;
            if (SelectedModelRegistryHistoryItem != null
                && (IsSelectedModelHistoryActionEnabled || string.IsNullOrWhiteSpace(modelCenterConfirmModelUnavailableToolTip)))
            {
                SelectedModelHistoryActionToolTip = string.IsNullOrWhiteSpace(SelectedModelRegistryHistoryItem.ActionToolTip)
                    ? "\uC120\uD0DD\uD55C \uBAA8\uB378 \uC774\uB825\uC744 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4."
                    : SelectedModelRegistryHistoryItem.ActionToolTip.Trim();
            }

            if (isSelectedModelHistoryActionAvailable
                && !IsSelectedModelHistoryActionEnabled
                && !string.IsNullOrWhiteSpace(modelCenterConfirmModelUnavailableToolTip))
            {
                SelectedModelHistoryActionToolTip = modelCenterConfirmModelUnavailableToolTip;
            }
        }

        public void SetDatasetContext(
            string datasetName,
            string purposeText,
            string outputRootPath,
            string imageRootPath,
            bool canOpenDatasetFolder,
            int classCount = 0)
        {
            WpfDatasetContextPresentation presentation = WpfDatasetContextPresentationService.Build(
                datasetName,
                purposeText,
                outputRootPath,
                imageRootPath,
                classCount);

            CurrentDatasetName = presentation.DatasetName;
            CurrentDatasetPurposeText = presentation.PurposeText;
            CurrentDatasetPathText = presentation.CombinedPathText;
            CurrentDatasetStoragePathText = presentation.StoragePathText;
            CurrentDatasetImageRootText = presentation.ImageRootText;
            CurrentDatasetSourceText = presentation.SourceText;
            CurrentDatasetToolTip = presentation.Tooltip;
            IsOpenDatasetFolderEnabled = canOpenDatasetFolder;
        }

        private static string StripModelCenterPrefix(string text, params string[] prefixes)
        {
            string normalized = (text ?? string.Empty).Trim();
            foreach (string prefix in prefixes ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(prefix)
                    && normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(prefix.Length).Trim();
                }
            }

            return normalized;
        }

        private void RefreshModelCenterConfirmModelEnabled()
        {
            IsModelCenterConfirmModelEnabled = isModelCenterConfirmModelAvailable && canRunModelCenterCommands;
            RefreshModelCenterConfirmModelToolTip();
            RefreshModelCenterActionStateText();
        }

        private void RefreshModelCenterReviewCandidateEnabled()
        {
            IsModelCenterReviewCandidateEnabled = isModelCenterReviewCandidateAvailable && canRunModelCenterReviewCommands;
            RefreshModelCenterActionStateText();
        }

        private void RefreshModelCenterInspectCurrentImageState()
        {
            ModelCenterInspectCurrentImageButtonText = "\uD604\uC7AC \uAC80\uC0AC";
            string currentModel = ModelCenterCurrentModelDetailText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentModel)
                || string.Equals(currentModel, "\uC5C6\uC74C", StringComparison.OrdinalIgnoreCase)
                || currentModel.Contains("\uD30C\uC77C \uC5C6\uC74C", StringComparison.OrdinalIgnoreCase))
            {
                ModelCenterInspectCurrentImageButtonToolTip = "\uAC80\uC0AC \uBAA8\uB378\uC744 \uC800\uC7A5\uD55C \uB4A4 \uD604\uC7AC \uAC80\uC0AC\uB97C \uC2E4\uD589\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                RefreshModelCenterInspectCurrentImageEnabled();
                return;
            }

            string runtimeText = string.IsNullOrWhiteSpace(ModelCenterRuntimeActionText)
                || currentModel.IndexOf(ModelCenterRuntimeActionText, StringComparison.OrdinalIgnoreCase) >= 0
                ? string.Empty
                : $" \uC2E4\uD589\uAE30: {ModelCenterRuntimeActionText}.";
            ModelCenterInspectCurrentImageButtonToolTip = $"\uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0 {currentModel} \uBAA8\uB378\uB85C \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uACE0 \uACB0\uACFC\uB97C AI \uD6C4\uBCF4/\uCE94\uBC84\uC2A4\uC5D0 \uD45C\uC2DC\uD569\uB2C8\uB2E4.{runtimeText}";
            RefreshModelCenterInspectCurrentImageEnabled();
        }

        private void RefreshModelCenterInspectCurrentImageEnabled()
        {
            IsModelCenterInspectCurrentImageEnabled = IsCurrentImageDetectionEnabled && canRunModelCenterReviewCommands;
            RefreshModelCenterActionStateText();
        }

        private void RefreshModelCenterConfirmModelToolTip()
        {
            if (isModelCenterConfirmModelAvailable
                && !IsModelCenterConfirmModelEnabled
                && !string.IsNullOrWhiteSpace(modelCenterConfirmModelUnavailableToolTip))
            {
                ModelCenterConfirmModelButtonToolTip = modelCenterConfirmModelUnavailableToolTip;
                return;
            }

            ModelCenterConfirmModelButtonToolTip = modelCenterConfirmModelBaseToolTip;
        }

        private void RefreshModelCenterActionStateText()
        {
            string runtimeStateText = string.IsNullOrWhiteSpace(ModelCenterRuntimeActionText)
                ? string.Empty
                : $"\uC2E4\uD589\uAE30: {ModelCenterRuntimeActionText} / ";
            ModelCenterActionStateText = runtimeStateText
                + ModelCenterActionRouteText
                + " / \uBC84\uD2BC \uC0C1\uD0DC: "
                + BuildModelCenterActionStatePart(
                    ModelCenterReviewCandidateButtonText,
                    IsModelCenterReviewCandidateEnabled,
                    isModelCenterReviewCandidateAvailable,
                    canRunModelCenterReviewCommands,
                    ModelCenterReviewCandidateButtonToolTip)
                + " / "
                + BuildModelCenterActionStatePart(
                    ModelCenterConfirmModelButtonText,
                    IsModelCenterConfirmModelEnabled,
                    isModelCenterConfirmModelAvailable,
                    canRunModelCenterCommands,
                    ModelCenterConfirmModelButtonToolTip)
                + " / "
                + BuildModelCenterActionStatePart(
                    ModelCenterInspectCurrentImageButtonText,
                    IsModelCenterInspectCurrentImageEnabled,
                    true,
                    canRunModelCenterReviewCommands && IsCurrentImageDetectionEnabled,
                    ModelCenterInspectCurrentImageButtonToolTip);
        }

        private static string BuildModelCenterActionStatePart(
            string buttonText,
            bool isEnabled,
            bool isAvailable,
            bool canRun,
            string toolTip)
        {
            string label = CompactModelCenterActionLabel(buttonText);
            if (isEnabled)
            {
                return $"{label} \uAC00\uB2A5";
            }

            string reason = !isAvailable
                ? "\uB300\uC0C1 \uC5C6\uC74C"
                : !canRun
                    ? BuildModelCenterUnavailableReason(toolTip, "\uC791\uC5C5 \uC870\uAC74 \uD655\uC778")
                    : BuildModelCenterUnavailableReason(toolTip, "\uC900\uBE44 \uD544\uC694");
            return $"{label} \uB300\uAE30: {reason}";
        }

        private static string CompactModelCenterActionLabel(string buttonText)
        {
            string text = (buttonText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "\uBC84\uD2BC";
            }

            if (text.Contains("\uD6C4\uBCF4", StringComparison.Ordinal))
            {
                return "\uD6C4\uBCF4 \uAC80\uC99D";
            }

            if (text.Contains("\uC800\uC7A5", StringComparison.Ordinal))
            {
                return "\uAC80\uC0AC \uBAA8\uB378 \uC800\uC7A5";
            }

            if (text.Contains("\uAC80\uC0AC", StringComparison.Ordinal))
            {
                return "\uD604\uC7AC \uAC80\uC0AC";
            }

            return text.Length > 16
                ? text.Substring(0, 16) + "..."
                : text;
        }

        private static string BuildModelCenterUnavailableReason(string toolTip, string fallback)
        {
            string text = (toolTip ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            if (text.Contains("\uAC80\uD1A0\uD560", StringComparison.Ordinal)
                && text.Contains("\uC5C6\uC2B5\uB2C8\uB2E4", StringComparison.Ordinal))
            {
                return "\uD559\uC2B5 \uD6C4\uBCF4 \uC5C6\uC74C";
            }

            if (text.Contains("\uD655\uC815\uD560", StringComparison.Ordinal)
                && text.Contains("\uC5C6\uC2B5\uB2C8\uB2E4", StringComparison.Ordinal))
            {
                return "\uC800\uC7A5\uD560 \uD6C4\uBCF4 \uC5C6\uC74C";
            }

            if (text.Contains("recipe", StringComparison.OrdinalIgnoreCase))
            {
                return "recipe \uC800\uC7A5 \uC870\uAC74 \uD655\uC778";
            }

            if (text.Contains("\uAC80\uC0AC \uBAA8\uB378\uC744 \uC800\uC7A5", StringComparison.Ordinal))
            {
                return "\uAC80\uC0AC \uBAA8\uB378 \uC800\uC7A5 \uD544\uC694";
            }

            if (text.Contains("\uD604\uC7AC \uC774\uBBF8\uC9C0", StringComparison.Ordinal))
            {
                return "\uD604\uC7AC \uC774\uBBF8\uC9C0/\uBAA8\uB378 \uD655\uC778";
            }

            return text.Length > 30
                ? text.Substring(0, 30) + "..."
                : text;
        }
    }
}
