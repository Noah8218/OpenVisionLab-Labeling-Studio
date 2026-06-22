using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfYoloModelSettingsPanel : UserControl
    {
        public WpfYoloModelSettingsPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfYoloModelSettingsPanelViewModel ViewModel { get; } = new WpfYoloModelSettingsPanelViewModel();

        public event RoutedEventHandler BrowsePythonRequested;
        public event RoutedEventHandler BrowseProjectRootRequested;
        public event RoutedEventHandler BrowseClientScriptRequested;
        public event RoutedEventHandler BrowseWeightsRequested;
        public event RoutedEventHandler BrowseImageRootRequested;
        public event RoutedEventHandler SaveRequested;
        public event RoutedEventHandler ResetRequested;
        public event TextCompositionEventHandler DecimalTextInputPreview;
        public event TextCompositionEventHandler IntegerTextInputPreview;

        public Expander SettingsExpander => YoloModelSettingsExpander;
        public TextBox PythonPathBox => YoloPythonPathBox;
        public TextBox ProjectRootBox => YoloProjectRootBox;
        public TextBox ClientScriptBox => YoloClientScriptBox;
        public TextBox WeightsPathBox => YoloWeightsPathBox;
        public TextBox ImageRootBox => YoloImageRootBox;
        public TextBox ConfidenceBox => YoloConfidenceBox;
        public TextBox InferenceImageSizeBox => YoloInferenceImageSizeBox;
        public TextBox MaxCandidatesBox => YoloMaxCandidatesBox;
        public TextBox TimeoutBox => YoloTimeoutBox;
        public CheckBox AutoStartCheckBox => YoloAutoStartCheckBox;
        public WpfUiButton BrowsePythonButton => BrowseYoloPythonButton;
        public WpfUiButton BrowseProjectRootButton => BrowseYoloProjectRootButton;
        public WpfUiButton BrowseClientScriptButton => BrowseYoloClientScriptButton;
        public WpfUiButton BrowseWeightsButton => BrowseYoloWeightsButton;
        public WpfUiButton BrowseImageRootButton => BrowseYoloImageRootButton;
        public WpfUiButton SaveButton => SaveYoloSettingsButton;
        public WpfUiButton ResetButton => ResetYoloSettingsButton;

        private void BrowseYoloPythonButton_Click(object sender, RoutedEventArgs e) => BrowsePythonRequested?.Invoke(sender, e);
        private void BrowseYoloProjectRootButton_Click(object sender, RoutedEventArgs e) => BrowseProjectRootRequested?.Invoke(sender, e);
        private void BrowseYoloClientScriptButton_Click(object sender, RoutedEventArgs e) => BrowseClientScriptRequested?.Invoke(sender, e);
        private void BrowseYoloWeightsButton_Click(object sender, RoutedEventArgs e) => BrowseWeightsRequested?.Invoke(sender, e);
        private void BrowseYoloImageRootButton_Click(object sender, RoutedEventArgs e) => BrowseImageRootRequested?.Invoke(sender, e);
        private void SaveYoloSettingsButton_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(sender, e);
        private void ResetYoloSettingsButton_Click(object sender, RoutedEventArgs e) => ResetRequested?.Invoke(sender, e);
        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) => DecimalTextInputPreview?.Invoke(sender, e);
        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) => IntegerTextInputPreview?.Invoke(sender, e);
    }
}
