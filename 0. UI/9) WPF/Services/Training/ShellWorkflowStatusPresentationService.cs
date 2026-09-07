using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Snapshot of the shell state needed to present the top workflow status.
    /// It intentionally contains no Window, control, image, or persistence
    /// references so the status policy can be tested without WPF.
    /// </summary>
    public sealed class ShellWorkflowStatusContext
    {
        public ShellWorkflowStatusContext(
            bool isInferenceMode,
            int totalImageCount,
            int completedImageCount,
            bool hasPendingCandidates,
            bool hasUnsavedAnnotationChanges,
            bool isTrainingReady,
            bool hasActiveImage)
        {
            IsInferenceMode = isInferenceMode;
            TotalImageCount = totalImageCount;
            CompletedImageCount = completedImageCount;
            HasPendingCandidates = hasPendingCandidates;
            HasUnsavedAnnotationChanges = hasUnsavedAnnotationChanges;
            IsTrainingReady = isTrainingReady;
            HasActiveImage = hasActiveImage;
        }

        public bool IsInferenceMode { get; }

        public int TotalImageCount { get; }

        public int CompletedImageCount { get; }

        public bool HasPendingCandidates { get; }

        public bool HasUnsavedAnnotationChanges { get; }

        public bool IsTrainingReady { get; }

        public bool HasActiveImage { get; }
    }

    public sealed class ShellWorkflowStatus
    {
        public ShellWorkflowStatus(
            string stageText,
            string progressText,
            string nextActionText)
        {
            StageText = stageText ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
        }

        public string StageText { get; }

        public string ProgressText { get; }

        public string NextActionText { get; }
    }

    /// <summary>
    /// Owns the pure top-rail workflow status policy. The shell only supplies
    /// a state snapshot and forwards the resulting text to its status bar VM.
    /// </summary>
    public static class ShellWorkflowStatusPresentationService
    {
        public static ShellWorkflowStatus Build(ShellWorkflowStatusContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            int remainingCount = Math.Max(0, context.TotalImageCount - context.CompletedImageCount);
            string progressText = context.TotalImageCount > 0
                ? $"진행: {context.CompletedImageCount}/{context.TotalImageCount} 완료 · {remainingCount} 남음"
                : "진행: 이미지 없음";

            return new ShellWorkflowStatus(
                BuildStageText(context),
                progressText,
                BuildNextActionText(context, remainingCount));
        }

        private static string BuildStageText(ShellWorkflowStatusContext context)
        {
            if (context.IsInferenceMode)
            {
                return context.HasPendingCandidates
                    ? "단계: AI 후보 검토"
                    : "단계: AI 후보 대기";
            }

            if (context.HasUnsavedAnnotationChanges)
            {
                return "단계: 저장 필요";
            }

            if (context.IsTrainingReady)
            {
                return "단계: 학습 준비";
            }

            return context.HasActiveImage
                ? "단계: 라벨링"
                : context.TotalImageCount > 0
                    ? "단계: 이미지 선택"
                    : "단계: 데이터셋 준비";
        }

        private static string BuildNextActionText(
            ShellWorkflowStatusContext context,
            int remainingCount)
        {
            if (!context.HasActiveImage)
            {
                return context.TotalImageCount > 0
                    ? "다음: 이미지 선택"
                    : "다음: 데이터셋 시작";
            }

            if (context.HasPendingCandidates)
            {
                return "다음: AI 후보 확정/스킵";
            }

            if (context.HasUnsavedAnnotationChanges)
            {
                return "다음: 저장";
            }

            if (context.TotalImageCount > 0 && remainingCount > 0)
            {
                return "다음: 다음 미완료 이미지";
            }

            if (context.TotalImageCount > 0)
            {
                return context.IsTrainingReady
                    ? "다음: 학습 시작"
                    : "다음: 데이터셋 점검";
            }

            return "다음: 이미지 폴더";
        }
    }

    [Obsolete("Use ShellWorkflowStatusContext.", false)]
    public sealed class WpfShellWorkflowStatusContext
    {
        private readonly ShellWorkflowStatusContext inner;

        public WpfShellWorkflowStatusContext(
            bool isInferenceMode,
            int totalImageCount,
            int completedImageCount,
            bool hasPendingCandidates,
            bool hasUnsavedAnnotationChanges,
            bool isTrainingReady,
            bool hasActiveImage)
        {
            inner = new ShellWorkflowStatusContext(
                isInferenceMode,
                totalImageCount,
                completedImageCount,
                hasPendingCandidates,
                hasUnsavedAnnotationChanges,
                isTrainingReady,
                hasActiveImage);
        }

        internal ShellWorkflowStatusContext ToCanonical() => inner;
    }

    [Obsolete("Use ShellWorkflowStatus.", false)]
    public sealed class WpfShellWorkflowStatus
    {
        private readonly ShellWorkflowStatus inner;

        public WpfShellWorkflowStatus(string stageText, string progressText, string nextActionText)
        {
            inner = new ShellWorkflowStatus(stageText, progressText, nextActionText);
        }

        private WpfShellWorkflowStatus(ShellWorkflowStatus source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static WpfShellWorkflowStatus FromCanonical(ShellWorkflowStatus source)
            => source == null ? null : new WpfShellWorkflowStatus(source);

        public string StageText => inner.StageText;

        public string ProgressText => inner.ProgressText;

        public string NextActionText => inner.NextActionText;
    }

    [Obsolete("Use ShellWorkflowStatusPresentationService.", false)]
    public static class WpfShellWorkflowStatusPresentationService
    {
        public static WpfShellWorkflowStatus Build(WpfShellWorkflowStatusContext context)
            => WpfShellWorkflowStatus.FromCanonical(
                ShellWorkflowStatusPresentationService.Build(context?.ToCanonical()));
    }
}
