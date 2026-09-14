using System;
using System.Windows;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MediaBrush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public partial class WpfBatchDetectionPreflightWindow : FluentWindow
    {
        private static readonly string[] ThemeBrushKeys =
        {
            "AppBackgroundBrush",
            "PanelBrush",
            "CanvasBrush",
            "BorderBrushDark",
            "PrimaryTextBrush",
            "SecondaryTextBrush",
            "AccentBrush",
            "ToolbarButtonBrush",
            "ToolbarButtonBorderBrush",
            "GridLineBrush",
            "GridHeaderBrush"
        };

        public WpfBatchDetectionPreflightWindow(WpfBatchDetectionPreflightViewModel viewModel)
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            viewModel.StartRequested += ViewModel_StartRequested;
            Closed += Window_Closed;
        }

        public WpfBatchDetectionPlan SelectedPlan { get; private set; }

        public void ApplyThemeFrom(FrameworkElement source)
        {
            if (source == null)
            {
                return;
            }

            foreach (string key in ThemeBrushKeys)
            {
                if (source.TryFindResource(key) is MediaBrush brush)
                {
                    Resources[key] = brush;
                }
            }

            ApplicationThemeManager.Apply(this);
            if (TryFindResource("AppBackgroundBrush") is MediaBrush background)
            {
                Background = background;
            }
        }

        private void ViewModel_StartRequested(object sender, WpfBatchDetectionPlan plan)
        {
            SelectedPlan = plan;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is WpfBatchDetectionPreflightViewModel viewModel)
            {
                viewModel.StartRequested -= ViewModel_StartRequested;
                viewModel.Dispose();
            }

            Closed -= Window_Closed;
        }
    }
}
