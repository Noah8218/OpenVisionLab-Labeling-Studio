using MvcVisionSystem._1._Core;
using System;
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

            if (!Directory.Exists(imageRootPath))
            {
                context.AppendLog?.Invoke($"설정된 이미지 루트가 없습니다: {imageRootPath}");
                return;
            }

            if (!string.Equals(
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
            string selectedPath = context.SelectFolder?.Invoke("이미지 폴더 선택", currentRoot) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.EnsureProjectSettings?.Invoke();
            context.SetConfiguredImageRootPath?.Invoke(selectedPath);
            SaveCurrentImageRootToRecipe(selectedPath);
            _ = LoadImageQueueAsync(selectedPath, string.Empty, loadFirstImage: true);
            context.RefreshShellDatasetContext?.Invoke();
        }

        internal void SaveCurrentImageRootToRecipe(string selectedPath)
        {
            string recipeName = context.CurrentRecipeNameProvider?.Invoke() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                context.AppendLog?.Invoke($"이미지 폴더 선택: {selectedPath}");
                return;
            }

            try
            {
                context.ProjectRecipeSessionService.Save(context.DataProvider(), recipeName);
                context.PopulateYoloEditorFields?.Invoke();
                context.PopulateProjectConfigPanelFields?.Invoke();
                context.AppendLog?.Invoke($"이미지 폴더 저장: {selectedPath}");
            }
            catch (Exception exception)
            {
                context.AppendLog?.Invoke($"이미지 폴더 저장 실패: {exception.Message}");
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
                context.AppendLog?.Invoke($"현재 이미지 폴더를 열 수 없습니다: {root}");
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
            if (!Directory.Exists(root))
            {
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
