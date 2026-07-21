using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunRealUltralyticsSegmentationPredictionExportSmoke(string[] args)
    {
        string artifactRoot = string.Empty;
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            string yoloRoot = Path.GetFullPath(GetArgumentValue(args, "--yolo-root", @"C:\Git\yolov8"));
            string pythonPath = Path.Combine(yoloRoot, ".venv-gpu", "Scripts", "python.exe");
            string weightsPath = Path.GetFullPath(GetArgumentValue(
                args,
                "--weights",
                Path.Combine(yoloRoot, "runs", "segment", "openvisionlab-yolov8-seg-contour124-retrain-20260716", "weights", "best.pt")));
            artifactRoot = Path.Combine(
                repositoryRoot,
                "artifacts",
                "real-ultralytics-segmentation-prediction-export-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            AssertTrue(File.Exists(pythonPath), "Ultralytics Python was not found: " + pythonPath);
            AssertTrue(File.Exists(weightsPath), "A compatible two-class Ultralytics segmentation checkpoint was not found: " + weightsPath);
            AssertTrue(!Directory.Exists(artifactRoot), "refusing to overwrite an existing prediction-export artifact: " + artifactRoot);
            Directory.CreateDirectory(artifactRoot);

            (string canonicalRoot, string testImagePath, string sourceSha256) = CreateUltralyticsCanonicalArtifact(Path.Combine(artifactRoot, "canonical-export"));

            var settings = new PythonModelSettings
            {
                ModelEngine = PythonModelSettings.EngineYoloV8,
                ProjectRootPath = yoloRoot,
                PythonExecutablePath = pythonPath,
                WeightsPath = weightsPath,
                InferenceImageSize = 32,
                MinimumDetectionConfidence = 0.0F
            };
            SegmentationPredictionExportRequest request = SegmentationPredictionExportService.BuildRequest(
                SegmentationPredictionExportService.AdapterUltralytics,
                settings,
                canonicalRoot,
                Path.Combine(artifactRoot, "ultralytics-test-predictions"));
            request.ImageSize = 32;
            request.Confidence = 0.0D;
            request.Device = "cpu";
            SegmentationPredictionExportResult prediction = SegmentationPredictionExportService.Run(request);
            AssertTrue(prediction.Succeeded, "Ultralytics segmentation prediction export failed: " + prediction.Error);
            string[] records = File.ReadAllLines(prediction.PredictionManifestPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            AssertEqual(1, records.Length);
            AssertEqual(sourceSha256, ComputeFileSha256(testImagePath));

            File.WriteAllLines(Path.Combine(artifactRoot, "summary.txt"), new[]
            {
                "REAL_ULTRALYTICS_SEGMENTATION_PREDICTION_EXPORT completed.",
                "python=" + pythonPath,
                "weights=" + weightsPath,
                "weightsSha256=" + ComputeFileSha256(weightsPath),
                "canonicalExport=" + canonicalRoot,
                "sourceImageSha256Before=" + sourceSha256,
                "sourceImageSha256After=" + ComputeFileSha256(testImagePath),
                "predictionManifest=" + prediction.PredictionManifestPath,
                "predictionRecordCount=" + records.Length.ToString(CultureInfo.InvariantCulture)
            });
            Console.WriteLine("REAL_ULTRALYTICS_SEGMENTATION_PREDICTION_EXPORT summary=" + Path.Combine(artifactRoot, "summary.txt"));
            return 0;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(artifactRoot))
            {
                Directory.CreateDirectory(artifactRoot);
                File.WriteAllText(Path.Combine(artifactRoot, "failure.txt"), ex.ToString());
            }
            Console.Error.WriteLine("FAIL real Ultralytics segmentation prediction export: " + ex.Message);
            return 1;
        }
    }

    private static (string Root, string TestImagePath, string SourceSha256) CreateUltralyticsCanonicalArtifact(string root)
    {
        string imagePath = Path.Combine(root, "images", "test", "test.png");
        string maskPath = Path.Combine(root, "masks", "test", "test.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath) ?? root);
        Directory.CreateDirectory(Path.GetDirectoryName(maskPath) ?? root);
        using (Bitmap image = CreateSolidBitmap(32, 32, Color.Gray))
        {
            image.Save(imagePath, ImageFormat.Png);
        }
        using (var mask = new Bitmap(32, 32, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(mask))
        using (var brush = new SolidBrush(Color.FromArgb(2, 2, 2)))
        {
            graphics.Clear(Color.Black);
            graphics.FillRectangle(brush, 16, 16, 10, 10);
            mask.Save(maskPath, ImageFormat.Png);
        }
        string imageSha256 = ComputeFileSha256(imagePath);
        var manifest = new UnetSegmentationDatasetExportManifest
        {
            Version = 1,
            DatasetFingerprint = "ultralytics-normalization-fixture",
            SourceRecipeRootPath = root,
            SourceDataTreeSha256 = imageSha256,
            ClassContractSha256 = "ok-ng-class-contract",
            Classes = new List<UnetClassContractItem>
            {
                new UnetClassContractItem { Index = 1, Name = "OK" },
                new UnetClassContractItem { Index = 2, Name = "NG" }
            },
            Splits = new List<UnetSegmentationDatasetExportManifestSplit>
            {
                new UnetSegmentationDatasetExportManifestSplit
                {
                    Split = "test",
                    Images = new List<UnetSegmentationDatasetExportManifestImage>
                    {
                        new UnetSegmentationDatasetExportManifestImage
                        {
                            SourceRelativeImagePath = "test/images/test.png",
                            ImageSha256 = imageSha256,
                            ImageWidth = 32,
                            ImageHeight = 32,
                            ExportImageRelativePath = "images/test/test.png",
                            ExportImageSha256 = imageSha256,
                            ExportMaskRelativePath = "masks/test/test.png",
                            ExportMaskSha256 = ComputeFileSha256(maskPath),
                            HasForeground = true
                        }
                    }
                }
            }
        };
        File.WriteAllText(Path.Combine(root, "dataset-manifest.json"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
        File.WriteAllText(Path.Combine(root, "classes.json"), JsonConvert.SerializeObject(manifest.Classes, Formatting.Indented));
        return (root, imagePath, imageSha256);
    }
}
