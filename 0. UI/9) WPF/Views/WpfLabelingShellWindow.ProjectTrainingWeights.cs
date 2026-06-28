using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Training weight auto-apply is separated from recipe persistence because it only stages settings until save.
        private bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            WpfTrainingWeightsComparison comparison = trainingWeightsService.BuildComparison(
                settings.ProjectRootPath,
                global.Data.OutputRootPath,
                settings.WeightsPath);
            string comparisonStatusText = BuildTrainingComparisonStatusText(comparison);
            if (LearningWorkflowViewModel != null)
            {
                UpdateTrainingComparisonViewModel(comparison, comparisonStatusText);
            }

            string latestWeightsPath = comparison.LatestWeightsPath;
            if (!comparison.HasLatestWeights)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus($"{comparisonStatusText}. \uBAA8\uB378 \uC124\uC815\uC5D0\uC11C \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC744 \uC9C1\uC811 \uC120\uD0DD\uD558\uC138\uC694.", isBusy: false);
                    AppendLog("\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
                }

                return false;
            }

            string currentWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            if (string.Equals(currentWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus(comparisonStatusText, isBusy: false);
                    AppendLog($"현재 weight 유지: {latestWeightsPath}");
                }

                return false;
            }

            if (!comparison.ShouldApplyLatest)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus(comparisonStatusText, isBusy: false);
                    AppendLog($"기존 weight 유지: {currentWeightsPath}");
                }

                return false;
            }

            settings.WeightsPath = latestWeightsPath;
            YoloModelSettingsViewModel?.LoadFrom(settings);
            SetModelStatus($"모델: {Path.GetFileName(latestWeightsPath)}");
            hasPendingTrainingWeightsRecipeSave = true;
            UpdateAppliedTrainingWeightsHistory(latestWeightsPath, savedToRecipe: false);
            FocusYoloModelSettingsTab();
            SaveYoloSettingsButton?.Focus();
            SetProjectConfigStatus("\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uC801\uC6A9\uB428. \uC124\uC815 \uC800\uC7A5\uC744 \uB204\uB974\uBA74 \uD504\uB85C\uC81D\uD2B8\uC5D0 \uBC18\uC601\uB429\uB2C8\uB2E4.");
            SetYoloCommandStatus($"\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uC801\uC6A9: {Path.GetFileName(latestWeightsPath)} / {comparison.MetricsStatusText} / \uC124\uC815 \uC800\uC7A5 \uD544\uC694", isBusy: false);

            if (!string.Equals(lastAutoAppliedTrainingWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                lastAutoAppliedTrainingWeightsPath = latestWeightsPath;
                AppendLog($"\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uC801\uC6A9: {latestWeightsPath} / {comparison.MetricsStatusText} / \uC124\uC815 \uC800\uC7A5 \uD544\uC694");
            }

            return true;
        }

        private static string BuildTrainingComparisonStatusText(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : $"{comparison.StatusText} / {comparison.MetricsStatusText}";
        }

        private static string BuildTrainingComparisonSummaryText(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : comparison.MetricsStatusText;
        }

        private void UpdateTrainingComparisonViewModel(WpfTrainingWeightsComparison comparison, string comparisonStatusText = null)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            comparisonStatusText ??= BuildTrainingComparisonStatusText(comparison);
            LearningWorkflowViewModel.TrainingResultComparisonText = comparisonStatusText;
            LearningWorkflowViewModel.TrainingResultComparisonSummaryText = BuildTrainingComparisonSummaryText(comparison);
            LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = BuildTrainingModelAdoptionDecisionText(comparison);
            LearningWorkflowViewModel.SetTrainingResultReportItems(BuildTrainingResultReportItems(comparison));
            UpdateCandidateModelComparisonReviewPanel();
        }

        private static string BuildTrainingModelAdoptionDecisionText(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return "교체 판단: 학습 결과 비교 전";
            }

            if (!comparison.HasLatestWeights)
            {
                return "교체 판단: 학습 결과 없음";
            }

            if (string.Equals(comparison.LatestWeightsPath?.Trim(), comparison.CurrentWeightsPath?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return "교체 판단: 이미 현재 모델로 사용 중";
            }

            if (comparison.LatestMetrics?.HasScore != true)
            {
                return "교체 판단: 보류 - 학습 지표가 없어 품질 판단 불가";
            }

            if (comparison.CurrentMetrics?.HasScore != true)
            {
                return "교체 판단: 비교 필요 - 기존 모델 지표가 부족합니다";
            }

            return comparison.MetricVerdictText switch
            {
                "최신 우세" => comparison.ShouldApplyLatest
                    ? "교체 판단: 새 모델 후보 우세 - 최종 검증 예시 확인 후 적용"
                    : "교체 판단: 새 모델 지표 우세 - 파일 상태 확인 필요",
                "기존 우세" => "교체 판단: 기존 모델 유지",
                "동률" => "교체 판단: 보류 - 차이가 작아 예시 확인 필요",
                _ => "교체 판단: 보류 - 최종 검증 비교 필요"
            };
        }

        private void UpdateCandidateModelComparisonReviewPanel()
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            IReadOnlyList<string> classNames = global.Data?.ClassNamedList == null
                ? Array.Empty<string>()
                : global.Data.ClassNamedList
                    .Select(item => item?.Text ?? string.Empty)
                    .ToList();
            double confidence = global.Data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            // Candidate Review shows visual disagreement examples from the latest model-comparison artifact.
            // This keeps final best.pt adoption tied to held-out examples, not only aggregate metrics.
            WpfModelComparisonReviewReport report = modelComparisonReviewService.BuildLatestReport(classNames, confidence);
            CandidateReviewViewModel.SetModelComparisonReview(report);
        }

        private static IEnumerable<WpfTrainingResultReportItem> BuildTrainingResultReportItems(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                yield break;
            }

            string verdict = string.IsNullOrWhiteSpace(comparison.MetricVerdictText)
                ? "비교 대기"
                : comparison.MetricVerdictText;
            string decision = comparison.ShouldApplyLatest
                ? "\uC0C8 \uBAA8\uB378 \uC801\uC6A9 \uAC00\uB2A5"
                : comparison.HasLatestWeights
                    ? "기존 모델 유지"
                    : "학습 결과 없음";
            bool hasMetrics = comparison.LatestMetrics?.HasScore == true;

            yield return new WpfTrainingResultReportItem(
                "판정",
                verdict,
                decision,
                hasMetrics ? PackIconMaterialKind.CheckCircleOutline : PackIconMaterialKind.AlertCircleOutline,
                isWarning: !hasMetrics);
            yield return new WpfTrainingResultReportItem(
                "지표",
                FormatTrainingReportMetricValue(comparison),
                hasMetrics ? "mAP50-95를 우선 보고 precision/recall을 함께 확인합니다." : "results.csv가 없으면 모델 교체 판단을 보류합니다.",
                PackIconMaterialKind.ProgressClock,
                isWarning: !hasMetrics);
            yield return new WpfTrainingResultReportItem(
                "최신",
                FormatTrainingReportPath(comparison.LatestWeightsPath),
                FormatTrainingReportMetricSource(comparison.LatestMetrics),
                PackIconMaterialKind.FileDocumentOutline,
                isWarning: !comparison.HasLatestWeights);
            yield return new WpfTrainingResultReportItem(
                "현재",
                FormatTrainingReportPath(comparison.CurrentWeightsPath),
                FormatTrainingReportMetricSource(comparison.CurrentMetrics),
                PackIconMaterialKind.RobotIndustrial);
        }

        private static string FormatTrainingReportMetricValue(WpfTrainingWeightsComparison comparison)
        {
            if (comparison?.LatestMetrics?.HasScore != true)
            {
                return "지표 없음";
            }

            WpfTrainingRunMetrics metrics = comparison.LatestMetrics;
            if (metrics.Map5095.HasValue)
            {
                return $"mAP50-95 {FormatTrainingReportPercent(metrics.Map5095.Value)}";
            }

            if (metrics.Map50.HasValue)
            {
                return $"mAP50 {FormatTrainingReportPercent(metrics.Map50.Value)}";
            }

            if (metrics.Precision.HasValue)
            {
                return $"precision {FormatTrainingReportPercent(metrics.Precision.Value)}";
            }

            return metrics.Recall.HasValue
                ? $"recall {FormatTrainingReportPercent(metrics.Recall.Value)}"
                : "지표 있음";
        }

        private static string FormatTrainingReportMetricSource(WpfTrainingRunMetrics metrics)
            => string.IsNullOrWhiteSpace(metrics?.ResultsCsvPath)
                ? "results.csv 없음"
                : $"results.csv: {Path.GetFileName(Path.GetDirectoryName(metrics.ResultsCsvPath) ?? metrics.ResultsCsvPath)}";

        private static string FormatTrainingReportPath(string path)
            => string.IsNullOrWhiteSpace(path) ? "없음" : Path.GetFileName(path);

        private static string FormatTrainingReportPercent(double value)
            => $"{(Math.Abs(value) <= 1.5D ? value * 100D : value):0.0}%";
    }
}
