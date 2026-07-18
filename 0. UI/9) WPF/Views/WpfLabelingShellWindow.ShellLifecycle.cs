using System;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Window lifecycle is invoked through WpfLabelingShellViewModel commands, not XAML event handlers.
        private void ExecuteLoadedCommand()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshYoloStatus();
                _ = RefreshYoloSettingsPanelAsync();
                TryLoadStartupSampleImage();
                SetPythonStatus("\uCD94\uB860: \uC2E4\uD589 \uB300\uAE30");
                AppendLog("시작 완료. 추론은 사용자가 명시적으로 실행할 때만 시작합니다.");
            }), DispatcherPriority.ApplicationIdle);
        }


        private void ExecuteClosedCommand()
        {
            SaveWorkspaceLayoutSettings();
            CloseModelBenchmarkWindow();
            CloseDatasetHealthWindow();
            StopInferenceStatusPulse();
            inferenceStatusPulseTimer.Tick -= InferenceStatusPulseTimer_Tick;
            StopTrainingStatusPolling();
            trainingStatusPollTimer.Tick -= TrainingStatusPollTimer_Tick;
            imageDecodePreloadService.CancelAndWait(TimeSpan.FromSeconds(2));
            CancelImageQueueDetailRefresh(waitForCompletion: true);
            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = null;
            global.StopPythonModelClientConnection();
            imageDecodeCacheService.Clear();
            activeImageBitmap?.Dispose();
            activeImageBitmap = null;
        }
    }
}
