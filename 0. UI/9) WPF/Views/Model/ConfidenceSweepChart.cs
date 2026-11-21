using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MvcVisionSystem
{
    // Presentation only. Draws the confidence sweep the evaluation recorded for models A and B
    // (WpfModelBenchmarkThresholdReview): F1 as a solid line, recall as a dashed line, the current
    // confidence as a dashed marker and each model's highest F1 as a dot. Matches the v2 prototype's
    // confChart(); A blue, B orange.
    public sealed class ConfidenceSweepChart : Control
    {
        public static readonly DependencyProperty BaselineProperty = DependencyProperty.Register(nameof(Baseline), typeof(IReadOnlyList<WpfModelBenchmarkThresholdReview>), typeof(ConfidenceSweepChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CandidateProperty = DependencyProperty.Register(nameof(Candidate), typeof(IReadOnlyList<WpfModelBenchmarkThresholdReview>), typeof(ConfidenceSweepChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CurrentConfidenceProperty = DependencyProperty.Register(nameof(CurrentConfidence), typeof(double), typeof(ConfidenceSweepChart),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CurrentTextProperty = DependencyProperty.Register(nameof(CurrentText), typeof(string), typeof(ConfidenceSweepChart),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(ConfidenceSweepChart),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly Color AColor = Color.FromRgb(0x60, 0xA5, 0xFA), BColor = Color.FromRgb(0xF5, 0xA5, 0x24);
        private static readonly Pen GridPen = CreatePen(Color.FromRgb(0x28, 0x30, 0x40), 1, null);
        private static readonly Pen MarkerPen = CreatePen(Color.FromRgb(0x9A, 0xA6, 0xB6), 1, new DoubleCollection { 3, 3 });
        private static readonly Pen RingPen = CreatePen(Color.FromRgb(0x0F, 0x14, 0x1A), 2, null);
        private const double Left = 36, Right = 10, Top = 10, Bottom = 20;

        public ConfidenceSweepChart() { Focusable = false; ClipToBounds = true; }
        public IReadOnlyList<WpfModelBenchmarkThresholdReview> Baseline { get => (IReadOnlyList<WpfModelBenchmarkThresholdReview>)GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
        public IReadOnlyList<WpfModelBenchmarkThresholdReview> Candidate { get => (IReadOnlyList<WpfModelBenchmarkThresholdReview>)GetValue(CandidateProperty); set => SetValue(CandidateProperty, value); }
        public double CurrentConfidence { get => (double)GetValue(CurrentConfidenceProperty); set => SetValue(CurrentConfidenceProperty, value); }
        public string CurrentText { get => (string)GetValue(CurrentTextProperty); set => SetValue(CurrentTextProperty, value); }
        public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

        internal static WpfModelBenchmarkThresholdReview[] Usable(IReadOnlyList<WpfModelBenchmarkThresholdReview> sweep)
            => (sweep ?? Array.Empty<WpfModelBenchmarkThresholdReview>())
                .Where(point => point != null && double.IsFinite(point.Confidence) && point.Confidence >= 0 && point.Confidence <= 1 && Valid(point.F1) && Valid(point.Recall))
                .OrderBy(point => point.Confidence).ToArray();

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth <= Left + Right + 20 || ActualHeight <= Top + Bottom + 20) return;
            WpfModelBenchmarkThresholdReview[] a = Usable(Baseline), b = Usable(Candidate);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            FormattedText Text(string value, Brush brush) => new(value ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection, typeface, 11, brush, pixelsPerDip);
            if (a.Length < 2 || b.Length < 2)
            {
                FormattedText empty = Text(EmptyText, Foreground);
                empty.MaxTextWidth = Math.Max(20, ActualWidth - 24);
                drawingContext.DrawText(empty, new Point(12, Math.Max(8, (ActualHeight - empty.Height) / 2)));
                return;
            }

            double low = a.Concat(b).SelectMany(point => new[] { point.F1.Value, point.Recall.Value }).Min();
            double yMin = Math.Max(0, Math.Floor(low * 10) / 10);
            if (yMin >= 0.95) yMin = 0.9;
            double xMin = Math.Min(a[0].Confidence, b[0].Confidence), xMax = Math.Max(a[^1].Confidence, b[^1].Confidence);
            if (xMax - xMin < 0.05) xMax = xMin + 0.05;
            double width = ActualWidth - Left - Right, height = ActualHeight - Top - Bottom;
            double X(double t) => Left + (t - xMin) / (xMax - xMin) * width;
            double Y(double v) => Top + (1 - (v - yMin) / (1 - yMin)) * height;

            foreach (double value in new[] { yMin, (yMin + 1) / 2, 1 })
            {
                drawingContext.DrawLine(GridPen, new Point(Left, Y(value)), new Point(Left + width, Y(value)));
                FormattedText label = Text(value.ToString("0.00", CultureInfo.CurrentCulture), Foreground);
                drawingContext.DrawText(label, new Point(Left - 4 - label.Width, Y(value) - label.Height / 2));
            }
            foreach (double t in new[] { xMin, (xMin + xMax) / 2, xMax })
            {
                FormattedText label = Text(t.ToString("0.0", CultureInfo.CurrentCulture), Foreground);
                drawingContext.DrawText(label, new Point(Math.Clamp(X(t) - label.Width / 2, 0, ActualWidth - label.Width), Top + height + 3));
            }
            if (double.IsFinite(CurrentConfidence) && CurrentConfidence >= xMin && CurrentConfidence <= xMax)
            {
                double x = X(CurrentConfidence);
                drawingContext.DrawLine(MarkerPen, new Point(x, Top), new Point(x, Top + height));
                FormattedText label = Text(CurrentText, Foreground);
                drawingContext.DrawText(label, new Point(Math.Min(x + 4, ActualWidth - Right - label.Width), Top));
            }

            void Series(WpfModelBenchmarkThresholdReview[] points, Color color)
            {
                Pen solid = CreatePen(color, 2, null), dashed = CreatePen(color, 1.6, new DoubleCollection { 4, 3 });
                drawingContext.DrawGeometry(null, dashed, Line(points, point => point.Recall.Value));
                drawingContext.DrawGeometry(null, solid, Line(points, point => point.F1.Value));
                WpfModelBenchmarkThresholdReview best = points.OrderByDescending(point => point.F1.Value).ThenBy(point => point.Confidence).First();
                var brush = new SolidColorBrush(color); brush.Freeze();
                drawingContext.DrawEllipse(brush, RingPen, new Point(X(best.Confidence), Y(best.F1.Value)), 4, 4);
            }
            StreamGeometry Line(WpfModelBenchmarkThresholdReview[] points, Func<WpfModelBenchmarkThresholdReview, double> value)
            {
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(new Point(X(points[0].Confidence), Y(value(points[0]))), false, false);
                    context.PolyLineTo(points.Skip(1).Select(point => new Point(X(point.Confidence), Y(value(point)))).ToList(), true, true);
                }
                geometry.Freeze();
                return geometry;
            }
            Series(a, AColor);
            Series(b, BColor);
        }

        private static bool Valid(double? value) => value.HasValue && double.IsFinite(value.Value) && value >= 0 && value <= 1;

        private static Pen CreatePen(Color color, double thickness, DoubleCollection dashes)
        {
            var brush = new SolidColorBrush(color); brush.Freeze();
            var pen = new Pen(brush, thickness);
            if (dashes != null) pen.DashStyle = new DashStyle(dashes, 0);
            pen.Freeze();
            return pen;
        }
    }
}
