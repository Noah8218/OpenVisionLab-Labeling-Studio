using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool BeginYoloEnvironmentCommand(string statusText)
        {
            if (isYoloEnvironmentCommandRunning || isTrainingCommandRunning || isDetecting || isBatchDetectionRunning)
            {
                AppendLog("\uBAA8\uB378 \uC2E4\uD589 \uBA85\uB839\uC774 \uC774\uBBF8 \uC2E4\uD589 \uC911\uC785\uB2C8\uB2E4.");
                return false;
            }

            isYoloEnvironmentCommandRunning = true;
            ClearYoloRecoveryStatus();
            SetYoloCommandStatus(statusText, isBusy: true);
            UpdateYoloCommandButtons();
            return true;
        }

        private void EndYoloEnvironmentCommand()
        {
            isYoloEnvironmentCommandRunning = false;
            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetCommandBusy(false);
            }
            else
            {
                YoloCommandProgressBar.IsIndeterminate = false;
                YoloCommandProgressBar.Visibility = Visibility.Collapsed;
            }

            UpdateYoloCommandButtons();
            RefreshYoloStatus();
        }

        private void SetYoloCommandStatus(string text, bool isBusy)
        {
            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetCommandStatus(text, isBusy);
                return;
            }

            YoloCommandStatusText.Text = string.IsNullOrWhiteSpace(text) ? "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uB300\uAE30" : text;
            YoloCommandProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            YoloCommandProgressBar.IsIndeterminate = isBusy;
            if (!isBusy)
            {
                YoloCommandProgressBar.Value = 0;
            }
        }

        private void SetYoloRecoveryStatus(string titleText, string detailText, string actionText)
        {
            ShellViewModel?.SetModelCenterRecoveryState(titleText, detailText, actionText);
            YoloStatusViewModel?.SetRecoveryState(titleText, detailText, actionText);
        }

        private void ClearYoloRecoveryStatus()
        {
            ShellViewModel?.ClearModelCenterRecoveryState();
            YoloStatusViewModel?.ClearRecoveryState();
        }
    }
}
