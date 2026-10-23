using System.Windows;
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
        public static readonly DependencyProperty SettingsSectionProperty = DependencyProperty.Register(nameof(SettingsSection), typeof(int), typeof(WpfYoloModelSettingsPanel), new PropertyMetadata(0, OnSettingsSectionChanged));
        public static readonly DependencyProperty TrainingWorkspaceProperty = DependencyProperty.Register(nameof(TrainingWorkspace), typeof(TrainingWorkspaceViewModel), typeof(WpfYoloModelSettingsPanel));
        public static readonly DependencyProperty ShowRuntimeFieldsProperty = DependencyProperty.Register(nameof(ShowRuntimeFields), typeof(bool), typeof(WpfYoloModelSettingsPanel), new PropertyMetadata(true));
        public static readonly DependencyProperty ShowInspectionFieldsProperty = DependencyProperty.Register(nameof(ShowInspectionFields), typeof(bool), typeof(WpfYoloModelSettingsPanel), new PropertyMetadata(true));
        public static readonly DependencyProperty ShowLegacyFieldsProperty = DependencyProperty.Register(nameof(ShowLegacyFields), typeof(bool), typeof(WpfYoloModelSettingsPanel), new PropertyMetadata(true));

        public WpfYoloModelSettingsPanel()
        {
            InitializeComponent();
        }

        public int SettingsSection { get => (int)GetValue(SettingsSectionProperty); set => SetValue(SettingsSectionProperty, value); }
        public TrainingWorkspaceViewModel TrainingWorkspace { get => (TrainingWorkspaceViewModel)GetValue(TrainingWorkspaceProperty); set => SetValue(TrainingWorkspaceProperty, value); }
        public bool ShowRuntimeFields => (bool)GetValue(ShowRuntimeFieldsProperty);
        public bool ShowInspectionFields => (bool)GetValue(ShowInspectionFieldsProperty);
        public bool ShowLegacyFields => (bool)GetValue(ShowLegacyFieldsProperty);

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

        private static void OnSettingsSectionChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
        {
            var panel = (WpfYoloModelSettingsPanel)owner;
            int section = (int)args.NewValue;
            panel.SetValue(ShowRuntimeFieldsProperty, section != 2);
            panel.SetValue(ShowInspectionFieldsProperty, section != 1);
            panel.SetValue(ShowLegacyFieldsProperty, section == 0);
            if (section != 0) { panel.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true); panel.AdvancedSettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true); }
        }

    }
}
