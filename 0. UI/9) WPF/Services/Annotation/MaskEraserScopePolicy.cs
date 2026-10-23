using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public static class MaskEraserScopePolicy
    {
        public static bool CanEdit(
            LabelingSegmentationObject segment,
            MaskEraserScope scope,
            string currentClassName,
            int selectedSegmentIndex,
            IReadOnlyList<LabelingSegmentationObject> segments,
            Func<LabelingSegmentationObject, bool> canEditBase)
        {
            if (segment == null || !(canEditBase?.Invoke(segment) ?? true))
            {
                return false;
            }

            return scope switch
            {
                MaskEraserScope.CurrentClass => string.Equals(
                    segment.ClassName,
                    currentClassName,
                    StringComparison.OrdinalIgnoreCase),
                MaskEraserScope.SelectedObject => selectedSegmentIndex >= 0
                    && selectedSegmentIndex < (segments?.Count ?? 0)
                    && ReferenceEquals(segments[selectedSegmentIndex], segment),
                _ => true
            };
        }
    }
}
