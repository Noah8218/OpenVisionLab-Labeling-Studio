using MvcVisionSystem._3._Communication.Integration;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Integration.Contracts;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MvcVisionSystem
{
    // These are local editor states/artifact kinds, not integration wire enums.
    public enum LabelingIntegrationReviewState { Prepared, Editing, Saving, Saved, PublicationPending, Cancelled, Failed, Published }
    public enum LabelingReviewArtifactKind { SourceImage, SavedImage, DetectionLabels, CanonicalSegments, RasterMask, ObjectMetadata, ClassCatalog }

    public sealed record LabelingReviewArtifact(
        LabelingReviewArtifactKind Kind, string RelativePath, LabelingIntegrationFileSnapshot Content);

    public sealed record LabelingReviewPixelDescriptor(int Width, int Height, string ChannelOrder, string Sha256);

    public sealed record LabelingReviewWorkspace(
        Guid SessionId, Guid TransactionId, string DirectoryPath, string OriginalImagePath,
        string EditingImagePath, string OutputRootPath, LabelingReviewPixelDescriptor SourcePixels,
        LabelingReviewPixelDescriptor EditingPixels);

    /// <summary>
    /// Expected persisted counts from the same live editor snapshot used by the save adapter.
    /// SegmentRecordCount counts canonical components, not only object IDs. MetadataRecordCount
    /// counts non-default per-object records. Empty annotations require explicit no-object intent.
    /// </summary>
    public sealed record LabelingReviewSaveExpectation(
        int BoxCount, int SegmentRecordCount, int MetadataRecordCount, bool ExplicitNoObject);

    public sealed class LabelingIntegrationSavedReview
    {
        internal LabelingIntegrationSavedReview(LabelingIntegrationReviewInput input, LabelingReviewWorkspace workspace,
            IReadOnlyList<string> classes, IReadOnlyList<LabelingReviewArtifact> artifacts, LabelingReviewSaveExpectation expected)
        {
            Input = input;
            Workspace = workspace;
            Classes = Array.AsReadOnly(classes.ToArray());
            Artifacts = Array.AsReadOnly(artifacts.ToArray());
            Expectation = expected;
            // Local deterministic identity: timestamps and absolute workstation paths are excluded.
            Digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                transactionId = workspace.TransactionId,
                handoffSha256 = input.HandoffSha256,
                sessionId = workspace.SessionId,
                revision = 1,
                sourcePixels = workspace.SourcePixels,
                editingPixels = workspace.EditingPixels,
                classes = Classes,
                expected,
                files = Artifacts.Select(a => new { kind = a.Kind.ToString(), path = a.RelativePath,
                    sha256 = a.Content.Sha256, byteLength = a.Content.ByteLength })
            })));
        }
        public LabelingIntegrationReviewInput Input { get; }
        public LabelingReviewWorkspace Workspace { get; }
        public IReadOnlyList<string> Classes { get; }
        public IReadOnlyList<LabelingReviewArtifact> Artifacts { get; }
        public LabelingReviewSaveExpectation Expectation { get; }
        public string Digest { get; }
        public int Revision => 1;
    }

    /// <summary>
    /// Owns one explicit review of one immutable transaction. Existing image-load and
    /// annotation-save adapters are supplied by the Shell; no Recipe is applied here.
    /// Dispose releases only the local lease. Working files and committed labels survive
    /// cancellation, save/publication failures, and disposal for explicit recovery.
    /// </summary>
    public sealed class LabelingIntegrationReviewWorkflowService : IDisposable
    {
        private readonly object gate = new();
        private readonly FileStream lease;
        private readonly LabelingIntegrationReviewInput input;
        private readonly string[] classes;
        private readonly LabelingIntegrationFileSnapshot editingImage;
        private bool saveObserved;
        private bool disposed;
        private LabelingIntegrationReviewState state = LabelingIntegrationReviewState.Prepared;
        private LabelingIntegrationSavedReview savedReview;
        private LabelingDatasetPurpose? datasetPurpose;
        private string terminalDetail;
        private string confirmedResultSha256;
        private IntegrationInspectionOutcome terminalOutcome = IntegrationInspectionOutcome.ExecutionError;
        private const string CheckpointFileName = "review-session.json";
        private sealed record ReviewCheckpoint(string Schema, Guid TransactionId, Guid SessionId, string HandoffSha256,
            string EditingSha256, LabelingReviewPixelDescriptor SourcePixels, LabelingReviewPixelDescriptor EditingPixels,
            string[] Classes, LabelingDatasetPurpose? Purpose, LabelingIntegrationReviewState State,
            LabelingReviewSaveExpectation Expectation, string SavedDigest, string TerminalDetail,
            IntegrationInspectionOutcome TerminalOutcome, string ConfirmedResultSha256);

        private LabelingIntegrationReviewWorkflowService(LabelingIntegrationReviewInput input,
            LabelingReviewWorkspace workspace, string[] classes, LabelingIntegrationFileSnapshot editingImage, FileStream lease)
        {
            this.input = input;
            Workspace = workspace;
            this.classes = classes;
            this.editingImage = editingImage;
            this.lease = lease;
        }

        public LabelingReviewWorkspace Workspace { get; }
        public LabelingIntegrationReviewState State { get { lock (gate) return state; } }
        public LabelingIntegrationSavedReview SavedReview { get { lock (gate) return savedReview; } }
        public string TerminalDetail { get { lock (gate) return terminalDetail; } }

        public IntegrationAcknowledgementV2 Acknowledge()
        {
            lock (gate)
            {
                RequireState(LabelingIntegrationReviewState.Prepared);
                return LabelingIntegrationExchange.AcknowledgeReview(input);
            }
        }

        public static LabelingIntegrationReviewWorkflowService Prepare(string exchangeRoot, Guid transactionId,
            IntegrationApplicationIdentity consumerBuild, string workspaceRoot, IReadOnlyList<string> orderedClasses)
        {
            ArgumentNullException.ThrowIfNull(orderedClasses);
            string[] names = orderedClasses.ToArray();
            if (names.Length == 0 || names.Length > 255 || names.Any(string.IsNullOrWhiteSpace)
                || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length
                || names.Any(name => name != ClassCatalogService.NormalizeClassName(name)))
                throw new InvalidDataException("Review requires an ordered, unique canonical class catalog of 1 to 255 classes.");

            string root = Path.GetFullPath(workspaceRoot);
            string exchange = Path.GetFullPath(exchangeRoot);
            if (IsWithin(root, exchange) || IsWithin(exchange, root))
                throw new InvalidDataException("The editor workspace and immutable exchange must have separate roots.");
            FileStream lease = LabelingIntegrationExchange.AcquireReviewLease(exchangeRoot, transactionId);
            try
            {
                LabelingIntegrationReviewInput input = LabelingIntegrationExchange.ReadReviewInput(exchangeRoot, transactionId, consumerBuild);
                string transactionDirectory = Path.Combine(exchange, IntegrationTransactionLayout.TransactionsDirectoryName, transactionId.ToString("D"));
                if (File.Exists(LabelingIntegrationExchange.RequireContainedPath(transactionDirectory, IntegrationTransactionLayout.ResultFileName)))
                    throw new InvalidOperationException("A terminal transaction cannot start another review; read its existing result.");

                byte[] sourceBytes = input.SourceImage.GetBytes();
                (int width, int height, bool gray) = ValidateSourcePng(sourceBytes);
                string directory = LabelingIntegrationExchange.RequireContainedPath(root, transactionId.ToString("D"));
                if (Directory.Exists(directory) || File.Exists(directory))
                    throw new InvalidOperationException("An existing review workspace requires explicit recovery; it will not be overwritten.");
                Directory.CreateDirectory(directory);
                string original = LabelingIntegrationExchange.RequireContainedPath(directory, "source.png");
                string edit = LabelingIntegrationExchange.RequireContainedPath(directory, "editing.png");
                input.SourceImage.WriteNew(original);
                using CachedDecodedImage decoded = new ImageDecodeService().DecodeForCanvas(original);
                if (decoded?.Bitmap == null || decoded.Bitmap.Width != width || decoded.Bitmap.Height != height)
                    throw new InvalidDataException("Decoded source dimensions do not match its PNG header.");
                LabelingReviewPixelDescriptor sourcePixels = DescribePixels(decoded.Bitmap, gray);
                LabelingReviewPixelDescriptor editingPixels = DescribePixels(decoded.Bitmap, gray: false);
                using (var stream = new FileStream(edit, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    decoded.Bitmap.Save(stream, ImageFormat.Png);
                    stream.Flush(flushToDisk: true);
                }
                LabelingIntegrationFileSnapshot editing = LabelingIntegrationFileSnapshot.Read(edit);
                Guid sessionId = Guid.NewGuid();
                var workspace = new LabelingReviewWorkspace(sessionId, transactionId, directory, original, edit,
                    LabelingIntegrationExchange.RequireContainedPath(directory, "dataset"), sourcePixels, editingPixels);
                LabelingIntegrationExchange.RevalidateReviewInput(input);
                var service = new LabelingIntegrationReviewWorkflowService(input, workspace, names, editing, lease);
                service.WriteCheckpoint();
                return service;
            }
            catch
            {
                lease.Dispose();
                throw; // Never delete a partially prepared workspace or user-saved artifacts.
            }
        }

        /// <summary>Creates isolated data for the existing Shell context; never mutates the user's active Recipe.</summary>
        public LabelingProjectData CreateIsolatedProjectData(LabelingDatasetPurpose purpose)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (state == LabelingIntegrationReviewState.Saving)
                    throw new InvalidOperationException("Cannot replace project data during a save.");
                if (purpose != LabelingDatasetPurpose.Segmentation && purpose != LabelingDatasetPurpose.ObjectDetection)
                    throw new NotSupportedException("This review supports detection and segmentation annotation only.");
                if (datasetPurpose.HasValue && datasetPurpose.Value != purpose)
                    throw new InvalidDataException("The recovered review has a different dataset purpose.");
                var data = new LabelingProjectData();
                data.ConfigureOutputRoot(Workspace.OutputRootPath);
                data.ProjectSettings.DatasetPurpose = purpose;
                foreach (string name in classes)
                    if (!ClassCatalogService.TryAddClass(data, name, out _))
                        throw new InvalidDataException("The isolated class catalog could not be created in its admitted order.");
                if (!datasetPurpose.HasValue)
                {
                    datasetPurpose = purpose;
                    WriteCheckpoint();
                }
                return data;
            }
        }

        /// <summary>The callback installs the isolated data and calls ImageLoadWorkflowAdapter.TryLoadImage.</summary>
        public bool TryLoad(LabelingProjectData data, Func<string, bool> loadImage)
        {
            ArgumentNullException.ThrowIfNull(loadImage);
            lock (gate)
            {
                RequireState(LabelingIntegrationReviewState.Prepared);
                ValidateSaveTarget(data, Workspace.EditingImagePath);
                LabelingIntegrationExchange.ReadAcceptedReviewAcknowledgement(input);
                try
                {
                    if (!loadImage(Workspace.EditingImagePath)) return false;
                    ValidateSaveTarget(data, Workspace.EditingImagePath);
                    state = LabelingIntegrationReviewState.Editing;
                    WriteCheckpoint();
                    return true;
                }
                catch { state = LabelingIntegrationReviewState.Failed; WriteCheckpoint(); throw; }
            }
        }

        /// <summary>
        /// Call only from the explicit save-and-return command, not navigation/autosave.
        /// The save callback uses the existing transactional annotation adapter. Its
        /// OnAnnotationsSaved callback must call ObserveAnnotationsSaved below.
        /// </summary>
        public LabelingIntegrationSavedReview SaveExplicitly(LabelingProjectData data, string activeImagePath,
            LabelingReviewSaveExpectation expected, Func<bool> saveCurrentAnnotations)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(saveCurrentAnnotations);
            lock (gate)
            {
                RequireState(LabelingIntegrationReviewState.Editing);
                ValidateSaveTarget(data, activeImagePath);
                if (expected.BoxCount < 0 || expected.SegmentRecordCount < 0 || expected.MetadataRecordCount < 0
                    || ((long)expected.BoxCount + expected.SegmentRecordCount == 0) != expected.ExplicitNoObject)
                    throw new InvalidDataException("Empty return requires explicit no-object intent and valid saved counts.");
                saveObserved = false;
                state = LabelingIntegrationReviewState.Saving;
                try
                {
                    WriteCheckpoint(); // An interrupted save cannot be promoted to completion by recovery.
                    if (!saveCurrentAnnotations() || !saveObserved)
                        throw new InvalidOperationException("The explicit save did not report a successful annotation transaction.");
                    ValidateSaveTarget(data, activeImagePath);
                    savedReview = CaptureSavedReview(data, expected);
                    state = LabelingIntegrationReviewState.Saved;
                    WriteCheckpoint(); // Durable explicit-save proof must precede any publication.
                    return savedReview;
                }
                catch { state = LabelingIntegrationReviewState.Failed; WriteCheckpoint(); throw; }
            }
        }

        public void ObserveAnnotationsSaved(string activeImagePath)
        {
            lock (gate)
            {
                if (!disposed && state == LabelingIntegrationReviewState.Saving
                    && SamePath(activeImagePath, Workspace.EditingImagePath)) saveObserved = true;
            }
        }

        public void ValidateSaveTarget(LabelingProjectData data, string activeImagePath)
        {
            ArgumentNullException.ThrowIfNull(data);
            string transactionDirectory = Path.Combine(input.ExchangeRoot,
                IntegrationTransactionLayout.TransactionsDirectoryName, Workspace.TransactionId.ToString("D"));
            if (File.Exists(LabelingIntegrationExchange.RequireContainedPath(transactionDirectory, IntegrationTransactionLayout.ResultFileName)))
                throw new InvalidOperationException("A terminal transaction cannot load or save another review revision.");
            if (!SamePath(activeImagePath, Workspace.EditingImagePath)
                || !SamePath(data.OutputRootPath, Workspace.OutputRootPath)
                || !SamePath(data.DataYamlFilePath, Path.Combine(Workspace.OutputRootPath, "data.yaml"))
                || !classes.SequenceEqual(data.ClassNamedList.Select(item => item?.Text), StringComparer.Ordinal))
                throw new InvalidDataException("The active image, output directory, or ordered class catalog changed during review.");
            // Validate every existing descendant before invoking a writer that creates/removes split files.
            ValidateOwnedTree(Workspace.DirectoryPath);
            if (!input.SourceImage.MatchesFile(Workspace.OriginalImagePath)
                || !editingImage.MatchesFile(Workspace.EditingImagePath))
                throw new InvalidDataException("The review source or editing image was modified.");
            LabelingIntegrationExchange.RevalidateReviewInput(input);
        }

        public bool Cancel()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (state == LabelingIntegrationReviewState.Published || state == LabelingIntegrationReviewState.Failed) return false;
                if (state == LabelingIntegrationReviewState.PublicationPending)
                {
                    IntegrationResultV2 existing = LabelingIntegrationExchange.PublishSavedReview(savedReview, readOnly: true);
                    CheckResultConfirmation(existing);
                    if (existing != null)
                    {
                        state = LabelingIntegrationReviewState.Published;
                        confirmedResultSha256 = ResultDigest(existing);
                        WriteCheckpoint();
                        return false;
                    }
                }
                if (state == LabelingIntegrationReviewState.Saving)
                    throw new InvalidOperationException("A synchronous save must finish before cancellation is resolved.");
                state = LabelingIntegrationReviewState.Cancelled;
                WriteCheckpoint();
                return true;
            }
        }

        public void Fail()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (state == LabelingIntegrationReviewState.Saving)
                    throw new InvalidOperationException("Failure is resolved by the active save callback.");
                if (state == LabelingIntegrationReviewState.Published || state == LabelingIntegrationReviewState.Cancelled
                    || state == LabelingIntegrationReviewState.PublicationPending)
                    throw new InvalidOperationException("A terminal review cannot be replaced by a failure.");
                state = LabelingIntegrationReviewState.Failed;
                WriteCheckpoint();
            }
        }

        /// <summary>Retries reuse the identical immutable save snapshot. A publication exception is not proof of non-delivery.</summary>
        public IntegrationResultV2 PublishSaved()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (state != LabelingIntegrationReviewState.Saved && state != LabelingIntegrationReviewState.PublicationPending
                    && state != LabelingIntegrationReviewState.Published)
                    throw new InvalidOperationException("Only a saved, uncancelled review may publish its artifacts.");
                // The receiver's immutable local Result is authoritative. Never blindly resend on uncertainty.
                IntegrationResultV2 existing = LabelingIntegrationExchange.PublishSavedReview(savedReview, readOnly: true);
                CheckResultConfirmation(existing);
                if (existing != null)
                {
                    state = LabelingIntegrationReviewState.Published;
                    confirmedResultSha256 = ResultDigest(existing);
                    WriteCheckpoint();
                    return existing;
                }
                state = LabelingIntegrationReviewState.PublicationPending;
                WriteCheckpoint();
                IntegrationResultV2 result = LabelingIntegrationExchange.PublishSavedReview(savedReview);
                state = LabelingIntegrationReviewState.Published;
                confirmedResultSha256 = ResultDigest(result);
                WriteCheckpoint();
                return result;
            }
        }

        public IntegrationResultV2 PublishCancellation(string detail)
        {
            lock (gate)
            {
                RequireState(LabelingIntegrationReviewState.Cancelled);
                RecordTerminalIntent(detail, IntegrationInspectionOutcome.Indeterminate);
                IntegrationResultV2 result = LabelingIntegrationExchange.PublishReviewTerminal(input, true, terminalDetail, readOnly: true);
                CheckResultConfirmation(result);
                result ??= LabelingIntegrationExchange.PublishReviewTerminal(input, true, terminalDetail);
                confirmedResultSha256 = ResultDigest(result);
                WriteCheckpoint();
                return result;
            }
        }

        public IntegrationResultV2 PublishFailure(string detail,
            IntegrationInspectionOutcome outcome = IntegrationInspectionOutcome.ExecutionError)
        {
            lock (gate)
            {
                RequireState(LabelingIntegrationReviewState.Failed);
                RecordTerminalIntent(detail, outcome);
                IntegrationResultV2 result = LabelingIntegrationExchange.PublishReviewTerminal(input, false, terminalDetail, terminalOutcome, readOnly: true);
                CheckResultConfirmation(result);
                result ??= LabelingIntegrationExchange.PublishReviewTerminal(input, false, terminalDetail, terminalOutcome);
                confirmedResultSha256 = ResultDigest(result);
                WriteCheckpoint();
                return result;
            }
        }

        /// <summary>
        /// Reopens only a checkpoint produced by this owner. Saved returns are reconstructed
        /// from actual files and the same deterministic digest; an interrupted save reopens
        /// for editing and requires a new explicit save. No result is published here.
        /// </summary>
        public static LabelingIntegrationReviewWorkflowService Recover(string exchangeRoot, Guid transactionId,
            IntegrationApplicationIdentity consumerBuild, string workspaceRoot, IReadOnlyList<string> orderedClasses,
            LabelingDatasetPurpose purpose)
        {
            ArgumentNullException.ThrowIfNull(orderedClasses);
            string root = Path.GetFullPath(workspaceRoot);
            string exchange = Path.GetFullPath(exchangeRoot);
            if (IsWithin(root, exchange) || IsWithin(exchange, root))
                throw new InvalidDataException("Review and exchange roots must remain separate.");
            FileStream lease = LabelingIntegrationExchange.AcquireReviewLease(exchangeRoot, transactionId);
            try
            {
                LabelingIntegrationReviewInput input = LabelingIntegrationExchange.ReadReviewInput(exchangeRoot, transactionId, consumerBuild);
                string directory = LabelingIntegrationExchange.RequireContainedPath(root, transactionId.ToString("D"));
                ValidateOwnedTree(directory);
                LabelingIntegrationFileSnapshot checkpointBytes = LabelingIntegrationFileSnapshot.Read(
                    LabelingIntegrationExchange.RequireContainedPath(directory, CheckpointFileName));
                if (checkpointBytes.ByteLength > 65536) throw new InvalidDataException("Review checkpoint is too large.");
                ReviewCheckpoint checkpoint = JsonSerializer.Deserialize<ReviewCheckpoint>(checkpointBytes.GetBytes());
                if (checkpoint == null || checkpoint.Schema != "ovl.labeling-review-session/1.0"
                    || checkpoint.TransactionId != transactionId || checkpoint.SessionId == Guid.Empty
                    || checkpoint.HandoffSha256 != input.HandoffSha256 || checkpoint.Classes == null
                    || !checkpoint.Classes.SequenceEqual(orderedClasses, StringComparer.Ordinal)
                    || !Enum.IsDefined(checkpoint.State)
                    || (checkpoint.Purpose.HasValue && checkpoint.Purpose.Value != purpose))
                    throw new InvalidDataException("The retained review does not match this transaction, class order or purpose.");
                string original = LabelingIntegrationExchange.RequireContainedPath(directory, "source.png");
                string edit = LabelingIntegrationExchange.RequireContainedPath(directory, "editing.png");
                if (!input.SourceImage.MatchesFile(original)) throw new InvalidDataException("Retained source changed.");
                LabelingIntegrationFileSnapshot editing = LabelingIntegrationFileSnapshot.Read(edit);
                if (editing.Sha256 != checkpoint.EditingSha256) throw new InvalidDataException("Retained editing image changed.");
                (int width, int height, bool gray) = ValidateSourcePng(input.SourceImage.GetBytes());
                using (CachedDecodedImage source = new ImageDecodeService().DecodeForCanvas(original))
                using (CachedDecodedImage edited = new ImageDecodeService().DecodeForCanvas(edit))
                {
                    if (source?.Bitmap == null || edited?.Bitmap == null || source.Bitmap.Width != width || source.Bitmap.Height != height
                        || DescribePixels(source.Bitmap, gray) != checkpoint.SourcePixels
                        || DescribePixels(source.Bitmap, false) != checkpoint.EditingPixels
                        || DescribePixels(edited.Bitmap, false) != checkpoint.EditingPixels)
                        throw new InvalidDataException("Retained source/editing pixels or coordinate frame changed.");
                }
                var workspace = new LabelingReviewWorkspace(checkpoint.SessionId, transactionId, directory, original, edit,
                    LabelingIntegrationExchange.RequireContainedPath(directory, "dataset"), checkpoint.SourcePixels, checkpoint.EditingPixels);
                var service = new LabelingIntegrationReviewWorkflowService(input, workspace, checkpoint.Classes.ToArray(), editing, lease)
                {
                    datasetPurpose = checkpoint.Purpose ?? purpose,
                    terminalDetail = checkpoint.TerminalDetail,
                    terminalOutcome = checkpoint.TerminalOutcome,
                    confirmedResultSha256 = checkpoint.ConfirmedResultSha256
                };
                LabelingProjectData data = service.CreateIsolatedProjectData(purpose);
                if (checkpoint.State != LabelingIntegrationReviewState.Prepared)
                    LabelingIntegrationExchange.ReadAcceptedReviewAcknowledgement(input);
                if (checkpoint.State is LabelingIntegrationReviewState.Saved or LabelingIntegrationReviewState.PublicationPending
                    or LabelingIntegrationReviewState.Published)
                {
                    if (checkpoint.Expectation == null || string.IsNullOrWhiteSpace(checkpoint.SavedDigest))
                        throw new InvalidDataException("No durable proof of a completed explicit save exists.");
                    service.savedReview = service.CaptureSavedReview(data, checkpoint.Expectation);
                    if (service.savedReview.Digest != checkpoint.SavedDigest)
                        throw new InvalidDataException("Retained saved artifacts differ from the explicit-save digest.");
                    service.state = checkpoint.State;
                    IntegrationResultV2 existing = service.ReadPublishedResult();
                    if (checkpoint.State == LabelingIntegrationReviewState.Published && existing == null)
                        throw new InvalidDataException("A previously confirmed Result is missing; automatic republication is forbidden.");
                    service.CheckResultConfirmation(existing);
                    if (existing != null) service.state = LabelingIntegrationReviewState.Published;
                }
                else if (checkpoint.State is LabelingIntegrationReviewState.Cancelled or LabelingIntegrationReviewState.Failed)
                {
                    service.state = checkpoint.State;
                    if (checkpoint.State == LabelingIntegrationReviewState.Cancelled)
                        service.terminalOutcome = IntegrationInspectionOutcome.Indeterminate;
                    // A crash before terminal publication may leave no detail; no earlier Result can then be inferred.
                    service.terminalDetail ??= checkpoint.State == LabelingIntegrationReviewState.Cancelled
                        ? "Retained review was cancelled before result publication."
                        : "Retained review failed before result publication.";
                    service.CheckResultConfirmation(service.ReadPublishedResult());
                }
                else
                {
                    if (checkpoint.ConfirmedResultSha256 != null)
                        throw new InvalidDataException("An editable session cannot carry a terminal-result confirmation.");
                    service.state = LabelingIntegrationReviewState.Prepared;
                    service.ValidateSaveTarget(data, edit);
                }
                if (!checkpointBytes.MatchesFile(Path.Combine(directory, CheckpointFileName)))
                    throw new InvalidDataException("Review checkpoint changed during recovery.");
                service.WriteCheckpoint();
                return service;
            }
            catch { lease.Dispose(); throw; }
        }

        /// <summary>Reads and validates the receiver's Result without creating a message or artifact bundle.</summary>
        public IntegrationResultV2 ReadPublishedResult()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                IntegrationResultV2 result = state switch
                {
                    LabelingIntegrationReviewState.Saved or LabelingIntegrationReviewState.PublicationPending
                        or LabelingIntegrationReviewState.Published => LabelingIntegrationExchange.PublishSavedReview(savedReview, readOnly: true),
                    LabelingIntegrationReviewState.Cancelled => LabelingIntegrationExchange.PublishReviewTerminal(input, true, terminalDetail, readOnly: true),
                    LabelingIntegrationReviewState.Failed => LabelingIntegrationExchange.PublishReviewTerminal(input, false, terminalDetail, terminalOutcome, readOnly: true),
                    _ => throw new InvalidOperationException("There is no saved or terminal result intent to reconcile.")
                };
                CheckResultConfirmation(result);
                return result;
            }
        }

        private void RecordTerminalIntent(string detail, IntegrationInspectionOutcome outcome)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(detail);
            if (terminalDetail != null && (terminalDetail != detail || terminalOutcome != outcome))
                throw new InvalidDataException("A terminal publication retry must retain the original reason and outcome.");
            terminalDetail = detail;
            terminalOutcome = outcome;
            WriteCheckpoint();
        }

        private static string ResultDigest(IntegrationResultV2 result)
            => Convert.ToHexString(SHA256.HashData(IntegrationContractJson.SerializeCanonical(result)));

        private void CheckResultConfirmation(IntegrationResultV2 result)
        {
            if (confirmedResultSha256 != null && (result == null || confirmedResultSha256 != ResultDigest(result)))
                throw new InvalidDataException("A previously confirmed Result was removed or replaced; republication is forbidden.");
            if (result != null) confirmedResultSha256 = ResultDigest(result);
        }

        private void WriteCheckpoint()
        {
            var checkpoint = new ReviewCheckpoint("ovl.labeling-review-session/1.0", Workspace.TransactionId,
                Workspace.SessionId, input.HandoffSha256, editingImage.Sha256, Workspace.SourcePixels, Workspace.EditingPixels,
                classes, datasetPurpose, state, savedReview?.Expectation, savedReview?.Digest, terminalDetail, terminalOutcome, confirmedResultSha256);
            string destination = LabelingIntegrationExchange.RequireContainedPath(Workspace.DirectoryPath, CheckpointFileName);
            string temporary = LabelingIntegrationExchange.RequireContainedPath(Workspace.DirectoryPath, $".review-session-{Guid.NewGuid():N}.tmp");
            new LabelingIntegrationFileSnapshot(JsonSerializer.SerializeToUtf8Bytes(checkpoint)).WriteNew(temporary);
            File.Move(temporary, destination, overwrite: true);
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                if (state == LabelingIntegrationReviewState.Saving)
                    throw new InvalidOperationException("Cannot dispose from inside the synchronous save callback.");
                disposed = true;
                lease.Dispose();
            }
        }

        private LabelingIntegrationSavedReview CaptureSavedReview(LabelingProjectData data, LabelingReviewSaveExpectation expected)
        {
            LabelingIntegrationFileSnapshot yamlBytes = CaptureFile(data.DataYamlFilePath);
            YamlData yaml = new YamlDotNet.Serialization.DeserializerBuilder().Build()
                .Deserialize<YamlData>(Encoding.UTF8.GetString(yamlBytes.GetBytes()));
            if (yaml?.names == null || yaml.nc != classes.Length || !classes.SequenceEqual(yaml.names, StringComparer.Ordinal))
                throw new InvalidDataException("The saved YAML class order differs from the admitted catalog.");
            var artifacts = new List<LabelingReviewArtifact>
            {
                new(LabelingReviewArtifactKind.SourceImage, "source.png", input.SourceImage),
                new(LabelingReviewArtifactKind.ClassCatalog, "classes.json",
                    new LabelingIntegrationFileSnapshot(JsonSerializer.SerializeToUtf8Bytes(classes)))
            };
            string[] labels = YoloAnnotationService.GetTargetLabelPaths(Path.GetFileName(Workspace.EditingImagePath), data).ToArray();
            if (labels.Length != 1) throw new InvalidDataException("One review image must have exactly one destination split.");
            string splitRoot = Path.GetDirectoryName(Path.GetDirectoryName(labels[0]));
            string image = Path.Combine(splitRoot, "images", "editing.png");
            LabelingIntegrationFileSnapshot savedImage = CaptureFile(image);
            ValidateSavedImage(savedImage);
            using (CachedDecodedImage decoded = new ImageDecodeService().DecodeForCanvas(image))
            {
                if (decoded?.Bitmap == null || DescribePixels(decoded.Bitmap, false) != Workspace.EditingPixels)
                    throw new InvalidDataException("Saved image dimensions/pixels differ from the full-frame editing image.");
            }
            // Read again after decoding so validation and returned bytes cannot come from different saves.
            if (!savedImage.MatchesFile(image)) throw new InvalidDataException("Saved image changed during validation.");
            artifacts.Add(new(LabelingReviewArtifactKind.SavedImage, Relative(image), savedImage));
            LabelingIntegrationFileSnapshot label = CaptureFile(labels[0]);
            ValidateBoxes(label, expected.BoxCount);
            YoloAnnotationLoadResult reopenedBoxes = YoloAnnotationService.LoadAnnotationRectanglesWithDiagnostics(
                labels[0], data.ClassNamedList, new Size(Workspace.SourcePixels.Width, Workspace.SourcePixels.Height));
            if (reopenedBoxes.HasErrors || reopenedBoxes.Annotations.Values.Sum(items => items.Count) != expected.BoxCount
                || !label.MatchesFile(labels[0]))
                throw new InvalidDataException("Saved detection labels cannot be reopened without losing objects.");
            artifacts.Add(new(LabelingReviewArtifactKind.DetectionLabels, Relative(labels[0]), label));

            string segmentsPath = Path.Combine(splitRoot, "segments", "editing.json");
            string maskPath = Path.Combine(splitRoot, "masks", "editing.png");
            SegmentationAnnotationFile segments = null;
            if (expected.SegmentRecordCount > 0)
            {
                LabelingIntegrationFileSnapshot canonical = CaptureFile(segmentsPath);
                LabelingIntegrationFileSnapshot mask = CaptureFile(maskPath);
                segments = JsonSerializer.Deserialize<SegmentationAnnotationFile>(canonical.GetBytes());
                ValidateSegments(segments, expected.SegmentRecordCount);
                ValidateMask(mask, classes.Length);
                // Reuse the existing reader for geometry/raster compatibility; returned bytes are never reserialized.
                YoloSegmentationAnnotationService.LoadSegmentationObjects(segmentsPath, maskPath, data.ClassNamedList,
                    new Size(Workspace.SourcePixels.Width, Workspace.SourcePixels.Height));
                if (!canonical.MatchesFile(segmentsPath) || !mask.MatchesFile(maskPath))
                    throw new InvalidDataException("Segmentation artifacts changed during validation.");
                artifacts.Add(new(LabelingReviewArtifactKind.CanonicalSegments, Relative(segmentsPath), canonical));
                artifacts.Add(new(LabelingReviewArtifactKind.RasterMask, Relative(maskPath), mask));
            }
            else if (File.Exists(segmentsPath) || File.Exists(maskPath))
                throw new InvalidDataException("Unexpected segmentation artifacts remain after this save.");

            string metadataPath = Path.Combine(splitRoot, "object-metadata", "editing.json");
            if (expected.MetadataRecordCount > 0)
            {
                LabelingIntegrationFileSnapshot metadata = CaptureFile(metadataPath);
                ValidateMetadata(metadata, segments, reopenedBoxes.Annotations, expected.MetadataRecordCount);
                artifacts.Add(new(LabelingReviewArtifactKind.ObjectMetadata, Relative(metadataPath), metadata));
            }
            else if (File.Exists(metadataPath)) throw new InvalidDataException("Unexpected object metadata remains after this save.");
            string[] allowed = artifacts.Where(a => a.Kind != LabelingReviewArtifactKind.ClassCatalog
                && a.Kind != LabelingReviewArtifactKind.SourceImage).Select(a => a.RelativePath).ToArray();
            string dataRoot = Path.Combine(Workspace.OutputRootPath, "data");
            if (Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories)
                .Any(path => !allowed.Contains(Relative(path), StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unexpected image/label/mask/metadata files exist in the isolated dataset.");
            foreach (LabelingReviewArtifact artifact in artifacts.Where(a => a.Kind != LabelingReviewArtifactKind.ClassCatalog))
                if (!artifact.Content.MatchesFile(Path.Combine(Workspace.DirectoryPath, artifact.RelativePath)))
                    throw new InvalidDataException("A saved artifact changed while the return snapshot was captured.");
            if (!yamlBytes.MatchesFile(data.DataYamlFilePath)) throw new InvalidDataException("The saved class catalog changed during capture.");
            LabelingIntegrationExchange.RevalidateReviewInput(input);
            return new LabelingIntegrationSavedReview(input, Workspace, classes, artifacts, expected);
        }

        private void ValidateBoxes(LabelingIntegrationFileSnapshot labels, int expectedCount)
        {
            string[] lines = Encoding.UTF8.GetString(labels.GetBytes()).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length != expectedCount) throw new InvalidDataException("Saved detection count does not match the editor snapshot.");
            foreach (string line in lines)
            {
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                    || index < 0 || index >= classes.Length) throw new InvalidDataException("Invalid YOLO class index or field count.");
                double[] values = parts.Skip(1).Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                const double tolerance = 0.000001; // Existing writer uses six decimal places; no lossless box claim.
                if (values.Any(value => !double.IsFinite(value)) || values[2] <= 0 || values[3] <= 0
                    || values[0] - values[2] / 2 < -tolerance || values[0] + values[2] / 2 > 1 + tolerance
                    || values[1] - values[3] / 2 < -tolerance || values[1] + values[3] / 2 > 1 + tolerance)
                    throw new InvalidDataException("Saved YOLO coordinates are outside the full source frame.");
            }
        }

        private void ValidateSegments(SegmentationAnnotationFile file, int count)
        {
            if (file == null || file.Version != 3 || file.ImageName != Path.GetFileNameWithoutExtension(Workspace.EditingImagePath)
                || file.ImageWidth != Workspace.SourcePixels.Width || file.ImageHeight != Workspace.SourcePixels.Height
                || file.Polygons == null || file.Polygons.Count != count)
                throw new InvalidDataException("Canonical segmentation identity, dimensions, version, or count changed.");
            foreach (SegmentationPolygonRecord record in file.Polygons)
            {
                if (record == null || record.ClassIndex < 0 || record.ClassIndex >= classes.Length
                    || record.ClassName != classes[record.ClassIndex] || string.IsNullOrWhiteSpace(record.ObjectId)
                    || record.ComponentIndex < 0 || record.Points == null || record.Points.Count < 3 || record.Cutouts == null)
                    throw new InvalidDataException("Invalid canonical segment class, object identity, or geometry.");
                foreach (IReadOnlyList<SegmentationPointRecord> ring in new[] { record.Points }.Concat(record.Cutouts))
                    if (ring == null || ring.Count < 3 || ring.Any(p => p == null || p.X < 0 || p.Y < 0
                        || p.X >= file.ImageWidth || p.Y >= file.ImageHeight))
                        throw new InvalidDataException("Canonical segment coordinates are outside the source frame.");
            }
            if (file.Polygons.Select(p => (p.ObjectId, p.ComponentIndex)).Distinct().Count() != file.Polygons.Count)
                throw new InvalidDataException("Canonical segment component identities are duplicated.");
        }

        private void ValidateMask(LabelingIntegrationFileSnapshot snapshot, int classCount)
        {
            byte[] bytes = snapshot.GetBytes();
            ValidateSourcePng(bytes);
            using var stream = new MemoryStream(bytes, writable: false);
            using var mask = new Bitmap(stream);
            // The existing writer encodes class-index+1 in all three RGB channels, with zero background.
            if (mask.RawFormat.Guid != ImageFormat.Png.Guid || mask.Width != Workspace.SourcePixels.Width
                || mask.Height != Workspace.SourcePixels.Height || mask.PixelFormat != PixelFormat.Format24bppRgb)
                throw new InvalidDataException("Class mask must be the existing writer's full-size RGB class-value PNG.");
            BitmapData bits = mask.LockBits(new Rectangle(0, 0, mask.Width, mask.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[checked(mask.Width * 3)];
                for (int y = 0; y < mask.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), row, 0, row.Length);
                    for (int x = 0; x < mask.Width; x++)
                        if (row[x * 3] > classCount || row[x * 3] != row[x * 3 + 1] || row[x * 3] != row[x * 3 + 2])
                            throw new InvalidDataException("Class mask contains an unknown or non-grayscale class value.");
                }
            }
            finally { mask.UnlockBits(bits); }
        }

        private void ValidateMetadata(LabelingIntegrationFileSnapshot snapshot, SegmentationAnnotationFile segments,
            IReadOnlyDictionary<string, List<Rectangle>> boxes, int count)
        {
            WpfObjectMetadataFile file = JsonSerializer.Deserialize<WpfObjectMetadataFile>(snapshot.GetBytes());
            if (file == null || file.Version != 2 || file.ImageName != Path.GetFileNameWithoutExtension(Workspace.EditingImagePath)
                || file.Objects == null || file.Objects.Count != count)
                throw new InvalidDataException("Object metadata identity/version/count does not match this save.");
            foreach (WpfObjectMetadataRecord item in file.Objects)
            {
                if (item == null || !classes.Contains(item.ClassName, StringComparer.Ordinal)
                    || item.Tags == null || item.Tags.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException("Invalid object metadata class or tags.");
                if (item.Kind == "Segment")
                {
                    if (segments?.Polygons.Any(p => p.ObjectId == item.ObjectId && p.ClassName == item.ClassName) != true)
                        throw new InvalidDataException("Object metadata references a missing canonical segment.");
                }
                else if (item.Kind != "Box" || item.X < 0 || item.Y < 0 || item.Width <= 0 || item.Height <= 0
                    || (long)item.X + item.Width > Workspace.SourcePixels.Width || (long)item.Y + item.Height > Workspace.SourcePixels.Height)
                    throw new InvalidDataException("Invalid object metadata geometry or kind.");
                else if (item.Occurrence < 0 || !boxes.TryGetValue(item.ClassName, out List<Rectangle> candidates)
                    || candidates.Count(bounds => bounds == new Rectangle(item.X, item.Y, item.Width, item.Height)) <= item.Occurrence)
                    throw new InvalidDataException("Object metadata references a missing saved box occurrence.");
            }
            if (file.Objects.GroupBy(item => item.Kind == "Segment" ? $"Segment:{item.ObjectId}"
                : $"Box:{item.ClassName}:{item.X}:{item.Y}:{item.Width}:{item.Height}:{item.Occurrence}", StringComparer.Ordinal).Any(group => group.Count() > 1)
                || file.Objects.Where(item => !string.IsNullOrEmpty(item.GroupId)).GroupBy(item => item.GroupId, StringComparer.Ordinal).Any(group => group.Count() < 2))
                throw new InvalidDataException("Object metadata contains duplicate identities or a group with fewer than two members.");
        }

        private void ValidateSavedImage(LabelingIntegrationFileSnapshot snapshot)
        {
            byte[] bytes = snapshot.GetBytes();
            (int width, int height, _) = ValidateSourcePng(bytes, allowOpaqueAlpha: true);
            if (width != Workspace.SourcePixels.Width || height != Workspace.SourcePixels.Height)
                throw new InvalidDataException("Saved image dimensions changed.");
            if (bytes[25] != 6) return;
            // GDI's existing PNG saver may add an opaque alpha channel. Never hide non-opaque pixels.
            using var stream = new MemoryStream(bytes, writable: false);
            using var bitmap = new Bitmap(stream);
            BitmapData bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[checked(width * 4)];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), row, 0, row.Length);
                    for (int x = 0; x < width; x++) if (row[x * 4 + 3] != 255)
                        throw new InvalidDataException("Saved derivative has non-opaque alpha.");
                }
            }
            finally { bitmap.UnlockBits(bits); }
        }

        private LabelingIntegrationFileSnapshot CaptureFile(string path)
            => LabelingIntegrationFileSnapshot.Read(LabelingIntegrationExchange.RequireContainedPath(Workspace.DirectoryPath, Relative(path)));
        private string Relative(string path) => Path.GetRelativePath(Workspace.DirectoryPath, path).Replace('\\', '/');

        private static void ValidateOwnedTree(string directory)
        {
            LabelingIntegrationExchange.RequireContainedPath(directory, ".path-check");
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                LabelingIntegrationExchange.RequireContainedPath(directory, Path.GetFileName(entry));
                if (Directory.Exists(entry)) ValidateOwnedTree(entry);
            }
        }

        private static LabelingReviewPixelDescriptor DescribePixels(Bitmap bitmap, bool gray)
        {
            if (bitmap.PixelFormat != PixelFormat.Format24bppRgb) throw new InvalidDataException("The existing canvas decoder did not produce RGB8.");
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            BitmapData bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] bgr = new byte[checked(bitmap.Width * 3)];
                byte[] row = new byte[gray ? bitmap.Width : bgr.Length];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), bgr, 0, bgr.Length);
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (gray)
                        {
                            if (bgr[x * 3] != bgr[x * 3 + 1] || bgr[x * 3] != bgr[x * 3 + 2])
                                throw new InvalidDataException("Grayscale decoding changed channel values.");
                            row[x] = bgr[x * 3];
                        }
                        else { row[x * 3] = bgr[x * 3 + 2]; row[x * 3 + 1] = bgr[x * 3 + 1]; row[x * 3 + 2] = bgr[x * 3]; }
                    }
                    hash.AppendData(row);
                }
                return new(bitmap.Width, bitmap.Height, gray ? "Gray8" : "RGB8", Convert.ToHexString(hash.GetHashAndReset()));
            }
            finally { bitmap.UnlockBits(bits); }
        }

        private static (int Width, int Height, bool Gray) ValidateSourcePng(byte[] bytes, bool allowOpaqueAlpha = false)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(signature)
                || Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR" || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13)
                throw new NotSupportedException("Review currently accepts single-frame Gray8/RGB8 PNG sources only.");
            int width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)));
            int height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)));
            if (width < 1 || height < 1 || width > 8192 || height > 8192 || (long)width * height > 16 * 1024 * 1024
                || bytes[24] != 8 || (bytes[25] != 0 && bytes[25] != 2 && !(allowOpaqueAlpha && bytes[25] == 6)))
                throw new NotSupportedException("Unsupported source dimensions, bit depth, alpha, or channel layout.");
            bool ended = false;
            for (int offset = 8; offset <= bytes.Length - 12;)
            {
                uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                if (length > bytes.Length - offset - 12) throw new InvalidDataException("Truncated PNG chunk.");
                string kind = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                if (kind is "tRNS" or "acTL" or "eXIf") throw new NotSupportedException("Transparency, animation, and EXIF transforms are not supported for review.");
                offset = checked(offset + (int)length + 12);
                if (kind == "IEND") { ended = length == 0 && offset == bytes.Length; break; }
            }
            if (!ended) throw new InvalidDataException("PNG has no complete terminal chunk.");
            return (width, height, bytes[25] == 0);
        }

        private void RequireState(LabelingIntegrationReviewState expected)
        {
            ThrowIfDisposed();
            if (state != expected) throw new InvalidOperationException($"Review is {state}; expected {expected}.");
        }
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
        private static bool SamePath(string left, string right) => !string.IsNullOrWhiteSpace(left)
            && string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'), Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        private static bool IsWithin(string path, string root) => SamePath(path, root)
            || Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
