using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfTrainingSettingsPanel : UserControl
    {
        public WpfTrainingSettingsPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfTrainingSettingsPanelViewModel ViewModel { get; } = new WpfTrainingSettingsPanelViewModel();

        public event RoutedEventHandler RefreshReadinessRequested;
        public event RoutedEventHandler StartTrainingRequested;
        public event RoutedEventHandler StopTrainingRequested;
        public event TextCompositionEventHandler IntegerTextInputPreview;

        public Expander SettingsExpander => TrainingSettingsExpander;
        public TextBox ImageSizeBox => TrainingImageSizeBox;
        public TextBox BatchBox => TrainingBatchBox;
        public TextBox EpochBox => TrainingEpochBox;
        public ComboBox CfgBox => TrainingCfgBox;
        public ComboBox WeightBox => TrainingWeightBox;
        public TextBox ValidationPercentBox => TrainingValidationPercentBox;
        public TextBox TestPercentBox => TrainingTestPercentBox;
        public TextBox SplitSeedBox => TrainingSplitSeedBox;
        public TextBlock SplitPolicyHintTextBlock => TrainingSplitPolicyHintText;
        public WpfUiButton RefreshReadinessButton => RefreshTrainingReadinessButton;
        public WpfUiButton StartButton => StartTrainingButton;
        public WpfUiButton StopButton => StopTrainingButton;
        public TextBlock ReadinessTextBlock => TrainingReadinessText;
        public ProgressBar Progress => TrainingProgressBar;
        public TextBlock ProgressTextBlock => TrainingProgressText;
        public TextBlock EpochTextBlock => TrainingEpochText;

        private void RefreshTrainingReadinessButton_Click(object sender, RoutedEventArgs e) => RefreshReadinessRequested?.Invoke(sender, e);
        private void StartTrainingButton_Click(object sender, RoutedEventArgs e) => StartTrainingRequested?.Invoke(sender, e);
        private void StopTrainingButton_Click(object sender, RoutedEventArgs e) => StopTrainingRequested?.Invoke(sender, e);
        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) => IntegerTextInputPreview?.Invoke(sender, e);
    }
}
