using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
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
                    "YOLOv5 실행 환경 연결됨",
                    "YOLOv5 TCP \uC5F0\uACB0\uB85C \uD559\uC2B5\uACFC \uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860\uC744 \uC2E4\uD589\uD569\uB2C8\uB2E4.",
                    "YOLOv5 학습·추론 가능");
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

            if (string.Equals(engine, PythonModelSettings.EnginePatchCore, StringComparison.Ordinal))
            {
                bool workerPresent = PythonModelRuntimeBundledWorkerService.IsPatchCoreWorkerScriptPath(settings?.ClientScriptPath);
                bool runtimeInstalled = IsPatchCoreRuntimeInstalled(settings);
                bool capabilityMismatch = HasAnyCapability(supportedModels)
                    && !SupportsModel(adapterKey, supportedModels);
                if (workerPresent && runtimeInstalled && !capabilityMismatch)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: true,
                        canTrain: true,
                        canInspect: true,
                        "PatchCore worker connection ready",
                        "번들 PatchCore 프로그램과 torch/torchvision이 준비되었습니다. 학습은 정상으로 판정한 이미지만 사용합니다. 추론 결과는 후보이며 라벨 저장은 별도입니다.",
                        "PatchCore 정상 이미지 학습·현재 추론 가능");
                }

                string missing = !workerPresent
                    ? "bundled PatchCore worker"
                    : !runtimeInstalled
                        ? "torch/torchvision/numpy/Pillow runtime"
                        : "PatchCore worker capability";
                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: false,
                    canTrain: false,
                    canInspect: false,
                    "PatchCore runtime connection required",
                    $"PatchCore 학습·추론은 {missing} 확인 전까지 차단됩니다.",
                    "PatchCore PyTorch 실행 환경과 번들 프로그램을 연결하세요.");
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
                        $"{displayName} 실행 환경 연결 확인됨",
                        $"{displayName} \uc2e4\ud589 \ud658\uacbd \uc9c0\uc6d0\uc774 \ud655\uc778\ub410\uc2b5\ub2c8\ub2e4. \ud559\uc2b5: {FormatAllowed(canTrain)} / \ucd94\ub860: {FormatAllowed(canInspect)}",
                        canTrain && canInspect
                            ? $"{displayName} 학습·추론 가능"
                            : $"{displayName} 실행 환경의 일부 기능만 지원이 확인됐습니다.");
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
                        localWorkerCanTrain ? "YOLOv8 학습·추론 준비됨" : "YOLOv8 추론 준비됨",
                        localWorkerCanTrain
                            ? "\uB85C\uCEEC YOLOv8 TCP \uC5F0\uACB0\uB85C \uD559\uC2B5\uACFC \uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860\uC744 \uC2E4\uD589\uD569\uB2C8\uB2E4. \uC138\uADF8\uBA58\uD14C\uC774\uC158 \uBAA8\uB378 \uD30C\uC77C\uB3C4 \uD655\uC778\uD558\uC138\uC694."
                            : "\uB85C\uCEEC YOLOv8 TCP \uC5F0\uACB0\uB85C \uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860\uC744 \uC2E4\uD589\uD569\uB2C8\uB2E4. \uD559\uC2B5\uC740 \uC2E4\uD589 \uD504\uB85C\uADF8\uB7A8\uC758 TrainYolo \uC9C0\uC6D0\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.",
                        localWorkerCanTrain ? "YOLOv8 학습·현재 추론 가능" : "YOLOv8 현재 추론 가능 / 학습은 TrainYolo 지원 필요");
                }

                if (hasLocalYoloV8Worker)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: false,
                        canTrain: false,
                        canInspect: false,
                        "YOLOv8 실행 환경 연결됨 / Ultralytics 설치 필요",
                        "YOLOv8 \uC2E4\uD589 \uD504\uB85C\uADF8\uB7A8\uC774 \uC788\uC9C0\uB9CC \uC120\uD0DD\uD55C venv\uC5D0\uC11C ultralytics \uD328\uD0A4\uC9C0\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.",
                        "YOLOv8 venv\uC5D0 ultralytics\uB97C \uC124\uCE58\uD558\uACE0 segmentation \uAC00\uC911\uCE58\uB97C \uC5F0\uACB0\uD558\uC138\uC694.");
                }

                bool hasBundledWorker = PythonModelRuntimeBundledWorkerService.IsUltralyticsWorkerScriptPath(settings?.ClientScriptPath);
                if (hasBundledWorker && installPlan.IsAlreadyInstalled && !hasAnyWorkerCapabilities)
                {
                    return new PythonModelRuntimeAdapterSupport(
                        isExecutionSupported: true,
                        canTrain: false,
                        canInspect: true,
                        $"{displayName} Ultralytics 추론 준비됨",
                        $"{displayName} \uBC88\uB4E4 Ultralytics \uC2E4\uD589 \uD504\uB85C\uADF8\uB7A8\uC73C\uB85C \uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860\uC744 \uC2E4\uD589\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uC774 \uD504\uB85C\uADF8\uB7A8\uC5D0\uC11C\uB294 \uD604\uC7AC \uD559\uC2B5\uC744 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
                        $"{displayName} 현재 추론 가능 / 학습 지원 실행 환경 연결 필요");
                }

                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: false,
                    canTrain: false,
                    canInspect: false,
                    "라벨링 가능 / Ultralytics 실행 환경 연결 필요",
                    $"{displayName} 패키지·경로를 점검할 수 있습니다. 학습·추론은 현재 YOLOv5 환경을 쓰며 {displayName}로 자동 대체하지 않습니다.",
                    $"{displayName} 연결 전에는 YOLOv5 실행 환경으로 학습·추론하세요.");
            }

            if (string.Equals(engine, PythonModelSettings.EngineOnnx, StringComparison.Ordinal))
            {
                return new PythonModelRuntimeAdapterSupport(
                    isExecutionSupported: false,
                    canTrain: false,
                    canInspect: false,
                    "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / ONNX \uCD94\uB860 \uC5F0\uACB0 \uD544\uC694",
                    "ONNX\uB294 \uCD94\uB860 \uC804\uC6A9\uC785\uB2C8\uB2E4. \uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860\uC744 \uC704\uD55C \uBCC4\uB3C4 \uC2E4\uD589 \uD658\uACBD \uC5F0\uACB0\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.",
                    "ONNX 추론 연결 전에는 YOLOv5 실행 환경으로 추론하세요.");
            }

            return new PythonModelRuntimeAdapterSupport(
                isExecutionSupported: false,
                canTrain: false,
                canInspect: false,
                "라벨링 가능 / 실행 환경 연결 필요",
                "\uC120\uD0DD\uD55C \uC2E4\uD589 \uD658\uACBD \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.",
                "실행 환경 연결 필요");
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

        private static bool IsPatchCoreRuntimeInstalled(PythonModelSettings settings)
        {
            string pythonPath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (string.IsNullOrWhiteSpace(pythonPath) || !File.Exists(pythonPath))
            {
                return false;
            }

            string pythonDirectory = Path.GetDirectoryName(pythonPath) ?? string.Empty;
            string sitePackagesPath = string.Equals(Path.GetFileName(pythonDirectory), "Scripts", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Directory.GetParent(pythonDirectory)?.FullName ?? string.Empty, "Lib", "site-packages")
                : Path.Combine(pythonDirectory, "Lib", "site-packages");
            return Directory.Exists(Path.Combine(sitePackagesPath, "torch"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "torchvision"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "numpy"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "PIL"));
        }
    }
}
