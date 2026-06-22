using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

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
                Ok = ok
            };
        }

        private static PythonModelStatusMessage ParseTaskStatus(JObject root)
        {
            string taskType = root["taskType"]?.Value<string>() ?? string.Empty;
            if (!IsTrainTaskType(taskType))
            {
                return null;
            }

            return new PythonModelStatusMessage
            {
                Type = TrainingStatusType,
                Version = root["version"]?.Value<int?>() ?? 1,
                State = root["state"]?.Value<string>() ?? string.Empty,
                Message = root["message"]?.Value<string>() ?? string.Empty,
                ProgressPercent = root["progressPercent"]?.Value<int?>(),
                Epoch = root["epoch"]?.Value<int?>(),
                TotalEpochs = root["totalEpochs"]?.Value<int?>(),
                Error = FormatError(root["error"])
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
                Message = ok ? "YOLOv5 training accepted by worker." : "YOLOv5 training could not start.",
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
                Loaded = loaded
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
