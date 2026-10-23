using System;

namespace MvcVisionSystem._1._Core
{
    // A result keeps the settings used by its request, independent of the next selection.
    public sealed record InferenceResultSource(string ImagePath, string ModelEngine, string WeightsPath, int ImageSize, float Confidence, int MaximumCandidates, bool ResultOnly, string CandidateId, DateTimeOffset StartedAt)
    {
        public DateTimeOffset CompletedAt { get; init; }
        public string ProjectRootPath { get; init; } = string.Empty;

        public static InferenceResultSource Capture(string imagePath, PythonModelSettings settings, bool resultOnly, string candidateId = "")
            => new InferenceResultSource(imagePath ?? string.Empty, settings?.ModelEngine ?? string.Empty, settings?.WeightsPath ?? string.Empty, settings?.InferenceImageSize ?? 320, settings?.MinimumDetectionConfidence ?? 0.25F, settings?.MaximumDetectionCandidates ?? 20, resultOnly, candidateId ?? string.Empty, DateTimeOffset.UtcNow) { ProjectRootPath = settings?.ProjectRootPath ?? string.Empty };

        public bool Matches(PythonModelSettings settings, bool resultOnly)
            => settings != null && string.Equals(ProjectRootPath.Trim(), settings.ProjectRootPath?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase) && ResultOnly == resultOnly && ImageSize == settings.InferenceImageSize && Confidence == settings.MinimumDetectionConfidence && MaximumCandidates == settings.MaximumDetectionCandidates
                && string.Equals(PythonModelSettings.NormalizeModelEngine(ModelEngine), PythonModelSettings.NormalizeModelEngine(settings.ModelEngine), StringComparison.OrdinalIgnoreCase)
                && string.Equals(WeightsPath?.Trim(), settings.WeightsPath?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
