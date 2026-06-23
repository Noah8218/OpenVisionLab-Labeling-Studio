using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Views;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using OpenVisionLab.Mvvm.Behaviors;
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
        private const string TutorialHtmlGuideRelativePath = @"docs\tutorial\labeling-workbench-tutorial.html";
        private const int BatchReviewStatusSaveInterval = 10;
        private const int TrainingStatusPollTimeoutSeconds = 600;
        private const int AnnotationHistoryLimit = 50;
        private const int ObjectReviewFullRefreshDeleteLimit = 10_000;
        private readonly CGlobal global = CGlobal.Inst;
        private readonly ObservableCollection<WpfImageQueueItem> imageQueueItems = new ObservableCollection<WpfImageQueueItem>();
        private readonly YoloImageReviewStatusService imageReviewStatus = new YoloImageReviewStatusService();
        private int queuedActiveImageQueueStatusRefreshVersion;
        private readonly WpfImageQueueSelectionService imageQueueSelectionService = new WpfImageQueueSelectionService();
        private readonly WpfImageDecodeCacheService imageDecodeCacheService = new WpfImageDecodeCacheService();
        private readonly WpfImageDecodeService imageDecodeService = new WpfImageDecodeService();
        private readonly WpfImageDecodePreloadService imageDecodePreloadService = new WpfImageDecodePreloadService();
        private WpfImageLoadDiagnostics lastImageLoadDiagnostics = WpfImageLoadDiagnostics.Empty;
        private ICollectionView imageQueueView;
        private CancellationTokenSource imageQueueDetailLoadCts;
        private Task imageQueueDetailLoadTask = Task.CompletedTask;
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
        private readonly WpfCandidateReviewStateService candidateReviewState = new WpfCandidateReviewStateService();
        private readonly WpfCandidateReviewPresentationService candidateReviewPresentationService = new WpfCandidateReviewPresentationService();
        private readonly WpfCandidateConfirmationService candidateConfirmationService = new WpfCandidateConfirmationService();
        private readonly WpfDetectionResultPresentationService detectionResultPresentationService = new WpfDetectionResultPresentationService();
        private readonly WpfDetectionTargetService detectionTargetService = new WpfDetectionTargetService();
        private readonly WpfBatchDetectionProgressService batchDetectionProgressService = new WpfBatchDetectionProgressService();
        private readonly WpfImageLoadPresentationService imageLoadPresentationService = new WpfImageLoadPresentationService();
        private readonly WpfObjectReviewPresentationService objectReviewPresentationService = new WpfObjectReviewPresentationService();
        private readonly WpfFileDialogService fileDialogService = new WpfFileDialogService();
        private readonly WpfTrainingWeightsService trainingWeightsService = new WpfTrainingWeightsService();
        private readonly WpfTrainingGuideHistoryService trainingGuideHistoryService = new WpfTrainingGuideHistoryService();
        private bool suppressImageQueueSelection;
        private bool isDetecting;
        private bool isBatchDetectionRunning;
        private bool isYoloEnvironmentCommandRunning;
        private bool isTrainingCommandRunning;
        private bool suppressProjectRecipeSelection;
        private int batchDetectionTotalCount;
        private int batchDetectionCompletedCount;
        private readonly Stopwatch inferenceStatusPulseStopwatch = new Stopwatch();
        private readonly DispatcherTimer inferenceStatusPulseTimer;
        private readonly DispatcherTimer trainingStatusPollTimer;
        private DateTime trainingStatusPollStartedUtc = DateTime.MinValue;
        private string lastAutoAppliedTrainingWeightsPath = string.Empty;
        private bool hasPendingTrainingWeightsRecipeSave;
        private YoloDatasetReadinessReport lastYoloTrainingReadinessReport;
        private string lastRecordedTrainingGuideRunSignature = string.Empty;
        private ShellTheme currentTheme = ShellTheme.Dark;
        private WorkflowMode currentWorkflowMode = WorkflowMode.Labeling;
        private WpfAnnotationTool activeAnnotationTool = WpfAnnotationTool.Select;
        private bool applyingAnnotationToolSelection;
        private System.Drawing.Point? lastMaskStrokePoint;
        private WpfAnnotationHistorySnapshot activeMaskStrokeSnapshot;
        private readonly HashSet<int> activeMaskStrokeSegmentIndices = new HashSet<int>();
        private readonly WpfMaskStrokeCommitSession activeMaskStrokeCommitSession = new WpfMaskStrokeCommitSession();
        private bool activeMaskStrokeChanged;
        private bool activeMaskStrokeNeedsFullObjectRefresh;
        private int activeSegmentDragIndex = -1;
        private int activePolygonPointDragIndex = -1;
        private System.Drawing.Point? lastSegmentDragPoint;
        private WpfAnnotationHistorySnapshot activeSegmentDragSnapshot;
        private bool activeSegmentDragChanged;
        private bool suppressAnnotationHistory;
        private string annotationDirtyReason = string.Empty;
        private string activeRoiEditHistoryOverlayId = string.Empty;
        private readonly WpfLabelingShellViewModels viewModels;

        public WpfLabelingShellWindow()
            : this(new WpfLabelingShellViewModels())
        {
        }

        private void SeedImageQueueInputCommands()
        {
            // The shell is the composition root; seed behavior commands so pre-Loaded queue selection uses the same path as real clicks.
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueFilterBox, ImageQueueViewModel.FilterSelectionChangedCommand);
            InputCommandBehaviors.SetTextInputCommand(ImageQueueSearchBox, ImageQueueViewModel.SearchTextChangedCommand);
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueGrid, ImageQueueViewModel.QueueSelectionChangedCommand);
            InputCommandBehaviors.SetMouseDoubleClickInputCommand(ImageQueueGrid, ImageQueueViewModel.QueueMouseDoubleClickCommand);
        }

        internal WpfLabelingShellWindow(WpfLabelingShellViewModels viewModels)
        {
            this.viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
            InitializeComponent();
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
            DataContext = viewModels;
            ComposePanelViewModels();
            ConfigureShellCommands();
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

        private void ExecuteShellPreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null
                || (e.Modifiers & ModifierKeys.Control) != ModifierKeys.Control
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

        public WpfLabelingShellViewModel ShellViewModel => viewModels.ShellViewModel;

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => viewModels.LearningWorkflowViewModel;

        public WpfImageQueuePanelViewModel ImageQueueViewModel => viewModels.ImageQueueViewModel;

        public WpfCanvasPanelViewModel CanvasPanelViewModel => viewModels.CanvasPanelViewModel;

        public WpfObjectReviewPanelViewModel ObjectReviewViewModel => viewModels.ObjectReviewViewModel;

        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel => viewModels.CandidateReviewViewModel;

        public WpfClassCatalogPanelViewModel ClassCatalogViewModel => viewModels.ClassCatalogViewModel;

        public WpfYoloStatusPanelViewModel YoloStatusViewModel => viewModels.YoloStatusViewModel;

        public WpfProjectConfigPanelViewModel ProjectConfigViewModel => viewModels.ProjectConfigViewModel;

        public WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel => viewModels.YoloModelSettingsViewModel;

        public WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel => viewModels.TrainingSettingsViewModel;

        public WpfStatusBarPanelViewModel StatusBarViewModel => viewModels.StatusBarViewModel;

        public WpfShellLogPanelViewModel ShellLogViewModel => viewModels.ShellLogViewModel;

        public RoiImageCanvasViewModel MainCanvasViewModel => viewModels.MainCanvasViewModel;
        public ObservableCollection<WpfImageQueueItem> ImageQueueItems => imageQueueItems;

        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates => candidateReviewState.PendingCandidates;

        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates => candidateReviewState.ConfirmedCandidates;

        private void ComposePanelViewModels()
        {
            // Keep ViewModels out of UserControls; the shell composes data contexts so each View can be constructed standalone.
            LearningWorkflowPanelControl.DataContext = LearningWorkflowViewModel;
            ImageQueuePanelControl.DataContext = ImageQueueViewModel;
            CanvasPanelControl.DataContext = CanvasPanelViewModel;
            ObjectReviewPanelControl.DataContext = ObjectReviewViewModel;
            CandidateReviewPanelControl.DataContext = CandidateReviewViewModel;
            ClassCatalogPanelControl.DataContext = ClassCatalogViewModel;
            YoloStatusPanelControl.DataContext = YoloStatusViewModel;
            ProjectConfigPanelControl.DataContext = ProjectConfigViewModel;
            YoloModelSettingsPanelControl.DataContext = YoloModelSettingsViewModel;
            TrainingSettingsPanelControl.DataContext = TrainingSettingsViewModel;
            StatusBarPanelControl.DataContext = StatusBarViewModel;
            ShellLogPanelControl.DataContext = ShellLogViewModel;
        }

        private static void RefreshAttachedCommandBindings(DependencyObject target, params DependencyProperty[] properties)
        {
            if (target == null || properties == null)
            {
                return;
            }

            // Command ViewModels are injected after InitializeComponent; refresh attached-event bindings before the first user input.
            foreach (DependencyProperty property in properties)
            {
                BindingOperations.GetBindingExpression(target, property)?.UpdateTarget();
            }
        }

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

            PushAnnotationHistorySnapshot(CaptureManualRoiHistory("Remove ROI"));
            manualRois.RemoveAt(index);
            RemoveAtIfPresent(manualRoiClassNames, index);
            RemoveAtIfPresent(manualRoiShapeKinds, index);
            RemoveAtIfPresent(manualRoiOverlayIds, index);
            // Canvas ViewModel owns the OpenGL overlay removal after this event; the shell only updates model/review state here.
            RefreshObjectReviewAfterDelete(WpfObjectReviewSource.ManualRoi, index);
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
            // Tool changes should not drop a GPU-previewed brush stroke before MouseUp.
            CompleteMaskAnnotationStroke();
            SetWorkflowMode(WorkflowMode.Labeling);
            activeAnnotationTool = tool;
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            polygonAnnotationService.Reset();
            lastMaskStrokePoint = null;
            activeMaskStrokeSnapshot = null;
            activeMaskStrokeSegmentIndices.Clear();
            ResetMaskStrokeCommitBuffer();
            activeMaskStrokeChanged = false;
            activeMaskStrokeNeedsFullObjectRefresh = false;
            MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
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
            MainCanvasViewModel?.ClearMaskStrokePreview();
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
                MainCanvasViewModel?.ClearMaskStrokePreview();
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
                // Match the old Viewer2D brush flow: MouseMove only feeds the GPU/FBO
                // edit preview, while CPU MaskData and object rows are committed once on MouseUp.
                activeMaskStrokeSnapshot = CaptureAnnotationHistory(actionName);
                activeMaskStrokeSegmentIndices.Clear();
                activeMaskStrokeChanged = false;
                activeMaskStrokeNeedsFullObjectRefresh = false;
                activeMaskStrokeCommitSession.Begin(
                    radius,
                    activeAnnotationTool,
                    FirstNonEmpty(GetSelectedClassName(), "Defect"));
                MainCanvasViewModel?.BeginMaskStrokePreview(
                    activeImageSize,
                    GetMaskCursorPreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                    activeAnnotationTool == WpfAnnotationTool.Eraser);
            }

            IReadOnlyList<System.Drawing.Point> previewCenters = AppendMaskStrokeCommitCenters(centers);
            if (previewCenters.Count == 0)
            {
                return;
            }

            MainCanvasViewModel?.AddMaskStrokePreview(
                previewCenters,
                radius,
                GetMaskCursorPreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                activeAnnotationTool == WpfAnnotationTool.Eraser);
            string action = activeAnnotationTool == WpfAnnotationTool.Brush ? "Mask paint preview" : "Mask erase preview";
            SetModelStatus($"{action}: {activeMaskStrokeCommitSession.Count} stroke point(s)");
        }

        private void CompleteMaskAnnotationStroke()
        {
            bool strokeWasActive = activeMaskStrokeSnapshot != null;
            bool commitChangedStroke = strokeWasActive && CommitMaskAnnotationStrokeCenters();
            if (commitChangedStroke)
            {
                PushAnnotationHistorySnapshot(activeMaskStrokeSnapshot);
                if (!TryRefreshMaskStrokeObjectReviewRows())
                {
                    RefreshObjectList();
                }

                ObjectsReviewTab.IsSelected = true;
                SetModelStatus($"Mask edit committed: {manualSegments.Count} segment object(s)");
                RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
                // Existing mask edits keep the OpenGL texture/cache path hot; new or removed masks still need a full rebuild.
                if (!TryRefreshMaskStrokeCanvasOverlays())
                {
                    RefreshPolygonOverlays();
                }
            }
            else
            {
                MainCanvasViewModel?.ClearMaskStrokePreview();
            }

            activeMaskStrokeSnapshot = null;
            activeMaskStrokeSegmentIndices.Clear();
            ResetMaskStrokeCommitBuffer();
            activeMaskStrokeChanged = false;
            activeMaskStrokeNeedsFullObjectRefresh = false;
        }

        private IReadOnlyList<System.Drawing.Point> AppendMaskStrokeCommitCenters(IEnumerable<System.Drawing.Point> centers)
            => activeMaskStrokeCommitSession.Append(centers, activeImageSize);

        private bool CommitMaskAnnotationStrokeCenters()
        {
            if (activeMaskStrokeCommitSession.Count == 0)
            {
                return false;
            }

            int radius = activeMaskStrokeCommitSession.Radius > 0 ? activeMaskStrokeCommitSession.Radius : GetMaskBrushRadius();
            bool changed;
            if (activeMaskStrokeCommitSession.Tool == WpfAnnotationTool.Brush)
            {
                CClassItem classItem = EnsureClassItem(FirstNonEmpty(activeMaskStrokeCommitSession.ClassName, GetSelectedClassName(), "Defect"));
                int segmentCountBeforePaint = manualSegments.Count;
                changed = maskAnnotationService.Paint(
                    manualSegments,
                    activeMaskStrokeCommitSession.Centers,
                    radius,
                    activeImageSize,
                    classItem,
                    out LabelingSegmentationObject changedSegment,
                    out _);
                TrackMaskStrokeSegment(changedSegment);
            }
            else if (activeMaskStrokeCommitSession.Tool == WpfAnnotationTool.Eraser)
            {
                int segmentCountBeforeErase = manualSegments.Count;
                changed = maskAnnotationService.Erase(
                    manualSegments,
                    activeMaskStrokeCommitSession.Centers,
                    radius,
                    activeImageSize,
                    out _,
                    out IReadOnlyList<LabelingSegmentationObject> changedSegments);
                TrackMaskStrokeSegments(changedSegments);
                activeMaskStrokeNeedsFullObjectRefresh |= manualSegments.Count != segmentCountBeforeErase;
            }
            else
            {
                changed = false;
            }

            activeMaskStrokeChanged = changed;
            return changed;
        }

        private void ResetMaskStrokeCommitBuffer()
            => activeMaskStrokeCommitSession.Reset();

        private void TrackMaskStrokeSegment(LabelingSegmentationObject segment)
        {
            if (segment == null)
            {
                return;
            }

            int index = manualSegments.IndexOf(segment);
            if (index >= 0)
            {
                activeMaskStrokeSegmentIndices.Add(index);
                return;
            }

            activeMaskStrokeNeedsFullObjectRefresh = true;
        }

        private void TrackMaskStrokeSegments(IEnumerable<LabelingSegmentationObject> segments)
        {
            foreach (LabelingSegmentationObject segment in segments ?? Array.Empty<LabelingSegmentationObject>())
            {
                TrackMaskStrokeSegment(segment);
            }
        }

        private bool TryRefreshMaskStrokeObjectReviewRows()
        {
            if (activeMaskStrokeNeedsFullObjectRefresh
                || activeMaskStrokeSegmentIndices.Count == 0
                || ObjectReviewViewModel == null)
            {
                return false;
            }

            string summary = WpfObjectReviewPresenter.BuildSummary(
                manualRois.Count + manualSegments.Count + confirmedDetectionCandidates.Count);
            bool selectChangedMask = activeMaskStrokeSegmentIndices.Count == 1;
            foreach (int segmentIndex in activeMaskStrokeSegmentIndices.OrderBy(index => index))
            {
                if (!TryRefreshManualSegmentObjectReviewRow(segmentIndex, summary, selectChangedMask))
                {
                    return false;
                }
            }

            return true;
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
                    if (TryBuildManualMaskOverlay(i, selectedSourceKey, selectedSourceIndex, maskOpacity, out RoiImageCanvasMaskOverlay maskOverlay))
                    {
                        maskOverlays.Add(maskOverlay);
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

        private bool TryRefreshMaskStrokeCanvasOverlays()
        {
            if (activeMaskStrokeNeedsFullObjectRefresh
                || activeMaskStrokeSegmentIndices.Count == 0
                || MainCanvasViewModel == null)
            {
                return false;
            }

            float maskOpacity = (float)(LearningWorkflowViewModel?.MaskOpacity ?? 0.66);
            WpfObjectReviewListItem selectedObject = ObjectReviewViewModel?.SelectedObject;
            string selectedSourceKey = selectedObject?.SourceKey ?? string.Empty;
            int selectedSourceIndex = selectedObject?.SourceIndex ?? -1;
            foreach (int segmentIndex in activeMaskStrokeSegmentIndices.OrderBy(index => index))
            {
                if (!TryBuildManualMaskOverlay(segmentIndex, selectedSourceKey, selectedSourceIndex, maskOpacity, out RoiImageCanvasMaskOverlay maskOverlay)
                    || !MainCanvasViewModel.TryUpdateMaskOverlay(maskOverlay))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryBuildManualMaskOverlay(
            int segmentIndex,
            string selectedSourceKey,
            int selectedSourceIndex,
            float maskOpacity,
            out RoiImageCanvasMaskOverlay overlay)
        {
            overlay = null;
            if (segmentIndex < 0 || segmentIndex >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[segmentIndex];
            if (segment?.IsRasterMask != true || segment.MaskData == null || segment.MaskSize.IsEmpty || segment.Bounds.IsEmpty)
            {
                return false;
            }

            DrawingRectangle maskBounds = DrawingRectangle.Intersect(
                segment.Bounds,
                new DrawingRectangle(0, 0, segment.MaskSize.Width, segment.MaskSize.Height));
            if (maskBounds.IsEmpty)
            {
                return false;
            }

            string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
            bool isSegmentSelected = string.Equals(
                selectedSourceKey,
                WpfObjectReviewSource.ManualSegment.ToString(),
                StringComparison.OrdinalIgnoreCase)
                && selectedSourceIndex == segmentIndex;
            int displayIndex = manualRois.Count + segmentIndex + 1;
            overlay = new RoiImageCanvasMaskOverlay(
                $"{activeImagePath}|mask|{segmentIndex}",
                segment.MaskData,
                segment.MaskSize,
                maskBounds,
                segment.Color,
                maskOpacity,
                segment.RenderVersion,
                isSegmentSelected,
                $"MASK {displayIndex} {className}",
                segment.RenderDirtyBounds,
                (uploadedVersion, uploadedBounds) => ClearMaskRenderDirtyBounds(segment, uploadedVersion, uploadedBounds));
            return true;
        }
        private static void ClearMaskRenderDirtyBounds(LabelingSegmentationObject segment, int uploadedVersion, DrawingRectangle uploadedBounds)
        {
            if (segment == null || uploadedBounds.IsEmpty || segment.RenderVersion != uploadedVersion)
            {
                return;
            }

            // The OpenGL texture has consumed this exact render version. Keep newer
            // stroke dirt intact so a fast MouseMove cannot clear work not uploaded yet.
            segment.RenderDirtyBounds = DrawingRectangle.Empty;
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

        private WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
        {
            return WpfAnnotationHistoryService.CaptureManualRoiList(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds);
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

        private void ExecuteUndoAnnotationCommand()
        {
            UndoWpfAnnotationHistory();
        }

        private void ExecuteRedoAnnotationCommand()
        {
            RedoWpfAnnotationHistory();
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
                    candidateReviewState.MutablePendingCandidates,
                    candidateReviewState.MutableConfirmedCandidates);

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
            => WpfObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, overlayId);

        private CanvasRoiShapeKind GetManualRoiShapeKind(int index)
            => WpfObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, index);

        private string GetManualRoiOverlayId(int index)
            => WpfObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, index);

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
            => WpfObjectReviewPresentationService.FormatManualRoiShapeName(shapeKind);

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

        public void FocusAnnotationToolsTab()
        {
            LearningReviewTab.IsSelected = true;
            UpdateLayout();
            LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
        }

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
        private ListBox CanvasAnnotationToolListBox => CanvasPanelControl?.AnnotationToolList;
        private Border CanvasWorkflowContextStrip => CanvasPanelControl?.WorkflowContextStrip;
        private TextBlock CanvasCurrentStepText => CanvasPanelControl?.CurrentStepText;
        private TextBlock CanvasCurrentToolText => CanvasPanelControl?.CurrentToolText;
        private TextBlock CanvasNextActionText => CanvasPanelControl?.NextActionText;
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
        private TextBox ClassNameBox => ClassCatalogPanelControl?.ClassNameTextBox;
        private Wpf.Ui.Controls.Button AddClassButton => ClassCatalogPanelControl?.AddClass;
        private Wpf.Ui.Controls.Button RemoveClassButton => ClassCatalogPanelControl?.RemoveClass;
        private TextBox OutputRootPathBox => ClassCatalogPanelControl?.OutputRootPathTextBox;
        private Wpf.Ui.Controls.Button BrowseOutputRootButton => ClassCatalogPanelControl?.BrowseOutputRoot;
        private Wpf.Ui.Controls.Button SaveOutputRootButton => ClassCatalogPanelControl?.SaveOutputRoot;
        private TextBlock ClassEditStatusText => ClassCatalogPanelControl?.StatusTextBlock;
        private ListBox ClassListBox => ClassCatalogPanelControl?.ClassList;
        private TextBlock YoloSettingsSummaryText => YoloStatusPanelControl?.SummaryTextBlock;
        private TextBlock YoloSettingsDetailText => YoloStatusPanelControl?.DetailTextBlock;
        private Wpf.Ui.Controls.Button FirstCheckYoloButton => YoloStatusPanelControl?.FirstCheckButton;
        private Wpf.Ui.Controls.Button InstallRequirementsButton => YoloStatusPanelControl?.InstallRequirements;
        private Wpf.Ui.Controls.Button RunYoloSmokeButton => YoloStatusPanelControl?.RunSmokeButton;
        private Wpf.Ui.Controls.Button RestartPythonWorkerButton => YoloStatusPanelControl?.RestartWorkerButton;
        private Wpf.Ui.Controls.Button StopPythonWorkerButton => YoloStatusPanelControl?.StopWorkerButton;
        private TextBlock YoloCommandStatusText => YoloStatusPanelControl?.CommandStatusTextBlock;
        private ProgressBar YoloCommandProgressBar => YoloStatusPanelControl?.CommandProgress;
        private Expander ProjectConfigExpander => ProjectConfigPanelControl?.SettingsExpander;
        private TextBox ProjectRecipeNameBox => ProjectConfigPanelControl?.RecipeNameBox;
        private ComboBox ProjectRecipeListBox => ProjectConfigPanelControl?.RecipeListBox;
        private TextBox ProjectConfigPathBox => ProjectConfigPanelControl?.ConfigPathBox;
        private TextBlock ProjectConfigStatusText => ProjectConfigPanelControl?.StatusTextBlock;
        private Wpf.Ui.Controls.Button ApplyProjectRecipeButton => ProjectConfigPanelControl?.ApplyRecipeButton;
        private Wpf.Ui.Controls.Button RefreshProjectRecipeListButton => ProjectConfigPanelControl?.RefreshRecipeListButton;
        private Wpf.Ui.Controls.Button SaveProjectConfigButton => ProjectConfigPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button OpenProjectConfigFolderButton => ProjectConfigPanelControl?.OpenFolderButton;
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
        private TextBlock DatasetStatusText => StatusBarPanelControl?.DatasetStatusTextBlock;
        private TextBlock PythonStatusText => StatusBarPanelControl?.PythonStatusTextBlock;
        private TextBlock AnnotationSaveStatusText => StatusBarPanelControl?.AnnotationSaveStatusTextBlock;
        private TextBlock ModelStatusText => StatusBarPanelControl?.ModelStatusTextBlock;
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

        public WpfImageLoadDiagnostics LastImageLoadDiagnostics => lastImageLoadDiagnostics;

        private void ConfigureShellCommands()
        {
            ShellViewModel.ConfigureCommands(
                ExecuteToggleThemeCommand,
                ExecuteLoadSampleCommand,
                ExecuteAddSampleRoiCommand,
                ExecuteSaveAnnotationsCommand,
                ExecuteLabelingModeCommand,
                ExecuteInferenceModeCommand,
                ExecuteCheckYoloCommand,
                ExecuteDetectCurrentImageCommand,
                ExecuteLoadedCommand,
                ExecuteClosedCommand,
                ExecuteShellPreviewKeyDownCommand);
            RefreshAttachedCommandBindings(
                this,
                WindowLifecycleCommandBehavior.LoadedCommandProperty,
                WindowLifecycleCommandBehavior.ClosedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }
        private void ConfigureLearningWorkflowPanelCommands()
        {
            LearningWorkflowViewModel.ConfigureCommands(
                selected => LearningWorkflowModeListBox_SelectionChanged(LearningModeListBox, selected),
                selected => AnnotationToolListBox_SelectionChanged(AnnotationToolListBox, selected),
                selected => LearningStepListBox_SelectionChanged(LearningStepListBox, selected),
                step => ExecuteYoloTrainingWorkflowStep(step?.Order ?? 0, LearningWorkflowPanelControl),
                ExecuteOpenTutorialHtmlGuideCommand,
                ExecuteFixYoloClassesCommand,
                ExecuteFixYoloLabelsCommand,
                ExecuteFixYoloDatasetCommand);
            RefreshAttachedCommandBindings(LearningModeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(AnnotationToolListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(LearningStepListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterLearningWorkflowPanelNames()
        {
            ConfigureLearningWorkflowPanelCommands();
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
            ConfigureImageQueuePanelCommands();
            ImageQueueFilterBox.ItemsSource = WpfImageQueueFilterOption.CreateDefaults();
            ImageQueueFilterBox.SelectedIndex = 0;
            imageQueueView = CollectionViewSource.GetDefaultView(imageQueueItems);
            imageQueueView.Filter = item => ShouldShowImageQueueItem(item as WpfImageQueueItem);
            ImageQueueGrid.ItemsSource = imageQueueView;
            UpdateQueueQuickFilterButtons();
        }

        private void ConfigureImageQueuePanelCommands()
        {
            ImageQueueViewModel.ConfigureCommands(
                ExecuteLoadImageRootQueueCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteRefreshImageQueueCommand,
                ExecuteNextUnlabeledQueueCommand,
                ExecuteOpenSelectedQueueImageCommand,
                ExecuteDetectSelectedQueueCommand,
                ExecuteBatchDetectQueueCommand,
                ExecuteRetryFailedQueueCommand,
                ExecuteStopBatchQueueCommand,
                ExecuteQueueFilterAllCommand,
                ExecuteQueueFilterCandidateCommand,
                ExecuteQueueFilterFailedCommand,
                ExecuteQueueFilterConfirmedCommand,
                ExecuteQueueFilterSkippedCommand,
                ExecuteQueueFilterNoCandidateCommand,
                ExecuteSelectedQueueItemChanged,
                selected => ImageQueueFilterBox_SelectionChanged(ImageQueueFilterBox, selected),
                text => ImageQueueSearchBox_TextChanged(ImageQueueSearchBox, text),
                selected => ImageQueueGrid_SelectionChanged(ImageQueueGrid, selected),
                () => ImageQueueGrid_MouseDoubleClick(ImageQueueGrid));
            RefreshAttachedCommandBindings(ImageQueueFilterBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(ImageQueueSearchBox, InputCommandBehaviors.TextInputCommandProperty);
            RefreshAttachedCommandBindings(
                ImageQueueGrid,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.MouseDoubleClickInputCommandProperty);
            SeedImageQueueInputCommands();
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

        private void ConfigureCanvasPanelCommands()
        {
            CanvasPanelViewModel.ConfigureCommands(
                ExecuteFitCanvasCommand,
                ExecuteActualSizeCanvasCommand,
                ExecutePanCanvasCommand,
                ExecuteFocusCandidateCommand,
                ExecuteResetAiOverlayCommand);
            CanvasPanelViewModel.ConfigureAnnotationTools(
                LearningWorkflowViewModel.AnnotationTools,
                LearningWorkflowViewModel.SelectedTool,
                ExecuteCanvasAnnotationToolSelectionChanged);
            CanvasPanelViewModel.ConfigureAnnotationCommands(
                ExecuteUndoAnnotationCommand,
                ExecuteRedoAnnotationCommand,
                ExecuteDeleteObjectCommand);
            RefreshCanvasWorkflowContext();
            RefreshAttachedCommandBindings(
                CanvasAnnotationToolListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RefreshCanvasWorkflowContext()
        {
            // The shell remains the workflow composer; the canvas view only binds the summarized state.
            WpfLearningStepItem selectedStep = LearningWorkflowViewModel?.SelectedStep;
            WpfAnnotationToolItem selectedTool = CanvasPanelViewModel?.SelectedAnnotationTool
                ?? LearningWorkflowViewModel?.SelectedTool;
            CanvasPanelViewModel?.SetWorkflowContext(
                selectedStep?.Text,
                selectedTool?.Text,
                BuildCanvasWorkflowActionText(selectedStep, selectedTool));
        }

        private static string BuildCanvasWorkflowActionText(WpfLearningStepItem selectedStep, WpfAnnotationToolItem selectedTool)
        {
            if (selectedStep?.Step == WpfLearningStep.Label)
            {
                return selectedTool?.Tool switch
                {
                    WpfAnnotationTool.Rectangle => "캔버스에서 드래그해 박스를 만들고 클래스를 확인하세요.",
                    WpfAnnotationTool.Ellipse => "드래그해 원형 영역을 만들고 클래스와 위치를 확인하세요.",
                    WpfAnnotationTool.Polygon => "꼭짓점을 찍어 경계를 만들고 마지막 점에서 마무리하세요.",
                    WpfAnnotationTool.Brush => "드래그해 마스크를 칠하고 놓은 뒤 결과를 확인하세요.",
                    WpfAnnotationTool.Eraser => "마스크 위를 드래그해 지울 영역을 정리하세요.",
                    WpfAnnotationTool.PanZoom => "이미지를 끌어 위치를 맞춘 뒤 라벨 도구로 돌아가세요.",
                    _ => "라벨을 클릭해 선택한 뒤 이동하거나 크기를 조절하세요."
                };
            }

            return selectedStep?.Step switch
            {
                WpfLearningStep.Sample => "이미지를 열거나 왼쪽 큐에서 선택하세요.",
                WpfLearningStep.Infer => "현재 검사로 AI 후보를 만들고 검토 탭에서 확인하세요.",
                WpfLearningStep.Review => "후보를 확정, 전체 확정, 또는 스킵하세요.",
                WpfLearningStep.Save => "저장 후 YOLO 폴더와 데이터셋 상태를 확인하세요.",
                _ => "다음 작업을 선택하세요."
            };
        }

        private void RegisterCanvasPanelNames()
        {
            ConfigureCanvasPanelCommands();
            RegisterCanvasName(nameof(MainCanvasView), MainCanvasView);
            RegisterCanvasName(nameof(CanvasAnnotationToolListBox), CanvasAnnotationToolListBox);
            RegisterCanvasName(nameof(CanvasWorkflowContextStrip), CanvasWorkflowContextStrip);
            RegisterCanvasName(nameof(CanvasCurrentStepText), CanvasCurrentStepText);
            RegisterCanvasName(nameof(CanvasCurrentToolText), CanvasCurrentToolText);
            RegisterCanvasName(nameof(CanvasNextActionText), CanvasNextActionText);
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

        private void ConfigureObjectReviewPanelCommands()
        {
            ObjectReviewViewModel.ConfigureCommands(
                ExecuteDeleteObjectCommand,
                ExecuteApplyObjectClassCommand,
                ExecuteObjectSelectionChangedCommand,
                ExecuteObjectPreviewKeyDownCommand);
            RefreshAttachedCommandBindings(
                ObjectListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }

        private void RegisterObjectReviewPanelNames()
        {
            ConfigureObjectReviewPanelCommands();
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

        private void ConfigureCandidateReviewPanelCommands()
        {
            CandidateReviewViewModel.ConfigureCommands(
                ExecuteCandidateConfidenceChangedCommand,
                ExecuteConfirmSelectedCandidateCommand,
                ExecuteConfirmAllCandidatesCommand,
                ExecuteSkipSelectedCandidateCommand,
                ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand,
                ExecuteFocusCandidateCommand,
                ExecuteCandidateSelectionChangedCommand,
                ExecuteCandidatePreviewKeyDownCommand);
            RefreshAttachedCommandBindings(CandidateConfidenceSlider, InputCommandBehaviors.ValueInputCommandProperty);
            RefreshAttachedCommandBindings(
                CandidateListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }

        private void RegisterCandidateReviewPanelNames()
        {
            ConfigureCandidateReviewPanelCommands();
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

        private void ConfigureClassCatalogPanelCommands()
        {
            ClassCatalogViewModel.ConfigureCommands(
                args => ClassNameBox_KeyDown(ClassNameBox, args),
                ExecuteAddClassCommand,
                ExecuteRemoveClassCommand,
                ExecuteBrowseOutputRootCommand,
                ExecuteSaveOutputRootCommand,
                selected => ClassListBox_SelectionChanged(ClassListBox, selected));
            RefreshAttachedCommandBindings(ClassNameBox, InputCommandBehaviors.PreviewKeyInputCommandProperty);
            RefreshAttachedCommandBindings(ClassListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterClassCatalogPanelNames()
        {
            ConfigureClassCatalogPanelCommands();
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

        private void ConfigureYoloStatusPanelCommands()
        {
            YoloStatusViewModel.ConfigureCommands(
                ExecuteCheckYoloCommand,
                ExecuteInstallRequirementsCommand,
                ExecuteRunYoloSmokeCommand,
                ExecuteRestartPythonWorkerCommand,
                ExecuteStopPythonWorkerCommand);
        }

        private void RegisterYoloStatusPanelNames()
        {
            ConfigureYoloStatusPanelCommands();
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

        private void ConfigureProjectConfigPanelCommands()
        {
            ProjectConfigViewModel.ConfigureCommands(
                ExecuteApplyProjectRecipeCommand,
                ExecuteRefreshProjectRecipeListCommand,
                ExecuteSaveProjectConfigCommand,
                ExecuteOpenProjectConfigFolderCommand,
                selected => ProjectRecipeListBox_SelectionChanged(ProjectRecipeListBox, selected));
            RefreshAttachedCommandBindings(ProjectRecipeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterProjectConfigPanelNames()
        {
            ConfigureProjectConfigPanelCommands();
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

        private void ConfigureYoloModelSettingsPanelCommands()
        {
            YoloModelSettingsViewModel.ConfigureCommands(
                ExecuteBrowseYoloPythonCommand,
                ExecuteBrowseYoloProjectRootCommand,
                ExecuteBrowseYoloClientScriptCommand,
                ExecuteBrowseYoloWeightsCommand,
                ExecuteBrowseYoloImageRootCommand,
                ExecuteSaveYoloSettingsCommand,
                ExecuteResetYoloSettingsCommand);
        }

        private void RegisterYoloModelSettingsPanelNames()
        {
            ConfigureYoloModelSettingsPanelCommands();
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

        private void ConfigureTrainingSettingsPanelCommands()
        {
            TrainingSettingsViewModel.ConfigureCommands(
                ExecuteRefreshTrainingReadinessCommand,
                ExecuteStartTrainingCommand,
                ExecuteStopTrainingCommand);
        }

        private void RegisterTrainingSettingsPanelNames()
        {
            ConfigureTrainingSettingsPanelCommands();
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
                SetDatasetStatus(imageLoadPresentationService.BuildStartupSampleMissingDatasetStatus());
                AppendLog(imageLoadPresentationService.BuildStartupSampleMissingLog());
                return false;
            }

            return TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false);
        }

        public bool TryLoadImage(string imagePath, bool populateQueue = true, bool refreshQueueDetails = true, bool refreshActiveStatus = true, bool appendLoadLog = true)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                AppendLog(imageLoadPresentationService.BuildMissingImageLog(imagePath));
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
                if (imageDecodeCacheService.TryTake(imagePath, out WpfCachedDecodedImage cachedImage))
                {
                    cacheHit = true;
                    workspaceBitmap = cachedImage.TakeBitmap();
                    imageMat = cachedImage.TakeMat();
                    cachedImage.Dispose();
                }
                else
                {
                    using WpfCachedDecodedImage decodedImage = imageDecodeService.DecodeForCanvas(imagePath);
                    workspaceBitmap = decodedImage.TakeBitmap();
                    imageMat = decodedImage.TakeMat();
                }
                decodeMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                string imageName = Path.GetFileNameWithoutExtension(imagePath);
                using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
                {
                    MainCanvasViewModel.LoadImage(imageMat, Path.GetFileName(imagePath));
                    MainCanvasViewModel.ClearRois();
                    MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                    MainCanvasViewModel.SetMaskOverlays(Array.Empty<RoiImageCanvasMaskOverlay>());
                    MainCanvasViewModel.SetPolygonOverlays(Array.Empty<RoiImageCanvasPolygonOverlay>());
                }
                canvasUploadMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                MainCanvasViewModel.ImageViewer.RefreshGL();
                canvasRefreshMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

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
                stateTransferMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                manualRois.Clear();
                manualRoiClassNames.Clear();
                manualRoiShapeKinds.Clear();
                manualRoiOverlayIds.Clear();
                manualSegments.Clear();
                polygonAnnotationService.Reset();
                lastMaskStrokePoint = null;
                activeMaskStrokeSnapshot = null;
                activeMaskStrokeChanged = false;
                candidateReviewState.ClearAll();
                ClearAnnotationHistory();
                UpdateDetectionResultOverlay();
                annotationResetMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (populateQueue)
                {
                    PopulateImageQueue(Path.GetDirectoryName(imagePath), imagePath, refreshQueueDetails);
                }
                queuePopulateMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                SetDatasetStatus(imageLoadPresentationService.BuildLoadedDatasetStatus(imagePath, activeImageSize));
                SetModelStatus(imageLoadPresentationService.BuildModelStatus(global.Data.ProjectSettings?.PythonModel?.WeightsPath));
                MarkAnnotationsSaved(imageLoadPresentationService.BuildAnnotationLoadedStatus());
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
                reviewRefreshMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (!appendLoadLog)
                {
                    PreloadAdjacentQueueImages(imagePath);
                    preloadScheduleMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
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
                AppendLog(imageLoadPresentationService.BuildLoadLog(imagePath));
                PreloadAdjacentQueueImages(imagePath);
                preloadScheduleMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
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
                SetDatasetStatus(imageLoadPresentationService.BuildLoadFailureDatasetStatus());
                AppendLog(imageLoadPresentationService.BuildLoadFailureLog(ex.Message));
                return false;
            }
            finally
            {
                imageMat?.Dispose();
            }
        }

        public WpfImageDecodeCacheDiagnostics GetImageDecodeCacheDiagnostics()
            => imageDecodeCacheService.GetDiagnostics();

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
            lastImageLoadDiagnostics = WpfImageLoadDiagnosticsService.Create(
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


        private void PreloadAdjacentQueueImages(string imagePath)
        {
            // Adjacent preload is only useful after the interactive shell is loaded; headless construction tests should not open extra image files.
            if (!IsLoaded || string.IsNullOrWhiteSpace(imagePath) || imageQueueItems.Count == 0)
            {
                return;
            }

            imageDecodePreloadService.StartAdjacentPreload(
                imagePath,
                imageQueueItems.Select(item => item.ImagePath),
                imageDecodeCacheService,
                File.Exists,
                imageDecodeService.TryDecodeForCache);
        }

        // Compatibility wrappers stay thin; command wiring above must call Execute* methods directly.
        private void Window_Loaded(object sender, RoutedEventArgs e) => ExecuteLoadedCommand();

        private void Window_Closed(object sender, EventArgs e) => ExecuteClosedCommand();

        private void YoloFixClassesButton_Click(object sender, RoutedEventArgs e) => ExecuteFixYoloClassesCommand();

        private void YoloFixLabelsButton_Click(object sender, RoutedEventArgs e) => ExecuteFixYoloLabelsCommand();

        private void YoloFixDatasetButton_Click(object sender, RoutedEventArgs e) => ExecuteFixYoloDatasetCommand();

        private void TutorialOpenHtmlGuideButton_Click(object sender, RoutedEventArgs e) => ExecuteOpenTutorialHtmlGuideCommand();

        private void AddClassButton_Click(object sender, RoutedEventArgs e) => ExecuteAddClassCommand();

        private void RemoveClassButton_Click(object sender, RoutedEventArgs e) => ExecuteRemoveClassCommand();

        private void BrowseOutputRootButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseOutputRootCommand();

        private void SaveOutputRootButton_Click(object sender, RoutedEventArgs e) => ExecuteSaveOutputRootCommand();

        private void InstallRequirementsButton_Click(object sender, RoutedEventArgs e) => ExecuteInstallRequirementsCommand();

        private void RunYoloSmokeButton_Click(object sender, RoutedEventArgs e) => ExecuteRunYoloSmokeCommand();

        private void RestartPythonWorkerButton_Click(object sender, RoutedEventArgs e) => ExecuteRestartPythonWorkerCommand();

        private void StopPythonWorkerButton_Click(object sender, RoutedEventArgs e) => ExecuteStopPythonWorkerCommand();

        private void BrowseYoloPythonButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseYoloPythonCommand();

        private void BrowseYoloProjectRootButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseYoloProjectRootCommand();

        private void BrowseYoloClientScriptButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseYoloClientScriptCommand();

        private void BrowseYoloWeightsButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseYoloWeightsCommand();

        private void BrowseYoloImageRootButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseYoloImageRootCommand();

        private void SaveYoloSettingsButton_Click(object sender, RoutedEventArgs e) => ExecuteSaveYoloSettingsCommand();

        private void ResetYoloSettingsButton_Click(object sender, RoutedEventArgs e) => ExecuteResetYoloSettingsCommand();

        private void RefreshTrainingReadinessButton_Click(object sender, RoutedEventArgs e) => ExecuteRefreshTrainingReadinessCommand();

        private void StartTrainingButton_Click(object sender, RoutedEventArgs e) => ExecuteStartTrainingCommand();

        private void StopTrainingButton_Click(object sender, RoutedEventArgs e) => ExecuteStopTrainingCommand();

        private void SaveProjectConfigButton_Click(object sender, RoutedEventArgs e) => ExecuteSaveProjectConfigCommand();

        private void ApplyProjectRecipeButton_Click(object sender, RoutedEventArgs e) => ExecuteApplyProjectRecipeCommand();

        private void RefreshProjectRecipeListButton_Click(object sender, RoutedEventArgs e) => ExecuteRefreshProjectRecipeListCommand();

        private void OpenProjectConfigFolderButton_Click(object sender, RoutedEventArgs e) => ExecuteOpenProjectConfigFolderCommand();

        private void ExecuteLoadedCommand()
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

        private void ExecuteClosedCommand()
        {
            StopInferenceStatusPulse();
            inferenceStatusPulseTimer.Tick -= InferenceStatusPulseTimer_Tick;
            StopTrainingStatusPolling();
            trainingStatusPollTimer.Tick -= TrainingStatusPollTimer_Tick;
            imageDecodePreloadService.CancelAndWait(TimeSpan.FromSeconds(2));
            CancelImageQueueDetailRefresh(waitForCompletion: true);
            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = null;
            imageDecodeCacheService.Clear();
            activeImageBitmap?.Dispose();
            activeImageBitmap = null;
        }


        private void CancelImageQueueDetailRefresh(bool waitForCompletion)
        {
            CancellationTokenSource cts = imageQueueDetailLoadCts;
            Task detailTask = imageQueueDetailLoadTask;
            if (cts == null)
            {
                return;
            }

            cts.Cancel();
            if (waitForCompletion)
            {
                WaitForImageQueueDetailRefresh(detailTask);
            }

            if (detailTask == null || detailTask.IsCompleted)
            {
                cts.Dispose();
            }
            else
            {
                detailTask.ContinueWith(_ => cts.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            if (ReferenceEquals(cts, imageQueueDetailLoadCts))
            {
                imageQueueDetailLoadCts = null;
            }

            if (ReferenceEquals(detailTask, imageQueueDetailLoadTask))
            {
                imageQueueDetailLoadTask = Task.CompletedTask;
            }
        }

        private void WaitForImageQueueDetailRefresh(Task detailTask)
        {
            if (detailTask == null || detailTask.IsCompleted)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    detailTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                }

                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!detailTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                // Detail refresh resumes on the UI dispatcher; pump briefly so close can release image file handles.
                var frame = new DispatcherFrame();
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            if (detailTask.IsFaulted)
            {
                _ = detailTask.Exception;
            }
        }
        private void ExecuteLoadSampleCommand()
        {
            TryLoadStartupSampleImage();
        }

        private void ExecuteAddSampleRoiCommand()
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

        private void ExecuteSaveAnnotationsCommand()
        {
            if (SaveCurrentAnnotations(out int savedCount))
            {
                MarkActiveImageConfirmed();
                AppendLog($"YOLO 라벨 저장. 객체:{savedCount}  {BuildLabelPathSummary()}");
                return;
            }

            AppendLog("저장할 ROI 또는 확정 후보가 없습니다.");
        }


        private void ExecuteFixYoloClassesCommand()
        {
            ExecuteYoloTrainingWorkflowStep(2, LearningWorkflowPanelControl);
        }

        private void ExecuteFixYoloLabelsCommand()
        {
            ExecuteYoloTrainingWorkflowStep(3, LearningWorkflowPanelControl);
        }

        private void ExecuteFixYoloDatasetCommand()
        {
            ExecuteYoloTrainingWorkflowStep(4, LearningWorkflowPanelControl);
        }

        private void ExecuteOpenTutorialHtmlGuideCommand()
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
                    ExecuteBrowseImageFolderCommand();
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

        private void LearningWorkflowModeListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningMode? mode = (selectedItem as WpfLearningModeItem)?.Mode ?? LearningWorkflowViewModel?.SelectedMode?.Mode;
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

        private void AnnotationToolListBox_SelectionChanged(object sender, object selectedItem)
        {
            ApplyAnnotationToolSelection((selectedItem as WpfAnnotationToolItem) ?? LearningWorkflowViewModel?.SelectedTool);
        }

        private void ExecuteCanvasAnnotationToolSelectionChanged(object selectedItem)
        {
            ApplyAnnotationToolSelection((selectedItem as WpfAnnotationToolItem) ?? CanvasPanelViewModel?.SelectedAnnotationTool);
        }

        private void ApplyAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return;
            }

            if (applyingAnnotationToolSelection)
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                return;
            }

            applyingAnnotationToolSelection = true;
            try
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                WpfAnnotationTool tool = selectedToolItem.Tool;
                WpfAnnotationToolCapability capability = WpfAnnotationToolCapabilityService.Get(tool);
                if (!capability.IsConnected)
                {
                    activeAnnotationTool = WpfAnnotationTool.Select;
                    EndPolygonAnnotationMode(clearDraft: true);
                    EndMaskAnnotationMode();
                    SetPendingAnnotationToolStatus(capability);
                    return;
                }

                activeAnnotationTool = tool;
                if (tool != WpfAnnotationTool.Polygon)
                {
                    EndPolygonAnnotationMode(clearDraft: true);
                }

                if (tool != WpfAnnotationTool.Brush && tool != WpfAnnotationTool.Eraser)
                {
                    EndMaskAnnotationMode();
                }

                if (tool == WpfAnnotationTool.Polygon)
                {
                    BeginPolygonAnnotationMode();
                    return;
                }

                if (tool == WpfAnnotationTool.Brush || tool == WpfAnnotationTool.Eraser)
                {
                    BeginMaskAnnotationMode(tool);
                    return;
                }

                switch (tool)
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
                        ExecutePanCanvasCommand();
                        break;

                    case WpfAnnotationTool.Delete:
                        SetWorkflowMode(WorkflowMode.Labeling);
                        ExecuteDeleteObjectCommand();
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
                        MainCanvasViewModel.IsImagePointInputMode = ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true;
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
            finally
            {
                applyingAnnotationToolSelection = false;
            }
        }

        private void SynchronizeAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return;
            }

            // The guide palette and canvas toolbar display the same tool source; synchronize selection without reapplying command tools.
            if (!ReferenceEquals(LearningWorkflowViewModel?.SelectedTool, selectedToolItem))
            {
                LearningWorkflowViewModel.SelectedTool = selectedToolItem;
            }

            CanvasPanelViewModel?.SetSelectedAnnotationTool(selectedToolItem);
            RefreshCanvasWorkflowContext();
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

        private void LearningStepListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningStep? step = (selectedItem as WpfLearningStepItem)?.Step ?? LearningWorkflowViewModel?.SelectedStep?.Step;
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
                    ExecuteAddSampleRoiCommand();
                    break;

                case WpfLearningStep.Infer:
                    SetWorkflowMode(WorkflowMode.Inference);
                    break;

                case WpfLearningStep.Review:
                    CandidatesReviewTab.IsSelected = true;
                    break;

                case WpfLearningStep.Save:
                    ExecuteSaveAnnotationsCommand();
                    break;
            }

            RefreshCanvasWorkflowContext();
        }

        private void SelectAnnotationTool(WpfAnnotationTool tool, bool revealInGuide = false)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            WpfAnnotationToolItem selectedTool = LearningWorkflowViewModel.AnnotationTools
                .FirstOrDefault(item => item.Tool == tool)
                ?? LearningWorkflowViewModel.SelectedTool;
            SynchronizeAnnotationToolSelection(selectedTool);

            if (revealInGuide)
            {
                LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
            }
        }

        private void ExecuteFitCanvasCommand()
        {
            MainCanvasViewModel.ImageViewer.ZoomToFit();
        }

        private void FitCanvasButton_Click(object sender, RoutedEventArgs e) => ExecuteFitCanvasCommand();

        private void ExecuteActualSizeCanvasCommand()
        {
            MainCanvasViewModel.ImageViewer.ZoomToActualSize();
        }

        private void ActualSizeCanvasButton_Click(object sender, RoutedEventArgs e) => ExecuteActualSizeCanvasCommand();

        private void ExecutePanCanvasCommand()
        {
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);
            AppendLog("캔버스 이동 모드");
        }

        private void PanCanvasButton_Click(object sender, RoutedEventArgs e) => ExecutePanCanvasCommand();

        private void ExecuteFocusCandidateCommand()
        {
            FocusSelectedCandidateInViewer(logIfMissing: true);
        }

        private void FocusCandidateButton_Click(object sender, RoutedEventArgs e) => ExecuteFocusCandidateCommand();

        private void ExecuteResetAiOverlayCommand()
        {
            int removedCount = candidateReviewState.ClearPendingCandidates();
            RefreshCandidateList();
            RedrawReviewRois();
            UpdateDetectionResultOverlay();
            SetPythonStatus("Python: AI 후보 표시 지움");
            AppendLog($"AI 후보 표시 지움: {removedCount}개");
        }

        private void ResetAiOverlayCanvasButton_Click(object sender, RoutedEventArgs e) => ExecuteResetAiOverlayCommand();

        private void ExecuteLabelingModeCommand()
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            FocusAnnotationToolsTab();
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true)
            {
                if (!MainCanvasViewModel.IsTeachingMode)
                {
                    MainCanvasViewModel.TeachingCommand.Execute(null);
                }
            }

            AppendLog("라벨링 모드로 전환했습니다. 이미지 선택만으로 추론하지 않습니다.");
        }

        private void ExecuteInferenceModeCommand()
        {
            SetWorkflowMode(WorkflowMode.Inference);
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true && MainCanvasViewModel.IsTeachingMode)
            {
                MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            AppendLog("추론 검토 모드로 전환했습니다. 현재 추론 또는 큐 검사 버튼으로 YOLO를 실행하세요.");
        }

        private async void ExecuteCheckYoloCommand()
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

        private async void ExecuteDetectCurrentImageCommand()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunInteractiveDetectionAsync(allowSmokeFallback: false).ConfigureAwait(true);
        }

        private async void ExecuteInstallRequirementsCommand()
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

        private async void ExecuteRunYoloSmokeCommand()
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

        private async void ExecuteRestartPythonWorkerCommand()
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

        private async void ExecuteStopPythonWorkerCommand()
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

        private async void ExecuteDetectSelectedQueueCommand()
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

        private async void ExecuteBatchDetectQueueCommand()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunBatchDetectionAsync(GetVisibleQueueItems(), "표시 행").ConfigureAwait(true);
        }

        private async void ExecuteRetryFailedQueueCommand()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunBatchDetectionAsync(
                imageQueueItems.Where(item => item.ReviewState == YoloImageReviewState.Failed).ToList(),
                "실패 재시도").ConfigureAwait(true);
        }

        private void ExecuteStopBatchQueueCommand()
        {
            batchDetectionCts?.Cancel();
            AppendLog("일괄 검사 중지를 요청했습니다.");
        }

        private void ExecuteCandidateConfidenceChangedCommand(double confidence)
        {
            UpdateCandidateConfidenceText();
            if (CandidateListBox == null)
            {
                return;
            }

            RefreshCandidateList();
        }

        private void ExecuteCandidatePreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                ExecuteConfirmSelectedCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                ExecuteSkipSelectedCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A && (e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ExecuteConfirmAllCandidatesCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                ExecuteNextCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                ExecutePreviousCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                FocusSelectedCandidateInViewer(logIfMissing: true);
                e.Handled = true;
            }
        }

        private void ExecutePreviousCandidateCommand()
        {
            SelectCandidateOffset(-1);
        }

        private void PreviousCandidateButton_Click(object sender, RoutedEventArgs e)
            => ExecutePreviousCandidateCommand();

        private void ExecuteNextCandidateCommand()
        {
            SelectCandidateOffset(1);
        }

        private void NextCandidateButton_Click(object sender, RoutedEventArgs e)
            => ExecuteNextCandidateCommand();

        private void SelectCandidateOffset(int offset)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            WpfCandidateNavigationSelection selection = WpfCandidateReviewSelectionService.SelectCandidateOffset(
                CandidateReviewViewModel.Candidates,
                CandidateReviewViewModel.SelectedCandidate,
                offset);
            if (selection.Status == WpfCandidateNavigationStatus.NoCandidates)
            {
                AppendLog("이동할 AI 후보가 없습니다.");
                return;
            }

            if (selection.Status == WpfCandidateNavigationStatus.SingleCandidate)
            {
                AppendLog("이동할 다른 AI 후보가 없습니다.");
                return;
            }

            CandidateReviewViewModel.SelectedCandidate = selection.SelectedItem;
            CandidateListBox?.ScrollIntoView(selection.SelectedItem);
            CandidateListBox?.Focus();
            FocusSelectedCandidateInViewer(logIfMissing: false);
        }

        private YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
            YoloWorkerSmokeCandidate current,
            IEnumerable<YoloWorkerSmokeCandidate> removingCandidates)
        {
            return WpfCandidateReviewSelectionService.FindNextVisibleCandidateAfter(
                GetVisibleCandidateList(),
                current,
                removingCandidates);
        }

        private void ExecuteConfirmSelectedCandidateCommand()
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("먼저 AI 후보를 선택하세요.");
                return;
            }

            ConfirmCandidates(new[] { candidate }, "선택");
        }

        private void ConfirmSelectedCandidateButton_Click(object sender, RoutedEventArgs e)
            => ExecuteConfirmSelectedCandidateCommand();

        private void ExecuteConfirmAllCandidatesCommand()
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates = GetVisibleCandidateList();
            if (candidates.Count == 0)
            {
                AppendLog("확정할 표시 AI 후보가 없습니다.");
                return;
            }

            ConfirmCandidates(candidates, "표시 후보 전체");
        }

        private void ConfirmAllCandidatesButton_Click(object sender, RoutedEventArgs e)
            => ExecuteConfirmAllCandidatesCommand();

        private void ExecuteSkipSelectedCandidateCommand()
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("스킵할 AI 후보를 선택하세요.");
                return;
            }

            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(candidate, new[] { candidate });
            RegisterAnnotationHistoryBeforeChange("Skip AI candidate", markDirty: false);
            candidateReviewState.SkipCandidate(candidate);
            RefreshCandidateListWithPreferred(nextCandidate);
            RedrawReviewRois();
            FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            MarkActiveImageSkippedOrCandidate();
            AddCandidateReviewHistory($"스킵: {FormatCandidate(candidate)}");
            SetPythonStatus($"Python: 대기 {pendingDetectionCandidates.Count} / 확정 {confirmedDetectionCandidates.Count}");
            AppendLog($"후보 스킵: {FormatCandidate(candidate)}");
        }

        private void SkipSelectedCandidateButton_Click(object sender, RoutedEventArgs e)
            => ExecuteSkipSelectedCandidateCommand();

        private void ExecuteCandidateSelectionChangedCommand(object selectedItem)
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

        private void ClassNameBox_KeyDown(object sender, KeyInputCommandArgs e)
        {
            if (e?.Key == Key.Enter)
            {
                ExecuteAddClassCommand();
                e.Handled = true;
            }
        }

        private void ExecuteAddClassCommand()
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

        private void ExecuteRemoveClassCommand()
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

        private void ClassListBox_SelectionChanged(object sender, object selectedItem)
        {
            string className = (selectedItem as WpfClassCatalogListItem)?.Text ?? GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            if (ClassCatalogViewModel != null)
            {
                ClassCatalogViewModel.ClassName = className;
            }
        }

        private void ExecuteBrowseOutputRootCommand()
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

        private void ExecuteSaveOutputRootCommand()
        {
            SaveOutputRootFromEditor();
        }

        private void ExecuteBrowseYoloPythonCommand()
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

        private void ExecuteBrowseYoloProjectRootCommand()
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

        private void ExecuteBrowseYoloClientScriptCommand()
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

        private void ExecuteBrowseYoloWeightsCommand()
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

        private void ExecuteBrowseYoloImageRootCommand()
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
        private async void ExecuteSaveYoloSettingsCommand()
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

        private async void ExecuteResetYoloSettingsCommand()
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel = new PythonModelSettings();
            global.Data.ProjectSettings.PythonModel.EnsureDefaults();
            PopulateYoloEditorFields();
            RefreshYoloStatus();
            await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
            AppendLog("YOLO 모델 설정을 기본값으로 되돌렸습니다.");
        }

        private void ExecuteRefreshTrainingReadinessCommand()
        {
            SaveTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: true);
        }

        private async void ExecuteStartTrainingCommand()
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

        private async void ExecuteStopTrainingCommand()
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

        // Keep legacy WPF event handlers thin so tests/XAML compatibility do not reintroduce event-object command paths.
        private void LoadImageRootButton_Click(object sender, RoutedEventArgs e) => ExecuteLoadImageRootQueueCommand();

        private void BrowseImageFolderButton_Click(object sender, RoutedEventArgs e) => ExecuteBrowseImageFolderCommand();

        private void RefreshImageQueueButton_Click(object sender, RoutedEventArgs e) => ExecuteRefreshImageQueueCommand();

        private void NextUnlabeledButton_Click(object sender, RoutedEventArgs e) => ExecuteNextUnlabeledQueueCommand();

        private void OpenSelectedQueueImageButton_Click(object sender, RoutedEventArgs e) => ExecuteOpenSelectedQueueImageCommand();

        private void DetectSelectedQueueButton_Click(object sender, RoutedEventArgs e) => ExecuteDetectSelectedQueueCommand();

        private void BatchDetectQueueButton_Click(object sender, RoutedEventArgs e) => ExecuteBatchDetectQueueCommand();

        private void RetryFailedQueueButton_Click(object sender, RoutedEventArgs e) => ExecuteRetryFailedQueueCommand();

        private void StopBatchQueueButton_Click(object sender, RoutedEventArgs e) => ExecuteStopBatchQueueCommand();

        private void QueueFilterAllButton_Click(object sender, RoutedEventArgs e) => ExecuteQueueFilterAllCommand();

        private void QueueFilterCandidateButton_Click(object sender, RoutedEventArgs e) => ExecuteQueueFilterCandidateCommand();

        private void QueueFilterFailedButton_Click(object sender, RoutedEventArgs e) => ExecuteQueueFilterFailedCommand();

        private void QueueFilterConfirmedButton_Click(object sender, RoutedEventArgs e) => ExecuteQueueFilterConfirmedCommand();

        private void QueueFilterSkippedButton_Click(object sender, RoutedEventArgs e) => ExecuteQueueFilterSkippedCommand();

        private void QueueFilterNoCandidateButton_Click(object sender, RoutedEventArgs e) => ExecuteQueueFilterNoCandidateCommand();

        private void ExecuteLoadImageRootQueueCommand()
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

        private void ExecuteBrowseImageFolderCommand()
        {
            string currentRoot = Directory.Exists(currentImageRoot) ? currentImageRoot : string.Empty;
            if (!TryPickFolder("이미지 폴더 선택", currentRoot, out string selectedPath))
            {
                return;
            }

            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel.ImageRootPath = selectedPath;
            LoadImageQueueFromRoot(selectedPath, string.Empty, loadFirstImage: true);
        }

        private void ExecuteRefreshImageQueueCommand()
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

        private void ExecuteNextUnlabeledQueueCommand()
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

        private void ImageQueueFilterBox_SelectionChanged(object sender, object selectedItem)
        {
            imageQueueView?.Refresh();
            UpdateQueueQuickFilterButtons();
            UpdateImageQueueStatusText();
        }

        private void ExecuteQueueFilterAllCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.All);
        }

        private void ExecuteQueueFilterCandidateCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Candidate);
        }

        private void ExecuteQueueFilterFailedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Failed);
        }

        private void ExecuteQueueFilterConfirmedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Confirmed);
        }

        private void ExecuteQueueFilterSkippedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Skipped);
        }

        private void ExecuteQueueFilterNoCandidateCommand()
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

        private void ImageQueueSearchBox_TextChanged(object sender, string searchText)
        {
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void ImageQueueGrid_SelectionChanged(object sender, object selectedItem)
        {
            ExecuteSelectedQueueItemChanged(selectedItem as WpfImageQueueItem);
        }

        private void ExecuteSelectedQueueItemChanged(WpfImageQueueItem item)
        {
            WpfImageQueueItem selectedItem = imageQueueSelectionService.ResolveSelectedItem(item, imageQueueItems, activeImagePath);
            if (suppressImageQueueSelection)
            {
                UpdateSelectedQueueImageButton(selectedItem);
                return;
            }

            if (selectedItem == null)
            {
                UpdateSelectedQueueImageButton(null);
                return;
            }

            UpdateSelectedQueueImageButton(selectedItem);
            if (ReferenceEquals(selectedItem, ImageQueueGrid?.SelectedItem))
            {
                TryOpenSelectedQueueImage(skipIfAlreadyActive: true);
            }
            else
            {
                TryOpenSelectedQueueImage(selectedItem, skipIfAlreadyActive: true);
            }
        }

        private void ImageQueueGrid_MouseDoubleClick(object sender)
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private void ExecuteOpenSelectedQueueImageCommand()
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private bool TryOpenSelectedQueueImage(bool skipIfAlreadyActive = false)
        {
            return TryOpenSelectedQueueImage(ImageQueueGrid.SelectedItem as WpfImageQueueItem, skipIfAlreadyActive);
        }

        private bool TryOpenSelectedQueueImage(WpfImageQueueItem item, bool skipIfAlreadyActive = false)
        {
            if (!imageQueueSelectionService.CanOpen(item))
            {
                AppendLog("\uC5F4 \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return false;
            }

            UpdateSelectedQueueImageButton(item);

            if (!imageQueueSelectionService.ShouldOpen(item, activeImagePath, skipIfAlreadyActive))
            {
                return true;
            }

            bool loaded = TryLoadImage(
                item.ImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
            if (loaded)
            {
                UpdateSelectedQueueImageButton(item);
            }

            return loaded;
        }

        private void UpdateSelectedQueueImageButton(WpfImageQueueItem item)
        {
            bool canOpenSelectedImage = imageQueueSelectionService.CanOpen(item);

            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.SetSelectedImageAvailability(canOpenSelectedImage);
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
                string targetImagePath = detectionTargetService.ResolveInteractiveTargetPath(
                    imagePath,
                    activeImagePath,
                    global.Data.ProjectSettings.PythonModel);
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

            IReadOnlyList<WpfImageQueueItem> queue = detectionTargetService.BuildBatchQueue(items);
            if (queue.Count == 0)
            {
                AppendLog(detectionTargetService.BuildEmptyBatchMessage(scopeText));
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
            SetYoloCommandStatus(batchDetectionProgressService.BuildStartCommandStatus(queue.Count), isBusy: true);
            SetGlobalInferenceStatus(batchDetectionProgressService.BuildStartInferenceStatus(queue.Count), isBusy: true);

            AppendLog(batchDetectionProgressService.BuildStartLog(scopeText, queue.Count));
            var batchStopwatch = Stopwatch.StartNew();
            int pendingReviewStatusSaves = 0;
            bool batchFailed = false;
            string batchFailureSummary = string.Empty;
            try
            {
                SetGlobalInferenceStatus(batchDetectionProgressService.BuildWorkerPreparingInferenceStatus(queue.Count), isBusy: true);
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
                    string currentFileName = batchDetectionProgressService.ResolveImageFileName(item.ImagePath);
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildItemInferenceStatus(batchDetectionCompletedCount, batchDetectionTotalCount, item.ImagePath), isBusy: true);
                    UpdateBatchDetectionControls(scopeText, currentFileName);

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
                        AppendLog(batchDetectionProgressService.BuildItemCompletedLog(nextCompleted, batchDetectionTotalCount, item.ImagePath, result.CandidateCount, elapsedText));
                    }
                    else if (!token.IsCancellationRequested)
                    {
                        AppendLog(batchDetectionProgressService.BuildItemFailedLog(nextCompleted, batchDetectionTotalCount, item.ImagePath, elapsedText, result.Summary));
                    }
                    pendingReviewStatusSaves++;
                    if (pendingReviewStatusSaves >= BatchReviewStatusSaveInterval)
                    {
                        imageReviewStatus.SaveReviewStatus(global.Data);
                        pendingReviewStatusSaves = 0;
                    }

                    batchDetectionCompletedCount++;
                    SetPythonStatus(batchDetectionProgressService.BuildItemPythonStatus(batchDetectionCompletedCount, batchDetectionTotalCount, elapsedText));
                    UpdateBatchDetectionControls(scopeText, batchDetectionProgressService.BuildLatestFileStatus(item.ImagePath, elapsedText));
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
                SetYoloCommandStatus(batchDetectionProgressService.BuildCompletionCommandStatus(canceled, batchDetectionCompletedCount, batchDetectionTotalCount, totalElapsedText), isBusy: false);
                SetGlobalInferenceStatus(
                    batchDetectionProgressService.BuildCompletionInferenceStatus(canceled, batchDetectionCompletedCount, batchDetectionTotalCount, totalElapsedText),
                    isBusy: false,
                    isWarning: canceled);
                AppendLog(batchDetectionProgressService.BuildCompletionLog(canceled, batchDetectionCompletedCount, batchDetectionTotalCount, totalElapsedText, averageElapsedText));
                if (batchFailed)
                {
                    UpdateBatchDetectionControls("실패", string.Empty);
                    SetPythonStatus("Python: 일괄 검사 실패");
                    SetYoloCommandStatus(batchDetectionProgressService.BuildFailureCommandStatus(batchDetectionCompletedCount, batchDetectionTotalCount, batchFailureSummary), isBusy: false);
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildFailureInferenceStatus(batchDetectionCompletedCount, batchDetectionTotalCount), isBusy: false, isWarning: true);
                    AppendLog(batchDetectionProgressService.BuildFailureLog(batchDetectionCompletedCount, batchDetectionTotalCount, batchFailureSummary));
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
            UpdateYoloCommandButtons();
            WpfBatchDetectionControlState controlState = batchDetectionProgressService.BuildControlState(
                isBatchDetectionRunning,
                batchDetectionTotalCount,
                batchDetectionCompletedCount,
                scopeText,
                currentFileName);

            BatchProgressBar.Maximum = controlState.ProgressMaximum;
            BatchProgressBar.Value = controlState.ProgressValue;
            BatchStatusText.Text = controlState.StatusText;

            if (controlState.ShouldRefreshQueueStatus)
            {
                UpdateImageQueueStatusText();
            }
            else
            {
                SetDatasetStatus(controlState.DatasetStatusText);
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
            bool loaded = TryLoadImage(
                item.ImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
            if (loaded)
            {
                UpdateSelectedQueueImageButton(item);
            }

            return loaded;
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
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = detectionResultPresentationService.BuildNoCandidateOverlay(
                item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty,
                GetCandidateConfidenceFilter());
            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = detectionResultPresentationService.BuildFailureOverlay(
                item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty,
                result?.Summary);
            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = candidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();

            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(loadedCount, succeeded);
            AddCandidateReviewHistory(detectionResultPresentationService.BuildCandidateLoadHistory(loadedCount, succeeded, GetCandidateConfidenceFilter()));
            if (candidateReviewState.HasPendingCandidates)
            {
                CandidatesReviewTab.IsSelected = true;
            }

            CenterCanvasAfterInferenceResult();
        }
        private void ApplyDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = candidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();

            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(loadedCount, succeeded);
            AddCandidateReviewHistory(detectionResultPresentationService.BuildCandidateLoadHistory(loadedCount, succeeded, GetCandidateConfidenceFilter()));

            if (!candidateReviewState.HasPendingCandidates)
            {
                CenterCanvasAfterInferenceResult();
                AppendLog("AI 검출 후보가 없습니다.");
                return;
            }

            CandidatesReviewTab.IsSelected = true;
            CenterCanvasAfterInferenceResult();
            AppendLog($"AI 후보 로드: {loadedCount}개");
        }
        private void AddCandidateReviewHistory(string message)
        {
            CandidateReviewViewModel?.AddReviewHistory(message);
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

            WpfCandidateConfirmationAttempt attempt = candidateConfirmationService.Prepare(
                candidateReviewState,
                candidates,
                IsCandidateConfirmable,
                IsCandidateHighOverlap);
            if (!attempt.CanConfirm)
            {
                AddCandidateReviewHistory(attempt.ReviewHistoryMessage);
                AppendLog(attempt.LogMessage);
                return;
            }

            WpfCandidateConfirmationPlan plan = attempt.Plan;
            YoloWorkerSmokeCandidate selectedBeforeConfirm = GetSelectedCandidate();
            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(selectedBeforeConfirm, plan.ConfirmableCandidates);
            RegisterAnnotationHistoryBeforeChange($"Confirm {scope}");
            candidateConfirmationService.ApplyConfirmation(candidateReviewState, plan);

            bool saved = SaveCurrentAnnotations(out int savedCount);
            WpfCandidateConfirmationResult result = candidateConfirmationService.BuildConfirmedResult(
                scope,
                plan,
                saved,
                savedCount,
                BuildLabelPathSummary());
            AddCandidateReviewHistory(result.ReviewHistoryMessage);
            RefreshCandidateListWithPreferred(nextCandidate);
            RefreshObjectList();
            RedrawReviewRois();
            PopulateClassList();
            if (saved)
            {
                MarkActiveImageConfirmed();
            }
            if (candidateReviewState.HasPendingCandidates)
            {
                CandidatesReviewTab.IsSelected = true;
                FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            }
            else
            {
                ObjectsReviewTab.IsSelected = true;
            }
            SetPythonStatus($"Python: 대기 {candidateReviewState.PendingCount} / 확정 {candidateReviewState.ConfirmedCount}");

            AppendLog(result.LogMessage);
            if (!string.IsNullOrWhiteSpace(result.DuplicateLogMessage))
            {
                AppendLog(result.DuplicateLogMessage);
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
            savedCount = CountAnnotationRois(roisByClass) + CountAnnotationSegments(segmentsByClass);
            if (savedCount == 0)
            {
                return false;
            }

            bool saved = LabelingAnnotationPersistence.SaveCurrent(activeImageBitmap, roisByClass, segmentsByClass, global.Data);
            if (saved)
            {
                MarkAnnotationsSaved($"라벨 저장 완료: 객체 {savedCount}개");
                global.System?.UpdateData();
            }

            return saved;
        }

        private static int CountAnnotationRois(IReadOnlyDictionary<string, List<CRectangleObject>> roisByClass)
        {
            return roisByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(roi => roi != null && !roi.Roi.IsEmpty) ?? 0;
        }

        private static int CountAnnotationSegments(IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass)
        {
            return segmentsByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(segment => segment != null && ((segment.Points != null && segment.Points.Count >= 3) || (segment.IsRasterMask && !segment.Bounds.IsEmpty))) ?? 0;
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
            => WpfCandidateReviewSelectionService.GetSelectedCandidate(CandidateReviewViewModel?.SelectedCandidate);

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
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = candidateReviewPresentationService.BuildOverlayPresentation(
                activeImagePath,
                pendingDetectionCandidates,
                GetSelectedCandidate(),
                GetCandidateConfidenceFilter(),
                IsCandidateHighOverlap,
                IsCandidateConfirmable,
                BuildCandidateSecondaryText);
            if (presentation.IsEmpty)
            {
                CanvasPanelViewModel.ClearDetectionOverlay();
                return;
            }

            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
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
            WpfCandidateReviewListPresentation presentation = candidateReviewPresentationService.BuildListPresentation(
                pendingDetectionCandidates,
                GetVisibleCandidateList(),
                preferredCandidate,
                GetCandidateConfidenceFilter(),
                GetMinimumDetectionConfidence(),
                GetClippedCandidateBounds,
                GetCandidateOverlapInfo);

            CandidateReviewViewModel.SetCandidates(
                presentation.Rows,
                presentation.Detail,
                presentation.PreferredCandidate);
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

            WpfObjectReviewListPresentation presentation = objectReviewPresentationService.BuildListPresentation(
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                manualSegments,
                confirmedDetectionCandidates,
                preferredSelection,
                previousSelection,
                GetClippedCandidateBounds,
                FormatCandidateDetail);

            SetObjectReviewObjects(presentation.Rows, presentation.Summary, presentation.SelectedItem);
            UpdateObjectReviewActionState();
        }

        private WpfObjectReviewListItem BuildManualRoiObjectReviewItem(int index)
            => objectReviewPresentationService.BuildManualRoiItem(
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                index);

        private bool TryRefreshManualRoiObjectReviewRow(int manualRoiIndex, bool select)
        {
            WpfObjectReviewListItem row = BuildManualRoiObjectReviewItem(manualRoiIndex);
            if (row == null
                || !WpfObjectReviewSelectionService.CanReplaceManualRoiRow(
                    ObjectReviewViewModel?.Objects,
                    manualRoiIndex,
                    manualRois.Count))
            {
                return false;
            }

            bool replaced;
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                replaced = ObjectReviewViewModel.TryReplaceObject(
                    manualRoiIndex,
                    row,
                    select);
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            return replaced;
        }

        private void SetObjectReviewObjects(
            IEnumerable<WpfObjectReviewListItem> rows,
            string summary,
            WpfObjectReviewItemRef selectedItem)
        {
            // Rebuilding the side list temporarily clears WPF SelectedItem. During ROI click/drag
            // that transient null must not clear the active canvas ROI handles.
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                ObjectReviewViewModel.SetObjects(
                    rows,
                    summary,
                    selectedItem?.Source.ToString() ?? string.Empty,
                    selectedItem?.Index ?? -1);
            }

            SyncObjectClassEditorToSelection();
        }

        private string GetManualRoiClassName(int index)
            => WpfObjectReviewPresentationService.GetManualRoiClassName(manualRoiClassNames, index);

        private WpfObjectReviewListItem BuildManualSegmentObjectReviewItem(int manualSegmentIndex)
            => objectReviewPresentationService.BuildManualSegmentItem(
                manualRois.Count,
                manualSegments,
                manualSegmentIndex);

        private bool TryRefreshManualSegmentObjectReviewRow(int manualSegmentIndex, string summary, bool select)
        {
            WpfObjectReviewListItem row = BuildManualSegmentObjectReviewItem(manualSegmentIndex);
            int objectRowIndex = manualRois.Count + manualSegmentIndex;
            if (row == null || ObjectReviewViewModel == null || objectRowIndex < 0)
            {
                return false;
            }

            bool updated;
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                updated = ObjectReviewViewModel.TryUpsertObject(
                    objectRowIndex,
                    row,
                    summary,
                    select);
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            return updated;
        }
        private void ExecuteObjectSelectionChangedCommand(object selectedItem)
        {
            if (ObjectReviewViewModel?.IsSelectionNotificationSuppressed == true)
            {
                UpdateObjectReviewActionState();
                return;
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            bool isManualSegmentSelected = ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true;
            if (activeAnnotationTool == WpfAnnotationTool.Select)
            {
                MainCanvasViewModel.IsImagePointInputMode = isManualSegmentSelected;
            }

            if (ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualRoi) != true)
            {
                MainCanvasViewModel.ClearRoiSelection();
            }

            RefreshPolygonOverlays();
        }

        private void ExecuteApplyObjectClassCommand()
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
                candidateReviewState.MutableConfirmedCandidates,
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

        private void ExecuteDeleteObjectCommand()
        {
            DeleteSelectedObject();
        }

        private void ExecuteObjectPreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null || (e.Key != Key.Delete && e.Key != Key.Back))
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
            WpfAnnotationHistorySnapshot beforeChange = item.Source == WpfObjectReviewSource.ManualRoi
                ? CaptureManualRoiHistory("Delete object")
                : CaptureAnnotationHistory("Delete object");
            if (!WpfObjectReviewEditService.TryDelete(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                candidateReviewState.MutableConfirmedCandidates))
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
            if (ObjectReviewViewModel == null)
            {
                item = null;
                return false;
            }

            return ObjectReviewViewModel.TryResolveSelectedItem(
                manualRoiOverlayIds,
                manualRois.Count,
                out item);
        }

        private int GetSelectedObjectReviewRowIndex()
            => ObjectReviewViewModel?.GetSelectedRowIndex() ?? -1;

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
                groupName,
                refreshImmediately: false);
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
            WpfObjectReviewDeleteRefreshPlan plan = objectReviewPresentationService.BuildDeleteRefreshPlan(
                deletedSource,
                objectCount,
                ObjectReviewFullRefreshDeleteLimit,
                deletedObjectRowIndex,
                ObjectReviewViewModel?.Objects?.Count ?? 0);
            if (!plan.UseIncremental)
            {
                RefreshObjectList();
                return;
            }

            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                if (!ObjectReviewViewModel.TryRemoveObject(
                    deletedObjectRowIndex,
                    plan.Summary,
                    plan.SelectedRowIndex))
                {
                    RefreshObjectList();
                    return;
                }
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

            if (CanvasPanelViewModel != null)
            {
                CanvasPanelViewModel.SetCommandAvailability(hasImage, hasSelectedCandidate, hasPendingCandidates);
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
            return candidateReviewState.GetVisibleCandidates(minimum);
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
            CancelImageQueueDetailRefresh(waitForCompletion: false);
            imageQueueDetailLoadCts = new CancellationTokenSource();
            imageQueueDetailLoadTask = Task.CompletedTask;

            List<string> imagePaths = imageQueueSelectionService.EnumerateImageFiles(imageRoot);
            imageReviewStatus.SetImages(imagePaths);
            imageReviewStatus.LoadReviewStatus(global.Data, imagePaths);

            suppressImageQueueSelection = true;
            try
            {
                imageQueueItems.Clear();
                foreach (WpfImageQueueItem item in imageQueueSelectionService.CreateShellItems(imagePaths))
                {
                    imageQueueItems.Add(item);
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
                imageQueueDetailLoadTask = StartImageQueueDetailRefreshAsync(imagePaths, imageQueueDetailLoadCts.Token);
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
                || !imageQueueSelectionService.IsSameRoot(imageRoot, currentImageRoot))
            {
                LoadImageQueueFromRoot(imageRoot, selectedImagePath, loadFirstImage: false, refreshDetails: refreshDetails);
                return;
            }

            SelectImageQueueItem(selectedImagePath);
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }


        private async Task StartImageQueueDetailRefreshAsync(IReadOnlyList<string> imagePaths, CancellationToken token)
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
            YoloImageReviewStatus status = RefreshActiveImageQueueStatusCore(
                activeImagePath,
                activeImageSize,
                global.Data,
                hasActiveCandidates);
            ApplyReviewStatusToItem(item, status);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            string imagePath = activeImagePath;
            DrawingSize imageSize = activeImageSize;
            CData data = global.Data;
            int refreshVersion = Interlocked.Increment(ref queuedActiveImageQueueStatusRefreshVersion);

            // Delete must feel immediate. Label-file recount and review-state JSON writes are
            // background bookkeeping; only the latest completed result returns to the UI thread.
            Task.Run(() => RefreshActiveImageQueueStatusCore(
                    imagePath,
                    imageSize,
                    data,
                    hasActiveCandidates))
                .ContinueWith(
                    task => ApplyQueuedActiveImageQueueStatusRefresh(refreshVersion, imagePath, task),
                    TaskScheduler.Default);
        }

        private YoloImageReviewStatus RefreshActiveImageQueueStatusCore(
            string imagePath,
            DrawingSize imageSize,
            CData data,
            bool hasActiveCandidates)
        {
            YoloImageReviewStatus status = imageReviewStatus.RefreshLabelStatusAndReviewState(
                imagePath,
                imageSize,
                data,
                hasActiveCandidates);
            imageReviewStatus.SaveReviewStatus(data);
            return status;
        }

        private void ApplyQueuedActiveImageQueueStatusRefresh(
            int refreshVersion,
            string imagePath,
            Task<YoloImageReviewStatus> refreshTask)
        {
            try
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (refreshVersion != Volatile.Read(ref queuedActiveImageQueueStatusRefreshVersion)
                            || !string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase)
                            || refreshTask.IsCanceled)
                        {
                            return;
                        }

                        if (refreshTask.IsFaulted)
                        {
                            AppendLog($"Image queue status refresh failed after delete: {refreshTask.Exception?.GetBaseException().Message}");
                            return;
                        }

                        ApplyReviewStatusToItem(FindImageQueueItem(imagePath), refreshTask.Result);
                        imageQueueView?.Refresh();
                        UpdateImageQueueStatusText();
                    }),
                    DispatcherPriority.Background);
            }
            catch (InvalidOperationException)
            {
                // The shell can close while a queued delete-status refresh is finishing.
            }
            catch (TaskCanceledException)
            {
            }
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
            return imageQueueSelectionService.FindItem(imageQueueItems, imagePath);
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
            ImageQueueViewModel.SetQuickFilterState(
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

        private void ExecuteSaveProjectConfigCommand()
        {
            SaveProjectConfigFromPanel();
        }

        private void ExecuteApplyProjectRecipeCommand()
        {
            ApplyProjectRecipeFromPanel();
        }

        private void ProjectRecipeListBox_SelectionChanged(object sender, object selectedItem)
        {
            if (suppressProjectRecipeSelection)
            {
                return;
            }

            string recipeName = selectedItem as string ?? ProjectConfigViewModel?.SelectedRecipeName ?? ProjectRecipeListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            ProjectConfigViewModel?.SelectRecipeFromList(recipeName);
        }

        private void ExecuteRefreshProjectRecipeListCommand()
        {
            string selectedRecipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? GetCurrentRecipeName();
            if (PopulateProjectRecipeList(selectedRecipeName))
            {
                SetProjectConfigStatus("Recipe 목록을 다시 읽었습니다. 적용할 항목을 선택하세요.");
            }
        }

        private void ExecuteOpenProjectConfigFolderCommand()
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
            => fileDialogService.TryPickFile(this, title, filter, currentPath, out selectedPath);

        private bool TryPickFolder(string title, string currentPath, out string selectedPath)
            => fileDialogService.TryPickFolder(this, title, currentPath, out selectedPath);

        private bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            if (!trainingWeightsService.TryFindLatestTrainingWeights(settings.ProjectRootPath, global.Data.OutputRootPath, out string latestWeightsPath))
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus("학습 결과 weight를 찾지 못했습니다. YOLO 탭에서 best.pt를 직접 선택하세요.", isBusy: false);
                    AppendLog("학습 결과 best.pt 후보를 찾지 못했습니다.");
                }

                return false;
            }

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

            if (!WpfTrainingWeightsService.ShouldPreferTrainingWeights(latestWeightsPath, currentWeightsPath))
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
            trainingGuideHistoryService.UpdateDatasetHistory(
                history,
                report?.IsReady == true,
                presentation?.IssueKind,
                presentation?.DetailText,
                recordHistory);
            UpdateYoloTrainingHistoryText();

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
            trainingGuideHistoryService.UpdateTrainingHistory(
                history,
                status,
                IsTerminalTrainingState,
                ref lastRecordedTrainingGuideRunSignature);
            UpdateYoloTrainingHistoryText();

            if (IsTerminalTrainingState(history.LastTrainingState) && !hasPendingTrainingWeightsRecipeSave)
            {
                TrySaveTrainingGuideHistoryQuietly();
            }
        }

        private void UpdateAppliedTrainingWeightsHistory(string weightsPath, bool savedToRecipe)
        {
            EnsureProjectSettings();
            trainingGuideHistoryService.UpdateAppliedWeightsHistory(
                global.Data.ProjectSettings.TrainingGuide,
                weightsPath,
                savedToRecipe);
            UpdateYoloTrainingHistoryText();
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
            LearningWorkflowViewModel.SetTrainingRunHistoryItems(
                trainingGuideHistoryService.BuildRunHistoryItems(history, FormatTrainingState));
            LearningWorkflowViewModel.TrainingHistoryText = trainingGuideHistoryService.BuildHistoryText(history, FormatTrainingState);
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
            bool trainingCompleted = WpfTrainingWeightsService.IsCompletedTrainingState(trainingState);
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
                if (WpfTrainingWeightsService.IsCompletedTrainingState(status.LastTrainingState))
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
            if (WpfTrainingWeightsService.IsCompletedTrainingState(state))
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
            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.ApplyWorkflowCommandState(state);
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

        private void ExecuteToggleThemeCommand()
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
