using MahApps.Metro.IconPacks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateReviewPanelViewModel : WpfObservableViewModel
    {
        private string confidenceText = "0%";
        private string detailText = "AI \uD6C4\uBCF4 \uC5C6\uC74C";
        private WpfCandidateReviewListItem selectedCandidate;
        private bool isConfirmSelectedEnabled;
        private bool isConfirmAllEnabled;
        private bool isSkipSelectedEnabled;
        private bool isPreviousCandidateEnabled;
        private bool isNextCandidateEnabled;
        private bool isFocusCandidateEnabled;
        private string confirmSelectedToolTip = "\uD655\uC815\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string confirmAllToolTip = "\uD655\uC815 \uAC00\uB2A5\uD55C \uD45C\uC2DC \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string skipSelectedToolTip = "\uC2A4\uD0B5\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private Visibility comparisonVisibility = Visibility.Collapsed;
        private string comparisonCandidateText = "-";
        private string comparisonCurrentText = "-";
        private string comparisonOverlapText = "IoU\n0%";
        private string postActionPolicyText = "확정/스킵 후 다음 후보로 이동";
        private Visibility reviewHistoryVisibility = Visibility.Collapsed;
        private bool isComparisonHighOverlap;

        public string ViewName => nameof(WpfCandidateReviewPanel);

        public WpfBulkObservableCollection<WpfCandidateReviewListItem> Candidates { get; } = new WpfBulkObservableCollection<WpfCandidateReviewListItem>();

        public ObservableCollection<string> ReviewHistory { get; } = new ObservableCollection<string>();

        public WpfCandidateReviewPanelViewModel()
        {
            PostActionPolicyText = "\uD655\uC815/\uC2A4\uD0B5 \uD6C4\uC5D0\uB294 \uB2E4\uC74C \uD6C4\uBCF4\uB85C \uC774\uB3D9";
        }

        public string ConfidenceText
        {
            get => confidenceText;
            set => SetProperty(ref confidenceText, value ?? string.Empty);
        }

        public string DetailText
        {
            get => detailText;
            set => SetProperty(ref detailText, value ?? string.Empty);
        }

        public WpfCandidateReviewListItem SelectedCandidate
        {
            get => selectedCandidate;
            set => SetProperty(ref selectedCandidate, value);
        }

        public bool IsConfirmSelectedEnabled
        {
            get => isConfirmSelectedEnabled;
            private set => SetProperty(ref isConfirmSelectedEnabled, value);
        }

        public bool IsConfirmAllEnabled
        {
            get => isConfirmAllEnabled;
            private set => SetProperty(ref isConfirmAllEnabled, value);
        }

        public bool IsSkipSelectedEnabled
        {
            get => isSkipSelectedEnabled;
            private set => SetProperty(ref isSkipSelectedEnabled, value);
        }

        public bool IsPreviousCandidateEnabled
        {
            get => isPreviousCandidateEnabled;
            private set => SetProperty(ref isPreviousCandidateEnabled, value);
        }

        public bool IsNextCandidateEnabled
        {
            get => isNextCandidateEnabled;
            private set => SetProperty(ref isNextCandidateEnabled, value);
        }

        public bool IsFocusCandidateEnabled
        {
            get => isFocusCandidateEnabled;
            private set => SetProperty(ref isFocusCandidateEnabled, value);
        }

        public string ConfirmSelectedToolTip
        {
            get => confirmSelectedToolTip;
            private set => SetProperty(ref confirmSelectedToolTip, value ?? string.Empty);
        }

        public string ConfirmAllToolTip
        {
            get => confirmAllToolTip;
            private set => SetProperty(ref confirmAllToolTip, value ?? string.Empty);
        }

        public string SkipSelectedToolTip
        {
            get => skipSelectedToolTip;
            private set => SetProperty(ref skipSelectedToolTip, value ?? string.Empty);
        }

        public Visibility ComparisonVisibility
        {
            get => comparisonVisibility;
            private set => SetProperty(ref comparisonVisibility, value);
        }

        public string ComparisonCandidateText
        {
            get => comparisonCandidateText;
            private set => SetProperty(ref comparisonCandidateText, value ?? string.Empty);
        }

        public string ComparisonCurrentText
        {
            get => comparisonCurrentText;
            private set => SetProperty(ref comparisonCurrentText, value ?? string.Empty);
        }

        public string ComparisonOverlapText
        {
            get => comparisonOverlapText;
            private set => SetProperty(ref comparisonOverlapText, value ?? string.Empty);
        }

        public bool IsComparisonHighOverlap
        {
            get => isComparisonHighOverlap;
            private set => SetProperty(ref isComparisonHighOverlap, value);
        }

        public string PostActionPolicyText
        {
            get => postActionPolicyText;
            set => SetProperty(ref postActionPolicyText, value ?? string.Empty);
        }

        public Visibility ReviewHistoryVisibility
        {
            get => reviewHistoryVisibility;
            private set => SetProperty(ref reviewHistoryVisibility, value);
        }

        public void SetCandidates(IEnumerable<WpfCandidateReviewListItem> candidates, string detail, object preferredPayload = null)
        {
            SelectedCandidate = null;
            List<WpfCandidateReviewListItem> rows = (candidates ?? Array.Empty<WpfCandidateReviewListItem>()).ToList();
            Candidates.ReplaceAll(rows);

            SelectedCandidate = Candidates.FirstOrDefault(item => item.IsEnabled && ReferenceEquals(item.Payload, preferredPayload))
                ?? Candidates.FirstOrDefault(item => item.IsEnabled);
            DetailText = detail;
            ClearComparison();
        }

        public void SetActionState(
            bool confirmSelectedEnabled,
            bool confirmAllEnabled,
            bool skipSelectedEnabled,
            string confirmSelectedHint,
            string confirmAllHint,
            string skipSelectedHint)
        {
            IsConfirmSelectedEnabled = confirmSelectedEnabled;
            IsConfirmAllEnabled = confirmAllEnabled;
            IsSkipSelectedEnabled = skipSelectedEnabled;
            ConfirmSelectedToolTip = confirmSelectedHint;
            ConfirmAllToolTip = confirmAllHint;
            SkipSelectedToolTip = skipSelectedHint;
        }

        public void SetNavigationState(bool previousEnabled, bool nextEnabled, bool focusEnabled)
        {
            IsPreviousCandidateEnabled = previousEnabled;
            IsNextCandidateEnabled = nextEnabled;
            IsFocusCandidateEnabled = focusEnabled;
        }

        public void ClearComparison()
        {
            ComparisonVisibility = Visibility.Collapsed;
            ComparisonCandidateText = "-";
            ComparisonCurrentText = "-";
            ComparisonOverlapText = "IoU\n0%";
            IsComparisonHighOverlap = false;
        }

        public void SetComparison(WpfCandidateComparisonPresentation presentation)
        {
            ComparisonVisibility = Visibility.Visible;
            ComparisonCandidateText = presentation.CandidateText;
            ComparisonCurrentText = presentation.CurrentText;
            ComparisonOverlapText = presentation.OverlapText;
            IsComparisonHighOverlap = presentation.IsHighOverlap;
        }

        public void ApplySelectionReview(string detail, WpfCandidateComparisonPresentation comparison, bool showComparison)
        {
            DetailText = detail;
            if (showComparison)
            {
                SetComparison(comparison);
            }
            else
            {
                ClearComparison();
            }
        }

        public void ClearReviewHistory()
        {
            ReviewHistory.Clear();
            ReviewHistoryVisibility = Visibility.Collapsed;
        }

        public void AddReviewHistory(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ReviewHistory.Insert(0, message.Trim());
            while (ReviewHistory.Count > 4)
            {
                ReviewHistory.RemoveAt(ReviewHistory.Count - 1);
            }

            ReviewHistoryVisibility = ReviewHistory.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public sealed class WpfCandidateReviewListItem
    {
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
