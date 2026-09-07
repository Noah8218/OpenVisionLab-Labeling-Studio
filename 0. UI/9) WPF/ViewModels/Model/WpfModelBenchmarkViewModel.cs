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
            SetBaselineCommand = new RelayCommand<WpfModelBenchmarkRunItemViewModel>(SetBaseline, _ => !disposed);
            Refresh(preferredSourcePath);
        }

        public ObservableCollection<WpfModelBenchmarkRunItemViewModel> CatalogRuns { get; } = new();

        public ObservableCollection<string> TaskFilters { get; } = new();

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

        public ICommand SetBaselineCommand { get; }

        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value ?? string.Empty))
                {
                    filteredRuns.Refresh();
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
            IReadOnlyList<WpfModelBenchmarkRun> runs = catalogService.Load(repositoryRoot, this.preferredSourcePath);

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
            RefreshComparison();
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
            CatalogTaskSummaryText = string.Join(
                " \u00B7 ",
                runs.GroupBy(run => run.TaskText, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(group => GetTaskDisplayOrder(group.Key))
                    .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(group => $"{group.Key} {group.Count()}"));
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

            selectedTaskFilter = TaskFilters.Contains(previous) ? previous : "\uC804\uCCB4";
            OnPropertyChanged(nameof(SelectedTaskFilter));
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
                        notices.Add($"{run.DisplayName}: \uC774\uBBF8\uC9C0 {review.ImageCount}\uC7A5 \u00B7 \uC815\uC0C1 \uC624\uAC80\uCD9C {review.FalsePositiveCount} \u00B7 \uC774\uC0C1 \uBBF8\uAC80\uCD9C {review.FalseNegativeCount} \u00B7 \uC800\uC7A5\uB41C \uD310\uC815 \uACB0\uACFC {review.Examples.Count}\uAC74");
                    }
                    else
                    {
                        string nmsIouText = review.PredictionNmsIouThreshold?.ToString("P0", CultureInfo.CurrentCulture) ?? "\uBBF8\uAE30\uB85D";
                        notices.Add(string.Format(
                            CultureInfo.CurrentCulture,
                            "{0}: \uC2E0\uB8B0\uB3C4 {1:P0} \u00B7 NMS IoU {2} \u00B7 \uC815\uB2F5 \uB9E4\uCE6D IoU {3:P0} \u00B7 TP {4} / FP {5} / FN {6}",
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
                    ? $"\uC624\uB958 \uC6B0\uC120\uC73C\uB85C \uC800\uC7A5\uB41C \uC774\uBBF8\uC9C0\uBCC4 \uD310\uC815 \uACB0\uACFC {GroundTruthExamples.Count}\uAC74\uC785\uB2C8\uB2E4. \uD45C\uC2DC\uB294 \uCD5C\uB300 500\uAC74\uC785\uB2C8\uB2E4."
                    : $"\uB9AC\uD3EC\uD2B8\uC5D0 \uC800\uC7A5\uB41C \uC624\uB958 \uC608\uC2DC {GroundTruthExamples.Count}\uAC74"
                : T("WpfModelBenchmark.GroundTruth.Errors.Empty");
            if (notices.Count == 0
                && selectedRuns.Count > 0
                && selectedRuns.All(run => string.Equals(run.TaskKey, "segmentation", StringComparison.OrdinalIgnoreCase)))
            {
                GroundTruthReviewNoticeText = "\uC138\uADF8\uBA58\uD14C\uC774\uC158 \uBCF4\uACE0\uC11C\ub294 \ud3f4\ub9ac\uace4/\ub9c8\uc2a4\ud06c \uc9c0\ud45c\ub97c \uc0ac\uc6a9\ud569\ub2c8\ub2e4. \ubc15\uc2a4 \uc815\ub2f5 \ub300\uc870 \uae30\ub85d\uc740 \uac1d\uccb4 \ud0d0\uc9c0 \ube44\uad50\uc5d0\uc11c\ub9cc \uc81c\uacf5\ub429\ub2c8\ub2e4.";
                GroundTruthErrorExampleStatusText = "\uC138\uADF8\uBA58\uD14C\uC774\uC158 \uBCF4\uACE0\uC11C\uc5d0\ub294 \ubc15\uc2a4 \ubbf8\uac80\ucd9c/\uc624\uac80\ucd9c \uc608\uc2dc\uac00 \uc5c6\uc2b5\ub2c8\ub2e4.";
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
                    anomaly ? "\uC815\uB2F5" : "TP",
                    anomaly ? "\uC624\uAC80\uCD9C" : "FP",
                    anomaly ? "\uBBF8\uAC80\uCD9C" : "FN"));
            }

            HasDashboardQualityTaktPoints = DashboardQualityTaktPoints.Count >= 2;
            HasDashboardOutcomeRows = DashboardOutcomeRows.Count > 0;
            bool qualityComparable = selectedRuns.Count > 1
                && selectedRuns.All(run => AreQualityComparable(run, baseline));
            bool timingComparable = selectedRuns.Count > 1
                && selectedRuns.All(run => AreTimingComparable(run, baseline));
            DashboardEvidenceText = selectedRuns.Count == 1
                ? "\uAE30\uC900 \uC2E4\uD589 1\uAC1C"
                : qualityComparable
                    ? selectedRuns.All(run => !string.IsNullOrWhiteSpace(run.EvidenceFingerprintSha256))
                        ? "\uB370\uC774\uD130 \uC9C0\uBB38 \uC77C\uCE58"
                        : "\uD3C9\uAC00 \uACBD\uB85C/\uBD84\uD560 \uC77C\uCE58"
                    : "\uBE44\uAD50 \uC81C\uC678";
            DashboardEvidenceDetailText = qualityComparable
                ? $"{baseline.Split} {baseline.EvidenceCount}\uC7A5 \uAE30\uC900 \u00B7 \uC120\uD0DD {selectedRuns.Count}\uAC1C"
                : ComparisonNoticeText;

            WpfModelBenchmarkDashboardPointViewModel candidatePoint = DashboardQualityTaktPoints
                .FirstOrDefault(point => !point.IsBaseline);
            if (candidatePoint != null && baselineMetric != null)
            {
                DashboardQualityText = $"{baselineMetric.DisplayName} {baselineMetric.FormatValue()} -> {candidatePoint.QualityText}";
                DashboardQualityDetailText = $"\uB3D9\uC77C \uD3C9\uAC00 \uADFC\uAC70\uC758 {DashboardQualityTaktPoints.Count}\uAC1C \uC2E4\uD589";
                DashboardTaktText = $"{baseline.TaktMs.Value:0.00} ms -> {candidatePoint.TaktMs:0.00} ms";
                DashboardTaktDetailText = $"{baseline.TimingSource} \u00B7 n={baseline.TimingRepeatCount}";
                DashboardQualityTaktStatusText = $"\uD3C9\uAC00 \uC9C0\uD45C: {baselineMetric.DisplayName} \u00B7 \uC624\uB978\uCABD\uC77C\uC218\uB85D \uB354 \uB192\uACE0, \uC67C\uCABD\uC77C\uC218\uB85D \uB354 \uBE60\uB985\uB2C8\uB2E4.";
            }
            else
            {
                DashboardQualityText = baselineMetric == null
                    ? "\uB300\uD45C \uC9C0\uD45C \uC5C6\uC74C"
                    : baselineMetric.DisplayName + " " + baselineMetric.FormatValue();
                DashboardQualityDetailText = "\uB3D9\uC77C \uD3C9\uAC00 \uADFC\uAC70\uC758 \uBE44\uAD50 \uC2E4\uD589\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                DashboardTaktText = baseline.TaktMs.HasValue
                    ? baseline.TaktMs.Value.ToString("0.00", CultureInfo.CurrentCulture) + " ms"
                    : "Takt \uBBF8\uCE21\uC815";
                DashboardTaktDetailText = "\uB3D9\uC77C \uD0C0\uC774\uBC0D \uC870\uAC74\uC758 \uC2E4\uD589\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
                DashboardQualityTaktStatusText = "\uC0B0\uC810\uB3C4 \uC81C\uC678: " + ComparisonNoticeText;
            }

            WpfModelBenchmarkRun decisionRun = selectedRuns
                .FirstOrDefault(run => !string.Equals(run.Id, baseline.Id, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(run.DecisionText))
                ?? baseline;
            DashboardDecisionText = string.IsNullOrWhiteSpace(decisionRun?.DecisionText)
                ? "\uD310\uC815 \uAE30\uB85D \uC5C6\uC74C"
                : decisionRun.DecisionText;
            DashboardDecisionDetailText = decisionRun == null
                ? string.Empty
                : string.Equals(decisionRun.Id, baseline.Id, StringComparison.OrdinalIgnoreCase)
                    ? "\uAE30\uC900 \uC2E4\uD589 \uD310\uC815"
                    : "\uBE44\uAD50 \uC2E4\uD589: " + decisionRun.DisplayName;
            DashboardOutcomeStatusText = DashboardOutcomeRows.Count > 0
                ? selectedRuns.All(run => string.Equals(run.TaskKey, "anomaly-classification", StringComparison.OrdinalIgnoreCase))
                    ? "\uAC01 \uC2E4\uD589\uC758 \uC815\uB2F5, \uC815\uC0C1 \uC624\uAC80\uCD9C, \uC774\uC0C1 \uBBF8\uAC80\uCD9C \uC218\uC785\uB2C8\uB2E4. \uC774\uBBF8\uC9C0\uBCC4 \uACB0\uACFC\uB294 \uD074\uB798\uC2A4/\uC624\uB958 \uD0ED\uC5D0\uC11C \uD655\uC778\uD569\uB2C8\uB2E4."
                    : "\uAC01 \uC2E4\uD589 \uBCF4\uACE0\uC11C\uC758 \uC815\uB2F5 \uB300\uC870 \uC6D0\uC2DC \uC218\uCE58\uC785\uB2C8\uB2E4. \uC0C1\uD638 \uC6B0\uC5F4\uC740 \uBCF4\uACE0\uC11C \uC870\uAC74\uC774 \uC77C\uCE58\uD560 \uB54C\uB9CC \uD310\uB2E8\uD569\uB2C8\uB2E4."
                : "\uC120\uD0DD\uB41C \uBCF4\uACE0\uC11C\uC5D0 \uC815\uB2F5 \uB300\uC870(TP/FP/FN) \uAE30\uB85D\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            if (DashboardOutcomeRows.Count == 0
                && selectedRuns.Count > 0
                && selectedRuns.All(run => string.Equals(run.TaskKey, "segmentation", StringComparison.OrdinalIgnoreCase)))
            {
                DashboardOutcomeStatusText = "\uC138\uADF8\uBA58\uD14C\uC774\uC158: \ud3f4\ub9ac\uace4/\ub9c8\uc2a4\ud06c \uc9c0\ud45c \uc0ac\uc6a9. \ubc15\uc2a4 TP/FP/FN \ub300\uc870\ub294 \uac1d\uccb4 \ud0d0\uc9c0 \ube44\uad50\uc5d0\uc11c\ub9cc \uc81c\uacf5\ub429\ub2c8\ub2e4.";
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
                return "\uAE30\uC900";
            }

            if (metric == null || baselineMetric == null || !metric.SupportsDelta)
            {
                return string.Empty;
            }

            if (!AreQualityComparable(run, baseline))
            {
                return "\uC870\uAC74 \uB2E4\uB984";
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
                return "\uC791\uC5C5 \uC885\uB958\uAC00 \uB2EC\uB77C \uC815\uD655\uB3C4\uC640 Takt \uC6B0\uC5F4\uC744 \uACC4\uC0B0\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            }

            if (selectedRuns.Any(run => !AreQualityComparable(run, baseline)))
            {
                return "\uD3C9\uAC00 \uB370\uC774\uD130 \uC9C0\uBB38, \uACBD\uB85C, \uBD84\uD560 \uB610\uB294 \uC774\uBBF8\uC9C0 \uC218\uAC00 \uB2EC\uB77C \uC815\uD655\uB3C4 \uC6B0\uC5F4\uC744 \uACC4\uC0B0\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            }

            string evidenceIdentityText = selectedRuns.All(run => !string.IsNullOrWhiteSpace(run.EvidenceFingerprintSha256))
                ? "\uB370\uC774\uD130 \uC9C0\uBB38 \uC77C\uCE58"
                : "\uB370\uC774\uD130 \uC9C0\uBB38 \uBBF8\uAE30\uB85D \u00B7 \uACBD\uB85C/\uBD84\uD560/\uC774\uBBF8\uC9C0 \uC218\uB85C\uB9CC \uD655\uC778";
            IReadOnlyList<WpfModelBenchmarkRun> taktRuns = selectedRuns.Where(run => run.TaktMs.HasValue).ToArray();
            if (taktRuns.Count == 0)
            {
                return "\uC815\uD655\uB3C4 \uBE44\uAD50 \uAC00\uB2A5 \u00B7 Takt \uCE21\uC815 \uC5C6\uC74C \u00B7 " + evidenceIdentityText;
            }

            if (taktRuns.Count != selectedRuns.Count || taktRuns.Any(run => !AreTimingComparable(run, baseline)))
            {
                return "\uC815\uD655\uB3C4 \uBE44\uAD50 \uAC00\uB2A5 \u00B7 Takt\uB294 \uBBF8\uCE21\uC815 \uB610\uB294 \uC2E4\uD589 \uC870\uAC74\uC774 \uB2EC\uB77C \uC6B0\uC5F4 \uACC4\uC0B0 \uC81C\uC678";
            }

            return "\uC815\uD655\uB3C4/Takt \uBE44\uAD50 \uAC00\uB2A5 \u00B7 " + evidenceIdentityText;
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
            RefreshComparison();
        }

        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] values)
            => string.Format(CultureInfo.InvariantCulture, T(key), values ?? Array.Empty<object>());

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
