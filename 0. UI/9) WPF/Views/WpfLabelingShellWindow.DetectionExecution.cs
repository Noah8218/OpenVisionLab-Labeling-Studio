using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Detection execution is kept apart from panel wiring so worker latency, fallback, and canvas update paths can be audited in one place.
        private async Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
        {
            if (isDetecting || isBatchDetectionRunning)
            {
                return;
            }

            EnsureProjectSettings();
            isDetecting = true;
            UpdateYoloCommandButtons();
            UpdateCandidateActionState();
            SetYoloCommandStatus("추론 준비 중...", isBusy: true);
            SetGlobalInferenceStatus("현재 이미지 추론 준비 중", isBusy: true);
            SetPythonStatus("\uCD94\uB860: \uC900\uBE44 \uC911");
            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                string targetImagePath = detectionTargetService.ResolveInteractiveTargetPath(
                    imagePath,
                    activeImagePath,
                    global.Data.ProjectSettings.PythonModel);
                string inferencePath = "worker";
                YoloWorkerSmokeTestResult result = await RunWorkerDetectionForImageAsync(
                        targetImagePath,
                        applyToCanvas: true,
                        CancellationToken.None,
                        GetInteractiveWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);
                if (!result.Succeeded && allowSmokeFallback)
                {
                    AppendLog($"\uCD94\uB860 \uC2E4\uD328, \uD14C\uC2A4\uD2B8 \uACBD\uB85C\uB85C \uC804\uD658: {Path.GetFileName(targetImagePath)}");
                    inferencePath = "smoke fallback";
                    result = await RunDetectionForImageAsync(targetImagePath, applyToCanvas: true, CancellationToken.None)
                        .ConfigureAwait(true);
                }

                string elapsed = FormatElapsed(totalStopwatch.Elapsed);
                SetYoloCommandStatus(
                    result.Succeeded
                        ? $"추론 완료: 후보 {result.CandidateCount}개 / {elapsed}"
                        : $"추론 실패: {elapsed}",
                    isBusy: false);
                SetGlobalInferenceStatus(
                    result.Succeeded
                        ? $"완료: 후보 {result.CandidateCount}개 / {elapsed}"
                        : $"실패: {elapsed}",
                    isBusy: false,
                    isWarning: !result.Succeeded);
                AppendLog(result.Succeeded
                    ? $"단일 이미지 추론 완료: {FormatElapsed(totalStopwatch.Elapsed)} / 경로 {FormatInferencePath(inferencePath)}"
                    : $"단일 이미지 추론 실패: {FormatElapsed(totalStopwatch.Elapsed)} / 추론 연결 또는 응답을 확인하세요.");
            }
            finally
            {
                isDetecting = false;
                UpdateYoloCommandButtons();
                UpdateCandidateActionState();
            }
        }


    }
}
