using System;
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
        private string trainingReadinessText = "\uD559\uC2B5 \uC0C1\uD0DC \uBBF8\uD655\uC778";
        private string trainingProgressText = "\uD559\uC2B5 \uB300\uAE30";
        private string trainingEpochStatusText = string.Empty;
        private double trainingProgressValue;
        private bool trainingProgressIsIndeterminate;
        private MediaBrush trainingReadinessForeground = MediaBrushes.Gray;
        private MediaBrush trainingProgressForeground = MediaBrushes.Gray;
        private bool isRefreshReadinessEnabled = true;
        private bool isStartTrainingEnabled = true;
        private bool isStopTrainingEnabled;
        private ICommand refreshReadinessCommand = new RelayCommand(NoOpCommand);
        private ICommand startTrainingCommand = new RelayCommand(NoOpCommand);
        private ICommand stopTrainingCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfTrainingSettingsPanel);

        public string SplitPolicyHintText =>
            "Validation %\uB294 \uD559\uC2B5 \uC911 \uD655\uC778\uC6A9, Test %\uB294 \uD559\uC2B5 \uD6C4 \uCD5C\uC885 \uBAA8\uB378 \uBE44\uAD50\uC6A9\uC785\uB2C8\uB2E4. \uB450 \uAC12\uC744 \uD569\uCCD0 100% \uC774\uD558\uB85C \uC124\uC815\uD558\uC138\uC694.";

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
        public string ImageSizeText
        {
            get => imageSizeText;
            set => SetProperty(ref imageSizeText, value ?? string.Empty);
        }

        public string BatchText
        {
            get => batchText;
            set => SetProperty(ref batchText, value ?? string.Empty);
        }

        public string EpochText
        {
            get => epochText;
            set => SetProperty(ref epochText, value ?? string.Empty);
        }

        public string Cfg
        {
            get => cfg;
            set => SetProperty(ref cfg, value ?? string.Empty);
        }

        public string Weight
        {
            get => weight;
            set => SetProperty(ref weight, value ?? string.Empty);
        }

        public string ValidationPercentText
        {
            get => validationPercentText;
            set => SetProperty(ref validationPercentText, value ?? string.Empty);
        }

        public string TestPercentText
        {
            get => testPercentText;
            set => SetProperty(ref testPercentText, value ?? string.Empty);
        }

        public string SplitSeedText
        {
            get => splitSeedText;
            set => SetProperty(ref splitSeedText, value ?? string.Empty);
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

        public bool IsStopTrainingEnabled
        {
            get => isStopTrainingEnabled;
            private set => SetProperty(ref isStopTrainingEnabled, value);
        }

        public void ConfigureCommands(Action refreshReadiness, Action startTraining, Action stopTraining)
        {
            // Training commands stay injected so long-running workflow logic remains outside the view.
            RefreshReadinessCommand = new RelayCommand(refreshReadiness ?? NoOpCommand);
            StartTrainingCommand = new RelayCommand(startTraining ?? NoOpCommand);
            StopTrainingCommand = new RelayCommand(stopTraining ?? NoOpCommand);
        }
        public void LoadFrom(TrainingSettings training, YoloDatasetSettings dataset)
        {
            if (training == null || dataset == null)
            {
                return;
            }

            ImageSizeText = training.ImageSize.ToString(CultureInfo.InvariantCulture);
            BatchText = training.Batch.ToString(CultureInfo.InvariantCulture);
            EpochText = training.Epoch.ToString(CultureInfo.InvariantCulture);
            Cfg = training.Cfg ?? string.Empty;
            Weight = training.Weight ?? string.Empty;
            ValidationPercentText = dataset.ValidationPercent.ToString(CultureInfo.InvariantCulture);
            TestPercentText = dataset.TestPercent.ToString(CultureInfo.InvariantCulture);
            SplitSeedText = dataset.SplitSeed.ToString(CultureInfo.InvariantCulture);
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
            IsRefreshReadinessEnabled = canRunGeneralCommands;
            IsStartTrainingEnabled = canRunGeneralCommands;
            IsStopTrainingEnabled = state?.CanStopTraining == true;
        }

        private static void NoOpCommand()
        {
        }
    }
}
