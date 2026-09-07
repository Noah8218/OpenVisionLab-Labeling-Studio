using OpenVisionLab.Mvvm;
using OpenVisionLab;
using System.Globalization;
using System;
using System.Windows;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfShellLogPanelViewModel : WpfObservableViewModel, IDisposable
    {
        private static readonly GridLength ExpandedLogPaneGridLengthValue = new GridLength(180D);
        private static readonly GridLength CollapsedLogPaneGridLengthValue = new GridLength(42D);
        private static readonly GridLength ExpandedSeparatorGridLengthValue = new GridLength(10D);
        private static readonly GridLength CollapsedSeparatorGridLengthValue = new GridLength(6D);

        private bool isLogPaneExpanded;
        private int logCount;
        private string logSummaryText = Format("WpfShell.Log.Count", 0);
        private string latestLogText = T("WpfShell.Log.Empty");
        private string latestLogMessage = string.Empty;
        private string logPaneToggleText = T("WpfShell.Log.Toggle.Open");
        private string logPaneToggleToolTip = T("WpfShell.Log.Toggle.Open.ToolTip");
        private Visibility collapsedSummaryVisibility = Visibility.Visible;
        private Visibility expandedLogVisibility = Visibility.Collapsed;
        private GridLength logPaneGridLength = CollapsedLogPaneGridLengthValue;
        private GridLength logPaneSeparatorGridLength = CollapsedSeparatorGridLengthValue;
        private ICommand toggleLogPaneCommand;
        private bool disposed;

        public WpfShellLogPanelViewModel()
        {
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

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
            LogPaneToggleText = isExpanded
                ? T("WpfShell.Log.Toggle.Close")
                : T("WpfShell.Log.Toggle.Open");
            LogPaneToggleToolTip = isExpanded
                ? T("WpfShell.Log.Toggle.Close.ToolTip")
                : T("WpfShell.Log.Toggle.Open.ToolTip");
        }

        public void RecordLog(string message)
        {
            LogCount++;
            latestLogMessage = message?.Trim() ?? string.Empty;
            RefreshLocalizedPresentation();
        }

        public void RefreshLocalizedPresentation()
        {
            LogSummaryText = Format("WpfShell.Log.Count", LogCount);
            string localizedMessage = string.IsNullOrWhiteSpace(latestLogMessage)
                ? string.Empty
                : LocalizationTextRuntimeService.Translate(latestLogMessage);
            LatestLogText = string.IsNullOrWhiteSpace(localizedMessage)
                ? T("WpfShell.Log.Empty")
                : Format("WpfShell.Log.Latest", localizedMessage);
            LogPaneToggleText = IsLogPaneExpanded
                ? T("WpfShell.Log.Toggle.Close")
                : T("WpfShell.Log.Toggle.Open");
            LogPaneToggleToolTip = IsLogPaneExpanded
                ? T("WpfShell.Log.Toggle.Close.ToolTip")
                : T("WpfShell.Log.Toggle.Open.ToolTip");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            RefreshLocalizedPresentation();
        }

        private static string T(string key)
        {
            return OpenVisionLanguageService.T(key);
        }

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }

        private void ToggleLogPane()
        {
            SetLogPaneExpanded(!IsLogPaneExpanded);
        }
    }
}
