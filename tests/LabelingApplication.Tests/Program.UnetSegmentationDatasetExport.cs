using MvcVisionSystem;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestUnetSegmentationDatasetExport()
    {
        string root = CreateTempRoot();
        try
        {
            CData data = CreateUnetSegmentationFixture(Path.Combine(root, "ready"));
            string sourceBefore = UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data);
            UnetSegmentationDatasetExportResult result = UnetSegmentationDatasetExportService.Export(data);

            AssertTrue(result.IsReady, string.Join(Environment.NewLine, result.Errors));
            AssertTrue(!result.ReusedExistingArtifact, "first U-Net export should materialize a new app-owned artifact");
            AssertEqual(3, result.ImageCount);
            AssertEqual(3, result.PositiveMaskImageCount);
            AssertEqual(3, result.Splits.Count);
            AssertEqual(sourceBefore, result.SourceDataTreeSha256Before);
            AssertEqual(sourceBefore, result.SourceDataTreeSha256After);
            AssertEqual(sourceBefore, UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data));
            AssertTrue(File.Exists(Path.Combine(result.OutputRootPath, "classes.json")), "U-Net export should persist its class contract");
            string manifestPath = Path.Combine(result.OutputRootPath, "dataset-manifest.json");
            AssertTrue(File.Exists(manifestPath), "U-Net export should persist its provenance manifest");

            UnetSegmentationDatasetExportManifest manifest = JsonConvert.DeserializeObject<UnetSegmentationDatasetExportManifest>(File.ReadAllText(manifestPath));
            AssertEqual(result.DatasetFingerprint, manifest.DatasetFingerprint);
            AssertEqual(result.SourceDataTreeSha256Before, manifest.SourceDataTreeSha256);
            AssertEqual(2, manifest.Classes.Count);
            AssertEqual(3, manifest.Splits.Sum(split => split.Images.Count));

            using (var trainMask = new Bitmap(Path.Combine(result.OutputRootPath, "masks", "train", "train.png")))
            using (var testMask = new Bitmap(Path.Combine(result.OutputRootPath, "masks", "test", "test.png")))
            {
                AssertEqual(1, trainMask.GetPixel(8, 8).R);
                AssertEqual(2, testMask.GetPixel(20, 20).R);
            }

            UnetSegmentationDatasetExportResult reused = UnetSegmentationDatasetExportService.Export(data);
            AssertTrue(reused.IsReady, string.Join(Environment.NewLine, reused.Errors));
            AssertTrue(reused.ReusedExistingArtifact, "matching source/class provenance should reuse the existing export");
            AssertEqual(result.OutputRootPath, reused.OutputRootPath);

            CData duplicateAcrossSplits = CreateUnetSegmentationFixture(Path.Combine(root, "duplicate"));
            string duplicateTrainImagePath = Directory.EnumerateFiles(
                Path.Combine(duplicateAcrossSplits.OutputRootPath, "data", "train", "images"),
                "*",
                SearchOption.TopDirectoryOnly).Single();
            string duplicateTestImagePath = Directory.EnumerateFiles(
                Path.Combine(duplicateAcrossSplits.OutputRootPath, "data", "test", "images"),
                "*",
                SearchOption.TopDirectoryOnly).Single();
            File.Copy(
                duplicateTrainImagePath,
                duplicateTestImagePath,
                overwrite: true);
            UnetSegmentationDatasetExportResult duplicateResult = UnetSegmentationDatasetExportService.Export(duplicateAcrossSplits);
            AssertTrue(!duplicateResult.IsReady, "U-Net export must reject image content shared by different splits");
            AssertTrue(duplicateResult.Errors.Any(error => error.Contains("duplicate image content across splits", StringComparison.OrdinalIgnoreCase)),
                "split-content leakage should be an explicit U-Net export error");

            CData overlappingClasses = CreateUnetSegmentationFixture(Path.Combine(root, "overlap"), overlapTrainClasses: true);
            UnetSegmentationDatasetExportResult overlapResult = UnetSegmentationDatasetExportService.Export(overlappingClasses);
            AssertTrue(!overlapResult.IsReady, "U-Net export must reject a pixel assigned to different classes");
            AssertTrue(overlapResult.Errors.Any(error => error.Contains("different classes overlap", StringComparison.OrdinalIgnoreCase)),
                "class-overlap validation should name the mask conflict");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static CData CreateUnetSegmentationFixture(string root, bool overlapTrainClasses = false)
    {
        var data = new CData();
        data.ConfigureOutputRoot(root);
        data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
        data.ClassNamedList.Add(new CClassItem { Text = "SurfaceDefect", DrawColor = Color.Red });
        data.ClassNamedList.Add(new CClassItem { Text = "EdgeDefect", DrawColor = Color.Lime });

        SaveUnetFixtureImage(
            data,
            "train.png",
            Color.Black,
            new Dictionary<string, List<LabelingSegmentationObject>>
            {
                ["SurfaceDefect"] = new List<LabelingSegmentationObject>
                {
                    CreateUnetPolygon(data.ClassNamedList[0], 4, 4, 14, 14)
                },
                ["EdgeDefect"] = overlapTrainClasses
                    ? new List<LabelingSegmentationObject> { CreateUnetPolygon(data.ClassNamedList[1], 10, 10, 22, 22) }
                    : new List<LabelingSegmentationObject> { CreateUnetPolygon(data.ClassNamedList[1], 18, 18, 28, 28) }
            },
            validationPercent: 0,
            testPercent: 0);
        SaveUnetFixtureImage(
            data,
            "valid.png",
            Color.White,
            new Dictionary<string, List<LabelingSegmentationObject>>
            {
                ["SurfaceDefect"] = new List<LabelingSegmentationObject>
                {
                    CreateUnetPolygon(data.ClassNamedList[0], 5, 5, 15, 15)
                }
            },
            validationPercent: 100,
            testPercent: 0);
        SaveUnetFixtureImage(
            data,
            "test.png",
            Color.Gray,
            new Dictionary<string, List<LabelingSegmentationObject>>
            {
                ["EdgeDefect"] = new List<LabelingSegmentationObject>
                {
                    CreateUnetPolygon(data.ClassNamedList[1], 16, 16, 26, 26)
                }
            },
            validationPercent: 0,
            testPercent: 100);

        return data;
    }

    private static LabelingSegmentationObject CreateUnetPolygon(CClassItem classItem, int left, int top, int right, int bottom)
    {
        return new LabelingSegmentationObject(
            new[]
            {
                new Point(left, top),
                new Point(right, top),
                new Point(right, bottom),
                new Point(left, bottom)
            },
            classItem);
    }

    private static void SaveUnetFixtureImage(
        CData data,
        string imageName,
        Color color,
        IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments,
        int validationPercent,
        int testPercent)
    {
        data.ProjectSettings.YoloDataset.ValidationPercent = validationPercent;
        data.ProjectSettings.YoloDataset.TestPercent = testPercent;
        using Bitmap image = CreateSolidBitmap(32, 32, color);
        YoloAnnotationService.SaveAnnotations(
            imageName,
            image,
            new Dictionary<string, List<CRectangleObject>>(),
            data.ClassNamedList,
            data);
        YoloSegmentationAnnotationService.SaveSegmentationAnnotations(imageName, image, segments, data.ClassNamedList, data);
    }
}
