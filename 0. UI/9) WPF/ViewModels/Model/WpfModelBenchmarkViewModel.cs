using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MvcVisionSystem
{
    public sealed class WpfModelBenchmarkViewModel : WpfObservableViewModel, IDisposable
    {
        public const int MaximumSelectedRunCount = 6;

        private readonly ModelBenchmarkCatalogService catalogService;
        private readonly string repositoryRoot;
        private readonly ICollectionView filteredRuns;
        private IReadOnlyList<string> unreadableCatalogPaths = Array.Empty<string>();
        private string preferredSourcePath = string.Empty;
        private string searchText = string.Empty;
        private string selectedTaskFilter = "\uC804\uCCB4";
        private string baselineRunId = string.Empty;
        private string statusText = string.Empty;
        private string catalogTaskSummaryText = string.Empty;
        private string comparisonNoticeText = string.Empty;
        private string groundTruthReviewNoticeText = string.Empty;
        private string groundTruthErrorExampleStatusText = string.Empty;
        private string groundTruthExamplesTitleText = string.Empty;
        private string thresholdReviewStatusText = string.Empty;
        private string dashboardEvidenceText = string.Empty;
        private string dashboardEvidenceDetailText = string.Empty;
        private string dashboardQualityText = string.Empty;
        private string dashboardQualityDetailText = string.Empty;
        private string dashboardTaktText = string.Empty;
        private string dashboardTaktDetailText = string.Empty;
        private string dashboardDecisionText = string.Empty;
        private string dashboardDecisionDetailText = string.Empty;
        private string dashboardQualityTaktStatusText = string.Empty;
        private string dashboardOutcomeStatusText = string.Empty;
        private WpfModelBenchmarkGroundTruthExampleViewModel selectedGroundTruthExample;
        private bool isCatalogEmpty;
        private bool isSelectionEmpty = true;
        private bool hasDashboardQualityTaktPoints;
        private bool hasDashboardOutcomeRows;
        private bool hasThresholdReviewRows;
        private int dashboardRevision;
        private bool suppressSelectionRefresh;
        private bool disposed;

        public WpfModelBenchmarkViewModel(
            ModelBenchmarkCatalogService catalogService = null,
            string repositoryRoot = "",
            string preferredSourcePath = "")
        {
            this.catalogService = catalogService ?? new ModelBenchmarkCatalogService();
            this.repositoryRoot = repositoryRoot ?? string.Empty;
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            TaskFilters.Add("\uC804\uCCB4");
            filteredRuns = CollectionViewSource.GetDefaultView(CatalogRuns);
            filteredRuns.Filter = MatchesFilter;
            RefreshCommand = new RelayCommand(() => Refresh(), () => !disposed);
            ClearSelectionCommand = new RelayCommand(ClearSelection, () => !disposed);
            ResetCatalogFiltersCommand = new RelayCommand(ResetCatalogFilters, () => !disposed && HasCatalogFilter);
            SetBaselineCommand = new RelayCommand<WpfModelBenchmarkRunItemViewModel>(SetBaseline, _ => !disposed);
            Refresh(preferredSourcePath);
        }

        public ObservableCollection<WpfModelBenchmarkRunItemViewModel> CatalogRuns { get; } = new();

        public ObservableCollection<string> TaskFilters { get; } = new();

        public ObservableCollection<ModelBenchmarkTaskFilterOption> TaskFilterOptions { get; } = new();

        public ObservableCollection<WpfModelBenchmarkSelectedRunViewModel> SelectedRuns { get; } = new();

        public ObservableCollection<WpfModelBenchmarkMetricRowViewModel> MetricRows { get; } = new();

        public ObservableCollection<WpfModelBenchmarkClassMetricRowViewModel> ClassMetricRows { get; } = new();

        public ObservableCollection<WpfModelBenchmarkGroundTruthExampleViewModel> GroundTruthExamples { get; } = new();

        public ObservableCollection<WpfModelBenchmarkThresholdReviewRowViewModel> ThresholdReviewRows { get; } = new();

        public ObservableCollection<WpfModelBenchmarkDashboardPointViewModel> DashboardQualityTaktPoints { get; } = new();

        public ObservableCollection<WpfModelBenchmarkDashboardOutcomeRowViewModel> DashboardOutcomeRows { get; } = new();

        public WpfModelBenchmarkGroundTruthExampleViewModel SelectedGroundTruthExample
        {
            get => selectedGroundTruthExample;
            set
            {
                if (SetProperty(ref selectedGroundTruthExample, value))
                {
                    OnPropertyChanged(nameof(HasSelectedGroundTruthPreview));
                    OnPropertyChanged(nameof(HasSelectedGroundTruthPreviewOverlay));
                    OnPropertyChanged(nameof(HasSelectedGroundTruthPreviewGroundTruthBox));
                    OnPropertyChanged(nameof(HasSelectedGroundTruthPreviewPredictionBox));
                    OnPropertyChanged(nameof(GroundTruthPreviewTitleText));
                    OnPropertyChanged(nameof(GroundTruthPreviewDetailText));
                    OnPropertyChanged(nameof(GroundTruthPreviewStatusText));
                }
            }
        }

        public bool HasSelectedGroundTruthPreview => SelectedGroundTruthExample?.PreviewSource != null;

        public bool HasSelectedGroundTruthPreviewOverlay => SelectedGroundTruthExample?.HasOverlay ?? false;

        public bool HasSelectedGroundTruthPreviewGroundTruthBox => SelectedGroundTruthExample?.HasGroundTruthBoxOverlay ?? false;

        public bool HasSelectedGroundTruthPreviewPredictionBox => SelectedGroundTruthExample?.HasPredictionBoxOverlay ?? false;

        public string GroundTruthPreviewTitleText => SelectedGroundTruthExample == null
            ? T("WpfModelBenchmark.GroundTruthPreview.ErrorTitle")
            : SelectedGroundTruthExample.ImageName;

        public string GroundTruthPreviewDetailText => SelectedGroundTruthExample == null
            ? string.Empty
            : string.Join(" \u00B7 ", new[]
            {
                SelectedGroundTruthExample.ModelName,
                SelectedGroundTruthExample.ErrorTypeText,
                SelectedGroundTruthExample.ClassName,
                SelectedGroundTruthExample.DetailText
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        public string GroundTruthPreviewStatusText => SelectedGroundTruthExample == null
            ? T("WpfModelBenchmark.GroundTruthPreview.SelectPrompt")
            : HasSelectedGroundTruthPreview
                ? string.Empty
                : T("WpfModelBenchmark.GroundTruthPreview.SourceMissing");

        public ICollectionView FilteredRuns => filteredRuns;

        public string SummaryTabText => T("WpfModelBenchmark.Tab.Summary");

        public string MetricsTabText => T("WpfModelBenchmark.Tab.Metrics");

        public string ClassErrorsTabText => T("WpfModelBenchmark.Tab.ClassErrors");

        public string RunConditionsTabText => T("WpfModelBenchmark.Tab.RunConditions");

        public string ThresholdTabText => T("WpfModelBenchmark.Tab.Threshold");

        public ICommand RefreshCommand { get; }

        public ICommand ClearSelectionCommand { get; }

        public ICommand ResetCatalogFiltersCommand { get; }

        public ICommand SetBaselineCommand { get; }

        public bool HasCatalogFilter => !string.IsNullOrEmpty(SearchText) || SelectedTaskFilter != "\uC804\uCCB4";

        public bool HasCatalogLoadFailures => unreadableCatalogPaths.Count > 0;

        public string CatalogLoadFailureText => Format("WpfModelBenchmark.Catalog.ReadFailed", unreadableCatalogPaths.Count);

        public string CatalogLoadFailureDetailText => string.Join(Environment.NewLine, unreadableCatalogPaths);

        public string CatalogLoadFailureHelpText => T("WpfModelBenchmark.Catalog.ReadFailed.Help");

        public string CatalogEmptyTitleText => IsCatalogEmpty
            ? T(HasCatalogLoadFailures ? "WpfModelBenchmark.Catalog.ReadFailed.Empty" : "WpfModelBenchmark.Catalog.Empty")
            : T("WpfModelBenchmark.Catalog.FilterEmpty");

        public string CatalogEmptyDetailText => IsCatalogEmpty
            ? T(HasCatalogLoadFailures ? "WpfModelBenchmark.Catalog.ReadFailed.Help" : "WpfModelBenchmark.Catalog.Empty.Help")
            : T("WpfModelBenchmark.Catalog.FilterEmpty.Help");

        public string ResetCatalogFiltersText => T("WpfModelBenchmark.Catalog.ResetFilters");

        public string SelectionEmptyTitleText => T("WpfModelBenchmark.Selection.Empty");

        public string SelectionEmptyDetailText => T(IsCatalogEmpty
            ? "WpfModelBenchmark.Selection.NoReports.Help"
            : "WpfModelBenchmark.Selection.Empty.Help");

        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value ?? string.Empty))
                {
                    filteredRuns.Refresh();
                    RefreshCatalogPresentation();
                }
            }
        }

        public string SelectedTaskFilter
        {
            get => selectedTaskFilter;
            set
            {
                if (SetProperty(ref selectedTaskFilter, string.IsNullOrWhiteSpace(value) ? "\uC804\uCCB4" : value))
                {
                    filteredRuns.Refresh();
                    RefreshCatalogPresentation();
                }
            }
        }

        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public string ComparisonNoticeText
        {
            get => comparisonNoticeText;
            private set => SetProperty(ref comparisonNoticeText, value ?? string.Empty);
        }

        public string CatalogTaskSummaryText
        {
            get => catalogTaskSummaryText;
            private set => SetProperty(ref catalogTaskSummaryText, value ?? string.Empty);
        }

        public string GroundTruthReviewNoticeText
        {
            get => groundTruthReviewNoticeText;
            private set => SetProperty(ref groundTruthReviewNoticeText, value ?? string.Empty);
        }

        public string GroundTruthErrorExampleStatusText
        {
            get => groundTruthErrorExampleStatusText;
            private set => SetProperty(ref groundTruthErrorExampleStatusText, value ?? string.Empty);
        }

        public string GroundTruthExamplesTitleText
        {
            get => groundTruthExamplesTitleText;
            private set => SetProperty(ref groundTruthExamplesTitleText, value ?? string.Empty);
        }

        public string ThresholdReviewStatusText
        {
            get => thresholdReviewStatusText;
            private set => SetProperty(ref thresholdReviewStatusText, value ?? string.Empty);
        }

        public string DashboardEvidenceText
        {
            get => dashboardEvidenceText;
            private set => SetProperty(ref dashboardEvidenceText, value ?? string.Empty);
        }

        public string DashboardEvidenceDetailText
        {
            get => dashboardEvidenceDetailText;
            private set => SetProperty(ref dashboardEvidenceDetailText, value ?? string.Empty);
        }

        public string DashboardQualityText
        {
            get => dashboardQualityText;
            private set => SetProperty(ref dashboardQualityText, value ?? string.Empty);
        }

        public string DashboardQualityDetailText
        {
            get => dashboardQualityDetailText;
            private set => SetProperty(ref dashboardQualityDetailText, value ?? string.Empty);
        }

        public string DashboardTaktText
        {
            get => dashboardTaktText;
            private set => SetProperty(ref dashboardTaktText, value ?? string.Empty);
        }

        public string DashboardTaktDetailText
        {
            get => dashboardTaktDetailText;
            private set => SetProperty(ref dashboardTaktDetailText, value ?? string.Empty);
        }

        public string DashboardDecisionText
        {
            get => dashboardDecisionText;
            private set => SetProperty(ref dashboardDecisionText, value ?? string.Empty);
        }

        public string DashboardDecisionDetailText
        {
            get => dashboardDecisionDetailText;
            private set => SetProperty(ref dashboardDecisionDetailText, value ?? string.Empty);
        }

        public string DashboardQualityTaktStatusText
        {
            get => dashboardQualityTaktStatusText;
            private set => SetProperty(ref dashboardQualityTaktStatusText, value ?? string.Empty);
        }

        public string DashboardOutcomeStatusText
        {
            get => dashboardOutcomeStatusText;
            private set => SetProperty(ref dashboardOutcomeStatusText, value ?? string.Empty);
        }

        public bool HasDashboardQualityTaktPoints
        {
            get => hasDashboardQualityTaktPoints;
            private set => SetProperty(ref hasDashboardQualityTaktPoints, value);
        }

        public bool HasDashboardOutcomeRows
        {
            get => hasDashboardOutcomeRows;
            private set => SetProperty(ref hasDashboardOutcomeRows, value);
        }

        public bool HasThresholdReviewRows
        {
            get => hasThresholdReviewRows;
            private set => SetProperty(ref hasThresholdReviewRows, value);
        }

        public int DashboardRevision
        {
            get => dashboardRevision;
            private set => SetProperty(ref dashboardRevision, value);
        }

        public bool IsCatalogEmpty
        {
            get => isCatalogEmpty;
            private set => SetProperty(ref isCatalogEmpty, value);
        }

        public bool IsSelectionEmpty
        {
            get => isSelectionEmpty;
            private set => SetProperty(ref isSelectionEmpty, value);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
            CommandManager.InvalidateRequerySuggested();
        }

        public void Refresh(string preferredSourcePath = "")
        {
            if (disposed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(preferredSourcePath))
            {
                this.preferredSourcePath = preferredSourcePath;
            }

            HashSet<string> selectedIds = CatalogRuns
                .Where(item => item.IsSelected)
                .Select(item => item.Run.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string previousBaselineId = baselineRunId;
            IReadOnlyList<WpfModelBenchmarkRun> runs = catalogService.Load(repositoryRoot, this.preferredSourcePath, out unreadableCatalogPaths);

            suppressSelectionRefresh = true;
            try
            {
                CatalogRuns.Clear();
                foreach (WpfModelBenchmarkRun run in runs)
                {
                    CatalogRuns.Add(new WpfModelBenchmarkRunItemViewModel(
                        run,
                        CanChangeSelection,
                        RefreshComparison));
                }

                RefreshTaskFilters(runs);
                bool restored = RestorePreferredSelection(this.preferredSourcePath);
                if (!restored)
                {
                    foreach (WpfModelBenchmarkRunItemViewModel item in CatalogRuns.Where(item => selectedIds.Contains(item.Run.Id)))
                    {
                        item.SetSelected(true);
                    }
                }

                if (!CatalogRuns.Any(item => item.IsSelected))
                {
                    SelectDefaultRuns();
                }

                baselineRunId = CatalogRuns.Any(item => item.IsSelected && string.Equals(item.Run.Id, previousBaselineId, StringComparison.OrdinalIgnoreCase))
                    ? previousBaselineId
                    : CatalogRuns.FirstOrDefault(item => item.IsSelected && string.Equals(item.Run.SourceRole, "baseline", StringComparison.OrdinalIgnoreCase))?.Run.Id
                        ?? CatalogRuns.FirstOrDefault(item => item.IsSelected)?.Run.Id
                        ?? string.Empty;
            }
            finally
            {
                suppressSelectionRefresh = false;
            }

            IsCatalogEmpty = CatalogRuns.Count == 0;
            filteredRuns.Refresh();
            RefreshCatalogPresentation();
            RefreshComparison();
        }

        private void ResetCatalogFilters()
        {
            if (disposed)
            {
                return;
            }

            SearchText = string.Empty;
            SelectedTaskFilter = "\uC804\uCCB4";
        }

        private void RefreshCatalogPresentation()
        {
            OnPropertyChanged(nameof(HasCatalogFilter));
            OnPropertyChanged(nameof(HasCatalogLoadFailures));
            OnPropertyChanged(nameof(CatalogLoadFailureText));
            OnPropertyChanged(nameof(CatalogLoadFailureDetailText));
            OnPropertyChanged(nameof(CatalogLoadFailureHelpText));
            OnPropertyChanged(nameof(CatalogEmptyTitleText));
            OnPropertyChanged(nameof(CatalogEmptyDetailText));
            OnPropertyChanged(nameof(ResetCatalogFiltersText));
            OnPropertyChanged(nameof(SelectionEmptyTitleText));
            OnPropertyChanged(nameof(SelectionEmptyDetailText));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool RestorePreferredSelection(string preferredSourcePath)
        {
            if (string.IsNullOrWhiteSpace(preferredSourcePath))
            {
                return false;
            }

            string preferred = NormalizePath(preferredSourcePath);
            List<WpfModelBenchmarkRunItemViewModel> matches = CatalogRuns
                .Where(item => string.Equals(NormalizePath(item.Run.SourcePath), preferred, StringComparison.OrdinalIgnoreCase))
                .Take(MaximumSelectedRunCount)
                .ToList();
            WpfModelBenchmarkRun anomalyRun = matches
                .Select(item => item.Run)
                .FirstOrDefault(run => string.Equals(run.TaskKey, "anomaly-classification", StringComparison.OrdinalIgnoreCase));
            if (anomalyRun != null)
            {
                matches.AddRange(CatalogRuns
                    .Where(item => string.Equals(item.Run.TaskKey, anomalyRun.TaskKey, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.Run.QualityComparisonKey, anomalyRun.QualityComparisonKey, StringComparison.OrdinalIgnoreCase)
                        && !matches.Contains(item))
                    .OrderByDescending(item => item.Run.CreatedAt)
                    .Take(MaximumSelectedRunCount - matches.Count));
            }
            foreach (WpfModelBenchmarkRunItemViewModel match in matches)
            {
                match.SetSelected(true);
            }

            if (matches.Count > 0)
            {
                baselineRunId = matches.FirstOrDefault(item => string.Equals(item.Run.SourceRole, "baseline", StringComparison.OrdinalIgnoreCase))?.Run.Id
                    ?? matches[0].Run.Id;
            }

            return matches.Count > 0;
        }

        private void SelectDefaultRuns()
        {
            IGrouping<string, WpfModelBenchmarkRunItemViewModel> pair = CatalogRuns
                .GroupBy(item => item.Run.SourcePath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() >= 2);
            IEnumerable<WpfModelBenchmarkRunItemViewModel> defaults = pair != null
                ? pair.Take(2)
                : CatalogRuns.Take(1);
            foreach (WpfModelBenchmarkRunItemViewModel item in defaults)
            {
                item.SetSelected(true);
            }
        }

        private void RefreshTaskFilters(IReadOnlyList<WpfModelBenchmarkRun> runs)
        {
            string previous = SelectedTaskFilter;
            TaskFilters.Clear();
            TaskFilters.Add("\uC804\uCCB4");
            foreach (string task in runs
                .Select(run => run.TaskText)
                .Where(task => !string.IsNullOrWhiteSpace(task))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(task => task, StringComparer.CurrentCultureIgnoreCase))
            {
                TaskFilters.Add(task);
            }

            for (int index = TaskFilterOptions.Count - 1; index >= 0; index--)
            {
                if (!TaskFilters.Contains(TaskFilterOptions[index].Key)) TaskFilterOptions.RemoveAt(index);
            }
            for (int index = 0; index < TaskFilters.Count; index++)
            {
                string task = TaskFilters[index];
                ModelBenchmarkTaskFilterOption option = TaskFilterOptions.FirstOrDefault(item => item.Key == task);
                if (option == null) TaskFilterOptions.Insert(index, new ModelBenchmarkTaskFilterOption(task));
                else if (TaskFilterOptions.IndexOf(option) != index) TaskFilterOptions.Move(TaskFilterOptions.IndexOf(option), index);
            }
            selectedTaskFilter = TaskFilters.Contains(previous) ? previous : "\uC804\uCCB4";
            OnPropertyChanged(nameof(SelectedTaskFilter));
            RefreshTaskFilterPresentation();
        }

        private void RefreshTaskFilterPresentation()
        {
            CatalogTaskSummaryText = string.Join(
                " \u00B7 ",
                CatalogRuns.GroupBy(item => item.Run.TaskText, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(group => GetTaskDisplayOrder(group.Key))
                    .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(group => $"{ModelBenchmarkValueFormatter.FormatTaskText(group.Key)} {group.Count()}"));
            foreach (ModelBenchmarkTaskFilterOption option in TaskFilterOptions)
            {
                option.RefreshLocalizedPresentation();
            }
        }

        private static int GetTaskDisplayOrder(string taskText)
        {
            return taskText switch
            {
                "\uAC1D\uCCB4 \uD0D0\uC9C0" => 0,
                "\uC138\uADF8\uBA58\uD14C\uC774\uC158" => 1,
                "\uC774\uC0C1 \uBD84\uB958" => 2,
                _ => 10
            };
        }

        private bool MatchesFilter(object item)
        {
            if (item is not WpfModelBenchmarkRunItemViewModel runItem)
            {
                return false;
            }

            bool taskMatches = string.Equals(SelectedTaskFilter, "\uC804\uCCB4", StringComparison.Ordinal)
                || string.Equals(runItem.Run.TaskText, SelectedTaskFilter, StringComparison.CurrentCultureIgnoreCase);
            if (!taskMatches)
            {
                return false;
            }

            string query = SearchText.Trim();
            return query.Length == 0
                || runItem.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        private bool CanChangeSelection(WpfModelBenchmarkRunItemViewModel item, bool selected)
        {
            if (!selected || item.IsSelected)
            {
                return true;
            }

            if (CatalogRuns.Count(candidate => candidate.IsSelected) < MaximumSelectedRunCount)
            {
                return true;
            }

            StatusText = Format("WpfModelBenchmark.Status.SelectionLimit", MaximumSelectedRunCount);
            return false;
        }

        private void SetBaseline(WpfModelBenchmarkRunItemViewModel item)
        {
            if (disposed || item == null)
            {
                return;
            }

            if (!item.IsSelected)
            {
                item.IsSelected = true;
                if (!item.IsSelected)
                {
                    return;
                }
            }

            baselineRunId = item.Run.Id;
            RefreshComparison();
        }

        private void ClearSelection()
        {
            if (disposed)
            {
                return;
            }

            suppressSelectionRefresh = true;
            try
            {
                foreach (WpfModelBenchmarkRunItemViewModel item in CatalogRuns)
                {
                    item.SetSelected(false);
                }

                baselineRunId = string.Empty;
            }
            finally
            {
                suppressSelectionRefresh = false;
            }

            RefreshComparison();
        }

        private void RefreshComparison()
        {
            if (disposed || suppressSelectionRefresh)
            {
                return;
            }

            List<WpfModelBenchmarkRunItemViewModel> selectedItems = CatalogRuns
                .Where(item => item.IsSelected)
                .OrderBy(item => string.Equals(item.Run.Id, baselineRunId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(item => item.Run.CreatedAt)
                .ToList();
            if (selectedItems.Count > 0
                && !selectedItems.Any(item => string.Equals(item.Run.Id, baselineRunId, StringComparison.OrdinalIgnoreCase)))
            {
                baselineRunId = selectedItems[0].Run.Id;
            }

            WpfModelBenchmarkRun baseline = selectedItems
                .FirstOrDefault(item => string.Equals(item.Run.Id, baselineRunId, StringComparison.OrdinalIgnoreCase))?.Run;
            foreach (WpfModelBenchmarkRunItemViewModel item in CatalogRuns)
            {
                item.SetBaseline(string.Equals(item.Run.Id, baselineRunId, StringComparison.OrdinalIgnoreCase));
            }

            SelectedRuns.Clear();
            foreach (WpfModelBenchmarkRunItemViewModel item in selectedItems)
            {
                SelectedRuns.Add(new WpfModelBenchmarkSelectedRunViewModel(
                    item.Run,
                    baseline,
                    string.Equals(item.Run.Id, baselineRunId, StringComparison.OrdinalIgnoreCase)));
            }

            WpfModelBenchmarkRun[] selectedRuns = selectedItems.Select(item => item.Run).ToArray();
            RebuildMetricRows(selectedRuns, baseline);
            RebuildClassDetails(selectedRuns);
            RebuildThresholdReviewRows(selectedRuns);
            IsSelectionEmpty = selectedItems.Count == 0;
            ComparisonNoticeText = BuildComparisonNotice(selectedRuns, baseline);
            RebuildDashboard(selectedRuns, baseline);
            StatusText = selectedItems.Count == 0
                ? Format("WpfModelBenchmark.Status.Empty", CatalogRuns.Count, MaximumSelectedRunCount)
                : Format("WpfModelBenchmark.Status.Selected", CatalogRuns.Count, selectedItems.Count, MaximumSelectedRunCount, baseline?.DisplayName);
        }

        private void RebuildMetricRows(IReadOnlyList<WpfModelBenchmarkRun> selectedRuns, WpfModelBenchmarkRun baseline)
        {
            MetricRows.Clear();
            IReadOnlyList<WpfModelBenchmarkMetric> metricDefinitions = selectedRuns
                .SelectMany(run => run.Metrics)
                .GroupBy(metric => metric.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(metric => metric.Order).First())
                .OrderBy(metric => metric.Order)
                .ThenBy(metric => metric.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            foreach (WpfModelBenchmarkMetric definition in metricDefinitions)
            {
                var cells = new List<WpfModelBenchmarkMetricCellViewModel>();
                foreach (WpfModelBenchmarkRun run in selectedRuns)
                {
                    WpfModelBenchmarkMetric metric = run.Metrics.FirstOrDefault(candidate =>
                        string.Equals(candidate.Key, definition.Key, StringComparison.OrdinalIgnoreCase));
                    WpfModelBenchmarkMetric baselineMetric = baseline?.Metrics.FirstOrDefault(candidate =>
                        string.Equals(candidate.Key, definition.Key, StringComparison.OrdinalIgnoreCase));
                    bool isBaseline = baseline != null && string.Equals(run.Id, baseline.Id, StringComparison.OrdinalIgnoreCase);
                    cells.Add(new WpfModelBenchmarkMetricCellViewModel(
                        metric?.FormatValue() ?? "-",
                        BuildMetricDeltaText(run, metric, baseline, baselineMetric, isBaseline),
                        isBaseline));
                }

                MetricRows.Add(new WpfModelBenchmarkMetricRowViewModel(definition.DisplayName, cells));
            }
        }

        private void RebuildClassDetails(IReadOnlyList<WpfModelBenchmarkRun> selectedRuns)
        {
            ClassMetricRows.Clear();
            SelectedGroundTruthExample = null;
            GroundTruthExamples.Clear();
            var notices = new List<string>();
            var exampleSources = new List<(string ModelName, WpfModelBenchmarkGroundTruthExample Example)>();
            bool anomalyOnly = selectedRuns.Count > 0
                && selectedRuns.All(run => string.Equals(run.TaskKey, "anomaly-classification", StringComparison.OrdinalIgnoreCase));
            GroundTruthExamplesTitleText = anomalyOnly
                ? T("WpfModelBenchmark.GroundTruth.Title.Decision")
                : T("WpfModelBenchmark.GroundTruth.Title.Errors");
            foreach (WpfModelBenchmarkRun run in selectedRuns)
            {
                IReadOnlyDictionary<int, WpfModelBenchmarkClassMetric> metricsByClass = run.ClassMetrics
                    .GroupBy(item => item.ClassId)
                    .ToDictionary(group => group.Key, group => group.First());
                IReadOnlyDictionary<int, WpfModelBenchmarkGroundTruthClassReview> reviewsByClass = (run.GroundTruthReview?.PerClass
                    ?? Array.Empty<WpfModelBenchmarkGroundTruthClassReview>())
                    .GroupBy(item => item.ClassId)
                    .ToDictionary(group => group.Key, group => group.First());
                foreach (int classId in metricsByClass.Keys.Concat(reviewsByClass.Keys).Distinct().OrderBy(value => value))
                {
                    metricsByClass.TryGetValue(classId, out WpfModelBenchmarkClassMetric metric);
                    reviewsByClass.TryGetValue(classId, out WpfModelBenchmarkGroundTruthClassReview review);
                    ClassMetricRows.Add(new WpfModelBenchmarkClassMetricRowViewModel(run, classId, metric, review));
                }

                if (run.GroundTruthReview != null)
                {
                    WpfModelBenchmarkGroundTruthReview review = run.GroundTruthReview;
                    if (string.Equals(run.TaskKey, "anomaly-classification", StringComparison.OrdinalIgnoreCase))
                    {
                        notices.Add(Format("WpfModelBenchmark.GroundTruth.AnomalyNotice", run.DisplayName, review.ImageCount, review.FalsePositiveCount, review.FalseNegativeCount, review.Examples.Count));
                    }
                    else
                    {
                        string nmsIouText = review.PredictionNmsIouThreshold?.ToString("P0", CultureInfo.CurrentCulture) ?? T("WpfModelBenchmark.GroundTruth.NmsUnrecorded");
                        notices.Add(string.Format(
                            CultureInfo.CurrentCulture,
                            T("WpfModelBenchmark.GroundTruth.DetectionNotice"),
                            run.DisplayName,
                            review.Confidence ?? 0D,
                            nmsIouText,
                            review.IouThreshold ?? 0D,
                            review.TruePositiveCount,
                            review.FalsePositiveCount,
                            review.FalseNegativeCount));
                    }
                    foreach (WpfModelBenchmarkGroundTruthExample example in review.Examples)
                    {
                        exampleSources.Add((run.DisplayName, example));
                    }
                }
            }

            foreach ((string modelName, WpfModelBenchmarkGroundTruthExample example) in exampleSources
                .OrderBy(item => string.Equals(item.Example.ErrorType, "correct", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            {
                GroundTruthExamples.Add(new WpfModelBenchmarkGroundTruthExampleViewModel(modelName, example));
            }

            GroundTruthReviewNoticeText = notices.Count > 0
                ? string.Join("  |  ", notices)
                : T("WpfModelBenchmark.GroundTruth.Notice.Empty");
            GroundTruthErrorExampleStatusText = GroundTruthExamples.Count > 0
                ? anomalyOnly
                    ? Format("WpfModelBenchmark.GroundTruth.AnomalyCount", GroundTruthExamples.Count)
                    : Format("WpfModelBenchmark.GroundTruth.ErrorCount", GroundTruthExamples.Count)
                : T("WpfModelBenchmark.GroundTruth.Errors.Empty");
            if (notices.Count == 0
                && selectedRuns.Count > 0
                && selectedRuns.All(run => string.Equals(run.TaskKey, "segmentation", StringComparison.OrdinalIgnoreCase)))
            {
                GroundTruthReviewNoticeText = T("WpfModelBenchmark.GroundTruth.SegmentationNotice");
                GroundTruthErrorExampleStatusText = T("WpfModelBenchmark.GroundTruth.SegmentationErrors");
            }
            SelectedGroundTruthExample = GroundTruthExamples.FirstOrDefault();
        }

        private void RebuildThresholdReviewRows(IReadOnlyList<WpfModelBenchmarkRun> selectedRuns)
        {
            ThresholdReviewRows.Clear();
            int reviewRunCount = 0;
            foreach (WpfModelBenchmarkRun run in selectedRuns)
            {
                WpfModelBenchmarkGroundTruthReview review = run.GroundTruthReview;
                if (review == null || review.SchemaVersion < 2 || review.ThresholdSweep.Count == 0)
                {
                    continue;
                }

                reviewRunCount++;
                foreach (WpfModelBenchmarkThresholdReview threshold in review.ThresholdSweep)
                {
                    ThresholdReviewRows.Add(new WpfModelBenchmarkThresholdReviewRowViewModel(run, review, threshold));
                }
            }

            HasThresholdReviewRows = ThresholdReviewRows.Count > 0;
            ThresholdReviewStatusText = HasThresholdReviewRows
                ? Format("WpfModelBenchmark.Threshold.Status.Ready", reviewRunCount)
                : T("WpfModelBenchmark.Threshold.Status.Empty");
        }

        private void RebuildDashboard(IReadOnlyList<WpfModelBenchmarkRun> selectedRuns, WpfModelBenchmarkRun baseline)
        {
            DashboardQualityTaktPoints.Clear();
            DashboardOutcomeRows.Clear();

            if (baseline == null)
            {
                DashboardEvidenceText = T("WpfModelBenchmark.Dashboard.Evidence.None");
                DashboardEvidenceDetailText = T("WpfModelBenchmark.Dashboard.Evidence.Prompt");
                DashboardQualityText = T("WpfModelBenchmark.Dashboard.Quality.None");
                DashboardQualityDetailText = T("WpfModelBenchmark.Dashboard.Quality.Prompt");
                DashboardTaktText = T("WpfModelBenchmark.Dashboard.Takt.None");
                DashboardTaktDetailText = T("WpfModelBenchmark.Dashboard.Takt.Prompt");
                DashboardDecisionText = T("WpfModelBenchmark.Dashboard.Decision.None");
                DashboardDecisionDetailText = T("WpfModelBenchmark.Dashboard.Decision.Prompt");
                DashboardQualityTaktStatusText = T("WpfModelBenchmark.Dashboard.QualityTakt.Prompt");
                DashboardOutcomeStatusText = T("WpfModelBenchmark.Dashboard.Outcome.Prompt");
                HasDashboardQualityTaktPoints = false;
                HasDashboardOutcomeRows = false;
                DashboardRevision = DashboardRevision == int.MaxValue ? 0 : DashboardRevision + 1;
                return;
            }

            WpfModelBenchmarkMetric baselineMetric = FindPrimaryMetric(baseline);
            if (baselineMetric != null && baseline.TaktMs.HasValue)
            {
                foreach (WpfModelBenchmarkRun run in selectedRuns)
                {
                    WpfModelBenchmarkMetric metric = string.Equals(run.Id, baseline.Id, StringComparison.OrdinalIgnoreCase)
                        ? baselineMetric
                        : FindMetric(run, baselineMetric.Key);
                    bool isBaseline = string.Equals(run.Id, baseline.Id, StringComparison.OrdinalIgnoreCase);
                    bool isComparable = isBaseline
                        || (AreQualityComparable(run, baseline) && AreTimingComparable(run, baseline));
                    if (metric == null || !run.TaktMs.HasValue || !isComparable)
                    {
                        continue;
                    }

                    DashboardQualityTaktPoints.Add(new WpfModelBenchmarkDashboardPointViewModel(
                        run.Id,
                        run.DisplayName,
                        metric,
                        run.TaktMs.Value,
                        isBaseline));
                }

                if (DashboardQualityTaktPoints.Count < 2)
                {
                    DashboardQualityTaktPoints.Clear();
                }
            }

            foreach (WpfModelBenchmarkRun run in selectedRuns)
            {
                WpfModelBenchmarkGroundTruthReview review = run.GroundTruthReview;
                if (review == null)
                {
                    continue;
                }

                bool anomaly = string.Equals(run.TaskKey, "anomaly-classification", StringComparison.OrdinalIgnoreCase);
                DashboardOutcomeRows.Add(new WpfModelBenchmarkDashboardOutcomeRowViewModel(
                    run.DisplayName,
                    review.TruePositiveCount,
                    review.FalsePositiveCount,
                    review.FalseNegativeCount,
                    anomaly ? T("WpfModelBenchmark.Error.Correct") : "TP",
                    anomaly ? T("WpfModelBenchmark.Outcome.FalsePositive") : "FP",
                    anomaly ? T("WpfModelBenchmark.Outcome.FalseNegative") : "FN"));
            }

            HasDashboardQualityTaktPoints = DashboardQualityTaktPoints.Count >= 2;
            HasDashboardOutcomeRows = DashboardOutcomeRows.Count > 0;
            bool qualityComparable = selectedRuns.Count > 1
                && selectedRuns.All(run => AreQualityComparable(run, baseline));
            bool timingComparable = selectedRuns.Count > 1
                && selectedRuns.All(run => AreTimingComparable(run, baseline));
            DashboardEvidenceText = selectedRuns.Count == 1
                ? T("WpfModelBenchmark.Dashboard.Evidence.One")
                : qualityComparable
                    ? selectedRuns.All(run => !string.IsNullOrWhiteSpace(run.EvidenceFingerprintSha256))
                        ? T("WpfModelBenchmark.Dashboard.Evidence.Fingerprint")
                        : T("WpfModelBenchmark.Dashboard.Evidence.PathSplit")
                    : T("WpfModelBenchmark.Dashboard.Evidence.Excluded");
            DashboardEvidenceDetailText = qualityComparable
                ? Format("WpfModelBenchmark.Dashboard.Evidence.Detail", baseline.Split, baseline.EvidenceCount, selectedRuns.Count)
                : ComparisonNoticeText;

            WpfModelBenchmarkDashboardPointViewModel candidatePoint = DashboardQualityTaktPoints
                .FirstOrDefault(point => !point.IsBaseline);
            if (candidatePoint != null && baselineMetric != null)
            {
                DashboardQualityText = $"{baselineMetric.DisplayName} {baselineMetric.FormatValue()} -> {candidatePoint.QualityText}";
                DashboardQualityDetailText = Format("WpfModelBenchmark.Dashboard.Quality.ComparableCount", DashboardQualityTaktPoints.Count);
                DashboardTaktText = $"{baseline.TaktMs.Value:0.00} ms -> {candidatePoint.TaktMs:0.00} ms";
                DashboardTaktDetailText = $"{baseline.TimingSource} \u00B7 n={baseline.TimingRepeatCount}";
                DashboardQualityTaktStatusText = Format("WpfModelBenchmark.Dashboard.QualityTakt.Metric", baselineMetric.DisplayName);
            }
            else
            {
                DashboardQualityText = baselineMetric == null
                    ? T("WpfModelBenchmark.Dashboard.Quality.Unavailable")
                    : baselineMetric.DisplayName + " " + baselineMetric.FormatValue();
                DashboardQualityDetailText = T("WpfModelBenchmark.Dashboard.Quality.SelectComparable");
                DashboardTaktText = baseline.TaktMs.HasValue
                    ? baseline.TaktMs.Value.ToString("0.00", CultureInfo.CurrentCulture) + " ms"
                    : T("WpfModelBenchmark.Dashboard.Takt.Unmeasured");
                DashboardTaktDetailText = T("WpfModelBenchmark.Dashboard.Takt.SelectComparable");
                DashboardQualityTaktStatusText = Format("WpfModelBenchmark.Dashboard.QualityTakt.Excluded", ComparisonNoticeText);
            }

            WpfModelBenchmarkRun decisionRun = selectedRuns
                .FirstOrDefault(run => !string.Equals(run.Id, baseline.Id, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(run.DecisionText))
                ?? baseline;
            DashboardDecisionText = string.IsNullOrWhiteSpace(decisionRun?.DecisionText)
                ? T("WpfModelBenchmark.Dashboard.Decision.Unrecorded")
                : ModelBenchmarkValueFormatter.FormatDecisionText(decisionRun.DecisionText);
            DashboardDecisionDetailText = decisionRun == null
                ? string.Empty
                : string.Equals(decisionRun.Id, baseline.Id, StringComparison.OrdinalIgnoreCase)
                    ? T("WpfModelBenchmark.Dashboard.Decision.Baseline")
                    : Format("WpfModelBenchmark.Dashboard.Decision.Candidate", decisionRun.DisplayName);
            DashboardOutcomeStatusText = DashboardOutcomeRows.Count > 0
                ? selectedRuns.All(run => string.Equals(run.TaskKey, "anomaly-classification", StringComparison.OrdinalIgnoreCase))
                    ? T("WpfModelBenchmark.Dashboard.Outcome.Anomaly")
                    : T("WpfModelBenchmark.Dashboard.Outcome.Detection")
                : T("WpfModelBenchmark.Dashboard.Outcome.Unrecorded");
            if (DashboardOutcomeRows.Count == 0
                && selectedRuns.Count > 0
                && selectedRuns.All(run => string.Equals(run.TaskKey, "segmentation", StringComparison.OrdinalIgnoreCase)))
            {
                DashboardOutcomeStatusText = T("WpfModelBenchmark.Dashboard.Outcome.Segmentation");
            }
            DashboardRevision = DashboardRevision == int.MaxValue ? 0 : DashboardRevision + 1;
        }

        private static WpfModelBenchmarkMetric FindPrimaryMetric(WpfModelBenchmarkRun run)
        {
            foreach (string key in new[] { "map5095", "accuracy", "map50", "precision" })
            {
                WpfModelBenchmarkMetric metric = FindMetric(run, key);
                if (metric != null)
                {
                    return metric;
                }
            }

            return null;
        }

        private static WpfModelBenchmarkMetric FindMetric(WpfModelBenchmarkRun run, string key)
        {
            return run?.Metrics.FirstOrDefault(metric =>
                string.Equals(metric.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildMetricDeltaText(
            WpfModelBenchmarkRun run,
            WpfModelBenchmarkMetric metric,
            WpfModelBenchmarkRun baseline,
            WpfModelBenchmarkMetric baselineMetric,
            bool isBaseline)
        {
            if (isBaseline)
            {
                return T("WpfModelBenchmark.Run.Baseline");
            }

            if (metric == null || baselineMetric == null || !metric.SupportsDelta)
            {
                return string.Empty;
            }

            if (!AreQualityComparable(run, baseline))
            {
                return T("WpfModelBenchmark.Run.ConditionMismatch");
            }

            double delta = metric.Value - baselineMetric.Value;
            return metric.IsPercent
                ? string.Format(CultureInfo.CurrentCulture, "{0:+0.0;-0.0;0.0}%p", delta * 100D)
                : string.Format(CultureInfo.CurrentCulture, "{0:+0.##;-0.##;0}", delta);
        }

        internal static bool AreQualityComparable(WpfModelBenchmarkRun first, WpfModelBenchmarkRun second)
        {
            return first != null
                && second != null
                && first.EvidenceCount > 0
                && second.EvidenceCount > 0
                && (!string.IsNullOrWhiteSpace(first.EvidenceFingerprintSha256)
                    || !string.IsNullOrWhiteSpace(first.EvaluationDataPath))
                && (!string.IsNullOrWhiteSpace(second.EvidenceFingerprintSha256)
                    || !string.IsNullOrWhiteSpace(second.EvaluationDataPath))
                && string.Equals(first.QualityComparisonKey, second.QualityComparisonKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool AreTimingComparable(WpfModelBenchmarkRun first, WpfModelBenchmarkRun second)
        {
            return first?.TaktMs.HasValue == true
                && second?.TaktMs.HasValue == true
                && first.ImageSize > 0
                && second.ImageSize > 0
                && first.BatchSize > 0
                && second.BatchSize > 0
                && !string.IsNullOrWhiteSpace(first.TimingSource)
                && !string.IsNullOrWhiteSpace(second.TimingSource)
                && string.Equals(first.TimingComparisonKey, second.TimingComparisonKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildComparisonNotice(IReadOnlyList<WpfModelBenchmarkRun> selectedRuns, WpfModelBenchmarkRun baseline)
        {
            if (selectedRuns.Count == 0)
            {
                return T("WpfModelBenchmark.Comparison.None");
            }

            if (selectedRuns.Count == 1)
            {
                return T("WpfModelBenchmark.Comparison.One");
            }

            if (selectedRuns.Any(run => !string.Equals(run.TaskKey, baseline?.TaskKey, StringComparison.OrdinalIgnoreCase)))
            {
                return T("WpfModelBenchmark.Comparison.TaskMismatch");
            }

            if (selectedRuns.Any(run => !AreQualityComparable(run, baseline)))
            {
                return T("WpfModelBenchmark.Comparison.EvidenceMismatch");
            }

            string evidenceIdentityText = selectedRuns.All(run => !string.IsNullOrWhiteSpace(run.EvidenceFingerprintSha256))
                ? T("WpfModelBenchmark.Dashboard.Evidence.Fingerprint")
                : T("WpfModelBenchmark.Comparison.LegacyEvidence");
            IReadOnlyList<WpfModelBenchmarkRun> taktRuns = selectedRuns.Where(run => run.TaktMs.HasValue).ToArray();
            if (taktRuns.Count == 0)
            {
                return Format("WpfModelBenchmark.Comparison.NoTakt", evidenceIdentityText);
            }

            if (taktRuns.Count != selectedRuns.Count || taktRuns.Any(run => !AreTimingComparable(run, baseline)))
            {
                return T("WpfModelBenchmark.Comparison.TaktMismatch");
            }

            return Format("WpfModelBenchmark.Comparison.Comparable", evidenceIdentityText);
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(SummaryTabText));
            OnPropertyChanged(nameof(MetricsTabText));
            OnPropertyChanged(nameof(ClassErrorsTabText));
            OnPropertyChanged(nameof(RunConditionsTabText));
            OnPropertyChanged(nameof(ThresholdTabText));
            OnPropertyChanged(nameof(GroundTruthPreviewTitleText));
            OnPropertyChanged(nameof(GroundTruthPreviewStatusText));
            foreach (WpfModelBenchmarkRunItemViewModel item in CatalogRuns)
            {
                item.RefreshLocalizedPresentation();
            }
            RefreshTaskFilterPresentation();
            RefreshCatalogPresentation();
            int selectedExampleIndex = GroundTruthExamples.IndexOf(SelectedGroundTruthExample);
            RefreshComparison();
            if (selectedExampleIndex >= 0 && selectedExampleIndex < GroundTruthExamples.Count)
            {
                SelectedGroundTruthExample = GroundTruthExamples[selectedExampleIndex];
            }
        }

        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] values)
            => string.Format(CultureInfo.CurrentCulture, T(key), values ?? Array.Empty<object>());

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
