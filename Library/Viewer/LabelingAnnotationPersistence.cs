using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    internal static class LabelingAnnotationPersistence
    {
        public static bool SaveCurrent(
            LabelingImageSnapshot activeImage,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois,
            LabelingProjectData data)
        {
            return SaveCurrent(activeImage, rois, null, data);
        }

        public static bool SaveCurrent(
            LabelingImageSnapshot activeImage,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments,
            LabelingProjectData data)
            => SaveCurrentWithAdditionalArtifacts(activeImage, rois, segments, data, null);

        internal static bool SaveCurrentWithAdditionalArtifacts(
            LabelingImageSnapshot activeImage,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments,
            LabelingProjectData data,
            Func<bool> saveAdditionalArtifacts)
            => SaveImageAnnotations(
                activeImage?.ImageName,
                activeImage?.Image,
                rois,
                segments,
                data,
                sourceImagePath: activeImage?.ImagePath ?? string.Empty,
                saveAdditionalArtifacts: saveAdditionalArtifacts);

        internal static bool SaveImageAnnotations(
            string imageName,
            Image image,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments,
            LabelingProjectData data,
            string sourceImagePath,
            Func<bool> saveAdditionalArtifacts = null)
        {
            if (image == null || data == null || string.IsNullOrWhiteSpace(imageName))
            {
                return false;
            }

            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> normalizedRois = NormalizeRoisByClass(rois);
            List<LabelClass> originalClassItems = data.ClassNamedList?.ToList();
            bool hadClassList = data.ClassNamedList != null;
            EnsureRoiClasses(data, normalizedRois);
            EnsureSegmentationClasses(data, segments);
            try
            {
                bool committed = AnnotationFilePersistence.ExecuteTransaction(() =>
                {
                    YoloAnnotationService.SaveAnnotations(
                        imageName,
                        image,
                        normalizedRois,
                        data.ClassNamedList,
                        data,
                        sourceImagePath);
                    YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                        imageName,
                        image,
                        segments,
                        data.ClassNamedList,
                        data,
                        sourceImagePath);
                    bool additionalArtifactsSaved = saveAdditionalArtifacts?.Invoke() ?? true;
                    AppLog.COMM(
                        $"Annotation save additional artifacts: {additionalArtifactsSaved} / {imageName}");
                    return additionalArtifactsSaved;
                });

                if (!committed)
                {
                    AppLog.ABNORMAL($"Annotation save transaction returned false / {imageName}");
                    RestoreClassCatalog(data, originalClassItems, hadClassList);
                }

                return committed;
            }
            catch (YoloImageIdentityCollisionException ex)
            {
                AppLog.ABNORMAL($"Annotation save image identity collision / {imageName}: {ex.Message}");
                RestoreClassCatalog(data, originalClassItems, hadClassList);
                return false;
            }
            catch (InvalidDataException ex)
            {
                AppLog.ABNORMAL($"Annotation save invalid data / {imageName}: {ex.Message}");
                RestoreClassCatalog(data, originalClassItems, hadClassList);
                return false;
            }
            catch (IOException ex)
            {
                AppLog.ABNORMAL($"Annotation save I/O failure / {imageName}: {ex.Message}");
                RestoreClassCatalog(data, originalClassItems, hadClassList);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                AppLog.ABNORMAL($"Annotation save access failure / {imageName}: {ex.Message}");
                RestoreClassCatalog(data, originalClassItems, hadClassList);
                return false;
            }
            catch
            {
                RestoreClassCatalog(data, originalClassItems, hadClassList);
                throw;
            }
        }

        private static void RestoreClassCatalog(
            LabelingProjectData data,
            IReadOnlyList<LabelClass> originalClassItems,
            bool hadClassList)
        {
            if (!hadClassList)
            {
                data.ClassNamedList = null;
                return;
            }

            data.ClassNamedList ??= new List<LabelClass>();
            data.ClassNamedList.Clear();
            foreach (LabelClass classItem in originalClassItems ?? Array.Empty<LabelClass>())
            {
                data.ClassNamedList.Add(classItem);
            }
        }

        private static IReadOnlyDictionary<string, List<AnnotationRectangleObject>> NormalizeRoisByClass(
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois)
        {
            var result = new Dictionary<string, List<AnnotationRectangleObject>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<AnnotationRectangleObject>> group in rois ?? new Dictionary<string, List<AnnotationRectangleObject>>())
            {
                foreach (AnnotationRectangleObject roi in group.Value ?? new List<AnnotationRectangleObject>())
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
                    roi.cClassItem ??= new LabelClass();
                    roi.cClassItem.Text = className;

                    if (!result.TryGetValue(className, out List<AnnotationRectangleObject> list))
                    {
                        list = new List<AnnotationRectangleObject>();
                        result[className] = list;
                    }

                    list.Add(roi);
                }
            }

            return result;
        }

        private static void EnsureSegmentationClasses(
            LabelingProjectData data,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments)
        {
            if (data == null || segments == null)
            {
                return;
            }

            data.ClassNamedList ??= new List<LabelClass>();
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
            LabelingProjectData data,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois)
        {
            if (data == null || rois == null)
            {
                return;
            }

            data.ClassNamedList ??= new List<LabelClass>();
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
