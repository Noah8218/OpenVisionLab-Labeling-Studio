using MahApps.Metro.IconPacks;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MvcVisionSystem
{
    internal static class DatasetSelectionTextFormatter
    {
        public static string Translate(string key) => OpenVisionLanguageService.T(key);

        public static string Format(string key, params object[] values)
            => string.Format(System.Globalization.CultureInfo.CurrentCulture, Translate(key), values ?? Array.Empty<object>());
    }

    public sealed class WpfDatasetSelectionWindowViewModel : WpfObservableViewModel, IDisposable
    {
        private static readonly Action NoOpCommand = () => { };
        private readonly DatasetSelectionCatalogService datasetSelectionCatalogService = new DatasetSelectionCatalogService();
        private WpfDatasetSelectionItem selectedDataset;
        private string statusText = string.Empty;
        private Visibility emptyStateVisibility = Visibility.Collapsed;
        private ICommand openCommand = new RelayCommand(NoOpCommand);
        private ICommand createNewCommand = new RelayCommand(NoOpCommand);
        private ICommand refreshCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelCommand = new RelayCommand(NoOpCommand);
        private bool disposed;

        public WpfDatasetSelectionWindowViewModel()
        {
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            RefreshStatusText();
        }

        public string ViewName => nameof(WpfDatasetSelectionWindow);

        public string WindowTitleText => DatasetSelectionTextFormatter.Translate("WpfDatasetSelection.Title");

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

        public ObservableCollection<WpfDatasetSelectionItem> Datasets { get; } = new ObservableCollection<WpfDatasetSelectionItem>();

        public WpfDatasetSelectionItem SelectedDataset
        {
            get => selectedDataset;
            set => SetProperty(ref selectedDataset, value);
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

        public void ConfigureCommands(Action open, Action createNew, Action refresh, Action cancel)
        {
            if (disposed)
            {
                return;
            }

            OpenCommand = new RelayCommand(open ?? NoOpCommand);
            CreateNewCommand = new RelayCommand(createNew ?? NoOpCommand);
            RefreshCommand = new RelayCommand(refresh ?? NoOpCommand);
            CancelCommand = new RelayCommand(cancel ?? NoOpCommand);
        }

        public void LoadDatasets(string recipeRootPath, string currentRecipeName)
        {
            if (disposed)
            {
                return;
            }

            ReleaseDatasetItems();
            IReadOnlyList<DatasetSelectionSnapshot> snapshots = datasetSelectionCatalogService.Load(
                recipeRootPath,
                currentRecipeName);
            foreach (DatasetSelectionSnapshot snapshot in snapshots)
            {
                Datasets.Add(BuildDatasetItem(snapshot));
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
        }

        private static WpfDatasetSelectionItem BuildDatasetItem(DatasetSelectionSnapshot snapshot)
        {
            string purposeKey = GetDatasetPurposeKey(snapshot?.DatasetPurpose);
            string classesText = snapshot?.Classes?.Count > 0
                ? string.Join(", ", snapshot.Classes.Take(4)) + (snapshot.Classes.Count > 4 ? " ..." : string.Empty)
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
                snapshot?.IsCurrent == true);
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
            bool isCurrent)
        {
            RecipeName = recipeName ?? string.Empty;
            PurposeKey = purposeKey ?? "WpfShell.Dataset.Purpose.Unselected";
            OutputRootPath = outputRootPath ?? string.Empty;
            ImageRootPath = imageRootPath ?? string.Empty;
            ClassesText = classesText ?? string.Empty;
            ImageCount = imageCount;
            LabelCount = labelCount;
            ManifestPath = manifestPath ?? string.Empty;
            HasManifest = hasManifest;
            IsCurrent = isCurrent;
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

        public string RecipeName { get; }

        public string PurposeKey { get; }

        public string PurposeText => DatasetSelectionTextFormatter.Translate(PurposeKey);

        public string OutputRootPath { get; }

        public string ImageRootPath { get; }

        public string ClassesText { get; }

        public int ImageCount { get; }

        public int LabelCount { get; }

        public string ManifestPath { get; }

        public bool HasManifest { get; }

        public bool IsCurrent { get; }

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
            OnPropertyChanged(nameof(OpenActionText));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CountText));
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
