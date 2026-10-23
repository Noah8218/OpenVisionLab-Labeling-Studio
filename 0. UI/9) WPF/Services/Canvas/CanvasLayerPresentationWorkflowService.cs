using System;
using System.Globalization;
using OpenVisionLab;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas layer mode state and turns it into the text/visibility
    /// snapshot consumed by the WPF binding adapter.
    /// </summary>
    public sealed class CanvasLayerPresentationWorkflowService
    {
        public const double DefaultOverlayOpacity = 0.45D;
        public const double DefaultLabelOverlayOpacity = 0.30D;

        private WpfCanvasDisplayMode displayMode = WpfCanvasDisplayMode.LabelsOnly;
        private bool showSavedGroundTruth;
        private bool showInferenceHeatmap = true;
        private int labelCount;
        private int inferenceCandidateCount;
        private bool hasUnsavedLabelChanges;
        private bool isTemporaryOriginalView;
        private bool areOverlaysVisible = true;
        private double overlayOpacity = DefaultLabelOverlayOpacity;

        public bool ShowSavedGroundTruth => showSavedGroundTruth;
        public bool ShowInferenceHeatmap => showInferenceHeatmap;

        public WpfCanvasDisplayMode CurrentMode => displayMode;

        public bool IsTemporaryOriginalView => isTemporaryOriginalView;

        public bool AreOverlaysVisible => areOverlaysVisible;

        public double OverlayOpacity => overlayOpacity;


        public bool SetSavedGroundTruth(bool value)
        {
            if (showSavedGroundTruth == value) return false;
            showSavedGroundTruth = value;
            return true;
        }

        public bool SetInferenceHeatmap(bool value)
        {
            if (showInferenceHeatmap == value) return false;
            showInferenceHeatmap = value;
            return true;
        }

        public void ResetReferenceOverlayChoices()
        {
            showSavedGroundTruth = false;
            showInferenceHeatmap = true;
        }

        public CanvasLayerPresentationSnapshot SetDisplayMode(WpfCanvasDisplayMode mode)
        {
            displayMode = mode;
            return BuildSnapshot();
        }

        public CanvasLayerPresentationSnapshot SetOverlaysVisible(bool value)
        {
            areOverlaysVisible = value;
            return BuildSnapshot();
        }

        public CanvasLayerPresentationSnapshot SetOverlayOpacity(double value)
        {
            overlayOpacity = Math.Clamp(value, 0D, 1D);
            return BuildSnapshot();
        }

        public CanvasLayerPresentationSnapshot SetState(
            WpfCanvasDisplayMode mode,
            int labelCount,
            int inferenceCandidateCount,
            bool hasUnsavedLabelChanges)
        {
            SetDisplayMode(mode);
            this.labelCount = Math.Max(0, labelCount);
            this.inferenceCandidateCount = Math.Max(0, inferenceCandidateCount);
            this.hasUnsavedLabelChanges = hasUnsavedLabelChanges;
            return BuildSnapshot();
        }

        public CanvasLayerPresentationSnapshot GetSnapshot()
        {
            return BuildSnapshot();
        }

        public CanvasLayerPresentationSnapshot BeginTemporaryOriginalView()
        {
            isTemporaryOriginalView = true;
            return BuildSnapshot();
        }

        public CanvasLayerPresentationSnapshot EndTemporaryOriginalView()
        {
            isTemporaryOriginalView = false;
            return BuildSnapshot();
        }

        private CanvasLayerPresentationSnapshot BuildSnapshot()
        {
            bool showLabels = areOverlaysVisible && !isTemporaryOriginalView && displayMode != WpfCanvasDisplayMode.InferenceOnly;
            bool showInference = areOverlaysVisible && !isTemporaryOriginalView && displayMode != WpfCanvasDisplayMode.LabelsOnly;
            string unsavedSuffix = hasUnsavedLabelChanges
                ? Translate("WpfCanvas.Layer.UnsavedSuffix")
                : string.Empty;
            string labelText = Format(
                showLabels ? "WpfCanvas.Layer.Labels.Shown" : "WpfCanvas.Layer.Labels.Hidden",
                labelCount,
                unsavedSuffix);
            string inferenceText = Format(
                showInference ? "WpfCanvas.Layer.Candidates.Shown" : "WpfCanvas.Layer.Candidates.Hidden",
                inferenceCandidateCount);
            string titleKey;
            string detailKey;
            if (isTemporaryOriginalView)
            {
                titleKey = "WpfCanvas.LayerMode.Original.Title";
                detailKey = "WpfCanvas.LayerMode.Original.Detail";
            }
            else switch (displayMode)
            {
                case WpfCanvasDisplayMode.InferenceOnly:
                    titleKey = "WpfCanvas.LayerMode.Inference.Title";
                    detailKey = "WpfCanvas.LayerMode.Inference.Detail";
                    break;

                case WpfCanvasDisplayMode.Both:
                    titleKey = "WpfCanvas.LayerMode.Both.Title";
                    detailKey = "WpfCanvas.LayerMode.Both.Detail";
                    break;

                default:
                    titleKey = "WpfCanvas.LayerMode.Labels.Title";
                    detailKey = "WpfCanvas.LayerMode.Labels.Detail";
                    break;
            }

            string title = Translate(titleKey);
            string detail = Translate(detailKey);
            return new CanvasLayerPresentationSnapshot(
                displayMode,
                isTemporaryOriginalView,
                areOverlaysVisible,
                overlayOpacity,
                labelCount,
                inferenceCandidateCount,
                hasUnsavedLabelChanges,
                showLabels,
                showInference,
                title,
                detail,
                labelText,
                inferenceText,
                $"{detail}\n{labelText}\n{inferenceText}");
        }

        private static string Translate(string key)
        {
            return OpenVisionLanguageService.T(key);
        }

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                Translate(key),
                arguments ?? Array.Empty<object>());
        }
    }

    public sealed class CanvasLayerPresentationSnapshot
    {
        public CanvasLayerPresentationSnapshot(
            WpfCanvasDisplayMode mode,
            bool isTemporaryOriginalView,
            bool areOverlaysVisible,
            double overlayOpacity,
            int labelCount,
            int inferenceCandidateCount,
            bool hasUnsavedLabelChanges,
            bool showLabels,
            bool showInference,
            string title,
            string detail,
            string labelText,
            string inferenceText,
            string toolTip)
        {
            Mode = mode;
            IsTemporaryOriginalView = isTemporaryOriginalView;
            AreOverlaysVisible = areOverlaysVisible;
            OverlayOpacity = Math.Clamp(overlayOpacity, 0D, 1D);
            LabelCount = labelCount;
            InferenceCandidateCount = inferenceCandidateCount;
            HasUnsavedLabelChanges = hasUnsavedLabelChanges;
            ShowLabels = showLabels;
            ShowInference = showInference;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            InferenceText = inferenceText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
        }

        public WpfCanvasDisplayMode Mode { get; }

        public bool IsTemporaryOriginalView { get; }

        public bool AreOverlaysVisible { get; }

        public double OverlayOpacity { get; }

        public int LabelCount { get; }

        public int InferenceCandidateCount { get; }

        public bool HasUnsavedLabelChanges { get; }

        public bool ShowLabels { get; }

        public bool ShowInference { get; }

        public string Title { get; }

        public string Detail { get; }

        public string LabelText { get; }

        public string InferenceText { get; }

        public string ToolTip { get; }
    }
}
