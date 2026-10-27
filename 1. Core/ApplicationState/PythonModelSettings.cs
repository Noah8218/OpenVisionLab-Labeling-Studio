using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MvcVisionSystem
{
    public class PythonModelSettings
    {
        public const string EngineYoloV5 = "YOLOv5";
        public const string EngineYoloV8 = "YOLOv8";
        public const string EngineYolo11 = "YOLO11";
        public const string EngineUnet = "U-Net";
        public const string EnginePatchCore = "PatchCore";
        public const string EngineOnnx = "ONNX";

        public string PythonExecutablePath { get; set; } = "";

        public string ModelEngine { get; set; } = EngineYoloV5;

        public string ProjectRootPath { get; set; } = "";

        public string ClientScriptPath { get; set; } = "";

        public string WeightsPath { get; set; } = "";

        public string ImageRootPath { get; set; } = "";

        public float MinimumDetectionConfidence { get; set; } = 0.25F;

        public int MaximumDetectionCandidates { get; set; } = 20;

        public int InferenceImageSize { get; set; } = 320;

        public int DetectionTimeoutSeconds { get; set; } = 30;

        public bool AutoStartClient { get; set; } = true;

        public static string BuildCandidateSettingsIdentity(PythonModelSettings settings, string modelIdentity)
            => settings == null || string.IsNullOrWhiteSpace(modelIdentity) ? string.Empty
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modelIdentity + "|" + JsonSerializer.Serialize(settings))));

        /// <summary>
        /// Normalizes persisted values without inspecting the host environment or filesystem.
        /// Runtime and UI composition points apply host-specific paths before
        /// executing model work.
        /// </summary>
        public void EnsureDefaults()
        {
            ModelEngine = NormalizeModelEngine(ModelEngine);
            MinimumDetectionConfidence = Math.Clamp(MinimumDetectionConfidence, 0F, 1F);
            MaximumDetectionCandidates = Math.Clamp(MaximumDetectionCandidates, 1, 200);
            InferenceImageSize = Math.Clamp(InferenceImageSize, 64, 2048);
            DetectionTimeoutSeconds = Math.Clamp(DetectionTimeoutSeconds, 1, 600);
        }

        public static IReadOnlyList<string> GetSupportedModelEngines()
            => new[] { EngineYoloV5, EngineYoloV8, EngineYolo11, EngineUnet, EnginePatchCore, EngineOnnx };

        public static string NormalizeModelEngine(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (string.Equals(normalized, "yolov8", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "yolo8", StringComparison.OrdinalIgnoreCase))
            {
                return EngineYoloV8;
            }

            if (string.Equals(normalized, "yolo11", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "yolov11", StringComparison.OrdinalIgnoreCase))
            {
                return EngineYolo11;
            }

            if (string.Equals(normalized, "unet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "u-net", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "u net", StringComparison.OrdinalIgnoreCase))
            {
                return EngineUnet;
            }

            if (string.Equals(normalized, "patchcore", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "patch-core", StringComparison.OrdinalIgnoreCase))
            {
                return EnginePatchCore;
            }

            if (string.Equals(normalized, "onnx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onnxruntime", StringComparison.OrdinalIgnoreCase))
            {
                return EngineOnnx;
            }

            return EngineYoloV5;
        }

        public static string FormatModelEngineName(string value)
        {
            return NormalizeModelEngine(value) switch
            {
                EngineYoloV5 => "YOLOv5",
                EngineYoloV8 => "YOLOv8",
                EngineYolo11 => "YOLO11",
                EngineUnet => "U-Net",
                EnginePatchCore => "PatchCore",
                EngineOnnx => "ONNX",
                _ => "YOLO"
            };
        }

        public string GetProtocolModelName()
        {
            return NormalizeModelEngine(ModelEngine) switch
            {
                EngineYoloV8 => "yolov8",
                EngineYolo11 => "yolo11",
                EngineUnet => "unet",
                EnginePatchCore => "patchcore",
                EngineOnnx => "onnx",
                _ => "yolov5"
            };
        }

        public string GetModelRootPath()
        {
            string projectRootPath = ProjectRootPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return string.Empty;
            }

            return NormalizeModelEngine(ModelEngine) switch
            {
                EngineYoloV5 => Path.Combine(projectRootPath, "yolov5Master"),
                _ => projectRootPath
            };
        }

        public string GetRequirementsPath()
        {
            return string.IsNullOrWhiteSpace(ProjectRootPath)
                ? string.Empty
                : Path.Combine(ProjectRootPath, "requirements.txt");
        }
    }
}
