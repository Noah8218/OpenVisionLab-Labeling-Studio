using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Globalization;
using System.IO;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    // Opt-in evidence only: executes the same Model Center orchestration against two
    // checkpoints trained from one native segmentation source. It never adopts either.
    private static int RunRealExternalSegmentationAdapterComparison(string[] args)
    {
        string artifactRoot = string.Empty;
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            string dataYamlPath = Path.GetFullPath(GetArgumentValue(args, "--external-data-yaml", string.Empty));
            string unetRoot = Path.GetFullPath(GetArgumentValue(args, "--unet-root", @"C:\Git\unet"));
            string yoloRoot = Path.GetFullPath(GetArgumentValue(args, "--yolo-root", @"C:\Git\yolov8"));
            string unetWeightsPath = Path.GetFullPath(GetArgumentValue(args, "--unet-weights", string.Empty));
            string yoloWeightsPath = Path.GetFullPath(GetArgumentValue(args, "--yolo-weights", string.Empty));
            int imageSize = GetPositiveArgument(args, "--image-size", 32);
            float yoloConfidence = GetYoloConfidenceArgument(args);
            artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(repositoryRoot, "artifacts", "real-external-segmentation-adapter-comparison-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))));

            AssertTrue(File.Exists(dataYamlPath), "external native YOLO segmentation data.yaml was not found: " + dataYamlPath);
            AssertTrue(Directory.Exists(unetRoot), "U-Net runtime root was not found: " + unetRoot);
            AssertTrue(Directory.Exists(yoloRoot), "YOLO runtime root was not found: " + yoloRoot);
            AssertTrue(File.Exists(unetWeightsPath), "U-Net checkpoint was not found: " + unetWeightsPath);
            AssertTrue(File.Exists(yoloWeightsPath), "YOLO-seg checkpoint was not found: " + yoloWeightsPath);
            AssertTrue(!Directory.Exists(artifactRoot), "refusing to overwrite an existing segmentation adapter comparison artifact: " + artifactRoot);

            YoloExternalDatasetIntakeReport sourceBefore = YoloExternalDatasetIntakeService.Build(
                dataYamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(sourceBefore.IsReady, "external segmentation source is not ready: " + string.Join(" / ", sourceBefore.Errors));
            AssertTrue(sourceBefore.Test.ImageCount > 0, "external segmentation source needs a held-out test split for comparison");

            Directory.CreateDirectory(artifactRoot);
            var data = new CData();
            data.ConfigureOutputRoot(Path.Combine(artifactRoot, "app-output"));
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ProjectSettings.ExternalYoloDataset.DataYamlFilePath = sourceBefore.DataYamlFilePath;
            data.ProjectSettings.ExternalYoloDataset.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;
            YoloExternalDatasetIntakeService.ApplyValidation(
                data.ProjectSettings.ExternalYoloDataset,
                sourceBefore,
                acceptSourceIdentity: true);

            PythonModelRuntimeConnectionResult unetConnection = PythonModelRuntimeConnectionService.BuildUnetFolderConnection(
                new PythonModelSettings(),
                unetRoot);
            AssertTrue(unetConnection.SelfTestReport.CanTrain, "U-Net runtime cannot run comparison prediction export: " + unetConnection.SelfTestReport.DetailText);
            var unetSettings = unetConnection.Settings;
            unetSettings.WeightsPath = unetWeightsPath;
            unetSettings.InferenceImageSize = imageSize;
            unetSettings.MinimumDetectionConfidence = 0F;

            string yoloPythonPath = Path.Combine(yoloRoot, ".venv", "Scripts", "python.exe");
            AssertTrue(File.Exists(yoloPythonPath), "YOLO Python was not found: " + yoloPythonPath);
            var yoloSettings = new PythonModelSettings
            {
                ModelEngine = PythonModelSettings.EngineYoloV8,
                ProjectRootPath = yoloRoot,
                PythonExecutablePath = yoloPythonPath,
                WeightsPath = yoloWeightsPath,
                InferenceImageSize = imageSize,
                MinimumDetectionConfidence = yoloConfidence
            };

            var service = new WpfSegmentationAdapterComparisonRunService();
            WpfSegmentationAdapterComparisonRunResult result = service.Run(new WpfSegmentationAdapterComparisonRunRequest
            {
                Data = data,
                UnetSettings = unetSettings,
                YoloSettings = yoloSettings,
                YoloEngine = PythonModelSettings.EngineYoloV8,
                OutputParentDirectory = Path.Combine(artifactRoot, "model-center-run")
            });
            AssertTrue(result.Succeeded, "external U-Net versus YOLO-seg comparison failed: " + result.Error);
            AssertTrue(result.Comparison?.IsReady == true, "external U-Net versus YOLO-seg comparison did not produce a common-mask report");
            AssertTrue(File.Exists(result.Comparison.ReportPath), "external U-Net versus YOLO-seg comparison report was not written");
            AssertTrue(File.Exists(result.UnetPredictionManifestPath), "external U-Net prediction manifest was not written");
            AssertTrue(File.Exists(result.YoloPredictionManifestPath), "external YOLO-seg prediction manifest was not written");

            YoloExternalDatasetIntakeReport sourceAfter = YoloExternalDatasetIntakeService.Build(
                dataYamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(sourceAfter.IsReady, "external segmentation source could not be revalidated after comparison: " + string.Join(" / ", sourceAfter.Errors));
            AssertEqual(sourceBefore.SourceFingerprintSha256, sourceAfter.SourceFingerprintSha256);

            File.WriteAllLines(Path.Combine(artifactRoot, "summary.txt"), new[]
            {
                "REAL_EXTERNAL_SEGMENTATION_ADAPTER_COMPARISON completed.",
                "externalDataYaml=" + dataYamlPath,
                "sourceFingerprintBefore=" + sourceBefore.SourceFingerprintSha256,
                "sourceFingerprintAfter=" + sourceAfter.SourceFingerprintSha256,
                "unetWeights=" + unetWeightsPath,
                "yoloWeights=" + yoloWeightsPath,
                "yoloConfidence=" + yoloConfidence.ToString("0.####", CultureInfo.InvariantCulture),
                "canonicalExport=" + result.CanonicalExportRootPath,
                "unetPredictionManifest=" + result.UnetPredictionManifestPath,
                "yoloPredictionManifest=" + result.YoloPredictionManifestPath,
                "comparisonReport=" + result.Comparison.ReportPath,
                "testImageCount=" + result.Comparison.Baseline.ImageCount.ToString(CultureInfo.InvariantCulture),
                "unetMeanDice=" + result.Comparison.Baseline.MeanDice.ToString("0.######", CultureInfo.InvariantCulture),
                "unetMeanIoU=" + result.Comparison.Baseline.MeanIoU.ToString("0.######", CultureInfo.InvariantCulture),
                "yoloMeanDice=" + result.Comparison.Candidate.MeanDice.ToString("0.######", CultureInfo.InvariantCulture),
                "yoloMeanIoU=" + result.Comparison.Candidate.MeanIoU.ToString("0.######", CultureInfo.InvariantCulture)
            });
            Console.WriteLine("REAL_EXTERNAL_SEGMENTATION_ADAPTER_COMPARISON summary=" + Path.Combine(artifactRoot, "summary.txt"));
            return 0;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(artifactRoot))
            {
                Directory.CreateDirectory(artifactRoot);
                File.WriteAllText(Path.Combine(artifactRoot, "failure.txt"), ex.ToString());
            }
            Console.Error.WriteLine("FAIL real external segmentation adapter comparison: " + ex.Message);
            return 1;
        }
    }

    private static float GetYoloConfidenceArgument(string[] args)
    {
        string text = GetArgumentValue(args, "--yolo-confidence", "0.25");
        bool parsed = float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float confidence);
        AssertTrue(
            parsed && confidence >= 0F && confidence <= 1F,
            "--yolo-confidence must be a number from 0 to 1.");
        return confidence;
    }
}
