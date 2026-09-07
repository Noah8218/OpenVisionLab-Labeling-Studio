using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public sealed class WpfModelRegistryHistoryItem
    {
        public string CandidateId { get; set; } = string.Empty;

        public string ProfileId { get; set; } = string.Empty;

        public string TrainingRunId { get; set; } = string.Empty;

        public string WeightsPath { get; set; } = string.Empty;

        public string BaselineWeightsPath { get; set; } = string.Empty;

        public string KindText { get; set; } = string.Empty;

        public string TitleText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string MetricText { get; set; } = string.Empty;

        public string DecisionText { get; set; } = string.Empty;

        public bool IsCurrentInspectionModel { get; set; }

        public bool CanPromoteToInspectionModel { get; set; }

        public string ActionText { get; set; } = string.Empty;

        public string ActionToolTip { get; set; } = string.Empty;
    }

    public sealed class WpfModelRegistryPresentation
    {
        public string ProfileText { get; set; } = string.Empty;

        public string TrainingRunText { get; set; } = string.Empty;

        public string CandidateModelText { get; set; } = string.Empty;

        public string InspectionModelText { get; set; } = string.Empty;

        public string ActionText { get; set; } = string.Empty;

        public string SummaryPrimaryText { get; set; } = string.Empty;

        public string SummarySecondaryText { get; set; } = string.Empty;

        public IReadOnlyList<WpfModelRegistryHistoryItem> HistoryItems { get; set; } = Array.Empty<WpfModelRegistryHistoryItem>();
    }

    /// <summary>
    /// Pure projection for the selected model-history row. The ViewModel keeps
    /// the observable selection and command enablement; this value carries only
    /// the text and comparison state derived from a history snapshot.
    /// </summary>
    public sealed class WpfModelRegistryHistorySelectionPresentation
    {
        public bool IsVisible { get; set; }

        public string TitleText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string MetricText { get; set; } = string.Empty;

        public string DecisionText { get; set; } = string.Empty;

        public string ComparisonTitleText { get; set; } = string.Empty;

        public string CurrentModelText { get; set; } = string.Empty;

        public string SelectedModelText { get; set; } = string.Empty;

        public string ComparisonMetricText { get; set; } = string.Empty;

        public string ActionText { get; set; } = string.Empty;

        public string ActionToolTip { get; set; } = string.Empty;

        public bool CanPromoteToInspectionModel { get; set; }
    }
}
