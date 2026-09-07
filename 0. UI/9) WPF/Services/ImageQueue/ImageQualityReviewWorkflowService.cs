using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    // WPF owns the image-queue review workflow while the Core service remains the
    // single state and persistence authority for review-status.json.
    public class ImageQualityReviewWorkflowService
    {
        private readonly YoloImageReviewStatusService reviewStatus;

        public ImageQualityReviewWorkflowService(YoloImageReviewStatusService reviewStatus)
        {
            this.reviewStatus = reviewStatus ?? throw new ArgumentNullException(nameof(reviewStatus));
        }

        public IReadOnlyList<YoloImageReviewStatus> GetItems()
        {
            return reviewStatus.GetItems();
        }

        public void SetImages(IEnumerable<string> imagePaths)
        {
            reviewStatus.SetImages(imagePaths);
        }

        public void LoadReviewStatus(LabelingProjectData data, IEnumerable<string> imagePaths)
        {
            reviewStatus.LoadReviewStatus(data, imagePaths);
        }

        public void SaveReviewStatus(LabelingProjectData data)
        {
            reviewStatus.SaveReviewStatus(data);
        }

        public YoloImageReviewStatus GetOrCreate(string imagePath)
        {
            return reviewStatus.GetOrCreate(imagePath);
        }

        public YoloImageReviewStatus RefreshLabelStatus(string imagePath, Size imageSize, LabelingProjectData data)
        {
            return reviewStatus.RefreshLabelStatus(imagePath, imageSize, data);
        }

        public YoloImageReviewStatus RefreshLabelStatusAndReviewState(
            string imagePath,
            Size imageSize,
            LabelingProjectData data,
            bool hasActiveCandidates)
        {
            return reviewStatus.RefreshLabelStatusAndReviewState(imagePath, imageSize, data, hasActiveCandidates);
        }

        public YoloImageReviewStatus SetDetectionRequested(string imagePath, string imageName = "")
        {
            return reviewStatus.SetDetectionRequested(imagePath, imageName);
        }

        public YoloImageReviewStatus SetDetectionFailed(string imagePath, string imageName, string message)
        {
            return reviewStatus.SetDetectionFailed(imagePath, imageName, message);
        }

        public YoloImageReviewStatus SetDetectionNoCandidates(string imagePath, string imageName)
        {
            return reviewStatus.SetDetectionNoCandidates(imagePath, imageName);
        }

        public YoloImageReviewStatus SetDetectionCandidates(string imagePath, string imageName, int candidateCount)
        {
            return reviewStatus.SetDetectionCandidates(imagePath, imageName, candidateCount);
        }

        public YoloImageReviewStatus MarkConfirmed(string imagePath, string imageName = "")
        {
            return reviewStatus.MarkConfirmed(imagePath, imageName);
        }

        public YoloImageReviewStatus MarkSkipped(string imagePath, string imageName = "")
        {
            return reviewStatus.MarkSkipped(imagePath, imageName);
        }

        public bool TryFindNextUnlabeled(
            IReadOnlyList<string> orderedImagePaths,
            string currentImagePath,
            out string nextImagePath)
        {
            return reviewStatus.TryFindNextUnlabeled(orderedImagePaths, currentImagePath, out nextImagePath);
        }

        public ImageQualityReviewResult ApplyQualityReview(
            ImageQualityReviewRequest request,
            LabelingProjectData data)
        {
            if (request == null || !request.IsQualityReviewPurpose || string.IsNullOrWhiteSpace(request.ImagePath))
            {
                return ImageQualityReviewResult.NotApplicable();
            }

            YoloImageReviewStatus current = reviewStatus.GetOrCreate(request.ImagePath);
            if (request.State == YoloImageQualityReviewState.Reviewed
                && (request.IsSaveRequired
                    || request.IsAnnotationDirty
                    || !request.HasCompletedLabelWork))
            {
                return ImageQualityReviewResult.Rejected(current);
            }

            YoloImageReviewStatus status = request.State switch
            {
                YoloImageQualityReviewState.NeedsFix => reviewStatus.MarkQualityNeedsFix(
                    request.ImagePath,
                    request.ImageName,
                    request.QualityReviewNote),
                YoloImageQualityReviewState.Reviewed => reviewStatus.MarkQualityReviewed(
                    request.ImagePath,
                    request.ImageName),
                _ => reviewStatus.ClearQualityReview(
                    request.ImagePath,
                    request.ImageName)
            };
            reviewStatus.SaveReviewStatus(data);
            return ImageQualityReviewResult.Accepted(status);
        }

        public YoloImageReviewStatus InvalidateQualityReviewAfterEdit(string imagePath, string imageName = "")
        {
            return reviewStatus.InvalidateQualityReviewAfterEdit(imagePath, imageName);
        }

        public ImageQualityReviewReportResult ExportQualityReviewReport(LabelingProjectData data)
        {
            string outputPath = YoloImageQualityReviewReportExportService.ResolveDefaultOutputPath(data);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return ImageQualityReviewReportResult.NoOutputPath();
            }

            YoloImageQualityReviewReportExportResult result = YoloImageQualityReviewReportExportService.ExportMarkdown(
                reviewStatus.GetItems(),
                outputPath);
            return new ImageQualityReviewReportResult(
                result.OutputPath,
                result.TotalImageCount,
                result.UnreviewedCount,
                result.NeedsFixCount,
                result.ReviewedCount);
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
