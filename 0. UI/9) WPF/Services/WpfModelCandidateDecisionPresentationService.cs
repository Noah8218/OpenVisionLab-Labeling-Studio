using MvcVisionSystem._1._Core;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class WpfModelCandidateDecisionPresentation
    {
        public bool CanSave { get; set; }

        public bool CanReject { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string SaveToolTip { get; set; } = string.Empty;

        public string RejectToolTip { get; set; } = string.Empty;
    }

    public static class WpfModelCandidateDecisionPresentationService
    {
        public static WpfModelCandidateDecisionPresentation BuildPendingCandidate(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool canReject)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "후보 모델");
            string baselineName = string.IsNullOrWhiteSpace(baselineWeightsPath)
                ? "기존 모델 확인 필요"
                : FormatModelName(baselineWeightsPath, "기존 모델");

            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = true,
                CanReject = canReject,
                StatusText = $"후보 결정: 저장 또는 거절 필요 ({candidateName})",
                DetailText = $"저장하면 다음 추론부터 이 후보를 사용합니다. 거절하면 기존 검사 모델 {baselineName}을 유지합니다.",
                SaveToolTip = "이 학습 결과를 recipe의 현재 검사 모델로 저장합니다.",
                RejectToolTip = canReject
                    ? "이 학습 결과를 쓰지 않고 기존 검사 모델을 유지합니다."
                    : "되돌릴 기존 검사 모델 경로가 없어 거절할 수 없습니다."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildRejectedCandidate(
            string candidateWeightsPath,
            string decisionSummary)
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = $"후보 결정: 거절됨 ({FormatModelName(candidateWeightsPath, "후보 모델")})",
                DetailText = string.IsNullOrWhiteSpace(decisionSummary)
                    ? "이 후보는 현재 recipe의 검사 모델로 채택하지 않았습니다."
                    : decisionSummary.Trim(),
                SaveToolTip = "이미 거절된 후보입니다. 다시 쓰려면 모델 설정에서 직접 선택하세요.",
                RejectToolTip = "이미 거절된 후보입니다."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildSavedCandidate(string candidateWeightsPath)
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = $"후보 결정: 검사 모델로 저장됨 ({FormatModelName(candidateWeightsPath, "후보 모델")})",
                DetailText = "이 후보는 recipe의 현재 검사 모델 이력에 기록되어 있습니다.",
                SaveToolTip = "이미 검사 모델로 저장된 후보입니다.",
                RejectToolTip = "이미 저장된 후보는 여기서 거절하지 않습니다."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildReviewAvailable()
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = "후보 결정: 검토 가능",
                DetailText = "후보 검증을 실행해 기존 검사 모델과 비교한 뒤 저장 여부를 결정하세요.",
                SaveToolTip = "먼저 후보 검증으로 학습 결과를 선택하세요.",
                RejectToolTip = "먼저 후보 검증으로 학습 결과를 선택하세요."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildNoCandidate()
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = "후보 결정: 후보 없음",
                DetailText = "학습이 완료된 모델 후보가 생기면 이곳에서 저장 또는 거절 결정을 남길 수 있습니다.",
                SaveToolTip = "저장할 학습 모델 후보가 없습니다.",
                RejectToolTip = "거절할 학습 모델 후보가 없습니다."
            };
        }

        public static string BuildNoRejectCandidateStatus()
        {
            return "거절할 학습 모델 후보가 없습니다.";
        }

        public static string BuildRejectDecisionSummary()
        {
            return "후보 거절, 기존 검사 모델 유지";
        }

        public static string BuildRejectCommandStatus(string candidateWeightsPath, bool configSaved)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "후보 모델");
            return configSaved
                ? $"학습 모델 후보를 거절했습니다: {candidateName}. 현재 검사 모델을 유지합니다."
                : $"학습 모델 후보를 거절했습니다: {candidateName}. Recipe 저장은 별도 확인이 필요합니다.";
        }

        public static string BuildRejectProjectConfigStatus(bool configSaved)
        {
            return configSaved
                ? "학습 모델 후보 거절 기록 저장 완료."
                : "학습 모델 후보 거절 기록은 메모리에 반영됐지만 recipe 저장은 실패했습니다.";
        }

        public static string BuildRejectLog(string candidateWeightsPath, string baselineWeightsPath)
        {
            return $"학습 모델 후보 거절: {candidateWeightsPath} / baseline={baselineWeightsPath}";
        }

        public static string BuildRejectFailureStatus(string message)
        {
            return $"학습 모델 후보 거절 실패: {NormalizeMessage(message)}";
        }

        private static string FormatModelName(string path, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path.Trim());
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "상세 원인을 확인할 수 없습니다."
                : message.Trim();
        }
    }
}
