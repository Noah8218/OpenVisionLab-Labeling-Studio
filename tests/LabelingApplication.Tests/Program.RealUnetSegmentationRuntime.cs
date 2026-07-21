using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunRealUnetSegmentationRuntimeSmoke(string[] args)
    {
        string artifactRoot = string.Empty;
        Process pythonProcess = null;
        CCommunicationLearning communication = null;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            string repositoryRoot = FindRepositoryRoot();
            string unetRoot = Path.GetFullPath(GetArgumentValue(args, "--unet-root", @"C:\Git\unet"));
            string artifactName = "real-unet-segmentation-runtime-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            artifactRoot = Path.GetFullPath(GetArgumentValue(args, "--artifact-root", Path.Combine(repositoryRoot, "artifacts", artifactName)));
            string runName = GetArgumentValue(args, "--run-name", "openvisionlab-unet-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            int timeoutSeconds = GetPositiveArgument(args, "--timeout-seconds", 180);
            int imageSize = GetPositiveArgument(args, "--image-size", 32);
            int batchSize = GetPositiveArgument(args, "--batch", 1);
            int epochCount = GetPositiveArgument(args, "--epochs", 1);
            string device = GetArgumentValue(args, "--device", "cpu").Trim();
            string externalDataYamlPath = GetArgumentValue(args, "--external-data-yaml", string.Empty);
            if (!string.IsNullOrWhiteSpace(externalDataYamlPath))
            {
                externalDataYamlPath = Path.GetFullPath(externalDataYamlPath);
            }
            bool usesExternalNativeYolo = !string.IsNullOrWhiteSpace(externalDataYamlPath);

            AssertTrue(Directory.Exists(unetRoot), "U-Net runtime root was not found: " + unetRoot);
            AssertTrue(!Directory.Exists(artifactRoot), "refusing to overwrite an existing U-Net smoke artifact: " + artifactRoot);
            AssertTrue(!string.IsNullOrWhiteSpace(runName) && string.Equals(runName, Path.GetFileName(runName), StringComparison.Ordinal), "U-Net run name must be a single folder name.");

            Directory.CreateDirectory(artifactRoot);
            var connection = PythonModelRuntimeConnectionService.BuildUnetFolderConnection(new PythonModelSettings(), unetRoot);
            AssertEqual(PythonModelSettings.EngineUnet, connection.Settings.ModelEngine);
            AssertTrue(connection.SelfTestReport.CanTrain, "U-Net runtime self-test did not permit training: " + connection.SelfTestReport.DetailText);
            AssertTrue(File.Exists(connection.Settings.PythonExecutablePath), "U-Net Python was not found: " + connection.Settings.PythonExecutablePath);
            AssertTrue(File.Exists(connection.Settings.ClientScriptPath), "U-Net worker was not found: " + connection.Settings.ClientScriptPath);

            string outputRoot = Path.Combine(artifactRoot, "app-output");
            CData data;
            string sourceBefore;
            if (usesExternalNativeYolo)
            {
                YoloExternalDatasetIntakeReport externalReport = YoloExternalDatasetIntakeService.Build(
                    externalDataYamlPath,
                    LabelingDatasetPurpose.Segmentation);
                AssertTrue(externalReport.IsReady, "external U-Net runtime source is not ready: " + string.Join(" / ", externalReport.Errors));
                data = new CData();
                data.ConfigureOutputRoot(outputRoot);
                data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
                data.ProjectSettings.ExternalYoloDataset.DataYamlFilePath = externalReport.DataYamlFilePath;
                data.ProjectSettings.ExternalYoloDataset.DatasetPurpose = LabelingDatasetPurpose.Segmentation;
                data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;
                YoloExternalDatasetIntakeService.ApplyValidation(
                    data.ProjectSettings.ExternalYoloDataset,
                    externalReport,
                    acceptSourceIdentity: true);
                sourceBefore = externalReport.SourceFingerprintSha256;
            }
            else
            {
                data = CreateUnetSegmentationFixture(outputRoot);
                sourceBefore = UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data);
            }
            data.ProjectSettings.PythonModel.ModelEngine = PythonModelSettings.EngineUnet;
            data.ProjectSettings.PythonModel.ProjectRootPath = connection.Settings.ProjectRootPath;
            data.ProjectSettings.PythonModel.PythonExecutablePath = connection.Settings.PythonExecutablePath;
            data.ProjectSettings.PythonModel.ClientScriptPath = connection.Settings.ClientScriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = connection.Settings.WeightsPath;
            data.ProjectSettings.PythonModel.ImageRootPath = usesExternalNativeYolo
                ? YoloExternalDatasetIntakeService.Build(externalDataYamlPath, LabelingDatasetPurpose.Segmentation).Valid.ImageDirectoryPath
                : Path.Combine(outputRoot, "data", "valid", "images");
            data.ProjectSettings.PythonModel.AutoStartClient = false;
            data.TranningParam.imageSize = imageSize;
            data.TranningParam.batch = batchSize;
            data.TranningParam.epoch = epochCount;

            int firstPort = GetAvailableTcpPort();
            communication = new CCommunicationLearning(startListen: false, port: firstPort);
            AssertTrue(communication.Start(), "U-Net TCP listener did not start");
            pythonProcess = StartRealYoloV8TrainingClient(
                connection.Settings.PythonExecutablePath,
                connection.Settings.ClientScriptPath,
                unetRoot,
                data.ProjectSettings.PythonModel.ImageRootPath,
                connection.Settings.WeightsPath,
                firstPort,
                imageSize: imageSize,
                stdout,
                stderr,
                device: device);
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(30)),
                BuildRealYoloSmokeFailure("U-Net worker did not connect", stdout, stderr));
            AssertTrue(communication.SendHealthCheck("unet-health"), "U-Net worker health check was not sent");
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().WorkerTrainingModels.Contains("unet", StringComparer.OrdinalIgnoreCase), TimeSpan.FromSeconds(15)),
                BuildRealYoloSmokeFailure("U-Net worker did not report unet training capability", stdout, stderr));

            var workflow = new YoloTrainingWorkflowService();
            AssertTrue(
                workflow.TryStartTraining(data, communication, runName),
                BuildRealYoloSmokeFailure("U-Net training request was not sent: " + workflow.LastPreparationFailureMessage, stdout, stderr));
            AssertEqual(
                sourceBefore,
                usesExternalNativeYolo
                    ? YoloExternalDatasetIntakeService.Build(externalDataYamlPath, LabelingDatasetPurpose.Segmentation).SourceFingerprintSha256
                    : UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data));

            AssertTrue(
                WaitUntil(
                    () => IsUnetTrainingTerminal(communication.GetStatusSnapshot().LastTrainingState),
                    TimeSpan.FromSeconds(timeoutSeconds)),
                BuildRealYoloSmokeFailure("U-Net training did not reach a terminal state", stdout, stderr));
            PythonCommunicationStatus trainingStatus = communication.GetStatusSnapshot();
            AssertTrue(
                string.Equals(trainingStatus.LastTrainingState, "completed", StringComparison.OrdinalIgnoreCase),
                BuildRealYoloSmokeFailure("U-Net training failed. State=" + trainingStatus.LastTrainingState + " Message=" + trainingStatus.LastTrainingMessage, stdout, stderr));

            string bestWeightsPath = Path.Combine(unetRoot, "runs", "segment", runName, "weights", "best.pt");
            AssertTrue(File.Exists(bestWeightsPath), "U-Net training completed without best.pt: " + bestWeightsPath);
            AssertTrue(string.Equals(Path.GetFullPath(bestWeightsPath), Path.GetFullPath(trainingStatus.LastTrainingWeightsPath), StringComparison.OrdinalIgnoreCase), "U-Net completed status did not identify best.pt");
            string canonicalArtifactParent = Path.Combine(
                outputRoot,
                "artifacts",
                usesExternalNativeYolo ? "unet-ext" : "unet-dataset");
            AssertTrue(Directory.Exists(canonicalArtifactParent), "U-Net app-owned dataset export was not produced");
            string canonicalExportRoot = Directory
                .EnumerateDirectories(canonicalArtifactParent)
                .Single(path => File.Exists(Path.Combine(path, "dataset-manifest.json")));
            SegmentationPredictionExportRequest predictionRequest = SegmentationPredictionExportService.BuildRequest(
                SegmentationPredictionExportService.AdapterUnet,
                connection.Settings,
                canonicalExportRoot,
                Path.Combine(artifactRoot, "unet-test-predictions"));
            predictionRequest.WeightsPath = bestWeightsPath;
            predictionRequest.ImageSize = imageSize;
            predictionRequest.Confidence = 0.0D;
            predictionRequest.Device = device;
            SegmentationPredictionExportResult predictionExport = SegmentationPredictionExportService.Run(predictionRequest);
            AssertTrue(predictionExport.Succeeded, "U-Net prediction export failed: " + predictionExport.Error);
            AssertTrue(File.Exists(predictionExport.PredictionManifestPath), "U-Net prediction export did not produce prediction-manifest.jsonl");

            communication.Close();
            StopRealYoloClient(pythonProcess);
            communication.Dispose();
            communication = null;
            pythonProcess = null;

            int secondPort = GetAvailableTcpPort();
            communication = new CCommunicationLearning(startListen: false, port: secondPort);
            AssertTrue(communication.Start(), "U-Net restart TCP listener did not start");
            pythonProcess = StartRealYoloV8TrainingClient(
                connection.Settings.PythonExecutablePath,
                connection.Settings.ClientScriptPath,
                unetRoot,
                data.ProjectSettings.PythonModel.ImageRootPath,
                bestWeightsPath,
                secondPort,
                imageSize: imageSize,
                stdout,
                stderr,
                device: device);
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(30)),
                BuildRealYoloSmokeFailure("restarted U-Net worker did not connect", stdout, stderr));
            AssertTrue(communication.SendModelStatus("unet-restart", ensureLoaded: true), "U-Net restart model status request was not sent");
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().LastModelLoaded, TimeSpan.FromSeconds(30)),
                BuildRealYoloSmokeFailure("restarted U-Net worker did not load best.pt", stdout, stderr));

            string inferenceImagePath = Directory.EnumerateFiles(
                    Path.Combine(canonicalExportRoot, "images", "valid"),
                    "*",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            AssertTrue(File.Exists(inferenceImagePath), "U-Net restart inference image was not found: " + inferenceImagePath);
            AssertTrue(communication.SendDetectImage("unet-restart-detect", "valid", inferenceImagePath, 0F, "unet"), "U-Net restart inference request was not sent");
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().LastDetectionResultAtUtc.HasValue, TimeSpan.FromSeconds(30)),
                BuildRealYoloSmokeFailure("restarted U-Net worker did not return DetectImageResult", stdout, stderr));
            PythonCommunicationStatus inferenceStatus = communication.GetStatusSnapshot();
            AssertTrue(string.IsNullOrWhiteSpace(inferenceStatus.LastError), "U-Net restart inference returned an error: " + inferenceStatus.LastError);

            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "REAL_UNET_SEGMENTATION_RUNTIME completed.",
                "unetRoot=" + unetRoot,
                "python=" + connection.Settings.PythonExecutablePath,
                "worker=" + connection.Settings.ClientScriptPath,
                "externalDataYaml=" + externalDataYamlPath,
                "sourceSha256Before=" + sourceBefore,
                "sourceSha256After=" + (usesExternalNativeYolo
                    ? YoloExternalDatasetIntakeService.Build(externalDataYamlPath, LabelingDatasetPurpose.Segmentation).SourceFingerprintSha256
                    : UnetSegmentationDatasetExportService.ComputeSourceDataTreeSha256(data)),
                "canonicalExport=" + canonicalExportRoot,
                "runName=" + runName,
                "epochs=" + epochCount.ToString(CultureInfo.InvariantCulture),
                "imageSize=" + imageSize.ToString(CultureInfo.InvariantCulture),
                "batch=" + batchSize.ToString(CultureInfo.InvariantCulture),
                "requestedDevice=" + device,
                "bestWeights=" + bestWeightsPath,
                "bestWeightsSha256=" + ComputeFileSha256(bestWeightsPath),
                "unetPredictionManifest=" + predictionExport.PredictionManifestPath,
                "restartModelState=" + inferenceStatus.LastModelState,
                "restartDetectionState=" + inferenceStatus.LastDetectionState,
                "restartDetectionCount=" + inferenceStatus.LastDetectionCount.ToString(CultureInfo.InvariantCulture)
            });
            WriteRealYoloProcessLog(artifactRoot, stdout, stderr);
            Console.WriteLine("REAL_UNET_SEGMENTATION_RUNTIME weights=" + bestWeightsPath);
            Console.WriteLine("REAL_UNET_SEGMENTATION_RUNTIME summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(artifactRoot))
            {
                Directory.CreateDirectory(artifactRoot);
                File.WriteAllText(Path.Combine(artifactRoot, "failure.txt"), ex.ToString());
            }

            Console.Error.WriteLine("FAIL real U-Net segmentation runtime: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            communication?.Close();
            StopRealYoloClient(pythonProcess);
            WriteRealYoloProcessLog(artifactRoot, stdout, stderr);
            communication?.Dispose();
        }
    }

    private static bool IsUnetTrainingTerminal(string state)
    {
        return string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "canceled", StringComparison.OrdinalIgnoreCase);
    }
}
