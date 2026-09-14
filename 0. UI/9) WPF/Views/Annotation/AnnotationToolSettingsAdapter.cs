using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.Canvas;
using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns persisted annotation-tool preferences and the canvas input mode
    /// derived from those preferences. The Shell supplies state and UI-safe
    /// callbacks; this owner does not access WPF controls directly.
    /// </summary>
    internal sealed class AnnotationToolSettingsAdapter
    {
        private readonly AnnotationToolSettingsAdapterContext context;

        internal AnnotationToolSettingsAdapter(AnnotationToolSettingsAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal bool IsFourPointBoxInputActive()
            => context.ActiveAnnotationToolProvider?.Invoke() == WpfAnnotationTool.Rectangle
                && context.SelectedBoxDrawingMethodProvider?.Invoke()
                    == LabelingBoxDrawingMethod.FourPointExtreme;

        internal void ExecuteSetBoxDrawingMethod(LabelingBoxDrawingMethod method)
        {
            LabelingBoxDrawingMethod normalized = Enum.IsDefined(typeof(LabelingBoxDrawingMethod), method)
                ? method
                : LabelingBoxDrawingMethod.TwoPointDrag;
            context.CancelFourPointBoxDraft?.Invoke(false);
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            data.ProjectSettings.BoxDrawingMethod = normalized;
            context.RestoreBoxDrawingMethod?.Invoke(normalized);
            PersistCurrentRecipe("박스 입력 방식 저장 실패: ");

            ApplyRectangleDrawingInputMode();
            string methodName = normalized == LabelingBoxDrawingMethod.FourPointExtreme
                ? "4점 극점"
                : "2점 드래그";
            context.SetYoloCommandStatus?.Invoke($"박스 입력: {methodName}", false);
            context.AppendLog?.Invoke($"박스 입력 방법: {methodName}");
        }

        internal void RestoreBoxDrawingMethodFromProject()
        {
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            context.RestoreBoxDrawingMethod?.Invoke(
                data.ProjectSettings.BoxDrawingMethod);
            context.CancelFourPointBoxDraft?.Invoke(false);
            ApplyRectangleDrawingInputMode();
        }

        internal void ApplyRectangleDrawingInputMode()
        {
            if (context.IsMainCanvasAvailable?.Invoke() != true
                || context.ActiveAnnotationToolProvider?.Invoke() != WpfAnnotationTool.Rectangle)
            {
                return;
            }

            if (IsFourPointBoxInputActive())
            {
                context.SetCanvasTeachingMode?.Invoke(false);
                context.SetCanvasImagePointInputMode?.Invoke(true);
                context.SetCanvasInteractionMode?.Invoke(CanvasInteractionMode.None);
                context.SetFourPointBoxProgress?.Invoke(
                    context.FourPointBoxPointCountProvider?.Invoke() ?? 0);
                context.RefreshCanvasWorkflowContext?.Invoke();
                return;
            }

            context.SetCanvasImagePointInputMode?.Invoke(false);
            context.SetCanvasTeachingMode?.Invoke(true);
            context.SetFourPointBoxProgress?.Invoke(0);
            context.RefreshCanvasWorkflowContext?.Invoke();
        }

        internal void ExecuteSetSmartMaskAutoContourMode(bool enabled)
        {
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            data.ProjectSettings.SmartMaskAutoContourEnabled = enabled;
            PersistCurrentRecipe("자동 윤곽 옵션 저장 실패: ");

            if (enabled)
            {
                context.SelectAnnotationTool?.Invoke(WpfAnnotationTool.Rectangle);
                context.RefreshSmartMaskCommandState?.Invoke(
                    "자동 윤곽 켜짐 · 새 사각형을 완성하면 MobileSAM 후보를 바로 만듭니다.");
                context.SetYoloCommandStatus?.Invoke("라벨링 옵션: 자동 윤곽 켜짐", false);
                context.AppendLog?.Invoke("자동 윤곽 켜짐: 새 사각형마다 검토용 윤곽 후보를 자동 생성합니다.");
                return;
            }

            context.RefreshSmartMaskCommandState?.Invoke(
                "자동 윤곽 꺼짐 · 사각형은 일반 박스 라벨로 유지됩니다.");
            context.SetYoloCommandStatus?.Invoke("라벨링 옵션: 일반 박스", false);
            context.AppendLog?.Invoke("자동 윤곽 꺼짐: 새 사각형을 일반 박스 라벨로 유지합니다.");
        }

        private void PersistCurrentRecipe(string errorPrefix)
        {
            string recipeName = context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName)
                || context.SaveCurrentRecipeConfiguration == null)
            {
                return;
            }

            try
            {
                RecipeConfigurationSaveResult saveResult =
                    context.SaveCurrentRecipeConfiguration(recipeName);
                if (saveResult != null && !saveResult.IsSuccess)
                {
                    context.AppendLog?.Invoke(errorPrefix + saveResult.ErrorMessage);
                }
            }
            catch (Exception error)
            {
                context.AppendLog?.Invoke(errorPrefix + error.Message);
            }
        }
    }

    internal sealed class AnnotationToolSettingsAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<LabelingBoxDrawingMethod> SelectedBoxDrawingMethodProvider { get; init; }
        internal Func<int> FourPointBoxPointCountProvider { get; init; }
        internal Func<bool> IsMainCanvasAvailable { get; init; }
        internal Action<bool> SetCanvasTeachingMode { get; init; }
        internal Action<bool> SetCanvasImagePointInputMode { get; init; }
        internal Action<CanvasInteractionMode> SetCanvasInteractionMode { get; init; }
        internal Action<int> SetFourPointBoxProgress { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<bool> CancelFourPointBoxDraft { get; init; }
        internal Action<LabelingBoxDrawingMethod> RestoreBoxDrawingMethod { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Func<string, RecipeConfigurationSaveResult> SaveCurrentRecipeConfiguration { get; init; }
        internal Action<WpfAnnotationTool> SelectAnnotationTool { get; init; }
        internal Action<string> RefreshSmartMaskCommandState { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
