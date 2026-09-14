using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Composition-only adapter for the image queue panel. Queue policy and mutable
    // selection/search state remain owned by WpfImageQueuePanelViewModel and the
    // existing shell workflow methods injected below.
    internal sealed class ImageQueuePanelCommandWiring
    {
        private readonly ImageQueuePanelCommandWiringContext context;

        internal ImageQueuePanelCommandWiring(ImageQueuePanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureImageQueuePanelCommands()
        {
            context.ImageQueueViewModel.ConfigureCommands(
                context.ExecuteLoadImageRootCommand,
                context.ExecuteBrowseImageFolderCommand,
                context.ExecuteOpenCurrentImageFolderCommand,
                context.ExecuteRefreshImageQueueCommand,
                context.ExecuteNextUnlabeledQueueCommand,
                context.ExecuteOpenSelectedQueueImageCommand,
                context.ExecuteDetectSelectedQueueCommand,
                context.ExecuteBatchDetectQueueCommand,
                context.ExecuteTemplateBatchQueueCommand,
                context.ExecuteRetryFailedQueueCommand,
                context.ExecuteStopBatchQueueCommand,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                context.ExecuteSelectedQueueItemChanged,
                context.ImageQueueFilterSelectionChanged,
                null,
                queueSelectionChanged: null,
                context.ExecuteOpenSelectedQueueImageCommand,
                context.ExecuteApplyAnomalyFolderStateSuggestionCommand,
                context.ExecuteDismissAnomalyFolderStateSuggestionCommand,
                context.ExecuteMarkActiveAnomalyNormalAndNextCommand,
                context.ExecuteMarkActiveAnomalyAbnormalAndNextCommand,
                context.ExecuteClearActiveAnomalyReviewCommand);
            context.ImageQueueViewModel.ConfigureFilterWorkflow(context.SetImageQueueFilter);
            context.ImageQueueViewModel.ConfigureSearchWorkflow(context.ApplyImageQueueSearchChanged);
            context.ImageQueueViewModel.ConfigureQueueSelectionWorkflow();
            context.RefreshAttachedCommandBindings(
                context.ImageQueueFilterBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.ImageQueueSearchBox,
                new[] { InputCommandBehaviors.TextInputCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.ImageQueueGrid,
                new[]
                {
                    InputCommandBehaviors.SelectedItemChangedCommandProperty,
                    InputCommandBehaviors.MouseDoubleClickInputCommandProperty
                });
            context.SeedImageQueueInputCommands();
        }
    }

    internal sealed class ImageQueuePanelCommandWiringContext
    {
        internal WpfImageQueuePanelViewModel ImageQueueViewModel { get; init; }
        internal Action ExecuteLoadImageRootCommand { get; init; }
        internal Action ExecuteBrowseImageFolderCommand { get; init; }
        internal Action ExecuteOpenCurrentImageFolderCommand { get; init; }
        internal Action ExecuteRefreshImageQueueCommand { get; init; }
        internal Action ExecuteNextUnlabeledQueueCommand { get; init; }
        internal Action ExecuteOpenSelectedQueueImageCommand { get; init; }
        internal Action ExecuteDetectSelectedQueueCommand { get; init; }
        internal Action ExecuteBatchDetectQueueCommand { get; init; }
        internal Action ExecuteTemplateBatchQueueCommand { get; init; }
        internal Action ExecuteRetryFailedQueueCommand { get; init; }
        internal Action ExecuteStopBatchQueueCommand { get; init; }
        internal Action<WpfImageQueueItem> ExecuteSelectedQueueItemChanged { get; init; }
        internal Action<object> ImageQueueFilterSelectionChanged { get; init; }
        internal Action<WpfImageQueueFilter> SetImageQueueFilter { get; init; }
        internal Action<string> ApplyImageQueueSearchChanged { get; init; }
        internal Action ExecuteApplyAnomalyFolderStateSuggestionCommand { get; init; }
        internal Action ExecuteDismissAnomalyFolderStateSuggestionCommand { get; init; }
        internal Action ExecuteMarkActiveAnomalyNormalAndNextCommand { get; init; }
        internal Action ExecuteMarkActiveAnomalyAbnormalAndNextCommand { get; init; }
        internal Action ExecuteClearActiveAnomalyReviewCommand { get; init; }
        internal ComboBox ImageQueueFilterBox { get; init; }
        internal TextBox ImageQueueSearchBox { get; init; }
        internal DataGrid ImageQueueGrid { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
        internal Action SeedImageQueueInputCommands { get; init; }
    }
}
