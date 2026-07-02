using OpenVisionLab.Mvvm.Behaviors;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Settings/status panel wiring is kept apart from workflow wiring to make configuration commands easier to audit.
        private void ConfigureClassCatalogPanelCommands()
        {
            ClassCatalogViewModel.ConfigureCommands(
                args => ClassNameBox_KeyDown(ClassNameBox, args),
                ExecuteAddClassCommand,
                ExecuteRenameClassCommand,
                ExecuteRemoveClassCommand,
                ExecuteApplyClassColorCommand,
                selected => ClassListBox_SelectionChanged(ClassListBox, selected));
            RefreshAttachedCommandBindings(ClassNameBox, InputCommandBehaviors.PreviewKeyInputCommandProperty);
            RefreshAttachedCommandBindings(ClassListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterClassCatalogPanelNames()
        {
            ConfigureClassCatalogPanelCommands();
            RegisterClassCatalogName(nameof(ClassCatalogGuidePanel), ClassCatalogGuidePanel);
            RegisterClassCatalogName(nameof(ClassCatalogGuideTitleText), ClassCatalogGuideTitleText);
            RegisterClassCatalogName(nameof(ClassCatalogGuideDetailText), ClassCatalogGuideDetailText);
            RegisterClassCatalogName(nameof(ClassCatalogSummaryText), ClassCatalogSummaryText);
            RegisterClassCatalogName(nameof(CurrentDrawingClassTitleText), CurrentDrawingClassTitleText);
            RegisterClassCatalogName(nameof(CurrentDrawingClassDetailText), CurrentDrawingClassDetailText);
            RegisterClassCatalogName(nameof(ClassCatalogActionText), ClassCatalogActionText);
            RegisterClassCatalogName(nameof(ClassNameBox), ClassNameBox);
            RegisterClassCatalogName(nameof(AddClassButton), AddClassButton);
            RegisterClassCatalogName(nameof(RenameClassButton), RenameClassButton);
            RegisterClassCatalogName(nameof(RemoveClassButton), RemoveClassButton);
            RegisterClassCatalogName(nameof(ClassColorBox), ClassColorBox);
            RegisterClassCatalogName(nameof(ClassColorAdvancedPanel), ClassColorAdvancedPanel);
            RegisterClassCatalogName(nameof(ApplyClassColorButton), ApplyClassColorButton);
            RegisterClassCatalogName(nameof(ClassEditStatusText), ClassEditStatusText);
            RegisterClassCatalogName(nameof(ClassListBox), ClassListBox);
        }

        private void RegisterClassCatalogName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureYoloStatusPanelCommands()
        {
            YoloStatusViewModel.ConfigureCommands(
                ExecuteCheckYoloCommand,
                ExecuteInstallRequirementsCommand,
                ExecuteRunYoloSmokeCommand,
                ExecuteRestartPythonWorkerCommand,
                ExecuteStopPythonWorkerCommand);
        }

        private void RegisterYoloStatusPanelNames()
        {
            ConfigureYoloStatusPanelCommands();
            RegisterYoloStatusName(nameof(YoloSettingsSummaryText), YoloSettingsSummaryText);
            RegisterYoloStatusName(nameof(YoloRuntimeDetailsExpander), YoloRuntimeDetailsExpander);
            RegisterYoloStatusName(nameof(YoloSettingsDetailText), YoloSettingsDetailText);
            RegisterYoloStatusName(nameof(FirstCheckYoloButton), FirstCheckYoloButton);
            RegisterYoloStatusName(nameof(InstallRequirementsButton), InstallRequirementsButton);
            RegisterYoloStatusName(nameof(RunYoloSmokeButton), RunYoloSmokeButton);
            RegisterYoloStatusName(nameof(RestartPythonWorkerButton), RestartPythonWorkerButton);
            RegisterYoloStatusName(nameof(StopPythonWorkerButton), StopPythonWorkerButton);
            RegisterYoloStatusName(nameof(YoloCommandStatusText), YoloCommandStatusText);
            RegisterYoloStatusName(nameof(YoloCommandProgressBar), YoloCommandProgressBar);
        }

        private void RegisterYoloStatusName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureProjectConfigPanelCommands()
        {
            ProjectConfigViewModel.ConfigureCommands(
                ExecuteApplyProjectRecipeCommand,
                ExecuteRefreshProjectRecipeListCommand,
                ExecuteSaveProjectConfigCommand,
                ExecuteOpenProjectConfigFolderCommand,
                selected => ProjectRecipeListBox_SelectionChanged(ProjectRecipeListBox, selected));
            RefreshAttachedCommandBindings(ProjectRecipeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterProjectConfigPanelNames()
        {
            ConfigureProjectConfigPanelCommands();
            RegisterProjectConfigName(nameof(ProjectConfigExpander), ProjectConfigExpander);
            RegisterProjectConfigName(nameof(ProjectRecipeNameBox), ProjectRecipeNameBox);
            RegisterProjectConfigName(nameof(ProjectRecipeListBox), ProjectRecipeListBox);
            RegisterProjectConfigName(nameof(ProjectConfigPathBox), ProjectConfigPathBox);
            RegisterProjectConfigName(nameof(ProjectManifestPathBox), ProjectManifestPathBox);
            RegisterProjectConfigName(nameof(ProjectConfigStatusText), ProjectConfigStatusText);
            RegisterProjectConfigName(nameof(ApplyProjectRecipeButton), ApplyProjectRecipeButton);
            RegisterProjectConfigName(nameof(RefreshProjectRecipeListButton), RefreshProjectRecipeListButton);
            RegisterProjectConfigName(nameof(SaveProjectConfigButton), SaveProjectConfigButton);
            RegisterProjectConfigName(nameof(OpenProjectConfigFolderButton), OpenProjectConfigFolderButton);
        }

        private void RegisterProjectConfigName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureYoloModelSettingsPanelCommands()
        {
            YoloModelSettingsViewModel.ConfigureCommands(
                ExecuteBrowseYoloPythonCommand,
                ExecuteBrowseYoloProjectRootCommand,
                ExecuteBrowseYoloClientScriptCommand,
                ExecuteBrowseYoloWeightsCommand,
                ExecuteBrowseYoloImageRootCommand,
                ExecuteSaveYoloSettingsCommand,
                ExecuteResetYoloSettingsCommand,
                ExecuteRuntimeProfileActionCommand,
                ExecuteInstallUltralyticsPackageCommand,
                ExecuteUninstallUltralyticsPackageCommand);
        }

        private void RegisterYoloModelSettingsPanelNames()
        {
            ConfigureYoloModelSettingsPanelCommands();
            RegisterYoloModelSettingsName(nameof(YoloInspectionModelQuickPanel), YoloInspectionModelQuickPanel);
            RegisterYoloModelSettingsName(nameof(YoloPythonPathBox), YoloPythonPathBox);
            RegisterYoloModelSettingsName(nameof(YoloModelEngineBox), YoloModelEngineBox);
            RegisterYoloModelSettingsName(nameof(YoloProjectRootBox), YoloProjectRootBox);
            RegisterYoloModelSettingsName(nameof(YoloClientScriptBox), YoloClientScriptBox);
            RegisterYoloModelSettingsName(nameof(YoloWeightsPathBox), YoloWeightsPathBox);
            RegisterYoloModelSettingsName(nameof(YoloImageRootBox), YoloImageRootBox);
            RegisterYoloModelSettingsName(nameof(YoloConfidenceBox), YoloConfidenceBox);
            RegisterYoloModelSettingsName(nameof(YoloInferenceImageSizeBox), YoloInferenceImageSizeBox);
            RegisterYoloModelSettingsName(nameof(YoloMaxCandidatesBox), YoloMaxCandidatesBox);
            RegisterYoloModelSettingsName(nameof(YoloTimeoutBox), YoloTimeoutBox);
            RegisterYoloModelSettingsName(nameof(YoloAutoStartCheckBox), YoloAutoStartCheckBox);
            RegisterYoloModelSettingsName(nameof(BrowseYoloPythonButton), BrowseYoloPythonButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloProjectRootButton), BrowseYoloProjectRootButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloClientScriptButton), BrowseYoloClientScriptButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloWeightsButton), BrowseYoloWeightsButton);
            RegisterYoloModelSettingsName(nameof(BrowseYoloImageRootButton), BrowseYoloImageRootButton);
            RegisterYoloModelSettingsName(nameof(SaveYoloSettingsButton), SaveYoloSettingsButton);
            RegisterYoloModelSettingsName(nameof(ResetYoloSettingsButton), ResetYoloSettingsButton);
            RegisterYoloModelSettingsName(nameof(YoloRuntimeInstallPackageButton), YoloRuntimeInstallPackageButton);
            RegisterYoloModelSettingsName(nameof(YoloRuntimeUninstallPackageButton), YoloRuntimeUninstallPackageButton);
        }

        private void RegisterYoloModelSettingsName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureTrainingSettingsPanelCommands()
        {
            TrainingSettingsViewModel.ConfigureCommands(
                ExecuteRefreshTrainingReadinessCommand,
                ExecuteStartTrainingCommand,
                ExecuteStopTrainingCommand,
                ExecuteReviewCandidateModelCommand,
                ExecuteSaveYoloSettingsCommand);
        }

        private void RegisterTrainingSettingsPanelNames()
        {
            ConfigureTrainingSettingsPanelCommands();
            RegisterTrainingSettingsName(nameof(TrainingSettingsExpander), TrainingSettingsExpander);
            RegisterTrainingSettingsName(nameof(PostTrainingModelActionPanel), PostTrainingModelActionPanel);
            RegisterTrainingSettingsName(nameof(PostTrainingModelStatusText), PostTrainingModelStatusText);
            RegisterTrainingSettingsName(nameof(PostTrainingModelDetailText), PostTrainingModelDetailText);
            RegisterTrainingSettingsName(nameof(ReviewTrainedModelButton), ReviewTrainedModelButton);
            RegisterTrainingSettingsName(nameof(ConfirmTrainedModelButton), ConfirmTrainedModelButton);
            RegisterTrainingSettingsName(nameof(TrainingImageSizeBox), TrainingImageSizeBox);
            RegisterTrainingSettingsName(nameof(TrainingBatchBox), TrainingBatchBox);
            RegisterTrainingSettingsName(nameof(TrainingEpochBox), TrainingEpochBox);
            RegisterTrainingSettingsName(nameof(TrainingCfgBox), TrainingCfgBox);
            RegisterTrainingSettingsName(nameof(TrainingWeightBox), TrainingWeightBox);
            RegisterTrainingSettingsName(nameof(TrainingValidationPercentBox), TrainingValidationPercentBox);
            RegisterTrainingSettingsName(nameof(TrainingTestPercentBox), TrainingTestPercentBox);
            RegisterTrainingSettingsName(nameof(TrainingSplitSeedBox), TrainingSplitSeedBox);
            RegisterTrainingSettingsName(nameof(TrainingSplitPolicyHintText), TrainingSplitPolicyHintText);
            RegisterTrainingSettingsName(nameof(ApplyFastTrainingPresetButton), ApplyFastTrainingPresetButton);
            RegisterTrainingSettingsName(nameof(RefreshTrainingReadinessButton), RefreshTrainingReadinessButton);
            RegisterTrainingSettingsName(nameof(StartTrainingButton), StartTrainingButton);
            RegisterTrainingSettingsName(nameof(StopTrainingButton), StopTrainingButton);
            RegisterTrainingSettingsName(nameof(TrainingReadinessText), TrainingReadinessText);
            RegisterTrainingSettingsName(nameof(TrainingProgressBar), TrainingProgressBar);
            RegisterTrainingSettingsName(nameof(TrainingProgressText), TrainingProgressText);
            RegisterTrainingSettingsName(nameof(TrainingEpochText), TrainingEpochText);
        }

        private void RegisterTrainingSettingsName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void RegisterStatusBarPanelNames()
        {
            RegisterStatusBarName(nameof(DatasetStatusText), DatasetStatusText);
            RegisterStatusBarName(nameof(WorkflowStageText), WorkflowStageText);
            RegisterStatusBarName(nameof(WorkflowProgressText), WorkflowProgressText);
            RegisterStatusBarName(nameof(WorkflowNextActionText), WorkflowNextActionText);
            RegisterStatusBarName(nameof(PythonStatusText), PythonStatusText);
            RegisterStatusBarName(nameof(AnnotationSaveStatusText), AnnotationSaveStatusText);
            RegisterStatusBarName(nameof(InspectionModelStatusText), InspectionModelStatusText);
            RegisterStatusBarName(nameof(ModelStatusText), ModelStatusText);
        }

        private void RegisterStatusBarName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void RegisterShellLogPanelNames()
        {
            RegisterShellLogName(nameof(ShellLogPanel), ShellLogPanel);
        }

        private void RegisterShellLogName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }

        }
    }
}
