using MahApps.Metro.IconPacks;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateReviewPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<double> NoOpValueCommand = _ => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private static readonly Action<WpfModelComparisonReviewExample> NoOpModelComparisonExampleCommand = _ => { };
        private string confidenceText = "0%";
        private string detailText = "\uAC80\uCD9C \uD6C4\uBCF4 \uC5C6\uC74C";
        private string selectedCandidateSummaryText = "\uC120\uD0DD: \uAC80\uCD9C \uD6C4\uBCF4 \uC5C6\uC74C";
        private WpfCandidateReviewListItem selectedCandidate;
        private bool isConfirmSelectedEnabled;
        private bool isConfirmAllEnabled;
        private bool isSkipSelectedEnabled;
        private bool isPreviousCandidateEnabled;
        private bool isNextCandidateEnabled;
        private bool isFocusCandidateEnabled;
        private bool isFocusCurrentLabelEnabled;
        private bool isCompleteImageAndNextEnabled;
        private string confirmSelectedToolTip = "\uD655\uC815\uD560 \uAC80\uCD9C \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string confirmAllToolTip = "\uD655\uC815 \uAC00\uB2A5\uD55C \uD45C\uC2DC \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string skipSelectedToolTip = "\uC2A4\uD0B5\uD560 \uAC80\uCD9C \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string focusCurrentLabelToolTip = "\uACB9\uCE58\uB294 \uAE30\uC874 \uB77C\uBCA8\uC744 \uB77C\uBCA8 \uBAA9\uB85D\uACFC \uD654\uBA74\uC5D0\uC11C \uC120\uD0DD\uD569\uB2C8\uB2E4.";
        private string completeImageAndNextToolTip = "\uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uC644\uB8CC\uD558\uACE0 \uB2E4\uC74C \uBBF8\uAC80\uD1A0 \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.";
        private Visibility comparisonVisibility = Visibility.Collapsed;
        private string comparisonCandidateText = "-";
        private string comparisonCurrentText = "-";
        private string comparisonOverlapText = "\uACB9\uCE68\n0%";
        private string comparisonDecisionText = string.Empty;
        private string postActionPolicyText = string.Empty;
        private Visibility reviewHistoryVisibility = Visibility.Collapsed;
        private Visibility modelComparisonVisibility = Visibility.Collapsed;
        private string modelComparisonStatusText = string.Empty;
        private string modelComparisonDetailText = string.Empty;
        private string modelComparisonActionText = string.Empty;
        private bool isComparisonHighOverlap;
        private ICommand confidenceChangedCommand = new RelayCommand<double>(NoOpValueCommand);
        private ICommand confirmSelectedCommand = new RelayCommand(NoOpCommand);
        private ICommand confirmAllCommand = new RelayCommand(NoOpCommand);
        private ICommand skipSelectedCommand = new RelayCommand(NoOpCommand);
        private ICommand previousCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand nextCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand focusCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand focusCurrentLabelCommand = new RelayCommand(NoOpCommand);
        private ICommand completeImageAndNextCommand = new RelayCommand(NoOpCommand);
        private ICommand modelComparisonExampleCommand = new RelayCommand<WpfModelComparisonReviewExample>(NoOpModelComparisonExampleCommand);
        private ICommand candidateSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand candidatePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);

        public string ViewName => nameof(WpfCandidateReviewPanel);

        public WpfBulkObservableCollection<WpfCandidateReviewListItem> Candidates { get; } = new WpfBulkObservableCollection<WpfCandidateReviewListItem>();

        public ObservableCollection<string> ReviewHistory { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfModelComparisonReviewExample> ModelComparisonExamples { get; } = new ObservableCollection<WpfModelComparisonReviewExample>();

        public WpfCandidateReviewPanelViewModel()
        {
            PostActionPolicyText = "\uD655\uC815/\uC2A4\uD0B5 \uD6C4\uC5D0\uB294 \uB2E4\uC74C \uD6C4\uBCF4\uB85C \uC774\uB3D9";
        }

        public ICommand ConfidenceChangedCommand
        {
            get => confidenceChangedCommand;
            private set => SetProperty(ref confidenceChangedCommand, value);
        }

        public ICommand ConfirmSelectedCommand
        {
            get => confirmSelectedCommand;
            private set => SetProperty(ref confirmSelectedCommand, value);
        }

        public ICommand ConfirmAllCommand
        {
            get => confirmAllCommand;
            private set => SetProperty(ref confirmAllCommand, value);
        }

        public ICommand SkipSelectedCommand
        {
            get => skipSelectedCommand;
            private set => SetProperty(ref skipSelectedCommand, value);
        }

        public ICommand PreviousCandidateCommand
        {
            get => previousCandidateCommand;
            private set => SetProperty(ref previousCandidateCommand, value);
        }

        public ICommand NextCandidateCommand
        {
            get => nextCandidateCommand;
            private set => SetProperty(ref nextCandidateCommand, value);
        }

        public ICommand FocusCandidateCommand
        {
            get => focusCandidateCommand;
            private set => SetProperty(ref focusCandidateCommand, value);
        }

        public ICommand FocusCurrentLabelCommand
        {
            get => focusCurrentLabelCommand;
            private set => SetProperty(ref focusCurrentLabelCommand, value);
        }

        public ICommand CompleteImageAndNextCommand
        {
            get => completeImageAndNextCommand;
            private set => SetProperty(ref completeImageAndNextCommand, value);
        }

        public ICommand ModelComparisonExampleCommand
        {
            get => modelComparisonExampleCommand;
            private set => SetProperty(ref modelComparisonExampleCommand, value);
        }

        public ICommand CandidateSelectionChangedCommand
        {
            get => candidateSelectionChangedCommand;
            private set => SetProperty(ref candidateSelectionChangedCommand, value);
        }

        public ICommand CandidatePreviewKeyDownCommand
        {
            get => candidatePreviewKeyDownCommand;
            private set => SetProperty(ref candidatePreviewKeyDownCommand, value);
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

        public string SelectedCandidateSummaryText
        {
            get => selectedCandidateSummaryText;
            private set => SetProperty(ref selectedCandidateSummaryText, value ?? string.Empty);
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

        public bool IsFocusCurrentLabelEnabled
        {
            get => isFocusCurrentLabelEnabled;
            private set => SetProperty(ref isFocusCurrentLabelEnabled, value);
        }

        public bool IsCompleteImageAndNextEnabled
        {
            get => isCompleteImageAndNextEnabled;
            private set => SetProperty(ref isCompleteImageAndNextEnabled, value);
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

        public string FocusCurrentLabelToolTip
        {
            get => focusCurrentLabelToolTip;
            private set => SetProperty(ref focusCurrentLabelToolTip, value ?? string.Empty);
        }

        public string CompleteImageAndNextToolTip
        {
            get => completeImageAndNextToolTip;
            private set => SetProperty(ref completeImageAndNextToolTip, value ?? string.Empty);
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

        public string ComparisonDecisionText
        {
            get => comparisonDecisionText;
            private set => SetProperty(ref comparisonDecisionText, value ?? string.Empty);
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

        public Visibility ModelComparisonVisibility
        {
            get => modelComparisonVisibility;
            private set => SetProperty(ref modelComparisonVisibility, value);
        }

        public string ModelComparisonStatusText
        {
            get => modelComparisonStatusText;
            private set => SetProperty(ref modelComparisonStatusText, value ?? string.Empty);
        }

        public string ModelComparisonDetailText
        {
            get => modelComparisonDetailText;
            private set => SetProperty(ref modelComparisonDetailText, value ?? string.Empty);
        }

        public string ModelComparisonActionText
        {
            get => modelComparisonActionText;
            private set => SetProperty(ref modelComparisonActionText, value ?? string.Empty);
        }

        public void ConfigureCommands(
            Action<double> confidenceChanged,
            Action confirmSelected,
            Action confirmAll,
            Action skipSelected,
            Action previousCandidate,
            Action nextCandidate,
            Action focusCandidate,
            Action focusCurrentLabel,
            Action<object> candidateSelectionChanged,
            Action<KeyInputCommandArgs> candidatePreviewKeyDown,
            Action completeImageAndNext = null,
            Action<WpfModelComparisonReviewExample> openModelComparisonExample = null)
        {
            // Candidate review stays virtualized; commands keep the view declarative while shell owns workflow state.
            ConfidenceChangedCommand = new RelayCommand<double>(confidenceChanged ?? NoOpValueCommand);
            ConfirmSelectedCommand = new RelayCommand(confirmSelected ?? NoOpCommand);
            ConfirmAllCommand = new RelayCommand(confirmAll ?? NoOpCommand);
            SkipSelectedCommand = new RelayCommand(skipSelected ?? NoOpCommand);
            PreviousCandidateCommand = new RelayCommand(previousCandidate ?? NoOpCommand);
            NextCandidateCommand = new RelayCommand(nextCandidate ?? NoOpCommand);
            FocusCandidateCommand = new RelayCommand(focusCandidate ?? NoOpCommand);
            FocusCurrentLabelCommand = new RelayCommand(focusCurrentLabel ?? NoOpCommand);
            CandidateSelectionChangedCommand = new RelayCommand<object>(candidateSelectionChanged ?? NoOpSelectionCommand);
            CandidatePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(candidatePreviewKeyDown ?? NoOpKeyCommand);
            CompleteImageAndNextCommand = new RelayCommand(completeImageAndNext ?? NoOpCommand);
            ModelComparisonExampleCommand = new RelayCommand<WpfModelComparisonReviewExample>(openModelComparisonExample ?? NoOpModelComparisonExampleCommand);
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

        public void SetCurrentLabelFocusState(bool enabled, string toolTip)
        {
            IsFocusCurrentLabelEnabled = enabled;
            FocusCurrentLabelToolTip = toolTip;
        }

        public void SetCompletionState(bool enabled, string toolTip)
        {
            IsCompleteImageAndNextEnabled = enabled;
            CompleteImageAndNextToolTip = toolTip;
        }

        public void ClearComparison()
        {
            ComparisonVisibility = Visibility.Collapsed;
            ComparisonCandidateText = "-";
            ComparisonCurrentText = "-";
            ComparisonOverlapText = "\uACB9\uCE68\n0%";
            ComparisonDecisionText = string.Empty;
            SelectedCandidateSummaryText = "\uC120\uD0DD: \uAC80\uCD9C \uD6C4\uBCF4 \uC5C6\uC74C";
            IsComparisonHighOverlap = false;
        }

        public void SetComparison(WpfCandidateComparisonPresentation presentation)
        {
            // Keep comparison guidance with the presentation payload so future view changes do not rebuild workflow text in code-behind.
            ComparisonVisibility = Visibility.Visible;
            ComparisonCandidateText = presentation.CandidateText;
            ComparisonCurrentText = presentation.CurrentText;
            ComparisonOverlapText = presentation.OverlapText;
            ComparisonDecisionText = presentation.DecisionText;
            SelectedCandidateSummaryText = string.IsNullOrWhiteSpace(presentation.SelectionSummaryText)
                ? SelectedCandidate?.Title ?? "\uC120\uD0DD: \uAC80\uCD9C \uD6C4\uBCF4 \uC5C6\uC74C"
                : presentation.SelectionSummaryText;
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

        public void ClearModelComparisonReview()
        {
            ModelComparisonStatusText = string.Empty;
            ModelComparisonDetailText = string.Empty;
            ModelComparisonActionText = string.Empty;
            ModelComparisonExamples.Clear();
            ModelComparisonVisibility = Visibility.Collapsed;
        }

        public void SetModelComparisonReview(WpfModelComparisonReviewReport report)
        {
            ModelComparisonExamples.Clear();
            if (report?.HasComparison != true)
            {
                ClearModelComparisonReview();
                return;
            }

            ModelComparisonStatusText = report.SummaryText;
            ModelComparisonDetailText = string.IsNullOrWhiteSpace(report.SourcePath)
                ? report.DetailText
                : $"{report.DetailText} / {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(report.SourcePath) ?? report.SourcePath)}";
            ModelComparisonActionText = (report.Examples?.Count ?? 0) > 0
                ? "\uB2E4\uC74C: \uC544\uB798 \uC608\uC2DC\uB97C \uD074\uB9AD\uD574 \uC774\uBBF8\uC9C0 \uC704\uCE58\uB97C \uD655\uC778\uD55C \uB4A4 Guide\uC758 \uAD50\uCCB4 \uD310\uB2E8\uC744 \uBCF4\uC138\uC694."
                : "\uB2E4\uC74C: \uBAA8\uB378 \uCC28\uC774\uAC00 \uC5C6\uC73C\uBA74 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB97C \uB354 \uD655\uC778\uD55C \uB4A4 \uAD50\uCCB4\uB97C \uD310\uB2E8\uD558\uC138\uC694.";

            foreach (WpfModelComparisonReviewExample example in report.Examples ?? Array.Empty<WpfModelComparisonReviewExample>())
            {
                if (example != null)
                {
                    ModelComparisonExamples.Add(example);
                }
            }

            ModelComparisonVisibility = Visibility.Visible;
        }

        public void SetModelComparisonFocus(WpfModelComparisonReviewExample example, string locationText)
        {
            if (example == null)
            {
                return;
            }

            string location = string.IsNullOrWhiteSpace(locationText)
                ? example.LocationText
                : locationText.Trim();
            string action = example.ActionText ?? string.Empty;
            ModelComparisonStatusText = $"\uAC80\uD1A0 \uC608\uC2DC \uC5F4\uB9BC: {example.Title}";
            ModelComparisonDetailText = string.IsNullOrWhiteSpace(location)
                ? action
                : string.IsNullOrWhiteSpace(action)
                    ? location
                    : $"{location} / {action}";
            ModelComparisonActionText = "\uD655\uC778 \uD6C4: \uC774 \uC608\uC2DC\uAC00 \uC0C8 \uBAA8\uB378\uC5D0 \uC720\uB9AC\uD55C\uC9C0 \uD310\uB2E8\uD558\uACE0, Guide\uC758 \uAD50\uCCB4 \uD310\uB2E8\uC73C\uB85C \uB3CC\uC544\uAC00\uC138\uC694.";
            ModelComparisonVisibility = Visibility.Visible;
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
