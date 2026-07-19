using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfYoloModelSettingsPanel : UserControl
    {
        public WpfYoloModelSettingsPanel()
        {
            InitializeComponent();
        }

        public WpfYoloModelSettingsPanelViewModel ViewModel => DataContext as WpfYoloModelSettingsPanelViewModel;

        public Expander SettingsExpander => YoloModelSettingsExpander;
        public Expander AdvancedSettingsExpander => YoloAdvancedModelSettingsExpander;
        public Border SummaryPanel => YoloModelSettingsSummaryPanel;
        public Border InspectionModelQuickPanel => YoloInspectionModelQuickPanel;
        public TextBlock SummaryModelTextBlock => YoloModelSettingsSummaryModelText;
        public TextBlock SummaryRuntimeTextBlock => YoloModelSettingsSummaryRuntimeText;
        public ComboBox ModelEngineBox => YoloModelEngineBox;
        public TextBox PythonPathBox => YoloPythonPathBox;
        public TextBox ProjectRootBox => YoloProjectRootBox;
        public TextBox ClientScriptBox => YoloClientScriptBox;
        public TextBox WeightsPathBox => YoloWeightsPathBox;
        public TextBox ImageRootBox => YoloImageRootBox;
        public TextBox ConfidenceBox => YoloConfidenceBox;
        public TextBox MaxCandidatesBox => YoloMaxCandidatesBox;
        public TextBox InferenceImageSizeBox => YoloInferenceImageSizeBox;
        public TextBox TimeoutBox => YoloTimeoutBox;
        public CheckBox AutoStartCheckBox => YoloAutoStartCheckBox;
        public WpfUiButton BrowsePythonButton => BrowseYoloPythonButton;
        public WpfUiButton BrowseProjectRootButton => BrowseYoloProjectRootButton;
        public WpfUiButton BrowseClientScriptButton => BrowseYoloClientScriptButton;
        public WpfUiButton BrowseWeightsButton => BrowseYoloWeightsButton;
        public WpfUiButton BrowseImageRootButton => BrowseYoloImageRootButton;
        public WpfUiButton SaveButton => SaveYoloSettingsButton;
        public WpfUiButton ResetButton => ResetYoloSettingsButton;
        public WpfUiButton RuntimeInstallPackageButton => YoloRuntimeInstallPackageButton;
        public WpfUiButton RuntimeUninstallPackageButton => YoloRuntimeUninstallPackageButton;

        private bool isApplyingModelEngineSelection;
        private string modelEngineBeforeDropDown = string.Empty;

        private void YoloModelEngineBox_DropDownOpened(object sender, System.EventArgs e)
        {
            modelEngineBeforeDropDown = ModelEngineBox.SelectedItem as string ?? string.Empty;
        }

        private void YoloModelEngineBox_DropDownClosed(object sender, System.EventArgs e)
        {
            if (isApplyingModelEngineSelection
                || sender is not ComboBox comboBox
                || comboBox.SelectedItem is not string engine
                || string.Equals(modelEngineBeforeDropDown, engine, System.StringComparison.Ordinal)
                || ViewModel?.RuntimeProfileActionCommand?.CanExecute(engine) != true)
            {
                return;
            }

            try
            {
                isApplyingModelEngineSelection = true;
                ViewModel.RuntimeProfileActionCommand.Execute(engine);
            }
            finally
            {
                isApplyingModelEngineSelection = false;
            }
        }
    }
}
