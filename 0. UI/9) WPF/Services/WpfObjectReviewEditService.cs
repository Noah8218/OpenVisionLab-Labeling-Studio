using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public static class WpfObjectReviewEditService
    {
        public static string NormalizeClassName(string className)
        {
            string normalized = ClassCatalogService.NormalizeClassName(className);
            return string.IsNullOrWhiteSpace(normalized) ? "Defect" : normalized;
        }

        public static string GetClassName(
            WpfObjectReviewItemRef item,
            IReadOnlyList<string> manualClassNames,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            IReadOnlyList<YoloWorkerSmokeCandidate> confirmedCandidates)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return item.Source switch
            {
                WpfObjectReviewSource.ManualRoi => GetManualClassName(manualClassNames, item.Index),
                WpfObjectReviewSource.ManualSegment when IsValidIndex(manualSegments, item.Index) => FirstNonEmpty(manualSegments[item.Index]?.ClassName, manualSegments[item.Index]?.ClassItem?.Text, "Defect"),
                WpfObjectReviewSource.ConfirmedAi when IsValidIndex(confirmedCandidates, item.Index) => confirmedCandidates[item.Index]?.ClassName ?? string.Empty,
                _ => string.Empty
            };
        }

        public static bool TryApplyClass(
            WpfObjectReviewItemRef item,
            IReadOnlyList<DrawingRectangle> manualRois,
            IList<string> manualClassNames,
            IList<LabelingSegmentationObject> manualSegments,
            IList<YoloWorkerSmokeCandidate> confirmedCandidates,
            string className,
            out string normalizedClassName,
            CClassItem classItem = null)
        {
            normalizedClassName = NormalizeClassName(className);
            if (item == null)
            {
                return false;
            }

            switch (item.Source)
            {
                case WpfObjectReviewSource.ManualRoi:
                    if (!IsValidIndex(manualRois, item.Index) || manualClassNames == null)
                    {
                        return false;
                    }

                    while (manualClassNames.Count <= item.Index)
                    {
                        manualClassNames.Add("Defect");
                    }

                    manualClassNames[item.Index] = normalizedClassName;
                    return true;

                case WpfObjectReviewSource.ManualSegment:
                    if (!IsValidIndex(manualSegments, item.Index) || manualSegments[item.Index] == null)
                    {
                        return false;
                    }

                    LabelingSegmentationObject segment = manualSegments[item.Index];
                    segment.ClassName = normalizedClassName;
                    if (classItem != null)
                    {
                        segment.ClassItem = classItem;
                    }

                    if (segment.ClassItem == null)
                    {
                        segment.ClassItem = new CClassItem();
                    }

                    segment.ClassItem.Text = normalizedClassName;
                    if (segment.IsRasterMask)
                    {
                        segment.RenderVersion++;
                        segment.RenderDirtyBounds = segment.Bounds;
                    }

                    return true;

                case WpfObjectReviewSource.ConfirmedAi:
                    if (!IsValidIndex(confirmedCandidates, item.Index) || confirmedCandidates[item.Index] == null)
                    {
                        return false;
                    }

                    confirmedCandidates[item.Index].ClassName = normalizedClassName;
                    return true;

                default:
                    return false;
            }
        }

        public static bool TryDelete(
            WpfObjectReviewItemRef item,
            IList<DrawingRectangle> manualRois,
            IList<string> manualClassNames,
            IList<LabelingSegmentationObject> manualSegments,
            IList<YoloWorkerSmokeCandidate> confirmedCandidates)
        {
            if (item == null)
            {
                return false;
            }

            switch (item.Source)
            {
                case WpfObjectReviewSource.ManualRoi:
                    if (!RemoveAt(manualRois, item.Index))
                    {
                        return false;
                    }

                    RemoveAt(manualClassNames, item.Index);
                    return true;

                case WpfObjectReviewSource.ManualSegment:
                    return RemoveAt(manualSegments, item.Index);

                case WpfObjectReviewSource.ConfirmedAi:
                    return RemoveAt(confirmedCandidates, item.Index);

                default:
                    return false;
            }
        }

        private static string GetManualClassName(IReadOnlyList<string> manualClassNames, int index)
        {
            return IsValidIndex(manualClassNames, index) && !string.IsNullOrWhiteSpace(manualClassNames[index])
                ? manualClassNames[index]
                : "Defect";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool RemoveAt<T>(IList<T> items, int index)
        {
            if (!IsValidIndex(items, index))
            {
                return false;
            }

            items.RemoveAt(index);
            return true;
        }

        private static bool IsValidIndex<T>(IReadOnlyList<T> items, int index)
            => items != null && index >= 0 && index < items.Count;

        private static bool IsValidIndex<T>(IList<T> items, int index)
            => items != null && index >= 0 && index < items.Count;
    }

    public sealed class WpfObjectReviewItemRef
    {
        private WpfObjectReviewItemRef(WpfObjectReviewSource source, int index, string sourceId = "")
        {
            Source = source;
            Index = index;
            SourceId = sourceId ?? string.Empty;
        }

        public WpfObjectReviewSource Source { get; }

        public int Index { get; }

        public string SourceId { get; }

        public static WpfObjectReviewItemRef Manual(int index, string sourceId = "") => new WpfObjectReviewItemRef(WpfObjectReviewSource.ManualRoi, index, sourceId);

        public static WpfObjectReviewItemRef ManualSegment(int index) => new WpfObjectReviewItemRef(WpfObjectReviewSource.ManualSegment, index);

        public static WpfObjectReviewItemRef ConfirmedAi(int index) => new WpfObjectReviewItemRef(WpfObjectReviewSource.ConfirmedAi, index);
    }

    public enum WpfObjectReviewSource
    {
        ManualRoi,
        ManualSegment,
        ConfirmedAi
    }
}
