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
    private static int RunRealYoloV8AnomalyFolderTraining(string[] args)
    {
        string artifactRoot = string.Empty;
        Process pythonProcess = null;
        CCommunicationLearning communication = null;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            string root = FindRepositoryRoot();
            string sourceRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--source-root",
                @"D:\circular_defect_labeling_dataset_v1\images"));
            string yoloRoot = Path.GetFullPath(GetArgumentValue(args, "--yolov8-root", @"C:\Git\yolov8"));
            string pythonPath = Path.Combine(yoloRoot, ".venv", "Scripts", "python.exe");
            string clientScriptPath = Path.Combine(yoloRoot, "labeling_tcp_client.py");
            string seedWeightsPath = Path.Combine(yoloRoot, "yolov8n-cls.pt");
            int epochCount = GetPositiveArgument(args, "--epochs", 20);
            int imageSize = GetPositiveArgument(args, "--image-size", 128);
            int batchSize = GetPositiveArgument(args, "--batch", 4);
            int timeoutSeconds = GetPositiveArgument(args, "--timeout-seconds", 900);
            string runName = "openvisionlab-yolov8-classify";
            string runDirectory = Path.Combine(yoloRoot, "runs", "classify", runName);
            artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "real-yolov8-anomaly-folder-training", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))));

            AssertTrue(Directory.Exists(sourceRoot), "anomaly source image root was not found: " + sourceRoot);
            AssertTrue(Directory.Exists(yoloRoot), "YOLOv8 root was not found: " + yoloRoot);
            AssertTrue(File.Exists(pythonPath), "YOLOv8 Python was not found: " + pythonPath);
            AssertTrue(File.Exists(clientScriptPath), "YOLOv8 TCP adapter was not found: " + clientScriptPath);
            AssertTrue(File.Exists(seedWeightsPath), "YOLOv8 classification seed was not found: " + seedWeightsPath);
            AssertTrue(!Directory.Exists(runDirectory), "refusing to overwrite an existing YOLOv8 classification run: " + runDirectory);
            AssertTrue(!Directory.Exists(artifactRoot), "artifact root already exists: " + artifactRoot);

            Directory.CreateDirectory(artifactRoot);
            string outputRoot = Path.Combine(artifactRoot, "app-output");
            var data = new CData();
            data.ConfigureOutputRoot(outputRoot);
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.AnomalyDetection;
            data.ProjectSettings.PythonModel.ModelEngine = PythonModelSettings.EngineYoloV8;
            data.ProjectSettings.PythonModel.ProjectRootPath = yoloRoot;
            data.ProjectSettings.PythonModel.PythonExecutablePath = pythonPath;
            data.ProjectSettings.PythonModel.ClientScriptPath = clientScriptPath;
            data.ProjectSettings.PythonModel.WeightsPath = seedWeightsPath;
            data.ProjectSettings.PythonModel.ImageRootPath = sourceRoot;
            data.ProjectSettings.PythonModel.AutoStartClient = false;
            data.ProjectSettings.YoloDataset.ValidationPercent = 20;
            data.ProjectSettings.YoloDataset.TestPercent = 10;
            data.ProjectSettings.YoloDataset.SplitSeed = 17;
            data.TranningParam.imageSize = imageSize;
            data.TranningParam.batch = batchSize;
            data.TranningParam.epoch = epochCount;
            data.ProjectSettings.AnomalyClassification.NormalClassNames.Clear();
            data.ProjectSettings.AnomalyClassification.AbnormalClassNames.Clear();
            data.ProjectSettings.AnomalyClassification.NormalClassNames.Add("normal");
            data.ProjectSettings.AnomalyClassification.AbnormalClassNames.Add("abnormal");

            string[] sourceImages = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(IsAnomalyTrainingImage)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            AssertTrue(sourceImages.Length > 0, "anomaly source image root contains no supported image files: " + sourceRoot);

            var reviewStatus = new AnomalyImageReviewStatusService();
            reviewStatus.LoadReviewStatus(data, sourceImages);
            AnomalyImageReviewFolderImportResult import = reviewStatus.ImportUnreviewedStatesFromParentFolders();
            reviewStatus.SaveReviewStatus(data);
            AssertTrue(import.NormalImageCount > 0, "OK parent folder did not import normal anomaly review states");
            AssertTrue(import.AbnormalImageCount > 0, "NG parent folder did not import abnormal anomaly review states");

            AnomalyClassificationTrainingReadinessReport readiness = AnomalyClassificationTrainingReadinessService.Build(data);
            AssertTrue(readiness.IsReady, "anomaly classification readiness failed: " + string.Join("; ", readiness.Errors));
            AssertTrue(readiness.NormalImageCount > 0 && readiness.AbnormalImageCount > 0, "anomaly source needs both normal and abnormal images");
            AssertTrue(readiness.TrainNormalImageCount > 0 && readiness.TrainAbnormalImageCount > 0, "anomaly source needs both normal and abnormal train images");

            int port = GetAvailableTcpPort();
            communication = new CCommunicationLearning(startListen: false, port: port);
            AssertTrue(communication.Start(), "YOLOv8 anomaly training TCP listener did not start");
            pythonProcess = StartRealYoloV8TrainingClient(
                pythonPath,
                clientScriptPath,
                yoloRoot,
                sourceRoot,
                seedWeightsPath,
                port,
                imageSize,
                stdout,
                stderr);
            AssertTrue(
                WaitUntil(() => communication.GetStatusSnapshot().IsClientConnected, TimeSpan.FromSeconds(30)),
                BuildRealYoloSmokeFailure("YOLOv8 anomaly training client did not connect", stdout, stderr));

            var workflow = new YoloTrainingWorkflowService();
            AssertTrue(
                workflow.TryStartTraining(data, communication),
                BuildRealYoloSmokeFailure("YOLOv8 anomaly training request was not sent: " + workflow.LastPreparationFailureMessage, stdout, stderr));

            string classificationRoot = Path.Combine(outputRoot, AnomalyClassificationDatasetExportService.DefaultFolderName);
            AssertTrue(HasAnomalyTrainingImages(classificationRoot, "train", "normal"), "application anomaly export did not write train/normal images");
            AssertTrue(HasAnomalyTrainingImages(classificationRoot, "train", "abnormal"), "application anomaly export did not write train/abnormal images");
            AssertTrue(HasAnomalyTrainingImages(classificationRoot, "valid", "normal"), "application anomaly export did not write valid/normal images");
            AssertTrue(HasAnomalyTrainingImages(classificationRoot, "test", "abnormal"), "application anomaly export did not write test/abnormal images");

            bool terminal = WaitUntil(
                () => IsAnomalyTrainingTerminal(communication.GetStatusSnapshot().LastTrainingState),
                TimeSpan.FromSeconds(timeoutSeconds));
            PythonCommunicationStatus finalStatus = communication.GetStatusSnapshot();
            AssertTrue(
                terminal && string.Equals(finalStatus.LastTrainingState, "completed", StringComparison.OrdinalIgnoreCase),
                BuildRealYoloSmokeFailure(
                    "YOLOv8 anomaly training did not complete. State=" + finalStatus.LastTrainingState + " Message=" + finalStatus.LastTrainingMessage,
                    stdout,
                    stderr));

            string bestWeightsPath = Path.Combine(runDirectory, "weights", "best.pt");
            AssertTrue(File.Exists(bestWeightsPath), "YOLOv8 anomaly training completed without best.pt: " + bestWeightsPath);
            string copiedWeightsPath = Path.Combine(artifactRoot, "best.pt");
            File.Copy(bestWeightsPath, copiedWeightsPath, overwrite: false);

            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "REAL_YOLOV8_ANOMALY_FOLDER_TRAINING completed.",
                "sourceRoot=" + sourceRoot,
                "sourceImageCount=" + sourceImages.Length.ToString(CultureInfo.InvariantCulture),
                "folderImportNormal=" + import.NormalImageCount.ToString(CultureInfo.InvariantCulture),
                "folderImportAbnormal=" + import.AbnormalImageCount.ToString(CultureInfo.InvariantCulture),
                "classificationRoot=" + classificationRoot,
                "normalImageCount=" + readiness.NormalImageCount.ToString(CultureInfo.InvariantCulture),
                "abnormalImageCount=" + readiness.AbnormalImageCount.ToString(CultureInfo.InvariantCulture),
                "trainNormalImageCount=" + readiness.TrainNormalImageCount.ToString(CultureInfo.InvariantCulture),
                "trainAbnormalImageCount=" + readiness.TrainAbnormalImageCount.ToString(CultureInfo.InvariantCulture),
                "epochs=" + epochCount.ToString(CultureInfo.InvariantCulture),
                "imageSize=" + imageSize.ToString(CultureInfo.InvariantCulture),
                "batch=" + batchSize.ToString(CultureInfo.InvariantCulture),
                "workerTrainingState=" + finalStatus.LastTrainingState,
                "workerTrainingMessage=" + finalStatus.LastTrainingMessage,
                "bestWeights=" + bestWeightsPath,
                "bestWeightsSha256=" + ComputeFileSha256(bestWeightsPath),
                "copiedWeights=" + copiedWeightsPath
            });
            WriteRealYoloProcessLog(artifactRoot, stdout, stderr);

            Console.WriteLine("REAL_YOLOV8_ANOMALY_FOLDER_TRAINING weights=" + bestWeightsPath);
            Console.WriteLine("REAL_YOLOV8_ANOMALY_FOLDER_TRAINING summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(artifactRoot))
            {
                Directory.CreateDirectory(artifactRoot);
                File.WriteAllText(Path.Combine(artifactRoot, "failure.txt"), ex.ToString());
            }

            Console.Error.WriteLine("FAIL REAL YOLOv8 anomaly folder training: " + ex.Message);
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

    private static Process StartRealYoloV8TrainingClient(
        string pythonPath,
        string clientScriptPath,
        string yoloRoot,
        string imageRoot,
        string weightsPath,
        int port,
        int imageSize,
        StringBuilder stdout,
        StringBuilder stderr)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            WorkingDirectory = yoloRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.ArgumentList.Add(clientScriptPath);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--timeout");
        startInfo.ArgumentList.Add("60");
        startInfo.ArgumentList.Add("--retry");
        startInfo.ArgumentList.Add("--retry-delay");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("--weights");
        startInfo.ArgumentList.Add(weightsPath);
        startInfo.ArgumentList.Add("--model-root");
        startInfo.ArgumentList.Add(yoloRoot);
        startInfo.ArgumentList.Add("--image-root");
        startInfo.ArgumentList.Add(imageRoot);
        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add("cpu");
        startInfo.ArgumentList.Add("--img-size");
        startInfo.ArgumentList.Add(imageSize.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--conf");
        startInfo.ArgumentList.Add("0");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, e) => AppendProcessLine(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => AppendProcessLine(stderr, e.Data);

        AssertTrue(process.Start(), "YOLOv8 anomaly training Python client did not start");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static bool IsAnomalyTrainingTerminal(string state)
    {
        return string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnomalyTrainingImage(string path)
    {
        string extension = Path.GetExtension(path) ?? string.Empty;
        return extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnomalyTrainingImages(string datasetRoot, string split, string className)
    {
        string directory = Path.Combine(datasetRoot, split, className);
        return Directory.Exists(directory) && Directory.EnumerateFiles(directory).Any(IsAnomalyTrainingImage);
    }

    private static int GetPositiveArgument(string[] args, string name, int fallback)
    {
        string text = GetArgumentValue(args, name, fallback.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : fallback;
    }
}
