using MvcVisionSystem.Yolo;
using System;

namespace MvcVisionSystem
{
    public sealed class DatasetHealthWorkflowRequest
    {
        public LabelingProjectData Data { get; init; }

        public bool IncludeHealthReport { get; init; }

        public bool IncludeVisualQa { get; init; }

        public int? VisualQaClassIndex { get; init; }
    }

    public sealed class DatasetHealthWorkflowResult
    {
        public DatasetHealthWorkflowResult(
            YoloDatasetHealthReport healthReport,
            WpfDatasetVisualQaCatalog visualQaCatalog)
        {
            HealthReport = healthReport;
            VisualQaCatalog = visualQaCatalog;
        }

        public YoloDatasetHealthReport HealthReport { get; }

        public WpfDatasetVisualQaCatalog VisualQaCatalog { get; }
    }

    /// <summary>
    /// Coordinates the read-only Dataset Health and visual-QA analysis paths.
    /// The ViewModel keeps presentation, filters, and commands while existing
    /// dataset services keep their health, quality, split, and preview rules.
    /// </summary>
    public sealed class DatasetHealthWorkflowService
    {
        public const int MaximumVisualQaItemCount = DatasetVisualQaService.MaximumCatalogItemCount;

        private readonly DatasetVisualQaService datasetVisualQaService;
        private bool hasCachedHealthReport;
        private LabelingProjectData cachedHealthData;
        private YoloDatasetHealthReport cachedHealthReport;

        public DatasetHealthWorkflowService(DatasetVisualQaService datasetVisualQaService = null)
        {
            this.datasetVisualQaService = datasetVisualQaService
                ?? new DatasetVisualQaService();
        }

        public DatasetHealthWorkflowResult Analyze(DatasetHealthWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            YoloDatasetHealthReport healthReport = request.IncludeHealthReport
                ? YoloDatasetHealthService.Build(request.Data)
                : null;
            if (request.IncludeHealthReport)
            {
                cachedHealthData = request.Data;
                cachedHealthReport = healthReport;
                hasCachedHealthReport = true;
            }

            WpfDatasetVisualQaCatalog visualQaCatalog = request.IncludeVisualQa
                ? datasetVisualQaService.BuildCatalog(request.Data, request.VisualQaClassIndex)
                : null;
            return new DatasetHealthWorkflowResult(healthReport, visualQaCatalog);
        }

        public bool TryReuseLastHealthReport(LabelingProjectData data, out YoloDatasetHealthReport healthReport)
        {
            if (hasCachedHealthReport && ReferenceEquals(cachedHealthData, data))
            {
                healthReport = cachedHealthReport;
                return true;
            }

            healthReport = null;
            return false;
        }
    }
}
