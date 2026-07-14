using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
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
        public Expander AdvancedSettingsExpander => TrainingAdvancedSettingsExpander;
        public Border SummaryPanel => TrainingSettingsSummaryPanel;
        public TextBlock SummaryRuntimeTextBlock => TrainingSettingsSummaryRuntimeText;
        public Border PostTrainingActionPanel => PostTrainingModelActionPanel;
        public TextBlock PostTrainingModelStatus => PostTrainingModelStatusText;
        public TextBlock PostTrainingModelDetail => PostTrainingModelDetailText;
        public WpfUiButton ReviewTrainedModel => ReviewTrainedModelButton;
        public WpfUiButton ConfirmTrainedModel => ConfirmTrainedModelButton;
        public WpfUiButton RunYoloEngineComparison => RunYoloEngineComparisonButton;
        public TextBox ImageSizeBox => TrainingImageSizeBox;
        public TextBox BatchBox => TrainingBatchBox;
        public TextBox EpochBox => TrainingEpochBox;
        public ComboBox CfgBox => TrainingCfgBox;
        public ComboBox WeightBox => TrainingWeightBox;
        public TextBox ValidationPercentBox => TrainingValidationPercentBox;
        public TextBox TestPercentBox => TrainingTestPercentBox;
        public TextBox SplitSeedBox => TrainingSplitSeedBox;
        public TextBlock SplitPolicyHintTextBlock => TrainingSplitPolicyHintText;
        public WpfUiButton ApplyFastPresetButton => ApplyFastTrainingPresetButton;
        public WpfUiButton RefreshReadinessButton => RefreshTrainingReadinessButton;
        public WpfUiButton StartButton => StartTrainingButton;
        public WpfUiButton StopButton => StopTrainingButton;
        public TextBlock ReadinessTextBlock => TrainingReadinessText;
        public ProgressBar Progress => TrainingProgressBar;
        public TextBlock ProgressTextBlock => TrainingProgressText;
        public TextBlock EpochTextBlock => TrainingEpochText;
    }
}
