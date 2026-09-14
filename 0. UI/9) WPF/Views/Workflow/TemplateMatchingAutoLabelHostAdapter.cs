using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Implements the existing template auto-label host contract without owning
    /// a Window. UI dialogs and dispatcher yielding are explicit callbacks.
    /// </summary>
    internal sealed class TemplateMatchingAutoLabelHostAdapter : IWpfTemplateMatchingAutoLabelHost
    {
        private readonly TemplateMatchingAutoLabelHostAdapterContext context;

        internal TemplateMatchingAutoLabelHostAdapter(TemplateMatchingAutoLabelHostAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.TemplateMatchingSourceService);
            ArgumentNullException.ThrowIfNull(context.ClassCatalogWorkflowService);
            ArgumentNullException.ThrowIfNull(context.DetectionTargetService);
            ArgumentNullException.ThrowIfNull(context.BatchDetectionWorkflowService);
            ArgumentNullException.ThrowIfNull(context.ImageQualityReviewWorkflowService);
            ArgumentNullException.ThrowIfNull(context.CandidateReviewState);
        }

        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private Bitmap activeImageBitmap => context.ActiveImageBitmapProvider?.Invoke();
        private string activeImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
        private List<Rectangle> manualRois => context.ManualRois;
        private List<string> manualRoiClassNames => context.ManualRoiClassNames;
        private List<CanvasRoiShapeKind> manualRoiShapeKinds => context.ManualRoiShapeKinds;
        private List<string> manualRoiOverlayIds => context.ManualRoiOverlayIds;
        private List<LabelingSegmentationObject> manualSegments => context.ManualSegments;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates
            => context.CandidateReviewState.PendingCandidates;
        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates
            => context.CandidateReviewState.ConfirmedCandidates;

        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelBusy
            => context.IsBatchDetectionRunning?.Invoke() == true
                || context.IsImageDetectionRunning?.Invoke() == true;

        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelCloseApproved
            => context.IsApplicationCloseApproved?.Invoke() == true;

        bool IWpfTemplateMatchingAutoLabelHost.HasActiveAutoLabelImage
            => activeImageBitmap != null && !activeImageSize.IsEmpty;

        Bitmap IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImage => activeImageBitmap;

        string IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImagePath => activeImagePath;

        LabelingProjectData IWpfTemplateMatchingAutoLabelHost.AutoLabelData => projectData;

        int IWpfTemplateMatchingAutoLabelHost.MaximumTemplateMatchingCandidateCount
        {
            get
            {
                int configured = projectData?.ProjectSettings?.PythonModel?.MaximumDetectionCandidates ?? 20;
                return Math.Clamp(configured, 1, 200);
            }
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSource(
            out Rectangle templateBounds,
            out string className)
            => context.TemplateMatchingSourceService.TryResolveTemplateMatchingSource(
                context.CreateTemplateMatchingSourceSnapshot(),
                out templateBounds,
                out className);

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceSegment(
            out IReadOnlyList<Point> points,
            out IReadOnlyList<IReadOnlyList<Point>> cutouts)
            => context.TemplateMatchingSourceService.TryResolveTemplateMatchingSourceSegment(
                context.CreateTemplateMatchingSourceSnapshot(),
                out points,
                out cutouts);

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceMask(
            out byte[] maskData,
            out Size maskSize,
            out Rectangle maskBounds)
            => context.TemplateMatchingSourceService.TryResolveTemplateMatchingSourceMask(
                context.CreateTemplateMatchingSourceSnapshot(),
                out maskData,
                out maskSize,
                out maskBounds);

        LabelClass IWpfTemplateMatchingAutoLabelHost.EnsureAutoLabelClassItem(string className)
            => context.ClassCatalogWorkflowService.EnsureClassItem(projectData, className);

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetVisibleAutoLabelQueueItems()
            => context.VisibleQueueItemsProvider?.Invoke() ?? Array.Empty<WpfImageQueueItem>();

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetAllAutoLabelQueueItems()
            => context.AllQueueItemsProvider?.Invoke() ?? Array.Empty<WpfImageQueueItem>();

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.BuildAutoLabelBatchQueue(
            IEnumerable<WpfImageQueueItem> items)
            => context.DetectionTargetService.BuildBatchQueue(items);

        void IWpfTemplateMatchingAutoLabelHost.AppendAutoLabelLog(string message)
            => context.AppendLog?.Invoke(message);

        void IWpfTemplateMatchingAutoLabelHost.ShowAutoLabelGuide(string title, string message)
        {
            context.SetGlobalInferenceStatus?.Invoke(title ?? string.Empty, false, true);
            context.ShowGuide?.Invoke(title, message);
        }

        int IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            Rectangle? sourceSegmentBounds,
            IReadOnlyList<Point> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            Size sourceMaskSize,
            Rectangle sourceMaskBounds)
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> safeCandidates = candidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            if (!succeeded)
            {
                context.ApplyDetectionCandidates?.Invoke(safeCandidates, false);
                return 0;
            }

            if (safeCandidates.Count == 0)
            {
                ApplyTemplateNoCandidateResult();
                return 0;
            }

            return ApplyTemplateLabelCandidates(
                safeCandidates,
                sourceSegmentBounds,
                sourceSegmentPoints,
                sourceSegmentCutouts,
                sourceMaskData,
                sourceMaskSize,
                sourceMaskBounds);
        }

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelPythonStatus(string text)
            => context.SetPythonStatus?.Invoke(text);

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelCommandStatus(string text, bool isBusy)
            => context.SetCommandStatus?.Invoke(text, isBusy);

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelGlobalInferenceStatus(
            string text,
            bool isBusy,
            bool isWarning)
            => context.SetGlobalInferenceStatus?.Invoke(text, isBusy, isWarning);

        CancellationToken IWpfTemplateMatchingAutoLabelHost.StartAutoLabelBatch(int totalCount, string scopeText)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true
                || context.IsImageDetectionRunning?.Invoke() == true)
            {
                return new CancellationToken(canceled: true);
            }

            BatchDetectionRun run = context.BatchDetectionWorkflowService.TryBegin(
                totalCount,
                context.CaptureBatchReviewStatusSave?.Invoke());
            if (run == null)
            {
                return new CancellationToken(canceled: true);
            }

            context.UpdateBatchDetectionControls?.Invoke(scopeText ?? string.Empty, string.Empty);
            context.UpdateYoloCommandButtons?.Invoke();
            return run.Token;
        }

        void IWpfTemplateMatchingAutoLabelHost.MarkAutoLabelBatchItemRequested(WpfImageQueueItem item)
        {
            if (!context.ImageQualityReviewWorkflowService.CanReview(projectData)
                || (item != null && !ReferenceEquals(context.FindQueueItem?.Invoke(item.ImagePath), item))
                || item == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            context.ApplyReviewStatusToItem?.Invoke(
                item,
                context.ImageQualityReviewWorkflowService.SetDetectionRequested(item.ImagePath, imageName));
        }

        void IWpfTemplateMatchingAutoLabelHost.UpdateAutoLabelBatchProgress(
            string scopeText,
            string currentFileName,
            int completedCount,
            int totalCount)
            => context.UpdateBatchDetectionControls?.Invoke(scopeText ?? string.Empty, currentFileName ?? string.Empty);

        void IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelBatchResult(
            WpfImageQueueItem item,
            TemplateMatchingBatchAutoLabelItemResult result,
            bool saveReviewStatus)
        {
            if (context.BatchDetectionWorkflowService.Current?.CanApplyResult != true
                || !context.ImageQualityReviewWorkflowService.CanReview(projectData)
                || (item != null && !ReferenceEquals(context.FindQueueItem?.Invoke(item.ImagePath), item))
                || item == null
                || result == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            YoloImageReviewStatus status = result.Saved
                ? context.ImageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(
                    item.ImagePath,
                    result.ImageSize,
                    projectData,
                    hasActiveCandidates: false)
                    ?? context.ImageQualityReviewWorkflowService.MarkConfirmed(item.ImagePath, imageName)
                : result.NoCandidate
                    ? context.ImageQualityReviewWorkflowService.SetDetectionNoCandidates(item.ImagePath, imageName)
                    : context.ImageQualityReviewWorkflowService.SetDetectionFailed(item.ImagePath, imageName, result.Message);

            context.ApplyReviewStatusToItem?.Invoke(item, status);
            context.BatchDetectionWorkflowService.Current.RecordResult();
            if (saveReviewStatus)
            {
                context.BatchDetectionWorkflowService.Current.FlushReviewStatus();
            }

            context.UpdateImageQueueStatusText?.Invoke();
        }

        void IWpfTemplateMatchingAutoLabelHost.SaveAutoLabelReviewStatus()
            => context.BatchDetectionWorkflowService.Current?.FlushReviewStatus();

        void IWpfTemplateMatchingAutoLabelHost.CompleteAutoLabelBatch(
            bool canceled,
            int completedCount,
            int totalCount,
            string scopeText)
        {
            context.BatchDetectionWorkflowService.Current?.Complete();
            context.RefreshQueueView?.Invoke();
            context.RefreshActiveImageQueueStatus?.Invoke(pendingDetectionCandidates.Count > 0);
            context.UpdateBatchDetectionControls?.Invoke(
                canceled ? "canceled" : "complete",
                string.Empty);
            context.UpdateYoloCommandButtons?.Invoke();
        }

        void IWpfTemplateMatchingAutoLabelHost.NotifyAutoLabelDataChanged()
            => context.UpdateApplicationData?.Invoke();

        Task IWpfTemplateMatchingAutoLabelHost.YieldAutoLabelBatchFrameAsync(CancellationToken token)
            => context.YieldBatchFrameAsync?.Invoke(token) ?? Task.CompletedTask;

        private void ApplyTemplateNoCandidateResult()
        {
            context.CandidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: true);
            context.ClearCandidateReviewHistory?.Invoke();
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.AddCandidateReviewHistory?.Invoke(
                "템플릿 초안 없음: 기준 박스는 결과에서 제외되며, 현재 이미지에서 추가 위치를 찾지 못했습니다.");
            context.AppendLog?.Invoke("Template matching no candidate: source box excluded, no extra current-image candidate.");

            if (!string.IsNullOrWhiteSpace(activeImagePath) && !activeImageSize.IsEmpty)
            {
                context.RefreshActiveImageQueueStatus?.Invoke(false);
            }

            context.RefreshImageQueueViewAfterItemStateChange?.Invoke();
            context.UpdateImageQueueStatusText?.Invoke();
        }

        private int ApplyTemplateLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            Rectangle? sourceSegmentBounds,
            IReadOnlyList<Point> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            Size sourceMaskSize,
            Rectangle sourceMaskBounds)
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return 0;
            }

            var labelsToAdd = new List<(YoloWorkerSmokeCandidate Candidate, Rectangle Bounds)>();
            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                Rectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                if (bounds.IsEmpty
                    || IsTemplateLabelDuplicate(
                        bounds,
                        CandidateReviewPresenter.GetClassName(candidate),
                        labelsToAdd.Select(item => item.Bounds)))
                {
                    continue;
                }

                labelsToAdd.Add((candidate, bounds));
            }

            if (labelsToAdd.Count == 0)
            {
                ApplyTemplateNoCandidateResult();
                return 0;
            }

            context.RegisterAnnotationHistoryBeforeChange?.Invoke("Template label");
            context.CandidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: true);
            int addedCount;
            if (context.IsSegmentationDatasetPurposeActive?.Invoke() == true)
            {
                string className = CandidateReviewPresenter.GetClassName(labelsToAdd[0].Candidate);
                LabelClass classItem = context.ClassCatalogWorkflowService.EnsureClassItem(projectData, className);
                IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass =
                    TemplateMatchingBatchAutoLabelService.BuildSegmentsByClass(
                        classItem,
                        className,
                        labelsToAdd.Select(item => item.Candidate).ToList(),
                        activeImageSize,
                        sourceSegmentBounds,
                        sourceSegmentPoints,
                        sourceSegmentCutouts,
                        sourceMaskData,
                        sourceMaskSize,
                        sourceMaskBounds);
                List<LabelingSegmentationObject> transferredSegments = segmentsByClass
                    .Values
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .Where(segment => segment != null)
                    .ToList();
                int nextZOrder = SegmentationZOrderService.GetNextZOrder(manualSegments);
                for (int index = 0; index < transferredSegments.Count; index++)
                {
                    transferredSegments[index].ZOrder = nextZOrder + index;
                }

                manualSegments.AddRange(transferredSegments);
                addedCount = transferredSegments.Count;
            }
            else
            {
                foreach ((YoloWorkerSmokeCandidate candidate, Rectangle bounds) in labelsToAdd)
                {
                    string className = CandidateReviewPresenter.GetClassName(candidate);
                    context.ClassCatalogWorkflowService.EnsureClassItem(projectData, className);
                    manualRois.Add(bounds);
                    manualRoiClassNames.Add(className);
                    manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
                    manualRoiOverlayIds.Add(string.Empty);
                }

                addedCount = labelsToAdd.Count;
            }

            if (addedCount == 0)
            {
                ApplyTemplateNoCandidateResult();
                return 0;
            }

            context.ApplyCanvasDisplayMode?.Invoke(WpfCanvasDisplayMode.LabelsOnly, false, false);
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.PopulateClassList?.Invoke();
            context.ShowSavedLabelsWorkflowView?.Invoke();
            context.SetModelStatus?.Invoke($"템플릿 라벨 초안 생성: {addedCount}개 / 위치 확인 후 라벨 저장");
            context.AddCandidateReviewHistory?.Invoke($"템플릿 라벨 초안 생성: {addedCount}개 / 저장 전 초안");
            context.AppendLog?.Invoke($"Template labels added: {addedCount}");
            context.RefreshImageQueueViewAfterItemStateChange?.Invoke();
            context.UpdateImageQueueStatusText?.Invoke();
            return addedCount;
        }

        private bool IsTemplateLabelDuplicate(
            Rectangle bounds,
            string className,
            IEnumerable<Rectangle> pendingBounds)
        {
            string normalizedClassName = ClassCatalogService.NormalizeClassName(className);
            foreach (Rectangle pending in pendingBounds ?? Array.Empty<Rectangle>())
            {
                if (CandidateReviewPresenter.CalculateIntersectionOverUnion(bounds, pending) >= 0.9D)
                {
                    return true;
                }
            }

            for (int i = 0; i < manualRois.Count; i++)
            {
                if (string.Equals(
                        ClassCatalogService.NormalizeClassName(context.GetManualRoiClassName?.Invoke(i)),
                        normalizedClassName,
                        StringComparison.OrdinalIgnoreCase)
                    && CandidateReviewPresenter.CalculateIntersectionOverUnion(bounds, manualRois[i]) >= 0.9D)
                {
                    return true;
                }
            }

            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment != null
                    && string.Equals(
                        ClassCatalogService.NormalizeClassName(
                            context.TemplateMatchingSourceService.GetManualSegmentClassName(segment)),
                        normalizedClassName,
                        StringComparison.OrdinalIgnoreCase)
                    && CandidateReviewPresenter.CalculateIntersectionOverUnion(bounds, segment.Bounds) >= 0.9D)
                {
                    return true;
                }
            }

            foreach (YoloWorkerSmokeCandidate confirmed in confirmedDetectionCandidates)
            {
                if (string.Equals(
                        ClassCatalogService.NormalizeClassName(CandidateReviewPresenter.GetClassName(confirmed)),
                        normalizedClassName,
                        StringComparison.OrdinalIgnoreCase)
                    && CandidateReviewPresenter.CalculateIntersectionOverUnion(
                        bounds,
                        CandidateReviewPresentationService.ClipCandidateBounds(confirmed, activeImageSize)) >= 0.9D)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class TemplateMatchingAutoLabelHostAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal TemplateMatchingSourceService TemplateMatchingSourceService { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal DetectionTargetService DetectionTargetService { get; init; }
        internal BatchDetectionWorkflowService BatchDetectionWorkflowService { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal List<Rectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<Bitmap> ActiveImageBitmapProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<TemplateMatchingSourceSnapshot> CreateTemplateMatchingSourceSnapshot { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> VisibleQueueItemsProvider { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> AllQueueItemsProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsImageDetectionRunning { get; init; }
        internal Func<bool> IsBatchDetectionRunning { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string, string> ShowGuide { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyDetectionCandidates { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string, bool> SetCommandStatus { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Func<Action> CaptureBatchReviewStatusSave { get; init; }
        internal Action<string, string> UpdateBatchDetectionControls { get; init; }
        internal Action UpdateYoloCommandButtons { get; init; }
        internal Func<string, WpfImageQueueItem> FindQueueItem { get; init; }
        internal Action<WpfImageQueueItem, YoloImageReviewStatus> ApplyReviewStatusToItem { get; init; }
        internal Action RefreshQueueView { get; init; }
        internal Action<bool> RefreshActiveImageQueueStatus { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action UpdateApplicationData { get; init; }
        internal Func<CancellationToken, Task> YieldBatchFrameAsync { get; init; }
        internal Action ClearCandidateReviewHistory { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action RefreshImageQueueViewAfterItemStateChange { get; init; }
        internal Func<bool> IsSegmentationDatasetPurposeActive { get; init; }
        internal Action<string> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action PopulateClassList { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Func<int, string> GetManualRoiClassName { get; init; }
    }
}
