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
            if (isTrainingWorkflowRunning || IsTrainingStopAvailable(global.GetPythonCommunicationStatusSnapshot()))
            {
                string alreadyRunningText = "\uC774\uBBF8 \uD559\uC2B5\uC774 \uC9C4\uD589 \uC911\uC785\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uC791\uD558\uB824\uBA74 \uBA3C\uC800 \uC911\uC9C0\uD558\uC138\uC694.";
                SetTrainingReadinessStatus(alreadyRunningText);
                AppendLog(alreadyRunningText);
                UpdateYoloCommandButtons();
                return;
            }

            if (!EnsureModelRuntimeForTraining())
            {
                UpdateYoloCommandButtons();
                return;
            }

            if (!BeginTrainingCommand("학습 데이터셋 준비 중..."))
            {
                return;
            }

            string pendingRecoveryTitle = null;
            string pendingRecoveryDetail = null;
            string pendingRecoveryAction = null;
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
                    pendingRecoveryTitle = "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC2E4\uD328";
                    pendingRecoveryDetail = readinessText;
                    pendingRecoveryAction = "\uB2E4\uC74C: \uBAA8\uB378 \uC2E4\uD589\uAE30\uC758 \uD14C\uC2A4\uD2B8 \uB610\uB294 \uC7AC\uC2DC\uC791\uC73C\uB85C \uC5F0\uACB0\uC744 \uD655\uC778\uD55C \uB4A4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.";
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                    pendingRecoveryTitle = "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC2E4\uD328";
                    pendingRecoveryDetail = readinessText;
                    pendingRecoveryAction = "\uB2E4\uC74C: \uBAA8\uB378 \uC2E4\uD589\uAE30\uC758 \uD14C\uC2A4\uD2B8 \uB610\uB294 \uC7AC\uC2DC\uC791\uC73C\uB85C \uC5F0\uACB0\uC744 \uD655\uC778\uD55C \uB4A4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.";
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                    AppendLog(readinessText);
                    return;
                }

                bool started = global.TrainingWorkflow.TryStartTraining(global.Data, global.DeepLearning);
                string startText = started
                    ? "\uD559\uC2B5 \uBA85\uB839 \uC804\uC1A1 \uC644\uB8CC. \uC6CC\uCEE4 \uC751\uB2F5\uACFC \uC5D0\uD3ED \uB85C\uADF8\uB97C \uAE30\uB2E4\uB9AC\uB294 \uC911\uC785\uB2C8\uB2E4. \uC911\uC9C0 \uBC84\uD2BC\uC73C\uB85C \uCDE8\uC18C\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4."
                    : "\uD559\uC2B5 \uC2DC\uC791 \uBA85\uB839\uC744 \uBCF4\uB0B4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB370\uC774\uD130\uC14B \uC900\uBE44 \uC0C1\uD0DC\uC640 \uCD94\uB860 \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.";
                SetTrainingReadinessStatus(startText);
                if (!started)
                {
                    pendingRecoveryTitle = "\uD559\uC2B5 \uC2DC\uC791 \uC2E4\uD328";
                    pendingRecoveryDetail = startText;
                    pendingRecoveryAction = "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uACB0\uACFC\uC640 \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC0C1\uD0DC\uB97C \uD655\uC778\uD55C \uB4A4 \uD559\uC2B5 \uC2DC\uC791\uC744 \uB2E4\uC2DC \uB204\uB974\uC138\uC694.";
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                }
                if (!started)
                {
                    pendingRecoveryTitle = "\uD559\uC2B5 \uC2DC\uC791 \uC2E4\uD328";
                    pendingRecoveryDetail = startText;
                    pendingRecoveryAction = "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uACB0\uACFC\uC640 \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC0C1\uD0DC\uB97C \uD655\uC778\uD55C \uB4A4 \uD559\uC2B5 \uC2DC\uC791\uC744 \uB2E4\uC2DC \uB204\uB974\uC138\uC694.";
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                }

                AppendLog(startText);
                if (started)
                {
                    isTrainingWorkflowRunning = true;
                    SetTrainingProgressStatus("\uD559\uC2B5 \uBA85\uB839 \uC218\uB77D\uB428 / \uC5D0\uD3ED \uC2DC\uC791 \uB300\uAE30", string.Empty, 0D, isIndeterminate: true);
                    StartTrainingStatusPolling();
                    UpdateYoloCommandButtons();
                }
            }
            catch (Exception ex)
            {
                string errorText = $"학습 시작 실패: {ex.Message}";
                SetTrainingReadinessStatus(errorText);
                pendingRecoveryTitle = "\uD559\uC2B5 \uC2DC\uC791 \uC624\uB958";
                pendingRecoveryDetail = errorText;
                pendingRecoveryAction = "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C \uB9C8\uC9C0\uB9C9 \uC624\uB958\uB97C \uD655\uC778\uD558\uACE0 \uC124\uC815\uC744 \uC218\uC815\uD55C \uB4A4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.";
                SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                pendingRecoveryTitle = "\uD559\uC2B5 \uC2DC\uC791 \uC624\uB958";
                pendingRecoveryDetail = errorText;
                pendingRecoveryAction = "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C \uB9C8\uC9C0\uB9C9 \uC624\uB958\uB97C \uD655\uC778\uD558\uACE0 \uC124\uC815\uC744 \uC218\uC815\uD55C \uB4A4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.";
                SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
                if (!string.IsNullOrWhiteSpace(pendingRecoveryTitle))
                {
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                }
            }
        }

        private async void ExecuteStopTrainingCommand()
        {
            if (!BeginTrainingCommand("학습 중지 요청 중..."))
            {
                return;
            }

            string pendingRecoveryTitle = null;
            string pendingRecoveryDetail = null;
            string pendingRecoveryAction = null;
            try
            {
                bool stopped = await Task.Run(() => global.TrainingWorkflow.TryStopTraining(global.DeepLearning)).ConfigureAwait(true);
                string stopText = stopped
                    ? "학습 중지 명령 전송 완료."
                    : "\uD559\uC2B5 \uC911\uC9C0 \uBA85\uB839\uC744 \uBCF4\uB0B4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uCD94\uB860 \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.";
                if (stopped)
                {
                    isTrainingWorkflowRunning = false;
                }

                SetTrainingReadinessStatus(stopText);
                if (!stopped)
                {
                    pendingRecoveryTitle = "\uD559\uC2B5 \uC911\uC9C0 \uC2E4\uD328";
                    pendingRecoveryDetail = stopText;
                    pendingRecoveryAction = "\uB2E4\uC74C: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uB610\uB294 \uC911\uC9C0 \uD6C4 \uC0C1\uD0DC\uB97C \uB2E4\uC2DC \uD655\uC778\uD558\uC138\uC694.";
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                }

                if (!stopped)
                {
                    pendingRecoveryTitle = "\uD559\uC2B5 \uC911\uC9C0 \uC2E4\uD328";
                    pendingRecoveryDetail = stopText;
                    pendingRecoveryAction = "\uB2E4\uC74C: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uB610\uB294 \uC911\uC9C0 \uD6C4 \uC0C1\uD0DC\uB97C \uB2E4\uC2DC \uD655\uC778\uD558\uC138\uC694.";
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                }

                AppendLog(stopText);
            }
            catch (Exception ex)
            {
                string errorText = $"학습 중지 실패: {ex.Message}";
                SetTrainingReadinessStatus(errorText);
                pendingRecoveryTitle = "\uD559\uC2B5 \uC911\uC9C0 \uC624\uB958";
                pendingRecoveryDetail = errorText;
                pendingRecoveryAction = "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C \uC624\uB958\uB97C \uD655\uC778\uD558\uACE0 \uBAA8\uB378 \uC2E4\uD589\uAE30\uB97C \uC7AC\uC2DC\uC791\uD558\uC138\uC694.";
                SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
                if (!string.IsNullOrWhiteSpace(pendingRecoveryTitle))
                {
                    SetYoloRecoveryStatus(pendingRecoveryTitle, pendingRecoveryDetail, pendingRecoveryAction);
                }
            }
        }
    }
}
