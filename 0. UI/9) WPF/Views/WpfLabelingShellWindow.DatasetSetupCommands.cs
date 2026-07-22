using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void ExecuteStartDatasetSetupCommand(object selectedPurpose)
        {
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
                    () => ExecuteBrowseDatasetSetupOutputRootCommand(wizardViewModel));

                if (wizard.ShowDialog() == true && acceptedRequest != null)
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
            // Dataset change is intentionally a selector-first flow. Opening the
            // creation wizard directly made "change dataset" feel like "create dataset".
            AppendLog("\uB370\uC774\uD130\uC14B \uC120\uD0DD \uCC3D \uC5F4\uAE30");
            var viewModel = new WpfDatasetSelectionWindowViewModel();
            string recipeRoot = GetRecipeRootDirectory();
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

            if (createNewRequested)
            {
                ExecuteStartDatasetSetupCommand(LearningWorkflowViewModel?.SelectedDatasetPurposeMode);
                return;
            }

            ApplySelectedDatasetRecipe(selectedRecipeName);
        }

        private void ApplySelectedDatasetRecipe(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            ProjectConfigViewModel.RecipeName = recipeName.Trim();
            ProjectConfigViewModel.SelectedRecipeName = recipeName.Trim();
            ApplyProjectRecipeFromPanel();

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

            string configuredRoot = global.Data?.ProjectSettings?.PythonModel?.ImageRootPath ?? string.Empty;
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
            imageReviewStatus.SetImages(Array.Empty<string>());
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
                : global.Data?.ProjectSettings?.PythonModel?.ImageRootPath ?? string.Empty;
            string datasetName = WpfDatasetContextPresentationService.BuildDatasetName(recipeName, outputRootPath);
            int classCount = global.Data?.ClassNamedList?
                .Count(item => item != null && !string.IsNullOrWhiteSpace(item.Text)) ?? 0;

            ShellViewModel.SetDatasetContext(
                datasetName,
                WpfDatasetContextPresentationService.FormatPurposeName(GetCurrentDatasetPurpose()),
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
                selectedDatasetPurpose => datasetSetupPathService.BuildUniqueRecipeName(selectedDatasetPurpose, GetRecipeRootDirectory()),
                ResolveDatasetSetupOutputRoot);
            return viewModel;
        }

        private bool ApplyDatasetSetupRequest(WpfDatasetSetupRequest request)
        {
            WpfDatasetSetupExecutionResult result = datasetSetupExecutionService.Execute(
                request,
                GetRecipeRootDirectory(),
                GetDatasetSetupDefaultOutputRoot());
            if (!result.Succeeded)
            {
                string message = result.Failure switch
                {
                    WpfDatasetSetupExecutionFailure.DuplicateOutputRoot => datasetSetupPresentationService.BuildDuplicateOutputRootMessage(result.ExistingRecipeName),
                    WpfDatasetSetupExecutionFailure.SamplePreset => datasetSetupPresentationService.BuildSamplePresetFailureMessage(result.SampleError),
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

            // CRecipe.Name raises the existing recipe-change lifecycle. The
            // service saved the fully prepared CData first, so the lifecycle can
            // load that contract without the view constructing files or labels.
            global.Data = result.Data;
            global.Recipe.Name = result.RecipeName;
            ApplyDatasetPurposeToCurrentProject(request.Purpose);
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
            string recipeRootPath = GetRecipeRootDirectory();
            string recipeName = WpfProjectRecipeService.ResolveStartupRecipeName(recipeRootPath, string.Empty);
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            ApplySelectedDatasetRecipe(recipeName);
            AppendLog($"\uC774\uC804 \uB370\uC774\uD130\uC14B \uBCF5\uC6D0: {recipeName}");
            return true;
        }

        private static void RememberLastOpenedDatasetRecipe(string recipeName)
        {
            // This is the app-start restore anchor. Do not infer "last opened"
            // from manifest timestamps because saving labels can update another dataset later.
            WpfProjectRecipeService.SaveLastOpenedRecipeName(GetRecipeRootDirectory(), recipeName);
        }

        private void ExecuteBrowseDatasetSetupOutputRootCommand(WpfDatasetSetupWizardViewModel viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            if (TryPickFolder("\uB370\uC774\uD130\uC14B \uC800\uC7A5 \uD3F4\uB354 \uC120\uD0DD", viewModel.OutputRootPath, out string selectedPath))
            {
                viewModel.OutputRootPath = selectedPath;
            }
        }

        private string ResolveDatasetSetupRecipeName(LabelingDatasetPurpose purpose, out bool generated)
        {
            return datasetSetupPathService.ResolveRecipeName(
                ProjectConfigViewModel?.RecipeName?.Trim(),
                GetCurrentRecipeName(),
                purpose,
                GetRecipeRootDirectory(),
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
            return datasetSetupPathService.ResolveOutputRoot(recipeName, GetDatasetSetupDefaultOutputRoot(), GetRecipeRootDirectory());
        }

        private static string GetDatasetSetupDefaultOutputRoot()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "DATA"));
        }

        private static bool PathsEqual(string firstPath, string secondPath)
        {
            return WpfDatasetSetupPathService.PathsEqual(firstPath, secondPath);
        }

        private void SetDatasetSetupStatus(string message)
        {
            if (LearningWorkflowViewModel != null)
            {
                LearningWorkflowViewModel.DatasetSetupStatusText = message ?? string.Empty;
            }
        }
    }
}
