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
        private const int ObjectReviewFullRefreshDeleteLimit = 10_000;
        private const double PreferredInitialShellWidth = 1920D;
        private const double PreferredInitialShellHeight = 1080D;
        private readonly LabelingApplicationState global = LabelingApplicationState.Inst;
        private readonly BulkObservableCollection<WpfImageQueueItem> imageQueueItems = new BulkObservableCollection<WpfImageQueueItem>();
        private readonly Dictionary<string, WpfImageQueueItem> imageQueueItemsByPath = new Dictionary<string, WpfImageQueueItem>(StringComparer.OrdinalIgnoreCase);
        private readonly ImageQualityReviewWorkflowService imageQualityReviewWorkflowService = new ImageQualityReviewWorkflowService();
        private readonly AnomalyImageReviewSession anomalyImageReviewSession = new AnomalyImageReviewSession();
        private readonly ImageQueueSelectionService imageQueueSelectionService = new ImageQueueSelectionService();
        private readonly ImageQueueCatalogLoadCoordinator imageQueueCatalogLoadCoordinator;
        private readonly ImageQueueDetailRefreshCoordinator imageQueueDetailRefreshCoordinator;
        private readonly DatasetImageRootResolver datasetImageRootResolver = new DatasetImageRootResolver();
        private readonly ImageDecodeCacheService imageDecodeCacheService = new ImageDecodeCacheService();
        private readonly ImageDecodeService imageDecodeService = new ImageDecodeService();
        private readonly ImageDecodePreloadService imageDecodePreloadService = new ImageDecodePreloadService();
        private readonly ImageLoadResourceService imageLoadResourceService = new ImageLoadResourceService();
        private readonly ImageDisplayAdjustmentService imageDisplayAdjustmentService = new ImageDisplayAdjustmentService();
        private ImageLoadDiagnostics lastImageLoadDiagnostics = ImageLoadDiagnostics.Empty;
        private ICollectionView imageQueueView;
        private DrawingBitmap activeImageBitmap;
        private string activeImagePath = string.Empty;
        private string currentImageRoot = string.Empty;
        private DrawingSize activeImageSize = DrawingSize.Empty;
        private readonly List<DrawingRectangle> manualRois = new List<DrawingRectangle>();
        private readonly List<string> manualRoiClassNames = new List<string>();
        private readonly List<CanvasRoiShapeKind> manualRoiShapeKinds = new List<CanvasRoiShapeKind>();
        private readonly List<string> manualRoiOverlayIds = new List<string>();
        private readonly List<LabelingSegmentationObject> manualSegments = new List<LabelingSegmentationObject>();
        private readonly PolygonAnnotationService polygonAnnotationService = new PolygonAnnotationService();
        private readonly MaskAnnotationService maskAnnotationService = new MaskAnnotationService();
        private readonly SegmentationMergeService segmentationMergeService = new SegmentationMergeService();
        private readonly SegmentationSplitService segmentationSplitService = new SegmentationSplitService();
        private readonly SegmentationZOrderService segmentationZOrderService = new SegmentationZOrderService();
        private readonly SegmentationRemoveUnderlyingService segmentationRemoveUnderlyingService = new SegmentationRemoveUnderlyingService();
        private WpfSegmentationRemoveUnderlyingPlan pendingSegmentationRemoveUnderlyingPlan;
        private readonly ObjectSessionStateService objectSessionStateService = new ObjectSessionStateService();
        private readonly ObjectMetadataStateService objectMetadataStateService = new ObjectMetadataStateService();
        private readonly ObjectReviewWorkflowService objectReviewWorkflowService;
        private LabelingSegmentationObject pendingSegmentationSplitSource;
        private int pendingSegmentationSplitSourceIndex = -1;
        private WpfSegmentationSplitOrientation? pendingSegmentationSplitOrientation;
        private readonly SegmentationHoleService segmentationHoleService = new SegmentationHoleService();
        private readonly PolygonAnnotationService holePolygonAnnotationService = new PolygonAnnotationService();
        private LabelingSegmentationObject pendingSegmentationHoleSource;
        private int pendingSegmentationHoleSourceIndex = -1;
        private WpfSegmentationHoleEditMode? pendingSegmentationHoleEditMode;
        private readonly PolygonBoundaryEditWorkflowService polygonBoundaryEditWorkflowService = new PolygonBoundaryEditWorkflowService();
        private readonly AnnotationHistoryWorkflowService annotationHistoryWorkflowService = new AnnotationHistoryWorkflowService();
        private readonly AnnotationSaveWorkflowService annotationSaveWorkflowService = new AnnotationSaveWorkflowService();
        private readonly CandidateReviewStateService candidateReviewState = new CandidateReviewStateService();
        private readonly CandidateReviewPresentationService candidateReviewPresentationService = new CandidateReviewPresentationService();
        private readonly CandidateConfirmationService candidateConfirmationService = new CandidateConfirmationService();
        private readonly PatchCoreHeatmapReviewService patchCoreHeatmapReviewService = new PatchCoreHeatmapReviewService();
        private WpfPatchCoreHeatmapWindow patchCoreHeatmapWindow;
        private YoloWorkerSmokeCandidate patchCoreHeatmapWindowCandidate;
        private readonly CandidateReviewCompletionPresentationService candidateReviewCompletionPresentationService = new CandidateReviewCompletionPresentationService();
        private readonly ImageDetectionWorkflowService imageDetectionWorkflowService;
        private readonly YoloEnvironmentWorkflowService yoloEnvironmentWorkflowService;
        private readonly DetectionResultPresentationService detectionResultPresentationService = new DetectionResultPresentationService();
        private readonly DetectionTargetService detectionTargetService = new DetectionTargetService();
        private readonly TemplateMatchingSourceService templateMatchingSourceService = new TemplateMatchingSourceService();
        private readonly BatchDetectionWorkflowService batchDetectionWorkflowService = new BatchDetectionWorkflowService();
        private readonly BatchDetectionProgressService batchDetectionProgressService = new BatchDetectionProgressService();
        private readonly ImageLoadPresentationService imageLoadPresentationService = new ImageLoadPresentationService();
        private readonly ObjectReviewPresentationService objectReviewPresentationService = new ObjectReviewPresentationService();
        private readonly WpfFileDialogService fileDialogService = new WpfFileDialogService();
        private readonly DatasetSetupPathService datasetSetupPathService = new DatasetSetupPathService();
        private readonly DatasetSetupExecutionService datasetSetupExecutionService = new DatasetSetupExecutionService();
        private readonly DatasetSetupPresentationService datasetSetupPresentationService = new DatasetSetupPresentationService();
        private readonly ProjectRecipeSessionService projectRecipeSessionService = new ProjectRecipeSessionService();
        private readonly ProjectRecipeApplyWorkflowService projectRecipeApplyWorkflowService;
        private readonly ProjectArchiveWorkflowService projectArchiveWorkflowService = new ProjectArchiveWorkflowService();
        private readonly ClassCatalogWorkflowService classCatalogWorkflowService;
        private readonly CancellationTokenSource yoloSettingsRefreshCancellation = new CancellationTokenSource();
        private readonly TrainingWeightsService trainingWeightsService = new TrainingWeightsService();
        private readonly ModelCenterDashboardWorkflowService modelCenterDashboardWorkflowService;
        private readonly TrainingWeightsApplicationWorkflowService trainingWeightsApplicationWorkflowService = new TrainingWeightsApplicationWorkflowService();
        private readonly TrainingReadinessWorkflowService trainingReadinessWorkflowService = new TrainingReadinessWorkflowService();
        private readonly ExternalYoloDatasetIntakeWorkflowService externalYoloDatasetIntakeWorkflowService = new ExternalYoloDatasetIntakeWorkflowService();
        private readonly ExternalAuditWorkflowService externalAuditWorkflowService = new ExternalAuditWorkflowService();
        private readonly TrainingRuntimeWorkflowService trainingRuntimeWorkflowService;
        private readonly ModelCandidateLifecycleWorkflowService modelCandidateLifecycleWorkflowService = new ModelCandidateLifecycleWorkflowService();
        private readonly ModelComparisonReviewService modelComparisonReviewService = new ModelComparisonReviewService();
        private readonly ModelComparisonWorkflowService modelComparisonWorkflowService;
        private readonly ModelComparisonRunService modelComparisonRunService = new ModelComparisonRunService();
        private readonly SegmentationAdapterComparisonRunService segmentationAdapterComparisonRunService = new SegmentationAdapterComparisonRunService();
        private readonly SmartMaskWorkflowService smartMaskWorkflowService = new SmartMaskWorkflowService();
        private readonly SmartMaskPromptSessionService smartMaskPromptSession = new SmartMaskPromptSessionService();
        private readonly AnomalyClassificationEvaluationWorkflowService anomalyClassificationEvaluationWorkflowService;
        private readonly WorkspaceLayoutSettingsService workspaceLayoutSettingsService = new WorkspaceLayoutSettingsService();
        private readonly ApplicationClosePolicyService applicationClosePolicyService = new ApplicationClosePolicyService();
        private readonly CrashRecoveryJournalService crashRecoveryJournalService = new CrashRecoveryJournalService();
        private readonly CrashRecoveryJournalWorkflowService crashRecoveryJournalWorkflowService;
        private readonly CrashRecoverySessionService crashRecoverySessionService = new CrashRecoverySessionService();
        private readonly TrainingGuideHistoryService trainingGuideHistoryService = new TrainingGuideHistoryService();
        private readonly TrainingGuideHistoryWorkflowService trainingGuideHistoryWorkflowService;
        private readonly MaskEditStateService maskEditStateService = new MaskEditStateService();
        private readonly MaskStrokeHistoryDraftService maskStrokeHistoryDraftService = new MaskStrokeHistoryDraftService();
        private bool suppressImageQueueSelection;
        private bool isTrainingCommandRunning;
        private CancellationTokenSource trainingCommandCts;
        private bool suppressProjectRecipeSelection;
        private readonly Stopwatch inferenceStatusPulseStopwatch = new Stopwatch();
        private readonly ShellTimerSet shellTimers;
        private string pendingAnnotationVisibilityStatusText = string.Empty;
        private string lastAutoAppliedTrainingWeightsPath = string.Empty;
        private string pendingTrainingBaselineWeightsPath = string.Empty;
        private bool hasPendingTrainingWeightsRecipeSave;
        private YoloDatasetReadinessReport lastYoloTrainingReadinessReport;
        private ShellTheme currentTheme = ShellTheme.Dark;
        private WorkflowMode currentWorkflowMode = WorkflowMode.Labeling;
        private WpfAnnotationTool activeAnnotationTool = WpfAnnotationTool.Select;
        private bool applyingAnnotationToolSelection;
        private System.Drawing.Point? lastMaskStrokePoint;
        private long lastMaskStrokeStatusUpdateTicks;
        private readonly HashSet<int> activeMaskStrokeSegmentIndices = new HashSet<int>();
        private readonly MaskStrokeCommitSession activeMaskStrokeCommitSession = new MaskStrokeCommitSession();
        private readonly Queue<QueuedMaskStrokeCommit> queuedMaskStrokeCommits = new Queue<QueuedMaskStrokeCommit>();
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
        private readonly AnnotationDirtyState annotationDirtyState = new AnnotationDirtyState();
        private bool isApplicationCloseApproved;
        private bool isApplicationClosePromptOpen;
        private bool modelWorkflowPanelsComposed;
        private string activeRoiEditHistoryOverlayId = string.Empty;
        private int canvasLayoutAutoFitVersion;
        private readonly WpfLabelingShellViewModels viewModels;

        public WpfLabelingShellWindow()
            : this(new WpfLabelingShellViewModels())
        {
        }


        internal WpfLabelingShellWindow(WpfLabelingShellViewModels viewModels)
        {
            this.viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
            projectRecipeApplyWorkflowService = new ProjectRecipeApplyWorkflowService(projectRecipeSessionService);
            imageDetectionWorkflowService = CreateImageDetectionWorkflow();
            yoloEnvironmentWorkflowService = CreateYoloEnvironmentWorkflow();
            modelCenterDashboardWorkflowService = new ModelCenterDashboardWorkflowService(trainingWeightsService);
            modelComparisonWorkflowService = CreateModelComparisonWorkflow();
            anomalyClassificationEvaluationWorkflowService = new AnomalyClassificationEvaluationWorkflowService();
            crashRecoveryJournalWorkflowService = new CrashRecoveryJournalWorkflowService(
                crashRecoveryJournalService,
                callback => Dispatcher.BeginInvoke(
                    new Action(callback),
                    System.Windows.Threading.DispatcherPriority.ContextIdle));
            trainingGuideHistoryWorkflowService = new TrainingGuideHistoryWorkflowService(trainingGuideHistoryService);
            trainingRuntimeWorkflowService = new TrainingRuntimeWorkflowService(
                () => global.Data,
                () => global.ModelRuntime.TrainingWorkflow,
                () => global.ModelRuntime.PythonClientProcess,
                () => global.ModelRuntime.DeepLearning,
                global.GetPythonCommunicationStatusSnapshot,
                (timeoutMilliseconds, cancellationToken) => global.ModelRuntime.EnsurePythonModelClientReadyAsync(timeoutMilliseconds, cancellationToken),
                GetCurrentRecipeName);
            crashRecoveryJournalWorkflowService.WriteFailed += OnCrashRecoveryJournalWriteFailed;
            crashRecoveryJournalWorkflowService.CaptureFailed += OnCrashRecoveryJournalCaptureFailed;
            imageQueueCatalogLoadCoordinator = new ImageQueueCatalogLoadCoordinator(
                new ImageQueueCatalogLoadService(imageQueueSelectionService));
            imageQueueDetailRefreshCoordinator = new ImageQueueDetailRefreshCoordinator(
                new ImageQueueDetailRefreshService());
            classCatalogWorkflowService = new ClassCatalogWorkflowService(projectRecipeSessionService);
            objectReviewWorkflowService = new ObjectReviewWorkflowService(
                new ObjectMetadataPersistenceService(),
                projectRecipeSessionService);
            InitializeComponent();
            MainCanvasViewModel.ConfigureImageFileDialogHost(new WpfImageFileDialogHost(this, fileDialogService));
            LocalizationTextRuntimeService.RegisterWindow(this);
            PromoteSharedThemeResourcesToApplication();
            ApplyInitialWindowSizeToWorkArea();
            shellTimers = new ShellTimerSet(
                Dispatcher,
                InferenceStatusPulseTimer_Tick,
                TrainingStatusPollTimer_Tick,
                MaskStrokePreviewCommitSwapTimer_Tick,
                MaskStrokeCommitQueueTimer_Tick,
                DisplayAdjustmentRefreshTimer_Tick,
                AnnotationVisibilityRefreshTimer_Tick);
            DataContext = viewModels;
            viewModels.LanguageViewModel.LanguageChanged += LanguageViewModel_LanguageChanged;
            RuntimeDiagnosticsViewModel.AttachGraphicsCapabilityProvider(
                () => OpenGlRuntimeCapabilityProbe.Probe(MainCanvasViewModel.ImageViewer));
            RuntimeDiagnosticsViewModel.ConfigureOpenSetupCenterAction(ExecuteOpenEnvironmentSetupCenterCommand);
            RestoreWorkspaceLayoutSettings();
            ShellViewModel.RefreshLocalizedPresentation();
            TemplateMatchingAutoLabelViewModel.ConfigureHost(this);
            ComposePanelViewModels();
            LearningWorkflowViewModel.PropertyChanged += LearningWorkflowViewModel_PropertyChanged;
            ApplyProjectDatasetPurposeToWorkflow();
            ConfigureShellCommands();
            RegisterLearningWorkflowPanelNames();
            RegisterImageQueuePanelNames();
            RegisterCanvasPanelNames();
            RegisterObjectReviewPanelNames();
            RegisterClassCatalogPanelNames();
            RegisterProjectConfigPanelNames();
            RegisterStatusBarPanelNames();
            RegisterShellLogPanelNames();
            EnsureModelWorkflowPanelsComposed();
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
            MainCanvasView.SizeChanged += MainCanvasView_SizeChanged;
            InitializeImageQueuePanel();
            TryRestoreLastOpenedDatasetOnStartup();
            PopulateClassList();
            RefreshCandidateList();
            RefreshObjectList();
            UpdateCandidateActionState();
            UpdateYoloCommandButtons();
            // Keep the first-run dashboard/checklist in its explicit "before check"
            // state.  Dataset Health is an operator action; constructing the shell
            // must not scan the current data and replace the catalog-backed first
            // action before the operator chooses to run that check.  Recipe apply,
            // queue completion, and the explicit guide actions still refresh the
            // readiness report through their existing paths.
            SetAnnotationSaveStatusWaiting();
            RefreshAnnotationHistoryToolState();
            RefreshShellDatasetContext();
            FocusDatasetOnboardingTabIfNoActiveImage();
        }

        // The event remains a View adapter; formatting policy lives in the
        // UI-independent canvas diagnostics presentation service.
        private void MainCanvasViewModel_RenderDiagnosticsCaptured(
            object sender,
            RoiImageCanvasRenderDiagnosticsEventArgs diagnostics)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string message = CanvasRenderDiagnosticsPresentationService.BuildLogMessage(diagnostics);
            if (!string.IsNullOrWhiteSpace(message))
            {
                AppendLog(message);
            }
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

            foreach (string key in ThemePalette.SharedResourceKeys)
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
