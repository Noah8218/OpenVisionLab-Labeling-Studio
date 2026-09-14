using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF-only projection of benchmark quality/takt points into the
    /// dashboard canvas. It receives the already prepared ViewModel snapshot;
    /// it does not read application state, perform persistence, or own a
    /// Window lifetime.
    /// </summary>
    internal sealed class ModelBenchmarkQualityTaktChartRenderer
    {
        private const double MinimumCanvasDimension = 24D;
        private const double AxisPadding = 10D;

        internal void Render(
            Canvas canvas,
            IReadOnlyList<WpfModelBenchmarkDashboardPointViewModel> sourcePoints,
            string statusText,
            MediaBrush gridBrush,
            MediaBrush primaryBrush,
            MediaBrush secondaryBrush,
            MediaBrush baselineBrush,
            MediaBrush candidateBrush)
        {
            if (canvas == null
                || canvas.ActualWidth < MinimumCanvasDimension
                || canvas.ActualHeight < MinimumCanvasDimension)
            {
                return;
            }

            canvas.Children.Clear();
            gridBrush ??= new SolidColorBrush(MediaColor.FromRgb(42, 42, 42));
            primaryBrush ??= new SolidColorBrush(MediaColor.FromRgb(247, 247, 247));
            secondaryBrush ??= new SolidColorBrush(MediaColor.FromRgb(183, 183, 183));
            baselineBrush ??= new SolidColorBrush(MediaColor.FromRgb(34, 197, 94));
            candidateBrush ??= new SolidColorBrush(MediaColor.FromRgb(78, 161, 255));

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            canvas.Children.Add(new Line
            {
                X1 = AxisPadding,
                X2 = AxisPadding,
                Y1 = AxisPadding,
                Y2 = height - AxisPadding,
                Stroke = gridBrush,
                StrokeThickness = 1D
            });
            canvas.Children.Add(new Line
            {
                X1 = AxisPadding,
                X2 = width - AxisPadding,
                Y1 = height - AxisPadding,
                Y2 = height - AxisPadding,
                Stroke = gridBrush,
                StrokeThickness = 1D
            });

            WpfModelBenchmarkDashboardPointViewModel[] points = sourcePoints?.ToArray()
                ?? Array.Empty<WpfModelBenchmarkDashboardPointViewModel>();
            if (points.Length < 2)
            {
                var empty = new TextBlock
                {
                    Text = "\uBE44\uAD50 \uAC00\uB2A5\uD55C \uC2E4\uD589\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.",
                    Foreground = secondaryBrush,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10D,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = statusText
                };
                Canvas.SetLeft(empty, Math.Max(AxisPadding + 8D, (width - 170D) / 2D));
                Canvas.SetTop(empty, Math.Max(AxisPadding + 8D, (height - 20D) / 2D));
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
            double plotWidth = Math.Max(1D, width - AxisPadding * 2D);
            double plotHeight = Math.Max(1D, height - AxisPadding * 2D);

            foreach (WpfModelBenchmarkDashboardPointViewModel point in points)
            {
                double x = AxisPadding + (maxTakt - point.TaktMs) / (maxTakt - minTakt) * plotWidth;
                double y = AxisPadding + (maxQuality - point.QualityValue) / (maxQuality - minQuality) * plotHeight;
                var marker = new Ellipse
                {
                    Width = 13D,
                    Height = 13D,
                    Fill = point.IsBaseline ? baselineBrush : candidateBrush,
                    Stroke = primaryBrush,
                    StrokeThickness = 1D,
                    ToolTip = point.ToolTipText
                };
                Canvas.SetLeft(marker, x - marker.Width / 2D);
                Canvas.SetTop(marker, y - marker.Height / 2D);
                canvas.Children.Add(marker);

                var label = new TextBlock
                {
                    Text = point.QualityText + " / " + point.TaktText,
                    Foreground = primaryBrush,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 8D,
                    ToolTip = point.ToolTipText
                };
                double labelLeft = Math.Clamp(x + 9D, AxisPadding + 2D, Math.Max(AxisPadding + 2D, width - 108D));
                double labelTop = Math.Clamp(y - 18D, AxisPadding + 2D, Math.Max(AxisPadding + 2D, height - 18D));
                Canvas.SetLeft(label, labelLeft);
                Canvas.SetTop(label, labelTop);
                canvas.Children.Add(label);
            }
        }
    }
}
