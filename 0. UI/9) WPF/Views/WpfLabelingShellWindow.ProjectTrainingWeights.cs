using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
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
            RefreshModelCenterDashboard(comparison);

            string latestWeightsPath = comparison.LatestWeightsPath;
            if (!comparison.HasLatestWeights)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus($"{comparisonStatusText}. 모델 설정에서 학습 결과 모델을 직접 선택하세요.", isBusy: false);
                    AppendLog("학습 결과 모델 후보를 찾지 못했습니다.");
                }

                return false;
            }

            string currentWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            if (string.Equals(currentWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus(comparisonStatusText, isBusy: false);
                    AppendLog($"현재 검사 모델 유지: {latestWeightsPath}");
                }

                return false;
            }

            if (!comparison.ShouldApplyLatest)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus(comparisonStatusText, isBusy: false);
                    AppendLog($"현재 검사 모델 유지: {currentWeightsPath}");
                }

                return false;
            }

            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && File.Exists(currentWeightsPath)
                && !string.Equals(currentWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                pendingTrainingBaselineWeightsPath = currentWeightsPath;
            }

            settings.WeightsPath = latestWeightsPath;
            YoloModelSettingsViewModel?.LoadFrom(settings);
            string latestDisplayName = WpfTrainingWeightsService.FormatWeightsDisplayPath(latestWeightsPath);
            SetModelStatus($"모델 후보: {Path.GetFileName(latestWeightsPath)}");
            hasPendingTrainingWeightsRecipeSave = true;
            RefreshModelCenterDashboard(comparison);
            SetGlobalInferenceStatus(string.Empty, isBusy: false);
            SetModelStatus($"모델 후보: {latestDisplayName}");
            UpdateAppliedTrainingWeightsHistory(latestWeightsPath, savedToRecipe: false);
            FocusYoloModelSettingsTab();
            SaveYoloSettingsButton?.Focus();
            SetProjectConfigStatus("새 학습 모델 후보를 검사 모델 설정에 올렸습니다. 모델 비교 후 저장하면 프로젝트에 반영됩니다.");
            SetYoloCommandStatus($"새 학습 모델 후보: {Path.GetFileName(latestWeightsPath)} / {comparison.MetricsStatusText} / 모델 비교 후 저장 필요", isBusy: false);

            SetYoloCommandStatus($"현재 데이터셋 학습 완료: {latestDisplayName} / {comparison.MetricsStatusText} / 모델 비교 및 저장 필요", isBusy: false);

            if (!string.Equals(lastAutoAppliedTrainingWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                lastAutoAppliedTrainingWeightsPath = latestWeightsPath;
                AppendLog($"새 학습 모델 후보 등록: {latestWeightsPath} / baseline={pendingTrainingBaselineWeightsPath} / {comparison.MetricsStatusText} / 모델 비교 후 저장 필요");
            }

            return true;
        }

        private string GetTrainingComparisonCurrentWeightsPath(string configuredWeightsPath)
        {
            string pendingBaseline = pendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(pendingBaseline)
                && File.Exists(pendingBaseline)
                && !string.Equals(pendingBaseline, configuredWeightsPath?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return pendingBaseline;
            }

            return configuredWeightsPath ?? string.Empty;
        }

        private WpfTrainingWeightsComparison BuildCurrentTrainingWeightsComparison()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            return trainingWeightsService.BuildComparison(
                settings.ProjectRootPath,
                global.Data.OutputRootPath,
                GetTrainingComparisonCurrentWeightsPath(settings.WeightsPath));
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
            UpdateCandidateModelComparisonReviewPanel(comparison);
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
                return "교체 판단: 이미 현재 검사 모델로 사용 중";
            }

            if (comparison.LatestMetrics?.HasScore != true)
            {
                return "교체 판단: 보류 - 학습 지표가 없어 채택 판단 불가";
            }

            if (comparison.CurrentMetrics?.HasScore != true)
            {
                return "교체 판단: 비교 필요 - 현재 모델 지표가 부족합니다";
            }

            return comparison.MetricVerdictText switch
            {
                "새 모델 우세" => comparison.ShouldApplyLatest
                    ? "교체 판단: 새 모델 후보 우세 - 최종 검증 예시 확인 후 저장"
                    : "교체 판단: 새 모델 지표 우세 - 파일 상태 확인 필요",
                "현재 모델 우세" => "교체 판단: 현재 모델 유지",
                "동률" => "교체 판단: 보류 - 차이가 작아 예시 확인 필요",
                _ => "교체 판단: 보류 - 최종 검증 비교 필요"
            };
        }

        private void UpdateCandidateModelComparisonReviewPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            comparison ??= BuildCurrentTrainingWeightsComparison();
            IReadOnlyList<string> classNames = global.Data?.ClassNamedList == null
                ? Array.Empty<string>()
                : global.Data.ClassNamedList
                    .Select(item => item?.Text ?? string.Empty)
                    .ToList();
            double confidence = global.Data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            CandidateReviewViewModel.SetModelComparisonSourceText(
                WpfInferenceStatusPresentationService.BuildModelComparisonSourceText(
                    global.Data?.ProjectSettings?.PythonModel,
                    comparison?.CurrentWeightsPath,
                    comparison?.LatestWeightsPath));
            // The latest matching artifact remains authoritative; older matching runs are read-only history.
            WpfModelComparisonHistoryItem historyItem = RefreshModelComparisonHistoryItems(
                comparison?.CurrentWeightsPath,
                comparison?.LatestWeightsPath);
            WpfModelComparisonReviewReport report = historyItem == null
                ? WpfModelComparisonReviewReport.Empty
                : modelComparisonReviewService.BuildFromSummaryFile(
                    historyItem.SourcePath,
                    classNames,
                    confidence,
                    maxExamples: 5);
            CandidateReviewViewModel.SetModelComparisonReview(
                report,
                isHistoricalSelection: historyItem?.IsLatest == false);
            UpdateCandidateModelDecisionPanel(comparison);
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
                ? "새 모델 후보"
                : comparison.HasLatestWeights
                    ? "현재 모델 유지"
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
                "새 후보",
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
