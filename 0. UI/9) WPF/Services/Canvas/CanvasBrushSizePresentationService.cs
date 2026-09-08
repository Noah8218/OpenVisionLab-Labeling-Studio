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
    }
}
