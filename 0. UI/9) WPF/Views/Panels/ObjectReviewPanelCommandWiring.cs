using OpenVisionLab.Mvvm;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Composition-only adapter for Object Review. Review policy, mutable
    // metadata state, and persistence remain owned by the existing ViewModel,
    // workflow service, and Shell result callbacks.
    internal sealed class ObjectReviewPanelCommandWiring
    {
        private readonly ObjectReviewPanelCommandWiringContext context;

        internal ObjectReviewPanelCommandWiring(ObjectReviewPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureObjectReviewPanelCommands()
        {
            context.ObjectReviewViewModel.ConfigureCommands(
                context.ExecuteDeleteObjectCommand,
                context.ExecuteApplyObjectClassCommand,
                context.ExecuteMarkQualityUnreviewedCommand,
                context.ExecuteMarkQualityNeedsFixCommand,
                context.ExecuteMarkQualityReviewedCommand,
                context.ExecuteExportQualityReviewReportCommand,
                context.ExecuteObjectSelectionChangedCommand,
                context.ExecuteObjectPreviewKeyDownCommand,
                context.ExecuteMergeSelectedSegmentsCommand,
                mergeSelectionChanged: null,
                beginVerticalSplit: context.ExecuteBeginVerticalSegmentationSplitCommand,
                beginHorizontalSplit: context.ExecuteBeginHorizontalSegmentationSplitCommand,
                cancelSplit: context.ExecuteCancelSegmentationSplitCommand,
                beginAddHole: context.ExecuteBeginAddSegmentationHoleCommand,
                beginRemoveHole: context.ExecuteBeginRemoveSegmentationHoleCommand,
                cancelHoleEdit: context.ExecuteCancelSegmentationHoleEditCommand,
                beginInsertVertex: context.ExecuteBeginInsertPolygonVertexCommand,
                beginDeleteVertex: context.ExecuteBeginDeletePolygonVertexCommand,
                cancelVertexEdit: context.ExecuteCancelPolygonVertexEditCommand,
                beginIntelligentScissors: context.ExecuteBeginIntelligentScissorsCommand,
                applyIntelligentScissors: context.ExecuteApplyIntelligentScissorsCommand,
                cancelIntelligentScissors: context.ExecuteCancelIntelligentScissorsCommand,
                sendToBack: context.ExecuteSendSegmentationToBackCommand,
                sendBackward: context.ExecuteSendSegmentationBackwardCommand,
                bringForward: context.ExecuteBringSegmentationForwardCommand,
                bringToFront: context.ExecuteBringSegmentationToFrontCommand,
                previewRemoveUnderlying: context.ExecutePreviewSegmentationRemoveUnderlyingCommand,
                applyRemoveUnderlying: context.ExecuteApplySegmentationRemoveUnderlyingCommand,
                cancelRemoveUnderlying: context.ExecuteCancelSegmentationRemoveUnderlyingCommand,
                toggleObjectHidden: null,
                toggleObjectLocked: null,
                toggleObjectPinned: null,
                togglePersistentOccluded: null,
                togglePersistentTag: null,
                resetRecipeMetadataTags: null,
                beginGroupSelection: null,
                cancelGroupSelection: null,
                createGroup: null,
                groupSelectionChanged: null,
                removeSelectedFromGroup: null,
                dissolveSelectedGroup: null,
                toggleGroupOccluded: null,
                toggleGroupTag: null);
            context.ObjectReviewViewModel.ConfigureGroupSelectionWorkflow(context.ObjectReviewWorkflowService);
            context.ObjectReviewViewModel.ConfigureObjectDeleteShortcutWorkflow(context.DeleteSelectedObject);
            context.ObjectReviewViewModel.ConfigureObjectSessionStateWorkflow(
                context.ApplyObjectSessionStateMutation,
                context.ReportObjectReviewStatus);
            context.ObjectReviewViewModel.ConfigurePersistentOccludedWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.ApplyObjectPersistentOccludedMutation,
                context.ReportObjectReviewWorkflowError);
            context.ObjectReviewViewModel.ConfigurePersistentTagWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.GetApplicationData,
                context.GetCurrentRecipeName,
                context.ApplyObjectPersistentTagMutation,
                context.ReportObjectReviewWorkflowError);
            context.ObjectReviewViewModel.ConfigureRecipeMetadataResetWorkflow(
                context.GetApplicationData,
                context.GetCurrentRecipeName,
                context.ApplyObjectRecipeMetadataResetMutation,
                context.ReportObjectReviewWorkflowError);
            context.ObjectReviewViewModel.ConfigureGroupCreationWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.ApplyCreatedObjectGroupMutation,
                context.ReportObjectReviewStatus);
            context.ObjectReviewViewModel.ConfigureGroupOccludedWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.ApplyObjectGroupOccludedMutation,
                context.ReportObjectReviewStatus);
            context.ObjectReviewViewModel.ConfigureGroupMemberRemovalWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.ApplyObjectGroupMemberRemovalMutation,
                context.ReportObjectReviewStatus);
            context.ObjectReviewViewModel.ConfigureGroupTagWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.GetApplicationData,
                context.GetCurrentRecipeName,
                context.ApplyObjectGroupTagMutation,
                context.ReportObjectReviewStatus);
            context.ObjectReviewViewModel.ConfigureGroupDissolveWorkflow(
                context.CaptureObjectReviewSnapshots,
                context.ConfirmObjectGroupDissolve,
                context.ApplyObjectGroupDissolveMutation,
                context.ReportObjectReviewStatus);
            context.RefreshAttachedCommandBindings(
                context.ObjectListBox,
                new[]
                {
                    InputCommandBehaviors.SelectedItemChangedCommandProperty,
                    InputCommandBehaviors.PreviewKeyInputCommandProperty
                });
        }
    }

    internal sealed class ObjectReviewPanelCommandWiringContext
    {
        internal WpfObjectReviewPanelViewModel ObjectReviewViewModel { get; init; }
        internal Action ExecuteDeleteObjectCommand { get; init; }
        internal Action ExecuteApplyObjectClassCommand { get; init; }
        internal Action ExecuteMarkQualityUnreviewedCommand { get; init; }
        internal Action ExecuteMarkQualityNeedsFixCommand { get; init; }
        internal Action ExecuteMarkQualityReviewedCommand { get; init; }
        internal Action ExecuteExportQualityReviewReportCommand { get; init; }
        internal Action<object> ExecuteObjectSelectionChangedCommand { get; init; }
        internal Action<KeyInputCommandArgs> ExecuteObjectPreviewKeyDownCommand { get; init; }
        internal Action ExecuteMergeSelectedSegmentsCommand { get; init; }
        internal Action ExecuteBeginVerticalSegmentationSplitCommand { get; init; }
        internal Action ExecuteBeginHorizontalSegmentationSplitCommand { get; init; }
        internal Action ExecuteCancelSegmentationSplitCommand { get; init; }
        internal Action ExecuteBeginAddSegmentationHoleCommand { get; init; }
        internal Action ExecuteBeginRemoveSegmentationHoleCommand { get; init; }
        internal Action ExecuteCancelSegmentationHoleEditCommand { get; init; }
        internal Action ExecuteBeginInsertPolygonVertexCommand { get; init; }
        internal Action ExecuteBeginDeletePolygonVertexCommand { get; init; }
        internal Action ExecuteCancelPolygonVertexEditCommand { get; init; }
        internal Action ExecuteBeginIntelligentScissorsCommand { get; init; }
        internal Action ExecuteApplyIntelligentScissorsCommand { get; init; }
        internal Action ExecuteCancelIntelligentScissorsCommand { get; init; }
        internal Action ExecuteSendSegmentationToBackCommand { get; init; }
        internal Action ExecuteSendSegmentationBackwardCommand { get; init; }
        internal Action ExecuteBringSegmentationForwardCommand { get; init; }
        internal Action ExecuteBringSegmentationToFrontCommand { get; init; }
        internal Action ExecutePreviewSegmentationRemoveUnderlyingCommand { get; init; }
        internal Action ExecuteApplySegmentationRemoveUnderlyingCommand { get; init; }
        internal Action ExecuteCancelSegmentationRemoveUnderlyingCommand { get; init; }
        internal ObjectReviewWorkflowService ObjectReviewWorkflowService { get; init; }
        internal Func<bool> DeleteSelectedObject { get; init; }
        internal Func<WpfObjectReviewItemRef, WpfObjectSessionStateKind, WpfObjectSessionState> ApplyObjectSessionStateMutation { get; init; }
        internal Action<string> ReportObjectReviewStatus { get; init; }
        internal Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> CaptureObjectReviewSnapshots { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectPersistentOccludedMutation { get; init; }
        internal Action<WpfObjectReviewMutationResult> ReportObjectReviewWorkflowError { get; init; }
        internal Func<LabelingProjectData> GetApplicationData { get; init; }
        internal Func<string> GetCurrentRecipeName { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectPersistentTagMutation { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectRecipeMetadataResetMutation { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyCreatedObjectGroupMutation { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectGroupOccludedMutation { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectGroupMemberRemovalMutation { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectGroupTagMutation { get; init; }
        internal Func<int, bool> ConfirmObjectGroupDissolve { get; init; }
        internal Func<WpfObjectReviewMutationResult, bool> ApplyObjectGroupDissolveMutation { get; init; }
        internal ListBox ObjectListBox { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
    }
}
