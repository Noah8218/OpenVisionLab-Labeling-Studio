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
using OpenVisionLab;
using OpenVisionLab.Wpf.MessageDialogs;
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
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using TextBox = System.Windows.Controls.TextBox;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;
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
    public partial class WpfLabelingShellWindow : WpfUiFluentWindow, IWpfTemplateMatchingAutoLabelHost
    {
        private const double PreferredInitialShellWidth = 1920D;
        private const double PreferredInitialShellHeight = 1080D;
        private readonly LabelingApplicationState applicationState = LabelingApplicationState.Inst;
        private readonly BulkObservableCollection<WpfImageQueueItem> imageQueueItems = new BulkObservableCollection<WpfImageQueueItem>();
        private readonly Dictionary<string, WpfImageQueueItem> imageQueueItemsByPath = new Dictionary<string, WpfImageQueueItem>(StringComparer.OrdinalIgnoreCase);
        private readonly ImageQualityReviewWorkflowService imageQualityReviewWorkflowService = new ImageQualityReviewWorkflowService();
        private readonly AnomalyImageReviewSession anomalyImageReviewSession = new AnomalyImageReviewSession();
        private readonly ImageQueueSelectionService imageQueueSelectionService = new ImageQueueSelectionService();
        private readonly ImageQueueCatalogLoadCoordinator imageQueueCatalogLoadCoordinator;
        private readonly DatasetImageRootResolver datasetImageRootResolver = new DatasetImageRootResolver();
        private readonly ImageDecodeCacheService imageDecodeCacheService = new ImageDecodeCacheService();
        private readonly ImageDecodeService imageDecodeService = new ImageDecodeService();
        private readonly ImageDecodePreloadService imageDecodePreloadService = new ImageDecodePreloadService();
        private readonly ImageLoadResourceService imageLoadResourceService = new ImageLoadResourceService();
        private ImageLoadDiagnostics lastImageLoadDiagnostics = ImageLoadDiagnostics.Empty;
        // Compatibility field retained for headless image-load tests; the workflow adapter consumes it through a provider.
        private Action<CvMat, string> imageViewerLoadOverride = null;
        private readonly ImageQueueLoadingAdapter imageQueueLoadingAdapter;
        private readonly ImageLoadWorkflowAdapter imageLoadWorkflowAdapter;
        private ICollectionView imageQueueView;
        private readonly List<DrawingRectangle> manualRois = new List<DrawingRectangle>();
        private readonly List<string> manualRoiClassNames = new List<string>();
        private readonly List<CanvasRoiShapeKind> manualRoiShapeKinds = new List<CanvasRoiShapeKind>();
        private readonly List<string> manualRoiOverlayIds = new List<string>();
        private readonly List<LabelingSegmentationObject> manualSegments = new List<LabelingSegmentationObject>();
        private readonly PolygonAnnotationService polygonAnnotationService = new PolygonAnnotationService();
        private readonly PolygonBoundaryEditAdapter polygonBoundaryEditAdapter;
        private readonly AnnotationToolSelectionAdapter annotationToolSelectionAdapter;
        private readonly AnnotationVisibilityAdapter annotationVisibilityAdapter;
        private readonly MaskStrokeWorkflowAdapter maskStrokeWorkflowAdapter;
        private readonly AnnotationInputAdapter annotationInputAdapter;
        private readonly FourPointBoxService fourPointBoxService = new FourPointBoxService();
        private readonly MaskAnnotationService maskAnnotationService = new MaskAnnotationService();
        private readonly SegmentationMergeService segmentationMergeService = new SegmentationMergeService();
        private readonly SegmentationZOrderService segmentationZOrderService = new SegmentationZOrderService();
        private readonly SegmentationEditAdapter segmentationEditAdapter;
        private readonly ObjectSessionStateService objectSessionStateService = new ObjectSessionStateService();
        private readonly ObjectMetadataStateService objectMetadataStateService = new ObjectMetadataStateService();
        private readonly ObjectReviewWorkflowService objectReviewWorkflowService;
        private readonly ObjectReviewStateAdapter objectReviewStateAdapter;
        private readonly ObjectReviewInteractionAdapter objectReviewInteractionAdapter;
        private readonly PolygonBoundaryEditWorkflowService polygonBoundaryEditWorkflowService = new PolygonBoundaryEditWorkflowService();
        private readonly AnnotationHistoryAdapter annotationHistoryAdapter;
        // Compatibility field retained for existing diagnostics/tests; the adapter owns creation and use.
        private readonly AnnotationHistoryWorkflowService annotationHistoryWorkflowService;
        private readonly AnnotationSaveWorkflowService annotationSaveWorkflowService = new AnnotationSaveWorkflowService();
        private readonly AnnotationSaveStateAdapter annotationSaveStateAdapter;
        private readonly AnnotationPersistenceAdapter annotationPersistenceAdapter;
        private readonly AnnotationLoadAdapter annotationLoadAdapter;
        private readonly AnnotationSegmentEditAdapter annotationSegmentEditAdapter;
        private readonly AnnotationProductivityAdapter annotationProductivityAdapter;
        private readonly FourPointBoxInputAdapter fourPointBoxInputAdapter;
        private readonly AnnotationToolSettingsAdapter annotationToolSettingsAdapter;
        private readonly AnnotationRenderingAdapter annotationRenderingAdapter;
        private readonly SegmentationZOrderCommandAdapter segmentationZOrderCommandAdapter;
        private readonly SmartMaskWorkflowAdapter smartMaskWorkflowAdapter;
        private readonly CandidateReviewStateService candidateReviewState = new CandidateReviewStateService();
        private readonly CandidateReviewPresentationService candidateReviewPresentationService = new CandidateReviewPresentationService();
        private readonly CandidateConfirmationService candidateConfirmationService = new CandidateConfirmationService();
        private readonly PatchCoreHeatmapReviewService patchCoreHeatmapReviewService = new PatchCoreHeatmapReviewService();
        private readonly CandidateReviewCompletionPresentationService candidateReviewCompletionPresentationService = new CandidateReviewCompletionPresentationService();
        private readonly CandidateReviewStateAdapter candidateReviewStateAdapter;
        private readonly ImageQueueReviewAdapter imageQueueReviewAdapter;
        private readonly ImageQueueDetailRefreshAdapter imageQueueDetailRefreshAdapter;
        private readonly ImageQueueCatalogProjectionAdapter imageQueueCatalogProjectionAdapter;
        private readonly ImageQueueCatalogLoadAdapter imageQueueCatalogLoadAdapter;
        private readonly ImageChangeStateAdapter imageChangeStateAdapter;
        private readonly ImageQueueRootCommandAdapter imageQueueRootCommandAdapter;
        private readonly ImageQueueNavigationAdapter imageQueueNavigationAdapter;
        private readonly CandidateReviewActionAdapter candidateReviewActionAdapter;
        private readonly DetectionWorkflowAdapter detectionWorkflowAdapter;
        private readonly ImageDetectionWorkflowService imageDetectionWorkflowService;
        private readonly DetectionResultProjectionAdapter detectionResultProjectionAdapter;
        private readonly TemplateMatchingAutoLabelHostAdapter templateMatchingAutoLabelHostAdapter;
        private readonly YoloEnvironmentWorkflowService yoloEnvironmentWorkflowService;
        private readonly YoloEnvironmentWorkflowAdapter yoloEnvironmentWorkflowAdapter;
        private readonly YoloRuntimeStatusAdapter yoloRuntimeStatusAdapter;
        private readonly DetectionResultPresentationService detectionResultPresentationService = new DetectionResultPresentationService();
        private readonly DetectionTargetService detectionTargetService = new DetectionTargetService();
        private readonly TemplateMatchingSourceService templateMatchingSourceService = new TemplateMatchingSourceService();
        private readonly BatchDetectionWorkflowService batchDetectionWorkflowService = new BatchDetectionWorkflowService();
        private readonly BatchDetectionProgressService batchDetectionProgressService = new BatchDetectionProgressService();
        private readonly BatchDetectionWorkflowAdapter batchDetectionWorkflowAdapter;
        private readonly ImageLoadPresentationService imageLoadPresentationService = new ImageLoadPresentationService();
        private readonly ObjectReviewPresentationService objectReviewPresentationService = new ObjectReviewPresentationService();
        private readonly WpfFileDialogService fileDialogService = new WpfFileDialogService();
        private readonly DatasetSetupPathService datasetSetupPathService = new DatasetSetupPathService();
        private readonly DatasetSetupExecutionService datasetSetupExecutionService = new DatasetSetupExecutionService();
        private readonly DatasetSetupPresentationService datasetSetupPresentationService = new DatasetSetupPresentationService();
        private readonly DatasetSetupWorkflowAdapter datasetSetupWorkflowAdapter;
        private readonly ProjectRecipeSessionService projectRecipeSessionService = new ProjectRecipeSessionService();
        private readonly ProjectRecipeApplyWorkflowService projectRecipeApplyWorkflowService;
        private readonly ProjectArchiveWorkflowService projectArchiveWorkflowService = new ProjectArchiveWorkflowService();
        private readonly ClassCatalogWorkflowService classCatalogWorkflowService;
        private readonly AnnotationClassRenameService annotationClassRenameService = new AnnotationClassRenameService();
        private readonly TrainingWeightsService trainingWeightsService = new TrainingWeightsService();
        private readonly ModelCenterDashboardWorkflowService modelCenterDashboardWorkflowService;
        private readonly TrainingWeightsApplicationWorkflowService trainingWeightsApplicationWorkflowService = new TrainingWeightsApplicationWorkflowService();
        private readonly TrainingReadinessWorkflowService trainingReadinessWorkflowService = new TrainingReadinessWorkflowService();
        private readonly ExternalYoloDatasetIntakeWorkflowService externalYoloDatasetIntakeWorkflowService = new ExternalYoloDatasetIntakeWorkflowService();
        private readonly ExternalAuditWorkflowService externalAuditWorkflowService = new ExternalAuditWorkflowService();
        private readonly TrainingRuntimeWorkflowService trainingRuntimeWorkflowService;
        private readonly ModelCandidateLifecycleWorkflowService modelCandidateLifecycleWorkflowService = new ModelCandidateLifecycleWorkflowService();
        private readonly ModelComparisonReviewService modelComparisonReviewService = new ModelComparisonReviewService();
        private readonly ModelCenterWorkflowAdapter modelCenterWorkflowAdapter;
        private readonly ModelComparisonWorkflowAdapter modelComparisonWorkflowAdapter;
        private readonly ModelComparisonWorkflowService modelComparisonWorkflowService;
        private readonly ModelComparisonRunService modelComparisonRunService = new ModelComparisonRunService();
        private readonly SegmentationAdapterComparisonRunService segmentationAdapterComparisonRunService = new SegmentationAdapterComparisonRunService();
        private readonly SmartMaskWorkflowService smartMaskWorkflowService = new SmartMaskWorkflowService();
        private readonly SmartMaskPromptSessionService smartMaskPromptSession = new SmartMaskPromptSessionService();
        private readonly AnomalyClassificationEvaluationWorkflowService anomalyClassificationEvaluationWorkflowService;
        private readonly ApplicationClosePolicyService applicationClosePolicyService = new ApplicationClosePolicyService();
        private readonly CrashRecoveryJournalService crashRecoveryJournalService = new CrashRecoveryJournalService();
        private readonly CrashRecoveryJournalWorkflowService crashRecoveryJournalWorkflowService;
        private readonly CrashRecoverySessionService crashRecoverySessionService = new CrashRecoverySessionService();
        private readonly CrashRecoverySnapshotAdapter crashRecoverySnapshotAdapter;
        private readonly CrashRecoveryIntegrationAdapter crashRecoveryIntegrationAdapter;
        private readonly TrainingGuideHistoryService trainingGuideHistoryService = new TrainingGuideHistoryService();
        private readonly TrainingGuideHistoryWorkflowService trainingGuideHistoryWorkflowService;
        private readonly AnnotationWorkflowCommandAdapter annotationWorkflowCommandAdapter;
        private readonly TrainingGuideCommandAdapter trainingGuideCommandAdapter;
        private bool suppressImageQueueSelection;
        // Compatibility seam retained for focused queue-navigation tests; navigation policy is owned by ImageQueueNavigationAdapter.
        private Func<string, bool> imageQueueNavigationLoadOverride = null;
        private readonly TrainingCommandLifecycleService trainingCommandLifecycleService = new TrainingCommandLifecycleService();
        private readonly ShellTimerSet shellTimers;
        private string lastAutoAppliedTrainingWeightsPath = string.Empty;
        private string pendingTrainingBaselineWeightsPath = string.Empty;
        private bool hasPendingTrainingWeightsRecipeSave;
        private YoloDatasetReadinessReport lastYoloTrainingReadinessReport;
        private ShellTheme currentTheme = ShellTheme.Dark;
        private WpfAnnotationTool activeAnnotationTool = WpfAnnotationTool.Select;
        private readonly AnnotationDirtyState annotationDirtyState = new AnnotationDirtyState();
        private bool isApplicationCloseApproved;
        private bool modelWorkflowPanelsComposed;
        private readonly WpfLabelingShellViewModels viewModels;
        private readonly ShellAuxiliaryWindowHost auxiliaryWindowHost;
        private readonly DatasetTransferWindowHost datasetTransferWindowHost;
        private readonly PatchCoreHeatmapWindowHost patchCoreHeatmapWindowHost;
        private readonly CanvasWorkflowContextPresenter canvasWorkflowContextPresenter;
        private readonly CanvasAnnotationToolScopePresenter canvasAnnotationToolScopePresenter;
        private readonly CandidateReviewViewerNavigator candidateReviewViewerNavigator;
        private readonly CanvasDisplayWorkspaceAdapter displayWorkspaceAdapter;
        private readonly ShellStatusPresentationAdapter statusPresentationAdapter;
        private readonly ExternalYoloDatasetIntakeAdapter externalYoloDatasetIntakeAdapter;
        private readonly ClassCatalogWorkflowAdapter classCatalogWorkflowAdapter;
        private readonly TrainingGuideWorkflowAdapter trainingGuideWorkflowAdapter;
        private readonly WorkflowNavigationAdapter workflowNavigationAdapter;
        private readonly WorkflowPanelFocusAdapter workflowPanelFocusAdapter;
        private readonly DatasetPurposeWorkflowAdapter datasetPurposeWorkflowAdapter;
        private readonly ProjectSettingsWorkflowAdapter projectSettingsWorkflowAdapter;
        private readonly ProjectSettingsEditorAdapter projectSettingsEditorAdapter;
        private readonly ShellKeyboardShortcutAdapter shellKeyboardShortcutAdapter;
        private readonly ShellInputLifecycleAdapter shellInputLifecycleAdapter;
        private readonly TrainingRuntimeAdapter trainingRuntimeAdapter;

        // SegmentationEditAdapter owns pending edit state and service instances;
        // these read-only compatibility accessors keep existing callers explicit.
        private SegmentationHoleService segmentationHoleService
            => segmentationEditAdapter?.SegmentationHoleService;
        private SegmentationSplitService segmentationSplitService
            => segmentationEditAdapter?.SegmentationSplitService;
        private SegmentationRemoveUnderlyingService segmentationRemoveUnderlyingService
            => segmentationEditAdapter?.SegmentationRemoveUnderlyingService;
        private PolygonAnnotationService holePolygonAnnotationService
            => segmentationEditAdapter?.HolePolygonAnnotationService;
        private WpfSegmentationRemoveUnderlyingPlan pendingSegmentationRemoveUnderlyingPlan
            => segmentationEditAdapter?.PendingSegmentationRemoveUnderlyingPlan;
        private LabelingSegmentationObject pendingSegmentationSplitSource
            => segmentationEditAdapter?.PendingSegmentationSplitSource;
        private int pendingSegmentationSplitSourceIndex
            => segmentationEditAdapter?.PendingSegmentationSplitSourceIndex ?? -1;
        private WpfSegmentationSplitOrientation? pendingSegmentationSplitOrientation
            => segmentationEditAdapter?.PendingSegmentationSplitOrientation;
        private LabelingSegmentationObject pendingSegmentationHoleSource
            => segmentationEditAdapter?.PendingSegmentationHoleSource;
        private int pendingSegmentationHoleSourceIndex
            => segmentationEditAdapter?.PendingSegmentationHoleSourceIndex ?? -1;
        private WpfSegmentationHoleEditMode? pendingSegmentationHoleEditMode
            => segmentationEditAdapter?.PendingSegmentationHoleEditMode;

        public WpfLabelingShellWindow()
            : this(new WpfLabelingShellViewModels())
        {
        }


        internal WpfLabelingShellWindow(WpfLabelingShellViewModels viewModels)
        {
            this.viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
            projectRecipeApplyWorkflowService = new ProjectRecipeApplyWorkflowService(projectRecipeSessionService);
            classCatalogWorkflowService = new ClassCatalogWorkflowService(projectRecipeSessionService);
            detectionWorkflowAdapter = new DetectionWorkflowAdapter(
                new DetectionWorkflowAdapterContext
                {
                    SettingsAccessor = () =>
                    {
                        EnsureProjectSettings();
                        return applicationState.Data.ProjectSettings.PythonModel;
                    },
                    DetectionResultsAccessor = () => applicationState.DetectionResults,
                    EnsureReadyAsync = (timeout, token) => applicationState.ModelRuntime.EnsurePythonModelClientReadyAsync(timeout, token),
                    TryStartDetection = (currentImage, path, size) => currentImage
                        ? applicationState.ModelRuntime.DetectionWorkflow.TryStartCurrentImageDetection(
                            applicationState.Data,
                            applicationState.ModelRuntime.DeepLearning,
                            applicationState.DetectionTransport,
                            () => true)
                        : applicationState.ModelRuntime.DetectionWorkflow.TryStartImagePathDetection(
                            applicationState.Data,
                            applicationState.ModelRuntime.DeepLearning,
                            applicationState.DetectionTransport,
                            path,
                            size,
                            () => true),
                    WorkerFailureAccessor = () => YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                        applicationState.GetPythonCommunicationStatusSnapshot(),
                        applicationState.ModelRuntime.PythonClientProcess?.LastError),
                    RequestErrorAccessor = () => applicationState.GetPythonCommunicationStatusSnapshot().LastError,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    TryLoadImage = (path, populateQueue) => TryLoadImage(path, populateQueue: populateQueue),
                    ApplyCandidates = (candidates, succeeded) => ApplyDetectionCandidates(candidates, succeeded),
                    RefreshActions = () =>
                    {
                        UpdateYoloCommandButtons();
                        UpdateCandidateActionState();
                    },
                    SetPythonStatus = SetPythonStatus,
                    SetCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    SetInferenceStatus = (text, busy, warning) => SetGlobalInferenceStatus(text, busy, warning),
                    AppendLog = AppendLog,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsBatchDetectionRunning = () => batchDetectionWorkflowService.IsRunning
                });
            imageDetectionWorkflowService = detectionWorkflowAdapter.WorkflowService;
            detectionResultProjectionAdapter = new DetectionResultProjectionAdapter(
                new DetectionResultProjectionAdapterContext
                {
                    CandidateReviewState = candidateReviewState,
                    DetectionResultPresentationService = detectionResultPresentationService,
                    ClearCandidateReviewHistory = () => CandidateReviewViewModel?.ClearReviewHistory(),
                    ApplyCanvasDisplayMode = (mode, redraw, logChange) => ApplyCanvasDisplayMode(mode, redraw, logChange),
                    RefreshCandidateList = RefreshCandidateList,
                    RefreshObjectList = RefreshObjectList,
                    RedrawReviewRois = RedrawReviewRois,
                    SetActiveImageDetectionStatus = SetActiveImageDetectionStatus,
                    ApplyActiveAnomalyClassification = candidates => ApplyActiveAnomalyClassification(candidates),
                    AddCandidateReviewHistory = message => CandidateReviewViewModel?.AddReviewHistory(message),
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    CandidateConfidenceFilterProvider = GetCandidateConfidenceFilter,
                    AppendLog = AppendLog
                });
            templateMatchingAutoLabelHostAdapter = new TemplateMatchingAutoLabelHostAdapter(
                new TemplateMatchingAutoLabelHostAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    TemplateMatchingSourceService = templateMatchingSourceService,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    DetectionTargetService = detectionTargetService,
                    BatchDetectionWorkflowService = batchDetectionWorkflowService,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    CandidateReviewState = candidateReviewState,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    CreateTemplateMatchingSourceSnapshot = () =>
                    {
                        TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected);
                        return new TemplateMatchingSourceSnapshot(
                            selected,
                            manualRois,
                            manualRoiClassNames,
                            manualSegments);
                    },
                    VisibleQueueItemsProvider = () => imageQueueView == null
                        ? imageQueueItems.ToList()
                        : imageQueueView.Cast<object>().OfType<WpfImageQueueItem>().ToList(),
                    AllQueueItemsProvider = () => imageQueueItems.ToList(),
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsImageDetectionRunning = () => imageDetectionWorkflowService.IsDetecting,
                    IsBatchDetectionRunning = () => batchDetectionWorkflowService.IsRunning,
                    AppendLog = AppendLog,
                    ShowGuide = (title, message) => ShowTemplateAutoLabelGuide(title, message),
                    ApplyDetectionCandidates = (candidates, succeeded) => ApplyDetectionCandidates(candidates, succeeded),
                    SetPythonStatus = SetPythonStatus,
                    SetCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    SetGlobalInferenceStatus = (text, busy, warning) => SetGlobalInferenceStatus(text, busy, warning),
                    CaptureBatchReviewStatusSave = CaptureBatchReviewStatusSave,
                    UpdateBatchDetectionControls = UpdateBatchDetectionControls,
                    UpdateYoloCommandButtons = UpdateYoloCommandButtons,
                    FindQueueItem = FindImageQueueItem,
                    ApplyReviewStatusToItem = ApplyReviewStatusToItem,
                    RefreshQueueView = () => ImageQueuePanelControl?.RefreshQueueView(),
                    RefreshActiveImageQueueStatus = RefreshActiveImageQueueStatus,
                    UpdateImageQueueStatusText = () => UpdateImageQueueStatusText(),
                    UpdateApplicationData = () => applicationState.System?.UpdateData(),
                    YieldBatchFrameAsync = YieldBatchDetectionResultFrameAsync,
                    ClearCandidateReviewHistory = () => CandidateReviewViewModel?.ClearReviewHistory(),
                    RefreshCandidateList = RefreshCandidateList,
                    RefreshObjectList = RefreshObjectList,
                    RedrawReviewRois = RedrawReviewRois,
                    AddCandidateReviewHistory = message => CandidateReviewViewModel?.AddReviewHistory(message),
                    RefreshImageQueueViewAfterItemStateChange = RefreshImageQueueViewAfterItemStateChange,
                    IsSegmentationDatasetPurposeActive = IsSegmentationDatasetPurposeActive,
                    RegisterAnnotationHistoryBeforeChange = action => RegisterAnnotationHistoryBeforeChange(action),
                    ApplyCanvasDisplayMode = (mode, redraw, logChange) => ApplyCanvasDisplayMode(mode, redraw, logChange),
                    PopulateClassList = () => PopulateClassList(),
                    ShowSavedLabelsWorkflowView = ShowSavedLabelsWorkflowView,
                    SetModelStatus = SetModelStatus,
                    GetManualRoiClassName = GetManualRoiClassName
                });
            yoloEnvironmentWorkflowAdapter = new YoloEnvironmentWorkflowAdapter(
                new YoloEnvironmentWorkflowAdapterContext
                {
                    ApplicationState = applicationState,
                    DataProvider = () => applicationState.Data,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    YoloModelSettingsViewModelProvider = () => viewModels.YoloModelSettingsViewModel,
                    YoloStatusViewModelProvider = () => viewModels.YoloStatusViewModel,
                    TrainingSettingsViewModelProvider = () => viewModels.TrainingSettingsViewModel,
                    CandidateReviewViewModelProvider = () => viewModels.CandidateReviewViewModel,
                    ShellViewModelProvider = () => viewModels.ShellViewModel,
                    GetPythonModelRuntimeState = GetPythonModelRuntimeState,
                    IsDetecting = () => imageDetectionWorkflowService.IsDetecting,
                    IsBatchDetectionRunning = () => batchDetectionWorkflowService.IsRunning,
                    IsTrainingCommandRunning = () => trainingCommandLifecycleService.IsRunning,
                    IsInferenceWorkflowActive = () => IsInferenceWorkflowActive,
                    SetWorkflowMode = isInference => workflowNavigationAdapter?.SetWorkflowMode(isInference),
                    EnsureModelRuntimeForInference = EnsureModelRuntimeForInference,
                    EnsureInferenceModeForDetection = EnsureInferenceModeForDetection,
                    RunInteractiveDetectionAsync = allowSmokeFallback => RunInteractiveDetectionAsync(allowSmokeFallback: allowSmokeFallback),
                    EnsureProjectSettings = EnsureProjectSettings,
                    NotifyYoloPathSelected = NotifyYoloPathSelected,
                    RefreshYoloStatus = RefreshYoloStatus,
                    RefreshYoloSettingsPanelAsync = validation => RefreshYoloSettingsPanelAsync(validation),
                    SaveYoloEditorFields = SaveYoloEditorFields,
                    SaveTrainingEditorFields = SaveTrainingEditorFields,
                    RefreshCandidateConfidenceFilterFromAppliedSettings = RefreshCandidateConfidenceFilterFromAppliedSettings,
                    PopulateYoloEditorFields = PopulateYoloEditorFields,
                    PopulateTrainingEditorFields = PopulateTrainingEditorFields,
                    UpdateYoloTrainingHistoryText = UpdateYoloTrainingHistoryText,
                    SetGlobalInferenceStatus = (text, busy, warning) => SetGlobalInferenceStatus(text, busy, warning),
                    SetProjectConfigStatus = SetProjectConfigStatus,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    SetYoloRecoveryStatus = SetYoloRecoveryStatus,
                    ClearYoloRecoveryStatus = ClearYoloRecoveryStatus,
                    UpdateYoloCommandButtons = UpdateYoloCommandButtons,
                    ShowYoloModelCenterWorkflowView = ShowYoloModelCenterWorkflowView,
                    ShowModelRuntimeUnavailable = ShowModelRuntimeUnavailable,
                    FocusYoloPythonPath = () => YoloPythonPathBox?.Focus(),
                    FocusYoloProjectRoot = () => YoloProjectRootBox?.Focus(),
                    FocusYoloWeightsPath = () => YoloWeightsPathBox?.Focus(),
                    ExpandYoloAdvancedSettings = () =>
                    {
                        if (YoloModelSettingsPanelControl?.AdvancedSettingsExpander != null)
                        {
                            YoloModelSettingsPanelControl.AdvancedSettingsExpander.IsExpanded = true;
                        }
                    },
                    YoloPythonPathTextProvider = () => YoloModelSettingsViewModel?.PythonExecutablePath ?? string.Empty,
                    YoloProjectRootTextProvider = () => YoloModelSettingsViewModel?.ProjectRootPath ?? string.Empty,
                    SelectFile = (title, filter, initialPath) =>
                        TryPickFile(title, filter, initialPath, out string selectedPath)
                            ? selectedPath
                            : string.Empty,
                    SelectFolder = (title, initialPath) =>
                        TryPickFolder(title, initialPath, out string selectedPath)
                            ? selectedPath
                            : string.Empty,
                    SaveProjectConfigFromPanel = SaveProjectConfigFromPanel,
                    RefreshModelCenterDashboard = (path, pending) => RefreshModelCenterDashboard(
                        configuredWeightsPathOverride: path,
                        pendingManualWeightsSelection: pending),
                    AppendLog = AppendLog,
                    SetUltralyticsPackageOperationResult = (summary, detail) =>
                        YoloModelSettingsViewModel?.SetRuntimePackageOperationResult(summary, detail),
                    ConfirmUltralyticsPackageOperation = ConfirmUltralyticsPackageOperation,
                    PendingTrainingBaselineWeightsPathProvider = () => pendingTrainingBaselineWeightsPath,
                    SetPendingTrainingBaselineWeightsPath = path => pendingTrainingBaselineWeightsPath = path ?? string.Empty,
                    HasPendingTrainingWeightsRecipeSaveProvider = () => hasPendingTrainingWeightsRecipeSave,
                    SetHasPendingTrainingWeightsRecipeSave = value => hasPendingTrainingWeightsRecipeSave = value,
                    TrainingWeightsApplicationWorkflowService = trainingWeightsApplicationWorkflowService
                });
            yoloEnvironmentWorkflowService = yoloEnvironmentWorkflowAdapter.WorkflowService;
            modelCenterDashboardWorkflowService = new ModelCenterDashboardWorkflowService(trainingWeightsService);
            anomalyClassificationEvaluationWorkflowService = new AnomalyClassificationEvaluationWorkflowService();
            modelCenterWorkflowAdapter = new ModelCenterWorkflowAdapter(
                new ModelCenterWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ModelCandidateLifecycleWorkflowService = modelCandidateLifecycleWorkflowService,
                    ModelCenterDashboardWorkflowService = modelCenterDashboardWorkflowService,
                    AnomalyClassificationEvaluationWorkflowService = anomalyClassificationEvaluationWorkflowService,
                    IsApplicationCloseApprovedProvider = () => isApplicationCloseApproved,
                    CandidateReviewViewModelProvider = () => viewModels.CandidateReviewViewModel,
                    ShellViewModelProvider = () => viewModels.ShellViewModel,
                    LearningWorkflowViewModelProvider = () => viewModels.LearningWorkflowViewModel,
                    TrainingSettingsViewModelProvider = () => viewModels.TrainingSettingsViewModel,
                    YoloModelSettingsViewModelProvider = () => viewModels.YoloModelSettingsViewModel,
                    PendingTrainingBaselineWeightsPathProvider = () => pendingTrainingBaselineWeightsPath,
                    SetPendingTrainingBaselineWeightsPath = path => pendingTrainingBaselineWeightsPath = path ?? string.Empty,
                    HasPendingTrainingWeightsRecipeSaveProvider = () => hasPendingTrainingWeightsRecipeSave,
                    SetHasPendingTrainingWeightsRecipeSave = value => hasPendingTrainingWeightsRecipeSave = value,
                    LastAutoAppliedTrainingWeightsPathProvider = () => lastAutoAppliedTrainingWeightsPath,
                    SetLastAutoAppliedTrainingWeightsPath = path => lastAutoAppliedTrainingWeightsPath = path ?? string.Empty,
                    EnsureProjectSettings = EnsureProjectSettings,
                    ExecuteSaveYoloSettingsCommand = ExecuteSaveYoloSettingsCommand,
                    PopulateYoloEditorFields = PopulateYoloEditorFields,
                    RefreshYoloStatus = RefreshYoloStatus,
                    UpdateYoloTrainingHistoryText = UpdateYoloTrainingHistoryText,
                    SaveModelMetadataConfigFromPanel = SaveModelMetadataConfigFromPanel,
                    SetCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    SetProjectConfigStatus = SetProjectConfigStatus,
                    SetModelStatus = SetModelStatus,
                    UpdateCommandState = UpdateYoloCommandButtons,
                    SaveYoloEditorFields = SaveYoloEditorFields,
                    SaveTrainingEditorFields = SaveTrainingEditorFields,
                    SelectFile = (title, filter, initialPath) =>
                        TryPickFile(title, filter, initialPath, out string selectedPath)
                            ? selectedPath
                            : string.Empty
                });
            modelComparisonWorkflowAdapter = new ModelComparisonWorkflowAdapter(
                new ModelComparisonWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ModelComparisonRunService = modelComparisonRunService,
                    SegmentationAdapterComparisonRunService = segmentationAdapterComparisonRunService,
                    ModelComparisonReviewService = modelComparisonReviewService,
                    ModelCenterDashboardWorkflowService = modelCenterDashboardWorkflowService,
                    TrainingWeightsService = trainingWeightsService,
                    TrainingWeightsApplicationWorkflowService = trainingWeightsApplicationWorkflowService,
                    TrainingSettingsViewModelProvider = () => viewModels.TrainingSettingsViewModel,
                    LearningWorkflowViewModelProvider = () => viewModels.LearningWorkflowViewModel,
                    CandidateReviewViewModelProvider = () => viewModels.CandidateReviewViewModel,
                    PendingTrainingBaselineWeightsPathProvider = () => pendingTrainingBaselineWeightsPath,
                    LastAutoAppliedTrainingWeightsPathProvider = () => lastAutoAppliedTrainingWeightsPath,
                    BuildCurrentTrainingWeightsComparison = () => modelCenterWorkflowAdapter.BuildCurrentTrainingWeightsComparison(),
                    EnsureProjectSettings = EnsureProjectSettings,
                    SaveTrainingEditorFields = SaveTrainingEditorFields,
                    RefreshTrainingReadinessPanel = refreshYaml => RefreshTrainingReadinessPanel(refreshYaml),
                    RefreshCommands = UpdateYoloCommandButtons,
                    RefreshModelCenterDashboard = comparison => modelCenterWorkflowAdapter.RefreshModelCenterDashboard(comparison),
                    UpdateCandidateModelDecisionPanel = comparison => modelCenterWorkflowAdapter.UpdateCandidateModelDecisionPanel(comparison),
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    SetCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    LoadYoloSettings = settings => viewModels.YoloModelSettingsViewModel?.LoadFrom(settings),
                    SetPendingTrainingBaselineWeightsPath = path => pendingTrainingBaselineWeightsPath = path ?? string.Empty,
                    SetHasPendingTrainingWeightsRecipeSave = value => hasPendingTrainingWeightsRecipeSave = value,
                    SetLastAutoAppliedTrainingWeightsPath = path => lastAutoAppliedTrainingWeightsPath = path ?? string.Empty,
                    SetModelStatus = SetModelStatus,
                    SetProjectConfigStatus = SetProjectConfigStatus,
                    SetGlobalInferenceStatus = (text, isBusy) => SetGlobalInferenceStatus(text, isBusy),
                    FocusYoloModelSettingsTab = FocusYoloModelSettingsTab,
                    FocusSaveYoloSettingsButton = () => SaveYoloSettingsButton?.Focus()
                });
            modelComparisonWorkflowService = CreateModelComparisonWorkflow();
            crashRecoveryJournalWorkflowService = new CrashRecoveryJournalWorkflowService(
                crashRecoveryJournalService,
                callback => Dispatcher.BeginInvoke(
                    new Action(callback),
                    System.Windows.Threading.DispatcherPriority.ContextIdle));
            trainingGuideHistoryWorkflowService = new TrainingGuideHistoryWorkflowService(trainingGuideHistoryService);
            trainingRuntimeWorkflowService = new TrainingRuntimeWorkflowService(
                () => applicationState.Data,
                () => applicationState.ModelRuntime.TrainingWorkflow,
                () => applicationState.ModelRuntime.PythonClientProcess,
                () => applicationState.ModelRuntime.DeepLearning,
                applicationState.GetPythonCommunicationStatusSnapshot,
                (timeoutMilliseconds, cancellationToken) => applicationState.ModelRuntime.EnsurePythonModelClientReadyAsync(timeoutMilliseconds, cancellationToken),
                GetCurrentRecipeName);
            imageQueueCatalogLoadCoordinator = new ImageQueueCatalogLoadCoordinator(
                new ImageQueueCatalogLoadService(imageQueueSelectionService));
            objectReviewWorkflowService = new ObjectReviewWorkflowService(
                new ObjectMetadataPersistenceService(),
                projectRecipeSessionService);
            InitializeComponent();
            objectReviewStateAdapter = new ObjectReviewStateAdapter(
                new ObjectReviewStateAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ObjectReviewWorkflowService = objectReviewWorkflowService,
                    ObjectMetadataStateService = objectMetadataStateService,
                    ObjectSessionStateService = objectSessionStateService,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    ObjectReviewViewModelProvider = () => ObjectReviewViewModel,
                    SetMetadataTagDefinitions = tags => ObjectReviewViewModel?.SetMetadataTagDefinitions(tags),
                    SetGroupSelectionMode = active => ObjectReviewViewModel?.SetGroupSelectionMode(active),
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog,
                    RefreshObjectListWithSelection = RefreshObjectListWithSelection,
                    MarkAnnotationsDirty = MarkAnnotationsDirty,
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    CompleteSelectedSegmentEdit = CompleteSelectedSegmentEdit,
                    CancelPendingSegmentationRemoveUnderlying = updateStatus => CancelPendingSegmentationRemoveUnderlying(updateStatus),
                    CancelPendingSegmentationSplit = updateStatus => CancelPendingSegmentationSplit(updateStatus),
                    CancelPendingSegmentationHoleEdit = updateStatus => CancelPendingSegmentationHoleEdit(updateStatus),
                    CancelPendingPolygonVertexEdit = updateStatus => CancelPendingPolygonVertexEdit(updateStatus),
                    CancelPendingIntelligentScissors = updateStatus => CancelPendingIntelligentScissors(updateStatus),
                    ClearCanvasRoiSelection = () => MainCanvasViewModel?.ClearRoiSelection(),
                    SetCanvasImagePointInputMode = value =>
                    {
                        if (MainCanvasViewModel != null)
                        {
                            MainCanvasViewModel.IsImagePointInputMode = value;
                        }
                    },
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    ConfirmObjectGroupDissolve = ConfirmObjectGroupDissolve,
                    EnsureProjectSettings = EnsureProjectSettings
                });
            objectReviewInteractionAdapter = new ObjectReviewInteractionAdapter(
                new ObjectReviewInteractionAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ObjectReviewPresentationService = objectReviewPresentationService,
                    ObjectSessionStateService = objectSessionStateService,
                    ObjectMetadataStateService = objectMetadataStateService,
                    SegmentationMergeService = segmentationMergeService,
                    CandidateReviewState = candidateReviewState,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    PendingDetectionCandidatesProvider = () => pendingDetectionCandidates,
                    ObjectReviewViewModelProvider = () => ObjectReviewViewModel,
                    MainCanvasViewModelProvider = () => MainCanvasViewModel,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    PendingSegmentationSplitOrientationProvider = () => pendingSegmentationSplitOrientation,
                    PendingSegmentationSplitSourceIndexProvider = () => pendingSegmentationSplitSourceIndex,
                    PendingSegmentationHoleEditModeProvider = () => pendingSegmentationHoleEditMode,
                    PendingSegmentationHoleSourceIndexProvider = () => pendingSegmentationHoleSourceIndex,
                    PendingSegmentationRemoveUnderlyingPlanProvider = () => pendingSegmentationRemoveUnderlyingPlan,
                    PolygonBoundaryEditWorkflowService = polygonBoundaryEditWorkflowService,
                    VisibleManualSegmentsProvider = GetVisibleManualSegments,
                    VisibleManualSegmentCountProvider = GetVisibleManualSegmentCount,
                    GetCandidateOverlapInfo = bounds => GetCandidateOverlapInfo(bounds),
                    MinimumDetectionConfidenceProvider = GetMinimumDetectionConfidence,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    RefreshObjectListWithSelection = selection => RefreshObjectListWithSelection(selection),
                    MarkAnnotationsDirty = MarkAnnotationsDirty,
                    ApplyObjectPersistentMetadata = ApplyObjectPersistentMetadata,
                    CancelObjectGroupSelection = updateStatus => CancelObjectGroupSelection(updateStatus),
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    CompleteSelectedSegmentEdit = CompleteSelectedSegmentEdit,
                    CancelPendingSegmentationRemoveUnderlying = updateStatus => CancelPendingSegmentationRemoveUnderlying(updateStatus),
                    CancelPendingSegmentationSplit = updateStatus => CancelPendingSegmentationSplit(updateStatus),
                    CancelPendingSegmentationHoleEdit = updateStatus => CancelPendingSegmentationHoleEdit(updateStatus),
                    CancelPendingPolygonVertexEdit = updateStatus => CancelPendingPolygonVertexEdit(updateStatus),
                    CancelPendingIntelligentScissors = updateStatus => CancelPendingIntelligentScissors(updateStatus),
                    ClearCanvasRoiSelection = () => MainCanvasViewModel?.ClearRoiSelection(),
                    SetCanvasImagePointInputMode = value =>
                    {
                        if (MainCanvasViewModel != null)
                        {
                            MainCanvasViewModel.IsImagePointInputMode = value;
                        }
                    },
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    CaptureAnnotationHistory = CaptureAnnotationHistory,
                    PushAnnotationHistorySnapshot = snapshot => PushAnnotationHistorySnapshot(snapshot),
                    CanMutateSelectedObject = (WpfObjectReviewItemRef item, bool requireVisible, out string error) =>
                        CanMutateSelectedObject(item, requireVisible, out error),
                    CaptureManualRoiHistory = CaptureManualRoiHistory,
                    GetObjectSessionState = GetObjectSessionState,
                    GetManualRoiSessionState = GetManualRoiSessionState,
                    GetManualSegmentSessionState = GetManualSegmentSessionState,
                    IsSegmentationDatasetPurposeActive = IsSegmentationDatasetPurposeActive,
                    ApplyManualRoiOverlayColor = (index, refreshImmediately) => ApplyManualRoiOverlayColor(index, refreshImmediately),
                    ClearMaskStrokePreview = (refresh, clearTexture) => MainCanvasViewModel?.ClearMaskStrokePreview(refresh, clearTexture),
                    QueueActiveImageQueueStatusRefresh = QueueActiveImageQueueStatusRefresh
                });
            projectSettingsEditorAdapter = new ProjectSettingsEditorAdapter(
                new ProjectSettingsEditorAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    // Resolve the lazy editor ViewModels explicitly before the
                    // panel composition callback can request them. Capturing
                    // the binding-safe shell properties here would otherwise
                    // store null before EnsureModelWorkflowPanelsComposed().
                    YoloModelSettingsViewModel = viewModels.ModelWorkflowViewModels.YoloModelSettingsViewModel,
                    TrainingSettingsViewModel = viewModels.ModelWorkflowViewModels.TrainingSettingsViewModel,
                    CandidateConfidenceSlider = CandidateConfidenceSlider,
                    EnsureProjectSettings = EnsureProjectSettings,
                    RefreshExternalYoloDatasetIntakePresentation = RefreshExternalYoloDatasetIntakePresentation,
                    UpdateCandidateConfidenceText = UpdateCandidateConfidenceText
                });
            annotationSaveStateAdapter = new AnnotationSaveStateAdapter(
                new AnnotationSaveStateAdapterContext
                {
                    DirtyState = annotationDirtyState,
                    InvalidateActiveImageQualityReviewAfterEdit = InvalidateActiveImageQualityReviewAfterEdit,
                    ApplyActiveImageQueueSaveRequiredStatus = ApplyActiveImageQueueSaveRequiredStatus,
                    RefreshActiveImageQualityReviewPresentation = RefreshActiveImageQualityReviewPresentation,
                    RefreshCanvasLayerVisibilityState = RefreshCanvasLayerVisibilityState,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    UpdateWorkflowProgressStatus = UpdateWorkflowProgressStatus,
                    ScheduleCrashRecoveryJournalWrite = () => ScheduleCrashRecoveryJournalWrite(),
                    SetQualityReviewState = (state, hasActiveImage, canMarkReviewed) =>
                        ObjectReviewViewModel?.SetQualityReviewState(state, hasActiveImage, canMarkReviewed),
                    SetStatusBarSaveStatus = (isDirty, text, toolTip) =>
                        StatusBarViewModel?.SetAnnotationSaveStatus(isDirty, text, toolTip),
                    ApplyCanvasSaveState = presentation =>
                        CanvasPanelViewModel?.ApplyAnnotationSaveStatePresentation(presentation),
                    ApplyObjectReviewSaveState = presentation =>
                        ObjectReviewViewModel?.SetLabelSaveState(
                            presentation.ObjectReviewStateKey,
                            presentation.ObjectReviewBadgeText,
                            presentation.ObjectReviewDetailText)
                });
            annotationPersistenceAdapter = new AnnotationPersistenceAdapter(
                new AnnotationPersistenceAdapterContext
                {
                    ActiveImageProvider = () => applicationState.ImageWorkspace.CaptureSnapshot(),
                    SaveTrainingEditorFields = SaveTrainingEditorFields,
                    AnnotationSaveWorkflowService = annotationSaveWorkflowService,
                    DataProvider = () => applicationState.Data,
                    TrySaveCurrentObjectMetadata = TrySaveCurrentObjectMetadata,
                    MarkAnnotationsSaved = MarkAnnotationsSaved,
                    AppendLog = AppendLog,
                    DiscardCrashRecoveryJournal = DiscardCrashRecoveryJournal,
                    UpdateApplicationData = () => applicationState.System?.UpdateData(),
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    AnnotationDirtyProvider = () => annotationDirtyState.IsDirty,
                    IsSegmentationDatasetPurposeActive = IsSegmentationDatasetPurposeActive,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualSegments = manualSegments,
                    ConfirmedDetectionCandidatesProvider = () => confirmedDetectionCandidates,
                    ClassCatalogWorkflowService = classCatalogWorkflowService
                });
            annotationLoadAdapter = new AnnotationLoadAdapter(
                new AnnotationLoadAdapterContext
                {
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    CurrentDatasetPurposeProvider = GetCurrentDatasetPurpose,
                    DataProvider = () => applicationState.Data,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshPolygonOverlays = RefreshPolygonOverlays
                });
            crashRecoverySnapshotAdapter = new CrashRecoverySnapshotAdapter(
                new CrashRecoverySnapshotAdapterContext
                {
                    SessionService = crashRecoverySessionService,
                    ApplicationVersionProvider = () => GetType().Assembly.GetName().Version?.ToString() ?? string.Empty,
                    RecipeNameProvider = GetCurrentRecipeName,
                    DatasetRootPathProvider = () => applicationState.Data.OutputRootPath,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    DirtyReasonProvider = () => annotationDirtyState.Reason,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualSegments = manualSegments,
                    ConfirmedDetectionCandidatesProvider = () => confirmedDetectionCandidates,
                    ObjectMetadataStateService = objectMetadataStateService,
                    BuildAnnotationSegments = BuildAnnotationSegments
                });
            crashRecoveryIntegrationAdapter = new CrashRecoveryIntegrationAdapter(
                new CrashRecoveryIntegrationAdapterContext
                {
                    JournalService = crashRecoveryJournalService,
                    JournalWorkflowService = crashRecoveryJournalWorkflowService,
                    SessionService = crashRecoverySessionService,
                    SnapshotAdapter = crashRecoverySnapshotAdapter,
                    DataProvider = () => applicationState.Data,
                    CurrentRecipeNameProvider = GetCurrentRecipeName,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    HasActiveImage = () => applicationState.ImageWorkspace.ActiveImage != null
                        && !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty
                        && !string.IsNullOrWhiteSpace(applicationState.ImageWorkspace.ActiveImagePath),
                    HasDirtyAnnotations = () => annotationDirtyState.IsDirty,
                    HasPendingCommitWork = HasPendingMaskStrokeCommitWork,
                    TryLoadImage = path => TryLoadImage(path, populateQueue: true),
                    ShowCrashRecoveryPrompt = ShowCrashRecoveryPromptDialog,
                    ShowRestoreFailureDialog = ShowCrashRecoveryRestoreFailureDialog,
                    AppendLog = AppendLog,
                    RunOnUiThread = action =>
                    {
                        if (Dispatcher.CheckAccess())
                        {
                            action();
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(new Action(action));
                        }
                    },
                    SetAnnotationSaveStatus = (isDirty, title, detail) =>
                        StatusBarViewModel?.SetAnnotationSaveStatus(isDirty, title, detail),
                    CaptureAnnotationHistory = CaptureAnnotationHistory,
                    ClearAnnotationHistory = ClearAnnotationHistory,
                    PushAnnotationHistorySnapshot = (snapshot, markDirty) =>
                        PushAnnotationHistorySnapshot(snapshot, markDirty),
                    MarkAnnotationsDirty = MarkAnnotationsDirty,
                    RefreshRestorePresentation = () =>
                    {
                        objectMetadataStateService.DissolveInvalidGroups(manualRois.Count, manualSegments);
                        RedrawReviewRois();
                        RefreshPolygonOverlays();
                        RefreshObjectList();
                        RefreshCandidateList();
                        PopulateClassList();
                        UpdateDetectionResultOverlay();
                        RefreshActiveImageQueueStatus(hasActiveCandidates: false);
                        SetPythonStatus("추론: 대기 0 / 확정 0");
                    },
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    SetPythonStatus = SetPythonStatus,
                    RefreshActiveImageQueueStatus = RefreshActiveImageQueueStatus,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    ObjectMetadataStateService = objectMetadataStateService,
                    CandidateReviewStateService = candidateReviewState,
                    SmartMaskPromptSessionService = smartMaskPromptSession,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments
                });
            crashRecoveryJournalWorkflowService.WriteFailed += OnCrashRecoveryJournalWriteFailed;
            crashRecoveryJournalWorkflowService.CaptureFailed += OnCrashRecoveryJournalCaptureFailed;
            yoloRuntimeStatusAdapter = new YoloRuntimeStatusAdapter(
                new YoloRuntimeStatusAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    CommunicationStatusProvider = applicationState.GetPythonCommunicationStatusSnapshot,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    PythonClientProcessRunningProvider = () => applicationState.ModelRuntime.PythonClientProcess?.IsRunning == true,
                    HasPendingTrainingWeightsRecipeSaveProvider = () => hasPendingTrainingWeightsRecipeSave,
                    EnsureProjectSettings = EnsureProjectSettings,
                    RefreshModelCenterDashboard = () => RefreshModelCenterDashboard(),
                    ApplyRuntimeCapabilities = communicationStatus => viewModels.YoloModelSettingsViewModel?.ApplyRuntimeCapabilities(
                        communicationStatus?.WorkerSupportedModels,
                        communicationStatus?.WorkerTrainingModels,
                        communicationStatus?.WorkerDetectionModels),
                    SaveProjectConfigFromPanel = () => SaveProjectConfigFromPanel(),
                    SetGlobalInferenceStatus = (text, isBusy, isWarning) => SetGlobalInferenceStatus(text, isBusy, isWarning),
                    SetPythonStatus = SetPythonStatus,
                    SetInspectionModelStatus = SetInspectionModelStatus,
                    SetModelStatus = SetModelStatus,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    SetYoloRecoveryStatus = SetYoloRecoveryStatus,
                    AppendLog = AppendLog,
                    YoloModelSettingsViewModelProvider = () => viewModels.YoloModelSettingsViewModel,
                    TrainingSettingsViewModelProvider = () => viewModels.TrainingSettingsViewModel,
                    LearningWorkflowViewModelProvider = () => viewModels.LearningWorkflowViewModel,
                    ShellViewModelProvider = () => viewModels.ShellViewModel,
                    YoloStatusViewModelProvider = () => viewModels.YoloStatusViewModel,
                    ShellTimersProvider = () => shellTimers,
                    InferenceStatusTextProvider = () => InferenceStatusText,
                    InferenceStatusBorderProvider = () => InferenceStatusBorder,
                    InferenceStatusProgressBarProvider = () => InferenceStatusProgressBar,
                    InferenceStatusIconProvider = () => InferenceStatusIcon
                });
            shellKeyboardShortcutAdapter = new ShellKeyboardShortcutAdapter(
                new ShellKeyboardShortcutAdapterContext
                {
                    CanvasPanelViewModel = viewModels.CanvasPanelViewModel,
                    CancelFourPointBoxDraft = () => CancelFourPointBoxDraft(updateStatus: true),
                    RemoveLastFourPointBoxPoint = RemoveLastFourPointBoxPoint,
                    UndoAnnotationHistory = UndoWpfAnnotationHistory,
                    RedoAnnotationHistory = RedoWpfAnnotationHistory,
                    TryDuplicateSelectedAnnotation = TryDuplicateSelectedAnnotation,
                    ResolveSelectableAnnotationTool = ResolveSelectableAnnotationTool,
                    ApplyAnnotationToolSelection = ApplyAnnotationToolSelection,
                    ShowClassCatalogWorkflowView = ShowClassCatalogWorkflowView,
                    SetModelStatus = SetModelStatus,
                    TryOpenAdjacentQueueImage = TryOpenAdjacentQueueImage
                });
            auxiliaryWindowHost = new ShellAuxiliaryWindowHost(
                this,
                RuntimeDiagnosticsViewModel.DiagnosticsService,
                () => applicationState.Data.ProjectSettings?.PythonModel ?? new PythonModelSettings(),
                () =>
                {
                    FocusYoloModelSettingsTab();
                    if (WindowState == WindowState.Minimized)
                    {
                        WindowState = WindowState.Normal;
                    }

                    Activate();
                },
                modelCenterWorkflowAdapter.ResolveModelComparisonSummaryPath);
            datasetTransferWindowHost = new DatasetTransferWindowHost(
                this,
                fileDialogService,
                () => applicationState.Data,
                GetCurrentRecipeName,
                () => applicationState.Data?.OutputRootPath,
                ExecuteOpenDatasetHealthImageInEditor);
            patchCoreHeatmapWindowHost = new PatchCoreHeatmapWindowHost(
                this,
                () => viewModels.CandidateReviewViewModel?.ClosePatchCoreHeatmap());
            canvasWorkflowContextPresenter = new CanvasWorkflowContextPresenter(
                CanvasPanelViewModel,
                LearningWorkflowViewModel,
                () => RefreshSmartMaskCommandState(),
                CaptureCanvasWorkflowContext);
            canvasAnnotationToolScopePresenter = new CanvasAnnotationToolScopePresenter(
                ShellViewModel,
                CanvasPanelViewModel,
                ImageQueueViewModel,
                LearningWorkflowViewModel,
                IsAnomalyDatasetPurpose,
                RefreshImageQueuePurposePresentation,
                () => ShellViewModel?.IsLabelingStageActive == true,
                () =>
                {
                    FocusAnnotationToolsTab();
                },
                ExecuteCanvasAnnotationToolSelectionChanged);
            candidateReviewViewerNavigator = new CandidateReviewViewerNavigator(
                MainCanvasViewModel,
                () => applicationState.ImageWorkspace.ActiveImageSize,
                GetSelectedCandidate,
                GetCandidateOverlapInfo,
                ApplyCanvasDisplayMode,
                RefreshObjectListWithSelection,
                ShowSavedLabelsWorkflowView,
                SetModelStatus,
                AppendLog);
            displayWorkspaceAdapter = new CanvasDisplayWorkspaceAdapter(
                Dispatcher,
                () => shellTimers.DisplayAdjustmentRefresh,
                () => isApplicationCloseApproved,
                () => applicationState.ImageWorkspace.ActiveImage,
                () => applicationState.ImageWorkspace.ActiveImageSize,
                () => applicationState.ImageWorkspace.ActiveImagePath,
                CanvasPanelViewModel,
                MainCanvasViewModel,
                MainCanvasView,
                new ImageDisplayAdjustmentService());
            statusPresentationAdapter = new ShellStatusPresentationAdapter(
                StatusBarViewModel,
                () => new ShellWorkflowStatusContext(
                    isInferenceMode: IsInferenceWorkflowActive,
                    totalImageCount: imageQueueItems?.Count ?? 0,
                    completedImageCount: imageQueueItems?.Count(ImageQueueFilterService.IsCompletedQueueItem) ?? 0,
                    hasPendingCandidates: pendingDetectionCandidates.Count > 0,
                    hasUnsavedAnnotationChanges: annotationDirtyState.IsDirty,
                    isTrainingReady: lastYoloTrainingReadinessReport?.IsReady == true,
                    hasActiveImage: applicationState.ImageWorkspace.ActiveImage != null && !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty),
                RefreshShellDatasetContext);
            projectSettingsWorkflowAdapter = new ProjectSettingsWorkflowAdapter(
                new ProjectSettingsWorkflowAdapterContext
                {
                    ApplicationState = applicationState,
                    DataProvider = () => applicationState.Data,
                    ProjectConfigViewModel = viewModels.ProjectConfigViewModel,
                    ProjectRecipeSessionService = projectRecipeSessionService,
                    ProjectRecipeApplyWorkflowService = projectRecipeApplyWorkflowService,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    BuildApplicationCloseState = BuildApplicationCloseState,
                    DefaultDatasetParentDirectory = datasetTransferWindowHost.ResolveProjectArchiveDatasetParent,
                    SelectSaveFile = (title, filter, currentPath) =>
                        fileDialogService.TryPickSaveFile(
                            this,
                            title,
                            filter,
                            currentPath,
                            ".zip",
                            out string selectedPath)
                            ? selectedPath
                            : string.Empty,
                    SelectFile = (title, filter, currentPath) =>
                        fileDialogService.TryPickFile(this, title, filter, currentPath, out string selectedPath)
                            ? selectedPath
                            : string.Empty,
                    SelectFolder = (title, currentPath) =>
                        fileDialogService.TryPickFolder(this, title, currentPath, out string selectedPath)
                            ? selectedPath
                            : string.Empty,
                    SetProjectConfigStatus = message => viewModels.ProjectConfigViewModel.StatusText = message ?? string.Empty,
                    SetDatasetStatus = SetDatasetStatus,
                    AppendLog = AppendLog,
                    UpdateYoloCommandButtons = UpdateYoloCommandButtons,
                    CancelFourPointBoxDraft = () => CancelFourPointBoxDraft(updateStatus: false),
                    RememberLastOpenedDatasetRecipe = RememberLastOpenedDatasetRecipe,
                    ApplyProjectDatasetPurposeToWorkflow = ApplyProjectDatasetPurposeToWorkflow,
                    PopulateYoloEditorFields = PopulateYoloEditorFields,
                    PopulateTrainingEditorFields = PopulateTrainingEditorFields,
                    PopulateClassList = () => PopulateClassList(),
                    RestoreObjectMetadataTagsFromProject = RestoreObjectMetadataTagsFromProject,
                    RefreshCandidateList = RefreshCandidateList,
                    RefreshObjectList = RefreshObjectList,
                    RefreshTrainingReadinessPanel = refreshYaml => RefreshTrainingReadinessPanel(refreshYaml),
                    OpenFolder = directoryPath => Process.Start(new ProcessStartInfo
                     {
                         FileName = directoryPath,
                         UseShellExecute = true
                     })
                 });
            datasetSetupWorkflowAdapter = new DatasetSetupWorkflowAdapter(
                datasetSetupPathService,
                datasetSetupExecutionService,
                datasetSetupPresentationService,
                projectRecipeSessionService,
                projectRecipeApplyWorkflowService,
                datasetImageRootResolver,
                imageQueueSelectionService,
                new DatasetSetupWorkflowAdapterContext
                {
                    ApplicationState = applicationState,
                    DataProvider = () => applicationState.Data,
                    ProjectSettingsWorkflowAdapter = projectSettingsWorkflowAdapter,
                    ProjectConfigViewModel = viewModels.ProjectConfigViewModel,
                    LearningWorkflowViewModel = viewModels.LearningWorkflowViewModel,
                    ShellViewModel = viewModels.ShellViewModel,
                    SelectedPurposeProvider = () => DatasetPurposeListBox?.SelectedItem,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    CreateWizardWindow = wizardViewModel => new WpfDatasetSetupWizardWindow
                    {
                        Owner = this,
                        DataContext = wizardViewModel
                    },
                    CreateSelectionWindow = selectionViewModel => new WpfDatasetSelectionWindow
                    {
                        Owner = this,
                        DataContext = selectionViewModel
                    },
                    LoadImageQueueFromRootAsync = (imageRoot, selectedImagePath, loadFirstImage, refreshDetails) =>
                        LoadImageQueueFromRootAsync(imageRoot, selectedImagePath, loadFirstImage, refreshDetails),
                    CurrentImageRootProvider = () => ImageQueueViewModel?.CurrentImageFolderPath ?? string.Empty,
                    SetCurrentImageRoot = path => ImageQueueViewModel?.SetCurrentImageFolder(
                        path,
                        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)),
                    SetCurrentImageFolder = (path, canOpenFolder) => ImageQueueViewModel?.SetCurrentImageFolder(path, canOpenFolder),
                    CancelImageQueueLoads = () =>
                    {
                        CancelImageQueueCatalogLoad(waitForCompletion: false);
                        CancelImageQueueDetailRefresh(waitForCompletion: false);
                        batchDetectionWorkflowService.Cancel();
                        imageQualityReviewWorkflowService.Reset();
                        anomalyImageReviewSession.Reset();
                    },
                    ClearAnomalyFolderStateSuggestion = () => ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion(),
                    ClearImageQueueItems = () =>
                    {
                        suppressImageQueueSelection = true;
                        try
                        {
                            imageQueueItems.Clear();
                            imageQueueItemsByPath.Clear();
                            ImageQueuePanelControl?.RefreshQueueView();
                        }
                        finally
                        {
                            suppressImageQueueSelection = false;
                        }
                    },
                    UpdateImageQueueStatusText = () => UpdateImageQueueStatusText(),
                    ClearActiveImageAfterQueueReset = ClearActiveImageAfterQueueReset,
                    SetDatasetStatus = SetDatasetStatus,
                    SetProjectConfigStatus = message => viewModels.ProjectConfigViewModel.StatusText = message ?? string.Empty,
                    AppendLog = AppendLog,
                    OpenFolder = directoryPath => Process.Start(new ProcessStartInfo
                    {
                        FileName = directoryPath,
                        UseShellExecute = true
                    }),
                    FocusDatasetOnboardingTab = FocusDatasetOnboardingTab,
                    EnsureProjectSettings = EnsureProjectSettings,
                    ApplyPersistedDatasetPurposeToCurrentProject = ApplyPersistedDatasetPurposeToCurrentProject,
                    RememberLastOpenedDatasetRecipe = RememberLastOpenedDatasetRecipe,
                    PopulateProjectConfigPanelFields = PopulateProjectConfigPanelFields,
                    PopulateClassList = PopulateClassList,
                    PopulateYoloEditorFields = PopulateYoloEditorFields,
                    PopulateTrainingEditorFields = PopulateTrainingEditorFields,
                    RefreshTrainingReadinessPanel = RefreshTrainingReadinessPanel,
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    EnterLabelingWorkbenchStartView = EnterLabelingWorkbenchStartView,
                    GetCurrentDatasetPurpose = GetCurrentDatasetPurpose,
                    CompleteProjectRecipeApply = CompleteProjectRecipeApply
                });
            externalYoloDatasetIntakeAdapter = new ExternalYoloDatasetIntakeAdapter(
                new ExternalYoloDatasetIntakeAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ProjectRecipeSessionService = projectRecipeSessionService,
                    CurrentRecipeNameProvider = GetCurrentRecipeName,
                    SettingsProvider = () => applicationState.Data?.ProjectSettings?.ExternalYoloDataset,
                    SelectedPurposeProvider = () => LearningWorkflowViewModel?.GetSelectedExternalYoloDatasetPurpose()
                        ?? LabelingDatasetPurpose.ObjectDetection,
                    SelectDataYamlPath = currentPath => TryPickFile(
                        "외부 YOLO data.yaml 선택",
                        "YOLO data.yaml (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*",
                        currentPath,
                        out string selectedPath)
                        ? selectedPath
                        : string.Empty,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    SetPresentation = (purpose, statusText, detailText, pathText) =>
                        LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                            purpose,
                            statusText,
                            detailText,
                            pathText),
                    PopulateTrainingEditorFields = PopulateTrainingEditorFields,
                    RefreshTrainingReadinessPanel = RefreshTrainingReadinessPanel,
                    RefreshExternalTrainingReadinessPanel = RefreshExternalTrainingReadinessPanel,
                    AppendLog = AppendLog
                });
            candidateReviewStateAdapter = new CandidateReviewStateAdapter(
                new CandidateReviewStateAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    CanvasPanelViewModel = CanvasPanelViewModel,
                    CandidateReviewViewModel = CandidateReviewViewModel,
                    CandidateReviewState = candidateReviewState,
                    CandidateReviewPresentationService = candidateReviewPresentationService,
                    CandidateReviewCompletionPresentationService = candidateReviewCompletionPresentationService,
                    ImageDetectionWorkflowService = imageDetectionWorkflowService,
                    PatchCoreHeatmapReviewService = patchCoreHeatmapReviewService,
                    PatchCoreHeatmapWindowHost = patchCoreHeatmapWindowHost,
                    AnnotationDirtyState = annotationDirtyState,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    ManualRois = manualRois,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    GetManualRoiClassName = GetManualRoiClassName,
                    GetCanvasLabelObjectCount = GetCanvasLabelObjectCount,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsModelWorkflowCreated = () => viewModels.IsModelWorkflowCreated,
                    IsTopmostProvider = () => Topmost,
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    RedrawReviewRois = RedrawReviewRois,
                    SetModelStatus = SetModelStatus,
                    UpdateWorkflowProgressStatus = UpdateWorkflowProgressStatus,
                    CandidateConfidenceSlider = CandidateConfidenceSlider,
                    CandidateListBox = CandidateListBox,
                    FitCanvasButton = FitCanvasButton,
                    ActualSizeCanvasButton = ActualSizeCanvasButton,
                    PanCanvasButton = PanCanvasButton,
                    FocusCandidateCanvasButton = FocusCandidateCanvasButton,
                    ResetAiOverlayCanvasButton = ResetAiOverlayCanvasButton
                });
            imageQueueReviewAdapter = new ImageQueueReviewAdapter(
                new ImageQueueReviewAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    ImageQueueSelectionService = imageQueueSelectionService,
                    ImageQueueItems = imageQueueItems,
                    ImageQueueItemsByPath = imageQueueItemsByPath,
                    ImageQueueViewProvider = () => imageQueueView,
                    ImageQueueViewModel = ImageQueueViewModel,
                    ObjectReviewViewModel = ObjectReviewViewModel,
                    ImageQueueFilterBox = ImageQueueFilterBox,
                    ImageQueueSearchBox = ImageQueueSearchBox,
                    ImageQueueGrid = ImageQueueGrid,
                    ImageQueuePanelControl = ImageQueuePanelControl,
                    AnnotationDirtyState = annotationDirtyState,
                    PendingDetectionCandidatesProvider = () => pendingDetectionCandidates,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsImageQueueSelectionSuppressed = () => suppressImageQueueSelection,
                    SetImageQueueSelectionSuppressed = value => suppressImageQueueSelection = value,
                    Dispatcher = Dispatcher,
                    AppendLog = AppendLog,
                    SetDatasetStatus = SetDatasetStatus,
                    SetModelStatus = SetModelStatus,
                    RefreshImageQueueViewAfterItemStateChange = RefreshImageQueueViewAfterItemStateChange,
                    TryOpenNextIncompleteQueueImage = () => TryOpenNextIncompleteQueueImage(),
                    CanOpenQueueItem = item => imageQueueNavigationAdapter?.CanOpenQueueItem(item) == true,
                    SetOpenSelectedImageEnabled = canOpen => SetControlEnabled(OpenSelectedQueueImageButton, canOpen),
                    OpenSelectedQueueImage = (item, skipIfAlreadyActive) => imageQueueNavigationAdapter?.TryOpenSelectedQueueImage(item, skipIfAlreadyActive),
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion()
                });
            imageQueueDetailRefreshAdapter = new ImageQueueDetailRefreshAdapter(
                new ImageQueueDetailRefreshAdapterContext
                {
                    RefreshService = new ImageQueueDetailRefreshService(),
                    ReviewWorkflow = imageQualityReviewWorkflowService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsAnomalyDatasetPurpose = IsAnomalyDatasetPurpose,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    QueueItemCountProvider = () => imageQueueItems.Count,
                    ApplyStandardReviewStatus = (item, status) => imageQueueReviewAdapter.ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion: false),
                    AppendLog = AppendLog,
                    SetDatasetStatus = SetDatasetStatus,
                    UpdateImageQueueStatusText = () => UpdateImageQueueStatusText(),
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    ImageQueuePanelControl = ImageQueuePanelControl,
                    Dispatcher = Dispatcher
                });
            imageQueueCatalogProjectionAdapter = new ImageQueueCatalogProjectionAdapter(
                new ImageQueueCatalogProjectionAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    ImageQueueSelectionService = imageQueueSelectionService,
                    ImageQueueItems = imageQueueItems,
                    ImageQueueItemsByPath = imageQueueItemsByPath,
                    ImageQueueViewModel = ImageQueueViewModel,
                    IsCurrentCatalogRequest = IsCurrentImageQueueCatalogLoad,
                    IsAnomalyDatasetPurpose = IsAnomalyDatasetPurpose,
                    SetImageQueueSelectionSuppressed = value => suppressImageQueueSelection = value,
                    SelectImageQueueItem = SelectImageQueueItem,
                    RefreshImageQueueView = () => ImageQueuePanelControl?.RefreshQueueView(),
                    UpdateImageQueueStatusText = () => UpdateImageQueueStatusText(),
                    BeginDetailRefresh = (paths, lookup, data) => imageQueueDetailRefreshAdapter.Begin(paths, lookup, data),
                    TryLoadImage = path => TryLoadImage(path),
                    ClearActiveImageAfterQueueReset = ClearActiveImageAfterQueueReset,
                    SetDatasetStatus = SetDatasetStatus,
                    AppendLog = AppendLog
                });
            imageQueueCatalogLoadAdapter = new ImageQueueCatalogLoadAdapter(
                new ImageQueueCatalogLoadAdapterContext
                {
                    Coordinator = imageQueueCatalogLoadCoordinator,
                    Projection = imageQueueCatalogProjectionAdapter,
                    DataProvider = () => applicationState.Data,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    BatchDetectionWorkflowService = batchDetectionWorkflowService,
                    ImageQueueViewModel = ImageQueueViewModel,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsAnomalyDatasetPurpose = IsAnomalyDatasetPurpose,
                    SetCurrentImageRoot = path => ImageQueueViewModel?.SetCurrentImageFolder(
                        path,
                        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)),
                    CancelImageQueueDetailRefresh = () => imageQueueDetailRefreshAdapter.Cancel(waitForCompletion: false),
                    SetDatasetStatus = SetDatasetStatus,
                    AppendLog = AppendLog
                });
            imageQueueRootCommandAdapter = new ImageQueueRootCommandAdapter(
                new ImageQueueRootCommandAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ProjectRecipeSessionService = projectRecipeSessionService,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    EnsureProjectSettings = EnsureProjectSettings,
                    ResolveConfiguredImageRootPath = projectSettingsWorkflowAdapter.ResolveConfiguredImageRootPath,
                    IsModelWorkflowCreated = () => viewModels.IsModelWorkflowCreated,
                    ModelImageRootPath = () => viewModels.YoloModelSettingsViewModel?.ImageRootPath,
                    SetConfiguredImageRootPath = projectSettingsWorkflowAdapter.SetConfiguredImageRootPath,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    CurrentImageRootProvider = () => ImageQueueViewModel?.CurrentImageFolderPath ?? string.Empty,
                    SelectFolder = (title, currentPath) => TryPickFolder(title, currentPath, out string selectedPath)
                        ? selectedPath
                        : string.Empty,
                    LoadImageQueueAsync = (imageRoot, selectedImagePath, loadFirstImage) =>
                        LoadImageQueueFromRootAsync(imageRoot, selectedImagePath, loadFirstImage),
                    QueueItemCountProvider = () => imageQueueItems.Count,
                    RefreshShellDatasetContext = RefreshShellDatasetContext,
                    SetCurrentImageFolder = (path, canOpenFolder) => ImageQueueViewModel?.SetCurrentImageFolder(path, canOpenFolder),
                    OpenFolder = directoryPath => Process.Start(new ProcessStartInfo
                    {
                        FileName = directoryPath,
                        UseShellExecute = true
                    }),
                    CurrentRecipeNameProvider = GetCurrentRecipeName,
                    PopulateYoloEditorFields = PopulateYoloEditorFields,
                    PopulateProjectConfigPanelFields = PopulateProjectConfigPanelFields,
                    AppendLog = AppendLog
                });
            imageQueueNavigationAdapter = new ImageQueueNavigationAdapter(
                new ImageQueueNavigationAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    ImageQueueSelectionService = imageQueueSelectionService,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsAnomalyDatasetPurpose = IsAnomalyDatasetPurpose,
                    QueueItemsProvider = () => imageQueueItems.ToList(),
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    SelectedQueueItemProvider = () => ImageQueueViewModel?.SelectedQueueItem,
                    VisibleQueueItemsProvider = GetVisibleQueueItems,
                    OpenSelectionProvider = () => imageQueueReviewAdapter.GetOpenSelectedQueueSelection(),
                    NavigationLoadOverrideProvider = () => imageQueueNavigationLoadOverride,
                    LoadAnomalyImage = path => TryLoadImage(
                        path,
                        populateQueue: false,
                        refreshQueueDetails: false,
                        refreshActiveStatus: false,
                        appendLoadLog: false),
                    LoadNextImage = path => TryLoadImage(path),
                    LoadSelectedImage = path => TryLoadImage(
                        path,
                        populateQueue: false,
                        refreshQueueDetails: false,
                        refreshActiveStatus: false,
                        appendLoadLog: false),
                    SelectImageQueueItem = SelectImageQueueItem,
                    UpdateSelectedQueueImageButton = item => imageQueueReviewAdapter.UpdateSelectedQueueImageButton(item),
                    BuildOpenQueueSelectionFailureMessage = () => imageQueueReviewAdapter.BuildOpenQueueSelectionFailureMessage(),
                    AppendLog = AppendLog
                });
            annotationSegmentEditAdapter = new AnnotationSegmentEditAdapter(
                new AnnotationSegmentEditAdapterContext
                {
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    SelectedObjectReviewItemProvider = () => TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                        ? item
                        : null,
                    ManualSegments = manualSegments,
                    CanEditManualSegment = CanEditManualSegment,
                    MaskAnnotationService = maskAnnotationService,
                    ObjectSessionStateService = objectSessionStateService,
                    CaptureAnnotationHistory = CaptureAnnotationHistory,
                    PushAnnotationHistorySnapshot = PushAnnotationHistorySnapshot,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    MarkAnnotationsDirty = MarkAnnotationsDirty,
                    RefreshObjectList = RefreshObjectList,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    RefreshActiveImageQueueStatus = RefreshActiveImageQueueStatus
                });
            annotationProductivityAdapter = new AnnotationProductivityAdapter(
                new AnnotationProductivityAdapterContext
                {
                    SelectedObjectReviewItemProvider = () => TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                        ? item
                        : null,
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    MaskAnnotationService = maskAnnotationService,
                    MutationErrorProvider = (item, requireVisible) => CanMutateSelectedObject(
                        item,
                        requireVisible,
                        out string error)
                        ? string.Empty
                        : error,
                    RegisterAnnotationHistoryBeforeChange = action => RegisterAnnotationHistoryBeforeChange(action),
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshObjectListWithSelection = RefreshObjectListWithSelection,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    ShowSavedLabelsWorkflowView = ShowSavedLabelsWorkflowView,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    QueueActiveImageQueueStatusRefresh = QueueActiveImageQueueStatusRefresh,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                     AppendLog = AppendLog
                 });
            fourPointBoxInputAdapter = new FourPointBoxInputAdapter(
                fourPointBoxService,
                new FourPointBoxInputAdapterContext
                {
                    IsInputActive = IsFourPointBoxInputActive,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    SelectedClassNameProvider = GetSelectedClassName,
                    AddCompletedRectangle = (bounds, className) => MainCanvasViewModel?.AddCompletedImageRectangle(bounds, className) != null,
                    SetProgress = count => CanvasPanelViewModel?.SetFourPointBoxProgress(count),
                    RefreshOverlays = RefreshPolygonOverlays,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy)
                });
            annotationToolSettingsAdapter = new AnnotationToolSettingsAdapter(
                new AnnotationToolSettingsAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    SelectedBoxDrawingMethodProvider = () =>
                        CanvasPanelViewModel?.SelectedBoxDrawingMethod?.Method
                            ?? LabelingBoxDrawingMethod.TwoPointDrag,
                    FourPointBoxPointCountProvider = () => fourPointBoxService.PointCount,
                    IsMainCanvasAvailable = () => MainCanvasViewModel != null,
                    SetCanvasTeachingMode = value =>
                    {
                        if (MainCanvasViewModel != null)
                        {
                            MainCanvasViewModel.IsTeachingMode = value;
                        }
                    },
                    SetCanvasImagePointInputMode = value =>
                    {
                        if (MainCanvasViewModel != null)
                        {
                            MainCanvasViewModel.IsImagePointInputMode = value;
                        }
                    },
                    SetCanvasInteractionMode = mode => MainCanvasViewModel?.ImageViewer?.SetViewMode(mode),
                    SetFourPointBoxProgress = count => CanvasPanelViewModel?.SetFourPointBoxProgress(count),
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    CancelFourPointBoxDraft = updateStatus => fourPointBoxInputAdapter.CancelDraft(updateStatus),
                    RestoreBoxDrawingMethod = method => CanvasPanelViewModel?.RestoreBoxDrawingMethod(method),
                    EnsureProjectSettings = EnsureProjectSettings,
                    CurrentRecipeNameProvider = GetCurrentRecipeName,
                    SaveCurrentRecipeConfiguration = projectSettingsWorkflowAdapter.SaveCurrentRecipeConfiguration,
                    SelectAnnotationTool = tool => SelectAnnotationTool(tool),
                    RefreshSmartMaskCommandState = RefreshSmartMaskCommandState,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog
                });
            segmentationZOrderCommandAdapter = new SegmentationZOrderCommandAdapter(
                segmentationZOrderService,
                new SegmentationZOrderCommandAdapterContext
                {
                    SelectedObjectReviewItemProvider = () => TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                        ? item
                        : null,
                    ManualSegments = manualSegments,
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    MutationErrorProvider = (item, requireVisible) => CanMutateSelectedObject(
                        item,
                        requireVisible,
                        out string error)
                        ? string.Empty
                        : error,
                    CaptureAnnotationHistory = CaptureAnnotationHistory,
                    PushAnnotationHistorySnapshot = snapshot => PushAnnotationHistorySnapshot(snapshot),
                    ClearMaskStrokePreview = () => MainCanvasViewModel?.ClearMaskStrokePreview(
                        refresh: false,
                        clearTexture: true),
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    RefreshObjectListWithSelection = RefreshObjectListWithSelection,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    QueueActiveImageQueueStatusRefresh = QueueActiveImageQueueStatusRefresh,
                    RefreshObjectReviewActionState = () => ObjectReviewViewModel?.RefreshActionState(),
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog
                });
            polygonBoundaryEditAdapter = new PolygonBoundaryEditAdapter(
                polygonBoundaryEditWorkflowService,
                new PolygonBoundaryEditAdapterContext
                {
                    ManualSegments = manualSegments,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    SelectedObjectReviewItemProvider = () => TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                        ? item
                        : null,
                    MutationErrorProvider = (item, requireVisible) => CanMutateSelectedObject(
                        item,
                        requireVisible,
                        out string error)
                        ? string.Empty
                        : error,
                    IsSelectedManualSegment = () => ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true,
                    HasSmartMaskSession = () => smartMaskPromptSession.HasSession,
                    IsSmartMaskRunning = () => smartMaskWorkflowService.IsRunning,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    CanvasPanelViewModel = CanvasPanelViewModel,
                    MainCanvasViewModel = MainCanvasViewModel,
                    CancelPendingSegmentationRemoveUnderlying = CancelPendingSegmentationRemoveUnderlying,
                    CancelPendingSegmentationSplit = CancelPendingSegmentationSplit,
                    CancelPendingSegmentationHoleEdit = CancelPendingSegmentationHoleEdit,
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    RefreshObjectListWithSelection = RefreshObjectListWithSelection,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    CaptureAnnotationHistory = CaptureAnnotationHistory,
                    PushAnnotationHistorySnapshot = (snapshot, markDirty) => PushAnnotationHistorySnapshot(snapshot, markDirty),
                    MarkAnnotationsDirty = MarkAnnotationsDirty,
                    SelectAnnotationTool = tool => SelectAnnotationTool(tool),
                    SetIntelligentScissorsState = (pending, hasPreview, statusText) => ObjectReviewViewModel?.SetIntelligentScissorsState(
                        pending,
                        hasPreview,
                        statusText),
                    SetVertexEditPending = mode => ObjectReviewViewModel?.SetVertexEditPending(mode),
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog
                });
            annotationToolSelectionAdapter = new AnnotationToolSelectionAdapter(
                new AnnotationToolSelectionAdapterContext
                {
                    LearningWorkflowViewModel = viewModels.LearningWorkflowViewModel,
                    CanvasPanelViewModel = viewModels.CanvasPanelViewModel,
                    MainCanvasViewModel = viewModels.MainCanvasViewModel,
                    CurrentDatasetPurposeProvider = GetCurrentDatasetPurpose,
                    HasPendingSegmentationRemoveUnderlyingPlan = () => pendingSegmentationRemoveUnderlyingPlan != null,
                    IsPolygonVertexEditPending = () => polygonBoundaryEditWorkflowService.IsPolygonVertexEditPending,
                    IsIntelligentScissorsPending = () => polygonBoundaryEditWorkflowService.IsIntelligentScissorsPending,
                    IsManualSegmentSelected = () => ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true,
                    CancelFourPointBoxDraft = () => CancelFourPointBoxDraft(updateStatus: false),
                    CancelPendingSegmentationRemoveUnderlying = () => CancelPendingSegmentationRemoveUnderlying(updateStatus: false),
                    CancelPendingSegmentationSplit = () =>
                    {
                        if (pendingSegmentationSplitOrientation.HasValue)
                        {
                            CancelPendingSegmentationSplit(updateStatus: false);
                        }
                    },
                    CancelPendingSegmentationHoleEdit = () =>
                    {
                        if (pendingSegmentationHoleEditMode.HasValue)
                        {
                            CancelPendingSegmentationHoleEdit(updateStatus: false);
                        }
                    },
                    CancelPendingPolygonVertexEdit = () => CancelPendingPolygonVertexEdit(updateStatus: false),
                    CancelPendingIntelligentScissors = () => CancelPendingIntelligentScissors(updateStatus: false),
                    EndPolygonAnnotationMode = clearDraft => EndPolygonAnnotationMode(clearDraft),
                    EndMaskAnnotationMode = EndMaskAnnotationMode,
                    FocusAnnotationToolsTab = FocusAnnotationToolsTab,
                    SetActiveAnnotationTool = tool => activeAnnotationTool = tool,
                    SetLabelingWorkflowMode = () => SetWorkflowMode(WorkflowMode.Labeling),
                    FocusLabelingSidePanelForTool = FocusLabelingSidePanelForTool,
                    ApplyRectangleDrawingInputMode = ApplyRectangleDrawingInputMode,
                    BeginPolygonAnnotationMode = BeginPolygonAnnotationMode,
                    BeginMaskAnnotationMode = BeginMaskAnnotationMode,
                    ExecutePanCanvasCommand = ExecutePanCanvasCommand,
                    ExecuteDeleteObjectCommand = ExecuteDeleteObjectCommand,
                    UndoAnnotationHistory = () => _ = UndoWpfAnnotationHistory(),
                    RedoAnnotationHistory = () => _ = RedoWpfAnnotationHistory(),
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    SetModelStatus = SetModelStatus,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog,
                    ShowAnnotationToolPalette = () =>
                    {
                        LearningWorkflowViewModel?.ShowLabelingTask();
                        LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
                    }
                });
            smartMaskWorkflowAdapter = new SmartMaskWorkflowAdapter(
                smartMaskWorkflowService,
                smartMaskPromptSession,
                new SmartMaskWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    RecipeNameProvider = GetCurrentRecipeName,
                    CandidateReviewState = candidateReviewState,
                    CanvasPanelViewModel = CanvasPanelViewModel,
                    MainCanvasViewModel = MainCanvasViewModel,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    GetManualRoiClassName = GetManualRoiClassName,
                    EnsureManualRoiMetadataCount = EnsureManualRoiMetadataCount,
                    ApplyDetectionCandidatesPreservingConfirmed = (candidates, succeeded) =>
                        ApplyDetectionCandidatesPreservingConfirmed(candidates, succeeded),
                    RegisterAnnotationHistoryBeforeChange = (actionName, markDirty) =>
                        RegisterAnnotationHistoryBeforeChange(actionName, markDirty),
                    SelectAnnotationTool = (tool, reveal) => SelectAnnotationTool(tool, reveal),
                    ApplyCanvasDisplayMode = (mode, redraw, logChange) =>
                        ApplyCanvasDisplayMode(mode, redraw, logChange),
                    RefreshCandidateListWithPreferred = RefreshCandidateListWithPreferred,
                    RefreshObjectList = RefreshObjectList,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    AddCandidateReviewHistory = AddCandidateReviewHistory,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog
                });
            candidateReviewActionAdapter = new CandidateReviewActionAdapter(
                new CandidateReviewActionAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    CandidateReviewState = candidateReviewState,
                    CandidateConfirmationService = candidateConfirmationService,
                    SmartMaskPromptSession = smartMaskPromptSession,
                    CandidateReviewViewModel = CandidateReviewViewModel,
                    CanvasPanelViewModel = CanvasPanelViewModel,
                    MainCanvasViewModel = MainCanvasViewModel,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    GetSelectedCandidate = GetSelectedCandidate,
                    GetVisibleCandidateList = GetVisibleCandidateList,
                    IsCandidateConfirmable = IsCandidateConfirmable,
                    IsCandidateHighOverlap = IsCandidateHighOverlap,
                    AppendLog = AppendLog,
                    AddCandidateReviewHistory = AddCandidateReviewHistory,
                    SetPythonStatus = SetPythonStatus,
                    SetModelStatus = SetModelStatus,
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    ShowSavedLabelsWorkflowView = ShowSavedLabelsWorkflowView,
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshCandidateListWithPreferred = RefreshCandidateListWithPreferred,
                    RefreshObjectList = RefreshObjectList,
                    PopulateClassList = () => PopulateClassList(),
                    SyncObjectClassEditorToSelection = SyncObjectClassEditorToSelection,
                    RefreshSmartMaskCommandState = () => RefreshSmartMaskCommandState(),
                    MarkActiveImageConfirmed = MarkActiveImageConfirmed,
                    MarkActiveImageNoCandidate = MarkActiveImageNoCandidate,
                    MarkActiveImageSkippedOrCandidate = MarkActiveImageSkippedOrCandidate,
                    ContinueAutoSmartMaskAfterResolvedCandidate = ContinueAutoSmartMaskAfterResolvedCandidate,
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    RegisterAnnotationHistoryBeforeChange = (actionName, markDirty) => RegisterAnnotationHistoryBeforeChange(actionName, markDirty),
                    ApplyCanvasDisplayMode = (mode, redraw, logChange) => ApplyCanvasDisplayMode(mode, redraw, logChange),
                    UpdateCandidateActionState = UpdateCandidateActionState,
                    ApplyCandidateSelectionReview = ApplyCandidateSelectionReview,
                    UpdateDetectionResultOverlay = UpdateDetectionResultOverlay,
                    FocusCandidateInViewer = (candidate, logIfMissing) => FocusCandidateInViewer(candidate, logIfMissing),
                    FocusSelectedCandidateInViewer = logIfMissing => FocusSelectedCandidateInViewer(logIfMissing),
                    HasCanvasLabelObjects = HasCanvasLabelObjects,
                    SaveCurrentAnnotations = () =>
                    {
                        bool succeeded = SaveCurrentAnnotations(out int savedCount);
                        return new AnnotationSaveOutcome(succeeded, savedCount);
                    },
                    SaveCurrentEmptyAnnotations = SaveCurrentEmptyAnnotations,
                    TryOpenNextIncompleteQueueImageWithoutPath = () => TryOpenNextIncompleteQueueImage(),
                    FinishQueueCompletionAndGuideDatasetCheck = FinishQueueCompletionAndGuideDatasetCheck,
                    BuildLabelPathSummary = BuildLabelPathSummary,
                    BuildCandidateFocusRect = BuildCandidateFocusRect,
                    FocusCandidateListItem = item =>
                    {
                        CandidateListBox?.ScrollIntoView(item);
                        CandidateListBox?.Focus();
                    },
                    CanOpenModelComparisonImage = path => File.Exists(path),
                    TryLoadModelComparisonImage = path => TryLoadImage(
                        path,
                        populateQueue: true,
                        refreshQueueDetails: true,
                        refreshActiveStatus: true,
                        appendLoadLog: false)
                });
            MainCanvasViewModel.ConfigureImageFileDialogHost(new WpfImageFileDialogHost(this, fileDialogService));
            LocalizationTextRuntimeService.RegisterWindow(this);
            PromoteSharedThemeResourcesToApplication();
            ApplyInitialWindowSizeToWorkArea();
            shellTimers = new ShellTimerSet(
                Dispatcher,
                yoloRuntimeStatusAdapter.HandleInferenceStatusPulseTimerTick,
                TrainingStatusPollTimer_Tick,
                MaskStrokePreviewCommitSwapTimer_Tick,
                MaskStrokeCommitQueueTimer_Tick,
                DisplayAdjustmentRefreshTimer_Tick,
                AnnotationVisibilityRefreshTimer_Tick);
            maskStrokeWorkflowAdapter = new MaskStrokeWorkflowAdapter(
                new MaskStrokeWorkflowAdapterContext
                {
                    ManualSegments = manualSegments,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    SelectedClassNameProvider = GetSelectedClassName,
                    EnsureClassItem = className => classCatalogWorkflowService.EnsureClassItem(applicationState.Data, className),
                    FindClassItem = className => applicationState.Data.ClassNamedList?
                        .FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase)),
                    ObjectReviewSummaryProvider = () => manualRois.Count + manualSegments.Count + confirmedDetectionCandidates.Count,
                    ManualRoiCountProvider = () => manualRois.Count,
                    ConfirmedCandidateCountProvider = () => confirmedDetectionCandidates.Count,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsAnnotationDirty = () => annotationDirtyState.IsDirty,
                    CanEditManualSegment = CanEditManualSegment,
                    MaskAnnotationService = maskAnnotationService,
                    MainCanvasViewModelProvider = () => MainCanvasViewModel,
                    CanvasPanelViewModelProvider = () => CanvasPanelViewModel,
                    LearningWorkflowViewModelProvider = () => LearningWorkflowViewModel,
                    ObjectReviewViewModelProvider = () => ObjectReviewViewModel,
                    StatusBarViewModelProvider = () => StatusBarViewModel,
                    Timers = new MaskStrokeTimerSetFacade(
                        new MaskStrokeTimerFacade(
                            () => shellTimers.MaskStrokeCommitQueue.Stop(),
                            () => shellTimers.MaskStrokeCommitQueue.Start(),
                            interval => shellTimers.MaskStrokeCommitQueue.Interval = interval,
                            shellTimers.MaskStrokeCommitQueue.Interval),
                        new MaskStrokeTimerFacade(
                            () => shellTimers.MaskStrokePreviewCommitSwap.Stop(),
                            () => shellTimers.MaskStrokePreviewCommitSwap.Start(),
                            interval => shellTimers.MaskStrokePreviewCommitSwap.Interval = interval,
                            shellTimers.MaskStrokePreviewCommitSwap.Interval)),
                    Dispatcher = new MaskStrokeDispatcherFacade(
                        () => Dispatcher.CheckAccess(),
                        action => Dispatcher.Invoke(action),
                        (action, priority) => Dispatcher.BeginInvoke(
                            new Action(action),
                            priority == MaskStrokeDispatchPriority.Background
                                ? System.Windows.Threading.DispatcherPriority.Background
                                : System.Windows.Threading.DispatcherPriority.ApplicationIdle)),
                    SetModelStatus = SetModelStatus,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    MarkMaskStrokeAnnotationsDirty = MarkMaskStrokeAnnotationsDirty,
                    RefreshAnnotationHistoryToolState = RefreshAnnotationHistoryToolState,
                    RefreshDeferredMaskStrokeDirtyPresentation = RefreshDeferredMaskStrokeDirtyPresentation,
                    PushAnnotationHistorySnapshot = snapshot => PushAnnotationHistorySnapshot(snapshot, markDirty: true),
                    TryRefreshMaskStrokeCanvasOverlays = TryRefreshMaskStrokeCanvasOverlays,
                    TryRefreshManualSegmentObjectReviewRow = TryRefreshManualSegmentObjectReviewRow,
                    QueueActiveImageQueueStatusRefresh = QueueActiveImageQueueStatusRefresh,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    RefreshObjectList = RefreshObjectList,
                    ScheduleCrashRecoveryJournalWrite = () => ScheduleCrashRecoveryJournalWrite(),
                    SetModelStatusAutomationText = text => StatusBarViewModel?.SetModelStatusAutomationText(text)
                });
            annotationHistoryAdapter = new AnnotationHistoryAdapter(
                new AnnotationHistoryAdapterContext
                {
                    AnnotationHistoryWorkflowService = new AnnotationHistoryWorkflowService(),
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    PendingCandidates = candidateReviewState.MutablePendingCandidates,
                    ConfirmedCandidates = candidateReviewState.MutableConfirmedCandidates,
                    HasPendingMaskStrokeUndoWork = maskStrokeWorkflowAdapter.HasPendingMaskStrokeUndoWork,
                    GetPendingMaskStrokeUndoActionName = maskStrokeWorkflowAdapter.GetPendingMaskStrokeUndoActionName,
                    CompleteMaskAnnotationStroke = maskStrokeWorkflowAdapter.CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = maskStrokeWorkflowAdapter.FlushQueuedMaskStrokeCommits,
                    CancelPendingIntelligentScissors = () => CancelPendingIntelligentScissors(updateStatus: false),
                    ResetPolygonAnnotation = polygonAnnotationService.Reset,
                    ResetMaskStrokeStateAfterHistoryRestore = maskStrokeWorkflowAdapter.ResetAfterHistoryRestore,
                    ClearMaskStrokePreview = () => MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false),
                    EnsureManualRoiMetadataCount = EnsureManualRoiMetadataCount,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    RefreshObjectList = RefreshObjectList,
                    RefreshCandidateList = RefreshCandidateList,
                    RedrawReviewRois = RedrawReviewRois,
                    PopulateClassList = () => PopulateClassList(),
                    UpdateDetectionResultOverlay = UpdateDetectionResultOverlay,
                    RefreshActiveImageQueueStatus = hasActiveCandidates => RefreshActiveImageQueueStatus(hasActiveCandidates),
                    SetPythonStatus = SetPythonStatus,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    AppendLog = AppendLog,
                    MarkAnnotationsDirty = MarkAnnotationsDirty,
                    ApplyToolState = toolState => LearningWorkflowViewModel?.SetAnnotationHistoryState(
                        toolState.CanUndo,
                        toolState.CanRedo,
                        toolState.UndoActionName,
                        toolState.RedoActionName)
                });
            annotationHistoryWorkflowService = annotationHistoryAdapter.WorkflowService;
            annotationVisibilityAdapter = new AnnotationVisibilityAdapter(
                new AnnotationVisibilityAdapterContext
                {
                    ManualSegments = manualSegments,
                    CurrentDatasetPurposeProvider = GetCurrentDatasetPurpose,
                    ResetPolygonAnnotation = polygonAnnotationService.Reset,
                    ClearBrushCursorPreview = () => MainCanvasViewModel?.ClearBrushCursorPreview(),
                    ClearMaskStrokePreview = () => MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false),
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    RefreshObjectList = RefreshObjectList,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    SetModelStatus = SetModelStatus,
                    AppendLog = AppendLog,
                    ScheduleAtApplicationIdle = action => Dispatcher.BeginInvoke(
                        new Action(action),
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle),
                    StopAnnotationVisibilityRefreshTimer = () => shellTimers.AnnotationVisibilityRefresh.Stop(),
                    StartAnnotationVisibilityRefreshTimer = () => shellTimers.AnnotationVisibilityRefresh.Start(),
                    ApplyDatasetPurposeToCurrentProject = ApplyDatasetPurposeToCurrentProject,
                    RefreshCanvasAnnotationToolScope = RefreshCanvasAnnotationToolScope
                });
            segmentationEditAdapter = new SegmentationEditAdapter(
                new SegmentationHoleService(),
                new SegmentationSplitService(),
                new SegmentationRemoveUnderlyingService(),
                new PolygonAnnotationService(),
                new SegmentationEditAdapterContext
                {
                    ManualSegments = manualSegments,
                    ManualRoiCountProvider = () => manualRois.Count,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    CanvasPanelViewModelProvider = () => CanvasPanelViewModel,
                    MainCanvasViewModelProvider = () => MainCanvasViewModel,
                    ObjectReviewViewModelProvider = () => ObjectReviewViewModel,
                    SelectedObjectReviewItemProvider = () => TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                        ? item
                        : null,
                    MutationErrorProvider = (item, requireVisible) => CanMutateSelectedObject(
                        item,
                        requireVisible,
                        out string error)
                        ? string.Empty
                        : error,
                    SmartMaskPromptSession = smartMaskPromptSession,
                    SmartMaskWorkflowService = smartMaskWorkflowService,
                    ObjectMetadataStateService = objectMetadataStateService,
                    PendingDetectionCandidatesProvider = () => pendingDetectionCandidates,
                    CancelPendingIntelligentScissors = updateStatus => CancelPendingIntelligentScissors(updateStatus),
                    CompleteMaskAnnotationStroke = CompleteMaskAnnotationStroke,
                    FlushQueuedMaskStrokeCommits = FlushQueuedMaskStrokeCommits,
                    SelectAnnotationTool = tool => SelectAnnotationTool(tool),
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    ClearMaskStrokePreview = () => MainCanvasViewModel?.ClearMaskStrokePreview(
                        refresh: false,
                        clearTexture: true),
                    RefreshObjectListWithSelection = RefreshObjectListWithSelection,
                    QueueActiveImageQueueStatusRefresh = QueueActiveImageQueueStatusRefresh,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    CaptureAnnotationHistory = CaptureAnnotationHistory,
                    PushAnnotationHistorySnapshot = snapshot => PushAnnotationHistorySnapshot(snapshot),
                    CancelObjectGroupSelection = CancelObjectGroupSelection
                });
            annotationRenderingAdapter = new AnnotationRenderingAdapter(
                new AnnotationRenderingAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    CanvasPanelViewModelProvider = () => CanvasPanelViewModel,
                    MainCanvasViewModelProvider = () => MainCanvasViewModel,
                    LearningWorkflowViewModelProvider = () => LearningWorkflowViewModel,
                    ObjectReviewViewModelProvider = () => ObjectReviewViewModel,
                    PolygonAnnotationService = polygonAnnotationService,
                    HolePolygonAnnotationService = segmentationEditAdapter.HolePolygonAnnotationService,
                    PolygonBoundaryEditWorkflowService = polygonBoundaryEditWorkflowService,
                    SmartMaskPromptSession = smartMaskPromptSession,
                    FourPointBoxService = fourPointBoxService,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    AnnotationSegmentEditAdapter = annotationSegmentEditAdapter,
                    PendingSegmentationHoleEditModeProvider = () => segmentationEditAdapter.PendingSegmentationHoleEditMode,
                    PendingDetectionCandidatesProvider = () => pendingDetectionCandidates,
                    ConfirmedDetectionCandidatesProvider = () => confirmedDetectionCandidates,
                    SelectedCandidateProvider = GetSelectedCandidate,
                    IsCandidateConfirmableProvider = IsCandidateConfirmable,
                    IsApplicationCloseApprovedProvider = () => isApplicationCloseApproved,
                    SelectedClassNameProvider = GetSelectedClassName,
                    ManualRoiClassNameProvider = GetManualRoiClassName,
                    ManualRoiSessionStateProvider = GetManualRoiSessionState,
                    ManualSegmentSessionStateProvider = GetManualSegmentSessionState,
                    IsPendingRemoveUnderlyingAffectedIndexProvider = IsPendingRemoveUnderlyingAffectedIndex,
                    IsSegmentationDatasetPurposeActiveProvider = IsSegmentationDatasetPurposeActive,
                    ActiveMaskStrokeSegmentIndicesProvider = GetActiveMaskStrokeSegmentIndices,
                    HasActiveMaskStrokeFullObjectRefreshProvider = HasActiveMaskStrokeFullObjectRefresh,
                    ShouldSelectCommittedMaskAfterStrokeProvider = ShouldSelectCommittedMaskAfterStroke,
                    RegisterAnnotationHistoryBeforeChange = (actionName, markDirty) => RegisterAnnotationHistoryBeforeChange(actionName, markDirty),
                    RegisterRoiEditHistoryBeforeChange = RegisterRoiEditHistoryBeforeChange,
                    CaptureManualRoiHistory = CaptureManualRoiHistory,
                    PushAnnotationHistorySnapshot = (snapshot, markDirty) => PushAnnotationHistorySnapshot(snapshot, markDirty),
                    ResetActiveRoiEditHistory = ResetActiveRoiEditHistory,
                    TryRefreshManualRoiObjectReviewRow = TryRefreshManualRoiObjectReviewRow,
                    RefreshObjectListWithSelection = RefreshObjectListWithSelection,
                    RefreshObjectList = RefreshObjectList,
                    RefreshObjectReviewAfterDelete = RefreshObjectReviewAfterDelete,
                    QueueActiveImageQueueStatusRefresh = QueueActiveImageQueueStatusRefresh,
                    RefreshActiveImageQueueStatus = RefreshActiveImageQueueStatus,
                    ShowSavedLabelsWorkflowView = ShowSavedLabelsWorkflowView,
                    SetModelStatus = SetModelStatus,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    RefreshSmartMaskCommandState = () => RefreshSmartMaskCommandState(),
                    TryStartAutoSmartMaskForNewRoi = TryStartAutoSmartMaskForNewRoi,
                    RefreshCanvasLayerVisibilityState = RefreshCanvasLayerVisibilityState
                });
            annotationInputAdapter = new AnnotationInputAdapter(
                new AnnotationInputAdapterContext
                {
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    TryHandleFourPointBoxInput = fourPointBoxInputAdapter.TryHandleInput,
                    TryApplyPendingPolygonVertexEdit = polygonBoundaryEditAdapter.TryApplyPendingPolygonVertexEdit,
                    TryHandlePendingIntelligentScissors = polygonBoundaryEditAdapter.TryHandlePendingIntelligentScissors,
                    TryApplyPendingSegmentationSplit = TryApplyPendingSegmentationSplit,
                    TryApplyPendingSegmentationHoleEdit = TryApplyPendingSegmentationHoleEdit,
                    TryApplySmartMaskPointInput = smartMaskWorkflowAdapter.TryApplySmartMaskPointInput,
                    ActiveAnnotationToolProvider = () => activeAnnotationTool,
                    TryBeginSelectedSegmentEdit = annotationSegmentEditAdapter.TryBeginSelectedSegmentEdit,
                    TryMoveSelectedSegmentEdit = annotationSegmentEditAdapter.TryMoveSelectedSegmentEdit,
                    ApplyMaskAnnotationStroke = maskStrokeWorkflowAdapter.ApplyMaskAnnotationStroke,
                    CompleteMaskAnnotationStroke = maskStrokeWorkflowAdapter.CompleteMaskAnnotationStroke,
                    CompleteSelectedSegmentEdit = annotationSegmentEditAdapter.CompleteSelectedSegmentEdit,
                    ResetPolygonAnnotation = polygonAnnotationService.Reset,
                    CompletePolygonAnnotation = CompletePolygonAnnotation,
                    PolygonAnnotationService = polygonAnnotationService,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    RefreshPolygonOverlays = RefreshPolygonOverlays,
                    SetYoloCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                    EnsureSegmentationDatasetPurposeForSegmentationTool = EnsureSegmentationDatasetPurposeForSegmentationTool,
                    SetLabelingWorkflowMode = () => SetWorkflowMode(WorkflowMode.Labeling),
                    SetActiveAnnotationTool = tool => activeAnnotationTool = tool,
                    SetCanvasTeachingMode = value =>
                    {
                        if (MainCanvasViewModel != null)
                        {
                            MainCanvasViewModel.IsTeachingMode = value;
                        }
                    },
                    SetCanvasImagePointInputMode = value =>
                    {
                        if (MainCanvasViewModel != null)
                        {
                            MainCanvasViewModel.IsImagePointInputMode = value;
                        }
                    },
                    SetCanvasInteractionMode = mode => MainCanvasViewModel?.ImageViewer?.SetViewMode(mode),
                    SetModelStatus = SetModelStatus,
                    AppendLog = AppendLog,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    SetLastMaskStrokePoint = maskStrokeWorkflowAdapter.SetLastMaskStrokePoint,
                    SetActiveMaskStrokeInProgress = maskStrokeWorkflowAdapter.SetActiveMaskStrokeInProgress,
                    SetActiveMaskStrokeActionName = maskStrokeWorkflowAdapter.SetActiveMaskStrokeActionName,
                    ClearActiveMaskStrokeSegmentIndices = maskStrokeWorkflowAdapter.ClearActiveMaskStrokeSegmentIndices,
                    ResetMaskStrokeCommitBuffer = maskStrokeWorkflowAdapter.ResetMaskStrokeCommitBuffer,
                    SetActiveMaskStrokeNeedsFullObjectRefresh = maskStrokeWorkflowAdapter.SetActiveMaskStrokeNeedsFullObjectRefresh,
                    CancelMaskStrokePreviewCommitSwap = maskStrokeWorkflowAdapter.CancelMaskStrokePreviewCommitSwap,
                    ShouldPreserveMaskPreviewDuringToolSwitch = maskStrokeWorkflowAdapter.ShouldPreserveMaskPreviewDuringToolSwitch,
                    ScheduleQueuedMaskStrokeCommitsAfterToolEnd = maskStrokeWorkflowAdapter.ScheduleQueuedMaskStrokeCommitsAfterToolEnd,
                    ClearBrushCursorPreview = () => MainCanvasViewModel?.ClearBrushCursorPreview(),
                    ClearMaskStrokePreview = refresh => MainCanvasViewModel?.ClearMaskStrokePreview(refresh),
                    GetMaskBrushRadius = maskStrokeWorkflowAdapter.GetMaskBrushRadius,
                    GetMaskCursorPreviewColor = maskStrokeWorkflowAdapter.GetMaskCursorPreviewColor,
                    SetBrushCursorPreview = (point, radius, color, isEraser) => MainCanvasViewModel?.SetBrushCursorPreview(
                        point,
                        radius,
                        color,
                        isEraser)
                });
            imageChangeStateAdapter = new ImageChangeStateAdapter(
                new ImageChangeStateAdapterContext
                {
                    ApplicationState = applicationState,
                    ObjectSessionStateService = objectSessionStateService,
                    ObjectMetadataStateService = objectMetadataStateService,
                    ImageLoadResourceService = imageLoadResourceService,
                    CandidateReviewState = candidateReviewState,
                    SmartMaskPromptSession = smartMaskPromptSession,
                    PolygonAnnotationService = polygonAnnotationService,
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    ManualSegments = manualSegments,
                    StopDisplayAdjustmentRefresh = () => shellTimers.DisplayAdjustmentRefresh.Stop(),
                    CancelFourPointBoxDraft = () => CancelFourPointBoxDraft(updateStatus: false),
                    CancelPendingSegmentationRemoveUnderlying = () => CancelPendingSegmentationRemoveUnderlying(updateStatus: false),
                    CancelPendingSegmentationSplit = () => CancelPendingSegmentationSplit(updateStatus: false),
                    CancelPendingSegmentationHoleEdit = () => CancelPendingSegmentationHoleEdit(updateStatus: false),
                    CancelPendingPolygonVertexEdit = () => CancelPendingPolygonVertexEdit(updateStatus: false),
                    CancelPendingIntelligentScissors = () => CancelPendingIntelligentScissors(updateStatus: false),
                    CancelObjectGroupSelection = () => CancelObjectGroupSelection(updateStatus: false),
                    ResetMaskStrokeStateForImageChange = ResetMaskStrokeStateForImageChange,
                    ClearAnnotationHistory = ClearAnnotationHistory,
                    ClearCanvasImage = () => MainCanvasViewModel?.ClearImage(),
                    RefreshCandidateList = RefreshCandidateList,
                    RefreshObjectList = RefreshObjectList,
                    SetAnnotationSaveStatusWaiting = SetAnnotationSaveStatusWaiting
                });
            imageQueueLoadingAdapter = new ImageQueueLoadingAdapter(
                new ImageQueueLoadingAdapterContext
                {
                    ImageDecodeCacheService = imageDecodeCacheService,
                    ImageDecodePreloadService = imageDecodePreloadService,
                    ImageDecodeService = imageDecodeService,
                    AnnotationDirtyState = annotationDirtyState,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsShellLoaded = () => IsLoaded,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    HasPendingMaskStrokeCommitWork = HasPendingMaskStrokeCommitWork,
                    SaveCurrentAnnotations = (out int savedCount) => SaveCurrentAnnotations(out savedCount),
                    ScheduleBackground = action => Dispatcher.BeginInvoke(
                        new Action(action),
                        System.Windows.Threading.DispatcherPriority.Background),
                    RefreshCandidateList = RefreshCandidateList,
                    RefreshObjectList = RefreshObjectList,
                    PopulateClassList = () => PopulateClassList(),
                    RefreshActiveImageQueueStatus = RefreshActiveImageQueueStatus,
                    UpdateImageQueueStatusText = () => UpdateImageQueueStatusText(),
                    AppendLog = AppendLog,
                    LoadImageQueueFromRoot = (imageRoot, selectedImagePath, loadFirstImage, refreshDetails) =>
                        LoadImageQueueFromRoot(imageRoot, selectedImagePath, loadFirstImage, refreshDetails),
                    SelectImageQueueItem = SelectImageQueueItem,
                    QueueItemCountProvider = () => imageQueueItems.Count,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    QueueImagePathsProvider = () => imageQueueItems.Select(item => item.ImagePath),
                    IsImageQueued = imagePath => imageQueueItemsByPath.ContainsKey(imagePath),
                    IsSameImageRoot = imageQueueSelectionService.IsSameRoot,
                    CurrentImageRootProvider = () => ImageQueueViewModel?.CurrentImageFolderPath ?? string.Empty
                });
            imageLoadWorkflowAdapter = new ImageLoadWorkflowAdapter(
                new ImageLoadWorkflowAdapterContext
                {
                    ApplicationState = applicationState,
                    DataProvider = () => applicationState.Data,
                    ImageDecodeCacheService = imageDecodeCacheService,
                    ImageDecodeService = imageDecodeService,
                    ImageLoadResourceService = imageLoadResourceService,
                    ImageLoadPresentationService = imageLoadPresentationService,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ImageViewerLoadOverrideProvider = () => imageViewerLoadOverride != null,
                    EnsureViewerReadyForImageLoad = () =>
                    {
                        bool isReady = RuntimeDiagnosticsViewModel.EnsureViewerReadyForImageLoad(out string detail);
                        return (isReady, detail);
                    },
                    IsViewerVisible = () => IsLoaded && IsVisible,
                    ShowViewerUnavailable = ShowViewerUnavailable,
                    LoadImageToCanvas = LoadImageToCanvas,
                    RefreshCanvas = () => MainCanvasViewModel?.ImageViewer?.RefreshGL(),
                    EnsureProjectSettings = EnsureProjectSettings,
                    TrySavePendingAnnotationsBeforeImageChange = imageQueueLoadingAdapter.TrySavePendingAnnotationsBeforeImageChange,
                    PrepareForImageChange = imageChangeStateAdapter.PrepareForImageChange,
                    ResetForImageChange = imageChangeStateAdapter.ResetForImageChange,
                    UpdateDetectionResultOverlay = UpdateDetectionResultOverlay,
                    LoadSavedBoxAnnotationsForActiveImage = LoadSavedBoxAnnotationsForActiveImage,
                    LoadSavedSegmentationAnnotationsForActiveImage = LoadSavedSegmentationAnnotationsForActiveImage,
                    LoadObjectMetadataForActiveImage = LoadObjectMetadataForActiveImage,
                    PopulateImageQueueAfterLoad = imageQueueLoadingAdapter.PopulateImageQueueAfterLoad,
                    ScheduleImageLoadReviewRefresh = imageQueueLoadingAdapter.ScheduleImageLoadReviewRefresh,
                    RefreshImageLoadReviewState = imageQueueLoadingAdapter.RefreshImageLoadReviewState,
                    IsDisplayAdjustmentActive = () => CanvasPanelViewModel.IsDisplayAdjustmentActive,
                    ScheduleDisplayAdjustmentRefresh = ScheduleDisplayAdjustmentRefresh,
                    PreloadAdjacentQueueImages = imageQueueLoadingAdapter.PreloadAdjacentQueueImages,
                    SetLastImageLoadDiagnostics = value => lastImageLoadDiagnostics = value,
                    LastImageLoadDiagnosticsProvider = () => lastImageLoadDiagnostics,
                    SetDatasetStatus = SetDatasetStatus,
                    SetModelStatus = SetModelStatus,
                    MarkAnnotationsSaved = MarkAnnotationsSaved,
                    AppendLog = AppendLog
                });
            shellInputLifecycleAdapter = new ShellInputLifecycleAdapter(
                new ShellInputLifecycleAdapterContext
                {
                    ApplicationClosePolicyService = applicationClosePolicyService,
                    ApplicationState = applicationState,
                    CandidateReviewState = candidateReviewState,
                    AnnotationDirtyState = annotationDirtyState,
                    ShellKeyboardShortcutAdapter = shellKeyboardShortcutAdapter,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    ScheduleDeferredStartup = action => Dispatcher.BeginInvoke(
                        new Action(action),
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle),
                    RefreshYoloStatus = RefreshYoloStatus,
                    RefreshYoloSettingsPanelAsync = () => RefreshYoloSettingsPanelAsync(),
                    TryHandleCrashRecoveryOnStartup = TryHandleCrashRecoveryOnStartup,
                    TryLoadStartupSampleImage = TryLoadStartupSampleImage,
                    SetPythonStatus = SetPythonStatus,
                    InferenceWaitingTextProvider = () => OpenVisionLanguageService.T("WpfShell.Status.InferenceWaiting"),
                    AppendLog = AppendLog,
                    IsDispatcherThread = () => Dispatcher.CheckAccess(),
                    ScheduleLanguageRefresh = action => Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.DataBind,
                        new Action(action)),
                    RefreshLocalizedPresentation = RefreshLocalizedPresentationForLanguage,
                    HasUnsavedAnnotations = () => annotationDirtyState.IsDirty || HasPendingMaskStrokeCommitWork(),
                    UnsavedAnnotationReasonProvider = () => annotationDirtyState.Reason,
                    ActiveWorkStateProvider = () => new ApplicationCloseWorkState
                    {
                        IsCreatingSmartMask = smartMaskWorkflowService.IsRunning,
                        IsDetecting = imageDetectionWorkflowService.IsDetecting,
                        IsBatchDetectionRunning = batchDetectionWorkflowService.IsRunning,
                        IsExternalYoloDatasetIntakeRunning = externalYoloDatasetIntakeWorkflowService.IsRunning,
                        IsExternalEvaluationDataAuditRunning = externalAuditWorkflowService.IsExternalEvaluationDataAuditRunning,
                        IsHistoricalSegmentationRemediationAuditRunning = externalAuditWorkflowService.IsHistoricalSegmentationRemediationAuditRunning,
                        IsTrainingRunning = trainingCommandLifecycleService.IsRunning
                            || trainingRuntimeWorkflowService.IsTrainingWorkflowRunning
                            || TrainingProgressPresentationService.IsTrainingStopAvailable(applicationState.GetPythonCommunicationStatusSnapshot()),
                        IsYoloEnvironmentCommandRunning = yoloEnvironmentWorkflowService.IsRunning,
                        IsModelComparisonRunning = modelComparisonWorkflowService.IsModelComparisonRunning,
                        IsSegmentationAdapterComparisonRunning = modelComparisonWorkflowService.IsSegmentationComparisonRunning,
                        IsAnomalyEvaluationRunning = anomalyClassificationEvaluationWorkflowService.IsRunning
                    },
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ShowApplicationClosePrompt = ShowApplicationClosePromptForAdapter,
                    TrySaveCurrentAnnotations = (out string failureDetails) =>
                    {
                        failureDetails = string.Empty;
                        bool saved = SaveCurrentAnnotations(out _);
                        if (!saved)
                        {
                            failureDetails = "라벨 저장 결과가 실패를 반환했습니다.";
                        }

                        return saved;
                    },
                    ShowSaveFailure = ShowSaveFailureForAdapter,
                    SetApplicationCloseApproved = () => isApplicationCloseApproved = true,
                    ModelComparisonWorkflowService = modelComparisonWorkflowService,
                    ExternalYoloDatasetIntakeWorkflowService = externalYoloDatasetIntakeWorkflowService,
                    ExternalAuditWorkflowService = externalAuditWorkflowService,
                    YoloEnvironmentWorkflowService = yoloEnvironmentWorkflowService,
                    ImageDetectionWorkflowService = imageDetectionWorkflowService,
                    BatchDetectionWorkflowService = batchDetectionWorkflowService,
                    CrashRecoveryJournalWorkflowService = crashRecoveryJournalWorkflowService,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    DetachShellEventSubscriptions = DetachShellEventSubscriptions,
                    DiscardCrashRecoveryJournal = DiscardCrashRecoveryJournal,
                    DetachCrashRecoveryJournalEvents = () =>
                    {
                        crashRecoveryJournalWorkflowService.WriteFailed -= OnCrashRecoveryJournalWriteFailed;
                        crashRecoveryJournalWorkflowService.CaptureFailed -= OnCrashRecoveryJournalCaptureFailed;
                    },
                    SaveWorkspaceLayoutSettings = () => ShellViewModel.SaveWorkspaceLayoutSettings(),
                    AuxiliaryWindowHost = auxiliaryWindowHost,
                    DatasetTransferWindowHost = datasetTransferWindowHost,
                    PatchCoreHeatmapWindowHost = patchCoreHeatmapWindowHost,
                    StopInferenceStatusPulse = StopInferenceStatusPulse,
                    StopTrainingStatusPolling = StopTrainingStatusPolling,
                    MaskStrokeWorkflowAdapter = maskStrokeWorkflowAdapter,
                    ShellTimers = shellTimers,
                    ImageDecodePreloadService = imageDecodePreloadService,
                    CancelImageQueueCatalogLoad = waitForCompletion => CancelImageQueueCatalogLoad(waitForCompletion),
                    ImageQueueCatalogLoadAdapter = imageQueueCatalogLoadAdapter,
                    CancelImageQueueDetailRefresh = waitForCompletion => CancelImageQueueDetailRefresh(waitForCompletion),
                    ImageQueueDetailRefreshAdapter = imageQueueDetailRefreshAdapter,
                    ProjectRecipeApplyWorkflowService = projectRecipeApplyWorkflowService,
                    YoloRuntimeStatusAdapter = yoloRuntimeStatusAdapter,
                    SmartMaskWorkflowService = smartMaskWorkflowService,
                    AnomalyClassificationEvaluationWorkflowService = anomalyClassificationEvaluationWorkflowService,
                    TrainingCommandLifecycleService = trainingCommandLifecycleService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    TrainingRuntimeWorkflowService = trainingRuntimeWorkflowService,
                    ImageDecodeCacheService = imageDecodeCacheService,
                    ImageLoadResourceService = imageLoadResourceService,
                    ActiveImageBitmapProvider = () => applicationState.ImageWorkspace.ActiveImage,
                    ViewModels = viewModels
                });
            DataContext = viewModels;
            viewModels.LanguageViewModel.LanguageChanged += LanguageViewModel_LanguageChanged;
            ShellViewModel.WorkspaceLayoutSaveFailed += ShellViewModel_WorkspaceLayoutSaveFailed;
            ShellViewModel.WorkspaceLayoutReset += ShellViewModel_WorkspaceLayoutReset;
            RuntimeDiagnosticsViewModel.AttachGraphicsCapabilityProvider(
                () => OpenGlRuntimeCapabilityProbe.Probe(MainCanvasViewModel.ImageViewer));
            RuntimeDiagnosticsViewModel.ConfigureOpenSetupCenterAction(ExecuteOpenEnvironmentSetupCenterCommand);
            ShellViewModel.RestoreWorkspaceLayoutSettings();
            ShellViewModel.RefreshLocalizedPresentation();
            TemplateMatchingAutoLabelViewModel.ConfigureHost(templateMatchingAutoLabelHostAdapter);
            classCatalogWorkflowAdapter = new ClassCatalogWorkflowAdapter(
                new ClassCatalogWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    ClassCatalogViewModel = viewModels.ClassCatalogViewModel,
                    CanvasPanelViewModel = viewModels.CanvasPanelViewModel,
                    ClassNameBox = ClassNameBox,
                    ClassCatalogWorkflowService = classCatalogWorkflowService,
                    AnnotationClassRenameService = annotationClassRenameService,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualSegments = manualSegments,
                    ConfirmedDetectionCandidates = confirmedDetectionCandidates,
                    AnnotationDirtyState = annotationDirtyState,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsDatasetStageActive = () => ShellViewModel?.IsDatasetStageActive == true,
                    ShowClassCatalogWorkflowView = ShowClassCatalogWorkflowView,
                    UpdateLayout = UpdateLayout,
                    SelectOutputRootFolder = (title, initialPath) => TryPickFolder(
                        title,
                        initialPath,
                        out string selectedPath)
                        ? selectedPath
                        : string.Empty,
                    EnsureProjectSettings = EnsureProjectSettings,
                    RefreshObjectClassOptions = RefreshObjectClassOptions,
                    RefreshObjectList = RefreshObjectList,
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshTrainingReadiness = () => RefreshTrainingReadinessPanel(refreshYaml: false),
                    PopulateProjectConfigPanelFields = PopulateProjectConfigPanelFields,
                    ShouldReloadActiveImage = (previousPath, currentPath) =>
                        !string.IsNullOrWhiteSpace(applicationState.ImageWorkspace.ActiveImagePath)
                        && applicationState.ImageWorkspace.ActiveImage != null
                        && !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty
                        && !DatasetSetupPathService.PathsEqual(previousPath, currentPath),
                    ReloadActiveImageAnnotations = () => TryLoadImage(
                        applicationState.ImageWorkspace.ActiveImagePath,
                        populateQueue: false,
                        refreshQueueDetails: true,
                        refreshActiveStatus: true,
                        appendLoadLog: false),
                    SetDatasetStatus = SetDatasetStatus,
                    AppendLog = AppendLog,
                    RecipeNameProvider = GetCurrentRecipeName,
                    RefreshTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion()
                });
            trainingGuideWorkflowAdapter = new TrainingGuideWorkflowAdapter(
                new TrainingGuideWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    LearningWorkflowViewModel = viewModels.LearningWorkflowViewModel,
                    TrainingGuideHistoryService = trainingGuideHistoryService,
                    TrainingGuideHistoryWorkflowService = trainingGuideHistoryWorkflowService,
                    ProjectRecipeSessionService = projectRecipeSessionService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    CurrentRecipeNameProvider = GetCurrentRecipeName,
                    CommunicationStatusProvider = applicationState.GetPythonCommunicationStatusSnapshot,
                    HasPendingTrainingWeightsRecipeSaveProvider = () => hasPendingTrainingWeightsRecipeSave,
                    LastTrainingReadinessReportProvider = () => lastYoloTrainingReadinessReport,
                    SetLastTrainingReadinessReport = report => lastYoloTrainingReadinessReport = report,
                    BuildCurrentTrainingWeightsComparison = BuildCurrentTrainingWeightsComparison,
                    UpdateTrainingComparisonViewModel = comparison => UpdateTrainingComparisonViewModel(comparison),
                    EnsureProjectSettings = EnsureProjectSettings,
                    AppendLog = AppendLog,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    ImageQueueItemsProvider = () => imageQueueItems,
                    ManualRoiCountProvider = () => manualRois.Count,
                    ConfirmedCandidateCountProvider = () => confirmedDetectionCandidates.Count,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    RefreshTrainingReadinessPanel = refreshYaml => RefreshTrainingReadinessPanel(refreshYaml),
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    SetModelStatus = SetModelStatus
                });
            batchDetectionWorkflowAdapter = new BatchDetectionWorkflowAdapter(
                new BatchDetectionWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    BatchDetectionWorkflowService = batchDetectionWorkflowService,
                    BatchDetectionProgressService = batchDetectionProgressService,
                    DetectionTargetService = detectionTargetService,
                    ImageDetectionWorkflowService = imageDetectionWorkflowService,
                    ImageQualityReviewWorkflowService = imageQualityReviewWorkflowService,
                    AnomalyImageReviewSession = anomalyImageReviewSession,
                    DetectionResultPresentationService = detectionResultPresentationService,
                    CandidateReviewState = candidateReviewState,
                    QueueItemsProvider = () => imageQueueItems.ToList(),
                    VisibleQueueItemsProvider = () => imageQueueView == null
                        ? imageQueueItems.ToList()
                        : imageQueueView.Cast<object>().OfType<WpfImageQueueItem>().ToList(),
                    SelectedQueueItemProvider = () => ImageQueueGrid?.SelectedItem as WpfImageQueueItem,
                    FindQueueItem = FindImageQueueItem,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    EnsureInferenceModeForDetection = EnsureInferenceModeForDetection,
                    RunInteractiveDetectionAsync = (path, allowSmokeFallback) =>
                        RunInteractiveDetectionAsync(path, allowSmokeFallback),
                    ShowBatchDetectionPreflight = ShowBatchDetectionPreflight,
                    RunWorkerDetectionAsync = (path, token) => RunWorkerDetectionForImageAsync(
                        path,
                        applyToCanvas: false,
                        token,
                        workerReadyAlreadyChecked: true),
                    YieldBatchDetectionResultFrameAsync = YieldBatchDetectionResultFrameOnUiAsync,
                    EnsurePythonModelClientReadyAsync = (timeoutMilliseconds, cancellationToken) =>
                        applicationState.ModelRuntime.EnsurePythonModelClientReadyAsync(timeoutMilliseconds, cancellationToken),
                    CommunicationStatusProvider = applicationState.GetPythonCommunicationStatusSnapshot,
                    PythonWorkerLastErrorProvider = () => applicationState.ModelRuntime.PythonClientProcess?.LastError,
                    IsAnomalyDatasetPurpose = IsAnomalyDatasetPurpose,
                    AppendLog = AppendLog,
                    ApplyReviewStatusToItem = ApplyReviewStatusToItem,
                    ApplyAnomalyClassificationToImage = (path, imageName, candidates, saveReviewStatus) =>
                        ApplyAnomalyClassificationToImage(path, imageName, candidates, saveReviewStatus),
                    RefreshQueueView = () => ImageQueuePanelControl?.RefreshQueueView(),
                    UpdateImageQueueStatusText = () => UpdateImageQueueStatusText(),
                    ApplyBatchDetectionControls = controlState =>
                    {
                        UpdateYoloCommandButtons();
                        ImageQueueViewModel?.SetBatchDetectionProgress(
                            controlState.ProgressMaximum,
                            controlState.ProgressValue,
                            controlState.StatusText);
                        if (controlState.ShouldRefreshQueueStatus)
                        {
                            UpdateImageQueueStatusText();
                        }
                        else
                        {
                            SetDatasetStatus(controlState.DatasetStatusText);
                        }
                    },
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    SetGlobalInferenceStatus = (text, isBusy, isWarning) =>
                        SetGlobalInferenceStatus(text, isBusy, isWarning),
                    SetPythonStatus = SetPythonStatus,
                    SelectImageQueueItem = SelectImageQueueItem,
                    TryLoadBatchImage = path => TryLoadImage(
                        path,
                        populateQueue: false,
                        refreshQueueDetails: false,
                        refreshActiveStatus: false,
                        appendLoadLog: false),
                    UpdateSelectedQueueImageButton = item => imageQueueReviewAdapter.UpdateSelectedQueueImageButton(item),
                    RefreshCandidateList = RefreshCandidateList,
                    RefreshObjectList = RefreshObjectList,
                    RedrawReviewRois = RedrawReviewRois,
                    SetActiveImageDetectionStatus = SetActiveImageDetectionStatus,
                    AddCandidateReviewHistory = AddCandidateReviewHistory,
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    ClearCandidateReviewHistory = () => CandidateReviewViewModel?.ClearReviewHistory(),
                    ApplyCanvasDisplayMode = (mode, redraw, logChange) =>
                        ApplyCanvasDisplayMode(mode, redraw, logChange),
                    SetDetectionOverlay = presentation => CanvasPanelViewModel?.SetDetectionOverlay(
                        presentation.Title,
                        presentation.Summary,
                        presentation.SelectedText,
                        presentation.Detail,
                        presentation.Status),
                    CandidateConfidenceFilterProvider = GetCandidateConfidenceFilter
                });
            trainingRuntimeAdapter = new TrainingRuntimeAdapter(
                new TrainingRuntimeAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    TrainingRuntimeWorkflowService = trainingRuntimeWorkflowService,
                    TrainingReadinessWorkflowService = trainingReadinessWorkflowService,
                    TrainingSettingsViewModelProvider = () => viewModels.TrainingSettingsViewModel,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    IsModelWorkflowCreated = () => viewModels.IsModelWorkflowCreated,
                    IsYoloEnvironmentRunning = () => yoloEnvironmentWorkflowService.IsRunning,
                    IsImageDetectionRunning = () => imageDetectionWorkflowService.IsDetecting,
                    IsBatchDetectionRunning = () => batchDetectionWorkflowService.IsRunning,
                    IsTrainingCommandRunning = () => trainingCommandLifecycleService.IsRunning,
                    CurrentProgressValueProvider = () => viewModels.TrainingSettingsViewModel?.TrainingProgressValue ?? 0D,
                    LastTrainingReadinessReportProvider = () => lastYoloTrainingReadinessReport,
                    CurrentRecipeNameProvider = GetCurrentRecipeName,
                    EnsureModelRuntimeForTraining = EnsureModelRuntimeForTraining,
                    SaveTrainingEditorFields = SaveTrainingEditorFields,
                    TrySaveExternalYoloDatasetSettings = () => TrySaveExternalYoloDatasetSettings(),
                    StartTrainingStatusTimer = () => shellTimers.TrainingStatusPoll.Start(),
                    StopTrainingStatusTimer = () => shellTimers.TrainingStatusPoll.Stop(),
                    SetModelCenterTrainingState = (progressText, detailText) => ShellViewModel?.SetModelCenterTrainingState(progressText, detailText),
                    UpdateTrainingGuideTrainingHistory = UpdateYoloTrainingGuideTrainingHistory,
                    TryApplyLatestTrainingWeightsFromProject = logIfUnchanged => TryApplyLatestTrainingWeightsFromProject(logIfUnchanged),
                    UpdateTrainingChecklist = UpdateYoloTrainingChecklist,
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    RefreshExternalYoloDatasetIntakePresentation = RefreshExternalYoloDatasetIntakePresentation,
                    SetTrainingChecklistText = (statusText, detailText, actionText) => LearningWorkflowViewModel?.SetTrainingChecklistText(statusText, detailText, actionText),
                    SetRecoveryStatus = SetYoloRecoveryStatus,
                    ClearRecoveryStatus = ClearYoloRecoveryStatus,
                    UpdateCommandState = UpdateYoloCommandButtons,
                    RefreshYoloStatus = RefreshYoloStatus,
                    AppendLog = AppendLog,
                    ResolveBrushResource = (key, fallback) => TryFindResource(key) as MediaBrush ?? fallback
                });
            annotationWorkflowCommandAdapter = new AnnotationWorkflowCommandAdapter(
                new AnnotationWorkflowCommandAdapterContext
                {
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    TryLoadStartupSampleImage = TryLoadStartupSampleImage,
                    ActiveImageSizeProvider = () => applicationState.ImageWorkspace.ActiveImageSize,
                    ActiveImagePathProvider = () => applicationState.ImageWorkspace.ActiveImagePath,
                    SelectedClassNameProvider = GetSelectedClassName,
                    RegisterAnnotationHistoryBeforeChange = action => RegisterAnnotationHistoryBeforeChange(action),
                    ManualRois = manualRois,
                    ManualRoiClassNames = manualRoiClassNames,
                    ManualRoiShapeKinds = manualRoiShapeKinds,
                    ManualRoiOverlayIds = manualRoiOverlayIds,
                    RedrawReviewRois = RedrawReviewRois,
                    RefreshObjectList = RefreshObjectList,
                    ShowSavedLabelsWorkflowView = ShowSavedLabelsWorkflowView,
                    SaveCurrentAnnotations = () =>
                    {
                        bool succeeded = SaveCurrentAnnotations(out int savedCount);
                        return new AnnotationSaveOutcome(succeeded, savedCount);
                    },
                    MarkActiveImageConfirmed = MarkActiveImageConfirmed,
                    GetSelectedImageQueueFilter = GetSelectedImageQueueFilter,
                    ScheduleOpenNextIncompleteQueueImageAfterSave = completedImagePath => Dispatcher.BeginInvoke(
                        new Action(() => OpenNextIncompleteQueueImageAfterSave(completedImagePath)),
                        System.Windows.Threading.DispatcherPriority.Background),
                    TryOpenNextIncompleteQueueImage = path => TryOpenNextIncompleteQueueImage(path),
                    HasActiveImage = () => applicationState.ImageWorkspace.ActiveImage != null && !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    HasCanvasLabelObjects = HasCanvasLabelObjects,
                    SaveCurrentEmptyAnnotations = SaveCurrentEmptyAnnotations,
                    MarkActiveImageNoCandidate = MarkActiveImageNoCandidate,
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    TryOpenNextIncompleteQueueImageWithoutPath = () => TryOpenNextIncompleteQueueImage(),
                    FinishQueueCompletionAndGuideDatasetCheck = FinishQueueCompletionAndGuideDatasetCheck,
                    BuildLabelPathSummary = BuildLabelPathSummary,
                    AppendLog = AppendLog
                });
            trainingGuideCommandAdapter = new TrainingGuideCommandAdapter(
                new TrainingGuideCommandAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    SelectedDatasetPurposeProvider = () => LearningWorkflowViewModel?.SelectedDatasetPurposeMode,
                    CurrentDatasetPurposeProvider = GetCurrentDatasetPurpose,
                    ExecuteStartDatasetSetupCommand = ExecuteStartDatasetSetupCommand,
                    ExecuteBrowseImageFolderCommand = ExecuteBrowseImageFolderCommand,
                    ExecuteLoadSampleCommand = ExecuteLoadSampleCommand,
                    ExecuteAddSampleRoiCommand = ExecuteAddSampleRoiCommand,
                    ExecuteSaveAnnotationsCommand = ExecuteSaveAnnotationsCommand,
                    ExecuteHistoricalSegmentationRemediationAudit = () => LearningWorkflowViewModel?.HistoricalSegmentationRemediationAuditCommand?.Execute(null),
                    SetInferenceMode = isInference => SetWorkflowMode(isInference ? WorkflowMode.Inference : WorkflowMode.Labeling),
                    SelectAnnotationTool = (tool, revealInGuide) => SelectAnnotationTool(tool, revealInGuide),
                    SetCanvasTeachingMode = value => MainCanvasViewModel.IsTeachingMode = value,
                    FocusMainCanvas = () => MainCanvasView?.Focus(),
                    FocusClassCatalogTab = FocusClassCatalogTab,
                    FocusClassNameBox = () => ClassNameBox?.Focus(),
                    FocusAnnotationToolsTab = FocusAnnotationToolsTab,
                    FocusYoloTrainingSettingsTab = FocusYoloTrainingSettingsTab,
                    FocusStartTrainingButton = () => StartTrainingButton?.Focus(),
                    FocusDetectButton = () => DetectButton?.Focus(),
                    RefreshTrainingReadinessPanel = refreshYaml => RefreshTrainingReadinessPanel(refreshYaml),
                    TryApplyLatestTrainingWeightsFromProject = logIfUnchanged => TryApplyLatestTrainingWeightsFromProject(logIfUnchanged),
                    ShowCandidateReviewWorkflowView = ShowCandidateReviewWorkflowView,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    SetModelStatus = SetModelStatus,
                    SetYoloCommandStatus = SetYoloCommandStatus,
                    AppendLog = AppendLog,
                    HasActiveImage = () => applicationState.ImageWorkspace.ActiveImage != null && !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty,
                    ManualRoiCountProvider = () => manualRois.Count,
                    VisibleManualSegmentCountProvider = GetVisibleManualSegmentCount,
                    ConfirmedCandidateCountProvider = () => confirmedDetectionCandidates.Count,
                    SaveCurrentAnnotations = () =>
                    {
                        bool succeeded = SaveCurrentAnnotations(out int savedCount);
                        return (succeeded, savedCount);
                    },
                    MarkActiveImageConfirmed = MarkActiveImageConfirmed,
                    BuildLabelPathSummary = BuildLabelPathSummary
                });
            workflowNavigationAdapter = new WorkflowNavigationAdapter(
                new WorkflowNavigationAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    CommunicationStatusProvider = applicationState.GetPythonCommunicationStatusSnapshot,
                    ShellViewModel = viewModels.ShellViewModel,
                    LearningWorkflowViewModel = viewModels.LearningWorkflowViewModel,
                    CanvasPanelViewModel = viewModels.CanvasPanelViewModel,
                    MainCanvasViewModel = viewModels.MainCanvasViewModel,
                    CandidateReviewState = candidateReviewState,
                    AnnotationDirtyState = annotationDirtyState,
                    ImageDetectionWorkflowService = imageDetectionWorkflowService,
                    BatchDetectionWorkflowService = batchDetectionWorkflowService,
                    YoloEnvironmentWorkflowService = yoloEnvironmentWorkflowService,
                    ModelComparisonWorkflowService = modelComparisonWorkflowService,
                    AnomalyClassificationEvaluationWorkflowService = anomalyClassificationEvaluationWorkflowService,
                    TrainingCommandLifecycleService = trainingCommandLifecycleService,
                    TrainingRuntimeWorkflowService = trainingRuntimeWorkflowService,
                    GetPythonModelRuntimeState = GetPythonModelRuntimeState,
                    GetCurrentRecipeName = GetCurrentRecipeName,
                    LastTrainingReadinessReportProvider = () => lastYoloTrainingReadinessReport,
                    ApplyWorkflowCommandState = viewModels.ApplyWorkflowCommandState,
                    CanvasLabelObjectCountProvider = GetCanvasLabelObjectCount,
                    PendingCandidateCountProvider = () => pendingDetectionCandidates.Count,
                    ActiveImageAvailableProvider = () => applicationState.ImageWorkspace.ActiveImage != null && !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    SelectedAnnotationToolProvider = () => LearningWorkflowViewModel?.SelectedTool,
                    SetGlobalInferenceStatus = (text, isBusy, isWarning) => SetGlobalInferenceStatus(text, isBusy, isWarning),
                    SetYoloCommandStatus = (text, isBusy) => SetYoloCommandStatus(text, isBusy),
                    SetPythonStatus = SetPythonStatus,
                    SetModelStatus = SetModelStatus,
                    AppendLog = AppendLog,
                    FocusSelectedCandidateInViewer = FocusSelectedCandidateInViewer,
                    RedrawReviewRois = RedrawReviewRois,
                    UpdateDetectionResultOverlay = UpdateDetectionResultOverlay,
                    UpdateCanvasCommandButtons = UpdateCanvasCommandButtons,
                    RefreshCandidateList = RefreshCandidateList,
                    UpdateCandidateActionState = UpdateCandidateActionState,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    UpdateWorkflowProgressStatus = UpdateWorkflowProgressStatus,
                    FocusAnnotationToolsTab = FocusAnnotationToolsTab,
                    FocusDatasetOnboardingTab = FocusDatasetOnboardingTab,
                    FocusYoloSettingsTab = FocusYoloSettingsTab,
                    ReviewTabControl = ReviewTabControl,
                    ObjectsReviewTab = ObjectsReviewTab,
                    CandidatesReviewTab = CandidatesReviewTab,
                    LearningReviewTab = LearningReviewTab,
                    ClassesReviewTab = ClassesReviewTab,
                    YoloSettingsReviewTab = YoloSettingsReviewTab,
                    BuildCurrentTrainingWeightsComparison = BuildCurrentTrainingWeightsComparison,
                    UpdateTrainingComparisonViewModel = comparison => UpdateTrainingComparisonViewModel(comparison),
                    EnsureProjectSettings = EnsureProjectSettings,
                    RefreshCanvasAnnotationToolScope = RefreshCanvasAnnotationToolScope,
                    ApplyAnnotationToolSelection = ApplyAnnotationToolSelection,
                    RefreshAnnotationVisibilityForDatasetPurpose = notifyOperator => RefreshAnnotationVisibilityForDatasetPurpose(notifyOperator),
                    RefreshTrainingReadinessPanel = refreshYaml => RefreshTrainingReadinessPanel(refreshYaml),
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    DetectButton = DetectButton,
                    DetectSelectedQueueButton = DetectSelectedQueueButton,
                    BatchDetectQueueButton = BatchDetectQueueButton,
                    RetryFailedQueueButton = RetryFailedQueueButton,
                    StopBatchQueueButton = StopBatchQueueButton
                });
            workflowPanelFocusAdapter = new WorkflowPanelFocusAdapter(
                new WorkflowPanelFocusAdapterContext
                {
                    ShowYoloModelCenterWorkflowView = ShowYoloModelCenterWorkflowView,
                    ShowGuideToolsWorkflowView = ShowGuideToolsWorkflowView,
                    ShowSavedLabelsWorkflowView = ShowSavedLabelsWorkflowView,
                    UpdateLayout = UpdateLayout,
                    ShowAnnotationToolPalette = () =>
                    {
                        LearningWorkflowViewModel?.ShowLabelingTask();
                        LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
                    },
                    ShowDatasetSetupStart = () =>
                    {
                        LearningWorkflowViewModel?.ShowDatasetOnboarding();
                        LearningWorkflowPanelControl?.ShowDatasetSetupStart();
                    },
                    IsInferenceWorkflowActive = () => IsInferenceWorkflowActive,
                    HasCanvasLabelObjects = HasCanvasLabelObjects,
                    IsDatasetStageActive = () => ShellViewModel?.IsDatasetStageActive == true,
                    HasActiveImage = () => applicationState.ImageWorkspace.ActiveImage != null,
                    ImageQueueItemCount = () => imageQueueItems.Count,
                    AreModelWorkflowPanelsComposed = () => modelWorkflowPanelsComposed,
                    IsModelComparisonVisible = () => CandidateReviewViewModel?.ModelComparisonVisibility == Visibility.Visible,
                    BuildCurrentTrainingWeightsComparison = BuildCurrentTrainingWeightsComparison,
                    UpdateTrainingComparisonViewModel = comparison => UpdateTrainingComparisonViewModel(comparison),
                    AppendLog = AppendLog,
                    YoloModelCenterTaskTabs = YoloModelCenterTaskTabs,
                    YoloModelCenterOverviewTaskTab = YoloModelCenterOverviewTaskTab,
                    YoloModelCenterDataTaskTab = YoloModelCenterDataTaskTab,
                    YoloModelCenterTrainingTaskTab = YoloModelCenterTrainingTaskTab,
                    YoloModelCenterRuntimeTaskTab = YoloModelCenterRuntimeTaskTab,
                    YoloSettingsScrollViewer = YoloSettingsScrollViewer,
                    YoloDatasetReadinessQuickPanel = YoloDatasetReadinessQuickPanel,
                    YoloRuntimeDetailsExpander = YoloStatusPanelControl?.RuntimeDetailsExpander,
                    ProjectConfigExpander = ProjectConfigPanelControl?.SettingsExpander,
                    YoloModelSettingsExpander = YoloModelSettingsPanelControl?.SettingsExpander,
                    TrainingSettingsExpander = TrainingSettingsPanelControl?.SettingsExpander,
                    YoloModelSettingsPanel = YoloModelSettingsPanelControl,
                    TrainingSettingsPanel = TrainingSettingsPanelControl
                });
            datasetPurposeWorkflowAdapter = new DatasetPurposeWorkflowAdapter(
                new DatasetPurposeWorkflowAdapterContext
                {
                    DataProvider = () => applicationState.Data,
                    RecipeNameProvider = GetCurrentRecipeName,
                    ProjectRecipeSessionService = projectRecipeSessionService,
                    ProjectRecipeApplyWorkflowService = projectRecipeApplyWorkflowService,
                    LearningWorkflowViewModel = viewModels.LearningWorkflowViewModel,
                    CanvasPanelViewModel = viewModels.CanvasPanelViewModel,
                    DatasetPurposeListBox = DatasetPurposeListBox,
                    Dispatcher = Dispatcher,
                    EnsureProjectSettings = EnsureProjectSettings,
                    RestoreBoxDrawingMethodFromProject = RestoreBoxDrawingMethodFromProject,
                    CancelFourPointBoxDraft = () => CancelFourPointBoxDraft(updateStatus: false),
                    RefreshCanvasAnnotationToolScope = RefreshCanvasAnnotationToolScope,
                    ApplyAnnotationToolSelection = ApplyAnnotationToolSelection,
                    RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext,
                    RefreshAnnotationVisibilityForDatasetPurpose = () => RefreshAnnotationVisibilityForDatasetPurpose(),
                    RefreshYoloTrainingStepCompletion = () => RefreshYoloTrainingStepCompletion(),
                    RefreshShellDatasetContext = RefreshShellDatasetContext,
                    GetCurrentDatasetPurpose = GetCurrentDatasetPurpose,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    AppendLog = AppendLog
                });
            ComposePanelViewModels();
            LearningWorkflowViewModel.PropertyChanged += LearningWorkflowViewModel_PropertyChanged;
            ApplyProjectDatasetPurposeToWorkflow();
            ConfigureShellCommands();
            ConfigureLearningWorkflowPanelCommands();
            ConfigureImageQueuePanelCommands();
            ConfigureCanvasPanelCommands();
            ConfigureObjectReviewPanelCommands();
            ConfigureClassCatalogPanelCommands();
            ConfigureProjectConfigPanelCommands();
            EnsureModelWorkflowPanelsComposed();
            PanelNameScopeRegistrar.Register(this);
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

        #region ShellInputLifecycleAdapterFacade
        // The Window keeps framework lifecycle hooks and dialog presentation. The
        // adapter owns startup, close policy, and ordered disposal decisions.
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!IsLoaded)
            {
                ApproveCrashRecoveryClose();
                base.OnClosing(e);
                return;
            }

            if (!isApplicationCloseApproved && !TryApproveApplicationClose())
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private void ExecuteShellPreviewKeyDownCommand(KeyInputCommandArgs e)
            => shellInputLifecycleAdapter?.ExecuteShellPreviewKeyDown(e);

        private void ExecuteLoadedCommand()
            => shellInputLifecycleAdapter?.ExecuteLoaded();

        private void ApplyDeferredShellStartup()
            => shellInputLifecycleAdapter?.ApplyDeferredStartup();

        private void LanguageViewModel_LanguageChanged(object sender, EventArgs e)
            => shellInputLifecycleAdapter?.HandleLanguageChanged(sender, e);

        private void ApplyLanguageChangedOnUi()
            => shellInputLifecycleAdapter?.ApplyLanguageChangedOnUiForShell();

        private bool TryApproveApplicationClose()
            => shellInputLifecycleAdapter?.TryApproveClose() == true;

        private ApplicationClosePlan BuildApplicationClosePlan()
            => shellInputLifecycleAdapter?.GetApplicationClosePlan();

        private ApplicationCloseState BuildApplicationCloseState()
            => shellInputLifecycleAdapter?.GetApplicationCloseState();

        private WpfApplicationCloseDecision ShowApplicationClosePrompt(ApplicationClosePlan plan)
            => shellInputLifecycleAdapter?.GetApplicationClosePrompt(plan) ?? WpfApplicationCloseDecision.Cancel;

        private bool ApplyApplicationCloseDecision(WpfApplicationCloseDecision decision)
            => shellInputLifecycleAdapter?.ApplyCloseDecision(decision) == true;

        private void ApproveCrashRecoveryClose()
            => shellInputLifecycleAdapter?.ApproveClose();

        private void ExecuteClosedCommand()
            => shellInputLifecycleAdapter?.ExecuteClosed();

        private void RefreshLocalizedPresentationForLanguage()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            ShellViewModel?.RefreshLocalizedPresentation();
            ClassCatalogViewModel?.RefreshLocalizedPresentation();
            ImageQueueViewModel?.RefreshLocalizedPresentation(imageQueueItems);
            StatusBarViewModel?.RefreshLocalizedPresentation();
            CanvasPanelControl?.RefreshLocalizedViewerStatus();
            if (ImageQueueFilterBox?.ItemsSource is IEnumerable<WpfImageQueueFilterOption> filterOptions)
            {
                foreach (WpfImageQueueFilterOption filterOption in filterOptions)
                {
                    filterOption?.RefreshLocalizedPresentation();
                }
            }

            RefreshShellDatasetContext();
            UpdateImageQueueStatusText();
            UpdateYoloCommandButtons();
            LocalizationTextRuntimeService.RefreshAll();
            CanvasPanelControl?.RefreshLocalizedViewerStatus();
        }

        private WpfApplicationCloseDecision ShowApplicationClosePromptForAdapter(ApplicationClosePlan plan)
        {
            bool canSave = plan.PromptKind == WpfApplicationClosePromptKind.SaveDiscardCancel;
            WpfMessageDialogResult result = WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = plan.Title,
                Message = plan.Message,
                Details = plan.Details,
                Kind = WpfMessageDialogKind.Warning,
                Buttons = canSave
                    ? WpfMessageDialogButtons.YesNoCancel
                    : WpfMessageDialogButtons.OKCancel,
                DefaultResult = WpfMessageDialogResult.Cancel,
                PrimaryButtonText = plan.PrimaryButtonText,
                SecondaryButtonText = plan.SecondaryButtonText,
                TertiaryButtonText = plan.TertiaryButtonText,
                MaxWidth = 620D
            });

            if (canSave)
            {
                return result switch
                {
                    WpfMessageDialogResult.Yes => WpfApplicationCloseDecision.SaveAndClose,
                    WpfMessageDialogResult.No => WpfApplicationCloseDecision.DiscardAndClose,
                    _ => WpfApplicationCloseDecision.Cancel
                };
            }

            return result == WpfMessageDialogResult.OK
                ? WpfApplicationCloseDecision.DiscardAndClose
                : WpfApplicationCloseDecision.Cancel;
        }

        private void ShowSaveFailureForAdapter(string failureDetails)
        {
            WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = "라벨을 저장하지 못했습니다",
                Message = "현재 이미지의 라벨 저장에 실패하여 창을 닫지 않았습니다.",
                Details = string.IsNullOrWhiteSpace(failureDetails)
                    ? "데이터셋 출력 경로와 파일 쓰기 권한을 확인한 뒤 다시 시도하세요."
                    : failureDetails,
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.OK,
                PrimaryButtonText = "확인"
            });
        }

        private WpfBatchDetectionPlan ShowBatchDetectionPreflight(
            IReadOnlyList<WpfImageQueueItem> items,
            string scopeText)
        {
            var viewModel = new WpfBatchDetectionPreflightViewModel(applicationState.Data, items, scopeText);
            var window = new WpfBatchDetectionPreflightWindow(viewModel)
            {
                Owner = this
            };
            window.ApplyThemeFrom(this);
            bool? accepted = window.ShowDialog();
            if (accepted != true || window.SelectedPlan == null)
            {
                AppendLog($"AI 배치 검사 취소: {scopeText}");
                return null;
            }

            AppendLog(
                $"AI 배치 사전점검 통과: {scopeText} · "
                + $"실행 {window.SelectedPlan.Items.Count}개 · "
                + "결과는 Candidate Review 대기, 자동 저장 없음");
            return window.SelectedPlan;
        }

        private void ShowTemplateAutoLabelGuide(string title, string message)
        {
            WpfMessageDialog.ShowInfo(
                this,
                string.IsNullOrWhiteSpace(title) ? "템플릿 안내" : title,
                message ?? string.Empty,
                "확인");
        }

        private async Task YieldBatchDetectionResultFrameOnUiAsync(CancellationToken token)
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

        private void DetachShellEventSubscriptions()
        {
            viewModels.LanguageViewModel.LanguageChanged -= LanguageViewModel_LanguageChanged;
            ShellViewModel.WorkspaceLayoutSaveFailed -= ShellViewModel_WorkspaceLayoutSaveFailed;
            ShellViewModel.WorkspaceLayoutReset -= ShellViewModel_WorkspaceLayoutReset;
            LearningWorkflowViewModel.PropertyChanged -= LearningWorkflowViewModel_PropertyChanged;
            ClassCatalogViewModel.MutationCompleted -= HandleClassCatalogMutationCompleted;
            ProjectConfigViewModel.ProjectConfigSaved -= HandleProjectConfigSaved;
            ProjectConfigViewModel.ProjectRecipeApplied -= HandleProjectRecipeApplied;
            ProjectConfigViewModel.WorkflowError -= HandleProjectConfigWorkflowError;
            ObjectReviewViewModel.WorkflowStatusChanged -= HandleObjectReviewWorkflowStatusChanged;
            MainCanvasViewModel.RoiAdded -= MainCanvasViewModel_RoiAdded;
            MainCanvasViewModel.RoiEditingCompleted -= MainCanvasViewModel_RoiEditingCompleted;
            MainCanvasViewModel.RoiMouseUp -= MainCanvasViewModel_RoiMouseUp;
            MainCanvasViewModel.RemoveRoiRequested -= MainCanvasViewModel_RemoveRoiRequested;
            MainCanvasViewModel.DetectionOverlayClicked -= MainCanvasViewModel_DetectionOverlayClicked;
            MainCanvasViewModel.ImagePointClicked -= MainCanvasViewModel_ImagePointClicked;
            MainCanvasViewModel.ImagePointHovered -= MainCanvasViewModel_ImagePointHovered;
            MainCanvasViewModel.ImagePointMoved -= MainCanvasViewModel_ImagePointMoved;
            MainCanvasViewModel.ImagePointReleased -= MainCanvasViewModel_ImagePointReleased;
            MainCanvasViewModel.RenderDiagnosticsCaptured -= MainCanvasViewModel_RenderDiagnosticsCaptured;
            MainCanvasView.SizeChanged -= MainCanvasView_SizeChanged;
        }
        #endregion

        private void ApplyProjectDatasetPurposeToWorkflow()
            => datasetPurposeWorkflowAdapter.ApplyProjectDatasetPurposeToWorkflow();

        private void ApplyDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
            => datasetPurposeWorkflowAdapter.ApplyDatasetPurposeToCurrentProject(purpose);

        private void ApplyPersistedDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
            => datasetPurposeWorkflowAdapter.ApplyPersistedDatasetPurposeToCurrentProject(purpose);

        #region DetectionWorkflowAdapterFacade
        private ImageDetectionCallbacks CreateImageDetectionCallbacks()
            => detectionWorkflowAdapter?.CreateImageDetectionCallbacks();

        private Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
            => detectionWorkflowAdapter?.RunInteractiveDetectionAsync(imagePath, allowSmokeFallback) ?? Task.CompletedTask;

        private Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken)
            => detectionWorkflowAdapter?.RunDetectionForImageAsync(imagePath, applyToCanvas, cancellationToken)
                ?? Task.FromResult(new YoloWorkerSmokeTestResult { ImagePath = imagePath ?? string.Empty });

        private Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
            => detectionWorkflowAdapter?.RunWorkerDetectionForImageAsync(
                imagePath,
                applyToCanvas,
                cancellationToken,
                connectTimeoutMilliseconds,
                workerReadyAlreadyChecked)
                ?? Task.FromResult(new YoloWorkerSmokeTestResult { ImagePath = imagePath ?? string.Empty });

        private void ApplyDetectionCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => detectionResultProjectionAdapter?.ApplyDetectionCandidates(candidates, succeeded);

        private void ApplyDetectionCandidatesPreservingConfirmed(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => detectionResultProjectionAdapter?.ApplyDetectionCandidatesPreservingConfirmed(candidates, succeeded);

        private void AddCandidateReviewHistory(string message)
            => detectionResultProjectionAdapter?.AddCandidateReviewHistory(message);

        private IWpfTemplateMatchingAutoLabelHost TemplateAutoLabelHost
            => templateMatchingAutoLabelHostAdapter;

        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelBusy => TemplateAutoLabelHost.IsAutoLabelBusy;
        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelCloseApproved => TemplateAutoLabelHost.IsAutoLabelCloseApproved;
        bool IWpfTemplateMatchingAutoLabelHost.HasActiveAutoLabelImage => TemplateAutoLabelHost.HasActiveAutoLabelImage;
        DrawingBitmap IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImage => TemplateAutoLabelHost.ActiveAutoLabelImage;
        string IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImagePath => TemplateAutoLabelHost.ActiveAutoLabelImagePath;
        LabelingProjectData IWpfTemplateMatchingAutoLabelHost.AutoLabelData => TemplateAutoLabelHost.AutoLabelData;
        int IWpfTemplateMatchingAutoLabelHost.MaximumTemplateMatchingCandidateCount => TemplateAutoLabelHost.MaximumTemplateMatchingCandidateCount;

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSource(
            out DrawingRectangle templateBounds,
            out string className)
            => TemplateAutoLabelHost.TryResolveTemplateMatchingSource(out templateBounds, out className);

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceSegment(
            out IReadOnlyList<DrawingPoint> points,
            out IReadOnlyList<IReadOnlyList<DrawingPoint>> cutouts)
            => TemplateAutoLabelHost.TryResolveTemplateMatchingSourceSegment(out points, out cutouts);

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceMask(
            out byte[] maskData,
            out DrawingSize maskSize,
            out DrawingRectangle maskBounds)
            => TemplateAutoLabelHost.TryResolveTemplateMatchingSourceMask(out maskData, out maskSize, out maskBounds);

        LabelClass IWpfTemplateMatchingAutoLabelHost.EnsureAutoLabelClassItem(string className)
            => TemplateAutoLabelHost.EnsureAutoLabelClassItem(className);

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetVisibleAutoLabelQueueItems()
            => TemplateAutoLabelHost.GetVisibleAutoLabelQueueItems();

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetAllAutoLabelQueueItems()
            => TemplateAutoLabelHost.GetAllAutoLabelQueueItems();

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.BuildAutoLabelBatchQueue(
            IEnumerable<WpfImageQueueItem> items)
            => TemplateAutoLabelHost.BuildAutoLabelBatchQueue(items);

        void IWpfTemplateMatchingAutoLabelHost.AppendAutoLabelLog(string message)
            => TemplateAutoLabelHost.AppendAutoLabelLog(message);

        void IWpfTemplateMatchingAutoLabelHost.ShowAutoLabelGuide(string title, string message)
            => TemplateAutoLabelHost.ShowAutoLabelGuide(title, message);

        int IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            DrawingRectangle? sourceSegmentBounds,
            IReadOnlyList<DrawingPoint> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<DrawingPoint>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            DrawingSize sourceMaskSize,
            DrawingRectangle sourceMaskBounds)
            => TemplateAutoLabelHost.ApplyAutoLabelCandidates(
                candidates,
                succeeded,
                sourceSegmentBounds,
                sourceSegmentPoints,
                sourceSegmentCutouts,
                sourceMaskData,
                sourceMaskSize,
                sourceMaskBounds);

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelPythonStatus(string text)
            => TemplateAutoLabelHost.SetAutoLabelPythonStatus(text);

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelCommandStatus(string text, bool isBusy)
            => TemplateAutoLabelHost.SetAutoLabelCommandStatus(text, isBusy);

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelGlobalInferenceStatus(string text, bool isBusy, bool isWarning)
            => TemplateAutoLabelHost.SetAutoLabelGlobalInferenceStatus(text, isBusy, isWarning);

        CancellationToken IWpfTemplateMatchingAutoLabelHost.StartAutoLabelBatch(int totalCount, string scopeText)
            => TemplateAutoLabelHost.StartAutoLabelBatch(totalCount, scopeText);

        void IWpfTemplateMatchingAutoLabelHost.MarkAutoLabelBatchItemRequested(WpfImageQueueItem item)
            => TemplateAutoLabelHost.MarkAutoLabelBatchItemRequested(item);

        void IWpfTemplateMatchingAutoLabelHost.UpdateAutoLabelBatchProgress(
            string scopeText,
            string currentFileName,
            int completedCount,
            int totalCount)
            => TemplateAutoLabelHost.UpdateAutoLabelBatchProgress(scopeText, currentFileName, completedCount, totalCount);

        void IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelBatchResult(
            WpfImageQueueItem item,
            TemplateMatchingBatchAutoLabelItemResult result,
            bool saveReviewStatus)
            => TemplateAutoLabelHost.ApplyAutoLabelBatchResult(item, result, saveReviewStatus);

        void IWpfTemplateMatchingAutoLabelHost.SaveAutoLabelReviewStatus()
            => TemplateAutoLabelHost.SaveAutoLabelReviewStatus();

        void IWpfTemplateMatchingAutoLabelHost.CompleteAutoLabelBatch(
            bool canceled,
            int completedCount,
            int totalCount,
            string scopeText)
            => TemplateAutoLabelHost.CompleteAutoLabelBatch(canceled, completedCount, totalCount, scopeText);

        void IWpfTemplateMatchingAutoLabelHost.NotifyAutoLabelDataChanged()
            => TemplateAutoLabelHost.NotifyAutoLabelDataChanged();

        Task IWpfTemplateMatchingAutoLabelHost.YieldAutoLabelBatchFrameAsync(CancellationToken token)
            => TemplateAutoLabelHost.YieldAutoLabelBatchFrameAsync(token);
        #endregion

        #region BatchDetectionWorkflowAdapterFacade
        private void ExecuteDetectSelectedQueueCommand()
            => batchDetectionWorkflowAdapter?.ExecuteDetectSelectedQueueCommand();

        private Task ExecuteDetectSelectedQueueCommandAsync()
            => batchDetectionWorkflowAdapter?.ExecuteDetectSelectedQueueCommandAsync() ?? Task.CompletedTask;

        private void ExecuteBatchDetectQueueCommand()
            => batchDetectionWorkflowAdapter?.ExecuteBatchDetectQueueCommand();

        private Task ExecuteBatchDetectQueueCommandAsync()
            => batchDetectionWorkflowAdapter?.ExecuteBatchDetectQueueCommandAsync() ?? Task.CompletedTask;

        private void ExecuteRetryFailedQueueCommand()
            => batchDetectionWorkflowAdapter?.ExecuteRetryFailedQueueCommand();

        private Task ExecuteRetryFailedQueueCommandAsync()
            => batchDetectionWorkflowAdapter?.ExecuteRetryFailedQueueCommandAsync() ?? Task.CompletedTask;

        private void ExecuteStopBatchQueueCommand()
            => batchDetectionWorkflowAdapter?.ExecuteStopBatchQueueCommand();

        private void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
            => batchDetectionWorkflowAdapter?.ApplyDetectionResultToQueueItem(
                item,
                result,
                saveReviewStatus,
                refreshQueueView,
                updateQueueStatusText);

        private IReadOnlyList<WpfImageQueueItem> GetVisibleQueueItems()
            => batchDetectionWorkflowAdapter?.GetVisibleQueueItems() ?? Array.Empty<WpfImageQueueItem>();

        private void UpdateBatchDetectionControls(string scopeText = "", string currentFileName = "")
            => batchDetectionWorkflowAdapter?.UpdateBatchDetectionControls(scopeText, currentFileName);

        private bool ShowBatchDetectionImage(WpfImageQueueItem item)
            => batchDetectionWorkflowAdapter?.ShowBatchDetectionImage(item) == true;

        private bool IsActiveImagePath(string imagePath)
            => batchDetectionWorkflowAdapter?.IsActiveImagePath(imagePath) == true;

        private bool ApplyBatchDetectionResultToCanvas(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
            => batchDetectionWorkflowAdapter?.ApplyBatchDetectionResultToCanvas(item, result) == true;

        private Task YieldBatchDetectionResultFrameAsync(CancellationToken token)
            => batchDetectionWorkflowAdapter?.YieldBatchDetectionResultFrameAsync(token) ?? Task.CompletedTask;

        private void ShowBatchNoCandidateResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
            => batchDetectionWorkflowAdapter?.ShowBatchNoCandidateResult(item, result);

        private void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
            => batchDetectionWorkflowAdapter?.ShowBatchDetectionFailureResult(item, result);

        private void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
            => batchDetectionWorkflowAdapter?.ApplyBatchDetectionCandidates(candidates, succeeded);

        private Task RunBatchDetectionAsync(IReadOnlyList<WpfImageQueueItem> items, string scopeText)
            => batchDetectionWorkflowAdapter?.RunBatchDetectionAsync(items, scopeText) ?? Task.CompletedTask;

        private Action CaptureBatchReviewStatusSave()
            => batchDetectionWorkflowAdapter?.CaptureBatchReviewStatusSave() ?? (() => { });

        private void PresentBatchDetectionCompletion(BatchDetectionRun run, string modelSourceText)
            => batchDetectionWorkflowAdapter?.PresentBatchDetectionCompletion(run, modelSourceText);
        #endregion

        #region DatasetSetupWorkflowAdapterFacade
        private void ExecuteStartDatasetSetupCommand(object selectedPurpose)
            => datasetSetupWorkflowAdapter?.ExecuteStartDatasetSetupCommand(selectedPurpose);

        private void ExecuteChangeDatasetCommand()
            => datasetSetupWorkflowAdapter?.ExecuteChangeDatasetCommand();

        private Task ExecuteChangeDatasetCommandAsync()
            => datasetSetupWorkflowAdapter?.ExecuteChangeDatasetCommandAsync() ?? Task.CompletedTask;

        private DatasetSetupWizardPathCallbacks CreateDatasetSetupPathSelectionCallbacks()
            => datasetSetupWorkflowAdapter?.CreateDatasetSetupPathSelectionCallbacks();

        private WpfDatasetSetupWizardViewModel CreateDatasetSetupWizardViewModel(object selectedPurpose)
            => datasetSetupWorkflowAdapter?.CreateDatasetSetupWizardViewModel(selectedPurpose);

        private bool ApplyDatasetSetupRequest(WpfDatasetSetupRequest request)
            => datasetSetupWorkflowAdapter?.ApplyDatasetSetupRequest(request) == true;

        private bool TryRestoreLastOpenedDatasetOnStartup()
            => datasetSetupWorkflowAdapter?.TryRestoreLastOpenedDatasetOnStartup() == true;

        private string ResolveActiveDatasetImageRoot()
            => datasetSetupWorkflowAdapter?.ResolveActiveDatasetImageRoot() ?? string.Empty;

        private void CompleteSelectedDatasetRecipeSwitch()
            => datasetSetupWorkflowAdapter?.CompleteSelectedDatasetRecipeSwitch();

        private void ExecuteOpenDatasetRootFolderCommand()
            => datasetSetupWorkflowAdapter?.ExecuteOpenDatasetRootFolderCommand();

        private void RefreshShellDatasetContext()
            => datasetSetupWorkflowAdapter?.RefreshShellDatasetContext();

        private void ClearImageQueueAfterDatasetSwitch(string imageRootPath)
            => datasetSetupWorkflowAdapter?.ClearImageQueueAfterDatasetSwitch(imageRootPath);

        private static void RememberLastOpenedDatasetRecipe(string recipeName)
            => ProjectRecipeService.SaveLastOpenedRecipeName(ProjectRecipeService.GetRecipeRootDirectory(), recipeName);
        #endregion

        #region TrainingRuntimeAdapterFacade
        private void UpdateTrainingProgressFromWorker()
            => trainingRuntimeAdapter?.UpdateTrainingProgressFromWorker();

        private void UpdateTrainingProgressFromWorker(TrainingRuntimeStatusSnapshot snapshot)
            => trainingRuntimeAdapter?.UpdateTrainingProgressFromWorker(snapshot);

        private void StartTrainingStatusPolling()
            => trainingRuntimeAdapter?.StartTrainingStatusPolling();

        private void StopTrainingStatusPolling()
            => trainingRuntimeAdapter?.StopTrainingStatusPolling();

        private void TrainingStatusPollTimer_Tick(object sender, EventArgs e)
            => trainingRuntimeAdapter?.HandleTrainingStatusPollTimerTick();

        private void RefreshTrainingReadinessPanel(bool refreshYaml)
            => trainingRuntimeAdapter?.RefreshTrainingReadinessPanel(refreshYaml);

        private void RefreshExternalTrainingReadinessPanel(YoloExternalDatasetIntakeReport externalReport)
            => trainingRuntimeAdapter?.RefreshExternalTrainingReadinessPanel(externalReport);

        private TrainingRuntimeCallbacks CreateTrainingRuntimeCallbacks()
            => trainingRuntimeAdapter?.CreateTrainingRuntimeCallbacks() ?? new TrainingRuntimeCallbacks();

        private void UpdateTrainingStatusVisual(PythonCommunicationStatus status, YoloDatasetReadinessReport report = null)
            => trainingRuntimeAdapter?.UpdateTrainingStatusVisual(status, report);
        #endregion

        #region ObjectReviewStateAdapterFacade
        private void RestoreObjectMetadataTagsFromProject()
            => objectReviewStateAdapter?.RestoreObjectMetadataTagsFromProject();

        private void LoadObjectMetadataForActiveImage(string imagePath)
            => objectReviewStateAdapter?.LoadObjectMetadataForActiveImage(imagePath);

        private bool TrySaveCurrentObjectMetadata(string imageName)
            => objectReviewStateAdapter?.TrySaveCurrentObjectMetadata(imageName) == true;

        private void CancelObjectGroupSelection(bool updateStatus)
            => objectReviewStateAdapter?.CancelObjectGroupSelection(updateStatus);

        private bool ApplyCreatedObjectGroupMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyCreatedObjectGroupMutation(workflowResult) == true;

        private bool ApplyObjectGroupMemberRemovalMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectGroupMemberRemovalMutation(workflowResult) == true;

        private bool ConfirmObjectGroupDissolve(int memberCount)
        {
            WpfMessageDialogResult result = WpfMessageDialog.Confirm(
                this,
                "검수 그룹 해제",
                $"구성원 {memberCount}개의 그룹 관계를 해제합니다. 객체와 라벨은 삭제하지 않습니다.",
                "그룹 해제",
                "취소");
            return result == WpfMessageDialogResult.Yes;
        }

        private bool ApplyObjectGroupDissolveMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectGroupDissolveMutation(workflowResult) == true;

        private bool ApplyObjectGroupOccludedMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectGroupOccludedMutation(workflowResult) == true;

        private bool ApplyObjectGroupTagMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectGroupTagMutation(workflowResult) == true;

        private bool ApplyObjectPersistentOccludedMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectPersistentOccludedMutation(workflowResult) == true;

        private bool ApplyObjectPersistentTagMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectPersistentTagMutation(workflowResult) == true;

        private bool ApplyObjectRecipeMetadataResetMutation(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ApplyObjectRecipeMetadataResetMutation(workflowResult) == true;

        private IReadOnlyList<WpfObjectReviewObjectSnapshot> CaptureObjectReviewSnapshots()
            => objectReviewStateAdapter?.CaptureObjectReviewSnapshots()
                ?? Array.Empty<WpfObjectReviewObjectSnapshot>();

        private void ReportObjectReviewWorkflowError(WpfObjectReviewMutationResult workflowResult)
            => objectReviewStateAdapter?.ReportObjectReviewWorkflowError(workflowResult);

        private void ApplyObjectPersistentMetadata(IEnumerable<WpfObjectReviewListItem> rows)
            => objectReviewStateAdapter?.ApplyObjectPersistentMetadata(rows);

        private WpfObjectSessionState ApplyObjectSessionStateMutation(
            WpfObjectReviewItemRef item,
            WpfObjectSessionStateKind kind)
            => objectReviewStateAdapter?.ApplyObjectSessionStateMutation(item, kind)
                ?? WpfObjectSessionState.Default;

        private WpfObjectSessionState GetObjectSessionState(WpfObjectReviewItemRef item)
            => objectReviewStateAdapter?.GetObjectSessionState(item)
                ?? WpfObjectSessionState.Default;

        private WpfObjectSessionState GetManualRoiSessionState(int index)
            => objectReviewStateAdapter?.GetManualRoiSessionState(index)
                ?? WpfObjectSessionState.Default;

        private WpfObjectSessionState GetManualSegmentSessionState(int index)
            => objectReviewStateAdapter?.GetManualSegmentSessionState(index)
                ?? WpfObjectSessionState.Default;

        private bool CanMutateSelectedObject(
            WpfObjectReviewItemRef item,
            bool requireVisible,
            out string error)
        {
            if (objectReviewStateAdapter == null)
            {
                error = "객체 검수 상태를 초기화할 수 없습니다.";
                return false;
            }

            return objectReviewStateAdapter.CanMutateSelectedObject(item, requireVisible, out error);
        }

        private bool CanEditManualSegment(LabelingSegmentationObject segment)
            => objectReviewStateAdapter?.CanEditManualSegment(segment) == true;
        #endregion

        #region ObjectReviewInteractionAdapterFacade
        private void HandleObjectReviewWorkflowStatusChanged(string status)
            => objectReviewInteractionAdapter?.HandleObjectReviewWorkflowStatusChanged(status);

        private void RefreshObjectList()
            => objectReviewInteractionAdapter?.RefreshObjectList();

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef preferredSelection)
            => objectReviewInteractionAdapter?.RefreshObjectListWithSelection(preferredSelection);

        private WpfObjectReviewListItem BuildManualRoiObjectReviewItem(int index)
            => objectReviewInteractionAdapter?.BuildManualRoiObjectReviewItem(index);

        private bool TryRefreshManualRoiObjectReviewRow(int manualRoiIndex, bool select)
            => objectReviewInteractionAdapter?.TryRefreshManualRoiObjectReviewRow(manualRoiIndex, select) == true;

        private WpfObjectReviewListItem BuildManualSegmentObjectReviewItem(int manualSegmentIndex)
            => objectReviewInteractionAdapter?.BuildManualSegmentObjectReviewItem(manualSegmentIndex);

        private bool TryRefreshManualSegmentObjectReviewRow(int manualSegmentIndex, string summary, bool select)
            => objectReviewInteractionAdapter?.TryRefreshManualSegmentObjectReviewRow(manualSegmentIndex, summary, select) == true;

        private string GetManualRoiClassName(int index)
            => objectReviewInteractionAdapter?.GetManualRoiClassName(index) ?? string.Empty;

        private void SyncObjectClassEditorToSelection()
            => objectReviewInteractionAdapter?.SyncObjectClassEditorToSelection();

        private void RefreshObjectClassOptions(string selectedName = "")
            => objectReviewInteractionAdapter?.RefreshObjectClassOptions(selectedName);

        private void ExecuteObjectSelectionChangedCommand(object selectedItem)
            => objectReviewInteractionAdapter?.ExecuteObjectSelectionChangedCommand(selectedItem);

        private void ExecuteApplyObjectClassCommand()
            => objectReviewInteractionAdapter?.ExecuteApplyObjectClassCommand();

        private void ExecuteDeleteObjectCommand()
            => objectReviewInteractionAdapter?.ExecuteDeleteObjectCommand();

        private void ExecuteMergeSelectedSegmentsCommand()
            => objectReviewInteractionAdapter?.ExecuteMergeSelectedSegmentsCommand();

        private bool DeleteSelectedObject()
            => objectReviewInteractionAdapter?.DeleteSelectedObject() == true;

        private bool TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
        {
            if (objectReviewInteractionAdapter == null)
            {
                item = null;
                return false;
            }

            return objectReviewInteractionAdapter.TryGetSelectedObjectReviewItem(out item);
        }

        private void RefreshObjectReviewAfterDelete(WpfObjectReviewSource deletedSource, int deletedObjectRowIndex)
            => objectReviewInteractionAdapter?.RefreshObjectReviewAfterDelete(deletedSource, deletedObjectRowIndex);
        #endregion

        #region ProjectSettingsWorkflowAdapterFacade
        private bool IsProjectRecipeSelectionRefreshActive()
            => projectSettingsWorkflowAdapter.IsRecipeSelectionRefreshActive;

        private void SetProjectConfigStatus(string message)
            => projectSettingsWorkflowAdapter.SetProjectConfigStatus(message);

        private void HandleProjectConfigSaved(string configPath)
            => projectSettingsWorkflowAdapter.HandleProjectConfigSaved(configPath);

        private void HandleProjectRecipeApplied(string recipeName, ProjectRecipeApplyResult result)
            => projectSettingsWorkflowAdapter.HandleProjectRecipeApplied(recipeName, result);

        private void HandleProjectConfigWorkflowError(string message)
            => projectSettingsWorkflowAdapter.HandleProjectConfigWorkflowError(message);

        private string GetCurrentRecipeName()
            => projectSettingsWorkflowAdapter.GetCurrentRecipeName();

        private string GetCurrentRecipeConfigDirectory()
            => projectSettingsWorkflowAdapter.GetCurrentRecipeConfigDirectory();

        private string GetCurrentRecipeConfigPath()
            => projectSettingsWorkflowAdapter.GetCurrentRecipeConfigPath();

        private bool TryPickFile(string title, string filter, string currentPath, out string selectedPath)
            => projectSettingsWorkflowAdapter.TryPickFile(title, filter, currentPath, out selectedPath);

        private bool TryPickFolder(string title, string currentPath, out string selectedPath)
            => projectSettingsWorkflowAdapter.TryPickFolder(title, currentPath, out selectedPath);

        private void PopulateProjectConfigPanelFields()
            => projectSettingsWorkflowAdapter.PopulateProjectConfigPanelFields();

        private bool PopulateProjectRecipeList(string selectedRecipeName)
            => projectSettingsWorkflowAdapter.PopulateProjectRecipeList(selectedRecipeName);

        private ProjectConfigNavigationCallbacks CreateProjectConfigNavigationCallbacks()
            => projectSettingsWorkflowAdapter.CreateProjectConfigNavigationCallbacks();

        private ProjectConfigArchiveCallbacks CreateProjectConfigArchiveCallbacks()
            => projectSettingsWorkflowAdapter.CreateProjectConfigArchiveCallbacks();

        private void OpenProjectConfigFolder()
            => projectSettingsWorkflowAdapter.OpenProjectConfigFolder();

        private bool SaveProjectConfigFromPanel()
            => projectSettingsWorkflowAdapter.SaveProjectConfigFromPanel();

        private bool SaveModelMetadataConfigFromPanel()
            => projectSettingsWorkflowAdapter.SaveModelMetadataConfigFromPanel();

        private bool SaveProjectConfigFromPanelCore(Func<string, string> saveRecipe)
            => projectSettingsWorkflowAdapter.SaveProjectConfigFromPanelCore(saveRecipe);

        private Task<bool> ApplyProjectRecipeFromPanelAsync()
            => projectSettingsWorkflowAdapter.ApplyProjectRecipeFromPanelAsync();

        private void CompleteProjectRecipeApply(string previousRecipeName, string recipeName)
            => projectSettingsWorkflowAdapter.CompleteProjectRecipeApply(previousRecipeName, recipeName);

        private string BuildLabelPathSummary()
            => projectSettingsWorkflowAdapter.BuildLabelPathSummary();

        private void EnsureProjectSettings()
            => projectSettingsWorkflowAdapter.EnsureProjectSettings();
        #endregion

        #region AnnotationWorkflowCommandAdapterFacade
        private void ExecuteLoadSampleCommand()
            => annotationWorkflowCommandAdapter.ExecuteLoadSampleCommand();

        private void ExecuteAddSampleRoiCommand()
            => annotationWorkflowCommandAdapter.ExecuteAddSampleRoiCommand();

        private void ExecuteSaveAnnotationsCommand()
            => annotationWorkflowCommandAdapter.ExecuteSaveAnnotationsCommand();

        private void OpenNextIncompleteQueueImageAfterSave(string completedImagePath)
            => annotationWorkflowCommandAdapter.OpenNextIncompleteQueueImageAfterSave(completedImagePath);

        private void ExecuteCompleteNoObjectAndNextCommand()
            => annotationWorkflowCommandAdapter.ExecuteCompleteNoObjectAndNextCommand();
        #endregion

        #region CandidateReviewStateAdapterFacade
        private YoloWorkerSmokeCandidate GetSelectedCandidate()
            => candidateReviewStateAdapter.GetSelectedCandidate();

        private void UpdateDetectionResultOverlay()
            => candidateReviewStateAdapter.UpdateDetectionResultOverlay();

        private void MainCanvasViewModel_DetectionOverlayClicked(object sender, int candidateIndex)
            => candidateReviewStateAdapter.MainCanvasViewModel_DetectionOverlayClicked(sender, candidateIndex);

        private void ExecuteCandidateConfidenceChangedCommand(double confidence)
            => candidateReviewStateAdapter.ExecuteCandidateConfidenceChangedCommand(confidence);

        private void RefreshCandidateList()
            => candidateReviewStateAdapter.RefreshCandidateList();

        private void RefreshCandidateListWithPreferred(YoloWorkerSmokeCandidate preferredCandidate)
            => candidateReviewStateAdapter.RefreshCandidateListWithPreferred(preferredCandidate);

        private void UpdateCandidateActionState()
            => candidateReviewStateAdapter.UpdateCandidateActionState();

        private void UpdateCanvasCommandButtons()
            => candidateReviewStateAdapter.UpdateCanvasCommandButtons();

        private string BuildCandidateConfirmDisabledHintText(YoloWorkerSmokeCandidate candidate)
            => candidateReviewStateAdapter.BuildCandidateConfirmDisabledHintText(candidate);

        private IReadOnlyList<YoloWorkerSmokeCandidate> GetVisibleCandidateList()
            => candidateReviewStateAdapter.GetVisibleCandidateList();

        private double GetCandidateConfidenceFilter()
            => candidateReviewStateAdapter.GetCandidateConfidenceFilter();

        private float GetMinimumDetectionConfidence()
            => candidateReviewStateAdapter.GetMinimumDetectionConfidence();

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(YoloWorkerSmokeCandidate candidate)
            => candidateReviewStateAdapter.GetCandidateOverlapInfo(candidate);

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(DrawingRectangle candidateBounds)
            => candidateReviewStateAdapter.GetCandidateOverlapInfo(candidateBounds);

        private bool IsCandidateConfirmable(YoloWorkerSmokeCandidate candidate)
            => candidateReviewStateAdapter.IsCandidateConfirmable(candidate);

        private bool IsCandidateHighOverlap(YoloWorkerSmokeCandidate candidate)
            => candidateReviewStateAdapter.IsCandidateHighOverlap(candidate);

        private void UpdateCandidateConfidenceText()
            => candidateReviewStateAdapter?.UpdateCandidateConfidenceText();

        private void ApplyCandidateSelectionReview(YoloWorkerSmokeCandidate candidate)
            => candidateReviewStateAdapter.ApplyCandidateSelectionReview(candidate);

        private void ExecuteTogglePatchCoreHeatmapCommand()
            => candidateReviewStateAdapter.ExecuteTogglePatchCoreHeatmapCommand();

        private void ClosePatchCoreHeatmapWindow()
            => candidateReviewStateAdapter.ClosePatchCoreHeatmapWindow();
        #endregion

        #region CandidateReviewActionAdapterFacade
        private void ExecuteConfirmSelectedCandidateCommand()
            => candidateReviewActionAdapter.ExecuteConfirmSelectedCandidateCommand();

        private void ExecuteConfirmAllCandidatesCommand()
            => candidateReviewActionAdapter.ExecuteConfirmAllCandidatesCommand();

        private void ExecuteSkipSelectedCandidateCommand()
            => candidateReviewActionAdapter.ExecuteSkipSelectedCandidateCommand();

        private void ExecuteCompleteImageAndNextCommand()
            => candidateReviewActionAdapter.ExecuteCompleteImageAndNextCommand();

        private void ApplyCandidateSelectionChangedEffects(WpfCandidateReviewListItem selectedItem)
            => candidateReviewActionAdapter.ApplyCandidateSelectionChangedEffects(selectedItem);

        private void ConfirmCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, string scope)
            => candidateReviewActionAdapter.ConfirmCandidates(candidates, scope);

        private void EnsureConfirmedCandidateClassItems(IEnumerable<YoloWorkerSmokeCandidate> candidates)
            => candidateReviewActionAdapter.EnsureConfirmedCandidateClassItems(candidates);

        private void ExecutePreviousCandidateCommand()
            => candidateReviewActionAdapter.ExecutePreviousCandidateCommand();

        private void ExecuteNextCandidateCommand()
            => candidateReviewActionAdapter.ExecuteNextCandidateCommand();

        private void ExecuteOpenModelComparisonExampleCommand(WpfModelComparisonReviewExample example)
            => candidateReviewActionAdapter.ExecuteOpenModelComparisonExampleCommand(example);

        private void FocusModelComparisonExampleInViewer(WpfModelComparisonReviewExample example)
            => candidateReviewActionAdapter.FocusModelComparisonExampleInViewer(example);

        private DrawingRectangle BuildModelComparisonExampleBounds(WpfModelComparisonReviewExample example)
            => candidateReviewActionAdapter.BuildModelComparisonExampleBounds(example);

        private static string BuildModelComparisonExampleLabel(WpfModelComparisonReviewExample example)
            => CandidateReviewActionAdapter.BuildModelComparisonExampleLabel(example);

        private void SelectCandidateOffset(int offset)
            => candidateReviewActionAdapter.SelectCandidateOffset(offset);

        private YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
            YoloWorkerSmokeCandidate current,
            IEnumerable<YoloWorkerSmokeCandidate> removingCandidates)
            => candidateReviewActionAdapter.FindNextVisibleCandidateAfter(current, removingCandidates);
        #endregion

        #region ImageQueueReviewAdapterFacade
        private void SelectImageQueueItem(string imagePath)
            => imageQueueReviewAdapter.SelectImageQueueItem(imagePath);

        private WpfImageQueueItem FindImageQueueItem(string imagePath)
            => imageQueueReviewAdapter.FindImageQueueItem(imagePath);

        private void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
            => imageQueueReviewAdapter.ApplyReviewStatusToItem(item, status);

        private void ApplyReviewStatusToItemCore(
            WpfImageQueueItem item,
            YoloImageReviewStatus status,
            bool refreshTrainingStepCompletion)
            => imageQueueReviewAdapter.ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion);

        private void UpdateImageQueueStatusText(int loadedCount = -1, int totalToLoad = -1)
            => imageQueueReviewAdapter.UpdateImageQueueStatusText(loadedCount, totalToLoad);

        private void UpdateQueueQuickFilterButtons()
            => imageQueueReviewAdapter.UpdateQueueQuickFilterButtons();

        private WpfImageQueueFilter GetSelectedImageQueueFilter()
            => imageQueueReviewAdapter.GetSelectedImageQueueFilter();

        private string GetImageQueueSearchText()
            => imageQueueReviewAdapter.GetImageQueueSearchText();

        // Existing XAML/test entry points remain thin facades; queue selection and
        // search policy is owned by ImageQueueReviewAdapter.
        private void ImageQueueFilterBox_SelectionChanged(object sender, object selectedItem)
            => imageQueueReviewAdapter.ApplyFilterSelectionChanged();

        private void SetImageQueueFilter(WpfImageQueueFilter filter)
            => imageQueueReviewAdapter.SetImageQueueFilter(filter);

        private void ApplyImageQueueSearchChanged(string searchText)
            => imageQueueReviewAdapter.ApplySearchChanged(searchText);

        private void SelectSingleVisibleQueueSearchResult()
            => imageQueueReviewAdapter.SelectSingleVisibleQueueSearchResult();

        private void ExecuteSelectedQueueItemChanged(WpfImageQueueItem item)
            => imageQueueReviewAdapter.ExecuteSelectedQueueItemChanged(item);

        private ImageQueueOpenSelection GetOpenSelectedQueueSelection()
            => imageQueueReviewAdapter.GetOpenSelectedQueueSelection();

        private WpfImageQueueItem FindSingleSearchMatchedQueueItem()
            => imageQueueReviewAdapter.FindSingleSearchMatchedQueueItem();

        private string BuildOpenQueueSelectionFailureMessage()
            => imageQueueReviewAdapter.BuildOpenQueueSelectionFailureMessage();

        private int CountVisibleQueueItems(int limit)
            => imageQueueReviewAdapter.CountVisibleQueueItems(limit);

        private int CountSearchMatchedQueueItems(string searchText, int limit)
            => imageQueueReviewAdapter.CountSearchMatchedQueueItems(searchText, limit);

        private void UpdateSelectedQueueImageButton(WpfImageQueueItem item)
            => imageQueueReviewAdapter.UpdateSelectedQueueImageButton(item);

        private bool CanOpenQueueItem(WpfImageQueueItem item)
            => imageQueueNavigationAdapter.CanOpenQueueItem(item);

        private void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
            => imageQueueReviewAdapter.RefreshActiveImageQueueStatus(hasActiveCandidates);

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
            => imageQueueReviewAdapter.QueueActiveImageQueueStatusRefresh(hasActiveCandidates);

        private bool IsActiveImageQueueSaveRequired(WpfImageQueueItem item)
            => imageQueueReviewAdapter.IsActiveImageQueueSaveRequired(item);

        private void ApplyActiveImageQueueSaveRequiredStatus(string reason)
            => imageQueueReviewAdapter.ApplyActiveImageQueueSaveRequiredStatus(reason);

        private void SetActiveImageDetectionStatus(int candidateCount, bool succeeded)
            => imageQueueReviewAdapter.SetActiveImageDetectionStatus(candidateCount, succeeded);

        private bool ApplyActiveAnomalyClassification(IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
            => imageQueueReviewAdapter.ApplyActiveAnomalyClassification(candidates);

        private bool ApplyAnomalyClassificationToImage(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool saveReviewStatus)
            => imageQueueReviewAdapter.ApplyAnomalyClassificationToImage(imagePath, imageName, candidates, saveReviewStatus);

        private void MarkActiveImageConfirmed()
            => imageQueueReviewAdapter.MarkActiveImageConfirmed();

        private void MarkActiveImageNoCandidate()
            => imageQueueReviewAdapter.MarkActiveImageNoCandidate();

        private void MarkActiveImageSkippedOrCandidate()
            => imageQueueReviewAdapter.MarkActiveImageSkippedOrCandidate();

        private void ExecuteMarkQualityUnreviewedCommand()
            => imageQueueReviewAdapter.ExecuteMarkQualityUnreviewedCommand();

        private void ExecuteMarkQualityNeedsFixCommand()
            => imageQueueReviewAdapter.ExecuteMarkQualityNeedsFixCommand();

        private void ExecuteMarkQualityReviewedCommand()
            => imageQueueReviewAdapter.ExecuteMarkQualityReviewedCommand();

        private void ExecuteExportQualityReviewReportCommand()
            => imageQueueReviewAdapter.ExecuteExportQualityReviewReportCommand();

        private void SetActiveImageQualityReviewState(YoloImageQualityReviewState state)
            => imageQueueReviewAdapter.SetActiveImageQualityReviewState(state);

        private void InvalidateActiveImageQualityReviewAfterEdit()
            => imageQueueReviewAdapter.InvalidateActiveImageQualityReviewAfterEdit();

        private void RefreshActiveImageQualityReviewPresentation()
            => imageQueueReviewAdapter.RefreshActiveImageQualityReviewPresentation();

        private void RefreshActiveImageQualityReviewPresentation(
            WpfImageQueueItem item,
            YoloImageReviewStatus status)
            => imageQueueReviewAdapter.RefreshActiveImageQualityReviewPresentation(item, status);

        private bool IsLabelQualityReviewPurpose()
            => imageQueueReviewAdapter.IsLabelQualityReviewPurpose();

        private bool IsAnomalyDatasetPurpose()
            => imageQueueReviewAdapter.IsAnomalyDatasetPurpose();

        private void MarkActiveAnomalyImageNormal()
            => imageQueueReviewAdapter.MarkActiveAnomalyImageNormal();

        private void MarkActiveAnomalyImageAbnormal()
            => imageQueueReviewAdapter.MarkActiveAnomalyImageAbnormal();

        private void MarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
            => imageQueueReviewAdapter.MarkActiveAnomalyImageReviewState(state);

        private bool TryMarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
            => imageQueueReviewAdapter.TryMarkActiveAnomalyImageReviewState(state);

        private void ExecuteMarkActiveAnomalyNormalAndNextCommand()
            => imageQueueReviewAdapter.ExecuteMarkActiveAnomalyNormalAndNextCommand();

        private void ExecuteMarkActiveAnomalyAbnormalAndNextCommand()
            => imageQueueReviewAdapter.ExecuteMarkActiveAnomalyAbnormalAndNextCommand();

        private void ExecuteClearActiveAnomalyReviewCommand()
            => imageQueueReviewAdapter.ExecuteClearActiveAnomalyReviewCommand();

        private void MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState state)
            => imageQueueReviewAdapter.MarkActiveAnomalyImageAndOpenNext(state);

        private bool MarkAnomalyImageReviewState(
            string imagePath,
            string imageName,
            AnomalyImageReviewState state,
            bool saveReviewStatus)
            => imageQueueReviewAdapter.MarkAnomalyImageReviewState(imagePath, imageName, state, saveReviewStatus);

        private void RefreshImageQueuePurposePresentation()
            => imageQueueReviewAdapter.RefreshImageQueuePurposePresentation();
        #endregion

        #region ImageQueueCommandFacade
        private void ExecuteLoadImageRootQueueCommand()
            => imageQueueRootCommandAdapter.ExecuteLoadImageRootCommand();

        private void ExecuteBrowseImageFolderCommand()
            => imageQueueRootCommandAdapter.ExecuteBrowseImageFolderCommand();

        private void SaveCurrentImageRootToRecipe(string selectedPath)
            => imageQueueRootCommandAdapter.SaveCurrentImageRootToRecipe(selectedPath);

        private void ExecuteOpenCurrentImageFolderCommand()
            => imageQueueRootCommandAdapter.ExecuteOpenCurrentImageFolderCommand();

        private void ExecuteRefreshImageQueueCommand()
            => imageQueueRootCommandAdapter.ExecuteRefreshImageQueueCommand();

        private void ExecuteNextUnlabeledQueueCommand()
            => imageQueueNavigationAdapter.ExecuteNextUnlabeledQueueCommand();

        private bool TryOpenNextIncompleteQueueImage()
            => imageQueueNavigationAdapter.TryOpenNextIncompleteQueueImage();

        private bool TryOpenNextIncompleteQueueImage(string currentImagePath)
            => imageQueueNavigationAdapter.TryOpenNextIncompleteQueueImage(currentImagePath);

        private void ExecuteOpenSelectedQueueImageCommand()
            => imageQueueNavigationAdapter.ExecuteOpenSelectedQueueImageCommand();

        private bool TryOpenSelectedQueueImage(bool skipIfAlreadyActive = false)
            => imageQueueNavigationAdapter.TryOpenSelectedQueueImage(skipIfAlreadyActive);

        private bool TryOpenSelectedQueueImage(WpfImageQueueItem item, bool skipIfAlreadyActive = false)
            => imageQueueNavigationAdapter.TryOpenSelectedQueueImage(item, skipIfAlreadyActive);

        private bool TryOpenSelectedQueueImage(ImageQueueOpenSelection selection, bool skipIfAlreadyActive = false)
            => imageQueueNavigationAdapter.TryOpenSelectedQueueImage(selection, skipIfAlreadyActive);

        private bool TryOpenAdjacentQueueImage(int direction)
            => imageQueueNavigationAdapter.TryOpenAdjacentQueueImage(direction);

        #region ImageQueueCatalogLoadFacade
        public int LoadImageQueueFromRoot(
            string imageRoot,
            string selectedImagePath = "",
            bool loadFirstImage = false,
            bool refreshDetails = true)
            => imageQueueCatalogLoadAdapter.Load(imageRoot, selectedImagePath, loadFirstImage, refreshDetails);

        public Task<int> LoadImageQueueFromRootAsync(
            string imageRoot,
            string selectedImagePath = "",
            bool loadFirstImage = false,
            bool refreshDetails = true)
            => imageQueueCatalogLoadAdapter.LoadAsync(imageRoot, selectedImagePath, loadFirstImage, refreshDetails);

        private bool TryBeginImageQueueCatalogLoad(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails,
            out ImageQueueCatalogLoadRequest request)
            => imageQueueCatalogLoadAdapter.TryBegin(
                imageRoot,
                selectedImagePath,
                loadFirstImage,
                refreshDetails,
                out request);

        private int ApplyImageQueueCatalogLoad(
            ImageQueueCatalogLoadRequest request,
            ImageQueueCatalogLoadResult snapshot)
            => imageQueueCatalogLoadAdapter.Apply(request, snapshot);

        private bool IsCurrentImageQueueCatalogLoad(ImageQueueCatalogLoadRequest request)
            => imageQueueCatalogLoadAdapter?.IsCurrent(request) == true;

        private void CompleteImageQueueCatalogLoad(ImageQueueCatalogLoadRequest request)
            => imageQueueCatalogLoadAdapter.Complete(request);

        private void ReportImageQueueCatalogLoadFailure(ImageQueueCatalogLoadRequest request, Exception exception)
            => imageQueueCatalogLoadAdapter.ReportFailure(request, exception);

        private void RebuildImageQueueItemIndex(IEnumerable<WpfImageQueueItem> items)
            => imageQueueCatalogProjectionAdapter.RebuildImageQueueItemIndex(items);

        private void UpdateAnomalyFolderStateSuggestion(ImageQueueCatalogLoadRequest request)
            => imageQueueCatalogProjectionAdapter.UpdateAnomalyFolderStateSuggestion(request);

        private void ExecuteApplyAnomalyFolderStateSuggestionCommand()
            => imageQueueCatalogProjectionAdapter.ExecuteApplyAnomalyFolderStateSuggestionCommand();

        private void ExecuteDismissAnomalyFolderStateSuggestionCommand()
            => imageQueueCatalogProjectionAdapter.ExecuteDismissAnomalyFolderStateSuggestionCommand();
        #endregion

        #region ImageChangeStateAdapterFacade
        private void ClearActiveImageAfterQueueReset()
            => imageChangeStateAdapter.ClearAfterQueueReset();
        #endregion

        #region ImageLoadWorkflowAdapterFacade
        public ImageLoadDiagnostics LastImageLoadDiagnostics
            => imageLoadWorkflowAdapter?.LastImageLoadDiagnostics ?? lastImageLoadDiagnostics;

        public ImageDecodeCacheDiagnostics GetImageDecodeCacheDiagnostics()
            => imageQueueLoadingAdapter?.GetImageDecodeCacheDiagnostics()
                ?? imageDecodeCacheService.GetDiagnostics();

        public bool TryLoadStartupSampleImage()
            => imageLoadWorkflowAdapter?.TryLoadStartupSampleImage() == true;

        public bool TryLoadImage(
            string imagePath,
            bool populateQueue = true,
            bool refreshQueueDetails = true,
            bool refreshActiveStatus = true,
            bool appendLoadLog = true)
            => imageLoadWorkflowAdapter?.TryLoadImage(
                imagePath,
                populateQueue,
                refreshQueueDetails,
                refreshActiveStatus,
                appendLoadLog) == true;

        private void ScheduleImageLoadReviewRefresh(string imagePath, bool refreshActiveStatus, bool refreshClassCatalog)
            => imageQueueLoadingAdapter.ScheduleImageLoadReviewRefresh(imagePath, refreshActiveStatus, refreshClassCatalog);

        private void RefreshImageLoadReviewState(bool refreshActiveStatus, bool refreshClassCatalog)
            => imageQueueLoadingAdapter.RefreshImageLoadReviewState(refreshActiveStatus, refreshClassCatalog);

        private bool TrySavePendingAnnotationsBeforeImageChange(string nextImagePath)
            => imageQueueLoadingAdapter.TrySavePendingAnnotationsBeforeImageChange(nextImagePath);

        private void PreloadAdjacentQueueImages(string imagePath)
            => imageQueueLoadingAdapter.PreloadAdjacentQueueImages(imagePath);

        private void LoadImageToCanvas(CvMat imageMat, string fileName)
        {
            using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                if (imageViewerLoadOverride != null)
                {
                    imageViewerLoadOverride(imageMat, fileName);
                }
                else
                {
                    MainCanvasViewModel.LoadImage(imageMat, fileName);
                }

                MainCanvasViewModel.ClearRois();
                MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                MainCanvasViewModel.SetMaskOverlays(Array.Empty<RoiImageCanvasMaskOverlay>());
                MainCanvasViewModel.SetPolygonOverlays(Array.Empty<RoiImageCanvasPolygonOverlay>());
                MainCanvasViewModel.ClearMaskStrokePreview(refresh: false, clearTexture: true);
            }
        }

        private void ShowViewerUnavailable(string graphicsDetail)
        {
            WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = "이미지 뷰어를 시작할 수 없습니다",
                Message = "현재 그래픽 환경이 라벨링 뷰어의 필수 기능을 지원하지 않습니다.",
                Details =
                    graphicsDetail
                    + "\n\n설정/도구 > 진단/지원에서 환경 점검을 실행하거나 지원 자료를 만들어 확인하세요.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.OK,
                PrimaryButtonText = "확인",
                MaxWidth = 680D
            });
        }

        private void PopulateImageQueueAfterLoad(string imagePath, bool refreshQueueDetails)
            => imageQueueLoadingAdapter.PopulateImageQueueAfterLoad(imagePath, refreshQueueDetails);

        private void PopulateImageQueue(string imageRoot, string selectedImagePath, bool refreshDetails = true)
            => imageQueueLoadingAdapter.PopulateImageQueue(imageRoot, selectedImagePath, refreshDetails);
        #endregion

        private void CancelImageQueueCatalogLoad(bool waitForCompletion)
        {
            Task catalogTask = imageQueueCatalogLoadAdapter?.Cancel();
            if (waitForCompletion)
            {
                WaitForImageQueueCatalogLoad(catalogTask);
            }
        }

        private void CancelImageQueueDetailRefresh(bool waitForCompletion)
            => imageQueueDetailRefreshAdapter.Cancel(waitForCompletion);

        private void WaitForImageQueueCatalogLoad(Task catalogTask)
        {
            if (catalogTask == null || catalogTask.IsCompleted)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    catalogTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException exception)
                {
                    AppendLog("Image Queue catalog load close wait failed: " + exception.GetBaseException().Message);
                }

                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!catalogTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                var frame = new DispatcherFrame();
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            if (catalogTask.IsFaulted)
            {
                AppendLog("Image Queue catalog load failed during close: " + catalogTask.Exception.GetBaseException().Message);
            }
        }
        #endregion

        #region TrainingGuideCommandAdapterFacade
        private void ExecuteFixYoloClassesCommand()
            => trainingGuideCommandAdapter.ExecuteFixYoloClassesCommand();

        private void ExecuteFixYoloLabelsCommand()
            => trainingGuideCommandAdapter.ExecuteFixYoloLabelsCommand();

        private void ExecuteFixYoloDatasetCommand()
            => trainingGuideCommandAdapter.ExecuteFixYoloDatasetCommand();

        private void ExecuteDatasetDashboardMetricCommand(WpfDatasetDashboardMetricItem metric)
            => trainingGuideCommandAdapter.ExecuteDatasetDashboardMetricCommand(metric);

        private void ExecuteExportDatasetQualityAuditCommand()
            => trainingGuideCommandAdapter.ExecuteExportDatasetQualityAuditCommand();

        private void ExecuteFirstRunSamplePathCommand(WpfFirstRunChecklistItem item)
            => trainingGuideCommandAdapter.ExecuteFirstRunSamplePathCommand(item);

        private void ExecuteOpenTutorialHtmlGuideCommand()
            => trainingGuideCommandAdapter.ExecuteOpenTutorialHtmlGuideCommand();

        private void ExecuteYoloTrainingWorkflowStep(int order, object sender)
            => trainingGuideCommandAdapter.ExecuteYoloTrainingWorkflowStep(order);

        private WpfAnnotationTool ResolvePrimaryLabelingToolForCurrentPurpose()
            => trainingGuideCommandAdapter.ResolvePrimaryLabelingToolForCurrentPurpose();

        private void TrySaveActiveAnnotationsForTrainingCheck()
            => trainingGuideCommandAdapter.TrySaveActiveAnnotationsForTrainingCheck();

        private void ApplyLearningStepWorkflowAction(WpfLearningStepWorkflowAction action)
            => trainingGuideCommandAdapter.ApplyLearningStepWorkflowAction(action);
        #endregion

        #region YoloEnvironmentWorkflowAdapterFacade
        private YoloModelSettingsPathCallbacks CreateYoloModelSettingsPathCallbacks()
            => yoloEnvironmentWorkflowAdapter.CreateYoloModelSettingsPathCallbacks();

        private void ExecuteRuntimeProfileActionCommand(string engine)
            => yoloEnvironmentWorkflowAdapter.ExecuteRuntimeProfileActionCommand(engine);

        private void ExecuteSaveYoloSettingsCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteSaveYoloSettingsCommand();

        private void ExecuteResetYoloSettingsCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteResetYoloSettingsCommand();

        private YoloEnvironmentCallbacks CreateYoloEnvironmentCallbacks()
            => yoloEnvironmentWorkflowAdapter.CreateYoloEnvironmentCallbacks();

        private void ExecuteCheckYoloCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteCheckYoloCommand();

        private void ExecuteDetectCurrentImageCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteDetectCurrentImageCommand();

        private void ExecuteInstallUltralyticsPackageCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteInstallUltralyticsPackageCommand();

        private void ExecuteUninstallUltralyticsPackageCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteUninstallUltralyticsPackageCommand();

        private void ExecuteRunYoloSmokeCommand()
            => yoloEnvironmentWorkflowAdapter.ExecuteRunYoloSmokeCommand();

        private void SetYoloCommandStatus(string text, bool isBusy)
            => YoloStatusViewModel?.SetCommandStatus(text, isBusy);

        private void SetYoloRecoveryStatus(string titleText, string detailText, string actionText)
        {
            ShellViewModel?.SetModelCenterRecoveryState(titleText, detailText, actionText);
            YoloStatusViewModel?.SetRecoveryState(titleText, detailText, actionText);
        }

        private void ClearYoloRecoveryStatus()
        {
            ShellViewModel?.ClearModelCenterRecoveryState();
            YoloStatusViewModel?.ClearRecoveryState();
        }

        private bool ConfirmUltralyticsPackageOperation(bool uninstall, PythonModelRuntimeInstallPlan plan)
        {
            UltralyticsPackageConfirmationPresentation presentation =
                YoloEnvironmentCommandPresentationService.BuildUltralyticsConfirmation(uninstall, plan);
            WpfMessageDialogResult result = WpfMessageDialog.Confirm(
                this,
                presentation.Title,
                presentation.Detail,
                presentation.PrimaryButtonText,
                presentation.CancelButtonText);
            return result == WpfMessageDialogResult.Yes;
        }
        #endregion

        #region YoloRuntimeStatusAdapterFacade
        private void NotifyYoloPathSelected(string label, string selectedPath)
            => yoloRuntimeStatusAdapter.NotifyYoloPathSelected(label, selectedPath);

        private void RefreshYoloStatus()
            => yoloRuntimeStatusAdapter.RefreshYoloStatus();

        private PythonModelRuntimeState GetPythonModelRuntimeState()
            => yoloRuntimeStatusAdapter.GetPythonModelRuntimeState();

        private bool EnsureModelRuntimeForTraining()
            => yoloRuntimeStatusAdapter.EnsureModelRuntimeForTraining();

        private bool EnsureModelRuntimeForInference()
            => yoloRuntimeStatusAdapter.EnsureModelRuntimeForInference();

        private void ShowModelRuntimeUnavailable(string statusText, PythonModelRuntimeState runtimeState)
            => yoloRuntimeStatusAdapter.ShowModelRuntimeUnavailable(statusText, runtimeState);

        private void ApplyModelRuntimeUnavailablePresentation(PythonModelRuntimeState runtimeState, string statusText = null)
            => yoloRuntimeStatusAdapter.ApplyModelRuntimeUnavailablePresentation(runtimeState, statusText);

        private void SetGlobalInferenceStatus(string text, bool isBusy, bool isWarning = false)
            => yoloRuntimeStatusAdapter.SetGlobalInferenceStatus(text, isBusy, isWarning);

        private void StartInferenceStatusPulse()
            => yoloRuntimeStatusAdapter.StartInferenceStatusPulse();

        private void StopInferenceStatusPulse()
            => yoloRuntimeStatusAdapter.StopInferenceStatusPulse();

        private Task RefreshYoloSettingsPanelAsync(PythonModelValidationResult validation = null)
            => yoloRuntimeStatusAdapter.RefreshSettingsAsync(validation);

        private void SaveYoloEditorFields()
            => projectSettingsEditorAdapter.SaveYoloEditorFields();

        private void SaveTrainingEditorFields()
            => projectSettingsEditorAdapter.SaveTrainingEditorFields();

        private void RefreshCandidateConfidenceFilterFromAppliedSettings()
            => projectSettingsEditorAdapter.RefreshCandidateConfidenceFilterFromAppliedSettings();
        #endregion

        #region ModelCenterWorkflowAdapterFacade
        private void ExecuteSaveModelCandidateCommand()
            => modelCenterWorkflowAdapter.ExecuteSaveModelCandidateCommand();

        private void ExecuteRejectModelCandidateCommand()
            => modelCenterWorkflowAdapter.ExecuteRejectModelCandidateCommand();

        private void UpdateCandidateModelDecisionPanel(WpfTrainingWeightsComparison comparison = null)
            => modelCenterWorkflowAdapter.UpdateCandidateModelDecisionPanel(comparison);

        private void ExecutePromoteSelectedModelHistoryCommand()
            => modelCenterWorkflowAdapter.ExecutePromoteSelectedModelHistoryCommand();

        private void RefreshModelCenterDashboard(
            WpfTrainingWeightsComparison comparison = null,
            string configuredWeightsPathOverride = null,
            bool pendingManualWeightsSelection = false)
            => modelCenterWorkflowAdapter.RefreshModelCenterDashboard(
                comparison,
                configuredWeightsPathOverride,
                pendingManualWeightsSelection);

        private AnomalyEvaluationCallbacks CreateAnomalyEvaluationCallbacks()
            => modelCenterWorkflowAdapter.CreateAnomalyEvaluationCallbacks();

        private void RefreshModelCenterAnomalyEvaluationState()
            => modelCenterWorkflowAdapter.RefreshModelCenterAnomalyEvaluationState();
        #endregion

        #region ModelComparisonWorkflowAdapterFacade
        private ModelComparisonWorkflowService CreateModelComparisonWorkflow()
            => modelComparisonWorkflowAdapter.CreateWorkflow();

        private ModelComparisonCallbacks CreateModelComparisonCallbacks()
            => modelComparisonWorkflowAdapter.CreateCallbacks();

        private bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
            => modelComparisonWorkflowAdapter.TryApplyLatestTrainingWeightsFromProject(logIfUnchanged);

        private WpfTrainingWeightsComparison BuildCurrentTrainingWeightsComparison()
            => modelComparisonWorkflowAdapter.BuildCurrentTrainingWeightsComparison();

        private void UpdateTrainingComparisonViewModel(WpfTrainingWeightsComparison comparison, string comparisonStatusText = null)
            => modelComparisonWorkflowAdapter.UpdateTrainingComparisonViewModel(comparison, comparisonStatusText);

        private void UpdateCandidateModelComparisonReviewPanel(WpfTrainingWeightsComparison comparison = null)
            => modelComparisonWorkflowAdapter.UpdateCandidateModelComparisonReviewPanel(comparison);

        private WpfModelComparisonHistoryItem RefreshModelComparisonHistoryItems(
            string baselineWeightsPath,
            string candidateWeightsPath,
            string preferredSummaryPath = "")
            => modelComparisonWorkflowAdapter.RefreshHistoryItems(
                baselineWeightsPath,
                candidateWeightsPath,
                preferredSummaryPath);

        private WpfModelComparisonReviewReport BuildModelComparisonHistoryReport(WpfModelComparisonHistoryItem item)
            => modelComparisonWorkflowAdapter.BuildHistoryReport(item);

        private void ApplyModelComparisonHistorySelectionEffects(WpfModelComparisonHistoryItem item)
            => modelComparisonWorkflowAdapter.ApplyHistorySelectionEffects(item);
        #endregion

        #region CrashRecoveryIntegrationAdapterFacade
        private void OnCrashRecoveryJournalWriteFailed(
            object sender,
            CrashRecoveryJournalWriteFailedEventArgs failure)
            => crashRecoveryIntegrationAdapter.OnCrashRecoveryJournalWriteFailed(sender, failure);

        private void OnCrashRecoveryJournalCaptureFailed(
            object sender,
            CrashRecoveryJournalCaptureFailedEventArgs failure)
            => crashRecoveryIntegrationAdapter.OnCrashRecoveryJournalCaptureFailed(sender, failure);

        private bool TryHandleCrashRecoveryOnStartup()
            => crashRecoveryIntegrationAdapter.TryHandleCrashRecoveryOnStartup();

        private WpfMessageDialogResult ShowCrashRecoveryPrompt(WpfCrashRecoveryDraft draft)
            => crashRecoveryIntegrationAdapter.ShowCrashRecoveryPrompt(draft);

        private bool TryRestoreCrashRecoveryDraft(WpfCrashRecoveryDraft draft)
            => crashRecoveryIntegrationAdapter.TryRestoreCrashRecoveryDraft(draft);

        private bool ScheduleCrashRecoveryJournalWrite()
            => crashRecoveryIntegrationAdapter.ScheduleCrashRecoveryJournalWrite();

        private WpfCrashRecoveryDraft CaptureCrashRecoveryDraft()
            => crashRecoveryIntegrationAdapter.CaptureCrashRecoveryDraft();

        private void DiscardCrashRecoveryJournal()
            => crashRecoveryIntegrationAdapter.DiscardCrashRecoveryJournal();

        private WpfMessageDialogResult ShowCrashRecoveryPromptDialog(WpfCrashRecoveryDraft draft)
        {
            string imageName = Path.GetFileName(draft?.ImagePath ?? string.Empty);
            string localTime = draft == null
                ? "-"
                : draft.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            return WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = "비정상 종료 편집 복구",
                Message = "저장되지 않은 현재 이미지 편집 초안을 발견했습니다.",
                Details =
                    $"Recipe: {draft?.RecipeName}\n" +
                    $"이미지: {imageName}\n" +
                    $"초안 시각: {localTime}\n" +
                    $"편집 사유: {draft?.DirtyReason}\n" +
                    $"객체: 박스 {draft?.Boxes?.Count ?? 0}개, 세그멘테이션 {draft?.Segments?.Count ?? 0}개\n\n" +
                    "복구하면 화면의 미저장 편집 상태로만 돌아옵니다. " +
                    "AI 후보를 승인하거나 라벨 파일을 저장하지 않습니다. 검토 후 `라벨 저장`을 눌러야 합니다.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.YesNo,
                DefaultResult = WpfMessageDialogResult.No,
                PrimaryButtonText = "편집 복구",
                SecondaryButtonText = "초안 폐기",
                MaxWidth = 660D
            });
        }

        private void ShowCrashRecoveryRestoreFailureDialog()
        {
            WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = "편집 초안을 복구하지 못했습니다",
                Message = "원본 이미지 또는 현재 Recipe 상태를 확인한 뒤 다시 시작해 주세요.",
                Details = "복구 초안은 안전을 위해 격리되거나 유지되지 않습니다. 저장된 라벨 파일은 변경하지 않았습니다.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.OK,
                PrimaryButtonText = "확인"
            });
        }
        #endregion

        // Class catalog remains a public shell entry point for the existing
        // launcher, while its workflow and mutable-state projection live in
        // ClassCatalogWorkflowAdapter.
        public void FocusClassCatalogTab() => classCatalogWorkflowAdapter.FocusClassCatalogTab();

        private void HandleClassCatalogMutationCompleted(
            WpfClassCatalogMutationKind kind,
            WpfClassCatalogMutationResult result)
            => classCatalogWorkflowAdapter.HandleClassCatalogMutationCompleted(kind, result);

        private void ExecuteBrowseOutputRootCommand()
            => classCatalogWorkflowAdapter.ExecuteBrowseOutputRootCommand();

        private void ExecuteSaveOutputRootCommand()
            => classCatalogWorkflowAdapter.ExecuteSaveOutputRootCommand();

        private void PopulateClassList(string selectedName = "")
            => classCatalogWorkflowAdapter.PopulateClassList(selectedName);

        private string GetSelectedClassName()
            => classCatalogWorkflowAdapter.GetSelectedClassName();

        private void UpdateYoloTrainingGuideDatasetHistory(
            YoloDatasetReadinessReport report,
            TrainingChecklistPresentation presentation,
            bool recordHistory)
            => trainingGuideWorkflowAdapter.UpdateYoloTrainingGuideDatasetHistory(report, presentation, recordHistory);

        private void UpdateYoloTrainingGuideTrainingHistory(PythonCommunicationStatus status)
            => trainingGuideWorkflowAdapter.UpdateYoloTrainingGuideTrainingHistory(status);

        private void UpdateYoloTrainingHistoryText()
            => trainingGuideWorkflowAdapter.UpdateYoloTrainingHistoryText();

        private void UpdateTrainingResultComparisonText()
            => trainingGuideWorkflowAdapter.UpdateTrainingResultComparisonText();

        private bool TrySaveTrainingGuideHistoryQuietly()
            => trainingGuideWorkflowAdapter.TrySaveTrainingGuideHistoryQuietly();

        private void UpdateYoloTrainingChecklist(YoloDatasetReadinessReport report, bool recordHistory)
            => trainingGuideWorkflowAdapter.UpdateYoloTrainingChecklist(report, recordHistory);

        private void RefreshYoloTrainingStepCompletion(YoloDatasetReadinessReport report = null)
            => trainingGuideWorkflowAdapter.RefreshYoloTrainingStepCompletion(report);

        private void FinishQueueCompletionAndGuideDatasetCheck()
            => trainingGuideWorkflowAdapter.FinishQueueCompletionAndGuideDatasetCheck();

        private void UpdateDatasetStatusDashboard(
            YoloDatasetReadinessReport report,
            TrainingChecklistPresentation presentation)
            => trainingGuideWorkflowAdapter.UpdateDatasetStatusDashboard(report, presentation);

        private bool IsInferenceWorkflowActive
            => workflowNavigationAdapter?.IsInferenceWorkflowActive == true
                || (workflowNavigationAdapter == null && ShellViewModel?.IsInferenceModeActive == true);

        private void ExecuteFitCanvasCommand()
            => workflowNavigationAdapter.ExecuteFitCanvasCommand();

        private void ExecuteActualSizeCanvasCommand()
            => workflowNavigationAdapter.ExecuteActualSizeCanvasCommand();

        private void ExecutePanCanvasCommand()
            => workflowNavigationAdapter.ExecutePanCanvasCommand();

        private void ExecuteFocusCandidateCommand()
            => workflowNavigationAdapter.ExecuteFocusCandidateCommand();

        private void SelectRightWorkflowView(TabItem tab)
            => workflowNavigationAdapter.SelectRightWorkflowView(tab);

        private void ShowSavedLabelsWorkflowView()
            => workflowNavigationAdapter.ShowSavedLabelsWorkflowView();

        private void ShowCandidateReviewWorkflowView()
            => workflowNavigationAdapter.ShowCandidateReviewWorkflowView();

        private void ShowGuideToolsWorkflowView(WpfShellWorkflowStage stage)
            => workflowNavigationAdapter.ShowGuideToolsWorkflowView(stage);

        private void ShowClassCatalogWorkflowView(WpfShellWorkflowStage stage)
            => workflowNavigationAdapter.ShowClassCatalogWorkflowView(stage);

        private void ShowYoloModelCenterWorkflowView()
            => workflowNavigationAdapter.ShowYoloModelCenterWorkflowView();

        public void FocusYoloSettingsTab()
            => workflowPanelFocusAdapter.FocusYoloSettingsTab();

        private void FocusYoloModelSettingsTab()
            => workflowPanelFocusAdapter.FocusYoloModelSettingsTab();

        private void FocusYoloTrainingSettingsTab()
            => workflowPanelFocusAdapter.FocusYoloTrainingSettingsTab();

        private void YoloModelCenterTaskTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // InitializeComponent may raise SelectionChanged before the focus adapter is composed.
            workflowPanelFocusAdapter?.YoloModelCenterTaskTabsSelectionChanged(sender, e);
        }

        private void CollapseYoloAdvancedSettingsForOverview()
            => workflowPanelFocusAdapter.CollapseYoloAdvancedSettingsForOverview();

        public void FocusAnnotationToolsTab()
            => workflowPanelFocusAdapter.FocusAnnotationToolsTab();

        private void FocusCurrentStageGuideToolsTab()
            => workflowPanelFocusAdapter.FocusCurrentStageGuideToolsTab();

        private void FocusDatasetOnboardingTab()
            => workflowPanelFocusAdapter.FocusDatasetOnboardingTab();

        private void FocusDatasetOnboardingTabIfNoActiveImage()
            => workflowPanelFocusAdapter.FocusDatasetOnboardingTabIfNoActiveImage();

        private void FocusLabelingSidePanelForTool(WpfAnnotationTool tool)
            => workflowPanelFocusAdapter.FocusLabelingSidePanelForTool(tool);

        private void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange)
            => workflowNavigationAdapter.ApplyCanvasDisplayMode(mode, redraw, logChange);

        private void RefreshCanvasLayerVisibilityState()
            => workflowNavigationAdapter.RefreshCanvasLayerVisibilityState();

        private void ExecuteResetAiOverlayCommand()
            => workflowNavigationAdapter.ExecuteResetAiOverlayCommand();

        private void ExecuteLabelingModeCommand()
            => workflowNavigationAdapter.ExecuteLabelingModeCommand();

        private void EnterLabelingMode(bool openGuidePanel)
            => workflowNavigationAdapter.EnterLabelingMode(openGuidePanel);

        private void ExecuteInferenceModeCommand()
            => workflowNavigationAdapter.ExecuteInferenceModeCommand();

        private void SetWorkflowMode(WorkflowMode mode)
            => workflowNavigationAdapter.SetWorkflowMode(mode == WorkflowMode.Inference);

        private void UpdateWorkflowModeUi()
            => workflowNavigationAdapter.UpdateWorkflowModeUi();

        #region AnnotationInput
        private void MainCanvasViewModel_ImagePointClicked(object sender, CanvasImagePointEventArgs e)
            => annotationInputAdapter.HandleImagePointClicked(e);

        private void MainCanvasViewModel_ImagePointMoved(object sender, CanvasImagePointEventArgs e)
            => annotationInputAdapter.HandleImagePointMoved(e);

        private void MainCanvasViewModel_ImagePointReleased(object sender, CanvasImagePointEventArgs e)
            => annotationInputAdapter.HandleImagePointReleased();

        private void MainCanvasViewModel_ImagePointHovered(object sender, CanvasImagePointEventArgs e)
            => annotationInputAdapter.HandleImagePointHovered(e);

        private void BeginPolygonAnnotationMode()
            => annotationInputAdapter.BeginPolygonAnnotationMode();

        private void EndPolygonAnnotationMode(bool clearDraft)
            => annotationInputAdapter.EndPolygonAnnotationMode(clearDraft);

        private void BeginMaskAnnotationMode(WpfAnnotationTool tool)
            => annotationInputAdapter.BeginMaskAnnotationMode(tool);

        private void EndMaskAnnotationMode()
            => annotationInputAdapter.EndMaskAnnotationMode();

        private bool TryBeginSelectedSegmentEdit(CanvasImagePointEventArgs e)
            => annotationSegmentEditAdapter.TryBeginSelectedSegmentEdit(e);

        private bool TryMoveSelectedSegmentEdit(CanvasImagePointEventArgs e)
            => annotationSegmentEditAdapter.TryMoveSelectedSegmentEdit(e);

        private void CompleteSelectedSegmentEdit()
            => annotationSegmentEditAdapter.CompleteSelectedSegmentEdit();
        #endregion

        #region AnnotationRenderingAdapterFacade
        private bool ShouldDrawOverExistingRoiForCurrentClass(CanvasRect<float> roiRect)
            => annotationRenderingAdapter?.ShouldDrawOverExistingRoiForCurrentClass(roiRect) == true;

        private System.Drawing.Color GetClassDrawColor(string className)
            => annotationRenderingAdapter?.GetClassDrawColor(className)
                ?? System.Drawing.Color.FromArgb(34, 197, 94);

        private System.Drawing.Color GetManualRoiDrawColor(int index)
            => annotationRenderingAdapter?.GetManualRoiDrawColor(index)
                ?? System.Drawing.Color.FromArgb(34, 197, 94);

        private string ResolveNewManualRoiClassName(CanvasRect<float> roiRect)
            => annotationRenderingAdapter?.ResolveNewManualRoiClassName(roiRect) ?? string.Empty;

        private void ApplyManualRoiOverlayColor(int index, bool refreshImmediately = false)
            => annotationRenderingAdapter?.ApplyManualRoiOverlayColor(index, refreshImmediately);

        private void MainCanvasViewModel_RoiAdded(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
            => annotationRenderingAdapter?.MainCanvasViewModel_RoiAdded(sender, e);

        private void MainCanvasViewModel_RoiEditingCompleted(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
            => annotationRenderingAdapter?.MainCanvasViewModel_RoiEditingCompleted(sender, e);

        private void MainCanvasViewModel_RoiMouseUp(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
            => annotationRenderingAdapter?.MainCanvasViewModel_RoiMouseUp(sender, e);

        private void MainCanvasViewModel_RemoveRoiRequested(object sender, CanvasRect<float> rect)
            => annotationRenderingAdapter?.MainCanvasViewModel_RemoveRoiRequested(sender, rect);

        private bool TryRefreshMaskStrokeCanvasOverlays()
            => annotationRenderingAdapter?.TryRefreshMaskStrokeCanvasOverlays() == true;

        private bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh)
            => annotationRenderingAdapter?.TryRefreshMaskStrokeCanvasOverlays(
                segmentIndices,
                needsFullObjectRefresh) == true;

        private bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh,
            bool refreshAfterInput)
            => annotationRenderingAdapter?.TryRefreshMaskStrokeCanvasOverlays(
                segmentIndices,
                needsFullObjectRefresh,
                refreshAfterInput) == true;

        private void CompletePolygonAnnotation()
            => annotationRenderingAdapter?.CompletePolygonAnnotation();

        private void RefreshPolygonOverlays()
            => annotationRenderingAdapter?.RefreshPolygonOverlays();

        private void EnsureManualRoiMetadataCount()
            => annotationRenderingAdapter?.EnsureManualRoiMetadataCount();

        private void RedrawReviewRois()
            => annotationRenderingAdapter?.RedrawReviewRois();

        private void ClearSegmentationOverlays()
            => annotationRenderingAdapter?.ClearSegmentationOverlays();

        private static void RemoveAtIfPresent<T>(IList<T> items, int index)
        {
            if (items != null && index >= 0 && index < items.Count)
            {
                items.RemoveAt(index);
            }
        }
        #endregion

        #region SegmentationEditAdapterFacade
        private void ExecuteBeginAddSegmentationHoleCommand()
            => segmentationEditAdapter?.ExecuteBeginAddSegmentationHoleCommand();

        private void ExecuteBeginRemoveSegmentationHoleCommand()
            => segmentationEditAdapter?.ExecuteBeginRemoveSegmentationHoleCommand();

        private void ExecuteCancelSegmentationHoleEditCommand()
            => segmentationEditAdapter?.ExecuteCancelSegmentationHoleEditCommand();

        private bool TryApplyPendingSegmentationHoleEdit(CanvasImagePointEventArgs e)
            => segmentationEditAdapter?.TryApplyPendingSegmentationHoleEdit(e) == true;

        private bool TryResolvePendingSegmentationHoleSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
        {
            if (segmentationEditAdapter == null)
            {
                sourceIndex = -1;
                source = null;
                return false;
            }

            return segmentationEditAdapter.TryResolvePendingSegmentationHoleSource(
                out sourceIndex,
                out source);
        }

        private void CancelPendingSegmentationHoleEdit(bool updateStatus)
            => segmentationEditAdapter?.CancelPendingSegmentationHoleEdit(updateStatus);

        private void ExecutePreviewSegmentationRemoveUnderlyingCommand()
            => segmentationEditAdapter?.ExecutePreviewSegmentationRemoveUnderlyingCommand();

        private void ExecuteApplySegmentationRemoveUnderlyingCommand()
            => segmentationEditAdapter?.ExecuteApplySegmentationRemoveUnderlyingCommand();

        private void ExecuteCancelSegmentationRemoveUnderlyingCommand()
            => segmentationEditAdapter?.ExecuteCancelSegmentationRemoveUnderlyingCommand();

        private void CancelPendingSegmentationRemoveUnderlying(bool updateStatus)
            => segmentationEditAdapter?.CancelPendingSegmentationRemoveUnderlying(updateStatus);

        private bool IsPendingRemoveUnderlyingAffectedIndex(int sourceIndex)
            => segmentationEditAdapter?.IsPendingRemoveUnderlyingAffectedIndex(sourceIndex) == true;

        private void ExecuteBeginVerticalSegmentationSplitCommand()
            => segmentationEditAdapter?.ExecuteBeginVerticalSegmentationSplitCommand();

        private void ExecuteBeginHorizontalSegmentationSplitCommand()
            => segmentationEditAdapter?.ExecuteBeginHorizontalSegmentationSplitCommand();

        private void ExecuteCancelSegmentationSplitCommand()
            => segmentationEditAdapter?.ExecuteCancelSegmentationSplitCommand();

        private bool TryApplyPendingSegmentationSplit(CanvasImagePointEventArgs e)
            => segmentationEditAdapter?.TryApplyPendingSegmentationSplit(e) == true;

        private void CancelPendingSegmentationSplit(bool updateStatus)
            => segmentationEditAdapter?.CancelPendingSegmentationSplit(updateStatus);

        private void ExecuteSendSegmentationToBackCommand()
            => MoveSelectedSegmentationZOrder(WpfSegmentationZOrderMove.SendToBack);

        private void ExecuteSendSegmentationBackwardCommand()
            => MoveSelectedSegmentationZOrder(WpfSegmentationZOrderMove.SendBackward);

        private void ExecuteBringSegmentationForwardCommand()
            => MoveSelectedSegmentationZOrder(WpfSegmentationZOrderMove.BringForward);

        private void ExecuteBringSegmentationToFrontCommand()
            => MoveSelectedSegmentationZOrder(WpfSegmentationZOrderMove.BringToFront);

        private void MoveSelectedSegmentationZOrder(WpfSegmentationZOrderMove move)
            => segmentationZOrderCommandAdapter?.MoveSelectedSegmentationZOrder(move);
        #endregion

        #region AnnotationToolSettingsAdapterFacade
        private bool IsFourPointBoxInputActive()
            => annotationToolSettingsAdapter?.IsFourPointBoxInputActive() == true;

        private void ExecuteSetBoxDrawingMethod(LabelingBoxDrawingMethod method)
            => annotationToolSettingsAdapter.ExecuteSetBoxDrawingMethod(method);

        private void RestoreBoxDrawingMethodFromProject()
            => annotationToolSettingsAdapter.RestoreBoxDrawingMethodFromProject();

        private void ApplyRectangleDrawingInputMode()
            => annotationToolSettingsAdapter.ApplyRectangleDrawingInputMode();

        private bool TryHandleFourPointBoxInput(CanvasImagePointEventArgs e)
            => fourPointBoxInputAdapter.TryHandleInput(e);

        private bool RemoveLastFourPointBoxPoint()
            => fourPointBoxInputAdapter.RemoveLastPoint();

        private bool CancelFourPointBoxDraft(bool updateStatus)
            => fourPointBoxInputAdapter.CancelDraft(updateStatus);

        private void ExecuteBeginIntelligentScissorsCommand()
            => polygonBoundaryEditAdapter.ExecuteBeginIntelligentScissorsCommand();

        private bool TryHandlePendingIntelligentScissors(CanvasImagePointEventArgs e)
            => polygonBoundaryEditAdapter.TryHandlePendingIntelligentScissors(e);

        private void ExecuteApplyIntelligentScissorsCommand()
            => polygonBoundaryEditAdapter.ExecuteApplyIntelligentScissorsCommand();

        private void ExecuteCancelIntelligentScissorsCommand()
            => polygonBoundaryEditAdapter.ExecuteCancelIntelligentScissorsCommand();

        private bool TryResolvePendingIntelligentScissorsSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
            => polygonBoundaryEditAdapter.TryResolvePendingIntelligentScissorsSource(
                out sourceIndex,
                out source);

        private void CancelPendingIntelligentScissors(bool updateStatus)
            => polygonBoundaryEditAdapter.CancelPendingIntelligentScissors(updateStatus);

        private void ExecuteBeginInsertPolygonVertexCommand()
            => polygonBoundaryEditAdapter.ExecuteBeginInsertPolygonVertexCommand();

        private void ExecuteBeginDeletePolygonVertexCommand()
            => polygonBoundaryEditAdapter.ExecuteBeginDeletePolygonVertexCommand();

        private void ExecuteCancelPolygonVertexEditCommand()
            => polygonBoundaryEditAdapter.ExecuteCancelPolygonVertexEditCommand();

        private void BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode mode)
            => polygonBoundaryEditAdapter.BeginPendingPolygonVertexEdit(mode);

        private bool TryApplyPendingPolygonVertexEdit(CanvasImagePointEventArgs e)
            => polygonBoundaryEditAdapter.TryApplyPendingPolygonVertexEdit(e);

        private bool TryResolvePendingPolygonVertexSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
            => polygonBoundaryEditAdapter.TryResolvePendingPolygonVertexSource(
                out sourceIndex,
                out source);

        private void CancelPendingPolygonVertexEdit(bool updateStatus)
            => polygonBoundaryEditAdapter.CancelPendingPolygonVertexEdit(updateStatus);

        private void ExecuteCreateSmartMaskCandidateCommand()
            => smartMaskWorkflowAdapter.StartCandidateGeneration();

        private async Task ExecuteCreateSmartMaskCandidateCommandAsync()
            => await smartMaskWorkflowAdapter.ExecuteCreateSmartMaskCandidateCommandAsync();

        private void ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode mode)
            => smartMaskWorkflowAdapter.ExecuteSetSmartMaskPointModeCommand(mode);

        private void ExecuteCancelSmartMaskGenerationCommand()
            => smartMaskWorkflowAdapter.ExecuteCancelSmartMaskGenerationCommand();

        private void ExecuteUndoSmartMaskPointCommand()
            => smartMaskWorkflowAdapter.ExecuteUndoSmartMaskPointCommand();

        private void ExecuteClearSmartMaskPointsCommand()
            => smartMaskWorkflowAdapter.ExecuteClearSmartMaskPointsCommand();

        private void ExecuteSetSmartMaskPolygonDetailCommand(WpfSmartMaskPolygonDetail detail)
            => smartMaskWorkflowAdapter.ExecuteSetSmartMaskPolygonDetailCommand(detail);

        private void ExecuteNextSmartMaskInstanceCommand()
            => smartMaskWorkflowAdapter.ExecuteNextSmartMaskInstanceCommand();

        private void ExecuteSetSmartMaskAutoContourMode(bool enabled)
            => annotationToolSettingsAdapter.ExecuteSetSmartMaskAutoContourMode(enabled);

        private void TryStartAutoSmartMaskForNewRoi(CanvasRect<float> roiRect)
            => smartMaskWorkflowAdapter.TryStartAutoSmartMaskForNewRoi(roiRect);

        private void ContinueAutoSmartMaskAfterResolvedCandidate(string resolution)
            => smartMaskWorkflowAdapter.ContinueAutoSmartMaskAfterResolvedCandidate(resolution);

        private void ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion version)
            => smartMaskWorkflowAdapter.ExecuteSelectSmartMaskCandidateVersionCommand(version);

        private bool TryApplySmartMaskPointInput(CanvasImagePointEventArgs e)
            => smartMaskWorkflowAdapter.TryApplySmartMaskPointInput(e);

        private void ResetSmartMaskPromptSession()
            => smartMaskWorkflowAdapter.ResetSmartMaskPromptSession();

        private string GetCurrentSmartMaskRecipeName()
            => smartMaskWorkflowAdapter.GetCurrentSmartMaskRecipeName();

        private int FindSmartMaskPromptIndex()
            => smartMaskWorkflowAdapter.FindSmartMaskPromptIndex();

        private void RefreshSmartMaskCommandState(string detail = "")
            => smartMaskWorkflowAdapter.RefreshSmartMaskCommandState(detail);
        #endregion

        #region AnnotationVisibilityAdapterFacade
        private LabelingDatasetPurpose GetCurrentDatasetPurpose()
        {
            EnsureProjectSettings();
            return LearningWorkflowViewModel?.GetSelectedDatasetPurpose()
                ?? applicationState.Data.ProjectSettings.DatasetPurpose;
        }

        private bool IsSegmentationDatasetPurposeActive()
            => annotationVisibilityAdapter.IsSegmentationDatasetPurposeActive();

        private int GetVisibleManualSegmentCount()
            => annotationVisibilityAdapter.GetVisibleManualSegmentCount();

        private IReadOnlyList<LabelingSegmentationObject> GetVisibleManualSegments()
            => annotationVisibilityAdapter.GetVisibleManualSegments();

        private void RefreshAnnotationVisibilityForDatasetPurpose(bool notifyOperator = false)
            => annotationVisibilityAdapter.RefreshAnnotationVisibilityForDatasetPurpose(notifyOperator);

        private void ReportAnnotationVisibilityForDatasetPurpose(LabelingDatasetPurpose purpose, int segmentCount)
            => annotationVisibilityAdapter.ReportAnnotationVisibilityForDatasetPurpose(purpose, segmentCount);

        private void ApplyAnnotationVisibilityStatus(string text)
            => annotationVisibilityAdapter.ApplyAnnotationVisibilityStatus(text);

        private void ScheduleAnnotationVisibilityStatusRefresh(string text)
            => annotationVisibilityAdapter.ScheduleAnnotationVisibilityStatusRefresh(text);

        private void ApplyScheduledAnnotationVisibilityStatus(string text)
            => annotationVisibilityAdapter.ApplyScheduledAnnotationVisibilityStatus(text);

        private void AnnotationVisibilityRefreshTimer_Tick(object sender, EventArgs e)
            => annotationVisibilityAdapter.HandleAnnotationVisibilityRefreshTimerTick();

        private void EnsureSegmentationDatasetPurposeForSegmentationTool()
            => annotationVisibilityAdapter.EnsureSegmentationDatasetPurposeForSegmentationTool();
        #endregion

        #region AnnotationToolSelectionAdapterFacade
        private void ExecuteCanvasAnnotationToolSelectionChanged(object selectedItem)
            => annotationToolSelectionAdapter.ExecuteCanvasAnnotationToolSelectionChanged(selectedItem);

        private void ApplyAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
            => annotationToolSelectionAdapter.ApplyAnnotationToolSelection(selectedToolItem);

        private void SelectAnnotationTool(WpfAnnotationTool tool, bool revealInGuide = false)
            => annotationToolSelectionAdapter.SelectAnnotationTool(tool, revealInGuide);

        private WpfAnnotationToolItem ResolveSelectableAnnotationTool(WpfAnnotationTool tool)
            => annotationToolSelectionAdapter.ResolveSelectableAnnotationTool(tool);
        #endregion

        #region AnnotationProductivityAdapterFacade
        private bool TryDuplicateSelectedAnnotation()
            => annotationProductivityAdapter.TryDuplicateSelectedAnnotation();

        private bool TryDuplicateManualRoi(int sourceIndex)
            => annotationProductivityAdapter.TryDuplicateManualRoi(sourceIndex);

        private bool TryDuplicateManualSegment(int sourceIndex)
            => annotationProductivityAdapter.TryDuplicateManualSegment(sourceIndex);
        #endregion

        #region MaskStrokeWorkflowAdapterFacade
        private void CompleteMaskAnnotationStroke()
            => maskStrokeWorkflowAdapter.CompleteMaskAnnotationStroke();

        private void FlushQueuedMaskStrokeCommits()
            => maskStrokeWorkflowAdapter.FlushQueuedMaskStrokeCommits();

        private bool HasPendingMaskStrokeCommitWork()
            => maskStrokeWorkflowAdapter.HasPendingMaskStrokeCommitWork();

        private void ClearQueuedMaskStrokeCommits()
            => maskStrokeWorkflowAdapter.ClearQueuedMaskStrokeCommits();

        private void CancelMaskStrokePreviewCommitSwap()
            => maskStrokeWorkflowAdapter.CancelMaskStrokePreviewCommitSwap();

        private bool ScheduleQueuedMaskStrokeCommitsAfterToolEnd()
            => maskStrokeWorkflowAdapter.ScheduleQueuedMaskStrokeCommitsAfterToolEnd();

        private void ApplyMaskAnnotationStroke(CanvasImagePointEventArgs e, bool resetStroke)
            => maskStrokeWorkflowAdapter.ApplyMaskAnnotationStroke(e, resetStroke);

        private bool IsMaskAnnotationToolActive()
            => maskStrokeWorkflowAdapter.IsMaskAnnotationToolActive();

        private bool ShouldSelectCommittedMaskAfterStroke()
            => maskStrokeWorkflowAdapter.ShouldSelectCommittedMaskAfterStroke();

        private int GetMaskBrushRadius()
            => maskStrokeWorkflowAdapter.GetMaskBrushRadius();

        private System.Drawing.Color GetMaskCursorPreviewColor(bool isEraser)
            => maskStrokeWorkflowAdapter.GetMaskCursorPreviewColor(isEraser);

        private System.Drawing.Color GetMaskStrokePreviewColor(bool isEraser)
            => maskStrokeWorkflowAdapter.GetMaskStrokePreviewColor(isEraser);

        private void ExecuteDecreaseBrushSizeCommand()
            => maskStrokeWorkflowAdapter.ExecuteDecreaseBrushSizeCommand();

        private void ExecuteIncreaseBrushSizeCommand()
            => maskStrokeWorkflowAdapter.ExecuteIncreaseBrushSizeCommand();

        private void SyncCanvasBrushSizeFromWorkflow()
            => maskStrokeWorkflowAdapter.SyncCanvasBrushSizeFromWorkflow();

        private void MaskStrokeCommitQueueTimer_Tick(object sender, EventArgs e)
            => maskStrokeWorkflowAdapter.MaskStrokeCommitQueueTimer_Tick(sender, e);

        private void MaskStrokePreviewCommitSwapTimer_Tick(object sender, EventArgs e)
            => maskStrokeWorkflowAdapter.MaskStrokePreviewCommitSwapTimer_Tick(sender, e);

        private void LearningWorkflowViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
            => maskStrokeWorkflowAdapter.LearningWorkflowViewModel_PropertyChanged(sender, e);

        private void ResetMaskStrokeStateForImageChange()
            => maskStrokeWorkflowAdapter.ResetForImageChange();

        private void ResetMaskStrokeStateAfterHistoryRestore()
            => maskStrokeWorkflowAdapter.ResetAfterHistoryRestore();

        private IReadOnlyCollection<int> GetActiveMaskStrokeSegmentIndices()
            => maskStrokeWorkflowAdapter.ActiveMaskStrokeSegmentIndices;

        private bool HasActiveMaskStrokeFullObjectRefresh()
            => maskStrokeWorkflowAdapter.ActiveMaskStrokeNeedsFullObjectRefresh;
        #endregion

        #region AnnotationHistoryAdapterFacade
        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
            => annotationHistoryAdapter.CaptureAnnotationHistory(actionName);

        private WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
            => annotationHistoryAdapter.CaptureManualRoiHistory(actionName);

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true)
            => annotationHistoryAdapter.RegisterAnnotationHistoryBeforeChange(actionName, markDirty);

        private void RegisterRoiEditHistoryBeforeChange(string overlayId, string actionName)
            => annotationHistoryAdapter.RegisterRoiEditHistoryBeforeChange(overlayId, actionName);

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot, bool markDirty = true)
            => annotationHistoryAdapter.PushAnnotationHistorySnapshot(snapshot, markDirty);

        private void ClearAnnotationHistory()
            => annotationHistoryAdapter.ClearAnnotationHistory();

        private void RefreshAnnotationHistoryToolState()
            => annotationHistoryAdapter.RefreshAnnotationHistoryToolState();

        private bool UndoWpfAnnotationHistory()
            => annotationHistoryAdapter.UndoWpfAnnotationHistory();

        private void ExecuteUndoAnnotationCommand()
            => annotationHistoryAdapter.ExecuteUndoAnnotationCommand();

        private void ExecuteRedoAnnotationCommand()
            => annotationHistoryAdapter.ExecuteRedoAnnotationCommand();

        private bool RedoWpfAnnotationHistory()
            => annotationHistoryAdapter.RedoWpfAnnotationHistory();

        private void ResetActiveRoiEditHistory()
            => annotationHistoryAdapter.ResetActiveRoiEditHistory();
        #endregion

        #region AnnotationLoadAdapterFacade
        private int LoadSavedBoxAnnotationsForActiveImage(string imagePath)
            => annotationLoadAdapter.LoadSavedBoxAnnotationsForActiveImage(imagePath);

        private int LoadSavedSegmentationAnnotationsForActiveImage(string imagePath)
            => annotationLoadAdapter.LoadSavedSegmentationAnnotationsForActiveImage(imagePath);
        #endregion

        #region AnnotationSaveStateAdapterFacade
        private void MarkAnnotationsDirty(string reason)
            => annotationSaveStateAdapter.MarkAnnotationsDirty(reason);

        private void MarkMaskStrokeAnnotationsDirty(string reason)
            => annotationSaveStateAdapter.MarkMaskStrokeAnnotationsDirty(reason);

        private void RefreshDeferredMaskStrokeDirtyPresentation()
            => annotationSaveStateAdapter.RefreshDeferredMaskStrokeDirtyPresentation();

        private void ApplyAnnotationDirtyPresentation()
            => annotationSaveStateAdapter.ApplyAnnotationDirtyPresentation();

        private void MarkAnnotationsSaved(string reason)
            => annotationSaveStateAdapter.MarkAnnotationsSaved(reason);

        private void SetAnnotationSaveStatusWaiting()
            => annotationSaveStateAdapter.SetAnnotationSaveStatusWaiting();

        private void ApplyAnnotationSaveStatePresentation(AnnotationSaveStatePresentation presentation)
            => annotationSaveStateAdapter.ApplyAnnotationSaveStatePresentation(presentation);
        #endregion

        #region AnnotationPersistenceAdapterFacade
        private bool SaveCurrentAnnotations(out int savedCount)
            => annotationPersistenceAdapter.SaveCurrentAnnotations(out savedCount);

        private bool SaveCurrentEmptyAnnotations()
            => annotationPersistenceAdapter.SaveCurrentEmptyAnnotations();

        private Dictionary<string, List<AnnotationRectangleObject>> BuildAnnotationRois()
            => annotationPersistenceAdapter.BuildAnnotationRois();

        private Dictionary<string, List<LabelingSegmentationObject>> BuildAnnotationSegments()
            => annotationPersistenceAdapter.BuildAnnotationSegments();
        #endregion

        private bool EnsureInferenceModeForDetection()
            => workflowNavigationAdapter.EnsureInferenceModeForDetection();

        private void ExecuteDatasetHomeCommand()
            => workflowNavigationAdapter.ExecuteDatasetHomeCommand();

        private void ExecuteLabelingWorkbenchCommand()
            => workflowNavigationAdapter.ExecuteLabelingWorkbenchCommand();

        private void EnterLabelingWorkbenchStartView()
            => workflowNavigationAdapter.EnterLabelingWorkbenchStartView();

        private void ExecuteInferenceReviewCommand()
            => workflowNavigationAdapter.ExecuteInferenceReviewCommand();

        private void ExecuteTrainingModelCenterCommand()
            => workflowNavigationAdapter.ExecuteTrainingModelCenterCommand();

        private void ExecuteReviewCandidateModelCommand()
            => workflowNavigationAdapter.ExecuteReviewCandidateModelCommand();

        private void ApplyWorkflowDatasetPurposeSelection(LabelingDatasetPurpose purpose)
            => workflowNavigationAdapter.ApplyWorkflowDatasetPurposeSelection(purpose);

        private void ApplyLearningModeWorkflowAction(WpfLearningModeWorkflowAction action)
            => workflowNavigationAdapter.ApplyLearningModeWorkflowAction(action);

        private void UpdateYoloCommandButtons()
            => workflowNavigationAdapter?.UpdateYoloCommandButtons();

        private void ScheduleDisplayAdjustmentRefresh()
        {
            displayWorkspaceAdapter.ScheduleDisplayAdjustmentRefresh();
        }

        private void DisplayAdjustmentRefreshTimer_Tick(object sender, EventArgs e)
        {
            displayWorkspaceAdapter.HandleDisplayAdjustmentRefreshTimerTick(sender, e);
        }

        private void ApplyDisplayAdjustmentNow()
        {
            displayWorkspaceAdapter.ApplyDisplayAdjustmentNow();
        }

        private void MainCanvasView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            displayWorkspaceAdapter.HandleCanvasViewSizeChanged(sender, e);
        }

        private void SetDatasetStatus(string text)
        {
            statusPresentationAdapter.SetDatasetStatus(text);
        }

        private void SetPythonStatus(string text)
        {
            statusPresentationAdapter.SetPythonStatus(text);
        }

        private void UpdateWorkflowProgressStatus()
        {
            statusPresentationAdapter.UpdateWorkflowProgressStatus();
        }

        private void SetModelStatus(string text)
        {
            statusPresentationAdapter.SetModelStatus(text);
        }

        private void SetInspectionModelStatus(string text, string toolTip = null)
        {
            statusPresentationAdapter.SetInspectionModelStatus(text, toolTip);
        }

        private ExternalYoloDatasetIntakeCallbacks CreateExternalYoloDatasetIntakeCallbacks()
        {
            return externalYoloDatasetIntakeAdapter.CreateExternalYoloDatasetIntakeCallbacks();
        }

        private void ClearExternalYoloDatasetSelection()
        {
            externalYoloDatasetIntakeAdapter.ClearExternalYoloDatasetSelection();
        }

        private bool ApplyExternalYoloDatasetValidation(
            YoloExternalDatasetIntakeReport report,
            string dataYamlFilePath,
            LabelingDatasetPurpose purpose,
            bool useForNextTraining)
        {
            return externalYoloDatasetIntakeAdapter.ApplyExternalYoloDatasetValidation(
                report,
                dataYamlFilePath,
                purpose,
                useForNextTraining);
        }

        private ExternalYoloDatasetSettings GetExternalYoloDatasetSettings()
        {
            return externalYoloDatasetIntakeAdapter.GetExternalYoloDatasetSettings();
        }

        private void RefreshExternalYoloDatasetIntakePresentation()
        {
            externalYoloDatasetIntakeAdapter.RefreshExternalYoloDatasetIntakePresentation();
        }

        private bool TrySaveExternalYoloDatasetSettings()
        {
            return externalYoloDatasetIntakeAdapter.TrySaveExternalYoloDatasetSettings();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private void ExecuteToggleThemeCommand()
        {
            ApplyTheme(ShellTheme.Dark);
            AppendLog("테마 고정: 다크");
        }

        private void ApplyTheme(ShellTheme theme)
        {
            // Theme selection is intentionally hidden for the focused workstation product.
            // Keep legacy callers safe by treating every request as the supported dark theme.
            theme = ShellTheme.Dark;
            currentTheme = theme;
            WpfUiApplicationThemeManager.Apply(WpfUiApplicationTheme.Dark, WpfUiWindowBackdropType.None, updateAccent: true);
            WpfUiApplicationThemeManager.Apply(this);

            ThemePalette.ApplyDark(Resources, System.Windows.Application.Current?.Resources);

            if (FindResource("AppBackgroundBrush") is MediaBrush backgroundBrush)
            {
                Background = backgroundBrush;
            }

            auxiliaryWindowHost.RefreshTheme();
            datasetTransferWindowHost.RefreshTheme();
            patchCoreHeatmapWindowHost.RefreshTheme();
            UpdateWorkflowModeUi();
            UpdateQueueQuickFilterButtons();
        }

        private void AppendLog(string message)
        {
            ShellLogViewModel?.RecordLog(message);
            OVLog.Write(LogCategory.Main, LogLevel.Info, message);
        }

        private void ShellViewModel_WorkspaceLayoutSaveFailed(object sender, EventArgs e)
        {
            AppendLog("패널 너비 저장 실패: " + ShellViewModel.WorkspaceLayoutLastSaveError);
        }

        private void ShellViewModel_WorkspaceLayoutReset(object sender, EventArgs e)
        {
            AppendLog("패널 너비 초기화: 작업 패널 340px / 이미지 큐 320px");
        }

        private CanvasWorkflowContextSnapshot CaptureCanvasWorkflowContext()
        {
            WpfLearningStepItem selectedStep = LearningWorkflowViewModel?.SelectedStep;
            WpfAnnotationToolItem selectedTool = CanvasPanelViewModel?.SelectedAnnotationTool
                ?? LearningWorkflowViewModel?.SelectedTool;
            return new CanvasWorkflowContextSnapshot(
                IsInferenceWorkflowActive,
                IsAnomalyDatasetPurpose(),
                !applicationState.ImageWorkspace.ActiveImageSize.IsEmpty,
                annotationDirtyState.IsDirty,
                HasCanvasLabelObjects(),
                pendingDetectionCandidates?.Count ?? 0,
                selectedStep?.Step,
                selectedStep?.Text,
                selectedTool?.Tool,
                selectedTool?.Text,
                activeAnnotationTool,
                CanvasPanelViewModel?.SelectedBoxDrawingMethod?.Method);
        }

        private void RefreshCanvasWorkflowContext()
        {
            canvasWorkflowContextPresenter.Refresh();
        }

        private void RefreshCanvasAnnotationToolScope()
        {
            canvasAnnotationToolScopePresenter.Refresh();
        }

        private void ConfigureCanvasPanelCommands()
        {
            new CanvasPanelCommandWiring(new CanvasPanelCommandWiringContext
            {
                CanvasPanelViewModel = CanvasPanelViewModel,
                LearningWorkflowViewModel = LearningWorkflowViewModel,
                CanvasAnnotationToolListBox = CanvasAnnotationToolListBox,
                CanvasLabelClassListBox = CanvasLabelClassListBox,
                CanvasDisplayModeListBox = CanvasDisplayModeListBox,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings,
                ExecuteFitCanvasCommand = ExecuteFitCanvasCommand,
                ExecuteActualSizeCanvasCommand = ExecuteActualSizeCanvasCommand,
                ExecutePanCanvasCommand = ExecutePanCanvasCommand,
                ExecuteFocusCandidateCommand = ExecuteFocusCandidateCommand,
                ExecuteResetAiOverlayCommand = ExecuteResetAiOverlayCommand,
                ScheduleDisplayAdjustmentRefresh = ScheduleDisplayAdjustmentRefresh,
                ExecutePreviousCandidateCommand = ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand = ExecuteNextCandidateCommand,
                ExecuteFocusCurrentLabelCommand = ExecuteFocusCurrentLabelCommand,
                ExecuteConfirmSelectedCandidateCommand = ExecuteConfirmSelectedCandidateCommand,
                ExecuteSkipSelectedCandidateCommand = ExecuteSkipSelectedCandidateCommand,
                ExecuteCanvasAnnotationToolSelectionChanged = ExecuteCanvasAnnotationToolSelectionChanged,
                ExecuteUndoAnnotationCommand = ExecuteUndoAnnotationCommand,
                ExecuteRedoAnnotationCommand = ExecuteRedoAnnotationCommand,
                ExecuteDeleteObjectCommand = ExecuteDeleteObjectCommand,
                ExecuteSaveAnnotationsCommand = ExecuteSaveAnnotationsCommand,
                ExecuteCompleteNoObjectAndNextCommand = ExecuteCompleteNoObjectAndNextCommand,
                ShowClassCatalogWorkflowView = ShowClassCatalogWorkflowView,
                CancelFourPointBoxDraft = updateStatus => CancelFourPointBoxDraft(updateStatus),
                SelectClassCatalogClass = className => ClassCatalogViewModel?.SelectClass(className),
                RefreshObjectClassOptions = RefreshObjectClassOptions,
                ApplyCanvasDisplayMode = mode => ApplyCanvasDisplayMode(mode, redraw: true, logChange: true),
                ExecuteSetBoxDrawingMethod = ExecuteSetBoxDrawingMethod,
                ExecuteDecreaseBrushSizeCommand = ExecuteDecreaseBrushSizeCommand,
                ExecuteIncreaseBrushSizeCommand = ExecuteIncreaseBrushSizeCommand,
                ExecuteCreateSmartMaskCandidateCommand = ExecuteCreateSmartMaskCandidateCommand,
                ExecuteSetSmartMaskPointModeCommand = ExecuteSetSmartMaskPointModeCommand,
                ExecuteUndoSmartMaskPointCommand = ExecuteUndoSmartMaskPointCommand,
                ExecuteClearSmartMaskPointsCommand = ExecuteClearSmartMaskPointsCommand,
                ExecuteCancelSmartMaskGenerationCommand = ExecuteCancelSmartMaskGenerationCommand,
                ExecuteNextSmartMaskInstanceCommand = ExecuteNextSmartMaskInstanceCommand,
                ExecuteSelectSmartMaskCandidateVersionCommand = ExecuteSelectSmartMaskCandidateVersionCommand,
                ExecuteSetSmartMaskAutoContourMode = ExecuteSetSmartMaskAutoContourMode,
                ExecuteSetSmartMaskPolygonDetailCommand = ExecuteSetSmartMaskPolygonDetailCommand,
                SyncCanvasBrushSizeFromWorkflow = SyncCanvasBrushSizeFromWorkflow,
                RefreshCanvasAnnotationToolScope = RefreshCanvasAnnotationToolScope,
                RefreshCanvasWorkflowContext = RefreshCanvasWorkflowContext
            }).ConfigureCanvasPanelCommands();
        }

        private void ConfigureLearningWorkflowPanelCommands()
        {
            new LearningWorkflowPanelCommandWiring(new LearningWorkflowPanelCommandWiringContext
            {
                LearningWorkflowViewModel = LearningWorkflowViewModel,
                ExecuteStartDatasetSetupCommand = ExecuteStartDatasetSetupCommand,
                ExecuteYoloTrainingWorkflowStep = step => ExecuteYoloTrainingWorkflowStep(
                    step?.Order ?? 0,
                    LearningWorkflowPanelControl),
                ExecuteOpenTutorialHtmlGuideCommand = ExecuteOpenTutorialHtmlGuideCommand,
                ExecuteFixYoloClassesCommand = ExecuteFixYoloClassesCommand,
                ExecuteFixYoloLabelsCommand = ExecuteFixYoloLabelsCommand,
                ExecuteFixYoloDatasetCommand = ExecuteFixYoloDatasetCommand,
                ExecuteDatasetDashboardMetricCommand = ExecuteDatasetDashboardMetricCommand,
                ExecuteChangeDatasetCommand = ExecuteChangeDatasetCommand,
                ExecuteFirstRunSamplePathCommand = ExecuteFirstRunSamplePathCommand,
                ExecuteTemplateCurrentImageCommand = TemplateMatchingAutoLabelViewModel.RunCurrentImage,
                ExecuteTemplateBatchCommand = TemplateMatchingAutoLabelViewModel.RunBatch,
                ApplyLearningModeWorkflowAction = ApplyLearningModeWorkflowAction,
                ApplyLearningStepWorkflowAction = ApplyLearningStepWorkflowAction,
                ApplyAnnotationToolSelection = ApplyAnnotationToolSelection,
                DatasetPurposeSelectionGuard = selected =>
                    DatasetPurposeListBox == null || ReferenceEquals(DatasetPurposeListBox.SelectedItem, selected),
                ApplyWorkflowDatasetPurposeSelection = ApplyWorkflowDatasetPurposeSelection,
                ExternalAuditWorkflowService = externalAuditWorkflowService,
                ExternalEvaluationDataAuditInitialDirectoryProvider = () =>
                {
                    string initialDirectory = LearningWorkflowViewModel?.ExternalEvaluationDataAuditPathText;
                    if (!Directory.Exists(initialDirectory))
                    {
                        string currentImageRoot = ImageQueueViewModel?.CurrentImageFolderPath ?? string.Empty;
                        initialDirectory = Directory.Exists(currentImageRoot) ? currentImageRoot : string.Empty;
                    }

                    return initialDirectory;
                },
                ExternalEvaluationDataAuditDirectorySelector = initialDirectory => TryPickFolder(
                    "\uC678\uBD80 \uD3C9\uAC00 \uD3F4\uB354 \uB300\uC870",
                    initialDirectory,
                    out string selectedDirectory)
                    ? selectedDirectory
                    : string.Empty,
                ExternalEvaluationDataAuditReferenceDirectoriesProvider = () =>
                    applicationState.Data?.GetDatasetImageDirectories() ?? Array.Empty<string>(),
                ExternalEvaluationDataAuditCloseApproval = () => isApplicationCloseApproved,
                ExternalEvaluationDataAuditStatusSink = status =>
                {
                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        AppendLog(status);
                    }
                },
                HistoricalSegmentationRemediationDataProvider = () => applicationState.Data,
                HistoricalSegmentationRemediationSourceImagePathProvider = () =>
                    TemplateMatchingAutoLabelViewModel?.RegisteredTemplateSourceImagePath ?? string.Empty,
                HistoricalSegmentationRemediationCloseApproval = () => isApplicationCloseApproved,
                HistoricalSegmentationRemediationStatusSink = (status, log) =>
                {
                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        SetModelStatus(status);
                    }

                    if (!string.IsNullOrWhiteSpace(log))
                    {
                        AppendLog(log);
                    }
                },
                ModelComparisonWorkflowService = modelComparisonWorkflowService,
                CreateModelComparisonCallbacks = CreateModelComparisonCallbacks,
                ExternalYoloDatasetIntakeWorkflowService = externalYoloDatasetIntakeWorkflowService,
                CreateExternalYoloDatasetIntakeCallbacks = CreateExternalYoloDatasetIntakeCallbacks,
                DatasetPurposeListBox = DatasetPurposeListBox,
                LearningModeListBox = LearningModeListBox,
                AnnotationToolListBox = AnnotationToolListBox,
                LearningStepListBox = LearningStepListBox,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings
            }).ConfigureLearningWorkflowPanelCommands();
        }

        private void ConfigureClassCatalogPanelCommands()
        {
            new ClassCatalogPanelCommandWiring(new ClassCatalogPanelCommandWiringContext
            {
                ClassCatalogViewModel = ClassCatalogViewModel,
                ClassCatalogWorkflowService = classCatalogWorkflowService,
                DataProvider = () => applicationState.Data,
                RecipeNameProvider = () => applicationState.Recipe.Name,
                CloseApprovedProvider = () => isApplicationCloseApproved,
                CancelPendingFourPointBoxDraft = () => CancelFourPointBoxDraft(updateStatus: false),
                SelectCanvasClass = className => CanvasPanelViewModel?.SelectLabelClass(className),
                RefreshObjectClassOptions = RefreshObjectClassOptions,
                MutationCompleted = HandleClassCatalogMutationCompleted,
                ClassNameBox = ClassNameBox,
                ClassListBox = ClassListBox,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings
            }).ConfigureClassCatalogPanelCommands();
        }

        private void ConfigureYoloStatusPanelCommands()
        {
            new StatusPanelCommandWiring(new StatusPanelCommandWiringContext
            {
                YoloStatusViewModel = YoloStatusViewModel,
                ExecuteRunYoloSmokeCommand = ExecuteRunYoloSmokeCommand,
                YoloEnvironmentWorkflowService = yoloEnvironmentWorkflowService,
                CreateYoloEnvironmentCallbacks = CreateYoloEnvironmentCallbacks
            }).ConfigureYoloStatusPanelCommands();
        }

        private void ConfigureYoloModelSettingsPanelCommands()
        {
            new ModelSettingsPanelCommandWiring(new ModelSettingsPanelCommandWiringContext
            {
                ModelSettingsViewModel = YoloModelSettingsViewModel,
                ExecuteSaveSettingsCommand = ExecuteSaveYoloSettingsCommand,
                ExecuteResetSettingsCommand = ExecuteResetYoloSettingsCommand,
                ExecuteRuntimeProfileActionCommand = ExecuteRuntimeProfileActionCommand,
                ExecuteRuntimeInstallPackageCommand = ExecuteInstallUltralyticsPackageCommand,
                ExecuteRuntimeUninstallPackageCommand = ExecuteUninstallUltralyticsPackageCommand,
                CancelSettingsChanges = PopulateYoloEditorFields,
                CreatePathSelectionCallbacks = CreateYoloModelSettingsPathCallbacks
            }).ConfigureModelSettingsPanelCommands();
        }

        private void ConfigureProjectConfigPanelCommands()
        {
            new ProjectConfigPanelCommandWiring(new ProjectConfigPanelCommandWiringContext
            {
                ProjectConfigViewModel = ProjectConfigViewModel,
                ProjectRecipeSessionService = projectRecipeSessionService,
                ProjectRecipeApplyWorkflowService = projectRecipeApplyWorkflowService,
                ApplicationStateProvider = () => applicationState,
                CloseApprovedProvider = () => isApplicationCloseApproved,
                 CreateNavigationCallbacks = projectSettingsWorkflowAdapter.CreateProjectConfigNavigationCallbacks,
                 ProjectArchiveWorkflowService = projectArchiveWorkflowService,
                 CreateArchiveCallbacks = projectSettingsWorkflowAdapter.CreateProjectConfigArchiveCallbacks,
                 RecipeSelectionGuard = selected => !projectSettingsWorkflowAdapter.IsRecipeSelectionRefreshActive
                     && (ProjectRecipeListBox == null
                         || string.Equals(ProjectRecipeListBox.SelectedItem as string, selected, StringComparison.Ordinal)),
                 ProjectConfigSaved = projectSettingsWorkflowAdapter.HandleProjectConfigSaved,
                 ProjectRecipeApplied = projectSettingsWorkflowAdapter.HandleProjectRecipeApplied,
                 WorkflowError = projectSettingsWorkflowAdapter.HandleProjectConfigWorkflowError,
                RecipeListBox = ProjectRecipeListBox,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings
            }).ConfigureProjectConfigPanelCommands();
        }

        private void ConfigureTrainingSettingsPanelCommands()
        {
            new TrainingSettingsPanelCommandWiring(new TrainingSettingsPanelCommandWiringContext
            {
                TrainingSettingsViewModel = TrainingSettingsViewModel,
                ExecuteReviewTrainedModelCommand = ExecuteReviewCandidateModelCommand,
                ExecuteConfirmTrainedModelCommand = ExecuteSaveYoloSettingsCommand,
                TrainingRuntimeWorkflowService = trainingRuntimeWorkflowService,
                TrainingCommandLifecycleService = trainingCommandLifecycleService,
                CreateTrainingRuntimeCallbacks = CreateTrainingRuntimeCallbacks,
                ModelComparisonWorkflowService = modelComparisonWorkflowService,
                CreateModelComparisonCallbacks = CreateModelComparisonCallbacks,
                CreateSegmentationAdapterComparisonContext = modelComparisonWorkflowAdapter.BuildSegmentationAdapterComparisonContext,
                CreateSegmentationCheckpointPathCallbacks = () => new SegmentationCheckpointPathCallbacks
                {
                    SelectFile = (title, filter, initialPath) => TryPickFile(
                        title,
                        filter,
                        initialPath,
                        out string selectedPath)
                        ? selectedPath
                        : string.Empty,
                    IsApplicationCloseApproved = () => isApplicationCloseApproved,
                    PathSelected = TrainingSettingsViewModel.RefreshSegmentationAdapterComparisonContext
                }
            }).ConfigureTrainingSettingsPanelCommands();
        }

        private void ConfigureImageQueuePanelCommands()
        {
            new ImageQueuePanelCommandWiring(new ImageQueuePanelCommandWiringContext
            {
                ImageQueueViewModel = ImageQueueViewModel,
                ExecuteLoadImageRootCommand = ExecuteLoadImageRootQueueCommand,
                ExecuteBrowseImageFolderCommand = ExecuteBrowseImageFolderCommand,
                ExecuteOpenCurrentImageFolderCommand = ExecuteOpenCurrentImageFolderCommand,
                ExecuteRefreshImageQueueCommand = ExecuteRefreshImageQueueCommand,
                ExecuteNextUnlabeledQueueCommand = ExecuteNextUnlabeledQueueCommand,
                ExecuteOpenSelectedQueueImageCommand = ExecuteOpenSelectedQueueImageCommand,
                ExecuteDetectSelectedQueueCommand = ExecuteDetectSelectedQueueCommand,
                ExecuteBatchDetectQueueCommand = ExecuteBatchDetectQueueCommand,
                ExecuteTemplateBatchQueueCommand = TemplateMatchingAutoLabelViewModel.RunBatch,
                ExecuteRetryFailedQueueCommand = ExecuteRetryFailedQueueCommand,
                ExecuteStopBatchQueueCommand = ExecuteStopBatchQueueCommand,
                ExecuteSelectedQueueItemChanged = imageQueueReviewAdapter.ExecuteSelectedQueueItemChanged,
                ImageQueueFilterSelectionChanged = selected => imageQueueReviewAdapter.ApplyFilterSelectionChanged(),
                SetImageQueueFilter = imageQueueReviewAdapter.SetImageQueueFilter,
                ApplyImageQueueSearchChanged = imageQueueReviewAdapter.ApplySearchChanged,
                ExecuteApplyAnomalyFolderStateSuggestionCommand = ExecuteApplyAnomalyFolderStateSuggestionCommand,
                ExecuteDismissAnomalyFolderStateSuggestionCommand = ExecuteDismissAnomalyFolderStateSuggestionCommand,
                ExecuteMarkActiveAnomalyNormalAndNextCommand = ExecuteMarkActiveAnomalyNormalAndNextCommand,
                ExecuteMarkActiveAnomalyAbnormalAndNextCommand = ExecuteMarkActiveAnomalyAbnormalAndNextCommand,
                ExecuteClearActiveAnomalyReviewCommand = ExecuteClearActiveAnomalyReviewCommand,
                ImageQueueFilterBox = ImageQueueFilterBox,
                ImageQueueSearchBox = ImageQueueSearchBox,
                ImageQueueGrid = ImageQueueGrid,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings,
                SeedImageQueueInputCommands = SeedImageQueueInputCommands
            }).ConfigureImageQueuePanelCommands();
        }

        private void InitializeImageQueuePanel()
        {
            ConfigureImageQueuePanelCommands();
            imageQueueView = ImageQueuePanelControl?.InitializeQueue(imageQueueItems);
            UpdateQueueQuickFilterButtons();
        }

        private void RefreshImageQueueViewAfterItemStateChange()
        {
            ImageQueuePanelControl?.RefreshQueueViewAfterItemStateChange();
        }

        private void ConfigureCandidateReviewPanelCommands()
        {
            new CandidateReviewPanelCommandWiring(new CandidateReviewPanelCommandWiringContext
            {
                CandidateReviewViewModel = CandidateReviewViewModel,
                ExecuteCandidateConfidenceChangedCommand = ExecuteCandidateConfidenceChangedCommand,
                ExecuteConfirmSelectedCandidateCommand = ExecuteConfirmSelectedCandidateCommand,
                ExecuteConfirmAllCandidatesCommand = ExecuteConfirmAllCandidatesCommand,
                ExecuteSkipSelectedCandidateCommand = ExecuteSkipSelectedCandidateCommand,
                ExecutePreviousCandidateCommand = ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand = ExecuteNextCandidateCommand,
                ExecuteFocusCandidateCommand = ExecuteFocusCandidateCommand,
                ExecuteFocusCurrentLabelCommand = ExecuteFocusCurrentLabelCommand,
                ExecuteCompleteImageAndNextCommand = ExecuteCompleteImageAndNextCommand,
                ExecuteOpenModelComparisonExampleCommand = ExecuteOpenModelComparisonExampleCommand,
                ExecuteSaveModelCandidateCommand = ExecuteSaveModelCandidateCommand,
                ExecuteRejectModelCandidateCommand = ExecuteRejectModelCandidateCommand,
                ExecuteTogglePatchCoreHeatmapCommand = ExecuteTogglePatchCoreHeatmapCommand,
                ApplyCandidateSelectionChangedEffects = ApplyCandidateSelectionChangedEffects,
                ApplyModelComparisonHistorySelectionEffects = ApplyModelComparisonHistorySelectionEffects,
                CandidateConfidenceSlider = CandidateConfidenceSlider,
                CandidateListBox = CandidateListBox,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings
            }).ConfigureCandidateReviewPanelCommands();
        }

        private void ConfigureObjectReviewPanelCommands()
        {
            new ObjectReviewPanelCommandWiring(new ObjectReviewPanelCommandWiringContext
            {
                ObjectReviewViewModel = ObjectReviewViewModel,
                ExecuteDeleteObjectCommand = ExecuteDeleteObjectCommand,
                ExecuteApplyObjectClassCommand = ExecuteApplyObjectClassCommand,
                ExecuteMarkQualityUnreviewedCommand = ExecuteMarkQualityUnreviewedCommand,
                ExecuteMarkQualityNeedsFixCommand = ExecuteMarkQualityNeedsFixCommand,
                ExecuteMarkQualityReviewedCommand = ExecuteMarkQualityReviewedCommand,
                ExecuteExportQualityReviewReportCommand = ExecuteExportQualityReviewReportCommand,
                ExecuteObjectSelectionChangedCommand = ExecuteObjectSelectionChangedCommand,
                ExecuteObjectPreviewKeyDownCommand = null,
                ExecuteMergeSelectedSegmentsCommand = ExecuteMergeSelectedSegmentsCommand,
                ExecuteBeginVerticalSegmentationSplitCommand = ExecuteBeginVerticalSegmentationSplitCommand,
                ExecuteBeginHorizontalSegmentationSplitCommand = ExecuteBeginHorizontalSegmentationSplitCommand,
                ExecuteCancelSegmentationSplitCommand = ExecuteCancelSegmentationSplitCommand,
                ExecuteBeginAddSegmentationHoleCommand = ExecuteBeginAddSegmentationHoleCommand,
                ExecuteBeginRemoveSegmentationHoleCommand = ExecuteBeginRemoveSegmentationHoleCommand,
                ExecuteCancelSegmentationHoleEditCommand = ExecuteCancelSegmentationHoleEditCommand,
                ExecuteBeginInsertPolygonVertexCommand = ExecuteBeginInsertPolygonVertexCommand,
                ExecuteBeginDeletePolygonVertexCommand = ExecuteBeginDeletePolygonVertexCommand,
                ExecuteCancelPolygonVertexEditCommand = ExecuteCancelPolygonVertexEditCommand,
                ExecuteBeginIntelligentScissorsCommand = ExecuteBeginIntelligentScissorsCommand,
                ExecuteApplyIntelligentScissorsCommand = ExecuteApplyIntelligentScissorsCommand,
                ExecuteCancelIntelligentScissorsCommand = ExecuteCancelIntelligentScissorsCommand,
                ExecuteSendSegmentationToBackCommand = ExecuteSendSegmentationToBackCommand,
                ExecuteSendSegmentationBackwardCommand = ExecuteSendSegmentationBackwardCommand,
                ExecuteBringSegmentationForwardCommand = ExecuteBringSegmentationForwardCommand,
                ExecuteBringSegmentationToFrontCommand = ExecuteBringSegmentationToFrontCommand,
                ExecutePreviewSegmentationRemoveUnderlyingCommand = ExecutePreviewSegmentationRemoveUnderlyingCommand,
                ExecuteApplySegmentationRemoveUnderlyingCommand = ExecuteApplySegmentationRemoveUnderlyingCommand,
                ExecuteCancelSegmentationRemoveUnderlyingCommand = ExecuteCancelSegmentationRemoveUnderlyingCommand,
                ObjectReviewWorkflowService = objectReviewWorkflowService,
                DeleteSelectedObject = DeleteSelectedObject,
                ApplyObjectSessionStateMutation = ApplyObjectSessionStateMutation,
                ReportObjectReviewStatus = status => SetYoloCommandStatus(status, isBusy: false),
                CaptureObjectReviewSnapshots = CaptureObjectReviewSnapshots,
                ApplyObjectPersistentOccludedMutation = ApplyObjectPersistentOccludedMutation,
                ReportObjectReviewWorkflowError = ReportObjectReviewWorkflowError,
                GetApplicationData = () => applicationState.Data,
                GetCurrentRecipeName = GetCurrentRecipeName,
                ApplyObjectPersistentTagMutation = ApplyObjectPersistentTagMutation,
                ApplyObjectRecipeMetadataResetMutation = ApplyObjectRecipeMetadataResetMutation,
                ApplyCreatedObjectGroupMutation = ApplyCreatedObjectGroupMutation,
                ApplyObjectGroupOccludedMutation = ApplyObjectGroupOccludedMutation,
                ApplyObjectGroupMemberRemovalMutation = ApplyObjectGroupMemberRemovalMutation,
                ApplyObjectGroupTagMutation = ApplyObjectGroupTagMutation,
                ConfirmObjectGroupDissolve = ConfirmObjectGroupDissolve,
                ApplyObjectGroupDissolveMutation = ApplyObjectGroupDissolveMutation,
                ObjectListBox = ObjectListBox,
                RefreshAttachedCommandBindings = RefreshAttachedCommandBindings
            }).ConfigureObjectReviewPanelCommands();
            ObjectReviewViewModel.WorkflowStatusChanged -= HandleObjectReviewWorkflowStatusChanged;
            ObjectReviewViewModel.WorkflowStatusChanged += HandleObjectReviewWorkflowStatusChanged;
        }

        private bool FocusSelectedCandidateInViewer(bool logIfMissing)
            => candidateReviewViewerNavigator.FocusSelectedCandidateInViewer(logIfMissing);

        private void ExecuteFocusCurrentLabelCommand()
        {
            candidateReviewViewerNavigator.ExecuteFocusCurrentLabelCommand();
        }

        private bool FocusCurrentLabelForSelectedCandidate(bool logIfMissing)
            => candidateReviewViewerNavigator.FocusCurrentLabelForSelectedCandidate(logIfMissing);

        private bool FocusCandidateInViewer(YoloWorkerSmokeCandidate candidate, bool logIfMissing)
            => candidateReviewViewerNavigator.FocusCandidateInViewer(candidate, logIfMissing);

        private DrawingRectangleF BuildCandidateFocusRect(DrawingRectangle bounds)
            => candidateReviewViewerNavigator.BuildCandidateFocusRectForShell(bounds);

        private bool HasCanvasLabelObjects()
            => GetCanvasLabelObjectCount() > 0;

        private int GetCanvasLabelObjectCount()
            => manualRois.Count + GetVisibleManualSegmentCount() + confirmedDetectionCandidates.Count;

        private bool ExecuteOpenDatasetHealthImageInEditor(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            EnterLabelingWorkbenchStartView();
            if (!TryLoadImage(
                imagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: true,
                appendLoadLog: true))
            {
                return false;
            }

            datasetTransferWindowHost.CloseDatasetHealth();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            AppendLog($"Dataset Health 시각 QA에서 편집기로 이동: {imagePath}");
            return true;
        }

        private void ExecuteOpenEnvironmentSetupCenterCommand()
        {
            HeaderToolsPopup.IsOpen = false;
            auxiliaryWindowHost.ShowEnvironmentSetupCenter();
        }

        private void ExecuteOpenModelBenchmarkCommand()
        {
            auxiliaryWindowHost.ShowModelBenchmark();
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

        private static void SetControlEnabled(System.Windows.Controls.Control control, bool isEnabled)
        {
            if (control != null)
            {
                control.IsEnabled = isEnabled;
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







        // WPF panel accessors and composition stay at the generated shell composition root.
        // This is framework-bound wiring; workflow policy remains in ViewModels and adapters.
        #region PanelAccessors
        // UI element proxies are centralized while panels finish moving to pure ViewModel bindings; this keeps legacy name lookups out of command logic.
        // The shell is the composition root: keep UserControls ViewModel-free and wire panel commands here.
        public WpfLabelingShellViewModel ShellViewModel => viewModels.ShellViewModel;

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => viewModels.LearningWorkflowViewModel;

        public WpfImageQueuePanelViewModel ImageQueueViewModel => viewModels.ImageQueueViewModel;

        public WpfTemplateMatchingAutoLabelViewModel TemplateMatchingAutoLabelViewModel => viewModels.TemplateMatchingAutoLabelViewModel;

        public WpfCanvasPanelViewModel CanvasPanelViewModel => viewModels.CanvasPanelViewModel;

        public WpfObjectReviewPanelViewModel ObjectReviewViewModel => viewModels.ObjectReviewViewModel;

        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.CandidateReviewViewModel;
            }
        }

        public WpfCandidateReviewPanelViewModel ExistingCandidateReviewViewModel =>
            viewModels.CandidateReviewViewModel;

        public WpfClassCatalogPanelViewModel ClassCatalogViewModel => viewModels.ClassCatalogViewModel;

        public WpfYoloStatusPanelViewModel YoloStatusViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.YoloStatusViewModel;
            }
        }

        public WpfYoloStatusPanelViewModel ExistingYoloStatusViewModel =>
            viewModels.YoloStatusViewModel;

        public WpfProjectConfigPanelViewModel ProjectConfigViewModel => viewModels.ProjectConfigViewModel;

        public WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.YoloModelSettingsViewModel;
            }
        }

        public WpfYoloModelSettingsPanelViewModel ExistingYoloModelSettingsViewModel =>
            viewModels.YoloModelSettingsViewModel;

        public WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel
        {
            get
            {
                EnsureModelWorkflowPanelsComposed();
                return viewModels.ModelWorkflowViewModels.TrainingSettingsViewModel;
            }
        }

        public WpfTrainingSettingsPanelViewModel ExistingTrainingSettingsViewModel =>
            viewModels.TrainingSettingsViewModel;

        public WpfStatusBarPanelViewModel StatusBarViewModel => viewModels.StatusBarViewModel;

        public WpfShellLogPanelViewModel ShellLogViewModel => viewModels.ShellLogViewModel;

        public WpfRuntimeDiagnosticsViewModel RuntimeDiagnosticsViewModel => viewModels.RuntimeDiagnosticsViewModel;

        public RoiImageCanvasViewModel MainCanvasViewModel => viewModels.MainCanvasViewModel;

        private ListBox DatasetPurposeListBox => LearningWorkflowPanelControl?.DatasetPurposeList;
        private TextBlock DatasetPurposeSummaryText => LearningWorkflowPanelControl?.DatasetPurposeSummary;
        private TextBlock DatasetPurposeToolSummaryText => LearningWorkflowPanelControl?.DatasetPurposeToolSummary;
        private Border FirstRunSamplePathPanel => LearningWorkflowPanelControl?.FirstRunSamplePathPanelControl;
        private TextBlock FirstRunSamplePathTitleText => LearningWorkflowPanelControl?.FirstRunSamplePathTitle;
        private TextBlock FirstRunSamplePathSummaryText => LearningWorkflowPanelControl?.FirstRunSamplePathSummary;
        private TextBlock FirstRunSamplePathPrimaryActionText => LearningWorkflowPanelControl?.FirstRunSamplePathPrimaryAction;
        private ItemsControl FirstRunSamplePathItemsControl => LearningWorkflowPanelControl?.FirstRunSamplePathList;
        private Button DatasetSetupStartButton => LearningWorkflowPanelControl?.DatasetSetupStart;
        private Button DatasetOpenExistingButton => LearningWorkflowPanelControl?.DatasetOpenExisting;
        private TextBlock DatasetSetupStatusText => LearningWorkflowPanelControl?.DatasetSetupStatus;
        private TextBlock CurrentWorkflowActionText => LearningWorkflowPanelControl?.CurrentWorkflowAction;
        private ListBox LearningModeListBox => LearningWorkflowPanelControl?.ModeList;
        private ListBox AnnotationToolListBox => LearningWorkflowPanelControl?.ToolList;
        private ListBox LearningStepListBox => LearningWorkflowPanelControl?.StepList;
        private Expander LearningConceptsExpander => LearningWorkflowPanelControl?.LearningConcepts;
        private TextBlock GroundTruthChipText => LearningWorkflowPanelControl?.GroundTruthTextBlock;
        private TextBlock PredictionChipText => LearningWorkflowPanelControl?.PredictionTextBlock;
        private ItemsControl YoloTrainingWorkflowItemsControl => LearningWorkflowPanelControl?.YoloTrainingWorkflowList;
        private ItemsControl YoloCurrentTrainingProgressItemsControl => LearningWorkflowPanelControl?.YoloCurrentTrainingProgressList;
        private TextBlock YoloTrainingWorkflowSummaryText => LearningWorkflowPanelControl?.YoloTrainingWorkflowSummary;
        private TextBlock YoloTrainingChecklistStatusText => LearningWorkflowPanelControl?.YoloTrainingChecklistStatus;
        private TextBlock YoloTrainingChecklistDetailText => LearningWorkflowPanelControl?.YoloTrainingChecklistDetail;
        private TextBlock YoloTrainingChecklistActionText => LearningWorkflowPanelControl?.YoloTrainingChecklistAction;
        private TextBlock DatasetDashboardStatusText => LearningWorkflowPanelControl?.DatasetDashboardStatus;
        private TextBlock DatasetDashboardSummaryText => LearningWorkflowPanelControl?.DatasetDashboardSummary;
        private TextBlock DatasetDashboardActionText => LearningWorkflowPanelControl?.DatasetDashboardAction;
        private ItemsControl DatasetDashboardMetricItemsControl => LearningWorkflowPanelControl?.DatasetDashboardMetricList;
        private ItemsControl DatasetDashboardIssueItemsControl => LearningWorkflowPanelControl?.DatasetDashboardIssueList;
        private TextBlock YoloTrainingHistoryText => LearningWorkflowPanelControl?.YoloTrainingHistory;
        private Button YoloRunModelComparisonButton => LearningWorkflowPanelControl?.YoloRunModelComparison;
        private ItemsControl YoloTrainingRunHistoryItemsControl => LearningWorkflowPanelControl?.YoloTrainingRunHistoryList;
        private Border TemplateWorkflowPanel => LearningWorkflowPanelControl?.TemplateWorkflow;
        private TextBlock TemplateWorkflowTitleText => LearningWorkflowPanelControl?.TemplateWorkflowTitle;
        private TextBlock TemplateWorkflowSummaryText => LearningWorkflowPanelControl?.TemplateWorkflowSummary;
        private ItemsControl TemplateWorkflowItemsControl => LearningWorkflowPanelControl?.TemplateWorkflowStepList;
        private Button TemplateCurrentImageGuideButton => LearningWorkflowPanelControl?.TemplateCurrentImage;
        private Button TemplateBatchGuideButton => LearningWorkflowPanelControl?.TemplateBatch;
        private Button YoloFixClassesButton => LearningWorkflowPanelControl?.YoloFixClasses;
        private Button YoloFixLabelsButton => LearningWorkflowPanelControl?.YoloFixLabels;
        private Button YoloFixDatasetButton => LearningWorkflowPanelControl?.YoloFixDataset;
        private Button TutorialOpenHtmlGuideButton => LearningWorkflowPanelControl?.TutorialOpenHtmlGuide;
        private ComboBox ImageQueueFilterBox => ImageQueuePanelControl?.FilterBox;
        private TextBox ImageQueueSearchBox => ImageQueuePanelControl?.SearchBox;
        private DataGrid ImageQueueGrid => ImageQueuePanelControl?.QueueGrid;
        private TextBlock CurrentImageFolderPathText => ImageQueuePanelControl?.CurrentFolderPathTextBlock;
        private Wpf.Ui.Controls.Button OpenCurrentImageFolderButton => ImageQueuePanelControl?.OpenCurrentFolderButton;
        private Wpf.Ui.Controls.Button OpenSelectedQueueImageButton => ImageQueuePanelControl?.OpenSelectedButton;
        private Wpf.Ui.Controls.Button DetectSelectedQueueButton => ImageQueuePanelControl?.DetectSelectedButton;
        private Wpf.Ui.Controls.Button BatchDetectQueueButton => ImageQueuePanelControl?.BatchDetectButton;
        private Wpf.Ui.Controls.Button TemplateBatchQueueButton => ImageQueuePanelControl?.TemplateBatchButton;
        private Wpf.Ui.Controls.Button RetryFailedQueueButton => ImageQueuePanelControl?.RetryFailedButton;
        private Wpf.Ui.Controls.Button StopBatchQueueButton => ImageQueuePanelControl?.StopBatchButton;
        private Wpf.Ui.Controls.Button QueueFilterUnfinishedButton => ImageQueuePanelControl?.QueueFilterUnfinished;
        private Wpf.Ui.Controls.Button QueueFilterAllButton => ImageQueuePanelControl?.QueueFilterAll;
        private Wpf.Ui.Controls.Button QueueFilterCandidateButton => ImageQueuePanelControl?.QueueFilterCandidate;
        private Wpf.Ui.Controls.Button QueueFilterFailedButton => ImageQueuePanelControl?.QueueFilterFailed;
        private Wpf.Ui.Controls.Button QueueFilterConfirmedButton => ImageQueuePanelControl?.QueueFilterConfirmed;
        private Wpf.Ui.Controls.Button QueueFilterSkippedButton => ImageQueuePanelControl?.QueueFilterSkipped;
        private Wpf.Ui.Controls.Button QueueFilterNoCandidateButton => ImageQueuePanelControl?.QueueFilterNoCandidate;
        private TextBlock QueueFilterUnfinishedText => ImageQueuePanelControl?.QueueFilterUnfinishedTextBlock;
        private TextBlock ImageQueuePanelTitleText => ImageQueuePanelControl?.PanelTitleTextBlock;
        private TextBlock QueueFilterAllText => ImageQueuePanelControl?.QueueFilterAllTextBlock;
        private TextBlock QueueFilterCandidateText => ImageQueuePanelControl?.QueueFilterCandidateTextBlock;
        private TextBlock QueueFilterFailedText => ImageQueuePanelControl?.QueueFilterFailedTextBlock;
        private TextBlock QueueFilterConfirmedText => ImageQueuePanelControl?.QueueFilterConfirmedTextBlock;
        private TextBlock QueueFilterSkippedText => ImageQueuePanelControl?.QueueFilterSkippedTextBlock;
        private TextBlock QueueFilterNoCandidateText => ImageQueuePanelControl?.QueueFilterNoCandidateTextBlock;
        private RoiImageCanvasView MainCanvasView => CanvasPanelControl?.MainCanvas;
        private ListBox CanvasAnnotationToolListBox => CanvasPanelControl?.AnnotationToolList;
        private ListBox CanvasLabelClassListBox => CanvasPanelControl?.LabelClassList;
        private ListBox CanvasDisplayModeListBox => CanvasPanelControl?.DisplayModeList;
        private Border CanvasWorkflowContextStrip => CanvasPanelControl?.WorkflowContextStrip;
        private TextBlock CanvasCurrentStepText => CanvasPanelControl?.CurrentStepText;
        private TextBlock CanvasCurrentToolText => CanvasPanelControl?.CurrentToolText;
        private TextBlock CanvasNextActionText => CanvasPanelControl?.NextActionText;
        private Border CanvasLayerVisibilityStrip => CanvasPanelControl?.LayerVisibilityStrip;
        private TextBlock CanvasLayerModeTitleText => CanvasPanelControl?.LayerModeTitleText;
        private TextBlock CanvasLayerModeDetailText => CanvasPanelControl?.LayerModeDetailText;
        private TextBlock CanvasLabelLayerText => CanvasPanelControl?.LabelLayerText;
        private TextBlock CanvasInferenceLayerText => CanvasPanelControl?.InferenceLayerText;
        private Wpf.Ui.Controls.Button CanvasSaveAnnotationButton => CanvasPanelControl?.SaveAnnotationButton;
        private Wpf.Ui.Controls.Button CanvasCreateSmartMaskButton => CanvasPanelControl?.CreateSmartMaskButton;
        private Wpf.Ui.Controls.Button CanvasCompleteNoObjectButton => CanvasPanelControl?.CompleteNoObjectButton;
        private Border CanvasAnnotationSaveStateCard => CanvasPanelControl?.AnnotationSaveStateCard;
        private TextBlock CanvasAnnotationSaveStatusTitleText => CanvasPanelControl?.AnnotationSaveStatusTitleTextBlock;
        private TextBlock CanvasAnnotationSaveStatusDetailText => CanvasPanelControl?.AnnotationSaveStatusDetailTextBlock;
        private Border CanvasActiveLabelClassCard => CanvasPanelControl?.ActiveLabelClassCard;
        private TextBlock CanvasActiveLabelClassTitleText => CanvasPanelControl?.ActiveLabelClassTitleTextBlock;
        private TextBlock CanvasActiveLabelClassDetailText => CanvasPanelControl?.ActiveLabelClassDetailTextBlock;
        private Wpf.Ui.Controls.Button CanvasOpenClassCatalogButton => CanvasPanelControl?.OpenClassCatalogButton;
        private Wpf.Ui.Controls.Button FitCanvasButton => CanvasPanelControl?.FitButton;
        private Wpf.Ui.Controls.Button ActualSizeCanvasButton => CanvasPanelControl?.ActualSizeButton;
        private Wpf.Ui.Controls.Button PanCanvasButton => CanvasPanelControl?.PanButton;
        private Wpf.Ui.Controls.Button DisplayAdjustmentCanvasButton => CanvasPanelControl?.DisplayAdjustmentButton;
        private System.Windows.Controls.Primitives.Popup DisplayAdjustmentPopup => CanvasPanelControl?.DisplayAdjustmentFlyout;
        private Wpf.Ui.Controls.Button FocusCandidateCanvasButton => CanvasPanelControl?.FocusCandidateButton;
        private Wpf.Ui.Controls.Button ResetAiOverlayCanvasButton => CanvasPanelControl?.ResetAiOverlayButton;
        private Border DetectionResultOverlay => CanvasPanelControl?.ResultOverlay;
        private TextBlock DetectionOverlayTitleText => CanvasPanelControl?.OverlayTitleText;
        private TextBlock DetectionOverlaySummaryText => CanvasPanelControl?.OverlaySummaryText;
        private Border DetectionOverlaySelectedBorder => CanvasPanelControl?.OverlaySelectedBorder;
        private TextBlock DetectionOverlaySelectedText => CanvasPanelControl?.OverlaySelectedText;
        private TextBlock DetectionOverlayDetailText => CanvasPanelControl?.OverlayDetailText;
        private TextBlock ObjectReviewSummaryText => ObjectReviewPanelControl?.SummaryTextBlock;
        private Border ObjectReviewLabelSaveBadge => ObjectReviewPanelControl?.LabelSaveBadge;
        private TextBlock ObjectReviewLabelSaveBadgeText => ObjectReviewPanelControl?.LabelSaveBadgeTextBlock;
        private TextBlock ObjectReviewLabelSaveDetailText => ObjectReviewPanelControl?.LabelSaveDetailTextBlock;
        private Wpf.Ui.Controls.Button DeleteObjectButton => ObjectReviewPanelControl?.DeleteButton;
        private ComboBox ObjectClassBox => ObjectReviewPanelControl?.ClassBox;
        private Wpf.Ui.Controls.Button ApplyObjectClassButton => ObjectReviewPanelControl?.ApplyClassButton;
        private TextBlock MergeSelectionText => ObjectReviewPanelControl?.MergeSelectionTextBlock;
        private Wpf.Ui.Controls.Button MergeSelectedSegmentsButton => ObjectReviewPanelControl?.MergeSegmentsButton;
        private Wpf.Ui.Controls.Button BeginVerticalSplitButton => ObjectReviewPanelControl?.VerticalSplitButton;
        private Wpf.Ui.Controls.Button BeginHorizontalSplitButton => ObjectReviewPanelControl?.HorizontalSplitButton;
        private Wpf.Ui.Controls.Button CancelSplitButton => ObjectReviewPanelControl?.SplitCancelButton;
        private TextBlock SplitStatusText => ObjectReviewPanelControl?.SplitStatusTextBlock;
        private Wpf.Ui.Controls.Button BeginAddHoleButton => ObjectReviewPanelControl?.AddHoleButton;
        private Wpf.Ui.Controls.Button BeginRemoveHoleButton => ObjectReviewPanelControl?.RemoveHoleButton;
        private Wpf.Ui.Controls.Button CancelHoleEditButton => ObjectReviewPanelControl?.HoleEditCancelButton;
        private TextBlock HoleEditStatusText => ObjectReviewPanelControl?.HoleEditStatusTextBlock;
        private Wpf.Ui.Controls.Button BeginInsertVertexButton => ObjectReviewPanelControl?.InsertVertexButton;
        private Wpf.Ui.Controls.Button BeginDeleteVertexButton => ObjectReviewPanelControl?.DeleteVertexButton;
        private Wpf.Ui.Controls.Button CancelVertexEditButton => ObjectReviewPanelControl?.VertexEditCancelButton;
        private TextBlock VertexEditStatusText => ObjectReviewPanelControl?.VertexEditStatusTextBlock;
        private Wpf.Ui.Controls.Button BeginIntelligentScissorsButton => ObjectReviewPanelControl?.IntelligentScissorsBeginButton;
        private Wpf.Ui.Controls.Button ApplyIntelligentScissorsButton => ObjectReviewPanelControl?.IntelligentScissorsApplyButton;
        private Wpf.Ui.Controls.Button CancelIntelligentScissorsButton => ObjectReviewPanelControl?.IntelligentScissorsCancelButton;
        private TextBlock IntelligentScissorsStatusText => ObjectReviewPanelControl?.IntelligentScissorsStatusTextBlock;
        private Wpf.Ui.Controls.Button SendSegmentationToBackButton => ObjectReviewPanelControl?.SegmentationToBackButton;
        private Wpf.Ui.Controls.Button SendSegmentationBackwardButton => ObjectReviewPanelControl?.SegmentationBackwardButton;
        private Wpf.Ui.Controls.Button BringSegmentationForwardButton => ObjectReviewPanelControl?.SegmentationForwardButton;
        private Wpf.Ui.Controls.Button BringSegmentationToFrontButton => ObjectReviewPanelControl?.SegmentationToFrontButton;
        private TextBlock ZOrderStatusText => ObjectReviewPanelControl?.SegmentationZOrderStatusTextBlock;
        private Wpf.Ui.Controls.Button PreviewRemoveUnderlyingButton => ObjectReviewPanelControl?.RemoveUnderlyingPreviewButton;
        private Wpf.Ui.Controls.Button ApplyRemoveUnderlyingButton => ObjectReviewPanelControl?.RemoveUnderlyingApplyButton;
        private Wpf.Ui.Controls.Button CancelRemoveUnderlyingButton => ObjectReviewPanelControl?.RemoveUnderlyingCancelButton;
        private TextBlock RemoveUnderlyingStatusText => ObjectReviewPanelControl?.RemoveUnderlyingStatusTextBlock;
        private Wpf.Ui.Controls.Button ToggleObjectHiddenButton => ObjectReviewPanelControl?.ObjectHiddenButton;
        private Wpf.Ui.Controls.Button ToggleObjectLockedButton => ObjectReviewPanelControl?.ObjectLockedButton;
        private Wpf.Ui.Controls.Button ToggleObjectPinnedButton => ObjectReviewPanelControl?.ObjectPinnedButton;
        private TextBlock ObjectSessionStateStatusText => ObjectReviewPanelControl?.ObjectSessionStateStatusTextBlock;
        private Expander ObjectMetadataExpander => ObjectReviewPanelControl?.ObjectMetadataEditor;
        private Wpf.Ui.Controls.Button TogglePersistentOccludedButton => ObjectReviewPanelControl?.PersistentOccludedButton;
        private ComboBox ObjectMetadataTagBox => ObjectReviewPanelControl?.MetadataTagBox;
        private Wpf.Ui.Controls.Button TogglePersistentTagButton => ObjectReviewPanelControl?.PersistentTagButton;
        private Wpf.Ui.Controls.Button ToggleOccludedFilterButton => ObjectReviewPanelControl?.OccludedFilterButton;
        private ComboBox ObjectMetadataTagFilterBox => ObjectReviewPanelControl?.MetadataTagFilterBox;
        private Wpf.Ui.Controls.Button ResetObjectMetadataFilterButton => ObjectReviewPanelControl?.MetadataFilterResetButton;
        private Wpf.Ui.Controls.Button ResetRecipeMetadataTagsButton => ObjectReviewPanelControl?.RecipeMetadataTagsResetButton;
        private Wpf.Ui.Controls.Button BeginObjectGroupSelectionButton => ObjectReviewPanelControl?.GroupSelectionBeginButton;
        private Wpf.Ui.Controls.Button CreateObjectGroupButton => ObjectReviewPanelControl?.GroupCreateButton;
        private Wpf.Ui.Controls.Button CancelObjectGroupSelectionButton => ObjectReviewPanelControl?.GroupSelectionCancelButton;
        private TextBlock ObjectGroupSelectionStatusText => ObjectReviewPanelControl?.GroupSelectionStatusTextBlock;
        private TextBlock SelectedObjectGroupText => ObjectReviewPanelControl?.SelectedGroupTextBlock;
        private Wpf.Ui.Controls.Button RemoveObjectFromGroupButton => ObjectReviewPanelControl?.GroupMemberRemoveButton;
        private Wpf.Ui.Controls.Button DissolveObjectGroupButton => ObjectReviewPanelControl?.GroupDissolveButton;
        private Wpf.Ui.Controls.Button ToggleObjectGroupOccludedButton => ObjectReviewPanelControl?.GroupOccludedButton;
        private Wpf.Ui.Controls.Button ToggleObjectGroupTagButton => ObjectReviewPanelControl?.GroupTagButton;
        private ComboBox ObjectGroupFilterBox => ObjectReviewPanelControl?.GroupFilterBox;
        private Expander ObjectQualityReviewExpander => ObjectReviewPanelControl?.QualityReviewEditor;
        private Expander SegmentationAdvancedEditExpander => ObjectReviewPanelControl?.SegmentationAdvancedEditor;
        private ListBox ObjectListBox => ObjectReviewPanelControl?.ObjectList;
        private Slider CandidateConfidenceSlider => CandidateReviewPanelControl?.ConfidenceSlider;
        private Grid CandidateReviewRoleSplitPanel => CandidateReviewPanelControl?.RoleSplitPanel;
        private Border CurrentImageCandidateRoleCard => CandidateReviewPanelControl?.CurrentImageRoleCard;
        private Border ModelValidationRoleCard => CandidateReviewPanelControl?.ModelValidationCard;
        private TextBlock CurrentImageReviewRoleTitleText => CandidateReviewPanelControl?.CurrentImageRoleTitle;
        private TextBlock CurrentImageReviewRoleDetailText => CandidateReviewPanelControl?.CurrentImageRoleDetail;
        private TextBlock CurrentImageReviewRoleResultText => CandidateReviewPanelControl?.CurrentImageRoleResult;
        private TextBlock ModelValidationRoleTitleText => CandidateReviewPanelControl?.ModelValidationRoleTitle;
        private TextBlock ModelValidationRoleDetailText => CandidateReviewPanelControl?.ModelValidationRoleDetail;
        private TextBlock ModelValidationRoleResultText => CandidateReviewPanelControl?.ModelValidationRoleResult;
        private TextBlock CandidateConfidenceText => CandidateReviewPanelControl?.ConfidenceTextBlock;
        private TextBlock CandidateDetailText => CandidateReviewPanelControl?.DetailTextBlock;
        private Border ModelCandidateDecisionPanel => CandidateReviewPanelControl?.ModelCandidateDecision;
        private TextBlock ModelCandidateDecisionStatusText => CandidateReviewPanelControl?.ModelCandidateDecisionStatus;
        private TextBlock ModelCandidateDecisionDetailText => CandidateReviewPanelControl?.ModelCandidateDecisionDetail;
        private Border SelectedCandidateSummaryPanel => CandidateReviewPanelControl?.SelectedCandidateSummary;
        private TextBlock SelectedCandidateSummaryText => CandidateReviewPanelControl?.SelectedCandidateSummaryTextBlock;
        private Border CandidateComparisonPanel => CandidateReviewPanelControl?.ComparisonPanel;
        private TextBlock CandidateCompareCandidateText => CandidateReviewPanelControl?.CompareCandidateText;
        private TextBlock CandidateCompareCurrentText => CandidateReviewPanelControl?.CompareCurrentText;
        private TextBlock CandidateCompareOverlapText => CandidateReviewPanelControl?.CompareOverlapText;
        private TextBlock CandidateCompareDecisionText => CandidateReviewPanelControl?.CompareDecisionText;
        private Wpf.Ui.Controls.Button ConfirmSelectedCandidateButton => CandidateReviewPanelControl?.ConfirmSelectedButton;
        private Wpf.Ui.Controls.Button ConfirmAllCandidatesButton => CandidateReviewPanelControl?.ConfirmAllButton;
        private Wpf.Ui.Controls.Button SkipSelectedCandidateButton => CandidateReviewPanelControl?.SkipSelectedButton;
        private Wpf.Ui.Controls.Button CompleteImageAndNextButton => CandidateReviewPanelControl?.CompleteImageAndNext;
        private Wpf.Ui.Controls.Button SaveModelCandidateButton => CandidateReviewPanelControl?.SaveModelCandidate;
        private Wpf.Ui.Controls.Button RejectModelCandidateButton => CandidateReviewPanelControl?.RejectModelCandidate;
        private Wpf.Ui.Controls.Button PreviousCandidateButton => CandidateReviewPanelControl?.PreviousCandidate;
        private Wpf.Ui.Controls.Button NextCandidateButton => CandidateReviewPanelControl?.NextCandidate;
        private Wpf.Ui.Controls.Button FocusCandidateButton => CandidateReviewPanelControl?.FocusCandidate;
        private Wpf.Ui.Controls.Button FocusCurrentLabelButton => CandidateReviewPanelControl?.FocusCurrentLabel;
        private ListBox CandidateListBox => CandidateReviewPanelControl?.CandidateList;
        private Border ClassCatalogGuidePanel => ClassCatalogPanelControl?.GuidePanel;
        private TextBlock ClassCatalogGuideTitleText => ClassCatalogPanelControl?.GuideTitleTextBlock;
        private TextBlock ClassCatalogGuideDetailText => ClassCatalogPanelControl?.GuideDetailTextBlock;
        private TextBlock ClassCatalogSummaryText => ClassCatalogPanelControl?.SummaryTextBlock;
        private TextBlock CurrentDrawingClassTitleText => ClassCatalogPanelControl?.CurrentDrawingClassTitleTextBlock;
        private TextBlock CurrentDrawingClassDetailText => ClassCatalogPanelControl?.CurrentDrawingClassDetailTextBlock;
        private TextBlock ClassCatalogActionText => ClassCatalogPanelControl?.ActionTextBlock;
        private TextBlock ClassSectionLabelText => ClassCatalogPanelControl?.ClassSectionLabelTextBlock;
        private TextBox ClassNameBox => ClassCatalogPanelControl?.ClassNameTextBox;
        private Wpf.Ui.Controls.Button AddClassButton => ClassCatalogPanelControl?.AddClass;
        private Wpf.Ui.Controls.Button RenameClassButton => ClassCatalogPanelControl?.RenameClass;
        private Wpf.Ui.Controls.Button RemoveClassButton => ClassCatalogPanelControl?.RemoveClass;
        private ListBox ClassColorBox => ClassCatalogPanelControl?.ClassColor;
        private Expander ClassColorAdvancedPanel => ClassCatalogPanelControl?.ClassColorAdvanced;
        private Wpf.Ui.Controls.Button ApplyClassColorButton => ClassCatalogPanelControl?.ApplyClassColor;
        private TextBlock ClassEditStatusText => ClassCatalogPanelControl?.StatusTextBlock;
        private ListBox ClassListBox => ClassCatalogPanelControl?.ClassList;
        private TextBlock YoloSettingsSummaryText => YoloStatusPanelControl?.SummaryTextBlock;
        private Expander YoloRuntimeDetailsExpander => YoloStatusPanelControl?.RuntimeDetailsExpander;
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
        private TextBox ProjectManifestPathBox => ProjectConfigPanelControl?.ManifestPathBox;
        private TextBox ProjectDatasetVersionBox => ProjectConfigPanelControl?.DatasetVersionBox;
        private TextBox ProjectDatasetVersionDetailBox => ProjectConfigPanelControl?.DatasetVersionDetailBox;
        private TextBlock ProjectConfigStatusText => ProjectConfigPanelControl?.StatusTextBlock;
        private Wpf.Ui.Controls.Button ApplyProjectRecipeButton => ProjectConfigPanelControl?.ApplyRecipeButton;
        private Wpf.Ui.Controls.Button RefreshProjectRecipeListButton => ProjectConfigPanelControl?.RefreshRecipeListButton;
        private Wpf.Ui.Controls.Button SaveProjectConfigButton => ProjectConfigPanelControl?.SaveButton;
        private Wpf.Ui.Controls.Button OpenProjectConfigFolderButton => ProjectConfigPanelControl?.OpenFolderButton;
        private Border YoloInspectionModelQuickPanel => YoloModelSettingsPanelControl?.InspectionModelQuickPanel;
        private TextBox YoloPythonPathBox => YoloModelSettingsPanelControl?.PythonPathBox;
        private ComboBox YoloModelEngineBox => YoloModelSettingsPanelControl?.ModelEngineBox;
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
        private Wpf.Ui.Controls.Button YoloRuntimeInstallPackageButton => YoloModelSettingsPanelControl?.RuntimeInstallPackageButton;
        private Wpf.Ui.Controls.Button YoloRuntimeUninstallPackageButton => YoloModelSettingsPanelControl?.RuntimeUninstallPackageButton;
        private Expander TrainingSettingsExpander => TrainingSettingsPanelControl?.SettingsExpander;
        private Border PostTrainingModelActionPanel => TrainingSettingsPanelControl?.PostTrainingActionPanel;
        private TextBlock PostTrainingModelStatusText => TrainingSettingsPanelControl?.PostTrainingModelStatus;
        private TextBlock PostTrainingModelDetailText => TrainingSettingsPanelControl?.PostTrainingModelDetail;
        private Wpf.Ui.Controls.Button ReviewTrainedModelButton => TrainingSettingsPanelControl?.ReviewTrainedModel;
        private Wpf.Ui.Controls.Button ConfirmTrainedModelButton => TrainingSettingsPanelControl?.ConfirmTrainedModel;
        private Wpf.Ui.Controls.Button RunYoloEngineComparisonButton => TrainingSettingsPanelControl?.RunYoloEngineComparison;
        private Border SegmentationAdapterComparisonPanel => TrainingSettingsPanelControl?.SegmentationAdapterComparison;
        private TextBox SegmentationUnetCheckpointPathBox => TrainingSettingsPanelControl?.SegmentationUnetCheckpointPath;
        private TextBox SegmentationYoloCheckpointPathBox => TrainingSettingsPanelControl?.SegmentationYoloCheckpointPath;
        private Wpf.Ui.Controls.Button BrowseSegmentationUnetCheckpointButton => TrainingSettingsPanelControl?.BrowseSegmentationUnetCheckpoint;
        private Wpf.Ui.Controls.Button BrowseSegmentationYoloCheckpointButton => TrainingSettingsPanelControl?.BrowseSegmentationYoloCheckpoint;
        private Wpf.Ui.Controls.Button RunSegmentationAdapterComparisonButton => TrainingSettingsPanelControl?.RunSegmentationAdapterComparison;
        private TextBox TrainingImageSizeBox => TrainingSettingsPanelControl?.ImageSizeBox;
        private TextBox TrainingBatchBox => TrainingSettingsPanelControl?.BatchBox;
        private TextBox TrainingEpochBox => TrainingSettingsPanelControl?.EpochBox;
        private ComboBox TrainingCfgBox => TrainingSettingsPanelControl?.CfgBox;
        private ComboBox TrainingWeightBox => TrainingSettingsPanelControl?.WeightBox;
        private TextBox TrainingValidationPercentBox => TrainingSettingsPanelControl?.ValidationPercentBox;
        private TextBox TrainingTestPercentBox => TrainingSettingsPanelControl?.TestPercentBox;
        private TextBox TrainingSplitSeedBox => TrainingSettingsPanelControl?.SplitSeedBox;
        private TextBlock TrainingSplitPolicyHintText => TrainingSettingsPanelControl?.SplitPolicyHintTextBlock;
        private Wpf.Ui.Controls.Button ApplyFastTrainingPresetButton => TrainingSettingsPanelControl?.ApplyFastPresetButton;
        private Wpf.Ui.Controls.Button RefreshTrainingReadinessButton => TrainingSettingsPanelControl?.RefreshReadinessButton;
        private Wpf.Ui.Controls.Button StartTrainingButton => TrainingSettingsPanelControl?.StartButton;
        private Wpf.Ui.Controls.Button StopTrainingButton => TrainingSettingsPanelControl?.StopButton;
        private TextBlock DatasetStatusText => StatusBarPanelControl?.DatasetStatusTextBlock;
        private TextBlock WorkflowStageText => StatusBarPanelControl?.WorkflowStageTextBlock;
        private TextBlock WorkflowProgressText => StatusBarPanelControl?.WorkflowProgressTextBlock;
        private TextBlock WorkflowNextActionText => StatusBarPanelControl?.WorkflowNextActionTextBlock;
        private TextBlock PythonStatusText => StatusBarPanelControl?.PythonStatusTextBlock;
        private TextBlock AnnotationSaveStatusText => StatusBarPanelControl?.AnnotationSaveStatusTextBlock;
        private TextBlock InspectionModelStatusText => StatusBarPanelControl?.InspectionModelStatusTextBlock;
        private TextBlock ModelStatusText => StatusBarPanelControl?.ModelStatusTextBlock;
        private FrameworkElement ShellLogPanel => ShellLogPanelControl?.LogPanel;
        #endregion

        #region PanelWiring
        private void ComposePanelViewModels()
        {
            // Keep ViewModels out of UserControls; the shell composes data contexts so each View can be constructed standalone.
            LearningWorkflowPanelControl.DataContext = LearningWorkflowViewModel;
            ImageQueuePanelControl.DataContext = ImageQueueViewModel;
            CanvasPanelControl.DataContext = CanvasPanelViewModel;
            ObjectReviewPanelControl.DataContext = ObjectReviewViewModel;
            ClassCatalogPanelControl.DataContext = ClassCatalogViewModel;
            ProjectConfigPanelControl.DataContext = ProjectConfigViewModel;
            StatusBarPanelControl.DataContext = StatusBarViewModel;
            ShellLogPanelControl.DataContext = ShellLogViewModel;
            EnsureModelWorkflowPanelsComposed();
        }

        private void EnsureModelWorkflowPanelsComposed()
        {
            if (modelWorkflowPanelsComposed)
            {
                return;
            }

            modelWorkflowPanelsComposed = true;
            CandidateReviewPanelControl.DataContext = viewModels.ModelWorkflowViewModels.CandidateReviewViewModel;
            YoloStatusPanelControl.DataContext = viewModels.ModelWorkflowViewModels.YoloStatusViewModel;
            YoloModelSettingsPanelControl.DataContext = viewModels.ModelWorkflowViewModels.YoloModelSettingsViewModel;
            TrainingSettingsPanelControl.DataContext = viewModels.ModelWorkflowViewModels.TrainingSettingsViewModel;

            InitializeYoloEditorPanel();
            ConfigureCandidateReviewPanelCommands();
            ConfigureYoloStatusPanelCommands();
            ConfigureYoloModelSettingsPanelCommands();
            ConfigureTrainingSettingsPanelCommands();
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
            MainCanvasViewModel.ShouldDrawOverExistingRoi = ShouldDrawOverExistingRoiForCurrentClass;
        }

        private void SeedImageQueueInputCommands()
        {
            // The shell is the composition root; seed behavior commands so pre-Loaded queue selection uses the same path as real clicks.
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueFilterBox, ImageQueueViewModel.FilterSelectionChangedCommand);
            InputCommandBehaviors.SetTextInputCommand(ImageQueueSearchBox, ImageQueueViewModel.SearchTextChangedCommand);
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueGrid, ImageQueueViewModel.QueueSelectionChangedCommand);
            InputCommandBehaviors.SetMouseDoubleClickInputCommand(ImageQueueGrid, ImageQueueViewModel.QueueMouseDoubleClickCommand);
        }


        private void InitializeYoloEditorPanel()
        {
            if (!modelWorkflowPanelsComposed)
            {
                return;
            }

            EnsureProjectSettings();
            PopulateProjectConfigPanelFields();
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            projectSettingsEditorAdapter.RefreshCandidateConfidenceFilterFromAppliedSettings();
        }

        private void PopulateYoloEditorFields()
            => projectSettingsEditorAdapter.PopulateYoloEditorFields();

        private void PopulateTrainingEditorFields()
            => projectSettingsEditorAdapter.PopulateTrainingEditorFields();

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
                TemplateMatchingAutoLabelViewModel.RunCurrentImage,
                ExecuteChangeDatasetCommand,
                ExecuteOpenDatasetRootFolderCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteLoadedCommand,
                ExecuteClosedCommand,
                ExecuteShellPreviewKeyDownCommand,
                ExecuteDatasetHomeCommand,
                ExecuteLabelingWorkbenchCommand,
                ExecuteInferenceReviewCommand,
                ExecuteTrainingModelCenterCommand,
                ExecuteReviewCandidateModelCommand,
                ShowSavedLabelsWorkflowView,
                FocusCurrentStageGuideToolsTab,
                FocusClassCatalogTab,
                ExecutePromoteSelectedModelHistoryCommand,
                null,
                null,
                null,
                ExecuteOpenModelBenchmarkCommand,
                datasetTransferWindowHost.ShowDatasetHealth,
                datasetTransferWindowHost.ShowDatasetInterchange);
            ShellViewModel.ConfigureAnomalyEvaluationWorkflow(
                anomalyClassificationEvaluationWorkflowService,
                CreateAnomalyEvaluationCallbacks);
            RefreshAttachedCommandBindings(
                this,
                WindowLifecycleCommandBehavior.LoadedCommandProperty,
                WindowLifecycleCommandBehavior.ClosedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
            // XAML bindings can keep the same command reference while the shell
            // replaces its configured delegates. Reattach the lifecycle handlers
            // so the first Loaded/Closed event cannot be missed.
            WindowLifecycleCommandBehavior.RefreshLoadedCommand(this);
            WindowLifecycleCommandBehavior.RefreshClosedCommand(this);
            RefreshAttachedCommandBindings(DatasetHomeStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(LabelingWorkbenchStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(InferenceReviewStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(TrainingModelStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(WorkflowStageReviewCandidateModelButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(WorkflowStageSaveModelSettingsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(WorkflowStageInspectCurrentImageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowDockToggleButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowDatasetHomeButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowSavedLabelsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowGuideToolsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowClassCatalogButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowInferenceCandidatesButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowInferenceInspectButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowTrainingModelButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowTrainingReviewCandidateButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowTrainingInspectButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailOpenButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailSavedLabelsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailGuideToolsButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(RightWorkflowRailClassCatalogButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
        }
        #endregion

    }
}
