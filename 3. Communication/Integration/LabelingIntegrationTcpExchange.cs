using OpenVisionLab.Integration.Transport.Tcp;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._3._Communication.Integration
{
    /// <summary>
    /// Connects Labeling Studio's local immutable transaction root to the
    /// shared authenticated TCP transport. Receiving bytes never acknowledges
    /// a Handoff, starts inference, changes labels, or publishes a Result.
    /// </summary>
    public sealed class LabelingIntegrationTcpExchange : IAsyncDisposable
    {
        private readonly string exchangeRoot;
        private readonly byte[] sharedKey;
        private readonly TcpIntegrationOptions options;
        private readonly LabelingIntegrationTcpServerLifecycle serverLifecycle;
        private readonly object clientCreationLock = new();
        private int disposeStarted;

        public LabelingIntegrationTcpExchange(
            string exchangeRoot,
            ReadOnlySpan<byte> sharedKey,
            TcpIntegrationOptions options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
            if (sharedKey.Length < 32)
            {
                throw new ArgumentException(
                    "The TCP integration shared key must contain at least 32 bytes.",
                    nameof(sharedKey));
            }

            this.exchangeRoot = Path.GetFullPath(exchangeRoot);
            this.sharedKey = sharedKey.ToArray();
            this.options = options ?? new TcpIntegrationOptions();
            serverLifecycle = new LabelingIntegrationTcpServerLifecycle(this.exchangeRoot, this.options);
        }

        public string ExchangeRoot => exchangeRoot;

        public IPEndPoint LocalEndpoint => serverLifecycle.LocalEndpoint;

        public async Task StartAsync(
            IPAddress listenAddress,
            int port,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await serverLifecycle.StartAsync(
                listenAddress,
                port,
                sharedKey,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await serverLifecycle.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<TcpIntegrationTransferReceipt> PingAsync(
            TcpIntegrationEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            using TcpIntegrationClient client = CreateClient(endpoint);
            return await client.PingAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<TcpIntegrationTransferReceipt> PushTransactionAsync(
            TcpIntegrationEndpoint endpoint,
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            using TcpIntegrationClient client = CreateClient(endpoint);
            return await client.PushTransactionAsync(
                exchangeRoot,
                transactionId,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<TcpIntegrationTransferReceipt> PullTransactionAsync(
            TcpIntegrationEndpoint endpoint,
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            using TcpIntegrationClient client = CreateClient(endpoint);
            return await client.PullTransactionAsync(
                exchangeRoot,
                transactionId,
                cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposeStarted, 1) != 0)
            {
                await serverLifecycle.DisposeAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                await serverLifecycle.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                lock (clientCreationLock)
                {
                    CryptographicOperations.ZeroMemory(sharedKey);
                }
            }
        }

        private TcpIntegrationClient CreateClient(TcpIntegrationEndpoint endpoint)
        {
            lock (clientCreationLock)
            {
                ThrowIfDisposed();
                return new TcpIntegrationClient(
                    LabelingIntegrationExchange.ApplicationId,
                    endpoint,
                    sharedKey,
                    options);
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposeStarted) != 0, this);
        }
    }
}
