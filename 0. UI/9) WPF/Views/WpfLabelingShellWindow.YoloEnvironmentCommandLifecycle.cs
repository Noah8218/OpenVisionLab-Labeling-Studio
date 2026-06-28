using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool BeginYoloEnvironmentCommand(string statusText)
        {
            if (isYoloEnvironmentCommandRunning || isTrainingCommandRunning || isDetecting || isBatchDetectionRunning)
            {
                AppendLog("YOLO 명령이 이미 실행 중입니다.");
                return false;
            }

            isYoloEnvironmentCommandRunning = true;
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

            YoloCommandStatusText.Text = string.IsNullOrWhiteSpace(text) ? "YOLO 명령 대기" : text;
            YoloCommandProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            YoloCommandProgressBar.IsIndeterminate = isBusy;
            if (!isBusy)
            {
                YoloCommandProgressBar.Value = 0;
            }
        }
    }
}
