using System;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfDatasetSetupWizardWindow : Window
    {
        public WpfDatasetSetupWizardWindow()
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
        }

        public WpfDatasetSetupWizardViewModel ViewModel => DataContext as WpfDatasetSetupWizardViewModel;

        protected override void OnClosed(EventArgs e)
        {
            ViewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
