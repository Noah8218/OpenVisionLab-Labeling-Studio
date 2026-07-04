using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private int LoadSavedBoxAnnotationsForActiveImage(string imagePath)
        {
            if (activeImageSize.IsEmpty || GetCurrentDatasetPurpose() == LabelingDatasetPurpose.Segmentation)
            {
                return 0;
            }

            IReadOnlyDictionary<string, List<Rectangle>> savedBoxes = YoloAnnotationService.LoadAnnotationRectanglesForImage(
                imagePath,
                global.Data.ClassNamedList,
                global.Data,
                activeImageSize);
            if (savedBoxes == null || savedBoxes.Count == 0)
            {
                return 0;
            }

            int loadedCount = 0;
            foreach (KeyValuePair<string, List<Rectangle>> classBoxes in savedBoxes)
            {
                string className = ClassCatalogService.NormalizeClassName(classBoxes.Key);
                if (string.IsNullOrWhiteSpace(className) || classBoxes.Value == null)
                {
                    continue;
                }

                foreach (Rectangle box in classBoxes.Value.Where(item => !item.IsEmpty))
                {
                    manualRois.Add(box);
                    manualRoiClassNames.Add(className);
                    manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
                    manualRoiOverlayIds.Add(string.Empty);
                    loadedCount++;
                }
            }

            if (loadedCount > 0)
            {
                // Sample/opened datasets must show their saved YOLO boxes immediately;
                // otherwise a prepared object-detection project looks empty to a first-time user.
                RedrawReviewRois();
            }

            return loadedCount;
        }

        private int LoadSavedSegmentationAnnotationsForActiveImage(string imagePath)
        {
            if (activeImageSize.IsEmpty || !IsSegmentationDatasetPurposeActive())
            {
                return 0;
            }

            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> savedSegments =
                YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(
                    imagePath,
                    global.Data.ClassNamedList,
                    global.Data,
                    activeImageSize);
            if (savedSegments == null || savedSegments.Count == 0)
            {
                return 0;
            }

            int loadedCount = 0;
            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> classSegments in savedSegments)
            {
                CClassItem classItem = EnsureClassItem(classSegments.Key);
                foreach (LabelingSegmentationObject segment in classSegments.Value ?? Enumerable.Empty<LabelingSegmentationObject>())
                {
                    if (segment == null)
                    {
                        continue;
                    }

                    segment.ClassName = classItem?.Text ?? "Defect";
                    segment.ClassItem = classItem;
                    if (segment.IsRasterMask && segment.RenderVersion <= 0)
                    {
                        segment.RenderVersion = 1;
                        segment.RenderDirtyBounds = segment.Bounds;
                    }

                    manualSegments.Add(segment);
                    loadedCount++;
                }
            }

            if (loadedCount > 0)
            {
                RefreshPolygonOverlays();
            }

            return loadedCount;
        }
    }
}
