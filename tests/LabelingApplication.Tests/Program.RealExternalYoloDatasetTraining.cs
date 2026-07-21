using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    // Opt-in runtime evidence only: it never runs in the default test suite or registers a candidate.
    private static int RunRealExternalYoloDatasetTraining(string[] args)
    {
        string artifactRoot = string.Empty;
        Process pythonProcess = null;
        CCommunicationLearning communication = null;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            string repositoryRoot = FindRepositoryRoot();
            string engine = GetArgumentValue(args, "--engine", "yolov8").Trim().ToLowerInvariant();
            AssertTrue(engine == "yolov5" || engine == "yolov8" || engine == "yolo11", "external YOLO training engine must be yolov5, yolov8, or yolo11: " + engine);
            bool useYoloV5 = engine == "yolov5";
            bool useYolo11 = engine == "yolo11";
            LabelingDatasetPurpose purpose = ResolveExternalDatasetPurpose(args, useYoloV5);
            AssertTrue(!useYoloV5 || purpose == LabelingDatasetPurpose.ObjectDetection, "the local YOLOv5 smoke supports object-detection data only");
            string defaultDataYamlPath = purpose == LabelingDatasetPurpose.Segmentation
                ? @"D:\라벨테스트\EasyMatch_Labeling_Dataset_300\EasyMatch_Labeling_Dataset_300\segmentation\data.yaml"
                : @"D:\라벨테스트\EasyMatch_Die_Array_500(1)\EasyMatch_Die_Array_500\object_detection\data.yaml";
            string dataYamlPath = Path.GetFullPath(GetArgumentValue(
                args,
                "--data-yaml",
                defaultDataYamlPath));
            string sourceRoot = Path.GetDirectoryName(dataYamlPath) ?? string.Empty;
            string defaultYoloRoot = useYoloV5 ? @"C:\Git\yolov5" : @"C:\Git\yolov8";
            string yoloRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--yolo-root",
                GetArgumentValue(args, useYoloV5 ? "--yolov5-root" : useYolo11 ? "--yolo11-root" : "--yolov8-root", defaultYoloRoot)));
            string modelRoot = useYoloV5 ? Path.Combine(yoloRoot, "yolov5Master") : yoloRoot;
            string pythonPath = Path.GetFullPath(GetArgumentValue(
                args,
                "--python-exe",
                Path.Combine(yoloRoot, ".venv", "Scripts", "python.exe")));
            string clientScriptPath = useYolo11
                ? PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath()
                : Path.Combine(yoloRoot, "labeling_tcp_client.py");
            string defaultSeedWeightsPath = Path.Combine(yoloRoot, useYoloV5
                ? "yolov5s.pt"
                : useYolo11
                    ? purpose == LabelingDatasetPurpose.Segmentation
                        ? "yolo11n-seg.pt"
                        : "yolo11n.pt"
                : purpose == LabelingDatasetPurpose.Segmentation
                    ? "yolov8n-seg.pt"
                    : "yolov8n.pt");
            string seedWeightsPath = Path.GetFullPath(GetArgumentValue(args, "--weights", defaultSeedWeightsPath));
            int epochCount = GetPositiveArgument(args, "--epochs", 1);
            int imageSize = GetPositiveArgument(args, "--image-size", 320);
            int batchSize = GetPositiveArgument(args, "--batch", 4);
            int timeoutSeconds = GetPositiveArgument(args, "--timeout-seconds", 600);
            string device = GetArgumentValue(args, "--device", "cpu").Trim();
            string taskFolder = purpose == LabelingDatasetPurpose.Segmentation ? "segment" : "detect";
            string runFolder = purpose == LabelingDatasetPurpose.Segmentation ? "segment" : "train";
            string runName = GetArgumentValue(
                args,
                "--run-name",
                "openvisionlab-" + engine + "-" + taskFolder + "-external-yaml-e" + epochCount.ToString(CultureInfo.InvariantCulture) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            string runDirectory = useYoloV5
                ? Path.Combine(modelRoot, "runs", "train", runName)
                : Path.Combine(yoloRoot, "runs", runFolder, runName);
            artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(repositoryRoot, "artifacts", "real-external-yolo-dataset-training", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))));

            AssertTrue(File.Exists(dataYamlPath), "external YOLO data.yaml was not found: " + dataYamlPath);
            AssertTrue(Directory.Exists(sourceRoot), "external YOLO source root was not found: " + sourceRoot);
            AssertTrue(Directory.Exists(yoloRoot), "YOLO root was not found: " + yoloRoot);
            AssertTrue(Directory.Exists(modelRoot), "YOLO model root was not found: " + modelRoot);
            AssertTrue(File.Exists(pythonPath), "YOLO Python was not found: " + pythonPath);
            AssertTrue(File.Exists(clientScriptPath), "YOLO TCP adapter was not found: " + clientScriptPath);
            AssertTrue(File.Exists(seedWeightsPath), "YOLO training seed was not found: " + seedWeightsPath);
            AssertTrue(!string.IsNullOrWhiteSpace(runName) && string.Equals(runName, Path.GetFileName(runName), StringComparison.Ordinal), "run name must be a single folder name: " + runName);
            AssertTrue(!Directory.Exists(runDirectory), "refusing to overwrite an existing YOLO training run: " + runDirectory);
            AssertTrue(!Directory.Exists(artifactRoot), "artifact root already exists: " + artifactRoot);

            YoloExternalDatasetIntakeReport intake = YoloExternalDatasetIntakeService.Build(dataYamlPath, purpose);
            AssertTrue(intake.IsReady, "external YOLO data.yaml readiness failed: " + string.Join("; ", intake.Errors));
            AssertTrue(intake.Train.ImageCount > 0 && intake.Valid.ImageCount > 0 && intake.Test.ImageCount > 0, "external YOLO source requires train, val, and test images");
            AssertTrue(intake.TotalAnnotationCount > 0, "external YOLO source requires annotations");

            Directory.CreateDirectory(artifactRoot);
            string sourceYamlBefore = File.ReadAllText(dataYamlPath);
            string sourceYamlSha256 = ComputeFileSha256(dataYamlPath);
            ExternalYoloSourceTreeSnapshot sourceTreeBefore = CaptureExternalYoloSourceTree(sourceRoot);
            File.WriteAllLines(Path.Combine(artifactRoot, "source-tree-before.tsv"), sourceTreeBefore.ManifestLines);
            AppendExternalTrainingProgress(artifactRoot, "source tree snapshot captured");
            var data = new CData();
            data.ConfigureOutputRoot(Path.Combine(artifactRoot, "app-output"));
            AppendExternalTrainingProgress(artifactRoot, "app-owned output configured");
            string internalDataYamlPath = data.DataYamlFilePath;
            data.ProjectSettings.DatasetPurpose = purpose;
            data.ProjectSettings.PythonModel.ModelEngine = useYoloV5
                ? PythonModelSettings.EngineYoloV5
                : useYolo11
                    ? PythonModelSettings.EngineYolo11
                : PythonModelSettings.EngineYoloV8;
            data.ProjectSettings.PythonModel.ProjectRootPath = yoloRoot;
            data.ProjectSettings.PythonModel.PythonExecutablePath = pythonPath;
            data.ProjectSettings.PythonModel.ClientScriptPath = clientScriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = seedWeightsPath;
            data.ProjectSettings.PythonModel.ImageRootPath = sourceRoot;
            data.ProjectSettings.PythonModel.AutoStartClient = false;
            data.ProjectSettings.ExternalYoloDataset.DataYamlFilePath = dataYamlPath;
            data.ProjectSettings.ExternalYoloDataset.DatasetPurpose = purpose;
            data.ProjectSettings.ExternalYoloDataset.UseForTraining = true;
            YoloExternalDatasetIntakeService.ApplyValidation(data.ProjectSettings.ExternalYoloDataset, intake, acceptSourceIdentity: true);
            if (useYoloV5)
            {
                data.TranningParam.cfg = CYolov5TrainingParam.Cfg.yolov5s;
                data.TranningParam.weight = CYolov5TrainingParam.Weight.yolov5s;
            }
            data.TranningParam.imageSize = imageSize;
            data.TranningParam.batch = batchSize;
            data.TranningParam.epoch = epochCount;

            int port = GetAvailableTcpPort();
            communication = new CCommunicationLearning(startListen: false, port: port);
            AssertTrue(communication.Start(), "external YOLO training TCP listener did not start");
            AppendExternalTrainingProgress(artifactRoot, "TCP listener started on port " + port.ToString(CultureInfo.InvariantCulture));
            pythonProcess = StartRealYoloV8TrainingClient(
                pythonPath,
                clientScriptPath,
                modelRoot,
                sourceRoot,
                seedWeightsPath,
                port,
                imageSize,
                stdout,
                stderr,
                useYolo11 ? "yolo11" : string.Empty,
                device);
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(30)),
                BuildRealYoloSmokeFailure("external YOLO training client did not connect", stdout, stderr));
            AppendExternalTrainingProgress(artifactRoot, "worker client connected");

            var workflow = new YoloTrainingWorkflowService();
            AssertTrue(
                workflow.TryStartTraining(data, communication, runName),
                BuildRealYoloSmokeFailure("external YOLO training request was not sent: " + workflow.LastPreparationFailureMessage, stdout, stderr));
            AppendExternalTrainingProgress(artifactRoot, "training request sent");

            bool terminal = WaitUntil(
                () => IsExternalYoloTrainingTerminal(communication.GetStatusSnapshot().LastTrainingState),
                TimeSpan.FromSeconds(timeoutSeconds));
            PythonCommunicationStatus finalStatus = communication.GetStatusSnapshot();
            AssertTrue(
                terminal && string.Equals(finalStatus.LastTrainingState, "completed", StringComparison.OrdinalIgnoreCase),
                BuildRealYoloSmokeFailure(
                    "external YOLO training did not complete. State=" + finalStatus.LastTrainingState + " Message=" + finalStatus.LastTrainingMessage,
                    stdout,
                    stderr));
            AppendExternalTrainingProgress(artifactRoot, "worker completed training");

            string bestWeightsPath = Path.Combine(runDirectory, "weights", "best.pt");
            AssertTrue(File.Exists(bestWeightsPath), "external YOLO training completed without best.pt: " + bestWeightsPath);
            AssertEqual(internalDataYamlPath, data.DataYamlFilePath);
            AssertEqual(sourceYamlBefore, File.ReadAllText(dataYamlPath));
            AssertEqual(sourceYamlSha256, ComputeFileSha256(dataYamlPath));
            ExternalYoloDatasetSettings externalProfile = data.ProjectSettings.ExternalYoloDataset;
            AssertEqual(intake.SourceFingerprintSha256, externalProfile.LastTrainingSourceFingerprintSha256);
            AssertEqual(Path.GetFullPath(dataYamlPath), externalProfile.LastTrainingDataYamlFilePath);
            AssertTrue(File.Exists(externalProfile.LastTrainingRuntimeDataYamlFilePath), "external training provenance must record the runtime data.yaml sent to YOLO");
            AssertTrue(!IsPathWithinRoot(externalProfile.LastTrainingRuntimeDataYamlFilePath, sourceRoot), "external source directory must not receive the runtime data.yaml");
            AssertEqual(useYoloV5 ? "yolov5" : useYolo11 ? "yolo11" : "yolov8", externalProfile.LastTrainingModel);
            AssertEqual(purpose == LabelingDatasetPurpose.Segmentation ? "segment" : "detect", externalProfile.LastTrainingTask);
            AssertEqual(runName, externalProfile.LastTrainingRunName);
            AssertEqual(Path.GetFileName(seedWeightsPath), externalProfile.LastTrainingWeightFile);
            AssertEqual(Path.GetFullPath(seedWeightsPath), externalProfile.LastTrainingResolvedWeightFile);
            AssertEqual(ComputeFileSha256(seedWeightsPath), externalProfile.LastTrainingWeightSha256);
            AssertTrue(!string.IsNullOrWhiteSpace(externalProfile.LastTrainingUtc), "external training provenance must include a request timestamp");
            AssertTrue(!string.IsNullOrWhiteSpace(externalProfile.LastTrainingWeightSha256), "external training provenance must include the seed-weight hash");
            AssertTrue(!string.IsNullOrWhiteSpace(externalProfile.LastTrainingClientScriptSha256), "external training provenance must include the worker-script hash");
            AssertTrue(!IsPathWithinRoot(bestWeightsPath, sourceRoot), "external source directory must not receive trained weights");

            ExternalYoloSourceTreeSnapshot sourceTreeAfter = CaptureExternalYoloSourceTree(sourceRoot);
            File.WriteAllLines(Path.Combine(artifactRoot, "source-tree-after.tsv"), sourceTreeAfter.ManifestLines);
            AssertTrue(sourceTreeBefore.FileCount == sourceTreeAfter.FileCount, "external source file count changed during training");
            AssertTrue(
                string.Equals(sourceTreeBefore.TreeSha256, sourceTreeAfter.TreeSha256, StringComparison.OrdinalIgnoreCase),
                "external source tree SHA-256 changed during training");
            AssertTrue(
                sourceTreeBefore.CacheRelativePaths.SequenceEqual(sourceTreeAfter.CacheRelativePaths, StringComparer.OrdinalIgnoreCase),
                "external source cache paths changed during training");
            AssertTrue(
                sourceTreeBefore.TemporaryDirectoryRelativePaths.SequenceEqual(sourceTreeAfter.TemporaryDirectoryRelativePaths, StringComparer.OrdinalIgnoreCase),
                "external source temporary directories changed during training");

            string copiedWeightsPath = Path.Combine(artifactRoot, "best.pt");
            File.Copy(bestWeightsPath, copiedWeightsPath, overwrite: false);
            File.WriteAllLines(Path.Combine(artifactRoot, "summary.txt"), new[]
            {
                "REAL_EXTERNAL_YOLO_DATASET_TRAINING completed.",
                "engine=" + engine,
                "datasetPurpose=" + purpose,
                "dataYaml=" + dataYamlPath,
                "dataYamlSha256=" + sourceYamlSha256,
                "intake=" + intake.Summary,
                "sourceTreeBeforeFileCount=" + sourceTreeBefore.FileCount.ToString(CultureInfo.InvariantCulture),
                "sourceTreeBeforeSha256=" + sourceTreeBefore.TreeSha256,
                "sourceTreeAfterFileCount=" + sourceTreeAfter.FileCount.ToString(CultureInfo.InvariantCulture),
                "sourceTreeAfterSha256=" + sourceTreeAfter.TreeSha256,
                "sourceCacheFilesBefore=" + sourceTreeBefore.CacheRelativePaths.Count.ToString(CultureInfo.InvariantCulture),
                "sourceCacheFilesAfter=" + sourceTreeAfter.CacheRelativePaths.Count.ToString(CultureInfo.InvariantCulture),
                "sourceTemporaryDirectoriesBefore=" + sourceTreeBefore.TemporaryDirectoryRelativePaths.Count.ToString(CultureInfo.InvariantCulture),
                "sourceTemporaryDirectoriesAfter=" + sourceTreeAfter.TemporaryDirectoryRelativePaths.Count.ToString(CultureInfo.InvariantCulture),
                "epochs=" + epochCount.ToString(CultureInfo.InvariantCulture),
                "imageSize=" + imageSize.ToString(CultureInfo.InvariantCulture),
                "batch=" + batchSize.ToString(CultureInfo.InvariantCulture),
                "requestedDevice=" + device,
                "runName=" + runName,
                "modelRoot=" + modelRoot,
                "workerTrainingState=" + finalStatus.LastTrainingState,
                "workerTrainingMessage=" + finalStatus.LastTrainingMessage,
                "profileSourceFingerprintSha256=" + externalProfile.LastTrainingSourceFingerprintSha256,
                "profileDataYaml=" + externalProfile.LastTrainingDataYamlFilePath,
                "profileRuntimeDataYaml=" + externalProfile.LastTrainingRuntimeDataYamlFilePath,
                "profileModel=" + externalProfile.LastTrainingModel,
                "profileTask=" + externalProfile.LastTrainingTask,
                "profileRunName=" + externalProfile.LastTrainingRunName,
                "profileWeight=" + externalProfile.LastTrainingWeightFile,
                "profileResolvedWeight=" + externalProfile.LastTrainingResolvedWeightFile,
                "profileWeightSha256=" + externalProfile.LastTrainingWeightSha256,
                "profilePython=" + externalProfile.LastTrainingPythonExecutablePath,
                "profileWorkerScript=" + externalProfile.LastTrainingClientScriptPath,
                "profileWorkerScriptSha256=" + externalProfile.LastTrainingClientScriptSha256,
                "bestWeights=" + bestWeightsPath,
                "bestWeightsSha256=" + ComputeFileSha256(bestWeightsPath),
                "copiedWeights=" + copiedWeightsPath
            });
            WriteRealYoloProcessLog(artifactRoot, stdout, stderr);

            Console.WriteLine("REAL_EXTERNAL_YOLO_DATASET_TRAINING weights=" + bestWeightsPath);
            Console.WriteLine("REAL_EXTERNAL_YOLO_DATASET_TRAINING summary=" + Path.Combine(artifactRoot, "summary.txt"));
            return 0;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(artifactRoot))
            {
                Directory.CreateDirectory(artifactRoot);
                File.WriteAllText(Path.Combine(artifactRoot, "failure.txt"), ex.ToString());
            }

            Console.Error.WriteLine("FAIL REAL external YOLO dataset training: " + ex.Message);
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

    private static bool IsExternalYoloTrainingTerminal(string state)
    {
        return string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static LabelingDatasetPurpose ResolveExternalDatasetPurpose(string[] args, bool useYoloV5)
    {
        string value = GetArgumentValue(args, "--purpose", useYoloV5 ? "detection" : "segmentation").Trim();
        if (value.Equals("segmentation", StringComparison.OrdinalIgnoreCase)
            || value.Equals("segment", StringComparison.OrdinalIgnoreCase))
        {
            return LabelingDatasetPurpose.Segmentation;
        }

        if (value.Equals("detection", StringComparison.OrdinalIgnoreCase)
            || value.Equals("detect", StringComparison.OrdinalIgnoreCase)
            || value.Equals("object-detection", StringComparison.OrdinalIgnoreCase))
        {
            return LabelingDatasetPurpose.ObjectDetection;
        }

        throw new ArgumentException("external YOLO training purpose must be detection or segmentation: " + value);
    }

    private static ExternalYoloSourceTreeSnapshot CaptureExternalYoloSourceTree(string sourceRoot)
    {
        string root = Path.GetFullPath(sourceRoot);
        string[] filePaths = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var manifestLines = new List<string>(filePaths.Length);
        var cacheRelativePaths = new List<string>();
        using SHA256 treeHash = SHA256.Create();
        foreach (string filePath in filePaths)
        {
            string relativePath = Path.GetRelativePath(root, filePath).Replace('\\', '/');
            string fileHash = ComputeFileSha256(filePath);
            string manifestLine = relativePath + "\t" + fileHash;
            manifestLines.Add(manifestLine);
            if (relativePath.EndsWith(".cache", StringComparison.OrdinalIgnoreCase))
            {
                cacheRelativePaths.Add(relativePath);
            }

            byte[] entry = Encoding.UTF8.GetBytes(manifestLine + "\n");
            treeHash.TransformBlock(entry, 0, entry.Length, entry, 0);
        }

        treeHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string[] temporaryDirectoryRelativePaths = Directory.EnumerateDirectories(root, "openvisionlab-*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith("openvisionlab-yolov5-training-", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).StartsWith("openvisionlab-ultralytics-label-cache-", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ExternalYoloSourceTreeSnapshot(
            filePaths.Length,
            BitConverter.ToString(treeHash.Hash ?? Array.Empty<byte>()).Replace("-", string.Empty),
            manifestLines,
            cacheRelativePaths,
            temporaryDirectoryRelativePaths);
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        string relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void AppendExternalTrainingProgress(string artifactRoot, string message)
    {
        if (!string.IsNullOrWhiteSpace(artifactRoot))
        {
            File.AppendAllText(
                Path.Combine(artifactRoot, "progress.txt"),
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + (message ?? string.Empty) + Environment.NewLine);
        }
    }

    private sealed class ExternalYoloSourceTreeSnapshot
    {
        public ExternalYoloSourceTreeSnapshot(
            int fileCount,
            string treeSha256,
            IReadOnlyList<string> manifestLines,
            IReadOnlyList<string> cacheRelativePaths,
            IReadOnlyList<string> temporaryDirectoryRelativePaths)
        {
            FileCount = fileCount;
            TreeSha256 = treeSha256 ?? string.Empty;
            ManifestLines = manifestLines ?? Array.Empty<string>();
            CacheRelativePaths = cacheRelativePaths ?? Array.Empty<string>();
            TemporaryDirectoryRelativePaths = temporaryDirectoryRelativePaths ?? Array.Empty<string>();
        }

        public int FileCount { get; }

        public string TreeSha256 { get; }

        public IReadOnlyList<string> ManifestLines { get; }

        public IReadOnlyList<string> CacheRelativePaths { get; }

        public IReadOnlyList<string> TemporaryDirectoryRelativePaths { get; }
    }
}
