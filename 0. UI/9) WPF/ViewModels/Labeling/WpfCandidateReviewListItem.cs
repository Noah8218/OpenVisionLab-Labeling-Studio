using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using System;
using System.Globalization;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateReviewListItem : WpfObservableViewModel
    {
        private MediaBrush classBrush = MediaBrushes.Transparent;
        private bool isInlineConfirmEnabled;
        private bool isInlineSkipEnabled;
        private bool isSmartMaskCandidate;
        private bool isInferenceDecisionEvidence;

        public WpfCandidateReviewListItem(
            string title,
            string secondaryText,
            string toolTip,
            object payload,
            PackIconMaterialKind iconKind,
            MediaBrush stateBrush,
            bool isEnabled = true)
        {
            Title = title ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            Payload = payload;
            IconKind = iconKind;
            StateBrush = stateBrush ?? MediaBrushes.Transparent;
            IsEnabled = isEnabled;
        }

        public string Title { get; }

        public string SecondaryText { get; }

        public string ToolTip { get; }

        public object Payload { get; }

        public PackIconMaterialKind IconKind { get; }

        public MediaBrush StateBrush { get; }

        public bool IsEnabled { get; }

        public string ClassName => Payload is YoloWorkerSmokeCandidate candidate ? CandidateReviewPresenter.GetClassName(candidate) : Title;
        public string InlineConfidenceText => Payload is YoloWorkerSmokeCandidate candidate && double.IsFinite(candidate.Confidence)
            ? "AI " + candidate.Confidence.ToString("0.00", CultureInfo.InvariantCulture) : "AI —";
        public MediaBrush ClassBrush { get => classBrush; private set => SetProperty(ref classBrush, value); }
        public bool IsInlineConfirmEnabled { get => isInlineConfirmEnabled; private set => SetProperty(ref isInlineConfirmEnabled, value); }
        public bool IsInlineSkipEnabled { get => isInlineSkipEnabled; private set => SetProperty(ref isInlineSkipEnabled, value); }
        public bool IsSmartMaskCandidate { get => isSmartMaskCandidate; private set => SetProperty(ref isSmartMaskCandidate, value); }
        public bool IsInferenceDecisionEvidence { get => isInferenceDecisionEvidence; internal set => SetProperty(ref isInferenceDecisionEvidence, value); }

        internal void SetInlinePresentation(MediaBrush brush, bool isSmartMask)
        {
            ClassBrush = brush ?? MediaBrushes.Transparent;
            IsSmartMaskCandidate = isSmartMask;
        }

        internal void SetInlineAvailability(bool canConfirm, bool canSkip)
        {
            IsInlineConfirmEnabled = IsEnabled && canConfirm;
            IsInlineSkipEnabled = IsEnabled && canSkip;
        }

        public string Content => Title;

        public static WpfCandidateReviewListItem Empty(string title, string toolTip)
            => new WpfCandidateReviewListItem(
                title,
                string.Empty,
                toolTip,
                null,
                PackIconMaterialKind.InformationOutline,
                MediaBrushes.Gray,
                isEnabled: false);

        public override string ToString() => Title;
    }
}
