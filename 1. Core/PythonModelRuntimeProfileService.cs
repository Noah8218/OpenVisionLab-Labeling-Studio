using System;
using System.Collections.Generic;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeProfile
    {
        public PythonModelRuntimeProfile(
            string engine,
            string displayName,
            string runtimeFamilyText,
            string statusText,
            string capabilityText,
            string detailText,
            string nextActionText,
            string primaryActionText,
            bool isSelected,
            bool isRuntimeConnected,
            bool canTrain,
            bool canInspect)
        {
            Engine = engine ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RuntimeFamilyText = runtimeFamilyText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            CapabilityText = capabilityText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            PrimaryActionText = primaryActionText ?? string.Empty;
            IsSelected = isSelected;
            IsRuntimeConnected = isRuntimeConnected;
            CanTrain = canTrain;
            CanInspect = canInspect;
        }

        public string Engine { get; }
        public string DisplayName { get; }
        public string RuntimeFamilyText { get; }
        public string StatusText { get; }
        public string CapabilityText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
        public string PrimaryActionText { get; }
        public bool IsSelected { get; }
        public bool IsRuntimeConnected { get; }
        public bool CanTrain { get; }
        public bool CanInspect { get; }
    }

    public static class PythonModelRuntimeProfileService
    {
        public static IReadOnlyList<PythonModelRuntimeProfile> BuildProfiles(PythonModelSettings settings)
            => BuildProfiles(settings, null, null, null);

        public static IReadOnlyList<PythonModelRuntimeProfile> BuildProfiles(
            PythonModelSettings settings,
            IEnumerable<string> supportedModels,
            IEnumerable<string> trainingModels,
            IEnumerable<string> detectionModels)
        {
            settings ??= new PythonModelSettings();
            string selectedEngine = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine);
            PythonModelRuntimeState selectedState = PythonModelSettingsValidator.GetRuntimeState(
                settings,
                supportedModels,
                trainingModels,
                detectionModels);
            return new[]
            {
                BuildProfile(PythonModelSettings.EngineYoloV5, selectedEngine, selectedState),
                BuildProfile(PythonModelSettings.EngineYoloV8, selectedEngine, selectedState),
                BuildProfile(PythonModelSettings.EngineYolo11, selectedEngine, selectedState),
                BuildProfile(PythonModelSettings.EngineUnet, selectedEngine, selectedState),
                BuildProfile(PythonModelSettings.EngineOnnx, selectedEngine, selectedState)
            };
        }

        private static PythonModelRuntimeProfile BuildProfile(
            string engine,
            string selectedEngine,
            PythonModelRuntimeState selectedState)
        {
            bool isSelected = string.Equals(engine, selectedEngine, StringComparison.OrdinalIgnoreCase);
            if (isSelected)
            {
                return new PythonModelRuntimeProfile(
                    engine,
                    FormatDisplayName(engine),
                    FormatRuntimeFamily(engine),
                    FormatSelectedStatus(selectedState),
                    FormatCurrentCapability(selectedState?.CanRunTraining == true, selectedState?.CanRunInference == true),
                    selectedState?.DetailText ?? string.Empty,
                    selectedState?.NextActionText ?? string.Empty,
                    FormatSelectedActionText(selectedState),
                    isSelected: true,
                    isRuntimeConnected: selectedState?.IsRuntimeInstalled == true,
                    canTrain: selectedState?.CanRunTraining == true,
                    canInspect: selectedState?.CanRunInference == true);
            }

            return engine switch
            {
                PythonModelSettings.EngineYoloV8 => BuildDisconnectedProfile(
                    engine,
                    "\uC124\uCE58/\uC5F0\uACB0 \uB300\uAE30",
                    "YOLOv5\uCC98\uB7FC local YOLOv8 worker \uD3F4\uB354\uB97C \uC5F0\uACB0\uD558\uB294 \uC2E4\uD589 \uD504\uB85C\uD544\uC785\uB2C8\uB2E4.",
                    "YOLOv8 local worker \uD3F4\uB354 \uC5F0\uACB0",
                    "\uC5F0\uACB0"),
                PythonModelSettings.EngineYolo11 => BuildDisconnectedProfile(
                    engine,
                    "\uC124\uCE58/\uC5F0\uACB0 \uB300\uAE30",
                    "Ultralytics \uD328\uD0A4\uC9C0\uB97C \uC0AC\uC6A9\uD558\uB294 YOLO11 \uC2E4\uD589 \uD504\uB85C\uD544\uC785\uB2C8\uB2E4.",
                    "Ultralytics \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 \uACBD\uB85C \uC5F0\uACB0",
                    "\uC5F0\uACB0"),
                PythonModelSettings.EngineUnet => BuildDisconnectedProfile(
                    engine,
                    "U-Net \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uB300\uAE30",
                    "\uC571\uC774 \uB0B4\uBCB4\ub0B4\ub294 segmentation mask export\ub97c \uc0ac\uc6a9\ud558\ub294 PyTorch U-Net \uc2e4\ud589 \ud504\ub85c\ud544\uc785\ub2c8\ub2e4.",
                    "C:\\Git\\unet \uc2e4\ud589\uae30 \uc5f0\uacb0",
                    "\uC5F0\uACB0"),
                PythonModelSettings.EngineOnnx => BuildDisconnectedProfile(
                    engine,
                    "\uCD94\uB860 \uC5F0\uACB0 \uB300\uAE30",
                    "ONNX \uBAA8\uB378\uC744 \uAC80\uC0AC \uC804\uC6A9\uC73C\uB85C \uC5F0\uACB0\uD558\uB294 \uD504\uB85C\uD544\uC785\uB2C8\uB2E4.",
                    "ONNX \uCD94\uB860 adapter \uC5F0\uACB0"),
                _ => BuildDisconnectedProfile(
                    engine,
                    "YOLOv5 \uD3F4\uB354 \uC5F0\uACB0 \uB300\uAE30",
                    "YOLOv5 repo \uB610\uB294 \uAE30\uC874 \uC2E4\uD589 \uD3F4\uB354\uB97C \uC5F0\uACB0\uD558\uB294 \uD504\uB85C\uD544\uC785\uB2C8\uB2E4.",
                    "YOLOv5 \uD3F4\uB354 \uC5F0\uACB0",
                    "\uC5F0\uACB0")
            };
        }

        private static PythonModelRuntimeProfile BuildDisconnectedProfile(
            string engine,
            string statusText,
            string detailText,
            string nextActionText,
            string primaryActionText = "\uC120\uD0DD")
            => new PythonModelRuntimeProfile(
                engine,
                FormatDisplayName(engine),
                FormatRuntimeFamily(engine),
                statusText,
                FormatProfileCapability(engine),
                detailText,
                nextActionText,
                primaryActionText,
                isSelected: false,
                isRuntimeConnected: false,
                canTrain: false,
                canInspect: false);

        private static string FormatSelectedStatus(PythonModelRuntimeState state)
        {
            if (state == null)
            {
                return "\uC120\uD0DD\uB428 / \uC0C1\uD0DC \uD655\uC778 \uD544\uC694";
            }

            return state.State switch
            {
                PythonModelRuntimeStateKind.Ready => "\uC120\uD0DD\uB428 / \uD559\uC2B5\u00B7\uAC80\uC0AC \uAC00\uB2A5",
                PythonModelRuntimeStateKind.Incomplete => FormatSelectedIncompleteStatus(state),
                _ => "\uC120\uD0DD\uB428 / \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58"
            };
        }

        private static string FormatSelectedActionText(PythonModelRuntimeState state)
            => state?.State == PythonModelRuntimeStateKind.Ready
                || state?.CanRunTraining == true
                || state?.CanRunInference == true
                ? "\uD655\uC778"
                : "\uC5F0\uACB0";

        private static string FormatSelectedIncompleteStatus(PythonModelRuntimeState state)
        {
            if (state?.CanRunInference == true && state.CanRunTraining != true)
            {
                return "\uC120\uD0DD\uB428 / \uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5\u00B7\uD559\uC2B5 \uBBF8\uC9C0\uC6D0";
            }

            if (state?.CanRunTraining == true)
            {
                return "\uC120\uD0DD\uB428 / \uD559\uC2B5 \uAC00\uB2A5\u00B7\uAC80\uC0AC \uBAA8\uB378 \uD544\uC694";
            }

            return "\uC120\uD0DD\uB428 / \uC124\uC815 \uD655\uC778 \uD544\uC694";
        }

        private static string FormatCurrentCapability(bool canTrain, bool canInspect)
        {
            if (canTrain && canInspect)
            {
                return "\uC9C0\uC6D0 \uBC94\uC704: \uD559\uC2B5 + \uD604\uC7AC \uAC80\uC0AC";
            }

            if (canInspect)
            {
                return "\uC9C0\uC6D0 \uBC94\uC704: \uD604\uC7AC \uAC80\uC0AC / \uD559\uC2B5 \uBBF8\uC9C0\uC6D0";
            }

            if (canTrain)
            {
                return "\uC9C0\uC6D0 \uBC94\uC704: \uD559\uC2B5 / \uAC80\uC0AC \uBAA8\uB378 \uD544\uC694";
            }

            return "\uC9C0\uC6D0 \uBC94\uC704: \uC5F0\uACB0 \uD6C4 \uD655\uC778";
        }

        private static string FormatProfileCapability(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "\uC9C0\uC6D0 \uBC94\uC704: local worker \uD604\uC7AC \uAC80\uC0AC / \uD559\uC2B5\uC740 TrainYolo \uC9C0\uC6D0 \uD544\uC694",
                PythonModelSettings.EngineYolo11 => "\uC9C0\uC6D0 \uBC94\uC704: \uD604\uC7AC \uAC80\uC0AC \uC6B0\uC120 / \uD559\uC2B5\uC740 worker \uC5F0\uACB0 \uD544\uC694",
                PythonModelSettings.EngineUnet => "\uC9C0\uC6D0 \uBC94\uC704: segmentation \uD559\uC2B5 + \uD604\uC7AC \uAC80\uC0AC",
                PythonModelSettings.EngineOnnx => "\uC9C0\uC6D0 \uBC94\uC704: \uCD94\uB860 \uC804\uC6A9",
                _ => "\uC9C0\uC6D0 \uBC94\uC704: \uD559\uC2B5 + \uD604\uC7AC \uAC80\uC0AC"
            };

        private static string FormatDisplayName(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8",
                PythonModelSettings.EngineYolo11 => "YOLO11",
                PythonModelSettings.EngineUnet => "U-Net",
                PythonModelSettings.EngineOnnx => "ONNX",
                _ => "YOLOv5"
            };

        private static string FormatRuntimeFamily(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "Ultralytics",
                PythonModelSettings.EngineYolo11 => "Ultralytics",
                PythonModelSettings.EngineUnet => "PyTorch U-Net",
                PythonModelSettings.EngineOnnx => "ONNX Runtime",
                _ => "YOLOv5 repo"
            };
    }
}
