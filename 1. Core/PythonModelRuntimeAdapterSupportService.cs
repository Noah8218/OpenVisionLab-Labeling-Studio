using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeAdapterSupport
    {
        public PythonModelRuntimeAdapterSupport(
            bool isExecutionSupported,
            bool canTrain,
            bool canInspect,
            string summaryText,
            string detailText,
            string nextActionText)
        {
            IsExecutionSupported = isExecutionSupported;
            CanTrain = canTrain;
            CanInspect = canInspect;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
        }

        public bool IsExecutionSupported { get; }
        public bool CanTrain { get; }
        public bool CanInspect { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
    }

    public static class PythonModelRuntimeAdapterSupportService
    {
        public static PythonModelRuntimeAdapterSupport Build(
            PythonModelSettings settings,
            IEnumerable<string> supportedModels = null,
            IEnumerable<string> trainingModels = null,
            IEnumerable<string> detectionModels = null)
        {
            string engine = PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine);
            string adapterKey = settings?.GetProtocolModelName() ?? "yolov5";
            if (string.Equals(engine, PythonModelSettings.EngineYoloV5, StringComparison.Ordinal))
            {
                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: true,
                    canTrain: true,
                    canInspect: true,
                    "YOLOv5 worker \uC5F0\uACB0 \uC9C0\uC6D0",
                    "YOLOv5 TCP worker\uB85C \uD559\uC2B5\uACFC \uD604\uC7AC \uAC80\uC0AC\uB97C \uC2E4\uD589\uD569\uB2C8\uB2E4.",
                    "YOLOv5 \uD559\uC2B5/\uAC80\uC0AC \uC2E4\uD589 \uAC00\uB2A5");
            }

            if (string.Equals(engine, PythonModelSettings.EngineUnet, StringComparison.Ordinal))
            {
                bool workerPresent = PythonModelRuntimeBundledWorkerService.IsUnetWorkerScriptPath(settings?.ClientScriptPath);
                bool torchInstalled = IsTorchInstalled(settings);
                bool capabilityMismatch = HasAnyCapability(supportedModels)
                    && !SupportsModel(adapterKey, supportedModels);
                if (workerPresent && torchInstalled && !capabilityMismatch)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: true,
                        canTrain: true,
                        canInspect: true,
                        "U-Net worker connection ready",
                        "The bundled U-Net worker and PyTorch environment are available. Training uses the app-owned segmentation export; inspection needs a produced best.pt.",
                        "U-Net segmentation training and inspection are available");
                }

                string missing = !workerPresent
                    ? "bundled U-Net worker"
                    : !torchInstalled
                        ? "PyTorch package"
                        : "U-Net worker capability";
                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: false,
                    canTrain: false,
                    canInspect: false,
                    "U-Net runtime connection required",
                    $"U-Net training and inspection stay blocked until {missing} is available in the selected runtime.",
                    "Connect C:\\Git\\unet and install the U-Net PyTorch environment");
            }

            if (string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(engine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal))
            {
                string displayName = string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                    ? "YOLOv8"
                    : "YOLO11";
                bool hasAnyWorkerCapabilities = HasAnyCapability(supportedModels)
                    || HasAnyCapability(trainingModels)
                    || HasAnyCapability(detectionModels);
                bool hasOperationSpecificCapabilities = HasAnyCapability(trainingModels) || HasAnyCapability(detectionModels);
                bool canTrain = SupportsModel(adapterKey, trainingModels)
                    || (!hasOperationSpecificCapabilities && SupportsModel(adapterKey, supportedModels));
                bool canInspect = SupportsModel(adapterKey, detectionModels)
                    || (!hasOperationSpecificCapabilities && SupportsModel(adapterKey, supportedModels));
                if (canTrain || canInspect)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: true,
                        canTrain,
                        canInspect,
                        $"{displayName} worker \uC5F0\uACB0 \uD655\uC778",
                        $"{displayName} adapter capability\uAC00 worker\uC5D0\uC11C \uD655\uC778\uB410\uC2B5\uB2C8\uB2E4. \uD559\uC2B5:{FormatAllowed(canTrain)} / \uAC80\uC0AC:{FormatAllowed(canInspect)}",
                        canTrain && canInspect
                            ? $"{displayName} \uD559\uC2B5/\uAC80\uC0AC \uC2E4\uD589 \uAC00\uB2A5"
                            : $"{displayName} worker capability\uC5D0\uC11C \uBD80\uBD84 \uC9C0\uC6D0\uB9CC \uD655\uC778\uB428");
                }

                if (hasAnyWorkerCapabilities)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: false,
                        canTrain: false,
                        canInspect: false,
                        $"{displayName} worker capability mismatch",
                        $"Connected Ultralytics worker did not report adapter capability for {adapterKey}. Training and inspection stay blocked until the worker reports {adapterKey}.",
                        $"Update/select a runtime that reports {adapterKey}, or switch to a reported adapter.");
                }

                PythonModelRuntimeInstallPlan installPlan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
                bool hasLocalYoloV8Worker = string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                    && IsLocalYoloV8Worker(settings);
                if (hasLocalYoloV8Worker && installPlan.IsAlreadyInstalled)
                {
                    bool localWorkerCanTrain = LocalYoloV8WorkerSupportsTraining(settings);
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: true,
                        canTrain: localWorkerCanTrain,
                        canInspect: true,
                        localWorkerCanTrain ? "YOLOv8 local worker \uD559\uC2B5/\uAC80\uC0AC \uC900\uBE44" : "YOLOv8 local worker \uAC80\uC0AC \uC900\uBE44",
                        localWorkerCanTrain
                            ? "YOLOv8 local TCP worker\uB85C \uD559\uC2B5\uACFC \uD604\uC7AC \uAC80\uC0AC\uB97C \uC2E4\uD589\uD569\uB2C8\uB2E4. \uB85C\uCEEC segmentation weight\uB97C \uD655\uC778\uD558\uC138\uC694."
                            : "YOLOv8 local TCP worker\uB85C \uD604\uC7AC \uAC80\uC0AC\uB97C \uC2E4\uD589\uD569\uB2C8\uB2E4. \uD559\uC2B5\uC740 worker\uAC00 TrainYolo \uCC98\uB9AC\uB97C \uC9C0\uC6D0\uD560 \uB54C\uAE4C\uC9C0 \uCC28\uB2E8\uD569\uB2C8\uB2E4.",
                        localWorkerCanTrain ? "YOLOv8 \uD559\uC2B5/\uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5" : "YOLOv8 \uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5 / \uD559\uC2B5\uC740 local worker TrainYolo \uC9C0\uC6D0 \uD544\uC694");
                }

                if (hasLocalYoloV8Worker)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: false,
                        canTrain: false,
                        canInspect: false,
                        "YOLOv8 local worker \uC5F0\uACB0 / Ultralytics \uC124\uCE58 \uD544\uC694",
                        "YOLOv8 local TCP worker\uB294 \uD655\uC778\uB410\uC9C0\uB9CC, \uC120\uD0DD\uD55C venv\uC5D0 ultralytics \uD328\uD0A4\uC9C0\uAC00 \uD655\uC778\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.",
                        "YOLOv8 venv\uC5D0 ultralytics\uB97C \uC124\uCE58\uD558\uACE0 segmentation \uAC00\uC911\uCE58\uB97C \uC5F0\uACB0\uD558\uC138\uC694.");
                }

                bool hasBundledWorker = PythonModelRuntimeBundledWorkerService.IsUltralyticsWorkerScriptPath(settings?.ClientScriptPath);
                if (hasBundledWorker && installPlan.IsAlreadyInstalled && !hasAnyWorkerCapabilities)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: true,
                        canTrain: false,
                        canInspect: true,
                        $"{displayName} Ultralytics worker \uAC80\uC0AC \uC900\uBE44",
                        $"{displayName} \uBC88\uB4E4 Ultralytics worker\uAC00 \uC5F0\uACB0\uB418\uC5B4 \uD604\uC7AC \uAC80\uC0AC\uB294 \uC2E4\uD589\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uD559\uC2B5\uC740 \uC544\uC9C1 \uC774 worker\uC5D0 \uC5F0\uACB0\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.",
                        $"{displayName} \uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5 / \uD559\uC2B5\uC740 \uC9C0\uC6D0 \uB418\uB294 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694");
                }

                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: false,
                    canTrain: false,
                    canInspect: false,
                    "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / Ultralytics \uC2E4\uD589 \uC5F0\uACB0 \uD544\uC694",
                    $"{displayName} \uD328\uD0A4\uC9C0/\uACBD\uB85C \uC810\uAC80\uC740 \uAC00\uB2A5\uD558\uC9C0\uB9CC, \uD604\uC7AC \uD559\uC2B5/\uAC80\uC0AC \uC2E4\uD589\uC740 YOLOv5 worker\uB9CC \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4. {displayName}\uB85C \uC790\uB3D9 \uB300\uCCB4 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
                    $"{displayName} \uC2E4\uD589 \uC5F0\uACB0 \uC804\uC5D0\uB294 YOLOv5 \uD504\uB85C\uD544\uB85C \uD559\uC2B5/\uAC80\uC0AC\uD558\uC138\uC694.");
            }

            if (string.Equals(engine, PythonModelSettings.EngineOnnx, StringComparison.Ordinal))
            {
                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: false,
                    canTrain: false,
                    canInspect: false,
                    "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / ONNX \uCD94\uB860 \uC5F0\uACB0 \uD544\uC694",
                    "ONNX\uB294 \uCD94\uB860 \uC804\uC6A9 \uD504\uB85C\uD544\uC774\uBA70, \uD604\uC7AC \uAC80\uC0AC \uC2E4\uD589 \uC5F0\uACB0\uC740 \uC544\uC9C1 \uBD84\uB9AC \uAD6C\uD604\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.",
                    "ONNX \uCD94\uB860 \uC5F0\uACB0\uC774 \uC900\uBE44\uB418\uAE30 \uC804\uC5D0\uB294 YOLOv5 \uD504\uB85C\uD544\uB85C \uAC80\uC0AC\uD558\uC138\uC694.");
            }

            return new PythonModelRuntimeAdapterSupport(
                isExecutionSupported: false,
                canTrain: false,
                canInspect: false,
                "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / \uBAA8\uB378 \uC2E4\uD589 \uC5F0\uACB0 \uD544\uC694",
                "\uC120\uD0DD\uD55C \uBAA8\uB378 \uC2E4\uD589 \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.",
                "\uBAA8\uB378 \uC2E4\uD589 \uC5F0\uACB0 \uD544\uC694");
        }

        private static bool SupportsModel(string adapterKey, IEnumerable<string> values)
        {
            string normalizedAdapter = NormalizeModel(adapterKey);
            if (string.IsNullOrWhiteSpace(normalizedAdapter) || values == null)
            {
                return false;
            }

            return values.Any(value => string.Equals(NormalizeModel(value), normalizedAdapter, StringComparison.Ordinal));
        }

        private static bool HasAnyCapability(IEnumerable<string> values)
            => values != null && values.Any(value => !string.IsNullOrWhiteSpace(NormalizeModel(value)));

        private static string NormalizeModel(string value)
        {
            string lower = value?.Trim().ToLowerInvariant() ?? string.Empty;
            return lower switch
            {
                "yolo5" => "yolov5",
                "v5" => "yolov5",
                "yolo8" => "yolov8",
                "v8" => "yolov8",
                "yolov11" => "yolo11",
                "v11" => "yolo11",
                "onnxruntime" => "onnx",
                "u-net" => "unet",
                _ => lower
            };
        }

        private static string FormatAllowed(bool value)
            => value ? "\uAC00\uB2A5" : "\uC5F0\uACB0 \uD544\uC694";

        private static bool IsLocalYoloV8Worker(PythonModelSettings settings)
        {
            if (!TryReadLocalWorkerSource(settings, out string source))
            {
                return false;
            }

            return source.IndexOf("YOLOv8", StringComparison.OrdinalIgnoreCase) >= 0
                && source.IndexOf("DetectImage", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LocalYoloV8WorkerSupportsTraining(PythonModelSettings settings)
        {
            if (!TryReadLocalWorkerSource(settings, out string source))
            {
                return false;
            }

            return source.IndexOf("handle_train_yolo", StringComparison.OrdinalIgnoreCase) >= 0
                && source.IndexOf("TrainYoloResult", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryReadLocalWorkerSource(PythonModelSettings settings, out string source)
        {
            source = string.Empty;
            string clientScriptPath = settings?.ClientScriptPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(clientScriptPath)
                || !File.Exists(clientScriptPath)
                || PythonModelRuntimeBundledWorkerService.IsUltralyticsWorkerScriptPath(clientScriptPath))
            {
                return false;
            }

            string fileName = Path.GetFileName(clientScriptPath);
            if (!string.Equals(fileName, "labelling_tcp_client.py", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fileName, "labeling_tcp_client.py", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                source = File.ReadAllText(clientScriptPath);
                string siblingScriptPath = Path.Combine(Path.GetDirectoryName(clientScriptPath) ?? string.Empty, "labeling_tcp_client.py");
                if (File.Exists(siblingScriptPath)
                    && !string.Equals(Path.GetFullPath(siblingScriptPath), Path.GetFullPath(clientScriptPath), StringComparison.OrdinalIgnoreCase))
                {
                    source += Environment.NewLine + File.ReadAllText(siblingScriptPath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTorchInstalled(PythonModelSettings settings)
        {
            string pythonPath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (string.IsNullOrWhiteSpace(pythonPath) || !File.Exists(pythonPath))
            {
                return false;
            }

            string scriptsPath = Path.GetDirectoryName(pythonPath) ?? string.Empty;
            string venvRootPath = Directory.GetParent(scriptsPath)?.FullName ?? string.Empty;
            string sitePackagesPath = Path.Combine(venvRootPath, "Lib", "site-packages");
            return Directory.Exists(Path.Combine(sitePackagesPath, "torch"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "PIL"));
        }
    }
}
