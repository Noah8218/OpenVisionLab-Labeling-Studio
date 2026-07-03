using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public static class WpfImageQueuePresenter
    {
        public static string FormatQuickFilterText(string label, int count)
        {
            return $"{label} {(count > 99 ? "99+" : Math.Max(0, count).ToString(CultureInfo.InvariantCulture))}";
        }

        public static string BuildOpenSelectionFailureMessage(
            string searchText,
            int visibleCount,
            int searchMatchCount,
            string gridSelection,
            string viewModelSelection)
        {
            return $"열 이미지를 선택하세요. 검색='{(searchText ?? string.Empty).Trim()}' 표시={FormatLimitedQueueCount(visibleCount)} 검색일치={FormatLimitedQueueCount(searchMatchCount)} 선택={FormatSelection(gridSelection)} VM={FormatSelection(viewModelSelection)}";
        }

        public static string FormatLimitedQueueCount(int count)
        {
            return count >= 3 ? "3+" : Math.Max(0, count).ToString(CultureInfo.InvariantCulture);
        }

        public static string BuildReviewCountSummary(IEnumerable<WpfImageQueueItem> items)
        {
            var queueItems = (items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => item != null)
                .ToList();
            if (queueItems.Count == 0)
            {
                return string.Empty;
            }

            int candidateCount = queueItems.Count(item => item.ReviewState == YoloImageReviewState.Candidate);
            int failedCount = queueItems.Count(item => item.ReviewState == YoloImageReviewState.Failed);
            int confirmedCount = queueItems.Count(item => item.ReviewState == YoloImageReviewState.Confirmed);
            int skippedCount = queueItems.Count(item => item.ReviewState == YoloImageReviewState.Skipped);
            int noCandidateCount = queueItems.Count(item => item.ReviewState == YoloImageReviewState.NoCandidate);
            var parts = new List<string>();
            if (candidateCount > 0)
            {
                parts.Add($"AI 후보 {candidateCount}");
            }

            if (failedCount > 0)
            {
                parts.Add($"실패 {failedCount}");
            }

            if (confirmedCount > 0)
            {
                parts.Add($"저장됨 {confirmedCount}");
            }

            if (skippedCount > 0)
            {
                parts.Add($"숨김 {skippedCount}");
            }

            if (noCandidateCount > 0)
            {
                parts.Add($"객체없음 {noCandidateCount}");
            }

            return parts.Count == 0 ? string.Empty : " / " + string.Join(" / ", parts);
        }

        private static string FormatSelection(string selection)
        {
            return string.IsNullOrWhiteSpace(selection) ? "-" : selection.Trim();
        }

        public static string FormatLabelStatus(string labelText)
        {
            if (string.Equals(labelText, "No Label", StringComparison.OrdinalIgnoreCase))
            {
                return "없음";
            }

            if (string.Equals(labelText, "Empty Label", StringComparison.OrdinalIgnoreCase))
            {
                // Empty YOLO files are intentional "no object" completions, not unknown labels.
                return "객체 없음";
            }

            if (labelText?.StartsWith("Label ", StringComparison.OrdinalIgnoreCase) == true)
            {
                string count = labelText.Substring("Label ".Length).Split('/')[0].Trim();
                return $"{count}개";
            }

            if (labelText?.StartsWith("Invalid ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return $"오류 {labelText.Substring("Invalid ".Length).Trim()}";
            }

            return string.IsNullOrWhiteSpace(labelText) ? "없음" : labelText;
        }

        public static string FormatDetectionStatus(YoloImageReviewStatus status)
        {
            string detectionText = status?.DetectionText ?? string.Empty;
            if (detectionText.StartsWith("Candidate ", StringComparison.OrdinalIgnoreCase))
            {
                return $"AI 후보 {detectionText.Substring("Candidate ".Length).Trim()}";
            }

            return detectionText switch
            {
                "Requested" => "검사중",
                "Failed" => status?.DetectionAttemptCount > 1 ? $"실패 x{status.DetectionAttemptCount}" : "실패",
                "No Candidate" => "객체 없음",
                "Confirmed" => "저장됨",
                "Skipped" => "후보 숨김",
                _ => "대기"
            };
        }

        public static PackIconMaterialKind GetIconKind(YoloImageReviewStatus status)
        {
            return status?.ReviewState switch
            {
                YoloImageReviewState.Requested => PackIconMaterialKind.ClockOutline,
                YoloImageReviewState.Candidate => PackIconMaterialKind.ImageSearch,
                YoloImageReviewState.Confirmed => PackIconMaterialKind.CheckboxMarkedCircleOutline,
                YoloImageReviewState.Skipped => PackIconMaterialKind.SkipNext,
                YoloImageReviewState.NoCandidate => PackIconMaterialKind.MinusCircleOutline,
                YoloImageReviewState.Failed => PackIconMaterialKind.AlertCircleOutline,
                _ => status?.IsLabeled == true ? PackIconMaterialKind.TagOutline : PackIconMaterialKind.ImageOutline
            };
        }

        public static Brush GetIconBrush(YoloImageReviewStatus status)
        {
            return status?.ReviewState switch
            {
                YoloImageReviewState.Requested => WpfImageQueueItem.InfoBrush,
                YoloImageReviewState.Candidate => WpfImageQueueItem.WarningBrush,
                YoloImageReviewState.Confirmed => WpfImageQueueItem.SuccessBrush,
                YoloImageReviewState.Skipped => WpfImageQueueItem.MutedBrush,
                YoloImageReviewState.NoCandidate => WpfImageQueueItem.MutedBrush,
                YoloImageReviewState.Failed => WpfImageQueueItem.ErrorBrush,
                _ => status?.IsLabeled == true ? WpfImageQueueItem.SuccessBrush : WpfImageQueueItem.MutedBrush
            };
        }

        public static Brush GetBadgeBackgroundBrush(YoloImageReviewStatus status)
        {
            return status?.ReviewState switch
            {
                YoloImageReviewState.Requested => WpfImageQueueItem.InfoBadgeBrush,
                YoloImageReviewState.Candidate => WpfImageQueueItem.WarningBadgeBrush,
                YoloImageReviewState.Confirmed => WpfImageQueueItem.SuccessBadgeBrush,
                YoloImageReviewState.Skipped => WpfImageQueueItem.MutedBadgeBrush,
                YoloImageReviewState.NoCandidate => WpfImageQueueItem.MutedBadgeBrush,
                YoloImageReviewState.Failed => WpfImageQueueItem.ErrorBadgeBrush,
                _ => status?.IsLabeled == true ? WpfImageQueueItem.SuccessBadgeBrush : WpfImageQueueItem.TransparentBrush
            };
        }

        public static Brush GetRowAccentBrush(YoloImageReviewStatus status)
        {
            return status == null ? WpfImageQueueItem.TransparentBrush : GetIconBrush(status);
        }

        public static string BuildBadgeText(YoloImageReviewStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return status.ReviewState switch
            {
                YoloImageReviewState.Requested => status.DetectionAttemptCount > 1
                    ? $"검사 {status.DetectionAttemptCount}"
                    : "검사중",
                YoloImageReviewState.Candidate => status.DetectionCandidateCount > 0
                    ? $"AI {status.DetectionCandidateCount}"
                    : "AI",
                YoloImageReviewState.Failed => status.DetectionAttemptCount > 1
                    ? $"실패 {status.DetectionAttemptCount}"
                    : "실패",
                YoloImageReviewState.Confirmed => "저장",
                YoloImageReviewState.Skipped => "숨김",
                YoloImageReviewState.NoCandidate => "없음",
                _ => status.IsLabeled ? "저장" : string.Empty
            };
        }

        public static string BuildStatusSummary(YoloImageReviewStatus status)
        {
            if (status == null)
            {
                return "상태 확인 중";
            }

            if (status.ReviewState == YoloImageReviewState.Failed)
            {
                return ShortenMessage($"실패: {TranslateDetectionMessage(status.LastDetectionMessage)}", 32);
            }

            if (status.ReviewState == YoloImageReviewState.Candidate && status.DetectionCandidateCount > 0)
            {
                return $"AI 후보 {status.DetectionCandidateCount}개 검토 필요";
            }

            string label = FormatLabelStatus(status.LabelText);
            if (status.ReviewState == YoloImageReviewState.Confirmed)
            {
                return $"저장 완료: 라벨 {label}";
            }

            if (status.ReviewState == YoloImageReviewState.NoCandidate)
            {
                return $"객체 없음 완료: 라벨 {label}";
            }

            if (status.ReviewState == YoloImageReviewState.Skipped)
            {
                return $"후보 숨김 완료: 라벨 {label}";
            }

            string detection = FormatDetectionStatus(status);
            return $"저장 라벨 {label} / AI {detection}";
        }

        public static string TranslateDetectionMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "원인 미확인";
            }

            return message.Trim() switch
            {
                "Detection failed." => "검사 실패",
                "Detection request failed." => "요청 실패",
                "Batch detection timed out." => "시간 초과",
                "YOLO client is not connected." => "YOLO 미연결",
                _ => message.Trim()
            };
        }

        public static string ShortenMessage(string message, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length <= maxLength)
            {
                return message ?? string.Empty;
            }

            return message.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        public static string BuildDetailText(YoloImageReviewStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            List<string> details = new List<string>
            {
                $"저장 라벨: {FormatLabelStatus(status.LabelText)}",
                $"AI 후보: {FormatDetectionStatus(status)}"
            };
            if (status.DetectionAttemptCount > 0)
            {
                details.Add($"시도 {status.DetectionAttemptCount}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastDetectionMessage))
            {
                details.Add(TranslateDetectionMessage(status.LastDetectionMessage));
            }

            return string.Join(" / ", details);
        }
    }
}
