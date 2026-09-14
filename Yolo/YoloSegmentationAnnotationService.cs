using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MvcVisionSystem.Yolo
{
    public static class YoloSegmentationAnnotationService
    {
        private const string PolygonGeometryType = "Polygon";
        private const string RasterMaskGeometryType = "RasterMask";
        private const int CanonicalSchemaVersion = 3;

        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static void SaveSegmentationAnnotations(
            string imageName,
            Image image,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data,
            string sourceImagePath = "")
        {
            if (string.IsNullOrWhiteSpace(imageName) || image == null || data == null)
            {
                return;
            }

            YoloAnnotationService.EnsureImageIdentity(imageName, image, data, sourceImagePath);
            bool writesSegmentation = data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation
                || segmentsByClass?.Values.Any(items => items?.Any(item => item != null) == true) == true;
            if (writesSegmentation)
            {
                EnsureExistingAnnotationsAreReadable(imageName, image.Size, classes, data);
            }

            AnnotationFilePersistence.ExecuteTransaction(
                () => SaveSegmentationAnnotationsCore(imageName, image, segmentsByClass, classes, data));
        }

        private static void EnsureExistingAnnotationsAreReadable(
            string imageName,
            Size imageSize,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data)
        {
            foreach (string segmentPath in GetCandidateSegmentPaths(imageName, data).Where(File.Exists))
            {
                LoadSegmentationObjects(
                    segmentPath,
                    ResolveSiblingMaskPath(segmentPath),
                    classes,
                    imageSize);
            }
        }

        private static void SaveSegmentationAnnotationsCore(
            string imageName,
            Image image,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data)
        {
            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                return;
            }

            IReadOnlyList<SegmentationPolygonRecord> polygons = BuildPolygonRecords(segmentsByClass, classes, image.Size);
            var targetModes = new HashSet<string>(
                YoloDatasetSplitService.SelectModesForImage(fileStem, data.ProjectSettings?.YoloDataset),
                StringComparer.OrdinalIgnoreCase);

            foreach (string mode in DatasetModes)
            {
                string maskDirectory = Path.Combine(data.OutputRootPath, "data", mode, "masks");
                string segmentDirectory = Path.Combine(data.OutputRootPath, "data", mode, "segments");
                Directory.CreateDirectory(maskDirectory);
                Directory.CreateDirectory(segmentDirectory);

                string maskPath = Path.Combine(maskDirectory, $"{fileStem}.png");
                string segmentPath = Path.Combine(segmentDirectory, $"{fileStem}.json");
                if (!targetModes.Contains(mode) || polygons.Count == 0)
                {
                    DeleteSegmentationFiles(maskPath, segmentPath);
                    continue;
                }

                SaveMask(maskPath, image.Size, segmentsByClass, classes);
                SaveSegmentJson(segmentPath, imageName, image.Size, polygons);
            }
        }

        public static IReadOnlyDictionary<string, List<List<Point>>> LoadSegmentationPolygonsForImage(
            string imagePath,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data,
            Size imageSize)
        {
            foreach (string segmentPath in GetCandidateSegmentPaths(imagePath, data))
            {
                if (File.Exists(segmentPath))
                {
                    YoloAnnotationService.EnsureAnnotationImageIdentity(imagePath, segmentPath);
                    return LoadSegmentationPolygons(segmentPath, classes, imageSize);
                }
            }

            return new Dictionary<string, List<List<Point>>>();
        }

        public static IReadOnlyDictionary<string, List<LabelingSegmentationObject>> LoadSegmentationObjectsForImage(
            string imagePath,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data,
            Size imageSize)
        {
            foreach (string segmentPath in GetCandidateSegmentPaths(imagePath, data))
            {
                if (File.Exists(segmentPath))
                {
                    YoloAnnotationService.EnsureAnnotationImageIdentity(imagePath, segmentPath);
                    return LoadSegmentationObjects(
                        segmentPath,
                        ResolveSiblingMaskPath(segmentPath),
                        classes,
                        imageSize);
                }
            }

            return new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyDictionary<string, List<List<Point>>> LoadSegmentationPolygons(
            string segmentPath,
            IReadOnlyList<LabelClass> classes,
            Size imageSize)
        {
            return LoadSegmentationObjects(segmentPath, string.Empty, classes, imageSize)
                .ToDictionary(
                    group => group.Key,
                    group => group.Value.Select(segment => new List<Point>(segment.Points)).ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyDictionary<string, List<LabelingSegmentationObject>> LoadSegmentationObjects(
            string segmentPath,
            IReadOnlyList<LabelClass> classes,
            Size imageSize)
            => LoadSegmentationObjects(segmentPath, ResolveSiblingMaskPath(segmentPath), classes, imageSize);

        public static IReadOnlyDictionary<string, List<LabelingSegmentationObject>> LoadSegmentationObjects(
            string segmentPath,
            string maskPath,
            IReadOnlyList<LabelClass> classes,
            Size imageSize)
        {
            var result = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(segmentPath) || !File.Exists(segmentPath))
            {
                return result;
            }

            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch (JsonException ex)
            {
                throw InvalidSegmentationSchema(segmentPath, "JSON cannot be parsed.", ex);
            }

            if (annotation == null)
            {
                throw InvalidSegmentationSchema(segmentPath, "The document root is null.");
            }

            if (annotation.Version < 1 || annotation.Version > CanonicalSchemaVersion)
            {
                throw InvalidSegmentationSchema(
                    segmentPath,
                    $"Schema version {annotation.Version} is not supported.");
            }

            if (annotation.Polygons == null)
            {
                throw InvalidSegmentationSchema(segmentPath, "The polygons collection is missing.");
            }

            Size targetSize;
            if (imageSize.Width > 0 && imageSize.Height > 0)
            {
                targetSize = imageSize;
            }
            else if (annotation.ImageWidth > 0 && annotation.ImageHeight > 0)
            {
                targetSize = new Size(annotation.ImageWidth, annotation.ImageHeight);
            }
            else
            {
                throw InvalidSegmentationSchema(segmentPath, "Image dimensions are missing or invalid.");
            }

            bool maskLoaded = TryLoadMaskClassValues(
                maskPath,
                targetSize,
                out byte[] maskClassValues,
                out string maskFailureReason);

            for (int recordIndex = 0; recordIndex < annotation.Polygons.Count; recordIndex++)
            {
                SegmentationPolygonRecord record = annotation.Polygons[recordIndex];
                if (record == null)
                {
                    throw InvalidSegmentationRecord(segmentPath, recordIndex, "The record is null.");
                }

                if (record.ClassIndex < 0 || classes?.Count > 0 && record.ClassIndex >= classes.Count)
                {
                    throw InvalidSegmentationRecord(segmentPath, recordIndex, "The class index is outside the active class catalog.");
                }

                string className = ResolveClassName(record, classes);
                if (string.IsNullOrWhiteSpace(className))
                {
                    throw InvalidSegmentationRecord(segmentPath, recordIndex, "The class name cannot be resolved.");
                }

                if (!string.IsNullOrWhiteSpace(record.GeometryType)
                    && !string.Equals(record.GeometryType, PolygonGeometryType, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(record.GeometryType, RasterMaskGeometryType, StringComparison.OrdinalIgnoreCase))
                {
                    throw InvalidSegmentationRecord(segmentPath, recordIndex, "The geometry type is not supported.");
                }

                if (record.Points == null || record.Points.Any(point => point == null))
                {
                    throw InvalidSegmentationRecord(segmentPath, recordIndex, "The points collection is missing or contains null points.");
                }

                List<Point> points = SegmentationGeometry.NormalizePolygon(
                    record.Points.Select(point => new Point(point.X, point.Y)),
                    targetSize,
                    minimumDistance: 1);
                if (points.Count < 3)
                {
                    throw InvalidSegmentationRecord(segmentPath, recordIndex, "The geometry has fewer than three valid points.");
                }

                var cutouts = new List<List<Point>>();
                foreach (List<SegmentationPointRecord> cutoutRecord in record.Cutouts ?? new List<List<SegmentationPointRecord>>())
                {
                    if (cutoutRecord == null || cutoutRecord.Any(point => point == null))
                    {
                        throw InvalidSegmentationRecord(segmentPath, recordIndex, "A cutout is null or contains null points.");
                    }

                    List<Point> cutout = SegmentationGeometry.NormalizePolygon(
                        cutoutRecord.Select(point => new Point(point.X, point.Y)),
                        targetSize,
                        minimumDistance: 1);
                    if (cutout.Count < 3)
                    {
                        throw InvalidSegmentationRecord(segmentPath, recordIndex, "A cutout has fewer than three valid points.");
                    }

                    cutouts.Add(cutout);
                }

                if (!result.ContainsKey(className))
                {
                    result.Add(className, new List<LabelingSegmentationObject>());
                }

                LabelClass classItem = ResolveClassItem(className, classes);
                bool isExplicitRaster = string.Equals(
                    record.GeometryType,
                    RasterMaskGeometryType,
                    StringComparison.OrdinalIgnoreCase);
                if (isExplicitRaster && !maskLoaded)
                {
                    throw InvalidSegmentationRecord(
                        segmentPath,
                        recordIndex,
                        "The explicit raster mask is unavailable or invalid: " + maskFailureReason);
                }

                if (TryBuildRasterMaskSegment(record, points, classItem, targetSize, maskClassValues, out LabelingSegmentationObject rasterSegment))
                {
                    ApplyCanonicalMetadata(rasterSegment, record, recordIndex);
                    result[className].Add(rasterSegment);
                    continue;
                }

                if (isExplicitRaster)
                {
                    throw InvalidSegmentationRecord(
                        segmentPath,
                        recordIndex,
                        "The explicit raster mask has no matching class pixels inside its declared geometry.");
                }

                var segment = new LabelingSegmentationObject(points, classItem)
                {
                    ClassName = className
                };
                ApplyCanonicalMetadata(segment, record, recordIndex);
                foreach (List<Point> cutout in cutouts)
                {
                    segment.CutoutPolygons.Add(cutout);
                }

                result[className].Add(segment);
            }

            return result;
        }

        private static InvalidDataException InvalidSegmentationSchema(
            string segmentPath,
            string reason,
            Exception innerException = null)
            => new InvalidDataException(
                $"Invalid segmentation annotation '{segmentPath}': {reason}",
                innerException);

        private static InvalidDataException InvalidSegmentationRecord(
            string segmentPath,
            int recordIndex,
            string reason)
            => InvalidSegmentationSchema(segmentPath, $"Record {recordIndex}: {reason}");

        // Kept internal so dry-run audits use the same legacy-mask compatibility rule as reopening and training export.
        internal static bool IsLegacyRasterMaskCandidate(SegmentationPolygonRecord record, Size imageSize)
        {
            if (record == null
                || imageSize.Width <= 0
                || imageSize.Height <= 0
                || !string.IsNullOrWhiteSpace(record.GeometryType)
                || (record.Cutouts != null && record.Cutouts.Count > 0)
                || record.Points == null)
            {
                return false;
            }

            List<Point> points = SegmentationGeometry.NormalizePolygon(
                record.Points.Select(point => new Point(point.X, point.Y)),
                imageSize,
                minimumDistance: 1);
            return IsAxisAlignedRectangle(points);
        }

        internal static bool TryBuildLegacyRasterMaskSegment(
            SegmentationPolygonRecord record,
            string maskPath,
            IReadOnlyList<LabelClass> classes,
            Size imageSize,
            out LabelingSegmentationObject segment)
        {
            segment = null;
            if (!IsLegacyRasterMaskCandidate(record, imageSize)
                || !TryLoadMaskClassValues(maskPath, imageSize, out byte[] maskClassValues))
            {
                return false;
            }

            string className = ResolveClassName(record, classes);
            if (string.IsNullOrWhiteSpace(className))
            {
                return false;
            }

            List<Point> points = SegmentationGeometry.NormalizePolygon(
                record.Points.Select(point => new Point(point.X, point.Y)),
                imageSize,
                minimumDistance: 1);
            bool built = TryBuildRasterMaskSegment(
                record,
                points,
                ResolveClassItem(className, classes),
                imageSize,
                maskClassValues,
                out segment);
            if (built)
            {
                ApplyCanonicalMetadata(segment, record, 0);
            }

            return built;
        }

        public static IReadOnlyList<SegmentationPolygonRecord> BuildPolygonRecords(
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<LabelClass> classes,
            Size imageSize)
        {
            var records = new List<SegmentationPolygonRecord>();
            if (segmentsByClass == null || classes == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return records;
            }

            for (int classIndex = 0; classIndex < classes.Count; classIndex++)
            {
                string className = classes[classIndex]?.Text ?? "";
                if (string.IsNullOrWhiteSpace(className) || !segmentsByClass.TryGetValue(className, out List<LabelingSegmentationObject> segments))
                {
                    continue;
                }

                foreach (LabelingSegmentationObject segment in segments.Where(item => item != null))
                {
                    string objectId = EnsureObjectId(segment);
                    if (segment.IsRasterMask)
                    {
                        records.AddRange(BuildRasterMaskPolygonRecords(
                            segment,
                            classIndex,
                            className,
                            imageSize,
                            objectId));

                        continue;
                    }

                    List<Point> points = SegmentationGeometry.NormalizePolygon(segment.Points, imageSize);
                    if (points.Count < 3)
                    {
                        continue;
                    }

                    records.Add(new SegmentationPolygonRecord
                    {
                        ClassIndex = classIndex,
                        ClassName = className,
                        GeometryType = PolygonGeometryType,
                        ObjectId = objectId,
                        ComponentIndex = Math.Max(0, segment.ComponentIndex),
                        ZOrder = segment.ZOrder,
                        LastStructuralOperation = segment.LastStructuralOperation ?? string.Empty,
                        Points = points.Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y }).ToList(),
                        Cutouts = NormalizeCutouts(segment.CutoutPolygons, imageSize)
                            .Select(cutout => cutout.Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y }).ToList())
                            .ToList()
                    });
                }
            }

            return records;
        }

        private static IReadOnlyList<SegmentationPolygonRecord> BuildRasterMaskPolygonRecords(
            LabelingSegmentationObject segment,
            int classIndex,
            string className,
            Size imageSize,
            string objectId)
        {
            Rectangle bounds = segment?.Bounds ?? Rectangle.Empty;
            if (bounds.IsEmpty)
            {
                return Array.Empty<SegmentationPolygonRecord>();
            }

            List<SegmentationGeometry.SegmentationMaskRegion> regions = RasterMaskPolygonService.BuildRegions(
                segment.MaskData,
                segment.MaskSize,
                imageSize,
                bounds).ToList();
            if (regions.Count == 0)
            {
                List<Point> fallback = SegmentationGeometry.RectangleToPolygon(bounds, imageSize);
                if (fallback.Count < 3)
                {
                    return Array.Empty<SegmentationPolygonRecord>();
                }

                regions.Add(new SegmentationGeometry.SegmentationMaskRegion { Points = fallback });
            }

            List<SegmentationGeometry.SegmentationMaskRegion> validRegions = regions
                .Where(region => region?.Points?.Count >= 3)
                .ToList();
            int firstComponentIndex = segment.ComponentIndex >= 0
                ? segment.ComponentIndex
                : 0;
            return validRegions
                .Select((region, regionIndex) => new SegmentationPolygonRecord
                {
                    ClassIndex = classIndex,
                    ClassName = className,
                    GeometryType = RasterMaskGeometryType,
                    ObjectId = objectId,
                    ComponentIndex = firstComponentIndex + regionIndex,
                    ZOrder = segment.ZOrder,
                    LastStructuralOperation = segment.LastStructuralOperation ?? string.Empty,
                    Points = region.Points
                        .Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y })
                        .ToList(),
                    Cutouts = NormalizeCutouts(region.Cutouts, imageSize)
                        .Select(cutout => cutout.Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y }).ToList())
                        .ToList()
                })
                .ToList();
        }

        private static string EnsureObjectId(LabelingSegmentationObject segment)
        {
            if (segment == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(segment.ObjectId))
            {
                segment.ObjectId = Guid.NewGuid().ToString("N");
            }

            return segment.ObjectId;
        }

        private static void ApplyCanonicalMetadata(
            LabelingSegmentationObject segment,
            SegmentationPolygonRecord record,
            int recordIndex)
        {
            if (segment == null)
            {
                return;
            }

            segment.ObjectId = string.IsNullOrWhiteSpace(record?.ObjectId)
                ? $"legacy-{Math.Max(0, recordIndex):D6}"
                : record.ObjectId.Trim();
            segment.ComponentIndex = (record?.ComponentIndex ?? -1) >= 0
                ? record.ComponentIndex
                : 0;
            segment.ZOrder = record?.ZOrder ?? 0;
            segment.LastStructuralOperation = record?.LastStructuralOperation ?? string.Empty;
        }

        private static bool TryBuildRasterMaskSegment(
            SegmentationPolygonRecord record,
            IReadOnlyList<Point> points,
            LabelClass classItem,
            Size imageSize,
            byte[] maskClassValues,
            out LabelingSegmentationObject segment)
        {
            segment = null;
            bool isExplicitRaster = string.Equals(record?.GeometryType, RasterMaskGeometryType, StringComparison.OrdinalIgnoreCase);
            bool isLegacyRasterCandidate = string.IsNullOrWhiteSpace(record?.GeometryType)
                && (record?.Cutouts == null || record.Cutouts.Count == 0)
                && IsAxisAlignedRectangle(points);
            if ((!isExplicitRaster && !isLegacyRasterCandidate)
                || maskClassValues == null
                || maskClassValues.Length != imageSize.Width * imageSize.Height)
            {
                return false;
            }

            Rectangle bounds = Rectangle.Intersect(
                SegmentationGeometry.GetBounds(points),
                new Rectangle(Point.Empty, imageSize));
            if (bounds.IsEmpty)
            {
                return false;
            }

            int classValue = Math.Clamp((record?.ClassIndex ?? 0) + 1, 1, 255);
            var maskData = new byte[imageSize.Width * imageSize.Height];
            int left = imageSize.Width;
            int top = imageSize.Height;
            int right = -1;
            int bottom = -1;
            int pixelCount = 0;
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    int index = rowOffset + x;
                    if (maskClassValues[index] != classValue)
                    {
                        continue;
                    }

                    maskData[index] = 255;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                    pixelCount++;
                }
            }

            if (pixelCount == 0)
            {
                return false;
            }

            Rectangle maskBounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            // Version 1 did not record geometry type. When its sibling class-index mask
            // exists, that mask is authoritative even when it is a solid rectangle;
            // otherwise adding another class can change the same label from polygon to mask.

            segment = new LabelingSegmentationObject
            {
                ClassItem = classItem,
                ClassName = classItem?.Text ?? record?.ClassName ?? string.Empty,
                MaskData = maskData,
                MaskSize = imageSize,
                MaskBounds = maskBounds
            };
            return true;
        }

        private static bool IsAxisAlignedRectangle(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count != 4)
            {
                return false;
            }

            int left = points.Min(point => point.X);
            int right = points.Max(point => point.X);
            int top = points.Min(point => point.Y);
            int bottom = points.Max(point => point.Y);
            var corners = new HashSet<Point>(points);
            return left < right
                && top < bottom
                && corners.SetEquals(new[]
                {
                    new Point(left, top),
                    new Point(right, top),
                    new Point(right, bottom),
                    new Point(left, bottom)
                });
        }

        private static bool TryLoadMaskClassValues(string maskPath, Size imageSize, out byte[] classValues)
            => TryLoadMaskClassValues(maskPath, imageSize, out classValues, out _);

        private static bool TryLoadMaskClassValues(
            string maskPath,
            Size imageSize,
            out byte[] classValues,
            out string failureReason)
        {
            classValues = null;
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(maskPath) || !File.Exists(maskPath))
            {
                failureReason = "the sibling mask file does not exist";
                return false;
            }

            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                failureReason = "the target image dimensions are invalid";
                return false;
            }

            try
            {
                using var source = new Bitmap(maskPath);
                if (source.Size != imageSize)
                {
                    failureReason = "the mask dimensions do not match the image";
                    return false;
                }

                Rectangle bounds = new Rectangle(Point.Empty, imageSize);
                using Bitmap normalized = source.Clone(bounds, PixelFormat.Format24bppRgb);
                BitmapData bitmapData = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int stride = Math.Abs(bitmapData.Stride);
                    var pixels = new byte[stride * imageSize.Height];
                    Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
                    classValues = new byte[imageSize.Width * imageSize.Height];
                    for (int y = 0; y < imageSize.Height; y++)
                    {
                        int sourceRow = bitmapData.Stride >= 0 ? y : imageSize.Height - 1 - y;
                        int sourceOffset = sourceRow * stride;
                        int targetOffset = y * imageSize.Width;
                        for (int x = 0; x < imageSize.Width; x++)
                        {
                            int pixelOffset = sourceOffset + (x * 3);
                            byte blue = pixels[pixelOffset];
                            if (pixels[pixelOffset + 1] != blue || pixels[pixelOffset + 2] != blue)
                            {
                                classValues = null;
                                failureReason = "the mask contains non-grayscale pixels";
                                return false;
                            }

                            classValues[targetOffset + x] = blue;
                        }
                    }
                }
                finally
                {
                    normalized.UnlockBits(bitmapData);
                }

                return true;
            }
            catch (ArgumentException ex)
            {
                failureReason = ex.Message;
                return false;
            }
            catch (ExternalException ex)
            {
                failureReason = ex.Message;
                return false;
            }
            catch (IOException ex)
            {
                failureReason = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                failureReason = ex.Message;
                return false;
            }
        }

        private static string ResolveSiblingMaskPath(string segmentPath)
        {
            DirectoryInfo segmentDirectory = Directory.GetParent(segmentPath ?? string.Empty);
            if (segmentDirectory == null
                || !string.Equals(segmentDirectory.Name, "segments", StringComparison.OrdinalIgnoreCase)
                || segmentDirectory.Parent == null)
            {
                return string.Empty;
            }

            string fileStem = Path.GetFileNameWithoutExtension(segmentPath);
            return string.IsNullOrWhiteSpace(fileStem)
                ? string.Empty
                : Path.Combine(segmentDirectory.Parent.FullName, "masks", $"{fileStem}.png");
        }

        public static IEnumerable<string> GetCandidateSegmentPaths(string imagePath, LabelingProjectData data)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                yield break;
            }

            string fileStem = Path.GetFileNameWithoutExtension(imagePath);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                yield break;
            }

            var emittedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (data != null)
            {
                data.NormalizeOutputPaths();
                string outputRootPath = data.OutputRootPath;
                if (!string.IsNullOrWhiteSpace(outputRootPath))
                {
                    foreach (string mode in DatasetModes)
                    {
                        string datasetSegmentPath = Path.Combine(outputRootPath, "data", mode, "segments", $"{fileStem}.json");
                        if (emittedPaths.Add(datasetSegmentPath))
                        {
                            yield return datasetSegmentPath;
                        }
                    }

                    if (IsPathUnderDirectory(imagePath, outputRootPath))
                    {
                        string outputSiblingSegmentPath = ResolveSiblingSegmentPath(imagePath, fileStem);
                        if (!string.IsNullOrWhiteSpace(outputSiblingSegmentPath) && emittedPaths.Add(outputSiblingSegmentPath))
                        {
                            yield return outputSiblingSegmentPath;
                        }

                        string outputSidecarSegmentPath = Path.Combine(Path.GetDirectoryName(imagePath) ?? string.Empty, $"{fileStem}.segments.json");
                        if (!string.IsNullOrWhiteSpace(outputSidecarSegmentPath) && emittedPaths.Add(outputSidecarSegmentPath))
                        {
                            yield return outputSidecarSegmentPath;
                        }
                    }

                    yield break;
                }
            }

            string siblingSegmentPath = ResolveSiblingSegmentPath(imagePath, fileStem);
            if (!string.IsNullOrWhiteSpace(siblingSegmentPath) && emittedPaths.Add(siblingSegmentPath))
            {
                yield return siblingSegmentPath;
            }

            string sidecarSegmentPath = Path.Combine(Path.GetDirectoryName(imagePath) ?? string.Empty, $"{fileStem}.segments.json");
            if (!string.IsNullOrWhiteSpace(sidecarSegmentPath) && emittedPaths.Add(sidecarSegmentPath))
            {
                yield return sidecarSegmentPath;
            }
        }

        private static string ResolveSiblingSegmentPath(string imagePath, string fileStem)
        {
            DirectoryInfo imageDirectory = Directory.GetParent(imagePath);
            if (imageDirectory != null && string.Equals(imageDirectory.Name, "images", StringComparison.OrdinalIgnoreCase))
            {
                string siblingSegmentDirectory = Path.Combine(imageDirectory.Parent?.FullName ?? imageDirectory.FullName, "segments");
                return Path.Combine(siblingSegmentDirectory, $"{fileStem}.json");
            }

            return string.Empty;
        }

        private static bool IsPathUnderDirectory(string path, string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootDirectory))
            {
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullRoot = Path.GetFullPath(rootDirectory);
                if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    && !fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    fullRoot += Path.DirectorySeparatorChar;
                }

                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static void SaveMask(string maskPath, Size imageSize, IReadOnlyList<SegmentationPolygonRecord> polygons)
        {
            using (var mask = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb))
            using (Graphics graphics = Graphics.FromImage(mask))
            {
                graphics.Clear(Color.Black);
                foreach (SegmentationPolygonRecord polygon in polygons)
                {
                    if (polygon.Points == null || polygon.Points.Count < 3)
                    {
                        continue;
                    }

                    int value = Math.Clamp(polygon.ClassIndex + 1, 1, 255);
                    using (var brush = new SolidBrush(Color.FromArgb(value, value, value)))
                    {
                        graphics.FillPolygon(brush, polygon.Points.Select(point => new Point(point.X, point.Y)).ToArray());
                    }

                    foreach (List<SegmentationPointRecord> cutout in polygon.Cutouts ?? new List<List<SegmentationPointRecord>>())
                    {
                        if (cutout == null || cutout.Count < 3)
                        {
                            continue;
                        }

                        using var eraseBrush = new SolidBrush(Color.Black);
                        graphics.FillPolygon(eraseBrush, cutout.Select(point => new Point(point.X, point.Y)).ToArray());
                    }
                }

                AnnotationFilePersistence.WriteAtomically(
                    maskPath,
                    temporaryPath => mask.Save(temporaryPath, ImageFormat.Png));
            }
        }

        private static void SaveMask(
            string maskPath,
            Size imageSize,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<LabelClass> classes)
        {
            using (var mask = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(mask))
                {
                    graphics.Clear(Color.Black);
                }

                if (segmentsByClass != null && classes != null)
                {
                    for (int classIndex = 0; classIndex < classes.Count; classIndex++)
                    {
                        string className = classes[classIndex]?.Text ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(className)
                            || !segmentsByClass.TryGetValue(className, out List<LabelingSegmentationObject> segments))
                        {
                            continue;
                        }

                        int value = Math.Clamp(classIndex + 1, 1, 255);
                        Color classColor = Color.FromArgb(value, value, value);
                        foreach (LabelingSegmentationObject segment in segments.Where(item => item != null))
                        {
                            if (segment.IsRasterMask)
                            {
                                WriteRasterMask(mask, segment, imageSize, (byte)value);
                                continue;
                            }

                            using (Graphics graphics = Graphics.FromImage(mask))
                            using (var fillBrush = new SolidBrush(classColor))
                            {
                                if (segment.Points?.Count >= 3)
                                {
                                    graphics.FillPolygon(fillBrush, segment.Points.ToArray());
                                }

                                using var eraseBrush = new SolidBrush(Color.Black);
                                foreach (List<Point> cutout in segment.CutoutPolygons ?? new List<List<Point>>())
                                {
                                    if (cutout?.Count >= 3)
                                    {
                                        graphics.FillPolygon(eraseBrush, cutout.ToArray());
                                    }
                                }
                            }
                        }
                    }
                }

                AnnotationFilePersistence.WriteAtomically(
                    maskPath,
                    temporaryPath => mask.Save(temporaryPath, ImageFormat.Png));
            }
        }

        private static void WriteRasterMask(
            Bitmap target,
            LabelingSegmentationObject segment,
            Size imageSize,
            byte classValue)
        {
            Rectangle bounds = Rectangle.Intersect(
                segment.Bounds,
                new Rectangle(Point.Empty, imageSize));
            bounds = Rectangle.Intersect(bounds, new Rectangle(Point.Empty, segment.MaskSize));
            if (bounds.IsEmpty)
            {
                return;
            }

            byte[] classPixels = new byte[bounds.Width * 3];
            Array.Fill(classPixels, classValue);
            Rectangle targetBounds = new Rectangle(Point.Empty, imageSize);
            BitmapData bitmapData = target.LockBits(targetBounds, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    int sourceRowOffset = y * segment.MaskSize.Width;
                    int targetRow = bitmapData.Stride >= 0 ? y : imageSize.Height - 1 - y;
                    int x = bounds.Left;
                    while (x < bounds.Right)
                    {
                        while (x < bounds.Right && segment.MaskData[sourceRowOffset + x] == 0)
                        {
                            x++;
                        }

                        int runStart = x;
                        while (x < bounds.Right && segment.MaskData[sourceRowOffset + x] != 0)
                        {
                            x++;
                        }

                        int runLength = x - runStart;
                        if (runLength > 0)
                        {
                            IntPtr targetAddress = IntPtr.Add(
                                bitmapData.Scan0,
                                (targetRow * Math.Abs(bitmapData.Stride)) + (runStart * 3));
                            Marshal.Copy(classPixels, 0, targetAddress, runLength * 3);
                        }
                    }
                }
            }
            finally
            {
                target.UnlockBits(bitmapData);
            }
        }

        private static void SaveSegmentJson(
            string segmentPath,
            string imageName,
            Size imageSize,
            IReadOnlyList<SegmentationPolygonRecord> polygons)
        {
            var annotation = new SegmentationAnnotationFile
            {
                Version = CanonicalSchemaVersion,
                ImageName = imageName,
                ImageWidth = imageSize.Width,
                ImageHeight = imageSize.Height,
                Polygons = polygons.ToList()
            };

            AnnotationFilePersistence.WriteAtomically(
                segmentPath,
                temporaryPath => File.WriteAllText(
                    temporaryPath,
                    JsonConvert.SerializeObject(annotation, Formatting.Indented)));
        }

        private static string ResolveClassName(SegmentationPolygonRecord record, IReadOnlyList<LabelClass> classes)
        {
            if (!string.IsNullOrWhiteSpace(record.ClassName))
            {
                return record.ClassName;
            }

            return record.ClassIndex >= 0 && classes != null && record.ClassIndex < classes.Count
                ? classes[record.ClassIndex]?.Text ?? string.Empty
                : string.Empty;
        }

        private static LabelClass ResolveClassItem(string className, IReadOnlyList<LabelClass> classes)
        {
            return classes?.FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase))
                ?? new LabelClass { Text = className, DrawColor = Color.LimeGreen };
        }

        private static IReadOnlyList<List<Point>> NormalizeCutouts(IEnumerable<IEnumerable<Point>> cutouts, Size imageSize)
        {
            return (cutouts ?? Enumerable.Empty<IEnumerable<Point>>())
                .Select(cutout => SegmentationGeometry.NormalizePolygon(cutout, imageSize, minimumDistance: 1, simplificationTolerance: 0.75D))
                .Where(cutout => cutout.Count >= 3)
                .ToList();
        }

        private static void DeleteSegmentationFiles(string maskPath, string segmentPath)
        {
            if (File.Exists(maskPath))
            {
                AnnotationFilePersistence.Delete(maskPath);
            }

            if (File.Exists(segmentPath))
            {
                AnnotationFilePersistence.Delete(segmentPath);
            }
        }
    }

    public sealed class SegmentationAnnotationFile
    {
        public int Version { get; set; } = 1;
        public string ImageName { get; set; } = "";
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public List<SegmentationPolygonRecord> Polygons { get; set; } = new List<SegmentationPolygonRecord>();
    }

    public sealed class SegmentationPolygonRecord
    {
        public int ClassIndex { get; set; }
        public string ClassName { get; set; } = "";
        public string GeometryType { get; set; } = "";
        public string ObjectId { get; set; } = "";
        public int ComponentIndex { get; set; } = -1;
        public int ZOrder { get; set; }
        public string LastStructuralOperation { get; set; } = "";
        public List<SegmentationPointRecord> Points { get; set; } = new List<SegmentationPointRecord>();
        public List<List<SegmentationPointRecord>> Cutouts { get; set; } = new List<List<SegmentationPointRecord>>();
    }

    public sealed class SegmentationPointRecord
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
