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
    public sealed class WpfModelBenchmarkViewModel : WpfObservableViewModel
    {
        public const int MaximumSelectedRunCount = 6;

        private readonly WpfModelBenchmarkCatalogService catalogService;
        private readonly string repositoryRoot;
        private readonly ICollectionView filteredRuns;
        private string searchText = string.Empty;
        private string selectedTaskFilter = "\uC804\uCCB4";
        private string baselineRunId = string.Empty;
        private string statusText = string.Empty;
        private string catalogTaskSummaryText = string.Empty;
        private string comparisonNoticeText = string.Empty;
        private string groundTruthReviewNoticeText = string.Empty;
        private string groundTruthErrorExampleStatusText = string.Empty;
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

        public WpfModelBenchmarkViewModel(
            WpfModelBenchmarkCatalogService catalogService = null,
            string repositoryRoot = "",
            string preferredSourcePath = "")
        {
            this.catalogService = catalogService ?? new WpfModelBenchmarkCatalogService();
            this.repositoryRoot = repositoryRoot ?? string.Empty;
            TaskFilters.Add("\uC804\uCCB4");
            filteredRuns = CollectionViewSource.GetDefaultView(CatalogRuns);
            filteredRuns.Filter = MatchesFilter;
            RefreshCommand = new RelayCommand(() => Refresh());
            ClearSelectionCommand = new RelayCommand(ClearSelection);
            SetBaselineCommand = new RelayCommand<WpfModelBenchmarkRunItemViewModel>(SetBaseline);
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
            ? "선택 오류 원본"
            : SelectedGroundTruthExample.ImageName;

        public string GroundTruthPreviewDetailText => SelectedGroundTruthExample == null
            ? string.Empty
            : $"{SelectedGroundTruthExample.ModelName} · {SelectedGroundTruthExample.ErrorTypeText} · {SelectedGroundTruthExample.ClassName}";

        public string GroundTruthPreviewStatusText => SelectedGroundTruthExample == null
            ? "오류 예시를 선택하세요."
            : HasSelectedGroundTruthPreview
                ? string.Empty
                : "원본 이미지 경로를 열 수 없습니다.";

        public ICollectionView FilteredRuns => filteredRuns;

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

        public void Refresh(string preferredSourcePath = "")
        {
            HashSet<string> selectedIds = CatalogRuns
                .Where(item => item.IsSelected)
                .Select(item => item.Run.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string previousBaselineId = baselineRunId;
            IReadOnlyList<WpfModelBenchmarkRun> runs = catalogService.Load(repositoryRoot);

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
                bool restored = RestorePreferredSelection(preferredSourcePath);
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

            StatusText = $"\uBE44\uAD50 \uC120\uD0DD\uC740 {MaximumSelectedRunCount}\uAC1C\uAE4C\uC9C0 \uAC00\uB2A5\uD569\uB2C8\uB2E4.";
            return false;
        }

        private void SetBaseline(WpfModelBenchmarkRunItemViewModel item)
        {
            if (item == null)
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
            if (suppressSelectionRefresh)
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
                ? $"\uC2E4\uD589 {CatalogRuns.Count}\uAC1C \u00B7 \uC120\uD0DD 0/{MaximumSelectedRunCount}"
                : $"\uC2E4\uD589 {CatalogRuns.Count}\uAC1C \u00B7 \uC120\uD0DD {selectedItems.Count}/{MaximumSelectedRunCount} \u00B7 \uAE30\uC900 {baseline?.DisplayName}";
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
                    string nmsIouText = review.PredictionNmsIouThreshold?.ToString("P0", CultureInfo.CurrentCulture) ?? "미기록";
                    notices.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}: 신뢰도 {1:P0} · NMS IoU {2} · 정답 매칭 IoU {3:P0} · TP {4} / FP {5} / FN {6}",
                        run.DisplayName,
                        review.Confidence ?? 0D,
                        nmsIouText,
                        review.IouThreshold ?? 0D,
                        review.TruePositiveCount,
                        review.FalsePositiveCount,
                        review.FalseNegativeCount));
                    foreach (WpfModelBenchmarkGroundTruthExample example in review.Examples)
                    {
                        GroundTruthExamples.Add(new WpfModelBenchmarkGroundTruthExampleViewModel(run.DisplayName, example));
                    }
                }
            }

            GroundTruthReviewNoticeText = notices.Count > 0
                ? string.Join("  |  ", notices)
                : "선택한 리포트에 클래스별/정답 대조 정보가 없습니다. 새 비교를 실행하면 추가됩니다.";
            GroundTruthErrorExampleStatusText = GroundTruthExamples.Count > 0
                ? $"리포트에 저장된 오류 예시 {GroundTruthExamples.Count}건"
                : "리포트에 저장된 미검출/오검출 예시가 없습니다.";
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
                ? $"\uC800\uC7A5\uB41C \uC608\uCE21/\uC815\uB2F5 \uB300\uC870 \uB9AC\uD3EC\uD2B8 {reviewRunCount}\uAC1C\uC758 \uC784\uACC4\uAC12\uBCC4 \uACB0\uACFC\uC785\uB2C8\uB2E4. \uC774 \uD654\uBA74\uC740 \uCD94\uB860\uC744 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4."
                : "\uC120\uD0DD\uD55C \uB9AC\uD3EC\uD2B8\uC5D0\uB294 v2 \uC784\uACC4\uAC12 \uB300\uC870\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uAC1D\uCCB4 \uD0D0\uC9C0 \uBE44\uAD50\uB97C \uB2E4\uC2DC \uC2E4\uD589\uD558\uBA74 \uC800\uC7A5\uB429\uB2C8\uB2E4.";
        }

        private void RebuildDashboard(IReadOnlyList<WpfModelBenchmarkRun> selectedRuns, WpfModelBenchmarkRun baseline)
        {
            DashboardQualityTaktPoints.Clear();
            DashboardOutcomeRows.Clear();

            if (baseline == null)
            {
                DashboardEvidenceText = "\uC120\uD0DD \uC5C6\uC74C";
                DashboardEvidenceDetailText = "\uBE44\uAD50\uD560 \uC2E4\uD589\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                DashboardQualityText = "\uC9C0\uD45C \uC5C6\uC74C";
                DashboardQualityDetailText = "\uC120\uD0DD\uB41C \uC2E4\uD589\uC758 \uB300\uD45C \uC9C0\uD45C\uAC00 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
                DashboardTaktText = "Takt \uC5C6\uC74C";
                DashboardTaktDetailText = "\uB3D9\uC77C \uC2E4\uD589 \uC870\uAC74\uC758 \uC2E4\uD589\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                DashboardDecisionText = "\uD310\uC815 \uC5C6\uC74C";
                DashboardDecisionDetailText = "\uC120\uD0DD \uD6C4 \uBCF4\uACE0\uC11C\uC758 \uD310\uC815\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.";
                DashboardQualityTaktStatusText = "\uB3D9\uC77C \uD3C9\uAC00 \uADFC\uAC70\uC640 \uD0C0\uC774\uBC0D \uC870\uAC74\uC758 \uC2E4\uD589 2\uAC1C \uC774\uC0C1\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
                DashboardOutcomeStatusText = "\uC815\uB2F5 \uB300\uC870 \uACB0\uACFC\uAC00 \uC788\uB294 \uD0D0\uC9C0 \uBCF4\uACE0\uC11C\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
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

                DashboardOutcomeRows.Add(new WpfModelBenchmarkDashboardOutcomeRowViewModel(
                    run.DisplayName,
                    review.TruePositiveCount,
                    review.FalsePositiveCount,
                    review.FalseNegativeCount));
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
                ? "\uAC01 \uC2E4\uD589 \uBCF4\uACE0\uC11C\uC758 \uC815\uB2F5 \uB300\uC870 \uC6D0\uC2DC \uC218\uCE58\uC785\uB2C8\uB2E4. \uC0C1\uD638 \uC6B0\uC5F4\uC740 \uBCF4\uACE0\uC11C \uC870\uAC74\uC774 \uC77C\uCE58\uD560 \uB54C\uB9CC \uD310\uB2E8\uD569\uB2C8\uB2E4."
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
                return "\uBE44\uAD50\uD560 \uC2E4\uD589\uC774 \uC120\uD0DD\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            }

            if (selectedRuns.Count == 1)
            {
                return "\uAE30\uC900 \uC2E4\uD589 1\uAC1C\uAC00 \uC120\uD0DD\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
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

    public sealed class WpfModelBenchmarkDashboardPointViewModel
    {
        public WpfModelBenchmarkDashboardPointViewModel(
            string runId,
            string displayName,
            WpfModelBenchmarkMetric metric,
            double taktMs,
            bool isBaseline)
        {
            RunId = runId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            MetricName = metric?.DisplayName ?? string.Empty;
            QualityValue = metric?.Value ?? 0D;
            IsPercentMetric = metric?.IsPercent == true;
            TaktMs = taktMs;
            IsBaseline = isBaseline;
        }

        public string RunId { get; }
        public string DisplayName { get; }
        public string MetricName { get; }
        public double QualityValue { get; }
        public bool IsPercentMetric { get; }
        public double TaktMs { get; }
        public bool IsBaseline { get; }
        public string QualityText => IsPercentMetric
            ? QualityValue.ToString("P1", CultureInfo.CurrentCulture)
            : QualityValue.ToString("0.##", CultureInfo.CurrentCulture);
        public string TaktText => TaktMs.ToString("0.00", CultureInfo.CurrentCulture) + " ms";
        public string LegendText => (IsBaseline ? "\uAE30\uC900 \u00B7 " : string.Empty) + DisplayName;
        public string ToolTipText => $"{DisplayName}\n{MetricName}: {QualityText}\nTakt: {TaktText}";
    }

    public sealed class WpfModelBenchmarkDashboardOutcomeRowViewModel
    {
        public WpfModelBenchmarkDashboardOutcomeRowViewModel(
            string displayName,
            int truePositiveCount,
            int falsePositiveCount,
            int falseNegativeCount)
        {
            DisplayName = displayName ?? string.Empty;
            TruePositiveCount = Math.Max(0, truePositiveCount);
            FalsePositiveCount = Math.Max(0, falsePositiveCount);
            FalseNegativeCount = Math.Max(0, falseNegativeCount);
            int total = Math.Max(1, TruePositiveCount + FalsePositiveCount + FalseNegativeCount);
            TruePositivePercent = TruePositiveCount * 100D / total;
            FalsePositivePercent = FalsePositiveCount * 100D / total;
            FalseNegativePercent = FalseNegativeCount * 100D / total;
        }

        public string DisplayName { get; }
        public int TruePositiveCount { get; }
        public int FalsePositiveCount { get; }
        public int FalseNegativeCount { get; }
        public double TruePositivePercent { get; }
        public double FalsePositivePercent { get; }
        public double FalseNegativePercent { get; }
        public string TruePositiveText => "TP " + TruePositiveCount.ToString(CultureInfo.CurrentCulture);
        public string FalsePositiveText => "FP " + FalsePositiveCount.ToString(CultureInfo.CurrentCulture);
        public string FalseNegativeText => "FN " + FalseNegativeCount.ToString(CultureInfo.CurrentCulture);
    }

    public sealed class WpfModelBenchmarkRunItemViewModel : WpfObservableViewModel
    {
        private readonly Func<WpfModelBenchmarkRunItemViewModel, bool, bool> selectionGuard;
        private readonly Action selectionChanged;
        private bool isSelected;
        private bool isBaseline;

        public WpfModelBenchmarkRunItemViewModel(
            WpfModelBenchmarkRun run,
            Func<WpfModelBenchmarkRunItemViewModel, bool, bool> selectionGuard,
            Action selectionChanged)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            this.selectionGuard = selectionGuard;
            this.selectionChanged = selectionChanged;
        }

        public WpfModelBenchmarkRun Run { get; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                {
                    return;
                }

                if (selectionGuard?.Invoke(this, value) == false)
                {
                    OnPropertyChanged(nameof(IsSelected));
                    return;
                }

                if (SetProperty(ref isSelected, value))
                {
                    selectionChanged?.Invoke();
                }
            }
        }

        public bool IsBaseline
        {
            get => isBaseline;
            private set => SetProperty(ref isBaseline, value);
        }

        public string DisplayName => Run.DisplayName;
        public string TaskText => Run.TaskText;
        public string RuntimeName => Run.RuntimeName;
        public string SourceTypeText => Run.SourceTypeText;
        public string DateText => Run.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        public string EvidenceText => $"{Run.Split} \u00B7 {Run.EvidenceCount}\uC7A5";
        public string SearchText => string.Join(" ", Run.DisplayName, Run.ModelName, Run.RuntimeName, Run.TaskText, Run.SourcePath);

        internal void SetSelected(bool selected)
        {
            SetProperty(ref isSelected, selected, nameof(IsSelected));
        }

        internal void SetBaseline(bool baseline)
        {
            IsBaseline = baseline;
        }
    }

    public sealed class WpfModelBenchmarkSelectedRunViewModel
    {
        public WpfModelBenchmarkSelectedRunViewModel(
            WpfModelBenchmarkRun run,
            WpfModelBenchmarkRun baseline,
            bool isBaseline)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            IsBaseline = isBaseline;
            BaselineText = isBaseline ? "\uAE30\uC900" : string.Empty;
            DeltaText = BuildDeltaText(run, baseline, isBaseline);
        }

        public WpfModelBenchmarkRun Run { get; }
        public bool IsBaseline { get; }
        public string BaselineText { get; }
        public string DisplayName => Run.DisplayName;
        public string ModelName => Run.ModelName;
        public string RuntimeName => Run.RuntimeName;
        public string TaskText => Run.TaskText;
        public string DecisionText => Run.DecisionText;
        public string QualityText => BuildQualityText(Run);
        public string TaktText => BuildTaktText(Run);
        public string EvidenceText => $"{Run.Split} \u00B7 {Run.EvidenceCount}\uC7A5";
        public string DeltaText { get; }
        public string SourceTypeText => Run.SourceTypeText;
        public string DateText => Run.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        public string WeightsPath => Run.WeightsPath;
        public string WeightsSha256 => Run.WeightsSha256;
        public string WeightsSha256Text => FormatSha256(Run.WeightsSha256);
        public string WeightsSha256SummaryText => "SHA " + WeightsSha256Text;
        public string EvaluationDataPath => Run.EvaluationDataPath;
        public string EvidenceFingerprintSha256 => Run.EvidenceFingerprintSha256;
        public string EvidenceFingerprintText => FormatSha256(Run.EvidenceFingerprintSha256);
        public string EvidenceFingerprintSummaryText => "SHA " + EvidenceFingerprintText;
        public string SourcePath => Run.SourcePath;
        public string ImageSizeText => Run.ImageSize > 0 ? Run.ImageSize.ToString(CultureInfo.CurrentCulture) : "-";
        public string BatchSizeText => Run.BatchSize > 0 ? Run.BatchSize.ToString(CultureInfo.CurrentCulture) : "-";
        public string ConfidenceText => Run.Confidence.HasValue ? Run.Confidence.Value.ToString("P0", CultureInfo.CurrentCulture) : "-";
        public string TimingProtocolText => string.IsNullOrWhiteSpace(Run.TimingSource)
            ? "\uCE21\uC815 \uC5C6\uC74C"
            : Run.TimingSource + (Run.TimingRepeatCount > 0 ? $" \u00B7 n={Run.TimingRepeatCount}" : string.Empty);

        private static string FormatSha256(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "\uAE30\uB85D \uC5C6\uC74C"
                : value.Substring(0, Math.Min(12, value.Length));
        }

        private static string BuildQualityText(WpfModelBenchmarkRun run)
        {
            foreach (string key in new[] { "map5095", "accuracy", "map50", "precision" })
            {
                WpfModelBenchmarkMetric metric = run.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                if (metric != null)
                {
                    return metric.DisplayName + " " + metric.FormatValue();
                }
            }

            return "-";
        }

        private static string BuildTaktText(WpfModelBenchmarkRun run)
        {
            if (!run.TaktMs.HasValue)
            {
                return "\uCE21\uC815 \uC5C6\uC74C";
            }

            string text = run.TaktMs.Value.ToString("0.00", CultureInfo.CurrentCulture) + " ms";
            if (run.TaktMinMs.HasValue && run.TaktMaxMs.HasValue && run.TimingRepeatCount > 1)
            {
                text += string.Format(
                    CultureInfo.CurrentCulture,
                    " ({0:0.00}-{1:0.00}, n={2})",
                    run.TaktMinMs.Value,
                    run.TaktMaxMs.Value,
                    run.TimingRepeatCount);
            }

            return text;
        }

        private static string BuildDeltaText(WpfModelBenchmarkRun run, WpfModelBenchmarkRun baseline, bool isBaseline)
        {
            if (isBaseline)
            {
                return "\uAE30\uC900 \uC2E4\uD589";
            }

            if (baseline == null || !string.Equals(run.TaskKey, baseline.TaskKey, StringComparison.OrdinalIgnoreCase))
            {
                return "\uC791\uC5C5 \uB2E4\uB984";
            }

            if (!WpfModelBenchmarkViewModel.AreQualityComparable(run, baseline))
            {
                return "\uD3C9\uAC00 \uC870\uAC74 \uB2E4\uB984";
            }

            var parts = new List<string>();
            foreach (string key in new[] { "map5095", "accuracy", "map50", "precision" })
            {
                WpfModelBenchmarkMetric metric = run.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                WpfModelBenchmarkMetric baselineMetric = baseline.Metrics.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                if (metric != null && baselineMetric != null && metric.SupportsDelta)
                {
                    parts.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} {1:+0.0;-0.0;0.0}%p",
                        metric.DisplayName,
                        (metric.Value - baselineMetric.Value) * 100D));
                    break;
                }
            }

            if (WpfModelBenchmarkViewModel.AreTimingComparable(run, baseline))
            {
                parts.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "Takt {0:+0.00;-0.00;0.00} ms",
                    run.TaktMs.Value - baseline.TaktMs.Value));
            }

            return parts.Count > 0 ? string.Join(" / ", parts) : "\uACF5\uD1B5 \uC9C0\uD45C \uC5C6\uC74C";
        }
    }

    public sealed class WpfModelBenchmarkMetricRowViewModel
    {
        public WpfModelBenchmarkMetricRowViewModel(
            string displayName,
            IReadOnlyList<WpfModelBenchmarkMetricCellViewModel> values)
        {
            DisplayName = displayName ?? string.Empty;
            Values = values ?? Array.Empty<WpfModelBenchmarkMetricCellViewModel>();
        }

        public string DisplayName { get; }
        public IReadOnlyList<WpfModelBenchmarkMetricCellViewModel> Values { get; }
    }

    public sealed class WpfModelBenchmarkMetricCellViewModel
    {
        public WpfModelBenchmarkMetricCellViewModel(string valueText, string deltaText, bool isBaseline)
        {
            ValueText = valueText ?? string.Empty;
            DeltaText = deltaText ?? string.Empty;
            IsBaseline = isBaseline;
        }

        public string ValueText { get; }
        public string DeltaText { get; }
        public bool IsBaseline { get; }
    }

    public sealed class WpfModelBenchmarkClassMetricRowViewModel
    {
        public WpfModelBenchmarkClassMetricRowViewModel(
            WpfModelBenchmarkRun run,
            int classId,
            WpfModelBenchmarkClassMetric metric,
            WpfModelBenchmarkGroundTruthClassReview review)
        {
            ModelName = run?.DisplayName ?? string.Empty;
            ClassName = !string.IsNullOrWhiteSpace(metric?.ClassName)
                ? metric.ClassName
                : !string.IsNullOrWhiteSpace(review?.ClassName) ? review.ClassName : classId.ToString(CultureInfo.CurrentCulture);
            InstanceCountText = metric?.InstanceCount?.ToString(CultureInfo.CurrentCulture)
                ?? review?.GroundTruthCount.ToString(CultureInfo.CurrentCulture)
                ?? "-";
            PrecisionText = FormatPercent(metric?.Precision);
            RecallText = FormatPercent(metric?.Recall);
            Map50Text = FormatPercent(metric?.Map50);
            Map5095Text = FormatPercent(metric?.Map5095);
            GroundTruthReviewText = review == null
                ? "-"
                : $"TP {review.TruePositiveCount} · FP {review.FalsePositiveCount} · FN {review.FalseNegativeCount}";
        }

        public string ModelName { get; }
        public string ClassName { get; }
        public string InstanceCountText { get; }
        public string PrecisionText { get; }
        public string RecallText { get; }
        public string Map50Text { get; }
        public string Map5095Text { get; }
        public string GroundTruthReviewText { get; }

        private static string FormatPercent(double? value)
        {
            return value.HasValue ? value.Value.ToString("P1", CultureInfo.CurrentCulture) : "-";
        }
    }

    public sealed class WpfModelBenchmarkThresholdReviewRowViewModel
    {
        public WpfModelBenchmarkThresholdReviewRowViewModel(
            WpfModelBenchmarkRun run,
            WpfModelBenchmarkGroundTruthReview review,
            WpfModelBenchmarkThresholdReview threshold)
        {
            ModelName = run?.DisplayName ?? string.Empty;
            ConfidenceText = threshold?.Confidence.ToString("P0", CultureInfo.CurrentCulture) ?? "-";
            GroundTruthCountText = threshold?.GroundTruthCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            PredictionCountText = threshold?.PredictionCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            TruePositiveCountText = threshold?.TruePositiveCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            FalsePositiveCountText = threshold?.FalsePositiveCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            FalseNegativeCountText = threshold?.FalseNegativeCount.ToString(CultureInfo.CurrentCulture) ?? "-";
            PrecisionText = FormatPercent(threshold?.Precision);
            RecallText = FormatPercent(threshold?.Recall);
            F1Text = FormatPercent(threshold?.F1);
            EvidenceText = string.IsNullOrWhiteSpace(review?.GeometryCoordinateSystem)
                ? "v" + Math.Max(1, review?.SchemaVersion ?? 1).ToString(CultureInfo.CurrentCulture)
                : "v" + Math.Max(1, review.SchemaVersion).ToString(CultureInfo.CurrentCulture) + " / " + review.GeometryCoordinateSystem;
        }

        public string ModelName { get; }
        public string ConfidenceText { get; }
        public string GroundTruthCountText { get; }
        public string PredictionCountText { get; }
        public string TruePositiveCountText { get; }
        public string FalsePositiveCountText { get; }
        public string FalseNegativeCountText { get; }
        public string PrecisionText { get; }
        public string RecallText { get; }
        public string F1Text { get; }
        public string EvidenceText { get; }

        private static string FormatPercent(double? value)
        {
            return value?.ToString("P1", CultureInfo.CurrentCulture) ?? "-";
        }
    }

    public sealed class WpfModelBenchmarkGroundTruthExampleViewModel
    {
        private ImageSource previewSource;
        private bool previewLoadAttempted;

        public WpfModelBenchmarkGroundTruthExampleViewModel(string modelName, WpfModelBenchmarkGroundTruthExample example)
        {
            ModelName = modelName ?? string.Empty;
            ImagePath = example?.ImagePath ?? string.Empty;
            ImageName = !string.IsNullOrWhiteSpace(example?.ImageName)
                ? example.ImageName
                : Path.GetFileName(ImagePath);
            ErrorTypeText = string.Equals(example?.ErrorType, "false-negative", StringComparison.OrdinalIgnoreCase)
                ? "미검출 (FN)"
                : string.Equals(example?.ErrorType, "false-positive", StringComparison.OrdinalIgnoreCase)
                    ? "오검출 (FP)"
                    : example?.ErrorType ?? string.Empty;
            ClassName = example?.ClassName ?? string.Empty;
            ConfidenceText = example?.Confidence?.ToString("P1", CultureInfo.CurrentCulture) ?? "-";
            BestIouText = example?.BestIou?.ToString("P1", CultureInfo.CurrentCulture) ?? "-";
            PredictionBox = example?.PredictionBox;
            GroundTruthBox = example?.GroundTruthBox;
        }

        public string ModelName { get; }
        public string ImagePath { get; }
        public string ImageName { get; }
        public string ErrorTypeText { get; }
        public string ClassName { get; }
        public string ConfidenceText { get; }
        public string BestIouText { get; }
        public WpfModelBenchmarkNormalizedBox PredictionBox { get; }
        public WpfModelBenchmarkNormalizedBox GroundTruthBox { get; }
        public bool HasGroundTruthBoxOverlay => IsRenderableBox(GroundTruthBox);
        public bool HasPredictionBoxOverlay => IsRenderableBox(PredictionBox);
        public bool HasOverlay => HasGroundTruthBoxOverlay || HasPredictionBoxOverlay;

        public ImageSource PreviewSource
        {
            get
            {
                if (!previewLoadAttempted)
                {
                    previewLoadAttempted = true;
                    previewSource = CreatePreviewSource(ImagePath, GroundTruthBox, PredictionBox);
                }

                return previewSource;
            }
        }

        private static ImageSource CreatePreviewSource(
            string imagePath,
            WpfModelBenchmarkNormalizedBox groundTruthBox,
            WpfModelBenchmarkNormalizedBox predictionBox)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.DecodePixelWidth = 640;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();
                if (!IsRenderableBox(groundTruthBox) && !IsRenderableBox(predictionBox))
                {
                    return bitmap;
                }

                var drawing = new DrawingGroup();
                drawing.Children.Add(new ImageDrawing(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)));
                DrawNormalizedBox(drawing, groundTruthBox, bitmap.PixelWidth, bitmap.PixelHeight, System.Windows.Media.Color.FromRgb(57, 217, 138), null);
                DrawNormalizedBox(drawing, predictionBox, bitmap.PixelWidth, bitmap.PixelHeight, System.Windows.Media.Color.FromRgb(50, 184, 255), DashStyles.Dash);
                drawing.Freeze();

                var preview = new DrawingImage(drawing);
                preview.Freeze();
                return preview;
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is NotSupportedException
                || ex is InvalidOperationException
                || ex is ArgumentException)
            {
                return null;
            }
        }

        private static bool IsRenderableBox(WpfModelBenchmarkNormalizedBox box)
        {
            return box != null
                && IsUnitInterval(box.XMin)
                && IsUnitInterval(box.YMin)
                && IsUnitInterval(box.XMax)
                && IsUnitInterval(box.YMax)
                && box.XMax > box.XMin
                && box.YMax > box.YMin;
        }

        private static bool IsUnitInterval(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0.0
                && value <= 1.0;
        }

        private static void DrawNormalizedBox(
            DrawingGroup drawing,
            WpfModelBenchmarkNormalizedBox box,
            double imageWidth,
            double imageHeight,
            System.Windows.Media.Color color,
            DashStyle dashStyle)
        {
            if (!IsRenderableBox(box))
            {
                return;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var pen = new System.Windows.Media.Pen(brush, 2.5);
            if (dashStyle != null)
            {
                pen.DashStyle = dashStyle;
            }

            pen.Freeze();
            double x = box.XMin * imageWidth;
            double y = box.YMin * imageHeight;
            double width = (box.XMax - box.XMin) * imageWidth;
            double height = (box.YMax - box.YMin) * imageHeight;
            drawing.Children.Add(new GeometryDrawing(null, pen, new RectangleGeometry(new Rect(x, y, width, height))));
        }
    }
}
