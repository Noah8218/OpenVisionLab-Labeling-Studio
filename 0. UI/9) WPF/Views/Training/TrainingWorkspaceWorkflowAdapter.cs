using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    internal sealed class TrainingWorkspaceWorkflowAdapter : IDisposable
    {
        private readonly TrainingWorkspaceWorkflowAdapterContext context;
        private bool configured, disposed;
        private TrainingWorkspaceViewModel Workspace => context.ViewModels.TrainingWorkspaceViewModel;

        internal TrainingWorkspaceWorkflowAdapter(TrainingWorkspaceWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.SaveMetadata);
        }

        internal void Configure()
        {
            if (configured || disposed) return;
            configured = true;
            context.Runtime.ConfigureQueue(new TrainingQueueRuntimeCallbacks
            {
                CanStart = () => Workspace.CanStart,
                IsCloseApproved = context.IsCloseApproved,
                SaveMetadata = context.SaveMetadata,
                CommandStateChanged = () => context.UpdateCommandState?.Invoke(),
                HistoryUpdated = () => { Refresh(); context.RefreshComparison?.Invoke(); }
            });
            Workspace.Configure(context.Runtime, CaptureImages, Refresh, context.OpenDataset, context.OpenEnvironment, context.OpenComparison, context.OpenInference,
                () => context.DataProvider()?.ProjectSettings?.ModelRegistry?.TrainingRuns?.Count(run => run != null && string.Equals(run.State, "completed", StringComparison.OrdinalIgnoreCase)) ?? 0,
                context.ViewModels.ModelWorkflowViewModels.YoloStatusViewModel);
            Workspace.RefreshRequested += Refresh;
        }

        internal void Refresh()
        {
            if (disposed || context.IsCloseApproved?.Invoke() == true) return;
            LabelingProjectData data = context.DataProvider();
            if (data?.ProjectSettings == null) { Workspace.SetContext(null); return; }
            TrainingDatasetImageSelection[] images = CaptureImages().ToArray();
            RecipeDatasetVersionSnapshot version = null;
            string failure = string.Empty;
            try
            {
                if (!data.ProjectSettings.ExternalYoloDataset.UseForTraining) version = RecipeDatasetVersionService.CreateSnapshot(data);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { failure = error.Message; }
            IReadOnlyList<TrainingModelChoice> models = BuildModels(data.ProjectSettings.DatasetPurpose, data.ProjectSettings.PythonModel, context.StatusProvider(), context.RuntimeStateProvider()?.CanRunTraining == true);
            IReadOnlyList<WpfModelRegistryHistoryItem> history = ModelRegistryHistoryPresentationService.BuildHistoryItems(data.ProjectSettings.ModelRegistry, int.MaxValue);
            IReadOnlyList<WpfModelBenchmarkRun> reports = new ModelBenchmarkCatalogService().Load(context.RepositoryRootProvider?.Invoke() ?? ModelBenchmarkCatalogService.FindRepositoryRoot());
            TrainingRun[] runs = data.ProjectSettings.ModelRegistry.TrainingRuns.Where(run => run != null && run.State.Equals("completed", StringComparison.OrdinalIgnoreCase)).ToArray();
            TrainingHistoryItem[] records = runs.OrderByDescending(run => ModelRegistryTextPresentationService.ParseUtc(run.EventUtc)).Select(run =>
            {
                WpfModelRegistryHistoryItem item = history.FirstOrDefault(row => row.TrainingRunId == run.TrainingRunId);
                string name = models.FirstOrDefault(model => model.ModelId == run.Parameters?.ModelId)?.Name ?? run.RunName;
                return item == null ? null : BuildHistoryItem(item, run, name, data.ProjectSettings.DatasetPurpose, reports);
            }).Where(item => item != null).ToArray();
            string engine = PythonModelSettings.NormalizeModelEngine(data.ProjectSettings.PythonModel.ModelEngine);
            string environment = TrainingWorkspaceViewModel.F(context.StatusProvider().IsClientConnected ? "EnvironmentReady" : "EnvironmentDisconnected", engine);
            var names = images.Where(image => image.IsEligible(false)).Select(image => Path.GetFileNameWithoutExtension(image.ImagePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var reviewed = images.Where(image => image.IsEligible(true)).Select(image => Path.GetFileNameWithoutExtension(image.ImagePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Workspace.SetContext(new TrainingWorkspaceContext(data.OutputRootPath + "|" + context.RecipeNameProvider(), context.RecipeNameProvider(), data.ProjectSettings.DatasetPurpose,
                version?.DatasetVersionId ?? "—", version?.ContentSha256 ?? string.Empty, images, data.ClassNamedList.Where(item => item != null).Select(item => item.Text).ToArray(),
                YoloDatasetStatisticsService.BuildClassObjectCounts(data, imageNames: names), YoloDatasetStatisticsService.BuildClassObjectCounts(data, imageNames: reviewed), models, records,
                Path.GetFileName(data.ProjectSettings.PythonModel.WeightsPath), environment,
                history.Any(item => item.IsCurrentInspectionModel) ? "WpfTrainingWorkspace.InspectionModel" : "WpfModelRegistry.History.Configured.Model"));
            Workspace.SetSnapshotError(failure);
        }

        internal static TrainingHistoryItem BuildHistoryItem(WpfModelRegistryHistoryItem history, TrainingRun run, string name, LabelingDatasetPurpose purpose, IReadOnlyList<WpfModelBenchmarkRun> reports)
        {
            string task = purpose switch { LabelingDatasetPurpose.Segmentation => "segmentation", LabelingDatasetPurpose.AnomalyDetection => "anomaly-classification", _ => "object-detection" };
            string path = File.Exists(history.WeightsPath) ? history.WeightsPath : history.ArtifactPath;
            WpfModelBenchmarkRun evaluation = null;
            if (run.WeightsSha256.Length == 64 && run.WeightsSha256.Equals(history.WeightsSha256, StringComparison.OrdinalIgnoreCase) && ModelArtifactStoreService.Verify(path, history.WeightsSha256))
                evaluation = reports.Where(report => report.TaskKey == task && report.DatasetVersionId == run.DatasetVersionId && report.DatasetContentSha256 == run.DatasetContentSha256
                    && report.SourceSha256.Length == 64 && report.WeightsSha256.Equals(run.WeightsSha256, StringComparison.OrdinalIgnoreCase)
                    && (ModelComparisonWorkspaceViewModel.SamePath(history.WeightsPath, report.WeightsPath) || ModelComparisonWorkspaceViewModel.SamePath(history.ArtifactPath, report.WeightsPath)))
                    .OrderByDescending(report => report.CreatedAt).FirstOrDefault();
            if (evaluation != null && !ModelArtifactStoreService.Verify(evaluation.SourcePath, evaluation.SourceSha256)) evaluation = null;
            return new TrainingHistoryItem(history, run, name, purpose, evaluation);
        }

        private IReadOnlyList<TrainingDatasetImageSelection> CaptureImages()
        {
            bool anomaly = context.DataProvider()?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
            return context.ImageQueueItems.Select(item => new TrainingDatasetImageSelection(item.ImagePath, YoloDatasetSplitService.NormalizeStandardSplit(item.DatasetSplit),
                anomaly ? item.AnomalyReviewState != AnomalyImageReviewState.Unreviewed : item.HasSavedLabelFile, item.IsSaveRequired, item.QualityReviewState, item.AnomalyReviewState)).ToArray();
        }

        internal static IReadOnlyList<TrainingModelChoice> BuildModels(LabelingDatasetPurpose purpose, PythonModelSettings settings, PythonCommunicationStatus status, bool runtimeReady)
        {
            var models = new List<TrainingModelChoice>();
            void Add(string id, string name, string requiredEngine, string weights, string note)
            {
                bool compatible = YoloTrainingWorkflowService.CanUseQueuedTrainingModel(settings, purpose, status, requiredEngine, weights, requireLocalWeights: false);
                bool cached = requiredEngine is PythonModelSettings.EngineUnet or PythonModelSettings.EnginePatchCore
                    || status.WorkerRuntimeReadyTrainingWeights.Any(path => Path.GetFileName(path).Equals(weights, StringComparison.OrdinalIgnoreCase));
                bool available = compatible && runtimeReady && status.IsClientConnected && cached;
                models.Add(new TrainingModelChoice(id, name, requiredEngine, weights, note, available, !compatible ? "OtherRuntime" : !cached ? "MissingWeights" : "RuntimeUnavailable"));
            }
            if (purpose == LabelingDatasetPurpose.Segmentation)
            {
                Add("yolov8s-seg", "YOLOv8s-seg", PythonModelSettings.EngineYoloV8, "yolov8s-seg.pt", "Note.Balanced");
                Add("yolo11s-seg", "YOLO11s-seg", PythonModelSettings.EngineYolo11, "yolo11s-seg.pt", "Note.Balanced");
                Add("unet", "U-Net", PythonModelSettings.EngineUnet, string.Empty, "Note.Unet");
            }
            else if (purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                Add("patchcore", "PatchCore (WRN50)", PythonModelSettings.EnginePatchCore, string.Empty, "Note.PatchCore");
                Add("yolov8s-cls", "YOLOv8s-cls", PythonModelSettings.EngineYoloV8, "yolov8s-cls.pt", "Note.Classification");
                Add("yolo11s-cls", "YOLO11s-cls", PythonModelSettings.EngineYolo11, "yolo11s-cls.pt", "Note.Classification");
            }
            else
            {
                Add("yolov5s", "YOLOv5s", PythonModelSettings.EngineYoloV5, "yolov5s.pt", "Note.Balanced");
                Add("yolov8n", "YOLOv8n", PythonModelSettings.EngineYoloV8, "yolov8n.pt", "Note.Small");
                Add("yolov8s", "YOLOv8s", PythonModelSettings.EngineYoloV8, "yolov8s.pt", "Note.Balanced");
                Add("yolo11n", "YOLO11n", PythonModelSettings.EngineYolo11, "yolo11n.pt", "Note.Small");
                Add("yolo11s", "YOLO11s", PythonModelSettings.EngineYolo11, "yolo11s.pt", "Note.Balanced");
                Add("yolo11m", "YOLO11m", PythonModelSettings.EngineYolo11, "yolo11m.pt", "Note.Larger");
            }
            return models;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Workspace.RefreshRequested -= Refresh;
        }
    }

    internal sealed class TrainingWorkspaceWorkflowAdapterContext
    {
        internal WpfLabelingShellViewModels ViewModels { get; init; }
        internal TrainingRuntimeWorkflowService Runtime { get; init; }
        internal BulkObservableCollection<WpfImageQueueItem> ImageQueueItems { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> RecipeNameProvider { get; init; }
        internal Func<string> RepositoryRootProvider { get; init; }
        internal Func<PythonCommunicationStatus> StatusProvider { get; init; }
        internal Func<PythonModelRuntimeState> RuntimeStateProvider { get; init; }
        internal Func<bool> IsCloseApproved { get; init; }
        internal Func<LabelingProjectData, string, bool> SaveMetadata { get; init; }
        internal Action UpdateCommandState { get; init; }
        internal Action RefreshComparison { get; init; }
        internal Action OpenDataset { get; init; }
        internal Action OpenEnvironment { get; init; }
        internal Action<WpfModelRegistryHistoryItem> OpenComparison { get; init; }
        internal Action<WpfModelRegistryHistoryItem> OpenInference { get; init; }
    }
}
