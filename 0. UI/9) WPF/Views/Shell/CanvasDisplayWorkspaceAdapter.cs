using OpenCvSharp;
using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.Canvas;
using System.Linq;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.Views;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF-only display preview and settled canvas auto-fit callbacks.
    /// The Shell remains the owner of the active image and timer lifetime; this
    /// adapter receives those values through explicit providers.
    /// </summary>
    internal sealed class CanvasDisplayWorkspaceAdapter : IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly Func<DispatcherTimer> displayAdjustmentTimerProvider;
        private readonly Func<bool> isApplicationCloseApproved;
        private readonly Func<DrawingBitmap> activeImageBitmapProvider;
        private readonly Func<DrawingSize> activeImageSizeProvider;
        private readonly Func<string> activeImagePathProvider;
        private readonly WpfCanvasPanelViewModel canvasPanelViewModel;
        private readonly RoiImageCanvasViewModel mainCanvasViewModel;
        private readonly RoiImageCanvasView mainCanvasView;
        private readonly ImageDisplayAdjustmentService imageDisplayAdjustmentService;
        private readonly Func<LabelingProjectData> referenceDataProvider;
        private readonly Func<string> referenceRecipeProvider;
        private readonly Func<bool> isInferenceStageActive;
        private readonly Func<string, (bool Success, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>> Outlines, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>> PolygonFills, string StatusKey)> readSavedGroundTruth;
        private readonly PatchCoreHeatmapReviewService referenceHeatmapLoader = new PatchCoreHeatmapReviewService();
        private bool referenceSessionInitialized;
        private string referenceSessionName = string.Empty;
        private string referenceSessionRoot = string.Empty;
        private DrawingBitmap referenceSourceBitmap;
        private string referenceImagePath = string.Empty;
        private DrawingSize referenceImageSize;
        private string referenceOutputRoot = string.Empty;
        private LabelingDatasetPurpose referencePurpose;
        private System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>> savedGroundTruth = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>();
        private System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>> savedGroundTruthPolygonFills = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>>();
        private bool savedGroundTruthLoaded;
        private string groundTruthStatusKey = "WpfCanvas.Reference.GT.Empty";
        private DrawingBitmap heatmapBitmap;
        private YoloWorkerSmokeCandidate heatmapCandidate;
        private string heatmapFileIdentity = string.Empty;
        private string heatmapStatusKey = "WpfCanvas.Reference.HM.NoResult";
        private DrawingBitmap inferenceSourceBitmap;
        private string inferenceSourcePath = string.Empty;
        private string inferenceSourceIdentity = string.Empty;
        private LabelingProjectData inferenceSourceData;
        private bool inferenceUsesFullFrameWorker;
        private bool wasInferenceStageActive;
        private bool referenceDisplayDisposed;
        private bool referenceWasRendered;
        private bool groundTruthWasRendered;
        private bool heatmapWasRendered;
        private bool hasPatchCoreReference;

        private int canvasLayoutAutoFitVersion;
        private readonly Func<bool> isLabelingStageActive;
        private readonly Func<bool> keepZoomOnImageMove;
        private LabelingProjectData labelingViewData;
        private string labelingViewRecipe = string.Empty, labelingViewRoot = string.Empty, labelingViewImagePath = string.Empty;
        private CanvasViewState labelingViewState, labelingPanStart;
        private DrawingSize labelingViewViewport, labelingViewImageSize;
        private bool wasLabelingStageActive, labelingAutoFit = true, labelingRestorePending, isApplyingCanvasViewState;

        internal CanvasDisplayWorkspaceAdapter(
            Dispatcher dispatcher,
            Func<DispatcherTimer> displayAdjustmentTimerProvider,
            Func<bool> isApplicationCloseApproved,
            Func<DrawingBitmap> activeImageBitmapProvider,
            Func<DrawingSize> activeImageSizeProvider,
            Func<string> activeImagePathProvider,
            WpfCanvasPanelViewModel canvasPanelViewModel,
            RoiImageCanvasViewModel mainCanvasViewModel,
            RoiImageCanvasView mainCanvasView,
            ImageDisplayAdjustmentService imageDisplayAdjustmentService,
            Func<LabelingProjectData> referenceDataProvider = null,
            Func<string> referenceRecipeProvider = null,
            Func<bool> isInferenceStageActive = null,
            Func<string, (bool Success, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>> Outlines, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>> PolygonFills, string StatusKey)> readSavedGroundTruth = null,
            Func<bool> isLabelingStageActive = null,
            Func<bool> keepZoomOnImageMove = null)
        {
            this.referenceDataProvider = referenceDataProvider;
            this.referenceRecipeProvider = referenceRecipeProvider;
            this.isInferenceStageActive = isInferenceStageActive;
            this.readSavedGroundTruth = readSavedGroundTruth;
            this.isLabelingStageActive = isLabelingStageActive;
            this.keepZoomOnImageMove = keepZoomOnImageMove;
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.displayAdjustmentTimerProvider = displayAdjustmentTimerProvider
                ?? throw new ArgumentNullException(nameof(displayAdjustmentTimerProvider));
            this.isApplicationCloseApproved = isApplicationCloseApproved
                ?? throw new ArgumentNullException(nameof(isApplicationCloseApproved));
            this.activeImageBitmapProvider = activeImageBitmapProvider
                ?? throw new ArgumentNullException(nameof(activeImageBitmapProvider));
            this.activeImageSizeProvider = activeImageSizeProvider
                ?? throw new ArgumentNullException(nameof(activeImageSizeProvider));
            this.activeImagePathProvider = activeImagePathProvider
                ?? throw new ArgumentNullException(nameof(activeImagePathProvider));
            this.canvasPanelViewModel = canvasPanelViewModel
                ?? throw new ArgumentNullException(nameof(canvasPanelViewModel));
            this.mainCanvasViewModel = mainCanvasViewModel
                ?? throw new ArgumentNullException(nameof(mainCanvasViewModel));
            this.mainCanvasView = mainCanvasView
                ?? throw new ArgumentNullException(nameof(mainCanvasView));
            this.imageDisplayAdjustmentService = imageDisplayAdjustmentService
                ?? throw new ArgumentNullException(nameof(imageDisplayAdjustmentService));
            mainCanvasViewModel.ImageViewer.ZoomChanged += OnReferenceZoomChanged;
            mainCanvasViewModel.ImageViewer.Resized += HandleCanvasViewSizeChanged;
            mainCanvasViewModel.ImageViewer.MouseDoubleClicked += OnCanvasMouseDoubleClicked;
            mainCanvasViewModel.ImageViewer.MouseDown += OnCanvasMouseDown;
            mainCanvasViewModel.ImageViewer.MouseUp += OnCanvasMouseUp;
            BeginReferenceSession();
        }

        internal void BeginReferenceSession()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            ResetLabelingViewState();
            referenceSessionRoot = referenceDataProvider?.Invoke()?.OutputRootPath ?? string.Empty;
            referenceSessionName = referenceRecipeProvider?.Invoke() ?? string.Empty;
            referenceSessionInitialized = true;
            canvasPanelViewModel.ResetReferenceOverlayChoices();
            ClearReferenceImage();
        }

        internal void ClearReferenceImage()
        {
            dispatcher.VerifyAccess();
            savedGroundTruth = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>();
            savedGroundTruthPolygonFills = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>>();
            savedGroundTruthLoaded = false;
            groundTruthStatusKey = "WpfCanvas.Reference.GT.Empty";
            ReleaseHeatmap();
            heatmapCandidate = null;
            heatmapFileIdentity = string.Empty;
            heatmapStatusKey = "WpfCanvas.Reference.HM.NoResult";
            hasPatchCoreReference = false;
            referenceSourceBitmap = null;
            referenceImagePath = string.Empty;
            referenceImageSize = DrawingSize.Empty;
            inferenceSourceBitmap = null;
            inferenceSourceData = null;
            inferenceUsesFullFrameWorker = false;
            inferenceSourcePath = inferenceSourceIdentity = string.Empty;
            canvasPanelViewModel.SetReferenceOverlayContext(false, false);
            canvasPanelViewModel.SetReferenceOverlayStatus(groundTruthStatusKey, heatmapStatusKey);
            if (referenceWasRendered && !referenceDisplayDisposed && !isApplicationCloseApproved() && HasActiveImage())
            {
                using DrawingBitmap baseCopy = imageDisplayAdjustmentService.CreateAdjustedCopy(activeImageBitmapProvider(), canvasPanelViewModel.GetDisplayAdjustmentOptions());
                UploadDisplayCopy(baseCopy);
                groundTruthWasRendered = heatmapWasRendered = referenceWasRendered = false;
            }
        }

        internal void InvalidateSavedGroundTruth()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            savedGroundTruthLoaded = false;
            savedGroundTruth = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>();
            savedGroundTruthPolygonFills = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>>();
            RefreshReferenceDisplay();
        }

        internal void BeginInferenceReference(string imagePath, PythonModelSettings requestSettings = null)
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            SynchronizeReferenceContext();
            string path = string.IsNullOrWhiteSpace(imagePath) ? activeImagePathProvider() : imagePath;
            if (!SameReferencePath(path, activeImagePathProvider())) return;
            ReleaseHeatmap();
            heatmapCandidate = null;
            heatmapFileIdentity = string.Empty;
            inferenceSourceBitmap = activeImageBitmapProvider();
            inferenceSourceData = referenceDataProvider?.Invoke();
            PythonModelSettings settings = requestSettings;
            bool patchCoreRequest = settings != null && isInferenceStageActive?.Invoke() == true
                && inferenceSourceData?.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection
                && PythonModelSettings.NormalizeModelEngine(settings.ModelEngine) == PythonModelSettings.EnginePatchCore;
            inferenceUsesFullFrameWorker = false;
            inferenceSourcePath = path;
            inferenceSourceIdentity = string.Empty;
            if (patchCoreRequest)
            {
                string bundled = PythonModelRuntimeBundledWorkerService.ResolvePatchCoreWorkerScriptPath();
                string worker = string.IsNullOrWhiteSpace(settings.ClientScriptPath) ? bundled : settings.ClientScriptPath;
                inferenceUsesFullFrameWorker = SameReferencePath(worker, bundled) && System.IO.File.Exists(bundled)
                    && TryMatchCanonicalSource(path, inferenceSourceBitmap, out inferenceSourceIdentity);
            }
            heatmapStatusKey = inferenceUsesFullFrameWorker ? "WpfCanvas.Reference.HM.Running"
                : patchCoreRequest ? "WpfCanvas.Reference.SourceUnknown" : "WpfCanvas.Reference.HM.NoResult";
            RefreshReferenceDisplay();
        }

        private static bool TryMatchCanonicalSource(string imagePath, DrawingBitmap canonical, out string identity)
        {
            identity = string.Empty;
            if (canonical == null) return false;
            try
            {
                string before = TryReadReferenceIdentity(imagePath);
                if (string.IsNullOrEmpty(before)) return false;
                using DrawingBitmap decoded = AppImageLoader.LoadBitmap(imagePath);
                if (decoded.Size != canonical.Size) return false;
                var bounds = new System.Drawing.Rectangle(0, 0, canonical.Width, canonical.Height);
                using DrawingBitmap fileCopy = decoded.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using DrawingBitmap memoryCopy = canonical.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using Mat fileMat = BitmapMatConversionService.CopyToMat(fileCopy);
                using Mat memoryMat = BitmapMatConversionService.CopyToMat(memoryCopy);
                if (Cv2.Norm(fileMat, memoryMat, NormTypes.INF) != 0D) return false;
                string after = TryReadReferenceIdentity(imagePath);
                if (!string.Equals(before, after, StringComparison.OrdinalIgnoreCase)) return false;
                identity = after;
                return true;
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is OutOfMemoryException || exception is System.Runtime.InteropServices.ExternalException || exception is OpenCVException)
            {
                return false;
            }
        }

        internal void FinishInferenceReference(string imagePath)
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            string path = string.IsNullOrWhiteSpace(imagePath) ? inferenceSourcePath : imagePath;
            if (SameReferencePath(path, activeImagePathProvider()) && heatmapStatusKey == "WpfCanvas.Reference.HM.Running")
            {
                heatmapStatusKey = "WpfCanvas.Reference.HM.Stopped";
                RefreshReferenceDisplay();
            }
        }

        internal void AcceptInferenceReference(YoloWorkerSmokeTestResult result)
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            SynchronizeReferenceContext();
            if (result == null || !SameReferencePath(result.ImagePath, activeImagePathProvider())) return;
            ReleaseHeatmap();
            heatmapCandidate = null;
            heatmapFileIdentity = string.Empty;
            hasPatchCoreReference = result.Source != null
                && PythonModelSettings.NormalizeModelEngine(result.Source.ModelEngine) == PythonModelSettings.EnginePatchCore
                || (result.Candidates ?? Array.Empty<YoloWorkerSmokeCandidate>()).Any(item => item != null
                    && string.Equals(item.PredictionType, "patchcore", StringComparison.OrdinalIgnoreCase));
            if (!result.Succeeded)
            {
                heatmapStatusKey = "WpfCanvas.Reference.HM.Failed";
                RefreshReferenceDisplay();
                return;
            }
            if (!hasPatchCoreReference)
            {
                heatmapStatusKey = "WpfCanvas.Reference.HM.NoResult";
                RefreshReferenceDisplay();
                return;
            }
            bool current = inferenceUsesFullFrameWorker && result.Source != null
                && PythonModelSettings.NormalizeModelEngine(result.Source.ModelEngine) == PythonModelSettings.EnginePatchCore
                && SameReferencePath(result.Source.ImagePath, activeImagePathProvider())
                && SameReferencePath(inferenceSourcePath, activeImagePathProvider())
                && ReferenceEquals(inferenceSourceBitmap, activeImageBitmapProvider())
                && activeImageBitmapProvider()?.Size == activeImageSizeProvider()
                && ReferenceEquals(inferenceSourceData, referenceDataProvider?.Invoke())
                && !string.IsNullOrEmpty(inferenceSourceIdentity)
                && string.Equals(inferenceSourceIdentity, result.CaptureImageSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(inferenceSourceIdentity, TryReadReferenceIdentity(activeImagePathProvider()), StringComparison.OrdinalIgnoreCase);
            string[] paths = (result.Candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(item => item != null && string.Equals(item.PredictionType, "patchcore", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.HeatmapPath).Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!current || paths.Length != 1)
            {
                heatmapStatusKey = "WpfCanvas.Reference.SourceUnknown";
                RefreshReferenceDisplay();
                return;
            }
            heatmapFileIdentity = TryReadReferenceIdentity(paths[0]);
            heatmapCandidate = new YoloWorkerSmokeCandidate { PredictionType = "patchcore", HeatmapPath = paths[0] };
            heatmapStatusKey = string.IsNullOrEmpty(heatmapFileIdentity) ? "WpfCanvas.Reference.HM.Missing" : "WpfCanvas.Reference.HM.Ready";
            RefreshReferenceDisplay();
        }

        internal void RefreshReferenceDisplay()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            SynchronizeReferenceContext();
            SynchronizeLabelingViewContext();
            mainCanvasViewModel.ImageViewer.ClearColorOverride = isLabelingStageActive?.Invoke() == true
                ? System.Drawing.Color.FromArgb(11, 14, 18) : null;
            bool hidden = isInferenceStageActive?.Invoke() != true || canvasPanelViewModel.IsTemporaryOriginalView;
            canvasPanelViewModel.SetReferenceOverlayStatus(
                !canvasPanelViewModel.ShowSavedGroundTruth ? "WpfCanvas.Reference.Off" : hidden ? "WpfCanvas.Reference.Hidden" : groundTruthStatusKey,
                !canvasPanelViewModel.ShowInferenceHeatmap ? "WpfCanvas.Reference.Off" : hidden ? "WpfCanvas.Reference.Hidden" : heatmapStatusKey);
            bool removeRenderedEvidence = referenceWasRendered && hidden
                || groundTruthWasRendered && (!canvasPanelViewModel.ShowSavedGroundTruth || !canvasPanelViewModel.IsSavedGroundTruthAvailable || savedGroundTruth.Count == 0)
                || heatmapWasRendered && (!canvasPanelViewModel.ShowInferenceHeatmap || !canvasPanelViewModel.IsInferenceHeatmapAvailable
                    || heatmapCandidate == null || heatmapBitmap == null);
            if (removeRenderedEvidence)
            {
                ApplyDisplayAdjustmentNow();
                return;
            }
            if (isInferenceStageActive?.Invoke() == true || canvasPanelViewModel.IsDisplayAdjustmentActive || referenceWasRendered)
                ScheduleDisplayAdjustmentRefresh();
        }

        private void SynchronizeReferenceContext()
        {
            LabelingProjectData data = referenceDataProvider?.Invoke();
            string recipe = referenceRecipeProvider?.Invoke() ?? string.Empty;
            if (!referenceSessionInitialized || !string.Equals(referenceSessionName, recipe, StringComparison.Ordinal)
                || !string.Equals(referenceSessionRoot, data?.OutputRootPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                BeginReferenceSession();
            DrawingBitmap bitmap = activeImageBitmapProvider();
            DrawingSize size = activeImageSizeProvider();
            string path = activeImagePathProvider() ?? string.Empty;
            string root = data?.OutputRootPath ?? string.Empty;
            LabelingDatasetPurpose purpose = data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
            bool inference = isInferenceStageActive?.Invoke() == true;
            if (!ReferenceEquals(referenceSourceBitmap, bitmap) || !SameReferencePath(referenceImagePath, path)
                || referenceImageSize != size
                || !string.Equals(referenceOutputRoot, root, StringComparison.OrdinalIgnoreCase) || referencePurpose != purpose
                || wasInferenceStageActive && !inference)
            {
                ClearReferenceImage();
                referenceSourceBitmap = bitmap;
                referenceImagePath = path;
                referenceImageSize = size;
                referenceOutputRoot = root;
                referencePurpose = purpose;
            }
            wasInferenceStageActive = inference;
            bool frameReady = HasActiveImage() && bitmap.Size == size;
            canvasPanelViewModel.SetReferenceOverlayContext(inference && (purpose == LabelingDatasetPurpose.ObjectDetection || purpose == LabelingDatasetPurpose.Segmentation),
                inference && purpose == LabelingDatasetPurpose.AnomalyDetection
                && (hasPatchCoreReference || PythonModelSettings.NormalizeModelEngine(data?.ProjectSettings?.PythonModel?.ModelEngine) == PythonModelSettings.EnginePatchCore),
                frameReady);
            if (!frameReady)
            {
                groundTruthStatusKey = heatmapStatusKey = bitmap == null ? "WpfCanvas.Reference.NoImage" : "WpfCanvas.Reference.SourceUnknown";
            }
            if (!canvasPanelViewModel.ShowSavedGroundTruth)
            {
                savedGroundTruth = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>();
                savedGroundTruthPolygonFills = Array.Empty<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>>();
                savedGroundTruthLoaded = false;
            }
            else if (canvasPanelViewModel.IsSavedGroundTruthAvailable && !savedGroundTruthLoaded && readSavedGroundTruth != null)
            {
                var loaded = readSavedGroundTruth(path);
                savedGroundTruth = loaded.Success ? loaded.Outlines : Array.Empty<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>();
                savedGroundTruthPolygonFills = loaded.Success ? loaded.PolygonFills : Array.Empty<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>>();
                groundTruthStatusKey = loaded.StatusKey;
                savedGroundTruthLoaded = true;
            }
            if (!canvasPanelViewModel.ShowInferenceHeatmap) ReleaseHeatmap();
        }

        internal void PrepareReferenceHeatmap()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            SynchronizeReferenceContext();
            if (heatmapBitmap != null || heatmapCandidate == null || !canvasPanelViewModel.ShowInferenceHeatmap
                || !canvasPanelViewModel.IsInferenceHeatmapAvailable
                || canvasPanelViewModel.IsTemporaryOriginalView || string.IsNullOrEmpty(heatmapFileIdentity)) return;
            string before = TryReadReferenceIdentity(heatmapCandidate.HeatmapPath);
            if (string.IsNullOrEmpty(before)) { heatmapStatusKey = "WpfCanvas.Reference.HM.Missing"; return; }
            if (!string.Equals(before, heatmapFileIdentity, StringComparison.Ordinal))
            { heatmapStatusKey = "WpfCanvas.Reference.HM.Changed"; return; }
            PatchCoreHeatmapLoadResult loaded = referenceHeatmapLoader.Load(heatmapCandidate);
            if (!loaded.Succeeded || loaded.ImageSource is not System.Windows.Media.Imaging.BitmapSource source)
            { heatmapStatusKey = "WpfCanvas.Reference.HM.Unreadable"; return; }
            DrawingSize size = activeImageSizeProvider();
            if (Math.Abs((long)source.PixelWidth * size.Height - (long)source.PixelHeight * size.Width) > Math.Max(size.Width, size.Height)
                || !string.Equals(before, TryReadReferenceIdentity(heatmapCandidate.HeatmapPath), StringComparison.Ordinal))
            { heatmapStatusKey = "WpfCanvas.Reference.SourceUnknown"; return; }
            // Keep WPF conversion local and use standard in-memory image conversion;
            // Bitmap-to-Mat pixel access remains with BitmapMatConversionService.
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using var memory = new System.IO.MemoryStream();
            encoder.Save(memory);
            memory.Position = 0;
            using var decoded = new DrawingBitmap(memory);
            heatmapBitmap = new DrawingBitmap(decoded);
        }

        private void UploadDisplayCopy(DrawingBitmap displayCopy)
        {
            using Mat displayMat = BitmapMatConversionService.CopyToMat(displayCopy);
            DrawingSize uploadSize = activeImageSizeProvider();
            CanvasImageLoader.UploadMatAsTexture(mainCanvasViewModel.ImageViewer, displayMat,
                string.IsNullOrWhiteSpace(activeImagePathProvider()) ? "display-preview" : System.IO.Path.GetFileName(activeImagePathProvider()),
                ref uploadSize, zoomToFit: false, replaceExistingTextures: true);
        }

        private void OnReferenceZoomChanged(object sender, EventArgs e)
        {
            if (!referenceDisplayDisposed && !isApplicationCloseApproved() && !isApplyingCanvasViewState && isLabelingStageActive?.Invoke() == true)
            {
                labelingAutoFit = false;
                labelingViewViewport = mainCanvasViewModel.ImageViewer.GetSize();
            }
            if (!referenceDisplayDisposed && !isApplicationCloseApproved() && canvasPanelViewModel.ShowSavedGroundTruth
                && savedGroundTruth.Count > 0) ScheduleDisplayAdjustmentRefresh();
        }

        private void ReleaseHeatmap()
        {
            heatmapBitmap?.Dispose();
            heatmapBitmap = null;
        }

        private static string TryReadReferenceIdentity(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return string.Empty;
                using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
                return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
            }
            catch (Exception error) when (error is System.IO.IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            { return string.Empty; }
        }

        private static bool SameReferencePath(string left, string right)
        {
            try { return string.Equals(System.IO.Path.GetFullPath(left ?? string.Empty), System.IO.Path.GetFullPath(right ?? string.Empty), StringComparison.OrdinalIgnoreCase); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is System.IO.IOException)
            { return string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right); }
        }

        public void Dispose()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed) return;
            displayAdjustmentTimerProvider().Stop();
            canvasLayoutAutoFitVersion++;
            mainCanvasViewModel.ImageViewer.ZoomChanged -= OnReferenceZoomChanged;
            mainCanvasViewModel.ImageViewer.Resized -= HandleCanvasViewSizeChanged;
            mainCanvasViewModel.ImageViewer.MouseDoubleClicked -= OnCanvasMouseDoubleClicked;
            mainCanvasViewModel.ImageViewer.MouseDown -= OnCanvasMouseDown;
            mainCanvasViewModel.ImageViewer.MouseUp -= OnCanvasMouseUp;
            ResetLabelingViewState();
            ClearReferenceImage();
            mainCanvasViewModel.ImageViewer.ClearColorOverride = null;
            referenceDisplayDisposed = true;
        }

        #region DisplayAdjustment
        internal void ScheduleDisplayAdjustmentRefresh()
        {
            if (referenceDisplayDisposed || isApplicationCloseApproved()) return;
            DispatcherTimer timer = displayAdjustmentTimerProvider();
            timer.Stop();
            if (!HasActiveImage())
            {
                return;
            }

            timer.Start();
        }

        internal void HandleDisplayAdjustmentRefreshTimerTick(object sender, EventArgs e)
        {
            DispatcherTimer timer = displayAdjustmentTimerProvider();
            timer.Stop();
            if (isApplicationCloseApproved())
            {
                return;
            }

            ApplyDisplayAdjustmentNow();
        }

        internal void ApplyDisplayAdjustmentNow()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved() || !HasActiveImage()) return;
            using DrawingBitmap adjusted = CreateReferenceDisplayCopy(out bool showGroundTruth, out bool showHeatmap);
            UploadDisplayCopy(adjusted);
            groundTruthWasRendered = showGroundTruth;
            heatmapWasRendered = showHeatmap;
            referenceWasRendered = showGroundTruth || showHeatmap;
            bool show = isInferenceStageActive?.Invoke() == true && !canvasPanelViewModel.IsTemporaryOriginalView;
            string gtStatus = !canvasPanelViewModel.ShowSavedGroundTruth ? "WpfCanvas.Reference.Off"
                : !show ? "WpfCanvas.Reference.Hidden" : showGroundTruth ? "WpfCanvas.Reference.GT.Shown" : groundTruthStatusKey;
            string hmStatus = !canvasPanelViewModel.ShowInferenceHeatmap ? "WpfCanvas.Reference.Off"
                : !show ? "WpfCanvas.Reference.Hidden" : showHeatmap ? "WpfCanvas.Reference.HM.Shown" : heatmapStatusKey;
            canvasPanelViewModel.SetReferenceOverlayStatus(gtStatus, hmStatus);
        }

        // The caller owns this display copy; upload and canonical pixels retain their existing owners.
        internal DrawingBitmap CreateReferenceDisplayCopy(out bool showGroundTruth, out bool showHeatmap)
        {
            dispatcher.VerifyAccess();
            SynchronizeReferenceContext();
            bool show = isInferenceStageActive?.Invoke() == true && !canvasPanelViewModel.IsTemporaryOriginalView;
            if (show)
            {
                try { PrepareReferenceHeatmap(); }
                catch (Exception error) when (error is System.IO.IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException)
                { ReleaseHeatmap(); heatmapStatusKey = "WpfCanvas.Reference.HM.Unreadable"; }
            }
            DrawingBitmap adjusted = imageDisplayAdjustmentService.CreateAdjustedCopy(activeImageBitmapProvider(), canvasPanelViewModel.GetDisplayAdjustmentOptions());
            try
            {
                bool showSavedGroundTruth = show && canvasPanelViewModel.IsSavedGroundTruthAvailable && canvasPanelViewModel.ShowSavedGroundTruth;
                var outlines = showSavedGroundTruth ? savedGroundTruth : Array.Empty<System.Collections.Generic.IReadOnlyList<System.Drawing.Point>>();
                DrawingBitmap heatmap = show && canvasPanelViewModel.IsInferenceHeatmapAvailable && canvasPanelViewModel.ShowInferenceHeatmap ? heatmapBitmap : null;
                imageDisplayAdjustmentService.CompositeReadOnlyEvidence(adjusted, heatmap, CanvasLayerPresentationWorkflowService.DefaultOverlayOpacity, outlines, mainCanvasViewModel.ImageViewer.ZoomScale, showSavedGroundTruth ? savedGroundTruthPolygonFills : null);
                showGroundTruth = outlines.Count > 0;
                showHeatmap = heatmap != null;
                return adjusted;
            }
            catch
            {
                adjusted.Dispose();
                throw;
            }
        }
        #endregion

        #region CanvasLayout
        internal bool TryApplyInferenceFit(DrawingSize? uploadedImageSize = null)
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved() || isInferenceStageActive?.Invoke() != true) return false;
            return TryApplyCanvasFit(uploadedImageSize ?? activeImageSizeProvider(), 10D, 1D);
        }

        internal bool TryApplyLabelingFit(DrawingSize? uploadedImageSize = null)
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved() || isLabelingStageActive?.Invoke() != true) return false;
            SynchronizeLabelingViewContext();
            DrawingSize size = uploadedImageSize ?? activeImageSizeProvider();
            if (!TryApplyCanvasFit(size, 0D, .94D)) return false;
            labelingAutoFit = true;
            labelingRestorePending = false;
            labelingPanStart = null;
            canvasLayoutAutoFitVersion++;
            RememberLabelingView(activeImagePathProvider(), size);
            return true;
        }

        // The upload and canonical-image ownership remain with the existing caller.
        // This scope suppresses only view-change feedback from its legacy upload fit.
        internal void LoadCanvasImage(Action upload, string imagePath, DrawingSize imageSize)
        {
            dispatcher.VerifyAccess();
            ArgumentNullException.ThrowIfNull(upload);
            if (referenceDisplayDisposed || isApplicationCloseApproved()) { upload(); return; }
            SynchronizeLabelingViewContext();
            bool labeling = isLabelingStageActive?.Invoke() == true;
            if (labeling && !labelingRestorePending && HasActiveImage()) RememberLabelingView(activeImagePathProvider(), activeImageSizeProvider());
            labelingPanStart = null;
            CanvasViewState previous = labelingViewState;
            DrawingSize previousViewport = labelingViewViewport, previousImage = labelingViewImageSize;
            bool previousApplying = isApplyingCanvasViewState;
            isApplyingCanvasViewState = true;
            try { upload(); }
            finally { isApplyingCanvasViewState = previousApplying; }
            if (!labeling) { TryApplyInferenceFit(imageSize); return; }
            if (previous == null || keepZoomOnImageMove?.Invoke() != true) labelingAutoFit = true;
            bool applied = labelingAutoFit ? TryApplyLabelingFit(imageSize)
                : TryPreserveLabelingView(previous, previousViewport, previousImage, imageSize);
            if (applied) { labelingRestorePending = false; RememberLabelingView(imagePath, imageSize); }
            else labelingRestorePending = true;
        }

        // Inference uses the established physical10DIP inset; Labeling uses94%
        // of the actual viewport. The shared OpenGL state remains world-units/pixel.
        private bool TryApplyCanvasFit(DrawingSize imageSize, double insetDip, double fraction)
        {
            DrawingSize viewport = mainCanvasViewModel.ImageViewer.GetSize();
            DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(mainCanvasView);
            double availableWidth = viewport.Width - 2D * insetDip * dpi.DpiScaleX;
            double availableHeight = viewport.Height - 2D * insetDip * dpi.DpiScaleY;
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || availableWidth <= 0D || availableHeight <= 0D) return false;
            float worldScale = (float)(1D / (Math.Min(availableWidth / imageSize.Width, availableHeight / imageSize.Height) * fraction));
            ApplyManagedViewState(new CanvasViewState(mainCanvasViewModel.ImageViewer.GetControlMinSize() * worldScale,
                new SizeF((viewport.Width * worldScale - imageSize.Width) / 2F, (viewport.Height * worldScale - imageSize.Height) / 2F)));
            return true;
        }

        private void ApplyManagedViewState(CanvasViewState state)
        {
            bool previous = isApplyingCanvasViewState;
            isApplyingCanvasViewState = true;
            try { mainCanvasViewModel.ImageViewer.ApplyViewState(state); }
            finally { isApplyingCanvasViewState = previous; }
        }

        private bool TryPreserveLabelingView(CanvasViewState previous, DrawingSize previousViewport, DrawingSize previousImage, DrawingSize imageSize)
        {
            DrawingSize viewport = mainCanvasViewModel.ImageViewer.GetSize();
            int oldMinimum = Math.Min(previousViewport.Width, previousViewport.Height), minimum = Math.Min(viewport.Width, viewport.Height);
            if (previous == null || !float.IsFinite(previous.Zoom) || previous.Zoom <= 0 || oldMinimum <= 0 || minimum <= 0
                || previousImage.Width <= 0 || previousImage.Height <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0) return false;
            float worldScale = previous.Zoom / oldMinimum;
            // Preserve SVG-like screen scale/translation despite OpenGL's bottom origin
            // and a new image height or viewport height.
            ApplyManagedViewState(new CanvasViewState(minimum * worldScale, new SizeF(previous.OffsetSize.Width,
                previous.OffsetSize.Height + previousImage.Height - imageSize.Height + (viewport.Height - previousViewport.Height) * worldScale)));
            return true;
        }

        private void RememberLabelingView(string imagePath, DrawingSize imageSize)
        {
            DrawingSize viewport = mainCanvasViewModel.ImageViewer.GetSize();
            if (string.IsNullOrWhiteSpace(imagePath) || imageSize.Width <= 0 || imageSize.Height <= 0 || viewport.Width <= 0 || viewport.Height <= 0) return;
            labelingViewState = mainCanvasViewModel.ImageViewer.CaptureViewState();
            labelingViewViewport = viewport;
            labelingViewImageSize = imageSize;
            labelingViewImagePath = imagePath;
        }

        private void ResetLabelingViewState()
        {
            canvasLayoutAutoFitVersion++;
            labelingViewData = null;
            labelingViewRecipe = labelingViewRoot = labelingViewImagePath = string.Empty;
            labelingViewState = labelingPanStart = null;
            labelingViewViewport = labelingViewImageSize = DrawingSize.Empty;
            labelingAutoFit = true;
            labelingRestorePending = wasLabelingStageActive = false;
        }

        private void SynchronizeLabelingViewContext()
        {
            LabelingProjectData data = referenceDataProvider?.Invoke();
            string recipe = referenceRecipeProvider?.Invoke() ?? string.Empty, root = data?.OutputRootPath ?? string.Empty;
            if (!ReferenceEquals(labelingViewData, data) || !string.Equals(labelingViewRecipe, recipe, StringComparison.Ordinal)
                || !string.Equals(labelingViewRoot, root, StringComparison.OrdinalIgnoreCase) || labelingViewState != null && !HasActiveImage())
            {
                ResetLabelingViewState();
                labelingViewData = data; labelingViewRecipe = recipe; labelingViewRoot = root;
            }
            bool labeling = isLabelingStageActive?.Invoke() == true;
            if (wasLabelingStageActive && !labeling)
            {
                RememberLabelingView(activeImagePathProvider(), activeImageSizeProvider());
                labelingPanStart = null;
            }
            if (!wasLabelingStageActive && labeling)
            {
                labelingRestorePending = true;
                ScheduleCanvasAutoFit();
            }
            wasLabelingStageActive = labeling;
        }

        internal bool TryApplyLabelingLayout()
        {
            dispatcher.VerifyAccess();
            if (referenceDisplayDisposed || isApplicationCloseApproved() || isLabelingStageActive?.Invoke() != true || !HasActiveImage()) return false;
            SynchronizeLabelingViewContext();
            DrawingSize imageSize = activeImageSizeProvider();
            string path = activeImagePathProvider();
            bool changedImage = labelingViewState != null && (!SameReferencePath(labelingViewImagePath, path) || labelingViewImageSize != imageSize);
            if (changedImage && keepZoomOnImageMove?.Invoke() != true) labelingAutoFit = true;
            if (labelingAutoFit || labelingViewState == null) return TryApplyLabelingFit();
            CanvasViewState previous = labelingRestorePending || changedImage ? labelingViewState : mainCanvasViewModel.ImageViewer.CaptureViewState();
            DrawingSize previousImage = labelingRestorePending || changedImage ? labelingViewImageSize : imageSize;
            if (!TryPreserveLabelingView(previous, labelingViewViewport, previousImage, imageSize)) return false;
            labelingRestorePending = false;
            RememberLabelingView(path, imageSize);
            return true;
        }

        private void OnCanvasMouseDown(object sender, CanvasMouseEventArgs e)
        {
            if (referenceDisplayDisposed || isApplicationCloseApproved() || isLabelingStageActive?.Invoke() != true) return;
            if (e.Button != System.Windows.Forms.MouseButtons.None && mainCanvasViewModel.ImageViewer.GetViewMode() == CanvasInteractionMode.Drag)
                labelingPanStart = mainCanvasViewModel.ImageViewer.CaptureViewState();
        }

        private void OnCanvasMouseUp(object sender, CanvasMouseEventArgs e)
        {
            if (referenceDisplayDisposed || isApplicationCloseApproved() || labelingPanStart == null) return;
            CanvasViewState start = labelingPanStart;
            labelingPanStart = null;
            if (isLabelingStageActive?.Invoke() != true) return;
            CanvasViewState current = mainCanvasViewModel.ImageViewer.CaptureViewState();
            if (current.Zoom != start.Zoom || current.OffsetSize != start.OffsetSize) labelingAutoFit = false;
            RememberLabelingView(activeImagePathProvider(), activeImageSizeProvider());
        }

        private void OnCanvasMouseDoubleClicked(object sender, EventArgs e)
        {
            if (!TryApplyInferenceFit()) TryApplyLabelingFit();
        }

        internal void HandleCanvasViewSizeChanged(object sender, EventArgs e)
        {
            if (referenceDisplayDisposed || !HasActiveImage() || e is SizeChangedEventArgs size && !size.WidthChanged && !size.HeightChanged) return;
            ScheduleCanvasAutoFit();
        }

        private void ScheduleCanvasAutoFit()
        {
            int requestVersion = ++canvasLayoutAutoFitVersion;
            dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => ApplyScheduledCanvasAutoFit(requestVersion)));
        }

        private void ApplyScheduledCanvasAutoFit(int requestVersion)
        {
            if (referenceDisplayDisposed || isApplicationCloseApproved() || requestVersion != canvasLayoutAutoFitVersion || !HasActiveImage()
                || !mainCanvasView.IsVisible || mainCanvasView.ActualWidth <= 1D || mainCanvasView.ActualHeight <= 1D || labelingPanStart != null) return;
            SynchronizeLabelingViewContext();
            if (isLabelingStageActive?.Invoke() == true) { TryApplyLabelingLayout(); return; }
            if (!TryApplyInferenceFit()) mainCanvasViewModel.ImageViewer.ZoomToFit();
        }
        #endregion

        private bool HasActiveImage()
        {
            return activeImageBitmapProvider() != null && !activeImageSizeProvider().IsEmpty;
        }
    }
}
