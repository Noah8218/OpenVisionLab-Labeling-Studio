using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class ApplicationClosePolicyService
    {
        public IReadOnlyList<string> GetActiveWorkNames(ApplicationCloseWorkState state)
        {
            state ??= new ApplicationCloseWorkState();

            var names = new List<string>();
            if (state.IsCreatingSmartMask)
            {
                names.Add("Smart Mask 후보 생성");
            }
            if (state.IsDetecting)
            {
                names.Add("현재 이미지 AI 검사");
            }
            if (state.IsBatchDetectionRunning)
            {
                names.Add("일괄 AI 검사");
            }
            if (state.IsExternalYoloDatasetIntakeRunning)
            {
                names.Add("외부 YOLO 데이터셋 확인");
            }
            if (state.IsExternalEvaluationDataAuditRunning)
            {
                names.Add("외부 평가 데이터 대조");
            }
            if (state.IsHistoricalSegmentationRemediationAuditRunning)
            {
                names.Add("SEG 보정 검토");
            }
            if (state.IsTrainingRunning)
            {
                names.Add("모델 학습");
            }
            if (state.IsYoloEnvironmentCommandRunning)
            {
                names.Add("모델 실행환경 설정");
            }
            if (state.IsModelComparisonRunning)
            {
                names.Add("모델 비교");
            }
            if (state.IsSegmentationAdapterComparisonRunning)
            {
                names.Add("세그멘테이션 어댑터 비교");
            }
            if (state.IsAnomalyEvaluationRunning)
            {
                names.Add("이상 분류 평가");
            }

            return names;
        }

        public ApplicationClosePlan Build(ApplicationCloseState state)
        {
            state ??= new ApplicationCloseState();

            string[] activeWorkNames = (state.ActiveWorkNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            int pendingCandidateCount = Math.Max(0, state.PendingCandidateCount);
            bool hasUnsavedAnnotations = state.HasUnsavedAnnotations;
            if (!hasUnsavedAnnotations && pendingCandidateCount == 0 && activeWorkNames.Length == 0)
            {
                return ApplicationClosePlan.NoPrompt;
            }

            var messageLines = new List<string>();
            if (hasUnsavedAnnotations)
            {
                messageLines.Add("현재 이미지의 라벨 편집이 아직 파일에 저장되지 않았습니다.");
            }

            if (pendingCandidateCount > 0)
            {
                messageLines.Add(
                    $"미확정 AI 후보 {pendingCandidateCount}개는 확정 라벨이 아니며 종료하면 저장되지 않고 폐기됩니다.");
            }

            if (activeWorkNames.Length > 0)
            {
                messageLines.Add(
                    $"진행 중인 작업 {activeWorkNames.Length}개는 창이 닫힐 때 중지됩니다.");
            }

            messageLines.Add(
                hasUnsavedAnnotations
                    ? "라벨을 저장할지 선택한 뒤 종료하세요."
                    : pendingCandidateCount > 0 && activeWorkNames.Length > 0
                        ? "후보를 폐기하고 진행 중인 작업을 중지한 뒤 종료할지 선택하세요."
                        : activeWorkNames.Length > 0
                            ? "진행 중인 작업을 중지하고 종료할지 선택하세요."
                            : "현재 후보를 폐기하고 종료할지 선택하세요.");

            var detailLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(state.ActiveImagePath))
            {
                detailLines.Add("현재 이미지: " + Path.GetFileName(state.ActiveImagePath));
            }

            if (hasUnsavedAnnotations && !string.IsNullOrWhiteSpace(state.UnsavedAnnotationReason))
            {
                detailLines.Add("저장되지 않은 편집: " + state.UnsavedAnnotationReason.Trim());
            }

            if (pendingCandidateCount > 0)
            {
                detailLines.Add($"미확정 AI 후보: {pendingCandidateCount}개");
            }

            if (activeWorkNames.Length > 0)
            {
                detailLines.Add("중지할 작업: " + string.Join(", ", activeWorkNames));
            }

            return new ApplicationClosePlan
            {
                PromptKind = hasUnsavedAnnotations
                    ? WpfApplicationClosePromptKind.SaveDiscardCancel
                    : WpfApplicationClosePromptKind.DiscardCancel,
                Title = hasUnsavedAnnotations
                    ? "저장하지 않은 라벨이 있습니다"
                    : pendingCandidateCount > 0
                        ? "확인되지 않은 작업이 있습니다"
                        : "진행 중인 작업이 있습니다",
                Message = string.Join(Environment.NewLine, messageLines),
                Details = string.Join(Environment.NewLine, detailLines),
                PrimaryButtonText = hasUnsavedAnnotations
                    ? "저장 후 종료"
                    : pendingCandidateCount > 0 && activeWorkNames.Length > 0
                        ? "폐기·중지 후 종료"
                        : activeWorkNames.Length > 0
                            ? "작업 중지 후 종료"
                            : "폐기하고 종료",
                SecondaryButtonText = hasUnsavedAnnotations ? "저장하지 않고 종료" : "계속 작업",
                TertiaryButtonText = hasUnsavedAnnotations ? "계속 작업" : string.Empty
            };
        }
    }

    [Obsolete("Use ApplicationClosePolicyService.", false)]
    public sealed class WpfApplicationClosePolicyService : ApplicationClosePolicyService
    {
        public WpfApplicationClosePlan Build(WpfApplicationCloseState state)
            => new WpfApplicationClosePlan(base.Build(state));
    }

    public class ApplicationCloseWorkState
    {
        public bool IsCreatingSmartMask { get; set; }

        public bool IsDetecting { get; set; }

        public bool IsBatchDetectionRunning { get; set; }

        public bool IsExternalYoloDatasetIntakeRunning { get; set; }

        public bool IsExternalEvaluationDataAuditRunning { get; set; }

        public bool IsHistoricalSegmentationRemediationAuditRunning { get; set; }

        public bool IsTrainingRunning { get; set; }

        public bool IsYoloEnvironmentCommandRunning { get; set; }

        public bool IsModelComparisonRunning { get; set; }

        public bool IsSegmentationAdapterComparisonRunning { get; set; }

        public bool IsAnomalyEvaluationRunning { get; set; }
    }

    public class ApplicationCloseState
    {
        public bool HasUnsavedAnnotations { get; set; }

        public string UnsavedAnnotationReason { get; set; } = string.Empty;

        public int PendingCandidateCount { get; set; }

        public IReadOnlyList<string> ActiveWorkNames { get; set; } = Array.Empty<string>();

        public string ActiveImagePath { get; set; } = string.Empty;
    }

    public enum WpfApplicationClosePromptKind
    {
        None,
        SaveDiscardCancel,
        DiscardCancel
    }

    public enum WpfApplicationCloseDecision
    {
        Cancel,
        SaveAndClose,
        DiscardAndClose
    }

    public class ApplicationClosePlan
    {
        public static ApplicationClosePlan NoPrompt { get; } = new ApplicationClosePlan();

        public WpfApplicationClosePromptKind PromptKind { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public string PrimaryButtonText { get; set; } = string.Empty;

        public string SecondaryButtonText { get; set; } = string.Empty;

        public string TertiaryButtonText { get; set; } = string.Empty;

        public bool RequiresPrompt => PromptKind != WpfApplicationClosePromptKind.None;
    }

    [Obsolete("Use ApplicationCloseWorkState.", false)]
    public sealed class WpfApplicationCloseWorkState : ApplicationCloseWorkState
    {
    }

    [Obsolete("Use ApplicationCloseState.", false)]
    public sealed class WpfApplicationCloseState : ApplicationCloseState
    {
    }

    [Obsolete("Use ApplicationClosePlan.", false)]
    public sealed class WpfApplicationClosePlan : ApplicationClosePlan
    {
        public static new WpfApplicationClosePlan NoPrompt { get; } = new WpfApplicationClosePlan();

        public WpfApplicationClosePlan()
        {
        }

        public WpfApplicationClosePlan(ApplicationClosePlan source)
        {
            if (source == null)
            {
                return;
            }

            PromptKind = source.PromptKind;
            Title = source.Title;
            Message = source.Message;
            Details = source.Details;
            PrimaryButtonText = source.PrimaryButtonText;
            SecondaryButtonText = source.SecondaryButtonText;
            TertiaryButtonText = source.TertiaryButtonText;
        }
    }
}
