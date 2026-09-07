using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    internal static class DatasetInterchangeTextFormatter
    {
        public static string Translate(string key)
            => OpenVisionLanguageService.T(key);

        public static string Format(string key, params object[] arguments)
            => string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                Translate(key),
                arguments ?? Array.Empty<object>());
    }

    public sealed class WpfDatasetInterchangeOption : WpfObservableViewModel, IDisposable
    {
        private bool disposed;

        public WpfDatasetInterchangeOption(DatasetExportCapability capability)
        {
            Capability = capability;
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

        public DatasetExportCapability Capability { get; }

        public string DisplayText => $"{Capability.DisplayName} \u00B7 {DirectionText}";

        public string DirectionText => T(
            string.Equals(Capability.Direction, "import", StringComparison.OrdinalIgnoreCase)
                ? "WpfDatasetInterchange.Direction.Import"
                : "WpfDatasetInterchange.Direction.Export");

        public string PurposeText => DatasetContextPresentationService.FormatPurposeName(
            Enum.TryParse(Capability.DatasetPurpose, out LabelingDatasetPurpose purpose)
                ? purpose
                : LabelingDatasetPurpose.ObjectDetection);

        public bool IsImport => string.Equals(Capability.Direction, "import", StringComparison.OrdinalIgnoreCase);

        public bool RequiresImageRoot => IsImport
            && !string.Equals(Capability.FormatKey, "cvat-detection-import", StringComparison.Ordinal)
            && !string.Equals(Capability.FormatKey, "cvat-segmentation-import", StringComparison.Ordinal);

        public bool SourceIsDirectory => string.Equals(
            Capability.FormatKey,
            "pascal-voc-detection-import",
            StringComparison.Ordinal);

        public bool TargetIsDirectory => string.Equals(
            Capability.FormatKey,
            "pascal-voc-detection",
            StringComparison.Ordinal);

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
            if (disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(DirectionText));
            OnPropertyChanged(nameof(PurposeText));
        }

        private static string T(string key)
            => DatasetInterchangeTextFormatter.Translate(key);
    }

    public sealed class WpfDatasetInterchangeIssueItem
    {
        public WpfDatasetInterchangeIssueItem(string text, bool isBlocking)
        {
            Text = text ?? string.Empty;
            IsBlocking = isBlocking;
        }

        public string Text { get; }

        public bool IsBlocking { get; }

        public string SeverityText => DatasetInterchangeTextFormatter.Translate(
            IsBlocking
                ? "WpfDatasetInterchange.IssueSeverity.Blocking"
                : "WpfDatasetInterchange.IssueSeverity.Warning");
    }

    public sealed class WpfDatasetInterchangeViewModel : WpfObservableViewModel, IDisposable
    {
        private readonly DatasetInterchangePreflightService preflightService;
        private bool disposed;
        private LabelingProjectData data;
        private string recipeName = string.Empty;
        private WpfDatasetInterchangeOption selectedOperation;
        private string sourcePath = string.Empty;
        private string imageRoot = string.Empty;
        private string targetPath = string.Empty;
        private string targetSplit = YoloDatasetSplitService.TrainMode;
        private string datasetName = "\uB370\uC774\uD130\uC14B \uBBF8\uC120\uD0DD";
        private string datasetPurposeText = "\uBAA9\uC801 \uBBF8\uD655\uC778";
        private string statusText = "\uC0AC\uC804\uAC80\uC0AC \uB300\uAE30";
        private string statusDetailText = "\uD615\uC2DD\uACFC \uACBD\uB85C\uB97C \uC120\uD0DD\uD55C \uB4A4 Dry-run\uC744 \uC2E4\uD589\uD558\uC138\uC694.";
        private string metricText = "\uC774\uBBF8\uC9C0 - \u00B7 \uC5B4\uB178\uD14C\uC774\uC158 - \u00B7 \uD074\uB798\uC2A4 -";
        private string sourceIntegrityText = "\uC6D0\uBCF8 \uBB34\uACB0\uC131: \uBBF8\uD655\uC778";
        private string targetIntegrityText = "\uC694\uCCAD \uB300\uC0C1: \uBBF8\uD655\uC778";
        private string lastDryRunSignature = string.Empty;
        private bool canApply;
        private Func<WpfDatasetInterchangeOption, string, string> pickSource =
            (_, current) => current;
        private Func<WpfDatasetInterchangeOption, string, string> pickTarget =
            (_, current) => current;
        private Func<string, string> pickImageRoot = current => current;

        public WpfDatasetInterchangeViewModel(
            LabelingProjectData data = null,
            DatasetInterchangePreflightService preflightService = null,
            string recipeName = "")
        {
            this.preflightService = preflightService ?? new DatasetInterchangePreflightService();
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            foreach (DatasetExportCapability capability in this.preflightService.BuildSupportedCapabilities())
            {
                Operations.Add(new WpfDatasetInterchangeOption(capability));
            }

            Splits.Add(YoloDatasetSplitService.TrainMode);
            Splits.Add(YoloDatasetSplitService.ValidMode);
            Splits.Add(YoloDatasetSplitService.TestMode);
            BrowseSourceCommand = new RelayCommand(BrowseSource);
            BrowseTargetCommand = new RelayCommand(BrowseTarget);
            BrowseImageRootCommand = new RelayCommand(BrowseImageRoot);
            DryRunCommand = new RelayCommand(RunDryRun);
            ApplyCommand = new RelayCommand(Apply, () => CanApply);
            Refresh(data, recipeName);
        }

        public ObservableCollection<WpfDatasetInterchangeOption> Operations { get; } =
            new ObservableCollection<WpfDatasetInterchangeOption>();

        public ObservableCollection<string> Splits { get; } = new ObservableCollection<string>();

        public ObservableCollection<WpfDatasetInterchangeIssueItem> Findings { get; } =
            new ObservableCollection<WpfDatasetInterchangeIssueItem>();

        public WpfDatasetInterchangeOption SelectedOperation
        {
            get => selectedOperation;
            set
            {
                if (SetProperty(ref selectedOperation, value))
                {
                    ResetPathsForSelection();
                    OnPropertyChanged(nameof(IsImport));
                    OnPropertyChanged(nameof(IsExport));
                    OnPropertyChanged(nameof(RequiresImageRoot));
                    OnPropertyChanged(nameof(SourceLabelText));
                    OnPropertyChanged(nameof(TargetLabelText));
                    OnPropertyChanged(nameof(OperationContractText));
                }
            }
        }

        public bool IsImport => SelectedOperation?.IsImport == true;

        public bool IsExport => !IsImport;

        public bool RequiresImageRoot => SelectedOperation?.RequiresImageRoot == true;

        public string SourceLabelText => T(IsImport
            ? "WpfDatasetInterchange.SourceLabel.ExternalAnnotation"
            : "WpfDatasetInterchange.SourceLabel.CurrentDataset");

        public string TargetLabelText => T(IsImport
            ? "WpfDatasetInterchange.TargetLabel.Import"
            : "WpfDatasetInterchange.TargetLabel.Export");

        public string OperationContractText => SelectedOperation == null
            ? T("WpfDatasetInterchange.Operation.ContractPrompt")
            : Format(
                "WpfDatasetInterchange.Operation.Contract",
                SelectedOperation.PurposeText,
                SelectedOperation.DirectionText,
                IsImport
                    ? T("WpfDatasetInterchange.Operation.ImportContract")
                    : T("WpfDatasetInterchange.Operation.ExportContract"));

        public string SourcePath
        {
            get => sourcePath;
            set
            {
                if (SetProperty(ref sourcePath, value ?? string.Empty))
                {
                    InvalidateDryRun();
                }
            }
        }

        public string ImageRoot
        {
            get => imageRoot;
            set
            {
                if (SetProperty(ref imageRoot, value ?? string.Empty))
                {
                    InvalidateDryRun();
                }
            }
        }

        public string TargetPath
        {
            get => targetPath;
            set
            {
                if (SetProperty(ref targetPath, value ?? string.Empty))
                {
                    InvalidateDryRun();
                }
            }
        }

        public string TargetSplit
        {
            get => targetSplit;
            set
            {
                if (SetProperty(ref targetSplit, value ?? YoloDatasetSplitService.TrainMode))
                {
                    InvalidateDryRun();
                }
            }
        }

        public string DatasetName
        {
            get => datasetName;
            private set => SetProperty(ref datasetName, value ?? string.Empty);
        }

        public string DatasetPurposeText
        {
            get => datasetPurposeText;
            private set => SetProperty(ref datasetPurposeText, value ?? string.Empty);
        }

        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public string StatusDetailText
        {
            get => statusDetailText;
            private set => SetProperty(ref statusDetailText, value ?? string.Empty);
        }

        public string MetricText
        {
            get => metricText;
            private set => SetProperty(ref metricText, value ?? string.Empty);
        }

        public string SourceIntegrityText
        {
            get => sourceIntegrityText;
            private set => SetProperty(ref sourceIntegrityText, value ?? string.Empty);
        }

        public string TargetIntegrityText
        {
            get => targetIntegrityText;
            private set => SetProperty(ref targetIntegrityText, value ?? string.Empty);
        }

        public bool CanApply
        {
            get => canApply;
            private set => SetProperty(ref canApply, value);
        }

        public ICommand BrowseSourceCommand { get; }

        public ICommand BrowseTargetCommand { get; }

        public ICommand BrowseImageRootCommand { get; }

        public ICommand DryRunCommand { get; }

        public ICommand ApplyCommand { get; }

        public void ConfigurePickers(
            Func<WpfDatasetInterchangeOption, string, string> sourcePicker,
            Func<WpfDatasetInterchangeOption, string, string> targetPicker,
            Func<string, string> imageRootPicker)
        {
            pickSource = sourcePicker ?? ((_, current) => current);
            pickTarget = targetPicker ?? ((_, current) => current);
            pickImageRoot = imageRootPicker ?? (current => current);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
            foreach (WpfDatasetInterchangeOption operation in Operations.ToArray())
            {
                operation.Dispose();
            }

            Operations.Clear();
            Findings.Clear();
        }

        public void Refresh(LabelingProjectData sourceData, string currentRecipeName = null)
        {
            if (disposed)
            {
                return;
            }

            data = sourceData;
            if (currentRecipeName != null)
            {
                recipeName = currentRecipeName;
            }
            DatasetName = DatasetContextPresentationService.BuildDatasetName(
                string.Empty,
                data?.OutputRootPath);
            LabelingDatasetPurpose purpose = data?.ProjectSettings?.DatasetPurpose
                ?? LabelingDatasetPurpose.ObjectDetection;
            DatasetPurposeText = DatasetContextPresentationService.FormatPurposeName(purpose);
            WpfDatasetInterchangeOption preferred = Operations.FirstOrDefault(item =>
                !item.IsImport
                && string.Equals(item.Capability.DatasetPurpose, purpose.ToString(), StringComparison.Ordinal))
                ?? Operations.FirstOrDefault();
            SelectedOperation = preferred;
            InvalidateDryRun();
        }

        private void BrowseSource()
        {
            string selected = pickSource(SelectedOperation, SourcePath);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                SourcePath = selected;
            }
        }

        private void BrowseTarget()
        {
            string selected = pickTarget(SelectedOperation, TargetPath);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                TargetPath = selected;
            }
        }

        private void BrowseImageRoot()
        {
            string selected = pickImageRoot(ImageRoot);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                ImageRoot = selected;
            }
        }

        private void RunDryRun()
        {
            DatasetInterchangePreflightReport report = preflightService.DryRun(BuildRequest());
            lastDryRunSignature = BuildRequestSignature();
            ApplyReport(report);
            CanApply = report.CanApply;
        }

        private void Apply()
        {
            if (!CanApply || !string.Equals(lastDryRunSignature, BuildRequestSignature(), StringComparison.Ordinal))
            {
                InvalidateDryRun();
                return;
            }

            DatasetInterchangePreflightReport report = preflightService.Apply(BuildRequest());
            ApplyReport(report);
            CanApply = false;
        }

        private DatasetInterchangeRequest BuildRequest()
            => new DatasetInterchangeRequest
            {
                Data = data,
                RecipeName = recipeName,
                FormatKey = SelectedOperation?.Capability.FormatKey ?? string.Empty,
                SourcePath = SourcePath,
                ImageRoot = ImageRoot,
                TargetPath = TargetPath,
                TargetSplit = TargetSplit
            };

        private string BuildRequestSignature()
            => string.Join(
                "|",
                SelectedOperation?.Capability.FormatKey ?? string.Empty,
                SourcePath,
                ImageRoot,
                TargetPath,
                TargetSplit,
                recipeName,
                data?.OutputRootPath ?? string.Empty);

        private void ResetPathsForSelection()
        {
            string datasetRoot = data?.OutputRootPath ?? string.Empty;
            if (IsImport)
            {
                sourcePath = string.Empty;
                imageRoot = string.Empty;
                targetPath = datasetRoot;
            }
            else
            {
                sourcePath = datasetRoot;
                imageRoot = string.Empty;
                targetPath = string.Empty;
            }

            OnPropertyChanged(nameof(SourcePath));
            OnPropertyChanged(nameof(ImageRoot));
            OnPropertyChanged(nameof(TargetPath));
            InvalidateDryRun();
        }

        private void InvalidateDryRun()
        {
            lastDryRunSignature = string.Empty;
            CanApply = false;
            Findings.Clear();
            StatusText = T("WpfDatasetInterchange.Status.Waiting");
            StatusDetailText = T("WpfDatasetInterchange.Status.Detail");
            MetricText = T("WpfDatasetInterchange.Metric.Empty");
            SourceIntegrityText = T("WpfDatasetInterchange.Integrity.SourceUnknown");
            TargetIntegrityText = T("WpfDatasetInterchange.Integrity.TargetUnknown");
        }

        private void ApplyReport(DatasetInterchangePreflightReport report)
        {
            Findings.Clear();
            foreach (string issue in report.Issues)
            {
                Findings.Add(new WpfDatasetInterchangeIssueItem(LocalizeInterchangeText(issue), isBlocking: true));
            }

            foreach (string warning in report.Warnings)
            {
                Findings.Add(new WpfDatasetInterchangeIssueItem(LocalizeInterchangeText(warning), isBlocking: false));
            }

            StatusText = TranslateStatus(report);
            StatusDetailText = LocalizeInterchangeText(report.DetailText);
            MetricText = Format(
                "WpfDatasetInterchange.Metric.Counts",
                report.ImageCount,
                report.AnnotationCount,
                report.CategoryCount);
            SourceIntegrityText = report.SourceUnchanged
                ? Format(
                    "WpfDatasetInterchange.Integrity.SourceKept",
                    ShortFingerprint(report.SourceFingerprint))
                : T("WpfDatasetInterchange.Integrity.SourceChanged");
            TargetIntegrityText = report.IsDryRun
                ? report.RequestedTargetUnchanged
                    ? T("WpfDatasetInterchange.Integrity.TargetDryRunKept")
                    : T("WpfDatasetInterchange.Integrity.TargetChanged")
                : T("WpfDatasetInterchange.Integrity.TargetApplied");
        }

        private static string TranslateStatus(DatasetInterchangePreflightReport report)
        {
            if (report.Issues.Count > 0)
            {
                return report.IsDryRun
                    ? T("WpfDatasetInterchange.Status.DryRunBlocked")
                    : T("WpfDatasetInterchange.Status.ApplyFailed");
            }

            if (!report.IsDryRun)
            {
                return T("WpfDatasetInterchange.Status.Applied");
            }

            return report.Warnings.Count > 0
                ? T("WpfDatasetInterchange.Status.ReadyWithWarnings")
                : T("WpfDatasetInterchange.Status.Ready");
        }

        private static string ShortFingerprint(string value)
            => string.IsNullOrWhiteSpace(value) || value.Length <= 12
                ? value
                : value.Substring(0, 12);

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(SourceLabelText));
            OnPropertyChanged(nameof(TargetLabelText));
            OnPropertyChanged(nameof(OperationContractText));
            DatasetPurposeText = DatasetContextPresentationService.FormatPurposeName(
                data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            InvalidateDryRun();
        }

        private static string T(string key)
            => DatasetInterchangeTextFormatter.Translate(key);

        private static string Format(string key, params object[] arguments)
            => DatasetInterchangeTextFormatter.Format(key, arguments);

        private static string LocalizeInterchangeText(string value)
            => LocalizationTextRuntimeService.Translate(value ?? string.Empty);
    }
}
