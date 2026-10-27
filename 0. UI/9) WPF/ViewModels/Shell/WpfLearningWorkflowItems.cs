using MahApps.Metro.IconPacks;
using OpenVisionLab.Mvvm;
using System;

namespace MvcVisionSystem
{
    public enum WpfLearningMode
    {
        LabelingBasics,
        ObjectDetection,
        Segmentation,
        AnomalyDetection,
        Train,
        Infer,
        Review
    }

    public enum WpfAnnotationTool
    {
        Select,
        Rectangle,
        Ellipse,
        Polygon,
        Brush,
        Eraser,
        PanZoom,
        Undo,
        Redo,
        Delete,
        SmartMask
    }

    public enum WpfLearningStep
    {
        Sample,
        Label,
        Infer,
        Review,
        Save
    }

    public sealed class WpfFirstRunChecklistItem
    {
        public WpfFirstRunChecklistItem(
            int order,
            string title,
            string actionText,
            string toolTip,
            PackIconMaterialKind iconKind,
            int shortcutWorkflowStepOrder = 0,
            string shortcutActionText = "")
        {
            Order = order;
            Title = title ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            ToolTip = string.IsNullOrWhiteSpace(toolTip) ? ActionText : toolTip;
            IconKind = iconKind;
            ShortcutWorkflowStepOrder = shortcutWorkflowStepOrder;
            ShortcutActionText = shortcutActionText ?? string.Empty;
        }

        public int Order { get; }

        public string StepText => Order.ToString();

        public string Title { get; }

        public string ActionText { get; }

        public string ToolTip { get; }

        public PackIconMaterialKind IconKind { get; }

        public int ShortcutWorkflowStepOrder { get; }

        public string ShortcutActionText { get; }

        public string ShortcutAutomationName => string.IsNullOrWhiteSpace(ShortcutActionText)
            ? Title
            : $"{Title} {ShortcutActionText}";
    }

    public sealed class WpfLearningModeItem
    {
        public WpfLearningModeItem(WpfLearningMode mode, string text, PackIconMaterialKind iconKind, string toolTip)
        {
            Mode = mode;
            Text = text ?? string.Empty;
            IconKind = iconKind;
            ToolTip = toolTip ?? string.Empty;
        }

        public WpfLearningMode Mode { get; }

        public string Text { get; }

        public PackIconMaterialKind IconKind { get; }

        public string ToolTip { get; }

        public bool IsActionEnabled => true;
    }

    public sealed class WpfAnnotationToolItem : WpfObservableViewModel
    {
        private readonly string baseToolTip;
        private readonly string baseToolTipDescription;
        private bool isActionEnabled = true;
        private string displayCapabilityText = string.Empty;
        private string toolTip = string.Empty;

        public WpfAnnotationToolItem(WpfAnnotationTool tool, string text, PackIconMaterialKind iconKind, string toolTip)
        {
            AnnotationToolCapability capability = AnnotationToolCapabilityService.Get(tool);
            baseToolTipDescription = toolTip ?? string.Empty;
            Tool = tool;
            Text = text ?? string.Empty;
            IconKind = iconKind;
            baseToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? capability.StatusText
                : $"{toolTip} / {capability.StatusText}";
            string shortcutText = AnnotationProductivityService.GetToolShortcutText(tool);
            ShortcutText = shortcutText;
            if (!string.IsNullOrWhiteSpace(shortcutText))
            {
                baseToolTip = $"{baseToolTip} / 단축키 {shortcutText}";
            }
            ToolTip = baseToolTip;
            IsConnected = capability.IsConnected;
            CapabilityText = capability.StateText;
            DisplayCapabilityText = CapabilityText;
            CapabilityStatusText = capability.StatusText;
        }

        public WpfAnnotationTool Tool { get; }

        public string Text { get; }

        public string AutomationId => $"CanvasAnnotationTool{Tool}";

        public string DisplayText => OpenVisionLab.OpenVisionLanguageService.T($"WpfAnnotationTool.{Tool}");

        public string AccessibleName => DisplayText;

        public string ShortcutText { get; }

        public PackIconMaterialKind IconKind { get; }

