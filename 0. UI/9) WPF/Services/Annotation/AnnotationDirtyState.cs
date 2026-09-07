using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the current unsaved-annotation reason without owning presentation,
    /// persistence, or crash-recovery behavior.
    /// </summary>
    public sealed class AnnotationDirtyState
    {
        private string reason = string.Empty;

        public string Reason => reason;

        public bool IsDirty => !string.IsNullOrWhiteSpace(reason);

        public void MarkDirty(string value, string fallbackReason)
        {
            reason = string.IsNullOrWhiteSpace(value)
                ? fallbackReason ?? string.Empty
                : value;
        }

        public void Clear()
        {
            reason = string.Empty;
        }
    }
}
