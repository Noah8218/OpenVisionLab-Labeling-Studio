using System;
using System.Threading;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the short-lived start/stop command lifetime around the training runtime.
    /// The Shell keeps presentation and conflict-status projection; this owner keeps
    /// one active cancellation source and its disposal boundary.
    /// </summary>
    public sealed class TrainingCommandLifecycleService : IDisposable
    {
        private readonly object syncRoot = new object();
        private TrainingCommandLease activeLease;
        private bool disposed;

        public bool IsRunning
        {
            get
            {
                lock (syncRoot)
                {
                    return activeLease != null;
                }
            }
        }

        public TrainingCommandLease TryBegin(bool hasConflictingCommand)
        {
            lock (syncRoot)
            {
                if (disposed || hasConflictingCommand || activeLease != null)
                {
                    return null;
                }

                activeLease = new TrainingCommandLease();
                return activeLease;
            }
        }

        public void Complete(TrainingCommandLease lease)
        {
            if (lease == null)
            {
                return;
            }

            bool ownsLease;
            lock (syncRoot)
            {
                ownsLease = ReferenceEquals(activeLease, lease);
                if (ownsLease)
                {
                    activeLease = null;
                }
            }

            if (ownsLease)
            {
                lease.Dispose();
            }
        }

        public void Cancel()
        {
            TrainingCommandLease lease;
            lock (syncRoot)
            {
                lease = activeLease;
            }

            lease?.Cancel();
        }

        public void Dispose()
        {
            TrainingCommandLease lease;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                lease = activeLease;
                activeLease = null;
            }

            lease?.CancelAndDispose();
        }
    }

    public sealed class TrainingCommandLease : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool disposed;

        public CancellationToken Token => cancellation.Token;

        internal void Cancel()
        {
            lock (syncRoot)
            {
                if (!disposed)
                {
                    cancellation.Cancel();
                }
            }
        }

        internal void CancelAndDispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                cancellation.Cancel();
                disposed = true;
                cancellation.Dispose();
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
                cancellation.Dispose();
            }
        }
    }
}
