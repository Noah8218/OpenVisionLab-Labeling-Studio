using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestExternalYoloDatasetIntake()
    {
        string root = CreateTempRoot();
        string recipeName = "codex_external_yolo_" + Guid.NewGuid().ToString("N");
        string recipeDirectory = Path.Combine(AppContext.BaseDirectory, "RECIPE", recipeName);
        try
        {
            string segmentationYamlPath = CreateExternalNativeYoloDataset(root, "segment", segmentation: true, mappingNames: true);
            string segmentationYamlBeforeIntake = File.ReadAllText(segmentationYamlPath);
            YoloExternalDatasetIntakeReport segmentationReport = YoloExternalDatasetIntakeService.Build(
                segmentationYamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(segmentationReport.IsReady, string.Join(Environment.NewLine, segmentationReport.Errors));
            AssertEqual(Path.GetDirectoryName(segmentationYamlPath), segmentationReport.DatasetRootPath);
            AssertEqual(1, segmentationReport.Train.ImageCount);
            AssertEqual(1, segmentationReport.Valid.ImageCount);
            AssertEqual(2, segmentationReport.TotalAnnotationCount);
            AssertEqual("defect", segmentationReport.ClassNames[0]);
            AssertEqual(5, segmentationReport.SourceFileCount);
            AssertTrue(!string.IsNullOrWhiteSpace(segmentationReport.SourceFingerprintSha256), "external intake should record a source fingerprint");
            AssertEqual(segmentationYamlBeforeIntake, File.ReadAllText(segmentationYamlPath));

            string segmentationTrainLabelPath = Path.Combine(
                Path.GetDirectoryName(segmentationYamlPath),
                "labels",
                "train",
                "sample.txt");
            File.WriteAllText(segmentationTrainLabelPath, "0 0.50 0.50 0.20 0.20");
            YoloExternalDatasetIntakeReport invalidSegmentationReport = YoloExternalDatasetIntakeService.Build(
                segmentationYamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(!invalidSegmentationReport.IsReady, "segmentation intake should reject box-only label rows");
            AssertTrue(
                string.Join(" ", invalidSegmentationReport.Errors).Contains("Segmentation labels", StringComparison.Ordinal),
                "segmentation intake should explain the required polygon format");
            File.WriteAllText(segmentationTrainLabelPath, "0 0.10 0.10 0.80 0.10 0.80 0.80");

            string detectionYamlPath = CreateExternalNativeYoloDataset(root, "detect", segmentation: false, mappingNames: false);
            YoloExternalDatasetIntakeReport detectionReport = YoloExternalDatasetIntakeService.Build(
                detectionYamlPath,
                LabelingDatasetPurpose.ObjectDetection);
            AssertTrue(detectionReport.IsReady, string.Join(Environment.NewLine, detectionReport.Errors));
            AssertEqual(1, detectionReport.ClassNames.Count);
            AssertEqual("defect", detectionReport.ClassNames[0]);

            string internalOutputRoot = Path.Combine(root, "recipe-output");
            string modelRoot = Path.Combine(root, "model-root");
            Directory.CreateDirectory(modelRoot);
            string seedWeightsPath = Path.Combine(modelRoot, "yolov8n-seg.pt");
            File.WriteAllBytes(seedWeightsPath, new byte[] { 1, 2, 3, 4 });
            var data = new CData();
            data.ConfigureOutputRoot(internalOutputRoot);
            string internalDataYamlPath = data.DataYamlFilePath;
            string externalYamlBeforeTraining = File.ReadAllText(segmentationYamlPath);
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.ObjectDetection;
            data.ProjectSettings.PythonModel.ModelEngine = PythonModelSettings.EngineYoloV8;
            data.ProjectSettings.PythonModel.ProjectRootPath = modelRoot;
            data.ProjectSettings.ExternalYoloDataset.DataYamlFilePath = segmentationYamlPath;
            data.ProjectSettings.ExternalYoloDataset.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;
            YoloExternalDatasetIntakeService.ApplyValidation(
                data.ProjectSettings.ExternalYoloDataset,
                segmentationReport,
                acceptSourceIdentity: true);

            data.SaveConfig(recipeName);
            CData reloaded = new CData().LoadConfig(recipeName);
            AssertEqual(Path.GetFullPath(segmentationYamlPath), reloaded.ProjectSettings.ExternalYoloDataset.DataYamlFilePath);
            AssertEqual(LabelingDatasetPurpose.Segmentation, reloaded.ProjectSettings.ExternalYoloDataset.DatasetPurpose);
            AssertTrue(reloaded.ProjectSettings.ExternalYoloDataset.UseForTraining, "explicit external training selection should persist with the recipe");
            AssertTrue(reloaded.ProjectSettings.ExternalYoloDataset.LastValidationSucceeded, "validated external profile should persist its readiness snapshot");
            AssertEqual(segmentationReport.SourceFingerprintSha256, reloaded.ProjectSettings.ExternalYoloDataset.SourceFingerprintSha256);
            AssertEqual(segmentationReport.SourceFileCount, reloaded.ProjectSettings.ExternalYoloDataset.SourceFileCount);

            var workflow = new YoloTrainingWorkflowService();
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = false;
            AssertTrue(!workflow.TryPrepareTrainingDataset(data), "an external data.yaml must not be used until explicitly activated");
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;

            int port = GetAvailableTcpPort();
            using var communication = new CCommunicationLearning(startListen: false, port: port);
            using var requestReceived = new ManualResetEventSlim(false);
            AssertTrue(communication.Start(), "external YOLO intake test TCP listener did not start");
            Task mockClient = Task.Run(() => RunMockTrainingPacketCaptureClient(
                port,
                requestReceived,
                request =>
                {
                    AssertEqual("yolov8", request.model);
                    AssertEqual("segment", request.task);
                    AssertEqual("yolov8n-seg.pt", request.weight);
                    AssertEqual(LearningProtocol.NormalizeProtocolPath(Path.GetFullPath(segmentationYamlPath)), request.dataYaml);
                }));
            AssertTrue(WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(5)), "external YOLO intake mock client did not connect");
            AssertTrue(workflow.TryStartTraining(data, communication, "external-seg-source"), "activated external YOLO data.yaml should start training");
            AssertTrue(requestReceived.Wait(TimeSpan.FromSeconds(5)), "external YOLO intake mock client did not receive StartTraining");
            AssertTrue(mockClient.Wait(TimeSpan.FromSeconds(5)), "external YOLO intake mock client did not finish");
            if (mockClient.IsFaulted && mockClient.Exception != null)
            {
                throw mockClient.Exception;
            }

            AssertEqual(internalDataYamlPath, data.DataYamlFilePath);
            AssertEqual(externalYamlBeforeTraining, File.ReadAllText(segmentationYamlPath));
            AssertTrue(data.ProjectSettings.ExternalYoloDataset.LastValidationSucceeded, "training should revalidate the external YAML before sending it");
            AssertEqual(segmentationReport.SourceFingerprintSha256, data.ProjectSettings.ExternalYoloDataset.LastTrainingSourceFingerprintSha256);
            AssertEqual(Path.GetFullPath(segmentationYamlPath), data.ProjectSettings.ExternalYoloDataset.LastTrainingDataYamlFilePath);
            AssertEqual("yolov8", data.ProjectSettings.ExternalYoloDataset.LastTrainingModel);
            AssertEqual("segment", data.ProjectSettings.ExternalYoloDataset.LastTrainingTask);
            AssertEqual("external-seg-source", data.ProjectSettings.ExternalYoloDataset.LastTrainingRunName);
            AssertEqual("yolov8n-seg.pt", data.ProjectSettings.ExternalYoloDataset.LastTrainingWeightFile);
            AssertEqual(Path.GetFullPath(seedWeightsPath), data.ProjectSettings.ExternalYoloDataset.LastTrainingResolvedWeightFile);
            AssertEqual(ComputeFileSha256(seedWeightsPath), data.ProjectSettings.ExternalYoloDataset.LastTrainingWeightSha256);
            AssertEqual(externalYamlBeforeTraining, File.ReadAllText(segmentationYamlPath));
            YoloExternalDatasetIntakeReport postTrainingReport = YoloExternalDatasetIntakeService.Build(
                segmentationYamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(postTrainingReport.IsReady, string.Join(Environment.NewLine, postTrainingReport.Errors));
            AssertEqual(segmentationReport.SourceFingerprintSha256, postTrainingReport.SourceFingerprintSha256);

            File.WriteAllText(segmentationTrainLabelPath, "0 0.15 0.15 0.80 0.15 0.80 0.80");
            YoloExternalDatasetIntakeReport changedSourceReport = YoloExternalDatasetIntakeService.Build(
                segmentationYamlPath,
                LabelingDatasetPurpose.Segmentation);
            AssertTrue(changedSourceReport.IsReady, string.Join(Environment.NewLine, changedSourceReport.Errors));
            AssertTrue(
                !string.Equals(segmentationReport.SourceFingerprintSha256, changedSourceReport.SourceFingerprintSha256, StringComparison.OrdinalIgnoreCase),
                "a valid external label change must change the stored source fingerprint");
            YoloExternalDatasetIntakeService.ApplyValidation(data.ProjectSettings.ExternalYoloDataset, changedSourceReport);
            AssertTrue(
                string.Equals(
                    segmentationReport.SourceFingerprintSha256,
                    data.ProjectSettings.ExternalYoloDataset.SourceFingerprintSha256,
                    StringComparison.OrdinalIgnoreCase),
                "a background readiness refresh must not silently accept a changed external source");
            AssertTrue(!workflow.TryPrepareTrainingDataset(data), "external source changes after activation must fail closed");
            AssertTrue(
                workflow.LastPreparationFailureMessage.Contains("source files changed", StringComparison.OrdinalIgnoreCase),
                "source identity failure should explain that the external source changed");
            AssertTrue(!data.ProjectSettings.ExternalYoloDataset.UseForTraining, "source identity failure must deactivate the external training input");
            AssertTrue(!data.ProjectSettings.ExternalYoloDataset.LastValidationSucceeded, "source identity failure must require explicit reactivation");
            AssertTrue(data.ProjectSettings.ExternalYoloDataset.RequiresExplicitReactivation, "source identity failure must block fallback to the internal recipe dataset");
            AssertTrue(!workflow.TryPrepareTrainingDataset(data), "a source identity block must not silently fall back to the internal recipe dataset");

            YoloExternalDatasetIntakeService.ApplyValidation(data.ProjectSettings.ExternalYoloDataset, changedSourceReport, acceptSourceIdentity: true);
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;
            AssertTrue(!data.ProjectSettings.ExternalYoloDataset.RequiresExplicitReactivation, "explicit revalidation should clear the source identity block");
            AssertTrue(workflow.TryPrepareTrainingDataset(data), "an explicitly revalidated external source should be ready again");
        }
        finally
        {
            if (Directory.Exists(recipeDirectory))
            {
                Directory.Delete(recipeDirectory, recursive: true);
            }

            DeleteTempRoot(root);
        }
    }

    private static string CreateExternalNativeYoloDataset(string root, string name, bool segmentation, bool mappingNames)
    {
        string datasetRoot = Path.Combine(root, name);
        string trainImagePath = Path.Combine(datasetRoot, "images", "train", "sample.png");
        string validImagePath = Path.Combine(datasetRoot, "images", "val", "sample.png");
        string trainLabelPath = Path.Combine(datasetRoot, "labels", "train", "sample.txt");
        string validLabelPath = Path.Combine(datasetRoot, "labels", "val", "sample.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(trainImagePath));
        Directory.CreateDirectory(Path.GetDirectoryName(validImagePath));
        Directory.CreateDirectory(Path.GetDirectoryName(trainLabelPath));
        Directory.CreateDirectory(Path.GetDirectoryName(validLabelPath));
        using (Bitmap trainImage = CreateSolidBitmap(24, 24, Color.White))
        using (Bitmap validImage = CreateSolidBitmap(24, 24, Color.Black))
        {
            trainImage.Save(trainImagePath);
            validImage.Save(validImagePath);
        }

        string label = segmentation
            ? "0 0.10 0.10 0.80 0.10 0.80 0.80"
            : "0 0.50 0.50 0.40 0.40";
        File.WriteAllText(trainLabelPath, label);
        File.WriteAllText(validLabelPath, label);
        string yamlPath = Path.Combine(datasetRoot, "data.yaml");
        File.WriteAllText(
            yamlPath,
            mappingNames
                ? "path: .\ntrain: images/train\nval: images/val\nnames:\n  0: defect\n"
                : "path: .\ntrain: images/train\nval: images/val\nnames:\n  - defect\n");
        return yamlPath;
    }
}
