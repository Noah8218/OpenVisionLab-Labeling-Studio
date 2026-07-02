using OpenVisionLab.Mvvm;
using System;
using System.Windows;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfShellLogPanelViewModel : WpfObservableViewModel
    {
        private static readonly GridLength ExpandedLogPaneGridLengthValue = new GridLength(180D);
        private static readonly GridLength CollapsedLogPaneGridLengthValue = new GridLength(42D);
        private static readonly GridLength ExpandedSeparatorGridLengthValue = new GridLength(10D);
        private static readonly GridLength CollapsedSeparatorGridLengthValue = new GridLength(6D);

        private bool isLogPaneExpanded;
        private int logCount;
        private string logSummaryText = "\uB85C\uADF8 0\uAC74";
        private string latestLogText = "\uCD5C\uADFC \uB85C\uADF8 \uC5C6\uC74C";
        private string logPaneToggleText = "\uB85C\uADF8 \uC5F4\uAE30";
        private string logPaneToggleToolTip = "\uD558\uB2E8 \uC0C1\uC138 \uB85C\uADF8\uB97C \uC5F4\uC5B4 \uC2E4\uD589 \uC774\uB825\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.";
        private Visibility collapsedSummaryVisibility = Visibility.Visible;
        private Visibility expandedLogVisibility = Visibility.Collapsed;
        private GridLength logPaneGridLength = CollapsedLogPaneGridLengthValue;
        private GridLength logPaneSeparatorGridLength = CollapsedSeparatorGridLengthValue;
        private ICommand toggleLogPaneCommand;

        public string ViewName => nameof(WpfShellLogPanel);

        public bool IsLogPaneExpanded
        {
            get => isLogPaneExpanded;
            private set => SetProperty(ref isLogPaneExpanded, value);
        }

        public int LogCount
        {
            get => logCount;
            private set => SetProperty(ref logCount, value);
        }

        public string LogSummaryText
        {
            get => logSummaryText;
            private set => SetProperty(ref logSummaryText, value ?? string.Empty);
        }

        public string LatestLogText
        {
            get => latestLogText;
            private set => SetProperty(ref latestLogText, value ?? string.Empty);
        }

        public string LogPaneToggleText
        {
            get => logPaneToggleText;
            private set => SetProperty(ref logPaneToggleText, value ?? string.Empty);
        }

        public string LogPaneToggleToolTip
        {
            get => logPaneToggleToolTip;
            private set => SetProperty(ref logPaneToggleToolTip, value ?? string.Empty);
        }

        public Visibility CollapsedSummaryVisibility
        {
            get => collapsedSummaryVisibility;
            private set => SetProperty(ref collapsedSummaryVisibility, value);
        }

        public Visibility ExpandedLogVisibility
        {
            get => expandedLogVisibility;
            private set => SetProperty(ref expandedLogVisibility, value);
        }

        public GridLength LogPaneGridLength
        {
            get => logPaneGridLength;
            private set => SetProperty(ref logPaneGridLength, value);
        }

        public GridLength LogPaneSeparatorGridLength
        {
            get => logPaneSeparatorGridLength;
            private set => SetProperty(ref logPaneSeparatorGridLength, value);
        }

        public ICommand ToggleLogPaneCommand
        {
            get
            {
                if (toggleLogPaneCommand == null)
                {
                    toggleLogPaneCommand = new RelayCommand(ToggleLogPane);
                }

                return toggleLogPaneCommand;
            }
        }

        public void SetLogPaneExpanded(bool isExpanded)
        {
            IsLogPaneExpanded = isExpanded;
            CollapsedSummaryVisibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
            ExpandedLogVisibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            LogPaneGridLength = isExpanded ? ExpandedLogPaneGridLengthValue : CollapsedLogPaneGridLengthValue;
            LogPaneSeparatorGridLength = isExpanded ? ExpandedSeparatorGridLengthValue : CollapsedSeparatorGridLengthValue;
            LogPaneToggleText = isExpanded ? "\uB85C\uADF8 \uC811\uAE30" : "\uB85C\uADF8 \uC5F4\uAE30";
            LogPaneToggleToolTip = isExpanded
                ? "\uD558\uB2E8 \uB85C\uADF8\uB97C \uC811\uACE0 \uCE94\uBC84\uC2A4 \uC138\uB85C \uACF5\uAC04\uC744 \uB113\uD799\uB2C8\uB2E4."
                : "\uD558\uB2E8 \uC0C1\uC138 \uB85C\uADF8\uB97C \uC5F4\uC5B4 \uC2E4\uD589 \uC774\uB825\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.";
        }

        public void RecordLog(string message)
        {
            LogCount++;
            LogSummaryText = $"\uB85C\uADF8 {LogCount}\uAC74";
            LatestLogText = string.IsNullOrWhiteSpace(message)
                ? "\uCD5C\uADFC \uB85C\uADF8: \uB0B4\uC6A9 \uC5C6\uC74C"
                : $"\uCD5C\uADFC \uB85C\uADF8: {message.Trim()}";
        }

        private void ToggleLogPane()
        {
            SetLogPaneExpanded(!IsLogPaneExpanded);
        }
    }
}
