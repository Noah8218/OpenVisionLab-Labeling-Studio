using System.Windows.Controls;
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
    }
}
