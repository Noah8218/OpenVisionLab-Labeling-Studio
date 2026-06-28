using MahApps.Metro.IconPacks;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfLearningWorkflowPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<WpfYoloTrainingWorkflowStepItem> NoOpTrainingStepCommand = _ => { };
        private static readonly Action<WpfDatasetDashboardMetricItem> NoOpDatasetDashboardMetricCommand = _ => { };
        private const string DefaultTrainingResultComparisonText = "\uD559\uC2B5 \uACB0\uACFC \uBE44\uAD50: \uC544\uC9C1 \uBE44\uAD50\uD560 \uD559\uC2B5 \uACB0\uACFC\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";

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
        private string modeDetailText = string.Empty;
        private string stepDetailText = string.Empty;
        private string toolDetailText = string.Empty;
        private string trainingChecklistStatusText = "\uB370\uC774\uD130\uC14B: \uC810\uAC80 \uC804";
        private string trainingChecklistDetailText = "\uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 YOLO \uD0ED\uC5D0\uC11C \uC0C8\uB85C\uACE0\uCE68\uD558\uBA74 \uD559\uC2B5 \uAC00\uB2A5 \uC5EC\uBD80\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.";
        private string trainingChecklistActionText = "\uD544\uC694\uD55C \uD56D\uBAA9\uC774 \uBCF4\uC774\uBA74 \uC544\uB798 \uD574\uACB0 \uBC84\uD2BC\uC73C\uB85C \uBC14\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.";
        private string datasetDashboardStatusText = "\uB370\uC774\uD130\uC14B \uC0C1\uD0DC: \uC810\uAC80 \uC804";
        private string datasetDashboardSummaryText = "\uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uC2E4\uD589\uD558\uBA74 \uC774\uBBF8\uC9C0, \uB77C\uBCA8, \uBD84\uD560, \uD074\uB798\uC2A4 \uC0C1\uD0DC\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private string datasetDashboardActionText = "\uBA3C\uC800 \uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 \uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uB204\uB974\uC138\uC694.";
        private string objectDetectionMvpNextActionText = "\uAC1D\uCCB4\uD0D0\uC9C0 MVP: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uC804";
        private string modelReplacementStatusText = "\uBAA8\uB378 \uAD50\uCCB4: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uC804";
        private string modelReplacementDetailText = "\uD559\uC2B5 \uD6C4 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 \uACB0\uACFC\uB85C \uAE30\uC874 \uBAA8\uB378 \uAD50\uCCB4 \uC5EC\uBD80\uB97C \uD310\uB2E8\uD569\uB2C8\uB2E4.";
        private string trainingHistoryText = "\uCD5C\uADFC \uD559\uC2B5 \uC774\uB825: \uC544\uC9C1 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string trainingResultComparisonSummaryText = DefaultTrainingResultComparisonText;
        private string trainingResultComparisonText = DefaultTrainingResultComparisonText;
        private string trainingModelAdoptionDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uD559\uC2B5 \uACB0\uACFC \uBE44\uAD50 \uC804";
        private string runModelComparisonActionText = "\uBAA8\uB378 \uBE44\uAD50";
        private string runModelComparisonToolTipText = "\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uD559\uC2B5 \uBAA8\uB378\uC744 \uBE44\uAD50";
        private string modelComparisonBasisText = "\uBE44\uAD50 \uAE30\uC900: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 \uC218\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private bool isRunModelComparisonEnabled = true;
        private WpfYoloTrainingWorkflowStepItem currentYoloTrainingStep;
        private string currentYoloTrainingStepTitleText = string.Empty;
        private string currentYoloTrainingStepDetailText = string.Empty;
        private string currentYoloTrainingActionText = string.Empty;
        private bool hasCurrentYoloTrainingStep;
        private bool isYoloFixClassesEnabled = true;
        private bool isYoloFixLabelsEnabled;
        private bool isYoloFixDatasetEnabled = true;
        private int brushSize = 12;
        private double maskOpacity = 0.66;
        private ICommand datasetPurposeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand datasetSetupStartCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand learningModeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand annotationToolSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand learningStepSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand yoloTrainingWorkflowStepCommand = new RelayCommand<WpfYoloTrainingWorkflowStepItem>(NoOpTrainingStepCommand);
        private ICommand datasetDashboardMetricCommand = new RelayCommand<WpfDatasetDashboardMetricItem>(NoOpDatasetDashboardMetricCommand);
        private ICommand tutorialOpenHtmlGuideCommand = new RelayCommand(NoOpCommand);
        private ICommand yoloFixClassesCommand = new RelayCommand(NoOpCommand);
        private ICommand yoloFixLabelsCommand = new RelayCommand(NoOpCommand);
        private ICommand yoloFixDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand runModelComparisonCommand = new RelayCommand(NoOpCommand);

        public WpfLearningWorkflowPanelViewModel()
        {
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.LabelingBasics, "\uB77C\uBCA8\uB9C1", PackIconMaterialKind.SchoolOutline, "\uC815\uB2F5 \uB77C\uBCA8\uC744 \uADF8\uB9AC\uB294 \uD750\uB984"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.ObjectDetection, "\uAC1D\uCCB4 \uD0D0\uC9C0", PackIconMaterialKind.ShapeSquareRoundedPlus, "YOLO \uBC15\uC2A4 \uB77C\uBCA8\uACFC \uAC80\uCD9C \uD6C4\uBCF4 \uAC80\uD1A0"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Segmentation, "\uC138\uADF8\uBA58\uD14C\uC774\uC158", PackIconMaterialKind.ViewListOutline, "\uD3F4\uB9AC\uACE4\uACFC \uB9C8\uC2A4\uD06C \uB77C\uBCA8"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.AnomalyDetection, "\uC774\uC0C1 \uD0D0\uC9C0", PackIconMaterialKind.AlertCircleOutline, "\uC815\uC0C1/\uC774\uC0C1 \uC0D8\uD50C\uACFC \uC601\uC5ED \uAC80\uD1A0"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Train, "\uD559\uC2B5", PackIconMaterialKind.PlayCircleOutline, "\uB370\uC774\uD130\uC14B \uC900\uBE44\uC640 \uD559\uC2B5"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Infer, "\uCD94\uB860", PackIconMaterialKind.RobotIndustrial, "\uBAA8\uB378 \uC2E4\uD589\uACFC \uC608\uCE21 \uD655\uC778"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Review, "\uAC80\uD1A0", PackIconMaterialKind.CheckAll, "\uC608\uCE21\uC744 \uD655\uC815 \uB77C\uBCA8\uB85C \uC804\uD658"));
            DatasetPurposeModes.Add(LearningModes.First(item => item.Mode == WpfLearningMode.ObjectDetection));
            DatasetPurposeModes.Add(LearningModes.First(item => item.Mode == WpfLearningMode.Segmentation));
            DatasetPurposeModes.Add(LearningModes.First(item => item.Mode == WpfLearningMode.AnomalyDetection));

            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Select, "\uC120\uD0DD", PackIconMaterialKind.Tune, "\uAC1D\uCCB4 \uC120\uD0DD\uACFC \uD3B8\uC9D1"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Rectangle, "\uBC15\uC2A4", PackIconMaterialKind.ShapeSquareRoundedPlus, "YOLO \uBC15\uC2A4 \uC601\uC5ED"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Ellipse, "\uC6D0/\uD0C0\uC6D0", PackIconMaterialKind.ShapeSquareRoundedPlus, "\uC6D0\uD615 \uD639\uC740 \uD0C0\uC6D0 \uC601\uC5ED"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Polygon, "\uD3F4\uB9AC\uACE4", PackIconMaterialKind.ViewListOutline, "\uB2E4\uAC01\uD615 \uC138\uADF8\uBA58\uD14C\uC774\uC158"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Brush, "\uBE0C\uB7EC\uC2DC", PackIconMaterialKind.Tune, "\uBE0C\uB7EC\uC2DC \uB9C8\uC2A4\uD06C \uD3B8\uC9D1"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Eraser, "\uC9C0\uC6B0\uAC1C", PackIconMaterialKind.TrashCanOutline, "\uB9C8\uC2A4\uD06C\uB098 \uC601\uC5ED \uC9C0\uC6B0\uAE30"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.PanZoom, "\uC774\uB3D9", PackIconMaterialKind.Magnify, "\uD654\uBA74 \uC774\uB3D9\uACFC \uD655\uB300"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Undo, "\uB418\uB3CC\uB9AC\uAE30", PackIconMaterialKind.Refresh, "\uB9C8\uC9C0\uB9C9 \uD3B8\uC9D1 \uB418\uB3CC\uB9AC\uAE30"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Redo, "\uB2E4\uC2DC \uC801\uC6A9", PackIconMaterialKind.Reload, "\uB418\uB3CC\uB9B0 \uD3B8\uC9D1 \uB2E4\uC2DC \uC801\uC6A9"));
            RegisterAnnotationTool(new WpfAnnotationToolItem(WpfAnnotationTool.Delete, "\uC0AD\uC81C", PackIconMaterialKind.TrashCanOutline, "\uC120\uD0DD \uB77C\uBCA8 \uC0AD\uC81C"));
            ApplyDatasetPurpose(LabelingDatasetPurpose.ObjectDetection);

            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Sample, "\uC0D8\uD50C", PackIconMaterialKind.FolderImage));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Label, "\uB77C\uBCA8", PackIconMaterialKind.ShapeSquareRoundedPlus));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Infer, "\uCD94\uB860", PackIconMaterialKind.RobotIndustrial));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Review, "\uB9AC\uBDF0", PackIconMaterialKind.CheckAll));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Save, "\uC800\uC7A5", PackIconMaterialKind.ContentSaveOutline));

            TutorialChecklistItems.Add("\uC0D8\uD50C \uB610\uB294 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC5F4\uACE0 \uC88C\uCE21 \uD050\uC5D0\uC11C \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD0ED\uC5D0\uC11C OK, NG\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uB4F1\uB85D\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uB77C\uBCA8\uB9C1 \uBAA8\uB4DC\uC5D0\uC11C \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uC800\uC7A5\uD558\uC5EC YOLO txt\uB97C \uB9CC\uB4ED\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uB370\uC774\uD130\uC14B \uC810\uAC80\uC73C\uB85C \uB77C\uBCA8, \uD074\uB798\uC2A4, \uD559\uC2B5 \uC124\uC815\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("YOLO \uD559\uC2B5\uC744 \uC2E4\uD589\uD558\uACE0 \uC644\uB8CC \uD6C4 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC744 \uC801\uC6A9\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uD604\uC7AC \uAC80\uC0AC\uB85C \uAC80\uCD9C \uD6C4\uBCF4\uB97C \uD655\uC778\uD558\uACE0 \uD655\uC815 \uB610\uB294 \uC2A4\uD0B5\uD569\uB2C8\uB2E4.");

            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                1,
                "\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC5F4\uAE30",
                "\uD559\uC2B5\uD560 N\uAC1C \uC774\uBBF8\uC9C0\uB97C \uD050\uC5D0 \uC62C\uB9BD\uB2C8\uB2E4.",
                "\uC67C\uCABD \uC774\uBBF8\uC9C0 \uD050\uC5D0 \uC0D8\uD50C\uACFC \uD30C\uC77C \uC218\uAC00 \uBCF4\uC774\uBA74 \uB2E4\uC74C \uB2E8\uACC4\uC785\uB2C8\uB2E4.",
                PackIconMaterialKind.FolderImage));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                2,
                "\uD074\uB798\uC2A4 \uB4F1\uB85D",
                "OK, NG, defect\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uBA3C\uC800 \uB9CC\uB4ED\uB2C8\uB2E4.",
                "\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD0ED\uC5D0\uC11C \uBAA9\uB85D\uACFC \uC800\uC7A5 \uACBD\uB85C\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                PackIconMaterialKind.TagMultipleOutline));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                3,
                "\uBC15\uC2A4 \uB77C\uBCA8\uB9C1",
                "\uAC01 \uC774\uBBF8\uC9C0\uC5D0\uC11C \uAC1D\uCCB4\uB97C \uBC15\uC2A4\uB85C \uADF8\uB9AC\uACE0 \uD074\uB798\uC2A4\uB97C \uBD99\uC785\uB2C8\uB2E4.",
                "\uB77C\uBCA8 \uC218\uAC00 \uB298\uACE0 \uC800\uC7A5\uD558\uBA74 YOLO txt\uAC00 \uC0DD\uC131\uB429\uB2C8\uB2E4.",
                PackIconMaterialKind.ShapeSquareRoundedPlus));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                4,
                "\uC800\uC7A5\uACFC \uB370\uC774\uD130\uC14B \uC810\uAC80",
                "\uB77C\uBCA8\uC774 \uBE60\uC9C4 \uC774\uBBF8\uC9C0, \uD074\uB798\uC2A4, \uD559\uC2B5 \uC124\uC815\uC744 \uAC80\uC0AC\uD569\uB2C8\uB2E4.",
                "YOLO \uD0ED\uC758 \uC0C8\uB85C\uACE0\uCE68\uC5D0\uC11C \uD559\uC2B5 \uAC00\uB2A5 \uC0C1\uD0DC\uAC00 \uB098\uC640\uC57C \uD569\uB2C8\uB2E4.",
                PackIconMaterialKind.CheckAll));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                5,
                "YOLO \uD559\uC2B5",
                "\uC774\uBBF8\uC9C0 \uD06C\uAE30, \uBC30\uCE58, \uC5D0\uD3ED, \uAC00\uC911\uCE58\uB97C \uD655\uC778\uD558\uACE0 \uD559\uC2B5\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4.",
                "\uC9C4\uD589\uB960\uACFC \uC5D0\uD3ED\uC744 \uBCF4\uACE0, \uC644\uB8CC \uD6C4 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.",
                PackIconMaterialKind.PlayCircleOutline));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                6,
                "\uD559\uC2B5 \uACB0\uACFC \uCD94\uB860 \uAC80\uD1A0",
                "\uC0C8\uB85C \uB9CC\uB4E0 \uBAA8\uB378\uB85C \uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uACE0 \uD6C4\uBCF4\uB97C \uD655\uC815\uD569\uB2C8\uB2E4.",
                "\uACB0\uACFC\uAC00 \uB9DE\uC73C\uBA74 \uB77C\uBCA8\uB85C \uD655\uC815\uD558\uACE0, \uD2C0\uB9AC\uBA74 \uB370\uC774\uD130\uB97C \uCD94\uAC00\uD569\uB2C8\uB2E4.",
                PackIconMaterialKind.RobotIndustrial));

            // Keep the file-format lesson close to dataset status: saving a box creates a paired label txt, not a hidden app-only object.
            YoloDatasetStructureItems.Add(new WpfYoloDatasetStructureItem("data.yaml", "\uD3F4\uB354/\uD074\uB798\uC2A4 \uBAA9\uB85D", "\uD559\uC2B5\uC774 \uC77D\uC744 \uC704\uCE58", PackIconMaterialKind.FileCodeOutline));
            YoloDatasetStructureItems.Add(new WpfYoloDatasetStructureItem("images", "\uC6D0\uBCF8 \uC774\uBBF8\uC9C0", "train / valid / test", PackIconMaterialKind.FolderImage));
            YoloDatasetStructureItems.Add(new WpfYoloDatasetStructureItem("labels", "\uC815\uB2F5 \uB77C\uBCA8", "\uAC19\uC740 \uC774\uB984\uC758 txt", PackIconMaterialKind.FileDocumentOutline));
            YoloDatasetStructureItems.Add(new WpfYoloDatasetStructureItem("txt 1\uC904", "\uD074\uB798\uC2A4 + \uBC15\uC2A4", "\uC911\uC2EC x/y, \uB108\uBE44, \uB192\uC774", PackIconMaterialKind.FormatListNumbered));

            RefreshCurrentYoloTrainingStep();
            SelectedMode = LearningModes.FirstOrDefault(item => item.Mode == WpfLearningMode.LabelingBasics) ?? LearningModes.FirstOrDefault();
            SelectedTool = SelectableAnnotationTools.FirstOrDefault();
            SelectedStep = LearningSteps.FirstOrDefault();
            SetAnnotationHistoryState(canUndo: false, canRedo: false, undoActionName: string.Empty, redoActionName: string.Empty);
            SetDatasetDashboard(
                datasetDashboardStatusText,
                datasetDashboardSummaryText,
                datasetDashboardActionText,
                BuildInitialDatasetDashboardMetrics(),
                new[] { "\uC810\uAC80 \uC804: \uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uC2E4\uD589\uD558\uBA74 \uBB38\uC81C \uD56D\uBAA9\uC774 \uD45C\uC2DC\uB429\uB2C8\uB2E4." });
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

        public ObservableCollection<WpfLearningModeItem> LearningModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfLearningModeItem> DatasetPurposeModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfAnnotationToolItem> AnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfAnnotationToolItem> SelectableAnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfAnnotationToolItem> AnnotationCommandTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfAnnotationToolItem> VisibleAnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfLearningStepItem> LearningSteps { get; } = new ObservableCollection<WpfLearningStepItem>();

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

        public string TrainingWorkflowSummaryText => "\uC774\uBBF8\uC9C0 \u2192 \uD074\uB798\uC2A4 \u2192 \uB77C\uBCA8 \u2192 \uC810\uAC80 \u2192 \uD559\uC2B5 \u2192 \uCD94\uB860/\uAC80\uD1A0";

        public string YoloDatasetStructureTitleText => "\uC800\uC7A5 \uAD6C\uC870";

        public string YoloDatasetStructureSummaryText => "\uC124\uC815 \uD30C\uC77C\uC774 \uC774\uBBF8\uC9C0 \uD3F4\uB354, \uB77C\uBCA8 \uD3F4\uB354, \uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC54C\uB824\uC90D\uB2C8\uB2E4.";

        public string YoloDatasetPairSummaryText => "\uC608: image_001.jpg \u2194 image_001.txt / txt=\uD074\uB798\uC2A4+\uBC15\uC2A4";

        public string GroundTruthChipText => "\uC815\uB2F5 \uB77C\uBCA8";

        public string PredictionChipText => "\uAC80\uCD9C \uACB0\uACFC";

        public string TrainingChecklistStatusText
        {
            get => trainingChecklistStatusText;
            set => SetProperty(ref trainingChecklistStatusText, value ?? string.Empty);
        }

        public string TrainingChecklistDetailText
        {
            get => trainingChecklistDetailText;
            set => SetProperty(ref trainingChecklistDetailText, value ?? string.Empty);
        }

        public string TrainingChecklistActionText
        {
            get => trainingChecklistActionText;
            set => SetProperty(ref trainingChecklistActionText, value ?? string.Empty);
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

        public string ObjectDetectionMvpNextActionText
        {
            get => objectDetectionMvpNextActionText;
            private set => SetProperty(ref objectDetectionMvpNextActionText, value ?? string.Empty);
        }

        public string ModelReplacementStatusText
        {
            get => modelReplacementStatusText;
            set => SetProperty(ref modelReplacementStatusText, value ?? string.Empty);
        }

        public string ModelReplacementDetailText
        {
            get => modelReplacementDetailText;
            set => SetProperty(ref modelReplacementDetailText, value ?? string.Empty);
        }

        public string TrainingHistoryText
        {
            get => trainingHistoryText;
            set => SetProperty(ref trainingHistoryText, value ?? string.Empty);
        }

        public string TrainingResultComparisonText
        {
            get => trainingResultComparisonText;
            set => SetProperty(ref trainingResultComparisonText, string.IsNullOrWhiteSpace(value) ? DefaultTrainingResultComparisonText : value);
        }

        public string TrainingResultComparisonSummaryText
        {
            get => trainingResultComparisonSummaryText;
            set => SetProperty(ref trainingResultComparisonSummaryText, string.IsNullOrWhiteSpace(value) ? DefaultTrainingResultComparisonText : value);
        }

        public string TrainingModelAdoptionDecisionText
        {
            get => trainingModelAdoptionDecisionText;
            set => SetProperty(ref trainingModelAdoptionDecisionText, string.IsNullOrWhiteSpace(value) ? "\uAD50\uCCB4 \uD310\uB2E8: \uD559\uC2B5 \uACB0\uACFC \uBE44\uAD50 \uC804" : value);
        }

        public string RunModelComparisonActionText
        {
            get => runModelComparisonActionText;
            private set => SetProperty(ref runModelComparisonActionText, string.IsNullOrWhiteSpace(value) ? "\uBAA8\uB378 \uBE44\uAD50" : value);
        }

        public string RunModelComparisonToolTipText
        {
            get => runModelComparisonToolTipText;
            private set => SetProperty(ref runModelComparisonToolTipText, string.IsNullOrWhiteSpace(value) ? "\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uD559\uC2B5 \uBAA8\uB378\uC744 \uBE44\uAD50" : value);
        }

        public string ModelComparisonBasisText
        {
            get => modelComparisonBasisText;
            private set => SetProperty(ref modelComparisonBasisText, string.IsNullOrWhiteSpace(value) ? "\uBE44\uAD50 \uAE30\uC900: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD45C\uC2DC\uB429\uB2C8\uB2E4." : value);
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
                        : ResolveCurrentYoloTrainingActionText(value);
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
            if (IsOneShotCommandTool(tool.Tool))
            {
                AnnotationCommandTools.Add(tool);
                return;
            }

            SelectableAnnotationTools.Add(tool);
        }

        private static bool IsOneShotCommandTool(WpfAnnotationTool tool)
            => tool == WpfAnnotationTool.Undo
                || tool == WpfAnnotationTool.Redo
                || tool == WpfAnnotationTool.Delete;

        private void RefreshAnnotationToolScopeForMode(WpfLearningMode mode)
        {
            // Dataset purpose owns which drawing tools are visible. Keep the full
            // AnnotationTools catalog for commands/tests, but only expose tools that
            // match the labeling task so operators do not see irrelevant controls.
            HashSet<WpfAnnotationTool> visibleSelectableTools = new HashSet<WpfAnnotationTool>(ResolveSelectableToolsForMode(mode));
            SelectableAnnotationTools.Clear();
            AnnotationCommandTools.Clear();
            VisibleAnnotationTools.Clear();

            foreach (WpfAnnotationToolItem tool in AnnotationTools)
            {
                if (IsOneShotCommandTool(tool.Tool))
                {
                    AnnotationCommandTools.Add(tool);
                    VisibleAnnotationTools.Add(tool);
                    continue;
                }

                if (visibleSelectableTools.Contains(tool.Tool))
                {
                    SelectableAnnotationTools.Add(tool);
                    VisibleAnnotationTools.Add(tool);
                }
            }

            if (SelectedTool == null || !SelectableAnnotationTools.Contains(SelectedTool))
            {
                SelectedTool = SelectableAnnotationTools.FirstOrDefault();
            }
        }

        private static IEnumerable<WpfAnnotationTool> ResolveSelectableToolsForMode(WpfLearningMode mode)
        {
            switch (mode)
            {
                case WpfLearningMode.Segmentation:
                    return new[]
                    {
                        WpfAnnotationTool.Select,
                        WpfAnnotationTool.Polygon,
                        WpfAnnotationTool.Brush,
                        WpfAnnotationTool.Eraser,
                        WpfAnnotationTool.PanZoom
                    };

                case WpfLearningMode.AnomalyDetection:
                    return new[]
                    {
                        WpfAnnotationTool.Select,
                        WpfAnnotationTool.Rectangle,
                        WpfAnnotationTool.Brush,
                        WpfAnnotationTool.Eraser,
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
            Action runModelComparison = null)
        {
            // Dataset purpose is a project setting; learning mode is only guide/navigation.
            // Keep both command paths separate so task-specific tools do not change when the operator browses lesson concepts.
            DatasetPurposeSelectionChangedCommand = new RelayCommand<object>(datasetPurposeSelectionChanged ?? NoOpSelectionCommand);
            DatasetSetupStartCommand = new RelayCommand<object>(datasetSetupStart ?? NoOpSelectionCommand);
            LearningModeSelectionChangedCommand = new RelayCommand<object>(learningModeSelectionChanged ?? NoOpSelectionCommand);
            AnnotationToolSelectionChangedCommand = new RelayCommand<object>(annotationToolSelectionChanged ?? NoOpSelectionCommand);
            LearningStepSelectionChangedCommand = new RelayCommand<object>(learningStepSelectionChanged ?? NoOpSelectionCommand);
            YoloTrainingWorkflowStepCommand = new RelayCommand<WpfYoloTrainingWorkflowStepItem>(yoloTrainingWorkflowStep ?? NoOpTrainingStepCommand);
            DatasetDashboardMetricCommand = new RelayCommand<WpfDatasetDashboardMetricItem>(datasetDashboardMetricSelected ?? NoOpDatasetDashboardMetricCommand);
            TutorialOpenHtmlGuideCommand = new RelayCommand(tutorialOpenHtmlGuide ?? NoOpCommand);
            YoloFixClassesCommand = new RelayCommand(yoloFixClasses ?? NoOpCommand);
            YoloFixLabelsCommand = new RelayCommand(yoloFixLabels ?? NoOpCommand);
            YoloFixDatasetCommand = new RelayCommand(yoloFixDataset ?? NoOpCommand);
            RunModelComparisonCommand = new RelayCommand(runModelComparison ?? NoOpCommand);
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

        public void SetModelComparisonRunState(bool enabled, string actionText)
            => SetModelComparisonRunState(enabled, actionText, string.Empty);

        public void SetModelComparisonRunState(bool enabled, string actionText, string toolTipText)
            => SetModelComparisonRunState(enabled, actionText, toolTipText, string.Empty);

        public void SetModelComparisonRunState(bool enabled, string actionText, string toolTipText, string basisText)
        {
            IsRunModelComparisonEnabled = enabled;
            RunModelComparisonActionText = actionText;
            RunModelComparisonToolTipText = toolTipText;
            ModelComparisonBasisText = basisText;
        }

        public void SetModelReplacementReadiness(string statusText, string detailText)
        {
            // Training readiness and model replacement are intentionally separate:
            // Keep replacement stricter than training readiness: users may train with learning/validation data only,
            // but switching the active model needs a separate final-verification set.
            ModelReplacementStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "\uBAA8\uB378 \uAD50\uCCB4: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uC804"
                : statusText;
            ModelReplacementDetailText = string.IsNullOrWhiteSpace(detailText)
                ? "\uD559\uC2B5 \uD6C4 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 \uACB0\uACFC\uB85C \uAE30\uC874 \uBAA8\uB378 \uAD50\uCCB4 \uC5EC\uBD80\uB97C \uD310\uB2E8\uD569\uB2C8\uB2E4."
                : detailText;
        }

        public void SetDatasetDashboard(
            string statusText,
            string summaryText,
            string actionText,
            IEnumerable<WpfDatasetDashboardMetricItem> metrics,
            IEnumerable<string> issues)
        {
            DatasetDashboardStatusText = string.IsNullOrWhiteSpace(statusText) ? "\uB370\uC774\uD130\uC14B \uC0C1\uD0DC: \uC810\uAC80 \uC804" : statusText;
            DatasetDashboardSummaryText = string.IsNullOrWhiteSpace(summaryText) ? string.Empty : summaryText;
            DatasetDashboardActionText = string.IsNullOrWhiteSpace(actionText) ? string.Empty : actionText;
            ObjectDetectionMvpNextActionText = BuildObjectDetectionMvpNextActionText(DatasetDashboardActionText);

            DatasetDashboardMetrics.Clear();
            foreach (WpfDatasetDashboardMetricItem item in metrics ?? Enumerable.Empty<WpfDatasetDashboardMetricItem>())
            {
                if (item != null)
                {
                    DatasetDashboardMetrics.Add(item);
                }
            }

            DatasetDashboardIssueItems.Clear();
            foreach (string issue in issues ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    DatasetDashboardIssueItems.Add(issue);
                }
            }

            if (DatasetDashboardIssueItems.Count == 0)
            {
                DatasetDashboardIssueItems.Add("\uBB38\uC81C \uC5C6\uC74C: \uB2E4\uC74C \uB2E8\uACC4\uB85C \uC774\uB3D9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.");
            }
        }

        private static string BuildObjectDetectionMvpNextActionText(string actionText)
        {
            if (string.IsNullOrWhiteSpace(actionText))
            {
                return "\uAC1D\uCCB4\uD0D0\uC9C0 MVP: \uC810\uAC80 \uACB0\uACFC \uD655\uC778 \uC804";
            }

            string normalized = actionText.Trim();
            if (normalized.StartsWith("\uC644\uB8CC:", StringComparison.Ordinal))
            {
                return "\uAC1D\uCCB4\uD0D0\uC9C0 MVP: \uC644\uB8CC - \uD559\uC2B5\uACFC \uCD5C\uC885 \uAC80\uC99D\uC73C\uB85C \uC774\uB3D9";
            }

            const string nextPrefix = "\uB2E4\uC74C:";
            if (normalized.StartsWith(nextPrefix, StringComparison.Ordinal))
            {
                normalized = normalized.Substring(nextPrefix.Length).Trim();
            }

            return "\uAC1D\uCCB4\uD0D0\uC9C0 MVP \uC644\uB8CC\uAE4C\uC9C0: " + normalized;
        }

        private static IEnumerable<WpfDatasetDashboardMetricItem> BuildInitialDatasetDashboardMetrics()
        {
            yield return new WpfDatasetDashboardMetricItem("\uC774\uBBF8\uC9C0", "-", "\uC810\uAC80 \uC804", "\uB300\uAE30", PackIconMaterialKind.FolderImage, isProblem: false, isWarning: false, actionKind: WpfDatasetDashboardActionKind.OpenImages);
            yield return new WpfDatasetDashboardMetricItem("\uC9C4\uD589", "-", "\uC810\uAC80 \uC804", "\uB300\uAE30", PackIconMaterialKind.ProgressClock, isProblem: false, isWarning: false, actionKind: WpfDatasetDashboardActionKind.OpenLabelingProgress);
            yield return new WpfDatasetDashboardMetricItem("\uB77C\uBCA8", "-", "\uC810\uAC80 \uC804", "\uB300\uAE30", PackIconMaterialKind.ShapeSquareRoundedPlus, isProblem: false, isWarning: false, actionKind: WpfDatasetDashboardActionKind.OpenLabelingTool);
            yield return new WpfDatasetDashboardMetricItem("\uBD84\uD560", "-", "\uC810\uAC80 \uC804", "\uB300\uAE30", PackIconMaterialKind.CheckAll, isProblem: false, isWarning: false, actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings);
            yield return new WpfDatasetDashboardMetricItem("\uD074\uB798\uC2A4", "-", "\uC810\uAC80 \uC804", "\uB300\uAE30", PackIconMaterialKind.TagMultipleOutline, isProblem: false, isWarning: false, actionKind: WpfDatasetDashboardActionKind.OpenClassCatalog);
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

        private static string ResolveCurrentYoloTrainingActionText(WpfYoloTrainingWorkflowStepItem step)
        {
            return step?.Order switch
            {
                1 => "이미지 폴더 열기",
                2 => "클래스 등록",
                3 => "라벨링 시작",
                4 => "데이터셋 점검",
                5 => "학습 설정 확인",
                6 => "추론 검토",
                _ => "이 단계로 이동"
            };
        }

        public void SetAnnotationHistoryState(bool canUndo, bool canRedo, string undoActionName, string redoActionName)
        {
            SetAnnotationToolRuntimeState(
                WpfAnnotationTool.Undo,
                canUndo,
                canUndo ? "\uAC00\uB2A5" : "\uC5C6\uC74C",
                canUndo
                    ? "\uB418\uB3CC\uB9AC\uAE30 \uAC00\uB2A5" + FormatHistoryActionSuffix(undoActionName)
                    : "\uB418\uB3CC\uB9B4 \uD3B8\uC9D1 \uC774\uB825\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
            SetAnnotationToolRuntimeState(
                WpfAnnotationTool.Redo,
                canRedo,
                canRedo ? "\uAC00\uB2A5" : "\uC5C6\uC74C",
                canRedo
                    ? "\uB2E4\uC2DC \uC801\uC6A9 \uAC00\uB2A5" + FormatHistoryActionSuffix(redoActionName)
                    : "\uB2E4\uC2DC \uC801\uC6A9\uD560 \uD3B8\uC9D1 \uC774\uB825\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
        }

        private void SetAnnotationToolRuntimeState(WpfAnnotationTool tool, bool isEnabled, string stateText, string statusText)
        {
            WpfAnnotationToolItem item = AnnotationTools.FirstOrDefault(candidate => candidate.Tool == tool);
            item?.SetRuntimeAvailability(isEnabled, stateText, statusText);
        }

        private static string FormatHistoryActionSuffix(string actionName)
            => string.IsNullOrWhiteSpace(actionName) ? string.Empty : $": {actionName}";

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
            set => SetProperty(ref brushSize, Math.Clamp(value, 2, 64));
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
            DatasetPurposeSummaryText = SelectedDatasetPurposeMode?.Mode switch
            {
                WpfLearningMode.ObjectDetection => "객체 탐지 데이터셋: 박스 라벨만 먼저 보입니다. YOLO처럼 위치를 학습하는 흐름입니다.",
                WpfLearningMode.Segmentation => "세그멘테이션 데이터셋: 폴리곤, 브러시, 지우개로 픽셀 마스크를 만듭니다.",
                WpfLearningMode.AnomalyDetection => "이상 탐지 데이터셋: 박스 또는 마스크로 결함 영역을 표시합니다.",
                WpfLearningMode.Train => "학습 단계: 현재 선택한 데이터셋을 점검하고 모델 학습을 시작합니다.",
                WpfLearningMode.Infer => "추론 단계: 학습된 모델로 후보를 만들고 검토합니다.",
                WpfLearningMode.Review => "검토 단계: 검출 후보를 정답 라벨로 확정하거나 제외합니다.",
                _ => "데이터셋 목적을 먼저 선택하면 필요한 라벨링 도구만 표시됩니다."
            };

            DatasetPurposeToolSummaryText = SelectedDatasetPurposeMode?.Mode switch
            {
                WpfLearningMode.ObjectDetection => "\uD45C\uC2DC \uB3C4\uAD6C: \uC120\uD0DD, \uBC15\uC2A4, \uC774\uB3D9. \uBE0C\uB7EC\uC2DC/\uC9C0\uC6B0\uAC1C\uB294 \uC228\uACA8 \uBC15\uC2A4 \uB77C\uBCA8\uB9C1\uC5D0 \uC9D1\uC911\uD569\uB2C8\uB2E4.",
                WpfLearningMode.Segmentation => "\uD45C\uC2DC \uB3C4\uAD6C: \uC120\uD0DD, \uD3F4\uB9AC\uACE4, \uBE0C\uB7EC\uC2DC, \uC9C0\uC6B0\uAC1C, \uC774\uB3D9. \uD53D\uC140 \uB9C8\uC2A4\uD06C \uC791\uC5C5\uC5D0 \uD544\uC694\uD55C \uB3C4\uAD6C\uB9CC \uBCF4\uC785\uB2C8\uB2E4.",
                WpfLearningMode.AnomalyDetection => "\uD45C\uC2DC \uB3C4\uAD6C: \uC120\uD0DD, \uBC15\uC2A4, \uBE0C\uB7EC\uC2DC, \uC9C0\uC6B0\uAC1C, \uC774\uB3D9. \uACB0\uD568 \uC601\uC5ED\uC744 \uBE60\uB974\uAC8C \uD45C\uC2DC\uD569\uB2C8\uB2E4.",
                _ => "\uB370\uC774\uD130\uC14B \uBAA9\uC801\uC5D0 \uB9DE\uB294 \uB3C4\uAD6C\uB9CC \uD45C\uC2DC\uD569\uB2C8\uB2E4."
            };

            DatasetSetupActionText = SelectedDatasetPurposeMode?.Mode switch
            {
                WpfLearningMode.ObjectDetection => "\uBC15\uC2A4 \uB370\uC774\uD130\uC14B",
                WpfLearningMode.Segmentation => "\uC138\uADF8 \uB370\uC774\uD130\uC14B",
                WpfLearningMode.AnomalyDetection => "\uC774\uC0C1\uD0D0\uC9C0 \uB370\uC774\uD130\uC14B",
                _ => "\uB370\uC774\uD130\uC14B \uC2DC\uC791"
            };

            DatasetSetupFirstActionText = SelectedDatasetPurposeMode?.Mode switch
            {
                WpfLearningMode.ObjectDetection => "\uCC98\uC74C \uC2DC\uC791: \uAC1D\uCCB4\uD0D0\uC9C0\uB97C \uACE0\uB974\uACE0 \uBC15\uC2A4 \uB370\uC774\uD130\uC14B\uC744 \uBA3C\uC800 \uC900\uBE44\uD558\uC138\uC694.",
                WpfLearningMode.Segmentation => "\uCC98\uC74C \uC2DC\uC791: \uC138\uADF8\uBA58\uD14C\uC774\uC158\uC744 \uACE0\uB974\uACE0 \uB9C8\uC2A4\uD06C \uB370\uC774\uD130\uC14B\uC744 \uBA3C\uC800 \uC900\uBE44\uD558\uC138\uC694.",
                WpfLearningMode.AnomalyDetection => "\uCC98\uC74C \uC2DC\uC791: \uC774\uC0C1\uD0D0\uC9C0\uB97C \uACE0\uB974\uACE0 \uC815\uC0C1/\uC774\uC0C1 \uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC900\uBE44\uD558\uC138\uC694.",
                _ => "\uCC98\uC74C \uC2DC\uC791: \uBAA9\uC801\uC744 \uACE0\uB974\uACE0 \uB370\uC774\uD130\uC14B\uC744 \uBA3C\uC800 \uC900\uBE44\uD558\uC138\uC694."
            };

            ModeDetailText = SelectedMode?.Mode switch
            {
                WpfLearningMode.ObjectDetection => "YOLO: 이미지 안의 객체 위치를 박스로 찾고, 후보를 정답 라벨로 확정합니다.",
                WpfLearningMode.Segmentation => "U-Net: 픽셀 단위 마스크를 만들며, 폴리곤/브러시/지우개 흐름으로 설명합니다.",
                WpfLearningMode.AnomalyDetection => "Anomaly: 정상/이상 샘플 차이를 보고, 검출 영역이 왜 이상인지 확인합니다.",
                WpfLearningMode.Train => "학습: 라벨과 클래스가 준비된 뒤 데이터셋과 파라미터를 점검합니다.",
                WpfLearningMode.Infer => "추론: 현재 이미지 또는 선택 이미지를 명시적으로 검사합니다.",
                WpfLearningMode.Review => "검토: 검출 후보를 보고 확정/스킵하며 정답 라벨로 바꿉니다.",
                _ => "라벨링: 사람이 정답 영역을 만들고 AI가 배울 기준을 준비합니다."
            };

            StepDetailText = SelectedStep?.Step switch
            {
                WpfLearningStep.Sample => "샘플 이미지를 불러와 기준 화면을 만듭니다.",
                WpfLearningStep.Label => "정답 라벨을 직접 만들고 클래스와 위치를 확인합니다.",
                WpfLearningStep.Infer => "검출 후보를 만든 뒤 라벨과 비교합니다.",
                WpfLearningStep.Review => "후보를 하나씩 보며 확정, 전체 확정, 스킵을 선택합니다.",
                WpfLearningStep.Save => "현재 라벨을 YOLO 학습 폴더로 저장합니다.",
                _ => string.Empty
            };

            CurrentWorkflowActionText = SelectedStep?.Step switch
            {
                WpfLearningStep.Sample => "\uB2E4\uC74C: \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC5F4\uACE0 \uCCAB \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Label => SelectedDatasetPurposeMode?.Mode == WpfLearningMode.Segmentation
                    ? "\uB2E4\uC74C: \uD3F4\uB9AC\uACE4/\uBE0C\uB7EC\uC2DC\uB85C \uB9C8\uC2A4\uD06C\uB97C \uB9CC\uB4E4\uACE0 \uC800\uC7A5\uD569\uB2C8\uB2E4."
                    : "\uB2E4\uC74C: \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uD074\uB798\uC2A4\uAC00 \uB9DE\uB294\uC9C0 \uD655\uC778\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Infer => "\uB2E4\uC74C: \uCD94\uB860\uC744 \uC2E4\uD589\uD558\uACE0 \uAC80\uCD9C \uD6C4\uBCF4\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Review => "\uB2E4\uC74C: \uAC80\uCD9C \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uC2A4\uD0B5\uD569\uB2C8\uB2E4.",
                WpfLearningStep.Save => "\uB2E4\uC74C: \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uACE0 \uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uC2E4\uD589\uD569\uB2C8\uB2E4.",
                _ => string.Empty
            };

            ToolDetailText = SelectedTool?.Tool switch
            {
                WpfAnnotationTool.Rectangle => "박스: 객체 탐지 학습에 가장 기본이 되는 영역입니다.",
                WpfAnnotationTool.Ellipse => "원/타원: 원형 부품이나 결함을 빠르게 설명하는 보조 도구입니다.",
                WpfAnnotationTool.Polygon => "폴리곤: 세그멘테이션 경계를 꼭짓점으로 만듭니다.",
                WpfAnnotationTool.Brush => "브러시: 마스크를 칠해 픽셀 단위 정답을 만듭니다.",
                WpfAnnotationTool.Eraser => "지우개: 마스크나 영역 일부를 제거합니다.",
                WpfAnnotationTool.PanZoom => "이동: 라벨을 만들기 전에 화면 위치를 빠르게 조정합니다.",
                WpfAnnotationTool.Delete => "삭제: 선택한 라벨을 제거합니다.",
                WpfAnnotationTool.Undo => "되돌리기: 직전 편집을 되돌리는 흐름입니다.",
                WpfAnnotationTool.Redo => "다시 적용: 되돌린 편집을 다시 적용하는 흐름입니다.",
                _ => "선택: 만든 라벨을 고르고 검토합니다."
            };
        }
    }

    public enum WpfLearningMode
    {
        LabelingBasics,
        ObjectDetection,
        Segmentation,
        AnomalyDetection,
        Train,
        Infer,
        Review
    }

    public enum WpfAnnotationTool
    {
        Select,
        Rectangle,
        Ellipse,
        Polygon,
        Brush,
        Eraser,
        PanZoom,
        Undo,
        Redo,
        Delete
    }

    public enum WpfLearningStep
    {
        Sample,
        Label,
        Infer,
        Review,
        Save
    }

    public sealed class WpfLearningModeItem
    {
        public WpfLearningModeItem(WpfLearningMode mode, string text, PackIconMaterialKind iconKind, string toolTip)
        {
            Mode = mode;
            Text = text ?? string.Empty;
            IconKind = iconKind;
            ToolTip = toolTip ?? string.Empty;
        }

        public WpfLearningMode Mode { get; }

        public string Text { get; }

        public PackIconMaterialKind IconKind { get; }

        public string ToolTip { get; }

        public bool IsActionEnabled => true;
    }

    public sealed class WpfAnnotationToolItem : WpfObservableViewModel
    {
        private readonly string baseToolTip;
        private bool isActionEnabled = true;
        private string displayCapabilityText = string.Empty;
        private string toolTip = string.Empty;

        public WpfAnnotationToolItem(WpfAnnotationTool tool, string text, PackIconMaterialKind iconKind, string toolTip)
        {
            WpfAnnotationToolCapability capability = WpfAnnotationToolCapabilityService.Get(tool);
            Tool = tool;
            Text = text ?? string.Empty;
            IconKind = iconKind;
            baseToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? capability.StatusText
                : $"{toolTip} / {capability.StatusText}";
            ToolTip = baseToolTip;
            IsConnected = capability.IsConnected;
            CapabilityText = capability.StateText;
            DisplayCapabilityText = CapabilityText;
            CapabilityStatusText = capability.StatusText;
        }

        public WpfAnnotationTool Tool { get; }

        public string Text { get; }

        public PackIconMaterialKind IconKind { get; }

        public string ToolTip
        {
            get => toolTip;
            private set => SetProperty(ref toolTip, value ?? string.Empty);
        }

        public bool IsConnected { get; }

        public string CapabilityText { get; }

        public string DisplayCapabilityText
        {
            get => displayCapabilityText;
            private set => SetProperty(ref displayCapabilityText, value ?? string.Empty);
        }

        public string CapabilityStatusText { get; }

        public bool IsActionEnabled
        {
            get => isActionEnabled;
            private set => SetProperty(ref isActionEnabled, value);
        }

        public void SetRuntimeAvailability(bool isEnabled, string stateText, string statusText)
        {
            IsActionEnabled = isEnabled;
            DisplayCapabilityText = string.IsNullOrWhiteSpace(stateText) ? CapabilityText : stateText;
            ToolTip = string.IsNullOrWhiteSpace(statusText) ? baseToolTip : $"{baseToolTip} / {statusText}";
        }
    }

    public sealed class WpfLearningStepItem
    {
        public WpfLearningStepItem(WpfLearningStep step, string text, PackIconMaterialKind iconKind)
        {
            Step = step;
            Text = text ?? string.Empty;
            IconKind = iconKind;
        }

        public WpfLearningStep Step { get; }

        public string Text { get; }

        public PackIconMaterialKind IconKind { get; }

        public bool IsActionEnabled => true;
    }

    public enum WpfDatasetDashboardActionKind
    {
        None,
        OpenImages,
        OpenClassCatalog,
        OpenLabelingProgress,
        OpenLabelingTool,
        CheckDataset,
        OpenDatasetSettings
    }

    public sealed class WpfDatasetDashboardMetricItem
    {
        public WpfDatasetDashboardMetricItem(
            string title,
            string value,
            string detail,
            string stateText,
            PackIconMaterialKind iconKind,
            bool isProblem,
            bool isWarning,
            WpfDatasetDashboardActionKind actionKind = WpfDatasetDashboardActionKind.None)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            StateText = stateText ?? string.Empty;
            IconKind = iconKind;
            IsProblem = isProblem;
            IsWarning = isWarning;
            ActionKind = actionKind;
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public string StateText { get; }

        public PackIconMaterialKind IconKind { get; }

        public bool IsProblem { get; }

        public bool IsWarning { get; }

        public WpfDatasetDashboardActionKind ActionKind { get; }
    }

    public sealed class WpfTrainingResultReportItem
    {
        public WpfTrainingResultReportItem(
            string title,
            string value,
            string detail,
            PackIconMaterialKind iconKind,
            bool isWarning = false)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            IconKind = iconKind;
            IsWarning = isWarning;
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public PackIconMaterialKind IconKind { get; }

        public bool IsWarning { get; }
    }

    public sealed class WpfYoloDatasetStructureItem
    {
        public WpfYoloDatasetStructureItem(string title, string value, string detail, PackIconMaterialKind iconKind)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            IconKind = iconKind;
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public PackIconMaterialKind IconKind { get; }
    }

    public sealed class WpfYoloTrainingWorkflowStepItem : WpfObservableViewModel
    {
        private string stateText = "대기";
        private bool isCompleted;
        private PackIconMaterialKind stateIconKind = PackIconMaterialKind.ClockOutline;

        public WpfYoloTrainingWorkflowStepItem(
            int order,
            string title,
            string actionText,
            string resultText,
            PackIconMaterialKind iconKind)
        {
            Order = order;
            Title = title ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            ResultText = resultText ?? string.Empty;
            IconKind = iconKind;
        }

        public int Order { get; }

        public string Title { get; }

        public string ActionText { get; }

        public string ResultText { get; }

        public PackIconMaterialKind IconKind { get; }

        public string StateText
        {
            get => stateText;
            set => SetProperty(ref stateText, value ?? string.Empty);
        }

        public bool IsCompleted
        {
            get => isCompleted;
            set => SetProperty(ref isCompleted, value);
        }

        public PackIconMaterialKind StateIconKind
        {
            get => stateIconKind;
            set => SetProperty(ref stateIconKind, value);
        }
    }
}
