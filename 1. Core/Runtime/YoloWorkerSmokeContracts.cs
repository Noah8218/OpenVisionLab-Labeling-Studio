using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloWorkerSmokeCandidate
    {
        public int Index { get; set; }
        public int? ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string CandidateType { get; set; } = string.Empty;
        public string PredictionType { get; set; } = string.Empty;
        public bool ImageLevel { get; set; }
        public double? AnomalyScore { get; set; }
        public double? AnomalyThreshold { get; set; }
        public string HeatmapPath { get; set; } = string.Empty;
        public string SegmentationType { get; set; } = string.Empty;
        public IReadOnlyList<DetectionPolygonPoint> PolygonPoints { get; set; } = Array.Empty<DetectionPolygonPoint>();
        public IReadOnlyList<DetectionPolygonPoint> NormalizedPolygonPoints { get; set; } = Array.Empty<DetectionPolygonPoint>();

        public System.Drawing.Rectangle ToRectangle()
        {
            int x = (int)Math.Round(X);
            int y = (int)Math.Round(Y);
            int width = (int)Math.Round(Width);
            int height = (int)Math.Round(Height);
            return width <= 0 || height <= 0
                ? System.Drawing.Rectangle.Empty
                : new System.Drawing.Rectangle(x, y, width, height);
        }
    }

    public sealed class YoloWorkerSmokeTestResult
    {
        public bool Succeeded { get; set; }
        public int ExitCode { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string PythonExecutablePath { get; set; } = string.Empty;
        public string ProjectRootPath { get; set; } = string.Empty;
        public string ClientScriptPath { get; set; } = string.Empty;
        public string ModelRootPath { get; set; } = string.Empty;
        public string WeightsPath { get; set; } = string.Empty;
        public string InferenceModelCandidateId { get; set; } = string.Empty;
        public InferenceResultSource Source { get; internal set; }
        public string ImagePath { get; set; } = string.Empty;
        public int CandidateCount { get; set; }
        public string FirstClassName { get; set; } = string.Empty;
        public double? FirstConfidence { get; set; }
        public IReadOnlyList<YoloWorkerSmokeCandidate> Candidates { get; set; } = Array.Empty<YoloWorkerSmokeCandidate>();
        public double? CaptureConfidenceFloor { get; set; }
        public bool HasCompletePredictions { get; set; }
        public int? CaptureCandidateLimit { get; set; }
        public string CaptureImageSha256 { get; set; } = string.Empty;
        public string CaptureWeightsSha256 { get; set; } = string.Empty;
        public double? CaptureAnomalyScore { get; set; }
        public string CaptureTrainingRunId { get; set; } = string.Empty;
        public string CaptureCandidateSettingsIdentity { get; set; } = string.Empty;
        public int? ElapsedMilliseconds { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }
}
