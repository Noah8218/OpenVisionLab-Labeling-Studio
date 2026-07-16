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
        private const string DefaultModelComparisonSourceText = "\uBE44\uAD50 \uB300\uC0C1: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 / \uD559\uC2B5 \uD6C4\uBCF4 \uD655\uC778 \uD544\uC694";
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<double> NoOpValueCommand = _ => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private static readonly Action<WpfModelComparisonReviewExample> NoOpModelComparisonExampleCommand = _ => { };
        private string panelModeTitleText = "\uAC80\uD1A0\uD560 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4";
        private string panelModeBadgeText = "\uC120\uD0DD \uB2E8\uACC4";
        private string panelModeScopeText = "AI \uC790\uB3D9 \uB77C\uBCA8\uC744 \uC0AC\uC6A9\uD560 \uB54C\uB9CC \uC704\uC758 \uD604\uC7AC \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC138\uC694. \uC9C1\uC811 \uB77C\uBCA8\uB9C1\uC744 \uB9C8\uCCE4\uB2E4\uBA74 4 \uD559\uC2B5/\uBAA8\uB378\uB85C \uC774\uB3D9\uD558\uC138\uC694. \uC800\uC7A5\uD55C \uB77C\uBCA8\uC740 \uADF8\uB300\uB85C \uC720\uC9C0\uB429\uB2C8\uB2E4.";
        private Visibility candidateInteractionVisibility = Visibility.Collapsed;
        private string confidenceText = "0%";
        private string detailText = "AI \uD6C4\uBCF4 \uC5C6\uC74C";
        private string candidateCountSummaryText = "AI \uD6C4\uBCF4 0\uAC1C";
        private string selectedCandidateSummaryText = "\uC120\uD0DD: AI \uD6C4\uBCF4 \uC5C6\uC74C";
        private WpfCandidateReviewListItem selectedCandidate;
        private bool isConfirmSelectedEnabled;
        private bool isConfirmAllEnabled;
        private bool isSkipSelectedEnabled;
        private bool isPreviousCandidateEnabled;
        private bool isNextCandidateEnabled;
        private bool isFocusCandidateEnabled;
        private bool isFocusCurrentLabelEnabled;
        private bool isCompleteImageAndNextEnabled;
        private string completionTitleText = "\uAC80\uD1A0 \uB300\uAE30";
        private string completionDetailText = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uAC70\uB098 \uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uBA74 \uD6C4\uBCF4 \uAC80\uD1A0 \uC0C1\uD0DC\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private string completionNextActionText = "\uB2E4\uC74C: \uC774\uBBF8\uC9C0 \uC120\uD0DD \uB610\uB294 \uAC80\uC0AC";
        private string completeImageAndNextActionText = "\uC774\uBBF8\uC9C0 \uC644\uB8CC";
        private string confirmSelectedToolTip = "\uD655\uC815\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string confirmAllToolTip = "\uD655\uC815 \uAC00\uB2A5\uD55C \uD45C\uC2DC \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string skipSelectedToolTip = "\uC2A4\uD0B5\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string focusCurrentLabelToolTip = "\uACB9\uCE58\uB294 \uAE30\uC874 \uB77C\uBCA8\uC744 \uB77C\uBCA8 \uBAA9\uB85D\uACFC \uD654\uBA74\uC5D0\uC11C \uC120\uD0DD\uD569\uB2C8\uB2E4.";
        private string completeImageAndNextToolTip = "\uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uC644\uB8CC\uD558\uACE0 \uB2E4\uC74C \uBBF8\uAC80\uD1A0 \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.";
        private Visibility comparisonVisibility = Visibility.Collapsed;
        private string comparisonCandidateText = "-";
        private string comparisonCurrentText = "-";
        private string comparisonOverlapText = "\uACB9\uCE68\n0%";
        private string comparisonDecisionText = string.Empty;
        private string postActionPolicyText = string.Empty;
        private Visibility reviewHistoryVisibility = Visibility.Collapsed;
        private bool isReviewHistoryExpanded;
        private string reviewHistoryHeaderText = "\uAC80\uD1A0 \uC774\uB825 0\uAC74";
        private string reviewHistorySummaryText = "\uD655\uC815/\uC228\uAE40 \uC791\uC5C5\uC774 \uC788\uC73C\uBA74 \uC5EC\uAE30\uC5D0 \uC694\uC57D\uB429\uB2C8\uB2E4.";
        private Visibility modelComparisonVisibility = Visibility.Collapsed;
        private Visibility modelComparisonExampleListVisibility = Visibility.Collapsed;
        private bool isModelComparisonExamplesExpanded;
        private string modelComparisonExampleHeaderText = "\uAC80\uC99D \uC608\uC2DC 0\uAC74";
        private string modelComparisonExampleSummaryText = "\uC608\uC2DC\uAC00 \uC788\uC73C\uBA74 \uD3BC\uCCD0\uC11C \uC774\uBBF8\uC9C0\uBCC4 \uCC28\uC774 \uC704\uCE58\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.";
        private string modelComparisonStatusText = "\uBAA8\uB378 \uBE44\uAD50: \uB300\uAE30";
        private string modelComparisonDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uB300\uAE30";
        private string modelComparisonPromotionDecision = string.Empty;
        private string modelComparisonSourceText = DefaultModelComparisonSourceText;
        private string modelComparisonDetailText = "\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uAC80\uC99D\uD558\uBA74 \uC774\uACF3\uC5D0 \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uBAA8\uB378\uC758 \uCC28\uC774\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private string modelComparisonBenchmarkText = string.Empty;
        private Visibility modelComparisonBenchmarkVisibility = Visibility.Collapsed;
        private Visibility modelComparisonHistoryVisibility = Visibility.Collapsed;
        private WpfModelComparisonHistoryItem selectedModelComparisonHistoryItem;
        private bool isHistoricalModelComparisonSelection;
        private string modelComparisonActionText = "\uB2E4\uC74C: \uD6C4\uBCF4\uAC00 \uC788\uC73C\uBA74 \uD559\uC2B5/\uBAA8\uB378 \uC13C\uD130\uC758 \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB204\uB974\uACE0, \uAC80\uD1A0 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC138\uC694.";
        private string modelCandidateDecisionStatusText = "\uD6C4\uBCF4 \uACB0\uC815: \uB300\uAE30";
        private string modelCandidateDecisionDetailText = "\uBE44\uAD50 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uAC70\uB098 \uD6C4\uBCF4\uB97C \uAC70\uC808\uD574 \uD604\uC7AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4.";
        private string saveModelCandidateToolTip = "\uC800\uC7A5\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string rejectModelCandidateToolTip = "\uAC70\uC808\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private bool isSaveModelCandidateEnabled;
        private bool isRejectModelCandidateEnabled;
        private Visibility modelCandidateDecisionVisibility = Visibility.Collapsed;
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
        private ICommand modelComparisonHistorySelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand saveModelCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand rejectModelCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand candidateSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand candidatePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);

        public string ViewName => nameof(WpfCandidateReviewPanel);

        public string PanelModeTitleText
        {
            get => panelModeTitleText;
            private set => SetProperty(ref panelModeTitleText, value ?? string.Empty);
        }

        public string PanelModeBadgeText
        {
            get => panelModeBadgeText;
            private set => SetProperty(ref panelModeBadgeText, value ?? string.Empty);
        }

        public string PanelModeScopeText
        {
            get => panelModeScopeText;
            private set => SetProperty(ref panelModeScopeText, value ?? string.Empty);
        }

        public string PanelModeDetailText => "\uC774 \uD328\uB110\uC740 \uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 AI \uD6C4\uBCF4\uB97C \uD655\uC778\uD569\uB2C8\uB2E4. \uD655\uC815\uD558\uBA74 \uC800\uC7A5 \uB77C\uBCA8\uC774 \uB418\uACE0, \uC2A4\uD0B5\uD558\uBA74 \uD6C4\uBCF4 \uD45C\uC2DC\uB9CC \uC228\uAE41\uB2C8\uB2E4.";

        public string ReviewActionGuideText => "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 AI \uD6C4\uBCF4\uB97C \uBA3C\uC800 \uD655\uC815\uD558\uAC70\uB098 \uC2A4\uD0B5\uD558\uC138\uC694. \uD655\uC815=\uC800\uC7A5 \uB77C\uBCA8 \uCD94\uAC00, \uC2A4\uD0B5=\uD6C4\uBCF4\uB9CC \uC228\uAE40, \uBAA8\uB378 \uAC80\uC99D\uC740 \uC544\uB798 \uCE74\uB4DC\uC5D0\uC11C \uB530\uB85C \uD310\uB2E8\uD569\uB2C8\uB2E4.";

        public string ModelComparisonSectionTitleText => "\uD559\uC2B5 \uBAA8\uB378 \uAC80\uC99D";

        public string ModelComparisonSectionDetailText => "\uD559\uC2B5\uC774 \uB05D\uB09C \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50\uD558\uACE0 \uCC44\uD0DD \uC5EC\uBD80\uB97C \uD310\uB2E8\uD569\uB2C8\uB2E4.";

        public string CurrentImageReviewRoleTitleText => "\uD604\uC7AC \uC774\uBBF8\uC9C0 AI \uD6C4\uBCF4";

        public string CurrentImageReviewRoleDetailText => "\uD655\uC815/\uC2A4\uD0B5\uC740 \uC9C0\uAE08 \uBCF4\uB294 \uC774\uBBF8\uC9C0\uC5D0\uB9CC \uC801\uC6A9";

        public string CurrentImageReviewRoleResultText => "\uACB0\uACFC: \uD655\uC815=\uC800\uC7A5 \uB77C\uBCA8 \uCD94\uAC00 / \uC2A4\uD0B5=\uD6C4\uBCF4\uB9CC \uC228\uAE40";

        public string ModelValidationRoleTitleText => "\uD559\uC2B5 \uBAA8\uB378 \uAC80\uC99D";

        public string ModelValidationRoleDetailText => "\uD559\uC2B5 \uACB0\uACFC \uD6C4\uBCF4\uB97C \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50";

        public string ModelValidationRoleResultText => "\uACB0\uACFC: \uBAA8\uB378\uC13C\uD130\uC5D0\uC11C \uCC44\uD0DD/\uC720\uC9C0 \uD655\uC815";

        public WpfBulkObservableCollection<WpfCandidateReviewListItem> Candidates { get; } = new WpfBulkObservableCollection<WpfCandidateReviewListItem>();

        public ObservableCollection<string> ReviewHistory { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfModelComparisonReviewExample> ModelComparisonExamples { get; } = new ObservableCollection<WpfModelComparisonReviewExample>();

        public ObservableCollection<WpfModelComparisonHistoryItem> ModelComparisonHistoryItems { get; } = new ObservableCollection<WpfModelComparisonHistoryItem>();

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

        public ICommand ModelComparisonHistorySelectionChangedCommand
        {
            get => modelComparisonHistorySelectionChangedCommand;
            private set => SetProperty(ref modelComparisonHistorySelectionChangedCommand, value);
        }

        public ICommand SaveModelCandidateCommand
        {
            get => saveModelCandidateCommand;
            private set => SetProperty(ref saveModelCandidateCommand, value);
        }

        public ICommand RejectModelCandidateCommand
        {
            get => rejectModelCandidateCommand;
            private set => SetProperty(ref rejectModelCandidateCommand, value);
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

        public string CandidateCountSummaryText
        {
            get => candidateCountSummaryText;
            private set => SetProperty(ref candidateCountSummaryText, value ?? string.Empty);
        }

        public Visibility CandidateInteractionVisibility
        {
            get => candidateInteractionVisibility;
            private set => SetProperty(ref candidateInteractionVisibility, value);
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

        public string CompletionTitleText
        {
            get => completionTitleText;
            private set => SetProperty(ref completionTitleText, value ?? string.Empty);
        }

        public string CompletionDetailText
        {
            get => completionDetailText;
            private set => SetProperty(ref completionDetailText, value ?? string.Empty);
        }

        public string CompletionNextActionText
        {
            get => completionNextActionText;
            private set => SetProperty(ref completionNextActionText, value ?? string.Empty);
        }

        public string CompleteImageAndNextActionText
        {
            get => completeImageAndNextActionText;
            private set => SetProperty(ref completeImageAndNextActionText, value ?? string.Empty);
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

        public bool IsReviewHistoryExpanded
        {
            get => isReviewHistoryExpanded;
            set => SetProperty(ref isReviewHistoryExpanded, value);
        }

        public string ReviewHistoryHeaderText
        {
            get => reviewHistoryHeaderText;
            private set => SetProperty(ref reviewHistoryHeaderText, value ?? string.Empty);
        }

        public string ReviewHistorySummaryText
        {
            get => reviewHistorySummaryText;
            private set => SetProperty(ref reviewHistorySummaryText, value ?? string.Empty);
        }

        public Visibility ModelComparisonVisibility
        {
            get => modelComparisonVisibility;
            private set => SetProperty(ref modelComparisonVisibility, value);
        }

        public Visibility ModelComparisonExampleListVisibility
        {
            get => modelComparisonExampleListVisibility;
            private set => SetProperty(ref modelComparisonExampleListVisibility, value);
        }

        public Visibility ModelComparisonHistoryVisibility
        {
            get => modelComparisonHistoryVisibility;
            private set => SetProperty(ref modelComparisonHistoryVisibility, value);
        }

        public WpfModelComparisonHistoryItem SelectedModelComparisonHistoryItem
        {
            get => selectedModelComparisonHistoryItem;
            set => SetProperty(ref selectedModelComparisonHistoryItem, value);
        }

        public bool IsHistoricalModelComparisonSelection
        {
            get => isHistoricalModelComparisonSelection;
            private set
            {
                if (SetProperty(ref isHistoricalModelComparisonSelection, value))
                {
                    OnPropertyChanged(nameof(IsModelPromotionHeld));
                }
            }
        }

        public bool IsModelComparisonExamplesExpanded
        {
            get => isModelComparisonExamplesExpanded;
            set => SetProperty(ref isModelComparisonExamplesExpanded, value);
        }

        public string ModelComparisonExampleHeaderText
        {
            get => modelComparisonExampleHeaderText;
            private set => SetProperty(ref modelComparisonExampleHeaderText, value ?? string.Empty);
        }

        public string ModelComparisonExampleSummaryText
        {
            get => modelComparisonExampleSummaryText;
            private set => SetProperty(ref modelComparisonExampleSummaryText, value ?? string.Empty);
        }

        public string ModelComparisonStatusText
        {
            get => modelComparisonStatusText;
            private set => SetProperty(ref modelComparisonStatusText, value ?? string.Empty);
        }

        public string ModelComparisonDecisionText
        {
            get => modelComparisonDecisionText;
            private set => SetProperty(ref modelComparisonDecisionText, value ?? string.Empty);
        }

        public string ModelComparisonPromotionDecision
        {
            get => modelComparisonPromotionDecision;
            private set
            {
                if (SetProperty(ref modelComparisonPromotionDecision, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(IsModelPromotionHeld));
                }
            }
        }

        public bool IsModelPromotionHeld => IsHistoricalModelComparisonSelection
            || string.Equals(ModelComparisonPromotionDecision, "hold", StringComparison.OrdinalIgnoreCase);

        public string ModelComparisonSourceText
        {
            get => modelComparisonSourceText;
            private set => SetProperty(ref modelComparisonSourceText, value ?? string.Empty);
        }

        public string ModelComparisonDetailText
        {
            get => modelComparisonDetailText;
            private set => SetProperty(ref modelComparisonDetailText, value ?? string.Empty);
        }

        public string ModelComparisonBenchmarkText
        {
            get => modelComparisonBenchmarkText;
            private set
            {
                if (SetProperty(ref modelComparisonBenchmarkText, value ?? string.Empty))
                {
                    ModelComparisonBenchmarkVisibility = string.IsNullOrWhiteSpace(modelComparisonBenchmarkText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
        }

        public Visibility ModelComparisonBenchmarkVisibility
        {
            get => modelComparisonBenchmarkVisibility;
            private set => SetProperty(ref modelComparisonBenchmarkVisibility, value);
        }

        public string ModelComparisonActionText
        {
            get => modelComparisonActionText;
            private set => SetProperty(ref modelComparisonActionText, value ?? string.Empty);
        }

        public string ModelCandidateDecisionStatusText
        {
            get => modelCandidateDecisionStatusText;
            private set => SetProperty(ref modelCandidateDecisionStatusText, value ?? string.Empty);
        }

        public string ModelCandidateDecisionDetailText
        {
            get => modelCandidateDecisionDetailText;
            private set => SetProperty(ref modelCandidateDecisionDetailText, value ?? string.Empty);
        }

        public bool IsSaveModelCandidateEnabled
        {
            get => isSaveModelCandidateEnabled;
            private set => SetProperty(ref isSaveModelCandidateEnabled, value);
        }

        public bool IsRejectModelCandidateEnabled
        {
            get => isRejectModelCandidateEnabled;
            private set => SetProperty(ref isRejectModelCandidateEnabled, value);
        }

        public Visibility ModelCandidateDecisionVisibility
        {
            get => modelCandidateDecisionVisibility;
            private set => SetProperty(ref modelCandidateDecisionVisibility, value);
        }

        public string SaveModelCandidateToolTip
        {
            get => saveModelCandidateToolTip;
            private set => SetProperty(ref saveModelCandidateToolTip, value ?? string.Empty);
        }

        public string RejectModelCandidateToolTip
        {
            get => rejectModelCandidateToolTip;
            private set => SetProperty(ref rejectModelCandidateToolTip, value ?? string.Empty);
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
            Action<WpfModelComparisonReviewExample> openModelComparisonExample = null,
            Action saveModelCandidate = null,
            Action rejectModelCandidate = null,
            Action<object> modelComparisonHistorySelectionChanged = null)
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
            ModelComparisonHistorySelectionChangedCommand = new RelayCommand<object>(modelComparisonHistorySelectionChanged ?? NoOpSelectionCommand);
            SaveModelCandidateCommand = new RelayCommand(saveModelCandidate ?? NoOpCommand);
            RejectModelCandidateCommand = new RelayCommand(rejectModelCandidate ?? NoOpCommand);
        }

        public void SetCandidates(
            IEnumerable<WpfCandidateReviewListItem> candidates,
            string detail,
            object preferredPayload = null,
            int totalCandidateCount = -1)
        {
            SelectedCandidate = null;
            List<WpfCandidateReviewListItem> rows = (candidates ?? Array.Empty<WpfCandidateReviewListItem>()).ToList();
            Candidates.ReplaceAll(rows);

            int visibleCandidateCount = rows.Count(item => item?.IsEnabled == true);
            int pendingCandidateCount = totalCandidateCount < 0
                ? visibleCandidateCount
                : Math.Max(totalCandidateCount, visibleCandidateCount);
            CandidateCountSummaryText = FormatCandidateCount(visibleCandidateCount);
            UpdateCandidateAvailabilityPresentation(visibleCandidateCount, pendingCandidateCount);
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
            CompleteImageAndNextActionText = "\uC774\uBBF8\uC9C0 \uC644\uB8CC";
        }

        public void SetCompletionState(WpfCandidateReviewCompletionPresentation presentation)
        {
            if (presentation == null)
            {
                SetCompletionState(false, "\uC644\uB8CC\uD560 \uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC5F4\uC5B4\uC8FC\uC138\uC694.");
                CompletionTitleText = "\uAC80\uD1A0 \uB300\uAE30";
                CompletionDetailText = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uAC70\uB098 \uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uBA74 \uD6C4\uBCF4 \uAC80\uD1A0 \uC0C1\uD0DC\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
                CompletionNextActionText = "\uB2E4\uC74C: \uC774\uBBF8\uC9C0 \uC120\uD0DD \uB610\uB294 \uAC80\uC0AC";
                return;
            }

            IsCompleteImageAndNextEnabled = presentation.CanComplete;
            CompleteImageAndNextToolTip = presentation.ToolTip;
            CompleteImageAndNextActionText = string.IsNullOrWhiteSpace(presentation.ButtonText)
                ? "\uC774\uBBF8\uC9C0 \uC644\uB8CC"
                : presentation.ButtonText;
            CompletionTitleText = presentation.TitleText;
            CompletionDetailText = presentation.DetailText;
            CompletionNextActionText = presentation.NextActionText;
        }

        public void ClearComparison()
        {
            ComparisonVisibility = Visibility.Collapsed;
            ComparisonCandidateText = "-";
            ComparisonCurrentText = "-";
            ComparisonOverlapText = "\uACB9\uCE68\n0%";
            ComparisonDecisionText = string.Empty;
            SelectedCandidateSummaryText = "\uC120\uD0DD: AI \uD6C4\uBCF4 \uC5C6\uC74C";
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
                ? SelectedCandidate?.Title ?? "\uC120\uD0DD: AI \uD6C4\uBCF4 \uC5C6\uC74C"
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
            IsReviewHistoryExpanded = false;
            UpdateReviewHistoryDisclosure();
        }

        public void ClearModelComparisonReview()
        {
            ModelComparisonExamples.Clear();
            ModelComparisonHistoryItems.Clear();
            SelectedModelComparisonHistoryItem = null;
            ModelComparisonHistoryVisibility = Visibility.Collapsed;
            IsHistoricalModelComparisonSelection = false;
            IsModelComparisonExamplesExpanded = false;
            UpdateModelComparisonExampleDisclosure();
            ModelComparisonStatusText = "\uBAA8\uB378 \uBE44\uAD50: \uB300\uAE30";
            ModelComparisonDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uB300\uAE30";
            ModelComparisonPromotionDecision = string.Empty;
            ModelComparisonSourceText = DefaultModelComparisonSourceText;
            ModelComparisonDetailText = "\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uAC80\uC99D\uD558\uBA74 \uC774\uACF3\uC5D0 \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uBAA8\uB378\uC758 \uCC28\uC774\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
            ModelComparisonBenchmarkText = string.Empty;
            ModelComparisonActionText = "\uB2E4\uC74C: \uD6C4\uBCF4\uAC00 \uC788\uC73C\uBA74 \uD559\uC2B5/\uBAA8\uB378 \uC13C\uD130\uC758 \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB204\uB974\uACE0, \uAC80\uD1A0 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC138\uC694.";
            ModelComparisonVisibility = Visibility.Collapsed;
            SetModelCandidateDecisionState(false, false, null, null, null, null);
        }

        public void SetModelComparisonSourceText(string sourceText)
        {
            ModelComparisonSourceText = string.IsNullOrWhiteSpace(sourceText)
                ? DefaultModelComparisonSourceText
                : sourceText.Trim();
        }

        public void SetModelComparisonHistory(
            IEnumerable<WpfModelComparisonHistoryItem> items,
            string selectedSourcePath = "")
        {
            List<WpfModelComparisonHistoryItem> rows = (items ?? Array.Empty<WpfModelComparisonHistoryItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.SourcePath))
                .GroupBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            ModelComparisonHistoryItems.Clear();
            foreach (WpfModelComparisonHistoryItem row in rows)
            {
                ModelComparisonHistoryItems.Add(row);
            }

            SelectedModelComparisonHistoryItem = rows.FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(selectedSourcePath)
                    && string.Equals(item.SourcePath, selectedSourcePath, StringComparison.OrdinalIgnoreCase))
                ?? rows.FirstOrDefault();
            ModelComparisonHistoryVisibility = rows.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void SetModelComparisonReview(
            WpfModelComparisonReviewReport report,
            bool isHistoricalSelection = false)
        {
            ModelComparisonExamples.Clear();
            IsHistoricalModelComparisonSelection = report?.HasComparison == true && isHistoricalSelection;
            if (report?.HasComparison != true)
            {
                IsModelComparisonExamplesExpanded = false;
                UpdateModelComparisonExampleDisclosure();
                ModelComparisonStatusText = "\uBAA8\uB378 \uBE44\uAD50: \uC544\uC9C1 \uC2E4\uD589 \uC548 \uB428";
                ModelComparisonDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uBE44\uAD50 \uD544\uC694";
                ModelComparisonPromotionDecision = string.Empty;
                ModelComparisonBenchmarkText = string.Empty;
                ModelComparisonDetailText = "\uBE44\uAD50 \uACB0\uACFC \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uD559\uC2B5/\uBAA8\uB378 \uC13C\uD130\uC5D0\uC11C \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB204\uB974\uAC70\uB098 Guide\uC5D0\uC11C \uBAA8\uB378 \uBE44\uAD50\uB97C \uC2E4\uD589\uD558\uC138\uC694.";
                ModelComparisonActionText = "\uBAA8\uB378 \uD6C4\uBCF4\uB97C \uC801\uC6A9\uD558\uB824\uBA74 \uBE44\uAD50 \uACB0\uACFC\uB97C \uD655\uC778\uD55C \uB4A4 \uBAA8\uB378\uC13C\uD130\uC758 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5 \uBC84\uD2BC\uC744 \uB204\uB974\uC138\uC694.";
                ModelComparisonVisibility = Visibility.Collapsed;
                return;
            }

            ModelComparisonStatusText = report.SummaryText;
            ModelComparisonPromotionDecision = report.PromotionDecision;
            ModelComparisonBenchmarkText = report.BenchmarkText;
            ModelComparisonDecisionText = IsHistoricalModelComparisonSelection
                ? "\uACFC\uAC70 \uC2E4\uD589 \uBCF4\uAE30: \uCC38\uACE0\uC6A9 - \uBAA8\uB378 \uCC44\uD0DD \uD310\uB2E8 \uC544\uB2D8"
                : string.IsNullOrWhiteSpace(report.RecommendationText)
                    ? "\uAD50\uCCB4 \uD310\uB2E8: \uC608\uC2DC \uAC80\uD1A0"
                    : report.RecommendationText;
            ModelComparisonDetailText = string.IsNullOrWhiteSpace(report.SourcePath)
                ? report.DetailText
                : $"{report.DetailText} / {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(report.SourcePath) ?? report.SourcePath)}";
            ModelComparisonActionText = IsHistoricalModelComparisonSelection
                ? "\uB2E4\uC74C: \uACFC\uAC70 \uACB0\uACFC\uB294 \uBE44\uAD50 \uCC38\uACE0\uC6A9\uC785\uB2C8\uB2E4. \uBAA8\uB378 \uCC44\uD0DD\uC740 \uCD5C\uC2E0 \uC2E4\uD589\uC73C\uB85C \uB3CC\uC544\uAC00 \uD310\uB2E8\uD558\uC138\uC694."
                : report.IsEngineComparison
                    ? "\uB2E4\uC74C: \uC774\uBBF8\uC9C0\uBCC4 \uCC28\uC774\uC640 Takt\uB97C \uD655\uC778\uD558\uC138\uC694. \uC5D4\uC9C4 \uCC44\uD0DD\uC740 \uAC00\uC911\uCE58\uB9CC \uBC14\uAFB8\uC9C0 \uB9D0\uACE0 Python/\uB7F0\uD0C0\uC784 \uC124\uC815\uAE4C\uC9C0 \uD568\uAED8 \uC801\uC6A9\uD574\uC57C \uD569\uB2C8\uB2E4."
                : string.Equals(report.PromotionDecision, "hold", StringComparison.OrdinalIgnoreCase)
                    ? "\uB2E4\uC74C: \uB2E4\uC591\uD55C \uD559\uC2B5 \uB370\uC774\uD130\uB97C \uBCF4\uAC15\uD558\uAC70\uB098 \uBAA8\uB378\uC744 \uC870\uC815\uD55C \uB4A4 \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694."
                    : (report.Examples?.Count ?? 0) > 0
                        ? "\uB2E4\uC74C: \uC544\uB798 \uC608\uC2DC\uB97C \uD074\uB9AD\uD574 \uC774\uBBF8\uC9C0 \uC704\uCE58\uB97C \uD655\uC778\uD55C \uB4A4 Guide\uC758 \uAD50\uCCB4 \uD310\uB2E8\uC744 \uBCF4\uACE0, \uBAA8\uB378\uC13C\uD130\uC5D0\uC11C \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC138\uC694."
                        : "\uB2E4\uC74C: \uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC\uAC00 \uC5C6\uC73C\uBA74 \uAC80\uC99D \uB370\uC774\uD130 \uC218\uC640 \uC9C0\uD45C\uB97C \uD655\uC778\uD55C \uB4A4 \uBAA8\uB378\uC13C\uD130\uC5D0\uC11C \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uAC70\uB098 \uD604\uC7AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD558\uC138\uC694.";

            foreach (WpfModelComparisonReviewExample example in report.Examples ?? Array.Empty<WpfModelComparisonReviewExample>())
            {
                if (example != null)
                {
                    ModelComparisonExamples.Add(example);
                }
            }

            IsModelComparisonExamplesExpanded = false;
            UpdateModelComparisonExampleDisclosure();
            ModelComparisonVisibility = Visibility.Visible;
        }

        public void SetModelCandidateDecisionState(
            bool canSave,
            bool canReject,
            string statusText,
            string detailText,
            string saveToolTip,
            string rejectToolTip)
        {
            IsSaveModelCandidateEnabled = canSave;
            IsRejectModelCandidateEnabled = canReject;
            ModelCandidateDecisionVisibility = canSave || canReject
                ? Visibility.Visible
                : Visibility.Collapsed;
            ModelCandidateDecisionStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "\uD6C4\uBCF4 \uACB0\uC815: \uB300\uAE30"
                : statusText;
            ModelCandidateDecisionDetailText = string.IsNullOrWhiteSpace(detailText)
                ? "\uBE44\uAD50 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uAC70\uB098 \uD6C4\uBCF4\uB97C \uAC70\uC808\uD574 \uD604\uC7AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4."
                : detailText;
            SaveModelCandidateToolTip = string.IsNullOrWhiteSpace(saveToolTip)
                ? "\uC800\uC7A5\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
                : saveToolTip;
            RejectModelCandidateToolTip = string.IsNullOrWhiteSpace(rejectToolTip)
                ? "\uAC70\uC808\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
                : rejectToolTip;
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
            ModelComparisonActionText = "\uD655\uC778 \uD6C4: \uC774 \uC608\uC2DC\uAC00 \uC0C8 \uBAA8\uB378\uC5D0 \uC720\uB9AC\uD55C\uC9C0 \uD310\uB2E8\uD558\uACE0, Guide\uC758 \uAD50\uCCB4 \uD310\uB2E8\uC744 \uBCF8 \uB4A4 \uBAA8\uB378\uC13C\uD130\uC5D0\uC11C \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC138\uC694.";
            UpdateModelComparisonExampleDisclosure();
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

            UpdateReviewHistoryDisclosure();
        }

        private void UpdateModelComparisonExampleDisclosure()
        {
            int count = ModelComparisonExamples.Count;
            ModelComparisonExampleListVisibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ModelComparisonExampleHeaderText = count <= 0
                ? "\uAC80\uC99D \uC608\uC2DC 0\uAC74"
                : $"\uAC80\uC99D \uC608\uC2DC {count}\uAC74";
            ModelComparisonExampleSummaryText = count <= 0
                ? "\uC608\uC2DC\uAC00 \uC788\uC73C\uBA74 \uD3BC\uCCD0\uC11C \uC774\uBBF8\uC9C0\uBCC4 \uCC28\uC774 \uC704\uCE58\uB97C \uD655\uC778\uD569\uB2C8\uB2E4."
                : "\uAE30\uBCF8\uC740 \uC811\uC5B4 \uB450\uACE0, \uD544\uC694\uD560 \uB54C\uB9CC \uD3BC\uCCD0\uC11C \uC774\uBBF8\uC9C0\uBCC4 \uCC28\uC774\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.";
            if (count <= 0)
            {
                IsModelComparisonExamplesExpanded = false;
            }
        }

        private void UpdateReviewHistoryDisclosure()
        {
            int count = ReviewHistory.Count;
            ReviewHistoryVisibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ReviewHistoryHeaderText = count <= 0
                ? "\uAC80\uD1A0 \uC774\uB825 0\uAC74"
                : $"\uAC80\uD1A0 \uC774\uB825 {count}\uAC74";
            ReviewHistorySummaryText = count <= 0
                ? "\uD655\uC815/\uC228\uAE40 \uC791\uC5C5\uC774 \uC788\uC73C\uBA74 \uC5EC\uAE30\uC5D0 \uC694\uC57D\uB429\uB2C8\uB2E4."
                : "\uCD5C\uADFC \uAC80\uD1A0 \uC791\uC5C5\uB9CC \uC694\uC57D\uD558\uACE0, \uC0C1\uC138 \uC774\uB825\uC740 \uD3BC\uCCD0\uC11C \uD655\uC778\uD569\uB2C8\uB2E4.";
            if (count <= 0)
            {
                IsReviewHistoryExpanded = false;
            }
        }

        private void UpdateCandidateAvailabilityPresentation(int visibleCandidateCount, int pendingCandidateCount)
        {
            CandidateInteractionVisibility = pendingCandidateCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (visibleCandidateCount > 0)
            {
                PanelModeTitleText = "\uD604\uC7AC \uC774\uBBF8\uC9C0 AI \uD6C4\uBCF4 \uD655\uC778";
                PanelModeBadgeText = "AI \uD6C4\uBCF4 \uAC80\uD1A0";
                PanelModeScopeText = "\uB9DE\uB294 \uD6C4\uBCF4\uB9CC \uD655\uC815\uD558\uBA74 \uC800\uC7A5 \uB77C\uBCA8\uB85C \uCD94\uAC00\uB429\uB2C8\uB2E4. \uD6C4\uBCF4 \uC228\uAE40\uC740 \uC800\uC7A5 \uB77C\uBCA8\uC744 \uBC14\uAFB8\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
                return;
            }

            if (pendingCandidateCount > 0)
            {
                PanelModeTitleText = "\uD604\uC7AC \uC2E0\uB8B0\uB3C4 \uAE30\uC900\uC5D0 \uD45C\uC2DC\uB418\uB294 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4";
                PanelModeBadgeText = "AI \uD6C4\uBCF4 \uAC80\uD1A0";
                PanelModeScopeText = "\uD6C4\uBCF4\uAC00 \uC2E0\uB8B0\uB3C4 \uAE30\uC900 \uC544\uB798\uC5D0 \uC788\uC2B5\uB2C8\uB2E4. \uC2E0\uB8B0\uB3C4\uB97C \uB0AE\uCD94\uBA74 \uB2E4\uC2DC \uD45C\uC2DC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                return;
            }

            PanelModeTitleText = "\uAC80\uD1A0\uD560 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4";
            PanelModeBadgeText = "\uC120\uD0DD \uB2E8\uACC4";
            PanelModeScopeText = "AI \uC790\uB3D9 \uB77C\uBCA8\uC744 \uC0AC\uC6A9\uD560 \uB54C\uB9CC \uC704\uC758 \uD604\uC7AC \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC138\uC694. \uC9C1\uC811 \uB77C\uBCA8\uB9C1\uC744 \uB9C8\uCCE4\uB2E4\uBA74 4 \uD559\uC2B5/\uBAA8\uB378\uB85C \uC774\uB3D9\uD558\uC138\uC694. \uC800\uC7A5\uD55C \uB77C\uBCA8\uC740 \uADF8\uB300\uB85C \uC720\uC9C0\uB429\uB2C8\uB2E4.";
        }

        private static string FormatCandidateCount(int count)
            => count <= 0 ? "AI \uD6C4\uBCF4 0\uAC1C" : $"AI \uD6C4\uBCF4 {count}\uAC1C";
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
