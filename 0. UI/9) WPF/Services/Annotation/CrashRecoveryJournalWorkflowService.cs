using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the crash-recovery journal lifecycle around a WPF live-state adapter.
    /// Scheduling, revision invalidation, close/discard gating, and suppression stay
    /// here; the Shell supplies only a deferred callback and a current draft capture.
    /// </summary>
    public sealed class CrashRecoveryJournalWorkflowService : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly CrashRecoveryJournalWriteCoordinator writeCoordinator;
        private readonly Action<Action> scheduleDeferredWork;
        private long captureVersion;
        private int suppressionDepth;
        private bool closeApproved;
        private bool disposed;

        public CrashRecoveryJournalWorkflowService(
            CrashRecoveryJournalService journalService,
            Action<Action> scheduleDeferredWork)
        {
            ArgumentNullException.ThrowIfNull(journalService);
            this.scheduleDeferredWork = scheduleDeferredWork
                ?? throw new ArgumentNullException(nameof(scheduleDeferredWork));
            writeCoordinator = new CrashRecoveryJournalWriteCoordinator(journalService);
        }

        public event EventHandler<CrashRecoveryJournalWriteFailedEventArgs> WriteFailed
        {
            add => writeCoordinator.WriteFailed += value;
            remove => writeCoordinator.WriteFailed -= value;
        }

        public event EventHandler<CrashRecoveryJournalCaptureFailedEventArgs> CaptureFailed;

        public bool ScheduleWrite(
            bool hasActiveImage,
            bool hasDirtyAnnotations,
            Func<bool> hasPendingCommitWork,
            Func<WpfCrashRecoveryDraft> captureDraft)
        {
            ArgumentNullException.ThrowIfNull(hasPendingCommitWork);
            ArgumentNullException.ThrowIfNull(captureDraft);

            long scheduledVersion;
            lock (syncRoot)
            {
                if (disposed
                    || closeApproved
                    || suppressionDepth > 0
                    || !hasActiveImage
                    || !hasDirtyAnnotations)
                {
                    return false;
                }

                scheduledVersion = ++captureVersion;
            }

            try
            {
                scheduleDeferredWork(() => ApplyScheduledWrite(
                    scheduledVersion,
                    hasPendingCommitWork,
                    captureDraft));
                return true;
            }
            catch (Exception ex)
            {
                RaiseCaptureFailed(ex);
                return false;
            }
        }

        public IDisposable SuppressCapture()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return NoopScope.Instance;
                }

                suppressionDepth++;
                ++captureVersion;
                return new SuppressionScope(this);
            }
        }

        public void ApproveClose()
        {
            lock (syncRoot)
            {
                if (disposed || closeApproved)
                {
                    return;
                }

                closeApproved = true;
                ++captureVersion;
            }
        }

        public void Discard()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                ++captureVersion;
                writeCoordinator.Discard();
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                ++captureVersion;
                writeCoordinator.Dispose();
            }
        }

        private void ApplyScheduledWrite(
            long scheduledVersion,
            Func<bool> hasPendingCommitWork,
            Func<WpfCrashRecoveryDraft> captureDraft)
        {
            if (!IsCurrent(scheduledVersion))
            {
                return;
            }

            bool hasPendingWork;
            try
            {
                hasPendingWork = hasPendingCommitWork();
            }
            catch (Exception ex)
            {
                RaiseCaptureFailed(ex);
                return;
            }

            if (hasPendingWork || !IsCurrent(scheduledVersion))
            {
                return;
            }

            WpfCrashRecoveryDraft draft;
            try
            {
                draft = captureDraft();
            }
            catch (Exception ex)
            {
                RaiseCaptureFailed(ex);
                return;
            }

            if (draft == null || !IsCurrent(scheduledVersion))
            {
                return;
            }

            writeCoordinator.QueueWrite(draft);
        }

        private bool IsCurrent(long scheduledVersion)
        {
            lock (syncRoot)
            {
                return !disposed
                    && !closeApproved
                    && suppressionDepth == 0
                    && scheduledVersion == captureVersion;
            }
        }

        private void EndSuppression()
        {
            lock (syncRoot)
            {
                if (suppressionDepth <= 0)
                {
                    return;
                }

                suppressionDepth--;
                ++captureVersion;
            }
        }

        private void RaiseCaptureFailed(Exception exception)
        {
            CrashRecoveryJournalCaptureFailedEventArgs failure =
                new CrashRecoveryJournalCaptureFailedEventArgs(exception);
            try
            {
                CaptureFailed?.Invoke(this, failure);
            }
            catch (Exception eventException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Crash recovery journal capture failure notification failed: {eventException.Message}");
            }
        }

        private sealed class SuppressionScope : IDisposable
        {
            private CrashRecoveryJournalWorkflowService owner;

            public SuppressionScope(CrashRecoveryJournalWorkflowService owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                CrashRecoveryJournalWorkflowService currentOwner = owner;
                owner = null;
                currentOwner?.EndSuppression();
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new NoopScope();

            public void Dispose()
            {
            }
        }
    }

    public sealed class CrashRecoveryJournalCaptureFailedEventArgs : EventArgs
    {
        public CrashRecoveryJournalCaptureFailedEventArgs(Exception exception)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public Exception Exception { get; }

        public string Message => Exception.Message;
    }
}
