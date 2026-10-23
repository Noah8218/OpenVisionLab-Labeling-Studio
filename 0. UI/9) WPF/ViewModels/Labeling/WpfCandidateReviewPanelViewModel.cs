using OpenVisionLab.Mvvm;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public sealed record LabelSuspicionListItem(LabelSuspicionItem Item)
    {
        public string DisplayText => OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Kind." + Item.Kind)
            + (Item.Label == null ? string.Empty : " · " + Item.Label.ClassName)
            + (Item.Prediction == null || Item.Prediction.ImageLevel ? string.Empty : " → " + Item.Prediction.ClassName + " " + Item.Prediction.Confidence.ToString("P0", CultureInfo.CurrentCulture));
        public string FixText => Item.Kind == LabelSuspicionKind.Class
            ? string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Fix.Class"), Item.Prediction?.ClassName)
            : OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Fix." + Item.Kind);
    }

    public sealed class WpfCandidateReviewPanelViewModel : WpfObservableViewModel
    {
        private static string DefaultModelComparisonSourceText => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.SourceDefault");
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<double> NoOpValueCommand = _ => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private static readonly Action<WpfModelComparisonReviewExample> NoOpModelComparisonExampleCommand = _ => { };
        private WpfModelComparisonReviewExample selectedComparisonExample;
        private LabelSuspicionReport suspicionReport;
        private bool suspicionVisible;
        private bool suspicionOverlayVisible = true;
        private Action<bool> suspicionOverlayChanged;
        private ICommand fixLabelSuspicionCommand = new RelayCommand(NoOpCommand, () => false);
        private ICommand dismissLabelSuspicionCommand = new RelayCommand(NoOpCommand, () => false);
        private ICommand undoLabelSuspicionDismissalCommand = new RelayCommand(NoOpCommand, () => false);
        private Func<bool> canEditSuspicion;
        private bool suspicionDismissed;
        private bool suspicionIsPreview;
        private bool suspicionActionRunning;

        private WpfModelComparisonReviewReport currentComparisonReport;
        private WpfModelComparisonReviewReport returnComparisonReport;
        private WpfModelComparisonReviewExample returnComparisonExample;
        private WpfModelComparisonHistoryItem returnComparisonHistory;
        private string returnComparisonSource;
        private bool returnComparisonHistorical;
        private bool isModelErrorReviewActive;
        private ImageSource baselineComparisonPreviewImage;
        private ImageSource candidateComparisonPreviewImage;
        private string comparisonPreviewErrorText = string.Empty;
        private Func<bool, bool> beginModelErrorReview;
        private Func<WpfModelComparisonReviewExample, bool> returnToModelComparison;
        private Func<bool> canNavigateModelReview;
        private ICommand modelErrorLabelReviewCommand = new RelayCommand(NoOpCommand);
        private ICommand modelErrorQualityReviewCommand = new RelayCommand(NoOpCommand);
        private ICommand returnToModelComparisonCommand = new RelayCommand(NoOpCommand);
        private string panelModeTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.NoSuggestions");
        private string panelModeBadgeText = "\uC120\uD0DD \uB2E8\uACC4";
        private string panelModeScopeText = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Assistance.Panel.Empty");
        private Visibility candidateInteractionVisibility = Visibility.Collapsed;
        private string confidenceText = "0%";
        private double displayConfidence;
        private string inferenceRecipeIdentity;
        private Action<double> displayConfidenceChanged;
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
        private string completionTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.Waiting");
        private string completionDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.OpenOrInfer");
        private string completionNextActionText = "\uB2E4\uC74C: \uC774\uBBF8\uC9C0 \uC120\uD0DD \uB610\uB294 \uAC80\uC0AC";
        private string completeImageAndNextActionText = "\uC774\uBBF8\uC9C0 \uC644\uB8CC";
        private string confirmSelectedToolTip = "\uD655\uC815\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string confirmAllToolTip = "\uD655\uC815 \uAC00\uB2A5\uD55C \uD45C\uC2DC \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        private string skipSelectedToolTip = "\uAC74\uB108\uB6F8 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        private string focusCurrentLabelToolTip = "\uACB9\uCE58\uB294 \uAE30\uC874 \uB77C\uBCA8\uC744 \uB77C\uBCA8 \uBAA9\uB85D\uACFC \uD654\uBA74\uC5D0\uC11C \uC120\uD0DD\uD569\uB2C8\uB2E4.";
        private string completeImageAndNextToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.NextUnworked");
        private Visibility comparisonVisibility = Visibility.Collapsed;
        private string comparisonCandidateText = "-";
        private string comparisonCurrentText = "-";
        private string comparisonOverlapText = "\uACB9\uCE68\n0%";
        private string comparisonDecisionText = string.Empty;
        private string postActionPolicyText = string.Empty;
        private Visibility reviewHistoryVisibility = Visibility.Collapsed;
        private bool isReviewHistoryExpanded;
        private string reviewHistoryHeaderText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.NoHistory");
        private string reviewHistorySummaryText = "\uD655\uC815/\uC228\uAE40 \uC791\uC5C5\uC774 \uC788\uC73C\uBA74 \uC5EC\uAE30\uC5D0 \uC694\uC57D\uB429\uB2C8\uB2E4.";
        private Visibility modelComparisonVisibility = Visibility.Collapsed;
        private Visibility modelComparisonExampleListVisibility = Visibility.Collapsed;
        private bool isModelComparisonExamplesExpanded;
        private string modelComparisonExampleHeaderText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.NoExamples");
        private string modelComparisonExampleSummaryText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExamplesEmptyHelp");
        private string modelComparisonStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.Waiting");
        private string modelComparisonDecisionText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.DecisionWaiting");
        private string modelComparisonPromotionDecision = string.Empty;
        private string modelComparisonSourceText = DefaultModelComparisonSourceText;
        private string modelComparisonDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.DetailWaiting");
        private string modelComparisonBenchmarkText = string.Empty;
        private Visibility modelComparisonBenchmarkVisibility = Visibility.Collapsed;
        private Visibility modelComparisonHistoryVisibility = Visibility.Collapsed;
        private WpfModelComparisonHistoryItem selectedModelComparisonHistoryItem;
        private bool isHistoricalModelComparisonSelection;
        private string modelComparisonActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingNext");
        private string modelCandidateDecisionStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.CandidateWaiting");
        private string modelCandidateDecisionDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.CandidateDetail");
        private string saveModelCandidateToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingSaveEmpty");
        private string rejectModelCandidateToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingRejectEmpty");
        private bool isSaveModelCandidateEnabled;
        private bool isRejectModelCandidateEnabled;
        private Visibility modelCandidateDecisionVisibility = Visibility.Collapsed;
        private bool isComparisonHighOverlap;
        private ICommand confidenceChangedCommand = new RelayCommand<double>(NoOpValueCommand);
        private ICommand candidateClassFilterChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
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
        private Action<WpfCandidateReviewListItem> candidateSelectionChangedAction = _ => { };
        private Action<string> candidateClassFilterChangedAction = _ => { };
        private bool isRefreshingCandidateClassFilters;
        private string selectedCandidateClassFilter = "모든 클래스";
        private Action<WpfModelComparisonHistoryItem> modelComparisonHistorySelectionChangedAction = _ => { };
        private ICommand togglePatchCoreHeatmapCommand = new RelayCommand(NoOpCommand);
        private Visibility patchCoreHeatmapVisibility = Visibility.Collapsed;
        private Visibility patchCoreHeatmapPreviewVisibility = Visibility.Collapsed;
        private ImageSource patchCoreHeatmapSource;
        private string patchCoreHeatmapStatusText = string.Empty;
        private string patchCoreHeatmapFileName = string.Empty;
        private string patchCoreHeatmapToolTip = string.Empty;
        private string patchCoreHeatmapActionText = "히트맵 보기";
        private bool isPatchCoreHeatmapActionEnabled;
        private bool isPatchCoreHeatmapOpen;

        public bool IsModelComparisonEvidence { get; private set; }
        public bool IsModelErrorReviewActive { get => isModelErrorReviewActive; private set => SetProperty(ref isModelErrorReviewActive, value); }
        public bool HasModelComparisonPreview => BaselineComparisonPreviewImage != null && CandidateComparisonPreviewImage != null;
        public ImageSource BaselineComparisonPreviewImage { get => baselineComparisonPreviewImage; private set => SetProperty(ref baselineComparisonPreviewImage, value); }
        public ImageSource CandidateComparisonPreviewImage { get => candidateComparisonPreviewImage; private set => SetProperty(ref candidateComparisonPreviewImage, value); }
        public string ComparisonPreviewErrorText { get => comparisonPreviewErrorText; private set => SetProperty(ref comparisonPreviewErrorText, value ?? string.Empty); }
        public string ComparisonPreviewSourceText => selectedComparisonExample?.ComparisonConditionsText ?? string.Empty;

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

        public string PanelModeDetailText => OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Draft.PanelDetail");

        public string ReviewActionGuideText => OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Action.Guide");

        public string ModelComparisonSectionTitleText => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.SectionTitle");

        public string ModelComparisonSectionDetailText => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.SectionDetail");

        public string CurrentImageReviewRoleTitleText => "\uD604\uC7AC \uC774\uBBF8\uC9C0 AI \uD6C4\uBCF4";

        public string CurrentImageReviewRoleDetailText => "\uD655\uC815/\uAC74\uB108\uB6F0\uAE30\uB294 \uC9C0\uAE08 \uBCF4\uB294 \uC774\uBBF8\uC9C0\uC5D0\uB9CC \uC801\uC6A9";

        public string CurrentImageReviewRoleResultText => OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Draft.RoleResult");

        public string ModelValidationRoleTitleText => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.SectionTitle");

        public string ModelValidationRoleDetailText => "\uD559\uC2B5 \uACB0\uACFC \uD6C4\uBCF4\uB97C \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50";

        public string ModelValidationRoleResultText => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.RoleResult");

        public ObservableCollection<LabelSuspicionListItem> LabelSuspicions { get; } = new();
        public Visibility LabelSuspicionVisibility => suspicionVisible ? Visibility.Visible : Visibility.Collapsed;
        public string LabelSuspicionTitle => suspicionReport?.CanEvaluate == true
            ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Title") + " " + LabelSuspicions.Count.ToString(CultureInfo.CurrentCulture)
            : OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Title");
        public string LabelSuspicionFixText => OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Fix");
        public string LabelSuspicionDismissText => OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Dismiss");
        public string LabelSuspicionUndoText => OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Undo");
        public string LabelSuspicionOverlayText => OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Overlay");
        public string LabelSuspicionWarningText => suspicionReport == null ? string.Empty : string.Join("\n", new[]
        {
            !suspicionReport.CanEvaluate ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Unavailable." + suspicionReport.UnavailableReason) : string.Empty,
            suspicionReport.HasTrainingImageWarning ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.TrainingWarning") : string.Empty,
            suspicionReport.HasUnknownTrainingWarning ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.UnknownTrainingWarning") : string.Empty,
            suspicionReport.HasSingleModelWarning ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.SingleModelWarning") : string.Empty
        }.Where(text => !string.IsNullOrEmpty(text)));
        public string LabelSuspicionDiagnostic => suspicionReport?.Diagnostic ?? string.Empty;
        public string LabelSuspicionStateText => suspicionReport?.CanEvaluate != true ? string.Empty : OpenVisionLab.OpenVisionLanguageService.T(
            suspicionDismissed ? "WpfLabelSuspicion.Dismissed" : suspicionIsPreview ? "WpfLabelSuspicion.Preview" : LabelSuspicions.Count == 0 ? "WpfLabelSuspicion.Clean" : "WpfLabelSuspicion.SavedBasis");
        public string LabelSuspicionCriterionText => OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.InitialCriterion");
        public string LabelSuspicionEvidenceIdentity => suspicionReport?.EvidenceIdentity ?? string.Empty;
        public ICommand FixLabelSuspicionCommand => fixLabelSuspicionCommand;
        public ICommand DismissLabelSuspicionCommand => dismissLabelSuspicionCommand;
        public ICommand UndoLabelSuspicionDismissalCommand => undoLabelSuspicionDismissalCommand;
        public bool IsSuspicionOverlayVisible
        {
            get => suspicionOverlayVisible;
            set { if (SetProperty(ref suspicionOverlayVisible, value)) suspicionOverlayChanged?.Invoke(value); }
        }

        public void ConfigureLabelSuspicionReview(Action<LabelSuspicionItem> fix, Action dismiss, Action undoDismissal, Func<bool> canEdit, Func<bool> canUndo, Action<bool> overlayChanged)
        {
            canEditSuspicion = canEdit;
            suspicionOverlayChanged = overlayChanged;
            fixLabelSuspicionCommand = new RelayCommand<object>(item => { if (item is LabelSuspicionListItem row && LabelSuspicions.Contains(row) && canEditSuspicion?.Invoke() == true) RunSuspicionAction(() => fix?.Invoke(row.Item)); },
                item => !suspicionActionRunning && suspicionReport?.CanEvaluate == true && item is LabelSuspicionListItem row && LabelSuspicions.Contains(row) && canEditSuspicion?.Invoke() == true);
            dismissLabelSuspicionCommand = new RelayCommand(() => { if (CanDismissSuspicion()) RunSuspicionAction(dismiss ?? NoOpCommand); }, CanDismissSuspicion);
            undoLabelSuspicionDismissalCommand = new RelayCommand(() => { if (canUndo?.Invoke() == true && canEditSuspicion?.Invoke() == true) RunSuspicionAction(undoDismissal ?? NoOpCommand); },
                () => !suspicionActionRunning && canUndo?.Invoke() == true && canEditSuspicion?.Invoke() == true);
            OnPropertyChanged(nameof(FixLabelSuspicionCommand)); OnPropertyChanged(nameof(DismissLabelSuspicionCommand)); OnPropertyChanged(nameof(UndoLabelSuspicionDismissalCommand));
        }

        private bool CanDismissSuspicion() => !suspicionActionRunning && suspicionReport?.CanEvaluate == true && !suspicionIsPreview
            && !string.IsNullOrEmpty(suspicionReport.EvidenceIdentity) && LabelSuspicions.Count > 0 && canEditSuspicion?.Invoke() == true;

        private void RunSuspicionAction(Action action)
        {
            if (suspicionActionRunning) return;
            suspicionActionRunning = true;
            try { action(); }
            finally { suspicionActionRunning = false; RefreshLabelSuspicionCommands(); }
        }

        public void SetLabelSuspicionReview(LabelSuspicionReport report, bool dismissed = false, bool isPreview = false)
        {
            suspicionReport = report;
            suspicionDismissed = dismissed;
            suspicionIsPreview = isPreview;
            LabelSuspicions.Clear();
            if (!dismissed) foreach (LabelSuspicionItem item in report?.Items ?? Array.Empty<LabelSuspicionItem>()) LabelSuspicions.Add(new(item));
            suspicionVisible = report != null;
            OnPropertyChanged(nameof(LabelSuspicionVisibility)); OnPropertyChanged(nameof(LabelSuspicionWarningText));
            OnPropertyChanged(nameof(LabelSuspicionTitle)); OnPropertyChanged(nameof(LabelSuspicionDiagnostic)); OnPropertyChanged(nameof(LabelSuspicionStateText));
            OnPropertyChanged(nameof(LabelSuspicionCriterionText));
            RefreshLabelSuspicionCommands();
        }

        public void RefreshLabelSuspicionLocalization()
        {
            SetLabelSuspicionReview(suspicionReport, suspicionDismissed, suspicionIsPreview);
            OnPropertyChanged(nameof(LabelSuspicionFixText)); OnPropertyChanged(nameof(LabelSuspicionDismissText));
            OnPropertyChanged(nameof(LabelSuspicionUndoText)); OnPropertyChanged(nameof(LabelSuspicionOverlayText));
        }

        public void RefreshLabelSuspicionCommands()
        {
            if (fixLabelSuspicionCommand is RelayCommand<object> fix) fix.RaiseCanExecuteChanged();
            if (dismissLabelSuspicionCommand is RelayCommand dismiss) dismiss.RaiseCanExecuteChanged();
            if (undoLabelSuspicionDismissalCommand is RelayCommand undo) undo.RaiseCanExecuteChanged();
        }
        public BulkObservableCollection<WpfCandidateReviewListItem> Candidates { get; } = new BulkObservableCollection<WpfCandidateReviewListItem>();

        public ObservableCollection<string> CandidateClassFilters { get; } = new ObservableCollection<string>
        {
            "모든 클래스"
        };

        public ObservableCollection<string> ReviewHistory { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfModelComparisonReviewExample> ModelComparisonExamples { get; } = new ObservableCollection<WpfModelComparisonReviewExample>();

        public ObservableCollection<WpfModelComparisonHistoryItem> ModelComparisonHistoryItems { get; } = new ObservableCollection<WpfModelComparisonHistoryItem>();

        public WpfCandidateReviewPanelViewModel()
        {
            PostActionPolicyText = "\uD655\uC815/\uAC74\uB108\uB6F0\uAE30 \uD6C4\uC5D0\uB294 \uB2E4\uC74C \uD6C4\uBCF4\uB85C \uC774\uB3D9";
            ConfirmRowCommand = new RelayCommand<WpfCandidateReviewListItem>(row => ExecuteRowAction(row, ConfirmSelectedCommand, confirm: true), row => row != null && Candidates.Contains(row) && row.IsInlineConfirmEnabled);
            SkipRowCommand = new RelayCommand<WpfCandidateReviewListItem>(row => ExecuteRowAction(row, SkipSelectedCommand, confirm: false), row => row != null && Candidates.Contains(row) && row.IsInlineSkipEnabled);
        }

        public ICommand ConfidenceChangedCommand
        {
            get => confidenceChangedCommand;
            private set => SetProperty(ref confidenceChangedCommand, value);
        }

        public ICommand CandidateClassFilterChangedCommand
        {
            get => candidateClassFilterChangedCommand;
            private set => SetProperty(ref candidateClassFilterChangedCommand, value);
        }

        public string SelectedCandidateClassFilter
        {
            get => selectedCandidateClassFilter;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? "모든 클래스" : value;
                if (SetProperty(ref selectedCandidateClassFilter, normalized)
                    && !isRefreshingCandidateClassFilters)
                {
                    candidateClassFilterChangedAction(normalized);
                }
            }
        }

        public string CandidateClassFilterSummaryText
            => SelectedCandidateClassFilter == "모든 클래스"
                ? "클래스: 전체"
                : $"클래스: {SelectedCandidateClassFilter}";

        public ICommand ConfirmSelectedCommand
        {
            get => confirmSelectedCommand;
            private set => SetProperty(ref confirmSelectedCommand, value);
        }

        public ICommand ConfirmRowCommand { get; }
        public ICommand SkipRowCommand { get; }

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

        public ICommand ModelErrorLabelReviewCommand { get => modelErrorLabelReviewCommand; private set => SetProperty(ref modelErrorLabelReviewCommand, value); }
        public ICommand ModelErrorQualityReviewCommand { get => modelErrorQualityReviewCommand; private set => SetProperty(ref modelErrorQualityReviewCommand, value); }
        public ICommand ReturnToModelComparisonCommand { get => returnToModelComparisonCommand; private set => SetProperty(ref returnToModelComparisonCommand, value); }

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

        public ICommand TogglePatchCoreHeatmapCommand
        {
            get => togglePatchCoreHeatmapCommand;
            private set => SetProperty(ref togglePatchCoreHeatmapCommand, value);
        }

        public Visibility PatchCoreHeatmapVisibility
        {
            get => patchCoreHeatmapVisibility;
            private set => SetProperty(ref patchCoreHeatmapVisibility, value);
        }

        public Visibility PatchCoreHeatmapPreviewVisibility
        {
            get => patchCoreHeatmapPreviewVisibility;
            private set => SetProperty(ref patchCoreHeatmapPreviewVisibility, value);
        }

        public ImageSource PatchCoreHeatmapSource
        {
            get => patchCoreHeatmapSource;
            private set => SetProperty(ref patchCoreHeatmapSource, value);
        }

        public string PatchCoreHeatmapStatusText
        {
            get => patchCoreHeatmapStatusText;
            private set => SetProperty(ref patchCoreHeatmapStatusText, value ?? string.Empty);
        }

        public string PatchCoreHeatmapFileName
        {
            get => patchCoreHeatmapFileName;
            private set => SetProperty(ref patchCoreHeatmapFileName, value ?? string.Empty);
        }

        public string PatchCoreHeatmapToolTip
        {
            get => patchCoreHeatmapToolTip;
            private set => SetProperty(ref patchCoreHeatmapToolTip, value ?? string.Empty);
        }

        public string PatchCoreHeatmapActionText
        {
            get => patchCoreHeatmapActionText;
            private set => SetProperty(ref patchCoreHeatmapActionText, value ?? string.Empty);
        }

        public bool IsPatchCoreHeatmapActionEnabled
        {
            get => isPatchCoreHeatmapActionEnabled;
            private set => SetProperty(ref isPatchCoreHeatmapActionEnabled, value);
        }

        public bool IsPatchCoreHeatmapOpen
        {
            get => isPatchCoreHeatmapOpen;
            private set => SetProperty(ref isPatchCoreHeatmapOpen, value);
        }

        public string ConfidenceText
        {
            get => confidenceText;
            set => SetProperty(ref confidenceText, value ?? string.Empty);
        }

        public double DisplayConfidence
        {
            get => displayConfidence;
            set
            {
                double next = double.IsFinite(value) ? Math.Clamp(value, 0D, 1D) : displayConfidence;
                if (!SetProperty(ref displayConfidence, next)) return;
                OnPropertyChanged(nameof(DisplayConfidenceText));
                displayConfidenceChanged?.Invoke(next);
            }
        }

        public string DisplayConfidenceText => displayConfidence.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        public void BeginInferenceRecipe(string recipeIdentity)
        {
            if (string.Equals(inferenceRecipeIdentity, recipeIdentity, StringComparison.OrdinalIgnoreCase)) return;
            inferenceRecipeIdentity = recipeIdentity;
            DisplayConfidence = .25D;
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
            Action<object> modelComparisonHistorySelectionChanged = null,
            Action togglePatchCoreHeatmap = null)
        {
            // Candidate review stays virtualized; commands keep the view declarative while shell owns workflow state.
            displayConfidenceChanged = confidenceChanged;
            ConfidenceChangedCommand = new RelayCommand<double>(value => DisplayConfidence = value);
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
            TogglePatchCoreHeatmapCommand = new RelayCommand(togglePatchCoreHeatmap ?? NoOpCommand);
        }

        public void ConfigureCandidatePreviewKeyWorkflow()
        {
            CandidatePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(ExecuteCandidatePreviewKeyDown);
        }

        public void ConfigureSelectionWorkflow(Action<WpfCandidateReviewListItem> applySelectionAction)
        {
            candidateSelectionChangedAction = applySelectionAction ?? (_ => { });
            CandidateSelectionChangedCommand = new RelayCommand<object>(ExecuteCandidateSelectionChanged);
        }

        public void ConfigureCandidateClassFilterWorkflow(Action<string> applyFilterAction)
        {
            candidateClassFilterChangedAction = applyFilterAction ?? (_ => { });
            CandidateClassFilterChangedCommand = new RelayCommand<object>(
                value => SelectedCandidateClassFilter = value as string);
        }

        public void SetCandidateClassFilters(IEnumerable<string> classNames, string preferredFilter = null)
        {
            List<string> filters = (classNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            isRefreshingCandidateClassFilters = true;
            try
            {
                CandidateClassFilters.Clear();
                CandidateClassFilters.Add("모든 클래스");
                foreach (string name in filters)
                {
                    CandidateClassFilters.Add(name);
                }

                string next = !string.IsNullOrWhiteSpace(preferredFilter)
                    && CandidateClassFilters.Any(item => string.Equals(item, preferredFilter, StringComparison.OrdinalIgnoreCase))
                        ? CandidateClassFilters.First(item => string.Equals(item, preferredFilter, StringComparison.OrdinalIgnoreCase))
                        : "모든 클래스";
                SelectedCandidateClassFilter = next;
            }
            finally
            {
                isRefreshingCandidateClassFilters = false;
            }

            OnPropertyChanged(nameof(CandidateClassFilters));
            OnPropertyChanged(nameof(CandidateClassFilterSummaryText));
        }

        public void ConfigureModelComparisonHistorySelectionWorkflow(Action<WpfModelComparisonHistoryItem> applySelectionAction)
        {
            modelComparisonHistorySelectionChangedAction = applySelectionAction ?? (_ => { });
            ModelComparisonHistorySelectionChangedCommand = new RelayCommand<object>(ExecuteModelComparisonHistorySelectionChanged);
        }

        private void ExecuteCandidatePreviewKeyDown(KeyInputCommandArgs args)
        {
            if (args == null)
            {
                return;
            }

            if (args.IsRepeat
                && (args.Key == Key.Enter
                    || args.Key == Key.Delete
                    || args.Key == Key.Back
                    || (args.Key == Key.A && (args.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)))
            {
                args.Handled = true;
                return;
            }

            if (args.Key == Key.Enter)
            {
                if ((args.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) ConfirmAllCommand.Execute(null);
                else ConfirmSelectedCommand.Execute(null);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.Delete || args.Key == Key.Back)
            {
                SkipSelectedCommand.Execute(null);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.A && (args.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ConfirmAllCommand.Execute(null);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.N)
            {
                NextCandidateCommand.Execute(null);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.P)
            {
                PreviousCandidateCommand.Execute(null);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.F)
            {
                FocusCandidateCommand.Execute(null);
                args.Handled = true;
            }
        }

        private void ExecuteCandidateSelectionChanged(object selectedItem)
        {
            WpfCandidateReviewListItem selectedCandidate = selectedItem as WpfCandidateReviewListItem;
            if (selectedItem != null && selectedCandidate == null)
            {
                return;
            }

            if (selectedCandidate != null && !Candidates.Contains(selectedCandidate))
            {
                return;
            }

            SelectedCandidate = selectedCandidate;
            candidateSelectionChangedAction?.Invoke(SelectedCandidate);
        }

        private void ExecuteModelComparisonHistorySelectionChanged(object selectedItem)
        {
            WpfModelComparisonHistoryItem selectedHistoryItem = selectedItem as WpfModelComparisonHistoryItem;
            if (selectedHistoryItem == null || !ModelComparisonHistoryItems.Contains(selectedHistoryItem))
            {
                return;
            }

            SelectedModelComparisonHistoryItem = selectedHistoryItem;
            modelComparisonHistorySelectionChangedAction?.Invoke(selectedHistoryItem);
        }

        private void ExecuteRowAction(WpfCandidateReviewListItem row, ICommand command, bool confirm)
        {
            if (row == null || !Candidates.Contains(row) || (confirm ? !row.IsInlineConfirmEnabled : !row.IsInlineSkipEnabled)) return;
            ExecuteCandidateSelectionChanged(row);
            if (ReferenceEquals(SelectedCandidate, row) && Candidates.Contains(row) && command.CanExecute(null)) command.Execute(null);
        }

        internal void RefreshInlineCommands()
        {
            ((RelayCommand<WpfCandidateReviewListItem>)ConfirmRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand<WpfCandidateReviewListItem>)SkipRowCommand).RaiseCanExecuteChanged();
        }

        public void SetCandidates(
            IEnumerable<WpfCandidateReviewListItem> candidates,
            string detail,
            object preferredPayload = null,
            int totalCandidateCount = -1)
        {
            SelectedCandidate = null;
            ClearPatchCoreHeatmapReview();
            IsModelComparisonEvidence = false;
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
                CompletionTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.Waiting");
                CompletionDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.OpenOrInfer");
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
            SelectedCandidateSummaryText = OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Selection.None");
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
                ? SelectedCandidate?.Title ?? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Selection.None")
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
                SelectedCandidateSummaryText = SelectedCandidate?.Title ?? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Selection.None");
            }
        }

        public void SetPatchCoreHeatmapAvailability(PatchCoreHeatmapAvailability availability)
        {
            ClosePatchCoreHeatmap();
            if (availability?.IsPatchCoreCandidate != true)
            {
                PatchCoreHeatmapVisibility = Visibility.Collapsed;
                PatchCoreHeatmapStatusText = string.Empty;
                PatchCoreHeatmapFileName = string.Empty;
                PatchCoreHeatmapToolTip = string.Empty;
                IsPatchCoreHeatmapActionEnabled = false;
                return;
            }

            PatchCoreHeatmapVisibility = Visibility.Visible;
            PatchCoreHeatmapStatusText = availability.StatusText;
            PatchCoreHeatmapFileName = availability.FileName;
            PatchCoreHeatmapToolTip = availability.ToolTip;
            IsPatchCoreHeatmapActionEnabled = availability.CanOpen;
        }

        public void ShowPatchCoreHeatmap(PatchCoreHeatmapLoadResult result)
        {
            if (result?.Succeeded != true || result.ImageSource == null)
            {
                ClosePatchCoreHeatmap();
                PatchCoreHeatmapStatusText = result?.StatusText ?? "히트맵 이미지를 열 수 없습니다.";
                return;
            }

            PatchCoreHeatmapSource = result.ImageSource;
            PatchCoreHeatmapPreviewVisibility = Visibility.Visible;
            PatchCoreHeatmapStatusText = result.StatusText;
            PatchCoreHeatmapActionText = "히트맵 닫기";
            IsPatchCoreHeatmapOpen = true;
        }

        public void ClosePatchCoreHeatmap()
        {
            PatchCoreHeatmapSource = null;
            PatchCoreHeatmapPreviewVisibility = Visibility.Collapsed;
            PatchCoreHeatmapActionText = "히트맵 보기";
            IsPatchCoreHeatmapOpen = false;
        }

        public void ClearPatchCoreHeatmapReview()
        {
            ClosePatchCoreHeatmap();
            PatchCoreHeatmapVisibility = Visibility.Collapsed;
            PatchCoreHeatmapStatusText = string.Empty;
            PatchCoreHeatmapFileName = string.Empty;
            PatchCoreHeatmapToolTip = string.Empty;
            IsPatchCoreHeatmapActionEnabled = false;
        }

        public void ClearReviewHistory()
        {
            ReviewHistory.Clear();
            IsReviewHistoryExpanded = false;
            UpdateReviewHistoryDisclosure();
        }

        public void ClearModelComparisonReview()
        {
            currentComparisonReport = null;
            returnComparisonReport = null;
            selectedComparisonExample = null;
            IsModelErrorReviewActive = false;
            SetModelComparisonPreview(new(null, null, string.Empty));
            IsModelComparisonEvidence = false;
            ModelComparisonExamples.Clear();
            ModelComparisonHistoryItems.Clear();
            SelectedModelComparisonHistoryItem = null;
            ModelComparisonHistoryVisibility = Visibility.Collapsed;
            IsHistoricalModelComparisonSelection = false;
            IsModelComparisonExamplesExpanded = false;
            UpdateModelComparisonExampleDisclosure();
            ModelComparisonStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.Waiting");
            ModelComparisonDecisionText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.DecisionWaiting");
            ModelComparisonPromotionDecision = string.Empty;
            ModelComparisonSourceText = DefaultModelComparisonSourceText;
            ModelComparisonDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.DetailWaiting");
            ModelComparisonBenchmarkText = string.Empty;
            ModelComparisonActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingNext");
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
            currentComparisonReport = report;
            selectedComparisonExample = null;
            SetModelComparisonPreview(new(null, null, string.Empty));
            ModelComparisonExamples.Clear();
            IsHistoricalModelComparisonSelection = report?.HasComparison == true && isHistoricalSelection;
            IsModelComparisonEvidence = false;
            if (report?.HasComparison != true)
            {
                IsModelComparisonExamplesExpanded = false;
                UpdateModelComparisonExampleDisclosure();
                ModelComparisonStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.NotRun");
                ModelComparisonDecisionText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.CompareNeeded");
                ModelComparisonPromotionDecision = string.Empty;
                ModelComparisonBenchmarkText = string.Empty;
                ModelComparisonDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.NoReport");
                ModelComparisonActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.SetInstruction");
                ModelComparisonVisibility = Visibility.Collapsed;
                return;
            }

            ModelComparisonStatusText = report.SummaryText;
            ModelComparisonPromotionDecision = report.PromotionDecision;
            ModelComparisonBenchmarkText = report.BenchmarkText;
            ModelComparisonDecisionText = IsHistoricalModelComparisonSelection
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.HistoricalDecision")
                : string.IsNullOrWhiteSpace(report.RecommendationText)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExampleDecision")
                    : report.RecommendationText;
            ModelComparisonDetailText = string.IsNullOrWhiteSpace(report.SourcePath)
                ? report.DetailText
                : $"{report.DetailText} / {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(report.SourcePath) ?? report.SourcePath)}";
            ModelComparisonActionText = IsHistoricalModelComparisonSelection
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.HistoricalNext")
                : report.IsEngineComparison
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.EngineNext")
                : string.Equals(report.PromotionDecision, "hold", StringComparison.OrdinalIgnoreCase)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.HoldNext")
                    : (report.Examples?.Count ?? 0) > 0
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExamplesNext")
                        : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.NoExamplesNext");

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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.CandidateWaiting")
                : statusText;
            ModelCandidateDecisionDetailText = string.IsNullOrWhiteSpace(detailText)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.CandidateDetail")
                : detailText;
            SaveModelCandidateToolTip = string.IsNullOrWhiteSpace(saveToolTip)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingSaveEmpty")
                : saveToolTip;
            RejectModelCandidateToolTip = string.IsNullOrWhiteSpace(rejectToolTip)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingRejectEmpty")
                : rejectToolTip;
        }

        internal void ConfigureModelErrorReview(Func<bool, bool> beginReview, Func<WpfModelComparisonReviewExample, bool> returnReview, Func<bool> canNavigate)
        {
            beginModelErrorReview = beginReview;
            returnToModelComparison = returnReview;
            canNavigateModelReview = canNavigate;
            ModelErrorLabelReviewCommand = new RelayCommand(() => BeginModelErrorReview(false), CanBeginModelErrorReview);
            ModelErrorQualityReviewCommand = new RelayCommand(() => BeginModelErrorReview(true), CanBeginModelErrorReview);
            ReturnToModelComparisonCommand = new RelayCommand(ReturnToComparison, () => IsModelErrorReviewActive && canNavigateModelReview?.Invoke() == true);
        }

        private bool CanBeginModelErrorReview() => selectedComparisonExample != null && HasModelComparisonPreview && !IsModelErrorReviewActive && canNavigateModelReview?.Invoke() == true;

        private void BeginModelErrorReview(bool quality)
        {
            if (!CanBeginModelErrorReview()) return;
            WpfModelComparisonReviewExample example = selectedComparisonExample;
            WpfModelComparisonReviewReport report = currentComparisonReport;
            WpfModelComparisonHistoryItem history = SelectedModelComparisonHistoryItem;
            string source = ModelComparisonSourceText;
            bool historical = IsHistoricalModelComparisonSelection;
            if (beginModelErrorReview?.Invoke(quality) != true) return;
            returnComparisonReport = report;
            returnComparisonExample = example;
            returnComparisonHistory = history;
            returnComparisonSource = source;
            returnComparisonHistorical = historical;
            IsModelErrorReviewActive = true;
            RefreshModelErrorReviewCommands();
        }

        private void ReturnToComparison()
        {
            if (!IsModelErrorReviewActive || canNavigateModelReview?.Invoke() != true) return;
            WpfModelComparisonReviewExample example = returnComparisonExample;
            if (returnToModelComparison?.Invoke(example) != true) return;
            SetModelComparisonReview(returnComparisonReport, returnComparisonHistorical);
            SelectedModelComparisonHistoryItem = returnComparisonHistory;
            SetModelComparisonSourceText(returnComparisonSource);
            SetModelComparisonFocus(example, example.LocationText);
            SetModelComparisonPreview(ModelComparisonPreviewPresentationService.Build(example));
            IsModelErrorReviewActive = false;
            RefreshModelErrorReviewCommands();
        }

        internal void RefreshModelErrorReviewCommands()
        {
            foreach (ICommand command in new[] { ModelErrorLabelReviewCommand, ModelErrorQualityReviewCommand, ReturnToModelComparisonCommand }) if (command is RelayCommand relay) relay.RaiseCanExecuteChanged();
        }

        public void SetModelComparisonPreview(ModelComparisonPreviewPresentation presentation)
        {
            BaselineComparisonPreviewImage = presentation?.Baseline;
            CandidateComparisonPreviewImage = presentation?.Candidate;
            ComparisonPreviewErrorText = presentation?.Error;
            OnPropertyChanged(nameof(HasModelComparisonPreview));
            OnPropertyChanged(nameof(ComparisonPreviewSourceText));
            RefreshModelErrorReviewCommands();
        }

        public void SetModelComparisonFocus(WpfModelComparisonReviewExample example, string locationText)
        {
            if (example == null)
            {
                return;
            }

            IsModelComparisonEvidence = true;
            selectedComparisonExample = example;
            OnPropertyChanged(nameof(ComparisonPreviewSourceText));
            RefreshModelErrorReviewCommands();
            string location = string.IsNullOrWhiteSpace(locationText)
                ? example.LocationText
                : locationText.Trim();
            string action = example.ActionText ?? string.Empty;
            ModelComparisonStatusText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExampleOpened"), example.Title);
            ModelComparisonDetailText = string.IsNullOrWhiteSpace(location)
                ? action
                : string.IsNullOrWhiteSpace(action)
                    ? location
                    : $"{location} / {action}";
            ModelComparisonActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExampleNext");
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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.NoExamples")
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExamplesCount"), count);
            ModelComparisonExampleSummaryText = count <= 0
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExamplesEmptyHelp")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.WpfCandidateReviewPanelViewModel.ExamplesHelp");
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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.NoHistory")
                : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.HistoryCount"), count);
            ReviewHistorySummaryText = count <= 0
                ? "\uD655\uC815/\uC228\uAE40 \uC791\uC5C5\uC774 \uC788\uC73C\uBA74 \uC5EC\uAE30\uC5D0 \uC694\uC57D\uB429\uB2C8\uB2E4."
                : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.HistoryHelp");
            if (count <= 0)
            {
                IsReviewHistoryExpanded = false;
            }
        }

        public void UpdateCandidateAvailabilityPresentation(int visibleCandidateCount, int pendingCandidateCount, bool inspectionOnly = false, bool hasInferenceResult = false)
        {
            CandidateInteractionVisibility = pendingCandidateCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (inspectionOnly)
            {
                PanelModeTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultOnly.Panel.Title");
                PanelModeBadgeText = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultOnly.Panel.Badge");
                PanelModeScopeText = OpenVisionLab.OpenVisionLanguageService.T(pendingCandidateCount > 0
                    ? "WpfInference.ResultOnly.Panel.Detail"
                    : hasInferenceResult ? "WpfInference.ResultOnly.Panel.CompletedEmpty" : "WpfInference.ResultOnly.Panel.Empty");
                return;
            }

            if (visibleCandidateCount > 0)
            {
                PanelModeTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.CurrentSuggestions");
                PanelModeBadgeText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.CheckSuggestions");
                PanelModeScopeText = string.Empty;
                return;
            }

            if (pendingCandidateCount > 0)
            {
                PanelModeTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.BelowThreshold");
                PanelModeBadgeText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.CheckSuggestions");
                PanelModeScopeText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.LowerThreshold");
                return;
            }

            PanelModeTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfCandidateReviewPanelViewModel.NoSuggestions");
            PanelModeBadgeText = "\uC120\uD0DD \uB2E8\uACC4";
            PanelModeScopeText = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Assistance.Panel.Empty");
        }

        private static string FormatCandidateCount(int count)
            => string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Inline.Count"), Math.Max(0, count));
    }
}
