using System;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfDatasetSelectionWindow : Window
    {
        public WpfDatasetSelectionWindow()
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
        }

        public WpfDatasetSelectionWindowViewModel ViewModel => DataContext as WpfDatasetSelectionWindowViewModel;

        protected override void OnClosed(EventArgs e)
        {
            ViewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
