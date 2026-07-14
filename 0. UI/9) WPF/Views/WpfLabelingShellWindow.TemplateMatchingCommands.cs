using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow : IWpfTemplateMatchingAutoLabelHost
    {
        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelBusy => isBatchDetectionRunning || isDetecting;

        bool IWpfTemplateMatchingAutoLabelHost.HasActiveAutoLabelImage => activeImageBitmap != null && !activeImageSize.IsEmpty;

        DrawingBitmap IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImage => activeImageBitmap;

        string IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImagePath => activeImagePath;

        CData IWpfTemplateMatchingAutoLabelHost.AutoLabelData => global.Data;

        int IWpfTemplateMatchingAutoLabelHost.MaximumTemplateMatchingCandidateCount
        {
            get
            {
                int configured = global.Data?.ProjectSettings?.PythonModel?.MaximumDetectionCandidates ?? 20;
                return Math.Clamp(configured, 1, 200);
            }
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSource(out DrawingRectangle templateBounds, out string className)
        {
            templateBounds = DrawingRectangle.Empty;
            className = string.Empty;

            if (TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                && selected?.Source == WpfObjectReviewSource.ManualRoi
                && selected.Index >= 0
                && selected.Index < manualRois.Count)
            {
                templateBounds = manualRois[selected.Index];
                className = GetManualRoiClassName(selected.Index);
                return !templateBounds.IsEmpty;
            }

            if (selected?.Source == WpfObjectReviewSource.ManualSegment
                && TryResolveManualSegmentTemplateSource(selected.Index, out templateBounds, out className))
            {
                return true;
            }

            if (manualRois.Count == 1)
            {
                templateBounds = manualRois[0];
                className = GetManualRoiClassName(0);
                return !templateBounds.IsEmpty;
            }

            if (manualRois.Count == 0
                && manualSegments.Count == 1
                && TryResolveManualSegmentTemplateSource(0, out templateBounds, out className))
            {
                return true;
            }

            return false;
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceSegment(
            out IReadOnlyList<DrawingPoint> points,
            out IReadOnlyList<IReadOnlyList<DrawingPoint>> cutouts)
        {
            points = Array.Empty<DrawingPoint>();
            cutouts = Array.Empty<IReadOnlyList<DrawingPoint>>();

            if (TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                && selected?.Source == WpfObjectReviewSource.ManualSegment
                && TryResolveManualSegmentTemplateShape(selected.Index, out points, out cutouts))
            {
                return true;
            }

            return manualRois.Count == 0
                && manualSegments.Count == 1
                && TryResolveManualSegmentTemplateShape(0, out points, out cutouts);
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceMask(
            out byte[] maskData,
            out System.Drawing.Size maskSize,
            out DrawingRectangle maskBounds)
        {
            maskData = Array.Empty<byte>();
            maskSize = System.Drawing.Size.Empty;
            maskBounds = DrawingRectangle.Empty;

            if (TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                && selected?.Source == WpfObjectReviewSource.ManualSegment
                && TryResolveManualSegmentTemplateMask(selected.Index, out maskData, out maskSize, out maskBounds))
            {
                return true;
            }

            return manualRois.Count == 0
                && manualSegments.Count == 1
                && TryResolveManualSegmentTemplateMask(0, out maskData, out maskSize, out maskBounds);
        }

        private bool TryResolveManualSegmentTemplateSource(int index, out DrawingRectangle templateBounds, out string className)
        {
            templateBounds = DrawingRectangle.Empty;
            className = string.Empty;

            if (index < 0 || index >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            if (segment == null || segment.Bounds.IsEmpty)
            {
                return false;
            }

            templateBounds = segment.Bounds;
            className = GetManualSegmentClassName(segment);
            return true;
        }

        private bool TryResolveManualSegmentTemplateShape(
            int index,
            out IReadOnlyList<DrawingPoint> points,
            out IReadOnlyList<IReadOnlyList<DrawingPoint>> cutouts)
        {
            points = Array.Empty<DrawingPoint>();
            cutouts = Array.Empty<IReadOnlyList<DrawingPoint>>();

            if (index < 0 || index >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            if (segment?.Points == null || segment.Points.Count < 3)
            {
                return false;
            }

            points = segment.Points.Select(point => point).ToList();
            cutouts = (segment.CutoutPolygons ?? new List<List<DrawingPoint>>())
                .Where(cutout => cutout?.Count >= 3)
                .Select(cutout => (IReadOnlyList<DrawingPoint>)cutout.Select(point => point).ToList())
                .ToList();
            return true;
        }

        private bool TryResolveManualSegmentTemplateMask(
            int index,
            out byte[] maskData,
            out System.Drawing.Size maskSize,
            out DrawingRectangle maskBounds)
        {
            maskData = Array.Empty<byte>();
            maskSize = System.Drawing.Size.Empty;
            maskBounds = DrawingRectangle.Empty;

            if (index < 0 || index >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            if (segment?.IsRasterMask != true || segment.MaskData == null || segment.MaskSize.IsEmpty)
            {
                return false;
            }

            maskData = segment.MaskData.ToArray();
            maskSize = segment.MaskSize;
            maskBounds = segment.Bounds;
            return !maskBounds.IsEmpty;
        }

        private static string GetManualSegmentClassName(LabelingSegmentationObject segment)
        {
            if (!string.IsNullOrWhiteSpace(segment?.ClassName))
            {
                return segment.ClassName;
            }

            if (!string.IsNullOrWhiteSpace(segment?.ClassItem?.Text))
            {
                return segment.ClassItem.Text;
            }

            return "Defect";
        }

        CClassItem IWpfTemplateMatchingAutoLabelHost.EnsureAutoLabelClassItem(string className)
        {
            return EnsureClassItem(className);
        }

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetVisibleAutoLabelQueueItems()
        {
            return GetVisibleQueueItems();
        }

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetAllAutoLabelQueueItems()
        {
            return imageQueueItems.ToList();
        }

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.BuildAutoLabelBatchQueue(IEnumerable<WpfImageQueueItem> items)
        {
            return detectionTargetService.BuildBatchQueue(items);
        }

        void IWpfTemplateMatchingAutoLabelHost.AppendAutoLabelLog(string message)
        {
            AppendLog(message);
        }

        void IWpfTemplateMatchingAutoLabelHost.ShowAutoLabelGuide(string title, string message)
        {
            SetGlobalInferenceStatus(title ?? string.Empty, isBusy: false, isWarning: true);
            WpfMessageDialog.ShowInfo(
                this,
                string.IsNullOrWhiteSpace(title) ? "\uD15C\uD50C\uB9BF \uC548\uB0B4" : title,
                message ?? string.Empty,
                "\uD655\uC778");
        }

        int IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            DrawingRectangle? sourceSegmentBounds,
            IReadOnlyList<DrawingPoint> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<DrawingPoint>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            System.Drawing.Size sourceMaskSize,
            DrawingRectangle sourceMaskBounds)
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> safeCandidates = candidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            if (!succeeded)
            {
                ApplyDetectionCandidates(safeCandidates, succeeded: false);
                return 0;
            }

            if (succeeded && safeCandidates.Count == 0)
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
        {
            SetPythonStatus(text);
        }

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelCommandStatus(string text, bool isBusy)
        {
            SetYoloCommandStatus(text, isBusy);
        }

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelGlobalInferenceStatus(string text, bool isBusy, bool isWarning)
        {
            SetGlobalInferenceStatus(text, isBusy, isWarning);
        }

        CancellationToken IWpfTemplateMatchingAutoLabelHost.StartAutoLabelBatch(int totalCount, string scopeText)
        {
            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = new CancellationTokenSource();
            isBatchDetectionRunning = true;
            batchDetectionTotalCount = Math.Max(0, totalCount);
            batchDetectionCompletedCount = 0;
            UpdateBatchDetectionControls(scopeText, string.Empty);
            UpdateYoloCommandButtons();
            return batchDetectionCts.Token;
        }

        void IWpfTemplateMatchingAutoLabelHost.MarkAutoLabelBatchItemRequested(WpfImageQueueItem item)
        {
            if (item == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            ApplyReviewStatusToItem(item, imageReviewStatus.SetDetectionRequested(item.ImagePath, imageName));
        }

        void IWpfTemplateMatchingAutoLabelHost.UpdateAutoLabelBatchProgress(
            string scopeText,
            string currentFileName,
            int completedCount,
            int totalCount)
        {
            batchDetectionCompletedCount = Math.Max(0, completedCount);
            batchDetectionTotalCount = Math.Max(0, totalCount);
            UpdateBatchDetectionControls(scopeText, currentFileName);
        }

        void IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelBatchResult(
            WpfImageQueueItem item,
            TemplateMatchingBatchAutoLabelItemResult result,
            bool saveReviewStatus)
        {
            if (item == null || result == null)
            {
                return;
            }

            YoloImageReviewStatus status;
            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            if (result.Saved)
            {
                status = imageReviewStatus.RefreshLabelStatusAndReviewState(
                    item.ImagePath,
                    result.ImageSize,
                    global.Data,
                    hasActiveCandidates: false)
                    ?? imageReviewStatus.MarkConfirmed(item.ImagePath, imageName);
            }
            else if (result.NoCandidate)
            {
                status = imageReviewStatus.SetDetectionNoCandidates(item.ImagePath, imageName);
            }
            else
            {
                status = imageReviewStatus.SetDetectionFailed(item.ImagePath, imageName, result.Message);
            }

            ApplyReviewStatusToItem(item, status);
            if (saveReviewStatus)
            {
                imageReviewStatus.SaveReviewStatus(global.Data);
            }

            UpdateImageQueueStatusText();
        }

        void IWpfTemplateMatchingAutoLabelHost.SaveAutoLabelReviewStatus()
        {
            imageReviewStatus.SaveReviewStatus(global.Data);
        }

        void IWpfTemplateMatchingAutoLabelHost.CompleteAutoLabelBatch(
            bool canceled,
            int completedCount,
            int totalCount,
            string scopeText)
        {
            isBatchDetectionRunning = false;
            batchDetectionCompletedCount = Math.Max(0, completedCount);
            batchDetectionTotalCount = Math.Max(0, totalCount);
            imageQueueView?.Refresh();
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            UpdateBatchDetectionControls(canceled ? "canceled" : "complete", string.Empty);
            UpdateYoloCommandButtons();
        }

        void IWpfTemplateMatchingAutoLabelHost.NotifyAutoLabelDataChanged()
        {
            global.System?.UpdateData();
        }

        Task IWpfTemplateMatchingAutoLabelHost.YieldAutoLabelBatchFrameAsync(CancellationToken token)
        {
            return YieldBatchDetectionResultFrameAsync(token);
        }

        private void ApplyTemplateNoCandidateResult()
        {
            candidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();
            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            AddCandidateReviewHistory("템플릿 초안 없음: 기준 박스는 결과에서 제외되며, 현재 이미지에서 추가 위치를 찾지 못했습니다.");
            AppendLog("Template matching no candidate: source box excluded, no extra current-image candidate.");

            if (!string.IsNullOrWhiteSpace(activeImagePath) && !activeImageSize.IsEmpty)
            {
                RefreshActiveImageQueueStatus(hasActiveCandidates: false);
            }

            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private int ApplyTemplateLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            DrawingRectangle? sourceSegmentBounds,
            IReadOnlyList<DrawingPoint> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<DrawingPoint>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            System.Drawing.Size sourceMaskSize,
            DrawingRectangle sourceMaskBounds)
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return 0;
            }

            var labelsToAdd = new List<(YoloWorkerSmokeCandidate Candidate, DrawingRectangle Bounds)>();
            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
                if (bounds.IsEmpty || IsTemplateLabelDuplicate(bounds, GetCandidateClassName(candidate), labelsToAdd.Select(item => item.Bounds)))
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

            RegisterAnnotationHistoryBeforeChange("Template label");
            candidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: true);
            int addedCount;
            if (IsSegmentationDatasetPurposeActive())
            {
                string className = GetCandidateClassName(labelsToAdd[0].Candidate);
                CClassItem classItem = EnsureClassItem(className);
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
                manualSegments.AddRange(transferredSegments);
                addedCount = transferredSegments.Count;
            }
            else
            {
                foreach ((YoloWorkerSmokeCandidate candidate, DrawingRectangle bounds) in labelsToAdd)
                {
                    string className = GetCandidateClassName(candidate);
                    EnsureClassItem(className);
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

            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            PopulateClassList();
            ShowSavedLabelsWorkflowView();
            SetModelStatus($"템플릿 라벨 초안 생성: {addedCount}개 / 위치 확인 후 라벨 저장");
            AddCandidateReviewHistory($"템플릿 라벨 초안 생성: {addedCount}개 / 저장 전 초안");
            AppendLog($"Template labels added: {addedCount}");
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
            return addedCount;
        }

        private bool IsTemplateLabelDuplicate(
            DrawingRectangle bounds,
            string className,
            IEnumerable<DrawingRectangle> pendingBounds)
        {
            string normalizedClassName = ClassCatalogService.NormalizeClassName(className);
            foreach (DrawingRectangle pending in pendingBounds ?? Array.Empty<DrawingRectangle>())
            {
                if (CalculateIntersectionOverUnion(bounds, pending) >= 0.9D)
                {
                    return true;
                }
            }

            for (int i = 0; i < manualRois.Count; i++)
            {
                if (string.Equals(ClassCatalogService.NormalizeClassName(GetManualRoiClassName(i)), normalizedClassName, StringComparison.OrdinalIgnoreCase)
                    && CalculateIntersectionOverUnion(bounds, manualRois[i]) >= 0.9D)
                {
                    return true;
                }
            }

            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment != null
                    && string.Equals(ClassCatalogService.NormalizeClassName(GetManualSegmentClassName(segment)), normalizedClassName, StringComparison.OrdinalIgnoreCase)
                    && CalculateIntersectionOverUnion(bounds, segment.Bounds) >= 0.9D)
                {
                    return true;
                }
            }

            foreach (YoloWorkerSmokeCandidate confirmed in confirmedDetectionCandidates)
            {
                if (string.Equals(ClassCatalogService.NormalizeClassName(GetCandidateClassName(confirmed)), normalizedClassName, StringComparison.OrdinalIgnoreCase)
                    && CalculateIntersectionOverUnion(bounds, GetClippedCandidateBounds(confirmed)) >= 0.9D)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
