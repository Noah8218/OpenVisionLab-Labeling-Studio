using System;
using System.Threading;
using System.Threading.Tasks;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns Recipe-apply cancellation and close invalidation for the WPF Shell.
    /// ProjectRecipeSessionService remains the Core load, ordering, and commit owner.
    /// </summary>
    public sealed class ProjectRecipeApplyWorkflowService : IDisposable
    {
        private readonly ProjectRecipeSessionService projectRecipeSessionService;
        private readonly CancellationTokenSource closeCancellation = new CancellationTokenSource();
        private int closeApproved;
        private int disposed;

        public ProjectRecipeApplyWorkflowService(ProjectRecipeSessionService projectRecipeSessionService)
        {
            this.projectRecipeSessionService = projectRecipeSessionService
                ?? throw new ArgumentNullException(nameof(projectRecipeSessionService));
        }

        public bool IsClosed => Volatile.Read(ref closeApproved) != 0;

        public bool CanContinue => !IsClosed;

        public async Task<ProjectRecipeApplyResult> ApplyAsync(
            LabelingApplicationState application,
            string recipeName)
        {
            if (!TryGetCancellationToken(out CancellationToken cancellationToken))
            {
                return ProjectRecipeApplyResult.Closed;
            }

            try
            {
                string previousRecipeName = await projectRecipeSessionService.ApplyAsync(
                    application,
                    recipeName,
                    cancellationToken);
                return CanContinue
                    ? ProjectRecipeApplyResult.Applied(previousRecipeName)
                    : ProjectRecipeApplyResult.Closed;
            }
            catch (OperationCanceledException)
            {
                return ProjectRecipeApplyResult.Cancelled;
            }
        }

        public bool TryApplyPrepared(
            LabelingApplicationState application,
            string recipeName,
            LabelingProjectData preparedData,
            out string previousRecipeName)
        {
            previousRecipeName = string.Empty;
            if (!CanContinue)
            {
                return false;
            }

            try
            {
                previousRecipeName = projectRecipeSessionService.ApplyPrepared(
                    application,
                    recipeName,
                    preparedData);
                return CanContinue;
            }
            catch (OperationCanceledException)
            {
                previousRecipeName = string.Empty;
                return false;
            }
        }

        public bool TryGetCancellationToken(out CancellationToken cancellationToken)
        {
            if (!CanContinue)
            {
                cancellationToken = default;
                return false;
            }

            cancellationToken = closeCancellation.Token;
            return true;
        }

        public void ApproveClose()
        {
            if (Interlocked.Exchange(ref closeApproved, 1) != 0)
            {
                return;
            }

            closeCancellation.Cancel();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            ApproveClose();
            closeCancellation.Dispose();
        }
    }

    public sealed class ProjectRecipeApplyResult
    {
        private ProjectRecipeApplyResult(
            bool isApplied,
            bool isCancelled,
            bool isClosed,
            string previousRecipeName)
        {
            IsApplied = isApplied;
            IsCancelled = isCancelled;
            IsClosed = isClosed;
            PreviousRecipeName = previousRecipeName ?? string.Empty;
        }

        public static ProjectRecipeApplyResult Applied(string previousRecipeName)
            => new ProjectRecipeApplyResult(
                isApplied: true,
                isCancelled: false,
                isClosed: false,
                previousRecipeName: previousRecipeName);

        public static ProjectRecipeApplyResult Cancelled { get; } = new ProjectRecipeApplyResult(
            isApplied: false,
            isCancelled: true,
            isClosed: false,
            previousRecipeName: string.Empty);

        public static ProjectRecipeApplyResult Closed { get; } = new ProjectRecipeApplyResult(
            isApplied: false,
            isCancelled: false,
            isClosed: true,
            previousRecipeName: string.Empty);

        public bool IsApplied { get; }

        public bool IsCancelled { get; }

        public bool IsClosed { get; }

        public string PreviousRecipeName { get; }
    }
}
