using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
                LoadImageQueueFromRoot(imageRootPath, string.Empty, loadFirstImage: true);
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
            if (Directory.Exists(configuredRoot) && !IsImplicitDefaultImageRoot(configuredRoot))
            {
                return configuredRoot;
            }

            // Dataset-owned image copies are a fallback. The operator expects the
            // folder they explicitly opened for this dataset to come back first.
            foreach (string datasetImageRoot in EnumerateActiveDatasetImageRoots())
            {
                if (HasQueueImages(datasetImageRoot))
                {
                    return datasetImageRoot;
                }
            }

            if (Directory.Exists(configuredRoot))
            {
                return configuredRoot;
            }

            return EnumerateActiveDatasetImageRoots().FirstOrDefault(Directory.Exists) ?? configuredRoot;
        }

        private IEnumerable<string> EnumerateActiveDatasetImageRoots()
        {
            if (global.Data == null)
            {
                yield break;
            }

            global.Data.NormalizeOutputPaths();
            foreach (string imageRoot in new[]
            {
                global.Data.TrainImagesPath,
                global.Data.ValidImagesPath,
                global.Data.TestImagesPath
            })
            {
                if (!string.IsNullOrWhiteSpace(imageRoot))
                {
                    yield return imageRoot;
                }
            }
        }

        private bool HasQueueImages(string imageRoot)
        {
            return !string.IsNullOrWhiteSpace(imageRoot)
                && Directory.Exists(imageRoot)
                && imageQueueSelectionService.EnumerateImageFiles(imageRoot).Count > 0;
        }

        private void ClearImageQueueAfterDatasetSwitch(string imageRootPath)
        {
            currentImageRoot = imageRootPath ?? string.Empty;
            ImageQueueViewModel?.SetCurrentImageFolder(currentImageRoot, canOpenFolder: false);
            CancelImageQueueDetailRefresh(waitForCompletion: false);
            imageReviewStatus.SetImages(Array.Empty<string>());
            suppressImageQueueSelection = true;
            try
            {
                imageQueueItems.Clear();
                imageQueueView?.Refresh();
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateImageQueueStatusText();
            SetDatasetStatus("\uB370\uC774\uD130\uC14B: \uC774\uBBF8\uC9C0 \uD3F4\uB354 \uD655\uC778 \uD544\uC694");
            AppendLog($"\uB370\uC774\uD130\uC14B \uC804\uD658: \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. root={imageRootPath}");
            FocusDatasetOnboardingTab();
        }

        private static bool IsImplicitDefaultImageRoot(string imageRoot)
        {
            string defaultRoot = PythonModelSettings.GetDefaultImageRootPath();
            if (string.IsNullOrWhiteSpace(imageRoot) || string.IsNullOrWhiteSpace(defaultRoot))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(imageRoot),
                    Path.GetFullPath(defaultRoot),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) when (imageRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || defaultRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }
        }

        private void ExecuteOpenDatasetRootFolderCommand()
        {
            string outputRootPath = global.Data?.OutputRootPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                SetDatasetStatus("\uB370\uC774\uD130\uC14B: \uC800\uC7A5 \uACBD\uB85C \uBBF8\uC124\uC815");
                AppendLog("\uC5F4 \uB370\uC774\uD130\uC14B \uD3F4\uB354\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uB370\uC774\uD130\uC14B\uC744 \uB9CC\uB4E4\uAC70\uB098 \uC800\uC7A5 \uACBD\uB85C\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
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
                SetDatasetStatus($"\uB370\uC774\uD130\uC14B: \uD3F4\uB354 \uC5F4\uAE30 \uC2E4\uD328");
                AppendLog($"\uB370\uC774\uD130\uC14B \uD3F4\uB354 \uC5F4\uAE30 \uC2E4\uD328: {ex.Message}");
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
            string datasetName = FirstNonEmpty(
                recipeName,
                string.IsNullOrWhiteSpace(outputRootPath) ? string.Empty : Path.GetFileName(outputRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                "\uB370\uC774\uD130\uC14B \uBBF8\uC120\uD0DD");
            int classCount = global.Data?.ClassNamedList?
                .Count(item => item != null && !string.IsNullOrWhiteSpace(item.Text)) ?? 0;

            ShellViewModel.SetDatasetContext(
                datasetName,
                FormatShellDatasetPurposeName(GetCurrentDatasetPurpose()),
                outputRootPath,
                imageRootPath,
                !string.IsNullOrWhiteSpace(outputRootPath),
                classCount);
        }

        private static string FormatShellDatasetPurposeName(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "\uC138\uADF8\uBA58\uD14C\uC774\uC158",
                LabelingDatasetPurpose.AnomalyDetection => "\uC774\uC0C1 \uD0D0\uC9C0",
                _ => "\uAC1D\uCCB4 \uD0D0\uC9C0"
            };
        }

        private WpfDatasetSetupWizardViewModel CreateDatasetSetupWizardViewModel(object selectedPurpose)
        {
            LabelingDatasetPurpose purpose = ResolveRequestedDatasetPurpose(selectedPurpose);
            string recipeName = ResolveDatasetSetupRecipeName(purpose);
            string outputRootPath = ResolveDatasetSetupOutputRoot(recipeName);
            IEnumerable<string> classNames = global.Data?.ClassNamedList?
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text)
                ?? new[] { "Defect" };

            WpfDatasetSetupWizardViewModel viewModel = new WpfDatasetSetupWizardViewModel();
            viewModel.LoadFrom(purpose, recipeName, outputRootPath, classNames.DefaultIfEmpty("Defect"));
            return viewModel;
        }

        private bool ApplyDatasetSetupRequest(WpfDatasetSetupRequest request)
        {
            if (request == null)
            {
                return false;
            }

            string recipeName = request.RecipeName?.Trim() ?? string.Empty;
            if (!WpfProjectRecipeService.IsValidRecipeName(recipeName))
            {
                SetDatasetSetupStatus("Recipe \uC774\uB984\uC5D0 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4.");
                SetProjectConfigStatus("Recipe \uC774\uB984\uC5D0 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4.");
                return false;
            }

            string outputRootPath = string.IsNullOrWhiteSpace(request.OutputRootPath)
                ? ResolveDatasetSetupOutputRoot(recipeName)
                : request.OutputRootPath.Trim();
            if (TryFindDatasetUsingOutputRoot(outputRootPath, recipeName, out string existingRecipeName))
            {
                string message = $"\uB370\uC774\uD130\uC14B \uC800\uC7A5 \uACBD\uB85C\uAC00 \uC774\uBBF8 '{existingRecipeName}'\uC5D0\uC11C \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4. \uC0C8 \uB370\uC774\uD130\uC14B\uC740 \uB2E4\uB978 \uBE48 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
                SetDatasetSetupStatus(message);
                SetProjectConfigStatus(message);
                AppendLog(message);
                return false;
            }

            IReadOnlyList<string> classNames = request.ClassNames == null
                ? Array.Empty<string>()
                : request.ClassNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
            if (classNames.Count == 0)
            {
                classNames = new[] { "Defect" };
            }

            ProjectConfigViewModel.RecipeName = recipeName;

            // Dataset setup is a low-frequency workflow action. Keep folder,
            // config, YAML, and manifest creation here instead of spreading it
            // across the purpose selector and drawing tool paths.
            global.Recipe.Name = recipeName;
            ApplyDatasetPurposeToCurrentProject(request.Purpose);

            global.Data.ConfigureOutputRoot(outputRootPath);
            global.Data.ClassNamedList.Clear();
            foreach (string className in classNames)
            {
                CClassItem classItem = EnsureClassItem(className);
                // The manifest is generated from global.Data.ClassNamedList, so
                // the setup request must be materialized there before SaveConfig.
                if (classItem != null
                    && !global.Data.ClassNamedList.Any(item => string.Equals(item?.Text, classItem.Text, StringComparison.OrdinalIgnoreCase)))
                {
                    global.Data.ClassNamedList.Add(classItem);
                }
            }

            global.Data.EnsureYoloOutputDirectories();
            if (!WpfDatasetSamplePresetService.TryApplySample(request, global.Data, out WpfDatasetSamplePresetApplyResult sampleResult, out string sampleError))
            {
                SetDatasetSetupStatus(sampleError);
                SetProjectConfigStatus(sampleError);
                AppendLog(sampleError);
                return false;
            }

            if (sampleResult?.Applied == true && Directory.Exists(sampleResult.ImageRootPath))
            {
                global.Data.ProjectSettings.PythonModel.ImageRootPath = sampleResult.ImageRootPath;
            }

            global.Data.SaveYoloDataYaml();
            global.Data.SaveConfig(recipeName);
            RememberLastOpenedDatasetRecipe(recipeName);

            string selectedClassName = classNames.FirstOrDefault() ?? "Defect";
            PopulateProjectConfigPanelFields();
            PopulateClassList(selectedClassName);
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            RefreshYoloTrainingStepCompletion();
            FocusClassCatalogTab();
            if (sampleResult?.Applied == true && Directory.Exists(sampleResult.ImageRootPath))
            {
                LoadImageQueueFromRoot(sampleResult.ImageRootPath, string.Empty, loadFirstImage: true);
            }

            string manifestPath = LabelingDatasetManifestService.GetManifestPath(recipeName);
            string sampleStatus = sampleResult?.Applied == true ? $" / {sampleResult.SummaryText}" : string.Empty;
            string status = $"\uC900\uBE44 \uC644\uB8CC: {recipeName} / {request.Purpose} / {Path.GetFileName(manifestPath)}{sampleStatus}";
            SetDatasetSetupStatus(status);
            SetProjectConfigStatus(status);
            SetDatasetStatus($"\uB370\uC774\uD130\uC14B: {Path.GetFileName(outputRootPath)} \uC900\uBE44 \uC644\uB8CC");
            AppendLog($"\uB370\uC774\uD130\uC14B \uC0DD\uC131 \uC644\uB8CC: recipe={recipeName}, purpose={request.Purpose}, output={outputRootPath}, manifest={manifestPath}");
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

        private string ResolveDatasetSetupRecipeName(LabelingDatasetPurpose purpose)
        {
            string recipeName = ProjectConfigViewModel?.RecipeName?.Trim();
            if (CanUseRecipeNameForNewDataset(recipeName))
            {
                return recipeName;
            }

            recipeName = GetCurrentRecipeName();
            if (CanUseRecipeNameForNewDataset(recipeName))
            {
                return recipeName;
            }

            return BuildUniqueDatasetSetupRecipeName(purpose);
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
            string defaultOutputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "DATA"));
            string normalizedRecipeName = WpfProjectRecipeService.IsValidRecipeName(recipeName)
                ? recipeName.Trim()
                : BuildUniqueDatasetSetupRecipeName(LabelingDatasetPurpose.ObjectDetection);
            string outputRootPath = Path.Combine(defaultOutputRoot, normalizedRecipeName);
            if (!Directory.Exists(outputRootPath))
            {
                return outputRootPath;
            }

            int suffix = 2;
            while (Directory.Exists($"{outputRootPath}_{suffix}"))
            {
                suffix++;
            }

            return $"{outputRootPath}_{suffix}";
        }

        private bool CanUseRecipeNameForNewDataset(string recipeName)
        {
            if (!WpfProjectRecipeService.IsValidRecipeName(recipeName))
            {
                return false;
            }

            string recipeDirectory = WpfProjectRecipeService.BuildConfigDirectory(GetRecipeRootDirectory(), recipeName.Trim());
            return !Directory.Exists(recipeDirectory);
        }

        private string BuildUniqueDatasetSetupRecipeName(LabelingDatasetPurpose purpose)
        {
            string baseName = $"Dataset_{purpose}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
            string recipeName = baseName;
            int suffix = 2;
            while (!CanUseRecipeNameForNewDataset(recipeName))
            {
                recipeName = $"{baseName}_{suffix}";
                suffix++;
            }

            return recipeName;
        }

        private bool TryFindDatasetUsingOutputRoot(string outputRootPath, string requestedRecipeName, out string existingRecipeName)
        {
            existingRecipeName = string.Empty;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return false;
            }

            string recipeRoot = GetRecipeRootDirectory();
            foreach (string recipeName in WpfProjectRecipeService.ListRecipeNames(recipeRoot))
            {
                if (string.Equals(recipeName, requestedRecipeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string configPath = WpfProjectRecipeService.BuildConfigPath(recipeRoot, recipeName);
                string existingOutputRoot = TryReadRecipeOutputRoot(configPath);
                if (PathsEqual(existingOutputRoot, outputRootPath))
                {
                    existingRecipeName = recipeName;
                    return true;
                }
            }

            return false;
        }

        private static string TryReadRecipeOutputRoot(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return string.Empty;
            }

            try
            {
                XDocument document = XDocument.Load(configPath);
                return document.Descendants("OutputRootPath").FirstOrDefault()?.Value
                    ?? document.Descendants("OutputDataImageAndTxtPath").FirstOrDefault()?.Value
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool PathsEqual(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
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
