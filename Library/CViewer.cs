using Lib.Common;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using static OpenVisionLab.DrawObject.DrawObjectEnums;
using Point = System.Drawing.Point;

namespace MvcVisionSystem
{
    public partial class CViewer : Component
    {
        private const string TextureName = "LabelingViewerImage";
        private const int DefaultSegmentationBrushRadius = 8;
        private const int MinSegmentationBrushRadius = 2;
        private const int MaxSegmentationBrushRadius = 48;
        private const int MaxUndoSnapshotCount = 30;
        private const int InteractiveRefreshIntervalMilliseconds = 33;

        private enum ViewerMenuCommand
        {
            None,
            ImageLoad,
            ImageSave,
            ShowFolder,
            ToggleCross,
            DeleteRoi,
            RoiList
        }

        private PosSizableRect _MouseOperation = PosSizableRect.None;
        private Point _StartPt = Point.Empty;
        private Point _EndPt = Point.Empty;
        private Point _MouseDown = Point.Empty;
        private Point _LastPoint = Point.Empty;
        private int _SelectROiIndex;
        private int _SelectSegmentIndex = -1;
        private int _ExcuteCount;
        private int _MinY = 0;
        private int _MaxY = 10000;
        private int _MinX = 0;
        private int _MaxX = 10000;
        private bool _OnlyDragMode;
        private bool _pendingImageLoad;
        private bool _pendingZoomToFit = true;
        private Size _imageSize = Size.Empty;
        private Bitmap _currentImage;
        private bool isDrawing;
        private bool segmentationEditDirty;
        private int segmentationBrushRadius = DefaultSegmentationBrushRadius;
        private AnnotationSnapshot annotationEditSnapshot;
        private Point? _measureLineStart;
        private Point? _measureLineEnd;
        private Point? _measureDistanceStart;
        private Point? _measureDistanceEnd;
        private float _measurePixelPermm = 1F;
        private List<Point> currentPoints = new List<Point>();
        private readonly List<DetectionOverlayItem> _detectionOverlays = new List<DetectionOverlayItem>();
        private readonly Stopwatch interactiveRefreshStopwatch = Stopwatch.StartNew();
        private readonly ContextMenuStrip imageContextMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip roiContextMenu = new ContextMenuStrip();

        private CRectangleObject _TempOb = new CRectangleObject();
        private bool _ViewCross;
        private LabelingRoiMode _Mode = LabelingRoiMode.Rectangle;
        private Point _Position = Point.Empty;
        private Color _Rgb = Color.Empty;
        private int _GrayValue;
        private Dictionary<string, List<CRectangleObject>> _RoiDic = new Dictionary<string, List<CRectangleObject>>();
        private Dictionary<string, List<LabelingSegmentationObject>> _SegmentationDic = new Dictionary<string, List<LabelingSegmentationObject>>();
        private readonly Stack<AnnotationSnapshot> undoSnapshots = new Stack<AnnotationSnapshot>();
        private readonly Stack<AnnotationSnapshot> redoSnapshots = new Stack<AnnotationSnapshot>();
        private string _SelectedClass = string.Empty;
        private string _SelectedSegmentClass = string.Empty;
        private bool _ImageChanged;
        public ImageCanvasControl Canvas { get; private set; }
        public Bitmap CurrentImage => _currentImage;
        public float ZoomPercent => Canvas == null ? 0F : Canvas.ZoomScale * 100F;
        public bool ImageChanged => _ImageChanged;
        public Point ImagePosition => _Position;
        public Color PixelRgb => _Rgb;
        public int GrayValue => _GrayValue;
        public int DetectionOverlayCount => _detectionOverlays.Count;
        public int SegmentationBrushRadius
        {
            get => segmentationBrushRadius;
            set
            {
                segmentationBrushRadius = Math.Clamp(value, MinSegmentationBrushRadius, MaxSegmentationBrushRadius);
                Canvas?.RefreshGL();
            }
        }
        public bool CanUndoAnnotationChange => undoSnapshots.Count > 0;
        public bool CanRedoAnnotationChange => redoSnapshots.Count > 0;
        public LabelingRoiMode CurrentMode => _OnlyDragMode ? LabelingRoiMode.Drag : _Mode;
        public string ModeDisplayText => GetUiModeDisplayText(CurrentMode);
        public int SelectedAnnotationListIndex => FindSelectedAnnotationListIndex();
        internal IReadOnlyDictionary<string, List<CRectangleObject>> RoiByClass => _RoiDic;
        internal IReadOnlyDictionary<string, List<LabelingSegmentationObject>> SegmentsByClass => _SegmentationDic;
        public event EventHandler<LabelingAnnotationSelectionChangedEventArgs> AnnotationSelectionChanged;

        private Rectangle TempROI
        {
            get => _TempOb.Roi;
            set => _TempOb.Roi = value;
        }

        private void ClearTemporaryDrawingState(bool clearEditSnapshot = false)
        {
            isDrawing = false;
            segmentationEditDirty = false;
            TempROI = Rectangle.Empty;
            currentPoints.Clear();
            _StartPt = Point.Empty;
            _EndPt = Point.Empty;
            _MouseDown = Point.Empty;
            _LastPoint = Point.Empty;
            _MouseOperation = PosSizableRect.None;

            if (clearEditSnapshot)
            {
                annotationEditSnapshot = null;
            }
        }

        public CViewer(bool bCenter = true)
        {
            ConfigureReadableWorkbenchContextMenu();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeViewerResources();
            }

            base.Dispose(disposing);
        }

        internal ImageCanvasControl AttachToWinFormsHost(Control host, bool onlyDragmode = false)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            DisposeCanvas();
            _OnlyDragMode = onlyDragmode;

