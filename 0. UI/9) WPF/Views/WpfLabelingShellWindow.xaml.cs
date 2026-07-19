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
        private const double PreferredInitialShellWidth = 1920D;
        private const double PreferredInitialShellHeight = 1080D;
        private static readonly string[] SharedThemeResourceKeys =
        {
            "AppBackgroundBrush",
            "FrameBrush",
            "PanelBrush",
            "PanelHeaderBrush",
            "CanvasBrush",
            "StatusBarBrush",
            "BorderBrushDark",
            "PrimaryTextBrush",
            "SecondaryTextBrush",
            "AccentBrush",
            "ModelCenterCandidateBrush",
            "ModelCenterDecisionBrush",
            "ModelCenterCandidatePanelBrush",
            "ModelCenterDecisionPanelBrush",
            "ToolbarButtonBrush",
            "ToolbarButtonBorderBrush",
            "ToolbarButtonHoverBrush",
            "ToolbarButtonPressedBrush",
            "ToolbarButtonDisabledBrush",
            "ToolbarButtonDisabledBorderBrush",
            "DisabledTextBrush",
            "InputBrush",
            "InputBorderBrush",
            "GridLineBrush",
            "GridHeaderBrush",
            "RowHoverBrush",
            "SelectedRowBrush",
            "SelectedRowTextBrush",
            "DetectionOverlayBackgroundBrush",
            "DetectionOverlayBorderBrush",
            "DetectionOverlayTitleTextBrush",
            "DetectionOverlaySummaryTextBrush",
            "DetectionOverlaySelectedBackgroundBrush",
            "DetectionOverlaySelectedTextBrush",
            "DetectionOverlayDetailTextBrush"
        };
        private readonly CGlobal global = CGlobal.Inst;
        private readonly WpfBulkObservableCollection<WpfImageQueueItem> imageQueueItems = new WpfBulkObservableCollection<WpfImageQueueItem>();
        private readonly Dictionary<string, WpfImageQueueItem> imageQueueItemsByPath = new Dictionary<string, WpfImageQueueItem>(StringComparer.OrdinalIgnoreCase);
        private YoloImageReviewStatusService imageReviewStatus = new YoloImageReviewStatusService();
        private AnomalyImageReviewStatusService anomalyImageReviewStatus = new AnomalyImageReviewStatusService();
        private string dismissedAnomalyFolderStateSuggestionRoot = string.Empty;
        private int queuedActiveImageQueueStatusRefreshVersion;
        private readonly WpfImageQueueSelectionService imageQueueSelectionService = new WpfImageQueueSelectionService();
        private readonly WpfDatasetImageRootResolver datasetImageRootResolver = new WpfDatasetImageRootResolver();
        private readonly WpfImageDecodeCacheService imageDecodeCacheService = new WpfImageDecodeCacheService();
        private readonly WpfImageDecodeService imageDecodeService = new WpfImageDecodeService();
        private readonly WpfImageDecodePreloadService imageDecodePreloadService = new WpfImageDecodePreloadService();
        private WpfImageLoadDiagnostics lastImageLoadDiagnostics = WpfImageLoadDiagnostics.Empty;
        private ICollectionView imageQueueView;
        private CancellationTokenSource imageQueueCatalogLoadCts;
        private Task imageQueueCatalogLoadTask = Task.CompletedTask;
        private int imageQueueCatalogLoadVersion;
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
        private readonly WpfCandidateReviewCompletionPresentationService candidateReviewCompletionPresentationService = new WpfCandidateReviewCompletionPresentationService();
        private readonly WpfDetectionResultPresentationService detectionResultPresentationService = new WpfDetectionResultPresentationService();
        private readonly WpfDetectionTargetService detectionTargetService = new WpfDetectionTargetService();
        private readonly WpfBatchDetectionProgressService batchDetectionProgressService = new WpfBatchDetectionProgressService();
        private readonly WpfImageLoadPresentationService imageLoadPresentationService = new WpfImageLoadPresentationService();
        private readonly WpfObjectReviewPresentationService objectReviewPresentationService = new WpfObjectReviewPresentationService();
        private readonly WpfFileDialogService fileDialogService = new WpfFileDialogService();
        private readonly WpfDatasetSetupPathService datasetSetupPathService = new WpfDatasetSetupPathService();
        private readonly WpfDatasetSetupDataService datasetSetupDataService = new WpfDatasetSetupDataService();
        private readonly WpfDatasetSetupPresentationService datasetSetupPresentationService = new WpfDatasetSetupPresentationService();
        private readonly WpfTrainingWeightsService trainingWeightsService = new WpfTrainingWeightsService();
        private readonly WpfModelComparisonReviewService modelComparisonReviewService = new WpfModelComparisonReviewService();
        private readonly WpfModelComparisonRunService modelComparisonRunService = new WpfModelComparisonRunService();
        private readonly WpfAnomalyClassificationEvaluationRunService anomalyClassificationEvaluationRunService = new WpfAnomalyClassificationEvaluationRunService();
        private readonly WpfWorkspaceLayoutSettingsService workspaceLayoutSettingsService = new WpfWorkspaceLayoutSettingsService();
        private readonly WpfTrainingGuideHistoryService trainingGuideHistoryService = new WpfTrainingGuideHistoryService();
        private readonly WpfMaskEditStateService maskEditStateService = new WpfMaskEditStateService();
        private readonly WpfMaskStrokeHistoryDraftService maskStrokeHistoryDraftService = new WpfMaskStrokeHistoryDraftService();
        private bool suppressImageQueueSelection;
        private bool isDetecting;
        private bool isBatchDetectionRunning;
        private bool isYoloEnvironmentCommandRunning;
        private bool isTrainingCommandRunning;
        private bool isTrainingWorkflowRunning;
        private bool isModelComparisonRunning;
        private bool isAnomalyEvaluationRunning;
        private bool suppressProjectRecipeSelection;
        private int batchDetectionTotalCount;
        private int batchDetectionCompletedCount;
        private readonly Stopwatch inferenceStatusPulseStopwatch = new Stopwatch();
        private readonly DispatcherTimer inferenceStatusPulseTimer;
        private readonly DispatcherTimer trainingStatusPollTimer;
        private readonly DispatcherTimer maskStrokePreviewCommitSwapTimer;
        private readonly DispatcherTimer maskStrokeCommitQueueTimer;
        private DateTime trainingStatusPollStartedUtc = DateTime.MinValue;
        private string lastAutoAppliedTrainingWeightsPath = string.Empty;
        private string pendingTrainingBaselineWeightsPath = string.Empty;
        private bool hasPendingTrainingWeightsRecipeSave;
        private YoloDatasetReadinessReport lastYoloTrainingReadinessReport;
        private string lastRecordedTrainingGuideRunSignature = string.Empty;
        private ShellTheme currentTheme = ShellTheme.Dark;
        private WorkflowMode currentWorkflowMode = WorkflowMode.Labeling;
        private WpfCanvasDisplayMode canvasDisplayMode = WpfCanvasDisplayMode.LabelsOnly;
        private WpfAnnotationTool activeAnnotationTool = WpfAnnotationTool.Select;
        private bool applyingAnnotationToolSelection;
        private System.Drawing.Point? lastMaskStrokePoint;
        private long lastMaskStrokeStatusUpdateTicks;
        private readonly HashSet<int> activeMaskStrokeSegmentIndices = new HashSet<int>();
        private readonly WpfMaskStrokeCommitSession activeMaskStrokeCommitSession = new WpfMaskStrokeCommitSession();
        private readonly Queue<WpfQueuedMaskStrokeCommit> queuedMaskStrokeCommits = new Queue<WpfQueuedMaskStrokeCommit>();
        private bool activeMaskStrokeInProgress;
        private bool isMaskStrokeCommitQueueScheduled;
        private bool isMaskStrokeToolEndFlushScheduled;
        private long maskStrokeToolEndFlushRequestedTicks;
        private bool suppressMaskStrokeCommitSelection;
        private int pendingMaskStrokeCommitCount;
        private int queuedMaskStrokeCommitSequence;
        private string activeMaskStrokeActionName = string.Empty;
        private bool activeMaskStrokeNeedsFullObjectRefresh;
        private bool isMaskStrokeCommitBatchFlushActive;
        private readonly HashSet<int> batchedMaskStrokeSegmentIndices = new HashSet<int>();
        private bool batchedMaskStrokeNeedsFullObjectRefresh;
        private bool batchedMaskStrokeHasActiveCandidates;
        private int batchedMaskStrokeChangedCommitCount;
        private double batchedMaskStrokeMaxWaitMilliseconds;
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


        internal WpfLabelingShellWindow(WpfLabelingShellViewModels viewModels)
        {
            this.viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
            InitializeComponent();
            PromoteSharedThemeResourcesToApplication();
            ApplyInitialWindowSizeToWorkArea();
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
            maskStrokePreviewCommitSwapTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(90)
            };
            maskStrokePreviewCommitSwapTimer.Tick += MaskStrokePreviewCommitSwapTimer_Tick;
            maskStrokeCommitQueueTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(WpfMaskEditStateService.CommitQueueQuietMilliseconds)
            };
            maskStrokeCommitQueueTimer.Tick += MaskStrokeCommitQueueTimer_Tick;
            DataContext = viewModels;
            RestoreWorkspaceLayoutSettings();
            TemplateMatchingAutoLabelViewModel.ConfigureHost(this);
            ComposePanelViewModels();
            LearningWorkflowViewModel.PropertyChanged += LearningWorkflowViewModel_PropertyChanged;
            ApplyProjectDatasetPurposeToWorkflow();
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
            MainCanvasViewModel.RenderDiagnosticsCaptured += MainCanvasViewModel_RenderDiagnosticsCaptured;
            MainCanvasView.DataContext = MainCanvasViewModel;
            InitializeImageQueuePanel();
            InitializeYoloEditorPanel();
            TryRestoreLastOpenedDatasetOnStartup();
            PopulateClassList();
            RefreshCandidateList();
            RefreshObjectList();
            UpdateCandidateActionState();
            UpdateYoloCommandButtons();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            SetAnnotationSaveStatusWaiting();
            RefreshAnnotationHistoryToolState();
            RefreshShellDatasetContext();
            FocusDatasetOnboardingTabIfNoActiveImage();
        }

        private void PromoteSharedThemeResourcesToApplication()
        {
            ResourceDictionary applicationResources = System.Windows.Application.Current?.Resources;
            if (applicationResources == null)
            {
                return;
            }

            foreach (ResourceDictionary dictionary in Resources.MergedDictionaries)
            {
                if (!applicationResources.MergedDictionaries.Any(existing => existing.GetType() == dictionary.GetType()))
                {
                    applicationResources.MergedDictionaries.Add(dictionary);
                }
            }

            foreach (string key in SharedThemeResourceKeys)
            {
                if (Resources.Contains(key))
                {
                    applicationResources[key] = Resources[key];
                }
            }
        }

        private void ApplyInitialWindowSizeToWorkArea()
        {
            Rect workArea = SystemParameters.WorkArea;
            Width = ClampInitialWindowDimension(PreferredInitialShellWidth, MinWidth, workArea.Width);
            Height = ClampInitialWindowDimension(PreferredInitialShellHeight, MinHeight, workArea.Height);
        }

        private static double ClampInitialWindowDimension(double preferred, double minimum, double available)
        {
            if (double.IsNaN(available) || available <= 0D)
            {
                return preferred;
            }

            return Math.Max(Math.Min(preferred, available), minimum);
        }

        public ObservableCollection<WpfImageQueueItem> ImageQueueItems => imageQueueItems;

        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates => candidateReviewState.PendingCandidates;

        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates => candidateReviewState.ConfirmedCandidates;



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






    }
}
