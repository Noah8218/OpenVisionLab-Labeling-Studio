using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MvcVisionSystem.Yolo
{
    public sealed class AnomalyClassificationEvaluationSample
    {
        public string ImagePath { get; set; } = string.Empty;

        public string ExpectedClassName { get; set; } = string.Empty;

        public string PredictedClassName { get; set; } = string.Empty;

        public double Confidence { get; set; }
    }

    public sealed class AnomalyClassificationEvaluationOptions
    {
        public int MinimumTotalImageCount { get; set; } = 10;

        public int MinimumPerClassImageCount { get; set; } = 5;

        public double MinimumAccuracy { get; set; } = 0.9D;

        public double MinimumPerClassAccuracy { get; set; } = 0.8D;

        public double MinimumConfidence { get; set; } = 0D;
    }

    public sealed class AnomalyClassificationEvaluationReport
    {
        public int TotalImageCount { get; set; }

        public int NormalImageCount { get; set; }

        public int AbnormalImageCount { get; set; }

        public int CorrectImageCount { get; set; }

        public int NormalCorrectCount { get; set; }

        public int AbnormalCorrectCount { get; set; }

        public int LowConfidenceClassMatchCount { get; set; }

        public double Accuracy { get; set; }

        public double NormalAccuracy { get; set; }

        public double AbnormalAccuracy { get; set; }

        public IReadOnlyList<string> HoldReasons { get; set; } = Array.Empty<string>();

        public string Recommendation { get; set; } = string.Empty;

        public bool IsAdoptionCandidate
            => HoldReasons.Count == 0
                && string.Equals(Recommendation, "adopt", StringComparison.OrdinalIgnoreCase);
    }

    public static class AnomalyClassificationEvaluationService
    {
        public const string NormalClassName = "normal";
        public const string AbnormalClassName = "abnormal";

        public static AnomalyClassificationEvaluationReport ReadSummaryFile(string summaryPath)
        {
            if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
            {
                throw new FileNotFoundException("Anomaly classification evaluation summary was not found.", summaryPath);
            }

            return ParseSummaryJson(File.ReadAllText(summaryPath));
        }

        public static AnomalyClassificationEvaluationReport ParseSummaryJson(string summaryJson)
        {
            if (string.IsNullOrWhiteSpace(summaryJson))
            {
                return new AnomalyClassificationEvaluationReport();
            }

            using JsonDocument document = JsonDocument.Parse(summaryJson);
            JsonElement root = document.RootElement;
            JsonElement metrics = TryGetProperty(root, "metrics");
            JsonElement promotion = TryGetProperty(root, "promotion");

            return new AnomalyClassificationEvaluationReport
            {
                TotalImageCount = ReadInt(metrics, "totalImageCount"),
                NormalImageCount = ReadInt(metrics, "normalImageCount"),
                AbnormalImageCount = ReadInt(metrics, "abnormalImageCount"),
                CorrectImageCount = ReadInt(metrics, "correctImageCount"),
                NormalCorrectCount = ReadInt(metrics, "normalCorrectCount"),
                AbnormalCorrectCount = ReadInt(metrics, "abnormalCorrectCount"),
                LowConfidenceClassMatchCount = ReadInt(metrics, "lowConfidenceClassMatchCount"),
                Accuracy = ReadDouble(metrics, "accuracy"),
                NormalAccuracy = ReadDouble(metrics, "normalAccuracy"),
                AbnormalAccuracy = ReadDouble(metrics, "abnormalAccuracy"),
                Recommendation = ReadString(promotion, "recommendation"),
                HoldReasons = ReadStringArray(promotion, "reasons")
            };
        }

        public static AnomalyClassificationEvaluationReport Build(
            IEnumerable<AnomalyClassificationEvaluationSample> samples,
            AnomalyClassificationEvaluationOptions options = null)
        {
            options ??= new AnomalyClassificationEvaluationOptions();
            AnomalyClassificationEvaluationSample[] items = (samples ?? Enumerable.Empty<AnomalyClassificationEvaluationSample>())
                .Where(sample => sample != null)
                .ToArray();

            int totalCount = items.Length;
            int normalCount = CountExpected(items, NormalClassName);
            int abnormalCount = CountExpected(items, AbnormalClassName);
            int normalCorrect = CountCorrect(items, NormalClassName, options.MinimumConfidence);
            int abnormalCorrect = CountCorrect(items, AbnormalClassName, options.MinimumConfidence);
            int correctCount = normalCorrect + abnormalCorrect;
            int lowConfidenceClassMatchCount = CountLowConfidenceClassMatches(items, options.MinimumConfidence);

            double accuracy = SafeRatio(correctCount, totalCount);
            double normalAccuracy = SafeRatio(normalCorrect, normalCount);
            double abnormalAccuracy = SafeRatio(abnormalCorrect, abnormalCount);
            var holdReasons = new List<string>();

            if (totalCount < Math.Max(1, options.MinimumTotalImageCount))
            {
                holdReasons.Add($"Evaluation uses {totalCount} images; collect at least {options.MinimumTotalImageCount} held-out images.");
            }

            if (normalCount < Math.Max(1, options.MinimumPerClassImageCount))
            {
                holdReasons.Add($"Evaluation uses {normalCount} normal images; collect at least {options.MinimumPerClassImageCount} normal held-out images.");
            }

            if (abnormalCount < Math.Max(1, options.MinimumPerClassImageCount))
            {
                holdReasons.Add($"Evaluation uses {abnormalCount} abnormal images; collect at least {options.MinimumPerClassImageCount} abnormal held-out images.");
            }

            if (accuracy < Clamp01(options.MinimumAccuracy))
            {
                holdReasons.Add($"Accuracy {FormatRatio(accuracy)} is below minimum {FormatRatio(options.MinimumAccuracy)}.");
            }

            if (lowConfidenceClassMatchCount > 0)
            {
                holdReasons.Add($"{lowConfidenceClassMatchCount} class-matching predictions were below minimum confidence {FormatRatio(options.MinimumConfidence)}.");
            }

            if (normalAccuracy < Clamp01(options.MinimumPerClassAccuracy))
            {
                holdReasons.Add($"Normal accuracy {FormatRatio(normalAccuracy)} is below minimum {FormatRatio(options.MinimumPerClassAccuracy)}.");
            }

            if (abnormalAccuracy < Clamp01(options.MinimumPerClassAccuracy))
            {
                holdReasons.Add($"Abnormal accuracy {FormatRatio(abnormalAccuracy)} is below minimum {FormatRatio(options.MinimumPerClassAccuracy)}.");
            }

            return new AnomalyClassificationEvaluationReport
            {
                TotalImageCount = totalCount,
                NormalImageCount = normalCount,
                AbnormalImageCount = abnormalCount,
                CorrectImageCount = correctCount,
                NormalCorrectCount = normalCorrect,
                AbnormalCorrectCount = abnormalCorrect,
                LowConfidenceClassMatchCount = lowConfidenceClassMatchCount,
                Accuracy = accuracy,
                NormalAccuracy = normalAccuracy,
                AbnormalAccuracy = abnormalAccuracy,
                Recommendation = holdReasons.Count == 0 ? "adopt" : "hold",
                HoldReasons = holdReasons
            };
        }

        private static int CountExpected(IEnumerable<AnomalyClassificationEvaluationSample> samples, string className)
            => samples.Count(sample => IsClass(sample.ExpectedClassName, className));

        private static JsonElement TryGetProperty(JsonElement element, string propertyName)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement value)
                ? value
                : default;

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
            {
                return 0;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : 0;
        }

        private static double ReadDouble(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
            {
                return 0D;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result) ? Clamp01(result) : 0D;
        }

        private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(propertyName, out JsonElement value)
                || value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
            {
                return string.Empty;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int CountCorrect(IEnumerable<AnomalyClassificationEvaluationSample> samples, string className, double minimumConfidence)
        {
            double threshold = Clamp01(minimumConfidence);
            return samples.Count(sample =>
                IsClass(sample.ExpectedClassName, className)
                && IsClass(sample.PredictedClassName, className)
                && sample.Confidence >= threshold);
        }

        private static int CountLowConfidenceClassMatches(IEnumerable<AnomalyClassificationEvaluationSample> samples, double minimumConfidence)
        {
            double threshold = Clamp01(minimumConfidence);
            if (threshold <= 0D)
            {
                return 0;
            }

            return samples.Count(sample =>
                IsClass(sample.ExpectedClassName, sample.PredictedClassName)
                && sample.Confidence < threshold);
        }

        private static bool IsClass(string value, string className)
            => string.Equals((value ?? string.Empty).Trim(), className, StringComparison.OrdinalIgnoreCase);

        private static double SafeRatio(int numerator, int denominator)
            => denominator <= 0 ? 0D : Math.Clamp((double)numerator / denominator, 0D, 1D);

        private static double Clamp01(double value)
            => double.IsNaN(value) ? 0D : Math.Clamp(value, 0D, 1D);

        private static string FormatRatio(double value)
            => Clamp01(value).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
