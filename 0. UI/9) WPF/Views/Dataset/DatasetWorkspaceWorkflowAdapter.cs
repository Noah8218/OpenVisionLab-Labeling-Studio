using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Linq;
using System.IO;

namespace MvcVisionSystem
{
    internal sealed class DatasetWorkspaceWorkflowAdapter
    {
        private readonly DatasetWorkspaceWorkflowAdapterContext context;
        private bool assigning;

        internal DatasetWorkspaceWorkflowAdapter(DatasetWorkspaceWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.ViewModels);
            ArgumentNullException.ThrowIfNull(context.PersistRecipe);
            context.ViewModels.DatasetWorkspaceViewModel.AttachCatalog(context.ImageQueueItems);
            context.ViewModels.DatasetWorkspaceViewModel.ConfigureCommands(AssignSplit, OpenImage);
            context.ViewModels.DatasetWorkspaceViewModel.ConfigureAddSourceFolderCommand(() => context.AddSourceFolder?.Invoke());
            context.ViewModels.DatasetWorkspaceViewModel.ConfigureSplitSettingsCommands(PrepareSplit, ApplySplit);
            context.ViewModels.DatasetWorkspaceViewModel.ConfigureLabelSuspicionEntry(() => context.OpenLabelSuspicionReview?.Invoke(),
                () => !assigning && context.IsCloseApproved?.Invoke() != true && context.CanOpenLabelSuspicionReview?.Invoke() == true);
            context.ViewModels.DatasetWorkspaceViewModel.RefreshRequested += Refresh;
        }

        internal void AdoptCatalog(LabelingProjectData data)
        {
            context.ViewModels.DatasetWorkspaceViewModel.SetContext(data, context.ImageRootProvider?.Invoke(),
                YoloDatasetStatisticsService.BuildClassObjectCounts(data, includeExcluded: true),
                data?.ProjectSettings?.AdditionalImageRootPaths?.Count > 0 ? data.ProjectSettings.ResolveImageRootPaths().Count(root => !Directory.Exists(root)) : 0);
            context.ViewModels.DatasetWorkspaceViewModel.SetSplitOverlap(YoloDatasetSplitQualityService.BuildAssignmentSummary(
                context.ImageQueueItems.Select(item => item.ImagePath), data?.ProjectSettings?.YoloDataset));
        }

        internal void Refresh()
        {
            if (context.IsCloseApproved?.Invoke() == true) return;
            AdoptCatalog(context.DataProvider());
            context.ViewModels.ProjectConfigViewModel.SetDatasetVersionInfo(
                RecipeDatasetVersionPresentationService.BuildCurrent(context.ViewModels.ProjectConfigViewModel.ManifestPath, context.DataProvider()));
        }

        private void AssignSplit(string split)
        {
            DatasetWorkspaceViewModel model = context.ViewModels.DatasetWorkspaceViewModel;
            if (assigning || !model.CanAssignSplit || context.IsCloseApproved?.Invoke() == true) return;
            assigning = true;
            model.IsApplyingSplit = true;
            try
            {
                int changed = DatasetSplitAssignmentService.Apply(context.DataProvider(),
                    context.ImageQueueItems.Select(item => item.ImagePath).ToArray(),
                    model.SelectedItems.Select(item => item.ImagePath).ToArray(), split,
                    () => context.PersistRecipe(context.DataProvider()));
                Refresh();
                context.RefreshTrainingReadiness?.Invoke();
                if (changed > 0) context.DatasetChanged?.Invoke();
                model.ShowAssignmentResult(changed, split);
                context.SetDatasetStatus?.Invoke(model.OperationStatus);
            }
            catch (Exception error) when (error is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or AggregateException)
            {
                model.ShowAssignmentFailure(error.Message);
                context.SetDatasetStatus?.Invoke(model.OperationStatus);
                context.AppendLog?.Invoke(error.ToString());
            }
            finally
            {
                assigning = false;
                model.IsApplyingSplit = false;
            }
        }

        private void OpenImage(string path)
        {
            if (assigning || !context.ViewModels.DatasetWorkspaceViewModel.CanOpenImage || context.IsCloseApproved?.Invoke() == true || string.IsNullOrWhiteSpace(path)) return;
            if (context.TryLoadImage?.Invoke(path) == true) context.EnterLabeling?.Invoke();
        }

        private void PrepareSplit()
        {
            DatasetWorkspaceViewModel model = context.ViewModels.DatasetWorkspaceViewModel;
            if (assigning || !model.CanConfigureSplit || context.IsCloseApproved?.Invoke() == true) return;
            try { model.SetSplitPreview(DatasetSplitAssignmentService.Prepare(context.DataProvider(), context.ImageQueueItems.Select(item => item.ImagePath).ToArray(), model.SplitDraft)); }
            catch (Exception error) when (error is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
            { model.ShowSplitSettingsFailure(error.Message); context.AppendLog?.Invoke(error.ToString()); }
        }

        private void ApplySplit()
        {
            DatasetWorkspaceViewModel model = context.ViewModels.DatasetWorkspaceViewModel;
            if (assigning || !model.CanConfigureSplit || model.SplitPreview == null || context.IsCloseApproved?.Invoke() == true) return;
            assigning = true;
            model.IsApplyingSplit = true;
            try
            {
                int changed = DatasetSplitAssignmentService.ApplyPrepared(context.DataProvider(), context.ImageQueueItems.Select(item => item.ImagePath).ToArray(), model.SplitPreview,
                    () => context.PersistRecipe(context.DataProvider()));
                Refresh();
                context.RefreshTrainingReadiness?.Invoke();
                if (changed > 0) context.DatasetChanged?.Invoke();
                model.CompleteSplitSettings(changed);
                context.SetDatasetStatus?.Invoke(model.OperationStatus);
            }
            catch (Exception error) when (error is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or AggregateException or Newtonsoft.Json.JsonException)
            { model.ShowSplitSettingsFailure(error.Message); context.SetDatasetStatus?.Invoke(model.SplitPreviewText); context.AppendLog?.Invoke(error.ToString()); }
            finally { assigning = false; model.IsApplyingSplit = false; }
        }
    }

    internal sealed class DatasetWorkspaceWorkflowAdapterContext
    {
        internal WpfLabelingShellViewModels ViewModels { get; init; }
        internal BulkObservableCollection<WpfImageQueueItem> ImageQueueItems { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> ImageRootProvider { get; init; }
        internal Func<LabelingProjectData, bool> PersistRecipe { get; init; }
        internal Func<bool> IsCloseApproved { get; init; }
        internal Func<string, bool> TryLoadImage { get; init; }
        internal Action EnterLabeling { get; init; }
        internal Action RefreshTrainingReadiness { get; init; }
        internal Action DatasetChanged { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action AddSourceFolder { get; init; }
        internal Action OpenLabelSuspicionReview { get; init; }
        internal Func<bool> CanOpenLabelSuspicionReview { get; init; }
    }
}
