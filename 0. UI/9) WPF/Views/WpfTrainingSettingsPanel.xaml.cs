using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfTrainingSettingsPanel : UserControl
    {
        public WpfTrainingSettingsPanel()
        {
            InitializeComponent();
        }

        public WpfTrainingSettingsPanelViewModel ViewModel => DataContext as WpfTrainingSettingsPanelViewModel;

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
    }
}