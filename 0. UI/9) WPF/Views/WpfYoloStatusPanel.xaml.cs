using System.Windows;
using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfYoloStatusPanel : UserControl
    {
        public WpfYoloStatusPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfYoloStatusPanelViewModel ViewModel { get; } = new WpfYoloStatusPanelViewModel();

        public event RoutedEventHandler CheckRequested;
        public event RoutedEventHandler InstallRequirementsRequested;
        public event RoutedEventHandler RunSmokeRequested;
        public event RoutedEventHandler RestartWorkerRequested;
        public event RoutedEventHandler StopWorkerRequested;

        public TextBlock SummaryTextBlock => YoloSettingsSummaryText;
        public TextBlock DetailTextBlock => YoloSettingsDetailText;
        public WpfUiButton FirstCheckButton => FirstCheckYoloButton;
        public WpfUiButton InstallRequirements => InstallRequirementsButton;
        public WpfUiButton RunSmokeButton => RunYoloSmokeButton;
        public WpfUiButton RestartWorkerButton => RestartPythonWorkerButton;
        public WpfUiButton StopWorkerButton => StopPythonWorkerButton;
        public TextBlock CommandStatusTextBlock => YoloCommandStatusText;
        public ProgressBar CommandProgress => YoloCommandProgressBar;

        private void CheckYoloButton_Click(object sender, RoutedEventArgs e) => CheckRequested?.Invoke(sender, e);
        private void InstallRequirementsButton_Click(object sender, RoutedEventArgs e) => InstallRequirementsRequested?.Invoke(sender, e);
        private void RunYoloSmokeButton_Click(object sender, RoutedEventArgs e) => RunSmokeRequested?.Invoke(sender, e);
        private void RestartPythonWorkerButton_Click(object sender, RoutedEventArgs e) => RestartWorkerRequested?.Invoke(sender, e);
        private void StopPythonWorkerButton_Click(object sender, RoutedEventArgs e) => StopWorkerRequested?.Invoke(sender, e);
    }
}
