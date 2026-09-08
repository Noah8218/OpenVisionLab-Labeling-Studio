using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns box-drawing method and four-point progress presentation without WPF.
    /// FourPointBoxService remains the geometry and point-input owner.
    /// </summary>
    public sealed class BoxDrawingPresentationWorkflowService
    {
        private static readonly string[] RoleNames =
        {
            "위",
            "아래",
            "왼쪽",
            "오른쪽"
        };

        private LabelingBoxDrawingMethod selectedMethod = LabelingBoxDrawingMethod.TwoPointDrag;
        private bool isRectangleToolSelected;
        private int acceptedPointCount;

        public LabelingBoxDrawingMethod SelectedMethod => selectedMethod;

        public BoxDrawingPresentationSnapshot SetSelectedMethod(LabelingBoxDrawingMethod method)
        {
            selectedMethod = NormalizeMethod(method);
            acceptedPointCount = 0;
            return BuildSnapshot();
        }

        public BoxDrawingPresentationSnapshot SetRectangleToolSelected(bool isSelected)
        {
            isRectangleToolSelected = isSelected;
            acceptedPointCount = 0;
            return BuildSnapshot();
        }

        public BoxDrawingPresentationSnapshot SetAcceptedPointCount(int count)
        {
            acceptedPointCount = Math.Clamp(count, 0, 4);
            return BuildSnapshot();
        }

        public BoxDrawingPresentationSnapshot GetSnapshot() => BuildSnapshot();

        private BoxDrawingPresentationSnapshot BuildSnapshot()
        {
            string nextRole = acceptedPointCount switch
            {
                0 => RoleNames[0],
                1 => RoleNames[1],
                2 => RoleNames[2],
                3 => RoleNames[3],
                _ => "완료"
            };
            bool isProgressVisible = isRectangleToolSelected
                && selectedMethod == LabelingBoxDrawingMethod.FourPointExtreme;
            return new BoxDrawingPresentationSnapshot(
                selectedMethod,
                isRectangleToolSelected,
                isProgressVisible,
                acceptedPointCount,
                $"4점 극점 · {nextRole} {acceptedPointCount}/4");
        }

        private static LabelingBoxDrawingMethod NormalizeMethod(LabelingBoxDrawingMethod method)
            => method == LabelingBoxDrawingMethod.FourPointExtreme
                ? LabelingBoxDrawingMethod.FourPointExtreme
                : LabelingBoxDrawingMethod.TwoPointDrag;
    }

    public sealed class BoxDrawingPresentationSnapshot
    {
        public BoxDrawingPresentationSnapshot(
            LabelingBoxDrawingMethod selectedMethod,
            bool isMethodSelectorVisible,
            bool isProgressVisible,
            int acceptedPointCount,
            string progressText)
        {
            SelectedMethod = selectedMethod;
            IsMethodSelectorVisible = isMethodSelectorVisible;
            IsProgressVisible = isProgressVisible;
            AcceptedPointCount = acceptedPointCount;
            ProgressText = progressText ?? string.Empty;
        }

        public LabelingBoxDrawingMethod SelectedMethod { get; }

        public bool IsMethodSelectorVisible { get; }

        public bool IsProgressVisible { get; }

        public int AcceptedPointCount { get; }

        public string ProgressText { get; }
    }
}
