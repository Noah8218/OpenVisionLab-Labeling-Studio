using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace MvcVisionSystem
{
    public partial class WpfModelBenchmarkWindow : FluentWindow
    {
        private WpfModelBenchmarkViewModel observedViewModel;

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
            "SelectedRowTextBrush"
        };

        public WpfModelBenchmarkWindow(WpfModelBenchmarkViewModel viewModel = null)
        {
            InitializeComponent();
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
            AttachDashboardViewModel();
            RenderQualityTaktCanvas();
        }

        private void OnWindowUnloaded(object sender, RoutedEventArgs e)
        {
            if (observedViewModel != null)
            {
                observedViewModel.PropertyChanged -= OnDashboardViewModelPropertyChanged;
                observedViewModel = null;
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
                    new Action(RenderQualityTaktCanvas));
            }
        }

        private void ModelBenchmarkQualityTaktCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderQualityTaktCanvas();
        }

        private void RenderQualityTaktCanvas()
        {
            if (ModelBenchmarkQualityTaktCanvas == null
                || ModelBenchmarkQualityTaktCanvas.ActualWidth < 24D
                || ModelBenchmarkQualityTaktCanvas.ActualHeight < 24D)
            {
                return;
            }

            Canvas canvas = ModelBenchmarkQualityTaktCanvas;
            canvas.Children.Clear();
            MediaBrush gridBrush = ResolveBrush("GridLineBrush", MediaColor.FromRgb(42, 42, 42));
            MediaBrush primaryBrush = ResolveBrush("PrimaryTextBrush", MediaColor.FromRgb(247, 247, 247));
            MediaBrush secondaryBrush = ResolveBrush("SecondaryTextBrush", MediaColor.FromRgb(183, 183, 183));
            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            const double axisPadding = 10D;

            canvas.Children.Add(new Line
            {
                X1 = axisPadding,
                X2 = axisPadding,
                Y1 = axisPadding,
                Y2 = height - axisPadding,
                Stroke = gridBrush,
                StrokeThickness = 1D
            });
            canvas.Children.Add(new Line
            {
                X1 = axisPadding,
                X2 = width - axisPadding,
                Y1 = height - axisPadding,
                Y2 = height - axisPadding,
                Stroke = gridBrush,
                StrokeThickness = 1D
            });

            WpfModelBenchmarkDashboardPointViewModel[] points = ViewModel?.DashboardQualityTaktPoints.ToArray()
                ?? Array.Empty<WpfModelBenchmarkDashboardPointViewModel>();
            if (points.Length < 2)
            {
                var empty = new System.Windows.Controls.TextBlock
                {
                    Text = "\uBE44\uAD50 \uAC00\uB2A5\uD55C \uC2E4\uD589\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
                    Foreground = secondaryBrush,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 10D,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = ViewModel?.DashboardQualityTaktStatusText
                };
                Canvas.SetLeft(empty, Math.Max(axisPadding + 8D, (width - 170D) / 2D));
                Canvas.SetTop(empty, Math.Max(axisPadding + 8D, (height - 20D) / 2D));
                canvas.Children.Add(empty);
                return;
            }

            double minTakt = points.Min(point => point.TaktMs);
            double maxTakt = points.Max(point => point.TaktMs);
            double minQuality = points.Min(point => point.QualityValue);
            double maxQuality = points.Max(point => point.QualityValue);
            double taktPadding = Math.Max((maxTakt - minTakt) * 0.12D, 1D);
            double qualityPadding = Math.Max((maxQuality - minQuality) * 0.12D, points[0].IsPercentMetric ? 0.01D : 0.5D);
            minTakt -= taktPadding;
            maxTakt += taktPadding;
            minQuality -= qualityPadding;
            maxQuality += qualityPadding;
            double plotWidth = Math.Max(1D, width - axisPadding * 2D);
            double plotHeight = Math.Max(1D, height - axisPadding * 2D);

            foreach (WpfModelBenchmarkDashboardPointViewModel point in points)
            {
                double x = axisPadding + (maxTakt - point.TaktMs) / (maxTakt - minTakt) * plotWidth;
                double y = axisPadding + (maxQuality - point.QualityValue) / (maxQuality - minQuality) * plotHeight;
                MediaBrush pointBrush = point.IsBaseline
                    ? new SolidColorBrush(MediaColor.FromRgb(34, 197, 94))
                    : new SolidColorBrush(MediaColor.FromRgb(78, 161, 255));
                var marker = new Ellipse
                {
                    Width = 13D,
                    Height = 13D,
                    Fill = pointBrush,
                    Stroke = primaryBrush,
                    StrokeThickness = 1D,
                    ToolTip = point.ToolTipText
                };
                Canvas.SetLeft(marker, x - marker.Width / 2D);
                Canvas.SetTop(marker, y - marker.Height / 2D);
                canvas.Children.Add(marker);

                var label = new System.Windows.Controls.TextBlock
                {
                    Text = point.QualityText + " / " + point.TaktText,
                    Foreground = primaryBrush,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 8D,
                    ToolTip = point.ToolTipText
                };
                double labelLeft = Math.Clamp(x + 9D, axisPadding + 2D, Math.Max(axisPadding + 2D, width - 108D));
                double labelTop = Math.Clamp(y - 18D, axisPadding + 2D, Math.Max(axisPadding + 2D, height - 18D));
                Canvas.SetLeft(label, labelLeft);
                Canvas.SetTop(label, labelTop);
                canvas.Children.Add(label);
            }
        }

        private MediaBrush ResolveBrush(string resourceKey, MediaColor fallback)
        {
            return TryFindResource(resourceKey) as MediaBrush
                ?? new SolidColorBrush(fallback);
        }
    }
}
