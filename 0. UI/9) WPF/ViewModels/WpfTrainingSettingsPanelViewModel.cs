using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.Mvvm;
using MvcVisionSystem.Yolo;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public sealed class WpfTrainingSettingsPanelViewModel : WpfObservableViewModel
    {
        private string imageSizeText = string.Empty;
        private string batchText = string.Empty;
        private string epochText = string.Empty;
        private string cfg = string.Empty;
        private string weight = string.Empty;
        private string validationPercentText = string.Empty;
        private string testPercentText = string.Empty;
        private string splitSeedText = string.Empty;
        private string modelEngine = PythonModelSettings.EngineYoloV5;
        private LabelingDatasetPurpose datasetPurpose = LabelingDatasetPurpose.ObjectDetection;
        private string trainingReadinessText = "학습 상태 미확인";
        private string trainingProgressText = "학습 대기";
        private string trainingEpochStatusText = string.Empty;
        private double trainingProgressValue;
        private bool trainingProgressIsIndeterminate;
        private MediaBrush trainingReadinessForeground = MediaBrushes.Gray;
        private MediaBrush trainingProgressForeground = MediaBrushes.Gray;
        private bool isRefreshReadinessEnabled = true;
        private bool isStartTrainingEnabled = true;
        private string startTrainingToolTip = "\uD604\uC7AC \uC124\uC815\uC73C\uB85C \uD559\uC2B5\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4.";
        private bool isStopTrainingEnabled;
        private bool isApplyFastRecommendationEnabled = true;
        private bool isApplyFinalVerificationPresetEnabled = true;
        private bool isReviewTrainedModelEnabled;
        private bool isConfirmTrainedModelEnabled;
        private bool isRunYoloEngineComparisonEnabled = true;
        private bool canRunPostTrainingReviewCommands = true;
        private bool canRunPostTrainingConfirmCommands = true;
        private bool isReviewTrainedModelAvailable;
        private bool isConfirmTrainedModelAvailable;
        private string postTrainingModelStatusText = "\uD559\uC2B5 \uC644\uB8CC \uD6C4 \uC791\uC5C5: \uD559\uC2B5 \uACB0\uACFC \uD6C4\uBCF4 \uC5C6\uC74C";
        private string postTrainingModelDetailText = "\uD559\uC2B5\uC774 \uB05D\uB098\uBA74 \uD6C4\uBCF4 \uAC80\uC99D \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD574\uC57C \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.";
        private string reviewTrainedModelActionText = "\uD6C4\uBCF4 \uC5C6\uC74C";
        private string reviewTrainedModelToolTip = "\uAC80\uC99D\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string confirmTrainedModelActionText = "\uD6C4\uBCF4 \uC5C6\uC74C";
        private string confirmTrainedModelToolTip = "\uC800\uC7A5\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private ICommand applyFastRecommendationCommand;
        private ICommand applyFinalVerificationPresetCommand;
        private ICommand refreshReadinessCommand = new RelayCommand(NoOpCommand);
        private ICommand startTrainingCommand = new RelayCommand(NoOpCommand);
        private ICommand stopTrainingCommand = new RelayCommand(NoOpCommand);
        private ICommand reviewTrainedModelCommand = new RelayCommand(NoOpCommand);
        private ICommand confirmTrainedModelCommand = new RelayCommand(NoOpCommand);
        private ICommand runYoloEngineComparisonCommand = new RelayCommand(NoOpCommand);

        public WpfTrainingSettingsPanelViewModel()
        {
            ApplyFastRecommendationCommand = new RelayCommand(ApplyFastRecommendation);
            ApplyFinalVerificationPresetCommand = new RelayCommand(ApplyFinalVerificationPreset);
        }

        public string ViewName => nameof(WpfTrainingSettingsPanel);

        public string TrainingRecommendationTitleText => "추천 시작값";

        public string TrainingRecommendationText =>
            $"빠른 첫 학습은 이미지 320, 배치 4, 에폭 50, 모델 {TrainingModelDisplayText}, 검증 20%, 최종 검증 0%로 시작합니다. CPU에서는 먼저 성공 여부를 확인하고, GPU가 있거나 여유가 있으면 배치를 8/16으로 올립니다.";

        public string FinalVerificationPresetActionText => "\uCD5C\uC885 \uAC80\uC99D \uC900\uBE44";

        public string FinalVerificationPresetToolTipText =>
            "\uC0C8\uB85C \uC800\uC7A5\uD558\uB294 \uC774\uBBF8\uC9C0\uB97C \uCD5C\uC885 \uAC80\uC99D(test)\uC5D0\uB9CC \uB123\uB3C4\uB85D \uAC80\uC99D 0%, \uCD5C\uC885 \uAC80\uC99D 100%\uB97C \uC124\uC815\uD569\uB2C8\uB2E4. \uAE30\uC874 \uB370\uC774\uD130\uB294 \uBC14\uB85C \uBCC0\uACBD\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";

        public string ImageSizeGuideText =>
            "추천 320: 빠르게 되는지 확인합니다. 작은 결함이나 얇은 부품은 640을 사용합니다.";

        public string BatchGuideText =>
            "추천 4: CPU에서도 먼저 성공 여부를 확인하기 위한 안전값입니다. GPU가 있거나 여유가 있으면 8 또는 16으로 올립니다.";

        public string EpochGuideText =>
            "추천 50: 전체 데이터를 반복 학습하는 횟수입니다. 결과가 흔들리면 100까지 늘립니다.";

        public string CfgGuideText => IsLegacyYoloV5TrainingSelection
            ? "추천 yolov5s: 빠른 기준 모델입니다. 정확도가 우선이면 m/l/x로 올리며 학습 시간은 늘어납니다."
            : $"현재 모델 엔진과 데이터셋 용도에 맞춰 {TrainingModelDisplayText}을 사용합니다.";

        public string WeightGuideText =>
            "학습 시작 가중치입니다. 공개 가중치에서 시작하면 적은 데이터에서도 더 안정적으로 학습됩니다.";

        public string ValidationPercentGuideText =>
            "추천 20%: 학습 중 성능 확인용 이미지 비율입니다. 데이터가 매우 적으면 10%도 가능합니다.";

        public string TestPercentGuideText =>
            "추천 0~10%: 학습 후 모델 비교용입니다. 데이터가 적으면 0%로 두고 검증 데이터를 우선 확보합니다.";

        public string SplitSeedGuideText =>
            "추천 17: 이미지 분할을 재현하기 위한 기준값입니다. 같은 값이면 같은 방식으로 train/valid가 나뉩니다.";

        public string SplitPolicyHintText =>
            "검증 %는 학습 중 확인용, 최종 검증 %는 학습 후 모델 비교용입니다. 두 값을 합쳐 100% 이하로 설정하세요.";

        public string TrainingSettingsSummaryTitleText => "\uD604\uC7AC \uD559\uC2B5 \uC124\uC815";

        public bool IsLegacyYoloV5TrainingSelection
            => string.Equals(modelEngine, PythonModelSettings.EngineYoloV5, StringComparison.Ordinal);

        public bool IsTrainingModelSelectionEnabled => IsLegacyYoloV5TrainingSelection;

        public IReadOnlyList<string> TrainingModelOptions
            => IsLegacyYoloV5TrainingSelection
                ? Enum.GetNames(typeof(CYolov5TrainingParam.Cfg))
                : new[] { TrainingModelDisplayText };

        public IReadOnlyList<string> TrainingWeightOptions
            => IsLegacyYoloV5TrainingSelection
                ? Enum.GetNames(typeof(CYolov5TrainingParam.Weight))
                : new[] { TrainingStartWeightDisplayText };

        public string SelectedTrainingModel
        {
            get => IsLegacyYoloV5TrainingSelection ? Cfg : TrainingModelDisplayText;
            set
            {
                if (IsLegacyYoloV5TrainingSelection)
                {
                    Cfg = value;
                }
            }
        }

        public string SelectedTrainingWeight
        {
            get => IsLegacyYoloV5TrainingSelection ? Weight : TrainingStartWeightDisplayText;
            set
            {
                if (IsLegacyYoloV5TrainingSelection)
                {
                    Weight = value;
                }
            }
        }

        public string TrainingModelDisplayText
            => modelEngine switch
            {
                PythonModelSettings.EngineYoloV8 => datasetPurpose == LabelingDatasetPurpose.Segmentation ? "YOLOv8 SEG" : "YOLOv8 Detect",
                PythonModelSettings.EngineYolo11 => datasetPurpose == LabelingDatasetPurpose.Segmentation ? "YOLO11 SEG" : "YOLO11 Detect",
                PythonModelSettings.EngineOnnx => "ONNX (\uCD94\uB860 \uC804\uC6A9)",
                _ => string.IsNullOrWhiteSpace(Cfg) ? "-" : Cfg
            };

        public string TrainingStartWeightDisplayText
            => modelEngine switch
            {
                PythonModelSettings.EngineYoloV8 => datasetPurpose == LabelingDatasetPurpose.Segmentation ? "yolov8n-seg.pt" : "yolov8n.pt",
                PythonModelSettings.EngineYolo11 => datasetPurpose == LabelingDatasetPurpose.Segmentation ? "yolo11n-seg.pt" : "yolo11n.pt",
                PythonModelSettings.EngineOnnx => "\uD559\uC2B5 \uAC00\uC911\uCE58 \uC5C6\uC74C",
                _ => string.IsNullOrWhiteSpace(Weight) ? "-" : Weight
            };

        public string TrainingSettingsSummaryModelText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uBAA8\uB378: {0} / \uC2DC\uC791 \uAC00\uC911\uCE58: {1}",
                TrainingModelDisplayText,
                TrainingStartWeightDisplayText);

        public string TrainingSettingsSummaryRuntimeText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uC774\uBBF8\uC9C0 {0} / \uBC30\uCE58 {1} / \uC5D0\uD3ED {2}",
                string.IsNullOrWhiteSpace(ImageSizeText) ? "-" : ImageSizeText,
                string.IsNullOrWhiteSpace(BatchText) ? "-" : BatchText,
                string.IsNullOrWhiteSpace(EpochText) ? "-" : EpochText);

        public string TrainingSettingsSummarySplitText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uAC80\uC99D {0}% / \uCD5C\uC885 \uAC80\uC99D {1}% / \uBD84\uD560 \uAE30\uC900 {2}",
                string.IsNullOrWhiteSpace(ValidationPercentText) ? "-" : ValidationPercentText,
                string.IsNullOrWhiteSpace(TestPercentText) ? "-" : TestPercentText,
                string.IsNullOrWhiteSpace(SplitSeedText) ? "-" : SplitSeedText);

        public string TrainingSettingsSummaryActionText
            => "\uCC98\uC74C\uC5D0\uB294 \uBE60\uB978 \uCD94\uCC9C\uAC12\uC744 \uC801\uC6A9\uD55C \uB4A4 \uC0C8\uB85C\uACE0\uCE68\uC73C\uB85C \uB370\uC774\uD130\uC14B\uC744 \uC810\uAC80\uD558\uACE0, \uC900\uBE44\uAC00 \uB418\uBA74 \uC2DC\uC791\uD558\uC138\uC694.";

        public string PostTrainingModelActionTitleText => "\uD559\uC2B5 \uC644\uB8CC \uD6C4 \uC791\uC5C5";

        public string AdvancedTrainingSettingsHeaderText
            => "\uACE0\uAE09 \uD559\uC2B5 \uD30C\uB77C\uBBF8\uD130";

        public ICommand ApplyFastRecommendationCommand
        {
            get => applyFastRecommendationCommand;
            private set => SetProperty(ref applyFastRecommendationCommand, value);
        }

        public ICommand ApplyFinalVerificationPresetCommand
        {
            get => applyFinalVerificationPresetCommand;
            private set => SetProperty(ref applyFinalVerificationPresetCommand, value);
        }

        public ICommand RefreshReadinessCommand
        {
            get => refreshReadinessCommand;
            private set => SetProperty(ref refreshReadinessCommand, value);
        }

        public ICommand StartTrainingCommand
        {
            get => startTrainingCommand;
            private set => SetProperty(ref startTrainingCommand, value);
        }

        public ICommand StopTrainingCommand
        {
            get => stopTrainingCommand;
            private set => SetProperty(ref stopTrainingCommand, value);
        }

        public ICommand ReviewTrainedModelCommand
        {
            get => reviewTrainedModelCommand;
            private set => SetProperty(ref reviewTrainedModelCommand, value);
        }

        public ICommand ConfirmTrainedModelCommand
        {
            get => confirmTrainedModelCommand;
            private set => SetProperty(ref confirmTrainedModelCommand, value);
        }

        public ICommand RunYoloEngineComparisonCommand
        {
            get => runYoloEngineComparisonCommand;
            private set => SetProperty(ref runYoloEngineComparisonCommand, value);
        }

        public string ImageSizeText
        {
            get => imageSizeText;
            set
            {
                if (SetProperty(ref imageSizeText, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string BatchText
        {
            get => batchText;
            set
            {
                if (SetProperty(ref batchText, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string EpochText
        {
            get => epochText;
            set
            {
                if (SetProperty(ref epochText, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string Cfg
        {
            get => cfg;
            set
            {
                if (SetProperty(ref cfg, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string Weight
        {
            get => weight;
            set
            {
                if (SetProperty(ref weight, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string ValidationPercentText
        {
            get => validationPercentText;
            set
            {
                if (SetProperty(ref validationPercentText, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string TestPercentText
        {
            get => testPercentText;
            set
            {
                if (SetProperty(ref testPercentText, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string SplitSeedText
        {
            get => splitSeedText;
            set
            {
                if (SetProperty(ref splitSeedText, value ?? string.Empty))
                {
                    NotifyTrainingSettingsSummaryChanged();
                }
            }
        }

        public string TrainingReadinessText
        {
            get => trainingReadinessText;
            private set => SetProperty(ref trainingReadinessText, value ?? string.Empty);
        }

        public string TrainingProgressText
        {
            get => trainingProgressText;
            private set => SetProperty(ref trainingProgressText, value ?? string.Empty);
        }

        public string TrainingEpochStatusText
        {
            get => trainingEpochStatusText;
            private set => SetProperty(ref trainingEpochStatusText, value ?? string.Empty);
        }

        public double TrainingProgressValue
        {
            get => trainingProgressValue;
            private set => SetProperty(ref trainingProgressValue, Math.Clamp(value, 0D, 100D));
        }

        public bool TrainingProgressIsIndeterminate
        {
            get => trainingProgressIsIndeterminate;
            private set => SetProperty(ref trainingProgressIsIndeterminate, value);
        }

        public MediaBrush TrainingReadinessForeground
        {
            get => trainingReadinessForeground;
            private set => SetProperty(ref trainingReadinessForeground, value ?? MediaBrushes.Gray);
        }

        public MediaBrush TrainingProgressForeground
        {
            get => trainingProgressForeground;
            private set => SetProperty(ref trainingProgressForeground, value ?? MediaBrushes.Gray);
        }

        public bool IsRefreshReadinessEnabled
        {
            get => isRefreshReadinessEnabled;
            private set => SetProperty(ref isRefreshReadinessEnabled, value);
        }

        public bool IsStartTrainingEnabled
        {
            get => isStartTrainingEnabled;
            private set => SetProperty(ref isStartTrainingEnabled, value);
        }

        public string StartTrainingToolTip
        {
            get => startTrainingToolTip;
            private set => SetProperty(ref startTrainingToolTip, string.IsNullOrWhiteSpace(value) ? "\uD604\uC7AC \uC124\uC815\uC73C\uB85C \uD559\uC2B5\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4." : value);
        }

        public bool IsStopTrainingEnabled
        {
            get => isStopTrainingEnabled;
            private set => SetProperty(ref isStopTrainingEnabled, value);
        }

        public bool IsApplyFastRecommendationEnabled
        {
            get => isApplyFastRecommendationEnabled;
            private set => SetProperty(ref isApplyFastRecommendationEnabled, value);
        }

        public bool IsApplyFinalVerificationPresetEnabled
        {
            get => isApplyFinalVerificationPresetEnabled;
            private set => SetProperty(ref isApplyFinalVerificationPresetEnabled, value);
        }

        public bool IsReviewTrainedModelEnabled
        {
            get => isReviewTrainedModelEnabled;
            private set => SetProperty(ref isReviewTrainedModelEnabled, value);
        }

        public bool IsConfirmTrainedModelEnabled
        {
            get => isConfirmTrainedModelEnabled;
            private set => SetProperty(ref isConfirmTrainedModelEnabled, value);
        }

        public bool IsRunYoloEngineComparisonEnabled
        {
            get => isRunYoloEngineComparisonEnabled;
            private set => SetProperty(ref isRunYoloEngineComparisonEnabled, value);
        }

        public string PostTrainingModelStatusText
        {
            get => postTrainingModelStatusText;
            private set => SetProperty(ref postTrainingModelStatusText, string.IsNullOrWhiteSpace(value) ? "\uD559\uC2B5 \uC644\uB8CC \uD6C4 \uC791\uC5C5: \uD559\uC2B5 \uACB0\uACFC \uD6C4\uBCF4 \uC5C6\uC74C" : value);
        }

        public string PostTrainingModelDetailText
        {
            get => postTrainingModelDetailText;
            private set => SetProperty(ref postTrainingModelDetailText, string.IsNullOrWhiteSpace(value) ? "\uD559\uC2B5\uC774 \uB05D\uB098\uBA74 \uD6C4\uBCF4 \uAC80\uC99D \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD574\uC57C \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uC0AC\uC6A9\uD569\uB2C8\uB2E4." : value);
        }

        public string ReviewTrainedModelActionText
        {
            get => reviewTrainedModelActionText;
            private set => SetProperty(ref reviewTrainedModelActionText, string.IsNullOrWhiteSpace(value) ? "\uD6C4\uBCF4 \uC5C6\uC74C" : value);
        }

        public string ReviewTrainedModelToolTip
        {
            get => reviewTrainedModelToolTip;
            private set => SetProperty(ref reviewTrainedModelToolTip, string.IsNullOrWhiteSpace(value) ? "\uAC80\uC99D\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4." : value);
        }

        public string ConfirmTrainedModelActionText
        {
            get => confirmTrainedModelActionText;
            private set => SetProperty(ref confirmTrainedModelActionText, string.IsNullOrWhiteSpace(value) ? "\uD6C4\uBCF4 \uC5C6\uC74C" : value);
        }

        public string ConfirmTrainedModelToolTip
        {
            get => confirmTrainedModelToolTip;
            private set => SetProperty(ref confirmTrainedModelToolTip, string.IsNullOrWhiteSpace(value) ? "\uC800\uC7A5\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4." : value);
        }

        public string RunYoloEngineComparisonActionText => "v5 vs v8 \uBD84\uC11D";

        public string RunYoloEngineComparisonToolTipText =>
            "\uB3D9\uC77C\uD55C \uAC1D\uCCB4\uD0D0\uC9C0 \uB370\uC774\uD130\uC5D0\uC11C YOLOv5\uC640 YOLOv8\uC758 \uC815\uD655\uB3C4\uC640 \uBAA8\uB378 Takt\uB97C \uBE44\uAD50\uD569\uB2C8\uB2E4. test\uAC00 \uC5C6\uC73C\uBA74 val\uC744 \uC131\uB2A5 \uCC38\uACE0\uC6A9\uC73C\uB85C \uC0AC\uC6A9\uD558\uBA70 \uAD50\uCCB4 \uADFC\uAC70\uB85C \uC0AC\uC6A9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";

        public void ConfigureCommands(
            Action refreshReadiness,
            Action startTraining,
            Action stopTraining,
            Action reviewTrainedModel = null,
            Action confirmTrainedModel = null,
            Action runYoloEngineComparison = null)
        {
            // Training commands stay injected so long-running workflow logic remains outside the view.
            RefreshReadinessCommand = new RelayCommand(refreshReadiness ?? NoOpCommand);
            StartTrainingCommand = new RelayCommand(startTraining ?? NoOpCommand);
            StopTrainingCommand = new RelayCommand(stopTraining ?? NoOpCommand);
            ReviewTrainedModelCommand = new RelayCommand(reviewTrainedModel ?? NoOpCommand);
            ConfirmTrainedModelCommand = new RelayCommand(confirmTrainedModel ?? NoOpCommand);
            RunYoloEngineComparisonCommand = new RelayCommand(runYoloEngineComparison ?? NoOpCommand);
        }

        public void LoadFrom(
            TrainingSettings training,
            YoloDatasetSettings dataset,
            PythonModelSettings modelSettings = null,
            LabelingDatasetPurpose purpose = LabelingDatasetPurpose.ObjectDetection)
        {
            if (training == null || dataset == null)
            {
                return;
            }

            modelEngine = PythonModelSettings.NormalizeModelEngine(modelSettings?.ModelEngine);
            datasetPurpose = purpose;
            ImageSizeText = training.ImageSize.ToString(CultureInfo.InvariantCulture);
            BatchText = training.Batch.ToString(CultureInfo.InvariantCulture);
            EpochText = training.Epoch.ToString(CultureInfo.InvariantCulture);
            Cfg = training.Cfg ?? string.Empty;
            Weight = training.Weight ?? string.Empty;
            ValidationPercentText = dataset.ValidationPercent.ToString(CultureInfo.InvariantCulture);
            TestPercentText = dataset.TestPercent.ToString(CultureInfo.InvariantCulture);
            SplitSeedText = dataset.SplitSeed.ToString(CultureInfo.InvariantCulture);
            NotifyTrainingModelSelectionChanged();
        }

        private void ApplyFastRecommendation()
        {
            // Keep this preset intentionally conservative: it prioritizes quick feedback before
            // the operator spends time on larger images or heavier model structures.
            ImageSizeText = "320";
            BatchText = "4";
            EpochText = "50";
            if (IsLegacyYoloV5TrainingSelection)
            {
                Cfg = "yolov5s";
                Weight = "yolov5s";
            }
            ValidationPercentText = "20";
            TestPercentText = "0";
            SplitSeedText = "17";
            TrainingReadinessText = "빠른 추천값을 적용했습니다. 새로고침으로 데이터셋 상태를 확인한 뒤 시작하세요.";
        }

        private void ApplyFinalVerificationPreset()
        {
            ValidationPercentText = "0";
            TestPercentText = "100";
            TrainingReadinessText = "\uCD5C\uC885 \uAC80\uC99D \uC900\uBE44: \uC0C8\uB85C \uC800\uC7A5\uD558\uB294 \uC774\uBBF8\uC9C0\uB294 test\uC5D0\uB9CC \uC800\uC7A5\uB429\uB2C8\uB2E4. \uB3C5\uB9BD \uC774\uBBF8\uC9C0\uB97C \uBD88\uB7EC\uC640 \uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 \uBE60\uB978 \uCD94\uCC9C \uC801\uC6A9\uC73C\uB85C \uD559\uC2B5 \uBD84\uD560\uC5D0 \uB3CC\uC544\uC624\uC138\uC694.";
        }

        public void ApplyTo(TrainingSettings training, YoloDatasetSettings dataset, CYolov5TrainingParam trainingParam)
        {
            if (training == null)
            {
                throw new ArgumentNullException(nameof(training));
            }

            if (dataset == null)
            {
                throw new ArgumentNullException(nameof(dataset));
            }

            if (!int.TryParse(ImageSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int imageSize)
                || !int.TryParse(BatchText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int batch)
                || !int.TryParse(EpochText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int epoch)
                || !int.TryParse(ValidationPercentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int validationPercent)
                || !int.TryParse(TestPercentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int testPercent)
                || !int.TryParse(SplitSeedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int splitSeed))
            {
                throw new FormatException("학습 설정 값은 정수여야 합니다.");
            }

            training.ImageSize = Math.Max(1, imageSize);
            training.Batch = Math.Max(1, batch);
            training.Epoch = Math.Max(1, epoch);
            training.Cfg = string.IsNullOrWhiteSpace(Cfg) ? training.Cfg : Cfg;
            training.Weight = string.IsNullOrWhiteSpace(Weight) ? training.Weight : Weight;
            training.ApplyTo(trainingParam);
            dataset.ValidationPercent = Math.Clamp(validationPercent, 0, 100);
            dataset.TestPercent = Math.Clamp(testPercent, 0, 100);
            dataset.SplitSeed = splitSeed;
        }

        public void SetTrainingReadinessText(string text)
        {
            TrainingReadinessText = text;
        }

        public void SetTrainingProgress(string progressText, string epochText, double progressValue, bool isIndeterminate)
        {
            TrainingProgressText = progressText;
            TrainingEpochStatusText = epochText;
            TrainingProgressValue = progressValue;
            TrainingProgressIsIndeterminate = isIndeterminate;
        }

        public void SetTrainingProgressValue(double value)
        {
            TrainingProgressValue = value;
        }

        public void SetPostTrainingModelActionState(
            string currentModelText,
            string candidateModelText,
            string adoptionText,
            string nextActionText,
            string reviewActionText,
            string reviewToolTip,
            bool canReview,
            string confirmActionText,
            string confirmToolTip,
            bool canConfirm)
        {
            PostTrainingModelStatusText = string.IsNullOrWhiteSpace(adoptionText)
                ? "\uD559\uC2B5 \uC644\uB8CC \uD6C4 \uC791\uC5C5: \uD559\uC2B5 \uACB0\uACFC \uBE44\uAD50 \uC804"
                : StripPostTrainingPrefix(adoptionText, "\uBAA8\uB378 \uC801\uC6A9:");
            PostTrainingModelDetailText = BuildPostTrainingModelDetail(currentModelText, candidateModelText, nextActionText);
            ReviewTrainedModelActionText = reviewActionText;
            ReviewTrainedModelToolTip = reviewToolTip;
            ConfirmTrainedModelActionText = confirmActionText;
            ConfirmTrainedModelToolTip = confirmToolTip;
            isReviewTrainedModelAvailable = canReview;
            isConfirmTrainedModelAvailable = canConfirm;
            RefreshPostTrainingActionAvailability();
        }

        public void SetTrainingProgressBusy(bool isBusy)
        {
            TrainingProgressIsIndeterminate = isBusy;
        }

        public void SetTrainingStatusBrushes(MediaBrush readinessBrush, MediaBrush progressBrush)
        {
            TrainingReadinessForeground = readinessBrush;
            TrainingProgressForeground = progressBrush;
        }

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            IsApplyFastRecommendationEnabled = canRunGeneralCommands;
            IsApplyFinalVerificationPresetEnabled = canRunGeneralCommands;
            IsRefreshReadinessEnabled = canRunGeneralCommands;
            IsStartTrainingEnabled = state?.CanStartTraining == true;
            StartTrainingToolTip = state?.StartTrainingToolTip;
            IsStopTrainingEnabled = state?.CanStopTraining == true;
            IsRunYoloEngineComparisonEnabled = canRunGeneralCommands;
            canRunPostTrainingReviewCommands = canRunGeneralCommands;
            canRunPostTrainingConfirmCommands = state?.CanSaveProjectConfig == true;
            RefreshPostTrainingActionAvailability();
        }

        private void RefreshPostTrainingActionAvailability()
        {
            IsReviewTrainedModelEnabled = isReviewTrainedModelAvailable && canRunPostTrainingReviewCommands;
            IsConfirmTrainedModelEnabled = isConfirmTrainedModelAvailable && canRunPostTrainingConfirmCommands;
        }

        private static string BuildPostTrainingModelDetail(string currentModelText, string candidateModelText, string nextActionText)
        {
            string current = StripPostTrainingPrefix(
                currentModelText,
                "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378:",
                "\uAC80\uC0AC \uBAA8\uB378 \uD6C4\uBCF4:");
            string candidate = StripPostTrainingPrefix(
                candidateModelText,
                "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4:");
            string next = StripPostTrainingPrefix(nextActionText, "\uB2E4\uC74C:");
            return $"\uAC80\uC0AC \uBAA8\uB378 {current} / \uD559\uC2B5 \uD6C4\uBCF4 {candidate} / \uB2E4\uC74C {next}";
        }

        private static string StripPostTrainingPrefix(string text, params string[] prefixes)
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

        private static void NoOpCommand()
        {
        }

        private void NotifyTrainingSettingsSummaryChanged()
        {
            OnPropertyChanged(nameof(TrainingSettingsSummaryModelText));
            OnPropertyChanged(nameof(TrainingSettingsSummaryRuntimeText));
            OnPropertyChanged(nameof(TrainingSettingsSummarySplitText));
        }

        private void NotifyTrainingModelSelectionChanged()
        {
            OnPropertyChanged(nameof(IsLegacyYoloV5TrainingSelection));
            OnPropertyChanged(nameof(IsTrainingModelSelectionEnabled));
            OnPropertyChanged(nameof(TrainingModelOptions));
            OnPropertyChanged(nameof(TrainingWeightOptions));
            OnPropertyChanged(nameof(SelectedTrainingModel));
            OnPropertyChanged(nameof(SelectedTrainingWeight));
            OnPropertyChanged(nameof(TrainingModelDisplayText));
            OnPropertyChanged(nameof(TrainingStartWeightDisplayText));
            OnPropertyChanged(nameof(TrainingRecommendationText));
            OnPropertyChanged(nameof(CfgGuideText));
            OnPropertyChanged(nameof(TrainingSettingsSummaryModelText));
        }
    }
}
