using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Training commands are separated from inference commands because their status and cancellation paths differ.
        private void ExecuteRefreshTrainingReadinessCommand()
        {
            SaveTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: true);
        }

        private async void ExecuteStartTrainingCommand()
        {
            if (!BeginTrainingCommand("학습 데이터셋 준비 중..."))
            {
                return;
            }

            try
            {
                SaveTrainingEditorFields();
                RefreshTrainingReadinessPanel(refreshYaml: true);
                bool ready = await global
                    .EnsurePythonModelClientReadyAsync(GetWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);
                if (!ready)
                {
                    string readinessText = BuildPythonWorkerFailureText();
                    SetTrainingReadinessStatus(readinessText);
                    AppendLog(readinessText);
                    return;
                }

                bool started = global.TrainingWorkflow.TryStartTraining(global.Data, global.DeepLearning);
                string startText = started
                    ? "\uD559\uC2B5 \uC2DC\uC791 \uBA85\uB839 \uC804\uC1A1 \uC644\uB8CC. \uCD94\uB860 \uC2E4\uD589\uAE30 \uC0C1\uD0DC \uB300\uAE30 \uC911..."
                    : "\uD559\uC2B5 \uC2DC\uC791 \uBA85\uB839\uC744 \uBCF4\uB0B4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB370\uC774\uD130\uC14B \uC900\uBE44 \uC0C1\uD0DC\uC640 \uCD94\uB860 \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.";
                SetTrainingReadinessStatus(startText);
                AppendLog(startText);
                if (started)
                {
                    StartTrainingStatusPolling();
                }
            }
            catch (Exception ex)
            {
                string errorText = $"학습 시작 실패: {ex.Message}";
                SetTrainingReadinessStatus(errorText);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
            }
        }

        private async void ExecuteStopTrainingCommand()
        {
            if (!BeginTrainingCommand("학습 중지 요청 중..."))
            {
                return;
            }

            try
            {
                bool stopped = await Task.Run(() => global.TrainingWorkflow.TryStopTraining(global.DeepLearning)).ConfigureAwait(true);
                string stopText = stopped
                    ? "학습 중지 명령 전송 완료."
                    : "\uD559\uC2B5 \uC911\uC9C0 \uBA85\uB839\uC744 \uBCF4\uB0B4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uCD94\uB860 \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.";
                SetTrainingReadinessStatus(stopText);
                AppendLog(stopText);
            }
            catch (Exception ex)
            {
                string errorText = $"학습 중지 실패: {ex.Message}";
                SetTrainingReadinessStatus(errorText);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
            }
        }
    }
}
