using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Owns the project-settings workflow that used to be spread across the
    // Shell partial. The Shell supplies only live UI callbacks and dependent
    // panel refresh actions.
    internal sealed class ProjectSettingsWorkflowAdapter
    {
        private readonly ProjectSettingsWorkflowAdapterContext context;
        private bool isRecipeSelectionRefreshActive;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal ProjectSettingsWorkflowAdapter(ProjectSettingsWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.ApplicationState);
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal bool IsRecipeSelectionRefreshActive => isRecipeSelectionRefreshActive;

        #region ProjectSettingsWorkflow

        internal void SetProjectConfigStatus(string message)
            => context.SetProjectConfigStatus?.Invoke(message ?? string.Empty);

        internal void HandleProjectConfigSaved(string configPath)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            PopulateProjectConfigPanelFields();
            SetProjectConfigStatus($"설정 저장 완료: {DateTime.Now:HH:mm:ss}");
            context.SetDatasetStatus?.Invoke($"데이터셋: 설정 저장 {Path.GetFileName(configPath)}");
            context.AppendLog?.Invoke($"프로젝트 설정 저장: {configPath}");
        }

        internal void HandleProjectRecipeApplied(string recipeName, ProjectRecipeApplyResult result)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true || result?.IsApplied != true)
            {
                return;
            }

            CompleteProjectRecipeApply(result.PreviousRecipeName, recipeName);
        }

        internal void HandleProjectConfigWorkflowError(string message)
        {
            if (context.IsApplicationCloseApproved?.Invoke() != true && !string.IsNullOrWhiteSpace(message))
            {
                context.AppendLog?.Invoke(message);
            }
        }

        internal string GetCurrentRecipeName()
            => context.ApplicationState?.Recipe?.Name?.Trim() ?? string.Empty;

        internal string GetCurrentRecipeConfigDirectory()
            => ProjectRecipeService.BuildConfigDirectory(ProjectRecipeService.GetRecipeRootDirectory(), GetCurrentRecipeName());

        internal string GetCurrentRecipeConfigPath()
            => ProjectRecipeService.BuildConfigPath(ProjectRecipeService.GetRecipeRootDirectory(), GetCurrentRecipeName());

        internal string ResolveConfiguredImageRootPath()
        {
            EnsureProjectSettings();
            return projectData.ProjectSettings?.ResolveImageRootPath() ?? string.Empty;
        }

        internal void SetConfiguredImageRootPath(string imageRootPath)
        {
            EnsureProjectSettings();
            projectData.ProjectSettings.ImageRootPath = imageRootPath ?? string.Empty;
        }

        internal bool TryPickFile(string title, string filter, string currentPath, out string selectedPath)
        {
            selectedPath = context.SelectFile?.Invoke(title, filter, currentPath) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(selectedPath);
        }

        internal bool TryPickFolder(string title, string currentPath, out string selectedPath)
        {
            selectedPath = context.SelectFolder?.Invoke(title, currentPath) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(selectedPath);
        }

        internal void PopulateProjectConfigPanelFields()
        {
            string recipeName = GetCurrentRecipeName();
            string configPath = GetCurrentRecipeConfigPath();
            context.ProjectConfigViewModel?.LoadFrom(recipeName, ProjectRecipeService.GetRecipeRootDirectory());
            if (context.ProjectConfigViewModel != null)
            {
                context.ProjectConfigViewModel.SetDatasetVersionInfo(
                    RecipeDatasetVersionPresentationService.Build(context.ProjectConfigViewModel.ManifestPath));
            }

            PopulateProjectRecipeList(recipeName);

            SetProjectConfigStatus(string.IsNullOrWhiteSpace(recipeName)
                ? "Recipe 이름이 아직 없습니다. 저장 전에 recipe를 선택하거나 생성해야 합니다."
                : $"현재 설정 파일: {Path.GetFileName(configPath)}");
            context.UpdateYoloCommandButtons?.Invoke();
        }

        internal bool PopulateProjectRecipeList(string selectedRecipeName)
        {
            WpfProjectConfigPanelViewModel viewModel = context.ProjectConfigViewModel;
            if (viewModel == null)
            {
                return false;
            }

            isRecipeSelectionRefreshActive = true;
            try
            {
                IReadOnlyList<string> recipeNames = ProjectRecipeService.ListRecipeNames(ProjectRecipeService.GetRecipeRootDirectory());
                string matchingRecipeName = recipeNames
                    .FirstOrDefault(name => string.Equals(name, selectedRecipeName, StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;
                viewModel.SetRecipeList(recipeNames, matchingRecipeName);
                return true;
            }
            catch (Exception ex)
            {
                viewModel.SetRecipeList(Array.Empty<string>(), string.Empty);
                SetProjectConfigStatus($"Recipe 목록 읽기 실패: {ex.Message}");
                context.AppendLog?.Invoke($"Recipe 목록 읽기 실패: {ex.Message}");
                return false;
            }
            finally
            {
                isRecipeSelectionRefreshActive = false;
            }
        }

        internal ProjectConfigNavigationCallbacks CreateProjectConfigNavigationCallbacks()
            => new ProjectConfigNavigationCallbacks
            {
                RefreshRecipeList = PopulateProjectRecipeList,
                CurrentRecipeName = GetCurrentRecipeName,
                OpenProjectConfigFolder = OpenProjectConfigFolder,
                IsApplicationCloseApproved = () => context.IsApplicationCloseApproved?.Invoke() == true
            };

        internal ProjectConfigArchiveCallbacks CreateProjectConfigArchiveCallbacks()
            => new ProjectConfigArchiveCallbacks
            {
                BuildApplicationCloseState = context.BuildApplicationCloseState,
                CurrentRecipeName = GetCurrentRecipeName,
                CurrentRecipeConfigPath = GetCurrentRecipeConfigPath,
                CurrentRecipeConfigDirectory = GetCurrentRecipeConfigDirectory,
                RecipeRootDirectory = ProjectRecipeService.GetRecipeRootDirectory,
                DefaultDatasetParentDirectory = context.DefaultDatasetParentDirectory,
                SelectSaveFile = (title, filter, currentPath) => context.SelectSaveFile?.Invoke(title, filter, currentPath) ?? string.Empty,
                SelectFile = (title, filter, currentPath) => context.SelectFile?.Invoke(title, filter, currentPath) ?? string.Empty,
                SelectFolder = (title, currentPath) => context.SelectFolder?.Invoke(title, currentPath) ?? string.Empty,
                ImportedRecipeLoaded = recipeName =>
                {
                    PopulateProjectRecipeList(recipeName);
                    context.ProjectConfigViewModel?.SelectRecipeFromList(recipeName);
                },
                AppendLog = context.AppendLog,
                IsApplicationCloseApproved = context.IsApplicationCloseApproved
            };

        internal void OpenProjectConfigFolder()
        {
            string directoryPath = string.IsNullOrWhiteSpace(GetCurrentRecipeName())
                ? ProjectRecipeService.GetRecipeRootDirectory()
                : GetCurrentRecipeConfigDirectory();

            try
            {
                Directory.CreateDirectory(directoryPath);
                context.OpenFolder?.Invoke(directoryPath);
                SetProjectConfigStatus($"폴더 열기: {directoryPath}");
                context.AppendLog?.Invoke($"Recipe 설정 폴더 열기: {directoryPath}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"폴더 열기 실패: {ex.Message}");
                context.AppendLog?.Invoke($"Recipe 설정 폴더 열기 실패: {ex.Message}");
            }
        }

        internal bool SaveProjectConfigFromPanel()
            => SaveProjectConfigFromPanelCore(recipeName =>
                context.ProjectRecipeSessionService.Save(projectData, recipeName));

        internal RecipeConfigurationSaveResult SaveCurrentRecipeConfiguration(string recipeName)
            => context.ProjectRecipeSessionService.SaveConfiguration(
                projectData,
                recipeName,
                updateYoloDataYaml: false,
                refreshDatasetVersion: false);

        internal bool SaveModelMetadataConfigFromPanel()
            => SaveProjectConfigFromPanelCore(recipeName =>
                context.ProjectRecipeSessionService.Save(projectData, recipeName, refreshDatasetVersion: false));

        internal bool SaveProjectConfigFromPanelCore(Func<string, string> saveRecipe)
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus("Recipe 이름이 없어 설정을 저장하지 않았습니다.");
                return false;
            }

            try
            {
                string configPath = saveRecipe(recipeName);
                PopulateProjectConfigPanelFields();
                SetProjectConfigStatus($"설정 저장 완료: {DateTime.Now:HH:mm:ss}");
                context.SetDatasetStatus?.Invoke($"데이터셋: 설정 저장 {Path.GetFileName(configPath)}");
                context.AppendLog?.Invoke($"프로젝트 설정 저장: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"설정 저장 실패: {ex.Message}");
                context.AppendLog?.Invoke($"프로젝트 설정 저장 실패: {ex.Message}");
                return false;
            }
        }

        internal async Task<bool> ApplyProjectRecipeFromPanelAsync()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return false;
            }

            context.CancelFourPointBoxDraft?.Invoke();
            string recipeName = context.ProjectConfigViewModel?.RecipeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus("적용할 recipe 이름을 입력하세요.");
                return false;
            }

            if (!ProjectRecipeService.IsValidRecipeName(recipeName))
            {
                SetProjectConfigStatus("Recipe 이름에 사용할 수 없는 문자가 있습니다.");
                return false;
            }

            try
            {
                ProjectRecipeApplyResult applyResult = await context.ProjectRecipeApplyWorkflowService.ApplyAsync(
                    context.ApplicationState,
                    recipeName);
                if (!applyResult.IsApplied || context.IsApplicationCloseApproved?.Invoke() == true)
                {
                    return false;
                }

                CompleteProjectRecipeApply(applyResult.PreviousRecipeName, recipeName);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                if (context.IsApplicationCloseApproved?.Invoke() == true)
                {
                    return false;
                }

                SetProjectConfigStatus($"Recipe 적용 실패: {ex.Message}");
                context.AppendLog?.Invoke($"Recipe 적용 실패: {ex.Message}");
                return false;
            }
        }

        internal void CompleteProjectRecipeApply(string previousRecipeName, string recipeName)
        {
            context.RememberLastOpenedDatasetRecipe?.Invoke(recipeName);
            EnsureProjectSettings();
            context.ApplyProjectDatasetPurposeToWorkflow?.Invoke();
            PopulateProjectConfigPanelFields();
            context.PopulateYoloEditorFields?.Invoke();
            context.PopulateTrainingEditorFields?.Invoke();
            context.PopulateClassList?.Invoke();
            context.RestoreObjectMetadataTagsFromProject?.Invoke();
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RefreshTrainingReadinessPanel?.Invoke(false);
            context.SetDatasetStatus?.Invoke($"데이터셋: recipe {recipeName}");
            SetProjectConfigStatus(string.Equals(previousRecipeName, recipeName, StringComparison.OrdinalIgnoreCase)
                ? $"Recipe 재적용: {recipeName}"
                : $"Recipe 적용: {recipeName}");
            context.AppendLog?.Invoke($"Recipe 적용: {recipeName}");
        }

        internal string BuildLabelPathSummary()
        {
            LabelingImageSnapshot activeImage = context.ApplicationState.ImageWorkspace.CaptureSnapshot();
            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(activeImage.ImageName, projectData);
            return labelPaths.Count == 0
                ? "라벨 경로: 확인 안 됨"
                : $"라벨: {labelPaths[0]}";
        }

        internal void EnsureProjectSettings()
        {
            projectData.ProjectSettings ??= new LabelingProjectSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(projectData.ProjectSettings);
        }

        #endregion
    }

    internal sealed class ProjectSettingsWorkflowAdapterContext
    {
        internal LabelingApplicationState ApplicationState { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal WpfProjectConfigPanelViewModel ProjectConfigViewModel { get; init; }
        internal ProjectRecipeSessionService ProjectRecipeSessionService { get; init; }
        internal ProjectRecipeApplyWorkflowService ProjectRecipeApplyWorkflowService { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<ApplicationCloseState> BuildApplicationCloseState { get; init; }
        internal Func<string> DefaultDatasetParentDirectory { get; init; }
        internal Func<string, string, string, string> SelectSaveFile { get; init; }
        internal Func<string, string, string, string> SelectFile { get; init; }
        internal Func<string, string, string> SelectFolder { get; init; }
        internal Action<string> SetProjectConfigStatus { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action UpdateYoloCommandButtons { get; init; }
        internal Action CancelFourPointBoxDraft { get; init; }
        internal Action<string> RememberLastOpenedDatasetRecipe { get; init; }
        internal Action ApplyProjectDatasetPurposeToWorkflow { get; init; }
        internal Action PopulateYoloEditorFields { get; init; }
        internal Action PopulateTrainingEditorFields { get; init; }
        internal Action PopulateClassList { get; init; }
        internal Action RestoreObjectMetadataTagsFromProject { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action<string> OpenFolder { get; init; }
    }
}
