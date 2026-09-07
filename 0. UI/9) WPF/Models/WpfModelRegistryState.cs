using System;

namespace MvcVisionSystem
{
    [Obsolete("Use ModelRegistryState.", false)]
    public sealed class WpfModelRegistryState : ModelRegistryState
    {
        internal static WpfModelRegistryState FromCanonical(ModelRegistryState source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfModelRegistryState
            {
                SummaryPrimaryText = source.SummaryPrimaryText,
                SummarySecondaryText = source.SummarySecondaryText,
                ProfileText = source.ProfileText,
                TrainingRunText = source.TrainingRunText,
                CandidateModelText = source.CandidateModelText,
                InspectionModelText = source.InspectionModelText,
                ActionText = source.ActionText,
                HistoryHeaderText = source.HistoryHeaderText,
                HistorySummaryText = source.HistorySummaryText,
                IsHistoryVisible = source.IsHistoryVisible,
                HistoryItems = source.HistoryItems,
                SelectedHistoryItem = source.SelectedHistoryItem,
                SelectedHistoryPresentation = source.SelectedHistoryPresentation
            };
        }
    }
}
