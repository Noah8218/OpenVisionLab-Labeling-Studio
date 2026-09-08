using MvcVisionSystem.Yolo;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class ModelCenterDashboardWorkflowRequest
    {
        public LabelingProjectData Data { get; init; }

        public WpfTrainingWeightsComparison Comparison { get; init; }

        public string ConfiguredWeightsPathOverride { get; init; } = string.Empty;

        public string PendingBaselineWeightsPath { get; init; } = string.Empty;

        public bool PendingManualWeightsSelection { get; init; }

        public bool HasPendingTrainingWeightsRecipeSave { get; init; }

        public bool IsModelPromotionHeld { get; init; }
    }

    public sealed class ModelCenterDashboardWorkflowResult
    {
        public ModelCenterDashboardWorkflowResult(
            ModelCenterDashboardState dashboardState,
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            DashboardState = dashboardState ?? throw new ArgumentNullException(nameof(dashboardState));
            Comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
            ConfiguredWeightsPath = configuredWeightsPath ?? string.Empty;
            HasPendingModelSelection = hasPendingModelSelection;
        }

        public ModelCenterDashboardState DashboardState { get; }

        public WpfTrainingWeightsComparison Comparison { get; }

        public string ConfiguredWeightsPath { get; }

        public bool HasPendingModelSelection { get; }
    }

    /// <summary>
    /// Owns the data-side snapshot and comparison needed to render Model Center.
    /// The Shell remains responsible for applying the typed result to WPF
    /// bindings and controls.
    /// </summary>
    public sealed class ModelCenterDashboardWorkflowService
    {
        private readonly TrainingWeightsService trainingWeightsService;

        public ModelCenterDashboardWorkflowService()
            : this(new TrainingWeightsService())
        {
        }

        public ModelCenterDashboardWorkflowService(TrainingWeightsService trainingWeightsService)
        {
            this.trainingWeightsService = trainingWeightsService
                ?? throw new ArgumentNullException(nameof(trainingWeightsService));
        }

        public ModelCenterDashboardWorkflowResult Build(ModelCenterDashboardWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            LabelingProjectData data = request.Data
                ?? throw new ArgumentNullException(nameof(request.Data));
            EnsureProjectSettings(data);

            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            string configuredWeightsPath = string.IsNullOrWhiteSpace(request.ConfiguredWeightsPathOverride)
                ? settings.WeightsPath ?? string.Empty
                : request.ConfiguredWeightsPathOverride;
            WpfTrainingWeightsComparison comparison = request.Comparison
                ?? BuildComparison(data, configuredWeightsPath, request.PendingBaselineWeightsPath);
            bool hasPendingModelSelection = request.PendingManualWeightsSelection
                || request.HasPendingTrainingWeightsRecipeSave;
            ModelCenterDashboardState dashboardState = ModelCenterDashboardPresentationService.Build(
                settings,
                comparison,
                data.ProjectSettings.TrainingGuide,
                data.ProjectSettings.ModelRegistry,
                configuredWeightsPath,
                hasPendingModelSelection,
                request.IsModelPromotionHeld);

            return new ModelCenterDashboardWorkflowResult(
                dashboardState,
                comparison,
                configuredWeightsPath,
                hasPendingModelSelection);
        }

        public WpfTrainingWeightsComparison BuildComparison(
            LabelingProjectData data,
            string configuredWeightsPath,
            string pendingBaselineWeightsPath = "")
        {
            ArgumentNullException.ThrowIfNull(data);
            EnsureProjectSettings(data);
            string currentWeightsPath = ResolveComparisonCurrentWeightsPath(
                configuredWeightsPath ?? data.ProjectSettings.PythonModel.WeightsPath,
                pendingBaselineWeightsPath);
            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            return trainingWeightsService.BuildComparison(
                settings.ProjectRootPath,
                data.OutputRootPath,
                currentWeightsPath);
        }

        public static string ResolveComparisonCurrentWeightsPath(
            string configuredWeightsPath,
            string pendingBaselineWeightsPath)
        {
            string pendingBaseline = pendingBaselineWeightsPath?.Trim() ?? string.Empty;
            string configured = configuredWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(pendingBaseline)
                && File.Exists(pendingBaseline)
                && !string.Equals(pendingBaseline, configured, StringComparison.OrdinalIgnoreCase))
            {
                return pendingBaseline;
            }

            return configured;
        }

        private static void EnsureProjectSettings(LabelingProjectData data)
        {
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();
        }
    }
}
