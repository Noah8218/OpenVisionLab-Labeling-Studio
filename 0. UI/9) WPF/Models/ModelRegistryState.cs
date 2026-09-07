using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Transient Model Registry panel state projected from presentation data.
    /// The Shell applies this snapshot to observable properties and keeps
    /// command enablement tied to the current workflow state.
    /// </summary>
    public class ModelRegistryState
    {
        public string SummaryPrimaryText { get; set; } = string.Empty;

        public string SummarySecondaryText { get; set; } = string.Empty;

        public string ProfileText { get; set; } = string.Empty;

        public string TrainingRunText { get; set; } = string.Empty;

        public string CandidateModelText { get; set; } = string.Empty;

        public string InspectionModelText { get; set; } = string.Empty;

        public string ActionText { get; set; } = string.Empty;

        public string HistoryHeaderText { get; set; } = string.Empty;

        public string HistorySummaryText { get; set; } = string.Empty;

        public bool IsHistoryVisible { get; set; }

        public IReadOnlyList<WpfModelRegistryHistoryItem> HistoryItems { get; set; } = Array.Empty<WpfModelRegistryHistoryItem>();

        public WpfModelRegistryHistoryItem SelectedHistoryItem { get; set; }

        public WpfModelRegistryHistorySelectionPresentation SelectedHistoryPresentation { get; set; }
    }
}
