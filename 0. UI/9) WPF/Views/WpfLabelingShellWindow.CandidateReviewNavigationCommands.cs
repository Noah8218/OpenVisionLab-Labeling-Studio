using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Keyboard shortcuts and candidate navigation stay isolated from confirmation side effects.
        private void ExecuteCandidatePreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                ExecuteConfirmSelectedCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                ExecuteSkipSelectedCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A && (e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ExecuteConfirmAllCandidatesCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                ExecuteNextCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                ExecutePreviousCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                FocusSelectedCandidateInViewer(logIfMissing: true);
                e.Handled = true;
            }
        }

        private void ExecutePreviousCandidateCommand()
        {
            SelectCandidateOffset(-1);
        }

        private void ExecuteNextCandidateCommand()
        {
            SelectCandidateOffset(1);
        }

        private void ExecuteOpenModelComparisonExampleCommand(WpfModelComparisonReviewExample example)
        {
            if (example == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(example.ImagePath) || !File.Exists(example.ImagePath))
            {
                CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C: {example.ImageKey}");
                AppendLog($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C: {example.ImageKey}");
                return;
            }

            bool loaded = TryLoadImage(
                example.ImagePath,
                populateQueue: true,
                refreshQueueDetails: true,
                refreshActiveStatus: true,
                appendLoadLog: false);
            if (!loaded)
            {
                return;
            }

            FocusModelComparisonExampleInViewer(example);
            AppendLog($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {example.Title}");
        }

        private void FocusModelComparisonExampleInViewer(WpfModelComparisonReviewExample example)
        {
            DrawingRectangle bounds = BuildModelComparisonExampleBounds(example);
            string boundsText = bounds.IsEmpty
                ? example?.LocationText ?? string.Empty
                : WpfCandidateReviewPresenter.FormatBoundsCompact(bounds);
            CandidateReviewViewModel?.SetModelComparisonFocus(example, boundsText);

            if (bounds.IsEmpty)
            {
                CanvasPanelViewModel?.SetDetectionOverlay(
                    "\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC",
                    Path.GetFileName(example.ImagePath),
                    example.Title,
                    example.ReviewText,
                    WpfDetectionOverlayStatus.Review);
                CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {Path.GetFileName(example.ImagePath)} / \uC704\uCE58 \uD45C\uC2DC \uC5C6\uC74C");
                return;
            }

            // Model comparison examples are not live detection candidates; draw one selected overlay so the clicked difference is unmistakable.
            MainCanvasViewModel.SetDetectionOverlays(new[]
            {
                new RoiImageCanvasDetectionOverlay
                {
                    Index = -1,
                    Bounds = bounds,
                    Label = BuildModelComparisonExampleLabel(example),
                    IsSelected = true,
                    Color = System.Drawing.Color.FromArgb(245, 158, 11)
                }
            });
            MainCanvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRect(bounds));
            CanvasPanelViewModel?.SetDetectionOverlay(
                "\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC",
                Path.GetFileName(example.ImagePath),
                $"{example.Title} / {boundsText}",
                example.ReviewText,
                WpfDetectionOverlayStatus.Review);
            CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {Path.GetFileName(example.ImagePath)} / {boundsText}");
            SetModelStatus($"\uBAA8\uB378 \uCC28\uC774 \uC704\uCE58: {example.Title}  {boundsText}");
        }

        private DrawingRectangle BuildModelComparisonExampleBounds(WpfModelComparisonReviewExample example)
        {
            if (example?.HasFocusBox != true || activeImageSize.IsEmpty)
            {
                return DrawingRectangle.Empty;
            }

            int left = (int)Math.Floor(example.Left * activeImageSize.Width);
            int top = (int)Math.Floor(example.Top * activeImageSize.Height);
            int right = (int)Math.Ceiling(example.Right * activeImageSize.Width);
            int bottom = (int)Math.Ceiling(example.Bottom * activeImageSize.Height);
            left = Math.Max(0, Math.Min(activeImageSize.Width - 1, left));
            top = Math.Max(0, Math.Min(activeImageSize.Height - 1, top));
            right = Math.Max(left + 1, Math.Min(activeImageSize.Width, right));
            bottom = Math.Max(top + 1, Math.Min(activeImageSize.Height, bottom));
            return new DrawingRectangle(left, top, right - left, bottom - top);
        }

        private static string BuildModelComparisonExampleLabel(WpfModelComparisonReviewExample example)
        {
            switch (example?.Kind)
            {
                case "CandidateOnly":
                    return "\uC0C8 \uBAA8\uB378\uB9CC";
                case "BaselineOnly":
                    return "\uAE30\uC874 \uBAA8\uB378\uB9CC";
                case "ClassChanged":
                    return "\uB77C\uBCA8 \uB2E4\uB984";
                default:
                    return "\uCC28\uC774 \uC704\uCE58";
            }
        }

        private void SelectCandidateOffset(int offset)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            WpfCandidateNavigationSelection selection = WpfCandidateReviewSelectionService.SelectCandidateOffset(
                CandidateReviewViewModel.Candidates,
                CandidateReviewViewModel.SelectedCandidate,
                offset);
            if (selection.Status == WpfCandidateNavigationStatus.NoCandidates)
            {
                AppendLog("이동할 검출 후보가 없습니다.");
                return;
            }

            if (selection.Status == WpfCandidateNavigationStatus.SingleCandidate)
            {
                AppendLog("이동할 다른 검출 후보가 없습니다.");
                return;
            }

            CandidateReviewViewModel.SelectedCandidate = selection.SelectedItem;
            CandidateListBox?.ScrollIntoView(selection.SelectedItem);
            CandidateListBox?.Focus();
            FocusSelectedCandidateInViewer(logIfMissing: false);
        }

        private YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
            YoloWorkerSmokeCandidate current,
            IEnumerable<YoloWorkerSmokeCandidate> removingCandidates)
        {
            return WpfCandidateReviewSelectionService.FindNextVisibleCandidateAfter(
                GetVisibleCandidateList(),
                current,
                removingCandidates);
        }
    }
}
