using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Loads saved annotation files into the Shell's live annotation state.
    /// File parsing stays with the existing YOLO services; this adapter owns
    /// the conversion to the in-memory collections and refresh admission.
    /// </summary>
    internal sealed class AnnotationLoadAdapter
    {
        private readonly AnnotationLoadAdapterContext context;

        internal AnnotationLoadAdapter(AnnotationLoadAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.ActiveImageSizeProvider);
            ArgumentNullException.ThrowIfNull(context.CurrentDatasetPurposeProvider);
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.ManualRois);
            ArgumentNullException.ThrowIfNull(context.ManualRoiClassNames);
            ArgumentNullException.ThrowIfNull(context.ManualRoiShapeKinds);
            ArgumentNullException.ThrowIfNull(context.ManualRoiOverlayIds);
            ArgumentNullException.ThrowIfNull(context.ManualSegments);
            ArgumentNullException.ThrowIfNull(context.ClassCatalogWorkflowService);
        }

        internal int LoadSavedBoxAnnotationsForActiveImage(string imagePath)
        {
            Size activeImageSize = context.ActiveImageSizeProvider();
            if (activeImageSize.IsEmpty
                || context.CurrentDatasetPurposeProvider() == LabelingDatasetPurpose.Segmentation)
            {
                return 0;
            }

            LabelingProjectData data = context.DataProvider();
            IReadOnlyDictionary<string, List<Rectangle>> savedBoxes =
                YoloAnnotationService.LoadAnnotationRectanglesForImage(
                    imagePath,
                    data?.ClassNamedList,
                    data,
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
                    context.ManualRois.Add(box);
                    context.ManualRoiClassNames.Add(className);
                    context.ManualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
                    context.ManualRoiOverlayIds.Add(string.Empty);
                    loadedCount++;
                }
            }

            if (loadedCount > 0)
            {
                context.RedrawReviewRois?.Invoke();
            }

            return loadedCount;
        }

        internal int LoadSavedSegmentationAnnotationsForActiveImage(string imagePath)
        {
            Size activeImageSize = context.ActiveImageSizeProvider();
            if (activeImageSize.IsEmpty
                || context.CurrentDatasetPurposeProvider() != LabelingDatasetPurpose.Segmentation)
            {
                return 0;
            }

            LabelingProjectData data = context.DataProvider();
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> savedSegments =
                YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(
                    imagePath,
                    data?.ClassNamedList,
                    data,
                    activeImageSize);
            if (savedSegments == null || savedSegments.Count == 0)
            {
                return 0;
            }

            var loadedSegments = new List<LabelingSegmentationObject>();
            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> classSegments in savedSegments)
            {
                LabelClass classItem = context.ClassCatalogWorkflowService.EnsureClassItem(data, classSegments.Key);
                foreach (LabelingSegmentationObject segment in classSegments.Value
                    ?? Enumerable.Empty<LabelingSegmentationObject>())
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

                    loadedSegments.Add(segment);
                }
            }

            foreach (LabelingSegmentationObject segment in loadedSegments
                .Select((segment, index) => new { Segment = segment, Index = index })
                .OrderBy(item => item.Segment.ZOrder)
                .ThenBy(item => item.Index)
                .Select(item => item.Segment))
            {
                context.ManualSegments.Add(segment);
            }
            int loadedCount = loadedSegments.Count;
            if (loadedCount > 0)
            {
                context.RefreshPolygonOverlays?.Invoke();
            }

            return loadedCount;
        }
    }

    internal sealed class AnnotationLoadAdapterContext
    {
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<LabelingDatasetPurpose> CurrentDatasetPurposeProvider { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal IList<Rectangle> ManualRois { get; init; }
        internal IList<string> ManualRoiClassNames { get; init; }
        internal IList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal IList<string> ManualRoiOverlayIds { get; init; }
        internal IList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
    }
}
