using OpenCvSharp.Extensions;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class DetectionResultApplicationService
    {
        private readonly object sync = new object();
        private readonly DetectionTransportService transport;
        private readonly LabelingWorkflowService labelingWorkflow;
        private readonly Func<LabelingProjectData> dataAccessor;
        private List<DefectInfo> lastDefects = new List<DefectInfo>();
        private DetectionRequestContext lastDetectionContext = DetectionRequestContext.Empty;
        private int selectedCandidateIndex;

        public DetectionResultApplicationService(
            LabelingImageWorkspace imageWorkspace,
            Func<LabelingProjectData> dataAccessor = null)
            : this(new DetectionTransportService(
                    dataAccessor ?? (() => null),
                    (imageWorkspace ?? throw new ArgumentNullException(nameof(imageWorkspace))).CaptureSnapshot),
                new LabelingWorkflowService(imageWorkspace),
                dataAccessor ?? (() => null))
        {
        }

        internal DetectionResultApplicationService(
            DetectionTransportService transport,
            LabelingWorkflowService labelingWorkflow,
            Func<LabelingProjectData> dataAccessor)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.labelingWorkflow = labelingWorkflow ?? throw new ArgumentNullException(nameof(labelingWorkflow));
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.transport.RequestStarted = HandleDetectionRequestStarted;
            this.transport.RequestTimedOut = HandleDetectionRequestTimedOut;
        }

        public DetectionTransportService Transport => transport;

        public event EventHandler<DetectionCandidatesUpdatedEventArgs> DetectionCandidatesUpdated;

        public IReadOnlyList<DefectInfo> GetLastDefects()
        {
            lock (sync)
            {
                return lastDefects.ToList();
            }
        }

        public IReadOnlyList<DetectionCandidateReviewItem> GetLastCandidateReviewItems(LabelingProjectData data, float minimumConfidence = 0F)
        {
            IReadOnlyList<DefectInfo> defects = GetLastDefects();
            int selectedIndex = GetSelectedCandidateIndex();
            if (defects.Count == 0)
            {
                return Array.Empty<DetectionCandidateReviewItem>();
            }

            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return Array.Empty<DetectionCandidateReviewItem>();
            }

            DetectionRequestContext activeContext = CaptureCurrentContext(currentImage.Size);
            DetectionRequestContext detectionContext = GetLastDetectionContext();
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

        public bool SelectDetectionCandidate(int candidateIndex, LabelingProjectData data)
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

            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return false;
            }

            DetectionRequestContext activeContext = CaptureCurrentContext(currentImage.Size);
            DetectionRequestContext detectionContext = GetLastDetectionContext();
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
            DisplayManager.SetDetectionOverlays("Main", overlays);
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.SelectionChanged);
            return true;
        }

        public bool TrySendCurrentImageForDetection(PythonModelCommunication communication, int detectionTimeoutSeconds = 30)
            => transport.TrySendCurrentImageForDetection(communication, detectionTimeoutSeconds);

        public bool TrySendImagePathForDetection(
            PythonModelCommunication communication,
            LabelingProjectData data,
            string imagePath,
            Size imageSize,
            int detectionTimeoutSeconds = 30)
            => transport.TrySendImagePathForDetection(
                communication,
                data,
                imagePath,
                imageSize,
                detectionTimeoutSeconds);

        public void RegisterPendingDetectionImage(
            LabelingImageSnapshot image,
            Size imageSize,
            int detectionTimeoutSeconds = 30,
            string requestId = "",
            string imageId = "")
            => transport.RegisterPendingDetectionImage(image, imageSize, detectionTimeoutSeconds, requestId, imageId);

        public void CancelPendingDetection()
        {
            transport.CancelPendingDetection();
            lock (sync)
            {
                selectedCandidateIndex = 0;
            }
        }

        public bool ApplyToDetectLayer(IReadOnlyList<DefectInfo> defects, string requestId = "", string imageId = "")
        {
            if (DisplayManager.IsDisplayInvokeRequired)
            {
                return DisplayManager.InvokeOnDisplayThread(() => ApplyToDetectLayer(defects, requestId, imageId));
            }

            if (transport.TakePendingDetectionCanceled())
            {
                ClearLastResult();
                AppLog.COMM("ResultDefect ignored because the pending detection request was cancelled.");
                return false;
            }

            DetectionRequestContext detectionContext;
            if (!transport.TryTakePendingDetectionContext(requestId, imageId, out detectionContext))
            {
                if (transport.HasPendingDetectionContext)
                {
                    AppLog.COMM($"ResultDefect ignored because request/image id changed. Result:{requestId}/{imageId}");
                    return false;
                }

                if (transport.HasCompletedDetectionContext
                    || !string.IsNullOrWhiteSpace(requestId)
                    || !string.IsNullOrWhiteSpace(imageId))
                {
                    AppLog.COMM($"ResultDefect ignored because no matching detection run is pending. Result:{requestId}/{imageId}");
                    return false;
                }

                detectionContext = DetectionRequestContext.Empty;
            }

            Size activeImageSize = DisplayManager.ImageSrc == null || DisplayManager.ImageSrc.Empty()
                ? Size.Empty
                : new Size(DisplayManager.ImageSrc.Width, DisplayManager.ImageSrc.Height);
            DetectionRequestContext activeContext = CaptureCurrentContext(activeImageSize);
            if (!detectionContext.Matches(activeContext))
            {
                DetectionRequestContext previousContext = GetLastDetectionContext();
                if (!previousContext.Matches(activeContext))
                {
                    ClearLastResult();
                }

                AppLog.COMM($"YOLO detection result ignored because the active image changed. Result:{detectionContext.DisplayName}, Current:{activeContext.DisplayName}");
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.StaleResultIgnored);
                return false;
            }

            if (defects == null || defects.Count == 0)
            {
                SetLastResult(defects, detectionContext);
                DisplayManager.SetDetectionOverlays("Main", null);
                AppLog.NORMAL($"YOLO detection completed with no candidates. Image:{detectionContext.DisplayName}");
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.ResultCompleted);
                return false;
            }

            List<DefectInfo> reviewDefects = NormalizeDetectionCandidates(defects);
            if (reviewDefects.Count == 0)
            {
                SetLastResult(reviewDefects, detectionContext);
                DisplayManager.SetDetectionOverlays("Main", null);
                AppLog.NORMAL($"YOLO detection completed, but no reviewable candidates were produced. Image:{detectionContext.DisplayName}, Raw:{defects.Count}");
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.ResultCompleted);
                return false;
            }

            if (DisplayManager.ImageSrc == null || DisplayManager.ImageSrc.Empty())
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

            SetLastResult(reviewDefects, detectionContext);
            if (DisplayManager.GetMainDisplayOrNull() == null)
            {
                using (Bitmap source = BitmapConverter.ToBitmap(DisplayManager.ImageSrc))
                using (Bitmap image = BitmapDrawingUtilities.GetBitmapFormat24bppRgb(source))
                {
                    DisplayManager.CreateLayerDisplay(image, "Main", false, overlays, activate: true);
                }
            }
            else
            {
                DisplayManager.SetDetectionOverlays("Main", overlays);
            }

            DisplayManager.ActivateLayer("Main");
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.ResultCompleted);

            return true;
        }

        public bool CommitLastDetectionToMainLabels(
            LabelingProjectData data,
            ApplicationRuntimeState system,
            float minimumConfidence = 0F,
            bool createSegmentationFromBoxes = false)
        {
            IReadOnlyList<DefectInfo> defects = GetLastDefects();
            if (defects.Count == 0)
            {
                AppLog.COMM("No detection result is available to confirm as labels.");
                return false;
            }

            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (mainDisplay == null || currentImage == null)
            {
                AppLog.COMM("Detection result cannot be confirmed because Main image is not loaded.");
                return false;
            }

            DetectionRequestContext activeContext = CaptureCurrentContext(currentImage.Size);
            DetectionRequestContext detectionContext = GetLastDetectionContext();
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

            data.ClassNamedList ??= new List<LabelClass>();

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
                LabelClass classItem = data.ClassNamedList
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

            bool saved = labelingWorkflow.CommitDisplayAnnotations(
                mainDisplay,
                transport.CaptureCurrentImageSnapshot(),
                data,
                system);
            if (saved)
            {
                UpdateDetectionStateAfterCommit(defects, confirmableItems, detectionContext);
            }

            AppLog.NORMAL($"Detection candidates confirmed as labels. Count:{confirmedCount}, Segments:{confirmedSegmentCount}");
            return saved;
        }

        public bool CommitSelectedDetectionToMainLabels(
            LabelingProjectData data,
            ApplicationRuntimeState system,
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
            LabelingProjectData data,
            ApplicationRuntimeState system,
            float minimumConfidence = 0F,
            bool createSegmentationFromBoxes = false)
        {
            lock (sync)
            {
                selectedCandidateIndex = 0;
            }

            return CommitLastDetectionToMainLabels(data, system, minimumConfidence, createSegmentationFromBoxes);
        }

        public bool CanCommitSelectedDetection(LabelingProjectData data, float minimumConfidence = 0F)
        {
            return GetSelectedCandidateIndex() > 0 && CanCommitLastDetection(data, minimumConfidence);
        }

        public bool CanSkipSelectedDetectionCandidate(LabelingProjectData data)
        {
            return GetSelectedCandidateReviewItem(data) != null;
        }

        public bool SkipSelectedDetectionCandidate(LabelingProjectData data)
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

            DetectionRequestContext detectionContext = GetLastDetectionContext();
            List<DefectInfo> remainingDefects = defects
                .Select((defect, index) => new IndexedDetectionItem(index + 1, defect))
                .Where(item => item.Index != selectedIndex)
                .Select(item => item.Defect)
                .ToList();

            if (remainingDefects.Count == 0)
            {
                DisplayManager.SetDetectionOverlays("Main", null);
                ClearLastResult();
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.CandidateSkipped);
                AppLog.NORMAL($"AI 후보를 건너뛰었습니다. 후보:{selectedIndex}");
                return true;
            }

            SetLastResult(remainingDefects, detectionContext);
            List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(remainingDefects, ResolveClassColor);
            DisplayManager.SetDetectionOverlays("Main", overlays);
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.CandidatesChanged);
            AppLog.NORMAL($"AI 후보를 건너뛰었습니다. 후보:{selectedIndex}");
            return true;
        }

        public bool CanCommitLastDetection(LabelingProjectData data, float minimumConfidence = 0F)
        {
            if (GetLastDefects().Count == 0)
            {
                return false;
            }

            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return false;
            }

            DetectionRequestContext activeContext = CaptureCurrentContext(currentImage.Size);
            DetectionRequestContext detectionContext = GetLastDetectionContext();
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

        private DetectionCandidateReviewItem GetSelectedCandidateReviewItem(LabelingProjectData data)
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
            LabelClass classItem = dataAccessor()?.ClassNamedList?
                .FirstOrDefault(item => string.Equals(item.Text, className, System.StringComparison.OrdinalIgnoreCase));
            return classItem?.DrawColor;
        }

        private List<DefectInfo> NormalizeDetectionCandidates(IReadOnlyList<DefectInfo> defects)
        {
            int maximumCandidates = dataAccessor()?.ProjectSettings?.PythonModel?.MaximumDetectionCandidates ?? 20;
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
            DetectionRequestContext detectionContext)
        {
            if (previousDefects == null || previousDefects.Count == 0 || confirmedItems == null || confirmedItems.Count == 0)
            {
                DisplayManager.SetDetectionOverlays("Main", null);
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
                DisplayManager.SetDetectionOverlays("Main", null);
                ClearLastResult();
                RaiseDetectionCandidatesUpdated(detectionContext, 0, DetectionCandidateUpdateReason.CandidatesConfirmed);
                return;
            }

            SetLastResult(remainingDefects, detectionContext);
            List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(remainingDefects, ResolveClassColor);
            DisplayManager.SetDetectionOverlays("Main", overlays);
            RaiseDetectionCandidatesUpdated(detectionContext, overlays.Count, DetectionCandidateUpdateReason.CandidatesChanged);
        }

        private void SetLastResult(IReadOnlyList<DefectInfo> defects, DetectionRequestContext context)
        {
            lock (sync)
            {
                lastDefects = defects?.ToList() ?? new List<DefectInfo>();
                lastDetectionContext = context ?? DetectionRequestContext.Empty;
                selectedCandidateIndex = 0;
            }
        }

        private void ClearLastResult()
        {
            lock (sync)
            {
                lastDefects = new List<DefectInfo>();
                lastDetectionContext = DetectionRequestContext.Empty;
                selectedCandidateIndex = 0;
            }
        }

        private int GetSelectedCandidateIndex()
        {
            lock (sync)
            {
                return selectedCandidateIndex;
            }
        }

        private DetectionRequestContext GetLastDetectionContext()
        {
            lock (sync)
            {
                return lastDetectionContext ?? DetectionRequestContext.Empty;
            }
        }

        private void HandleDetectionRequestStarted(DetectionRequestContext context)
        {
            lock (sync)
            {
                lastDefects = new List<DefectInfo>();
                lastDetectionContext = DetectionRequestContext.Empty;
                selectedCandidateIndex = 0;
            }

            DisplayManager.SetDetectionOverlays("Main", null);
            RaiseDetectionCandidatesUpdated(context, 0, DetectionCandidateUpdateReason.RequestStarted);
        }

        private void HandleDetectionRequestTimedOut(DetectionRequestContext context, int timeoutSeconds)
        {
            lock (sync)
            {
                lastDefects = new List<DefectInfo>();
                lastDetectionContext = DetectionRequestContext.Empty;
                selectedCandidateIndex = 0;
            }

            DisplayManager.SetDetectionOverlays("Main", null);
            AppLog.ABNORMAL($"YOLO 검사 시간이 초과되었습니다. 제한:{timeoutSeconds}초 / 이미지:{context?.DisplayName}");
            RaiseDetectionCandidatesUpdated(context, 0, DetectionCandidateUpdateReason.RequestTimedOut);
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
            DetectionRequestContext context,
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

        private DetectionRequestContext CaptureCurrentContext(
            Size imageSize,
            string requestId = "",
            string imageId = "")
            => transport.CaptureCurrentContext(imageSize, requestId, imageId);

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
