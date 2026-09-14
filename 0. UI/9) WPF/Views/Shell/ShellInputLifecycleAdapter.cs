using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrawingBitmap = System.Drawing.Bitmap;

namespace MvcVisionSystem
{
    // Owns shell input, deferred startup, close approval, and ordered resource
    // release. WPF event hookup and visual-tree access stay in the composition root.
    internal sealed class ShellInputLifecycleAdapter
    {
        private readonly ShellInputLifecycleAdapterContext context;
        private bool isApplicationClosePromptOpen;

        internal ShellInputLifecycleAdapter(ShellInputLifecycleAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.ApplicationClosePolicyService == null) throw new ArgumentNullException(nameof(context.ApplicationClosePolicyService));
            if (context.ApplicationState == null) throw new ArgumentNullException(nameof(context.ApplicationState));
            if (context.CandidateReviewState == null) throw new ArgumentNullException(nameof(context.CandidateReviewState));
            if (context.AnnotationDirtyState == null) throw new ArgumentNullException(nameof(context.AnnotationDirtyState));
        }

        private bool isApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;

        #region ShellInputCommands
        // Shell-level keyboard shortcuts stay behind one explicit adapter boundary.
        private void ExecuteShellPreviewKeyDownCommand(KeyInputCommandArgs e)
            => context.ShellKeyboardShortcutAdapter?.Execute(e);
        #endregion

        #region ShellLifecycle
        private void ExecuteLoadedCommand()
        {
            context.ScheduleDeferredStartup?.Invoke(ApplyDeferredShellStartup);
        }

        private void ApplyDeferredShellStartup()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            context.RefreshYoloStatus?.Invoke();
            _ = context.RefreshYoloSettingsPanelAsync?.Invoke();
            if (context.TryHandleCrashRecoveryOnStartup?.Invoke() != true)
            {
                context.TryLoadStartupSampleImage?.Invoke();
            }

