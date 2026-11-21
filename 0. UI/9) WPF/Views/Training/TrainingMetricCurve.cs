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
        // Finished-run view: validation metric curves of the other models trained on the same dataset version,
        // drawn in grey behind this run. The value axis then starts 0.2 below the lowest final value so close
        // models stay apart (v2 prototype runPeers()). The live view leaves this empty.
        public static readonly DependencyProperty PeersProperty = DependencyProperty.Register(nameof(Peers), typeof(IReadOnlyList<IReadOnlyList<TrainingEpochMetric>>), typeof(TrainingMetricCurve),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public IReadOnlyList<IReadOnlyList<TrainingEpochMetric>> Peers { get => (IReadOnlyList<IReadOnlyList<TrainingEpochMetric>>)GetValue(PeersProperty); set => SetValue(PeersProperty, value); }
        // The live view keeps the 0-1 axis with peers (prototype livePeerLines()): early epochs below a raised floor
        // would otherwise be drawn flat on the bottom edge while the running model is still low.
        public static readonly DependencyProperty PeerAxisFloorProperty = DependencyProperty.Register(nameof(PeerAxisFloor), typeof(bool), typeof(TrainingMetricCurve),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool PeerAxisFloor { get => (bool)GetValue(PeerAxisFloorProperty); set => SetValue(PeerAxisFloorProperty, value); }
        private static readonly Pen PeerPen = CreatePen(0x5B, 0x66, 0x78, 1.5);
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
            double maximum = MetricOnly ? 1 : LossAxisMaximum(samples.SelectMany(metric => new[] { metric.TrainLoss, metric.ValidationLoss }).Where(value => value.HasValue).Select(value => value.Value).ToArray());
            TrainingEpochMetric[][] peers = MetricOnly && Peers != null
                ? Peers.Where(series => series != null).Select(series => series.Where(metric => metric != null).Select(metric => metric.Normalize(Math.Max(1, Epochs))).Where(metric => metric != null && metric.ValidationMetricKey == key)
                    .GroupBy(metric => metric.Epoch).Select(group => group.Last()).OrderBy(metric => metric.Epoch).ToArray()).Where(series => series.Length > 0).ToArray()
                : Array.Empty<TrainingEpochMetric[]>();
            double minimum = 0;
            if (peers.Length > 0 && PeerAxisFloor)
            {
                double lowestFinal = peers.Select(series => series.Last(metric => metric.ValidationMetricValue.HasValue).ValidationMetricValue.Value)
                    .Append(samples.Last(metric => Value(metric).HasValue).ValidationMetricValue.Value).Min();
                minimum = Math.Max(0, Math.Floor((lowestFinal - 0.2) * 10) / 10);
            }
            // Plot area and axis values follow the v2 prototype chartAxes(): value axis at 0, half and
            // maximum with faint grid lines, epoch axis at 1 and the configured epoch count.
            const double left = 44, right = 20, top = 20, bottom = 20;
            double plotWidth = ActualWidth - left - right, plotHeight = ActualHeight - top - bottom;
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            FormattedText Label(string text, Brush brush, FontWeight? weight = null)
                => new(text, CultureInfo.CurrentCulture, FlowDirection, weight.HasValue ? new Typeface(FontFamily, FontStyle, weight.Value, FontStretch) : typeface, 11, brush, pixelsPerDip);
            // A value above the axis (an early validation spike) is pinned to the top edge and drawn as a triangle.
            Point At(int epoch, double value) => new(left + (epoch - 1D) / Math.Max(1, Epochs - 1) * plotWidth, top + (1 - (Math.Clamp(value, minimum, maximum) - minimum) / (maximum - minimum)) * plotHeight);
            foreach (double value in new[] { minimum, (minimum + maximum) / 2, maximum })
            {
                double y = top + (1 - (value - minimum) / (maximum - minimum)) * plotHeight;
                drawingContext.DrawLine(GridPen, new Point(left, y), new Point(left + plotWidth, y));
                FormattedText text = Label(value.ToString("0.00", CultureInfo.CurrentCulture), Foreground);
                drawingContext.DrawText(text, new Point(left - 6 - text.Width, y - text.Height / 2));
            }
            FormattedText first = Label("1", Foreground), last = Label(Math.Max(1, Epochs).ToString(CultureInfo.CurrentCulture), Foreground);
            drawingContext.DrawText(first, new Point(left - first.Width / 2, top + plotHeight + 3));
            drawingContext.DrawText(last, new Point(left + plotWidth - last.Width / 2, top + plotHeight + 3));

            if (MetricOnly)
            {
                foreach (TrainingEpochMetric[] series in peers)
                {
                    TrainingEpochMetric[] own = samples; samples = series; DrawSeries(Value, PeerPen); samples = own;
                }
                DrawSeries(Value, MetricPen); DrawLatest(Value, MetricPen, "0.000", below: true);
            }
            else
            {
                DrawSeries(metric => metric.TrainLoss, TrainPen); DrawSeries(metric => metric.ValidationLoss, ValidationPen);
                // The higher of the two latest losses gets its label above its point, the lower one below,
                // so neither label sits on the other curve.
                double? train = samples.LastOrDefault(metric => metric.TrainLoss.HasValue)?.TrainLoss, validation = samples.LastOrDefault(metric => metric.ValidationLoss.HasValue)?.ValidationLoss;
                bool trainHigher = !validation.HasValue || train >= validation;
                DrawLatest(metric => metric.TrainLoss, TrainPen, "0.00", below: !trainHigher); DrawLatest(metric => metric.ValidationLoss, ValidationPen, "0.00", below: trainHigher);
            }

            void DrawSeries(Func<TrainingEpochMetric, double?> select, Pen pen)
            {
                Point? previous = null; int previousEpoch = 0;
                foreach (TrainingEpochMetric metric in samples)
                {
                    double? value = select(metric);
                    if (!value.HasValue) { previous = null; continue; }
                    Point point = At(metric.Epoch, value.Value);
                    if (previous.HasValue && metric.Epoch == previousEpoch + 1) drawingContext.DrawLine(pen, previous.Value, point);
                    if (value.Value > maximum)
                    {
                        var marker = new StreamGeometry();
                        using (StreamGeometryContext geometry = marker.Open())
                        {
                            geometry.BeginFigure(new Point(point.X, point.Y - 1), true, true);
                            geometry.LineTo(new Point(point.X - 4, point.Y + 6), true, false);
                            geometry.LineTo(new Point(point.X + 4, point.Y + 6), true, false);
                        }
                        marker.Freeze();
                        drawingContext.DrawGeometry(pen.Brush, null, marker);
                    }
                    else drawingContext.DrawEllipse(pen.Brush, null, point, 2, 2);
                    previous = point; previousEpoch = metric.Epoch;
                }
            }

            // The latest value next to its point, above or below it as chosen by the caller.
            void DrawLatest(Func<TrainingEpochMetric, double?> select, Pen pen, string format, bool below)
            {
                TrainingEpochMetric latest = samples.LastOrDefault(metric => select(metric).HasValue);
                if (latest == null) return;
                Point point = At(latest.Epoch, select(latest).Value);
                FormattedText text = Label(select(latest).Value.ToString(format, CultureInfo.CurrentCulture), pen.Brush, FontWeights.SemiBold);
                double x = Math.Max(left, Math.Min(point.X, ActualWidth - right - 4) - text.Width);
                double y = below ? point.Y + 4 : point.Y - 4 - text.Height;
                drawingContext.DrawText(text, new Point(x, Math.Max(0, Math.Min(y, ActualHeight - text.Height))));
            }
        }

        private static readonly Pen GridPen = CreatePen(0x28, 0x30, 0x40, 1);

        // Same rule as the v2 prototype's lossAxisMax(): the largest loss, unless one value exceeds four times
        // the median, which happens when a validation loss spikes in the first epochs. Then the axis follows the
        // remaining values with 15% headroom so the trend stays readable; the spike is drawn pinned to the top.
        internal static double LossAxisMaximum(IReadOnlyList<double> values)
        {
            double[] finite = (values ?? Array.Empty<double>()).Where(value => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0).OrderBy(value => value).ToArray();
            if (finite.Length == 0) return 1;
            double largest = finite[^1];
            if (finite.Length < 4) return Math.Max(1, largest);
            double median = finite.Length % 2 == 1 ? finite[finite.Length / 2] : (finite[finite.Length / 2 - 1] + finite[finite.Length / 2]) / 2;
            double limit = median * 4;
            if (median <= 0 || largest <= limit) return Math.Max(1, largest);
            return Math.Max(1, finite.Where(value => value <= limit).Max() * 1.15);
        }

        private static Pen CreatePen(byte red, byte green, byte blue, double thickness = 2)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(red, green, blue)), thickness); pen.Freeze(); return pen;
        }
    }
}
