using MvcVisionSystem;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunSegmentationTemplateContourMigrationApply(string[] args)
    {
        string outputRootPath = GetArgumentValue(args, "--dataset-output-root", string.Empty);
        string sourceImagePath = GetArgumentValue(args, "--source-image", string.Empty);
        string sourceClassName = GetArgumentValue(args, "--source-class", "OK");
        string classNamesText = GetArgumentValue(args, "--dataset-classes", "OK,NG");
        string backupRootPath = GetArgumentValue(args, "--backup-root", string.Empty);
        string expectedTargetCountText = GetArgumentValue(args, "--expected-target-count", "120");
        AssertTrue(
            int.TryParse(expectedTargetCountText, out int expectedTargetCount) && expectedTargetCount > 0,
            "--expected-target-count must be a positive integer.");
        return RunSingleSmoke(
            "Approved SEG template contour migration preserves source and writes backups",
            () => TestYoloSegmentationTemplateContourMigrationApply(
                outputRootPath,
                sourceImagePath,
                sourceClassName,
                classNamesText,
                backupRootPath,
                expectedTargetCount));
    }

    private static void TestYoloSegmentationTemplateContourMigration()
    {
        string root = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(root);
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ClassNamedList.Clear();
            data.ClassNamedList.Add(new CClassItem { Text = "OK", DrawColor = Color.LimeGreen });
            data.ClassNamedList.Add(new CClassItem { Text = "NG", DrawColor = Color.Red });
            data.EnsureYoloOutputDirectories();

            string sourceImagePath = SaveTemplateContourMigrationSource(data);
            string targetImagePath = SaveTemplateContourMigrationTarget(data);
            string sourceSegmentPath = Path.Combine(root, "data", "valid", "segments", "source.json");
            string sourceMaskPath = Path.Combine(root, "data", "valid", "masks", "source.png");
            string sourceLabelPath = Path.Combine(root, "data", "valid", "labels", "source.txt");
            string targetSegmentPath = Path.Combine(root, "data", "train", "segments", "target.json");
            string targetMaskPath = Path.Combine(root, "data", "train", "masks", "target.png");
            string targetLabelPath = Path.Combine(root, "data", "train", "labels", "target.txt");
            var sourceBefore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [sourceImagePath] = Convert.ToBase64String(File.ReadAllBytes(sourceImagePath)),
                [sourceSegmentPath] = Convert.ToBase64String(File.ReadAllBytes(sourceSegmentPath)),
                [sourceMaskPath] = Convert.ToBase64String(File.ReadAllBytes(sourceMaskPath)),
                [sourceLabelPath] = Convert.ToBase64String(File.ReadAllBytes(sourceLabelPath))
            };
            var targetBefore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [targetSegmentPath] = Convert.ToBase64String(File.ReadAllBytes(targetSegmentPath)),
                [targetMaskPath] = Convert.ToBase64String(File.ReadAllBytes(targetMaskPath)),
                [targetLabelPath] = Convert.ToBase64String(File.ReadAllBytes(targetLabelPath))
            };
            string backupRootPath = Path.Combine(root, "backups", "approved-template-contour-migration");

            YoloSegmentationTemplateContourMigrationPlan plan =
                YoloSegmentationTemplateContourMigrationService.BuildPlan(data, sourceImagePath, "OK", backupRootPath);
            AssertTrue(plan.CanApply, string.Join(Environment.NewLine, plan.Errors));
            AssertEqual(1, plan.Items.Count);
            AssertEqual("target", plan.Items[0].FileStem);

            YoloSegmentationTemplateContourMigrationResult result =
                YoloSegmentationTemplateContourMigrationService.Apply(plan);
            AssertEqual(1, result.MigratedImageCount);
            AssertTrue(File.Exists(result.ManifestPath), "migration should persist a backup manifest");

            foreach (KeyValuePair<string, string> source in sourceBefore)
            {
                AssertEqual(source.Value, Convert.ToBase64String(File.ReadAllBytes(source.Key)));
            }

            foreach (KeyValuePair<string, string> target in targetBefore)
            {
                string relative = Path.GetRelativePath(root, target.Key);
                string backupPath = Path.Combine(result.BackupRootPath, "originals", relative);
                AssertTrue(File.Exists(backupPath), "migration backup is missing: " + relative);
                AssertEqual(target.Value, Convert.ToBase64String(File.ReadAllBytes(backupPath)));
            }

            SegmentationAnnotationFile annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(targetSegmentPath));
            SegmentationPolygonRecord okRecord = annotation.Polygons.Single(record => record.ClassIndex == 0);
            AssertEqual("RasterMask", okRecord.GeometryType);
            AssertTrue(okRecord.Points.Count > 4, "migrated OK record should store the transferred contour rather than a rectangle");

            using (var mask = new Bitmap(targetMaskPath))
            {
                AssertEqual(0, mask.GetPixel(4, 4).R);
                AssertEqual(1, mask.GetPixel(9, 9).R);
                AssertEqual(2, mask.GetPixel(12, 9).R);
            }

            string[] migratedLabelLines = File.ReadAllLines(targetLabelPath);
            AssertTrue(migratedLabelLines.Length >= 2, "migrated target should retain both OK and NG YOLO labels");
            AssertTrue(migratedLabelLines.Single(line => line.StartsWith("0 ", StringComparison.Ordinal)).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 9,
                "migrated OK YOLO label should contain a contour instead of four rectangle corners");

            YoloSegmentationHistoricalRemediationAuditReport postMigrationAudit =
                YoloSegmentationHistoricalRemediationAuditService.Build(data, sourceImagePath);
            AssertEqual(0, postMigrationAudit.CandidateImageCount);
            AssertTrue(File.Exists(targetImagePath), "migration should not remove the target image");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static void TestYoloSegmentationTemplateContourMigrationApply(
        string outputRootPath,
        string sourceImagePath,
        string sourceClassName,
        string classNamesText,
        string backupRootPath,
        int expectedTargetCount)
    {
        AssertTrue(!string.IsNullOrWhiteSpace(outputRootPath), "--dataset-output-root is required.");
        AssertTrue(!string.IsNullOrWhiteSpace(sourceImagePath), "--source-image is required.");
        string root = Path.GetFullPath(outputRootPath);
        string sourcePath = Path.GetFullPath(sourceImagePath);
        AssertTrue(Directory.Exists(root), "SEG migration dataset root was not found: " + root);
        AssertTrue(File.Exists(sourcePath), "SEG migration source image was not found: " + sourcePath);

        string[] classNames = (classNamesText ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        AssertTrue(classNames.Length > 0, "--dataset-classes must provide at least one class.");

        if (string.IsNullOrWhiteSpace(backupRootPath))
        {
            backupRootPath = Path.Combine(
                root,
                "backups",
                "segmentation-template-contour-migration-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        var data = new CData();
        data.ConfigureOutputRoot(root);
        data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
        data.ClassNamedList.Clear();
        foreach (string className in classNames)
        {
            data.ClassNamedList.Add(new CClassItem { Text = className });
        }

        string sourceSegmentPath = FindSegmentationArtifactPath(root, sourcePath, "segments", ".json");
        string sourceMaskPath = FindSegmentationArtifactPath(root, sourcePath, "masks", ".png");
        string sourceLabelPath = FindSegmentationArtifactPath(root, sourcePath, "labels", ".txt");
        var sourceBefore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [sourcePath] = Convert.ToBase64String(File.ReadAllBytes(sourcePath)),
            [sourceSegmentPath] = Convert.ToBase64String(File.ReadAllBytes(sourceSegmentPath)),
            [sourceMaskPath] = Convert.ToBase64String(File.ReadAllBytes(sourceMaskPath)),
            [sourceLabelPath] = Convert.ToBase64String(File.ReadAllBytes(sourceLabelPath))
        };

        YoloSegmentationTemplateContourMigrationPlan plan =
            YoloSegmentationTemplateContourMigrationService.BuildPlan(data, sourcePath, sourceClassName, backupRootPath);
        AssertTrue(plan.CanApply, string.Join(Environment.NewLine, plan.Errors));
        AssertEqual(expectedTargetCount, plan.Items.Count);
        AssertTrue(!Directory.Exists(backupRootPath), "migration backup root must be new before applying the approved operation.");

        YoloSegmentationTemplateContourMigrationResult result =
            YoloSegmentationTemplateContourMigrationService.Apply(plan);
        AssertEqual(expectedTargetCount, result.MigratedImageCount);
        AssertTrue(File.Exists(result.ManifestPath), "approved migration manifest was not written.");
        AssertEqual(
            expectedTargetCount * 3,
            Directory.EnumerateFiles(Path.Combine(result.BackupRootPath, "originals"), "*", SearchOption.AllDirectories).Count());

        foreach (KeyValuePair<string, string> source in sourceBefore)
        {
            AssertEqual(source.Value, Convert.ToBase64String(File.ReadAllBytes(source.Key)));
        }

        YoloSegmentationTemplateContourMigrationPlan postMigrationPlan =
            YoloSegmentationTemplateContourMigrationService.BuildPlan(
                data,
                sourcePath,
                sourceClassName,
                Path.Combine(root, "backups", "segmentation-template-contour-postcheck-" + Guid.NewGuid().ToString("N")));
        AssertEqual(0, postMigrationPlan.Items.Count);

        Console.WriteLine(
            FormattableString.Invariant(
                $"SEG_TEMPLATE_CONTOUR_MIGRATION={result.BackupRootPath} / images={result.MigratedImageCount} / records={result.MigratedRecordCount} / sourceArtifactsHashUnchanged=true"));
    }

    private static string SaveTemplateContourMigrationSource(CData data)
    {
        const int size = 20;
        string imagePath = Path.Combine(data.ValidImagesPath, "source.png");
        string segmentPath = Path.Combine(data.OutputRootPath, "data", "valid", "segments", "source.json");
        string maskPath = Path.Combine(data.OutputRootPath, "data", "valid", "masks", "source.png");
        string labelPath = Path.Combine(data.OutputRootPath, "data", "valid", "labels", "source.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(segmentPath));
        Directory.CreateDirectory(Path.GetDirectoryName(maskPath));
        Directory.CreateDirectory(Path.GetDirectoryName(labelPath));
        using (var image = CreateSolidBitmap(size, size, Color.DimGray))
        {
            image.Save(imagePath, ImageFormat.Png);
        }

        var values = new byte[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (((x - 10) * (x - 10)) + ((y - 10) * (y - 10)) <= 25)
                {
                    values[(y * size) + x] = 1;
                }
            }
        }

        SaveTemplateContourMigrationMask(maskPath, values, new Size(size, size));
        SaveTemplateContourMigrationAnnotation(
            segmentPath,
            "source.png",
            size,
            new List<SegmentationPolygonRecord>
            {
                CreateTemplateContourMigrationRecord(0, "OK", new Rectangle(5, 5, 11, 11), string.Empty)
            });
        File.WriteAllLines(labelPath, new[] { "0 0.25 0.25 0.75 0.25 0.75 0.75 0.25 0.75" });
        return imagePath;
    }

    private static string SaveTemplateContourMigrationTarget(CData data)
    {
        const int size = 20;
        string imagePath = Path.Combine(data.TrainImagesPath, "target.png");
        string segmentPath = Path.Combine(data.OutputRootPath, "data", "train", "segments", "target.json");
        string maskPath = Path.Combine(data.OutputRootPath, "data", "train", "masks", "target.png");
        string labelPath = Path.Combine(data.OutputRootPath, "data", "train", "labels", "target.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(segmentPath));
        Directory.CreateDirectory(Path.GetDirectoryName(maskPath));
        Directory.CreateDirectory(Path.GetDirectoryName(labelPath));
        using (var image = CreateSolidBitmap(size, size, Color.DimGray))
        {
            image.Save(imagePath, ImageFormat.Png);
        }

        var values = new byte[size * size];
        for (int y = 4; y < 15; y++)
        {
            for (int x = 4; x < 15; x++)
            {
                values[(y * size) + x] = 1;
            }
        }

        values[(9 * size) + 12] = 2;
        values[(9 * size) + 13] = 2;
        values[(10 * size) + 12] = 2;
        values[(10 * size) + 13] = 2;
        SaveTemplateContourMigrationMask(maskPath, values, new Size(size, size));
        SaveTemplateContourMigrationAnnotation(
            segmentPath,
            "target.png",
            size,
            new List<SegmentationPolygonRecord>
            {
                CreateTemplateContourMigrationRecord(0, "OK", new Rectangle(4, 4, 12, 12), string.Empty),
                CreateTemplateContourMigrationRecord(1, "NG", new Rectangle(12, 9, 2, 2), "RasterMask")
            });
        File.WriteAllLines(labelPath, new[]
        {
            "0 0.2 0.2 0.75 0.2 0.75 0.75 0.2 0.75",
            "1 0.6 0.45 0.7 0.45 0.7 0.55 0.6 0.55"
        });
        return imagePath;
    }

    private static SegmentationPolygonRecord CreateTemplateContourMigrationRecord(int classIndex, string className, Rectangle bounds, string geometryType)
    {
        return new SegmentationPolygonRecord
        {
            ClassIndex = classIndex,
            ClassName = className,
            GeometryType = geometryType,
            Points = SegmentationGeometry.RectangleToPolygon(bounds, new Size(20, 20))
                .Select(point => new SegmentationPointRecord { X = point.X, Y = point.Y })
                .ToList()
        };
    }

    private static void SaveTemplateContourMigrationAnnotation(string path, string imageName, int size, List<SegmentationPolygonRecord> records)
    {
        var annotation = new SegmentationAnnotationFile
        {
            Version = 1,
            ImageName = imageName,
            ImageWidth = size,
            ImageHeight = size,
            Polygons = records
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(annotation, Formatting.Indented));
    }

    private static void SaveTemplateContourMigrationMask(string path, byte[] values, Size imageSize)
    {
        using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb);
        for (int y = 0; y < imageSize.Height; y++)
        {
            int rowOffset = y * imageSize.Width;
            for (int x = 0; x < imageSize.Width; x++)
            {
                byte value = values[rowOffset + x];
                bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
            }
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private static string FindSegmentationArtifactPath(string root, string imagePath, string folderName, string extension)
    {
        string fullImagePath = Path.GetFullPath(imagePath);
        string fileStem = Path.GetFileNameWithoutExtension(fullImagePath);
        foreach (string split in new[] { "train", "valid", "test" })
        {
            string expectedImageDirectory = Path.Combine(root, "data", split, "images");
            if (!fullImagePath.StartsWith(Path.GetFullPath(expectedImageDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string path = Path.Combine(root, "data", split, folderName, fileStem + extension);
            AssertTrue(File.Exists(path), "expected source artifact was not found: " + path);
            return path;
        }

        throw new InvalidOperationException("source image is outside the active dataset split folders: " + fullImagePath);
    }
}
