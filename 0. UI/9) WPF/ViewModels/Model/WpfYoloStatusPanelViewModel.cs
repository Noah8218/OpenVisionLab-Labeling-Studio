using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfYoloStatusPanelViewModel : WpfObservableViewModel
    {
        private const string RuntimeCheckWaitingText = "실행 환경 확인 대기";
        private string summaryText = "\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uBBF8\uD655\uC778";
        private string detailText = string.Empty;
        private string commandStatusText = RuntimeCheckWaitingText;
        private bool isRecoveryVisible;
        private string recoveryTitleText = string.Empty;
        private string recoveryDetailText = string.Empty;
        private string recoveryActionText = string.Empty;
        private Visibility commandProgressVisibility = Visibility.Collapsed;
        private bool commandProgressIsIndeterminate;
        private double commandProgressValue;
        private bool isFirstCheckEnabled = true;
        private bool isInstallRequirementsEnabled = true;
        private bool isRunSmokeEnabled = true;
        private bool isRestartWorkerEnabled = true;
        private bool isStopWorkerEnabled = true;
        private ICommand checkCommand = new RelayCommand(NoOpCommand);
        private ICommand checkTrainingCommand = new RelayCommand(NoOpCommand);
        private ICommand installRequirementsCommand = new RelayCommand(NoOpCommand);
        private ICommand runSmokeCommand = new RelayCommand(NoOpCommand);
        private ICommand restartWorkerCommand = new RelayCommand(NoOpCommand);
        private ICommand stopWorkerCommand = new RelayCommand(NoOpCommand);
        private YoloEnvironmentWorkflowService runtimeWorkflowService;
        private Func<YoloEnvironmentCallbacks> runtimeCallbacksProvider;

        public string ViewName => nameof(WpfYoloStatusPanel);

        public ICommand CheckCommand
        {
            get => checkCommand;
            private set => SetProperty(ref checkCommand, value);
        }

        public ICommand CheckTrainingCommand
        {
            get => checkTrainingCommand;
            private set => SetProperty(ref checkTrainingCommand, value);
        }

        public ICommand InstallRequirementsCommand
        {
            get => installRequirementsCommand;
            private set => SetProperty(ref installRequirementsCommand, value);
        }

        public ICommand RunSmokeCommand
        {
            get => runSmokeCommand;
            private set => SetProperty(ref runSmokeCommand, value);
        }

        public ICommand RestartWorkerCommand
        {
            get => restartWorkerCommand;
            private set => SetProperty(ref restartWorkerCommand, value);
        }

        public ICommand StopWorkerCommand
        {
            get => stopWorkerCommand;
            private set => SetProperty(ref stopWorkerCommand, value);
        }

        public string SummaryText
        {
            get => summaryText;
            set => SetProperty(ref summaryText, value ?? string.Empty);
        }

        public string DetailText
        {
            get => detailText;
            set => SetProperty(ref detailText, value ?? string.Empty);
        }

        public string CommandStatusText
        {
            get => commandStatusText == RuntimeCheckWaitingText ? OpenVisionLab.OpenVisionLanguageService.T("WpfTrainingWorkspace.RuntimeWaiting") : commandStatusText;
            set => SetProperty(ref commandStatusText, value ?? string.Empty);
        }

        public bool IsRecoveryVisible
        {
            get => isRecoveryVisible;
            private set => SetProperty(ref isRecoveryVisible, value);
        }

        public string RecoveryTitleText
        {
            get => recoveryTitleText;
            private set => SetProperty(ref recoveryTitleText, value ?? string.Empty);
        }

        public string RecoveryDetailText
        {
            get => recoveryDetailText;
            private set => SetProperty(ref recoveryDetailText, value ?? string.Empty);
        }

        public string RecoveryActionText
        {
            get => recoveryActionText;
            private set => SetProperty(ref recoveryActionText, value ?? string.Empty);
        }

        public Visibility CommandProgressVisibility
        {
            get => commandProgressVisibility;
            set => SetProperty(ref commandProgressVisibility, value);
        }

        public bool CommandProgressIsIndeterminate
        {
            get => commandProgressIsIndeterminate;
            set => SetProperty(ref commandProgressIsIndeterminate, value);
        }

        public double CommandProgressValue
        {
            get => commandProgressValue;
            set => SetProperty(ref commandProgressValue, value);
        }

        public bool IsFirstCheckEnabled
        {
            get => isFirstCheckEnabled;
            private set => SetProperty(ref isFirstCheckEnabled, value);
        }

        public bool IsInstallRequirementsEnabled
        {
            get => isInstallRequirementsEnabled;
            private set => SetProperty(ref isInstallRequirementsEnabled, value);
        }

        public bool IsRunSmokeEnabled
        {
            get => isRunSmokeEnabled;
            private set => SetProperty(ref isRunSmokeEnabled, value);
        }

        public bool IsRestartWorkerEnabled
        {
            get => isRestartWorkerEnabled;
            private set
            {
                if (!SetProperty(ref isRestartWorkerEnabled, value) || !value || pendingSavedRuntimeConnect == null) return;
                // A stage opened while general commands were locked (for example, the image list was still loading).
                (string key, PythonModelSettings settings, Func<bool> isConnected) pending = pendingSavedRuntimeConnect.Value;
                pendingSavedRuntimeConnect = null;
                TryConnectSavedRuntimeOnce(pending.key, pending.settings, pending.isConnected);
            }
        }

        public bool IsStopWorkerEnabled
        {
            get => isStopWorkerEnabled;
            private set => SetProperty(ref isStopWorkerEnabled, value);
        }

        public void ConfigureCommands(
            Action check,
            Action installRequirements,
            Action runSmoke,
            Action restartWorker,
            Action stopWorker)
        {
            // Keep this compatibility entry point for callers that supply a complete command surface.
            CheckCommand = new RelayCommand(check ?? NoOpCommand);
            CheckTrainingCommand = new RelayCommand(check ?? NoOpCommand);
            InstallRequirementsCommand = new RelayCommand(installRequirements ?? NoOpCommand);
            RunSmokeCommand = new RelayCommand(runSmoke ?? NoOpCommand);
            RestartWorkerCommand = new RelayCommand(restartWorker ?? NoOpCommand);
            StopWorkerCommand = new RelayCommand(stopWorker ?? NoOpCommand);
        }

        public void ConfigureRuntimeWorkflow(
            YoloEnvironmentWorkflowService workflowService,
            Func<YoloEnvironmentCallbacks> callbacksProvider)
        {
            runtimeWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            runtimeCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            CheckCommand = new RelayCommand(() => _ = ExecuteCheckAsync());
            CheckTrainingCommand = new RelayCommand(() => _ = ExecuteTrainingCheckAsync());
            InstallRequirementsCommand = new RelayCommand(() => _ = ExecuteInstallRequirementsAsync());
            RestartWorkerCommand = new RelayCommand(() => _ = ExecuteRestartWorkerAsync());
            StopWorkerCommand = new RelayCommand(() => _ = ExecuteStopWorkerAsync());
        }

        public void SetSettingsStatus(string summary, string detail)
        {
            SummaryText = summary;
            DetailText = detail;
        }

        public void SetCommandStatus(string text, bool isBusy)
        {
            CommandStatusText = string.IsNullOrWhiteSpace(text) ? RuntimeCheckWaitingText : text;
            SetCommandBusy(isBusy);
        }

        public void SetRecoveryState(string titleText, string detailText, string actionText)
        {
            RecoveryTitleText = (titleText ?? string.Empty).Trim();
            RecoveryDetailText = (detailText ?? string.Empty).Trim();
            RecoveryActionText = (actionText ?? string.Empty).Trim();
            IsRecoveryVisible = !string.IsNullOrWhiteSpace(RecoveryTitleText)
                || !string.IsNullOrWhiteSpace(RecoveryDetailText)
                || !string.IsNullOrWhiteSpace(RecoveryActionText);
        }

        public void ClearRecoveryState()
        {
            SetRecoveryState(string.Empty, string.Empty, string.Empty);
        }

        public void SetCommandBusy(bool isBusy)
        {
            CommandProgressVisibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            CommandProgressIsIndeterminate = isBusy;
            if (!isBusy)
            {
                CommandProgressValue = 0;
            }
        }

        public void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            IsFirstCheckEnabled = canRunGeneralCommands;
            IsInstallRequirementsEnabled = canRunGeneralCommands;
            IsRunSmokeEnabled = canRunGeneralCommands;
            IsRestartWorkerEnabled = canRunGeneralCommands;
            IsStopWorkerEnabled = canRunGeneralCommands;
        }

        // The Python worker only starts on demand, so after an app start the training and comparison stages
        // would stay blocked until the user found 환경 점검. When one of those stages opens, this connects the worker
        // once per recipe and session, and only for a saved runtime (Python path set) with auto start on.
        // While general commands are locked the request waits and runs once they unlock.
        public bool TryConnectSavedRuntimeOnce(string recipeKey, PythonModelSettings settings, Func<bool> isConnected)
        {
            if (isConnected?.Invoke() == true || settings == null || !settings.AutoStartClient || string.IsNullOrWhiteSpace(settings.PythonExecutablePath)) return false;
            if (savedRuntimeConnectAttempted.Contains(recipeKey ?? string.Empty)) return false;
            // The worker restart command connects without the environment check's model-center navigation, which
            // goes through a stage-change approval and stalled when started from the comparison stage.
            if (!IsRestartWorkerEnabled || RestartWorkerCommand?.CanExecute(null) != true)
            {
                pendingSavedRuntimeConnect = (recipeKey ?? string.Empty, settings, isConnected);
                return false;
            }
            savedRuntimeConnectAttempted.Add(recipeKey ?? string.Empty);
            RestartWorkerCommand.Execute(null);
            return true;
        }

        private readonly System.Collections.Generic.HashSet<string> savedRuntimeConnectAttempted = new(StringComparer.OrdinalIgnoreCase);
        private (string key, PythonModelSettings settings, Func<bool> isConnected)? pendingSavedRuntimeConnect;

        private Task ExecuteCheckAsync()
            => runtimeWorkflowService?.CheckAsync(runtimeCallbacksProvider?.Invoke()) ?? Task.CompletedTask;

        private Task ExecuteTrainingCheckAsync()
            => runtimeWorkflowService?.CheckTrainingAsync(runtimeCallbacksProvider?.Invoke()) ?? Task.CompletedTask;

        private Task ExecuteInstallRequirementsAsync()
            => runtimeWorkflowService?.InstallRequirementsAsync(runtimeCallbacksProvider?.Invoke()) ?? Task.CompletedTask;

        private Task ExecuteRestartWorkerAsync()
            => runtimeWorkflowService?.RestartWorkerAsync(runtimeCallbacksProvider?.Invoke()) ?? Task.CompletedTask;

        private Task ExecuteStopWorkerAsync()
            => runtimeWorkflowService?.StopWorkerAsync(runtimeCallbacksProvider?.Invoke()) ?? Task.CompletedTask;

        private static void NoOpCommand()
        {
        }
    }
}
