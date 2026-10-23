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
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: true, keepCurrentWorkflow: false);

        internal void ApplyDetectionCandidatesPreservingConfirmed(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: false, keepCurrentWorkflow: false);

        internal void ApplyDetectionCandidatesPreservingConfirmedInCurrentWorkflow(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: false, keepCurrentWorkflow: true);

        private void ApplyDetectionCandidatesCore(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            bool clearConfirmed,
            bool keepCurrentWorkflow)
        {
            int loadedCount = context.CandidateReviewState.LoadPendingCandidates(candidates, clearConfirmed);
            context.ClearCandidateReviewHistory?.Invoke();
            context.ApplyCanvasDisplayMode?.Invoke(
                keepCurrentWorkflow ? WpfCanvasDisplayMode.Both : WpfCanvasDisplayMode.InferenceOnly,
                false,
                false);
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.SetActiveImageDetectionStatus?.Invoke(loadedCount, succeeded);
            context.ApplyActiveAnomalyClassification?.Invoke(succeeded ? candidates : Array.Empty<YoloWorkerSmokeCandidate>());
            context.AddCandidateReviewHistory?.Invoke(
                context.DetectionResultPresentationService.BuildCandidateLoadHistory(
                    loadedCount,
                    succeeded,
                    context.CandidateConfidenceFilterProvider?.Invoke() ?? 0D));
            if (!keepCurrentWorkflow)
            {
                context.ShowCandidateReviewWorkflowView?.Invoke();
            }
            context.RefreshCanvasWorkflowContext?.Invoke();

            if (!context.CandidateReviewState.HasPendingCandidates)
            {
                context.AppendLog?.Invoke("AI 후보가 없습니다.");
                return;
            }

            context.AppendLog?.Invoke($"AI 후보 로드: {loadedCount}개");
        }

        internal void RestoreInferenceResult(YoloWorkerSmokeTestResult result)
        {
            if (result != null && !result.Succeeded) return;
            if (result == null) context.CandidateReviewState.ClearPendingCandidates();
            else context.CandidateReviewState.LoadPendingCandidates(result.Candidates, clearConfirmed: true);
            context.RefreshCandidateList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.RefreshCanvasWorkflowContext?.Invoke();
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
