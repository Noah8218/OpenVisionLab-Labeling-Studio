using OpenVisionLab;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    internal sealed class WorkflowTransitionApproval
    {
        internal WorkflowTransitionApproval(bool isAllowed, bool discardAnnotations = false, bool discardSettings = false)
        {
            IsAllowed = isAllowed;
            DiscardAnnotations = discardAnnotations;
            DiscardSettings = discardSettings;
        }

        internal bool IsAllowed { get; }
        internal bool DiscardAnnotations { get; }
        internal bool DiscardSettings { get; }

        internal static WorkflowTransitionApproval Denied { get; } = new WorkflowTransitionApproval(false);
        internal static WorkflowTransitionApproval Allowed { get; } = new WorkflowTransitionApproval(true);
    }

    // Shared admission for stage movement and recipe replacement. Existing adapters retain
    // the actual save, reload, and recipe-session mutations.
    internal sealed class WorkflowTransitionAdapter
    {
        private readonly WorkflowTransitionAdapterContext context;
        private bool isDeciding;

        internal WorkflowTransitionAdapter(WorkflowTransitionAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal WorkflowTransitionApproval TryApprove(string destination, bool replacesRecipe, bool allowCapturedTrainingBrowse = false, bool closesRecipe = false)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true || isDeciding)
            {
                return WorkflowTransitionApproval.Denied;
            }

            if (context.IsOperationRunning?.Invoke() == true && !(allowCapturedTrainingBrowse && !replacesRecipe && context.CanBrowseCapturedTrainingQueue?.Invoke() == true))
            {
                context.ShowBlocked?.Invoke(
                    Text("Block.BusyTitle"),
                    Text("Block.BusyMessage"),
                    string.Join(Environment.NewLine, context.ActiveWorkNamesProvider?.Invoke() ?? Array.Empty<string>()));
                return WorkflowTransitionApproval.Denied;
            }

            if (context.HasDraft?.Invoke() == true)
            {
                context.ShowBlocked?.Invoke(
                    Text("Block.DraftTitle"),
                    Text("Block.DraftMessage"),
                    context.DraftReasonProvider?.Invoke() ?? string.Empty);
                return WorkflowTransitionApproval.Denied;
            }

            int pendingCandidates = Math.Max(0, context.PendingCandidateCountProvider?.Invoke() ?? 0);
            if (replacesRecipe && pendingCandidates > 0)
            {
                context.ShowBlocked?.Invoke(
                    Text("Block.CandidatesTitle"),
                    Text("Block.CandidatesMessage"),
                    Text("Block.CandidatesDetails", pendingCandidates));
                return WorkflowTransitionApproval.Denied;
            }

            bool hasUnsavedAnnotations = context.HasUnsavedAnnotations?.Invoke() == true;
            bool hasUnsavedSettings = context.HasUnsavedSettings?.Invoke() == true;
            if (!hasUnsavedAnnotations && !hasUnsavedSettings)
            {
                return WorkflowTransitionApproval.Allowed;
            }

            isDeciding = true;
            try
            {
                WpfMessageDialogResult decision = context.ShowDirtyPrompt?.Invoke(
                    Text("Title"),
                    replacesRecipe ? Text(closesRecipe ? "CloseRecipeMessage" : "RecipeMessage") : Text("Message", destination ?? string.Empty),
                    BuildDirtyDetails(hasUnsavedAnnotations, hasUnsavedSettings)) ?? WpfMessageDialogResult.Cancel;

                if (decision == WpfMessageDialogResult.Yes)
                {
                    if (hasUnsavedAnnotations && context.SaveAnnotations?.Invoke() != true)
                    {
                        context.ShowTransitionFailure?.Invoke(
                            Text("SaveFailedTitle"),
                            Text("SaveFailedMessage", Text("AnnotationName")));
                        return WorkflowTransitionApproval.Denied;
                    }

                    if (hasUnsavedSettings && context.SaveSettings?.Invoke() != true)
                    {
                        context.ShowTransitionFailure?.Invoke(
                            Text("SaveFailedTitle"),
                            Text("SaveFailedMessage", Text("SettingsName")));
                        return WorkflowTransitionApproval.Denied;
                    }

                    context.RefreshCommandState?.Invoke();
                    return WorkflowTransitionApproval.Allowed;
                }

                if (decision != WpfMessageDialogResult.No)
                {
                    return WorkflowTransitionApproval.Denied;
                }

                if (!replacesRecipe && hasUnsavedAnnotations && pendingCandidates > 0)
                {
                    context.ShowBlocked?.Invoke(
                        Text("Block.CandidatesTitle"),
                        Text("Block.DiscardCandidatesMessage"),
                        Text("Block.CandidatesDetails", pendingCandidates));
                    return WorkflowTransitionApproval.Denied;
                }

                return new WorkflowTransitionApproval(
                    isAllowed: true,
                    discardAnnotations: hasUnsavedAnnotations,
                    discardSettings: hasUnsavedSettings);
            }
            finally
            {
                isDeciding = false;
            }
        }

        internal bool ApplyDiscard(WorkflowTransitionApproval approval)
        {
            if (approval?.IsAllowed != true)
            {
                return false;
            }

            if (approval.DiscardSettings && context.DiscardSettings?.Invoke() != true)
            {
                context.ShowTransitionFailure?.Invoke(
                    Text("DiscardFailedTitle"),
                    Text("DiscardFailedMessage", Text("SettingsName")));
                return false;
            }

            if (approval.DiscardAnnotations && context.DiscardAnnotations?.Invoke() != true)
            {
                context.ShowTransitionFailure?.Invoke(
                    Text("DiscardFailedTitle"),
                    Text("DiscardFailedMessage", Text("AnnotationName")));
                return false;
            }

            context.RefreshCommandState?.Invoke();
            return true;
        }

        internal void RefreshCommandState() => context.RefreshCommandState?.Invoke();

        private static string BuildDirtyDetails(bool hasUnsavedAnnotations, bool hasUnsavedSettings)
        {
            var details = new List<string>();
            if (hasUnsavedAnnotations)
            {
                details.Add(Text("AnnotationDirty"));
            }

            if (hasUnsavedSettings)
            {
                details.Add(Text("SettingsDirty"));
            }

            return string.Join(Environment.NewLine, details);
        }

        private static string Text(string key, params object[] arguments)
        {
            string value = OpenVisionLanguageService.T("ShellTransition." + key);
            return arguments == null || arguments.Length == 0
                ? value
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, value, arguments);
        }
    }

    internal sealed class WorkflowTransitionAdapterContext
    {
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsOperationRunning { get; init; }
        internal Func<bool> CanBrowseCapturedTrainingQueue { get; init; }
        internal Func<bool> HasDraft { get; init; }
        internal Func<string> DraftReasonProvider { get; init; }
        internal Func<IReadOnlyList<string>> ActiveWorkNamesProvider { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Func<bool> HasUnsavedAnnotations { get; init; }
        internal Func<bool> HasUnsavedSettings { get; init; }
        internal Func<bool> SaveAnnotations { get; init; }
        internal Func<bool> SaveSettings { get; init; }
        internal Func<bool> DiscardAnnotations { get; init; }
        internal Func<bool> DiscardSettings { get; init; }
        internal Func<string, string, string, WpfMessageDialogResult> ShowDirtyPrompt { get; init; }
        internal Action<string, string, string> ShowBlocked { get; init; }
        internal Action<string, string> ShowTransitionFailure { get; init; }
        internal Action RefreshCommandState { get; init; }
    }
}
