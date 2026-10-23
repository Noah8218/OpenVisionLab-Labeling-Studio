using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns Image Queue folder-root commands and their Recipe persistence handoff.
    /// Queue selection/navigation remains with the existing selection service and
    /// Shell facade; this adapter only coordinates root changes and refreshes.
    /// </summary>
    internal sealed class ImageQueueRootCommandAdapter
    {
        private readonly ImageQueueRootCommandAdapterContext context;
        private bool isAddingSourceFolder;

        internal ImageQueueRootCommandAdapter(ImageQueueRootCommandAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal void ExecuteLoadImageRootCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.EnsureProjectSettings?.Invoke();
            string imageRootPath = context.ResolveConfiguredImageRootPath?.Invoke() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(imageRootPath) && context.IsModelWorkflowCreated?.Invoke() == true)
            {
                imageRootPath = context.ModelImageRootPath?.Invoke()?.Trim() ?? string.Empty;
            }

            if (!Directory.Exists(imageRootPath) && context.DataProvider()?.ProjectSettings?.AdditionalImageRootPaths?.Count > 0)
                imageRootPath = context.DataProvider().ProjectSettings.ResolveImageRootPaths().FirstOrDefault(Directory.Exists) ?? imageRootPath;
            if (!Directory.Exists(imageRootPath))
            {
                context.SetCurrentImageFolder?.Invoke(imageRootPath, false);
                context.AppendLog?.Invoke($"설정된 이미지 루트가 없습니다: {imageRootPath}");
                return;
            }

            if (context.DataProvider()?.ProjectSettings?.AdditionalImageRootPaths?.Count == 0 && !string.Equals(
                context.ResolveConfiguredImageRootPath?.Invoke(),
                imageRootPath,
                StringComparison.OrdinalIgnoreCase))
            {
                context.SetConfiguredImageRootPath?.Invoke(imageRootPath);
                SaveCurrentImageRootToRecipe(imageRootPath);
            }

            _ = LoadImageQueueAsync(imageRootPath, context.ActiveImagePathProvider?.Invoke() ?? string.Empty, loadFirstImage: true);
        }

        internal void ExecuteBrowseImageFolderCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string currentImageRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
            string currentRoot = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : string.Empty;
            string selectedPath = context.SelectFolder?.Invoke("원본 이미지 폴더 선택", currentRoot) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.EnsureProjectSettings?.Invoke();
            LabelingProjectSettings settings = context.DataProvider().ProjectSettings;
            string previousRoot = settings.ImageRootPath;
            string previousLegacyRoot = settings.PythonModel.ImageRootPath;
            List<string> previousAdditionalRoots = settings.AdditionalImageRootPaths;
            settings.AdditionalImageRootPaths = new List<string>();
            context.SetConfiguredImageRootPath?.Invoke(selectedPath);
            if (!TrySaveCurrentImageRootToRecipe(selectedPath) && previousAdditionalRoots?.Count > 0)
            {
                settings.ImageRootPath = previousRoot;
                settings.PythonModel.ImageRootPath = previousLegacyRoot;
                settings.AdditionalImageRootPaths = previousAdditionalRoots;
                context.PopulateYoloEditorFields?.Invoke();
                context.PopulateProjectConfigPanelFields?.Invoke();
                return;
            }
            _ = LoadImageQueueAsync(selectedPath, string.Empty, loadFirstImage: true);
            context.RefreshShellDatasetContext?.Invoke();
        }

        internal async Task ExecuteAddImageFolderCommandAsync()
        {
            if (isAddingSourceFolder || context.IsApplicationCloseApproved?.Invoke() == true) return;
            isAddingSourceFolder = true;
            context.SetAddingSourceFolder?.Invoke(true);
            LabelingProjectData data = context.DataProvider();
            string initialRecipeName = context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty;
            LabelingProjectSettings settings = data?.ProjectSettings;
            string previousRoot = settings?.ImageRootPath;
            string previousLegacyRoot = settings?.PythonModel?.ImageRootPath;
            List<string> previousAdditionalRoots = settings?.AdditionalImageRootPaths;
            bool changedSettings = false;
            bool saved = false;
            try
            {
                if (settings == null) return;
                string currentRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
                string selected = context.SelectFolder?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfDatasetWorkspace.AddFolder.Title"),
                    Directory.Exists(currentRoot) ? currentRoot : string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(selected) || context.IsApplicationCloseApproved?.Invoke() == true || !ReferenceEquals(data, context.DataProvider())) return;
                selected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selected));
                if (!Directory.Exists(selected))
                {
                    ShowSourceFolderStatus("AddFolder.Unavailable", selected);
                    return;
                }
                var selection = context.ImageQueueSelectionService ?? new ImageQueueSelectionService();
                List<string> selectedImages = selection.EnumerateImageFiles(selected);
                if (selectedImages.Count == 0)
                {
                    ShowSourceFolderStatus("AddFolder.Empty", selected);
                    return;
                }
                string configuredPrimary = settings.ResolveImageRootPath();
                bool emptyDefaultRoot = new DatasetImageRootResolver().IsImplicitDefaultImageRoot(configuredPrimary) && !selection.HasImageFiles(configuredPrimary);
                IReadOnlyList<string> currentQueue = context.QueueImagePathsProvider?.Invoke() ?? Array.Empty<string>();
                string[] currentRoots = settings.ResolveImageRootPaths()
                    .Where(root => !emptyDefaultRoot || !string.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPrimary)), StringComparison.OrdinalIgnoreCase))
                    .Concat(currentQueue.Count > 0 && !string.IsNullOrWhiteSpace(currentRoot) ? new[] { Path.GetFullPath(currentRoot) } : Array.Empty<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                string[] currentImages = selection.EnumerateImageFiles(currentRoots)
                    .Concat(currentQueue).Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                IGrouping<string, string>[] existingGroups = currentImages.GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase).ToArray();
                IGrouping<string, string> conflictingGroup = existingGroups.FirstOrDefault(group => group.Count() > 1);
                if (conflictingGroup != null)
                {
                    ShowSourceFolderStatus("AddFolder.NameConflict", conflictingGroup.First(), conflictingGroup.Skip(1).First());
                    return;
                }
                var existingPaths = new HashSet<string>(currentImages, StringComparer.OrdinalIgnoreCase);
                string[] newImages = selectedImages.Select(Path.GetFullPath).Where(path => !existingPaths.Contains(path)).ToArray();
                if (newImages.Length == 0)
                {
                    ShowSourceFolderStatus("AddFolder.AlreadyAdded");
                    return;
                }
                var names = existingGroups.ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
                foreach (string path in newImages)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (names.TryGetValue(name, out string existing))
                    {
                        ShowSourceFolderStatus("AddFolder.NameConflict", existing, path);
                        return;
                    }
                    names.Add(name, path);
                    string savedImage = data.GetDatasetImageDirectories().Where(Directory.Exists)
                        .SelectMany(directory => Directory.EnumerateFiles(directory, name + ".*", SearchOption.TopDirectoryOnly)).FirstOrDefault();
                    if (savedImage != null)
                    {
                        using System.Drawing.Image image = System.Drawing.Image.FromFile(path);
                        try { YoloAnnotationService.EnsureImageIdentity(Path.GetFileName(path), image, data, path); }
                        catch (YoloImageIdentityCollisionException)
                        {
                            ShowSourceFolderStatus("AddFolder.NameConflict", savedImage, path);
                            return;
                        }
                    }
                }
                if (context.IsApplicationCloseApproved?.Invoke() == true || !ReferenceEquals(data, context.DataProvider())) return;
                string recipeName = initialRecipeName;
                if (!string.Equals(recipeName, context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty, StringComparison.Ordinal)) return;
                if (string.IsNullOrWhiteSpace(recipeName))
                {
                    ShowSourceFolderStatus("AddFolder.RecipeRequired");
                    return;
                }
                string primaryRoot = settings.ResolveImageRootPath();
                if (string.IsNullOrWhiteSpace(primaryRoot) || emptyDefaultRoot) settings.ImageRootPath = currentRoots.FirstOrDefault() ?? selected;
                settings.AdditionalImageRootPaths = currentRoots.Where(root => !string.Equals(root, settings.ResolveImageRootPath(), StringComparison.OrdinalIgnoreCase))
                    .Append(selected).Where(root => !string.Equals(root, settings.ResolveImageRootPath(), StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                changedSettings = true;
                RecipeConfigurationSaveResult result = context.PersistSourceFolders?.Invoke(data, recipeName)
                    ?? context.ProjectRecipeSessionService.SaveConfiguration(data, recipeName, updateYoloDataYaml: false,
                        refreshDatasetVersion: settings.ResolveImageRootPaths().All(Directory.Exists));
                if (!result.IsSuccess) throw new IOException(result.ErrorMessage);
                saved = true;
                context.PopulateYoloEditorFields?.Invoke();
                context.PopulateProjectConfigPanelFields?.Invoke();
                string loadRoot = settings.ResolveImageRootPaths().First(Directory.Exists);
                int imageCount = await LoadImageQueueAsync(loadRoot, context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                    loadFirstImage: context.QueueItemCountProvider?.Invoke() == 0);
                if (context.IsApplicationCloseApproved?.Invoke() != true && ReferenceEquals(data, context.DataProvider())
                    && string.Equals(recipeName, context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty, StringComparison.Ordinal))
                {
                    context.RefreshShellDatasetContext?.Invoke();
                    ShowSourceFolderStatus(imageCount > 0 ? "AddFolder.Added" : "AddFolder.RefreshFailed", newImages.Length);
                }
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or OutOfMemoryException or AggregateException)
            {
                if (changedSettings && !saved)
                {
                    settings.ImageRootPath = previousRoot;
                    settings.PythonModel.ImageRootPath = previousLegacyRoot;
                    settings.AdditionalImageRootPaths = previousAdditionalRoots;
                }
                if (context.IsApplicationCloseApproved?.Invoke() != true && ReferenceEquals(data, context.DataProvider())
                    && string.Equals(initialRecipeName, context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty, StringComparison.Ordinal))
                    ShowSourceFolderStatus("AddFolder.Failed");
                context.AppendLog?.Invoke(error.ToString());
            }
            finally
            {
                isAddingSourceFolder = false;
                context.SetAddingSourceFolder?.Invoke(false);
            }
        }

        private void ShowSourceFolderStatus(string key, params object[] arguments)
        {
            context.ShowSourceFolderStatus?.Invoke(key, arguments);
            context.AppendLog?.Invoke(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfDatasetWorkspace." + key), arguments));
        }

        internal void SaveCurrentImageRootToRecipe(string selectedPath) => TrySaveCurrentImageRootToRecipe(selectedPath);

        private bool TrySaveCurrentImageRootToRecipe(string selectedPath)
        {
            string recipeName = context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                context.AppendLog?.Invoke(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueRootCommandAdapter.FolderSelected"), selectedPath));
                return true;
            }

            try
            {
                context.ProjectRecipeSessionService.Save(context.DataProvider(), recipeName);
                context.PopulateYoloEditorFields?.Invoke();
                context.PopulateProjectConfigPanelFields?.Invoke();
                context.AppendLog?.Invoke(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueRootCommandAdapter.FolderSaved"), selectedPath));
                return true;
            }
            catch (Exception exception)
            {
                context.AppendLog?.Invoke(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueRootCommandAdapter.FolderSaveFailed"), exception.Message));
                return false;
            }
        }

        internal void ExecuteOpenCurrentImageFolderCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string currentImageRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : context.ResolveConfiguredImageRootPath?.Invoke() ?? string.Empty;
            if (!Directory.Exists(root))
            {
                context.AppendLog?.Invoke(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueRootCommandAdapter.FolderOpenFailed"), root));
                context.SetCurrentImageFolder?.Invoke(root, false);
                return;
            }

            context.OpenFolder?.Invoke(root);
        }

        internal void ExecuteRefreshImageQueueCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string currentImageRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : context.ResolveConfiguredImageRootPath?.Invoke() ?? string.Empty;
            if (!Directory.Exists(root) && context.DataProvider()?.ProjectSettings?.AdditionalImageRootPaths?.Count > 0)
                root = context.DataProvider().ProjectSettings.ResolveImageRootPaths().FirstOrDefault(Directory.Exists) ?? root;
            if (!Directory.Exists(root))
            {
                context.SetCurrentImageFolder?.Invoke(root, false);
                context.AppendLog?.Invoke($"이미지 루트가 없습니다: {root}");
                return;
            }

            _ = LoadImageQueueAsync(
                root,
                context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                loadFirstImage: context.QueueItemCountProvider?.Invoke() == 0);
        }

        private Task<int> LoadImageQueueAsync(string imageRoot, string selectedImagePath, bool loadFirstImage)
            => context.LoadImageQueueAsync?.Invoke(imageRoot, selectedImagePath, loadFirstImage)
                ?? Task.FromResult(0);
    }

    internal sealed class ImageQueueRootCommandAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ProjectRecipeSessionService ProjectRecipeSessionService { get; init; }
        internal ImageQueueSelectionService ImageQueueSelectionService { get; init; }
        internal Func<IReadOnlyList<string>> QueueImagePathsProvider { get; init; }
        internal Func<LabelingProjectData, string, RecipeConfigurationSaveResult> PersistSourceFolders { get; init; }
        internal Action<bool> SetAddingSourceFolder { get; init; }
        internal Action<string, object[]> ShowSourceFolderStatus { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Func<string> ResolveConfiguredImageRootPath { get; init; }
        internal Func<bool> IsModelWorkflowCreated { get; init; }
        internal Func<string> ModelImageRootPath { get; init; }
        internal Action<string> SetConfiguredImageRootPath { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<string> CurrentImageRootProvider { get; init; }
        internal Func<string, string, string> SelectFolder { get; init; }
        internal Func<string, string, bool, Task<int>> LoadImageQueueAsync { get; init; }
        internal Func<int> QueueItemCountProvider { get; init; }
        internal Action RefreshShellDatasetContext { get; init; }
        internal Action<string, bool> SetCurrentImageFolder { get; init; }
        internal Action<string> OpenFolder { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Action PopulateYoloEditorFields { get; init; }
        internal Action PopulateProjectConfigPanelFields { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
