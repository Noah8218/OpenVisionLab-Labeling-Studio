using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MvcVisionSystem
{
    // Presentation only. The comparison workspace ViewModel supplies the rows evaluated on the selected
    // dataset version; this View performs no evaluation, state mutation or IO. It matches the v2
    // prototype's speedChart(): processing time on the horizontal axis, the task's first metric on the
    // vertical axis, A in blue and B in orange, other models in grey.
    public sealed class ModelSpeedAccuracyChart : Control
    {
        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(IReadOnlyList<ModelComparisonWorkspaceRow>), typeof(ModelSpeedAccuracyChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty SpeedCaptionProperty = DependencyProperty.Register(nameof(SpeedCaption), typeof(string), typeof(ModelSpeedAccuracyChart),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty MetricCaptionProperty = DependencyProperty.Register(nameof(MetricCaption), typeof(string), typeof(ModelSpeedAccuracyChart),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(ModelSpeedAccuracyChart),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly Brush ABrush = Frozen(0x60, 0xA5, 0xFA);
        private static readonly Brush BBrush = Frozen(0xF5, 0xA5, 0x24);
        private static readonly Brush OtherBrush = Frozen(0x9A, 0xA6, 0xB6);
        private static readonly Pen GridPen = CreatePen(0x28, 0x30, 0x40, 1);
        private static readonly Pen RingPen = CreatePen(0x0F, 0x14, 0x1A, 2);
        private const double Left = 50, Right = 14, Top = 26, Bottom = 30;

        public ModelSpeedAccuracyChart() { Focusable = false; ClipToBounds = true; }
        public IReadOnlyList<ModelComparisonWorkspaceRow> Rows { get => (IReadOnlyList<ModelComparisonWorkspaceRow>)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
        public string SpeedCaption { get => (string)GetValue(SpeedCaptionProperty); set => SetValue(SpeedCaptionProperty, value); }
        public string MetricCaption { get => (string)GetValue(MetricCaptionProperty); set => SetValue(MetricCaptionProperty, value); }
        public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth <= Left + Right + 20 || ActualHeight <= Top + Bottom + 20) return;
            ModelComparisonWorkspaceRow[] points = (Rows ?? Array.Empty<ModelComparisonWorkspaceRow>())
                .Where(row => row != null && row.PrimaryMetric.HasValue && row.TaktValue.HasValue).ToArray();
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            FormattedText Text(string value, Brush brush, double size, FontWeight? weight = null)
                => new(value ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection, weight.HasValue ? new Typeface(FontFamily, FontStyle, weight.Value, FontStretch) : typeface, size, brush, pixelsPerDip);
            if (points.Length < 2)
            {
                FormattedText empty = Text(EmptyText, Foreground, FontSize);
                empty.MaxTextWidth = Math.Max(20, ActualWidth - 32);
                drawingContext.DrawText(empty, new Point(16, Math.Max(16, (ActualHeight - empty.Height) / 2)));
                return;
            }

            double width = ActualWidth - Left - Right, height = ActualHeight - Top - Bottom;
            double speedMax = points.Max(row => row.TaktValue.Value) * 1.15;
            if (speedMax <= 0) speedMax = 1;
            double low = points.Min(row => row.PrimaryMetric.Value), high = points.Max(row => row.PrimaryMetric.Value);
            double pad = Math.Max(0.01, (high - low) * 0.25);
            double metricMin = Math.Max(0, low - pad), metricMax = Math.Min(1, high + pad);
            if (metricMax - metricMin < 0.02) { metricMin = Math.Max(0, metricMax - 0.02); }
            double X(double speed) => Left + speed / speedMax * width;
            double Y(double metric) => Top + (1 - (metric - metricMin) / (metricMax - metricMin)) * height;

            foreach (double metric in new[] { metricMin, (metricMin + metricMax) / 2, metricMax })
            {
                double y = Y(metric);
                drawingContext.DrawLine(GridPen, new Point(Left, y), new Point(Left + width, y));
                FormattedText label = Text(metric.ToString("0.000", CultureInfo.CurrentCulture), Foreground, 11);
                drawingContext.DrawText(label, new Point(Left - 6 - label.Width, y - label.Height / 2));
            }
            foreach (double speed in new[] { 0, speedMax / 2, speedMax })
            {
                FormattedText label = Text(speed.ToString("0.0", CultureInfo.CurrentCulture), Foreground, 11);
                drawingContext.DrawText(label, new Point(Math.Min(X(speed) - label.Width / 2, ActualWidth - Right - label.Width), Top + height + 3));
            }
            FormattedText speedCaption = Text(SpeedCaption, Foreground, 11);
            drawingContext.DrawText(speedCaption, new Point(Left + width - speedCaption.Width, ActualHeight - speedCaption.Height - 1));
            FormattedText metricCaption = Text(MetricCaption, Foreground, 11);
            drawingContext.DrawText(metricCaption, new Point(Left + 4, Math.Max(2, Top - 6 - metricCaption.Height)));

            // Others first so A and B are drawn on top.
            foreach (ModelComparisonWorkspaceRow row in points.OrderBy(row => row.IsA || row.IsB))
            {
                Brush brush = row.IsA ? ABrush : row.IsB ? BBrush : OtherBrush;
                var center = new Point(X(row.TaktValue.Value), Y(row.PrimaryMetric.Value));
                drawingContext.DrawEllipse(brush, RingPen, center, row.IsA || row.IsB ? 6 : 4, row.IsA || row.IsB ? 6 : 4);
                if (row.IsA || row.IsB)
                {
                    FormattedText tag = Text(row.IsA ? "A" : "B", brush, 12, FontWeights.SemiBold);
                    drawingContext.DrawText(tag, new Point(Math.Min(center.X + 8, ActualWidth - Right - tag.Width), center.Y - tag.Height / 2));
                }
            }
        }

        private static Brush Frozen(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue)); brush.Freeze(); return brush;
        }

        private static Pen CreatePen(byte red, byte green, byte blue, double thickness)
        {
            var pen = new Pen(Frozen(red, green, blue), thickness); pen.Freeze(); return pen;
        }
    }
}
