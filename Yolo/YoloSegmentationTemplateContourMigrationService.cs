using MvcVisionSystem._1._Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloSegmentationTemplateContourMigrationPlan
    {
        public string OutputRootPath { get; internal set; } = string.Empty;

        public string SourceImagePath { get; internal set; } = string.Empty;

        public string SourceSegmentPath { get; internal set; } = string.Empty;

        public string SourceMaskPath { get; internal set; } = string.Empty;

        public string SourceClassName { get; internal set; } = string.Empty;

        public int SourceClassIndex { get; internal set; } = -1;

        public int SourceMaskPixelCount { get; internal set; }

        public string BackupRootPath { get; internal set; } = string.Empty;

        public List<YoloSegmentationTemplateContourMigrationItem> Items { get; } = new List<YoloSegmentationTemplateContourMigrationItem>();

        public List<string> Errors { get; } = new List<string>();

        public bool CanApply => Errors.Count == 0 && Items.Count > 0;

        internal CData Data { get; set; }

        internal byte[] SourceMaskData { get; set; }

        internal Size SourceMaskSize { get; set; }

        internal Rectangle SourceMaskBounds { get; set; }

        internal Dictionary<string, string> SourceArtifactHashes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class YoloSegmentationTemplateContourMigrationItem
    {
        public string Split { get; internal set; } = string.Empty;

        public string FileStem { get; internal set; } = string.Empty;

        public string ImagePath { get; internal set; } = string.Empty;

        public string SegmentPath { get; internal set; } = string.Empty;

        public string MaskPath { get; internal set; } = string.Empty;

        public string LabelPath { get; internal set; } = string.Empty;

        public Rectangle TargetBounds { get; internal set; }

        public int ClassIndex { get; internal set; } = -1;

        public string ClassName { get; internal set; } = string.Empty;

        public int OriginalClassPixelCount { get; internal set; }

        public string SegmentSha256 { get; internal set; } = string.Empty;

        public string MaskSha256 { get; internal set; } = string.Empty;

        public string LabelSha256 { get; internal set; } = string.Empty;
    }

    public sealed class YoloSegmentationTemplateContourMigrationResult
    {
        public string BackupRootPath { get; internal set; } = string.Empty;

        public string ManifestPath { get; internal set; } = string.Empty;

        public int MigratedImageCount { get; internal set; }

        public int MigratedRecordCount { get; internal set; }
    }

    public static class YoloSegmentationTemplateContourMigrationService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloSegmentationTemplateContourMigrationPlan BuildPlan(
            CData data,
            string sourceImagePath,
            string sourceClassName,
            string backupRootPath)
        {
            var plan = new YoloSegmentationTemplateContourMigrationPlan
            {
                Data = data
            };
            if (data == null)
            {
                plan.Errors.Add("Dataset configuration is missing.");
                return plan;
            }

            if (string.IsNullOrWhiteSpace(data.OutputRootPath) || !Directory.Exists(data.OutputRootPath))
            {
                plan.Errors.Add("Dataset output root is missing.");
                return plan;
            }

            try
            {
                plan.OutputRootPath = Path.GetFullPath(data.OutputRootPath);
                plan.SourceImagePath = Path.GetFullPath(sourceImagePath ?? string.Empty);
                plan.BackupRootPath = Path.GetFullPath(backupRootPath ?? string.Empty);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                plan.Errors.Add("Migration paths are invalid: " + ex.Message);
                return plan;
            }

            if (!File.Exists(plan.SourceImagePath) || !IsPathWithin(plan.SourceImagePath, plan.OutputRootPath))
            {
                plan.Errors.Add("The source image must be an existing image inside the active dataset root.");
                return plan;
            }

            string backupParent = Path.Combine(plan.OutputRootPath, "backups");
            if (string.IsNullOrWhiteSpace(plan.BackupRootPath) || !IsPathWithin(plan.BackupRootPath, backupParent))
            {
                plan.Errors.Add("The migration backup folder must stay under the active dataset backups folder.");
                return plan;
            }

            plan.SourceClassIndex = FindClassIndex(sourceClassName, data.ClassNamedList);
            if (plan.SourceClassIndex < 0)
            {
                plan.Errors.Add("The requested source class does not exist in the active dataset class catalog: " + sourceClassName);
                return plan;
            }

            plan.SourceClassName = data.ClassNamedList[plan.SourceClassIndex]?.Text ?? sourceClassName?.Trim() ?? string.Empty;
            DatasetArtifact source = FindDatasetArtifactByImagePath(plan.OutputRootPath, plan.SourceImagePath);
            if (source == null)
            {
                plan.Errors.Add("The source image is not registered in a train, valid, or test dataset split.");
                return plan;
            }

            plan.SourceSegmentPath = source.SegmentPath;
            plan.SourceMaskPath = source.MaskPath;
            if (!File.Exists(source.SegmentPath) || !File.Exists(source.MaskPath))
            {
                plan.Errors.Add("The source image needs both saved SEG JSON and mask PNG artifacts.");
                return plan;
            }

            try
            {
                SegmentationAnnotationFile sourceAnnotation = ReadAnnotation(source.SegmentPath);
                List<SegmentationPolygonRecord> sourceRecords = (sourceAnnotation.Polygons ?? new List<SegmentationPolygonRecord>())
                    .Where(record => ResolveClassIndex(record, data.ClassNamedList) == plan.SourceClassIndex)
                    .ToList();
                if (sourceRecords.Count != 1)
                {
                    plan.Errors.Add("The source image must contain exactly one saved object for the approved class.");
                    return plan;
                }

                Size sourceImageSize = GetImageSize(source.ImagePath, sourceAnnotation);
                byte[] sourceClassValues = ReadMaskClassValues(source.MaskPath, sourceImageSize);
                byte sourceClassValue = GetMaskClassValue(plan.SourceClassIndex);
                plan.SourceMaskData = sourceClassValues
                    .Select(value => value == sourceClassValue ? (byte)1 : (byte)0)
                    .ToArray();
                plan.SourceMaskSize = sourceImageSize;
                plan.SourceMaskBounds = GetMaskBounds(plan.SourceMaskData, sourceImageSize, 1);
                plan.SourceMaskPixelCount = plan.SourceMaskData.Count(value => value != 0);
                if (plan.SourceMaskBounds.IsEmpty || plan.SourceMaskPixelCount == 0)
                {
                    plan.Errors.Add("The approved source class has no raster mask pixels.");
                    return plan;
                }

                AddHash(plan.SourceArtifactHashes, source.ImagePath);
                AddHash(plan.SourceArtifactHashes, source.SegmentPath);
                AddHash(plan.SourceArtifactHashes, source.MaskPath);
                if (File.Exists(source.LabelPath))
                {
                    AddHash(plan.SourceArtifactHashes, source.LabelPath);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is JsonException)
            {
                plan.Errors.Add("The source SEG artifacts cannot be read: " + ex.Message);
                return plan;
            }

            foreach (DatasetArtifact target in EnumerateDatasetArtifacts(plan.OutputRootPath))
            {
                if (PathsEqual(target.ImagePath, plan.SourceImagePath) || !File.Exists(target.SegmentPath))
                {
                    continue;
                }

                try
                {
                    SegmentationAnnotationFile annotation = ReadAnnotation(target.SegmentPath);
                    Size imageSize = GetImageSize(target.ImagePath, annotation);
                    List<SegmentationPolygonRecord> classRecords = (annotation.Polygons ?? new List<SegmentationPolygonRecord>())
                        .Where(record => ResolveClassIndex(record, data.ClassNamedList) == plan.SourceClassIndex)
                        .ToList();
                    List<SegmentationPolygonRecord> legacyRecords = classRecords
                        .Where(record => YoloSegmentationAnnotationService.IsLegacyRasterMaskCandidate(record, imageSize))
                        .ToList();
                    if (legacyRecords.Count == 0)
                    {
                        continue;
                    }

                    if (legacyRecords.Count != 1 || classRecords.Count != 1)
                    {
                        plan.Errors.Add(target.Split + "/" + target.FileStem + ": the approved class must have exactly one legacy rectangle record.");
                        continue;
                    }

                    if (!File.Exists(target.MaskPath) || !File.Exists(target.LabelPath))
                    {
                        plan.Errors.Add(target.Split + "/" + target.FileStem + ": segment JSON, mask PNG, and YOLO label must all exist before migration.");
                        continue;
                    }

                    byte[] targetMaskValues = ReadMaskClassValues(target.MaskPath, imageSize);
                    Rectangle targetBounds = SegmentationGeometry.GetBounds(
                        legacyRecords[0].Points.Select(point => new Point(point.X, point.Y)));
                    byte classValue = GetMaskClassValue(plan.SourceClassIndex);
                    int targetPixelCount = targetMaskValues.Count(value => value == classValue);
                    if (targetBounds.IsEmpty || targetPixelCount == 0 || HasClassPixelsOutsideBounds(targetMaskValues, imageSize, classValue, targetBounds))
                    {
                        plan.Errors.Add(target.Split + "/" + target.FileStem + ": the saved class mask is not confined to its legacy rectangle.");
                        continue;
                    }

                    plan.Items.Add(new YoloSegmentationTemplateContourMigrationItem
                    {
                        Split = target.Split,
                        FileStem = target.FileStem,
                        ImagePath = target.ImagePath,
                        SegmentPath = target.SegmentPath,
                        MaskPath = target.MaskPath,
                        LabelPath = target.LabelPath,
                        TargetBounds = targetBounds,
                        ClassIndex = plan.SourceClassIndex,
                        ClassName = plan.SourceClassName,
                        OriginalClassPixelCount = targetPixelCount,
                        SegmentSha256 = ComputeSha256(target.SegmentPath),
                        MaskSha256 = ComputeSha256(target.MaskPath),
                        LabelSha256 = ComputeSha256(target.LabelPath)
                    });
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is JsonException)
                {
                    plan.Errors.Add(target.Split + "/" + target.FileStem + ": migration preflight failed: " + ex.Message);
                }
            }

            plan.Items.Sort((left, right) => string.Compare(
                left.Split + "/" + left.FileStem,
                right.Split + "/" + right.FileStem,
                StringComparison.OrdinalIgnoreCase));
            if (plan.Items.Count == 0 && plan.Errors.Count == 0)
            {
                plan.Errors.Add("No approved legacy rectangle masks were found for the requested source class.");
            }

            return plan;
        }

        public static YoloSegmentationTemplateContourMigrationResult Apply(YoloSegmentationTemplateContourMigrationPlan plan)
        {
            if (plan == null || !plan.CanApply)
            {
                throw new InvalidOperationException("Migration preflight must complete without errors before applying changes.");
            }

            if (Directory.Exists(plan.BackupRootPath))
            {
                throw new InvalidOperationException("Migration backup folder already exists and will not be reused: " + plan.BackupRootPath);
            }

            VerifyPlanInputsUnchanged(plan);
            Directory.CreateDirectory(plan.BackupRootPath);
            string stagingRoot = Path.Combine(plan.BackupRootPath, "staging");
            string manifestPath = Path.Combine(plan.BackupRootPath, "migration-manifest.json");
            var payloads = new List<MigrationPayload>();
            bool originalsBackedUp = false;

            try
            {
                foreach (YoloSegmentationTemplateContourMigrationItem item in plan.Items)
                {
                    payloads.Add(BuildPayload(plan, item, stagingRoot));
                }

                BackupOriginalArtifacts(plan);
                originalsBackedUp = true;
                WriteManifest(plan, payloads, manifestPath, "Prepared", string.Empty);

                foreach (MigrationPayload payload in payloads)
                {
                    ReplaceFileFromStage(payload.StagedSegmentPath, payload.Item.SegmentPath);
                    ReplaceFileFromStage(payload.StagedMaskPath, payload.Item.MaskPath);
                    ReplaceFileFromStage(payload.StagedLabelPath, payload.Item.LabelPath);
                }

                foreach (MigrationPayload payload in payloads)
                {
                    VerifyAppliedPayload(plan, payload);
                }
                VerifySourceArtifactsUnchanged(plan);
                WriteManifest(plan, payloads, manifestPath, "Applied", string.Empty);

                return new YoloSegmentationTemplateContourMigrationResult
                {
                    BackupRootPath = plan.BackupRootPath,
                    ManifestPath = manifestPath,
                    MigratedImageCount = payloads.Count,
                    MigratedRecordCount = payloads.Count
                };
            }
            catch (Exception ex)
            {
                if (originalsBackedUp)
                {
                    try
                    {
                        RestoreOriginalArtifacts(plan);
                        WriteManifest(plan, payloads, manifestPath, "RolledBack", ex.Message);
                    }
                    catch (Exception restoreEx)
                    {
                        throw new InvalidOperationException("SEG contour migration failed and automatic rollback also failed. Backup: " + plan.BackupRootPath, new AggregateException(ex, restoreEx));
                    }
                }

                throw;
            }
        }

        private static MigrationPayload BuildPayload(
            YoloSegmentationTemplateContourMigrationPlan plan,
            YoloSegmentationTemplateContourMigrationItem item,
            string stagingRoot)
        {
            VerifyItemInputsUnchanged(item);
            SegmentationAnnotationFile annotation = ReadAnnotation(item.SegmentPath);
            Size imageSize = GetImageSize(item.ImagePath, annotation);
            List<SegmentationPolygonRecord> legacyRecords = (annotation.Polygons ?? new List<SegmentationPolygonRecord>())
                .Where(record => ResolveClassIndex(record, plan.Data.ClassNamedList) == item.ClassIndex)
                .Where(record => YoloSegmentationAnnotationService.IsLegacyRasterMaskCandidate(record, imageSize))
                .Where(record => SegmentationGeometry.GetBounds(record.Points.Select(point => new Point(point.X, point.Y))) == item.TargetBounds)
                .ToList();
            if (legacyRecords.Count != 1)
            {
                throw new InvalidOperationException(item.Split + "/" + item.FileStem + ": legacy target changed after preflight.");
            }

            IReadOnlyList<LabelingSegmentationObject> translated = TemplateMatchingBatchAutoLabelService.BuildTranslatedSourceMaskSegments(
                plan.Data.ClassNamedList[item.ClassIndex],
                item.ClassName,
                item.TargetBounds,
                imageSize,
                plan.SourceMaskData,
                plan.SourceMaskSize,
                plan.SourceMaskBounds);
            if (translated.Count != 1 || translated[0]?.MaskData == null)
            {
                throw new InvalidOperationException(item.Split + "/" + item.FileStem + ": source contour could not be translated into the target bounds.");
            }

            IReadOnlyList<SegmentationPolygonRecord> replacement = YoloSegmentationAnnotationService.BuildPolygonRecords(
                new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase)
                {
                    [item.ClassName] = translated.ToList()
                },
                plan.Data.ClassNamedList,
                imageSize);
            if (replacement.Count == 0 || replacement.Any(record => !string.Equals(record.GeometryType, "RasterMask", StringComparison.OrdinalIgnoreCase) || record.Points.Count <= 4))
            {
                throw new InvalidOperationException(item.Split + "/" + item.FileStem + ": translated contour did not produce an explicit raster contour record.");
            }

            SegmentationPolygonRecord legacyRecord = legacyRecords[0];
            annotation.Polygons = (annotation.Polygons ?? new List<SegmentationPolygonRecord>())
                .SelectMany(record => ReferenceEquals(record, legacyRecord) ? replacement : new[] { record })
                .ToList();

            byte[] originalMaskValues = ReadMaskClassValues(item.MaskPath, imageSize);
            byte[] updatedMaskValues = BuildUpdatedMaskValues(
                originalMaskValues,
                imageSize,
                GetMaskClassValue(item.ClassIndex),
                item.TargetBounds,
                translated[0].MaskData);
            if (updatedMaskValues.Count(value => value == GetMaskClassValue(item.ClassIndex)) == 0)
            {
                throw new InvalidOperationException(item.Split + "/" + item.FileStem + ": translated contour removed every target-class mask pixel.");
            }

            string itemStageRoot = Path.Combine(stagingRoot, item.Split, item.FileStem);
            Directory.CreateDirectory(itemStageRoot);
            string stagedSegmentPath = Path.Combine(itemStageRoot, "segment.json");
            string stagedMaskPath = Path.Combine(itemStageRoot, "mask.png");
            string stagedLabelPath = Path.Combine(itemStageRoot, "label.txt");
            File.WriteAllText(stagedSegmentPath, JsonConvert.SerializeObject(annotation, Formatting.Indented), new UTF8Encoding(false));
            File.WriteAllBytes(stagedMaskPath, BuildMaskPng(updatedMaskValues, imageSize));

            IReadOnlyList<string> labelLines = YoloSegmentationTrainingLabelService.BuildReadOnlyLabelLines(
                stagedSegmentPath,
                stagedMaskPath,
                plan.Data.ClassNamedList,
                item.ImagePath,
                out IReadOnlyList<string> labelErrors);
            if (labelErrors.Count > 0 || labelLines.Count == 0)
            {
                throw new InvalidOperationException(item.Split + "/" + item.FileStem + ": staged YOLO SEG label could not be generated: " + string.Join(" | ", labelErrors));
            }

            File.WriteAllLines(stagedLabelPath, labelLines, new UTF8Encoding(false));
            return new MigrationPayload
            {
                Item = item,
                StagedSegmentPath = stagedSegmentPath,
                StagedMaskPath = stagedMaskPath,
                StagedLabelPath = stagedLabelPath,
                ExpectedMaskValues = updatedMaskValues,
                ExpectedLabelLines = labelLines.ToList(),
                SegmentSha256 = ComputeSha256(stagedSegmentPath),
                MaskSha256 = ComputeSha256(stagedMaskPath),
                LabelSha256 = ComputeSha256(stagedLabelPath)
            };
        }

        private static byte[] BuildUpdatedMaskValues(
            byte[] originalMaskValues,
            Size imageSize,
            byte classValue,
            Rectangle targetBounds,
            byte[] translatedMaskData)
        {
            if (originalMaskValues == null || originalMaskValues.Length != imageSize.Width * imageSize.Height
                || translatedMaskData == null || translatedMaskData.Length != imageSize.Width * imageSize.Height)
            {
                throw new InvalidOperationException("The source or target raster mask does not match the target image size.");
            }

            byte[] updated = originalMaskValues.ToArray();
            Rectangle clippedBounds = Rectangle.Intersect(targetBounds, new Rectangle(Point.Empty, imageSize));
            for (int y = clippedBounds.Top; y < clippedBounds.Bottom; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = clippedBounds.Left; x < clippedBounds.Right; x++)
                {
                    int index = rowOffset + x;
                    if (updated[index] == classValue)
                    {
                        updated[index] = 0;
                    }
                }
            }

            for (int index = 0; index < translatedMaskData.Length; index++)
            {
                if (translatedMaskData[index] != 0 && updated[index] == 0)
                {
                    updated[index] = classValue;
                }
            }

            return updated;
        }

        private static void BackupOriginalArtifacts(YoloSegmentationTemplateContourMigrationPlan plan)
        {
            foreach (YoloSegmentationTemplateContourMigrationItem item in plan.Items)
            {
                BackupFile(plan, item.SegmentPath);
                BackupFile(plan, item.MaskPath);
                BackupFile(plan, item.LabelPath);
            }
        }

        private static void RestoreOriginalArtifacts(YoloSegmentationTemplateContourMigrationPlan plan)
        {
            foreach (YoloSegmentationTemplateContourMigrationItem item in plan.Items)
            {
                RestoreFile(plan, item.SegmentPath);
                RestoreFile(plan, item.MaskPath);
                RestoreFile(plan, item.LabelPath);
            }
        }

        private static void BackupFile(YoloSegmentationTemplateContourMigrationPlan plan, string sourcePath)
        {
            string backupPath = GetBackupPath(plan, sourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? plan.BackupRootPath);
            File.Copy(sourcePath, backupPath, overwrite: false);
        }

        private static void RestoreFile(YoloSegmentationTemplateContourMigrationPlan plan, string targetPath)
        {
            string backupPath = GetBackupPath(plan, targetPath);
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Migration backup file is missing.", backupPath);
            }

            File.Copy(backupPath, targetPath, overwrite: true);
        }

        private static string GetBackupPath(YoloSegmentationTemplateContourMigrationPlan plan, string sourcePath)
        {
            string relativePath = Path.GetRelativePath(plan.OutputRootPath, sourcePath);
            if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            {
                throw new InvalidOperationException("Migration artifact escaped the active dataset root.");
            }

            return Path.Combine(plan.BackupRootPath, "originals", relativePath);
        }

        private static void ReplaceFileFromStage(string stagedPath, string targetPath)
        {
            string temporaryPath = targetPath + ".seg-contour-migration-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(stagedPath, temporaryPath, overwrite: true);
                File.Move(temporaryPath, targetPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void VerifyAppliedPayload(YoloSegmentationTemplateContourMigrationPlan plan, MigrationPayload payload)
        {
            if (!string.Equals(ComputeSha256(payload.Item.SegmentPath), payload.SegmentSha256, StringComparison.Ordinal)
                || !string.Equals(ComputeSha256(payload.Item.MaskPath), payload.MaskSha256, StringComparison.Ordinal)
                || !string.Equals(ComputeSha256(payload.Item.LabelPath), payload.LabelSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(payload.Item.Split + "/" + payload.Item.FileStem + ": applied artifact hash does not match staged output.");
            }

            SegmentationAnnotationFile annotation = ReadAnnotation(payload.Item.SegmentPath);
            List<SegmentationPolygonRecord> rasterRecords = (annotation.Polygons ?? new List<SegmentationPolygonRecord>())
                .Where(record => ResolveClassIndex(record, plan.Data.ClassNamedList) == payload.Item.ClassIndex)
                .Where(record => string.Equals(record.GeometryType, "RasterMask", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (rasterRecords.Count != 1 || rasterRecords[0].Points.Count <= 4)
            {
                throw new InvalidOperationException(payload.Item.Split + "/" + payload.Item.FileStem + ": applied JSON does not contain one contour-based raster record.");
            }

            Size imageSize = GetImageSize(payload.Item.ImagePath, annotation);
            byte[] currentMaskValues = ReadMaskClassValues(payload.Item.MaskPath, imageSize);
            if (!currentMaskValues.SequenceEqual(payload.ExpectedMaskValues))
            {
                throw new InvalidOperationException(payload.Item.Split + "/" + payload.Item.FileStem + ": applied mask differs from staged contour output.");
            }

            IReadOnlyList<string> actualLabelLines = YoloSegmentationTrainingLabelService.BuildReadOnlyLabelLines(
                payload.Item.SegmentPath,
                payload.Item.MaskPath,
                plan.Data.ClassNamedList,
                payload.Item.ImagePath,
                out IReadOnlyList<string> labelErrors);
            if (labelErrors.Count > 0 || !actualLabelLines.SequenceEqual(payload.ExpectedLabelLines, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(payload.Item.Split + "/" + payload.Item.FileStem + ": applied YOLO label does not match the saved raster contour.");
            }
        }

        private static void VerifyPlanInputsUnchanged(YoloSegmentationTemplateContourMigrationPlan plan)
        {
            foreach (KeyValuePair<string, string> source in plan.SourceArtifactHashes)
            {
                if (!File.Exists(source.Key) || !string.Equals(ComputeSha256(source.Key), source.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The approved source artifact changed after migration preflight: " + source.Key);
                }
            }

            foreach (YoloSegmentationTemplateContourMigrationItem item in plan.Items)
            {
                VerifyItemInputsUnchanged(item);
            }
        }

        private static void VerifyItemInputsUnchanged(YoloSegmentationTemplateContourMigrationItem item)
        {
            if (!File.Exists(item.SegmentPath) || !File.Exists(item.MaskPath) || !File.Exists(item.LabelPath)
                || !string.Equals(ComputeSha256(item.SegmentPath), item.SegmentSha256, StringComparison.Ordinal)
                || !string.Equals(ComputeSha256(item.MaskPath), item.MaskSha256, StringComparison.Ordinal)
                || !string.Equals(ComputeSha256(item.LabelPath), item.LabelSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(item.Split + "/" + item.FileStem + ": an input artifact changed after migration preflight.");
            }
        }

        private static void VerifySourceArtifactsUnchanged(YoloSegmentationTemplateContourMigrationPlan plan)
        {
            foreach (KeyValuePair<string, string> source in plan.SourceArtifactHashes)
            {
                if (!File.Exists(source.Key) || !string.Equals(ComputeSha256(source.Key), source.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Migration changed the approved source artifact: " + source.Key);
                }
            }
        }

        private static void WriteManifest(
            YoloSegmentationTemplateContourMigrationPlan plan,
            IReadOnlyList<MigrationPayload> payloads,
            string manifestPath,
            string status,
            string error)
        {
            var manifest = new
            {
                contract = "Approved source contour transfer to legacy rectangle masks",
                status,
                source = new
                {
                    imagePath = plan.SourceImagePath,
                    segmentPath = plan.SourceSegmentPath,
                    maskPath = plan.SourceMaskPath,
                    className = plan.SourceClassName,
                    classIndex = plan.SourceClassIndex,
                    sourceMaskPixels = plan.SourceMaskPixelCount,
                    hashes = plan.SourceArtifactHashes
                },
                targetCount = plan.Items.Count,
                targets = plan.Items.Select(item => new
                {
                    split = item.Split,
                    fileStem = item.FileStem,
                    targetBounds = new { item.TargetBounds.X, item.TargetBounds.Y, item.TargetBounds.Width, item.TargetBounds.Height },
                    originalClassPixels = item.OriginalClassPixelCount,
                    original = new { segmentSha256 = item.SegmentSha256, maskSha256 = item.MaskSha256, labelSha256 = item.LabelSha256 },
                    staged = payloads.FirstOrDefault(payload => ReferenceEquals(payload.Item, item)) is MigrationPayload payload
                        ? new { segmentSha256 = payload.SegmentSha256, maskSha256 = payload.MaskSha256, labelSha256 = payload.LabelSha256 }
                        : null
                }),
                backupRoot = plan.BackupRootPath,
                error = error ?? string.Empty
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented), new UTF8Encoding(false));
        }

        private static DatasetArtifact FindDatasetArtifactByImagePath(string outputRootPath, string imagePath)
        {
            foreach (DatasetArtifact artifact in EnumerateDatasetArtifacts(outputRootPath))
            {
                if (PathsEqual(artifact.ImagePath, imagePath))
                {
                    return artifact;
                }
            }

            return null;
        }

        private static IEnumerable<DatasetArtifact> EnumerateDatasetArtifacts(string outputRootPath)
        {
            foreach (string split in DatasetModes)
            {
                string splitRoot = Path.Combine(outputRootPath, "data", split);
                string segmentDirectory = Path.Combine(splitRoot, "segments");
                string imageDirectory = Path.Combine(splitRoot, "images");
                if (!Directory.Exists(segmentDirectory) || !Directory.Exists(imageDirectory))
                {
                    continue;
                }

                foreach (string segmentPath in Directory.EnumerateFiles(segmentDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    string fileStem = Path.GetFileNameWithoutExtension(segmentPath);
                    string imagePath = FindImagePath(imageDirectory, fileStem);
                    if (string.IsNullOrWhiteSpace(imagePath))
                    {
                        continue;
                    }

                    yield return new DatasetArtifact
                    {
                        Split = split,
                        FileStem = fileStem,
                        ImagePath = imagePath,
                        SegmentPath = segmentPath,
                        MaskPath = Path.Combine(splitRoot, "masks", fileStem + ".png"),
                        LabelPath = Path.Combine(splitRoot, "labels", fileStem + ".txt")
                    };
                }
            }
        }

        private static string FindImagePath(string imageDirectory, string fileStem)
        {
            return Directory.EnumerateFiles(imageDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), fileStem, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? string.Empty;
        }

        private static SegmentationAnnotationFile ReadAnnotation(string segmentPath)
        {
            SegmentationAnnotationFile annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            if (annotation == null)
            {
                throw new JsonException("Segment JSON is empty.");
            }

            annotation.Polygons ??= new List<SegmentationPolygonRecord>();
            return annotation;
        }

        private static Size GetImageSize(string imagePath, SegmentationAnnotationFile annotation)
        {
            if ((annotation?.ImageWidth ?? 0) > 0 && (annotation?.ImageHeight ?? 0) > 0)
            {
                return new Size(annotation.ImageWidth, annotation.ImageHeight);
            }

            using Image image = Image.FromFile(imagePath);
            return image.Size;
        }

        private static byte[] ReadMaskClassValues(string maskPath, Size imageSize)
        {
            if (!File.Exists(maskPath) || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                throw new FileNotFoundException("Mask PNG is missing or its image size is invalid.", maskPath);
            }

            using var bitmap = new Bitmap(maskPath);
            if (bitmap.Size != imageSize)
            {
                throw new InvalidOperationException("Mask PNG dimensions do not match the segment image dimensions.");
            }

            var values = new byte[imageSize.Width * imageSize.Height];
            for (int y = 0; y < imageSize.Height; y++)
            {
                for (int x = 0; x < imageSize.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.R != pixel.G || pixel.R != pixel.B)
                    {
                        throw new InvalidOperationException("Mask PNG must contain grayscale class-index values.");
                    }

                    values[(y * imageSize.Width) + x] = pixel.R;
                }
            }

            return values;
        }

        private static byte[] BuildMaskPng(byte[] classValues, Size imageSize)
        {
            using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb);
            for (int y = 0; y < imageSize.Height; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = 0; x < imageSize.Width; x++)
                {
                    byte value = classValues[rowOffset + x];
                    bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
                }
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        private static Rectangle GetMaskBounds(byte[] values, Size imageSize, byte classValue)
        {
            int left = imageSize.Width;
            int top = imageSize.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < imageSize.Height; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = 0; x < imageSize.Width; x++)
                {
                    if (values[rowOffset + x] != classValue)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return right < left || bottom < top
                ? Rectangle.Empty
                : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private static bool HasClassPixelsOutsideBounds(byte[] values, Size imageSize, byte classValue, Rectangle bounds)
        {
            for (int y = 0; y < imageSize.Height; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = 0; x < imageSize.Width; x++)
                {
                    if (values[rowOffset + x] == classValue && !bounds.Contains(x, y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int ResolveClassIndex(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (record != null && record.ClassIndex >= 0 && record.ClassIndex < (classes?.Count ?? 0))
            {
                return record.ClassIndex;
            }

            return FindClassIndex(record?.ClassName, classes);
        }

        private static int FindClassIndex(string className, IReadOnlyList<CClassItem> classes)
        {
            if (string.IsNullOrWhiteSpace(className) || classes == null)
            {
                return -1;
            }

            for (int index = 0; index < classes.Count; index++)
            {
                if (string.Equals(classes[index]?.Text, className.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static byte GetMaskClassValue(int classIndex)
        {
            if (classIndex < 0 || classIndex >= 255)
            {
                throw new InvalidOperationException("SEG class index cannot be represented by the grayscale mask format.");
            }

            return (byte)(classIndex + 1);
        }

        private static void AddHash(IDictionary<string, string> hashes, string path)
        {
            hashes[path] = ComputeSha256(path);
        }

        private static string ComputeSha256(string path)
        {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        }

        private static bool PathsEqual(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithin(string path, string root)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return false;
            }
        }

        private sealed class DatasetArtifact
        {
            public string Split { get; set; } = string.Empty;

            public string FileStem { get; set; } = string.Empty;

            public string ImagePath { get; set; } = string.Empty;

            public string SegmentPath { get; set; } = string.Empty;

            public string MaskPath { get; set; } = string.Empty;

            public string LabelPath { get; set; } = string.Empty;
        }

        private sealed class MigrationPayload
        {
            public YoloSegmentationTemplateContourMigrationItem Item { get; set; }

            public string StagedSegmentPath { get; set; } = string.Empty;

            public string StagedMaskPath { get; set; } = string.Empty;

            public string StagedLabelPath { get; set; } = string.Empty;

            public byte[] ExpectedMaskValues { get; set; }

            public List<string> ExpectedLabelLines { get; set; } = new List<string>();

            public string SegmentSha256 { get; set; } = string.Empty;

            public string MaskSha256 { get; set; } = string.Empty;

            public string LabelSha256 { get; set; } = string.Empty;
        }
    }
}
