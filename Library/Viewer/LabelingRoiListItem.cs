using System.Drawing;

namespace MvcVisionSystem
{
    public enum LabelingAnnotationKind
    {
        Rectangle,
        Segmentation
    }

    public sealed class LabelingRoiListItem
    {
        public LabelingRoiListItem(
            int index,
            string className,
            Rectangle roi,
            LabelingAnnotationKind kind = LabelingAnnotationKind.Rectangle,
            int sourceIndex = -1,
            bool isSelected = false)
        {
            Index = index;
            ClassName = className ?? string.Empty;
            Roi = roi;
            Kind = kind;
            SourceIndex = sourceIndex;
            IsSelected = isSelected;
        }

        public int Index { get; }

        public string ClassName { get; }

        public Rectangle Roi { get; }

        public LabelingAnnotationKind Kind { get; }

        public int SourceIndex { get; }

        public bool IsSelected { get; }

        public string RoiText => Roi.ToString();
    }

    public sealed class LabelingAnnotationSelectionChangedEventArgs : System.EventArgs
    {
        public LabelingAnnotationSelectionChangedEventArgs(int selectedListIndex, LabelingAnnotationKind? selectedKind, string className)
        {
            SelectedListIndex = selectedListIndex;
            SelectedKind = selectedKind;
            ClassName = className ?? string.Empty;
        }

        public int SelectedListIndex { get; }

        public LabelingAnnotationKind? SelectedKind { get; }

        public string ClassName { get; }
    }
}
