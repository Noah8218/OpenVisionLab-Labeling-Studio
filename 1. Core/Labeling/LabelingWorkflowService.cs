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
        private readonly LabelingImageWorkspace imageWorkspace;

        public LabelingWorkflowService(LabelingImageWorkspace imageWorkspace)
        {
            this.imageWorkspace = imageWorkspace ?? throw new ArgumentNullException(nameof(imageWorkspace));
        }

        public void ApplySelectedClass(LabelClass classItem)
        {
            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            mainDisplay?.SetSelectedClass(classItem);
        }

        public IReadOnlyList<LabelingRoiListItem> GetMainRoiItems()
        {
            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.GetRoiListItems() ?? new List<LabelingRoiListItem>();
        }

        public int GetMainSelectedRoiListIndex()
        {
            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.SelectedAnnotationListIndex ?? -1;
        }

        public bool SelectMainRoiItem(int listIndex)
        {
            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.SelectAnnotationListItem(listIndex) == true;
        }

        public bool DeleteMainSelectedAnnotation()
        {
            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            return mainDisplay?.DeleteSelectedAnnotation() == true;
        }

        public bool CommitCurrentAnnotations(AnnotationViewer viewer, LabelingProjectData data, ApplicationRuntimeState system)
            => CommitCurrentAnnotations(viewer, CaptureCurrentImage(), data, system);

        public bool CommitCurrentAnnotations(
            AnnotationViewer viewer,
            LabelingImageSnapshot activeImage,
            LabelingProjectData data,
            ApplicationRuntimeState system)
        {
            if (viewer == null)
            {
                return false;
            }

            bool saved = LabelingAnnotationPersistence.SaveCurrent(
                activeImage,
                viewer.RoiByClass,
                viewer.SegmentsByClass,
                data);
            if (saved)
            {
                LogAnnotationSave(
                    activeImage,
                    data,
                    CountRoiObjects(viewer.RoiByClass) + CountSegmentObjects(viewer.SegmentsByClass));
                system?.UpdateData();
            }

            return saved;
        }

        public bool CommitMainAnnotations(LabelingProjectData data, ApplicationRuntimeState system)
        {
            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            return CommitDisplayAnnotations(mainDisplay, CaptureCurrentImage(), data, system);
        }

        public bool CommitDisplayAnnotations(DisplayLayerDocument display, LabelingProjectData data, ApplicationRuntimeState system)
            => CommitDisplayAnnotations(display, CaptureCurrentImage(), data, system);

        public bool CommitDisplayAnnotations(
            DisplayLayerDocument display,
            LabelingImageSnapshot activeImage,
            LabelingProjectData data,
            ApplicationRuntimeState system)
        {
            if (display == null)
            {
                return false;
            }

            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois = display.GetRoiByClass();
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments = display.GetSegmentsByClass();
            bool saved = LabelingAnnotationPersistence.SaveCurrent(activeImage, rois, segments, data);
            if (saved)
            {
                LogAnnotationSave(activeImage, data, CountRoiObjects(rois) + CountSegmentObjects(segments));
                system?.UpdateData();
            }

            return saved;
        }

        private static void LogAnnotationSave(
            LabelingImageSnapshot activeImage,
            LabelingProjectData data,
            int objectCount)
        {
            if (activeImage == null || data == null)
            {
                return;
            }

            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(activeImage.ImageName, data);
            string pathText = labelPaths.Count == 0
                ? "(저장 경로 없음)"
                : string.Join(", ", labelPaths.Select(path => Path.GetFileName(path)));
            AppLog.NORMAL($"라벨 저장 완료. 이미지:{activeImage.ImageName}, 객체:{Math.Max(0, objectCount)}, 파일:{pathText}");
        }

        private LabelingImageSnapshot CaptureCurrentImage()
            => imageWorkspace.CaptureSnapshot();

        private static int CountRoiObjects(IReadOnlyDictionary<string, List<AnnotationRectangleObject>> rois)
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

        public bool LoadSavedAnnotationsToMainDisplay(string imagePath, Size imageSize, LabelingProjectData data)
        {
            if (data == null)
            {
                return false;
            }

            DisplayLayerDocument mainDisplay = DisplayManager.GetMainDisplayOrNull();
            if (mainDisplay == null)
            {
                return false;
            }

            mainDisplay.ResetAnnotations();
            YoloAnnotationLoadResult annotationLoad = YoloAnnotationService.LoadAnnotationRectanglesForImageWithDiagnostics(
                imagePath,
                data.ClassNamedList,
                data,
                imageSize);
            if (annotationLoad.HasErrors)
            {
                return false;
            }

            IReadOnlyDictionary<string, List<Rectangle>> annotations = annotationLoad.Annotations;
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
                LabelClass classItem = data.ClassNamedList
                    .FirstOrDefault(item => string.Equals(item.Text, annotation.Key, System.StringComparison.OrdinalIgnoreCase))
                    ?? new LabelClass { Text = annotation.Key, DrawColor = Color.LimeGreen };

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
