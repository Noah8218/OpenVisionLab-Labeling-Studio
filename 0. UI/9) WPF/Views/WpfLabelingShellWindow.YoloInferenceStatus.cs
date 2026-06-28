using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Inference status animation is UI-only and must stay out of detection hot paths.
        private void SetGlobalInferenceStatus(string text, bool isBusy, bool isWarning = false)
        {
            if (InferenceStatusText == null || InferenceStatusBorder == null)
            {
                return;
            }

            InferenceStatusText.Text = string.IsNullOrWhiteSpace(text) ? "대기" : text;
            InferenceStatusProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            InferenceStatusProgressBar.IsIndeterminate = false;
            if (isBusy)
            {
                StartInferenceStatusPulse();
            }
            else
            {
                StopInferenceStatusPulse();
            }
            InferenceStatusIcon.Kind = isBusy
                ? PackIconMaterialKind.ProgressClock
                : isWarning
                    ? PackIconMaterialKind.AlertCircleOutline
                    : PackIconMaterialKind.RobotIndustrial;

            InferenceStatusBorder.SetResourceReference(
                System.Windows.Controls.Border.BackgroundProperty,
                isBusy ? "DetectionOverlaySelectedBackgroundBrush" : "ToolbarButtonBrush");
            InferenceStatusBorder.SetResourceReference(
                System.Windows.Controls.Border.BorderBrushProperty,
                isBusy || isWarning ? "AccentBrush" : "BorderBrushDark");
        }

        private void StartInferenceStatusPulse()
        {
            if (InferenceStatusProgressBar == null)
            {
                return;
            }

            if (!inferenceStatusPulseTimer.IsEnabled)
            {
                // The progress is cosmetic: keep it timer-driven so inference work does not force layout updates from hot paths.
                inferenceStatusPulseStopwatch.Restart();
                InferenceStatusProgressBar.Value = 8;
                inferenceStatusPulseTimer.Start();
            }
        }

        private void StopInferenceStatusPulse()
        {
            inferenceStatusPulseTimer.Stop();
            inferenceStatusPulseStopwatch.Reset();
            if (InferenceStatusProgressBar != null)
            {
                InferenceStatusProgressBar.Value = 0;
            }
        }

        private void InferenceStatusPulseTimer_Tick(object sender, EventArgs e)
        {
            if (InferenceStatusProgressBar == null || InferenceStatusProgressBar.Visibility != Visibility.Visible)
            {
                StopInferenceStatusPulse();
                return;
            }

            const double cycleMilliseconds = 1400D;
            double elapsed = inferenceStatusPulseStopwatch.Elapsed.TotalMilliseconds;
            double phase = (elapsed % cycleMilliseconds) / cycleMilliseconds;
            InferenceStatusProgressBar.Value = 8D + (phase * 84D);
        }
    }
}
