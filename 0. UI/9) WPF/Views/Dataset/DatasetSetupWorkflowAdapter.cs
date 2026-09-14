using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Owns dataset setup dialogs, persisted recipe selection, and the dataset
    // switch handoff. Existing services retain path, execution, and presentation policy.
    internal sealed class DatasetSetupWorkflowAdapter
    {
        private readonly DatasetSetupPathService datasetSetupPathService;
        private readonly DatasetSetupExecutionService datasetSetupExecutionService;
        private readonly DatasetSetupPresentationService datasetSetupPresentationService;
        private readonly ProjectRecipeSessionService projectRecipeSessionService;
        private readonly ProjectRecipeApplyWorkflowService projectRecipeApplyWorkflowService;
        private readonly DatasetImageRootResolver datasetImageRootResolver;
        private readonly ImageQueueSelectionService imageQueueSelectionService;
        private readonly DatasetSetupWorkflowAdapterContext context;

        internal DatasetSetupWorkflowAdapter(
            DatasetSetupPathService datasetSetupPathService,
            DatasetSetupExecutionService datasetSetupExecutionService,
            DatasetSetupPresentationService datasetSetupPresentationService,
            ProjectRecipeSessionService projectRecipeSessionService,
            ProjectRecipeApplyWorkflowService projectRecipeApplyWorkflowService,
            DatasetImageRootResolver datasetImageRootResolver,
            ImageQueueSelectionService imageQueueSelectionService,
            DatasetSetupWorkflowAdapterContext context)
        {
            this.datasetSetupPathService = datasetSetupPathService ?? throw new ArgumentNullException(nameof(datasetSetupPathService));
            this.datasetSetupExecutionService = datasetSetupExecutionService ?? throw new ArgumentNullException(nameof(datasetSetupExecutionService));
            this.datasetSetupPresentationService = datasetSetupPresentationService ?? throw new ArgumentNullException(nameof(datasetSetupPresentationService));
            this.projectRecipeSessionService = projectRecipeSessionService ?? throw new ArgumentNullException(nameof(projectRecipeSessionService));
            this.projectRecipeApplyWorkflowService = projectRecipeApplyWorkflowService ?? throw new ArgumentNullException(nameof(projectRecipeApplyWorkflowService));
            this.datasetImageRootResolver = datasetImageRootResolver ?? throw new ArgumentNullException(nameof(datasetImageRootResolver));
            this.imageQueueSelectionService = imageQueueSelectionService ?? throw new ArgumentNullException(nameof(imageQueueSelectionService));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.ApplicationState == null) throw new ArgumentNullException(nameof(context.ApplicationState));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            if (context.ProjectSettingsWorkflowAdapter == null) throw new ArgumentNullException(nameof(context.ProjectSettingsWorkflowAdapter));
        }

        internal void ExecuteStartDatasetSetupCommand(object selectedPurpose)
        {
            if (IsApplicationCloseApproved())
            {
                return;
            }

            try
            {
                WpfDatasetSetupWizardViewModel wizardViewModel = CreateDatasetSetupWizardViewModel(selectedPurpose);
                WpfDatasetSetupRequest acceptedRequest = null;
                WpfDatasetSetupWizardWindow wizard = context.CreateWizardWindow?.Invoke(wizardViewModel);
                if (wizard == null)
                {
                    wizardViewModel.Dispose();
                    SetFailureStatus("데이터셋 생성 창을 만들 수 없습니다.");
                    return;
                }

                wizardViewModel.ConfigureCommands(
                    commandPurpose =>
                    {
                        if (wizardViewModel.TryBuildRequest(commandPurpose, out WpfDatasetSetupRequest request, out string error))
                        {
                            acceptedRequest = request;
                            wizard.DialogResult = true;
                            return;
                        }

                        wizardViewModel.StatusText = error;
                    },
                    () => wizard.DialogResult = false,
                    browseOutputRoot: null,
                    browseImageRoot: null,
                    browseWeights: null);
                wizardViewModel.ConfigurePathSelectionWorkflow(CreateDatasetSetupPathSelectionCallbacks);

                if (wizard.ShowDialog() == true && acceptedRequest != null && !IsApplicationCloseApproved())
                {
                    ApplyDatasetSetupRequest(acceptedRequest);
                }
            }
            catch (Exception ex)
            {
                SetFailureStatus($"데이터셋 생성 실패: {ex.Message}");
            }
        }

        internal DatasetSetupWizardPathCallbacks CreateDatasetSetupPathSelectionCallbacks()
        {
            return new DatasetSetupWizardPathCallbacks
            {
                SelectFolder = (title, currentPath) =>
                    context.ProjectSettingsWorkflowAdapter.TryPickFolder(title, currentPath, out string selectedPath)
                        ? selectedPath
                        : string.Empty,
                SelectFile = (title, filter, currentPath) =>
                    context.ProjectSettingsWorkflowAdapter.TryPickFile(title, filter, currentPath, out string selectedPath)
                        ? selectedPath
                        : string.Empty,
                IsApplicationCloseApproved = IsApplicationCloseApproved
            };
        }

        internal void ExecuteChangeDatasetCommand()
        {
            _ = ExecuteChangeDatasetCommandAsync();
        }

        internal async Task ExecuteChangeDatasetCommandAsync()
        {
            if (IsApplicationCloseApproved())
            {
                return;
            }

            // Dataset change is intentionally a selector-first flow. Opening the
            // creation wizard directly made "change dataset" feel like "create dataset".
            context.AppendLog?.Invoke("데이터셋 선택 창 열기");
            var viewModel = new WpfDatasetSelectionWindowViewModel();
            string recipeRoot = ProjectRecipeService.GetRecipeRootDirectory();
            viewModel.LoadDatasets(recipeRoot, context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName());
            string selectedRecipeName = string.Empty;
            bool createNewRequested = false;

            WpfDatasetSelectionWindow window = context.CreateSelectionWindow?.Invoke(viewModel);
            if (window == null)
            {
                viewModel.Dispose();
                context.AppendLog?.Invoke("데이터셋 선택 창을 만들 수 없습니다.");
                return;
            }

            viewModel.ConfigureCommands(
                () =>
                {
                    if (viewModel.SelectedDataset == null || string.IsNullOrWhiteSpace(viewModel.SelectedDataset.RecipeName))
                    {
                        viewModel.StatusText = "열 데이터셋을 먼저 선택하세요.";
                        return;
                    }

                    selectedRecipeName = viewModel.SelectedDataset.RecipeName;
                    window.DialogResult = true;
                },
                () =>
                {
                    createNewRequested = true;
                    window.DialogResult = true;
                },
                () => viewModel.LoadDatasets(recipeRoot, context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName()),
                () => window.DialogResult = false);

            if (window.ShowDialog() != true || IsApplicationCloseApproved())
            {
                return;
            }

            if (createNewRequested)
            {
                ExecuteStartDatasetSetupCommand(context.SelectedPurposeProvider?.Invoke());
                return;
            }

            await ApplySelectedDatasetRecipeAsync(selectedRecipeName);
        }

        internal async Task ApplySelectedDatasetRecipeAsync(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            context.ProjectConfigViewModel.RecipeName = recipeName.Trim();
            context.ProjectConfigViewModel.SelectedRecipeName = recipeName.Trim();
            if (!await context.ProjectSettingsWorkflowAdapter.ApplyProjectRecipeFromPanelAsync())
            {
                return;
            }

            if (!IsApplicationCloseApproved())
            {
                CompleteSelectedDatasetRecipeSwitch();
            }
        }

        internal void CompleteSelectedDatasetRecipeSwitch()
        {
            string imageRootPath = ResolveActiveDatasetImageRoot();
            if (Directory.Exists(imageRootPath))
            {
                _ = context.LoadImageQueueFromRootAsync?.Invoke(imageRootPath, string.Empty, true, true);
            }
            else
            {
                ClearImageQueueAfterDatasetSwitch(imageRootPath);
            }

            RefreshShellDatasetContext();
        }

        internal string ResolveActiveDatasetImageRoot()
        {
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            string configuredRoot = data?.ProjectSettings?.ResolveImageRootPath() ?? string.Empty;
            return datasetImageRootResolver.Resolve(data, configuredRoot, HasQueueImages);
        }

        internal bool HasQueueImages(string imageRoot)
        {
            return !string.IsNullOrWhiteSpace(imageRoot)
                && Directory.Exists(imageRoot)
                && imageQueueSelectionService.HasImageFiles(imageRoot);
        }

        internal void ClearImageQueueAfterDatasetSwitch(string imageRootPath)
        {
            context.SetCurrentImageRoot?.Invoke(imageRootPath ?? string.Empty);
            context.SetCurrentImageFolder?.Invoke(imageRootPath ?? string.Empty, false);
            context.CancelImageQueueLoads?.Invoke();
            context.ClearAnomalyFolderStateSuggestion?.Invoke();
            context.ClearImageQueueItems?.Invoke();
            context.UpdateImageQueueStatusText?.Invoke();
            context.ClearActiveImageAfterQueueReset?.Invoke();
            context.SetDatasetStatus?.Invoke(datasetSetupPresentationService.BuildMissingImageRootStatus());
            context.AppendLog?.Invoke(datasetSetupPresentationService.BuildMissingImageRootLog(imageRootPath));
            context.FocusDatasetOnboardingTab?.Invoke();
        }

        internal void ExecuteOpenDatasetRootFolderCommand()
        {
            if (IsApplicationCloseApproved())
            {
                return;
            }

            LabelingProjectData data = context.DataProvider?.Invoke();
            string outputRootPath = data?.OutputRootPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                context.SetDatasetStatus?.Invoke(datasetSetupPresentationService.BuildMissingOutputRootStatus());
                context.AppendLog?.Invoke(datasetSetupPresentationService.BuildMissingOutputRootLog());
                return;
            }

            try
            {
                Directory.CreateDirectory(outputRootPath);
                context.OpenFolder?.Invoke(outputRootPath);
                context.AppendLog?.Invoke($"데이터셋 폴더 열기: {outputRootPath}");
            }
            catch (Exception ex)
            {
                context.SetDatasetStatus?.Invoke(datasetSetupPresentationService.BuildOpenDatasetFolderFailedStatus());
                context.AppendLog?.Invoke(datasetSetupPresentationService.BuildOpenDatasetFolderFailedLog(ex.Message));
            }
        }

        internal void RefreshShellDatasetContext()
        {
            if (context.ShellViewModel == null)
            {
                return;
            }

            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            string recipeName = context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName();
            string outputRootPath = data?.OutputRootPath ?? string.Empty;
            string currentImageRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
            string imageRootPath = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : data?.ProjectSettings?.ResolveImageRootPath() ?? string.Empty;
            string datasetName = DatasetContextPresentationService.BuildDatasetName(recipeName, outputRootPath);
            int classCount = data?.ClassNamedList?
                .Count(item => item != null && !string.IsNullOrWhiteSpace(item.Text)) ?? 0;

            context.ShellViewModel.SetDatasetContext(
                datasetName,
                DatasetContextPresentationService.FormatPurposeName(context.GetCurrentDatasetPurpose?.Invoke() ?? LabelingDatasetPurpose.ObjectDetection),
                outputRootPath,
                imageRootPath,
                !string.IsNullOrWhiteSpace(outputRootPath),
                classCount);
        }

        internal WpfDatasetSetupWizardViewModel CreateDatasetSetupWizardViewModel(object selectedPurpose)
        {
            LabelingDatasetPurpose purpose = ResolveRequestedDatasetPurpose(selectedPurpose);
            string recipeName = ResolveDatasetSetupRecipeName(purpose, out bool recipeNameWasGenerated);
            string outputRootPath = ResolveDatasetSetupOutputRoot(recipeName);
            LabelingProjectData data = context.DataProvider?.Invoke();
            IEnumerable<string> classNames = data?.ClassNamedList?
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text)
                ?? new[] { "Defect" };

            WpfDatasetSetupWizardViewModel viewModel = new WpfDatasetSetupWizardViewModel();
            viewModel.LoadFrom(purpose, recipeName, outputRootPath, classNames.DefaultIfEmpty("Defect"));
            viewModel.ConfigureAutomaticPathSync(
                recipeNameWasGenerated,
                selectedDatasetPurpose => datasetSetupPathService.BuildUniqueRecipeName(selectedDatasetPurpose, ProjectRecipeService.GetRecipeRootDirectory()),
                ResolveDatasetSetupOutputRoot);
            return viewModel;
        }

        internal bool ApplyDatasetSetupRequest(WpfDatasetSetupRequest request)
        {
            WpfDatasetSetupExecutionResult result = datasetSetupExecutionService.Execute(
                request,
                ProjectRecipeService.GetRecipeRootDirectory(),
                GetDatasetSetupDefaultOutputRoot());
            if (!result.Succeeded)
            {
                string message = result.Failure switch
                {
                    WpfDatasetSetupExecutionFailure.DuplicateOutputRoot => datasetSetupPresentationService.BuildDuplicateOutputRootMessage(result.ExistingRecipeName),
                    WpfDatasetSetupExecutionFailure.SamplePreset => datasetSetupPresentationService.BuildSamplePresetFailureMessage(result.SampleError),
                    WpfDatasetSetupExecutionFailure.RecipePersistence => datasetSetupPresentationService.BuildRecipePersistenceFailureMessage(result.PersistenceError),
                    _ => datasetSetupPresentationService.BuildInvalidRecipeNameMessage()
                };
                SetDatasetSetupStatus(message);
                context.SetProjectConfigStatus?.Invoke(message);
                if (result.Failure != WpfDatasetSetupExecutionFailure.InvalidRecipeName)
                {
                    context.AppendLog?.Invoke(message);
                }

                return false;
            }

            context.ProjectConfigViewModel.RecipeName = result.RecipeName;
            if (!projectRecipeApplyWorkflowService.TryApplyPrepared(
                    context.ApplicationState,
                    result.RecipeName,
                    result.Data,
                    out _))
            {
                return false;
            }

            context.ApplyPersistedDatasetPurposeToCurrentProject?.Invoke(request.Purpose);
            context.RememberLastOpenedDatasetRecipe?.Invoke(result.RecipeName);
            context.PopulateProjectConfigPanelFields?.Invoke();
            context.PopulateClassList?.Invoke(result.SelectedClassName);
            context.PopulateYoloEditorFields?.Invoke();
            context.PopulateTrainingEditorFields?.Invoke();
            context.RefreshTrainingReadinessPanel?.Invoke(false);
            context.RefreshYoloTrainingStepCompletion?.Invoke();
            context.EnterLabelingWorkbenchStartView?.Invoke();
            if (Directory.Exists(result.ImageRootPath))
            {
                _ = context.LoadImageQueueFromRootAsync?.Invoke(result.ImageRootPath, string.Empty, true, true);
            }
            else
            {
                ClearImageQueueAfterDatasetSwitch(result.ImageRootPath);
            }

            string status = datasetSetupPresentationService.BuildReadyStatus(result.RecipeName, request.Purpose, result.ManifestPath, result.SampleResult);
            SetDatasetSetupStatus(status);
            context.SetProjectConfigStatus?.Invoke(status);
            context.SetDatasetStatus?.Invoke(datasetSetupPresentationService.BuildDatasetReadyStatus(result.OutputRootPath));
            context.AppendLog?.Invoke(datasetSetupPresentationService.BuildCreationLog(result.RecipeName, request.Purpose, result.OutputRootPath, result.ManifestPath));
            return true;
        }

        internal bool TryRestoreLastOpenedDatasetOnStartup()
        {
            string recipeRootPath = ProjectRecipeService.GetRecipeRootDirectory();
            string recipeName = ProjectRecipeService.ResolveStartupRecipeName(recipeRootPath, string.Empty);
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            context.ProjectConfigViewModel.RecipeName = recipeName;
            context.ProjectConfigViewModel.SelectedRecipeName = recipeName;
            try
            {
                string previousRecipeName = projectRecipeSessionService.Apply(context.ApplicationState, recipeName);
                context.CompleteProjectRecipeApply?.Invoke(previousRecipeName, recipeName);
                CompleteSelectedDatasetRecipeSwitch();
            }
            catch (Exception ex)
            {
                context.SetProjectConfigStatus?.Invoke($"Recipe 적용 실패: {ex.Message}");
                context.AppendLog?.Invoke($"Recipe 적용 실패: {ex.Message}");
                return false;
            }

            context.AppendLog?.Invoke($"이전 데이터셋 복원: {recipeName}");
            return true;
        }

        internal string ResolveDatasetSetupRecipeName(LabelingDatasetPurpose purpose, out bool generated)
            => datasetSetupPathService.ResolveRecipeName(
                context.ProjectConfigViewModel?.RecipeName?.Trim(),
                context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName(),
                purpose,
                ProjectRecipeService.GetRecipeRootDirectory(),
                out generated);

        internal LabelingDatasetPurpose ResolveRequestedDatasetPurpose(object selectedPurpose)
        {
            WpfLearningModeItem selectedPurposeItem = selectedPurpose as WpfLearningModeItem
                ?? context.SelectedPurposeProvider?.Invoke() as WpfLearningModeItem
                ?? context.LearningWorkflowViewModel?.SelectedDatasetPurposeMode;
            if (selectedPurposeItem != null)
            {
                return WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(selectedPurposeItem.Mode);
            }

            return context.LearningWorkflowViewModel?.GetSelectedDatasetPurpose() ?? LabelingDatasetPurpose.ObjectDetection;
        }

        internal string ResolveDatasetSetupOutputRoot(string recipeName)
            => datasetSetupPathService.ResolveOutputRoot(recipeName, GetDatasetSetupDefaultOutputRoot(), ProjectRecipeService.GetRecipeRootDirectory());

        internal static string GetDatasetSetupDefaultOutputRoot()
            => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "DATA"));

        internal void SetDatasetSetupStatus(string message)
        {
            if (context.LearningWorkflowViewModel != null)
            {
                context.LearningWorkflowViewModel.DatasetSetupStatusText = message ?? string.Empty;
            }
        }

        private bool IsApplicationCloseApproved()
            => context.IsApplicationCloseApproved?.Invoke() == true;

        private void SetFailureStatus(string message)
        {
            SetDatasetSetupStatus(message);
            context.SetProjectConfigStatus?.Invoke(message);
            context.AppendLog?.Invoke(message);
        }
    }

    internal sealed class DatasetSetupWorkflowAdapterContext
    {
        internal LabelingApplicationState ApplicationState { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ProjectSettingsWorkflowAdapter ProjectSettingsWorkflowAdapter { get; init; }
        internal WpfProjectConfigPanelViewModel ProjectConfigViewModel { get; init; }
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal WpfLabelingShellViewModel ShellViewModel { get; init; }
        internal Func<object> SelectedPurposeProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<WpfDatasetSetupWizardViewModel, WpfDatasetSetupWizardWindow> CreateWizardWindow { get; init; }
        internal Func<WpfDatasetSelectionWindowViewModel, WpfDatasetSelectionWindow> CreateSelectionWindow { get; init; }
        internal Func<string, string, bool, bool, Task<int>> LoadImageQueueFromRootAsync { get; init; }
        internal Func<string> CurrentImageRootProvider { get; init; }
        internal Action<string> SetCurrentImageRoot { get; init; }
        internal Action<string, bool> SetCurrentImageFolder { get; init; }
        internal Action CancelImageQueueLoads { get; init; }
        internal Action ClearAnomalyFolderStateSuggestion { get; init; }
        internal Action ClearImageQueueItems { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action ClearActiveImageAfterQueueReset { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> SetProjectConfigStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> OpenFolder { get; init; }
        internal Action FocusDatasetOnboardingTab { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action<LabelingDatasetPurpose> ApplyPersistedDatasetPurposeToCurrentProject { get; init; }
        internal Action<string> RememberLastOpenedDatasetRecipe { get; init; }
        internal Action PopulateProjectConfigPanelFields { get; init; }
        internal Action<string> PopulateClassList { get; init; }
        internal Action PopulateYoloEditorFields { get; init; }
        internal Action PopulateTrainingEditorFields { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal Action EnterLabelingWorkbenchStartView { get; init; }
        internal Func<LabelingDatasetPurpose> GetCurrentDatasetPurpose { get; init; }
        internal Action<string, string> CompleteProjectRecipeApply { get; init; }
    }
}
