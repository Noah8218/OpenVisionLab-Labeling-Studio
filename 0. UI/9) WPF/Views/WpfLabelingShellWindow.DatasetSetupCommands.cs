using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                return recipeName;
            }

            recipeName = GetCurrentRecipeName();
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                return recipeName;
            }

            return $"Dataset_{purpose}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
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
            string currentOutputRoot = global.Data.OutputRootPath;
            string defaultOutputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "DATA"));
            if (string.IsNullOrWhiteSpace(currentOutputRoot))
            {
                return Path.Combine(defaultOutputRoot, recipeName);
            }

            string normalizedCurrent = Path.GetFullPath(currentOutputRoot);
            return string.Equals(normalizedCurrent, defaultOutputRoot, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(defaultOutputRoot, recipeName)
                : currentOutputRoot;
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
