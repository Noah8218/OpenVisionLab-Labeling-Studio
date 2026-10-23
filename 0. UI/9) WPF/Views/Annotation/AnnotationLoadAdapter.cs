using MvcVisionSystem.DrawObject;
using MvcVisionSystem._1._Core;
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

        internal bool TryReadSavedGroundTruth(string imagePath, out IReadOnlyList<IReadOnlyList<Point>> outlines, out string statusKey, out IReadOnlyList<IReadOnlyList<IReadOnlyList<Point>>> polygonFills)
        {
            outlines = Array.Empty<IReadOnlyList<Point>>();
            polygonFills = Array.Empty<IReadOnlyList<IReadOnlyList<Point>>>();
            statusKey = "WpfCanvas.Reference.GT.Unavailable";
            Size size = context.ActiveImageSizeProvider();
            LabelingProjectData source = context.DataProvider();
            LabelingDatasetPurpose purpose = context.CurrentDatasetPurposeProvider();
            if (source == null || size.IsEmpty || string.IsNullOrWhiteSpace(imagePath)
                || purpose != LabelingDatasetPurpose.ObjectDetection && purpose != LabelingDatasetPurpose.Segmentation) return false;
            // Candidate path lookup normalizes its input. Keep that mutation off the
            // live Recipe and never use the editable annotation collections as GT.
            var snapshot = new LabelingProjectData
            {
                OutputDataYamlPath = source.OutputDataYamlPath,
                OutputDataImageAndTxtPath = source.OutputDataImageAndTxtPath,
                ProjectSettings = new LabelingProjectSettings { DatasetPurpose = purpose },
                ClassNamedList = source.ClassNamedList?.Select(item => item == null ? null : new LabelClass
                {
                    Text = item.Text, DrawColor = item.DrawColor, IsArchived = item.IsArchived
                }).ToList()
            };
            try
            {
                string path = FindSavedReferenceFile(purpose == LabelingDatasetPurpose.Segmentation
                    ? YoloSegmentationAnnotationService.GetCandidateSegmentPaths(imagePath, snapshot)
                    : YoloAnnotationService.GetCandidateLabelPaths(imagePath, snapshot));
                if (string.IsNullOrEmpty(path)) { statusKey = "WpfCanvas.Reference.GT.Empty"; return true; }
                string before = ReadReferenceFileIdentity(path);
                var result = new List<IReadOnlyList<Point>>();
                var fills = new List<IReadOnlyList<IReadOnlyList<Point>>>();
                if (purpose == LabelingDatasetPurpose.Segmentation)
                {
                    var saved = YoloSegmentationAnnotationService.LoadSavedPolygonReferencesForImage(imagePath, snapshot.ClassNamedList, snapshot, size, path);
                    foreach (LabelingSegmentationObject segment in saved.Values.SelectMany(items => items))
                    {
                        if (segment.IsRasterMask) continue;
                        var contours = new List<IReadOnlyList<Point>> { segment.Points.ToArray() };
                        contours.AddRange(segment.CutoutPolygons.Select(hole => (IReadOnlyList<Point>)hole.ToArray()));
                        result.AddRange(contours);
                        fills.Add(contours);
                    }
                }
                else
                {
                    YoloAnnotationLoadResult saved = YoloAnnotationService.LoadAnnotationRectanglesForImageWithDiagnostics(imagePath, snapshot.ClassNamedList, snapshot, size);
                    if (saved.HasErrors) return false;
                    if (!string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(saved.LabelPath), StringComparison.OrdinalIgnoreCase))
                    { statusKey = "WpfCanvas.Reference.GT.Changed"; return false; }
                    foreach (Rectangle box in saved.Annotations.Values.SelectMany(items => items))
                        result.Add(new[] { new Point(box.Left, box.Top), new Point(box.Right, box.Top), new Point(box.Right, box.Bottom), new Point(box.Left, box.Bottom) });
                }
                if (!string.Equals(before, ReadReferenceFileIdentity(path), StringComparison.Ordinal))
                { statusKey = "WpfCanvas.Reference.GT.Changed"; return false; }
                outlines = result;
                polygonFills = fills;
                statusKey = result.Count == 0 ? "WpfCanvas.Reference.GT.Empty" : "WpfCanvas.Reference.GT.Ready";
                return true;
            }
            catch (YoloImageIdentityCollisionException) { statusKey = "WpfCanvas.Reference.SourceUnknown"; return false; }
            catch (Exception error) when (error is System.IO.IOException || error is UnauthorizedAccessException
                || error is System.IO.InvalidDataException || error is Newtonsoft.Json.JsonException || error is ArgumentException)
            { return false; }
        }

        private static string FindSavedReferenceFile(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if ((System.IO.File.GetAttributes(path) & System.IO.FileAttributes.Directory) == 0) return path;
                }
                catch (System.IO.FileNotFoundException) { }
                catch (System.IO.DirectoryNotFoundException) { }
            }
            return string.Empty;
        }

        private static string ReadReferenceFileIdentity(string path)
        {
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
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
            YoloAnnotationLoadResult loadResult =
                YoloAnnotationService.LoadAnnotationRectanglesForImageWithDiagnostics(
                    imagePath,
                    data?.ClassNamedList,
                    data,
                    activeImageSize);
            if (loadResult.HasErrors)
            {
                context.BlockSaveForMalformedLoad?.Invoke(
                    loadResult.LabelPath,
                    loadResult.ErrorSummary);
                return 0;
            }

            context.ClearSaveBlock?.Invoke();
            IReadOnlyDictionary<string, List<Rectangle>> savedBoxes = loadResult.Annotations;
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
        internal Action<string, string> BlockSaveForMalformedLoad { get; init; }
        internal Action ClearSaveBlock { get; init; }
    }
}
