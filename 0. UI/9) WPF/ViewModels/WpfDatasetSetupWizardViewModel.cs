using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSetupRequest
    {
        public LabelingDatasetPurpose Purpose { get; set; } = LabelingDatasetPurpose.ObjectDetection;

        public string RecipeName { get; set; } = string.Empty;

        public string OutputRootPath { get; set; } = string.Empty;

        public IReadOnlyList<string> ClassNames { get; set; } = Array.Empty<string>();

        public WpfDatasetSamplePresetKind SamplePresetKind { get; set; } = WpfDatasetSamplePresetKind.Empty;

        public string SampleSourcePath { get; set; } = string.Empty;
    }

    public sealed class WpfDatasetSetupWizardViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private WpfLearningModeItem selectedDatasetPurposeMode;
        private string recipeName = string.Empty;
        private string outputRootPath = string.Empty;
        private string classNamesText = "Defect";
        private string previewText = string.Empty;
        private string statusText = string.Empty;
        private WpfDatasetSamplePresetItem selectedSamplePreset;
        private ICommand createCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand cancelCommand = new RelayCommand(NoOpCommand);
        private ICommand browseOutputRootCommand = new RelayCommand(NoOpCommand);

        public WpfDatasetSetupWizardViewModel()
        {
            DatasetPurposeModes.Add(new WpfLearningModeItem(WpfLearningMode.ObjectDetection, "\uAC1D\uCCB4 \uD0D0\uC9C0", PackIconMaterialKind.ShapeSquareRoundedPlus, "YOLO \uBC15\uC2A4 \uB77C\uBCA8"));
            DatasetPurposeModes.Add(new WpfLearningModeItem(WpfLearningMode.Segmentation, "\uC138\uADF8\uBA58\uD14C\uC774\uC158", PackIconMaterialKind.ViewListOutline, "\uD3F4\uB9AC\uACE4\uACFC \uB9C8\uC2A4\uD06C \uB77C\uBCA8"));
            DatasetPurposeModes.Add(new WpfLearningModeItem(WpfLearningMode.AnomalyDetection, "\uC774\uC0C1 \uD0D0\uC9C0", PackIconMaterialKind.AlertCircleOutline, "\uC815\uC0C1/\uC774\uC0C1 \uC0D8\uD50C\uACFC \uC601\uC5ED"));
            SelectedDatasetPurposeMode = DatasetPurposeModes.FirstOrDefault();
            RefreshPreview();
        }

        public string ViewName => nameof(WpfDatasetSetupWizardWindow);

        public ObservableCollection<WpfLearningModeItem> DatasetPurposeModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfDatasetSamplePresetItem> SamplePresets { get; } = new ObservableCollection<WpfDatasetSamplePresetItem>();

        public WpfLearningModeItem SelectedDatasetPurposeMode
        {
            get => selectedDatasetPurposeMode;
            set
            {
                if (SetProperty(ref selectedDatasetPurposeMode, value))
                {
                    RefreshSamplePresets();
                    RefreshPreview();
                }
            }
        }

        public WpfDatasetSamplePresetItem SelectedSamplePreset
        {
            get => selectedSamplePreset;
            set
            {
                if (SetProperty(ref selectedSamplePreset, value))
                {
                    ApplySamplePresetDefaults(value);
                    RefreshPreview();
                }
            }
        }

        public string RecipeName
        {
            get => recipeName;
            set
            {
                if (SetProperty(ref recipeName, value ?? string.Empty))
                {
                    RefreshPreview();
                }
            }
        }

        public string OutputRootPath
        {
            get => outputRootPath;
            set
            {
                if (SetProperty(ref outputRootPath, value ?? string.Empty))
                {
                    RefreshPreview();
                }
            }
        }

        public string ClassNamesText
        {
            get => classNamesText;
            set
            {
                if (SetProperty(ref classNamesText, value ?? string.Empty))
                {
                    RefreshPreview();
                }
            }
        }

        public string PreviewText
        {
            get => previewText;
            private set => SetProperty(ref previewText, value ?? string.Empty);
        }

        public string StatusText
        {
            get => statusText;
            set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public ICommand CreateCommand
        {
            get => createCommand;
            private set => SetProperty(ref createCommand, value);
        }

        public ICommand CancelCommand
        {
            get => cancelCommand;
            private set => SetProperty(ref cancelCommand, value);
        }

        public ICommand BrowseOutputRootCommand
        {
            get => browseOutputRootCommand;
            private set => SetProperty(ref browseOutputRootCommand, value);
        }

        public void ConfigureCommands(Action<object> create, Action cancel, Action browseOutputRoot)
        {
            // The dialog owns no persistence. Commands are supplied by the shell composition root.
            CreateCommand = new RelayCommand<object>(create ?? NoOpSelectionCommand);
            CancelCommand = new RelayCommand(cancel ?? NoOpCommand);
            BrowseOutputRootCommand = new RelayCommand(browseOutputRoot ?? NoOpCommand);
        }

        public void LoadFrom(LabelingDatasetPurpose purpose, string recipeName, string outputRootPath, IEnumerable<string> classNames)
        {
            WpfLearningMode mode = WpfLearningWorkflowPanelViewModel.ToLearningMode(purpose);
            SelectedDatasetPurposeMode = DatasetPurposeModes.FirstOrDefault(item => item.Mode == mode)
                ?? DatasetPurposeModes.FirstOrDefault();
            RecipeName = recipeName ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            ClassNamesText = string.Join(Environment.NewLine, NormalizeClassNames(classNames).DefaultIfEmpty("Defect"));
            StatusText = "\uC0DD\uC131 \uC804\uC5D0 \uBAA9\uC801, recipe, \uC800\uC7A5 \uACBD\uB85C, \uD074\uB798\uC2A4\uB97C \uD655\uC778\uD558\uC138\uC694.";
            RefreshPreview();
        }

        public bool TryBuildRequest(out WpfDatasetSetupRequest request, out string error)
            => TryBuildRequest(SelectedDatasetPurposeMode, out request, out error);

        public bool TryBuildRequest(object selectedPurpose, out WpfDatasetSetupRequest request, out string error)
        {
            request = null;
            error = string.Empty;

            string normalizedRecipeName = (RecipeName ?? string.Empty).Trim();
            if (!WpfProjectRecipeService.IsValidRecipeName(normalizedRecipeName))
            {
                error = "Recipe \uC774\uB984\uC5D0 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            string normalizedOutputRoot = (OutputRootPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedOutputRoot))
            {
                error = "\uB370\uC774\uD130\uC14B \uC800\uC7A5 \uACBD\uB85C\uB97C \uC785\uB825\uD558\uC138\uC694.";
                return false;
            }

            IReadOnlyList<string> classNames = ParseClassNames(ClassNamesText);
            if (classNames.Count == 0)
            {
                error = "\uCD5C\uC18C 1\uAC1C \uC774\uC0C1\uC758 \uD074\uB798\uC2A4\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4.";
                return false;
            }

            WpfLearningModeItem selectedPurposeItem = selectedPurpose as WpfLearningModeItem
                ?? SelectedDatasetPurposeMode;
            request = new WpfDatasetSetupRequest
            {
                Purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(selectedPurposeItem?.Mode ?? WpfLearningMode.ObjectDetection),
                RecipeName = normalizedRecipeName,
                OutputRootPath = normalizedOutputRoot,
                ClassNames = classNames
            };
            WpfDatasetSamplePresetItem samplePreset = SelectedSamplePreset
                ?? WpfDatasetSamplePresetService.CreateEmptyPreset(request.Purpose);
            if (!samplePreset.IsAvailable)
            {
                error = $"{samplePreset.Text}: {samplePreset.AvailabilityText}";
                return false;
            }

            request.SamplePresetKind = samplePreset.Kind;
            request.SampleSourcePath = samplePreset.ImageSourcePath;
            return true;
        }

        public static IReadOnlyList<string> ParseClassNames(string text)
            => NormalizeClassNames((text ?? string.Empty)
                .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries));

        private static IReadOnlyList<string> NormalizeClassNames(IEnumerable<string> classNames)
        {
            List<string> normalized = new List<string>();
            foreach (string className in classNames ?? Array.Empty<string>())
            {
                string item = ClassCatalogService.NormalizeClassName(className);
                if (!string.IsNullOrWhiteSpace(item)
                    && !normalized.Any(existing => string.Equals(existing, item, StringComparison.OrdinalIgnoreCase)))
                {
                    normalized.Add(item);
                }
            }

            return normalized;
        }

        private void RefreshPreview()
        {
            LabelingDatasetPurpose purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);
            IReadOnlyList<string> classNames = ParseClassNames(ClassNamesText);
            string classSummary = classNames.Count == 0 ? "-" : string.Join(", ", classNames);
            string outputName = string.IsNullOrWhiteSpace(OutputRootPath) ? "-" : Path.GetFileName(OutputRootPath.Trim());
            string sampleSummary = SelectedSamplePreset == null ? "-" : SelectedSamplePreset.Text;
            PreviewText = string.Format(
                CultureInfo.InvariantCulture,
                "Purpose: {0} / Sample: {1} / Recipe: {2} / Output: {3} / Classes: {4}",
                purpose,
                sampleSummary,
                string.IsNullOrWhiteSpace(RecipeName) ? "-" : RecipeName.Trim(),
                string.IsNullOrWhiteSpace(outputName) ? OutputRootPath : outputName,
                classSummary);
        }

        private void RefreshSamplePresets()
        {
            WpfDatasetSamplePresetKind previousKind = SelectedSamplePreset?.Kind ?? WpfDatasetSamplePresetKind.Empty;
            LabelingDatasetPurpose purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);

            SamplePresets.Clear();
            foreach (WpfDatasetSamplePresetItem preset in WpfDatasetSamplePresetService.BuildPresets(purpose))
            {
                SamplePresets.Add(preset);
            }

            SelectedSamplePreset = SamplePresets.FirstOrDefault(item => item.Kind == previousKind && item.IsAvailable)
                ?? SamplePresets.FirstOrDefault(item => item.Kind == WpfDatasetSamplePresetKind.Empty)
                ?? SamplePresets.FirstOrDefault();
        }

        private void ApplySamplePresetDefaults(WpfDatasetSamplePresetItem samplePreset)
        {
            if (samplePreset == null || samplePreset.ClassNames.Count == 0)
            {
                return;
            }

            ClassNamesText = string.Join(Environment.NewLine, samplePreset.ClassNames);
        }
    }

    public enum WpfDatasetSamplePresetKind
    {
        Empty,
        Coco128ObjectDetection,
        IndustrialObjectDetectionImages,
        IndustrialDefectMasks
    }

    public sealed class WpfDatasetSamplePresetItem
    {
        public WpfDatasetSamplePresetItem(
            WpfDatasetSamplePresetKind kind,
            LabelingDatasetPurpose purpose,
            string text,
            string toolTip,
            string imageSourcePath,
            string labelSourcePath,
            IReadOnlyList<string> classNames,
            bool isAvailable,
            string availabilityText)
        {
            Kind = kind;
            Purpose = purpose;
            Text = text ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            ImageSourcePath = imageSourcePath ?? string.Empty;
            LabelSourcePath = labelSourcePath ?? string.Empty;
            ClassNames = classNames ?? Array.Empty<string>();
            IsAvailable = isAvailable;
            AvailabilityText = availabilityText ?? string.Empty;
        }

        public WpfDatasetSamplePresetKind Kind { get; }

        public LabelingDatasetPurpose Purpose { get; }

        public string Text { get; }

        public string ToolTip { get; }

        public string ImageSourcePath { get; }

        public string LabelSourcePath { get; }

        public IReadOnlyList<string> ClassNames { get; }

        public bool IsAvailable { get; }

        public string AvailabilityText { get; }
    }
}
