using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    internal static class LabelingAnnotationPersistence
    {
        public static bool SaveCurrent(
            Image image,
            IReadOnlyDictionary<string, List<CRectangleObject>> rois,
            CData data)
        {
            return SaveCurrent(image, rois, null, data);
        }

        public static bool SaveCurrent(
            Image image,
            IReadOnlyDictionary<string, List<CRectangleObject>> rois,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments,
            CData data)
        {
            if (image == null || data == null || string.IsNullOrWhiteSpace(data.LastSelectImageName))
            {
                return false;
            }

            IReadOnlyDictionary<string, List<CRectangleObject>> normalizedRois = NormalizeRoisByClass(rois);
            EnsureRoiClasses(data, normalizedRois);
            EnsureSegmentationClasses(data, segments);
            YoloAnnotationService.SaveAnnotations(
                data.LastSelectImageName,
                image,
                normalizedRois,
                data.ClassNamedList,
                data);
            YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                data.LastSelectImageName,
                image,
                segments,
                data.ClassNamedList,
                data);
            return true;
        }

        private static IReadOnlyDictionary<string, List<CRectangleObject>> NormalizeRoisByClass(
            IReadOnlyDictionary<string, List<CRectangleObject>> rois)
        {
            var result = new Dictionary<string, List<CRectangleObject>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<CRectangleObject>> group in rois ?? new Dictionary<string, List<CRectangleObject>>())
            {
                foreach (CRectangleObject roi in group.Value ?? new List<CRectangleObject>())
                {
                    if (roi == null || roi.Roi.IsEmpty)
                    {
                        continue;
                    }

                    string className = roi.cClassItem?.Text;
                    if (string.IsNullOrWhiteSpace(className))
                    {
                        className = group.Key;
                    }

                    className = string.IsNullOrWhiteSpace(className)
                        ? "Defect"
                        : ClassCatalogService.NormalizeClassName(className);
                    roi.cClassItem ??= new CClassItem();
                    roi.cClassItem.Text = className;

                    if (!result.TryGetValue(className, out List<CRectangleObject> list))
                    {
                        list = new List<CRectangleObject>();
                        result[className] = list;
                    }

                    list.Add(roi);
                }
            }

            return result;
        }

        private static void EnsureSegmentationClasses(
            CData data,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments)
        {
            if (data == null || segments == null)
            {
                return;
            }

            data.ClassNamedList ??= new List<CClassItem>();
            foreach (string className in segments
                .SelectMany(group => new[] { group.Key }
                    .Concat(group.Value?.Select(segment => segment?.ClassItem?.Text ?? segment?.ClassName ?? string.Empty)
                        ?? Enumerable.Empty<string>()))
                .Select(ClassCatalogService.NormalizeClassName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase))
            {
                if (data.ClassNamedList.Any(item => string.Equals(item.Text, className, System.StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ClassCatalogService.TryAddClass(data, className, out _);
            }
        }

        private static void EnsureRoiClasses(
            CData data,
            IReadOnlyDictionary<string, List<CRectangleObject>> rois)
        {
            if (data == null || rois == null)
            {
                return;
            }

            data.ClassNamedList ??= new List<CClassItem>();
            foreach (string className in rois
                .SelectMany(group => new[] { group.Key }
                    .Concat(group.Value?.Select(roi => roi?.cClassItem?.Text ?? string.Empty)
                        ?? Enumerable.Empty<string>()))
                .Select(name => string.IsNullOrWhiteSpace(name) ? "Defect" : ClassCatalogService.NormalizeClassName(name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase))
            {
                if (data.ClassNamedList.Any(item => string.Equals(item.Text, className, System.StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ClassCatalogService.TryAddClass(data, className, out _);
            }
        }
    }
}
