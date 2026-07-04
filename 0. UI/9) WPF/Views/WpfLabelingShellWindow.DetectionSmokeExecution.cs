using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private async Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            EnsureProjectSettings();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                AppendLog($"검출 이미지 없음: {imagePath}");
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 찾지 못했습니다.",
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { $"검출 이미지를 찾지 못했습니다: {imagePath}" }
                };
            }

            if (applyToCanvas && !string.Equals(imagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                TryLoadImage(imagePath);
            }

            SetPythonStatus("\uCD94\uB860: \uD14C\uC2A4\uD2B8 \uC2E4\uD589 \uC911");
            AppendLog($"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uC791: {Path.GetFileName(imagePath)}");
            YoloWorkerSmokeTestResult result = await YoloWorkerSmokeTestService
                .RunAsync(global.Data.ProjectSettings.PythonModel, imagePath, cancellationToken)
                .ConfigureAwait(true);

            if (applyToCanvas)
            {
                // Keep existing manual labels when smoke detection returns the already-active image;
                // Candidate Review needs those labels to compute duplicate/current-label focus.
                if (!string.IsNullOrWhiteSpace(result.ImagePath)
                    && File.Exists(result.ImagePath)
                    && !string.Equals(result.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    TryLoadImage(result.ImagePath);
                }

                ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                SetPythonStatus(detectionResultPresentationService.BuildSmokeStatus(result));
                foreach (string error in result.Errors)
                {
                    AppendLog($"- {error}");
                }
            }

            AppendLog(result.Summary);
            AppendLog($"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uAC04: {FormatElapsed(stopwatch.Elapsed)}");
            return result;
        }
    }
}
