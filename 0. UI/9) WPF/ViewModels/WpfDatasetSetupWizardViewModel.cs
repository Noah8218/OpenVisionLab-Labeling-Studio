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
    public sealed class WpfDatasetSetupWizardViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private WpfLearningModeItem selectedDatasetPurposeMode;
        private string recipeName = string.Empty;
        private string outputRootPath = string.Empty;
        private string classNamesText = "Defect";
        private string classSummaryText = string.Empty;
        private string storageHelpText = string.Empty;
        private string imageSourcePreviewText = string.Empty;
        private string isolationHelpText = string.Empty;
        private string previewText = string.Empty;
        private string statusText = string.Empty;
        private WpfDatasetSamplePresetItem selectedSamplePreset;
        private ICommand createCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand cancelCommand = new RelayCommand(NoOpCommand);
        private ICommand browseOutputRootCommand = new RelayCommand(NoOpCommand);
        private Func<LabelingDatasetPurpose, string> automaticRecipeNameResolver;
        private Func<string, string> automaticOutputRootResolver;
        private string automaticRecipeName = string.Empty;
        private string automaticOutputRootPath = string.Empty;
        private bool automaticPathSyncEnabled;
        private bool isApplyingAutomaticPathSync;
        private bool recipeNameWasEdited;
        private bool outputRootPathWasEdited;

        public WpfDatasetSetupWizardViewModel()
        {
            DatasetPurposeModes.Add(new WpfLearningModeItem(WpfLearningMode.ObjectDetection, "\uAC1D\uCCB4 \uD0D0\uC9C0", PackIconMaterialKind.ShapeSquareRoundedPlus, "\uBC15\uC2A4 \uB77C\uBCA8 \uB370\uC774\uD130\uC14B"));
            DatasetPurposeModes.Add(new WpfLearningModeItem(WpfLearningMode.Segmentation, "\uC138\uADF8\uBA58\uD14C\uC774\uC158", PackIconMaterialKind.ViewListOutline, "\uD3F4\uB9AC\uACE4\uACFC \uB9C8\uC2A4\uD06C \uB77C\uBCA8"));
            DatasetPurposeModes.Add(new WpfLearningModeItem(WpfLearningMode.AnomalyDetection, "\uC774\uC0C1 \uD0D0\uC9C0", PackIconMaterialKind.AlertCircleOutline, "\uC815\uC0C1/\uC774\uC0C1 \uC0D8\uD50C\uACFC \uC601\uC5ED"));
            SelectedDatasetPurposeMode = DatasetPurposeModes.FirstOrDefault();
            RefreshPreview();
        }

        public string ViewName => nameof(WpfDatasetSetupWizardWindow);

        public string SetupSummaryText => "\uC0C8 \uB370\uC774\uD130\uC14B\uC740 \uC0C8 \uC800\uC7A5 \uD3F4\uB354\uB97C \uB9CC\uB4E4\uACE0, \uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB294 \uBCC4\uB3C4\uB85C \uC5F0\uACB0\uD569\uB2C8\uB2E4.";

        public string SetupSourceRuleTitleText => "\uC800\uC7A5 \uD3F4\uB354\uC640 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB294 \uC5ED\uD560\uC774 \uB2E4\uB985\uB2C8\uB2E4";

        public string SetupSourceRuleDetailText => "\uC800\uC7A5 \uD3F4\uB354\uB294 \uB77C\uBCA8, Recipe, \uD559\uC2B5 \uACB0\uACFC\uB97C \uBCF4\uAD00\uD558\uB294 \uB370\uC774\uD130\uC14B \uAE30\uC900\uC785\uB2C8\uB2E4. \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB294 \uC6D0\uBCF8 \uC774\uBBF8\uC9C0\uB97C \uBCF4\uB294 \uC704\uCE58\uC77C \uBFD0\uC774\uBBC0\uB85C, \uAC19\uC740 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC368\uB3C4 \uC0C8 \uC800\uC7A5 \uD3F4\uB354\uBA74 \uB77C\uBCA8\uC774 \uC11E\uC774\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";

        public string SetupSourceRuleChecklistText => "\uC0C8\uB85C \uC2DC\uC791: \uC0C8 \uC800\uC7A5 \uD3F4\uB354 / \uAE30\uC874 \uC791\uC5C5 \uACC4\uC18D: \uAE30\uC874 \uC800\uC7A5 \uD3F4\uB354 / \uC6D0\uBCF8 \uC774\uBBF8\uC9C0\uB9CC \uAD50\uCCB4: \uC774\uBBF8\uC9C0 \uD3F4\uB354 \uBCC0\uACBD";

        public ObservableCollection<WpfLearningModeItem> DatasetPurposeModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfDatasetSamplePresetItem> SamplePresets { get; } = new ObservableCollection<WpfDatasetSamplePresetItem>();

        public WpfLearningModeItem SelectedDatasetPurposeMode
        {
            get => selectedDatasetPurposeMode;
            set
            {
                if (SetProperty(ref selectedDatasetPurposeMode, value))
                {
                    SynchronizeUntouchedAutomaticPaths();
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
                    if (automaticPathSyncEnabled && !isApplyingAutomaticPathSync)
                    {
                        recipeNameWasEdited = true;
                    }

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
                    if (automaticPathSyncEnabled && !isApplyingAutomaticPathSync)
                    {
                        outputRootPathWasEdited = true;
                    }

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

        public string ClassSummaryText
        {
            get => classSummaryText;
            private set => SetProperty(ref classSummaryText, value ?? string.Empty);
        }

        public string StorageHelpText
        {
            get => storageHelpText;
            private set => SetProperty(ref storageHelpText, value ?? string.Empty);
        }

        public string ImageSourcePreviewText
        {
            get => imageSourcePreviewText;
            private set => SetProperty(ref imageSourcePreviewText, value ?? string.Empty);
        }

        public string IsolationHelpText
        {
            get => isolationHelpText;
            private set => SetProperty(ref isolationHelpText, value ?? string.Empty);
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

        public void ConfigureAutomaticPathSync(
            bool recipeNameWasGenerated,
            Func<LabelingDatasetPurpose, string> recipeNameResolver,
            Func<string, string> outputRootResolver)
        {
            automaticRecipeNameResolver = recipeNameResolver;
            automaticOutputRootResolver = outputRootResolver;
            automaticPathSyncEnabled = recipeNameWasGenerated;
            recipeNameWasEdited = false;
            outputRootPathWasEdited = false;
            automaticRecipeName = recipeNameWasGenerated ? (RecipeName ?? string.Empty).Trim() : string.Empty;
            automaticOutputRootPath = recipeNameWasGenerated ? (OutputRootPath ?? string.Empty).Trim() : string.Empty;
        }

        public void LoadFrom(LabelingDatasetPurpose purpose, string recipeName, string outputRootPath, IEnumerable<string> classNames)
        {
            WpfLearningMode mode = WpfLearningWorkflowPanelViewModel.ToLearningMode(purpose);
            SelectedDatasetPurposeMode = DatasetPurposeModes.FirstOrDefault(item => item.Mode == mode)
                ?? DatasetPurposeModes.FirstOrDefault();
            RecipeName = recipeName ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            ClassNamesText = string.Join(Environment.NewLine, NormalizeClassNames(classNames).DefaultIfEmpty("Defect"));
            StatusText = "\uC0DD\uC131 \uC804\uC5D0 \uC0C8 \uC800\uC7A5 \uD3F4\uB354, \uC2DC\uC791 \uC774\uBBF8\uC9C0, \uD074\uB798\uC2A4\uB97C \uD655\uC778\uD558\uC138\uC694.";
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
            // Operators often paste class lists from notes. Accept both visual line breaks and compact comma/semicolon lists.
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
            ClassSummaryText = classNames.Count == 0
                ? "\uC0DD\uC131\uB420 \uD074\uB798\uC2A4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uCD5C\uC18C 1\uAC1C\uB97C \uC785\uB825\uD558\uC138\uC694."
                : string.Format(CultureInfo.InvariantCulture, "\uC0DD\uC131\uB420 \uD074\uB798\uC2A4 {0}\uAC1C: {1}", classNames.Count, classSummary);
            string outputName = string.IsNullOrWhiteSpace(OutputRootPath) ? "-" : Path.GetFileName(OutputRootPath.Trim());
            string sampleSummary = SelectedSamplePreset == null ? "-" : SelectedSamplePreset.Text;
            StorageHelpText = string.IsNullOrWhiteSpace(OutputRootPath)
                ? "\uC0C8 \uC800\uC7A5 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694. \uC774 \uD3F4\uB354\uAC00 \uB77C\uBCA8, Recipe, \uD559\uC2B5 \uD30C\uC77C\uC744 \uBCF4\uAD00\uD558\uB294 \uB370\uC774\uD130\uC14B \uAE30\uC900\uC785\uB2C8\uB2E4."
                : $"\uC774 \uC800\uC7A5 \uD3F4\uB354\uAC00 \uB77C\uBCA8/Recipe/\uD559\uC2B5 \uD30C\uC77C\uC758 \uAE30\uC900\uC785\uB2C8\uB2E4. \uC0DD\uC131 \uD6C4 \uB77C\uBCA8\uC740 \uC774 \uACBD\uB85C\uC5D0 \uBD84\uB9AC\uB429\uB2C8\uB2E4: {OutputRootPath.Trim()}";
            ImageSourcePreviewText = BuildImageSourcePreviewText(SelectedSamplePreset);
            IsolationHelpText = "\uC800\uC7A5 \uD3F4\uB354\uAC00 \uB370\uC774\uD130\uC14B\uC744 \uAD6C\uBD84\uD569\uB2C8\uB2E4. \uAC19\uC740 \uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC368\uB3C4 \uC0C8 \uC800\uC7A5 \uD3F4\uB354\uBA74 \uB77C\uBCA8\uACFC \uD559\uC2B5 \uACB0\uACFC\uAC00 \uBD84\uB9AC\uB429\uB2C8\uB2E4.";
            PreviewText = string.Format(
                CultureInfo.InvariantCulture,
                "\uBAA9\uC801: {0} / \uC2DC\uC791 \uB370\uC774\uD130: {1} / Recipe: {2} / \uC800\uC7A5 \uD3F4\uB354: {3} / \uD074\uB798\uC2A4: {4}",
                FormatPurposeText(purpose),
                sampleSummary,
                string.IsNullOrWhiteSpace(RecipeName) ? "-" : RecipeName.Trim(),
                string.IsNullOrWhiteSpace(outputName) ? OutputRootPath : outputName,
                classSummary);
        }

        private static string BuildImageSourcePreviewText(WpfDatasetSamplePresetItem samplePreset)
        {
            if (samplePreset == null || samplePreset.Kind == WpfDatasetSamplePresetKind.Empty)
            {
                return "\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354: \uC0DD\uC131 \uD6C4 \uC0C1\uB2E8\uC758 \uC774\uBBF8\uC9C0 \uD3F4\uB354 \uBC84\uD2BC\uC73C\uB85C \uC5F0\uACB0\uD569\uB2C8\uB2E4.";
            }

            string sourcePath = string.IsNullOrWhiteSpace(samplePreset.ImageSourcePath)
                ? "\uC0D8\uD50C \uC6D0\uBCF8 \uACBD\uB85C \uBBF8\uD655\uC778"
                : samplePreset.ImageSourcePath;
            return $"\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354: {sourcePath}";
        }

        private static string FormatPurposeText(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "\uC138\uADF8\uBA58\uD14C\uC774\uC158",
                LabelingDatasetPurpose.AnomalyDetection => "\uC774\uC0C1 \uD0D0\uC9C0",
                _ => "\uAC1D\uCCB4 \uD0D0\uC9C0"
            };
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

        private void SynchronizeUntouchedAutomaticPaths()
        {
            if (!automaticPathSyncEnabled
                || recipeNameWasEdited
                || automaticRecipeNameResolver == null
                || string.IsNullOrWhiteSpace(automaticRecipeName)
                || !string.Equals((RecipeName ?? string.Empty).Trim(), automaticRecipeName, StringComparison.Ordinal))
            {
                return;
            }

            bool outputRootIsUntouched = !outputRootPathWasEdited
                && WpfDatasetSetupPathService.PathsEqual(OutputRootPath, automaticOutputRootPath);
            LabelingDatasetPurpose purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(
                SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);
            string nextRecipeName = automaticRecipeNameResolver(purpose)?.Trim() ?? string.Empty;
            if (!WpfProjectRecipeService.IsValidRecipeName(nextRecipeName))
            {
                return;
            }

            isApplyingAutomaticPathSync = true;
            try
            {
                automaticRecipeName = nextRecipeName;
                RecipeName = nextRecipeName;
                if (outputRootIsUntouched && automaticOutputRootResolver != null)
                {
                    automaticOutputRootPath = automaticOutputRootResolver(nextRecipeName)?.Trim() ?? string.Empty;
                    OutputRootPath = automaticOutputRootPath;
                }
            }
            finally
            {
                isApplyingAutomaticPathSync = false;
            }
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
