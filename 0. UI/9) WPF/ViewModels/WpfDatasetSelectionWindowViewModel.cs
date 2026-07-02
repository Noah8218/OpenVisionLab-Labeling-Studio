using MahApps.Metro.IconPacks;
using Lib.Common;
using Newtonsoft.Json;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSelectionWindowViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private WpfDatasetSelectionItem selectedDataset;
        private string statusText = "\uC5F4 \uB370\uC774\uD130\uC14B\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
        private Visibility emptyStateVisibility = Visibility.Collapsed;
        private ICommand openCommand = new RelayCommand(NoOpCommand);
        private ICommand createNewCommand = new RelayCommand(NoOpCommand);
        private ICommand refreshCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfDatasetSelectionWindow);

        public string DatasetSourceRuleTitleText => "\uC791\uC5C5 \uAE30\uC900\uC740 \uC800\uC7A5 \uD3F4\uB354\uC785\uB2C8\uB2E4";

        public string DatasetSourceRuleDetailText => "\uC774\uBBF8\uC9C0 \uD3F4\uB354\uB294 \uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uBAA9\uB85D\uB9CC \uAC00\uC838\uC635\uB2C8\uB2E4. \uB77C\uBCA8, Recipe, \uD559\uC2B5 \uACB0\uACFC\uB294 \uC800\uC7A5 \uD3F4\uB354\uC5D0 \uC788\uC73C\uBBC0\uB85C \uC0C8 \uC791\uC5C5\uC740 \uC0C8 \uC800\uC7A5 \uD3F4\uB354\uB85C \uBD84\uB9AC\uD558\uC138\uC694.";

        public string ExistingDatasetGuideTitleText => "\uAE30\uC874 \uB370\uC774\uD130\uC14B \uC5F4\uAE30";

        public string ExistingDatasetGuideDetailText => "\uBAA9\uB85D\uC5D0\uC11C \uC120\uD0DD\uD558\uBA74 \uD574\uB2F9 \uC800\uC7A5 \uD3F4\uB354\uC758 \uB77C\uBCA8\uACFC \uD559\uC2B5 \uAE30\uB85D\uC744 \uADF8\uB300\uB85C \uBD88\uB7EC\uC635\uB2C8\uB2E4.";

        public string CreateDatasetGuideTitleText => "\uC0C8 \uC800\uC7A5 \uD3F4\uB354\uB85C \uB9CC\uB4E4\uAE30";

        public string CreateDatasetGuideDetailText => "\uAC19\uC740 \uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC368\uB3C4 \uC0C8 \uC800\uC7A5 \uD3F4\uB354\uBA74 \uAE30\uC874 \uB77C\uBCA8\uC744 \uBD88\uB7EC\uC624\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";

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
            OpenCommand = new RelayCommand(open ?? NoOpCommand);
            CreateNewCommand = new RelayCommand(createNew ?? NoOpCommand);
            RefreshCommand = new RelayCommand(refresh ?? NoOpCommand);
            CancelCommand = new RelayCommand(cancel ?? NoOpCommand);
        }

        public void LoadDatasets(string recipeRootPath, string currentRecipeName)
        {
            Datasets.Clear();
            IReadOnlyList<string> recipeNames = WpfProjectRecipeService.ListRecipeNames(recipeRootPath);
            foreach (string recipeName in recipeNames)
            {
                Datasets.Add(BuildDatasetItem(recipeRootPath, recipeName, currentRecipeName));
            }

            SelectedDataset = Datasets.FirstOrDefault(item => item.IsCurrent)
                ?? Datasets.FirstOrDefault();
            StatusText = Datasets.Count > 0
                ? $"\uC120\uD0DD \uAC00\uB2A5\uD55C \uB370\uC774\uD130\uC14B {Datasets.Count}\uAC1C"
                : "\uC5F4 \uC218 \uC788\uB294 \uB370\uC774\uD130\uC14B\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uC0C8 \uB370\uC774\uD130\uC14B\uC744 \uB9CC\uB4E4\uC5B4\uC8FC\uC138\uC694.";
            EmptyStateVisibility = Datasets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static WpfDatasetSelectionItem BuildDatasetItem(string recipeRootPath, string recipeName, string currentRecipeName)
        {
            string manifestPath = WpfProjectRecipeService.BuildManifestPath(recipeRootPath, recipeName);
            string configPath = WpfProjectRecipeService.BuildConfigPath(recipeRootPath, recipeName);
            LabelingDatasetManifest manifest = TryReadManifest(manifestPath);
            CData recipeData = TryReadRecipeConfig(configPath);
            string purposeText = FormatDatasetPurpose(manifest?.DatasetPurpose);
            string outputRootPath = FirstNonEmpty(manifest?.OutputRootPath, recipeData?.OutputRootPath);
            string imageRootPath = FirstNonEmpty(manifest?.ImageRootPath, recipeData?.ProjectSettings?.PythonModel?.ImageRootPath);
            List<string> configClasses = recipeData?.ClassNamedList?
                .Select(item => item?.Text)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList()
                ?? new List<string>();
            IReadOnlyList<string> classes = manifest?.Classes?.Count > 0
                ? manifest.Classes
                : configClasses;
            string classesText = classes.Count > 0
                ? string.Join(", ", classes.Take(4)) + (classes.Count > 4 ? " ..." : string.Empty)
                : "\uD074\uB798\uC2A4 \uBBF8\uC815";
            int imageCount = manifest?.ArtifactSummary?.ImageCount ?? 0;
            int labelCount = manifest?.ArtifactSummary?.PrimaryLabelCount ?? 0;
            return new WpfDatasetSelectionItem(
                recipeName,
                purposeText,
                string.IsNullOrWhiteSpace(outputRootPath) ? "\uC800\uC7A5 \uACBD\uB85C \uBBF8\uD655\uC778" : outputRootPath,
                string.IsNullOrWhiteSpace(imageRootPath) ? "\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uBBF8\uD655\uC778" : imageRootPath,
                classesText,
                imageCount,
                labelCount,
                manifestPath,
                File.Exists(manifestPath),
                string.Equals(recipeName, currentRecipeName, StringComparison.OrdinalIgnoreCase));
        }

        private static LabelingDatasetManifest TryReadManifest(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<LabelingDatasetManifest>(File.ReadAllText(manifestPath));
            }
            catch
            {
                return null;
            }
        }

        private static CData TryReadRecipeConfig(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return null;
            }

            try
            {
                CData data = SerializeHelper.FromXmlFile<CData>(configPath);
                data?.NormalizeOutputPaths();
                return data;
            }
            catch
            {
                return null;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string FormatDatasetPurpose(string purpose)
        {
            if (Enum.TryParse(purpose, out LabelingDatasetPurpose parsed))
            {
                return parsed switch
                {
                    LabelingDatasetPurpose.Segmentation => "\uC138\uADF8\uBA58\uD14C\uC774\uC158",
                    LabelingDatasetPurpose.AnomalyDetection => "\uC774\uC0C1 \uD0D0\uC9C0",
                    _ => "\uAC1D\uCCB4 \uD0D0\uC9C0"
                };
            }

            return "\uBAA9\uC801 \uBBF8\uD655\uC778";
        }
    }

    public sealed class WpfDatasetSelectionItem
    {
        public WpfDatasetSelectionItem(
            string recipeName,
            string purposeText,
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
            PurposeText = purposeText ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            ImageRootPath = imageRootPath ?? string.Empty;
            ClassesText = classesText ?? string.Empty;
            ImageCount = imageCount;
            LabelCount = labelCount;
            ManifestPath = manifestPath ?? string.Empty;
            HasManifest = hasManifest;
            IsCurrent = isCurrent;
        }

        public string RecipeName { get; }

        public string PurposeText { get; }

        public string OutputRootPath { get; }

        public string ImageRootPath { get; }

        public string ClassesText { get; }

        public int ImageCount { get; }

        public int LabelCount { get; }

        public string ManifestPath { get; }

        public bool HasManifest { get; }

        public bool IsCurrent { get; }

        public string ToolTipText => $"\uC800\uC7A5: {OutputRootPath}\n\uC774\uBBF8\uC9C0: {ImageRootPath}";

        public string StoragePathText => $"\uB77C\uBCA8/Recipe \uC800\uC7A5: {OutputRootPath}";

        public string ImageRootPathText => $"\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354: {ImageRootPath}";

        public string OpenActionText => IsCurrent ? "\uD604\uC7AC \uC5F4\uB824 \uC788\uC74C" : "\uC120\uD0DD \uD6C4 \uC5F4\uAE30";

        public string StatusText => IsCurrent ? "\uD604\uC7AC \uC791\uC5C5 \uC911" : (HasManifest ? "\uC5F4\uAE30 \uAC00\uB2A5" : "\uC124\uC815\uB9CC \uC788\uC74C");

        public string CountText => $"\uC774\uBBF8\uC9C0 {ImageCount}\uAC1C / \uB77C\uBCA8 {LabelCount}\uAC1C";

        public PackIconMaterialKind IconKind => HasManifest ? PackIconMaterialKind.DatabaseCheckOutline : PackIconMaterialKind.DatabaseAlertOutline;
    }
}
