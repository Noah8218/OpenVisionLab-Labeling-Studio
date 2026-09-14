using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace MvcVisionSystem
{
    public partial class WpfModelBenchmarkWindow : FluentWindow
    {
        private WpfModelBenchmarkViewModel observedViewModel;
        private readonly ModelBenchmarkQualityTaktChartRenderer qualityTaktChartRenderer = new();
        private bool isClosed;

        private static readonly string[] ThemeBrushKeys =
        {
            "AppBackgroundBrush",
            "FrameBrush",
            "PanelBrush",
            "PanelHeaderBrush",
            "CanvasBrush",
            "BorderBrushDark",
            "PrimaryTextBrush",
            "SecondaryTextBrush",
            "AccentBrush",
            "ToolbarButtonBrush",
            "ToolbarButtonBorderBrush",
            "ToolbarButtonHoverBrush",
            "DisabledTextBrush",
            "InputBrush",
            "InputBorderBrush",
            "GridLineBrush",
            "GridHeaderBrush",
            "RowHoverBrush",
            "SelectedRowBrush",
            "SelectedRowTextBrush",
            "BaselineBrush",
            "CandidateBrush"
        };

        public WpfModelBenchmarkWindow(WpfModelBenchmarkViewModel viewModel = null)
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
            DataContext = viewModel ?? new WpfModelBenchmarkViewModel();
            Loaded += OnWindowLoaded;
            Unloaded += OnWindowUnloaded;
        }

        public WpfModelBenchmarkViewModel ViewModel => DataContext as WpfModelBenchmarkViewModel;

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

            RenderQualityTaktCanvas();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (isClosed)
            {
                return;
            }

            OpenVisionLanguageService.LanguageChanged += OnLanguageChanged;
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    if (!isClosed)
                    {
                        ApplyLocalizedColumnHeaders();
                    }
                }));
            AttachDashboardViewModel();
            RenderQualityTaktCanvas();
        }

        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            OpenVisionLanguageService.LanguageChanged -= OnLanguageChanged;
            if (observedViewModel != null)
            {
                observedViewModel.PropertyChanged -= OnDashboardViewModelPropertyChanged;
                observedViewModel = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            isClosed = true;
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (isClosed)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    if (!isClosed)
                    {
                        ApplyLocalizedColumnHeaders();
                    }
                }));
        }

        private void ApplyLocalizedColumnHeaders()
        {
            if (ModelBenchmarkSummaryGrid == null)
            {
                return;
            }

            if (ModelBenchmarkSummaryGrid.Columns.Count > 3)
            {
                ModelBenchmarkSummaryGrid.Columns[3].Header =
                    OpenVisionLanguageService.T("WpfModelBenchmark.Header.Runtime");
            }

            if (ModelBenchmarkSummaryGrid.Columns.Count > 6)
            {
                ModelBenchmarkSummaryGrid.Columns[6].Header =
                    OpenVisionLanguageService.T("WpfModelBenchmark.Header.BaselineDelta");
            }
        }

        private void AttachDashboardViewModel()
        {
            WpfModelBenchmarkViewModel current = ViewModel;
            if (ReferenceEquals(current, observedViewModel))
            {
                return;
            }

            if (observedViewModel != null)
            {
                observedViewModel.PropertyChanged -= OnDashboardViewModelPropertyChanged;
            }

            observedViewModel = current;
            if (observedViewModel != null)
            {
                observedViewModel.PropertyChanged += OnDashboardViewModelPropertyChanged;
            }
        }

        private void OnDashboardViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (isClosed)
            {
                return;
            }

            if (!string.Equals(e.PropertyName, nameof(WpfModelBenchmarkViewModel.DashboardRevision), StringComparison.Ordinal))
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                RenderQualityTaktCanvas();
            }
            else
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() =>
                    {
                        if (!isClosed)
                        {
                            RenderQualityTaktCanvas();
                        }
                    }));
            }
        }

        private void ModelBenchmarkQualityTaktCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderQualityTaktCanvas();
        }

        private void RenderQualityTaktCanvas()
        {
            qualityTaktChartRenderer.Render(
                ModelBenchmarkQualityTaktCanvas,
                ViewModel?.DashboardQualityTaktPoints,
                ViewModel?.DashboardQualityTaktStatusText,
                ResolveBrush("GridLineBrush", MediaColor.FromRgb(42, 42, 42)),
                ResolveBrush("PrimaryTextBrush", MediaColor.FromRgb(247, 247, 247)),
                ResolveBrush("SecondaryTextBrush", MediaColor.FromRgb(183, 183, 183)),
                ResolveBrush("BaselineBrush", MediaColor.FromRgb(34, 197, 94)),
                ResolveBrush("CandidateBrush", MediaColor.FromRgb(78, 161, 255)));
        }

        private MediaBrush ResolveBrush(string resourceKey, MediaColor fallback)
        {
            return TryFindResource(resourceKey) as MediaBrush
                ?? new SolidColorBrush(fallback);
        }
    }
}
