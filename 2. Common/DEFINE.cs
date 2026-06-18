using System.Drawing;

namespace MvcVisionSystem
{
    public static class DEFINE
    {
        public const int Main = 0;

        public enum VISION_DOCK_FORM
        {
            IMAGELIST = 0,
            CLASSLIST,
            DETECTIONREVIEW,
            TRAINING,
            LOG
        }

        public enum LABELING_WORKSPACE_MODE
        {
            TEACHING = 0,
            DETECTION_REVIEW,
            TRAINING
        }

        public enum ALGORITHM : uint
        {
            BLOB,
            CONTOUR,
            MATCING,
            MEAN
        }

        public enum AUTHORIZATION : uint
        {
            OPERATOR = 1,
            ENGINEER,
            MASTER
        }

        public static Color MOUSEHOVER_COLOR = Color.FromArgb(83, 97, 212);
        public static Color ButtonColorBlue = Color.FromArgb(83, 97, 212);
        public static Color ButtonColorRed = Color.FromArgb(234, 79, 82);

        public const string Threshold = "Threshold";
        public const string AdaptiveThreshold = "AdaptiveThreshold";
    }
}
