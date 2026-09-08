using MvcVisionSystem._1._Core;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public enum TrainingWeightsApplicationStatus
    {
        NoCandidate,
        AlreadyCurrent,
        NotNewer,
        Applied
    }

    public sealed class TrainingWeightsApplicationRequest
    {
        public LabelingProjectData Data { get; init; }

        public string CurrentWeightsPath { get; init; } = string.Empty;
    }

    public sealed class TrainingWeightsApplicationResult
    {
        public TrainingWeightsApplicationResult(
            TrainingWeightsApplicationStatus status,
            WpfTrainingWeightsComparison comparison,
            string candidateWeightsPath,
            string baselineWeightsPath)
        {
            Status = status;
            Comparison = comparison;
            CandidateWeightsPath = candidateWeightsPath ?? string.Empty;
            BaselineWeightsPath = baselineWeightsPath ?? string.Empty;
        }

        public TrainingWeightsApplicationStatus Status { get; }

        public WpfTrainingWeightsComparison Comparison { get; }

        public string CandidateWeightsPath { get; }

        public string BaselineWeightsPath { get; }

        public bool Applied => Status == TrainingWeightsApplicationStatus.Applied;
    }

    public sealed class TrainingWeightsApplicationRecordRequest
    {
        public LabelingProjectData Data { get; init; }

        public string WeightsPath { get; init; } = string.Empty;

        public string BaselineWeightsPath { get; init; } = string.Empty;

        public bool SavedToRecipe { get; init; }
    }

    /// <summary>
    /// Owns the data-side transition from a completed training artifact to a
    /// pending inspection-model candidate. WPF only presents the returned
    /// comparison and keeps the explicit Recipe save/decision commands.
    /// </summary>
    public sealed class TrainingWeightsApplicationWorkflowService
    {
        private readonly TrainingWeightsService trainingWeightsService;
        private readonly TrainingGuideHistoryService trainingGuideHistoryService;

        public TrainingWeightsApplicationWorkflowService()
            : this(new TrainingWeightsService(), new TrainingGuideHistoryService())
        {
        }

        public TrainingWeightsApplicationWorkflowService(
            TrainingWeightsService trainingWeightsService,
            TrainingGuideHistoryService trainingGuideHistoryService)
        {
            this.trainingWeightsService = trainingWeightsService
                ?? throw new ArgumentNullException(nameof(trainingWeightsService));
            this.trainingGuideHistoryService = trainingGuideHistoryService
                ?? throw new ArgumentNullException(nameof(trainingGuideHistoryService));
        }

        public TrainingWeightsApplicationResult StageLatestCandidate(TrainingWeightsApplicationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            LabelingProjectData data = request.Data
                ?? throw new ArgumentNullException(nameof(request.Data));
            EnsureProjectSettings(data);

            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            string currentWeightsPath = Normalize(request.CurrentWeightsPath);
            if (string.IsNullOrWhiteSpace(currentWeightsPath))
            {
                currentWeightsPath = Normalize(settings.WeightsPath);
            }

            WpfTrainingWeightsComparison comparison = trainingWeightsService.BuildComparison(
                settings.ProjectRootPath,
                data.OutputRootPath,
                currentWeightsPath);
            string candidateWeightsPath = Normalize(comparison?.LatestWeightsPath);
            if (string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                return new TrainingWeightsApplicationResult(
                    TrainingWeightsApplicationStatus.NoCandidate,
                    comparison,
                    string.Empty,
                    string.Empty);
            }

            if (string.Equals(currentWeightsPath, candidateWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                return new TrainingWeightsApplicationResult(
                    TrainingWeightsApplicationStatus.AlreadyCurrent,
                    comparison,
                    candidateWeightsPath,
                    string.Empty);
            }

            if (comparison?.ShouldApplyLatest != true)
            {
                return new TrainingWeightsApplicationResult(
                    TrainingWeightsApplicationStatus.NotNewer,
                    comparison,
                    candidateWeightsPath,
                    string.Empty);
            }

            string baselineWeightsPath = File.Exists(currentWeightsPath)
                ? currentWeightsPath
                : string.Empty;
            settings.WeightsPath = candidateWeightsPath;
            RecordAppliedWeights(
                data,
                candidateWeightsPath,
                baselineWeightsPath,
                savedToRecipe: false,
                comparison);

            return new TrainingWeightsApplicationResult(
                TrainingWeightsApplicationStatus.Applied,
                comparison,
                candidateWeightsPath,
                baselineWeightsPath);
        }

        public void RecordAppliedWeights(TrainingWeightsApplicationRecordRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            LabelingProjectData data = request.Data
                ?? throw new ArgumentNullException(nameof(request.Data));
            EnsureProjectSettings(data);

            string weightsPath = Normalize(request.WeightsPath);
            if (string.IsNullOrWhiteSpace(weightsPath))
            {
                return;
            }

            string baselineWeightsPath = Normalize(request.BaselineWeightsPath);
            WpfTrainingWeightsComparison comparison = trainingWeightsService.BuildComparison(
                data.ProjectSettings.PythonModel.ProjectRootPath,
                data.OutputRootPath,
                string.IsNullOrWhiteSpace(baselineWeightsPath) ? weightsPath : baselineWeightsPath);
            RecordAppliedWeights(
                data,
                weightsPath,
                baselineWeightsPath,
                request.SavedToRecipe,
                comparison);
        }

        private void RecordAppliedWeights(
            LabelingProjectData data,
            string weightsPath,
            string baselineWeightsPath,
            bool savedToRecipe,
            WpfTrainingWeightsComparison comparison)
        {
            YoloTrainingGuideHistory history = data.ProjectSettings.TrainingGuide;
            trainingGuideHistoryService.UpdateAppliedWeightsHistory(history, weightsPath, savedToRecipe);
            ModelRegistryService.RecordTrainingCandidate(
                data.ProjectSettings.ModelRegistry,
                data.ProjectSettings.PythonModel,
                data.ProjectSettings.DatasetPurpose,
                data.OutputRootPath,
                weightsPath,
                baselineWeightsPath,
                comparison?.MetricsStatusText,
                history.LastTrainingState,
                history.LastTrainingProgressPercent,
                history.LastTrainingMessage,
                savedToRecipe,
                history.LastTrainingDatasetVersionId,
                history.LastTrainingDatasetContentSha256);
        }

        private static void EnsureProjectSettings(LabelingProjectData data)
        {
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();
        }

        private static string Normalize(string path)
            => path?.Trim() ?? string.Empty;
    }
}
