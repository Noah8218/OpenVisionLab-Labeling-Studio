using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfBatchExistingLabelPolicyOption : WpfObservableViewModel
    {
        public WpfBatchExistingLabelPolicyOption(
            WpfBatchExistingLabelPolicy policy,
            string displayKey,
            string detailKey)
        {
            Policy = policy;
            this.displayKey = displayKey ?? string.Empty;
            this.detailKey = detailKey ?? string.Empty;
        }

        private readonly string displayKey;
        private readonly string detailKey;

        public WpfBatchExistingLabelPolicy Policy { get; }

        public string DisplayText => OpenVisionLanguageService.T(displayKey);

        public string DetailText => OpenVisionLanguageService.T(detailKey);

        internal void RefreshLocalizedPresentation()
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(DetailText));
        }
    }

    public sealed class WpfBatchPreflightFindingItem
    {
        public WpfBatchPreflightFindingItem(string text, bool isBlocking)
        {
            Text = text ?? string.Empty;
            IsBlocking = isBlocking;
        }

        public string Text { get; }

        public bool IsBlocking { get; }

        public string SeverityText => OpenVisionLanguageService.T(
            IsBlocking ? "WpfBatch.Finding.Severity.Blocking" : "WpfBatch.Finding.Severity.Warning");
    }

    public sealed class WpfBatchDetectionPreflightViewModel : WpfObservableViewModel, IDisposable
    {
        private readonly BatchDetectionPreflightService preflightService;
        private bool disposed;
        private LabelingProjectData data;
        private IReadOnlyList<WpfImageQueueItem> items = Array.Empty<WpfImageQueueItem>();
        private string scopeText = string.Empty;
        private WpfBatchExistingLabelPolicyOption selectedExistingLabelPolicy;
        private WpfBatchDetectionPreflightReport currentReport;
        private string statusText = "\uC0AC\uC804\uAC80\uC0AC \uB300\uAE30";
        private string countText = "\uC694\uCCAD 0 \u00B7 \uC2E4\uD589 0";
        private string modelText = string.Empty;
        private string classContractText = string.Empty;
        private string destinationPolicyText = string.Empty;

        public WpfBatchDetectionPreflightViewModel(
            LabelingProjectData data,
            IReadOnlyList<WpfImageQueueItem> items,
            string scopeText,
            BatchDetectionPreflightService preflightService = null)
        {
            this.preflightService = preflightService ?? new BatchDetectionPreflightService();
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            ExistingLabelPolicies.Add(new WpfBatchExistingLabelPolicyOption(
                WpfBatchExistingLabelPolicy.SkipLabeled,
                "WpfBatch.Policy.Skip.Display",
                "WpfBatch.Policy.Skip.Detail"));
            ExistingLabelPolicies.Add(new WpfBatchExistingLabelPolicyOption(
                WpfBatchExistingLabelPolicy.IncludeAndKeep,
                "WpfBatch.Policy.Include.Display",
                "WpfBatch.Policy.Include.Detail"));
            RecheckCommand = new RelayCommand(RunDryRun);
            StartCommand = new RelayCommand(RequestStart, () => CanStart);
            Refresh(data, items, scopeText);
        }

        public event EventHandler<WpfBatchDetectionPlan> StartRequested;

        public ObservableCollection<WpfBatchExistingLabelPolicyOption> ExistingLabelPolicies { get; } =
            new ObservableCollection<WpfBatchExistingLabelPolicyOption>();

        public ObservableCollection<WpfBatchClassMappingItem> ClassMappings { get; } =
            new ObservableCollection<WpfBatchClassMappingItem>();

        public ObservableCollection<WpfBatchPreflightFindingItem> Findings { get; } =
            new ObservableCollection<WpfBatchPreflightFindingItem>();

        public WpfBatchExistingLabelPolicyOption SelectedExistingLabelPolicy
        {
            get => selectedExistingLabelPolicy;
            set
            {
                if (SetProperty(ref selectedExistingLabelPolicy, value))
                {
                    RunDryRun();
                }
            }
        }

        public string ScopeText => scopeText;

        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public string CountText
        {
            get => countText;
            private set => SetProperty(ref countText, value ?? string.Empty);
        }

        public string ModelText
        {
            get => modelText;
            private set => SetProperty(ref modelText, value ?? string.Empty);
        }

        public string ClassContractText
        {
            get => classContractText;
            private set => SetProperty(ref classContractText, value ?? string.Empty);
        }

        public string DestinationPolicyText
        {
            get => destinationPolicyText;
            private set => SetProperty(ref destinationPolicyText, value ?? string.Empty);
        }

        public bool CanStart => !disposed && currentReport?.CanStart == true;

        public ICommand RecheckCommand { get; }

        public ICommand StartCommand { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
        }

        public void Refresh(LabelingProjectData sourceData, IReadOnlyList<WpfImageQueueItem> sourceItems, string sourceScopeText)
        {
            if (disposed)
            {
                return;
            }

            data = sourceData;
            items = sourceItems ?? Array.Empty<WpfImageQueueItem>();
            scopeText = sourceScopeText ?? string.Empty;
            OnPropertyChanged(nameof(ScopeText));
            if (SelectedExistingLabelPolicy == null)
            {
                SelectedExistingLabelPolicy = ExistingLabelPolicies.First();
            }
            else
            {
                RunDryRun();
            }
        }

        private void RunDryRun()
        {
            if (disposed)
            {
                return;
            }

            currentReport = preflightService.DryRun(new WpfBatchDetectionPreflightRequest
            {
                Data = data,
                Items = items,
                ScopeText = scopeText,
                ExistingLabelPolicy = SelectedExistingLabelPolicy?.Policy
                    ?? WpfBatchExistingLabelPolicy.SkipLabeled
            });

            Findings.Clear();
            foreach (string issue in currentReport.Issues)
            {
                Findings.Add(new WpfBatchPreflightFindingItem(LocalizeFinding(issue), isBlocking: true));
            }

            foreach (string warning in currentReport.Warnings)
            {
                Findings.Add(new WpfBatchPreflightFindingItem(LocalizeFinding(warning), isBlocking: false));
            }

            ClassMappings.Clear();
            foreach (WpfBatchClassMappingItem mapping in currentReport.ClassMappings)
            {
                ClassMappings.Add(mapping);
            }

            StatusText = currentReport.CanStart
                ? currentReport.Warnings.Count > 0
                    ? T("WpfBatch.Status.ReadyWithWarnings")
                    : T("WpfBatch.Status.Ready")
                : T("WpfBatch.Status.Blocked");
            CountText = Format(
                "WpfBatch.Count",
                currentReport.RequestedCount,
                currentReport.RunnableItems.Count,
                currentReport.ExistingLabelCount,
                currentReport.SkippedExistingLabelCount);
            ModelText = Format(
                "WpfBatch.Model",
                currentReport.ModelEngineText,
                FormatPurposeText(data?.ProjectSettings?.DatasetPurpose),
                currentReport.ConfidenceText,
                currentReport.WeightsPath);
            ClassContractText = ClassMappings.Count == 0
                ? T("WpfBatch.ClassContract.Empty")
                : Format("WpfBatch.ClassContract.Count", ClassMappings.Count);
            DestinationPolicyText = T("WpfBatch.DestinationPolicy");
            OnPropertyChanged(nameof(CanStart));
            if (StartCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            foreach (WpfBatchExistingLabelPolicyOption option in ExistingLabelPolicies)
            {
                option.RefreshLocalizedPresentation();
            }

            RunDryRun();
            OnPropertyChanged(nameof(ScopeText));
        }

        private string LocalizeFinding(string text)
        {
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean
                || string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            if (TryLocalizePathFinding(
                    text,
                    "\u0059\u004F\u004C\u004F \uD504\uB85C\uC81D\uD2B8 \uD3F4\uB354\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ",
                    "WpfBatch.Finding.ProjectFolderNotFound",
                    out string localizedPathFinding)
                || TryLocalizePathFinding(
                    text,
                    "\u0059\u004F\u004C\u004F \u0054\u0043\u0050 \uD074\uB77C\uC774\uC5B8\uD2B8 \uC2A4\uD06C\uB9BD\uD2B8\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ",
                    "WpfBatch.Finding.ClientScriptNotFound",
                    out localizedPathFinding)
                || TryLocalizePathFinding(
                    text,
                    "\u0050\u0079\u0074\u0068\u006F\u006E \uC2E4\uD589 \uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ",
                    "WpfBatch.Finding.PythonExecutableNotFound",
                    out localizedPathFinding)
                || TryLocalizePathFinding(
                    text,
                    "\u0059\u004F\u004C\u004F \uAC00\uC911\uCE58 \uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ",
                    "WpfBatch.Finding.WeightsFileNotFound",
                    out localizedPathFinding))
            {
                return localizedPathFinding;
            }

            const string missingImagePrefix = "\uD30C\uC77C\uC744 \uC5F4 \uC218 \uC5C6\uB294 \uC774\uBBF8\uC9C0 ";
            const string missingImageSuffix = "\uAC1C\uAC00 \uD3EC\uD568\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
            if (text.StartsWith(missingImagePrefix, StringComparison.Ordinal)
                && text.EndsWith(missingImageSuffix, StringComparison.Ordinal)
                && int.TryParse(
                    text.Substring(
                        missingImagePrefix.Length,
                        text.Length - missingImagePrefix.Length - missingImageSuffix.Length),
                    out int missingImageCount))
            {
                return Format("WpfBatch.Finding.MissingImages", missingImageCount);
            }

            const string includeExistingPrefix = "\uAE30\uC874 \uB77C\uBCA8\uC774 \uC788\uB294 ";
            const string includeExistingMiddle = "\uAC1C \uC774\uBBF8\uC9C0\uB3C4 \uAC80\uC0AC\uD569\uB2C8\uB2E4. \uAE30\uC874 \uB77C\uBCA8\uC740 \uBCF4\uC874\uB429\uB2C8\uB2E4.";
            if (text.StartsWith(includeExistingPrefix, StringComparison.Ordinal)
                && text.EndsWith(includeExistingMiddle, StringComparison.Ordinal)
                && int.TryParse(
                    text.Substring(
                        includeExistingPrefix.Length,
                        text.Length - includeExistingPrefix.Length - includeExistingMiddle.Length),
                    out int existingLabelCount))
            {
                return Format("WpfBatch.Finding.IncludeExisting", existingLabelCount);
            }

            return LocalizationTextRuntimeService.Translate(text);
        }

        private static bool TryLocalizePathFinding(
            string text,
            string prefix,
            string localizationKey,
            out string localizedText)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal))
            {
                localizedText = string.Empty;
                return false;
            }

            localizedText = Format(localizationKey, text.Substring(prefix.Length));
            return true;
        }

        private static string FormatPurposeText(LabelingDatasetPurpose? purpose)
        {
            return (purpose ?? LabelingDatasetPurpose.ObjectDetection) switch
            {
                LabelingDatasetPurpose.Segmentation => T("WpfShell.Dataset.Purpose.Segmentation"),
                LabelingDatasetPurpose.AnomalyDetection => T("WpfShell.Dataset.Purpose.AnomalyDetection"),
                _ => T("WpfShell.Dataset.Purpose.ObjectDetection")
            };
        }

        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] values)
            => string.Format(System.Globalization.CultureInfo.InvariantCulture, T(key), values ?? Array.Empty<object>());

        private void RequestStart()
        {
            if (!CanStart)
            {
                return;
            }

            StartRequested?.Invoke(
                this,
                new WpfBatchDetectionPlan(
                    currentReport.RunnableItems,
                    currentReport.ScopeText,
                    SelectedExistingLabelPolicy.Policy));
        }
    }
}
