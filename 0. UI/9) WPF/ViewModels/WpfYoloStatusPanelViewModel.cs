using System.Windows;

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

        public string ViewName => nameof(WpfYoloStatusPanel);

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
    }
}
