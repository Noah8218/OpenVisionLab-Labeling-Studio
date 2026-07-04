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
                string alreadyRunningText = WpfTrainingCommandPresentationService.BuildAlreadyRunningStatus();
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

            if (!BeginTrainingCommand(WpfTrainingCommandPresentationService.BuildPreparingDatasetStatus()))
            {
                return;
            }

            WpfTrainingRecoveryStatus pendingRecovery = null;
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
                    pendingRecovery = WpfTrainingCommandPresentationService.BuildWorkerConnectionFailureRecovery(readinessText);
                    AppendLog(readinessText);
                    return;
                }

                bool started = global.TrainingWorkflow.TryStartTraining(global.Data, global.DeepLearning);
                string startText = WpfTrainingCommandPresentationService.BuildStartCommandResultStatus(
                    started,
                    global.TrainingWorkflow.LastPreparationFailureMessage);
                SetTrainingReadinessStatus(startText);
                if (!started)
                {
                    pendingRecovery = WpfTrainingCommandPresentationService.BuildStartFailureRecovery(startText);
                }

                AppendLog(startText);
                if (started)
                {
                    isTrainingWorkflowRunning = true;
                    SetTrainingProgressStatus(WpfTrainingCommandPresentationService.BuildTrainingAcceptedProgressText(), string.Empty, 0D, isIndeterminate: true);
                    StartTrainingStatusPolling();
                    UpdateYoloCommandButtons();
                }
            }
            catch (Exception ex)
            {
                string errorText = WpfTrainingCommandPresentationService.BuildStartExceptionStatus(ex.Message);
                SetTrainingReadinessStatus(errorText);
                pendingRecovery = WpfTrainingCommandPresentationService.BuildStartExceptionRecovery(errorText);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
                if (pendingRecovery != null)
                {
                    SetYoloRecoveryStatus(pendingRecovery.Title, pendingRecovery.Detail, pendingRecovery.Action);
                }
            }
        }

        private async void ExecuteStopTrainingCommand()
        {
            if (!BeginTrainingCommand(WpfTrainingCommandPresentationService.BuildStoppingStatus()))
            {
                return;
            }

            WpfTrainingRecoveryStatus pendingRecovery = null;
            try
            {
                bool stopped = await Task.Run(() => global.TrainingWorkflow.TryStopTraining(global.DeepLearning)).ConfigureAwait(true);
                string stopText = WpfTrainingCommandPresentationService.BuildStopCommandResultStatus(stopped);
                if (stopped)
                {
                    isTrainingWorkflowRunning = false;
                }

                SetTrainingReadinessStatus(stopText);
                if (!stopped)
                {
                    pendingRecovery = WpfTrainingCommandPresentationService.BuildStopFailureRecovery(stopText);
                }

                AppendLog(stopText);
            }
            catch (Exception ex)
            {
                string errorText = WpfTrainingCommandPresentationService.BuildStopExceptionStatus(ex.Message);
                SetTrainingReadinessStatus(errorText);
                pendingRecovery = WpfTrainingCommandPresentationService.BuildStopExceptionRecovery(errorText);
                AppendLog(errorText);
            }
            finally
            {
                EndTrainingCommand();
                if (pendingRecovery != null)
                {
                    SetYoloRecoveryStatus(pendingRecovery.Title, pendingRecovery.Detail, pendingRecovery.Action);
                }
            }
        }
    }
}
