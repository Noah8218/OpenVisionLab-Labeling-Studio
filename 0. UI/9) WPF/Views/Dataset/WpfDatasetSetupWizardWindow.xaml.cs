using System;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfDatasetSetupWizardWindow : Window
    {
        public WpfDatasetSetupWizardWindow()
        {
            InitializeComponent();
            SizeChanged += OnWindowSizeChanged;
            LocalizationTextRuntimeService.RegisterWindow(this);
        }

        public WpfDatasetSetupWizardViewModel ViewModel => DataContext as WpfDatasetSetupWizardViewModel;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            double monitorHeight = screen.WorkingArea.Height / System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleY;
            double viewportHeight = Owner?.ActualHeight > 0 ? Math.Min(Owner.ActualHeight, monitorHeight) : monitorHeight;
            MaxHeight = Math.Max(MinHeight, viewportHeight - 40);
        }


        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsLoaded || !e.HeightChanged || WindowState != WindowState.Normal) return;

            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            double scale = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleY;
            double monitorTop = screen.WorkingArea.Top / scale;
            double maximumTop = Math.Max(monitorTop, screen.WorkingArea.Bottom / scale - e.NewSize.Height);
            Top = Math.Clamp(Top, monitorTop, maximumTop);
        }

        private void OnJumpToClassesClick(object sender, RoutedEventArgs e)
        {
            DatasetClassesSection.BringIntoView();
            WizardClassTagInput.Focus();
            WizardSetupScrollViewer.UpdateLayout();
            System.Windows.Rect bounds = DatasetClassesSection.TransformToAncestor(WizardSetupScrollViewer)
                .TransformBounds(new Rect(DatasetClassesSection.RenderSize));
            double offsetAdjustment = bounds.Top < 0
                ? bounds.Top
                : Math.Max(0, bounds.Bottom - WizardSetupScrollViewer.ViewportHeight);
            WizardSetupScrollViewer.ScrollToVerticalOffset(WizardSetupScrollViewer.VerticalOffset + offsetAdjustment);
        }

        protected override void OnClosed(EventArgs e)
        {
            ViewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
