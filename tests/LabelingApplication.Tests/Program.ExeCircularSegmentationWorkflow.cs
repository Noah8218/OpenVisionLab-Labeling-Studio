using MvcVisionSystem;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private const string CircularSegmentationDefaultImageRoot = @"D:\circular_defect_labeling_dataset_v1\images";
    private const string CircularSegmentationDefaultYoloRoot = @"C:\Git\yolov8";

    private static int RunExeCircularSegmentationWorkflow(string[] args)
    {
        string recipeName = "circular_seg_exe_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        try
        {
            string root = FindRepositoryRoot();
            string exePath = Path.GetFullPath(GetArgumentValue(
                args,
                "--exe",
                Path.Combine(root, "artifacts", "run", "Debug", "OpenVisionLab.LabelingStudio.exe")));
            string imageRoot = Path.GetFullPath(GetArgumentValue(args, "--image-root", CircularSegmentationDefaultImageRoot));
            string yoloRoot = Path.GetFullPath(GetArgumentValue(args, "--yolov8-root", CircularSegmentationDefaultYoloRoot));
            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "exe-circular-segmentation-workflow", recipeName)));
            string screenshotDirectory = Path.Combine(artifactRoot, "screenshots");
            string outputRoot = Path.Combine(artifactRoot, "dataset");
            int labelCount = Math.Max(8, TryParseInt(GetArgumentValue(args, "--label-count", "12"), 12));

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException("EXE circular segmentation workflow target was not found. Build the app first.", exePath);
            }

            if (!Directory.Exists(imageRoot))
            {
                throw new DirectoryNotFoundException("Circular segmentation image root was not found: " + imageRoot);
            }

            if (!Directory.Exists(yoloRoot))
            {
                throw new DirectoryNotFoundException("YOLOv8 root was not found: " + yoloRoot);
            }

            string pythonPath = Path.Combine(yoloRoot, ".venv", "Scripts", "python.exe");
            string clientScriptPath = Path.Combine(yoloRoot, "labeling_tcp_client.py");
            string seedWeightsPath = Path.Combine(yoloRoot, "yolov8n-seg.pt");
            if (!File.Exists(pythonPath))
            {
                throw new FileNotFoundException("YOLOv8 venv python was not found.", pythonPath);
            }

            if (!File.Exists(clientScriptPath))
            {
                throw new FileNotFoundException("YOLOv8 TCP client was not found.", clientScriptPath);
            }

            if (!File.Exists(seedWeightsPath))
            {
                throw new FileNotFoundException("YOLOv8 segmentation seed weights were not found.", seedWeightsPath);
            }

            string exeDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string recipeDirectory = Path.Combine(exeDirectory, "RECIPE", recipeName);
            DeleteDirectoryIfExists(recipeDirectory);
            DeleteDirectoryIfExists(outputRoot);
            Directory.CreateDirectory(screenshotDirectory);

            ExeCircularSegmentationWorkflowResult result = ExecuteExeCircularSegmentationWorkflow(
                exePath,
                recipeName,
                outputRoot,
                recipeDirectory,
                imageRoot,
                yoloRoot,
                pythonPath,
                clientScriptPath,
                seedWeightsPath,
                screenshotDirectory,
                labelCount);

            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? artifactRoot);
            File.WriteAllLines(summaryPath, new[]
            {
                "EXE circular segmentation workflow passed.",
                "recipe=" + recipeName,
                "imageRoot=" + imageRoot,
                "outputRoot=" + outputRoot,
                "trainedWeights=" + result.TrainedWeightsPath,
                "trainSegments=" + result.TrainSegmentCount.ToString(CultureInfo.InvariantCulture),
                "validSegments=" + result.ValidSegmentCount.ToString(CultureInfo.InvariantCulture),
                "testSegments=" + result.TestSegmentCount.ToString(CultureInfo.InvariantCulture),
                "backgroundLabels=" + result.BackgroundLabelCount.ToString(CultureInfo.InvariantCulture),
                "screenshots=" + screenshotDirectory,
                "inferenceStatus=" + result.InferenceStatus
            }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Console.WriteLine($"EXE_CIRCULAR_SEGMENTATION_WORKFLOW recipe={recipeName}");
            Console.WriteLine($"EXE_CIRCULAR_SEGMENTATION_WORKFLOW screenshots={screenshotDirectory}");
            Console.WriteLine($"EXE_CIRCULAR_SEGMENTATION_WORKFLOW summary={summaryPath}");
            Console.WriteLine($"EXE_CIRCULAR_SEGMENTATION_WORKFLOW trainedWeights={result.TrainedWeightsPath}");
            Console.WriteLine($"EXE_CIRCULAR_SEGMENTATION_WORKFLOW trainSegments={result.TrainSegmentCount} validSegments={result.ValidSegmentCount} testSegments={result.TestSegmentCount} backgroundLabels={result.BackgroundLabelCount}");
            Console.WriteLine($"EXE_CIRCULAR_SEGMENTATION_WORKFLOW inferenceStatus={result.InferenceStatus}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL EXE circular segmentation workflow: " + ex.Message);
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static ExeCircularSegmentationWorkflowResult ExecuteExeCircularSegmentationWorkflow(
        string exePath,
        string recipeName,
        string outputRoot,
        string recipeDirectory,
        string imageRoot,
        string yoloRoot,
        string pythonPath,
        string clientScriptPath,
        string seedWeightsPath,
        string screenshotDirectory,
        int labelCount)
    {
        Process process = null;
        IntPtr handle = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            });
            AssertTrue(process != null, "failed to start EXE circular segmentation workflow process");

            handle = WaitForMainWindowHandle(process, TimeSpan.FromSeconds(25));
            AssertTrue(handle != IntPtr.Zero, "EXE circular segmentation workflow window did not appear");
            SetWindowPos(handle, HwndTopMost, 0, 0, VisualSmokeDefaultWindowWidth, VisualSmokeDefaultWindowHeight, SwpShowWindow);
            BringNativeWindowToFront(handle);
            var root = RefreshAutomationRoot(process);
            CaptureWorkflowStep(root, screenshotDirectory, "01_startup");

            CreateSegmentationRecipeThroughExe(process, handle, recipeName, outputRoot, recipeDirectory, screenshotDirectory);
            root = RefreshAutomationRoot(process, handle);
            CaptureWorkflowStep(root, screenshotDirectory, "03_dataset_created");

            if (!RecipeHasYoloV8SegmentationRuntime(recipeDirectory, imageRoot, yoloRoot, pythonPath, clientScriptPath, seedWeightsPath))
            {
                ConfigureYoloV8RuntimeThroughExe(
                    process,
                    handle,
                    imageRoot,
                    yoloRoot,
                    pythonPath,
                    clientScriptPath,
                    seedWeightsPath,
                    screenshotDirectory);
            }

            root = RefreshAutomationRoot(process, handle);
            CaptureWorkflowStep(root, screenshotDirectory, "04_yolov8_settings_ready");

            LoadConfiguredImageRootThroughExe(process, imageRoot, screenshotDirectory);
            root = RefreshAutomationRoot(process);
            CaptureWorkflowStep(root, screenshotDirectory, "05_image_queue_loaded");

            IReadOnlyList<string> selectedImages = SelectCircularSegmentationLabelImages(imageRoot, labelCount);
            IReadOnlyList<string> postSplitImages = selectedImages
                .Where(path =>
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    return IsCircularSegmentationTestSplit(stem) || IsCircularSegmentationValidSplit(stem);
                })
                .ToList();
            IReadOnlyList<string> initialImages = selectedImages
                .Where(path =>
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    return !IsCircularSegmentationTestSplit(stem) && !IsCircularSegmentationValidSplit(stem);
                })
                .ToList();
            AssertTrue(initialImages.Count > 0, "EXE workflow did not select any train segmentation images");
            AssertTrue(postSplitImages.Any(path => IsCircularSegmentationValidSplit(Path.GetFileNameWithoutExtension(path))), "EXE workflow did not select any validation segmentation images");
            AssertTrue(postSplitImages.Any(path => IsCircularSegmentationTestSplit(Path.GetFileNameWithoutExtension(path))), "EXE workflow did not select any held-out test segmentation images");
            LabelCircularSegmentationImagesThroughExe(process, initialImages, outputRoot, screenshotDirectory);

            int trainSegmentCount = CountFiles(Path.Combine(outputRoot, "data", "train", "segments"), "*.json");
            int trainMaskCount = CountFiles(Path.Combine(outputRoot, "data", "train", "masks"), "*.png");
            AssertTrue(trainSegmentCount > 0, "EXE workflow did not save any train segment JSON files");
            AssertTrue(trainMaskCount > 0, "EXE workflow did not save any train mask PNG files");
            AssertTrue(File.Exists(Path.Combine(outputRoot, "data.yaml")), "EXE workflow did not create data.yaml after segmentation labels were saved");

            root = RefreshAutomationRoot(process);
            CaptureWorkflowStep(root, screenshotDirectory, "07_saved_segmentation_artifacts");

            string trainedWeightsPath = TrainYoloV8SegmentationThroughExe(process, yoloRoot, outputRoot, postSplitImages, screenshotDirectory);
            trainSegmentCount = CountFiles(Path.Combine(outputRoot, "data", "train", "segments"), "*.json");
            int validSegmentCount = CountFiles(Path.Combine(outputRoot, "data", "valid", "segments"), "*.json");
            int testSegmentCount = CountFiles(Path.Combine(outputRoot, "data", "test", "segments"), "*.json");
            int validMaskCount = CountFiles(Path.Combine(outputRoot, "data", "valid", "masks"), "*.png");
            int testMaskCount = CountFiles(Path.Combine(outputRoot, "data", "test", "masks"), "*.png");
            AssertTrue(validSegmentCount > 0, "EXE workflow did not save any valid segment JSON files");
            AssertTrue(testSegmentCount > 0, "EXE workflow did not save any test segment JSON files");
            AssertTrue(validMaskCount > 0, "EXE workflow did not save any valid mask PNG files");
            AssertTrue(testMaskCount > 0, "EXE workflow did not save any test mask PNG files");
            int backgroundLabelCount = CountCircularOkBackgroundLabels(imageRoot, outputRoot);
            AssertTrue(backgroundLabelCount > 0, "EXE workflow did not include any OK folder background label files for YOLOv8 SEG training");
            ApplyTrainedModelAndRunInferenceThroughExe(process, trainedWeightsPath, screenshotDirectory, out string inferenceStatus);

            return new ExeCircularSegmentationWorkflowResult(
                trainedWeightsPath,
                trainSegmentCount,
                validSegmentCount,
                testSegmentCount,
                backgroundLabelCount,
                inferenceStatus);
        }
        catch
        {
            if (process != null && !process.HasExited)
            {
                System.Windows.Automation.AutomationElement failureRoot = handle != IntPtr.Zero
                    ? RefreshAutomationRoot(process, handle, bringToFront: false)
                    : RefreshAutomationRoot(process, bringToFront: false);
                TryCaptureExeSmokeFailure(failureRoot, Path.Combine(screenshotDirectory, "failure.png"), "latest");
            }

            throw;
        }
        finally
        {
            CloseExeSmokeProcess(process);
        }
    }

    private static void CreateSegmentationRecipeThroughExe(
        Process process,
        IntPtr stableHandle,
        string recipeName,
        string outputRoot,
        string recipeDirectory,
        string screenshotDirectory)
    {
        System.Windows.Automation.AutomationElement wizardRoot = OpenDatasetSetupWizardThroughExe(process, stableHandle);
        AssertTrue(wizardRoot != null, "dataset wizard did not open for circular segmentation workflow");
        _ = SelectListItemByText(wizardRoot, "\uC138\uADF8\uBA58\uD14C\uC774\uC158") || SelectListItemByText(wizardRoot, "Segmentation");
        wizardRoot = WaitForProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131", TimeSpan.FromSeconds(8));
        AssertTrue(TrySetAutomationValueByAutomationId(wizardRoot, "WizardRecipeNameBox", recipeName), "segmentation wizard recipe name was not editable");
        AssertTrue(TrySetAutomationValueByAutomationId(wizardRoot, "WizardOutputRootPathBox", outputRoot), "segmentation wizard output root was not editable");
        AssertTrue(TrySetAutomationValueByAutomationId(wizardRoot, "WizardClassNamesBox", "NG"), "segmentation wizard class names were not editable");
        Thread.Sleep(500);
        CaptureWorkflowStep(wizardRoot, screenshotDirectory, "02_segmentation_recipe_wizard");

        System.Windows.Automation.AutomationElement createButton = FindAutomationElementByAutomationId(wizardRoot, "WizardCreateButton");
        AssertTrue(createButton != null && createButton.Current.IsEnabled, "segmentation wizard create button was not clickable");
        AssertTrue(TryInvokeAutomationButtonByAutomationId(wizardRoot, "WizardCreateButton"), "segmentation wizard create button was not invokable");

        string manifestPath = Path.Combine(recipeDirectory, LabelingDatasetManifestService.FileName);
        LabelingDatasetManifest manifest = null;
        AssertTrue(
            WaitUntil(
                () =>
                {
                    if (!TryReadDatasetManifest(manifestPath, out manifest))
                    {
                        return false;
                    }

                    return string.Equals(manifest.RecipeName, recipeName, StringComparison.Ordinal)
                        && string.Equals(manifest.DatasetPurpose, LabelingDatasetPurpose.Segmentation.ToString(), StringComparison.Ordinal)
                        && manifest.Classes?.Contains("NG") == true;
                },
                TimeSpan.FromSeconds(10)),
            "EXE segmentation dataset wizard manifest did not reach final generated content");
        AssertTrue(File.Exists(Path.Combine(recipeDirectory, "VISION.xml")), "EXE segmentation wizard should create VISION.xml");
        AssertTrue(File.Exists(manifestPath), "EXE segmentation wizard should create dataset.manifest.json");
    }

    private static bool RecipeHasYoloV8SegmentationRuntime(
        string recipeDirectory,
        string imageRoot,
        string yoloRoot,
        string pythonPath,
        string clientScriptPath,
        string weightsPath)
    {
        string visionPath = Path.Combine(recipeDirectory, "VISION.xml");
        if (!File.Exists(visionPath))
        {
            return false;
        }

        string source = File.ReadAllText(visionPath);
        return source.Contains("<DatasetPurpose>Segmentation</DatasetPurpose>", StringComparison.Ordinal)
            && source.Contains("<ModelEngine>YOLOv8</ModelEngine>", StringComparison.Ordinal)
            && source.Contains($"<PythonExecutablePath>{pythonPath}</PythonExecutablePath>", StringComparison.OrdinalIgnoreCase)
            && source.Contains($"<ProjectRootPath>{yoloRoot}</ProjectRootPath>", StringComparison.OrdinalIgnoreCase)
            && source.Contains($"<ClientScriptPath>{clientScriptPath}</ClientScriptPath>", StringComparison.OrdinalIgnoreCase)
            && source.Contains($"<WeightsPath>{weightsPath}</WeightsPath>", StringComparison.OrdinalIgnoreCase)
            && source.Contains($"<ImageRootPath>{imageRoot}</ImageRootPath>", StringComparison.OrdinalIgnoreCase);
    }

    private static System.Windows.Automation.AutomationElement OpenDatasetSetupWizardThroughExe(Process process, IntPtr stableHandle)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            System.Windows.Automation.AutomationElement wizard = FindProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131");
            if (wizard != null)
            {
                return wizard;
            }

            _ = CloseRecipeManagerWindowIfOpen(process, stableHandle);

            System.Windows.Automation.AutomationElement root = stableHandle != IntPtr.Zero
                ? RefreshAutomationRoot(process, stableHandle)
                : RefreshAutomationRoot(process);
            _ = SelectAutomationTabByAutomationId(root, "LearningReviewTab") || SelectTabItemByName(root, "\uAC00\uC774\uB4DC/\uB3C4\uAD6C");
            _ = SelectListItemByText(root, "\uC138\uADF8\uBA58\uD14C\uC774\uC158") || SelectListItemByText(root, "Segmentation");
            if (TryInvokeAutomationButtonByAutomationId(root, "DatasetSetupStartButton")
                || TryInvokeAutomationButton(root, "\uC0C8 \uB370\uC774\uD130\uC14B \uB9CC\uB4E4\uAE30")
                || TryInvokeAutomationButton(root, "\uB370\uC774\uD130\uC14B \uC0DD\uC131 \uC2DC\uC791"))
            {
                if (WaitUntil(
                        () => (wizard = FindProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131")) != null,
                        TimeSpan.FromSeconds(3)))
                {
                    return wizard;
                }
            }

            root = stableHandle != IntPtr.Zero
                ? RefreshAutomationRoot(process, stableHandle)
                : RefreshAutomationRoot(process);
            if (!TryNativeClickAutomationElementByAutomationId(root, "ChangeDatasetButton"))
            {
                _ = TryInvokeAutomationButtonByAutomationId(root, "ChangeDatasetButton");
            }

            System.Windows.Automation.AutomationElement selection = null;
            if (WaitUntil(
                    () => (selection = FindProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC120\uD0DD")) != null,
                    TimeSpan.FromSeconds(2)))
            {
                if (TryInvokeAutomationButtonByAutomationId(selection, "CreateNewDatasetFromSelectionButton")
                    || TryInvokeAutomationButtonByAutomationId(selection, "CreateDatasetGuideButton")
                    || TryInvokeAutomationButtonByAutomationId(selection, "CreateFirstDatasetButton"))
                {
                    if (WaitUntil(
                            () => (wizard = FindProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131")) != null,
                            TimeSpan.FromSeconds(4)))
                    {
                        return wizard;
                    }
                }
            }

            _ = CloseRecipeManagerWindowIfOpen(process, stableHandle);

            root = stableHandle != IntPtr.Zero
                ? RefreshAutomationRoot(process, stableHandle)
                : RefreshAutomationRoot(process);
            if (!TryNativeClickAutomationElementByAutomationId(root, "DatasetHomeStageButton"))
            {
                _ = TryInvokeAutomationButtonByAutomationId(root, "DatasetHomeStageButton");
            }

            Thread.Sleep(220);
            root = stableHandle != IntPtr.Zero
                ? RefreshAutomationRoot(process, stableHandle)
                : RefreshAutomationRoot(process);
            _ = SelectAutomationTabByAutomationId(root, "LearningReviewTab") || SelectTabItemByName(root, "\uAC00\uC774\uB4DC/\uB3C4\uAD6C");
            _ = SelectListItemByText(root, "\uC138\uADF8\uBA58\uD14C\uC774\uC158") || SelectListItemByText(root, "Segmentation");
            if (TryInvokeAutomationButtonByAutomationId(root, "DatasetSetupStartButton"))
            {
                if (WaitUntil(
                        () => (wizard = FindProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131")) != null,
                        TimeSpan.FromSeconds(3)))
                {
                    return wizard;
                }
            }

            Thread.Sleep(350);
        }

        return FindProcessWindowByName(process, "\uB370\uC774\uD130\uC14B \uC0DD\uC131");
    }

    private static bool CloseRecipeManagerWindowIfOpen(Process process, IntPtr mainHandle)
    {
        if (process == null || process.HasExited)
        {
            return false;
        }

        IntPtr primaryHandle = mainHandle != IntPtr.Zero ? mainHandle : process.MainWindowHandle;
        bool closed = false;
        uint targetProcessId = (uint)process.Id;
        EnumWindows((hWnd, _) =>
        {
            if (hWnd == primaryHandle || !IsWindowVisible(hWnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId != targetProcessId)
            {
                return true;
            }

            try
            {
                System.Windows.Automation.AutomationElement element = System.Windows.Automation.AutomationElement.FromHandle(hWnd);
                if (element != null
                    && element.Current.ControlType == System.Windows.Automation.ControlType.Window
                    && ContainsAutomationText(element, "\uB808\uC2DC\uD53C \uAD00\uB9AC"))
                {
                    SendMessage(hWnd, WmClose, IntPtr.Zero, IntPtr.Zero);
                    closed = true;
                }
            }
            catch (System.Windows.Automation.ElementNotAvailableException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            return true;
        }, IntPtr.Zero);

        if (closed)
        {
            Thread.Sleep(300);
        }

        return closed;
    }

    private static void ConfigureYoloV8RuntimeThroughExe(
        Process process,
        IntPtr stableHandle,
        string imageRoot,
        string yoloRoot,
        string pythonPath,
        string clientScriptPath,
        string weightsPath,
        string screenshotDirectory,
        string confidence = "0.01",
        string timeoutSeconds = "300",
        string inferenceImageSize = "128",
        string anomalyNormalClasses = null,
        string anomalyAbnormalClasses = null,
        string anomalyMinimumConfidence = null)
    {
        var root = RefreshAutomationRoot(process, stableHandle);
        AssertTrue(
            OpenYoloModelCenterThroughExe(process, stableHandle)
                || OpenYoloModelCenterThroughExe(process, IntPtr.Zero),
            "model settings tab was not selectable");
        AssertTrue(
            TryExpandYoloSettingsSection(process, stableHandle, "YoloModelSettingsExpander", "YoloInspectionModelQuickPanel"),
            "YOLO model settings section was not expandable");
        root = RefreshAutomationRoot(process, stableHandle);
        AssertTrue(
            TryExpandYoloAdvancedModelSettingsThroughExe(process, stableHandle),
            "YOLO advanced model settings expander was not reachable");
        AssertTrue(
            TryBringYoloSettingsElementIntoView(process, stableHandle, "YoloModelEngineBox"),
            "YOLO model engine combo box was not reachable");
        root = RefreshAutomationRoot(process, stableHandle);

        bool engineSelected = TrySelectComboBoxItemByAutomationId(
                process,
                stableHandle,
                "YoloModelEngineBox",
                new[] { PythonModelSettings.EngineYoloV8, "YOLOv8" },
                string.Empty,
                out string selectedEngine)
            || TrySelectVisibleComboBoxItemByName(process, stableHandle, "YoloModelEngineBox", "YOLOv8", out selectedEngine);
        if (!engineSelected)
        {
            root = RefreshAutomationRoot(process, stableHandle);
            selectedEngine = FirstNonEmpty(
                GetSelectedComboBoxItemNameByAutomationId(root, "YoloModelEngineBox"),
                GetAutomationValueByAutomationId(root, "YoloModelEngineBox"));
            engineSelected = selectedEngine.Contains("YOLOv8", StringComparison.OrdinalIgnoreCase)
                || selectedEngine.Contains(PythonModelSettings.EngineYoloV8, StringComparison.OrdinalIgnoreCase);
        }

        AssertTrue(
            engineSelected,
            "YOLO model engine combo box did not accept YOLOv8 selection");
        AssertTrue(
            string.Equals(selectedEngine, PythonModelSettings.EngineYoloV8, StringComparison.OrdinalIgnoreCase)
                || selectedEngine.Contains("YOLOv8", StringComparison.OrdinalIgnoreCase),
            "YOLO model engine did not select YOLOv8; selected=" + selectedEngine);

        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloPythonPathBox", pythonPath), "YOLOv8 python path was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloProjectRootBox", yoloRoot), "YOLOv8 project root was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloClientScriptBox", clientScriptPath), "YOLOv8 client script was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloWeightsPathBox", weightsPath), "YOLOv8 weights path was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloImageRootBox", imageRoot), "YOLOv8 image root was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloConfidenceBox", confidence), "YOLO confidence was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloTimeoutBox", timeoutSeconds), "YOLO timeout was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloInferenceImageSizeBox", inferenceImageSize), "YOLO inference image size was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloMaxCandidatesBox", "20"), "YOLO max candidates was not editable");
        if (anomalyNormalClasses != null)
        {
            AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloAnomalyNormalClassesBox", anomalyNormalClasses), "anomaly normal classes were not editable");
        }

        if (anomalyAbnormalClasses != null)
        {
            AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloAnomalyAbnormalClassesBox", anomalyAbnormalClasses), "anomaly abnormal classes were not editable");
        }

        if (anomalyMinimumConfidence != null)
        {
            AssertTrue(TryPasteYoloSettingsValueThroughExe(process, stableHandle, "YoloAnomalyMinimumConfidenceBox", anomalyMinimumConfidence), "anomaly minimum confidence was not editable");
        }

        CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "04a_yolov8_fields_entered");

        AssertTrue(
            TryInvokeAutomationButtonByAutomationId(RefreshAutomationRoot(process, stableHandle), "SaveYoloSettingsButton")
                || TryInvokeAutomationButton(RefreshAutomationRoot(process, stableHandle), "\uC800\uC7A5"),
            "YOLO settings save button was not invokable");
        Thread.Sleep(1200);
    }

    private static void LoadConfiguredImageRootThroughExe(Process process, string imageRoot, string screenshotDirectory)
    {
        var root = RefreshAutomationRoot(process);
        string expectedImageMarker = FindImageQueueMarker(imageRoot);
        if (FindAutomationElementByAutomationId(root, "LoadConfiguredImageRootButton") == null)
        {
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(root, "DatasetHomeStageButton"),
                "dataset stage was not selectable before loading the configured image root");
            AssertTrue(
                WaitUntil(
                    () => FindAutomationElementByAutomationId(
                        RefreshAutomationRoot(process, bringToFront: false),
                        "LoadConfiguredImageRootButton") != null,
                    TimeSpan.FromSeconds(8)),
                "configured image-root load button did not appear in the dataset stage");
            root = RefreshAutomationRoot(process);
        }

        bool invoked = TryInvokeAutomationButtonByAutomationId(root, "LoadConfiguredImageRootButton");
        if (!invoked)
        {
            System.Windows.Automation.AutomationElement loadButton = FindAutomationElementByAutomationId(root, "LoadConfiguredImageRootButton");
            if (loadButton != null)
            {
                NativeClick(GetAutomationCenter(loadButton));
                invoked = true;
            }
        }

        AssertTrue(
            invoked || TryInvokeAutomationButton(root, "\uC124\uC815 \uD3F4\uB354"),
            "configured image-root load button was not invokable");
        bool loaded = WaitUntil(
                () =>
                {
                    var latestRoot = RefreshAutomationRoot(process, bringToFront: false);
                    return ImageRootAppearsLoaded(latestRoot, imageRoot, expectedImageMarker);
                },
                TimeSpan.FromSeconds(12));
        if (!loaded)
        {
            CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "05a_configured_image_root_load_failed");
        }

        AssertTrue(
            loaded,
            "configured image root did not load into the EXE image queue");
        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "05a_configured_image_root_loaded");
    }

    private static void LoadImageRootThroughFolderDialogExe(Process process, IntPtr stableHandle, string imageRoot, string screenshotDirectory)
    {
        var root = RefreshAutomationRoot(process, stableHandle);
        string expectedImageMarker = FindImageQueueMarker(imageRoot);
        AssertTrue(
            TryInvokeAutomationButton(root, "\uD3F4\uB354"),
            "image-folder browse button was not invokable");

        System.Windows.Automation.AutomationElement dialog = WaitForProcessWindowByName(
            process,
            "\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC120\uD0DD",
            TimeSpan.FromSeconds(8));
        BringNativeWindowToFront(new IntPtr(dialog.Current.NativeWindowHandle));
        Thread.Sleep(200);

        Clipboard.SetText(imageRoot);
        SendKeys.SendWait("%d");
        Thread.Sleep(120);
        SendKeys.SendWait("^v");
        Thread.Sleep(120);
        SendKeys.SendWait("{ENTER}");
        Thread.Sleep(800);

        dialog = FindProcessWindowByName(process, "\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC120\uD0DD");
        if (dialog != null)
        {
            if (!TryInvokeAutomationButton(dialog, "\uD3F4\uB354 \uC120\uD0DD")
                && !TryInvokeAutomationButton(dialog, "\uC120\uD0DD")
                && !TryInvokeAutomationButton(dialog, "Select Folder"))
            {
                SendKeys.SendWait("{ENTER}");
            }
        }

        AssertTrue(
            WaitUntil(
                () =>
                {
                    var latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                    return ImageRootAppearsLoaded(latestRoot, imageRoot, expectedImageMarker);
                },
                TimeSpan.FromSeconds(12)),
            "selected image root did not load into the EXE image queue");
        CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "05a_folder_dialog_image_root_loaded");
    }

    private static string FindImageQueueMarker(string imageRoot)
    {
        return Directory.EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories)
            .Where(path => ExeSmokeImageArtifactExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFileNameWithoutExtension)
            .FirstOrDefault() ?? Path.GetFileName(imageRoot);
    }

    private static bool ImageRootAppearsLoaded(System.Windows.Automation.AutomationElement root, string imageRoot, string expectedImageMarker)
    {
        string currentFolder = GetAutomationValueByAutomationId(root, "CurrentImageFolderPathText");
        string datasetStatus = GetAutomationValueByAutomationId(root, "DatasetStatusText");
        string imageRootLeaf = Path.GetFileName(imageRoot);
        return currentFolder.IndexOf(imageRoot, StringComparison.OrdinalIgnoreCase) >= 0
            || datasetStatus.IndexOf(imageRoot, StringComparison.OrdinalIgnoreCase) >= 0
            || currentFolder.IndexOf(imageRootLeaf, StringComparison.OrdinalIgnoreCase) >= 0
            || datasetStatus.IndexOf(imageRootLeaf, StringComparison.OrdinalIgnoreCase) >= 0
            || ContainsAutomationText(root, expectedImageMarker);
    }

    private static IReadOnlyList<string> SelectCircularSegmentationLabelImages(string imageRoot, int labelCount)
    {
        IReadOnlyList<string> imagePaths = Directory.EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories)
            .Where(path => ExeSmokeImageArtifactExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => IsUnderCircularSegmentationClassFolder(imageRoot, path, "NG"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AssertTrue(imagePaths.Count > 0, "circular segmentation image root did not contain supported NG image files");

        int validTarget = Math.Max(2, labelCount / 4);
        int testTarget = Math.Max(2, labelCount / 4);
        int trainTarget = Math.Max(4, labelCount - validTarget - testTarget);
        List<string> train = imagePaths
            .Where(path =>
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                return !IsCircularSegmentationTestSplit(stem) && !IsCircularSegmentationValidSplit(stem);
            })
            .Take(trainTarget)
            .ToList();
        List<string> valid = imagePaths
            .Where(path => IsCircularSegmentationValidSplit(Path.GetFileNameWithoutExtension(path)))
            .Take(validTarget)
            .ToList();
        List<string> test = imagePaths
            .Where(path => IsCircularSegmentationTestSplit(Path.GetFileNameWithoutExtension(path)))
            .Take(testTarget)
            .ToList();
        AssertTrue(train.Count > 0, "could not find train-split image candidates for circular segmentation workflow");
        AssertTrue(valid.Count > 0, "could not find valid-split image candidates for circular segmentation workflow");
        AssertTrue(test.Count > 0, "could not find test-split image candidates for circular segmentation workflow");

        return train.Concat(valid).Concat(test)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsCircularSegmentationTestSplit(string fileStem)
        => StableCircularSegmentationBucket(fileStem, 17) < 20;

    private static bool IsCircularSegmentationValidSplit(string fileStem)
    {
        uint bucket = StableCircularSegmentationBucket(fileStem, 17);
        return bucket >= 20 && bucket < 40;
    }

    private static int CountCircularOkBackgroundLabels(string imageRoot, string outputRoot)
    {
        return Directory.EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories)
            .Where(path => ExeSmokeImageArtifactExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => IsUnderCircularSegmentationClassFolder(imageRoot, path, "OK"))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(stem => CircularSegmentationDatasetModes.Any(mode =>
            {
                string labelPath = Path.Combine(outputRoot, "data", mode, "labels", $"{stem}.txt");
                return File.Exists(labelPath) && File.ReadAllText(labelPath).Trim().Length == 0;
            }));
    }

    private static bool IsUnderCircularSegmentationClassFolder(string root, string path, string folderName)
    {
        string rootFullPath = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        DirectoryInfo directory = new FileInfo(path).Directory;
        while (directory != null)
        {
            if (string.Equals(directory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(directory.Name, folderName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }

    private static uint StableCircularSegmentationBucket(string value, int seed)
    {
        unchecked
        {
            uint hash = 2166136261;
            string key = (value ?? string.Empty).Trim().ToLowerInvariant();
            foreach (char ch in key)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            hash ^= (uint)seed;
            hash *= 16777619;
            return hash % 100;
        }
    }

    private static readonly string[] CircularSegmentationDatasetModes =
    {
        "train",
        "valid",
        "test"
    };

    private static void LabelCircularSegmentationImagesThroughExe(
        Process process,
        IReadOnlyList<string> selectedImages,
        string outputRoot,
        string screenshotDirectory,
        string screenshotPrefix = "06")
    {
        var random = new Random(260704);
        string firstBrushScreenshot = screenshotPrefix == "06" ? "06_first_brush_strokes" : screenshotPrefix + "_first_brush_strokes";
        string firstSavedScreenshot = screenshotPrefix == "06" ? "06a_first_label_saved" : screenshotPrefix + "_first_label_saved";
        string lastSavedScreenshot = screenshotPrefix == "06" ? "06b_last_label_saved" : screenshotPrefix + "_last_label_saved";
        int index = 0;
        foreach (string imagePath in selectedImages)
        {
            index++;
            string stem = Path.GetFileNameWithoutExtension(imagePath);
            AssertTrue(SelectCircularImageQueueItemThroughExe(process, stem, TimeSpan.FromSeconds(8)), "EXE image queue could not open " + stem);
            Thread.Sleep(350);
            var root = RefreshAutomationRoot(process);
            AssertTrue(OpenLabelingWorkbenchThroughExe(process), "labeling workbench stage was not selectable before brush labeling");
            root = RefreshAutomationRoot(process);
            _ = SelectAutomationTabByAutomationId(root, "LearningReviewTab") || SelectTabItemByName(root, "\uAC00\uC774\uB4DC/\uB3C4\uAD6C");
            root = RefreshAutomationRoot(process);
            System.Windows.Automation.AutomationElement statusElement = FindAutomationElementByAutomationId(root, "ModelStatusText");
            string previousCommitSignal = GetAutomationHelpText(statusElement);
            System.Windows.Automation.AutomationElement brushToolItem = FindAnnotationToolItem(root, "\uBE0C\uB7EC\uC2DC");
            AssertTrue(brushToolItem != null, "brush tool was not visible for circular segmentation labeling");
            NativeClick(GetAutomationCenter(brushToolItem));
            AssertTrue(
                WaitUntil(
                    () => string.Equals(GetSelectedAnnotationToolText(RefreshAutomationRoot(process, bringToFront: false)), "\uBE0C\uB7EC\uC2DC", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(2)),
                "brush tool was not selected for circular segmentation labeling");

            root = RefreshAutomationRoot(process);
            System.Windows.Automation.AutomationElement canvas = FindAutomationElementByClass(root, "RoiImageCanvasView");
            AssertTrue(canvas != null, "circular segmentation canvas was not found");
            System.Windows.Rect fittedImageRegion = BuildExeSmokeFittedImageRegion(canvas.Current.BoundingRectangle, imagePath);
            IReadOnlyList<ExeSmokeDragPath> drags = BuildCircularSegmentationBrushDrags(random, fittedImageRegion);
            _ = ExecuteExeSmokeDragBatch(drags, moveDelayMilliseconds: 1, postMouseUpMilliseconds: 50);

            if (index == 1)
            {
                CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, firstBrushScreenshot);
            }

            _ = ClickAnnotationToolByText(RefreshAutomationRoot(process), "\uC120\uD0DD");
            bool materialized = WaitForExeMaskCommitSignal(process, statusElement, previousCommitSignal, TimeSpan.FromSeconds(6))
                || IsAutomationButtonEnabledByAutomationId(RefreshAutomationRoot(process, bringToFront: false), "CanvasSaveAnnotationButton");
            if (!materialized)
            {
                root = RefreshAutomationRoot(process);
                statusElement = FindAutomationElementByAutomationId(root, "ModelStatusText");
                previousCommitSignal = GetAutomationHelpText(statusElement);
                brushToolItem = FindAnnotationToolItem(root, "\uBE0C\uB7EC\uC2DC");
                AssertTrue(brushToolItem != null, "brush tool was not visible for circular segmentation labeling retry");
                NativeClick(GetAutomationCenter(brushToolItem));
                AssertTrue(
                    WaitUntil(
                        () => string.Equals(GetSelectedAnnotationToolText(RefreshAutomationRoot(process, bringToFront: false)), "\uBE0C\uB7EC\uC2DC", StringComparison.Ordinal),
                        TimeSpan.FromSeconds(2)),
                    "brush tool was not selected for circular segmentation labeling retry");
                canvas = FindAutomationElementByClass(RefreshAutomationRoot(process), "RoiImageCanvasView");
                AssertTrue(canvas != null, "circular segmentation canvas was not found for retry");
                fittedImageRegion = BuildExeSmokeFittedImageRegion(canvas.Current.BoundingRectangle, imagePath);
                drags = BuildCircularSegmentationBrushDrags(random, fittedImageRegion);
                _ = ExecuteExeSmokeDragBatch(drags, moveDelayMilliseconds: 1, postMouseUpMilliseconds: 80);
                _ = ClickAnnotationToolByText(RefreshAutomationRoot(process), "\uC120\uD0DD");
                materialized = WaitForExeMaskCommitSignal(process, statusElement, previousCommitSignal, TimeSpan.FromSeconds(8))
                    || IsAutomationButtonEnabledByAutomationId(RefreshAutomationRoot(process, bringToFront: false), "CanvasSaveAnnotationButton");
            }

            AssertTrue(materialized, "mask strokes did not materialize before save for " + stem);
            _ = WaitUntil(
                () => IsAutomationButtonEnabledByAutomationId(RefreshAutomationRoot(process, bringToFront: false), "CanvasSaveAnnotationButton"),
                TimeSpan.FromSeconds(3));

            root = RefreshAutomationRoot(process);
            AssertTrue(ClickCanvasSaveAnnotation(root), "label save button was not clickable for " + stem);
            bool saved = WaitForCircularSegmentationArtifacts(outputRoot, stem, TimeSpan.FromSeconds(8));
            if (!saved)
            {
                root = RefreshAutomationRoot(process);
                if (IsAutomationButtonEnabledByAutomationId(root, "CanvasSaveAnnotationButton") && ClickCanvasSaveAnnotation(root))
                {
                    saved = WaitForCircularSegmentationArtifacts(outputRoot, stem, TimeSpan.FromSeconds(8));
                }
            }

            AssertTrue(saved, "EXE save did not create segment/mask artifacts for " + stem);

            IReadOnlyList<string> savedArtifacts = EnumerateExeSmokeSaveArtifactPaths(outputRoot, stem).Where(File.Exists).ToList();
            _ = ValidateExeSmokeSavedArtifactContents(savedArtifacts);

            if (index == 1 || index == selectedImages.Count)
            {
                CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, index == 1 ? firstSavedScreenshot : lastSavedScreenshot);
            }
        }
    }

    private static bool WaitForCircularSegmentationArtifacts(string outputRoot, string stem, TimeSpan timeout)
    {
        return WaitUntil(
            () =>
            {
                IReadOnlyList<string> artifacts = EnumerateExeSmokeSaveArtifactPaths(outputRoot, stem).Where(File.Exists).ToList();
                return artifacts.Any(path => path.Contains($"{Path.DirectorySeparatorChar}segments{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    && artifacts.Any(path => path.Contains($"{Path.DirectorySeparatorChar}masks{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            },
            timeout);
    }

    private static bool SelectCircularImageQueueItemThroughExe(Process process, string imageId, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        System.Windows.Automation.AutomationElement root = RefreshAutomationRoot(process);
        if (!TrySetExeAutomationValueByAutomationId(root, "ImageQueueSearchBox", imageId))
        {
            Console.Error.WriteLine("IMAGE_QUEUE_SEARCH_BOX_NOT_FOUND " + BuildAutomationTextSample(root, 80));
            return false;
        }

        while (DateTime.UtcNow <= deadline)
        {
            root = RefreshAutomationRoot(process, bringToFront: false);
            System.Windows.Automation.AutomationElement item = FindListItemContainingText(root, imageId);
            if (item == null)
            {
                Thread.Sleep(120);
                continue;
            }

            if (item.TryGetCurrentPattern(System.Windows.Automation.SelectionItemPattern.Pattern, out object pattern)
                && pattern is System.Windows.Automation.SelectionItemPattern selectionPattern)
            {
                selectionPattern.Select();
                Thread.Sleep(120);
            }

            System.Windows.Automation.AutomationElement textElement = FindDescendantTextContaining(item, imageId);
            Point itemCenter = GetAutomationCenter(item);
            Point textCenter = GetAutomationCenter(textElement ?? item);
            NativeClick(itemCenter);
            NativeClick(textCenter);

            root = RefreshAutomationRoot(process, bringToFront: false);
            _ = TryInvokeAutomationButton(root, "\uC5F4\uAE30");
            if (WaitUntil(
                    () =>
                    {
                        string datasetStatus = GetAutomationValueByAutomationId(RefreshAutomationRoot(process, bringToFront: false), "DatasetStatusText");
                        return datasetStatus.IndexOf(imageId, StringComparison.OrdinalIgnoreCase) >= 0;
                    },
                    TimeSpan.FromMilliseconds(700)))
            {
                return true;
            }

            NativeClick(itemCenter);
            NativeClick(itemCenter);
            Thread.Sleep(180);
        }

        Console.Error.WriteLine("IMAGE_QUEUE_SEARCH_RESULT_NOT_FOUND " + imageId + " " + BuildAutomationTextSample(RefreshAutomationRoot(process, bringToFront: false), 120));
        return false;
    }

    private static bool OpenLabelingWorkbenchThroughExe(Process process)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            System.Windows.Automation.AutomationElement root = RefreshAutomationRoot(process);
            if (FindAnnotationToolItem(root, "\uBE0C\uB7EC\uC2DC") != null
                || ContainsAutomationText(root, "\uC791\uC5C5: \uC800\uC7A5 \uB77C\uBCA8 \uD3B8\uC9D1")
                || ContainsAutomationText(root, "\uC791\uC5C5: \uBE60\uB978 \uB77C\uBCA8\uB9C1"))
            {
                return true;
            }

            if (!TryNativeClickAutomationElementByAutomationId(root, "LabelingWorkbenchStageButton"))
            {
                _ = TryInvokeAutomationButtonByAutomationId(root, "LabelingWorkbenchStageButton")
                    || TryInvokeAutomationButton(root, "\uB77C\uBCA8\uB9C1 \uC6CC\uD06C\uBCA4\uCE58");
            }

            Thread.Sleep(350);
        }

        return false;
    }

    private static IReadOnlyList<ExeSmokeDragPath> BuildCircularSegmentationBrushDrags(Random random, System.Windows.Rect region)
    {
        int centerX = (int)Math.Round(region.Left + region.Width * 0.50D);
        int centerY = (int)Math.Round(region.Top + region.Height * 0.52D);
        int radiusX = Math.Max(18, (int)Math.Round(region.Width * 0.12D));
        int radiusY = Math.Max(18, (int)Math.Round(region.Height * 0.12D));
        var drags = new List<ExeSmokeDragPath>();
        for (int row = -2; row <= 2; row++)
        {
            int y = centerY + (row * radiusY / 3);
            var start = new Point(centerX - radiusX + RandomRange(random, -4, 5), y + RandomRange(random, -3, 4));
            var end = new Point(centerX + radiusX + RandomRange(random, -4, 5), y + RandomRange(random, -3, 4));
            drags.Add(CreateExeSmokeDragPath(random, start, end, 8, jitterPixels: 1, region));
        }

        return drags;
    }

    private static bool ClickCanvasSaveAnnotation(System.Windows.Automation.AutomationElement root)
    {
        System.Windows.Automation.AutomationElement saveButton = FindAutomationElementByAutomationId(root, "CanvasSaveAnnotationButton");
        if (saveButton != null && saveButton.Current.ControlType == System.Windows.Automation.ControlType.Button && saveButton.Current.IsEnabled)
        {
            NativeClick(GetAutomationCenter(saveButton));
            return true;
        }

        saveButton = FindEnabledAutomationButton(root, "\uB77C\uBCA8 \uC800\uC7A5")
            ?? FindEnabledAutomationButton(root, "\uC800\uC7A5");
        if (saveButton == null)
        {
            return false;
        }

        NativeClick(GetAutomationCenter(saveButton));
        return true;
    }

    private static string TrainYoloV8SegmentationThroughExe(
        Process process,
        string yoloRoot,
        string outputRoot,
        IReadOnlyList<string> validationAndTestImages,
        string screenshotDirectory)
    {
        string trainedWeightsPath = Path.Combine(yoloRoot, "runs", "segment", "openvisionlab-yolov8-segment", "weights", "best.pt");
        DateTime previousBestWriteUtc = File.Exists(trainedWeightsPath)
            ? File.GetLastWriteTimeUtc(trainedWeightsPath)
            : DateTime.MinValue;

        var root = RefreshAutomationRoot(process);
        AssertTrue(
            OpenYoloModelCenterThroughExe(process, IntPtr.Zero),
            "model settings tab was not selectable before training");
        AssertTrue(
            TryExpandYoloSettingsSection(process, IntPtr.Zero, "TrainingSettingsExpander", "TrainingSettingsSummaryPanel"),
            "training settings section was not expandable");
        root = RefreshAutomationRoot(process);
        AssertTrue(
            TryBringYoloSettingsElementIntoView(process, IntPtr.Zero, "TrainingAdvancedSettingsExpander"),
            "training advanced settings expander was not reachable");
        root = RefreshAutomationRoot(process);
        AssertTrue(TryExpandAutomationElementByAutomationId(root, "TrainingAdvancedSettingsExpander"), "training advanced settings expander was not expandable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, IntPtr.Zero, "TrainingImageSizeBox", "64"), "training image size was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, IntPtr.Zero, "TrainingBatchBox", "1"), "training batch was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, IntPtr.Zero, "TrainingEpochBox", "1"), "training epoch was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, IntPtr.Zero, "TrainingValidationPercentBox", "20"), "training validation percent was not editable");
        AssertTrue(TryPasteYoloSettingsValueThroughExe(process, IntPtr.Zero, "TrainingTestPercentBox", "20"), "training test percent was not editable");
        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "08_training_settings_ready");

        root = RefreshAutomationRoot(process);
        AssertTrue(
            TryInvokeAutomationButtonByAutomationId(root, "RefreshTrainingReadinessButton")
                || TryInvokeAutomationButtonByAutomationId(root, "YoloDatasetQuickRefreshButton"),
            "training split settings refresh button was not invokable before validation/test labeling");
        Thread.Sleep(1200);
        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "08a_training_split_settings_saved");

        if (validationAndTestImages.Count > 0)
        {
            LabelCircularSegmentationImagesThroughExe(process, validationAndTestImages, outputRoot, screenshotDirectory, "08b_split");
            AssertTrue(CountFiles(Path.Combine(outputRoot, "data", "valid", "segments"), "*.json") > 0, "EXE workflow did not save validation segment JSON files after split setup");
            AssertTrue(CountFiles(Path.Combine(outputRoot, "data", "test", "segments"), "*.json") > 0, "EXE workflow did not save held-out test segment JSON files");
            AssertTrue(CountFiles(Path.Combine(outputRoot, "data", "valid", "masks"), "*.png") > 0, "EXE workflow did not save validation mask PNG files after split setup");
            AssertTrue(CountFiles(Path.Combine(outputRoot, "data", "test", "masks"), "*.png") > 0, "EXE workflow did not save held-out test mask PNG files");
            AssertTrue(
                OpenYoloModelCenterThroughExe(process, IntPtr.Zero),
                "model settings tab was not selectable after held-out test labeling");
            AssertTrue(
                TryExpandYoloSettingsSection(process, IntPtr.Zero, "TrainingSettingsExpander", "TrainingSettingsSummaryPanel"),
                "training settings section was not expandable after held-out test labeling");
        }

        root = RefreshAutomationRoot(process);
        AssertTrue(
            TryInvokeAutomationButtonByAutomationId(root, "RefreshTrainingReadinessButton")
                || TryInvokeAutomationButtonByAutomationId(root, "YoloDatasetQuickRefreshButton"),
            "training readiness refresh button was not invokable");
        Thread.Sleep(1200);
        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "08c_training_readiness_refreshed");

        root = RefreshAutomationRoot(process);
        AssertTrue(
            TryInvokeAutomationButtonByAutomationId(root, "StartTrainingButton")
                || TryInvokeAutomationButtonByAutomationId(root, "YoloLifecycleStartTrainingButton"),
            "start training button was not invokable");
        Thread.Sleep(1500);
        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "09_training_started");

        AssertTrue(
            WaitUntil(
                () =>
                {
                    if (!File.Exists(trainedWeightsPath))
                    {
                        return false;
                    }

                    DateTime currentWriteUtc = File.GetLastWriteTimeUtc(trainedWeightsPath);
                    string resultsPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(trainedWeightsPath)) ?? string.Empty, "results.csv");
                    string argsPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(trainedWeightsPath)) ?? string.Empty, "args.yaml");
                    string normalizedArgsText = File.Exists(argsPath)
                        ? File.ReadAllText(argsPath).Replace("\\", "/", StringComparison.Ordinal)
                        : string.Empty;
                    return currentWriteUtc > previousBestWriteUtc
                        && File.Exists(resultsPath)
                        && File.Exists(argsPath)
                        && normalizedArgsText.IndexOf(outputRoot.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase) >= 0
                        && IsTrainingCompletionVisibleInExe(process);
                },
                TimeSpan.FromMinutes(5)),
            "YOLOv8 segmentation training did not produce a fresh best.pt for the EXE dataset");

        Thread.Sleep(1500);
        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "10_training_completed");
        return trainedWeightsPath;
    }

    private static bool IsTrainingCompletionVisibleInExe(Process process)
    {
        System.Windows.Automation.AutomationElement latestRoot = RefreshAutomationRoot(process, bringToFront: false);
        return IsAutomationButtonEnabledByAutomationId(latestRoot, "ConfirmTrainedModelButton")
            || ContainsAutomationText(latestRoot, "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uB4F1\uB85D")
            || ContainsAutomationText(latestRoot, "\uD559\uC2B5 \uC644\uB8CC / 100%");
    }

    private static void ApplyTrainedModelAndRunInferenceThroughExe(
        Process process,
        string trainedWeightsPath,
        string screenshotDirectory,
        out string inferenceStatus)
    {
        var root = RefreshAutomationRoot(process);
        AssertTrue(
            OpenYoloModelCenterThroughExe(process, IntPtr.Zero),
            "model settings tab was not selectable before inference");
        _ = TryExpandYoloSettingsSection(process, IntPtr.Zero, "TrainingSettingsExpander", "ConfirmTrainedModelButton");
        root = RefreshAutomationRoot(process);
        _ = TryBringYoloSettingsElementIntoView(process, IntPtr.Zero, "ConfirmTrainedModelButton");
        root = RefreshAutomationRoot(process);
        if (IsAutomationButtonEnabledByAutomationId(root, "ConfirmTrainedModelButton"))
        {
            AssertTrue(TryInvokeAutomationButtonByAutomationId(root, "ConfirmTrainedModelButton"), "confirm trained model button was enabled but not invokable");
            Thread.Sleep(1200);
        }
        else
        {
            AssertTrue(
                TryExpandYoloSettingsSection(process, IntPtr.Zero, "YoloModelSettingsExpander", "YoloWeightsPathBox"),
                "YOLO model settings section was not expandable for trained model");
            AssertTrue(
                TryBringYoloSettingsElementIntoView(process, IntPtr.Zero, "YoloWeightsPathBox"),
                "YOLO trained weights field was not reachable for inference");
            root = RefreshAutomationRoot(process);
            AssertTrue(TrySetExeAutomationValueByAutomationId(root, "YoloWeightsPathBox", trainedWeightsPath), "trained weights path was not editable for inference");
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(root, "SaveYoloSettingsButton")
                    || TryInvokeAutomationButton(root, "\uC800\uC7A5"),
                "YOLO settings save button was not invokable for trained model");
            Thread.Sleep(1200);
        }

        CaptureWorkflowStep(RefreshAutomationRoot(process), screenshotDirectory, "11_trained_model_applied");

        AssertTrue(OpenInferenceReviewThroughExe(process), "inference review stage was not selectable for trained YOLOv8 inference");
        root = RefreshAutomationRoot(process);
        bool currentInspectionInvoked =
            TryInvokeAutomationButtonByAutomationId(root, "YoloLifecycleInspectCurrentImageButton")
            || TryInvokeAutomationButtonByAutomationId(root, "ModelCenterPriorityInspectCurrentButton")
            || TryInvokeAutomationButtonByAutomationId(root, "WorkflowStageInspectCurrentImageButton")
            || TryInvokeAutomationButton(root, "\uD604\uC7AC \uAC80\uC0AC");

        string observedInferenceStatus = string.Empty;
        if (currentInspectionInvoked)
        {
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        var latestRoot = RefreshAutomationRoot(process, bringToFront: false);
                        observedInferenceStatus = ReadExeInferenceStatusSnapshot(latestRoot);
                        return IsExeTrainedInferenceFinished(latestRoot, observedInferenceStatus);
                    },
                    TimeSpan.FromMinutes(6)),
                "trained YOLOv8 inference did not report completion/status in the EXE");
            AssertTrue(
                !IsExeTrainedInferenceFailure(observedInferenceStatus),
                "trained YOLOv8 inference failed in the EXE; status=" + observedInferenceStatus);
        }
        else
        {
            AssertTrue(
                IsTrainedModelInferenceEvidenceVisible(root),
                "trained YOLOv8 model validation evidence was not visible in the EXE inference review stage");
            observedInferenceStatus = "trained YOLOv8 model validation visible";
        }

        root = RefreshAutomationRoot(process);
        inferenceStatus = FirstNonEmpty(ReadExeInferenceStatusSnapshot(root), "inference status text not exposed");
        bool hasCandidateStatus = inferenceStatus.Contains("\uD6C4\uBCF4:", StringComparison.Ordinal)
            || inferenceStatus.Contains("candidate", StringComparison.OrdinalIgnoreCase);
        if (hasCandidateStatus)
        {
            AssertTrue(
                inferenceStatus.Contains("NG", StringComparison.OrdinalIgnoreCase),
                "trained YOLOv8 segmentation inference should use the dataset class after applying best.pt; status=" + inferenceStatus);
        }

        AssertTrue(
            !inferenceStatus.Contains("toilet", StringComparison.OrdinalIgnoreCase),
            "trained YOLOv8 segmentation inference used stale COCO seed weights after applying best.pt; status=" + inferenceStatus);
        CaptureWorkflowStep(root, screenshotDirectory, "12_trained_model_inference");
    }

    private static string ReadExeInferenceStatusSnapshot(System.Windows.Automation.AutomationElement root)
    {
        return string.Join(
            " / ",
            new[]
            {
                GetAutomationValueByAutomationId(root, "YoloCommandStatusText"),
                GetAutomationValueByAutomationId(root, "ModelStatusText"),
                GetAutomationValueByAutomationId(root, "PythonStatusText")
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static bool IsExeTrainedInferenceFinished(System.Windows.Automation.AutomationElement root, string status)
    {
        if (IsExeTrainedInferenceFailure(status))
        {
            return true;
        }

        return status.Contains("\uCD94\uB860 \uC644\uB8CC", StringComparison.Ordinal)
            || status.Contains("\uC644\uB8CC: \uD6C4\uBCF4", StringComparison.Ordinal)
            || status.Contains("\uCD94\uB860: \uC644\uB8CC", StringComparison.Ordinal);
    }

    private static bool IsExeTrainedInferenceFailure(string status)
    {
        return status.Contains("\uCD94\uB860 \uC2E4\uD328", StringComparison.Ordinal)
            || status.Contains("\uC2E4\uD328:", StringComparison.Ordinal)
            || status.Contains("did not connect", StringComparison.OrdinalIgnoreCase)
            || status.Contains("connection failure", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrainedModelInferenceEvidenceVisible(System.Windows.Automation.AutomationElement root)
    {
        return ContainsAutomationText(root, "YOLOv8")
            && ContainsAutomationText(root, "openvisionlab-yolov8-segment")
            && (ContainsAutomationText(root, "\uD559\uC2B5 \uBAA8\uB378 \uAC80\uC99D")
                || ContainsAutomationText(root, "AI \uD6C4\uBCF4 \uC5C6\uC74C")
                || ContainsAutomationText(root, "\uC120\uD0DD \uD6C4\uBCF4 \uC5C6\uC74C"));
    }

    private static bool OpenInferenceReviewThroughExe(Process process)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            System.Windows.Automation.AutomationElement root = RefreshAutomationRoot(process);
            if (ContainsAutomationText(root, "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uD6C4\uBCF4 \uAC80\uD1A0")
                || ContainsAutomationText(root, "\uD559\uC2B5 \uBAA8\uB378 \uAC80\uC99D"))
            {
                return true;
            }

            if (!TryNativeClickAutomationElementByAutomationId(root, "InferenceReviewStageButton"))
            {
                _ = TryInvokeAutomationButtonByAutomationId(root, "InferenceReviewStageButton")
                    || TryInvokeAutomationButton(root, "\uCD94\uB860 \uAC80\uD1A0");
            }

            Thread.Sleep(350);
        }

        return false;
    }

    private static void CaptureWorkflowStep(System.Windows.Automation.AutomationElement root, string screenshotDirectory, string stepName)
    {
        Directory.CreateDirectory(screenshotDirectory);
        CaptureAutomationRoot(root, Path.Combine(screenshotDirectory, stepName + ".png"));
    }

    private static int CountFiles(string directoryPath, string searchPattern)
        => Directory.Exists(directoryPath)
            ? Directory.EnumerateFiles(directoryPath, searchPattern).Count()
            : 0;

    private static bool TryBringYoloSettingsElementIntoView(Process process, IntPtr stableHandle, string automationId)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            System.Windows.Automation.AutomationElement root = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: false)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            System.Windows.Automation.AutomationElement element = FindAutomationElementByAutomationId(root, automationId);
            System.Windows.Automation.AutomationElement scrollViewer = FindAutomationElementByAutomationId(root, "YoloSettingsScrollViewer");
            if (element != null && IsAutomationElementVisible(element) && IsAutomationElementInScrollViewport(element, scrollViewer))
            {
                return true;
            }

            System.Windows.Automation.ScrollAmount amount = GetYoloSettingsScrollAmount(element, scrollViewer);
            if (!TryScrollAutomationElementByAutomationId(root, "YoloSettingsScrollViewer", amount, repeatCount: 2))
            {
            NativeMouseWheel(GetYoloSettingsWheelPoint(root, scrollViewer), amount == System.Windows.Automation.ScrollAmount.SmallDecrement ? 720 : -720);
            }

            Thread.Sleep(220);

        }

        return false;
    }

    private static bool OpenYoloModelCenterThroughExe(Process process, IntPtr stableHandle)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (stableHandle != IntPtr.Zero)
            {
                SetWindowPos(stableHandle, HwndTopMost, 0, 0, VisualSmokeDefaultWindowWidth, VisualSmokeDefaultWindowHeight, SwpShowWindow);
                BringNativeWindowToFront(stableHandle);
            }

            System.Windows.Automation.AutomationElement root = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: true)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: true);
            if (IsAutomationElementVisibleByAutomationId(root, "YoloSettingsScrollViewer")
                && (IsAutomationElementVisibleByAutomationId(root, "YoloModelLifecycleProgressBar")
                    || IsAutomationElementVisibleByAutomationId(root, "ModelCenterPriorityPanel")))
            {
                return true;
            }

            if (!TryNativeClickAutomationElementByAutomationId(root, "TrainingModelStageButton")
                && !TryInvokeAutomationButtonByAutomationId(root, "TrainingModelStageButton")
                && !TryNativeClickAutomationElementByName(root, "4 학습/모델")
                && !TryInvokeAutomationButton(root, "4 학습/모델"))
            {
                _ = TryNativeClickAutomationElementByName(root, "학습/모델")
                    || TryInvokeAutomationButton(root, "학습/모델");
            }

            root = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: false)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            if (IsAutomationButtonEnabledByAutomationId(root, "RightWorkflowTrainingModelButton"))
            {
                _ = TryInvokeAutomationButtonByAutomationId(root, "RightWorkflowTrainingModelButton");
            }

            _ = SelectAutomationTabByAutomationId(root, "YoloSettingsReviewTab") || SelectTabItemByName(root, "\uBAA8\uB378");
            Thread.Sleep(500);
        }

        return false;
    }

    private static bool TryExpandYoloAdvancedModelSettingsThroughExe(Process process, IntPtr stableHandle)
    {
        for (int attempt = 0; attempt < 18; attempt++)
        {
            System.Windows.Automation.AutomationElement root = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: false)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            if (FindAutomationElementByAutomationId(root, "YoloModelEngineBox") != null)
            {
                return true;
            }

            if (FindAutomationElementByAutomationId(root, "YoloAdvancedModelSettingsExpander") != null
                && TryExpandAutomationElementByAutomationId(root, "YoloAdvancedModelSettingsExpander"))
            {
                Thread.Sleep(250);
                continue;
            }

            if (TryNativeClickAutomationElementByName(root, "\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC0C1\uC138"))
            {
                Thread.Sleep(250);
                continue;
            }

            System.Windows.Automation.ScrollAmount amount = ContainsAutomationText(root, "\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC0C1\uC138")
                ? System.Windows.Automation.ScrollAmount.SmallIncrement
                : System.Windows.Automation.ScrollAmount.LargeIncrement;
            if (!TryScrollAutomationElementByAutomationId(root, "YoloSettingsScrollViewer", amount, repeatCount: 1))
            {
                NativeMouseWheel(GetYoloSettingsWheelPoint(root, FindAutomationElementByAutomationId(root, "YoloSettingsScrollViewer")), -720);
            }

            Thread.Sleep(220);
        }

        return false;
    }

    private static bool TryPasteYoloSettingsValueThroughExe(
        Process process,
        IntPtr stableHandle,
        string automationId,
        string value)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            System.Windows.Automation.AutomationElement directRoot = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: false)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            if (TrySetExeAutomationValueByAutomationId(directRoot, automationId, value))
            {
                return true;
            }

            Thread.Sleep(150);
        }

        if (!TryBringYoloSettingsElementIntoView(process, stableHandle, automationId))
        {
            System.Windows.Automation.AutomationElement offscreenRoot = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: false)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            return TrySetExeAutomationValueByAutomationId(offscreenRoot, automationId, value);
        }

        System.Windows.Automation.AutomationElement root = stableHandle == IntPtr.Zero
            ? RefreshAutomationRoot(process, bringToFront: false)
            : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
        return TrySetExeAutomationValueByAutomationId(root, automationId, value);
    }

    private static bool TrySetExeAutomationValueByAutomationId(
        System.Windows.Automation.AutomationElement root,
        string automationId,
        string value)
    {
        System.Windows.Automation.AutomationElement element = FindAutomationElementByAutomationId(root, automationId);
        if (element == null || !element.Current.IsEnabled)
        {
            return false;
        }

        try
        {
            if (!element.TryGetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern, out object pattern)
                || pattern is not System.Windows.Automation.ValuePattern valuePattern)
            {
                return false;
            }

            string text = value ?? string.Empty;
            valuePattern.SetValue(text);
            Thread.Sleep(120);
            return string.Equals(GetAutomationValueByAutomationId(root, automationId), text, StringComparison.Ordinal);
        }
        catch (System.Windows.Automation.ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TrySelectVisibleComboBoxItemByName(
        Process process,
        IntPtr stableHandle,
        string comboAutomationId,
        string itemName,
        out string selectedItem)
    {
        selectedItem = string.Empty;
        System.Windows.Automation.AutomationElement root = stableHandle == IntPtr.Zero
            ? RefreshAutomationRoot(process, bringToFront: false)
            : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
        System.Windows.Automation.AutomationElement comboBox = FindAutomationElementByAutomationId(root, comboAutomationId);
        if (comboBox == null || !comboBox.Current.IsEnabled)
        {
            return false;
        }

        if (!TryExpandAutomationElementByAutomationId(root, comboAutomationId))
        {
            NativeClick(GetAutomationCenter(comboBox));
            Thread.Sleep(220);
        }

        System.Windows.Rect comboBounds = comboBox.Current.BoundingRectangle;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            System.Windows.Automation.AutomationElement item = FindVisibleProcessElementNearBounds(process, itemName, comboBounds);
            if (item != null)
            {
                if (item.TryGetCurrentPattern(System.Windows.Automation.SelectionItemPattern.Pattern, out object pattern)
                    && pattern is System.Windows.Automation.SelectionItemPattern selectionPattern)
                {
                    selectionPattern.Select();
                }
                else
                {
                    NativeClick(GetAutomationCenter(item));
                }

                selectedItem = itemName;
                if (WaitUntil(
                    () =>
                    {
                        System.Windows.Automation.AutomationElement latestRoot = stableHandle == IntPtr.Zero
                            ? RefreshAutomationRoot(process, bringToFront: false)
                            : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                        string value = GetAutomationValueByAutomationId(latestRoot, comboAutomationId);
                        return value.Contains(itemName, StringComparison.OrdinalIgnoreCase);
                    },
                    TimeSpan.FromSeconds(2)))
                {
                    return true;
                }
            }

            Thread.Sleep(180);
        }

        if (TryClickComboBoxRelativeOption(process, stableHandle, comboAutomationId, itemName, comboBox, -92)
            || TryClickComboBoxRelativeOption(process, stableHandle, comboAutomationId, itemName, comboBox, 52)
            || TryClickComboBoxRelativeOption(process, stableHandle, comboAutomationId, itemName, comboBox, 82))
        {
            selectedItem = itemName;
            return true;
        }

        if (TrySelectComboBoxSecondItemByKeyboard(process, stableHandle, comboAutomationId, itemName, comboBox))
        {
            selectedItem = itemName;
            return true;
        }

        selectedItem = string.Empty;
        return false;
    }

    private static string GetSelectedComboBoxItemNameByAutomationId(
        System.Windows.Automation.AutomationElement root,
        string comboAutomationId)
    {
        System.Windows.Automation.AutomationElement comboBox = FindAutomationElementByAutomationId(root, comboAutomationId);
        try
        {
            if (comboBox == null
                || !comboBox.TryGetCurrentPattern(System.Windows.Automation.SelectionPattern.Pattern, out object pattern)
                || pattern is not System.Windows.Automation.SelectionPattern selectionPattern)
            {
                return string.Empty;
            }

            System.Windows.Automation.AutomationElement[] selectedItems = selectionPattern.Current.GetSelection();
            return selectedItems.Length > 0
                ? selectedItems[0].Current.Name ?? string.Empty
                : string.Empty;
        }
        catch (System.Windows.Automation.ElementNotAvailableException)
        {
            return string.Empty;
        }
    }

    private static bool TryClickComboBoxRelativeOption(
        Process process,
        IntPtr stableHandle,
        string comboAutomationId,
        string expectedItemName,
        System.Windows.Automation.AutomationElement comboBox,
        int yOffsetFromComboTop)
    {
        System.Windows.Rect bounds = comboBox.Current.BoundingRectangle;
        var point = new Point(
            (int)Math.Round(bounds.Left + Math.Max(24D, bounds.Width * 0.40D)),
            (int)Math.Round(bounds.Top + yOffsetFromComboTop));
        NativeClick(point);
        Thread.Sleep(250);
        return WaitUntil(
            () =>
            {
                System.Windows.Automation.AutomationElement latestRoot = stableHandle == IntPtr.Zero
                    ? RefreshAutomationRoot(process, bringToFront: false)
                    : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                string value = GetAutomationValueByAutomationId(latestRoot, comboAutomationId);
                return value.Contains(expectedItemName, StringComparison.OrdinalIgnoreCase);
            },
            TimeSpan.FromSeconds(2));
    }

    private static bool TrySelectComboBoxSecondItemByKeyboard(
        Process process,
        IntPtr stableHandle,
        string comboAutomationId,
        string expectedItemName,
        System.Windows.Automation.AutomationElement comboBox)
    {
        try
        {
            NativeClick(GetAutomationCenter(comboBox));
            Thread.Sleep(120);
            SendKeys.SendWait("%{DOWN}");
            Thread.Sleep(120);
            SendKeys.SendWait("{HOME}");
            Thread.Sleep(80);
            SendKeys.SendWait("{DOWN}");
            Thread.Sleep(80);
            SendKeys.SendWait("{ENTER}");
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return WaitUntil(
            () =>
            {
                System.Windows.Automation.AutomationElement latestRoot = stableHandle == IntPtr.Zero
                    ? RefreshAutomationRoot(process, bringToFront: false)
                    : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                string value = GetAutomationValueByAutomationId(latestRoot, comboAutomationId);
                return value.Contains(expectedItemName, StringComparison.OrdinalIgnoreCase);
            },
            TimeSpan.FromSeconds(2));
    }

    private static System.Windows.Automation.AutomationElement FindVisibleProcessElementNearBounds(
        Process process,
        string name,
        System.Windows.Rect ownerBounds)
    {
        var condition = new System.Windows.Automation.AndCondition(
            new System.Windows.Automation.PropertyCondition(
                System.Windows.Automation.AutomationElement.ProcessIdProperty,
                process.Id),
            new System.Windows.Automation.PropertyCondition(
                System.Windows.Automation.AutomationElement.NameProperty,
                name));
        System.Windows.Automation.AutomationElementCollection matches;
        try
        {
            matches = System.Windows.Automation.AutomationElement.RootElement.FindAll(
                System.Windows.Automation.TreeScope.Descendants,
                condition);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }

        foreach (System.Windows.Automation.AutomationElement element in matches)
        {
            try
            {
                System.Windows.Rect bounds = element.Current.BoundingRectangle;
                bool nearOwner = bounds.Width > 0
                    && bounds.Height > 0
                    && bounds.Top >= ownerBounds.Top - 260D
                    && bounds.Top <= ownerBounds.Bottom + 260D
                    && bounds.Left >= ownerBounds.Left - 100D
                    && bounds.Left <= ownerBounds.Right + 160D;
                if (element.Current.IsEnabled && nearOwner)
                {
                    return element;
                }
            }
            catch (System.Windows.Automation.ElementNotAvailableException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        }

        return null;
    }

    private static bool TryNativeClickAutomationElementByAutomationId(System.Windows.Automation.AutomationElement root, string automationId)
    {
        System.Windows.Automation.AutomationElement element = FindAutomationElementByAutomationId(root, automationId);
        try
        {
            if (element == null || !element.Current.IsEnabled || !IsAutomationElementVisible(element))
            {
                return false;
            }

            NativeClick(GetAutomationCenter(element));
            return true;
        }
        catch (System.Windows.Automation.ElementNotAvailableException)
        {
            return false;
        }
    }

    private static bool TryNativeClickAutomationElementByName(System.Windows.Automation.AutomationElement root, string name)
    {
        foreach (System.Windows.Automation.AutomationElement element in EnumerateAutomationDescendants(root))
        {
            try
            {
                if (!string.Equals(element.Current.Name, name, StringComparison.Ordinal)
                    || !IsAutomationElementVisible(element))
                {
                    continue;
                }

                NativeClick(GetAutomationCenter(element));
                return true;
            }
            catch (System.Windows.Automation.ElementNotAvailableException)
            {
                continue;
            }
        }

        return false;
    }

    private static bool TryExpandYoloSettingsSection(Process process, IntPtr stableHandle, string sectionAutomationId, string expectedAutomationId)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            System.Windows.Automation.AutomationElement root = stableHandle == IntPtr.Zero
                ? RefreshAutomationRoot(process, bringToFront: false)
                : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            string taskAutomationId = string.Equals(sectionAutomationId, "YoloModelSettingsExpander", StringComparison.Ordinal)
                ? "YoloModelCenterRuntimeTaskTab"
                : "YoloModelCenterTrainingTaskTab";
            string taskName = string.Equals(sectionAutomationId, "YoloModelSettingsExpander", StringComparison.Ordinal)
                ? "실행기"
                : "학습/비교";
            if (FindAutomationElementByAutomationId(root, expectedAutomationId) == null
                && FindAutomationElementByAutomationId(root, sectionAutomationId) == null
                && (SelectAutomationTabByAutomationId(root, taskAutomationId) || SelectTabItemByName(root, taskName)))
            {
                Thread.Sleep(250);
                root = stableHandle == IntPtr.Zero
                    ? RefreshAutomationRoot(process, bringToFront: false)
                    : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            }

            if (FindAutomationElementByAutomationId(root, expectedAutomationId) != null)
            {
                return true;
            }

            if (IsYoloSettingsSectionContentVisible(root, sectionAutomationId))
            {
                return true;
            }

            if (FindAutomationElementByAutomationId(root, sectionAutomationId) != null
                && TryExpandAutomationElementByAutomationId(root, sectionAutomationId))
            {
                Thread.Sleep(250);
                root = stableHandle == IntPtr.Zero
                    ? RefreshAutomationRoot(process, bringToFront: false)
                    : RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                if (FindAutomationElementByAutomationId(root, expectedAutomationId) != null)
                {
                    return true;
                }
            }

            if (!TryScrollAutomationElementByAutomationId(
                    root,
                    "YoloSettingsScrollViewer",
                    System.Windows.Automation.ScrollAmount.SmallIncrement,
                    repeatCount: 1))
            {
                NativeMouseWheel(GetYoloSettingsWheelPoint(root, FindAutomationElementByAutomationId(root, "YoloSettingsScrollViewer")), -720);
            }

            Thread.Sleep(220);

        }

        return false;
    }

    private static bool IsYoloSettingsSectionContentVisible(System.Windows.Automation.AutomationElement root, string sectionAutomationId)
    {
        if (string.Equals(sectionAutomationId, "YoloModelSettingsExpander", StringComparison.Ordinal))
        {
            return ContainsAutomationText(root, "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uD504\uB85C\uD544")
                || ContainsAutomationText(root, "\uBAA8\uB378 \uD504\uB85C\uD544:");
        }

        if (string.Equals(sectionAutomationId, "TrainingSettingsExpander", StringComparison.Ordinal))
        {
            return ContainsAutomationText(root, "\uBE60\uB978 \uCD94\uCC9C")
                || ContainsAutomationText(root, "\uD559\uC2B5 \uC124\uC815");
        }

        return false;
    }

    private static System.Windows.Automation.ScrollAmount GetYoloSettingsScrollAmount(
        System.Windows.Automation.AutomationElement element,
        System.Windows.Automation.AutomationElement scrollViewer)
    {
        if (element == null || scrollViewer == null)
        {
            return System.Windows.Automation.ScrollAmount.LargeIncrement;
        }

        System.Windows.Rect elementBounds = element.Current.BoundingRectangle;
        System.Windows.Rect scrollBounds = scrollViewer.Current.BoundingRectangle;
        return elementBounds.Top < scrollBounds.Top + 12D
            ? System.Windows.Automation.ScrollAmount.SmallDecrement
            : System.Windows.Automation.ScrollAmount.SmallIncrement;
    }

    private static bool IsAutomationElementInScrollViewport(
        System.Windows.Automation.AutomationElement element,
        System.Windows.Automation.AutomationElement scrollViewer)
    {
        if (element == null || scrollViewer == null)
        {
            return false;
        }

        System.Windows.Rect elementBounds = element.Current.BoundingRectangle;
        System.Windows.Rect scrollBounds = scrollViewer.Current.BoundingRectangle;
        double centerX = elementBounds.Left + (elementBounds.Width / 2D);
        double centerY = elementBounds.Top + (elementBounds.Height / 2D);
        return centerX >= scrollBounds.Left + 8D
            && centerX <= scrollBounds.Right - 8D
            && centerY >= scrollBounds.Top + 8D
            && centerY <= scrollBounds.Bottom - 8D;
    }

    private static Point GetYoloSettingsWheelPoint(
        System.Windows.Automation.AutomationElement root,
        System.Windows.Automation.AutomationElement scrollViewer)
    {
        if (scrollViewer != null)
        {
            try
            {
                System.Windows.Rect scrollBounds = scrollViewer.Current.BoundingRectangle;
                return new Point(
                    (int)Math.Round(scrollBounds.Left + (scrollBounds.Width * 0.5D)),
                    (int)Math.Round(scrollBounds.Top + (scrollBounds.Height * 0.5D)));
            }
            catch (System.Windows.Automation.ElementNotAvailableException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        }

        System.Windows.Rect bounds;
        try
        {
            bounds = root?.Current.BoundingRectangle ?? new System.Windows.Rect(0D, 0D, VisualSmokeDefaultWindowWidth, VisualSmokeDefaultWindowHeight);
        }
        catch (System.Windows.Automation.ElementNotAvailableException)
        {
            bounds = new System.Windows.Rect(0D, 0D, VisualSmokeDefaultWindowWidth, VisualSmokeDefaultWindowHeight);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            bounds = new System.Windows.Rect(0D, 0D, VisualSmokeDefaultWindowWidth, VisualSmokeDefaultWindowHeight);
        }

        return new Point(
            (int)Math.Round(bounds.X + (bounds.Width * 0.16D)),
            (int)Math.Round(bounds.Y + (bounds.Height * 0.56D)));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private readonly record struct ExeCircularSegmentationWorkflowResult(
        string TrainedWeightsPath,
        int TrainSegmentCount,
        int ValidSegmentCount,
        int TestSegmentCount,
        int BackgroundLabelCount,
        string InferenceStatus);
}
