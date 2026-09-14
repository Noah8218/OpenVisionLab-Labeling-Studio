using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    // Owns the labeling command entry points that used to live in the Shell
    // workflow partial. Annotation persistence and queue services remain the
    // existing owners; this adapter only coordinates their UI-facing effects.
    internal sealed class AnnotationWorkflowCommandAdapter
    {
        private readonly AnnotationWorkflowCommandAdapterContext context;

        internal AnnotationWorkflowCommandAdapter(AnnotationWorkflowCommandAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #region Commands
        internal void ExecuteLoadSampleCommand()
        {
            if (IsCloseApproved())
            {
                return;
            }

            context.TryLoadStartupSampleImage?.Invoke();
        }

        internal void ExecuteAddSampleRoiCommand()
        {
            if (IsCloseApproved())
            {
                return;
            }

            Size activeImageSize = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            if (activeImageSize.IsEmpty)
            {
                context.AppendLog?.Invoke("박스 라벨을 추가하려면 이미지를 먼저 불러오세요.");
                return;
            }

            int width = Math.Max(20, activeImageSize.Width / 5);
            int height = Math.Max(20, activeImageSize.Height / 5);
            int x = Math.Max(0, (activeImageSize.Width - width) / 2);
            int y = Math.Max(0, (activeImageSize.Height - height) / 2);
            var roi = new Rectangle(x, y, width, height);

            context.RegisterAnnotationHistoryBeforeChange?.Invoke("가이드 박스 추가");
            context.ManualRois?.Add(roi);
            context.ManualRoiClassNames?.Add(FirstNonEmpty(
                context.SelectedClassNameProvider?.Invoke(),
                "Defect"));
            context.ManualRoiShapeKinds?.Add(CanvasRoiShapeKind.Rectangle);
            context.ManualRoiOverlayIds?.Add(string.Empty);
            context.RedrawReviewRois?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.ShowSavedLabelsWorkflowView?.Invoke();
            context.AppendLog?.Invoke($"박스 라벨 추가: {roi.X},{roi.Y},{roi.Width},{roi.Height}");
        }

        internal void ExecuteSaveAnnotationsCommand()
        {
            if (IsCloseApproved())
            {
                return;
            }

            string completedImagePath = context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
            context.AppendLog?.Invoke("라벨 저장 명령 호출");
            AnnotationSaveOutcome save = context.SaveCurrentAnnotations?.Invoke()
                ?? AnnotationSaveOutcome.Failed;
            context.AppendLog?.Invoke(
                $"라벨 저장 결과: 성공:{save.Succeeded} 객체:{save.SavedCount}");
            if (save.Succeeded)
            {
                context.MarkActiveImageConfirmed?.Invoke();
                if (context.GetSelectedImageQueueFilter?.Invoke() == WpfImageQueueFilter.Unlabeled)
                {
                    context.ScheduleOpenNextIncompleteQueueImageAfterSave?.Invoke(completedImagePath);
                }

                context.AppendLog?.Invoke(
                    $"YOLO 라벨 저장. 객체:{save.SavedCount}  {context.BuildLabelPathSummary?.Invoke()}");
                return;
            }

            context.AppendLog?.Invoke(
                "라벨 저장이 완료되지 않았습니다. 저장 상태를 확인하고 다시 시도하세요.");
        }

        internal void OpenNextIncompleteQueueImageAfterSave(string completedImagePath)
        {
            if (IsCloseApproved())
            {
                return;
            }

            if (!(context.TryOpenNextIncompleteQueueImage?.Invoke(completedImagePath) ?? false))
            {
                context.FinishQueueCompletionAndGuideDatasetCheck?.Invoke();
            }
        }

        internal void ExecuteCompleteNoObjectAndNextCommand()
        {
            if (IsCloseApproved())
            {
                return;
            }

            if (!(context.HasActiveImage?.Invoke() ?? false))
            {
                context.AppendLog?.Invoke("객체 없음으로 완료할 이미지를 먼저 열어주세요.");
                return;
            }

            int pendingCandidateCount = context.PendingCandidateCountProvider?.Invoke() ?? 0;
            if (pendingCandidateCount > 0)
            {
                context.ShowCandidateReviewWorkflowView?.Invoke();
                context.AppendLog?.Invoke(
                    $"남은 AI 후보 {pendingCandidateCount}개를 먼저 확정하거나 숨긴 뒤 객체 없음으로 완료하세요.");
                return;
            }

            if (context.HasCanvasLabelObjects?.Invoke() == true)
            {
                context.ShowSavedLabelsWorkflowView?.Invoke();
                context.AppendLog?.Invoke(
                    "현재 이미지에 라벨된 객체가 있습니다. 객체 없음으로 완료하려면 기존 라벨을 먼저 삭제하세요.");
                return;
            }

            if (!(context.SaveCurrentEmptyAnnotations?.Invoke() ?? false))
            {
                context.AppendLog?.Invoke(
                    "객체 없음 라벨을 저장하지 못했습니다. 이미지와 저장 경로를 확인하세요.");
                return;
            }

            context.MarkActiveImageNoCandidate?.Invoke();
            context.RefreshYoloTrainingStepCompletion?.Invoke();
            context.AppendLog?.Invoke(
                $"객체 없음으로 완료: {context.BuildLabelPathSummary?.Invoke()}");
            if (!(context.TryOpenNextIncompleteQueueImageWithoutPath?.Invoke() ?? false))
            {
                context.FinishQueueCompletionAndGuideDatasetCheck?.Invoke();
            }
        }

        private bool IsCloseApproved()
            => context.IsApplicationCloseApproved?.Invoke() == true;

        private static string FirstNonEmpty(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;
        #endregion
    }

    internal readonly struct AnnotationSaveOutcome
    {
        internal static AnnotationSaveOutcome Failed => new AnnotationSaveOutcome(false, 0);

        internal AnnotationSaveOutcome(bool succeeded, int savedCount)
        {
            Succeeded = succeeded;
            SavedCount = savedCount;
        }

        internal bool Succeeded { get; }
        internal int SavedCount { get; }
    }

    internal sealed class AnnotationWorkflowCommandAdapterContext
    {
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> TryLoadStartupSampleImage { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<string> SelectedClassNameProvider { get; init; }
        internal Action<string> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal IList<Rectangle> ManualRois { get; init; }
        internal IList<string> ManualRoiClassNames { get; init; }
        internal IList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal IList<string> ManualRoiOverlayIds { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Func<AnnotationSaveOutcome> SaveCurrentAnnotations { get; init; }
        internal Action MarkActiveImageConfirmed { get; init; }
        internal Func<WpfImageQueueFilter> GetSelectedImageQueueFilter { get; init; }
        internal Action<string> ScheduleOpenNextIncompleteQueueImageAfterSave { get; init; }
        internal Func<string, bool> TryOpenNextIncompleteQueueImage { get; init; }
        internal Func<bool> HasActiveImage { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Func<bool> HasCanvasLabelObjects { get; init; }
        internal Func<bool> SaveCurrentEmptyAnnotations { get; init; }
        internal Action MarkActiveImageNoCandidate { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal Func<bool> TryOpenNextIncompleteQueueImageWithoutPath { get; init; }
        internal Action FinishQueueCompletionAndGuideDatasetCheck { get; init; }
        internal Func<string> BuildLabelPathSummary { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
