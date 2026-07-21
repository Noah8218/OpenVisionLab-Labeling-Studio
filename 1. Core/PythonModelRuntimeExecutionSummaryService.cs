using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeExecutionSummary
    {
        public PythonModelRuntimeExecutionSummary(
            string titleText,
            string summaryText,
            string workerText,
            string trainingText,
            string inspectionText)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            WorkerText = workerText ?? string.Empty;
            TrainingText = trainingText ?? string.Empty;
            InspectionText = inspectionText ?? string.Empty;
        }

        public string TitleText { get; }

        public string SummaryText { get; }

        public string WorkerText { get; }

        public string TrainingText { get; }

        public string InspectionText { get; }
    }

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
                "\uC2E4\uC81C \uD559\uC2B5/\uAC80\uC0AC \uC2E4\uD589 \uACBD\uB85C",
                $"{engineText} / adapter={adapterKey} / {FormatRuntimeState(runtimeState)}",
                $"Worker: {FormatPathLeaf(pythonExecutablePath)} -> {FormatPathLeaf(clientScriptPath)} / \uC791\uC5C5 \uD3F4\uB354 {FormatPathLeaf(projectRootPath)}",
                BuildTrainingText(engine, adapterKey, modelRootPath, adapterSupport),
                BuildInspectionText(adapterKey, weightsPath, imageRootPath, adapterSupport));
        }

        private static string BuildTrainingText(string engine, string adapterKey, string modelRootPath, PythonModelRuntimeAdapterSupport adapterSupport)
        {
            if (string.Equals(PythonModelSettings.NormalizeModelEngine(engine), PythonModelSettings.EngineOnnx, StringComparison.Ordinal))
            {
                return "\uD559\uC2B5: ONNX\uB294 \uCD94\uB860 \uC804\uC6A9 \uD504\uB85C\uD544 / \uD559\uC2B5 \uC694\uCCAD \uC5C6\uC74C";
            }

            string model = string.IsNullOrWhiteSpace(adapterKey) ? "yolov5" : adapterKey;
            if (adapterSupport?.CanTrain != true)
            {
                if (adapterSupport?.CanInspect == true)
                {
                    return $"\uD559\uC2B5: \uBBF8\uC9C0\uC6D0 / \uD604\uC7AC \uC5F0\uACB0\uB41C worker\uB294 \uAC80\uC0AC\uB9CC \uC9C0\uC6D0 - TCP StartTraining(model={model}) \uC694\uCCAD\uD558\uC9C0 \uC54A\uC74C";
                }

                return $"\uD559\uC2B5: \uC2E4\uD589 \uCC28\uB2E8 / TCP StartTraining(model={model}) \uC5F0\uACB0 \uD544\uC694 - YOLOv5 worker\uB85C \uB300\uCCB4 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC74C";
            }

            if (string.Equals(PythonModelSettings.NormalizeModelEngine(engine), PythonModelSettings.EngineUnet, StringComparison.Ordinal))
            {
                return $"\uD559\uC2B5: TCP StartTraining(model={model}) -> app-owned U-Net segmentation export / \uBAA8\uB378 \uB8E8\uD2B8 {FormatPathLeaf(modelRootPath)}";
            }

            return $"\uD559\uC2B5: TCP StartTraining(model={model}) -> \uBAA8\uB378 \uB8E8\uD2B8 {FormatPathLeaf(modelRootPath)} / data.yaml + \uD559\uC2B5 \uC124\uC815";
        }

        private static string BuildInspectionText(string adapterKey, string weightsPath, string imageRootPath, PythonModelRuntimeAdapterSupport adapterSupport)
        {
            string model = string.IsNullOrWhiteSpace(adapterKey) ? "yolov5" : adapterKey;
            if (adapterSupport?.CanInspect != true)
            {
                return $"\uD604\uC7AC \uAC80\uC0AC: \uC2E4\uD589 \uCC28\uB2E8 / TCP DetectImage(model={model}) \uC5F0\uACB0 \uD544\uC694 - YOLOv5 worker\uB85C \uB300\uCCB4 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC74C";
            }

            return $"\uD604\uC7AC \uAC80\uC0AC: TCP DetectImage(model={model}) -> \uAC00\uC911\uCE58 {FormatPathLeaf(weightsPath)} / \uC774\uBBF8\uC9C0 {FormatPathLeaf(imageRootPath)}";
        }

        private static string FormatEngineName(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8 Ultralytics",
                PythonModelSettings.EngineYolo11 => "YOLO11 Ultralytics",
                PythonModelSettings.EngineUnet => "PyTorch U-Net",
                PythonModelSettings.EngineOnnx => "ONNX Runtime",
                _ => "YOLOv5 repo"
            };

        private static string FormatRuntimeState(PythonModelRuntimeState state)
        {
            if (state == null)
            {
                return "\uC0C1\uD0DC \uD655\uC778 \uD544\uC694";
            }

            return state.State switch
            {
                PythonModelRuntimeStateKind.Ready => "\uD559\uC2B5/\uAC80\uC0AC \uAC00\uB2A5",
                PythonModelRuntimeStateKind.Incomplete => FormatIncompleteRuntimeState(state),
                _ => "\uC124\uCE58/\uC5F0\uACB0 \uD544\uC694"
            };
        }

        private static string FormatIncompleteRuntimeState(PythonModelRuntimeState state)
        {
            if (state?.CanRunInference == true && state.CanRunTraining != true)
            {
                return "\uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5 / \uD559\uC2B5 \uBBF8\uC9C0\uC6D0";
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
