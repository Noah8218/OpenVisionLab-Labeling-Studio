using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetHealthViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private CData data;
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
        private ICommand refreshCommand = new RelayCommand(NoOpCommand);

        public WpfDatasetHealthViewModel(CData data = null)
        {
            this.data = data;
            RefreshCommand = new RelayCommand(() => Refresh(this.data));
            Refresh(data);
        }

        public ObservableCollection<WpfDatasetHealthMetricItem> Metrics { get; } = new ObservableCollection<WpfDatasetHealthMetricItem>();

        public ObservableCollection<WpfDatasetHealthSplitRow> SplitRows { get; } = new ObservableCollection<WpfDatasetHealthSplitRow>();

        public ObservableCollection<WpfDatasetHealthClassRow> ClassRows { get; } = new ObservableCollection<WpfDatasetHealthClassRow>();

        public ObservableCollection<WpfDatasetHealthIssueItem> Issues { get; } = new ObservableCollection<WpfDatasetHealthIssueItem>();

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

        public string DataScopeText => "분석 범위: 현재 Recipe 저장 데이터입니다. 외부 YOLO data.yaml은 별도 입력이며 이 화면의 집계에 포함하지 않습니다.";

        public string EvidenceBoundaryText => "이 화면은 데이터 구조와 라벨 상태를 점검합니다. 모델 채택에는 독립 held-out 데이터와 같은 조건의 모델 비교가 추가로 필요합니다.";

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

        public ICommand RefreshCommand
        {
            get => refreshCommand;
            private set => SetProperty(ref refreshCommand, value);
        }

        public void Refresh(CData sourceData)
        {
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
                DatasetName = WpfDatasetContextPresentationService.BuildDatasetName(string.Empty, data?.OutputRootPath);
                PurposeText = WpfDatasetContextPresentationService.FormatPurposeName(data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
                OutputRootText = data?.OutputRootPath ?? string.Empty;
                StatusText = "저장 데이터 분석 실패";
                StatusDetailText = ex.Message;
                GeneratedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                AnomalyDetailText = string.Empty;
                IsAnomalyDataset = data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
                Issues.Add(new WpfDatasetHealthIssueItem("분석 중 오류가 발생했습니다. 저장 폴더와 이미지 파일을 확인하세요.", isBlocking: true));
                HasIssues = true;
                HasClasses = false;
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

            DatasetName = WpfDatasetContextPresentationService.BuildDatasetName(string.Empty, data?.OutputRootPath);
            PurposeText = WpfDatasetContextPresentationService.FormatPurposeName(report.Purpose);
            OutputRootText = string.IsNullOrWhiteSpace(data?.OutputRootPath)
                ? "저장 폴더를 확인하세요."
                : data.OutputRootPath;
            IsAnomalyDataset = report.Purpose == LabelingDatasetPurpose.AnomalyDetection;
            StatusText = report.IsReady ? "학습 입력 구조: 준비됨" : "학습 입력 구조: 확인 필요";
            StatusDetailText = report.IsReady
                ? "저장된 분할과 주 라벨을 읽어 현재 상태를 정리했습니다."
                : FormatIssue(report.Issues.FirstOrDefault());
            GeneratedAtText = "갱신 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
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
                Issues.Add(new WpfDatasetHealthIssueItem($"추가 확인 항목 {report.Issues.Count - 6}개가 있습니다. 상세 라벨 점검 또는 품질 보고서를 사용하세요.", isBlocking: false));
            }
            HasIssues = Issues.Count > 0;
        }

        private static IEnumerable<WpfDatasetHealthMetricItem> BuildMetrics(YoloDatasetHealthReport report)
        {
            if (report.Purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                AnomalyClassificationTrainingReadinessReport anomaly = report.AnomalyReadiness;
                yield return new WpfDatasetHealthMetricItem("원본 이미지", (anomaly?.SourceImageCount ?? 0).ToString(CultureInfo.InvariantCulture), "현재 연결된 원본 이미지", isProblem: false);
                yield return new WpfDatasetHealthMetricItem("검토 완료", ((anomaly?.NormalImageCount ?? 0) + (anomaly?.AbnormalImageCount ?? 0)).ToString(CultureInfo.InvariantCulture), "정상/이상으로 확정된 이미지", isProblem: false);
                yield return new WpfDatasetHealthMetricItem("정상 / 이상", $"{anomaly?.NormalImageCount ?? 0} / {anomaly?.AbnormalImageCount ?? 0}", "분류 학습에 사용하는 두 상태", isProblem: anomaly?.NormalImageCount == 0 || anomaly?.AbnormalImageCount == 0);
                yield return new WpfDatasetHealthMetricItem("미검토", (anomaly?.UnreviewedImageCount ?? 0).ToString(CultureInfo.InvariantCulture), "학습 전에 정상 또는 이상으로 분류할 이미지", isProblem: (anomaly?.UnreviewedImageCount ?? 0) > 0);
                yield break;
            }

            YoloDatasetStatistics statistics = report.YoloReadiness?.Statistics ?? new YoloDatasetStatistics();
            string primaryLabelValue = report.Purpose == LabelingDatasetPurpose.Segmentation
                ? statistics.TotalSegmentationObjectCount > 0
                    ? statistics.TotalSegmentationObjectCount.ToString(CultureInfo.InvariantCulture)
                    : $"마스크 {statistics.TotalMaskFileCount}"
                : report.PrimaryLabelCount.ToString(CultureInfo.InvariantCulture);
            string primaryLabelDetail = report.Purpose == LabelingDatasetPurpose.Segmentation
                ? "세그먼트 객체 또는 저장된 마스크"
                : "YOLO 박스 객체";
            string qualityValue = report.QualityStatus switch
            {
                YoloDatasetHealthQualityStatus.Healthy => "정상",
                YoloDatasetHealthQualityStatus.NotEvaluated => "미확인",
                _ => report.QualityProblemCount.ToString(CultureInfo.InvariantCulture)
            };
            string qualityDetail = report.Purpose == LabelingDatasetPurpose.Segmentation
                ? "SEG 누락·손상 annotation 점검"
                : "누락 라벨과 잘못된 라벨 줄";
            bool qualityNeedsAttention = report.QualityStatus != YoloDatasetHealthQualityStatus.Healthy;
            yield return new WpfDatasetHealthMetricItem("저장 이미지", report.TotalImageCount.ToString(CultureInfo.InvariantCulture), "학습 / 검증 / 최종 검증 분할", isProblem: report.TotalImageCount == 0);
            yield return new WpfDatasetHealthMetricItem("주 라벨", primaryLabelValue, primaryLabelDetail, isProblem: report.PrimaryLabelCount == 0);
            yield return new WpfDatasetHealthMetricItem("라벨 품질", qualityValue, qualityDetail, isProblem: qualityNeedsAttention);
            yield return new WpfDatasetHealthMetricItem("분할 중복", report.SplitContentOverlapCount == 0 ? "없음" : report.SplitContentOverlapCount.ToString(CultureInfo.InvariantCulture), "학습/검증/최종 검증 간 동일 이미지 내용", isProblem: report.SplitContentOverlapCount > 0);
        }

        private static string BuildAnomalyDetailText(YoloDatasetHealthReport report)
        {
            if (report?.Purpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                return string.Empty;
            }

            AnomalyClassificationTrainingReadinessReport anomaly = report.AnomalyReadiness;
            return $"학습 분할 포함: 정상 {anomaly?.TrainNormalImageCount ?? 0}장 / 이상 {anomaly?.TrainAbnormalImageCount ?? 0}장. 원본 검토 상태와 학습 분할은 별도로 확인합니다.";
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
                return "저장 데이터 점검 결과를 확인하세요.";
            }

            if (normalized.Contains("Test split is empty", StringComparison.OrdinalIgnoreCase))
            {
                return "최종 검증 분할이 비어 있습니다. 모델 비교 전 독립 이미지를 확보하세요.";
            }

            if (normalized.Contains("duplicate image content", StringComparison.OrdinalIgnoreCase))
            {
                return "학습/검증/최종 검증 분할에 같은 이미지 내용이 있습니다. 분할을 다시 확인하세요.";
            }

            if (normalized.Contains("class balance is skewed", StringComparison.OrdinalIgnoreCase))
            {
                return "클래스별 라벨 수 차이가 큽니다. 적은 클래스 표본을 추가하세요.";
            }

            if (normalized.Contains("has only", StringComparison.OrdinalIgnoreCase))
            {
                return "클래스 표본 수가 적습니다. 학습 결과를 신뢰하기 전에 라벨 이미지를 추가하세요.";
            }

            if (normalized.Contains("unreviewed image", StringComparison.OrdinalIgnoreCase))
            {
                return "미검토 이미지가 있습니다. 정상 또는 이상 상태를 저장한 뒤 학습하세요.";
            }

            return WpfTrainingReadinessPresentationService.BuildFriendlyIssueSummary(normalized);
        }
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
                ? $"세그먼트/마스크 {source.PrimaryAnnotationCount}"
                : $"객체 {source.PrimaryAnnotationCount}";
            CoverageText = purpose == LabelingDatasetPurpose.Segmentation
                ? $"세그먼트 파일 {source.SegmentFileCount} / 마스크 {source.MaskFileCount} / 누락 {source.MissingLabelCount} / 손상 {source.InvalidLabelLineCount}"
                : $"누락 {source.MissingLabelCount} / invalid {source.InvalidLabelLineCount}";
            DetailText = purpose == LabelingDatasetPurpose.Segmentation
                ? $"보조 box 라벨 파일 {source.LabelFileCount} / 빈 정상 {source.EmptyLabelCount}"
                : $"라벨 파일 {source.LabelFileCount} / 빈 정상 {source.EmptyLabelCount}";
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
                "train" => "학습",
                "valid" => "검증",
                "test" => "최종 검증",
                _ => string.IsNullOrWhiteSpace(split) ? "미확인" : split
            };
        }
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
                ? "라벨 필요"
                : isAnomaly
                    ? "검토 완료"
                    : Count < 5
                        ? "표본 추가 권장"
                        : "확인";
            IsProblem = Count == 0;
        }

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
