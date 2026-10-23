using MahApps.Metro.IconPacks;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MvcVisionSystem
{
    internal static class DatasetSelectionTextFormatter
    {
        public static string Translate(string key) => OpenVisionLanguageService.T(key);

        public static string Format(string key, params object[] values)
            => string.Format(System.Globalization.CultureInfo.CurrentCulture, Translate(key), values ?? Array.Empty<object>());
    }

    public sealed class DatasetSelectionOption : WpfObservableViewModel
    {
        private readonly string localizationKey;

        internal DatasetSelectionOption(string key, string localizationKey)
        {
            Key = key ?? string.Empty;
            this.localizationKey = localizationKey ?? string.Empty;
        }

        public string Key { get; }

        public string Value => DatasetSelectionTextFormatter.Translate(localizationKey);

        internal void RefreshLocalizedValue() => OnPropertyChanged(nameof(Value));
    }

    public sealed class RecipePreviewImageOption : WpfObservableViewModel
    {
        private readonly string displayValue;
        private readonly string localizationKey;

        internal RecipePreviewImageOption(
            RecipeImageSelectionMode mode,
            string relativePath,
            string previewImagePath,
            string displayValue = "",
            string localizationKey = "",
            bool hasMissingImage = false)
        {
            Mode = mode;
            RelativePath = relativePath ?? string.Empty;
            PreviewImagePath = previewImagePath ?? string.Empty;
            this.displayValue = displayValue ?? string.Empty;
            this.localizationKey = localizationKey ?? string.Empty;
            HasMissingImage = hasMissingImage;
        }

        public RecipeImageSelectionMode Mode { get; }

        public string RelativePath { get; }

        public string PreviewImagePath { get; }

        public bool HasMissingImage { get; }

        public string Value => HasMissingImage
            ? DatasetSelectionTextFormatter.Format("WpfRecipeMetadata.MissingImageOption", RelativePath)
            : string.IsNullOrWhiteSpace(localizationKey)
                ? displayValue
                : DatasetSelectionTextFormatter.Translate(localizationKey);

        internal void RefreshLocalizedValue() => OnPropertyChanged(nameof(Value));
    }

    public sealed class WpfDatasetSelectionWindowViewModel : WpfObservableViewModel, IDisposable
    {
        private static readonly Action NoOpCommand = () => { };
        private readonly DatasetSelectionCatalogService datasetSelectionCatalogService = new DatasetSelectionCatalogService();
        private readonly DatasetSelectionPreferencesService datasetSelectionPreferencesService;
        private readonly bool selectFirstVisibleRecipe;
        private readonly DatasetSelectionOption[] purposeFilterOptions;
        private readonly DatasetSelectionOption[] sortOptions;
        private HashSet<string> favoriteRecipeNames;
        private WpfDatasetSelectionItem selectedDataset;
        private string statusText = string.Empty;
        private string searchText = string.Empty;
        private string purposeFilterKey = "all";
        private string sortModeKey = "modified";
        private bool favoritesOnly;
        private bool needsAttentionOnly;
        private bool isOpening;
        private bool isMetadataEditing;
        private bool isSavingMetadata;
        private string editableProductCode = string.Empty;
        private string editableRecipeDescription = string.Empty;
        private string metadataStatusText = string.Empty;
        private readonly ObservableCollection<RecipePreviewImageOption> recipePreviewImageOptions = new ObservableCollection<RecipePreviewImageOption>();
        private RecipePreviewImageOption selectedRecipePreviewImageOption;
        private ImageSource editableRecipePreviewImageSource;
        private string editableRecipePreviewImageSourcePath = string.Empty;
        private Func<string, string, string, RecipeImageSelectionMode, string, RecipeConfigurationSaveResult> saveRecipeMetadata;
        private Visibility emptyStateVisibility = Visibility.Collapsed;
        private Visibility filteredEmptyStateVisibility = Visibility.Collapsed;
        private ICommand openCommand = new RelayCommand(NoOpCommand, () => false);
        private ICommand resumeLabelingCommand = new RelayCommand(NoOpCommand, () => false);
        private Action resumeLabeling;
        private ICommand createNewCommand = new RelayCommand(NoOpCommand);
        private ICommand refreshCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelCommand = new RelayCommand(NoOpCommand);
        private ICommand clearSearchCommand = new RelayCommand(NoOpCommand, () => false);
        private ICommand resetFiltersCommand = new RelayCommand(NoOpCommand, () => false);
        private readonly RelayCommand<WpfDatasetSelectionItem> toggleFavoriteCommand;
        private readonly RelayCommand escapeSearchCommand;
        private readonly RelayCommand editMetadataCommand;
        private readonly RelayCommand saveMetadataCommand;
        private readonly RelayCommand cancelMetadataCommand;
        private bool disposed;
        private string recipeRootPath = string.Empty;

        public WpfDatasetSelectionWindowViewModel()
            : this(new DatasetSelectionPreferencesService())
        {
        }

        internal WpfDatasetSelectionWindowViewModel(DatasetSelectionPreferencesService datasetSelectionPreferencesService, bool selectFirstVisibleRecipe = false)
        {
            this.selectFirstVisibleRecipe = selectFirstVisibleRecipe;
            this.datasetSelectionPreferencesService = datasetSelectionPreferencesService ?? throw new ArgumentNullException(nameof(datasetSelectionPreferencesService));
            favoriteRecipeNames = this.datasetSelectionPreferencesService.LoadFavoriteRecipeNames();
            purposeFilterOptions = new[]
            {
                new DatasetSelectionOption("all", "WpfRecipeStart.Filter.All"),
                new DatasetSelectionOption(nameof(LabelingDatasetPurpose.ObjectDetection), "WpfShell.Dataset.Purpose.ObjectDetection"),
                new DatasetSelectionOption(nameof(LabelingDatasetPurpose.Segmentation), "WpfShell.Dataset.Purpose.Segmentation"),
                new DatasetSelectionOption(nameof(LabelingDatasetPurpose.AnomalyDetection), "WpfShell.Dataset.Purpose.AnomalyDetection")
            };
            sortOptions = new[]
            {
                new DatasetSelectionOption("modified", "WpfDatasetSelection.Sort.Modified"),
                new DatasetSelectionOption("name", "WpfDatasetSelection.Sort.Name")
            };
            FilteredDatasets = CollectionViewSource.GetDefaultView(Datasets);
            FilteredDatasets.Filter = IsDatasetVisible;
            ApplySortMode();
            toggleFavoriteCommand = new RelayCommand<WpfDatasetSelectionItem>(ToggleFavorite, CanToggleFavorite);
            escapeSearchCommand = new RelayCommand(HandleSearchEscape);
            OpenRecipeOnDoubleClickCommand = new RelayCommand<MouseButtonEventArgs>(args =>
            {
                if (args?.ChangedButton == MouseButton.Left && OpenCommand?.CanExecute(null) == true) OpenCommand.Execute(null);
            }, args => args?.ChangedButton == MouseButton.Left && OpenCommand?.CanExecute(null) == true);
            editMetadataCommand = new RelayCommand(BeginMetadataEdit, CanEditSelectedMetadata);
            saveMetadataCommand = new RelayCommand(SaveRecipeMetadata, CanSaveRecipeMetadata);
            cancelMetadataCommand = new RelayCommand(CancelMetadataEdit, () => !disposed && IsMetadataEditing && !isSavingMetadata);
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            RefreshStatusText();
        }

        public string ViewName => nameof(WpfDatasetSelectionWindow);

        public string WindowTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Title");

        public string RecipeStartTitleText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Title");

        public string RecipeStartSummaryText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Summary");

        public string RecipeListIdentityHeaderText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Header.Identity");

        public string RecipeListPurposeHeaderText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Header.Purpose");

        public string RecipeListDataHeaderText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Header.Data");

        public string RecipeListOptionsText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Options");

        public string RecipeListModelHeaderText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Header.Model");

        public string DatasetWorkHistoryLabelText => DatasetSelectionTextFormatter.Translate("WpfShell.Stage.Dataset.Name");

        public string InspectionModelLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Header.Model");

        public string DecisionCriteriaLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Criteria.Title");

        public string RecipeListModifiedHeaderText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Header.Modified");

        public string DatasetSourceRuleTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.SourceRule.Title");

        public string DatasetSourceRuleDetailText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.SourceRule.Detail");

        public string ExistingDatasetGuideTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Guide.Existing.Title");

        public string ExistingDatasetGuideDetailText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Guide.Existing.Detail");

        public string CreateDatasetGuideTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Guide.Create.Title");

        public string CreateDatasetGuideDetailText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Guide.Create.Detail");

        public string CreateDatasetGuideButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Action.Create.Short");

        public string EmptyStateTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Empty.Title");

        public string EmptyStateDetailText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Empty.Detail");

        public string CreateFirstDatasetButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Action.Create");

        public string RefreshButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Action.Refresh");

        public string CreateNewButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Action.Create.Short");

        public string CancelButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Action.Cancel");

        public string OpenSelectedButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Action.Open");

        public string SearchPlaceholderText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Search.Placeholder");

        public string SelectedDatasetDetailsTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Details.Title");

        public string NoSelectedDatasetText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Details.Empty");

        public string RecipeSelectionPromptText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.SelectPrompt");

        public string NeedsAttentionToolTipText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Filter.Attention.ToolTip");

        public string PurposeFilterLabelText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Filter.AllPurposes");

        public string ClearSearchButtonText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Search.Clear");

        public string ResetFiltersButtonText => DatasetSelectionTextFormatter.Translate("WpfImageQueue.Search.ResetFilters");

        public string FavoritesOnlyText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Filter.Favorites");

        public string NeedsAttentionOnlyText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Filter.Attention");

        public string SearchNoResultsText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Search.NoResults");

        public string FavoriteSaveFailedText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Favorite.SaveFailed");

        public string EditRecipeMetadataText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.Edit");

        public string RecipeMetadataDialogTitleText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.Title");

        public string SaveRecipeMetadataText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.Save");

        public string CancelRecipeMetadataText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.Cancel");

        public string ProductCodeLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.ProductCode");

        public string RecipeDescriptionLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.Description");

        public string RecipePreviewImageLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.PreviewImage");

        public string RecipeMetadataPreviewOnlyText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.PreviewOnly");

        public string RecipeMetadataPreviewCaptionText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.PreviewCaption");

        public string RecipeMetadataOptionalFieldsText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.OptionalFields");

        public string RecipeMetadataNoImagesText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.NoImages");

        public string RecipeMetadataMissingImageText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.MissingImage");

        public string RecipeMetadataSaveFailedText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.SaveFailed");

        public string RecipeMetadataHelpText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.Help");

        public string RecipeOpenNoteText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.OpenNote");

        public string RecipeListKeyboardHelpText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.KeyboardHelp");

        public string SavedWorkHistoryTitleText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.Title");

        public string LabelingWorkHistoryLabelText => DatasetSelectionTextFormatter.Translate(
            string.Equals(SelectedDataset?.DatasetPurpose, nameof(LabelingDatasetPurpose.AnomalyDetection), StringComparison.OrdinalIgnoreCase)
                ? "WpfAnomalyLabeling.Mode" : "WpfRecipeStart.WorkHistory.Labeling");

        public string ReviewWorkHistoryLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.Review");

        public string TrainingWorkHistoryLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.Training");

        public string InferenceWorkHistoryLabelText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.Inference");

        public string RecipeCreateText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeCreateText");

        public string RecipeOpenText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeOpenText");

        public Visibility ResumeLabelingVisibility => SelectedDataset?.HasUnworkedImages == true ? Visibility.Visible : Visibility.Collapsed;

        public string RecipeSearchPlaceholderText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeSearchPlaceholderText");

        public string RecipeResetText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeResetText");

        public string RecipeImportText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeImportText");

        public string RecipePracticeText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipePracticeText");

        public string RecipePracticeToolTipText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipePracticeToolTipText");

        public string RecipeLocationsTitleText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeLocationsTitleText");

        public string RecipeSelectedText => DatasetSelectionTextFormatter.Translate("WpfRecipeLibrary.RecipeSelectedText");

        public string RecipeNoPreviewText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.NoPreview");

        public bool IsMetadataEditing
        {
            get => isMetadataEditing;
            private set
            {
                if (SetProperty(ref isMetadataEditing, value))
                {
                    OnPropertyChanged(nameof(MetadataDetailsVisibility));
                    OnPropertyChanged(nameof(MetadataEditorVisibility));
                    OnPropertyChanged(nameof(RecipeDetailsActionsVisibility));
                    OnPropertyChanged(nameof(RecipeMetadataEditorActionsVisibility));
                    RefreshMetadataCommandState();
                }
            }
        }

        public Visibility MetadataDetailsVisibility => SelectedDataset != null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MetadataEditorVisibility => SelectedDataset != null && IsMetadataEditing ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RecipeDetailsActionsVisibility => SelectedDataset != null && !IsMetadataEditing ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RecipeMetadataEditorActionsVisibility => SelectedDataset != null && IsMetadataEditing ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoSelectedRecipeVisibility => SelectedDataset == null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EditRecipeMetadataVisibility => SelectedDataset?.HasConfiguration == true ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MetadataStatusVisibility => string.IsNullOrWhiteSpace(MetadataStatusText) ? Visibility.Collapsed : Visibility.Visible;

        public ObservableCollection<RecipePreviewImageOption> RecipePreviewImageOptions => recipePreviewImageOptions;

        public RecipePreviewImageOption SelectedRecipePreviewImageOption
        {
            get => selectedRecipePreviewImageOption;
            set
            {
                if (SetProperty(ref selectedRecipePreviewImageOption, value))
                {
                    editableRecipePreviewImageSourcePath = string.Empty;
                    editableRecipePreviewImageSource = null;
                    OnPropertyChanged(nameof(EditableRecipePreviewImageSource));
                    OnPropertyChanged(nameof(RecipeMetadataPreviewImageVisibility));
                    OnPropertyChanged(nameof(RecipeMetadataPreviewNoImageVisibility));
                    OnPropertyChanged(nameof(RecipeMetadataPreviewCaptionVisibility));
                    OnPropertyChanged(nameof(RecipeMetadataPreviewStatusText));
                    OnPropertyChanged(nameof(RecipeMetadataPreviewStatusVisibility));
                    saveMetadataCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool RecipePreviewImageSelectionEnabled => recipePreviewImageOptions.Any(option =>
            option.Mode == RecipeImageSelectionMode.Selected && !option.HasMissingImage);

        public ImageSource EditableRecipePreviewImageSource
        {
            get
            {
                string imagePath = selectedRecipePreviewImageOption?.PreviewImagePath ?? string.Empty;
                if (!string.Equals(editableRecipePreviewImageSourcePath, imagePath, StringComparison.Ordinal))
                {
                    editableRecipePreviewImageSourcePath = imagePath;
                    editableRecipePreviewImageSource = string.IsNullOrWhiteSpace(imagePath)
                        ? null
                        : WpfImageQueueItem.CreateThumbnailSource(imagePath, 120);
                }

                return editableRecipePreviewImageSource;
            }
        }

        public Visibility RecipeMetadataPreviewImageVisibility => EditableRecipePreviewImageSource != null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RecipeMetadataPreviewNoImageVisibility => EditableRecipePreviewImageSource == null
            && selectedRecipePreviewImageOption?.HasMissingImage != true
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility RecipeMetadataPreviewCaptionVisibility => EditableRecipePreviewImageSource != null
            && selectedRecipePreviewImageOption?.HasMissingImage != true
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string RecipeMetadataPreviewStatusText => selectedRecipePreviewImageOption?.HasMissingImage == true
            ? RecipeMetadataMissingImageText
            : string.Empty;

        public Visibility RecipeMetadataPreviewStatusVisibility => string.IsNullOrWhiteSpace(RecipeMetadataPreviewStatusText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        public string EditableProductCode
        {
            get => editableProductCode;
            set
            {
                if (SetProperty(ref editableProductCode, value ?? string.Empty))
                {
                    saveMetadataCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string EditableRecipeDescription
        {
            get => editableRecipeDescription;
            set
            {
                if (SetProperty(ref editableRecipeDescription, value ?? string.Empty))
                {
                    saveMetadataCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string MetadataStatusText
        {
            get => metadataStatusText;
            private set
            {
                if (SetProperty(ref metadataStatusText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(MetadataStatusVisibility));
                }
            }
        }

        public IReadOnlyList<DatasetSelectionOption> PurposeFilterOptions => purposeFilterOptions;

        public IReadOnlyList<DatasetSelectionOption> SortOptions => sortOptions;

        public ObservableCollection<WpfDatasetSelectionItem> Datasets { get; } = new ObservableCollection<WpfDatasetSelectionItem>();

        public ICollectionView FilteredDatasets { get; }

        public WpfDatasetSelectionItem SelectedDataset
        {
            get => selectedDataset;
            set
            {
                if (SetProperty(ref selectedDataset, value))
                {
                    (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ResumeLabelingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    toggleFavoriteCommand.RaiseCanExecuteChanged();
                    CancelMetadataEdit();
                    RefreshMetadataCommandState();
                    OnPropertyChanged(nameof(MetadataDetailsVisibility));
                    OnPropertyChanged(nameof(MetadataEditorVisibility));
                    OnPropertyChanged(nameof(RecipeDetailsActionsVisibility));
                    OnPropertyChanged(nameof(RecipeMetadataEditorActionsVisibility));
                    OnPropertyChanged(nameof(NoSelectedRecipeVisibility));
                    OnPropertyChanged(nameof(EditRecipeMetadataVisibility));
                    OnPropertyChanged(nameof(SelectedDatasetFavoriteActionText));
                    OnPropertyChanged(nameof(LabelingWorkHistoryLabelText));
                    if (selectedDataset != null && !selectedDataset.IsWorkHistoryLoaded)
                    {
                        selectedDataset.UpdateWorkHistory(datasetSelectionCatalogService.LoadWorkHistory(
                            recipeRootPath,
                            selectedDataset.RecipeName,
                            selectedDataset.DatasetPurpose));
                    }
                    OnPropertyChanged(nameof(ResumeLabelingVisibility));
                    (ResumeLabelingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(SearchPlaceholderVisibility));
                    RefreshDatasetView();
                    (ClearSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ResetFiltersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public Visibility SearchPlaceholderVisibility => string.IsNullOrEmpty(SearchText) ? Visibility.Visible : Visibility.Collapsed;

        public string PurposeFilterKey
        {
            get => purposeFilterKey;
            set
            {
                if (SetProperty(ref purposeFilterKey, string.IsNullOrWhiteSpace(value) ? "all" : value))
                {
                    (ResetFiltersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    RefreshDatasetView();
                }
            }
        }

        public string SortModeKey
        {
            get => sortModeKey;
            set
            {
                if (SetProperty(ref sortModeKey, string.Equals(value, "name", StringComparison.OrdinalIgnoreCase) ? "name" : "modified"))
                {
                    ApplySortMode();
                    RefreshDatasetView();
                }
            }
        }

        public bool FavoritesOnly
        {
            get => favoritesOnly;
            set
            {
                if (SetProperty(ref favoritesOnly, value))
                {
                    (ResetFiltersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    RefreshDatasetView();
                }
            }
        }

        public bool NeedsAttentionOnly
        {
            get => needsAttentionOnly;
            set
            {
                if (SetProperty(ref needsAttentionOnly, value))
                {
                    (ResetFiltersCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    RefreshDatasetView();
                }
            }
        }

        public Visibility FilteredEmptyStateVisibility
        {
            get => filteredEmptyStateVisibility;
            private set => SetProperty(ref filteredEmptyStateVisibility, value);
        }

        public string SearchResultsText => DatasetSelectionTextFormatter.Format(
            "WpfDatasetSelection.Search.ResultCount",
            FilteredDatasets?.Cast<object>().Count() ?? 0,
            Datasets.Count);

        public string SelectedDatasetFavoriteActionText => SelectedDataset == null
            ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Favorite.Add")
            : DatasetSelectionTextFormatter.Translate(SelectedDataset.IsFavorite
                ? "WpfDatasetSelection.Favorite.Remove"
                : "WpfDatasetSelection.Favorite.Add");

        public ICommand ClearSearchCommand
        {
            get => clearSearchCommand;
            private set => SetProperty(ref clearSearchCommand, value);
        }

        public ICommand ResetFiltersCommand
        {
            get => resetFiltersCommand;
            private set => SetProperty(ref resetFiltersCommand, value);
        }

        public ICommand ToggleFavoriteCommand => toggleFavoriteCommand;

        public ICommand SearchEscapeCommand => escapeSearchCommand;

        public ICommand EditMetadataCommand => editMetadataCommand;

        public ICommand SaveMetadataCommand => saveMetadataCommand;

        public ICommand CancelMetadataCommand => cancelMetadataCommand;

        private bool CanResumeLabeling => CanOpenDataset && resumeLabeling != null && SelectedDataset.HasUnworkedImages;

        private bool CanOpenDataset => !disposed && !isOpening && !IsMetadataEditing && SelectedDataset != null && FilteredDatasets.Contains(SelectedDataset);

        internal bool TryBeginOpen()
        {
            if (!CanOpenDataset)
            {
                return false;
            }

            isOpening = true;
            (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResumeLabelingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            return true;
        }

        internal void EndOpen()
        {
            if (!isOpening)
            {
                return;
            }

            isOpening = false;
            (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResumeLabelingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public string StatusText
        {
            get => statusText;
            set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public Visibility EmptyStateVisibility
        {
            get => emptyStateVisibility;
            private set => SetProperty(ref emptyStateVisibility, value);
        }

        public ICommand OpenCommand
        {
            get => openCommand;
            private set => SetProperty(ref openCommand, value);
        }

        public ICommand OpenRecipeOnDoubleClickCommand { get; }

        public ICommand ResumeLabelingCommand
        {
            get => resumeLabelingCommand;
            private set => SetProperty(ref resumeLabelingCommand, value);
        }

        public ICommand CreateNewCommand
        {
            get => createNewCommand;
            private set => SetProperty(ref createNewCommand, value);
        }

        public ICommand RefreshCommand
        {
            get => refreshCommand;
            private set => SetProperty(ref refreshCommand, value);
        }

        public ICommand CancelCommand
        {
            get => cancelCommand;
            private set => SetProperty(ref cancelCommand, value);
        }

        public void ConfigureCommands(
            Action open,
            Action createNew,
            Action refresh,
            Action cancel,
            Func<string, string, string, RecipeImageSelectionMode, string, RecipeConfigurationSaveResult> saveMetadata = null,
            Action resumeLabeling = null)
        {
            if (disposed)
            {
                return;
            }

            OpenCommand = new RelayCommand(() =>
            {
                if (CanOpenDataset)
                {
                    open?.Invoke();
                }
            }, () => CanOpenDataset);
            this.resumeLabeling = resumeLabeling;
            ResumeLabelingCommand = new RelayCommand(() =>
            {
                if (CanResumeLabeling) this.resumeLabeling?.Invoke();
            }, () => CanResumeLabeling);
            CreateNewCommand = new RelayCommand(createNew ?? NoOpCommand);
            RefreshCommand = new RelayCommand(refresh ?? NoOpCommand);
            CancelCommand = new RelayCommand(cancel ?? NoOpCommand);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => !string.IsNullOrWhiteSpace(SearchText));
            ResetFiltersCommand = new RelayCommand(ResetRecipeFilters, HasActiveRecipeFilters);
            saveRecipeMetadata = saveMetadata;
            RefreshMetadataCommandState();
        }

        public void LoadDatasets(string recipeRootPath, string currentRecipeName)
        {
            if (disposed)
            {
                return;
            }

            this.recipeRootPath = recipeRootPath ?? string.Empty;
            ReleaseDatasetItems();
            IReadOnlyList<DatasetSelectionSnapshot> snapshots = datasetSelectionCatalogService.Load(
                this.recipeRootPath,
                currentRecipeName);
            foreach (DatasetSelectionSnapshot snapshot in snapshots)
            {
                WpfDatasetSelectionItem item = BuildDatasetItem(snapshot);
                item.SetFavorite(favoriteRecipeNames.Contains(item.RecipeName));
                item.UpdateWorkHistory(datasetSelectionCatalogService.LoadWorkHistory(this.recipeRootPath, item.RecipeName, item.DatasetPurpose));
                Datasets.Add(item);
            }

            SelectedDataset = Datasets.FirstOrDefault(item => item.IsCurrent)
                ?? Datasets.FirstOrDefault();
            RefreshStatusText();
            EmptyStateVisibility = Datasets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(WindowTitleText));
            OnPropertyChanged(nameof(RecipeStartTitleText));
            OnPropertyChanged(nameof(RecipeStartSummaryText));
            OnPropertyChanged(nameof(RecipeListIdentityHeaderText));
            OnPropertyChanged(nameof(RecipeListPurposeHeaderText));
            OnPropertyChanged(nameof(RecipeListDataHeaderText));
            OnPropertyChanged(nameof(RecipeListModifiedHeaderText));
            OnPropertyChanged(nameof(RecipeListOptionsText));
            OnPropertyChanged(nameof(RecipeListModelHeaderText));
            OnPropertyChanged(nameof(DatasetWorkHistoryLabelText));
            OnPropertyChanged(nameof(InspectionModelLabelText));
            OnPropertyChanged(nameof(DecisionCriteriaLabelText));
            OnPropertyChanged(nameof(DatasetSourceRuleTitleText));
            OnPropertyChanged(nameof(DatasetSourceRuleDetailText));
            OnPropertyChanged(nameof(ExistingDatasetGuideTitleText));
            OnPropertyChanged(nameof(ExistingDatasetGuideDetailText));
            OnPropertyChanged(nameof(CreateDatasetGuideTitleText));
            OnPropertyChanged(nameof(CreateDatasetGuideDetailText));
            OnPropertyChanged(nameof(CreateDatasetGuideButtonText));
            OnPropertyChanged(nameof(EmptyStateTitleText));
            OnPropertyChanged(nameof(EmptyStateDetailText));
            OnPropertyChanged(nameof(CreateFirstDatasetButtonText));
            OnPropertyChanged(nameof(RefreshButtonText));
            OnPropertyChanged(nameof(CreateNewButtonText));
            OnPropertyChanged(nameof(CancelButtonText));
            OnPropertyChanged(nameof(OpenSelectedButtonText));
            OnPropertyChanged(nameof(SearchPlaceholderText));
            OnPropertyChanged(nameof(ClearSearchButtonText));
            OnPropertyChanged(nameof(SelectedDatasetDetailsTitleText));
            OnPropertyChanged(nameof(NoSelectedDatasetText));
            OnPropertyChanged(nameof(RecipeSelectionPromptText));
            OnPropertyChanged(nameof(NeedsAttentionToolTipText));
            OnPropertyChanged(nameof(PurposeFilterLabelText));
            OnPropertyChanged(nameof(ResetFiltersButtonText));
            OnPropertyChanged(nameof(FavoritesOnlyText));
            OnPropertyChanged(nameof(NeedsAttentionOnlyText));
            OnPropertyChanged(nameof(SearchNoResultsText));
            OnPropertyChanged(nameof(EditRecipeMetadataText));
            OnPropertyChanged(nameof(RecipeMetadataDialogTitleText));
            OnPropertyChanged(nameof(SaveRecipeMetadataText));
            OnPropertyChanged(nameof(CancelRecipeMetadataText));
            OnPropertyChanged(nameof(ProductCodeLabelText));
            OnPropertyChanged(nameof(RecipeDescriptionLabelText));
            OnPropertyChanged(nameof(RecipePreviewImageLabelText));
            OnPropertyChanged(nameof(RecipeMetadataPreviewOnlyText));
            OnPropertyChanged(nameof(RecipeMetadataPreviewCaptionText));
            OnPropertyChanged(nameof(RecipeMetadataOptionalFieldsText));
            OnPropertyChanged(nameof(RecipeMetadataNoImagesText));
            OnPropertyChanged(nameof(RecipeMetadataPreviewCaptionVisibility));
            OnPropertyChanged(nameof(RecipeMetadataMissingImageText));
            OnPropertyChanged(nameof(RecipeMetadataSaveFailedText));
            OnPropertyChanged(nameof(RecipeMetadataHelpText));
            OnPropertyChanged(nameof(RecipeMetadataPreviewStatusText));
            OnPropertyChanged(nameof(RecipeMetadataPreviewStatusVisibility));
            OnPropertyChanged(nameof(RecipeOpenNoteText));
            OnPropertyChanged(nameof(RecipeListKeyboardHelpText));
            OnPropertyChanged(nameof(SavedWorkHistoryTitleText));
            OnPropertyChanged(nameof(LabelingWorkHistoryLabelText));
            OnPropertyChanged(nameof(ReviewWorkHistoryLabelText));
            OnPropertyChanged(nameof(TrainingWorkHistoryLabelText));
            OnPropertyChanged(nameof(InferenceWorkHistoryLabelText));
            OnPropertyChanged(nameof(RecipeCreateText));
            OnPropertyChanged(nameof(RecipeOpenText));
            OnPropertyChanged(nameof(ResumeLabelingVisibility));
            OnPropertyChanged(nameof(RecipeSearchPlaceholderText));
            OnPropertyChanged(nameof(RecipeResetText));
            OnPropertyChanged(nameof(RecipeImportText));
            OnPropertyChanged(nameof(RecipePracticeText));
            OnPropertyChanged(nameof(RecipePracticeToolTipText));
            OnPropertyChanged(nameof(RecipeLocationsTitleText));
            OnPropertyChanged(nameof(RecipeSelectedText));
            OnPropertyChanged(nameof(RecipeNoPreviewText));
            foreach (DatasetSelectionOption option in purposeFilterOptions.Concat(sortOptions))
            {
                option.RefreshLocalizedValue();
            }

            foreach (RecipePreviewImageOption option in recipePreviewImageOptions)
            {
                option.RefreshLocalizedValue();
            }

            OnPropertyChanged(nameof(SearchResultsText));
            OnPropertyChanged(nameof(SelectedDatasetFavoriteActionText));
            RefreshStatusText();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
            ReleaseDatasetItems();
        }

        private void ReleaseDatasetItems()
        {
            foreach (WpfDatasetSelectionItem item in Datasets)
            {
                item?.Dispose();
            }

            SelectedDataset = null;
            Datasets.Clear();
        }

        private void RefreshStatusText()
        {
            StatusText = Datasets.Count > 0
                ? DatasetSelectionTextFormatter.Format("WpfDatasetSelection.Status.Count", Datasets.Count)
                : DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Status.Empty");
            OnPropertyChanged(nameof(SearchResultsText));
            FilteredEmptyStateVisibility = Datasets.Count > 0 && (FilteredDatasets?.IsEmpty ?? true)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private bool IsDatasetVisible(object value)
        {
            if (value is not WpfDatasetSelectionItem item
                || (FavoritesOnly && !item.IsFavorite)
                || (NeedsAttentionOnly && !item.NeedsAttention)
                || (!string.Equals(PurposeFilterKey, "all", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.DatasetPurpose, PurposeFilterKey, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string query = SearchText?.Trim() ?? string.Empty;
            return query.Length == 0
                || ContainsSearchText(item.RecipeName, query)
                || ContainsSearchText(item.ProductCode, query)
                || ContainsSearchText(item.RecipeDescription, query)
                || ContainsSearchText(item.DatasetPurpose, query)
                || ContainsSearchText(item.ClassesText, query)
                || ContainsSearchText(item.OutputRootPath, query)
                || ContainsSearchText(item.ImageRootPath, query);
        }

        private static bool ContainsSearchText(string value, string query)
            => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshDatasetView()
        {
            FilteredDatasets.Refresh();
            if (SelectedDataset == null || !FilteredDatasets.Contains(SelectedDataset))
            {
                SelectedDataset = selectFirstVisibleRecipe ? FilteredDatasets.Cast<WpfDatasetSelectionItem>().FirstOrDefault() : null;
            }

            FilteredEmptyStateVisibility = Datasets.Count > 0 && FilteredDatasets.IsEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;
            OnPropertyChanged(nameof(SearchResultsText));
        }

        private void ApplySortMode()
        {
            if (FilteredDatasets == null)
            {
                return;
            }

            FilteredDatasets.SortDescriptions.Clear();
            FilteredDatasets.SortDescriptions.Add(string.Equals(SortModeKey, "name", StringComparison.OrdinalIgnoreCase)
                ? new SortDescription(nameof(WpfDatasetSelectionItem.RecipeName), ListSortDirection.Ascending)
                : new SortDescription(nameof(WpfDatasetSelectionItem.LastModifiedUtc), ListSortDirection.Descending));
        }

        private void ToggleFavorite(WpfDatasetSelectionItem item)
        {
            if (!CanToggleFavorite(item))
            {
                return;
            }

            HashSet<string> updatedFavorites = new HashSet<string>(favoriteRecipeNames, StringComparer.OrdinalIgnoreCase);
            bool isFavorite = !item.IsFavorite;
            if (isFavorite)
            {
                updatedFavorites.Add(item.RecipeName);
            }
            else
            {
                updatedFavorites.Remove(item.RecipeName);
            }

            if (!datasetSelectionPreferencesService.SaveFavoriteRecipeNames(updatedFavorites))
            {
                StatusText = FavoriteSaveFailedText;
                return;
            }

            favoriteRecipeNames = updatedFavorites;
            item.SetFavorite(isFavorite);
            OnPropertyChanged(nameof(SelectedDatasetFavoriteActionText));
            RefreshDatasetView();
        }

        private void HandleSearchEscape()
        {
            if (IsMetadataEditing)
            {
                CancelMetadataEdit();
                return;
            }

            if (ClearSearchCommand.CanExecute(null))
            {
                ClearSearchCommand.Execute(null);
                return;
            }

            if (CancelCommand.CanExecute(null))
            {
                CancelCommand.Execute(null);
            }
        }

        private bool HasActiveRecipeFilters()
            => !string.IsNullOrWhiteSpace(SearchText)
                || !string.Equals(PurposeFilterKey, "all", StringComparison.OrdinalIgnoreCase)
                || FavoritesOnly
                || NeedsAttentionOnly;

        private void ResetRecipeFilters()
        {
            SearchText = string.Empty;
            PurposeFilterKey = "all";
            FavoritesOnly = false;
            NeedsAttentionOnly = false;
        }

        private bool CanToggleFavorite(WpfDatasetSelectionItem item)
            => !disposed && item != null && Datasets.Contains(item);

        private bool CanEditSelectedMetadata()
            => !disposed && !isOpening && !IsMetadataEditing && SelectedDataset?.HasConfiguration == true && saveRecipeMetadata != null;

        private bool CanSaveRecipeMetadata()
            => !disposed
                && IsMetadataEditing
                && !isSavingMetadata
                && SelectedDataset?.HasConfiguration == true
                && EditableProductCode.Trim().Length <= LabelingProjectSettings.RecipeProductCodeMaxLength
                && EditableRecipeDescription.Trim().Length <= LabelingProjectSettings.RecipeDescriptionMaxLength
                && SelectedRecipePreviewImageOption != null
                && (!string.Equals(EditableProductCode.Trim(), SelectedDataset.ProductCode, StringComparison.Ordinal)
                    || !string.Equals(EditableRecipeDescription.Trim(), SelectedDataset.RecipeDescription, StringComparison.Ordinal)
                    || SelectedRecipePreviewImageOption.Mode != SelectedDataset.PreviewImageMode
                    || !string.Equals(SelectedRecipePreviewImageOption.RelativePath, SelectedDataset.PreviewImageRelativePath, StringComparison.OrdinalIgnoreCase));

        private void BeginMetadataEdit()
        {
            if (!CanEditSelectedMetadata())
            {
                return;
            }

            ResetEditableRecipeMetadata();
            MetadataStatusText = string.Empty;
            IsMetadataEditing = true;
        }

        private void SaveRecipeMetadata()
        {
            if (!CanSaveRecipeMetadata())
            {
                return;
            }

            isSavingMetadata = true;
            RefreshMetadataCommandState();
            try
            {
                string productCode = EditableProductCode.Trim();
                string description = EditableRecipeDescription.Trim();
                RecipePreviewImageOption previewOption = SelectedRecipePreviewImageOption;
                RecipeImageSelectionMode previewMode = previewOption.Mode;
                string previewRelativePath = previewMode == RecipeImageSelectionMode.Selected
                    ? previewOption.RelativePath
                    : string.Empty;
                RecipeConfigurationSaveResult result = saveRecipeMetadata?.Invoke(
                    SelectedDataset.RecipeName,
                    productCode,
                    description,
                    previewMode,
                    previewRelativePath);
                if (result?.IsSuccess != true)
                {
                    MetadataStatusText = RecipeMetadataSaveFailedText;
                    return;
                }

                SelectedDataset.UpdateRecipeMetadata(
                    productCode,
                    description,
                    previewMode,
                    previewRelativePath,
                    previewOption.PreviewImagePath,
                    previewOption.HasMissingImage);
                RefreshDatasetView();
                MetadataStatusText = string.Empty;
                IsMetadataEditing = false;
            }
            catch
            {
                MetadataStatusText = RecipeMetadataSaveFailedText;
            }
            finally
            {
                isSavingMetadata = false;
                RefreshMetadataCommandState();
            }
        }

        private void CancelMetadataEdit()
        {
            if (!IsMetadataEditing)
            {
                return;
            }

            ResetEditableRecipeMetadata();
            MetadataStatusText = string.Empty;
            IsMetadataEditing = false;
        }

        private void ResetEditableRecipeMetadata()
        {
            EditableProductCode = SelectedDataset?.ProductCode ?? string.Empty;
            EditableRecipeDescription = SelectedDataset?.RecipeDescription ?? string.Empty;
            LoadRecipePreviewImageOptions(SelectedDataset);
        }

        private void LoadRecipePreviewImageOptions(WpfDatasetSelectionItem recipe)
        {
            recipePreviewImageOptions.Clear();
            IReadOnlyList<string> imagePaths = datasetSelectionCatalogService.LoadRecipePreviewImagePaths(recipe?.ImageRootPath);
            string automaticPreviewPath = imagePaths.FirstOrDefault() ?? recipe?.PreviewImagePath ?? string.Empty;
            recipePreviewImageOptions.Add(new RecipePreviewImageOption(
                RecipeImageSelectionMode.Automatic,
                string.Empty,
                automaticPreviewPath,
                localizationKey: "WpfRecipeMetadata.PreviewAuto"));

            foreach (string imagePath in imagePaths)
            {
                string relativePath = Path.GetRelativePath(recipe.ImageRootPath, imagePath);
                recipePreviewImageOptions.Add(new RecipePreviewImageOption(
                    RecipeImageSelectionMode.Selected,
                    relativePath,
                    imagePath,
                    displayValue: relativePath));
            }

            RecipePreviewImageOption selectedImageOption = recipePreviewImageOptions.FirstOrDefault(option =>
                option.Mode == RecipeImageSelectionMode.Selected
                && string.Equals(option.RelativePath, recipe?.PreviewImageRelativePath, StringComparison.OrdinalIgnoreCase));
            if (recipe?.PreviewImageMode == RecipeImageSelectionMode.Selected && selectedImageOption == null)
            {
                selectedImageOption = new RecipePreviewImageOption(
                    RecipeImageSelectionMode.Selected,
                    recipe.PreviewImageRelativePath,
                    automaticPreviewPath,
                    hasMissingImage: true);
                recipePreviewImageOptions.Add(selectedImageOption);
            }

            recipePreviewImageOptions.Add(new RecipePreviewImageOption(
                RecipeImageSelectionMode.Hidden,
                string.Empty,
                string.Empty,
                localizationKey: "WpfRecipeMetadata.PreviewNone"));
            OnPropertyChanged(nameof(RecipePreviewImageOptions));
            OnPropertyChanged(nameof(RecipePreviewImageSelectionEnabled));
            RecipePreviewImageOption storedOption = recipe == null
                ? null
                : recipePreviewImageOptions.FirstOrDefault(option =>
                    option.Mode == recipe.PreviewImageMode
                    && (option.Mode != RecipeImageSelectionMode.Selected
                        || string.Equals(option.RelativePath, recipe.PreviewImageRelativePath, StringComparison.OrdinalIgnoreCase)));
            SelectedRecipePreviewImageOption = storedOption
                ?? recipePreviewImageOptions.FirstOrDefault(option => option.Mode == RecipeImageSelectionMode.Automatic);
        }

        private void RefreshMetadataCommandState()
        {
            editMetadataCommand.RaiseCanExecuteChanged();
            saveMetadataCommand.RaiseCanExecuteChanged();
            cancelMetadataCommand.RaiseCanExecuteChanged();
            (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResumeLabelingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private static WpfDatasetSelectionItem BuildDatasetItem(DatasetSelectionSnapshot snapshot)
        {
            string purposeKey = GetDatasetPurposeKey(snapshot?.DatasetPurpose);
            string classesText = snapshot?.Classes?.Count > 0
                ? string.Join(", ", snapshot.Classes)
                : string.Empty;
            return new WpfDatasetSelectionItem(
                snapshot?.RecipeName,
                purposeKey,
                snapshot?.OutputRootPath,
                snapshot?.ImageRootPath,
                classesText,
                snapshot?.ImageCount ?? 0,
                snapshot?.LabelCount ?? 0,
                snapshot?.ManifestPath,
                snapshot?.HasManifest == true,
                snapshot?.IsCurrent == true,
                snapshot?.DatasetPurpose,
                snapshot?.LastModifiedUtc ?? DateTime.MinValue,
                snapshot?.ProductCode,
                snapshot?.RecipeDescription,
                snapshot?.HasConfiguration == true,
                snapshot?.PreviewImagePath,
                snapshot?.PreviewImageMode ?? RecipeImageSelectionMode.Automatic,
                snapshot?.PreviewImageRelativePath,
                snapshot?.HasMissingPreviewImage == true,
                snapshot?.LabelClasses?.Count > 0
                    ? snapshot.LabelClasses.Select((item, index) => new DatasetWorkspaceClassItem(new WpfClassCatalogListItem(item, index), 0)).ToArray()
                    : snapshot?.Classes?.Select((name, index) => new DatasetWorkspaceClassItem(name, 0, Brushes.Transparent, index + 1)).ToArray());
        }

        private static string GetDatasetPurposeKey(string purpose)
        {
            if (Enum.TryParse(purpose, out LabelingDatasetPurpose parsed))
            {
                return parsed switch
                {
                    LabelingDatasetPurpose.Segmentation => "WpfShell.Dataset.Purpose.Segmentation",
                    LabelingDatasetPurpose.AnomalyDetection => "WpfShell.Dataset.Purpose.AnomalyDetection",
                    _ => "WpfShell.Dataset.Purpose.ObjectDetection"
                };
            }

            return "WpfShell.Dataset.Purpose.Unselected";
        }

    }

    public sealed class WpfDatasetSelectionItem : WpfObservableViewModel, IDisposable
    {
        private bool disposed;
        private DatasetSelectionWorkHistorySnapshot workHistory = new DatasetSelectionWorkHistorySnapshot();
        private bool isFavorite;
        private bool previewImageLoaded;
        private ImageSource previewImageSource;
        private ImageSource coverImageSource;
        private bool coverImageLoaded;
        private string previewImagePath = string.Empty;
        private bool hasMissingPreviewImage;

        public WpfDatasetSelectionItem(
            string recipeName,
            string purposeKey,
            string outputRootPath,
            string imageRootPath,
            string classesText,
            int imageCount,
            int labelCount,
            string manifestPath,
            bool hasManifest,
            bool isCurrent,
            string datasetPurpose = "",
            DateTime lastModifiedUtc = default,
            string productCode = "",
            string recipeDescription = "",
            bool hasConfiguration = false,
            string previewImagePath = "",
            RecipeImageSelectionMode previewImageMode = RecipeImageSelectionMode.Automatic,
            string previewImageRelativePath = "",
            bool hasMissingPreviewImage = false,
            IReadOnlyList<DatasetWorkspaceClassItem> classItems = null)
        {
            RecipeName = recipeName ?? string.Empty;
            PurposeKey = purposeKey ?? "WpfShell.Dataset.Purpose.Unselected";
            OutputRootPath = outputRootPath ?? string.Empty;
            ImageRootPath = imageRootPath ?? string.Empty;
            ClassesText = classesText ?? string.Empty;
            ClassItems = classItems?.ToArray() ?? Array.Empty<DatasetWorkspaceClassItem>();
            ImageCount = imageCount;
            LabelCount = labelCount;
            ManifestPath = manifestPath ?? string.Empty;
            HasManifest = hasManifest;
            IsCurrent = isCurrent;
            DatasetPurpose = datasetPurpose ?? string.Empty;
            LastModifiedUtc = lastModifiedUtc;
            ProductCode = productCode ?? string.Empty;
            RecipeDescription = recipeDescription ?? string.Empty;
            HasConfiguration = hasConfiguration;
            PreviewImagePath = previewImagePath;
            PreviewImageMode = previewImageMode;
            PreviewImageRelativePath = previewImageRelativePath ?? string.Empty;
            this.hasMissingPreviewImage = hasMissingPreviewImage;
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

        public string RecipeName { get; }

        public string PurposeKey { get; }

        public string DatasetPurpose { get; }

        public string PurposeText => DatasetSelectionTextFormatter.Translate(PurposeKey);

        public string OutputRootPath { get; }

        public string ImageRootPath { get; }

        public string ClassesText { get; }

        public IReadOnlyList<DatasetWorkspaceClassItem> ClassItems { get; }

        public string ClassCountText => DatasetSelectionTextFormatter.Format("WpfRecipeLibrary.ClassCountText", ClassItems.Count);

        public int ImageCount { get; }

        public int LabelCount { get; }

        public string ManifestPath { get; }

        public bool HasManifest { get; }

        public bool IsCurrent { get; }

        public DateTime LastModifiedUtc { get; private set; }

        public string ProductCode { get; private set; }

        public string RecipeDescription { get; private set; }

        public string ProductCodeDisplayText => string.IsNullOrWhiteSpace(ProductCode)
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.NotSet")
            : ProductCode;

        public string RecipeDescriptionDisplayText => string.IsNullOrWhiteSpace(RecipeDescription)
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.NoDescription")
            : RecipeDescription;

        public bool HasConfiguration { get; }

        public string PreviewImagePath
        {
            get => previewImagePath;
            private set => previewImagePath = value ?? string.Empty;
        }

        public RecipeImageSelectionMode PreviewImageMode { get; private set; }

        public string PreviewImageRelativePath { get; private set; }

        public bool HasMissingPreviewImage
        {
            get => hasMissingPreviewImage;
            private set
            {
                if (SetProperty(ref hasMissingPreviewImage, value))
                {
                    OnPropertyChanged(nameof(MissingPreviewImageVisibility));
                    OnPropertyChanged(nameof(MissingPreviewImageText));
                }
            }
        }

        public Visibility MissingPreviewImageVisibility => HasMissingPreviewImage ? Visibility.Visible : Visibility.Collapsed;

        public string MissingPreviewImageText => DatasetSelectionTextFormatter.Translate("WpfRecipeMetadata.MissingImage");

        public ImageSource PreviewImageSource
        {
            get
            {
                if (!previewImageLoaded)
                {
                    previewImageLoaded = true;
                    previewImageSource = WpfImageQueueItem.CreateThumbnailSource(PreviewImagePath, 64);
                }

                return previewImageSource;
            }
        }

        public ImageSource CoverImageSource
        {
            get
            {
                if (!coverImageLoaded)
                {
                    coverImageLoaded = true;
                    coverImageSource = WpfImageQueueItem.CreateThumbnailSource(PreviewImagePath, 400);
                }
                return coverImageSource;
            }
        }

        public Visibility PreviewImageVisibility => PreviewImageSource != null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoPreviewVisibility => PreviewImageSource == null ? Visibility.Visible : Visibility.Collapsed;

        public bool NeedsAttention => !HasManifest || ImageCount == 0;

        public bool IsFavorite
        {
            get => isFavorite;
            private set
            {
                if (SetProperty(ref isFavorite, value))
                {
                    OnPropertyChanged(nameof(FavoriteGlyph));
                    OnPropertyChanged(nameof(FavoriteActionText));
                }
            }
        }

        public string FavoriteGlyph => IsFavorite ? "★" : "☆";

        public string FavoriteActionText => DatasetSelectionTextFormatter.Translate(IsFavorite
            ? "WpfDatasetSelection.Favorite.Remove"
            : "WpfDatasetSelection.Favorite.Add");

        internal void SetFavorite(bool value) => IsFavorite = value;

        internal void UpdateRecipeMetadata(
            string productCode,
            string recipeDescription,
            RecipeImageSelectionMode previewImageMode,
            string previewImageRelativePath,
            string previewImagePath,
            bool hasMissingPreviewImage)
        {
            LastModifiedUtc = DateTime.UtcNow;
            OnPropertyChanged(nameof(LastModifiedUtc));
            OnPropertyChanged(nameof(SummaryUpdatedText));
            if (!string.Equals(ProductCode, productCode, StringComparison.Ordinal))
            {
                ProductCode = productCode ?? string.Empty;
                OnPropertyChanged(nameof(ProductCode));
                OnPropertyChanged(nameof(ProductCodeDisplayText));
            }

            if (!string.Equals(RecipeDescription, recipeDescription, StringComparison.Ordinal))
            {
                RecipeDescription = recipeDescription ?? string.Empty;
                OnPropertyChanged(nameof(RecipeDescription));
                OnPropertyChanged(nameof(RecipeDescriptionDisplayText));
            }

            PreviewImageMode = previewImageMode;
            PreviewImageRelativePath = previewImageRelativePath ?? string.Empty;
            HasMissingPreviewImage = hasMissingPreviewImage;
            if (!string.Equals(PreviewImagePath, previewImagePath, StringComparison.Ordinal))
            {
                PreviewImagePath = previewImagePath;
                previewImageLoaded = false;
                previewImageSource = null;
                coverImageSource = null;
                coverImageLoaded = false;
                OnPropertyChanged(nameof(CoverImageSource));
                OnPropertyChanged(nameof(PreviewImagePath));
                OnPropertyChanged(nameof(PreviewImageSource));
                OnPropertyChanged(nameof(PreviewImageVisibility));
                OnPropertyChanged(nameof(NoPreviewVisibility));
            }

            OnPropertyChanged(nameof(PreviewImageMode));
            OnPropertyChanged(nameof(PreviewImageRelativePath));

            OnPropertyChanged(nameof(ModifiedDateText));
            OnPropertyChanged(nameof(RecipeListModifiedDateText));
        }

        public string ToolTipText => DatasetSelectionTextFormatter.Format(
            "WpfDatasetSelection.Item.Tooltip",
            string.IsNullOrWhiteSpace(OutputRootPath) ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.StorageUnknown") : OutputRootPath,
            string.IsNullOrWhiteSpace(ImageRootPath) ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.ImageRootUnknown") : ImageRootPath);

        public string StoragePathText => DatasetSelectionTextFormatter.Format(
            "WpfDatasetSelection.Item.StoragePath",
            string.IsNullOrWhiteSpace(OutputRootPath) ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.StorageUnknown") : OutputRootPath);

        public string ImageRootPathText => DatasetSelectionTextFormatter.Format(
            "WpfDatasetSelection.Item.ImageRootPath",
            string.IsNullOrWhiteSpace(ImageRootPath) ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.ImageRootUnknown") : ImageRootPath);

        public string RecipeFolderPathText => DatasetSelectionTextFormatter.Format("WpfRecipeLibrary.RecipeFolderPath", Path.GetDirectoryName(ManifestPath));

        public string ClassesLabelText => DatasetSelectionTextFormatter.Format(
            "WpfDatasetSelection.Item.Classes",
            string.IsNullOrWhiteSpace(ClassesText) ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.ClassesUnknown") : ClassesText);

        public string OpenActionText => DatasetSelectionTextFormatter.Translate(IsCurrent
            ? "WpfDatasetSelection.Item.OpenAction.Current"
            : "WpfDatasetSelection.Item.OpenAction.Open");

        public string StatusText => DatasetSelectionTextFormatter.Translate(IsCurrent
            ? "WpfDatasetSelection.Item.Status.Current"
            : (HasManifest ? "WpfDatasetSelection.Item.Status.Ready" : "WpfDatasetSelection.Item.Status.Configured"));

        public string CountText => DatasetSelectionTextFormatter.Format("WpfDatasetSelection.Item.Count", ImageCount, LabelCount);

        public bool IsWorkHistoryLoaded { get; private set; }

        public bool HasUnworkedImages => workHistory.UnworkedImageCount > 0;

        public string ResumeLabelingText => DatasetSelectionTextFormatter.Format("WpfRecipeLibrary.ResumeLabelingText", workHistory.UnworkedImageCount ?? 0);

        public int CompletedImageCount => IsWorkHistoryLoaded && workHistory.UnworkedImageCount.HasValue
            ? Math.Clamp(workHistory.ReviewImageCount - workHistory.UnworkedImageCount.Value, 0, workHistory.ReviewImageCount) : 0;

        public string LabelingProgressText => !workHistory.UnworkedImageCount.HasValue
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Progress.Unknown")
            : DatasetSelectionTextFormatter.Format("WpfRecipeStart.Progress.Summary", CompletedImageCount, workHistory.ReviewImageCount, workHistory.ReviewedImageCount);

        public string LabelingWorkHistoryText => !workHistory.UnworkedImageCount.HasValue
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Progress.Unknown")
            : DatasetSelectionTextFormatter.Format("WpfRecipeStart.Progress.Detail", CompletedImageCount, workHistory.ReviewImageCount, workHistory.ReviewedImageCount, workHistory.NeedsFixImageCount);

        public GridLength ReviewedProgressWidth => new GridLength(Math.Min(CompletedImageCount, workHistory.ReviewedImageCount), GridUnitType.Star);
        public GridLength LabeledProgressWidth => new GridLength(Math.Max(0, CompletedImageCount - workHistory.ReviewedImageCount), GridUnitType.Star);
        public GridLength RemainingProgressWidth => new GridLength(Math.Max(0, workHistory.ReviewImageCount - CompletedImageCount), GridUnitType.Star);
        public Visibility ProgressVisibility => workHistory.UnworkedImageCount.HasValue && workHistory.ReviewImageCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string DatasetWorkHistoryText => !workHistory.IsReviewAvailable
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Dataset.Unknown")
            : DatasetSelectionTextFormatter.Format("WpfRecipeStart.Dataset.Summary", workHistory.ReviewImageCount, workHistory.TrainImageCount, workHistory.ValidImageCount, workHistory.TestImageCount, workHistory.ExcludedImageCount);

        public string InspectionModelText => !workHistory.IsConfigurationAvailable
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Model.Unknown")
            : !workHistory.HasInspectionModel ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Model.NotAssigned")
            : string.IsNullOrWhiteSpace(workHistory.InspectionModelName) ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.ModelUnknown") : workHistory.InspectionModelName;

        public bool HasInspectionModel => workHistory.HasInspectionModel;

        public string RecipeListModelText => workHistory.IsConfigurationAvailable && !HasInspectionModel
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.NoneYet") : InspectionModelText;

        public string RecipeTrainingStageText => IsWorkHistoryLoaded && workHistory.IsConfigurationAvailable && !workHistory.HasTrainingRecord
            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.NoneYet")
            : workHistory.IsLatestTrainingCompleted && workHistory.TrainingRunCount > 0
                ? DatasetSelectionTextFormatter.Format("WpfRecipeStart.Training.RecordCount", workHistory.TrainingRunCount) : TrainingStageText;

        public string InspectionMetricsText => !workHistory.HasInspectionModel ? string.Empty
            : string.IsNullOrWhiteSpace(workHistory.InspectionMetrics) ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Model.NoMetrics")
            : ModelRegistryPresentationService.BuildCompactMetricSummary(workHistory.InspectionMetrics);

        public string DecisionCriteriaText
        {
            get
            {
                if (!workHistory.HasInspectionModel) return DatasetSelectionTextFormatter.Translate("WpfRecipeStart.Criteria.NoModel");
                ModelDecisionRule rule = workHistory.InspectionDecisionRule;
                if (rule == null) return DatasetSelectionTextFormatter.Translate("WpfComparisonWorkspace." + workHistory.DecisionRuleStatus);
                string text = rule.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection
                    ? DatasetSelectionTextFormatter.Format("WpfComparisonWorkspace.RuleAnomalySummary", rule.AnomalyScoreThreshold)
                    : DatasetSelectionTextFormatter.Format("WpfComparisonWorkspace.RuleDetectionSummary", rule.Confidence, rule.MinimumShortSidePixels, rule.MinimumDefectCount);
                ModelDecisionTestSummary test = rule.FixedTestSummary;
                return text + Environment.NewLine + (test == null ? DatasetSelectionTextFormatter.Translate("WpfComparisonWorkspace.RuleNeedsEvaluation")
                    : DatasetSelectionTextFormatter.Format("WpfComparisonWorkspace.RuleFixedTestSummary", test.Misses, test.NgCount, test.Extras, test.OkCount));
            }
        }
        public string DecisionCriteriaReadinessGlyph => workHistory.InspectionDecisionRule?.FixedTestSummary == null ? "!" : "✓";
        public string DatasetReadinessGlyph => workHistory.IsReviewAvailable && workHistory.ReviewImageCount > 0 ? "✓" : "!";
        public string LabelingReadinessGlyph => workHistory.ReviewImageCount == 0 || !workHistory.UnworkedImageCount.HasValue ? "!" : workHistory.UnworkedImageCount.Value == 0 ? "✓" : "…";
        public string TrainingReadinessGlyph => !workHistory.HasTrainingRecord ? "·" : workHistory.IsLatestTrainingCompleted ? "✓" : "!";
        public string TrainingStageText => workHistory.HasTrainingRecord && !workHistory.IsLatestTrainingCompleted
            ? DatasetSelectionTextFormatter.Format("WpfRecipeStart.Training.Unconfirmed", TrainingWorkHistoryText) : TrainingWorkHistoryText;
        public string InspectionReadinessGlyph => workHistory.HasInspectionModel ? "✓" : "·";

        public string ReviewWorkHistoryText
        {
            get
            {
                if (!IsWorkHistoryLoaded || !workHistory.IsReviewAvailable)
                {
                    return DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.ReviewUnknown");
                }

                return string.Equals(DatasetPurpose, nameof(LabelingDatasetPurpose.AnomalyDetection), StringComparison.OrdinalIgnoreCase)
                    ? DatasetSelectionTextFormatter.Format(
                        "WpfRecipeStart.WorkHistory.AnomalySummary",
                        workHistory.ReviewedImageCount,
                        workHistory.ReviewImageCount,
                        workHistory.NormalImageCount,
                        workHistory.AbnormalImageCount)
                    : DatasetSelectionTextFormatter.Format(
                        "WpfRecipeStart.WorkHistory.QualitySummary",
                        workHistory.ReviewedImageCount,
                        workHistory.ReviewImageCount,
                        workHistory.NeedsFixImageCount);
            }
        }

        public string TrainingWorkHistoryText
        {
            get
            {
                if (!IsWorkHistoryLoaded || !workHistory.IsConfigurationAvailable)
                {
                    return DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.TrainingUnknown");
                }

                if (!workHistory.HasTrainingRecord)
                {
                    return DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.TrainingEmpty");
                }

                return workHistory.TrainingRunCount == 0
                    ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.TrainingCountUnknown")
                    : DatasetSelectionTextFormatter.Format(
                        "WpfRecipeStart.WorkHistory.TrainingSummary",
                        workHistory.TrainingRunCount,
                        string.IsNullOrWhiteSpace(workHistory.LatestTrainingName)
                            ? DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.ModelUnknown")
                            : workHistory.LatestTrainingName);
            }
        }

        public string InferenceWorkHistoryText => DatasetSelectionTextFormatter.Translate("WpfRecipeStart.WorkHistory.InferenceUnknown");

        internal void UpdateWorkHistory(DatasetSelectionWorkHistorySnapshot snapshot)
        {
            workHistory = snapshot ?? new DatasetSelectionWorkHistorySnapshot();
            IsWorkHistoryLoaded = true;
            OnPropertyChanged(nameof(IsWorkHistoryLoaded));
            OnPropertyChanged(nameof(HasUnworkedImages));
            OnPropertyChanged(nameof(LabelingWorkHistoryText));
            OnPropertyChanged(nameof(ResumeLabelingText));
            OnPropertyChanged(nameof(ReviewWorkHistoryText));
            OnPropertyChanged(nameof(TrainingWorkHistoryText));
            OnPropertyChanged(nameof(InferenceWorkHistoryText));
            OnPropertyChanged(nameof(CompletedImageCount));
            OnPropertyChanged(nameof(LabelingProgressText));
            OnPropertyChanged(nameof(ReviewedProgressWidth));
            OnPropertyChanged(nameof(LabeledProgressWidth));
            OnPropertyChanged(nameof(RemainingProgressWidth));
            OnPropertyChanged(nameof(ProgressVisibility));
            OnPropertyChanged(nameof(DatasetWorkHistoryText));
            OnPropertyChanged(nameof(InspectionModelText));
            OnPropertyChanged(nameof(HasInspectionModel));
            OnPropertyChanged(nameof(RecipeListModelText));
            OnPropertyChanged(nameof(RecipeTrainingStageText));
            OnPropertyChanged(nameof(InspectionMetricsText));
            OnPropertyChanged(nameof(DecisionCriteriaText));
            OnPropertyChanged(nameof(DecisionCriteriaReadinessGlyph));
            OnPropertyChanged(nameof(DatasetReadinessGlyph));
            OnPropertyChanged(nameof(LabelingReadinessGlyph));
            OnPropertyChanged(nameof(TrainingReadinessGlyph));
            OnPropertyChanged(nameof(TrainingStageText));
            OnPropertyChanged(nameof(InspectionReadinessGlyph));
        }

        public string ListCountText => DatasetSelectionTextFormatter.Format("WpfDatasetSelection.Item.ListCount", ImageCount, LabelCount);

        public string ModifiedDateText => LastModifiedUtc == DateTime.MinValue
            ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.ModifiedUnknown")
            : LastModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.CurrentCulture);

        public string RecipeListModifiedDateText => LastModifiedUtc == DateTime.MinValue
            ? DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Item.ModifiedUnknown")
            : LastModifiedUtc.ToLocalTime().ToString("MM-dd HH:mm", System.Globalization.CultureInfo.CurrentCulture);

        public string SummaryUpdatedText => HasManifest
            ? DatasetSelectionTextFormatter.Format(
                "WpfDatasetSelection.Item.SummaryUpdated",
                LastModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.CurrentCulture))
            : string.Empty;

        public PackIconMaterialKind IconKind => HasManifest ? PackIconMaterialKind.DatabaseCheckOutline : PackIconMaterialKind.DatabaseAlertOutline;

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            OnPropertyChanged(nameof(PurposeText));
            OnPropertyChanged(nameof(ToolTipText));
            OnPropertyChanged(nameof(StoragePathText));
            OnPropertyChanged(nameof(ImageRootPathText));
            OnPropertyChanged(nameof(ClassesLabelText));
            OnPropertyChanged(nameof(ClassCountText));
            OnPropertyChanged(nameof(RecipeFolderPathText));
            OnPropertyChanged(nameof(OpenActionText));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(ListCountText));
            OnPropertyChanged(nameof(SummaryUpdatedText));
            OnPropertyChanged(nameof(ModifiedDateText));
            OnPropertyChanged(nameof(RecipeListModifiedDateText));
            OnPropertyChanged(nameof(ProductCodeDisplayText));
            OnPropertyChanged(nameof(RecipeDescriptionDisplayText));
            OnPropertyChanged(nameof(MissingPreviewImageText));
            OnPropertyChanged(nameof(FavoriteGlyph));
            OnPropertyChanged(nameof(FavoriteActionText));
            OnPropertyChanged(nameof(LabelingWorkHistoryText));
            OnPropertyChanged(nameof(ResumeLabelingText));
            OnPropertyChanged(nameof(ReviewWorkHistoryText));
            OnPropertyChanged(nameof(TrainingWorkHistoryText));
            OnPropertyChanged(nameof(InferenceWorkHistoryText));
            OnPropertyChanged(nameof(CompletedImageCount));
            OnPropertyChanged(nameof(LabelingProgressText));
            OnPropertyChanged(nameof(ReviewedProgressWidth));
            OnPropertyChanged(nameof(LabeledProgressWidth));
            OnPropertyChanged(nameof(RemainingProgressWidth));
            OnPropertyChanged(nameof(ProgressVisibility));
            OnPropertyChanged(nameof(DatasetWorkHistoryText));
            OnPropertyChanged(nameof(InspectionModelText));
            OnPropertyChanged(nameof(HasInspectionModel));
            OnPropertyChanged(nameof(RecipeListModelText));
            OnPropertyChanged(nameof(RecipeTrainingStageText));
            OnPropertyChanged(nameof(InspectionMetricsText));
            OnPropertyChanged(nameof(DecisionCriteriaText));
            OnPropertyChanged(nameof(DatasetReadinessGlyph));
            OnPropertyChanged(nameof(DecisionCriteriaReadinessGlyph));
            OnPropertyChanged(nameof(LabelingReadinessGlyph));
            OnPropertyChanged(nameof(TrainingReadinessGlyph));
            OnPropertyChanged(nameof(TrainingStageText));
            OnPropertyChanged(nameof(InspectionReadinessGlyph));
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

    }
}
