using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns one current queue-review session: catalog adoption, review commands,
    /// background recounts, persistence and reset. Core owns review rules/JSON;
    /// the Shell owns Dispatcher calls and row presentation.
    /// </summary>
    public class ImageQualityReviewWorkflowService : IDisposable
    {
        private readonly object sessionLock = new object();
        private readonly Func<string, Size, LabelingProjectData, YoloImageLabelStatus> readLabelStatus;
        private readonly ImageQueueReviewStatusRefreshCoordinator refreshCoordinator = new ImageQueueReviewStatusRefreshCoordinator();
        private readonly Dictionary<string, long> imageRevisions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private YoloImageReviewStatusService reviewStatus;
        private LabelingProjectData catalogData;
        private string reviewStatusFilePath;
        private HashSet<string> catalogImages;
        private int? activeCatalogVersion;
        private long generation;
        private long revision;
        private bool active = true;
        private bool disposed;

        public ImageQualityReviewWorkflowService() : this(new YoloImageReviewStatusService())
        {
        }

        // Unbound callers retain the original explicit status-service contract.
        public ImageQualityReviewWorkflowService(YoloImageReviewStatusService reviewStatus)
            : this(reviewStatus, YoloImageLabelStatusService.Build)
        {
        }

        internal ImageQualityReviewWorkflowService(YoloImageReviewStatusService reviewStatus, Func<string, Size, LabelingProjectData, YoloImageLabelStatus> readLabelStatus)
        {
            this.reviewStatus = reviewStatus ?? throw new ArgumentNullException(nameof(reviewStatus));
            this.readLabelStatus = readLabelStatus ?? throw new ArgumentNullException(nameof(readLabelStatus));
        }

        internal YoloImageReviewStatusService ReviewStatus => reviewStatus;

        public bool IsCatalogLoading { get { lock (sessionLock) return activeCatalogVersion.HasValue; } }

        public bool CanReview(LabelingProjectData data)
        {
            lock (sessionLock) return CanReviewCore(data);
        }

        private bool CanReviewCore(LabelingProjectData data)
        {
            return active && !disposed && !activeCatalogVersion.HasValue
                && (reviewStatusFilePath == null || SameTarget(data ?? catalogData));
        }

        private bool SameTarget(LabelingProjectData data)
        {
            return string.Equals(reviewStatusFilePath, YoloImageReviewStatusService.ResolveReviewStatusFilePath(data), StringComparison.OrdinalIgnoreCase);
        }

        public void BeginCatalogLoad(int version)
        {
            lock (sessionLock)
            {
                activeCatalogVersion = version;
                InvalidateRefreshes();
            }
        }

        public void CompleteCatalogLoad(int version)
        {
            lock (sessionLock)
            {
                if (activeCatalogVersion == version) activeCatalogVersion = null;
            }
        }

        public void CancelCatalogLoad()
        {
            lock (sessionLock)
            {
                activeCatalogVersion = null;
                InvalidateRefreshes();
            }
        }

        public void AdoptCatalog(ImageQualityReviewWorkflowService loaded)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            lock (sessionLock)
            {
                InvalidateRefreshes();
                reviewStatus = loaded.reviewStatus;
                catalogData = loaded.catalogData;
                reviewStatusFilePath = loaded.reviewStatusFilePath;
                catalogImages = loaded.catalogImages;
                active = true;
                // Adoption happens only after the catalog's current-request check.
                // Detail loading and initial image refresh may now use this catalog.
                activeCatalogVersion = null;
            }
        }

        public void Reset()
        {
            lock (sessionLock)
            {
                InvalidateRefreshes();
                reviewStatus = new YoloImageReviewStatusService();
                catalogData = null;
                catalogImages = null;
                reviewStatusFilePath = null;
                activeCatalogVersion = null;
                active = false;
            }
        }

        public void Dispose()
        {
            lock (sessionLock)
            {
                if (disposed) return;
                Reset();
                disposed = true;
                refreshCoordinator.Dispose();
            }
        }

        private void InvalidateRefreshes()
        {
            ++generation;
            imageRevisions.Clear();
            refreshCoordinator.Cancel();
        }

        public IReadOnlyList<YoloImageReviewStatus> GetItems()
        {
            lock (sessionLock) return reviewStatus.GetItems();
        }

        public void SetImages(IEnumerable<string> imagePaths)
        {
            lock (sessionLock)
            {
                InvalidateRefreshes();
                reviewStatus.SetImages(imagePaths);
                if (catalogImages != null) catalogImages = new HashSet<string>(reviewStatus.GetItems().Select(item => item.ImagePath), StringComparer.OrdinalIgnoreCase);
            }
        }

        public void LoadReviewStatus(LabelingProjectData data, IEnumerable<string> imagePaths)
        {
            lock (sessionLock)
            {
                InvalidateRefreshes();
                catalogData = data;
                LabelingProjectData snapshot = CaptureReviewData(data);
                reviewStatusFilePath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(snapshot);
                reviewStatus.LoadReviewStatus(snapshot, imagePaths);
                catalogImages = new HashSet<string>(reviewStatus.GetItems().Select(item => item.ImagePath), StringComparer.OrdinalIgnoreCase);
                active = true;
            }
        }

        public void SaveReviewStatus(LabelingProjectData data)
        {
            lock (sessionLock)
            {
                if (CanReviewCore(data)) reviewStatus.SaveReviewStatus(CaptureReviewData(data));
            }
        }

        // A batch's cancellation flush must target the original status and path,
        // even though this long-lived workflow will subsequently adopt a catalog.
        public Action CaptureReviewStatusSave(LabelingProjectData data)
        {
            lock (sessionLock)
            {
                if (!CanReviewCore(data)) return () => { };
                YoloImageReviewStatusService originalStatus = reviewStatus;
                LabelingProjectData snapshot = CaptureReviewData(data);
                string originalPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(snapshot);
                return () =>
                {
                    lock (sessionLock)
                    {
                        if (!disposed && string.Equals(originalPath, YoloImageReviewStatusService.ResolveReviewStatusFilePath(data), StringComparison.OrdinalIgnoreCase))
                            originalStatus.SaveReviewStatus(snapshot);
                    }
                };
            }
        }

        public YoloImageReviewStatus GetOrCreate(string imagePath)
        {
            lock (sessionLock)
            {
                return active && !disposed && ContainsImage(imagePath) ? reviewStatus.GetOrCreate(imagePath) : null;
            }
        }

        private bool ContainsImage(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath) && (catalogImages == null || catalogImages.Contains(imagePath));
        }

        private YoloImageReviewStatus Mutate(string imagePath, Func<YoloImageReviewStatus> apply)
        {
            lock (sessionLock)
            {
                if (!CanReviewCore(catalogData) || !ContainsImage(imagePath)) return null;
                imageRevisions[imagePath] = ++revision;
                return apply();
            }
        }

        // Capture before scheduling work. Reading files never mutates live state;
        // the commit rechecks the catalog, newer image edits and original target.
        internal Func<string, Size, YoloImageReviewStatus> CaptureLabelStatusRefresh(
            LabelingProjectData data,
            bool? hasActiveCandidates = null,
            bool saveReviewStatus = false,
            Func<bool> isCurrent = null)
        {
            lock (sessionLock)
            {
                if (!CanReviewCore(data)) return (_, _) => null;
                long capturedGeneration = generation;
                long capturedRevision = revision;
                LabelingProjectData snapshot = CaptureReviewData(data);
                string target = YoloImageReviewStatusService.ResolveReviewStatusFilePath(snapshot);
                return (imagePath, imageSize) =>
                {
                    bool IsValid() => generation == capturedGeneration && CanReviewCore(data) && ContainsImage(imagePath)
                        && (!imageRevisions.TryGetValue(imagePath, out long changedAt) || changedAt <= capturedRevision)
                        && string.Equals(target, YoloImageReviewStatusService.ResolveReviewStatusFilePath(data), StringComparison.OrdinalIgnoreCase)
                        && (isCurrent == null || isCurrent());
                    lock (sessionLock) { if (!IsValid()) return null; }
                    YoloImageLabelStatus labelStatus = readLabelStatus(imagePath, imageSize, snapshot);
                    lock (sessionLock)
                    {
                        if (!IsValid()) return null;
                        YoloImageReviewStatus result = hasActiveCandidates.HasValue
                            ? reviewStatus.ApplyLabelStatusAndReviewState(imagePath, labelStatus, hasActiveCandidates.Value)
                            : reviewStatus.ApplyLabelStatus(imagePath, labelStatus);
                        imageRevisions[imagePath] = ++revision;
                        if (saveReviewStatus) reviewStatus.SaveReviewStatus(snapshot);
                        return result;
                    }
                };
            }
        }

        public YoloImageReviewStatus RefreshLabelStatus(string imagePath, Size imageSize, LabelingProjectData data)
        {
            return CaptureLabelStatusRefresh(data)(imagePath, imageSize);
        }

        public YoloImageReviewStatus RefreshLabelStatusAndReviewState(string imagePath, Size imageSize, LabelingProjectData data, bool hasActiveCandidates)
        {
            return CaptureLabelStatusRefresh(data, hasActiveCandidates)(imagePath, imageSize);
        }

        public ImageQueueReviewStatusRefreshOperation QueueRefresh(string imagePath, Size imageSize, LabelingProjectData data, bool hasActiveCandidates)
        {
            return refreshCoordinator.Queue(imagePath, imageSize, this, data, hasActiveCandidates);
        }

        public bool IsCurrent(int requestVersion) => refreshCoordinator.IsCurrent(requestVersion);

        public YoloImageReviewStatus SetDetectionRequested(string imagePath, string imageName = "") => Mutate(imagePath, () => reviewStatus.SetDetectionRequested(imagePath, imageName));

        public YoloImageReviewStatus SetDetectionFailed(string imagePath, string imageName, string message) => Mutate(imagePath, () => reviewStatus.SetDetectionFailed(imagePath, imageName, message));

        public YoloImageReviewStatus SetDetectionNoCandidates(string imagePath, string imageName) => Mutate(imagePath, () => reviewStatus.SetDetectionNoCandidates(imagePath, imageName));

        public YoloImageReviewStatus SetDetectionCandidates(string imagePath, string imageName, int candidateCount) => Mutate(imagePath, () => reviewStatus.SetDetectionCandidates(imagePath, imageName, candidateCount));

        public YoloImageReviewStatus MarkConfirmed(string imagePath, string imageName = "") => Mutate(imagePath, () => reviewStatus.MarkConfirmed(imagePath, imageName));

        public YoloImageReviewStatus MarkSkipped(string imagePath, string imageName = "") => Mutate(imagePath, () => reviewStatus.MarkSkipped(imagePath, imageName));

        public bool TryFindNextUnlabeled(IReadOnlyList<string> orderedImagePaths, string currentImagePath, out string nextImagePath)
        {
            lock (sessionLock)
            {
                nextImagePath = string.Empty;
                return CanReviewCore(catalogData) && reviewStatus.TryFindNextUnlabeled(orderedImagePaths, currentImagePath, out nextImagePath);
            }
        }

        public ImageQualityReviewResult ApplyQualityReview(ImageQualityReviewRequest request, LabelingProjectData data)
        {
            lock (sessionLock)
            {
                if (request == null || !request.IsQualityReviewPurpose || !ContainsImage(request.ImagePath) || !CanReviewCore(data))
                    return ImageQualityReviewResult.NotApplicable();

                YoloImageReviewStatus current = reviewStatus.GetOrCreate(request.ImagePath);
                if (request.State == YoloImageQualityReviewState.Reviewed
                    && (request.IsSaveRequired || request.IsAnnotationDirty || !request.HasCompletedLabelWork))
                    return ImageQualityReviewResult.Rejected(current);

                imageRevisions[request.ImagePath] = ++revision;
                YoloImageReviewStatus status = request.State switch
                {
                    YoloImageQualityReviewState.NeedsFix => reviewStatus.MarkQualityNeedsFix(request.ImagePath, request.ImageName, request.QualityReviewNote),
                    YoloImageQualityReviewState.Reviewed => reviewStatus.MarkQualityReviewed(request.ImagePath, request.ImageName),
                    _ => reviewStatus.ClearQualityReview(request.ImagePath, request.ImageName)
                };
                reviewStatus.SaveReviewStatus(CaptureReviewData(data));
                return ImageQualityReviewResult.Accepted(status);
            }
        }

        public YoloImageReviewStatus InvalidateQualityReviewAfterEdit(string imagePath, string imageName = "")
        {
            return Mutate(imagePath, () => reviewStatus.InvalidateQualityReviewAfterEdit(imagePath, imageName));
        }

        public ImageQualityReviewReportResult ExportQualityReviewReport(LabelingProjectData data)
        {
            lock (sessionLock)
            {
                if (!CanReviewCore(data)) return ImageQualityReviewReportResult.NoOutputPath();
                string outputPath = YoloImageQualityReviewReportExportService.ResolveDefaultOutputPath(CaptureReviewData(data));
                if (string.IsNullOrWhiteSpace(outputPath)) return ImageQualityReviewReportResult.NoOutputPath();
                YoloImageQualityReviewReportExportResult result = YoloImageQualityReviewReportExportService.ExportMarkdown(reviewStatus.GetItems(), outputPath);
                return new ImageQualityReviewReportResult(result.OutputPath, result.TotalImageCount, result.UnreviewedCount, result.NeedsFixCount, result.ReviewedCount);
            }
        }

        private static LabelingProjectData CaptureReviewData(LabelingProjectData data)
        {
            // These are the inputs used by detection/segmentation label lookup and
            // review persistence. Normalizing the copy must not modify a live Recipe.
            return data == null ? null : new LabelingProjectData
            {
                OutputDataYamlPath = data.OutputDataYamlPath,
                OutputDataImageAndTxtPath = data.OutputDataImageAndTxtPath,
                ProjectSettings = new LabelingProjectSettings { DatasetPurpose = data.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection },
                ClassNamedList = data.ClassNamedList?.Select(item => item == null ? null : new LabelClass
                {
                    Text = item.Text,
                    DrawColor = item.DrawColor,
                    IsArchived = item.IsArchived
                }).ToList()
            };
        }
    }

    [Obsolete("Use ImageQualityReviewWorkflowService.", false)]
    public sealed class WpfImageQualityReviewWorkflowService : ImageQualityReviewWorkflowService
    {
        public WpfImageQualityReviewWorkflowService(YoloImageReviewStatusService reviewStatus)
            : base(reviewStatus)
        {
        }
    }

    public class ImageQualityReviewRequest
    {
        public ImageQualityReviewRequest(
            string imagePath,
            string imageName,
            YoloImageQualityReviewState state,
            bool isQualityReviewPurpose,
            bool isSaveRequired,
            bool isAnnotationDirty,
            bool hasCompletedLabelWork,
            string qualityReviewNote)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            State = state;
            IsQualityReviewPurpose = isQualityReviewPurpose;
            IsSaveRequired = isSaveRequired;
            IsAnnotationDirty = isAnnotationDirty;
            HasCompletedLabelWork = hasCompletedLabelWork;
            QualityReviewNote = qualityReviewNote ?? string.Empty;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public YoloImageQualityReviewState State { get; }

        public bool IsQualityReviewPurpose { get; }

        public bool IsSaveRequired { get; }

        public bool IsAnnotationDirty { get; }

        public bool HasCompletedLabelWork { get; }

        public string QualityReviewNote { get; }
    }

    public class ImageQualityReviewResult
    {
        protected ImageQualityReviewResult(bool isApplicable, bool isAccepted, YoloImageReviewStatus status)
        {
            IsApplicable = isApplicable;
            IsAccepted = isAccepted;
            Status = status;
        }

        public bool IsApplicable { get; }

        public bool IsAccepted { get; }

        public YoloImageReviewStatus Status { get; }

        public static ImageQualityReviewResult NotApplicable()
        {
            return new ImageQualityReviewResult(false, false, null);
        }

        public static ImageQualityReviewResult Rejected(YoloImageReviewStatus status)
        {
            return new ImageQualityReviewResult(true, false, status);
        }

        public static ImageQualityReviewResult Accepted(YoloImageReviewStatus status)
        {
            return new ImageQualityReviewResult(true, true, status);
        }
    }

    public class ImageQualityReviewReportResult
    {
        protected internal ImageQualityReviewReportResult(
            string outputPath,
            int totalImageCount,
            int unreviewedCount,
            int needsFixCount,
            int reviewedCount)
        {
            OutputPath = outputPath ?? string.Empty;
            TotalImageCount = totalImageCount;
            UnreviewedCount = unreviewedCount;
            NeedsFixCount = needsFixCount;
            ReviewedCount = reviewedCount;
        }

        public string OutputPath { get; }

        public int TotalImageCount { get; }

        public int UnreviewedCount { get; }

        public int NeedsFixCount { get; }

        public int ReviewedCount { get; }

        public bool HasOutputPath => !string.IsNullOrWhiteSpace(OutputPath);

        internal static ImageQualityReviewReportResult NoOutputPath()
        {
            return new ImageQualityReviewReportResult(string.Empty, 0, 0, 0, 0);
        }
    }

    [Obsolete("Use ImageQualityReviewRequest.", false)]
    public sealed class WpfImageQualityReviewRequest : ImageQualityReviewRequest
    {
        public WpfImageQualityReviewRequest(
            string imagePath,
            string imageName,
            YoloImageQualityReviewState state,
            bool isQualityReviewPurpose,
            bool isSaveRequired,
            bool isAnnotationDirty,
            bool hasCompletedLabelWork,
            string qualityReviewNote)
            : base(
                imagePath,
                imageName,
                state,
                isQualityReviewPurpose,
                isSaveRequired,
                isAnnotationDirty,
                hasCompletedLabelWork,
                qualityReviewNote)
        {
        }
    }

    [Obsolete("Use ImageQualityReviewResult.", false)]
    public sealed class WpfImageQualityReviewResult : ImageQualityReviewResult
    {
        private WpfImageQualityReviewResult(bool isApplicable, bool isAccepted, YoloImageReviewStatus status)
            : base(isApplicable, isAccepted, status)
        {
        }

        internal static WpfImageQualityReviewResult FromCanonical(ImageQualityReviewResult source)
        {
            return source == null
                ? null
                : new WpfImageQualityReviewResult(source.IsApplicable, source.IsAccepted, source.Status);
        }

        public static new WpfImageQualityReviewResult NotApplicable()
        {
            return new WpfImageQualityReviewResult(false, false, null);
        }

        public static new WpfImageQualityReviewResult Rejected(YoloImageReviewStatus status)
        {
            return new WpfImageQualityReviewResult(true, false, status);
        }

        public static new WpfImageQualityReviewResult Accepted(YoloImageReviewStatus status)
        {
            return new WpfImageQualityReviewResult(true, true, status);
        }
    }

    [Obsolete("Use ImageQualityReviewReportResult.", false)]
    public sealed class WpfImageQualityReviewReportResult : ImageQualityReviewReportResult
    {
        internal WpfImageQualityReviewReportResult(
            string outputPath,
            int totalImageCount,
            int unreviewedCount,
            int needsFixCount,
            int reviewedCount)
            : base(outputPath, totalImageCount, unreviewedCount, needsFixCount, reviewedCount)
        {
        }

        internal static WpfImageQualityReviewReportResult FromCanonical(ImageQualityReviewReportResult source)
        {
            return source == null
                ? null
                : new WpfImageQualityReviewReportResult(
                    source.OutputPath,
                    source.TotalImageCount,
                    source.UnreviewedCount,
                    source.NeedsFixCount,
                    source.ReviewedCount);
        }

        internal static new WpfImageQualityReviewReportResult NoOutputPath()
        {
            return new WpfImageQualityReviewReportResult(string.Empty, 0, 0, 0, 0);
        }
    }
}
