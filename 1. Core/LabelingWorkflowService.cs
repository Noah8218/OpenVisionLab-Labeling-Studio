using Lib.Common;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class LabelingWorkflowService
    {
        public void ApplySelectedClass(CClassItem classItem)
        {
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            mainDisplay?.SetSelectedClass(classItem);
        }

        public IReadOnlyList<LabelingRoiListItem> GetMainRoiItems()
        {
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.GetRoiListItems() ?? new List<LabelingRoiListItem>();
        }

        public int GetMainSelectedRoiListIndex()
        {
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.SelectedAnnotationListIndex ?? -1;
        }

        public bool SelectMainRoiItem(int listIndex)
        {
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.SelectAnnotationListItem(listIndex) == true;
        }

        public bool DeleteMainSelectedAnnotation()
        {
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.DeleteSelectedAnnotation() == true;
        }

        public bool CommitCurrentAnnotations(CViewer viewer, CData data, CSystem system)
        {
            if (viewer == null)
            {
                return false;
            }

            bool saved = LabelingAnnotationPersistence.SaveCurrent(viewer.CurrentImage, viewer.RoiByClass, viewer.SegmentsByClass, data);
            if (saved)
            {
                LogAnnotationSaveReadable(data, CountRoiObjects(viewer.RoiByClass) + CountSegmentObjects(viewer.SegmentsByClass));
                system?.UpdateData();
            }

            return saved;
        }

        public bool CommitMainAnnotations(CData data, CSystem system)
        {
            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            return CommitDisplayAnnotations(mainDisplay, data, system);
        }

        public bool CommitDisplayAnnotations(DisplayLayerDocument display, CData data, CSystem system)
        {
            if (display == null)
            {
                return false;
            }

            IReadOnlyDictionary<string, List<CRectangleObject>> rois = display.GetRoiByClass();
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments = display.GetSegmentsByClass();
            bool saved = LabelingAnnotationPersistence.SaveCurrent(display.GetCurrentImage(), rois, segments, data);
            if (saved)
            {
                LogAnnotationSaveReadable(data, CountRoiObjects(rois) + CountSegmentObjects(segments));
                system?.UpdateData();
            }

            return saved;
        }

        private static void LogAnnotationSave(CData data, int objectCount)
        {
            if (data == null)
            {
                return;
            }

            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(data.LastSelectImageName, data);
            string pathText = labelPaths.Count == 0
                ? "(저장 경로 없음)"
                : string.Join(", ", labelPaths.Select(path => Path.GetFileName(path)));
            AppLog.NORMAL($"라벨 저장 완료. 이미지:{data.LastSelectImageName}, 객체:{Math.Max(0, objectCount)}, 파일:{pathText}");
        }

        private static void LogAnnotationSaveReadable(CData data, int objectCount)
        {
            if (data == null)
            {
                return;
            }

            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(data.LastSelectImageName, data);
            string pathText = labelPaths.Count == 0
                ? "(저장 경로 없음)"
                : string.Join(", ", labelPaths.Select(path => Path.GetFileName(path)));
            AppLog.NORMAL($"라벨 저장 완료. 이미지:{data.LastSelectImageName}, 객체:{Math.Max(0, objectCount)}, 파일:{pathText}");
        }

        private static int CountRoiObjects(IReadOnlyDictionary<string, List<CRectangleObject>> rois)
        {
            if (rois == null)
            {
                return 0;
            }

            return rois.Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(item => item != null && !item.Roi.IsEmpty);
        }

        private static int CountSegmentObjects(IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments)
        {
            if (segments == null)
            {
                return 0;
            }

            return segments.Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(item => item?.Points != null && item.Points.Count >= 3);
        }

        public bool LoadSavedAnnotationsToMainDisplay(string imagePath, Size imageSize, CData data)
        {
            if (data == null)
            {
                return false;
            }

            DisplayLayerDocument mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            if (mainDisplay == null)
            {
                return false;
            }

            mainDisplay.ResetAnnotations();
            IReadOnlyDictionary<string, List<Rectangle>> annotations = YoloAnnotationService.LoadAnnotationRectanglesForImage(
                imagePath,
                data.ClassNamedList,
                data,
                imageSize);
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments = YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(
                imagePath,
                data.ClassNamedList,
                data,
                imageSize);

            if (annotations.Count == 0 && segments.Count == 0)
            {
                return false;
            }

            foreach (KeyValuePair<string, List<Rectangle>> annotation in annotations)
            {
                CClassItem classItem = data.ClassNamedList
                    .FirstOrDefault(item => string.Equals(item.Text, annotation.Key, System.StringComparison.OrdinalIgnoreCase))
                    ?? new CClassItem { Text = annotation.Key, DrawColor = Color.LimeGreen };

                mainDisplay.SetRoiRectangles(annotation.Value, classItem, reset: false);
            }

            if (segments.Count > 0)
            {
                mainDisplay.SetSegmentationObjects(segments, data.ClassNamedList, reset: false);
            }

            return true;
        }
    }
}
