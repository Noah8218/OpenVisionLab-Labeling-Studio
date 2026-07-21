using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestSegmentationMaskComparison()
    {
        string root = CreateTempRoot();
        try
        {
            CData data = CreateUnetSegmentationFixture(Path.Combine(root, "recipe"));
            string sourceBefore = UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data);
            UnetSegmentationDatasetExportResult export = UnetSegmentationDatasetExportService.Export(data);
            AssertTrue(export.IsReady, string.Join(Environment.NewLine, export.Errors));

            string datasetManifestPath = Path.Combine(export.OutputRootPath, "dataset-manifest.json");
            UnetSegmentationDatasetExportManifest dataset = JsonConvert.DeserializeObject<UnetSegmentationDatasetExportManifest>(File.ReadAllText(datasetManifestPath));
            UnetSegmentationDatasetExportManifestImage testImage = dataset.Splits
                .Single(split => string.Equals(split.Split, "test", StringComparison.OrdinalIgnoreCase))
                .Images
                .Single();
            string groundTruthPath = Path.Combine(export.OutputRootPath, testImage.ExportMaskRelativePath);

            string baselineRoot = Path.Combine(root, "baseline");
            string candidateRoot = Path.Combine(root, "candidate");
            string baselineMaskPath = CopyMask(groundTruthPath, baselineRoot, "predictions/test/test.png");
            string candidateMaskPath = CopyMask(groundTruthPath, candidateRoot, "predictions/test/test.png");
            using (var candidateMask = new Bitmap(candidateMaskPath))
            {
                for (int y = 16; y < 21; y++)
                {
                    for (int x = 16; x < 21; x++)
                    {
                        candidateMask.SetPixel(x, y, Color.Black);
                    }
                }
                for (int y = 1; y < 3; y++)
                {
                    for (int x = 1; x < 3; x++)
                    {
                        candidateMask.SetPixel(x, y, Color.FromArgb(2, 2, 2));
                    }
                }
                candidateMask.Save(candidateMaskPath + ".tmp", System.Drawing.Imaging.ImageFormat.Png);
            }
            File.Delete(candidateMaskPath);
            File.Move(candidateMaskPath + ".tmp", candidateMaskPath);

            string baselineManifestPath = WritePredictionManifest(baselineRoot, dataset, testImage, "unet", "U-Net", baselineMaskPath);
            string candidateManifestPath = WritePredictionManifest(candidateRoot, dataset, testImage, "ultralytics", "YOLOv8", candidateMaskPath);
            var request = new SegmentationMaskComparisonRequest
            {
                DatasetExportRootPath = export.OutputRootPath,
                BaselinePredictionManifestPath = baselineManifestPath,
                CandidatePredictionManifestPath = candidateManifestPath,
                Split = "test",
                OutputRootPath = Path.Combine(root, "comparison"),
                ComponentIouThreshold = 0.5D
            };
            SegmentationMaskComparisonResult result = SegmentationMaskComparisonService.Evaluate(request);

            AssertTrue(result.IsReady, string.Join(Environment.NewLine, result.Errors));
            AssertTrue(File.Exists(result.ReportPath), "comparison should persist an app-owned summary report");
            AssertEqual(dataset.DatasetFingerprint, result.DatasetFingerprint);
            AssertEqual(2, result.Classes.Count);
            SegmentationMaskComparisonClassResult edge = result.Classes.Single(item => string.Equals(item.ClassName, "EdgeDefect", StringComparison.Ordinal));
            AssertEqual(1D, edge.Baseline.Dice);
            AssertEqual(1D, edge.Baseline.IoU);
            AssertEqual(1, edge.Baseline.TruePositiveComponents);
            AssertEqual(0, edge.Baseline.FalsePositiveComponents);
            AssertTrue(edge.Candidate.FalseNegativePixels > 0, "candidate should preserve pixel-level false negatives");
            AssertEqual(1, edge.Candidate.TruePositiveComponents);
            AssertEqual(1, edge.Candidate.FalsePositiveComponents);
            AssertTrue(edge.Candidate.Dice < edge.Baseline.Dice, "candidate pixel Dice should not equal the exact baseline mask");
            AssertEqual(sourceBefore, UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data));

            SegmentationPredictionManifestRecord incompatible = JsonConvert.DeserializeObject<SegmentationPredictionManifestRecord>(File.ReadAllText(candidateManifestPath));
            incompatible.ClassContractSha256 = "incompatible";
            File.WriteAllText(candidateManifestPath, JsonConvert.SerializeObject(incompatible) + Environment.NewLine);
            SegmentationMaskComparisonResult blocked = SegmentationMaskComparisonService.Evaluate(new SegmentationMaskComparisonRequest
            {
                DatasetExportRootPath = export.OutputRootPath,
                BaselinePredictionManifestPath = baselineManifestPath,
                CandidatePredictionManifestPath = candidateManifestPath,
                Split = "test",
                OutputRootPath = Path.Combine(root, "blocked")
            });
            AssertTrue(!blocked.IsReady, "comparison must block an incompatible class contract before scoring");
            AssertTrue(blocked.Errors.Any(error => error.Contains("provenance does not match", StringComparison.OrdinalIgnoreCase)),
                "comparison should name the canonical provenance mismatch");

            PythonModelSettings unetSettings = PythonModelRuntimeConnectionService
                .BuildUnetFolderConnection(new PythonModelSettings(), PythonModelSettings.GetDefaultUnetProjectRootPath())
                .Settings;
            SegmentationPredictionExportRequest exportRequest = SegmentationPredictionExportService.BuildRequest(
                SegmentationPredictionExportService.AdapterUnet,
                unetSettings,
                export.OutputRootPath,
                Path.Combine(root, "unet-predictions"));
            AssertTrue(File.Exists(exportRequest.ScriptPath), "prediction export should resolve the bundled app script");
            AssertEqual("unet", exportRequest.AdapterKey);
            AssertTrue(SegmentationPredictionExportService.CreateStartInfo(exportRequest).ArgumentList.Contains("--adapter"),
                "prediction export command should declare its adapter instead of guessing it");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CopyMask(string sourcePath, string root, string relativePath)
    {
        string targetPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? root);
        File.Copy(sourcePath, targetPath, overwrite: false);
        return targetPath;
    }

    private static string WritePredictionManifest(
        string root,
        UnetSegmentationDatasetExportManifest dataset,
        UnetSegmentationDatasetExportManifestImage image,
        string adapter,
        string engine,
        string maskPath)
    {
        string fullRoot = Path.GetFullPath(root);
        var record = new SegmentationPredictionManifestRecord
        {
            Version = 1,
            AdapterKey = adapter,
            Engine = engine,
            DatasetFingerprint = dataset.DatasetFingerprint,
            SourceDataTreeSha256 = dataset.SourceDataTreeSha256,
            ClassContractSha256 = dataset.ClassContractSha256,
            Split = "test",
            CheckpointPath = engine + ".pt",
            CheckpointSha256 = "checkpoint-" + adapter,
            ImageSha256 = image.ImageSha256,
            ImageWidth = image.ImageWidth,
            ImageHeight = image.ImageHeight,
            PredictionMaskRelativePath = Path.GetRelativePath(fullRoot, maskPath).Replace('\\', '/'),
            PredictionMaskSha256 = ComputeFileSha256(maskPath)
        };
        string manifestPath = Path.Combine(fullRoot, "prediction-manifest.jsonl");
        File.WriteAllText(manifestPath, JsonConvert.SerializeObject(record) + Environment.NewLine);
        return manifestPath;
    }
}
