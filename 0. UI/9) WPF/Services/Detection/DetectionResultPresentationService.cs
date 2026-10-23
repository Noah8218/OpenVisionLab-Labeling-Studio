using MvcVisionSystem._1._Core;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class DetectionResultPresentationService
    {
        // Detection result wording is kept out of the shell so workflow refactors do not duplicate operator-facing status text.
        public string BuildCandidateLoadHistory(int candidateCount, bool succeeded, double confidenceFilter)
        {
            if (!succeeded)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.History.Failed");
            }

            return candidateCount == 0
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.History.Empty")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.History.Loaded"),
                    candidateCount,
                    confidenceFilter);
        }

        public string BuildSmokeStatus(YoloWorkerSmokeTestResult result)
        {
            if (result?.Succeeded == true)
            {
                int polygonCandidateCount = CountPolygonCandidates(result);
                string status = string.Format(
                    CultureInfo.CurrentCulture,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Smoke.Done"),
                    result.CandidateCount);
                return polygonCandidateCount > 0
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Smoke.Mask"),
                        status,
                        polygonCandidateCount)
                    : status;
            }

            string reason = FirstNonEmpty(result?.Summary, result?.ErrorCode);
            return string.IsNullOrWhiteSpace(reason)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Smoke.Failed")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Smoke.Reason"),
                    TrimStatusReason(reason));
        }

        public WpfDetectionOverlayPresentation BuildNoCandidateOverlay(string imagePath, double confidenceFilter)
        {
            string imageName = ResolveImageName(imagePath);
            return new WpfDetectionOverlayPresentation(
                isEmpty: false,
                title: OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Empty.Title"),
                summary: string.Format(
                    CultureInfo.CurrentCulture,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Empty.Summary"),
                    imageName,
                    confidenceFilter),
                selectedText: OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Empty.Selected"),
                detail: OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Empty.Detail"),
                status: WpfDetectionOverlayStatus.Review);
        }

        public WpfDetectionOverlayPresentation BuildFailureOverlay(string imagePath, string summary)
        {
            string imageName = ResolveImageName(imagePath);
            string failureSummary = WpfImageQueuePresenter.TranslateDetectionMessage(summary);
            return new WpfDetectionOverlayPresentation(
                isEmpty: false,
                title: OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Failure.Title"),
                summary: string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Failure.Summary"), imageName),
                selectedText: string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Failure.Selected"), failureSummary),
                detail: string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ResultPresentation.Failure.Detail"), failureSummary),
                status: WpfDetectionOverlayStatus.Review);
        }

        private static string ResolveImageName(string imagePath)
        {
            return string.IsNullOrWhiteSpace(imagePath)
                ? "-"
                : Path.GetFileName(imagePath);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string TrimStatusReason(string reason)
        {
            const int MaxLength = 180;
            string normalized = reason?.Trim() ?? string.Empty;
            return normalized.Length <= MaxLength
                ? normalized
                : normalized.Substring(0, MaxLength - 3) + "...";
        }

        private static int CountPolygonCandidates(YoloWorkerSmokeTestResult result)
            => result?.Candidates?.Count(candidate =>
                string.Equals(candidate?.SegmentationType, "polygon", System.StringComparison.OrdinalIgnoreCase)
                || (candidate?.PolygonPoints?.Count ?? 0) >= 3) ?? 0;
    }

    [Obsolete("Use DetectionResultPresentationService.", false)]
    public sealed class WpfDetectionResultPresentationService : DetectionResultPresentationService
    {
    }
}
