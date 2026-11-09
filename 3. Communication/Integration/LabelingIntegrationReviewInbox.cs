using OpenVisionLab.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._3._Communication.Integration
{
    /// <summary>
    /// Explicit local bootstrap, not a wire envelope. Start the normal product with
    /// --integration-review-inbox &lt;config.json&gt;. JSON uses camelCase properties:
    /// schema="ovl.labeling-review-inbox/1.0", exchangeRoot, workspaceRoot,
    /// consumerBuild, productAssemblySha256; optional listenAddress/listenPort/sharedKeyPath.
    /// The release/test coordinator supplies the build identity and hash of the actual
    /// product DLL. Identity is never inferred from an incoming Handoff or current Git state.
    /// Without listener settings the same inbox accepts existing local-file transfers.
    /// Results remain in the receiver exchange for the sender's existing PullTransaction path;
    /// this owner never pushes/retransmits a result to a peer.
    /// </summary>
    public sealed record LabelingIntegrationReviewInboxConfiguration(string Schema, string ExchangeRoot,
        string WorkspaceRoot, IntegrationApplicationIdentity ConsumerBuild, string ProductAssemblySha256,
        string ListenAddress = null, int ListenPort = 0, string SharedKeyPath = null);

    public sealed class LabelingIntegrationReviewInbox : IAsyncDisposable
    {
        public const string Argument = "--integration-review-inbox";
        private readonly HashSet<Guid> attempted = new();
        private readonly HashSet<string> reported = new(StringComparer.Ordinal);
        private readonly LabelingIntegrationTcpExchange transport;
        private bool disposed;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
        };

        private LabelingIntegrationReviewInbox(LabelingIntegrationReviewInboxConfiguration configuration,
            LabelingIntegrationTcpExchange transport)
        {
            Configuration = configuration;
            this.transport = transport;
        }

        public LabelingIntegrationReviewInboxConfiguration Configuration { get; }

        public static string GetConfigurationPath(IReadOnlyList<string> arguments)
        {
            if (arguments == null || !arguments.Contains(Argument, StringComparer.Ordinal)) return null;
            if (arguments.Count != 2 || arguments[0] != Argument || string.IsNullOrWhiteSpace(arguments[1]))
                throw new ArgumentException("Use --integration-review-inbox <config.json> as the only product launch option.");
            return Path.GetFullPath(arguments[1]);
        }

        public static async Task<LabelingIntegrationReviewInbox> StartAsync(string configurationPath,
            CancellationToken cancellationToken = default)
        {
            string path = Path.GetFullPath(configurationPath);
            path = LabelingIntegrationExchange.RequireContainedPath(Path.GetDirectoryName(path), Path.GetFileName(path));
            LabelingIntegrationFileSnapshot bytes = LabelingIntegrationFileSnapshot.Read(path);
            if (bytes.ByteLength > 65536) throw new InvalidDataException("Review inbox configuration is too large.");
            LabelingIntegrationReviewInboxConfiguration config = JsonSerializer.Deserialize<LabelingIntegrationReviewInboxConfiguration>(bytes.GetBytes(), JsonOptions);
            if (config?.Schema != "ovl.labeling-review-inbox/1.0" || config.ConsumerBuild?.ApplicationId != LabelingIntegrationExchange.ApplicationId
                || string.IsNullOrWhiteSpace(config.ExchangeRoot) || string.IsNullOrWhiteSpace(config.WorkspaceRoot)
                || !Path.IsPathFullyQualified(config.ExchangeRoot) || !Path.IsPathFullyQualified(config.WorkspaceRoot))
                throw new InvalidDataException("Review inbox requires an explicit build identity and absolute exchange/workspace roots.");
            Assembly product = typeof(LabelingIntegrationReviewInbox).Assembly;
            string version = product.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            using (FileStream assemblyFile = File.OpenRead(product.Location))
            {
                string digest = Convert.ToHexString(SHA256.HashData(assemblyFile));
                if (!string.Equals(digest, config.ProductAssemblySha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(version, config.ConsumerBuild.ApplicationVersion, StringComparison.Ordinal))
                    throw new InvalidDataException("Configured build identity does not match the loaded product assembly/version.");
            }
            string exchange = Path.TrimEndingDirectorySeparator(Path.GetFullPath(config.ExchangeRoot));
            string workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(config.WorkspaceRoot));
            if (exchange.Equals(workspace, StringComparison.OrdinalIgnoreCase)
                || exchange.StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || workspace.StartsWith(exchange + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Exchange and review workspaces must have separate roots.");
            LabelingIntegrationExchange.RequireContainedPath(exchange, IntegrationTransactionLayout.TransactionsDirectoryName);
            LabelingIntegrationExchange.RequireContainedPath(workspace, "probe");
            LabelingIntegrationTcpExchange transport = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(config.ListenAddress))
                {
                    if (!IPAddress.TryParse(config.ListenAddress, out IPAddress address)
                        || config.ListenPort is < 1 or > 65535 || !Path.IsPathFullyQualified(config.SharedKeyPath ?? string.Empty))
                        throw new InvalidDataException("TCP inbox requires a numeric listen address, port and absolute binary shared-key file.");
                    string keyPath = Path.GetFullPath(config.SharedKeyPath);
                    keyPath = LabelingIntegrationExchange.RequireContainedPath(Path.GetDirectoryName(keyPath), Path.GetFileName(keyPath));
                    using var keyFile = new FileStream(keyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (keyFile.Length is < 32 or > 4096) throw new InvalidDataException("Invalid integration shared-key length.");
                    byte[] key = new byte[checked((int)keyFile.Length)];
                    keyFile.ReadExactly(key);
                    try
                    {
                        if (key.Length is < 32 or > 4096) throw new InvalidDataException("Invalid integration shared-key length.");
                        transport = new LabelingIntegrationTcpExchange(exchange, key);
                        await transport.StartAsync(address, config.ListenPort, cancellationToken).ConfigureAwait(false);
                    }
                    finally { CryptographicOperations.ZeroMemory(key); }
                }
                else if (config.ListenPort != 0 || !string.IsNullOrWhiteSpace(config.SharedKeyPath))
                    throw new InvalidDataException("Partial TCP listener configuration is not accepted.");
                return new LabelingIntegrationReviewInbox(config with { ExchangeRoot = exchange, WorkspaceRoot = workspace }, transport);
            }
            catch
            {
                if (transport != null) await transport.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        // Called on the Shell dispatcher. A declined/broken request is not repeatedly opened;
        // explicit Retry admits it again after the operator resolves the Recipe or file problem.
        public IReadOnlyList<Guid> Discover(Action<string> report)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            string root = LabelingIntegrationExchange.RequireContainedPath(Configuration.ExchangeRoot, IntegrationTransactionLayout.TransactionsDirectoryName);
            if (!Directory.Exists(root)) return Array.Empty<Guid>();
            var found = new List<Guid>();
            foreach (string directory in Directory.EnumerateDirectories(root).OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!Guid.TryParseExact(Path.GetFileName(directory), "D", out Guid id) || attempted.Contains(id)) continue;
                try
                {
                    string handoffPath = LabelingIntegrationExchange.RequireContainedPath(root, id.ToString("D") + "/" + IntegrationTransactionLayout.HandoffFileName);
                    if (!File.Exists(handoffPath)) continue; // TCP transfer has not yet committed the Handoff.
                    if (new FileInfo(handoffPath).Length > 1024 * 1024) throw new InvalidDataException("Handoff envelope is too large.");
                    IntegrationHandoffV2 handoff = LabelingIntegrationExchange.ReadHandoffEnvelope(Configuration.ExchangeRoot, id);
                    if (handoff.Context.ConsumerBuild.ApplicationId != LabelingIntegrationExchange.ApplicationId) continue;
                    bool hasResult = File.Exists(LabelingIntegrationExchange.RequireContainedPath(directory, IntegrationTransactionLayout.ResultFileName));
                    bool retained = Directory.Exists(LabelingIntegrationExchange.RequireContainedPath(Configuration.WorkspaceRoot, id.ToString("D")));
                    if (hasResult && retained && IsConfirmed(id, directory)) continue;
                    if (!hasResult || retained)
                    {
                        // A partial/busy transfer is not an attempted review. Retry discovery after
                        // all immutable input bytes pass the existing validator and build correlation.
                        LabelingIntegrationExchange.ReadReviewInput(Configuration.ExchangeRoot, id, Configuration.ConsumerBuild);
                        found.Add(id);
                        break;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
                    || ex is JsonException || ex is IntegrationContractException)
                {
                    if (reported.Add(id.ToString("D") + ":" + ex.Message)) report?.Invoke("왕복 요청 확인 실패: " + id + " · " + ex.Message);
                }
            }
            return found;
        }

        public void MarkAttempted(Guid transactionId) => attempted.Add(transactionId);
        public void Retry(Guid transactionId) => attempted.Remove(transactionId);

        private bool IsConfirmed(Guid id, string transactionDirectory)
        {
            string checkpoint = LabelingIntegrationExchange.RequireContainedPath(Configuration.WorkspaceRoot,
                id.ToString("D") + "/review-session.json");
            if (!File.Exists(checkpoint)) return false;
            if (new FileInfo(checkpoint).Length > 65536) throw new InvalidDataException("Review checkpoint is too large.");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(checkpoint));
            if (!document.RootElement.TryGetProperty("ConfirmedResultSha256", out JsonElement value)
                || value.ValueKind != JsonValueKind.String) return false;
            IntegrationResultV2 result = IntegrationContractJson.DeserializeResultV2(LabelingIntegrationFileSnapshot.Read(
                LabelingIntegrationExchange.RequireContainedPath(transactionDirectory, IntegrationTransactionLayout.ResultFileName)).GetBytes());
            return string.Equals(value.GetString(), Convert.ToHexString(SHA256.HashData(
                IntegrationContractJson.SerializeCanonical(result))), StringComparison.Ordinal);
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;
            if (transport != null) await transport.DisposeAsync().ConfigureAwait(false);
        }
    }
}
