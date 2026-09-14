using MahApps.Metro.IconPacks;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfLearningWorkflowPanelViewModel : WpfObservableViewModel, IDisposable
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<WpfYoloTrainingWorkflowStepItem> NoOpTrainingStepCommand = _ => { };
        private static readonly Action<WpfFirstRunChecklistItem> NoOpFirstRunSamplePathCommand = _ => { };
        private static readonly Action<WpfDatasetDashboardMetricItem> NoOpDatasetDashboardMetricCommand = _ => { };
        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }

        private WpfLearningModeItem selectedMode;
        private WpfLearningModeItem selectedDatasetPurposeMode;
        private WpfAnnotationToolItem selectedTool;
        private WpfLearningStepItem selectedStep;
        private string datasetPurposeSummaryText = string.Empty;
        private string datasetPurposeToolSummaryText = string.Empty;
        private string datasetSetupFirstActionText = "\uCC98\uC74C \uC2DC\uC791: \uBAA9\uC801\uC744 \uACE0\uB974\uACE0 \uB370\uC774\uD130\uC14B\uC744 \uBA3C\uC800 \uC900\uBE44\uD558\uC138\uC694.";
        private string datasetSetupActionText = "\uB370\uC774\uD130\uC14B \uC2DC\uC791";
        private string currentWorkflowActionText = string.Empty;
        private string datasetSetupStatusText = "\uB370\uC774\uD130\uC14B \uC2DC\uC791 \uC804";
        private string currentLabelingTaskStepText = "\uC0D8\uD50C";
        private string currentLabelingTaskToolText = "\uB3C4\uAD6C: \uC120\uD0DD";
        private string currentLabelingTaskActionText = "\uC774\uBBF8\uC9C0 \uD050\uC5D0\uC11C \uC791\uC5C5\uD560 \uC774\uBBF8\uC9C0\uB97C \uC5F4\uACE0 \uCCAB \uB77C\uBCA8\uC744 \uC2DC\uC791\uD558\uC138\uC694.";
        private string currentLabelingTaskChecklistFirstText = "1  \uC774\uBBF8\uC9C0";
        private string currentLabelingTaskChecklistSecondText = "2  \uC5F4\uAE30";
        private string currentLabelingTaskChecklistThirdText = "3  \uB77C\uBCA8";
        private string currentLabelingTaskChecklistSummaryText = "\uD750\uB984: \uC774\uBBF8\uC9C0 > \uC5F4\uAE30 > \uB77C\uBCA8";
        private Visibility datasetOnboardingVisibility = Visibility.Visible;
        private Visibility labelingTaskVisibility = Visibility.Collapsed;
        private string modeDetailText = string.Empty;
        private string stepDetailText = string.Empty;
        private string toolDetailText = string.Empty;
        private string trainingChecklistStatusText = T("WpfLearningWorkflow.TrainingChecklist.Status.Initial");
        private string trainingChecklistDetailText = T("WpfLearningWorkflow.TrainingChecklist.Detail.Initial");
        private string trainingChecklistActionText = TrainingChecklistLocalizationService.CreateInitialAction().ActionText;
        private string datasetDashboardStatusText = T("WpfLearningWorkflow.DatasetDashboard.Status.Before");
        private string datasetDashboardSummaryText = T("WpfLearningWorkflow.DatasetDashboard.Summary.Before");
        private string datasetDashboardActionText = T("WpfLearningWorkflow.DatasetDashboardAction.Initial");
        private string externalEvaluationDataAuditStatusText = "\uC678\uBD80 \uD3C9\uAC00 \uD3F4\uB354\uB97C \uB300\uC870\uD558\uBA74 \uD559\uC2B5 \uB370\uC774\uD130\uC640\uC758 \uC911\uBCF5\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.";
        private string externalEvaluationDataAuditDetailText = string.Empty;
        private string externalEvaluationDataAuditPathText = string.Empty;
        private WpfLearningModeItem selectedExternalYoloDatasetPurposeMode;
        private string externalYoloDatasetIntakeStatusText = "\uC678\uBD80 YOLO data.yaml: \uC120\uD0DD \uC548 \uD568";
        private string externalYoloDatasetIntakeDetailText = "\uB0B4\uBD80 \uB77C\uBCA8\uB9C1 \uB370\uC774\uD130\uC640 \uBD84\uB9AC\uB41C \uC6D0\uBCF8 YOLO \uB370\uC774\uD130\uC14B\uC744 \uAC80\uC99D\uD55C \uB4A4 \uB2E4\uC74C \uD559\uC2B5\uC5D0\uB9CC \uC0AC\uC6A9\uD569\uB2C8\uB2E4.";
        private string externalYoloDatasetIntakePathText = string.Empty;
        private string objectDetectionMvpNextActionText = T("WpfLearningWorkflow.ObjectDetectionMvpNextAction.Empty");
        private string modelReplacementStatusText = ModelReplacementLocalizationService.CreateInitial().StatusText;
        private string modelReplacementDetailText = ModelReplacementLocalizationService.CreateInitial().DetailText;
        private string trainingHistoryText = string.Empty;
        private string trainingResultComparisonSummaryText = string.Empty;
        private string trainingResultComparisonText = string.Empty;
        private string trainingModelAdoptionDecisionText = string.Empty;
        private string trainingModelLifecycleCurrentText = T("WpfLearningWorkflow.TrainingModelLifecycle.Current.Initial");
        private string trainingModelLifecycleCandidateText = T("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Initial");
        private string trainingModelLifecycleDecisionText = T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Initial");
        private string trainingModelLifecycleNextActionText = T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial");
        private string runModelComparisonActionText = string.Empty;
        private string runModelComparisonToolTipText = string.Empty;
        private string modelComparisonBasisText = string.Empty;
        private string trainingHistorySourceText = string.Empty;
        private string trainingResultComparisonSummarySourceText = string.Empty;
        private string trainingResultComparisonSourceText = string.Empty;
        private string trainingModelAdoptionDecisionSourceText = string.Empty;
        private string runModelComparisonActionSourceText = string.Empty;
        private string runModelComparisonToolTipSourceText = string.Empty;
        private string modelComparisonBasisSourceText = string.Empty;
        private bool isRunModelComparisonEnabled = true;
        private WpfYoloTrainingWorkflowStepItem currentYoloTrainingStep;
        private string currentYoloTrainingStepTitleText = string.Empty;
        private string currentYoloTrainingStepDetailText = string.Empty;
        private string currentYoloTrainingActionText = string.Empty;
        private bool hasCurrentYoloTrainingStep;
        private bool isYoloFixClassesEnabled = true;
        private bool isYoloFixLabelsEnabled;
        private bool isYoloFixDatasetEnabled = true;
        private int brushSize = CanvasBrushSizePresentationService.DefaultSize;
        private double maskOpacity = 0.66;
        private ICommand datasetPurposeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand datasetSetupStartCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand datasetOpenExistingCommand = new RelayCommand(NoOpCommand);
        private ICommand learningModeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand annotationToolSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand learningStepSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand yoloTrainingWorkflowStepCommand = new RelayCommand<WpfYoloTrainingWorkflowStepItem>(NoOpTrainingStepCommand);
        private ICommand firstRunSamplePathCommand = new RelayCommand<WpfFirstRunChecklistItem>(NoOpFirstRunSamplePathCommand);
        private ICommand datasetDashboardMetricCommand = new RelayCommand<WpfDatasetDashboardMetricItem>(NoOpDatasetDashboardMetricCommand);
        private ICommand tutorialOpenHtmlGuideCommand = new RelayCommand(NoOpCommand);
        private ICommand yoloFixClassesCommand = new RelayCommand(NoOpCommand);
        private ICommand yoloFixLabelsCommand = new RelayCommand(NoOpCommand);
        private ICommand yoloFixDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand runModelComparisonCommand = new RelayCommand(NoOpCommand);
        private ICommand externalEvaluationDataAuditCommand = new RelayCommand(NoOpCommand);
        private ICommand historicalSegmentationRemediationAuditCommand = new RelayCommand(NoOpCommand);
        private ICommand selectExternalYoloDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand activateExternalYoloDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand clearExternalYoloDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand templateCurrentImageCommand = new RelayCommand(NoOpCommand);
        private ICommand templateBatchCommand = new RelayCommand(NoOpCommand);
        private Action<WpfLearningModeWorkflowAction> learningModeWorkflowAction = _ => { };
        private Action<WpfLearningStepWorkflowAction> learningStepWorkflowAction = _ => { };
        private Action<WpfAnnotationToolItem> annotationToolSelectionAction = _ => { };
        private Func<WpfLearningModeItem, bool> datasetPurposeSelectionGuard = _ => true;
        private Action<LabelingDatasetPurpose> datasetPurposeSelectionAction = _ => { };
        private ExternalAuditWorkflowService externalAuditWorkflowService;
        private Func<string> externalEvaluationDataAuditInitialDirectoryProvider;
        private Func<string, string> externalEvaluationDataAuditDirectorySelector;
        private Func<IReadOnlyList<string>> externalEvaluationDataAuditReferenceDirectoriesProvider;
        private Func<bool> externalEvaluationDataAuditCloseApproval;
        private Action<string> externalEvaluationDataAuditStatusSink;
        private Func<LabelingProjectData> historicalSegmentationRemediationDataProvider;
        private Func<string> historicalSegmentationRemediationSourceImagePathProvider;
        private Func<bool> historicalSegmentationRemediationCloseApproval;
        private Action<string, string> historicalSegmentationRemediationStatusSink;
        private ExternalYoloDatasetIntakeWorkflowService externalYoloDatasetIntakeWorkflowService;
        private Func<ExternalYoloDatasetIntakeCallbacks> externalYoloDatasetIntakeCallbacksProvider;
        private ModelComparisonWorkflowService modelComparisonWorkflowService;
        private Func<ModelComparisonCallbacks> modelComparisonCallbacksProvider;
        private TrainingChecklistLocalizationSnapshot trainingChecklistLocalizationSnapshot;
        private TrainingChecklistActionLocalizationSnapshot trainingChecklistActionLocalizationSnapshot;
        private ModelReplacementLocalizationSnapshot modelReplacementLocalizationSnapshot;
        private TrainingModelLifecycleLocalizationSnapshot trainingModelLifecycleLocalizationSnapshot;
        private TrainingComparisonLocalizationSnapshot trainingComparisonLocalizationSnapshot;
        private DatasetDashboardLocalizationSnapshot datasetDashboardLocalizationSnapshot;
        private bool refreshingTrainingChecklistLocalization;
        private bool refreshingModelReplacementLocalization;
        private bool refreshingTrainingModelLifecycleLocalization;
        private bool refreshingTrainingComparisonLocalization;
        private bool disposed;

        public event Action<string> ExternalEvaluationDataAuditStatusChanged;

        public WpfLearningWorkflowPanelViewModel()
        {
            modelReplacementLocalizationSnapshot = ModelReplacementLocalizationService.CreateInitial();
            trainingModelLifecycleLocalizationSnapshot = TrainingModelLifecycleLocalizationService.CreateInitial();
            trainingComparisonLocalizationSnapshot = TrainingComparisonLocalizationService.CreateInitial();
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;

            SetTrainingComparisonLocalization(trainingComparisonLocalizationSnapshot);

            foreach (WpfLearningModeItem mode in LearningWorkflowCatalogService.BuildLearningModes())
            {
                LearningModes.Add(mode);
            }
            DatasetPurposeModes.Add(LearningModes.First(item => item.Mode == WpfLearningMode.ObjectDetection));
            DatasetPurposeModes.Add(LearningModes.First(item => item.Mode == WpfLearningMode.Segmentation));
            DatasetPurposeModes.Add(LearningModes.First(item => item.Mode == WpfLearningMode.AnomalyDetection));
            ExternalYoloDatasetPurposeModes.Add(DatasetPurposeModes.First(item => item.Mode == WpfLearningMode.ObjectDetection));
            ExternalYoloDatasetPurposeModes.Add(DatasetPurposeModes.First(item => item.Mode == WpfLearningMode.Segmentation));
            SelectedExternalYoloDatasetPurposeMode = ExternalYoloDatasetPurposeModes.FirstOrDefault();

            foreach (WpfAnnotationToolItem tool in LearningWorkflowCatalogService.BuildAnnotationTools())
            {
                RegisterAnnotationTool(tool);
            }
            ApplyDatasetPurpose(LabelingDatasetPurpose.ObjectDetection);

            foreach (WpfLearningStepItem step in LearningWorkflowCatalogService.BuildLearningSteps())
            {
                LearningSteps.Add(step);
            }

            foreach (WpfTemplateWorkflowStepItem step in LearningWorkflowCatalogService.BuildTemplateWorkflowSteps())
            {
                TemplateWorkflowSteps.Add(step);
            }

            foreach (WpfFirstRunChecklistItem item in LearningWorkflowCatalogService.BuildFirstRunSamplePathItems())
            {
                FirstRunSamplePathItems.Add(item);
            }

            foreach (WpfFirstRunChecklistItem item in LearningWorkflowCatalogService.BuildFirstRunChecklistItems())
            {
                FirstRunChecklistItems.Add(item);
            }

            foreach (string item in LearningWorkflowCatalogService.BuildTutorialChecklistItems())
            {
                TutorialChecklistItems.Add(item);
            }

            foreach (WpfYoloTrainingWorkflowStepItem step in LearningWorkflowCatalogService.BuildYoloTrainingWorkflowSteps())
            {
                YoloTrainingWorkflowSteps.Add(step);
            }

            // Keep the file-format lesson close to dataset status: saving a box creates a paired label txt, not a hidden app-only object.
            RefreshYoloDatasetStructureItems();

            RefreshCurrentYoloTrainingStep();
            SelectedMode = LearningModes.FirstOrDefault(item => item.Mode == WpfLearningMode.LabelingBasics) ?? LearningModes.FirstOrDefault();
            SelectedTool = SelectableAnnotationTools.FirstOrDefault();
            SelectedStep = LearningSteps.FirstOrDefault();
            SetAnnotationHistoryState(canUndo: false, canRedo: false, undoActionName: string.Empty, redoActionName: string.Empty);
            SetTrainingChecklistLocalization(TrainingChecklistLocalizationService.CreateInitial());
            SetTrainingChecklistActionLocalization(TrainingChecklistLocalizationService.CreateInitialAction());
            DatasetDashboardLocalizationSnapshot initialDashboard = DatasetDashboardLocalizationService.CreateInitial();
            SetDatasetDashboard(
                initialDashboard.StatusText,
                initialDashboard.SummaryText,
                datasetDashboardActionText,
                LearningWorkflowCatalogService.BuildInitialDatasetDashboardMetrics(),
                initialDashboard.IssueItems,
                initialDashboard);
        }

        public string ViewName => nameof(WpfLearningWorkflowPanel);

        public ICommand DatasetPurposeSelectionChangedCommand
        {
            get => datasetPurposeSelectionChangedCommand;
            private set => SetProperty(ref datasetPurposeSelectionChangedCommand, value);
        }

        public ICommand DatasetSetupStartCommand
        {
            get => datasetSetupStartCommand;
            private set => SetProperty(ref datasetSetupStartCommand, value);
        }

        public ICommand DatasetOpenExistingCommand
        {
            get => datasetOpenExistingCommand;
            private set => SetProperty(ref datasetOpenExistingCommand, value);
        }

        public ICommand LearningModeSelectionChangedCommand
        {
            get => learningModeSelectionChangedCommand;
            private set => SetProperty(ref learningModeSelectionChangedCommand, value);
        }

        public ICommand AnnotationToolSelectionChangedCommand
        {
            get => annotationToolSelectionChangedCommand;
            private set => SetProperty(ref annotationToolSelectionChangedCommand, value);
        }

        public ICommand LearningStepSelectionChangedCommand
        {
            get => learningStepSelectionChangedCommand;
            private set => SetProperty(ref learningStepSelectionChangedCommand, value);
        }

        public ICommand YoloTrainingWorkflowStepCommand
        {
            get => yoloTrainingWorkflowStepCommand;
            private set => SetProperty(ref yoloTrainingWorkflowStepCommand, value);
        }

        public ICommand FirstRunSamplePathCommand
        {
            get => firstRunSamplePathCommand;
            private set => SetProperty(ref firstRunSamplePathCommand, value);
        }

        public ICommand DatasetDashboardMetricCommand
        {
            get => datasetDashboardMetricCommand;
            private set => SetProperty(ref datasetDashboardMetricCommand, value);
        }

        public ICommand TutorialOpenHtmlGuideCommand
        {
            get => tutorialOpenHtmlGuideCommand;
            private set => SetProperty(ref tutorialOpenHtmlGuideCommand, value);
        }

        public ICommand YoloFixClassesCommand
        {
            get => yoloFixClassesCommand;
            private set => SetProperty(ref yoloFixClassesCommand, value);
        }

        public ICommand YoloFixLabelsCommand
        {
            get => yoloFixLabelsCommand;
            private set => SetProperty(ref yoloFixLabelsCommand, value);
        }

        public ICommand YoloFixDatasetCommand
        {
            get => yoloFixDatasetCommand;
            private set => SetProperty(ref yoloFixDatasetCommand, value);
        }

        public ICommand RunModelComparisonCommand
        {
            get => runModelComparisonCommand;
            private set => SetProperty(ref runModelComparisonCommand, value);
        }

        public ICommand ExternalEvaluationDataAuditCommand
        {
            get => externalEvaluationDataAuditCommand;
            private set => SetProperty(ref externalEvaluationDataAuditCommand, value);
        }

        public ICommand HistoricalSegmentationRemediationAuditCommand
        {
            get => historicalSegmentationRemediationAuditCommand;
            private set => SetProperty(ref historicalSegmentationRemediationAuditCommand, value);
        }

        public ICommand SelectExternalYoloDatasetCommand
        {
            get => selectExternalYoloDatasetCommand;
            private set => SetProperty(ref selectExternalYoloDatasetCommand, value);
        }

        public ICommand ActivateExternalYoloDatasetCommand
        {
            get => activateExternalYoloDatasetCommand;
            private set => SetProperty(ref activateExternalYoloDatasetCommand, value);
        }

        public ICommand ClearExternalYoloDatasetCommand
        {
            get => clearExternalYoloDatasetCommand;
            private set => SetProperty(ref clearExternalYoloDatasetCommand, value);
        }

        public ICommand TemplateCurrentImageCommand
        {
            get => templateCurrentImageCommand;
            private set => SetProperty(ref templateCurrentImageCommand, value);
        }

        public ICommand TemplateBatchCommand
        {
            get => templateBatchCommand;
            private set => SetProperty(ref templateBatchCommand, value);
        }

        public ObservableCollection<WpfLearningModeItem> LearningModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfLearningModeItem> DatasetPurposeModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfLearningModeItem> ExternalYoloDatasetPurposeModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfAnnotationToolItem> AnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfAnnotationToolItem> SelectableAnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfAnnotationToolItem> AnnotationCommandTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfAnnotationToolItem> VisibleAnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfLearningStepItem> LearningSteps { get; } = new ObservableCollection<WpfLearningStepItem>();

        public ObservableCollection<WpfTemplateWorkflowStepItem> TemplateWorkflowSteps { get; } = new ObservableCollection<WpfTemplateWorkflowStepItem>();

        public ObservableCollection<WpfFirstRunChecklistItem> FirstRunSamplePathItems { get; } = new ObservableCollection<WpfFirstRunChecklistItem>();

        public ObservableCollection<WpfFirstRunChecklistItem> FirstRunChecklistItems { get; } = new ObservableCollection<WpfFirstRunChecklistItem>();

        public ObservableCollection<string> TutorialChecklistItems { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfYoloTrainingWorkflowStepItem> YoloTrainingWorkflowSteps { get; } = new ObservableCollection<WpfYoloTrainingWorkflowStepItem>();

        public ObservableCollection<WpfYoloDatasetStructureItem> YoloDatasetStructureItems { get; } = new ObservableCollection<WpfYoloDatasetStructureItem>();

        public ObservableCollection<WpfDatasetDashboardMetricItem> DatasetDashboardMetrics { get; } = new ObservableCollection<WpfDatasetDashboardMetricItem>();

        public ObservableCollection<string> DatasetDashboardIssueItems { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> TrainingRunHistoryItems { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfTrainingResultReportItem> TrainingResultReportItems { get; } = new ObservableCollection<WpfTrainingResultReportItem>();

        public string TutorialTitleText => "\uCC98\uC74C 10\uBD84 \uD29C\uD1A0\uB9AC\uC5BC";

        public string TutorialSummaryText => "\uAE43\uD5C8\uBE0C\uC5D0\uC11C \uBC1B\uC740 \uD6C4 \uC0D8\uD50C \uC774\uBBF8\uC9C0, \uB77C\uBCA8, \uD559\uC2B5, \uCD94\uB860 \uAC80\uD1A0\uAE4C\uC9C0 \uC544\uB798 \uC21C\uC11C\uB300\uB85C \uB530\uB77C\uD569\uB2C8\uB2E4.";

        public string TutorialHtmlPathText => "HTML: docs/tutorial/labeling-workbench-tutorial.html";

        public string TrainingWorkflowSummaryText => "\uB370\uC774\uD130\uC14B \u2192 \uC774\uBBF8\uC9C0 \u2192 \uD074\uB798\uC2A4 \u2192 \uB77C\uBCA8 \u2192 \uC810\uAC80 \u2192 \uD559\uC2B5 \u2192 \uCD94\uB860/\uAC80\uD1A0";

        public string GuideToolsRoleTitleText => T("WpfLearningWorkflow.GuideToolsRoleTitle");

        public string GuideToolsPrimaryTaskText => T("WpfLearningWorkflow.GuideToolsPrimaryTask");

        public string GuideToolsHelperTaskText => T("WpfLearningWorkflow.GuideToolsHelperTask");

        public string TemplateWorkflowTitleText => "\uD15C\uD50C\uB9BF \uBC18\uBCF5 \uB77C\uBCA8\uB9C1";

        public string TemplateWorkflowRoleText => "\uBCF4\uC870 \uB3C4\uAD6C: \uD604\uC7AC \uC774\uBBF8\uC9C0\uB294 \uB77C\uBCA8 \uCD08\uC548\uC744 \uB9CC\uB4E4\uACE0, \uC704\uCE58 \uD655\uC778 \uD6C4 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB20C\uB7EC\uC57C \uD559\uC2B5\uC5D0 \uBC18\uC601\uB429\uB2C8\uB2E4. \uC804\uCCB4 \uC774\uBBF8\uC9C0\uB294 \uB77C\uBCA8 \uC5C6\uB294 \uD56D\uBAA9\uC5D0 \uBC14\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4.";

        public string TemplateWorkflowSummaryText => "\uAE30\uC900 \uB77C\uBCA8 1\uAC1C\uB97C \uB4F1\uB85D\uD55C \uB4A4 \uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0\uB294 \uB77C\uBCA8 \uCD08\uC548\uC744, \uC804\uCCB4 \uC774\uBBF8\uC9C0\uC5D0\uB294 \uC800\uC7A5 \uB77C\uBCA8\uC744 \uCD94\uAC00\uD569\uB2C8\uB2E4.";

        public string TemplateCurrentActionText => "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD08\uC548";

        public string TemplateBatchActionText => "\uC804\uCCB4 \uC790\uB3D9 \uC800\uC7A5";

        public string TemplateCurrentActionToolTipText => "\uC120\uD0DD\uD55C \uAE30\uC900 \uB77C\uBCA8\uC744 \uB4F1\uB85D\uD558\uAC70\uB098, \uB4F1\uB85D\uB41C \uD15C\uD50C\uB9BF\uC73C\uB85C \uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0 \uB77C\uBCA8 \uD6C4\uBCF4\uB97C \uB9CC\uB4ED\uB2C8\uB2E4.";

        public string TemplateBatchActionToolTipText => "\uB4F1\uB85D\uB41C \uD15C\uD50C\uB9BF\uC73C\uB85C \uC774\uBBF8\uC9C0 \uBAA9\uB85D\uC744 \uD55C \uBC88\uC529 \uB3CC\uBA70 \uB77C\uBCA8 \uC5C6\uB294 \uD56D\uBAA9\uC5D0\uB9CC \uC800\uC7A5\uD569\uB2C8\uB2E4.";

        public string FirstRunSamplePathTitleText => T("WpfLearningWorkflow.FirstRunSamplePathTitle");

        public string FirstRunSamplePathSummaryText => T("WpfLearningWorkflow.FirstRunSamplePathSummary");

        public string FirstRunSamplePathPrimaryActionText => T("WpfLearningWorkflow.FirstRunSamplePathPrimaryAction");

        public string FirstRunChecklistTitleText => T("WpfLearningWorkflow.FirstRunChecklistTitle");

        public string FirstRunChecklistSummaryText => T("WpfLearningWorkflow.FirstRunChecklistSummary");

        public string DatasetSetupSequenceText => T("WpfLearningWorkflow.DatasetSetupSequence");

        public string YoloDatasetStructureTitleText => T("WpfLearningWorkflow.YoloDatasetStructureTitle");

        public string YoloDatasetStructureSummaryText => T("WpfLearningWorkflow.YoloDatasetStructureSummary");

        public string YoloDatasetPairSummaryText => T("WpfLearningWorkflow.YoloDatasetPairSummary");

        public string GroundTruthChipText => T("WpfLearningWorkflow.GroundTruthChip");

        public string PredictionChipText => T("WpfLearningWorkflow.PredictionChip");

        public string TrainingChecklistStatusText
        {
            get => trainingChecklistStatusText;
            set
            {
                if (!refreshingTrainingChecklistLocalization)
                {
                    trainingChecklistLocalizationSnapshot = null;
                }

                SetProperty(ref trainingChecklistStatusText, value ?? string.Empty);
            }
        }

        public string TrainingChecklistDetailText
        {
            get => trainingChecklistDetailText;
            set
            {
                if (!refreshingTrainingChecklistLocalization)
                {
                    trainingChecklistLocalizationSnapshot = null;
                }

                SetProperty(ref trainingChecklistDetailText, value ?? string.Empty);
            }
        }

        public string TrainingChecklistActionText
        {
            get => trainingChecklistActionText;
            set
            {
                if (!refreshingTrainingChecklistLocalization)
                {
                    trainingChecklistActionLocalizationSnapshot = null;
                }

                SetProperty(ref trainingChecklistActionText, value ?? string.Empty);
            }
        }

        public string DatasetDashboardStatusText
        {
            get => datasetDashboardStatusText;
            set => SetProperty(ref datasetDashboardStatusText, value ?? string.Empty);
        }

        public string DatasetDashboardSummaryText
        {
            get => datasetDashboardSummaryText;
            set => SetProperty(ref datasetDashboardSummaryText, value ?? string.Empty);
        }

        public string DatasetDashboardActionText
        {
            get => datasetDashboardActionText;
            set => SetProperty(ref datasetDashboardActionText, value ?? string.Empty);
        }

        public void SetTrainingChecklistLocalization(TrainingChecklistLocalizationSnapshot localization)
        {
            trainingChecklistLocalizationSnapshot = localization;
            refreshingTrainingChecklistLocalization = true;
            try
            {
                TrainingChecklistStatusText = localization?.StatusText ?? string.Empty;
                TrainingChecklistDetailText = localization?.DetailText ?? string.Empty;
            }
            finally
            {
                refreshingTrainingChecklistLocalization = false;
            }

            trainingChecklistLocalizationSnapshot = localization;
        }

        public void SetTrainingChecklistActionLocalization(TrainingChecklistActionLocalizationSnapshot localization)
        {
            trainingChecklistActionLocalizationSnapshot = localization;
            refreshingTrainingChecklistLocalization = true;
            try
            {
                TrainingChecklistActionText = localization?.ActionText ?? string.Empty;
            }
            finally
            {
                refreshingTrainingChecklistLocalization = false;
            }

            trainingChecklistActionLocalizationSnapshot = localization;
        }

        public void SetTrainingChecklistText(string statusText, string detailText, string actionText)
        {
            refreshingTrainingChecklistLocalization = true;
            try
            {
                TrainingChecklistStatusText = statusText ?? string.Empty;
                TrainingChecklistDetailText = detailText ?? string.Empty;
                TrainingChecklistActionText = actionText ?? string.Empty;
            }
            finally
            {
                refreshingTrainingChecklistLocalization = false;
            }

            trainingChecklistLocalizationSnapshot = null;
            trainingChecklistActionLocalizationSnapshot = null;
        }

        public string ExternalEvaluationDataAuditStatusText
        {
            get => externalEvaluationDataAuditStatusText;
            private set => SetProperty(ref externalEvaluationDataAuditStatusText, value ?? string.Empty);
        }

        public string ExternalEvaluationDataAuditDetailText
        {
            get => externalEvaluationDataAuditDetailText;
            private set => SetProperty(ref externalEvaluationDataAuditDetailText, value ?? string.Empty);
        }

        public string ExternalEvaluationDataAuditPathText
        {
            get => externalEvaluationDataAuditPathText;
            private set => SetProperty(ref externalEvaluationDataAuditPathText, value ?? string.Empty);
        }

        public string ExternalYoloDatasetIntakeStatusText
        {
            get => externalYoloDatasetIntakeStatusText;
            private set => SetProperty(ref externalYoloDatasetIntakeStatusText, value ?? string.Empty);
        }

        public string ExternalYoloDatasetIntakeDetailText
        {
            get => externalYoloDatasetIntakeDetailText;
            private set => SetProperty(ref externalYoloDatasetIntakeDetailText, value ?? string.Empty);
        }

        public string ExternalYoloDatasetIntakePathText
        {
            get => externalYoloDatasetIntakePathText;
            private set => SetProperty(ref externalYoloDatasetIntakePathText, value ?? string.Empty);
        }

        public string ObjectDetectionMvpNextActionText
        {
            get => objectDetectionMvpNextActionText;
            private set => SetProperty(ref objectDetectionMvpNextActionText, value ?? string.Empty);
        }

        public string ModelReplacementStatusText
        {
            get => modelReplacementStatusText;
            set
            {
                if (!refreshingModelReplacementLocalization)
                {
                    modelReplacementLocalizationSnapshot = null;
                }

                SetProperty(ref modelReplacementStatusText, value ?? string.Empty);
            }
        }

        public string ModelReplacementDetailText
        {
            get => modelReplacementDetailText;
            set
            {
                if (!refreshingModelReplacementLocalization)
                {
                    modelReplacementLocalizationSnapshot = null;
                }

                SetProperty(ref modelReplacementDetailText, value ?? string.Empty);
            }
        }

        public string TrainingHistoryText
        {
            get => trainingHistoryText;
            set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    trainingHistorySourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref trainingHistoryText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().HistoryText
                        : value);
            }
        }

        public string TrainingResultComparisonText
        {
            get => trainingResultComparisonText;
            set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    trainingResultComparisonSourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref trainingResultComparisonText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().ComparisonText
                        : value);
            }
        }

        public string TrainingResultComparisonSummaryText
        {
            get => trainingResultComparisonSummaryText;
            set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    trainingResultComparisonSummarySourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref trainingResultComparisonSummaryText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().SummaryText
                        : value);
            }
        }

        public string TrainingModelAdoptionDecisionText
        {
            get => trainingModelAdoptionDecisionText;
            set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    trainingModelAdoptionDecisionSourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref trainingModelAdoptionDecisionText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().AdoptionDecisionText
                        : value);
            }
        }

        public string TrainingModelLifecycleCurrentText
        {
            get => trainingModelLifecycleCurrentText;
            private set
            {
                if (!refreshingTrainingModelLifecycleLocalization)
                {
                    trainingModelLifecycleLocalizationSnapshot = null;
                }

                SetProperty(ref trainingModelLifecycleCurrentText, string.IsNullOrWhiteSpace(value) ? T("WpfLearningWorkflow.TrainingModelLifecycle.Current.Initial") : value);
            }
        }

        public string TrainingModelLifecycleCandidateText
        {
            get => trainingModelLifecycleCandidateText;
            private set
            {
                if (!refreshingTrainingModelLifecycleLocalization)
                {
                    trainingModelLifecycleLocalizationSnapshot = null;
                }

                SetProperty(ref trainingModelLifecycleCandidateText, string.IsNullOrWhiteSpace(value) ? T("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Initial") : value);
            }
        }

        public string TrainingModelLifecycleDecisionText
        {
            get => trainingModelLifecycleDecisionText;
            private set
            {
                if (!refreshingTrainingModelLifecycleLocalization)
                {
                    trainingModelLifecycleLocalizationSnapshot = null;
                }

                SetProperty(ref trainingModelLifecycleDecisionText, string.IsNullOrWhiteSpace(value) ? T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Initial") : value);
            }
        }

        public string TrainingModelLifecycleNextActionText
        {
            get => trainingModelLifecycleNextActionText;
            private set
            {
                if (!refreshingTrainingModelLifecycleLocalization)
                {
                    trainingModelLifecycleLocalizationSnapshot = null;
                }

                SetProperty(ref trainingModelLifecycleNextActionText, string.IsNullOrWhiteSpace(value) ? T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial") : value);
            }
        }

        public string TrainingModelLifecycleSummaryTitleText => T("WpfLearningWorkflow.TrainingModelLifecycle.Title");

        public string TrainingModelLifecycleCurrentCaptionText => T("WpfLearningWorkflow.TrainingModelLifecycle.Label.Current");

        public string TrainingModelLifecycleCandidateCaptionText => T("WpfLearningWorkflow.TrainingModelLifecycle.Label.Candidate");

        public string TrainingModelLifecycleDecisionCaptionText => T("WpfLearningWorkflow.TrainingModelLifecycle.Label.Decision");

        public string TrainingModelLifecycleNextActionCaptionText => T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction");

        public string TrainingResultComparisonTitleText => T("WpfLearningWorkflow.TrainingComparison.Title");

        public string RunModelComparisonActionText
        {
            get => runModelComparisonActionText;
            private set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    runModelComparisonActionSourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref runModelComparisonActionText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().RunActionText
                        : value);
            }
        }

        public string RunModelComparisonToolTipText
        {
            get => runModelComparisonToolTipText;
            private set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    runModelComparisonToolTipSourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref runModelComparisonToolTipText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().RunToolTipText
                        : value);
            }
        }

        public string ModelComparisonBasisText
        {
            get => modelComparisonBasisText;
            private set
            {
                if (!refreshingTrainingComparisonLocalization)
                {
                    modelComparisonBasisSourceText = value ?? string.Empty;
                    trainingComparisonLocalizationSnapshot = null;
                }

                SetProperty(
                    ref modelComparisonBasisText,
                    string.IsNullOrWhiteSpace(value)
                        ? TrainingComparisonLocalizationService.CreateInitial().ComparisonBasisText
                        : value);
            }
        }

        public bool IsRunModelComparisonEnabled
        {
            get => isRunModelComparisonEnabled;
            private set => SetProperty(ref isRunModelComparisonEnabled, value);
        }

        public WpfYoloTrainingWorkflowStepItem CurrentYoloTrainingStep
        {
            get => currentYoloTrainingStep;
            private set
            {
                if (SetProperty(ref currentYoloTrainingStep, value))
                {
                    HasCurrentYoloTrainingStep = value != null;
                    CurrentYoloTrainingStepTitleText = value == null
                        ? "다음 단계 없음"
                        : $"{value.Order}. {value.Title}";
                    CurrentYoloTrainingStepDetailText = value?.ActionText ?? string.Empty;
                    CurrentYoloTrainingActionText = value == null
                        ? "대기"
                        : LearningWorkflowGuidanceService.BuildCurrentYoloTrainingActionText(value);
                }
            }
        }

        public string CurrentYoloTrainingStepTitleText
        {
            get => currentYoloTrainingStepTitleText;
            private set => SetProperty(ref currentYoloTrainingStepTitleText, value ?? string.Empty);
        }

        public string CurrentYoloTrainingStepDetailText
        {
            get => currentYoloTrainingStepDetailText;
            private set => SetProperty(ref currentYoloTrainingStepDetailText, value ?? string.Empty);
        }

        public string CurrentYoloTrainingActionText
        {
            get => currentYoloTrainingActionText;
            private set => SetProperty(ref currentYoloTrainingActionText, value ?? string.Empty);
        }

        public bool HasCurrentYoloTrainingStep
        {
            get => hasCurrentYoloTrainingStep;
            private set => SetProperty(ref hasCurrentYoloTrainingStep, value);
        }

        public bool IsYoloFixClassesEnabled
        {
            get => isYoloFixClassesEnabled;
            private set => SetProperty(ref isYoloFixClassesEnabled, value);
        }

        public bool IsYoloFixLabelsEnabled
        {
            get => isYoloFixLabelsEnabled;
            private set => SetProperty(ref isYoloFixLabelsEnabled, value);
        }

        public bool IsYoloFixDatasetEnabled
        {
            get => isYoloFixDatasetEnabled;
            private set => SetProperty(ref isYoloFixDatasetEnabled, value);
        }

        private void RegisterAnnotationTool(WpfAnnotationToolItem tool)
        {
            if (tool == null)
            {
                return;
            }

            AnnotationTools.Add(tool);
            // The guide separates persistent drawing tools from one-shot edit commands;
            // the full AnnotationTools list stays as the shared source for canvas toolbar state.
            if (AnnotationWorkflowService.IsOneShotCommandTool(tool.Tool))
            {
                AnnotationCommandTools.Add(tool);
                return;
            }

            SelectableAnnotationTools.Add(tool);
        }

        private void RefreshAnnotationToolScopeForMode(WpfLearningMode mode)
        {
            // Dataset purpose owns which drawing tools are visible. Keep the full
            // AnnotationTools catalog for commands/tests, but only expose tools that
            // match the labeling task so operators do not see irrelevant controls.
            List<WpfAnnotationTool> visibleSelectableTools = ResolveSelectableToolsForMode(mode).ToList();
            SelectableAnnotationTools.Clear();
            AnnotationCommandTools.Clear();
            VisibleAnnotationTools.Clear();

            foreach (WpfAnnotationTool toolKind in visibleSelectableTools)
            {
                WpfAnnotationToolItem tool = AnnotationTools.FirstOrDefault(candidate => candidate.Tool == toolKind);
                if (tool != null)
                {
                    SelectableAnnotationTools.Add(tool);
                    VisibleAnnotationTools.Add(tool);
                }
            }

            foreach (WpfAnnotationToolItem tool in AnnotationTools.Where(tool => mode != WpfLearningMode.AnomalyDetection && AnnotationWorkflowService.IsOneShotCommandTool(tool.Tool)))
            {
                AnnotationCommandTools.Add(tool);
                VisibleAnnotationTools.Add(tool);
            }

            if (SelectedTool == null
                || !SelectableAnnotationTools.Contains(SelectedTool)
                || ShouldPreferModeDefaultTool(mode, SelectedTool.Tool))
            {
                SelectedTool = ResolvePreferredSelectableToolForMode(mode) ?? SelectableAnnotationTools.FirstOrDefault();
            }
        }

        private static IEnumerable<WpfAnnotationTool> ResolveSelectableToolsForMode(WpfLearningMode mode)
        {
            switch (mode)
            {
                case WpfLearningMode.Segmentation:
                    return new[]
                    {
                        WpfAnnotationTool.Rectangle,
                        WpfAnnotationTool.Brush,
                        WpfAnnotationTool.Eraser,
                        WpfAnnotationTool.Polygon,
                        WpfAnnotationTool.Select,
                        WpfAnnotationTool.PanZoom
                    };

                case WpfLearningMode.AnomalyDetection:
                    return new[]
                    {
                        WpfAnnotationTool.PanZoom
                    };

                case WpfLearningMode.ObjectDetection:
                case WpfLearningMode.Train:
                case WpfLearningMode.Infer:
                case WpfLearningMode.Review:
                    return new[]
                    {
                        WpfAnnotationTool.Select,
                        WpfAnnotationTool.Rectangle,
                        WpfAnnotationTool.PanZoom
                    };

                default:
                    return new[]
                    {
                        WpfAnnotationTool.Select,
                        WpfAnnotationTool.Rectangle,
                        WpfAnnotationTool.Polygon,
                        WpfAnnotationTool.Brush,
                        WpfAnnotationTool.Eraser,
                        WpfAnnotationTool.PanZoom
                    };
            }
        }

        private WpfAnnotationToolItem ResolvePreferredSelectableToolForMode(WpfLearningMode mode)
        {
            WpfAnnotationTool preferredTool = mode == WpfLearningMode.Segmentation
                ? WpfAnnotationTool.Brush
                : WpfAnnotationTool.Select;
            return SelectableAnnotationTools.FirstOrDefault(item => item.Tool == preferredTool);
        }

        private static bool ShouldPreferModeDefaultTool(WpfLearningMode mode, WpfAnnotationTool currentTool)
            => (mode == WpfLearningMode.Segmentation && currentTool == WpfAnnotationTool.Select)
                || (mode == WpfLearningMode.AnomalyDetection
                    && (currentTool == WpfAnnotationTool.Brush || currentTool == WpfAnnotationTool.Eraser))
                || (mode == WpfLearningMode.ObjectDetection && currentTool == WpfAnnotationTool.PanZoom);

        public void ApplyDatasetPurpose(LabelingDatasetPurpose purpose)
        {
            WpfLearningMode mode = ToLearningMode(purpose);
            SelectedDatasetPurposeMode = DatasetPurposeModes.FirstOrDefault(item => item.Mode == mode)
                ?? DatasetPurposeModes.FirstOrDefault(item => item.Mode == WpfLearningMode.ObjectDetection);
        }

        public LabelingDatasetPurpose GetSelectedDatasetPurpose()
            => ToDatasetPurpose(SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);

        public static WpfLearningMode ToLearningMode(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => WpfLearningMode.Segmentation,
                LabelingDatasetPurpose.AnomalyDetection => WpfLearningMode.AnomalyDetection,
                _ => WpfLearningMode.ObjectDetection
            };
        }

        public static LabelingDatasetPurpose ToDatasetPurpose(WpfLearningMode mode)
        {
            return mode switch
            {
                WpfLearningMode.Segmentation => LabelingDatasetPurpose.Segmentation,
                WpfLearningMode.AnomalyDetection => LabelingDatasetPurpose.AnomalyDetection,
                _ => LabelingDatasetPurpose.ObjectDetection
            };
        }

        public void ConfigureCommands(
            Action<object> datasetPurposeSelectionChanged,
            Action<object> datasetSetupStart,
            Action<object> learningModeSelectionChanged,
            Action<object> annotationToolSelectionChanged,
            Action<object> learningStepSelectionChanged,
            Action<WpfYoloTrainingWorkflowStepItem> yoloTrainingWorkflowStep,
            Action tutorialOpenHtmlGuide,
            Action yoloFixClasses,
            Action yoloFixLabels,
            Action yoloFixDataset,
            Action<WpfDatasetDashboardMetricItem> datasetDashboardMetricSelected = null,
            Action runModelComparison = null,
            Action datasetOpenExisting = null,
            Action<WpfFirstRunChecklistItem> firstRunSamplePathSelected = null,
            Action runTemplateCurrentImage = null,
            Action runTemplateBatch = null,
            Action runExternalEvaluationDataAudit = null,
            Action selectExternalYoloDataset = null,
            Action activateExternalYoloDataset = null,
            Action clearExternalYoloDataset = null)
        {
            // Dataset purpose is a project setting; learning mode is only guide/navigation.
            // Keep both command paths separate so task-specific tools do not change when the operator browses lesson concepts.
            DatasetPurposeSelectionChangedCommand = new RelayCommand<object>(datasetPurposeSelectionChanged ?? NoOpSelectionCommand);
            DatasetSetupStartCommand = new RelayCommand<object>(datasetSetupStart ?? NoOpSelectionCommand);
            DatasetOpenExistingCommand = new RelayCommand(datasetOpenExisting ?? NoOpCommand);
            LearningModeSelectionChangedCommand = new RelayCommand<object>(learningModeSelectionChanged ?? NoOpSelectionCommand);
            AnnotationToolSelectionChangedCommand = new RelayCommand<object>(annotationToolSelectionChanged ?? NoOpSelectionCommand);
            LearningStepSelectionChangedCommand = new RelayCommand<object>(learningStepSelectionChanged ?? NoOpSelectionCommand);
            YoloTrainingWorkflowStepCommand = new RelayCommand<WpfYoloTrainingWorkflowStepItem>(yoloTrainingWorkflowStep ?? NoOpTrainingStepCommand);
            FirstRunSamplePathCommand = new RelayCommand<WpfFirstRunChecklistItem>(firstRunSamplePathSelected ?? NoOpFirstRunSamplePathCommand);
            DatasetDashboardMetricCommand = new RelayCommand<WpfDatasetDashboardMetricItem>(datasetDashboardMetricSelected ?? NoOpDatasetDashboardMetricCommand);
            TutorialOpenHtmlGuideCommand = new RelayCommand(tutorialOpenHtmlGuide ?? NoOpCommand);
            YoloFixClassesCommand = new RelayCommand(yoloFixClasses ?? NoOpCommand);
            YoloFixLabelsCommand = new RelayCommand(yoloFixLabels ?? NoOpCommand);
            YoloFixDatasetCommand = new RelayCommand(yoloFixDataset ?? NoOpCommand);
            RunModelComparisonCommand = new RelayCommand(runModelComparison ?? NoOpCommand);
            ExternalEvaluationDataAuditCommand = new RelayCommand(runExternalEvaluationDataAudit ?? NoOpCommand);
            SelectExternalYoloDatasetCommand = new RelayCommand(selectExternalYoloDataset ?? NoOpCommand);
            ActivateExternalYoloDatasetCommand = new RelayCommand(activateExternalYoloDataset ?? NoOpCommand);
            ClearExternalYoloDatasetCommand = new RelayCommand(clearExternalYoloDataset ?? NoOpCommand);
            TemplateCurrentImageCommand = new RelayCommand(runTemplateCurrentImage ?? NoOpCommand);
            TemplateBatchCommand = new RelayCommand(runTemplateBatch ?? NoOpCommand);
        }

        public void ConfigureLearningModeSelectionWorkflow(Action<WpfLearningModeWorkflowAction> applyModeAction)
        {
            learningModeWorkflowAction = applyModeAction ?? (_ => { });
            LearningModeSelectionChangedCommand = new RelayCommand<object>(ExecuteLearningModeSelectionChanged);
        }

        private void ExecuteLearningModeSelectionChanged(object selectedItem)
        {
            WpfLearningModeItem selectedModeItem = selectedItem as WpfLearningModeItem ?? SelectedMode;
            if (selectedModeItem == null)
            {
                return;
            }

            SelectedMode = selectedModeItem;
            learningModeWorkflowAction?.Invoke(AnnotationWorkflowService.ResolveModeAction(selectedModeItem.Mode));
        }

        public void ConfigureLearningStepSelectionWorkflow(Action<WpfLearningStepWorkflowAction> applyStepAction)
        {
            learningStepWorkflowAction = applyStepAction ?? (_ => { });
            LearningStepSelectionChangedCommand = new RelayCommand<object>(ExecuteLearningStepSelectionChanged);
        }

        private void ExecuteLearningStepSelectionChanged(object selectedItem)
        {
            WpfLearningStepItem selectedStepItem = selectedItem as WpfLearningStepItem ?? SelectedStep;
            if (selectedStepItem == null)
            {
                return;
            }

            SelectedStep = selectedStepItem;
            learningStepWorkflowAction?.Invoke(AnnotationWorkflowService.ResolveStepAction(selectedStepItem.Step));
        }

        public void ConfigureAnnotationToolSelectionWorkflow(Action<WpfAnnotationToolItem> applyToolAction)
        {
            annotationToolSelectionAction = applyToolAction ?? (_ => { });
            AnnotationToolSelectionChangedCommand = new RelayCommand<object>(ExecuteAnnotationToolSelectionChanged);
        }

        private void ExecuteAnnotationToolSelectionChanged(object selectedItem)
        {
            WpfAnnotationToolItem selectedToolItem = selectedItem as WpfAnnotationToolItem ?? SelectedTool;
            if (selectedToolItem == null)
            {
                return;
            }

            annotationToolSelectionAction?.Invoke(selectedToolItem);
        }

        public void ConfigureDatasetPurposeSelectionWorkflow(
            Func<WpfLearningModeItem, bool> selectionGuard,
            Action<LabelingDatasetPurpose> applyPurposeAction)
        {
            datasetPurposeSelectionGuard = selectionGuard ?? (_ => true);
            datasetPurposeSelectionAction = applyPurposeAction ?? (_ => { });
            DatasetPurposeSelectionChangedCommand = new RelayCommand<object>(ExecuteDatasetPurposeSelectionChanged);
        }

        private void ExecuteDatasetPurposeSelectionChanged(object selectedItem)
        {
            WpfLearningModeItem selectedPurposeItem = selectedItem as WpfLearningModeItem;
            if (selectedPurposeItem != null && !datasetPurposeSelectionGuard(selectedPurposeItem))
            {
                return;
            }

            selectedPurposeItem ??= SelectedDatasetPurposeMode;
            if (selectedPurposeItem == null)
            {
                return;
            }

            SelectedDatasetPurposeMode = selectedPurposeItem;
            datasetPurposeSelectionAction?.Invoke(ToDatasetPurpose(selectedPurposeItem.Mode));
        }

        public void ConfigureModelComparisonWorkflow(
            ModelComparisonWorkflowService workflowService,
            Func<ModelComparisonCallbacks> callbacksProvider)
        {
            modelComparisonWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            modelComparisonCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            RunModelComparisonCommand = new RelayCommand(() => _ = ExecuteRunModelComparisonCommandAsync());
        }

        public void ConfigureExternalEvaluationDataAuditWorkflow(
            ExternalAuditWorkflowService workflowService,
            Func<string> initialDirectoryProvider,
            Func<string, string> directorySelector,
            Func<IReadOnlyList<string>> referenceDirectoriesProvider,
            Func<bool> closeApproval,
            Action<string> statusSink = null)
        {
            externalAuditWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            externalEvaluationDataAuditInitialDirectoryProvider = initialDirectoryProvider
                ?? throw new ArgumentNullException(nameof(initialDirectoryProvider));
            externalEvaluationDataAuditDirectorySelector = directorySelector
                ?? throw new ArgumentNullException(nameof(directorySelector));
            externalEvaluationDataAuditReferenceDirectoriesProvider = referenceDirectoriesProvider
                ?? throw new ArgumentNullException(nameof(referenceDirectoriesProvider));
            externalEvaluationDataAuditCloseApproval = closeApproval
                ?? throw new ArgumentNullException(nameof(closeApproval));
            externalEvaluationDataAuditStatusSink = statusSink;
            ExternalEvaluationDataAuditCommand = new RelayCommand(() => _ = ExecuteExternalEvaluationDataAuditAsync());
        }

        public void ConfigureHistoricalSegmentationRemediationAuditWorkflow(
            ExternalAuditWorkflowService workflowService,
            Func<LabelingProjectData> dataProvider,
            Func<string> sourceImagePathProvider,
            Func<bool> closeApproval,
            Action<string, string> statusSink)
        {
            externalAuditWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            historicalSegmentationRemediationDataProvider = dataProvider
                ?? throw new ArgumentNullException(nameof(dataProvider));
            historicalSegmentationRemediationSourceImagePathProvider = sourceImagePathProvider
                ?? throw new ArgumentNullException(nameof(sourceImagePathProvider));
            historicalSegmentationRemediationCloseApproval = closeApproval
                ?? throw new ArgumentNullException(nameof(closeApproval));
            historicalSegmentationRemediationStatusSink = statusSink
                ?? throw new ArgumentNullException(nameof(statusSink));
            HistoricalSegmentationRemediationAuditCommand = new RelayCommand(
                () => _ = ExecuteHistoricalSegmentationRemediationAuditAsync());
        }

        public void ConfigureExternalYoloDatasetIntakeWorkflow(
            ExternalYoloDatasetIntakeWorkflowService workflowService,
            Func<ExternalYoloDatasetIntakeCallbacks> callbacksProvider)
        {
            externalYoloDatasetIntakeWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            externalYoloDatasetIntakeCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            SelectExternalYoloDatasetCommand = new RelayCommand(() => _ = ExecuteSelectExternalYoloDatasetCommandAsync());
            ActivateExternalYoloDatasetCommand = new RelayCommand(() => _ = ExecuteActivateExternalYoloDatasetCommandAsync());
            ClearExternalYoloDatasetCommand = new RelayCommand(ExecuteClearExternalYoloDatasetCommand);
        }

        public void SetYoloFixActionAvailability(bool canFixClasses, bool canFixLabels, bool canFixDataset)
        {
            IsYoloFixClassesEnabled = canFixClasses;
            IsYoloFixLabelsEnabled = canFixLabels;
            IsYoloFixDatasetEnabled = canFixDataset;
        }

        public void SetTrainingRunHistoryItems(IEnumerable<string> items)
        {
            TrainingRunHistoryItems.Clear();
            foreach (string item in items ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    TrainingRunHistoryItems.Add(item);
                }
            }
        }

        public void SetTrainingResultReportItems(IEnumerable<WpfTrainingResultReportItem> items)
        {
            TrainingResultReportItems.Clear();
            foreach (WpfTrainingResultReportItem item in items ?? Enumerable.Empty<WpfTrainingResultReportItem>())
            {
                if (item != null)
                {
                    TrainingResultReportItems.Add(item);
                }
            }
        }

        public void SetTrainingHistoryText(string historyText)
        {
            trainingHistorySourceText = historyText ?? string.Empty;
            RefreshTrainingComparisonLocalization();
        }

        public void SetTrainingComparisonResultTexts(
            string summaryText = null,
            string comparisonText = null,
            string adoptionDecisionText = null)
        {
            if (summaryText != null)
            {
                trainingResultComparisonSummarySourceText = summaryText;
            }

            if (comparisonText != null)
            {
                trainingResultComparisonSourceText = comparisonText;
            }

            if (adoptionDecisionText != null)
            {
                trainingModelAdoptionDecisionSourceText = adoptionDecisionText;
            }

            RefreshTrainingComparisonLocalization();
        }

        public void SetTrainingComparisonLocalization(TrainingComparisonLocalizationSnapshot localization)
        {
            localization ??= TrainingComparisonLocalizationService.CreateInitial();
            trainingComparisonLocalizationSnapshot = localization;
            refreshingTrainingComparisonLocalization = true;
            try
            {
                TrainingHistoryText = localization.HistoryText;
                TrainingResultComparisonSummaryText = localization.SummaryText;
                TrainingResultComparisonText = localization.ComparisonText;
                TrainingModelAdoptionDecisionText = localization.AdoptionDecisionText;
                RunModelComparisonActionText = localization.RunActionText;
                RunModelComparisonToolTipText = localization.RunToolTipText;
                ModelComparisonBasisText = localization.ComparisonBasisText;
            }
            finally
            {
                refreshingTrainingComparisonLocalization = false;
            }
        }

        private void RefreshTrainingComparisonLocalization()
        {
            SetTrainingComparisonLocalization(
                TrainingComparisonLocalizationService.Build(
                    trainingHistorySourceText,
                    trainingResultComparisonSummarySourceText,
                    trainingResultComparisonSourceText,
                    trainingModelAdoptionDecisionSourceText,
                    runModelComparisonActionSourceText,
                    runModelComparisonToolTipSourceText,
                    modelComparisonBasisSourceText));
        }

        public void SetModelComparisonRunState(bool enabled, string actionText)
            => SetModelComparisonRunState(enabled, actionText, string.Empty);

        public void SetModelComparisonRunState(bool enabled, string actionText, string toolTipText)
            => SetModelComparisonRunState(enabled, actionText, toolTipText, string.Empty);

        public void SetModelComparisonRunState(bool enabled, string actionText, string toolTipText, string basisText)
        {
            IsRunModelComparisonEnabled = enabled;
            runModelComparisonActionSourceText = actionText ?? string.Empty;
            runModelComparisonToolTipSourceText = toolTipText ?? string.Empty;
            modelComparisonBasisSourceText = basisText ?? string.Empty;
            RefreshTrainingComparisonLocalization();
        }

        public void SetModelReplacementReadiness(string statusText, string detailText)
        {
            // Training readiness and model replacement are intentionally separate:
            // Keep replacement stricter than training readiness: users may train with learning/validation data only,
            // but switching the active model needs a separate final-verification set.
            modelReplacementLocalizationSnapshot = null;
            ModelReplacementStatusText = string.IsNullOrWhiteSpace(statusText)
                ? T("WpfLearningWorkflow.ModelReplacement.Status.Initial")
                : statusText;
            ModelReplacementDetailText = string.IsNullOrWhiteSpace(detailText)
                ? T("WpfLearningWorkflow.ModelReplacement.Detail.Initial")
                : detailText;
        }

        public void SetModelReplacementLocalization(ModelReplacementLocalizationSnapshot localization)
        {
            localization ??= ModelReplacementLocalizationService.CreateInitial();
            modelReplacementLocalizationSnapshot = localization;
            refreshingModelReplacementLocalization = true;
            try
            {
                ModelReplacementStatusText = localization.StatusText;
                ModelReplacementDetailText = localization.DetailText;
            }
            finally
            {
                refreshingModelReplacementLocalization = false;
            }
        }

        public void SetTrainingModelLifecycleState(
            string currentModelText,
            string candidateModelText,
            string decisionText,
            string nextActionText)
        {
            SetTrainingModelLifecycleLocalization(
                TrainingModelLifecycleLocalizationService.Build(
                    currentModelText,
                    candidateModelText,
                    decisionText,
                    nextActionText));
        }

        public void SetTrainingModelLifecycleLocalization(
            TrainingModelLifecycleLocalizationSnapshot localization)
        {
            localization ??= TrainingModelLifecycleLocalizationService.CreateInitial();
            trainingModelLifecycleLocalizationSnapshot = localization;
            refreshingTrainingModelLifecycleLocalization = true;
            try
            {
                TrainingModelLifecycleCurrentText = localization.CurrentText;
                TrainingModelLifecycleCandidateText = localization.CandidateText;
                TrainingModelLifecycleDecisionText = localization.DecisionText;
                TrainingModelLifecycleNextActionText = localization.NextActionText;
            }
            finally
            {
                refreshingTrainingModelLifecycleLocalization = false;
            }
        }

        public void SetDatasetDashboard(
            string statusText,
            string summaryText,
            string actionText,
            IEnumerable<WpfDatasetDashboardMetricItem> metrics,
            IEnumerable<string> issues,
            DatasetDashboardLocalizationSnapshot localization = null)
        {
            List<WpfDatasetDashboardMetricItem> metricItems = (metrics ?? Enumerable.Empty<WpfDatasetDashboardMetricItem>())
                .Where(item => item != null)
                .ToList();
            datasetDashboardLocalizationSnapshot = localization?.WithMetricItems(metricItems);
            DatasetDashboardStatusText = datasetDashboardLocalizationSnapshot?.StatusText
                ?? (string.IsNullOrWhiteSpace(statusText)
                    ? T("WpfLearningWorkflow.DatasetDashboard.Status.Before")
                    : statusText);
            DatasetDashboardSummaryText = datasetDashboardLocalizationSnapshot?.SummaryText
                ?? (string.IsNullOrWhiteSpace(summaryText) ? string.Empty : summaryText);
            string localizedActionText = datasetDashboardLocalizationSnapshot?.ActionText;
            DatasetDashboardActionText = string.IsNullOrWhiteSpace(localizedActionText)
                ? (string.IsNullOrWhiteSpace(actionText) ? string.Empty : actionText)
                : localizedActionText;
            ObjectDetectionMvpNextActionText = LearningWorkflowGuidanceService.BuildObjectDetectionMvpNextActionText(DatasetDashboardActionText);

            DatasetDashboardMetrics.Clear();
            IEnumerable<WpfDatasetDashboardMetricItem> localizedMetrics = datasetDashboardLocalizationSnapshot?.MetricItems
                ?? metricItems;
            foreach (WpfDatasetDashboardMetricItem item in localizedMetrics)
            {
                DatasetDashboardMetrics.Add(item);
            }

            IEnumerable<string> localizedIssues = datasetDashboardLocalizationSnapshot?.IssueItems
                ?? issues
                ?? Enumerable.Empty<string>();
            DatasetDashboardIssueItems.Clear();
            foreach (string issue in localizedIssues)
            {
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    DatasetDashboardIssueItems.Add(issue);
                }
            }

            if (DatasetDashboardIssueItems.Count == 0)
            {
                DatasetDashboardIssueItems.Add(T("WpfLearningWorkflow.DatasetDashboard.Issue.NoIssues"));
            }
        }

        public void SetExternalEvaluationDataAuditResult(string statusText, string detailText, string pathText)
        {
            ExternalEvaluationDataAuditStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uC810\uAC80 \uC804"
                : statusText;
            ExternalEvaluationDataAuditDetailText = detailText ?? string.Empty;
            ExternalEvaluationDataAuditPathText = pathText ?? string.Empty;
        }

        private async Task ExecuteExternalEvaluationDataAuditAsync()
        {
            if (externalAuditWorkflowService == null
                || externalEvaluationDataAuditCloseApproval?.Invoke() == true
                || externalAuditWorkflowService.IsExternalEvaluationDataAuditRunning)
            {
                return;
            }

            string selectedDirectory = externalEvaluationDataAuditDirectorySelector(
                externalEvaluationDataAuditInitialDirectoryProvider() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(selectedDirectory))
            {
                SetExternalEvaluationDataAuditResult(
                    "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uCDE8\uC18C",
                    "\uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uBA74 \uD604\uC7AC \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC640 \uB3D9\uC77C \uCF58\uD150\uCE20\uC778\uC9C0 \uD655\uC778\uD569\uB2C8\uB2E4.",
                    string.Empty);
                return;
            }

            IReadOnlyList<string> referenceDirectories = externalEvaluationDataAuditReferenceDirectoriesProvider()
                ?? Array.Empty<string>();
            SetExternalEvaluationDataAuditResult(
                "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uC911",
                "SHA-256\uB85C \uD604\uC7AC \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC640 \uBE44\uAD50\uD569\uB2C8\uB2E4.",
                selectedDirectory);

            ExternalEvaluationDataAuditWorkflowResult result = await externalAuditWorkflowService.RunExternalEvaluationAsync(
                new ExternalEvaluationDataAuditWorkflowRequest
                {
                    ReferenceDirectories = referenceDirectories,
                    SelectedDirectory = selectedDirectory
                });
            if (externalEvaluationDataAuditCloseApproval?.Invoke() == true
                || externalAuditWorkflowService.IsClosed
                || result.IsCanceled
                || !result.Started)
            {
                return;
            }

            if (result.Error != null)
            {
                SetExternalEvaluationDataAuditResult(
                    "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uBD88\uAC00",
                    result.Error.Message,
                    selectedDirectory);
                return;
            }

            YoloExternalEvaluationDataAuditReport report = result.Report;
            if (!result.Succeeded || report == null)
            {
                return;
            }

            ExternalEvaluationDataAuditPresentation presentation =
                ExternalEvaluationDataAuditPresentationService.Build(report);
            SetExternalEvaluationDataAuditResult(
                presentation.StatusText,
                presentation.DetailText,
                selectedDirectory);
            string status =
                $"\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: {selectedDirectory} / \uAE30\uC900 {report.ReferenceImageCount} / \uC678\uBD80 {report.ExternalImageCount} / \uC911\uBCF5 {report.ContentOverlapCount}";
            ExternalEvaluationDataAuditStatusChanged?.Invoke(status);
            externalEvaluationDataAuditStatusSink?.Invoke(status);
        }

        private async Task ExecuteHistoricalSegmentationRemediationAuditAsync()
        {
            if (externalAuditWorkflowService == null
                || historicalSegmentationRemediationCloseApproval?.Invoke() == true
                || externalAuditWorkflowService.IsHistoricalSegmentationRemediationAuditRunning)
            {
                return;
            }

            LabelingProjectData data = historicalSegmentationRemediationDataProvider?.Invoke();
            string outputPath = YoloSegmentationHistoricalRemediationAuditService.ResolveDefaultOutputPath(data);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                historicalSegmentationRemediationStatusSink?.Invoke(
                    "SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: \uB370\uC774\uD130\uC14B \uC800\uC7A5 \uD3F4\uB354\uB97C \uBA3C\uC800 \uC9C0\uC815\uD558\uC138\uC694.",
                    string.Empty);
                return;
            }

            string sourceImagePath = historicalSegmentationRemediationSourceImagePathProvider?.Invoke() ?? string.Empty;
            historicalSegmentationRemediationStatusSink?.Invoke(
                "SEG \uBCF4\uC815 \uAC80\uD1A0: \uAE30\uC874 \uB9C8\uC2A4\uD06C\uC640 YOLO \uB77C\uBCA8\uC744 \uC77D\uAE30 \uC804\uC6A9\uC73C\uB85C \uBE44\uAD50 \uC911",
                string.Empty);

            HistoricalSegmentationRemediationAuditWorkflowResult result;
            try
            {
                result = await externalAuditWorkflowService.RunHistoricalRemediationAsync(
                    new HistoricalSegmentationRemediationAuditWorkflowRequest
                    {
                        Data = data,
                        SourceImagePath = sourceImagePath,
                        OutputPath = outputPath
                    }).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                historicalSegmentationRemediationStatusSink?.Invoke(
                    $"SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: {ex.Message}",
                    $"SEG remediation dry run failed: {ex.Message}");
                return;
            }

            if (historicalSegmentationRemediationCloseApproval?.Invoke() == true
                || externalAuditWorkflowService.IsClosed
                || result.IsCanceled
                || !result.Started)
            {
                return;
            }

            if (result.Error != null)
            {
                historicalSegmentationRemediationStatusSink?.Invoke(
                    $"SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: {result.Error.Message}",
                    $"SEG remediation dry run failed: {result.Error.Message}");
                return;
            }

            if (!result.Succeeded || result.Report == null || result.Export == null)
            {
                return;
            }

            string sourceSummary = result.Report.ExcludedSourceImageCount > 0
                ? $"\uAE30\uC900 \uC774\uBBF8\uC9C0 \uC81C\uC678 {result.Report.ExcludedSourceImageCount}\uC7A5"
                : "\uAE30\uC900 \uC774\uBBF8\uC9C0 \uC81C\uC678 \uC5C6\uC74C";
            string errorSummary = result.Report.HasErrors
                ? $" / \uD655\uC778 \uD544\uC694 {result.Report.UnresolvedRecordCount}\uAC74"
                : string.Empty;
            historicalSegmentationRemediationStatusSink?.Invoke(
                $"SEG \uBCF4\uC815 \uAC80\uD1A0 \uBCF4\uACE0\uC11C \uC800\uC7A5: {Path.GetFileName(result.Export.OutputPath)} / \uB300\uC0C1 {result.Report.CandidateImageCount}\uC7A5 / \uB77C\uBCA8 \uCC28\uC774 {result.Report.ChangedYoloLabelImageCount}\uC7A5 / {sourceSummary}{errorSummary}",
                $"SEG remediation dry run saved: {result.Export.OutputPath} / images {result.Report.CandidateImageCount} / records {result.Report.CandidateRecordCount} / changed labels {result.Report.ChangedYoloLabelImageCount} / excluded sources {result.Report.ExcludedSourceImageCount}");
        }

        private async Task ExecuteRunModelComparisonCommandAsync()
        {
            if (modelComparisonWorkflowService == null)
            {
                return;
            }

            ModelComparisonCallbacks callbacks = modelComparisonCallbacksProvider?.Invoke();
            if (callbacks == null)
            {
                return;
            }

            await modelComparisonWorkflowService
                .RunCandidateAsync(callbacks)
                .ConfigureAwait(true);
        }

        public LabelingDatasetPurpose GetSelectedExternalYoloDatasetPurpose()
            => ToDatasetPurpose(SelectedExternalYoloDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);

        public void SetExternalYoloDatasetIntakeResult(
            LabelingDatasetPurpose purpose,
            string statusText,
            string detailText,
            string pathText)
        {
            WpfLearningMode mode = ToLearningMode(purpose);
            SelectedExternalYoloDatasetPurposeMode = ExternalYoloDatasetPurposeModes.FirstOrDefault(item => item.Mode == mode)
                ?? ExternalYoloDatasetPurposeModes.FirstOrDefault();
            ExternalYoloDatasetIntakeStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "\uC678\uBD80 YOLO data.yaml: \uC120\uD0DD \uC548 \uD568"
                : statusText;
            ExternalYoloDatasetIntakeDetailText = detailText ?? string.Empty;
            ExternalYoloDatasetIntakePathText = pathText ?? string.Empty;
        }

        private async Task ExecuteSelectExternalYoloDatasetCommandAsync()
        {
            ExternalYoloDatasetIntakeCallbacks callbacks = externalYoloDatasetIntakeCallbacksProvider?.Invoke();
            if (externalYoloDatasetIntakeWorkflowService == null
                || callbacks == null
                || callbacks.IsApplicationCloseApproved?.Invoke() == true
                || externalYoloDatasetIntakeWorkflowService.IsRunning)
            {
                return;
            }

            ExternalYoloDatasetSettings settings = callbacks.SettingsProvider?.Invoke();
            if (settings == null)
            {
                return;
            }

            string selectedPath = callbacks.SelectDataYamlPath?.Invoke(settings.DataYamlFilePath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                SetExternalYoloDatasetIntakeResult(
                    settings.DatasetPurpose,
                    "외부 YOLO data.yaml: 선택 취소",
                    "파일을 선택하면 읽기 전용 검증 후 다음 학습에 사용할지 별도로 결정합니다.",
                    settings.DataYamlFilePath);
                return;
            }

            if (callbacks.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            await ValidateAndStoreExternalYoloDatasetAsync(
                callbacks,
                selectedPath,
                callbacks.SelectedPurposeProvider?.Invoke() ?? LabelingDatasetPurpose.ObjectDetection,
                useForNextTraining: false);
        }

        private async Task ExecuteActivateExternalYoloDatasetCommandAsync()
        {
            ExternalYoloDatasetIntakeCallbacks callbacks = externalYoloDatasetIntakeCallbacksProvider?.Invoke();
            if (externalYoloDatasetIntakeWorkflowService == null
                || callbacks == null
                || callbacks.IsApplicationCloseApproved?.Invoke() == true
                || externalYoloDatasetIntakeWorkflowService.IsRunning)
            {
                return;
            }

            ExternalYoloDatasetSettings settings = callbacks.SettingsProvider?.Invoke();
            if (settings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.DataYamlFilePath))
            {
                SetExternalYoloDatasetIntakeResult(
                    settings.DatasetPurpose,
                    "외부 YOLO data.yaml: 선택 필요",
                    "먼저 data.yaml을 선택해 검증하세요.",
                    string.Empty);
                return;
            }

            await ValidateAndStoreExternalYoloDatasetAsync(
                callbacks,
                settings.DataYamlFilePath,
                callbacks.SelectedPurposeProvider?.Invoke() ?? settings.DatasetPurpose,
                useForNextTraining: true);
        }

        private void ExecuteClearExternalYoloDatasetCommand()
        {
            ExternalYoloDatasetIntakeCallbacks callbacks = externalYoloDatasetIntakeCallbacksProvider?.Invoke();
            if (externalYoloDatasetIntakeWorkflowService == null
                || callbacks == null
                || callbacks.IsApplicationCloseApproved?.Invoke() == true
                || externalYoloDatasetIntakeWorkflowService.IsRunning)
            {
                return;
            }

            if (callbacks.SettingsProvider?.Invoke() == null)
            {
                return;
            }

            callbacks.ClearSelection?.Invoke();
            callbacks.AppendLog?.Invoke("외부 YOLO data.yaml 선택과 다음 학습 사용을 해제했습니다.");
        }

        private async Task ValidateAndStoreExternalYoloDatasetAsync(
            ExternalYoloDatasetIntakeCallbacks callbacks,
            string dataYamlFilePath,
            LabelingDatasetPurpose purpose,
            bool useForNextTraining)
        {
            if (externalYoloDatasetIntakeWorkflowService == null
                || callbacks == null
                || callbacks.IsApplicationCloseApproved?.Invoke() == true
                || externalYoloDatasetIntakeWorkflowService.IsRunning)
            {
                return;
            }

            SetExternalYoloDatasetIntakeResult(
                purpose,
                "외부 YOLO data.yaml: 확인 중",
                "원본 이미지와 라벨은 수정하지 않고 경로, 분할, 클래스, 라벨 형식만 확인합니다.",
                dataYamlFilePath);

            ExternalYoloDatasetIntakeWorkflowResult workflowResult = await externalYoloDatasetIntakeWorkflowService.RunAsync(
                new ExternalYoloDatasetIntakeWorkflowRequest
                {
                    DataYamlFilePath = dataYamlFilePath,
                    Purpose = purpose
                });
            if (!workflowResult.Started || workflowResult.IsCanceled)
            {
                return;
            }

            if (workflowResult.Error != null)
            {
                if (callbacks.IsApplicationCloseApproved?.Invoke() != true
                    && !externalYoloDatasetIntakeWorkflowService.IsClosed)
                {
                    SetExternalYoloDatasetIntakeResult(
                        purpose,
                        "외부 YOLO data.yaml: 확인 불가",
                        workflowResult.Error.Message,
                        dataYamlFilePath);
                }

                return;
            }

            if (callbacks.IsApplicationCloseApproved?.Invoke() == true
                || externalYoloDatasetIntakeWorkflowService.IsClosed)
            {
                return;
            }

            YoloExternalDatasetIntakeReport report = workflowResult.Report;
            bool useForTraining = callbacks.ApplyValidationResult?.Invoke(
                report,
                dataYamlFilePath,
                purpose,
                useForNextTraining) == true;
            if (report.IsReady)
            {
                callbacks.AppendLog?.Invoke($"외부 YOLO data.yaml 검증 완료: {System.IO.Path.GetFileName(report.DataYamlFilePath)} / {report.Summary} / 다음 학습 사용:{useForTraining}");
            }
            else
            {
                callbacks.AppendLog?.Invoke($"외부 YOLO data.yaml 검증 실패: {string.Join(" ", report.Errors.Take(2))}");
            }
        }

        public void SetYoloTrainingStepState(int order, bool isCompleted, string stateText)
        {
            WpfYoloTrainingWorkflowStepItem step = YoloTrainingWorkflowSteps.FirstOrDefault(item => item.Order == order);
            if (step == null)
            {
                return;
            }

            step.IsCompleted = isCompleted;
            step.StateText = string.IsNullOrWhiteSpace(stateText) ? (isCompleted ? "완료" : "대기") : stateText;
            step.StateIconKind = isCompleted ? PackIconMaterialKind.CheckCircleOutline : PackIconMaterialKind.ClockOutline;
            RefreshCurrentYoloTrainingStep();
        }

        private void RefreshCurrentYoloTrainingStep()
        {
            WpfYoloTrainingWorkflowStepItem nextStep = YoloTrainingWorkflowSteps.FirstOrDefault(item => !item.IsCompleted)
                ?? YoloTrainingWorkflowSteps.LastOrDefault();
            CurrentYoloTrainingStep = nextStep;
        }

        public void SetAnnotationHistoryState(bool canUndo, bool canRedo, string undoActionName, string redoActionName)
        {
            SetAnnotationToolRuntimeState(
                WpfAnnotationTool.Undo,
                canUndo,
                canUndo ? "\uAC00\uB2A5" : "\uC5C6\uC74C",
                canUndo
                    ? "\uB418\uB3CC\uB9AC\uAE30 \uAC00\uB2A5" + LearningWorkflowGuidanceService.FormatHistoryActionSuffix(undoActionName)
                    : "\uB418\uB3CC\uB9B4 \uD3B8\uC9D1 \uC774\uB825\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
            SetAnnotationToolRuntimeState(
                WpfAnnotationTool.Redo,
                canRedo,
                canRedo ? "\uAC00\uB2A5" : "\uC5C6\uC74C",
                canRedo
                    ? "\uB2E4\uC2DC \uC801\uC6A9 \uAC00\uB2A5" + LearningWorkflowGuidanceService.FormatHistoryActionSuffix(redoActionName)
                    : "\uB2E4\uC2DC \uC801\uC6A9\uD560 \uD3B8\uC9D1 \uC774\uB825\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
        }

        private void SetAnnotationToolRuntimeState(WpfAnnotationTool tool, bool isEnabled, string stateText, string statusText)
        {
            WpfAnnotationToolItem item = AnnotationTools.FirstOrDefault(candidate => candidate.Tool == tool);
            item?.SetRuntimeAvailability(isEnabled, stateText, statusText);
        }

        public string DatasetPurposeHeaderText => T("WpfLearningWorkflow.DatasetPurposeHeader");

        public string DatasetSetupSectionTitleText => T("WpfLearningWorkflow.DatasetSetupSectionTitle");

        public string DatasetOpenExistingButtonText => T("WpfLearningWorkflow.DatasetOpenExisting");

        public string DatasetOpenExistingToolTipText => T("WpfLearningWorkflow.DatasetOpenExisting.ToolTip");

        public string DatasetSetupStartToolTipText => T("WpfLearningWorkflow.DatasetSetupStart.ToolTip");

        public string DatasetPurposeSummaryText
        {
            get => datasetPurposeSummaryText;
            private set => SetProperty(ref datasetPurposeSummaryText, value ?? string.Empty);
        }

        public string DatasetPurposeToolSummaryText
        {
            get => datasetPurposeToolSummaryText;
            private set => SetProperty(ref datasetPurposeToolSummaryText, value ?? string.Empty);
        }

        public string DatasetSetupFirstActionText
        {
            get => datasetSetupFirstActionText;
            private set => SetProperty(ref datasetSetupFirstActionText, value ?? string.Empty);
        }

        public string DatasetSetupActionText
        {
            get => datasetSetupActionText;
            private set => SetProperty(ref datasetSetupActionText, value ?? string.Empty);
        }

        public string CurrentWorkflowActionText
        {
            get => currentWorkflowActionText;
            private set => SetProperty(ref currentWorkflowActionText, value ?? string.Empty);
        }

        public string DatasetSetupStatusText
        {
            get => datasetSetupStatusText;
            set => SetProperty(ref datasetSetupStatusText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskStepText
        {
            get => currentLabelingTaskStepText;
            private set => SetProperty(ref currentLabelingTaskStepText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskToolText
        {
            get => currentLabelingTaskToolText;
            private set => SetProperty(ref currentLabelingTaskToolText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskActionText
        {
            get => currentLabelingTaskActionText;
            private set => SetProperty(ref currentLabelingTaskActionText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskChecklistFirstText
        {
            get => currentLabelingTaskChecklistFirstText;
            private set => SetProperty(ref currentLabelingTaskChecklistFirstText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskChecklistSecondText
        {
            get => currentLabelingTaskChecklistSecondText;
            private set => SetProperty(ref currentLabelingTaskChecklistSecondText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskChecklistThirdText
        {
            get => currentLabelingTaskChecklistThirdText;
            private set => SetProperty(ref currentLabelingTaskChecklistThirdText, value ?? string.Empty);
        }

        public string CurrentLabelingTaskChecklistSummaryText
        {
            get => currentLabelingTaskChecklistSummaryText;
            private set => SetProperty(ref currentLabelingTaskChecklistSummaryText, value ?? string.Empty);
        }

        public Visibility DatasetOnboardingVisibility
        {
            get => datasetOnboardingVisibility;
            private set => SetProperty(ref datasetOnboardingVisibility, value);
        }

        public Visibility LabelingTaskVisibility
        {
            get => labelingTaskVisibility;
            private set => SetProperty(ref labelingTaskVisibility, value);
        }

        public void ShowDatasetOnboarding()
        {
            DatasetOnboardingVisibility = Visibility.Visible;
            LabelingTaskVisibility = Visibility.Collapsed;
        }

        public void ShowLabelingTask()
        {
            DatasetOnboardingVisibility = Visibility.Collapsed;
            LabelingTaskVisibility = Visibility.Visible;
        }

        public void SetLiveLabelingTask(CanvasWorkflowContext context)
        {
            SetLiveLabelingTask(
                context?.StepText,
                context?.ToolText,
                context?.ActionText);
        }

        public void SetLiveLabelingTask(string stepText, string toolText, string actionText)
        {
            CurrentLabelingTaskStepText = string.IsNullOrWhiteSpace(stepText)
                ? "\uB77C\uBCA8"
                : stepText.Trim();
            CurrentLabelingTaskToolText = string.IsNullOrWhiteSpace(toolText)
                ? "\uB3C4\uAD6C: \uC120\uD0DD"
                : LearningWorkflowGuidanceService.FormatLiveLabelingTaskToolText(toolText);
            CurrentLabelingTaskActionText = string.IsNullOrWhiteSpace(actionText)
                ? "\uB77C\uBCA8\uC744 \uADF8\uB9AC\uACE0 \uACB0\uACFC\uB97C \uD655\uC778\uD55C \uB4A4 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB204\uB974\uC138\uC694."
                : actionText.Trim();
            UpdateLiveLabelingTaskChecklist(
                CurrentLabelingTaskStepText,
                CurrentLabelingTaskToolText,
                CurrentLabelingTaskActionText);
        }

        private void UpdateLiveLabelingTaskChecklist(string stepText, string toolText, string actionText)
        {
            string normalizedStep = stepText?.Trim() ?? string.Empty;
            if (normalizedStep.IndexOf("\uAC80\uD1A0", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uD655\uC778", "\uD655\uC815", "\uC2A4\uD0B5");
                return;
            }

            if (normalizedStep.IndexOf("\uCD94\uB860", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uAC80\uC0AC", "\uD655\uC778", "\uAC80\uD1A0");
                return;
            }

            if (normalizedStep.IndexOf("\uC800\uC7A5", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uD655\uC778", "\uC800\uC7A5", "\uB2E4\uC74C");
                return;
            }

            if (normalizedStep.IndexOf("\uC0D8\uD50C", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uC774\uBBF8\uC9C0", "\uC5F4\uAE30", "\uB77C\uBCA8");
                return;
            }

            if (normalizedStep.IndexOf("\uB77C\uBCA8", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uADF8\uB9AC\uAE30", "\uC800\uC7A5", "\uB2E4\uC74C");
                return;
            }

            string combined = string.Join(
                " ",
                new[] { stepText, toolText, actionText }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (combined.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("\uD6C4\uBCF4", StringComparison.Ordinal) >= 0
                || combined.IndexOf("\uAC80\uD1A0", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uD655\uC778", "\uD655\uC815", "\uC2A4\uD0B5");
                return;
            }

            if (combined.IndexOf("\uCD94\uB860", StringComparison.Ordinal) >= 0
                || combined.IndexOf("\uAC80\uC0AC", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uAC80\uC0AC", "\uD655\uC778", "\uAC80\uD1A0");
                return;
            }

            if (combined.IndexOf("\uC800\uC7A5", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uD655\uC778", "\uC800\uC7A5", "\uB2E4\uC74C");
                return;
            }

            if (combined.IndexOf("\uC774\uBBF8\uC9C0 \uD050", StringComparison.Ordinal) >= 0
                || combined.IndexOf("\uC0D8\uD50C", StringComparison.Ordinal) >= 0)
            {
                SetCurrentLabelingTaskChecklist("\uC774\uBBF8\uC9C0", "\uC5F4\uAE30", "\uB77C\uBCA8");
                return;
            }

            SetCurrentLabelingTaskChecklist("\uADF8\uB9AC\uAE30", "\uC800\uC7A5", "\uB2E4\uC74C");
        }

        private void SetCurrentLabelingTaskChecklist(string first, string second, string third)
        {
            CurrentLabelingTaskChecklistFirstText = $"1  {first}";
            CurrentLabelingTaskChecklistSecondText = $"2  {second}";
            CurrentLabelingTaskChecklistThirdText = $"3  {third}";
            CurrentLabelingTaskChecklistSummaryText = $"\uD750\uB984: {first} > {second} > {third}";
        }

        public string ModeDetailText
        {
            get => modeDetailText;
            private set => SetProperty(ref modeDetailText, value ?? string.Empty);
        }

        public string StepDetailText
        {
            get => stepDetailText;
            private set => SetProperty(ref stepDetailText, value ?? string.Empty);
        }

        public string ToolDetailText
        {
            get => toolDetailText;
            private set => SetProperty(ref toolDetailText, value ?? string.Empty);
        }

        public int BrushSize
        {
            get => brushSize;
            set => SetProperty(ref brushSize, CanvasBrushSizePresentationService.Normalize(value));
        }

        public double MaskOpacity
        {
            get => maskOpacity;
            set
            {
                double normalized = Math.Clamp(value, 0.1, 1.0);
                if (SetProperty(ref maskOpacity, normalized))
                {
                    SetProperty(ref maskOpacityPercentText, $"{normalized:P0}", nameof(MaskOpacityPercentText));
                }
            }
        }

        private string maskOpacityPercentText = "66%";

        public string MaskOpacityPercentText
        {
            get => maskOpacityPercentText;
            private set => SetProperty(ref maskOpacityPercentText, value ?? string.Empty);
        }

        public WpfLearningModeItem SelectedDatasetPurposeMode
        {
            get => selectedDatasetPurposeMode;
            set
            {
                if (SetProperty(ref selectedDatasetPurposeMode, value))
                {
                    RefreshAnnotationToolScopeForMode(value?.Mode ?? WpfLearningMode.ObjectDetection);
                    RefreshLessonText();
                }
            }
        }

        public WpfLearningModeItem SelectedExternalYoloDatasetPurposeMode
        {
            get => selectedExternalYoloDatasetPurposeMode;
            set => SetProperty(ref selectedExternalYoloDatasetPurposeMode, value);
        }

        public WpfLearningModeItem SelectedMode
        {
            get => selectedMode;
            set
            {
                if (SetProperty(ref selectedMode, value))
                {
                    RefreshLessonText();
                }
            }
        }

        public WpfAnnotationToolItem SelectedTool
        {
            get => selectedTool;
            set
            {
                if (SetProperty(ref selectedTool, value))
                {
                    RefreshLessonText();
                }
            }
        }

        public WpfLearningStepItem SelectedStep
        {
            get => selectedStep;
            set
            {
                if (SetProperty(ref selectedStep, value))
                {
                    RefreshLessonText();
                }
            }
        }

        private void RefreshLessonText()
        {
            // Keep dataset-purpose UX copy in the ViewModel so the panel remains a display-only composition surface.
            DatasetPurposeSummaryText = LearningWorkflowGuidanceService.BuildDatasetPurposeSummaryText(
                SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);

            DatasetPurposeToolSummaryText = LearningWorkflowGuidanceService.BuildDatasetPurposeToolSummaryText(
                SelectedDatasetPurposeMode?.Mode);

            DatasetSetupActionText = LearningWorkflowGuidanceService.BuildDatasetSetupActionText(
                SelectedDatasetPurposeMode?.Mode);

            DatasetSetupFirstActionText = LearningWorkflowGuidanceService.BuildDatasetSetupFirstActionText(
                SelectedDatasetPurposeMode?.Mode);

            ModeDetailText = LearningWorkflowGuidanceService.BuildModeDetailText(
                SelectedMode?.Mode ?? WpfLearningMode.LabelingBasics);

            StepDetailText = LearningWorkflowGuidanceService.BuildStepDetailText(
                SelectedStep?.Step,
                SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);

            CurrentWorkflowActionText = LearningWorkflowGuidanceService.BuildCurrentWorkflowActionText(
                SelectedStep?.Step,
                SelectedDatasetPurposeMode?.Mode);

            ToolDetailText = LearningWorkflowGuidanceService.BuildToolDetailText(SelectedTool?.Tool);
        }

        private void RefreshYoloDatasetStructureItems()
        {
            YoloDatasetStructureItems.Clear();
            foreach (WpfYoloDatasetStructureItem item in LearningWorkflowCatalogService.BuildYoloDatasetStructureItems())
            {
                YoloDatasetStructureItems.Add(item);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ExternalEvaluationDataAuditStatusChanged = null;
            externalEvaluationDataAuditStatusSink = null;
            historicalSegmentationRemediationDataProvider = null;
            historicalSegmentationRemediationSourceImagePathProvider = null;
            historicalSegmentationRemediationCloseApproval = null;
            historicalSegmentationRemediationStatusSink = null;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            bool isDefaultDatasetSetupStatus = string.Equals(
                datasetSetupStatusText,
                "\uB370\uC774\uD130\uC14B \uC2DC\uC791 \uC804",
                StringComparison.Ordinal)
                || string.Equals(
                    datasetSetupStatusText,
                    "Before dataset start",
                    StringComparison.Ordinal);

            RefreshLessonText();
            RefreshYoloDatasetStructureItems();
            if (isDefaultDatasetSetupStatus)
            {
                DatasetSetupStatusText = T("WpfLearningWorkflow.DatasetSetupStatus.Before");
            }

            if (trainingChecklistLocalizationSnapshot != null)
            {
                SetTrainingChecklistLocalization(trainingChecklistLocalizationSnapshot);
            }

            if (trainingChecklistActionLocalizationSnapshot != null)
            {
                SetTrainingChecklistActionLocalization(trainingChecklistActionLocalizationSnapshot);
            }

            if (modelReplacementLocalizationSnapshot != null)
            {
                SetModelReplacementLocalization(modelReplacementLocalizationSnapshot);
            }

            if (trainingModelLifecycleLocalizationSnapshot != null)
            {
                SetTrainingModelLifecycleLocalization(trainingModelLifecycleLocalizationSnapshot);
            }

            if (trainingComparisonLocalizationSnapshot != null)
            {
                SetTrainingComparisonLocalization(trainingComparisonLocalizationSnapshot);
            }

            if (datasetDashboardLocalizationSnapshot != null)
            {
                DatasetDashboardStatusText = datasetDashboardLocalizationSnapshot.StatusText;
                DatasetDashboardSummaryText = datasetDashboardLocalizationSnapshot.SummaryText;
                string localizedActionText = datasetDashboardLocalizationSnapshot.ActionText;
                if (!string.IsNullOrWhiteSpace(localizedActionText))
                {
                    DatasetDashboardActionText = localizedActionText;
                }
                DatasetDashboardMetrics.Clear();
                foreach (WpfDatasetDashboardMetricItem metric in datasetDashboardLocalizationSnapshot.MetricItems)
                {
                    DatasetDashboardMetrics.Add(metric);
                }
                DatasetDashboardIssueItems.Clear();
                foreach (string issue in datasetDashboardLocalizationSnapshot.IssueItems)
                {
                    if (!string.IsNullOrWhiteSpace(issue))
                    {
                        DatasetDashboardIssueItems.Add(issue);
                    }
                }
            }

            ObjectDetectionMvpNextActionText = LearningWorkflowGuidanceService.BuildObjectDetectionMvpNextActionText(DatasetDashboardActionText);

            // The remaining panel captions are expression-backed so one owner-level
            // notification refreshes them without a visual-tree string rewrite.
            OnPropertyChanged(string.Empty);
        }
    }

    public sealed class ExternalYoloDatasetIntakeCallbacks
    {
        public Func<ExternalYoloDatasetSettings> SettingsProvider { get; init; }
        public Func<LabelingDatasetPurpose> SelectedPurposeProvider { get; init; }
        public Func<string, string> SelectDataYamlPath { get; init; }
        public Func<bool> IsApplicationCloseApproved { get; init; }
        public Action ClearSelection { get; init; }
        public Func<YoloExternalDatasetIntakeReport, string, LabelingDatasetPurpose, bool, bool> ApplyValidationResult { get; init; }
        public Action<string> AppendLog { get; init; }
    }
}
