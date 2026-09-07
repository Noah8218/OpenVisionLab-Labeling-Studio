using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    internal static class DatasetHealthTextFormatter
    {
        public static string Translate(string key)
            => OpenVisionLanguageService.T(key);

        public static string Format(string key, params object[] arguments)
            => string.Format(
                CultureInfo.InvariantCulture,
                Translate(key),
                arguments ?? Array.Empty<object>());
    }

    public sealed class WpfDatasetHealthViewModel : WpfObservableViewModel, IDisposable
    {
        public const string AllVisualQaSplits = "전체";
        public const string AllVisualQaClasses = "전체";
        private static readonly Action NoOpCommand = () => { };
        private static readonly string[] VisualQaSplitOrder = { "train", "valid", "test" };
        private readonly DatasetVisualQaService visualQaService = new DatasetVisualQaService();
        private readonly List<WpfDatasetVisualQaItem> visualQaCatalogItems = new List<WpfDatasetVisualQaItem>();
        private bool disposed;
        private LabelingProjectData data;
        private Action<string> openVisualQaImage = _ => { };
        private string datasetName = "데이터셋 미선택";
        private string purposeText = "목적 미확인";
        private string outputRootText = "저장 폴더를 확인하세요.";
        private string statusText = "저장 데이터 분석 대기";
        private string statusDetailText = "분할, 라벨 품질, 클래스 분포를 읽기 전용으로 확인합니다.";
        private string generatedAtText = string.Empty;
        private string anomalyDetailText = string.Empty;
        private bool isAnomalyDataset;
        private bool hasIssues;
        private bool hasClasses;
        private bool hasVisualQaItems;
        private bool isVisualQaLoaded;
        private bool isRefreshingVisualQa;
        private bool showOnlyVisualQaProblems;
        private IReadOnlyList<string> visualQaSplitFilters = new[] { AllVisualQaSplits };
        private string selectedVisualQaSplitFilter = AllVisualQaSplits;
        private IReadOnlyList<WpfDatasetVisualQaClassFilterItem> visualQaClassFilters =
            new[] { new WpfDatasetVisualQaClassFilterItem(null, string.Empty) };
        private WpfDatasetVisualQaClassFilterItem selectedVisualQaClassFilter =
            new WpfDatasetVisualQaClassFilterItem(null, string.Empty);
        private string visualQaCatalogStatusText = "시각 QA 탭을 열면 저장 라벨 문제와 표본을 읽습니다.";
        private string visualQaStatusText = "저장된 이미지를 점검하면 문제 후보와 표본이 표시됩니다.";
        private WpfDatasetVisualQaItem selectedVisualQaItem;
        private ICommand refreshCommand = new RelayCommand(NoOpCommand);
        private ICommand openSelectedVisualQaImageCommand = new RelayCommand(NoOpCommand);

        public WpfDatasetHealthViewModel(LabelingProjectData data = null)
        {
            this.data = data;
            RefreshCommand = new RelayCommand(() => Refresh(this.data));
            OpenSelectedVisualQaImageCommand = new RelayCommand(OpenSelectedVisualQaImage);
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            Refresh(data);
        }

        public ObservableCollection<WpfDatasetHealthMetricItem> Metrics { get; } = new ObservableCollection<WpfDatasetHealthMetricItem>();

        public ObservableCollection<WpfDatasetHealthSplitRow> SplitRows { get; } = new ObservableCollection<WpfDatasetHealthSplitRow>();

        public ObservableCollection<WpfDatasetHealthClassRow> ClassRows { get; } = new ObservableCollection<WpfDatasetHealthClassRow>();

        public ObservableCollection<WpfDatasetHealthIssueItem> Issues { get; } = new ObservableCollection<WpfDatasetHealthIssueItem>();

        public ObservableCollection<WpfDatasetVisualQaItem> VisualQaItems { get; } = new ObservableCollection<WpfDatasetVisualQaItem>();

        public IReadOnlyList<string> VisualQaSplitFilters
        {
            get => visualQaSplitFilters;
            private set => SetProperty(ref visualQaSplitFilters, value ?? Array.Empty<string>());
        }

        public IReadOnlyList<WpfDatasetVisualQaClassFilterItem> VisualQaClassFilters
        {
            get => visualQaClassFilters;
            private set => SetProperty(
                ref visualQaClassFilters,
                value ?? Array.Empty<WpfDatasetVisualQaClassFilterItem>());
        }

        public string DatasetName
        {
            get => datasetName;
            private set => SetProperty(ref datasetName, value ?? string.Empty);
        }

        public string PurposeText
        {
            get => purposeText;
            private set => SetProperty(ref purposeText, value ?? string.Empty);
        }

        public string OutputRootText
        {
            get => outputRootText;
            private set => SetProperty(ref outputRootText, value ?? string.Empty);
        }

        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public string StatusDetailText
        {
            get => statusDetailText;
            private set => SetProperty(ref statusDetailText, value ?? string.Empty);
        }

        public string GeneratedAtText
        {
            get => generatedAtText;
            private set => SetProperty(ref generatedAtText, value ?? string.Empty);
        }

        public string DataScopeText => T("WpfDatasetHealth.Scope");

        public string EvidenceBoundaryText => T("WpfDatasetHealth.EvidenceBoundary");

        public string AnomalyDetailText
        {
            get => anomalyDetailText;
            private set => SetProperty(ref anomalyDetailText, value ?? string.Empty);
        }

        public bool IsAnomalyDataset
        {
            get => isAnomalyDataset;
            private set
            {
                if (SetProperty(ref isAnomalyDataset, value))
                {
                    OnPropertyChanged(nameof(IsYoloDataset));
                }
            }
        }

        public bool IsYoloDataset => !IsAnomalyDataset;

        public bool HasIssues
        {
            get => hasIssues;
            private set => SetProperty(ref hasIssues, value);
        }

        public bool HasClasses
        {
            get => hasClasses;
            private set => SetProperty(ref hasClasses, value);
        }

        public bool HasVisualQaItems
        {
            get => hasVisualQaItems;
            private set => SetProperty(ref hasVisualQaItems, value);
        }

        public bool ShowOnlyVisualQaProblems
        {
            get => showOnlyVisualQaProblems;
            set
            {
                if (SetProperty(ref showOnlyVisualQaProblems, value))
                {
                    RefreshVisibleVisualQaItems();
                }
            }
        }

        public string SelectedVisualQaSplitFilter
        {
            get => selectedVisualQaSplitFilter;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value)
                    ? AllVisualQaSplits
                    : value.Trim();
                if (SetProperty(ref selectedVisualQaSplitFilter, normalized))
                {
                    RefreshVisibleVisualQaItems();
                }
            }
        }

        public bool IsVisualQaSplitFilterEnabled => VisualQaSplitFilters.Count > 1;

        public WpfDatasetVisualQaClassFilterItem SelectedVisualQaClassFilter
        {
            get => selectedVisualQaClassFilter;
            set
            {
                WpfDatasetVisualQaClassFilterItem normalized =
                    value ?? VisualQaClassFilters.FirstOrDefault(item => !item.ClassIndex.HasValue)
                    ?? new WpfDatasetVisualQaClassFilterItem(null, string.Empty);
                if (SetProperty(ref selectedVisualQaClassFilter, normalized)
                    && isVisualQaLoaded
                    && !isRefreshingVisualQa)
                {
                    RefreshVisualQa();
                }
            }
        }

        public bool IsVisualQaClassFilterEnabled => VisualQaClassFilters.Count > 1;

        public string VisualQaStatusText
        {
            get => visualQaStatusText;
            private set => SetProperty(ref visualQaStatusText, value ?? string.Empty);
        }

        public WpfDatasetVisualQaItem SelectedVisualQaItem
        {
            get => selectedVisualQaItem;
            set
            {
                if (SetProperty(ref selectedVisualQaItem, value))
                {
                    OnPropertyChanged(nameof(HasSelectedVisualQaItem));
                }
            }
        }

        public bool HasSelectedVisualQaItem => SelectedVisualQaItem != null;

        public ICommand RefreshCommand
        {
            get => refreshCommand;
            private set => SetProperty(ref refreshCommand, value);
        }

        public ICommand OpenSelectedVisualQaImageCommand
        {
            get => openSelectedVisualQaImageCommand;
            private set => SetProperty(ref openSelectedVisualQaImageCommand, value);
        }

        public void ConfigureVisualQaOpen(Action<string> openImage)
        {
            openVisualQaImage = openImage ?? (_ => { });
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
        }

        public void Refresh(LabelingProjectData sourceData)
        {
            if (disposed)
            {
                return;
            }

            bool reloadVisualQa = isVisualQaLoaded;
            isVisualQaLoaded = false;
            data = sourceData;
            try
            {
                ApplyReport(YoloDatasetHealthService.Build(data));
            }
            catch (Exception ex)
            {
                Metrics.Clear();
                SplitRows.Clear();
                ClassRows.Clear();
                Issues.Clear();
                DatasetName = DatasetContextPresentationService.BuildDatasetName(string.Empty, data?.OutputRootPath);
                PurposeText = DatasetContextPresentationService.FormatPurposeName(data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
                OutputRootText = data?.OutputRootPath ?? string.Empty;
                StatusText = T("WpfDatasetHealth.Status.AnalysisFailed");
                StatusDetailText = ex.Message;
                GeneratedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                AnomalyDetailText = string.Empty;
                IsAnomalyDataset = data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
                Issues.Add(new WpfDatasetHealthIssueItem(T("WpfDatasetHealth.Issue.Error"), isBlocking: true));
                HasIssues = true;
                HasClasses = false;
            }

            if (reloadVisualQa)
            {
                RefreshVisualQa();
            }
            else
            {
                ResetVisualQa();
            }
        }

        public void EnsureVisualQaLoaded()
        {
            if (!isVisualQaLoaded)
            {
                RefreshVisualQa();
            }
        }

        private void RefreshVisualQa()
        {
            isRefreshingVisualQa = true;
            isVisualQaLoaded = true;
            try
            {
                RefreshVisualQaClassFilters();
                int? selectedClassIndex = SelectedVisualQaClassFilter?.ClassIndex;
                WpfDatasetVisualQaCatalog catalog =
                    visualQaService.BuildCatalog(data, selectedClassIndex);
                visualQaCatalogItems.Clear();
                visualQaCatalogItems.AddRange(catalog.Items);
                string truncationText = catalog.IsTruncated
                    ? Format("WpfDatasetHealth.VisualQa.Truncated", DatasetVisualQaService.MaximumCatalogItemCount)
                    : string.Empty;
                visualQaCatalogStatusText = selectedClassIndex.HasValue
                    ? Format(
                        "WpfDatasetHealth.VisualQa.ClassSummary",
                        catalog.ScannedImageCount,
                        catalog.MatchedImageCount,
                        catalog.ProblemCount,
                        catalog.Items.Count,
                        truncationText)
                    : Format(
                        "WpfDatasetHealth.VisualQa.CatalogSummary",
                        catalog.ScannedImageCount,
                        catalog.ProblemCount,
                        catalog.Items.Count,
                        truncationText);
            }
            catch (Exception ex)
            {
                visualQaCatalogItems.Clear();
                visualQaCatalogStatusText = Format("WpfDatasetHealth.VisualQa.CatalogFailure", ex.Message);
            }
            finally
            {
                isRefreshingVisualQa = false;
            }

            RefreshVisualQaSplitFilters();
            RefreshVisibleVisualQaItems();
        }

        private void ResetVisualQa()
        {
            visualQaCatalogItems.Clear();
            visualQaCatalogStatusText = T("WpfDatasetHealth.VisualQa.NotLoaded");
            RefreshVisualQaClassFilters();
            RefreshVisualQaSplitFilters();
            RefreshVisibleVisualQaItems();
        }

        private void RefreshVisualQaClassFilters()
        {
            WpfDatasetVisualQaClassFilterItem previous = SelectedVisualQaClassFilter;
            var filters = new List<WpfDatasetVisualQaClassFilterItem>
            {
                new WpfDatasetVisualQaClassFilterItem(null, string.Empty)
            };
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                for (int index = 0; index < (data?.ClassNamedList?.Count ?? 0); index++)
                {
                    string className = data.ClassNamedList[index]?.Text?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(className))
                    {
                        filters.Add(new WpfDatasetVisualQaClassFilterItem(index, className));
                    }
                }
            }

            VisualQaClassFilters = filters;
            SelectedVisualQaClassFilter = filters.FirstOrDefault(item => item.HasSameIdentity(previous))
                ?? filters[0];
            OnPropertyChanged(nameof(SelectedVisualQaClassFilter));
            OnPropertyChanged(nameof(IsVisualQaClassFilterEnabled));
        }

        private void RefreshVisualQaSplitFilters()
        {
            string previous = SelectedVisualQaSplitFilter;
            var filters = new List<string> { AllVisualQaSplits };
            foreach (string split in VisualQaSplitOrder.Where(split =>
                visualQaCatalogItems.Any(item =>
                    string.Equals(item.SplitText, split, StringComparison.OrdinalIgnoreCase))))
            {
                filters.Add(split);
            }

            VisualQaSplitFilters = filters;
            SelectedVisualQaSplitFilter = filters.Contains(previous)
                ? previous
                : AllVisualQaSplits;
            OnPropertyChanged(nameof(SelectedVisualQaSplitFilter));
            OnPropertyChanged(nameof(IsVisualQaSplitFilterEnabled));
        }

        private void RefreshVisibleVisualQaItems()
        {
            string previousImagePath = SelectedVisualQaItem?.ImagePath ?? string.Empty;
            VisualQaItems.Clear();
            foreach (WpfDatasetVisualQaItem item in visualQaCatalogItems.Where(item =>
                (!ShowOnlyVisualQaProblems || item.IsProblem)
                && (string.Equals(SelectedVisualQaSplitFilter, AllVisualQaSplits, StringComparison.Ordinal)
                    || string.Equals(item.SplitText, SelectedVisualQaSplitFilter, StringComparison.OrdinalIgnoreCase))))
            {
                VisualQaItems.Add(item);
            }

            HasVisualQaItems = VisualQaItems.Count > 0;
            SelectedVisualQaItem = VisualQaItems.FirstOrDefault(item =>
                    string.Equals(item.ImagePath, previousImagePath, StringComparison.OrdinalIgnoreCase))
                ?? VisualQaItems.FirstOrDefault(item => item.IsProblem)
                ?? VisualQaItems.FirstOrDefault();
            if (!isVisualQaLoaded)
            {
                VisualQaStatusText = visualQaCatalogStatusText;
                return;
            }

            string splitLabel = string.Equals(SelectedVisualQaSplitFilter, AllVisualQaSplits, StringComparison.Ordinal)
                ? T("WpfDatasetHealth.VisualQa.AllSplitLabel")
                : Format("WpfDatasetHealth.VisualQa.SplitLabel", SelectedVisualQaSplitFilter);
            string classLabel = SelectedVisualQaClassFilter?.ClassIndex.HasValue == true
                ? Format("WpfDatasetHealth.VisualQa.ClassLabel", SelectedVisualQaClassFilter.Text)
                : string.Empty;
            string problemsSuffix = ShowOnlyVisualQaProblems
                ? T("WpfDatasetHealth.VisualQa.ProblemsOnlySuffix")
                : string.Empty;
            VisualQaStatusText = Format(
                "WpfDatasetHealth.VisualQa.VisibleSummary",
                visualQaCatalogStatusText,
                VisualQaItems.Count,
                splitLabel,
                classLabel,
                problemsSuffix);
        }

        private void OpenSelectedVisualQaImage()
        {
            if (!string.IsNullOrWhiteSpace(SelectedVisualQaItem?.ImagePath))
            {
                openVisualQaImage(SelectedVisualQaItem.ImagePath);
            }
        }

        private void ApplyReport(YoloDatasetHealthReport report)
        {
            report ??= new YoloDatasetHealthReport(
                LabelingDatasetPurpose.ObjectDetection,
                new YoloDatasetReadinessReport(
                    new YoloDatasetValidationResult(Array.Empty<string>()),
                    new YoloDatasetValidationResult(Array.Empty<string>()),
                    new YoloDatasetStatistics()),
                anomalyReadiness: null,
                qualityAudit: null,
                splits: Array.Empty<YoloDatasetHealthSplitSummary>(),
                classes: Array.Empty<YoloDatasetHealthClassSummary>(),
                issues: Array.Empty<string>());

            DatasetName = DatasetContextPresentationService.BuildDatasetName(string.Empty, data?.OutputRootPath);
            PurposeText = DatasetContextPresentationService.FormatPurposeName(report.Purpose);
            OutputRootText = string.IsNullOrWhiteSpace(data?.OutputRootPath)
                ? T("WpfDatasetHealth.OutputRootPrompt")
                : data.OutputRootPath;
            IsAnomalyDataset = report.Purpose == LabelingDatasetPurpose.AnomalyDetection;
            StatusText = report.IsReady
                ? T("WpfDatasetHealth.Status.Ready")
                : T("WpfDatasetHealth.Status.NeedsAttention");
            StatusDetailText = report.IsReady
                ? T("WpfDatasetHealth.Status.Detail.Ready")
                : FormatIssue(report.Issues.FirstOrDefault());
            GeneratedAtText = Format(
                "WpfDatasetHealth.GeneratedAt",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            AnomalyDetailText = BuildAnomalyDetailText(report);

            Metrics.Clear();
            foreach (WpfDatasetHealthMetricItem item in BuildMetrics(report))
            {
                Metrics.Add(item);
            }

            SplitRows.Clear();
            foreach (YoloDatasetHealthSplitSummary split in report.Splits)
            {
                SplitRows.Add(new WpfDatasetHealthSplitRow(split, report.Purpose));
            }

            ClassRows.Clear();
            int totalClassCount = Math.Max(1, report.Classes.Sum(item => item.Count));
            foreach (YoloDatasetHealthClassSummary item in report.Classes.OrderByDescending(item => item.Count).ThenBy(item => item.ClassName, StringComparer.OrdinalIgnoreCase))
            {
                ClassRows.Add(new WpfDatasetHealthClassRow(
                    item.ClassName,
                    item.Count,
                    100D * item.Count / totalClassCount,
                    report.Purpose == LabelingDatasetPurpose.AnomalyDetection));
            }
            HasClasses = ClassRows.Count > 0;

            Issues.Clear();
            foreach (string issue in report.Issues.Take(6))
            {
                Issues.Add(new WpfDatasetHealthIssueItem(
                    FormatIssue(issue),
                    isBlocking: !report.IsReady && IsBlockingIssue(issue)));
            }
            if (report.Issues.Count > 6)
            {
                Issues.Add(new WpfDatasetHealthIssueItem(
                    Format("WpfDatasetHealth.Issue.Additional", report.Issues.Count - 6),
                    isBlocking: false));
            }
            HasIssues = Issues.Count > 0;
        }

        private static IEnumerable<WpfDatasetHealthMetricItem> BuildMetrics(YoloDatasetHealthReport report)
        {
            if (report.Purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                AnomalyClassificationTrainingReadinessReport anomaly = report.AnomalyReadiness;
                yield return new WpfDatasetHealthMetricItem(
                    T("WpfDatasetHealth.Metric.OriginalImages"),
                    (anomaly?.SourceImageCount ?? 0).ToString(CultureInfo.InvariantCulture),
                    T("WpfDatasetHealth.Metric.SourceImagesDetail"),
                    isProblem: false);
                yield return new WpfDatasetHealthMetricItem(
                    T("WpfDatasetHealth.Metric.Reviewed"),
                    ((anomaly?.NormalImageCount ?? 0) + (anomaly?.AbnormalImageCount ?? 0)).ToString(CultureInfo.InvariantCulture),
                    T("WpfDatasetHealth.Metric.ReviewedDetail"),
                    isProblem: false);
                yield return new WpfDatasetHealthMetricItem(
                    T("WpfDatasetHealth.Metric.NormalAbnormal"),
                    $"{anomaly?.NormalImageCount ?? 0} / {anomaly?.AbnormalImageCount ?? 0}",
                    T("WpfDatasetHealth.Metric.NormalAbnormalDetail"),
                    isProblem: anomaly?.NormalImageCount == 0 || anomaly?.AbnormalImageCount == 0);
                yield return new WpfDatasetHealthMetricItem(
                    T("WpfDatasetHealth.Metric.Unreviewed"),
                    (anomaly?.UnreviewedImageCount ?? 0).ToString(CultureInfo.InvariantCulture),
                    T("WpfDatasetHealth.Metric.UnreviewedDetail"),
                    isProblem: (anomaly?.UnreviewedImageCount ?? 0) > 0);
                yield break;
            }

            YoloDatasetStatistics statistics = report.YoloReadiness?.Statistics ?? new YoloDatasetStatistics();
            string primaryLabelValue = report.Purpose == LabelingDatasetPurpose.Segmentation
                    ? statistics.TotalSegmentationObjectCount > 0
                    ? statistics.TotalSegmentationObjectCount.ToString(CultureInfo.InvariantCulture)
                    : Format("WpfDatasetHealth.Metric.Segments", statistics.TotalMaskFileCount)
                : report.PrimaryLabelCount.ToString(CultureInfo.InvariantCulture);
            string primaryLabelDetail = report.Purpose == LabelingDatasetPurpose.Segmentation
                ? T("WpfDatasetHealth.Metric.Segments")
                : T("WpfDatasetHealth.Metric.YoloObjects");
            string qualityValue = report.QualityStatus switch
            {
                YoloDatasetHealthQualityStatus.Healthy => T("WpfDatasetHealth.Metric.Healthy"),
                YoloDatasetHealthQualityStatus.NotEvaluated => T("WpfDatasetHealth.Metric.NotEvaluated"),
                _ => report.QualityProblemCount.ToString(CultureInfo.InvariantCulture)
            };
            string qualityDetail = report.Purpose == LabelingDatasetPurpose.Segmentation
                ? T("WpfDatasetHealth.Metric.SegmentationQuality")
                : T("WpfDatasetHealth.Metric.MissingLabels");
            bool qualityNeedsAttention = report.QualityStatus != YoloDatasetHealthQualityStatus.Healthy;
            yield return new WpfDatasetHealthMetricItem(
                T("WpfDatasetHealth.Metric.StoredImages"),
                report.TotalImageCount.ToString(CultureInfo.InvariantCulture),
                T("WpfDatasetHealth.Metric.StoredImagesDetail"),
                isProblem: report.TotalImageCount == 0);
            yield return new WpfDatasetHealthMetricItem(
                T("WpfDatasetHealth.Metric.PrimaryLabel"),
                primaryLabelValue,
                primaryLabelDetail,
                isProblem: report.PrimaryLabelCount == 0);
            yield return new WpfDatasetHealthMetricItem(
                T("WpfDatasetHealth.Metric.LabelQuality"),
                qualityValue,
                qualityDetail,
                isProblem: qualityNeedsAttention);
            yield return new WpfDatasetHealthMetricItem(
                T("WpfDatasetHealth.Metric.SplitOverlap"),
                report.SplitContentOverlapCount == 0
                    ? T("WpfDatasetHealth.Metric.None")
                    : report.SplitContentOverlapCount.ToString(CultureInfo.InvariantCulture),
                T("WpfDatasetHealth.Metric.SplitOverlapDetail"),
                isProblem: report.SplitContentOverlapCount > 0);
        }

        private static string BuildAnomalyDetailText(YoloDatasetHealthReport report)
        {
            if (report?.Purpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                return string.Empty;
            }

            AnomalyClassificationTrainingReadinessReport anomaly = report.AnomalyReadiness;
            return Format(
                "WpfDatasetHealth.AnomalyDetail",
                anomaly?.TrainNormalImageCount ?? 0,
                anomaly?.TrainAbnormalImageCount ?? 0);
        }

        private static bool IsBlockingIssue(string issue)
        {
            string normalized = issue ?? string.Empty;
            return normalized.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("needs", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("missing", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("duplicate image content", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatIssue(string issue)
        {
            string normalized = issue?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return T("WpfDatasetHealth.Issue.Empty");
            }

            if (normalized.Contains("Test split is empty", StringComparison.OrdinalIgnoreCase))
            {
                return T("WpfDatasetHealth.Issue.TestSplitEmpty");
            }

            if (normalized.Contains("duplicate image content", StringComparison.OrdinalIgnoreCase))
            {
                return T("WpfDatasetHealth.Issue.DuplicateImages");
            }

            if (normalized.Contains("class balance is skewed", StringComparison.OrdinalIgnoreCase))
            {
                return T("WpfDatasetHealth.Issue.ClassBalance");
            }

            if (normalized.Contains("has only", StringComparison.OrdinalIgnoreCase))
            {
                return T("WpfDatasetHealth.Issue.SmallClass");
            }

            if (normalized.Contains("unreviewed image", StringComparison.OrdinalIgnoreCase))
            {
                return T("WpfDatasetHealth.Issue.Unreviewed");
            }

            return LocalizationTextRuntimeService.Translate(
                TrainingReadinessPresentationService.BuildFriendlyIssueSummary(normalized));
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            Refresh(data);
            OnPropertyChanged(nameof(DataScopeText));
            OnPropertyChanged(nameof(EvidenceBoundaryText));
        }

        private static string T(string key)
            => DatasetHealthTextFormatter.Translate(key);

        private static string Format(string key, params object[] arguments)
            => DatasetHealthTextFormatter.Format(key, arguments);
    }

    public sealed class WpfDatasetHealthMetricItem
    {
        public WpfDatasetHealthMetricItem(string title, string value, string detail, bool isProblem)
        {
            Title = title ?? string.Empty;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            IsProblem = isProblem;
        }

        public string Title { get; }

        public string Value { get; }

        public string Detail { get; }

        public bool IsProblem { get; }
    }

    public sealed class WpfDatasetHealthSplitRow
    {
        public WpfDatasetHealthSplitRow(YoloDatasetHealthSplitSummary source, LabelingDatasetPurpose purpose)
        {
            source ??= new YoloDatasetHealthSplitSummary(string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            SplitText = FormatSplit(source.Split);
            ImageCount = source.ImageCount;
            PrimaryAnnotationText = purpose == LabelingDatasetPurpose.Segmentation
                ? Format("WpfDatasetHealth.Split.Segments", source.PrimaryAnnotationCount)
                : Format("WpfDatasetHealth.Split.Objects", source.PrimaryAnnotationCount);
            CoverageText = purpose == LabelingDatasetPurpose.Segmentation
                ? Format(
                    "WpfDatasetHealth.Split.CoverageSeg",
                    source.SegmentFileCount,
                    source.MaskFileCount,
                    source.MissingLabelCount,
                    source.InvalidLabelLineCount)
                : Format(
                    "WpfDatasetHealth.Split.CoverageBox",
                    source.MissingLabelCount,
                    source.InvalidLabelLineCount);
            DetailText = purpose == LabelingDatasetPurpose.Segmentation
                ? Format(
                    "WpfDatasetHealth.Split.DetailSeg",
                    source.LabelFileCount,
                    source.EmptyLabelCount)
                : Format(
                    "WpfDatasetHealth.Split.DetailBox",
                    source.LabelFileCount,
                    source.EmptyLabelCount);
            HasProblem = purpose == LabelingDatasetPurpose.Segmentation
                ? source.MissingLabelCount > 0
                    || source.InvalidLabelLineCount > 0
                    || source.ImageCount > 0 && source.PrimaryAnnotationCount == 0
                : source.MissingLabelCount > 0 || source.InvalidLabelLineCount > 0;
        }

        public string SplitText { get; }

        public int ImageCount { get; }

        public string PrimaryAnnotationText { get; }

        public string CoverageText { get; }

        public string DetailText { get; }

        public bool HasProblem { get; }

        private static string FormatSplit(string split)
        {
            return split?.Trim().ToLowerInvariant() switch
            {
                "train" => T("WpfDatasetHealth.Split.Train"),
                "valid" => T("WpfDatasetHealth.Split.Valid"),
                "test" => T("WpfDatasetHealth.Split.Test"),
                _ => string.IsNullOrWhiteSpace(split) ? T("WpfDatasetHealth.Split.Unknown") : split
            };
        }

        private static string T(string key)
            => DatasetHealthTextFormatter.Translate(key);

        private static string Format(string key, params object[] arguments)
            => DatasetHealthTextFormatter.Format(key, arguments);
    }

    public sealed class WpfDatasetHealthClassRow
    {
        public WpfDatasetHealthClassRow(string className, int count, double sharePercent, bool isAnomaly)
        {
            ClassName = className ?? string.Empty;
            Count = Math.Max(0, count);
            SharePercent = Math.Clamp(sharePercent, 0D, 100D);
            ShareText = SharePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
            StatusText = Count == 0
                ? T("WpfDatasetHealth.Class.NeedsLabel")
                : isAnomaly
                    ? T("WpfDatasetHealth.Class.Reviewed")
                    : Count < 5
                        ? T("WpfDatasetHealth.Class.AddSamples")
                        : T("WpfDatasetHealth.Class.Checked");
            IsProblem = Count == 0;
        }

        private static string T(string key)
            => DatasetHealthTextFormatter.Translate(key);

        public string ClassName { get; }

        public int Count { get; }

        public double SharePercent { get; }

        public string ShareText { get; }

        public string StatusText { get; }

        public bool IsProblem { get; }
    }

    public sealed class WpfDatasetHealthIssueItem
    {
        public WpfDatasetHealthIssueItem(string text, bool isBlocking)
        {
            Text = text ?? string.Empty;
            IsBlocking = isBlocking;
        }

        public string Text { get; }

        public bool IsBlocking { get; }
    }
}
