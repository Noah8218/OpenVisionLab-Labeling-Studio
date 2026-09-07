using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MvcVisionSystem
{
    internal static class ModelBenchmarkValueFormatter
    {
        public static string FormatPercent(double? value)
            => value?.ToString("P1", CultureInfo.CurrentCulture) ?? "-";
    }

    public sealed class WpfModelBenchmarkDashboardPointViewModel
    {
        public WpfModelBenchmarkDashboardPointViewModel(
            string runId,
            string displayName,
            WpfModelBenchmarkMetric metric,
            double taktMs,
            bool isBaseline)
        {
            RunId = runId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            MetricName = metric?.DisplayName ?? string.Empty;
            QualityValue = metric?.Value ?? 0D;
            IsPercentMetric = metric?.IsPercent == true;
            TaktMs = taktMs;
            IsBaseline = isBaseline;
        }

        public string RunId { get; }
        public string DisplayName { get; }
        public string MetricName { get; }
        public double QualityValue { get; }
        public bool IsPercentMetric { get; }
        public double TaktMs { get; }
        public bool IsBaseline { get; }
        public string QualityText => IsPercentMetric
            ? QualityValue.ToString("P1", CultureInfo.CurrentCulture)
            : QualityValue.ToString("0.##", CultureInfo.CurrentCulture);
        public string TaktText => TaktMs.ToString("0.00", CultureInfo.CurrentCulture) + " ms";
        public string LegendText => (IsBaseline ? "\uAE30\uC900 \u00B7 " : string.Empty) + DisplayName;
        public string ToolTipText => $"{DisplayName}\n{MetricName}: {QualityText}\nTakt: {TaktText}";
    }

    public sealed class WpfModelBenchmarkDashboardOutcomeRowViewModel
    {
        public WpfModelBenchmarkDashboardOutcomeRowViewModel(
            string displayName,
            int truePositiveCount,
            int falsePositiveCount,
            int falseNegativeCount,
            string truePositiveLabel = "TP",
            string falsePositiveLabel = "FP",
            string falseNegativeLabel = "FN")
        {
            DisplayName = displayName ?? string.Empty;
            TruePositiveCount = Math.Max(0, truePositiveCount);
            FalsePositiveCount = Math.Max(0, falsePositiveCount);
            FalseNegativeCount = Math.Max(0, falseNegativeCount);
            int total = Math.Max(1, TruePositiveCount + FalsePositiveCount + FalseNegativeCount);
            TruePositivePercent = TruePositiveCount * 100D / total;
            FalsePositivePercent = FalsePositiveCount * 100D / total;
            FalseNegativePercent = FalseNegativeCount * 100D / total;
            TruePositiveLabel = truePositiveLabel ?? "TP";
            FalsePositiveLabel = falsePositiveLabel ?? "FP";
            FalseNegativeLabel = falseNegativeLabel ?? "FN";
        }

        public string DisplayName { get; }
        public int TruePositiveCount { get; }
        public int FalsePositiveCount { get; }
        public int FalseNegativeCount { get; }
        public double TruePositivePercent { get; }
        public double FalsePositivePercent { get; }
        public double FalseNegativePercent { get; }
        public string TruePositiveLabel { get; }
        public string FalsePositiveLabel { get; }
        public string FalseNegativeLabel { get; }
        public string TruePositiveText => TruePositiveLabel + " " + TruePositiveCount.ToString(CultureInfo.CurrentCulture);
        public string FalsePositiveText => FalsePositiveLabel + " " + FalsePositiveCount.ToString(CultureInfo.CurrentCulture);
        public string FalseNegativeText => FalseNegativeLabel + " " + FalseNegativeCount.ToString(CultureInfo.CurrentCulture);
    }

    public sealed class WpfModelBenchmarkRunItemViewModel : WpfObservableViewModel
    {
        private readonly Func<WpfModelBenchmarkRunItemViewModel, bool, bool> selectionGuard;
        private readonly Action selectionChanged;
        private bool isSelected;
        private bool isBaseline;

        public WpfModelBenchmarkRunItemViewModel(
            WpfModelBenchmarkRun run,
            Func<WpfModelBenchmarkRunItemViewModel, bool, bool> selectionGuard,
            Action selectionChanged)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            this.selectionGuard = selectionGuard;
            this.selectionChanged = selectionChanged;
        }

        public WpfModelBenchmarkRun Run { get; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                {
                    return;
                }

                if (selectionGuard?.Invoke(this, value) == false)
                {
                    OnPropertyChanged(nameof(IsSelected));
                    return;
                }

                if (SetProperty(ref isSelected, value))
                {
                    selectionChanged?.Invoke();
                }
            }
        }

        public bool IsBaseline
        {
            get => isBaseline;
            private set => SetProperty(ref isBaseline, value);
        }

        public string DisplayName => Run.DisplayName;
        public string TaskText => Run.TaskText;
        public string RuntimeName => Run.RuntimeName;
        public string SourceTypeText => Run.SourceTypeText;
        public string DateText => Run.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        public string EvidenceText => $"{Run.Split} \u00B7 {Run.EvidenceCount}\uC7A5";
        public string SearchText => string.Join(" ", Run.DisplayName, Run.ModelName, Run.RuntimeName, Run.TaskText, Run.SourcePath);

        internal void SetSelected(bool selected)
        {
            SetProperty(ref isSelected, selected, nameof(IsSelected));
        }

        internal void SetBaseline(bool baseline)
        {
            IsBaseline = baseline;
        }
    }

    public sealed class WpfModelBenchmarkSelectedRunViewModel
    {
        public WpfModelBenchmarkSelectedRunViewModel(
            WpfModelBenchmarkRun run,
            WpfModelBenchmarkRun baseline,
            bool isBaseline)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            IsBaseline = isBaseline;
            BaselineText = isBaseline ? "\uAE30\uC900" : string.Empty;
            DeltaText = BuildDeltaText(run, baseline, isBaseline);
        }

        public WpfModelBenchmarkRun Run { get; }
        public bool IsBaseline { get; }
        public string BaselineText { get; }
        public string DisplayName => Run.DisplayName;
        public string ModelName => Run.ModelName;
        public string RuntimeName => Run.RuntimeName;
        public string TaskText => Run.TaskText;
        public string DecisionText => Run.DecisionText;
        public string QualityText => BuildQualityText(Run);
        public string TaktText => BuildTaktText(Run);
        public string EvidenceText => $"{Run.Split} \u00B7 {Run.EvidenceCount}\uC7A5";
        public string DeltaText { get; }
        public string SourceTypeText => Run.SourceTypeText;
        public string DateText => Run.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        public string WeightsPath => Run.WeightsPath;
        public string WeightsSha256 => Run.WeightsSha256;
        public string WeightsSha256Text => FormatSha256(Run.WeightsSha256);
        public string WeightsSha256SummaryText => "SHA " + WeightsSha256Text;
        public string EvaluationDataPath => Run.EvaluationDataPath;
        public string EvidenceFingerprintSha256 => Run.EvidenceFingerprintSha256;
        public string EvidenceFingerprintText => FormatSha256(Run.EvidenceFingerprintSha256);
        public string EvidenceFingerprintSummaryText => "SHA " + EvidenceFingerprintText;
        public string SourcePath => Run.SourcePath;
        public string ImageSizeText => Run.ImageSize > 0 ? Run.ImageSize.ToString(CultureInfo.CurrentCulture) : "-";
        public string BatchSizeText => Run.BatchSize > 0 ? Run.BatchSize.ToString(CultureInfo.CurrentCulture) : "-";
        public string ConfidenceText => Run.Confidence.HasValue ? Run.Confidence.Value.ToString("P0", CultureInfo.CurrentCulture) : "-";
        public string TimingProtocolText => string.IsNullOrWhiteSpace(Run.TimingSource)
            ? "\uCE21\uC815 \uC5C6\uC74C"
            : Run.TimingSource + (Run.TimingRepeatCount > 0 ? $" \u00B7 n={Run.TimingRepeatCount}" : string.Empty);

        private static string FormatSha256(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "\uAE30\uB85D \uC5C6\uC74C"
                : value.Substring(0, Math.Min(12, value.Length));
        }

        private static string BuildQualityText(WpfModelBenchmarkRun run)
        {
            foreach (string key in new[] { "map5095", "accuracy", "map50", "precision" })
            {
                WpfModelBenchmarkMetric metric = run.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                if (metric != null)
                {
                    return metric.DisplayName + " " + metric.FormatValue();
                }
            }

            return "-";
        }

        private static string BuildTaktText(WpfModelBenchmarkRun run)
        {
            if (!run.TaktMs.HasValue)
            {
                return "\uCE21\uC815 \uC5C6\uC74C";
            }

            string text = run.TaktMs.Value.ToString("0.00", CultureInfo.CurrentCulture) + " ms";
            if (run.TaktMinMs.HasValue && run.TaktMaxMs.HasValue && run.TimingRepeatCount > 1)
            {
                text += string.Format(
                    CultureInfo.CurrentCulture,
                    " ({0:0.00}-{1:0.00}, n={2})",
                    run.TaktMinMs.Value,
                    run.TaktMaxMs.Value,
                    run.TimingRepeatCount);
            }

            return text;
        }

        private static string BuildDeltaText(WpfModelBenchmarkRun run, WpfModelBenchmarkRun baseline, bool isBaseline)
        {
            if (isBaseline)
            {
                return "\uAE30\uC900 \uC2E4\uD589";
            }

            if (baseline == null || !string.Equals(run.TaskKey, baseline.TaskKey, StringComparison.OrdinalIgnoreCase))
            {
                return "\uC791\uC5C5 \uB2E4\uB984";
            }

            if (!WpfModelBenchmarkViewModel.AreQualityComparable(run, baseline))
            {
                return "\uD3C9\uAC00 \uC870\uAC74 \uB2E4\uB984";
            }

            var parts = new List<string>();
            foreach (string key in new[] { "map5095", "accuracy", "map50", "precision" })
            {
                WpfModelBenchmarkMetric metric = run.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                WpfModelBenchmarkMetric baselineMetric = baseline.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                if (metric != null && baselineMetric != null && metric.SupportsDelta)
                {
                    parts.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} {1:+0.0;-0.0;0.0}%p",
                        metric.DisplayName,
                        (metric.Value - baselineMetric.Value) * 100D));
                    break;
                }
            }

            if (WpfModelBenchmarkViewModel.AreTimingComparable(run, baseline))
            {
                parts.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "Takt {0:+0.00;-0.00;0.00} ms",
                    run.TaktMs.Value - baseline.TaktMs.Value));
            }

            return parts.Count > 0 ? string.Join(" / ", parts) : "\uACF5\uD1B5 \uC9C0\uD45C \uC5C6\uC74C";
        }
    }

    public sealed class WpfModelBenchmarkMetricRowViewModel
    {
        public WpfModelBenchmarkMetricRowViewModel(
            string displayName,
            IReadOnlyList<WpfModelBenchmarkMetricCellViewModel> values)
        {
            DisplayName = displayName ?? string.Empty;
            Values = values ?? Array.Empty<WpfModelBenchmarkMetricCellViewModel>();
        }

        public string DisplayName { get; }
        public IReadOnlyList<WpfModelBenchmarkMetricCellViewModel> Values { get; }
    }

    public sealed class WpfModelBenchmarkMetricCellViewModel
    {
        public WpfModelBenchmarkMetricCellViewModel(string valueText, string deltaText, bool isBaseline)
        {
            ValueText = valueText ?? string.Empty;
            DeltaText = deltaText ?? string.Empty;
            IsBaseline = isBaseline;
        }

        public string ValueText { get; }
        public string DeltaText { get; }
        public bool IsBaseline { get; }
    }

    public sealed class WpfModelBenchmarkClassMetricRowViewModel
    {
        public WpfModelBenchmarkClassMetricRowViewModel(
            WpfModelBenchmarkRun run,
            int classId,
            WpfModelBenchmarkClassMetric metric,
            WpfModelBenchmarkGroundTruthClassReview review)
        {
            ModelName = run?.DisplayName ?? string.Empty;
            ClassName = !string.IsNullOrWhiteSpace(metric?.ClassName)
                ? metric.ClassName
                : !string.IsNullOrWhiteSpace(review?.ClassName) ? review.ClassName : classId.ToString(CultureInfo.CurrentCulture);
            InstanceCountText = metric?.InstanceCount?.ToString(CultureInfo.CurrentCulture)
                ?? review?.GroundTruthCount.ToString(CultureInfo.CurrentCulture)
                ?? "-";
            PrecisionText = ModelBenchmarkValueFormatter.FormatPercent(metric?.Precision);
            RecallText = ModelBenchmarkValueFormatter.FormatPercent(metric?.Recall);
            Map50Text = ModelBenchmarkValueFormatter.FormatPercent(metric?.Map50);
            Map5095Text = ModelBenchmarkValueFormatter.FormatPercent(metric?.Map5095);
            GroundTruthReviewText = review == null
                ? "-"
                : $"TP {review.TruePositiveCount} · FP {review.FalsePositiveCount} · FN {review.FalseNegativeCount}";
        }

        public string ModelName { get; }
        public string ClassName { get; }
        public string InstanceCountText { get; }
        public string PrecisionText { get; }
        public string RecallText { get; }
        public string Map50Text { get; }
        public string Map5095Text { get; }
        public string GroundTruthReviewText { get; }

    }

    public sealed class WpfModelBenchmarkThresholdReviewRowViewModel
    {
        public WpfModelBenchmarkThresholdReviewRowViewModel(
            WpfModelBenchmarkRun run,
            WpfModelBenchmarkGroundTruthReview review,
            WpfModelBenchmarkThresholdReview threshold)
        {
            ModelName = run?.DisplayName ?? string.Empty;
            ConfidenceText = threshold?.Confidence.ToString("P0", CultureInfo.CurrentCulture) ?? "-";
            GroundTruthCountText = threshold?.GroundTruthCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            PredictionCountText = threshold?.PredictionCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            TruePositiveCountText = threshold?.TruePositiveCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            FalsePositiveCountText = threshold?.FalsePositiveCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            FalseNegativeCountText = threshold?.FalseNegativeCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            PrecisionText = ModelBenchmarkValueFormatter.FormatPercent(threshold?.Precision);
            RecallText = ModelBenchmarkValueFormatter.FormatPercent(threshold?.Recall);
            F1Text = ModelBenchmarkValueFormatter.FormatPercent(threshold?.F1);
            EvidenceText = string.IsNullOrWhiteSpace(review?.GeometryCoordinateSystem)
                ? "v" + Math.Max(1, review?.SchemaVersion ?? 1).ToString(CultureInfo.CurrentCulture)
                : "v" + Math.Max(1, review.SchemaVersion).ToString(CultureInfo.CurrentCulture) + " / " + review.GeometryCoordinateSystem;
        }

        public string ModelName { get; }
        public string ConfidenceText { get; }
        public string GroundTruthCountText { get; }
        public string PredictionCountText { get; }
        public string TruePositiveCountText { get; }
        public string FalsePositiveCountText { get; }
        public string FalseNegativeCountText { get; }
        public string PrecisionText { get; }
        public string RecallText { get; }
        public string F1Text { get; }
        public string EvidenceText { get; }

    }

    public sealed class WpfModelBenchmarkGroundTruthExampleViewModel
    {
        private ImageSource previewSource;
        private bool previewLoadAttempted;

        public WpfModelBenchmarkGroundTruthExampleViewModel(string modelName, WpfModelBenchmarkGroundTruthExample example)
        {
            ModelName = modelName ?? string.Empty;
            ImagePath = example?.ImagePath ?? string.Empty;
            ImageName = !string.IsNullOrWhiteSpace(example?.ImageName)
                ? example.ImageName
                : Path.GetFileName(ImagePath);
            ErrorTypeText = string.Equals(example?.ErrorType, "false-negative", StringComparison.OrdinalIgnoreCase)
                ? "\uBBF8\uAC80\uCD9C(FN)"
                : string.Equals(example?.ErrorType, "false-positive", StringComparison.OrdinalIgnoreCase)
                    ? "\uC624\uAC80\uCD9C(FP)"
                    : string.Equals(example?.ErrorType, "correct", StringComparison.OrdinalIgnoreCase)
                        ? "\uC815\uB2F5"
                        : string.Equals(example?.ErrorType, "low-confidence", StringComparison.OrdinalIgnoreCase)
                            ? "\uC784\uACC4\uAC12 \uBBF8\uB2EC"
                        : string.Equals(example?.ErrorType, "incorrect", StringComparison.OrdinalIgnoreCase)
                            ? "\uC624\uB958"
                            : example?.ErrorType ?? string.Empty;
            ClassName = example?.ClassName ?? string.Empty;
            ConfidenceText = example?.Confidence?.ToString("P1", CultureInfo.CurrentCulture) ?? "-";
            BestIouText = example?.BestIou?.ToString("P1", CultureInfo.CurrentCulture) ?? "-";
            PredictionBox = example?.PredictionBox;
            GroundTruthBox = example?.GroundTruthBox;
            DetailText = example?.DetailText ?? string.Empty;
            EvidencePath = example?.EvidencePath ?? string.Empty;
        }

        public string ModelName { get; }
        public string ImagePath { get; }
        public string ImageName { get; }
        public string ErrorTypeText { get; }
        public string ClassName { get; }
        public string ConfidenceText { get; }
        public string BestIouText { get; }
        public WpfModelBenchmarkNormalizedBox PredictionBox { get; }
        public WpfModelBenchmarkNormalizedBox GroundTruthBox { get; }
        public string DetailText { get; }
        public string EvidencePath { get; }
        public bool HasGroundTruthBoxOverlay => IsRenderableBox(GroundTruthBox);
        public bool HasPredictionBoxOverlay => IsRenderableBox(PredictionBox);
        public bool HasOverlay => HasGroundTruthBoxOverlay || HasPredictionBoxOverlay;

        public ImageSource PreviewSource
        {
            get
            {
                if (!previewLoadAttempted)
                {
                    previewLoadAttempted = true;
                    previewSource = CreatePreviewSource(ImagePath, GroundTruthBox, PredictionBox);
                }

                return previewSource;
            }
        }

        private static ImageSource CreatePreviewSource(
            string imagePath,
            WpfModelBenchmarkNormalizedBox groundTruthBox,
            WpfModelBenchmarkNormalizedBox predictionBox)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.DecodePixelWidth = 640;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();
                if (!IsRenderableBox(groundTruthBox) && !IsRenderableBox(predictionBox))
                {
                    return bitmap;
                }

                var drawing = new DrawingGroup();
                drawing.Children.Add(new ImageDrawing(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)));
                DrawNormalizedBox(drawing, groundTruthBox, bitmap.PixelWidth, bitmap.PixelHeight, System.Windows.Media.Color.FromRgb(57, 217, 138), null);
                DrawNormalizedBox(drawing, predictionBox, bitmap.PixelWidth, bitmap.PixelHeight, System.Windows.Media.Color.FromRgb(50, 184, 255), DashStyles.Dash);
                drawing.Freeze();

                var preview = new DrawingImage(drawing);
                preview.Freeze();
                return preview;
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is NotSupportedException
                || ex is InvalidOperationException
                || ex is ArgumentException)
            {
                return null;
            }
        }

        private static bool IsRenderableBox(WpfModelBenchmarkNormalizedBox box)
        {
            return box != null
                && IsUnitInterval(box.XMin)
                && IsUnitInterval(box.YMin)
                && IsUnitInterval(box.XMax)
                && IsUnitInterval(box.YMax)
                && box.XMax > box.XMin
                && box.YMax > box.YMin;
        }

        private static bool IsUnitInterval(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0.0
                && value <= 1.0;
        }

        private static void DrawNormalizedBox(
            DrawingGroup drawing,
            WpfModelBenchmarkNormalizedBox box,
            double imageWidth,
            double imageHeight,
            System.Windows.Media.Color color,
            DashStyle dashStyle)
        {
            if (!IsRenderableBox(box))
            {
                return;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var pen = new System.Windows.Media.Pen(brush, 2.5);
            if (dashStyle != null)
            {
                pen.DashStyle = dashStyle;
            }

            pen.Freeze();
            double x = box.XMin * imageWidth;
            double y = box.YMin * imageHeight;
            double width = (box.XMax - box.XMin) * imageWidth;
            double height = (box.YMax - box.YMin) * imageHeight;
            drawing.Children.Add(new GeometryDrawing(null, pen, new RectangleGeometry(new Rect(x, y, width, height))));
        }
    }
}
