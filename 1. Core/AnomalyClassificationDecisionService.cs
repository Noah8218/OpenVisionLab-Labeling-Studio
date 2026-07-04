using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class AnomalyClassificationDecisionOptions
    {
        public IReadOnlyCollection<string> NormalClassNames { get; set; } = Array.Empty<string>();

        public IReadOnlyCollection<string> AbnormalClassNames { get; set; } = Array.Empty<string>();

        public double MinimumConfidence { get; set; } = 0D;
    }

    public sealed class AnomalyClassificationDecision
    {
        public bool IsMapped { get; set; }

        public AnomalyImageReviewState ReviewState { get; set; } = AnomalyImageReviewState.Unreviewed;

        public string ClassName { get; set; } = string.Empty;

        public double Confidence { get; set; }

        public string Reason { get; set; } = string.Empty;
    }

    public static class AnomalyClassificationDecisionService
    {
        public static AnomalyClassificationDecision Build(YoloWorkerSmokeCandidate candidate, AnomalyClassificationDecisionOptions options)
        {
            if (candidate == null)
            {
                return Unmapped("candidate is missing");
            }

            return BuildCore(
                candidate.ClassName,
                candidate.Confidence,
                candidate.ImageLevel,
                candidate.CandidateType,
                candidate.PredictionType,
                options);
        }

        public static AnomalyClassificationDecision Build(DefectInfo candidate, AnomalyClassificationDecisionOptions options)
        {
            if (candidate == null)
            {
                return Unmapped("candidate is missing");
            }

            return BuildCore(
                candidate.ClassName,
                candidate.Confidence,
                candidate.ImageLevel,
                candidate.CandidateType,
                candidate.PredictionType,
                options);
        }

        public static AnomalyClassificationDecision Build(IEnumerable<YoloWorkerSmokeCandidate> candidates, AnomalyClassificationDecisionOptions options)
        {
            List<AnomalyClassificationDecision> mapped = (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Select(candidate => Build(candidate, options))
                .Where(decision => decision.IsMapped)
                .ToList();

            return BuildAggregate(mapped);
        }

        public static AnomalyClassificationDecision Build(IEnumerable<DefectInfo> candidates, AnomalyClassificationDecisionOptions options)
        {
            List<AnomalyClassificationDecision> mapped = (candidates ?? Array.Empty<DefectInfo>())
                .Select(candidate => Build(candidate, options))
                .Where(decision => decision.IsMapped)
                .ToList();

            return BuildAggregate(mapped);
        }

        private static AnomalyClassificationDecision BuildCore(
            string className,
            double confidence,
            bool imageLevel,
            string candidateType,
            string predictionType,
            AnomalyClassificationDecisionOptions options)
        {
            string normalizedClassName = Normalize(className);
            if (string.IsNullOrWhiteSpace(normalizedClassName))
            {
                return Unmapped("class name is missing", className, confidence);
            }

            if (!IsImageLevelClassification(imageLevel, candidateType, predictionType))
            {
                return Unmapped("candidate is not an image-level classification", className, confidence);
            }

            options ??= new AnomalyClassificationDecisionOptions();
            if (confidence < Math.Max(0D, options.MinimumConfidence))
            {
                return Unmapped("classification confidence is below the configured threshold", className, confidence);
            }

            bool normal = ContainsClass(options.NormalClassNames, normalizedClassName);
            bool abnormal = ContainsClass(options.AbnormalClassNames, normalizedClassName);
            if (normal && abnormal)
            {
                return Unmapped("classification class is mapped to both normal and abnormal", className, confidence);
            }

            if (normal)
            {
                return Mapped(AnomalyImageReviewState.Normal, className, confidence);
            }

            if (abnormal)
            {
                return Mapped(AnomalyImageReviewState.Abnormal, className, confidence);
            }

            return Unmapped("classification class is not configured for anomaly review", className, confidence);
        }

        private static bool IsImageLevelClassification(bool imageLevel, string candidateType, string predictionType)
        {
            return imageLevel
                && (string.Equals(candidateType, "imageClassification", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(predictionType, "classification", StringComparison.OrdinalIgnoreCase));
        }

        private static AnomalyClassificationDecision BuildAggregate(IReadOnlyList<AnomalyClassificationDecision> mapped)
        {
            if (mapped == null || mapped.Count == 0)
            {
                return Unmapped("no configured image-level classification candidate was found");
            }

            AnomalyImageReviewState state = mapped[0].ReviewState;
            if (mapped.Any(item => item.ReviewState != state))
            {
                return Unmapped("classification candidates map to multiple anomaly review states");
            }

            return mapped[0];
        }

        private static bool ContainsClass(IEnumerable<string> classNames, string normalizedClassName)
        {
            return (classNames ?? Array.Empty<string>())
                .Select(Normalize)
                .Any(item => string.Equals(item, normalizedClassName, StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static AnomalyClassificationDecision Mapped(AnomalyImageReviewState state, string className, double confidence)
        {
            return new AnomalyClassificationDecision
            {
                IsMapped = true,
                ReviewState = state,
                ClassName = className ?? string.Empty,
                Confidence = confidence,
                Reason = "classification class matched configured anomaly review mapping"
            };
        }

        private static AnomalyClassificationDecision Unmapped(string reason, string className = "", double confidence = 0D)
        {
            return new AnomalyClassificationDecision
            {
                IsMapped = false,
                ReviewState = AnomalyImageReviewState.Unreviewed,
                ClassName = className ?? string.Empty,
                Confidence = confidence,
                Reason = reason ?? string.Empty
            };
        }
    }
}
