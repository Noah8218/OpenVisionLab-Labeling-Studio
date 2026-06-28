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

        private void ConfigureLabelingCanvasDefaults()
        {
            MainCanvasViewModel.ShowGroupNames = false;
            MainCanvasViewModel.ShowRoiItemNames = false;
            MainCanvasViewModel.ShowGroupBounds = false;
            MainCanvasViewModel.DrawingShapeKind = CanvasRoiShapeKind.Rectangle;
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
            YoloSettingsReviewTab.IsSelected = true;
            CollapseYoloAdvancedSettingsForOverview();
            UpdateLayout();
            YoloSettingsScrollViewer?.ScrollToTop();
        }

        private void FocusYoloModelSettingsTab()
        {
            YoloSettingsReviewTab.IsSelected = true;
            CollapseYoloAdvancedSettingsForOverview();
            YoloModelSettingsPanelControl?.SettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            UpdateLayout();
            YoloModelSettingsPanelControl?.BringIntoView();
        }

        private void FocusYoloTrainingSettingsTab()
        {
            YoloSettingsReviewTab.IsSelected = true;
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
            LearningReviewTab.IsSelected = true;
            UpdateLayout();
            LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
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
                        ObjectsReviewTab.IsSelected = true;
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
            YoloModelSettingsViewModel?.LoadFrom(global.Data.ProjectSettings.PythonModel);
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
                ExecuteLoadedCommand,
                ExecuteClosedCommand,
                ExecuteShellPreviewKeyDownCommand);
            RefreshAttachedCommandBindings(
                this,
                WindowLifecycleCommandBehavior.LoadedCommandProperty,
                WindowLifecycleCommandBehavior.ClosedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }
    }
}
