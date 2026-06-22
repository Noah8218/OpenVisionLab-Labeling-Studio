using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Model;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using CvScalar = OpenCvSharp.Scalar;

namespace LabelingApplication.Tests;

internal static class Program
{
    private const int WmClose = 0x0010;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopMost = new IntPtr(-1);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--wpf-visual-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfVisualSmoke(args);
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-queue-click-perf", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfQueueClickPerformanceSmoke(args);
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-labeling-session-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfLabelingSessionSmoke(args);
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-yolo-training-session-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfYoloTrainingSessionSmoke(args);
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-roi-object-verification", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfRoiObjectVerification();
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-segmentation-object-verification", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfSegmentationObjectVerification();
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-annotation-object-verification", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWpfAnnotationObjectVerification();
        }

        if (args.Any(arg => string.Equals(arg, "--roi-500k-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI 500K object single-move performance", TestRoi500KObjectSingleMovePerformance);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-500k-mouse-event-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI 500K mouse-event move/resize performance", TestRoi500KMouseEventMovePerformance);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-500k-delete-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI 500K single-delete performance", TestOpenGlRoiDeleteUsesIncrementalPathAt500KObjects);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-500k-hit-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI 500K spatial hit-test performance", TestOpenGlRoiHitTestUsesSpatialIndexAt500KObjects);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-500k-render", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI 500K viewport render culling performance", TestOpenGlRoiRenderingUsesSpatialIndexAt500KObjects);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-500k-full-viewport-render", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI 500K full-viewport render LOD performance", TestOpenGlRoiFullViewportRenderingUsesLodAt500KObjects);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-large-spatial-index", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI large-object spatial index uses coarse buckets", TestOpenGlRoiLargeObjectsUseCoarseSpatialIndex);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-overlap-hit-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI dense-overlap hit-test performance", TestOpenGlRoiDenseOverlapHitTestStaysInteractive);
        }

        if (args.Any(arg => string.Equals(arg, "--detection-500k-hit-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Detection overlay 500K spatial hit-test performance", TestDetectionOverlayHitTestUsesSpatialIndexAt500KCandidates);
        }

        if (args.Any(arg => string.Equals(arg, "--detection-500k-render", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Detection overlay 500K viewport render culling performance", TestDetectionOverlayRenderingUsesSpatialIndexAt500KCandidates);
        }

        if (args.Any(arg => string.Equals(arg, "--segmentation-overlay-render", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Segmentation overlay viewport render culling performance", TestSegmentationOverlayRenderingUsesSpatialIndex);
        }

        if (args.Any(arg => string.Equals(arg, "--texture-pan-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Texture pan MouseMove fast-path performance", TestTexturePanMouseMoveUsesFastPath);
        }

        if (args.Any(arg => string.Equals(arg, "--hover-mousemove-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Hover MouseMove status and ROI hit-test performance", TestHoverMouseMoveUsesSpatialIndexAndThrottledStatusAt500KObjects);
        }

        if (args.Any(arg => string.Equals(arg, "--brush-hover-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Brush hover MouseMove preview performance", TestBrushHoverMouseMoveStaysThrottledAt500KObjects);
        }

        if (args.Any(arg => string.Equals(arg, "--wpf-mask-drag-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("WPF mask brush drag commit performance", TestWpfMaskBrushDragCommitsHistoryOnce);
        }

        if (args.Any(arg => string.Equals(arg, "--mask-move-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("WPF raster mask move avoids full-image allocation", TestWpfRasterMaskMoveAvoidsFullImageAllocation);
        }

        if (args.Any(arg => string.Equals(arg, "--roi-drawing-preview-performance", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("ROI drawing preview MouseMove fast-path performance", TestRoiDrawingPreviewMouseMoveUsesLiveShape);
        }

        if (args.Any(arg => string.Equals(arg, "--real-yolo-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSingleSmoke("Real YOLO TCP workflow detects, overlays, confirms, and saves labels", TestRealYoloDetectionWorkflowSmoke);
        }

        var tests = new (string Name, Action Test)[]
        {
            ("YOLO path normalization uses forward slashes", TestNormalizeYamlPath),
            ("Class catalog normalizes names and rejects duplicates", TestClassCatalogService),
            ("WPF object review edit service applies and deletes objects", TestWpfObjectReviewEditService),
            ("CData creates YOLO dataset directories and data.yaml", TestCreateYoloDataset),
            ("YOLO dataset split service assigns train valid and test exclusively", TestYoloDatasetSplitService),
            ("YOLO dataset validator rejects invalid training configuration", TestYoloDatasetValidatorConfiguration),
            ("YOLO dataset validator accepts saved training files", TestYoloDatasetValidatorTrainingFiles),
            ("YOLO dataset validator rejects duplicate train and valid images", TestYoloDatasetValidatorRejectsTrainValidDuplicates),
            ("YOLO dataset validator rejects invalid label file contents", TestYoloDatasetValidatorInvalidLabels),
            ("YOLO dataset validator reports saved dataset statistics", TestYoloDatasetStatistics),
            ("YOLO dataset readiness report combines validation and statistics", TestYoloDatasetReadinessReport),
            ("YOLO dataset diagnostics report operator-ready issues", TestYoloDatasetDiagnosticsReport),
            ("YOLO annotation lines use normalized coordinates", TestYoloAnnotationLines),
            ("YOLO annotation service loads saved label rectangles", TestYoloAnnotationLoad),
            ("YOLO image label status reports saved object counts", TestYoloImageLabelStatusService),
            ("YOLO image review status tracks labels, candidates, and next unlabeled image", TestYoloImageReviewStatusService),
            ("YOLO annotation service writes image and label files", TestYoloAnnotationFileWrite),
            ("Segmentation annotation service writes masks and polygons", TestSegmentationAnnotationFileWrite),
            ("Segmentation geometry converts boxes into editable polygons", TestSegmentationGeometryBoxConversion),
            ("WPF polygon annotation service creates pixel segmentation objects", TestWpfPolygonAnnotationService),
            ("WPF shell polygon tool creates reviewable segmentation objects", TestWpfPolygonShellInputCreatesSegmentation),
            ("WPF mask annotation service paints and erases raster masks", TestWpfMaskAnnotationService),
            ("WPF shell brush and eraser create reviewable raster masks", TestWpfBrushEraserShellInputCreatesMaskSegmentation),
            ("WPF segmentation object manipulation verification matrix passes", TestWpfSegmentationObjectManipulationVerificationMatrix),
            ("WPF segmentation object manipulation updates shell state", TestWpfSegmentationObjectManipulationUpdatesShellState),
            ("WPF annotation history service clones editable state", TestWpfAnnotationHistoryService),
            ("WPF shell undo and redo restore ROI and mask edits", TestWpfShellUndoRedoRestoresAnnotationState),
            ("Training parameter alias preserves legacy configuration", TestTrainingParamAlias),
            ("Training settings mirror legacy training parameters", TestTrainingSettingsMirror),
            ("Learning protocol builds normalized training packet", TestLearningProtocol),
            ("Learning communication can be created without opening TCP listener", TestLearningCommunicationDeferredStart),
            ("Learning communication closes safely while accept callback is pending", TestLearningCommunicationCloseDuringAccept),
            ("Learning communication can restart listener after close", TestLearningCommunicationRestartAfterClose),
            ("Learning communication sends StartDefect and applies TCP ResultDefect", TestLearningCommunicationDetectionRoundTrip),
            ("Python model settings default to YOLOv5 client paths", TestPythonModelSettingsDefaults),
            ("Python model settings repair stale roots", TestPythonModelSettingsRepairsStaleRoots),
            ("Runtime example config uses portable sibling YOLO paths", TestRuntimeExampleConfigUsesPortableSiblingYoloPaths),
            ("Python model settings validator reports missing weights", TestPythonModelSettingsValidator),
            ("Python environment service parses requirements files", TestPythonEnvironmentRequirementsParser),
            ("YOLO worker smoke service validates inputs", TestYoloWorkerSmokeTestServiceValidation),
            ("YOLO worker smoke candidates expose label bounds", TestYoloWorkerSmokeCandidateBounds),
            ("YOLOv5 Python client start info validates script path", TestYoloPythonClientStartInfo),
            ("YOLO detection workflow validates settings before sending", TestYoloDetectionWorkflowValidation),
            ("Python message framer rebuilds split TCP messages", TestPythonMessageFramer),
            ("Python model status protocol parses training progress", TestPythonModelStatusProtocol),
            ("TCP receive queue decodes configured UTF-8 strings", TestTcpReceiveQueueEncoding),
            ("Python ResultDefect protocol parses detection boxes", TestPythonDetectionResultProtocol),
            ("Python ResultDefect protocol reports invalid payloads", TestPythonDetectionResultProtocolFailures),
            ("OpenVisionLab ImageSpace tracks active labeling image", TestLabelingImageWorkspace),
            ("OpenVisionLab log adapter accepts formatted messages", TestAppLog),
            ("Screen capture path is sanitized and created without opening UI", TestScreenCapturePath),
            ("Image list uses supported image file filtering", TestImageListSupportedFiles),
            ("WPF class catalog uses labeling terminology", TestWpfClassCatalogUiText),
            ("WPF YOLO settings use labeling terminology", TestWpfYoloSettingsUiText),
            ("ROI geometry clamps dragged rectangles to image bounds", TestRoiGeometry),
            ("ROI geometry suppresses extended-rectangle callbacks during drag", TestRoiGeometrySuppressesExtendedCallbacksDuringDrag),
            ("OpenGL image geometry maps image coordinates consistently", TestOpenGlImageGeometry),
            ("OpenGL detection overlay screen bounds stay anchored to image pixels", TestOpenGlDetectionOverlayScreenBounds),
            ("OpenGL refresh marshals to the child control thread", TestOpenGlRefreshThreadMarshal),
            ("OpenGL view math guards zero-size resize states", TestOpenGlViewMathGuardsZeroSizeResizeStates),
            ("OpenGL canvas exposes actual-size zoom", TestOpenGlCanvasExposesActualSizeZoom),
            ("OpenGL mouse pan avoids per-event pixel readback", TestOpenGlMousePanAvoidsPerEventPixelReadback),
            ("OpenGL texture pan MouseMove uses fast path", TestTexturePanMouseMoveUsesFastPath),
            ("OpenGL hover MouseMove uses spatial index and throttled status at 500K objects", TestHoverMouseMoveUsesSpatialIndexAndThrottledStatusAt500KObjects),
            ("OpenGL brush hover MouseMove preview is throttled at 500K objects", TestBrushHoverMouseMoveStaysThrottledAt500KObjects),
            ("WPF mask brush drag commits history and side lists once", TestWpfMaskBrushDragCommitsHistoryOnce),
            ("OpenGL ROI drawing preview MouseMove uses live shape", TestRoiDrawingPreviewMouseMoveUsesLiveShape),
            ("OpenGL ROI mouse-event move/resize stays incremental at 500K objects", TestRoi500KMouseEventMovePerformance),
            ("OpenGL ROI delete stays incremental at 500K objects", TestOpenGlRoiDeleteUsesIncrementalPathAt500KObjects),
            ("OpenGL ROI hit test uses spatial index at 500K objects", TestOpenGlRoiHitTestUsesSpatialIndexAt500KObjects),
            ("OpenGL ROI rendering culls 500K objects by viewport", TestOpenGlRoiRenderingUsesSpatialIndexAt500KObjects),
            ("OpenGL ROI full-viewport rendering uses LOD at 500K objects", TestOpenGlRoiFullViewportRenderingUsesLodAt500KObjects),
            ("OpenGL ROI large-object spatial index uses coarse buckets", TestOpenGlRoiLargeObjectsUseCoarseSpatialIndex),
            ("OpenGL ROI dense-overlap hit-test stays interactive", TestOpenGlRoiDenseOverlapHitTestStaysInteractive),
            ("OpenGL detection overlay hit test uses spatial index at 500K candidates", TestDetectionOverlayHitTestUsesSpatialIndexAt500KCandidates),
            ("OpenGL detection overlay rendering culls 500K candidates by viewport", TestDetectionOverlayRenderingUsesSpatialIndexAt500KCandidates),
            ("OpenGL segmentation overlay rendering culls by viewport", TestSegmentationOverlayRenderingUsesSpatialIndex),
            ("ROI interaction selects and removes rectangles", TestRoiInteraction),
            ("WPF annotation object verification covers rectangle and ellipse hit behavior", TestWpfAnnotationObjectVerificationProcess),
            ("WPF ROI object manipulation verification matrix passes", TestWpfRoiObjectManipulationVerificationMatrix),
            ("WPF ROI object manipulation updates shell state", TestWpfRoiObjectManipulationUpdatesShellState),
            ("CViewer stores ROI rectangles without opening UI", TestCViewerRoiApi),
            ("CViewer stores segmentation polygons without opening UI", TestCViewerSegmentationApi),
            ("CViewer saves default Defect segmentation labels", TestCViewerDefaultDefectSegmentationSave),
            ("CViewer converts display images to OpenGL texture format", TestCViewerImageCopy),
            ("Canvas image loader owns decoded Mat buffers", TestCanvasImageLoaderOwnsDecodedMatBuffers),
            ("Canvas image loader avoids texture upload clones for continuous Mats", TestCanvasImageLoaderUsesContinuousUploadMatWithoutClone),
            ("CViewer main image load preserves active image metadata", TestCViewerMainImageWorkspace),
            ("CViewer WinForms host adapter replaces and disposes previous OpenGL canvas", TestCViewerWinFormsHostAdapterLifecycle),
            ("WPF canvas panel hosts ROI view without WinForms bridge", TestWpfCanvasPanelHostsRoiViewWithoutWinFormsBridge),
            ("WPF canvas panel declares viewer commands", TestWpfCanvasPanelDeclaresViewerCommands),
            ("WPF learning workflow panel declares education modes and annotation tools", TestWpfLearningWorkflowPanelDeclaresEducationModesAndTools),
            ("WPF annotation tool verification matrix matches actual viewer paths", TestWpfAnnotationToolVerificationMatrix),
            ("WPF ellipse annotation stores pixel bounds for YOLO labels", TestWpfEllipseAnnotationStoresPixelBounds),
            ("WPF tutorial sample flow executes visible side effects", TestWpfTutorialSampleFlowSideEffects),
            ("WPF 10-minute labeling session covers labels, save, and candidate review", TestWpfTenMinuteLabelingSessionFlow),
            ("WPF YOLO training session covers readiness, worker status, and best.pt apply", TestWpfYoloTrainingSessionFlow),
            ("Program starts the WPF shell without legacy FormMainFrame fallback", TestProgramDefaultsToWpfShell),
            ("WPF labeling shell can be constructed without the WinForms shell", TestWpfLabelingShellWindowConstructs),
            ("Legacy settings buttons route to the WPF shell", TestLegacySettingsButtonsRouteToWpfShell),
            ("WPF migration removes legacy WinForms support libraries", TestWpfMigrationRemovesLegacyWinFormsSupportLibraries),
            ("WPF YOLO status panel declares command controls", TestWpfYoloStatusPanelDeclaresCommandControls),
            ("WPF project config panel declares recipe controls", TestWpfProjectConfigPanelDeclaresRecipeControls),
            ("WPF YOLO model settings panel declares path editors", TestWpfYoloModelSettingsPanelDeclaresPathEditors),
            ("WPF training settings panel declares controls", TestWpfTrainingSettingsPanelDeclaresControls),
            ("WPF settings view models round-trip editor values", TestWpfSettingsViewModelsRoundTrip),
            ("WPF status and log panels declare controls", TestWpfStatusAndLogPanelsDeclareControls),
            ("WPF canvas detection overlay uses theme resources", TestWpfCanvasDetectionOverlayUsesThemeResources),
            ("WPF numeric editors declare input guards", TestWpfNumericEditorsDeclareInputGuards),
            ("WPF training status summaries are operator-readable", TestWpfTrainingStatusSummaries),
            ("WPF training command disables conflicting actions", TestWpfTrainingCommandButtonState),
            ("WPF workflow command state service gates commands", TestWpfWorkflowCommandStateService),
            ("WPF single detection avoids startup warm-up and keeps short interactive wait", TestWpfSingleDetectionManualStartupPath),
            ("WPF workflow mode separates labeling and inference", TestWpfWorkflowModeSeparatesLabelingAndInference),
            ("WPF candidate rows show visual review status", TestWpfCandidateRowsShowVisualStatus),
            ("WPF candidate review supports navigation and focus commands", TestWpfCandidateReviewPanelDeclaresNavigation),
            ("WPF class catalog panel declares class edit controls", TestWpfClassCatalogPanelDeclaresClassEditControls),
            ("WPF object review summarizes current labels", TestWpfObjectReviewSummarizesLabels),
            ("WPF image queue loads supported image files", TestWpfImageQueueLoadsSupportedFiles),
            ("WPF image queue detail loader reads size without shell logic", TestWpfImageQueueDetailLoader),
            ("WPF image load replaces previous viewer textures", TestWpfImageLoadReplacesPreviousViewerTextures),
            ("WPF startup image load does not scan every queue image", TestWpfStartupImageLoadDoesNotScanEveryQueueImage),
            ("WPF image queue click uses the lightweight load path", TestWpfImageQueueClickUsesLightweightLoadPath),
            ("WPF image queue preloads adjacent image decodes", TestWpfImageQueuePreloadsAdjacentDecodes),
            ("WPF image queue click loads canvas", TestWpfImageQueueClickLoadsCanvas),
            ("WPF detection candidates render as detection overlays", TestWpfDetectionCandidatesRenderAsDetectionOverlays),
            ("WPF batch detection result displays on canvas", TestWpfBatchDetectionResultDisplaysOnCanvas),
            ("WPF detection overlay badges avoid overlap", TestWpfDetectionOverlayBadgesAvoidOverlap),
            ("WPF detection overlays hide fully offscreen badges", TestWpfDetectionOverlaySkipsOffscreenBadges),
            ("WPF detection overlay restores OpenGL state", TestWpfDetectionOverlayRestoresOpenGlState),
            ("WPF image queue presents row status with icons", TestWpfImageQueueStatusPresentation),
            ("CViewer exposes labeling mode chrome without legacy menu text", TestCViewerLabelingModeChrome),
            ("DisplayLayerDocument stores image and annotation state without a WinForms shell", TestDisplayLayerDocumentStateLifecycle),
            ("CDisplayManager owns replaced image source mats", TestDisplayManagerImageSourceOwnership),
            ("CDisplayManager exposes Dev-style layer catalog APIs", TestDisplayManagerLayerCatalog),
            ("CDisplayManager routes detection overlays to the exact layer", TestDisplayManagerDetectionOverlayRouting),
            ("Labeling workflow applies selected class and lists ROI rows", TestLabelingWorkflowService),
            ("Labeling workflow commits current annotations to YOLO files", TestLabelingWorkflowCommitAnnotations),
            ("Labeling workflow defaults unclassified ROI to Defect", TestLabelingWorkflowDefaultDefectClass),
            ("Labeling workflow reloads saved YOLO labels into Main layer", TestLabelingWorkflowReloadsSavedAnnotations),
            ("Detection result service applies Python boxes as Main candidates", TestDetectionResultApplicationService),
            ("Detection result service limits review candidates by confidence", TestDetectionResultLimitsReviewCandidates),
            ("Detection result service completes path-only batch results", TestDetectionResultPathOnlyBatchResult),
            ("Detection result service marshals Python boxes onto UI thread", TestDetectionResultApplicationServiceBackgroundThread),
            ("Detection result service keeps Main active after applying boxes", TestDetectionResultKeepsMainActive),
            ("Detection result service clears candidate state on empty Python boxes", TestDetectionResultApplicationServiceClearsEmptyResult),
            ("Detection result service ignores late boxes after pending cancellation", TestDetectionResultCancelPendingIgnoresLateBoxes),
            ("Detection result service times out pending Python requests", TestDetectionResultPendingRequestTimeout),
            ("Detection result service confirms boxes as Main labels", TestDetectionResultConfirmAsLabels),
            ("Detection result service can confirm boxes as segmentation masks", TestDetectionResultConfirmAsSegmentationMasks),
            ("Detection result service uses minimum confidence for confirmation availability", TestDetectionResultCanCommitUsesMinimumConfidence),
            ("Detection result service exposes review candidates with confirmability", TestDetectionResultReviewCandidates),
            ("Detection result service confirms selected candidate and keeps remaining candidates", TestDetectionResultConfirmsSelectedCandidateOnly),
            ("Detection result service skips selected candidate and reindexes overlays", TestDetectionResultSkipsSelectedCandidate),
            ("Detection result service confirms all candidates despite selection", TestDetectionResultConfirmsAllCandidatesDespiteSelection),
            ("Detection result service filters low-confidence boxes on confirm", TestDetectionResultFiltersLowConfidence),
            ("Detection result service adds missing model classes on confirm", TestDetectionResultAddsMissingClasses),
            ("Detection result service ignores boxes for a stale image", TestDetectionResultRejectsStaleImage),
            ("Detection result service rejects stale boxes during confirmation", TestDetectionResultRejectsStaleConfirmation),
            ("Measurement geometry calculates distance and vertical projection", TestMeasurementGeometry),
            ("Detection results build OpenGL overlays", TestDetectionOverlays),
            ("CViewer renders detection overlay labels and disposes textures", TestCViewerDetectionOverlayRenderLifecycle)
        };

        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        return 0;
    }

    private static int RunSingleSmoke(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            return 1;
        }
    }

    private static int RunWpfRoiObjectVerification()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("WPF annotation object verification covers rectangle and ellipse hit behavior", TestWpfAnnotationObjectVerificationProcess),
            ("WPF ROI object manipulation verification matrix passes", TestWpfRoiObjectManipulationVerificationMatrix),
            ("WPF ROI object manipulation updates shell state", TestWpfRoiObjectManipulationUpdatesShellState)
        };

        int exitCode = 0;
        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                exitCode = 1;
            }
        }

        return exitCode;
    }

    private static int RunWpfSegmentationObjectVerification()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("WPF polygon annotation service creates pixel segmentation objects", TestWpfPolygonAnnotationService),
            ("WPF shell polygon tool creates reviewable segmentation objects", TestWpfPolygonShellInputCreatesSegmentation),
            ("WPF mask annotation service paints and erases raster masks", TestWpfMaskAnnotationService),
            ("WPF raster mask move avoids full-image allocation", TestWpfRasterMaskMoveAvoidsFullImageAllocation),
            ("WPF shell brush and eraser create reviewable raster masks", TestWpfBrushEraserShellInputCreatesMaskSegmentation),
            ("WPF segmentation object manipulation verification matrix passes", TestWpfSegmentationObjectManipulationVerificationMatrix),
            ("WPF segmentation object manipulation updates shell state", TestWpfSegmentationObjectManipulationUpdatesShellState)
        };

        return RunNamedTestSet(tests);
    }

    private static int RunWpfAnnotationObjectVerification()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("WPF annotation object verification covers rectangle and ellipse hit behavior", TestWpfAnnotationObjectVerificationProcess),
            ("WPF ROI object manipulation verification matrix passes", TestWpfRoiObjectManipulationVerificationMatrix),
            ("WPF ROI object manipulation updates shell state", TestWpfRoiObjectManipulationUpdatesShellState),
            ("WPF polygon annotation service creates pixel segmentation objects", TestWpfPolygonAnnotationService),
            ("WPF shell polygon tool creates reviewable segmentation objects", TestWpfPolygonShellInputCreatesSegmentation),
            ("WPF mask annotation service paints and erases raster masks", TestWpfMaskAnnotationService),
            ("WPF shell brush and eraser create reviewable raster masks", TestWpfBrushEraserShellInputCreatesMaskSegmentation),
            ("WPF segmentation object manipulation verification matrix passes", TestWpfSegmentationObjectManipulationVerificationMatrix),
            ("WPF segmentation object manipulation updates shell state", TestWpfSegmentationObjectManipulationUpdatesShellState)
        };

        return RunNamedTestSet(tests);
    }

    private static int RunNamedTestSet(IReadOnlyList<(string Name, Action Test)> tests)
    {
        int exitCode = 0;
        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                exitCode = 1;
            }
        }

        return exitCode;
    }

    private static int RunWpfVisualSmoke(string[] args)
    {
        string outputPath = GetArgumentValue(
            args,
            "--output",
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "ui", "wpf-detection-overlay-visual-check.png"));
        outputPath = Path.GetFullPath(outputPath);
        int zoomSteps = TryParseInt(GetArgumentValue(args, "--zoom-steps", "0"), 0);
        int windowWidth = TryParseInt(GetArgumentValue(args, "--width", "1430"), 1430);
        int windowHeight = TryParseInt(GetArgumentValue(args, "--height", "900"), 900);
        string reviewTab = GetArgumentValue(args, "--review-tab", string.Empty);
        string theme = GetArgumentValue(args, "--theme", "dark");
        string annotationTool = GetArgumentValue(args, "--annotation-tool", string.Empty);
        bool roiOnly = HasArgument(args, "--roi-only");
        bool seedDuplicate = HasArgument(args, "--seed-duplicate");
        bool showBusyInferenceStatus = HasArgument(args, "--show-busy-inference-status");
        bool expandLearningConcepts = HasArgument(args, "--expand-learning-concepts");
        bool selectMaskObject = HasArgument(args, "--select-mask-object");
        if (roiOnly)
        {
            reviewTab = string.IsNullOrWhiteSpace(reviewTab) ? "objects" : reviewTab;
            annotationTool = string.IsNullOrWhiteSpace(annotationTool) ? "rectangle" : annotationTool;
        }

        try
        {
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            string root = null;
            try
            {
                string imagePath = GetArgumentValue(args, "--image", string.Empty);
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    imagePath = ResolveVisualSmokeImagePath();
                }

                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    root = CreateTempRoot();
                    imagePath = Path.Combine(root, "visual-check.jpg");
                    CreateVisualSmokeImage(imagePath);
                }

                Size imageSize = GetImageSize(imagePath);

                WpfLabelingShellWindow window = new WpfLabelingShellWindow
                {
                    Width = Math.Max(1100, windowWidth),
                    Height = Math.Max(720, windowHeight),
                    Left = 24,
                    Top = 24,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Topmost = true
                };

                try
                {
                    window.Show();
                    ApplyVisualSmokeTheme(window, theme);
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(500));
                    AssertTrue(window.TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false), "WPF visual smoke image load failed");

                    var candidates = roiOnly
                        ? new List<YoloWorkerSmokeCandidate>()
                        : CreateVisualSmokeCandidates(imageSize);
                    if (!roiOnly && seedDuplicate)
                    {
                        SeedVisualSmokeDuplicateLabel(window, candidates);
                    }

                    if (!roiOnly && window.FindName("InferenceModeButton") is System.Windows.Controls.Control inferenceModeButton)
                    {
                        InvokePrivateResult<object>(window, "InferenceModeButton_Click", inferenceModeButton, new System.Windows.RoutedEventArgs());
                    }

                    if (!roiOnly)
                    {
                        InvokePrivateResult<object>(window, "ApplyDetectionCandidates", candidates, true);
                        SeedVisualSmokeObjects(window, reviewTab, imageSize);
                    }

                    if (!roiOnly && selectMaskObject)
                    {
                        SelectVisualSmokeMaskObject(window);
                    }

                    SelectVisualSmokeReviewTab(window, reviewTab);
                    PrepareVisualSmokeGuidePanel(window, reviewTab, expandLearningConcepts);
                    window.UpdateLayout();
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(1200));
                    ApplyVisualSmokeZoom(window, zoomSteps);
                    CloseAuxiliaryVisualSmokeWindows(window);
                    BringVisualSmokeWindowToFront(window);
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(500));
                    CloseAuxiliaryVisualSmokeWindows(window);
                    BringVisualSmokeWindowToFront(window);
                    SelectVisualSmokeReviewTab(window, reviewTab);
                    PrepareVisualSmokeGuidePanel(window, reviewTab, expandLearningConcepts);
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(350));
                    CloseAuxiliaryVisualSmokeWindows(window);
                    BringVisualSmokeWindowToFront(window);
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(250));
                    CloseAuxiliaryVisualSmokeWindows(window);
                    BringVisualSmokeWindowToFront(window);
                    if (showBusyInferenceStatus)
                    {
                        InvokePrivateResult<object>(window, "SetGlobalInferenceStatus", "AI inference visual check", true, false);
                        PumpWpfDispatcher(TimeSpan.FromMilliseconds(250));
                    }
                    ApplyVisualSmokeAnnotationTool(window, annotationTool, imageSize);
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(250));
                    SelectVisualSmokeReviewTab(window, reviewTab);
                    PrepareVisualSmokeGuidePanel(window, reviewTab, expandLearningConcepts);
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(150));

                    CaptureWindow(window, outputPath);
                    Console.WriteLine($"WPF visual smoke captured: {outputPath}");
                    return 0;
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(root))
                {
                    DeleteTempRoot(root);
                }

                ShutdownVisualSmokeApplication();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL WPF visual smoke: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void ApplyVisualSmokeAnnotationTool(WpfLabelingShellWindow window, string annotationTool, Size imageSize)
    {
        if (window == null || string.IsNullOrWhiteSpace(annotationTool))
        {
            return;
        }

        string key = annotationTool.Trim().ToLowerInvariant();
        if (key is not ("brush" or "eraser" or "rectangle" or "box" or "ellipse" or "circle"))
        {
            return;
        }

        if (key is "rectangle" or "box" or "ellipse" or "circle")
        {
            int width = Math.Max(28, imageSize.Width / 3);
            int height = key == "circle" ? width : Math.Max(28, imageSize.Height / 3);
            AddWpfSessionRoi(
                window,
                new Rectangle(
                    Math.Max(4, imageSize.Width / 2 - width / 2),
                    Math.Max(4, imageSize.Height / 2 - height / 2),
                    width,
                    height),
                key is "ellipse" or "circle" ? CanvasRoiShapeKind.Ellipse : CanvasRoiShapeKind.Rectangle,
                imageSize,
                $"visual-{key}-roi");
            InvokePrivate(window, "RedrawReviewRois");
            SelectVisualSmokeActiveRoi(window);
            return;
        }

        WpfAnnotationTool tool = key == "eraser" ? WpfAnnotationTool.Eraser : WpfAnnotationTool.Brush;
        InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", tool);
        int radius = key == "eraser" ? 14 : 12;
        Color color = key == "eraser" ? Color.FromArgb(245, 158, 11) : Color.FromArgb(44, 210, 110);
        window.MainCanvasViewModel.SetBrushCursorPreview(
            new Point(Math.Max(0, imageSize.Width / 2), Math.Max(0, imageSize.Height / 2)),
            radius,
            color,
            key == "eraser");
    }

    private static int RunWpfQueueClickPerformanceSmoke(string[] args)
    {
        try
        {
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            string root = CreateTempRoot();
            try
            {
                int imageCount = Math.Max(4, TryParseInt(GetArgumentValue(args, "--count", "10"), 10));
                int settleMilliseconds = Math.Max(0, TryParseInt(GetArgumentValue(args, "--settle-ms", "60"), 60));
                string folder = GetArgumentValue(args, "--folder", string.Empty);
                var imagePaths = new List<string>();
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    if (!Directory.Exists(folder))
                    {
                        throw new DirectoryNotFoundException(folder);
                    }

                    imagePaths.AddRange(Directory.EnumerateFiles(folder)
                        .Where(path => IsSupportedImageFile(path))
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .Take(imageCount));
                    AssertTrue(imagePaths.Count >= Math.Min(imageCount, 4), "WPF performance smoke folder should contain at least four supported images");
                }
                else
                {
                    for (int i = 0; i < imageCount; i++)
                    {
                        string imagePath = Path.Combine(root, $"perf-{i:00}.jpg");
                        CreateVisualSmokeImage(imagePath);
                        imagePaths.Add(imagePath);
                    }
                }

                WpfLabelingShellWindow window = new WpfLabelingShellWindow
                {
                    Width = 1430,
                    Height = 900,
                    Left = 24,
                    Top = 24,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Topmost = true
                };

                try
                {
                    window.Show();
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(500));
                    AssertTrue(window.TryLoadImage(imagePaths[0], populateQueue: true, refreshQueueDetails: false), "WPF performance smoke initial load failed");
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(Math.Max(120, settleMilliseconds)));

                    var grid = (System.Windows.Controls.DataGrid)window.FindName("ImageQueueGrid");
                    Process process = Process.GetCurrentProcess();
                    process.Refresh();
                    long startWorkingSet = process.WorkingSet64;
                    long peakWorkingSet = startWorkingSet;
                    var elapsed = new List<double>();
                    var settledElapsed = new List<double>();
                    var selectionSetElapsed = new List<double>();
                    var renderDrainElapsed = new List<double>();
                    var backgroundDrainElapsed = new List<double>();
                    var idleDrainElapsed = new List<double>();
                    var loadDiagnostics = new List<WpfLabelingShellWindow.ImageLoadDiagnostics>();
                    foreach (string imagePath in imagePaths.Skip(1))
                    {
                        WpfImageQueueItem item = window.ImageQueueItems.First(queueItem =>
                            string.Equals(queueItem.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase));
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        Stopwatch selectionStopwatch = Stopwatch.StartNew();
                        grid.SelectedItem = item;
                        selectionStopwatch.Stop();
                        double renderDrainMilliseconds = DrainWpfDispatcherPriority(System.Windows.Threading.DispatcherPriority.Render);
                        double backgroundDrainMilliseconds = DrainWpfDispatcherPriority(System.Windows.Threading.DispatcherPriority.Background);
                        double idleDrainMilliseconds = DrainWpfDispatcherPriority(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        stopwatch.Stop();
                        AssertEqual(imagePath, GetPrivateField<string>(window, "activeImagePath"));
                        elapsed.Add(selectionStopwatch.Elapsed.TotalMilliseconds + renderDrainMilliseconds);
                        settledElapsed.Add(stopwatch.Elapsed.TotalMilliseconds);
                        selectionSetElapsed.Add(selectionStopwatch.Elapsed.TotalMilliseconds);
                        renderDrainElapsed.Add(renderDrainMilliseconds);
                        backgroundDrainElapsed.Add(backgroundDrainMilliseconds);
                        idleDrainElapsed.Add(idleDrainMilliseconds);
                        loadDiagnostics.Add(window.LastImageLoadDiagnostics);
                        process.Refresh();
                        peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
                        if (settleMilliseconds > 0)
                        {
                            PumpWpfDispatcher(TimeSpan.FromMilliseconds(settleMilliseconds));
                        }
                    }

                    process.Refresh();
                    long endWorkingSet = process.WorkingSet64;
                    peakWorkingSet = Math.Max(peakWorkingSet, endWorkingSet);
                    WpfLabelingShellWindow.ImageDecodeCacheDiagnostics cache = window.GetImageDecodeCacheDiagnostics();
                    double firstSwitchElapsed = elapsed[0];
                    IReadOnlyList<double> warmElapsed = elapsed.Skip(1).ToList();
                    double firstSettledElapsed = settledElapsed[0];
                    IReadOnlyList<double> warmSettledElapsed = settledElapsed.Skip(1).ToList();
                    double firstCommitElapsed = selectionSetElapsed[0];
                    IReadOnlyList<double> warmCommitElapsed = selectionSetElapsed.Skip(1).ToList();
                    int slowWarmIndex = 1;
                    if (warmElapsed.Count > 0)
                    {
                        double slowWarmElapsed = warmElapsed.Max();
                        slowWarmIndex = elapsed.FindIndex(1, value => Math.Abs(value - slowWarmElapsed) < 0.0001D);
                    }

                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF queue click perf: folder={(string.IsNullOrWhiteSpace(folder) ? "<generated>" : folder)}, metric=visible, count={elapsed.Count}, settle={settleMilliseconds}ms, first={firstSwitchElapsed:0.0}ms, min={elapsed.Min():0.0}ms, avg={elapsed.Average():0.0}ms, max={elapsed.Max():0.0}ms"));
                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF queue warm perf: count={warmElapsed.Count}, min={warmElapsed.Min():0.0}ms, avg={warmElapsed.Average():0.0}ms, max={warmElapsed.Max():0.0}ms"));
                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF queue settled perf: first={firstSettledElapsed:0.0}ms, warm-count={warmSettledElapsed.Count}, warm-min={warmSettledElapsed.Min():0.0}ms, warm-avg={warmSettledElapsed.Average():0.0}ms, warm-max={warmSettledElapsed.Max():0.0}ms"));
                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF queue image commit perf: first={firstCommitElapsed:0.0}ms, warm-count={warmCommitElapsed.Count}, warm-min={warmCommitElapsed.Min():0.0}ms, warm-avg={warmCommitElapsed.Average():0.0}ms, warm-max={warmCommitElapsed.Max():0.0}ms"));
                    Console.WriteLine(
                        "WPF queue click samples: "
                        + string.Join(", ", elapsed.Select(value => FormattableString.Invariant($"{value:0.0}ms"))));
                    Console.WriteLine(FormatImageLoadDiagnostics("WPF queue first switch steps", firstSwitchElapsed, loadDiagnostics[0]));
                    Console.WriteLine(FormatImageLoadDiagnostics("WPF queue slow warm steps", elapsed[slowWarmIndex], loadDiagnostics[slowWarmIndex]));
                    Console.WriteLine(FormatOuterQueueSwitchDiagnostics("WPF queue first outer steps", selectionSetElapsed[0], renderDrainElapsed[0], backgroundDrainElapsed[0], idleDrainElapsed[0]));
                    Console.WriteLine(FormatOuterQueueSwitchDiagnostics("WPF queue slow warm outer steps", selectionSetElapsed[slowWarmIndex], renderDrainElapsed[slowWarmIndex], backgroundDrainElapsed[slowWarmIndex], idleDrainElapsed[slowWarmIndex]));
                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF queue memory: start={FormatBytes(startWorkingSet)}, end={FormatBytes(endWorkingSet)}, peak={FormatBytes(peakWorkingSet)}, delta={FormatBytes(endWorkingSet - startWorkingSet)}"));
                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF decode cache: count={cache.Count}/{cache.Capacity}, bytes={FormatBytes(cache.Bytes)}/{FormatBytes(cache.MaxBytes)}, hits={cache.Hits}, misses={cache.Misses}, stores={cache.Stores}, evictions={cache.Evictions}"));
                    return 0;
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                DeleteTempRoot(root);
                ShutdownVisualSmokeApplication();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL WPF queue click perf: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static int RunWpfLabelingSessionSmoke(string[] args)
    {
        try
        {
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            string outputPath = GetArgumentValue(
                args,
                "--output",
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "ui", "wpf-labeling-session-smoke.png"));
            outputPath = Path.GetFullPath(outputPath);

            CData previousData = CGlobal.Inst.Data;
            string root = CreateTempRoot();
            try
            {
                string imageRoot = Path.Combine(root, "images");
                string outputRoot = Path.Combine(root, "dataset");
                Directory.CreateDirectory(imageRoot);
                string imagePath = Path.Combine(imageRoot, "session-0.jpg");
                CreateVisualSmokeImage(imagePath);
                CreateVisualSmokeImage(Path.Combine(imageRoot, "session-1.jpg"));

                CGlobal.Inst.Data = CreateWpfLabelingSessionData(root, imageRoot, outputRoot);

                WpfLabelingShellWindow window = new WpfLabelingShellWindow
                {
                    Width = 1430,
                    Height = 900,
                    Left = 24,
                    Top = 24,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Topmost = true
                };

                try
                {
                    window.Show();
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(500));
                    WpfLabelingSessionResult result = ExecuteWpfLabelingSessionFlow(window, imagePath, outputRoot);
                    SelectVisualSmokeReviewTab(window, "candidates");
                    window.UpdateLayout();
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(400));
                    CaptureWindow(window, outputPath);

                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF labeling session smoke: labels={result.SavedLabelLines}, segments={result.SavedSegmentFiles}, skipped={result.SkippedCandidates}, confirmed={result.ConfirmedCandidates}"));
                    Console.WriteLine($"WPF labeling session captured: {outputPath}");
                    return 0;
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                CGlobal.Inst.Data = previousData;
                DeleteTempRoot(root);
                ShutdownVisualSmokeApplication();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL WPF labeling session smoke: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static int RunWpfYoloTrainingSessionSmoke(string[] args)
    {
        try
        {
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            string outputPath = GetArgumentValue(
                args,
                "--output",
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "ui", "wpf-yolo-training-session-smoke.png"));
            outputPath = Path.GetFullPath(outputPath);

            CData previousData = CGlobal.Inst.Data;
            CCommunicationLearning previousCommunication = GetPrivateField<CCommunicationLearning>(CGlobal.Inst, "deepLearning");
            CCommunicationLearning trainingCommunication = null;
            string root = CreateTempRoot();
            try
            {
                string imageRoot = Path.Combine(root, "images");
                string outputRoot = Path.Combine(root, "dataset");
                Directory.CreateDirectory(imageRoot);
                CGlobal.Inst.Data = CreateWpfLabelingSessionData(root, imageRoot, outputRoot);
                CGlobal.Inst.Data.ProjectSettings.PythonModel.AutoStartClient = false;
                CGlobal.Inst.Data.ProjectSettings.YoloDataset.ValidationPercent = 50;
                CGlobal.Inst.Data.ProjectSettings.YoloDataset.SplitSeed = 17;
                CreateTrainingSessionImages(imageRoot, CGlobal.Inst.Data.ProjectSettings.YoloDataset, out string trainImagePath, out string validImagePath);

                int port = GetAvailableTcpPort();
                trainingCommunication = new CCommunicationLearning(startListen: false, port: port);
                CGlobal.Inst.DeepLearning = trainingCommunication;
                AssertTrue(trainingCommunication.Start(), "training smoke TCP listener did not start");
                using var requestReceived = new ManualResetEventSlim(false);
                using var statusSent = new ManualResetEventSlim(false);
                Task clientTask = Task.Run(() => RunMockTrainingClient(port, requestReceived, statusSent));
                AssertTrue(WaitUntil(() => trainingCommunication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(5)), "mock training client did not connect to listener");

                WpfLabelingShellWindow window = new WpfLabelingShellWindow
                {
                    Width = 1430,
                    Height = 900,
                    Left = 24,
                    Top = 24,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Topmost = true
                };

                try
                {
                    window.Show();
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(500));
                    WpfYoloTrainingSessionResult result = ExecuteWpfYoloTrainingSessionFlow(
                        window,
                        trainImagePath,
                        validImagePath,
                        outputRoot,
                        requestReceived,
                        statusSent);
                    SelectVisualSmokeReviewTab(window, "training");
                    window.UpdateLayout();
                    PumpWpfDispatcher(TimeSpan.FromMilliseconds(600));
                    CaptureWindow(window, outputPath);

                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"WPF YOLO training session smoke: ready={result.DatasetReady}, train={result.TrainImages}, valid={result.ValidImages}, objects={result.Objects}, packet={result.StartTrainingPacketReceived}, status={result.CompletedStatusReceived}, weights={Path.GetFileName(result.AppliedWeightsPath)}"));
                    Console.WriteLine($"WPF YOLO training session captured: {outputPath}");
                    AssertTrue(clientTask.Wait(TimeSpan.FromSeconds(5)), "mock training client did not finish");
                    if (clientTask.IsFaulted && clientTask.Exception != null)
                    {
                        throw clientTask.Exception;
                    }

                    return 0;
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                trainingCommunication?.Close();
                CGlobal.Inst.DeepLearning = previousCommunication;
                CGlobal.Inst.Data = previousData;
                DeleteTempRoot(root);
                ShutdownVisualSmokeApplication();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL WPF YOLO training session smoke: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static CData CreateWpfLabelingSessionData(string root, string imageRoot, string outputRoot)
    {
        var data = new CData();
        data.ConfigureOutputRoot(outputRoot);
        data.ProjectSettings.PythonModel.ImageRootPath = imageRoot;
        data.ProjectSettings.PythonModel.ProjectRootPath = root;
        data.ProjectSettings.YoloDataset.ValidationPercent = 0;
        data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.LimeGreen });
        data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.DeepSkyBlue });
        data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.Orange });
        return data;
    }

    private static WpfLabelingSessionResult ExecuteWpfLabelingSessionFlow(
        WpfLabelingShellWindow window,
        string imagePath,
        string outputRoot)
    {
        AssertTrue(window.TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: true), "session image load failed");
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));

        var learningPanel = (WpfLearningWorkflowPanel)window.FindName("LearningWorkflowPanelControl");
        AssertTrue(learningPanel != null, "learning workflow panel was not found");
        InvokePrivateResult<object>(window, "ExecuteYoloTrainingWorkflowStep", 3, learningPanel);
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));
        AssertEqual(WpfAnnotationTool.Rectangle, learningPanel.ViewModel.SelectedTool.Tool);
        AssertTrue(learningPanel.LearningConcepts.IsExpanded, "box labeling step should reveal the tool palette");

        Size imageSize = (Size)typeof(WpfLabelingShellWindow)
            .GetField("activeImageSize", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(window);
        AssertTrue(!imageSize.IsEmpty, "session image size was not set");

        AddWpfSessionRoi(window, new Rectangle(45, 50, 110, 100), CanvasRoiShapeKind.Rectangle, imageSize, "session-box");
        AddWpfSessionRoi(window, new Rectangle(170, 60, 48, 54), CanvasRoiShapeKind.Ellipse, imageSize, "session-ellipse");

        InvokePrivateResult<object>(window, "BeginPolygonAnnotationMode");
        ClickWpfSessionImagePoint(window, new Point(30, 30));
        ClickWpfSessionImagePoint(window, new Point(96, 35));
        ClickWpfSessionImagePoint(window, new Point(84, 88));
        ClickWpfSessionImagePoint(window, new Point(31, 31));

        InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Brush);
        InvokePrivateResult<object>(
            window,
            "MainCanvasViewModel_ImagePointClicked",
            window.MainCanvasViewModel,
            new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(188, 150), PointF.Empty));
        InvokePrivateResult<object>(
            window,
            "MainCanvasViewModel_ImagePointMoved",
            window.MainCanvasViewModel,
            new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(210, 154), PointF.Empty));
        InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Eraser);
        InvokePrivateResult<object>(
            window,
            "MainCanvasViewModel_ImagePointClicked",
            window.MainCanvasViewModel,
            new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(188, 150), PointF.Empty));

        var manualRois = GetPrivateField<List<Rectangle>>(window, "manualRois");
        var manualShapeKinds = GetPrivateField<List<CanvasRoiShapeKind>>(window, "manualRoiShapeKinds");
        var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
        AssertEqual(2, manualRois.Count);
        AssertTrue(manualShapeKinds.Contains(CanvasRoiShapeKind.Rectangle), "session should include a rectangle label");
        AssertTrue(manualShapeKinds.Contains(CanvasRoiShapeKind.Ellipse), "session should include an ellipse label");
        AssertTrue(manualSegments.Any(segment => segment?.IsRasterMask == false), "session should include a polygon segment");
        AssertTrue(manualSegments.Any(segment => segment?.IsRasterMask == true), "session should include a raster mask segment");

        object[] saveArgs = { 0 };
        AssertTrue(InvokePrivateResult<bool>(window, "SaveCurrentAnnotations", saveArgs), "session labels should be saveable");
        AssertTrue((int)saveArgs[0] >= 4, "session save should include boxes and segmentation objects");

        string labelPath = Path.Combine(outputRoot, "data", "train", "labels", "session-0.txt");
        AssertTrue(File.Exists(labelPath), "session YOLO label file was not saved");
        int savedLabelLines = File.ReadAllLines(labelPath).Length;
        AssertTrue(savedLabelLines >= 2, "session YOLO label file should include box labels");
        string segmentRoot = Path.Combine(outputRoot, "data", "train", "segments");
        int savedSegmentFiles = Directory.Exists(segmentRoot)
            ? Directory.EnumerateFiles(segmentRoot, "session-0.*").Count()
            : 0;
        AssertTrue(savedSegmentFiles > 0, "session segmentation data should be saved");

        var candidates = new List<YoloWorkerSmokeCandidate>
        {
            new YoloWorkerSmokeCandidate { Index = 1, ClassName = "OK", Confidence = 0.94, X = 45, Y = 50, Width = 110, Height = 100 },
            new YoloWorkerSmokeCandidate { Index = 2, ClassName = "NG", Confidence = 0.91, X = 198, Y = 128, Width = 34, Height = 44 }
        };
        InvokePrivateResult<object>(window, "ApplyDetectionCandidates", candidates, true);
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));

        var candidatePanel = (WpfCandidateReviewPanel)window.FindName("CandidateReviewPanelControl");
        WpfCandidateReviewPanelViewModel review = candidatePanel.ViewModel;
        AssertEqual(2, review.Candidates.Count);
        AssertTrue(review.ReviewHistory.Any(line => line.Contains("후보 로드", StringComparison.Ordinal)), "candidate review should show when AI candidates were loaded");
        AssertTrue(review.SelectedCandidate?.Payload == candidates[0], "duplicate candidate should be selected first");
        AssertTrue(!review.IsConfirmSelectedEnabled, "overlapping candidate should not be directly confirmable");
        AssertTrue(review.IsSkipSelectedEnabled, "selected candidate should be skippable");

        InvokePrivateResult<object>(window, "SkipSelectedCandidateButton_Click", candidatePanel.SkipSelectedButton, new System.Windows.RoutedEventArgs());
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));
        AssertTrue(review.ReviewHistory.Any(line => line.Contains("스킵", StringComparison.Ordinal)), "candidate review should record skipped candidates");
        AssertTrue(review.SelectedCandidate?.Payload == candidates[1], "skip should move review to the next candidate");
        AssertTrue(review.IsConfirmSelectedEnabled, "next non-overlapping candidate should be confirmable");
        AssertTrue(candidatePanel.SelectedCandidateSummaryTextBlock.Text.Contains("NG", StringComparison.Ordinal), "candidate summary should show the selected candidate");

        InvokePrivateResult<object>(window, "ConfirmSelectedCandidateButton_Click", candidatePanel.ConfirmSelectedButton, new System.Windows.RoutedEventArgs());
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));
        AssertTrue(review.ReviewHistory.Any(line => line.Contains("확정", StringComparison.Ordinal)), "candidate review should record confirmed candidates");
        var pending = GetPrivateField<List<YoloWorkerSmokeCandidate>>(window, "pendingDetectionCandidates");
        var confirmed = GetPrivateField<List<YoloWorkerSmokeCandidate>>(window, "confirmedDetectionCandidates");
        AssertEqual(0, pending.Count);
        AssertEqual(1, confirmed.Count);

        return new WpfLabelingSessionResult(savedLabelLines, savedSegmentFiles, 1, confirmed.Count);
    }

    private static WpfYoloTrainingSessionResult ExecuteWpfYoloTrainingSessionFlow(
        WpfLabelingShellWindow window,
        string trainImagePath,
        string validImagePath,
        string outputRoot,
        ManualResetEventSlim requestReceived,
        ManualResetEventSlim statusSent)
    {
        AssertTrue(window.TryLoadImage(trainImagePath, populateQueue: true, refreshQueueDetails: true), "training session train image load failed");
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(80));
        SaveTrainingSessionBox(window, new Rectangle(42, 52, 92, 80), "train label");

        AssertTrue(window.TryLoadImage(validImagePath, populateQueue: true, refreshQueueDetails: true), "training session valid image load failed");
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(80));
        SaveTrainingSessionBox(window, new Rectangle(52, 64, 76, 66), "valid label");

        InvokePrivateResult<object>(window, "ExecuteYoloTrainingWorkflowStep", 4, window.FindName("LearningWorkflowPanelControl"));
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
        YoloDatasetReadinessReport report = GetPrivateField<YoloDatasetReadinessReport>(window, "lastYoloTrainingReadinessReport");
        AssertTrue(report?.IsReady == true, report == null ? "dataset report was not created" : string.Join(Environment.NewLine, report.Errors));
        AssertTrue(report.Statistics.TrainImageCount > 0, "training session should have train images");
        AssertTrue(report.Statistics.ValidImageCount > 0, "training session should have valid images");
        AssertTrue(report.Statistics.TotalObjectCount >= 2, "training session should have saved objects");

        string oldWeightsPath = Path.Combine(outputRoot, "old.pt");
        File.WriteAllText(oldWeightsPath, "old");
        File.SetLastWriteTimeUtc(oldWeightsPath, DateTime.UtcNow.AddMinutes(-5));
        CGlobal.Inst.Data.ProjectSettings.PythonModel.WeightsPath = oldWeightsPath;

        string bestWeightsPath = Path.Combine(outputRoot, "runs", "train", "exp", "weights", "best.pt");
        Directory.CreateDirectory(Path.GetDirectoryName(bestWeightsPath));
        File.WriteAllText(bestWeightsPath, "best");
        File.SetLastWriteTimeUtc(bestWeightsPath, DateTime.UtcNow);

        InvokePrivateResult<object>(window, "ExecuteYoloTrainingWorkflowStep", 5, window.FindName("LearningWorkflowPanelControl"));
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(80));
        var startTrainingButton = (System.Windows.Controls.Control)window.FindName("StartTrainingButton");
        InvokePrivateResult<object>(window, "StartTrainingButton_Click", startTrainingButton, new System.Windows.RoutedEventArgs());
        AssertTrue(WaitUntilWpf(() => requestReceived.IsSet, TimeSpan.FromSeconds(5)), "mock worker did not receive StartTraining");
        AssertTrue(WaitUntilWpf(() => statusSent.IsSet, TimeSpan.FromSeconds(5)), "mock worker did not send training status");
        AssertTrue(WaitUntilWpf(
            () => string.Equals(CGlobal.Inst.Data.ProjectSettings.PythonModel.WeightsPath, bestWeightsPath, StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(5)),
            "completed training status should apply latest best.pt");

        var trainingPanel = (WpfTrainingSettingsPanel)window.FindName("TrainingSettingsPanelControl");
        AssertTrue(trainingPanel.ViewModel.TrainingProgressText.Contains("완료", StringComparison.Ordinal), "training progress should show completion");
        AssertTrue(trainingPanel.ViewModel.TrainingReadinessText.Contains("시작", StringComparison.Ordinal)
            || trainingPanel.ViewModel.TrainingReadinessText.Contains("완료", StringComparison.Ordinal)
            || trainingPanel.ViewModel.TrainingReadinessText.Contains("준비", StringComparison.Ordinal),
            "training readiness should remain operator-readable after start");

        InvokePrivateResult<object>(window, "ExecuteYoloTrainingWorkflowStep", 6, window.FindName("LearningWorkflowPanelControl"));
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(80));
        AssertTrue(((System.Windows.Controls.TabItem)window.FindName("CandidatesReviewTab")).IsSelected, "post-training step should move to candidate review");

        return new WpfYoloTrainingSessionResult(
            report.IsReady,
            report.Statistics.TrainImageCount,
            report.Statistics.ValidImageCount,
            report.Statistics.TotalObjectCount,
            requestReceived.IsSet,
            statusSent.IsSet,
            CGlobal.Inst.Data.ProjectSettings.PythonModel.WeightsPath);
    }

    private static void SaveTrainingSessionBox(WpfLabelingShellWindow window, Rectangle bounds, string label)
    {
        Size imageSize = GetPrivateField<Size>(window, "activeImageSize");
        AssertTrue(!imageSize.IsEmpty, $"{label} image size was not set");
        InvokePrivateResult<object>(window, "ExecuteYoloTrainingWorkflowStep", 3, window.FindName("LearningWorkflowPanelControl"));
        AddWpfSessionRoi(window, bounds, CanvasRoiShapeKind.Rectangle, imageSize, Guid.NewGuid().ToString("N"));
        object[] saveArgs = { 0 };
        AssertTrue(InvokePrivateResult<bool>(window, "SaveCurrentAnnotations", saveArgs), $"{label} should save");
        AssertTrue((int)saveArgs[0] > 0, $"{label} should save at least one object");
    }

    private static void CreateTrainingSessionImages(
        string imageRoot,
        YoloDatasetSettings settings,
        out string trainImagePath,
        out string validImagePath)
    {
        trainImagePath = string.Empty;
        validImagePath = string.Empty;
        for (int i = 0; i < 200 && (string.IsNullOrWhiteSpace(trainImagePath) || string.IsNullOrWhiteSpace(validImagePath)); i++)
        {
            string stem = $"training-session-{i:000}";
            IReadOnlyList<string> modes = YoloDatasetSplitService.SelectModesForImage(stem, settings);
            string path = Path.Combine(imageRoot, $"{stem}.jpg");
            if (modes.Contains(YoloDatasetSplitService.TrainMode) && string.IsNullOrWhiteSpace(trainImagePath))
            {
                CreateVisualSmokeImage(path, i + 1);
                trainImagePath = path;
            }
            else if (modes.Contains(YoloDatasetSplitService.ValidMode) && string.IsNullOrWhiteSpace(validImagePath))
            {
                CreateVisualSmokeImage(path, i + 1);
                validImagePath = path;
            }
        }

        AssertTrue(File.Exists(trainImagePath), "training session could not create a train-split image");
        AssertTrue(File.Exists(validImagePath), "training session could not create a valid-split image");
    }

    private static void RunMockTrainingClient(
        int port,
        ManualResetEventSlim requestReceived,
        ManualResetEventSlim statusSent)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using NetworkStream stream = client.GetStream();
        stream.ReadTimeout = 5000;
        stream.WriteTimeout = 5000;

        string packet = ReadTrainingPacket(stream);
        AssertTrue(packet.StartsWith("StartTraining", StringComparison.Ordinal), "mock worker received an unexpected command");
        AssertTrue(packet.Contains("dataYaml", StringComparison.Ordinal), "StartTraining packet should include dataYaml");
        requestReceived.Set();

        WriteJsonLine(stream, "{\"type\":\"TrainYoloResult\",\"version\":1,\"ok\":true,\"taskId\":\"task-smoke\",\"state\":\"started\"}");
        WriteJsonLine(stream, "{\"type\":\"TaskStatus\",\"version\":1,\"taskType\":\"TrainYolo\",\"taskId\":\"task-smoke\",\"state\":\"running\",\"message\":\"YOLOv5 training started.\",\"progressPercent\":35,\"epoch\":1,\"totalEpochs\":2}");
        Thread.Sleep(80);
        WriteJsonLine(stream, "{\"type\":\"TaskStatus\",\"version\":1,\"taskType\":\"TrainYolo\",\"taskId\":\"task-smoke\",\"state\":\"completed\",\"message\":\"YOLOv5 training completed.\",\"progressPercent\":100,\"epoch\":2,\"totalEpochs\":2}");
        statusSent.Set();
    }

    private static string ReadTrainingPacket(NetworkStream stream)
    {
        var buffer = new List<byte>();
        byte[] chunk = new byte[2048];
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow <= deadline)
        {
            int read = stream.Read(chunk, 0, chunk.Length);
            if (read <= 0)
            {
                break;
            }

            buffer.AddRange(chunk.Take(read));
            string text = Encoding.UTF8.GetString(buffer.ToArray());
            if (text.Contains("StartTraining", StringComparison.Ordinal)
                && text.Contains(LearningProtocol.PacketSeparator, StringComparison.Ordinal)
                && text.Contains("dataYaml", StringComparison.Ordinal)
                && text.TrimEnd().EndsWith("}", StringComparison.Ordinal))
            {
                return text;
            }
        }

        throw new InvalidOperationException("mock worker timed out while reading StartTraining packet");
    }

    private static void WriteJsonLine(NetworkStream stream, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static void AddWpfSessionRoi(
        WpfLabelingShellWindow window,
        Rectangle bounds,
        CanvasRoiShapeKind shapeKind,
        Size imageSize,
        string id)
    {
        var rect = new CanvasRect<float>(
            bounds.Left,
            imageSize.Height - bounds.Top,
            bounds.Right,
            imageSize.Height - bounds.Bottom)
        {
            UniqueId = id,
            ShapeKind = shapeKind,
            IsFill = shapeKind == CanvasRoiShapeKind.Ellipse
        };

        InvokePrivateResult<object>(
            window,
            "MainCanvasViewModel_RoiAdded",
            window.MainCanvasViewModel,
            new RoiChangedEventArgs { RoiRect = rect });
    }

    private static void ClickWpfSessionImagePoint(WpfLabelingShellWindow window, Point point)
    {
        InvokePrivateResult<object>(
            window,
            "MainCanvasViewModel_ImagePointClicked",
            window.MainCanvasViewModel,
            new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, point, PointF.Empty));
    }

    private sealed record WpfLabelingSessionResult(
        int SavedLabelLines,
        int SavedSegmentFiles,
        int SkippedCandidates,
        int ConfirmedCandidates);

    private sealed record WpfYoloTrainingSessionResult(
        bool DatasetReady,
        int TrainImages,
        int ValidImages,
        int Objects,
        bool StartTrainingPacketReceived,
        bool CompletedStatusReceived,
        string AppliedWeightsPath);

    private static string FormatImageLoadDiagnostics(string label, double measuredMilliseconds, WpfLabelingShellWindow.ImageLoadDiagnostics diagnostics)
    {
        double outsideMilliseconds = Math.Max(0D, measuredMilliseconds - diagnostics.TotalMilliseconds);
        string cacheText = diagnostics.CacheHit ? "hit" : "miss";
        return FormattableString.Invariant(
            $"{label}: measured={measuredMilliseconds:0.0}ms, load={diagnostics.TotalMilliseconds:0.0}ms, outside={outsideMilliseconds:0.0}ms, cache={cacheText}, decode={diagnostics.DecodeMilliseconds:0.0}ms, upload={diagnostics.CanvasUploadMilliseconds:0.0}ms, refresh={diagnostics.CanvasRefreshMilliseconds:0.0}ms, state={diagnostics.StateTransferMilliseconds:0.0}ms, reset={diagnostics.AnnotationResetMilliseconds:0.0}ms, queue={diagnostics.QueuePopulateMilliseconds:0.0}ms, review={diagnostics.ReviewRefreshMilliseconds:0.0}ms, preload={diagnostics.PreloadScheduleMilliseconds:0.0}ms");
    }

    private static string FormatOuterQueueSwitchDiagnostics(
        string label,
        double selectionSetMilliseconds,
        double renderDrainMilliseconds,
        double backgroundDrainMilliseconds,
        double idleDrainMilliseconds)
    {
        double totalDrainMilliseconds = renderDrainMilliseconds + backgroundDrainMilliseconds + idleDrainMilliseconds;
        return FormattableString.Invariant(
            $"{label}: selection-set={selectionSetMilliseconds:0.0}ms, render-drain={renderDrainMilliseconds:0.0}ms, background-drain={backgroundDrainMilliseconds:0.0}ms, idle-drain={idleDrainMilliseconds:0.0}ms, total-drain={totalDrainMilliseconds:0.0}ms");
    }

    private static double DrainWpfDispatcherPriority(System.Windows.Threading.DispatcherPriority priority)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        System.Windows.Threading.DispatcherFrame frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() =>
        {
            frame.Continue = false;
        }));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static bool IsSupportedImageFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBytes(long bytes)
    {
        double value = bytes;
        string sign = string.Empty;
        if (value < 0)
        {
            sign = "-";
            value = Math.Abs(value);
        }

        string[] units = { "B", "KB", "MB", "GB" };
        int unitIndex = 0;
        while (value >= 1024D && unitIndex < units.Length - 1)
        {
            value /= 1024D;
            unitIndex++;
        }

        return FormattableString.Invariant($"{sign}{value:0.0}{units[unitIndex]}");
    }

    private static void ShutdownVisualSmokeApplication()
    {
        System.Windows.Application application = System.Windows.Application.Current;
        if (application == null)
        {
            return;
        }

        application.Dispatcher.Invoke(() => application.Shutdown());
    }

    private static void SeedVisualSmokeDuplicateLabel(WpfLabelingShellWindow window, IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
    {
        if (window == null || candidates == null || candidates.Count == 0)
        {
            return;
        }

        var manualRois = GetPrivateField<List<System.Drawing.Rectangle>>(window, "manualRois");
        var manualRoiClassNames = GetPrivateField<List<string>>(window, "manualRoiClassNames");
        if (manualRois.Count > 0)
        {
            return;
        }

        YoloWorkerSmokeCandidate candidate = candidates[0];
        var duplicateBounds = new System.Drawing.Rectangle(
            Math.Max(0, (int)Math.Round(candidate.X)),
            Math.Max(0, (int)Math.Round(candidate.Y)),
            Math.Max(1, (int)Math.Round(candidate.Width)),
            Math.Max(1, (int)Math.Round(candidate.Height)));

        manualRois.Add(duplicateBounds);
        manualRoiClassNames.Add(string.IsNullOrWhiteSpace(candidate.ClassName) ? "OK" : candidate.ClassName);
    }

    private static void SeedVisualSmokeObjects(WpfLabelingShellWindow window, string reviewTab, Size imageSize)
    {
        if (window == null || string.IsNullOrWhiteSpace(reviewTab))
        {
            return;
        }

        string tabKey = reviewTab.Trim().ToLowerInvariant();
        if (tabKey is not ("objects" or "object"))
        {
            return;
        }

        var manualRois = GetPrivateField<List<System.Drawing.Rectangle>>(window, "manualRois");
        if (manualRois.Count == 0)
        {
            int width = Math.Max(24, imageSize.Width / 5);
            int height = Math.Max(24, imageSize.Height / 5);
            int x = Math.Max(0, (imageSize.Width - width) / 2);
            int y = Math.Max(0, (imageSize.Height - height) / 2);
            manualRois.Add(new System.Drawing.Rectangle(x, y, width, height));
            GetPrivateField<List<string>>(window, "manualRoiClassNames").Add("Defect");
        }

        var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
        if (manualSegments.Count == 0)
        {
            int left = Math.Max(2, imageSize.Width / 4);
            int top = Math.Max(2, imageSize.Height / 4);
            int right = Math.Min(imageSize.Width - 2, left + Math.Max(24, imageSize.Width / 4));
            int bottom = Math.Min(imageSize.Height - 2, top + Math.Max(20, imageSize.Height / 5));
            manualSegments.Add(new LabelingSegmentationObject(
                new[]
                {
                    new Point(left, top),
                    new Point(right, top + 4),
                    new Point(right - 8, bottom),
                    new Point(left + 6, bottom - 2)
                },
                new CClassItem { Text = "Defect", DrawColor = Color.DeepSkyBlue }));
        }

        if (!manualSegments.Any(segment => segment?.IsRasterMask == true))
        {
            var maskClass = new CClassItem { Text = "Mask", DrawColor = Color.FromArgb(44, 210, 110) };
            byte[] maskData = new byte[Math.Max(0, imageSize.Width * imageSize.Height)];
            int centerX = imageSize.Width / 2;
            int centerY = imageSize.Height / 2;
            int radiusX = Math.Max(8, imageSize.Width / 12);
            int radiusY = Math.Max(8, imageSize.Height / 14);
            for (int y = Math.Max(0, centerY - radiusY); y < Math.Min(imageSize.Height, centerY + radiusY); y++)
            {
                for (int x = Math.Max(0, centerX - radiusX); x < Math.Min(imageSize.Width, centerX + radiusX); x++)
                {
                    double dx = (x - centerX) / (double)radiusX;
                    double dy = (y - centerY) / (double)radiusY;
                    if ((dx * dx) + (dy * dy) <= 1D)
                    {
                        maskData[(y * imageSize.Width) + x] = 1;
                    }
                }
            }

            manualSegments.Add(new LabelingSegmentationObject(Array.Empty<Point>(), maskClass)
            {
                MaskData = maskData,
                MaskSize = imageSize,
                MaskBounds = SegmentationGeometry.GetMaskBounds(maskData, imageSize),
                RenderVersion = 1
            });
        }

        InvokePrivate(window, "RedrawReviewRois");
        InvokePrivate(window, "RefreshObjectList");
    }

    private static void SelectVisualSmokeMaskObject(WpfLabelingShellWindow window)
    {
        if (window?.FindName("ObjectReviewPanelControl") is not WpfObjectReviewPanel objectReviewPanel)
        {
            return;
        }

        WpfObjectReviewListItem maskItem = objectReviewPanel.ViewModel.Objects.FirstOrDefault(item =>
            item.IsEnabled
            && string.Equals(item.SourceKey, WpfObjectReviewSource.ManualSegment.ToString(), StringComparison.OrdinalIgnoreCase)
            && item.DisplayText.Contains("Mask", StringComparison.OrdinalIgnoreCase));
        if (maskItem == null)
        {
            return;
        }

        objectReviewPanel.ViewModel.SelectedObject = maskItem;
        InvokePrivate(window, "RefreshPolygonOverlays");
    }

    private static void SelectVisualSmokeActiveRoi(WpfLabelingShellWindow window)
    {
        if (window?.MainCanvasViewModel == null)
        {
            return;
        }

        CanvasRect<float> selectedRect = GetPrivateField<CanvasRect<float>>(window.MainCanvasViewModel, "_selectedRect");
        if (selectedRect == null || selectedRect.IsEmpty())
        {
            return;
        }

        selectedRect.IsEditing = true;
        selectedRect.IsChanged = true;
        window.MainCanvasViewModel.ImageViewer.RefreshGL();
    }

    private static void ApplyVisualSmokeZoom(WpfLabelingShellWindow window, int zoomSteps)
    {
        if (window == null || zoomSteps == 0)
        {
            return;
        }

        Size canvasSize = window.MainCanvasViewModel.ImageViewer.GetSize();
        var center = new Point(Math.Max(1, canvasSize.Width / 2), Math.Max(1, canvasSize.Height / 2));
        int delta = zoomSteps > 0 ? 120 : -120;
        for (int i = 0; i < Math.Abs(zoomSteps); i++)
        {
            window.MainCanvasViewModel.ImageViewer.ZoomAt(center, delta);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
        }
    }

    private static void SelectVisualSmokeReviewTab(WpfLabelingShellWindow window, string reviewTab)
    {
        if (window == null || string.IsNullOrWhiteSpace(reviewTab))
        {
            return;
        }

        string tabKey = reviewTab.Trim().ToLowerInvariant();
        string controlName = tabKey switch
        {
            "objects" or "object" => "ObjectsReviewTab",
            "candidates" or "candidate" => "CandidatesReviewTab",
            "guide" or "learning" or "learn" => "LearningReviewTab",
            "classes" or "class" => "ClassesReviewTab",
            "yolo" or "model" or "yolo-model" or "training" => "YoloSettingsReviewTab",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(controlName)
            && window.FindName(controlName) is System.Windows.Controls.TabItem tab)
        {
            if (window.FindName("ReviewTabControl") is System.Windows.Controls.TabControl reviewTabs)
            {
                reviewTabs.SelectedItem = tab;
            }

            tab.IsSelected = true;
            tab.Focus();
            window.UpdateLayout();
        }

        if (tabKey is "model" or "yolo-model"
            && window.FindName("YoloModelSettingsPanelControl") is WpfYoloModelSettingsPanel modelSettingsPanel)
        {
            modelSettingsPanel.SettingsExpander.IsExpanded = true;
            window.UpdateLayout();
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
            if (window.FindName("YoloSettingsScrollViewer") is System.Windows.Controls.ScrollViewer yoloScrollViewer)
            {
                yoloScrollViewer.UpdateLayout();
                yoloScrollViewer.ScrollToTop();
            }

            modelSettingsPanel.BringIntoView();
            window.UpdateLayout();
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
        }

        if (tabKey == "training"
            && window.FindName("TrainingSettingsPanelControl") is WpfTrainingSettingsPanel trainingPanel)
        {
            System.Windows.Controls.Expander trainingExpander = trainingPanel.SettingsExpander;
            trainingExpander.IsExpanded = true;
            window.UpdateLayout();
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
            if (window.FindName("YoloSettingsScrollViewer") is System.Windows.Controls.ScrollViewer yoloScrollViewer)
            {
                yoloScrollViewer.UpdateLayout();
                yoloScrollViewer.ScrollToVerticalOffset(yoloScrollViewer.ScrollableHeight);
            }

            trainingExpander.BringIntoView();
            window.UpdateLayout();
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
        }
    }

    private static void PrepareVisualSmokeGuidePanel(WpfLabelingShellWindow window, string reviewTab, bool expandLearningConcepts)
    {
        if (window == null || !expandLearningConcepts || string.IsNullOrWhiteSpace(reviewTab))
        {
            return;
        }

        string tabKey = reviewTab.Trim().ToLowerInvariant();
        if (tabKey is not ("guide" or "learning" or "learn"))
        {
            return;
        }

        if (window.FindName("LearningConceptsExpander") is System.Windows.Controls.Expander expander)
        {
            expander.IsExpanded = true;
        }

        window.UpdateLayout();
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(150));

        if (window.FindName("AnnotationToolListBox") is System.Windows.FrameworkElement toolList)
        {
            toolList.BringIntoView();
        }

        window.UpdateLayout();
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(150));
    }

    private static void ApplyVisualSmokeTheme(WpfLabelingShellWindow window, string theme)
    {
        if (window == null || string.IsNullOrWhiteSpace(theme))
        {
            return;
        }

        if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            InvokePrivateResult<object>(
                window,
                "ApplyTheme",
                GetNestedEnumValue<WpfLabelingShellWindow>("ShellTheme", "Light"));
        }
    }

    private static string GetArgumentValue(string[] args, string name, string defaultValue)
    {
        string prefix = name + "=";
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(prefix.Length).Trim('"');
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length
                && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[i + 1].Trim('"');
            }
        }

        return defaultValue;
    }

    private static bool HasArgument(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    private static int TryParseInt(string value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static string ResolveVisualSmokeImagePath()
    {
        var settings = new PythonModelSettings();
        settings.EnsureDefaults();
        return YoloWorkerSmokeTestService.ResolveSmokeImagePath(settings);
    }

    private static Size GetImageSize(string imagePath)
    {
        using Bitmap bitmap = new Bitmap(imagePath);
        return bitmap.Size;
    }

    private static List<YoloWorkerSmokeCandidate> CreateVisualSmokeCandidates(Size imageSize)
    {
        int width = Math.Max(80, imageSize.Width);
        int height = Math.Max(80, imageSize.Height);
        double outerWidth = Math.Max(42, width * 0.44);
        double outerHeight = Math.Max(42, height * 0.40);
        double outerX = Math.Max(0, (width - outerWidth) / 2);
        double outerY = Math.Max(0, (height - outerHeight) / 2);
        double innerWidth = Math.Max(18, width * 0.14);
        double innerHeight = Math.Max(24, height * 0.22);
        double innerX = Math.Max(0, (width - innerWidth) / 2);
        double innerY = Math.Max(0, (height - innerHeight) / 2);

        return new List<YoloWorkerSmokeCandidate>
        {
            new YoloWorkerSmokeCandidate
            {
                Index = 1,
                ClassName = "OK",
                Confidence = 0.959,
                X = outerX,
                Y = outerY,
                Width = outerWidth,
                Height = outerHeight
            },
            new YoloWorkerSmokeCandidate
            {
                Index = 2,
                ClassName = "NG",
                Confidence = 0.954,
                X = innerX,
                Y = innerY,
                Width = innerWidth,
                Height = innerHeight
            }
        };
    }

    private static void CloseAuxiliaryVisualSmokeWindows(System.Windows.Window mainWindow)
    {
        IntPtr mainHandle = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
        foreach (System.Windows.Window window in System.Windows.Application.Current.Windows.Cast<System.Windows.Window>().ToArray())
        {
            if (!ReferenceEquals(window, mainWindow))
            {
                window.Close();
            }
        }

        foreach (Form form in System.Windows.Forms.Application.OpenForms.Cast<Form>().ToArray())
        {
            form.Close();
        }

        uint currentProcessId = (uint)Process.GetCurrentProcess().Id;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == currentProcessId && hWnd != mainHandle && IsWindowVisible(hWnd))
            {
                SendMessage(hWnd, WmClose, IntPtr.Zero, IntPtr.Zero);
            }

            return true;
        }, IntPtr.Zero);
    }

    private static void BringVisualSmokeWindowToFront(System.Windows.Window window)
    {
        if (window == null)
        {
            return;
        }

        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        window.Topmost = true;
        window.Activate();
        window.Focus();
        SetWindowPos(handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
        SetForegroundWindow(handle);
    }

    private static void CreateVisualSmokeImage(string imagePath, int marker = 0)
    {
        using (Bitmap image = new Bitmap(260, 220))
        using (Graphics graphics = Graphics.FromImage(image))
        using (Brush background = new SolidBrush(Color.FromArgb(18, 18, 18)))
        using (Brush frame = new SolidBrush(Color.FromArgb(176, 176, 176)))
        using (Brush inner = new SolidBrush(Color.FromArgb(82, 82, 82)))
        using (Brush part = new SolidBrush(Color.FromArgb(138, 138, 138)))
        using (Brush defect = new SolidBrush(Color.FromArgb(38, 38, 38)))
        {
            graphics.Clear(Color.Black);
            graphics.FillRectangle(background, 0, 0, image.Width, image.Height);
            graphics.FillRectangle(frame, 30, 20, 200, 180);
            graphics.FillRectangle(inner, 43, 34, 174, 153);
            graphics.FillEllipse(part, 70, 58, 116, 116);
            graphics.FillRectangle(defect, 116, 86, 34, 58);
            if (marker > 0)
            {
                using Brush markerBrush = new SolidBrush(Color.FromArgb(40 + (marker * 37 % 180), 72 + (marker * 53 % 160), 96 + (marker * 29 % 140)));
                graphics.FillRectangle(markerBrush, 8, 8, 12, 12);
            }

            graphics.Save();
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
            image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
        }
    }

    private static void PumpWpfDispatcher(TimeSpan duration)
    {
        System.Windows.Threading.DispatcherFrame frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void CaptureWindow(System.Windows.Window window, string outputPath)
    {
        System.Windows.Media.Matrix transform = System.Windows.Media.Matrix.Identity;
        System.Windows.PresentationSource source = System.Windows.PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            transform = source.CompositionTarget.TransformToDevice;
        }

        System.Windows.Point topLeft = window.PointToScreen(new System.Windows.Point(0, 0));
        int width = Math.Max(1, (int)Math.Round(window.ActualWidth * transform.M11));
        int height = Math.Max(1, (int)Math.Round(window.ActualHeight * transform.M22));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        using (Bitmap bitmap = new Bitmap(width, height))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                (int)Math.Round(topLeft.X),
                (int)Math.Round(topLeft.Y),
                0,
                0,
                new Size(width, height));
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static void TestNormalizeYamlPath()
    {
        string normalized = CYolov5.NormalizeYamlPath(@"C:\datasets\train\images");
        AssertEqual("C:/datasets/train/images", normalized);
    }

    private static void TestClassCatalogService()
    {
        var data = new CData();

        AssertTrue(ClassCatalogService.TryAddClass(data, " Part ", out CClassItem first), "class was not added");
        AssertEqual("Part", first.Text);
        AssertEqual(Color.Green, first.DrawColor);
        AssertTrue(!ClassCatalogService.TryAddClass(data, "part", out CClassItem _), "duplicate class was accepted");
        AssertTrue(!ClassCatalogService.TryAddClass(data, " ", out CClassItem _), "empty class was accepted");
        AssertTrue(ClassCatalogService.RemoveClass(data, "PART"), "class was not removed case-insensitively");
        AssertEqual(0, data.ClassNamedList.Count);
    }

    private static void TestWpfObjectReviewEditService()
    {
        var manualRois = new List<Rectangle>
        {
            new Rectangle(1, 2, 10, 12),
            new Rectangle(3, 4, 20, 24)
        };
        var manualClasses = new List<string> { "Defect" };
        var manualSegments = new List<LabelingSegmentationObject>
        {
            new LabelingSegmentationObject(
                new[]
                {
                    new Point(1, 1),
                    new Point(8, 1),
                    new Point(8, 8)
                },
                new CClassItem { Text = "Poly" })
        };
        var confirmed = new List<YoloWorkerSmokeCandidate>
        {
            new YoloWorkerSmokeCandidate { ClassName = "OK", X = 5, Y = 6, Width = 7, Height = 8 },
            new YoloWorkerSmokeCandidate { ClassName = "NG", X = 9, Y = 10, Width = 11, Height = 12 }
        };

        AssertEqual("Defect", WpfObjectReviewEditService.GetClassName(WpfObjectReviewItemRef.Manual(1), manualClasses, manualSegments, confirmed));
        AssertTrue(
            WpfObjectReviewEditService.TryApplyClass(
                WpfObjectReviewItemRef.Manual(1),
                manualRois,
                manualClasses,
                manualSegments,
                confirmed,
                " Scratch ",
                out string manualClass),
            "manual object class was not applied");
        AssertEqual("Scratch", manualClass);
        AssertEqual("Scratch", manualClasses[1]);

        AssertTrue(
            WpfObjectReviewEditService.TryApplyClass(
                WpfObjectReviewItemRef.ConfirmedAi(0),
                manualRois,
                manualClasses,
                manualSegments,
                confirmed,
                " AIClass ",
                out string aiClass),
            "confirmed object class was not applied");
        AssertEqual("AIClass", aiClass);
        AssertEqual("AIClass", confirmed[0].ClassName);

        AssertTrue(
            WpfObjectReviewEditService.TryApplyClass(
                WpfObjectReviewItemRef.ManualSegment(0),
                manualRois,
                manualClasses,
                manualSegments,
                confirmed,
                " SegmentClass ",
                out string segmentClass),
            "manual segment class was not applied");
        AssertEqual("SegmentClass", segmentClass);
        AssertEqual("SegmentClass", manualSegments[0].ClassName);

        AssertTrue(
            WpfObjectReviewEditService.TryDelete(WpfObjectReviewItemRef.Manual(0), manualRois, manualClasses, manualSegments, confirmed),
            "manual object was not deleted");
        AssertEqual(1, manualRois.Count);
        AssertEqual(1, manualClasses.Count);
        AssertEqual("Scratch", manualClasses[0]);

        AssertTrue(
            WpfObjectReviewEditService.TryDelete(WpfObjectReviewItemRef.ManualSegment(0), manualRois, manualClasses, manualSegments, confirmed),
            "manual segment was not deleted");
        AssertEqual(0, manualSegments.Count);

        AssertTrue(
            WpfObjectReviewEditService.TryDelete(WpfObjectReviewItemRef.ConfirmedAi(1), manualRois, manualClasses, manualSegments, confirmed),
            "confirmed object was not deleted");
        AssertEqual(1, confirmed.Count);
        AssertEqual("AIClass", confirmed[0].ClassName);

        AssertTrue(
            !WpfObjectReviewEditService.TryDelete(WpfObjectReviewItemRef.Manual(3), manualRois, manualClasses, manualSegments, confirmed),
            "invalid manual object delete should fail");
    }

    private static void TestCreateYoloDataset()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.Red });

            data.SaveYoloDataYaml();

            AssertTrue(Directory.Exists(data.TrainImagesPath), "train images directory was not created");
            AssertTrue(Directory.Exists(Path.Combine(data.OutputRootPath, "data", "train", "labels")), "train labels directory was not created");
            AssertTrue(Directory.Exists(data.ValidImagesPath), "valid images directory was not created");
            AssertTrue(Directory.Exists(data.TestImagesPath), "test images directory was not created");
            AssertTrue(Directory.Exists(Path.Combine(data.OutputRootPath, "data", "test", "labels")), "test labels directory was not created");
            AssertTrue(File.Exists(data.DataYamlFilePath), "data.yaml was not created");

            string yaml = File.ReadAllText(data.DataYamlFilePath);
            AssertTrue(yaml.Contains("test:"), "data.yaml does not contain test image path");
            AssertTrue(yaml.Contains("nc: 2"), "data.yaml does not contain class count");
            AssertTrue(yaml.Contains("OK"), "data.yaml does not contain first class name");
            AssertTrue(yaml.Contains("NG"), "data.yaml does not contain second class name");
            AssertTrue(!yaml.Contains("\\"), "data.yaml contains backslashes");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetSplitService()
    {
        var settings = new YoloDatasetSettings
        {
            ValidationPercent = 20,
            TestPercent = 10,
            SplitSeed = 17
        };

        bool sawTrain = false;
        bool sawValid = false;
        bool sawTest = false;
        for (int i = 0; i < 500; i++)
        {
            IReadOnlyList<string> modes = YoloDatasetSplitService.SelectModesForImage($"split-sample-{i:000}", settings);
            AssertEqual(1, modes.Count);
            sawTrain |= modes.Contains(YoloDatasetSplitService.TrainMode);
            sawValid |= modes.Contains(YoloDatasetSplitService.ValidMode);
            sawTest |= modes.Contains(YoloDatasetSplitService.TestMode);
        }

        AssertTrue(sawTrain, "split service did not assign any train samples");
        AssertTrue(sawValid, "split service did not assign any valid samples");
        AssertTrue(sawTest, "split service did not assign any test samples");

        settings.ValidationPercent = 0;
        settings.TestPercent = 100;
        AssertEqual(YoloDatasetSplitService.TestMode, YoloDatasetSplitService.SelectModesForImage("all-test", settings)[0]);
    }

    private static void TestYoloDatasetValidatorConfiguration()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "ok", DrawColor = Color.Red });
            data.ProjectSettings.YoloDataset.ValidationPercent = 120;
            data.ProjectSettings.YoloDataset.TestPercent = 101;
            data.TranningParam.batch = 0;

            YoloDatasetValidationResult result = YoloDatasetValidator.ValidateConfiguration(data);

            AssertTrue(!result.IsValid, "invalid training configuration was accepted");
            AssertTrue(result.Errors.Any(error => error.Contains("Duplicate class name")), "duplicate class name was not reported");
            AssertTrue(result.Errors.Any(error => error.Contains("Validation split percent")), "invalid validation split was not reported");
            AssertTrue(result.Errors.Any(error => error.Contains("Test split percent")), "invalid test split was not reported");
            AssertTrue(result.Errors.Any(error => error.Contains("must not exceed 100 combined")), "combined split percent error was not reported");
            AssertTrue(result.Errors.Any(error => error.IndexOf("batch", StringComparison.OrdinalIgnoreCase) >= 0), "invalid batch was not reported");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetValidatorTrainingFiles()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(10, 10, 20, 20), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap trainImage = CreateSolidBitmap(80, 60, Color.Black))
            using (Bitmap validImage = CreateSolidBitmap(80, 60, Color.White))
            using (Bitmap testImage = CreateSolidBitmap(80, 60, Color.Gray))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                data.ProjectSettings.YoloDataset.TestPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", trainImage, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                data.ProjectSettings.YoloDataset.TestPercent = 0;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", validImage, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                data.ProjectSettings.YoloDataset.TestPercent = 100;
                YoloAnnotationService.SaveAnnotations("test-sample.png", testImage, rois, data.ClassNamedList, data);
            }

            YoloDatasetValidationResult configuration = YoloDatasetValidator.ValidateConfiguration(data);
            YoloDatasetValidationResult files = YoloDatasetValidator.ValidateTrainingFiles(data);

            AssertTrue(configuration.IsValid, configuration.Summary);
            AssertTrue(files.IsValid, files.Summary);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetValidatorRejectsTrainValidDuplicates()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(10, 10, 20, 20), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap image = CreateSolidBitmap(80, 60, Color.Black))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-duplicate.png", image, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                YoloAnnotationService.SaveAnnotations("valid-duplicate.png", image, rois, data.ClassNamedList, data);
            }

            YoloDatasetValidationResult result = YoloDatasetValidator.ValidateTrainingFiles(data);
            YoloDatasetStatistics statistics = YoloDatasetValidator.BuildStatistics(data);

            AssertTrue(!result.IsValid, "duplicate train/valid images were accepted");
            AssertTrue(result.Errors.Any(error => error.Contains("duplicate image content", StringComparison.OrdinalIgnoreCase)), "duplicate image content was not reported");
            AssertEqual(0, statistics.TrainValidImageNameOverlapCount);
            AssertEqual(1, statistics.TrainValidImageContentOverlapCount);
            AssertTrue(statistics.TrainValidImageOverlapExample.Contains("duplicate", StringComparison.OrdinalIgnoreCase), "overlap example should name the duplicated image");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetValidatorInvalidLabels()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.EnsureYoloOutputDirectories();
            data.SaveYoloDataYaml();

            using (Bitmap image = new Bitmap(20, 20))
            {
                image.Save(Path.Combine(data.TrainImagesPath, "sample.jpeg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(Path.Combine(data.ValidImagesPath, "sample.jpeg"), System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            string trainLabel = Path.Combine(root, "data", "train", "labels", "sample.txt");
            string validLabel = Path.Combine(root, "data", "valid", "labels", "sample.txt");
            File.WriteAllText(trainLabel, "1 0.5 0.5 0.2 0.2");
            File.WriteAllText(validLabel, "0 1.2 0.5 0.2 0.2");

            YoloDatasetValidationResult result = YoloDatasetValidator.ValidateTrainingFiles(data);

            AssertTrue(!result.IsValid, "invalid label contents were accepted");
            AssertTrue(result.Errors.Any(error => error.Contains("invalid class index")), "invalid class index was not reported");
            AssertTrue(result.Errors.Any(error => error.Contains("out-of-range normalized value")), "out-of-range value was not reported");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetStatistics()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(5, 5, 10, 10), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap trainImage = CreateSolidBitmap(40, 40, Color.Black))
            using (Bitmap validImage = CreateSolidBitmap(40, 40, Color.White))
            using (Bitmap testImage = CreateSolidBitmap(40, 40, Color.Gray))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                data.ProjectSettings.YoloDataset.TestPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", trainImage, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                data.ProjectSettings.YoloDataset.TestPercent = 0;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", validImage, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                data.ProjectSettings.YoloDataset.TestPercent = 100;
                YoloAnnotationService.SaveAnnotations("test-sample.png", testImage, rois, data.ClassNamedList, data);
            }

            YoloDatasetStatistics statistics = YoloDatasetValidator.BuildStatistics(data);

            AssertEqual(1, statistics.TrainImageCount);
            AssertEqual(1, statistics.ValidImageCount);
            AssertEqual(1, statistics.TestImageCount);
            AssertEqual(1, statistics.TrainLabelCount);
            AssertEqual(1, statistics.ValidLabelCount);
            AssertEqual(1, statistics.TestLabelCount);
            AssertEqual(3, statistics.TotalObjectCount);
            AssertEqual(3, statistics.ObjectCountByClass["OK"]);
            AssertEqual(0, statistics.TrainValidImageContentOverlapCount);
            AssertEqual(0, statistics.SplitImageContentOverlapCount);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetReadinessReport()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(5, 5, 10, 10), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap trainImage = CreateSolidBitmap(40, 40, Color.Black))
            using (Bitmap validImage = CreateSolidBitmap(40, 40, Color.White))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", trainImage, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", validImage, rois, data.ClassNamedList, data);
            }

            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml: true);

            AssertTrue(report.IsReady, string.Join(Environment.NewLine, report.Errors));
            AssertEqual(1, report.Statistics.TrainImageCount);
            AssertEqual(1, report.Statistics.ValidImageCount);
            AssertEqual(0, report.Statistics.TestImageCount);
            AssertTrue(report.SummaryLines.Any(line => line.Contains("TrainImages:1")), "readiness summary did not include train image count");
            AssertTrue(report.SummaryLines.Any(line => line.Contains("TestImages:0")), "readiness summary did not include test image count");
            AssertTrue(report.SummaryLines.Any(line => line.Contains("OK:2")), "readiness summary did not include class object count");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDatasetDiagnosticsReport()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);

            IReadOnlyList<string> lines = YoloDatasetDiagnosticsService.BuildOperatorReport(data, refreshYaml: false);

            AssertTrue(lines.Any(line => line.Contains("NOT READY")), "diagnostics did not report not-ready state");
            AssertTrue(lines.Any(line => line.Contains("YOLO output root")), "diagnostics did not include output root");
            AssertTrue(lines.Any(line => line.Contains("At least one class")), "diagnostics did not include class issue");

            data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.Red });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(5, 5, 10, 10), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap trainImage = CreateSolidBitmap(40, 40, Color.Black))
            using (Bitmap validImage = CreateSolidBitmap(40, 40, Color.White))
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                data.ProjectSettings.YoloDataset.TestPercent = 0;
                YoloAnnotationService.SaveAnnotations("train-sample.png", trainImage, rois, data.ClassNamedList, data);
                data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                data.ProjectSettings.YoloDataset.TestPercent = 0;
                YoloAnnotationService.SaveAnnotations("valid-sample.png", validImage, rois, data.ClassNamedList, data);
            }

            lines = YoloDatasetDiagnosticsService.BuildOperatorReport(data, refreshYaml: true);

            AssertTrue(lines.Any(line => line.Contains("READY")), "diagnostics did not report ready state");
            AssertTrue(lines.Any(line => line.Contains("YOLO split guide")), "diagnostics did not explain validation/test split use");
            AssertTrue(lines.Any(line => line.Contains("Test split is empty")), "diagnostics did not warn about missing test split");
            AssertTrue(lines.Any(line => line.Contains("class 'NG' has only 0")), "diagnostics did not warn about missing NG examples");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestTrainingSettingsMirror()
    {
        var data = new CData();
        data.TrainingParam.imageSize = 640;
        data.TrainingParam.batch = 8;
        data.TrainingParam.epoch = 100;
        data.TrainingParam.cfg = CYolov5TrainingParam.Cfg.yolov5m;
        data.TrainingParam.weight = CYolov5TrainingParam.Weight.yolov5m;

        TrainingSettings settings = data.GetTrainingSettings();

        AssertEqual(640, settings.ImageSize);
        AssertEqual(8, settings.Batch);
        AssertEqual(100, settings.Epoch);
        AssertEqual("yolov5m", settings.Cfg);
        AssertEqual("yolov5m", settings.Weight);
    }

    private static void TestTrainingParamAlias()
    {
        var data = new CData();
        data.TrainingParam.imageSize = 512;

        AssertTrue(ReferenceEquals(data.TranningParam, data.TrainingParam), "TrainingParam alias should use the legacy XML-backed object");
        AssertEqual(512, data.TranningParam.imageSize);

        data.TrainingParam = null;
        AssertTrue(data.TrainingParam != null, "TrainingParam alias should reject null assignment");
    }

    private static void TestYoloAnnotationLines()
    {
        var classes = new List<CClassItem>
        {
            new CClassItem { Text = "OK", DrawColor = Color.Green },
            new CClassItem { Text = "NG", DrawColor = Color.Red }
        };
        var rois = new Dictionary<string, List<CRectangleObject>>
        {
            ["NG"] = new List<CRectangleObject>
            {
                new CRectangleObject { Roi = new Rectangle(10, 20, 30, 40), cClassItem = classes[1] }
            }
        };

        List<string> lines = YoloAnnotationService.BuildAnnotationLines(rois, classes, new Size(100, 200));

        AssertEqual(1, lines.Count);
        AssertEqual("1 0.25 0.2 0.3 0.2", lines[0]);

        var data = new CData();
        string root = CreateTempRoot();
        try
        {
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            IReadOnlyList<string> targetLabelPaths = YoloAnnotationService.GetTargetLabelPaths("part-001.png", data);

            AssertEqual(1, targetLabelPaths.Count);
            AssertEqual(Path.Combine(root, "data", "train", "labels", "part-001.txt"), targetLabelPaths[0]);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloAnnotationLoad()
    {
        string root = CreateTempRoot();
        try
        {
            string labelPath = Path.Combine(root, "sample.txt");
            File.WriteAllLines(labelPath, new[]
            {
                "1 0.25 0.2 0.3 0.2",
                "bad line",
                "2 0.5 0.5 0.1 0.1"
            });

            var classes = new List<CClassItem>
            {
                new CClassItem { Text = "OK", DrawColor = Color.Green },
                new CClassItem { Text = "NG", DrawColor = Color.Red }
            };

            IReadOnlyDictionary<string, List<Rectangle>> loaded = YoloAnnotationService.LoadAnnotationRectangles(
                labelPath,
                classes,
                new Size(100, 200));

            AssertEqual(1, loaded.Count);
            AssertTrue(loaded.ContainsKey("NG"), "NG label was not loaded");
            AssertEqual(new Rectangle(10, 20, 30, 40), loaded["NG"][0]);

            AssertTrue(YoloAnnotationService.TryParseYoloLine("0 0.5 0.5 1 1", new Size(20, 10), out int classIndex, out Rectangle roi), "valid YOLO line did not parse");
            AssertEqual(0, classIndex);
            AssertEqual(new Rectangle(0, 0, 20, 10), roi);
            AssertTrue(!YoloAnnotationService.TryParseYoloLine("0 1.2 0.5 0.1 0.1", new Size(20, 10), out _, out _), "out-of-range YOLO line was accepted");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloImageLabelStatusService()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            data.EnsureYoloOutputDirectories();

            string imagePath = Path.Combine(root, "source", "sample.png");
            YoloImageLabelStatus missing = YoloImageLabelStatusService.Build(imagePath, new Size(20, 20), data);
            AssertTrue(!missing.HasLabelFile, "missing label status should not report a label file");
            AssertEqual("No Label", missing.Text);

            string labelPath = Path.Combine(root, "data", "train", "labels", "sample.txt");
            File.WriteAllLines(labelPath, new[]
            {
                "0 0.5 0.5 0.5 0.5",
                "9 0.5 0.5 0.5 0.5",
                "bad line"
            });

            YoloImageLabelStatus status = YoloImageLabelStatusService.Build(imagePath, new Size(20, 20), data);
            AssertTrue(status.HasLabelFile, "label status did not locate the saved label file");
            AssertEqual(labelPath, status.LabelPath);
            AssertEqual(1, status.ObjectCount);
            AssertEqual(2, status.InvalidLineCount);
            AssertEqual("Label 1 / Invalid 2", status.Text);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloImageReviewStatusService()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            data.EnsureYoloOutputDirectories();

            string sourceDirectory = Path.Combine(root, "source");
            Directory.CreateDirectory(sourceDirectory);

            string labeledImagePath = Path.Combine(sourceDirectory, "labeled.png");
            string candidateImagePath = Path.Combine(sourceDirectory, "candidate.png");
            string failedImagePath = Path.Combine(sourceDirectory, "failed.png");
            string skippedImagePath = Path.Combine(sourceDirectory, "skipped.png");
            var imagePaths = new List<string> { labeledImagePath, candidateImagePath, failedImagePath, skippedImagePath };

            string labelPath = Path.Combine(root, "data", "train", "labels", "labeled.txt");
            File.WriteAllText(labelPath, "0 0.5 0.5 0.25 0.25");

            var service = new YoloImageReviewStatusService();
            service.SetImages(imagePaths);
            service.RefreshLabelStatus(labeledImagePath, new Size(100, 100), data);
            service.RefreshLabelStatus(candidateImagePath, new Size(100, 100), data);
            service.RefreshLabelStatus(failedImagePath, new Size(100, 100), data);
            service.RefreshLabelStatus(skippedImagePath, new Size(100, 100), data);

            AssertEqual(1, service.GetLabeledCount());
            AssertTrue(service.TryFindNextUnlabeled(imagePaths, labeledImagePath, out string nextImagePath), "next unlabeled image was not found");
            AssertEqual(candidateImagePath, nextImagePath);

            YoloImageReviewStatus requested = service.SetDetectionRequested(candidateImagePath);
            AssertEqual("Requested", requested.DetectionText);

            YoloImageReviewStatus candidates = service.SetDetectionCandidates(string.Empty, "candidate", 2);
            AssertEqual(candidateImagePath, candidates.ImagePath);
            AssertEqual(2, candidates.DetectionCandidateCount);
            AssertEqual("Candidate 2", candidates.DetectionText);

            string candidateLabelPath = Path.Combine(root, "data", "train", "labels", "candidate.txt");
            File.WriteAllText(candidateLabelPath, "0 0.5 0.5 0.25 0.25");
            YoloImageReviewStatus candidateWithActiveBoxes = service.RefreshLabelStatusAndReviewState(candidateImagePath, new Size(100, 100), data, hasActiveCandidates: true);
            AssertEqual("Candidate 2", candidateWithActiveBoxes.DetectionText);
            AssertTrue(candidateWithActiveBoxes.IsLabeled, "saved label status should be preserved while active candidates remain");
            AssertEqual("Label 1", candidateWithActiveBoxes.LabelText);

            YoloImageReviewStatus noCandidates = service.SetDetectionNoCandidates(candidateImagePath, "candidate");
            AssertEqual("No Candidate", noCandidates.DetectionText);

            YoloImageReviewStatus cleared = service.ClearDetectionStatus(candidateImagePath);
            AssertEqual(string.Empty, cleared.DetectionText);

            YoloImageReviewStatus failedRequest = service.SetDetectionRequested(failedImagePath);
            AssertEqual(1, failedRequest.DetectionAttemptCount);

            YoloImageReviewStatus failed = service.SetDetectionFailed(failedImagePath, string.Empty, "Batch detection timed out.");
            AssertEqual("Failed", failed.DetectionText);
            AssertEqual(1, failed.DetectionAttemptCount);
            AssertTrue(failed.DetectionDetailText.Contains("Batch detection timed out."), "failed detection detail did not include the failure reason");

            service.SetDetectionRequested(failedImagePath);
            YoloImageReviewStatus retriedFailure = service.SetDetectionFailed(failedImagePath, string.Empty, "Detection request failed.");
            AssertEqual(2, retriedFailure.DetectionAttemptCount);
            AssertTrue(retriedFailure.DetectionDetailText.Contains("Attempt 2"), "retry count was not reflected in detection detail text");

            YoloImageReviewStatus skipped = service.MarkSkipped(skippedImagePath);
            AssertEqual("Skipped", skipped.DetectionText);
            AssertEqual(0, skipped.DetectionCandidateCount);
            AssertTrue(skipped.DetectionDetailText.Contains("Candidate skipped."), "skipped review detail did not include the skip reason");

            YoloImageReviewStatus confirmed = service.MarkConfirmed(labeledImagePath);
            AssertEqual("Confirmed", confirmed.DetectionText);
            AssertEqual(0, confirmed.DetectionCandidateCount);
            AssertTrue(confirmed.DetectionDetailText.Contains("Candidates confirmed."), "confirmed review detail did not include the confirmation reason");

            YoloImageReviewStatus confirmedBySavedLabel = service.RefreshLabelStatusAndReviewState(labeledImagePath, new Size(100, 100), data, hasActiveCandidates: false);
            AssertEqual("Confirmed", confirmedBySavedLabel.DetectionText);

            service.SetDetectionCandidates(candidateImagePath, "candidate", 2);
            service.SaveReviewStatus(data);

            string reviewStatusPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(data);
            AssertTrue(File.Exists(reviewStatusPath), "review status file was not saved");
            string reviewStatusJson = File.ReadAllText(reviewStatusPath);
            AssertTrue(reviewStatusJson.Contains("\"ReviewStateName\": \"Confirmed\""), "review status file did not include a readable confirmed state name");
            AssertTrue(reviewStatusJson.Contains("\"ReviewStateName\": \"Candidate\""), "review status file did not include a readable candidate state name");

            var restored = new YoloImageReviewStatusService();
            restored.LoadReviewStatus(data, imagePaths);

            AssertEqual("Confirmed", restored.GetOrCreate(labeledImagePath).DetectionText);
            AssertEqual("Candidate 2", restored.GetOrCreate(candidateImagePath).DetectionText);
            AssertEqual("Failed", restored.GetOrCreate(failedImagePath).DetectionText);
            AssertEqual(2, restored.GetOrCreate(failedImagePath).DetectionAttemptCount);
            AssertEqual("Detection request failed.", restored.GetOrCreate(failedImagePath).LastDetectionMessage);
            AssertEqual("Skipped", restored.GetOrCreate(skippedImagePath).DetectionText);

            string namedOnlyStatusJson = reviewStatusJson.Replace("\"ReviewState\": 4,", "\"ReviewState\": 999,");
            File.WriteAllText(reviewStatusPath, namedOnlyStatusJson);
            var restoredFromName = new YoloImageReviewStatusService();
            restoredFromName.LoadReviewStatus(data, imagePaths);
            AssertEqual("Confirmed", restoredFromName.GetOrCreate(labeledImagePath).DetectionText);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloAnnotationFileWrite()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["OK"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(25, 25, 50, 50), cClassItem = data.ClassNamedList[0] }
                }
            };

            using (Bitmap image = new Bitmap(100, 100))
            {
                YoloAnnotationService.SaveAnnotations("sample.png", image, rois, data.ClassNamedList, data);
            }

            string trainLabel = Path.Combine(root, "data", "train", "labels", "sample.txt");
            string validLabel = Path.Combine(root, "data", "valid", "labels", "sample.txt");
            string trainImage = Path.Combine(root, "data", "train", "images", "sample.jpeg");
            AssertTrue(File.Exists(trainLabel), "train label file was not created");
            AssertTrue(!File.Exists(validLabel), "valid label file should not be created when validation split is 0%");
            AssertTrue(File.Exists(trainImage), "train image file was not created");
            AssertEqual("0 0.5 0.5 0.5 0.5", File.ReadAllText(trainLabel).Trim());

            rois["OK"][0].Roi = new Rectangle(10, 20, 30, 40);
            DateTime imageStampBefore = File.GetLastWriteTimeUtc(trainImage).AddMinutes(-5);
            File.SetLastWriteTimeUtc(trainImage, imageStampBefore);
            imageStampBefore = File.GetLastWriteTimeUtc(trainImage);
            using (Bitmap image = new Bitmap(100, 100))
            {
                YoloAnnotationService.SaveAnnotations("sample.png", image, rois, data.ClassNamedList, data);
            }

            AssertEqual(imageStampBefore, File.GetLastWriteTimeUtc(trainImage));
            AssertEqual("0 0.25 0.4 0.3 0.4", File.ReadAllText(trainLabel).Trim());

            IReadOnlyDictionary<string, List<Rectangle>> loaded = YoloAnnotationService.LoadAnnotationRectanglesForImage(
                trainImage,
                data.ClassNamedList,
                data,
                new Size(100, 100));

            AssertEqual(new Rectangle(10, 20, 30, 40), loaded["OK"][0]);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestSegmentationAnnotationFileWrite()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.Green });
            data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.Red });

            using var image = new Bitmap(20, 20);
            var segments = new Dictionary<string, List<LabelingSegmentationObject>>
            {
                ["Defect"] = new List<LabelingSegmentationObject>
                {
                    new LabelingSegmentationObject(
                        new[]
                        {
                            new Point(2, 2),
                            new Point(12, 2),
                            new Point(12, 12),
                            new Point(2, 12)
                        },
                        data.ClassNamedList[1])
                    {
                        CutoutPolygons = new List<List<Point>>
                        {
                            new List<Point>
                            {
                                new Point(6, 6),
                                new Point(9, 6),
                                new Point(9, 9),
                                new Point(6, 9)
                            }
                        }
                    }
                }
            };

            YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                "seg-sample.png",
                image,
                segments,
                data.ClassNamedList,
                data);

            string maskPath = Path.Combine(root, "data", "train", "masks", "seg-sample.png");
            string segmentPath = Path.Combine(root, "data", "train", "segments", "seg-sample.json");
            AssertTrue(File.Exists(maskPath), "segmentation mask file was not saved");
            AssertTrue(File.Exists(segmentPath), "segmentation polygon json was not saved");

            using (var mask = new Bitmap(maskPath))
            {
                AssertEqual(2, mask.GetPixel(5, 5).R);
                AssertEqual(0, mask.GetPixel(7, 7).R);
                AssertEqual(0, mask.GetPixel(18, 18).R);
            }

            IReadOnlyDictionary<string, List<List<Point>>> loaded = YoloSegmentationAnnotationService.LoadSegmentationPolygons(
                segmentPath,
                data.ClassNamedList,
                image.Size);
            AssertTrue(loaded.ContainsKey("Defect"), "segmentation polygon class was not restored");
            AssertEqual(1, loaded["Defect"].Count);
            AssertEqual(new Point(2, 2), loaded["Defect"][0][0]);
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> loadedObjects = YoloSegmentationAnnotationService.LoadSegmentationObjects(
                segmentPath,
                data.ClassNamedList,
                image.Size);
            AssertEqual(1, loadedObjects["Defect"][0].CutoutPolygons.Count);
            SegmentationAnnotationFile annotationFile = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            AssertEqual(2, annotationFile.Version);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestSegmentationGeometryBoxConversion()
    {
        List<Point> polygon = SegmentationGeometry.RectangleToPolygon(new Rectangle(-2, 3, 12, 8), new Size(20, 20));

        AssertEqual(4, polygon.Count);
        AssertEqual(new Rectangle(0, 3, 10, 8), SegmentationGeometry.GetBounds(polygon));
        AssertTrue(SegmentationGeometry.ContainsPoint(polygon, new Point(5, 5)), "box polygon should contain its center");
        AssertTrue(!SegmentationGeometry.ContainsPoint(polygon, new Point(12, 5)), "box polygon should not contain outside pixels");

        List<Point> noisyLine = SegmentationGeometry.NormalizePolygon(
            new[]
            {
                new Point(1, 1),
                new Point(2, 1),
                new Point(3, 1),
                new Point(10, 1),
                new Point(10, 8),
                new Point(1, 8)
            },
            new Size(20, 20),
            minimumDistance: 1,
            simplificationTolerance: 2D);
        AssertTrue(noisyLine.Count < 6, "segmentation simplification should remove redundant drag points");

        List<Point> brush = SegmentationGeometry.CircleToPolygon(new Point(10, 10), 4, new Size(30, 30));
        AssertTrue(brush.Count >= 8, "brush polygon should create editable circle points");
        AssertTrue(SegmentationGeometry.IntersectsCircle(brush, new Point(10, 10), 3), "brush polygon should intersect its center circle");
        List<List<Point>> brushStroke = SegmentationGeometry.BrushStrokeToPolygons(
            new[]
            {
                new Point(10, 10),
                new Point(14, 10),
                new Point(18, 10)
            },
            4,
            new Size(40, 30));
        AssertEqual(1, brushStroke.Count);
        AssertTrue(SegmentationGeometry.GetBounds(brushStroke[0]).Contains(new Point(18, 10)), "brush stroke polygon should cover the last stroke point");
        List<Point> eraseSource = SegmentationGeometry.RectangleToPolygon(new Rectangle(5, 5, 20, 12), new Size(40, 30));
        List<List<Point>> edgeErased = SegmentationGeometry.EraseCircleFromPolygon(eraseSource, new Point(23, 11), 5, new Size(40, 30));
        AssertEqual(1, edgeErased.Count);
        AssertTrue(!SegmentationGeometry.ContainsPoint(edgeErased[0], new Point(23, 11)), "edge eraser should remove the brush center from the segment");
        List<Point> splitSource = SegmentationGeometry.RectangleToPolygon(new Rectangle(2, 2, 30, 12), new Size(40, 30));
        List<List<Point>> splitErased = SegmentationGeometry.EraseCircleFromPolygon(splitSource, new Point(17, 8), 8, new Size(40, 30));
        AssertTrue(splitErased.Count >= 2, "eraser should split separated remaining mask components");
        List<Point> mergedHull = SegmentationGeometry.MergePolygonsToHull(
            new[]
            {
                SegmentationGeometry.RectangleToPolygon(new Rectangle(1, 1, 5, 5), new Size(30, 30)),
                SegmentationGeometry.RectangleToPolygon(new Rectangle(12, 8, 4, 4), new Size(30, 30))
            },
            new Size(30, 30));
        AssertTrue(mergedHull.Count >= 3, "merged segmentation hull should create an editable polygon");
        AssertTrue(SegmentationGeometry.GetBounds(mergedHull).Contains(new Point(12, 8)), "merged hull should include the second segment bounds");

        using var image = new Bitmap(40, 40);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.FromArgb(180, 180, 180));
            using var brushFill = new SolidBrush(Color.FromArgb(25, 25, 25));
            graphics.FillEllipse(brushFill, 14, 15, 9, 8);
        }

        List<Point> extracted = SegmentationGeometry.ExtractDarkRegionPolygon(image, new Rectangle(8, 8, 24, 24));
        Rectangle extractedBounds = SegmentationGeometry.GetBounds(extracted);
        AssertTrue(extracted.Count >= 3, "dark region extraction should create a polygon");
        AssertTrue(extractedBounds.Contains(new Point(18, 18)), "dark region extraction should include the defect center");
    }

    private static void TestWpfPolygonAnnotationService()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            var defectClass = new CClassItem { Text = "Defect", DrawColor = Color.Red };
            data.ClassNamedList.Add(defectClass);

            var service = new WpfPolygonAnnotationService();
            Size imageSize = new Size(20, 20);
            bool closed;

            AssertTrue(service.TryAddPoint(new Point(-4, 2), imageSize, out closed), "first polygon point was not accepted");
            AssertTrue(!closed, "polygon should not close after the first point");
            AssertEqual(new Point(0, 2), service.Points[0]);
            AssertTrue(service.TryAddPoint(new Point(12, 2), imageSize, out closed), "second polygon point was not accepted");
            AssertTrue(service.TryAddPoint(new Point(12, 12), imageSize, out closed), "third polygon point was not accepted");
            AssertTrue(service.TryAddPoint(new Point(2, 12), imageSize, out closed), "fourth polygon point was not accepted");
            AssertEqual(4, service.Points.Count);
            AssertTrue(service.TryAddPoint(new Point(1, 3), imageSize, out closed), "near-start click should close the polygon draft");
            AssertTrue(closed, "near-start click did not report closed polygon");
            AssertTrue(service.IsClosed, "polygon draft should stay closed after near-start click");
            AssertEqual(4, service.Points.Count);

            AssertTrue(service.TryComplete(defectClass, imageSize, out LabelingSegmentationObject annotation, out string message), message);
            AssertEqual("Defect", annotation.ClassName);
            AssertEqual(4, annotation.Points.Count);
            AssertEqual(new Rectangle(0, 2, 13, 11), annotation.Bounds);
            AssertTrue(SegmentationGeometry.ContainsPoint(annotation.Points, new Point(6, 6)), "completed WPF polygon should contain its center");
            AssertEqual(0, WpfPolygonAnnotationService.FindNearestPointIndex(annotation, new Point(0, 2), 5));
            AssertTrue(WpfPolygonAnnotationService.TryMovePoint(annotation, 0, new Point(4, 4), imageSize, out Rectangle polygonMoveBounds), "polygon point should move in image-pixel coordinates");
            AssertEqual(new Point(4, 4), annotation.Points[0]);
            AssertTrue(polygonMoveBounds.Contains(new Point(4, 4)), "polygon point move bounds should include the new point");

            AssertTrue(WpfPolygonAnnotationService.TryCreateObject(
                new[]
                {
                    new Point(3, 3),
                    new Point(3, 3),
                    new Point(3, 3)
                },
                defectClass,
                imageSize,
                out LabelingSegmentationObject invalid) == false, "degenerate polygon should not create an annotation object");
            AssertTrue(invalid == null, "invalid polygon should not return an annotation object");

            using var image = new Bitmap(imageSize.Width, imageSize.Height);
            YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                "wpf-polygon.png",
                image,
                new Dictionary<string, List<LabelingSegmentationObject>>
                {
                    ["Defect"] = new List<LabelingSegmentationObject> { annotation }
                },
                data.ClassNamedList,
                data);

            string segmentPath = Path.Combine(root, "data", "train", "segments", "wpf-polygon.json");
            AssertTrue(File.Exists(segmentPath), "WPF polygon segmentation json was not saved");
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> loaded = YoloSegmentationAnnotationService.LoadSegmentationObjects(
                segmentPath,
                data.ClassNamedList,
                imageSize);
            AssertEqual(1, loaded["Defect"].Count);
            AssertEqual(annotation.Bounds, loaded["Defect"][0].Bounds);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfPolygonShellInputCreatesSegmentation()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        Bitmap bitmap = new Bitmap(40, 40);
        try
        {
            SetPrivateField(window, "activeImageSize", new Size(40, 40));
            SetPrivateField(window, "activeImageBitmap", bitmap);
            CGlobal.Inst.Data.LastSelectImageName = "wpf-polygon-shell";
            CGlobal.Inst.Data.ClassNamedList.Clear();
            CGlobal.Inst.Data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.DeepSkyBlue });

            InvokePrivateResult<object>(window, "BeginPolygonAnnotationMode");
            AssertTrue(window.MainCanvasViewModel.IsImagePointInputMode, "polygon tool should enter image-pixel click input mode");

            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(5, 5), PointF.Empty));
            AssertEqual(1, window.MainCanvasViewModel.PolygonOverlays.Count);
            AssertTrue(window.MainCanvasViewModel.PolygonOverlays[0].IsDraft, "first polygon click should render a draft overlay");

            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(25, 5), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(25, 20), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(6, 6), PointF.Empty));

            var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
            AssertEqual(1, manualSegments.Count);
            AssertEqual("Defect", manualSegments[0].ClassName);
            AssertTrue(SegmentationGeometry.ContainsPoint(manualSegments[0].Points, new Point(12, 9)), "completed shell polygon should contain an interior image point");
            AssertEqual(1, window.MainCanvasViewModel.PolygonOverlays.Count);
            AssertTrue(!window.MainCanvasViewModel.PolygonOverlays[0].IsDraft, "completed polygon should render as a confirmed overlay");

            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = InvokePrivateResult<Dictionary<string, List<LabelingSegmentationObject>>>(window, "BuildAnnotationSegments");
            AssertEqual(1, segmentsByClass["Defect"].Count);
            var objectReviewPanel = (WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl");
            AssertTrue(objectReviewPanel.ViewModel.Objects.Any(item => item.DisplayText.Contains("Polygon", StringComparison.Ordinal)), "object review should list the completed polygon");

            SetPrivateField(window, "activeAnnotationTool", WpfAnnotationTool.Select);
            window.MainCanvasViewModel.IsImagePointInputMode = true;
            objectReviewPanel.ViewModel.SelectedObject = objectReviewPanel.ViewModel.Objects.First(item => item.DisplayText.Contains("Polygon", StringComparison.Ordinal));
            Point originalPoint = manualSegments[0].Points[0];
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, originalPoint, PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(originalPoint.X + 3, originalPoint.Y + 2), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(originalPoint.X + 3, originalPoint.Y + 2), PointF.Empty));
            AssertEqual(new Point(originalPoint.X + 3, originalPoint.Y + 2), manualSegments[0].Points[0]);
        }
        finally
        {
            SetPrivateField(window, "activeImageBitmap", null);
            window.Close();
            bitmap.Dispose();
        }
    }

    private static void TestWpfMaskAnnotationService()
    {
        var service = new WpfMaskAnnotationService();
        var segments = new List<LabelingSegmentationObject>();
        Size imageSize = new Size(30, 30);
        var defectClass = new CClassItem { Text = "Defect", DrawColor = Color.DeepSkyBlue };

        AssertTrue(service.Paint(
            segments,
            new[] { new Point(10, 10) },
            3,
            imageSize,
            defectClass,
            out LabelingSegmentationObject paintedSegment,
            out Rectangle firstBounds), "brush should paint a raster mask");
        AssertEqual(1, segments.Count);
        AssertTrue(paintedSegment.IsRasterMask, "brush should create a raster mask segment");
        AssertEqual(imageSize, paintedSegment.MaskSize);
        AssertTrue(firstBounds.Contains(new Point(10, 10)), "changed bounds should include the brush center");
        AssertTrue(paintedSegment.Bounds.Contains(new Point(10, 10)), "mask bounds should include the brush center");
        AssertTrue(paintedSegment.MaskData[(10 * imageSize.Width) + 10] > 0, "brush center pixel should be painted");

        IReadOnlyList<Point> stroke = service.BuildStrokeCenters(new Point(10, 10), new Point(18, 10), 3);
        AssertTrue(stroke.Count > 2, "drag stroke should interpolate centers between sparse mouse events");
        AssertTrue(service.Paint(
            segments,
            stroke,
            3,
            imageSize,
            defectClass,
            out LabelingSegmentationObject strokeSegment,
            out _), "interpolated stroke should update the mask");
        AssertTrue(ReferenceEquals(paintedSegment, strokeSegment), "same class brush strokes should reuse the raster mask segment");
        AssertTrue(strokeSegment.Bounds.Contains(new Point(18, 10)), "stroke end should be included in mask bounds");
        Rectangle beforeMoveBounds = strokeSegment.Bounds;
        int renderVersionBeforeMove = strokeSegment.RenderVersion;
        AssertTrue(service.TryMoveRasterMask(strokeSegment, 2, 3, imageSize, out Rectangle moveBounds), "selected raster mask should move in image-pixel coordinates");
        AssertEqual(new Rectangle(beforeMoveBounds.X + 2, beforeMoveBounds.Y + 3, beforeMoveBounds.Width, beforeMoveBounds.Height), strokeSegment.Bounds);
        AssertTrue(strokeSegment.RenderVersion > renderVersionBeforeMove, "mask move should invalidate the OpenGL texture preview");
        AssertTrue(moveBounds.Contains(new Point(beforeMoveBounds.Left, beforeMoveBounds.Top)), "mask move dirty bounds should include the old mask area");
        AssertTrue(strokeSegment.MaskData[(13 * imageSize.Width) + 12] > 0, "mask move should carry painted pixels to the new location");

        AssertTrue(service.Erase(
            segments,
            new[] { new Point(12, 13) },
            3,
            imageSize,
            out Rectangle eraseBounds), "eraser should modify an existing raster mask");
        AssertTrue(eraseBounds.Contains(new Point(12, 13)), "erase bounds should include the erased center");
        AssertEqual((byte)0, paintedSegment.MaskData[(13 * imageSize.Width) + 12]);

        AssertTrue(service.Erase(
            segments,
            new[] { new Point(17, 13) },
            20,
            imageSize,
            out _), "large eraser should clear the remaining mask");
        AssertEqual(0, segments.Count);
    }

    private static void TestWpfRasterMaskMoveAvoidsFullImageAllocation()
    {
        var service = new WpfMaskAnnotationService();
        var imageSize = new Size(4_096, 4_096);
        var defectClass = new CClassItem { Text = "Defect", DrawColor = Color.DeepSkyBlue };
        var maskBounds = new Rectangle(200, 200, 64, 64);
        var segment = new LabelingSegmentationObject(Array.Empty<Point>(), defectClass)
        {
            ClassName = "Defect",
            ClassItem = defectClass,
            MaskData = new byte[imageSize.Width * imageSize.Height],
            MaskSize = imageSize,
            MaskBounds = maskBounds,
            RenderVersion = 1,
            RenderDirtyBounds = Rectangle.Empty
        };

        for (int y = maskBounds.Top; y < maskBounds.Bottom; y++)
        {
            int rowOffset = y * imageSize.Width;
            for (int x = maskBounds.Left; x < maskBounds.Right; x++)
            {
                segment.MaskData[rowOffset + x] = 1;
            }
        }

        byte[] originalMaskData = segment.MaskData;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            int deltaX = index % 2 == 0 ? 1 : -1;
            AssertTrue(service.TryMoveRasterMask(segment, deltaX, 0, imageSize, out _), "large image mask move should stay editable");
        }

        stopwatch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"WPF_RASTER_MASK_MOVE_4096_1000_MS={stopwatch.Elapsed.TotalMilliseconds:F3} ALLOCATED_MB={allocatedBytes / (1024D * 1024D):F3} SAME_BUFFER={ReferenceEquals(originalMaskData, segment.MaskData)} BOUNDS={segment.Bounds}"));

        AssertTrue(ReferenceEquals(originalMaskData, segment.MaskData), "raster mask move should reuse the full image buffer instead of allocating one per MouseMove");
        AssertEqual(maskBounds, segment.Bounds);
        AssertTrue(segment.MaskData[(maskBounds.Top * imageSize.Width) + maskBounds.Left] > 0, "raster mask move should preserve painted pixels");
        AssertTrue(allocatedBytes < 64L * 1024L * 1024L, "raster mask move should allocate only the active bounds buffer, not the full image mask per move");
        AssertTrue(stopwatch.Elapsed.TotalMilliseconds < 500.0, "raster mask move should stay lightweight for large images when the active mask bounds are small");

        var largeActiveImageSize = new Size(2_048, 2_048);
        var largeActiveBounds = new Rectangle(256, 256, 1_536, 1_536);
        var largeActiveSegment = new LabelingSegmentationObject(Array.Empty<Point>(), defectClass)
        {
            ClassName = "Defect",
            ClassItem = defectClass,
            MaskData = new byte[largeActiveImageSize.Width * largeActiveImageSize.Height],
            MaskSize = largeActiveImageSize,
            MaskBounds = largeActiveBounds,
            RenderVersion = 1,
            RenderDirtyBounds = Rectangle.Empty
        };

        for (int y = largeActiveBounds.Top; y < largeActiveBounds.Bottom; y++)
        {
            int rowOffset = y * largeActiveImageSize.Width;
            for (int x = largeActiveBounds.Left; x < largeActiveBounds.Right; x++)
            {
                largeActiveSegment.MaskData[rowOffset + x] = 1;
            }
        }

        byte[] originalLargeMaskData = largeActiveSegment.MaskData;
        long largeAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch largeStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 100; index++)
        {
            int deltaX = index % 2 == 0 ? 1 : -1;
            AssertTrue(service.TryMoveRasterMask(largeActiveSegment, deltaX, 0, largeActiveImageSize, out _), "large active mask move should stay editable");
        }

        largeStopwatch.Stop();
        long largeAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - largeAllocatedBefore;
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"WPF_RASTER_MASK_LARGE_ACTIVE_MOVE_2048_100_MS={largeStopwatch.Elapsed.TotalMilliseconds:F3} ALLOCATED_MB={largeAllocatedBytes / (1024D * 1024D):F3} SAME_BUFFER={ReferenceEquals(originalLargeMaskData, largeActiveSegment.MaskData)} BOUNDS={largeActiveSegment.Bounds}"));

        AssertTrue(ReferenceEquals(originalLargeMaskData, largeActiveSegment.MaskData), "large active mask move should keep the original full image mask buffer");
        AssertEqual(largeActiveBounds, largeActiveSegment.Bounds);
        AssertTrue(largeAllocatedBytes < 64L * 1024L * 1024L, "large active mask move should rent scratch memory instead of allocating one multi-MB array per MouseMove");
        AssertTrue(largeStopwatch.Elapsed.TotalMilliseconds < 750.0, "large active mask move should remain bounded even when the active mask covers much of the image");
    }

    private static void TestWpfBrushEraserShellInputCreatesMaskSegmentation()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        var data = new CData();
        data.LastSelectImageName = "wpf-mask-shell";
        data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.LimeGreen });
        CGlobal.Inst.Data = data;

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        Bitmap bitmap = new Bitmap(40, 40);
        try
        {
            SetPrivateField(window, "activeImageSize", new Size(40, 40));
            SetPrivateField(window, "activeImageBitmap", bitmap);
            SetPrivateField(window.MainCanvasViewModel, "_imageSize", new Size(40, 40));

            InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Brush);
            AssertTrue(window.MainCanvasViewModel.IsImagePointInputMode, "brush tool should enter image-pixel mask input mode");
            AssertTrue(!window.MainCanvasViewModel.IsTeachingMode, "brush tool should not use ROI drawing mode");
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointHovered", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.None, 0, 0, 0, new Point(10, 10), PointF.Empty));
            AssertTrue(window.MainCanvasViewModel.IsBrushCursorPreviewVisible, "brush hover should show the image-pixel radius preview");
            AssertEqual(6, window.MainCanvasViewModel.BrushCursorPreviewRadius);

            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(10, 10), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(18, 10), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(18, 10), PointF.Empty));

            var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
            AssertEqual(1, manualSegments.Count);
            LabelingSegmentationObject maskSegment = manualSegments[0];
            AssertTrue(maskSegment.IsRasterMask, "WPF brush should create a raster mask segment");
            AssertEqual("Defect", maskSegment.ClassName);
            AssertTrue(maskSegment.MaskData[(10 * 40) + 10] > 0, "WPF brush should paint the clicked image pixel");
            AssertEqual(1, window.MainCanvasViewModel.MaskOverlays.Count);
            AssertEqual(0, window.MainCanvasViewModel.PolygonOverlays.Count);
            AssertTrue(window.MainCanvasViewModel.MaskOverlays[0].IsValid, "raster mask should render through the OpenGL texture overlay preview");
            AssertTrue(window.MainCanvasViewModel.MaskOverlays[0].IsSelected, "selected mask object should be highlighted on the OpenGL mask overlay");
            AssertTrue(window.MainCanvasViewModel.MaskOverlays[0].Label.Contains("MASK", StringComparison.Ordinal), "mask overlay should carry a compact canvas selection label");

            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = InvokePrivateResult<Dictionary<string, List<LabelingSegmentationObject>>>(window, "BuildAnnotationSegments");
            AssertEqual(1, segmentsByClass["Defect"].Count);
            AssertTrue(segmentsByClass["Defect"][0].IsRasterMask, "WPF save path should keep raster mask segments");

            var objectReviewPanel = (WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl");
            AssertTrue(objectReviewPanel.ViewModel.Objects.Any(item => item.DisplayText.Contains("Mask", StringComparison.Ordinal)), "object review should list the raster mask");
            objectReviewPanel.ViewModel.SelectedObject = objectReviewPanel.ViewModel.Objects.First(item => item.DisplayText.Contains("Mask", StringComparison.Ordinal));

            SetPrivateField(window, "activeAnnotationTool", WpfAnnotationTool.Select);
            window.MainCanvasViewModel.IsImagePointInputMode = true;
            Rectangle maskBoundsBeforeMove = maskSegment.Bounds;
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(18, 10), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(21, 12), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(21, 12), PointF.Empty));
            AssertEqual(new Rectangle(maskBoundsBeforeMove.X + 3, maskBoundsBeforeMove.Y + 2, maskBoundsBeforeMove.Width, maskBoundsBeforeMove.Height), maskSegment.Bounds);

            InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Eraser);
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointHovered", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.None, 0, 0, 0, new Point(10, 10), PointF.Empty));
            AssertTrue(window.MainCanvasViewModel.IsBrushCursorPreviewVisible, "eraser hover should keep the radius preview visible");
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(13, 12), PointF.Empty));
            AssertEqual((byte)0, maskSegment.MaskData[(12 * 40) + 13]);
            AssertTrue(window.MainCanvasViewModel.MaskOverlays.Count <= 1, "mask overlay refresh should not duplicate stale texture overlays");
            InvokePrivateResult<object>(window, "EndMaskAnnotationMode");
            AssertTrue(!window.MainCanvasViewModel.IsBrushCursorPreviewVisible, "mask cursor preview should clear when the mask tool ends");
        }
        finally
        {
            SetPrivateField(window, "activeImageBitmap", null);
            window.Close();
            bitmap.Dispose();
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestWpfMaskBrushDragCommitsHistoryOnce()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        var data = new CData();
        data.LastSelectImageName = "wpf-mask-drag-performance";
        data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.LimeGreen });
        CGlobal.Inst.Data = data;

        var imageSize = new Size(512, 512);
        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        Bitmap bitmap = new Bitmap(imageSize.Width, imageSize.Height);
        try
        {
            SetPrivateField(window, "activeImageSize", imageSize);
            SetPrivateField(window, "activeImageBitmap", bitmap);
            SetPrivateField(window.MainCanvasViewModel, "_imageSize", imageSize);

            var objectReviewPanel = (WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl");
            System.Collections.IList undoHistory = GetPrivateField<System.Collections.IList>(window, "undoAnnotationHistory");
            InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Brush);
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(20, 20), PointF.Empty));

            int objectRowsBeforeDrag = objectReviewPanel.ViewModel.Objects.Count;
            int objectCollectionChangedDuringDrag = 0;
            ((INotifyCollectionChanged)objectReviewPanel.ViewModel.Objects).CollectionChanged += (_, _) => objectCollectionChangedDuringDrag++;

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int index = 0; index < 1_000; index++)
            {
                var point = new Point(20 + (index % 220), 20 + ((index / 220) * 8));
                InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, point, PointF.Empty));
            }

            stopwatch.Stop();
            var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
            int undoCountDuringDrag = undoHistory.Count;
            int objectRowsDuringDrag = objectReviewPanel.ViewModel.Objects.Count;
            int collectionChangesBeforeRelease = objectCollectionChangedDuringDrag;
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(240, 52), PointF.Empty));
            int undoCountAfterRelease = undoHistory.Count;
            int objectRowsAfterRelease = objectReviewPanel.ViewModel.Objects.Count;
            int collectionChangesAfterRelease = objectCollectionChangedDuringDrag;

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"WPF_MASK_BRUSH_DRAG_1000_MOVE_MS={stopwatch.Elapsed.TotalMilliseconds:F3} SEGMENTS={manualSegments.Count} UNDO_DURING={undoCountDuringDrag} UNDO_AFTER={undoCountAfterRelease} OBJECT_ROWS_BEFORE={objectRowsBeforeDrag} OBJECT_ROWS_DURING={objectRowsDuringDrag} OBJECT_ROWS_AFTER={objectRowsAfterRelease} COLLECTION_CHANGED_DURING={collectionChangesBeforeRelease} COLLECTION_CHANGED_AFTER={collectionChangesAfterRelease}"));

            AssertEqual(1, manualSegments.Count);
            AssertEqual(0, undoCountDuringDrag);
            AssertEqual(1, undoCountAfterRelease);
            AssertEqual(0, collectionChangesBeforeRelease);
            AssertTrue(collectionChangesAfterRelease > collectionChangesBeforeRelease, "mask brush drag should refresh object review on mouse-up, not during MouseMove");
            AssertTrue(objectRowsAfterRelease > 0, "mask brush drag should refresh the object review list once on mouse-up");
            AssertTrue(window.MainCanvasViewModel.MaskOverlays.Count == 1, "mask brush drag should keep one live raster mask overlay");
            AssertTrue(stopwatch.Elapsed.TotalMilliseconds < 1_000.0, "mask brush MouseMove should not capture history or rebuild side lists per event");
        }
        finally
        {
            SetPrivateField(window, "activeImageBitmap", null);
            window.Close();
            bitmap.Dispose();
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestWpfSegmentationObjectManipulationVerificationMatrix()
    {
        Size imageSize = new Size(40, 40);
        var defectClass = new CClassItem { Text = "Defect", DrawColor = Color.DeepSkyBlue };

        var polygon = new LabelingSegmentationObject(
            new[]
            {
                new Point(5, 5),
                new Point(20, 5),
                new Point(20, 20),
                new Point(5, 20)
            },
            defectClass)
        {
            ClassName = "Defect"
        };

        AssertTrue(WpfPolygonAnnotationService.IsPointInsidePolygon(polygon, new Point(10, 10)), "polygon interior hit-test should accept points inside the polygon body");
        AssertTrue(WpfPolygonAnnotationService.TryMovePolygon(polygon, 7, -3, imageSize, out Rectangle polygonMoveBounds), "polygon body drag should move the entire polygon");
        AssertEqual(new Rectangle(12, 2, 16, 16), polygon.Bounds);
        AssertEqual(new Point(12, 2), polygon.Points[0]);
        AssertTrue(polygonMoveBounds.Contains(new Point(5, 5)), "polygon move dirty bounds should include old geometry");
        AssertTrue(polygonMoveBounds.Contains(new Point(27, 17)), "polygon move dirty bounds should include new geometry");

        AssertTrue(WpfPolygonAnnotationService.TryMovePolygon(polygon, -100, -100, imageSize, out _), "polygon body drag should clamp at image bounds");
        AssertEqual(new Rectangle(0, 0, 16, 16), polygon.Bounds);
        AssertEqual(new Point(0, 0), polygon.Points[0]);

        AssertTrue(WpfPolygonAnnotationService.TryMovePoint(polygon, 2, new Point(39, 39), imageSize, out Rectangle pointMoveBounds), "polygon point resize/edit should move one vertex");
        AssertEqual(new Point(39, 39), polygon.Points[2]);
        AssertTrue(pointMoveBounds.Contains(new Point(39, 39)), "polygon point move dirty bounds should include the moved vertex");

        var maskService = new WpfMaskAnnotationService();
        var segments = new List<LabelingSegmentationObject>();
        AssertTrue(maskService.Paint(
            segments,
            new[] { new Point(10, 10), new Point(16, 10) },
            3,
            imageSize,
            defectClass,
            out LabelingSegmentationObject mask,
            out Rectangle paintBounds), "mask matrix should create a raster mask");
        AssertEqual(1, segments.Count);
        AssertTrue(mask.IsRasterMask, "mask matrix should create a raster segment");
        AssertTrue(paintBounds.Contains(new Point(10, 10)), "mask paint dirty bounds should include brush center");

        Rectangle maskBoundsBeforeMove = mask.Bounds;
        AssertTrue(maskService.TryMoveRasterMask(mask, 4, 5, imageSize, out Rectangle maskMoveBounds), "mask matrix should move a selected raster mask");
        AssertEqual(new Rectangle(maskBoundsBeforeMove.X + 4, maskBoundsBeforeMove.Y + 5, maskBoundsBeforeMove.Width, maskBoundsBeforeMove.Height), mask.Bounds);
        AssertTrue(maskMoveBounds.Contains(maskBoundsBeforeMove.Location), "mask move dirty bounds should include the old mask area");

        AssertTrue(maskService.TryMoveRasterMask(mask, -100, -100, imageSize, out _), "mask matrix should clamp movement at image bounds");
        AssertEqual(0, mask.Bounds.Left);
        AssertEqual(0, mask.Bounds.Top);
        AssertEqual(maskBoundsBeforeMove.Width, mask.Bounds.Width);
        AssertEqual(maskBoundsBeforeMove.Height, mask.Bounds.Height);

        Point maskCenter = new Point(mask.Bounds.Left + (mask.Bounds.Width / 2), mask.Bounds.Top + (mask.Bounds.Height / 2));
        AssertTrue(maskService.Erase(segments, new[] { maskCenter }, 40, imageSize, out _), "mask matrix should erase raster mask pixels");
        AssertEqual(0, segments.Count);
    }

    private static void TestWpfSegmentationObjectManipulationUpdatesShellState()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        var data = new CData();
        data.LastSelectImageName = "wpf-segmentation-object-shell";
        data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.DeepSkyBlue });
        CGlobal.Inst.Data = data;

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        Bitmap bitmap = new Bitmap(50, 50);
        try
        {
            SetPrivateField(window, "activeImageSize", new Size(50, 50));
            SetPrivateField(window, "activeImageBitmap", bitmap);
            SetPrivateField(window.MainCanvasViewModel, "_imageSize", new Size(50, 50));

            var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
            var polygon = new LabelingSegmentationObject(
                new[]
                {
                    new Point(10, 10),
                    new Point(25, 10),
                    new Point(25, 25),
                    new Point(10, 25)
                },
                data.ClassNamedList[0])
            {
                ClassName = "Defect"
            };
            manualSegments.Add(polygon);
            InvokePrivateResult<object>(window, "RefreshObjectList");
            InvokePrivateResult<object>(window, "RefreshPolygonOverlays");

            var objectReviewPanel = (WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl");
            objectReviewPanel.ViewModel.SelectedObject = objectReviewPanel.ViewModel.Objects.First(item => item.DisplayText.Contains("Polygon", StringComparison.Ordinal));
            SetPrivateField(window, "activeAnnotationTool", WpfAnnotationTool.Select);
            window.MainCanvasViewModel.IsImagePointInputMode = true;
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(16, 16), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(20, 19), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(20, 19), PointF.Empty));
            AssertEqual(new Rectangle(14, 13, 16, 16), polygon.Bounds);
            AssertEqual(new Point(14, 13), polygon.Points[0]);
            Dictionary<string, List<LabelingSegmentationObject>> afterPolygonMove = InvokePrivateResult<Dictionary<string, List<LabelingSegmentationObject>>>(window, "BuildAnnotationSegments");
            AssertEqual(new Rectangle(14, 13, 16, 16), afterPolygonMove["Defect"].First(segment => !segment.IsRasterMask).Bounds);

            InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Brush);
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(30, 30), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(34, 30), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(34, 30), PointF.Empty));
            LabelingSegmentationObject mask = manualSegments.First(segment => segment.IsRasterMask);
            Rectangle maskBoundsBeforeMove = mask.Bounds;

            objectReviewPanel.ViewModel.SelectedObject = objectReviewPanel.ViewModel.Objects.First(item => item.DisplayText.Contains("Mask", StringComparison.Ordinal));
            SetPrivateField(window, "activeAnnotationTool", WpfAnnotationTool.Select);
            window.MainCanvasViewModel.IsImagePointInputMode = true;
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(32, 30), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointMoved", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(35, 32), PointF.Empty));
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointReleased", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(35, 32), PointF.Empty));
            AssertEqual(new Rectangle(maskBoundsBeforeMove.X + 3, maskBoundsBeforeMove.Y + 2, maskBoundsBeforeMove.Width, maskBoundsBeforeMove.Height), mask.Bounds);
            Dictionary<string, List<LabelingSegmentationObject>> afterMaskMove = InvokePrivateResult<Dictionary<string, List<LabelingSegmentationObject>>>(window, "BuildAnnotationSegments");
            AssertTrue(afterMaskMove["Defect"].Any(segment => segment.IsRasterMask && segment.Bounds == mask.Bounds), "moved mask should be exported with the edited geometry");
            AssertTrue(window.MainCanvasViewModel.MaskOverlays.Any(overlay => overlay.IsSelected && overlay.Bounds == mask.Bounds), "moved mask should remain selected on the OpenGL overlay");
        }
        finally
        {
            SetPrivateField(window, "activeImageBitmap", null);
            window.Close();
            bitmap.Dispose();
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestWpfAnnotationHistoryService()
    {
        var rois = new List<Rectangle> { new Rectangle(1, 2, 3, 4) };
        var classNames = new List<string> { "OK" };
        var shapeKinds = new List<CanvasRoiShapeKind> { CanvasRoiShapeKind.Ellipse };
        var segment = new LabelingSegmentationObject(new[] { new Point(1, 1), new Point(5, 1), new Point(5, 5) }, new CClassItem { Text = "Defect", DrawColor = Color.Red })
        {
            MaskData = new byte[16],
            MaskSize = new Size(4, 4),
            MaskBounds = new Rectangle(1, 1, 2, 2)
        };
        segment.MaskData[5] = 1;
        var pending = new List<YoloWorkerSmokeCandidate>
        {
            new YoloWorkerSmokeCandidate { Index = 1, ClassName = "NG", Confidence = 0.9, X = 7, Y = 8, Width = 9, Height = 10 }
        };
        var confirmed = new List<YoloWorkerSmokeCandidate>
        {
            new YoloWorkerSmokeCandidate { Index = 2, ClassName = "OK", Confidence = 0.8, X = 11, Y = 12, Width = 13, Height = 14 }
        };

        WpfAnnotationHistorySnapshot snapshot = WpfAnnotationHistoryService.Capture(
            "test",
            rois,
            classNames,
            shapeKinds,
            new[] { segment },
            pending,
            confirmed);

        rois.Clear();
        classNames[0] = "Changed";
        shapeKinds[0] = CanvasRoiShapeKind.Rectangle;
        segment.MaskData[5] = 0;
        pending[0].ClassName = "Changed";

        var restoredRois = new List<Rectangle>();
        var restoredClassNames = new List<string>();
        var restoredShapeKinds = new List<CanvasRoiShapeKind>();
        var restoredOverlayIds = new List<string> { "old" };
        var restoredSegments = new List<LabelingSegmentationObject>();
        var restoredPending = new List<YoloWorkerSmokeCandidate>();
        var restoredConfirmed = new List<YoloWorkerSmokeCandidate>();

        WpfAnnotationHistoryService.Restore(
            snapshot,
            restoredRois,
            restoredClassNames,
            restoredShapeKinds,
            restoredOverlayIds,
            restoredSegments,
            restoredPending,
            restoredConfirmed);

        AssertEqual(new Rectangle(1, 2, 3, 4), restoredRois[0]);
        AssertEqual("OK", restoredClassNames[0]);
        AssertEqual(CanvasRoiShapeKind.Ellipse, restoredShapeKinds[0]);
        AssertEqual(0, restoredOverlayIds.Count);
        AssertTrue(restoredSegments[0].IsRasterMask, "history should restore raster mask metadata");
        AssertEqual((byte)1, restoredSegments[0].MaskData[5]);
        AssertTrue(!ReferenceEquals(segment, restoredSegments[0]), "segment snapshot should be a deep copy");
        AssertEqual("NG", restoredPending[0].ClassName);
        AssertEqual("OK", restoredConfirmed[0].ClassName);
        AssertTrue(!ReferenceEquals(pending[0], restoredPending[0]), "candidate snapshot should be a deep copy");
    }

    private static void TestWpfShellUndoRedoRestoresAnnotationState()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();
        var data = new CData();
        data.LastSelectImageName = "wpf-undo-redo";
        data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.LimeGreen });
        data.ConfigureOutputRoot(root);
        data.ProjectSettings.YoloDataset.ValidationPercent = 0;
        CGlobal.Inst.Data = data;

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        Bitmap bitmap = new Bitmap(40, 40);
        try
        {
            var learningPanel = (WpfLearningWorkflowPanel)window.FindName("LearningWorkflowPanelControl");
            WpfAnnotationToolItem undoTool = learningPanel.ViewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Undo);
            WpfAnnotationToolItem redoTool = learningPanel.ViewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Redo);
            var statusPanel = (WpfStatusBarPanel)window.FindName("StatusBarPanelControl");
            var saveStatusText = (System.Windows.Controls.TextBlock)window.FindName("AnnotationSaveStatusText");
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(!statusPanel.ViewModel.IsAnnotationDirty, "annotation save state should start clean before any WPF edit");
            AssertTrue(!undoTool.IsActionEnabled, "undo should start disabled before any WPF edit");
            AssertTrue(!redoTool.IsActionEnabled, "redo should start disabled before an undo action");
            AssertEqual("\uC5C6\uC74C", undoTool.DisplayCapabilityText);
            AssertEqual("\uC5C6\uC74C", redoTool.DisplayCapabilityText);

            SetPrivateField(window, "activeImageSize", new Size(40, 40));
            SetPrivateField(window, "activeImageBitmap", bitmap);

            var rect = new CanvasRect<float>(5, 30, 20, 15)
            {
                UniqueId = "undo-roi-1",
                ShapeKind = CanvasRoiShapeKind.Rectangle
            };
            var args = new RoiChangedEventArgs { RoiRect = rect };
            InvokePrivateResult<object>(window, "MainCanvasViewModel_RoiAdded", window.MainCanvasViewModel, args);

            var manualRois = GetPrivateField<List<Rectangle>>(window, "manualRois");
            AssertEqual(1, manualRois.Count);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(statusPanel.ViewModel.IsAnnotationDirty, "ROI add should mark annotation view model as dirty");
            AssertTrue(saveStatusText.Text.Contains("\uC800\uC7A5 \uD544\uC694", StringComparison.Ordinal), "ROI add should mark annotations as needing save");
            AssertTrue(undoTool.IsActionEnabled, "ROI add should enable undo in the guide palette");
            AssertTrue(!redoTool.IsActionEnabled, "redo should stay disabled until an undo action exists");
            AssertEqual("\uAC00\uB2A5", undoTool.DisplayCapabilityText);

            object[] saveArgs = { 0 };
            AssertTrue(InvokePrivateResult<bool>(window, "SaveCurrentAnnotations", saveArgs), "ROI add should be saveable");
            AssertEqual(1, (int)saveArgs[0]);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(!statusPanel.ViewModel.IsAnnotationDirty, "successful save should mark annotation view model as clean");
            AssertTrue(saveStatusText.Text.Contains("\uC800\uC7A5\uB428", StringComparison.Ordinal), "successful save should mark annotations as saved");

            AssertTrue(InvokePrivateResult<bool>(window, "UndoWpfAnnotationHistory"), "ROI add should be undoable");
            AssertEqual(0, manualRois.Count);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(statusPanel.ViewModel.IsAnnotationDirty, "undo after save should mark annotation view model as dirty");
            AssertTrue(saveStatusText.Text.Contains("\uC800\uC7A5 \uD544\uC694", StringComparison.Ordinal), "undo after save should mark annotations as needing save");
            AssertTrue(!undoTool.IsActionEnabled, "undo should disable after consuming the only history entry");
            AssertTrue(redoTool.IsActionEnabled, "undo should enable redo in the guide palette");
            AssertTrue(InvokePrivateResult<bool>(window, "RedoWpfAnnotationHistory"), "ROI add should be redoable");
            AssertEqual(1, manualRois.Count);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(saveStatusText.Text.Contains("\uC800\uC7A5 \uD544\uC694", StringComparison.Ordinal), "redo should keep annotations marked as needing save");
            AssertTrue(undoTool.IsActionEnabled, "redo should restore undo availability");
            AssertTrue(!redoTool.IsActionEnabled, "redo should disable after reapplying the only redo entry");

            InvokePrivateResult<object>(window, "BeginMaskAnnotationMode", WpfAnnotationTool.Brush);
            InvokePrivateResult<object>(window, "MainCanvasViewModel_ImagePointClicked", window.MainCanvasViewModel, new CanvasImagePointEventArgs(CanvasPointerButton.Left, 1, 0, 0, new Point(10, 10), PointF.Empty));

            var manualSegments = GetPrivateField<List<LabelingSegmentationObject>>(window, "manualSegments");
            AssertEqual(1, manualSegments.Count);
            AssertTrue(manualSegments[0].IsRasterMask, "brush should create a mask before undo");
            AssertTrue(InvokePrivateResult<bool>(window, "UndoWpfAnnotationHistory"), "mask paint should be undoable");
            AssertEqual(0, manualSegments.Count);
            AssertTrue(InvokePrivateResult<bool>(window, "RedoWpfAnnotationHistory"), "mask paint should be redoable");
            AssertEqual(1, manualSegments.Count);
            AssertTrue(manualSegments[0].IsRasterMask, "redo should restore a raster mask");

            LearningWorkflowViewModelSelectTool(window, WpfAnnotationTool.Undo);
            AssertEqual(0, manualSegments.Count);
            LearningWorkflowViewModelSelectTool(window, WpfAnnotationTool.Redo);
            AssertEqual(1, manualSegments.Count);
        }
        finally
        {
            SetPrivateField(window, "activeImageBitmap", null);
            window.Close();
            bitmap.Dispose();
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void LearningWorkflowViewModelSelectTool(WpfLabelingShellWindow window, WpfAnnotationTool tool)
    {
        var learningPanel = (WpfLearningWorkflowPanel)window.FindName("LearningWorkflowPanelControl");
        learningPanel.ToolList.SelectedItem = learningPanel.ViewModel.AnnotationTools.First(item => item.Tool == tool);
        PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
    }

    private static void TestLearningProtocol()
    {
        byte[] packet = LearningProtocol.BuildTrainingPacket(
            "StartTraining",
            "640",
            "8",
            "100",
            @"models\yolov5m.yaml",
            @"weights\yolov5m.pt",
            @"C:\데이터\data.yaml");

        string text = Encoding.UTF8.GetString(packet);
        string[] parts = text.Split(LearningProtocol.PacketSeparator);
        AssertEqual(2, parts.Length);
        AssertEqual("StartTraining", parts[0]);

        var request = JsonConvert.DeserializeObject<YoloTrainingRequest>(parts[1]);
        AssertEqual("models/yolov5m.yaml", request.cfg);
        AssertEqual("weights/yolov5m.pt", request.weight);
        string healthJson = Encoding.UTF8.GetString(LearningProtocol.BuildHealthCheckPacket("req-health")).Trim();
        AssertTrue(healthJson.Contains("\"type\":\"HealthCheck\""), "health check packet should use JSON lines protocol");
        AssertTrue(healthJson.Contains("\"requestId\":\"req-health\""), "health check packet should include requestId");

        string modelJson = Encoding.UTF8.GetString(LearningProtocol.BuildModelStatusPacket("req-model", ensureLoaded: true)).Trim();
        AssertTrue(modelJson.Contains("\"type\":\"ModelStatus\""), "model status packet should use JSON lines protocol");
        AssertTrue(modelJson.Contains("\"ensureLoaded\":true"), "model status packet should request model loading");

        string detectJson = Encoding.UTF8.GetString(LearningProtocol.BuildDetectImagePacket(
            "req-detect",
            "image-001",
            @"C:\images\part.png",
            0.25F)).Trim();
        AssertTrue(detectJson.Contains("\"type\":\"DetectImage\""), "detect image packet should use DetectImage type");
        AssertTrue(detectJson.Contains("\"requestId\":\"req-detect\""), "detect image packet should include requestId");
        AssertTrue(detectJson.Contains("\"imageId\":\"image-001\""), "detect image packet should include imageId");
        AssertTrue(detectJson.Contains("\"imagePath\":\"C:\\\\images\\\\part.png\""), "detect image packet should include imagePath");
        AssertEqual("C:/데이터/data.yaml", request.dataYaml);
    }

    private static void TestLearningCommunicationDeferredStart()
    {
        using var communication = new CCommunicationLearning(startListen: false);
        PythonCommunicationStatus status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "deferred communication should not start the TCP listener");
        AssertTrue(!status.IsClientConnected, "deferred communication should not report a connected client");

        using var bitmap = new Bitmap(4, 4);
        AssertTrue(!communication.SendData(CCommunicationLearning.CommandLearning.StartDefect.ToString(), bitmap), "image packet send should fail without a connected Python client");
        AssertTrue(!communication.SendTrainingData(CCommunicationLearning.CommandLearning.StartTraining.ToString(), "320", "1", "1", "yolov5m.yaml", "best.pt", "data.yaml"), "training packet send should fail without a connected Python client");

        communication.Close();
        status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed communication should not report a listening TCP listener");
        AssertEqual("stopped", status.LastWorkerState);
        AssertEqual("stopped", status.LastModelState);
        AssertTrue(!status.LastModelLoaded, "closed communication should clear model loaded state");
    }

    private static void TestLearningCommunicationCloseDuringAccept()
    {
        int port = GetAvailableTcpPort();
        using var communication = new CCommunicationLearning(startListen: true, port: port);
        using var client = new TcpClient();

        Task connectTask = client.ConnectAsync(IPAddress.Loopback, port);
        Thread.Sleep(20);
        communication.Close();

        try
        {
            connectTask.Wait(1000);
        }
        catch
        {
            // The connection can legitimately fail if Close wins the race.
        }

        Thread.Sleep(100);
        PythonCommunicationStatus status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed communication should not report a listening TCP listener");
        AssertTrue(!status.IsClientConnected, "closed communication should not report a connected client");
        communication.Close();
    }

    private static void TestLearningCommunicationRestartAfterClose()
    {
        int port = GetAvailableTcpPort();
        using var communication = new CCommunicationLearning(startListen: false, port: port);

        AssertTrue(communication.Start(), "initial TCP listener did not start");
        PythonCommunicationStatus status = communication.GetStatusSnapshot();
        AssertTrue(status.IsListening, "initial communication should report a listening TCP listener");
        AssertEqual("listening", status.LastWorkerState);

        communication.Close();
        status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed communication should not report a listening TCP listener before restart");
        AssertEqual("stopped", status.LastWorkerState);

        AssertTrue(WaitUntil(() => communication.Start(), TimeSpan.FromSeconds(2)), "TCP listener did not restart after close");

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        AssertTrue(WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(2)), "client did not connect to restarted listener");

        communication.Close();
        status = communication.GetStatusSnapshot();
        AssertTrue(!status.IsListening, "closed restarted communication should not report a listening TCP listener");
        AssertTrue(!status.IsClientConnected, "closed restarted communication should not report a connected client");
    }

    private static void TestLearningCommunicationDetectionRoundTrip()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        int port = GetAvailableTcpPort();
        string root = CreateTempRoot();
        DetectionResultApplicationService service = CGlobal.Inst.DetectionResults;
        EventHandler<DetectionCandidatesUpdatedEventArgs> candidateHandler = null;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        using var communication = new CCommunicationLearning(startListen: false, port: port);
        try
        {
            AssertTrue(communication.Start(), "test TCP listener did not start");

            var data = new CData
            {
                LastSelectImageName = "tcp-round-trip",
                LastSelectImagePath = Path.Combine(Path.GetTempPath(), "tcp-round-trip.bmp")
            };
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ProjectSettings.PythonModel.WeightsPath = Path.Combine(root, "best.pt");
            File.WriteAllText(data.ProjectSettings.PythonModel.WeightsPath, "");
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.Red });
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);
            service.ApplyToDetectLayer(Array.Empty<DefectInfo>());

            using var receivedRequest = new ManualResetEventSlim(false);
            Task mockClient = Task.Run(() => RunMockDetectionClient(port, receivedRequest));
            AssertTrue(WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(5)), "mock client did not connect to listener");

            int candidateEventCount = -1;
            candidateHandler = (_, e) => Volatile.Write(ref candidateEventCount, e.CandidateCount);
            service.DetectionCandidatesUpdated += candidateHandler;

            AssertTrue(service.TrySendCurrentImageForDetection(communication), "StartDefect packet was not sent");
            AssertTrue(receivedRequest.Wait(TimeSpan.FromSeconds(5)), "mock client did not receive StartDefect");
            AssertTrue(WaitUntil(() => service.GetLastDefects().Count == 1, TimeSpan.FromSeconds(5)), "ResultDefect was not applied");
            AssertTrue(WaitUntil(() => candidateEventCount == 1, TimeSpan.FromSeconds(2)), "detection candidate event was not raised");

            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data, minimumConfidence: 0.5F);
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();

            AssertEqual(1, candidates.Count);
            AssertEqual("NG", candidates[0].ClassName);
            AssertTrue(candidates[0].IsConfirmable, "mock detection candidate should be confirmable");
            AssertEqual(1, mainDisplay.GetDetectionOverlays().Count);
            AssertTrue(service.CanCommitLastDetection(data, minimumConfidence: 0.5F), "TCP detection result should be confirmable");
            AssertTrue(service.SelectDetectionCandidate(1, data), "TCP detection candidate was not selectable");
            AssertTrue(service.CommitLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F), "TCP detection candidate was not confirmed");
            AssertTrue(WaitUntil(() => service.GetLastDefects().Count == 0, TimeSpan.FromSeconds(2)), "confirmed TCP detection result was not cleared");

            IReadOnlyList<LabelingRoiListItem> committedItems = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "tcp-round-trip.txt");
            AssertEqual(1, committedItems.Count);
            AssertEqual("NG", committedItems[0].ClassName);
            AssertEqual(new Rectangle(4, 5, 12, 9), committedItems[0].Roi);
            AssertEqual(0, mainDisplay.GetDetectionOverlays().Count);
            AssertTrue(File.Exists(labelPath), "TCP detection label file was not saved");
            AssertEqual(1, File.ReadAllLines(labelPath).Length);
            AssertTrue(mockClient.Wait(TimeSpan.FromSeconds(5)), "mock client did not finish");
            if (mockClient.IsFaulted)
            {
                if (mockClient.Exception != null)
                {
                    throw mockClient.Exception;
                }

                throw new InvalidOperationException("mock client failed");
            }
        }
        finally
        {
            if (candidateHandler != null)
            {
                service.DetectionCandidatesUpdated -= candidateHandler;
            }

            CGlobal.Inst.DetectionResults.ApplyToDetectLayer(Array.Empty<DefectInfo>());
            communication.Close();
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestRealYoloDetectionWorkflowSmoke()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        int port = GetAvailableTcpPort();
        int timeoutSeconds = GetEnvironmentInt("LABELING_SMOKE_TIMEOUT_SECONDS", 240);
        string artifactRoot = CreateRealYoloSmokeArtifactRoot();
        string datasetRoot = Path.Combine(artifactRoot, "dataset");
        Directory.CreateDirectory(datasetRoot);

        RealYoloSmokeSettings smokeSettings = BuildRealYoloSmokeSettings(artifactRoot);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        DetectionResultApplicationService service = CGlobal.Inst.DetectionResults;
        EventHandler<DetectionCandidatesUpdatedEventArgs> candidateHandler = null;
        Process pythonProcess = null;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        using var communication = new CCommunicationLearning(startListen: false, port: port);
        try
        {
            AssertRealYoloSmokeInputs(smokeSettings);
            AssertTrue(communication.Start(), "real YOLO smoke TCP listener did not start");

            var data = new CData
            {
                LastSelectImageName = Path.GetFileNameWithoutExtension(smokeSettings.ImagePath),
                LastSelectImagePath = smokeSettings.ImagePath
            };
            data.ConfigureOutputRoot(datasetRoot);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ProjectSettings.PythonModel.PythonExecutablePath = smokeSettings.PythonExecutablePath;
            data.ProjectSettings.PythonModel.ProjectRootPath = smokeSettings.ProjectRootPath;
            data.ProjectSettings.PythonModel.ClientScriptPath = smokeSettings.ClientScriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = smokeSettings.WeightsPath;
            data.ProjectSettings.PythonModel.ImageRootPath = smokeSettings.ImageRootPath;
            data.ProjectSettings.PythonModel.MinimumDetectionConfidence = smokeSettings.MinimumConfidence;
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.LimeGreen });
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.OrangeRed });
            CGlobal.Inst.Data = data;

            using CvMat sourceMat = OpenCvSharp.Cv2.ImRead(smokeSettings.ImagePath);
            AssertTrue(sourceMat != null && !sourceMat.Empty(), $"smoke image could not be loaded by OpenCV: {smokeSettings.ImagePath}");
            CDisplayManager.ImageSrc = sourceMat.Clone();

            using var image = new Bitmap(smokeSettings.ImagePath);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);

            int candidateEventCount = -1;
            candidateHandler = (_, e) => candidateEventCount = e.CandidateCount;
            service.DetectionCandidatesUpdated += candidateHandler;

            pythonProcess = StartRealYoloClient(smokeSettings, port, timeoutSeconds, stdout, stderr);
            bool started = CGlobal.Inst.DetectionWorkflow.TryStartCurrentImageDetection(
                data,
                communication,
                service,
                () => WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(timeoutSeconds)));

            AssertTrue(started, BuildRealYoloSmokeFailure("real YOLO StartDefect request was not sent", stdout, stderr));
            AssertTrue(
                WaitUntil(
                    () =>
                        Volatile.Read(ref candidateEventCount) > 0 &&
                        service.GetLastCandidateReviewItems(data, smokeSettings.MinimumConfidence).Count > 0,
                    TimeSpan.FromSeconds(timeoutSeconds)),
                BuildRealYoloSmokeFailure($"real YOLO ResultDefect did not produce candidates and event. EventCount:{Volatile.Read(ref candidateEventCount)}", stdout, stderr));

            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data, smokeSettings.MinimumConfidence);
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            AssertTrue(mainDisplay != null, "Main display was not available after real YOLO detection");
            AssertTrue(mainDisplay.GetDetectionOverlays().Count > 0, "real YOLO candidates were not rendered as OpenGL overlays");
            AssertTrue(service.CanCommitLastDetection(data, smokeSettings.MinimumConfidence), "real YOLO candidates were not confirmable");
            AssertTrue(service.CommitAllLastDetectionToMainLabels(data, new CSystem(), smokeSettings.MinimumConfidence), "real YOLO candidates were not confirmed into labels");
            AssertTrue(WaitUntil(() => service.GetLastDefects().Count == 0, TimeSpan.FromSeconds(2)), "confirmed real YOLO detection result was not cleared");
            AssertEqual(0, mainDisplay.GetDetectionOverlays().Count);

            IReadOnlyList<LabelingRoiListItem> committedItems = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(datasetRoot, "data", "train", "labels", $"{data.LastSelectImageName}.txt");
            AssertTrue(committedItems.Count > 0, "real YOLO confirmed candidates did not create Main ROI labels");
            AssertTrue(File.Exists(labelPath), $"real YOLO label file was not saved: {labelPath}");
            AssertTrue(File.ReadAllLines(labelPath).Length > 0, $"real YOLO label file was empty: {labelPath}");
            string reviewStatusPath = VerifyRealYoloReviewStatus(data, smokeSettings.ImagePath, image.Size);

            WriteRealYoloSmokeSummary(artifactRoot, smokeSettings, candidates, committedItems, labelPath, reviewStatusPath, stdout, stderr);
            AssertRealYoloClientExitedCleanly(pythonProcess, stdout, stderr);
        }
        finally
        {
            if (candidateHandler != null)
            {
                service.DetectionCandidatesUpdated -= candidateHandler;
            }

            StopRealYoloClient(pythonProcess);
            WriteRealYoloProcessLog(artifactRoot, stdout, stderr);
            CGlobal.Inst.DetectionResults.CancelPendingDetection();
            communication.Close();
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestPythonModelSettingsDefaults()
    {
        var settings = new PythonModelSettings
        {
            ProjectRootPath = "",
            ClientScriptPath = "",
            WeightsPath = ""
        };

        settings.EnsureDefaults();

        AssertEqual(PythonModelSettings.GetDefaultProjectRootPath(), settings.ProjectRootPath);
        AssertEqual(Path.Combine(settings.ProjectRootPath, "labelling_tcp_client.py"), settings.ClientScriptPath);
        AssertEqual(Path.Combine(settings.ProjectRootPath, "best.pt"), settings.WeightsPath);
        AssertEqual(PythonModelSettings.GetDefaultImageRootPath(), settings.ImageRootPath);
        AssertEqual(0.25F, settings.MinimumDetectionConfidence);
        AssertEqual(20, settings.MaximumDetectionCandidates);
        AssertEqual(320, settings.InferenceImageSize);
        AssertEqual(30, settings.DetectionTimeoutSeconds);
        AssertTrue(settings.AutoStartClient, "YOLOv5 client auto-start should be enabled by default");

        var projectSettings = new LabelingProjectSettings
        {
            YoloDataset = null,
            Training = null,
            PythonModel = null,
            TrainingGuide = null
        };
        projectSettings.EnsureDefaults();
        AssertTrue(projectSettings.TrainingGuide != null, "project settings should include YOLO training guide history");
        AssertTrue(projectSettings.TrainingGuide.RunHistory != null, "YOLO training guide history should include a run-history list");
        projectSettings.TrainingGuide.LastDatasetIssueKind = "Labels";
        projectSettings.TrainingGuide.AppliedWeightsPath = Path.Combine(settings.ProjectRootPath, "best.pt");
        projectSettings.TrainingGuide.AppliedWeightsSavedToRecipe = false;
        projectSettings.TrainingGuide.RunHistory.Add(new YoloTrainingGuideRunRecord
        {
            EventKind = "Weight",
            AppliedWeightsPath = projectSettings.TrainingGuide.AppliedWeightsPath
        });
        AssertEqual("Labels", projectSettings.TrainingGuide.LastDatasetIssueKind);
        AssertTrue(!projectSettings.TrainingGuide.AppliedWeightsSavedToRecipe, "newly applied training weights should start as not saved to recipe");
        AssertEqual(1, projectSettings.TrainingGuide.RunHistory.Count);
    }

    private static void TestPythonModelSettingsRepairsStaleRoots()
    {
        string defaultProjectRoot = PythonModelSettings.GetDefaultProjectRootPath();
        string defaultScriptPath = Path.Combine(defaultProjectRoot, "labelling_tcp_client.py");
        string defaultWeightsPath = Path.Combine(defaultProjectRoot, "best.pt");
        if (!File.Exists(defaultScriptPath) || !File.Exists(defaultWeightsPath))
        {
            return;
        }

        string root = CreateTempRoot();
        try
        {
            string staleRoot = Path.Combine(root, "py");
            Directory.CreateDirectory(staleRoot);
            var settings = new PythonModelSettings
            {
                ProjectRootPath = staleRoot,
                ClientScriptPath = Path.Combine(staleRoot, "labelling_tcp_client.py"),
                WeightsPath = Path.Combine(staleRoot, "best.pt"),
                ImageRootPath = Path.Combine(staleRoot, "missing-images")
            };

            settings.EnsureDefaults();

            AssertEqual(defaultProjectRoot, settings.ProjectRootPath);
            AssertEqual(defaultScriptPath, settings.ClientScriptPath);
            AssertEqual(defaultWeightsPath, settings.WeightsPath);
            AssertTrue(
                string.IsNullOrWhiteSpace(settings.ImageRootPath) || Directory.Exists(settings.ImageRootPath),
                "image root should be empty or point at an existing directory after repair");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestRuntimeExampleConfigUsesPortableSiblingYoloPaths()
    {
        string repoRoot = FindRepositoryRoot();
        string configText = File.ReadAllText(Path.Combine(repoRoot, "config", "labeling-runtime.example.json"));
        string startScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "start-labeling-workbench.ps1"));
        string tcpSmokeScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "smoke-yolo-tcp.ps1"));
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "verify-first-run.ps1"));

        AssertTrue(configText.Contains("${repoParent}/yolov5", StringComparison.Ordinal), "runtime example config should use the sibling yolov5 token");
        AssertTrue(configText.Contains("\"imageSize\": 320", StringComparison.Ordinal), "runtime example config should set a fast default inference image size");
        AssertTrue(!configText.Contains("C:/Git/yolov5", StringComparison.OrdinalIgnoreCase), "runtime example config should not hard-code C:/Git/yolov5");
        AssertTrue(startScript.Contains("Expand-PathTokens", StringComparison.Ordinal), "launcher should resolve runtime config path tokens");
        AssertTrue(startScript.Contains("--img-size", StringComparison.Ordinal), "launcher should pass the configured YOLO inference image size");
        AssertTrue(startScript.Contains("-Preload", StringComparison.Ordinal), "launcher should preload the YOLO model before accepting inference work");
        AssertTrue(tcpSmokeScript.Contains("[int]$ImgSize = 320", StringComparison.Ordinal), "YOLO TCP smoke should use the fast default inference image size");
        AssertTrue(verifyScript.Contains("Expand-PathTokens", StringComparison.Ordinal), "first-run verifier should resolve runtime config path tokens");
    }

    private static void TestPythonModelSettingsValidator()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            File.WriteAllText(scriptPath, "print('ready')");

            var settings = new PythonModelSettings
            {
                ProjectRootPath = root,
                ClientScriptPath = scriptPath,
                WeightsPath = Path.Combine(root, "missing.pt")
            };

            PythonModelValidationResult trainingValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(trainingValidation.IsValid, trainingValidation.Summary);
            AssertTrue(trainingValidation.Warnings.Any(line => line.Contains("weight file")), "missing weights should be a training warning");

            PythonModelValidationResult detectionValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: true);
            AssertTrue(!detectionValidation.IsValid, "missing weights should block detection");
            AssertTrue(detectionValidation.Errors.Any(line => line.Contains("weight file")), "missing weights error was not reported");

            settings.MinimumDetectionConfidence = 1.2F;
            PythonModelValidationResult confidenceValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(!confidenceValidation.IsValid, "out-of-range confidence should be invalid");
            AssertTrue(confidenceValidation.Errors.Any(line => line.Contains("confidence")), "confidence error was not reported");

            settings.MinimumDetectionConfidence = 0.25F;
            settings.DetectionTimeoutSeconds = 0;
            PythonModelValidationResult timeoutValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(!timeoutValidation.IsValid, "out-of-range detection timeout should be invalid");
            AssertTrue(timeoutValidation.Errors.Any(line => line.Contains("timeout")), "detection timeout error was not reported");

            settings.DetectionTimeoutSeconds = 30;
            settings.MaximumDetectionCandidates = 0;
            PythonModelValidationResult maximumCandidatesValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(!maximumCandidatesValidation.IsValid, "out-of-range maximum candidates should be invalid");
            AssertTrue(maximumCandidatesValidation.Errors.Any(line => line.Contains("Maximum detection candidates")), "maximum candidates error was not reported");

            settings.MaximumDetectionCandidates = 20;
            settings.InferenceImageSize = 32;
            PythonModelValidationResult imageSizeValidation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            AssertTrue(!imageSizeValidation.IsValid, "out-of-range inference image size should be invalid");
            AssertTrue(imageSizeValidation.Errors.Any(line => line.Contains("Inference image size")), "inference image size error was not reported");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestPythonEnvironmentRequirementsParser()
    {
        string root = CreateTempRoot();
        try
        {
            string nestedRequirementsPath = Path.Combine(root, "extras.txt");
            File.WriteAllLines(nestedRequirementsPath, new[]
            {
                "opencv-python>=4.8",
                "git+https://example.invalid/repo.git#egg=custom_pkg"
            });

            string requirementsPath = Path.Combine(root, "requirements.txt");
            File.WriteAllLines(requirementsPath, new[]
            {
                "# YOLOv5 runtime dependencies",
                "numpy>=1.21.0",
                "PyYAML==6.0",
                "ultralytics[export]>=8.0; python_version >= '3.8'",
                "-r extras.txt",
                "--find-links https://example.invalid/packages"
            });

            IReadOnlyList<string> packageNames = PythonEnvironmentService.ReadRequirementPackageNames(requirementsPath);

            AssertTrue(packageNames.Contains("numpy"), "numpy requirement was not parsed");
            AssertTrue(packageNames.Contains("PyYAML"), "PyYAML requirement was not parsed");
            AssertTrue(packageNames.Contains("ultralytics"), "ultralytics extra requirement was not parsed");
            AssertTrue(packageNames.Contains("opencv-python"), "included opencv-python requirement was not parsed");
            AssertTrue(packageNames.Contains("custom_pkg"), "git egg requirement was not parsed");
            AssertTrue(!packageNames.Contains("--find-links"), "pip option should not be parsed as a package");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloWorkerSmokeTestServiceValidation()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            string weightsPath = Path.Combine(root, "best.pt");
            Directory.CreateDirectory(Path.Combine(root, "yolov5Master"));
            File.WriteAllText(scriptPath, "print('ready')");
            File.WriteAllText(weightsPath, "");
            File.WriteAllText(Path.Combine(root, "requirements.txt"), "torch");
            string explicitImagePath = Path.Combine(root, "explicit.png");
            using (var image = new Bitmap(4, 3))
            {
                image.Save(explicitImagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            var settings = new PythonModelSettings
            {
                ProjectRootPath = root,
                ClientScriptPath = Path.Combine(root, "missing-client.py"),
                WeightsPath = weightsPath,
                ImageRootPath = root,
                PythonExecutablePath = Path.Combine(root, "python.exe")
            };

            YoloWorkerSmokeTestResult result = YoloWorkerSmokeTestService
                .RunAsync(settings)
                .GetAwaiter()
                .GetResult();

            AssertTrue(!result.Succeeded, "smoke test should fail before starting Python when inputs are invalid");
            AssertTrue(
                result.Errors.Any(error => error.Contains("client script", StringComparison.OrdinalIgnoreCase)),
                "missing client script should be reported");

            YoloWorkerSmokeTestResult explicitImageResult = YoloWorkerSmokeTestService
                .RunAsync(settings, explicitImagePath)
                .GetAwaiter()
                .GetResult();

            AssertEqual(explicitImagePath, explicitImageResult.ImagePath);
            AssertTrue(
                !explicitImageResult.Errors.Any(error => error.Contains("Smoke test image", StringComparison.OrdinalIgnoreCase)),
                "explicit smoke image should be accepted during input validation");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloWorkerSmokeCandidateBounds()
    {
        var candidate = new YoloWorkerSmokeCandidate
        {
            ClassName = "OK",
            Confidence = 0.976,
            X = 24.1588,
            Y = 22.9412,
            Width = 55.9874,
            Height = 55.9470
        };

        AssertEqual(new Rectangle(24, 23, 56, 56), candidate.ToRectangle());

        candidate.Width = 0;
        AssertEqual(Rectangle.Empty, candidate.ToRectangle());
    }

    private static void TestYoloPythonClientStartInfo()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            string weightsPath = Path.Combine(root, "best.pt");
            string modelRootPath = Path.Combine(root, "yolov5Master");
            File.WriteAllText(scriptPath, "print('ready')");
            Directory.CreateDirectory(modelRootPath);

            var settings = new PythonModelSettings
            {
                ProjectRootPath = root,
                ClientScriptPath = scriptPath,
                WeightsPath = weightsPath,
                ImageRootPath = root,
                InferenceImageSize = 320,
                MinimumDetectionConfidence = 0.15F
            };

            AssertTrue(YoloPythonClientProcessService.TryCreateStartInfo(settings, out var startInfo, out string error), error);
            AssertEqual(root, startInfo.WorkingDirectory);
            AssertTrue(startInfo.CreateNoWindow, "Python client should start without opening a console window");
            AssertTrue(startInfo.ArgumentList.Contains(scriptPath), "client script was not passed to Python");
            AssertTrue(startInfo.ArgumentList.Contains("--retry"), "retry flag was not passed to Python");
            AssertTrue(startInfo.ArgumentList.Contains("--preload"), "preload flag should move model load into worker startup instead of the first image request");
            AssertTrue(startInfo.ArgumentList.Contains(weightsPath), "weights path was not passed to Python");
            AssertArgumentValue(startInfo.ArgumentList, "--model-root", modelRootPath);
            AssertArgumentValue(startInfo.ArgumentList, "--image-root", root);
            AssertArgumentValue(startInfo.ArgumentList, "--conf", "0.15");
            AssertArgumentValue(startInfo.ArgumentList, "--img-size", "320");

            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string firstSignature, out error), error);
            settings.MinimumDetectionConfidence = 0.2F;
            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string changedConfidenceSignature, out error), error);
            AssertTrue(
                !string.Equals(firstSignature, changedConfidenceSignature, StringComparison.Ordinal),
                "confidence changes should change Python client start signature");

            settings.MinimumDetectionConfidence = 0.15F;
            settings.InferenceImageSize = 416;
            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string changedImageSizeSignature, out error), error);
            AssertTrue(
                !string.Equals(firstSignature, changedImageSizeSignature, StringComparison.Ordinal),
                "inference image size changes should change Python client start signature");

            settings.InferenceImageSize = 320;
            settings.WeightsPath = Path.Combine(root, "other.pt");
            AssertTrue(YoloPythonClientProcessService.TryCreateStartSignature(settings, out string changedWeightsSignature, out error), error);
            AssertTrue(
                !string.Equals(firstSignature, changedWeightsSignature, StringComparison.Ordinal),
                "weight path changes should change Python client start signature");

            settings.ClientScriptPath = Path.Combine(root, "missing.py");
            AssertTrue(!YoloPythonClientProcessService.TryCreateStartInfo(settings, out _, out error), "missing client script was accepted");
            AssertTrue(error.Contains("not found"), "missing client script error was not descriptive");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloDetectionWorkflowValidation()
    {
        string root = CreateTempRoot();
        try
        {
            string scriptPath = Path.Combine(root, "labelling_tcp_client.py");
            string weightsPath = Path.Combine(root, "best.pt");
            File.WriteAllText(scriptPath, "print('ready')");

            var data = new CData();
            data.ProjectSettings.PythonModel.ProjectRootPath = root;
            data.ProjectSettings.PythonModel.ClientScriptPath = scriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = Path.Combine(root, "missing.pt");
            data.ProjectSettings.PythonModel.ImageRootPath = root;

            using var communication = new CCommunicationLearning(startListen: false);
            var workflow = new YoloDetectionWorkflowService();
            bool ensureCalled = false;
            bool invalidStarted = workflow.TryStartCurrentImageDetection(
                data,
                communication,
                new DetectionResultApplicationService(),
                () =>
                {
                    ensureCalled = true;
                    return true;
                });

            AssertTrue(!invalidStarted, "detection workflow should reject missing weights");
            AssertTrue(!ensureCalled, "Python client should not be prepared when detection settings are invalid");

            File.WriteAllText(weightsPath, "");
            data.ProjectSettings.PythonModel.WeightsPath = weightsPath;
            bool readyFailureStarted = workflow.TryStartCurrentImageDetection(
                data,
                communication,
                new DetectionResultApplicationService(),
                () =>
                {
                    ensureCalled = true;
                    return false;
                });

            AssertTrue(!readyFailureStarted, "detection workflow should stop when the Python client is not ready");
            AssertTrue(ensureCalled, "Python client readiness was not checked for valid settings");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestPythonMessageFramer()
    {
        var framer = new PythonMessageFramer();

        IReadOnlyList<string> first = framer.Append("ResultDefect [{\"ClassName\":\"NG\",");
        AssertEqual(0, first.Count);

        IReadOnlyList<string> second = framer.Append("\"Confidence\":0.9,\"X\":1,\"Y\":2,\"Width\":3,\"Height\":4}]");
        AssertEqual(1, second.Count);
        AssertTrue(second[0].StartsWith("ResultDefect ", StringComparison.Ordinal), "split ResultDefect was not rebuilt");

        DetectionResultParseResult result = PythonDetectionResultProtocol.Parse(second[0]);
        AssertEqual(DetectionResultParseStatus.Parsed, result.Status);
        AssertEqual("NG", result.Defects[0].ClassName);

        var separatedFramer = new PythonMessageFramer();
        IReadOnlyList<string> separated = separatedFramer.Append("ResultDefect\n\n[{\"ClassName\":\"OK\",\"Confidence\":0.8,\"X\":1,\"Y\":2,\"Width\":3,\"Height\":4}]");
        AssertEqual(1, separated.Count);
        AssertEqual(DetectionResultParseStatus.Parsed, PythonDetectionResultProtocol.Parse(separated[0]).Status);

        IReadOnlyList<string> commands = framer.Append("StartTraining\nStopTraining\n");
        AssertEqual(2, commands.Count);
        AssertEqual("StartTraining", commands[0]);
        AssertEqual("StopTraining", commands[1]);

        var envelopeFramer = new PythonMessageFramer();
        IReadOnlyList<string> envelopeFirst = envelopeFramer.Append("{\"type\":\"ResultDefect\",\"items\":[");
        AssertEqual(0, envelopeFirst.Count);

        IReadOnlyList<string> envelopeSecond = envelopeFramer.Append("{\"className\":\"OK\",\"confidence\":0.7,\"x\":1,\"y\":2,\"width\":3,\"height\":4}]}");
        AssertEqual(1, envelopeSecond.Count);
        AssertEqual(DetectionResultParseStatus.Parsed, PythonDetectionResultProtocol.Parse(envelopeSecond[0]).Status);
    }

    private static void TestPythonModelStatusProtocol()
    {
        string statusJson = "{\"type\":\"TrainingStatus\",\"version\":1,\"state\":\"running\",\"message\":\"epoch\",\"progressPercent\":42,\"epoch\":2,\"totalEpochs\":5}";

        PythonModelStatusParseResult result = PythonModelStatusProtocol.Parse(statusJson);

        AssertEqual(PythonModelStatusParseStatus.Parsed, result.Status);
        AssertEqual("running", result.Message.State);
        AssertEqual(42, result.Message.ProgressPercent.Value);
        AssertEqual(2, result.Message.Epoch.Value);
        AssertEqual(5, result.Message.TotalEpochs.Value);
        AssertTrue(!result.Message.IsError, "running status should not be an error");

        string trainResultJson = "{\"type\":\"TrainYoloResult\",\"version\":1,\"ok\":true,\"taskId\":\"task-1\",\"state\":\"started\"}";
        PythonModelStatusParseResult trainResult = PythonModelStatusProtocol.Parse(trainResultJson);
        AssertEqual(PythonModelStatusParseStatus.Parsed, trainResult.Status);
        AssertEqual(PythonModelStatusProtocol.TrainingStatusType, trainResult.Message.Type);
        AssertEqual("started", trainResult.Message.State);

        string taskStatusJson = "{\"type\":\"TaskStatus\",\"version\":1,\"taskType\":\"TrainYolo\",\"taskId\":\"task-1\",\"state\":\"completed\",\"message\":\"YOLOv5 training completed.\",\"progressPercent\":100}";
        PythonModelStatusParseResult taskStatus = PythonModelStatusProtocol.Parse(taskStatusJson);
        AssertEqual(PythonModelStatusParseStatus.Parsed, taskStatus.Status);
        AssertEqual(PythonModelStatusProtocol.TrainingStatusType, taskStatus.Message.Type);
        AssertEqual("completed", taskStatus.Message.State);
        AssertEqual(100, taskStatus.Message.ProgressPercent.Value);

        string healthJson = "{\"type\":\"HealthCheckResult\",\"version\":1,\"requestId\":\"req-health\",\"ok\":true,\"state\":\"ready\",\"worker\":{\"pid\":123}}";
        PythonModelStatusParseResult health = PythonModelStatusProtocol.Parse(healthJson);
        AssertEqual(PythonModelStatusParseStatus.Parsed, health.Status);
        AssertEqual(PythonModelStatusProtocol.HealthCheckResultType, health.Message.Type);
        AssertEqual("ready", health.Message.State);
        AssertTrue(health.Message.Ok == true, "health check ok state was not parsed");

        string modelJson = "{\"type\":\"ModelStatusResult\",\"version\":1,\"requestId\":\"req-model\",\"ok\":true,\"model\":{\"state\":\"ready\",\"loaded\":true,\"weightsPath\":\"C:/Git/yolov5/best.pt\"}}";
        PythonModelStatusParseResult model = PythonModelStatusProtocol.Parse(modelJson);
        AssertEqual(PythonModelStatusParseStatus.Parsed, model.Status);
        AssertEqual(PythonModelStatusProtocol.ModelStatusResultType, model.Message.Type);
        AssertEqual("ready", model.Message.State);
        AssertTrue(model.Message.Loaded == true, "model loaded state was not parsed");

        PythonModelStatusParseResult ignored = PythonModelStatusProtocol.Parse("{\"type\":\"Other\"}");
        AssertEqual(PythonModelStatusParseStatus.NotStatus, ignored.Status);
    }

    private static void TestTcpReceiveQueueEncoding()
    {
        var tcp = new CTCPAsync
        {
            TextEncoding = Encoding.UTF8
        };

        tcp.listRcvData.Add(Encoding.UTF8.GetBytes("class-\uB370\uC774\uD130"));

        AssertTrue(tcp.GetStringData(out string message), "queued string data was not returned");
        AssertEqual("class-\uB370\uC774\uD130", message);
        AssertTrue(!tcp.GetStringData(out string _), "queue should be empty after dequeue");
    }

    private static void TestPythonDetectionResultProtocol()
    {
        string message = "ResultDefect [{\"ClassName\":\"NG\",\"Confidence\":0.91,\"X\":12.5,\"Y\":20,\"Width\":30,\"Height\":40}]";

        DetectionResultParseResult result = PythonDetectionResultProtocol.Parse(message);

        AssertTrue(result.IsDetectionResult, "ResultDefect message was not recognized");
        AssertEqual(DetectionResultParseStatus.Parsed, result.Status);
        AssertEqual(1, result.Defects.Count);
        AssertEqual("NG", result.Defects[0].ClassName);
        AssertEqual(12.5F, result.Defects[0].X);
        AssertEqual(40F, result.Defects[0].Height);

        string envelope = "{\"type\":\"ResultDefect\",\"version\":1,\"imageId\":\"sample-001\",\"items\":[{\"className\":\"OK\",\"confidence\":0.82,\"x\":3,\"y\":4,\"width\":5,\"height\":6}]}";
        DetectionResultParseResult envelopeResult = PythonDetectionResultProtocol.Parse(envelope);

        AssertEqual(DetectionResultParseStatus.Parsed, envelopeResult.Status);
        AssertEqual(1, envelopeResult.Defects.Count);
        AssertEqual("OK", envelopeResult.Defects[0].ClassName);
        AssertEqual(5F, envelopeResult.Defects[0].Width);
        AssertEqual("sample-001", envelopeResult.ImageId);

        string detectResult = "{\"type\":\"DetectImageResult\",\"version\":1,\"requestId\":\"req-123\",\"imageId\":\"sample-002\",\"ok\":true,\"candidates\":[{\"className\":\"NG\",\"confidence\":0.72,\"x\":7,\"y\":8,\"width\":9,\"height\":10}]}";
        DetectionResultParseResult detectParse = PythonDetectionResultProtocol.Parse(detectResult);
        AssertEqual(DetectionResultParseStatus.Parsed, detectParse.Status);
        AssertEqual("req-123", detectParse.RequestId);
        AssertEqual("sample-002", detectParse.ImageId);
        AssertEqual("NG", detectParse.Defects[0].ClassName);
        AssertEqual(9F, detectParse.Defects[0].Width);
    }

    private static void TestPythonDetectionResultProtocolFailures()
    {
        DetectionResultParseResult ignored = PythonDetectionResultProtocol.Parse("StartTraining");
        AssertEqual(DetectionResultParseStatus.NotDetectionResult, ignored.Status);
        AssertTrue(!ignored.IsDetectionResult, "non-detection command was recognized as detection");

        DetectionResultParseResult empty = PythonDetectionResultProtocol.Parse("ResultDefect ");
        AssertEqual(DetectionResultParseStatus.EmptyPayload, empty.Status);
        AssertTrue(empty.IsDetectionResult, "empty ResultDefect command should still be handled");

        DetectionResultParseResult invalid = PythonDetectionResultProtocol.Parse("ResultDefect { bad json");
        AssertEqual(DetectionResultParseStatus.InvalidPayload, invalid.Status);
        AssertTrue(!string.IsNullOrWhiteSpace(invalid.ErrorMessage), "invalid payload did not include an error message");

        DetectionResultParseResult envelopeError = PythonDetectionResultProtocol.Parse("{\"type\":\"ResultDefect\",\"version\":1,\"error\":\"weights missing\",\"items\":[]}");
        AssertEqual(DetectionResultParseStatus.InvalidPayload, envelopeError.Status);
        AssertTrue(envelopeError.ErrorMessage.Contains("weights missing"), "detection envelope error was not surfaced");

        DetectionResultParseResult structuredError = PythonDetectionResultProtocol.Parse("{\"type\":\"DetectImageResult\",\"version\":1,\"requestId\":\"req-bad\",\"imageId\":\"img-bad\",\"ok\":false,\"error\":{\"code\":\"DetectImageFailed\",\"message\":\"file missing\"},\"candidates\":[]}");
        AssertEqual(DetectionResultParseStatus.InvalidPayload, structuredError.Status);
        AssertEqual("req-bad", structuredError.RequestId);
        AssertTrue(structuredError.ErrorMessage.Contains("file missing"), "structured detection error was not surfaced");
    }

    private static void TestLabelingImageWorkspace()
    {
        var workspace = new LabelingImageWorkspace();
        using Bitmap image = new Bitmap(32, 24);

        workspace.SetActiveImage("part-001", @"C:\images\part-001.png", image);

        AssertEqual("part-001", workspace.ActiveImageName);
        AssertEqual(@"C:\images\part-001.png", workspace.ActiveImagePath);
        AssertTrue(ReferenceEquals(image, workspace.ActiveImage), "active image was not stored in ImageSpace");
        AssertTrue(ReferenceEquals(image, workspace.GetMainImage()), "main image was not stored in ImageSpace");
        AssertTrue(workspace.MainImageChanged, "main image changed flag was not set");

        workspace.AcceptMainImageChange();
        AssertTrue(!workspace.MainImageChanged, "main image changed flag was not accepted");
    }

    private static void TestAppLog()
    {
        AppLog.NORMAL("Smoke log {0}", 1);
        AppLog.ABNORMAL("Smoke error {0}", 2);
        AppLog.COMM("Smoke communication {0}", 3);
    }

    private static void TestScreenCapturePath()
    {
        string root = CreateTempRoot();
        try
        {
            string path = ScreenCaptureService.CreateCaptureFilePath(
                root,
                "Main:Window?",
                new DateTime(2026, 6, 17, 10, 58, 10));

            AssertTrue(Directory.Exists(Path.Combine(root, "CAPTURE")), "capture directory was not created");
            AssertEqual("Main_Window__20260617_105810.jpeg", Path.GetFileName(path));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestImageListSupportedFiles()
    {
        string root = CreateTempRoot();
        try
        {
            using (Bitmap image = new Bitmap(4, 3))
            {
                image.Save(Path.Combine(root, "b.PNG"), System.Drawing.Imaging.ImageFormat.Png);
                image.Save(Path.Combine(root, "a.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(Path.Combine(root, "c.tiff"), System.Drawing.Imaging.ImageFormat.Tiff);
            }

            File.WriteAllText(Path.Combine(root, "ignore.txt"), "not an image");

            List<string> files = InvokePrivateStaticResult<List<string>>(
                typeof(WpfLabelingShellWindow),
                "EnumerateImageFiles",
                root);

            AssertEqual(3, files.Count);
            AssertEqual("a.jpg", Path.GetFileName(files[0]));
            AssertEqual("b.PNG", Path.GetFileName(files[1]));
            AssertEqual("c.tiff", Path.GetFileName(files[2]));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfClassCatalogUiText()
    {
        string xaml = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfClassCatalogPanel.xaml"),
            Encoding.UTF8);

        if (xaml.Contains("&#xD074;&#xB798;&#xC2A4;", StringComparison.Ordinal)
            && xaml.Contains("&#xC800;&#xC7A5; &#xACBD;&#xB85C;", StringComparison.Ordinal)
            && xaml.Contains("&#xCD94;&#xAC00;", StringComparison.Ordinal)
            && xaml.Contains("&#xC0AD;&#xC81C;", StringComparison.Ordinal)
            && xaml.Contains("&#xACBD;&#xB85C;", StringComparison.Ordinal)
            && new WpfClassCatalogPanelViewModel().StatusText.Contains("\uD074\uB798\uC2A4 \uC774\uB984", StringComparison.Ordinal))
        {
            return;
        }

        AssertTrue(xaml.Contains("클래스", StringComparison.Ordinal), "WPF class catalog class label was not localized");
        AssertTrue(xaml.Contains("저장 경로", StringComparison.Ordinal), "WPF class catalog output path label was not localized");
        AssertTrue(xaml.Contains("추가", StringComparison.Ordinal), "WPF class add button was not localized");
        AssertTrue(xaml.Contains("삭제", StringComparison.Ordinal), "WPF class delete button was not localized");
        AssertTrue(xaml.Contains("경로", StringComparison.Ordinal), "WPF output root browse button was not localized");
        AssertTrue(xaml.Contains("클래스 이름을 입력하고 추가를 누르세요.", StringComparison.Ordinal), "WPF class status text was not localized");
    }

    private static void TestWpfYoloSettingsUiText()
    {
        string yoloXaml = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfYoloModelSettingsPanel.xaml"),
            Encoding.UTF8);
        string trainingXaml = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfTrainingSettingsPanel.xaml"),
            Encoding.UTF8);

        AssertTrue(yoloXaml.Contains("모델 설정", StringComparison.Ordinal), "WPF YOLO model settings header was not localized");
        AssertTrue(yoloXaml.Contains("프로젝트", StringComparison.Ordinal), "WPF YOLO project label was not localized");
        AssertTrue(yoloXaml.Contains("클라이언트", StringComparison.Ordinal), "WPF YOLO client label was not localized");
        AssertTrue(yoloXaml.Contains("가중치", StringComparison.Ordinal), "WPF YOLO weights label was not localized");
        AssertTrue(yoloXaml.Contains("신뢰도", StringComparison.Ordinal), "WPF YOLO confidence label was not localized");
        AssertTrue(yoloXaml.Contains("검출 자동 시작", StringComparison.Ordinal), "WPF YOLO auto-start label was not localized");
        AssertTrue(yoloXaml.Contains("저장", StringComparison.Ordinal), "WPF YOLO save button was not localized");
        AssertTrue(yoloXaml.Contains("기본값", StringComparison.Ordinal), "WPF YOLO reset button was not localized");

        AssertTrue(trainingXaml.Contains("학습 설정", StringComparison.Ordinal), "WPF training settings header was not localized");
        AssertTrue(trainingXaml.Contains("이미지 크기", StringComparison.Ordinal), "WPF training image-size label was not localized");
        AssertTrue(trainingXaml.Contains("배치", StringComparison.Ordinal), "WPF training batch label was not localized");
        AssertTrue(trainingXaml.Contains("에포크", StringComparison.Ordinal), "WPF training epoch label was not localized");
        AssertTrue(trainingXaml.Contains("검증 %", StringComparison.Ordinal), "WPF training validation label was not localized");
        AssertTrue(trainingXaml.Contains("새로고침", StringComparison.Ordinal), "WPF training refresh button was not localized");
        AssertTrue(trainingXaml.Contains("시작", StringComparison.Ordinal), "WPF training start button was not localized");
        AssertTrue(trainingXaml.Contains("중지", StringComparison.Ordinal), "WPF training stop button was not localized");
    }

    private static void TestRoiGeometry()
    {
        Rectangle roi = RoiGeometry.CreateBoundedRectangle(
            new Point(50, 60),
            new Point(-10, 200),
            new Rectangle(0, 0, 100, 100));

        AssertEqual(new Rectangle(0, 60, 50, 40), roi);

        var canvasRoi = new CanvasRect<float>(10, 60, 70, 10);
        AssertEqual(LineOverType.Move2D, canvasRoi.GetHandleContainsPoint(22, 35, 0.2F, 20F));
        canvasRoi.SetEditingType(22, 35, 0.2F, 20F);
        AssertEqual(EditingType.Move, canvasRoi.EditingType);
        AssertEqual(LineOverType.Move2D, canvasRoi.GetHandleContainsPoint(11, 35, 0.2F, 20F));
        canvasRoi.SetEditingType(11, 35, 0.2F, 20F);
        AssertEqual(EditingType.Move, canvasRoi.EditingType);
        AssertEqual(LineOverType.Move2D, canvasRoi.GetHandleContainsPoint(12, 58, 0.2F, 20F));
        canvasRoi.SetEditingType(12, 58, 0.2F, 20F);
        AssertEqual(EditingType.Move, canvasRoi.EditingType);
        AssertEqual(LineOverType.VSplit, canvasRoi.GetHandleContainsPoint(10, 35, 0.2F, 20F));
        AssertEqual(LineOverType.VSplit, canvasRoi.GetHandleContainsPoint(9, 35, 0.2F, 20F));
        AssertEqual(LineOverType.SizeNWSE, canvasRoi.GetHandleContainsPoint(10, 60, 0.2F, 20F));
        AssertEqual(LineOverType.SizeNWSE, canvasRoi.GetHandleContainsPoint(9, 61, 0.2F, 20F));

        string devCanvasPath = Path.GetFullPath(Path.Combine(FindRepositoryRoot(), "..", "OpenVisionLab_Dev", "Library", "OpenVisionLab.ImageCanvas", "Compatibility", "CanvasCompatibility.cs"));
        if (File.Exists(devCanvasPath))
        {
            string devCanvasSource = File.ReadAllText(devCanvasPath);
            AssertTrue(devCanvasSource.Contains("if (IsInsideRoiBody(x, y)) return LineOverType.Move2D;", StringComparison.Ordinal), "OpenVisionLab_Dev ImageCanvas should keep the same ROI body-first hit-test rule");
            AssertTrue(!devCanvasSource.Contains("float tolerance = GetHitTolerance(zoomScale, handleSize);", StringComparison.Ordinal), "OpenVisionLab_Dev ImageCanvas should not keep the old single wide ROI hit tolerance");
        }
    }

    private static void TestRoiGeometrySuppressesExtendedCallbacksDuringDrag()
    {
        var canvasRoi = new CanvasRect<float>(10, 60, 70, 10);
        canvasRoi.CreateExtendedRectangleFromSize();
        int mainChanged = 0;
        int extendedChanged = 0;
        canvasRoi.OnChanged = () => mainChanged++;
        canvasRoi.ExtendedRectangle.OnChanged = () => extendedChanged++;

        canvasRoi.OffsetMove(new CanvasSize<float>(5, 0), notify: false);
        canvasRoi.SetEditingType(75, 35, 1F, 10F);
        canvasRoi.Move(80, 35, new Size(200, 200), notify: false);

        AssertEqual(0, mainChanged);
        AssertEqual(0, extendedChanged);
        AssertTrue(canvasRoi.IsChanged, "main ROI should still be marked dirty while suppressing MouseMove display-list callbacks");
        AssertTrue(canvasRoi.ExtendedRectangle.IsChanged, "extended ROI should still be marked dirty for the committed mouse-up rebuild");

        canvasRoi.OffsetMove(new CanvasSize<float>(1, 0), notify: true);
        AssertEqual(1, mainChanged);
        AssertEqual(1, extendedChanged);
    }

    private static void TestOpenGlImageGeometry()
    {
        AssertEqual(new Point(40, 70), OpenGlImageGeometry.RobotToImage(new Point(40, 10), new Size(100, 80)));
        AssertEqual(new Point(0, 79), OpenGlImageGeometry.RobotToImage(new Point(-10, -5), new Size(100, 80)));

        RectangleF glRect = OpenGlImageGeometry.ToOpenGlRectangle(new Rectangle(10, 20, 30, 15), 100);
        AssertEqual(new RectangleF(10, 65, 30, 15), glRect);

        List<Point> handles = OpenGlImageGeometry.GetHandlePoints(new Rectangle(10, 20, 30, 40)).ToList();
        AssertEqual(new Point(10, 20), handles[0]);
        AssertEqual(new Point(40, 60), handles[4]);

        AssertEqual(2, OpenGlImageGeometry.GetHandleSize(0.2F));
        AssertEqual(24, OpenGlImageGeometry.GetHandleSize(2F));
    }

    private static void TestOpenGlDetectionOverlayScreenBounds()
    {
        using var canvas = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl
        {
            Size = new Size(640, 480)
        };
        var openGlControl = (Control)typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(canvas, null);
        openGlControl.Size = new Size(640, 480);

        canvas.TextureAreas = new System.Collections.Concurrent.ConcurrentDictionary<string, List<OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam>>();
        canvas.TextureAreas.TryAdd("sample", new List<OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam>
        {
            new OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam
            {
                GLDrawingTextureArea = new RectangleF(100, 260, 260, -260),
                GLTextureArea = new RectangleF(100, 0, 260, 260)
            }
        });

        SetPrivateField(canvas, "_zoom", 260F);
        SetPrivateField(canvas, "_offsetSize", new SizeF(-100F, 0F));

        RectangleF screenBounds = canvas.GetScreenRectFromImagePixelBounds(new RectangleF(30, 32, 47, 42));
        AssertRectangleNearlyEqual(new RectangleF(55.38F, 59.08F, 86.77F, 77.54F), screenBounds, 0.02F);

        SetPrivateField(canvas, "_zoom", 130F);
        SetPrivateField(canvas, "_offsetSize", new SizeF(-100F, 0F));

        RectangleF zoomedBounds = canvas.GetScreenRectFromImagePixelBounds(new RectangleF(30, 32, 47, 42));
        AssertRectangleNearlyEqual(new RectangleF(110.77F, -361.85F, 173.54F, 155.08F), zoomedBounds, 0.02F);
    }

    private static void TestOpenGlRefreshThreadMarshal()
    {
        bool previous = System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls;
        System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = true;
        try
        {
            using var canvas = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
            var openGlControl = (System.Windows.Forms.Control)typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
                .GetMethod("GetOpenGLControl")
                .Invoke(canvas, null);
            openGlControl.CreateControl();

            Exception captured = null;
            using var completed = new ManualResetEventSlim(false);
            Thread worker = new Thread(() =>
            {
                try
                {
                    canvas.RefreshGL();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    completed.Set();
                }
            });
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();

            AssertTrue(completed.Wait(TimeSpan.FromSeconds(3)), "cross-thread RefreshGL did not return");
            AssertTrue(captured == null, $"RefreshGL should marshal to the OpenGL child control thread: {captured?.Message}");
        }
        finally
        {
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = previous;
        }
    }

    private static void TestOpenGlViewMathGuardsZeroSizeResizeStates()
    {
        using var canvas = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl
        {
            Size = Size.Empty
        };
        var openGlControl = (Control)typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(canvas, null);
        openGlControl.Size = Size.Empty;
        openGlControl.CreateControl();

        AssertEqual(1, canvas.GetControlMinSize());
        canvas.ZoomAt(Point.Empty, 120);
        canvas.ApplyViewState(new OpenVisionLab.ImageCanvas.Canvas.CanvasViewState(120F, SizeF.Empty));
    }

    private static void TestOpenGlCanvasExposesActualSizeZoom()
    {
        string sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "OpenVisionLab",
            "Library",
            "OpenVisionLab.ImageCanvas",
            "Engine",
            "ImageCanvasControl.ViewState.cs");
        string source = File.ReadAllText(sourcePath);
        string normalizedSource = source.Replace("\r\n", "\n");

        AssertTrue(source.Contains("public void ZoomToActualSize()", StringComparison.Ordinal), "OpenGL canvas should expose a 1:1 zoom command for WPF viewer controls");
        AssertTrue(source.Contains("_zoom = GetControlMinSize();", StringComparison.Ordinal), "actual-size zoom should map image pixels to screen pixels");
        AssertTrue(source.Contains("OpenGlDrawing.ZoomFactor = ZoomScale;", StringComparison.Ordinal), "actual-size zoom should update OpenGL drawing scale");
        AssertTrue(normalizedSource.Contains("_offsetSize = viewState.OffsetSize;\n\t\t\tInvalidateVisibleOverlayCache();", StringComparison.Ordinal), "applying a saved view state should rebuild the visible ROI cache for the new viewport");
        AssertTrue(normalizedSource.Contains("AdjustOffsetForZoom(mousePos, oldZoom);\n\t\t\t// Zoom changes viewport bounds", StringComparison.Ordinal), "mouse-wheel zoom should document why visible ROI cache is invalidated");
        AssertTrue(normalizedSource.Contains("AdjustOffsetForZoom(mousePos, oldZoom);\n\t\t\t// Zoom changes viewport bounds, so the visible ROI cache must be rebuilt\n\t\t\t// through the spatial index instead of reusing the previous view.\n\t\t\tInvalidateVisibleOverlayCache();", StringComparison.Ordinal), "mouse-wheel zoom should invalidate visible ROI cache before repaint");
    }

    private static void TestOpenGlMousePanAvoidsPerEventPixelReadback()
    {
        string sourcePath = Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Engine", "ImageCanvasControl.cs");
        string source = File.ReadAllText(sourcePath);
        string viewModelSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "ViewModel", "RoiImageCanvasViewModel.cs"));
        string refreshSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "ViewModel", "RoiImageCanvasViewModel.Refresh.cs"));
        string canvasViewXamlSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "View", "RoiImageCanvasView.xaml"));

        AssertTrue(source.Contains("bool shouldReadPixel = e.Button == MouseButtons.None && ShouldReadPixelStatus(e);", StringComparison.Ordinal), "mouse move should not read OpenGL pixels while the user is dragging");
		AssertTrue(source.Contains("if (isDraggingView && e.Button != MouseButtons.None)", StringComparison.Ordinal), "texture pan mouse move should bypass ViewModel/status/ROI hit-test work");
        AssertTrue(source.Contains("bool isManipulatingOverlay = IsOverlayMouseManipulationMode(_viewMode) && e.Button != MouseButtons.None;", StringComparison.Ordinal), "ROI drag mouse move should bypass status readback and cache work");
        AssertTrue(source.Contains("DrawVisibleOverlaysFast", StringComparison.Ordinal), "ROI drag frames should draw a cached static overlay scene plus the live ROI");
        AssertTrue(source.Contains("Draw it directly and compile", StringComparison.Ordinal), "active ROI drag should draw live geometry and defer display-list compilation until mouse-up");
		AssertTrue(source.Contains("TryReadScreenPixel", StringComparison.Ordinal), "mouse hover should read the screen pixel with a single readback");
		AssertTrue(source.Contains("_pixelReadbackBuffer", StringComparison.Ordinal), "mouse hover pixel readback should reuse a 1px buffer instead of allocating on every sampled MouseMove");
		AssertTrue(source.Contains("Reuse one 1px buffer", StringComparison.Ordinal), "pixel readback buffer reuse should document the long-session MouseMove reason");
		AssertTrue(!source.Contains("GrayValue = GetGrayValue(openGLControl.OpenGL", StringComparison.Ordinal), "mouse move should not perform a separate gray-value readback");
		AssertTrue(source.Contains("ShouldRecalculateVisibleOverlaysOnMouseMove", StringComparison.Ordinal), "mouse move should centralize expensive visible-overlay recalculation decisions");
		AssertTrue(source.Contains("return false;", StringComparison.Ordinal), "mouse move should never recalculate the visible-overlay cache while dragging or drawing");
		AssertTrue(source.Contains("RebuildVisibleOverlayCacheIfNeeded", StringComparison.Ordinal), "visible-overlay calculation should be cached instead of running on every RefreshGL");
		AssertTrue(source.Contains("InvalidateVisibleOverlayCache", StringComparison.Ordinal), "overlay cache should be explicitly invalidated only on structural or viewport changes");
		AssertTrue(source.Contains("ShouldRequestOpenGlRepaintOnMouseMove", StringComparison.Ordinal), "rectangle drawing should centralize live-preview repaint decisions");
		AssertTrue(source.Contains("isPointerDown && ShouldRefreshMouseMoveRepaint()", StringComparison.Ordinal), "rectangle drawing preview should be frame-rate limited while the pointer is down");
		AssertTrue(source.Contains("Drawing preview is live feedback only", StringComparison.Ordinal), "rectangle drawing preview should document why it avoids committed display-list rebuilds");
		AssertTrue(source.Contains("Hover only updates cursor/status state", StringComparison.Ordinal), "hover MouseMove should not repaint the OpenGL scene when no ROI geometry changed");
		AssertTrue(source.Contains("if (!isPointerDown)", StringComparison.Ordinal), "OpenGL MouseMove repaint requests should ignore pointer-up hover frames");
        AssertTrue(source.Contains("MouseMoveRepaintIntervalTicks", StringComparison.Ordinal), "texture pan mouse-move repaint should be frame-rate limited");
		AssertTrue(source.Contains("DrawContent(bool drawOverlays)", StringComparison.Ordinal), "OpenGL canvas should expose a texture-only draw path for fast pan frames");
        AssertTrue(source.Contains("FindInteractiveOverlaysNearPoint", StringComparison.Ordinal), "OpenGL canvas should expose spatial-indexed ROI candidate queries");
        AssertTrue(source.Contains("VisitInteractiveOverlaysNearPoint", StringComparison.Ordinal), "OpenGL canvas should expose allocation-light ROI hit-test visits");
        AssertTrue(source.Contains("VisitVisibleOverlaysInBounds(GetViewportBounds(), MaxVisibleOverlayShapes", StringComparison.Ordinal), "visible overlay cache should visit the viewport through the spatial index with a rendering LOD cap");
        AssertTrue(source.Contains("MaxVisibleOverlayShapes = 10_000", StringComparison.Ordinal), "zoomed-out ROI rendering should cap the visual cache while hit-testing remains full fidelity");
        AssertTrue(source.Contains("VisibleOverlayLodChanged", StringComparison.Ordinal), "OpenGL canvas should expose when zoomed-out ROI display is capped");
        AssertTrue(viewModelSource.Contains("OverlayDisplayStatusText", StringComparison.Ordinal), "ROI canvas ViewModel should expose the display cap status to the WPF status strip");
        AssertTrue(viewModelSource.Contains("표시 ROI", StringComparison.Ordinal), "ROI canvas display cap status should use a compact operator-readable label");
        AssertTrue(canvasViewXamlSource.Contains("OverlayDisplayStatusLabel", StringComparison.Ordinal), "ROI canvas status strip should include the display cap badge");
        AssertTrue(canvasViewXamlSource.Contains("IsOverlayDisplayLodActive", StringComparison.Ordinal), "ROI canvas display cap badge should appear only when LOD is active");
        string roiMouseMoveSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "RoiInteraction", "RoiInteractionMouseMove.cs"));
        AssertTrue(!roiMouseMoveSource.Contains("ResizeGroupRectangle(activeRoiRect.GroupType)", StringComparison.Ordinal), "moving one ROI should not resize the whole group on every mouse move");
        AssertTrue(roiMouseMoveSource.Contains("UpdateInteractiveOverlayIndex(activeRoiRect)", StringComparison.Ordinal), "moving or resizing one ROI should update only that ROI in the spatial index");
        AssertTrue(roiMouseMoveSource.Contains("notifyEditingCompleted = true", StringComparison.Ordinal), "low-level ROI move helpers should allow callers to suppress MouseMove callbacks");
        AssertTrue(viewModelSource.Contains("notifyEditingCompleted: false", StringComparison.Ordinal), "Viewer MouseMove should suppress display-list rebuilds and WPF edit callbacks during ROI drag");
        AssertTrue(viewModelSource.Contains("PixelPropertyUpdateIntervalTicks", StringComparison.Ordinal), "Viewer MouseMove should throttle status-bar property notifications");
        AssertTrue(viewModelSource.Contains("UpdatePixelProperty(throttle: true)", StringComparison.Ordinal), "Viewer MouseMove should not fire WPF status bindings for every input event");
        AssertTrue(viewModelSource.Contains("BrushCursorPreviewRefreshIntervalTicks", StringComparison.Ordinal), "brush/mask hover preview should throttle MouseMove repaint requests");
        AssertTrue(viewModelSource.Contains("NotifyBrushCursorPreviewProperties", StringComparison.Ordinal), "brush/mask hover preview should not notify all cursor bindings for every raw MouseMove");
        AssertTrue(viewModelSource.Contains("RaiseImagePointHovered(e, currentRobotyPos)", StringComparison.Ordinal), "brush/mask image-point hover should stay on its own MouseMove path before ROI cursor hit-testing");
        AssertTrue(viewModelSource.Contains("DetectionOverlaySpatialIndex", StringComparison.Ordinal), "AI detection overlay click hit-testing should use a spatial index");
        AssertTrue(viewModelSource.Contains("_detectionOverlayHitIndex.FindTopmostContainingPoint", StringComparison.Ordinal), "AI detection overlay click hit-testing should find the topmost nearby candidate without allocating a candidate list");
        AssertTrue(viewModelSource.Contains("TryGetDetectionOverlayIndexAtCanvasPoint", StringComparison.Ordinal), "AI detection overlay diagnostics should expose a direct non-reflection hit-test path");
        AssertTrue(viewModelSource.Contains("_detectionOverlayHitIndex.QueryBounds", StringComparison.Ordinal), "AI detection overlay rendering should query only candidates in the visible viewport");
        AssertTrue(viewModelSource.Contains("_polygonOverlayRenderIndex.QueryBounds", StringComparison.Ordinal), "polygon overlay rendering should query only polygons in the visible viewport");
        AssertTrue(viewModelSource.Contains("_maskOverlayRenderIndex.QueryBounds", StringComparison.Ordinal), "mask overlay rendering should query only masks in the visible viewport");
        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("suppressObjectReviewSelectionChanged", StringComparison.Ordinal), "object-list refresh should not clear the active ROI selection during transient WPF SelectedItem nulls");
        AssertTrue(shellSource.Contains("CreateManualRoiSelection(e.RoiRect)", StringComparison.Ordinal), "ROI mouse-up should keep the side object list selected on the clicked ROI");
        AssertTrue(shellSource.Contains("TryRefreshManualRoiObjectReviewRow", StringComparison.Ordinal), "ROI edit commit should update one object-review row instead of rebuilding the whole side list");
        AssertTrue(shellSource.Contains("RemoveCanvasRoiOverlayById", StringComparison.Ordinal), "object review delete should remove one canvas ROI by overlay id instead of redrawing every ROI");
        AssertTrue(shellSource.Contains("ClearCanvasRoiSelectionAfterDelete", StringComparison.Ordinal), "object review delete should clear the live selected ROI after removing the canvas overlay");
        AssertTrue(shellSource.Contains("RefreshObjectReviewAfterDelete", StringComparison.Ordinal), "object review delete should have an incremental side-list path for large ROI sets");
        AssertTrue(shellSource.Contains("QueueActiveImageQueueStatusRefresh", StringComparison.Ordinal), "object review delete should defer queue/review-status bookkeeping until after the UI deletion path");
        AssertTrue(shellSource.Contains("SetSegmentationOverlays(overlays, maskOverlays)", StringComparison.Ordinal), "mask/polygon MouseMove refresh should request one OpenGL repaint through the batch overlay API");
        string cViewerSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Library", "CViewer.cs"));
        AssertTrue(cViewerSource.Contains("Legacy CViewer hover updates status/cursor only", StringComparison.Ordinal), "legacy CViewer hover MouseMove should not repaint the texture when no annotation geometry changed");
        AssertTrue(cViewerSource.Contains("GetSelectedRoiObject", StringComparison.Ordinal), "legacy CViewer cursor updates should use the cached selected ROI instead of flattening every ROI list on MouseMove");
        string cViewerRenderingSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Library", "CViewer.Rendering.cs"));
        AssertTrue(cViewerRenderingSource.Contains("Color baseColor = EnsureReadableOverlayColor(overlay.Color);", StringComparison.Ordinal), "selected candidates should keep their semantic class color instead of changing to yellow on click");
        AssertTrue(!cViewerRenderingSource.Contains("overlay.IsSelected ? Color.Yellow : EnsureReadableOverlayColor(overlay.Color)", StringComparison.Ordinal), "legacy candidate selection should not replace class color with yellow");
        string shapeDrawingSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "OpenGL", "OpenGlShapeDrawing.cs"));
        AssertTrue(shapeDrawingSource.Contains("Selection handles are editor affordances", StringComparison.Ordinal), "ROI selection handles should document why they do not change the object class color");
        string objectReviewViewModelSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "ViewModels", "WpfObjectReviewPanelViewModel.cs"));
        AssertTrue(objectReviewViewModelSource.Contains("TryReplaceObject", StringComparison.Ordinal), "object review ViewModel should expose a single-row replacement path for ROI edits");
        AssertTrue(objectReviewViewModelSource.Contains("TryRemoveObject", StringComparison.Ordinal), "object review ViewModel should expose a single-row removal path for ROI deletes");
        string roiMouseDownSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "RoiInteraction", "RoiInteractionMouseDown.cs"));
        AssertTrue(roiMouseDownSource.Contains("FindBestInteractiveRectAtPoint", StringComparison.Ordinal), "ROI hit testing should ask the spatial index for one best rectangle instead of materializing every candidate");
        AssertTrue(!roiMouseDownSource.Contains("GetVisibleUnlockedOverlays()", StringComparison.Ordinal), "ROI hit testing should not build a full visible/unlocked overlay list");
        AssertTrue(viewModelSource.Contains("RefreshGroupBoundsForCommittedRoi", StringComparison.Ordinal), "group bounds should refresh once when the ROI edit is committed");
        AssertTrue(viewModelSource.Contains("TryBeginPanModeMouseDown", StringComparison.Ordinal), "pan mode should start without scanning all overlays for hit testing");
        AssertTrue(viewModelSource.Contains("TryBeginDrawingModeMouseDown", StringComparison.Ordinal), "drawing mode should start new boxes without scanning all overlays");
        AssertTrue(viewModelSource.Contains("UpdateInteractionCursor", StringComparison.Ordinal), "cursor updates should avoid overlay hit testing while dragging or drawing");
        AssertTrue(viewModelSource.Contains("Hover cursor stays on the selected ROI only", StringComparison.Ordinal), "hover cursor updates should not hit-test every unselected ROI on MouseMove");
        AssertTrue(viewModelSource.Contains("isFastDrawingPreviewFrame", StringComparison.Ordinal), "rectangle drawing should use a lightweight live preview frame");
        AssertTrue(viewModelSource.Contains("DrawContent(drawOverlays: true, liveOverlay: liveOverlay)", StringComparison.Ordinal), "pan, ROI drag, and drawing preview frames should draw the cached ROI scene rather than replaying every overlay directly");
        AssertTrue(viewModelSource.Contains("Pan keeps the cached ROI scene visible", StringComparison.Ordinal), "pan should keep ROI context visible while skipping expensive secondary overlays");
        AssertTrue(source.Contains("return isDraggingView && ShouldRefreshDragOverlays();", StringComparison.Ordinal), "pan MouseMove should refresh visible ROI cache at a bounded cadence, not every raw input event");
        AssertTrue(viewModelSource.Contains("_imageViewer.ZoomAt(e.Location, e.Delta);", StringComparison.Ordinal), "WPF wheel zoom should use the central viewport/cache invalidation path");
        AssertTrue(viewModelSource.Contains("Do not clear _selectedRect here", StringComparison.Ordinal), "ROI click selection should remain active after mouse-up until another target is clicked");
        AssertTrue(viewModelSource.Contains("ClearDeletedRoiSelection", StringComparison.Ordinal), "ROI delete should clear stale live selection without recompiling a removed overlay");
        AssertTrue(viewModelSource.Contains("MarkRoiDisplayChanged", StringComparison.Ordinal), "ROI click selection should refresh the display list immediately");
        AssertTrue(viewModelSource.Contains("MarkRoiDisplayChanged(canvasRect)", StringComparison.Ordinal) && viewModelSource.Contains("_imageViewer.RefreshGL();", StringComparison.Ordinal), "ROI click selection should repaint immediately without waiting for the next mouse move");
        AssertTrue(!source.Contains("Parallel.ForEach(visibleOverlays", StringComparison.Ordinal), "visible-overlay calculation should avoid per-frame parallel overhead");
        AssertTrue(viewModelSource.Contains("DrawingRefreshIntervalMilliseconds = 16.0", StringComparison.Ordinal), "ROI drawing refresh timer should be frame-rate bounded, not a 1ms loop");
        AssertTrue(!viewModelSource.Contains("new System.Timers.Timer(1)", StringComparison.Ordinal), "ROI drawing refresh timer should not use a 1ms interval");
        AssertTrue(viewModelSource.Contains("AutoReset = false", StringComparison.Ordinal), "ROI drawing refresh timer should be a debounced single-shot timer");
        AssertTrue(refreshSource.Contains("_refreshTimer.Stop();", StringComparison.Ordinal), "ROI drawing refresh timer should debounce repeated resize/draw requests");
        AssertTrue(refreshSource.Contains("_refreshTimer.Start();", StringComparison.Ordinal), "ROI drawing refresh timer should still schedule a repaint after a draw request");
    }

    private static void TestTexturePanMouseMoveUsesFastPath()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(1_000, 500);
        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        ((Control)openGlControl).CreateControl();
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_xSpan", (float)imageSize.Width);
        SetPrivateField(imageViewer, "_ySpan", (float)imageSize.Height);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        imageViewer.GetCanvasOverlayManager().Clear();
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "pan-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect",
                DisplayListId = 1
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, item);
        }
        loadStopwatch.Stop();
        imageViewer.PreMousePos = imageViewer.GetCurrentRobotPos(0, 50);
        imageViewer.SetViewMode(OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Drag);

        int viewModelMouseMoveEvents = 0;
        imageViewer.MouseMove += (_, _) => viewModelMouseMoveEvents++;
        MethodInfo mouseMoveMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseMove",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        AssertTrue(mouseMoveMethod != null, "OpenGL canvas MouseMove handler should be testable");

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 1; index <= 1_000; index++)
        {
            mouseMoveMethod.Invoke(
                imageViewer,
                new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 0, index, 50, 0) });
        }

        stopwatch.Stop();
        OpenVisionLab.ImageCanvas.Canvas.CanvasViewState viewState = imageViewer.CaptureViewState();
        List<CanvasShape> visibleShapes = GetPrivateField<List<CanvasShape>>(imageViewer, "_shapesViewPort");
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"TEXTURE_PAN_1000_MOUSEMOVE_MS={stopwatch.Elapsed.TotalMilliseconds:F3} VIEWMODEL_MOUSEMOVE_EVENTS={viewModelMouseMoveEvents} OFFSET_X={viewState.OffsetSize.Width:F1} ROI_OBJECTS={objectCount} PAN_ROI_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} PAN_VISIBLE_SHAPES={visibleShapes.Count}"));

        AssertEqual(0, viewModelMouseMoveEvents);
        AssertTrue(viewState.OffsetSize.Width >= 999F, "texture pan MouseMove should update only the view offset");
        AssertTrue(visibleShapes.Count < objectCount / 20, "texture pan should keep a bounded visible ROI cache while panning across 500K objects");
        AssertTrue(stopwatch.Elapsed.TotalMilliseconds < 180.0, "texture pan MouseMove should stay lightweight across repeated input events with a 500K ROI cache");
    }

    private static void TestHoverMouseMoveUsesSpatialIndexAndThrottledStatusAt500KObjects()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(1_000, 1_000);
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("Hover500K");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        ((Control)openGlControl).CreateControl();
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        SetPrivateField(viewModel, "_imageSize", imageSize);
        imageViewer.GetCanvasOverlayManager().Clear();

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "hover-root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = true
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        CanvasRect<float> targetRect = null;
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "hover-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect"
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(parent.GroupType, item);
            if (x == 50 && y == 50)
            {
                targetRect = rect;
            }
        }

        AssertTrue(targetRect != null, "target ROI was not created for hover MouseMove performance test");
        var hitPoint = new PointF(targetRect.Left + 0.5F, targetRect.Bottom + 0.5F);
        List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem> candidates = imageViewer.FindInteractiveOverlaysNearPoint(hitPoint, 12F);
        AssertTrue(candidates.Count < objectCount / 20, "hover spatial query should not return the full object set");

        int statusPropertyChangedCount = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.GrayValue)
                || args.PropertyName == nameof(viewModel.RobotPos)
                || args.PropertyName == nameof(viewModel.ImagePos)
                || args.PropertyName == nameof(viewModel.PixelColor)
                || args.PropertyName == nameof(viewModel.HeightValue))
            {
                statusPropertyChangedCount++;
            }
        };

        MethodInfo mouseMoveMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseMove",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        AssertTrue(mouseMoveMethod != null, "OpenGL canvas MouseMove handler should be testable");

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            int x = 50 + (index % 2);
            mouseMoveMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.None, 0, x, 50, 0) });
        }

        stopwatch.Stop();
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"HOVER_500K_1000_MOUSEMOVE_MS={stopwatch.Elapsed.TotalMilliseconds:F3} CANDIDATES={candidates.Count} STATUS_PROPERTY_CHANGED={statusPropertyChangedCount}"));

        AssertTrue(stopwatch.Elapsed.TotalMilliseconds < 150.0, "hover MouseMove should not repaint or rescan all ROI objects");
        AssertTrue(statusPropertyChangedCount <= 50, "hover MouseMove should throttle status-bar PropertyChanged notifications");
    }

    private static void TestBrushHoverMouseMoveStaysThrottledAt500KObjects()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(1_000, 1_000);
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("BrushHover500K");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        ((Control)openGlControl).CreateControl();
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        SetPrivateField(viewModel, "_imageSize", imageSize);
        imageViewer.GetCanvasOverlayManager().Clear();

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "brush-hover-root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = true
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "brush-hover-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect"
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(
                parent.GroupType,
                new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
                {
                    GroupType = "Defect",
                    Shape = rect,
                    ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                    InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                    IsVisible = true
                });
        }

        AssertEqual(objectCount, parent.ChildObjects.Count);
        viewModel.IsImagePointInputMode = true;

        int hoverEvents = 0;
        int brushPropertyChangedCount = 0;
        int statusPropertyChangedCount = 0;
        viewModel.ImagePointHovered += (_, args) =>
        {
            hoverEvents++;
            viewModel.SetBrushCursorPreview(args.ImagePoint, 11, Color.FromArgb(80, 180, 255), isEraser: false);
        };
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.IsBrushCursorPreviewVisible)
                || args.PropertyName == nameof(viewModel.BrushCursorPreviewImagePoint)
                || args.PropertyName == nameof(viewModel.BrushCursorPreviewRadius))
            {
                brushPropertyChangedCount++;
            }

            if (args.PropertyName == nameof(viewModel.GrayValue)
                || args.PropertyName == nameof(viewModel.RobotPos)
                || args.PropertyName == nameof(viewModel.ImagePos)
                || args.PropertyName == nameof(viewModel.PixelColor)
                || args.PropertyName == nameof(viewModel.HeightValue))
            {
                statusPropertyChangedCount++;
            }
        };

        MethodInfo mouseMoveMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseMove",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        AssertTrue(mouseMoveMethod != null, "OpenGL canvas MouseMove handler should be testable");

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            int x = 10 + (index % 80);
            int y = 10 + (index % 80);
            mouseMoveMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.None, 0, x, y, 0) });
        }

        stopwatch.Stop();
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"BRUSH_HOVER_500K_1000_MOUSEMOVE_MS={stopwatch.Elapsed.TotalMilliseconds:F3} HOVER_EVENTS={hoverEvents} BRUSH_PROPERTY_CHANGED={brushPropertyChangedCount} STATUS_PROPERTY_CHANGED={statusPropertyChangedCount}"));

        AssertEqual(1_000, hoverEvents);
        AssertTrue(viewModel.IsBrushCursorPreviewVisible, "brush hover should keep the cursor radius preview visible");
        AssertTrue(stopwatch.Elapsed.TotalMilliseconds < 200.0, "brush hover MouseMove should not repaint or scan ROI objects at 500K scale");
        AssertTrue(brushPropertyChangedCount <= 20, "brush hover preview should throttle WPF property notifications");
        AssertTrue(statusPropertyChangedCount <= 50, "brush hover MouseMove should throttle status-bar PropertyChanged notifications");
    }

    private static void TestRoiDrawingPreviewMouseMoveUsesLiveShape()
    {
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("DrawingPreviewPerf");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        ((Control)openGlControl).CreateControl();
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        SetPrivateField(viewModel, "_imageSize", new Size(100, 100));
        viewModel.IsTeachingMode = true;
        viewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;

        int addedCount = 0;
        viewModel.RoiAdded += (_, _) => addedCount++;
        MethodInfo mouseDownMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseDown",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        MethodInfo mouseMoveMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseMove",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        AssertTrue(mouseDownMethod != null && mouseMoveMethod != null, "OpenGL canvas mouse handlers should be testable");

        mouseDownMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            int x = 10 + (index % 80);
            int y = 10 + (index % 60);
            mouseMoveMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 0, x, y, 0) });
        }

        stopwatch.Stop();
        CanvasRect<float> drawingPreviewRect = GetPrivateField<CanvasRect<float>>(viewModel, "_drawingRect");
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_DRAW_PREVIEW_1000_MOUSEMOVE_MS={stopwatch.Elapsed.TotalMilliseconds:F3} ADDED={addedCount} PREVIEW_EMPTY={drawingPreviewRect == null || drawingPreviewRect.IsEmpty()}"));

        AssertEqual(0, addedCount);
        AssertTrue(drawingPreviewRect != null && !drawingPreviewRect.IsEmpty(), "ROI drawing MouseMove should update a live preview shape");
        AssertTrue(stopwatch.Elapsed.TotalMilliseconds < 150.0, "ROI drawing preview MouseMove should stay lightweight across repeated input events");
    }

    private static void TestOpenGlRoiHitTestUsesSpatialIndexAt500KObjects()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(10_000, 10_000);

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        imageViewer.GetCanvasOverlayManager().Clear();

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = true
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        CanvasRect<float> selectedRect = null;
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "hit-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect"
            };

            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(parent.GroupType, item);

            if (index == objectCount / 2)
            {
                selectedRect = rect;
            }
        }
        loadStopwatch.Stop();

        AssertTrue(selectedRect != null, "selected ROI was not created for the 500K hit-test performance test");
        var hitPoint = new PointF(selectedRect.Left + 0.5F, selectedRect.Bottom + 0.5F);

        _ = imageViewer.FindInteractiveOverlaysNearPoint(hitPoint, 12F);
        _ = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, hitPoint);

        Stopwatch queryStopwatch = Stopwatch.StartNew();
        List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem> candidates = imageViewer.FindInteractiveOverlaysNearPoint(hitPoint, 12F);
        queryStopwatch.Stop();

        Stopwatch hitStopwatch = Stopwatch.StartNew();
        var (hitRect, isGroupOverlay) = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, hitPoint);
        hitStopwatch.Stop();

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_INDEXED_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} ROI_500K_CANDIDATES={candidates.Count} ROI_500K_QUERY_MS={queryStopwatch.Elapsed.TotalMilliseconds:F3} ROI_500K_HIT_MS={hitStopwatch.Elapsed.TotalMilliseconds:F3}"));

        AssertTrue(!isGroupOverlay, "window ROI hit test should not select a group overlay");
        AssertTrue(ReferenceEquals(selectedRect, hitRect), "spatial ROI hit-test should return the clicked ROI");
        AssertTrue(candidates.Count < objectCount / 20, "spatial ROI hit-test should not return the full object set as candidates");
        AssertTrue(queryStopwatch.Elapsed.TotalMilliseconds < 35.0, "spatial ROI candidate-list query should remain bounded at 500K objects; click selection uses the faster best-hit path");
        AssertTrue(hitStopwatch.Elapsed.TotalMilliseconds < 20.0, "ROI hit-test should stay interactive at 500K objects");
    }

    private static void TestOpenGlRoiRenderingUsesSpatialIndexAt500KObjects()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(1_000, 1_000);

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_xSpan", 100F);
        SetPrivateField(imageViewer, "_ySpan", 100F);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        imageViewer.GetCanvasOverlayManager().Clear();

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "render-root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = false
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "render-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect",
                DisplayListId = 1
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(parent.GroupType, item);
        }
        loadStopwatch.Stop();

        var viewport = RectangleF.FromLTRB(0F, 0F, 100F, 100F);
        _ = imageViewer.GetVisibleOverlaysInBounds(viewport);
        InvokePrivateResult<object>(imageViewer, "CalculatorVisibleOverlays");

        Stopwatch queryStopwatch = Stopwatch.StartNew();
        List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem> visibleOverlayCandidates = imageViewer.GetVisibleOverlaysInBounds(viewport);
        queryStopwatch.Stop();

        Stopwatch rebuildStopwatch = Stopwatch.StartNew();
        InvokePrivateResult<object>(imageViewer, "CalculatorVisibleOverlays");
        rebuildStopwatch.Stop();
        List<CanvasShape> visibleShapes = GetPrivateField<List<CanvasShape>>(imageViewer, "_shapesViewPort");

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_RENDER_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} ROI_500K_RENDER_CANDIDATES={visibleOverlayCandidates.Count} ROI_500K_RENDER_QUERY_MS={queryStopwatch.Elapsed.TotalMilliseconds:F3} ROI_500K_RENDER_REBUILD_MS={rebuildStopwatch.Elapsed.TotalMilliseconds:F3} VISIBLE_SHAPES={visibleShapes.Count}"));

        AssertTrue(visibleOverlayCandidates.Count > 0, "ROI viewport culling should keep visible ROI objects");
        AssertTrue(visibleOverlayCandidates.Count < objectCount / 20, "ROI viewport culling should not return the full object set");
        AssertTrue(visibleShapes.Count > 0, "ROI visible overlay cache should keep visible shapes after viewport filtering");
        AssertTrue(visibleShapes.Count <= visibleOverlayCandidates.Count, "ROI visible overlay cache should only reduce the spatial candidates");
        AssertTrue(visibleShapes.Count < objectCount / 20, "ROI visible overlay cache should not keep the full object set");
        AssertTrue(queryStopwatch.Elapsed.TotalMilliseconds < 35.0, "ROI viewport candidate-list query should stay bounded at 500K objects; visible cache rebuild uses the capped visitor path");
        AssertTrue(rebuildStopwatch.Elapsed.TotalMilliseconds < 50.0, "ROI visible overlay cache rebuild should stay interactive at 500K objects");
    }

    private static void TestOpenGlRoiFullViewportRenderingUsesLodAt500KObjects()
    {
        const int objectCount = 500_000;
        const int expectedVisibleShapeLimit = 10_000;
        var imageSize = new Size(1_000, 500);

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_xSpan", (float)imageSize.Width);
        SetPrivateField(imageViewer, "_ySpan", (float)imageSize.Height);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        imageViewer.GetCanvasOverlayManager().Clear();
        int lodChangedCount = 0;
        imageViewer.VisibleOverlayLodChanged += (_, _) => lodChangedCount++;

        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "full-render-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect",
                DisplayListId = 1
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, item);
        }
        loadStopwatch.Stop();

        InvokePrivateResult<object>(imageViewer, "CalculatorVisibleOverlays");
        Stopwatch rebuildStopwatch = Stopwatch.StartNew();
        InvokePrivateResult<object>(imageViewer, "CalculatorVisibleOverlays");
        rebuildStopwatch.Stop();
        List<CanvasShape> visibleShapes = GetPrivateField<List<CanvasShape>>(imageViewer, "_shapesViewPort");

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_FULL_VIEW_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} ROI_500K_FULL_VIEW_REBUILD_MS={rebuildStopwatch.Elapsed.TotalMilliseconds:F3} VISIBLE_SHAPES={visibleShapes.Count} LIMIT={expectedVisibleShapeLimit}"));

        AssertTrue(visibleShapes.Count > 0, "full-viewport ROI LOD should still keep visible ROI references");
        AssertTrue(visibleShapes.Count <= expectedVisibleShapeLimit, "full-viewport ROI rendering should cap the visual cache instead of keeping every visible ROI");
        AssertTrue(imageViewer.IsVisibleOverlayLodActive, "full-viewport ROI rendering should expose that display LOD is active");
        AssertEqual(expectedVisibleShapeLimit, imageViewer.VisibleOverlayShapeLimit);
        AssertTrue(lodChangedCount > 0, "full-viewport ROI rendering should notify the WPF status strip when display LOD becomes active");
        AssertTrue(rebuildStopwatch.Elapsed.TotalMilliseconds < 150.0, "full-viewport ROI visible cache rebuild should stay bounded at 500K objects");
    }

    private static void TestOpenGlRoiLargeObjectsUseCoarseSpatialIndex()
    {
        const int objectCount = 100_000;
        const float objectSize = 5_000F;
        const float spacing = 12_000F;
        const int columns = 400;

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        imageViewer.GetCanvasOverlayManager().Clear();

        CanvasRect<float> targetRect = null;
        OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem targetItem = null;
        int targetIndex = objectCount / 2;

        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int column = index % columns;
            int row = index / columns;
            float left = column * spacing;
            float bottom = row * spacing;
            var rect = new CanvasRect<float>(left, bottom + objectSize, left + objectSize, bottom)
            {
                UniqueId = "large-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect"
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, item);

            if (index == targetIndex)
            {
                targetRect = rect;
                targetItem = item;
            }
        }
        loadStopwatch.Stop();

        AssertTrue(targetRect != null && targetItem != null, "large ROI target was not created");
        PointF hitPoint = new PointF(targetRect.Left + (targetRect.Width / 2F), targetRect.Bottom + (targetRect.Height / 2F));
        _ = imageViewer.FindInteractiveOverlaysNearPoint(hitPoint, 8F);

        Stopwatch queryStopwatch = Stopwatch.StartNew();
        List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem> candidates = imageViewer.FindInteractiveOverlaysNearPoint(hitPoint, 8F);
        queryStopwatch.Stop();

        object spatialIndex = GetPrivateField<object>(imageViewer.GetCanvasOverlayManager(), "_spatialIndex");
        FieldInfo largeCellsField = spatialIndex.GetType().GetField("_largeCells", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo globalItemsField = spatialIndex.GetType().GetField("_globalItems", BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(largeCellsField != null && globalItemsField != null, "large ROI spatial index should expose coarse buckets for regression verification");
        var largeCells = largeCellsField.GetValue(spatialIndex) as System.Collections.IDictionary;
        object globalItems = globalItemsField.GetValue(spatialIndex);
        int globalItemCount = (int)globalItems.GetType().GetProperty("Count").GetValue(globalItems);
        AssertTrue(largeCells != null && largeCells.Count > 0, "large ROI objects should be stored in coarse spatial buckets");
        AssertEqual(0, globalItemCount);

        targetRect.UpdateRectangle(targetRect.Left + 128F, targetRect.Top, targetRect.Right + 128F, targetRect.Bottom);
        Stopwatch updateStopwatch = Stopwatch.StartNew();
        imageViewer.GetCanvasOverlayManager().UpdateInteractiveOverlayIndex(targetRect);
        updateStopwatch.Stop();

        PointF movedHitPoint = new PointF(targetRect.Left + (targetRect.Width / 2F), targetRect.Bottom + (targetRect.Height / 2F));
        List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem> movedCandidates = imageViewer.FindInteractiveOverlaysNearPoint(movedHitPoint, 8F);

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_LARGE_INDEX_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} ROI_LARGE_CANDIDATES={candidates.Count} ROI_LARGE_QUERY_MS={queryStopwatch.Elapsed.TotalMilliseconds:F3} ROI_LARGE_UPDATE_MS={updateStopwatch.Elapsed.TotalMilliseconds:F3} LARGE_CELLS={largeCells.Count} GLOBAL_ITEMS={globalItemCount}"));

        AssertTrue(candidates.Any(candidate => ReferenceEquals(candidate, targetItem)), "large ROI query should return the clicked object");
        AssertTrue(movedCandidates.Any(candidate => ReferenceEquals(candidate, targetItem)), "large ROI should remain queryable after a single-object spatial index update");
        AssertTrue(candidates.Count < objectCount / 100, "large ROI query should not return the full large-object set");
        AssertTrue(queryStopwatch.Elapsed.TotalMilliseconds < 20.0, "large ROI point query should stay interactive without scanning every large object");
        AssertTrue(updateStopwatch.Elapsed.TotalMilliseconds < 20.0, "single large ROI spatial update should stay incremental");
    }

    private static void TestOpenGlRoiDenseOverlapHitTestStaysInteractive()
    {
        const int objectCount = 50_000;
        var hitPoint = new PointF(5_000F, 5_000F);

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        imageViewer.GetCanvasOverlayManager().Clear();

        CanvasRect<float> smallestRect = null;
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            float size = objectCount - index + 4F;
            float half = size / 2F;
            var rect = new CanvasRect<float>(hitPoint.X - half, hitPoint.Y + half, hitPoint.X + half, hitPoint.Y - half)
            {
                UniqueId = "overlap-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect"
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, item);
            smallestRect = rect;
        }
        loadStopwatch.Stop();

        AssertTrue(smallestRect != null, "dense-overlap ROI target was not created");
        Stopwatch firstHitStopwatch = Stopwatch.StartNew();
        var (hitRect, isGroupOverlay) = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, hitPoint);
        firstHitStopwatch.Stop();

        Stopwatch repeatHitStopwatch = Stopwatch.StartNew();
        var (repeatHitRect, _) = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, hitPoint);
        repeatHitStopwatch.Stop();

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_OVERLAP_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} ROI_OVERLAP_FIRST_HIT_MS={firstHitStopwatch.Elapsed.TotalMilliseconds:F3} ROI_OVERLAP_REPEAT_HIT_MS={repeatHitStopwatch.Elapsed.TotalMilliseconds:F3} OBJECTS={objectCount} SELECTED_SIZE={hitRect?.Width:F1}"));

        AssertTrue(!isGroupOverlay, "dense-overlap hit-test should select a concrete ROI, not a group rectangle");
        AssertTrue(ReferenceEquals(smallestRect, hitRect), "dense-overlap hit-test should prefer the smallest ROI at the clicked point");
        AssertTrue(ReferenceEquals(smallestRect, repeatHitRect), "dense-overlap repeat hit-test should keep selecting the smallest ROI");
        AssertTrue(firstHitStopwatch.Elapsed.TotalMilliseconds < 20.0, "first dense-overlap ROI hit-test should stay interactive without list-allocation spikes");
        AssertTrue(repeatHitStopwatch.Elapsed.TotalMilliseconds < 20.0, "repeat dense-overlap ROI hit-test should stay interactive without list-allocation spikes");
    }

    private static void TestDetectionOverlayHitTestUsesSpatialIndexAt500KCandidates()
    {
        const int candidateCount = 500_000;
        var imageSize = new Size(12_000, 6_000);
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("Detection500K");
        SetPrivateField(viewModel, "_imageSize", imageSize);

        var overlays = new List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasDetectionOverlay>(candidateCount);
        for (int index = 0; index < candidateCount; index++)
        {
            int column = index % 1000;
            int row = index / 1000;
            overlays.Add(new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasDetectionOverlay
            {
                Index = index,
                Bounds = new Rectangle(column * 10, row * 10, 4, 4),
                Label = string.Empty,
                Color = Color.DeepSkyBlue
            });
        }

        int targetIndex = candidateCount - 1;
        Rectangle target = overlays[targetIndex].Bounds;
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        viewModel.SetDetectionOverlays(overlays);
        loadStopwatch.Stop();

        var hitPoint = new PointF(target.Left + 1F, imageSize.Height - (target.Top + 1F));
        viewModel.TryGetDetectionOverlayIndexAtCanvasPoint(hitPoint, out _);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool hit = false;
        int hitIndex = -1;
        var hitTimings = new List<double>();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Stopwatch hitStopwatch = Stopwatch.StartNew();
            hit = viewModel.TryGetDetectionOverlayIndexAtCanvasPoint(hitPoint, out hitIndex);
            hitStopwatch.Stop();
            hitTimings.Add(hitStopwatch.Elapsed.TotalMilliseconds);
        }

        hitTimings.Sort();
        double medianHitMilliseconds = hitTimings[hitTimings.Count / 2];
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"DETECTION_500K_INDEXED_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} DETECTION_500K_HIT_MS={medianHitMilliseconds:F3} DETECTION_500K_HIT_MAX_MS={hitTimings[hitTimings.Count - 1]:F3} HIT_INDEX={hitIndex}"));

        AssertTrue(hit, "detection overlay spatial hit-test should find the target candidate");
        AssertEqual(targetIndex, hitIndex);
        AssertTrue(medianHitMilliseconds < 20.0, "detection overlay hit-test should stay interactive at 500K candidates");
    }

    private static void TestDetectionOverlayRenderingUsesSpatialIndexAt500KCandidates()
    {
        const int candidateCount = 500_000;
        var imageSize = new Size(12_000, 6_000);
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("DetectionRender500K");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", new SizeF(0F, -5_900F));
        SetPrivateField(viewModel, "_imageSize", imageSize);
        imageViewer.TextureAreas.TryAdd(
            "detection-render",
            new List<OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam>
            {
                new OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam
                {
                    ImageName = "detection-render",
                    GLDrawingTextureArea = new RectangleF(0F, imageSize.Height, imageSize.Width, -imageSize.Height),
                    GLTextureArea = new RectangleF(0F, 0F, imageSize.Width, imageSize.Height),
                    ImageTexutreArea = new Rectangle(0, 0, imageSize.Width, imageSize.Height),
                    TextureFullScreen = imageSize,
                    TitleSize = imageSize
                }
            });

        var overlays = CreateDetectionOverlayGrid(candidateCount);
        viewModel.SetDetectionOverlays(overlays);
        _ = InvokePrivateResult<List<int>>(viewModel, "GetVisibleDetectionOverlayIndicesForRendering", 100F, 100F);

        Stopwatch renderCullStopwatch = Stopwatch.StartNew();
        List<int> visibleIndices = InvokePrivateResult<List<int>>(viewModel, "GetVisibleDetectionOverlayIndicesForRendering", 100F, 100F);
        renderCullStopwatch.Stop();

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"DETECTION_500K_RENDER_CANDIDATES={visibleIndices.Count} DETECTION_500K_RENDER_CULL_MS={renderCullStopwatch.Elapsed.TotalMilliseconds:F3}"));

        AssertTrue(visibleIndices.Count > 0, "detection overlay render culling should keep visible candidates");
        AssertTrue(visibleIndices.Count < candidateCount / 100, "detection overlay render culling should not return the full candidate set");
        AssertTrue(renderCullStopwatch.Elapsed.TotalMilliseconds < 20.0, "detection overlay render culling should stay interactive at 500K candidates");
    }

    private static void TestSegmentationOverlayRenderingUsesSpatialIndex()
    {
        const int overlayCount = 100_000;
        var imageSize = new Size(12_000, 6_000);
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("SegmentationRenderIndex");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", new SizeF(0F, -5_900F));
        SetPrivateField(viewModel, "_imageSize", imageSize);
        imageViewer.TextureAreas.TryAdd(
            "segmentation-render",
            new List<OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam>
            {
                new OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam
                {
                    ImageName = "segmentation-render",
                    GLDrawingTextureArea = new RectangleF(0F, imageSize.Height, imageSize.Width, -imageSize.Height),
                    GLTextureArea = new RectangleF(0F, 0F, imageSize.Width, imageSize.Height),
                    ImageTexutreArea = new Rectangle(0, 0, imageSize.Width, imageSize.Height),
                    TextureFullScreen = imageSize,
                    TitleSize = imageSize
                }
            });

        viewModel.SetPolygonOverlays(CreatePolygonOverlayGrid(overlayCount));
        viewModel.SetMaskOverlays(CreateMaskOverlayGrid(overlayCount, imageSize));
        _ = InvokePrivateResult<List<int>>(viewModel, "GetVisiblePolygonOverlayIndicesForRendering", 100F, 100F);
        _ = InvokePrivateResult<List<int>>(viewModel, "GetVisibleMaskOverlayIndicesForRendering", 100F, 100F);

        Stopwatch polygonStopwatch = Stopwatch.StartNew();
        List<int> visiblePolygonIndices = InvokePrivateResult<List<int>>(viewModel, "GetVisiblePolygonOverlayIndicesForRendering", 100F, 100F);
        polygonStopwatch.Stop();
        Stopwatch maskStopwatch = Stopwatch.StartNew();
        List<int> visibleMaskIndices = InvokePrivateResult<List<int>>(viewModel, "GetVisibleMaskOverlayIndicesForRendering", 100F, 100F);
        maskStopwatch.Stop();

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"SEGMENTATION_RENDER_POLYGON_CANDIDATES={visiblePolygonIndices.Count} POLYGON_CULL_MS={polygonStopwatch.Elapsed.TotalMilliseconds:F3} MASK_CANDIDATES={visibleMaskIndices.Count} MASK_CULL_MS={maskStopwatch.Elapsed.TotalMilliseconds:F3}"));

        AssertTrue(visiblePolygonIndices.Count > 0, "polygon render culling should keep visible overlays");
        AssertTrue(visibleMaskIndices.Count > 50, "mask render culling should keep the visible image-window overlays");
        AssertTrue(visiblePolygonIndices.Count < overlayCount / 100, "polygon render culling should not return the full overlay set");
        AssertTrue(visibleMaskIndices.Count < overlayCount / 100, "mask render culling should not return the full overlay set");
        AssertTrue(polygonStopwatch.Elapsed.TotalMilliseconds < 20.0, "polygon render culling should stay interactive at 100K overlays");
        AssertTrue(maskStopwatch.Elapsed.TotalMilliseconds < 20.0, "mask render culling should stay interactive at 100K overlays");
    }

    private static List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasDetectionOverlay> CreateDetectionOverlayGrid(int candidateCount)
    {
        var overlays = new List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasDetectionOverlay>(candidateCount);
        for (int index = 0; index < candidateCount; index++)
        {
            int column = index % 1000;
            int row = index / 1000;
            overlays.Add(new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasDetectionOverlay
            {
                Index = index,
                Bounds = new Rectangle(column * 10, row * 10, 4, 4),
                Label = string.Empty,
                Color = Color.DeepSkyBlue
            });
        }

        return overlays;
    }

    private static List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasPolygonOverlay> CreatePolygonOverlayGrid(int overlayCount)
    {
        var overlays = new List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasPolygonOverlay>(overlayCount);
        for (int index = 0; index < overlayCount; index++)
        {
            int column = index % 1000;
            int row = index / 1000;
            int x = column * 10;
            int y = row * 10;
            overlays.Add(new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasPolygonOverlay(
                new[]
                {
                    new Point(x, y),
                    new Point(x + 4, y),
                    new Point(x + 4, y + 4),
                    new Point(x, y + 4)
                },
                string.Empty,
                Color.DeepSkyBlue,
                isClosed: true));
        }

        return overlays;
    }

    private static List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasMaskOverlay> CreateMaskOverlayGrid(int overlayCount, Size imageSize)
    {
        var overlays = new List<OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasMaskOverlay>(overlayCount);
        byte[] maskData = new byte[imageSize.Width * imageSize.Height];
        for (int index = 0; index < overlayCount; index++)
        {
            int column = index % 1000;
            int row = index / 1000;
            overlays.Add(new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasMaskOverlay(
                $"mask-{index.ToString(CultureInfo.InvariantCulture)}",
                maskData,
                imageSize,
                new Rectangle(column * 10, row * 10, 4, 4),
                Color.DeepSkyBlue,
                opacity: 0.45F,
                renderVersion: 1));
        }

        return overlays;
    }

    private static void TestRoi500KObjectSingleMovePerformance()
    {
        const int objectCount = 500_000;
        int changedCallbacks = 0;
        int editCallbacks = 0;
        var imageSize = new Size(10_000, 10_000);

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        imageViewer.GetCanvasOverlayManager().Clear();

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = true,
            ChildObjects = new List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem>(objectCount)
        };

        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        Action changedAction = () => changedCallbacks++;
        CanvasRect<float> selectedRect = null;
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect",
                DisplayListId = 1,
                OnChanged = changedAction
            };

            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                Parent = parent,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            parent.ChildObjects.Add(item);

            if (index == objectCount / 2)
            {
                selectedRect = rect;
            }
        }
        loadStopwatch.Stop();

        AssertEqual(objectCount, parent.ChildObjects.Count);
        AssertTrue(selectedRect != null, "selected ROI was not created for the 500K performance test");

        imageViewer.PreMousePos = new PointF(selectedRect.Left, selectedRect.Bottom);
        OpenVisionLab.ImageCanvas.RoiInteractionMouseMove.MoveOverlay(
            imageViewer,
            selectedRect,
            new PointF(selectedRect.Left + 1F, selectedRect.Bottom),
            imageSize,
            canMoveRoi: true,
            callbackOverlayEditingComleted: _ => editCallbacks++);

        changedCallbacks = 0;
        editCallbacks = 0;
        imageViewer.PreMousePos = new PointF(selectedRect.Left, selectedRect.Bottom);

        Stopwatch moveStopwatch = Stopwatch.StartNew();
        OpenVisionLab.ImageCanvas.RoiInteractionMouseMove.MoveOverlay(
            imageViewer,
            selectedRect,
            new PointF(selectedRect.Left + 1F, selectedRect.Bottom),
            imageSize,
            canMoveRoi: true,
            callbackOverlayEditingComleted: _ => editCallbacks++);
        moveStopwatch.Stop();

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} ROI_500K_SINGLE_MOVE_MS={moveStopwatch.Elapsed.TotalMilliseconds:F3} CHANGED_CALLBACKS={changedCallbacks} EDIT_CALLBACKS={editCallbacks} OBJECTS={parent.ChildObjects.Count}"));

        AssertEqual(1, changedCallbacks);
        AssertEqual(1, editCallbacks);
        AssertTrue(moveStopwatch.Elapsed.TotalMilliseconds < 50.0, "moving one ROI among 500K objects should not scan or update the whole object set");

        parent.IsVisible = false;
        SetPrivateField(imageViewer, "_visibleOverlayCacheDirty", false);
        SetPrivateField(imageViewer, "_shapesViewPort", new List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasShape>());
        SetPrivateField(imageViewer, "_xSpan", (float)imageSize.Width);
        SetPrivateField(imageViewer, "_ySpan", (float)imageSize.Height);
        SetPrivateField(imageViewer, "_offsetSize", new SizeF(0F, 0F));

        var newRect = new CanvasRect<float>(25, 26, 26, 25)
        {
            UniqueId = "roi-new",
            GroupType = "Defect",
            DisplayListId = 1
        };

        Stopwatch addStopwatch = Stopwatch.StartNew();
        OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlOverlayExtensions.AddOverlay(imageViewer, parent.GroupType, parent.GroupType, newRect, newRect.UniqueId, parent.InspWindowType, OpenVisionLab.ImageCanvas.EnumItemType.Window);
        imageViewer.RefreshGL();
        addStopwatch.Stop();

        bool visibleCacheDirty = GetPrivateField<bool>(imageViewer, "_visibleOverlayCacheDirty");
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_INCREMENTAL_ADD_MS={addStopwatch.Elapsed.TotalMilliseconds:F3} VISIBLE_CACHE_DIRTY={visibleCacheDirty} OBJECTS={parent.ChildObjects.Count}"));

        AssertEqual(objectCount + 1, parent.ChildObjects.Count);
        AssertTrue(!visibleCacheDirty, "adding one ROI while group bounds are hidden should not dirty the full visible-overlay cache");
        AssertTrue(addStopwatch.Elapsed.TotalMilliseconds < 50.0, "adding one ROI among 500K objects should append the new object instead of rebuilding every visible overlay");
    }

    private static void TestOpenGlRoiDeleteUsesIncrementalPathAt500KObjects()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(1_000, 1_000);

        using var imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
        imageViewer.Size = new Size(100, 100);
        imageViewer.GetCanvasOverlayManager().Clear();
        SetPrivateField(imageViewer, "_visibleOverlayCacheDirty", false);
        SetPrivateField(imageViewer, "_shapesViewPort", new List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasShape>());
        SetPrivateField(imageViewer, "_xSpan", (float)imageSize.Width);
        SetPrivateField(imageViewer, "_ySpan", (float)imageSize.Height);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "delete-root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = true,
            ChildObjects = new List<OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem>(objectCount)
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        CanvasRect<float> targetRect = null;
        string targetUniqueId = string.Empty;
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < objectCount; index++)
        {
            int x = index % imageSize.Width;
            int y = index / imageSize.Width;
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "delete-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = parent.GroupType
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = parent.GroupType,
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(parent.GroupType, item);
            if (index == objectCount / 2)
            {
                targetRect = rect;
                targetUniqueId = rect.UniqueId;
            }
        }
        loadStopwatch.Stop();

        AssertTrue(targetRect != null, "target ROI was not created for the delete performance test");
        SetPrivateField(imageViewer, "_shapesViewPort", new List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasShape> { targetRect });
        SetPrivateField(imageViewer, "_visibleOverlayCacheDirty", false);

        Stopwatch deleteStopwatch = Stopwatch.StartNew();
        OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlOverlayExtensions.DeleteOverlay(imageViewer, targetUniqueId, parent.GroupType);
        deleteStopwatch.Stop();

        bool visibleCacheDirty = GetPrivateField<bool>(imageViewer, "_visibleOverlayCacheDirty");
        List<CanvasShape> visibleShapes = GetPrivateField<List<CanvasShape>>(imageViewer, "_shapesViewPort");
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_SINGLE_DELETE_MS={deleteStopwatch.Elapsed.TotalMilliseconds:F3} ROI_500K_DELETE_LOAD_MS={loadStopwatch.Elapsed.TotalMilliseconds:F1} VISIBLE_CACHE_DIRTY={visibleCacheDirty} VISIBLE_SHAPES={visibleShapes.Count} OBJECTS={parent.ChildObjects.Count}"));

        AssertEqual(objectCount - 1, parent.ChildObjects.Count);
        AssertTrue(imageViewer.GetCanvasOverlayManager().GetOverlayByUniqueId(targetUniqueId) == null, "deleted ROI should be removed from the overlay manager index");
        AssertTrue(!visibleCacheDirty, "deleting one ROI from a large visible group should not dirty the full visible-overlay cache");
        AssertEqual(0, visibleShapes.Count);
        AssertTrue(deleteStopwatch.Elapsed.TotalMilliseconds < 80.0, "deleting one ROI among 500K objects should not recalculate every group bound or redraw every ROI");
    }

    private static void TestRoi500KMouseEventMovePerformance()
    {
        const int objectCount = 500_000;
        var imageSize = new Size(1_000, 1_000);
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("RoiMouseEvent500K");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        ((Control)openGlControl).CreateControl();
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", SizeF.Empty);
        SetPrivateField(viewModel, "_imageSize", imageSize);
        imageViewer.GetCanvasOverlayManager().Clear();

        var parentShape = new CanvasRect<float>(0, imageSize.Height, imageSize.Width, 0)
        {
            UniqueId = "mouse-event-root-defect",
            GroupType = "Defect"
        };
        var parent = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
        {
            GroupType = "Defect",
            Shape = parentShape,
            ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Group,
            InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Module,
            IsVisible = true
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(string.Empty, parent);

        for (int index = 0; index < objectCount - 2; index++)
        {
            int x = index % imageSize.Width;
            int y = 500 + (index / imageSize.Width);
            var rect = new CanvasRect<float>(x, y + 1, x + 1, y)
            {
                UniqueId = "mouse-event-roi-" + index.ToString(CultureInfo.InvariantCulture),
                GroupType = "Defect"
            };
            var item = new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = "Defect",
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            };
            imageViewer.GetCanvasOverlayManager().AddOverlayItem(parent.GroupType, item);
        }

        var targetRect = new CanvasRect<float>(45, 65, 65, 45)
        {
            UniqueId = "mouse-event-target",
            GroupType = "Defect",
            ShapeKind = CanvasRoiShapeKind.Rectangle
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(
            parent.GroupType,
            new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = parent.GroupType,
                Shape = targetRect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            });

        var resizeTargetRect = new CanvasRect<float>(10, 90, 30, 70)
        {
            UniqueId = "mouse-event-resize-target",
            GroupType = "Defect",
            ShapeKind = CanvasRoiShapeKind.Rectangle
        };
        imageViewer.GetCanvasOverlayManager().AddOverlayItem(
            parent.GroupType,
            new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = parent.GroupType,
                Shape = resizeTargetRect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = OpenVisionLab.ImageCanvas.EnumInspWindowType.Unit,
                IsVisible = true
            });
        AssertEqual(objectCount, parent.ChildObjects.Count);

        var (hitRect, _) = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, new PointF(55F, 55F));
        AssertTrue(ReferenceEquals(targetRect, hitRect), "mouse-event performance test should select the large target ROI among 500K objects");
        var (resizeHitRect, _) = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, new PointF(10F, 90F));
        AssertTrue(ReferenceEquals(resizeTargetRect, resizeHitRect), "mouse-event performance test should select the resize target ROI corner among 500K objects");

        int editingCompletedCount = 0;
        viewModel.RoiEditingCompleted += (_, _) => editingCompletedCount++;
        viewModel.IsTeachingMode = true;
        viewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;

        MethodInfo mouseDownMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseDown",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        MethodInfo mouseMoveMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseMove",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        MethodInfo mouseUpMethod = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod(
                "OnMouseUp",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(object), typeof(MouseEventArgs) },
                modifiers: null);
        AssertTrue(mouseDownMethod != null && mouseMoveMethod != null && mouseUpMethod != null, "OpenGL canvas mouse handlers should be testable");

        mouseDownMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 1, 55, 45, 0) });
        AssertTrue(targetRect.IsEditing, "target ROI should be selected on mouse-down before move events");
        AssertTrue(
            imageViewer.GetViewMode() == OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Move
            || imageViewer.GetViewMode() == OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Edit,
            "mouse-down should enter ROI manipulation mode before move events");

        int displayListRebuildCount = 0;
        targetRect.OnChanged = () => displayListRebuildCount++;

        Stopwatch moveStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            int x = 56 + (index % 12);
            mouseMoveMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 0, x, 45, 0) });
        }

        moveStopwatch.Stop();
        int displayListRebuildsDuringMove = displayListRebuildCount;
        int editingCallbacksDuringMove = editingCompletedCount;
        mouseUpMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 1, 67, 45, 0) });
        int editingCompletedAfterMove = editingCompletedCount;

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_MOUSE_EVENT_MOVE_1000_MS={moveStopwatch.Elapsed.TotalMilliseconds:F3} DISPLAY_REBUILDS_DURING_MOVE={displayListRebuildsDuringMove} EDIT_CALLBACKS_DURING_MOVE={editingCallbacksDuringMove} DISPLAY_REBUILDS_TOTAL={displayListRebuildCount} EDIT_CALLBACKS_TOTAL={editingCompletedCount}"));

        AssertEqual(0, displayListRebuildsDuringMove);
        AssertEqual(0, editingCallbacksDuringMove);
        AssertTrue(displayListRebuildCount > 0, "ROI mouse-up should rebuild the moved ROI display list once after fast MouseMove events");
        AssertTrue(editingCompletedCount > 0, "ROI mouse-up should report one committed edit after fast MouseMove events");
        AssertTrue(moveStopwatch.Elapsed.TotalMilliseconds < 300.0, "actual ROI MouseMove event path should stay lightweight at 500K objects");

        int resizeDisplayListRebuildCount = 0;
        resizeTargetRect.OnChanged = () => resizeDisplayListRebuildCount++;
        mouseDownMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
        AssertTrue(resizeTargetRect.IsEditing, "resize target ROI should be selected on mouse-down before resize events");
        AssertEqual(OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Edit, imageViewer.GetViewMode());
        resizeDisplayListRebuildCount = 0;

        Stopwatch resizeStopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            int x = 5 + (index % 12);
            mouseMoveMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 0, x, 5, 0) });
        }

        resizeStopwatch.Stop();
        int resizeDisplayListRebuildsDuringMove = resizeDisplayListRebuildCount;
        int resizeEditingCallbacksDuringMove = editingCompletedCount - editingCompletedAfterMove;
        mouseUpMethod.Invoke(imageViewer, new object[] { openGlControl, new MouseEventArgs(MouseButtons.Left, 1, 16, 5, 0) });
        int resizeEditingCallbacksTotal = editingCompletedCount - editingCompletedAfterMove;

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"ROI_500K_MOUSE_EVENT_RESIZE_1000_MS={resizeStopwatch.Elapsed.TotalMilliseconds:F3} DISPLAY_REBUILDS_DURING_RESIZE={resizeDisplayListRebuildsDuringMove} EDIT_CALLBACKS_DURING_RESIZE={resizeEditingCallbacksDuringMove} DISPLAY_REBUILDS_RESIZE_TOTAL={resizeDisplayListRebuildCount} EDIT_CALLBACKS_RESIZE_TOTAL={resizeEditingCallbacksTotal}"));

        AssertEqual(0, resizeDisplayListRebuildsDuringMove);
        AssertEqual(0, resizeEditingCallbacksDuringMove);
        AssertTrue(resizeDisplayListRebuildCount > 0, "ROI mouse-up should rebuild the resized ROI display list once after fast MouseMove events");
        AssertTrue(resizeEditingCallbacksTotal > 0, "ROI mouse-up should report one committed resize after fast MouseMove events");
        AssertTrue(resizeStopwatch.Elapsed.TotalMilliseconds < 300.0, "actual ROI resize MouseMove event path should stay lightweight at 500K objects");
    }

    private static void TestRoiInteraction()
    {
        var rois = new List<CRectangleObject>
        {
            new CRectangleObject { Roi = new Rectangle(10, 10, 20, 20) }
        };

        bool selected = RoiInteractionController.TrySelect(
            rois,
            new Point(15, 15),
            4,
            out int selectedIndex,
            out var operation);

        AssertTrue(selected, "ROI was not selected");
        AssertEqual(0, selectedIndex);
        AssertEqual("SizeAll", operation.ToString());
        AssertTrue(rois[0].Selected, "ROI selected flag was not set");

        var roiGroups = new Dictionary<string, List<CRectangleObject>>
        {
            ["OK"] = rois
        };

        RoiInteractionController.ApplyImageBounds(roiGroups, new Size(200, 100));
        AssertEqual(200, rois[0]._MaxX);
        AssertEqual(100, rois[0]._MaxY);
        AssertEqual(new Size(200, 100), rois[0].OriginalSize);

        AssertTrue(RoiInteractionController.TryRemoveSelected(roiGroups, "OK"), "selected ROI was not removed");
        AssertEqual(0, rois.Count);

        rois.Add(new CRectangleObject { Roi = new Rectangle(0, 0, 40, 40) });
        rois.Add(new CRectangleObject { Roi = new Rectangle(5, 5, 40, 40) });
        selected = RoiInteractionController.TrySelect(
            rois,
            new Point(10, 10),
            8,
            out selectedIndex,
            out operation);
        AssertTrue(selected, "overlapped ROI was not selected");
        AssertEqual(1, selectedIndex);
    }

    private static void TestWpfAnnotationObjectVerificationProcess()
    {
        var viewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("Verify");
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(imageViewer, null);
        ((Control)openGlControl).Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", new SizeF(0F, 0F));
        SetPrivateField(viewModel, "_imageSize", new Size(100, 100));
        imageViewer.TextureAreas.TryAdd(
            "verify",
            new List<OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam>
            {
                new OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam
                {
                    ImageName = "verify",
                    GLDrawingTextureArea = new RectangleF(0F, 100F, 100F, -100F),
                    GLTextureArea = new RectangleF(0F, 0F, 100F, 100F),
                    ImageTexutreArea = new Rectangle(0, 0, 100, 100),
                    TextureFullScreen = new Size(100, 100),
                    TitleSize = new Size(100, 100)
                }
            });

        OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem parentGroup = imageViewer.GetCanvasOverlayManager().GetGroupToType(OpenVisionLab.ImageCanvas.EnumInspWindowType.Module.ToString());
        parentGroup.IsVisible = false;
        var existingRectangle = AddAnnotationVerificationOverlay(
            imageViewer,
            parentGroup,
            new CanvasRect<float>(10, 90, 40, 60)
            {
                UniqueId = "existing-rectangle",
                ShapeKind = CanvasRoiShapeKind.Rectangle
            });

        var (hitRectangle, _) = OpenVisionLab.ImageCanvas.RoiInteractionMouseDown.FindOverlayAtPosition(imageViewer, new PointF(20, 80));
        AssertTrue(ReferenceEquals(existingRectangle, hitRectangle), "rectangle interior should be selectable even when the debug group frame is hidden");

        int addedCount = 0;
        int mouseUpCount = 0;
        int editingCompletedCount = 0;
        CanvasRect<float> lastAdded = null;
        CanvasRect<float> lastMouseUp = null;
        CanvasRect<float> lastEdited = null;
        viewModel.RoiAdded += (_, e) =>
        {
            addedCount++;
            lastAdded = e.RoiRect;
        };
        viewModel.RoiMouseUp += (_, e) =>
        {
            mouseUpCount++;
            lastMouseUp = e.RoiRect;
        };
        viewModel.RoiEditingCompleted += (_, e) =>
        {
            editingCompletedCount++;
            lastEdited = e.RoiRect;
        };

        viewModel.IsTeachingMode = true;
        viewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
        int overlayCountBeforeSelection = CountAnnotationVerificationWindowOverlays(imageViewer);
        existingRectangle.IsEditing = false;
        existingRectangle.IsChanged = false;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));
        AssertEqual(overlayCountBeforeSelection, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(0, addedCount);
        AssertEqual(0, mouseUpCount);
        AssertTrue(existingRectangle.IsEditing, "rectangle interior mouse-down should immediately select the existing ROI");
        AssertTrue(existingRectangle.IsChanged, "rectangle interior mouse-down should invalidate only the clicked ROI display");
        AssertTrue(
            imageViewer.GetViewMode() == OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Move
            || imageViewer.GetViewMode() == OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Edit,
            "rectangle interior mouse-down should enter ROI manipulation mode before mouse move");
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));

        AssertEqual(overlayCountBeforeSelection, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(0, addedCount);
        AssertEqual(1, mouseUpCount);
        AssertTrue(ReferenceEquals(existingRectangle, lastMouseUp), "rectangle interior click should select the existing ROI instead of creating a new one");
        AssertTrue(existingRectangle.IsEditing, "selected rectangle should keep edit handles visible after click");
        AssertEqual(OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Drawing, imageViewer.GetViewMode());

        int editingCompletedBeforeRectangleMove = editingCompletedCount;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 25, 25, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, 35, 30, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 35, 30, 0));

        AssertEqual(overlayCountBeforeSelection, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(0, addedCount);
        AssertTrue(editingCompletedCount > editingCompletedBeforeRectangleMove, "rectangle interior drag should report editing completion");
        AssertTrue(ReferenceEquals(existingRectangle, lastEdited), "rectangle interior drag should move the existing ROI");
        AssertCanvasRectBounds(20F, 85F, 50F, 55F, existingRectangle, "rectangle move");
        AssertTrue(existingRectangle.IsEditing, "moved rectangle should stay selected after mouse-up");

        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 90, 90, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 90, 90, 0));
        AssertTrue(!existingRectangle.IsEditing, "empty canvas click should clear the selected ROI edit handles");

        int clickedDetectionOverlayIndex = -1;
        viewModel.DetectionOverlayClicked += (_, index) => clickedDetectionOverlayIndex = index;
        viewModel.SetDetectionOverlays(new[]
        {
            new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasDetectionOverlay
            {
                Index = 3,
                Bounds = new Rectangle(80, 45, 10, 10),
                Label = "AI",
                Color = Color.DeepSkyBlue
            }
        });
        int overlayCountBeforeDetectionClick = CountAnnotationVerificationWindowOverlays(imageViewer);
        int addedBeforeDetectionClick = addedCount;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 85, 50, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, 95, 60, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 95, 60, 0));

        AssertEqual(3, clickedDetectionOverlayIndex);
        AssertEqual(overlayCountBeforeDetectionClick, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(addedBeforeDetectionClick, addedCount);
        AssertEqual(OpenVisionLab.ImageCanvas.Canvas.CanvasInteractionMode.Drawing, imageViewer.GetViewMode());

        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 15, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 15, 0));
        int editingCompletedBeforeRectangleResize = editingCompletedCount;
        int resizeDisplayListRebuildCount = 0;
        existingRectangle.OnChanged = () => resizeDisplayListRebuildCount++;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 15, 0));
        resizeDisplayListRebuildCount = 0;
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, 15, 10, 0));
        AssertEqual(0, resizeDisplayListRebuildCount);
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 15, 10, 0));
        AssertTrue(resizeDisplayListRebuildCount > 0, "rectangle resize should rebuild the display list once on mouse-up, not during MouseMove");

        AssertEqual(overlayCountBeforeSelection, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(0, addedCount);
        AssertTrue(editingCompletedCount > editingCompletedBeforeRectangleResize, "rectangle corner drag should report editing completion");
        AssertTrue(ReferenceEquals(existingRectangle, lastEdited), "rectangle corner drag should resize the existing ROI");
        AssertCanvasRectBounds(15F, 90F, 50F, 55F, existingRectangle, "rectangle resize");
        AssertTrue(existingRectangle.IsEditing, "resized rectangle should stay selected after mouse-up");

        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 75, 15, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, 95, 35, 0));
        CanvasRect<float> drawingPreviewRect = GetPrivateField<CanvasRect<float>>(viewModel, "_drawingRect");
        AssertTrue(drawingPreviewRect != null && !drawingPreviewRect.IsEmpty(), "rectangle drag should create a lightweight preview before mouse-up");
        AssertEqual(0, addedCount);
        AssertEqual(overlayCountBeforeSelection, CountAnnotationVerificationWindowOverlays(imageViewer));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 95, 35, 0));

        AssertEqual(1, addedCount);
        AssertEqual(CanvasRoiShapeKind.Rectangle, lastAdded.ShapeKind);
        AssertEqual(overlayCountBeforeSelection + 1, CountAnnotationVerificationWindowOverlays(imageViewer));

        viewModel.DrawingShapeKind = CanvasRoiShapeKind.Ellipse;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 60, 65, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 90, 95, 0));

        AssertEqual(2, addedCount);
        AssertEqual(CanvasRoiShapeKind.Ellipse, lastAdded.ShapeKind);
        AssertTrue(lastAdded.IsFill, "ellipse/circle ROI should be filled");

        int overlayCountBeforeEllipseSelection = CountAnnotationVerificationWindowOverlays(imageViewer);
        int addedBeforeEllipseSelection = addedCount;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 75, 80, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 75, 80, 0));

        AssertEqual(overlayCountBeforeEllipseSelection, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(addedBeforeEllipseSelection, addedCount);
        AssertEqual(CanvasRoiShapeKind.Ellipse, lastMouseUp.ShapeKind);
        AssertTrue(lastMouseUp.IsEditing, "ellipse/circle interior click should select the existing ROI and keep edit handles visible");

        var editableEllipse = AddAnnotationVerificationOverlay(
            imageViewer,
            parentGroup,
            new CanvasRect<float>(5, 55, 35, 15)
            {
                UniqueId = "editable-ellipse",
                ShapeKind = CanvasRoiShapeKind.Ellipse,
                IsFill = true
            });
        int overlayCountBeforeEllipseMove = CountAnnotationVerificationWindowOverlays(imageViewer);
        int addedBeforeEllipseMove = addedCount;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 65, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 65, 0));
        int editingCompletedBeforeEllipseMove = editingCompletedCount;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 20, 65, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, 25, 70, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 25, 70, 0));

        AssertEqual(overlayCountBeforeEllipseMove, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(addedBeforeEllipseMove, addedCount);
        AssertTrue(editingCompletedCount > editingCompletedBeforeEllipseMove, "ellipse/circle interior drag should report editing completion");
        AssertTrue(ReferenceEquals(editableEllipse, lastEdited), "ellipse/circle interior drag should move the existing ROI");
        AssertCanvasRectBounds(10F, 50F, 40F, 10F, editableEllipse, "ellipse/circle move");
        AssertTrue(editableEllipse.IsEditing, "moved ellipse/circle should stay selected after mouse-up");

        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 40, 90, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 40, 90, 0));
        int editingCompletedBeforeEllipseResize = editingCompletedCount;
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 40, 90, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, 45, 95, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, 45, 95, 0));

        AssertEqual(overlayCountBeforeEllipseMove, CountAnnotationVerificationWindowOverlays(imageViewer));
        AssertEqual(addedBeforeEllipseMove, addedCount);
        AssertTrue(editingCompletedCount > editingCompletedBeforeEllipseResize, "ellipse/circle corner drag should report editing completion");
        AssertTrue(ReferenceEquals(editableEllipse, lastEdited), "ellipse/circle corner drag should resize the existing ROI");
        AssertCanvasRectBounds(10F, 50F, 45F, 5F, editableEllipse, "ellipse/circle resize");
        AssertTrue(editableEllipse.IsEditing, "resized ellipse/circle should stay selected after mouse-up");

        string verificationDocPath = Path.Combine(FindRepositoryRoot(), "docs", "WPF_ANNOTATION_OBJECT_VERIFICATION.md");
        AssertTrue(File.Exists(verificationDocPath), "annotation object verification process should be documented");
        string verificationDoc = File.ReadAllText(verificationDocPath);
        AssertTrue(verificationDoc.Contains("Rectangle", StringComparison.Ordinal), "verification process should include rectangle");
        AssertTrue(verificationDoc.Contains("Ellipse/Circle", StringComparison.Ordinal), "verification process should include ellipse/circle");
        AssertTrue(verificationDoc.Contains("inside click selects", StringComparison.Ordinal), "verification process should require inside-click selection");
        AssertTrue(verificationDoc.Contains("move/resize coordinate delta", StringComparison.Ordinal), "verification process should require coordinate-delta checks");
    }

    private static void TestWpfRoiObjectManipulationVerificationMatrix()
    {
        VerifyRoiDrag(
            "rectangle interior move",
            CanvasRoiShapeKind.Rectangle,
            40F,
            60F,
            50F,
            55F,
            new CanvasRect<float>(20, 80, 60, 40) { ShapeKind = CanvasRoiShapeKind.Rectangle },
            30F,
            75F,
            70F,
            35F);

        VerifyRoiDrag("rectangle left edge resize", CanvasRoiShapeKind.Rectangle, 20F, 60F, 10F, 60F, NewVerificationRect(), 10F, 80F, 60F, 40F);
        VerifyRoiDrag("rectangle right edge resize", CanvasRoiShapeKind.Rectangle, 60F, 60F, 70F, 60F, NewVerificationRect(), 20F, 80F, 70F, 40F);
        VerifyRoiDrag("rectangle top edge resize", CanvasRoiShapeKind.Rectangle, 40F, 80F, 40F, 90F, NewVerificationRect(), 20F, 90F, 60F, 40F);
        VerifyRoiDrag("rectangle bottom edge resize", CanvasRoiShapeKind.Rectangle, 40F, 40F, 40F, 30F, NewVerificationRect(), 20F, 80F, 60F, 30F);
        VerifyRoiDrag("rectangle one-pixel-inside left body move", CanvasRoiShapeKind.Rectangle, 21F, 60F, 31F, 55F, NewVerificationRect(), 30F, 75F, 70F, 35F);
        VerifyRoiDrag("rectangle inside corner body move", CanvasRoiShapeKind.Rectangle, 22F, 78F, 32F, 73F, NewVerificationRect(), 30F, 75F, 70F, 35F);
        VerifyRoiDrag("rectangle left-top corner resize", CanvasRoiShapeKind.Rectangle, 20F, 80F, 15F, 90F, NewVerificationRect(), 15F, 90F, 60F, 40F);
        VerifyRoiDrag("rectangle right-top corner resize", CanvasRoiShapeKind.Rectangle, 60F, 80F, 70F, 90F, NewVerificationRect(), 20F, 90F, 70F, 40F);
        VerifyRoiDrag("rectangle right-bottom corner resize", CanvasRoiShapeKind.Rectangle, 60F, 40F, 70F, 30F, NewVerificationRect(), 20F, 80F, 70F, 30F);
        VerifyRoiDrag("rectangle left-bottom corner resize", CanvasRoiShapeKind.Rectangle, 20F, 40F, 10F, 30F, NewVerificationRect(), 10F, 80F, 60F, 30F);

        VerifyRoiDrag(
            "ellipse interior move",
            CanvasRoiShapeKind.Ellipse,
            40F,
            60F,
            50F,
            55F,
            new CanvasRect<float>(20, 80, 60, 40)
            {
                ShapeKind = CanvasRoiShapeKind.Ellipse,
                IsFill = true
            },
            30F,
            75F,
            70F,
            35F);
        VerifyRoiDrag(
            "ellipse right-bottom corner resize",
            CanvasRoiShapeKind.Ellipse,
            60F,
            40F,
            72F,
            28F,
            new CanvasRect<float>(20, 80, 60, 40)
            {
                ShapeKind = CanvasRoiShapeKind.Ellipse,
                IsFill = true
            },
            20F,
            80F,
            72F,
            28F);

        using (var harness = new RoiObjectVerificationHarness())
        {
            harness.ViewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
            int before = harness.WindowOverlayCount;
            harness.DragWorld(10F, 90F, 30F, 70F);
            AssertEqual(before + 1, harness.WindowOverlayCount);
            AssertEqual(1, harness.AddedCount);
            AssertEqual(CanvasRoiShapeKind.Rectangle, harness.LastAdded.ShapeKind);
            AssertCanvasRectBounds(10F, 90F, 30F, 70F, harness.LastAdded, "empty rectangle draw");
        }

        using (var harness = new RoiObjectVerificationHarness())
        {
            harness.ViewModel.DrawingShapeKind = CanvasRoiShapeKind.Ellipse;
            int before = harness.WindowOverlayCount;
            harness.DragWorld(50F, 50F, 80F, 20F);
            AssertEqual(before + 1, harness.WindowOverlayCount);
            AssertEqual(1, harness.AddedCount);
            AssertEqual(CanvasRoiShapeKind.Ellipse, harness.LastAdded.ShapeKind);
            AssertTrue(harness.LastAdded.IsFill, "empty ellipse/circle draw should create a filled ROI");
            AssertTrue(harness.LastAdded.DisplayListId != 0, "newly created ellipse/circle ROI should be visible without requiring a follow-up click");
            AssertCanvasRectBounds(50F, 50F, 80F, 20F, harness.LastAdded, "empty ellipse draw");
        }

        using (var harness = new RoiObjectVerificationHarness())
        {
            CanvasRect<float> rect = harness.AddRoi(new CanvasRect<float>(5, 25, 25, 5)
            {
                ShapeKind = CanvasRoiShapeKind.Rectangle
            });
            int before = harness.WindowOverlayCount;
            harness.ClickWorld(15F, 15F);
            harness.DragWorld(15F, 15F, -5F, 15F);
            AssertEqual(before, harness.WindowOverlayCount);
            AssertEqual(0, harness.AddedCount);
            AssertCanvasRectBounds(0F, 25F, 20F, 5F, rect, "rectangle move clamps at image left boundary without resizing");
        }

        using (var harness = new RoiObjectVerificationHarness())
        {
            CanvasRect<float> large = harness.AddRoi(new CanvasRect<float>(10, 90, 90, 10)
            {
                ShapeKind = CanvasRoiShapeKind.Rectangle
            });
            CanvasRect<float> small = harness.AddRoi(new CanvasRect<float>(45, 55, 55, 45)
            {
                ShapeKind = CanvasRoiShapeKind.Rectangle
            });

            harness.ClickWorld(50F, 50F);
            AssertTrue(!ReferenceEquals(large, harness.LastMouseUp), "overlapping ROI click should not keep the broad background box selected");
            AssertTrue(ReferenceEquals(small, harness.LastMouseUp), "overlapping ROI click should select the smallest concrete ROI under the cursor");
        }

        using (var harness = new RoiObjectVerificationHarness())
        {
            harness.AddRoi(NewVerificationRect());
            harness.ClickWorld(21F, 60F);
            harness.HoverWorld(21F, 60F);
            AssertEqual(Cursors.SizeAll, ((Control)harness.OpenGlControl).Cursor);
            harness.HoverWorld(22F, 78F);
            AssertEqual(Cursors.SizeAll, ((Control)harness.OpenGlControl).Cursor);
            harness.HoverWorld(20F, 60F);
            AssertEqual(Cursors.VSplit, ((Control)harness.OpenGlControl).Cursor);
        }

        static CanvasRect<float> NewVerificationRect()
            => new CanvasRect<float>(20, 80, 60, 40) { ShapeKind = CanvasRoiShapeKind.Rectangle };

        static void VerifyRoiDrag(
            string name,
            CanvasRoiShapeKind expectedShapeKind,
            float startX,
            float startY,
            float endX,
            float endY,
            CanvasRect<float> sourceRect,
            float expectedLeft,
            float expectedTop,
            float expectedRight,
            float expectedBottom)
        {
            using var harness = new RoiObjectVerificationHarness();
            CanvasRect<float> rect = harness.AddRoi(sourceRect);
            harness.ClickWorld(startX, startY);
            int before = harness.WindowOverlayCount;
            int addedBefore = harness.AddedCount;
            int editingCompletedBefore = harness.EditingCompletedCount;
            harness.DragWorld(startX, startY, endX, endY);

            AssertEqual(before, harness.WindowOverlayCount);
            AssertEqual(addedBefore, harness.AddedCount);
            AssertTrue(harness.EditingCompletedCount > editingCompletedBefore, $"{name} should report editing completion");
            AssertTrue(ReferenceEquals(rect, harness.LastEdited), $"{name} should edit the same ROI instance");
            AssertEqual(expectedShapeKind, rect.ShapeKind);
            AssertCanvasRectBounds(expectedLeft, expectedTop, expectedRight, expectedBottom, rect, name);
        }
    }

    private static void TestWpfRoiObjectManipulationUpdatesShellState()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "roi-shell.jpg");
            using (Bitmap image = CreateSolidBitmap(100, 100, Color.FromArgb(32, 32, 32)))
            {
                image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                AssertTrue(window.TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false), "WPF shell image load failed for ROI object verification");
                ConfigureCanvasForRoiObjectVerification(window.MainCanvasViewModel);
                AddWpfSessionRoi(window, new Rectangle(20, 20, 40, 40), CanvasRoiShapeKind.Rectangle, new Size(100, 100), "shell-roi");
                InvokePrivateResult<object>(window, "RedrawReviewRois");

                var manualRois = GetPrivateField<List<Rectangle>>(window, "manualRois");
                var manualOverlayIds = GetPrivateField<List<string>>(window, "manualRoiOverlayIds");
                AssertEqual(1, manualRois.Count);
                AssertEqual(new Rectangle(20, 20, 40, 40), manualRois[0]);
                AssertTrue(!string.IsNullOrWhiteSpace(manualOverlayIds[0]), "redraw should bind manual ROI to a real canvas overlay id");

                object openGlControl = GetCanvasOpenGlControl(window.MainCanvasViewModel);
                int overlayCountBeforeMove = CountAnnotationVerificationWindowOverlays(window.MainCanvasViewModel.ImageViewer);
                DragCanvasWorld(window.MainCanvasViewModel, openGlControl, 40F, 60F, 45F, 55F);
                AssertEqual(overlayCountBeforeMove, CountAnnotationVerificationWindowOverlays(window.MainCanvasViewModel.ImageViewer));
                AssertEqual(new Rectangle(25, 25, 40, 40), manualRois[0]);

                DragCanvasWorld(window.MainCanvasViewModel, openGlControl, 65F, 55F, 70F, 55F);
                AssertEqual(overlayCountBeforeMove, CountAnnotationVerificationWindowOverlays(window.MainCanvasViewModel.ImageViewer));
                AssertEqual(new Rectangle(25, 25, 45, 40), manualRois[0]);

                Dictionary<string, List<CRectangleObject>> roisByClass = InvokePrivateResult<Dictionary<string, List<CRectangleObject>>>(window, "BuildAnnotationRois");
                AssertTrue(roisByClass.TryGetValue("Defect", out List<CRectangleObject> savedRois), "edited ROI should stay exportable as Defect");
                AssertEqual(1, savedRois.Count);
                AssertEqual(new Rectangle(25, 25, 45, 40), savedRois[0].Roi);

                var objectReviewPanel = (WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl");
                objectReviewPanel.ViewModel.SelectedObject = objectReviewPanel.ViewModel.Objects.First(item => item.IsEnabled);
                CanvasRect<float> selectedBeforeDelete = GetPrivateField<CanvasRect<float>>(window.MainCanvasViewModel, "_selectedRect");
                AssertTrue(selectedBeforeDelete != null && !selectedBeforeDelete.IsEmpty(), "WPF object delete test should start with a selected canvas ROI");

                Stopwatch deleteStopwatch = Stopwatch.StartNew();
                AssertTrue(InvokePrivateResult<bool>(window, "DeleteSelectedObject"), "WPF object review delete should remove the selected ROI");
                deleteStopwatch.Stop();

                CanvasRect<float> selectedAfterDelete = GetPrivateField<CanvasRect<float>>(window.MainCanvasViewModel, "_selectedRect");
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"WPF_OBJECT_REVIEW_DELETE_MS={deleteStopwatch.Elapsed.TotalMilliseconds:F3} REMAINING_ROIS={manualRois.Count} REMAINING_OVERLAYS={CountAnnotationVerificationWindowOverlays(window.MainCanvasViewModel.ImageViewer)} SELECTED_EMPTY={selectedAfterDelete == null || selectedAfterDelete.IsEmpty()}"));

                AssertEqual(0, manualRois.Count);
                AssertEqual(0, CountAnnotationVerificationWindowOverlays(window.MainCanvasViewModel.ImageViewer));
                AssertTrue(selectedAfterDelete == null || selectedAfterDelete.IsEmpty(), "WPF object review delete should clear stale canvas ROI handles");
                AssertTrue(deleteStopwatch.Elapsed.TotalMilliseconds < 250.0, "WPF object review delete should not block on queue/review-status file bookkeeping");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private sealed class RoiObjectVerificationHarness : IDisposable
    {
        public RoiObjectVerificationHarness()
        {
            ViewModel = new OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel("RoiObjectVerification")
            {
                IsTeachingMode = true,
                DrawingShapeKind = CanvasRoiShapeKind.Rectangle
            };
            ImageViewer = ViewModel.ImageViewer;
            ConfigureCanvasForRoiObjectVerification(ViewModel);
            OpenGlControl = GetCanvasOpenGlControl(ViewModel);
            ParentGroup = ImageViewer.GetCanvasOverlayManager().GetGroupToType(OpenVisionLab.ImageCanvas.EnumInspWindowType.Module.ToString());
            ParentGroup.IsVisible = false;

            ViewModel.RoiAdded += (_, e) =>
            {
                AddedCount++;
                LastAdded = e.RoiRect;
            };
            ViewModel.RoiMouseUp += (_, e) =>
            {
                MouseUpCount++;
                LastMouseUp = e.RoiRect;
            };
            ViewModel.RoiEditingCompleted += (_, e) =>
            {
                EditingCompletedCount++;
                LastEdited = e.RoiRect;
            };
        }

        public OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel ViewModel { get; }

        public OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl ImageViewer { get; }

        public object OpenGlControl { get; }

        public OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem ParentGroup { get; }

        public int AddedCount { get; private set; }

        public int MouseUpCount { get; private set; }

        public int EditingCompletedCount { get; private set; }

        public CanvasRect<float> LastAdded { get; private set; }

        public CanvasRect<float> LastMouseUp { get; private set; }

        public CanvasRect<float> LastEdited { get; private set; }

        public int WindowOverlayCount => CountAnnotationVerificationWindowOverlays(ImageViewer);

        public CanvasRect<float> AddRoi(CanvasRect<float> rect)
        {
            if (string.IsNullOrWhiteSpace(rect.UniqueId))
            {
                rect.UniqueId = Guid.NewGuid().ToString("N");
            }

            return AddAnnotationVerificationOverlay(ImageViewer, ParentGroup, rect);
        }

        public void DragWorld(float startX, float startY, float endX, float endY)
            => DragCanvasWorld(ViewModel, OpenGlControl, startX, startY, endX, endY);

        public void ClickWorld(float x, float y)
        {
            Point point = ToRoiVerificationScreenPoint(x, y);
            InvokePrivateResult<object>(ViewModel, "OnMouseDown", OpenGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
            InvokePrivateResult<object>(ViewModel, "OnMouseUp", OpenGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        }

        public void HoverWorld(float x, float y)
            => HoverCanvasWorld(ViewModel, OpenGlControl, x, y);

        public void Dispose()
        {
            if (OpenGlControl is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static void ConfigureCanvasForRoiObjectVerification(OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel viewModel)
    {
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer = viewModel.ImageViewer;
        imageViewer.Size = new Size(100, 100);
        object openGlControl = GetCanvasOpenGlControl(viewModel);
        ((Control)openGlControl).Size = new Size(100, 100);
        SetPrivateField(imageViewer, "_zoom", 100F);
        SetPrivateField(imageViewer, "_offsetSize", new SizeF(0F, 0F));
        SetPrivateField(viewModel, "_imageSize", new Size(100, 100));
        imageViewer.TextureAreas.Clear();
        imageViewer.TextureAreas.TryAdd(
            "roi-object-verification",
            new List<OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam>
            {
                new OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlTextureDrawingParam
                {
                    ImageName = "roi-object-verification",
                    GLDrawingTextureArea = new RectangleF(0F, 100F, 100F, -100F),
                    GLTextureArea = new RectangleF(0F, 0F, 100F, 100F),
                    ImageTexutreArea = new Rectangle(0, 0, 100, 100),
                    TextureFullScreen = new Size(100, 100),
                    TitleSize = new Size(100, 100)
                }
            });
    }

    private static object GetCanvasOpenGlControl(OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel viewModel)
        => typeof(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)
            .GetMethod("GetOpenGLControl")
            .Invoke(viewModel.ImageViewer, null);

    private static void DragCanvasWorld(
        OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel viewModel,
        object openGlControl,
        float startX,
        float startY,
        float endX,
        float endY)
    {
        Point start = ToRoiVerificationScreenPoint(startX, startY);
        Point end = ToRoiVerificationScreenPoint(endX, endY);
        InvokePrivateResult<object>(viewModel, "OnMouseDown", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0));
        InvokePrivateResult<object>(viewModel, "OnMouseUp", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0));
    }

    private static void HoverCanvasWorld(
        OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel viewModel,
        object openGlControl,
        float x,
        float y)
    {
        Point point = ToRoiVerificationScreenPoint(x, y);
        InvokePrivateResult<object>(viewModel, "OnMouseMove", openGlControl, new OpenVisionLab.ImageCanvas.Canvas.CanvasMouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
    }

    private static Point ToRoiVerificationScreenPoint(float canvasX, float canvasY)
        => new Point((int)Math.Round(canvasX), (int)Math.Round(100F - canvasY));

    private static CanvasRect<float> AddAnnotationVerificationOverlay(
        OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer,
        OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem parentGroup,
        CanvasRect<float> rect)
    {
        rect.GroupType = parentGroup.GroupType;
        if (string.IsNullOrWhiteSpace(rect.UniqueId))
        {
            rect.UniqueId = Guid.NewGuid().ToString();
        }

        imageViewer.GetCanvasOverlayManager().AddOverlayItem(
            parentGroup.GroupType,
            new OpenVisionLab.ImageCanvas.Overlays.CanvasOverlayItem
            {
                GroupType = parentGroup.GroupType,
                Shape = rect,
                ItemType = OpenVisionLab.ImageCanvas.EnumItemType.Window,
                InspWindowType = parentGroup.InspWindowType,
                IsVisible = true,
                Color = Color.White
            });

        return rect;
    }

    private static int CountAnnotationVerificationWindowOverlays(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer)
        => imageViewer.GetCanvasOverlayManager()
            .GetAllVisibleUnlockedOverlays()
            .Count(item => !item.IsGroupRectangle && item.ItemType == OpenVisionLab.ImageCanvas.EnumItemType.Window);

    private static void AssertCanvasRectBounds(float left, float top, float right, float bottom, CanvasRect<float> rect, string name)
    {
        AssertNearlyEqual(left, rect.Left, 0.01F, $"{name} left");
        AssertNearlyEqual(top, rect.Top, 0.01F, $"{name} top");
        AssertNearlyEqual(right, rect.Right, 0.01F, $"{name} right");
        AssertNearlyEqual(bottom, rect.Bottom, 0.01F, $"{name} bottom");
    }

    private static void TestCViewerRoiApi()
    {
        using var viewer = new CViewer();

        viewer.SetRoiRectangles(new[]
        {
            new Rectangle(1, 2, 30, 40),
            Rectangle.Empty,
            new Rectangle(10, 10, 0, 20)
        });

        List<Rectangle> rois = viewer.GetRoiRectangles();
        AssertEqual(1, rois.Count);
        AssertEqual(new Rectangle(1, 2, 30, 40), rois[0]);

        viewer.SetRoiRectangles(new[]
        {
            new Rectangle(4, 5, 20, 21),
            new Rectangle(40, 45, 22, 23)
        });
        AssertTrue(viewer.SelectAnnotationListItem(2), "ROI list item was not selected");
        viewer.SetModeSegmentation();
        AssertTrue(viewer.DeleteSelectedAnnotation(), "selected ROI was not deleted outside rectangle mode");
        rois = viewer.GetRoiRectangles();
        AssertEqual(1, rois.Count);
        AssertEqual(new Rectangle(4, 5, 20, 21), rois[0]);
        AssertEqual(-1, viewer.SelectedAnnotationListIndex);
        AssertTrue(viewer.UndoAnnotationChange(), "ROI delete should be undoable");
        AssertEqual(2, viewer.GetRoiRectangles().Count);

        viewer.ResetAnnotations();
        AssertEqual(0, viewer.GetRoiRectangles().Count);
    }

    private static void TestCViewerSegmentationApi()
    {
        using var viewer = new CViewer();
        using var source = new Bitmap(40, 30);
        var classItem = new CClassItem { Text = "Defect", DrawColor = Color.Red };

        viewer.SetDisplayImage(source);
        viewer.SetSegmentationPolygons(
            new[]
            {
                new[]
                {
                    new Point(3, 4),
                    new Point(20, 4),
                    new Point(20, 18),
                    new Point(3, 18)
                }
            },
            classItem,
            reset: true);

        List<List<Point>> polygons = viewer.GetSegmentationPolygons();
        AssertEqual(1, polygons.Count);
        AssertEqual(new Point(3, 4), polygons[0][0]);
        AssertEqual(1, viewer.GetRoiListItems().Count);
        AssertEqual(new Rectangle(3, 4, 18, 15), viewer.GetRoiListItems()[0].Roi);
        AssertTrue(!viewer.CanUndoAnnotationChange, "loading segmentation data should not populate edit undo history");

        viewer.SegmentationBrushRadius = 100;
        AssertEqual(48, viewer.SegmentationBrushRadius);
        viewer.SegmentationBrushRadius = 1;
        AssertEqual(2, viewer.SegmentationBrushRadius);
        viewer.SegmentationBrushRadius = 10;

        int brushAdded = viewer.AddSegmentationBrushStamps(new[] { new Point(30, 20) }, 4, classItem);
        AssertEqual(1, brushAdded);
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "brush stamp should add a segment polygon");
        AssertTrue(viewer.CanUndoAnnotationChange, "brush edit should create an undo snapshot");
        AssertTrue(viewer.UndoAnnotationChange(), "brush edit undo failed");
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.CanRedoAnnotationChange, "undo should create a redo snapshot");
        AssertTrue(viewer.RedoAnnotationChange(), "brush edit redo failed");
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "redo should restore the brush segment");
        int erased = viewer.EraseSegmentationAt(new Point(30, 20), 5);
        AssertTrue(erased > 0, "eraser should remove a brush segment");
        AssertTrue(viewer.UndoAnnotationChange(), "eraser undo failed");
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "eraser undo should restore removed segment");

        viewer.ResetAnnotations();
        viewer.AddSegmentationRectangles(
            new[]
            {
                new Rectangle(2, 2, 8, 8),
                new Rectangle(18, 10, 8, 8)
            },
            classItem);
        int merged = viewer.MergeSegmentationSegments("Defect");
        AssertTrue(merged >= 2, "segment merge should consume multiple segment polygons");
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.UndoAnnotationChange(), "segment merge undo failed");
        AssertTrue(viewer.GetSegmentationPolygons().Count >= 2, "segment merge undo should restore original segments");

        viewer.ResetAnnotations();
        int strokeAdded = viewer.AddSegmentationBrushStamps(
            new[]
            {
                new Point(10, 10),
                new Point(14, 10),
                new Point(18, 10),
                new Point(22, 10)
            },
            5,
            classItem);
        AssertEqual(1, strokeAdded);
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        AssertTrue(SegmentationGeometry.GetBounds(viewer.GetSegmentationPolygons()[0]).Contains(new Point(22, 10)), "viewer brush stroke should be saved as one merged segment");
        int overlappingBrushAdded = viewer.AddSegmentationBrushStamps(new[] { new Point(24, 10), new Point(27, 10) }, 5, classItem);
        AssertEqual(1, overlappingBrushAdded);
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);
        int strokeErased = viewer.EraseSegmentationStroke(
            new[]
            {
                new Point(10, 10),
                new Point(14, 10),
                new Point(18, 10),
                new Point(22, 10),
                new Point(24, 10),
                new Point(27, 10)
            },
            7);
        AssertEqual(1, strokeErased);
        AssertEqual(0, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.UndoAnnotationChange(), "brush stroke eraser undo failed");
        AssertEqual(1, viewer.GetSegmentationPolygons().Count);

        viewer.ResetAnnotations();
        int rectangleSegmentAdded = viewer.AddSegmentationRectangles(new[] { new Rectangle(5, 5, 20, 12) }, classItem);
        AssertEqual(1, rectangleSegmentAdded);
        Rectangle beforeEraseBounds = SegmentationGeometry.GetBounds(viewer.GetSegmentationPolygons()[0]);
        int partialEraseCount = viewer.EraseSegmentationAt(new Point(23, 11), 5);
        AssertEqual(1, partialEraseCount);
        List<Point> afterErasePolygon = viewer.GetSegmentationPolygons()[0];
        AssertTrue(!SegmentationGeometry.ContainsPoint(afterErasePolygon, new Point(23, 11)), "viewer eraser should trim part of a segment instead of deleting the whole object");
        AssertTrue(viewer.UndoAnnotationChange(), "partial eraser undo failed");
        AssertEqual(beforeEraseBounds, SegmentationGeometry.GetBounds(viewer.GetSegmentationPolygons()[0]));
        int innerEraseCount = viewer.EraseSegmentationAt(new Point(14, 11), 3);
        AssertEqual(1, innerEraseCount);
        AssertEqual(1, viewer.GetSegmentationCutoutPolygons().Count);
        AssertTrue(SegmentationGeometry.ContainsPoint(viewer.GetSegmentationCutoutPolygons()[0], new Point(14, 11)), "internal eraser should preserve a cutout polygon");
        AssertTrue(viewer.UndoAnnotationChange(), "internal eraser undo failed");
        AssertEqual(0, viewer.GetSegmentationCutoutPolygons().Count);

        using var defectImage = new Bitmap(40, 30);
        using (Graphics graphics = Graphics.FromImage(defectImage))
        {
            graphics.Clear(Color.FromArgb(170, 170, 170));
            using var defectBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
            graphics.FillEllipse(defectBrush, 24, 12, 6, 6);
        }

        viewer.SetDisplayImage(defectImage);
        viewer.ResetAnnotations();
        viewer.SetRoiRectangles(new[] { new Rectangle(20, 8, 14, 14) }, classItem);
        int skippedAuto = viewer.AddAutoSegmentationFromRois(classItem, onlySelected: true);
        AssertEqual(0, skippedAuto);
        AssertEqual(0, viewer.GetSegmentationPolygons().Count);
        AssertTrue(viewer.SelectAnnotationListItem(1), "ROI list selection should select the rectangle for auto segmentation");
        int autoAdded = viewer.AddAutoSegmentationFromRois(classItem, onlySelected: true);
        AssertEqual(1, autoAdded);
        AssertTrue(viewer.GetSegmentationPolygons().Count == 1, "auto ROI extraction should add a segment polygon");

        viewer.ResetAnnotations();
        AssertEqual(0, viewer.GetSegmentationPolygons().Count);
    }

    private static void TestCViewerDefaultDefectSegmentationSave()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "default-defect.png";

            using var viewer = new CViewer();
            using var source = new Bitmap(20, 20);
            viewer.SetDisplayImage(source);

            int added = viewer.AddSegmentationRectangles(new[] { new Rectangle(2, 3, 7, 6) });
            bool saved = new LabelingWorkflowService().CommitCurrentAnnotations(viewer, data, new CSystem());
            string maskPath = Path.Combine(root, "data", "train", "masks", "default-defect.png");

            AssertEqual(1, added);
            AssertTrue(saved, "default Defect segmentation was not saved");
            AssertEqual("Defect", data.ClassNamedList[0].Text);
            AssertTrue(File.Exists(maskPath), "default Defect mask file was not created");

            using var mask = new Bitmap(maskPath);
            AssertEqual(1, mask.GetPixel(4, 4).R);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestCViewerImageCopy()
    {
        using var viewer = new CViewer();
        using var source = new Bitmap(12, 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        viewer.SetDisplayImage(source);

        AssertTrue(!ReferenceEquals(source, viewer.CurrentImage), "viewer should keep an internal image copy");
        AssertEqual(source.Size, viewer.CurrentImage.Size);
        AssertEqual(System.Drawing.Imaging.PixelFormat.Format24bppRgb, viewer.CurrentImage.PixelFormat);
    }

    private static void TestCanvasImageLoaderOwnsDecodedMatBuffers()
    {
        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "alpha.png");
            using (var source = new Bitmap(7, 5, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                source.SetPixel(0, 0, Color.FromArgb(80, 10, 30, 220));
                source.SetPixel(6, 4, Color.FromArgb(255, 90, 120, 150));
                source.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            using CvMat mat = OpenVisionLab.ImageCanvas.CanvasImageLoader.LoadMatFromFile(imagePath);
            AssertTrue(!mat.Empty(), "decoded Mat should not be empty");
            AssertEqual(7, mat.Width);
            AssertEqual(5, mat.Height);
            AssertEqual(3, mat.Channels());

            string loaderSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "OpenVisionLab",
                "Library",
                "OpenVisionLab.ImageCanvas",
                "Util",
                "CanvasImageLoader.cs"));
            AssertTrue(!loaderSource.Contains("DataPointer", StringComparison.Ordinal), "image loader should not return a Mat that wraps an external buffer pointer");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestCanvasImageLoaderUsesContinuousUploadMatWithoutClone()
    {
        MethodInfo method = typeof(OpenVisionLab.ImageCanvas.CanvasImageLoader).GetMethod(
            "UseContinuousMatForTextureUpload",
            BindingFlags.NonPublic | BindingFlags.Static);

        AssertTrue(method != null, "texture upload helper should exist");

        using CvMat continuous = new CvMat(6, 7, CvMatType.CV_8UC3, CvScalar.All(5));
        object[] continuousArgs = { continuous, false };
        CvMat continuousUpload = (CvMat)method.Invoke(null, continuousArgs);

        AssertTrue(ReferenceEquals(continuous, continuousUpload), "continuous Mats should upload without cloning");
        AssertEqual(false, (bool)continuousArgs[1]!);
        AssertTrue(!continuous.IsDisposed, "continuous source Mat should still be owned by the caller");

        using CvMat source = new CvMat(8, 8, CvMatType.CV_8UC3, CvScalar.All(7));
        using CvMat subMat = source.SubMat(new OpenCvSharp.Rect(1, 1, 4, 4));
        object[] subMatArgs = { subMat, false };
        CvMat compactUpload = (CvMat)method.Invoke(null, subMatArgs);

        try
        {
            AssertTrue(!ReferenceEquals(subMat, compactUpload), "strided submats should clone into a compact upload buffer");
            AssertEqual(true, (bool)subMatArgs[1]!);
            AssertTrue(compactUpload.IsContinuous(), "strided upload clone should be continuous");
            AssertEqual(subMat.Rows, compactUpload.Rows);
            AssertEqual(subMat.Cols, compactUpload.Cols);
        }
        finally
        {
            compactUpload.Dispose();
        }
    }

    private static void TestCViewerMainImageWorkspace()
    {
        using var viewer = new CViewer();
        using var source = new Bitmap(12, 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        viewer.LoadMainImage(source, "sample", @"C:\images\sample.png");

        AssertEqual("sample", CGlobal.Inst.ImageWorkspace.ActiveImageName);
        AssertEqual(@"C:\images\sample.png", CGlobal.Inst.ImageWorkspace.ActiveImagePath);
        AssertTrue(ReferenceEquals(viewer.CurrentImage, CGlobal.Inst.ImageWorkspace.ActiveImage), "workspace should reference the viewer-owned image copy");
        AssertTrue(!ReferenceEquals(source, CGlobal.Inst.ImageWorkspace.ActiveImage), "workspace should not reference the temporary source bitmap");
    }

    private static void TestCViewerWinFormsHostAdapterLifecycle()
    {
        using var viewer = new CViewer();
        using var host = new Panel();

        using var firstAdapter = new CViewerWinFormsHostAdapter(viewer, host);
        Control firstCanvas = firstAdapter.Canvas;
        AssertEqual(1, host.Controls.Count);
        AssertTrue(ReferenceEquals(firstCanvas, host.Controls[0]), "first canvas was not attached");

        using var secondAdapter = new CViewerWinFormsHostAdapter(viewer, host);
        Control secondCanvas = secondAdapter.Canvas;
        AssertEqual(1, host.Controls.Count);
        AssertTrue(!ReferenceEquals(firstCanvas, secondCanvas), "second attach reused the old canvas");
        AssertTrue(firstCanvas.IsDisposed, "first canvas was not disposed");
        AssertTrue(ReferenceEquals(secondCanvas, host.Controls[0]), "second canvas was not attached");

        firstAdapter.Dispose();
        AssertTrue(!secondCanvas.IsDisposed, "disposing an old adapter should not detach the active canvas");

        secondAdapter.Dispose();
        AssertTrue(secondCanvas.IsDisposed, "second canvas was not disposed with viewer");
        AssertEqual(0, host.Controls.Count);
    }

    private static void TestWpfCanvasPanelHostsRoiViewWithoutWinFormsBridge()
    {
        string root = FindRepositoryRoot();
        string canvasPanelXaml = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfCanvasPanel.xaml"));

        AssertTrue(!File.Exists(Path.Combine(root, "0. UI", "9) WPF", "Interop", "WpfRoiCanvasHost.cs")), "WPF ROI canvas should not keep the reverse WinForms bridge");
        AssertTrue(!canvasPanelXaml.Contains("WindowsFormsHost", StringComparison.Ordinal), "WPF canvas panel should not host the ROI view through WindowsFormsHost");
        AssertTrue(!canvasPanelXaml.Contains("ElementHost", StringComparison.Ordinal), "WPF canvas panel should not host the ROI view through ElementHost");
        AssertTrue(canvasPanelXaml.Contains("canvas:RoiImageCanvasView", StringComparison.Ordinal), "WPF canvas panel should host the ROI image canvas view directly");

        var panel = new WpfCanvasPanel();
        AssertTrue(panel.ViewModel != null, "WPF canvas panel view model was not created");
        AssertTrue(panel.MainCanvas != null, "WPF canvas panel did not create the ROI image canvas view");
        AssertEqual("OpenVisionLab.ImageCanvas.Views.RoiImageCanvasView", panel.MainCanvas.GetType().FullName);
    }

    private static void TestWpfCanvasPanelDeclaresViewerCommands()
    {
        string root = FindRepositoryRoot();
        string canvasPanelPath = Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfCanvasPanel.xaml");
        XDocument xaml = XDocument.Load(canvasPanelPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        foreach (string buttonName in new[]
        {
            "FitCanvasButton",
            "ActualSizeCanvasButton",
            "PanCanvasButton",
            "FocusCandidateCanvasButton",
            "ResetAiOverlayCanvasButton"
        })
        {
            AssertNamedXamlElement(xaml, xName, "Button", buttonName);
        }

        AssertNamedXamlBinding(xaml, xName, "FitCanvasButton", "IsEnabled", "IsFitEnabled");
        AssertNamedXamlBinding(xaml, xName, "ActualSizeCanvasButton", "IsEnabled", "IsActualSizeEnabled");
        AssertNamedXamlBinding(xaml, xName, "PanCanvasButton", "IsEnabled", "IsPanEnabled");
        AssertNamedXamlBinding(xaml, xName, "FocusCandidateCanvasButton", "IsEnabled", "IsFocusCandidateEnabled");
        AssertNamedXamlBinding(xaml, xName, "ResetAiOverlayCanvasButton", "IsEnabled", "IsResetAiOverlayEnabled");

        string panelCode = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfCanvasPanel.xaml.cs"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));

        AssertTrue(panelCode.Contains("FitRequested", StringComparison.Ordinal), "canvas panel should expose fit command events");
        AssertTrue(panelCode.Contains("ActualSizeRequested", StringComparison.Ordinal), "canvas panel should expose actual-size command events");
        AssertTrue(panelCode.Contains("FocusCandidateRequested", StringComparison.Ordinal), "canvas panel should expose selected-candidate focus events");
        AssertTrue(shellSource.Contains("MainCanvasViewModel.ImageViewer.ZoomToActualSize();", StringComparison.Ordinal), "WPF shell should route the 1:1 command to the OpenGL viewer");
        AssertTrue(shellSource.Contains("MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);", StringComparison.Ordinal), "WPF shell should expose a pan command");
        AssertTrue(shellSource.Contains("ResetAiOverlayCanvasButton_Click", StringComparison.Ordinal), "WPF shell should expose AI overlay reset");
        AssertTrue(shellSource.Contains("UpdateCanvasCommandButtons", StringComparison.Ordinal), "WPF shell should enable canvas commands only when image/candidate state allows them");
        AssertTrue(shellSource.Contains("CanvasPanelControl.ViewModel.SetCommandAvailability", StringComparison.Ordinal), "WPF shell should push canvas command availability through the canvas ViewModel");
        AssertTrue(!shellSource.Contains("FitCanvasButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the fit canvas button on the normal path");
        AssertTrue(!shellSource.Contains("FocusCandidateCanvasButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the selected-candidate focus button on the normal path");
    }

    private static void TestWpfLearningWorkflowPanelDeclaresEducationModesAndTools()
    {
        string root = FindRepositoryRoot();
        string panelPath = Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLearningWorkflowPanel.xaml");
        string tutorialHtmlPath = Path.Combine(root, "docs", "tutorial", "labeling-workbench-tutorial.html");
        XDocument xaml = XDocument.Load(panelPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlBinding(xaml, xName, "LearningModeListBox", "ItemsSource", "LearningModes");
        AssertNamedXamlBinding(xaml, xName, "LearningModeListBox", "SelectedItem", "SelectedMode");
        AssertNamedXamlBinding(xaml, xName, "AnnotationToolListBox", "ItemsSource", "AnnotationTools");
        AssertNamedXamlBinding(xaml, xName, "AnnotationToolListBox", "SelectedItem", "SelectedTool");
        AssertNamedXamlBinding(xaml, xName, "LearningStepListBox", "ItemsSource", "LearningSteps");
        AssertNamedXamlBinding(xaml, xName, "LearningStepListBox", "SelectedItem", "SelectedStep");
        AssertNamedXamlBinding(xaml, xName, "TutorialIntroTitleText", "Text", "TutorialTitleText");
        AssertNamedXamlBinding(xaml, xName, "TutorialIntroSummaryText", "Text", "TutorialSummaryText");
        AssertNamedXamlBinding(xaml, xName, "TutorialChecklistItemsControl", "ItemsSource", "TutorialChecklistItems");
        AssertNamedXamlBinding(xaml, xName, "TutorialHtmlGuidePathText", "Text", "TutorialHtmlPathText");
        AssertNamedXamlBinding(xaml, xName, "GroundTruthChipText", "Text", "GroundTruthChipText");
        AssertNamedXamlBinding(xaml, xName, "PredictionChipText", "Text", "PredictionChipText");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingWorkflowSummaryText", "Text", "TrainingWorkflowSummaryText");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingChecklistStatusText", "Text", "TrainingChecklistStatusText");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingChecklistDetailText", "Text", "TrainingChecklistDetailText");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingChecklistActionText", "Text", "TrainingChecklistActionText");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingHistoryText", "Text", "TrainingHistoryText");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingWorkflowItemsControl", "ItemsSource", "YoloTrainingWorkflowSteps");
        AssertNamedXamlBinding(xaml, xName, "YoloTrainingRunHistoryItemsControl", "ItemsSource", "TrainingRunHistoryItems");
        AssertNamedXamlElement(xaml, xName, "ScrollViewer", "LearningWorkflowScrollViewer");
        AssertNamedXamlElement(xaml, xName, "Expander", "TutorialChecklistExpander");
        AssertNamedXamlElement(xaml, xName, "Expander", "LearningConceptsExpander");
        XElement conceptsExpander = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Expander"
                && string.Equals((string)element.Attribute(xName), "LearningConceptsExpander", StringComparison.Ordinal));
        AssertTrue(((string)conceptsExpander?.Attribute("Header") ?? string.Empty).Contains("심화", StringComparison.Ordinal), "secondary concepts should be labeled as deeper learning, not the default guide path");
        AssertNamedXamlElement(xaml, xName, "Border", "TutorialIntroPanel");
        AssertNamedXamlElement(xaml, xName, "Button", "TutorialOpenHtmlGuideButton");
        AssertNamedXamlElement(xaml, xName, "Button", "YoloFixClassesButton");
        AssertNamedXamlElement(xaml, xName, "Button", "YoloFixLabelsButton");
        AssertNamedXamlElement(xaml, xName, "Button", "YoloFixDatasetButton");
        AssertNamedXamlBinding(xaml, xName, "YoloFixClassesButton", "IsEnabled", "IsYoloFixClassesEnabled");
        AssertNamedXamlBinding(xaml, xName, "YoloFixLabelsButton", "IsEnabled", "IsYoloFixLabelsEnabled");
        AssertNamedXamlBinding(xaml, xName, "YoloFixDatasetButton", "IsEnabled", "IsYoloFixDatasetEnabled");
        string panelSource = File.ReadAllText(panelPath);
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(panelSource.Contains("ModeDetailText", StringComparison.Ordinal), "WPF education panel should explain the selected lesson mode");
        AssertTrue(panelSource.Contains("StepDetailText", StringComparison.Ordinal), "WPF education panel should explain the selected lesson step");
        AssertTrue(panelSource.Contains("ToolDetailText", StringComparison.Ordinal), "WPF education panel should explain the selected annotation tool");
        AssertTrue(panelSource.Contains("AnnotationToolItemTemplate", StringComparison.Ordinal), "WPF education panel should render annotation tools with their own status template");
        AssertTrue(panelSource.Contains("CapabilityText", StringComparison.Ordinal), "WPF annotation tool palette should show connection status");
        AssertTrue(panelSource.Contains("DisplayCapabilityText", StringComparison.Ordinal), "WPF annotation tool palette should show runtime status text");
        AssertTrue(panelSource.Contains("IsActionEnabled", StringComparison.Ordinal), "WPF annotation tool palette should bind runtime availability");
        AssertTrue(panelSource.Contains("IsConnected", StringComparison.Ordinal), "WPF annotation tool palette should distinguish connected and pending tools");
        AssertTrue(panelSource.Contains("BrushSize", StringComparison.Ordinal), "WPF education panel should expose brush size control");
        AssertTrue(shellSource.Contains("LearningWorkflowViewModel.SetYoloFixActionAvailability", StringComparison.Ordinal), "WPF shell should push YOLO guide fix availability through the learning workflow ViewModel");
        AssertTrue(!shellSource.Contains("YoloFixClassesButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the class-fix button on the normal path");
        AssertTrue(!shellSource.Contains("YoloFixLabelsButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the label-fix button on the normal path");
        AssertTrue(!shellSource.Contains("YoloFixDatasetButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the dataset-fix button on the normal path");
        AssertTrue(panelSource.Contains("MaskOpacity", StringComparison.Ordinal), "WPF education panel should expose mask opacity control");
        AssertTrue(panelSource.Contains("YOLOv5", StringComparison.Ordinal), "WPF education panel should show the YOLOv5 training path");
        AssertTrue(panelSource.Contains("TutorialHtmlPathText", StringComparison.Ordinal), "WPF education panel should show the HTML tutorial path");
        AssertTrue(panelSource.Contains("TutorialOpenHtmlGuideButton_Click", StringComparison.Ordinal), "WPF education panel should expose a direct HTML tutorial open action");
        AssertTrue(panelSource.Contains("YoloTrainingWorkflowStep_MouseLeftButtonUp", StringComparison.Ordinal), "YOLO training workflow rows should be clickable");
        AssertTrue(panelSource.Contains("ScrollableChild_PreviewMouseWheel", StringComparison.Ordinal), "nested guide lists should pass mouse wheel scrolling to the parent guide");
        AssertTrue(panelSource.IndexOf("AnnotationToolListBox", StringComparison.Ordinal) < panelSource.IndexOf("LearningStepListBox", StringComparison.Ordinal), "annotation tools should appear before secondary lesson flow controls");
        AssertTrue(panelSource.Contains("StateText", StringComparison.Ordinal), "YOLO training workflow rows should show per-step state text");
        AssertTrue(panelSource.Contains("StateIconKind", StringComparison.Ordinal), "YOLO training workflow rows should show per-step state icons");
        AssertTrue(panelSource.Contains("IsExpanded=\"False\"", StringComparison.Ordinal), "secondary lesson concepts should be collapsed by default so the guide starts with the actionable YOLO flow");
        AssertTrue(xaml.Descendants().Any(element =>
            element.Name.LocalName == "TextBlock"
            && string.Equals((string)element.Attribute("Text"), "라벨링 시작", StringComparison.Ordinal)),
            "YOLO guide should expose a direct labeling action instead of making users hunt through lesson controls");
        string panelCodeSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLearningWorkflowPanel.xaml.cs"));
        AssertTrue(panelCodeSource.Contains("ShowAnnotationToolPalette", StringComparison.Ordinal), "YOLO box-label step should be able to reveal the annotation tool palette");
        AssertTrue(panelCodeSource.Contains("ScrollToVerticalOffset", StringComparison.Ordinal), "guide mouse wheel handling should scroll the parent panel");

        XElement modeList = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ListBox"
                && string.Equals((string)element.Attribute(xName), "LearningModeListBox", StringComparison.Ordinal));
        XElement toolList = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ListBox"
                && string.Equals((string)element.Attribute(xName), "AnnotationToolListBox", StringComparison.Ordinal));
        XElement stepList = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ListBox"
                && string.Equals((string)element.Attribute(xName), "LearningStepListBox", StringComparison.Ordinal));
        AssertEqual("LearningModeListBox_SelectionChanged", (string)modeList?.Attribute("SelectionChanged"));
        AssertEqual("AnnotationToolListBox_SelectionChanged", (string)toolList?.Attribute("SelectionChanged"));
        AssertEqual("LearningStepListBox_SelectionChanged", (string)stepList?.Attribute("SelectionChanged"));

        var viewModel = new WpfLearningWorkflowPanelViewModel();
        AssertEqual(7, viewModel.LearningModes.Count);
        AssertEqual(10, viewModel.AnnotationTools.Count);
        AssertEqual(5, viewModel.LearningSteps.Count);
        AssertEqual(10, WpfAnnotationToolCapabilityService.GetAll().Count(item => item.IsConnected));
        AssertEqual(0, WpfAnnotationToolCapabilityService.GetAll().Count(item => !item.IsConnected));
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Rectangle), "rectangle tool should be connected to the WPF ROI path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Ellipse), "ellipse tool should be connected to the WPF ROI path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.PanZoom), "pan tool should be connected to the WPF viewer path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Delete), "delete tool should be connected to the WPF object review path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Brush), "brush tool should be connected to the WPF raster mask edit path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Eraser), "eraser tool should be connected to the WPF raster mask edit path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Undo), "undo tool should be connected to the WPF annotation history path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Redo), "redo tool should be connected to the WPF annotation history path");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Polygon), "polygon tool should be connected through image-pixel click input and segmentation save");
        AssertEqual("\uAC00\uB2A5", viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Rectangle).CapabilityText);
        AssertEqual("\uAC00\uB2A5", viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Ellipse).CapabilityText);
        AssertEqual("\uAC00\uB2A5", viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Brush).CapabilityText);
        AssertTrue(!viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Undo).IsActionEnabled, "undo should be disabled until edit history exists");
        AssertEqual("\uC5C6\uC74C", viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Undo).DisplayCapabilityText);
        viewModel.SetAnnotationHistoryState(canUndo: true, canRedo: false, undoActionName: "Add ROI", redoActionName: string.Empty);
        AssertTrue(viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Undo).IsActionEnabled, "undo should become enabled when edit history exists");
        AssertEqual("\uAC00\uB2A5", viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Undo).DisplayCapabilityText);
        AssertTrue(viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Undo).ToolTip.Contains("Add ROI", StringComparison.Ordinal), "undo tooltip should name the next undo action");
        AssertTrue(viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Polygon).CapabilityStatusText.Contains("OpenGL", StringComparison.Ordinal), "polygon tooltip should mention the verified OpenGL preview path");
        AssertTrue(viewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Brush).CapabilityStatusText.Contains("raster mask", StringComparison.Ordinal), "brush tooltip should mention the verified raster mask path");
        AssertEqual(6, viewModel.TutorialChecklistItems.Count);
        AssertEqual(6, viewModel.YoloTrainingWorkflowSteps.Count);
        AssertEqual(WpfLearningMode.LabelingBasics, viewModel.SelectedMode.Mode);
        AssertEqual(WpfAnnotationTool.Select, viewModel.SelectedTool.Tool);
        AssertEqual(WpfLearningStep.Sample, viewModel.SelectedStep.Step);
        AssertTrue(viewModel.TrainingWorkflowSummaryText.Contains("\uD559\uC2B5", StringComparison.Ordinal), "YOLO training summary should include the training stage");
        AssertTrue(viewModel.TutorialTitleText.Contains("\uD29C\uD1A0\uB9AC\uC5BC", StringComparison.Ordinal), "tutorial title should be visible in the guide panel");
        AssertTrue(viewModel.TutorialHtmlPathText.Contains("docs/tutorial/labeling-workbench-tutorial.html", StringComparison.Ordinal), "tutorial should point to the HTML guide");
        AssertTrue(File.Exists(tutorialHtmlPath), "HTML tutorial file should exist");
        string resolvedTutorialHtmlPath = InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "ResolveTutorialHtmlGuidePath");
        AssertTrue(File.Exists(resolvedTutorialHtmlPath), $"HTML tutorial resolver should find the guide file: {resolvedTutorialHtmlPath}");
        string tutorialHtml = File.ReadAllText(tutorialHtmlPath);
        AssertTrue(tutorialHtml.Contains("images/01-guide.png", StringComparison.Ordinal), "HTML tutorial should include the guide step screenshot");
        AssertTrue(tutorialHtml.Contains("images/06-inference-review.png", StringComparison.Ordinal), "HTML tutorial should include the inference review screenshot");
        AssertTrue(viewModel.YoloTrainingWorkflowSteps[0].Title.Contains("\uC774\uBBF8\uC9C0", StringComparison.Ordinal), "YOLO workflow should start with image loading");
        AssertTrue(viewModel.YoloTrainingWorkflowSteps[1].Title.Contains("\uD074\uB798\uC2A4", StringComparison.Ordinal), "YOLO workflow should require class registration before labeling");
        AssertTrue(viewModel.YoloTrainingWorkflowSteps[4].Title.Contains("YOLOv5", StringComparison.Ordinal), "YOLO workflow should include YOLOv5 training");
        AssertTrue(viewModel.YoloTrainingWorkflowSteps[5].ActionText.Contains("best.pt", StringComparison.Ordinal), "YOLO workflow should guide post-training inference with best.pt");
        AssertTrue(viewModel.TrainingChecklistStatusText.Contains("\uB370\uC774\uD130\uC14B", StringComparison.Ordinal), "YOLO training guide should show dataset readiness status");
        AssertEqual("대기", viewModel.YoloTrainingWorkflowSteps[0].StateText);
        viewModel.SetYoloTrainingStepState(1, true, "완료");
        AssertTrue(viewModel.YoloTrainingWorkflowSteps[0].IsCompleted, "YOLO workflow step should expose completed state");
        AssertEqual("완료", viewModel.YoloTrainingWorkflowSteps[0].StateText);
        viewModel.TrainingChecklistStatusText = "Ready";
        viewModel.TrainingChecklistDetailText = "Detail";
        viewModel.TrainingChecklistActionText = "Action";
        viewModel.TrainingHistoryText = "History";
        viewModel.SetTrainingRunHistoryItems(new[] { "Run A", "Run B" });
        AssertTrue(viewModel.IsYoloFixClassesEnabled, "YOLO class-fix action should start enabled");
        AssertTrue(!viewModel.IsYoloFixLabelsEnabled, "YOLO label-fix action should start disabled until images are loaded");
        AssertTrue(viewModel.IsYoloFixDatasetEnabled, "YOLO dataset-fix action should start enabled");
        viewModel.SetYoloFixActionAvailability(canFixClasses: true, canFixLabels: true, canFixDataset: true);
        AssertTrue(viewModel.IsYoloFixLabelsEnabled, "YOLO label-fix action should enable once images are available");
        viewModel.SetYoloFixActionAvailability(canFixClasses: false, canFixLabels: false, canFixDataset: false);
        AssertTrue(!viewModel.IsYoloFixClassesEnabled, "YOLO class-fix action should follow ViewModel availability");
        AssertTrue(!viewModel.IsYoloFixLabelsEnabled, "YOLO label-fix action should follow ViewModel availability");
        AssertTrue(!viewModel.IsYoloFixDatasetEnabled, "YOLO dataset-fix action should follow ViewModel availability");
        AssertEqual("Ready", viewModel.TrainingChecklistStatusText);
        AssertEqual("Detail", viewModel.TrainingChecklistDetailText);
        AssertEqual("Action", viewModel.TrainingChecklistActionText);
        AssertEqual("History", viewModel.TrainingHistoryText);
        AssertEqual(2, viewModel.TrainingRunHistoryItems.Count);
        AssertTrue(viewModel.LearningModes.Any(item => item.Mode == WpfLearningMode.ObjectDetection), "object detection education mode was not registered");
        AssertTrue(viewModel.LearningModes.Any(item => item.Mode == WpfLearningMode.Segmentation), "segmentation education mode was not registered");
        AssertTrue(viewModel.LearningModes.Any(item => item.Mode == WpfLearningMode.AnomalyDetection), "anomaly education mode was not registered");
        AssertTrue(viewModel.AnnotationTools.Any(item => item.Tool == WpfAnnotationTool.Ellipse), "ellipse annotation tool was not registered");
        AssertTrue(viewModel.AnnotationTools.Any(item => item.Tool == WpfAnnotationTool.Polygon), "polygon annotation tool was not registered");
        AssertTrue(viewModel.AnnotationTools.Any(item => item.Tool == WpfAnnotationTool.Brush), "brush annotation tool was not registered");
        AssertTrue(!string.Equals(viewModel.GroundTruthChipText, viewModel.PredictionChipText, StringComparison.Ordinal), "ground-truth and AI prediction chips should be visually distinct states");
        viewModel.SelectedMode = viewModel.LearningModes.First(item => item.Mode == WpfLearningMode.ObjectDetection);
        AssertTrue(viewModel.ModeDetailText.Contains("YOLO", StringComparison.Ordinal), "object detection lesson should explain YOLO");
        viewModel.SelectedMode = viewModel.LearningModes.First(item => item.Mode == WpfLearningMode.Segmentation);
        AssertTrue(viewModel.ModeDetailText.Contains("U-Net", StringComparison.Ordinal), "segmentation lesson should explain U-Net");
        viewModel.SelectedMode = viewModel.LearningModes.First(item => item.Mode == WpfLearningMode.AnomalyDetection);
        AssertTrue(viewModel.ModeDetailText.Contains("Anomaly", StringComparison.Ordinal), "anomaly lesson should explain anomaly detection");
        viewModel.BrushSize = 999;
        AssertEqual(64, viewModel.BrushSize);
        viewModel.MaskOpacity = 0.01;
        AssertEqual("10%", viewModel.MaskOpacityPercentText);

        var panel = new WpfLearningWorkflowPanel();
        AssertTrue(panel.ViewModel != null, "WPF learning workflow view model was not created");
        AssertTrue(panel.WorkflowScrollViewer != null, "WPF learning workflow parent scroll viewer was not exposed");
        AssertEqual(7, panel.ModeList.Items.Count);
        AssertEqual(10, panel.ToolList.Items.Count);
        AssertEqual(5, panel.StepList.Items.Count);
        AssertTrue(panel.FindName("TutorialIntroPanel") != null, "WPF learning workflow tutorial panel was not created");
        AssertTrue(panel.FindName("TutorialChecklistItemsControl") is System.Windows.Controls.ItemsControl, "WPF learning workflow tutorial checklist was not created");
        var tutorialOpenButton = panel.FindName("TutorialOpenHtmlGuideButton") as System.Windows.Controls.Button;
        AssertTrue(tutorialOpenButton != null, "WPF learning workflow tutorial open button was not created");
        bool tutorialOpenRequested = false;
        panel.TutorialOpenHtmlGuideRequested += (_, _) => tutorialOpenRequested = true;
        tutorialOpenButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        AssertTrue(tutorialOpenRequested, "WPF learning workflow tutorial open button should raise a shell-routable event");
        AssertEqual(6, panel.YoloTrainingWorkflowList.Items.Count);
        AssertTrue(panel.YoloTrainingChecklistStatus != null, "WPF training checklist status text was not exposed");
        AssertTrue(panel.YoloTrainingChecklistDetail != null, "WPF training checklist detail text was not exposed");
        AssertTrue(panel.YoloTrainingChecklistAction != null, "WPF training checklist action text was not exposed");
        AssertTrue(panel.YoloTrainingHistory != null, "WPF training history text was not exposed");
        AssertTrue(panel.YoloTrainingRunHistoryList != null, "WPF training run history list was not exposed");

        string shellXaml = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml"));
        AssertTrue(shellXaml.Contains("WpfLearningWorkflowPanel", StringComparison.Ordinal), "WPF shell should host the education workflow panel");
        AssertTrue(shellXaml.Contains("LearningReviewTab", StringComparison.Ordinal), "WPF shell should keep education controls in a guide tab instead of the main workspace");
        AssertTrue(!shellXaml.Contains("WpfLearningWorkflowPanel x:Name=\"LearningWorkflowPanelControl\"\r\n                                        DockPanel.Dock=\"Top\"", StringComparison.Ordinal), "WPF shell should not pin the education workflow panel to the main top area");
        AssertTrue(!File.ReadAllText(panelPath).Contains("Height=\"86\"", StringComparison.Ordinal), "WPF education panel should not keep the old wide top-strip height");
        AssertTrue(shellSource.Contains("LearningWorkflowModeListBox_SelectionChanged", StringComparison.Ordinal), "WPF shell should react to education mode selection");
        AssertTrue(shellSource.Contains("AnnotationToolListBox_SelectionChanged", StringComparison.Ordinal), "WPF shell should track annotation tool selection");
        AssertTrue(shellSource.Contains("ConfigureLabelingCanvasDefaults", StringComparison.Ordinal), "WPF shell should apply labeling-friendly canvas defaults");
        AssertTrue(shellSource.Contains("ShowGroupNames = false", StringComparison.Ordinal), "WPF labeling canvas should hide debug group names by default");
        AssertTrue(shellSource.Contains("ShowRoiItemNames = false", StringComparison.Ordinal), "WPF labeling canvas should hide ROI debug item numbers by default");
        AssertTrue(shellSource.Contains("ShowGroupBounds = false", StringComparison.Ordinal), "WPF labeling canvas should hide the Module group frame by default");
        AssertTrue(shellSource.Contains("SetPendingAnnotationToolStatus", StringComparison.Ordinal), "WPF shell should show status when a palette tool has no verified drawing path yet");
        AssertTrue(shellSource.Contains("WpfAnnotationToolCapabilityService.Get(tool.Value)", StringComparison.Ordinal), "WPF shell should gate annotation tools through the capability service");
        AssertTrue(shellSource.Contains("LearningStepListBox_SelectionChanged", StringComparison.Ordinal), "WPF shell should wire the beginner sample flow steps");
        AssertTrue(shellSource.Contains("TutorialOpenHtmlGuideRequested", StringComparison.Ordinal), "WPF shell should wire the HTML tutorial open event");
        AssertTrue(shellSource.Contains("ResolveTutorialHtmlGuidePath", StringComparison.Ordinal), "WPF shell should resolve the tutorial path from the clone or execution folder");
        AssertTrue(shellSource.Contains("TutorialHtmlGuideRelativePath", StringComparison.Ordinal), "WPF shell should keep the tutorial path as an explicit relative path");
        AssertTrue(shellSource.Contains("WpfLearningMode.Infer", StringComparison.Ordinal), "WPF shell should map the Infer lesson to inference workflow mode");
        AssertTrue(shellSource.Contains("WpfLearningStep.Label", StringComparison.Ordinal), "WPF shell should map the Label step to the existing label creation flow");
        AssertTrue(shellSource.Contains("YoloTrainingWorkflowStep_Requested", StringComparison.Ordinal), "WPF shell should react to YOLO training guide step clicks");
        AssertTrue(shellSource.Contains("ExecuteYoloTrainingWorkflowStep", StringComparison.Ordinal), "WPF shell should route YOLO training guide steps to real actions");
        AssertTrue(shellSource.Contains("SelectAnnotationTool(WpfAnnotationTool.Rectangle, revealInGuide: true)", StringComparison.Ordinal), "YOLO guide box-label step should reveal the real rectangle tool");
        AssertTrue(shellSource.Contains("BrowseImageFolderButton_Click(sender, new RoutedEventArgs())", StringComparison.Ordinal), "YOLO guide step 1 should open the image folder picker");
        AssertTrue(shellSource.Contains("FocusClassCatalogTab();", StringComparison.Ordinal), "YOLO guide step 2 should focus class registration");
        AssertTrue(shellSource.Contains("RefreshTrainingReadinessPanel(refreshYaml: true);", StringComparison.Ordinal), "YOLO guide steps should refresh dataset readiness");
        AssertTrue(shellSource.Contains("StartTrainingButton?.Focus();", StringComparison.Ordinal), "YOLO guide training step should lead the user to the explicit start button");
        AssertTrue(shellSource.Contains("TryApplyLatestTrainingWeightsFromProject", StringComparison.Ordinal), "YOLO guide should apply the latest trained best.pt candidate");
        AssertTrue(shellSource.Contains("DetectButton?.Focus();", StringComparison.Ordinal), "YOLO guide post-training step should lead the user to current-image inference");
        AssertTrue(shellSource.Contains("UpdateYoloTrainingChecklist", StringComparison.Ordinal), "YOLO dataset readiness should update the guide checklist");
        AssertTrue(shellSource.Contains("BuildYoloTrainingIssuePresentation", StringComparison.Ordinal), "YOLO dataset issues should be split into user-action categories");
        AssertTrue(shellSource.Contains("ClassifyYoloTrainingIssue", StringComparison.Ordinal), "YOLO dataset issue classification should be explicit");
        AssertTrue(shellSource.Contains("RefreshYoloTrainingStepCompletion", StringComparison.Ordinal), "YOLO workflow guide should refresh per-step completion states");
        AssertTrue(shellSource.Contains("YoloFixClassesButton_Click", StringComparison.Ordinal), "YOLO guide should expose a class issue fix action");
        AssertTrue(shellSource.Contains("YoloFixLabelsButton_Click", StringComparison.Ordinal), "YOLO guide should expose a label issue fix action");
        AssertTrue(shellSource.Contains("YoloFixDatasetButton_Click", StringComparison.Ordinal), "YOLO guide should expose a dataset issue fix action");
        AssertTrue(shellSource.Contains("hasPendingTrainingWeightsRecipeSave", StringComparison.Ordinal), "trained best.pt application should remind the operator to save the recipe");
        AssertTrue(shellSource.Contains("UpdateYoloTrainingGuideDatasetHistory", StringComparison.Ordinal), "YOLO training guide should store dataset check history");
        AssertTrue(shellSource.Contains("UpdateAppliedTrainingWeightsHistory", StringComparison.Ordinal), "YOLO training guide should store applied best.pt history");
        AssertTrue(shellSource.Contains("AppliedWeightsSavedToRecipe", StringComparison.Ordinal), "YOLO training guide should distinguish saved and unsaved best.pt paths");
        AssertTrue(shellSource.Contains("AddYoloTrainingRunHistoryRecord", StringComparison.Ordinal), "YOLO training guide should keep a compact run history list");
        AssertTrue(shellSource.Contains("TrainingGuideRunHistoryLimit", StringComparison.Ordinal), "YOLO training run history should be bounded");
        AssertTrue(shellSource.Contains("FormatYoloTrainingRunHistoryItem", StringComparison.Ordinal), "YOLO training run history should be formatted for the guide");
        AssertTrue(shellSource.Contains("SaveYoloSettingsButton?.Focus();", StringComparison.Ordinal), "trained best.pt application should lead the operator to the settings save button");
        AssertTrue(shellSource.Contains("UpdateTrainingStatusVisual", StringComparison.Ordinal), "WPF training status should update readiness/progress colors");
        AssertTrue(shellSource.Contains("실제 드로잉 경로 검증 후 연결", StringComparison.Ordinal), "unverified drawing tools should clearly say they are not connected yet");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            var learningPanel = window.FindName("LearningWorkflowPanelControl") as WpfLearningWorkflowPanel;
            AssertTrue(learningPanel != null, "WPF learning workflow panel was not created in the shell");
            AssertTrue(window.FindName("LearningModeListBox") is System.Windows.Controls.ListBox, "WPF learning mode list was not registered");
            AssertTrue(window.FindName("AnnotationToolListBox") is System.Windows.Controls.ListBox, "WPF annotation tool list was not registered");
            AssertTrue(window.FindName("LearningStepListBox") is System.Windows.Controls.ListBox, "WPF learning step list was not registered");
            AssertTrue(window.FindName("GroundTruthChipText") is System.Windows.Controls.TextBlock, "ground-truth chip was not registered");
            AssertTrue(window.FindName("PredictionChipText") is System.Windows.Controls.TextBlock, "AI prediction chip was not registered");
            AssertTrue(window.FindName("YoloTrainingWorkflowItemsControl") is System.Windows.Controls.ItemsControl, "YOLO training workflow list was not registered");
            AssertTrue(window.FindName("YoloTrainingWorkflowSummaryText") is System.Windows.Controls.TextBlock, "YOLO training workflow summary was not registered");
            AssertTrue(window.FindName("YoloTrainingChecklistStatusText") is System.Windows.Controls.TextBlock, "YOLO training checklist status was not registered");
            AssertTrue(window.FindName("YoloTrainingChecklistDetailText") is System.Windows.Controls.TextBlock, "YOLO training checklist detail was not registered");
            AssertTrue(window.FindName("YoloTrainingChecklistActionText") is System.Windows.Controls.TextBlock, "YOLO training checklist action was not registered");
            AssertTrue(window.FindName("YoloTrainingHistoryText") is System.Windows.Controls.TextBlock, "YOLO training history was not registered");
            AssertTrue(window.FindName("YoloTrainingRunHistoryItemsControl") is System.Windows.Controls.ItemsControl, "YOLO training run history list was not registered");
            AssertTrue(window.FindName("TutorialOpenHtmlGuideButton") is System.Windows.Controls.Button, "tutorial HTML open button was not registered");
            AssertTrue(window.FindName("LearningConceptsExpander") is System.Windows.Controls.Expander, "secondary lesson concept expander was not registered");
            AssertTrue(window.FindName("YoloFixClassesButton") is System.Windows.Controls.Button, "YOLO fix classes button was not registered");
            AssertTrue(window.FindName("YoloFixLabelsButton") is System.Windows.Controls.Button, "YOLO fix labels button was not registered");
            AssertTrue(window.FindName("YoloFixDatasetButton") is System.Windows.Controls.Button, "YOLO fix dataset button was not registered");
            AssertTrue(!window.MainCanvasViewModel.ShowGroupNames, "WPF labeling canvas should not show the debug Module label by default");
            AssertTrue(!window.MainCanvasViewModel.ShowRoiItemNames, "WPF labeling canvas should not show debug ROI item numbers by default");
            AssertTrue(!window.MainCanvasViewModel.ShowGroupBounds, "WPF labeling canvas should not show the Module group rectangle by default");

            InvokePrivateResult<object>(window, "ExecuteYoloTrainingWorkflowStep", 3, learningPanel);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(120));
            AssertEqual(WpfAnnotationTool.Rectangle, learningPanel.ViewModel.SelectedTool.Tool);
            AssertTrue(learningPanel.LearningConcepts.IsExpanded, "YOLO box-label step should open the tool palette when it sends the learner to box drawing");
            AssertEqual(CanvasRoiShapeKind.Rectangle, window.MainCanvasViewModel.DrawingShapeKind);
            AssertTrue(window.MainCanvasViewModel.IsTeachingMode, "YOLO box-label step should enter WPF rectangle drawing mode");

            learningPanel.ToolList.SelectedItem = learningPanel.ViewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Rectangle);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(window.MainCanvasViewModel.IsTeachingMode, "connected rectangle tool should enter WPF drawing mode");
            learningPanel.ToolList.SelectedItem = learningPanel.ViewModel.AnnotationTools.First(item => item.Tool == WpfAnnotationTool.Brush);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(!window.MainCanvasViewModel.IsTeachingMode, "connected brush tool should not use ROI drawing mode");
            AssertTrue(window.MainCanvasViewModel.IsImagePointInputMode, "connected brush tool should enter WPF image-pixel mask input mode");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfAnnotationToolVerificationMatrix()
    {
        string root = FindRepositoryRoot();
        string validationPath = Path.Combine(root, "docs", "WPF_ANNOTATION_TOOL_VALIDATION.md");
        string objectVerificationPath = Path.Combine(root, "docs", "WPF_ANNOTATION_OBJECT_VERIFICATION.md");
        string legacyAnnotationScriptPath = Path.Combine(root, "scripts", "verify-wpf-annotation-objects.ps1");
        string focusedAnnotationScriptPath = Path.Combine(root, "scripts", "verify-wpf-annotation-object-interactions.ps1");
        string focusedRoiScriptPath = Path.Combine(root, "scripts", "verify-wpf-roi-object-interactions.ps1");
        string focusedSegmentationScriptPath = Path.Combine(root, "scripts", "verify-wpf-segmentation-object-interactions.ps1");
        string firstRunScriptPath = Path.Combine(root, "scripts", "verify-first-run.ps1");
        string canvasModeSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Canvas", "CanvasInteractionMode.cs"));
        string roiViewModelSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "ViewModel", "RoiImageCanvasViewModel.cs"));
        string maskOverlaySource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "ViewModel", "RoiImageCanvasMaskOverlay.cs"));
        string roiMouseUpSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "RoiInteraction", "RoiInteractionMouseUp.cs"));
        string imageCanvasSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Engine", "ImageCanvasControl.cs"));
        string shapeDrawingSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "OpenGL", "OpenGlShapeDrawing.cs"));
        string overlayCompilerSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "OpenGL", "OpenGlOverlayCompiler.cs"));
        string overlayExtensionsSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "OpenGL", "OpenGlOverlayExtensions.cs"));
        string cViewerSource = File.ReadAllText(Path.Combine(root, "Library", "CViewer.cs"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));

        AssertTrue(File.Exists(validationPath), "WPF annotation tool validation matrix should be documented");
        AssertTrue(File.Exists(objectVerificationPath), "WPF annotation object verification gate should be documented");
        AssertTrue(File.Exists(legacyAnnotationScriptPath), "legacy annotation verification script name should remain available");
        AssertTrue(File.Exists(focusedAnnotationScriptPath), "focused annotation object verification script should exist");
        AssertTrue(File.Exists(focusedRoiScriptPath), "focused ROI object verification script should exist");
        AssertTrue(File.Exists(focusedSegmentationScriptPath), "focused segmentation object verification script should exist");
        string validation = File.ReadAllText(validationPath);
        string objectVerification = File.ReadAllText(objectVerificationPath);
        string legacyAnnotationScript = File.ReadAllText(legacyAnnotationScriptPath);
        string focusedAnnotationScript = File.ReadAllText(focusedAnnotationScriptPath);
        string focusedRoiScript = File.ReadAllText(focusedRoiScriptPath);
        string focusedSegmentationScript = File.ReadAllText(focusedSegmentationScriptPath);
        string firstRunScript = File.ReadAllText(firstRunScriptPath);
        AssertTrue(validation.Contains("Validation Criteria", StringComparison.Ordinal), "validation document should state the verification criteria");
        AssertTrue(validation.Contains("Ellipse/Circle", StringComparison.Ordinal), "validation document should include ellipse/circle");
        AssertTrue(validation.Contains("Polygon", StringComparison.Ordinal), "validation document should include polygon");
        AssertTrue(validation.Contains("Brush", StringComparison.Ordinal), "validation document should include brush");
        AssertTrue(validation.Contains("Undo", StringComparison.Ordinal), "validation document should include undo");
        AssertTrue(validation.Contains("WpfAnnotationHistoryService", StringComparison.Ordinal), "validation document should include the WPF annotation history path");
        AssertTrue(validation.Contains("All palette tools currently have a verified first WPF path", StringComparison.Ordinal), "validation document should state that the current palette has no pending first-path tools");
        AssertTrue(objectVerification.Contains("--wpf-annotation-object-verification", StringComparison.Ordinal), "object verification document should name the combined annotation gate");
        AssertTrue(objectVerification.Contains("scripts\\verify-wpf-annotation-object-interactions.ps1", StringComparison.Ordinal), "object verification document should name the focused annotation script");
        AssertTrue(legacyAnnotationScript.Contains("--wpf-annotation-object-verification", StringComparison.Ordinal), "legacy annotation verification script should run the combined gate");
        AssertTrue(legacyAnnotationScript.Contains("--no-build", StringComparison.Ordinal), "legacy annotation verification script should build once and reuse the output for focused tests");
        AssertTrue(focusedAnnotationScript.Contains("--wpf-annotation-object-verification", StringComparison.Ordinal), "focused annotation script should run the combined gate");
        AssertTrue(focusedRoiScript.Contains("--wpf-roi-object-verification", StringComparison.Ordinal), "focused ROI script should run the ROI gate");
        AssertTrue(focusedSegmentationScript.Contains("--wpf-segmentation-object-verification", StringComparison.Ordinal), "focused segmentation script should run the segmentation gate");
        AssertTrue(firstRunScript.Contains("verify-wpf-annotation-object-interactions.ps1", StringComparison.Ordinal), "first-run script syntax check should include the focused annotation object script");
        AssertTrue(shellSource.Contains("UndoWpfAnnotationHistory", StringComparison.Ordinal), "WPF shell should expose its own undo history command");
        AssertTrue(shellSource.Contains("RedoWpfAnnotationHistory", StringComparison.Ordinal), "WPF shell should expose its own redo history command");
        AssertTrue(validation.Contains("The source of truth must be the original image coordinate system", StringComparison.Ordinal), "validation document should define image-pixel annotations as the implementation direction");
        AssertTrue(validation.Contains("Do not route a WPF palette action through legacy `CViewer`", StringComparison.Ordinal), "validation document should keep WPF actions off legacy CViewer shortcuts");

        AssertTrue(canvasModeSource.Contains("Drawing", StringComparison.Ordinal), "WPF canvas should expose the existing rectangle drawing mode");
        AssertTrue(canvasModeSource.Contains("Drag", StringComparison.Ordinal), "WPF canvas should expose the existing pan mode");
        AssertTrue(!canvasModeSource.Contains("Brush", StringComparison.Ordinal), "WPF canvas mode should keep brush input as image-point input instead of a legacy viewer mode");
        AssertTrue(!canvasModeSource.Contains("Polygon", StringComparison.Ordinal), "WPF canvas mode should not pretend polygon input is verified");
        AssertTrue(!canvasModeSource.Contains("Ellipse", StringComparison.Ordinal), "WPF canvas mode should keep shape type separate from viewer mode");

        AssertTrue(roiMouseUpSource.Contains("AddRectangleToOverlay", StringComparison.Ordinal), "WPF ROI interaction should have a concrete rectangle add path");
        AssertTrue(roiMouseUpSource.Contains("AddEllipseToOverlay", StringComparison.Ordinal), "WPF ROI interaction should have a concrete ellipse add path");
        AssertTrue(roiMouseUpSource.Contains("CanvasRoiShapeKind.Ellipse", StringComparison.Ordinal), "ellipse drawing should use the pixel-space ROI shape kind");
        AssertTrue(roiMouseUpSource.Contains("IsFill = shapeKind == CanvasRoiShapeKind.Ellipse", StringComparison.Ordinal), "ellipse ROI should be created as a filled label region");
        AssertTrue(roiMouseUpSource.Contains("imageViewer.RefreshGL();", StringComparison.Ordinal), "newly created ROI should repaint immediately after mouse-up");
        AssertTrue(roiViewModelSource.Contains("RoiInteractionMouseUp.AddRectangleToOverlay", StringComparison.Ordinal), "WPF ROI view model should route drawing to rectangle creation");
        AssertTrue(roiViewModelSource.Contains("DrawingShapeKind == CanvasRoiShapeKind.Ellipse", StringComparison.Ordinal), "WPF ROI view model should route the ellipse palette tool through the drawing shape kind");
        AssertTrue(roiViewModelSource.Contains("IsFill = shapeKind == CanvasRoiShapeKind.Ellipse", StringComparison.Ordinal), "redrawn ellipse ROI should remain filled");
        AssertTrue(!roiViewModelSource.Contains("SetModeSegmentationBrush", StringComparison.Ordinal), "WPF ROI view model should not use legacy brush mode directly");
        AssertTrue(!roiViewModelSource.Contains("UndoAnnotationChange", StringComparison.Ordinal), "WPF ROI view model should not use legacy undo directly");
        AssertTrue(roiViewModelSource.Contains("ImagePointClicked", StringComparison.Ordinal), "WPF ROI view model should expose image-pixel click input for polygon drawing");
        AssertTrue(roiViewModelSource.Contains("ImagePointHovered", StringComparison.Ordinal), "WPF ROI view model should expose image-pixel hover input for brush cursor preview");
        AssertTrue(roiViewModelSource.Contains("ImagePointMoved", StringComparison.Ordinal), "WPF ROI view model should expose image-pixel drag input for mask drawing");
        AssertTrue(roiViewModelSource.Contains("ImagePointReleased", StringComparison.Ordinal), "WPF ROI view model should expose image-pixel release input for committing drag edits");
		AssertTrue(roiViewModelSource.Contains("New-box feedback is drawn as one live shape", StringComparison.Ordinal), "WPF ROI view model should draw a lightweight in-progress ROI preview");
        AssertTrue(roiViewModelSource.Contains("SetBrushCursorPreview", StringComparison.Ordinal), "WPF ROI view model should keep a visible image-pixel brush radius preview");
        AssertTrue(roiViewModelSource.Contains("DrawBrushCursorPreview", StringComparison.Ordinal), "WPF ROI view model should draw the brush cursor preview in OpenGL");
        AssertTrue(roiViewModelSource.Contains("SetPolygonOverlays", StringComparison.Ordinal), "WPF ROI view model should render polygon preview overlays in the OpenGL viewer");
        AssertTrue(roiViewModelSource.Contains("SetMaskOverlays", StringComparison.Ordinal), "WPF ROI view model should expose a dedicated raster-mask overlay path");
        AssertTrue(roiViewModelSource.Contains("TexImage2D", StringComparison.Ordinal), "WPF raster masks should render through an OpenGL texture upload path");
        AssertTrue(roiViewModelSource.Contains("TexSubImage2D", StringComparison.Ordinal), "WPF raster masks should update changed mask regions without reuploading the whole texture");
        AssertTrue(roiViewModelSource.Contains("GL_TEXTURE_2D", StringComparison.Ordinal), "WPF raster masks should draw as OpenGL texture quads instead of contour fragments");
        AssertTrue(maskOverlaySource.Contains("IsSelected", StringComparison.Ordinal), "WPF raster mask overlays should carry selected object state");
        AssertTrue(maskOverlaySource.Contains("DirtyBounds", StringComparison.Ordinal), "WPF raster mask overlays should carry changed image-pixel bounds for partial OpenGL updates");
        AssertTrue(roiViewModelSource.Contains("DrawSelectedMaskOverlayMarkers", StringComparison.Ordinal), "WPF raster mask selection should draw above regular image and object overlays");
        AssertTrue(roiViewModelSource.Contains("ClearRoiSelection", StringComparison.Ordinal), "WPF canvas should allow non-ROI object selection to clear stale ROI handles");
        AssertTrue(roiViewModelSource.Contains("DrawDetectionScreenMarker", StringComparison.Ordinal), "WPF raster mask selection should reuse the polished OpenGL object marker");
        AssertTrue(shellSource.Contains("RoiImageCanvasMaskOverlay", StringComparison.Ordinal), "WPF shell should pass raster masks to the texture overlay path");
        AssertTrue(shellSource.Contains("TryBeginSelectedSegmentEdit", StringComparison.Ordinal), "WPF shell should start selected mask/polygon edit from image-pixel hit testing");
        AssertTrue(shellSource.Contains("TryMoveSelectedSegmentEdit", StringComparison.Ordinal), "WPF shell should move selected masks and polygon points through image-pixel drags");
        AssertTrue(shellSource.Contains("TryMoveRasterMask", StringComparison.Ordinal), "WPF shell should support moving selected raster masks");
        AssertTrue(shellSource.Contains("TryMovePoint", StringComparison.Ordinal), "WPF shell should support selected polygon point editing");
        AssertTrue(shellSource.Contains("FindNearestPointIndex", StringComparison.Ordinal), "WPF shell should hit-test polygon points before adding a new polygon point");
        AssertTrue(shellSource.Contains("RefreshPolygonOverlays();", StringComparison.Ordinal), "WPF object selection changes should refresh the canvas overlay selection state");
        AssertTrue(shellSource.Contains("MainCanvasViewModel_ImagePointHovered", StringComparison.Ordinal), "WPF shell should update brush cursor preview from image-pixel hover events");
        AssertTrue(!shellSource.Contains("RasterMaskToRegions", StringComparison.Ordinal), "WPF shell should not turn raster masks into polygon contour overlays");

        AssertTrue(imageCanvasSource.Contains("CircleInfo", StringComparison.Ordinal), "OpenGL canvas has low-level circle rendering support");
        AssertTrue(shapeDrawingSource.Contains("DrawCircle", StringComparison.Ordinal), "OpenGL shape drawing has circle primitives");
        AssertTrue(shapeDrawingSource.Contains("DrawEllipse", StringComparison.Ordinal), "OpenGL shape drawing should render pixel-space ellipse ROI bounds");
        AssertTrue(shapeDrawingSource.Contains("for (int i = 0; i <= segments; i++)", StringComparison.Ordinal), "ellipse fill triangle fan should close the center-to-edge seam");
        AssertTrue(shapeDrawingSource.Contains("Math.Min(a, 0.24F)", StringComparison.Ordinal), "ellipse fill should be translucent instead of hiding the source image");
        AssertTrue(shapeDrawingSource.Contains("gl.Disable(OpenGL.GL_TEXTURE_2D);", StringComparison.Ordinal), "ellipse rendering should not inherit texture state from the image layer");
        AssertTrue(shapeDrawingSource.Contains("GL_LINE_LOOP", StringComparison.Ordinal), "ellipse rendering should keep a visible outline around the filled region");
        AssertTrue(overlayCompilerSource.Contains("newObject.IsFill && !renderedAsEllipse", StringComparison.Ordinal), "ellipse fill should not be followed by a rectangular fill overlay");
        AssertTrue(overlayExtensionsSource.Contains("newObject.Shape.OnChanged?.Invoke();", StringComparison.Ordinal), "new ROI overlays should compile their display list immediately after being added");
        AssertTrue(shapeDrawingSource.Contains("DrawFilledPolygon", StringComparison.Ordinal), "OpenGL shape drawing has polygon primitives");
        AssertTrue(cViewerSource.Contains("SetModeSegmentationBrush", StringComparison.Ordinal), "legacy CViewer has brush support that is not the WPF palette path");
        AssertTrue(cViewerSource.Contains("SetModeSegmentationEraser", StringComparison.Ordinal), "legacy CViewer has eraser support that is not the WPF palette path");
        AssertTrue(cViewerSource.Contains("UndoAnnotationChange", StringComparison.Ordinal), "legacy CViewer has undo support that is not the WPF palette path");
        AssertTrue(!shellSource.Contains("SetModeSegmentationBrush(", StringComparison.Ordinal), "WPF shell should not route brush palette clicks through legacy CViewer");
        AssertTrue(!shellSource.Contains("UndoAnnotationChange(", StringComparison.Ordinal), "WPF shell should not route undo palette clicks through legacy CViewer");

        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Rectangle), "box should stay connected");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Ellipse), "ellipse should be connected through pixel-space bounding boxes");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.PanZoom), "pan should stay connected");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Delete), "delete should stay connected");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Polygon), "polygon should be connected through WPF image-pixel input and OpenGL preview");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Brush), "brush should be connected through WPF image-pixel mask input and OpenGL preview");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Eraser), "eraser should be connected through WPF image-pixel mask input and OpenGL preview");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Undo), "undo should be connected through WPF annotation history");
        AssertTrue(WpfAnnotationToolCapabilityService.IsConnected(WpfAnnotationTool.Redo), "redo should be connected through WPF annotation history");
    }

    private static void TestWpfEllipseAnnotationStoresPixelBounds()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.LastSelectImageName = "ellipse-sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.Lime });
            CGlobal.Inst.Data = data;

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                SetPrivateField(window, "activeImageSize", new Size(100, 100));
                SetPrivateField(window, "activeImageBitmap", new Bitmap(100, 100));

                var rect = new CanvasRect<float>(10, 80, 50, 40)
                {
                    UniqueId = "ellipse-1",
                    ShapeKind = CanvasRoiShapeKind.Ellipse
                };
                var args = new RoiChangedEventArgs { RoiRect = rect };

                InvokePrivateResult<object>(window, "MainCanvasViewModel_RoiAdded", window.MainCanvasViewModel, args);

                var manualRois = GetPrivateField<List<Rectangle>>(window, "manualRois");
                var manualShapeKinds = GetPrivateField<List<CanvasRoiShapeKind>>(window, "manualRoiShapeKinds");
                var manualOverlayIds = GetPrivateField<List<string>>(window, "manualRoiOverlayIds");
                AssertEqual(1, manualRois.Count);
                AssertEqual(new Rectangle(10, 20, 40, 40), manualRois[0]);
                AssertEqual(CanvasRoiShapeKind.Ellipse, manualShapeKinds[0]);
                AssertEqual("ellipse-1", manualOverlayIds[0]);

                Dictionary<string, List<CRectangleObject>> roisByClass = InvokePrivateResult<Dictionary<string, List<CRectangleObject>>>(window, "BuildAnnotationRois");
                AssertTrue(roisByClass.ContainsKey("Defect"), "ellipse annotation should export through the selected/default class");
                AssertEqual(new Rectangle(10, 20, 40, 40), roisByClass["Defect"][0].Roi);

                InvokePrivate(window, "RedrawReviewRois");
                AssertEqual(CanvasRoiShapeKind.Ellipse, manualShapeKinds[0]);
                AssertTrue(!string.IsNullOrWhiteSpace(manualOverlayIds[0]), "redraw should keep a live overlay id for later edit/delete sync");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfTutorialSampleFlowSideEffects()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        try
        {
            string sampleRoot = Path.Combine(root, "images");
            string outputRoot = Path.Combine(root, "dataset");
            Directory.CreateDirectory(sampleRoot);
            string imagePath = Path.Combine(sampleRoot, "tutorial-sample.jpg");
            CreateVisualSmokeImage(imagePath);

            var data = new CData();
            data.ConfigureOutputRoot(outputRoot);
            data.ProjectSettings.PythonModel.ImageRootPath = sampleRoot;
            data.ProjectSettings.PythonModel.ProjectRootPath = root;
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "Defect", DrawColor = Color.Lime });
            CGlobal.Inst.Data = data;

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                var learningPanel = (WpfLearningWorkflowPanel)window.FindName("LearningWorkflowPanelControl");
                var detectButton = (System.Windows.Controls.Control)window.FindName("DetectButton");
                var objectsTab = (System.Windows.Controls.TabItem)window.FindName("ObjectsReviewTab");
                var candidatesTab = (System.Windows.Controls.TabItem)window.FindName("CandidatesReviewTab");

                SelectTutorialStep(window, learningPanel, WpfLearningStep.Sample);
                AssertEqual(imagePath, GetPrivateField<string>(window, "activeImagePath"));
                AssertTrue(window.ImageQueueItems.Count > 0, "tutorial sample step should populate the image queue");

                SelectTutorialStep(window, learningPanel, WpfLearningStep.Label);
                var manualRois = GetPrivateField<List<System.Drawing.Rectangle>>(window, "manualRois");
                AssertEqual(1, manualRois.Count);
                AssertEqual(WpfAnnotationTool.Rectangle, learningPanel.ViewModel.SelectedTool.Tool);
                AssertTrue(objectsTab.IsSelected, "tutorial label step should move the user to object review");
                AssertTrue(!detectButton.IsEnabled, "tutorial label step should keep inference disabled in labeling mode");

                SelectTutorialStep(window, learningPanel, WpfLearningStep.Infer);
                AssertTrue(detectButton.IsEnabled, "tutorial infer step should enable current-image inference");

                objectsTab.IsSelected = true;
                SelectTutorialStep(window, learningPanel, WpfLearningStep.Review);
                AssertTrue(candidatesTab.IsSelected, "tutorial review step should select the AI candidate review tab");

                SelectTutorialStep(window, learningPanel, WpfLearningStep.Save);
                string labelPath = Path.Combine(outputRoot, "data", "train", "labels", "tutorial-sample.txt");
                AssertTrue(File.Exists(labelPath), "tutorial save step should write the YOLO label file");
                AssertTrue(File.ReadAllLines(labelPath).Length > 0, "tutorial save step should persist at least one label line");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfTenMinuteLabelingSessionFlow()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        try
        {
            string imageRoot = Path.Combine(root, "images");
            string outputRoot = Path.Combine(root, "dataset");
            Directory.CreateDirectory(imageRoot);
            string imagePath = Path.Combine(imageRoot, "session-0.jpg");
            CreateVisualSmokeImage(imagePath);
            CreateVisualSmokeImage(Path.Combine(imageRoot, "session-1.jpg"));
            CGlobal.Inst.Data = CreateWpfLabelingSessionData(root, imageRoot, outputRoot);

            WpfLabelingShellWindow window = new WpfLabelingShellWindow
            {
                Width = 1430,
                Height = 900,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual
            };

            try
            {
                window.Show();
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(200));
                WpfLabelingSessionResult result = ExecuteWpfLabelingSessionFlow(window, imagePath, outputRoot);
                AssertTrue(result.SavedLabelLines >= 2, "session should save YOLO box labels");
                AssertTrue(result.SavedSegmentFiles > 0, "session should save segmentation labels");
                AssertEqual(1, result.SkippedCandidates);
                AssertEqual(1, result.ConfirmedCandidates);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfYoloTrainingSessionFlow()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        CCommunicationLearning previousCommunication = GetPrivateField<CCommunicationLearning>(CGlobal.Inst, "deepLearning");
        CCommunicationLearning trainingCommunication = null;
        string root = CreateTempRoot();

        try
        {
            string imageRoot = Path.Combine(root, "images");
            string outputRoot = Path.Combine(root, "dataset");
            Directory.CreateDirectory(imageRoot);
            CGlobal.Inst.Data = CreateWpfLabelingSessionData(root, imageRoot, outputRoot);
            CGlobal.Inst.Data.ProjectSettings.PythonModel.AutoStartClient = false;
            CGlobal.Inst.Data.ProjectSettings.YoloDataset.ValidationPercent = 50;
            CGlobal.Inst.Data.ProjectSettings.YoloDataset.SplitSeed = 17;
            CreateTrainingSessionImages(imageRoot, CGlobal.Inst.Data.ProjectSettings.YoloDataset, out string trainImagePath, out string validImagePath);

            int port = GetAvailableTcpPort();
            trainingCommunication = new CCommunicationLearning(startListen: false, port: port);
            CGlobal.Inst.DeepLearning = trainingCommunication;
            AssertTrue(trainingCommunication.Start(), "training session listener did not start");
            using var requestReceived = new ManualResetEventSlim(false);
            using var statusSent = new ManualResetEventSlim(false);
            Task clientTask = Task.Run(() => RunMockTrainingClient(port, requestReceived, statusSent));
            AssertTrue(WaitUntil(() => trainingCommunication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(5)), "mock training client did not connect to listener");

            WpfLabelingShellWindow window = new WpfLabelingShellWindow
            {
                Width = 1430,
                Height = 900,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual
            };

            try
            {
                window.Show();
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(200));
                WpfYoloTrainingSessionResult result = ExecuteWpfYoloTrainingSessionFlow(
                    window,
                    trainImagePath,
                    validImagePath,
                    outputRoot,
                    requestReceived,
                    statusSent);
                AssertTrue(result.DatasetReady, "training session dataset should be ready");
                AssertTrue(result.StartTrainingPacketReceived, "training session should send StartTraining");
                AssertTrue(result.CompletedStatusReceived, "training session should receive completed status");
                AssertTrue(result.AppliedWeightsPath.EndsWith("best.pt", StringComparison.OrdinalIgnoreCase), "training session should apply best.pt");
                AssertTrue(clientTask.Wait(TimeSpan.FromSeconds(5)), "mock training client did not finish");
                if (clientTask.IsFaulted && clientTask.Exception != null)
                {
                    throw clientTask.Exception;
                }
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            trainingCommunication?.Close();
            CGlobal.Inst.DeepLearning = previousCommunication;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void SelectTutorialStep(
        WpfLabelingShellWindow window,
        WpfLearningWorkflowPanel panel,
        WpfLearningStep step)
    {
        WpfLearningStepItem item = panel.ViewModel.LearningSteps.First(candidate => candidate.Step == step);
        bool alreadySelected = ReferenceEquals(panel.StepList.SelectedItem, item);
        panel.StepList.SelectedItem = item;
        if (alreadySelected)
        {
            InvokePrivateResult<object>(window, "LearningStepListBox_SelectionChanged", panel.StepList, null);
        }

        PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
    }

    private static void TestProgramDefaultsToWpfShell()
    {
        string programSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Program.cs"));

        AssertTrue(programSource.Contains("RunWpfApplication();", StringComparison.Ordinal), "application startup should run the WPF shell");
        AssertTrue(!programSource.Contains("FormMainFrame", StringComparison.Ordinal), "application startup should not reference FormMainFrame");
        AssertTrue(!programSource.Contains("RunWinFormsApplication", StringComparison.Ordinal), "application startup should not keep the legacy WinForms shell runner");
        AssertTrue(!programSource.Contains("--winforms-shell", StringComparison.Ordinal), "legacy WinForms shell command-line option should be removed");
        AssertTrue(!programSource.Contains("--legacy-winforms", StringComparison.Ordinal), "legacy WinForms shell command-line option should be removed");
    }

    private static void TestWpfLabelingShellWindowConstructs()
    {
        string root = FindRepositoryRoot();
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        XDocument shellXaml = XDocument.Load(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml"));
        XDocument queueXaml = XDocument.Load(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfImageQueuePanel.xaml"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));

        AssertNamedXamlBinding(shellXaml, xName, "DetectButton", "IsEnabled", "IsCurrentImageDetectionEnabled");
        AssertNamedXamlBinding(shellXaml, xName, "TeachingModeButton", "IsEnabled", "IsLabelingModeButtonEnabled");
        AssertNamedXamlBinding(shellXaml, xName, "TeachingModeButton", "Tag", "IsLabelingModeActive");
        AssertNamedXamlBinding(shellXaml, xName, "InferenceModeButton", "IsEnabled", "IsInferenceModeButtonEnabled");
        AssertNamedXamlBinding(shellXaml, xName, "InferenceModeButton", "Tag", "IsInferenceModeActive");
        AssertNamedXamlBinding(queueXaml, xName, "OpenSelectedQueueImageButton", "IsEnabled", "IsOpenSelectedImageEnabled");
        AssertNamedXamlBinding(queueXaml, xName, "DetectSelectedQueueButton", "IsEnabled", "IsDetectSelectedEnabled");
        AssertNamedXamlBinding(queueXaml, xName, "BatchDetectQueueButton", "IsEnabled", "IsBatchDetectEnabled");
        AssertNamedXamlBinding(queueXaml, xName, "RetryFailedQueueButton", "IsEnabled", "IsRetryFailedEnabled");
        AssertNamedXamlBinding(queueXaml, xName, "StopBatchQueueButton", "IsEnabled", "IsStopBatchEnabled");
        AssertTrue(shellSource.Contains("ShellViewModel.ApplyWorkflowCommandState", StringComparison.Ordinal), "WPF shell should push current-image detection availability through the shell ViewModel");
        AssertTrue(shellSource.Contains("ShellViewModel?.SetWorkflowModeState", StringComparison.Ordinal), "WPF shell should push top workflow mode button state through the shell ViewModel");
        AssertTrue(shellSource.Contains("ImageQueuePanelControl.ViewModel.ApplyWorkflowCommandState", StringComparison.Ordinal), "WPF shell should push queue detection availability through the image queue ViewModel");
        AssertTrue(shellSource.Contains("ImageQueuePanelControl.ViewModel.SetSelectedImageAvailability", StringComparison.Ordinal), "WPF shell should push selected queue image availability through the image queue ViewModel");
        AssertTrue(!shellSource.Contains("TeachingModeButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the labeling mode button on the normal path");
        AssertTrue(!shellSource.Contains("InferenceModeButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the inference mode button on the normal path");
        AssertTrue(!shellSource.Contains("ApplyWorkflowModeButtonState", StringComparison.Ordinal), "WPF shell should not keep direct workflow button styling in code-behind");
        AssertTrue(!shellSource.Contains("OpenSelectedQueueImageButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the selected image open button on the normal path");
        AssertTrue(!shellSource.Contains("DetectButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the current-image detect button on the normal path");
        AssertTrue(!shellSource.Contains("DetectSelectedQueueButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the selected queue detect button on the normal path");
        AssertTrue(!shellSource.Contains("BatchDetectQueueButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the batch queue detect button on the normal path");
        AssertTrue(!shellSource.Contains("RetryFailedQueueButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the retry queue detect button on the normal path");
        AssertTrue(!shellSource.Contains("StopBatchQueueButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the stop batch button on the normal path");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertEqual("OpenVisionLab Labeling Studio", window.Title);
            AssertTrue(window.GetType().BaseType?.FullName == "Wpf.Ui.Controls.FluentWindow", "WPF shell should inherit WPF-UI FluentWindow");
            AssertTrue(window.MainCanvasViewModel != null, "WPF shell canvas view model was not created");
            AssertTrue(window.ShellViewModel != null, "WPF shell view model was not created");
            AssertEqual("WpfLabelingShellWindow", window.ShellViewModel.ViewName);
            AssertTrue(window.FindName("ShellTitleBar") != null, "WPF-UI title bar was not created");
            AssertTrue(window.FindName("ShellTitleBar").GetType().FullName == "Wpf.Ui.Controls.TitleBar", "WPF shell title bar should use WPF-UI TitleBar");
            AssertTrue(window.FindName("FirstCheckYoloButton") != null, "WPF YOLO first-check button was not created");
            AssertTrue(window.FindName("InstallRequirementsButton") != null, "WPF YOLO install button was not created");
            AssertTrue(window.FindName("RunYoloSmokeButton") != null, "WPF YOLO test button was not created");
            AssertTrue(window.FindName("RestartPythonWorkerButton") != null, "WPF YOLO restart button was not created");
            AssertTrue(window.FindName("StopPythonWorkerButton") != null, "WPF YOLO stop button was not created");
            AssertTrue(window.FindName("YoloStatusPanelControl") != null, "WPF YOLO status user control was not created");
            AssertTrue(window.FindName("YoloStatusPanelControl").GetType().FullName == "MvcVisionSystem.WpfYoloStatusPanel", "WPF YOLO status should be hosted by a UserControl");
            AssertTrue(((WpfYoloStatusPanel)window.FindName("YoloStatusPanelControl")).ViewModel != null, "WPF YOLO status view model was not created");
            AssertTrue(window.FindName("ProjectConfigPanelControl") != null, "WPF project config user control was not created");
            AssertTrue(window.FindName("ProjectConfigPanelControl").GetType().FullName == "MvcVisionSystem.WpfProjectConfigPanel", "WPF project config should be hosted by a UserControl");
            AssertTrue(((WpfProjectConfigPanel)window.FindName("ProjectConfigPanelControl")).ViewModel != null, "WPF project config view model was not created");
            AssertTrue(window.FindName("ProjectRecipeNameBox") != null, "WPF project recipe name box was not created");
            AssertTrue(window.FindName("ProjectRecipeListBox") != null, "WPF project recipe list was not created");
            AssertTrue(window.FindName("ProjectConfigPathBox") != null, "WPF project config path box was not created");
            AssertTrue(window.FindName("ApplyProjectRecipeButton") != null, "WPF project recipe apply button was not created");
            AssertTrue(window.FindName("RefreshProjectRecipeListButton") != null, "WPF project recipe refresh button was not created");
            AssertTrue(window.FindName("SaveProjectConfigButton") != null, "WPF project config save button was not created");
            AssertTrue(window.FindName("OpenProjectConfigFolderButton") != null, "WPF project config folder button was not created");
            AssertTrue(window.FindName("ThemeToggleButton") != null, "WPF theme toggle button was not created");
            AssertTrue(window.FindName("ThemeToggleText") != null, "WPF theme toggle text was not created");
            AssertTrue(window.FindName("ThemeToggleButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF theme toggle should use WPF-UI button");
            AssertTrue(window.FindName("YoloCommandStatusText") != null, "WPF YOLO command status text was not created");
            AssertTrue(window.FindName("YoloSettingsScrollViewer") != null, "WPF YOLO settings scroll viewer was not created");
            AssertTrue(window.FindName("YoloModelSettingsPanelControl") != null, "WPF YOLO model settings user control was not created");
            AssertTrue(window.FindName("YoloModelSettingsPanelControl").GetType().FullName == "MvcVisionSystem.WpfYoloModelSettingsPanel", "WPF YOLO model settings should be hosted by a UserControl");
            AssertTrue(((WpfYoloModelSettingsPanel)window.FindName("YoloModelSettingsPanelControl")).ViewModel != null, "WPF YOLO model settings view model was not created");
            AssertTrue(window.FindName("YoloProjectRootBox") != null, "WPF YOLO model settings editor was not created");
            AssertTrue(window.FindName("BrowseYoloPythonButton") != null, "WPF YOLO python browse button was not created");
            AssertTrue(window.FindName("BrowseYoloProjectRootButton") != null, "WPF YOLO project browse button was not created");
            AssertTrue(window.FindName("BrowseYoloClientScriptButton") != null, "WPF YOLO client browse button was not created");
            AssertTrue(window.FindName("BrowseYoloWeightsButton") != null, "WPF YOLO weights browse button was not created");
            AssertTrue(window.FindName("BrowseYoloImageRootButton") != null, "WPF YOLO image root browse button was not created");
            AssertTrue(window.FindName("SaveYoloSettingsButton") != null, "WPF YOLO settings save button was not created");
            AssertTrue(window.FindName("TrainingImageSizeBox") != null, "WPF training settings editor was not created");
            AssertTrue(window.FindName("TrainingProgressText") != null, "WPF training progress text was not created");
            AssertTrue(window.FindName("TrainingEpochText") != null, "WPF training epoch text was not created");
            AssertTrue(window.FindName("TrainingSettingsExpander") != null, "WPF training settings expander was not created");
            AssertTrue(window.FindName("TrainingSettingsPanelControl") != null, "WPF training settings user control was not created");
            AssertTrue(window.FindName("TrainingSettingsPanelControl").GetType().FullName == "MvcVisionSystem.WpfTrainingSettingsPanel", "WPF training settings should be hosted by a UserControl");
            AssertTrue(((WpfTrainingSettingsPanel)window.FindName("TrainingSettingsPanelControl")).ViewModel != null, "WPF training settings view model was not created");
            AssertTrue(window.FindName("StartTrainingButton") != null, "WPF training start button was not created");
            AssertTrue(window.FindName("CandidateReviewPanelControl") != null, "WPF candidate review user control was not created");
            AssertTrue(window.FindName("CandidateReviewPanelControl").GetType().FullName == "MvcVisionSystem.WpfCandidateReviewPanel", "WPF candidate review should be hosted by a UserControl");
            AssertTrue(((WpfCandidateReviewPanel)window.FindName("CandidateReviewPanelControl")).ViewModel != null, "WPF candidate review view model was not created");
            AssertTrue(window.FindName("CandidateConfidenceSlider") != null, "WPF candidate confidence filter was not created");
            AssertTrue(window.FindName("TeachingModeButton") != null, "WPF labeling mode button was not created");
            AssertTrue(window.FindName("InferenceModeButton") != null, "WPF inference mode button was not created");
            AssertTrue(window.FindName("ImageQueuePanelControl") != null, "WPF image queue user control was not created");
            AssertTrue(window.FindName("ImageQueuePanelControl").GetType().FullName == "MvcVisionSystem.WpfImageQueuePanel", "WPF image queue should be hosted by a UserControl");
            AssertTrue(((WpfImageQueuePanel)window.FindName("ImageQueuePanelControl")).ViewModel != null, "WPF image queue view model was not created");
            AssertTrue(window.FindName("DetectSelectedQueueButton") != null, "WPF queue selected detect button was not created");
            AssertTrue(window.FindName("BatchDetectQueueButton") != null, "WPF queue batch detect button was not created");
            AssertTrue(window.FindName("RetryFailedQueueButton") != null, "WPF queue retry button was not created");
            AssertTrue(window.FindName("StopBatchQueueButton") != null, "WPF queue stop button was not created");
            var imageQueueGrid = window.FindName("ImageQueueGrid") as System.Windows.Controls.DataGrid;
            AssertTrue(imageQueueGrid != null, "WPF image queue grid was not created");
            AssertTrue(imageQueueGrid.Columns[0] is System.Windows.Controls.DataGridTemplateColumn, "WPF image queue file column should use icon/detail template");
            string imageQueueXaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfImageQueuePanel.xaml"));
            AssertTrue(imageQueueXaml.Contains("EnableRowVirtualization=\"True\"", StringComparison.Ordinal), "WPF image queue should keep row virtualization enabled for large folders");
            AssertTrue(imageQueueXaml.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", StringComparison.Ordinal), "WPF image queue should recycle row containers for large folders");
            AssertTrue(window.FindName("StatusBarPanelControl") != null, "WPF status bar user control was not created");
            AssertTrue(window.FindName("StatusBarPanelControl").GetType().FullName == "MvcVisionSystem.WpfStatusBarPanel", "WPF status bar should be hosted by a UserControl");
            AssertTrue(((WpfStatusBarPanel)window.FindName("StatusBarPanelControl")).ViewModel != null, "WPF status bar view model was not created");
            AssertTrue(window.FindName("DatasetStatusText") != null, "WPF dataset status text was not created");
            AssertTrue(window.FindName("PythonStatusText") != null, "WPF python status text was not created");
            AssertTrue(window.FindName("AnnotationSaveStatusText") != null, "WPF annotation save status text was not created");
            AssertTrue(window.FindName("ModelStatusText") != null, "WPF model status text was not created");
            AssertTrue(window.FindName("ShellLogPanelControl") != null, "WPF log user control was not created");
            AssertTrue(window.FindName("ShellLogPanelControl").GetType().FullName == "MvcVisionSystem.WpfShellLogPanel", "WPF log should be hosted by a UserControl");
            AssertTrue(((WpfShellLogPanel)window.FindName("ShellLogPanelControl")).ViewModel != null, "WPF log view model was not created");
            AssertTrue(window.FindName("ShellLogPanel") != null, "WPF log panel was not created");
            AssertTrue(window.FindName("ShellLogPanel").GetType().FullName == "OpenVisionLab.Logging.Controls.View.LogPanelView", "WPF shell should use the OpenVisionLab logging WPF panel");
            AssertTrue(window.FindName("ObjectReviewSummaryText") != null, "WPF object review summary was not created");
            AssertTrue(window.FindName("ObjectReviewPanelControl") != null, "WPF object review user control was not created");
            AssertTrue(window.FindName("ObjectReviewPanelControl").GetType().FullName == "MvcVisionSystem.WpfObjectReviewPanel", "WPF object review should be hosted by a UserControl");
            AssertTrue(((WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl")).ViewModel != null, "WPF object review view model was not created");
            AssertTrue(window.FindName("DeleteObjectButton") != null, "WPF object delete button was not created");
            AssertTrue(window.FindName("DeleteObjectButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF object delete button should use WPF-UI button");
            AssertTrue(window.FindName("ObjectClassBox") != null, "WPF object class selector was not created");
            AssertTrue(window.FindName("ApplyObjectClassButton") != null, "WPF object class apply button was not created");
            AssertTrue(window.FindName("ApplyObjectClassButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF object class apply button should use WPF-UI button");
            AssertTrue(window.FindName("DetectionResultOverlay") != null, "WPF detection result overlay was not created");
            AssertTrue(window.FindName("CanvasPanelControl") != null, "WPF canvas user control was not created");
            AssertTrue(window.FindName("CanvasPanelControl").GetType().FullName == "MvcVisionSystem.WpfCanvasPanel", "WPF canvas should be hosted by a UserControl");
            AssertTrue(((WpfCanvasPanel)window.FindName("CanvasPanelControl")).ViewModel != null, "WPF canvas panel view model was not created");
            AssertTrue(window.FindName("DetectionOverlaySummaryText") != null, "WPF detection summary text was not created");
            AssertTrue(window.FindName("QueuePreviewImage") == null, "WPF queue preview image should be removed from the one-click canvas workflow");
            AssertTrue(window.FindName("QueuePreviewText") == null, "WPF queue preview text should be removed from the one-click canvas workflow");
            AssertTrue(window.FindName("ClassCatalogPanelControl") != null, "WPF class catalog user control was not created");
            AssertTrue(window.FindName("ClassCatalogPanelControl").GetType().FullName == "MvcVisionSystem.WpfClassCatalogPanel", "WPF class catalog should be hosted by a UserControl");
            AssertTrue(((WpfClassCatalogPanel)window.FindName("ClassCatalogPanelControl")).ViewModel != null, "WPF class catalog view model was not created");
            AssertTrue(window.FindName("ClassNameBox") != null, "WPF class name editor was not created");
            AssertTrue(window.FindName("AddClassButton") != null, "WPF class add button was not created");
            AssertTrue(window.FindName("RemoveClassButton") != null, "WPF class remove button was not created");
            AssertTrue(window.FindName("OutputRootPathBox") != null, "WPF output root path editor was not created");
            AssertTrue(window.FindName("BrowseOutputRootButton") != null, "WPF output root browse button was not created");
            AssertTrue(window.FindName("SaveOutputRootButton") != null, "WPF output root save button was not created");
            AssertTrue(window.FindName("SaveAnnotationsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF save annotations button should use WPF-UI button");
            AssertTrue(window.FindName("DetectButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF detect button should use WPF-UI button");
            AssertTrue(window.FindName("ConfirmSelectedCandidateButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF selected confirm button should use WPF-UI button");
            AssertTrue(window.FindName("DetectSelectedQueueButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF queue selected detect button should use WPF-UI button");
            AssertTrue(window.FindName("BatchDetectQueueButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF queue batch detect button should use WPF-UI button");
            AssertTrue(window.FindName("RetryFailedQueueButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF queue retry button should use WPF-UI button");
            AssertTrue(window.FindName("StopBatchQueueButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF queue stop button should use WPF-UI button");
            AssertTrue(window.FindName("FirstCheckYoloButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO first-check button should use WPF-UI button");
            AssertTrue(window.FindName("InstallRequirementsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO install button should use WPF-UI button");
            AssertTrue(window.FindName("RunYoloSmokeButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO test button should use WPF-UI button");
            AssertTrue(window.FindName("RestartPythonWorkerButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO restart button should use WPF-UI button");
            AssertTrue(window.FindName("StopPythonWorkerButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO stop button should use WPF-UI button");
            AssertTrue(window.FindName("SaveYoloSettingsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO settings save button should use WPF-UI button");
            AssertTrue(window.FindName("ResetYoloSettingsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO settings reset button should use WPF-UI button");
            AssertTrue(window.FindName("RefreshTrainingReadinessButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF training readiness button should use WPF-UI button");
            AssertTrue(window.FindName("StartTrainingButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF training start button should use WPF-UI button");
            AssertTrue(window.FindName("StopTrainingButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF training stop button should use WPF-UI button");
            AssertEqual("새로고침", ((System.Windows.Controls.ContentControl)window.FindName("RefreshTrainingReadinessButton")).Content?.ToString());
            AssertEqual("시작", ((System.Windows.Controls.ContentControl)window.FindName("StartTrainingButton")).Content?.ToString());
            AssertEqual("중지", ((System.Windows.Controls.ContentControl)window.FindName("StopTrainingButton")).Content?.ToString());
            AssertTrue(window.FindName("AddClassButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF class add button should use WPF-UI button");
            AssertTrue(window.FindName("RemoveClassButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF class remove button should use WPF-UI button");
            AssertTrue(window.FindName("BrowseOutputRootButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF output root browse button should use WPF-UI button");
            AssertTrue(window.FindName("SaveOutputRootButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF output root save button should use WPF-UI button");
            AssertTrue(window.FindName("BrowseYoloPythonButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO python browse button should use WPF-UI button");
            AssertTrue(window.FindName("BrowseYoloProjectRootButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO project browse button should use WPF-UI button");
            AssertTrue(window.FindName("BrowseYoloClientScriptButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO client browse button should use WPF-UI button");
            AssertTrue(window.FindName("BrowseYoloWeightsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO weights browse button should use WPF-UI button");
            AssertTrue(window.FindName("BrowseYoloImageRootButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO image root browse button should use WPF-UI button");

            window.FocusYoloSettingsTab();
            AssertTrue(window.FindName("YoloSettingsReviewTab") is System.Windows.Controls.TabItem yoloSettingsTab && yoloSettingsTab.IsSelected, "WPF shell should focus the YOLO settings tab");
            AssertTrue(((WpfYoloModelSettingsPanel)window.FindName("YoloModelSettingsPanelControl")).SettingsExpander.IsExpanded, "WPF YOLO model settings should open when launched from legacy buttons");
            AssertTrue(((WpfTrainingSettingsPanel)window.FindName("TrainingSettingsPanelControl")).SettingsExpander.IsExpanded, "WPF training settings should open when launched from legacy buttons");

            window.FocusClassCatalogTab();
            AssertTrue(window.FindName("ClassesReviewTab") is System.Windows.Controls.TabItem classesReviewTab && classesReviewTab.IsSelected, "WPF shell should focus the class catalog tab");
            AssertTrue(window.FindName("ClassCatalogPanelControl") is WpfClassCatalogPanel, "WPF class catalog should be visible through the shell focus method");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestLegacySettingsButtonsRouteToWpfShell()
    {
        string root = FindRepositoryRoot();
        string programSource = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));

        AssertTrue(shellSource.Contains("FocusYoloSettingsTab", StringComparison.Ordinal), "WPF shell should own the YOLO settings focus path");
        AssertTrue(shellSource.Contains("FocusClassCatalogTab", StringComparison.Ordinal), "WPF shell should own the class catalog focus path");
        AssertTrue(!programSource.Contains("FormMainFrame", StringComparison.Ordinal), "program startup should not keep the legacy WinForms main frame");
        AssertTrue(!File.Exists(Path.Combine(root, "0. UI", "8) TeachingPanel", "FormTrainingPanel.cs")), "legacy training panel should be removed");
        AssertTrue(!File.Exists(Path.Combine(root, "0. UI", "8) TeachingPanel", "FormVision_NewPanel.cs")), "legacy new-panel dialog should be removed");
        AssertTrue(!File.Exists(Path.Combine(root, "Yolo", "FormVision_ClassMenu.cs")), "legacy class menu dialog should be removed");
        AssertTrue(!File.Exists(Path.Combine(root, "Yolo", "FormVision_Yolov5ParamSetting.cs")), "legacy YOLO settings dialog should be removed");
    }

    private static void TestWpfMigrationRemovesLegacyWinFormsSupportLibraries()
    {
        string root = FindRepositoryRoot();
        string projectSource = File.ReadAllText(Path.Combine(root, "MvcVisionSystem.csproj"));
        string solutionSource = File.ReadAllText(Path.Combine(root, "MvcVisionSystem.sln"));
        string programSource = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string commonSource = File.ReadAllText(Path.Combine(root, "2. Common", "CCommon.cs"));
        string dataSource = File.ReadAllText(Path.Combine(root, "1. Core", "CData.cs"));
        string recipeSource = File.ReadAllText(Path.Combine(root, "1. Core", "CRecipe.cs"));
        string captureSource = File.ReadAllText(Path.Combine(root, "2. Common", "ScreenCaptureService.cs"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        string viewerSource = File.ReadAllText(Path.Combine(root, "Library", "CViewer.cs"));
        string viewerRenderingSource = File.ReadAllText(Path.Combine(root, "Library", "CViewer.Rendering.cs"));
        string viewerHostAdapterSource = File.ReadAllText(Path.Combine(root, "Library", "CViewerWinFormsHostAdapter.cs"));
        string rectangleObjectSource = File.ReadAllText(Path.Combine(root, "Library", "DrawObject", "CRectangleObject.cs"));
        string roiCanvasViewModelSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "ViewModel", "RoiImageCanvasViewModel.cs"));
        string roiCanvasInputAdapterSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Canvas", "RoiImageCanvasInputAdapter.cs"));
        string imageCanvasControlSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Engine", "ImageCanvasControl.cs"));
        string imageCanvasHostAdapterSource = File.ReadAllText(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Engine", "ImageCanvasOpenGlHostAdapter.cs"));

        string[] removedSupportNames =
        {
            "RJControls",
            "RJCodeUI_M1",
            "OpenVisionLab.MessageBox",
            "OpenVisionLab.Controls.Init",
            "FontAwesome.Sharp",
            "VisionMessageBox"
        };

        foreach (string removedName in removedSupportNames)
        {
            AssertTrue(!projectSource.Contains(removedName, StringComparison.Ordinal), $"app project should not reference {removedName}");
            AssertTrue(!solutionSource.Contains(removedName, StringComparison.Ordinal), $"solution should not reference {removedName}");
            AssertTrue(!programSource.Contains(removedName, StringComparison.Ordinal), $"program startup should not reference {removedName}");
        }

        AssertTrue(!File.Exists(Path.Combine(root, "1. Core", "FormScreenPlacement.cs")), "legacy WinForms screen placement helper should be removed");
        AssertTrue(!commonSource.Contains("System.Windows.Forms", StringComparison.Ordinal), "common message boxes should not use WinForms");
        AssertTrue(!commonSource.Contains("OpenVisionLab.MessageDialogs", StringComparison.Ordinal), "common message boxes should not use the legacy message dialog library");
        AssertTrue(commonSource.Contains("WpfMessageBox.Show", StringComparison.Ordinal), "common message boxes should route through WPF MessageBox");
        AssertTrue(!programSource.Contains("SettingsManager.LoadApperanceSettings", StringComparison.Ordinal), "WPF startup should not load RJ WinForms appearance settings");
        AssertTrue(!dataSource.Contains("Application.StartupPath", StringComparison.Ordinal), "CData paths should not use WinForms Application.StartupPath");
        AssertTrue(!recipeSource.Contains("Application.StartupPath", StringComparison.Ordinal), "CRecipe paths should not use WinForms Application.StartupPath");
        AssertTrue(!shellSource.Contains("System.Windows.Forms.Application.StartupPath", StringComparison.Ordinal), "WPF shell paths should not use WinForms Application.StartupPath");
        AssertTrue(!captureSource.Contains("System.Windows.Forms", StringComparison.Ordinal), "screen capture service should not use WinForms Screen");
        AssertTrue(!File.Exists(Path.Combine(root, "Library", "CViewer.Designer.cs")), "CViewer should not keep a WinForms designer file");
        AssertTrue(!File.Exists(Path.Combine(root, "Library", "CViewer.resx")), "CViewer should not keep a WinForms component resource file");
        AssertTrue(!File.Exists(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Engine", "ImageCanvasControl.designer.cs")), "ImageCanvasControl should not keep a WinForms designer file");
        AssertTrue(!File.Exists(Path.Combine(root, "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Engine", "ImageCanvasControl.resx")), "ImageCanvasControl should not keep a WinForms component resource file");
        AssertTrue(!File.Exists(Path.Combine(root, "0. UI", "9) WPF", "Interop", "WpfRoiCanvasHost.cs")), "WPF shell should not keep the reverse WinForms ROI canvas bridge");
        AssertTrue(!File.Exists(Path.Combine(root, "2. Common", "UIThreadInvokeClass.cs")), "unused WinForms UI-thread invoke helper should be removed");
        AssertTrue(!projectSource.Contains("CViewer.Designer.cs", StringComparison.Ordinal), "app project should not wire CViewer to a WinForms designer file");
        AssertTrue(!viewerSource.Contains("InitializeComponent", StringComparison.Ordinal), "CViewer should not use WinForms designer initialization");
        AssertTrue(!imageCanvasControlSource.Contains("InitializeComponent", StringComparison.Ordinal), "ImageCanvasControl should not use WinForms designer initialization");
        AssertTrue(!viewerSource.Contains("timer1", StringComparison.Ordinal), "CViewer should not use a WinForms timer to force drag mode");
        AssertTrue(!viewerSource.Contains("public ImageCanvasControl AttachTo(", StringComparison.Ordinal), "CViewer should not expose a raw WinForms host attach API");
        AssertTrue(viewerSource.Contains("AttachToWinFormsHost", StringComparison.Ordinal), "temporary OpenGL WinForms host attachment should be explicitly named");
        AssertTrue(viewerHostAdapterSource.Contains("System.Windows.Forms", StringComparison.Ordinal), "remaining WinForms host boundary should live in the adapter");
        AssertTrue(viewerHostAdapterSource.Contains("AttachToWinFormsHost", StringComparison.Ordinal), "adapter should own the temporary WinForms host attachment");
        AssertTrue(!roiCanvasViewModelSource.Contains("System.Windows.Forms.MouseButtons", StringComparison.Ordinal), "ROI canvas view model should not branch on WinForms mouse buttons directly");
        AssertTrue(!roiCanvasViewModelSource.Contains("System.Windows.Forms.Keys", StringComparison.Ordinal), "ROI canvas view model should not branch on WinForms keys directly");
        AssertTrue(!roiCanvasViewModelSource.Contains("System.Windows.Forms.KeyEventArgs", StringComparison.Ordinal), "ROI canvas view model should not receive WinForms key event args directly");
        AssertTrue(roiCanvasViewModelSource.Contains("CanvasKeyboardEventArgs", StringComparison.Ordinal), "ROI canvas view model should receive neutral canvas keyboard args");
        AssertTrue(roiCanvasViewModelSource.Contains("CanvasPointerButton", StringComparison.Ordinal), "ROI canvas view model should receive neutral canvas pointer buttons");
        AssertTrue(roiCanvasInputAdapterSource.Contains("System.Windows.Forms", StringComparison.Ordinal), "remaining ROI canvas WinForms input conversion should live in the input adapter");
        AssertTrue(roiCanvasInputAdapterSource.Contains("CanvasKeyboardEventArgs.FromWinForms", StringComparison.Ordinal), "input adapter should translate WinForms key events before they reach the view model");
        AssertTrue(imageCanvasHostAdapterSource.Contains("new OpenGLControl", StringComparison.Ordinal), "OpenGL host creation should live in the host adapter");
        AssertTrue(imageCanvasHostAdapterSource.Contains("OpenGLDraw +=", StringComparison.Ordinal), "OpenGL host event wiring should live in the host adapter");
        AssertTrue(!imageCanvasControlSource.Contains("openGLControl.OpenGLDraw +=", StringComparison.Ordinal), "ImageCanvasControl should not wire the SharpGL draw event directly");
        AssertTrue(!viewerSource.Contains("RJDropdownMenu", StringComparison.Ordinal), "OpenGL viewer context menus should not use RJControls dropdowns");
        AssertTrue(!viewerSource.Contains("IconMenuItem", StringComparison.Ordinal), "OpenGL viewer context menus should not use FontAwesome WinForms menu items");
        AssertTrue(viewerSource.Contains("ContextMenuStrip", StringComparison.Ordinal), "OpenGL viewer should use a small temporary context menu while it is still hosted through WinForms");
        AssertTrue(!viewerRenderingSource.Contains("System.Windows.Forms", StringComparison.Ordinal), "OpenGL rendering code should not import WinForms when it does not need it");
        AssertTrue(!rectangleObjectSource.Contains("System.Windows.Forms", StringComparison.Ordinal), "ROI draw object should not own WinForms cursor dependencies");
        AssertTrue(!rectangleObjectSource.Contains("Cursor", StringComparison.Ordinal), "ROI draw object should expose selection geometry instead of UI cursors");
    }

    private static void TestWpfNumericEditorsDeclareInputGuards()
    {
        string yoloModelSettingsXamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfYoloModelSettingsPanel.xaml");
        XDocument yoloModelSettingsXaml = XDocument.Load(yoloModelSettingsXamlPath);
        string trainingSettingsXamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfTrainingSettingsPanel.xaml");
        XDocument trainingSettingsXaml = XDocument.Load(trainingSettingsXamlPath);

        AssertXamlTextBoxInputGuard(yoloModelSettingsXaml, "YoloConfidenceBox", "DecimalTextBox_PreviewTextInput", "0~1");
        AssertXamlTextBoxInputGuard(yoloModelSettingsXaml, "YoloTimeoutBox", "IntegerTextBox_PreviewTextInput", "1~600");
        AssertXamlTextBoxInputGuard(yoloModelSettingsXaml, "YoloInferenceImageSizeBox", "IntegerTextBox_PreviewTextInput", "64~2048");
        AssertXamlTextBoxInputGuard(yoloModelSettingsXaml, "YoloMaxCandidatesBox", "IntegerTextBox_PreviewTextInput", "1~200");
        AssertXamlTextBoxInputGuard(trainingSettingsXaml, "TrainingImageSizeBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(trainingSettingsXaml, "TrainingBatchBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(trainingSettingsXaml, "TrainingEpochBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(trainingSettingsXaml, "TrainingValidationPercentBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(trainingSettingsXaml, "TrainingTestPercentBox", "IntegerTextBox_PreviewTextInput", "integer");
        AssertXamlTextBoxInputGuard(trainingSettingsXaml, "TrainingSplitSeedBox", "IntegerTextBox_PreviewTextInput", "정수");
    }

    private static void TestWpfYoloModelSettingsPanelDeclaresPathEditors()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfYoloModelSettingsPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlButtonClick(xaml, xName, "BrowseYoloPythonButton", "BrowseYoloPythonButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "BrowseYoloProjectRootButton", "BrowseYoloProjectRootButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "BrowseYoloClientScriptButton", "BrowseYoloClientScriptButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "BrowseYoloWeightsButton", "BrowseYoloWeightsButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "BrowseYoloImageRootButton", "BrowseYoloImageRootButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "SaveYoloSettingsButton", "SaveYoloSettingsButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "ResetYoloSettingsButton", "ResetYoloSettingsButton_Click");
        AssertXamlTextBoxInputGuard(xaml, "YoloConfidenceBox", "DecimalTextBox_PreviewTextInput", "0~1");
        AssertXamlTextBoxInputGuard(xaml, "YoloTimeoutBox", "IntegerTextBox_PreviewTextInput", "1~600");
        AssertXamlTextBoxInputGuard(xaml, "YoloInferenceImageSizeBox", "IntegerTextBox_PreviewTextInput", "64~2048");
        AssertXamlTextBoxInputGuard(xaml, "YoloMaxCandidatesBox", "IntegerTextBox_PreviewTextInput", "1~200");
        AssertNamedXamlBinding(xaml, xName, "YoloPythonPathBox", "Text", "PythonExecutablePath");
        AssertNamedXamlBinding(xaml, xName, "YoloProjectRootBox", "Text", "ProjectRootPath");
        AssertNamedXamlBinding(xaml, xName, "YoloClientScriptBox", "Text", "ClientScriptPath");
        AssertNamedXamlBinding(xaml, xName, "YoloWeightsPathBox", "Text", "WeightsPath");
        AssertNamedXamlBinding(xaml, xName, "YoloImageRootBox", "Text", "ImageRootPath");
        AssertNamedXamlBinding(xaml, xName, "YoloConfidenceBox", "Text", "MinimumConfidenceText");
        AssertNamedXamlBinding(xaml, xName, "YoloInferenceImageSizeBox", "Text", "InferenceImageSizeText");
        AssertNamedXamlBinding(xaml, xName, "YoloMaxCandidatesBox", "Text", "MaximumCandidatesText");
        AssertNamedXamlBinding(xaml, xName, "YoloTimeoutBox", "Text", "TimeoutSecondsText");
        AssertNamedXamlBinding(xaml, xName, "YoloAutoStartCheckBox", "IsChecked", "AutoStartClient");
        AssertNamedXamlBinding(xaml, xName, "BrowseYoloPythonButton", "IsEnabled", "IsBrowsePythonEnabled");
        AssertNamedXamlBinding(xaml, xName, "BrowseYoloProjectRootButton", "IsEnabled", "IsBrowseProjectRootEnabled");
        AssertNamedXamlBinding(xaml, xName, "BrowseYoloClientScriptButton", "IsEnabled", "IsBrowseClientScriptEnabled");
        AssertNamedXamlBinding(xaml, xName, "BrowseYoloWeightsButton", "IsEnabled", "IsBrowseWeightsEnabled");
        AssertNamedXamlBinding(xaml, xName, "BrowseYoloImageRootButton", "IsEnabled", "IsBrowseImageRootEnabled");
        AssertNamedXamlBinding(xaml, xName, "SaveYoloSettingsButton", "IsEnabled", "IsSaveSettingsEnabled");
        AssertNamedXamlBinding(xaml, xName, "ResetYoloSettingsButton", "IsEnabled", "IsResetSettingsEnabled");

        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("YoloModelSettingsViewModel.ApplyWorkflowCommandState", StringComparison.Ordinal), "WPF shell should push YOLO model settings command availability through the ViewModel");
        AssertTrue(!shellSource.Contains("SaveYoloSettingsButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the YOLO model settings save button on the normal path");
        AssertTrue(!shellSource.Contains("ResetYoloSettingsButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the YOLO model settings reset button on the normal path");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertTrue(window.FindName("YoloModelSettingsPanelControl") is WpfYoloModelSettingsPanel, "WPF YOLO model settings user control was not registered");
            AssertTrue(((WpfYoloModelSettingsPanel)window.FindName("YoloModelSettingsPanelControl")).ViewModel != null, "WPF YOLO model settings view model was not created");
            AssertTrue(window.FindName("YoloProjectRootBox") is System.Windows.Controls.TextBox, "WPF YOLO project path proxy was not registered");
            AssertTrue(window.FindName("YoloInferenceImageSizeBox") is System.Windows.Controls.TextBox, "WPF YOLO inference image-size proxy was not registered");
            AssertTrue(window.FindName("YoloMaxCandidatesBox") is System.Windows.Controls.TextBox, "WPF YOLO max-candidates proxy was not registered");
            AssertTrue(window.FindName("YoloAutoStartCheckBox") is System.Windows.Controls.CheckBox, "WPF YOLO auto-start proxy was not registered");
            AssertTrue(window.FindName("SaveYoloSettingsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO settings save button should use WPF-UI button");
            AssertTrue(window.FindName("ResetYoloSettingsButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF YOLO settings reset button should use WPF-UI button");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfTrainingSettingsPanelDeclaresControls()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfTrainingSettingsPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlButtonClick(xaml, xName, "RefreshTrainingReadinessButton", "RefreshTrainingReadinessButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "StartTrainingButton", "StartTrainingButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "StopTrainingButton", "StopTrainingButton_Click");
        AssertNamedXamlBinding(xaml, xName, "RefreshTrainingReadinessButton", "IsEnabled", "IsRefreshReadinessEnabled");
        AssertNamedXamlBinding(xaml, xName, "StartTrainingButton", "IsEnabled", "IsStartTrainingEnabled");
        AssertNamedXamlBinding(xaml, xName, "StopTrainingButton", "IsEnabled", "IsStopTrainingEnabled");
        AssertXamlTextBoxInputGuard(xaml, "TrainingImageSizeBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(xaml, "TrainingBatchBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(xaml, "TrainingEpochBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(xaml, "TrainingValidationPercentBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertXamlTextBoxInputGuard(xaml, "TrainingTestPercentBox", "IntegerTextBox_PreviewTextInput", "integer");
        AssertXamlTextBoxInputGuard(xaml, "TrainingSplitSeedBox", "IntegerTextBox_PreviewTextInput", "정수");
        AssertNamedXamlBinding(xaml, xName, "TrainingImageSizeBox", "Text", "ImageSizeText");
        AssertNamedXamlBinding(xaml, xName, "TrainingBatchBox", "Text", "BatchText");
        AssertNamedXamlBinding(xaml, xName, "TrainingEpochBox", "Text", "EpochText");
        AssertNamedXamlBinding(xaml, xName, "TrainingCfgBox", "SelectedItem", "Cfg");
        AssertNamedXamlBinding(xaml, xName, "TrainingWeightBox", "SelectedItem", "Weight");
        AssertNamedXamlBinding(xaml, xName, "TrainingValidationPercentBox", "Text", "ValidationPercentText");
        AssertNamedXamlBinding(xaml, xName, "TrainingTestPercentBox", "Text", "TestPercentText");
        AssertNamedXamlBinding(xaml, xName, "TrainingSplitSeedBox", "Text", "SplitSeedText");
        AssertNamedXamlBinding(xaml, xName, "TrainingSplitPolicyHintText", "Text", "SplitPolicyHintText");
        AssertNamedXamlBinding(xaml, xName, "TrainingReadinessText", "Text", "TrainingReadinessText");
        AssertNamedXamlBinding(xaml, xName, "TrainingReadinessText", "Foreground", "TrainingReadinessForeground");
        AssertNamedXamlBinding(xaml, xName, "TrainingProgressBar", "Value", "TrainingProgressValue");
        AssertNamedXamlBinding(xaml, xName, "TrainingProgressBar", "IsIndeterminate", "TrainingProgressIsIndeterminate");
        AssertNamedXamlBinding(xaml, xName, "TrainingProgressBar", "Foreground", "TrainingProgressForeground");
        AssertNamedXamlBinding(xaml, xName, "TrainingProgressText", "Text", "TrainingProgressText");
        AssertNamedXamlBinding(xaml, xName, "TrainingProgressText", "Foreground", "TrainingProgressForeground");
        AssertNamedXamlBinding(xaml, xName, "TrainingEpochText", "Text", "TrainingEpochStatusText");

        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("TrainingSettingsViewModel.ApplyWorkflowCommandState", StringComparison.Ordinal), "WPF shell should push training command availability through the training settings ViewModel");
        AssertTrue(!shellSource.Contains("RefreshTrainingReadinessButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the training refresh button on the normal path");
        AssertTrue(!shellSource.Contains("StartTrainingButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the training start button on the normal path");
        AssertTrue(!shellSource.Contains("StopTrainingButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the training stop button on the normal path");

        XElement readinessText = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBlock"
                && string.Equals((string)element.Attribute(xName), "TrainingReadinessText", StringComparison.Ordinal));
        XElement progress = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ProgressBar"
                && string.Equals((string)element.Attribute(xName), "TrainingProgressBar", StringComparison.Ordinal));

        AssertTrue(readinessText != null, "WPF training readiness text was not found in training panel XAML");
        AssertTrue(progress != null, "WPF training progress bar was not found in training panel XAML");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertTrue(window.FindName("TrainingSettingsPanelControl") is WpfTrainingSettingsPanel, "WPF training settings user control was not registered");
            AssertTrue(((WpfTrainingSettingsPanel)window.FindName("TrainingSettingsPanelControl")).ViewModel != null, "WPF training settings view model was not created");
            AssertTrue(window.FindName("TrainingImageSizeBox") is System.Windows.Controls.TextBox, "WPF training image-size proxy was not registered");
            AssertTrue(window.FindName("TrainingTestPercentBox") is System.Windows.Controls.TextBox, "WPF training test-percent proxy was not registered");
            AssertTrue(window.FindName("TrainingSplitPolicyHintText") is System.Windows.Controls.TextBlock, "WPF training split policy hint proxy was not registered");
            AssertTrue(window.FindName("TrainingCfgBox") is System.Windows.Controls.ComboBox, "WPF training cfg proxy was not registered");
            AssertTrue(window.FindName("TrainingProgressBar") is System.Windows.Controls.ProgressBar, "WPF training progress proxy was not registered");
            AssertTrue(window.FindName("StartTrainingButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF training start button should use WPF-UI button");
            AssertTrue(window.FindName("StopTrainingButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF training stop button should use WPF-UI button");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfStatusAndLogPanelsDeclareControls()
    {
        string statusXamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfStatusBarPanel.xaml");
        XDocument statusXaml = XDocument.Load(statusXamlPath);
        string shellXamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml");
        XDocument shellXaml = XDocument.Load(shellXamlPath);
        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        string logXamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfShellLogPanel.xaml");
        XDocument logXaml = XDocument.Load(logXamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlElement(statusXaml, xName, "TextBlock", "DatasetStatusText");
        AssertNamedXamlElement(statusXaml, xName, "TextBlock", "PythonStatusText");
        AssertNamedXamlElement(statusXaml, xName, "TextBlock", "AnnotationSaveStatusText");
        AssertNamedXamlElement(statusXaml, xName, "TextBlock", "ModelStatusText");
        AssertNamedXamlBinding(statusXaml, xName, "DatasetStatusText", "Text", "DatasetStatusText");
        AssertNamedXamlBinding(statusXaml, xName, "PythonStatusText", "Text", "PythonStatusText");
        AssertNamedXamlBinding(statusXaml, xName, "ModelStatusText", "Text", "ModelStatusText");
        string statusSource = File.ReadAllText(statusXamlPath);
        AssertTrue(statusSource.Contains("IsAnnotationDirty", StringComparison.Ordinal), "WPF status bar should switch annotation save visuals from dirty state");
        AssertTrue(statusSource.Contains("AnnotationSaveStatusText", StringComparison.Ordinal), "WPF status bar should bind annotation save text");
        AssertTrue(statusSource.Contains("AnnotationSaveStatusToolTip", StringComparison.Ordinal), "WPF status bar should explain annotation save status");
        AssertTrue(shellSource.Contains("StatusBarViewModel.SetDatasetStatus", StringComparison.Ordinal), "WPF shell dataset status should update the status ViewModel");
        AssertTrue(shellSource.Contains("StatusBarViewModel.SetPythonStatus", StringComparison.Ordinal), "WPF shell python status should update the status ViewModel");
        AssertTrue(shellSource.Contains("StatusBarViewModel.SetModelStatus", StringComparison.Ordinal), "WPF shell model status should update the status ViewModel");
        AssertNamedXamlElement(shellXaml, xName, "Border", "InferenceStatusBorder");
        AssertNamedXamlElement(shellXaml, xName, "TextBlock", "InferenceStatusText");
        AssertNamedXamlElement(shellXaml, xName, "ProgressBar", "InferenceStatusProgressBar");
        AssertTrue(shellSource.Contains("SetGlobalInferenceStatus", StringComparison.Ordinal), "WPF shell should expose inference progress outside the YOLO settings tab");
        AssertTrue(shellSource.Contains("StartInferenceStatusPulse", StringComparison.Ordinal), "WPF global inference status should use a stable progress pulse");
        AssertTrue(shellSource.Contains("InferenceStatusPulseTimer_Tick", StringComparison.Ordinal), "WPF global inference status pulse should be driven by a render-priority timer");
        AssertNamedXamlElement(logXaml, xName, "LogPanelView", "ShellLogPanel");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertTrue(window.FindName("StatusBarPanelControl") is WpfStatusBarPanel, "WPF status bar user control was not registered");
            AssertTrue(((WpfStatusBarPanel)window.FindName("StatusBarPanelControl")).ViewModel != null, "WPF status bar view model was not created");
            AssertTrue(window.FindName("DatasetStatusText") is System.Windows.Controls.TextBlock, "WPF dataset status proxy was not registered");
            AssertTrue(window.FindName("PythonStatusText") is System.Windows.Controls.TextBlock, "WPF python status proxy was not registered");
            AssertTrue(window.FindName("AnnotationSaveStatusText") is System.Windows.Controls.TextBlock, "WPF annotation save status proxy was not registered");
            AssertTrue(window.FindName("ModelStatusText") is System.Windows.Controls.TextBlock, "WPF model status proxy was not registered");
            var statusPanel = (WpfStatusBarPanel)window.FindName("StatusBarPanelControl");
            var datasetStatusText = (System.Windows.Controls.TextBlock)window.FindName("DatasetStatusText");
            var pythonStatusText = (System.Windows.Controls.TextBlock)window.FindName("PythonStatusText");
            var modelStatusText = (System.Windows.Controls.TextBlock)window.FindName("ModelStatusText");
            InvokePrivateResult<object>(window, "SetDatasetStatus", "Dataset VM check");
            InvokePrivateResult<object>(window, "SetPythonStatus", "Python VM check");
            InvokePrivateResult<object>(window, "SetModelStatus", "Model VM check");
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertEqual("Dataset VM check", statusPanel.ViewModel.DatasetStatusText);
            AssertEqual("Python VM check", statusPanel.ViewModel.PythonStatusText);
            AssertEqual("Model VM check", statusPanel.ViewModel.ModelStatusText);
            AssertEqual("Dataset VM check", datasetStatusText.Text);
            AssertEqual("Python VM check", pythonStatusText.Text);
            AssertEqual("Model VM check", modelStatusText.Text);
            AssertTrue(window.FindName("InferenceStatusBorder") is System.Windows.Controls.Border, "WPF global inference status border was not registered");
            AssertTrue(window.FindName("InferenceStatusText") is System.Windows.Controls.TextBlock, "WPF global inference status text was not registered");
            AssertTrue(window.FindName("InferenceStatusProgressBar") is System.Windows.Controls.ProgressBar, "WPF global inference status progress bar was not registered");
            var inferenceStatusText = (System.Windows.Controls.TextBlock)window.FindName("InferenceStatusText");
            var inferenceStatusProgress = (System.Windows.Controls.ProgressBar)window.FindName("InferenceStatusProgressBar");
            InvokePrivateResult<object>(window, "SetGlobalInferenceStatus", "AI 추론 중", true, false);
            AssertEqual("AI 추론 중", inferenceStatusText.Text);
            AssertEqual(System.Windows.Visibility.Visible, inferenceStatusProgress.Visibility);
            AssertEqual(false, inferenceStatusProgress.IsIndeterminate);
            AssertTrue(inferenceStatusProgress.Value >= 8D && inferenceStatusProgress.Value <= 100D, "WPF global inference progress should start without indeterminate animation stutter");
            InvokePrivateResult<object>(window, "SetGlobalInferenceStatus", "완료", false, false);
            AssertEqual("완료", inferenceStatusText.Text);
            AssertEqual(System.Windows.Visibility.Collapsed, inferenceStatusProgress.Visibility);
            AssertEqual(0D, inferenceStatusProgress.Value);
            AssertTrue(window.FindName("ShellLogPanelControl") is WpfShellLogPanel, "WPF shell log user control was not registered");
            AssertTrue(((WpfShellLogPanel)window.FindName("ShellLogPanelControl")).ViewModel != null, "WPF shell log view model was not created");
            AssertTrue(window.FindName("ShellLogPanel").GetType().FullName == "OpenVisionLab.Logging.Controls.View.LogPanelView", "WPF log proxy should use the OpenVisionLab logging WPF panel");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfYoloStatusPanelDeclaresCommandControls()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfYoloStatusPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlButtonClick(xaml, xName, "FirstCheckYoloButton", "CheckYoloButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "InstallRequirementsButton", "InstallRequirementsButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "RunYoloSmokeButton", "RunYoloSmokeButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "RestartPythonWorkerButton", "RestartPythonWorkerButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "StopPythonWorkerButton", "StopPythonWorkerButton_Click");

        XElement statusText = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBlock"
                && string.Equals((string)element.Attribute(xName), "YoloCommandStatusText", StringComparison.Ordinal));
        XElement progress = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ProgressBar"
                && string.Equals((string)element.Attribute(xName), "YoloCommandProgressBar", StringComparison.Ordinal));

        AssertTrue(statusText != null, "WPF YOLO command status text was not found in status panel XAML");
        AssertTrue(progress != null, "WPF YOLO command progress bar was not found in status panel XAML");
        AssertNamedXamlBinding(xaml, xName, "YoloSettingsSummaryText", "Text", "SummaryText");
        AssertNamedXamlBinding(xaml, xName, "YoloSettingsDetailText", "Text", "DetailText");
        AssertNamedXamlBinding(xaml, xName, "YoloCommandStatusText", "Text", "CommandStatusText");
        AssertNamedXamlBinding(xaml, xName, "YoloCommandProgressBar", "Value", "CommandProgressValue");
        AssertNamedXamlBinding(xaml, xName, "YoloCommandProgressBar", "Visibility", "CommandProgressVisibility");
        AssertNamedXamlBinding(xaml, xName, "YoloCommandProgressBar", "IsIndeterminate", "CommandProgressIsIndeterminate");
        AssertNamedXamlBinding(xaml, xName, "FirstCheckYoloButton", "IsEnabled", "IsFirstCheckEnabled");
        AssertNamedXamlBinding(xaml, xName, "InstallRequirementsButton", "IsEnabled", "IsInstallRequirementsEnabled");
        AssertNamedXamlBinding(xaml, xName, "RunYoloSmokeButton", "IsEnabled", "IsRunSmokeEnabled");
        AssertNamedXamlBinding(xaml, xName, "RestartPythonWorkerButton", "IsEnabled", "IsRestartWorkerEnabled");
        AssertNamedXamlBinding(xaml, xName, "StopPythonWorkerButton", "IsEnabled", "IsStopWorkerEnabled");

        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("YoloStatusViewModel.ApplyWorkflowCommandState", StringComparison.Ordinal), "WPF shell should push YOLO status command availability through the ViewModel");
        AssertTrue(!shellSource.Contains("FirstCheckYoloButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the YOLO first-check button on the normal path");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertTrue(window.FindName("YoloStatusPanelControl") is WpfYoloStatusPanel, "WPF YOLO status user control was not registered");
            AssertTrue(((WpfYoloStatusPanel)window.FindName("YoloStatusPanelControl")).ViewModel != null, "WPF YOLO status view model was not created");
            AssertTrue(window.FindName("YoloSettingsSummaryText") is System.Windows.Controls.TextBlock, "WPF YOLO summary proxy was not registered");
            AssertTrue(window.FindName("YoloCommandProgressBar") is System.Windows.Controls.ProgressBar, "WPF YOLO progress proxy was not registered");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfProjectConfigPanelDeclaresRecipeControls()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfProjectConfigPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlElement(xaml, xName, "Expander", "ProjectConfigExpander");
        AssertNamedXamlElement(xaml, xName, "TextBox", "ProjectRecipeNameBox");
        AssertNamedXamlElement(xaml, xName, "ComboBox", "ProjectRecipeListBox");
        AssertNamedXamlElement(xaml, xName, "TextBox", "ProjectConfigPathBox");
        AssertNamedXamlElement(xaml, xName, "TextBlock", "ProjectConfigStatusText");
        AssertNamedXamlButtonClick(xaml, xName, "ApplyProjectRecipeButton", "ApplyProjectRecipeButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "RefreshProjectRecipeListButton", "RefreshProjectRecipeListButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "SaveProjectConfigButton", "SaveProjectConfigButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "OpenProjectConfigFolderButton", "OpenProjectConfigFolderButton_Click");
        AssertNamedXamlBinding(xaml, xName, "ApplyProjectRecipeButton", "IsEnabled", "IsApplyRecipeEnabled");
        AssertNamedXamlBinding(xaml, xName, "RefreshProjectRecipeListButton", "IsEnabled", "IsRefreshRecipeListEnabled");
        AssertNamedXamlBinding(xaml, xName, "SaveProjectConfigButton", "IsEnabled", "IsSaveProjectConfigEnabled");
        AssertNamedXamlBinding(xaml, xName, "OpenProjectConfigFolderButton", "IsEnabled", "IsOpenProjectConfigFolderEnabled");

        string projectCommandSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(projectCommandSource.Contains("ProjectConfigViewModel.ApplyWorkflowCommandState", StringComparison.Ordinal), "WPF shell should push project command availability through the project config ViewModel");
        AssertTrue(!projectCommandSource.Contains("ApplyProjectRecipeButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the project apply button on the normal path");
        AssertTrue(!projectCommandSource.Contains("SaveProjectConfigButton.IsEnabled", StringComparison.Ordinal), "WPF shell should not directly enable the project save button on the normal path");
        XElement expander = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Expander"
                && string.Equals((string)element.Attribute(xName), "ProjectConfigExpander", StringComparison.Ordinal));
        AssertEqual("True", (string)expander.Attribute("IsExpanded"));
        XElement recipeBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBox"
                && string.Equals((string)element.Attribute(xName), "ProjectRecipeNameBox", StringComparison.Ordinal));
        AssertEqual("False", (string)recipeBox.Attribute("IsReadOnly"));
        AssertTrue(((string)recipeBox.Attribute("Text") ?? string.Empty).Contains("Binding RecipeName", StringComparison.Ordinal), "project recipe name should bind to the view model");
        XElement recipeListBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ComboBox"
                && string.Equals((string)element.Attribute(xName), "ProjectRecipeListBox", StringComparison.Ordinal));
        AssertEqual("ProjectRecipeListBox_SelectionChanged", (string)recipeListBox.Attribute("SelectionChanged"));
        AssertTrue(((string)recipeListBox.Attribute("Style") ?? string.Empty).Contains("ProjectConfigComboBoxStyle", StringComparison.Ordinal), "project recipe list should use the dark project ComboBox style");
        AssertTrue(((string)recipeListBox.Attribute("ItemsSource") ?? string.Empty).Contains("Binding RecipeNames", StringComparison.Ordinal), "project recipe list should bind to the view model");
        AssertTrue(((string)recipeListBox.Attribute("SelectedItem") ?? string.Empty).Contains("Binding SelectedRecipeName", StringComparison.Ordinal), "project selected recipe should bind to the view model");
        AssertTrue(xaml.ToString().Contains("SystemColors.WindowBrushKey", StringComparison.Ordinal), "project recipe ComboBox should override the default light WPF chrome");
        XElement configPathBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBox"
                && string.Equals((string)element.Attribute(xName), "ProjectConfigPathBox", StringComparison.Ordinal));
        AssertTrue(((string)configPathBox.Attribute("Text") ?? string.Empty).Contains("Binding ConfigPath", StringComparison.Ordinal), "project config path should bind to the view model");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertTrue(window.FindName("ProjectConfigPanelControl") is WpfProjectConfigPanel, "WPF project config user control was not registered");
            AssertTrue(((WpfProjectConfigPanel)window.FindName("ProjectConfigPanelControl")).ViewModel != null, "WPF project config view model was not created");
            AssertTrue(window.FindName("ProjectRecipeNameBox") is System.Windows.Controls.TextBox, "WPF project recipe proxy was not registered");
            AssertTrue(window.FindName("ProjectRecipeListBox") is System.Windows.Controls.ComboBox, "WPF project recipe list proxy was not registered");
            AssertTrue(window.FindName("ProjectConfigPathBox") is System.Windows.Controls.TextBox, "WPF project config path proxy was not registered");
            AssertTrue(window.FindName("ProjectConfigStatusText") is System.Windows.Controls.TextBlock, "WPF project config status proxy was not registered");
            AssertTrue(window.FindName("ApplyProjectRecipeButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF project recipe apply button should use WPF-UI button");
            AssertTrue(window.FindName("RefreshProjectRecipeListButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF project recipe refresh button should use WPF-UI button");
            AssertTrue(window.FindName("SaveProjectConfigButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF project config save button should use WPF-UI button");
            AssertTrue(window.FindName("OpenProjectConfigFolderButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF project config folder button should use WPF-UI button");
            AssertTrue(InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "GetRecipeRootDirectory").EndsWith("RECIPE", StringComparison.OrdinalIgnoreCase), "WPF project config should expose the recipe root path");

            string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
            string viewModelSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "ViewModels", "WpfProjectConfigPanelViewModel.cs"));
            string serviceSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Services", "WpfProjectRecipeService.cs"));
            AssertTrue(shellSource.Contains("PopulateProjectRecipeList", StringComparison.Ordinal), "WPF project config should list existing recipe folders");
            AssertTrue(!shellSource.Contains("ProjectRecipeNameBox.Text =", StringComparison.Ordinal), "WPF project config should not push recipe text directly into the TextBox");
            AssertTrue(!shellSource.Contains("ProjectRecipeListBox.Items", StringComparison.Ordinal), "WPF project config should not mutate ComboBox items directly");
            AssertTrue(viewModelSource.Contains("ObservableCollection<string> RecipeNames", StringComparison.Ordinal), "WPF project config view model should own recipe list state");
            AssertTrue(viewModelSource.Contains("SelectRecipeFromList", StringComparison.Ordinal), "WPF project config view model should own recipe selection preview");
            AssertTrue(serviceSource.Contains("ListRecipeNames", StringComparison.Ordinal), "WPF project config recipe listing should live in a service");
            AssertTrue(shellSource.Contains("suppressProjectRecipeSelection", StringComparison.Ordinal), "WPF project config should not auto-apply during recipe list refresh");
            AssertTrue(viewModelSource.Contains("적용을 누르세요", StringComparison.Ordinal), "WPF project recipe list selection should guide the operator to apply explicitly");
            AssertTrue(shellSource.Contains("WpfProjectRecipeService.IsValidRecipeName", StringComparison.Ordinal), "WPF project config should validate recipe file-system characters through the service");
            AssertTrue(shellSource.Contains("global.Recipe.Name = recipeName", StringComparison.Ordinal), "WPF project config should apply the recipe through CRecipe");
            AssertTrue(shellSource.Contains("Recipe 적용 후 설정 저장이 필요합니다.", StringComparison.Ordinal), "WPF project config should not claim XML save completion when no recipe exists");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertNamedXamlButtonClick(XDocument xaml, XName xName, string controlName, string expectedHandler)
    {
        XElement button = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Button"
                && string.Equals((string)element.Attribute(xName), controlName, StringComparison.Ordinal));

        AssertTrue(button != null, $"WPF button was not found: {controlName}");
        AssertEqual(expectedHandler, (string)button.Attribute("Click"));
    }

    private static void AssertNamedXamlElement(XDocument xaml, XName xName, string localName, string controlName)
    {
        XElement element = xaml.Descendants()
            .FirstOrDefault(candidate => candidate.Name.LocalName == localName
                && string.Equals((string)candidate.Attribute(xName), controlName, StringComparison.Ordinal));

        AssertTrue(element != null, $"WPF {localName} was not found: {controlName}");
    }

    private static void TestWpfCanvasDetectionOverlayUsesThemeResources()
    {
        string root = FindRepositoryRoot();
        string canvasXamlPath = Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfCanvasPanel.xaml");
        XDocument canvasXaml = XDocument.Load(canvasXamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        XElement FindElement(string localName, string controlName)
        {
            return canvasXaml.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal)
                    && string.Equals((string)element.Attribute(xName), controlName, StringComparison.Ordinal));
        }

        XElement overlay = FindElement("Border", "DetectionResultOverlay");
        XElement selectedBorder = FindElement("Border", "DetectionOverlaySelectedBorder");
        XElement titleText = FindElement("TextBlock", "DetectionOverlayTitleText");
        XElement summaryText = FindElement("TextBlock", "DetectionOverlaySummaryText");
        XElement selectedText = FindElement("TextBlock", "DetectionOverlaySelectedText");
        XElement detailText = FindElement("TextBlock", "DetectionOverlayDetailText");

        AssertNamedXamlBinding(canvasXaml, xName, "DetectionResultOverlay", "Visibility", "DetectionOverlayVisibility");
        AssertNamedXamlBinding(canvasXaml, xName, "DetectionOverlaySummaryText", "Text", "DetectionOverlaySummaryText");
        AssertNamedXamlBinding(canvasXaml, xName, "DetectionOverlaySelectedText", "Text", "DetectionOverlaySelectedText");
        AssertNamedXamlBinding(canvasXaml, xName, "DetectionOverlayDetailText", "Text", "DetectionOverlayDetailText");
        AssertEqual("{StaticResource DetectionOverlayPanelStyle}", (string)overlay.Attribute("Style"));
        AssertEqual("{StaticResource DetectionOverlaySelectedBorderStyle}", (string)selectedBorder.Attribute("Style"));
        AssertEqual("{DynamicResource DetectionOverlayTitleTextBrush}", (string)titleText.Attribute("Foreground"));
        AssertEqual("{StaticResource DetectionOverlaySummaryTextStyle}", (string)summaryText.Attribute("Style"));
        AssertEqual("{DynamicResource DetectionOverlaySelectedTextBrush}", (string)selectedText.Attribute("Foreground"));
        AssertEqual("{DynamicResource DetectionOverlayDetailTextBrush}", (string)detailText.Attribute("Foreground"));
        string canvasSource = File.ReadAllText(canvasXamlPath);
        AssertTrue(canvasSource.Contains("DetectionOverlayStatusKey", StringComparison.Ordinal), "WPF detection result overlay should style itself from the canvas ViewModel state");
        AssertTrue(canvasSource.Contains("DetectionOverlayDuplicateAccentBrush", StringComparison.Ordinal), "WPF detection result overlay should expose duplicate candidate styling");
        AssertTrue(canvasSource.Contains("DetectionOverlayReviewAccentBrush", StringComparison.Ordinal), "WPF detection result overlay should expose review-needed candidate styling");

        string shellXaml = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("CanvasPanelControl.ViewModel.SetDetectionOverlay", StringComparison.Ordinal), "WPF shell should push detection overlay presentation through the canvas ViewModel");
        AssertTrue(shellSource.Contains("CanvasPanelControl.ViewModel.ClearDetectionOverlay", StringComparison.Ordinal), "WPF shell should clear detection overlay presentation through the canvas ViewModel");
        AssertTrue(!shellSource.Contains("DetectionResultOverlay.Visibility =", StringComparison.Ordinal), "WPF shell should not directly show or hide the detection result overlay");
        AssertTrue(!shellSource.Contains("DetectionOverlaySummaryText.Text =", StringComparison.Ordinal), "WPF shell should not directly write detection overlay summary text");
        AssertTrue(!shellSource.Contains("DetectionOverlaySelectedText.Text =", StringComparison.Ordinal), "WPF shell should not directly write detection overlay selected text");
        AssertTrue(!shellSource.Contains("DetectionOverlayDetailText.Text =", StringComparison.Ordinal), "WPF shell should not directly write detection overlay detail text");
        AssertTrue(!shellSource.Contains("ApplyDetectionOverlayStatusStyle", StringComparison.Ordinal), "WPF shell should not directly paint detection overlay status");
        foreach (string key in new[]
        {
            "DetectionOverlayBackgroundBrush",
            "DetectionOverlayBorderBrush",
            "DetectionOverlayTitleTextBrush",
            "DetectionOverlaySummaryTextBrush",
            "DetectionOverlaySelectedBackgroundBrush",
            "DetectionOverlaySelectedTextBrush",
            "DetectionOverlayDetailTextBrush"
        })
        {
            AssertTrue(shellXaml.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal), $"WPF shell should declare theme resource: {key}");
            AssertTrue(shellSource.Contains($"SetThemeBrush(\"{key}\"", StringComparison.Ordinal), $"WPF shell should update theme resource: {key}");
        }
    }

    private static void TestWpfTrainingStatusSummaries()
    {
        var running = new PythonCommunicationStatus
        {
            LastTrainingState = "running",
            LastTrainingMessage = "epoch",
            LastTrainingProgressPercent = 42,
            LastTrainingEpoch = 2,
            LastTrainingTotalEpochs = 5
        };

        AssertEqual("학습 진행 중 / 42% / 에폭", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildTrainingProgressSummary", running));
        AssertEqual("에폭 2/5", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildTrainingEpochSummary", running));

        var clamped = new PythonCommunicationStatus
        {
            LastTrainingState = "completed",
            LastTrainingProgressPercent = 140,
            LastTrainingEpoch = 5
        };

        AssertEqual("학습 완료 / 100%", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildTrainingProgressSummary", clamped));
        AssertEqual("에폭 5", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildTrainingEpochSummary", clamped));
        AssertEqual("학습 대기", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildTrainingProgressSummary", new PythonCommunicationStatus()));
        AssertTrue(InvokePrivateStaticResult<bool>(typeof(WpfLabelingShellWindow), "IsTrainingStopAvailable", running), "running training status should allow stop");
        AssertTrue(!InvokePrivateStaticResult<bool>(typeof(WpfLabelingShellWindow), "IsTrainingStopAvailable", clamped), "completed training status should not allow stop");
        AssertTrue(!InvokePrivateStaticResult<bool>(typeof(WpfLabelingShellWindow), "IsTrainingStopAvailable", new PythonCommunicationStatus()), "idle training status should not allow stop");
    }

    private static void TestWpfSingleDetectionManualStartupPath()
    {
        string sourcePath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs");
        string source = File.ReadAllText(sourcePath);

        AssertTrue(!source.Contains("WarmupPythonWorkerAsync", StringComparison.Ordinal), "WPF shell should not keep startup worker warm-up code");
        AssertTrue(!source.Contains("GetWorkerWarmupTimeoutMilliseconds", StringComparison.Ordinal), "WPF shell should not keep a separate startup warm-up timeout");
        AssertTrue(source.Contains("추론은 사용자가 명시적으로 실행할 때만 시작합니다.", StringComparison.Ordinal), "startup should tell the user inference is manual");
        AssertTrue(source.Contains("RunInteractiveDetectionAsync(allowSmokeFallback: false)", StringComparison.Ordinal), "current-image detection should not use smoke fallback");
        AssertTrue(source.Contains("RunInteractiveDetectionAsync(item.ImagePath, allowSmokeFallback: false)", StringComparison.Ordinal), "selected-image detection should not use smoke fallback");
        AssertTrue(source.Contains("RunInteractiveDetectionAsync(allowSmokeFallback: true)", StringComparison.Ordinal), "YOLO diagnostic test may use smoke fallback");
        AssertTrue(source.Contains("GetInteractiveWorkerConnectTimeoutMilliseconds()", StringComparison.Ordinal), "single-image detection should use the interactive worker wait helper");
        AssertTrue(source.Contains("return GetWorkerConnectTimeoutMilliseconds();", StringComparison.Ordinal), "first interactive worker connection should allow model preload to finish");
        AssertTrue(source.Contains("단일 이미지 추론 완료", StringComparison.Ordinal), "single-image detection should log elapsed time for UX diagnostics");
        AssertTrue(source.Contains("worker 연결 확인 중", StringComparison.Ordinal), "single-image detection should tell the operator when it is waiting for the worker");
        AssertTrue(source.Contains("AI 추론 요청 중", StringComparison.Ordinal), "single-image detection should tell the operator when the request is being sent");
        AssertTrue(source.Contains("추론 완료: 후보", StringComparison.Ordinal), "single-image detection should show completion with candidate count");
        AssertTrue(source.Contains("일괄 검사 완료", StringComparison.Ordinal), "batch detection should clear busy command status on completion");
        AssertTrue(source.Contains("TryStartImagePathDetection", StringComparison.Ordinal), "batch detection should use the path-based worker request");
        AssertTrue(source.Contains("ShowBatchDetectionImage(item)", StringComparison.Ordinal), "batch detection should display the image currently being inspected");
        AssertTrue(source.Contains("SelectImageQueueItem(item.ImagePath)", StringComparison.Ordinal), "batch detection should move the left queue selection with the current item");
        AssertTrue(source.Contains("ApplyBatchDetectionCandidates", StringComparison.Ordinal), "batch detection should update the active image candidate overlay without spamming the log");
        AssertTrue(source.Contains("ApplyBatchDetectionResultToCanvas(item, result)", StringComparison.Ordinal), "batch detection should display completed inspection results on the canvas, not just the image");
        AssertTrue(source.Contains("ShowBatchNoCandidateResult", StringComparison.Ordinal), "batch detection should show a no-candidate result card when YOLO finds nothing");
        AssertTrue(source.Contains("ShowBatchDetectionFailureResult", StringComparison.Ordinal), "batch detection should show a failure result card when YOLO fails");
        AssertTrue(source.Contains("YieldBatchDetectionResultFrameAsync(token)", StringComparison.Ordinal), "batch detection should let the result overlay render before moving to the next item");
        AssertTrue(source.Contains("일괄 검사 항목 완료", StringComparison.Ordinal), "batch detection should log per-image elapsed time");
        AssertTrue(source.Contains("일괄 검사 항목 실패", StringComparison.Ordinal), "batch detection should log failed item elapsed time");
        AssertTrue(source.Contains("최근 {elapsedText}", StringComparison.Ordinal), "batch detection status should show the latest item elapsed time");
        AssertTrue(source.Contains("FormatAverageElapsed", StringComparison.Ordinal), "batch detection completion should show average elapsed time");
        AssertTrue(source.Contains("BatchReviewStatusSaveInterval", StringComparison.Ordinal), "batch detection should throttle review-status disk writes");
        AssertTrue(source.Contains("saveReviewStatus: false", StringComparison.Ordinal), "batch detection should defer per-row review-status saves");
        AssertTrue(source.Contains("refreshQueueView: false", StringComparison.Ordinal), "batch detection should defer full queue refresh while rows are processing");
        AssertTrue(source.Contains("TryOpenSelectedQueueImage(skipIfAlreadyActive: true)", StringComparison.Ordinal), "queue selection should load the clicked image into the canvas");
        AssertTrue(source.Contains("refreshActiveStatus: false", StringComparison.Ordinal), "queue selection should not save review status while loading the clicked image");
        AssertTrue(source.Contains("appendLoadLog: false", StringComparison.Ordinal), "queue selection should not refresh the log panel on every image click");

        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml");
        string xaml = File.ReadAllText(xamlPath);
        AssertTrue(xaml.Contains("Text=\"현재 검사\"", StringComparison.Ordinal), "current-image detection command should read as an action, not a mode");
        AssertTrue(xaml.Contains("Kind=\"ImageSearch\"", StringComparison.Ordinal), "current-image detection command should use an inspection/search icon");

        string overlaySourcePath = Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "ViewModel", "RoiImageCanvasViewModel.cs");
        string overlaySource = File.ReadAllText(overlaySourcePath);
        AssertTrue(overlaySource.Contains("return \"#\" + parts[1] + \" \" + parts[2];", StringComparison.Ordinal), "compact overlay badges should show candidate number and class");
    }

    private static void TestWpfTrainingCommandButtonState()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            var firstCheck = (System.Windows.Controls.Control)window.FindName("FirstCheckYoloButton");
            var detect = (System.Windows.Controls.Control)window.FindName("DetectButton");
            var startTraining = (System.Windows.Controls.Control)window.FindName("StartTrainingButton");
            var stopTraining = (System.Windows.Controls.Control)window.FindName("StopTrainingButton");
            var yoloStatusPanel = (WpfYoloStatusPanel)window.FindName("YoloStatusPanelControl");
            var projectConfigPanel = (WpfProjectConfigPanel)window.FindName("ProjectConfigPanelControl");
            var yoloModelPanel = (WpfYoloModelSettingsPanel)window.FindName("YoloModelSettingsPanelControl");
            var trainingPanel = (WpfTrainingSettingsPanel)window.FindName("TrainingSettingsPanelControl");
            var imageQueuePanel = (WpfImageQueuePanel)window.FindName("ImageQueuePanelControl");

            AssertTrue(!window.ShellViewModel.IsCurrentImageDetectionEnabled, "Current-image detection should start disabled through the shell ViewModel in labeling mode");
            AssertTrue(!imageQueuePanel.ViewModel.IsDetectSelectedEnabled, "Queue selected detection should start disabled through the image queue ViewModel in labeling mode");
            AssertTrue(!imageQueuePanel.ViewModel.IsBatchDetectEnabled, "Queue batch detection should start disabled through the image queue ViewModel in labeling mode");
            AssertTrue(!imageQueuePanel.ViewModel.IsRetryFailedEnabled, "Queue retry detection should start disabled through the image queue ViewModel in labeling mode");
            AssertTrue(!imageQueuePanel.ViewModel.IsStopBatchEnabled, "Queue stop should start disabled through the image queue ViewModel while idle");
            AssertTrue(trainingPanel.ViewModel.IsStartTrainingEnabled, "StartTraining should start enabled through the training ViewModel");
            AssertTrue(!trainingPanel.ViewModel.IsStopTrainingEnabled, "StopTraining should start disabled through the training ViewModel");
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(!stopTraining.IsEnabled, "StopTraining should be disabled while training is idle");
            AssertTrue(InvokePrivateResult<bool>(window, "BeginTrainingCommand", "Preparing training dataset..."), "WPF training command did not enter busy state");
            AssertTrue(!yoloStatusPanel.ViewModel.IsFirstCheckEnabled, "First Check should disable through the YOLO status ViewModel while training command is running");
            AssertTrue(!projectConfigPanel.ViewModel.IsApplyRecipeEnabled, "Project apply should disable through the project ViewModel while training command is running");
            AssertTrue(!yoloModelPanel.ViewModel.IsSaveSettingsEnabled, "YOLO model settings save should disable through the model settings ViewModel while training command is running");
            AssertTrue(!trainingPanel.ViewModel.IsStartTrainingEnabled, "StartTraining should disable through the training ViewModel while training command is running");
            AssertTrue(trainingPanel.ViewModel.IsStopTrainingEnabled, "StopTraining should enable through the training ViewModel while training command is running");
            AssertTrue(!window.ShellViewModel.IsCurrentImageDetectionEnabled, "Detect should disable through the shell ViewModel while training command is running");
            AssertTrue(!imageQueuePanel.ViewModel.IsDetectSelectedEnabled, "Queue selected detect should disable through the image queue ViewModel while training command is running");
            AssertTrue(!imageQueuePanel.ViewModel.IsBatchDetectEnabled, "Queue batch detect should disable through the image queue ViewModel while training command is running");
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));

            AssertTrue(!firstCheck.IsEnabled, "First Check should be disabled while training command is running");
            AssertTrue(!detect.IsEnabled, "Detect should be disabled while training command is running");
            AssertTrue(!startTraining.IsEnabled, "StartTraining should be disabled while training command is running");
            AssertTrue(stopTraining.IsEnabled, "StopTraining should remain enabled while training command is running");

            InvokePrivate(window, "EndTrainingCommand");
            AssertTrue(yoloStatusPanel.ViewModel.IsFirstCheckEnabled, "First Check should re-enable through the YOLO status ViewModel after training command ends");
            AssertTrue(projectConfigPanel.ViewModel.IsApplyRecipeEnabled, "Project apply should re-enable through the project ViewModel after training command ends");
            AssertTrue(yoloModelPanel.ViewModel.IsSaveSettingsEnabled, "YOLO model settings save should re-enable through the model settings ViewModel after training command ends");
            AssertTrue(trainingPanel.ViewModel.IsStartTrainingEnabled, "StartTraining should re-enable through the training ViewModel after training command ends");
            AssertTrue(!trainingPanel.ViewModel.IsStopTrainingEnabled, "StopTraining should disable through the training ViewModel after training command ends");
            AssertTrue(!window.ShellViewModel.IsCurrentImageDetectionEnabled, "Detect should remain disabled through the shell ViewModel after returning to idle labeling mode");
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            AssertTrue(startTraining.IsEnabled, "StartTraining should be re-enabled after training command ends");
            AssertTrue(!stopTraining.IsEnabled, "StopTraining should be disabled after training command ends");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfWorkflowModeSeparatesLabelingAndInference()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "sample.jpg");
            using (Bitmap image = new Bitmap(8, 6))
            {
                image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                var teachingButton = (System.Windows.Controls.Control)window.FindName("TeachingModeButton");
                var inferenceButton = (System.Windows.Controls.Control)window.FindName("InferenceModeButton");
                var detectButton = (System.Windows.Controls.Control)window.FindName("DetectButton");
                var queueDetectButton = (System.Windows.Controls.Control)window.FindName("DetectSelectedQueueButton");
                var batchDetectButton = (System.Windows.Controls.Control)window.FindName("BatchDetectQueueButton");
                var queuePanel = (WpfImageQueuePanel)window.FindName("ImageQueuePanelControl");
                var yoloSmokeButton = (System.Windows.Controls.Control)window.FindName("RunYoloSmokeButton");

                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertTrue(window.ShellViewModel.IsLabelingModeActive, "Labeling mode should start active through the shell ViewModel");
                AssertTrue(!window.ShellViewModel.IsInferenceModeActive, "Inference mode should start inactive through the shell ViewModel");
                AssertTrue(window.ShellViewModel.IsLabelingModeButtonEnabled, "Labeling mode button should start enabled through the shell ViewModel");
                AssertTrue(window.ShellViewModel.IsInferenceModeButtonEnabled, "Inference mode button should start enabled through the shell ViewModel while idle");
                AssertTrue(teachingButton.IsEnabled, "Label edit mode should remain enabled as the active mode");
                AssertTrue(inferenceButton.IsEnabled, "Inference mode should be available by explicit action");
                AssertTrue(!window.ShellViewModel.IsCurrentImageDetectionEnabled, "Current-image inference should be disabled through the shell ViewModel in labeling mode");
                AssertTrue(!queuePanel.ViewModel.IsDetectSelectedEnabled, "Queue inference should be disabled through the image queue ViewModel in labeling mode");
                AssertTrue(!detectButton.IsEnabled, "Current-image inference should be disabled in labeling mode");
                AssertTrue(!queueDetectButton.IsEnabled, "Queue inference should be disabled in labeling mode");
                AssertTrue(!batchDetectButton.IsEnabled, "Batch inference should be disabled in labeling mode");
                AssertTrue(yoloSmokeButton.IsEnabled, "YOLO tab smoke test should stay available as an explicit operator action");
                AssertTrue(System.Windows.Controls.ToolTipService.GetShowOnDisabled(detectButton), "Disabled detection buttons should still explain why they are locked");
                AssertTrue((detectButton.ToolTip?.ToString() ?? string.Empty).Contains("추론 검사 모드", StringComparison.Ordinal), "Disabled current-image detection should explain the required mode");
                AssertTrue((queueDetectButton.ToolTip?.ToString() ?? string.Empty).Contains("추론 검사 모드", StringComparison.Ordinal), "Disabled queue detection should explain the required mode");

                AssertTrue(window.TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false), "WPF image load failed");
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertTrue(!detectButton.IsEnabled, "Image click/load must not enable inference");

                InvokePrivateResult<object>(window, "InferenceModeButton_Click", inferenceButton, new System.Windows.RoutedEventArgs());
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertTrue(!window.ShellViewModel.IsLabelingModeActive, "Labeling mode should become inactive after switching to inference");
                AssertTrue(window.ShellViewModel.IsInferenceModeActive, "Inference mode should become active through the shell ViewModel");
                AssertTrue(teachingButton.IsEnabled, "Labeling mode should be available after switching to inference");
                AssertTrue(inferenceButton.IsEnabled, "AI detection mode should remain enabled as the active mode");
                AssertTrue(window.ShellViewModel.IsCurrentImageDetectionEnabled, "Current-image inference should enable through the shell ViewModel in inference mode");
                AssertTrue(queuePanel.ViewModel.IsDetectSelectedEnabled, "Queue selected inference should enable through the image queue ViewModel in inference mode");
                AssertTrue(queuePanel.ViewModel.IsBatchDetectEnabled, "Queue batch inference should enable through the image queue ViewModel in inference mode");
                AssertTrue(detectButton.IsEnabled, "Current-image inference should be enabled only in inference mode");
                AssertTrue(queueDetectButton.IsEnabled, "Queue inference should be enabled only in inference mode");
                AssertTrue(batchDetectButton.IsEnabled, "Batch inference should be enabled only in inference mode");
                AssertTrue((detectButton.ToolTip?.ToString() ?? string.Empty).Contains("현재 이미지", StringComparison.Ordinal), "Enabled current-image detection should show the executable action");

                InvokePrivateResult<object>(window, "TeachingModeButton_Click", teachingButton, new System.Windows.RoutedEventArgs());
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertTrue(window.ShellViewModel.IsLabelingModeActive, "Labeling mode should become active again through the shell ViewModel");
                AssertTrue(!window.ShellViewModel.IsInferenceModeActive, "Inference mode should become inactive again through the shell ViewModel");
                AssertTrue(!window.ShellViewModel.IsCurrentImageDetectionEnabled, "Returning to labeling mode should disable shell ViewModel inference");
                AssertTrue(!detectButton.IsEnabled, "Returning to labeling mode should disable inference");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfCandidateRowsShowVisualStatus()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfCandidateReviewPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        XElement candidateListBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ListBox"
                && string.Equals((string)element.Attribute(xName), "CandidateListBox", StringComparison.Ordinal));
        XElement confidenceSlider = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Slider"
                && string.Equals((string)element.Attribute(xName), "CandidateConfidenceSlider", StringComparison.Ordinal));

        AssertTrue(candidateListBox != null, "WPF candidate review list was not found in XAML");
        AssertEqual("CandidateListBox_SelectionChanged", (string)candidateListBox.Attribute("SelectionChanged"));
        AssertEqual("CandidateListBox_PreviewKeyDown", (string)candidateListBox.Attribute("PreviewKeyDown"));
        AssertTrue(((string)candidateListBox.Attribute("ItemsSource") ?? string.Empty).Contains("ViewModel.Candidates", StringComparison.Ordinal), "WPF candidate list should bind rows through the panel view model");
        AssertTrue(((string)candidateListBox.Attribute("SelectedItem") ?? string.Empty).Contains("ViewModel.SelectedCandidate", StringComparison.Ordinal), "WPF candidate list should bind selection through the panel view model");
        string candidateXamlSource = File.ReadAllText(xamlPath);
        AssertTrue(candidateXamlSource.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", StringComparison.Ordinal), "WPF candidate list should recycle item containers for large candidate sets");
        AssertTrue(candidateXamlSource.Contains("ScrollViewer.CanContentScroll=\"True\"", StringComparison.Ordinal), "WPF candidate list should keep logical scrolling so virtualization stays active");
        AssertTrue(confidenceSlider != null, "WPF candidate confidence slider was not found in XAML");
        AssertEqual("CandidateConfidenceSlider_ValueChanged", (string)confidenceSlider.Attribute("ValueChanged"));
        AssertNamedXamlBinding(xaml, xName, "CandidateComparisonPanel", "Visibility", "ViewModel.ComparisonVisibility");
        AssertNamedXamlBinding(xaml, xName, "CandidateCompareCandidateText", "Text", "ViewModel.ComparisonCandidateText");
        AssertNamedXamlBinding(xaml, xName, "CandidateCompareCurrentText", "Text", "ViewModel.ComparisonCurrentText");
        AssertNamedXamlBinding(xaml, xName, "CandidateCompareOverlapText", "Text", "ViewModel.ComparisonOverlapText");
        AssertNamedXamlBinding(xaml, xName, "CandidateReviewHistoryPanel", "Visibility", "ViewModel.ReviewHistoryVisibility");
        AssertNamedXamlBinding(xaml, xName, "CandidateReviewHistoryItems", "ItemsSource", "ViewModel.ReviewHistory");

        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("CandidateReviewViewModel.ApplySelectionReview", StringComparison.Ordinal), "WPF shell should apply candidate detail and comparison together through the candidate review ViewModel");
        AssertTrue(!shellSource.Contains("SetCandidateDetailText", StringComparison.Ordinal), "WPF shell should not keep a separate candidate detail TextBlock update helper");
        AssertTrue(!shellSource.Contains("UpdateCandidateComparisonPanel", StringComparison.Ordinal), "WPF shell should not update the candidate comparison panel separately from candidate detail");
        AssertTrue(!shellSource.Contains("CandidateListBox?.SelectedItem is WpfCandidateReviewListItem", StringComparison.Ordinal), "WPF shell should use the candidate review ViewModel selection instead of a direct ListBox fallback");
        AssertTrue(!shellSource.Contains("CandidateComparisonPanel.Visibility =", StringComparison.Ordinal), "WPF shell should not directly show or hide the candidate comparison panel");
        AssertTrue(!shellSource.Contains("CandidateComparisonPanel.BorderBrush =", StringComparison.Ordinal), "WPF shell should not directly paint the candidate comparison border");
        AssertTrue(!shellSource.Contains("CandidateCompareCandidateText.Text =", StringComparison.Ordinal), "WPF shell should not directly write the candidate comparison AI text");
        AssertTrue(!shellSource.Contains("CandidateCompareCurrentText.Text =", StringComparison.Ordinal), "WPF shell should not directly write the candidate comparison current-label text");
        AssertTrue(!shellSource.Contains("CandidateCompareOverlapText.Text =", StringComparison.Ordinal), "WPF shell should not directly write the candidate comparison overlap text");
        AssertTrue(!shellSource.Contains("CandidateCompareOverlapText.Foreground =", StringComparison.Ordinal), "WPF shell should not directly paint the candidate comparison overlap text");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            var manualRois = GetPrivateField<List<System.Drawing.Rectangle>>(window, "manualRois");
            manualRois.Add(new System.Drawing.Rectangle(4, 6, 18, 20));

            var candidates = GetPrivateField<List<YoloWorkerSmokeCandidate>>(window, "pendingDetectionCandidates");
            candidates.Add(new YoloWorkerSmokeCandidate
            {
                Index = 2,
                ClassName = "OK",
                Confidence = 0.95,
                X = 4,
                Y = 6,
                Width = 18,
                Height = 20
            });
            candidates.Add(new YoloWorkerSmokeCandidate
            {
                Index = 3,
                ClassName = "NG",
                Confidence = 0.96,
                X = 60,
                Y = 64,
                Width = 14,
                Height = 16
            });

            InvokePrivate(window, "RefreshCandidateList");
            var candidatePanel = (WpfCandidateReviewPanel)window.FindName("CandidateReviewPanelControl");
            if (candidatePanel != null)
            {
            WpfCandidateReviewPanelViewModel reviewViewModel = candidatePanel.ViewModel;
            var boundList = (System.Windows.Controls.ListBox)window.FindName("CandidateListBox");
            AssertTrue(ReferenceEquals(boundList.ItemsSource, reviewViewModel.Candidates), "WPF candidate list should render the view model candidate collection");
            AssertEqual(2, reviewViewModel.Candidates.Count);

            WpfCandidateReviewListItem firstRow = reviewViewModel.Candidates[0];
            AssertTrue(firstRow.Payload is YoloWorkerSmokeCandidate, "WPF candidate row should keep the detection candidate payload");
            AssertTrue(firstRow.IconKind.ToString().Contains("AlertCircle", StringComparison.Ordinal), "WPF duplicate candidate row should expose warning status");
            AssertTrue(firstRow.SecondaryText.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate row should show candidate bounds");
            AssertTrue(firstRow.ToolTip.Contains("IoU", StringComparison.Ordinal), "WPF candidate tooltip should show overlap ratio");

            AssertTrue(!reviewViewModel.IsConfirmSelectedEnabled, "WPF duplicate selected candidate should not be directly confirmable");
            AssertTrue(reviewViewModel.ConfirmSelectedToolTip.Contains("\uC911\uBCF5", StringComparison.Ordinal), "WPF duplicate selected candidate should explain why confirmation is disabled");
            AssertTrue(reviewViewModel.IsConfirmAllEnabled, "WPF all-candidate action should remain available when another visible candidate is confirmable");
            AssertTrue(reviewViewModel.IsSkipSelectedEnabled, "WPF skip candidate action should be available when a candidate is visible");
            AssertTrue(reviewViewModel.IsPreviousCandidateEnabled, "WPF previous candidate navigation should be enabled when multiple candidates are visible");
            AssertTrue(reviewViewModel.IsNextCandidateEnabled, "WPF next candidate navigation should be enabled when multiple candidates are visible");
            AssertTrue(reviewViewModel.IsFocusCandidateEnabled, "WPF focus candidate action should be enabled when a candidate is selected");
            AssertEqual(System.Windows.Visibility.Visible, reviewViewModel.ComparisonVisibility);
            AssertTrue(reviewViewModel.IsComparisonHighOverlap, "WPF duplicate selected candidate should mark comparison as high-overlap through the ViewModel");
            AssertTrue(reviewViewModel.ComparisonCandidateText.Contains("OK 95.0%", StringComparison.Ordinal), "WPF candidate comparison ViewModel should show AI class and confidence");
            AssertTrue(reviewViewModel.ComparisonCandidateText.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate comparison ViewModel should show AI bounds");
            AssertTrue(reviewViewModel.ComparisonCurrentText.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate comparison ViewModel should show current label bounds");
            AssertTrue(reviewViewModel.ComparisonOverlapText.Contains("100", StringComparison.Ordinal), "WPF candidate comparison ViewModel should show overlap ratio");

            var uiComparisonPanel = (System.Windows.Controls.Border)window.FindName("CandidateComparisonPanel");
            var uiCandidateText = (System.Windows.Controls.TextBlock)window.FindName("CandidateCompareCandidateText");
            var uiCurrentText = (System.Windows.Controls.TextBlock)window.FindName("CandidateCompareCurrentText");
            var uiOverlapText = (System.Windows.Controls.TextBlock)window.FindName("CandidateCompareOverlapText");
            AssertEqual(System.Windows.Visibility.Visible, uiComparisonPanel.Visibility);
            AssertTrue(uiCandidateText.Text.Contains("OK 95.0%", StringComparison.Ordinal), "WPF candidate comparison should show AI class and confidence");
            AssertTrue(uiCandidateText.Text.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate comparison should show AI bounds");
            AssertTrue(uiCurrentText.Text.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate comparison should show current label bounds");
            AssertTrue(uiOverlapText.Text.Contains("100", StringComparison.Ordinal), "WPF candidate comparison should show overlap ratio");

            boundList.SelectedIndex = 1;
            InvokePrivate(window, "UpdateCandidateActionState");
            AssertTrue(reviewViewModel.IsConfirmSelectedEnabled, "WPF non-overlapping selected candidate should be confirmable");
            AssertTrue(reviewViewModel.IsPreviousCandidateEnabled, "WPF previous candidate navigation should stay enabled after moving selection");
            AssertTrue(reviewViewModel.IsNextCandidateEnabled, "WPF next candidate navigation should stay enabled after moving selection");
            }
            else
            {
            var list = (System.Windows.Controls.ListBox)window.FindName("CandidateListBox");
            var row = list.Items[0] as System.Windows.Controls.ListBoxItem;
            AssertTrue(row != null, "WPF candidate review should use ListBoxItem rows");
            AssertTrue(row.Content is System.Windows.Controls.Grid, "WPF candidate row should use a visual grid layout");
            var confirmSelectedButton = (System.Windows.Controls.Control)window.FindName("ConfirmSelectedCandidateButton");
            var confirmAllButton = (System.Windows.Controls.Control)window.FindName("ConfirmAllCandidatesButton");
            AssertTrue(!confirmSelectedButton.IsEnabled, "WPF duplicate selected candidate should not be directly confirmable");
            AssertTrue((confirmSelectedButton.ToolTip?.ToString() ?? string.Empty).Contains("중복", StringComparison.Ordinal), "WPF duplicate selected candidate should explain why confirmation is disabled");
            AssertTrue(confirmAllButton.IsEnabled, "WPF all-candidate action should remain available when another visible candidate is confirmable");
            AssertTrue(((System.Windows.Controls.Control)window.FindName("SkipSelectedCandidateButton")).IsEnabled, "WPF skip candidate action should be available when a candidate is visible");

            var grid = (System.Windows.Controls.Grid)row.Content;
            AssertTrue(grid.Children.Cast<System.Windows.UIElement>().Any(child => child.GetType().FullName == "MahApps.Metro.IconPacks.PackIconMaterial"), "WPF candidate row should include a status icon");
            AssertTrue(grid.Children.OfType<System.Windows.Controls.StackPanel>().Any(panel => panel.Children.OfType<System.Windows.Controls.TextBlock>().Any(text => text.Text.Contains("중복 가능", StringComparison.Ordinal))), "WPF duplicate candidate row should show duplicate status");
            AssertTrue(row.ToolTip.ToString().Contains("상태: 중복 가능", StringComparison.Ordinal), "WPF duplicate candidate tooltip should show duplicate review status");
            AssertTrue(row.ToolTip.ToString().Contains("현재 라벨: 수동 Defect", StringComparison.Ordinal), "WPF candidate tooltip should compare against current labels");
            AssertTrue(row.ToolTip.ToString().Contains("IoU", StringComparison.Ordinal), "WPF candidate tooltip should show overlap ratio");

            var comparisonPanel = (System.Windows.Controls.Border)window.FindName("CandidateComparisonPanel");
            var candidateText = (System.Windows.Controls.TextBlock)window.FindName("CandidateCompareCandidateText");
            var currentText = (System.Windows.Controls.TextBlock)window.FindName("CandidateCompareCurrentText");
            var overlapText = (System.Windows.Controls.TextBlock)window.FindName("CandidateCompareOverlapText");
            AssertEqual(System.Windows.Visibility.Visible, comparisonPanel.Visibility);
            AssertTrue(candidateText.Text.Contains("OK 95.0%", StringComparison.Ordinal), "WPF candidate comparison should show AI class and confidence");
            AssertTrue(candidateText.Text.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate comparison should show AI bounds");
            AssertTrue(currentText.Text.Contains("수동 Defect", StringComparison.Ordinal), "WPF candidate comparison should show overlapping current label");
            AssertTrue(currentText.Text.Contains("크기 18x20 / 위치 x=4, y=6", StringComparison.Ordinal), "WPF candidate comparison should show current label bounds");
            AssertTrue(overlapText.Text.Contains("중복", StringComparison.Ordinal), "WPF candidate comparison should flag high-overlap candidates");
            AssertTrue(overlapText.Text.Contains("100", StringComparison.Ordinal), "WPF candidate comparison should show overlap ratio");

            list.SelectedIndex = 1;
            InvokePrivate(window, "UpdateCandidateActionState");
            AssertTrue(confirmSelectedButton.IsEnabled, "WPF non-overlapping selected candidate should be confirmable");
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfCandidateReviewPanelDeclaresNavigation()
    {
        string root = FindRepositoryRoot();
        string panelPath = Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfCandidateReviewPanel.xaml");
        XDocument xaml = XDocument.Load(panelPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        AssertNamedXamlButtonClick(xaml, xName, "PreviousCandidateButton", "PreviousCandidateButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "NextCandidateButton", "NextCandidateButton_Click");
        AssertNamedXamlButtonClick(xaml, xName, "FocusCandidateButton", "FocusCandidateButton_Click");
        AssertNamedXamlBinding(xaml, xName, "PreviousCandidateButton", "IsEnabled", "ViewModel.IsPreviousCandidateEnabled");
        AssertNamedXamlBinding(xaml, xName, "NextCandidateButton", "IsEnabled", "ViewModel.IsNextCandidateEnabled");
        AssertNamedXamlBinding(xaml, xName, "FocusCandidateButton", "IsEnabled", "ViewModel.IsFocusCandidateEnabled");
        AssertNamedXamlBinding(xaml, xName, "SkipSelectedCandidateButton", "IsEnabled", "ViewModel.IsSkipSelectedEnabled");
        AssertNamedXamlBinding(xaml, xName, "CandidatePostActionPolicyText", "Text", "ViewModel.PostActionPolicyText");

        string panelCode = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfCandidateReviewPanel.xaml.cs"));
        string shellSource = File.ReadAllText(Path.Combine(root, "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));

        AssertTrue(panelCode.Contains("PreviousCandidateRequested", StringComparison.Ordinal), "candidate panel should expose previous-candidate events");
        AssertTrue(panelCode.Contains("NextCandidateRequested", StringComparison.Ordinal), "candidate panel should expose next-candidate events");
        AssertTrue(panelCode.Contains("FocusCandidateRequested", StringComparison.Ordinal), "candidate panel should expose focus-candidate events");
        AssertTrue(shellSource.Contains("SelectCandidateOffset(1)", StringComparison.Ordinal), "WPF shell should implement next-candidate navigation");
        AssertTrue(shellSource.Contains("SelectCandidateOffset(-1)", StringComparison.Ordinal), "WPF shell should implement previous-candidate navigation");
        AssertTrue(shellSource.Contains("FocusSelectedCandidateInViewer", StringComparison.Ordinal), "WPF shell should focus the selected candidate in the viewer");
        AssertTrue(shellSource.Contains("CandidateReviewViewModel?.SetNavigationState", StringComparison.Ordinal), "WPF shell should expose candidate navigation state through the candidate review ViewModel");
        AssertTrue(shellSource.Contains("FindNextVisibleCandidateAfter", StringComparison.Ordinal), "WPF shell should preserve next-candidate review position after confirm/skip");
        AssertTrue(shellSource.Contains("RefreshCandidateListWithPreferred(nextCandidate)", StringComparison.Ordinal), "candidate confirm/skip should refresh with the next preferred candidate");
        AssertTrue(shellSource.Contains("FocusCandidateInViewer(nextCandidate", StringComparison.Ordinal), "candidate confirm/skip should keep the next candidate visible on the canvas");
        AssertTrue(new WpfCandidateReviewPanelViewModel().PostActionPolicyText.Contains("다음 후보", StringComparison.Ordinal), "candidate review should make the next-candidate policy visible");
        AssertTrue(new WpfCandidateReviewPanelViewModel().ReviewHistoryVisibility == System.Windows.Visibility.Collapsed, "candidate review history should stay hidden until a review action occurs");
        AssertTrue(shellSource.Contains("Key.N", StringComparison.Ordinal), "candidate list should support next-candidate keyboard review");
        AssertTrue(shellSource.Contains("Key.P", StringComparison.Ordinal), "candidate list should support previous-candidate keyboard review");
        AssertTrue(shellSource.Contains("Key.F", StringComparison.Ordinal), "candidate list should support focus keyboard review");
    }

    private static void TestWpfWorkflowCommandStateService()
    {
        WpfWorkflowCommandState labeling = WpfWorkflowCommandStateService.Build(
            isInferenceMode: false,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true);
        AssertTrue(labeling.CanRunGeneralCommands, "General commands should be available while idle");
        AssertTrue(!labeling.CanRunInference, "Inference commands should stay disabled in labeling mode");
        AssertTrue(labeling.CanSaveProjectConfig, "Project config save should be available when a recipe name exists");
        AssertTrue(labeling.CurrentImageDetectionToolTip.Contains("추론 검사 모드", StringComparison.Ordinal), "Disabled detection tooltip should explain the required mode");

        WpfWorkflowCommandState inference = WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: false);
        AssertTrue(inference.CanRunInference, "Inference commands should be available in inference mode while idle");
        AssertTrue(!inference.CanSaveProjectConfig, "Project config save should require a recipe name");

        WpfWorkflowCommandState busy = WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: true,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true);
        AssertTrue(!busy.CanRunGeneralCommands, "General commands should be disabled while any YOLO environment command is running");
        AssertTrue(!busy.CanRunInference, "Inference commands should be disabled while busy");

        WpfWorkflowCommandState stoppingTraining = WpfWorkflowCommandStateService.Build(
            isInferenceMode: false,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: true,
            isTrainingStopAvailable: true,
            hasCurrentRecipeName: true);
        AssertTrue(stoppingTraining.CanStopTraining, "Training stop should stay available while training is running");
        AssertTrue(!stoppingTraining.CanRunGeneralCommands, "Starting another command should be disabled while training is running");
    }

    private static void TestWpfClassCatalogPanelDeclaresClassEditControls()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfClassCatalogPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        XElement classNameBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBox"
                && string.Equals((string)element.Attribute(xName), "ClassNameBox", StringComparison.Ordinal));
        XElement addButton = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Button"
                && string.Equals((string)element.Attribute(xName), "AddClassButton", StringComparison.Ordinal));
        XElement removeButton = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Button"
                && string.Equals((string)element.Attribute(xName), "RemoveClassButton", StringComparison.Ordinal));
        XElement outputRootBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBox"
                && string.Equals((string)element.Attribute(xName), "OutputRootPathBox", StringComparison.Ordinal));
        XElement browseOutputRootButton = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Button"
                && string.Equals((string)element.Attribute(xName), "BrowseOutputRootButton", StringComparison.Ordinal));
        XElement saveOutputRootButton = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Button"
                && string.Equals((string)element.Attribute(xName), "SaveOutputRootButton", StringComparison.Ordinal));
        XElement classListBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ListBox"
                && string.Equals((string)element.Attribute(xName), "ClassListBox", StringComparison.Ordinal));
        XElement classEditStatusText = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBlock"
                && string.Equals((string)element.Attribute(xName), "ClassEditStatusText", StringComparison.Ordinal));

        AssertTrue(classNameBox != null, "WPF class name editor was not found in class catalog XAML");
        AssertEqual("ClassNameBox_KeyDown", (string)classNameBox.Attribute("KeyDown"));
        AssertNamedXamlBinding(xaml, xName, "ClassNameBox", "Text", "ClassName");
        AssertTrue(addButton != null, "WPF class add button was not found in class catalog XAML");
        AssertEqual("AddClassButton_Click", (string)addButton.Attribute("Click"));
        AssertTrue(removeButton != null, "WPF class remove button was not found in class catalog XAML");
        AssertEqual("RemoveClassButton_Click", (string)removeButton.Attribute("Click"));
        AssertTrue(outputRootBox != null, "WPF output root path editor was not found in class catalog XAML");
        AssertNamedXamlBinding(xaml, xName, "OutputRootPathBox", "Text", "OutputRootPath");
        AssertTrue(browseOutputRootButton != null, "WPF output root browse button was not found in class catalog XAML");
        AssertEqual("BrowseOutputRootButton_Click", (string)browseOutputRootButton.Attribute("Click"));
        AssertTrue(saveOutputRootButton != null, "WPF output root save button was not found in class catalog XAML");
        AssertEqual("SaveOutputRootButton_Click", (string)saveOutputRootButton.Attribute("Click"));
        AssertTrue(classEditStatusText != null, "WPF class edit status text was not found in class catalog XAML");
        AssertNamedXamlBinding(xaml, xName, "ClassEditStatusText", "Text", "StatusText");
        AssertTrue(classListBox != null, "WPF class list was not found in class catalog XAML");
        AssertEqual("ClassListBox_SelectionChanged", (string)classListBox.Attribute("SelectionChanged"));
        AssertNamedXamlBinding(xaml, xName, "ClassListBox", "ItemsSource", "Classes");
        AssertNamedXamlBinding(xaml, xName, "ClassListBox", "SelectedItem", "SelectedClass");

        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("ClassCatalogViewModel?.SetClasses", StringComparison.Ordinal)
            || shellSource.Contains("ClassCatalogViewModel.SetClasses", StringComparison.Ordinal),
            "WPF class catalog should populate the view model collection");
        AssertTrue(shellSource.Contains("ClassCatalogViewModel?.OutputRootPath", StringComparison.Ordinal), "WPF class catalog should read output root from the view model");
        AssertTrue(!shellSource.Contains("ClassListBox?.SelectedItem", StringComparison.Ordinal), "WPF class catalog should not read selected classes directly from the list box");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        try
        {
            AssertTrue(window.FindName("ClassCatalogPanelControl") is WpfClassCatalogPanel, "WPF class catalog user control was not registered");
            AssertTrue(((WpfClassCatalogPanel)window.FindName("ClassCatalogPanelControl")).ViewModel != null, "WPF class catalog view model was not created");
            AssertTrue(window.FindName("ClassNameBox") is System.Windows.Controls.TextBox, "WPF class name editor proxy was not registered");
            AssertTrue(window.FindName("OutputRootPathBox") is System.Windows.Controls.TextBox, "WPF output root path editor proxy was not registered");
            AssertTrue(window.FindName("BrowseOutputRootButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF output root browse button should use WPF-UI button");
            AssertTrue(window.FindName("SaveOutputRootButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF output root save button should use WPF-UI button");
            AssertTrue(window.FindName("ClassListBox") is System.Windows.Controls.ListBox, "WPF class list proxy was not registered");
            AssertTrue(window.FindName("AddClassButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF class add button should use WPF-UI button");
            AssertTrue(window.FindName("RemoveClassButton").GetType().FullName == "Wpf.Ui.Controls.Button", "WPF class remove button should use WPF-UI button");
        }
        finally
        {
            window.Close();
        }
    }

    private static void TestWpfObjectReviewSummarizesLabels()
    {
        string xamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfObjectReviewPanel.xaml");
        XDocument xaml = XDocument.Load(xamlPath);
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        XElement objectListBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ListBox"
                && string.Equals((string)element.Attribute(xName), "ObjectListBox", StringComparison.Ordinal));
        XElement objectClassBoxElement = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "ComboBox"
                && string.Equals((string)element.Attribute(xName), "ObjectClassBox", StringComparison.Ordinal));

        AssertTrue(objectListBox != null, "WPF object review list was not found in XAML");
        AssertEqual("ObjectListBox_PreviewKeyDown", (string)objectListBox.Attribute("PreviewKeyDown"));
        AssertTrue(objectClassBoxElement != null, "WPF object class selector was not found in XAML");
        AssertTrue(objectClassBoxElement.Attribute("SelectionChanged") == null, "WPF object class selector should update action state through ViewModel binding, not a shell selection event");
        AssertNamedXamlBinding(xaml, xName, "ObjectReviewSummaryText", "Text", "ViewModel.SummaryText");
        AssertNamedXamlBinding(xaml, xName, "ObjectListBox", "ItemsSource", "ViewModel.Objects");
        AssertNamedXamlBinding(xaml, xName, "ObjectListBox", "SelectedItem", "ViewModel.SelectedObject");
        AssertNamedXamlBinding(xaml, xName, "ObjectClassBox", "ItemsSource", "ViewModel.ClassNames");
        AssertNamedXamlBinding(xaml, xName, "ObjectClassBox", "SelectedItem", "ViewModel.SelectedClassName");
        AssertNamedXamlBinding(xaml, xName, "DeleteObjectButton", "IsEnabled", "ViewModel.IsDeleteEnabled");
        AssertNamedXamlBinding(xaml, xName, "ApplyObjectClassButton", "IsEnabled", "ViewModel.IsApplyClassEnabled");
        string objectXamlSource = File.ReadAllText(xamlPath);
        AssertTrue(objectXamlSource.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", StringComparison.Ordinal), "WPF object list should recycle item containers for large label sets");
        AssertTrue(objectXamlSource.Contains("ScrollViewer.CanContentScroll=\"True\"", StringComparison.Ordinal), "WPF object list should keep logical scrolling so virtualization stays active");
        AssertTrue(objectXamlSource.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\"", StringComparison.Ordinal), "WPF object list should avoid horizontal scrolling during labeling review");

        string manualSummary = WpfObjectReviewPresenter.FormatManualSummary(1, "Defect", new System.Drawing.Rectangle(36, 36, 35, 35), "\uBC15\uC2A4");
        AssertTrue(manualSummary.Contains("\uD06C\uAE30 35x35 / \uC704\uCE58 x=36, y=36", StringComparison.Ordinal), "WPF object review should show size before position in compact rows");
        AssertTrue(!manualSummary.Contains("\uC218\uB3D9", StringComparison.Ordinal), "WPF object review rows should avoid redundant manual prefixes that crowd the side panel");

        string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
        AssertTrue(shellSource.Contains("WpfObjectReviewEditService.TryApplyClass", StringComparison.Ordinal), "WPF shell should delegate object class application to the review edit service");
        AssertTrue(shellSource.Contains("WpfObjectReviewEditService.TryDelete", StringComparison.Ordinal), "WPF shell should delegate object deletion to the review edit service");
        AssertTrue(!shellSource.Contains("SetManualRoiClassName", StringComparison.Ordinal), "WPF shell should not keep manual ROI class mutation helpers");
        AssertTrue(!shellSource.Contains("RemoveManualRoi", StringComparison.Ordinal), "WPF shell should not keep direct manual ROI deletion helpers");
        AssertTrue(!shellSource.Contains("ObjectListBox?.SelectedItem is WpfObjectReviewListItem", StringComparison.Ordinal), "WPF shell should use the object review ViewModel selection instead of a direct ListBox fallback");
        AssertTrue(!shellSource.Contains("ObjectClassBox_SelectionChanged", StringComparison.Ordinal), "WPF shell should not use a class ComboBox selection event for object review action state");
        AssertTrue(!shellSource.Contains("private void SelectObjectClass", StringComparison.Ordinal), "WPF shell should not keep object class selection helpers that write directly to the editor");
        AssertTrue(shellSource.Contains("ObjectReviewViewModel.SetSelectedObjectClass", StringComparison.Ordinal), "WPF shell should ask the object review ViewModel to sync the selected object's class");

        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        WpfLabelingShellWindow window = new WpfLabelingShellWindow();
        string reviewClassName = "ScratchObjectReview";
        try
        {
            var summary = (System.Windows.Controls.TextBlock)window.FindName("ObjectReviewSummaryText");
            var list = (System.Windows.Controls.ListBox)window.FindName("ObjectListBox");
            var objectReviewPanel = (WpfObjectReviewPanel)window.FindName("ObjectReviewPanelControl");
            var deleteButton = (System.Windows.Controls.Control)window.FindName("DeleteObjectButton");
            var objectClassBox = (System.Windows.Controls.ComboBox)window.FindName("ObjectClassBox");
            var applyClassButton = (System.Windows.Controls.Control)window.FindName("ApplyObjectClassButton");
            AssertEqual("현재 이미지 객체 없음", summary.Text);
            AssertTrue(list.Items.Count == 1, "WPF object review should show an empty state item");
            AssertTrue(!deleteButton.IsEnabled, "WPF object delete button should be disabled without a selected object");
            AssertTrue(!applyClassButton.IsEnabled, "WPF object class apply button should be disabled without a selected object");

            ClassCatalogService.TryAddClass(CGlobal.Inst.Data, reviewClassName, out CClassItem _);
            InvokePrivateResult<object>(window, "PopulateClassList", reviewClassName);

            var manualRois = GetPrivateField<List<System.Drawing.Rectangle>>(window, "manualRois");
            manualRois.Add(new System.Drawing.Rectangle(3, 4, 20, 30));
            InvokePrivate(window, "RefreshObjectList");

            AssertEqual("1개 객체", summary.Text);
            var item = list.Items[0] as WpfObjectReviewListItem;
            AssertTrue(item != null, "WPF object review should use bound view model rows");
            AssertEqual(0, list.SelectedIndex);
            AssertTrue(deleteButton.IsEnabled, "WPF object review should select the first object automatically");
            AssertTrue(item.DisplayText.Contains("x=3, y=4", StringComparison.Ordinal), "WPF object review should show labeled object position");
            AssertTrue(item.DisplayText.Contains("20x30", StringComparison.Ordinal), "WPF object review should show labeled object size");
            AssertTrue(item.ToolTip.ToString().Contains("클래스: Defect", StringComparison.Ordinal), "WPF object review tooltip should show the class");

            list.SelectedIndex = 0;
            AssertTrue(ReferenceEquals(objectReviewPanel.ViewModel.SelectedObject, item), "WPF object review selection should flow through the ViewModel binding");
            AssertTrue(deleteButton.IsEnabled, "WPF object delete button should be enabled for a selected object");
            AssertTrue(applyClassButton.IsEnabled, "WPF object class apply button should be enabled for a selected object");
            objectClassBox.SelectedItem = reviewClassName;
            InvokePrivateResult<object>(window, "ApplyObjectClassButton_Click", applyClassButton, new System.Windows.RoutedEventArgs());

            item = list.Items[0] as WpfObjectReviewListItem;
            AssertTrue(item.Content.ToString().Contains(reviewClassName, StringComparison.Ordinal), "WPF object review should show the updated class");
            AssertTrue(!item.Content.ToString().Contains("\uC218\uB3D9", StringComparison.Ordinal), "WPF object review row should keep the compact class/tool/bounds format after class edits");
            Dictionary<string, List<CRectangleObject>> roisByClass = InvokePrivateResult<Dictionary<string, List<CRectangleObject>>>(window, "BuildAnnotationRois");
            AssertTrue(roisByClass.ContainsKey(reviewClassName), "WPF object review class change should be reflected in saved annotations");
            AssertEqual(1, roisByClass[reviewClassName].Count);

            list.SelectedIndex = 0;
            InvokePrivateResult<object>(window, "DeleteObjectButton_Click", deleteButton, new System.Windows.RoutedEventArgs());

            AssertEqual(0, manualRois.Count);
            AssertEqual("현재 이미지 객체 없음", summary.Text);
            AssertTrue(!deleteButton.IsEnabled, "WPF object delete button should disable after the selected object is deleted");
        }
        finally
        {
            window.Close();
            CGlobal.Inst.Data.ClassNamedList.RemoveAll(item => string.Equals(item?.Text, reviewClassName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertXamlTextBoxInputGuard(XDocument xaml, string controlName, string expectedHandler, string expectedTooltipFragment)
    {
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        XElement textBox = xaml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TextBox"
                && string.Equals((string)element.Attribute(xName), controlName, StringComparison.Ordinal));

        AssertTrue(textBox != null, $"WPF numeric TextBox was not found: {controlName}");
        AssertEqual(expectedHandler, (string)textBox.Attribute("PreviewTextInput"));

        string tooltip = (string)textBox.Attribute("ToolTip") ?? string.Empty;
        AssertTrue(tooltip.Contains(expectedTooltipFragment, StringComparison.Ordinal), $"WPF numeric TextBox tooltip was not set: {controlName}");
    }

    private static void AssertNamedXamlBinding(XDocument xaml, XName xName, string controlName, string targetPropertyName, string expectedBindingProperty)
    {
        XElement element = xaml.Descendants()
            .FirstOrDefault(candidate => string.Equals((string)candidate.Attribute(xName), controlName, StringComparison.Ordinal));

        AssertTrue(element != null, $"WPF bound control was not found: {controlName}");
        string binding = (string)element.Attribute(targetPropertyName) ?? string.Empty;
        AssertTrue(
            binding.Contains($"Binding {expectedBindingProperty}", StringComparison.Ordinal),
            $"WPF control {controlName}.{targetPropertyName} was not bound to {expectedBindingProperty}");
    }

    private static void TestWpfSettingsViewModelsRoundTrip()
    {
        var yoloSettings = new PythonModelSettings
        {
            PythonExecutablePath = @"C:\Python311\python.exe",
            ProjectRootPath = @"C:\Git\yolov5",
            ClientScriptPath = @"C:\Git\yolov5\labelling_tcp_client.py",
            WeightsPath = @"C:\Git\yolov5\best.pt",
            ImageRootPath = @"C:\Git\yolov5\data\train\images",
            MinimumDetectionConfidence = 0.25F,
            MaximumDetectionCandidates = 20,
            InferenceImageSize = 320,
            DetectionTimeoutSeconds = 30,
            AutoStartClient = true
        };
        var yoloViewModel = new WpfYoloModelSettingsPanelViewModel();
        yoloViewModel.LoadFrom(yoloSettings);
        AssertEqual(@"C:\Git\yolov5", yoloViewModel.ProjectRootPath);

        yoloViewModel.MinimumConfidenceText = "0.85";
        yoloViewModel.MaximumCandidatesText = "12";
        yoloViewModel.InferenceImageSizeText = "416";
        yoloViewModel.TimeoutSecondsText = "42";
        yoloViewModel.AutoStartClient = false;
        yoloViewModel.WeightsPath = @"C:\model\custom.pt";
        yoloViewModel.ApplyTo(yoloSettings);

        AssertEqual(0.85F, yoloSettings.MinimumDetectionConfidence);
        AssertEqual(12, yoloSettings.MaximumDetectionCandidates);
        AssertEqual(416, yoloSettings.InferenceImageSize);
        AssertEqual(42, yoloSettings.DetectionTimeoutSeconds);
        AssertTrue(!yoloSettings.AutoStartClient, "YOLO auto-start value was not applied from the view model");
        AssertEqual(@"C:\model\custom.pt", yoloSettings.WeightsPath);
        yoloViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(yoloViewModel.IsBrowsePythonEnabled, "YOLO python browse should be enabled while idle");
        AssertTrue(yoloViewModel.IsBrowseProjectRootEnabled, "YOLO project browse should be enabled while idle");
        AssertTrue(yoloViewModel.IsBrowseClientScriptEnabled, "YOLO client browse should be enabled while idle");
        AssertTrue(yoloViewModel.IsBrowseWeightsEnabled, "YOLO weights browse should be enabled while idle");
        AssertTrue(yoloViewModel.IsBrowseImageRootEnabled, "YOLO image-root browse should be enabled while idle");
        AssertTrue(yoloViewModel.IsSaveSettingsEnabled, "YOLO settings save should be enabled while idle");
        AssertTrue(yoloViewModel.IsResetSettingsEnabled, "YOLO settings reset should be enabled while idle");
        yoloViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: true,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(!yoloViewModel.IsBrowsePythonEnabled, "YOLO python browse should disable while busy");
        AssertTrue(!yoloViewModel.IsBrowseProjectRootEnabled, "YOLO project browse should disable while busy");
        AssertTrue(!yoloViewModel.IsBrowseClientScriptEnabled, "YOLO client browse should disable while busy");
        AssertTrue(!yoloViewModel.IsBrowseWeightsEnabled, "YOLO weights browse should disable while busy");
        AssertTrue(!yoloViewModel.IsBrowseImageRootEnabled, "YOLO image-root browse should disable while busy");
        AssertTrue(!yoloViewModel.IsSaveSettingsEnabled, "YOLO settings save should disable while busy");
        AssertTrue(!yoloViewModel.IsResetSettingsEnabled, "YOLO settings reset should disable while busy");

        var trainingSettings = new TrainingSettings();
        var datasetSettings = new YoloDatasetSettings();
        var trainingParam = new CYolov5TrainingParam();
        var trainingViewModel = new WpfTrainingSettingsPanelViewModel();
        trainingViewModel.LoadFrom(trainingSettings, datasetSettings);
        trainingViewModel.ImageSizeText = "640";
        trainingViewModel.BatchText = "8";
        trainingViewModel.EpochText = "12";
        trainingViewModel.Cfg = CYolov5TrainingParam.Cfg.yolov5m.ToString();
        trainingViewModel.Weight = CYolov5TrainingParam.Weight.yolov5m.ToString();
        trainingViewModel.ValidationPercentText = "25";
        trainingViewModel.TestPercentText = "10";
        trainingViewModel.SplitSeedText = "99";
        trainingViewModel.ApplyTo(trainingSettings, datasetSettings, trainingParam);

        AssertEqual(640, trainingSettings.ImageSize);
        AssertEqual(8, trainingSettings.Batch);
        AssertEqual(12, trainingSettings.Epoch);
        AssertEqual(CYolov5TrainingParam.Cfg.yolov5m.ToString(), trainingSettings.Cfg);
        AssertEqual(CYolov5TrainingParam.Weight.yolov5m.ToString(), trainingSettings.Weight);
        AssertEqual(25, datasetSettings.ValidationPercent);
        AssertEqual(10, datasetSettings.TestPercent);
        AssertEqual(99, datasetSettings.SplitSeed);
        AssertTrue(trainingViewModel.SplitPolicyHintText.Contains("Validation", StringComparison.Ordinal), "training split policy hint should mention validation");
        AssertTrue(trainingViewModel.SplitPolicyHintText.Contains("Test", StringComparison.Ordinal), "training split policy hint should mention test");
        AssertEqual(CYolov5TrainingParam.Cfg.yolov5m, trainingParam.cfg);
        AssertEqual(CYolov5TrainingParam.Weight.yolov5m, trainingParam.weight);
        trainingViewModel.SetTrainingReadinessText("Ready");
        trainingViewModel.SetTrainingProgress("Running", "Epoch 1/2", 42, isIndeterminate: true);
        trainingViewModel.SetTrainingStatusBrushes(System.Windows.Media.Brushes.LimeGreen, System.Windows.Media.Brushes.DodgerBlue);
        AssertEqual("Ready", trainingViewModel.TrainingReadinessText);
        AssertEqual("Running", trainingViewModel.TrainingProgressText);
        AssertEqual("Epoch 1/2", trainingViewModel.TrainingEpochStatusText);
        AssertEqual(42D, trainingViewModel.TrainingProgressValue);
        AssertEqual(true, trainingViewModel.TrainingProgressIsIndeterminate);
        AssertEqual(System.Windows.Media.Brushes.LimeGreen, trainingViewModel.TrainingReadinessForeground);
        AssertEqual(System.Windows.Media.Brushes.DodgerBlue, trainingViewModel.TrainingProgressForeground);
        trainingViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(trainingViewModel.IsRefreshReadinessEnabled, "training refresh should be enabled while idle");
        AssertTrue(trainingViewModel.IsStartTrainingEnabled, "training start should be enabled while idle");
        AssertTrue(!trainingViewModel.IsStopTrainingEnabled, "training stop should be disabled while idle");
        trainingViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: true,
            isTrainingStopAvailable: true,
            hasCurrentRecipeName: true));
        AssertTrue(!trainingViewModel.IsRefreshReadinessEnabled, "training refresh should disable while training is running");
        AssertTrue(!trainingViewModel.IsStartTrainingEnabled, "training start should disable while training is running");
        AssertTrue(trainingViewModel.IsStopTrainingEnabled, "training stop should enable while training is running");

        var shellViewModel = new WpfLabelingShellViewModel();
        var candidateReviewViewModel = new WpfCandidateReviewPanelViewModel();
        AssertEqual(System.Windows.Visibility.Collapsed, candidateReviewViewModel.ComparisonVisibility);
        candidateReviewViewModel.SetComparison(new WpfCandidateComparisonPresentation("AI OK\n10x10 @ 1,2", "수동 OK\n10x10 @ 1,2", "중복\n100%", true));
        AssertEqual(System.Windows.Visibility.Visible, candidateReviewViewModel.ComparisonVisibility);
        AssertTrue(candidateReviewViewModel.IsComparisonHighOverlap, "candidate comparison should expose duplicate/high-overlap state");
        AssertTrue(candidateReviewViewModel.ComparisonCandidateText.Contains("AI OK", StringComparison.Ordinal), "candidate comparison should keep AI text");
        AssertTrue(candidateReviewViewModel.ComparisonCurrentText.Contains("수동 OK", StringComparison.Ordinal), "candidate comparison should keep current-label text");
        AssertTrue(candidateReviewViewModel.ComparisonOverlapText.Contains("100", StringComparison.Ordinal), "candidate comparison should keep overlap text");
        candidateReviewViewModel.ClearComparison();
        AssertEqual(System.Windows.Visibility.Collapsed, candidateReviewViewModel.ComparisonVisibility);
        AssertTrue(!candidateReviewViewModel.IsComparisonHighOverlap, "candidate comparison clear should reset high-overlap state");
        candidateReviewViewModel.ApplySelectionReview(
            "Selected detail",
            new WpfCandidateComparisonPresentation("AI NG", "Manual NG", "IoU\n25%", false),
            showComparison: true);
        AssertEqual("Selected detail", candidateReviewViewModel.DetailText);
        AssertEqual(System.Windows.Visibility.Visible, candidateReviewViewModel.ComparisonVisibility);
        AssertTrue(candidateReviewViewModel.ComparisonCandidateText.Contains("AI NG", StringComparison.Ordinal), "candidate selection review should update comparison text with detail");
        candidateReviewViewModel.ApplySelectionReview("No selection", default, showComparison: false);
        AssertEqual("No selection", candidateReviewViewModel.DetailText);
        AssertEqual(System.Windows.Visibility.Collapsed, candidateReviewViewModel.ComparisonVisibility);
        candidateReviewViewModel.SetNavigationState(previousEnabled: true, nextEnabled: true, focusEnabled: true);
        AssertTrue(candidateReviewViewModel.IsPreviousCandidateEnabled, "candidate previous navigation should be exposed independently from skip");
        AssertTrue(candidateReviewViewModel.IsNextCandidateEnabled, "candidate next navigation should be exposed independently from skip");
        AssertTrue(candidateReviewViewModel.IsFocusCandidateEnabled, "candidate focus should be exposed independently from skip");
        candidateReviewViewModel.SetNavigationState(previousEnabled: false, nextEnabled: false, focusEnabled: true);
        AssertTrue(!candidateReviewViewModel.IsPreviousCandidateEnabled, "candidate previous navigation should disable when there is only one visible candidate");
        AssertTrue(!candidateReviewViewModel.IsNextCandidateEnabled, "candidate next navigation should disable when there is only one visible candidate");
        AssertTrue(candidateReviewViewModel.IsFocusCandidateEnabled, "candidate focus can remain enabled for a single selected candidate");
        int candidateCollectionChangedCount = 0;
        NotifyCollectionChangedAction candidateCollectionAction = NotifyCollectionChangedAction.Add;
        candidateReviewViewModel.Candidates.CollectionChanged += (_, e) =>
        {
            candidateCollectionChangedCount++;
            candidateCollectionAction = e.Action;
        };
        candidateReviewViewModel.SetCandidates(
            Enumerable.Range(0, 10000).Select(index => WpfCandidateReviewListItem.Empty($"candidate {index}", string.Empty)),
            "10000 candidates");
        AssertEqual(1, candidateCollectionChangedCount);
        AssertEqual(NotifyCollectionChangedAction.Reset, candidateCollectionAction);
        AssertEqual(10000, candidateReviewViewModel.Candidates.Count);
        var objectReviewViewModel = new WpfObjectReviewPanelViewModel();
        objectReviewViewModel.SetObjects(new[]
        {
            new WpfObjectReviewListItem("1. manual", "class", WpfObjectReviewSource.ManualRoi.ToString(), 0, WpfObjectReviewItemRef.Manual(0))
        }, "1 object");
        objectReviewViewModel.SetSelectedObjectClass(new[] { "Defect", "Scratch" }, "scratch");
        AssertEqual("Scratch", objectReviewViewModel.SelectedClassName);
        AssertTrue(objectReviewViewModel.IsApplyClassEnabled, "object review class apply should enable through the view model after selecting an object and class");
        objectReviewViewModel.SetSelectedObjectClass(new[] { "Defect", "Scratch" }, "");
        AssertEqual("Defect", objectReviewViewModel.SelectedClassName);
        int objectCollectionChangedCount = 0;
        NotifyCollectionChangedAction objectCollectionAction = NotifyCollectionChangedAction.Add;
        objectReviewViewModel.Objects.CollectionChanged += (_, e) =>
        {
            objectCollectionChangedCount++;
            objectCollectionAction = e.Action;
        };
        objectReviewViewModel.SetObjects(
            Enumerable.Range(0, 100000).Select(index => new WpfObjectReviewListItem(
                $"{index + 1}. manual",
                "class",
                WpfObjectReviewSource.ManualRoi.ToString(),
                index,
                WpfObjectReviewItemRef.Manual(index))),
            "100000 objects");
        AssertEqual(1, objectCollectionChangedCount);
        AssertEqual(NotifyCollectionChangedAction.Reset, objectCollectionAction);
        AssertEqual(100000, objectReviewViewModel.Objects.Count);
        objectCollectionChangedCount = 0;
        objectCollectionAction = NotifyCollectionChangedAction.Reset;
        var replacementObject = new WpfObjectReviewListItem(
            "50001. manual / moved",
            "class",
            WpfObjectReviewSource.ManualRoi.ToString(),
            50000,
            WpfObjectReviewItemRef.Manual(50000));
        Stopwatch objectReplaceStopwatch = Stopwatch.StartNew();
        AssertTrue(objectReviewViewModel.TryReplaceObject(50000, replacementObject, select: true), "object review single-row replacement should succeed in a large list");
        objectReplaceStopwatch.Stop();
        AssertEqual(1, objectCollectionChangedCount);
        AssertEqual(NotifyCollectionChangedAction.Replace, objectCollectionAction);
        AssertTrue(ReferenceEquals(replacementObject, objectReviewViewModel.SelectedObject), "single-row replacement should keep the edited ROI selected");
        AssertTrue(objectReplaceStopwatch.Elapsed.TotalMilliseconds < 20.0, "single ROI edit should replace one object-review row without rebuilding the full side list");
        objectCollectionChangedCount = 0;
        objectCollectionAction = NotifyCollectionChangedAction.Reset;
        Stopwatch objectRemoveStopwatch = Stopwatch.StartNew();
        AssertTrue(objectReviewViewModel.TryRemoveObject(50000, "99999 objects", 50000), "object review single-row removal should succeed in a large list");
        objectRemoveStopwatch.Stop();
        AssertEqual(1, objectCollectionChangedCount);
        AssertEqual(NotifyCollectionChangedAction.Remove, objectCollectionAction);
        AssertEqual(99999, objectReviewViewModel.Objects.Count);
        AssertTrue(objectReviewViewModel.SelectedObject?.IsEnabled == true, "single-row removal should keep a neighboring object selected");
        AssertTrue(objectRemoveStopwatch.Elapsed.TotalMilliseconds < 20.0, "single ROI delete should remove one object-review row without resetting the full side list");
        var queueViewModel = new WpfImageQueuePanelViewModel();
        AssertTrue(shellViewModel.IsLabelingModeActive, "shell should start in labeling mode");
        AssertTrue(!shellViewModel.IsInferenceModeActive, "shell inference mode should start inactive");
        AssertTrue(shellViewModel.IsLabelingModeButtonEnabled, "active labeling mode button should start enabled");
        AssertTrue(shellViewModel.IsInferenceModeButtonEnabled, "inactive inference mode should be switchable when idle");
        shellViewModel.SetWorkflowModeState(isInferenceMode: true, canSwitchMode: false);
        AssertTrue(!shellViewModel.IsLabelingModeActive, "labeling mode should become inactive after switching to inference");
        AssertTrue(shellViewModel.IsInferenceModeActive, "inference mode should become active");
        AssertTrue(!shellViewModel.IsLabelingModeButtonEnabled, "inactive labeling button should lock while detection is busy");
        AssertTrue(shellViewModel.IsInferenceModeButtonEnabled, "active inference button should stay enabled while detection is busy");
        shellViewModel.SetWorkflowModeState(isInferenceMode: false, canSwitchMode: false);
        AssertTrue(shellViewModel.IsLabelingModeActive, "labeling mode should become active again");
        AssertTrue(shellViewModel.IsLabelingModeButtonEnabled, "active labeling button should stay enabled while busy");
        AssertTrue(!shellViewModel.IsInferenceModeButtonEnabled, "inactive inference button should lock while labeling-side work is busy");
        shellViewModel.SetWorkflowModeState(isInferenceMode: false, canSwitchMode: true);
        AssertTrue(shellViewModel.IsInferenceModeButtonEnabled, "inactive inference button should be switchable when idle");
        AssertTrue(!queueViewModel.IsOpenSelectedImageEnabled, "queue selected image open should start disabled");
        queueViewModel.SetSelectedImageAvailability(true);
        AssertTrue(queueViewModel.IsOpenSelectedImageEnabled, "queue selected image open should enable when a valid image row is selected");
        queueViewModel.SetSelectedImageAvailability(false);
        AssertTrue(!queueViewModel.IsOpenSelectedImageEnabled, "queue selected image open should disable when selection is cleared or invalid");
        queueViewModel.SetQuickFilterState(WpfImageQueueFilter.Candidate, 2, 1, 3, 4, 5);
        AssertEqual("\uC804\uCCB4", queueViewModel.QueueFilterAllText);
        AssertEqual("\uD6C4\uBCF4 2", queueViewModel.QueueFilterCandidateText);
        AssertEqual("\uC2E4\uD328 1", queueViewModel.QueueFilterFailedText);
        AssertEqual("\uD655\uC815 3", queueViewModel.QueueFilterConfirmedText);
        AssertEqual("\uC2A4\uD0B5 4", queueViewModel.QueueFilterSkippedText);
        AssertEqual("\uC5C6\uC74C 5", queueViewModel.QueueFilterNoCandidateText);
        AssertTrue(!queueViewModel.IsQueueFilterAllActive, "queue all quick filter should become inactive when candidate filter is selected");
        AssertTrue(queueViewModel.IsQueueFilterCandidateActive, "queue candidate quick filter should become active when selected");
        AssertTrue(!queueViewModel.IsQueueFilterFailedActive, "queue failed quick filter should stay inactive when candidate filter is selected");
        shellViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: false,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        queueViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: false,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(!shellViewModel.IsCurrentImageDetectionEnabled, "shell detect should stay disabled in labeling mode");
        AssertTrue(!queueViewModel.IsDetectSelectedEnabled, "queue selected detect should stay disabled in labeling mode");
        AssertTrue(!queueViewModel.IsBatchDetectEnabled, "queue batch detect should stay disabled in labeling mode");
        AssertTrue(!queueViewModel.IsRetryFailedEnabled, "queue retry should stay disabled in labeling mode");
        AssertTrue(!queueViewModel.IsStopBatchEnabled, "queue stop should stay disabled while batch detection is idle");

        shellViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        queueViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(shellViewModel.IsCurrentImageDetectionEnabled, "shell detect should enable in idle inference mode");
        AssertTrue(queueViewModel.IsDetectSelectedEnabled, "queue selected detect should enable in idle inference mode");
        AssertTrue(queueViewModel.IsBatchDetectEnabled, "queue batch detect should enable in idle inference mode");
        AssertTrue(queueViewModel.IsRetryFailedEnabled, "queue retry should enable in idle inference mode");

        queueViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: true,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(!queueViewModel.IsBatchDetectEnabled, "queue batch detect should disable while batch detection is running");
        AssertTrue(queueViewModel.IsStopBatchEnabled, "queue stop should enable while batch detection is running");

        var canvasViewModel = new WpfCanvasPanelViewModel();
        canvasViewModel.SetCommandAvailability(hasImage: false, hasSelectedCandidate: false, hasPendingCandidates: false);
        AssertTrue(!canvasViewModel.IsFitEnabled, "canvas fit should stay disabled without an image");
        AssertTrue(!canvasViewModel.IsActualSizeEnabled, "canvas actual-size should stay disabled without an image");
        AssertTrue(!canvasViewModel.IsPanEnabled, "canvas pan should stay disabled without an image");
        AssertTrue(!canvasViewModel.IsFocusCandidateEnabled, "canvas candidate focus should stay disabled without an image");
        AssertTrue(!canvasViewModel.IsResetAiOverlayEnabled, "canvas AI reset should stay disabled without an image");
        canvasViewModel.SetCommandAvailability(hasImage: true, hasSelectedCandidate: false, hasPendingCandidates: true);
        AssertTrue(canvasViewModel.IsFitEnabled, "canvas fit should enable when an image is loaded");
        AssertTrue(canvasViewModel.IsActualSizeEnabled, "canvas actual-size should enable when an image is loaded");
        AssertTrue(canvasViewModel.IsPanEnabled, "canvas pan should enable when an image is loaded");
        AssertTrue(!canvasViewModel.IsFocusCandidateEnabled, "canvas candidate focus should require a selected candidate");
        AssertTrue(canvasViewModel.IsResetAiOverlayEnabled, "canvas AI reset should enable when pending candidates exist");
        canvasViewModel.SetCommandAvailability(hasImage: true, hasSelectedCandidate: true, hasPendingCandidates: false);
        AssertTrue(canvasViewModel.IsFocusCandidateEnabled, "canvas candidate focus should enable for a selected candidate");
        AssertTrue(!canvasViewModel.IsResetAiOverlayEnabled, "canvas AI reset should disable when no pending candidates exist");

        var projectViewModel = new WpfProjectConfigPanelViewModel();
        projectViewModel.LoadFrom("MainRecipe", @"C:\App\RECIPE");
        AssertEqual("MainRecipe", projectViewModel.RecipeName);
        AssertEqual(@"C:\App\RECIPE\MainRecipe\VISION.xml", projectViewModel.ConfigPath);
        projectViewModel.SetRecipeList(new[] { "Beta", "Alpha" }, "Beta");
        AssertEqual(2, projectViewModel.RecipeNames.Count);
        AssertEqual("Beta", projectViewModel.SelectedRecipeName);
        projectViewModel.SelectRecipeFromList("Alpha");
        AssertEqual("Alpha", projectViewModel.RecipeName);
        AssertEqual(@"C:\App\RECIPE\Alpha\VISION.xml", projectViewModel.ConfigPath);
        projectViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(projectViewModel.IsApplyRecipeEnabled, "project apply should be enabled while idle");
        AssertTrue(projectViewModel.IsRefreshRecipeListEnabled, "project recipe refresh should be enabled while idle");
        AssertTrue(projectViewModel.IsSaveProjectConfigEnabled, "project config save should be enabled when a recipe is active");
        AssertTrue(projectViewModel.IsOpenProjectConfigFolderEnabled, "project config folder should be enabled while idle");
        projectViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: true,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(!projectViewModel.IsApplyRecipeEnabled, "project apply should disable while busy");
        AssertTrue(!projectViewModel.IsRefreshRecipeListEnabled, "project recipe refresh should disable while busy");
        AssertTrue(!projectViewModel.IsSaveProjectConfigEnabled, "project config save should disable while busy");
        AssertTrue(!projectViewModel.IsOpenProjectConfigFolderEnabled, "project config folder should disable while busy");

        var classViewModel = new WpfClassCatalogPanelViewModel();
        classViewModel.LoadOutputRoot(@"C:\Dataset\Output");
        classViewModel.SetClasses(new[]
        {
            new CClassItem { Text = "OK", DrawColor = Color.Green },
            new CClassItem { Text = "NG", DrawColor = Color.Red }
        }, "NG");
        AssertEqual(@"C:\Dataset\Output", classViewModel.OutputRootPath);
        AssertEqual(2, classViewModel.Classes.Count);
        AssertEqual("NG", classViewModel.SelectedClass.Text);
        AssertEqual("NG", classViewModel.ClassName);
        classViewModel.SelectClass("OK");
        AssertEqual("OK", classViewModel.SelectedClass.Text);
        AssertEqual("OK", classViewModel.ClassName);
        classViewModel.ClearClassName();
        AssertEqual(string.Empty, classViewModel.ClassName);
        classViewModel.StatusText = "Ready";
        AssertEqual("Ready", classViewModel.StatusText);

        var yoloStatusViewModel = new WpfYoloStatusPanelViewModel();
        yoloStatusViewModel.SetSettingsStatus("Ready", "Python: OK");
        AssertEqual("Ready", yoloStatusViewModel.SummaryText);
        AssertEqual("Python: OK", yoloStatusViewModel.DetailText);
        yoloStatusViewModel.SetCommandStatus("Running", isBusy: true);
        AssertEqual("Running", yoloStatusViewModel.CommandStatusText);
        AssertEqual(System.Windows.Visibility.Visible, yoloStatusViewModel.CommandProgressVisibility);
        AssertTrue(yoloStatusViewModel.CommandProgressIsIndeterminate, "YOLO command progress should be indeterminate while busy");
        yoloStatusViewModel.SetCommandStatus("", isBusy: false);
        AssertEqual(System.Windows.Visibility.Collapsed, yoloStatusViewModel.CommandProgressVisibility);
        AssertTrue(!yoloStatusViewModel.CommandProgressIsIndeterminate, "YOLO command progress should stop when idle");
        AssertEqual(0D, yoloStatusViewModel.CommandProgressValue);
        yoloStatusViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: true,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(!yoloStatusViewModel.IsFirstCheckEnabled, "YOLO first-check should disable through the status ViewModel while busy");
        AssertTrue(!yoloStatusViewModel.IsInstallRequirementsEnabled, "YOLO install should disable through the status ViewModel while busy");
        AssertTrue(!yoloStatusViewModel.IsRunSmokeEnabled, "YOLO smoke should disable through the status ViewModel while busy");
        AssertTrue(!yoloStatusViewModel.IsRestartWorkerEnabled, "YOLO restart should disable through the status ViewModel while busy");
        AssertTrue(!yoloStatusViewModel.IsStopWorkerEnabled, "YOLO stop-worker should disable through the status ViewModel while busy");
        yoloStatusViewModel.ApplyWorkflowCommandState(WpfWorkflowCommandStateService.Build(
            isInferenceMode: true,
            isYoloEnvironmentCommandRunning: false,
            isDetecting: false,
            isBatchDetectionRunning: false,
            isTrainingCommandRunning: false,
            isTrainingStopAvailable: false,
            hasCurrentRecipeName: true));
        AssertTrue(yoloStatusViewModel.IsFirstCheckEnabled, "YOLO first-check should re-enable through the status ViewModel when idle");
        AssertTrue(projectViewModel.StatusText.Contains("적용을 누르세요", StringComparison.Ordinal), "recipe selection should guide the operator to apply explicitly");
        AssertEqual(@"C:\App\RECIPE\(recipe 선택 필요)\VISION.xml", WpfProjectRecipeService.BuildConfigPreviewPath(@"C:\App\RECIPE", string.Empty));
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(method != null, $"private method was not found: {methodName}");
        try
        {
            method.Invoke(instance, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static T InvokePrivateResult<T>(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(method != null, $"private method was not found: {methodName}");
        try
        {
            object result = method.Invoke(instance, args);
            return result is T typed ? typed : default;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static bool InvokeDetectionOverlayHitTest(object viewModel, object[] args)
    {
        MethodInfo method = viewModel.GetType().GetMethod("TryGetDetectionOverlayIndexAtPosition", BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(method != null, "private detection overlay hit-test method was not found");
        try
        {
            object result = method.Invoke(viewModel, args);
            return result is bool hit && hit;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static T InvokePrivateStaticResult<T>(Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        AssertTrue(method != null, $"private static method was not found: {methodName}");
        try
        {
            object result = method.Invoke(null, args);
            return result is T typed ? typed : default;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static object GetNestedEnumValue<TDeclaringType>(string enumName, string valueName)
    {
        Type enumType = typeof(TDeclaringType).GetNestedType(enumName, BindingFlags.NonPublic);
        AssertTrue(enumType != null && enumType.IsEnum, $"private nested enum was not found: {enumName}");
        return Enum.Parse(enumType, valueName);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(field != null, $"private field was not found: {fieldName}");
        object value = field.GetValue(instance);
        if (value == null)
        {
            return default;
        }

        AssertTrue(value is T, $"private field had unexpected type: {fieldName}");
        return (T)value;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(field != null, $"private field was not found: {fieldName}");
        field.SetValue(instance, value);
    }

    private static bool ContainsControlText(Control root, string text)
    {
        if (root == null)
        {
            return false;
        }

        foreach (Control control in root.Controls)
        {
            if (string.Equals(control.Text, text, StringComparison.Ordinal))
            {
                return true;
            }

            if (ContainsControlText(control, text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsControlType<T>(Control root)
        where T : Control
    {
        if (root == null)
        {
            return false;
        }

        foreach (Control control in root.Controls)
        {
            if (control is T)
            {
                return true;
            }

            if (ContainsControlType<T>(control))
            {
                return true;
            }
        }

        return false;
    }

    private static void TestWpfImageQueueLoadsSupportedFiles()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            using (Bitmap image = new Bitmap(4, 3))
            {
                image.Save(Path.Combine(root, "b.PNG"), System.Drawing.Imaging.ImageFormat.Png);
                image.Save(Path.Combine(root, "a.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(Path.Combine(root, "c.tiff"), System.Drawing.Imaging.ImageFormat.Tiff);
            }

            File.WriteAllText(Path.Combine(root, "ignore.txt"), "not an image");

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                int count = window.LoadImageQueueFromRoot(root, loadFirstImage: false, refreshDetails: false);

                AssertEqual(3, count);
                AssertEqual(3, window.ImageQueueItems.Count);
                AssertEqual("a.jpg", window.ImageQueueItems[0].FileName);
                AssertEqual("b.PNG", window.ImageQueueItems[1].FileName);
                AssertEqual("c.tiff", window.ImageQueueItems[2].FileName);

                AssertTrue(window.TryLoadImage(Path.Combine(root, "a.jpg")), "WPF shell did not load the selected queue image");
                AssertEqual(System.Drawing.Imaging.PixelFormat.Format24bppRgb, CGlobal.Inst.ImageWorkspace.ActiveImage.PixelFormat);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfImageQueueDetailLoader()
    {
        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "detail-loader.png");
            using (Bitmap image = new Bitmap(37, 19))
            {
                image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            AssertTrue(
                WpfImageQueueDetailLoader.TryReadImageSize(imagePath, out Size imageSize, out string error),
                $"WPF image queue detail loader did not read image size: {error}");
            AssertEqual(new Size(37, 19), imageSize);
            AssertEqual("37x19", WpfImageQueueDetailLoader.FormatImageSize(imageSize));
            AssertEqual(string.Empty, WpfImageQueueDetailLoader.FormatImageSize(Size.Empty));

            var reviewStatus = new YoloImageReviewStatusService();
            WpfImageQueueDetail detail = WpfImageQueueDetailLoader.Build(imagePath, reviewStatus, new CData());
            AssertEqual(new Size(37, 19), detail.ImageSize);
            AssertTrue(detail.ReviewStatus != null, "WPF image queue detail loader did not create review status");
            AssertEqual("No Label", detail.ReviewStatus.LabelText);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfStartupImageLoadDoesNotScanEveryQueueImage()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string activePath = Path.Combine(root, "a.jpg");
            string queuedPath = Path.Combine(root, "b.jpg");
            using (Bitmap image = new Bitmap(8, 6))
            {
                image.Save(activePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(queuedPath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                AssertTrue(window.TryLoadImage(activePath, populateQueue: true, refreshQueueDetails: false), "WPF startup-style image load failed");
                AssertEqual(2, window.ImageQueueItems.Count);

                WpfImageQueueItem queuedItem = window.ImageQueueItems.First(item => string.Equals(item.ImagePath, queuedPath, StringComparison.OrdinalIgnoreCase));
                AssertEqual(string.Empty, queuedItem.Dimensions);
                AssertEqual("확인중", queuedItem.LabelStatus);
                AssertEqual("상태 확인 전", queuedItem.QueueStatusSummary);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfImageLoadReplacesPreviousViewerTextures()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string firstPath = Path.Combine(root, "first.jpg");
            string secondPath = Path.Combine(root, "second.jpg");
            using (Bitmap image = new Bitmap(16, 12))
            {
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.FromArgb(70, 90, 120));
                }

                image.Save(firstPath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            using (Bitmap image = new Bitmap(12, 16))
            {
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.FromArgb(130, 90, 70));
                }

                image.Save(secondPath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                AssertTrue(window.TryLoadImage(firstPath, populateQueue: true, refreshQueueDetails: false), "first WPF image load failed");
                AssertEqual(1, window.MainCanvasViewModel.LoadedTextureGroupCount);

                AssertTrue(window.TryLoadImage(secondPath, populateQueue: false, refreshQueueDetails: false), "second WPF image load failed");
                AssertEqual(1, window.MainCanvasViewModel.LoadedTextureGroupCount);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfImageQueueClickUsesLightweightLoadPath()
    {
        string sourcePath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs");
        string source = File.ReadAllText(sourcePath);

        AssertTrue(source.Contains("TryOpenSelectedQueueImage(skipIfAlreadyActive: true)", StringComparison.Ordinal), "queue selection should load the clicked image");
        AssertTrue(source.Contains("populateQueue: false", StringComparison.Ordinal), "queue selection should not rebuild the image queue");
        AssertTrue(source.Contains("refreshQueueDetails: false", StringComparison.Ordinal), "queue selection should not restart queue detail loading");
        AssertTrue(source.Contains("refreshActiveStatus: false", StringComparison.Ordinal), "queue selection should not save review status while only switching the canvas image");
        AssertTrue(source.Contains("appendLoadLog: false", StringComparison.Ordinal), "queue selection should not append a log row for every click");
        AssertTrue(source.Contains("if (!appendLoadLog)", StringComparison.Ordinal), "lightweight image loads should skip log-panel refresh work");
        AssertTrue(source.Contains("CDisplayManager.ImageSrc = imageMat;", StringComparison.Ordinal), "image loading should transfer the decoded Mat to the display manager without cloning");
        AssertTrue(source.Contains("imageMat = null;", StringComparison.Ordinal), "image loading should clear local Mat ownership after handing it to the display manager");
        AssertTrue(!source.Contains("CDisplayManager.ImageSrc = imageMat.Clone();", StringComparison.Ordinal), "image loading should not duplicate the decoded Mat after OpenGL upload");

        string loaderSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OpenVisionLab", "Library", "OpenVisionLab.ImageCanvas", "Util", "CanvasImageLoader.cs"));
        AssertTrue(loaderSource.Contains("TryReplaceSingleTexture", StringComparison.Ordinal), "small same-size queue images should reuse the OpenGL texture instead of deleting and recreating it");
        AssertTrue(loaderSource.Contains("UseContinuousMatForTextureUpload", StringComparison.Ordinal), "OpenGL texture upload should centralize clone decisions");
        AssertTrue(loaderSource.Contains("mat.IsContinuous()", StringComparison.Ordinal), "continuous OpenCV Mats should upload without a defensive clone");
    }

    private static void TestWpfImageQueuePreloadsAdjacentDecodes()
    {
        string sourcePath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs");
        string source = File.ReadAllText(sourcePath);

        AssertTrue(source.Contains("ImageDecodeCacheCapacity = 8", StringComparison.Ordinal), "WPF image decode cache should stay bounded");
        AssertTrue(source.Contains("ImageDecodeCacheMaxPixels", StringComparison.Ordinal), "WPF image decode cache should skip very large images");
        AssertTrue(source.Contains("ImageDecodeCacheMaxBytes", StringComparison.Ordinal), "WPF image decode cache should have a total memory budget");
        AssertTrue(source.Contains("imageDecodeCacheBytes", StringComparison.Ordinal), "WPF image decode cache should track estimated retained bytes");
        AssertTrue(source.Contains("GetImageDecodeCacheDiagnostics", StringComparison.Ordinal), "WPF image decode cache should expose diagnostics for performance smoke checks");
        AssertTrue(source.Contains("imageDecodeCacheHits", StringComparison.Ordinal), "WPF image decode cache should track cache hits");
        AssertTrue(source.Contains("imageDecodeCacheMisses", StringComparison.Ordinal), "WPF image decode cache should track cache misses");
        AssertTrue(source.Contains("imageDecodeCacheEvictions", StringComparison.Ordinal), "WPF image decode cache should track memory-budget evictions");
        AssertTrue(source.Contains("LastImageLoadDiagnostics", StringComparison.Ordinal), "WPF image loading should expose the latest step-level diagnostics for perf smoke checks");
        AssertTrue(source.Contains("RecordImageLoadDiagnostics", StringComparison.Ordinal), "WPF image loading should record step-level timings");
        AssertTrue(source.Contains("TakeElapsedMilliseconds", StringComparison.Ordinal), "WPF image loading should measure elapsed steps without extra allocations");
        AssertTrue(source.Contains("loaded.Width * loaded.Height > ImageDecodeCacheMaxPixels", StringComparison.Ordinal), "WPF image decode cache should check pixel count before storing Bitmap/Mat pairs");
        AssertTrue(source.Contains("imageDecodeCacheBytes > ImageDecodeCacheMaxBytes", StringComparison.Ordinal), "WPF image decode cache should evict entries when the memory budget is exceeded");
        AssertTrue(source.Contains("PreloadAdjacentQueueImages(imagePath)", StringComparison.Ordinal), "successful image loads should queue adjacent image preloads");
        AssertTrue(source.Contains("Task.Run", StringComparison.Ordinal), "adjacent image decode should run off the UI thread");
        AssertTrue(source.Contains("TryTakeCachedDecodedImage", StringComparison.Ordinal), "image load should use prepared adjacent decodes");
        AssertTrue(source.Contains("StoreCachedDecodedImage", StringComparison.Ordinal), "background decode should store reusable image data");
        AssertTrue(source.Contains("WaitForImageDecodePreload()", StringComparison.Ordinal), "window close should wait briefly for preload file handles to be released");
        AssertTrue(source.Contains("ClearImageDecodeCache()", StringComparison.Ordinal), "cached Bitmap/Mat resources should be disposed on close");

        string testsSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tests", "LabelingApplication.Tests", "Program.cs"));
        AssertTrue(testsSource.Contains("--folder", StringComparison.Ordinal), "WPF queue click performance smoke should accept a real image folder");
        AssertTrue(testsSource.Contains("WorkingSet64", StringComparison.Ordinal), "WPF queue click performance smoke should report process working set");
        AssertTrue(testsSource.Contains("firstSwitchElapsed", StringComparison.Ordinal), "WPF queue click performance smoke should split the first cold switch from warm switches");
        AssertTrue(testsSource.Contains("metric=visible", StringComparison.Ordinal), "WPF queue click performance smoke should treat visible image update separately from full dispatcher settling");
        AssertTrue(testsSource.Contains("WPF queue warm perf:", StringComparison.Ordinal), "WPF queue click performance smoke should report warm-switch latency separately");
        AssertTrue(testsSource.Contains("WPF queue settled perf:", StringComparison.Ordinal), "WPF queue click performance smoke should report full dispatcher-settled latency separately");
        AssertTrue(testsSource.Contains("WPF queue first switch steps", StringComparison.Ordinal), "WPF queue click performance smoke should report first-switch step timings");
        AssertTrue(testsSource.Contains("WPF queue slow warm steps", StringComparison.Ordinal), "WPF queue click performance smoke should report slow warm-switch step timings");
        AssertTrue(testsSource.Contains("WPF queue image commit perf:", StringComparison.Ordinal), "WPF queue click performance smoke should report image-commit timing separately from dispatcher settling");
        AssertTrue(testsSource.Contains("selection-set", StringComparison.Ordinal), "WPF queue click performance smoke should report DataGrid selection assignment time");
        AssertTrue(testsSource.Contains("render-drain", StringComparison.Ordinal), "WPF queue click performance smoke should report render-priority dispatcher drain time");
        AssertTrue(testsSource.Contains("background-drain", StringComparison.Ordinal), "WPF queue click performance smoke should report background-priority dispatcher drain time");
        AssertTrue(testsSource.Contains("idle-drain", StringComparison.Ordinal), "WPF queue click performance smoke should report idle-priority dispatcher drain time");
        AssertTrue(testsSource.Contains("WPF decode cache:", StringComparison.Ordinal), "WPF queue click performance smoke should report decode cache diagnostics");
    }

    private static void TestWpfImageQueueClickLoadsCanvas()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string firstPath = Path.Combine(root, "first.jpg");
            string secondPath = Path.Combine(root, "second.jpg");
            using (Bitmap image = new Bitmap(10, 8))
            {
                image.Save(firstPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                image.Save(secondPath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                AssertTrue(window.TryLoadImage(firstPath, populateQueue: true, refreshQueueDetails: false), "WPF initial image load failed");
                var grid = (System.Windows.Controls.DataGrid)window.FindName("ImageQueueGrid");
                var queuePanel = (WpfImageQueuePanel)window.FindName("ImageQueuePanelControl");
                var openSelectedButton = (System.Windows.Controls.Control)window.FindName("OpenSelectedQueueImageButton");
                WpfImageQueueItem secondItem = window.ImageQueueItems.First(item => string.Equals(item.ImagePath, secondPath, StringComparison.OrdinalIgnoreCase));

                grid.SelectedItem = secondItem;
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));

                AssertEqual(secondPath, GetPrivateField<string>(window, "activeImagePath"));
                AssertEqual("second", CGlobal.Inst.Data.LastSelectImageName);
                AssertTrue(queuePanel.ViewModel.IsOpenSelectedImageEnabled, "queue selected open ViewModel state should enable after selecting a valid image");
                AssertTrue(openSelectedButton.IsEnabled, "queue selected open button should enable through binding after selecting a valid image");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfDetectionCandidatesRenderAsDetectionOverlays()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "detect.jpg");
            using (Bitmap image = new Bitmap(80, 60))
            {
                image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                AssertTrue(window.TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false), "WPF image load failed");
                var candidates = new List<YoloWorkerSmokeCandidate>
                {
                    new YoloWorkerSmokeCandidate
                    {
                        Index = 1,
                        ClassName = "OK",
                        Confidence = 0.96,
                        X = 10,
                        Y = 12,
                        Width = 20,
                        Height = 18
                    }
                };

                InvokePrivateResult<object>(window, "ApplyDetectionCandidates", candidates, true);

                AssertEqual(1, window.MainCanvasViewModel.DetectionOverlays.Count);
                AssertTrue(window.MainCanvasViewModel.DetectionOverlays[0].Label.Contains("AI 1 OK", StringComparison.Ordinal), "Detection overlay should show AI label text");
                AssertTrue(window.MainCanvasViewModel.DetectionOverlays[0].IsSelected, "Selected candidate should be highlighted on the detection overlay");
                string shellSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs"));
                AssertTrue(shellSource.Contains("MainCanvasViewModel.ImageViewer.ZoomToFit();", StringComparison.Ordinal), "WPF inference should center the image after applying detection candidates");
                AssertTrue(shellSource.Contains("CenterCanvasAfterInferenceResult", StringComparison.Ordinal), "WPF inference should centralize the post-result centering logic");
                AssertTrue(shellSource.Contains("DispatcherPriority.Render", StringComparison.Ordinal), "WPF inference centering should run after the result overlay renders");
                AssertTrue(shellSource.Contains("DispatcherPriority.ApplicationIdle", StringComparison.Ordinal), "WPF inference centering should settle after layout idle");
                var resultOverlay = (System.Windows.Controls.Border)window.FindName("DetectionResultOverlay");
                var summary = (System.Windows.Controls.TextBlock)window.FindName("DetectionOverlaySummaryText");
                var selected = (System.Windows.Controls.TextBlock)window.FindName("DetectionOverlaySelectedText");
                var canvasPanel = (WpfCanvasPanel)window.FindName("CanvasPanelControl");
                AssertEqual(System.Windows.Visibility.Visible, canvasPanel.ViewModel.DetectionOverlayVisibility);
                AssertTrue(canvasPanel.ViewModel.DetectionOverlaySummaryText.Contains("1", StringComparison.Ordinal), "Detection overlay ViewModel summary should show candidate count");
                AssertTrue(canvasPanel.ViewModel.DetectionOverlaySelectedText.Contains("AI 1 OK", StringComparison.Ordinal), "Detection overlay ViewModel should show selected candidate details");
                AssertEqual(WpfDetectionOverlayStatus.Confirmable.ToString(), canvasPanel.ViewModel.DetectionOverlayStatusKey);
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertEqual(System.Windows.Visibility.Visible, resultOverlay.Visibility);
                AssertTrue(summary.Text.Contains("1", StringComparison.Ordinal), "Detection overlay summary should show candidate count");
                AssertTrue(selected.Text.Contains("AI 1 OK", StringComparison.Ordinal), "Detection overlay should show selected candidate details");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfBatchDetectionResultDisplaysOnCanvas()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "batch-detect.jpg");
            using (Bitmap image = new Bitmap(80, 60))
            {
                image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                AssertTrue(window.TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false), "WPF batch image load failed");
                WpfImageQueueItem item = WpfImageQueueItem.CreateShell(imagePath);
                var result = new YoloWorkerSmokeTestResult
                {
                    Succeeded = true,
                    ImagePath = imagePath,
                    CandidateCount = 1,
                    Candidates = new[]
                    {
                        new YoloWorkerSmokeCandidate
                        {
                            Index = 1,
                            ClassName = "OK",
                            Confidence = 0.97,
                            X = 10,
                            Y = 12,
                            Width = 20,
                            Height = 18
                        }
                    }
                };

                AssertTrue(InvokePrivateResult<bool>(window, "ApplyBatchDetectionResultToCanvas", item, result), "batch result should apply to the active canvas");
                AssertEqual(1, window.MainCanvasViewModel.DetectionOverlays.Count);
                AssertTrue(window.MainCanvasViewModel.DetectionOverlays[0].Label.Contains("AI 1 OK", StringComparison.Ordinal), "batch detection result should draw the candidate overlay");

                var canvasPanel = (WpfCanvasPanel)window.FindName("CanvasPanelControl");
                AssertEqual(System.Windows.Visibility.Visible, canvasPanel.ViewModel.DetectionOverlayVisibility);
                AssertTrue(canvasPanel.ViewModel.DetectionOverlaySelectedText.Contains("AI 1 OK", StringComparison.Ordinal), "batch detection result should show selected candidate details");

                var emptyResult = new YoloWorkerSmokeTestResult
                {
                    Succeeded = true,
                    ImagePath = imagePath,
                    CandidateCount = 0,
                    Candidates = Array.Empty<YoloWorkerSmokeCandidate>()
                };
                AssertTrue(InvokePrivateResult<bool>(window, "ApplyBatchDetectionResultToCanvas", item, emptyResult), "empty batch result should still apply to the active canvas");
                AssertEqual(0, window.MainCanvasViewModel.DetectionOverlays.Count);
                AssertEqual(System.Windows.Visibility.Visible, canvasPanel.ViewModel.DetectionOverlayVisibility);
                AssertTrue(canvasPanel.ViewModel.DetectionOverlaySummaryText.Contains("0", StringComparison.Ordinal), "empty batch result should show a zero-candidate result card");

                var failedResult = new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    ImagePath = imagePath,
                    Summary = "worker failed",
                    Candidates = Array.Empty<YoloWorkerSmokeCandidate>()
                };
                AssertTrue(InvokePrivateResult<bool>(window, "ApplyBatchDetectionResultToCanvas", item, failedResult), "failed batch result should still apply to the active canvas");
                AssertEqual(System.Windows.Visibility.Visible, canvasPanel.ViewModel.DetectionOverlayVisibility);
                AssertTrue(canvasPanel.ViewModel.DetectionOverlayTitleText.Contains("실패", StringComparison.Ordinal), "failed batch result should show a failure result card");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestWpfDetectionOverlayBadgesAvoidOverlap()
    {
        var occupied = new List<RectangleF>();
        var anchor = new RectangleF(40F, 60F, 80F, 50F);
        RectangleF first = InvokePrivateStaticResult<RectangleF>(
            typeof(OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel),
            "PlaceDetectionBadge",
            anchor,
            84F,
            20F,
            320F,
            180F,
            occupied);
        occupied.Add(first);

        RectangleF second = InvokePrivateStaticResult<RectangleF>(
            typeof(OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel),
            "PlaceDetectionBadge",
            anchor,
            84F,
            20F,
            320F,
            180F,
            occupied);

        AssertTrue(!first.IntersectsWith(second), "WPF detection overlay badges should not reuse the same screen area");
        AssertTrue(second.Top > first.Top, "A colliding detection badge should move below the first badge");
    }

    private static void TestWpfDetectionOverlaySkipsOffscreenBadges()
    {
        Type viewModelType = typeof(OpenVisionLab.ImageCanvas.ViewModels.RoiImageCanvasViewModel);
        AssertTrue(InvokePrivateStaticResult<bool>(
            viewModelType,
            "IntersectsViewport",
            new RectangleF(10F, 10F, 40F, 30F),
            320F,
            180F), "Visible detection overlay should intersect the viewport");
        AssertTrue(InvokePrivateStaticResult<bool>(
            viewModelType,
            "IntersectsViewport",
            new RectangleF(-20F, 10F, 30F, 30F),
            320F,
            180F), "Partly visible detection overlay should remain visible");
        AssertTrue(!InvokePrivateStaticResult<bool>(
            viewModelType,
            "IntersectsViewport",
            new RectangleF(-40F, 10F, 20F, 30F),
            320F,
            180F), "Fully left-offscreen detection overlay should be hidden");
        AssertTrue(!InvokePrivateStaticResult<bool>(
            viewModelType,
            "IntersectsViewport",
            new RectangleF(320F, 10F, 20F, 30F),
            320F,
            180F), "Edge-only detection overlay should not leave a floating badge");
    }

    private static void TestWpfDetectionOverlayRestoresOpenGlState()
    {
        string viewModelSource = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "OpenVisionLab",
            "Library",
            "OpenVisionLab.ImageCanvas",
            "ViewModel",
            "RoiImageCanvasViewModel.cs"));
        string textDrawingSource = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "OpenVisionLab",
            "Library",
            "OpenVisionLab.ImageCanvas",
            "OpenGL",
            "OpenGlTextDrawing.cs"));

        AssertTrue(viewModelSource.Contains("gl.PushAttrib(OpenGL.GL_ENABLE_BIT", StringComparison.Ordinal), "detection overlay should save OpenGL attrib state");
        AssertTrue(viewModelSource.Contains("finally", StringComparison.Ordinal)
            && viewModelSource.Contains("gl.PopAttrib();", StringComparison.Ordinal), "detection overlay should restore OpenGL attrib state in finally");
        AssertTrue(textDrawingSource.Contains("public static void DrawTextAt", StringComparison.Ordinal)
            && textDrawingSource.Contains("finally", StringComparison.Ordinal)
            && textDrawingSource.Contains("gl.PopMatrix();", StringComparison.Ordinal), "screen text drawing should restore matrix state in finally");
    }

    private static void TestWpfImageQueueStatusPresentation()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = CreateTempRoot();
        try
        {
            string imagePath = Path.Combine(root, "failed.jpg");
            File.WriteAllText(imagePath, string.Empty);

            var service = new YoloImageReviewStatusService();
            YoloImageReviewStatus failedStatus = service.SetDetectionFailed(imagePath, "failed", "Detection request failed.");
            AssertEqual("실패: 요청 실패", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildQueueStatusSummary", failedStatus));
            AssertEqual("AlertCircleOutline", InvokePrivateStaticResult<object>(typeof(WpfLabelingShellWindow), "GetQueueIconKind", failedStatus).ToString());
            AssertEqual("실패", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildQueueBadgeText", failedStatus));
            AssertEqual("실패: 요청 실패", WpfImageQueuePresenter.BuildStatusSummary(failedStatus));

            WpfImageQueueItem item = WpfImageQueueItem.CreateShell(imagePath);
            WpfLabelingShellWindow window = new WpfLabelingShellWindow();
            try
            {
                InvokePrivateResult<object>(window, "ApplyReviewStatusToItem", item, failedStatus);
            }
            finally
            {
                window.Close();
            }

            AssertEqual("실패: 요청 실패", item.QueueStatusSummary);
            AssertEqual("AlertCircleOutline", item.QueueIconKind.ToString());
            AssertEqual("실패", item.QueueBadgeText);
            AssertTrue(item.Detail.Contains("Detection request failed.", StringComparison.Ordinal), "WPF queue tooltip detail should include the failure reason");

            YoloImageReviewStatus candidateStatus = service.SetDetectionCandidates(imagePath, "failed", 2);
            AssertEqual("후보 2개 확인 필요", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildQueueStatusSummary", candidateStatus));
            AssertEqual("ImageSearch", InvokePrivateStaticResult<object>(typeof(WpfLabelingShellWindow), "GetQueueIconKind", candidateStatus).ToString());
            AssertEqual("후보 2", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildQueueBadgeText", candidateStatus));
            AssertEqual("후보 2", WpfImageQueuePresenter.BuildBadgeText(candidateStatus));

            var queueSummaryItems = new List<WpfImageQueueItem>
            {
                WpfImageQueueItem.CreateShell(Path.Combine(root, "candidate-a.jpg")),
                WpfImageQueueItem.CreateShell(Path.Combine(root, "candidate-b.jpg")),
                WpfImageQueueItem.CreateShell(Path.Combine(root, "failed-a.jpg")),
                WpfImageQueueItem.CreateShell(Path.Combine(root, "confirmed-a.jpg")),
                WpfImageQueueItem.CreateShell(Path.Combine(root, "skipped-a.jpg")),
                WpfImageQueueItem.CreateShell(Path.Combine(root, "empty-a.jpg"))
            };
            queueSummaryItems[0].ReviewState = YoloImageReviewState.Candidate;
            queueSummaryItems[1].ReviewState = YoloImageReviewState.Candidate;
            queueSummaryItems[2].ReviewState = YoloImageReviewState.Failed;
            queueSummaryItems[3].ReviewState = YoloImageReviewState.Confirmed;
            queueSummaryItems[3].IsLabeled = true;
            queueSummaryItems[4].ReviewState = YoloImageReviewState.Skipped;
            queueSummaryItems[5].ReviewState = YoloImageReviewState.NoCandidate;
            AssertEqual(" / 후보 2 / 실패 1 / 확정 1 / 스킵 1 / 검출없음 1", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "BuildQueueReviewCountSummary", queueSummaryItems));
            AssertEqual(2, WpfImageQueueFilterService.CountByFilter(queueSummaryItems, WpfImageQueueFilter.Candidate));
            AssertEqual(1, WpfImageQueueFilterService.CountByFilter(queueSummaryItems, WpfImageQueueFilter.Failed));
            AssertTrue(WpfImageQueueFilterService.ShouldShow(queueSummaryItems[0], "candidate", WpfImageQueueFilter.Candidate), "WPF queue filter should match candidate file by state and search text");
            AssertTrue(!WpfImageQueueFilterService.ShouldShow(queueSummaryItems[2], "candidate", WpfImageQueueFilter.Candidate), "WPF queue filter should hide rows outside the selected review state");
            AssertEqual(
                "데이터셋: 2/6 이미지 / 라벨 1 / 후보 2 / 실패 1 / 확정 1 / 스킵 1 / 검출없음 1 / 필터 후보 / 로드 3/6",
                WpfImageQueueFilterService.BuildDatasetStatusText(queueSummaryItems, 2, WpfImageQueueFilter.Candidate, 3, 6));

            string queuePanelXamlPath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfImageQueuePanel.xaml");
            string queuePanelSource = File.ReadAllText(queuePanelXamlPath);
            AssertTrue(queuePanelSource.Contains("<UniformGrid Grid.Row=\"2\" Columns=\"3\" Rows=\"2\"", StringComparison.Ordinal),
                "WPF queue quick filters should use a 3x2 layout so icons and labels are not clipped in the narrow left panel");
            AssertTrue(queuePanelSource.Contains("<Setter Property=\"MinWidth\" Value=\"84\" />", StringComparison.Ordinal),
                "WPF queue quick filter buttons should reserve enough width for icon and Korean label text");
            AssertTrue(queuePanelSource.Contains("Path=Tag", StringComparison.Ordinal),
                "WPF queue quick filter buttons should style active state from the bound button Tag");
            AssertTrue(queuePanelSource.Contains("Tag=\"{Binding IsQueueFilterCandidateActive}\"", StringComparison.Ordinal),
                "WPF queue candidate quick filter active state should bind to the image queue panel view model");
            AssertTrue(queuePanelSource.Contains("Text=\"{Binding QueueFilterCandidateText}\"", StringComparison.Ordinal),
                "WPF queue candidate quick filter text should bind to the image queue panel view model");
            string shellSourcePath = Path.Combine(FindRepositoryRoot(), "0. UI", "9) WPF", "Views", "WpfLabelingShellWindow.xaml.cs");
            string shellSource = File.ReadAllText(shellSourcePath);
            AssertTrue(shellSource.Contains("ImageQueuePanelControl?.ViewModel.SetQuickFilterState", StringComparison.Ordinal),
                "WPF shell should hand queue quick filter state to WpfImageQueuePanelViewModel");
            AssertTrue(!shellSource.Contains("ApplyQueueQuickFilterButtonState", StringComparison.Ordinal),
                "WPF shell should not directly paint queue quick filter buttons");
            AssertTrue(!shellSource.Contains("QueueFilterCandidateText.Text", StringComparison.Ordinal),
                "WPF shell should not directly write queue quick filter count labels");

            WpfLabelingShellWindow quickFilterWindow = new WpfLabelingShellWindow();
            try
            {
                var filterBox = (System.Windows.Controls.ComboBox)quickFilterWindow.FindName("ImageQueueFilterBox");
                var candidateButton = (System.Windows.Controls.Control)quickFilterWindow.FindName("QueueFilterCandidateButton");
                var failedButton = (System.Windows.Controls.Control)quickFilterWindow.FindName("QueueFilterFailedButton");
                var confirmedButton = (System.Windows.Controls.Control)quickFilterWindow.FindName("QueueFilterConfirmedButton");
                var skippedButton = (System.Windows.Controls.Control)quickFilterWindow.FindName("QueueFilterSkippedButton");
                var noCandidateButton = (System.Windows.Controls.Control)quickFilterWindow.FindName("QueueFilterNoCandidateButton");
                var candidateText = (System.Windows.Controls.TextBlock)quickFilterWindow.FindName("QueueFilterCandidateText");
                var failedText = (System.Windows.Controls.TextBlock)quickFilterWindow.FindName("QueueFilterFailedText");
                var confirmedText = (System.Windows.Controls.TextBlock)quickFilterWindow.FindName("QueueFilterConfirmedText");
                var skippedText = (System.Windows.Controls.TextBlock)quickFilterWindow.FindName("QueueFilterSkippedText");
                var noCandidateText = (System.Windows.Controls.TextBlock)quickFilterWindow.FindName("QueueFilterNoCandidateText");
                var statusText = (System.Windows.Controls.TextBlock)quickFilterWindow.FindName("DatasetStatusText");

                AssertTrue(candidateButton != null, "WPF queue candidate quick filter button was not created");
                AssertTrue(failedButton != null, "WPF queue failed quick filter button was not created");
                AssertTrue(confirmedButton != null, "WPF queue confirmed quick filter button was not created");
                AssertTrue(skippedButton != null, "WPF queue skipped quick filter button was not created");
                AssertTrue(noCandidateButton != null, "WPF queue no-candidate quick filter button was not created");
                AssertTrue(candidateText != null, "WPF queue candidate quick filter count text was not created");
                AssertTrue(failedText != null, "WPF queue failed quick filter count text was not created");
                AssertTrue(confirmedText != null, "WPF queue confirmed quick filter count text was not created");
                AssertTrue(skippedText != null, "WPF queue skipped quick filter count text was not created");
                AssertTrue(noCandidateText != null, "WPF queue no-candidate quick filter count text was not created");

                foreach (WpfImageQueueItem queueItem in queueSummaryItems)
                {
                    quickFilterWindow.ImageQueueItems.Add(queueItem);
                }

                InvokePrivateResult<object>(quickFilterWindow, "QueueFilterCandidateButton_Click", candidateButton, new System.Windows.RoutedEventArgs());
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertEqual("후보 2", candidateText.Text);
                AssertEqual("실패 1", failedText.Text);
                AssertEqual("확정 1", confirmedText.Text);
                AssertEqual("스킵 1", skippedText.Text);
                AssertEqual("없음 1", noCandidateText.Text);
                AssertTrue(Equals(true, candidateButton.Tag), "WPF queue candidate quick filter should expose active state through the bound button Tag");
                AssertTrue(!Equals(true, failedButton.Tag), "WPF queue failed quick filter should stay inactive while candidate filter is selected");
                AssertEqual(WpfImageQueueFilter.Candidate, ((WpfImageQueueFilterOption)filterBox.SelectedItem).Filter);
                AssertTrue(statusText.Text.Contains("데이터셋: 2/6 이미지", StringComparison.Ordinal), "WPF queue candidate quick filter should update visible count");
                AssertTrue(statusText.Text.Contains("필터 후보", StringComparison.Ordinal), "WPF queue candidate quick filter should update status text");

                InvokePrivateResult<object>(quickFilterWindow, "QueueFilterFailedButton_Click", failedButton, new System.Windows.RoutedEventArgs());
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertEqual(WpfImageQueueFilter.Failed, ((WpfImageQueueFilterOption)filterBox.SelectedItem).Filter);
                AssertTrue(!Equals(true, candidateButton.Tag), "WPF queue candidate quick filter should become inactive after selecting another filter");
                AssertTrue(Equals(true, failedButton.Tag), "WPF queue failed quick filter should expose active state through the bound button Tag");
                AssertTrue(statusText.Text.Contains("데이터셋: 1/6 이미지", StringComparison.Ordinal), "WPF queue failed quick filter should update visible count");
                AssertTrue(statusText.Text.Contains("필터 실패", StringComparison.Ordinal), "WPF queue failed quick filter should update status text");
                InvokePrivateResult<object>(quickFilterWindow, "QueueFilterSkippedButton_Click", skippedButton, new System.Windows.RoutedEventArgs());
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertEqual(WpfImageQueueFilter.Skipped, ((WpfImageQueueFilterOption)filterBox.SelectedItem).Filter);
                AssertTrue(statusText.Text.Contains("필터 스킵", StringComparison.Ordinal), "WPF queue skipped quick filter should update status text");
                InvokePrivateResult<object>(quickFilterWindow, "QueueFilterNoCandidateButton_Click", noCandidateButton, new System.Windows.RoutedEventArgs());
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                AssertEqual(WpfImageQueueFilter.NoCandidate, ((WpfImageQueueFilterOption)filterBox.SelectedItem).Filter);
                AssertTrue(statusText.Text.Contains("필터 검출없음", StringComparison.Ordinal), "WPF queue no-candidate quick filter should update status text");
                AssertEqual("후보 99+", InvokePrivateStaticResult<string>(typeof(WpfLabelingShellWindow), "FormatQueueQuickFilterText", "후보", 125));
            }
            finally
            {
                quickFilterWindow.Close();
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestCViewerLabelingModeChrome()
    {
        using var viewer = new CViewer();

        AssertEqual("\uBAA8\uB4DC ROI", viewer.ModeDisplayText);
        viewer.SetModeDrag();
        AssertEqual(LabelingRoiMode.Drag, viewer.CurrentMode);
        AssertEqual("\uBAA8\uB4DC \uC774\uB3D9", viewer.ModeDisplayText);
        viewer.SetModeMultiRoi();
        AssertEqual(LabelingRoiMode.Rectangle, viewer.CurrentMode);
        AssertEqual("\uBAA8\uB4DC ROI", viewer.ModeDisplayText);
        viewer.SetModeSegmentation();
        AssertEqual("\uBAA8\uB4DC \uD3F4\uB9AC\uACE4", viewer.ModeDisplayText);
        viewer.SetModeSegmentationBrush();
        AssertEqual("\uBAA8\uB4DC \uBE0C\uB7EC\uC2DC", viewer.ModeDisplayText);
        viewer.SetModeSegmentationEraser();
        AssertEqual("\uBAA8\uB4DC \uC9C0\uC6B0\uAC1C", viewer.ModeDisplayText);

        var contextMenu = GetPrivateField<ContextMenuStrip>(viewer, "imageContextMenu");
        ToolStripItem[] menuItems = contextMenu.Items.Cast<ToolStripItem>().ToArray();
        ToolStripItem readableItemImageLoad = menuItems[0];
        ToolStripItem readableItemCross = menuItems[3];

        AssertEqual("\uC774\uBBF8\uC9C0 \uC5F4\uAE30", readableItemImageLoad.Text);
        AssertTrue(readableItemImageLoad.Tag != null, "image load command should use a tag instead of text switching");
        AssertEqual("\uC2ED\uC790\uC120", readableItemCross.Text);
        AssertTrue(readableItemCross.Tag != null, "crosshair command should use a tag instead of text switching");
        AssertTrue(menuItems.All(item => item.Tag is not LabelingRoiMode), "right-click menu should not expose labeling mode commands");
        AssertTrue(!menuItems.Any(item => string.Equals(item.Text, "\uCE94\uBC84\uC2A4 \uBAA8\uB4DC", StringComparison.Ordinal)), "right-click menu should not expose a canvas mode group");
    }
    private static void TestDisplayLayerDocumentStateLifecycle()
    {
        using var image = new Bitmap(10, 10);
        using var display = new DisplayLayerDocument(image, 0, "Main");

        AssertEqual("Main", display.Text);
        AssertTrue(display.GetCurrentImage() != null, "document image was not loaded");
        AssertTrue(display.ImageChanged, "new document should track image change state");

        display.SetLabelingMode(LabelingRoiMode.Segmentation);
        AssertEqual(LabelingRoiMode.Segmentation, display.CurrentLabelingMode);
        display.SetLabelingMode(LabelingRoiMode.Rectangle);
        AssertEqual(LabelingRoiMode.Rectangle, display.CurrentLabelingMode);
        display.SegmentationBrushRadius = 100;
        AssertEqual(48, display.SegmentationBrushRadius);

        display.SetRoiRectangles(new[] { new Rectangle(1, 2, 3, 4) }, new CClassItem { Text = "OK", DrawColor = Color.LimeGreen });
        AssertEqual(1, display.GetRoiListItems().Count);
        AssertTrue(display.GetRoiByClass().ContainsKey("OK"), "ROI class was not stored in the document");

        display.AcceptImageChanged();
        AssertTrue(!display.ImageChanged, "image change flag was not accepted");
        display.Dispose();
        AssertTrue(display.IsDisposed, "document dispose state was not set");
    }
    private static void TestDisplayManagerImageSourceOwnership()
    {
        CvMat first = new CvMat(2, 2, CvMatType.CV_8UC1, CvScalar.All(1));
        CvMat second = new CvMat(3, 3, CvMatType.CV_8UC1, CvScalar.All(2));

        CDisplayManager.ImageSrc = first;
        AssertTrue(ReferenceEquals(first, CDisplayManager.ImageSrc), "display manager should own the assigned image source Mat without cloning");
        CDisplayManager.ImageSrc = second;

        AssertTrue(first.IsDisposed, "replaced image source Mat should be disposed");
        AssertTrue(ReferenceEquals(second, CDisplayManager.ImageSrc), "display manager should keep the assigned replacement Mat");
        AssertEqual(3, CDisplayManager.ImageSrc.Width);
        CDisplayManager.ImageSrc = new CvMat();
        AssertTrue(second.IsDisposed, "cleared image source Mat should be disposed");
    }

    private static void TestDisplayManagerLayerCatalog()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            using var image = new Bitmap(12, 8);

            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.CreateLayerDisplay(image, "Detect", true);

            AssertEqual(2, CDisplayManager.LayerCount);
            AssertEqual("Main", CDisplayManager.GetLayerTitle(0));
            AssertEqual("Detect", CDisplayManager.GetLayerTitle(1));
            AssertEqual(1, CDisplayManager.FindIndex("detect"));
            AssertTrue(ReferenceEquals(CDisplayManager.GetMainDisplayOrNull(), CDisplayManager.GetLayerDisplayOrNull("Main")), "main display lookup was inconsistent");

            CDisplayManager.SelecteItem = "Detect";
            AssertEqual("Detect", CDisplayManager.GetSelectedDisplayOrNull().Text);

            AssertTrue(CDisplayManager.IsLayerImageChanged("Detect"), "new layer image change flag was not set");
            CDisplayManager.AcceptLayerImageChanged("Detect");
            AssertTrue(!CDisplayManager.IsLayerImageChanged("Detect"), "layer image change flag was not accepted");

            IReadOnlyList<DisplayLayerInfo> infos = CDisplayManager.GetLayerInfos();
            AssertEqual(2, infos.Count);
            AssertEqual("Main", infos[0].Title);
            AssertEqual("Detect", infos[1].Title);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestLabelingWorkflowService()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);

            var classItem = new CClassItem { Text = "Part", DrawColor = Color.Blue };
            CGlobal.Inst.LabelingWorkflow.ApplySelectedClass(classItem);

            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            mainDisplay.SetRoiRectangles(new[] { new Rectangle(2, 3, 10, 11) }, reset: true);

            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();

            AssertEqual(1, items.Count);
            AssertEqual(1, items[0].Index);
            AssertEqual("Part", items[0].ClassName);
            AssertEqual(new Rectangle(2, 3, 10, 11), items[0].Roi);
            AssertEqual(LabelingAnnotationKind.Rectangle, items[0].Kind);
            AssertTrue(!items[0].IsSelected, "ROI should not be selected before explicit list selection");

            AssertTrue(CGlobal.Inst.LabelingWorkflow.SelectMainRoiItem(1), "main ROI list selection failed");
            AssertEqual(1, CGlobal.Inst.LabelingWorkflow.GetMainSelectedRoiListIndex());
            items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            AssertTrue(items[0].IsSelected, "selected ROI list item was not reflected back from the viewer");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestDisplayManagerDetectionOverlayRouting()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            using var image = new Bitmap(12, 8);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.CreateLayerDisplay(image, "Detect", true);

            var overlays = new[]
            {
                new DetectionOverlayItem
                {
                    ClassName = "Part",
                    Confidence = 0.95F,
                    Bounds = new RectangleF(1, 2, 3, 4),
                    Color = Color.Red
                }
            };

            AssertTrue(CDisplayManager.SetDetectionOverlays("Main", overlays), "Main overlay update failed");
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
            AssertEqual(0, CDisplayManager.GetLayerDisplayOrNull("Detect").DetectionOverlayCount);

            AssertTrue(!CDisplayManager.SetDetectionOverlays("Missing", overlays), "missing layer overlay update should not fall back to the first layer");
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
            AssertEqual(0, CDisplayManager.GetLayerDisplayOrNull("Detect").DetectionOverlayCount);

            AssertTrue(CDisplayManager.SetDetectionOverlays("Main", null), "Main overlay clear failed");
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
        }
    }

    private static void TestLabelingWorkflowCommitAnnotations()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var viewer = new CViewer();
            using var image = new Bitmap(20, 20);
            viewer.SetDisplayImage(image);
            viewer.SetSelectedClass(data.ClassNamedList[0]);
            viewer.SetRoiRectangles(new[] { new Rectangle(5, 5, 10, 10) }, data.ClassNamedList[0], reset: true);

            var workflow = new LabelingWorkflowService();
            bool committed = workflow.CommitCurrentAnnotations(viewer, data, new CSystem());

            AssertTrue(committed, "annotation commit failed");
            string trainLabel = Path.Combine(root, "data", "train", "labels", "sample.txt");
            AssertTrue(File.Exists(trainLabel), "committed label file was not created");
            AssertEqual("0 0.5 0.5 0.5 0.5", File.ReadAllText(trainLabel).Trim());

            AssertTrue(!workflow.CommitCurrentAnnotations(viewer, null, new CSystem()), "commit with null data should fail without exception");

            var previousDisplays = CDisplayManager.Displays.ToList();
            try
            {
                using var displayImage = new Bitmap(20, 20);
                CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
                CDisplayManager.CreateLayerDisplay(displayImage, "Main", true);
                DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
                mainDisplay.SetSelectedClass(data.ClassNamedList[0]);
                mainDisplay.SetRoiRectangles(new[] { new Rectangle(2, 2, 4, 4) }, data.ClassNamedList[0], reset: true);
                data.LastSelectImageName = "display-sample";

                AssertTrue(workflow.CommitMainAnnotations(data, new CSystem()), "main display annotation commit failed");
                string displayTrainLabel = Path.Combine(root, "data", "train", "labels", "display-sample.txt");
                AssertTrue(File.Exists(displayTrainLabel), "main display committed label file was not created");
            }
            finally
            {
                foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
                {
                    display.Dispose();
                }

                CDisplayManager.SetDisplayLayerList(previousDisplays);
            }
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestLabelingWorkflowDefaultDefectClass()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "unclassified";

            using var viewer = new CViewer();
            using var image = new Bitmap(40, 30);
            viewer.SetDisplayImage(image);
            viewer.SetRoiRectangles(new[] { new Rectangle(4, 6, 20, 12) }, classItem: null, reset: true);

            var workflow = new LabelingWorkflowService();
            AssertTrue(workflow.CommitCurrentAnnotations(viewer, data, new CSystem()), "unclassified ROI commit failed");
            AssertEqual(1, data.ClassNamedList.Count);
            AssertEqual("Defect", data.ClassNamedList[0].Text);

            string trainLabel = Path.Combine(root, "data", "train", "labels", "unclassified.txt");
            AssertTrue(File.Exists(trainLabel), "default Defect label file was not created");
            string line = File.ReadAllText(trainLabel).Trim();
            AssertTrue(line.StartsWith("0 ", StringComparison.Ordinal), "default Defect class should be written as class index 0");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultApplicationService()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => candidateEventCount = e.CandidateCount;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) => lastReason = e.Reason;
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "NG", Confidence = 0.77F, X = 2, Y = 3, Width = 8, Height = 9 }
            });

            AssertTrue(applied, "detection result was not applied");
            AssertEqual(1, candidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.ResultCompleted, lastReason);
            AssertEqual(1, CDisplayManager.LayerCount);
            AssertEqual("Main", CDisplayManager.GetLayerTitle(0));
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultLimitsReviewCandidates()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "candidate-limit"
            };
            data.ProjectSettings.PythonModel.MaximumDetectionCandidates = 2;
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(80, 60);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(60, 80, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => candidateEventCount = e.CandidateCount;
            service.RegisterPendingDetectionImage(data, image.Size);
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Low", Confidence = 0.30F, X = 1, Y = 1, Width = 10, Height = 10 },
                new DefectInfo { ClassName = "Best", Confidence = 0.95F, X = 12, Y = 1, Width = 10, Height = 10 },
                new DefectInfo { ClassName = "Second", Confidence = 0.80F, X = 24, Y = 1, Width = 10, Height = 10 }
            });

            IReadOnlyList<DefectInfo> defects = service.GetLastDefects();
            IReadOnlyList<DetectionOverlayItem> overlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();

            AssertTrue(applied, "limited detection candidates were not applied");
            AssertEqual(2, candidateEventCount);
            AssertEqual(2, defects.Count);
            AssertEqual("Best", defects[0].ClassName);
            AssertEqual("Second", defects[1].ClassName);
            AssertEqual(2, overlays.Count);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestDetectionResultPathOnlyBatchResult()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
        CDisplayManager.ImageSrc = null;

        try
        {
            string imagePath = Path.Combine(root, "batch-path-only.bmp");
            using (var bitmap = new Bitmap(32, 24))
            {
                bitmap.Save(imagePath);
            }

            var data = new CData
            {
                LastSelectImageName = Path.GetFileNameWithoutExtension(imagePath),
                LastSelectImagePath = imagePath
            };
            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) =>
            {
                candidateEventCount = e.CandidateCount;
                lastReason = e.Reason;
            };

            service.RegisterPendingDetectionImage(data, new Size(32, 24), detectionTimeoutSeconds: 30, requestId: "req1", imageId: "batch-path-only");
            bool applied = service.ApplyToDetectLayer(
                new[]
                {
                    new DefectInfo { ClassName = "OK", Confidence = 0.91F, X = 3, Y = 4, Width = 8, Height = 9 }
                },
                requestId: "req1",
                imageId: "batch-path-only");

            AssertTrue(applied, "path-only detection result should complete without a loaded OpenGL source image");
            AssertEqual(1, candidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.ResultCompleted, lastReason);
            AssertEqual(1, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.LayerCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultApplicationServiceBackgroundThread()
    {
        bool previousCrossThreadCheck = Control.CheckForIllegalCrossThreadCalls;
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        Form owner = null;
        Panel canvasPanel = null;

        Control.CheckForIllegalCrossThreadCalls = true;
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            owner = new Form();
            canvasPanel = new Panel { Dock = DockStyle.Fill };
            owner.Controls.Add(canvasPanel);
            _ = owner.Handle;
            _ = canvasPanel.Handle;
            CDisplayManager.SetForm(owner);
            CDisplayManager.SetDisplayPanel(canvasPanel);

            using (Bitmap image = new Bitmap(20, 20))
            {
                CDisplayManager.CreateLayerDisplay(image, "Main", false);
            }

            var service = new DetectionResultApplicationService();
            Task<bool> applyTask = Task.Run(() => service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "NG", Confidence = 0.91F, X = 2, Y = 3, Width = 8, Height = 9 }
            }));

            AssertTrue(PumpUntil(() => applyTask.IsCompleted, TimeSpan.FromSeconds(3)), "background detection result did not return to the caller");
            AssertTrue(applyTask.GetAwaiter().GetResult(), "background detection result was not applied");
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetForm(null);
            CDisplayManager.SetDisplayPanel(null);
            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            canvasPanel?.Dispose();
            owner?.Dispose();
            Control.CheckForIllegalCrossThreadCalls = previousCrossThreadCheck;
        }
    }

    private static void TestDetectionResultKeepsMainActive()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.SelecteItem = "Main";
            CDisplayManager.FocusItem = "Main";

            var service = new DetectionResultApplicationService();
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "NG", Confidence = 0.77F, X = 2, Y = 3, Width = 8, Height = 9 }
            });

            AssertTrue(applied, "detection result was not applied");
            AssertEqual(1, CDisplayManager.LayerCount);
            AssertEqual("Main", CDisplayManager.SelecteItem);
            AssertEqual(1, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultApplicationServiceClearsEmptyResult()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", true);
            CDisplayManager.SetDetectionOverlays("Main", new[]
            {
                new DetectionOverlayItem
                {
                    ClassName = "NG",
                    Confidence = 0.9F,
                    Bounds = new RectangleF(1, 1, 4, 4),
                    Color = Color.Red
                }
            });

            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => candidateEventCount = e.CandidateCount;

            bool applied = service.ApplyToDetectLayer(Array.Empty<DefectInfo>());

            AssertTrue(!applied, "empty detection result should not create a new layer");
            AssertEqual(0, candidateEventCount);
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultCancelPendingIgnoresLateBoxes()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());
        CDisplayManager.ImageSrc = new CvMat(20, 20, CvMatType.CV_8UC3, CvScalar.White);

        try
        {
            var data = new CData
            {
                LastSelectImageName = "pending-cancel",
                LastSelectImagePath = Path.Combine(root, "pending-cancel.bmp")
            };
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", true);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(data, image.Size);
            service.CancelPendingDetection();

            int candidateEventCount = 0;
            service.DetectionCandidatesUpdated += (_, _) => candidateEventCount++;

            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Late", Confidence = 0.9F, X = 1, Y = 1, Width = 8, Height = 8 }
            });

            AssertTrue(!applied, "late detection boxes should be ignored after pending cancellation");
            AssertEqual(0, candidateEventCount);
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultPendingRequestTimeout()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "timeout-sample",
                LastSelectImagePath = Path.Combine(root, "timeout-sample.bmp")
            };
            CGlobal.Inst.Data = data;

            var service = new DetectionResultApplicationService();
            using var timeoutEvent = new ManualResetEventSlim(false);
            DetectionCandidateUpdateReason timeoutReason = DetectionCandidateUpdateReason.CandidatesChanged;
            int timeoutCandidateCount = -1;

            service.DetectionCandidatesUpdated += (_, e) =>
            {
                if (e.Reason != DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    return;
                }

                timeoutReason = e.Reason;
                timeoutCandidateCount = e.CandidateCount;
                timeoutEvent.Set();
            };

            service.RegisterPendingDetectionImage(data, new Size(40, 30), detectionTimeoutSeconds: 1);

            AssertTrue(timeoutEvent.Wait(TimeSpan.FromSeconds(3)), "pending detection request did not time out");
            AssertEqual(DetectionCandidateUpdateReason.RequestTimedOut, timeoutReason);
            AssertEqual(0, timeoutCandidateCount);
            AssertEqual(0, service.GetLastDefects().Count);

            bool appliedLateResult = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Late", Confidence = 0.9F, X = 1, Y = 1, Width = 8, Height = 8 }
            });

            AssertTrue(!appliedLateResult, "late detection boxes should be ignored after timeout");
            AssertEqual(0, service.GetLastDefects().Count);
        }
        finally
        {
            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultConfirmAsLabels()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "confirm-sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => lastCandidateEventCount = e.CandidateCount;
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            AssertTrue(service.CanCommitLastDetection(data), "fresh detection result should be confirmable");
            AssertEqual(1, lastCandidateEventCount);
            bool confirmed = service.CommitLastDetectionToMainLabels(data, new CSystem());
            bool confirmedAgain = service.CommitLastDetectionToMainLabels(data, new CSystem());
            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "confirm-sample.txt");

            AssertTrue(confirmed, "detection result was not confirmed as labels");
            AssertTrue(!confirmedAgain, "already confirmed detection result should not be confirmed again");
            AssertTrue(!service.CanCommitLastDetection(data), "confirmed detection result should be cleared");
            AssertEqual(0, lastCandidateEventCount);
            AssertEqual(1, items.Count);
            AssertEqual("Part", items[0].ClassName);
            AssertEqual(new Rectangle(5, 6, 10, 8), items[0].Roi);
            AssertTrue(File.Exists(labelPath), "confirmed detection label file was not saved");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultConfirmAsSegmentationMasks()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "confirm-segment";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            bool confirmed = service.CommitLastDetectionToMainLabels(
                data,
                new CSystem(),
                minimumConfidence: 0.5F,
                createSegmentationFromBoxes: true);
            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "confirm-segment.txt");
            string maskPath = Path.Combine(root, "data", "train", "masks", "confirm-segment.png");
            string segmentPath = Path.Combine(root, "data", "train", "segments", "confirm-segment.json");

            AssertTrue(confirmed, "detection result was not confirmed with segmentation masks");
            AssertEqual(2, items.Count);
            AssertTrue(File.Exists(labelPath), "YOLO label file was not saved");
            AssertTrue(File.Exists(maskPath), "segmentation mask file was not saved");
            AssertTrue(File.Exists(segmentPath), "segmentation polygon file was not saved");

            using var mask = new Bitmap(maskPath);
            AssertEqual(1, mask.GetPixel(7, 8).R);
            AssertEqual(0, mask.GetPixel(25, 25).R);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultRejectsStaleImage()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var requestedData = new CData
            {
                LastSelectImageName = "requested-image",
                LastSelectImagePath = Path.Combine(root, "requested-image.bmp")
            };
            var activeData = new CData
            {
                LastSelectImageName = "active-image",
                LastSelectImagePath = Path.Combine(root, "active-image.bmp")
            };

            CGlobal.Inst.Data = activeData;
            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int candidateEventCount = -1;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) =>
            {
                candidateEventCount = e.CandidateCount;
                lastReason = e.Reason;
            };

            service.RegisterPendingDetectionImage(requestedData, image.Size);
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            AssertTrue(!applied, "stale detection result should not be applied to the active image");
            AssertEqual(1, CDisplayManager.LayerCount);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().DetectionOverlayCount);
            AssertEqual(1, candidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.ResultCompleted, lastReason);
            AssertEqual(1, service.GetLastDefects().Count);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultAddsMissingClasses()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "auto-class-sample";

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "OK", Confidence = 0.98F, X = 4, Y = 5, Width = 12, Height = 9 }
            });

            bool confirmed = service.CommitLastDetectionToMainLabels(data, new CSystem());
            string labelPath = Path.Combine(root, "data", "train", "labels", "auto-class-sample.txt");

            AssertTrue(confirmed, "detection result with a missing model class was not confirmed");
            AssertEqual(1, data.ClassNamedList.Count);
            AssertEqual("OK", data.ClassNamedList[0].Text);
            AssertTrue(File.Exists(labelPath), "auto-class detection label file was not saved");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultFiltersLowConfidence()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "confidence-sample";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.2F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.8F, X = 12, Y = 6, Width = 10, Height = 9 }
            });

            bool confirmed = service.CommitLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F);
            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();

            AssertTrue(confirmed, "high-confidence detection result was not confirmed");
            AssertEqual(1, items.Count);
            AssertEqual(new Rectangle(12, 6, 10, 9), items[0].Roi);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultCanCommitUsesMinimumConfidence()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.LastSelectImageName = "confidence-availability";
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.2F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.8F, X = 60, Y = 60, Width = 10, Height = 9 }
            });

            AssertTrue(service.CanCommitLastDetection(data), "default threshold should allow the low-confidence in-bounds box");
            AssertTrue(!service.CanCommitLastDetection(data, minimumConfidence: 0.5F), "minimum confidence should disable confirmation when no in-bounds box passes");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
        }
    }

    private static void TestDetectionResultReviewCandidates()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "candidate-review"
            };
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.8F, X = 2, Y = 3, Width = 8, Height = 9 },
                new DefectInfo { ClassName = "Part", Confidence = 0.2F, X = 12, Y = 5, Width = 7, Height = 6 },
                new DefectInfo { ClassName = "Part", Confidence = 0.9F, X = 60, Y = 60, Width = 10, Height = 10 }
            });

            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data, minimumConfidence: 0.5F);

            AssertEqual(3, candidates.Count);
            AssertTrue(candidates[0].IsConfirmable, "high-confidence in-bounds candidate should be confirmable");
            AssertTrue(!candidates[1].IsConfirmable, "low-confidence candidate should not be confirmable");
            AssertTrue(!candidates[1].IsConfidenceAccepted, "low-confidence candidate was not marked as below threshold");
            AssertTrue(!candidates[2].IsConfirmable, "out-of-bounds candidate should not be confirmable");
            AssertTrue(!candidates[2].IsInImageBounds, "out-of-bounds candidate was not marked as outside the image");
            AssertEqual(new Rectangle(2, 3, 8, 9), candidates[0].ClippedBounds);

            AssertTrue(service.SelectDetectionCandidate(2, data), "candidate selection failed");
            IReadOnlyList<DetectionCandidateReviewItem> selectedCandidates = service.GetLastCandidateReviewItems(data, minimumConfidence: 0.5F);
            IReadOnlyList<DetectionOverlayItem> mainOverlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();

            AssertTrue(!selectedCandidates[0].IsSelected, "first candidate should not be selected");
            AssertTrue(selectedCandidates[1].IsSelected, "second candidate should be selected");
            AssertTrue(mainOverlays.Any(overlay => overlay.CandidateIndex == 2 && overlay.IsSelected), "Main overlay did not select candidate 2");
            AssertTrue(!service.CanCommitLastDetection(data, minimumConfidence: 0.5F), "selected low-confidence candidate should not be confirmable");
            AssertTrue(!service.SelectDetectionCandidate(99, data), "missing candidate selection should fail");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestDetectionResultConfirmsSelectedCandidateOnly()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "selected-candidate";
            data.LastSelectImagePath = Path.Combine(root, "selected-candidate.bmp");
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(50, 40);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(40, 50, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            service.DetectionCandidatesUpdated += (_, e) => lastCandidateEventCount = e.CandidateCount;
            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.91F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.92F, X = 20, Y = 10, Width = 12, Height = 9 }
            });

            AssertTrue(service.SelectDetectionCandidate(2, data), "second candidate was not selected");
            AssertTrue(service.CommitLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F), "selected candidate was not confirmed");

            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            IReadOnlyList<DefectInfo> remaining = service.GetLastDefects();
            IReadOnlyList<DetectionOverlayItem> mainOverlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();
            string labelPath = Path.Combine(root, "data", "train", "labels", "selected-candidate.txt");

            AssertEqual(1, items.Count);
            AssertEqual(new Rectangle(20, 10, 12, 9), items[0].Roi);
            AssertEqual(1, remaining.Count);
            AssertEqual(1, mainOverlays.Count);
            AssertEqual(1, mainOverlays[0].CandidateIndex);
            AssertTrue(!mainOverlays[0].IsSelected, "remaining candidate should not stay selected after commit");
            AssertEqual(1, lastCandidateEventCount);
            AssertTrue(File.Exists(labelPath), "selected candidate label file was not saved");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultSkipsSelectedCandidate()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData
            {
                LastSelectImageName = "skip-candidate"
            };
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(50, 40);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(40, 50, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) =>
            {
                lastCandidateEventCount = e.CandidateCount;
                lastReason = e.Reason;
            };

            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "A", Confidence = 0.91F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "B", Confidence = 0.92F, X = 20, Y = 10, Width = 12, Height = 9 }
            });

            AssertTrue(service.SelectDetectionCandidate(1, data), "first candidate was not selected");
            AssertTrue(service.CanSkipSelectedDetectionCandidate(data), "selected candidate should be skippable");
            AssertTrue(service.SkipSelectedDetectionCandidate(data), "selected candidate was not skipped");

            IReadOnlyList<DefectInfo> remaining = service.GetLastDefects();
            IReadOnlyList<DetectionOverlayItem> overlays = CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays();
            IReadOnlyList<DetectionCandidateReviewItem> candidates = service.GetLastCandidateReviewItems(data);

            AssertEqual(1, remaining.Count);
            AssertEqual("B", remaining[0].ClassName);
            AssertEqual(1, overlays.Count);
            AssertEqual(1, overlays[0].CandidateIndex);
            AssertTrue(!overlays[0].IsSelected, "remaining candidate should not stay selected after skip");
            AssertEqual(1, candidates.Count);
            AssertEqual(1, candidates[0].Index);
            AssertEqual("B", candidates[0].ClassName);
            AssertEqual(1, lastCandidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.CandidatesChanged, lastReason);
            AssertTrue(!service.CanSkipSelectedDetectionCandidate(data), "selection should clear after skip");

            AssertTrue(service.SelectDetectionCandidate(1, data), "remaining candidate was not selected");
            AssertTrue(service.SkipSelectedDetectionCandidate(data), "last candidate was not skipped");
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays().Count);
            AssertEqual(0, lastCandidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.CandidateSkipped, lastReason);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
        }
    }

    private static void TestDetectionResultConfirmsAllCandidatesDespiteSelection()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.LastSelectImageName = "all-candidates";
            data.LastSelectImagePath = Path.Combine(root, "all-candidates.bmp");
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });
            CGlobal.Inst.Data = data;

            using var image = new Bitmap(50, 40);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(40, 50, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            int lastCandidateEventCount = -1;
            DetectionCandidateUpdateReason lastReason = DetectionCandidateUpdateReason.CandidatesChanged;
            service.DetectionCandidatesUpdated += (_, e) =>
            {
                lastCandidateEventCount = e.CandidateCount;
                lastReason = e.Reason;
            };

            service.RegisterPendingDetectionImage(data, image.Size);
            service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.91F, X = 1, Y = 2, Width = 8, Height = 8 },
                new DefectInfo { ClassName = "Part", Confidence = 0.92F, X = 20, Y = 10, Width = 12, Height = 9 }
            });

            AssertTrue(service.SelectDetectionCandidate(1, data), "first candidate was not selected");
            AssertTrue(service.CommitAllLastDetectionToMainLabels(data, new CSystem(), minimumConfidence: 0.5F), "all candidates were not confirmed");

            IReadOnlyList<LabelingRoiListItem> items = CGlobal.Inst.LabelingWorkflow.GetMainRoiItems();
            string labelPath = Path.Combine(root, "data", "train", "labels", "all-candidates.txt");

            AssertEqual(2, items.Count);
            AssertTrue(items.Any(item => item.Roi == new Rectangle(1, 2, 8, 8)), "first candidate was not saved");
            AssertTrue(items.Any(item => item.Roi == new Rectangle(20, 10, 12, 9)), "second candidate was not saved");
            AssertEqual(0, service.GetLastDefects().Count);
            AssertEqual(0, CDisplayManager.GetMainDisplayOrNull().GetDetectionOverlays().Count);
            AssertEqual(0, lastCandidateEventCount);
            AssertEqual(DetectionCandidateUpdateReason.CandidatesConfirmed, lastReason);
            AssertTrue(File.Exists(labelPath), "all-candidate label file was not saved");
            AssertEqual(2, File.ReadAllLines(labelPath).Length);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestDetectionResultRejectsStaleConfirmation()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        CData previousData = CGlobal.Inst.Data;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var requestedData = new CData();
            requestedData.ConfigureOutputRoot(root);
            requestedData.ProjectSettings.YoloDataset.ValidationPercent = 0;
            requestedData.LastSelectImageName = "requested-image";
            requestedData.LastSelectImagePath = Path.Combine(root, "requested-image.bmp");
            requestedData.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            var activeData = new CData();
            activeData.ConfigureOutputRoot(root);
            activeData.ProjectSettings.YoloDataset.ValidationPercent = 0;
            activeData.LastSelectImageName = "active-image";
            activeData.LastSelectImagePath = Path.Combine(root, "active-image.bmp");
            activeData.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            CGlobal.Inst.Data = requestedData;
            using var image = new Bitmap(40, 30);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);
            CDisplayManager.ImageSrc = new CvMat(30, 40, CvMatType.CV_8UC3, CvScalar.White);

            var service = new DetectionResultApplicationService();
            service.RegisterPendingDetectionImage(requestedData, image.Size);
            bool applied = service.ApplyToDetectLayer(new[]
            {
                new DefectInfo { ClassName = "Part", Confidence = 0.99F, X = 5, Y = 6, Width = 10, Height = 8 }
            });

            CGlobal.Inst.Data = activeData;
            bool confirmed = service.CommitLastDetectionToMainLabels(activeData, new CSystem());
            string activeLabelPath = Path.Combine(root, "data", "train", "labels", "active-image.txt");

            AssertTrue(applied, "fresh detection result should be applied before the image changes");
            AssertTrue(!confirmed, "stale detection result should not be confirmed for a different image");
            AssertTrue(!File.Exists(activeLabelPath), "stale detection labels should not be saved under the active image");
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            CDisplayManager.ImageSrc = null;
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(root);
        }
    }

    private static void TestLabelingWorkflowReloadsSavedAnnotations()
    {
        List<DisplayLayerDocument> previousDisplays = CDisplayManager.Displays;
        string previousSelected = CDisplayManager.SelecteItem;
        string previousFocus = CDisplayManager.FocusItem;
        string root = CreateTempRoot();

        CDisplayManager.SetForm(null);
        CDisplayManager.SetDockPanel(null);
        CDisplayManager.SetDisplayLayerList(new List<DisplayLayerDocument>());

        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ClassNamedList.Add(new CClassItem { Text = "Part", DrawColor = Color.Blue });

            using var image = new Bitmap(20, 20);
            CDisplayManager.CreateLayerDisplay(image, "Main", false);

            var rois = new Dictionary<string, List<CRectangleObject>>
            {
                ["Part"] = new List<CRectangleObject>
                {
                    new CRectangleObject { Roi = new Rectangle(2, 4, 10, 8), cClassItem = data.ClassNamedList[0] }
                }
            };

            YoloAnnotationService.SaveAnnotations("sample.png", image, rois, data.ClassNamedList, data);
            string trainImage = Path.Combine(root, "data", "train", "images", "sample.jpeg");

            var service = new LabelingWorkflowService();
            bool loaded = service.LoadSavedAnnotationsToMainDisplay(trainImage, image.Size, data);
            IReadOnlyList<LabelingRoiListItem> items = service.GetMainRoiItems();

            AssertTrue(loaded, "saved YOLO labels were not loaded");
            AssertEqual(1, items.Count);
            AssertEqual("Part", items[0].ClassName);
            AssertEqual(new Rectangle(2, 4, 10, 8), items[0].Roi);

            bool emptyLoaded = service.LoadSavedAnnotationsToMainDisplay(
                Path.Combine(root, "data", "train", "images", "empty.jpeg"),
                image.Size,
                data);
            AssertTrue(!emptyLoaded, "missing labels should report no saved annotations");
            AssertEqual(0, service.GetMainRoiItems().Count);
        }
        finally
        {
            foreach (DisplayLayerDocument display in CDisplayManager.Displays.ToList())
            {
                display.Dispose();
            }

            CDisplayManager.SetDisplayLayerList(previousDisplays);
            CDisplayManager.SelecteItem = previousSelected;
            CDisplayManager.FocusItem = previousFocus;
            DeleteTempRoot(root);
        }
    }

    private static void TestMeasurementGeometry()
    {
        AssertEqual(5D, MeasurementGeometry.Distance(new Point(0, 0), new Point(3, 4)));

        bool calculated = MeasurementGeometry.TryCalculateVerticalMeasurement(
            new Point(10, 10),
            new Point(90, 10),
            new Point(50, 50),
            new Size(100, 100),
            out Point verticalPoint,
            out double pixelDistance);

        AssertTrue(calculated, "vertical measurement was not calculated");
        AssertEqual(new Point(50, 10), verticalPoint);
        AssertEqual(40D, pixelDistance);

        using var viewer = new CViewer();
        viewer.SetMeasurementOverlay(new Point(1, 1), new Point(4, 5), new Point(4, 5), new Point(4, 1), 0.5F);
        viewer.ClearMeasurementOverlay();
    }

    private static void TestDetectionOverlays()
    {
        var defects = new List<DefectInfo>
        {
            new DefectInfo { ClassName = "OK", Confidence = 0.9876F, X = 1.234F, Y = 2.345F, Width = 30.456F, Height = 40.567F },
            new DefectInfo { ClassName = "NG", Confidence = 0.1234F, X = 5, Y = 6, Width = 0, Height = 7 }
        };

        List<DetectionOverlayItem> overlays = PythonDetectionResultProtocol.BuildDetectionOverlays(
            defects,
            className => string.Equals(className, "OK", StringComparison.OrdinalIgnoreCase) ? Color.Blue : null);

        AssertEqual(1, overlays.Count);
        AssertEqual(1, overlays[0].CandidateIndex);
        AssertEqual("OK", overlays[0].ClassName);
        AssertEqual(0.98F, overlays[0].Confidence);
        AssertEqual(new RectangleF(1.23F, 2.34F, 30.45F, 40.56F), overlays[0].Bounds);
        AssertEqual(Color.Blue, overlays[0].Color);
        AssertTrue(!overlays[0].IsSelected, "overlay should not be selected by default");
        AssertEqual("AI #1 OK 98%", overlays[0].Label);

        List<DetectionOverlayItem> selectedOverlays = PythonDetectionResultProtocol.BuildDetectionOverlays(defects, null, selectedCandidateIndex: 1);
        AssertTrue(selectedOverlays[0].IsSelected, "selected overlay was not marked");
        AssertEqual("AI #1 OK 98%", selectedOverlays[0].Label);

        using var viewer = new CViewer();
        viewer.SetDetectionOverlays(overlays);
        viewer.ClearDetectionOverlays();

        Color fallbackOverlayColor = InvokePrivateStaticResult<Color>(typeof(CViewer), "EnsureReadableOverlayColor", Color.Black);
        AssertEqual(Color.FromArgb(72, 190, 255), fallbackOverlayColor);
    }

    private static void TestCViewerDetectionOverlayRenderLifecycle()
    {
        using var form = new Form
        {
            Size = new Size(640, 480),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, 100),
            ShowInTaskbar = false,
            Text = "OverlayRenderLifecycle"
        };
        using var host = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(host);
        using var viewer = new CViewer();
        using var hostAdapter = new CViewerWinFormsHostAdapter(viewer, host);

        using var image = new Bitmap(120, 100);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.FromArgb(70, 70, 70));
            using var fill = new SolidBrush(Color.FromArgb(150, 150, 150));
            graphics.FillEllipse(fill, 30, 25, 65, 55);
        }

        form.Show();
        Application.DoEvents();
        viewer.LoadMainImage(image, "overlay_lifecycle.bmp");
        viewer.SetDetectionOverlays(new[]
        {
            new DetectionOverlayItem
            {
                CandidateIndex = 1,
                ClassName = "OK",
                Confidence = 0.95F,
                Bounds = new RectangleF(20, 20, 80, 55),
                Color = Color.LimeGreen
            },
            new DetectionOverlayItem
            {
                CandidateIndex = 2,
                ClassName = "NG",
                Confidence = 0.91F,
                Bounds = new RectangleF(50, 38, 22, 34),
                Color = Color.Red,
                IsSelected = true
            }
        });

        for (int i = 0; i < 8; i++)
        {
            viewer.Canvas.RefreshGL();
            Application.DoEvents();
            Thread.Sleep(20);
        }

        AssertEqual(2, viewer.DetectionOverlayCount);
        viewer.Dispose();
        Application.DoEvents();
        form.Close();
    }

    private static int GetAvailableTcpPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void RunMockDetectionClient(int port, ManualResetEventSlim receivedRequest)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        using NetworkStream stream = client.GetStream();
        stream.ReadTimeout = 30000;
        stream.WriteTimeout = 5000;

        byte[] packet = ReadStartDefectPacket(stream);
        string command = Encoding.ASCII.GetString(packet, 0, Math.Min(packet.Length, CCommunicationLearning.CommandLearning.StartDefect.ToString().Length));
        AssertTrue(command.StartsWith(CCommunicationLearning.CommandLearning.StartDefect.ToString(), StringComparison.Ordinal), "mock client received an unexpected command");
        receivedRequest.Set();

        string result = "{\"type\":\"ResultDefect\",\"version\":1,\"imageId\":\"tcp-round-trip\",\"items\":[{\"className\":\"NG\",\"confidence\":0.98,\"x\":4,\"y\":5,\"width\":12,\"height\":9}]}";
        byte[] resultBytes = Encoding.UTF8.GetBytes(result);
        stream.Write(resultBytes, 0, resultBytes.Length);
        stream.Flush();
    }

    private static byte[] ReadStartDefectPacket(NetworkStream stream)
    {
        var buffer = new List<byte>();
        byte[] chunk = new byte[4096];
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow <= deadline)
        {
            int read = stream.Read(chunk, 0, chunk.Length);
            if (read <= 0)
            {
                break;
            }

            buffer.AddRange(chunk.Take(read));
            int markerIndex = IndexOf(buffer, Encoding.ASCII.GetBytes("IEND"));
            if (markerIndex >= 0 && buffer.Count >= markerIndex + 8)
            {
                return buffer.ToArray();
            }
        }

        throw new InvalidOperationException("mock client timed out while reading StartDefect packet");
    }

    private static int IndexOf(List<byte> source, byte[] pattern)
    {
        if (source == null || pattern == null || pattern.Length == 0 || source.Count < pattern.Length)
        {
            return -1;
        }

        for (int i = 0; i <= source.Count - pattern.Length; i++)
        {
            bool matched = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static RealYoloSmokeSettings BuildRealYoloSmokeSettings(string artifactRoot)
    {
        var defaults = new PythonModelSettings();
        defaults.EnsureDefaults();

        string projectRoot = GetEnvironmentValue("LABELING_SMOKE_PROJECT_ROOT", defaults.ProjectRootPath);
        string clientScriptPath = GetEnvironmentValue("LABELING_SMOKE_CLIENT_SCRIPT", Path.Combine(projectRoot, "labelling_tcp_client.py"));
        string weightsPath = GetEnvironmentValue("LABELING_SMOKE_WEIGHTS", Path.Combine(projectRoot, "best.pt"));
        string imageRootPath = GetEnvironmentValue("LABELING_SMOKE_IMAGE_ROOT", defaults.ImageRootPath);
        defaults.ProjectRootPath = projectRoot;
        defaults.ClientScriptPath = clientScriptPath;
        defaults.WeightsPath = weightsPath;
        defaults.ImageRootPath = imageRootPath;
        string imagePath = GetEnvironmentValue("LABELING_SMOKE_IMAGE_PATH", string.Empty);
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            imagePath = YoloWorkerSmokeTestService.ResolveSmokeImagePath(defaults);
        }

        string modelRootPath = GetEnvironmentValue("LABELING_SMOKE_MODEL_ROOT", Path.Combine(projectRoot, "yolov5Master"));
        string venvPythonPath = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        string defaultPythonPath = File.Exists(venvPythonPath)
            ? venvPythonPath
            : PythonModelSettingsValidator.ResolvePythonExecutable(defaults);

        return new RealYoloSmokeSettings
        {
            ArtifactRoot = artifactRoot,
            PythonExecutablePath = GetEnvironmentValue("LABELING_SMOKE_PYTHON_EXE", defaultPythonPath),
            ProjectRootPath = projectRoot,
            ClientScriptPath = clientScriptPath,
            ModelRootPath = modelRootPath,
            WeightsPath = weightsPath,
            ImageRootPath = imageRootPath,
            ImagePath = imagePath,
            Device = GetEnvironmentValue("LABELING_SMOKE_DEVICE", "cpu"),
            ImageSize = GetEnvironmentInt("LABELING_SMOKE_IMAGE_SIZE", defaults.InferenceImageSize),
            MinimumConfidence = GetEnvironmentFloat("LABELING_SMOKE_CONFIDENCE", defaults.MinimumDetectionConfidence),
            Iou = GetEnvironmentFloat("LABELING_SMOKE_IOU", 0.45F)
        };
    }

    private static void AssertRealYoloSmokeInputs(RealYoloSmokeSettings settings)
    {
        AssertTrue(settings != null, "real YOLO smoke settings were not created");
        AssertTrue(!PythonModelSettingsValidator.LooksLikePath(settings.PythonExecutablePath) || File.Exists(settings.PythonExecutablePath), $"Python executable was not found: {settings.PythonExecutablePath}");
        AssertTrue(Directory.Exists(settings.ProjectRootPath), $"YOLO project root was not found: {settings.ProjectRootPath}");
        AssertTrue(File.Exists(settings.ClientScriptPath), $"YOLO TCP client script was not found: {settings.ClientScriptPath}");
        AssertTrue(Directory.Exists(settings.ModelRootPath), $"YOLO model root was not found: {settings.ModelRootPath}");
        AssertTrue(File.Exists(settings.WeightsPath), $"YOLO weights were not found: {settings.WeightsPath}");
        AssertTrue(Directory.Exists(settings.ImageRootPath), $"YOLO image root was not found: {settings.ImageRootPath}");
        AssertTrue(File.Exists(settings.ImagePath), $"YOLO smoke image was not found: {settings.ImagePath}");
        AssertTrue(settings.ImageSize > 0, $"YOLO smoke image size must be positive: {settings.ImageSize}");
        AssertTrue(settings.MinimumConfidence >= 0F && settings.MinimumConfidence <= 1F, $"YOLO smoke confidence must be between 0 and 1: {settings.MinimumConfidence}");
        AssertTrue(settings.Iou >= 0F && settings.Iou <= 1F, $"YOLO smoke IoU must be between 0 and 1: {settings.Iou}");
    }

    private static Process StartRealYoloClient(
        RealYoloSmokeSettings settings,
        int port,
        int timeoutSeconds,
        StringBuilder stdout,
        StringBuilder stderr)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = settings.PythonExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(settings.ClientScriptPath) ?? settings.ProjectRootPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.ArgumentList.Add(settings.ClientScriptPath);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--timeout");
        startInfo.ArgumentList.Add(timeoutSeconds.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--weights");
        startInfo.ArgumentList.Add(settings.WeightsPath);
        startInfo.ArgumentList.Add("--model-root");
        startInfo.ArgumentList.Add(settings.ModelRootPath);
        startInfo.ArgumentList.Add("--image-root");
        startInfo.ArgumentList.Add(settings.ImageRootPath);
        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add(settings.Device ?? string.Empty);
        startInfo.ArgumentList.Add("--img-size");
        startInfo.ArgumentList.Add(settings.ImageSize.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--conf");
        startInfo.ArgumentList.Add(settings.MinimumConfidence.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--iou");
        startInfo.ArgumentList.Add(settings.Iou.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--once");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) => AppendProcessLine(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => AppendProcessLine(stderr, e.Data);

        AssertTrue(process.Start(), "real YOLO Python client process did not start");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void AssertRealYoloClientExitedCleanly(Process process, StringBuilder stdout, StringBuilder stderr)
    {
        if (process == null)
        {
            return;
        }

        if (!process.WaitForExit(10000))
        {
            throw new InvalidOperationException(BuildRealYoloSmokeFailure("real YOLO Python client did not exit after --once", stdout, stderr));
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildRealYoloSmokeFailure($"real YOLO Python client exited with code {process.ExitCode}", stdout, stderr));
        }
    }

    private static void StopRealYoloClient(Process process)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string CreateRealYoloSmokeArtifactRoot()
    {
        string configuredRoot = GetEnvironmentValue("LABELING_SMOKE_ARTIFACT_ROOT", string.Empty);
        string root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetFullPath("artifacts"), "real-yolo-smoke", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))
            : configuredRoot;

        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteRealYoloSmokeSummary(
        string artifactRoot,
        RealYoloSmokeSettings settings,
        IReadOnlyList<DetectionCandidateReviewItem> candidates,
        IReadOnlyList<LabelingRoiListItem> committedItems,
        string labelPath,
        string reviewStatusPath,
        StringBuilder stdout,
        StringBuilder stderr)
    {
        Directory.CreateDirectory(artifactRoot);
        var lines = new List<string>
        {
            $"createdAt={DateTime.Now:O}",
            $"python={settings.PythonExecutablePath}",
            $"client={settings.ClientScriptPath}",
            $"weights={settings.WeightsPath}",
            $"image={settings.ImagePath}",
            $"labelPath={labelPath}",
            $"reviewStatusPath={reviewStatusPath}",
            $"candidateCount={candidates?.Count ?? 0}",
            $"committedCount={committedItems?.Count ?? 0}"
        };

        if (candidates != null)
        {
            lines.AddRange(candidates.Select(item => $"candidate[{item.Index}]={item.ClassName},{item.Confidence.ToString("0.####", CultureInfo.InvariantCulture)},{item.ClippedBounds}"));
        }

        if (committedItems != null)
        {
            lines.AddRange(committedItems.Select(item => $"label[{item.Index}]={item.ClassName},{item.Roi}"));
        }

        File.WriteAllLines(Path.Combine(artifactRoot, "summary.txt"), lines);
        WriteRealYoloProcessLog(artifactRoot, stdout, stderr);
    }

    private static string VerifyRealYoloReviewStatus(CData data, string imagePath, Size imageSize)
    {
        var reviewStatus = new YoloImageReviewStatusService();
        reviewStatus.SetImages(new[] { imagePath });
        YoloImageReviewStatus confirmed = reviewStatus.RefreshLabelStatusAndReviewState(
            imagePath,
            imageSize,
            data,
            hasActiveCandidates: false);

        AssertTrue(confirmed != null, "real YOLO review status was not created");
        AssertEqual("Confirmed", confirmed.DetectionText);
        AssertTrue(confirmed.IsLabeled, "real YOLO confirmed review status did not include label objects");

        reviewStatus.SaveReviewStatus(data);
        string reviewStatusPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(data);
        AssertTrue(File.Exists(reviewStatusPath), $"real YOLO review status file was not saved: {reviewStatusPath}");

        var restored = new YoloImageReviewStatusService();
        restored.LoadReviewStatus(data, new[] { imagePath });
        AssertEqual("Confirmed", restored.GetOrCreate(imagePath).DetectionText);
        return reviewStatusPath;
    }

    private static void WriteRealYoloProcessLog(string artifactRoot, StringBuilder stdout, StringBuilder stderr)
    {
        if (string.IsNullOrWhiteSpace(artifactRoot))
        {
            return;
        }

        Directory.CreateDirectory(artifactRoot);
        File.WriteAllText(Path.Combine(artifactRoot, "python-stdout.txt"), Snapshot(stdout));
        File.WriteAllText(Path.Combine(artifactRoot, "python-stderr.txt"), Snapshot(stderr));
    }

    private static string BuildRealYoloSmokeFailure(string message, StringBuilder stdout, StringBuilder stderr)
    {
        string stderrText = TrimForMessage(Snapshot(stderr), 1600);
        string stdoutText = TrimForMessage(Snapshot(stdout), 1600);
        return $"{message}. stderr: {stderrText} stdout: {stdoutText}";
    }

    private static void AppendProcessLine(StringBuilder builder, string line)
    {
        if (builder == null || line == null)
        {
            return;
        }

        lock (builder)
        {
            builder.AppendLine(line);
        }
    }

    private static string Snapshot(StringBuilder builder)
    {
        if (builder == null)
        {
            return string.Empty;
        }

        lock (builder)
        {
            return builder.ToString();
        }
    }

    private static string TrimForMessage(string text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(empty)";
        }

        text = text.Trim();
        if (text.Length <= maximumLength)
        {
            return text;
        }

        return text.Substring(text.Length - maximumLength);
    }

    private static string GetEnvironmentValue(string name, string fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int GetEnvironmentInt(string name, int fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    private static float GetEnvironmentFloat(string name, float fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow <= deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    private static bool WaitUntilWpf(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow <= deadline)
        {
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));
        return condition();
    }

    private static bool PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow <= deadline)
        {
            Application.DoEvents();
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        Application.DoEvents();
        return condition();
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "LabelingApplication.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Bitmap CreateSolidBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MvcVisionSystem.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertArgumentValue(System.Collections.Generic.IList<string> arguments, string flag, string expectedValue)
    {
        int index = arguments.IndexOf(flag);
        AssertTrue(index >= 0, $"argument flag was not passed to Python: {flag}");
        AssertTrue(index + 1 < arguments.Count, $"argument flag did not include a value: {flag}");
        AssertEqual(expectedValue, arguments[index + 1]);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void AssertRectangleNearlyEqual(RectangleF expected, RectangleF actual, float tolerance)
    {
        AssertNearlyEqual(expected.X, actual.X, tolerance, "X");
        AssertNearlyEqual(expected.Y, actual.Y, tolerance, "Y");
        AssertNearlyEqual(expected.Width, actual.Width, tolerance, "Width");
        AssertNearlyEqual(expected.Height, actual.Height, tolerance, "Height");
    }

    private static void AssertNearlyEqual(float expected, float actual, float tolerance, string name)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException($"Expected {name} {expected}, got {actual}.");
        }
    }

    private sealed class RealYoloSmokeSettings
    {
        public string ArtifactRoot { get; set; }
        public string PythonExecutablePath { get; set; }
        public string ProjectRootPath { get; set; }
        public string ClientScriptPath { get; set; }
        public string ModelRootPath { get; set; }
        public string WeightsPath { get; set; }
        public string ImageRootPath { get; set; }
        public string ImagePath { get; set; }
        public string Device { get; set; }
        public int ImageSize { get; set; }
        public float MinimumConfidence { get; set; }
        public float Iou { get; set; }
    }
}
