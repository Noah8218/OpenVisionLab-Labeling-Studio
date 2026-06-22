using MvcVisionSystem.DrawObject;
using OpenVisionLab.ImageCanvas.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem._1._Core
{
    public sealed class DisplayLayerDocument : IDisposable
    {
        private readonly CViewer viewer = new CViewer();
        private bool disposed;

        public DisplayLayerDocument(Bitmap imageSource, int index, string title)
        {
            Index = index;
            Text = string.IsNullOrWhiteSpace(title) ? "Layer" : title;
            viewer.AnnotationSelectionChanged += Viewer_AnnotationSelectionChanged;
            if (imageSource != null)
            {
                SetImage(imageSource);
            }
        }

        public int Index { get; }

        public string Text { get; }

        public bool IsDisposed => disposed;

        public bool ImageChanged => viewer.ImageChanged;

        public int DetectionOverlayCount => viewer.DetectionOverlayCount;

        public LabelingRoiMode CurrentLabelingMode => viewer.CurrentMode;

        public int SegmentationBrushRadius
        {
            get => viewer.SegmentationBrushRadius;
            set => viewer.SegmentationBrushRadius = value;
        }

        public bool CanUndoAnnotationChange => viewer.CanUndoAnnotationChange;

        public bool CanRedoAnnotationChange => viewer.CanRedoAnnotationChange;

        public int SelectedAnnotationListIndex => viewer.SelectedAnnotationListIndex;

        public void SetImage(Bitmap image, bool zoomToFit = true)
        {
            ThrowIfDisposed();
            viewer.SetDisplayImage(image, Text, string.Empty, resetAnnotations: false, zoomToFit: zoomToFit);
        }

        public Bitmap GetCurrentImage()
        {
            return disposed ? null : viewer.CurrentImage;
        }

        public void AcceptImageChanged()
        {
            if (!disposed)
            {
                viewer.AcceptImageChanged();
            }
        }

        public void RefreshViewer()
        {
            viewer.Canvas?.RefreshGL();
        }

        public void SetDetectionOverlays(IEnumerable<DetectionOverlayItem> overlays)
        {
            ThrowIfDisposed();
            viewer.SetDetectionOverlays(overlays);
        }

        public IReadOnlyList<DetectionOverlayItem> GetDetectionOverlays()
        {
            return disposed ? Array.Empty<DetectionOverlayItem>() : viewer.GetDetectionOverlays();
        }

        public void ZoomToFit()
        {
            if (!disposed)
            {
                viewer.ZoomToFit();
            }
        }

        public void ResetAnnotations()
        {
            if (!disposed)
            {
                viewer.ResetAnnotations();
            }
        }

        public void SetLabelingMode(LabelingRoiMode mode)
        {
            if (disposed)
            {
                return;
            }

            switch (mode)
            {
                case LabelingRoiMode.Rectangle:
                    viewer.SetModeMultiRoi();
                    break;
                case LabelingRoiMode.Segmentation:
                    viewer.SetModeSegmentation();
                    break;
                case LabelingRoiMode.SegmentationBrush:
                    viewer.SetModeSegmentationBrush();
                    break;
                case LabelingRoiMode.SegmentationEraser:
                    viewer.SetModeSegmentationEraser();
                    break;
                default:
                    viewer.SetModeDrag();
                    break;
            }

            RefreshViewer();
        }

        public int AddAutoSegmentationFromRois(Yolo.CClassItem classItem = null, bool onlySelected = true)
        {
            return disposed ? 0 : viewer.AddAutoSegmentationFromRois(classItem, onlySelected);
        }

        public int MergeSegmentationSegments(string className = null)
        {
            return disposed ? 0 : viewer.MergeSegmentationSegments(className);
        }

        public bool UndoAnnotationChange()
        {
            return !disposed && viewer.UndoAnnotationChange();
        }

        public bool RedoAnnotationChange()
        {
            return !disposed && viewer.RedoAnnotationChange();
        }

        public bool DeleteSelectedAnnotation()
        {
            return !disposed && viewer.DeleteSelectedAnnotation();
        }

        public bool SelectAnnotationListItem(int listIndex)
        {
            return !disposed && viewer.SelectAnnotationListItem(listIndex);
        }

        public void SetSelectedClass(Yolo.CClassItem classItem)
        {
            if (!disposed)
            {
                viewer.SetSelectedClass(classItem);
            }
        }

        public void SetRoiRectangles(IEnumerable<Rectangle> rectangles, Yolo.CClassItem classItem = null, bool reset = true)
        {
            ThrowIfDisposed();
            viewer.SetRoiRectangles(rectangles, classItem, reset);
        }

        public void SetSegmentationPolygons(
            IReadOnlyDictionary<string, List<List<Point>>> polygonsByClass,
            IReadOnlyList<Yolo.CClassItem> classes,
            bool reset = true)
        {
            ThrowIfDisposed();
            viewer.SetSegmentationPolygons(polygonsByClass, classes, reset);
        }

        public void SetSegmentationObjects(
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<Yolo.CClassItem> classes,
            bool reset = true)
        {
            ThrowIfDisposed();
            viewer.SetSegmentationObjects(segmentsByClass, classes, reset);
        }

        public int AddSegmentationRectangles(IEnumerable<Rectangle> rectangles, Yolo.CClassItem classItem = null, bool reset = false)
        {
            return disposed ? 0 : viewer.AddSegmentationRectangles(rectangles, classItem, reset);
        }

        public IReadOnlyList<LabelingRoiListItem> GetRoiListItems()
        {
            return disposed ? Array.Empty<LabelingRoiListItem>() : viewer.GetRoiListItems();
        }

        public IReadOnlyDictionary<string, List<CRectangleObject>> GetRoiByClass()
        {
            return disposed
                ? new Dictionary<string, List<CRectangleObject>>()
                : viewer.RoiByClass;
        }

        public IReadOnlyDictionary<string, List<LabelingSegmentationObject>> GetSegmentsByClass()
        {
            return disposed
                ? new Dictionary<string, List<LabelingSegmentationObject>>()
                : viewer.SegmentsByClass;
        }

        public IReadOnlyList<List<Point>> GetSegmentationCutoutPolygons()
        {
            return disposed ? Array.Empty<List<Point>>() : viewer.GetSegmentationCutoutPolygons();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            viewer.AnnotationSelectionChanged -= Viewer_AnnotationSelectionChanged;
            viewer.Dispose();
        }

        private void Viewer_AnnotationSelectionChanged(object sender, LabelingAnnotationSelectionChangedEventArgs e)
        {
            CDisplayManager.NotifyAnnotationSelectionChanged(this, e);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(DisplayLayerDocument));
            }
        }
    }
}
