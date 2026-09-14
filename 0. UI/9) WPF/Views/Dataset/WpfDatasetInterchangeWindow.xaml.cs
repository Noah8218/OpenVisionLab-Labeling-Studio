using System;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MediaBrush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public partial class WpfDatasetInterchangeWindow : FluentWindow
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

        public WpfDatasetInterchangeWindow(WpfDatasetInterchangeViewModel viewModel = null)
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
            DataContext = viewModel ?? new WpfDatasetInterchangeViewModel();
        }

        public WpfDatasetInterchangeViewModel ViewModel =>
            DataContext as WpfDatasetInterchangeViewModel;

        protected override void OnClosed(EventArgs e)
        {
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

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
    }
}
