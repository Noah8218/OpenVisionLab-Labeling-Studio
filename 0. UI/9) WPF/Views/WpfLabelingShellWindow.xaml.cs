using Lib.Common;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Views;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUiApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfUiApplicationThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;
using WpfUiFluentWindow = Wpf.Ui.Controls.FluentWindow;
using WpfUiWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow : WpfUiFluentWindow
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
        private const string TutorialHtmlGuideRelativePath = @"docs\tutorial\labeling-workbench-tutorial.html";
        private const int BatchReviewStatusSaveInterval = 10;
        private const int ImageDecodeCacheCapacity = 8;
        private const long ImageDecodeCacheMaxPixels = 4L * 1024L * 1024L;
        private const long ImageDecodeCacheMaxBytes = 64L * 1024L * 1024L;
        private const int TrainingGuideRunHistoryLimit = 8;
        private const int TrainingStatusPollTimeoutSeconds = 600;
        private const int AnnotationHistoryLimit = 50;
        private const int ObjectReviewFullRefreshDeleteLimit = 10_000;
        private readonly CGlobal global = CGlobal.Inst;
        private readonly ObservableCollection<WpfImageQueueItem> imageQueueItems = new ObservableCollection<WpfImageQueueItem>();
        private readonly YoloImageReviewStatusService imageReviewStatus = new YoloImageReviewStatusService();
        private readonly object imageDecodeCacheLock = new object();
        private readonly Dictionary<string, CachedDecodedImage> imageDecodeCache = new Dictionary<string, CachedDecodedImage>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> imageDecodeCacheOrder = new LinkedList<string>();
        private long imageDecodeCacheBytes;
        private long imageDecodeCacheHits;
        private long imageDecodeCacheMisses;
        private long imageDecodeCacheStores;
        private long imageDecodeCacheEvictions;
        private ImageLoadDiagnostics lastImageLoadDiagnostics = ImageLoadDiagnostics.Empty;
        private ICollectionView imageQueueView;
        private CancellationTokenSource imageQueueDetailLoadCts;
        private CancellationTokenSource batchDetectionCts;
        private DrawingBitmap activeImageBitmap;
        private string activeImagePath = string.Empty;
        private string currentImageRoot = string.Empty;
        private DrawingSize activeImageSize = DrawingSize.Empty;
        private readonly List<DrawingRectangle> manualRois = new List<DrawingRectangle>();
        private readonly List<string> manualRoiClassNames = new List<string>();
        private readonly List<CanvasRoiShapeKind> manualRoiShapeKinds = new List<CanvasRoiShapeKind>();
        private readonly List<string> manualRoiOverlayIds = new List<string>();
        private readonly List<LabelingSegmentationObject> manualSegments = new List<LabelingSegmentationObject>();
        private readonly WpfPolygonAnnotationService polygonAnnotationService = new WpfPolygonAnnotationService();
        private readonly WpfMaskAnnotationService maskAnnotationService = new WpfMaskAnnotationService();
        private readonly List<WpfAnnotationHistorySnapshot> undoAnnotationHistory = new List<WpfAnnotationHistorySnapshot>();
        private readonly List<WpfAnnotationHistorySnapshot> redoAnnotationHistory = new List<WpfAnnotationHistorySnapshot>();
        private readonly List<YoloWorkerSmokeCandidate> pendingDetectionCandidates = new List<YoloWorkerSmokeCandidate>();
        private readonly List<YoloWorkerSmokeCandidate> confirmedDetectionCandidates = new List<YoloWorkerSmokeCandidate>();
        private bool suppressImageQueueSelection;
        private bool isDetecting;
        private bool isBatchDetectionRunning;
        private bool isYoloEnvironmentCommandRunning;
        private bool isTrainingCommandRunning;
        private bool imageQueuePanelEventsAttached;
        private bool canvasPanelEventsAttached;
        private bool learningWorkflowPanelEventsAttached;
        private bool objectReviewPanelEventsAttached;
        private bool suppressObjectReviewSelectionChanged;
        private bool candidateReviewPanelEventsAttached;
        private bool classCatalogPanelEventsAttached;
        private bool yoloStatusPanelEventsAttached;
        private bool projectConfigPanelEventsAttached;
        private bool suppressProjectRecipeSelection;
        private bool yoloModelSettingsPanelEventsAttached;
        private bool trainingSettingsPanelEventsAttached;
        private int batchDetectionTotalCount;
        private int batchDetectionCompletedCount;
        private readonly Stopwatch inferenceStatusPulseStopwatch = new Stopwatch();
        private readonly DispatcherTimer inferenceStatusPulseTimer;
        private readonly DispatcherTimer trainingStatusPollTimer;
        private DateTime trainingStatusPollStartedUtc = DateTime.MinValue;
        private int imageDecodePreloadVersion;
        private string lastAutoAppliedTrainingWeightsPath = string.Empty;
        private bool hasPendingTrainingWeightsRecipeSave;
        private YoloDatasetReadinessReport lastYoloTrainingReadinessReport;
        private string lastRecordedTrainingGuideRunSignature = string.Empty;
        private Task imageDecodePreloadTask = Task.CompletedTask;
        private ShellTheme currentTheme = ShellTheme.Dark;
        private WorkflowMode currentWorkflowMode = WorkflowMode.Labeling;
        private WpfAnnotationTool activeAnnotationTool = WpfAnnotationTool.Select;
        private System.Drawing.Point? lastMaskStrokePoint;
        private WpfAnnotationHistorySnapshot activeMaskStrokeSnapshot;
        private bool activeMaskStrokeChanged;
        private int activeSegmentDragIndex = -1;
        private int activePolygonPointDragIndex = -1;
        private System.Drawing.Point? lastSegmentDragPoint;
        private WpfAnnotationHistorySnapshot activeSegmentDragSnapshot;
        private bool activeSegmentDragChanged;
        private bool suppressAnnotationHistory;
        private string annotationDirtyReason = string.Empty;
        private string activeRoiEditHistoryOverlayId = string.Empty;

        public WpfLabelingShellWindow()
        {
            InitializeComponent();
            PreviewKeyDown += WpfLabelingShellWindow_PreviewKeyDown;
            inferenceStatusPulseTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            inferenceStatusPulseTimer.Tick += InferenceStatusPulseTimer_Tick;
            trainingStatusPollTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            trainingStatusPollTimer.Tick += TrainingStatusPollTimer_Tick;
            DataContext = ShellViewModel;
            RegisterLearningWorkflowPanelNames();
            RegisterImageQueuePanelNames();
            RegisterCanvasPanelNames();
            RegisterObjectReviewPanelNames();
            RegisterCandidateReviewPanelNames();
            RegisterClassCatalogPanelNames();
            RegisterYoloStatusPanelNames();
            RegisterProjectConfigPanelNames();
            RegisterYoloModelSettingsPanelNames();
            RegisterTrainingSettingsPanelNames();
            RegisterStatusBarPanelNames();
            RegisterShellLogPanelNames();
            ApplyTheme(ShellTheme.Dark);
            MainCanvasViewModel = new RoiImageCanvasViewModel("Main");
            ConfigureLabelingCanvasDefaults();
            MainCanvasViewModel.RoiAdded += MainCanvasViewModel_RoiAdded;
            MainCanvasViewModel.RoiEditingCompleted += MainCanvasViewModel_RoiEditingCompleted;
            MainCanvasViewModel.RoiMouseUp += MainCanvasViewModel_RoiMouseUp;
            MainCanvasViewModel.RemoveRoiRequested += MainCanvasViewModel_RemoveRoiRequested;
            MainCanvasViewModel.DetectionOverlayClicked += MainCanvasViewModel_DetectionOverlayClicked;
            MainCanvasViewModel.ImagePointClicked += MainCanvasViewModel_ImagePointClicked;
            MainCanvasViewModel.ImagePointHovered += MainCanvasViewModel_ImagePointHovered;
            MainCanvasViewModel.ImagePointMoved += MainCanvasViewModel_ImagePointMoved;
            MainCanvasViewModel.ImagePointReleased += MainCanvasViewModel_ImagePointReleased;
            MainCanvasView.DataContext = MainCanvasViewModel;
            InitializeImageQueuePanel();
            InitializeYoloEditorPanel();
            PopulateClassList();
            RefreshCandidateList();
            RefreshObjectList();
            UpdateCandidateActionState();
            UpdateYoloCommandButtons();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            SetAnnotationSaveStatusWaiting();
            RefreshAnnotationHistoryToolState();
        }

        private void WpfLabelingShellWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control
                || IsTextEditingElement(e.OriginalSource))
            {
                return;
            }

            if (e.Key == Key.Z)
            {
                e.Handled = UndoWpfAnnotationHistory();
                return;
            }

            if (e.Key == Key.Y)
            {
                e.Handled = RedoWpfAnnotationHistory();
            }
        }

        private static bool IsTextEditingElement(object source)
        {
            return source is TextBox
                || source is ComboBox
                || source is System.Windows.Controls.Primitives.TextBoxBase;
        }

        public WpfLabelingShellViewModel ShellViewModel { get; } = new WpfLabelingShellViewModel();

        public RoiImageCanvasViewModel MainCanvasViewModel { get; }

        public ObservableCollection<WpfImageQueueItem> ImageQueueItems => imageQueueItems;

        private void ConfigureLabelingCanvasDefaults()
        {
            MainCanvasViewModel.ShowGroupNames = false;
            MainCanvasViewModel.ShowRoiItemNames = false;
            MainCanvasViewModel.ShowGroupBounds = false;
            MainCanvasViewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
        }

        private void MainCanvasViewModel_RoiAdded(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (e?.RoiRect == null || activeImageSize.IsEmpty)
            {
                return;
            }

            DrawingRectangle bounds = ConvertCanvasRectToImageBounds(e.RoiRect);
            if (bounds.IsEmpty)
            {
                return;
            }

            string overlayId = e.RoiRect.UniqueId ?? string.Empty;
            int existingIndex = FindManualRoiIndexByOverlayId(overlayId);
            if (existingIndex >= 0)
            {
                if (manualRois[existingIndex] != bounds || GetManualRoiShapeKind(existingIndex) != e.RoiRect.ShapeKind)
                {
                    RegisterAnnotationHistoryBeforeChange("Update ROI");
                }

                manualRois[existingIndex] = bounds;
                manualRoiShapeKinds[existingIndex] = e.RoiRect.ShapeKind;
            }
            else
            {
                RegisterAnnotationHistoryBeforeChange("Add ROI");
                manualRois.Add(bounds);
                manualRoiClassNames.Add(FirstNonEmpty(GetSelectedClassName(), "Defect"));
                manualRoiShapeKinds.Add(e.RoiRect.ShapeKind);
                manualRoiOverlayIds.Add(overlayId);
            }

            RefreshObjectListWithSelection(CreateManualRoiSelection(e.RoiRect));
            ObjectsReviewTab.IsSelected = true;
            string shapeName = FormatManualRoiShapeName(e.RoiRect.ShapeKind);
            SetModelStatus($"라벨 추가: {shapeName} {WpfCandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            AppendLog($"라벨 추가({shapeName}): {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}");
        }

        private void MainCanvasViewModel_RoiEditingCompleted(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (e?.RoiRect == null || activeImageSize.IsEmpty)
            {
                return;
            }

            UpdateManualRoiFromCanvasRect(e.RoiRect);
        }

        private void MainCanvasViewModel_RoiMouseUp(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            WpfObjectReviewItemRef selectedManualRoi = null;
            bool updatedSingleObjectRow = false;
            if (e?.RoiRect != null)
            {
                UpdateManualRoiFromCanvasRect(e.RoiRect);
                selectedManualRoi = CreateManualRoiSelection(e.RoiRect);
                updatedSingleObjectRow = selectedManualRoi != null
                    && TryRefreshManualRoiObjectReviewRow(selectedManualRoi.Index, select: true);
            }

            activeRoiEditHistoryOverlayId = string.Empty;
            if (!updatedSingleObjectRow)
            {
                RefreshObjectListWithSelection(selectedManualRoi);
            }
        }

        private void MainCanvasViewModel_DetectionOverlayClicked(object sender, int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= pendingDetectionCandidates.Count)
            {
                return;
            }

            YoloWorkerSmokeCandidate candidate = pendingDetectionCandidates[candidateIndex];
            RefreshCandidateListWithPreferred(candidate);
            CandidatesReviewTab.IsSelected = true;
            CandidateListBox?.ScrollIntoView(CandidateReviewViewModel?.SelectedCandidate);
            ApplyCandidateSelectionReview(candidate);
            UpdateDetectionResultOverlay();
            RedrawReviewRois();
            SetModelStatus($"AI 후보 선택: {FormatCandidate(candidate)}");
        }

        private void MainCanvasViewModel_RemoveRoiRequested(object sender, CanvasRect<float> rect)
        {
            int index = FindManualRoiIndexByOverlayId(rect?.UniqueId);
            if (index < 0)
            {
                return;
            }

            RegisterAnnotationHistoryBeforeChange("Remove ROI");
            manualRois.RemoveAt(index);
            RemoveAtIfPresent(manualRoiClassNames, index);
            RemoveAtIfPresent(manualRoiShapeKinds, index);
            RemoveAtIfPresent(manualRoiOverlayIds, index);
            RefreshObjectList();
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }

        private void MainCanvasViewModel_ImagePointClicked(object sender, CanvasImagePointEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (activeAnnotationTool == WpfAnnotationTool.Select && TryBeginSelectedSegmentEdit(e))
            {
                return;
            }

            if (activeAnnotationTool == WpfAnnotationTool.Brush || activeAnnotationTool == WpfAnnotationTool.Eraser)
            {
                ApplyMaskAnnotationStroke(e, resetStroke: true);
                return;
            }

            if (activeAnnotationTool != WpfAnnotationTool.Polygon)
            {
                return;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                polygonAnnotationService.Reset();
                RefreshPolygonOverlays();
                SetYoloCommandStatus("Polygon canceled. Click the image to start a new polygon.", isBusy: false);
                return;
            }

            if (e.Button != CanvasPointerButton.Left || activeImageSize.IsEmpty)
            {
                return;
            }

            if (e.Clicks > 1 && polygonAnnotationService.Points.Count >= 3)
            {
                CompletePolygonAnnotation();
                return;
            }

            if (!polygonAnnotationService.TryAddPoint(e.ImagePoint, activeImageSize, out bool closed))
            {
                return;
            }

            RefreshPolygonOverlays();
            if (closed)
            {
                CompletePolygonAnnotation();
                return;
            }

            SetYoloCommandStatus($"Polygon draft: {polygonAnnotationService.Points.Count} point(s). Click near the first point or double-click to finish.", isBusy: false);
        }

        private void MainCanvasViewModel_ImagePointMoved(object sender, CanvasImagePointEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (activeAnnotationTool == WpfAnnotationTool.Select && TryMoveSelectedSegmentEdit(e))
            {
                return;
            }

            if (activeAnnotationTool != WpfAnnotationTool.Brush && activeAnnotationTool != WpfAnnotationTool.Eraser)
            {
                return;
            }

            ApplyMaskAnnotationStroke(e, resetStroke: false);
        }

        private void MainCanvasViewModel_ImagePointReleased(object sender, CanvasImagePointEventArgs e)
        {
            CompleteMaskAnnotationStroke();
            lastMaskStrokePoint = null;
            CompleteSelectedSegmentEdit();
        }

        private void MainCanvasViewModel_ImagePointHovered(object sender, CanvasImagePointEventArgs e)
        {
            if (e == null || (activeAnnotationTool != WpfAnnotationTool.Brush && activeAnnotationTool != WpfAnnotationTool.Eraser))
            {
                MainCanvasViewModel.ClearBrushCursorPreview();
                return;
            }

            MainCanvasViewModel.SetBrushCursorPreview(
                e.ImagePoint,
                GetMaskBrushRadius(),
                GetMaskCursorPreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                activeAnnotationTool == WpfAnnotationTool.Eraser);
        }

        private void BeginPolygonAnnotationMode()
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            activeAnnotationTool = WpfAnnotationTool.Polygon;
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            SetModelStatus("Tool: polygon segmentation");
            SetYoloCommandStatus("Polygon: click boundary points. Click near the first point or double-click to finish. Right-click cancels the draft.", isBusy: false);
            AppendLog("Polygon labeling tool enabled.");
        }

        private void EndPolygonAnnotationMode(bool clearDraft)
        {
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode = false;
            }

            if (clearDraft)
            {
                polygonAnnotationService.Reset();
                RefreshPolygonOverlays();
            }
        }

        private void BeginMaskAnnotationMode(WpfAnnotationTool tool)
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            activeAnnotationTool = tool;
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            polygonAnnotationService.Reset();
            lastMaskStrokePoint = null;
            activeMaskStrokeSnapshot = null;
            activeMaskStrokeChanged = false;
            RefreshPolygonOverlays();

            string toolName = tool == WpfAnnotationTool.Eraser ? "eraser" : "brush";
            int radius = GetMaskBrushRadius();
            SetModelStatus($"Tool: mask {toolName}");
            SetYoloCommandStatus($"Mask {toolName}: drag on the image. Brush radius {radius}px. Right-click resets the current stroke.", isBusy: false);
            AppendLog($"Mask {toolName} tool enabled. Radius:{radius}px");
        }

        private void EndMaskAnnotationMode()
        {
            CompleteMaskAnnotationStroke();
            lastMaskStrokePoint = null;
            MainCanvasViewModel?.ClearBrushCursorPreview();
            if (activeAnnotationTool == WpfAnnotationTool.Brush || activeAnnotationTool == WpfAnnotationTool.Eraser)
            {
                activeAnnotationTool = WpfAnnotationTool.Select;
            }
        }

        private bool TryBeginSelectedSegmentEdit(CanvasImagePointEventArgs e)
        {
            if (e == null || e.Button != CanvasPointerButton.Left || activeImageSize.IsEmpty)
            {
                return false;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                || item.Source != WpfObjectReviewSource.ManualSegment
                || item.Index < 0
                || item.Index >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[item.Index];
            if (segment == null)
            {
                return false;
            }

            int pointIndex = -1;
            if (segment.IsRasterMask)
            {
                if (!IsMaskPixelHit(segment, e.ImagePoint))
                {
                    return false;
                }
            }
            else
            {
                pointIndex = WpfPolygonAnnotationService.FindNearestPointIndex(segment, e.ImagePoint, maxDistancePixels: 8);
                if (pointIndex < 0 && !WpfPolygonAnnotationService.IsPointInsidePolygon(segment, e.ImagePoint))
                {
                    return false;
                }
            }

            activeSegmentDragIndex = item.Index;
            activePolygonPointDragIndex = pointIndex;
            lastSegmentDragPoint = e.ImagePoint;
            activeSegmentDragChanged = false;
            activeSegmentDragSnapshot = CaptureAnnotationHistory(segment.IsRasterMask
                ? "Move mask"
                : pointIndex >= 0 ? "Move polygon point" : "Move polygon");
            RefreshPolygonOverlays();
            SetYoloCommandStatus(segment.IsRasterMask
                ? "Mask selected: drag to move it."
                : pointIndex >= 0
                    ? $"Polygon point {pointIndex + 1} selected: drag to move it."
                    : "Polygon selected: drag inside to move it.",
                isBusy: false);
            return true;
        }

        private bool TryMoveSelectedSegmentEdit(CanvasImagePointEventArgs e)
        {
            if (e == null
                || e.Button != CanvasPointerButton.Left
                || activeSegmentDragIndex < 0
                || activeSegmentDragIndex >= manualSegments.Count
                || !lastSegmentDragPoint.HasValue)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[activeSegmentDragIndex];
            if (segment == null)
            {
                return false;
            }

            bool changed;
            if (segment.IsRasterMask)
            {
                System.Drawing.Point previous = lastSegmentDragPoint.Value;
                changed = maskAnnotationService.TryMoveRasterMask(
                    segment,
                    e.ImagePoint.X - previous.X,
                    e.ImagePoint.Y - previous.Y,
                    activeImageSize,
                    out _);
            }
            else
            {
                System.Drawing.Point previous = lastSegmentDragPoint.Value;
                changed = activePolygonPointDragIndex >= 0
                    ? WpfPolygonAnnotationService.TryMovePoint(
                        segment,
                        activePolygonPointDragIndex,
                        e.ImagePoint,
                        activeImageSize,
                        out _)
                    : WpfPolygonAnnotationService.TryMovePolygon(
                        segment,
                        e.ImagePoint.X - previous.X,
                        e.ImagePoint.Y - previous.Y,
                        activeImageSize,
                        out _);
            }

            if (!changed)
            {
                return true;
            }

            lastSegmentDragPoint = e.ImagePoint;
            activeSegmentDragChanged = true;
            RefreshPolygonOverlays();
            RefreshObjectList();
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            return true;
        }

        private void CompleteSelectedSegmentEdit()
        {
            if (activeSegmentDragSnapshot != null && activeSegmentDragChanged)
            {
                PushAnnotationHistorySnapshot(activeSegmentDragSnapshot);
                AppendLog(activePolygonPointDragIndex >= 0
                    ? "Polygon point moved."
                    : "Mask or polygon moved.");
            }

            activeSegmentDragIndex = -1;
            activePolygonPointDragIndex = -1;
            lastSegmentDragPoint = null;
            activeSegmentDragSnapshot = null;
            activeSegmentDragChanged = false;
            RefreshPolygonOverlays();
        }

        private static bool IsMaskPixelHit(LabelingSegmentationObject segment, System.Drawing.Point imagePoint)
        {
            if (segment?.IsRasterMask != true || segment.Bounds.IsEmpty || !segment.Bounds.Contains(imagePoint))
            {
                return false;
            }

            int index = (imagePoint.Y * segment.MaskSize.Width) + imagePoint.X;
            return index >= 0 && index < segment.MaskData.Length && segment.MaskData[index] != 0;
        }

        private void ApplyMaskAnnotationStroke(CanvasImagePointEventArgs e, bool resetStroke)
        {
            if (e == null || activeImageSize.IsEmpty)
            {
                return;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                CompleteMaskAnnotationStroke();
                lastMaskStrokePoint = null;
                SetYoloCommandStatus("Mask stroke reset. Drag again to continue editing.", isBusy: false);
                return;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return;
            }

            int radius = GetMaskBrushRadius();
            IReadOnlyList<System.Drawing.Point> centers = maskAnnotationService.BuildStrokeCenters(
                resetStroke ? null : lastMaskStrokePoint,
                e.ImagePoint,
                radius);
            lastMaskStrokePoint = e.ImagePoint;

            string actionName = activeAnnotationTool == WpfAnnotationTool.Brush ? "Paint mask" : "Erase mask";
            if (resetStroke && activeMaskStrokeSnapshot != null)
            {
                CompleteMaskAnnotationStroke();
            }

            if (activeMaskStrokeSnapshot == null)
            {
                // Brush/eraser MouseMove can fire hundreds of times in one stroke.
                // Capture the undo state once and commit side-list updates on mouse-up.
                activeMaskStrokeSnapshot = CaptureAnnotationHistory(actionName);
                activeMaskStrokeChanged = false;
            }

            bool changed;
            if (activeAnnotationTool == WpfAnnotationTool.Brush)
            {
                CClassItem classItem = EnsureClassItem(FirstNonEmpty(GetSelectedClassName(), "Defect"));
                changed = maskAnnotationService.Paint(
                    manualSegments,
                    centers,
                    radius,
                    activeImageSize,
                    classItem,
                    out _,
                    out _);
            }
            else
            {
                changed = maskAnnotationService.Erase(
                    manualSegments,
                    centers,
                    radius,
                    activeImageSize,
                    out _);
            }

            if (!changed)
            {
                return;
            }

            activeMaskStrokeChanged = true;
            RefreshPolygonOverlays();
            string action = activeAnnotationTool == WpfAnnotationTool.Brush ? "Mask painted" : "Mask erased";
            SetModelStatus($"{action}: editing {manualSegments.Count} segment object(s)");
        }

        private void CompleteMaskAnnotationStroke()
        {
            if (activeMaskStrokeSnapshot != null && activeMaskStrokeChanged)
            {
                PushAnnotationHistorySnapshot(activeMaskStrokeSnapshot);
                RefreshObjectList();
                ObjectsReviewTab.IsSelected = true;
                SetModelStatus($"Mask edit committed: {manualSegments.Count} segment object(s)");
                RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            }

            activeMaskStrokeSnapshot = null;
            activeMaskStrokeChanged = false;
        }

        private int GetMaskBrushRadius()
        {
            int brushSize = LearningWorkflowViewModel?.BrushSize ?? WpfMaskAnnotationService.DefaultBrushRadius * 2;
            return Math.Clamp((int)Math.Round(brushSize / 2D), 1, 128);
        }

        private System.Drawing.Color GetMaskCursorPreviewColor(bool isEraser)
        {
            if (isEraser)
            {
                return System.Drawing.Color.FromArgb(245, 158, 11);
            }

            string className = FirstNonEmpty(GetSelectedClassName(), "Defect");
            CClassItem existing = global.Data.ClassNamedList?
                .FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
            return existing?.DrawColor ?? System.Drawing.Color.FromArgb(44, 210, 110);
        }

        private void CompletePolygonAnnotation()
        {
            CClassItem classItem = EnsureClassItem(FirstNonEmpty(GetSelectedClassName(), "Defect"));
            if (!polygonAnnotationService.TryComplete(classItem, activeImageSize, out LabelingSegmentationObject annotation, out string message))
            {
                SetYoloCommandStatus(message, isBusy: false);
                return;
            }

            RegisterAnnotationHistoryBeforeChange("Add polygon");
            manualSegments.Add(annotation);
            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            RefreshObjectList();
            ObjectsReviewTab.IsSelected = true;
            SetModelStatus($"Polygon added: {annotation.ClassName} / {annotation.Points.Count} points");
            AppendLog($"Polygon added: {annotation.ClassName} / {annotation.Points.Count} points / {FormatSegmentBoundsCompact(annotation)}");
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }

        private void RefreshPolygonOverlays()
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            var overlays = new List<RoiImageCanvasPolygonOverlay>();
            var maskOverlays = new List<RoiImageCanvasMaskOverlay>();
            float maskOpacity = (float)(LearningWorkflowViewModel?.MaskOpacity ?? 0.66);
            WpfObjectReviewListItem selectedObject = ObjectReviewViewModel?.SelectedObject;
            string selectedSourceKey = selectedObject?.SourceKey ?? string.Empty;
            int selectedSourceIndex = selectedObject?.SourceIndex ?? -1;
            for (int i = 0; i < manualSegments.Count; i++)
            {
                LabelingSegmentationObject segment = manualSegments[i];
                if (segment == null)
                {
                    continue;
                }

                string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
                bool isSegmentSelected = string.Equals(
                    selectedSourceKey,
                    WpfObjectReviewSource.ManualSegment.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                    && selectedSourceIndex == i;
                if (segment.IsRasterMask)
                {
                    if (segment.MaskData == null || segment.MaskSize.IsEmpty || segment.Bounds.IsEmpty)
                    {
                        continue;
                    }

                    DrawingRectangle maskBounds = DrawingRectangle.Intersect(
                        segment.Bounds,
                        new DrawingRectangle(0, 0, segment.MaskSize.Width, segment.MaskSize.Height));
                    if (!maskBounds.IsEmpty)
                    {
                        int displayIndex = manualRois.Count + i + 1;
                        maskOverlays.Add(new RoiImageCanvasMaskOverlay(
                            $"{activeImagePath}|mask|{i}",
                            segment.MaskData,
                            segment.MaskSize,
                            maskBounds,
                            segment.Color,
                            maskOpacity,
                            segment.RenderVersion,
                            isSegmentSelected,
                            $"MASK {displayIndex} {className}",
                            segment.RenderDirtyBounds));
                    }

                    continue;
                }

                if (segment.Points == null || segment.Points.Count == 0)
                {
                    continue;
                }

                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    segment.Points,
                    $"SEG {i + 1} {className}",
                    segment.Color,
                    isClosed: true,
                    isDraft: false,
                    isSelected: isSegmentSelected,
                    selectedPointIndex: activeSegmentDragIndex == i ? activePolygonPointDragIndex : -1));
            }

            if (polygonAnnotationService.Points.Count > 0)
            {
                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    polygonAnnotationService.Points,
                    $"Draft {polygonAnnotationService.Points.Count}",
                    System.Drawing.Color.FromArgb(80, 180, 255),
                    polygonAnnotationService.IsClosed,
                    isDraft: true));
            }

            MainCanvasViewModel.SetSegmentationOverlays(overlays, maskOverlays);
        }

        private static string FormatSegmentBoundsCompact(LabelingSegmentationObject segment)
        {
            DrawingRectangle bounds = segment?.Bounds ?? DrawingRectangle.Empty;
            return bounds.IsEmpty
                ? "-"
                : WpfCandidateReviewPresenter.FormatBoundsCompact(bounds);
        }

        private void UpdateManualRoiFromCanvasRect(CanvasRect<float> rect)
        {
            int index = FindManualRoiIndexByOverlayId(rect?.UniqueId);
            if (index < 0)
            {
                return;
            }

            DrawingRectangle bounds = ConvertCanvasRectToImageBounds(rect);
            if (bounds.IsEmpty)
            {
                return;
            }

            if (manualRois[index] != bounds || GetManualRoiShapeKind(index) != rect.ShapeKind)
            {
                RegisterRoiEditHistoryBeforeChange(rect.UniqueId, "Edit ROI");
            }

            manualRois[index] = bounds;
            manualRoiShapeKinds[index] = rect.ShapeKind;
        }

        private WpfObjectReviewItemRef CreateManualRoiSelection(CanvasRect<float> rect)
        {
            int index = FindManualRoiIndexByOverlayId(rect?.UniqueId);
            return index >= 0 ? WpfObjectReviewItemRef.Manual(index, rect?.UniqueId) : null;
        }

        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
        {
            return WpfAnnotationHistoryService.Capture(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualSegments,
                pendingDetectionCandidates,
                confirmedDetectionCandidates);
        }

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true)
        {
            PushAnnotationHistorySnapshot(CaptureAnnotationHistory(actionName), markDirty);
        }

        private void RegisterRoiEditHistoryBeforeChange(string overlayId, string actionName)
        {
            string normalizedOverlayId = overlayId ?? string.Empty;
            if (string.Equals(activeRoiEditHistoryOverlayId, normalizedOverlayId, StringComparison.Ordinal))
            {
                return;
            }

            RegisterAnnotationHistoryBeforeChange(actionName);
            activeRoiEditHistoryOverlayId = normalizedOverlayId;
        }

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot, bool markDirty = true)
        {
            if (suppressAnnotationHistory || snapshot == null)
            {
                return;
            }

            undoAnnotationHistory.Add(snapshot);
            if (undoAnnotationHistory.Count > AnnotationHistoryLimit)
            {
                undoAnnotationHistory.RemoveAt(0);
            }

            redoAnnotationHistory.Clear();
            if (markDirty)
            {
                MarkAnnotationsDirty(snapshot.ActionName);
            }
            RefreshAnnotationHistoryToolState();
        }

        private void ClearAnnotationHistory()
        {
            undoAnnotationHistory.Clear();
            redoAnnotationHistory.Clear();
            activeRoiEditHistoryOverlayId = string.Empty;
            RefreshAnnotationHistoryToolState();
        }

        private void RefreshAnnotationHistoryToolState()
        {
            string undoActionName = undoAnnotationHistory.Count > 0
                ? NormalizeHistoryActionName(undoAnnotationHistory[undoAnnotationHistory.Count - 1].ActionName)
                : string.Empty;
            string redoActionName = redoAnnotationHistory.Count > 0
                ? NormalizeHistoryActionName(redoAnnotationHistory[redoAnnotationHistory.Count - 1].ActionName)
                : string.Empty;
            LearningWorkflowViewModel?.SetAnnotationHistoryState(
                undoAnnotationHistory.Count > 0,
                redoAnnotationHistory.Count > 0,
                undoActionName,
                redoActionName);
        }

        private static string NormalizeHistoryActionName(string actionName)
        {
            string normalized = actionName ?? string.Empty;
            if (normalized.StartsWith("Undo ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(5);
            }

            if (normalized.StartsWith("Redo ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(5);
            }

            return normalized;
        }

        private bool UndoWpfAnnotationHistory()
        {
            if (undoAnnotationHistory.Count == 0)
            {
                SetYoloCommandStatus("되돌릴 편집 이력이 없습니다.", isBusy: false);
                return false;
            }

            WpfAnnotationHistorySnapshot target = undoAnnotationHistory[undoAnnotationHistory.Count - 1];
            undoAnnotationHistory.RemoveAt(undoAnnotationHistory.Count - 1);
            redoAnnotationHistory.Add(CaptureAnnotationHistory($"Redo {target.ActionName}"));
            RestoreAnnotationHistorySnapshot(target);
            SetYoloCommandStatus($"Undo: {target.ActionName}", isBusy: false);
            AppendLog($"Undo: {target.ActionName}");
            MarkAnnotationsDirty($"Undo {target.ActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private bool RedoWpfAnnotationHistory()
        {
            if (redoAnnotationHistory.Count == 0)
            {
                SetYoloCommandStatus("다시 실행할 편집 이력이 없습니다.", isBusy: false);
                return false;
            }

            WpfAnnotationHistorySnapshot target = redoAnnotationHistory[redoAnnotationHistory.Count - 1];
            redoAnnotationHistory.RemoveAt(redoAnnotationHistory.Count - 1);
            undoAnnotationHistory.Add(CaptureAnnotationHistory($"Undo {target.ActionName}"));
            if (undoAnnotationHistory.Count > AnnotationHistoryLimit)
            {
                undoAnnotationHistory.RemoveAt(0);
            }

            RestoreAnnotationHistorySnapshot(target);
            SetYoloCommandStatus($"Redo: {target.ActionName}", isBusy: false);
            AppendLog($"Redo: {target.ActionName}");
            MarkAnnotationsDirty($"Redo {target.ActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private void RestoreAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
        {
            suppressAnnotationHistory = true;
            try
            {
                WpfAnnotationHistoryService.Restore(
                    snapshot,
                    manualRois,
                    manualRoiClassNames,
                    manualRoiShapeKinds,
                    manualRoiOverlayIds,
                    manualSegments,
                    pendingDetectionCandidates,
                    confirmedDetectionCandidates);

                activeRoiEditHistoryOverlayId = string.Empty;
                polygonAnnotationService.Reset();
                lastMaskStrokePoint = null;
                activeMaskStrokeSnapshot = null;
                activeMaskStrokeChanged = false;
                EnsureManualRoiMetadataCount();
                RefreshPolygonOverlays();
                RefreshObjectList();
                RefreshCandidateList();
                RedrawReviewRois();
                PopulateClassList();
                UpdateDetectionResultOverlay();
                RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
                SetPythonStatus($"Python: 대기 {pendingDetectionCandidates.Count} / 확정 {confirmedDetectionCandidates.Count}");
            }
            finally
            {
                suppressAnnotationHistory = false;
            }
        }

        private DrawingRectangle ConvertCanvasRectToImageBounds(CanvasRect<float> rect)
        {
            if (rect == null || rect.IsEmpty() || activeImageSize.IsEmpty)
            {
                return DrawingRectangle.Empty;
            }

            var raw = new DrawingRectangle(
                (int)Math.Round(rect.Left),
                (int)Math.Round(activeImageSize.Height - rect.Top),
                (int)Math.Round(rect.Width),
                (int)Math.Round(rect.Height));

            return DrawingRectangle.Intersect(
                raw,
                new DrawingRectangle(0, 0, activeImageSize.Width, activeImageSize.Height));
        }

        private int FindManualRoiIndexByOverlayId(string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId))
            {
                return -1;
            }

            for (int i = 0; i < manualRoiOverlayIds.Count; i++)
            {
                if (string.Equals(manualRoiOverlayIds[i], overlayId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private CanvasRoiShapeKind GetManualRoiShapeKind(int index)
        {
            return index >= 0 && index < manualRoiShapeKinds.Count
                ? manualRoiShapeKinds[index]
                : CanvasRoiShapeKind.Rectangle;
        }

        private string GetManualRoiOverlayId(int index)
            => index >= 0 && index < manualRoiOverlayIds.Count
                ? manualRoiOverlayIds[index] ?? string.Empty
                : string.Empty;

        private void EnsureManualRoiMetadataCount()
        {
            while (manualRoiShapeKinds.Count < manualRois.Count)
            {
                manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
            }

            while (manualRoiOverlayIds.Count < manualRois.Count)
            {
                manualRoiOverlayIds.Add(string.Empty);
            }
        }

        private static void RemoveAtIfPresent<T>(IList<T> items, int index)
        {
            if (items != null && index >= 0 && index < items.Count)
            {
                items.RemoveAt(index);
            }
        }

        private static string FormatManualRoiShapeName(CanvasRoiShapeKind shapeKind)
            => shapeKind == CanvasRoiShapeKind.Ellipse ? "타원" : "박스";

        public void FocusYoloSettingsTab()
        {
            YoloSettingsReviewTab.IsSelected = true;
            YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            TrainingSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            UpdateLayout();
            YoloSettingsScrollViewer?.ScrollToTop();
        }

        public void FocusClassCatalogTab()
        {
            ClassesReviewTab.IsSelected = true;
            PopulateClassList(GetSelectedClassName());
            UpdateLayout();
        }

        private WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => LearningWorkflowPanelControl?.ViewModel;
        private ListBox LearningModeListBox => LearningWorkflowPanelControl?.ModeList;
        private ListBox AnnotationToolListBox => LearningWorkflowPanelControl?.ToolList;
        private ListBox LearningStepListBox => LearningWorkflowPanelControl?.StepList;
        private Expander LearningConceptsExpander => LearningWorkflowPanelControl?.LearningConcepts;
        private TextBlock GroundTruthChipText => LearningWorkflowPanelControl?.GroundTruthTextBlock;
        private TextBlock PredictionChipText => LearningWorkflowPanelControl?.PredictionTextBlock;
        private ItemsControl YoloTrainingWorkflowItemsControl => LearningWorkflowPanelControl?.YoloTrainingWorkflowList;
        private TextBlock YoloTrainingWorkflowSummaryText => LearningWorkflowPanelControl?.YoloTrainingWorkflowSummary;
        private TextBlock YoloTrainingChecklistStatusText => LearningWorkflowPanelControl?.YoloTrainingChecklistStatus;
        private TextBlock YoloTrainingChecklistDetailText => LearningWorkflowPanelControl?.YoloTrainingChecklistDetail;
        private TextBlock YoloTrainingChecklistActionText => LearningWorkflowPanelControl?.YoloTrainingChecklistAction;
        private TextBlock YoloTrainingHistoryText => LearningWorkflowPanelControl?.YoloTrainingHistory;
        private ItemsControl YoloTrainingRunHistoryItemsControl => LearningWorkflowPanelControl?.YoloTrainingRunHistoryList;
        private Button YoloFixClassesButton => LearningWorkflowPanelControl?.YoloFixClasses;
        private Button YoloFixLabelsButton => LearningWorkflowPanelControl?.YoloFixLabels;
        private Button YoloFixDatasetButton => LearningWorkflowPanelControl?.YoloFixDataset;
        private Button TutorialOpenHtmlGuideButton => LearningWorkflowPanelControl?.TutorialOpenHtmlGuide;
        private ComboBox ImageQueueFilterBox => ImageQueuePanelControl?.FilterBox;
        private TextBox ImageQueueSearchBox => ImageQueuePanelControl?.SearchBox;
        private DataGrid ImageQueueGrid => ImageQueuePanelControl?.QueueGrid;
        private TextBlock BatchStatusText => ImageQueuePanelControl?.BatchStatusTextBlock;
        private ProgressBar BatchProgressBar => ImageQueuePanelControl?.BatchProgress;
        private Wpf.Ui.Controls.Button OpenSelectedQueueImageButton => ImageQueuePanelControl?.OpenSelectedButton;
        private Wpf.Ui.Controls.Button DetectSelectedQueueButton => ImageQueuePanelControl?.DetectSelectedButton;
        private Wpf.Ui.Controls.Button BatchDetectQueueButton => ImageQueuePanelControl?.BatchDetectButton;
        private Wpf.Ui.Controls.Button RetryFailedQueueButton => ImageQueuePanelControl?.RetryFailedButton;
        private Wpf.Ui.Controls.Button StopBatchQueueButton => ImageQueuePanelControl?.StopBatchButton;
        private Wpf.Ui.Controls.Button QueueFilterAllButton => ImageQueuePanelControl?.QueueFilterAll;
        private Wpf.Ui.Controls.Button QueueFilterCandidateButton => ImageQueuePanelControl?.QueueFilterCandidate;
        private Wpf.Ui.Controls.Button QueueFilterFailedButton => ImageQueuePanelControl?.QueueFilterFailed;
        private Wpf.Ui.Controls.Button QueueFilterConfirmedButton => ImageQueuePanelControl?.QueueFilterConfirmed;
        private Wpf.Ui.Controls.Button QueueFilterSkippedButton => ImageQueuePanelControl?.QueueFilterSkipped;
        private Wpf.Ui.Controls.Button QueueFilterNoCandidateButton => ImageQueuePanelControl?.QueueFilterNoCandidate;
        private TextBlock QueueFilterAllText => ImageQueuePanelControl?.QueueFilterAllTextBlock;
        private TextBlock QueueFilterCandidateText => ImageQueuePanelControl?.QueueFilterCandidateTextBlock;
        private TextBlock QueueFilterFailedText => ImageQueuePanelControl?.QueueFilterFailedTextBlock;
        private TextBlock QueueFilterConfirmedText => ImageQueuePanelControl?.QueueFilterConfirmedTextBlock;
        private TextBlock QueueFilterSkippedText => ImageQueuePanelControl?.QueueFilterSkippedTextBlock;
        private TextBlock QueueFilterNoCandidateText => ImageQueuePanelControl?.QueueFilterNoCandidateTextBlock;
        private RoiImageCanvasView MainCanvasView => CanvasPanelControl?.MainCanvas;
        private Wpf.Ui.Controls.Button FitCanvasButton => CanvasPanelControl?.FitButton;
        private Wpf.Ui.Controls.Button ActualSizeCanvasButton => CanvasPanelControl?.ActualSizeButton;
        private Wpf.Ui.Controls.Button PanCanvasButton => CanvasPanelControl?.PanButton;
        private Wpf.Ui.Controls.Button FocusCandidateCanvasButton => CanvasPanelControl?.FocusCandidateButton;
        private Wpf.Ui.Controls.Button ResetAiOverlayCanvasButton => CanvasPanelControl?.ResetAiOverlayButton;
        private Border DetectionResultOverlay => CanvasPanelControl?.ResultOverlay;
        private TextBlock DetectionOverlayTitleText => CanvasPanelControl?.OverlayTitleText;
        private TextBlock DetectionOverlaySummaryText => CanvasPanelControl?.OverlaySummaryText;
        private Border DetectionOverlaySelectedBorder => CanvasPanelControl?.OverlaySelectedBorder;
        private TextBlock DetectionOverlaySelectedText => CanvasPanelControl?.OverlaySelectedText;
        private TextBlock DetectionOverlayDetailText => CanvasPanelControl?.OverlayDetailText;
        private TextBlock ObjectReviewSummaryText => ObjectReviewPanelControl?.SummaryTextBlock;
        private Wpf.Ui.Controls.Button DeleteObjectButton => ObjectReviewPanelControl?.DeleteButton;
        private ComboBox ObjectClassBox => ObjectReviewPanelControl?.ClassBox;
        private Wpf.Ui.Controls.Button ApplyObjectClassButton => ObjectReviewPanelControl?.ApplyClassButton;
        private ListBox ObjectListBox => ObjectReviewPanelControl?.ObjectList;
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => ObjectReviewPanelControl?.ViewModel;
        private Slider CandidateConfidenceSlider => CandidateReviewPanelControl?.ConfidenceSlider;
        private TextBlock CandidateConfidenceText => CandidateReviewPanelControl?.ConfidenceTextBlock;
        private TextBlock CandidateDetailText => CandidateReviewPanelControl?.DetailTextBlock;
        private Border SelectedCandidateSummaryPanel => CandidateReviewPanelControl?.SelectedCandidateSummary;
        private TextBlock SelectedCandidateSummaryText => CandidateReviewPanelControl?.SelectedCandidateSummaryTextBlock;
        private Border CandidateComparisonPanel => CandidateReviewPanelControl?.ComparisonPanel;
        private TextBlock CandidateCompareCandidateText => CandidateReviewPanelControl?.CompareCandidateText;
        private TextBlock CandidateCompareCurrentText => CandidateReviewPanelControl?.CompareCurrentText;
        private TextBlock CandidateCompareOverlapText => CandidateReviewPanelControl?.CompareOverlapText;
        private Wpf.Ui.Controls.Button ConfirmSelectedCandidateButton => CandidateReviewPanelControl?.ConfirmSelectedButton;
        private Wpf.Ui.Controls.Button ConfirmAllCandidatesButton => CandidateReviewPanelControl?.ConfirmAllButton;
        private Wpf.Ui.Controls.Button SkipSelectedCandidateButton => CandidateReviewPanelControl?.SkipSelectedButton;
        private Wpf.Ui.Controls.Button PreviousCandidateButton => CandidateReviewPanelControl?.PreviousCandidate;
        private Wpf.Ui.Controls.Button NextCandidateButton => CandidateReviewPanelControl?.NextCandidate;
        private Wpf.Ui.Controls.Button FocusCandidateButton => CandidateReviewPanelControl?.FocusCandidate;
        private ListBox CandidateListBox => CandidateReviewPanelControl?.CandidateList;
        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel => CandidateReviewPanelControl?.ViewModel;
        private TextBox ClassNameBox => ClassCatalogPanelControl?.ClassNameTextBox;
        private Wpf.Ui.Controls.Button AddClassButton => ClassCatalogPanelControl?.AddClass;
        private Wpf.Ui.Controls.Button RemoveClassButton => ClassCatalogPanelControl?.RemoveClass;
        private TextBox OutputRootPathBox => ClassCatalogPanelControl?.OutputRootPathTextBox;
        private Wpf.Ui.Controls.Button BrowseOutputRootButton => ClassCatalogPanelControl?.BrowseOutputRoot;
        private Wpf.Ui.Controls.Button SaveOutputRootButton => ClassCatalogPanelControl?.SaveOutputRoot;
        private TextBlock ClassEditStatusText => ClassCatalogPanelControl?.StatusTextBlock;
        private ListBox ClassListBox => ClassCatalogPanelControl?.ClassList;
        private WpfClassCatalogPanelViewModel ClassCatalogViewModel => ClassCatalogPanelControl?.ViewModel;
        private TextBlock YoloSettingsSummaryText => YoloStatusPanelControl?.SummaryTextBlock;
        private TextBlock YoloSettingsDetailText => YoloStatusPanelControl?.DetailTextBlock;
        private Wpf.Ui.Controls.Button FirstCheckYoloButton => YoloStatusPanelControl?.FirstCheckButton;
        private Wpf.Ui.Controls.Button InstallRequirementsButton => YoloStatusPanelControl?.InstallRequirements;
        private Wpf.Ui.Controls.Button RunYoloSmokeButton => YoloStatusPanelControl?.RunSmokeButton;
        private Wpf.Ui.Controls.Button RestartPythonWorkerButton => YoloStatusPanelControl?.RestartWorkerButton;
        private Wpf.Ui.Controls.Button StopPythonWorkerButton => YoloStatusPanelControl?.StopWorkerButton;
        private TextBlock YoloCommandStatusText => YoloStatusPanelControl?.CommandStatusTextBlock;
        private ProgressBar YoloCommandProgressBar => YoloStatusPanelControl?.CommandProgress;
        private WpfYoloStatusPanelViewModel YoloStatusViewModel => YoloStatusPanelControl?.ViewModel;
        private Expander ProjectConfigExpander => ProjectConfigPanelControl?.SettingsExpander;
        private TextBox ProjectRecipeNameBox => ProjectConfigPanelControl?.RecipeNameBox;
        private ComboBox ProjectRecipeListBox => ProjectConfigPanelControl?.RecipeListBox;
        private TextBox ProjectConfigPathBox => ProjectConfigPanelControl?.ConfigPathBox;
        private TextBlock ProjectConfigStatusText => ProjectConfigPanelControl?.StatusTextBlock;
        private Wpf.Ui.Controls.Button ApplyProjectRecipeButton => ProjectConfigPanelControl?.ApplyRecipeButton;
        private Wpf.Ui.Controls.Button RefreshProjectRecipeListButton => ProjectConfigPanelControl?.RefreshRecipeListButton;
        private Wpf.Ui.Controls.Button SaveProjectConfigButton => ProjectConfigPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button OpenProjectConfigFolderButton => ProjectConfigPanelControl?.OpenFolderButton;
        private WpfProjectConfigPanelViewModel ProjectConfigViewModel => ProjectConfigPanelControl?.ViewModel;
        private TextBox YoloPythonPathBox => YoloModelSettingsPanelControl?.PythonPathBox;
        private TextBox YoloProjectRootBox => YoloModelSettingsPanelControl?.ProjectRootBox;
        private TextBox YoloClientScriptBox => YoloModelSettingsPanelControl?.ClientScriptBox;
        private TextBox YoloWeightsPathBox => YoloModelSettingsPanelControl?.WeightsPathBox;
        private TextBox YoloImageRootBox => YoloModelSettingsPanelControl?.ImageRootBox;
        private TextBox YoloConfidenceBox => YoloModelSettingsPanelControl?.ConfidenceBox;
        private TextBox YoloInferenceImageSizeBox => YoloModelSettingsPanelControl?.InferenceImageSizeBox;
        private TextBox YoloMaxCandidatesBox => YoloModelSettingsPanelControl?.MaxCandidatesBox;
        private TextBox YoloTimeoutBox => YoloModelSettingsPanelControl?.TimeoutBox;
        private CheckBox YoloAutoStartCheckBox => YoloModelSettingsPanelControl?.AutoStartCheckBox;
        private Wpf.Ui.Controls.Button BrowseYoloPythonButton => YoloModelSettingsPanelControl?.BrowsePythonButton;
        private Wpf.Ui.Controls.Button BrowseYoloProjectRootButton => YoloModelSettingsPanelControl?.BrowseProjectRootButton;
        private Wpf.Ui.Controls.Button BrowseYoloClientScriptButton => YoloModelSettingsPanelControl?.BrowseClientScriptButton;
        private Wpf.Ui.Controls.Button BrowseYoloWeightsButton => YoloModelSettingsPanelControl?.BrowseWeightsButton;
        private Wpf.Ui.Controls.Button BrowseYoloImageRootButton => YoloModelSettingsPanelControl?.BrowseImageRootButton;
        private Wpf.Ui.Controls.Button SaveYoloSettingsButton => YoloModelSettingsPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button ResetYoloSettingsButton => YoloModelSettingsPanelControl?.ResetButton;
        private WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel => YoloModelSettingsPanelControl?.ViewModel;
        private Expander TrainingSettingsExpander => TrainingSettingsPanelControl?.SettingsExpander;
        private TextBox TrainingImageSizeBox => TrainingSettingsPanelControl?.ImageSizeBox;
        private TextBox TrainingBatchBox => TrainingSettingsPanelControl?.BatchBox;
        private TextBox TrainingEpochBox => TrainingSettingsPanelControl?.EpochBox;
        private ComboBox TrainingCfgBox => TrainingSettingsPanelControl?.CfgBox;
        private ComboBox TrainingWeightBox => TrainingSettingsPanelControl?.WeightBox;
        private TextBox TrainingValidationPercentBox => TrainingSettingsPanelControl?.ValidationPercentBox;
        private TextBox TrainingTestPercentBox => TrainingSettingsPanelControl?.TestPercentBox;
        private TextBox TrainingSplitSeedBox => TrainingSettingsPanelControl?.SplitSeedBox;
        private TextBlock TrainingSplitPolicyHintText => TrainingSettingsPanelControl?.SplitPolicyHintTextBlock;
        private Wpf.Ui.Controls.Button RefreshTrainingReadinessButton => TrainingSettingsPanelControl?.RefreshReadinessButton;
        private Wpf.Ui.Controls.Button StartTrainingButton => TrainingSettingsPanelControl?.StartButton;
        private Wpf.Ui.Controls.Button StopTrainingButton => TrainingSettingsPanelControl?.StopButton;
        private TextBlock TrainingReadinessText => TrainingSettingsPanelControl?.ReadinessTextBlock;
        private ProgressBar TrainingProgressBar => TrainingSettingsPanelControl?.Progress;
        private TextBlock TrainingProgressText => TrainingSettingsPanelControl?.ProgressTextBlock;
        private TextBlock TrainingEpochText => TrainingSettingsPanelControl?.EpochTextBlock;
        private WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel => TrainingSettingsPanelControl?.ViewModel;
        private TextBlock DatasetStatusText => StatusBarPanelControl?.DatasetStatusTextBlock;
        private TextBlock PythonStatusText => StatusBarPanelControl?.PythonStatusTextBlock;
        private TextBlock AnnotationSaveStatusText => StatusBarPanelControl?.AnnotationSaveStatusTextBlock;
        private TextBlock ModelStatusText => StatusBarPanelControl?.ModelStatusTextBlock;
        private WpfStatusBarPanelViewModel StatusBarViewModel => StatusBarPanelControl?.ViewModel;
        private FrameworkElement ShellLogPanel => ShellLogPanelControl?.LogPanel;

        private enum ShellTheme
        {
            Dark,
            Light
        }

        private enum WorkflowMode
        {
            Labeling,
            Inference
        }

        public sealed class ImageDecodeCacheDiagnostics
        {
            public ImageDecodeCacheDiagnostics(int count, long bytes, long hits, long misses, long stores, long evictions, int capacity, long maxBytes)
            {
                Count = count;
                Bytes = bytes;
                Hits = hits;
                Misses = misses;
                Stores = stores;
                Evictions = evictions;
                Capacity = capacity;
                MaxBytes = maxBytes;
            }

            public int Count { get; }

            public long Bytes { get; }

            public long Hits { get; }

            public long Misses { get; }

            public long Stores { get; }

            public long Evictions { get; }

            public int Capacity { get; }

            public long MaxBytes { get; }
        }

        public sealed class ImageLoadDiagnostics
        {
            public static readonly ImageLoadDiagnostics Empty = new ImageLoadDiagnostics(
                string.Empty,
                false,
                0D,
                0D,
                0D,
                0D,
                0D,
                0D,
                0D,
                0D,
                0D);

            public ImageLoadDiagnostics(
                string imagePath,
                bool cacheHit,
                double totalMilliseconds,
                double decodeMilliseconds,
                double canvasUploadMilliseconds,
                double canvasRefreshMilliseconds,
                double stateTransferMilliseconds,
                double annotationResetMilliseconds,
                double queuePopulateMilliseconds,
                double reviewRefreshMilliseconds,
                double preloadScheduleMilliseconds)
            {
                ImagePath = imagePath ?? string.Empty;
                CacheHit = cacheHit;
                TotalMilliseconds = totalMilliseconds;
                DecodeMilliseconds = decodeMilliseconds;
                CanvasUploadMilliseconds = canvasUploadMilliseconds;
                CanvasRefreshMilliseconds = canvasRefreshMilliseconds;
                StateTransferMilliseconds = stateTransferMilliseconds;
                AnnotationResetMilliseconds = annotationResetMilliseconds;
                QueuePopulateMilliseconds = queuePopulateMilliseconds;
                ReviewRefreshMilliseconds = reviewRefreshMilliseconds;
                PreloadScheduleMilliseconds = preloadScheduleMilliseconds;
            }

            public string ImagePath { get; }

            public bool CacheHit { get; }

            public double TotalMilliseconds { get; }

            public double DecodeMilliseconds { get; }

            public double CanvasUploadMilliseconds { get; }

            public double CanvasRefreshMilliseconds { get; }

            public double StateTransferMilliseconds { get; }

            public double AnnotationResetMilliseconds { get; }

            public double QueuePopulateMilliseconds { get; }

            public double ReviewRefreshMilliseconds { get; }

            public double PreloadScheduleMilliseconds { get; }
        }

        public ImageLoadDiagnostics LastImageLoadDiagnostics => lastImageLoadDiagnostics;

        private sealed class CachedDecodedImage : IDisposable
        {
            public CachedDecodedImage(string imagePath, DrawingBitmap bitmap, CvMat mat)
            {
                ImagePath = imagePath ?? string.Empty;
                Bitmap = bitmap;
                Mat = mat;
                EstimatedBytes = EstimateBytes(bitmap, mat);
            }

            public string ImagePath { get; }

            public DrawingBitmap Bitmap { get; private set; }

            public CvMat Mat { get; private set; }

            public long EstimatedBytes { get; }

            public DrawingBitmap TakeBitmap()
            {
                DrawingBitmap bitmap = Bitmap;
                Bitmap = null;
                return bitmap;
            }

            public CvMat TakeMat()
            {
                CvMat mat = Mat;
                Mat = null;
                return mat;
            }

            public void Dispose()
            {
                Bitmap?.Dispose();
                Mat?.Dispose();
                Bitmap = null;
                Mat = null;
            }

            private static long EstimateBytes(DrawingBitmap bitmap, CvMat mat)
            {
                long bitmapBytes = bitmap == null
                    ? 0L
                    : (long)bitmap.Width * bitmap.Height * 3L;
                long matBytes = mat == null
                    ? 0L
                    : (long)mat.Rows * mat.Cols * Math.Max(1, mat.Channels());
                return bitmapBytes + matBytes;
            }
        }

        private void AttachLearningWorkflowPanelEvents()
        {
            if (learningWorkflowPanelEventsAttached || LearningWorkflowPanelControl == null)
            {
                return;
            }

            learningWorkflowPanelEventsAttached = true;
            LearningWorkflowPanelControl.LearningModeSelectionChanged += LearningWorkflowModeListBox_SelectionChanged;
            LearningWorkflowPanelControl.AnnotationToolSelectionChanged += AnnotationToolListBox_SelectionChanged;
            LearningWorkflowPanelControl.LearningStepSelectionChanged += LearningStepListBox_SelectionChanged;
            LearningWorkflowPanelControl.YoloTrainingWorkflowStepRequested += YoloTrainingWorkflowStep_Requested;
            LearningWorkflowPanelControl.TutorialOpenHtmlGuideRequested += TutorialOpenHtmlGuideButton_Click;
            LearningWorkflowPanelControl.YoloFixClassesRequested += YoloFixClassesButton_Click;
            LearningWorkflowPanelControl.YoloFixLabelsRequested += YoloFixLabelsButton_Click;
            LearningWorkflowPanelControl.YoloFixDatasetRequested += YoloFixDatasetButton_Click;
        }

        private void RegisterLearningWorkflowPanelNames()
        {
            AttachLearningWorkflowPanelEvents();
            RegisterLearningWorkflowName(nameof(LearningModeListBox), LearningModeListBox);
            RegisterLearningWorkflowName(nameof(AnnotationToolListBox), AnnotationToolListBox);
            RegisterLearningWorkflowName(nameof(LearningStepListBox), LearningStepListBox);
            RegisterLearningWorkflowName(nameof(LearningConceptsExpander), LearningConceptsExpander);
            RegisterLearningWorkflowName(nameof(GroundTruthChipText), GroundTruthChipText);
            RegisterLearningWorkflowName(nameof(PredictionChipText), PredictionChipText);
            RegisterLearningWorkflowName(nameof(YoloTrainingWorkflowItemsControl), YoloTrainingWorkflowItemsControl);
            RegisterLearningWorkflowName(nameof(YoloTrainingWorkflowSummaryText), YoloTrainingWorkflowSummaryText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistStatusText), YoloTrainingChecklistStatusText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistDetailText), YoloTrainingChecklistDetailText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistActionText), YoloTrainingChecklistActionText);
            RegisterLearningWorkflowName(nameof(YoloTrainingHistoryText), YoloTrainingHistoryText);
            RegisterLearningWorkflowName(nameof(YoloTrainingRunHistoryItemsControl), YoloTrainingRunHistoryItemsControl);
            RegisterLearningWorkflowName(nameof(TutorialOpenHtmlGuideButton), TutorialOpenHtmlGuideButton);
            RegisterLearningWorkflowName(nameof(YoloFixClassesButton), YoloFixClassesButton);
            RegisterLearningWorkflowName(nameof(YoloFixLabelsButton), YoloFixLabelsButton);
            RegisterLearningWorkflowName(nameof(YoloFixDatasetButton), YoloFixDatasetButton);
        }

        private void RegisterLearningWorkflowName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void InitializeImageQueuePanel()
        {
            AttachImageQueuePanelEvents();
            ImageQueueFilterBox.ItemsSource = WpfImageQueueFilterOption.CreateDefaults();
            ImageQueueFilterBox.SelectedIndex = 0;
            imageQueueView = CollectionViewSource.GetDefaultView(imageQueueItems);
            imageQueueView.Filter = item => ShouldShowImageQueueItem(item as WpfImageQueueItem);
            ImageQueueGrid.ItemsSource = imageQueueView;
            UpdateQueueQuickFilterButtons();
        }

        private void AttachImageQueuePanelEvents()
        {
            if (imageQueuePanelEventsAttached || ImageQueuePanelControl == null)
            {
                return;
            }

            imageQueuePanelEventsAttached = true;
            ImageQueuePanelControl.LoadImageRootRequested += LoadImageRootButton_Click;
            ImageQueuePanelControl.BrowseImageFolderRequested += BrowseImageFolderButton_Click;
            ImageQueuePanelControl.RefreshImageQueueRequested += RefreshImageQueueButton_Click;
            ImageQueuePanelControl.NextUnlabeledRequested += NextUnlabeledButton_Click;
            ImageQueuePanelControl.OpenSelectedQueueImageRequested += OpenSelectedQueueImageButton_Click;
            ImageQueuePanelControl.DetectSelectedQueueRequested += DetectSelectedQueueButton_Click;
            ImageQueuePanelControl.BatchDetectQueueRequested += BatchDetectQueueButton_Click;
            ImageQueuePanelControl.RetryFailedQueueRequested += RetryFailedQueueButton_Click;
            ImageQueuePanelControl.StopBatchQueueRequested += StopBatchQueueButton_Click;
            ImageQueuePanelControl.QueueFilterAllRequested += QueueFilterAllButton_Click;
            ImageQueuePanelControl.QueueFilterCandidateRequested += QueueFilterCandidateButton_Click;
            ImageQueuePanelControl.QueueFilterFailedRequested += QueueFilterFailedButton_Click;
            ImageQueuePanelControl.QueueFilterConfirmedRequested += QueueFilterConfirmedButton_Click;
            ImageQueuePanelControl.QueueFilterSkippedRequested += QueueFilterSkippedButton_Click;
            ImageQueuePanelControl.QueueFilterNoCandidateRequested += QueueFilterNoCandidateButton_Click;
            ImageQueuePanelControl.FilterSelectionChanged += ImageQueueFilterBox_SelectionChanged;
            ImageQueuePanelControl.SearchTextChanged += ImageQueueSearchBox_TextChanged;
            ImageQueuePanelControl.QueueSelectionChanged += ImageQueueGrid_SelectionChanged;
            ImageQueuePanelControl.QueueMouseDoubleClick += ImageQueueGrid_MouseDoubleClick;
        }

        private void RegisterImageQueuePanelNames()
        {
            RegisterImageQueueName(nameof(ImageQueueFilterBox), ImageQueueFilterBox);
            RegisterImageQueueName(nameof(ImageQueueSearchBox), ImageQueueSearchBox);
            RegisterImageQueueName(nameof(ImageQueueGrid), ImageQueueGrid);
            RegisterImageQueueName(nameof(BatchStatusText), BatchStatusText);
            RegisterImageQueueName(nameof(BatchProgressBar), BatchProgressBar);
            RegisterImageQueueName(nameof(OpenSelectedQueueImageButton), OpenSelectedQueueImageButton);
            RegisterImageQueueName(nameof(DetectSelectedQueueButton), DetectSelectedQueueButton);
            RegisterImageQueueName(nameof(BatchDetectQueueButton), BatchDetectQueueButton);
            RegisterImageQueueName(nameof(RetryFailedQueueButton), RetryFailedQueueButton);
            RegisterImageQueueName(nameof(StopBatchQueueButton), StopBatchQueueButton);
            RegisterImageQueueName(nameof(QueueFilterAllButton), QueueFilterAllButton);
            RegisterImageQueueName(nameof(QueueFilterCandidateButton), QueueFilterCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterFailedButton), QueueFilterFailedButton);
            RegisterImageQueueName(nameof(QueueFilterConfirmedButton), QueueFilterConfirmedButton);
            RegisterImageQueueName(nameof(QueueFilterSkippedButton), QueueFilterSkippedButton);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateButton), QueueFilterNoCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterAllText), QueueFilterAllText);
            RegisterImageQueueName(nameof(QueueFilterCandidateText), QueueFilterCandidateText);
            RegisterImageQueueName(nameof(QueueFilterFailedText), QueueFilterFailedText);
            RegisterImageQueueName(nameof(QueueFilterConfirmedText), QueueFilterConfirmedText);
            RegisterImageQueueName(nameof(QueueFilterSkippedText), QueueFilterSkippedText);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateText), QueueFilterNoCandidateText);
        }

        private void RegisterImageQueueName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachCanvasPanelEvents()
        {
            if (canvasPanelEventsAttached || CanvasPanelControl == null)
            {
                return;
            }

            canvasPanelEventsAttached = true;
            CanvasPanelControl.FitRequested += FitCanvasButton_Click;
            CanvasPanelControl.ActualSizeRequested += ActualSizeCanvasButton_Click;
            CanvasPanelControl.PanRequested += PanCanvasButton_Click;
            CanvasPanelControl.FocusCandidateRequested += FocusCandidateButton_Click;
            CanvasPanelControl.ResetAiOverlayRequested += ResetAiOverlayCanvasButton_Click;
        }

        private void RegisterCanvasPanelNames()
        {
            AttachCanvasPanelEvents();
            RegisterCanvasName(nameof(MainCanvasView), MainCanvasView);
            RegisterCanvasName(nameof(FitCanvasButton), FitCanvasButton);
            RegisterCanvasName(nameof(ActualSizeCanvasButton), ActualSizeCanvasButton);
            RegisterCanvasName(nameof(PanCanvasButton), PanCanvasButton);
            RegisterCanvasName(nameof(FocusCandidateCanvasButton), FocusCandidateCanvasButton);
            RegisterCanvasName(nameof(ResetAiOverlayCanvasButton), ResetAiOverlayCanvasButton);
            RegisterCanvasName(nameof(DetectionResultOverlay), DetectionResultOverlay);
            RegisterCanvasName(nameof(DetectionOverlayTitleText), DetectionOverlayTitleText);
            RegisterCanvasName(nameof(DetectionOverlaySummaryText), DetectionOverlaySummaryText);
            RegisterCanvasName(nameof(DetectionOverlaySelectedBorder), DetectionOverlaySelectedBorder);
            RegisterCanvasName(nameof(DetectionOverlaySelectedText), DetectionOverlaySelectedText);
            RegisterCanvasName(nameof(DetectionOverlayDetailText), DetectionOverlayDetailText);
        }

        private void RegisterCanvasName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachObjectReviewPanelEvents()
        {
            if (objectReviewPanelEventsAttached || ObjectReviewPanelControl == null)
            {
                return;
            }

            objectReviewPanelEventsAttached = true;
            ObjectReviewPanelControl.DeleteObjectRequested += DeleteObjectButton_Click;
            ObjectReviewPanelControl.ApplyObjectClassRequested += ApplyObjectClassButton_Click;
            ObjectReviewPanelControl.ObjectSelectionChanged += ObjectListBox_SelectionChanged;
            ObjectReviewPanelControl.ObjectPreviewKeyDown += ObjectListBox_PreviewKeyDown;
        }

        private void RegisterObjectReviewPanelNames()
        {
            AttachObjectReviewPanelEvents();
            RegisterObjectReviewName(nameof(ObjectReviewSummaryText), ObjectReviewSummaryText);
            RegisterObjectReviewName(nameof(DeleteObjectButton), DeleteObjectButton);
            RegisterObjectReviewName(nameof(ObjectClassBox), ObjectClassBox);
            RegisterObjectReviewName(nameof(ApplyObjectClassButton), ApplyObjectClassButton);
            RegisterObjectReviewName(nameof(ObjectListBox), ObjectListBox);
        }

        private void RegisterObjectReviewName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachCandidateReviewPanelEvents()
        {
            if (candidateReviewPanelEventsAttached || CandidateReviewPanelControl == null)
            {
                return;
            }

            candidateReviewPanelEventsAttached = true;
            CandidateReviewPanelControl.ConfidenceChanged += CandidateConfidenceSlider_ValueChanged;
            CandidateReviewPanelControl.ConfirmSelectedRequested += ConfirmSelectedCandidateButton_Click;
            CandidateReviewPanelControl.ConfirmAllRequested += ConfirmAllCandidatesButton_Click;
            CandidateReviewPanelControl.SkipSelectedRequested += SkipSelectedCandidateButton_Click;
            CandidateReviewPanelControl.PreviousCandidateRequested += PreviousCandidateButton_Click;
            CandidateReviewPanelControl.NextCandidateRequested += NextCandidateButton_Click;
            CandidateReviewPanelControl.FocusCandidateRequested += FocusCandidateButton_Click;
            CandidateReviewPanelControl.CandidateSelectionChanged += CandidateListBox_SelectionChanged;
            CandidateReviewPanelControl.CandidatePreviewKeyDown += CandidateListBox_PreviewKeyDown;
        }

        private void RegisterCandidateReviewPanelNames()
        {
            AttachCandidateReviewPanelEvents();
            RegisterCandidateReviewName(nameof(CandidateConfidenceSlider), CandidateConfidenceSlider);
            RegisterCandidateReviewName(nameof(CandidateConfidenceText), CandidateConfidenceText);
            RegisterCandidateReviewName(nameof(CandidateDetailText), CandidateDetailText);
            RegisterCandidateReviewName(nameof(SelectedCandidateSummaryPanel), SelectedCandidateSummaryPanel);
            RegisterCandidateReviewName(nameof(SelectedCandidateSummaryText), SelectedCandidateSummaryText);
            RegisterCandidateReviewName(nameof(CandidateComparisonPanel), CandidateComparisonPanel);
            RegisterCandidateReviewName(nameof(CandidateCompareCandidateText), CandidateCompareCandidateText);
            RegisterCandidateReviewName(nameof(CandidateCompareCurrentText), CandidateCompareCurrentText);
            RegisterCandidateReviewName(nameof(CandidateCompareOverlapText), CandidateCompareOverlapText);
            RegisterCandidateReviewName(nameof(ConfirmSelectedCandidateButton), ConfirmSelectedCandidateButton);
            RegisterCandidateReviewName(nameof(ConfirmAllCandidatesButton), ConfirmAllCandidatesButton);
            RegisterCandidateReviewName(nameof(SkipSelectedCandidateButton), SkipSelectedCandidateButton);
            RegisterCandidateReviewName(nameof(PreviousCandidateButton), PreviousCandidateButton);
            RegisterCandidateReviewName(nameof(NextCandidateButton), NextCandidateButton);
            RegisterCandidateReviewName(nameof(FocusCandidateButton), FocusCandidateButton);
            RegisterCandidateReviewName(nameof(CandidateListBox), CandidateListBox);
        }

        private void RegisterCandidateReviewName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachClassCatalogPanelEvents()
        {
            if (classCatalogPanelEventsAttached || ClassCatalogPanelControl == null)
            {
                return;
            }

            classCatalogPanelEventsAttached = true;
            ClassCatalogPanelControl.ClassNameKeyDown += ClassNameBox_KeyDown;
            ClassCatalogPanelControl.AddClassRequested += AddClassButton_Click;
            ClassCatalogPanelControl.RemoveClassRequested += RemoveClassButton_Click;
            ClassCatalogPanelControl.BrowseOutputRootRequested += BrowseOutputRootButton_Click;
            ClassCatalogPanelControl.SaveOutputRootRequested += SaveOutputRootButton_Click;
            ClassCatalogPanelControl.ClassSelectionChanged += ClassListBox_SelectionChanged;
        }

        private void RegisterClassCatalogPanelNames()
        {
            AttachClassCatalogPanelEvents();
            RegisterClassCatalogName(nameof(ClassNameBox), ClassNameBox);
            RegisterClassCatalogName(nameof(AddClassButton), AddClassButton);
            RegisterClassCatalogName(nameof(RemoveClassButton), RemoveClassButton);
            RegisterClassCatalogName(nameof(OutputRootPathBox), OutputRootPathBox);
            RegisterClassCatalogName(nameof(BrowseOutputRootButton), BrowseOutputRootButton);
            RegisterClassCatalogName(nameof(SaveOutputRootButton), SaveOutputRootButton);
            RegisterClassCatalogName(nameof(ClassEditStatusText), ClassEditStatusText);
            RegisterClassCatalogName(nameof(ClassListBox), ClassListBox);
        }

        private void RegisterClassCatalogName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachYoloStatusPanelEvents()
        {
            if (yoloStatusPanelEventsAttached || YoloStatusPanelControl == null)
            {
                return;
            }

            yoloStatusPanelEventsAttached = true;
            YoloStatusPanelControl.CheckRequested += CheckYoloButton_Click;
            YoloStatusPanelControl.InstallRequirementsRequested += InstallRequirementsButton_Click;
            YoloStatusPanelControl.RunSmokeRequested += RunYoloSmokeButton_Click;
            YoloStatusPanelControl.RestartWorkerRequested += RestartPythonWorkerButton_Click;
            YoloStatusPanelControl.StopWorkerRequested += StopPythonWorkerButton_Click;
        }

        private void RegisterYoloStatusPanelNames()
        {
            AttachYoloStatusPanelEvents();
            RegisterYoloStatusName(nameof(YoloSettingsSummaryText), YoloSettingsSummaryText);
            RegisterYoloStatusName(nameof(YoloSettingsDetailText), YoloSettingsDetailText);
            RegisterYoloStatusName(nameof(FirstCheckYoloButton), FirstCheckYoloButton);
            RegisterYoloStatusName(nameof(InstallRequirementsButton), InstallRequirementsButton);
            RegisterYoloStatusName(nameof(RunYoloSmokeButton), RunYoloSmokeButton);
            RegisterYoloStatusName(nameof(RestartPythonWorkerButton), RestartPythonWorkerButton);
            RegisterYoloStatusName(nameof(StopPythonWorkerButton), StopPythonWorkerButton);
            RegisterYoloStatusName(nameof(YoloCommandStatusText), YoloCommandStatusText);
            RegisterYoloStatusName(nameof(YoloCommandProgressBar), YoloCommandProgressBar);
        }

        private void RegisterYoloStatusName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachProjectConfigPanelEvents()
        {
            if (projectConfigPanelEventsAttached || ProjectConfigPanelControl == null)
            {
                return;
            }

            projectConfigPanelEventsAttached = true;
            ProjectConfigPanelControl.ApplyRecipeRequested += ApplyProjectRecipeButton_Click;
            ProjectConfigPanelControl.RecipeSelectionChanged += ProjectRecipeListBox_SelectionChanged;
            ProjectConfigPanelControl.RefreshRecipeListRequested += RefreshProjectRecipeListButton_Click;
            ProjectConfigPanelControl.SaveRequested += SaveProjectConfigButton_Click;
            ProjectConfigPanelControl.OpenFolderRequested += OpenProjectConfigFolderButton_Click;
        }

        private void RegisterProjectConfigPanelNames()
        {
            AttachProjectConfigPanelEvents();
            RegisterProjectConfigName(nameof(ProjectConfigExpander), ProjectConfigExpander);
            RegisterProjectConfigName(nameof(ProjectRecipeNameBox), ProjectRecipeNameBox);
            RegisterProjectConfigName(nameof(ProjectRecipeListBox), ProjectRecipeListBox);
            RegisterProjectConfigName(nameof(ProjectConfigPathBox), ProjectConfigPathBox);
            RegisterProjectConfigName(nameof(ProjectConfigStatusText), ProjectConfigStatusText);
            RegisterProjectConfigName(nameof(ApplyProjectRecipeButton), ApplyProjectRecipeButton);
            RegisterProjectConfigName(nameof(RefreshProjectRecipeListButton), RefreshProjectRecipeListButton);
            RegisterProjectConfigName(nameof(SaveProjectConfigButton), SaveProjectConfigButton);
            RegisterProjectConfigName(nameof(OpenProjectConfigFolderButton), OpenProjectConfigFolderButton);
        }

        private void RegisterProjectConfigName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachYoloModelSettingsPanelEvents()
        {
            if (yoloModelSettingsPanelEventsAttached || YoloModelSettingsPanelControl == null)
            {
                return;
            }

            yoloModelSettingsPanelEventsAttached = true;
            YoloModelSettingsPanelControl.BrowsePythonRequested += BrowseYoloPythonButton_Click;
            YoloModelSettingsPanelControl.BrowseProjectRootRequested += BrowseYoloProjectRootButton_Click;
            YoloModelSettingsPanelControl.BrowseClientScriptRequested += BrowseYoloClientScriptButton_Click;
            YoloModelSettingsPanelControl.BrowseWeightsRequested += BrowseYoloWeightsButton_Click;
            YoloModelSettingsPanelControl.BrowseImageRootRequested += BrowseYoloImageRootButton_Click;
            YoloModelSettingsPanelControl.SaveRequested += SaveYoloSettingsButton_Click;
            YoloModelSettingsPanelControl.ResetRequested += ResetYoloSettingsButton_Click;
            YoloModelSettingsPanelControl.DecimalTextInputPreview += DecimalTextBox_PreviewTextInput;
            YoloModelSettingsPanelControl.IntegerTextInputPreview += IntegerTextBox_PreviewTextInput;
        }

        private void RegisterYoloModelSettingsPanelNames()
        {
            AttachYoloModelSettingsPanelEvents();
            RegisterYoloModelSettingsName(nameof(YoloPythonPathBox), YoloPythonPathBox);
            RegisterYoloModelSettingsName(nameof(YoloProjectRootBox), YoloProjectRootBox);
            RegisterYoloModelSettingsName(nameof(YoloClientScriptBox), YoloClientScriptBox);
            RegisterYoloModelSettingsName(nameof(YoloWeightsPathBox), YoloWeightsPathBox);
            RegisterYoloModelSettingsName(nameof(YoloImageRootBox), YoloImageRootBox);
            RegisterYoloModelSettingsName(nameof(YoloConfidenceBox), YoloConfidenceBox);
            RegisterYoloModelSettingsName(nameof(YoloInferenceImageSizeBox), YoloInferenceImageSizeBox);
            RegisterYoloModelSettingsName(nameof(YoloMaxCandidatesBox), YoloMaxCandidatesBox);
            RegisterYoloModelSettingsName(nameof(YoloTimeoutBox), YoloTimeoutBox);
            RegisterYoloModelSettingsName(nameof(YoloAutoStartCheckBox), YoloAutoStartCheckBox);
            RegisterYoloModelSettingsName(nameof(BrowseYoloPythonButton), BrowseYoloPythonButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloProjectRootButton), BrowseYoloProjectRootButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloClientScriptButton), BrowseYoloClientScriptButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloWeightsButton), BrowseYoloWeightsButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloImageRootButton), BrowseYoloImageRootButton);
            RegisterYoloModelSettingsName(nameof(SaveYoloSettingsButton), SaveYoloSettingsButton);
            RegisterYoloModelSettingsName(nameof(ResetYoloSettingsButton), ResetYoloSettingsButton);
        }

        private void RegisterYoloModelSettingsName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void AttachTrainingSettingsPanelEvents()
        {
            if (trainingSettingsPanelEventsAttached || TrainingSettingsPanelControl == null)
            {
                return;
            }

            trainingSettingsPanelEventsAttached = true;
            TrainingSettingsPanelControl.RefreshReadinessRequested += RefreshTrainingReadinessButton_Click;
            TrainingSettingsPanelControl.StartTrainingRequested += StartTrainingButton_Click;
            TrainingSettingsPanelControl.StopTrainingRequested += StopTrainingButton_Click;
            TrainingSettingsPanelControl.IntegerTextInputPreview += IntegerTextBox_PreviewTextInput;
        }

        private void RegisterTrainingSettingsPanelNames()
        {
            AttachTrainingSettingsPanelEvents();
            RegisterTrainingSettingsName(nameof(TrainingSettingsExpander), TrainingSettingsExpander);
            RegisterTrainingSettingsName(nameof(TrainingImageSizeBox), TrainingImageSizeBox);
            RegisterTrainingSettingsName(nameof(TrainingBatchBox), TrainingBatchBox);
            RegisterTrainingSettingsName(nameof(TrainingEpochBox), TrainingEpochBox);
            RegisterTrainingSettingsName(nameof(TrainingCfgBox), TrainingCfgBox);
            RegisterTrainingSettingsName(nameof(TrainingWeightBox), TrainingWeightBox);
            RegisterTrainingSettingsName(nameof(TrainingValidationPercentBox), TrainingValidationPercentBox);
            RegisterTrainingSettingsName(nameof(TrainingTestPercentBox), TrainingTestPercentBox);
            RegisterTrainingSettingsName(nameof(TrainingSplitSeedBox), TrainingSplitSeedBox);
            RegisterTrainingSettingsName(nameof(TrainingSplitPolicyHintText), TrainingSplitPolicyHintText);
            RegisterTrainingSettingsName(nameof(RefreshTrainingReadinessButton), RefreshTrainingReadinessButton);
            RegisterTrainingSettingsName(nameof(StartTrainingButton), StartTrainingButton);
            RegisterTrainingSettingsName(nameof(StopTrainingButton), StopTrainingButton);
            RegisterTrainingSettingsName(nameof(TrainingReadinessText), TrainingReadinessText);
            RegisterTrainingSettingsName(nameof(TrainingProgressBar), TrainingProgressBar);
            RegisterTrainingSettingsName(nameof(TrainingProgressText), TrainingProgressText);
            RegisterTrainingSettingsName(nameof(TrainingEpochText), TrainingEpochText);
        }

        private void RegisterTrainingSettingsName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void RegisterStatusBarPanelNames()
        {
            RegisterStatusBarName(nameof(DatasetStatusText), DatasetStatusText);
            RegisterStatusBarName(nameof(PythonStatusText), PythonStatusText);
            RegisterStatusBarName(nameof(AnnotationSaveStatusText), AnnotationSaveStatusText);
            RegisterStatusBarName(nameof(ModelStatusText), ModelStatusText);
        }

        private void RegisterStatusBarName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void RegisterShellLogPanelNames()
        {
            RegisterShellLogName(nameof(ShellLogPanel), ShellLogPanel);
        }

        private void RegisterShellLogName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void InitializeYoloEditorPanel()
        {
            EnsureProjectSettings();
            TrainingCfgBox.ItemsSource = Enum.GetNames(typeof(CYolov5TrainingParam.Cfg));
            TrainingWeightBox.ItemsSource = Enum.GetNames(typeof(CYolov5TrainingParam.Weight));
            PopulateProjectConfigPanelFields();
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            double configuredConfidence = global.Data.ProjectSettings.PythonModel.MinimumDetectionConfidence;
            CandidateConfidenceSlider.Value = Math.Clamp(configuredConfidence, 0D, 1D);
            UpdateCandidateConfidenceText();
        }

        private void PopulateProjectConfigPanelFields()
        {
            string recipeName = GetCurrentRecipeName();
            string configPath = GetCurrentRecipeConfigPath();
            ProjectConfigViewModel?.LoadFrom(recipeName, GetRecipeRootDirectory());

            PopulateProjectRecipeList(recipeName);

            SetProjectConfigStatus(string.IsNullOrWhiteSpace(recipeName)
                ? "Recipe 이름이 아직 없습니다. 저장 전에 recipe를 선택하거나 생성해야 합니다."
                : $"현재 설정 파일: {Path.GetFileName(configPath)}");
            UpdateYoloCommandButtons();
        }

        private bool PopulateProjectRecipeList(string selectedRecipeName)
        {
            WpfProjectConfigPanelViewModel viewModel = ProjectConfigViewModel;
            if (viewModel == null)
            {
                return false;
            }

            suppressProjectRecipeSelection = true;
            try
            {
                IReadOnlyList<string> recipeNames = WpfProjectRecipeService.ListRecipeNames(GetRecipeRootDirectory());
                string matchingRecipeName = recipeNames
                    .FirstOrDefault(name => string.Equals(name, selectedRecipeName, StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;
                viewModel.SetRecipeList(recipeNames, matchingRecipeName);

                return true;
            }
            catch (Exception ex)
            {
                viewModel.SetRecipeList(Array.Empty<string>(), string.Empty);
                SetProjectConfigStatus($"Recipe 목록 읽기 실패: {ex.Message}");
                AppendLog($"Recipe 목록 읽기 실패: {ex.Message}");
                return false;
            }
            finally
            {
                suppressProjectRecipeSelection = false;
            }
        }

        private void PopulateYoloEditorFields()
        {
            EnsureProjectSettings();
            YoloModelSettingsViewModel?.LoadFrom(global.Data.ProjectSettings.PythonModel);
        }

        private void PopulateTrainingEditorFields()
        {
            EnsureProjectSettings();
            TrainingSettingsViewModel?.LoadFrom(global.Data.GetTrainingSettings(), global.Data.ProjectSettings.YoloDataset);
        }

        public bool TryLoadStartupSampleImage()
        {
            EnsureProjectSettings();
            string imagePath = YoloWorkerSmokeTestService.ResolveSmokeImagePath(global.Data.ProjectSettings.PythonModel);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                SetDatasetStatus("데이터셋: 샘플 이미지 없음");
                AppendLog("샘플 이미지를 찾지 못했습니다. Python 모델 이미지 루트를 확인하세요.");
                return false;
            }

            return TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false);
        }

        public bool TryLoadImage(string imagePath, bool populateQueue = true, bool refreshQueueDetails = true, bool refreshActiveStatus = true, bool appendLoadLog = true)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                AppendLog($"이미지 없음: {imagePath}");
                return false;
            }

            Stopwatch loadStopwatch = Stopwatch.StartNew();
            long stepStartTicks = loadStopwatch.ElapsedTicks;
            bool cacheHit = false;
            double decodeMilliseconds = 0D;
            double canvasUploadMilliseconds = 0D;
            double canvasRefreshMilliseconds = 0D;
            double stateTransferMilliseconds = 0D;
            double annotationResetMilliseconds = 0D;
            double queuePopulateMilliseconds = 0D;
            double reviewRefreshMilliseconds = 0D;
            double preloadScheduleMilliseconds = 0D;
            DrawingBitmap workspaceBitmap = null;
            CvMat imageMat = null;
            try
            {
                if (TryTakeCachedDecodedImage(imagePath, out CachedDecodedImage cachedImage))
                {
                    cacheHit = true;
                    workspaceBitmap = cachedImage.TakeBitmap();
                    imageMat = cachedImage.TakeMat();
                    cachedImage.Dispose();
                }
                else
                {
                    using DrawingBitmap loaded = AppImageLoader.LoadBitmap(imagePath);
                    workspaceBitmap = loaded.Clone(
                        new DrawingRectangle(0, 0, loaded.Width, loaded.Height),
                        DrawingPixelFormat.Format24bppRgb);
                    imageMat = BitmapImageConverter.ToMat(workspaceBitmap);
                }
                decodeMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                string imageName = Path.GetFileNameWithoutExtension(imagePath);
                using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
                {
                    MainCanvasViewModel.LoadImage(imageMat, Path.GetFileName(imagePath));
                    MainCanvasViewModel.ClearRois();
                    MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                    MainCanvasViewModel.SetMaskOverlays(Array.Empty<RoiImageCanvasMaskOverlay>());
                    MainCanvasViewModel.SetPolygonOverlays(Array.Empty<RoiImageCanvasPolygonOverlay>());
                }
                canvasUploadMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                MainCanvasViewModel.ImageViewer.RefreshGL();
                canvasRefreshMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                activeImageBitmap?.Dispose();
                activeImageBitmap = workspaceBitmap;
                workspaceBitmap = null;
                activeImagePath = imagePath;
                activeImageSize = activeImageBitmap.Size;

                global.Data.LastSelectImageName = imageName;
                global.Data.LastSelectImagePath = imagePath;
                global.ImageWorkspace.SetActiveImage(imageName, imagePath, activeImageBitmap);
                CDisplayManager.ImageSrc = imageMat;
                imageMat = null;
                stateTransferMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                manualRois.Clear();
                manualRoiClassNames.Clear();
                manualRoiShapeKinds.Clear();
                manualRoiOverlayIds.Clear();
                manualSegments.Clear();
                polygonAnnotationService.Reset();
                lastMaskStrokePoint = null;
                activeMaskStrokeSnapshot = null;
                activeMaskStrokeChanged = false;
                pendingDetectionCandidates.Clear();
                confirmedDetectionCandidates.Clear();
                ClearAnnotationHistory();
                UpdateDetectionResultOverlay();
                annotationResetMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (populateQueue)
                {
                    PopulateImageQueue(Path.GetDirectoryName(imagePath), imagePath, refreshQueueDetails);
                }
                queuePopulateMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                SetDatasetStatus($"데이터셋: {Path.GetFileName(imagePath)}  {activeImageSize.Width}x{activeImageSize.Height}");
                SetModelStatus($"모델: {Path.GetFileName(global.Data.ProjectSettings?.PythonModel?.WeightsPath ?? string.Empty)}");
                MarkAnnotationsSaved("\uC774\uBBF8\uC9C0 \uB85C\uB4DC: \uD604\uC7AC \uB77C\uBCA8\uC740 \uC800\uC7A5\uB41C \uC0C1\uD0DC\uB85C \uC2DC\uC791\uD569\uB2C8\uB2E4.");
                RefreshCandidateList();
                RefreshObjectList();
                PopulateClassList();
                if (refreshActiveStatus)
                {
                    RefreshActiveImageQueueStatus(hasActiveCandidates: false);
                }
                else
                {
                    UpdateImageQueueStatusText();
                }
                reviewRefreshMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (!appendLoadLog)
                {
                    PreloadAdjacentQueueImages(imagePath);
                    preloadScheduleMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                    RecordImageLoadDiagnostics(
                        imagePath,
                        cacheHit,
                        loadStopwatch.Elapsed.TotalMilliseconds,
                        decodeMilliseconds,
                        canvasUploadMilliseconds,
                        canvasRefreshMilliseconds,
                        stateTransferMilliseconds,
                        annotationResetMilliseconds,
                        queuePopulateMilliseconds,
                        reviewRefreshMilliseconds,
                        preloadScheduleMilliseconds);
                    return true;
                }
                AppendLog($"이미지 로드: {imagePath}");
                PreloadAdjacentQueueImages(imagePath);
                preloadScheduleMilliseconds = TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                RecordImageLoadDiagnostics(
                    imagePath,
                    cacheHit,
                    loadStopwatch.Elapsed.TotalMilliseconds,
                    decodeMilliseconds,
                    canvasUploadMilliseconds,
                    canvasRefreshMilliseconds,
                    stateTransferMilliseconds,
                    annotationResetMilliseconds,
                    queuePopulateMilliseconds,
                    reviewRefreshMilliseconds,
                    preloadScheduleMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                workspaceBitmap?.Dispose();
                SetDatasetStatus("데이터셋: 이미지 로드 실패");
                AppendLog($"이미지 로드 실패: {ex.Message}");
                return false;
            }
            finally
            {
                imageMat?.Dispose();
            }
        }

        public ImageDecodeCacheDiagnostics GetImageDecodeCacheDiagnostics()
        {
            lock (imageDecodeCacheLock)
            {
                return new ImageDecodeCacheDiagnostics(
                    imageDecodeCache.Count,
                    imageDecodeCacheBytes,
                    Interlocked.Read(ref imageDecodeCacheHits),
                    Interlocked.Read(ref imageDecodeCacheMisses),
                    Interlocked.Read(ref imageDecodeCacheStores),
                    Interlocked.Read(ref imageDecodeCacheEvictions),
                    ImageDecodeCacheCapacity,
                    ImageDecodeCacheMaxBytes);
            }
        }

        private void RecordImageLoadDiagnostics(
            string imagePath,
            bool cacheHit,
            double totalMilliseconds,
            double decodeMilliseconds,
            double canvasUploadMilliseconds,
            double canvasRefreshMilliseconds,
            double stateTransferMilliseconds,
            double annotationResetMilliseconds,
            double queuePopulateMilliseconds,
            double reviewRefreshMilliseconds,
            double preloadScheduleMilliseconds)
        {
            lastImageLoadDiagnostics = new ImageLoadDiagnostics(
                imagePath,
                cacheHit,
                totalMilliseconds,
                decodeMilliseconds,
                canvasUploadMilliseconds,
                canvasRefreshMilliseconds,
                stateTransferMilliseconds,
                annotationResetMilliseconds,
                queuePopulateMilliseconds,
                reviewRefreshMilliseconds,
                preloadScheduleMilliseconds);
        }

        private static double TakeElapsedMilliseconds(Stopwatch stopwatch, ref long previousTicks)
        {
            long currentTicks = stopwatch.ElapsedTicks;
            double elapsedMilliseconds = (currentTicks - previousTicks) * 1000D / Stopwatch.Frequency;
            previousTicks = currentTicks;
            return elapsedMilliseconds;
        }

        private void PreloadAdjacentQueueImages(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || imageQueueItems.Count == 0)
            {
                return;
            }

            List<string> orderedPaths = imageQueueItems
                .Select(item => item.ImagePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
            int currentIndex = orderedPaths.FindIndex(path => string.Equals(path, imagePath, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                return;
            }

            List<string> preloadPaths = new List<string>();
            foreach (int offset in new[] { 1, -1, 2, -2 })
            {
                int index = currentIndex + offset;
                if (index < 0 || index >= orderedPaths.Count)
                {
                    continue;
                }

                string preloadPath = orderedPaths[index];
                if (string.Equals(preloadPath, imagePath, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(preloadPath)
                    || IsImageDecodeCached(preloadPath))
                {
                    continue;
                }

                preloadPaths.Add(preloadPath);
            }

            if (preloadPaths.Count == 0)
            {
                return;
            }

            int version = Interlocked.Increment(ref imageDecodePreloadVersion);
            imageDecodePreloadTask = Task.Run(() =>
            {
                foreach (string preloadPath in preloadPaths)
                {
                    if (version != Volatile.Read(ref imageDecodePreloadVersion)
                        || IsImageDecodeCached(preloadPath))
                    {
                        return;
                    }

                    CachedDecodedImage decoded = TryDecodeImageForCache(preloadPath);
                    if (version != Volatile.Read(ref imageDecodePreloadVersion))
                    {
                        decoded?.Dispose();
                        return;
                    }

                    if (decoded != null)
                    {
                        StoreCachedDecodedImage(decoded);
                    }
                }
            });
        }

        private static CachedDecodedImage TryDecodeImageForCache(string imagePath)
        {
            DrawingBitmap workspaceBitmap = null;
            CvMat imageMat = null;
            try
            {
                using DrawingBitmap loaded = AppImageLoader.LoadBitmap(imagePath);
                if ((long)loaded.Width * loaded.Height > ImageDecodeCacheMaxPixels)
                {
                    return null;
                }

                workspaceBitmap = loaded.Clone(
                    new DrawingRectangle(0, 0, loaded.Width, loaded.Height),
                    DrawingPixelFormat.Format24bppRgb);
                imageMat = BitmapImageConverter.ToMat(workspaceBitmap);
                return new CachedDecodedImage(imagePath, workspaceBitmap, imageMat);
            }
            catch
            {
                workspaceBitmap?.Dispose();
                imageMat?.Dispose();
                return null;
            }
        }

        private bool TryTakeCachedDecodedImage(string imagePath, out CachedDecodedImage cachedImage)
        {
            lock (imageDecodeCacheLock)
            {
                if (imageDecodeCache.TryGetValue(imagePath, out cachedImage))
                {
                    imageDecodeCache.Remove(imagePath);
                    imageDecodeCacheOrder.Remove(imagePath);
                    imageDecodeCacheBytes = Math.Max(0L, imageDecodeCacheBytes - cachedImage.EstimatedBytes);
                    Interlocked.Increment(ref imageDecodeCacheHits);
                    return true;
                }
            }

            Interlocked.Increment(ref imageDecodeCacheMisses);
            cachedImage = null;
            return false;
        }

        private bool IsImageDecodeCached(string imagePath)
        {
            lock (imageDecodeCacheLock)
            {
                return imageDecodeCache.ContainsKey(imagePath);
            }
        }

        private void StoreCachedDecodedImage(CachedDecodedImage decoded)
        {
            if (decoded == null || string.IsNullOrWhiteSpace(decoded.ImagePath))
            {
                decoded?.Dispose();
                return;
            }

            lock (imageDecodeCacheLock)
            {
                if (imageDecodeCache.ContainsKey(decoded.ImagePath))
                {
                    decoded.Dispose();
                    return;
                }

                imageDecodeCache[decoded.ImagePath] = decoded;
                imageDecodeCacheOrder.AddLast(decoded.ImagePath);
                imageDecodeCacheBytes += decoded.EstimatedBytes;
                Interlocked.Increment(ref imageDecodeCacheStores);

                while ((imageDecodeCache.Count > ImageDecodeCacheCapacity || imageDecodeCacheBytes > ImageDecodeCacheMaxBytes)
                    && imageDecodeCacheOrder.First != null)
                {
                    string oldestPath = imageDecodeCacheOrder.First.Value;
                    imageDecodeCacheOrder.RemoveFirst();
                    if (imageDecodeCache.TryGetValue(oldestPath, out CachedDecodedImage oldest))
                    {
                        imageDecodeCache.Remove(oldestPath);
                        imageDecodeCacheBytes = Math.Max(0L, imageDecodeCacheBytes - oldest.EstimatedBytes);
                        oldest.Dispose();
                        Interlocked.Increment(ref imageDecodeCacheEvictions);
                    }
                }
            }
        }

        private void ClearImageDecodeCache()
        {
            lock (imageDecodeCacheLock)
            {
                foreach (CachedDecodedImage decoded in imageDecodeCache.Values)
                {
                    decoded.Dispose();
                }

                imageDecodeCache.Clear();
                imageDecodeCacheOrder.Clear();
                imageDecodeCacheBytes = 0L;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshYoloStatus();
                _ = RefreshYoloSettingsPanelAsync();
                TryLoadStartupSampleImage();
                SetPythonStatus("Python: 추론 실행 대기");
                AppendLog("시작 완료. 추론은 사용자가 명시적으로 실행할 때만 시작합니다.");
            }), DispatcherPriority.ApplicationIdle);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopInferenceStatusPulse();
            inferenceStatusPulseTimer.Tick -= InferenceStatusPulseTimer_Tick;
            StopTrainingStatusPolling();
            trainingStatusPollTimer.Tick -= TrainingStatusPollTimer_Tick;
            Interlocked.Increment(ref imageDecodePreloadVersion);
            WaitForImageDecodePreload();
            imageQueueDetailLoadCts?.Cancel();
            imageQueueDetailLoadCts?.Dispose();
            imageQueueDetailLoadCts = null;
            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = null;
            ClearImageDecodeCache();
            activeImageBitmap?.Dispose();
            activeImageBitmap = null;
        }

        private void WaitForImageDecodePreload()
        {
            Task preloadTask = imageDecodePreloadTask;
            if (preloadTask == null || preloadTask.IsCompleted)
            {
                return;
            }

            try
            {
                preloadTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void LoadSampleButton_Click(object sender, RoutedEventArgs e)
        {
            TryLoadStartupSampleImage();
        }

        private void AddSampleRoiButton_Click(object sender, RoutedEventArgs e)
        {
            if (activeImageSize.IsEmpty)
            {
                AppendLog("ROI를 추가하려면 이미지를 먼저 불러오세요.");
                return;
            }

            int width = Math.Max(20, activeImageSize.Width / 5);
            int height = Math.Max(20, activeImageSize.Height / 5);
            int x = Math.Max(0, (activeImageSize.Width - width) / 2);
            int y = Math.Max(0, (activeImageSize.Height - height) / 2);
            var roi = new DrawingRectangle(x, y, width, height);

            RegisterAnnotationHistoryBeforeChange("Add guide ROI");
            manualRois.Add(roi);
            manualRoiClassNames.Add(FirstNonEmpty(GetSelectedClassName(), "Defect"));
            manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
            manualRoiOverlayIds.Add(string.Empty);
            RedrawReviewRois();
            RefreshObjectList();
            ObjectsReviewTab.IsSelected = true;
            AppendLog($"ROI 추가: {roi.X},{roi.Y},{roi.Width},{roi.Height}");
        }

        private void SaveAnnotationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SaveCurrentAnnotations(out int savedCount))
            {
                MarkActiveImageConfirmed();
                AppendLog($"YOLO 라벨 저장. 객체:{savedCount}  {BuildLabelPathSummary()}");
                return;
            }

            AppendLog("저장할 ROI 또는 확정 후보가 없습니다.");
        }

        private void YoloTrainingWorkflowStep_Requested(object sender, WpfYoloTrainingWorkflowStepEventArgs e)
        {
            ExecuteYoloTrainingWorkflowStep(e?.Step?.Order ?? 0, sender);
        }

        private void YoloFixClassesButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteYoloTrainingWorkflowStep(2, sender);
        }

        private void YoloFixLabelsButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteYoloTrainingWorkflowStep(3, sender);
        }

        private void YoloFixDatasetButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteYoloTrainingWorkflowStep(4, sender);
        }

        private void TutorialOpenHtmlGuideButton_Click(object sender, RoutedEventArgs e)
        {
            string path = ResolveTutorialHtmlGuidePath();
            if (!File.Exists(path))
            {
                SetModelStatus("\uD29C\uD1A0\uB9AC\uC5BC HTML\uC744 \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
                AppendLog($"\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5C6\uC74C: {path}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                SetModelStatus("\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30");
                AppendLog($"\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30: {path}");
            }
            catch (Exception ex)
            {
                SetModelStatus("\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30 \uC2E4\uD328");
                AppendLog($"\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30 \uC2E4\uD328: {ex.Message}");
            }
        }

        private static string ResolveTutorialHtmlGuidePath()
        {
            string[] searchRoots =
            {
                Environment.CurrentDirectory,
                AppContext.BaseDirectory
            };

            foreach (string root in searchRoots)
            {
                string path = FindRelativeFileFromAncestor(root, TutorialHtmlGuideRelativePath);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, TutorialHtmlGuideRelativePath));
        }

        private static string FindRelativeFileFromAncestor(string startPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(startPath) || string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(Path.GetFullPath(startPath));
            }
            catch
            {
                return string.Empty;
            }

            if (!directory.Exists && directory.Parent != null)
            {
                directory = directory.Parent;
            }

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        private void ExecuteYoloTrainingWorkflowStep(int order, object sender)
        {
            switch (order)
            {
                case 1:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    AppendLog("YOLOv5 1단계: 학습 이미지 폴더를 선택합니다.");
                    BrowseImageFolderButton_Click(sender, new RoutedEventArgs());
                    break;

                case 2:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusClassCatalogTab();
                    ClassNameBox?.Focus();
                    SetModelStatus("학습 준비: 클래스를 등록하세요");
                    AppendLog("YOLOv5 2단계: 클래스 탭에서 모델이 배울 이름을 등록하세요.");
                    break;

                case 3:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    SelectAnnotationTool(WpfAnnotationTool.Rectangle, revealInGuide: true);
                    MainCanvasViewModel.IsTeachingMode = true;
                    MainCanvasView?.Focus();
                    SetModelStatus("라벨링: 박스 도구");
                    AppendLog("YOLOv5 3단계: 박스 도구로 객체 영역을 만들고 클래스를 확인하세요.");
                    break;

                case 4:
                    TrySaveActiveAnnotationsForTrainingCheck();
                    FocusYoloSettingsTab();
                    RefreshTrainingReadinessPanel(refreshYaml: true);
                    RefreshTrainingReadinessButton?.Focus();
                    AppendLog("YOLOv5 4단계: 저장된 라벨과 data.yaml을 점검했습니다.");
                    break;

                case 5:
                    FocusYoloSettingsTab();
                    RefreshTrainingReadinessPanel(refreshYaml: true);
                    StartTrainingButton?.Focus();
                    SetModelStatus("학습: 설정 확인 후 시작");
                    AppendLog("YOLOv5 5단계: 학습 설정을 확인하고 시작 버튼을 누르세요.");
                    break;

                case 6:
                    TryApplyLatestTrainingWeightsFromProject(logIfUnchanged: true);
                    SetWorkflowMode(WorkflowMode.Inference);
                    CandidatesReviewTab.IsSelected = true;
                    DetectButton?.Focus();
                    SetYoloCommandStatus("학습 결과 추론 준비: 현재 검사 버튼으로 best.pt를 확인하세요.", isBusy: false);
                    AppendLog("YOLOv5 6단계: 학습한 weight로 현재 이미지를 검사하고 후보를 검토하세요.");
                    break;

                default:
                    AppendLog("YOLOv5 학습 단계가 선택되지 않았습니다.");
                    break;
            }
        }

        private void TrySaveActiveAnnotationsForTrainingCheck()
        {
            bool hasObjects = manualRois.Count > 0 || manualSegments.Count > 0 || confirmedDetectionCandidates.Count > 0;
            if (activeImageBitmap == null || !hasObjects)
            {
                AppendLog("저장할 현재 라벨이 없어서 데이터셋 점검만 실행합니다.");
                return;
            }

            if (SaveCurrentAnnotations(out int savedCount))
            {
                MarkActiveImageConfirmed();
                AppendLog($"YOLO 학습 라벨 저장 후 점검. 객체:{savedCount}  {BuildLabelPathSummary()}");
            }
        }

        private void LearningWorkflowModeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WpfLearningMode? mode = LearningWorkflowViewModel?.SelectedMode?.Mode;
            if (!mode.HasValue)
            {
                return;
            }

            switch (mode.Value)
            {
                case WpfLearningMode.ObjectDetection:
                case WpfLearningMode.Infer:
                case WpfLearningMode.Review:
                    SetWorkflowMode(WorkflowMode.Inference);
                    break;

                case WpfLearningMode.Train:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusYoloSettingsTab();
                    break;

                default:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    break;
            }
        }

        private void AnnotationToolListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WpfAnnotationTool? tool = LearningWorkflowViewModel?.SelectedTool?.Tool;
            if (!tool.HasValue)
            {
                return;
            }

            WpfAnnotationToolCapability capability = WpfAnnotationToolCapabilityService.Get(tool.Value);
            if (!capability.IsConnected)
            {
                activeAnnotationTool = WpfAnnotationTool.Select;
                EndPolygonAnnotationMode(clearDraft: true);
                EndMaskAnnotationMode();
                SetPendingAnnotationToolStatus(capability);
                return;
            }

            activeAnnotationTool = tool.Value;
            if (tool.Value != WpfAnnotationTool.Polygon)
            {
                EndPolygonAnnotationMode(clearDraft: true);
            }

            if (tool.Value != WpfAnnotationTool.Brush && tool.Value != WpfAnnotationTool.Eraser)
            {
                EndMaskAnnotationMode();
            }

            if (tool.Value == WpfAnnotationTool.Polygon)
            {
                BeginPolygonAnnotationMode();
                return;
            }

            if (tool.Value == WpfAnnotationTool.Brush || tool.Value == WpfAnnotationTool.Eraser)
            {
                BeginMaskAnnotationMode(tool.Value);
                return;
            }

            switch (tool.Value)
            {
                case WpfAnnotationTool.Rectangle:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    MainCanvasViewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
                    MainCanvasViewModel.IsTeachingMode = true;
                    SetModelStatus("도구: 박스 라벨링");
                    SetYoloCommandStatus("박스 라벨링 도구가 활성화되었습니다. 이미지 위에서 드래그해 객체 영역을 만드세요.", isBusy: false);
                    AppendLog("박스 라벨링 도구");
                    break;

                case WpfAnnotationTool.Ellipse:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    MainCanvasViewModel.DrawingShapeKind = CanvasRoiShapeKind.Ellipse;
                    MainCanvasViewModel.IsTeachingMode = true;
                    SetModelStatus("도구: 원/타원");
                    SetYoloCommandStatus("원/타원은 이미지 픽셀 기준 bounding box로 저장되고 캔버스에는 채워진 타원으로 표시됩니다.", isBusy: false);
                    AppendLog("원/타원 라벨링 도구");
                    break;

                case WpfAnnotationTool.PanZoom:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    PanCanvasButton_Click(sender, new RoutedEventArgs());
                    break;

                case WpfAnnotationTool.Delete:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    DeleteObjectButton_Click(sender, new RoutedEventArgs());
                    break;

                case WpfAnnotationTool.Polygon:
                    SetPendingAnnotationToolStatus("폴리곤");
                    break;

                case WpfAnnotationTool.Brush:
                    SetPendingAnnotationToolStatus("브러시");
                    break;

                case WpfAnnotationTool.Eraser:
                    SetPendingAnnotationToolStatus("지우개");
                    break;

                case WpfAnnotationTool.Select:
                    MainCanvasViewModel.IsTeachingMode = false;
                    MainCanvasViewModel.IsImagePointInputMode = IsSelectedManualSegment();
                    SetModelStatus("도구: 선택");
                    break;

                case WpfAnnotationTool.Undo:
                    UndoWpfAnnotationHistory();
                    break;

                case WpfAnnotationTool.Redo:
                    RedoWpfAnnotationHistory();
                    break;

                default:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    break;
            }
        }

        private void SetPendingAnnotationToolStatus(WpfAnnotationToolCapability capability)
        {
            string toolName = capability?.DisplayName ?? string.Empty;
            SetWorkflowMode(WorkflowMode.Labeling);
            MainCanvasViewModel.IsTeachingMode = false;
            SetModelStatus($"\uB3C4\uAD6C \uB300\uAE30: {toolName}");
            SetYoloCommandStatus(capability?.StatusText ?? "\uD604\uC7AC WPF \uACBD\uB85C \uAC80\uC99D \uC804\uC785\uB2C8\uB2E4.", isBusy: false);
            AppendLog($"{toolName} \uB3C4\uAD6C \uB300\uAE30: {capability?.StatusText}");
        }

        private void SetPendingAnnotationToolStatus(string toolName)
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            MainCanvasViewModel.IsTeachingMode = false;
            SetModelStatus($"도구 준비: {toolName}");
            SetYoloCommandStatus($"{toolName} 도구는 실제 드로잉 경로 검증 후 연결합니다. 현재 라벨링은 박스 도구를 사용하세요.", isBusy: false);
            AppendLog($"{toolName} 도구는 교육 팔레트에 준비됐고 실제 드로잉 경로는 다음 구현 대상입니다.");
        }

        private void LearningStepListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WpfLearningStep? step = LearningWorkflowViewModel?.SelectedStep?.Step;
            if (!step.HasValue)
            {
                return;
            }

            switch (step.Value)
            {
                case WpfLearningStep.Sample:
                    TryLoadStartupSampleImage();
                    break;

                case WpfLearningStep.Label:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    SelectAnnotationTool(WpfAnnotationTool.Rectangle, revealInGuide: true);
                    AddSampleRoiButton_Click(sender, new RoutedEventArgs());
                    break;

                case WpfLearningStep.Infer:
                    SetWorkflowMode(WorkflowMode.Inference);
                    break;

                case WpfLearningStep.Review:
                    CandidatesReviewTab.IsSelected = true;
                    break;

                case WpfLearningStep.Save:
                    SaveAnnotationsButton_Click(sender, new RoutedEventArgs());
                    break;
            }
        }

        private void SelectAnnotationTool(WpfAnnotationTool tool, bool revealInGuide = false)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            LearningWorkflowViewModel.SelectedTool = LearningWorkflowViewModel.AnnotationTools
                .FirstOrDefault(item => item.Tool == tool)
                ?? LearningWorkflowViewModel.SelectedTool;

            if (revealInGuide)
            {
                LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
            }
        }

        private void FitCanvasButton_Click(object sender, RoutedEventArgs e)
        {
            MainCanvasViewModel.ImageViewer.ZoomToFit();
        }

        private void ActualSizeCanvasButton_Click(object sender, RoutedEventArgs e)
        {
            MainCanvasViewModel.ImageViewer.ZoomToActualSize();
        }

        private void PanCanvasButton_Click(object sender, RoutedEventArgs e)
        {
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);
            AppendLog("캔버스 이동 모드");
        }

        private void FocusCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            FocusSelectedCandidateInViewer(logIfMissing: true);
        }

        private void ResetAiOverlayCanvasButton_Click(object sender, RoutedEventArgs e)
        {
            int removedCount = pendingDetectionCandidates.Count;
            pendingDetectionCandidates.Clear();
            RefreshCandidateList();
            RedrawReviewRois();
            UpdateDetectionResultOverlay();
            SetPythonStatus("Python: AI 후보 표시 지움");
            AppendLog($"AI 후보 표시 지움: {removedCount}개");
        }

        private void TeachingModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true)
            {
                if (!MainCanvasViewModel.IsTeachingMode)
                {
                    MainCanvasViewModel.TeachingCommand.Execute(null);
                }
            }

            AppendLog("라벨링 모드로 전환했습니다. 이미지 선택만으로 추론하지 않습니다.");
        }

        private void InferenceModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetWorkflowMode(WorkflowMode.Inference);
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true && MainCanvasViewModel.IsTeachingMode)
            {
                MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            AppendLog("추론 검토 모드로 전환했습니다. 현재 추론 또는 큐 검사 버튼으로 YOLO를 실행하세요.");
        }

        private async void CheckYoloButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginYoloEnvironmentCommand("YOLO 설정 점검 중..."))
            {
                return;
            }

            try
            {
                EnsureProjectSettings();
                PythonModelValidationResult result = PythonModelSettingsValidator.Validate(global.Data.ProjectSettings.PythonModel, requireWeights: true);
                RefreshYoloStatus();
                YoloSettingsReviewTab.IsSelected = true;
                await RefreshYoloSettingsPanelAsync(result).ConfigureAwait(true);

                if (result.IsValid)
                {
                    SetYoloCommandStatus("YOLO 설정 준비 완료.", isBusy: false);
                    AppendLog("YOLO 설정 준비 완료.");
                    return;
                }

                SetYoloCommandStatus("YOLO 설정 확인 필요.", isBusy: false);
                AppendLog("YOLO 설정 확인 필요:");
                foreach (string line in result.Errors.Concat(result.Warnings))
                {
                    AppendLog($"- {line}");
                }
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"YOLO 설정 점검 실패: {ex.Message}", isBusy: false);
                AppendLog($"YOLO 설정 점검 실패: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunInteractiveDetectionAsync(allowSmokeFallback: false).ConfigureAwait(true);
        }

        private async void InstallRequirementsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginYoloEnvironmentCommand("Python requirements 점검 중..."))
            {
                return;
            }

            try
            {
                EnsureProjectSettings();
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
                PythonEnvironmentCheckResult check = await PythonEnvironmentService
                    .CheckRequirementsAsync(settings)
                    .ConfigureAwait(true);

                if (check.Errors.Count > 0)
                {
                    SetYoloCommandStatus($"설치 건너뜀: {check.Summary}", isBusy: false);
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    AppendLog($"Python requirements 설치 건너뜀: {check.Summary}");
                    return;
                }

                if (check.MissingPackages.Count == 0)
                {
                    SetYoloCommandStatus("Python 패키지 설치 상태 정상.", isBusy: false);
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    AppendLog("Python requirements 설치 건너뜀. 누락 패키지가 없습니다.");
                    return;
                }

                SetYoloCommandStatus($"누락 Python 패키지 {check.MissingPackages.Count}개 설치 중...", isBusy: true);
                AppendLog($"Python requirements 설치 중: {string.Join(", ", check.MissingPackages.Take(8))}");
                PythonPackageInstallResult install = await PythonEnvironmentService
                    .InstallRequirementsAsync(settings)
                    .ConfigureAwait(true);

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus(install.Succeeded
                    ? "Python requirements 설치 완료. 다음은 테스트를 실행하세요."
                    : $"설치 실패: {install.Summary}", isBusy: false);
                AppendLog(install.Succeeded
                    ? "Python requirements 설치 완료."
                    : $"Python requirements 설치 실패: {install.Summary}");
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"설치 실패: {ex.Message}", isBusy: false);
                AppendLog($"Python requirements 설치 실패: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void RunYoloSmokeButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentWorkflowMode != WorkflowMode.Inference)
            {
                SetWorkflowMode(WorkflowMode.Inference);
                AppendLog("YOLO 테스트를 위해 추론 검토 모드로 전환했습니다.");
            }

            if (!BeginYoloEnvironmentCommand("YOLO 테스트 추론 중..."))
            {
                return;
            }

            try
            {
                await RunInteractiveDetectionAsync(allowSmokeFallback: true).ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus("YOLO 테스트 추론 완료.", isBusy: false);
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"YOLO 테스트 추론 실패: {ex.Message}", isBusy: false);
                AppendLog($"YOLO 테스트 추론 실패: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void RestartPythonWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginYoloEnvironmentCommand("Python worker 재시작 중..."))
            {
                return;
            }

            try
            {
                bool connected = await global
                    .RestartPythonModelClientConnectionAsync(GetWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);

                if (connected)
                {
                    string requestId = CreateRequestId();
                    global.DeepLearning.SendHealthCheck(requestId);
                    global.DeepLearning.SendModelStatus(requestId, ensureLoaded: false);
                }

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus(connected
                    ? "Python worker 재시작 및 연결 완료."
                    : BuildPythonWorkerFailureText(), isBusy: false);
                AppendLog(connected
                    ? "Python worker 재시작 및 연결 완료."
                    : BuildPythonWorkerFailureText());
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"Python worker 재시작 실패: {ex.Message}", isBusy: false);
                AppendLog($"Python worker 재시작 실패: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void StopPythonWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginYoloEnvironmentCommand("Python worker 중지 중..."))
            {
                return;
            }

            try
            {
                await global.StopPythonModelClientConnectionAsync().ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus("Python worker 중지 완료.", isBusy: false);
                AppendLog("Python worker 중지 완료.");
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"Python worker 중지 실패: {ex.Message}", isBusy: false);
                AppendLog($"Python worker 중지 실패: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void DetectSelectedQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            if (ImageQueueGrid.SelectedItem is not WpfImageQueueItem item)
            {
                AppendLog("먼저 이미지를 선택하세요.");
                return;
            }

            await RunInteractiveDetectionAsync(item.ImagePath, allowSmokeFallback: false).ConfigureAwait(true);
        }

        private async void BatchDetectQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunBatchDetectionAsync(GetVisibleQueueItems(), "표시 행").ConfigureAwait(true);
        }

        private async void RetryFailedQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunBatchDetectionAsync(
                imageQueueItems.Where(item => item.ReviewState == YoloImageReviewState.Failed).ToList(),
                "실패 재시도").ConfigureAwait(true);
        }

        private void StopBatchQueueButton_Click(object sender, RoutedEventArgs e)
        {
            batchDetectionCts?.Cancel();
            AppendLog("일괄 검사 중지를 요청했습니다.");
        }

        private void CandidateConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateCandidateConfidenceText();
            if (CandidateListBox == null)
            {
                return;
            }

            RefreshCandidateList();
        }

        private void CandidateListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmSelectedCandidateButton_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                SkipSelectedCandidateButton_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ConfirmAllCandidatesButton_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                SelectCandidateOffset(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                SelectCandidateOffset(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                FocusSelectedCandidateInViewer(logIfMissing: true);
                e.Handled = true;
            }
        }

        private void PreviousCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            SelectCandidateOffset(-1);
        }

        private void NextCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            SelectCandidateOffset(1);
        }

        private void SelectCandidateOffset(int offset)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            List<WpfCandidateReviewListItem> candidates = CandidateReviewViewModel.Candidates
                .Where(item => item?.Payload is YoloWorkerSmokeCandidate)
                .ToList();
            if (candidates.Count == 0)
            {
                AppendLog("이동할 AI 후보가 없습니다.");
                return;
            }

            if (candidates.Count == 1)
            {
                AppendLog("이동할 다른 AI 후보가 없습니다.");
                return;
            }

            int selectedIndex = candidates.IndexOf(CandidateReviewViewModel.SelectedCandidate);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }
            else
            {
                selectedIndex = (selectedIndex + offset + candidates.Count) % candidates.Count;
            }

            WpfCandidateReviewListItem selected = candidates[selectedIndex];
            CandidateReviewViewModel.SelectedCandidate = selected;
            CandidateListBox?.ScrollIntoView(selected);
            CandidateListBox?.Focus();
            FocusSelectedCandidateInViewer(logIfMissing: false);
        }

        private YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
            YoloWorkerSmokeCandidate current,
            IEnumerable<YoloWorkerSmokeCandidate> removingCandidates)
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            if (visibleCandidates.Count == 0)
            {
                return null;
            }

            var removing = new HashSet<YoloWorkerSmokeCandidate>(removingCandidates?.Where(candidate => candidate != null)
                ?? Enumerable.Empty<YoloWorkerSmokeCandidate>());
            List<YoloWorkerSmokeCandidate> remaining = visibleCandidates
                .Where(candidate => !removing.Contains(candidate))
                .ToList();
            if (remaining.Count == 0)
            {
                return null;
            }

            int currentIndex = -1;
            for (int i = 0; i < visibleCandidates.Count; i++)
            {
                if (ReferenceEquals(visibleCandidates[i], current))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = Math.Min(currentIndex, remaining.Count - 1);
            return remaining[nextIndex];
        }

        private void ConfirmSelectedCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("먼저 AI 후보를 선택하세요.");
                return;
            }

            ConfirmCandidates(new[] { candidate }, "선택");
        }

        private void ConfirmAllCandidatesButton_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates = GetVisibleCandidateList();
            if (candidates.Count == 0)
            {
                AppendLog("확정할 표시 AI 후보가 없습니다.");
                return;
            }

            ConfirmCandidates(candidates, "표시 후보 전체");
        }

        private void SkipSelectedCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("스킵할 AI 후보를 선택하세요.");
                return;
            }

            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(candidate, new[] { candidate });
            RegisterAnnotationHistoryBeforeChange("Skip AI candidate", markDirty: false);
            pendingDetectionCandidates.Remove(candidate);
            RefreshCandidateListWithPreferred(nextCandidate);
            RedrawReviewRois();
            FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            MarkActiveImageSkippedOrCandidate();
            AddCandidateReviewHistory($"\uC2A4\uD0B5: {FormatCandidate(candidate)}");
            SetPythonStatus($"Python: 대기 {pendingDetectionCandidates.Count} / 확정 {confirmedDetectionCandidates.Count}");
            AppendLog($"후보 스킵: {FormatCandidate(candidate)}");
        }

        private void CandidateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCandidateActionState();
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                ApplyCandidateSelectionReview(null);
                UpdateDetectionResultOverlay();
                RedrawReviewRois();
                return;
            }

            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            string confidence = candidate.Confidence.ToString("P1", CultureInfo.CurrentCulture);
            ApplyCandidateSelectionReview(candidate);
            SetModelStatus(bounds.IsEmpty
                ? $"후보: {candidate.ClassName} {confidence} 이미지 밖"
                : $"후보: {candidate.ClassName} {confidence}  {WpfCandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            UpdateDetectionResultOverlay();
            RedrawReviewRois();
        }

        private void ClassNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddClassButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void AddClassButton_Click(object sender, RoutedEventArgs e)
        {
            string className = ClassCatalogService.NormalizeClassName(ClassCatalogViewModel?.ClassName);
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("클래스 이름을 입력하세요.");
                return;
            }

            if (!ClassCatalogService.TryAddClass(global.Data, className, out CClassItem addedClass))
            {
                SetClassEditStatus($"이미 있거나 추가할 수 없는 클래스입니다: {className}");
                return;
            }

            SaveClassCatalog();
            PopulateClassList(addedClass.Text);
            ClassCatalogViewModel?.ClearClassName();

            ClassNameBox?.Focus();

            SetClassEditStatus($"클래스 추가됨: {addedClass.Text}");
        }

        private void RemoveClassButton_Click(object sender, RoutedEventArgs e)
        {
            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("삭제할 클래스를 선택하세요.");
                return;
            }

            if (string.Equals(className, "Defect", StringComparison.OrdinalIgnoreCase))
            {
                SetClassEditStatus("기본 Defect 클래스는 삭제하지 않습니다.");
                return;
            }

            if (!ClassCatalogService.RemoveClass(global.Data, className))
            {
                SetClassEditStatus($"삭제할 클래스를 찾지 못했습니다: {className}");
                return;
            }

            SaveClassCatalog();
            PopulateClassList();
            ClassCatalogViewModel?.ClearClassName();
            SetClassEditStatus($"클래스 삭제됨: {className}");
        }

        private void ClassListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            if (ClassCatalogViewModel != null)
            {
                ClassCatalogViewModel.ClassName = className;
            }
        }

        private void BrowseOutputRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickFolder("YOLO 데이터셋 출력 폴더 선택", ClassCatalogViewModel?.OutputRootPath, out string selectedPath))
            {
                if (ClassCatalogViewModel != null)
                {
                    ClassCatalogViewModel.OutputRootPath = selectedPath;
                }

                SaveOutputRootFromEditor();
            }
        }

        private void SaveOutputRootButton_Click(object sender, RoutedEventArgs e)
        {
            SaveOutputRootFromEditor();
        }

        private void BrowseYoloPythonButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickFile(
                "Select Python executable",
                "Python executable (python*.exe)|python*.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.PythonExecutablePath ?? YoloPythonPathBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.PythonExecutablePath = selectedPath;
                }

                NotifyYoloPathSelected("Python 실행 파일", selectedPath);
            }
        }

        private void BrowseYoloProjectRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickFolder("YOLO 프로젝트 폴더 선택", YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootBox.Text, out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ProjectRootPath = selectedPath;
                }

                NotifyYoloPathSelected("YOLO 프로젝트 폴더", selectedPath);
            }
        }

        private void BrowseYoloClientScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickFile(
                "YOLO 클라이언트 script 선택",
                "Python scripts (*.py)|*.py|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.ClientScriptPath ?? YoloClientScriptBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ClientScriptPath = selectedPath;
                }

                NotifyYoloPathSelected("YOLO 클라이언트 script", selectedPath);
            }
        }

        private void BrowseYoloWeightsButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickFile(
                "YOLO weights 선택",
                "YOLO weights (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.WeightsPath ?? YoloWeightsPathBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.WeightsPath = selectedPath;
                }

                NotifyYoloPathSelected("YOLO weights", selectedPath);
            }
        }

        private void BrowseYoloImageRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickFolder("이미지 루트 폴더 선택", YoloModelSettingsViewModel?.ImageRootPath ?? YoloImageRootBox.Text, out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ImageRootPath = selectedPath;
                }

                NotifyYoloPathSelected("이미지 루트 폴더", selectedPath);
            }
        }

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
        }

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                e.Handled = true;
                return;
            }

            string proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, e.Text);
            e.Handled = proposed.Count(ch => ch == '.') > 1
                || proposed.Any(ch => !char.IsDigit(ch) && ch != '.');
        }

        private async void SaveYoloSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveYoloEditorFields();
                SaveTrainingEditorFields();
                bool pendingWeightsRecipeSave = hasPendingTrainingWeightsRecipeSave;
                if (pendingWeightsRecipeSave)
                {
                    UpdateAppliedTrainingWeightsHistory(global.Data.ProjectSettings.PythonModel.WeightsPath, savedToRecipe: true);
                }

                bool configSaved = SaveProjectConfigFromPanel();
                if (!configSaved && pendingWeightsRecipeSave)
                {
                    UpdateAppliedTrainingWeightsHistory(global.Data.ProjectSettings.PythonModel.WeightsPath, savedToRecipe: false);
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                AppendLog(configSaved
                    ? "YOLO 모델 설정 저장 완료."
                    : "YOLO 모델 설정 반영 완료. Recipe 적용 후 설정 저장이 필요합니다.");
                if (configSaved && pendingWeightsRecipeSave)
                {
                    hasPendingTrainingWeightsRecipeSave = false;
                    UpdateYoloTrainingHistoryText();
                    SetYoloCommandStatus("학습 weight가 recipe 설정에 저장되었습니다.", isBusy: false);
                    SetProjectConfigStatus("best.pt 적용 및 설정 저장 완료.");
                }
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"설정 저장 실패: {ex.Message}", isBusy: false);
                AppendLog($"YOLO 설정 저장 실패: {ex.Message}");
            }
        }

        private async void ResetYoloSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel = new PythonModelSettings();
            global.Data.ProjectSettings.PythonModel.EnsureDefaults();
            PopulateYoloEditorFields();
            RefreshYoloStatus();
            await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
            AppendLog("YOLO 모델 설정을 기본값으로 되돌렸습니다.");
        }

        private void RefreshTrainingReadinessButton_Click(object sender, RoutedEventArgs e)
        {
            SaveTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: true);
        }

        private async void StartTrainingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginTrainingCommand("학습 데이터셋 준비 중..."))
            {
                return;
            }

            try
            {
                SaveTrainingEditorFields();
                RefreshTrainingReadinessPanel(refreshYaml: true);
                bool ready = await global
                    .EnsurePythonModelClientReadyAsync(GetWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);
                if (!ready)
                {
                    string readinessText = BuildPythonWorkerFailureText();
                    SetTrainingReadinessStatus(readinessText);
                    AppendLog(readinessText);
                    return;
                }

                bool started = global.TrainingWorkflow.TryStartTraining(global.Data, global.DeepLearning);
                string startText = started
                    ? "학습 시작 명령 전송 완료. worker 상태 대기 중..."
                    : "학습 시작 명령을 보내지 못했습니다. 데이터셋 준비 상태와 worker 연결을 확인하세요.";
                SetTrainingReadinessStatus(startText);
                AppendLog(startText);
                if (started)
                {
                    StartTrainingStatusPolling();
                }
            }
            catch (Exception ex)
            {
                string errorText = $"학습 시작 실패: {ex.Message}";
                SetTrainingReadinessStatus(errorText);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
            }
        }

        private async void StopTrainingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginTrainingCommand("학습 중지 요청 중..."))
            {
                return;
            }

            try
            {
                bool stopped = await Task.Run(() => global.TrainingWorkflow.TryStopTraining(global.DeepLearning)).ConfigureAwait(true);
                string stopText = stopped
                    ? "학습 중지 명령 전송 완료."
                    : "학습 중지 명령을 보내지 못했습니다. worker 연결을 확인하세요.";
                SetTrainingReadinessStatus(stopText);
                AppendLog(stopText);
            }
            catch (Exception ex)
            {
                string errorText = $"학습 중지 실패: {ex.Message}";
                SetTrainingReadinessStatus(errorText);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
            }
        }

        private void LoadImageRootButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureProjectSettings();
            string imageRootPath = global.Data.ProjectSettings.PythonModel.ImageRootPath;
            if (string.IsNullOrWhiteSpace(imageRootPath) || !Directory.Exists(imageRootPath))
            {
                AppendLog($"설정된 이미지 루트가 없습니다: {imageRootPath}");
                return;
            }

            LoadImageQueueFromRoot(imageRootPath, activeImagePath, loadFirstImage: true);
        }

        private void BrowseImageFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "이미지 폴더 선택",
                InitialDirectory = Directory.Exists(currentImageRoot) ? currentImageRoot : string.Empty
            };

            if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return;
            }

            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel.ImageRootPath = dialog.FolderName;
            LoadImageQueueFromRoot(dialog.FolderName, string.Empty, loadFirstImage: true);
        }

        private void RefreshImageQueueButton_Click(object sender, RoutedEventArgs e)
        {
            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : global.Data.ProjectSettings?.PythonModel?.ImageRootPath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                AppendLog($"이미지 루트가 없습니다: {root}");
                return;
            }

            LoadImageQueueFromRoot(root, activeImagePath, loadFirstImage: imageQueueItems.Count == 0);
        }

        private void NextUnlabeledButton_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<string> orderedPaths = imageQueueItems.Select(item => item.ImagePath).ToList();
            if (imageReviewStatus.TryFindNextUnlabeled(orderedPaths, activeImagePath, out string nextImagePath))
            {
                SelectImageQueueItem(nextImagePath);
                TryLoadImage(nextImagePath);
                return;
            }

            AppendLog("현재 큐에 미라벨 이미지가 없습니다.");
        }

        private void ImageQueueFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            imageQueueView?.Refresh();
            UpdateQueueQuickFilterButtons();
            UpdateImageQueueStatusText();
        }

        private void QueueFilterAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetImageQueueFilter(WpfImageQueueFilter.All);
        }

        private void QueueFilterCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            SetImageQueueFilter(WpfImageQueueFilter.Candidate);
        }

        private void QueueFilterFailedButton_Click(object sender, RoutedEventArgs e)
        {
            SetImageQueueFilter(WpfImageQueueFilter.Failed);
        }

        private void QueueFilterConfirmedButton_Click(object sender, RoutedEventArgs e)
        {
            SetImageQueueFilter(WpfImageQueueFilter.Confirmed);
        }

        private void QueueFilterSkippedButton_Click(object sender, RoutedEventArgs e)
        {
            SetImageQueueFilter(WpfImageQueueFilter.Skipped);
        }

        private void QueueFilterNoCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            SetImageQueueFilter(WpfImageQueueFilter.NoCandidate);
        }

        private void SetImageQueueFilter(WpfImageQueueFilter filter)
        {
            if (ImageQueueFilterBox?.ItemsSource is IEnumerable<WpfImageQueueFilterOption> options)
            {
                WpfImageQueueFilterOption selected = options.FirstOrDefault(option => option.Filter == filter);
                if (selected != null)
                {
                    ImageQueueFilterBox.SelectedItem = selected;
                    return;
                }
            }

            imageQueueView?.Refresh();
            UpdateQueueQuickFilterButtons();
            UpdateImageQueueStatusText();
        }

        private void ImageQueueSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void ImageQueueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressImageQueueSelection)
            {
                return;
            }

            if (ImageQueueGrid.SelectedItem is not WpfImageQueueItem item)
            {
                UpdateSelectedQueueImageButton(null);
                return;
            }

            UpdateSelectedQueueImageButton(item);
            TryOpenSelectedQueueImage(skipIfAlreadyActive: true);
        }

        private void ImageQueueGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private void OpenSelectedQueueImageButton_Click(object sender, RoutedEventArgs e)
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private bool TryOpenSelectedQueueImage(bool skipIfAlreadyActive = false)
        {
            if (ImageQueueGrid.SelectedItem is not WpfImageQueueItem item
                || string.IsNullOrWhiteSpace(item.ImagePath)
                || !File.Exists(item.ImagePath))
            {
                AppendLog("열 이미지를 선택하세요.");
                return false;
            }

            UpdateSelectedQueueImageButton(item);

            if (skipIfAlreadyActive
                && string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return TryLoadImage(
                item.ImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
        }

        private void UpdateSelectedQueueImageButton(WpfImageQueueItem item)
        {
            bool canOpenSelectedImage = item != null
                && !string.IsNullOrWhiteSpace(item.ImagePath)
                && File.Exists(item.ImagePath);

            if (ImageQueuePanelControl?.ViewModel != null)
            {
                ImageQueuePanelControl.ViewModel.SetSelectedImageAvailability(canOpenSelectedImage);
                return;
            }

            SetControlEnabled(OpenSelectedQueueImageButton, canOpenSelectedImage);
        }


        private static bool TryReadImageSize(string imagePath, out DrawingSize imageSize, out string error)
        {
            return WpfImageQueueDetailLoader.TryReadImageSize(imagePath, out imageSize, out error);
        }

        private async Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
        {
            if (isDetecting || isBatchDetectionRunning)
            {
                return;
            }

            EnsureProjectSettings();
            isDetecting = true;
            UpdateYoloCommandButtons();
            UpdateCandidateActionState();
            SetYoloCommandStatus("추론 준비 중...", isBusy: true);
            SetGlobalInferenceStatus("현재 이미지 추론 준비 중", isBusy: true);
            SetPythonStatus("Python: 추론 준비 중");
            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                string targetImagePath = !string.IsNullOrWhiteSpace(imagePath)
                    ? imagePath
                    : !string.IsNullOrWhiteSpace(activeImagePath)
                        ? activeImagePath
                        : YoloWorkerSmokeTestService.ResolveSmokeImagePath(global.Data.ProjectSettings.PythonModel);
                string inferencePath = "worker";
                YoloWorkerSmokeTestResult result = await RunWorkerDetectionForImageAsync(
                        targetImagePath,
                        applyToCanvas: true,
                        CancellationToken.None,
                        GetInteractiveWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);
                if (!result.Succeeded && allowSmokeFallback)
                {
                    AppendLog($"Worker 추론 실패, smoke 경로로 전환: {Path.GetFileName(targetImagePath)}");
                    inferencePath = "smoke fallback";
                    result = await RunDetectionForImageAsync(targetImagePath, applyToCanvas: true, CancellationToken.None)
                        .ConfigureAwait(true);
                }

                string elapsed = FormatElapsed(totalStopwatch.Elapsed);
                SetYoloCommandStatus(
                    result.Succeeded
                        ? $"추론 완료: 후보 {result.CandidateCount}개 / {elapsed}"
                        : $"추론 실패: {elapsed}",
                    isBusy: false);
                SetGlobalInferenceStatus(
                    result.Succeeded
                        ? $"완료: 후보 {result.CandidateCount}개 / {elapsed}"
                        : $"실패: {elapsed}",
                    isBusy: false,
                    isWarning: !result.Succeeded);
                AppendLog(result.Succeeded
                    ? $"단일 이미지 추론 완료: {FormatElapsed(totalStopwatch.Elapsed)} / 경로 {FormatInferencePath(inferencePath)}"
                    : $"단일 이미지 추론 실패: {FormatElapsed(totalStopwatch.Elapsed)} / worker 연결 또는 응답을 확인하세요.");
            }
            finally
            {
                isDetecting = false;
                UpdateYoloCommandButtons();
                UpdateCandidateActionState();
            }
        }

        private async Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            EnsureProjectSettings();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                AppendLog($"검출 이미지 없음: {imagePath}");
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 찾지 못했습니다.",
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { $"검출 이미지를 찾지 못했습니다: {imagePath}" }
                };
            }

            if (applyToCanvas && !string.Equals(imagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                TryLoadImage(imagePath);
            }

            SetPythonStatus("Python: YOLO smoke 실행 중");
            AppendLog($"YOLO smoke 추론 시작: {Path.GetFileName(imagePath)}");
            YoloWorkerSmokeTestResult result = await YoloWorkerSmokeTestService
                .RunAsync(global.Data.ProjectSettings.PythonModel, imagePath, cancellationToken)
                .ConfigureAwait(true);

            if (applyToCanvas)
            {
                if (!string.IsNullOrWhiteSpace(result.ImagePath) && File.Exists(result.ImagePath))
                {
                    TryLoadImage(result.ImagePath);
                }

                ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                SetPythonStatus(result.Succeeded ? $"Python: OK  후보 {result.CandidateCount}" : "Python: smoke 실패");
                foreach (string error in result.Errors)
                {
                    AppendLog($"- {error}");
                }
            }

            AppendLog(result.Summary);
            AppendLog($"YOLO smoke 추론 시간: {FormatElapsed(stopwatch.Elapsed)}");
            return result;
        }

        private async Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
        {
            var stopwatch = Stopwatch.StartNew();
            EnsureProjectSettings();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 찾지 못했습니다.",
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { $"검출 이미지를 찾지 못했습니다: {imagePath}" }
                };
            }

            DrawingSize requestImageSize = activeImageSize;
            if (applyToCanvas && !TryLoadImage(imagePath, populateQueue: false))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 로드하지 못했습니다.",
                    ImagePath = imagePath,
                    Errors = new[] { $"검출 이미지를 로드하지 못했습니다: {imagePath}" }
                };
            }

            if (applyToCanvas)
            {
                requestImageSize = activeImageSize;
            }
            else if (!TryReadImageSize(imagePath, out requestImageSize, out string imageSizeError))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = imageSizeError,
                    ImagePath = imagePath,
                    Errors = new[] { imageSizeError }
                };
            }

            int timeoutMilliseconds = connectTimeoutMilliseconds > 0
                ? connectTimeoutMilliseconds
                : GetWorkerConnectTimeoutMilliseconds();
            SetGlobalInferenceStatus(
                applyToCanvas
                    ? "현재 이미지 추론 준비 중"
                    : $"일괄 추론 준비: {Path.GetFileName(imagePath)}",
                isBusy: true);
            SetPythonStatus("Python: worker 연결 확인 중");
            SetYoloCommandStatus("Python worker 준비 중...", isBusy: true);
            bool ready = workerReadyAlreadyChecked
                ? true
                : await global.EnsurePythonModelClientReadyAsync(timeoutMilliseconds).ConfigureAwait(true);
            if (!ready)
            {
                SetGlobalInferenceStatus("실패: worker 연결 실패", isBusy: false, isWarning: true);
                SetPythonStatus("Python: worker 연결 실패");
                AppendLog($"Worker 추론 연결 실패: {FormatElapsed(stopwatch.Elapsed)}. YOLO 설정 또는 worker 상태를 확인하세요.");
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = BuildPythonWorkerFailureText(),
                    ImagePath = imagePath,
                    Errors = new[] { BuildPythonWorkerFailureText() }
                };
            }

            var completion = new TaskCompletionSource<DetectionCandidatesUpdatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object sender, DetectionCandidatesUpdatedEventArgs e)
            {
                if (e == null)
                {
                    return;
                }

                bool sameImage = string.Equals(e.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.ImageName, Path.GetFileNameWithoutExtension(imagePath), StringComparison.OrdinalIgnoreCase);
                if (!sameImage)
                {
                    return;
                }

                if (e.Reason == DetectionCandidateUpdateReason.ResultCompleted
                    || e.Reason == DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    completion.TrySetResult(e);
                }
            }

            global.DetectionResults.DetectionCandidatesUpdated += Handler;
            using CancellationTokenRegistration cancelRegistration = cancellationToken.Register(() =>
            {
                global.DetectionResults.CancelPendingDetection();
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                SetGlobalInferenceStatus(
                    applyToCanvas
                        ? "AI 추론 중"
                        : $"일괄 추론 중: {Path.GetFileName(imagePath)}",
                    isBusy: true);
                SetPythonStatus("Python: worker 추론 중");
                AppendLog($"Worker 추론 시작: {Path.GetFileName(imagePath)}");
                SetYoloCommandStatus("AI 추론 요청 중...", isBusy: true);
                bool started = applyToCanvas
                    ? global.DetectionWorkflow.TryStartCurrentImageDetection(
                        global.Data,
                        global.DeepLearning,
                        global.DetectionResults,
                        () => true)
                    : global.DetectionWorkflow.TryStartImagePathDetection(
                        global.Data,
                        global.DeepLearning,
                        global.DetectionResults,
                        imagePath,
                        requestImageSize,
                        () => true);
                if (!started)
                {
                    SetGlobalInferenceStatus("실패: worker 요청 실패", isBusy: false, isWarning: true);
                    SetPythonStatus("Python: worker 요청 실패");
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = FirstNonEmpty(global.GetPythonCommunicationStatusSnapshot().LastError, "Worker 검출 요청을 보내지 못했습니다."),
                        ImagePath = imagePath
                    };
                }

                DetectionCandidatesUpdatedEventArgs completed = await completion.Task.ConfigureAwait(true);
                if (completed.Reason == DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    SetGlobalInferenceStatus("실패: worker 시간 초과", isBusy: false, isWarning: true);
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = "Worker 검출 시간 초과.",
                        ImagePath = imagePath,
                        Errors = new[] { "Worker 검출 시간 초과." }
                    };
                }

                IReadOnlyList<DefectInfo> defects = global.DetectionResults.GetLastDefects();
                IReadOnlyList<YoloWorkerSmokeCandidate> candidates = defects
                    .Select((defect, index) => ToSmokeCandidate(defect, index + 1))
                    .ToList();
                YoloWorkerSmokeCandidate first = candidates.FirstOrDefault();
                var result = new YoloWorkerSmokeTestResult
                {
                    Succeeded = true,
                    Summary = $"YOLO worker 추론 OK. 후보:{candidates.Count}",
                    ImagePath = imagePath,
                    CandidateCount = candidates.Count,
                    FirstClassName = first?.ClassName ?? string.Empty,
                    FirstConfidence = first?.Confidence,
                    Candidates = candidates
                };

                if (applyToCanvas)
                {
                    ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                    SetPythonStatus($"Python: worker OK  후보 {result.CandidateCount}");
                }

                AppendLog($"Worker 추론 시간: {FormatElapsed(stopwatch.Elapsed)}");
                return result;
            }
            catch (OperationCanceledException)
            {
                SetGlobalInferenceStatus("추론 취소", isBusy: false, isWarning: true);
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "Worker 검출 취소.",
                    ImagePath = imagePath,
                    Errors = new[] { "Worker 검출 취소." }
                };
            }
            finally
            {
                global.DetectionResults.DetectionCandidatesUpdated -= Handler;
            }
        }

        private async Task RunBatchDetectionAsync(IReadOnlyList<WpfImageQueueItem> items, string scopeText)
        {
            if (isBatchDetectionRunning || isDetecting)
            {
                AppendLog("검출이 이미 실행 중입니다.");
                return;
            }

            List<WpfImageQueueItem> queue = (items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
                .GroupBy(item => item.ImagePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (queue.Count == 0)
            {
                AppendLog($"일괄 검사 건너뜀. 대상 이미지 없음: {scopeText}");
                return;
            }

            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = new CancellationTokenSource();
            CancellationToken token = batchDetectionCts.Token;
            isBatchDetectionRunning = true;
            batchDetectionTotalCount = queue.Count;
            batchDetectionCompletedCount = 0;
            UpdateBatchDetectionControls(scopeText, string.Empty);
            SetYoloCommandStatus($"일괄 검사 시작: {queue.Count}개", isBusy: true);
            SetGlobalInferenceStatus($"일괄 추론 시작: {queue.Count}개", isBusy: true);

            AppendLog($"일괄 검사 시작. 범위:{scopeText}, 개수:{queue.Count}");
            var batchStopwatch = Stopwatch.StartNew();
            int pendingReviewStatusSaves = 0;
            bool batchFailed = false;
            string batchFailureSummary = string.Empty;
            try
            {
                SetGlobalInferenceStatus($"일괄 추론 worker 준비 중: {queue.Count}개", isBusy: true);
                SetPythonStatus("Python: 일괄 worker 연결 확인 중");
                bool workerReady = await global
                    .EnsurePythonModelClientReadyAsync(GetWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);
                if (!workerReady)
                {
                    batchFailed = true;
                    batchFailureSummary = BuildPythonWorkerFailureText();
                    AppendLog($"일괄 검사 시작 실패: {batchFailureSummary}");
                    return;
                }

                foreach (WpfImageQueueItem item in queue)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
                    ApplyReviewStatusToItem(item, imageReviewStatus.SetDetectionRequested(item.ImagePath, imageName));
                    ShowBatchDetectionImage(item);
                    SetGlobalInferenceStatus($"일괄 추론 {batchDetectionCompletedCount + 1}/{batchDetectionTotalCount}: {Path.GetFileName(item.ImagePath)}", isBusy: true);
                    UpdateBatchDetectionControls(scopeText, Path.GetFileName(item.ImagePath));

                    var itemStopwatch = Stopwatch.StartNew();
                    YoloWorkerSmokeTestResult result = await RunWorkerDetectionForImageAsync(
                        item.ImagePath,
                        applyToCanvas: false,
                        token,
                        workerReadyAlreadyChecked: true).ConfigureAwait(true);
                    TimeSpan itemElapsed = itemStopwatch.Elapsed;
                    result.ElapsedMilliseconds ??= ClampElapsedMilliseconds(itemElapsed);
                    int nextCompleted = batchDetectionCompletedCount + 1;
                    string elapsedText = FormatElapsed(itemElapsed);
                    ApplyDetectionResultToQueueItem(
                        item,
                        result,
                        saveReviewStatus: false,
                        refreshQueueView: false,
                        updateQueueStatusText: false);

                    bool displayedResult = !token.IsCancellationRequested
                        && ApplyBatchDetectionResultToCanvas(item, result);
                    if (result.Succeeded)
                    {
                        AppendLog($"일괄 검사 항목 완료: {nextCompleted}/{batchDetectionTotalCount} {Path.GetFileName(item.ImagePath)} 후보:{result.CandidateCount} / {elapsedText}");
                    }
                    else if (!token.IsCancellationRequested)
                    {
                        AppendLog($"일괄 검사 항목 실패: {nextCompleted}/{batchDetectionTotalCount} {Path.GetFileName(item.ImagePath)} / {elapsedText} / {result.Summary}");
                    }
                    pendingReviewStatusSaves++;
                    if (pendingReviewStatusSaves >= BatchReviewStatusSaveInterval)
                    {
                        imageReviewStatus.SaveReviewStatus(global.Data);
                        pendingReviewStatusSaves = 0;
                    }

                    batchDetectionCompletedCount++;
                    SetPythonStatus($"Python: 일괄 {batchDetectionCompletedCount}/{batchDetectionTotalCount} / 최근 {elapsedText}");
                    UpdateBatchDetectionControls(scopeText, $"{Path.GetFileName(item.ImagePath)} / 최근 {elapsedText}");
                    if (displayedResult)
                    {
                        await YieldBatchDetectionResultFrameAsync(token).ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                bool canceled = token.IsCancellationRequested;
                isBatchDetectionRunning = false;
                if (pendingReviewStatusSaves > 0 || batchDetectionCompletedCount > 0)
                {
                    imageReviewStatus.SaveReviewStatus(global.Data);
                }

                imageQueueView?.Refresh();
                UpdateBatchDetectionControls(canceled ? "중지됨" : "완료", string.Empty);
                SetPythonStatus(canceled ? "Python: 일괄 검사 중지" : "Python: 일괄 검사 완료");
                string totalElapsedText = FormatElapsed(batchStopwatch.Elapsed);
                string averageElapsedText = FormatAverageElapsed(batchStopwatch.Elapsed, batchDetectionCompletedCount);
                SetYoloCommandStatus(
                    canceled
                        ? $"일괄 검사 중지: {batchDetectionCompletedCount}/{batchDetectionTotalCount} / {totalElapsedText}"
                        : $"일괄 검사 완료: {batchDetectionCompletedCount}/{batchDetectionTotalCount} / {totalElapsedText}",
                    isBusy: false);
                SetGlobalInferenceStatus(
                    canceled
                        ? $"일괄 중지: {batchDetectionCompletedCount}/{batchDetectionTotalCount}"
                        : $"일괄 완료: {batchDetectionCompletedCount}/{batchDetectionTotalCount} / {totalElapsedText}",
                    isBusy: false,
                    isWarning: canceled);
                AppendLog(canceled
                    ? $"일괄 검사 중지. 완료:{batchDetectionCompletedCount}/{batchDetectionTotalCount} / 전체:{totalElapsedText} / {averageElapsedText}"
                    : $"일괄 검사 완료. 완료:{batchDetectionCompletedCount}/{batchDetectionTotalCount} / 전체:{totalElapsedText} / {averageElapsedText}");
                if (batchFailed)
                {
                    UpdateBatchDetectionControls("실패", string.Empty);
                    SetPythonStatus("Python: 일괄 검사 실패");
                    SetYoloCommandStatus($"일괄 검사 실패: {batchDetectionCompletedCount}/{batchDetectionTotalCount} / {batchFailureSummary}", isBusy: false);
                    SetGlobalInferenceStatus($"일괄 실패: {batchDetectionCompletedCount}/{batchDetectionTotalCount}", isBusy: false, isWarning: true);
                    AppendLog($"일괄 검사 실패. 완료:{batchDetectionCompletedCount}/{batchDetectionTotalCount} / {batchFailureSummary}");
                }
            }
        }

        private void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
        {
            if (item == null || result == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            YoloImageReviewStatus status = result.Succeeded
                ? result.CandidateCount > 0
                    ? imageReviewStatus.SetDetectionCandidates(item.ImagePath, imageName, result.CandidateCount)
                    : imageReviewStatus.SetDetectionNoCandidates(item.ImagePath, imageName)
                : imageReviewStatus.SetDetectionFailed(item.ImagePath, imageName, result.Summary);
            ApplyReviewStatusToItem(item, status);
            if (saveReviewStatus)
            {
                imageReviewStatus.SaveReviewStatus(global.Data);
            }

            if (refreshQueueView)
            {
                imageQueueView?.Refresh();
            }

            if (updateQueueStatusText)
            {
                UpdateImageQueueStatusText();
            }
        }

        private IReadOnlyList<WpfImageQueueItem> GetVisibleQueueItems()
        {
            return imageQueueView == null
                ? imageQueueItems.ToList()
                : imageQueueView.Cast<object>().OfType<WpfImageQueueItem>().ToList();
        }

        private void UpdateBatchDetectionControls(string scopeText = "", string currentFileName = "")
        {
            bool busy = isBatchDetectionRunning;
            UpdateYoloCommandButtons();

            int total = Math.Max(0, batchDetectionTotalCount);
            int completed = Math.Max(0, batchDetectionCompletedCount);
            int visibleProgress = busy && total > 0 && !string.IsNullOrWhiteSpace(currentFileName)
                ? Math.Min(completed + 1, total)
                : completed;
            if (busy)
            {
                completed = visibleProgress;
            }

            BatchProgressBar.Maximum = total <= 0 ? 1 : total;
            BatchProgressBar.Value = total <= 0 ? 0 : Math.Min(visibleProgress, total);
            BatchStatusText.Text = busy
                ? $"{visibleProgress}/{total}"
                : total > 0 ? $"{completed}/{total}" : "배치 대기";

            if (busy)
            {
                string fileText = string.IsNullOrWhiteSpace(currentFileName) ? string.Empty : $" {currentFileName}";
                SetDatasetStatus($"데이터셋: 일괄 {completed}/{total} {scopeText}{fileText}");
            }
            else
            {
                UpdateImageQueueStatusText();
            }
        }

        private bool ShowBatchDetectionImage(WpfImageQueueItem item)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.ImagePath)
                || !File.Exists(item.ImagePath))
            {
                return false;
            }

            SelectImageQueueItem(item.ImagePath);
            return TryLoadImage(
                item.ImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
        }

        private bool IsActiveImagePath(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase);
        }

        private bool ApplyBatchDetectionResultToCanvas(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (item == null || result == null)
            {
                return false;
            }

            if (!IsActiveImagePath(item.ImagePath) && !ShowBatchDetectionImage(item))
            {
                return false;
            }

            SelectImageQueueItem(item.ImagePath);
            ApplyBatchDetectionCandidates(result.Candidates, result.Succeeded);
            if (!result.Succeeded)
            {
                ShowBatchDetectionFailureResult(item, result);
            }
            else if (pendingDetectionCandidates.Count == 0)
            {
                ShowBatchNoCandidateResult(item, result);
            }

            return true;
        }

        private async Task YieldBatchDetectionResultFrameAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested || Dispatcher == null)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        private void ShowBatchNoCandidateResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelControl?.ViewModel == null)
            {
                return;
            }

            string imageName = Path.GetFileName(item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty);
            CanvasPanelControl.ViewModel.SetDetectionOverlay(
                "AI 검사 결과",
                $"{imageName} / 후보 0개 / 기준 {GetCandidateConfidenceFilter().ToString("P0", CultureInfo.CurrentCulture)}",
                "결과: 검출 후보 없음",
                "현재 기준 신뢰도 이상으로 표시할 AI 후보가 없습니다.",
                WpfDetectionOverlayStatus.Review);
        }

        private void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelControl?.ViewModel == null)
            {
                return;
            }

            string imageName = Path.GetFileName(item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty);
            CanvasPanelControl.ViewModel.SetDetectionOverlay(
                "AI 검사 실패",
                $"{imageName} / 검사 실패",
                string.IsNullOrWhiteSpace(result?.Summary) ? "결과: worker 응답 실패" : result.Summary,
                "YOLO 설정, Python worker 상태, 이미지 경로를 확인하세요.",
                WpfDetectionOverlayStatus.Review);
        }

        private void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            pendingDetectionCandidates.Clear();
            confirmedDetectionCandidates.Clear();
            CandidateReviewViewModel?.ClearReviewHistory();

            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                if (candidate == null)
                {
                    continue;
                }

                pendingDetectionCandidates.Add(candidate);
            }

            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(pendingDetectionCandidates.Count, succeeded);
            AddCandidateReviewHistory(BuildCandidateLoadHistory(pendingDetectionCandidates.Count, succeeded));
            if (pendingDetectionCandidates.Count > 0)
            {
                CandidatesReviewTab.IsSelected = true;
            }

            CenterCanvasAfterInferenceResult();
        }

        private void ApplyDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            pendingDetectionCandidates.Clear();
            confirmedDetectionCandidates.Clear();
            CandidateReviewViewModel?.ClearReviewHistory();

            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                if (candidate == null)
                {
                    continue;
                }

                pendingDetectionCandidates.Add(candidate);
            }

            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(pendingDetectionCandidates.Count, succeeded);
            AddCandidateReviewHistory(BuildCandidateLoadHistory(pendingDetectionCandidates.Count, succeeded));

            if (pendingDetectionCandidates.Count == 0)
            {
                CenterCanvasAfterInferenceResult();
                AppendLog("AI 검출 후보가 없습니다.");
                return;
            }

            CandidatesReviewTab.IsSelected = true;
            CenterCanvasAfterInferenceResult();
            AppendLog($"AI 후보 로드: {pendingDetectionCandidates.Count}개");
        }

        private void AddCandidateReviewHistory(string message)
        {
            CandidateReviewViewModel?.AddReviewHistory(message);
        }

        private string BuildCandidateLoadHistory(int candidateCount, bool succeeded)
        {
            if (!succeeded)
            {
                return "\uD6C4\uBCF4 \uB85C\uB4DC \uC2E4\uD328: worker \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694";
            }

            return candidateCount == 0
                ? "\uD6C4\uBCF4 \uB85C\uB4DC: \uAC80\uCD9C \uD6C4\uBCF4 \uC5C6\uC74C"
                : $"\uD6C4\uBCF4 \uB85C\uB4DC: {candidateCount}\uAC1C / \uAE30\uC900 {GetCandidateConfidenceFilter():P0}";
        }

        private static string BuildCandidateConfirmHistory(string scope, int confirmedCount, int skippedDuplicateCount, bool saved, int savedCount)
        {
            string savedText = saved
                ? $"\uC800\uC7A5 {savedCount}\uAC1C"
                : "\uC800\uC7A5 \uAC74\uB108\uB700";
            string duplicateText = skippedDuplicateCount > 0
                ? $" / \uC911\uBCF5 \uC81C\uC678 {skippedDuplicateCount}\uAC1C"
                : string.Empty;
            return $"\uD655\uC815({scope}): {confirmedCount}\uAC1C / {savedText}{duplicateText}";
        }

        private void CenterCanvasAfterInferenceResult()
        {
            if (MainCanvasViewModel?.ImageViewer == null)
            {
                return;
            }

            MainCanvasViewModel.ImageViewer.ZoomToFit();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainCanvasViewModel?.ImageViewer?.ZoomToFit();
            }), DispatcherPriority.Render);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainCanvasViewModel?.ImageViewer?.ZoomToFit();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void ConfirmCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, string scope)
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                AppendLog("후보를 확정하려면 이미지를 먼저 불러오세요.");
                return;
            }

            var confirmable = (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null && pendingDetectionCandidates.Contains(candidate))
                .Where(IsCandidateConfirmable)
                .ToList();
            if (confirmable.Count == 0)
            {
                int duplicateCount = (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                    .Count(candidate => candidate != null && pendingDetectionCandidates.Contains(candidate) && IsCandidateHighOverlap(candidate));
                AddCandidateReviewHistory(duplicateCount > 0
                    ? $"\uC911\uBCF5 \uC81C\uC678: {duplicateCount}\uAC1C / \uAE30\uC874 \uB77C\uBCA8 \uD655\uC778 \uD544\uC694"
                    : "\uD655\uC815 \uAC00\uB2A5\uD55C AI \uD6C4\uBCF4 \uC5C6\uC74C");
                AppendLog(duplicateCount > 0
                    ? $"중복 가능 후보 {duplicateCount}개는 확정하지 않았습니다. 필요하면 기존 라벨을 확인하거나 후보를 스킵하세요."
                    : "확정 가능한 AI 후보가 없습니다.");
                return;
            }

            int skippedDuplicateCount = (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Count(candidate => candidate != null
                    && pendingDetectionCandidates.Contains(candidate)
                    && IsCandidateHighOverlap(candidate));
            YoloWorkerSmokeCandidate selectedBeforeConfirm = GetSelectedCandidate();
            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(selectedBeforeConfirm, confirmable);
            RegisterAnnotationHistoryBeforeChange($"Confirm {scope}");

            foreach (YoloWorkerSmokeCandidate candidate in confirmable)
            {
                pendingDetectionCandidates.Remove(candidate);
                if (!confirmedDetectionCandidates.Contains(candidate))
                {
                    confirmedDetectionCandidates.Add(candidate);
                }
            }

            bool saved = SaveCurrentAnnotations(out int savedCount);
            AddCandidateReviewHistory(BuildCandidateConfirmHistory(scope, confirmable.Count, skippedDuplicateCount, saved, savedCount));
            RefreshCandidateListWithPreferred(nextCandidate);
            RefreshObjectList();
            RedrawReviewRois();
            PopulateClassList();
            if (saved)
            {
                MarkActiveImageConfirmed();
            }
            if (pendingDetectionCandidates.Count > 0)
            {
                CandidatesReviewTab.IsSelected = true;
                FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            }
            else
            {
                ObjectsReviewTab.IsSelected = true;
            }
                SetPythonStatus($"Python: 대기 {pendingDetectionCandidates.Count} / 확정 {confirmedDetectionCandidates.Count}");

            string savedText = saved ? $" 저장 객체 {savedCount}. {BuildLabelPathSummary()}" : " 저장 건너뜀.";
            AppendLog($"AI 후보 확정({scope}): {confirmable.Count}개;{savedText}");
            if (skippedDuplicateCount > 0)
            {
                AppendLog($"중복 가능 후보 {skippedDuplicateCount}개는 확정에서 제외했습니다.");
            }
        }

        private bool SaveCurrentAnnotations(out int savedCount)
        {
            savedCount = 0;
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return false;
            }

            Dictionary<string, List<CRectangleObject>> roisByClass = BuildAnnotationRois();
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildAnnotationSegments();
            savedCount = roisByClass.Sum(group => group.Value?.Count ?? 0)
                + segmentsByClass.Sum(group => group.Value?.Count ?? 0);
            if (savedCount == 0)
            {
                return false;
            }

            bool saved = LabelingAnnotationPersistence.SaveCurrent(activeImageBitmap, roisByClass, segmentsByClass, global.Data);
            if (saved)
            {
                MarkAnnotationsSaved($"\uC800\uC7A5 \uC644\uB8CC: {savedCount}\uAC1C \uAC1D\uCCB4");
            }

            return saved;
        }

        private Dictionary<string, List<CRectangleObject>> BuildAnnotationRois()
        {
            var roisByClass = new Dictionary<string, List<CRectangleObject>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manualRois.Count; i++)
            {
                AddAnnotationRoi(roisByClass, GetManualRoiClassName(i), manualRois[i]);
            }

            foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
            {
                AddAnnotationRoi(roisByClass, candidate.ClassName, GetClippedCandidateBounds(candidate));
            }

            return roisByClass;
        }

        private Dictionary<string, List<LabelingSegmentationObject>> BuildAnnotationSegments()
        {
            var segmentsByClass = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment == null)
                {
                    continue;
                }

                bool hasRasterMask = segment.IsRasterMask && !segment.Bounds.IsEmpty;
                bool hasPolygon = segment.Points != null && segment.Points.Count >= 3;
                if (!hasRasterMask && !hasPolygon)
                {
                    continue;
                }

                CClassItem classItem = EnsureClassItem(FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect"));
                segment.ClassItem = classItem;
                segment.ClassName = classItem?.Text ?? "Defect";
                if (!segmentsByClass.TryGetValue(segment.ClassName, out List<LabelingSegmentationObject> segments))
                {
                    segments = new List<LabelingSegmentationObject>();
                    segmentsByClass[segment.ClassName] = segments;
                }

                segments.Add(segment);
            }

            return segmentsByClass;
        }

        private void AddAnnotationRoi(
            Dictionary<string, List<CRectangleObject>> roisByClass,
            string className,
            DrawingRectangle bounds)
        {
            if (roisByClass == null || bounds.IsEmpty)
            {
                return;
            }

            CClassItem classItem = EnsureClassItem(className);
            var roiObject = new CRectangleObject
            {
                Roi = bounds,
                cClassItem = classItem
            };

            string normalizedName = classItem?.Text ?? "Defect";
            if (!roisByClass.TryGetValue(normalizedName, out List<CRectangleObject> rois))
            {
                rois = new List<CRectangleObject>();
                roisByClass[normalizedName] = rois;
            }

            rois.Add(roiObject);
        }

        private CClassItem EnsureClassItem(string className)
        {
            global.Data.ClassNamedList ??= new List<CClassItem>();
            string normalizedName = ClassCatalogService.NormalizeClassName(className);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "Defect";
            }

            CClassItem existing = global.Data.ClassNamedList
                .FirstOrDefault(item => string.Equals(item.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            if (ClassCatalogService.TryAddClass(global.Data, normalizedName, out CClassItem added))
            {
                return added;
            }

            return new CClassItem
            {
                Text = normalizedName,
                DrawColor = System.Drawing.Color.Green
            };
        }

        private void RedrawReviewRois()
        {
            EnsureManualRoiMetadataCount();
            using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                MainCanvasViewModel.ClearRois();
                for (int i = 0; i < manualRois.Count; i++)
                {
                    DrawingRectangle roi = manualRois[i];
                    if (roi.IsEmpty)
                    {
                        continue;
                    }

                    var overlay = MainCanvasViewModel.AddInitialRoi(roi, GetManualRoiShapeKind(i));
                    manualRoiOverlayIds[i] = overlay?.UniqueId ?? string.Empty;
                }

                foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
                {
                    DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
                    if (!bounds.IsEmpty)
                    {
                        MainCanvasViewModel.AddInitialRoi(bounds);
                    }
                }

                MainCanvasViewModel.SetDetectionOverlays(BuildDetectionOverlays(pendingDetectionCandidates));
                RefreshPolygonOverlays();
            }

            MainCanvasViewModel.ImageViewer.RefreshGL();
        }

        private IReadOnlyList<RoiImageCanvasDetectionOverlay> BuildDetectionOverlays(IEnumerable<YoloWorkerSmokeCandidate> candidates)
        {
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            return (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null)
                .Select((candidate, index) => new RoiImageCanvasDetectionOverlay
                {
                    Index = index,
                    Bounds = GetClippedCandidateBounds(candidate),
                    Label = BuildDetectionOverlayLabel(candidate, index + 1),
                    IsSelected = ReferenceEquals(candidate, selectedCandidate),
                    Color = ReferenceEquals(candidate, selectedCandidate)
                        ? System.Drawing.Color.FromArgb(80, 180, 255)
                        : IsCandidateConfirmable(candidate)
                        ? System.Drawing.Color.FromArgb(36, 211, 102)
                        : System.Drawing.Color.FromArgb(255, 193, 7)
                })
                .Where(overlay => !overlay.Bounds.IsEmpty)
                .ToList();
        }

        private YoloWorkerSmokeCandidate GetSelectedCandidate()
        {
            if (CandidateReviewViewModel?.SelectedCandidate?.Payload is YoloWorkerSmokeCandidate viewModelCandidate)
            {
                return viewModelCandidate;
            }

            return null;
        }

        private bool FocusSelectedCandidateInViewer(bool logIfMissing)
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                if (logIfMissing)
                {
                    AppendLog("초점을 맞출 AI 후보를 선택하세요.");
                }

                return false;
            }

            return FocusCandidateInViewer(candidate, logIfMissing);
        }

        private bool FocusCandidateInViewer(YoloWorkerSmokeCandidate candidate, bool logIfMissing)
        {
            if (candidate == null || activeImageSize.IsEmpty)
            {
                if (logIfMissing)
                {
                    AppendLog("후보 초점 이동을 하려면 먼저 이미지를 불러오세요.");
                }

                return false;
            }

            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                if (logIfMissing)
                {
                    AppendLog("후보 영역이 이미지 범위 밖에 있습니다.");
                }

                return false;
            }

            MainCanvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRect(bounds));
            SetModelStatus($"후보 초점: {candidate.ClassName} {candidate.Confidence:P1}  {WpfCandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            return true;
        }

        private DrawingRectangleF BuildCandidateFocusRect(DrawingRectangle bounds)
        {
            float padding = Math.Max(12F, Math.Max(bounds.Width, bounds.Height) * 0.65F);
            float left = Math.Max(0F, bounds.Left - padding);
            float right = Math.Min(activeImageSize.Width, bounds.Right + padding);
            float top = Math.Max(0F, bounds.Top - padding);
            float bottom = Math.Min(activeImageSize.Height, bounds.Bottom + padding);

            if (right <= left)
            {
                right = Math.Min(activeImageSize.Width, left + 1F);
            }

            if (bottom <= top)
            {
                bottom = Math.Min(activeImageSize.Height, top + 1F);
            }

            return DrawingRectangleF.FromLTRB(
                left,
                activeImageSize.Height - bottom,
                right,
                activeImageSize.Height - top);
        }

        private void UpdateDetectionResultOverlay()
        {
            if (CanvasPanelControl?.ViewModel == null)
            {
                return;
            }

            if (pendingDetectionCandidates.Count == 0)
            {
                CanvasPanelControl.ViewModel.ClearDetectionOverlay();
                return;
            }

            string imageName = string.IsNullOrWhiteSpace(activeImagePath)
                ? "-"
                : Path.GetFileName(activeImagePath);
            string summary =
                $"{imageName} / 후보 {pendingDetectionCandidates.Count}개 / 기준 {GetCandidateConfidenceFilter().ToString("P0", CultureInfo.CurrentCulture)}";

            YoloWorkerSmokeCandidate selected = GetSelectedCandidate() ?? pendingDetectionCandidates.FirstOrDefault();
            bool selectedDuplicate = selected != null && IsCandidateHighOverlap(selected);
            bool selectedConfirmable = selected != null && IsCandidateConfirmable(selected);
            string selectedText = selected == null
                ? "선택 후보 없음"
                : $"선택: {BuildDetectionOverlayLabel(selected, pendingDetectionCandidates.IndexOf(selected) + 1)} / {BuildCandidateSecondaryText(selected)}";
            string detail = string.Join(
                "\n",
                pendingDetectionCandidates
                    .Take(4)
                    .Select((candidate, index) => $"{index + 1}. {GetCandidateClassName(candidate)} {FormatCandidateConfidence(candidate, "P1")}  {BuildCandidateSecondaryText(candidate)}"));
            WpfDetectionOverlayStatus status = selectedDuplicate
                ? WpfDetectionOverlayStatus.Duplicate
                : selectedConfirmable
                    ? WpfDetectionOverlayStatus.Confirmable
                    : WpfDetectionOverlayStatus.Review;

            CanvasPanelControl.ViewModel.SetDetectionOverlay(
                "AI 검출 결과",
                summary,
                selectedText,
                detail,
                status);
        }

        private string BuildDetectionOverlayLabel(YoloWorkerSmokeCandidate candidate, int fallbackIndex)
        {
            return WpfCandidateReviewPresenter.BuildDetectionOverlayLabel(candidate, fallbackIndex);
        }

        private DrawingRectangle GetClippedCandidateBounds(YoloWorkerSmokeCandidate candidate)
        {
            if (candidate == null)
            {
                return DrawingRectangle.Empty;
            }

            DrawingRectangle bounds = candidate.ToRectangle();
            if (bounds.IsEmpty || activeImageSize.IsEmpty)
            {
                return bounds;
            }

            return DrawingRectangle.Intersect(
                bounds,
                new DrawingRectangle(0, 0, activeImageSize.Width, activeImageSize.Height));
        }

        private void RefreshCandidateList()
        {
            RefreshCandidateListViewModel(null);
        }

        private void RefreshCandidateListWithPreferred(YoloWorkerSmokeCandidate preferredCandidate)
        {
            RefreshCandidateListViewModel(preferredCandidate);
        }

        private void RefreshCandidateListViewModel(YoloWorkerSmokeCandidate preferredCandidate)
        {
            var rows = new List<WpfCandidateReviewListItem>();

            if (pendingDetectionCandidates.Count == 0)
            {
                string detail = "AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                rows.Add(WpfCandidateReviewListItem.Empty(
                    "AI \uD6C4\uBCF4 \uC5C6\uC74C",
                    "\uAC80\uCD9C \uACB0\uACFC \uD6C4\uBCF4\uAC00 \uC5C6\uAC70\uB098 \uC544\uC9C1 \uAC80\uC0AC\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4."));
                CandidateReviewViewModel.SetCandidates(rows, detail);
                UpdateCandidateActionState();
                UpdateDetectionResultOverlay();
                return;
            }

            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            if (visibleCandidates.Count == 0)
            {
                string detail = $"{GetCandidateConfidenceFilter():P0} \uC774\uC0C1 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                rows.Add(WpfCandidateReviewListItem.Empty(
                    "\uD544\uD130 \uD1B5\uACFC \uD6C4\uBCF4 \uC5C6\uC74C",
                    "\uC2E0\uB8B0\uB3C4 \uAE30\uC900\uC744 \uB0AE\uCD94\uBA74 \uC228\uACA8\uC9C4 \uD6C4\uBCF4\uB97C \uB2E4\uC2DC \uBCFC \uC218 \uC788\uC2B5\uB2C8\uB2E4."));
                CandidateReviewViewModel.SetCandidates(rows, detail);
                UpdateCandidateActionState();
                UpdateDetectionResultOverlay();
                return;
            }

            for (int i = 0; i < visibleCandidates.Count; i++)
            {
                YoloWorkerSmokeCandidate candidate = visibleCandidates[i];
                int displayIndex = candidate.Index > 0 ? candidate.Index : i + 1;
                rows.Add(CreateCandidateReviewItem(candidate, displayIndex));
            }

            CandidateReviewViewModel.SetCandidates(rows, string.Empty, preferredCandidate);
            YoloWorkerSmokeCandidate selected = GetSelectedCandidate();
            if (selected != null)
            {
                ApplyCandidateSelectionReview(selected);
            }
            else
            {
                ApplyCandidateSelectionReview(null);
            }

            UpdateCandidateActionState();
            UpdateDetectionResultOverlay();
        }

        private WpfCandidateReviewListItem CreateCandidateReviewItem(YoloWorkerSmokeCandidate candidate, int displayIndex)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildListItem(
                candidate,
                displayIndex,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private void RefreshObjectList()
        {
            RefreshObjectListViewModel(null);
        }

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef preferredSelection)
        {
            RefreshObjectListViewModel(preferredSelection);
        }

        private void RefreshObjectListViewModel(WpfObjectReviewItemRef preferredSelection)
        {
            WpfObjectReviewItemRef previousSelection = null;
            TryGetSelectedObjectReviewItem(out previousSelection);
            WpfObjectReviewItemRef nextSelection = preferredSelection ?? previousSelection;

            int objectCount = manualRois.Count + manualSegments.Count + confirmedDetectionCandidates.Count;
            string summary = WpfObjectReviewPresenter.BuildSummary(objectCount);
            var rows = new List<WpfObjectReviewListItem>();

            if (objectCount == 0)
            {
                rows.Add(WpfObjectReviewListItem.Empty(WpfObjectReviewPresenter.EmptyText));
                SetObjectReviewObjects(rows, summary, nextSelection);
                UpdateObjectReviewActionState();
                return;
            }

            for (int i = 0; i < manualRois.Count; i++)
            {
                rows.Add(BuildManualRoiObjectReviewItem(i));
            }

            for (int i = 0; i < manualSegments.Count; i++)
            {
                LabelingSegmentationObject segment = manualSegments[i];
                if (segment == null)
                {
                    continue;
                }

                string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
                string shapeName = segment.IsRasterMask ? "Mask" : "Polygon";
                WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.ManualSegment(i);
                rows.Add(WpfObjectReviewPresenter.BuildManualItem(
                    manualRois.Count + i + 1,
                    className,
                    segment.Bounds,
                    shapeName,
                    payload.Source.ToString(),
                    payload.Index,
                    payload));
            }

            for (int i = 0; i < confirmedDetectionCandidates.Count; i++)
            {
                YoloWorkerSmokeCandidate candidate = confirmedDetectionCandidates[i];
                WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.ConfirmedAi(i);
                int displayIndex = candidate?.Index > 0 ? candidate.Index : i + 1;
                DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
                rows.Add(WpfObjectReviewPresenter.BuildConfirmedItem(
                    candidate,
                    displayIndex,
                    bounds,
                    FormatCandidateDetail(candidate),
                    payload.Source.ToString(),
                    payload.Index,
                    payload));
            }

            SetObjectReviewObjects(rows, summary, nextSelection);
            UpdateObjectReviewActionState();
        }

        private WpfObjectReviewListItem BuildManualRoiObjectReviewItem(int index)
        {
            DrawingRectangle roi = manualRois[index];
            string className = GetManualRoiClassName(index);
            WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.Manual(index, GetManualRoiOverlayId(index));
            return WpfObjectReviewPresenter.BuildManualItem(
                index + 1,
                className,
                roi,
                FormatManualRoiShapeName(GetManualRoiShapeKind(index)),
                payload.Source.ToString(),
                payload.Index,
                payload);
        }

        private bool TryRefreshManualRoiObjectReviewRow(int manualRoiIndex, bool select)
        {
            if (manualRoiIndex < 0
                || manualRoiIndex >= manualRois.Count
                || ObjectReviewViewModel?.Objects == null
                || manualRoiIndex >= ObjectReviewViewModel.Objects.Count)
            {
                return false;
            }

            WpfObjectReviewListItem currentRow = ObjectReviewViewModel.Objects[manualRoiIndex];
            if (!string.Equals(currentRow?.SourceKey, WpfObjectReviewSource.ManualRoi.ToString(), StringComparison.OrdinalIgnoreCase)
                || currentRow.SourceIndex != manualRoiIndex)
            {
                return false;
            }

            suppressObjectReviewSelectionChanged = true;
            try
            {
                return ObjectReviewViewModel.TryReplaceObject(
                    manualRoiIndex,
                    BuildManualRoiObjectReviewItem(manualRoiIndex),
                    select);
            }
            finally
            {
                suppressObjectReviewSelectionChanged = false;
                SyncObjectClassEditorToSelection();
                UpdateObjectReviewActionState();
            }
        }

        private void SetObjectReviewObjects(
            IEnumerable<WpfObjectReviewListItem> rows,
            string summary,
            WpfObjectReviewItemRef selectedItem)
        {
            // Rebuilding the side list temporarily clears WPF SelectedItem. During ROI click/drag
            // that transient null must not clear the active canvas ROI handles.
            suppressObjectReviewSelectionChanged = true;
            try
            {
                ObjectReviewViewModel.SetObjects(
                    rows,
                    summary,
                    selectedItem?.Source.ToString() ?? string.Empty,
                    selectedItem?.Index ?? -1);
            }
            finally
            {
                suppressObjectReviewSelectionChanged = false;
            }

            SyncObjectClassEditorToSelection();
        }

        private string GetManualRoiClassName(int index)
        {
            if (index >= 0 && index < manualRoiClassNames.Count
                && !string.IsNullOrWhiteSpace(manualRoiClassNames[index]))
            {
                return manualRoiClassNames[index];
            }

            return "Defect";
        }

        private void ObjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressObjectReviewSelectionChanged)
            {
                UpdateObjectReviewActionState();
                return;
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            bool isManualSegmentSelected = IsSelectedManualSegment();
            if (activeAnnotationTool == WpfAnnotationTool.Select)
            {
                MainCanvasViewModel.IsImagePointInputMode = isManualSegmentSelected;
            }

            if (!string.Equals(
                ObjectReviewViewModel?.SelectedObject?.SourceKey,
                WpfObjectReviewSource.ManualRoi.ToString(),
                StringComparison.OrdinalIgnoreCase))
            {
                MainCanvasViewModel.ClearRoiSelection();
            }

            RefreshPolygonOverlays();
        }

        private bool IsSelectedManualSegment()
        {
            return string.Equals(
                ObjectReviewViewModel?.SelectedObject?.SourceKey,
                WpfObjectReviewSource.ManualSegment.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyObjectClassButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return;
            }

            string className = WpfObjectReviewEditService.NormalizeClassName(ObjectReviewViewModel?.SelectedClassName);
            EnsureClassItem(className);
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("Change object class");
            if (!WpfObjectReviewEditService.TryApplyClass(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                confirmedDetectionCandidates,
                className,
                out string appliedClassName))
            {
                return;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            RefreshObjectList();
            RefreshPolygonOverlays();

            AppendLog($"Changed object class: {appliedClassName}");
        }

        private void DeleteObjectButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedObject();
        }

        private void ObjectListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete && e.Key != Key.Back)
            {
                return;
            }

            e.Handled = DeleteSelectedObject();
        }

        private bool DeleteSelectedObject()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return false;
            }

            int selectedObjectRowIndex = GetSelectedObjectReviewRowIndex();
            string manualOverlayId = item.Source == WpfObjectReviewSource.ManualRoi
                ? GetManualRoiOverlayId(item.Index)
                : string.Empty;
            string removedText = ObjectReviewViewModel?.SelectedObject?.DisplayText
                ?? "object";
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("Delete object");
            if (!WpfObjectReviewEditService.TryDelete(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                confirmedDetectionCandidates))
            {
                UpdateObjectReviewActionState();
                return false;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                RemoveAtIfPresent(manualRoiShapeKinds, item.Index);
                RemoveAtIfPresent(manualRoiOverlayIds, item.Index);
                if (!RemoveCanvasRoiOverlayById(manualOverlayId))
                {
                    RedrawReviewRois();
                }

                ClearCanvasRoiSelectionAfterDelete(manualOverlayId);
            }
            else if (item.Source == WpfObjectReviewSource.ManualSegment)
            {
                RefreshPolygonOverlays();
            }
            else
            {
                RedrawReviewRois();
            }

            RefreshObjectReviewAfterDelete(item.Source, selectedObjectRowIndex);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            AppendLog($"Removed object from review: {removedText}");
            return true;
        }

        private bool TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
        {
            item = null;
            if (ObjectReviewViewModel?.SelectedObject?.Payload is WpfObjectReviewItemRef viewModelItem)
            {
                item = ResolveObjectReviewItem(viewModelItem);
                return item != null;
            }

            return false;
        }

        private WpfObjectReviewItemRef ResolveObjectReviewItem(WpfObjectReviewItemRef item)
        {
            if (item?.Source != WpfObjectReviewSource.ManualRoi)
            {
                return item;
            }

            int manualIndex = ResolveManualRoiIndex(item);
            return manualIndex >= 0
                ? WpfObjectReviewItemRef.Manual(manualIndex, GetManualRoiOverlayId(manualIndex))
                : null;
        }

        private int ResolveManualRoiIndex(WpfObjectReviewItemRef item)
        {
            if (!string.IsNullOrWhiteSpace(item?.SourceId))
            {
                int indexByOverlay = FindManualRoiIndexByOverlayId(item.SourceId);
                if (indexByOverlay >= 0)
                {
                    return indexByOverlay;
                }
            }

            return item != null && item.Index >= 0 && item.Index < manualRois.Count
                ? item.Index
                : -1;
        }

        private int GetSelectedObjectReviewRowIndex()
        {
            WpfObjectReviewListItem selected = ObjectReviewViewModel?.SelectedObject;
            return selected != null && ObjectReviewViewModel?.Objects != null
                ? ObjectReviewViewModel.Objects.IndexOf(selected)
                : -1;
        }

        private bool RemoveCanvasRoiOverlayById(string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId) || MainCanvasViewModel?.ImageViewer == null)
            {
                return false;
            }

            var overlayItem = MainCanvasViewModel.ImageViewer.GetCanvasOverlayManager().GetOverlayByUniqueId(overlayId);
            string groupName = overlayItem?.Parent?.GroupType
                ?? overlayItem?.Shape?.GroupType
                ?? MainCanvasViewModel.ImageViewer.GetCanvasOverlayManager().LastGroupType
                ?? string.Empty;
            OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlOverlayExtensions.DeleteOverlay(
                MainCanvasViewModel.ImageViewer,
                overlayId,
                groupName);
            return overlayItem != null;
        }

        private void ClearCanvasRoiSelectionAfterDelete(string overlayId)
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            if (!MainCanvasViewModel.ClearDeletedRoiSelection(overlayId))
            {
                MainCanvasViewModel.ClearRoiSelection();
            }
        }

        private void RefreshObjectReviewAfterDelete(WpfObjectReviewSource deletedSource, int deletedObjectRowIndex)
        {
            int objectCount = manualRois.Count + manualSegments.Count + confirmedDetectionCandidates.Count;
            if (deletedSource != WpfObjectReviewSource.ManualRoi
                || objectCount == 0
                || objectCount <= ObjectReviewFullRefreshDeleteLimit
                || ObjectReviewViewModel?.Objects == null
                || deletedObjectRowIndex < 0
                || deletedObjectRowIndex >= ObjectReviewViewModel.Objects.Count)
            {
                RefreshObjectList();
                return;
            }

            // For very large ROI lists, keep operations object-local. SourceId keeps later
            // commands correct even though hidden rows are renumbered on the next full refresh.
            suppressObjectReviewSelectionChanged = true;
            try
            {
                if (!ObjectReviewViewModel.TryRemoveObject(
                    deletedObjectRowIndex,
                    WpfObjectReviewPresenter.BuildSummary(objectCount),
                    deletedObjectRowIndex))
                {
                    RefreshObjectList();
                    return;
                }
            }
            finally
            {
                suppressObjectReviewSelectionChanged = false;
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
        }

        private void UpdateObjectReviewActionState()
        {
            ObjectReviewViewModel?.RefreshActionState();
        }

        private void SyncObjectClassEditorToSelection()
        {
            if (ObjectReviewViewModel == null)
            {
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                ObjectReviewViewModel.SelectedClassName = string.Empty;
                return;
            }

            string className = WpfObjectReviewEditService.GetClassName(
                item,
                manualRoiClassNames,
                manualSegments,
                confirmedDetectionCandidates);
            ObjectReviewViewModel.SetSelectedObjectClass(GetClassNames(), className);
        }

        private void RefreshObjectClassOptions(string selectedName = "")
        {
            if (ObjectReviewViewModel == null)
            {
                return;
            }

            string viewModelSelection = string.IsNullOrWhiteSpace(selectedName)
                ? ObjectReviewViewModel.SelectedClassName
                : selectedName;
            ObjectReviewViewModel.SetClassNames(GetClassNames(), viewModelSelection);
        }

        private IReadOnlyList<string> GetClassNames()
        {
            EnsureClassItem("Defect");
            return global.Data.ClassNamedList
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void UpdateCandidateActionState()
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            bool hasVisibleCandidates = visibleCandidates.Count > 0 && !isDetecting;
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            bool hasSelectedCandidate = selectedCandidate != null;
            bool selectedConfirmable = hasVisibleCandidates && hasSelectedCandidate && IsCandidateConfirmable(selectedCandidate);
            bool hasConfirmableCandidates = hasVisibleCandidates && visibleCandidates.Any(IsCandidateConfirmable);
            bool canNavigateCandidates = hasVisibleCandidates && hasSelectedCandidate && visibleCandidates.Count > 1;
            bool canFocusCandidate = hasVisibleCandidates && hasSelectedCandidate;
            CandidateReviewViewModel?.SetActionState(
                selectedConfirmable,
                hasConfirmableCandidates,
                hasVisibleCandidates && hasSelectedCandidate,
                selectedConfirmable ? "\uC120\uD0DD AI \uD6C4\uBCF4 \uD655\uC815" : BuildCandidateConfirmDisabledHintText(selectedCandidate),
                hasConfirmableCandidates ? "\uD45C\uC2DC\uB41C \uD655\uC815 \uAC00\uB2A5 \uD6C4\uBCF4 \uC804\uCCB4 \uD655\uC815" : "\uD655\uC815 \uAC00\uB2A5\uD55C \uD45C\uC2DC \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC911\uBCF5 \uAC00\uB2A5 \uD6C4\uBCF4\uB294 \uC81C\uC678\uD569\uB2C8\uB2E4.",
                hasSelectedCandidate ? "\uC120\uD0DD AI \uD6C4\uBCF4 \uC2A4\uD0B5" : "\uC2A4\uD0B5\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
            CandidateReviewViewModel?.SetNavigationState(canNavigateCandidates, canNavigateCandidates, canFocusCandidate);
            UpdateCanvasCommandButtons();
        }

        private void UpdateCanvasCommandButtons()
        {
            bool hasImage = activeImageBitmap != null && !activeImageSize.IsEmpty && !isDetecting;
            bool hasSelectedCandidate = hasImage && GetSelectedCandidate() != null;
            bool hasPendingCandidates = hasImage && pendingDetectionCandidates.Count > 0 && !isDetecting;

            if (CanvasPanelControl?.ViewModel != null)
            {
                CanvasPanelControl.ViewModel.SetCommandAvailability(hasImage, hasSelectedCandidate, hasPendingCandidates);
                return;
            }

            SetControlEnabled(FitCanvasButton, hasImage);
            SetControlEnabled(ActualSizeCanvasButton, hasImage);
            SetControlEnabled(PanCanvasButton, hasImage);
            SetControlEnabled(FocusCandidateCanvasButton, hasSelectedCandidate);
            SetControlEnabled(ResetAiOverlayCanvasButton, hasPendingCandidates);
        }

        private string BuildCandidateConfirmDisabledHintText(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildConfirmDisabledHint(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds));
        }

        private IReadOnlyList<YoloWorkerSmokeCandidate> GetVisibleCandidateList()
        {
            double minimum = GetCandidateConfidenceFilter();
            return pendingDetectionCandidates
                .Where(candidate => candidate != null && candidate.Confidence >= minimum)
                .ToList();
        }

        private double GetCandidateConfidenceFilter()
        {
            return CandidateConfidenceSlider == null
                ? 0D
                : Math.Clamp(CandidateConfidenceSlider.Value, 0D, 1D);
        }

        private float GetMinimumDetectionConfidence()
        {
            return global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0F;
        }

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(YoloWorkerSmokeCandidate candidate)
        {
            return GetCandidateOverlapInfo(GetClippedCandidateBounds(candidate));
        }

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(DrawingRectangle candidateBounds)
        {
            (string label, DrawingRectangle bounds, double iou) = FindBestCurrentObjectOverlapInfo(candidateBounds);
            return new WpfCandidateOverlapInfo(label, bounds, iou);
        }

        private bool IsCandidateConfirmable(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.IsConfirmable(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private bool IsCandidateHighOverlap(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.IsHighOverlap(GetCandidateOverlapInfo(bounds));
        }

        private void UpdateCandidateConfidenceText()
        {
            string text = GetCandidateConfidenceFilter().ToString("P0", CultureInfo.CurrentCulture);
            if (CandidateReviewViewModel != null)
            {
                CandidateReviewViewModel.ConfidenceText = text;
            }
        }

        private void ApplyCandidateSelectionReview(YoloWorkerSmokeCandidate candidate)
        {
            if (CandidateReviewViewModel != null)
            {
                if (candidate == null)
                {
                    CandidateReviewViewModel.ApplySelectionReview("선택된 AI 후보가 없습니다.", default, showComparison: false);
                    return;
                }

                DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
                WpfCandidateComparisonPresentation comparison = WpfCandidateReviewPresenter.BuildComparison(
                    candidate,
                    bounds,
                    GetCandidateOverlapInfo(bounds));
                CandidateReviewViewModel.ApplySelectionReview(
                    FormatCandidateDetail(candidate),
                    comparison,
                    showComparison: true);
            }
        }

        private static YoloWorkerSmokeCandidate ToSmokeCandidate(DefectInfo defect, int index)
        {
            return new YoloWorkerSmokeCandidate
            {
                Index = index,
                ClassName = string.IsNullOrWhiteSpace(defect?.ClassName) ? "Defect" : defect.ClassName,
                Confidence = defect?.Confidence ?? 0D,
                X = defect?.X ?? 0D,
                Y = defect?.Y ?? 0D,
                Width = defect?.Width ?? 0D,
                Height = defect?.Height ?? 0D
            };
        }

        private string FormatCandidate(YoloWorkerSmokeCandidate candidate)
        {
            return WpfCandidateReviewPresenter.FormatCandidate(candidate, GetClippedCandidateBounds(candidate));
        }

        private string FormatCandidateDetail(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildDetail(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private string BuildCandidateSecondaryText(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildSecondaryText(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private string BuildCandidateCurrentObjectComparison(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildCurrentObjectComparison(
                bounds,
                GetCandidateOverlapInfo(bounds));
        }

        private (string label, DrawingRectangle bounds, double iou) FindBestCurrentObjectOverlapInfo(DrawingRectangle candidateBounds)
        {
            string bestLabel = string.Empty;
            DrawingRectangle bestBounds = DrawingRectangle.Empty;
            double bestIou = 0D;

            for (int i = 0; i < manualRois.Count; i++)
            {
                double iou = CalculateIntersectionOverUnion(candidateBounds, manualRois[i]);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestLabel = $"수동 {GetManualRoiClassName(i)}";
                    bestBounds = manualRois[i];
                }
            }

            for (int i = 0; i < confirmedDetectionCandidates.Count; i++)
            {
                DrawingRectangle confirmedBounds = GetClippedCandidateBounds(confirmedDetectionCandidates[i]);
                double iou = CalculateIntersectionOverUnion(candidateBounds, confirmedBounds);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestLabel = $"AI {GetCandidateClassName(confirmedDetectionCandidates[i])}";
                    bestBounds = confirmedBounds;
                }
            }

            return bestIou <= 0D ? (string.Empty, DrawingRectangle.Empty, 0D) : (bestLabel, bestBounds, bestIou);
        }

        private static string FormatBoundsCompact(DrawingRectangle bounds)
        {
            return WpfCandidateReviewPresenter.FormatBoundsCompact(bounds);
        }

        private static double CalculateIntersectionOverUnion(DrawingRectangle first, DrawingRectangle second)
        {
            if (first.IsEmpty || second.IsEmpty)
            {
                return 0D;
            }

            DrawingRectangle intersection = DrawingRectangle.Intersect(first, second);
            if (intersection.IsEmpty)
            {
                return 0D;
            }

            double intersectionArea = intersection.Width * intersection.Height;
            double unionArea = (first.Width * first.Height) + (second.Width * second.Height) - intersectionArea;
            return unionArea <= 0D ? 0D : intersectionArea / unionArea;
        }

        private static string GetCandidateClassName(YoloWorkerSmokeCandidate candidate)
        {
            return WpfCandidateReviewPresenter.GetClassName(candidate);
        }

        private static string FormatCandidateConfidence(YoloWorkerSmokeCandidate candidate, string format)
        {
            return WpfCandidateReviewPresenter.FormatConfidence(candidate, format);
        }

        private string BuildLabelPathSummary()
        {
            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(global.Data.LastSelectImageName, global.Data);
            return labelPaths.Count == 0
                ? "라벨 경로: 확인 안 됨"
                : $"라벨: {labelPaths[0]}";
        }

        private void EnsureProjectSettings()
        {
            global.Data.ProjectSettings ??= new LabelingProjectSettings();
            global.Data.ProjectSettings.EnsureDefaults();
        }

        public int LoadImageQueueFromRoot(string imageRoot, string selectedImagePath = "", bool loadFirstImage = false, bool refreshDetails = true)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                SetDatasetStatus("데이터셋: 이미지 루트 없음");
                AppendLog($"Image root does not exist: {imageRoot}");
                return 0;
            }

            currentImageRoot = imageRoot;
            imageQueueDetailLoadCts?.Cancel();
            imageQueueDetailLoadCts?.Dispose();
            imageQueueDetailLoadCts = new CancellationTokenSource();

            List<string> imagePaths = EnumerateImageFiles(imageRoot);
            imageReviewStatus.SetImages(imagePaths);
            imageReviewStatus.LoadReviewStatus(global.Data, imagePaths);

            suppressImageQueueSelection = true;
            try
            {
                imageQueueItems.Clear();
                foreach (string imagePath in imagePaths)
                {
                    imageQueueItems.Add(WpfImageQueueItem.CreateShell(imagePath));
                }

                imageQueueView?.Refresh();
                SelectImageQueueItem(selectedImagePath);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateImageQueueStatusText();
            if (refreshDetails)
            {
                StartImageQueueDetailRefresh(imagePaths, imageQueueDetailLoadCts.Token);
            }

            string targetPath = !string.IsNullOrWhiteSpace(selectedImagePath) && File.Exists(selectedImagePath)
                ? selectedImagePath
                : imagePaths.FirstOrDefault();
            if (loadFirstImage && !string.IsNullOrWhiteSpace(targetPath))
            {
                TryLoadImage(targetPath);
            }

            return imagePaths.Count;
        }

        private void PopulateImageQueue(string imageRoot, string selectedImagePath, bool refreshDetails = true)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return;
            }

            if (imageQueueItems.Count == 0
                || !string.Equals(Path.GetFullPath(imageRoot), Path.GetFullPath(currentImageRoot ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            {
                LoadImageQueueFromRoot(imageRoot, selectedImagePath, loadFirstImage: false, refreshDetails: refreshDetails);
                return;
            }

            SelectImageQueueItem(selectedImagePath);
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }

        private static List<string> EnumerateImageFiles(string imageRoot)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return new List<string>();
            }

            return Directory
                .EnumerateFiles(imageRoot)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async void StartImageQueueDetailRefresh(IReadOnlyList<string> imagePaths, CancellationToken token)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                return;
            }

            int loadedCount = 0;
            foreach (string imagePath in imagePaths)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                WpfImageQueueItem item = FindImageQueueItem(imagePath);
                if (item == null)
                {
                    continue;
                }

                try
                {
                    WpfImageQueueDetail detail = await Task.Run(() => BuildImageQueueDetail(imagePath), token).ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    ApplyImageQueueDetail(item, detail);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    item.LabelStatus = "확인 실패";
                    item.DetectStatus = "대기";
                    AppendLog($"Image status failed: {Path.GetFileName(imagePath)}  {ex.Message}");
                }

                loadedCount++;
                if (loadedCount == 1 || loadedCount % 20 == 0 || loadedCount == imagePaths.Count)
                {
                    imageQueueView?.Refresh();
                    UpdateImageQueueStatusText(loadedCount, imagePaths.Count);
                }
            }
        }

        private WpfImageQueueDetail BuildImageQueueDetail(string imagePath)
        {
            return WpfImageQueueDetailLoader.Build(imagePath, imageReviewStatus, global.Data);
        }

        private void ApplyImageQueueDetail(WpfImageQueueItem item, WpfImageQueueDetail detail)
        {
            if (item == null || detail == null)
            {
                return;
            }

            item.Dimensions = WpfImageQueueDetailLoader.FormatImageSize(detail.ImageSize);
            ApplyReviewStatusToItem(item, detail.ReviewStatus);
        }

        private void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
        {
            if (item == null || status == null)
            {
                return;
            }

            item.LabelStatus = FormatLabelStatusForQueue(status.LabelText);
            item.DetectStatus = FormatDetectionStatusForQueue(status);
            item.IsLabeled = status.IsLabeled;
            item.ReviewState = status.ReviewState;
            item.QueueIconKind = GetQueueIconKind(status);
            item.QueueIconBrush = GetQueueIconBrush(status);
            item.QueueBadgeText = BuildQueueBadgeText(status);
            item.QueueStatusSummary = BuildQueueStatusSummary(status);
            item.Detail = BuildReviewDetailText(status);
            RefreshYoloTrainingStepCompletion();
        }

        private void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            YoloImageReviewStatus status = imageReviewStatus.RefreshLabelStatusAndReviewState(
                activeImagePath,
                activeImageSize,
                global.Data,
                hasActiveCandidates);
            ApplyReviewStatusToItem(item, status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            // Delete must feel immediate. Label-file recount and review-state JSON writes are
            // bookkeeping, so run them after the canvas/list have already updated.
            Dispatcher.BeginInvoke(
                new Action(() => RefreshActiveImageQueueStatus(hasActiveCandidates)),
                DispatcherPriority.Background);
        }

        private void SetActiveImageDetectionStatus(int candidateCount, bool succeeded)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = succeeded
                ? candidateCount > 0
                    ? imageReviewStatus.SetDetectionCandidates(activeImagePath, imageName, candidateCount)
                    : imageReviewStatus.SetDetectionNoCandidates(activeImagePath, imageName)
                : imageReviewStatus.SetDetectionFailed(activeImagePath, imageName, "Detection failed.");
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageConfirmed()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus status = imageReviewStatus.MarkConfirmed(activeImagePath, Path.GetFileNameWithoutExtension(activeImagePath));
            if (!activeImageSize.IsEmpty)
            {
                status = imageReviewStatus.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageSkippedOrCandidate()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = pendingDetectionCandidates.Count > 0
                ? imageReviewStatus.SetDetectionCandidates(activeImagePath, imageName, pendingDetectionCandidates.Count)
                : imageReviewStatus.MarkSkipped(activeImagePath, imageName);
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void SelectImageQueueItem(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(imagePath);
            if (item == null)
            {
                return;
            }

            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.SelectedItem = item;
                ImageQueueGrid.ScrollIntoView(item);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(item);
        }

        private WpfImageQueueItem FindImageQueueItem(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            return imageQueueItems.FirstOrDefault(item =>
                string.Equals(item.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase));
        }

        private bool ShouldShowImageQueueItem(WpfImageQueueItem item)
        {
            return WpfImageQueueFilterService.ShouldShow(item, ImageQueueSearchBox?.Text, GetSelectedImageQueueFilter());
        }

        private void UpdateImageQueueStatusText(int loadedCount = -1, int totalToLoad = -1)
        {
            int visibleCount = imageQueueView?.Cast<object>().Count() ?? imageQueueItems.Count;
            UpdateQueueQuickFilterButtons();
            SetDatasetStatus(WpfImageQueueFilterService.BuildDatasetStatusText(
                imageQueueItems,
                visibleCount,
                GetSelectedImageQueueFilter(),
                loadedCount,
                totalToLoad));
        }

        private void UpdateQueueQuickFilterButtons()
        {
            WpfImageQueueFilter filter = GetSelectedImageQueueFilter();
            ImageQueuePanelControl?.ViewModel.SetQuickFilterState(
                filter,
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Candidate),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Failed),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Confirmed),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Skipped),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.NoCandidate));
        }

        private WpfImageQueueFilter GetSelectedImageQueueFilter()
        {
            return (ImageQueueFilterBox?.SelectedItem as WpfImageQueueFilterOption)?.Filter
                ?? WpfImageQueueFilter.All;
        }

        private static string FormatQueueQuickFilterText(string label, int count)
        {
            return WpfImageQueuePresenter.FormatQuickFilterText(label, count);
        }

        private static string BuildQueueReviewCountSummary(IEnumerable<WpfImageQueueItem> items)
        {
            return WpfImageQueuePresenter.BuildReviewCountSummary(items);
        }

        private static string FormatLabelStatusForQueue(string labelText)
        {
            return WpfImageQueuePresenter.FormatLabelStatus(labelText);
        }

        private static string FormatDetectionStatusForQueue(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.FormatDetectionStatus(status);
        }

        private static PackIconMaterialKind GetQueueIconKind(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.GetIconKind(status);
        }

        private static MediaBrush GetQueueIconBrush(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.GetIconBrush(status);
        }

        private static string BuildQueueBadgeText(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.BuildBadgeText(status);
        }

        private static string BuildQueueStatusSummary(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.BuildStatusSummary(status);
        }

        private static string TranslateDetectionMessageForQueue(string message)
        {
            return WpfImageQueuePresenter.TranslateDetectionMessage(message);
        }

        private static string ShortenQueueMessage(string message, int maxLength)
        {
            return WpfImageQueuePresenter.ShortenMessage(message, maxLength);
        }

        private static string BuildReviewDetailText(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.BuildDetailText(status);
        }

        private void PopulateClassList(string selectedName = "")
        {
            PopulateClassCatalogFields();
            EnsureClassItem("Defect");
            List<CClassItem> classItems = global.Data.ClassNamedList
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ClassCatalogViewModel?.SetClasses(classItems, selectedName);

            RefreshObjectClassOptions(selectedName);
            RefreshYoloTrainingStepCompletion();
        }

        private void PopulateClassCatalogFields()
        {
            EnsureProjectSettings();
            global.Data.NormalizeOutputPaths();
            ClassCatalogViewModel?.LoadOutputRoot(global.Data.OutputRootPath);
        }

        private string GetSelectedClassName()
        {
            if (ClassCatalogViewModel?.SelectedClass != null)
            {
                return ClassCatalogViewModel.SelectedClass.Text;
            }

            return string.Empty;
        }

        private void SetClassEditStatus(string message)
        {
            if (ClassCatalogViewModel != null)
            {
                ClassCatalogViewModel.StatusText = message;
            }
        }

        private void SaveClassCatalog()
        {
            global.Data.SaveYoloDataYaml();
            global.Data.SaveConfig(global.Recipe.Name);
            PopulateClassCatalogFields();
            PopulateProjectConfigPanelFields();
        }

        private void SaveOutputRootFromEditor()
        {
            string outputRootPath = (ClassCatalogViewModel?.OutputRootPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                SetClassEditStatus("저장 경로를 입력하거나 선택하세요.");
                return;
            }

            try
            {
                global.Data.ConfigureOutputRoot(outputRootPath);
                SaveClassCatalog();
                RefreshTrainingReadinessPanel(refreshYaml: false);
                SetDatasetStatus($"데이터셋: 출력 경로 {global.Data.OutputRootPath}");
                SetClassEditStatus($"저장 경로 적용됨: {global.Data.OutputRootPath}");
                AppendLog($"YOLO 데이터셋 출력 경로 저장: {global.Data.OutputRootPath}");
            }
            catch (Exception ex)
            {
                SetClassEditStatus($"저장 경로 적용 실패: {ex.Message}");
                AppendLog($"YOLO 데이터셋 출력 경로 저장 실패: {ex.Message}");
            }
        }

        private void SaveProjectConfigButton_Click(object sender, RoutedEventArgs e)
        {
            SaveProjectConfigFromPanel();
        }

        private void ApplyProjectRecipeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyProjectRecipeFromPanel();
        }

        private void ProjectRecipeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressProjectRecipeSelection)
            {
                return;
            }

            string recipeName = ProjectConfigViewModel?.SelectedRecipeName ?? ProjectRecipeListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            ProjectConfigViewModel?.SelectRecipeFromList(recipeName);
        }

        private void RefreshProjectRecipeListButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedRecipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? GetCurrentRecipeName();
            if (PopulateProjectRecipeList(selectedRecipeName))
            {
                SetProjectConfigStatus("Recipe 목록을 다시 읽었습니다. 적용할 항목을 선택하세요.");
            }
        }

        private void OpenProjectConfigFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string directoryPath = string.IsNullOrWhiteSpace(GetCurrentRecipeName())
                ? GetRecipeRootDirectory()
                : GetCurrentRecipeConfigDirectory();

            try
            {
                Directory.CreateDirectory(directoryPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = directoryPath,
                    UseShellExecute = true
                });
                SetProjectConfigStatus($"폴더 열기: {directoryPath}");
                AppendLog($"Recipe 설정 폴더 열기: {directoryPath}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"폴더 열기 실패: {ex.Message}");
                AppendLog($"Recipe 설정 폴더 열기 실패: {ex.Message}");
            }
        }

        private bool SaveProjectConfigFromPanel()
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus("Recipe 이름이 없어 설정을 저장하지 않았습니다.");
                return false;
            }

            try
            {
                CRecipe.InitDirectory(recipeName);
                global.Data.SaveConfig(recipeName);
                PopulateProjectConfigPanelFields();
                string configPath = GetCurrentRecipeConfigPath();
                SetProjectConfigStatus($"설정 저장 완료: {DateTime.Now:HH:mm:ss}");
                SetDatasetStatus($"데이터셋: 설정 저장 {Path.GetFileName(configPath)}");
                AppendLog($"프로젝트 설정 저장: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"설정 저장 실패: {ex.Message}");
                AppendLog($"프로젝트 설정 저장 실패: {ex.Message}");
                return false;
            }
        }

        private void ApplyProjectRecipeFromPanel()
        {
            string recipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus("적용할 recipe 이름을 입력하세요.");
                return;
            }

            if (!WpfProjectRecipeService.IsValidRecipeName(recipeName))
            {
                SetProjectConfigStatus("Recipe 이름에 사용할 수 없는 문자가 있습니다.");
                return;
            }

            try
            {
                string previousRecipeName = GetCurrentRecipeName();
                global.Recipe.Name = recipeName;
                EnsureProjectSettings();
                PopulateProjectConfigPanelFields();
                PopulateYoloEditorFields();
                PopulateTrainingEditorFields();
                PopulateClassList();
                RefreshCandidateList();
                RefreshObjectList();
                RefreshTrainingReadinessPanel(refreshYaml: false);
                SetDatasetStatus($"데이터셋: recipe {recipeName}");
                SetProjectConfigStatus(string.Equals(previousRecipeName, recipeName, StringComparison.OrdinalIgnoreCase)
                    ? $"Recipe 재적용: {recipeName}"
                    : $"Recipe 적용: {recipeName}");
                AppendLog($"Recipe 적용: {recipeName}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"Recipe 적용 실패: {ex.Message}");
                AppendLog($"Recipe 적용 실패: {ex.Message}");
            }
        }

        private void SetProjectConfigStatus(string message)
        {
            if (ProjectConfigViewModel != null)
            {
                ProjectConfigViewModel.StatusText = message ?? string.Empty;
            }
            else if (ProjectConfigStatusText != null)
            {
                ProjectConfigStatusText.Text = message ?? string.Empty;
            }
        }

        private string GetCurrentRecipeName()
        {
            return global.Recipe?.Name?.Trim() ?? string.Empty;
        }

        private static string GetRecipeRootDirectory()
        {
            return WpfProjectRecipeService.GetRecipeRootDirectory();
        }

        private string GetCurrentRecipeConfigDirectory()
        {
            string recipeName = GetCurrentRecipeName();
            return WpfProjectRecipeService.BuildConfigDirectory(GetRecipeRootDirectory(), recipeName);
        }

        private string GetCurrentRecipeConfigPath()
        {
            string recipeName = GetCurrentRecipeName();
            return WpfProjectRecipeService.BuildConfigPath(GetRecipeRootDirectory(), recipeName);
        }

        private bool TryPickFile(string title, string filter, string currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = ResolveInitialDirectory(currentPath)
            };

            if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return false;
            }

            selectedPath = dialog.FileName;
            return true;
        }

        private bool TryPickFolder(string title, string currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            var dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = ResolveInitialDirectory(currentPath)
            };

            if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return false;
            }

            selectedPath = dialog.FolderName;
            return true;
        }

        private static string ResolveInitialDirectory(string currentPath)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return string.Empty;
            }

            if (Directory.Exists(currentPath))
            {
                return Path.GetFullPath(currentPath);
            }

            string directory = Path.GetDirectoryName(currentPath);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : string.Empty;
        }

        private bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
        {
            EnsureProjectSettings();
            if (!TryFindLatestTrainingWeights(out string latestWeightsPath))
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus("학습 결과 weight를 찾지 못했습니다. YOLO 탭에서 best.pt를 직접 선택하세요.", isBusy: false);
                    AppendLog("학습 결과 best.pt 후보를 찾지 못했습니다.");
                }

                return false;
            }

            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            string currentWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            if (string.Equals(currentWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus($"현재 weight 사용 중: {Path.GetFileName(latestWeightsPath)}", isBusy: false);
                    AppendLog($"현재 weight 유지: {latestWeightsPath}");
                }

                return false;
            }

            if (!ShouldPreferTrainingWeights(latestWeightsPath, currentWeightsPath))
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus($"기존 weight가 더 최신입니다: {Path.GetFileName(currentWeightsPath)}", isBusy: false);
                    AppendLog($"기존 weight 유지: {currentWeightsPath}");
                }

                return false;
            }

            settings.WeightsPath = latestWeightsPath;
            YoloModelSettingsViewModel?.LoadFrom(settings);
            SetModelStatus($"모델: {Path.GetFileName(latestWeightsPath)}");
            hasPendingTrainingWeightsRecipeSave = true;
            UpdateAppliedTrainingWeightsHistory(latestWeightsPath, savedToRecipe: false);
            FocusYoloSettingsTab();
            SaveYoloSettingsButton?.Focus();
            SetProjectConfigStatus("best.pt 적용됨. 설정 저장을 누르면 recipe에 반영됩니다.");
            SetYoloCommandStatus($"학습 결과 weight 적용: {Path.GetFileName(latestWeightsPath)} / 설정 저장 필요", isBusy: false);

            if (!string.Equals(lastAutoAppliedTrainingWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                lastAutoAppliedTrainingWeightsPath = latestWeightsPath;
                AppendLog($"학습 결과 weight 적용: {latestWeightsPath} / 설정 저장 필요");
            }

            return true;
        }

        private bool TryFindLatestTrainingWeights(out string latestWeightsPath)
        {
            latestWeightsPath = string.Empty;
            EnsureProjectSettings();
            var candidates = new List<string>();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            AddBestWeightCandidates(candidates, settings.ProjectRootPath);
            AddBestWeightCandidates(candidates, global.Data.OutputRootPath);

            latestWeightsPath = candidates
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(latestWeightsPath);
        }

        private static void AddBestWeightCandidates(List<string> candidates, string rootPath)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return;
            }

            candidates.Add(Path.Combine(rootPath, "best.pt"));
            string trainRunsRoot = Path.Combine(rootPath, "runs", "train");
            if (!Directory.Exists(trainRunsRoot))
            {
                return;
            }

            candidates.Add(Path.Combine(trainRunsRoot, "weights", "best.pt"));
            foreach (string runDirectory in Directory.EnumerateDirectories(trainRunsRoot))
            {
                candidates.Add(Path.Combine(runDirectory, "weights", "best.pt"));
            }
        }

        private static bool ShouldPreferTrainingWeights(string latestWeightsPath, string currentWeightsPath)
        {
            if (string.IsNullOrWhiteSpace(latestWeightsPath) || !File.Exists(latestWeightsPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentWeightsPath) || !File.Exists(currentWeightsPath))
            {
                return true;
            }

            return File.GetLastWriteTimeUtc(latestWeightsPath) >= File.GetLastWriteTimeUtc(currentWeightsPath);
        }

        private static bool IsCompletedTrainingState(string state)
        {
            return string.Equals(state?.Trim(), "completed", StringComparison.OrdinalIgnoreCase);
        }

        private void NotifyYoloPathSelected(string label, string selectedPath)
        {
            SetYoloCommandStatus($"{label} 선택됨. 저장 후 첫 점검을 누르세요.", isBusy: false);
            AppendLog($"{label} 선택됨: {selectedPath}");
        }

        private void RefreshYoloStatus()
        {
            EnsureProjectSettings();
            PythonModelValidationResult result = PythonModelSettingsValidator.Validate(global.Data.ProjectSettings.PythonModel, requireWeights: true);
            SetPythonStatus(result.IsValid ? "Python: 준비 완료" : "Python: 점검 필요");
            SetModelStatus(File.Exists(global.Data.ProjectSettings.PythonModel.WeightsPath)
                ? $"모델: {Path.GetFileName(global.Data.ProjectSettings.PythonModel.WeightsPath)}"
                : "모델: 가중치 없음");
        }

        private void SaveYoloEditorFields()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            YoloModelSettingsViewModel?.ApplyTo(settings);
            YoloModelSettingsViewModel?.LoadFrom(settings);
            CandidateConfidenceSlider.Value = Math.Clamp(settings.MinimumDetectionConfidence, 0F, 1F);
        }

        private void SaveTrainingEditorFields()
        {
            EnsureProjectSettings();
            TrainingSettings training = global.Data.ProjectSettings.Training;
            TrainingSettingsViewModel?.ApplyTo(training, global.Data.ProjectSettings.YoloDataset, global.Data.TrainingParam);
            PopulateTrainingEditorFields();
        }

        private bool BeginTrainingCommand(string statusText)
        {
            if (isTrainingCommandRunning || isYoloEnvironmentCommandRunning || isDetecting || isBatchDetectionRunning)
            {
                AppendLog("YOLO 또는 학습 명령이 이미 실행 중입니다.");
                return false;
            }

            isTrainingCommandRunning = true;
            SetTrainingReadinessStatus(statusText);
            SetTrainingProgressStatus(
                string.IsNullOrWhiteSpace(statusText) ? "\uD559\uC2B5 \uBA85\uB839 \uC2E4\uD589 \uC911" : statusText,
                string.Empty,
                0D,
                isIndeterminate: true);
            SetTrainingStatusBrushes(
                TrainingSettingsViewModel?.TrainingReadinessForeground ?? TrainingReadinessText?.Foreground,
                MediaBrushes.DodgerBlue);
            UpdateYoloCommandButtons();
            return true;
        }

        private void EndTrainingCommand()
        {
            isTrainingCommandRunning = false;
            SyncTrainingReadinessFromTextBlockIfBindingWasBroken();
            SetTrainingProgressBusy(false);
            UpdateTrainingProgressFromWorker();
            UpdateYoloCommandButtons();
            RefreshYoloStatus();
        }

        private void SetTrainingReadinessStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingReadinessText(normalized);
                return;
            }

            if (TrainingReadinessText != null)
            {
                TrainingReadinessText.Text = normalized;
            }
        }

        private string GetTrainingReadinessStatus()
        {
            return TrainingReadinessText?.Text
                ?? TrainingSettingsViewModel?.TrainingReadinessText
                ?? string.Empty;
        }

        private void SyncTrainingReadinessFromTextBlockIfBindingWasBroken()
        {
            if (TrainingSettingsViewModel == null
                || TrainingReadinessText == null
                || BindingOperations.GetBindingExpressionBase(TrainingReadinessText, TextBlock.TextProperty) != null)
            {
                return;
            }

            SetTrainingReadinessStatus(TrainingReadinessText.Text);
        }

        private void SetTrainingProgressStatus(string progressText, string epochText, double progressValue, bool isIndeterminate)
        {
            string normalizedProgress = progressText ?? string.Empty;
            string normalizedEpoch = epochText ?? string.Empty;
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingProgress(normalizedProgress, normalizedEpoch, progressValue, isIndeterminate);
                return;
            }

            if (TrainingProgressText != null)
            {
                TrainingProgressText.Text = normalizedProgress;
            }

            if (TrainingEpochText != null)
            {
                TrainingEpochText.Text = normalizedEpoch;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.Value = Math.Clamp(progressValue, 0D, 100D);
                TrainingProgressBar.IsIndeterminate = isIndeterminate;
            }
        }

        private void SetTrainingProgressValue(double value)
        {
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingProgressValue(value);
                return;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.Value = Math.Clamp(value, 0D, 100D);
            }
        }

        private void SetTrainingProgressBusy(bool isBusy)
        {
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingProgressBusy(isBusy);
                return;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.IsIndeterminate = isBusy;
            }
        }

        private void SetTrainingStatusBrushes(MediaBrush readinessBrush, MediaBrush progressBrush)
        {
            if (TrainingSettingsViewModel != null)
            {
                EnsureTrainingStatusBindings();
                TrainingSettingsViewModel.SetTrainingStatusBrushes(readinessBrush, progressBrush);
                return;
            }

            if (TrainingReadinessText != null)
            {
                TrainingReadinessText.Foreground = readinessBrush;
            }

            if (TrainingProgressText != null)
            {
                TrainingProgressText.Foreground = progressBrush;
            }

            if (TrainingProgressBar != null)
            {
                TrainingProgressBar.Foreground = progressBrush;
            }
        }

        private void EnsureTrainingStatusBindings()
        {
            if (TrainingSettingsViewModel == null)
            {
                return;
            }

            EnsureBinding(TrainingReadinessText, TextBlock.TextProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingReadinessText));
            EnsureBinding(TrainingReadinessText, TextBlock.ForegroundProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingReadinessForeground));
            EnsureBinding(TrainingProgressText, TextBlock.TextProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressText));
            EnsureBinding(TrainingProgressText, TextBlock.ForegroundProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressForeground));
            EnsureBinding(TrainingEpochText, TextBlock.TextProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingEpochStatusText));
            EnsureBinding(TrainingProgressBar, ProgressBar.ValueProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressValue));
            EnsureBinding(TrainingProgressBar, ProgressBar.IsIndeterminateProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressIsIndeterminate));
            EnsureBinding(TrainingProgressBar, ProgressBar.ForegroundProperty, nameof(WpfTrainingSettingsPanelViewModel.TrainingProgressForeground));
        }

        private static void EnsureBinding(DependencyObject target, DependencyProperty property, string path)
        {
            if (target == null || BindingOperations.GetBindingExpressionBase(target, property) != null)
            {
                return;
            }

            BindingOperations.SetBinding(target, property, new Binding(path) { Mode = BindingMode.OneWay });
        }

        private void RefreshTrainingReadinessPanel(bool refreshYaml)
        {
            EnsureProjectSettings();
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(global.Data, refreshYaml);
            string readinessText;
            if (report.IsReady)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                readinessText =
                    $"학습 준비 완료. 학습 {statistics.TrainImageCount} / 검증 {statistics.ValidImageCount} / 테스트 {statistics.TestImageCount} / 객체 {statistics.TotalObjectCount} / 클래스 {global.Data.ClassNamedList.Count}";
            }
            else
            {
                readinessText = $"학습 데이터 확인 필요: {report.Errors.FirstOrDefault() ?? "원인 미확인"}";
            }

            SetTrainingReadinessStatus(readinessText);
            UpdateYoloTrainingChecklist(report, recordHistory: refreshYaml);
            UpdateTrainingProgressFromWorker();
        }

        private void UpdateYoloTrainingChecklist(YoloDatasetReadinessReport report, bool recordHistory)
        {
            if (LearningWorkflowViewModel == null || report == null)
            {
                return;
            }

            lastYoloTrainingReadinessReport = report;
            YoloTrainingIssuePresentation presentation = BuildYoloTrainingIssuePresentation(report);
            LearningWorkflowViewModel.TrainingChecklistStatusText = presentation.StatusText;
            LearningWorkflowViewModel.TrainingChecklistDetailText = presentation.DetailText;
            LearningWorkflowViewModel.TrainingChecklistActionText = presentation.ActionText;
            UpdateYoloTrainingGuideDatasetHistory(report, presentation, recordHistory);
            UpdateYoloTrainingHistoryText();
            RefreshYoloTrainingStepCompletion(report);
        }

        private YoloTrainingIssuePresentation BuildYoloTrainingIssuePresentation(YoloDatasetReadinessReport report)
        {
            if (report?.IsReady == true)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                int classCount = global.Data?.ClassNamedList?.Count ?? 0;
                return new YoloTrainingIssuePresentation(
                    "Ready",
                    $"데이터셋: 학습 가능 / 이미지 {statistics.TrainImageCount + statistics.ValidImageCount + statistics.TestImageCount} / 객체 {statistics.TotalObjectCount}",
                    $"train {statistics.TrainImageCount}, valid {statistics.ValidImageCount}, test {statistics.TestImageCount}, labels {statistics.TrainLabelCount + statistics.ValidLabelCount + statistics.TestLabelCount}, classes {classCount}",
                    "이제 5단계 YOLOv5 학습에서 설정을 확인하고 시작하세요.");
            }

            string firstError = report?.Errors?.FirstOrDefault() ?? "원인 미확인";
            string issueKind = ClassifyYoloTrainingIssue(report?.Errors ?? Array.Empty<string>());
            return issueKind switch
            {
                "Classes" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 클래스 등록 필요",
                    firstError,
                    "클래스 버튼을 눌러 모델이 배울 이름을 먼저 등록하세요."),
                "Labels" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 라벨 저장 필요",
                    firstError,
                    "라벨 버튼을 눌러 박스를 그리고 저장한 뒤 다시 점검하세요."),
                "ValidImages" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 검증 이미지 필요",
                    firstError,
                    "점검 버튼을 눌러 train/valid 분리와 validation 비율을 다시 확인하세요."),
                "Split" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: train/valid 분리 필요",
                    firstError,
                    "검증 이미지는 학습 이미지와 달라야 합니다. 이미지 폴더를 다시 나누거나 validation 비율과 샘플 구성을 점검하세요."),
                "DataYaml" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: data.yaml 확인 필요",
                    firstError,
                    "점검 버튼을 눌러 data.yaml을 다시 생성하고 저장 경로를 확인하세요."),
                "LabelFormat" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 라벨 형식 오류",
                    firstError,
                    "문제가 있는 txt 라벨을 다시 저장하거나 해당 이미지를 다시 라벨링하세요."),
                "OutputRoot" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 저장 경로 확인 필요",
                    firstError,
                    "클래스 탭에서 YOLO 출력 경로를 확인하고 저장하세요."),
                "Images" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 이미지 폴더 확인 필요",
                    firstError,
                    "1단계에서 학습 이미지 폴더를 다시 열고, 지원되는 이미지가 있는지 확인하세요."),
                _ => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 확인 필요",
                    firstError,
                    "원인을 확인한 뒤 클래스, 라벨, 점검 버튼 중 맞는 항목으로 이동하세요.")
            };
        }

        private static string ClassifyYoloTrainingIssue(IEnumerable<string> errors)
        {
            List<string> normalized = (errors ?? Array.Empty<string>())
                .Select(error => error?.Trim() ?? string.Empty)
                .Where(error => error.Length > 0)
                .Select(error => error.ToLowerInvariant())
                .ToList();

            if (normalized.Any(error => error.Contains("invalid yolo format", StringComparison.Ordinal)
                || error.Contains("invalid class index", StringComparison.Ordinal)
                || error.Contains("out-of-range normalized value", StringComparison.Ordinal)
                || error.Contains("label width must", StringComparison.Ordinal)
                || error.Contains("label height must", StringComparison.Ordinal)))
            {
                return "LabelFormat";
            }

            if (normalized.Any(error => error.Contains("at least one class", StringComparison.Ordinal)
                || error.Contains("class names", StringComparison.Ordinal)
                || error.Contains("duplicate class", StringComparison.Ordinal)))
            {
                return "Classes";
            }

            if (normalized.Any(error => error.Contains("label file is missing", StringComparison.Ordinal)
                || error.Contains("label directory", StringComparison.Ordinal)))
            {
                return "Labels";
            }

            if (normalized.Any(error => error.Contains("valid image directory", StringComparison.Ordinal)))
            {
                return "ValidImages";
            }

            if (normalized.Any(error => error.Contains("train/valid image split", StringComparison.Ordinal)
                || error.Contains("duplicate image content", StringComparison.Ordinal)
                || error.Contains("different validation images", StringComparison.Ordinal)))
            {
                return "Split";
            }

            if (normalized.Any(error => error.Contains("data.yaml", StringComparison.Ordinal)))
            {
                return "DataYaml";
            }

            if (normalized.Any(error => error.Contains("output root", StringComparison.Ordinal)))
            {
                return "OutputRoot";
            }

            if (normalized.Any(error => error.Contains("image directory", StringComparison.Ordinal)
                || error.Contains("supported images", StringComparison.Ordinal)))
            {
                return "Images";
            }

            return "Unknown";
        }

        private sealed class YoloTrainingIssuePresentation
        {
            public YoloTrainingIssuePresentation(string issueKind, string statusText, string detailText, string actionText)
            {
                IssueKind = issueKind ?? string.Empty;
                StatusText = statusText ?? string.Empty;
                DetailText = detailText ?? string.Empty;
                ActionText = actionText ?? string.Empty;
            }

            public string IssueKind { get; }

            public string StatusText { get; }

            public string DetailText { get; }

            public string ActionText { get; }
        }

        private void UpdateYoloTrainingGuideDatasetHistory(
            YoloDatasetReadinessReport report,
            YoloTrainingIssuePresentation presentation,
            bool recordHistory)
        {
            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.LastDatasetCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            history.LastDatasetReady = report?.IsReady == true;
            history.LastDatasetIssueKind = presentation?.IssueKind ?? string.Empty;
            history.LastDatasetSummary = presentation?.DetailText ?? string.Empty;

            if (recordHistory)
            {
                AddYoloTrainingRunHistoryRecord("DatasetCheck");
            }

            if (!hasPendingTrainingWeightsRecipeSave)
            {
                TrySaveTrainingGuideHistoryQuietly();
            }
        }

        private void UpdateYoloTrainingGuideTrainingHistory(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return;
            }

            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.LastTrainingUpdateUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            history.LastTrainingState = status.LastTrainingState ?? string.Empty;
            history.LastTrainingProgressPercent = status.LastTrainingProgressPercent ?? -1;
            history.LastTrainingMessage = status.LastTrainingMessage ?? string.Empty;
            UpdateYoloTrainingHistoryText();

            if (IsTerminalTrainingState(history.LastTrainingState))
            {
                string signature = $"{history.LastTrainingState}|{history.LastTrainingProgressPercent}|{history.LastTrainingMessage}";
                if (!string.Equals(lastRecordedTrainingGuideRunSignature, signature, StringComparison.Ordinal))
                {
                    lastRecordedTrainingGuideRunSignature = signature;
                    AddYoloTrainingRunHistoryRecord("TrainingState");
                }
            }

            if (IsTerminalTrainingState(history.LastTrainingState) && !hasPendingTrainingWeightsRecipeSave)
            {
                TrySaveTrainingGuideHistoryQuietly();
            }
        }

        private void UpdateAppliedTrainingWeightsHistory(string weightsPath, bool savedToRecipe)
        {
            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.AppliedWeightsPath = weightsPath ?? string.Empty;
            history.AppliedWeightsUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            history.AppliedWeightsSavedToRecipe = savedToRecipe;
            UpsertAppliedWeightsRunHistory(weightsPath, savedToRecipe);
            UpdateYoloTrainingHistoryText();
        }

        private void AddYoloTrainingRunHistoryRecord(string eventKind)
        {
            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.EnsureDefaults();
            history.RunHistory.Add(CreateYoloTrainingRunRecord(eventKind));
            TrimYoloTrainingRunHistory(history);
            RefreshYoloTrainingRunHistoryItems(history);
        }

        private void UpsertAppliedWeightsRunHistory(string weightsPath, bool savedToRecipe)
        {
            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.EnsureDefaults();
            string normalizedPath = weightsPath ?? string.Empty;
            YoloTrainingGuideRunRecord existing = history.RunHistory
                .LastOrDefault(item =>
                    string.Equals(item.EventKind, "Weight", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.AppliedWeightsPath ?? string.Empty, normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                history.RunHistory.Add(CreateYoloTrainingRunRecord("Weight"));
            }
            else
            {
                existing.EventUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                existing.AppliedWeightsSavedToRecipe = savedToRecipe;
                existing.TrainingState = history.LastTrainingState ?? string.Empty;
                existing.TrainingProgressPercent = history.LastTrainingProgressPercent;
                existing.TrainingMessage = history.LastTrainingMessage ?? string.Empty;
            }

            TrimYoloTrainingRunHistory(history);
            RefreshYoloTrainingRunHistoryItems(history);
        }

        private YoloTrainingGuideRunRecord CreateYoloTrainingRunRecord(string eventKind)
        {
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            return new YoloTrainingGuideRunRecord
            {
                EventUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                EventKind = eventKind ?? string.Empty,
                DatasetReady = history.LastDatasetReady,
                DatasetIssueKind = history.LastDatasetIssueKind ?? string.Empty,
                DatasetSummary = history.LastDatasetSummary ?? string.Empty,
                TrainingState = history.LastTrainingState ?? string.Empty,
                TrainingProgressPercent = history.LastTrainingProgressPercent,
                TrainingMessage = history.LastTrainingMessage ?? string.Empty,
                AppliedWeightsPath = history.AppliedWeightsPath ?? string.Empty,
                AppliedWeightsSavedToRecipe = history.AppliedWeightsSavedToRecipe
            };
        }

        private static void TrimYoloTrainingRunHistory(YoloTrainingGuideHistory history)
        {
            if (history?.RunHistory == null)
            {
                return;
            }

            while (history.RunHistory.Count > TrainingGuideRunHistoryLimit)
            {
                history.RunHistory.RemoveAt(0);
            }
        }

        private void RefreshYoloTrainingRunHistoryItems(YoloTrainingGuideHistory history)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            IReadOnlyList<string> items = (history?.RunHistory ?? new List<YoloTrainingGuideRunRecord>())
                .Where(item => item != null)
                .OrderByDescending(item => ParseHistoryUtc(item.EventUtc))
                .Take(5)
                .Select(FormatYoloTrainingRunHistoryItem)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            LearningWorkflowViewModel.SetTrainingRunHistoryItems(items);
        }

        private string FormatYoloTrainingRunHistoryItem(YoloTrainingGuideRunRecord record)
        {
            string time = FormatHistoryTime(record?.EventUtc);
            return (record?.EventKind ?? string.Empty) switch
            {
                "DatasetCheck" => $"{time} 점검: {(record.DatasetReady ? "학습 가능" : $"확인 필요 {record.DatasetIssueKind}")}",
                "TrainingState" => $"{time} 학습: {FormatTrainingState(record.TrainingState)} {FormatHistoryProgress(record.TrainingProgressPercent)}".TrimEnd(),
                "Weight" => $"{time} weight: {Path.GetFileName(record.AppliedWeightsPath)} / {(record.AppliedWeightsSavedToRecipe ? "recipe 저장됨" : "recipe 미저장")}",
                _ => $"{time} 기록: {record?.EventKind}"
            };
        }

        private static string FormatHistoryProgress(int progressPercent)
        {
            return progressPercent >= 0
                ? $"{Math.Clamp(progressPercent, 0, 100)}%"
                : string.Empty;
        }

        private static DateTime ParseHistoryUtc(string utcText)
        {
            return DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc)
                ? utc
                : DateTime.MinValue;
        }

        private void UpdateYoloTrainingHistoryText()
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.EnsureDefaults();
            RefreshYoloTrainingRunHistoryItems(history);
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(history.LastDatasetCheckUtc))
            {
                string readyText = history.LastDatasetReady ? "학습 가능" : $"확인 필요({history.LastDatasetIssueKind})";
                parts.Add($"점검 {FormatHistoryTime(history.LastDatasetCheckUtc)} {readyText}");
            }

            if (!string.IsNullOrWhiteSpace(history.LastTrainingState))
            {
                string progress = history.LastTrainingProgressPercent >= 0
                    ? $" {Math.Clamp(history.LastTrainingProgressPercent, 0, 100)}%"
                    : string.Empty;
                parts.Add($"학습 {FormatTrainingState(history.LastTrainingState)}{progress}");
            }

            if (!string.IsNullOrWhiteSpace(history.AppliedWeightsPath))
            {
                string savedText = history.AppliedWeightsSavedToRecipe ? "recipe 저장됨" : "recipe 미저장";
                parts.Add($"weight {Path.GetFileName(history.AppliedWeightsPath)} / {savedText}");
            }

            LearningWorkflowViewModel.TrainingHistoryText = parts.Count == 0
                ? "최근 학습 이력: 아직 없습니다."
                : $"최근 이력: {string.Join(" · ", parts)}";
        }

        private static string FormatHistoryTime(string utcText)
        {
            if (DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            return "-";
        }

        private bool TrySaveTrainingGuideHistoryQuietly()
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            try
            {
                CRecipe.InitDirectory(recipeName);
                global.Data.SaveConfig(recipeName);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"학습 가이드 이력 저장 실패: {ex.Message}");
                return false;
            }
        }

        private void RefreshYoloTrainingStepCompletion(YoloDatasetReadinessReport report = null)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            report ??= lastYoloTrainingReadinessReport;
            int classCount = global.Data?.ClassNamedList?.Count ?? 0;
            int savedObjectCount = report?.Statistics?.TotalObjectCount ?? 0;
            bool hasImages = imageQueueItems.Count > 0 || !string.IsNullOrWhiteSpace(activeImagePath);
            bool hasClasses = classCount > 0;
            bool hasLabels = manualRois.Count > 0
                || confirmedDetectionCandidates.Count > 0
                || savedObjectCount > 0
                || imageQueueItems.Any(item => item.IsLabeled);
            bool datasetReady = report?.IsReady == true;

            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            string trainingState = status?.LastTrainingState?.Trim() ?? string.Empty;
            bool hasTrainingStatus = HasTrainingStatus(status);
            bool trainingCompleted = IsCompletedTrainingState(trainingState);
            bool trainingRunning = hasTrainingStatus && !trainingCompleted && !IsTerminalTrainingState(trainingState);
            bool hasInferenceResult = pendingDetectionCandidates.Count > 0
                || imageQueueItems.Any(item => item.ReviewState == YoloImageReviewState.Candidate);

            LearningWorkflowViewModel.SetYoloTrainingStepState(1, hasImages, hasImages ? "완료" : "이미지 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(2, hasClasses, hasClasses ? "완료" : "클래스 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(3, hasLabels, hasLabels ? "완료" : "라벨 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(4, datasetReady, datasetReady ? "완료" : "점검 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(5, trainingCompleted, trainingCompleted ? "완료" : trainingRunning ? "진행 중" : "대기");
            LearningWorkflowViewModel.SetYoloTrainingStepState(6, hasInferenceResult, hasInferenceResult ? "후보 있음" : "추론 필요");
            LearningWorkflowViewModel.SetYoloFixActionAvailability(
                canFixClasses: true,
                canFixLabels: hasImages,
                canFixDataset: true);
        }

        private void UpdateTrainingProgressFromWorker()
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            bool hasStatus = HasTrainingStatus(status);
            if (status.LastTrainingProgressPercent.HasValue)
            {
                SetTrainingProgressValue(Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100));
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressValue(0);
            }

            if (hasStatus)
            {
                SetTrainingProgressStatus(
                    BuildTrainingProgressSummary(status),
                    BuildTrainingEpochSummary(status),
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: false);
                UpdateYoloTrainingGuideTrainingHistory(status);
                if (IsCompletedTrainingState(status.LastTrainingState))
                {
                    TryApplyLatestTrainingWeightsFromProject(logIfUnchanged: false);
                }
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressStatus("\uD559\uC2B5 \uB300\uAE30", string.Empty, 0D, isIndeterminate: false);
            }

            UpdateTrainingStatusVisual(status, lastYoloTrainingReadinessReport);
            RefreshYoloTrainingStepCompletion();
            if (hasStatus && IsTerminalTrainingState(status.LastTrainingState))
            {
                StopTrainingStatusPolling();
            }
        }

        private void StartTrainingStatusPolling()
        {
            trainingStatusPollStartedUtc = DateTime.UtcNow;
            if (!trainingStatusPollTimer.IsEnabled)
            {
                trainingStatusPollTimer.Start();
            }
        }

        private void StopTrainingStatusPolling()
        {
            if (trainingStatusPollTimer.IsEnabled)
            {
                trainingStatusPollTimer.Stop();
            }
        }

        private void TrainingStatusPollTimer_Tick(object sender, EventArgs e)
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            UpdateTrainingProgressFromWorker();
            if (HasTrainingStatus(status) && IsTerminalTrainingState(status.LastTrainingState))
            {
                StopTrainingStatusPolling();
                return;
            }

            if (!HasTrainingStatus(status)
                && trainingStatusPollStartedUtc != DateTime.MinValue
                && DateTime.UtcNow - trainingStatusPollStartedUtc > TimeSpan.FromSeconds(TrainingStatusPollTimeoutSeconds))
            {
                StopTrainingStatusPolling();
            }
        }

        private void UpdateTrainingStatusVisual(PythonCommunicationStatus status, YoloDatasetReadinessReport report = null)
        {
            MediaBrush readinessBrush = report == null
                ? ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray)
                : report.IsReady
                    ? MediaBrushes.LimeGreen
                    : MediaBrushes.DarkOrange;
            MediaBrush stateBrush = ResolveTrainingStateBrush(status);
            SetTrainingStatusBrushes(readinessBrush, stateBrush);
        }

        private MediaBrush ResolveTrainingStateBrush(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray);
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (IsCompletedTrainingState(state))
            {
                return MediaBrushes.LimeGreen;
            }

            if (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase))
            {
                return MediaBrushes.IndianRed;
            }

            if (string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                return MediaBrushes.DarkOrange;
            }

            if (!IsTerminalTrainingState(state) || status.LastTrainingProgressPercent.HasValue)
            {
                return MediaBrushes.DodgerBlue;
            }

            return ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray);
        }

        private MediaBrush ResolveBrushResource(string key, MediaBrush fallback)
        {
            return TryFindResource(key) as MediaBrush ?? fallback;
        }

        private static bool HasTrainingStatus(PythonCommunicationStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                    || !string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    || status.LastTrainingProgressPercent.HasValue
                    || status.LastTrainingEpoch.HasValue
                    || status.LastTrainingTotalEpochs.HasValue);
        }

        private static string BuildTrainingProgressSummary(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return "학습 대기";
            }

            List<string> parts = new List<string>
            {
                $"학습 {FormatTrainingState(status.LastTrainingState)}"
            };
            if (status.LastTrainingProgressPercent.HasValue)
            {
                parts.Add($"{Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100)}%");
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingMessage))
            {
                parts.Add(FormatTrainingMessage(status.LastTrainingMessage));
            }

            return string.Join(" / ", parts);
        }

        private static string FormatTrainingState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "" => "상태 미확인",
                "idle" => "대기",
                "running" => "진행 중",
                "completed" => "완료",
                "stopped" => "중지됨",
                "failed" => "실패",
                "error" => "오류",
                _ => normalized
            };
        }

        private static string FormatTrainingMessage(string message)
        {
            string normalized = message?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "epoch" => "에폭",
                "epoch update" => "에폭 갱신",
                _ => normalized
            };
        }

        private static string BuildTrainingEpochSummary(PythonCommunicationStatus status)
        {
            if (status?.LastTrainingEpoch.HasValue == true && status.LastTrainingTotalEpochs.HasValue)
            {
                return $"에폭 {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}";
            }

            if (status?.LastTrainingEpoch.HasValue == true)
            {
                return $"에폭 {status.LastTrainingEpoch.Value}";
            }

            return string.Empty;
        }

        private bool BeginYoloEnvironmentCommand(string statusText)
        {
            if (isYoloEnvironmentCommandRunning || isTrainingCommandRunning || isDetecting || isBatchDetectionRunning)
            {
                AppendLog("YOLO 명령이 이미 실행 중입니다.");
                return false;
            }

            isYoloEnvironmentCommandRunning = true;
            SetYoloCommandStatus(statusText, isBusy: true);
            UpdateYoloCommandButtons();
            return true;
        }

        private void EndYoloEnvironmentCommand()
        {
            isYoloEnvironmentCommandRunning = false;
            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetCommandBusy(false);
            }
            else
            {
                YoloCommandProgressBar.IsIndeterminate = false;
                YoloCommandProgressBar.Visibility = Visibility.Collapsed;
            }

            UpdateYoloCommandButtons();
            RefreshYoloStatus();
        }

        private void SetYoloCommandStatus(string text, bool isBusy)
        {
            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetCommandStatus(text, isBusy);
                return;
            }

            YoloCommandStatusText.Text = string.IsNullOrWhiteSpace(text) ? "YOLO 명령 대기" : text;
            YoloCommandProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            YoloCommandProgressBar.IsIndeterminate = isBusy;
            if (!isBusy)
            {
                YoloCommandProgressBar.Value = 0;
            }
        }

        private void SetGlobalInferenceStatus(string text, bool isBusy, bool isWarning = false)
        {
            if (InferenceStatusText == null || InferenceStatusBorder == null)
            {
                return;
            }

            InferenceStatusText.Text = string.IsNullOrWhiteSpace(text) ? "대기" : text;
            InferenceStatusProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            InferenceStatusProgressBar.IsIndeterminate = false;
            if (isBusy)
            {
                StartInferenceStatusPulse();
            }
            else
            {
                StopInferenceStatusPulse();
            }
            InferenceStatusIcon.Kind = isBusy
                ? PackIconMaterialKind.ProgressClock
                : isWarning
                    ? PackIconMaterialKind.AlertCircleOutline
                    : PackIconMaterialKind.RobotIndustrial;

            InferenceStatusBorder.SetResourceReference(
                Border.BackgroundProperty,
                isBusy ? "DetectionOverlaySelectedBackgroundBrush" : "ToolbarButtonBrush");
            InferenceStatusBorder.SetResourceReference(
                Border.BorderBrushProperty,
                isBusy || isWarning ? "AccentBrush" : "BorderBrushDark");
        }

        private void StartInferenceStatusPulse()
        {
            if (InferenceStatusProgressBar == null)
            {
                return;
            }

            if (!inferenceStatusPulseTimer.IsEnabled)
            {
                inferenceStatusPulseStopwatch.Restart();
                InferenceStatusProgressBar.Value = 8;
                inferenceStatusPulseTimer.Start();
            }
        }

        private void StopInferenceStatusPulse()
        {
            inferenceStatusPulseTimer.Stop();
            inferenceStatusPulseStopwatch.Reset();
            if (InferenceStatusProgressBar != null)
            {
                InferenceStatusProgressBar.Value = 0;
            }
        }

        private void InferenceStatusPulseTimer_Tick(object sender, EventArgs e)
        {
            if (InferenceStatusProgressBar == null || InferenceStatusProgressBar.Visibility != Visibility.Visible)
            {
                StopInferenceStatusPulse();
                return;
            }

            const double cycleMilliseconds = 1400D;
            double elapsed = inferenceStatusPulseStopwatch.Elapsed.TotalMilliseconds;
            double phase = (elapsed % cycleMilliseconds) / cycleMilliseconds;
            InferenceStatusProgressBar.Value = 8D + (phase * 84D);
        }

        private void UpdateYoloCommandButtons()
        {
            WpfWorkflowCommandState state = WpfWorkflowCommandStateService.Build(
                isInferenceMode: currentWorkflowMode == WorkflowMode.Inference,
                isYoloEnvironmentCommandRunning: isYoloEnvironmentCommandRunning,
                isDetecting: isDetecting,
                isBatchDetectionRunning: isBatchDetectionRunning,
                isTrainingCommandRunning: isTrainingCommandRunning,
                isTrainingStopAvailable: IsTrainingStopAvailable(global.GetPythonCommunicationStatusSnapshot()),
                hasCurrentRecipeName: !string.IsNullOrWhiteSpace(GetCurrentRecipeName()));

            ApplyYoloStatusCommandState(state);
            ApplyProjectConfigCommandState(state);
            ApplyYoloModelSettingsCommandState(state);
            ApplyTrainingSettingsCommandState(state);
            ApplyShellCommandState(state);
            ApplyImageQueueCommandState(state);
            UpdateDetectionCommandHints(state);
            UpdateWorkflowModeUi();
        }

        private void ApplyShellCommandState(WpfWorkflowCommandState state)
        {
            if (ShellViewModel != null)
            {
                ShellViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            SetControlEnabled(DetectButton, state?.CanRunInference == true);
        }

        private void ApplyImageQueueCommandState(WpfWorkflowCommandState state)
        {
            if (ImageQueuePanelControl?.ViewModel != null)
            {
                ImageQueuePanelControl.ViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunInference = state?.CanRunInference == true;
            SetControlEnabled(DetectSelectedQueueButton, canRunInference);
            SetControlEnabled(BatchDetectQueueButton, canRunInference);
            SetControlEnabled(RetryFailedQueueButton, canRunInference);
            SetControlEnabled(StopBatchQueueButton, state?.CanStopBatchDetection == true);
        }

        private void ApplyTrainingSettingsCommandState(WpfWorkflowCommandState state)
        {
            if (TrainingSettingsViewModel != null)
            {
                TrainingSettingsViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(RefreshTrainingReadinessButton, canRunGeneralCommands);
            SetControlEnabled(StartTrainingButton, canRunGeneralCommands);
            SetControlEnabled(StopTrainingButton, state?.CanStopTraining == true);
        }

        private void ApplyYoloModelSettingsCommandState(WpfWorkflowCommandState state)
        {
            if (YoloModelSettingsViewModel != null)
            {
                YoloModelSettingsViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(BrowseYoloPythonButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloProjectRootButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloClientScriptButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloWeightsButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloImageRootButton, canRunGeneralCommands);
            SetControlEnabled(SaveYoloSettingsButton, canRunGeneralCommands);
            SetControlEnabled(ResetYoloSettingsButton, canRunGeneralCommands);
        }

        private void ApplyProjectConfigCommandState(WpfWorkflowCommandState state)
        {
            if (ProjectConfigViewModel != null)
            {
                ProjectConfigViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(ApplyProjectRecipeButton, canRunGeneralCommands);
            SetControlEnabled(RefreshProjectRecipeListButton, canRunGeneralCommands);
            SetControlEnabled(SaveProjectConfigButton, state?.CanSaveProjectConfig == true);
            SetControlEnabled(OpenProjectConfigFolderButton, canRunGeneralCommands);
        }

        private void ApplyYoloStatusCommandState(WpfWorkflowCommandState state)
        {
            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(FirstCheckYoloButton, canRunGeneralCommands);
            SetControlEnabled(InstallRequirementsButton, canRunGeneralCommands);
            SetControlEnabled(RunYoloSmokeButton, canRunGeneralCommands);
            SetControlEnabled(RestartPythonWorkerButton, canRunGeneralCommands);
            SetControlEnabled(StopPythonWorkerButton, canRunGeneralCommands);
        }

        private static void SetControlEnabled(Control control, bool isEnabled)
        {
            if (control != null)
            {
                control.IsEnabled = isEnabled;
            }
        }

        private void UpdateDetectionCommandHints(WpfWorkflowCommandState state)
        {
            SetControlToolTip(DetectButton, state?.CurrentImageDetectionToolTip);
            SetControlToolTip(DetectSelectedQueueButton, state?.SelectedQueueDetectionToolTip);
            SetControlToolTip(BatchDetectQueueButton, state?.BatchDetectionToolTip);
            SetControlToolTip(RetryFailedQueueButton, state?.RetryFailedToolTip);
            SetControlToolTip(StopBatchQueueButton, state?.StopBatchToolTip);
        }

        private static void SetControlToolTip(FrameworkElement element, string text)
        {
            if (element != null)
            {
                element.ToolTip = text;
            }
        }

        private void SetWorkflowMode(WorkflowMode mode)
        {
            currentWorkflowMode = mode;
            UpdateYoloCommandButtons();
            UpdateCandidateActionState();
            SetModelStatus(mode == WorkflowMode.Inference
                ? "모드: 추론 검토"
                : "모드: 라벨링");
        }

        private void UpdateWorkflowModeUi()
        {
            bool canSwitchMode = !isDetecting && !isBatchDetectionRunning;
            ShellViewModel?.SetWorkflowModeState(
                currentWorkflowMode == WorkflowMode.Inference,
                canSwitchMode);
        }

        private bool EnsureInferenceModeForDetection()
        {
            if (currentWorkflowMode == WorkflowMode.Inference)
            {
                return true;
            }

            SetPythonStatus("Python: 추론 검토 모드 필요");
            SetGlobalInferenceStatus("추론 검토 모드 필요", isBusy: false, isWarning: true);
            AppendLog("검출 건너뜀. 먼저 추론 검토 모드로 전환하세요.");
            UpdateYoloCommandButtons();
            return false;
        }

        private static bool IsTrainingStopAvailable(PythonCommunicationStatus status)
        {
            if (status == null)
            {
                return false;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state))
            {
                return status.LastTrainingProgressPercent.HasValue
                    && status.LastTrainingProgressPercent.Value > 0
                    && status.LastTrainingProgressPercent.Value < 100;
            }

            return !IsTerminalTrainingState(state);
        }

        private static bool IsTerminalTrainingState(string state)
        {
            return string.Equals(state, "idle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RefreshYoloSettingsPanelAsync(PythonModelValidationResult validation = null)
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            validation ??= PythonModelSettingsValidator.Validate(settings, requireWeights: true);

            var detail = new StringBuilder();
            detail.AppendLine($"Python: {PythonModelSettingsValidator.ResolvePythonExecutable(settings)}");
            detail.AppendLine($"프로젝트: {settings.ProjectRootPath}");
            detail.AppendLine($"클라이언트: {settings.ClientScriptPath}");
            detail.AppendLine($"가중치: {settings.WeightsPath}");
            detail.AppendLine($"이미지: {settings.ImageRootPath}");
            detail.AppendLine($"필수 패키지: {settings.GetRequirementsPath()}");
            detail.AppendLine($"신뢰도: {settings.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture)}");
            detail.AppendLine($"시간 제한: {settings.DetectionTimeoutSeconds}s");
            AppendPythonWorkerStatus(detail);

            if (validation.Errors.Count > 0 || validation.Warnings.Count > 0)
            {
                detail.AppendLine();
                foreach (string error in validation.Errors)
                {
                    detail.AppendLine($"오류: {error}");
                }

                foreach (string warning in validation.Warnings)
                {
                    detail.AppendLine($"주의: {warning}");
                }
            }

            try
            {
                PythonEnvironmentCheckResult environment = await PythonEnvironmentService
                    .CheckRequirementsAsync(settings)
                    .ConfigureAwait(true);
                detail.AppendLine();
                detail.AppendLine($"패키지: {TranslatePythonEnvironmentSummary(environment.Summary)}");
                detail.AppendLine($"필수 패키지: {environment.RequiredPackages.Count}");
                if (environment.MissingPackages.Count > 0)
                {
                    detail.AppendLine($"누락: {string.Join(", ", environment.MissingPackages.Take(12))}");
                }
            }
            catch (Exception ex)
            {
                detail.AppendLine();
                detail.AppendLine($"패키지: 점검 실패 - {ex.Message}");
            }

            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetSettingsStatus(
                    validation.IsValid ? "YOLO \uC124\uC815 \uC900\uBE44 \uC644\uB8CC" : "YOLO \uC124\uC815 \uD655\uC778 \uD544\uC694",
                    detail.ToString());
                return;
            }

            YoloSettingsSummaryText.Text = validation.IsValid
                ? "YOLO 설정 준비 완료"
                : "YOLO 설정 확인 필요";
            YoloSettingsDetailText.Text = detail.ToString();
        }

        private void AppendPythonWorkerStatus(StringBuilder detail)
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            detail.AppendLine($"Worker: 리스너 {(status.IsListening ? "켜짐" : "꺼짐")} / 클라이언트 {(status.IsClientConnected ? "연결됨" : "미연결")} / 프로세스 {(global.PythonClientProcess.IsRunning ? "실행 중" : "중지")}");
            if (status.ListenerPort > 0)
            {
                detail.AppendLine($"Worker 포트: {status.ListenerPort}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastWorkerState)
                || !string.IsNullOrWhiteSpace(status.LastWorkerMessage))
            {
                detail.AppendLine($"Worker 상태: {FormatWorkerState(status.LastWorkerState)} {TranslateWorkerMessage(status.LastWorkerMessage)}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastModelState)
                || !string.IsNullOrWhiteSpace(status.LastModelMessage))
            {
                detail.AppendLine($"모델 상태: {FirstNonEmpty(status.LastModelState, "-")} / 로드:{status.LastModelLoaded} {status.LastModelMessage}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                || status.LastTrainingProgressPercent.HasValue)
            {
                string progress = status.LastTrainingProgressPercent.HasValue
                    ? $"{status.LastTrainingProgressPercent.Value}%"
                    : "-";
                string epoch = status.LastTrainingEpoch.HasValue && status.LastTrainingTotalEpochs.HasValue
                    ? $" 에폭 {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}"
                    : string.Empty;
                string trainingMessage = string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    ? string.Empty
                    : FormatTrainingMessage(status.LastTrainingMessage);
                detail.AppendLine($"학습: {FormatTrainingState(status.LastTrainingState)} {progress}{epoch} {trainingMessage}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastError))
            {
                detail.AppendLine($"Worker 오류: {status.LastError}");
            }
        }

        private string BuildPythonWorkerFailureText()
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            string error = FirstNonEmpty(status.LastError, global.PythonClientProcess.LastError, "상세 없음");
            return $"Python worker 연결 실패. Listener:{status.IsListening}, Client:{status.IsClientConnected}, Process:{global.PythonClientProcess.IsRunning}, 오류:{error}";
        }

        private int GetWorkerConnectTimeoutMilliseconds()
        {
            int detectionTimeoutSeconds = global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30;
            return Math.Clamp(detectionTimeoutSeconds * 1000, 8000, 60000);
        }

        private int GetInteractiveWorkerConnectTimeoutMilliseconds()
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            if (status.IsClientConnected)
            {
                return 1500;
            }

            return GetWorkerConnectTimeoutMilliseconds();
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds >= 1
                ? $"{elapsed.TotalSeconds:0.0}s"
                : $"{elapsed.TotalMilliseconds:0}ms";
        }

        private static int ClampElapsedMilliseconds(TimeSpan elapsed)
        {
            return (int)Math.Clamp(elapsed.TotalMilliseconds, 0D, int.MaxValue);
        }

        private static string FormatAverageElapsed(TimeSpan totalElapsed, int count)
        {
            if (count <= 0)
            {
                return "평균 -";
            }

            return $"평균 {FormatElapsed(TimeSpan.FromMilliseconds(totalElapsed.TotalMilliseconds / count))}";
        }

        private static string FormatInferencePath(string path)
        {
            return path switch
            {
                "worker" => "worker",
                "smoke fallback" => "smoke fallback",
                _ => FirstNonEmpty(path, "알 수 없음")
            };
        }

        private static string TranslatePythonEnvironmentSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return "상태 미확인";
            }

            return summary.Trim() switch
            {
                "Python environment is ready." => "Python 환경 준비 완료.",
                _ => summary.Trim()
            };
        }

        private static string FormatWorkerState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "" => "-",
                "listening" => "수신 대기",
                "connected" => "연결됨",
                "running" => "실행 중",
                "stopped" => "중지",
                "error" => "오류",
                _ => normalized
            };
        }

        private static string TranslateWorkerMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            return message.Trim() switch
            {
                "Python TCP listener is waiting for a client." => "Python TCP listener가 클라이언트 연결을 기다립니다.",
                "Python TCP listener stopped." => "Python TCP listener가 중지되었습니다.",
                _ => message.Trim()
            };
        }

        private static string CreateRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private void SetDatasetStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetDatasetStatus(normalized);
                return;
            }

            if (DatasetStatusText != null)
            {
                DatasetStatusText.Text = normalized;
            }
        }

        private void SetPythonStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetPythonStatus(normalized);
                return;
            }

            if (PythonStatusText != null)
            {
                PythonStatusText.Text = normalized;
            }
        }

        private void SetModelStatus(string text)
        {
            string normalized = text ?? string.Empty;
            if (StatusBarViewModel != null)
            {
                StatusBarViewModel.SetModelStatus(normalized);
                return;
            }

            if (ModelStatusText != null)
            {
                ModelStatusText.Text = normalized;
            }
        }

        private void MarkAnnotationsDirty(string reason)
        {
            annotationDirtyReason = string.IsNullOrWhiteSpace(reason) ? "Edit" : reason;
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: true,
                text: "\uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694",
                toolTip: $"\uC544\uC9C1 \uD30C\uC77C\uC5D0 \uC800\uC7A5\uB418\uC9C0 \uC54A\uC740 \uD3B8\uC9D1: {annotationDirtyReason}");
        }

        private void MarkAnnotationsSaved(string reason)
        {
            annotationDirtyReason = string.Empty;
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: false,
                text: "\uB77C\uBCA8 \uC800\uC7A5\uB428",
                toolTip: string.IsNullOrWhiteSpace(reason)
                    ? "\uD604\uC7AC \uB77C\uBCA8\uC774 \uD30C\uC77C\uC5D0 \uC800\uC7A5\uB418\uC5C8\uC2B5\uB2C8\uB2E4."
                    : reason);
        }

        private void SetAnnotationSaveStatusWaiting()
        {
            annotationDirtyReason = string.Empty;
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: false,
                text: "\uB77C\uBCA8 \uB300\uAE30",
                toolTip: "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(currentTheme == ShellTheme.Dark ? ShellTheme.Light : ShellTheme.Dark);
            AppendLog(currentTheme == ShellTheme.Dark ? "테마 변경: 다크" : "테마 변경: 라이트");
        }

        private void ApplyTheme(ShellTheme theme)
        {
            currentTheme = theme;
            WpfUiApplicationThemeManager.Apply(ToWpfUiTheme(theme), WpfUiWindowBackdropType.None, updateAccent: true);
            WpfUiApplicationThemeManager.Apply(this);

            if (theme == ShellTheme.Light)
            {
                SetThemeBrush("AppBackgroundBrush", "#F4F6F8");
                SetThemeBrush("FrameBrush", "#FFFFFF");
                SetThemeBrush("PanelBrush", "#FFFFFF");
                SetThemeBrush("PanelHeaderBrush", "#F1F3F6");
                SetThemeBrush("CanvasBrush", "#EDF2F8");
                SetThemeBrush("StatusBarBrush", "#FFFFFF");
                SetThemeBrush("BorderBrushDark", "#D8DEE8");
                SetThemeBrush("PrimaryTextBrush", "#151922");
                SetThemeBrush("SecondaryTextBrush", "#566170");
                SetThemeBrush("AccentBrush", "#E53935");
                SetThemeBrush("ToolbarButtonBrush", "#F7F9FC");
                SetThemeBrush("ToolbarButtonBorderBrush", "#CBD3DF");
                SetThemeBrush("ToolbarButtonHoverBrush", "#E8EEF7");
                SetThemeBrush("ToolbarButtonPressedBrush", "#DCE5F0");
                SetThemeBrush("ToolbarButtonDisabledBrush", "#EBEFF4");
                SetThemeBrush("ToolbarButtonDisabledBorderBrush", "#D5DDE8");
                SetThemeBrush("DisabledTextBrush", "#97A0AE");
                SetThemeBrush("InputBrush", "#FFFFFF");
                SetThemeBrush("InputBorderBrush", "#CAD2DD");
                SetThemeBrush("GridLineBrush", "#D6DEE8");
                SetThemeBrush("GridHeaderBrush", "#F1F3F6");
                SetThemeBrush("RowHoverBrush", "#E8EEF7");
                SetThemeBrush("SelectedRowBrush", "#DCEBFF");
                SetThemeBrush("SelectedRowTextBrush", "#101820");
                SetThemeBrush("DetectionOverlayBackgroundBrush", "#EAF7EF");
                SetThemeBrush("DetectionOverlayBorderBrush", "#3C22A65A");
                SetThemeBrush("DetectionOverlayTitleTextBrush", "#12351F");
                SetThemeBrush("DetectionOverlaySummaryTextBrush", "#157F3A");
                SetThemeBrush("DetectionOverlaySelectedBackgroundBrush", "#D9F4E2");
                SetThemeBrush("DetectionOverlaySelectedTextBrush", "#0E3B20");
                SetThemeBrush("DetectionOverlayDetailTextBrush", "#2E5A3D");
            }
            else
            {
                SetThemeBrush("AppBackgroundBrush", "#0C0D0F");
                SetThemeBrush("FrameBrush", "#0A0B0D");
                SetThemeBrush("PanelBrush", "#171717");
                SetThemeBrush("PanelHeaderBrush", "#1F1F1F");
                SetThemeBrush("CanvasBrush", "#101820");
                SetThemeBrush("StatusBarBrush", "#0F1115");
                SetThemeBrush("BorderBrushDark", "#303030");
                SetThemeBrush("PrimaryTextBrush", "#F7F7F7");
                SetThemeBrush("SecondaryTextBrush", "#B7B7B7");
                SetThemeBrush("AccentBrush", "#E53935");
                SetThemeBrush("ToolbarButtonBrush", "#252525");
                SetThemeBrush("ToolbarButtonBorderBrush", "#3A3A3A");
                SetThemeBrush("ToolbarButtonHoverBrush", "#333333");
                SetThemeBrush("ToolbarButtonPressedBrush", "#1D1D1D");
                SetThemeBrush("ToolbarButtonDisabledBrush", "#20242A");
                SetThemeBrush("ToolbarButtonDisabledBorderBrush", "#2B3038");
                SetThemeBrush("DisabledTextBrush", "#69707A");
                SetThemeBrush("InputBrush", "#242424");
                SetThemeBrush("InputBorderBrush", "#3A3A3A");
                SetThemeBrush("GridLineBrush", "#2A2A2A");
                SetThemeBrush("GridHeaderBrush", "#202020");
                SetThemeBrush("RowHoverBrush", "#222A33");
                SetThemeBrush("SelectedRowBrush", "#26384F");
                SetThemeBrush("SelectedRowTextBrush", "#FFFFFF");
                SetThemeBrush("DetectionOverlayBackgroundBrush", "#F00B1320");
                SetThemeBrush("DetectionOverlayBorderBrush", "#5524D366");
                SetThemeBrush("DetectionOverlayTitleTextBrush", "#FFFFFF");
                SetThemeBrush("DetectionOverlaySummaryTextBrush", "#BEEBD0");
                SetThemeBrush("DetectionOverlaySelectedBackgroundBrush", "#1F24D366");
                SetThemeBrush("DetectionOverlaySelectedTextBrush", "#FFFFFF");
                SetThemeBrush("DetectionOverlayDetailTextBrush", "#C9D4E2");
            }

            ThemeToggleText.Text = theme == ShellTheme.Dark ? "테마: 다크" : "테마: 라이트";
            if (FindResource("AppBackgroundBrush") is MediaBrush backgroundBrush)
            {
                Background = backgroundBrush;
            }

            UpdateWorkflowModeUi();
            UpdateQueueQuickFilterButtons();
        }

        private void SetThemeBrush(string key, string color)
        {
            Resources[key] = new MediaSolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(color));
        }

        private static WpfUiApplicationTheme ToWpfUiTheme(ShellTheme theme)
        {
            return theme == ShellTheme.Light ? WpfUiApplicationTheme.Light : WpfUiApplicationTheme.Dark;
        }

        private void AppendLog(string message)
        {
            OVLog.Write(LogCategory.Main, LogLevel.Info, message);
        }
    }
}
