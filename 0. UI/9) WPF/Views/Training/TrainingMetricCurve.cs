using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MvcVisionSystem
{
    // Presentation only. The inherited workspace ViewModel supplies the received
    // immutable samples; this View performs no training, state mutation or IO.
    public sealed class TrainingMetricCurve : Control
    {
        public static readonly DependencyProperty MetricsProperty = DependencyProperty.Register(nameof(Metrics), typeof(IReadOnlyList<TrainingEpochMetric>), typeof(TrainingMetricCurve),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty EpochsProperty = DependencyProperty.Register(nameof(Epochs), typeof(int), typeof(TrainingMetricCurve),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty MetricOnlyProperty = DependencyProperty.Register(nameof(MetricOnly), typeof(bool), typeof(TrainingMetricCurve),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(TrainingMetricCurve),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly Pen TrainPen = CreatePen(0x60, 0xA5, 0xFA);
        private static readonly Pen ValidationPen = CreatePen(0xF5, 0xA5, 0x24);
        private static readonly Pen MetricPen = CreatePen(0x3C, 0xCF, 0x8E);

        public TrainingMetricCurve() { Focusable = false; ClipToBounds = true; }
        public IReadOnlyList<TrainingEpochMetric> Metrics { get => (IReadOnlyList<TrainingEpochMetric>)GetValue(MetricsProperty); set => SetValue(MetricsProperty, value); }
        public int Epochs { get => (int)GetValue(EpochsProperty); set => SetValue(EpochsProperty, value); }
        public bool MetricOnly { get => (bool)GetValue(MetricOnlyProperty); set => SetValue(MetricOnlyProperty, value); }
        public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth <= 40 || ActualHeight <= 40) return;
            TrainingEpochMetric[] samples = (Metrics ?? Array.Empty<TrainingEpochMetric>()).Where(metric => metric != null)
                .Select(metric => metric.Normalize(Math.Max(1, Epochs))).Where(metric => metric != null).GroupBy(metric => metric.Epoch).Select(group => group.Last()).OrderBy(metric => metric.Epoch).ToArray();
            string key = samples.LastOrDefault(metric => metric.ValidationMetricValue.HasValue)?.ValidationMetricKey ?? string.Empty;
            double? Value(TrainingEpochMetric metric) => metric.ValidationMetricKey == key ? metric.ValidationMetricValue : null;
            bool hasValues = MetricOnly ? samples.Any(metric => Value(metric).HasValue) : samples.Any(metric => metric.TrainLoss.HasValue || metric.ValidationLoss.HasValue);
            if (!hasValues)
            {
                var empty = new FormattedText(EmptyText ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize,
                    Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = ActualWidth - 40, MaxTextHeight = ActualHeight - 40 };
                drawingContext.DrawText(empty, new Point(Math.Max(20, (ActualWidth - empty.Width) / 2), Math.Max(20, (ActualHeight - empty.Height) / 2)));
                return;
            }
            double maximum = MetricOnly ? 1 : Math.Max(1, samples.SelectMany(metric => new[] { metric.TrainLoss, metric.ValidationLoss }).Where(value => value.HasValue).Select(value => value.Value).DefaultIfEmpty(1).Max());
            if (MetricOnly) DrawSeries(Value, MetricPen);
            else { DrawSeries(metric => metric.TrainLoss, TrainPen); DrawSeries(metric => metric.ValidationLoss, ValidationPen); }

            void DrawSeries(Func<TrainingEpochMetric, double?> select, Pen pen)
            {
                Point? previous = null; int previousEpoch = 0;
                foreach (TrainingEpochMetric metric in samples)
                {
                    double? value = select(metric);
                    if (!value.HasValue) { previous = null; continue; }
                    var point = new Point(20 + (metric.Epoch - 1D) / Math.Max(1, Epochs - 1) * (ActualWidth - 40), 20 + (1 - value.Value / maximum) * (ActualHeight - 40));
                    if (previous.HasValue && metric.Epoch == previousEpoch + 1) drawingContext.DrawLine(pen, previous.Value, point);
                    drawingContext.DrawEllipse(pen.Brush, null, point, 2, 2);
                    previous = point; previousEpoch = metric.Epoch;
                }
            }
        }

        private static Pen CreatePen(byte red, byte green, byte blue)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(red, green, blue)), 2); pen.Freeze(); return pen;
        }
    }
}
