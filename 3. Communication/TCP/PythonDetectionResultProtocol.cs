using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem._3._Communication.TCP
{
    public sealed class DefectInfo
    {
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; } = 0;
        public float X { get; set; } = 0;
        public float Y { get; set; } = 0;
        public float Width { get; set; } = 0;
        public float Height { get; set; } = 0;
    }

    public enum DetectionResultParseStatus
    {
        NotDetectionResult,
        EmptyPayload,
        InvalidPayload,
        Parsed
    }

    public sealed class DetectionResultParseResult
    {
        private DetectionResultParseResult(
            DetectionResultParseStatus status,
            IReadOnlyList<DefectInfo> defects,
            string errorMessage,
            string requestId = "",
            string imageId = "")
        {
            Status = status;
            Defects = defects ?? Array.Empty<DefectInfo>();
            ErrorMessage = errorMessage ?? "";
            RequestId = requestId ?? "";
            ImageId = imageId ?? "";
        }

        public DetectionResultParseStatus Status { get; }
        public IReadOnlyList<DefectInfo> Defects { get; }
        public string ErrorMessage { get; }
        public string RequestId { get; }
        public string ImageId { get; }
        public bool IsDetectionResult => Status != DetectionResultParseStatus.NotDetectionResult;

        public static DetectionResultParseResult NotDetectionResult()
        {
            return new DetectionResultParseResult(DetectionResultParseStatus.NotDetectionResult, Array.Empty<DefectInfo>(), "");
        }

        public static DetectionResultParseResult EmptyPayload()
        {
            return new DetectionResultParseResult(DetectionResultParseStatus.EmptyPayload, Array.Empty<DefectInfo>(), "ResultDefect payload is empty.");
        }

        public static DetectionResultParseResult InvalidPayload(string errorMessage)
        {
            return new DetectionResultParseResult(DetectionResultParseStatus.InvalidPayload, Array.Empty<DefectInfo>(), errorMessage);
        }

        public static DetectionResultParseResult InvalidPayload(string errorMessage, string requestId, string imageId)
        {
            return new DetectionResultParseResult(DetectionResultParseStatus.InvalidPayload, Array.Empty<DefectInfo>(), errorMessage, requestId, imageId);
        }

        public static DetectionResultParseResult Parsed(IReadOnlyList<DefectInfo> defects, string requestId = "", string imageId = "")
        {
            return new DetectionResultParseResult(DetectionResultParseStatus.Parsed, defects, "", requestId, imageId);
        }
    }

    public static class PythonDetectionResultProtocol
    {
        public const string ResultDefectCommand = "ResultDefect";
        public const string DetectImageResultType = "DetectImageResult";

        public static DetectionResultParseResult Parse(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return DetectionResultParseResult.NotDetectionResult();
            }

            string trimmed = message.Trim();
            if (trimmed.StartsWith(ResultDefectCommand, StringComparison.Ordinal))
            {
                return ParseLegacyResult(trimmed);
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return ParseEnvelopeResult(trimmed);
            }

            return DetectionResultParseResult.NotDetectionResult();
        }

        private static DetectionResultParseResult ParseLegacyResult(string message)
        {
            string jsonResult = message.Substring(ResultDefectCommand.Length).Trim();
            if (string.IsNullOrWhiteSpace(jsonResult))
            {
                return DetectionResultParseResult.EmptyPayload();
            }

            return ParseDefectArray(jsonResult);
        }

        private static DetectionResultParseResult ParseEnvelopeResult(string message)
        {
            try
            {
                DetectionResultEnvelope envelope = JsonConvert.DeserializeObject<DetectionResultEnvelope>(message);
                if (envelope == null || !IsDetectionEnvelopeType(envelope.Type))
                {
                    return DetectionResultParseResult.NotDetectionResult();
                }

                string error = FormatEnvelopeError(envelope.Error);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return DetectionResultParseResult.InvalidPayload(error, envelope.RequestId, envelope.ImageId);
                }

                List<DefectInfo> defects = string.Equals(envelope.Type, DetectImageResultType, StringComparison.OrdinalIgnoreCase)
                    ? envelope.Candidates ?? new List<DefectInfo>()
                    : envelope.Items ?? new List<DefectInfo>();
                return DetectionResultParseResult.Parsed(defects, envelope.RequestId, envelope.ImageId);
            }
            catch (JsonException ex)
            {
                return DetectionResultParseResult.InvalidPayload(ex.Message);
            }
        }

        private static bool IsDetectionEnvelopeType(string type)
        {
            return string.Equals(type, ResultDefectCommand, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, DetectImageResultType, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatEnvelopeError(JToken error)
        {
            if (error == null || error.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (error.Type == JTokenType.String)
            {
                return error.Value<string>() ?? string.Empty;
            }

            string message = error["message"]?.Value<string>();
            string code = error["code"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message))
            {
                return $"{code}: {message}";
            }

            return !string.IsNullOrWhiteSpace(message) ? message : error.ToString(Formatting.None);
        }

        private static DetectionResultParseResult ParseDefectArray(string jsonResult)
        {
            try
            {
                List<DefectInfo> defects = JsonConvert.DeserializeObject<List<DefectInfo>>(jsonResult) ?? new List<DefectInfo>();
                return DetectionResultParseResult.Parsed(defects);
            }
            catch (JsonException ex)
            {
                return DetectionResultParseResult.InvalidPayload(ex.Message);
            }
        }

        public static List<DetectionOverlayItem> BuildDetectionOverlays(IEnumerable<DefectInfo> defects)
        {
            return BuildDetectionOverlays(defects, null);
        }

        public static List<DetectionOverlayItem> BuildDetectionOverlays(IEnumerable<DefectInfo> defects, Func<string, Color?> colorResolver)
        {
            return BuildDetectionOverlays(defects, colorResolver, selectedCandidateIndex: 0);
        }

        public static List<DetectionOverlayItem> BuildDetectionOverlays(IEnumerable<DefectInfo> defects, Func<string, Color?> colorResolver, int selectedCandidateIndex)
        {
            if (defects == null)
            {
                return new List<DetectionOverlayItem>();
            }

            return defects
                .Select((defect, index) => new { Defect = defect, CandidateIndex = index + 1 })
                .Where(item => item.Defect.Width > 0 && item.Defect.Height > 0)
                .Select(item => new DetectionOverlayItem
                {
                    CandidateIndex = item.CandidateIndex,
                    ClassName = item.Defect.ClassName ?? string.Empty,
                    Confidence = Truncate2(item.Defect.Confidence),
                    Bounds = new RectangleF(
                        Truncate2(item.Defect.X),
                        Truncate2(item.Defect.Y),
                        Truncate2(item.Defect.Width),
                        Truncate2(item.Defect.Height)),
                    Color = ResolveOverlayColor(item.Defect.ClassName, colorResolver),
                    IsSelected = item.CandidateIndex == selectedCandidateIndex
                })
                .ToList();
        }

        private static Color ResolveOverlayColor(string className, Func<string, Color?> colorResolver)
        {
            Color? classColor = colorResolver?.Invoke(className ?? "");
            if (classColor.HasValue)
            {
                return classColor.Value;
            }

            return string.Equals(className, "OK", StringComparison.OrdinalIgnoreCase) ? Color.Green : Color.Red;
        }

        private static float Truncate2(float value)
        {
            return (float)Math.Truncate(100 * value) / 100;
        }

        private sealed class DetectionResultEnvelope
        {
            public string Type { get; set; } = "";
            public int Version { get; set; }
            public string RequestId { get; set; } = "";
            public string ImageId { get; set; } = "";
            public JToken Error { get; set; }
            public List<DefectInfo> Items { get; set; } = new List<DefectInfo>();
            public List<DefectInfo> Candidates { get; set; } = new List<DefectInfo>();
        }
    }
}
