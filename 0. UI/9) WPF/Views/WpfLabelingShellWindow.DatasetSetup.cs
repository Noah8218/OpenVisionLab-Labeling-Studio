using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    // Responsibility group: dataset setup and persisted dataset selection.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region DatasetSetupCommands
        private void ExecuteStartDatasetSetupCommand(object selectedPurpose)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            try
            {
                WpfDatasetSetupWizardViewModel wizardViewModel = CreateDatasetSetupWizardViewModel(selectedPurpose);
                WpfDatasetSetupRequest acceptedRequest = null;
                WpfDatasetSetupWizardWindow wizard = new WpfDatasetSetupWizardWindow
                {
                    Owner = this,
                    DataContext = wizardViewModel
                };

                wizardViewModel.ConfigureCommands(
                    commandPurpose =>
                    {
                        wizard.CommitTextBindings();
                        // Commit first, then mirror visible editor values back to
                        // the VM. Real-EXE automation and some IME paths can update
                        // TextBox.Text before the binding source catches up.
                        if (!string.IsNullOrWhiteSpace(wizard.RecipeNameText))
                        {
                            wizardViewModel.RecipeName = wizard.RecipeNameText;
                        }

                        if (!string.IsNullOrWhiteSpace(wizard.OutputRootPathText))
                        {
                            wizardViewModel.OutputRootPath = wizard.OutputRootPathText;
                        }

                        if (WpfDatasetSetupWizardViewModel.ParseClassNames(wizard.ClassNamesText).Count > 0)
                        {
                            wizardViewModel.ClassNamesText = wizard.ClassNamesText;
                        }

                        wizardViewModel.ImageRootPath = wizard.ImageRootPathText;
                        wizardViewModel.WeightsPath = wizard.WeightsPathText;

                        object selectedWizardPurpose = wizard.SelectedDatasetPurpose ?? commandPurpose;
                        if (wizardViewModel.TryBuildRequest(selectedWizardPurpose, out WpfDatasetSetupRequest request, out string error))
                        {
                            acceptedRequest = request;
                            wizard.DialogResult = true;
                            return;
                        }

                        wizardViewModel.StatusText = error;
                    },
                    () => wizard.DialogResult = false,
                    () => ExecuteBrowseDatasetSetupOutputRootCommand(wizardViewModel),
                    () => ExecuteBrowseDatasetSetupImageRootCommand(wizardViewModel),
                    () => ExecuteBrowseDatasetSetupWeightsCommand(wizardViewModel));

                if (wizard.ShowDialog() == true && acceptedRequest != null && !isApplicationCloseApproved)
                {
                    ApplyDatasetSetupRequest(acceptedRequest);
                }
            }
            catch (Exception ex)
            {
                string message = $"\uB370\uC774\uD130\uC14B \uC0DD\uC131 \uC2E4\uD328: {ex.Message}";
                SetDatasetSetupStatus(message);
                SetProjectConfigStatus(message);
                AppendLog(message);
            }
        }

        private void ExecuteChangeDatasetCommand()
        {
            _ = ExecuteChangeDatasetCommandAsync();
        }

        private async Task ExecuteChangeDatasetCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            // Dataset change is intentionally a selector-first flow. Opening the
            // creation wizard directly made "change dataset" feel like "create dataset".
            AppendLog("\uB370\uC774\uD130\uC14B \uC120\uD0DD \uCC3D \uC5F4\uAE30");
            var viewModel = new WpfDatasetSelectionWindowViewModel();
            string recipeRoot = ProjectRecipeService.GetRecipeRootDirectory();
            viewModel.LoadDatasets(recipeRoot, GetCurrentRecipeName());
            string selectedRecipeName = string.Empty;
            bool createNewRequested = false;

            var window = new WpfDatasetSelectionWindow
            {
                Owner = this,
                DataContext = viewModel
            };

            viewModel.ConfigureCommands(
                () =>
                {
                    if (viewModel.SelectedDataset == null || string.IsNullOrWhiteSpace(viewModel.SelectedDataset.RecipeName))
                    {
                        viewModel.StatusText = "\uC5F4 \uB370\uC774\uD130\uC14B\uC744 \uBA3C\uC800 \uC120\uD0DD\uD558\uC138\uC694.";
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
                () => viewModel.LoadDatasets(recipeRoot, GetCurrentRecipeName()),
                () => window.DialogResult = false);

            if (window.ShowDialog() != true)
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            if (createNewRequested)
            {
                ExecuteStartDatasetSetupCommand(LearningWorkflowViewModel?.SelectedDatasetPurposeMode);
                return;
            }

            await ApplySelectedDatasetRecipeAsync(selectedRecipeName);
        }

        private async Task ApplySelectedDatasetRecipeAsync(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            ProjectConfigViewModel.RecipeName = recipeName.Trim();
            ProjectConfigViewModel.SelectedRecipeName = recipeName.Trim();
            if (!await ApplyProjectRecipeFromPanelAsync())
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            CompleteSelectedDatasetRecipeSwitch();
        }

        private void CompleteSelectedDatasetRecipeSwitch()
        {
            string imageRootPath = ResolveActiveDatasetImageRoot();
            if (Directory.Exists(imageRootPath))
            {
                _ = LoadImageQueueFromRootAsync(imageRootPath, string.Empty, loadFirstImage: true);
            }
            else
            {
                ClearImageQueueAfterDatasetSwitch(imageRootPath);
            }

            RefreshShellDatasetContext();
        }

        private string ResolveActiveDatasetImageRoot()
        {
            EnsureProjectSettings();

            string configuredRoot = global.Data?.ProjectSettings?.ResolveImageRootPath() ?? string.Empty;
            return datasetImageRootResolver.Resolve(global.Data, configuredRoot, HasQueueImages);
        }

        private bool HasQueueImages(string imageRoot)
        {
            return !string.IsNullOrWhiteSpace(imageRoot)
                && Directory.Exists(imageRoot)
                && imageQueueSelectionService.HasImageFiles(imageRoot);
        }

        private void ClearImageQueueAfterDatasetSwitch(string imageRootPath)
        {
            currentImageRoot = imageRootPath ?? string.Empty;
            ImageQueueViewModel?.SetCurrentImageFolder(currentImageRoot, canOpenFolder: false);
            CancelImageQueueCatalogLoad(waitForCompletion: false);
            CancelImageQueueDetailRefresh(waitForCompletion: false);
            batchDetectionWorkflowService.Cancel();
            imageQualityReviewWorkflowService.Reset();
            anomalyImageReviewSession.Reset();
            ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
            suppressImageQueueSelection = true;
            try
            {
                imageQueueItems.Clear();
                imageQueueItemsByPath.Clear();
                imageQueueView?.Refresh();
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateImageQueueStatusText();
            ClearActiveImageAfterQueueReset();
            SetDatasetStatus(datasetSetupPresentationService.BuildMissingImageRootStatus());
            AppendLog(datasetSetupPresentationService.BuildMissingImageRootLog(imageRootPath));
            FocusDatasetOnboardingTab();
        }

        private void ExecuteOpenDatasetRootFolderCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string outputRootPath = global.Data?.OutputRootPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                SetDatasetStatus(datasetSetupPresentationService.BuildMissingOutputRootStatus());
                AppendLog(datasetSetupPresentationService.BuildMissingOutputRootLog());
                return;
            }

            try
            {
                Directory.CreateDirectory(outputRootPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputRootPath,
                    UseShellExecute = true
                });
                AppendLog($"\uB370\uC774\uD130\uC14B \uD3F4\uB354 \uC5F4\uAE30: {outputRootPath}");
            }
            catch (Exception ex)
            {
                SetDatasetStatus(datasetSetupPresentationService.BuildOpenDatasetFolderFailedStatus());
                AppendLog(datasetSetupPresentationService.BuildOpenDatasetFolderFailedLog(ex.Message));
            }
        }

        private void RefreshShellDatasetContext()
        {
            if (ShellViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            string recipeName = GetCurrentRecipeName();
            string outputRootPath = global.Data?.OutputRootPath ?? string.Empty;
            string imageRootPath = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : global.Data?.ProjectSettings?.ResolveImageRootPath() ?? string.Empty;
            string datasetName = DatasetContextPresentationService.BuildDatasetName(recipeName, outputRootPath);
            int classCount = global.Data?.ClassNamedList?
                .Count(item => item != null && !string.IsNullOrWhiteSpace(item.Text)) ?? 0;

            ShellViewModel.SetDatasetContext(
                datasetName,
                DatasetContextPresentationService.FormatPurposeName(GetCurrentDatasetPurpose()),
                outputRootPath,
                imageRootPath,
                !string.IsNullOrWhiteSpace(outputRootPath),
                classCount);
        }

        private WpfDatasetSetupWizardViewModel CreateDatasetSetupWizardViewModel(object selectedPurpose)
        {
            LabelingDatasetPurpose purpose = ResolveRequestedDatasetPurpose(selectedPurpose);
            string recipeName = ResolveDatasetSetupRecipeName(purpose, out bool recipeNameWasGenerated);
            string outputRootPath = ResolveDatasetSetupOutputRoot(recipeName);
            IEnumerable<string> classNames = global.Data?.ClassNamedList?
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

        private bool ApplyDatasetSetupRequest(WpfDatasetSetupRequest request)
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
                SetProjectConfigStatus(message);
                if (result.Failure != WpfDatasetSetupExecutionFailure.InvalidRecipeName)
                {
                    AppendLog(message);
                }

                return false;
            }

            ProjectConfigViewModel.RecipeName = result.RecipeName;

            // Persistence is complete before this command. Commit the prepared
            // state through the same session owner without loading it a second time.
            if (!projectRecipeApplyWorkflowService.TryApplyPrepared(
                    global,
                    result.RecipeName,
                    result.Data,
                    out _))
            {
                return false;
            }

            ApplyPersistedDatasetPurposeToCurrentProject(request.Purpose);
            RememberLastOpenedDatasetRecipe(result.RecipeName);

            PopulateProjectConfigPanelFields();
            PopulateClassList(result.SelectedClassName);
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            RefreshYoloTrainingStepCompletion();
            EnterLabelingWorkbenchStartView();
            if (Directory.Exists(result.ImageRootPath))
            {
                _ = LoadImageQueueFromRootAsync(result.ImageRootPath, string.Empty, loadFirstImage: true);
            }
            else
            {
                ClearImageQueueAfterDatasetSwitch(result.ImageRootPath);
            }

            string status = datasetSetupPresentationService.BuildReadyStatus(result.RecipeName, request.Purpose, result.ManifestPath, result.SampleResult);
            SetDatasetSetupStatus(status);
            SetProjectConfigStatus(status);
            SetDatasetStatus(datasetSetupPresentationService.BuildDatasetReadyStatus(result.OutputRootPath));
            AppendLog(datasetSetupPresentationService.BuildCreationLog(result.RecipeName, request.Purpose, result.OutputRootPath, result.ManifestPath));
            return true;
        }

        private bool TryRestoreLastOpenedDatasetOnStartup()
        {
            string recipeRootPath = ProjectRecipeService.GetRecipeRootDirectory();
            string recipeName = ProjectRecipeService.ResolveStartupRecipeName(recipeRootPath, string.Empty);
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            ProjectConfigViewModel.RecipeName = recipeName;
            ProjectConfigViewModel.SelectedRecipeName = recipeName;
            try
            {
                string previousRecipeName = projectRecipeSessionService.Apply(global, recipeName);
                CompleteProjectRecipeApply(previousRecipeName, recipeName);
                CompleteSelectedDatasetRecipeSwitch();
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"Recipe 적용 실패: {ex.Message}");
                AppendLog($"Recipe 적용 실패: {ex.Message}");
                return false;
            }

            AppendLog($"\uC774\uC804 \uB370\uC774\uD130\uC14B \uBCF5\uC6D0: {recipeName}");
            return true;
        }

        private static void RememberLastOpenedDatasetRecipe(string recipeName)
        {
            // This is the app-start restore anchor. Do not infer "last opened"
            // from manifest timestamps because saving labels can update another dataset later.
            ProjectRecipeService.SaveLastOpenedRecipeName(ProjectRecipeService.GetRecipeRootDirectory(), recipeName);
        }

        private void ExecuteBrowseDatasetSetupOutputRootCommand(WpfDatasetSetupWizardViewModel viewModel)
        {
            if (isApplicationCloseApproved || viewModel == null)
            {
                return;
            }

            if (TryPickFolder("\uB370\uC774\uD130\uC14B \uC800\uC7A5 \uD3F4\uB354 \uC120\uD0DD", viewModel.OutputRootPath, out string selectedPath))
            {
                if (isApplicationCloseApproved)
                {
                    return;
                }

                viewModel.OutputRootPath = selectedPath;
            }
        }

        private void ExecuteBrowseDatasetSetupImageRootCommand(WpfDatasetSetupWizardViewModel viewModel)
        {
            if (isApplicationCloseApproved || viewModel == null)
            {
                return;
            }

            if (TryPickFolder("원본 이미지 폴더 선택", viewModel.ImageRootPath, out string selectedPath))
            {
                if (isApplicationCloseApproved)
                {
                    return;
                }

                viewModel.ImageRootPath = selectedPath;
            }
        }

        private void ExecuteBrowseDatasetSetupWeightsCommand(WpfDatasetSetupWizardViewModel viewModel)
        {
            if (isApplicationCloseApproved || viewModel == null)
            {
                return;
            }

            if (TryPickFile(
                "초기 검사 모델 파일 선택",
                "모델 파일 (*.pt;*.pth;*.onnx)|*.pt;*.pth;*.onnx|All files (*.*)|*.*",
                viewModel.WeightsPath,
                out string selectedPath))
            {
                if (isApplicationCloseApproved)
                {
                    return;
                }

                viewModel.WeightsPath = selectedPath;
            }
        }

        private string ResolveDatasetSetupRecipeName(LabelingDatasetPurpose purpose, out bool generated)
        {
            return datasetSetupPathService.ResolveRecipeName(
                ProjectConfigViewModel?.RecipeName?.Trim(),
                GetCurrentRecipeName(),
                purpose,
                ProjectRecipeService.GetRecipeRootDirectory(),
                out generated);
        }

        private LabelingDatasetPurpose ResolveRequestedDatasetPurpose(object selectedPurpose)
        {
            WpfLearningModeItem selectedPurposeItem = selectedPurpose as WpfLearningModeItem
                ?? DatasetPurposeListBox?.SelectedItem as WpfLearningModeItem
                ?? LearningWorkflowViewModel?.SelectedDatasetPurposeMode;
            if (selectedPurposeItem != null)
            {
                return WpfLearningWorkflowPanelViewModel.ToDatasetPurpose(selectedPurposeItem.Mode);
            }

            return LearningWorkflowViewModel?.GetSelectedDatasetPurpose() ?? LabelingDatasetPurpose.ObjectDetection;
        }

        private string ResolveDatasetSetupOutputRoot(string recipeName)
        {
            return datasetSetupPathService.ResolveOutputRoot(recipeName, GetDatasetSetupDefaultOutputRoot(), ProjectRecipeService.GetRecipeRootDirectory());
        }

        private static string GetDatasetSetupDefaultOutputRoot()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "DATA"));
        }

        private void SetDatasetSetupStatus(string message)
        {
            if (LearningWorkflowViewModel != null)
            {
                LearningWorkflowViewModel.DatasetSetupStatusText = message ?? string.Empty;
            }
        }
        #endregion

    }
}
