using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSetupWizardViewModel : WpfObservableViewModel, IDisposable
    {
        public const int RecipeNameMaxLength = 100;

        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private WpfLearningModeItem selectedDatasetPurposeMode;
        private string recipeName = string.Empty;
        private string productCode = string.Empty;
        private string recipeDescription = string.Empty;
        private string outputRootPath = string.Empty;
        private string classNamesText = "Defect";
        private string newClassName = string.Empty;
        private string imageRootPath = string.Empty;
        private string selectedModelEngine = PythonModelSettings.EngineYoloV8;
        private string weightsPath = string.Empty;
        private string anomalyNormalClassNamesText = "normal";
        private string anomalyAbnormalClassNamesText = "abnormal";
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
        private ICommand browseImageRootCommand = new RelayCommand(NoOpCommand);
        private ICommand browseWeightsCommand = new RelayCommand(NoOpCommand);
        private Func<LabelingDatasetPurpose, string> automaticRecipeNameResolver;
        private Func<string, string> automaticOutputRootResolver;
        private Func<DatasetSetupWizardPathCallbacks> pathCallbacksProvider;
        private string automaticRecipeName = string.Empty;
        private string automaticOutputRootPath = string.Empty;
        private bool automaticPathSyncEnabled;
        private bool isApplyingAutomaticPathSync;
        private bool recipeNameWasEdited;
        private bool outputRootPathWasEdited;
        private bool disposed;

        public WpfDatasetSetupWizardViewModel()
        {
            AddClassCommand = new RelayCommand(AddClass);
            RemoveClassCommand = new RelayCommand<string>(RemoveClass);
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            RefreshDatasetPurposeModes(WpfLearningMode.ObjectDetection);
            RefreshSamplePresets();
            RefreshPreview();
        }

        public string ViewName => nameof(WpfDatasetSetupWizardWindow);

        public string WindowTitleText => T("WpfDatasetSetup.Title");

        public string SetupSummaryText => T("WpfDatasetSetup.Summary");

        public string ClassEntryHelpText => T("WpfDatasetSetup.Classes.EntryHelp");
        public string ClassEntryPlaceholderText => T("WpfDatasetSetup.Classes.EntryPlaceholder");
        public string AdvancedSettingsText => T("WpfDatasetSetup.Advanced");
        public string AdvancedButtonText => T("WpfDatasetSetup.AdvancedButton");
        public string RecipeFolderNoteText => T("WpfDatasetSetup.RecipeFolderNote");
        public string CreateButtonText => T("WpfDatasetSetup.CreateAndOpen");
        public string CancelButtonText => T("WpfComparisonWorkspace.Cancel");
        public string CloseButtonText => T("Common.Close");
        public string JumpToClassesText => T("WpfDatasetSetup.JumpToClasses");
        public string BrowseText => T("찾기");

        public string NewClassName { get => newClassName; set => SetProperty(ref newClassName, value ?? string.Empty); }
        public IReadOnlyList<DatasetSetupClassTag> ClassTags => ParseClassNames(ClassNamesText).Select((name, index) => new DatasetSetupClassTag(index + 1, name)).ToArray();
        public ICommand AddClassCommand { get; }
        public ICommand RemoveClassCommand { get; }

        public string RecipeNameSectionText => T("WpfDatasetSetup.Section.RecipeName");

        public string DatasetPurposeSectionText => T("WpfDatasetSetup.Section.Purpose");

        public string ImageStorageSectionText => T("WpfDatasetSetup.Section.ImageStorage");

        public string ImageFolderLabelText => T("WpfDatasetSetup.Field.ImageFolder");

        public string OutputRootLabelText => T("WpfDatasetSetup.Field.OutputRoot");

        public string RecipeClassesSectionText => T("WpfDatasetSetup.Section.Classes");

        public string CreateSummaryTitleText => T("WpfDatasetSetup.CreateSummary.Title");

        public string CreateSummaryRecipeLabelText => T("WpfDatasetSetup.CreateSummary.Recipe");

        public string CreateSummaryPurposeLabelText => T("WpfDatasetSetup.CreateSummary.Purpose");

        public string CreateSummaryImageLabelText => T("WpfDatasetSetup.CreateSummary.Image");

        public string CreateSummaryClassesLabelText => T("WpfDatasetSetup.CreateSummary.Classes");

        public string CreateSummaryNoteText => T("WpfDatasetSetup.CreateSummary.Note");

        public string CreateSummaryExplainText => T("WpfDatasetSetup.CreateSummary.Explain");

        public string RecipeNamePlaceholderText => T("WpfDatasetSetup.RecipeName.Placeholder");

        public string ProductCodePlaceholderText => T("WpfDatasetSetup.ProductCode.Placeholder");

        public string ImageRootPlaceholderText => T("WpfDatasetSetup.ImageRoot.Placeholder");

        public string RecipeNameSummaryText
            => string.IsNullOrWhiteSpace(RecipeName) ? T("WpfDatasetSetup.CreateSummary.NameEmpty") : RecipeName;

        public string SetupSourceRuleTitleText => T("WpfDatasetSetup.SourceRule.Title");

        public string SetupSourceRuleDetailText => T("WpfDatasetSetup.SourceRule.Detail");

        public string SetupSourceRuleChecklistText => T("WpfDatasetSetup.SourceRule.Checklist");

        public string StartingDataTitleText => T("WpfDatasetSetup.StartingData");
        public string ModelSectionTitleText => T("WpfDatasetSetup.ModelSection.Title");
        public string ModelSetupIntroText => T("WpfDatasetSetup.ModelSetupIntro");
        public string ModelEngineLabelText => T("WpfDatasetSetup.ModelSection.Engine");
        public string InitialModelEngineNameText => T("WpfDatasetSetup.ModelSection.InitialEngine");
        public string ModelFileLabelText => T("WpfDatasetSetup.ModelSection.File");
        public string InitialModelFileNameText => T("WpfDatasetSetup.ModelSection.InitialFile");
        public string BrowseInitialModelFileNameText => T("WpfDatasetSetup.ModelSection.BrowseFile");
        public string AnomalyClassesTitleText => T("WpfDatasetSetup.AnomalyClasses.Title");
        public string NormalClassLabelText => T("WpfDatasetSetup.AnomalyClasses.Normal");
        public string AnomalousClassLabelText => T("WpfDatasetSetup.AnomalyClasses.Anomalous");
        public string NormalClassNameText => T("WpfDatasetSetup.AnomalyClasses.NormalName");
        public string AnomalousClassNameText => T("WpfDatasetSetup.AnomalyClasses.AnomalousName");

        public ObservableCollection<WpfLearningModeItem> DatasetPurposeModes { get; } = new ObservableCollection<WpfLearningModeItem>();

        public ObservableCollection<WpfDatasetSamplePresetItem> SamplePresets { get; } = new ObservableCollection<WpfDatasetSamplePresetItem>();

        public WpfLearningModeItem SelectedDatasetPurposeMode
        {
            get => selectedDatasetPurposeMode;
            set
            {
                if (SetProperty(ref selectedDatasetPurposeMode, value))
                {
                    string classDraft = SelectedSamplePreset?.Kind == WpfDatasetSamplePresetKind.Empty && value?.Mode != WpfLearningMode.AnomalyDetection ? ClassNamesText : null;
                    SynchronizeUntouchedAutomaticPaths();
                    RefreshSamplePresets();
                    if (classDraft != null) ClassNamesText = classDraft;
                    RefreshPreview();
                    OnPropertyChanged(nameof(AnomalySettingsVisibility));
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
                    if (!string.IsNullOrWhiteSpace(RecipeName) && StatusText == T("WpfDatasetSetup.Error.RecipeNameRequired")) StatusText = string.Empty;
                    OnPropertyChanged(nameof(RecipeNameSummaryText));
                    OnPropertyChanged(nameof(RecipeNamePlaceholderVisibility));
                    if (automaticPathSyncEnabled && !isApplyingAutomaticPathSync)
                    {
                        recipeNameWasEdited = true;
                    }

                    RefreshPreview();
                }
            }
        }

        public Visibility RecipeNamePlaceholderVisibility
            => string.IsNullOrEmpty(RecipeName) ? Visibility.Visible : Visibility.Collapsed;

        public string ProductCode
        {
            get => productCode;
            set => SetProperty(ref productCode, value ?? string.Empty);
        }

        public string RecipeDescription
        {
            get => recipeDescription;
            set => SetProperty(ref recipeDescription, value ?? string.Empty);
        }

        public string RecipeIdentitySectionText => T("WpfDatasetSetup.Identity.Section");

        public string RecipeProductCodeLabelText => T("WpfDatasetSetup.Identity.ProductCode");

        public string RecipeDescriptionLabelText => T("WpfDatasetSetup.Identity.Description");

        public string RecipeIdentityHelpText => T("WpfDatasetSetup.Identity.Help");

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
                    OnPropertyChanged(nameof(SummaryClassItems));
                    OnPropertyChanged(nameof(ClassTags));
                    RefreshPreview();
                }
            }
        }

        public string ImageRootPath
        {
            get => imageRootPath;
            set
            {
                if (SetProperty(ref imageRootPath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(ImageSummaryText));
                    RefreshPreview();
                }
            }
        }

        public ObservableCollection<string> ModelEngineOptions { get; } = new ObservableCollection<string>(PythonModelSettings.GetSupportedModelEngines());

        public string SelectedModelEngine
        {
            get => selectedModelEngine;
            set
            {
                if (SetProperty(ref selectedModelEngine, PythonModelSettings.NormalizeModelEngine(value)))
                {
                    RefreshPreview();
                }
            }
        }

        public string WeightsPath
        {
            get => weightsPath;
            set
            {
                if (SetProperty(ref weightsPath, value ?? string.Empty))
                {
                    RefreshPreview();
                }
            }
        }

        public string AnomalyNormalClassNamesText
        {
            get => anomalyNormalClassNamesText;
            set => SetProperty(ref anomalyNormalClassNamesText, value ?? string.Empty);
        }

        public string AnomalyAbnormalClassNamesText
        {
            get => anomalyAbnormalClassNamesText;
            set => SetProperty(ref anomalyAbnormalClassNamesText, value ?? string.Empty);
        }

        public Visibility AnomalySettingsVisibility
            => WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection)
                == LabelingDatasetPurpose.AnomalyDetection
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string ModelSetupHelpText
            => string.IsNullOrWhiteSpace(WeightsPath)
                ? T("WpfDatasetSetup.ModelHelp.Empty")
                : T("WpfDatasetSetup.ModelHelp.Selected");

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

        public IReadOnlyList<string> SummaryClassItems
            => ClassTags.Select(tag => tag.Caption).ToArray();

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

        public string ImageSummaryText
            => string.IsNullOrWhiteSpace(ImageRootPath)
                ? T("WpfDatasetSetup.CreateSummary.ImageLater")
                : Path.GetFileName(Path.TrimEndingDirectorySeparator(ImageRootPath.Trim()));

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

        public ICommand BrowseImageRootCommand
        {
            get => browseImageRootCommand;
            private set => SetProperty(ref browseImageRootCommand, value);
        }

        public ICommand BrowseWeightsCommand
        {
            get => browseWeightsCommand;
            private set => SetProperty(ref browseWeightsCommand, value);
        }

        public void ConfigureCommands(
            Action<object> create,
            Action cancel,
            Action browseOutputRoot,
            Action browseImageRoot = null,
            Action browseWeights = null)
        {
            // The dialog owns no persistence. Commands are supplied by the shell composition root.
            CreateCommand = new RelayCommand<object>(create ?? NoOpSelectionCommand);
            CancelCommand = new RelayCommand(cancel ?? NoOpCommand);
            BrowseOutputRootCommand = new RelayCommand(browseOutputRoot ?? NoOpCommand);
            BrowseImageRootCommand = new RelayCommand(browseImageRoot ?? NoOpCommand);
            BrowseWeightsCommand = new RelayCommand(browseWeights ?? NoOpCommand);
        }

        public void ConfigurePathSelectionWorkflow(
            Func<DatasetSetupWizardPathCallbacks> callbacksProvider)
        {
            pathCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            BrowseOutputRootCommand = new RelayCommand(ExecuteBrowseOutputRoot);
            BrowseImageRootCommand = new RelayCommand(ExecuteBrowseImageRoot);
            BrowseWeightsCommand = new RelayCommand(ExecuteBrowseWeights);
        }

        private void ExecuteBrowseOutputRoot()
        {
            ExecuteBrowseFolder(
                T("WpfDatasetSetup.Dialog.RecipeFolder"),
                OutputRootPath,
                selectedPath => OutputRootPath = selectedPath);
        }

        private void ExecuteBrowseImageRoot()
        {
            ExecuteBrowseFolder(
                T("WpfDatasetSetup.Dialog.SourceImageFolder"),
                ImageRootPath,
                selectedPath => ImageRootPath = selectedPath);
        }

        private void ExecuteBrowseWeights()
        {
            DatasetSetupWizardPathCallbacks callbacks = pathCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFile?.Invoke(
                T("WpfDatasetSetup.Dialog.InspectionModel"),
                "모델 파일 (*.pt;*.pth;*.onnx)|*.pt;*.pth;*.onnx|All files (*.*)|*.*",
                WeightsPath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            WeightsPath = selectedPath;
        }

        private void ExecuteBrowseFolder(string title, string currentPath, Action<string> setPath)
        {
            DatasetSetupWizardPathCallbacks callbacks = pathCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFolder?.Invoke(title, currentPath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            setPath(selectedPath);
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
            ImageRootPath = string.Empty;
            SelectedModelEngine = PythonModelSettings.EngineYoloV8;
            WeightsPath = string.Empty;
            AnomalyNormalClassNamesText = "normal";
            AnomalyAbnormalClassNamesText = "abnormal";
            StatusText = string.Empty;
            RefreshPreview();
        }

        public bool TryBuildRequest(out WpfDatasetSetupRequest request, out string error)
            => TryBuildRequest(SelectedDatasetPurposeMode, out request, out error);

        public bool TryBuildRequest(object selectedPurpose, out WpfDatasetSetupRequest request, out string error)
        {
            request = null;
            error = string.Empty;

            string normalizedRecipeName = (RecipeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedRecipeName))
            {
                error = T("WpfDatasetSetup.Error.RecipeNameRequired");
                return false;
            }

            if (normalizedRecipeName.Length > RecipeNameMaxLength)
            {
                error = T("WpfDatasetSetup.Error.RecipeNameTooLong");
                return false;
            }

            if (!ProjectRecipeService.IsValidRecipeName(normalizedRecipeName))
            {
                error = T("WpfDatasetSetup.Error.InvalidRecipeName");
                return false;
            }

            string normalizedOutputRoot = (OutputRootPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedOutputRoot))
            {
                error = T("WpfDatasetSetup.Error.StorageRequired");
                return false;
            }

            IReadOnlyList<string> classNames = ParseClassNames(ClassNamesText);
            if (classNames.Count == 0)
            {
                error = T("WpfDatasetSetup.Error.ClassRequired");
                return false;
            }

            string normalizedImageRoot = (ImageRootPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedImageRoot) && !Directory.Exists(normalizedImageRoot))
            {
                error = T("WpfDatasetSetup.Error.ImageRootMissing");
                return false;
            }

            string normalizedWeightsPath = (WeightsPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedWeightsPath) && !File.Exists(normalizedWeightsPath))
            {
                error = T("WpfDatasetSetup.Error.WeightsMissing");
                return false;
            }

            WpfLearningModeItem selectedPurposeItem = selectedPurpose as WpfLearningModeItem
                ?? SelectedDatasetPurposeMode;
            string normalizedProductCode = (ProductCode ?? string.Empty).Trim();
            string normalizedRecipeDescription = (RecipeDescription ?? string.Empty).Trim();
            if (normalizedProductCode.Length > LabelingProjectSettings.RecipeProductCodeMaxLength
                || normalizedRecipeDescription.Length > LabelingProjectSettings.RecipeDescriptionMaxLength)
            {
                error = T("WpfDatasetSetup.Error.IdentityTooLong");
                return false;
            }

            request = new WpfDatasetSetupRequest
            {
                Purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(selectedPurposeItem?.Mode ?? WpfLearningMode.ObjectDetection),
                RecipeName = normalizedRecipeName,
                ProductCode = normalizedProductCode,
                RecipeDescription = normalizedRecipeDescription,
                OutputRootPath = normalizedOutputRoot,
                ClassNames = classNames,
                ImageRootPath = normalizedImageRoot,
                ModelEngine = PythonModelSettings.NormalizeModelEngine(SelectedModelEngine),
                WeightsPath = normalizedWeightsPath,
                AnomalyNormalClassNames = ParseClassNames(AnomalyNormalClassNamesText),
                AnomalyAbnormalClassNames = ParseClassNames(AnomalyAbnormalClassNamesText)
            };
            WpfDatasetSamplePresetItem samplePreset = SelectedSamplePreset
                ?? DatasetSamplePresetService.CreateEmptyPreset(request.Purpose);
            if (!samplePreset.IsAvailable)
            {
                error = Format("WpfDatasetSetup.Error.SampleUnavailable", samplePreset.Text, samplePreset.AvailabilityText);
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

        private void AddClass()
        {
            string name = ClassCatalogService.NormalizeClassName(NewClassName);
            if (name.Length == 0) return;
            ClassNamesText = string.Join(Environment.NewLine, ParseClassNames(ClassNamesText).Concat(ParseClassNames(name)).Distinct(StringComparer.OrdinalIgnoreCase));
            NewClassName = string.Empty;
        }

        private void RemoveClass(string name)
            => ClassNamesText = string.Join(Environment.NewLine, ParseClassNames(ClassNamesText).Where(item => !string.Equals(item, name, StringComparison.OrdinalIgnoreCase)));

        private void RefreshPreview()
        {
            LabelingDatasetPurpose purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);
            IReadOnlyList<string> classNames = ParseClassNames(ClassNamesText);
            string classSummary = classNames.Count == 0 ? "-" : string.Join(", ", classNames);
            ClassSummaryText = classNames.Count == 0
                ? T("WpfDatasetSetup.ClassSummary.Empty")
                : Format("WpfDatasetSetup.ClassSummary.Count", classNames.Count, classSummary);
            string outputName = string.IsNullOrWhiteSpace(OutputRootPath) ? "-" : Path.GetFileName(OutputRootPath.Trim());
            string sampleSummary = SelectedSamplePreset == null ? "-" : SelectedSamplePreset.Text;
            StorageHelpText = string.IsNullOrWhiteSpace(OutputRootPath)
                ? T("WpfDatasetSetup.StorageHelp.Empty")
                : Format("WpfDatasetSetup.StorageHelp.Selected", OutputRootPath.Trim());
            ImageSourcePreviewText = BuildImageSourcePreviewText(SelectedSamplePreset, ImageRootPath);
            OnPropertyChanged(nameof(ModelSetupHelpText));
            IsolationHelpText = T("WpfDatasetSetup.IsolationHelp");
            PreviewText = string.Format(
                CultureInfo.InvariantCulture,
                T("WpfDatasetSetup.Preview"),
                FormatPurposeText(purpose),
                sampleSummary,
                string.IsNullOrWhiteSpace(RecipeName) ? "-" : RecipeName.Trim(),
                string.IsNullOrWhiteSpace(outputName) ? OutputRootPath : outputName,
                classSummary,
                PythonModelSettings.NormalizeModelEngine(SelectedModelEngine));
        }

        private static string BuildImageSourcePreviewText(WpfDatasetSamplePresetItem samplePreset, string imageRootPath)
        {
            if (!string.IsNullOrWhiteSpace(imageRootPath))
            {
                return Format("WpfDatasetSetup.ImageSource.Selected", imageRootPath.Trim());
            }

            if (samplePreset == null || samplePreset.Kind == WpfDatasetSamplePresetKind.Empty)
            {
                return T("WpfDatasetSetup.ImageSource.Empty");
            }

            string sourcePath = string.IsNullOrWhiteSpace(samplePreset.ImageSourcePath)
                ? T("WpfDatasetSetup.ImageSource.Unknown")
                : samplePreset.ImageSourcePath;
            return Format("WpfDatasetSetup.ImageSource.Sample", sourcePath);
        }

        private static string FormatPurposeText(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => T("WpfShell.Dataset.Purpose.Segmentation"),
                LabelingDatasetPurpose.AnomalyDetection => T("WpfShell.Dataset.Purpose.AnomalyDetection"),
                _ => T("WpfShell.Dataset.Purpose.ObjectDetection")
            };
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(WindowTitleText));
            OnPropertyChanged(nameof(ClassEntryHelpText));
            OnPropertyChanged(nameof(ClassEntryPlaceholderText));
            OnPropertyChanged(nameof(AdvancedSettingsText));
            OnPropertyChanged(nameof(AdvancedButtonText));
            OnPropertyChanged(nameof(RecipeFolderNoteText));
            OnPropertyChanged(nameof(CreateButtonText));
            OnPropertyChanged(nameof(JumpToClassesText));
            OnPropertyChanged(nameof(BrowseText));
            OnPropertyChanged(nameof(CancelButtonText));
            OnPropertyChanged(nameof(CloseButtonText));
            OnPropertyChanged(nameof(SetupSummaryText));
            OnPropertyChanged(nameof(RecipeNameSectionText));
            OnPropertyChanged(nameof(DatasetPurposeSectionText));
            OnPropertyChanged(nameof(ImageStorageSectionText));
            OnPropertyChanged(nameof(ImageFolderLabelText));
            OnPropertyChanged(nameof(OutputRootLabelText));
            OnPropertyChanged(nameof(RecipeClassesSectionText));
            OnPropertyChanged(nameof(CreateSummaryTitleText));
            OnPropertyChanged(nameof(CreateSummaryRecipeLabelText));
            OnPropertyChanged(nameof(CreateSummaryPurposeLabelText));
            OnPropertyChanged(nameof(CreateSummaryImageLabelText));
            OnPropertyChanged(nameof(CreateSummaryClassesLabelText));
            OnPropertyChanged(nameof(CreateSummaryNoteText));
            OnPropertyChanged(nameof(CreateSummaryExplainText));
            OnPropertyChanged(nameof(ImageSummaryText));
            OnPropertyChanged(nameof(RecipeNamePlaceholderText));
            OnPropertyChanged(nameof(ProductCodePlaceholderText));
            OnPropertyChanged(nameof(ImageRootPlaceholderText));
            OnPropertyChanged(nameof(RecipeNameSummaryText));
            OnPropertyChanged(nameof(SetupSourceRuleTitleText));
            OnPropertyChanged(nameof(SetupSourceRuleDetailText));
            OnPropertyChanged(nameof(SetupSourceRuleChecklistText));
            OnPropertyChanged(nameof(StartingDataTitleText));
            OnPropertyChanged(nameof(ModelSectionTitleText));
            OnPropertyChanged(nameof(ModelSetupIntroText));
            OnPropertyChanged(nameof(ModelEngineLabelText));
            OnPropertyChanged(nameof(InitialModelEngineNameText));
            OnPropertyChanged(nameof(ModelFileLabelText));
            OnPropertyChanged(nameof(InitialModelFileNameText));
            OnPropertyChanged(nameof(BrowseInitialModelFileNameText));
            OnPropertyChanged(nameof(AnomalyClassesTitleText));
            OnPropertyChanged(nameof(NormalClassLabelText));
            OnPropertyChanged(nameof(AnomalousClassLabelText));
            OnPropertyChanged(nameof(NormalClassNameText));
            OnPropertyChanged(nameof(AnomalousClassNameText));
            OnPropertyChanged(nameof(RecipeIdentitySectionText));
            OnPropertyChanged(nameof(RecipeProductCodeLabelText));
            OnPropertyChanged(nameof(RecipeDescriptionLabelText));
            OnPropertyChanged(nameof(RecipeIdentityHelpText));
            OnPropertyChanged(nameof(ModelSetupHelpText));
            StatusText = LocalizationTextRuntimeService.Translate(StatusText);
            WpfLearningMode selectedPurpose = SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection;
            string classNamesText = ClassNamesText;
            RefreshDatasetPurposeModes(selectedPurpose);
            RefreshSamplePresets();
            ClassNamesText = classNamesText;
            RefreshPreview();
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

        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] values)
            => string.Format(CultureInfo.InvariantCulture, T(key), values ?? Array.Empty<object>());

        private void RefreshSamplePresets()
        {
            WpfDatasetSamplePresetKind previousKind = SelectedSamplePreset?.Kind ?? WpfDatasetSamplePresetKind.Empty;
            LabelingDatasetPurpose purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);

            SamplePresets.Clear();
            foreach (WpfDatasetSamplePresetItem preset in DatasetSamplePresetService.BuildPresets(purpose))
            {
                SamplePresets.Add(preset);
            }

            SelectedSamplePreset = SamplePresets.FirstOrDefault(item => item.Kind == previousKind && item.IsAvailable)
                ?? SamplePresets.FirstOrDefault(item => item.Kind == WpfDatasetSamplePresetKind.Empty)
                ?? SamplePresets.FirstOrDefault();
        }

        private void RefreshDatasetPurposeModes(WpfLearningMode selectedMode)
        {
            DatasetPurposeModes.Clear();
            DatasetPurposeModes.Add(new WpfLearningModeItem(
                WpfLearningMode.ObjectDetection,
                T("WpfDatasetSetup.Mode.ObjectDetection"),
                PackIconMaterialKind.ShapeSquareRoundedPlus,
                T("WpfDatasetSetup.Mode.ObjectDetection.Tooltip")));
            DatasetPurposeModes.Add(new WpfLearningModeItem(
                WpfLearningMode.Segmentation,
                T("WpfDatasetSetup.Mode.Segmentation"),
                PackIconMaterialKind.ViewListOutline,
                T("WpfDatasetSetup.Mode.Segmentation.Tooltip")));
            DatasetPurposeModes.Add(new WpfLearningModeItem(
                WpfLearningMode.AnomalyDetection,
                T("WpfDatasetSetup.Mode.AnomalyDetection"),
                PackIconMaterialKind.AlertCircleOutline,
                T("WpfDatasetSetup.Mode.AnomalyDetection.Tooltip")));

            selectedDatasetPurposeMode = DatasetPurposeModes.FirstOrDefault(item => item.Mode == selectedMode)
                ?? DatasetPurposeModes.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedDatasetPurposeMode));
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
                && DatasetSetupPathService.PathsEqual(OutputRootPath, automaticOutputRootPath);
            LabelingDatasetPurpose purpose = WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(
                SelectedDatasetPurposeMode?.Mode ?? WpfLearningMode.ObjectDetection);
            string nextRecipeName = automaticRecipeNameResolver(purpose)?.Trim() ?? string.Empty;
            if (!ProjectRecipeService.IsValidRecipeName(nextRecipeName))
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

    // The displayed ordinal belongs to this draft form; persisted class indices remain zero-based.
    public sealed class DatasetSetupClassTag
    {
        public DatasetSetupClassTag(int ordinal, string name) { Ordinal = ordinal; Name = name; }
        public int Ordinal { get; }
        public string Name { get; }
        public string Caption => $"{Ordinal} {Name}";
    }

    public sealed class DatasetSetupWizardPathCallbacks
    {
        public Func<string, string, string> SelectFolder { get; init; }

        public Func<string, string, string, string> SelectFile { get; init; }

        public Func<bool> IsApplicationCloseApproved { get; init; }
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
        private readonly string text;
        private readonly string toolTip;
        private readonly string availabilityText;

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
            ImageSourcePath = imageSourcePath ?? string.Empty;
            LabelSourcePath = labelSourcePath ?? string.Empty;
            ClassNames = classNames ?? Array.Empty<string>();
            IsAvailable = isAvailable;
            this.text = text ?? string.Empty;
            this.toolTip = toolTip ?? string.Empty;
            this.availabilityText = availabilityText ?? string.Empty;
        }

        public WpfDatasetSamplePresetKind Kind { get; }

        public LabelingDatasetPurpose Purpose { get; }

        public string Text => LocalizationTextRuntimeService.Translate(text);

        public string ToolTip => LocalizationTextRuntimeService.Translate(toolTip);

        public string ImageSourcePath { get; }

        public string LabelSourcePath { get; }

        public IReadOnlyList<string> ClassNames { get; }

        public bool IsAvailable { get; }

        public string AvailabilityText => LocalizationTextRuntimeService.Translate(availabilityText);
    }
}
