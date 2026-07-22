using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private const string YoloV8DetectDefaultImage = @"D:\LabelingData\Test01\Images\Teaching_0.jpeg";
    private const string YoloV8DetectDefaultRoot = @"C:\Git\yolov8";

    private static int RunExeYoloV8DetectRestartSmoke(string[] args)
    {
        string recipeName = "codex_yolov8_detect_restart_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
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
            string modelEngine = PythonModelSettings.NormalizeModelEngine(GetArgumentValue(args, "--engine", PythonModelSettings.EngineYoloV8));
            bool useYolo11 = string.Equals(modelEngine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal);
            string engineDisplayName = useYolo11 ? "YOLO11" : "YOLOv8";
            string yoloRoot = Path.GetFullPath(GetArgumentValue(args, "--yolo-root", GetArgumentValue(args, "--yolov8-root", YoloV8DetectDefaultRoot)));
            string defaultWeightsPath = useYolo11
                ? Path.Combine(yoloRoot, "yolo11n.pt")
                : Path.Combine(
                    yoloRoot,
                    "runs",
                    "detect",
                    "openvisionlab-yolov8n-detect-test01-e100-img320-20260714",
                    "weights",
                    "best.pt");
            string weightsPath = Path.GetFullPath(GetArgumentValue(
                args,
                "--weights",
                defaultWeightsPath));
            string sourceImagePath = Path.GetFullPath(GetArgumentValue(args, "--image", YoloV8DetectDefaultImage));
            string externalDataYamlPath = GetArgumentValue(args, "--external-data-yaml", string.Empty);
            bool allowEmptyCandidates = HasArgument(args, "--allow-empty-candidates");
            if (!string.IsNullOrWhiteSpace(externalDataYamlPath))
            {
                externalDataYamlPath = Path.GetFullPath(externalDataYamlPath);
            }
            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "exe-yolov8-detect-restart-smoke", recipeName)));
            string screenshotDirectory = Path.Combine(artifactRoot, "screenshots");
            string inputRoot = Path.Combine(artifactRoot, "input");
            string outputRoot = Path.Combine(artifactRoot, "dataset");
            string smokeImagePath = Path.Combine(inputRoot, Path.GetFileName(sourceImagePath));
            string pythonPath = Path.GetFullPath(GetArgumentValue(
                args,
                "--python-exe",
                Path.Combine(yoloRoot, ".venv", "Scripts", "python.exe")));
            string clientScriptPath = useYolo11
                ? PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath()
                : Path.Combine(yoloRoot, "labeling_tcp_client.py");

            AssertTrue(File.Exists(exePath), "YOLOv8 Detect restart smoke EXE was not found: " + exePath);
            AssertTrue(Directory.Exists(yoloRoot), "YOLOv8 root was not found: " + yoloRoot);
            AssertTrue(File.Exists(pythonPath), "YOLOv8 Python was not found: " + pythonPath);
            AssertTrue(File.Exists(clientScriptPath), "YOLOv8 TCP adapter was not found: " + clientScriptPath);
            AssertTrue(File.Exists(weightsPath), "YOLOv8 Detect weights were not found: " + weightsPath);
            AssertTrue(File.Exists(sourceImagePath), "YOLOv8 Detect smoke image was not found: " + sourceImagePath);
            AssertTrue(
                string.IsNullOrWhiteSpace(externalDataYamlPath) || File.Exists(externalDataYamlPath),
                "optional external YOLO data.yaml was not found: " + externalDataYamlPath);

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
                "\uAC1D\uCCB4 \uD0D0\uC9C0",
                LabelingDatasetPurpose.ObjectDetection,
                "OK, NG");

            string visionPath = Path.Combine(recipeDirectory, "VISION.xml");
            AssertTrue(WaitUntil(() => File.Exists(visionPath), TimeSpan.FromSeconds(8)), "YOLOv8 Detect recipe VISION.xml was not created");
            File.Copy(visionPath, Path.Combine(artifactRoot, "created-before-runtime-VISION.xml"), overwrite: true);
            AssertEqual(LabelingDatasetPurpose.ObjectDetection, ReadRecipeData(visionPath).ProjectSettings.DatasetPurpose);
            CaptureWorkflowStep(RefreshAutomationRoot(firstProcess, firstHandle), screenshotDirectory, "01d_created_dataset_purpose_state");
            File.WriteAllText(
                Path.Combine(artifactRoot, "created-dataset-purpose-visible.txt"),
                GetAutomationValueByAutomationId(
                    RefreshAutomationRoot(firstProcess, firstHandle, bringToFront: false),
                    "CurrentDatasetPurposeText"),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            AssertDatasetPurposeVisibleThroughExe(
                firstProcess,
                firstHandle,
                "\uAC1D\uCCB4 \uD0D0\uC9C0",
                "newly created object-detection recipe did not show object detection as the active dataset purpose");

            ConfigureYoloV8RuntimeThroughExe(
                firstProcess,
                firstHandle,
                inputRoot,
                yoloRoot,
                pythonPath,
                clientScriptPath,
                weightsPath,
                screenshotDirectory,
                confidence: "0.25",
                timeoutSeconds: "180",
                inferenceImageSize: "320",
                modelEngine: modelEngine);

            if (!string.IsNullOrWhiteSpace(externalDataYamlPath))
            {
                SelectAndActivateExternalYoloDatasetThroughExe(
                    firstProcess,
                    firstHandle,
                    externalDataYamlPath,
                    screenshotDirectory);
            }
            LoadConfiguredImageRootThroughExe(firstProcess, inputRoot, screenshotDirectory);

            AssertTrue(WaitUntil(() => File.Exists(visionPath), TimeSpan.FromSeconds(8)), "YOLOv8 Detect recipe VISION.xml was not saved");
            File.Copy(visionPath, Path.Combine(artifactRoot, "saved-before-restart-VISION.xml"), overwrite: true);
            CData savedData = ReadRecipeData(visionPath);
            AssertYoloV8DetectRecipeSettings(savedData, yoloRoot, pythonPath, clientScriptPath, weightsPath, inputRoot, modelEngine);
            AssertExternalYoloDatasetSettings(savedData, externalDataYamlPath);
            string savedVisionHash = ComputeFileSha256(visionPath);
            CaptureWorkflowStep(RefreshAutomationRoot(firstProcess, firstHandle), screenshotDirectory, "02_saved_yolov8_detect_profile");

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
                "restarted EXE did not restore the saved YOLOv8 Detect recipe and image queue");
            CaptureWorkflowStep(RefreshAutomationRoot(restartedProcess, restartedHandle), screenshotDirectory, "03_restarted_recipe_restored");

            AssertTrue(
                string.Equals(File.ReadAllText(lastOpenedRecipePath).Trim(), recipeName, StringComparison.Ordinal),
                "restart marker did not preserve the YOLOv8 Detect smoke recipe");
            File.Copy(visionPath, Path.Combine(artifactRoot, "reopened-before-inference-VISION.xml"), overwrite: true);
            CData reopenedData = ReadRecipeData(visionPath);
            AssertYoloV8DetectRecipeSettings(reopenedData, yoloRoot, pythonPath, clientScriptPath, weightsPath, inputRoot, modelEngine);
            AssertExternalYoloDatasetSettings(reopenedData, externalDataYamlPath);
            string reopenedVisionHash = ComputeFileSha256(visionPath);
            PythonModelRuntimeState persistedRuntimeState = PythonModelSettingsValidator.GetRuntimeState(
                reopenedData.ProjectSettings.PythonModel);
            AssertTrue(
                persistedRuntimeState.CanRunInference,
                "persisted YOLOv8 Detect settings were not inference-ready: "
                    + persistedRuntimeState.SummaryText + " / "
                    + persistedRuntimeState.DetailText + " / "
                    + persistedRuntimeState.NextActionText);
            VerifyYoloV8SettingsVisibleAfterRestart(restartedProcess, restartedHandle, weightsPath, modelEngine);
            var settingsRoot = RefreshAutomationRoot(restartedProcess, restartedHandle, bringToFront: false);
            string settingsRuntimeStatus = string.Join(
                " / ",
                new[]
                {
                    GetAutomationValueByAutomationId(settingsRoot, "YoloModelSettingsSummaryRuntimeStatusText"),
                    GetAutomationValueByAutomationId(settingsRoot, "YoloModelSettingsSummaryRuntimeText"),
                    GetAutomationValueByAutomationId(settingsRoot, "YoloRuntimeExecutionSummaryText"),
                    GetAutomationValueByAutomationId(settingsRoot, "YoloRuntimeExecutionInspectionText")
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            var inferenceRoot = RefreshAutomationRoot(restartedProcess, restartedHandle);
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(inferenceRoot, "InferenceReviewStageButton")
                    || TryNativeClickAutomationElementByAutomationId(inferenceRoot, "InferenceReviewStageButton"),
                "AI candidate review stage was not selectable after restart");
            Thread.Sleep(500);
            inferenceRoot = RefreshAutomationRoot(restartedProcess, restartedHandle, bringToFront: false);
            CaptureWorkflowStep(inferenceRoot, screenshotDirectory, "03a_ai_candidate_stage");
            string stageStatus = string.Join(
                " / ",
                new[]
                {
                    ReadExeInferenceStatusSnapshot(inferenceRoot),
                    settingsRuntimeStatus,
                    persistedRuntimeState.SummaryText,
                    persistedRuntimeState.DetailText,
                    GetAutomationHelpText(FindAutomationElementByAutomationId(inferenceRoot, "RightWorkflowInferenceInspectButton")),
                    GetAutomationValueByAutomationId(inferenceRoot, "ModelCenterPriorityButtonStateText"),
                    GetAutomationValueByAutomationId(inferenceRoot, "WorkflowStageSummaryTitleText"),
                    GetAutomationValueByAutomationId(inferenceRoot, "WorkflowStageSummaryNextActionText")
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            File.WriteAllText(
                Path.Combine(artifactRoot, "ai-candidate-stage-status.txt"),
                stageStatus,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            AssertTrue(
                WaitUntil(
                    () => IsAutomationButtonEnabledByAutomationId(
                        RefreshAutomationRoot(restartedProcess, restartedHandle, bringToFront: false),
                        "RightWorkflowInferenceInspectButton"),
                    TimeSpan.FromSeconds(8)),
                "current-image inference did not become available in the AI candidate stage: " + stageStatus);
            inferenceRoot = RefreshAutomationRoot(restartedProcess, restartedHandle);
            bool invoked = TryInvokeAutomationButtonByAutomationId(inferenceRoot, "RightWorkflowInferenceInspectButton")
                || TryInvokeAutomationButtonByAutomationId(inferenceRoot, "WorkflowStageInspectCurrentImageButton")
                || TryInvokeAutomationButtonByAutomationId(inferenceRoot, "ModelCenterPriorityInspectCurrentButton")
                || TryInvokeAutomationButton(inferenceRoot, "\uD604\uC7AC \uAC80\uC0AC");
            AssertTrue(invoked, "current-image inference was not invokable after the YOLOv8 Detect restart");

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
                "first YOLOv8 Detect inference did not finish after restart");
            AssertTrue(!IsExeTrainedInferenceFailure(inferenceStatus), "first YOLOv8 Detect inference failed after restart: " + inferenceStatus);
            AssertTrue(inferenceStatus.Contains(engineDisplayName, StringComparison.OrdinalIgnoreCase), "inference status did not identify " + engineDisplayName + ": " + inferenceStatus);
            string weightsDirectory = Path.GetDirectoryName(weightsPath) ?? string.Empty;
            string expectedWeightsRunName = Path.GetFileName(weightsDirectory);
            if (string.Equals(expectedWeightsRunName, "weights", StringComparison.OrdinalIgnoreCase))
            {
                expectedWeightsRunName = Path.GetFileName(Path.GetDirectoryName(weightsDirectory) ?? string.Empty);
            }
            AssertTrue(
                !string.IsNullOrWhiteSpace(expectedWeightsRunName)
                    && inferenceStatus.Contains(expectedWeightsRunName, StringComparison.OrdinalIgnoreCase),
                "inference status did not identify the saved YOLOv8 Detect best.pt: " + inferenceStatus);
            AssertTrue(inferenceStatus.Contains("best.pt", StringComparison.OrdinalIgnoreCase), "inference status did not identify the saved YOLOv8 Detect weights file: " + inferenceStatus);
            AssertTrue(inferenceStatus.Contains("\uD6C4\uBCF4", StringComparison.Ordinal), "inference status did not report a candidate count: " + inferenceStatus);
            if (!allowEmptyCandidates)
            {
                AssertTrue(!inferenceStatus.Contains("\uD6C4\uBCF4 0", StringComparison.Ordinal), "YOLOv8 Detect smoke returned no UI-threshold candidates: " + inferenceStatus);
            }

            CaptureWorkflowStep(RefreshAutomationRoot(restartedProcess, restartedHandle), screenshotDirectory, "04_first_inference_after_restart");
            File.Copy(visionPath, Path.Combine(artifactRoot, "reopened-after-inference-VISION.xml"), overwrite: true);
            CData inferredData = ReadRecipeData(visionPath);
            AssertYoloV8DetectRecipeSettings(inferredData, yoloRoot, pythonPath, clientScriptPath, weightsPath, inputRoot, modelEngine);
            string inferredVisionHash = ComputeFileSha256(visionPath);

            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "EXE YOLOv8 Detect restart smoke passed.",
                "recipe=" + recipeName,
                "sourceImage=" + sourceImagePath,
                "smokeImage=" + smokeImagePath,
                "weights=" + weightsPath,
                "weightsSha256=" + ComputeFileSha256(weightsPath),
                "savedVisionSha256=" + savedVisionHash,
                "reopenedVisionSha256=" + reopenedVisionHash,
                "inferredVisionSha256=" + inferredVisionHash,
                "engine=" + reopenedData.ProjectSettings.PythonModel.ModelEngine,
                "confidence=" + reopenedData.ProjectSettings.PythonModel.MinimumDetectionConfidence.ToString(CultureInfo.InvariantCulture),
                "inferenceImageSize=" + reopenedData.ProjectSettings.PythonModel.InferenceImageSize.ToString(CultureInfo.InvariantCulture),
                "allowEmptyCandidates=" + allowEmptyCandidates.ToString(CultureInfo.InvariantCulture),
                "inferenceStatus=" + inferenceStatus,
                "screenshots=" + screenshotDirectory
            }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE recipe=" + recipeName);
            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE weights=" + weightsPath);
            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE savedVisionSha256=" + savedVisionHash);
            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE reopenedVisionSha256=" + reopenedVisionHash);
            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE inferredVisionSha256=" + inferredVisionHash);
            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE inferenceStatus=" + inferenceStatus);
            Console.WriteLine("EXE_YOLOV8_DETECT_RESTART_SMOKE summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL EXE YOLOv8 Detect restart smoke: " + ex.Message);
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

    private static Process StartYoloV8RuntimeSmokeExe(string exePath, out IntPtr handle)
    {
        Process process = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = true
        });
        AssertTrue(process != null, "failed to start YOLOv8 runtime restart smoke EXE");
        handle = WaitForMainWindowHandle(process, TimeSpan.FromSeconds(25));
        AssertTrue(handle != IntPtr.Zero, "YOLOv8 runtime restart smoke window did not appear");
        SetWindowPos(handle, HwndTopMost, 0, 0, VisualSmokeDefaultWindowWidth, VisualSmokeDefaultWindowHeight, SwpShowWindow);
        BringNativeWindowToFront(handle);
        return process;
    }

    private static void AssertDatasetPurposeVisibleThroughExe(
        Process process,
        IntPtr stableHandle,
        string expectedPurpose,
        string failureMessage)
    {
        string visiblePurpose = string.Empty;
        AssertTrue(
            WaitUntil(
                () =>
                {
                    var root = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                    visiblePurpose = GetAutomationValueByAutomationId(root, "CurrentDatasetPurposeText");
                    return visiblePurpose.Contains(expectedPurpose, StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(5)),
            failureMessage + ": " + visiblePurpose);
    }

    private static void SelectAndActivateExternalYoloDatasetThroughExe(
        Process process,
        IntPtr stableHandle,
        string dataYamlPath,
        string screenshotDirectory)
    {
        YoloExternalDatasetIntakeReport expectedReport = YoloExternalDatasetIntakeService.Build(
            dataYamlPath,
            LabelingDatasetPurpose.ObjectDetection);
        AssertTrue(expectedReport.IsReady, "provided external data.yaml was not ready for Object Detection: " + string.Join(" / ", expectedReport.Errors));
        string expectedClassText = string.Join(", ", expectedReport.ClassNames);

        AssertTrue(OpenYoloModelCenterThroughExe(process, stableHandle), "model center was not selectable for external data.yaml intake");
        var root = RefreshAutomationRoot(process, stableHandle);
        AssertTrue(
            SelectAutomationTabByAutomationId(root, "YoloModelCenterDataTaskTab"),
            "model center data tab was not selectable for external data.yaml intake");
        Thread.Sleep(300);
        root = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
        _ = TryExpandAutomationElementByAutomationId(root, "YoloDatasetReadinessQuickPanel");
        Thread.Sleep(300);
        root = RefreshAutomationRoot(process, stableHandle);
        AssertTrue(
            FindAutomationElementByAutomationId(root, "YoloExternalYoloDatasetSelectButton") != null,
            "external YOLO data.yaml selection was not reachable from the model center data tab");
        CaptureWorkflowStep(root, screenshotDirectory, "02a_external_yolo_data_before_select");

        AssertTrue(
            TryInvokeAutomationButtonByAutomationId(root, "YoloExternalYoloDatasetSelectButton"),
            "external YOLO data.yaml select button was not invokable");
        ChooseExternalYoloDataYamlFile(process, dataYamlPath);

        string statusText = string.Empty;
        string detailText = string.Empty;
        AssertTrue(
            WaitUntil(
                () =>
                {
                    var latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                    statusText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalYoloDatasetStatusText");
                    detailText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalYoloDatasetDetailText");
                    return statusText.Contains("검증됨", StringComparison.Ordinal)
                        && detailText.Contains("외부 학습 클래스: " + expectedClassText, StringComparison.Ordinal)
                        && detailText.Contains("레시피 클래스는 자동으로 바꾸지 않음", StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(20)),
            "external YOLO data.yaml did not show its validated native class list: " + statusText + " / " + detailText);

        root = RefreshAutomationRoot(process, stableHandle);
        AssertTrue(
            TryInvokeAutomationButtonByAutomationId(root, "YoloExternalYoloDatasetActivateButton"),
            "external YOLO data.yaml activation button was not invokable");
        AssertTrue(
            WaitUntil(
                () =>
                {
                    var latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                    statusText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalYoloDatasetStatusText");
                    detailText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalYoloDatasetDetailText");
                    return statusText.Contains("다음 학습에 사용", StringComparison.Ordinal)
                        && detailText.Contains("외부 학습 클래스: " + expectedClassText, StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(20)),
            "external YOLO data.yaml did not activate for the next training run: " + statusText + " / " + detailText);
        CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "02b_external_yolo_data_activated");
    }

    private static void ChooseExternalYoloDataYamlFile(Process process, string dataYamlPath)
    {
        const string dialogTitle = "외부 YOLO data.yaml 선택";
        var dialog = WaitForProcessWindowByName(process, dialogTitle, TimeSpan.FromSeconds(8));
        AssertTrue(dialog != null, "external YOLO data.yaml file dialog did not appear");
        BringNativeWindowToFront(new IntPtr(dialog.Current.NativeWindowHandle));
        Thread.Sleep(200);

        // The common file dialog exposes its file-name editor as 1148. Prefer
        // that stable control over the address bar because an address-bar Enter
        // can navigate to the folder without confirming data.yaml.
        if (TrySetAutomationValueByAutomationId(dialog, "1148", dataYamlPath))
        {
            _ = TryInvokeAutomationButtonByAutomationId(dialog, "1");
            Thread.Sleep(800);
            return;
        }

        Clipboard.SetText(dataYamlPath);
        SendKeys.SendWait("%d");
        Thread.Sleep(120);
        SendKeys.SendWait("^v");
        Thread.Sleep(120);
        SendKeys.SendWait("{ENTER}");
        Thread.Sleep(800);

        dialog = FindProcessWindowByName(process, dialogTitle);
        if (dialog != null && !TryInvokeAutomationButton(dialog, "열기") && !TryInvokeAutomationButton(dialog, "Open"))
        {
            SendKeys.SendWait("{ENTER}");
        }
    }

    private static void AssertExternalYoloDatasetSettings(CData data, string expectedDataYamlPath)
    {
        if (string.IsNullOrWhiteSpace(expectedDataYamlPath))
        {
            return;
        }

        ExternalYoloDatasetSettings settings = data.ProjectSettings.ExternalYoloDataset;
        AssertPathEqual(expectedDataYamlPath, settings.DataYamlFilePath, "saved external data.yaml path mismatch");
        AssertEqual(LabelingDatasetPurpose.ObjectDetection, settings.DatasetPurpose);
        AssertTrue(settings.UseForTraining, "validated external data.yaml should remain explicitly active for the next training run");
        AssertTrue(settings.LastValidationSucceeded, "validated external data.yaml readiness snapshot should persist");
        AssertTrue(!string.IsNullOrWhiteSpace(settings.LastValidationClassNames), "validated external data.yaml class list should persist");
    }

    private static void CreateDatasetRecipeThroughExe(
        Process process,
        IntPtr stableHandle,
        string recipeName,
        string outputRoot,
        string recipeDirectory,
        string screenshotDirectory,
        string purposeDisplayText,
        LabelingDatasetPurpose expectedPurpose,
        string classNames)
    {
        var wizardRoot = OpenDatasetSetupWizardThroughExe(process, stableHandle);
        AssertTrue(wizardRoot != null, "dataset wizard did not open for YOLOv8 Detect restart smoke");
        CaptureWorkflowStep(wizardRoot, screenshotDirectory, "01a_dataset_wizard_before_purpose_change");
        AssertTrue(
            ClickDatasetPurposeByText(wizardRoot, purposeDisplayText),
            expectedPurpose + " purpose was not clickable in the dataset wizard");
        wizardRoot = WaitForProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131", TimeSpan.FromSeconds(8));
        AssertTrue(
            WaitUntil(
                () => ContainsAutomationText(wizardRoot, "\uBAA9\uC801: " + purposeDisplayText)
                    || ContainsAutomationText(wizardRoot, "Purpose: " + expectedPurpose),
                TimeSpan.FromSeconds(3)),
            "dataset wizard did not select " + expectedPurpose);
        CaptureWorkflowStep(wizardRoot, screenshotDirectory, "01b_dataset_purpose_selected");
        string automaticRecipeName = GetAutomationValueByAutomationId(wizardRoot, "WizardRecipeNameBox");
        string automaticOutputRoot = GetAutomationValueByAutomationId(wizardRoot, "WizardOutputRootPathBox");
        AssertTrue(
            automaticRecipeName.Contains(expectedPurpose.ToString(), StringComparison.Ordinal),
            "dataset wizard automatic recipe name did not follow the selected purpose: " + automaticRecipeName);
        AssertTrue(
            Path.GetFileName(automaticOutputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Contains(expectedPurpose.ToString(), StringComparison.Ordinal),
            "dataset wizard automatic output root did not follow the selected purpose: " + automaticOutputRoot);
        AssertTrue(TrySetAutomationValueByAutomationId(wizardRoot, "WizardRecipeNameBox", recipeName), "dataset recipe name was not editable");
        AssertTrue(TrySetAutomationValueByAutomationId(wizardRoot, "WizardOutputRootPathBox", outputRoot), "dataset output root was not editable");
        AssertTrue(TrySetAutomationValueByAutomationId(wizardRoot, "WizardClassNamesBox", classNames), "dataset classes were not editable");
        CaptureWorkflowStep(wizardRoot, screenshotDirectory, "01c_dataset_recipe_wizard");

        var createButton = FindAutomationElementByAutomationId(wizardRoot, "WizardCreateButton");
        AssertTrue(createButton != null && createButton.Current.IsEnabled, "dataset recipe create button was not clickable");
        AssertTrue(TryInvokeAutomationButtonByAutomationId(wizardRoot, "WizardCreateButton"), "dataset recipe create button was not invokable");
        Thread.Sleep(500);
        CaptureWorkflowStep(
            RefreshAutomationRoot(process, stableHandle),
            screenshotDirectory,
            "01c_after_dataset_recipe_create");

        string manifestPath = Path.Combine(recipeDirectory, LabelingDatasetManifestService.FileName);
        LabelingDatasetManifest manifest = null;
        bool finalManifestReady = WaitUntil(
                    () => TryReadDatasetManifest(manifestPath, out manifest)
                    && string.Equals(manifest.RecipeName, recipeName, StringComparison.Ordinal)
                    && string.Equals(manifest.DatasetPurpose, expectedPurpose.ToString(), StringComparison.Ordinal)
                    && classNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .All(className => manifest.Classes?.Contains(className) == true),
                TimeSpan.FromSeconds(10));
        File.WriteAllText(
            Path.Combine(screenshotDirectory, "01c_dataset_manifest_after_create.json"),
            File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "<manifest-missing>",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        AssertTrue(finalManifestReady, "dataset recipe manifest did not reach final content");
        AssertTrue(File.Exists(Path.Combine(recipeDirectory, "VISION.xml")), "dataset recipe VISION.xml was not created");
        AssertTrue(File.Exists(manifestPath), "dataset recipe manifest was not created");
    }

    private static CData ReadRecipeData(string visionPath)
    {
        var serializer = new XmlSerializer(typeof(CData));
        using FileStream stream = File.OpenRead(visionPath);
        var data = serializer.Deserialize(stream) as CData;
        AssertTrue(data != null, "recipe VISION.xml did not deserialize: " + visionPath);
        data.ProjectSettings ??= new LabelingProjectSettings();
        data.ProjectSettings.EnsureDefaults();
        return data;
    }

    private static void AssertYoloV8DetectRecipeSettings(
        CData data,
        string yoloRoot,
        string pythonPath,
        string clientScriptPath,
        string weightsPath,
        string imageRoot,
        string expectedEngine = PythonModelSettings.EngineYoloV8)
    {
        AssertEqual(LabelingDatasetPurpose.ObjectDetection, data.ProjectSettings.DatasetPurpose);
        PythonModelSettings settings = data.ProjectSettings.PythonModel;
        AssertEqual(expectedEngine, settings.ModelEngine);
        AssertPathEqual(yoloRoot, settings.ProjectRootPath, "saved YOLOv8 project root mismatch");
        AssertPathEqual(pythonPath, settings.PythonExecutablePath, "saved YOLOv8 Python mismatch");
        AssertPathEqual(clientScriptPath, settings.ClientScriptPath, "saved YOLOv8 client script mismatch");
        AssertPathEqual(weightsPath, settings.WeightsPath, "saved YOLOv8 Detect weights mismatch");
        AssertPathEqual(imageRoot, settings.ImageRootPath, "saved YOLOv8 image root mismatch");
        AssertEqual(320, settings.InferenceImageSize);
        AssertTrue(Math.Abs(settings.MinimumDetectionConfidence - 0.25F) < 0.0001F, "saved YOLOv8 confidence should be 0.25");
        AssertTrue(settings.AutoStartClient, "saved YOLOv8 runtime should auto-start after restart");

        ModelRegistrySettings registry = data.ProjectSettings.ModelRegistry;
        AssertTrue(
            registry.Profiles.Exists(profile => string.Equals(profile.ModelEngine, expectedEngine, StringComparison.Ordinal)
                && string.Equals(profile.DatasetPurpose, LabelingDatasetPurpose.ObjectDetection.ToString(), StringComparison.Ordinal)),
            "saved YOLOv8 Detect settings should register an ObjectDetection model profile");
        ModelCandidate currentModel = ModelRegistryService.FindCurrentInspectionModel(registry);
        AssertTrue(currentModel != null, "saved YOLOv8 Detect settings should register the current inspection model");
        AssertPathEqual(weightsPath, currentModel.WeightsPath, "saved YOLOv8 Detect registry weights mismatch");
        AssertEqual(0, registry.TrainingRuns.Count);
    }

    private static void VerifyYoloV8SettingsVisibleAfterRestart(
        Process process,
        IntPtr stableHandle,
        string weightsPath,
        string expectedEngine = PythonModelSettings.EngineYoloV8)
    {
        AssertTrue(OpenYoloModelCenterThroughExe(process, stableHandle), "model center was not selectable after restart");
        AssertTrue(
            TryExpandYoloSettingsSection(process, stableHandle, "YoloModelSettingsExpander", "YoloInspectionModelQuickPanel"),
            "YOLO model settings were not expandable after restart");
        AssertTrue(TryExpandYoloAdvancedModelSettingsThroughExe(process, stableHandle), "advanced YOLO settings were not expandable after restart");
        AssertTrue(TryBringYoloSettingsElementIntoView(process, stableHandle, "YoloWeightsPathBox"), "saved YOLOv8 weights field was not reachable after restart");
        var root = RefreshAutomationRoot(process, stableHandle);
        string selectedEngine = FirstNonEmpty(
            GetSelectedComboBoxItemNameByAutomationId(root, "YoloModelEngineBox"),
            GetAutomationValueByAutomationId(root, "YoloModelEngineBox"));
        string visibleWeightsPath = GetAutomationValueByAutomationId(root, "YoloWeightsPathBox");
        string displayEngine = string.Equals(expectedEngine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal)
            ? "YOLO11"
            : "YOLOv8";
        AssertTrue(selectedEngine.Contains(displayEngine, StringComparison.OrdinalIgnoreCase), "reopened UI did not show " + displayEngine + ": " + selectedEngine);
        AssertPathEqual(weightsPath, visibleWeightsPath, "reopened UI showed the wrong Detect weights");
    }

    private static void AssertPathEqual(string expected, string actual, string message)
    {
        AssertTrue(
            string.Equals(Path.GetFullPath(expected), Path.GetFullPath(actual), StringComparison.OrdinalIgnoreCase),
            message + $"; expected={expected}, actual={actual}");
    }

    private static string ComputeFileSha256(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void RestoreLastOpenedRecipe(string path, bool existed, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (existed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
            File.WriteAllBytes(path, content ?? Array.Empty<byte>());
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
