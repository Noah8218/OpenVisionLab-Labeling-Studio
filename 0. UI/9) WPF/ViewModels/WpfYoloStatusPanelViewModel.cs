using System;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfYoloStatusPanelViewModel : WpfObservableViewModel
    {
        private string summaryText = "YOLO \uC124\uC815 \uBBF8\uD655\uC778";
        private string detailText = string.Empty;
        private string commandStatusText = "YOLO \uBA85\uB839 \uB300\uAE30";
        private Visibility commandProgressVisibility = Visibility.Collapsed;
        private bool commandProgressIsIndeterminate;
        private double commandProgressValue;
        private bool isFirstCheckEnabled = true;
        private bool isInstallRequirementsEnabled = true;
        private bool isRunSmokeEnabled = true;
        private bool isRestartWorkerEnabled = true;
        private bool isStopWorkerEnabled = true;
        private ICommand checkCommand = new RelayCommand(NoOpCommand);
        private ICommand installRequirementsCommand = new RelayCommand(NoOpCommand);
        private ICommand runSmokeCommand = new RelayCommand(NoOpCommand);
        private ICommand restartWorkerCommand = new RelayCommand(NoOpCommand);
        private ICommand stopWorkerCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfYoloStatusPanel);

        public ICommand CheckCommand
        {
            get => checkCommand;
            private set => SetProperty(ref checkCommand, value);
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
            get => commandStatusText;
            set => SetProperty(ref commandStatusText, value ?? string.Empty);
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
            private set => SetProperty(ref isRestartWorkerEnabled, value);
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
            // YOLO status actions remain shell-owned; this panel only declares the command surface.
            CheckCommand = new RelayCommand(check ?? NoOpCommand);
            InstallRequirementsCommand = new RelayCommand(installRequirements ?? NoOpCommand);
            RunSmokeCommand = new RelayCommand(runSmoke ?? NoOpCommand);
            RestartWorkerCommand = new RelayCommand(restartWorker ?? NoOpCommand);
            StopWorkerCommand = new RelayCommand(stopWorker ?? NoOpCommand);
        }

        public void SetSettingsStatus(string summary, string detail)
        {
            SummaryText = summary;
            DetailText = detail;
        }

        public void SetCommandStatus(string text, bool isBusy)
        {
            CommandStatusText = string.IsNullOrWhiteSpace(text) ? "YOLO \uBA85\uB839 \uB300\uAE30" : text;
            SetCommandBusy(isBusy);
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

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            IsFirstCheckEnabled = canRunGeneralCommands;
            IsInstallRequirementsEnabled = canRunGeneralCommands;
            IsRunSmokeEnabled = canRunGeneralCommands;
            IsRestartWorkerEnabled = canRunGeneralCommands;
            IsStopWorkerEnabled = canRunGeneralCommands;
        }

        private static void NoOpCommand()
        {
        }
    }
}