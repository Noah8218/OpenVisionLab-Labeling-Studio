using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Projects a completed detection into Candidate Review and the existing
    /// canvas/list owners. It does not execute inference or own WPF controls.
    /// </summary>
    internal sealed class DetectionResultProjectionAdapter
    {
        private readonly DetectionResultProjectionAdapterContext context;

        internal DetectionResultProjectionAdapter(DetectionResultProjectionAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.CandidateReviewState);
            ArgumentNullException.ThrowIfNull(context.DetectionResultPresentationService);
        }

        internal void ApplyDetectionCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: true);

        internal void ApplyDetectionCandidatesPreservingConfirmed(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: false);

        private void ApplyDetectionCandidatesCore(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            bool clearConfirmed)
        {
            int loadedCount = context.CandidateReviewState.LoadPendingCandidates(candidates, clearConfirmed);
            context.ClearCandidateReviewHistory?.Invoke();
            context.ApplyCanvasDisplayMode?.Invoke(WpfCanvasDisplayMode.InferenceOnly, false, false);
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.SetActiveImageDetectionStatus?.Invoke(loadedCount, succeeded);
            context.ApplyActiveAnomalyClassification?.Invoke(candidates);
            context.AddCandidateReviewHistory?.Invoke(
                context.DetectionResultPresentationService.BuildCandidateLoadHistory(
                    loadedCount,
                    succeeded,
                    context.CandidateConfidenceFilterProvider?.Invoke() ?? 0D));
            context.ShowCandidateReviewWorkflowView?.Invoke();
            context.RefreshCanvasWorkflowContext?.Invoke();

            if (!context.CandidateReviewState.HasPendingCandidates)
            {
                context.AppendLog?.Invoke("AI 후보가 없습니다.");
                return;
            }

            context.AppendLog?.Invoke($"AI 후보 로드: {loadedCount}개");
        }

        internal void AddCandidateReviewHistory(string message)
            => context.AddCandidateReviewHistory?.Invoke(message);
    }

    internal sealed class DetectionResultProjectionAdapterContext
    {
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal DetectionResultPresentationService DetectionResultPresentationService { get; init; }
        internal Action ClearCandidateReviewHistory { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<int, bool> SetActiveImageDetectionStatus { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>> ApplyActiveAnomalyClassification { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Func<double> CandidateConfidenceFilterProvider { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
