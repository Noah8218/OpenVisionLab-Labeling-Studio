using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MediaBrush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public partial class WpfDatasetHealthWindow : FluentWindow
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
            "GridHeaderBrush",
            "SelectedRowBrush"
        };

        private WpfDatasetHealthViewModel observedViewModel;

        public WpfDatasetHealthWindow(WpfDatasetHealthViewModel viewModel = null)
        {
            InitializeComponent();
            BindingOperations.SetBinding(DatasetHealthClassCountColumn, DataGridColumn.HeaderProperty, new Binding("DataContext.ClassCountHeaderText") { Source = this, Mode = BindingMode.OneWay });
            LocalizationTextRuntimeService.RegisterWindow(this);
            LocalizationTextRuntimeService.RegisterRoot(DatasetHealthOverviewSurface);
            LocalizationTextRuntimeService.RegisterRoot(DatasetHealthSplitSurface);
            LocalizationTextRuntimeService.RegisterRoot(DatasetHealthVisualQaSurface);
            LocalizationTextRuntimeService.RegisterRoot(DatasetHealthClassesSurface);
            DataContextChanged += OnDataContextChanged;
            Loaded += OnWindowLoaded;
            DataContext = viewModel ?? new WpfDatasetHealthViewModel();
        }

        public WpfDatasetHealthViewModel ViewModel => DataContext as WpfDatasetHealthViewModel;

        protected override void OnClosed(EventArgs e)
        {
            DataContextChanged -= OnDataContextChanged;
            Loaded -= OnWindowLoaded;
            if (observedViewModel != null) observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            observedViewModel = null;
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (observedViewModel != null) observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            observedViewModel = e.NewValue as WpfDatasetHealthViewModel;
            if (observedViewModel != null) observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyDetailsWindowSize();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WpfDatasetHealthViewModel.IsDetailsExpanded)) ApplyDetailsWindowSize();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e) => ApplyDetailsWindowSize();

        private void ApplyDetailsWindowSize()
        {
            bool expanded = observedViewModel?.IsDetailsExpanded == true;
            MinWidth = expanded ? 980 : 600; MinHeight = expanded ? 650 : 0;
            Width = expanded ? 1000 : 640; Height = expanded ? 680 : double.NaN;
            SizeToContent = expanded ? SizeToContent.Manual : SizeToContent.Height;
            double availableHeight = Owner?.ActualHeight > 0 ? Owner.ActualHeight : SystemParameters.WorkArea.Height;
            MaxHeight = expanded ? double.PositiveInfinity : System.Math.Max(0, availableHeight - 40);
            if (!IsVisible || Owner == null || WindowState != WindowState.Normal) return;
            Width = System.Math.Min(Width, System.Math.Max(MinWidth, Owner.ActualWidth));
            if (expanded) Height = System.Math.Min(Height, System.Math.Max(MinHeight, Owner.ActualHeight));
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!IsVisible || Owner == null || WindowState != WindowState.Normal) return;
                Left = Owner.Left + System.Math.Max(0, (Owner.ActualWidth - ActualWidth) / 2);
                Top = Owner.Top + System.Math.Max(0, (Owner.ActualHeight - ActualHeight) / 2);
            }));
        }

        private void OnCloseRequested(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
            e.Handled = true;
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
            if (TryFindResource("RecipePageBrush") is MediaBrush background)
            {
                Background = background;
            }
        }
    }
}
