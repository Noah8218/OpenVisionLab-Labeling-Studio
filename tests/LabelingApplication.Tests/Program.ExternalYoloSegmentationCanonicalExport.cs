using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestExternalYoloSegmentationCanonicalExport()
    {
        string root = CreateTempRoot();
        try
        {
            string yamlPath = CreateExternalSegmentationCanonicalFixture(root, "ready");
            string sourceSnapshotBefore = CaptureExternalSourceSnapshot(Path.GetDirectoryName(yamlPath));
            YoloExternalDatasetSourcePacket packet = YoloExternalDatasetIntakeService.ReadValidatedSourcePacket(
                yamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(packet.IsReady, string.Join(Environment.NewLine, packet.Report.Errors));
            AssertEqual(3, packet.Entries.Count);
            AssertEqual("train", packet.Entries[0].Split);
            AssertEqual(2, packet.Report.ClassNames.Count);

            var data = new CData();
            data.ConfigureOutputRoot(Path.Combine(root, "recipe-output"));
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ProjectSettings.PythonModel.ModelEngine = PythonModelSettings.EngineUnet;
            data.ProjectSettings.ExternalYoloDataset.DataYamlFilePath = yamlPath;
            data.ProjectSettings.ExternalYoloDataset.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;
            YoloExternalDatasetIntakeService.ApplyValidation(
                data.ProjectSettings.ExternalYoloDataset,
                packet.Report,
                acceptSourceIdentity: true);

            UnetSegmentationDatasetExportResult export = ExternalYoloSegmentationCanonicalExportService.Export(data);
            AssertTrue(export.IsReady, string.Join(Environment.NewLine, export.Errors));
            AssertEqual(3, export.ImageCount);
            AssertEqual(3, export.PositiveMaskImageCount);
            AssertEqual(packet.Report.SourceFingerprintSha256, export.SourceDataTreeSha256Before);
            AssertEqual(packet.Report.SourceFingerprintSha256, export.SourceDataTreeSha256After);
            AssertTrue(export.OutputRootPath.Contains("unet-ext", StringComparison.OrdinalIgnoreCase), "external source must use a separately named app-owned canonical artifact root");
            AssertTrue(File.Exists(Path.Combine(export.OutputRootPath, "classes.json")), "external canonical export should write its U-Net class contract");
            AssertEqual(sourceSnapshotBefore, CaptureExternalSourceSnapshot(Path.GetDirectoryName(yamlPath)));

            UnetSegmentationDatasetExportManifest manifest = JsonConvert.DeserializeObject<UnetSegmentationDatasetExportManifest>(
                File.ReadAllText(Path.Combine(export.OutputRootPath, "dataset-manifest.json")));
            AssertEqual(yamlPath, manifest.SourceRecipeRootPath);
            AssertEqual(2, manifest.Classes.Count);
            AssertEqual(3, manifest.Splits.Sum(split => split.Images.Count));
            AssertTrue(manifest.Splits.Any(split => split.Split == "valid"), "native val split should be normalized to the U-Net valid split");

            string validMaskPath = manifest.Splits
                .Single(split => split.Split == "valid")
                .Images.Single()
                .ExportMaskRelativePath.Replace('/', Path.DirectorySeparatorChar);
            using (var validMask = new Bitmap(Path.Combine(export.OutputRootPath, validMaskPath)))
            {
                AssertEqual(2, validMask.GetPixel(8, 8).R);
            }

            UnetSegmentationDatasetExportResult reused = ExternalYoloSegmentationCanonicalExportService.Export(data);
            AssertTrue(reused.IsReady, string.Join(Environment.NewLine, reused.Errors));
            AssertTrue(reused.ReusedExistingArtifact, "unchanged external source should reuse the immutable canonical artifact");
            AssertEqual(export.OutputRootPath, reused.OutputRootPath);

            var comparisonService = new WpfSegmentationAdapterComparisonRunService();
            WpfSegmentationAdapterComparisonContext externalComparisonContext = comparisonService.BuildContext(
                data,
                new ModelRegistrySettings(),
                data.ProjectSettings.PythonModel);
            AssertTrue(externalComparisonContext.CanonicalDatasetText.Contains("외부 native YOLO", StringComparison.Ordinal), "comparison context should disclose the selected external native YOLO source instead of describing it as recipe labels");

            var workflow = new YoloTrainingWorkflowService();
            AssertTrue(workflow.TryPrepareTrainingDataset(data), "explicitly activated native segmentation data.yaml should prepare U-Net training through the canonical export");
            int port = GetAvailableTcpPort();
            using var communication = new CCommunicationLearning(startListen: false, port: port);
            using var requestReceived = new ManualResetEventSlim(false);
            AssertTrue(communication.Start(), "external U-Net canonical export test TCP listener did not start");
            Task mockClient = Task.Run(() => RunMockTrainingPacketCaptureClient(
                port,
                requestReceived,
                request =>
                {
                    AssertEqual("unet", request.model);
                    AssertEqual("segment", request.task);
                    AssertEqual(LearningProtocol.NormalizeProtocolPath(export.OutputRootPath), request.dataYaml);
                }));
            AssertTrue(WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(5)), "external U-Net canonical export mock client did not connect");
            AssertTrue(workflow.TryStartTraining(data, communication, "external-unet-canonical"), "U-Net training request should use the app-owned canonical root, not source data.yaml");
            AssertTrue(requestReceived.Wait(TimeSpan.FromSeconds(5)), "external U-Net canonical export training request was not received");
            AssertTrue(mockClient.Wait(TimeSpan.FromSeconds(5)), "external U-Net canonical export mock client did not finish");
            if (mockClient.IsFaulted && mockClient.Exception != null)
            {
                throw mockClient.Exception;
            }
            AssertEqual(yamlPath, data.ProjectSettings.ExternalYoloDataset.LastTrainingDataYamlFilePath);
            AssertEqual(export.OutputRootPath, data.ProjectSettings.ExternalYoloDataset.LastTrainingRuntimeDataYamlFilePath);
            AssertEqual(sourceSnapshotBefore, CaptureExternalSourceSnapshot(Path.GetDirectoryName(yamlPath)));

            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(yamlPath), "labels", "val", "valid.txt"),
                "1 0.20 0.20 0.80 0.20 0.80 0.80");
            AssertTrue(!workflow.TryPrepareTrainingDataset(data), "U-Net must fail closed after its activated external source changes");
            AssertTrue(workflow.LastPreparationFailureMessage.Contains("source files changed", StringComparison.OrdinalIgnoreCase), "U-Net source identity failure should name the changed external source");
            AssertTrue(!data.ProjectSettings.ExternalYoloDataset.UseForTraining, "changed external U-Net source must require explicit reactivation");

            string duplicateYamlPath = CreateExternalSegmentationCanonicalFixture(root, "duplicate");
            string duplicateValidImage = Path.Combine(Path.GetDirectoryName(duplicateYamlPath), "images", "val", "valid.png");
            File.Copy(Path.Combine(Path.GetDirectoryName(duplicateYamlPath), "images", "train", "train.png"), duplicateValidImage, overwrite: true);
            var duplicateData = new CData();
            duplicateData.ConfigureOutputRoot(Path.Combine(root, "duplicate-output"));
            duplicateData.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            UnetSegmentationDatasetExportResult duplicate = ExternalYoloSegmentationCanonicalExportService.Export(duplicateData, duplicateYamlPath);
            AssertTrue(!duplicate.IsReady, "external canonical export must reject duplicate image content across native splits");
            AssertTrue(duplicate.Errors.Any(error => error.Contains("duplicate image content across splits", StringComparison.OrdinalIgnoreCase)), "duplicate split content should be explicit");

            string overlapYamlPath = CreateExternalSegmentationCanonicalFixture(root, "overlap");
            string overlapLabelPath = Path.Combine(Path.GetDirectoryName(overlapYamlPath), "labels", "train", "train.txt");
            File.WriteAllText(overlapLabelPath,
                "0 0.10 0.10 0.70 0.10 0.70 0.70\n"
                + "1 0.30 0.30 0.90 0.30 0.90 0.90");
            var overlapData = new CData();
            overlapData.ConfigureOutputRoot(Path.Combine(root, "overlap-output"));
            overlapData.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            UnetSegmentationDatasetExportResult overlap = ExternalYoloSegmentationCanonicalExportService.Export(overlapData, overlapYamlPath);
            AssertTrue(!overlap.IsReady, "external canonical export must reject native YOLO polygons that overlap across classes");
            AssertTrue(overlap.Errors.Any(error => error.Contains("different classes overlap", StringComparison.OrdinalIgnoreCase)), "cross-class polygon overlap should be explicit");
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CreateExternalSegmentationCanonicalFixture(string root, string name)
    {
        string datasetRoot = Path.Combine(root, name);
        foreach ((string split, string fileName, Color color, string label) in new[]
        {
            ("train", "train.png", Color.White, "0 0.10 0.10 0.70 0.10 0.70 0.70"),
            ("val", "valid.png", Color.Black, "1 0.10 0.10 0.70 0.10 0.70 0.70"),
            ("test", "test.png", Color.Gray, "0 0.20 0.20 0.80 0.20 0.80 0.80")
        })
        {
            string imagePath = Path.Combine(datasetRoot, "images", split, fileName);
            string labelPath = Path.Combine(datasetRoot, "labels", split, Path.ChangeExtension(fileName, ".txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
            Directory.CreateDirectory(Path.GetDirectoryName(labelPath));
            using Bitmap image = CreateSolidBitmap(24, 24, color);
            image.Save(imagePath);
            File.WriteAllText(labelPath, label);
        }

        string yamlPath = Path.Combine(datasetRoot, "data.yaml");
        File.WriteAllText(
            yamlPath,
            "path: .\ntrain: images/train\nval: images/val\ntest: images/test\nnames:\n  0: surface\n  1: edge\n");
        return yamlPath;
    }
}
