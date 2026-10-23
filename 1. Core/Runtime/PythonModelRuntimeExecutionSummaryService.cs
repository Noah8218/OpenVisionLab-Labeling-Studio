using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem._1._Core
{
    public static class PythonModelRuntimeExecutionSummaryService
    {
        public static PythonModelRuntimeExecutionSummary Build(PythonModelSettings settings)
            => Build(settings, null, null, null);

        public static PythonModelRuntimeExecutionSummary Build(
            PythonModelSettings settings,
            IEnumerable<string> supportedModels,
            IEnumerable<string> trainingModels,
            IEnumerable<string> detectionModels)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(settings);
            string engine = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine);
            string engineText = FormatEngineName(engine);
            string adapterKey = settings.GetProtocolModelName();
            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string clientScriptPath = settings.ClientScriptPath?.Trim() ?? string.Empty;
            string projectRootPath = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string modelRootPath = settings.GetModelRootPath();
            string weightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            string imageRootPath = settings.ImageRootPath?.Trim() ?? string.Empty;
            PythonModelRuntimeState runtimeState = PythonModelSettingsValidator.GetRuntimeState(
                settings,
                supportedModels,
                trainingModels,
                detectionModels);
            PythonModelRuntimeAdapterSupport adapterSupport = PythonModelRuntimeAdapterSupportService.Build(
                settings,
                supportedModels,
                trainingModels,
                detectionModels);

            return new PythonModelRuntimeExecutionSummary(
                "\ud559\uc2b5\u00b7\ucd94\ub860 \uc2e4\ud589 \uacbd\ub85c",
                $"{engineText} / adapter={adapterKey} / {FormatRuntimeState(runtimeState)}",
                $"\uc2e4\ud589 \uacbd\ub85c: {FormatPathLeaf(pythonExecutablePath)} -> {FormatPathLeaf(clientScriptPath)} / \uc791\uc5c5 \ud3f4\ub354 {FormatPathLeaf(projectRootPath)}",
                BuildTrainingText(engine, adapterKey, modelRootPath, adapterSupport),
                BuildInspectionText(adapterKey, weightsPath, imageRootPath, adapterSupport));
        }

        private static string BuildTrainingText(string engine, string adapterKey, string modelRootPath, PythonModelRuntimeAdapterSupport adapterSupport)
        {
            if (string.Equals(PythonModelSettings.NormalizeModelEngine(engine), PythonModelSettings.EngineOnnx, StringComparison.Ordinal))
            {
                return "\ud559\uc2b5: ONNX\ub294 \ucd94\ub860 \uc804\uc6a9 / \ud559\uc2b5 \uc694\uccad \uc5c6\uc74c";
            }

            string model = string.IsNullOrWhiteSpace(adapterKey) ? "yolov5" : adapterKey;
            if (adapterSupport?.CanTrain != true)
            {
                if (adapterSupport?.CanInspect == true)
                {
                    return $"\ud559\uc2b5: \ubbf8\uc9c0\uc6d0 / \uc5f0\uacb0\ud55c \uc2e4\ud589 \ud658\uacbd\uc740 \ucd94\ub860\ub9cc \uc9c0\uc6d0\ud558\uba70 TCP StartTraining(model={model})\ub97c \uc694\uccad\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.";
                }

                return $"\ud559\uc2b5: \uc2e4\ud589 \ucc28\ub2e8 / TCP StartTraining(model={model}) \uc5f0\uacb0 \ud544\uc694 - YOLOv5 \ud658\uacbd\uc73c\ub85c \ub300\uccb4 \uc2e4\ud589\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.";
            }

            if (string.Equals(PythonModelSettings.NormalizeModelEngine(engine), PythonModelSettings.EngineUnet, StringComparison.Ordinal))
            {
                return $"\ud559\uc2b5: TCP StartTraining(model={model}) -> \uc571\uc5d0\uc11c \uc900\ube44\ud55c U-Net \uc138\uadf8\uba58\ud14c\uc774\uc158 \ub9c8\uc2a4\ud06c / \ubaa8\ub378 \ub8e8\ud2b8 {FormatPathLeaf(modelRootPath)}";
            }

            if (string.Equals(PythonModelSettings.NormalizeModelEngine(engine), PythonModelSettings.EnginePatchCore, StringComparison.Ordinal))
            {
                return $"학습: TCP StartTraining(model={model}, task=anomaly) -> 정상으로 판정한 train/val 이미지 / 메모리 뱅크 {FormatPathLeaf(modelRootPath)}";
            }

            return $"\uD559\uC2B5: TCP StartTraining(model={model}) -> \uBAA8\uB378 \uB8E8\uD2B8 {FormatPathLeaf(modelRootPath)} / data.yaml + \uD559\uC2B5 \uC124\uC815";
        }

        private static string BuildInspectionText(string adapterKey, string weightsPath, string imageRootPath, PythonModelRuntimeAdapterSupport adapterSupport)
        {
            string model = string.IsNullOrWhiteSpace(adapterKey) ? "yolov5" : adapterKey;
            if (adapterSupport?.CanInspect != true)
            {
                return $"\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860: \uc2e4\ud589 \ucc28\ub2e8 / TCP DetectImage(model={model}) \uc5f0\uacb0 \ud544\uc694 - YOLOv5 \ud658\uacbd\uc73c\ub85c \ub300\uccb4 \uc2e4\ud589\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.";
            }

            return $"\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860: TCP DetectImage(model={model}) -> \ubaa8\ub378 \ud30c\uc77c {FormatPathLeaf(weightsPath)} / \uc774\ubbf8\uc9c0 {FormatPathLeaf(imageRootPath)}";
        }

        private static string FormatEngineName(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8 Ultralytics",
                PythonModelSettings.EngineYolo11 => "YOLO11 Ultralytics",
                PythonModelSettings.EngineUnet => "PyTorch U-Net",
                PythonModelSettings.EnginePatchCore => "PyTorch PatchCore",
                PythonModelSettings.EngineOnnx => "ONNX Runtime",
                _ => "YOLOv5 repo"
            };

        private static string FormatRuntimeState(PythonModelRuntimeState state)
        {
            if (state == null)
            {
                return "\uc2e4\ud589 \ud658\uacbd \ud655\uc778 \ud544\uc694";
            }

            return state.State switch
            {
                PythonModelRuntimeStateKind.Ready => "\ud559\uc2b5\u00b7\ucd94\ub860 \uac00\ub2a5",
                PythonModelRuntimeStateKind.Incomplete => FormatIncompleteRuntimeState(state),
                _ => "\uc124\uce58\u00b7\uc5f0\uacb0 \ud544\uc694"
            };
        }

        private static string FormatIncompleteRuntimeState(PythonModelRuntimeState state)
        {
            if (state?.CanRunInference == true && state.CanRunTraining != true)
            {
                return "\ucd94\ub860 \uac00\ub2a5 / \ud559\uc2b5 \ubbf8\uc9c0\uc6d0";
            }

            if (state?.CanRunTraining == true)
            {
                return "\uD559\uC2B5 \uAC00\uB2A5 / \uAC80\uC0AC \uBAA8\uB378 \uD544\uC694";
            }

            return "\uC124\uC815 \uD655\uC778 \uD544\uC694";
        }

        private static string FormatPathLeaf(string path)
        {
            string trimmed = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "\uBBF8\uC124\uC815";
            }

            string normalized = trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaf = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(leaf) ? normalized : leaf;
        }
    }
}
