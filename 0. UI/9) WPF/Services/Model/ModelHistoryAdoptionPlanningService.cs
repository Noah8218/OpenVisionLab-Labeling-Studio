using System;

namespace MvcVisionSystem
{
    public enum ModelHistoryAdoptionPlanStatus
    {
        MissingSelection,
        MissingWeightsPath,
        CandidateWeightsFileMissing,
        AlreadyCurrent,
        Ready
    }

    public class ModelHistoryAdoptionRequest
    {
        public bool HasSelection { get; set; }

        public string CandidateWeightsPath { get; set; } = string.Empty;

        public string CurrentWeightsPath { get; set; } = string.Empty;

        public string FallbackBaselineWeightsPath { get; set; } = string.Empty;

        public string MetricText { get; set; } = string.Empty;

        public string DecisionText { get; set; } = string.Empty;

        public string DatasetVersionId { get; set; } = string.Empty;

        public string DatasetContentSha256 { get; set; } = string.Empty;

        public bool CandidateWeightsFileExists { get; set; }
    }

    public class ModelHistoryAdoptionPlan
    {
        public ModelHistoryAdoptionPlan(
            ModelHistoryAdoptionPlanStatus status,
            string candidateWeightsPath = "",
            string baselineWeightsPath = "",
            string metricsSummary = "",
            string decisionSummary = "",
            string datasetVersionId = "",
            string datasetContentSha256 = "")
        {
            Status = status;
            CandidateWeightsPath = candidateWeightsPath ?? string.Empty;
            BaselineWeightsPath = baselineWeightsPath ?? string.Empty;
            MetricsSummary = metricsSummary ?? string.Empty;
            DecisionSummary = decisionSummary ?? string.Empty;
            DatasetVersionId = datasetVersionId?.Trim() ?? string.Empty;
            DatasetContentSha256 = datasetContentSha256?.Trim() ?? string.Empty;
        }

        public ModelHistoryAdoptionPlanStatus Status { get; }

        public string CandidateWeightsPath { get; }

        public string BaselineWeightsPath { get; }

        public string MetricsSummary { get; }

        public string DecisionSummary { get; }

        public string DatasetVersionId { get; }

        public string DatasetContentSha256 { get; }

        public bool IsReady => Status == ModelHistoryAdoptionPlanStatus.Ready;

        public bool IsAlreadyCurrent => Status == ModelHistoryAdoptionPlanStatus.AlreadyCurrent;
    }

    /// <summary>
    /// Prepares the explicit model-history adoption request without changing
    /// settings, registry history, or Recipe persistence.
    /// </summary>
    public static class ModelHistoryAdoptionPlanningService
    {
        public static ModelHistoryAdoptionPlan Build(ModelHistoryAdoptionRequest request)
        {
            request ??= new ModelHistoryAdoptionRequest();
            if (!request.HasSelection)
            {
                return new ModelHistoryAdoptionPlan(ModelHistoryAdoptionPlanStatus.MissingSelection);
            }

            string candidateWeightsPath = Normalize(request.CandidateWeightsPath);
            if (string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                return new ModelHistoryAdoptionPlan(ModelHistoryAdoptionPlanStatus.MissingWeightsPath);
            }

            if (!request.CandidateWeightsFileExists)
            {
                return new ModelHistoryAdoptionPlan(
                    ModelHistoryAdoptionPlanStatus.CandidateWeightsFileMissing,
                    candidateWeightsPath: candidateWeightsPath,
                    datasetVersionId: request.DatasetVersionId,
                    datasetContentSha256: request.DatasetContentSha256);
            }

            string currentWeightsPath = Normalize(request.CurrentWeightsPath);
            if (string.Equals(currentWeightsPath, candidateWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                return new ModelHistoryAdoptionPlan(
                    ModelHistoryAdoptionPlanStatus.AlreadyCurrent,
                    candidateWeightsPath: candidateWeightsPath,
                    datasetVersionId: request.DatasetVersionId,
                    datasetContentSha256: request.DatasetContentSha256);
            }

            string baselineWeightsPath = !string.IsNullOrWhiteSpace(currentWeightsPath)
                ? currentWeightsPath
                : Normalize(request.FallbackBaselineWeightsPath);
            string metricsSummary = !string.IsNullOrWhiteSpace(request.MetricText)
                ? request.MetricText.Trim()
                : Normalize(request.DecisionText);

            return new ModelHistoryAdoptionPlan(
                ModelHistoryAdoptionPlanStatus.Ready,
                candidateWeightsPath,
                baselineWeightsPath,
                metricsSummary,
                "모델 이력에서 검사 모델로 적용",
                request.DatasetVersionId,
                request.DatasetContentSha256);
        }

        private static string Normalize(string value)
            => value?.Trim() ?? string.Empty;
    }

    [Obsolete("Use ModelHistoryAdoptionPlanStatus.", false)]
    public enum WpfModelHistoryAdoptionPlanStatus
    {
        MissingSelection,
        MissingWeightsPath,
        CandidateWeightsFileMissing,
        AlreadyCurrent,
        Ready
    }

    [Obsolete("Use ModelHistoryAdoptionRequest.", false)]
    public sealed class WpfModelHistoryAdoptionRequest : ModelHistoryAdoptionRequest
    {
    }

    [Obsolete("Use ModelHistoryAdoptionPlan.", false)]
    public sealed class WpfModelHistoryAdoptionPlan : ModelHistoryAdoptionPlan
    {
        public WpfModelHistoryAdoptionPlan(
            WpfModelHistoryAdoptionPlanStatus status,
            string candidateWeightsPath = "",
            string baselineWeightsPath = "",
            string metricsSummary = "",
            string decisionSummary = "",
            string datasetVersionId = "",
            string datasetContentSha256 = "")
            : base(
                (ModelHistoryAdoptionPlanStatus)status,
                candidateWeightsPath,
                baselineWeightsPath,
                metricsSummary,
                decisionSummary,
                datasetVersionId,
                datasetContentSha256)
        {
        }

        public WpfModelHistoryAdoptionPlan(ModelHistoryAdoptionPlan source)
            : this(
                (WpfModelHistoryAdoptionPlanStatus)(source?.Status ?? ModelHistoryAdoptionPlanStatus.MissingSelection),
                source?.CandidateWeightsPath,
                source?.BaselineWeightsPath,
                source?.MetricsSummary,
                source?.DecisionSummary,
                source?.DatasetVersionId,
                source?.DatasetContentSha256)
        {
        }

        public new WpfModelHistoryAdoptionPlanStatus Status => (WpfModelHistoryAdoptionPlanStatus)base.Status;
    }

    [Obsolete("Use ModelHistoryAdoptionPlanningService.", false)]
    public static class WpfModelHistoryAdoptionPlanningService
    {
        public static WpfModelHistoryAdoptionPlan Build(WpfModelHistoryAdoptionRequest request)
            => new WpfModelHistoryAdoptionPlan(ModelHistoryAdoptionPlanningService.Build(request));
    }
}
