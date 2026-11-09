using OpenVisionLab.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace MvcVisionSystem._3._Communication.Integration
{
    /// <summary>Owned bytes, not a later read of a mutable editor file.</summary>
    public sealed class LabelingIntegrationFileSnapshot
    {
        private readonly byte[] bytes;
        internal LabelingIntegrationFileSnapshot(byte[] bytes)
        {
            this.bytes = (byte[])bytes.Clone();
            Sha256 = Convert.ToHexString(SHA256.HashData(this.bytes));
        }

        public long ByteLength => bytes.LongLength;
        public string Sha256 { get; }
        public byte[] GetBytes() => (byte[])bytes.Clone();

        internal static LabelingIntegrationFileSnapshot Read(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 256L * 1024 * 1024)
                throw new InvalidDataException("A review artifact exceeds the 256 MiB limit.");
            byte[] content = new byte[checked((int)stream.Length)];
            stream.ReadExactly(content);
            if (stream.ReadByte() != -1)
                throw new InvalidDataException("A review artifact changed length while being read.");
            return new LabelingIntegrationFileSnapshot(content);
        }

        internal bool MatchesFile(string path)
        {
            LabelingIntegrationFileSnapshot other = Read(path);
            return ByteLength == other.ByteLength && Sha256 == other.Sha256;
        }

        internal void WriteNew(string path)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
    }

    public sealed class LabelingIntegrationReviewInput
    {
        private readonly byte[] handoffBytes;
        internal LabelingIntegrationReviewInput(string exchangeRoot, IntegrationHandoffV2 handoff,
            string handoffSha256, LabelingIntegrationFileSnapshot sourceImage)
        {
            ExchangeRoot = exchangeRoot;
            handoffBytes = IntegrationContractJson.SerializeCanonical(handoff);
            HandoffSha256 = handoffSha256;
            SourceImage = sourceImage;
        }
        public string ExchangeRoot { get; }
        public IntegrationHandoffV2 Handoff => IntegrationContractJson.DeserializeHandoffV2(handoffBytes);
        public string HandoffSha256 { get; }
        public LabelingIntegrationFileSnapshot SourceImage { get; }
    }

    public sealed record LabelingIntegrationTransactionSummary(
        IntegrationHandoffV2 Handoff,
        bool HasAcknowledgement,
        bool HasResult);

    public sealed record LabelingIntegrationResultSummary(
        Guid TransactionId,
        IntegrationResultStatus Status,
        IntegrationInspectionOutcome Outcome,
        string RunId,
        int MetricCount,
        int EvidenceCount)
    {
        public string DisplayText =>
            $"{Outcome} · {Status} · Run {RunId} · metrics {MetricCount} · evidence {EvidenceCount}";
    }

    /// <summary>
    /// Owns Labeling Studio's explicit schema 2.0 local-file exchange. Merely
    /// discovering or reading a Handoff never starts inference, changes labels,
    /// loads a Recipe, or saves the project.
    /// </summary>
    public static class LabelingIntegrationExchange
    {
        public const string ApplicationId = "OpenVisionLab.LabelingStudio";

        /// <summary>Captures the exact verified input bytes before an editor workspace is created.</summary>
        public static LabelingIntegrationReviewInput ReadReviewInput(
            string exchangeRoot,
            Guid transactionId,
            IntegrationApplicationIdentity consumerBuild)
        {
            ArgumentNullException.ThrowIfNull(consumerBuild);
            string directory = GetTransactionDirectory(exchangeRoot, transactionId);
            string handoffPath = RequireContainedPath(directory, IntegrationTransactionLayout.HandoffFileName);
            LabelingIntegrationFileSnapshot envelope = LabelingIntegrationFileSnapshot.Read(handoffPath);
            IntegrationHandoffV2 handoff = IntegrationContractJson.DeserializeHandoffV2(envelope.GetBytes());
            if (handoff.TransactionId != transactionId)
                throw new InvalidDataException("Review transaction identity does not match its directory.");
            EnsureConsumerIdentity(handoff, consumerBuild);
            foreach (IntegrationArtifactReference artifact in handoff.Context.Artifacts)
            {
                RequireContainedPath(directory, artifact.RelativePath);
                ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(artifact, directory));
            }
            RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionRecipe);
            IntegrationArtifactReference source = RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionSource);
            LabelingIntegrationFileSnapshot image = LabelingIntegrationFileSnapshot.Read(
                RequireContainedPath(directory, source.RelativePath));
            if (image.ByteLength != source.ByteLength
                || !string.Equals(image.Sha256, source.Sha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(image.Sha256, handoff.Context.InputSha256, StringComparison.OrdinalIgnoreCase)
                || !envelope.MatchesFile(handoffPath))
            {
                throw new InvalidDataException("The review input changed while it was being captured.");
            }
            return new LabelingIntegrationReviewInput(
                Path.GetFullPath(exchangeRoot), handoff, envelope.Sha256, image);
        }

        /// <summary>A session lease is local coordination, not an ACK, save, or result.</summary>
        internal static FileStream AcquireReviewLease(string exchangeRoot, Guid transactionId)
        {
            string directory = GetTransactionDirectory(exchangeRoot, transactionId);
            return new FileStream(RequireContainedPath(directory, ".labeling-review.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        internal static void RevalidateReviewInput(LabelingIntegrationReviewInput input)
        {
            LabelingIntegrationReviewInput current = ReadReviewInput(
                input.ExchangeRoot, input.Handoff.TransactionId, input.Handoff.Context.ConsumerBuild);
            if (!string.Equals(input.HandoffSha256, current.HandoffSha256, StringComparison.Ordinal)
                || !string.Equals(input.SourceImage.Sha256, current.SourceImage.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The immutable review transaction no longer matches this session.");
            }
        }

        internal static IntegrationAcknowledgementV2 AcknowledgeReview(LabelingIntegrationReviewInput input)
        {
            RevalidateReviewInput(input);
            string directory = GetTransactionDirectory(input.ExchangeRoot, input.Handoff.TransactionId);
            if (!File.Exists(RequireContainedPath(directory, IntegrationTransactionLayout.AcknowledgementFileName)))
                return AcknowledgeHandoff(input.ExchangeRoot, input.Handoff.TransactionId, input.Handoff.Context.ConsumerBuild);
            return ReadAcceptedReviewAcknowledgement(input);
        }

        internal static IntegrationAcknowledgementV2 ReadAcceptedReviewAcknowledgement(LabelingIntegrationReviewInput input)
        {
            string directory = GetTransactionDirectory(input.ExchangeRoot, input.Handoff.TransactionId);
            var acknowledgement = IntegrationContractJson.DeserializeAcknowledgementV2(File.ReadAllBytes(
                RequireContainedPath(directory, IntegrationTransactionLayout.AcknowledgementFileName)));
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(input.Handoff, acknowledgement));
            if (acknowledgement.Status != IntegrationAcknowledgementStatus.Accepted)
                throw new InvalidOperationException("Review requires an accepted acknowledgement.");
            return acknowledgement;
        }

        /// <summary>
        /// Uses the shared ovl.image-roundtrip/1.0 sidecar and confirmed V2
        /// Completed/NotMeasured mapping. This is annotation saving, never inspection Pass.
        /// Called only by the session owner while its save/publication lease is held.
        /// </summary>
        internal static IntegrationResultV2 PublishSavedReview(LabelingIntegrationSavedReview review, bool readOnly = false)
        {
            RevalidateReviewInput(review.Input);
            IntegrationHandoffV2 handoff = review.Input.Handoff;
            IntegrationAcknowledgementV2 ack = ReadAcceptedReviewAcknowledgement(review.Input);
            string directory = GetTransactionDirectory(review.Input.ExchangeRoot, handoff.TransactionId);
            using var publicationLease = new FileStream(RequireContainedPath(directory, ".labeling-result.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            string runId = $"{review.Workspace.SessionId:D}-r{review.Revision}";
            string bundleRelative = $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/labeling-{review.Workspace.SessionId:N}-r{review.Revision}";
            var files = review.Artifacts.ToDictionary(a => a.RelativePath, a => a.Content, StringComparer.Ordinal);
            var evidence = review.Artifacts.Select(a => Reference(IntegrationArtifactRoles.ResultEvidence,
                ReviewArtifactId(a.Kind), $"{bundleRelative}/{a.RelativePath}", a.Content)).ToList();
            IntegrationArtifactReference source = RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionSource);
            LabelingReviewArtifact original = review.Artifacts.Single(a => a.Kind == LabelingReviewArtifactKind.SourceImage);
            var manifest = new LabelingIntegrationFileSnapshot(JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = "ovl.image-roundtrip/1.0",
                operation = "annotationReview",
                transactionId = handoff.TransactionId,
                source = new { artifactId = source.ArtifactId, sha256 = source.Sha256, byteLength = source.ByteLength,
                    width = review.Workspace.SourcePixels.Width, height = review.Workspace.SourcePixels.Height,
                    pixelFormat = review.Workspace.SourcePixels.ChannelOrder, coordinateFrameId = handoff.Context.FrameId },
                // Labels refer to the immutable original. The saved RGB re-encoding is separate evidence.
                outputImage = new { artifactId = ReviewArtifactId(original.Kind), sha256 = original.Content.Sha256,
                    byteLength = original.Content.ByteLength, width = review.Workspace.SourcePixels.Width,
                    height = review.Workspace.SourcePixels.Height, pixelFormat = review.Workspace.SourcePixels.ChannelOrder,
                    coordinateFrameId = handoff.Context.FrameId },
                coordinates = new { origin = "top-left", xDirection = "right", yDirection = "down", bounds = "half-open",
                    mapping = "identity", sourceToOutput = new[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 } },
                annotations = new { revisionId = runId, classCatalogArtifactId = ReviewArtifactId(LabelingReviewArtifactKind.ClassCatalog),
                    geometryArtifactIds = evidence.Where(e => e.ArtifactId is "labeling-detection-labels" or "labeling-canonical-segments").Select(e => e.ArtifactId).ToArray(),
                    maskArtifactIds = evidence.Where(e => e.ArtifactId == "labeling-raster-mask").Select(e => e.ArtifactId).ToArray(),
                    objectMetadataArtifactIds = evidence.Where(e => e.ArtifactId == "labeling-object-metadata").Select(e => e.ArtifactId).ToArray(),
                    reviewStatus = "saved" },
                execution = new { kind = "annotationSave", runId, groundTruthVerified = false }
            }));
            files.Add("image-roundtrip-manifest.json", manifest);
            evidence.Add(Reference(IntegrationArtifactRoles.ResultEvidence, "image-roundtrip-manifest",
                $"{bundleRelative}/image-roundtrip-manifest.json", manifest));
            // Product save record: retain exact bytes plus decoded identity and quantization semantics.
            var record = new LabelingIntegrationFileSnapshot(JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = "1.0", operation = "annotationSave", runId, reviewDigest = review.Digest,
                transactionId = handoff.TransactionId, handoffMessageId = handoff.MessageId,
                handoffSha256 = review.Input.HandoffSha256, sessionId = review.Workspace.SessionId, revision = review.Revision,
                correlation = IntegrationRunCorrelation.FromContext(handoff.Context),
                sourcePixels = review.Workspace.SourcePixels, editingPixels = review.Workspace.EditingPixels,
                pixelHashLayout = "top-to-bottom tightly packed rows; channel order and 8-bit depth in descriptor",
                coordinates = "full frame; no crop, resize, rotation or coordinate transfer",
                detectionNormalizedTolerance = 0.000001, maskEncoding = "RGB8; equal channels; background=0; classIndex+1",
                expected = review.Expectation, groundTruthVerified = false, artifacts = evidence
            }));
            files.Add("labeling-save-record.json", record);
            IntegrationArtifactReference runRecord = Reference(IntegrationArtifactRoles.RunRecord, runId,
                $"{bundleRelative}/labeling-save-record.json", record);
            var result = new IntegrationResultV2(IntegrationContractSchema.V2, IntegrationMessageKind.Result,
                Guid.NewGuid(), handoff.TransactionId, handoff.MessageId, ack.MessageId, NotBefore(ack.CreatedAtUtc),
                handoff.Context.ConsumerBuild, IntegrationResultStatus.Completed, IntegrationInspectionOutcome.NotMeasured,
                runId, runRecord, IntegrationRunCorrelation.FromContext(handoff.Context), Array.Empty<IntegrationMetric>(), evidence.ToArray(), null);
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(handoff, ack, result));

            string resultPath = RequireContainedPath(directory, IntegrationTransactionLayout.ResultFileName);
            if (File.Exists(resultPath)) return RequireSameReviewResult(directory, handoff, ack, result);
            if (readOnly) return null;
            string bundle = RequireContainedPath(directory, bundleRelative);
            if (!Directory.Exists(bundle))
            {
                string staging = RequireContainedPath(directory, $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/.labeling-{Guid.NewGuid():N}.tmp");
                Directory.CreateDirectory(staging);
                foreach (var file in files)
                {
                    string path = RequireContainedPath(staging, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    file.Value.WriteNew(path);
                }
                Directory.Move(staging, bundle);
            }
            // A prior crash after the directory move may be resumed only with identical content.
            string[] actual = EnumerateReviewFiles(bundle).Select(p => Path.GetRelativePath(bundle, p).Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(files.Keys.OrderBy(p => p, StringComparer.Ordinal), StringComparer.Ordinal)
                || files.Any(f => !f.Value.MatchesFile(RequireContainedPath(bundle, f.Key))))
                throw new InvalidDataException("The existing review bundle conflicts with this saved digest.");
            RevalidateReviewInput(review.Input);
            ValidateReviewResultFiles(directory, result);
            try { WriteNewMessage(directory, IntegrationTransactionLayout.ResultFileName, IntegrationContractJson.SerializeCanonical(result)); }
            catch (IOException) when (File.Exists(resultPath)) { return RequireSameReviewResult(directory, handoff, ack, result); }
            return result;
        }

        internal static IntegrationResultV2 PublishReviewTerminal(LabelingIntegrationReviewInput input,
            bool cancelled, string detail, IntegrationInspectionOutcome failureOutcome = IntegrationInspectionOutcome.ExecutionError,
            bool readOnly = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(detail);
            IntegrationHandoffV2 handoff = input.Handoff;
            string directory = GetTransactionDirectory(input.ExchangeRoot, handoff.TransactionId);
            // The admitted envelope must still be exact. A damaged artifact can itself be the failure being reported.
            if (LabelingIntegrationFileSnapshot.Read(RequireContainedPath(directory, IntegrationTransactionLayout.HandoffFileName)).Sha256 != input.HandoffSha256)
                throw new InvalidDataException("Cannot publish against a replaced handoff envelope.");
            IntegrationErrorCode code = cancelled ? IntegrationErrorCode.Cancelled : failureOutcome switch
            {
                IntegrationInspectionOutcome.ExecutionError => IntegrationErrorCode.ExecutionFailed,
                IntegrationInspectionOutcome.Tampered => IntegrationErrorCode.TamperedEvidence,
                IntegrationInspectionOutcome.CorrelationMismatch => IntegrationErrorCode.CorrelationMismatch,
                _ => throw new ArgumentOutOfRangeException(nameof(failureOutcome))
            };
            IntegrationAcknowledgementV2 ack = ReadAcceptedReviewAcknowledgement(input);
            using var publicationLease = new FileStream(RequireContainedPath(directory, ".labeling-result.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var result = new IntegrationResultV2(IntegrationContractSchema.V2, IntegrationMessageKind.Result,
                Guid.NewGuid(), handoff.TransactionId, handoff.MessageId, ack.MessageId, NotBefore(ack.CreatedAtUtc), handoff.Context.ConsumerBuild,
                cancelled ? IntegrationResultStatus.Cancelled : IntegrationResultStatus.Failed,
                cancelled ? IntegrationInspectionOutcome.Indeterminate : failureOutcome,
                null, null, IntegrationRunCorrelation.FromContext(handoff.Context), Array.Empty<IntegrationMetric>(),
                Array.Empty<IntegrationArtifactReference>(), new IntegrationError(code, detail, false));
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(handoff, ack, result));
            if (File.Exists(RequireContainedPath(directory, IntegrationTransactionLayout.ResultFileName)))
                return RequireSameReviewResult(directory, handoff, ack, result);
            if (readOnly) return null;
            WriteNewMessage(directory, IntegrationTransactionLayout.ResultFileName, IntegrationContractJson.SerializeCanonical(result));
            return result;
        }

        private static IntegrationArtifactReference Reference(string role, string id, string path, LabelingIntegrationFileSnapshot content)
            => new(role, id, path, content.ByteLength, content.Sha256);

        private static string ReviewArtifactId(LabelingReviewArtifactKind kind) => kind switch
        {
            LabelingReviewArtifactKind.SourceImage => "labeling-source-image",
            LabelingReviewArtifactKind.SavedImage => "labeling-saved-image",
            LabelingReviewArtifactKind.DetectionLabels => "labeling-detection-labels",
            LabelingReviewArtifactKind.CanonicalSegments => "labeling-canonical-segments",
            LabelingReviewArtifactKind.RasterMask => "labeling-raster-mask",
            LabelingReviewArtifactKind.ObjectMetadata => "labeling-object-metadata",
            LabelingReviewArtifactKind.ClassCatalog => "labeling-class-catalog",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private static IEnumerable<string> EnumerateReviewFiles(string directory)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                RequireContainedPath(directory, Path.GetFileName(path));
                if (Directory.Exists(path)) { foreach (string nested in EnumerateReviewFiles(path)) yield return nested; }
                else yield return path;
            }
        }

        private static IntegrationResultV2 RequireSameReviewResult(string directory, IntegrationHandoffV2 handoff,
            IntegrationAcknowledgementV2 ack, IntegrationResultV2 expected)
        {
            IntegrationResultV2 existing = IntegrationContractJson.DeserializeResultV2(File.ReadAllBytes(
                RequireContainedPath(directory, IntegrationTransactionLayout.ResultFileName)));
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(handoff, ack, existing));
            if (existing.Status != expected.Status || existing.Outcome != expected.Outcome || existing.RunId != expected.RunId
                || existing.RunRecord != expected.RunRecord || existing.Error != expected.Error || existing.Metrics.Count != 0
                || !existing.Evidence.SequenceEqual(expected.Evidence))
                throw new InvalidDataException("The transaction already has a different terminal result or saved digest.");
            ValidateReviewResultFiles(directory, existing);
            return existing;
        }

        private static void ValidateReviewResultFiles(string directory, IntegrationResultV2 result)
        {
            foreach (IntegrationArtifactReference artifact in result.Evidence.Concat(result.RunRecord == null
                ? Array.Empty<IntegrationArtifactReference>() : new[] { result.RunRecord }))
            {
                RequireContainedPath(directory, artifact.RelativePath);
                ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(artifact, directory));
            }
        }

        internal static string RequireContainedPath(string root, string relativePath)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (Path.IsPathRooted(relativePath)
                || !fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A review artifact must remain inside its owned directory.");
            }
            // Check existing ancestors as well as the leaf: a junction must not redirect a save.
            for (string path = fullPath; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                if ((File.Exists(path) || Directory.Exists(path))
                    && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Review artifacts cannot traverse a reparse point.");
                }
            }
            return fullPath;
        }

        public static IReadOnlyList<LabelingIntegrationTransactionSummary> DiscoverHandoffs(
            string exchangeRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
            string transactionsRoot = Path.Combine(
                Path.GetFullPath(exchangeRoot),
                IntegrationTransactionLayout.TransactionsDirectoryName);
            if (!Directory.Exists(transactionsRoot))
            {
                return Array.Empty<LabelingIntegrationTransactionSummary>();
            }

            var transactions = new List<LabelingIntegrationTransactionSummary>();
            foreach (string directory in Directory.EnumerateDirectories(transactionsRoot))
            {
                if (!Guid.TryParse(Path.GetFileName(directory), out Guid transactionId))
                {
                    continue;
                }
                string handoffPath = Path.Combine(
                    directory,
                    IntegrationTransactionLayout.HandoffFileName);
                if (!File.Exists(handoffPath)
                    || !UsesSchema(handoffPath, IntegrationContractSchema.V2))
                {
                    continue;
                }

                IntegrationHandoffV2 handoff = ReadHandoffEnvelope(
                    exchangeRoot,
                    transactionId);
                if (!string.Equals(
                    handoff.Context.ConsumerBuild.ApplicationId,
                    ApplicationId,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                transactions.Add(new LabelingIntegrationTransactionSummary(
                    handoff,
                    File.Exists(Path.Combine(
                        directory,
                        IntegrationTransactionLayout.AcknowledgementFileName)),
                    File.Exists(Path.Combine(
                        directory,
                        IntegrationTransactionLayout.ResultFileName))));
            }

            return transactions
                .OrderByDescending(transaction => transaction.Handoff.CreatedAtUtc)
                .ToArray();
        }

        public static IntegrationHandoffV2 ReadHandoff(
            string exchangeRoot,
            Guid transactionId)
        {
            IntegrationHandoffV2 handoff = ReadHandoffEnvelope(
                exchangeRoot,
                transactionId);
            ValidateLabelingConsumer(handoff);
            string transactionDirectory = GetTransactionDirectory(
                exchangeRoot,
                transactionId);
            foreach (IntegrationArtifactReference artifact in handoff.Context.Artifacts)
            {
                ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                    artifact,
                    transactionDirectory));
            }
            RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionSource);
            RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionRecipe);
            return handoff;
        }

        public static IntegrationHandoffV2 ReadHandoffEnvelope(
            string exchangeRoot,
            Guid transactionId)
        {
            string transactionDirectory = GetTransactionDirectory(
                exchangeRoot,
                transactionId);
            IntegrationHandoffV2 handoff = IntegrationContractJson.DeserializeHandoffV2(
                File.ReadAllBytes(Path.Combine(
                    transactionDirectory,
                    IntegrationTransactionLayout.HandoffFileName)));
            if (handoff.TransactionId != transactionId)
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.CorrelationMismatch,
                    "Handoff transaction identity does not match its directory.");
            }
            return handoff;
        }

        public static IntegrationAcknowledgementV2 AcknowledgeHandoff(
            string exchangeRoot,
            Guid transactionId,
            IntegrationApplicationIdentity consumerBuild)
        {
            ArgumentNullException.ThrowIfNull(consumerBuild);
            IntegrationHandoffV2 handoff = ReadHandoff(exchangeRoot, transactionId);
            EnsureConsumerIdentity(handoff, consumerBuild);
            var acknowledgement = new IntegrationAcknowledgementV2(
                IntegrationContractSchema.V2,
                IntegrationMessageKind.Acknowledgement,
                Guid.NewGuid(),
                handoff.TransactionId,
                handoff.MessageId,
                NotBefore(handoff.CreatedAtUtc),
                consumerBuild,
                IntegrationAcknowledgementStatus.Accepted,
                null);
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
                handoff,
                acknowledgement));
            WriteNewMessage(
                GetTransactionDirectory(exchangeRoot, transactionId),
                IntegrationTransactionLayout.AcknowledgementFileName,
                IntegrationContractJson.SerializeCanonical(acknowledgement));
            return acknowledgement;
        }

        public static IntegrationResultV2 PublishCompletedResult(
            string exchangeRoot,
            Guid transactionId,
            IntegrationApplicationIdentity consumerBuild,
            string runId,
            IntegrationInspectionOutcome outcome,
            string existingRunRecordPath,
            IReadOnlyList<IntegrationMetric> metrics)
        {
            ArgumentNullException.ThrowIfNull(consumerBuild);
            ArgumentException.ThrowIfNullOrWhiteSpace(runId);
            ArgumentException.ThrowIfNullOrWhiteSpace(existingRunRecordPath);
            ArgumentNullException.ThrowIfNull(metrics);
            if (outcome is not (
                IntegrationInspectionOutcome.Pass
                or IntegrationInspectionOutcome.Ng
                or IntegrationInspectionOutcome.NotMeasured
                or IntegrationInspectionOutcome.Indeterminate))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    "A completed Labeling result requires an inspection outcome.");
            }
            if (metrics.Any(metric => metric is null || !double.IsFinite(metric.Value)))
            {
                throw new ArgumentException(
                    "Result metrics must be non-null and finite.",
                    nameof(metrics));
            }

            IntegrationHandoffV2 handoff = ReadHandoff(exchangeRoot, transactionId);
            EnsureConsumerIdentity(handoff, consumerBuild);
            string transactionDirectory = GetTransactionDirectory(
                exchangeRoot,
                transactionId);
            IntegrationAcknowledgementV2 acknowledgement =
                IntegrationContractJson.DeserializeAcknowledgementV2(
                    File.ReadAllBytes(Path.Combine(
                        transactionDirectory,
                        IntegrationTransactionLayout.AcknowledgementFileName)));
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
                handoff,
                acknowledgement));
            if (acknowledgement.Status != IntegrationAcknowledgementStatus.Accepted)
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.InvalidState,
                    "A completed Result requires an accepted Acknowledgement.");
            }

            string artifactsDirectory = Path.Combine(
                transactionDirectory,
                IntegrationTransactionLayout.ArtifactsDirectoryName);
            Directory.CreateDirectory(artifactsDirectory);
            string targetPath = Path.Combine(
                artifactsDirectory,
                "labeling-run-record.json");
            File.Copy(Path.GetFullPath(existingRunRecordPath), targetPath, overwrite: false);
            try
            {
                IntegrationArtifactReference runRecord = CreateArtifactReference(
                    IntegrationArtifactRoles.RunRecord,
                    runId,
                    targetPath,
                    $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/labeling-run-record.json");
                var result = new IntegrationResultV2(
                    IntegrationContractSchema.V2,
                    IntegrationMessageKind.Result,
                    Guid.NewGuid(),
                    handoff.TransactionId,
                    handoff.MessageId,
                    acknowledgement.MessageId,
                    NotBefore(acknowledgement.CreatedAtUtc),
                    consumerBuild,
                    IntegrationResultStatus.Completed,
                    outcome,
                    runId,
                    runRecord,
                    IntegrationRunCorrelation.FromContext(handoff.Context),
                    metrics.ToArray(),
                    Array.Empty<IntegrationArtifactReference>(),
                    null);
                ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
                    handoff,
                    acknowledgement,
                    result));
                WriteNewMessage(
                    transactionDirectory,
                    IntegrationTransactionLayout.ResultFileName,
                    IntegrationContractJson.SerializeCanonical(result));
                return result;
            }
            catch
            {
                TryDeleteFile(targetPath);
                throw;
            }
        }

        public static LabelingIntegrationResultSummary ReadResultSummary(
            string exchangeRoot,
            Guid transactionId)
        {
            IntegrationHandoffV2 handoff = ReadHandoff(exchangeRoot, transactionId);
            string transactionDirectory = GetTransactionDirectory(
                exchangeRoot,
                transactionId);
            IntegrationAcknowledgementV2 acknowledgement =
                IntegrationContractJson.DeserializeAcknowledgementV2(
                    File.ReadAllBytes(Path.Combine(
                        transactionDirectory,
                        IntegrationTransactionLayout.AcknowledgementFileName)));
            IntegrationResultV2 result = IntegrationContractJson.DeserializeResultV2(
                File.ReadAllBytes(Path.Combine(
                    transactionDirectory,
                    IntegrationTransactionLayout.ResultFileName)));
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
                handoff,
                acknowledgement,
                result));
            if (result.RunRecord != null)
            {
                ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                    result.RunRecord,
                    transactionDirectory));
            }
            foreach (IntegrationArtifactReference evidence in result.Evidence)
            {
                ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                    evidence,
                    transactionDirectory));
            }
            return new LabelingIntegrationResultSummary(
                result.TransactionId,
                result.Status,
                result.Outcome,
                result.RunId ?? string.Empty,
                result.Metrics.Count,
                result.Evidence.Count);
        }

        private static void ValidateLabelingConsumer(IntegrationHandoffV2 handoff)
        {
            if (handoff.Context.Modality != IntegrationInspectionModality.Ai
                || handoff.Context.InputKind != IntegrationInspectionInputKind.Image
                || !string.Equals(
                    handoff.Context.ConsumerBuild.ApplicationId,
                    ApplicationId,
                    StringComparison.Ordinal))
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.RequestRejected,
                    "The Handoff is not a Labeling Studio AI/Image request.");
            }
        }

        private static void EnsureConsumerIdentity(
            IntegrationHandoffV2 handoff,
            IntegrationApplicationIdentity consumerBuild)
        {
            ValidateLabelingConsumer(handoff);
            IntegrationApplicationIdentity expected = handoff.Context.ConsumerBuild;
            if (!string.Equals(expected.ApplicationId, consumerBuild.ApplicationId, StringComparison.Ordinal)
                || !string.Equals(expected.ApplicationVersion, consumerBuild.ApplicationVersion, StringComparison.Ordinal)
                || !string.Equals(expected.SourceCommit, consumerBuild.SourceCommit, StringComparison.OrdinalIgnoreCase)
                || expected.SourceState != consumerBuild.SourceState)
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.CorrelationMismatch,
                    "The supplied Labeling Studio build does not match the Handoff context.");
            }
        }

        private static IntegrationArtifactReference RequireContextArtifact(
            IntegrationHandoffV2 handoff,
            string role)
        {
            return handoff.Context.Artifacts.SingleOrDefault(artifact =>
                string.Equals(artifact.Role, role, StringComparison.Ordinal))
                ?? throw new IntegrationContractException(
                    IntegrationErrorCode.InvalidArtifact,
                    $"The Handoff does not contain the required '{role}' artifact.");
        }

        private static IntegrationArtifactReference CreateArtifactReference(
            string role,
            string artifactId,
            string fullPath,
            string relativePath)
        {
            using FileStream stream = File.OpenRead(fullPath);
            return new IntegrationArtifactReference(
                role,
                artifactId,
                relativePath,
                stream.Length,
                Convert.ToHexString(SHA256.HashData(stream)));
        }

        private static bool UsesSchema(string path, string expectedSchema)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
                return document.RootElement.TryGetProperty("schemaVersion", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String
                    && string.Equals(value.GetString(), expectedSchema, StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static DateTimeOffset NotBefore(DateTimeOffset predecessor)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return now < predecessor ? predecessor : now;
        }

        private static string GetTransactionDirectory(
            string exchangeRoot,
            Guid transactionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
            if (transactionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Transaction identity cannot be empty.",
                    nameof(transactionId));
            }
            return Path.Combine(
                Path.GetFullPath(exchangeRoot),
                IntegrationTransactionLayout.TransactionsDirectoryName,
                transactionId.ToString("D"));
        }

        private static void WriteNewMessage(
            string transactionDirectory,
            string fileName,
            byte[] bytes)
        {
            string target = Path.Combine(transactionDirectory, fileName);
            if (File.Exists(target))
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.InvalidState,
                    $"The transaction already contains '{fileName}'.");
            }
            string temporary = Path.Combine(
                transactionDirectory,
                $".{fileName}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, target);
            }
            finally
            {
                TryDeleteFile(temporary);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Preserve the original contract, run record, or I/O failure.
            }
        }

        private static void ThrowIfInvalid(IntegrationValidationResult validation)
        {
            if (validation.IsValid)
            {
                return;
            }
            IntegrationValidationIssue issue = validation.Issues[0];
            throw new IntegrationContractException(
                issue.Code,
                $"{issue.Field}: {issue.Message}");
        }
    }
}