        public string ToolTip
        {
            get
            {
                string capabilityText = LocalizationTextRuntimeService.Translate(CapabilityStatusText);
                string value = Tool == WpfAnnotationTool.Undo || Tool == WpfAnnotationTool.Redo
                    ? DisplayText
                    : string.IsNullOrWhiteSpace(baseToolTipDescription)
                        ? capabilityText
                        : $"{LocalizationTextRuntimeService.Translate(baseToolTipDescription)} / {capabilityText}";
                if (!string.IsNullOrWhiteSpace(ShortcutText))
                {
                    value = string.Format(System.Globalization.CultureInfo.InvariantCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.WithShortcut"), value, ShortcutText);
                }

                string runtimeSuffix = toolTip.Substring(baseToolTip.Length);
                if (runtimeSuffix.Length == 0)
                {
                    return value;
                }

                string runtimeStatus = runtimeSuffix.Substring(3);
                if (Tool == WpfAnnotationTool.Undo || Tool == WpfAnnotationTool.Redo)
                {
                    string prefixKey = Tool == WpfAnnotationTool.Undo
                        ? "WpfLearningWorkflow.AnnotationHistory.UndoAvailable"
                        : "WpfLearningWorkflow.AnnotationHistory.RedoAvailable";
                    if (LocalizationTextRuntimeService.TryGetCachedTextPair(prefixKey, out string koreanPrefix, out string englishPrefix))
                    {
                        foreach (string prefix in new[] { OpenVisionLab.OpenVisionLanguageService.GetPreviousKoreanDefaults(prefixKey)[0], koreanPrefix, englishPrefix })
                        {
                            if (string.IsNullOrEmpty(prefix)) continue;
                            if (string.Equals(runtimeStatus, prefix, StringComparison.Ordinal) || runtimeStatus.StartsWith(prefix + ": ", StringComparison.Ordinal))
                            {
                                string actionSuffix = runtimeStatus.Substring(prefix.Length);
                                if (actionSuffix.StartsWith(": ", StringComparison.Ordinal))
                                {
                                    actionSuffix = ": " + LocalizationTextRuntimeService.TranslateAnnotationEditReason(actionSuffix.Substring(2));
                                }
                                return $"{value} / {OpenVisionLab.OpenVisionLanguageService.T(prefixKey)}{actionSuffix}";
                            }
                        }
                    }
                }

                return $"{value} / {LocalizationTextRuntimeService.Translate(runtimeStatus)}";
            }
            private set => SetProperty(ref toolTip, value ?? string.Empty);
        }

        public bool IsConnected { get; }

        public string CapabilityText { get; }

        public string DisplayCapabilityText
        {
            get => displayCapabilityText;
            private set => SetProperty(ref displayCapabilityText, value ?? string.Empty);
        }

        public string CapabilityStatusText { get; }

        public bool IsActionEnabled
        {
            get => isActionEnabled;
            private set => SetProperty(ref isActionEnabled, value);
        }

        internal void RefreshLocalizedPresentation()
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(AccessibleName));
            OnPropertyChanged(nameof(ToolTip));
        }

