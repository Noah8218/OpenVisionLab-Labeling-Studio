using System;
using System.Collections.Generic;
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

            if (string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(engine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal))
            {
                string displayName = string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                    ? "YOLOv8"
                    : "YOLO11";
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

                PythonModelRuntimeInstallPlan installPlan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
                bool hasBundledWorker = PythonModelRuntimeBundledWorkerService.IsUltralyticsWorkerScriptPath(settings?.ClientScriptPath);
                if (hasBundledWorker && installPlan.IsAlreadyInstalled)
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
                _ => lower
            };
        }

        private static string FormatAllowed(bool value)
            => value ? "\uAC00\uB2A5" : "\uC5F0\uACB0 \uD544\uC694";
    }
}
