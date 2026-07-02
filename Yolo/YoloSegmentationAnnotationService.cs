using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloSegmentationAnnotationService
    {
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
            IReadOnlyList<CClassItem> classes,
            CData data)
        {
            if (string.IsNullOrWhiteSpace(imageName) || image == null || data == null)
            {
                return;
            }

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
            IReadOnlyList<CClassItem> classes,
            CData data,
            Size imageSize)
        {
            foreach (string segmentPath in GetCandidateSegmentPaths(imagePath, data))
            {
                if (File.Exists(segmentPath))
                {
                    return LoadSegmentationPolygons(segmentPath, classes, imageSize);
                }
            }

            return new Dictionary<string, List<List<Point>>>();
        }

        public static IReadOnlyDictionary<string, List<LabelingSegmentationObject>> LoadSegmentationObjectsForImage(
            string imagePath,
            IReadOnlyList<CClassItem> classes,
            CData data,
            Size imageSize)
        {
            foreach (string segmentPath in GetCandidateSegmentPaths(imagePath, data))
            {
                if (File.Exists(segmentPath))
                {
                    return LoadSegmentationObjects(segmentPath, classes, imageSize);
                }
            }

            return new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyDictionary<string, List<List<Point>>> LoadSegmentationPolygons(
            string segmentPath,
            IReadOnlyList<CClassItem> classes,
            Size imageSize)
        {
            return LoadSegmentationObjects(segmentPath, classes, imageSize)
                .ToDictionary(
                    group => group.Key,
                    group => group.Value.Select(segment => new List<Point>(segment.Points)).ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyDictionary<string, List<LabelingSegmentationObject>> LoadSegmentationObjects(
            string segmentPath,
            IReadOnlyList<CClassItem> classes,
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
            catch
            {
                return result;
            }

            if (annotation?.Polygons == null)
            {
                return result;
            }

            Size targetSize = imageSize.Width > 0 && imageSize.Height > 0
                ? imageSize
                : new Size(Math.Max(1, annotation.ImageWidth), Math.Max(1, annotation.ImageHeight));

            foreach (SegmentationPolygonRecord record in annotation.Polygons)
            {
                string className = ResolveClassName(record, classes);
                if (string.IsNullOrWhiteSpace(className) || record.Points == null)
                {
                    continue;
                }

                List<Point> points = SegmentationGeometry.NormalizePolygon(
                    record.Points.Select(point => new Point(point.X, point.Y)),
                    targetSize,
                    minimumDistance: 1);
                if (points.Count < 3)
                {
                    continue;
                }

                if (!result.ContainsKey(className))
                {
                    result.Add(className, new List<LabelingSegmentationObject>());
                }

                CClassItem classItem = ResolveClassItem(className, classes);
                var segment = new LabelingSegmentationObject(points, classItem)
                {
                    ClassName = className
                };
                foreach (List<SegmentationPointRecord> cutoutRecord in record.Cutouts ?? new List<List<SegmentationPointRecord>>())
                {
                    List<Point> cutout = SegmentationGeometry.NormalizePolygon(
                        cutoutRecord?.Select(point => new Point(point.X, point.Y)),
                        targetSize,
                        minimumDistance: 1);
                    if (cutout.Count >= 3)
                    {
                        segment.CutoutPolygons.Add(cutout);
                    }
                }

                result[className].Add(segment);
            }

            return result;
        }

        public static IReadOnlyList<SegmentationPolygonRecord> BuildPolygonRecords(
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<CClassItem> classes,
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
                    if (segment.IsRasterMask)
                    {
                        SegmentationPolygonRecord maskRecord = BuildRasterMaskPolygonRecord(segment, classIndex, className, imageSize);
                        if (maskRecord != null)
                        {
                            records.Add(maskRecord);
                        }

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
                        Points = points.Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y }).ToList(),
                        Cutouts = NormalizeCutouts(segment.CutoutPolygons, imageSize)
                            .Select(cutout => cutout.Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y }).ToList())
                            .ToList()
                    });
                }
            }

            return records;
        }

        private static SegmentationPolygonRecord BuildRasterMaskPolygonRecord(
            LabelingSegmentationObject segment,
            int classIndex,
            string className,
            Size imageSize)
        {
            Rectangle bounds = segment?.Bounds ?? Rectangle.Empty;
            if (bounds.IsEmpty)
            {
                return null;
            }

            List<Point> points = SegmentationGeometry.RectangleToPolygon(bounds, imageSize);
            if (points.Count < 3)
            {
                return null;
            }

            return new SegmentationPolygonRecord
            {
                ClassIndex = classIndex,
                ClassName = className,
                Points = points.Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y }).ToList(),
                Cutouts = new List<List<SegmentationPointRecord>>()
            };
        }

        public static IEnumerable<string> GetCandidateSegmentPaths(string imagePath, CData data)
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
            if (File.Exists(maskPath))
            {
                File.Delete(maskPath);
            }

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

                mask.Save(maskPath, ImageFormat.Png);
            }
        }

        private static void SaveMask(
            string maskPath,
            Size imageSize,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            IReadOnlyList<CClassItem> classes)
        {
            if (File.Exists(maskPath))
            {
                File.Delete(maskPath);
            }

            using (var mask = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb))
            using (Graphics graphics = Graphics.FromImage(mask))
            {
                graphics.Clear(Color.Black);
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
                        using var fillBrush = new SolidBrush(classColor);
                        using var eraseBrush = new SolidBrush(Color.Black);
                        foreach (LabelingSegmentationObject segment in segments.Where(item => item != null))
                        {
                            if (segment.IsRasterMask)
                            {
                                Rectangle bounds = segment.Bounds;
                                for (int y = bounds.Top; y < bounds.Bottom && y < imageSize.Height; y++)
                                {
                                    int rowOffset = y * segment.MaskSize.Width;
                                    for (int x = bounds.Left; x < bounds.Right && x < imageSize.Width; x++)
                                    {
                                        if (segment.MaskData[rowOffset + x] != 0)
                                        {
                                            mask.SetPixel(x, y, classColor);
                                        }
                                    }
                                }

                                continue;
                            }

                            if (segment.Points?.Count >= 3)
                            {
                                graphics.FillPolygon(fillBrush, segment.Points.ToArray());
                            }

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

                mask.Save(maskPath, ImageFormat.Png);
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
                Version = polygons.Any(polygon => polygon.Cutouts != null && polygon.Cutouts.Count > 0) ? 2 : 1,
                ImageName = imageName,
                ImageWidth = imageSize.Width,
                ImageHeight = imageSize.Height,
                Polygons = polygons.ToList()
            };

            File.WriteAllText(segmentPath, JsonConvert.SerializeObject(annotation, Formatting.Indented));
        }

        private static string ResolveClassName(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (!string.IsNullOrWhiteSpace(record.ClassName))
            {
                return record.ClassName;
            }

            return record.ClassIndex >= 0 && classes != null && record.ClassIndex < classes.Count
                ? classes[record.ClassIndex]?.Text ?? string.Empty
                : string.Empty;
        }

        private static CClassItem ResolveClassItem(string className, IReadOnlyList<CClassItem> classes)
        {
            return classes?.FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase))
                ?? new CClassItem { Text = className, DrawColor = Color.LimeGreen };
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
                File.Delete(maskPath);
            }

            if (File.Exists(segmentPath))
            {
                File.Delete(segmentPath);
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
        public List<SegmentationPointRecord> Points { get; set; } = new List<SegmentationPointRecord>();
        public List<List<SegmentationPointRecord>> Cutouts { get; set; } = new List<List<SegmentationPointRecord>>();
    }

    public sealed class SegmentationPointRecord
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
