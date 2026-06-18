using Lib.Common;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using OpenVisionLab.ImageCanvas.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace MvcVisionSystem
{
    public partial class FormLayerDisplay : DockContent
    {
        public int nIndex;
        private CViewer viewer = new CViewer();
        public bool ChangeSize { get; set; }
        public List<FormLayerDisplay> DisplayList = new List<FormLayerDisplay>();
        public ImageCanvasControl ImageViewer => viewer?.Canvas;
        public bool ImageChanged => viewer?.ImageChanged == true;
        public int DetectionOverlayCount => viewer?.DetectionOverlayCount ?? 0;
        public LabelingRoiMode CurrentLabelingMode => viewer?.CurrentMode ?? LabelingRoiMode.Drag;
        public int SegmentationBrushRadius
        {
            get => viewer?.SegmentationBrushRadius ?? 8;
            set
            {
                if (viewer != null)
                {
                    viewer.SegmentationBrushRadius = value;
                }
            }
        }
        public bool CanUndoAnnotationChange => viewer?.CanUndoAnnotationChange == true;
        public bool CanRedoAnnotationChange => viewer?.CanRedoAnnotationChange == true;

        public FormLayerDisplay(Bitmap ImageSource, int nIndex, List<FormLayerDisplay> LayerDisplayList, bool UseCloseButton = true, string strTitle = "TEST", bool onlyDragMode = false)
        {
            InitializeComponent();
            ApplyViewerChromeTheme();

            this.nIndex = nIndex;
            DisplayList = LayerDisplayList;
            Text = strTitle != "TEST" ? strTitle : "TEST";
            TabText = BuildDisplayCaption(Text);
            ToolTipText = BuildDisplayToolTip(Text);
            CloseButton = UseCloseButton;
            CloseButtonVisible = UseCloseButton;
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.Document;

            viewer.AttachTo(panel3, onlyDragMode);
            viewer.AnnotationSelectionChanged += Viewer_AnnotationSelectionChanged;
            Activated += FormLayerDisplay_Activated;

            if (ImageSource != null)
            {
                SetImage(ImageSource);
            }
        }

        private void ApplyViewerChromeTheme()
        {
            BackColor = LabelingWorkbenchPalette.Canvas;
            panel1.BackColor = LabelingWorkbenchPalette.Canvas;
            panel2.BackColor = LabelingWorkbenchPalette.Status;
            panel3.BackColor = Color.Black;
            ApplyStatusLabelTheme(lbMODE);
            ApplyStatusLabelTheme(lbRGB);
            ApplyStatusLabelTheme(lbXY);
            ApplyStatusLabelTheme(lbGV);
            ApplyStatusLabelTheme(lbZOOM);
        }

        private static void ApplyStatusLabelTheme(Control label)
        {
            if (label == null)
            {
                return;
            }

            label.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            label.ForeColor = LabelingWorkbenchPalette.Text;
        }

        private static string BuildDisplayCaption(string title)
        {
            if (string.Equals(title, "Main", StringComparison.OrdinalIgnoreCase))
            {
                return "작업 캔버스";
            }

            if (string.Equals(title, "Detect", StringComparison.OrdinalIgnoreCase))
            {
                return "AI 후보";
            }

            return title;
        }

        private static string BuildDisplayToolTip(string title)
        {
            if (string.Equals(title, "Main", StringComparison.OrdinalIgnoreCase))
            {
                return "현재 이미지의 라벨 ROI를 편집합니다.";
            }

            if (string.Equals(title, "Detect", StringComparison.OrdinalIgnoreCase))
            {
                return "AI 검출 후보를 검토합니다.";
            }

            return title;
        }

        public void SetImage(Bitmap image, bool zoomToFit = true)
        {
            viewer.SetDisplayImage(image, string.Empty, string.Empty, resetAnnotations: false, zoomToFit: zoomToFit);
        }

        public Bitmap GetCurrentImage()
        {
            return viewer?.CurrentImage;
        }

        public void AcceptImageChanged()
        {
            viewer?.AcceptImageChanged();
        }

        public void RefreshViewer()
        {
            viewer?.Canvas?.RefreshGL();
        }

        public void SetDetectionOverlays(IEnumerable<DetectionOverlayItem> overlays)
        {
            viewer?.SetDetectionOverlays(overlays);
        }

        public IReadOnlyList<DetectionOverlayItem> GetDetectionOverlays()
        {
            return viewer?.GetDetectionOverlays() ?? new List<DetectionOverlayItem>();
        }

        public void ZoomToFit()
        {
            viewer?.ZoomToFit();
        }

        public void ResetAnnotations()
        {
            viewer?.ResetAnnotations();
        }

        public void SetLabelingMode(LabelingRoiMode mode)
        {
            switch (mode)
            {
                case LabelingRoiMode.Rectangle:
                    viewer?.SetModeMultiRoi();
                    break;
                case LabelingRoiMode.Segmentation:
                    viewer?.SetModeSegmentation();
                    break;
                case LabelingRoiMode.SegmentationBrush:
                    viewer?.SetModeSegmentationBrush();
                    break;
                case LabelingRoiMode.SegmentationEraser:
                    viewer?.SetModeSegmentationEraser();
                    break;
                default:
                    viewer?.SetModeDrag();
                    break;
            }

            RefreshViewer();
        }

        public int AddAutoSegmentationFromRois(Yolo.CClassItem classItem = null, bool onlySelected = true)
        {
            return viewer?.AddAutoSegmentationFromRois(classItem, onlySelected) ?? 0;
        }

        public int MergeSegmentationSegments(string className = null)
        {
            return viewer?.MergeSegmentationSegments(className) ?? 0;
        }

        public bool UndoAnnotationChange()
        {
            return viewer?.UndoAnnotationChange() == true;
        }

        public bool RedoAnnotationChange()
        {
            return viewer?.RedoAnnotationChange() == true;
        }

        public bool DeleteSelectedAnnotation()
        {
            return viewer?.DeleteSelectedAnnotation() == true;
        }

        public int SelectedAnnotationListIndex => viewer?.SelectedAnnotationListIndex ?? -1;

        public bool SelectAnnotationListItem(int listIndex)
        {
            return viewer?.SelectAnnotationListItem(listIndex) == true;
        }

        public void SetSelectedClass(Yolo.CClassItem classItem)
        {
            viewer?.SetSelectedClass(classItem);
        }

        public void SetRoiRectangles(IEnumerable<Rectangle> rectangles, Yolo.CClassItem classItem = null, bool reset = true)
        {
            viewer?.SetRoiRectangles(rectangles, classItem, reset);
        }

        public void SetSegmentationPolygons(
            IReadOnlyDictionary<string, List<List<Point>>> polygonsByClass,
            IReadOnlyList<Yolo.CClassItem> classes,
            bool reset = true)
        {
            viewer?.SetSegmentationPolygons(polygonsByClass, classes, reset);
        }

        public void SetSegmentationObjects(
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<Yolo.CClassItem> classes,
            bool reset = true)
        {
            viewer?.SetSegmentationObjects(segmentsByClass, classes, reset);
        }

        public int AddSegmentationRectangles(IEnumerable<Rectangle> rectangles, Yolo.CClassItem classItem = null, bool reset = false)
        {
            return viewer?.AddSegmentationRectangles(rectangles, classItem, reset) ?? 0;
        }

        public IReadOnlyList<LabelingRoiListItem> GetRoiListItems()
        {
            return viewer?.GetRoiListItems() ?? new List<LabelingRoiListItem>();
        }

        public IReadOnlyDictionary<string, List<CRectangleObject>> GetRoiByClass()
        {
            return viewer?.RoiByClass ?? new Dictionary<string, List<CRectangleObject>>();
        }

        public IReadOnlyDictionary<string, List<LabelingSegmentationObject>> GetSegmentsByClass()
        {
            return viewer?.SegmentsByClass ?? new Dictionary<string, List<LabelingSegmentationObject>>();
        }

        public IReadOnlyList<List<Point>> GetSegmentationCutoutPolygons()
        {
            return viewer?.GetSegmentationCutoutPolygons() ?? new List<List<Point>>();
        }

        private void FormLayerDisplay_Activated(object sender, EventArgs e)
        {
            CDisplayManager.FocusItem = Text;
            CDisplayManager.SelecteItem = Text;

            if (viewer?.CurrentImage != null)
            {
                CDisplayManager.ImageSrc = BitmapImageConverter.ToMat(viewer.CurrentImage);
            }
        }

        private void Viewer_AnnotationSelectionChanged(object sender, LabelingAnnotationSelectionChangedEventArgs e)
        {
            CDisplayManager.NotifyAnnotationSelectionChanged(this, e);
        }

        private void LayerDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (viewer != null)
            {
                viewer.AnnotationSelectionChanged -= Viewer_AnnotationSelectionChanged;
            }

            int displayIndex = GetDisplayIndex(Text);
            if (displayIndex >= 0 && displayIndex < DisplayList.Count)
            {
                DisplayList.RemoveAt(displayIndex);
            }
        }

        private int GetDisplayIndex(string strTitle)
        {
            for (int i = 0; i < DisplayList.Count; i++)
            {
                if (DisplayList[i].Text == strTitle)
                {
                    return i;
                }
            }

            return -1;
        }

        private void timePixelData_Tick(object sender, EventArgs e)
        {
            try
            {
                Color pixelRgb = viewer?.PixelRgb ?? Color.Empty;
                Point position = viewer?.ImagePosition ?? Point.Empty;
                lbMODE.Text = viewer?.ModeDisplayText ?? CViewer.GetUiModeDisplayText(LabelingRoiMode.Drag);
                lbRGB.Text = string.Format("RGB {0},{1},{2}", pixelRgb.R, pixelRgb.G, pixelRgb.B);
                lbXY.Text = string.Format("좌표 {0},{1}", position.X, position.Y);
                lbGV.Text = string.Format("GRAY {0}", viewer?.GrayValue ?? 0);
                lbZOOM.Text = string.Format("줌 {0:0}%", viewer?.ZoomPercent ?? 0);
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"[FAILED] {GetType().Name}==>{nameof(timePixelData_Tick)} Exception ==> {ex.Message}");
            }
        }

        private void FormLayerDisplay_VisibleChanged(object sender, EventArgs e)
        {
            if (ChangeSize)
            {
                return;
            }

            if (DockHandler.FloatPane != null)
            {
                DockHandler.FloatPane.FloatWindow.Bounds = new Rectangle(DockHandler.FloatPane.FloatWindow.Bounds.X, DockHandler.FloatPane.FloatWindow.Bounds.Y, 800, 400);
            }

            Refresh();
            ChangeSize = true;
            viewer?.ZoomToFit();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
        }
    }
}
