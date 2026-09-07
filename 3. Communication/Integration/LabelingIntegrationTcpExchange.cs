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
        private TcpIntegrationServer server;
        private bool disposed;

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
        }

        public string ExchangeRoot => exchangeRoot;

        public IPEndPoint LocalEndpoint => server?.LocalEndpoint;

        public async Task StartAsync(
            IPAddress listenAddress,
            int port,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (server != null)
            {
                throw new InvalidOperationException(
                    "The Labeling integration TCP server is already started.");
            }

            var candidate = new TcpIntegrationServer(
                LabelingIntegrationExchange.ApplicationId,
                exchangeRoot,
                listenAddress,
                port,
                sharedKey,
                options);
            try
            {
                await candidate.StartAsync(cancellationToken).ConfigureAwait(false);
                server = candidate;
            }
            catch
            {
                await candidate.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            TcpIntegrationServer activeServer = server;
            if (activeServer == null)
            {
                return;
            }

            server = null;
            try
            {
                await activeServer.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await activeServer.DisposeAsync().ConfigureAwait(false);
            }
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
            if (disposed)
            {
                return;
            }

            TcpIntegrationServer activeServer = server;
            server = null;
            try
            {
                if (activeServer != null)
                {
                    await activeServer.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sharedKey);
                disposed = true;
            }
        }

        private TcpIntegrationClient CreateClient(TcpIntegrationEndpoint endpoint)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new TcpIntegrationClient(
                LabelingIntegrationExchange.ApplicationId,
                endpoint,
                sharedKey,
                options);
        }
    }
}
