using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfDetectionTargetService
    {
        public string ResolveInteractiveTargetPath(string requestedImagePath, string activeImagePath, PythonModelSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(requestedImagePath))
            {
                return requestedImagePath;
            }

            if (!string.IsNullOrWhiteSpace(activeImagePath))
            {
                return activeImagePath;
            }

            return YoloWorkerSmokeTestService.ResolveSmokeImagePath(settings);
        }

        public IReadOnlyList<WpfImageQueueItem> BuildBatchQueue(IEnumerable<WpfImageQueueItem> items)
        {
            // Batch detection should inspect each physical image once, even when filtered views contain duplicate rows.
            return (items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
                .GroupBy(item => item.ImagePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        public string BuildEmptyBatchMessage(string scopeText)
        {
            return $"\uC77C\uAD04 \uAC80\uC0AC \uAC74\uB108\uB700. \uB300\uC0C1 \uC774\uBBF8\uC9C0 \uC5C6\uC74C: {scopeText}";
        }
    }
}