using OpenVisionLab.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace MvcVisionSystem._3._Communication.Integration
{
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
