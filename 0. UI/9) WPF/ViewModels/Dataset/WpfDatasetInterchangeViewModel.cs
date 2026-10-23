using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
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

        public static string TranslateWarning(string value)
        {
            string warning = value ?? string.Empty;
            const string affectedPrefix = " (영향 ";
            int marker = warning.LastIndexOf(affectedPrefix, StringComparison.Ordinal);
            if (marker < 0 || !warning.EndsWith("건)", StringComparison.Ordinal)) return LocalizationTextRuntimeService.Translate(warning);

            string countText = warning.Substring(marker + affectedPrefix.Length, warning.Length - marker - affectedPrefix.Length - 2);
            if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count)) return LocalizationTextRuntimeService.Translate(warning);
            string detail = warning.Substring(0, marker);
            string translatedDetail = detail switch
            {
                "ObjectMetadataTags: Lost - 현재 외부 exporter는 객체 tag를 기록하지 않습니다." => "ObjectMetadataTags: Lost - " + Translate("WpfDatasetInterchange.Loss.ObjectTags"),
                "ObjectMetadataGroups: Lost - 현재 외부 exporter는 객체 group id를 기록하지 않습니다." => "ObjectMetadataGroups: Lost - " + Translate("WpfDatasetInterchange.Loss.ObjectGroups"),
                "ObjectMetadataOcclusion: Lost - 현재 외부 exporter는 객체 occlusion 상태를 기록하지 않습니다." => "ObjectMetadataOcclusion: Lost - " + Translate("WpfDatasetInterchange.Loss.ObjectOcclusion"),
                _ => LocalizationTextRuntimeService.Translate(detail)
            };
            return Format("WpfDatasetInterchange.LossWarning.AffectedCount", translatedDetail, count);
        }
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

        public string FormatText => Capability.FormatKey switch
        {
            string key when key.StartsWith("coco-", StringComparison.Ordinal) => "COCO (json)",
            string key when key.StartsWith("pascal-voc-", StringComparison.Ordinal) => "Pascal VOC (xml)",
            string key when key.StartsWith("label-studio-", StringComparison.Ordinal) => "Label Studio (json)",
            string key when key.StartsWith("cvat-", StringComparison.Ordinal) => "CVAT (xml/zip)",
            _ => Capability.DisplayName
        };

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
        private bool isDetailsExpanded;
        private LabelingProjectData data;
        private string recipeName = string.Empty;
        private WpfDatasetInterchangeOption selectedOperation;
        private string sourcePath = string.Empty;
        private string imageRoot = string.Empty;
        private string targetPath = string.Empty;
        private string targetSplit = YoloDatasetSplitService.TrainMode;
        private string datasetName = "\uB370\uC774\uD130\uC14B \uBBF8\uC120\uD0DD";
        private string datasetPurposeText = "\uBAA9\uC801 \uBBF8\uD655\uC778";
        private string statusText = T("WpfDatasetInterchange.Status.Waiting");
        private string statusDetailText = T("WpfDatasetInterchange.Status.Detail");
        private string metricText = T("WpfDatasetInterchange.Metric.Empty");
        private string sourceIntegrityText = T("WpfDatasetInterchange.Integrity.SourceUnknown");
        private string targetIntegrityText = "\uC694\uCCAD \uB300\uC0C1: \uBBF8\uD655\uC778";
        private string lastDryRunSignature = string.Empty;
        private bool canApply;
        private bool confirmLossyExport;
        private bool requiresLossWarningConfirmation;
        private string lossManifestText = string.Empty;
        private DatasetInterchangePreflightReport lastDryRunReport;
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
            ToggleDetailsCommand = new RelayCommand(() => IsDetailsExpanded = !IsDetailsExpanded);
            ApplyCommand = new RelayCommand(Apply, () => CanApply);
            Refresh(data, recipeName);
        }

        public WpfLocalizationViewModel LanguageViewModel => WpfLocalizationViewModel.Instance;

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

        public bool IsDetailsExpanded
        {
            get => isDetailsExpanded;
            set
            {
                if (SetProperty(ref isDetailsExpanded, value)) OnPropertyChanged(nameof(DetailsToggleText));
            }
        }

        public string DetailsToggleText => T(IsDetailsExpanded ? "WpfDatasetInterchange.Action.ShowSummary" : "WpfDatasetInterchange.Action.ShowDetails");

        public ICommand ToggleDetailsCommand { get; }

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
            private set
            {
                if (SetProperty(ref canApply, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool ConfirmLossyExport
        {
            get => confirmLossyExport;
            set
            {
                if (!SetProperty(ref confirmLossyExport, value))
                {
                    return;
                }

                if (lastDryRunReport != null
                    && string.Equals(lastDryRunSignature, BuildRequestSignature(), StringComparison.Ordinal))
                {
                    CanApply = CanApplyFromLastDryRun();
                    StatusText = value && lastDryRunReport.RequiresLossConfirmation
                        ? T("WpfDatasetInterchange.Status.ReadyWithWarnings")
                        : TranslateStatus(lastDryRunReport);
                }
            }
        }

        public bool RequiresLossWarningConfirmation
        {
            get => requiresLossWarningConfirmation;
            private set => SetProperty(ref requiresLossWarningConfirmation, value);
        }

        public string LossWarningConfirmationText =>
            T("WpfDatasetInterchange.LossWarning.Confirm");

        public string LossManifestText
        {
            get => lossManifestText;
            private set => SetProperty(ref lossManifestText, value ?? string.Empty);
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

            bool contextChanged = !ReferenceEquals(data, sourceData)
                || !string.Equals(IsImport ? TargetPath : SourcePath, sourceData?.OutputRootPath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || (currentRecipeName != null && !string.Equals(recipeName, currentRecipeName, StringComparison.Ordinal));
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
            WpfDatasetInterchangeOption preferred = SelectedOperation != null
                && string.Equals(SelectedOperation.Capability.DatasetPurpose, purpose.ToString(), StringComparison.Ordinal)
                ? SelectedOperation
                : Operations.FirstOrDefault(item => !item.IsImport && string.Equals(item.Capability.DatasetPurpose, purpose.ToString(), StringComparison.Ordinal))
                    ?? Operations.FirstOrDefault();
            if (ReferenceEquals(SelectedOperation, preferred))
            {
                if (contextChanged) ResetPathsForSelection();
            }
            else
            {
                SelectedOperation = preferred;
            }
            InvalidateDryRun();
        }

        public void SelectDirection(bool isImport)
        {
            if (disposed || SelectedOperation?.IsImport == isImport) return;
            string purpose = (data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection).ToString();
            SelectedOperation = Operations.FirstOrDefault(item => item.IsImport == isImport && string.Equals(item.Capability.DatasetPurpose, purpose, StringComparison.Ordinal))
                ?? Operations.FirstOrDefault(item => item.IsImport == isImport);
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
            IsDetailsExpanded = true;
            DatasetInterchangePreflightReport report = preflightService.DryRun(BuildRequest());
            lastDryRunSignature = BuildRequestSignature();
            lastDryRunReport = report;
            RequiresLossWarningConfirmation = IsLossExportReport(report) && report.RequiresLossConfirmation;
            LossManifestText = string.IsNullOrWhiteSpace(report.LossManifestPath)
                ? string.Empty
                : Format("WpfDatasetInterchange.LossWarning.Manifest", report.LossManifestPath);
            ApplyReport(report);
            CanApply = CanApplyFromLastDryRun();
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
            lastDryRunReport = null;
            RequiresLossWarningConfirmation = false;
            LossManifestText = string.Empty;
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
                TargetSplit = TargetSplit,
                ConfirmLossyExport = ConfirmLossyExport
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
            lastDryRunReport = null;
            if (confirmLossyExport)
            {
                confirmLossyExport = false;
                OnPropertyChanged(nameof(ConfirmLossyExport));
            }

            RequiresLossWarningConfirmation = false;
            LossManifestText = string.Empty;
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
                Findings.Add(new WpfDatasetInterchangeIssueItem(DatasetInterchangeTextFormatter.TranslateWarning(warning), isBlocking: false));
            }

            StatusText = TranslateStatus(report);
            StatusDetailText = report.Issues.Count > 0
                ? string.Join(" ", Findings.Where(item => item.IsBlocking).Select(item => item.Text))
                : report.IsDryRun ? T("WpfDatasetInterchange.Status.DryRunImpact") : string.Empty;
            MetricText = Format(
                "WpfDatasetInterchange.Metric.Counts",
                report.ImageCount,
                report.AnnotationCount,
                report.CategoryCount);
            SourceIntegrityText = string.IsNullOrWhiteSpace(report.SourceFingerprint)
                ? T("WpfDatasetInterchange.Integrity.SourceUnknown")
                : report.SourceUnchanged
                    ? Format("WpfDatasetInterchange.Integrity.SourceKept", ShortFingerprint(report.SourceFingerprint))
                    : T("WpfDatasetInterchange.Integrity.SourceChanged");
            TargetIntegrityText = report.WasApplied
                ? T("WpfDatasetInterchange.Integrity.TargetApplied")
                : string.IsNullOrWhiteSpace(report.RequestedTargetFingerprint)
                    ? T("WpfDatasetInterchange.Integrity.TargetUnknown")
                    : report.RequestedTargetUnchanged
                        ? T(report.IsDryRun ? "WpfDatasetInterchange.Integrity.TargetDryRunKept" : "WpfDatasetInterchange.Integrity.TargetKept")
                        : T("WpfDatasetInterchange.Integrity.TargetChanged");
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
                ? report.RequiresLossConfirmation && !report.LossConfirmationAccepted
                    ? T("WpfDatasetInterchange.Status.LossConfirmationRequired")
                    : T("WpfDatasetInterchange.Status.ReadyWithWarnings")
                : T("WpfDatasetInterchange.Status.Ready");
        }

        private bool CanApplyFromLastDryRun()
        {
            if (lastDryRunReport == null
                || !lastDryRunReport.IsDryRun
                || lastDryRunReport.Issues.Count > 0
                || !lastDryRunReport.SourceUnchanged
                || !lastDryRunReport.RequestedTargetUnchanged)
            {
                return false;
            }

            return lastDryRunReport.CanApply
                || (IsLossExportReport(lastDryRunReport)
                    && lastDryRunReport.RequiresLossConfirmation
                    && ConfirmLossyExport);
        }

        private static bool IsLossExportReport(DatasetInterchangePreflightReport report)
            => string.Equals(
                report?.Capability?.Direction,
                "export",
                StringComparison.OrdinalIgnoreCase);

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
            OnPropertyChanged(nameof(LossWarningConfirmationText));
            OnPropertyChanged(nameof(DetailsToggleText));
            DatasetPurposeText = DatasetContextPresentationService.FormatPurposeName(
                data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            InvalidateDryRun();
        }

        private static string T(string key)
            => DatasetInterchangeTextFormatter.Translate(key);

        private static string Format(string key, params object[] arguments)
            => DatasetInterchangeTextFormatter.Format(key, arguments);

        private static string LocalizeInterchangeText(string value)
        {
            string text = value ?? string.Empty;
            const string purposePrefix = "선택한 작업은 ";
            const string purposeSeparator = " 목적이 필요하지만 현재 데이터셋 목적은 ";
            const string purposeSuffix = "입니다.";
            int purposeMarker = text.IndexOf(purposeSeparator, StringComparison.Ordinal);
            if (text.StartsWith(purposePrefix, StringComparison.Ordinal) && text.EndsWith(purposeSuffix, StringComparison.Ordinal) && purposeMarker > purposePrefix.Length)
            {
                string requiredText = text.Substring(purposePrefix.Length, purposeMarker - purposePrefix.Length);
                string currentText = text.Substring(purposeMarker + purposeSeparator.Length, text.Length - purposeMarker - purposeSeparator.Length - purposeSuffix.Length);
                if (Enum.TryParse(requiredText, out LabelingDatasetPurpose requiredPurpose) && Enum.IsDefined(typeof(LabelingDatasetPurpose), requiredPurpose)
                    && requiredPurpose.ToString() == requiredText && Enum.TryParse(currentText, out LabelingDatasetPurpose currentPurpose)
                    && Enum.IsDefined(typeof(LabelingDatasetPurpose), currentPurpose) && currentPurpose.ToString() == currentText)
                    return Format("WpfDatasetInterchange.Error.PurposeMismatch", DatasetContextPresentationService.FormatPurposeName(requiredPurpose), DatasetContextPresentationService.FormatPurposeName(currentPurpose));
            }

            const string lossReportPrefix = "내보내기 보존 목록을 만들지 못했습니다: ";
            if (text.StartsWith(lossReportPrefix, StringComparison.Ordinal))
                return Format("WpfDatasetInterchange.Error.LossReportFailed", text.Substring(lossReportPrefix.Length));

            const string skippedPrefix = "잘못되었거나 지원하지 않는 이미지·라벨 ";
            const string skippedSuffix = "건을 건너뛰었습니다. 적용 전에 수정하세요.";
            if (text.StartsWith(skippedPrefix, StringComparison.Ordinal) && text.EndsWith(skippedSuffix, StringComparison.Ordinal))
            {
                string countText = text.Substring(skippedPrefix.Length, text.Length - skippedPrefix.Length - skippedSuffix.Length);
                if (int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
                    return Format("WpfDatasetInterchange.Error.InvalidSkipped", count);
            }

            return LocalizationTextRuntimeService.Translate(text);
        }
    }
}
