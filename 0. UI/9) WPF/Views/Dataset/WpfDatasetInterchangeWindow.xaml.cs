using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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
            "InputBrush",
            "InputBorderBrush",
            "BorderBrushDark",
            "PrimaryTextBrush",
            "SecondaryTextBrush",
            "AccentBrush",
            "ToolbarButtonBrush",
            "ToolbarButtonBorderBrush",
            "GridLineBrush",
            "GridHeaderBrush"
        };

        private WpfDatasetInterchangeViewModel observedViewModel;

        public WpfDatasetInterchangeWindow(WpfDatasetInterchangeViewModel viewModel = null)
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
            DataContextChanged += OnDataContextChanged;
            DataContext = viewModel ?? new WpfDatasetInterchangeViewModel();
        }

        public WpfDatasetInterchangeViewModel ViewModel =>
            DataContext as WpfDatasetInterchangeViewModel;

        private void OnCloseWindowExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            DataContextChanged -= OnDataContextChanged;
            if (observedViewModel != null) observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            observedViewModel = null;
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (observedViewModel != null) observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            observedViewModel = e.NewValue as WpfDatasetInterchangeViewModel;
            if (observedViewModel != null) observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyDetailsWindowSize();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WpfDatasetInterchangeViewModel.IsDetailsExpanded)) ApplyDetailsWindowSize();
        }

        private void ApplyDetailsWindowSize()
        {
            bool expanded = observedViewModel?.IsDetailsExpanded == true;
            MinWidth = expanded ? 1040 : 540; MinHeight = expanded ? 680 : 280;
            Width = expanded ? 1040 : 560; Height = expanded ? 740 : 280;
            SizeToContent = expanded ? SizeToContent.Manual : SizeToContent.Height;
            if (!IsVisible || Owner == null || WindowState != WindowState.Normal) return;
            Width = System.Math.Min(Width, System.Math.Max(MinWidth, Owner.ActualWidth));
            if (expanded) Height = System.Math.Min(Height, System.Math.Max(MinHeight, Owner.ActualHeight));
            Left = Owner.Left + System.Math.Max(0, (Owner.ActualWidth - Width) / 2);
            Top = Owner.Top + System.Math.Max(0, (Owner.ActualHeight - Height) / 2);
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
            if (TryFindResource("RecipeSurfaceBrush") is MediaBrush background)
            {
                Background = background;
            }
        }
    }
}
