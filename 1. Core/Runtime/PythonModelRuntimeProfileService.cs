using System;
using System.Collections.Generic;

namespace MvcVisionSystem._1._Core
{
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
                BuildProfile(PythonModelSettings.EnginePatchCore, selectedEngine, selectedState),
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
                    "YOLOv5\uc640 \uac19\uc774 \ub85c\uceec YOLOv8 \uc2e4\ud589 \ud658\uacbd \ud3f4\ub354\ub97c \uc5f0\uacb0\ud569\ub2c8\ub2e4.",
                    "YOLOv8 \uc2e4\ud589 \ud658\uacbd \ud3f4\ub354 \uc5f0\uacb0",
                    "\uC5F0\uACB0"),
                PythonModelSettings.EngineYolo11 => BuildDisconnectedProfile(
                    engine,
                    "\uC124\uCE58/\uC5F0\uACB0 \uB300\uAE30",
                    "Ultralytics \ud328\ud0a4\uc9c0\ub97c \uc0ac\uc6a9\ud558\ub294 YOLO11 \uc2e4\ud589 \ud658\uacbd\uc785\ub2c8\ub2e4.",
                    "Ultralytics \uc2e4\ud589 \ud658\uacbd \uc124\uce58\u00b7\uacbd\ub85c \uc5f0\uacb0",
                    "\uC5F0\uACB0"),
                PythonModelSettings.EngineUnet => BuildDisconnectedProfile(
                    engine,
                    "U-Net \uc2e4\ud589 \ud658\uacbd \uc5f0\uacb0 \ub300\uae30",
                    "\uc571\uc5d0\uc11c \ub0b4\ubcf4\ub0b8 \uc138\uadf8\uba58\ud14c\uc774\uc158 \ub9c8\uc2a4\ud06c\ub97c \uc0ac\uc6a9\ud558\ub294 PyTorch U-Net \uc2e4\ud589 \ud658\uacbd\uc785\ub2c8\ub2e4.",
                    "C:\\Git\\unet \uc2e4\ud589 \ud658\uacbd \uc5f0\uacb0",
                    "\uC5F0\uACB0"),
                PythonModelSettings.EnginePatchCore => BuildDisconnectedProfile(
                    engine,
                    "PatchCore 실행 환경 연결 대기",
                    "정상 이미지만 학습하는 이상탐지 모델입니다. 이미지 판정과 확인용 위치 후보를 함께 보여줍니다.",
                    "PyTorch 실행 환경·번들 PatchCore 프로그램 연결",
                    "연결"),
                PythonModelSettings.EngineOnnx => BuildDisconnectedProfile(
                    engine,
                    "\uCD94\uB860 \uC5F0\uACB0 \uB300\uAE30",
                    "ONNX \ubaa8\ub378\uc744 \ucd94\ub860 \uc804\uc6a9\uc73c\ub85c \uc5f0\uacb0\ud569\ub2c8\ub2e4.",
                    "ONNX \ucd94\ub860 \ud658\uacbd \uc5f0\uacb0"),
                _ => BuildDisconnectedProfile(
                    engine,
                    "YOLOv5 \uD3F4\uB354 \uC5F0\uACB0 \uB300\uAE30",
                    "YOLOv5 \uc800\uc7a5\uc18c \ub610\ub294 \uae30\uc874 \uc2e4\ud589 \ud658\uacbd \ud3f4\ub354\ub97c \uc5f0\uacb0\ud569\ub2c8\ub2e4.",
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
                PythonModelRuntimeStateKind.Ready => "\uc120\ud0dd\ub428 / \ud559\uc2b5\u00b7\ucd94\ub860 \uac00\ub2a5",
                PythonModelRuntimeStateKind.Incomplete => FormatSelectedIncompleteStatus(state),
                _ => "\uc120\ud0dd\ub428 / \uc2e4\ud589 \ud658\uacbd \ubbf8\uc124\uce58"
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
                return "\uc120\ud0dd\ub428 / \ud604\uc7ac \ucd94\ub860 \uac00\ub2a5\u00b7\ud559\uc2b5 \ubbf8\uc9c0\uc6d0";
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
                return "\uc9c0\uc6d0 \ubc94\uc704: \ud559\uc2b5\u00b7\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860";
            }

            if (canInspect)
            {
                return "\uc9c0\uc6d0 \ubc94\uc704: \ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860 / \ud559\uc2b5 \ubbf8\uc9c0\uc6d0";
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
                PythonModelSettings.EngineYoloV8 => "\uc9c0\uc6d0 \ubc94\uc704: \ub85c\uceec \ud658\uacbd\uc758 \ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860 / \ud559\uc2b5\uc740 TrainYolo \uc9c0\uc6d0 \ud544\uc694",
                PythonModelSettings.EngineYolo11 => "\uc9c0\uc6d0 \ubc94\uc704: \ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860 / \ud559\uc2b5\uc740 \uc2e4\ud589 \ud658\uacbd \uc5f0\uacb0 \ud544\uc694",
                PythonModelSettings.EngineUnet => "\uc9c0\uc6d0 \ubc94\uc704: \uc138\uadf8\uba58\ud14c\uc774\uc158 \ud559\uc2b5\u00b7\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860",
                PythonModelSettings.EnginePatchCore => "지원 범위: 정상 이미지 학습·이미지 판정·위치 후보",
                PythonModelSettings.EngineOnnx => "\uC9C0\uC6D0 \uBC94\uC704: \uCD94\uB860 \uC804\uC6A9",
                _ => "\uc9c0\uc6d0 \ubc94\uc704: \ud559\uc2b5\u00b7\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860"
            };

        private static string FormatDisplayName(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8",
                PythonModelSettings.EngineYolo11 => "YOLO11",
                PythonModelSettings.EngineUnet => "U-Net",
                PythonModelSettings.EnginePatchCore => "PatchCore",
                PythonModelSettings.EngineOnnx => "ONNX",
                _ => "YOLOv5"
            };

        private static string FormatRuntimeFamily(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "Ultralytics",
                PythonModelSettings.EngineYolo11 => "Ultralytics",
                PythonModelSettings.EngineUnet => "PyTorch U-Net",
                PythonModelSettings.EnginePatchCore => "PyTorch PatchCore",
                PythonModelSettings.EngineOnnx => "ONNX Runtime",
                _ => "YOLOv5 repo"
            };
    }
}
