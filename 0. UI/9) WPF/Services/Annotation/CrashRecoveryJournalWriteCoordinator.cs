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
        private long journalRevision;
        private bool disposed;

        public CrashRecoveryJournalWriteCoordinator(CrashRecoveryJournalService journalService)
        {
            this.journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
        }

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

                long revision = ++journalRevision;
                // Keep disk I/O off the caller (usually the UI) even when the previous write already completed.
                pendingWriteTask = pendingWriteTask.ContinueWith(
                    _ => WriteDraft(draft, revision),
                    CancellationToken.None,
                    TaskContinuationOptions.DenyChildAttach,
                    TaskScheduler.Default);
                return true;
            }
        }

        public void Discard()
        {
            lock (syncRoot)
            {
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
                journalService.Discard(++journalRevision);
            }
        }

        private void WriteDraft(WpfCrashRecoveryDraft draft, long revision)
        {
            try
            {
                journalService.Write(draft, revision);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Crash recovery journal write failed: {ex.Message}");
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
}
