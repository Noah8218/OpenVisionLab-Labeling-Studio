using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns deferred crash-recovery journal writes and revision invalidation.
    /// The Shell captures the UI snapshot; this owner serializes the journal
    /// write and prevents a queued revision from returning after discard/close.
    /// </summary>
    public class CrashRecoveryJournalWriteCoordinator : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly CrashRecoveryJournalService journalService;
        private Task pendingWriteTask = Task.CompletedTask;
        private WpfCrashRecoveryDraft pendingDraft;
        private long pendingDraftRevision;
        private long journalRevision;
        private bool writeLoopScheduled;
        private bool disposed;

        public CrashRecoveryJournalWriteCoordinator(CrashRecoveryJournalService journalService)
        {
            this.journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
        }

        public event EventHandler<CrashRecoveryJournalWriteFailedEventArgs> WriteFailed;

        public long LastSuccessfulRevision { get; private set; }

        public long LastFailedRevision { get; private set; }

        public string LastFailureMessage { get; private set; } = string.Empty;

        public bool QueueWrite(WpfCrashRecoveryDraft draft)
        {
            if (draft == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                if (disposed)
                {
                    return false;
                }

                pendingDraft = draft;
                pendingDraftRevision = ++journalRevision;
                if (!writeLoopScheduled)
                {
                    writeLoopScheduled = true;
                    // Keep disk I/O off the caller (usually the UI). Replace a queued draft while
                    // this single continuation waits or the current write is in progress.
                    pendingWriteTask = pendingWriteTask.ContinueWith(
                        _ => ProcessPendingWrites(),
                        CancellationToken.None,
                        TaskContinuationOptions.DenyChildAttach,
                        TaskScheduler.Default).Unwrap();
                }
                return true;
            }
        }

        public void Discard()
        {
            lock (syncRoot)
            {
                pendingDraft = null;
                journalService.Discard(++journalRevision);
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
                pendingDraft = null;
                journalService.Discard(++journalRevision);
            }
        }

        private Task ProcessPendingWrites()
        {
            while (true)
            {
                WpfCrashRecoveryDraft draft;
                long revision;
                lock (syncRoot)
                {
                    if (disposed || pendingDraft == null)
                    {
                        writeLoopScheduled = false;
                        return Task.CompletedTask;
                    }

                    draft = pendingDraft;
                    revision = pendingDraftRevision;
                    pendingDraft = null;
                }

                WriteDraft(draft, revision);
            }
        }

        private void WriteDraft(WpfCrashRecoveryDraft draft, long revision)
        {
            try
            {
                if (journalService.Write(draft, revision))
                {
                    lock (syncRoot)
                    {
                        LastSuccessfulRevision = revision;
                        LastFailedRevision = 0;
                        LastFailureMessage = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                CrashRecoveryJournalWriteFailedEventArgs failure =
                    new CrashRecoveryJournalWriteFailedEventArgs(revision, ex);
                lock (syncRoot)
                {
                    LastFailedRevision = revision;
                    LastFailureMessage = failure.Message;
                }
                Debug.WriteLine($"Crash recovery journal write failed: {ex.Message}");
                try
                {
                    WriteFailed?.Invoke(this, failure);
                }
                catch (Exception eventException)
                {
                    Debug.WriteLine($"Crash recovery journal failure notification failed: {eventException.Message}");
                }
            }
        }
    }

    [Obsolete("Use CrashRecoveryJournalWriteCoordinator.", false)]
    public sealed class WpfCrashRecoveryJournalWriteCoordinator : CrashRecoveryJournalWriteCoordinator
    {
        public WpfCrashRecoveryJournalWriteCoordinator(WpfCrashRecoveryJournalService journalService) : base(journalService)
        {
        }
    }

    public sealed class CrashRecoveryJournalWriteFailedEventArgs : EventArgs
    {
        public CrashRecoveryJournalWriteFailedEventArgs(long revision, Exception exception)
        {
            Revision = revision;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public long Revision { get; }

        public Exception Exception { get; }

        public string Message => Exception.Message;
    }
}
