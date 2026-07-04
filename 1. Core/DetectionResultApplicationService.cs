using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Timer = System.Threading.Timer;

namespace MvcVisionSystem._1._Core
{
    public enum DetectionCandidateUpdateReason
    {
        CandidatesChanged,
        RequestStarted,
        ResultCompleted,
        SelectionChanged,
        CandidatesCleared,
        CandidatesConfirmed,
        CandidateSkipped,
        RequestTimedOut
    }

    public sealed class DetectionCandidatesUpdatedEventArgs : EventArgs
    {
        public DetectionCandidatesUpdatedEventArgs(
            string imageName,
            string imagePath,
            int candidateCount,
            DetectionCandidateUpdateReason reason = DetectionCandidateUpdateReason.CandidatesChanged)
        {
            ImageName = imageName ?? string.Empty;
            ImagePath = imagePath ?? string.Empty;
            CandidateCount = candidateCount;
            Reason = reason;
        }

        public string ImageName { get; }

        public string ImagePath { get; }

        public int CandidateCount { get; }

        public DetectionCandidateUpdateReason Reason { get; }
    }

    public sealed class DetectionCandidateReviewItem
    {
        public DetectionCandidateReviewItem(
            int index,
            string className,
            float confidence,
            Rectangle rawBounds,
            Rectangle clippedBounds,
            bool isConfidenceAccepted,
            bool isInImageBounds,
            bool isSelected)
        {
            Index = index;
            ClassName = className ?? string.Empty;
            Confidence = confidence;
            RawBounds = rawBounds;
            ClippedBounds = clippedBounds;
            IsConfidenceAccepted = isConfidenceAccepted;
            IsInImageBounds = isInImageBounds;
            IsSelected = isSelected;
        }

        public int Index { get; }

        public string ClassName { get; }

        public float Confidence { get; }

        public Rectangle RawBounds { get; }

        public Rectangle ClippedBounds { get; }

        public bool IsConfidenceAccepted { get; }

        public bool IsInImageBounds { get; }

        public bool IsSelected { get; }

        public bool IsConfirmable => IsConfidenceAccepted && IsInImageBounds;
    }

    public sealed class DetectionResultApplicationService
    {
        private readonly object sync = new object();
        private List<DefectInfo> lastDefects = new List<DefectInfo>();
        private DetectionImageContext pendingDetectionContext = DetectionImageContext.Empty;
        private DetectionImageContext lastDetectionContext = DetectionImageContext.Empty;
        private int selectedCandidateIndex;
        private bool pendingDetectionCanceled;
        private Timer pendingDetectionTimeoutTimer;
        private int pendingDetectionTimeoutGeneration;

        public event EventHandler<DetectionCandidatesUpdatedEventArgs> DetectionCandidatesUpdated;

        public IReadOnlyList<DefectInfo> GetLastDefects()
        {
            lock (sync)
            {
                return lastDefects.ToList();
            }
        }

        public IReadOnlyList<DetectionCandidateReviewItem> GetLastCandidateReviewItems(CData data, float minimumConfidence = 0F)
        {
            IReadOnlyList<DefectInfo> defects = GetLastDefects();
            int selectedIndex = GetSelectedCandidateIndex();
            if (defects.Count == 0)
            {
                return Array.Empty<DetectionCandidateReviewItem>();
            }

            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return Array.Empty<DetectionCandidateReviewItem>();
            }

            DetectionImageContext activeContext = CaptureCurrentContext(data, currentImage.Size);
            DetectionImageContext detectionContext = GetLastDetectionContext();
            if (!detectionContext.Matches(activeContext))
            {
                return Array.Empty<DetectionCandidateReviewItem>();
            }

            Rectangle imageBounds = new Rectangle(Point.Empty, currentImage.Size);
            return defects
                .Select((defect, index) =>
                {
                    Rectangle rawBounds = ToRectangle(defect);
                    Rectangle clippedBounds = Rectangle.Intersect(rawBounds, imageBounds);
                    bool isInImageBounds = clippedBounds.Width > 0 && clippedBounds.Height > 0;
                    bool isConfidenceAccepted = defect.Confidence >= minimumConfidence;

                    return new DetectionCandidateReviewItem(
                        index + 1,
                        defect.ClassName,
                        defect.Confidence,
                        rawBounds,
                        clippedBounds,
                        isConfidenceAccepted,
                        isInImageBounds,
                        selectedIndex == index + 1);
                })
                .ToList();
        }

