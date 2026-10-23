using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas brush-size bounds and display format without owning mutable UI state.
    /// </summary>
    public static class CanvasBrushSizePresentationService
    {
        public const int DefaultSize = 12;
        public const int MinimumSize = 2;
        public const int MaximumSize = 64;
        public const int Step = 2;

        public static int Normalize(int size)
            => Math.Clamp(size, MinimumSize, MaximumSize);

        public static string Format(int size)
            => $"{Normalize(size)}px";

        /// <summary>
        /// Formats the source-pixel brush diameter using the image-relative
        /// percentage shown by the reference labeling workflow. Editing and
        /// persistence continue to use source pixels.
        /// </summary>
        public static string FormatReferencePercent(int size)
        {
            double percent = Normalize(size) / 2048D * 100D;
            return $"{percent:0.##}%";
        }

        public static int GetSourceImageRadius(int size)
            => Math.Max(1, (int)Math.Round(Normalize(size) / 2D));
    }
}
