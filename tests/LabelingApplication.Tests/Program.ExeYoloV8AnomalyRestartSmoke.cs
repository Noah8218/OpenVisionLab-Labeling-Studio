using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private const string YoloV8AnomalyDefaultImage = @"C:\Git\Labelling_Application\artifacts\yolov8-cls-training-smoke\circular-defect-real-20260711\dataset-balanced\test\abnormal\033_NG.png";
    private const string YoloV8AnomalyDefaultWeights = @"C:\Git\Labelling_Application\artifacts\yolov8-cls-training-smoke\circular-defect-real-20260711\runs\yolov8n-cls-circular-defect-balanced-e20-img128-20260711\weights\best.pt";

    private static int RunExeYoloV8AnomalyRestartSmoke(string[] args)
    {
        string recipeName = "codex_yolov8_anomaly_restart_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        Process firstProcess = null;
        Process restartedProcess = null;
        string recipeDirectory = string.Empty;
        string lastOpenedRecipePath = string.Empty;
        byte[] previousLastOpenedRecipe = null;
        bool hadLastOpenedRecipe = false;

        try
        {
            string root = FindRepositoryRoot();
            string exePath = Path.GetFullPath(GetArgumentValue(
                args,
                "--exe",
                Path.Combine(root, "artifacts", "run", "Debug", "OpenVisionLab.LabelingStudio.exe")));
            string yoloRoot = Path.GetFullPath(GetArgumentValue(args, "--yolov8-root", @"C:\Git\yolov8"));
            string weightsPath = Path.GetFullPath(GetArgumentValue(args, "--weights", YoloV8AnomalyDefaultWeights));
            string sourceImagePath = Path.GetFullPath(GetArgumentValue(args, "--image", YoloV8AnomalyDefaultImage));
            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "exe-yolov8-anomaly-restart-smoke", recipeName)));
            string screenshotDirectory = Path.Combine(artifactRoot, "screenshots");
            string inputRoot = Path.Combine(artifactRoot, "input");
            string outputRoot = Path.Combine(artifactRoot, "dataset");
            string smokeImagePath = Path.Combine(inputRoot, Path.GetFileName(sourceImagePath));
            string pythonPath = Path.Combine(yoloRoot, ".venv", "Scripts", "python.exe");
            string clientScriptPath = Path.Combine(yoloRoot, "labeling_tcp_client.py");

            AssertTrue(File.Exists(exePath), "YOLOv8 anomaly restart smoke EXE was not found: " + exePath);
            AssertTrue(Directory.Exists(yoloRoot), "YOLOv8 root was not found: " + yoloRoot);
            AssertTrue(File.Exists(pythonPath), "YOLOv8 Python was not found: " + pythonPath);
            AssertTrue(File.Exists(clientScriptPath), "YOLOv8 TCP adapter was not found: " + clientScriptPath);
            AssertTrue(File.Exists(weightsPath), "YOLOv8 classification weights were not found: " + weightsPath);
            AssertTrue(File.Exists(sourceImagePath), "YOLOv8 anomaly smoke image was not found: " + sourceImagePath);

            string exeDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string recipeRoot = Path.Combine(exeDirectory, "RECIPE");
            recipeDirectory = Path.Combine(recipeRoot, recipeName);
            lastOpenedRecipePath = Path.Combine(recipeRoot, ".last-opened-recipe");
            hadLastOpenedRecipe = File.Exists(lastOpenedRecipePath);
            previousLastOpenedRecipe = hadLastOpenedRecipe ? File.ReadAllBytes(lastOpenedRecipePath) : null;

            DeleteDirectoryIfExists(recipeDirectory);
            DeleteDirectoryIfExists(artifactRoot);
            Directory.CreateDirectory(screenshotDirectory);
            Directory.CreateDirectory(inputRoot);
            File.Copy(sourceImagePath, smokeImagePath, overwrite: true);

            firstProcess = StartYoloV8RuntimeSmokeExe(exePath, out IntPtr firstHandle);
            CaptureWorkflowStep(RefreshAutomationRoot(firstProcess, firstHandle), screenshotDirectory, "01_before_recipe_setup");
            CreateDatasetRecipeThroughExe(
                firstProcess,
                firstHandle,
                recipeName,
                outputRoot,
                recipeDirectory,
                screenshotDirectory,
                "\uC774\uC0C1 \uD0D0\uC9C0",
                LabelingDatasetPurpose.AnomalyDetection,
                "normal, abnormal");

            string visionPath = Path.Combine(recipeDirectory, "VISION.xml");
            AssertTrue(WaitUntil(() => File.Exists(visionPath), TimeSpan.FromSeconds(8)), "YOLOv8 anomaly recipe VISION.xml was not created");
            AssertEqual(LabelingDatasetPurpose.AnomalyDetection, ReadRecipeData(visionPath).ProjectSettings.DatasetPurpose);

            ConfigureYoloV8RuntimeThroughExe(
                firstProcess,
                firstHandle,
                inputRoot,
                yoloRoot,
                pythonPath,
                clientScriptPath,
                weightsPath,
                screenshotDirectory,
                confidence: "0",
                timeoutSeconds: "180",
                inferenceImageSize: "128",
                anomalyNormalClasses: "normal",
                anomalyAbnormalClasses: "abnormal",
                anomalyMinimumConfidence: "0.8");
            LoadConfiguredImageRootThroughExe(firstProcess, inputRoot, screenshotDirectory);

            CData savedData = ReadRecipeData(visionPath);
            AssertYoloV8AnomalyRecipeSettings(savedData, yoloRoot, pythonPath, clientScriptPath, weightsPath, inputRoot);
            string savedVisionHash = ComputeFileSha256(visionPath);
            File.Copy(visionPath, Path.Combine(artifactRoot, "saved-before-restart-VISION.xml"), overwrite: true);
            CaptureWorkflowStep(RefreshAutomationRoot(firstProcess, firstHandle), screenshotDirectory, "02_saved_yolov8_anomaly_profile");

            CloseExeSmokeProcess(firstProcess);
            firstProcess = null;
            Thread.Sleep(1_000);

            restartedProcess = StartYoloV8RuntimeSmokeExe(exePath, out IntPtr restartedHandle);
            string expectedImageMarker = Path.GetFileNameWithoutExtension(smokeImagePath);
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        var rootElement = RefreshAutomationRoot(restartedProcess, restartedHandle, bringToFront: false);
                        return ContainsAutomationText(rootElement, recipeName)
                            && ImageRootAppearsLoaded(rootElement, inputRoot, expectedImageMarker);
                    },
                    TimeSpan.FromSeconds(20)),
                "restarted EXE did not restore the saved YOLOv8 anomaly recipe and image queue");
            CaptureWorkflowStep(RefreshAutomationRoot(restartedProcess, restartedHandle), screenshotDirectory, "03_restarted_recipe_restored");

            AssertTrue(
                string.Equals(File.ReadAllText(lastOpenedRecipePath).Trim(), recipeName, StringComparison.Ordinal),
                "restart marker did not preserve the YOLOv8 anomaly smoke recipe");
            CData reopenedData = ReadRecipeData(visionPath);
            AssertYoloV8AnomalyRecipeSettings(reopenedData, yoloRoot, pythonPath, clientScriptPath, weightsPath, inputRoot);
            string reopenedVisionHash = ComputeFileSha256(visionPath);
            File.Copy(visionPath, Path.Combine(artifactRoot, "reopened-before-inference-VISION.xml"), overwrite: true);
            VerifyYoloV8SettingsVisibleAfterRestart(restartedProcess, restartedHandle, weightsPath);
            VerifyAnomalyMappingVisibleAfterRestart(restartedProcess, restartedHandle);

            var inferenceRoot = RefreshAutomationRoot(restartedProcess, restartedHandle);
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(inferenceRoot, "InferenceReviewStageButton")
                    || TryNativeClickAutomationElementByAutomationId(inferenceRoot, "InferenceReviewStageButton"),
                "AI candidate review stage was not selectable after anomaly restart");
            AssertTrue(
                WaitUntil(
                    () => IsAutomationButtonEnabledByAutomationId(
                        RefreshAutomationRoot(restartedProcess, restartedHandle, bringToFront: false),
                        "RightWorkflowInferenceInspectButton"),
                    TimeSpan.FromSeconds(8)),
                "current-image anomaly inference did not become available after restart");
            inferenceRoot = RefreshAutomationRoot(restartedProcess, restartedHandle);
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(inferenceRoot, "RightWorkflowInferenceInspectButton")
                    || TryInvokeAutomationButton(inferenceRoot, "\uD604\uC7AC \uAC80\uC0AC"),
                "current-image anomaly inference was not invokable after restart");

            string inferenceStatus = string.Empty;
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        var latestRoot = RefreshAutomationRoot(restartedProcess, restartedHandle, bringToFront: false);
                        inferenceStatus = ReadExeInferenceStatusSnapshot(latestRoot);
                        return IsExeTrainedInferenceFinished(latestRoot, inferenceStatus);
                    },
                    TimeSpan.FromMinutes(4)),
                "first YOLOv8 anomaly inference did not finish after restart");
            AssertTrue(!IsExeTrainedInferenceFailure(inferenceStatus), "first YOLOv8 anomaly inference failed after restart: " + inferenceStatus);
            AssertTrue(inferenceStatus.Contains("YOLOv8", StringComparison.OrdinalIgnoreCase), "anomaly inference status did not identify YOLOv8: " + inferenceStatus);
            AssertTrue(
                inferenceStatus.Contains("yolov8n-cls-circular-defect-balanced-e20-img128-20260711", StringComparison.OrdinalIgnoreCase),
                "anomaly inference status did not identify the saved classification best.pt: " + inferenceStatus);
            AssertTrue(inferenceStatus.Contains("\uD6C4\uBCF4", StringComparison.Ordinal), "anomaly inference status did not report a candidate count: " + inferenceStatus);
            AssertTrue(!inferenceStatus.Contains("\uD6C4\uBCF4 0", StringComparison.Ordinal), "YOLOv8 anomaly smoke returned no classification candidate: " + inferenceStatus);

            AssertTrue(
                WaitUntil(
                    () => ReadAnomalyReviewState(reopenedData, smokeImagePath) == AnomalyImageReviewState.Abnormal,
                    TimeSpan.FromSeconds(8)),
                "first anomaly inference did not persist the expected Abnormal review state");
            CaptureWorkflowStep(RefreshAutomationRoot(restartedProcess, restartedHandle), screenshotDirectory, "04_first_abnormal_inference_after_restart");

            CData inferredData = ReadRecipeData(visionPath);
            AssertYoloV8AnomalyRecipeSettings(inferredData, yoloRoot, pythonPath, clientScriptPath, weightsPath, inputRoot);
            string inferredVisionHash = ComputeFileSha256(visionPath);
            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "EXE YOLOv8 anomaly restart smoke passed.",
                "recipe=" + recipeName,
                "sourceImage=" + sourceImagePath,
                "smokeImage=" + smokeImagePath,
                "weights=" + weightsPath,
                "weightsSha256=" + ComputeFileSha256(weightsPath),
                "savedVisionSha256=" + savedVisionHash,
                "reopenedVisionSha256=" + reopenedVisionHash,
                "inferredVisionSha256=" + inferredVisionHash,
                "engine=" + reopenedData.ProjectSettings.PythonModel.ModelEngine,
                "anomalyState=" + ReadAnomalyReviewState(reopenedData, smokeImagePath),
                "inferenceStatus=" + inferenceStatus,
                "screenshots=" + screenshotDirectory
            }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Console.WriteLine("EXE_YOLOV8_ANOMALY_RESTART_SMOKE recipe=" + recipeName);
            Console.WriteLine("EXE_YOLOV8_ANOMALY_RESTART_SMOKE weights=" + weightsPath);
            Console.WriteLine("EXE_YOLOV8_ANOMALY_RESTART_SMOKE inferenceStatus=" + inferenceStatus);
            Console.WriteLine("EXE_YOLOV8_ANOMALY_RESTART_SMOKE anomalyState=Abnormal");
            Console.WriteLine("EXE_YOLOV8_ANOMALY_RESTART_SMOKE summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL EXE YOLOv8 anomaly restart smoke: " + ex.Message);
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            CloseExeSmokeProcess(restartedProcess);
            CloseExeSmokeProcess(firstProcess);
            RestoreLastOpenedRecipe(lastOpenedRecipePath, hadLastOpenedRecipe, previousLastOpenedRecipe);
            DeleteDirectoryIfExists(recipeDirectory);
        }
    }

    private static void AssertYoloV8AnomalyRecipeSettings(
        CData data,
        string yoloRoot,
        string pythonPath,
        string clientScriptPath,
        string weightsPath,
        string imageRoot)
    {
        AssertEqual(LabelingDatasetPurpose.AnomalyDetection, data.ProjectSettings.DatasetPurpose);
        PythonModelSettings settings = data.ProjectSettings.PythonModel;
        AssertEqual(PythonModelSettings.EngineYoloV8, settings.ModelEngine);
        AssertPathEqual(yoloRoot, settings.ProjectRootPath, "saved YOLOv8 project root mismatch");
        AssertPathEqual(pythonPath, settings.PythonExecutablePath, "saved YOLOv8 Python mismatch");
        AssertPathEqual(clientScriptPath, settings.ClientScriptPath, "saved YOLOv8 client script mismatch");
        AssertPathEqual(weightsPath, settings.WeightsPath, "saved YOLOv8 classification weights mismatch");
        AssertPathEqual(imageRoot, settings.ImageRootPath, "saved YOLOv8 anomaly image root mismatch");
        AssertEqual(128, settings.InferenceImageSize);
        AssertTrue(Math.Abs(settings.MinimumDetectionConfidence) < 0.0001F, "saved YOLOv8 classification candidate confidence should be 0");
        AssertTrue(settings.AutoStartClient, "saved YOLOv8 anomaly runtime should auto-start after restart");
        AssertTrue(data.ProjectSettings.AnomalyClassification.NormalClassNames.Contains("normal", StringComparer.OrdinalIgnoreCase), "saved normal class mapping is missing");
        AssertTrue(data.ProjectSettings.AnomalyClassification.AbnormalClassNames.Contains("abnormal", StringComparer.OrdinalIgnoreCase), "saved abnormal class mapping is missing");
        AssertTrue(Math.Abs(data.ProjectSettings.AnomalyClassification.MinimumConfidence - 0.8D) < 0.0001D, "saved anomaly confidence should be 0.8");
    }

    private static void VerifyAnomalyMappingVisibleAfterRestart(Process process, IntPtr stableHandle)
    {
        AssertTrue(TryBringYoloSettingsElementIntoView(process, stableHandle, "YoloAnomalyNormalClassesBox"), "saved anomaly mapping fields were not reachable after restart");
        var root = RefreshAutomationRoot(process, stableHandle);
        string normalClasses = GetAutomationValueByAutomationId(root, "YoloAnomalyNormalClassesBox");
        string abnormalClasses = GetAutomationValueByAutomationId(root, "YoloAnomalyAbnormalClassesBox");
        string minimumConfidence = GetAutomationValueByAutomationId(root, "YoloAnomalyMinimumConfidenceBox");
        AssertTrue(normalClasses.Contains("normal", StringComparison.OrdinalIgnoreCase), "reopened UI did not show the normal mapping: " + normalClasses);
        AssertTrue(abnormalClasses.Contains("abnormal", StringComparison.OrdinalIgnoreCase), "reopened UI did not show the abnormal mapping: " + abnormalClasses);
        AssertTrue(
            double.TryParse(minimumConfidence, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedConfidence)
                && Math.Abs(parsedConfidence - 0.8D) < 0.0001D,
            "reopened UI did not show anomaly confidence 0.8: " + minimumConfidence);
    }

    private static AnomalyImageReviewState ReadAnomalyReviewState(CData data, string imagePath)
    {
        var service = new AnomalyImageReviewStatusService();
        service.LoadReviewStatus(data, new[] { imagePath });
        return service.GetItems().FirstOrDefault()?.ReviewState ?? AnomalyImageReviewState.Unreviewed;
    }
}
