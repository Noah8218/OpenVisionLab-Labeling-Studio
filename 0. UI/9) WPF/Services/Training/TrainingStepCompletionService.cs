using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the seven-step training guide projection from immutable workflow inputs.
    /// The service intentionally has no Shell, ViewModel, control, or file-system access.
    /// </summary>
    public static class TrainingStepCompletionService
    {
        public static TrainingStepCompletionSnapshot Build(
            YoloDatasetReadinessReport report,
            IEnumerable<TrainingStepQueueState> queueItems,
            string activeImagePath,
            int classCount,
            int manualRoiCount,
            int confirmedCandidateCount,
            bool hasDatasetSetup,
            bool hasCompletedCurrentDatasetTraining,
            PythonCommunicationStatus communicationStatus,
            int pendingCandidateCount)
        {
            IReadOnlyList<TrainingStepQueueState> queue = (queueItems ?? Enumerable.Empty<TrainingStepQueueState>())
                .Where(item => item != null)
                .ToList();
            YoloDatasetStatistics statistics = report?.Statistics;
            int savedObjectCount = statistics?.TotalObjectCount ?? 0;
            int totalImageCount = statistics != null && statistics.TotalImageCount > 0
                ? statistics.TotalImageCount
                : queue.Count;
            int completedImageCount = statistics != null && totalImageCount > 0
                ? Math.Min(statistics.TotalLabelFileCount, totalImageCount)
                : queue.Count(item => item.HasCompletedLabelWork);
            bool hasImages = queue.Count > 0 || !string.IsNullOrWhiteSpace(activeImagePath);
            bool hasClasses = classCount > 0;
            bool hasAnyLabelWork = manualRoiCount > 0
                || confirmedCandidateCount > 0
                || savedObjectCount > 0
                || queue.Any(item => item.IsLabeled);
            bool isLabelingComplete = hasImages && totalImageCount > 0
                ? completedImageCount >= totalImageCount
                : hasAnyLabelWork;
            string labelingStateText = isLabelingComplete
                ? "완료"
                : completedImageCount > 0 && totalImageCount > 0
                    ? $"{completedImageCount}/{totalImageCount}"
                    : "라벨 필요";
            bool datasetReady = report?.IsReady == true;

            string trainingState = communicationStatus?.LastTrainingState?.Trim() ?? string.Empty;
            bool hasTrainingStatus = TrainingProgressPresentationService.HasTrainingStatus(communicationStatus);
            bool trainingCompletedFromWorker = TrainingWeightsService.IsCompletedTrainingState(trainingState);
            bool trainingCompleted = trainingCompletedFromWorker || hasCompletedCurrentDatasetTraining;
            bool trainingRunning = hasTrainingStatus
                && !trainingCompleted
                && !TrainingProgressPresentationService.IsTerminalTrainingState(trainingState);
            bool hasInferenceResult = pendingCandidateCount > 0
                || queue.Any(item => item.IsCandidate);

            return new TrainingStepCompletionSnapshot(
                hasImages,
                new[]
                {
                    new TrainingStepState(1, hasDatasetSetup, hasDatasetSetup ? "완료" : "데이터셋 필요"),
                    new TrainingStepState(2, hasImages, hasImages ? "완료" : "이미지 필요"),
                    new TrainingStepState(3, hasClasses, hasClasses ? "완료" : "클래스 필요"),
                    new TrainingStepState(4, isLabelingComplete, labelingStateText),
                    new TrainingStepState(5, datasetReady, datasetReady ? "완료" : "점검 필요"),
                    new TrainingStepState(6, trainingCompleted, trainingCompleted ? "완료" : trainingRunning ? "진행 중" : "대기"),
                    new TrainingStepState(7, hasInferenceResult, hasInferenceResult ? "후보 있음" : "추론 필요")
                });
        }
    }

    public class TrainingStepCompletionSnapshot
    {
        public TrainingStepCompletionSnapshot(
            bool hasImages,
            IEnumerable<TrainingStepState> steps)
        {
            HasImages = hasImages;
            Steps = (steps ?? Enumerable.Empty<TrainingStepState>()).ToList();
        }

        public bool HasImages { get; }

        public IReadOnlyList<TrainingStepState> Steps { get; }
    }

    public class TrainingStepState
    {
        public TrainingStepState(int order, bool isCompleted, string stateText)
        {
            Order = order;
            IsCompleted = isCompleted;
            StateText = stateText ?? string.Empty;
        }

        public int Order { get; }

        public bool IsCompleted { get; }

        public string StateText { get; }
    }

    public class TrainingStepQueueState
    {
        public TrainingStepQueueState(
            bool isLabeled,
            bool isSaveRequired,
            YoloImageReviewState reviewState,
            YoloImageQualityReviewState qualityReviewState)
        {
            IsLabeled = isLabeled;
            IsSaveRequired = isSaveRequired;
            ReviewState = reviewState;
            QualityReviewState = qualityReviewState;
        }

        public bool IsLabeled { get; }

        public bool IsSaveRequired { get; }

        public YoloImageReviewState ReviewState { get; }

        public YoloImageQualityReviewState QualityReviewState { get; }

        public bool IsCandidate => ReviewState == YoloImageReviewState.Candidate;

        public bool HasCompletedLabelWork
        {
            get
            {
                if (IsSaveRequired || QualityReviewState == YoloImageQualityReviewState.NeedsFix)
                {
                    return false;
                }

                return IsLabeled
                    || ReviewState == YoloImageReviewState.Confirmed
                    || ReviewState == YoloImageReviewState.Skipped
                    || ReviewState == YoloImageReviewState.NoCandidate;
            }
        }
    }

    [Obsolete("Use TrainingStepCompletionSnapshot.", false)]
    public sealed class WpfTrainingStepCompletionSnapshot : TrainingStepCompletionSnapshot
    {
        public WpfTrainingStepCompletionSnapshot(
            bool hasImages,
            IEnumerable<WpfTrainingStepState> steps)
            : base(hasImages, (steps ?? Enumerable.Empty<WpfTrainingStepState>()).Cast<TrainingStepState>())
        {
            Steps = (steps ?? Enumerable.Empty<WpfTrainingStepState>()).ToList();
        }

        public new IReadOnlyList<WpfTrainingStepState> Steps { get; }
    }

    [Obsolete("Use TrainingStepState.", false)]
    public sealed class WpfTrainingStepState : TrainingStepState
    {
        public WpfTrainingStepState(int order, bool isCompleted, string stateText)
            : base(order, isCompleted, stateText)
        {
        }
    }

    [Obsolete("Use TrainingStepQueueState.", false)]
    public sealed class WpfTrainingStepQueueState : TrainingStepQueueState
    {
        public WpfTrainingStepQueueState(
            bool isLabeled,
            bool isSaveRequired,
            YoloImageReviewState reviewState,
            YoloImageQualityReviewState qualityReviewState)
            : base(isLabeled, isSaveRequired, reviewState, qualityReviewState)
        {
        }
    }

    [Obsolete("Use TrainingStepCompletionService.", false)]
    public static class WpfTrainingStepCompletionService
    {
        public static WpfTrainingStepCompletionSnapshot Build(
            YoloDatasetReadinessReport report,
            IEnumerable<WpfTrainingStepQueueState> queueItems,
            string activeImagePath,
            int classCount,
            int manualRoiCount,
            int confirmedCandidateCount,
            bool hasDatasetSetup,
            bool hasCompletedCurrentDatasetTraining,
            PythonCommunicationStatus communicationStatus,
            int pendingCandidateCount)
        {
            TrainingStepCompletionSnapshot completion = TrainingStepCompletionService.Build(
                report,
                (queueItems ?? Enumerable.Empty<WpfTrainingStepQueueState>()).Cast<TrainingStepQueueState>(),
                activeImagePath,
                classCount,
                manualRoiCount,
                confirmedCandidateCount,
                hasDatasetSetup,
                hasCompletedCurrentDatasetTraining,
                communicationStatus,
                pendingCandidateCount);
            return new WpfTrainingStepCompletionSnapshot(
                completion.HasImages,
                completion.Steps.Select(step => new WpfTrainingStepState(step.Order, step.IsCompleted, step.StateText)));
        }
    }
}
