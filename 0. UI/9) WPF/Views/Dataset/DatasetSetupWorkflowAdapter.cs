using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
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
        private Task<int> recipeImageQueueLoadTask = Task.FromResult(0);

        internal bool IsSelectionRunning { get; private set; }

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
            if (IsApplicationCloseApproved() || IsSelectionRunning)
            {
                return;
            }

            IsSelectionRunning = true;
            try
            {
                WpfDatasetSetupWizardViewModel wizardViewModel = CreateDatasetSetupWizardViewModel(selectedPurpose);
                WpfDatasetSetupRequest acceptedRequest = null;
                WpfDatasetSetupWizardWindow wizard = context.CreateWizardWindow?.Invoke(wizardViewModel);
                if (wizard == null)
                {
                    wizardViewModel.Dispose();
                    SetFailureStatus(OpenVisionLanguageService.T("WpfDatasetSetup.Error.CreationUnavailable"));
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

                bool? accepted;
                using (var backdrop = new DialogBackdropScope(wizard, (System.Windows.FrameworkElement)wizard.Owner.Content, managesOwnerInput: false))
                {
                    accepted = wizard.ShowDialog();
                }

                if (accepted == true && acceptedRequest != null && !IsApplicationCloseApproved())
                {
                    // The dialog has finished; the shared transition guard must only see other running work.
                    IsSelectionRunning = false;
                    ApplyDatasetSetupRequest(acceptedRequest);
                }
            }
            catch (Exception ex)
            {
                SetFailureStatus($"{OpenVisionLanguageService.T("WpfDatasetSetup.Error.CreationFailed")}: {ex.Message}");
            }
            finally
            {
                IsSelectionRunning = false;
                context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
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
            ShowRecipeLibrary();
        }

        internal void ShowRecipeLibrary()
        {
            if (IsApplicationCloseApproved() || IsSelectionRunning)
            {
                return;
            }

            WpfDatasetSelectionWindowViewModel viewModel = context.RecipeSelectionViewModel;
            if (viewModel == null)
            {
                context.AppendLog?.Invoke("레시피 목록을 표시할 수 없습니다.");
                return;
            }

            viewModel.ConfigureCommands(
                () => _ = OpenSelectedRecipeFromLibraryAsync(),
                () => ExecuteStartDatasetSetupCommand(context.SelectedPurposeProvider?.Invoke()),
                RefreshRecipeLibrary,
                CancelRecipeLibrary,
                context.ProjectSettingsWorkflowAdapter.SaveRecipeMetadata,
                () => _ = OpenSelectedRecipeFromLibraryAsync(continueLabeling: true));
            RefreshRecipeLibrary();
            context.ShellViewModel?.SetRecipeLibraryVisible(true);
        }

        private void RefreshRecipeLibrary()
            => RefreshRecipeLibrary(context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName());

        internal void RefreshRecipeLibrary(string selectedRecipeName)
            => context.RecipeSelectionViewModel?.LoadDatasets(ProjectRecipeService.GetRecipeRootDirectory(), selectedRecipeName);

        private void CancelRecipeLibrary()
        {
            if (!string.IsNullOrWhiteSpace(context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName()))
            {
                context.ShellViewModel?.SetRecipeLibraryVisible(false);
            }
        }

        private async Task OpenSelectedRecipeFromLibraryAsync(bool continueLabeling = false)
        {
            WpfDatasetSelectionWindowViewModel viewModel = context.RecipeSelectionViewModel;
            WpfDatasetSelectionItem selected = viewModel?.SelectedDataset;
            context.AppendLog?.Invoke($"레시피 홈 요청: {selected?.RecipeName} / 이어하기:{continueLabeling} / 전환 중:{IsSelectionRunning}");
            if (selected == null || IsApplicationCloseApproved() || IsSelectionRunning || !viewModel.TryBeginOpen())
            {
                return;
            }

            bool waitingToResume = false;
            try
            {
                bool applied = await ApplySelectedDatasetRecipeAsync(selected.RecipeName);
                if (!continueLabeling || !applied) return;
                waitingToResume = true;
                IsSelectionRunning = true;
                context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
                Task<int> catalogTask = recipeImageQueueLoadTask;
                int count = await catalogTask;
                await (context.ImageQueueDetailRefreshTask?.Invoke() ?? Task.CompletedTask);
                if (count > 0 && !IsApplicationCloseApproved() && ReferenceEquals(catalogTask, recipeImageQueueLoadTask)
                    && string.Equals(selected.RecipeName, context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName(), StringComparison.OrdinalIgnoreCase))
                {
                    IsSelectionRunning = false;
                    waitingToResume = false;
                    context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
                    context.ContinueLabeling?.Invoke();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                string message = string.Format(OpenVisionLanguageService.T("WpfRecipeLibrary.ResumeFailed"), error.Message);
                viewModel.StatusText = message;
                SetFailureStatus(message);
            }
            finally
            {
                if (waitingToResume)
                {
                    IsSelectionRunning = false;
                    context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
                }
                viewModel.EndOpen();
            }
        }

        internal async Task ExecuteChangeDatasetCommandAsync()
        {
            if (IsApplicationCloseApproved() || IsSelectionRunning)
            {
                return;
            }

            IsSelectionRunning = true;
            try
            {
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
                            viewModel.StatusText = OpenVisionLanguageService.T("WpfDatasetSelection.Error.SelectionRequired");
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
                    () => window.DialogResult = false,
                    context.ProjectSettingsWorkflowAdapter.SaveRecipeMetadata);

                if (window.ShowDialog() != true || IsApplicationCloseApproved())
                {
                    return;
                }

                if (createNewRequested)
                {
                    IsSelectionRunning = false;
                    ExecuteStartDatasetSetupCommand(context.SelectedPurposeProvider?.Invoke());
                    return;
                }

                IsSelectionRunning = false;
                await ApplySelectedDatasetRecipeAsync(selectedRecipeName);
            }
            finally
            {
                IsSelectionRunning = false;
                context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
            }
        }

        internal async Task<bool> ApplySelectedDatasetRecipeAsync(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName) || IsApplicationCloseApproved() || IsSelectionRunning)
            {
                return false;
            }

            Task<ProjectRecipeApplyResult> applyTask = context.ProjectSettingsWorkflowAdapter.ApplyProjectRecipeFromPanelAsync(recipeName.Trim());
            // Let the recipe adapter admit the transition before this adapter marks its own selection busy.
            IsSelectionRunning = true;
            try
            {
                ProjectRecipeApplyResult applyResult = await applyTask;
                if (!applyResult.IsApplied || IsApplicationCloseApproved())
                {
                    return false;
                }

                context.ProjectSettingsWorkflowAdapter.HandleProjectRecipeApplied(recipeName.Trim(), applyResult);
                context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
                context.ShellViewModel?.SetRecipeLibraryVisible(false);
                return true;
            }
            finally
            {
                IsSelectionRunning = false;
                context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
            }
        }

        internal bool CloseCurrentRecipe()
        {
            if (IsApplicationCloseApproved())
            {
                return false;
            }

            WorkflowTransitionApproval approval = context.WorkflowTransitionAdapterProvider?.Invoke()?.TryApprove(
                "레시피 닫기",
                replacesRecipe: true, closesRecipe: true) ?? WorkflowTransitionApproval.Allowed;
            if (!approval.IsAllowed)
            {
                return false;
            }

            string recipeName = context.ProjectSettingsWorkflowAdapter.GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            projectRecipeSessionService.ClearActiveRecipe(context.ApplicationState);
            context.CancelImageQueueLoads?.Invoke();
            recipeImageQueueLoadTask = Task.FromResult(0);
            context.SetCurrentImageRoot?.Invoke(string.Empty);
            context.SetCurrentImageFolder?.Invoke(string.Empty, false);
            context.ClearImageQueueItems?.Invoke();
            context.ClearActiveImageAfterQueueReset?.Invoke();
            context.UpdateImageQueueStatusText?.Invoke();
            context.PopulateProjectConfigPanelFields?.Invoke();
            context.PopulateClassList?.Invoke(string.Empty);
            context.PopulateYoloEditorFields?.Invoke();
            context.PopulateTrainingEditorFields?.Invoke();
            context.RefreshTrainingReadinessPanel?.Invoke(false);
            context.RefreshYoloTrainingStepCompletion?.Invoke();
            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
            context.LearningWorkflowViewModel?.ShowDatasetOnboarding();
            context.FocusDatasetOnboardingTab?.Invoke();
            context.SetProjectConfigStatus?.Invoke(OpenVisionLanguageService.T("ShellTransition.ClosedStatus"));
            context.SetDatasetStatus?.Invoke(OpenVisionLanguageService.T("ShellTransition.ClosedStatus"));
            context.AppendLog?.Invoke($"레시피 닫기: {recipeName}");
            context.RefreshShellDatasetContext?.Invoke();
            ShowRecipeLibrary();
            context.WorkflowTransitionAdapterProvider?.Invoke()?.RefreshCommandState();
            return true;
        }

        internal void CompleteSelectedDatasetRecipeSwitch()
        {
            string imageRootPath = ResolveActiveDatasetImageRoot();
            if (Directory.Exists(imageRootPath))
            {
                ResetImageQueueForRecipeSwitch();
                recipeImageQueueLoadTask = context.LoadImageQueueFromRootAsync?.Invoke(imageRootPath, string.Empty, true, true) ?? Task.FromResult(0);
            }
            else
            {
                recipeImageQueueLoadTask = Task.FromResult(0);
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
            context.RefreshDatasetWorkspace?.Invoke();
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
            if (request == null)
            {
                return false;
            }

            WorkflowTransitionApproval approval = context.WorkflowTransitionAdapterProvider?.Invoke()?.TryApprove(
                request.RecipeName,
                replacesRecipe: true) ?? WorkflowTransitionApproval.Allowed;
            if (!approval.IsAllowed)
            {
                return false;
            }

            WpfDatasetSetupExecutionResult result = datasetSetupExecutionService.Execute(
                request,
                ProjectRecipeService.GetRecipeRootDirectory(),
                GetDatasetSetupDefaultOutputRoot());
            if (!result.Succeeded)
            {
                string message = result.Failure switch
                {
                    WpfDatasetSetupExecutionFailure.InvalidRecipeMetadata => datasetSetupPresentationService.BuildInvalidRecipeMetadataMessage(),
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
                    out string previousRecipeName))
            {
                return false;
            }

            context.CompleteProjectRecipeApply?.Invoke(previousRecipeName, result.RecipeName);
            context.ApplyPersistedDatasetPurposeToCurrentProject?.Invoke(request.Purpose);
            if (Directory.Exists(result.ImageRootPath))
            {
                ResetImageQueueForRecipeSwitch();
            }

            context.RememberLastOpenedDatasetRecipe?.Invoke(result.RecipeName);
            context.PopulateProjectConfigPanelFields?.Invoke();
            context.PopulateClassList?.Invoke(result.SelectedClassName);
            context.PopulateYoloEditorFields?.Invoke();
            context.PopulateTrainingEditorFields?.Invoke();
            context.RefreshTrainingReadinessPanel?.Invoke(false);
            context.RefreshYoloTrainingStepCompletion?.Invoke();
            context.ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
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
            context.ShellViewModel?.SetRecipeLibraryVisible(false);
            return true;
        }

        internal bool TryRestoreLastOpenedDatasetOnStartup()
        {
            string recipeRootPath = ProjectRecipeService.GetRecipeRootDirectory();
            string recipeName = ProjectRecipeService.ResolveStartupRecipeName(recipeRootPath, string.Empty);
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                ShowRecipeLibrary();
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
                context.SetProjectConfigStatus?.Invoke($"{OpenVisionLanguageService.T("WpfDatasetSelection.Error.ApplyFailed")}: {ex.Message}");
                context.AppendLog?.Invoke($"{OpenVisionLanguageService.T("WpfDatasetSelection.Error.ApplyFailed")}: {ex.Message}");
                ShowRecipeLibrary();
                return false;
            }

            context.AppendLog?.Invoke($"이전 데이터셋 복원: {recipeName}");
            context.ShellViewModel?.SetRecipeLibraryVisible(false);
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

        private void ResetImageQueueForRecipeSwitch()
        {
            context.CancelImageQueueLoads?.Invoke();
            context.ClearImageQueueItems?.Invoke();
            context.ClearActiveImageAfterQueueReset?.Invoke();
        }

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
        internal WpfDatasetSelectionWindowViewModel RecipeSelectionViewModel { get; init; }
        internal Func<object> SelectedPurposeProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<WpfDatasetSetupWizardViewModel, WpfDatasetSetupWizardWindow> CreateWizardWindow { get; init; }
        internal Func<WpfDatasetSelectionWindowViewModel, WpfDatasetSelectionWindow> CreateSelectionWindow { get; init; }
        internal Func<string, string, bool, bool, Task<int>> LoadImageQueueFromRootAsync { get; init; }
        internal Func<Task> ImageQueueDetailRefreshTask { get; init; }
        internal Action ContinueLabeling { get; init; }
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
        internal Func<WorkflowTransitionAdapter> WorkflowTransitionAdapterProvider { get; init; }
        internal Action RefreshShellDatasetContext { get; init; }
        internal Action RefreshDatasetWorkspace { get; init; }
    }
}
