using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem._3._Communication.TCP
{
    public enum PythonModelStatusParseStatus
    {
        NotStatus,
        InvalidPayload,
        Parsed
    }

    public sealed class PythonModelStatusMessage
    {
        public string Type { get; set; } = "";
        public int Version { get; set; } = 1;
        public string State { get; set; } = "";
        public string Message { get; set; } = "";
        public int? ProgressPercent { get; set; }
        public int? Epoch { get; set; }
        public int? TotalEpochs { get; set; }
        public string Error { get; set; } = "";
        public string FailureReason { get; set; } = "";
        public int? ExitCode { get; set; }
        public List<string> LogTail { get; set; } = new List<string>();
        public List<string> SupportedModels { get; set; } = new List<string>();
        public List<string> TrainingModels { get; set; } = new List<string>();
        public List<string> DetectionModels { get; set; } = new List<string>();
        public bool? Ok { get; set; }
        public bool? Loaded { get; set; }

        public bool IsError => string.Equals(State, "failed", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Error);
    }

    public sealed class PythonModelStatusParseResult
    {
        private PythonModelStatusParseResult(PythonModelStatusParseStatus status, PythonModelStatusMessage message, string errorMessage)
        {
            Status = status;
            Message = message;
            ErrorMessage = errorMessage ?? "";
        }

        public PythonModelStatusParseStatus Status { get; }
        public PythonModelStatusMessage Message { get; }
        public string ErrorMessage { get; }
        public bool IsStatus => Status != PythonModelStatusParseStatus.NotStatus;

        public static PythonModelStatusParseResult NotStatus()
        {
            return new PythonModelStatusParseResult(PythonModelStatusParseStatus.NotStatus, null, "");
        }

        public static PythonModelStatusParseResult InvalidPayload(string error)
        {
            return new PythonModelStatusParseResult(PythonModelStatusParseStatus.InvalidPayload, null, error);
        }

        public static PythonModelStatusParseResult Parsed(PythonModelStatusMessage message)
        {
            return new PythonModelStatusParseResult(PythonModelStatusParseStatus.Parsed, message, "");
        }
    }

    public static class PythonModelStatusProtocol
    {
        public const string TrainingStatusType = "TrainingStatus";
        public const string TaskStatusType = "TaskStatus";
        public const string TrainYoloResultType = "TrainYoloResult";
        public const string DetectionStatusType = "DetectionStatus";
        public const string HealthCheckResultType = "HealthCheckResult";
        public const string ModelStatusResultType = "ModelStatusResult";

        public static PythonModelStatusParseResult Parse(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !message.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return PythonModelStatusParseResult.NotStatus();
            }

            try
            {
                JObject root = JObject.Parse(message);
                string type = root["type"]?.Value<string>() ?? string.Empty;
                if (!IsKnownStatusType(type))
                {
                    return PythonModelStatusParseResult.NotStatus();
                }

                PythonModelStatusMessage status = string.Equals(type, HealthCheckResultType, StringComparison.OrdinalIgnoreCase)
                    ? ParseHealthCheckResult(root)
                    : string.Equals(type, ModelStatusResultType, StringComparison.OrdinalIgnoreCase)
                        ? ParseModelStatusResult(root)
                        : string.Equals(type, TaskStatusType, StringComparison.OrdinalIgnoreCase)
                            ? ParseTaskStatus(root)
                            : string.Equals(type, TrainYoloResultType, StringComparison.OrdinalIgnoreCase)
                                ? ParseTrainYoloResult(root)
                                : root.ToObject<PythonModelStatusMessage>();
                if (status == null)
                {
                    return PythonModelStatusParseResult.NotStatus();
                }

                return PythonModelStatusParseResult.Parsed(status);
            }
            catch (JsonException ex)
            {
                return PythonModelStatusParseResult.InvalidPayload(ex.Message);
            }
        }

        private static PythonModelStatusMessage ParseHealthCheckResult(JObject root)
        {
            bool ok = root["ok"]?.Value<bool?>() ?? false;
            string state = root["state"]?.Value<string>();
            return new PythonModelStatusMessage
            {
                Type = HealthCheckResultType,
                Version = root["version"]?.Value<int?>() ?? 1,
                State = string.IsNullOrWhiteSpace(state) ? (ok ? "ready" : "error") : state,
                Message = BuildHealthSummary(root),
                Error = FormatError(root["error"]),
                Ok = ok,
                SupportedModels = ExtractCapabilityList(root, "supportedModels", "models", "adapters"),
                TrainingModels = ExtractCapabilityList(root, "trainingModels", "trainModels", "training", "train"),
                DetectionModels = ExtractCapabilityList(root, "detectionModels", "detectModels", "inspectionModels", "detect", "inspection")
            };
        }

        private static PythonModelStatusMessage ParseTaskStatus(JObject root)
        {
            string taskType = root["taskType"]?.Value<string>() ?? string.Empty;
            if (!IsTrainTaskType(taskType))
            {
                return null;
            }

            string state = root["state"]?.Value<string>() ?? string.Empty;
            List<string> logTail = ExtractStringArray(root["logTail"] as JArray);
            string failureReason = root["failureReason"]?.Value<string>() ?? string.Empty;
            string message = BuildTaskStatusMessage(root, state, failureReason, logTail);

            return new PythonModelStatusMessage
            {
                Type = TrainingStatusType,
                Version = root["version"]?.Value<int?>() ?? 1,
                State = state,
                Message = message,
                ProgressPercent = root["progressPercent"]?.Value<int?>(),
                Epoch = root["epoch"]?.Value<int?>(),
                TotalEpochs = root["totalEpochs"]?.Value<int?>(),
                Error = FormatError(root["error"]),
                FailureReason = failureReason,
                ExitCode = root["exitCode"]?.Value<int?>(),
                LogTail = logTail
            };
        }

        private static PythonModelStatusMessage ParseTrainYoloResult(JObject root)
        {
            bool ok = root["ok"]?.Value<bool?>() ?? false;
            string state = root["state"]?.Value<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state))
            {
                state = ok ? "started" : "failed";
            }

            return new PythonModelStatusMessage
            {
                Type = TrainingStatusType,
                Version = root["version"]?.Value<int?>() ?? 1,
                State = state,
                Message = ok ? "YOLO training accepted by worker." : "YOLO training could not start.",
                Error = FormatError(root["error"])
            };
        }

        private static PythonModelStatusMessage ParseModelStatusResult(JObject root)
        {
            JObject model = root["model"] as JObject;
            bool ok = root["ok"]?.Value<bool?>() ?? false;
            bool loaded = model?["loaded"]?.Value<bool?>() ?? false;
            string state = model?["state"]?.Value<string>();
            string weights = model?["weightsPath"]?.Value<string>();
            return new PythonModelStatusMessage
            {
                Type = ModelStatusResultType,
                Version = root["version"]?.Value<int?>() ?? 1,
                State = string.IsNullOrWhiteSpace(state) ? (ok ? "ready" : "error") : state,
                Message = !string.IsNullOrWhiteSpace(weights) ? $"weights: {weights}" : "model status",
                Error = FormatError(root["error"] ?? model?["lastError"]),
                Ok = ok,
                Loaded = loaded,
                SupportedModels = ExtractCapabilityList(root, "supportedModels", "models", "adapters"),
                TrainingModels = ExtractCapabilityList(root, "trainingModels", "trainModels", "training", "train"),
                DetectionModels = ExtractCapabilityList(root, "detectionModels", "detectModels", "inspectionModels", "detect", "inspection")
            };
        }

        private static List<string> ExtractCapabilityList(JObject root, params string[] names)
        {
            var values = new List<string>();
            if (root == null || names == null)
            {
                return values;
            }

            foreach (string name in names)
            {
                AppendCapabilityValues(root[name], values);
            }

            if (root["capabilities"] is JObject capabilities)
            {
                foreach (string name in names)
                {
                    AppendCapabilityValues(capabilities[name], values);
                }
            }

            if (root["worker"] is JObject worker && worker["capabilities"] is JObject workerCapabilities)
            {
                foreach (string name in names)
                {
                    AppendCapabilityValues(workerCapabilities[name], values);
                }
            }

            return values
                .Select(NormalizeCapabilityModel)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AppendCapabilityValues(JToken token, List<string> values)
        {
            if (token == null || values == null)
            {
                return;
            }

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken item in token)
                {
                    AppendCapabilityValues(item, values);
                }

                return;
            }

            if (token.Type == JTokenType.String)
            {
                string text = token.Value<string>() ?? string.Empty;
                foreach (string part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    values.Add(part);
                }
            }
        }

        private static string NormalizeCapabilityModel(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            string lower = trimmed.ToLowerInvariant();
            return lower switch
            {
                "yolov5" => "yolov5",
                "yolo5" => "yolov5",
                "v5" => "yolov5",
                "yolov8" => "yolov8",
                "yolo8" => "yolov8",
                "v8" => "yolov8",
                "yolo11" => "yolo11",
                "yolov11" => "yolo11",
                "v11" => "yolo11",
                "onnx" => "onnx",
                "onnxruntime" => "onnx",
                _ => lower
            };
        }

        private static string BuildHealthSummary(JObject root)
        {
            JObject worker = root["worker"] as JObject;
            string pid = worker?["pid"]?.Value<string>();
            string started = worker?["startedAtUtc"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(pid))
            {
                return string.IsNullOrWhiteSpace(started)
                    ? $"worker pid {pid}"
                    : $"worker pid {pid}, started {started}";
            }

            return "worker health checked";
        }

        private static string FormatError(JToken error)
        {
            if (error == null || error.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (error.Type == JTokenType.String)
            {
                return error.Value<string>() ?? string.Empty;
            }

            string code = error["code"]?.Value<string>();
            string message = error["message"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message))
            {
                return $"{code}: {message}";
            }

            return !string.IsNullOrWhiteSpace(message) ? message : error.ToString(Formatting.None);
        }

        private static string BuildTaskStatusMessage(JObject root, string state, string failureReason, List<string> logTail)
        {
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                return failureReason;
            }

            string message = root["message"]?.Value<string>() ?? string.Empty;
            if (!string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            string inferred = InferTrainingFailureReason(logTail);
            return !string.IsNullOrWhiteSpace(inferred) ? inferred : message;
        }

        private static string InferTrainingFailureReason(List<string> logTail)
        {
            if (logTail == null || logTail.Count == 0)
            {
                return string.Empty;
            }

            string joined = string.Join("\n", logTail).ToLowerInvariant();
            bool startedTraining = joined.Contains("starting training", StringComparison.Ordinal)
                || joined.Contains("epoch", StringComparison.Ordinal);
            bool hasExplicitError = joined.Contains("traceback", StringComparison.Ordinal)
                || joined.Contains("runtimeerror", StringComparison.Ordinal)
                || joined.Contains("exception", StringComparison.Ordinal)
                || joined.Contains("out of memory", StringComparison.Ordinal)
                || joined.Contains("memoryerror", StringComparison.Ordinal);

            if (startedTraining && !hasExplicitError)
            {
                return "Training failed: process exited near the first training batch. On CPU the selected training size may be too large. Try yolov5s, image 320, batch 4.";
            }

            return string.Empty;
        }

        private static List<string> ExtractStringArray(JArray array)
        {
            List<string> values = new List<string>();
            if (array == null)
            {
                return values;
            }

            foreach (JToken token in array)
            {
                string value = token?.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        private static bool IsKnownStatusType(string type)
        {
            return string.Equals(type, TrainingStatusType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, TaskStatusType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, TrainYoloResultType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, DetectionStatusType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, HealthCheckResultType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, ModelStatusResultType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrainTaskType(string taskType)
        {
            return string.Equals(taskType, "TrainYolo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(taskType, "Training", StringComparison.OrdinalIgnoreCase)
                || string.Equals(taskType, "Train", StringComparison.OrdinalIgnoreCase);
        }
    }
}
