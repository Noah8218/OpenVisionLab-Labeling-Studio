using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Composition-only adapter for Candidate Review. Candidate selection, model
    // comparison state, and review mutations remain owned by the existing ViewModel
    // and injected Shell workflow callbacks.
    internal sealed class CandidateReviewPanelCommandWiring
    {
        private readonly CandidateReviewPanelCommandWiringContext context;

        internal CandidateReviewPanelCommandWiring(CandidateReviewPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureCandidateReviewPanelCommands()
        {
            context.CandidateReviewViewModel.ConfigureCommands(
                context.ExecuteCandidateConfidenceChangedCommand,
                context.ExecuteConfirmSelectedCandidateCommand,
                context.ExecuteConfirmAllCandidatesCommand,
                context.ExecuteSkipSelectedCandidateCommand,
                context.ExecutePreviousCandidateCommand,
                context.ExecuteNextCandidateCommand,
                context.ExecuteFocusCandidateCommand,
                context.ExecuteFocusCurrentLabelCommand,
                candidateSelectionChanged: null,
                candidatePreviewKeyDown: null,
                context.ExecuteCompleteImageAndNextCommand,
                context.ExecuteOpenModelComparisonExampleCommand,
                context.ExecuteSaveModelCandidateCommand,
                context.ExecuteRejectModelCandidateCommand,
                modelComparisonHistorySelectionChanged: null,
                context.ExecuteTogglePatchCoreHeatmapCommand);
            context.CandidateReviewViewModel.ConfigureSelectionWorkflow(context.ApplyCandidateSelectionChangedEffects);
            context.CandidateReviewViewModel.ConfigureModelComparisonHistorySelectionWorkflow(context.ApplyModelComparisonHistorySelectionEffects);
            context.CandidateReviewViewModel.ConfigureCandidatePreviewKeyWorkflow();
            context.RefreshAttachedCommandBindings(
                context.CandidateConfidenceSlider,
                new[] { InputCommandBehaviors.ValueInputCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.CandidateListBox,
                new[]
                {
                    InputCommandBehaviors.SelectedItemChangedCommandProperty,
                    InputCommandBehaviors.PreviewKeyInputCommandProperty
                });
        }
    }

    internal sealed class CandidateReviewPanelCommandWiringContext
    {
        internal WpfCandidateReviewPanelViewModel CandidateReviewViewModel { get; init; }
        internal Action<double> ExecuteCandidateConfidenceChangedCommand { get; init; }
        internal Action ExecuteConfirmSelectedCandidateCommand { get; init; }
        internal Action ExecuteConfirmAllCandidatesCommand { get; init; }
        internal Action ExecuteSkipSelectedCandidateCommand { get; init; }
        internal Action ExecutePreviousCandidateCommand { get; init; }
        internal Action ExecuteNextCandidateCommand { get; init; }
        internal Action ExecuteFocusCandidateCommand { get; init; }
        internal Action ExecuteFocusCurrentLabelCommand { get; init; }
        internal Action ExecuteCompleteImageAndNextCommand { get; init; }
        internal Action<WpfModelComparisonReviewExample> ExecuteOpenModelComparisonExampleCommand { get; init; }
        internal Action ExecuteSaveModelCandidateCommand { get; init; }
        internal Action ExecuteRejectModelCandidateCommand { get; init; }
        internal Action ExecuteTogglePatchCoreHeatmapCommand { get; init; }
        internal Action<WpfCandidateReviewListItem> ApplyCandidateSelectionChangedEffects { get; init; }
        internal Action<WpfModelComparisonHistoryItem> ApplyModelComparisonHistorySelectionEffects { get; init; }
        internal Slider CandidateConfidenceSlider { get; init; }
        internal ListBox CandidateListBox { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
    }
}
