using MvcVisionSystem._3._Communication.TCP;
using System;

namespace MvcVisionSystem
{
    public sealed class TrainingGuideDatasetHistoryWorkflowRequest
    {
        public LabelingProjectData Data { get; init; }

        public bool IsReady { get; init; }

        public string IssueKind { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public bool RecordHistory { get; init; }

        public bool HasPendingTrainingWeightsRecipeSave { get; init; }

        public Func<bool> SaveHistoryQuietly { get; init; }
    }

    public sealed class TrainingGuideTrainingHistoryWorkflowRequest
    {
        public LabelingProjectData Data { get; init; }

        public PythonCommunicationStatus Status { get; init; }

        public bool HasPendingTrainingWeightsRecipeSave { get; init; }

        public Func<bool> SaveHistoryQuietly { get; init; }
    }

    public sealed class TrainingGuideHistoryWorkflowResult
    {
        public TrainingGuideHistoryWorkflowResult(
            bool historyUpdated,
            bool isTerminalTrainingState,
            bool saveAttempted,
            bool saveSucceeded)
        {
            HistoryUpdated = historyUpdated;
            IsTerminalTrainingState = isTerminalTrainingState;
            SaveAttempted = saveAttempted;
            SaveSucceeded = saveSucceeded;
        }

        public bool HistoryUpdated { get; }

        public bool IsTerminalTrainingState { get; }

        public bool SaveAttempted { get; }

        public bool SaveSucceeded { get; }

        public static TrainingGuideHistoryWorkflowResult Ignored { get; }
            = new TrainingGuideHistoryWorkflowResult(false, false, false, false);
    }

    /// <summary>
    /// Owns the event-to-history transition for the training guide. The shell
    /// supplies the current data snapshot and a quiet Recipe-save adapter; this
    /// service decides when a dataset or terminal training event may request
    /// that save and keeps duplicate terminal worker updates from adding runs.
    /// </summary>
    public sealed class TrainingGuideHistoryWorkflowService
    {
        private readonly TrainingGuideHistoryService trainingGuideHistoryService;
        private string lastRecordedTrainingGuideRunSignature = string.Empty;

        public TrainingGuideHistoryWorkflowService()
            : this(new TrainingGuideHistoryService())
        {
        }

        public TrainingGuideHistoryWorkflowService(TrainingGuideHistoryService trainingGuideHistoryService)
        {
            this.trainingGuideHistoryService = trainingGuideHistoryService
                ?? throw new ArgumentNullException(nameof(trainingGuideHistoryService));
        }

        public TrainingGuideHistoryWorkflowResult RecordDatasetHistory(
            TrainingGuideDatasetHistoryWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            LabelingProjectData data = request.Data
                ?? throw new ArgumentNullException(nameof(request.Data));
            EnsureProjectSettings(data);

            trainingGuideHistoryService.UpdateDatasetHistory(
                data.ProjectSettings.TrainingGuide,
                request.IsReady,
                request.IssueKind,
                request.Summary,
                request.RecordHistory);

            return TrySaveHistory(
                request.HasPendingTrainingWeightsRecipeSave,
                request.SaveHistoryQuietly,
                isTerminalTrainingState: false,
                shouldSave: true,
                historyUpdated: true);
        }

        public TrainingGuideHistoryWorkflowResult RecordTrainingHistory(
            TrainingGuideTrainingHistoryWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!TrainingProgressPresentationService.HasTrainingStatus(request.Status))
            {
                return TrainingGuideHistoryWorkflowResult.Ignored;
            }

            LabelingProjectData data = request.Data
                ?? throw new ArgumentNullException(nameof(request.Data));
            EnsureProjectSettings(data);
            YoloTrainingGuideHistory history = data.ProjectSettings.TrainingGuide;
            trainingGuideHistoryService.UpdateTrainingHistory(
                history,
                request.Status,
                TrainingProgressPresentationService.IsTerminalTrainingState,
                ref lastRecordedTrainingGuideRunSignature);

            bool isTerminalTrainingState = TrainingProgressPresentationService.IsTerminalTrainingState(
                history.LastTrainingState);
            return TrySaveHistory(
                request.HasPendingTrainingWeightsRecipeSave,
                request.SaveHistoryQuietly,
                isTerminalTrainingState,
                shouldSave: isTerminalTrainingState,
                historyUpdated: true);
        }

        private static TrainingGuideHistoryWorkflowResult TrySaveHistory(
            bool hasPendingTrainingWeightsRecipeSave,
            Func<bool> saveHistoryQuietly,
            bool isTerminalTrainingState,
            bool shouldSave,
            bool historyUpdated)
        {
            if (hasPendingTrainingWeightsRecipeSave || !shouldSave || saveHistoryQuietly == null)
            {
                return new TrainingGuideHistoryWorkflowResult(
                    historyUpdated,
                    isTerminalTrainingState,
                    saveAttempted: false,
                    saveSucceeded: false);
            }

            return new TrainingGuideHistoryWorkflowResult(
                historyUpdated,
                isTerminalTrainingState,
                saveAttempted: true,
                saveSucceeded: saveHistoryQuietly());
        }

        private static void EnsureProjectSettings(LabelingProjectData data)
        {
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();
        }
    }
}