        public void SetRuntimeAvailability(bool isEnabled, string stateText, string statusText)
        {
            IsActionEnabled = isEnabled;
            DisplayCapabilityText = string.IsNullOrWhiteSpace(stateText) ? CapabilityText : stateText;
            ToolTip = string.IsNullOrWhiteSpace(statusText) ? baseToolTip : $"{baseToolTip} / {statusText}";
        }
    }

    public sealed class WpfLearningStepItem
    {
        public WpfLearningStepItem(WpfLearningStep step, string text, PackIconMaterialKind iconKind)
        {
            Step = step;
            Text = text ?? string.Empty;
            IconKind = iconKind;
        }

        public WpfLearningStep Step { get; }

        public string Text { get; }

        public PackIconMaterialKind IconKind { get; }

        public bool IsActionEnabled => true;
    }

    public sealed class WpfTemplateWorkflowStepItem
    {
        public WpfTemplateWorkflowStepItem(
            int order,
            string title,
            string detail,
            string locationText,
            PackIconMaterialKind iconKind)
        {
            Order = order;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            LocationText = locationText ?? string.Empty;
            IconKind = iconKind;
        }

        public int Order { get; }

        public string StepText => Order.ToString();

        public string Title { get; }

        public string Detail { get; }

        public string LocationText { get; }

        public PackIconMaterialKind IconKind { get; }
    }

    public enum WpfDatasetDashboardActionKind
    {
        None,
        OpenImages,
        OpenClassCatalog,
        OpenLabelingProgress,
        OpenLabelingTool,
        CheckDataset,
        ExportQualityAudit,
        ExportHistoricalSegmentationRemediationAudit,
        OpenDatasetSettings
    }

    public sealed class WpfDatasetDashboardMetricItem
    {
        private readonly DatasetDashboardMetricLocalizationDescriptor localization;

        public WpfDatasetDashboardMetricItem(
            string title,
            string value,
            string detail,
            string stateText,
            PackIconMaterialKind iconKind,
            bool isProblem,
            bool isWarning,
            WpfDatasetDashboardActionKind actionKind = WpfDatasetDashboardActionKind.None)
            : this(
                title,
                value,
                detail,
                stateText,
                iconKind,
                isProblem,
                isWarning,
                actionKind,
                localization: null)
        {
        }

        private WpfDatasetDashboardMetricItem(
            string title,
            string value,
            string detail,
            string stateText,
            PackIconMaterialKind iconKind,
            bool isProblem,
            bool isWarning,
            WpfDatasetDashboardActionKind actionKind,
            DatasetDashboardMetricLocalizationDescriptor localization)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            StateText = stateText ?? string.Empty;
            IconKind = iconKind;
            IsProblem = isProblem;
            IsWarning = isWarning;
            ActionKind = actionKind;
            this.localization = localization;
        }

        internal static WpfDatasetDashboardMetricItem CreateLocalized(
            DatasetDashboardMetricLocalizationDescriptor localization,
            string fallbackValue,
            PackIconMaterialKind iconKind,
            bool isProblem,
            bool isWarning,
            WpfDatasetDashboardActionKind actionKind)
        {
            if (localization == null)
            {
                throw new ArgumentNullException(nameof(localization));
            }

            return new WpfDatasetDashboardMetricItem(
                localization.RenderTitle(),
                localization.RenderValue(fallbackValue),
                localization.RenderDetail(),
                localization.RenderState(),
                iconKind,
                isProblem,
                isWarning,
                actionKind,
                localization);
        }

        internal WpfDatasetDashboardMetricItem Localize()
        {
            return localization == null
                ? this
                : new WpfDatasetDashboardMetricItem(
                    localization.RenderTitle(),
                    localization.RenderValue(Value),
                    localization.RenderDetail(),
                    localization.RenderState(),
                    IconKind,
                    IsProblem,
                    IsWarning,
                    ActionKind,
                    localization);
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public string StateText { get; }

        public PackIconMaterialKind IconKind { get; }

        public bool IsProblem { get; }

        public bool IsWarning { get; }

        public WpfDatasetDashboardActionKind ActionKind { get; }
    }

    public sealed class WpfTrainingResultReportItem
    {
        public WpfTrainingResultReportItem(
            string title,
            string value,
            string detail,
            PackIconMaterialKind iconKind,
            bool isWarning = false)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            IconKind = iconKind;
            IsWarning = isWarning;
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public PackIconMaterialKind IconKind { get; }

        public bool IsWarning { get; }
    }

    public sealed class WpfYoloDatasetStructureItem
    {
        public WpfYoloDatasetStructureItem(string title, string value, string detail, PackIconMaterialKind iconKind)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            IconKind = iconKind;
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public PackIconMaterialKind IconKind { get; }
    }

    public sealed class WpfYoloTrainingWorkflowStepItem : WpfObservableViewModel
    {
        private string title;
        private string actionText;
        private string stateText = "대기";
        private bool isCompleted;
        private PackIconMaterialKind stateIconKind = PackIconMaterialKind.ClockOutline;

        public WpfYoloTrainingWorkflowStepItem(
            int order,
            string title,
            string actionText,
            string resultText,
            PackIconMaterialKind iconKind)
        {
            Order = order;
            this.title = title ?? string.Empty;
            this.actionText = actionText ?? string.Empty;
            ResultText = resultText ?? string.Empty;
            IconKind = iconKind;
        }

        public int Order { get; }

        public string Title => title;

        public string ActionText => actionText;

        public string ResultText { get; }

        public PackIconMaterialKind IconKind { get; }

        public string StateText
        {
            get => stateText;
            set => SetProperty(ref stateText, value ?? string.Empty);
        }

        public bool IsCompleted
        {
            get => isCompleted;
            set => SetProperty(ref isCompleted, value);
        }

        public PackIconMaterialKind StateIconKind
        {
            get => stateIconKind;
            set => SetProperty(ref stateIconKind, value);
        }

        internal void RefreshLocalizedPresentation(string localizedTitle, string localizedActionText)
        {
            SetProperty(ref title, localizedTitle ?? string.Empty, nameof(Title));
            SetProperty(ref actionText, localizedActionText ?? string.Empty, nameof(ActionText));
        }
    }
}
