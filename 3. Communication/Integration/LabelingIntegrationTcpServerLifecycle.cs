using OpenVisionLab.Integration.Transport.Tcp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._3._Communication.Integration
{
    /// <summary>
    /// Owns the mutable listener lifetime for the Labeling TCP exchange.
    /// Start, stop, and dispose are serialized so the exchange facade cannot
    /// lose a server when lifecycle calls overlap.
    /// </summary>
    internal sealed class LabelingIntegrationTcpServerLifecycle : IAsyncDisposable
    {
        private readonly object stateLock = new();
        private readonly SemaphoreSlim transitionGate = new(1, 1);
        private readonly string exchangeRoot;
        private readonly TcpIntegrationOptions options;
        private TcpIntegrationServer server;
        private bool disposed;

        internal LabelingIntegrationTcpServerLifecycle(
            string exchangeRoot,
            TcpIntegrationOptions options)
        {
            this.exchangeRoot = exchangeRoot;
            this.options = options;
        }

        internal IPEndPoint LocalEndpoint
        {
            get
            {
                lock (stateLock)
                {
                    return server?.LocalEndpoint;
                }
            }
        }

        internal async Task StartAsync(
            IPAddress listenAddress,
            int port,
            byte[] sharedKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listenAddress);
            ArgumentNullException.ThrowIfNull(sharedKey);
            await transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            TcpIntegrationServer candidate = null;
            try
            {
                lock (stateLock)
                {
                    ThrowIfDisposed();
                    if (server != null)
                    {
                        throw new InvalidOperationException(
                            "The Labeling integration TCP server is already started.");
                    }
                }

                candidate = new TcpIntegrationServer(
                    LabelingIntegrationExchange.ApplicationId,
                    exchangeRoot,
                    listenAddress,
                    port,
                    sharedKey,
                    options);
                try
                {
                    await candidate.StartAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await candidate.DisposeAsync().ConfigureAwait(false);
                    candidate = null;
                    throw;
                }

                lock (stateLock)
                {
                    ThrowIfDisposed();
                    server = candidate;
                    candidate = null;
                }
            }
            finally
            {
                if (candidate != null)
                {
                    await candidate.DisposeAsync().ConfigureAwait(false);
                }

                transitionGate.Release();
            }
        }

        internal async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            TcpIntegrationServer activeServer;
            try
            {
                lock (stateLock)
                {
                    ThrowIfDisposed();
                    activeServer = server;
                    server = null;
                }

                if (activeServer == null)
                {
                    return;
                }

                try
                {
                    await activeServer.StopAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await activeServer.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                transitionGate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await transitionGate.WaitAsync().ConfigureAwait(false);
            TcpIntegrationServer activeServer;
            try
            {
                lock (stateLock)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    activeServer = server;
                    server = null;
                }

                if (activeServer != null)
                {
                    await activeServer.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                transitionGate.Release();
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
