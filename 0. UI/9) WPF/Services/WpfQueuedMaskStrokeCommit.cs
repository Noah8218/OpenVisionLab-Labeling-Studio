using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MvcVisionSystem
{
    internal sealed class WpfQueuedMaskStrokeCommit
    {
        public WpfQueuedMaskStrokeCommit(
            int sequence,
            string imagePath,
            System.Drawing.Size imageSize,
            IReadOnlyList<System.Drawing.Point> centers,
            int radius,
            WpfAnnotationTool tool,
            string className,
            CClassItem classItem,
            string actionName,
            bool hasActiveCandidates)
        {
            Sequence = sequence;
            ImagePath = imagePath ?? string.Empty;
            ImageSize = imageSize;
            Centers = centers ?? Array.Empty<System.Drawing.Point>();
            Radius = Math.Max(1, radius);
            Tool = tool;
            ClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className;
            ClassItem = classItem;
            ActionName = string.IsNullOrWhiteSpace(actionName) ? "Mask edit" : actionName;
            HasActiveCandidates = hasActiveCandidates;
            CreatedTicks = Stopwatch.GetTimestamp();
        }

        public int Sequence { get; }

        public string ImagePath { get; }

        public System.Drawing.Size ImageSize { get; }

        public IReadOnlyList<System.Drawing.Point> Centers { get; }

        public int Radius { get; }

        public WpfAnnotationTool Tool { get; }

        public string ClassName { get; }

        public CClassItem ClassItem { get; }

        public string ActionName { get; }

        public bool HasActiveCandidates { get; }

        public long CreatedTicks { get; }
    }
}
