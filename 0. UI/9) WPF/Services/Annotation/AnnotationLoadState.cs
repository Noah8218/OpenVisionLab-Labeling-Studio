namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps the active image's malformed-label guard separate from unsaved edit state.
    /// A blocked source remains untouched until an explicit recovery workflow is chosen.
    /// </summary>
    public sealed class AnnotationLoadState
    {
        public bool IsSaveBlocked { get; private set; }

        public string LabelPath { get; private set; } = string.Empty;

        public string ErrorSummary { get; private set; } = string.Empty;

        public void BlockSave(string labelPath, string errorSummary)
        {
            IsSaveBlocked = true;
            LabelPath = labelPath ?? string.Empty;
            ErrorSummary = string.IsNullOrWhiteSpace(errorSummary)
                ? "라벨 파일 형식을 확인한 뒤 명시적으로 복구해야 합니다."
                : errorSummary;
        }

        public void Clear()
        {
            IsSaveBlocked = false;
            LabelPath = string.Empty;
            ErrorSummary = string.Empty;
        }
    }
}
