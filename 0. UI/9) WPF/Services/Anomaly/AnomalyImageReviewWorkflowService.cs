using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    // WPF owns anomaly review policy and classification mapping while the Core
    // service remains the single persistence authority for anomaly-review-status.json.
    public class AnomalyImageReviewWorkflowService
    {
        private readonly AnomalyImageReviewStatusService reviewStatus;

        public AnomalyImageReviewWorkflowService(AnomalyImageReviewStatusService reviewStatus)
        {
            this.reviewStatus = reviewStatus ?? throw new ArgumentNullException(nameof(reviewStatus));
        }

        public IReadOnlyList<AnomalyImageReviewStatus> GetItems()
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

        public AnomalyImageReviewSummary LoadPersistedSummary(LabelingProjectData data, int totalImageCount = 0)
        {
            return AnomalyImageReviewStatusService.LoadPersistedSummary(data, totalImageCount);
        }

        public void SaveReviewStatus(LabelingProjectData data)
        {
            reviewStatus.SaveReviewStatus(data);
        }

        public AnomalyImageReviewStatus GetOrCreate(string imagePath)
        {
            return reviewStatus.GetOrCreate(imagePath);
        }

        public AnomalyImageReviewFolderImportResult PreviewUnreviewedStatesFromParentFolders()
        {
            return reviewStatus.PreviewUnreviewedStatesFromParentFolders();
        }

        public AnomalyImageReviewFolderImportResult ImportUnreviewedStatesFromParentFolders()
        {
            return reviewStatus.ImportUnreviewedStatesFromParentFolders();
        }

        public bool TryFindNextUnreviewed(
            IReadOnlyList<string> orderedImagePaths,
            string currentImagePath,
            out string nextImagePath)
        {
            return reviewStatus.TryFindNextUnreviewed(orderedImagePaths, currentImagePath, out nextImagePath);
        }

        public AnomalyImageReviewResult ApplyReviewState(
            AnomalyImageReviewRequest request,
            LabelingProjectData data)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ImagePath))
            {
                return AnomalyImageReviewResult.NotApplicable();
            }

            AnomalyImageReviewStatus status = ApplyReviewStateCore(request);
            if (request.SaveReviewStatus)
            {
                reviewStatus.SaveReviewStatus(data);
            }

            return AnomalyImageReviewResult.Applied(status);
        }

        public AnomalyClassificationResult ApplyClassification(
            AnomalyClassificationRequest request,
            LabelingProjectData data)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ImagePath))
            {
                return AnomalyClassificationResult.NotApplicable();
            }

            AnomalyClassificationDecision decision = AnomalyClassificationDecisionService.Build(
                request.Candidates,
                request.Options?.ToDecisionOptions());
            if (!decision.IsMapped)
            {
                return AnomalyClassificationResult.Unmapped(decision);
            }

            AnomalyImageReviewStatus status = ApplyReviewStateCore(new AnomalyImageReviewRequest(
                request.ImagePath,
                request.ImageName,
                decision.ReviewState,
                request.SaveReviewStatus));
            if (request.SaveReviewStatus)
            {
                reviewStatus.SaveReviewStatus(data);
            }

            return AnomalyClassificationResult.Mapped(decision, status);
        }

        private AnomalyImageReviewStatus ApplyReviewStateCore(AnomalyImageReviewRequest request)
        {
            return request.State switch
            {
                AnomalyImageReviewState.Normal => reviewStatus.MarkNormal(request.ImagePath, request.ImageName),
                AnomalyImageReviewState.Abnormal => reviewStatus.MarkAbnormal(request.ImagePath, request.ImageName),
                _ => reviewStatus.ClearReviewState(request.ImagePath, request.ImageName)
            };
        }
    }

    [Obsolete("Use AnomalyImageReviewWorkflowService.", false)]
    public sealed class WpfAnomalyImageReviewWorkflowService : AnomalyImageReviewWorkflowService
    {
        public WpfAnomalyImageReviewWorkflowService(AnomalyImageReviewStatusService reviewStatus)
            : base(reviewStatus)
        {
        }

        public WpfAnomalyImageReviewResult ApplyReviewState(
            WpfAnomalyImageReviewRequest request,
            LabelingProjectData data)
        {
            return WpfAnomalyImageReviewResult.FromCanonical(base.ApplyReviewState(request, data));
        }

        public WpfAnomalyClassificationResult ApplyClassification(
            WpfAnomalyClassificationRequest request,
            LabelingProjectData data)
        {
            return WpfAnomalyClassificationResult.FromCanonical(base.ApplyClassification(request, data));
        }
    }

    public class AnomalyImageReviewRequest
    {
        public AnomalyImageReviewRequest(
            string imagePath,
            string imageName,
            AnomalyImageReviewState state,
            bool saveReviewStatus)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            State = state;
            SaveReviewStatus = saveReviewStatus;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public AnomalyImageReviewState State { get; }

        public bool SaveReviewStatus { get; }
    }

    public class AnomalyClassificationRequest
    {
        public AnomalyClassificationRequest(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            AnomalyClassificationOptionsSnapshot options,
            bool saveReviewStatus)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            Candidates = candidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            Options = options;
            SaveReviewStatus = saveReviewStatus;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public IReadOnlyList<YoloWorkerSmokeCandidate> Candidates { get; }

        public AnomalyClassificationOptionsSnapshot Options { get; }

        public bool SaveReviewStatus { get; }
    }

    public class AnomalyClassificationOptionsSnapshot
    {
        public AnomalyClassificationOptionsSnapshot(
            IEnumerable<string> normalClassNames,
            IEnumerable<string> abnormalClassNames,
            double minimumConfidence)
        {
            NormalClassNames = new List<string>(normalClassNames ?? Array.Empty<string>());
            AbnormalClassNames = new List<string>(abnormalClassNames ?? Array.Empty<string>());
            MinimumConfidence = minimumConfidence;
        }

        public IReadOnlyList<string> NormalClassNames { get; }

        public IReadOnlyList<string> AbnormalClassNames { get; }

        public double MinimumConfidence { get; }

        public static AnomalyClassificationOptionsSnapshot From(AnomalyClassificationDecisionOptions options)
        {
            return new AnomalyClassificationOptionsSnapshot(
                options?.NormalClassNames,
                options?.AbnormalClassNames,
                options?.MinimumConfidence ?? 0D);
        }

        internal AnomalyClassificationDecisionOptions ToDecisionOptions()
        {
            return new AnomalyClassificationDecisionOptions
            {
                NormalClassNames = NormalClassNames,
                AbnormalClassNames = AbnormalClassNames,
                MinimumConfidence = MinimumConfidence
            };
        }
    }

    public class AnomalyImageReviewResult
    {
        protected AnomalyImageReviewResult(bool isApplicable, AnomalyImageReviewStatus status)
        {
            IsApplicable = isApplicable;
            Status = status;
        }

        public bool IsApplicable { get; }

        public AnomalyImageReviewStatus Status { get; }

        public static AnomalyImageReviewResult NotApplicable()
        {
            return new AnomalyImageReviewResult(false, null);
        }

        public static AnomalyImageReviewResult Applied(AnomalyImageReviewStatus status)
        {
            return new AnomalyImageReviewResult(true, status);
        }
    }

    public class AnomalyClassificationResult
    {
        protected AnomalyClassificationResult(
            bool isApplicable,
            bool isMapped,
            AnomalyClassificationDecision decision,
            AnomalyImageReviewStatus status)
        {
            IsApplicable = isApplicable;
            IsMapped = isMapped;
            Decision = decision;
            Status = status;
        }

        public bool IsApplicable { get; }

        public bool IsMapped { get; }

        public AnomalyClassificationDecision Decision { get; }

        public AnomalyImageReviewStatus Status { get; }

        public static AnomalyClassificationResult NotApplicable()
        {
            return new AnomalyClassificationResult(false, false, null, null);
        }

        public static AnomalyClassificationResult Unmapped(AnomalyClassificationDecision decision)
        {
            return new AnomalyClassificationResult(true, false, decision, null);
        }

        public static AnomalyClassificationResult Mapped(
            AnomalyClassificationDecision decision,
            AnomalyImageReviewStatus status)
        {
            return new AnomalyClassificationResult(true, true, decision, status);
        }
    }

    [Obsolete("Use AnomalyImageReviewRequest.", false)]
    public sealed class WpfAnomalyImageReviewRequest : AnomalyImageReviewRequest
    {
        public WpfAnomalyImageReviewRequest(
            string imagePath,
            string imageName,
            AnomalyImageReviewState state,
            bool saveReviewStatus)
            : base(imagePath, imageName, state, saveReviewStatus)
        {
        }
    }

    [Obsolete("Use AnomalyClassificationRequest.", false)]
    public sealed class WpfAnomalyClassificationRequest : AnomalyClassificationRequest
    {
        public WpfAnomalyClassificationRequest(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            WpfAnomalyClassificationOptionsSnapshot options,
            bool saveReviewStatus)
            : base(imagePath, imageName, candidates, options, saveReviewStatus)
        {
        }
    }

    [Obsolete("Use AnomalyClassificationOptionsSnapshot.", false)]
    public sealed class WpfAnomalyClassificationOptionsSnapshot : AnomalyClassificationOptionsSnapshot
    {
        public WpfAnomalyClassificationOptionsSnapshot(
            IEnumerable<string> normalClassNames,
            IEnumerable<string> abnormalClassNames,
            double minimumConfidence)
            : base(normalClassNames, abnormalClassNames, minimumConfidence)
        {
        }

        public static new WpfAnomalyClassificationOptionsSnapshot From(AnomalyClassificationDecisionOptions options)
        {
            return new WpfAnomalyClassificationOptionsSnapshot(
                options?.NormalClassNames,
                options?.AbnormalClassNames,
                options?.MinimumConfidence ?? 0D);
        }
    }

    [Obsolete("Use AnomalyImageReviewResult.", false)]
    public sealed class WpfAnomalyImageReviewResult : AnomalyImageReviewResult
    {
        private WpfAnomalyImageReviewResult(bool isApplicable, AnomalyImageReviewStatus status)
            : base(isApplicable, status)
        {
        }

        internal static WpfAnomalyImageReviewResult FromCanonical(AnomalyImageReviewResult source)
        {
            return source == null
                ? null
                : new WpfAnomalyImageReviewResult(source.IsApplicable, source.Status);
        }

        public static new WpfAnomalyImageReviewResult NotApplicable()
        {
            return new WpfAnomalyImageReviewResult(false, null);
        }

        public static new WpfAnomalyImageReviewResult Applied(AnomalyImageReviewStatus status)
        {
            return new WpfAnomalyImageReviewResult(true, status);
        }
    }

    [Obsolete("Use AnomalyClassificationResult.", false)]
    public sealed class WpfAnomalyClassificationResult : AnomalyClassificationResult
    {
        private WpfAnomalyClassificationResult(
            bool isApplicable,
            bool isMapped,
            AnomalyClassificationDecision decision,
            AnomalyImageReviewStatus status)
            : base(isApplicable, isMapped, decision, status)
        {
        }

        internal static WpfAnomalyClassificationResult FromCanonical(AnomalyClassificationResult source)
        {
            return source == null
                ? null
                : new WpfAnomalyClassificationResult(source.IsApplicable, source.IsMapped, source.Decision, source.Status);
        }

        public static new WpfAnomalyClassificationResult NotApplicable()
        {
            return new WpfAnomalyClassificationResult(false, false, null, null);
        }

        public static new WpfAnomalyClassificationResult Unmapped(AnomalyClassificationDecision decision)
        {
            return new WpfAnomalyClassificationResult(true, false, decision, null);
        }

        public static new WpfAnomalyClassificationResult Mapped(
            AnomalyClassificationDecision decision,
            AnomalyImageReviewStatus status)
        {
            return new WpfAnomalyClassificationResult(true, true, decision, status);
        }
    }
}
