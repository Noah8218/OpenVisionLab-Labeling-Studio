using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public enum WpfSegmentationZOrderMove
    {
        SendToBack,
        SendBackward,
        BringForward,
        BringToFront
    }

    public class SegmentationZOrderService
    {
        public const string StructuralOperationName = "ZOrder";

        public static int GetNextZOrder(IEnumerable<LabelingSegmentationObject> segments)
            => (segments ?? Enumerable.Empty<LabelingSegmentationObject>())
                .Where(segment => segment != null)
                .Select(segment => segment.ZOrder)
                .DefaultIfEmpty(-1)
                .Max() + 1;

        public bool TryPlanMove(
            IReadOnlyList<LabelingSegmentationObject> segments,
            int selectedIndex,
            WpfSegmentationZOrderMove move,
            out SegmentationZOrderResult result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (segments == null
                || selectedIndex < 0
                || selectedIndex >= segments.Count
                || segments[selectedIndex] == null
                || segments.Any(segment => segment == null))
            {
                error = "\uC21C\uC11C\uB97C \uBCC0\uACBD\uD560 \uC138\uADF8\uBA3C\uD2B8\uB97C \uD558\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
                return false;
            }

            LabelingSegmentationObject selected = segments[selectedIndex];
            List<LabelingSegmentationObject> ordered = segments
                .Select((segment, index) => new { Segment = segment, Index = index })
                .Where(item => item.Segment != null)
                .OrderBy(item => item.Segment.ZOrder)
                .ThenBy(item => item.Index)
                .Select(item => item.Segment)
                .ToList();
            int currentIndex = ordered.FindIndex(segment => ReferenceEquals(segment, selected));
            if (currentIndex < 0)
            {
                error = "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uAC00 \uD604\uC7AC \uBAA9\uB85D\uACFC \uC77C\uCE58\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            int targetIndex = move switch
            {
                WpfSegmentationZOrderMove.SendToBack => 0,
                WpfSegmentationZOrderMove.SendBackward => Math.Max(0, currentIndex - 1),
                WpfSegmentationZOrderMove.BringForward => Math.Min(ordered.Count - 1, currentIndex + 1),
                WpfSegmentationZOrderMove.BringToFront => ordered.Count - 1,
                _ => currentIndex
            };
            if (targetIndex == currentIndex)
            {
                error = move is WpfSegmentationZOrderMove.SendToBack or WpfSegmentationZOrderMove.SendBackward
                    ? "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uB294 \uC774\uBBF8 \uAC00\uC7A5 \uB4A4\uC5D0 \uC788\uC2B5\uB2C8\uB2E4."
                    : "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uB294 \uC774\uBBF8 \uAC00\uC7A5 \uC55E\uC5D0 \uC788\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            ordered.RemoveAt(currentIndex);
            ordered.Insert(targetIndex, selected);
            result = new SegmentationZOrderResult(ordered, targetIndex, move);
            return true;
        }
    }

    public class SegmentationZOrderResult
    {
        public SegmentationZOrderResult(
            IReadOnlyList<LabelingSegmentationObject> orderedSegments,
            int selectedIndex,
            WpfSegmentationZOrderMove move)
        {
            OrderedSegments = orderedSegments ?? Array.Empty<LabelingSegmentationObject>();
            SelectedIndex = selectedIndex;
            Move = move;
        }

        public IReadOnlyList<LabelingSegmentationObject> OrderedSegments { get; }

        public int SelectedIndex { get; }

        public WpfSegmentationZOrderMove Move { get; }
    }

    [Obsolete("Use SegmentationZOrderService.", false)]
    public sealed class WpfSegmentationZOrderService
    {
        private readonly SegmentationZOrderService inner = new SegmentationZOrderService();

        public const string StructuralOperationName = SegmentationZOrderService.StructuralOperationName;

        public static int GetNextZOrder(IEnumerable<LabelingSegmentationObject> segments)
            => SegmentationZOrderService.GetNextZOrder(segments);

        public bool TryPlanMove(
            IReadOnlyList<LabelingSegmentationObject> segments,
            int selectedIndex,
            WpfSegmentationZOrderMove move,
            out WpfSegmentationZOrderResult result,
            out string error)
        {
            bool success = inner.TryPlanMove(segments, selectedIndex, move, out SegmentationZOrderResult canonicalResult, out error);
            result = success ? new WpfSegmentationZOrderResult(canonicalResult) : null;
            return success;
        }
    }

    [Obsolete("Use SegmentationZOrderResult.", false)]
    public sealed class WpfSegmentationZOrderResult : SegmentationZOrderResult
    {
        public WpfSegmentationZOrderResult(
            IReadOnlyList<LabelingSegmentationObject> orderedSegments,
            int selectedIndex,
            WpfSegmentationZOrderMove move)
            : base(orderedSegments, selectedIndex, move)
        {
        }

        public WpfSegmentationZOrderResult(SegmentationZOrderResult result)
            : this(result?.OrderedSegments, result?.SelectedIndex ?? -1, result?.Move ?? default)
        {
        }
    }
}
