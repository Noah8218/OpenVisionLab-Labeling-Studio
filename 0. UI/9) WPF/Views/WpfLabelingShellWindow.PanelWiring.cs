using Lib.Common;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.Views;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {

        private void ComposePanelViewModels()
        {
            // Keep ViewModels out of UserControls; the shell composes data contexts so each View can be constructed standalone.
            LearningWorkflowPanelControl.DataContext = LearningWorkflowViewModel;
            ImageQueuePanelControl.DataContext = ImageQueueViewModel;
            CanvasPanelControl.DataContext = CanvasPanelViewModel;
            ObjectReviewPanelControl.DataContext = ObjectReviewViewModel;
            CandidateReviewPanelControl.DataContext = CandidateReviewViewModel;
            ClassCatalogPanelControl.DataContext = ClassCatalogViewModel;
            YoloStatusPanelControl.DataContext = YoloStatusViewModel;
            ProjectConfigPanelControl.DataContext = ProjectConfigViewModel;
            YoloModelSettingsPanelControl.DataContext = YoloModelSettingsViewModel;
            TrainingSettingsPanelControl.DataContext = TrainingSettingsViewModel;
            StatusBarPanelControl.DataContext = StatusBarViewModel;
            ShellLogPanelControl.DataContext = ShellLogViewModel;
        }

        private static void RefreshAttachedCommandBindings(DependencyObject target, params DependencyProperty[] properties)
        {
            if (target == null || properties == null)
            {
                return;
            }

            // Command ViewModels are injected after InitializeComponent; refresh attached-event bindings before the first user input.
            foreach (DependencyProperty property in properties)
            {
                BindingOperations.GetBindingExpression(target, property)?.UpdateTarget();
            }
        }

        private void SelectRightWorkflowView(TabItem tab)
        {
            if (tab == null)
            {
                return;
            }

            if (ReviewTabControl != null)
            {
                ReviewTabControl.SelectedItem = tab;
            }

            tab.IsSelected = true;
        }

        private void ShowSavedLabelsWorkflowView()
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Labeling);
            ShellViewModel?.SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(ObjectsReviewTab);
        }

        private void ShowCandidateReviewWorkflowView()
        {
            SetWorkflowMode(WorkflowMode.Inference);
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Inference);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(CandidatesReviewTab);
        }

        private void ShowGuideToolsWorkflowView(WpfShellWorkflowStage stage)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                LearningWorkflowViewModel?.ShowDatasetOnboarding();
            }
            else if (stage == WpfShellWorkflowStage.Labeling)
            {
                LearningWorkflowViewModel?.ShowLabelingTask();
            }

            ShellViewModel?.SetWorkflowStage(stage);
            ShellViewModel?.SetRightWorkflowShortcut(stage == WpfShellWorkflowStage.Labeling
                || stage == WpfShellWorkflowStage.Dataset
                ? WpfRightWorkflowShortcut.LabelingGuide
                : WpfRightWorkflowShortcut.None);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(LearningReviewTab);
        }

        private void ShowClassCatalogWorkflowView(WpfShellWorkflowStage stage)
        {
            ShellViewModel?.SetWorkflowStage(stage);
            ShellViewModel?.SetRightWorkflowShortcut(stage == WpfShellWorkflowStage.Labeling
                || stage == WpfShellWorkflowStage.Dataset
                ? WpfRightWorkflowShortcut.ClassCatalog
                : WpfRightWorkflowShortcut.None);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(ClassesReviewTab);
        }

        private void ShowYoloModelCenterWorkflowView()
        {
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            ShellViewModel?.SetRightWorkflowDockExpanded(true);
            SelectRightWorkflowView(YoloSettingsReviewTab);
        }

        private void ConfigureLabelingCanvasDefaults()
        {
            MainCanvasViewModel.ShowGroupNames = false;
            MainCanvasViewModel.ShowRoiItemNames = false;
            MainCanvasViewModel.ShowGroupBounds = false;
            MainCanvasViewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
            MainCanvasViewModel.ShouldDrawOverExistingRoi = ShouldDrawOverExistingRoiForCurrentClass;
        }

        private void SeedImageQueueInputCommands()
        {
            // The shell is the composition root; seed behavior commands so pre-Loaded queue selection uses the same path as real clicks.
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueFilterBox, ImageQueueViewModel.FilterSelectionChangedCommand);
            InputCommandBehaviors.SetTextInputCommand(ImageQueueSearchBox, ImageQueueViewModel.SearchTextChangedCommand);
            InputCommandBehaviors.SetSelectedItemChangedCommand(ImageQueueGrid, ImageQueueViewModel.QueueSelectionChangedCommand);
            InputCommandBehaviors.SetMouseDoubleClickInputCommand(ImageQueueGrid, ImageQueueViewModel.QueueMouseDoubleClickCommand);
        }


        public void FocusYoloSettingsTab()
        {
            ShowYoloModelCenterWorkflowView();
            CollapseYoloAdvancedSettingsForOverview();
            UpdateLayout();
            YoloSettingsScrollViewer?.ScrollToTop();
        }

        private void FocusYoloModelSettingsTab()
        {
            ShowYoloModelCenterWorkflowView();
            CollapseYoloAdvancedSettingsForOverview();
            YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            UpdateLayout();
            YoloModelSettingsPanelControl?.BringIntoView();
        }

        private void FocusYoloTrainingSettingsTab()
        {
            ShowYoloModelCenterWorkflowView();
            CollapseYoloAdvancedSettingsForOverview();
            TrainingSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            UpdateLayout();
            TrainingSettingsPanelControl?.BringIntoView();
        }

        private void CollapseYoloAdvancedSettingsForOverview()
        {
            // Open the YOLO tab as an operator overview. Expanding every editor hides
            // the next action, so specific workflows opt into only the panel they need.
            YoloRuntimeDetailsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            ProjectConfigPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            TrainingSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
        }

        public void FocusAnnotationToolsTab()
        {
            ShowGuideToolsWorkflowView(WpfShellWorkflowStage.Labeling);
            UpdateLayout();
            LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
        }

        private void FocusCurrentStageGuideToolsTab()
        {
            if (ShellViewModel?.IsDatasetStageActive == true)
            {
                FocusDatasetOnboardingTab();
                return;
            }

            FocusAnnotationToolsTab();
        }

        private void FocusDatasetOnboardingTab()
        {
            ShowGuideToolsWorkflowView(WpfShellWorkflowStage.Dataset);
            UpdateLayout();
            LearningWorkflowPanelControl?.ShowDatasetSetupStart();
        }

        private void FocusDatasetOnboardingTabIfNoActiveImage()
        {
            if (activeImageBitmap != null && imageQueueItems.Count > 0)
            {
                return;
            }

            FocusDatasetOnboardingTab();
        }

        private void FocusLabelingSidePanelForTool(WpfAnnotationTool tool)
        {
            // Tool selection is a low-frequency UX event. Keep this out of brush/ROI
            // MouseMove/MouseUp paths so the side panel helps orientation without
            // competing with drawing performance or forcing tab changes per stroke.
            if (currentWorkflowMode != WorkflowMode.Labeling)
            {
                return;
            }

            switch (tool)
            {
                case WpfAnnotationTool.Rectangle:
                case WpfAnnotationTool.Ellipse:
                case WpfAnnotationTool.Polygon:
                case WpfAnnotationTool.Brush:
                case WpfAnnotationTool.Eraser:
                case WpfAnnotationTool.PanZoom:
                    FocusAnnotationToolsTab();
                    break;

                case WpfAnnotationTool.Select:
                case WpfAnnotationTool.Delete:
                    if (HasCanvasLabelObjects())
                    {
                        ShowSavedLabelsWorkflowView();
                    }
                    else
                    {
                        FocusAnnotationToolsTab();
                    }
                    break;
            }
        }

        private void InitializeYoloEditorPanel()
        {
            EnsureProjectSettings();
            TrainingCfgBox.ItemsSource = Enum.GetNames(typeof(CYolov5TrainingParam.Cfg));
            TrainingWeightBox.ItemsSource = Enum.GetNames(typeof(CYolov5TrainingParam.Weight));
            PopulateProjectConfigPanelFields();
            PopulateYoloEditorFields();
            PopulateTrainingEditorFields();
            double configuredConfidence = global.Data.ProjectSettings.PythonModel.MinimumDetectionConfidence;
            CandidateConfidenceSlider.Value = Math.Clamp(configuredConfidence, 0D, 1D);
            UpdateCandidateConfidenceText();
        }

        private void PopulateYoloEditorFields()
        {
            EnsureProjectSettings();
            YoloModelSettingsViewModel?.LoadFrom(global.Data.ProjectSettings.PythonModel, global.Data.ProjectSettings.AnomalyClassification);
        }

        private void PopulateTrainingEditorFields()
        {
            EnsureProjectSettings();
            TrainingSettingsViewModel?.LoadFrom(global.Data.GetTrainingSettings(), global.Data.ProjectSettings.YoloDataset);
        }

        private void ConfigureShellCommands()
        {
            ShellViewModel.ConfigureCommands(
                ExecuteToggleThemeCommand,
                ExecuteLoadSampleCommand,
                ExecuteAddSampleRoiCommand,
                ExecuteSaveAnnotationsCommand,
                ExecuteLabelingModeCommand,
                ExecuteInferenceModeCommand,
                ExecuteCheckYoloCommand,
                ExecuteDetectCurrentImageCommand,
                TemplateMatchingAutoLabelViewModel.RunCurrentImage,
                ExecuteChangeDatasetCommand,
                ExecuteOpenDatasetRootFolderCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteLoadedCommand,
                ExecuteClosedCommand,
                ExecuteShellPreviewKeyDownCommand,
                ExecuteDatasetHomeCommand,
                ExecuteLabelingWorkbenchCommand,
                ExecuteInferenceReviewCommand,
                ExecuteTrainingModelCenterCommand,
                ExecuteReviewCandidateModelCommand,
                ShowSavedLabelsWorkflowView,
                FocusCurrentStageGuideToolsTab,
                FocusClassCatalogTab,
                ExecutePromoteSelectedModelHistoryCommand,
                ExecuteRunAnomalyEvaluationCommand,
                ExecuteLoadAnomalyEvaluationSummaryCommand);
            RefreshAttachedCommandBindings(
                this,
                WindowLifecycleCommandBehavior.LoadedCommandProperty,
                WindowLifecycleCommandBehavior.ClosedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
            RefreshAttachedCommandBindings(DatasetHomeStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(LabelingWorkbenchStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(InferenceReviewStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            RefreshAttachedCommandBindings(TrainingModelStageButton, System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
        }
    }
}
