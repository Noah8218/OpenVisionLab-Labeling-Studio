using MahApps.Metro.IconPacks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfLearningWorkflowPanelViewModel : WpfObservableViewModel
    {
        private WpfLearningModeItem selectedMode;
        private WpfAnnotationToolItem selectedTool;
        private WpfLearningStepItem selectedStep;
        private string modeDetailText = string.Empty;
        private string stepDetailText = string.Empty;
        private string toolDetailText = string.Empty;
        private string trainingChecklistStatusText = "\uB370\uC774\uD130\uC14B: \uC810\uAC80 \uC804";
        private string trainingChecklistDetailText = "\uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 YOLO \uD0ED\uC5D0\uC11C \uC0C8\uB85C\uACE0\uCE68\uD558\uBA74 \uD559\uC2B5 \uAC00\uB2A5 \uC5EC\uBD80\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.";
        private string trainingChecklistActionText = "\uD544\uC694\uD55C \uD56D\uBAA9\uC774 \uBCF4\uC774\uBA74 \uC544\uB798 \uD574\uACB0 \uBC84\uD2BC\uC73C\uB85C \uBC14\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.";
        private string trainingHistoryText = "\uCD5C\uADFC \uD559\uC2B5 \uC774\uB825: \uC544\uC9C1 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private bool isYoloFixClassesEnabled = true;
        private bool isYoloFixLabelsEnabled;
        private bool isYoloFixDatasetEnabled = true;
        private int brushSize = 12;
        private double maskOpacity = 0.66;

        public WpfLearningWorkflowPanelViewModel()
        {
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.LabelingBasics, "\uB77C\uBCA8\uB9C1", PackIconMaterialKind.SchoolOutline, "\uC815\uB2F5 \uB77C\uBCA8\uC744 \uADF8\uB9AC\uB294 \uD750\uB984"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.ObjectDetection, "\uAC1D\uCCB4 \uD0D0\uC9C0", PackIconMaterialKind.ShapeSquareRoundedPlus, "YOLO \uBC15\uC2A4 \uB77C\uBCA8\uACFC AI \uD6C4\uBCF4 \uAC80\uD1A0"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Segmentation, "\uC138\uADF8\uBA58\uD14C\uC774\uC158", PackIconMaterialKind.ViewListOutline, "\uD3F4\uB9AC\uACE4\uACFC \uB9C8\uC2A4\uD06C \uB77C\uBCA8"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.AnomalyDetection, "\uC774\uC0C1 \uD0D0\uC9C0", PackIconMaterialKind.AlertCircleOutline, "\uC815\uC0C1/\uC774\uC0C1 \uC0D8\uD50C\uACFC \uC601\uC5ED \uAC80\uD1A0"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Train, "\uD559\uC2B5", PackIconMaterialKind.PlayCircleOutline, "\uB370\uC774\uD130\uC14B \uC900\uBE44\uC640 \uD559\uC2B5"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Infer, "\uCD94\uB860", PackIconMaterialKind.RobotIndustrial, "\uBAA8\uB378 \uC2E4\uD589\uACFC \uC608\uCE21 \uD655\uC778"));
            LearningModes.Add(new WpfLearningModeItem(WpfLearningMode.Review, "\uAC80\uD1A0", PackIconMaterialKind.CheckAll, "\uC608\uCE21\uC744 \uD655\uC815 \uB77C\uBCA8\uB85C \uC804\uD658"));

            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Select, "\uC120\uD0DD", PackIconMaterialKind.Tune, "\uAC1D\uCCB4 \uC120\uD0DD\uACFC \uD3B8\uC9D1"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Rectangle, "\uBC15\uC2A4", PackIconMaterialKind.ShapeSquareRoundedPlus, "YOLO \uBC15\uC2A4 \uC601\uC5ED"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Ellipse, "\uC6D0/\uD0C0\uC6D0", PackIconMaterialKind.ShapeSquareRoundedPlus, "\uC6D0\uD615 \uD639\uC740 \uD0C0\uC6D0 \uC601\uC5ED"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Polygon, "\uD3F4\uB9AC\uACE4", PackIconMaterialKind.ViewListOutline, "\uB2E4\uAC01\uD615 \uC138\uADF8\uBA58\uD14C\uC774\uC158"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Brush, "\uBE0C\uB7EC\uC2DC", PackIconMaterialKind.Tune, "\uBE0C\uB7EC\uC2DC \uB9C8\uC2A4\uD06C \uD3B8\uC9D1"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Eraser, "\uC9C0\uC6B0\uAC1C", PackIconMaterialKind.TrashCanOutline, "\uB9C8\uC2A4\uD06C\uB098 \uC601\uC5ED \uC9C0\uC6B0\uAE30"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.PanZoom, "\uC774\uB3D9", PackIconMaterialKind.Magnify, "\uD654\uBA74 \uC774\uB3D9\uACFC \uD655\uB300"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Undo, "Undo", PackIconMaterialKind.Refresh, "\uB9C8\uC9C0\uB9C9 \uD3B8\uC9D1 \uB418\uB3CC\uB9AC\uAE30"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Redo, "Redo", PackIconMaterialKind.Reload, "\uB418\uB3CC\uB9B0 \uD3B8\uC9D1 \uB2E4\uC2DC \uC801\uC6A9"));
            AnnotationTools.Add(new WpfAnnotationToolItem(WpfAnnotationTool.Delete, "\uC0AD\uC81C", PackIconMaterialKind.TrashCanOutline, "\uC120\uD0DD \uB77C\uBCA8 \uC0AD\uC81C"));

            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Sample, "\uC0D8\uD50C", PackIconMaterialKind.FolderImage));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Label, "\uB77C\uBCA8", PackIconMaterialKind.ShapeSquareRoundedPlus));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Infer, "\uCD94\uB860", PackIconMaterialKind.RobotIndustrial));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Review, "\uB9AC\uBDF0", PackIconMaterialKind.CheckAll));
            LearningSteps.Add(new WpfLearningStepItem(WpfLearningStep.Save, "\uC800\uC7A5", PackIconMaterialKind.ContentSaveOutline));

            TutorialChecklistItems.Add("\uC0D8\uD50C \uB610\uB294 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC5F4\uACE0 \uC88C\uCE21 \uD050\uC5D0\uC11C \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD0ED\uC5D0\uC11C OK, NG\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uB4F1\uB85D\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uB77C\uBCA8\uB9C1 \uBAA8\uB4DC\uC5D0\uC11C \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uC800\uC7A5\uD558\uC5EC YOLO txt\uB97C \uB9CC\uB4ED\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uB370\uC774\uD130\uC14B \uC810\uAC80\uC73C\uB85C \uB77C\uBCA8, \uD074\uB798\uC2A4, data.yaml\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("YOLOv5 \uD559\uC2B5\uC744 \uC2E4\uD589\uD558\uACE0 \uC644\uB8CC \uD6C4 best.pt\uB97C \uC801\uC6A9\uD569\uB2C8\uB2E4.");
            TutorialChecklistItems.Add("\uD604\uC7AC \uAC80\uC0AC\uB85C AI \uD6C4\uBCF4\uB97C \uD655\uC778\uD558\uACE0 \uD655\uC815 \uB610\uB294 \uC2A4\uD0B5\uD569\uB2C8\uB2E4.");

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
                "\uB77C\uBCA8\uC774 \uBE60\uC9C4 \uC774\uBBF8\uC9C0, \uD074\uB798\uC2A4, data.yaml\uC744 \uAC80\uC0AC\uD569\uB2C8\uB2E4.",
                "YOLO \uD0ED\uC758 \uC0C8\uB85C\uACE0\uCE68\uC5D0\uC11C \uD559\uC2B5 \uAC00\uB2A5 \uC0C1\uD0DC\uAC00 \uB098\uC640\uC57C \uD569\uB2C8\uB2E4.",
                PackIconMaterialKind.CheckAll));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                5,
                "YOLOv5 \uD559\uC2B5",
                "\uC774\uBBF8\uC9C0 \uD06C\uAE30, \uBC30\uCE58, \uC5D0\uD3ED, \uAC00\uC911\uCE58\uB97C \uD655\uC778\uD558\uACE0 \uD559\uC2B5\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4.",
                "\uC9C4\uD589\uB960\uACFC \uC5D0\uD3ED\uC744 \uBCF4\uACE0, \uC644\uB8CC \uD6C4 best.pt\uB97C \uCC3E\uC2B5\uB2C8\uB2E4.",
                PackIconMaterialKind.PlayCircleOutline));
            YoloTrainingWorkflowSteps.Add(new WpfYoloTrainingWorkflowStepItem(
                6,
                "\uD559\uC2B5 \uACB0\uACFC \uCD94\uB860 \uAC80\uD1A0",
                "\uC0C8\uB85C \uB9CC\uB4E0 best.pt\uB85C \uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uACE0 \uD6C4\uBCF4\uB97C \uD655\uC815\uD569\uB2C8\uB2E4.",
                "\uACB0\uACFC\uAC00 \uB9DE\uC73C\uBA74 \uB77C\uBCA8\uB85C \uD655\uC815\uD558\uACE0, \uD2C0\uB9AC\uBA74 \uB370\uC774\uD130\uB97C \uCD94\uAC00\uD569\uB2C8\uB2E4.",
                PackIconMaterialKind.RobotIndustrial));

            SelectedMode = LearningModes.FirstOrDefault();
            SelectedTool = AnnotationTools.FirstOrDefault();
            SelectedStep = LearningSteps.FirstOrDefault();
            SetAnnotationHistoryState(canUndo: false, canRedo: false, undoActionName: string.Empty, redoActionName: string.Empty);
        }

        public string ViewName => nameof(WpfLearningWorkflowPanel);

        public ObservableCollection<WpfLearningModeItem> LearningModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfAnnotationToolItem> AnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfLearningStepItem> LearningSteps { get; } = new ObservableCollection<WpfLearningStepItem>();

        public ObservableCollection<string> TutorialChecklistItems { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfYoloTrainingWorkflowStepItem> YoloTrainingWorkflowSteps { get; } = new ObservableCollection<WpfYoloTrainingWorkflowStepItem>();

        public ObservableCollection<string> TrainingRunHistoryItems { get; } = new ObservableCollection<string>();

        public string TutorialTitleText => "\uCC98\uC74C 10\uBD84 \uD29C\uD1A0\uB9AC\uC5BC";

        public string TutorialSummaryText => "\uAE43\uD5C8\uBE0C\uC5D0\uC11C \uBC1B\uC740 \uD6C4 \uC0D8\uD50C \uC774\uBBF8\uC9C0, \uB77C\uBCA8, \uD559\uC2B5, \uCD94\uB860 \uAC80\uD1A0\uAE4C\uC9C0 \uC544\uB798 \uC21C\uC11C\uB300\uB85C \uB530\uB77C\uD569\uB2C8\uB2E4.";

        public string TutorialHtmlPathText => "HTML: docs/tutorial/labeling-workbench-tutorial.html";

        public string TrainingWorkflowSummaryText => "\uC774\uBBF8\uC9C0 \u2192 \uD074\uB798\uC2A4 \u2192 \uB77C\uBCA8 \u2192 \uC810\uAC80 \u2192 \uD559\uC2B5 \u2192 \uCD94\uB860/\uAC80\uD1A0";

        public string GroundTruthChipText => "\uC815\uB2F5 \uB77C\uBCA8";

        public string PredictionChipText => "AI \uCD94\uB860";

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

        public string TrainingHistoryText
        {
            get => trainingHistoryText;
            set => SetProperty(ref trainingHistoryText, value ?? string.Empty);
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
            ModeDetailText = SelectedMode?.Mode switch
            {
                WpfLearningMode.ObjectDetection => "YOLO: 이미지 안의 객체 위치를 박스로 찾고, 후보를 정답 라벨로 확정합니다.",
                WpfLearningMode.Segmentation => "U-Net: 픽셀 단위 마스크를 만들며, 폴리곤/브러시/지우개 흐름으로 설명합니다.",
                WpfLearningMode.AnomalyDetection => "Anomaly: 정상/이상 샘플 차이를 보고, 검출 영역이 왜 이상인지 확인합니다.",
                WpfLearningMode.Train => "학습: 라벨과 클래스가 준비된 뒤 데이터셋과 파라미터를 점검합니다.",
                WpfLearningMode.Infer => "추론: 현재 이미지 또는 선택 이미지를 명시적으로 검사합니다.",
                WpfLearningMode.Review => "검토: AI 후보를 보고 확정/스킵하며 정답 라벨로 바꿉니다.",
                _ => "라벨링: 사람이 정답 영역을 만들고 AI가 배울 기준을 준비합니다."
            };

            StepDetailText = SelectedStep?.Step switch
            {
                WpfLearningStep.Sample => "샘플 이미지를 불러와 기준 화면을 만듭니다.",
                WpfLearningStep.Label => "정답 라벨을 직접 만들고 클래스와 위치를 확인합니다.",
                WpfLearningStep.Infer => "AI 후보를 만든 뒤 라벨과 비교합니다.",
                WpfLearningStep.Review => "후보를 하나씩 보며 확정, 전체 확정, 스킵을 선택합니다.",
                WpfLearningStep.Save => "현재 라벨을 YOLO 학습 폴더로 저장합니다.",
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
                WpfAnnotationTool.Undo => "Undo: 직전 편집을 되돌리는 흐름입니다.",
                WpfAnnotationTool.Redo => "Redo: 되돌린 편집을 다시 적용하는 흐름입니다.",
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
