using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Views;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using OpenVisionLab.Mvvm.Behaviors;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUiApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfUiApplicationThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;
using WpfUiFluentWindow = Wpf.Ui.Controls.FluentWindow;
using WpfUiWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace MvcVisionSystem
{
    // Responsibility group: project settings and recipe selection.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ShellProjectSettings
        // Project settings helpers are shared by detection, training, and save flows.
        private void SetProjectConfigStatus(string message)
        {
            ProjectConfigViewModel.StatusText = message ?? string.Empty;
        }

        private string GetCurrentRecipeName()
        {
            return global.Recipe?.Name?.Trim() ?? string.Empty;
        }

        private string GetCurrentRecipeConfigDirectory()
        {
            string recipeName = GetCurrentRecipeName();
            return ProjectRecipeService.BuildConfigDirectory(ProjectRecipeService.GetRecipeRootDirectory(), recipeName);
        }

        private string GetCurrentRecipeConfigPath()
        {
            string recipeName = GetCurrentRecipeName();
            return ProjectRecipeService.BuildConfigPath(ProjectRecipeService.GetRecipeRootDirectory(), recipeName);
        }

        private bool TryPickFile(string title, string filter, string currentPath, out string selectedPath)
            => fileDialogService.TryPickFile(this, title, filter, currentPath, out selectedPath);

        private bool TryPickFolder(string title, string currentPath, out string selectedPath)
            => fileDialogService.TryPickFolder(this, title, currentPath, out selectedPath);

        private void PopulateProjectConfigPanelFields()
        {
            string recipeName = GetCurrentRecipeName();
            string configPath = GetCurrentRecipeConfigPath();
            ProjectConfigViewModel?.LoadFrom(recipeName, ProjectRecipeService.GetRecipeRootDirectory());
            ProjectConfigViewModel?.SetDatasetVersionInfo(
                RecipeDatasetVersionPresentationService.Build(ProjectConfigViewModel.ManifestPath));

            PopulateProjectRecipeList(recipeName);

            SetProjectConfigStatus(string.IsNullOrWhiteSpace(recipeName)
                ? "Recipe 이름이 아직 없습니다. 저장 전에 recipe를 선택하거나 생성해야 합니다."
                : $"현재 설정 파일: {Path.GetFileName(configPath)}");
            UpdateYoloCommandButtons();
        }

        // Recipe list, save, and apply commands share the same project-settings
        // boundary so a junior reader can follow one complete configuration flow.
        private bool PopulateProjectRecipeList(string selectedRecipeName)
        {
            WpfProjectConfigPanelViewModel viewModel = ProjectConfigViewModel;
            if (viewModel == null)
            {
                return false;
            }

            suppressProjectRecipeSelection = true;
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
                AppendLog($"Recipe 목록 읽기 실패: {ex.Message}");
                return false;
            }
            finally
            {
                suppressProjectRecipeSelection = false;
            }
        }

        private void ExecuteSaveProjectConfigCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            SaveProjectConfigFromPanel();
        }

        private void ExecuteApplyProjectRecipeCommand()
        {
            _ = ExecuteApplyProjectRecipeCommandAsync();
        }

        private async Task ExecuteApplyProjectRecipeCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            await ApplyProjectRecipeFromPanelAsync();
        }

        private void ProjectRecipeListBox_SelectionChanged(object sender, object selectedItem)
        {
            if (suppressProjectRecipeSelection)
            {
                return;
            }

            string recipeName = selectedItem as string ?? ProjectConfigViewModel?.SelectedRecipeName ?? ProjectRecipeListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            ProjectConfigViewModel?.SelectRecipeFromList(recipeName);
        }

        private void ExecuteRefreshProjectRecipeListCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string selectedRecipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? GetCurrentRecipeName();
            if (PopulateProjectRecipeList(selectedRecipeName))
            {
                SetProjectConfigStatus("Recipe 목록을 다시 읽었습니다. 적용할 항목을 선택하세요.");
            }
        }

        private void ExecuteOpenProjectConfigFolderCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string directoryPath = string.IsNullOrWhiteSpace(GetCurrentRecipeName())
                ? ProjectRecipeService.GetRecipeRootDirectory()
                : GetCurrentRecipeConfigDirectory();

            try
            {
                Directory.CreateDirectory(directoryPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = directoryPath,
                    UseShellExecute = true
                });
                SetProjectConfigStatus($"폴더 열기: {directoryPath}");
                AppendLog($"Recipe 설정 폴더 열기: {directoryPath}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"폴더 열기 실패: {ex.Message}");
                AppendLog($"Recipe 설정 폴더 열기 실패: {ex.Message}");
            }
        }

        // Saving/applying a recipe reloads dependent panels so stale model state cannot survive a recipe switch.
        private bool SaveProjectConfigFromPanel()
        {
            return SaveProjectConfigFromPanelCore(
                recipeName => projectRecipeSessionService.Save(global.Data, recipeName));
        }

        private bool SaveModelMetadataConfigFromPanel()
        {
            return SaveProjectConfigFromPanelCore(
                recipeName => projectRecipeSessionService.Save(global.Data, recipeName, refreshDatasetVersion: false));
        }

        private bool SaveProjectConfigFromPanelCore(Func<string, string> saveRecipe)
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
                SetDatasetStatus($"데이터셋: 설정 저장 {Path.GetFileName(configPath)}");
                AppendLog($"프로젝트 설정 저장: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"설정 저장 실패: {ex.Message}");
                AppendLog($"프로젝트 설정 저장 실패: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ApplyProjectRecipeFromPanelAsync()
        {
            if (isApplicationCloseApproved)
            {
                return false;
            }

            CancelFourPointBoxDraft(updateStatus: false);
            string recipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? string.Empty;
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
                string previousRecipeName = await projectRecipeSessionService.ApplyAsync(
                    global,
                    recipeName,
                    projectRecipeSessionCts.Token);
                if (isApplicationCloseApproved)
                {
                    return false;
                }

                CompleteProjectRecipeApply(previousRecipeName, recipeName);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                if (isApplicationCloseApproved)
                {
                    return false;
                }

                SetProjectConfigStatus($"Recipe 적용 실패: {ex.Message}");
                AppendLog($"Recipe 적용 실패: {ex.Message}");
                return false;
            }
        }

        private void CompleteProjectRecipeApply(string previousRecipeName, string recipeName)
        {
            RememberLastOpenedDatasetRecipe(recipeName);
            EnsureProjectSettings();
            ApplyProjectDatasetPurposeToWorkflow();
            // Recipe changes reload every dependent panel so stale labels, weights, and class lists do not survive the switch.
            PopulateProjectConfigPanelFields();
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            PopulateClassList();
            RestoreObjectMetadataTagsFromProject();
            RefreshCandidateList();
            RefreshObjectList();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            SetDatasetStatus($"데이터셋: recipe {recipeName}");
            SetProjectConfigStatus(string.Equals(previousRecipeName, recipeName, StringComparison.OrdinalIgnoreCase)
                ? $"Recipe 재적용: {recipeName}"
                : $"Recipe 적용: {recipeName}");
            AppendLog($"Recipe 적용: {recipeName}");
        }

        private string BuildLabelPathSummary()
        {
            LabelingImageSnapshot activeImage = global.ImageWorkspace.CaptureSnapshot();
            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(activeImage.ImageName, global.Data);
            return labelPaths.Count == 0
                ? "라벨 경로: 확인 안 됨"
                : $"라벨: {labelPaths[0]}";
        }

        private void EnsureProjectSettings()
        {
            global.Data.ProjectSettings ??= new LabelingProjectSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(global.Data.ProjectSettings);
        }

        private void ApplyProjectDatasetPurposeToWorkflow()
        {
            EnsureProjectSettings();
            CanvasPanelViewModel?.RestoreSmartMaskAutoContourMode(
                global.Data.ProjectSettings.SmartMaskAutoContourEnabled);
            RestoreBoxDrawingMethodFromProject();
            LearningWorkflowViewModel?.ApplyDatasetPurpose(global.Data.ProjectSettings.DatasetPurpose);
            RefreshCanvasAnnotationToolScope();
            ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
            RefreshCanvasWorkflowContext();
            RefreshAnnotationVisibilityForDatasetPurpose();
            // Purpose synchronization is also called while the shell is being
            // constructed.  Keep that lifecycle path presentation-only; the
            // explicit recipe-apply flow refreshes readiness after the new
            // project has been committed, while a fresh shell retains the
            // dashboard/checklist "before check" state.
            RefreshYoloTrainingStepCompletion();
        }

        private void ApplyWorkflowDatasetPurposeToProjectSettings()
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.DatasetPurpose = LearningWorkflowViewModel?.GetSelectedDatasetPurpose()
                ?? global.Data.ProjectSettings.DatasetPurpose;
        }

        private void ApplyDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
        {
            ApplyDatasetPurposeToCurrentProjectCore(purpose, persistAfterBindingsSettle: false);
        }

        private void ApplyPersistedDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
        {
            ApplyDatasetPurposeToCurrentProjectCore(purpose, persistAfterBindingsSettle: true);
        }

        private void ApplyDatasetPurposeToCurrentProjectCore(
            LabelingDatasetPurpose purpose,
            bool persistAfterBindingsSettle)
        {
            SynchronizeDatasetPurposeToCurrentProject(purpose);
            string recipeName = global.Recipe.Name;
            var recipeSessionCancellationToken = projectRecipeSessionCts.Token;

            // The Recipe session commits Data before it publishes the selected
            // Recipe identity, but the previous ListBox selection can still
            // raise a queued WPF SelectionChanged callback afterwards. Apply
            // the same canonical recipe purpose once bindings have settled so
            // a previous recipe cannot repaint the new recipe as
            // segmentation/anomaly by mistake.
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => ApplyDatasetPurposeAfterBindingsSettle(
                    purpose,
                    recipeName,
                    persistAfterBindingsSettle,
                    recipeSessionCancellationToken)));
        }

        private void ApplyDatasetPurposeAfterBindingsSettle(
            LabelingDatasetPurpose purpose,
            string recipeName,
            bool persistAfterBindingsSettle,
            CancellationToken recipeSessionCancellationToken)
        {
            if (isApplicationCloseApproved
                || recipeSessionCancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (GetCurrentDatasetPurpose() != purpose)
            {
                SynchronizeDatasetPurposeToCurrentProject(purpose);
            }

            if (!persistAfterBindingsSettle
                || !string.Equals(global.Recipe.Name, recipeName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            RecipeConfigurationSaveResult saveResult = projectRecipeSessionService.SaveConfiguration(
                global.Data,
                recipeName,
                updateYoloDataYaml: false,
                refreshDatasetVersion: true);
            if (!saveResult.IsSuccess)
            {
                AppendLog($"\uB370\uC774\uD130\uC14B \uC6A9\uB3C4 \uC800\uC7A5 \uC2E4\uD328: {saveResult.ErrorMessage}");
            }
        }

        private void SynchronizeDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
        {
            CancelFourPointBoxDraft(updateStatus: false);
            EnsureProjectSettings();
            global.Data.ProjectSettings.DatasetPurpose = purpose;
            CanvasPanelViewModel?.RestoreSmartMaskAutoContourMode(
                global.Data.ProjectSettings.SmartMaskAutoContourEnabled);
            CanvasPanelViewModel?.RestoreBoxDrawingMethod(global.Data.ProjectSettings.BoxDrawingMethod);
            LearningWorkflowViewModel?.ApplyDatasetPurpose(purpose);

            // Recipe creation can run while the dataset-purpose ListBox still
            // holds the previous recipe's selection. Reconcile the view adapter
            // with the ViewModel before its SelectionChanged command can write
            // that stale purpose back into the newly created recipe.
            if (DatasetPurposeListBox != null)
            {
                BindingOperations.GetBindingExpression(
                    DatasetPurposeListBox,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.UpdateTarget();
            }

            RefreshCanvasAnnotationToolScope();
            ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
            RefreshCanvasWorkflowContext();
            RefreshAnnotationVisibilityForDatasetPurpose();
            RefreshShellDatasetContext();
        }
        #endregion

    }
}
