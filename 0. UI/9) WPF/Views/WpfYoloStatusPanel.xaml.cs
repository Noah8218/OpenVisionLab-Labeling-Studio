using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfYoloStatusPanel : UserControl
    {
        public WpfYoloStatusPanel()
        {
            InitializeComponent();
        }

        public WpfYoloStatusPanelViewModel ViewModel => DataContext as WpfYoloStatusPanelViewModel;

        public TextBlock SummaryTextBlock => YoloSettingsSummaryText;
        public TextBlock DetailTextBlock => YoloSettingsDetailText;
        public WpfUiButton FirstCheckButton => FirstCheckYoloButton;
        public WpfUiButton InstallRequirements => InstallRequirementsButton;
        public WpfUiButton RunSmokeButton => RunYoloSmokeButton;
        public WpfUiButton RestartWorkerButton => RestartPythonWorkerButton;
        public WpfUiButton StopWorkerButton => StopPythonWorkerButton;
        public TextBlock CommandStatusTextBlock => YoloCommandStatusText;
        public ProgressBar CommandProgress => YoloCommandProgressBar;
    }
}