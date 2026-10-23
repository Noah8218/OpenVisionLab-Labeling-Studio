using System;
using System.Globalization;

namespace MvcVisionSystem
{
    public class CandidateReviewCompletionPresentationService
    {
        public WpfCandidateReviewCompletionPresentation Build(
            bool hasImage,
            bool isDetecting,
            int pendingCandidateCount,
            int savedLabelCount,
            bool hasUnsavedLabels)
        {
            if (!hasImage)
            {
                return new WpfCandidateReviewCompletionPresentation(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.Waiting"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.OpenOrInfer"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoImage.Next"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoImage.Button"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoImage.ToolTip"),
                    canComplete: false);
            }

            if (isDetecting)
            {
                return new WpfCandidateReviewCompletionPresentation(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Running.Title"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Running.Detail"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Running.Next"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Running.Button"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Running.ToolTip"),
                    canComplete: false);
            }

            if (pendingCandidateCount > 0)
            {
                string pendingText = pendingCandidateCount.ToString(CultureInfo.CurrentCulture);
                return new WpfCandidateReviewCompletionPresentation(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Pending.Title"),
                    string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Pending.Detail"), pendingText),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Pending.Next"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Pending.Button"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.Pending.ToolTip"),
                    canComplete: false);
            }

            if (savedLabelCount > 0)
            {
                string labelText = savedLabelCount.ToString(CultureInfo.CurrentCulture);
                bool shouldSave = hasUnsavedLabels;
                return new WpfCandidateReviewCompletionPresentation(
                    shouldSave ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsUnsaved.Title") : OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsSaved.Title"),
                    string.Format(
                        CultureInfo.CurrentCulture,
                        shouldSave
                            ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsUnsaved.Detail")
                            : OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsSaved.Detail"),
                        labelText),
                    shouldSave ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsUnsaved.Next") : OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsSaved.Next"),
                    shouldSave ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsUnsaved.Button") : OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsSaved.Button"),
                    shouldSave ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsUnsaved.ToolTip") : OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.LabelsSaved.ToolTip"),
                    canComplete: true);
            }

            return new WpfCandidateReviewCompletionPresentation(
                OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoObject.Title"),
                OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoObject.Detail"),
                OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoObject.Next"),
                OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoObject.Button"),
                OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Completion.NoObject.ToolTip"),
                canComplete: true);
        }
    }

    [Obsolete("Use CandidateReviewCompletionPresentationService.", false)]
    public sealed class WpfCandidateReviewCompletionPresentationService : CandidateReviewCompletionPresentationService
    {
    }

    public sealed class WpfCandidateReviewCompletionPresentation
    {
        public WpfCandidateReviewCompletionPresentation(
            string titleText,
            string detailText,
            string nextActionText,
            string buttonText,
            string toolTip,
            bool canComplete)
        {
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            ButtonText = buttonText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            CanComplete = canComplete;
        }

        public string TitleText { get; }

        public string DetailText { get; }

        public string NextActionText { get; }

        public string ButtonText { get; }

        public string ToolTip { get; }

        public bool CanComplete { get; }
    }
}
