using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class WpfModelRegistryHistoryItem : WpfObservableViewModel
    {
        private string kindText = string.Empty, titleText = string.Empty, detailText = string.Empty, decisionText = string.Empty, actionText = string.Empty, actionToolTip = string.Empty;

        public string CandidateId { get; set; } = string.Empty;

        public string ProfileId { get; set; } = string.Empty;

        public string TrainingRunId { get; set; } = string.Empty;

        public string TrainingRunName { get; set; } = string.Empty;

        public string DatasetVersionId { get; set; } = string.Empty;

        public string DatasetContentSha256 { get; set; } = string.Empty;

        public string WeightsPath { get; set; } = string.Empty;

        public string ModelEngine { get; set; } = string.Empty;

        public string ProjectRootPath { get; set; } = string.Empty;

        public bool CanRunInferenceTrial { get; set; }

        public string WeightsSha256 { get; set; } = string.Empty;

        public string ArtifactPath { get; set; } = string.Empty;

        public string ArtifactStatus { get; set; } = string.Empty;

        public string BaselineWeightsPath { get; set; } = string.Empty;

        public string KindText { get => IsConfiguredModelFallback ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model") : kindText; set => kindText = value; }

        public string TitleText { get => IsConfiguredModelFallback ? $"{OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model")}: {ModelRegistryTextPresentationService.FormatModelPath(WeightsPath)}" : titleText; set => titleText = value; }
        public string InferenceTitleText => string.IsNullOrWhiteSpace(ModelEngine) ? TitleText : $"{ModelEngine} · {(!string.IsNullOrWhiteSpace(TrainingRunName) ? TrainingRunName : Path.GetFileName(WeightsPath))}";

        public string DetailText { get => IsConfiguredModelFallback ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Detail") : detailText; set => detailText = value; }

        public string MetricText { get; set; } = string.Empty;

        public string DecisionText { get => IsConfiguredModelFallback ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Detail") : decisionText; set => decisionText = value; }

        public bool IsCurrentInspectionModel { get; set; }

        public bool IsConfiguredModelFallback { get; set; }

        public string InferenceBadgeText => OpenVisionLab.OpenVisionLanguageService.T(IsConfiguredModelFallback ? "WpfModelRegistry.History.Configured.Model" : "WpfInference.InspectionBadge");

        public bool CanPromoteToInspectionModel { get; set; }

        public string ActionText { get => IsConfiguredModelFallback ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model") : actionText; set => actionText = value; }

        public string ActionToolTip { get => IsConfiguredModelFallback ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Detail") : actionToolTip; set => actionToolTip = value; }

        public void RefreshLocalizedPresentation()
        {
            OnPropertyChanged(nameof(InferenceBadgeText));
            if (!IsConfiguredModelFallback) return;
            foreach (string property in new[] { nameof(KindText), nameof(TitleText), nameof(InferenceTitleText), nameof(DetailText), nameof(DecisionText), nameof(ActionText), nameof(ActionToolTip) }) OnPropertyChanged(property);
        }
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

        internal Func<bool, bool, (string OriginCandidate, string OriginAction, string CandidateText, string ActionText)> DecisionTextSource { get; set; }

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