        public bool SelectDetectionCandidate(int candidateIndex, CData data)
        {
            if (candidateIndex <= 0)
            {
                return false;
            }

            IReadOnlyList<DefectInfo> defects = GetLastDefects();
            if (candidateIndex > defects.Count)
            {
                return false;
            }

            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return false;
            }

            DetectionImageContext activeContext = CaptureCurrentContext(data, currentImage.Size);
            DetectionImageContext detectionContext = GetLastDetectionContext();
            if (!detectionContext.Matches(activeContext))
            {
                return false;
            }

            DefectInfo selectedDefect = defects[candidateIndex - 1];
            if (selectedDefect.Width <= 0 || selectedDefect.Height <= 0)
            {
                return false;
            }

            lock (sync)
            {
                selectedCandidateIndex = candidateIndex;
            }

            List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(defects, ResolveClassColor, candidateIndex);
            CDisplayManager.SetDetectionOverlays("Main", overlays);
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.SelectionChanged);
            return true;
        }

        public bool TrySendCurrentImageForDetection(CCommunicationLearning communication, int detectionTimeoutSeconds = 30)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 검사 통신이 초기화되지 않았습니다.");
                return false;
            }

            if (CDisplayManager.ImageSrc == null || CDisplayManager.ImageSrc.Empty())
            {
                const string message = "현재 이미지가 비어 있어 검사 요청을 보낼 수 없습니다.";
                communication.SetLastError(message);
                AppLog.COMM(message);
                return false;
            }

            using (Bitmap bitmap = CImageConverter.ToBitmap(CDisplayManager.ImageSrc))
            {
                DetectionImageContext context = CaptureCurrentContext(CGlobal.Inst.Data, bitmap.Size);
                string requestId = Guid.NewGuid().ToString("N");
                string imageId = BuildImageId(context);
                RegisterPendingDetectionImage(CGlobal.Inst.Data, bitmap.Size, detectionTimeoutSeconds, requestId, imageId);

                var modelSettings = CGlobal.Inst.Data?.ProjectSettings?.PythonModel;
                bool sent = !string.IsNullOrWhiteSpace(context.ImagePath) && File.Exists(context.ImagePath)
                    ? communication.SendDetectImage(
                        requestId,
                        imageId,
                        context.ImagePath,
                        modelSettings?.MinimumDetectionConfidence ?? 0.25F,
                        modelSettings?.GetProtocolModelName() ?? "yolov5")
                    : communication.SendData(CCommunicationLearning.CommandLearning.StartDefect.ToString(), bitmap);
                if (!sent)
                {
                    ClearPendingDetectionContext();
                    const string message = "Python 모델 클라이언트가 연결되지 않아 현재 검사 요청을 보내지 못했습니다.";
                    communication.SetLastError(message);
                    AppLog.ABNORMAL(message);
                    return false;
                }

                communication.SetLastError("");
            }

            return true;
        }

        public bool TrySendImagePathForDetection(
            CCommunicationLearning communication,
            CData data,
            string imagePath,
            Size imageSize,
            int detectionTimeoutSeconds = 30)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 검사 통신이 초기화되지 않았습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                string message = $"검사 이미지 파일을 찾을 수 없습니다: {imagePath}";
                communication.SetLastError(message);
                AppLog.COMM(message);
                return false;
            }

            if (imageSize.IsEmpty)
            {
                string message = $"검사 이미지 크기를 확인할 수 없습니다: {imagePath}";
                communication.SetLastError(message);
                AppLog.COMM(message);
                return false;
            }

            string requestId = Guid.NewGuid().ToString("N");
            var context = new DetectionImageContext(
                Path.GetFileNameWithoutExtension(imagePath),
                imagePath,
                imageSize,
                requestId,
                Path.GetFileNameWithoutExtension(imagePath));
            RegisterPendingDetectionContext(context, detectionTimeoutSeconds);

            bool sent = communication.SendDetectImage(
                requestId,
                context.ImageId,
                imagePath,
                data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F,
                data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5");
            if (!sent)
            {
                ClearPendingDetectionContext();
                const string message = "Python 모델 클라이언트가 연결되지 않아 현재 검사 요청을 보내지 못했습니다.";
                communication.SetLastError(message);
                AppLog.ABNORMAL(message);
                return false;
            }

            communication.SetLastError("");
            return true;
        }

        public void RegisterPendingDetectionImage(
            CData data,
            Size imageSize,
            int detectionTimeoutSeconds = 30,
            string requestId = "",
            string imageId = "")
        {
            DetectionImageContext context = CaptureCurrentContext(data, imageSize, requestId, imageId);
            RegisterPendingDetectionContext(context, detectionTimeoutSeconds);
        }

        private void RegisterPendingDetectionContext(
            DetectionImageContext context,
            int detectionTimeoutSeconds = 30)
        {
            int generation;
            lock (sync)
            {
                pendingDetectionContext = context;
                lastDefects = new List<DefectInfo>();
                lastDetectionContext = DetectionImageContext.Empty;
                selectedCandidateIndex = 0;
                pendingDetectionCanceled = false;
                generation = ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
                int safeTimeoutSeconds = Math.Clamp(detectionTimeoutSeconds, 1, 600);
                pendingDetectionTimeoutTimer = new Timer(
                    _ => HandlePendingDetectionTimeout(context, generation, safeTimeoutSeconds),
                    null,
                    TimeSpan.FromSeconds(safeTimeoutSeconds),
                    Timeout.InfiniteTimeSpan);
            }

            CDisplayManager.SetDetectionOverlays("Main", null);
            RaiseDetectionCandidatesUpdated(context, 0, DetectionCandidateUpdateReason.RequestStarted);
        }

        public void CancelPendingDetection()
        {
            lock (sync)
            {
                pendingDetectionContext = DetectionImageContext.Empty;
                selectedCandidateIndex = 0;
                pendingDetectionCanceled = true;
                ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
            }
        }

        public bool ApplyToDetectLayer(IReadOnlyList<DefectInfo> defects, string requestId = "", string imageId = "")
        {
            if (CDisplayManager.IsDisplayInvokeRequired)
            {
                return CDisplayManager.InvokeOnDisplayThread(() => ApplyToDetectLayer(defects, requestId, imageId));
            }

            DetectionImageContext detectionContext = TakePendingDetectionContext();
            if (!detectionContext.MatchesResponse(requestId, imageId))
            {
                ClearLastResult();
                AppLog.COMM($"ResultDefect ignored because request/image id changed. Pending:{detectionContext.RequestId}/{detectionContext.ImageId}, Result:{requestId}/{imageId}");
                return false;
            }

            if (TakePendingDetectionCanceled())
            {
                ClearLastResult();
                AppLog.COMM("ResultDefect ignored because the pending detection request was cancelled.");
                return false;
            }

            if (defects == null || defects.Count == 0)
            {
                SetLastResult(defects, detectionContext);
                CDisplayManager.SetDetectionOverlays("Main", null);
                AppLog.NORMAL($"YOLO detection completed with no candidates. Image:{detectionContext.DisplayName}");
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.ResultCompleted);
                return false;
            }

            List<DefectInfo> reviewDefects = NormalizeDetectionCandidates(defects);
            if (reviewDefects.Count == 0)
            {
                SetLastResult(reviewDefects, detectionContext);
                CDisplayManager.SetDetectionOverlays("Main", null);
                AppLog.NORMAL($"YOLO detection completed, but no reviewable candidates were produced. Image:{detectionContext.DisplayName}, Raw:{defects.Count}");
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.ResultCompleted);
                return false;
            }

            if (CDisplayManager.ImageSrc == null || CDisplayManager.ImageSrc.Empty())
            {
                SetLastResult(reviewDefects, detectionContext);
                AppLog.NORMAL($"YOLO detection completed without active overlay source. Image:{detectionContext.DisplayName}, Candidates:{reviewDefects.Count}, Raw:{defects.Count}");
                RaiseDetectionCandidatesUpdated(detectionContext, reviewDefects.Count, DetectionCandidateUpdateReason.ResultCompleted);
                return true;
            }

            List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(reviewDefects, ResolveClassColor);
            if (overlays.Count == 0)
            {
                SetLastResult(reviewDefects, detectionContext);
                AppLog.NORMAL($"YOLO detection completed, but no drawable candidates were produced. Image:{detectionContext.DisplayName}, Raw:{defects.Count}");
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.ResultCompleted);
                return false;
            }

            var activeImageSize = new Size(CDisplayManager.ImageSrc.Width, CDisplayManager.ImageSrc.Height);
            DetectionImageContext activeContext = CaptureCurrentContext(CGlobal.Inst.Data, activeImageSize);
            if (!detectionContext.Matches(activeContext))
            {
                SetLastResult(reviewDefects, detectionContext);
                AppLog.NORMAL($"YOLO detection completed for non-active image. Image:{detectionContext.DisplayName}, Current:{activeContext.DisplayName}, Candidates:{reviewDefects.Count}, Raw:{defects.Count}");
                RaiseDetectionCandidatesUpdated(detectionContext, reviewDefects.Count, DetectionCandidateUpdateReason.ResultCompleted);
                return false;
            }

            SetLastResult(reviewDefects, detectionContext);
            if (CDisplayManager.GetMainDisplayOrNull() == null)
            {
                using (Bitmap source = CImageConverter.ToBitmap(CDisplayManager.ImageSrc))
                using (Bitmap image = CDrawBitmap.GetBitmapFormat24bppRgb(source))
                {
                    CDisplayManager.CreateLayerDisplay(image, "Main", false, overlays, activate: true);
                }
            }
            else
            {
                CDisplayManager.SetDetectionOverlays("Main", overlays);
            }

            CDisplayManager.ActivateLayer("Main");
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.ResultCompleted);

            return true;
        }

        public bool CommitLastDetectionToMainLabels(
            CData data,
            CSystem system,
            float minimumConfidence = 0F,
            bool createSegmentationFromBoxes = false)
        {
            IReadOnlyList<DefectInfo> defects = GetLastDefects();
            if (defects.Count == 0)
            {
                AppLog.COMM("No detection result is available to confirm as labels.");
                return false;
            }

            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (mainDisplay == null || currentImage == null)
            {
                AppLog.COMM("Detection result cannot be confirmed because Main image is not loaded.");
                return false;
            }

            DetectionImageContext activeContext = CaptureCurrentContext(data, currentImage.Size);
            DetectionImageContext detectionContext = GetLastDetectionContext();
            if (!detectionContext.Matches(activeContext))
            {
                AppLog.COMM($"Detection result cannot be confirmed because active image changed. Detection:{detectionContext.DisplayName}, Current:{activeContext.DisplayName}");
                return false;
            }

            if (data == null)
            {
                AppLog.ABNORMAL("Detection result cannot be confirmed because labeling data is not initialized.");
                return false;
            }

            data.ClassNamedList ??= new List<CClassItem>();

            int selectedIndex = GetSelectedCandidateIndex();
            Rectangle imageBounds = new Rectangle(Point.Empty, currentImage.Size);
            var indexedDefects = defects
                .Select((defect, index) => new IndexedDetectionItem(index + 1, defect))
                .Where(item => selectedIndex <= 0 || item.Index == selectedIndex)
                .ToList();

            var confirmableItems = indexedDefects
                .Select(item => TryBuildConfirmableRectangle(item.Defect, imageBounds, minimumConfidence, out Rectangle rectangle)
                    ? new ConfirmableDetectionItem(
                        item.Index,
                        item.Defect,
                        rectangle,
                        TryBuildConfirmableSegmentationPolygon(item.Defect, currentImage.Size, out List<Point> polygon) ? polygon : new List<Point>())
                    : null)
                .Where(item => item != null)
                .ToList();

            int confirmedCount = 0;
            int confirmedSegmentCount = 0;
            foreach (IGrouping<string, ConfirmableDetectionItem> group in confirmableItems
                .GroupBy(item => item.Defect.ClassName ?? "", System.StringComparer.OrdinalIgnoreCase))
            {
                CClassItem classItem = data.ClassNamedList
                    .FirstOrDefault(item => string.Equals(item.Text, group.Key, System.StringComparison.OrdinalIgnoreCase));
                if (classItem == null)
                {
                    if (!ClassCatalogService.TryAddClass(data, group.Key, out classItem))
                    {
                        AppLog.ABNORMAL($"Detection class is not in the labeling class list: {group.Key}");
                        continue;
                    }

                    AppLog.NORMAL($"Detection class added to labeling class list: {classItem.Text}");
                }

                List<Rectangle> rectangles = group.Select(item => item.Rectangle).ToList();

                if (rectangles.Count == 0)
                {
                    continue;
                }

                mainDisplay.SetRoiRectangles(rectangles, classItem, reset: false);
                if (createSegmentationFromBoxes)
                {
                    List<List<Point>> polygonSegments = group
                        .Where(item => item.SegmentationPolygon.Count >= 3)
                        .Select(item => item.SegmentationPolygon)
                        .ToList();
                    foreach (List<Point> polygon in polygonSegments)
                    {
                        if (mainDisplay.AddSegmentationPolygon(polygon, classItem, refresh: true, select: false, recordUndo: true))
                        {
                            confirmedSegmentCount++;
                        }
                    }

                    List<Rectangle> rectangleSegments = group
                        .Where(item => item.SegmentationPolygon.Count < 3)
                        .Select(item => item.Rectangle)
                        .ToList();
                    confirmedSegmentCount += mainDisplay.AddSegmentationRectangles(rectangleSegments, classItem, reset: false);
                }

                confirmedCount += rectangles.Count;
            }

            if (confirmedCount == 0)
            {
                AppLog.COMM("Detection result did not contain confirmable labels.");
                return false;
            }

            bool saved = CGlobal.Inst.LabelingWorkflow.CommitDisplayAnnotations(mainDisplay, data, system);
            if (saved)
            {
                UpdateDetectionStateAfterCommit(defects, confirmableItems, detectionContext);
            }

            AppLog.NORMAL($"Detection candidates confirmed as labels. Count:{confirmedCount}, Segments:{confirmedSegmentCount}");
            return saved;
        }

        public bool CommitSelectedDetectionToMainLabels(
            CData data,
            CSystem system,
            float minimumConfidence = 0F,
            bool createSegmentationFromBoxes = false)
        {
            if (GetSelectedCandidateIndex() <= 0)
            {
                AppLog.COMM("No detection candidate is selected to confirm.");
                return false;
            }

            return CommitLastDetectionToMainLabels(data, system, minimumConfidence, createSegmentationFromBoxes);
        }

        public bool CommitAllLastDetectionToMainLabels(
            CData data,
            CSystem system,
            float minimumConfidence = 0F,
            bool createSegmentationFromBoxes = false)
        {
            lock (sync)
            {
                selectedCandidateIndex = 0;
            }

            return CommitLastDetectionToMainLabels(data, system, minimumConfidence, createSegmentationFromBoxes);
        }

        public bool CanCommitSelectedDetection(CData data, float minimumConfidence = 0F)
        {
            return GetSelectedCandidateIndex() > 0 && CanCommitLastDetection(data, minimumConfidence);
        }

        public bool CanSkipSelectedDetectionCandidate(CData data)
        {
            return GetSelectedCandidateReviewItem(data) != null;
        }

        public bool SkipSelectedDetectionCandidate(CData data)
        {
            IReadOnlyList<DefectInfo> defects = GetLastDefects();
            int selectedIndex = GetSelectedCandidateIndex();
            if (selectedIndex <= 0 || selectedIndex > defects.Count)
            {
                AppLog.COMM("건너뛸 AI 후보가 선택되지 않았습니다.");
                return false;
            }

            DetectionCandidateReviewItem selectedItem = GetSelectedCandidateReviewItem(data);
            if (selectedItem == null)
            {
                AppLog.COMM("현재 이미지가 바뀌어 선택한 AI 후보를 건너뛸 수 없습니다.");
                return false;
            }

            DetectionImageContext detectionContext = GetLastDetectionContext();
            List<DefectInfo> remainingDefects = defects
                .Select((defect, index) => new IndexedDetectionItem(index + 1, defect))
                .Where(item => item.Index != selectedIndex)
                .Select(item => item.Defect)
                .ToList();

            if (remainingDefects.Count == 0)
            {
                CDisplayManager.SetDetectionOverlays("Main", null);
                ClearLastResult();
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.CandidateSkipped);
                AppLog.NORMAL($"AI 후보를 건너뛰었습니다. 후보:{selectedIndex}");
                return true;
            }

            SetLastResult(remainingDefects, detectionContext);
            List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(remainingDefects, ResolveClassColor);
            CDisplayManager.SetDetectionOverlays("Main", overlays);
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.CandidatesChanged);
            AppLog.NORMAL($"AI 후보를 건너뛰었습니다. 후보:{selectedIndex}");
            return true;
        }

        public bool CanCommitLastDetection(CData data, float minimumConfidence = 0F)
        {
            if (GetLastDefects().Count == 0)
            {
                return false;
            }

            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return false;
            }

            DetectionImageContext activeContext = CaptureCurrentContext(data, currentImage.Size);
            DetectionImageContext detectionContext = GetLastDetectionContext();
            if (!detectionContext.Matches(activeContext))
            {
                return false;
            }

            Rectangle imageBounds = new Rectangle(Point.Empty, currentImage.Size);
            int selectedIndex = GetSelectedCandidateIndex();
            return GetLastDefects()
                .Select((defect, index) => new IndexedDetectionItem(index + 1, defect))
                .Where(item => selectedIndex <= 0 || item.Index == selectedIndex)
                .Any(item => TryBuildConfirmableRectangle(item.Defect, imageBounds, minimumConfidence, out _));
        }

        private DetectionCandidateReviewItem GetSelectedCandidateReviewItem(CData data)
        {
            int selectedIndex = GetSelectedCandidateIndex();
            if (selectedIndex <= 0)
            {
                return null;
            }

            return GetLastCandidateReviewItems(data).FirstOrDefault(item => item.Index == selectedIndex);
        }

        private Color? ResolveClassColor(string className)
        {
            CClassItem classItem = CGlobal.Inst.Data?.ClassNamedList?
                .FirstOrDefault(item => string.Equals(item.Text, className, System.StringComparison.OrdinalIgnoreCase));
            return classItem?.DrawColor;
        }

        private static List<DefectInfo> NormalizeDetectionCandidates(IReadOnlyList<DefectInfo> defects)
        {
            int maximumCandidates = CGlobal.Inst.Data?.ProjectSettings?.PythonModel?.MaximumDetectionCandidates ?? 20;
            maximumCandidates = Math.Clamp(maximumCandidates, 1, 200);

            List<DefectInfo> reviewDefects = (defects ?? Array.Empty<DefectInfo>())
                .Where(defect => defect != null)
                .ToList();

            if (reviewDefects.Count <= maximumCandidates)
            {
                return reviewDefects;
            }

            return reviewDefects
                .OrderByDescending(defect => defect.Confidence)
                .ThenBy(defect => defect.ClassName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Take(maximumCandidates)
                .ToList();
        }

        private void UpdateDetectionStateAfterCommit(
            IReadOnlyList<DefectInfo> previousDefects,
            IReadOnlyList<ConfirmableDetectionItem> confirmedItems,
            DetectionImageContext detectionContext)
        {
            if (previousDefects == null || previousDefects.Count == 0 || confirmedItems == null || confirmedItems.Count == 0)
            {
                CDisplayManager.SetDetectionOverlays("Main", null);
                ClearLastResult();
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.CandidatesCleared);
                return;
            }

            HashSet<int> confirmedIndexes = confirmedItems
                .Select(item => item.CandidateIndex)
                .ToHashSet();

            List<DefectInfo> remainingDefects = previousDefects
                .Select((defect, index) => new IndexedDetectionItem(index + 1, defect))
                .Where(item => !confirmedIndexes.Contains(item.Index))
                .Select(item => item.Defect)
                .ToList();

            if (remainingDefects.Count == 0)
            {
                CDisplayManager.SetDetectionOverlays("Main", null);
                ClearLastResult();
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.CandidatesConfirmed);
                return;
            }

            SetLastResult(remainingDefects, detectionContext);
            List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(remainingDefects, ResolveClassColor);
            CDisplayManager.SetDetectionOverlays("Main", overlays);
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.CandidatesChanged);
        }

        private void SetLastResult(IReadOnlyList<DefectInfo> defects, DetectionImageContext context)
        {
            lock (sync)
            {
                lastDefects = defects?.ToList() ?? new List<DefectInfo>();
                lastDetectionContext = context ?? DetectionImageContext.Empty;
                selectedCandidateIndex = 0;
                ResetPendingDetectionTimeoutTimerLocked();
            }
        }

        private void ClearLastResult()
        {
            lock (sync)
            {
                lastDefects = new List<DefectInfo>();
                lastDetectionContext = DetectionImageContext.Empty;
                selectedCandidateIndex = 0;
                ResetPendingDetectionTimeoutTimerLocked();
            }
        }

        private int GetSelectedCandidateIndex()
        {
            lock (sync)
            {
                return selectedCandidateIndex;
            }
        }

        private DetectionImageContext TakePendingDetectionContext()
        {
            lock (sync)
            {
                DetectionImageContext context = pendingDetectionContext ?? DetectionImageContext.Empty;
                pendingDetectionContext = DetectionImageContext.Empty;
                ResetPendingDetectionTimeoutTimerLocked();
                return context;
            }
        }

        private bool TakePendingDetectionCanceled()
        {
            lock (sync)
            {
                bool canceled = pendingDetectionCanceled;
                pendingDetectionCanceled = false;
                return canceled;
            }
        }

        private void ClearPendingDetectionContext()
        {
            lock (sync)
            {
                pendingDetectionContext = DetectionImageContext.Empty;
                ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
            }
        }

        private DetectionImageContext GetLastDetectionContext()
        {
            lock (sync)
            {
                return lastDetectionContext ?? DetectionImageContext.Empty;
            }
        }

        private void HandlePendingDetectionTimeout(DetectionImageContext context, int generation, int timeoutSeconds)
        {
            DetectionImageContext timedOutContext = null;
            lock (sync)
            {
                if (generation != pendingDetectionTimeoutGeneration || !ReferenceEquals(pendingDetectionContext, context))
                {
                    return;
                }

                pendingDetectionContext = DetectionImageContext.Empty;
                lastDefects = new List<DefectInfo>();
                lastDetectionContext = DetectionImageContext.Empty;
                selectedCandidateIndex = 0;
                pendingDetectionCanceled = true;
                timedOutContext = context;
                ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
            }

            CDisplayManager.SetDetectionOverlays("Main", null);
            AppLog.ABNORMAL($"YOLO 검사 시간이 초과되었습니다. 제한:{timeoutSeconds}초 / 이미지:{timedOutContext.DisplayName}");
            RaiseDetectionCandidatesUpdated(timedOutContext, 0, DetectionCandidateUpdateReason.RequestTimedOut);
        }

        private void ResetPendingDetectionTimeoutTimerLocked()
        {
            Timer timer = pendingDetectionTimeoutTimer;
            pendingDetectionTimeoutTimer = null;
            timer?.Dispose();
        }

        private static Rectangle ToRectangle(DefectInfo defect)
        {
            return Rectangle.Round(new RectangleF(defect.X, defect.Y, defect.Width, defect.Height));
        }

        private static bool TryBuildConfirmableRectangle(DefectInfo defect, Rectangle imageBounds, float minimumConfidence, out Rectangle rectangle)
        {
            rectangle = Rectangle.Empty;
            if (defect == null || defect.Width <= 0 || defect.Height <= 0 || defect.Confidence < minimumConfidence)
            {
                return false;
            }

            rectangle = Rectangle.Intersect(ToRectangle(defect), imageBounds);
            return rectangle.Width > 0 && rectangle.Height > 0;
        }

        private static bool TryBuildConfirmableSegmentationPolygon(DefectInfo defect, Size imageSize, out List<Point> polygon)
        {
            polygon = new List<Point>();
            if (defect?.PolygonPoints == null || defect.PolygonPoints.Count < 3 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            foreach (DetectionPolygonPoint point in defect.PolygonPoints)
            {
                int x = Math.Max(0, Math.Min(imageSize.Width - 1, (int)Math.Round(point.X)));
                int y = Math.Max(0, Math.Min(imageSize.Height - 1, (int)Math.Round(point.Y)));
                polygon.Add(new Point(x, y));
            }

            polygon = MvcVisionSystem.SegmentationGeometry.NormalizePolygon(
                polygon,
                imageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);
            return polygon.Count >= 3;
        }

        private void RaiseDetectionCandidatesUpdated(
            DetectionImageContext context,
            int candidateCount,
            DetectionCandidateUpdateReason reason)
        {
            DetectionCandidatesUpdated?.Invoke(
                this,
                new DetectionCandidatesUpdatedEventArgs(
                    context?.ImageName ?? string.Empty,
                    context?.ImagePath ?? string.Empty,
                    candidateCount,
                    reason));
        }

        private static DetectionImageContext CaptureCurrentContext(CData data, Size imageSize, string requestId = "", string imageId = "")
        {
            string imageName = FirstNonEmpty(data?.LastSelectImageName, CGlobal.Inst.ImageWorkspace.ActiveImageName);
            string imagePath = FirstNonEmpty(data?.LastSelectImagePath, CGlobal.Inst.ImageWorkspace.ActiveImagePath);
            return new DetectionImageContext(imageName, imagePath, imageSize, requestId, imageId);
        }

        private static string BuildImageId(DetectionImageContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(context.ImagePath))
            {
                return Path.GetFileNameWithoutExtension(context.ImagePath);
            }

            return Path.GetFileNameWithoutExtension(context.ImageName ?? string.Empty);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private sealed class DetectionImageContext
        {
            public static readonly DetectionImageContext Empty = new DetectionImageContext(string.Empty, string.Empty, Size.Empty);

            public DetectionImageContext(string imageName, string imagePath, Size imageSize, string requestId = "", string imageId = "")
            {
                ImageName = imageName ?? string.Empty;
                ImagePath = imagePath ?? string.Empty;
                ImageSize = imageSize;
                RequestId = requestId ?? string.Empty;
                ImageId = imageId ?? string.Empty;
            }

            public string ImageName { get; }

            public string ImagePath { get; }

            public Size ImageSize { get; }

            public string RequestId { get; }

            public string ImageId { get; }

            public string DisplayName
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(ImagePath))
                    {
                        return Path.GetFileName(ImagePath);
                    }

                    return !string.IsNullOrWhiteSpace(ImageName) ? ImageName : "(unknown)";
                }
            }

            private bool HasIdentity => !string.IsNullOrWhiteSpace(ImagePath) || !string.IsNullOrWhiteSpace(ImageName);

            public bool Matches(DetectionImageContext current)
            {
                if (ReferenceEquals(this, Empty) || IsEmpty())
                {
                    return true;
                }

                if (current == null || current.IsEmpty())
                {
                    return !HasIdentity;
                }

                if (!string.IsNullOrWhiteSpace(ImagePath) && !string.IsNullOrWhiteSpace(current.ImagePath)
                    && !PathsEqual(ImagePath, current.ImagePath))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(ImageName) && !string.IsNullOrWhiteSpace(current.ImageName)
                    && !string.Equals(ImageName, current.ImageName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!ImageSize.IsEmpty && !current.ImageSize.IsEmpty && ImageSize != current.ImageSize)
                {
                    return false;
                }

                return !HasIdentity || current.HasIdentity;
            }

            public bool MatchesResponse(string requestId, string imageId)
            {
                if (!string.IsNullOrWhiteSpace(RequestId)
                    && !string.IsNullOrWhiteSpace(requestId)
                    && !string.Equals(RequestId, requestId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(ImageId)
                    && !string.IsNullOrWhiteSpace(imageId)
                    && !string.Equals(ImageId, imageId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }

            private bool IsEmpty()
            {
                return !HasIdentity && ImageSize.IsEmpty;
            }

            private static bool PathsEqual(string left, string right)
            {
                return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
            }

            private static string NormalizePath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return string.Empty;
                }

                try
                {
                    return Path.GetFullPath(path.Trim());
                }
                catch
                {
                    return path.Trim();
                }
            }
        }

        private sealed class ConfirmableDetectionItem
        {
            public ConfirmableDetectionItem(int candidateIndex, DefectInfo defect, Rectangle rectangle, List<Point> segmentationPolygon)
            {
                CandidateIndex = candidateIndex;
                Defect = defect;
                Rectangle = rectangle;
                SegmentationPolygon = segmentationPolygon ?? new List<Point>();
            }

            public int CandidateIndex { get; }

            public DefectInfo Defect { get; }

            public Rectangle Rectangle { get; }

            public List<Point> SegmentationPolygon { get; }
        }

        private sealed class IndexedDetectionItem
        {
            public IndexedDetectionItem(int index, DefectInfo defect)
            {
                Index = index;
                Defect = defect;
            }

            public int Index { get; }

            public DefectInfo Defect { get; }
        }
    }
}