            context.SetPythonStatus?.Invoke(context.InferenceWaitingTextProvider?.Invoke() ?? string.Empty);
            context.AppendLog?.Invoke("시작 완료. 추론은 사용자가 명시적으로 실행할 때만 시작합니다.");
        }

        private void LanguageViewModel_LanguageChanged(object sender, EventArgs e)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (context.IsDispatcherThread?.Invoke() != true)
            {
                context.ScheduleLanguageRefresh?.Invoke(ApplyLanguageChangedOnUi);
                return;
            }

            ApplyLanguageChangedOnUi();
        }

        private void ApplyLanguageChangedOnUi()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            context.RefreshLocalizedPresentation?.Invoke();
        }

        private bool TryApproveApplicationClose()
        {
            if (isApplicationClosePromptOpen)
            {
                return false;
            }

            ApplicationClosePlan plan = BuildApplicationClosePlan();
            if (!plan.RequiresPrompt)
            {
                ApproveCrashRecoveryClose();
                return true;
            }

            isApplicationClosePromptOpen = true;
            try
            {
                WpfApplicationCloseDecision decision = ShowApplicationClosePrompt(plan);
                return ApplyApplicationCloseDecision(decision);
            }
            finally
            {
                isApplicationClosePromptOpen = false;
            }
        }

        private ApplicationClosePlan BuildApplicationClosePlan()
            => context.ApplicationClosePolicyService.Build(BuildApplicationCloseState());

        private ApplicationCloseState BuildApplicationCloseState()
        {
            return new ApplicationCloseState
            {
                HasUnsavedAnnotations = context.HasUnsavedAnnotations?.Invoke() == true,
                UnsavedAnnotationReason = context.UnsavedAnnotationReasonProvider?.Invoke() ?? string.Empty,
                PendingCandidateCount = context.CandidateReviewState.PendingCount,
                ActiveWorkNames = context.ApplicationClosePolicyService.GetActiveWorkNames(
                    context.ActiveWorkStateProvider?.Invoke() ?? new ApplicationCloseWorkState()),
                ActiveImagePath = context.ActiveImagePathProvider?.Invoke() ?? string.Empty
            };
        }

        private WpfApplicationCloseDecision ShowApplicationClosePrompt(ApplicationClosePlan plan)
            => context.ShowApplicationClosePrompt?.Invoke(plan) ?? WpfApplicationCloseDecision.Cancel;

        private bool ApplyApplicationCloseDecision(WpfApplicationCloseDecision decision)
        {
            if (decision == WpfApplicationCloseDecision.Cancel)
            {
                return false;
            }

            if (decision == WpfApplicationCloseDecision.SaveAndClose)
            {
                string failureDetails = string.Empty;
                bool saved;
                try
                {
                    saved = context.TrySaveCurrentAnnotations?.Invoke(out failureDetails) == true;
                }
                catch (Exception ex)
                {
                    saved = false;
                    failureDetails = ex.Message;
                }

                if (!saved)
                {
                    context.ShowSaveFailure?.Invoke(failureDetails);
                    return false;
                }
            }

            ApproveCrashRecoveryClose();
            return true;
        }

        private void ApproveCrashRecoveryClose()
        {
            context.SetApplicationCloseApproved?.Invoke();
            context.ModelComparisonWorkflowService?.ApproveClose();
            context.ExternalYoloDatasetIntakeWorkflowService?.ApproveClose();
            context.ExternalAuditWorkflowService?.ApproveClose();
            context.YoloEnvironmentWorkflowService?.Dispose();
            context.ImageDetectionWorkflowService?.Dispose();
            // Close approval ends batch application immediately, before child-window cleanup.
            context.BatchDetectionWorkflowService?.Dispose();
            context.CrashRecoveryJournalWorkflowService?.ApproveClose();
        }
        #endregion

        #region Dispose
        private void ExecuteClosedCommand()
        {
            // Invalidate queued queue-status workers before disposing the Shell-owned state.
            // A worker may still finish its label scan, but it must not persist or marshal
            // its result after the owning Shell has closed.
            context.ImageQualityReviewWorkflowService?.Dispose();
            DetachShellEventSubscriptions();
            DisposeShellPersistenceAndChildWindows();
            StopShellTimersAndQueueWorkers();
            DisposeShellOperationCancellations();
            ResetShellOperationState();
            context.ApplicationState.StopPythonModelClientConnection();
            DisposeShellVisualState();
        }

        private void DetachShellEventSubscriptions()
            => context.DetachShellEventSubscriptions?.Invoke();

        private void DisposeShellPersistenceAndChildWindows()
        {
            context.DiscardCrashRecoveryJournal?.Invoke();
            context.DetachCrashRecoveryJournalEvents?.Invoke();
            context.CrashRecoveryJournalWorkflowService?.Dispose();
            context.SaveWorkspaceLayoutSettings?.Invoke();
            context.AuxiliaryWindowHost?.Dispose();
            context.DatasetTransferWindowHost?.Dispose();
            context.PatchCoreHeatmapWindowHost?.Dispose();
        }

        private void StopShellTimersAndQueueWorkers()
        {
            context.StopInferenceStatusPulse?.Invoke();
            context.StopTrainingStatusPolling?.Invoke();
            context.MaskStrokeWorkflowAdapter?.Dispose();
            context.ShellTimers?.Dispose();
            context.ImageDecodePreloadService?.CancelAndWait(TimeSpan.FromSeconds(2));
            context.CancelImageQueueCatalogLoad?.Invoke(true);
            context.ImageQueueCatalogLoadAdapter?.Dispose();
            context.CancelImageQueueDetailRefresh?.Invoke(true);
            context.ImageQueueDetailRefreshAdapter?.Dispose();
        }

        private void DisposeShellOperationCancellations()
        {
            context.ProjectRecipeApplyWorkflowService?.Dispose();
            context.YoloRuntimeStatusAdapter?.Dispose();
            context.SmartMaskWorkflowService?.ApproveClose();
            context.SmartMaskWorkflowService?.Dispose();
            context.ImageDetectionWorkflowService?.Dispose();
            context.BatchDetectionWorkflowService?.Dispose();
            context.ModelComparisonWorkflowService?.Dispose();
            context.ExternalYoloDatasetIntakeWorkflowService?.Dispose();
            context.ExternalAuditWorkflowService?.Dispose();
            context.AnomalyClassificationEvaluationWorkflowService?.Cancel();
            context.YoloEnvironmentWorkflowService?.Dispose();
            context.TrainingCommandLifecycleService?.Dispose();
        }

        private void ResetShellOperationState()
        {
            context.AnomalyClassificationEvaluationWorkflowService?.Reset();
            context.AnomalyImageReviewSession?.Reset();
            context.TrainingRuntimeWorkflowService?.Reset();
        }

        private void DisposeShellVisualState()
        {
            context.ImageDecodeCacheService?.Clear();
            context.ImageLoadResourceService?.Clear(context.ActiveImageBitmapProvider?.Invoke());
            context.ApplicationState?.ImageWorkspace.SetActiveImage(string.Empty, string.Empty, null);
            context.ViewModels?.Dispose();
        }
        #endregion

        // These wrappers keep the existing Shell command and reflection contracts while
        // the concrete lifecycle implementation remains outside the Window partial.
        internal void ExecuteShellPreviewKeyDown(KeyInputCommandArgs e) => ExecuteShellPreviewKeyDownCommand(e);
        internal void ExecuteLoaded() => ExecuteLoadedCommand();
        internal void ApplyDeferredStartup() => ApplyDeferredShellStartup();
        internal void HandleLanguageChanged(object sender, EventArgs e) => LanguageViewModel_LanguageChanged(sender, e);
        internal void ApplyLanguageChangedOnUiForShell() => ApplyLanguageChangedOnUi();
        internal bool TryApproveClose() => TryApproveApplicationClose();
        internal ApplicationClosePlan GetApplicationClosePlan() => BuildApplicationClosePlan();
        internal ApplicationCloseState GetApplicationCloseState() => BuildApplicationCloseState();
        internal WpfApplicationCloseDecision GetApplicationClosePrompt(ApplicationClosePlan plan) => ShowApplicationClosePrompt(plan);
        internal bool ApplyCloseDecision(WpfApplicationCloseDecision decision) => ApplyApplicationCloseDecision(decision);
        internal void ApproveClose() => ApproveCrashRecoveryClose();
        internal void ExecuteClosed() => ExecuteClosedCommand();
    }

    internal delegate bool TrySaveCurrentAnnotationsDelegate(out string failureDetails);

    internal sealed class ShellInputLifecycleAdapterContext
    {
        internal ApplicationClosePolicyService ApplicationClosePolicyService { get; init; }
        internal LabelingApplicationState ApplicationState { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal ShellKeyboardShortcutAdapter ShellKeyboardShortcutAdapter { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Action<Action> ScheduleDeferredStartup { get; init; }
        internal Action RefreshYoloStatus { get; init; }
        internal Func<Task> RefreshYoloSettingsPanelAsync { get; init; }
        internal Func<bool> TryHandleCrashRecoveryOnStartup { get; init; }
        internal Func<bool> TryLoadStartupSampleImage { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Func<string> InferenceWaitingTextProvider { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<bool> IsDispatcherThread { get; init; }
        internal Action<Action> ScheduleLanguageRefresh { get; init; }
        internal Action RefreshLocalizedPresentation { get; init; }
        internal Func<bool> HasUnsavedAnnotations { get; init; }
        internal Func<string> UnsavedAnnotationReasonProvider { get; init; }
        internal Func<ApplicationCloseWorkState> ActiveWorkStateProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<ApplicationClosePlan, WpfApplicationCloseDecision> ShowApplicationClosePrompt { get; init; }
        internal TrySaveCurrentAnnotationsDelegate TrySaveCurrentAnnotations { get; init; }
        internal Action<string> ShowSaveFailure { get; init; }
        internal Action SetApplicationCloseApproved { get; init; }
        internal ModelComparisonWorkflowService ModelComparisonWorkflowService { get; init; }
        internal ExternalYoloDatasetIntakeWorkflowService ExternalYoloDatasetIntakeWorkflowService { get; init; }
        internal ExternalAuditWorkflowService ExternalAuditWorkflowService { get; init; }
        internal YoloEnvironmentWorkflowService YoloEnvironmentWorkflowService { get; init; }
        internal ImageDetectionWorkflowService ImageDetectionWorkflowService { get; init; }
        internal BatchDetectionWorkflowService BatchDetectionWorkflowService { get; init; }
        internal CrashRecoveryJournalWorkflowService CrashRecoveryJournalWorkflowService { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal Action DetachShellEventSubscriptions { get; init; }
        internal Action DiscardCrashRecoveryJournal { get; init; }
        internal Action DetachCrashRecoveryJournalEvents { get; init; }
        internal Action SaveWorkspaceLayoutSettings { get; init; }
        internal ShellAuxiliaryWindowHost AuxiliaryWindowHost { get; init; }
        internal DatasetTransferWindowHost DatasetTransferWindowHost { get; init; }
        internal PatchCoreHeatmapWindowHost PatchCoreHeatmapWindowHost { get; init; }
        internal Action StopInferenceStatusPulse { get; init; }
        internal Action StopTrainingStatusPolling { get; init; }
        internal MaskStrokeWorkflowAdapter MaskStrokeWorkflowAdapter { get; init; }
        internal ShellTimerSet ShellTimers { get; init; }
        internal ImageDecodePreloadService ImageDecodePreloadService { get; init; }
        internal Action<bool> CancelImageQueueCatalogLoad { get; init; }
        internal ImageQueueCatalogLoadAdapter ImageQueueCatalogLoadAdapter { get; init; }
        internal Action<bool> CancelImageQueueDetailRefresh { get; init; }
        internal ImageQueueDetailRefreshAdapter ImageQueueDetailRefreshAdapter { get; init; }
        internal ProjectRecipeApplyWorkflowService ProjectRecipeApplyWorkflowService { get; init; }
        internal YoloRuntimeStatusAdapter YoloRuntimeStatusAdapter { get; init; }
        internal SmartMaskWorkflowService SmartMaskWorkflowService { get; init; }
        internal AnomalyClassificationEvaluationWorkflowService AnomalyClassificationEvaluationWorkflowService { get; init; }
        internal TrainingCommandLifecycleService TrainingCommandLifecycleService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal TrainingRuntimeWorkflowService TrainingRuntimeWorkflowService { get; init; }
        internal ImageDecodeCacheService ImageDecodeCacheService { get; init; }
        internal ImageLoadResourceService ImageLoadResourceService { get; init; }
        internal Func<DrawingBitmap> ActiveImageBitmapProvider { get; init; }
        internal WpfLabelingShellViewModels ViewModels { get; init; }
    }
}
