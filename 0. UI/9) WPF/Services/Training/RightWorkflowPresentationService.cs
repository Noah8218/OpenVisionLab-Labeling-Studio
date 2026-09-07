using OpenVisionLab;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the text projection for the shell's right workflow panel.
    /// Workflow selection state and WPF layout remain with the ViewModel; this
    /// type only maps the stable stage/shortcut pair to localized text.
    /// </summary>
    public sealed class RightWorkflowPresentation
    {
        public RightWorkflowPresentation(
            string titleText,
            string detailText,
            string railCurrentViewText)
        {
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            RailCurrentViewText = railCurrentViewText ?? string.Empty;
        }

        public string TitleText { get; }

        public string DetailText { get; }

        public string RailCurrentViewText { get; }
    }

    public static class RightWorkflowPresentationService
    {
        public static RightWorkflowPresentation Build(
            WpfShellWorkflowStage stage,
            WpfRightWorkflowShortcut shortcut)
        {
            return new RightWorkflowPresentation(
                BuildTitle(stage, shortcut),
                BuildDetail(stage, shortcut),
                BuildRailCurrentViewText(stage, shortcut));
        }

        private static string BuildTitle(
            WpfShellWorkflowStage stage,
            WpfRightWorkflowShortcut shortcut)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                return shortcut == WpfRightWorkflowShortcut.ClassCatalog
                    ? OpenVisionLanguageService.T("WpfShell.Right.Title.Classes")
                    : OpenVisionLanguageService.T("WpfShell.Right.Title.Dataset");
            }

            if (stage == WpfShellWorkflowStage.Labeling)
            {
                return shortcut switch
                {
                    WpfRightWorkflowShortcut.LabelingGuide => OpenVisionLanguageService.T("WpfShell.Right.Title.CurrentTask"),
                    WpfRightWorkflowShortcut.ClassCatalog => OpenVisionLanguageService.T("WpfShell.Right.Title.Classes"),
                    _ => OpenVisionLanguageService.T("WpfShell.Right.Title.SavedLabels")
                };
            }

            return stage switch
            {
                WpfShellWorkflowStage.Inference => OpenVisionLanguageService.T("WpfShell.Right.Title.AiCandidates"),
                WpfShellWorkflowStage.TrainingModel => OpenVisionLanguageService.T("WpfShell.Right.Title.TrainingModel"),
                _ => OpenVisionLanguageService.T("WpfShell.Right.Title.Dataset")
            };
        }

        private static string BuildDetail(
            WpfShellWorkflowStage stage,
            WpfRightWorkflowShortcut shortcut)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                return shortcut == WpfRightWorkflowShortcut.ClassCatalog
                    ? OpenVisionLanguageService.T("WpfShell.Right.Detail.DatasetClasses")
                    : OpenVisionLanguageService.T("WpfShell.Right.Detail.Dataset");
            }

            if (stage == WpfShellWorkflowStage.Labeling)
            {
                return shortcut switch
                {
                    WpfRightWorkflowShortcut.LabelingGuide => OpenVisionLanguageService.T("WpfShell.Right.Detail.CurrentTask"),
                    WpfRightWorkflowShortcut.ClassCatalog => OpenVisionLanguageService.T("WpfShell.Right.Detail.Classes"),
                    _ => OpenVisionLanguageService.T("WpfShell.Right.Detail.SavedLabels")
                };
            }

            return stage switch
            {
                WpfShellWorkflowStage.Inference => OpenVisionLanguageService.T("WpfShell.Right.Detail.AiCandidates"),
                WpfShellWorkflowStage.TrainingModel => OpenVisionLanguageService.T("WpfShell.Right.Detail.TrainingModel"),
                _ => OpenVisionLanguageService.T("WpfShell.Right.Detail.Dataset")
            };
        }

        private static string BuildRailCurrentViewText(
            WpfShellWorkflowStage stage,
            WpfRightWorkflowShortcut shortcut)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                return shortcut == WpfRightWorkflowShortcut.ClassCatalog
                    ? OpenVisionLanguageService.T("WpfShell.Right.Rail.Classes")
                    : OpenVisionLanguageService.T("WpfShell.Right.Rail.Home");
            }

            if (stage == WpfShellWorkflowStage.Labeling)
            {
                return shortcut switch
                {
                    WpfRightWorkflowShortcut.LabelingGuide => OpenVisionLanguageService.T("WpfShell.Right.Rail.Task"),
                    WpfRightWorkflowShortcut.ClassCatalog => OpenVisionLanguageService.T("WpfShell.Right.Rail.Classes"),
                    _ => OpenVisionLanguageService.T("WpfShell.Right.Rail.Labels")
                };
            }

            return stage switch
            {
                WpfShellWorkflowStage.Inference => OpenVisionLanguageService.T("WpfShell.Right.Rail.AiCandidates"),
                WpfShellWorkflowStage.TrainingModel => OpenVisionLanguageService.T("WpfShell.Right.Rail.Model"),
                _ => OpenVisionLanguageService.T("WpfShell.Right.Rail.Home")
            };
        }
    }

    [System.Obsolete("Use RightWorkflowPresentation.", false)]
    public sealed class WpfRightWorkflowPresentation
    {
        private readonly RightWorkflowPresentation inner;

        public WpfRightWorkflowPresentation(
            string titleText,
            string detailText,
            string railCurrentViewText)
        {
            inner = new RightWorkflowPresentation(titleText, detailText, railCurrentViewText);
        }

        private WpfRightWorkflowPresentation(RightWorkflowPresentation source)
        {
            inner = source ?? throw new System.ArgumentNullException(nameof(source));
        }

        internal static WpfRightWorkflowPresentation FromCanonical(RightWorkflowPresentation source)
            => source == null ? null : new WpfRightWorkflowPresentation(source);

        public string TitleText => inner.TitleText;

        public string DetailText => inner.DetailText;

        public string RailCurrentViewText => inner.RailCurrentViewText;
    }

    [System.Obsolete("Use RightWorkflowPresentationService.", false)]
    public static class WpfRightWorkflowPresentationService
    {
        public static WpfRightWorkflowPresentation Build(WpfShellWorkflowStage stage, WpfRightWorkflowShortcut shortcut)
            => WpfRightWorkflowPresentation.FromCanonical(RightWorkflowPresentationService.Build(stage, shortcut));
    }
}