            Canvas = new ImageCanvasControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                IsShowCrossLine = false,
                AllowDrop = true
            };

            Canvas.Draw += OnCanvasDraw;
            Canvas.MouseDoubleClicked += OnCanvasMouseDoubleClicked;
            Canvas.MouseDown += OnCanvasMouseDown;
            Canvas.MouseMove += OnCanvasMouseMove;
            Canvas.MouseUp += OnCanvasMouseUp;
            Canvas.MouseWheel += OnCanvasMouseWheel;
            Canvas.KeyDown += OnCanvasKeyDown;
            Canvas.Load += OnCanvasLoad;
            Canvas.DragEnter += OnCanvasDragEnter;
            Canvas.DragDrop += OnCanvasDragDrop;

            DisposeHostControls(host);
            host.Controls.Add(Canvas);

            SetModeDrag();
            return Canvas;
        }

        internal void DetachWinFormsCanvas(ImageCanvasControl canvas)
        {
            if (canvas == null || !ReferenceEquals(Canvas, canvas))
            {
                return;
            }

            DisposeCanvas();
        }

        private void OnCanvasLoad(object sender, EventArgs e)
        {
            LoadPendingImage();
        }

        public void ZoomToFit()
        {
            Canvas?.ZoomToFit();
        }

        public void ZoomIn()
        {
            Canvas?.ZoomAt(GetCanvasCenter(), 120);
        }

        public void ZoomOut()
        {
            Canvas?.ZoomAt(GetCanvasCenter(), -120);
        }

        public Point PointToImage(Point location)
        {
            if (_currentImage == null || Canvas == null)
            {
                return Point.Empty;
            }

            PointF robotPoint = Canvas.GetCurrentRobotPosf(location.X, location.Y);
            PointF imagePoint = Canvas.ConvertOpenGlToImagePoint(robotPoint);
            int x = Math.Clamp((int)Math.Round(imagePoint.X), 0, _currentImage.Width - 1);
            int y = Math.Clamp((int)Math.Round(imagePoint.Y), 0, _currentImage.Height - 1);
            return new Point(x, y);
        }

        public void ResetAnnotations()
        {
            ClearRasterMaskTextureCache();
            _RoiDic.Clear();
            _SegmentationDic.Clear();
            _SelectROiIndex = 0;
            _SelectSegmentIndex = -1;
            _SelectedSegmentClass = string.Empty;
            ClearTemporaryDrawingState(clearEditSnapshot: true);
            _MouseOperation = PosSizableRect.None;
            ClearUndoHistory();
            RaiseAnnotationSelectionChanged();
            Canvas?.RefreshGL();
        }

        public void AcceptImageChanged()
        {
            _ImageChanged = false;
        }

        public void SetSelectedClass(Yolo.CClassItem classItem)
        {
            _SelectedClass = classItem?.Text ?? string.Empty;
            _TempOb.cClassItem = classItem ?? new Yolo.CClassItem { Text = _SelectedClass };
            _TempOb.Color = classItem?.DrawColor ?? Color.LimeGreen;
            _TempOb.Title = _SelectedClass;
            Canvas?.RefreshGL();
        }

        public void SetRoiRectangles(IEnumerable<Rectangle> rectangles, Yolo.CClassItem classItem = null, bool reset = true)
        {
            if (reset)
            {
                _RoiDic.Clear();
            }

            Yolo.CClassItem targetClass = ResolveRoiClass(classItem);
            string className = targetClass.Text;

            if (!_RoiDic.TryGetValue(className, out List<CRectangleObject> rois))
            {
                rois = new List<CRectangleObject>();
                _RoiDic.Add(className, rois);
            }

            foreach (Rectangle rectangle in rectangles)
            {
                if (rectangle.Width <= 0 || rectangle.Height <= 0)
                {
                    continue;
                }

                rois.Add(new CRectangleObject
                {
                    Roi = rectangle,
                    cClassItem = targetClass,
                    Selected = false
                });
            }

            Canvas?.RefreshGL();
        }

        public bool UndoAnnotationChange()
        {
            if (undoSnapshots.Count == 0)
            {
                return false;
            }

            redoSnapshots.Push(CaptureAnnotationSnapshot());
            RestoreAnnotationSnapshot(undoSnapshots.Pop());
            Canvas?.RefreshGL();
            return true;
        }

        public bool RedoAnnotationChange()
        {
            if (redoSnapshots.Count == 0)
            {
                return false;
            }

            undoSnapshots.Push(CaptureAnnotationSnapshot());
            RestoreAnnotationSnapshot(redoSnapshots.Pop());
            Canvas?.RefreshGL();
            return true;
        }

        public bool DeleteSelectedAnnotation()
        {
            return ClearROI();
        }

        public void ClearUndoHistory()
        {
            undoSnapshots.Clear();
            redoSnapshots.Clear();
            annotationEditSnapshot = null;
        }

        public List<Rectangle> GetRoiRectangles()
        {
            var rectangles = new List<Rectangle>();
            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in _RoiDic)
            {
                for (int i = 0; i < roiGroup.Value.Count; i++)
                {
                    if (!roiGroup.Value[i].Roi.IsEmpty)
                    {
                        rectangles.Add(roiGroup.Value[i].Roi);
                    }
                }
            }

            return rectangles;
        }

        public void SetSegmentationPolygons(
            IReadOnlyDictionary<string, List<List<Point>>> polygonsByClass,
            IReadOnlyList<Yolo.CClassItem> classes,
            bool reset = true)
        {
            if (reset)
            {
                _SegmentationDic.Clear();
                _SelectedSegmentClass = string.Empty;
                _SelectSegmentIndex = -1;
            }

            if (polygonsByClass == null)
            {
                Canvas?.RefreshGL();
                return;
            }

            foreach (KeyValuePair<string, List<List<Point>>> group in polygonsByClass)
            {
                Yolo.CClassItem classItem = ResolveClassItem(group.Key, classes);
                SetSegmentationPolygons(group.Value, classItem, reset: false);
            }

            Canvas?.RefreshGL();
        }

        public void SetSegmentationPolygons(IEnumerable<IEnumerable<Point>> polygons, Yolo.CClassItem classItem = null, bool reset = true)
        {
            if (reset)
            {
                _SegmentationDic.Clear();
                _SelectedSegmentClass = string.Empty;
                _SelectSegmentIndex = -1;
            }

            foreach (IEnumerable<Point> polygon in polygons ?? Enumerable.Empty<IEnumerable<Point>>())
            {
                AddSegmentationPolygon(polygon, classItem, refresh: false, select: false, recordUndo: false);
            }

            Canvas?.RefreshGL();
        }

        public void SetSegmentationObjects(
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<Yolo.CClassItem> classes,
            bool reset = true)
        {
            if (reset)
            {
                _SegmentationDic.Clear();
                _SelectedSegmentClass = string.Empty;
                _SelectSegmentIndex = -1;
            }

            if (segmentsByClass == null)
            {
                Canvas?.RefreshGL();
                return;
            }

            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> group in segmentsByClass)
            {
                Yolo.CClassItem classItem = ResolveClassItem(group.Key, classes);
                if (!_SegmentationDic.TryGetValue(group.Key, out List<LabelingSegmentationObject> segments))
                {
                    segments = new List<LabelingSegmentationObject>();
                    _SegmentationDic[group.Key] = segments;
                }

                foreach (LabelingSegmentationObject item in group.Value ?? new List<LabelingSegmentationObject>())
                {
                    List<Point> points = SegmentationGeometry.NormalizePolygon(item?.Points, _currentImage?.Size ?? Size.Empty, simplificationTolerance: 1.5D);
                    if (points.Count < 3)
                    {
                        continue;
                    }

                    segments.Add(CreateSegmentObject(points, classItem, group.Key, selected: false, item?.CutoutPolygons));
                }
            }

            Canvas?.RefreshGL();
        }

        public bool AddSegmentationPolygon(IEnumerable<Point> polygon, Yolo.CClassItem classItem = null, bool refresh = true, bool select = true, bool recordUndo = true)
        {
            if (_currentImage == null)
            {
                return false;
            }

            List<Point> points = SegmentationGeometry.NormalizePolygon(polygon, _currentImage.Size, simplificationTolerance: 1.5D);
            if (points.Count < 3)
            {
                return false;
            }

            AnnotationSnapshot before = recordUndo ? CaptureAnnotationSnapshot() : null;
            Yolo.CClassItem targetClass = ResolveSegmentationClass(classItem);
            string className = targetClass.Text;
            if (!_SegmentationDic.TryGetValue(className, out List<LabelingSegmentationObject> segments))
            {
                segments = new List<LabelingSegmentationObject>();
                _SegmentationDic.Add(className, segments);
            }

            if (select)
            {
                ClearSegmentSelection();
            }

            var segment = CreateSegmentObject(points, targetClass, className, select);
            segments.Add(segment);
            if (select)
            {
                _SelectedSegmentClass = className;
                _SelectSegmentIndex = segments.Count - 1;
            }

            if (refresh)
            {
                Canvas?.RefreshGL();
            }

            if (recordUndo)
            {
                PushUndoSnapshot(before);
            }

            return true;
        }

        public int AddSegmentationRectangles(IEnumerable<Rectangle> rectangles, Yolo.CClassItem classItem = null, bool reset = false)
        {
            if (_currentImage == null)
            {
                return 0;
            }

            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            if (reset)
            {
                _SegmentationDic.Clear();
                _SelectedSegmentClass = string.Empty;
                _SelectSegmentIndex = -1;
            }

            int added = 0;
            foreach (Rectangle rectangle in rectangles ?? Enumerable.Empty<Rectangle>())
            {
                List<Point> polygon = SegmentationGeometry.RectangleToPolygon(rectangle, _currentImage.Size);
                if (AddSegmentationPolygon(polygon, classItem, refresh: false, recordUndo: false))
                {
                    added++;
                }
            }

            if (added > 0)
            {
                PushUndoSnapshot(before);
                Canvas?.RefreshGL();
            }

            return added;
        }

        public int AddSegmentationBrushStamps(IEnumerable<Point> centers, int radius = DefaultSegmentationBrushRadius, Yolo.CClassItem classItem = null)
        {
            if (_currentImage == null)
            {
                return 0;
            }

            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            int safeRadius = Math.Max(2, radius);
            int added = AddBrushStroke(centers, safeRadius, classItem);

            if (added > 0)
            {
                PushUndoSnapshot(before);
                Canvas?.RefreshGL();
            }

            return added;
        }

        public int EraseSegmentationAt(Point center, int radius = DefaultSegmentationBrushRadius)
        {
            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            int removed = RemoveSegmentsByStroke(new[] { center }, Math.Max(2, radius));
            if (removed > 0)
            {
                PushUndoSnapshot(before);
                Canvas?.RefreshGL();
            }

            return removed;
        }

        public int EraseSegmentationStroke(IEnumerable<Point> centers, int radius = DefaultSegmentationBrushRadius)
        {
            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            int removed = RemoveSegmentsByStroke(centers, Math.Max(2, radius));
            if (removed > 0)
            {
                PushUndoSnapshot(before);
                Canvas?.RefreshGL();
            }

            return removed;
        }

        public int AddAutoSegmentationFromRois(Yolo.CClassItem classItem = null, bool onlySelected = true)
        {
            if (_currentImage == null)
            {
                return 0;
            }

            List<CRectangleObject> rois = _RoiDic
                .SelectMany(group => group.Value ?? new List<CRectangleObject>())
                .Where(roi => roi != null && !roi.Roi.IsEmpty && (!onlySelected || roi.Selected))
                .ToList();
            if (rois.Count == 0 && onlySelected)
            {
                return 0;
            }

            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            int added = 0;
            foreach (CRectangleObject roi in rois)
            {
                List<Point> polygon = SegmentationGeometry.ExtractDarkRegionPolygon(_currentImage, roi.Roi);
                if (AddSegmentationPolygon(polygon, roi.cClassItem ?? classItem, refresh: false, recordUndo: false))
                {
                    added++;
                }
            }

            if (added > 0)
            {
                PushUndoSnapshot(before);
                Canvas?.RefreshGL();
            }

            return added;
        }

        public int MergeSegmentationSegments(string className = null)
        {
            if (_currentImage == null || _SegmentationDic.Count == 0)
            {
                return 0;
            }

            string targetClassName = ResolveMergeClassName(className);
            if (string.IsNullOrWhiteSpace(targetClassName)
                || !_SegmentationDic.TryGetValue(targetClassName, out List<LabelingSegmentationObject> segments))
            {
                return 0;
            }

            List<LabelingSegmentationObject> mergeTargets = segments
                .Where(segment => segment?.Points != null && segment.Points.Count >= 3)
                .ToList();
            if (mergeTargets.Count <= 1)
            {
                return 0;
            }

            List<Point> mergedPolygon = SegmentationGeometry.MergePolygonsToHull(
                mergeTargets.Select(segment => segment.Points),
                _currentImage.Size);
            if (mergedPolygon.Count < 3)
            {
                return 0;
            }

            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            Yolo.CClassItem targetClass = mergeTargets.FirstOrDefault(segment => segment?.ClassItem != null)?.ClassItem
                ?? ResolveSegmentationClass(new Yolo.CClassItem { Text = targetClassName });

            ClearSegmentSelection();
            List<List<Point>> mergedCutouts = mergeTargets
                .SelectMany(segment => segment.CutoutPolygons ?? new List<List<Point>>())
                .Where(cutout => cutout != null
                    && cutout.Count >= 3
                    && cutout.Any(point => SegmentationGeometry.ContainsPoint(mergedPolygon, point)))
                .Select(cutout => new List<Point>(cutout))
                .ToList();
            segments.Clear();
            segments.Add(CreateSegmentObject(mergedPolygon, targetClass, targetClass.Text, selected: true, mergedCutouts));
            _SelectedSegmentClass = targetClassName;
            _SelectSegmentIndex = 0;

            PushUndoSnapshot(before);
            Canvas?.RefreshGL();
            return mergeTargets.Count;
        }

        public List<List<Point>> GetSegmentationPolygons()
        {
            return _SegmentationDic.Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Where(IsSelectableSegment)
                .SelectMany(item => item.IsRasterMask
                    ? SegmentationGeometry.RasterMaskToRegions(item.MaskData, item.MaskSize, _currentImage?.Size ?? item.MaskSize)
                        .Select(region => new List<Point>(region.Points))
                    : new[] { new List<Point>(item.Points) })
                .ToList();
        }

        public List<List<Point>> GetSegmentationCutoutPolygons()
        {
            return _SegmentationDic.Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Where(item => item?.CutoutPolygons != null)
                .SelectMany(item => item.IsRasterMask
                    ? SegmentationGeometry.RasterMaskToRegions(item.MaskData, item.MaskSize, _currentImage?.Size ?? item.MaskSize)
                        .SelectMany(region => region.Cutouts)
                    : item.CutoutPolygons)
                .Where(cutout => cutout != null && cutout.Count >= 3)
                .Select(cutout => new List<Point>(cutout))
                .ToList();
        }

        public List<LabelingRoiListItem> GetRoiListItems()
        {
            var items = new List<LabelingRoiListItem>();
            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in _RoiDic)
            {
                for (int i = 0; i < roiGroup.Value.Count; i++)
                {
                    CRectangleObject roi = roiGroup.Value[i];
                    if (roi == null || roi.Roi.IsEmpty)
                    {
                        continue;
                    }

                    string className = roi.cClassItem?.Text ?? roiGroup.Key;
                    items.Add(new LabelingRoiListItem(
                        items.Count + 1,
                        className,
                        roi.Roi,
                        LabelingAnnotationKind.Rectangle,
                        i,
                        roi.Selected));
                }
            }

            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> segmentGroup in _SegmentationDic)
            {
                for (int i = 0; i < segmentGroup.Value.Count; i++)
                {
                    LabelingSegmentationObject segment = segmentGroup.Value[i];
                    if (!IsSelectableSegment(segment))
                    {
                        continue;
                    }

                    string className = segment.ClassItem?.Text ?? segment.ClassName ?? segmentGroup.Key;
                    items.Add(new LabelingRoiListItem(
                        items.Count + 1,
                        className,
                        segment.Bounds,
                        LabelingAnnotationKind.Segmentation,
                        i,
                        segment.Selected));
                }
            }

            return items;
        }

        public bool SelectAnnotationListItem(int listIndex)
        {
            if (listIndex <= 0)
            {
                return false;
            }

            int currentListIndex = 1;
            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in _RoiDic)
            {
                for (int i = 0; i < roiGroup.Value.Count; i++)
                {
                    CRectangleObject roi = roiGroup.Value[i];
                    if (roi == null || roi.Roi.IsEmpty)
                    {
                        continue;
                    }

                    if (currentListIndex == listIndex)
                    {
                        UnSelectAll();
                        roi.Selected = true;
                        _SelectedClass = roiGroup.Key;
                        _TempOb.cClassItem = roi.cClassItem;
                        _SelectROiIndex = i;
                        _SelectedSegmentClass = string.Empty;
                        _SelectSegmentIndex = -1;
                        RaiseAnnotationSelectionChanged();
                        Canvas?.RefreshGL();
                        return true;
                    }

                    currentListIndex++;
                }
            }

            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> segmentGroup in _SegmentationDic)
            {
                for (int i = 0; i < segmentGroup.Value.Count; i++)
                {
                    LabelingSegmentationObject segment = segmentGroup.Value[i];
                    if (!IsSelectableSegment(segment))
                    {
                        continue;
                    }

                    if (currentListIndex == listIndex)
                    {
                        UnSelectAll();
                        segment.Selected = true;
                        _SelectedSegmentClass = segmentGroup.Key;
                        _SelectSegmentIndex = i;
                        _SelectROiIndex = -1;
                        RaiseAnnotationSelectionChanged();
                        Canvas?.RefreshGL();
                        return true;
                    }

                    currentListIndex++;
                }
            }

            return false;
        }

        public void SetMeasurementOverlay(Point lineStart, Point lineEnd, Point? distanceStart = null, Point? distanceEnd = null, float pixelPermm = 1F)
        {
            _measureLineStart = lineStart;
            _measureLineEnd = lineEnd;
            _measureDistanceStart = distanceStart;
            _measureDistanceEnd = distanceEnd;
            _measurePixelPermm = pixelPermm;

            if (Canvas != null)
            {
                Canvas.PixelPermm = pixelPermm;
                Canvas.RefreshGL();
            }
        }

        public void ClearMeasurementOverlay()
        {
            _measureLineStart = null;
            _measureLineEnd = null;
            _measureDistanceStart = null;
            _measureDistanceEnd = null;
            Canvas?.RefreshGL();
        }

        public void SetDetectionOverlays(IEnumerable<DetectionOverlayItem> overlays)
        {
            _detectionOverlays.Clear();
            if (overlays != null)
            {
                _detectionOverlays.AddRange(overlays);
            }

            Canvas?.RefreshGL();
        }

        public IReadOnlyList<DetectionOverlayItem> GetDetectionOverlays()
        {
            return new List<DetectionOverlayItem>(_detectionOverlays);
        }

        public void ClearDetectionOverlays()
        {
            _detectionOverlays.Clear();
            Canvas?.RefreshGL();
        }

        public void SetModeMultiRoi()
        {
            if (_OnlyDragMode)
            {
                SetModeDrag();
                return;
            }

            ClearTemporaryDrawingState(clearEditSnapshot: true);
            _Mode = LabelingRoiMode.Rectangle;
            Canvas?.SetViewMode(CanvasInteractionMode.None);
        }

        public void SetModeSegmentation()
        {
            if (_OnlyDragMode)
            {
                SetModeDrag();
                return;
            }

            ClearTemporaryDrawingState(clearEditSnapshot: true);
            _Mode = LabelingRoiMode.Segmentation;
            Canvas?.SetViewMode(CanvasInteractionMode.None);
        }

        public void SetModeSegmentationBrush()
        {
            if (_OnlyDragMode)
            {
                SetModeDrag();
                return;
            }

            ClearTemporaryDrawingState(clearEditSnapshot: true);
            _Mode = LabelingRoiMode.SegmentationBrush;
            Canvas?.SetViewMode(CanvasInteractionMode.None);
        }

        public void SetModeSegmentationEraser()
        {
            if (_OnlyDragMode)
            {
                SetModeDrag();
                return;
            }

            ClearTemporaryDrawingState(clearEditSnapshot: true);
            _Mode = LabelingRoiMode.SegmentationEraser;
            Canvas?.SetViewMode(CanvasInteractionMode.None);
        }

        public void SetModeDrag()
        {
            ClearTemporaryDrawingState(clearEditSnapshot: true);
            _Mode = LabelingRoiMode.Drag;
            Canvas?.SetViewMode(CanvasInteractionMode.None);
        }

        internal static string GetUiModeDisplayText(LabelingRoiMode mode)
        {
            return mode switch
            {
                LabelingRoiMode.Rectangle => "\uBAA8\uB4DC ROI",
                LabelingRoiMode.Segmentation => "\uBAA8\uB4DC \uD3F4\uB9AC\uACE4",
                LabelingRoiMode.SegmentationBrush => "\uBAA8\uB4DC \uBE0C\uB7EC\uC2DC",
                LabelingRoiMode.SegmentationEraser => "\uBAA8\uB4DC \uC9C0\uC6B0\uAC1C",
                _ => "\uBAA8\uB4DC \uC774\uB3D9"
            };
        }

        private void OnCanvasMouseDown(object sender, CanvasMouseEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            if (e.Button == MouseButtons.Left && (_Mode == LabelingRoiMode.Drag || _OnlyDragMode))
            {
                Canvas.PreMousePos = Canvas.GetCurrentRobotPos(e.X, e.Y);
                Canvas.SetViewMode(CanvasInteractionMode.Drag);
                return;
            }

            SettingParameter();
            UnSelectAll();
            _StartPt = PointToImage(e.Location);
            _LastPoint = _StartPt;
            _MouseDown = _StartPt;

            if (e.Button == MouseButtons.Right)
            {
                switch (_Mode)
                {
                    case LabelingRoiMode.Rectangle:
                        SelectRoiAt(_StartPt);
                        break;
                    case LabelingRoiMode.Segmentation:
                        SelectSegmentAt(_StartPt);
                        break;
                }

                Canvas?.RefreshGL();
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            switch (_Mode)
            {
                case LabelingRoiMode.Rectangle:
                    currentPoints.Clear();
                    isDrawing = false;
                    SelectRoiAt(_StartPt);
                    if (_MouseOperation != PosSizableRect.None)
                    {
                        annotationEditSnapshot = CaptureAnnotationSnapshot();
                    }
                    break;
                case LabelingRoiMode.Segmentation:
                    TempROI = Rectangle.Empty;
                    isDrawing = true;
                    currentPoints.Clear();
                    if (!_StartPt.IsEmpty)
                    {
                        currentPoints.Add(_StartPt);
                    }
                    break;
                case LabelingRoiMode.SegmentationBrush:
                    TempROI = Rectangle.Empty;
                    isDrawing = true;
                    segmentationEditDirty = false;
                    annotationEditSnapshot = CaptureAnnotationSnapshot();
                    currentPoints.Clear();
                    if (!_StartPt.IsEmpty)
                    {
                        currentPoints.Add(_StartPt);
                        segmentationEditDirty |= PaintSegmentationBrushAt(_StartPt);
                        RefreshCanvasInteractive(force: true);
                    }
                    break;
                case LabelingRoiMode.SegmentationEraser:
                    TempROI = Rectangle.Empty;
                    isDrawing = true;
                    segmentationEditDirty = false;
                    annotationEditSnapshot = CaptureAnnotationSnapshot();
                    currentPoints.Clear();
                    if (!_StartPt.IsEmpty)
                    {
                        currentPoints.Add(_StartPt);
                        segmentationEditDirty |= EraseSegmentationBrushAt(_StartPt);
                        RefreshCanvasInteractive(force: true);
                    }
                    break;
            }
        }

        private void OnCanvasMouseMove(object sender, CanvasMouseEventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            _Position = PointToImage(e.Location);
            GetPixelData(_Position);
            UpdateCanvasCursor();

            bool isPointerDown = e.Button == MouseButtons.Left;
            bool interactiveRefresh = isPointerDown
                && (_Mode == LabelingRoiMode.SegmentationBrush || _Mode == LabelingRoiMode.SegmentationEraser);
            if (e.Button == MouseButtons.Left)
            {
                int distanceX = _Position.X - _LastPoint.X;
                int distanceY = _Position.Y - _LastPoint.Y;
                _LastPoint = _Position;

                switch (_Mode)
                {
                    case LabelingRoiMode.Rectangle:
                        UpdateRectangleDrawing(e, distanceX, distanceY);
                        break;
                    case LabelingRoiMode.Segmentation:
                        if (isDrawing && !_Position.IsEmpty)
                        {
                            currentPoints.Add(_Position);
                        }
                        break;
                    case LabelingRoiMode.SegmentationBrush:
                        segmentationEditDirty |= ApplyInteractiveBrushStroke(_Position, paint: true);
                        break;
                    case LabelingRoiMode.SegmentationEraser:
                        segmentationEditDirty |= ApplyInteractiveBrushStroke(_Position, paint: false);
                        break;
                }
            }

            if (!isPointerDown)
            {
                // Legacy CViewer hover updates status/cursor only. Repainting here makes
                // texture MouseMove feel slow even though no annotation geometry changed.
                return;
            }

            if (interactiveRefresh)
            {
                RefreshCanvasInteractive();
                return;
            }

            Canvas.RefreshGL();
        }

        private void OnCanvasMouseUp(object sender, CanvasMouseEventArgs e)
        {
            try
            {
                if (_Mode == LabelingRoiMode.Drag || _OnlyDragMode)
                {
                    Canvas?.SetViewMode(CanvasInteractionMode.None);
                }

                switch (e.Button)
                {
                    case MouseButtons.Left:
                        CompleteLeftMouseOperation();
                        break;
                    case MouseButtons.Right:
                        _ExcuteCount = 1;
                        Open_DropdownMenu(_MouseOperation == PosSizableRect.SizeAll ? roiContextMenu : imageContextMenu, e);
                        break;
                }

                _MouseOperation = PosSizableRect.None;
                Canvas?.RefreshGL();
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name} Exception ==> {ex.Message}");
            }
        }

        private void OnCanvasMouseWheel(object sender, CanvasMouseEventArgs e)
        {
            Canvas?.ZoomAt(e.Location, e.Delta);
        }

        private void OnCanvasMouseDoubleClicked(object sender, EventArgs e)
        {
            ZoomToFit();
        }

        private void OnCanvasKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                case Keys.V:
                case Keys.Space:
                    SetModeDrag();
                    e.Handled = true;
                    break;
                case Keys.D2:
                case Keys.R:
                    SetModeMultiRoi();
                    e.Handled = true;
                    break;
                case Keys.D3:
                case Keys.S:
                    SetModeSegmentation();
                    e.Handled = true;
                    break;
                case Keys.B:
                    SetModeSegmentationBrush();
                    e.Handled = true;
                    break;
                case Keys.E:
                    SetModeSegmentationEraser();
                    e.Handled = true;
                    break;
                case Keys.Delete:
                    ClearROI();
                    e.Handled = true;
                    break;
            }
        }

        private void OnCanvasDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnCanvasDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                string extension = Path.GetExtension(file);
                switch (extension.ToLowerInvariant())
                {
                    case ".bmp":
                    case ".exif":
                    case ".gif":
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".tif":
                    case ".tiff":
                        using (Bitmap image = AppImageLoader.LoadBitmap(file))
                        using (Bitmap mainImage = new Bitmap(image))
                        {
                            LoadMainImage(mainImage, Path.GetFileNameWithoutExtension(file), file);
                        }
                        break;
                    default:
                        throw new NotSupportedException("Unknown file extension " + extension);
                }
            }
        }

        private void SelectRoiAt(Point imagePoint)
        {
            _MouseOperation = PosSizableRect.None;
            int handleSize = GetImageHandleSize();
            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in _RoiDic.Reverse())
            {
                List<CRectangleObject> rois = roiGroup.Value;
                if (rois == null || rois.Count == 0)
                {
                    continue;
                }

                if (RoiInteractionController.TrySelect(rois, imagePoint, handleSize, out int selectedIndex, out PosSizableRect operation))
                {
                    UnSelectAll();
                    rois[selectedIndex].Selected = true;
                    _MouseOperation = operation;
                    _SelectROiIndex = selectedIndex;
                    _SelectedClass = roiGroup.Key;
                    _TempOb.cClassItem = rois[selectedIndex]?.cClassItem ?? _TempOb.cClassItem;
                    RaiseAnnotationSelectionChanged();
                    return;
                }
            }

            if (SelectedAnnotationListIndex > 0)
            {
                UnSelectAll();
                _SelectROiIndex = -1;
                _SelectedClass = string.Empty;
                _MouseOperation = PosSizableRect.None;
                RaiseAnnotationSelectionChanged();
            }
        }

        private void UpdateRectangleDrawing(MouseEventArgs e, int distanceX, int distanceY)
        {
            if (_MouseOperation == PosSizableRect.None)
            {
                SetToRectangle(e, ref _TempOb.Roi);
                return;
            }

            if (!_RoiDic.TryGetValue(_SelectedClass, out List<CRectangleObject> rois))
            {
                return;
            }

            if (_SelectROiIndex < 0 || _SelectROiIndex >= rois.Count)
            {
                return;
            }

            if (_MouseOperation == PosSizableRect.SizeAll)
            {
                rois[_SelectROiIndex].Move(distanceX, distanceY);
                return;
            }

            rois[_SelectROiIndex].MoveHandleTo(_Position, _MouseOperation);
        }

        private void CompleteLeftMouseOperation()
        {
            switch (_Mode)
            {
                case LabelingRoiMode.Rectangle:
                    bool modifiedExistingRoi = _MouseOperation != PosSizableRect.None;
                    if (!TempROI.IsEmpty && TempROI.Width > 15 && TempROI.Height > 15)
                    {
                        AnnotationSnapshot before = CaptureAnnotationSnapshot();
                        AddTempRectangle();
                        PushUndoSnapshot(before);
                        CommitCurrentAnnotations();
                    }
                    else if (modifiedExistingRoi)
                    {
                        PushUndoSnapshot(annotationEditSnapshot);
                        CommitCurrentAnnotations();
                    }
                    annotationEditSnapshot = null;
                    TempROI = Rectangle.Empty;
                    _StartPt = Point.Empty;
                    _EndPt = Point.Empty;
                    RaiseAnnotationSelectionChanged();
                    break;
                case LabelingRoiMode.Segmentation:
                    isDrawing = false;
                    if (!_Position.IsEmpty && currentPoints.Count > 2)
                    {
                        AddCurrentSegmentationPolygon();
                        CommitCurrentAnnotations();
                    }
                    currentPoints.Clear();
                    TempROI = Rectangle.Empty;
                    RaiseAnnotationSelectionChanged();
                    break;
                case LabelingRoiMode.SegmentationBrush:
                    isDrawing = false;
                    if (segmentationEditDirty)
                    {
                        PushUndoSnapshot(annotationEditSnapshot);
                        CommitCurrentAnnotations();
                    }
                    segmentationEditDirty = false;
                    annotationEditSnapshot = null;
                    currentPoints.Clear();
                    TempROI = Rectangle.Empty;
                    RaiseAnnotationSelectionChanged();
                    break;
                case LabelingRoiMode.SegmentationEraser:
                    isDrawing = false;
                    if (segmentationEditDirty)
                    {
                        PushUndoSnapshot(annotationEditSnapshot);
                        CommitCurrentAnnotations();
                    }
                    segmentationEditDirty = false;
                    annotationEditSnapshot = null;
                    currentPoints.Clear();
                    TempROI = Rectangle.Empty;
                    RaiseAnnotationSelectionChanged();
                    break;
            }
        }

        private void RefreshCanvasInteractive(bool force = false)
        {
            if (Canvas == null)
            {
                return;
            }

            if (!force && interactiveRefreshStopwatch.ElapsedMilliseconds < InteractiveRefreshIntervalMilliseconds)
            {
                return;
            }

            interactiveRefreshStopwatch.Restart();
            Canvas.RefreshGL();
        }

        private void AddCurrentSegmentationPolygon()
        {
            AddSegmentationPolygon(currentPoints, _TempOb.cClassItem, refresh: false);
        }

        private bool ShouldAppendBrushPoint(Point point)
        {
            if (point.IsEmpty)
            {
                return false;
            }

            if (currentPoints.Count == 0)
            {
                return true;
            }

            Point lastPoint = currentPoints[^1];
            int minDistance = Math.Max(2, SegmentationBrushRadius / 2);
            int dx = point.X - lastPoint.X;
            int dy = point.Y - lastPoint.Y;
            return (dx * dx) + (dy * dy) >= minDistance * minDistance;
        }

        private bool ApplyInteractiveBrushStroke(Point point, bool paint)
        {
            if (!isDrawing || point.IsEmpty)
            {
                return false;
            }

            if (currentPoints.Count == 0)
            {
                currentPoints.Add(point);
                return paint ? PaintSegmentationBrushAt(point) : EraseSegmentationBrushAt(point);
            }

            if (!ShouldAppendBrushPoint(point))
            {
                return false;
            }

            List<Point> strokePoints = BuildInterpolatedBrushPoints(currentPoints[^1], point);
            if (strokePoints.Count == 0)
            {
                return false;
            }

            currentPoints.AddRange(strokePoints);
            return paint ? PaintSegmentationBrushStroke(strokePoints) : EraseSegmentationBrushStroke(strokePoints);
        }

        private List<Point> BuildInterpolatedBrushPoints(Point from, Point to)
        {
            var points = new List<Point>();
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= 0)
            {
                return points;
            }

            int step = Math.Max(1, SegmentationBrushRadius / 2);
            int count = Math.Max(1, (int)Math.Ceiling(distance / step));
            Point last = from;
            for (int i = 1; i <= count; i++)
            {
                double ratio = i / (double)count;
                var point = new Point(
                    (int)Math.Round(from.X + (dx * ratio)),
                    (int)Math.Round(from.Y + (dy * ratio)));
                if (point.IsEmpty || point == last)
                {
                    continue;
                }

                points.Add(point);
                last = point;
            }

            return points;
        }

        private bool PaintSegmentationBrushAt(Point center)
        {
            return PaintSegmentationBrushStroke(new[] { center });
        }

        private bool PaintSegmentationBrushStroke(IReadOnlyCollection<Point> centers)
        {
            if (_currentImage == null || centers == null || centers.Count == 0)
            {
                return false;
            }

            Yolo.CClassItem targetClass = ResolveSegmentationClass(_TempOb.cClassItem);
            string className = targetClass.Text;
            LabelingSegmentationObject rasterSegment = GetOrCreateRasterSegment(className, targetClass);
            if (!ApplyBrushToRasterSegment(rasterSegment, centers, SegmentationBrushRadius, paint: true))
            {
                return false;
            }

            SelectRasterSegment(className, rasterSegment);
            return true;
        }

        private bool EraseSegmentationBrushAt(Point center)
        {
            return EraseSegmentationBrushStroke(new[] { center });
        }

        private bool EraseSegmentationBrushStroke(IReadOnlyCollection<Point> centers)
        {
            return RemoveSegmentsByStroke(centers, SegmentationBrushRadius) > 0;
        }

        private int AddBrushStroke(IEnumerable<Point> centers, int radius, Yolo.CClassItem classItem)
        {
            if (_currentImage == null)
            {
                return 0;
            }

            List<Point> strokeCenters = centers?
                .Where(point => !point.IsEmpty)
                .Distinct()
                .ToList() ?? new List<Point>();
            if (strokeCenters.Count == 0)
            {
                return 0;
            }

            Yolo.CClassItem targetClass = ResolveSegmentationClass(classItem);
            string className = targetClass.Text;
            LabelingSegmentationObject rasterSegment = GetOrCreateRasterSegment(className, targetClass);
            if (!ApplyBrushToRasterSegment(rasterSegment, strokeCenters, Math.Max(2, radius), paint: true))
            {
                return 0;
            }

            SelectRasterSegment(className, rasterSegment);

            return 1;
        }

        private int RemoveSegmentsNear(Point center, int radius)
        {
            return RemoveSegmentsByStroke(new[] { center }, radius);
        }

        private int RemoveSegmentsByStroke(IEnumerable<Point> centers, int radius)
        {
            if (_currentImage == null || _SegmentationDic.Count == 0)
            {
                return 0;
            }

            List<Point> strokeCenters = centers?
                .Where(point => !point.IsEmpty)
                .Distinct()
                .ToList() ?? new List<Point>();
            if (strokeCenters.Count == 0)
            {
                return 0;
            }

            int changed = 0;
            int safeRadius = Math.Max(2, radius);
            foreach (string className in _SegmentationDic.Keys.ToList())
            {
                List<LabelingSegmentationObject> segments = _SegmentationDic[className];
                if (segments == null || segments.Count == 0)
                {
                    continue;
                }

                Yolo.CClassItem classItem = segments.FirstOrDefault(segment => segment?.ClassItem != null)?.ClassItem
                    ?? new Yolo.CClassItem { Text = className, DrawColor = Color.LimeGreen };
                LabelingSegmentationObject rasterSegment = GetOrCreateRasterSegment(className, classItem);
                Rectangle strokeBounds = GetStrokeBounds(strokeCenters, safeRadius, rasterSegment.MaskSize);
                if (!rasterSegment.Bounds.IntersectsWith(strokeBounds))
                {
                    continue;
                }

                if (!ApplyBrushToRasterSegment(rasterSegment, strokeCenters, safeRadius, paint: false))
                {
                    continue;
                }

                changed++;
                if (rasterSegment.MaskBounds.IsEmpty)
                {
                    _SegmentationDic.Remove(className);
                }
            }

            if (changed > 0)
            {
                _SelectedSegmentClass = string.Empty;
                _SelectSegmentIndex = -1;
                ClearSegmentSelection();
            }

            return changed;
        }

        private bool ApplyBrushToRasterSegment(
            LabelingSegmentationObject rasterSegment,
            IReadOnlyCollection<Point> centers,
            int radius,
            bool paint)
        {
            if (rasterSegment?.IsRasterMask != true)
            {
                return false;
            }

            if (!ApplyBrushToMask(rasterSegment.MaskData, rasterSegment.MaskSize, centers, Math.Max(2, radius), paint, out Rectangle changedBounds))
            {
                return false;
            }

            if (paint)
            {
                rasterSegment.MaskBounds = rasterSegment.MaskBounds.IsEmpty
                    ? changedBounds
                    : Rectangle.Union(rasterSegment.MaskBounds, changedBounds);
                MarkRasterSegmentRenderDirty(rasterSegment, changedBounds);
                return true;
            }

            rasterSegment.MaskBounds = ShouldRecalculateMaskBoundsAfterErase(rasterSegment.MaskBounds, changedBounds)
                ? SegmentationGeometry.GetMaskBounds(rasterSegment.MaskData, rasterSegment.MaskSize)
                : rasterSegment.MaskBounds;
            MarkRasterSegmentRenderDirty(rasterSegment, changedBounds);
            return true;
        }

        private static void MarkRasterSegmentRenderDirty(LabelingSegmentationObject rasterSegment, Rectangle changedBounds)
        {
            if (rasterSegment == null || changedBounds.IsEmpty)
            {
                return;
            }

            rasterSegment.RenderDirtyBounds = rasterSegment.RenderDirtyBounds.IsEmpty
                ? changedBounds
                : Rectangle.Union(rasterSegment.RenderDirtyBounds, changedBounds);
            rasterSegment.RenderVersion++;
        }

        private void SelectRasterSegment(string className, LabelingSegmentationObject rasterSegment)
        {
            if (string.IsNullOrWhiteSpace(className)
                || rasterSegment == null
                || !_SegmentationDic.TryGetValue(className, out List<LabelingSegmentationObject> segments))
            {
                return;
            }

            int index = segments.IndexOf(rasterSegment);
            if (index < 0)
            {
                return;
            }

            if (_SelectedSegmentClass == className && _SelectSegmentIndex == index && rasterSegment.Selected)
            {
                return;
            }

            ClearSegmentSelection();
            rasterSegment.Selected = true;
            _SelectedSegmentClass = className;
            _SelectSegmentIndex = index;
        }

        private LabelingSegmentationObject GetOrCreateRasterSegment(string className, Yolo.CClassItem classItem)
        {
            if (!_SegmentationDic.TryGetValue(className, out List<LabelingSegmentationObject> segments))
            {
                segments = new List<LabelingSegmentationObject>();
                _SegmentationDic[className] = segments;
            }

            if (segments.Count == 1 && segments[0]?.IsRasterMask == true)
            {
                segments[0].ClassName = className;
                segments[0].ClassItem = classItem;
                return segments[0];
            }

            byte[] mask = segments.Count == 0
                ? new byte[Math.Max(0, _currentImage.Width * _currentImage.Height)]
                : RasterizeSegmentsToMask(segments, _currentImage.Size);
            var rasterSegment = new LabelingSegmentationObject(Array.Empty<Point>(), classItem)
            {
                ClassName = className,
                ClassItem = classItem,
                MaskData = mask,
                MaskSize = _currentImage.Size,
                MaskBounds = SegmentationGeometry.GetMaskBounds(mask, _currentImage.Size),
                RenderVersion = 1,
                RenderDirtyBounds = new Rectangle(Point.Empty, _currentImage.Size)
            };

            segments.Clear();
            segments.Add(rasterSegment);
            return rasterSegment;
        }

        private static bool ApplyBrushToMask(byte[] mask, Size maskSize, IEnumerable<Point> centers, int radius, bool paint)
        {
            return ApplyBrushToMask(mask, maskSize, centers, radius, paint, out _);
        }

        private static bool ApplyBrushToMask(byte[] mask, Size maskSize, IEnumerable<Point> centers, int radius, bool paint, out Rectangle changedBounds)
        {
            if (mask == null || maskSize.Width <= 0 || maskSize.Height <= 0 || mask.Length != maskSize.Width * maskSize.Height)
            {
                changedBounds = Rectangle.Empty;
                return false;
            }

            int safeRadius = Math.Max(1, radius);
            double radiusSquared = safeRadius * safeRadius;
            byte target = paint ? (byte)1 : (byte)0;
            bool changed = false;
            int changedLeft = int.MaxValue;
            int changedTop = int.MaxValue;
            int changedRight = int.MinValue;
            int changedBottom = int.MinValue;
            foreach (Point center in centers ?? Enumerable.Empty<Point>())
            {
                int left = Math.Max(0, center.X - safeRadius - 1);
                int top = Math.Max(0, center.Y - safeRadius - 1);
                int right = Math.Min(maskSize.Width - 1, center.X + safeRadius + 1);
                int bottom = Math.Min(maskSize.Height - 1, center.Y + safeRadius + 1);
                for (int y = top; y <= bottom; y++)
                {
                    int rowOffset = y * maskSize.Width;
                    for (int x = left; x <= right; x++)
                    {
                        if (!DoesPixelCellIntersectCircle(x, y, center, radiusSquared))
                        {
                            continue;
                        }

                        int index = rowOffset + x;
                        if (mask[index] == target)
                        {
                            continue;
                        }

                        mask[index] = target;
                        changed = true;
                        if (x < changedLeft)
                        {
                            changedLeft = x;
                        }

                        if (y < changedTop)
                        {
                            changedTop = y;
                        }

                        if (x > changedRight)
                        {
                            changedRight = x;
                        }

                        if (y > changedBottom)
                        {
                            changedBottom = y;
                        }
                    }
                }
            }

            changedBounds = changed
                ? Rectangle.FromLTRB(changedLeft, changedTop, changedRight + 1, changedBottom + 1)
                : Rectangle.Empty;
            return changed;
        }

        private static bool DoesPixelCellIntersectCircle(int x, int y, Point center, double radiusSquared)
        {
            double nearestX = Math.Clamp(center.X, x - 0.5D, x + 0.5D);
            double nearestY = Math.Clamp(center.Y, y - 0.5D, y + 0.5D);
            double dx = nearestX - center.X;
            double dy = nearestY - center.Y;
            return (dx * dx) + (dy * dy) <= radiusSquared;
        }

        private static bool ShouldRecalculateMaskBoundsAfterErase(Rectangle currentBounds, Rectangle changedBounds)
        {
            if (currentBounds.IsEmpty || changedBounds.IsEmpty)
            {
                return true;
            }

            return changedBounds.Left <= currentBounds.Left
                || changedBounds.Top <= currentBounds.Top
                || changedBounds.Right >= currentBounds.Right
                || changedBounds.Bottom >= currentBounds.Bottom;
        }

        private static Rectangle GetStrokeBounds(IReadOnlyCollection<Point> centers, int radius, Size imageSize)
        {
            if (centers == null || centers.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int left = Math.Max(0, centers.Min(point => point.X) - radius - 1);
            int top = Math.Max(0, centers.Min(point => point.Y) - radius - 1);
            int right = Math.Min(imageSize.Width, centers.Max(point => point.X) + radius + 2);
            int bottom = Math.Min(imageSize.Height, centers.Max(point => point.Y) + radius + 2);
            return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
        }

        private static bool HasMaskPixels(byte[] mask)
        {
            return mask != null && mask.Any(value => value != 0);
        }

        private static byte[] RasterizeSegmentsToMask(IEnumerable<LabelingSegmentationObject> segments, Size imageSize)
        {
            byte[] mask = new byte[Math.Max(0, imageSize.Width * imageSize.Height)];
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return mask;
            }

            using var bitmap = new Bitmap(imageSize.Width, imageSize.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
                using var fillBrush = new SolidBrush(Color.White);
                using var eraseBrush = new SolidBrush(Color.Black);
                foreach (LabelingSegmentationObject segment in segments ?? Enumerable.Empty<LabelingSegmentationObject>())
                {
                    if (segment == null)
                    {
                        continue;
                    }

                    if (segment.IsRasterMask)
                    {
                        for (int y = 0; y < Math.Min(imageSize.Height, segment.MaskSize.Height); y++)
                        {
                            int sourceOffset = y * segment.MaskSize.Width;
                            for (int x = 0; x < Math.Min(imageSize.Width, segment.MaskSize.Width); x++)
                            {
                                if (segment.MaskData[sourceOffset + x] != 0)
                                {
                                    bitmap.SetPixel(x, y, Color.White);
                                }
                            }
                        }

                        continue;
                    }

                    if (segment.Points?.Count >= 3)
                    {
                        graphics.FillPolygon(fillBrush, segment.Points.ToArray());
                    }

                    foreach (List<Point> cutout in segment.CutoutPolygons ?? new List<List<Point>>())
                    {
                        if (cutout?.Count >= 3)
                        {
                            graphics.FillPolygon(eraseBrush, cutout.ToArray());
                        }
                    }
                }
            }

            for (int y = 0; y < imageSize.Height; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = 0; x < imageSize.Width; x++)
                {
                    mask[rowOffset + x] = bitmap.GetPixel(x, y).R > 0 ? (byte)1 : (byte)0;
                }
            }

            return mask;
        }

        private LabelingSegmentationObject CreateSegmentObject(
            IEnumerable<Point> points,
            Yolo.CClassItem classItem,
            string className,
            bool selected,
            IEnumerable<IEnumerable<Point>> cutouts = null)
        {
            var segment = new LabelingSegmentationObject(points, classItem)
            {
                ClassName = string.IsNullOrWhiteSpace(className) ? classItem?.Text ?? string.Empty : className,
                Selected = selected
            };

            if (_currentImage != null)
            {
                foreach (IEnumerable<Point> cutout in cutouts ?? Enumerable.Empty<IEnumerable<Point>>())
                {
                    List<Point> normalized = SegmentationGeometry.NormalizePolygon(
                        cutout,
                        _currentImage.Size,
                        minimumDistance: 1,
                        simplificationTolerance: 0.75D);
                    if (normalized.Count >= 3)
                    {
                        segment.CutoutPolygons.Add(normalized);
                    }
                }
            }

            return segment;
        }

        private string ResolveMergeClassName(string className)
        {
            if (!string.IsNullOrWhiteSpace(className))
            {
                return className;
            }

            if (!string.IsNullOrWhiteSpace(_SelectedSegmentClass))
            {
                return _SelectedSegmentClass;
            }

            if (!string.IsNullOrWhiteSpace(_SelectedClass) && _SegmentationDic.ContainsKey(_SelectedClass))
            {
                return _SelectedClass;
            }

            if (!string.IsNullOrWhiteSpace(_TempOb.cClassItem?.Text) && _SegmentationDic.ContainsKey(_TempOb.cClassItem.Text))
            {
                return _TempOb.cClassItem.Text;
            }

            return _SegmentationDic
                .Where(group => group.Value != null && group.Value.Count > 0)
                .Select(group => group.Key)
                .FirstOrDefault() ?? string.Empty;
        }

        private void AddTempRectangle()
        {
            Yolo.CClassItem targetClass = ResolveRoiClass(_TempOb.cClassItem);
            UnSelectAll();
            CRectangleObject rectangleObject = new CRectangleObject
            {
                Roi = TempROI,
                cClassItem = targetClass,
                Selected = true
            };

            string className = targetClass.Text;
            if (!_RoiDic.TryGetValue(className, out List<CRectangleObject> list))
            {
                list = new List<CRectangleObject>();
                _RoiDic.Add(className, list);
            }

            list.Add(rectangleObject);
            _SelectROiIndex = list.Count - 1;
        }

        private void SetToRectangle(MouseEventArgs e, ref Rectangle roi)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _EndPt = PointToImage(e.Location);
            Rectangle bounds = Rectangle.FromLTRB(_MinX, _MinY, _MaxX, _MaxY);
            roi = RoiGeometry.CreateBoundedRectangle(_StartPt, _EndPt, bounds);
        }

        private bool ClearROI()
        {
            AnnotationSnapshot before = CaptureAnnotationSnapshot();
            bool removed = RemoveSelectedRoi() || RemoveSelectedSegmentation();

            if (removed)
            {
                PushUndoSnapshot(before);
                CommitCurrentAnnotations();
                RaiseAnnotationSelectionChanged();
            }

            _MouseDown = Point.Empty;
            _StartPt = Point.Empty;
            _EndPt = Point.Empty;
            Canvas?.RefreshGL();
            return removed;
        }

        private bool RemoveSelectedRoi()
        {
            foreach (string className in _RoiDic.Keys.ToList())
            {
                List<CRectangleObject> rois = _RoiDic[className];
                int selectedIndex = rois.FindIndex(roi => roi?.Selected == true);
                if (selectedIndex < 0)
                {
                    continue;
                }

                rois.RemoveAt(selectedIndex);
                if (rois.Count == 0)
                {
                    _RoiDic.Remove(className);
                }

                _SelectROiIndex = -1;
                _SelectedClass = string.Empty;
                _MouseOperation = PosSizableRect.None;
                UnSelectAll();
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_SelectedClass)
                && _RoiDic.TryGetValue(_SelectedClass, out List<CRectangleObject> selectedClassRois)
                && _SelectROiIndex >= 0
                && _SelectROiIndex < selectedClassRois.Count)
            {
                selectedClassRois.RemoveAt(_SelectROiIndex);
                if (selectedClassRois.Count == 0)
                {
                    _RoiDic.Remove(_SelectedClass);
                }

                _SelectROiIndex = -1;
                _SelectedClass = string.Empty;
                _MouseOperation = PosSizableRect.None;
                UnSelectAll();
                return true;
            }

            return false;
        }

        private bool RemoveLastSegmentation(string className)
        {
            string targetClassName = string.IsNullOrWhiteSpace(className) ? _SelectedSegmentClass : className;
            if (string.IsNullOrWhiteSpace(targetClassName))
            {
                targetClassName = _SegmentationDic
                    .Where(group => group.Value != null && group.Value.Count > 0)
                    .Select(group => group.Key)
                    .LastOrDefault() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(targetClassName) || !_SegmentationDic.TryGetValue(targetClassName, out List<LabelingSegmentationObject> segments))
            {
                return false;
            }

            if (segments.Count == 0)
            {
                return false;
            }

            segments.RemoveAt(segments.Count - 1);
            _SelectedSegmentClass = string.Empty;
            _SelectSegmentIndex = -1;
            return true;
        }

        private bool RemoveSelectedSegmentation()
        {
            if (string.IsNullOrWhiteSpace(_SelectedSegmentClass)
                || !_SegmentationDic.TryGetValue(_SelectedSegmentClass, out List<LabelingSegmentationObject> segments)
                || _SelectSegmentIndex < 0
                || _SelectSegmentIndex >= segments.Count)
            {
                return false;
            }

            segments.RemoveAt(_SelectSegmentIndex);
            _SelectedSegmentClass = string.Empty;
            _SelectSegmentIndex = -1;
            UnSelectAll();
            return true;
        }

        private bool SelectSegmentAt(Point imagePoint)
        {
            bool hadSelection = SelectedAnnotationListIndex > 0;
            UnSelectAll();
            _MouseOperation = PosSizableRect.None;
            _SelectedSegmentClass = string.Empty;
            _SelectSegmentIndex = -1;

            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> group in _SegmentationDic.Reverse())
            {
                List<LabelingSegmentationObject> segments = group.Value;
                if (segments == null)
                {
                    continue;
                }

                for (int i = segments.Count - 1; i >= 0; i--)
                {
                    LabelingSegmentationObject segment = segments[i];
                    if (!IsSelectableSegment(segment))
                    {
                        continue;
                    }

                    if (!ContainsSegmentPoint(segment, imagePoint))
                    {
                        continue;
                    }

                    segment.Selected = true;
                    _SelectedSegmentClass = group.Key;
                    _SelectSegmentIndex = i;
                    _MouseOperation = PosSizableRect.SizeAll;
                    RaiseAnnotationSelectionChanged();
                    return true;
                }
            }

            if (hadSelection)
            {
                RaiseAnnotationSelectionChanged();
            }

            return false;
        }

        private int FindSelectedAnnotationListIndex()
        {
            int listIndex = 1;
            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in _RoiDic)
            {
                foreach (CRectangleObject roi in roiGroup.Value ?? new List<CRectangleObject>())
                {
                    if (roi == null || roi.Roi.IsEmpty)
                    {
                        continue;
                    }

                    if (roi.Selected)
                    {
                        return listIndex;
                    }

                    listIndex++;
                }
            }

            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> segmentGroup in _SegmentationDic)
            {
                foreach (LabelingSegmentationObject segment in segmentGroup.Value ?? new List<LabelingSegmentationObject>())
                {
                    if (!IsSelectableSegment(segment))
                    {
                        continue;
                    }

                    if (segment.Selected)
                    {
                        return listIndex;
                    }

                    listIndex++;
                }
            }

            return -1;
        }

        private void RaiseAnnotationSelectionChanged()
        {
            int selectedIndex = FindSelectedAnnotationListIndex();
            LabelingAnnotationKind? selectedKind = null;
            string className = string.Empty;

            if (selectedIndex > 0)
            {
                LabelingRoiListItem item = GetRoiListItems().FirstOrDefault(x => x.Index == selectedIndex);
                selectedKind = item?.Kind;
                className = item?.ClassName ?? string.Empty;
            }

            AnnotationSelectionChanged?.Invoke(
                this,
                new LabelingAnnotationSelectionChangedEventArgs(selectedIndex, selectedKind, className));
        }

        private AnnotationSnapshot CaptureAnnotationSnapshot()
        {
            return new AnnotationSnapshot
            {
                SelectedClass = _SelectedClass,
                SelectedSegmentClass = _SelectedSegmentClass,
                SelectedSegmentIndex = _SelectSegmentIndex,
                SelectedRoiIndex = _SelectROiIndex,
                Rois = _RoiDic.ToDictionary(
                    group => group.Key,
                    group => group.Value?
                        .Where(item => item != null)
                        .Select(item => new RoiSnapshot
                        {
                            Roi = item.Roi,
                            ClassName = item.cClassItem?.Text ?? group.Key,
                            DrawColor = item.cClassItem?.DrawColor ?? Color.LimeGreen,
                            Selected = item.Selected
                        })
                        .ToList() ?? new List<RoiSnapshot>()),
                Segments = _SegmentationDic.ToDictionary(
                    group => group.Key,
                    group => group.Value?
                        .Where(IsSelectableSegment)
                        .Select(item => new SegmentSnapshot
                        {
                            Points = item.Points != null ? new List<Point>(item.Points) : new List<Point>(),
                            Cutouts = item.CutoutPolygons?
                                .Where(cutout => cutout != null)
                                .Select(cutout => new List<Point>(cutout))
                                .ToList() ?? new List<List<Point>>(),
                            MaskData = item.MaskData != null ? (byte[])item.MaskData.Clone() : null,
                            MaskSize = item.MaskSize,
                            ClassName = item.ClassItem?.Text ?? item.ClassName ?? group.Key,
                            DrawColor = item.ClassItem?.DrawColor ?? item.Color,
                            Selected = item.Selected
                        })
                        .ToList() ?? new List<SegmentSnapshot>())
            };
        }

        private void RestoreAnnotationSnapshot(AnnotationSnapshot snapshot)
        {
            ClearRasterMaskTextureCache();
            _RoiDic = new Dictionary<string, List<CRectangleObject>>();
            _SegmentationDic = new Dictionary<string, List<LabelingSegmentationObject>>();
            _SelectedClass = snapshot?.SelectedClass ?? _SelectedClass;
            _SelectedSegmentClass = snapshot?.SelectedSegmentClass ?? string.Empty;
            _SelectSegmentIndex = snapshot?.SelectedSegmentIndex ?? -1;
            _SelectROiIndex = snapshot?.SelectedRoiIndex ?? 0;

            foreach (KeyValuePair<string, List<RoiSnapshot>> group in snapshot?.Rois ?? new Dictionary<string, List<RoiSnapshot>>())
            {
                var rois = new List<CRectangleObject>();
                foreach (RoiSnapshot item in group.Value ?? new List<RoiSnapshot>())
                {
                    var classItem = new Yolo.CClassItem
                    {
                        Text = string.IsNullOrWhiteSpace(item.ClassName) ? group.Key : item.ClassName,
                        DrawColor = item.DrawColor.IsEmpty ? Color.LimeGreen : item.DrawColor
                    };
                    rois.Add(new CRectangleObject
                    {
                        Roi = item.Roi,
                        cClassItem = classItem,
                        Selected = item.Selected
                    });
                }

                if (rois.Count > 0)
                {
                    _RoiDic[group.Key] = rois;
                }
            }

            foreach (KeyValuePair<string, List<SegmentSnapshot>> group in snapshot?.Segments ?? new Dictionary<string, List<SegmentSnapshot>>())
            {
                var segments = new List<LabelingSegmentationObject>();
                foreach (SegmentSnapshot item in group.Value ?? new List<SegmentSnapshot>())
                {
                    if ((item.Points == null || item.Points.Count < 3)
                        && (item.MaskData == null || item.MaskData.Length == 0))
                    {
                        continue;
                    }

                    var classItem = new Yolo.CClassItem
                    {
                        Text = string.IsNullOrWhiteSpace(item.ClassName) ? group.Key : item.ClassName,
                        DrawColor = item.DrawColor.IsEmpty ? Color.LimeGreen : item.DrawColor
                    };
                    LabelingSegmentationObject segment = item.MaskData != null && item.MaskData.Length > 0
                        ? new LabelingSegmentationObject(Array.Empty<Point>(), classItem)
                        {
                            ClassName = classItem.Text,
                            ClassItem = classItem,
                            Selected = item.Selected,
                            MaskData = (byte[])item.MaskData.Clone(),
                            MaskSize = item.MaskSize,
                            MaskBounds = SegmentationGeometry.GetMaskBounds(item.MaskData, item.MaskSize),
                            RenderVersion = 1,
                            RenderDirtyBounds = new Rectangle(Point.Empty, item.MaskSize)
                        }
                        : CreateSegmentObject(item.Points, classItem, classItem.Text, item.Selected, item.Cutouts);
                    segments.Add(segment);
                }

                if (segments.Count > 0)
                {
                    _SegmentationDic[group.Key] = segments;
                }
            }
        }

        private void PushUndoSnapshot(AnnotationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            undoSnapshots.Push(snapshot);
            while (undoSnapshots.Count > MaxUndoSnapshotCount)
            {
                TrimOldestUndoSnapshot();
            }

            redoSnapshots.Clear();
        }

        private void TrimOldestUndoSnapshot()
        {
            if (undoSnapshots.Count <= MaxUndoSnapshotCount)
            {
                return;
            }

            AnnotationSnapshot[] snapshots = undoSnapshots.ToArray();
            undoSnapshots.Clear();
            for (int i = snapshots.Length - 2; i >= 0; i--)
            {
                undoSnapshots.Push(snapshots[i]);
            }
        }

        private sealed class AnnotationSnapshot
        {
            public Dictionary<string, List<RoiSnapshot>> Rois { get; set; } = new Dictionary<string, List<RoiSnapshot>>();
            public Dictionary<string, List<SegmentSnapshot>> Segments { get; set; } = new Dictionary<string, List<SegmentSnapshot>>();
            public string SelectedClass { get; set; } = string.Empty;
            public string SelectedSegmentClass { get; set; } = string.Empty;
            public int SelectedRoiIndex { get; set; }
            public int SelectedSegmentIndex { get; set; }
        }

        private sealed class RoiSnapshot
        {
            public Rectangle Roi { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public Color DrawColor { get; set; } = Color.LimeGreen;
            public bool Selected { get; set; }
        }

        private sealed class SegmentSnapshot
        {
            public List<Point> Points { get; set; } = new List<Point>();
            public List<List<Point>> Cutouts { get; set; } = new List<List<Point>>();
            public byte[] MaskData { get; set; }
            public Size MaskSize { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public Color DrawColor { get; set; } = Color.LimeGreen;
            public bool Selected { get; set; }
        }

        private static bool IsSelectableSegment(LabelingSegmentationObject segment)
        {
            return segment != null && (!segment.Bounds.IsEmpty || segment.Points?.Count >= 3);
        }

        private static bool ContainsSegmentPoint(LabelingSegmentationObject segment, Point imagePoint)
        {
            if (segment == null || !segment.Bounds.Contains(imagePoint))
            {
                return false;
            }

            if (segment.IsRasterMask)
            {
                if (imagePoint.X < 0 || imagePoint.Y < 0 || imagePoint.X >= segment.MaskSize.Width || imagePoint.Y >= segment.MaskSize.Height)
                {
                    return false;
                }

                return segment.MaskData[(imagePoint.Y * segment.MaskSize.Width) + imagePoint.X] != 0;
            }

            return SegmentationGeometry.ContainsPoint(segment.Points, imagePoint);
        }

        private void UpdateCanvasCursor()
        {
            if (Canvas == null)
            {
                return;
            }

            if (_Mode == LabelingRoiMode.SegmentationBrush || _Mode == LabelingRoiMode.SegmentationEraser)
            {
                Canvas.Cursor = Cursors.Cross;
                return;
            }

            if (_Mode != LabelingRoiMode.Rectangle)
            {
                Canvas.Cursor = Cursors.Default;
                return;
            }

            CRectangleObject selected = GetSelectedRoiObject();
            if (selected == null)
            {
                Canvas.Cursor = Cursors.Default;
                return;
            }

            PosSizableRect handle = selected.GetNodeSelectable(_Position, GetImageHandleSize());
            Canvas.Cursor = ResolveRoiCursor(handle, selected.Angle);
        }

        private CRectangleObject GetSelectedRoiObject()
        {
            if (string.IsNullOrEmpty(_SelectedClass)
                || !_RoiDic.TryGetValue(_SelectedClass, out List<CRectangleObject> selectedList)
                || selectedList == null
                || _SelectROiIndex < 0
                || _SelectROiIndex >= selectedList.Count)
            {
                return null;
            }

            CRectangleObject selected = selectedList[_SelectROiIndex];
            return selected?.Selected == true ? selected : null;
        }

        private void UnSelectAll()
        {
            RoiInteractionController.ClearSelection(_RoiDic);
            ClearSegmentSelection();
        }

        private void ClearSegmentSelection()
        {
            foreach (List<LabelingSegmentationObject> segments in _SegmentationDic.Values)
            {
                if (segments == null)
                {
                    continue;
                }

                foreach (LabelingSegmentationObject segment in segments.Where(item => item != null))
                {
                    segment.Selected = false;
                }
            }
        }

        private void SettingParameter()
        {
            if (_currentImage == null)
            {
                return;
            }

            _MaxX = _currentImage.Width;
            _MaxY = _currentImage.Height;
            _TempOb._MaxX = _currentImage.Width;
            _TempOb._MaxY = _currentImage.Height;
            _TempOb.OriginalSize = _currentImage.Size;

            RoiInteractionController.ApplyImageBounds(_RoiDic, _currentImage.Size);
        }

        private void GetPixelData(Point position)
        {
            if (_currentImage == null || position.X <= 0 || position.Y <= 0 || position.X >= _currentImage.Width || position.Y >= _currentImage.Height)
            {
                return;
            }

            _Rgb = _currentImage.GetPixel(position.X, position.Y);
            _GrayValue = (_Rgb.R + _Rgb.G + _Rgb.B) / 3;
        }

        private Point GetCanvasCenter()
        {
            if (Canvas == null)
            {
                return Point.Empty;
            }

            return new Point(Math.Max(0, Canvas.Width / 2), Math.Max(0, Canvas.Height / 2));
        }

        private void CommitCurrentAnnotations()
        {
            CGlobal.Inst.LabelingWorkflow.CommitMainAnnotations(CGlobal.Inst.Data, CGlobal.Inst.System);
        }

        private void ImageMenuClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            try
            {
                if (_ExcuteCount != 1) { return; }
                imageContextMenu.Hide();
                roiContextMenu.Hide();
                switch (GetViewerMenuCommand(e.ClickedItem))
                {
                    case ViewerMenuCommand.ImageLoad:
                        string imagePath = PromptLoadImageFilePath();
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            using (Bitmap image = AppImageLoader.LoadBitmap(imagePath))
                            using (Bitmap mainImage = new Bitmap(image))
                            {
                                LoadMainImage(mainImage, Path.GetFileNameWithoutExtension(imagePath), imagePath);
                            }
                        }
                        break;
                    case ViewerMenuCommand.ImageSave:
                        if (_currentImage != null && _currentImage.Width != 10 && _currentImage.Height != 10)
                        {
                            imagePath = PromptSaveImageFilePath();
                            if (!string.IsNullOrEmpty(imagePath)) { _currentImage.Save(imagePath); }
                        }
                        break;
                    case ViewerMenuCommand.ShowFolder:
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = AppContext.BaseDirectory,
                            UseShellExecute = true
                        });
                        break;
                    case ViewerMenuCommand.ToggleCross:
                        _ViewCross = !_ViewCross;
                        Canvas?.RefreshGL();
                        break;
                    case ViewerMenuCommand.DeleteRoi:
                        ClearROI();
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name} Exception ==> {ex.Message}");
            }
            _ExcuteCount++;
        }

        private static string PromptLoadImageFilePath()
        {
            using OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "이미지 열기",
                Filter = "Image files (*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
        }

        private static string PromptSaveImageFilePath()
        {
            using SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "이미지 저장",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap Image (*.bmp)|*.bmp|TIFF Image (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*",
                DefaultExt = "png",
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
        }

        private void ConfigureReadableWorkbenchContextMenu()
        {
            ConfigureContextMenu(
                imageContextMenu,
                (ViewerMenuCommand.ImageLoad, "\uC774\uBBF8\uC9C0 \uC5F4\uAE30"),
                (ViewerMenuCommand.ImageSave, "\uC774\uBBF8\uC9C0 \uC800\uC7A5"),
                (ViewerMenuCommand.ShowFolder, "\uD3F4\uB354 \uC5F4\uAE30"),
                (ViewerMenuCommand.ToggleCross, "\uC2ED\uC790\uC120"));

            ConfigureContextMenu(
                roiContextMenu,
                (ViewerMenuCommand.DeleteRoi, "\uB77C\uBCA8 \uC0AD\uC81C"),
                (ViewerMenuCommand.RoiList, "\uB77C\uBCA8 \uBAA9\uB85D"));
        }

        private void ConfigureContextMenu(ContextMenuStrip menu, params (ViewerMenuCommand Command, string Text)[] items)
        {
            menu.Items.Clear();
            menu.BackColor = Color.White;
            menu.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point);
            foreach ((ViewerMenuCommand command, string text) in items)
            {
                menu.Items.Add(new ToolStripMenuItem(text) { Tag = command });
            }

            menu.ItemClicked -= ImageMenuClicked;
            menu.ItemClicked += ImageMenuClicked;
        }

        private static ViewerMenuCommand GetViewerMenuCommand(ToolStripItem item)
        {
            if (item?.Tag is ViewerMenuCommand command)
            {
                return command;
            }

            return item?.Text switch
            {
                "Image Load" or "이미지 열기" => ViewerMenuCommand.ImageLoad,
                "Image Save" or "이미지 저장" => ViewerMenuCommand.ImageSave,
                "Show Folder" or "폴더 열기" => ViewerMenuCommand.ShowFolder,
                "CROSS" or "십자선" => ViewerMenuCommand.ToggleCross,
                "Delete" or "라벨 삭제" => ViewerMenuCommand.DeleteRoi,
                "Roi List" or "라벨 목록" => ViewerMenuCommand.RoiList,
                _ => ViewerMenuCommand.None
            };
        }

        private void Open_DropdownMenu(ContextMenuStrip dropdownMenu, MouseEventArgs e)
        {
            if (Canvas == null)
            {
                return;
            }

            dropdownMenu.Show(Canvas, e.X, e.Y);
        }

        private static Cursor ResolveRoiCursor(PosSizableRect handle, double angle)
        {
            double cursorAngle = angle;

            switch (handle)
            {
                case PosSizableRect.Rotate:
                    return Cursors.NoMove2D;
                case PosSizableRect.SizeAll:
                    return Cursors.SizeAll;
                case PosSizableRect.None:
                    return Cursors.Default;
                case PosSizableRect.LeftUp:
                case PosSizableRect.RightBottom:
                    cursorAngle += 45;
                    break;
                case PosSizableRect.UpMiddle:
                case PosSizableRect.BottomMiddle:
                    cursorAngle += 90;
                    break;
                case PosSizableRect.LeftBottom:
                case PosSizableRect.RightUp:
                    cursorAngle += 135;
                    break;
            }

            if (cursorAngle > 360)
            {
                cursorAngle -= 360;
            }

            return cursorAngle switch
            {
                > 26 and < 68 or > 204 and < 248 => Cursors.SizeNWSE,
                > 69 and < 113 or > 249 and < 293 => Cursors.SizeNS,
                > 114 and < 158 or > 294 and < 338 => Cursors.SizeNESW,
                _ => Cursors.SizeWE
            };
        }

        private void DisposeViewerResources()
        {
            ClearRasterMaskTextureCache();
            DisposeCanvas();
            imageContextMenu.ItemClicked -= ImageMenuClicked;
            roiContextMenu.ItemClicked -= ImageMenuClicked;
            imageContextMenu.Dispose();
            roiContextMenu.Dispose();

            _currentImage?.Dispose();
            _currentImage = null;
            _RoiDic.Clear();
            _SegmentationDic.Clear();
            currentPoints.Clear();
            _detectionOverlays.Clear();
        }

        private Yolo.CClassItem ResolveSegmentationClass(Yolo.CClassItem classItem)
        {
            Yolo.CClassItem targetClass = classItem ?? _TempOb.cClassItem ?? new Yolo.CClassItem { Text = _SelectedClass };
            string className = targetClass.Text ?? _SelectedClass ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                className = "Defect";
            }

            targetClass.Text = className;
            targetClass.DrawColor = ResolveVisibleClassColor(className, targetClass.DrawColor);
            return targetClass;
        }

        private Yolo.CClassItem ResolveRoiClass(Yolo.CClassItem classItem)
        {
            Yolo.CClassItem targetClass = classItem ?? _TempOb.cClassItem ?? new Yolo.CClassItem { Text = _SelectedClass };
            string className = targetClass.Text ?? _SelectedClass ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                className = "Defect";
            }

            targetClass.Text = className;
            targetClass.DrawColor = ResolveVisibleClassColor(className, targetClass.DrawColor);
            _SelectedClass = className;
            _TempOb.cClassItem = targetClass;
            _TempOb.Title = className;
            return targetClass;
        }

        private Color ResolveVisibleClassColor(string className, Color fallbackColor)
        {
            Color classColor = CGlobal.Inst?.Data?.ClassNamedList?
                .FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase))
                ?.DrawColor ?? fallbackColor;
            return EnsureVisibleAnnotationColor(classColor);
        }

        private static Yolo.CClassItem ResolveClassItem(string className, IReadOnlyList<Yolo.CClassItem> classes)
        {
            Yolo.CClassItem classItem = classes?.FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase))
                ?? new Yolo.CClassItem { Text = className, DrawColor = Color.LimeGreen };
            classItem.DrawColor = EnsureVisibleAnnotationColor(classItem.DrawColor);
            return classItem;
        }

        private static Color EnsureVisibleAnnotationColor(Color color)
        {
            if (color.IsEmpty || color.ToArgb() == Color.Black.ToArgb() || (color.R + color.G + color.B) < 72)
            {
                return Color.LimeGreen;
            }

            return color;
        }

        private void DisposeCanvas()
        {
            if (Canvas == null)
            {
                return;
            }

            DetachCanvasEvents(Canvas);

            Control parent = Canvas.Parent;
            if (parent != null)
            {
                parent.Controls.Remove(Canvas);
            }

            Canvas.Dispose();
            Canvas = null;
        }

        private void DetachCanvasEvents(ImageCanvasControl canvas)
        {
            canvas.Draw -= OnCanvasDraw;
            canvas.MouseDoubleClicked -= OnCanvasMouseDoubleClicked;
            canvas.MouseDown -= OnCanvasMouseDown;
            canvas.MouseMove -= OnCanvasMouseMove;
            canvas.MouseUp -= OnCanvasMouseUp;
            canvas.MouseWheel -= OnCanvasMouseWheel;
            canvas.KeyDown -= OnCanvasKeyDown;
            canvas.Load -= OnCanvasLoad;
            canvas.DragEnter -= OnCanvasDragEnter;
            canvas.DragDrop -= OnCanvasDragDrop;
        }

        private static void DisposeHostControls(Control host)
        {
            while (host.Controls.Count > 0)
            {
                Control control = host.Controls[0];
                host.Controls.RemoveAt(0);
                control.Dispose();
            }
        }
    }
}
