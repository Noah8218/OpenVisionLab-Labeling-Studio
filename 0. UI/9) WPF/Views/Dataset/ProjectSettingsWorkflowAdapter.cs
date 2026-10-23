using OpenVisionLab;
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
        private bool isRecipeApplyRunning;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal ProjectSettingsWorkflowAdapter(ProjectSettingsWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.ApplicationState);
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal bool IsRecipeSelectionRefreshActive => isRecipeSelectionRefreshActive;

        internal bool IsRecipeApplyRunning => isRecipeApplyRunning;

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
            context.CompleteDatasetRecipeSwitch?.Invoke();
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
            RefreshDatasetVersionPresentation();
            PopulateProjectRecipeList(recipeName);

            SetProjectConfigStatus(string.IsNullOrWhiteSpace(recipeName)
                ? OpenVisionLanguageService.T("WpfProjectConfig.Status.SelectBeforeSave")
                : $"현재 설정 파일: {Path.GetFileName(configPath)}");
            context.UpdateYoloCommandButtons?.Invoke();
        }

        internal void RefreshDatasetVersionPresentation()
        {
            if (context.ProjectConfigViewModel != null)
            {
                context.ProjectConfigViewModel.SetDatasetVersionInfo(
                    RecipeDatasetVersionPresentationService.BuildCurrent(context.ProjectConfigViewModel.ManifestPath, projectData));
            }
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
                SetProjectConfigStatus(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.ListReadFailed"), ex.Message));
                context.AppendLog?.Invoke(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.ListReadFailed"), ex.Message));
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
                    context.RefreshRecipeLibrary?.Invoke(recipeName);
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
                context.AppendLog?.Invoke(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Log.OpenSettingsFolder"), directoryPath));
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"폴더 열기 실패: {ex.Message}");
                context.AppendLog?.Invoke(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Log.OpenSettingsFolderFailed"), ex.Message));
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

        internal RecipeConfigurationSaveResult SaveRecipeMetadata(
            string recipeName,
            string productCode,
            string recipeDescription,
            RecipeImageSelectionMode previewImageMode,
            string previewImageRelativePath)
        {
            RecipeConfigurationSaveResult result = context.ProjectRecipeSessionService.SaveRecipeMetadata(
                ProjectRecipeService.GetRecipeRootDirectory(),
                recipeName,
                productCode,
                recipeDescription,
                previewImageMode,
                previewImageRelativePath);
            if (result.IsSuccess && string.Equals(GetCurrentRecipeName(), recipeName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureProjectSettings();
                projectData.ProjectSettings.ProductCode = productCode?.Trim() ?? string.Empty;
                projectData.ProjectSettings.RecipeDescription = recipeDescription?.Trim() ?? string.Empty;
                projectData.ProjectSettings.RecipeListPreviewImageMode = previewImageMode;
                projectData.ProjectSettings.RecipeListPreviewImageRelativePath = previewImageMode == RecipeImageSelectionMode.Selected
                    ? previewImageRelativePath?.Trim() ?? string.Empty
                    : string.Empty;
            }

            return result;
        }

        internal bool SaveModelMetadataConfigFromPanel()
            => SaveProjectConfigFromPanelCore(recipeName =>
                context.ProjectRecipeSessionService.Save(projectData, recipeName, refreshDatasetVersion: false));

        internal bool SaveProjectConfigFromPanelCore(Func<string, string> saveRecipe)
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus(OpenVisionLanguageService.T("WpfProjectConfig.Status.NoRecipeNameForSave"));
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

        internal async Task<ProjectRecipeApplyResult> ApplyProjectRecipeFromPanelAsync(string requestedRecipeName = null)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true || isRecipeApplyRunning)
            {
                return ProjectRecipeApplyResult.Cancelled;
            }

            string recipeName = (requestedRecipeName ?? context.ProjectConfigViewModel?.RecipeName)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus(OpenVisionLanguageService.T("WpfProjectConfig.Status.EnterRecipeName"));
                return ProjectRecipeApplyResult.Cancelled;
            }

            if (!ProjectRecipeService.IsValidRecipeName(recipeName))
            {
                SetProjectConfigStatus(OpenVisionLanguageService.T("WpfProjectConfig.Status.InvalidRecipeName"));
                return ProjectRecipeApplyResult.Cancelled;
            }

            WorkflowTransitionApproval approval = context.WorkflowTransitionAdapterProvider?.Invoke()?.TryApprove(
                recipeName,
                replacesRecipe: true) ?? WorkflowTransitionApproval.Allowed;
            if (!approval.IsAllowed)
            {
                SetProjectConfigStatus(OpenVisionLanguageService.T("WpfProjectConfig.Status.ApplyCanceledOrBlocked"));
                return ProjectRecipeApplyResult.Cancelled;
            }

            isRecipeApplyRunning = true;
            context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
            try
            {
                ProjectRecipeApplyResult applyResult = await context.ProjectRecipeApplyWorkflowService.ApplyAsync(
                    context.ApplicationState,
                    recipeName);
                if (!applyResult.IsApplied || context.IsApplicationCloseApproved?.Invoke() == true)
                {
                    if (!context.IsApplicationCloseApproved?.Invoke() == true)
                    {
                        SetProjectConfigStatus(OpenVisionLanguageService.T("WpfProjectConfig.Status.NotApplied"));
                    }

                    return applyResult;
                }

                return applyResult;
            }
            catch (OperationCanceledException)
            {
                return ProjectRecipeApplyResult.Cancelled;
            }
            catch (Exception ex)
            {
                if (context.IsApplicationCloseApproved?.Invoke() == true)
                {
                    return ProjectRecipeApplyResult.Closed;
                }

                SetProjectConfigStatus(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.ApplyFailed"), ex.Message));
                context.AppendLog?.Invoke(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.ApplyFailed"), ex.Message));
                return ProjectRecipeApplyResult.Cancelled;
            }
            finally
            {
                isRecipeApplyRunning = false;
                context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
            }
        }

        internal bool ReloadCurrentRecipeSettingsFromDisk()
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            try
            {
                string previousRecipeName = context.ProjectRecipeSessionService.Apply(context.ApplicationState, recipeName);
                CompleteProjectRecipeApply(previousRecipeName, recipeName);
                SetProjectConfigStatus("저장된 레시피 설정을 불러왔습니다.");
                return true;
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"저장된 레시피 설정을 불러오지 못했습니다: {ex.Message}");
                context.AppendLog?.Invoke($"저장된 레시피 설정 다시 불러오기 실패: {ex.Message}");
                return false;
            }
        }

        internal void CompleteProjectRecipeApply(string previousRecipeName, string recipeName)
        {
            var d1Total = System.Diagnostics.Stopwatch.StartNew();
            System.Diagnostics.Trace.WriteLine(FormattableString.Invariant($"D1_PROJECT_BEGIN previous={previousRecipeName} recipe={recipeName}"));
            context.ResetSmartMaskPromptSession?.Invoke();
            context.RememberLastOpenedDatasetRecipe?.Invoke(recipeName);
            EnsureProjectSettings();
            double d1BeforePurpose = d1Total.Elapsed.TotalMilliseconds;
            context.ApplyProjectDatasetPurposeToWorkflow?.Invoke();
            double d1AfterPurpose = d1Total.Elapsed.TotalMilliseconds;
            PopulateProjectConfigPanelFields();
            context.PopulateYoloEditorFields?.Invoke();
            context.PopulateTrainingEditorFields?.Invoke();
            context.PopulateClassList?.Invoke();
            context.RestoreObjectMetadataTagsFromProject?.Invoke();
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RefreshTrainingReadinessPanel?.Invoke(false);
            if (context.RefreshYoloStatus != null) context.RefreshYoloStatus();
            else context.RefreshModelCenterDashboard?.Invoke();
            context.SetDatasetStatus?.Invoke(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.DatasetRecipe"), recipeName));
            SetProjectConfigStatus(string.Equals(previousRecipeName, recipeName, StringComparison.OrdinalIgnoreCase)
                ? string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.Reapplied"), recipeName)
                : string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.Applied"), recipeName));
            context.AppendLog?.Invoke(string.Format(OpenVisionLanguageService.T("WpfProjectConfig.Status.Applied"), recipeName));
            System.Diagnostics.Trace.WriteLine(FormattableString.Invariant($"D1_PROJECT_END recipe={recipeName} totalMs={d1Total.Elapsed.TotalMilliseconds:F3} beforePurposeMs={d1BeforePurpose:F3} purposeMs={d1AfterPurpose - d1BeforePurpose:F3} afterPurposeMs={d1Total.Elapsed.TotalMilliseconds - d1AfterPurpose:F3}"));
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
        internal Action ResetSmartMaskPromptSession { get; init; }
        internal Action<string> RememberLastOpenedDatasetRecipe { get; init; }
        internal Action ApplyProjectDatasetPurposeToWorkflow { get; init; }
        internal Action PopulateYoloEditorFields { get; init; }
        internal Action PopulateTrainingEditorFields { get; init; }
        internal Action PopulateClassList { get; init; }
        internal Action RestoreObjectMetadataTagsFromProject { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action RefreshModelCenterDashboard { get; init; }
        internal Action RefreshYoloStatus { get; init; }
        internal Action<string> OpenFolder { get; init; }
        internal Func<WorkflowTransitionAdapter> WorkflowTransitionAdapterProvider { get; init; }
        internal Action CompleteDatasetRecipeSwitch { get; init; }
        internal Action<string> RefreshRecipeLibrary { get; init; }
    }
}
