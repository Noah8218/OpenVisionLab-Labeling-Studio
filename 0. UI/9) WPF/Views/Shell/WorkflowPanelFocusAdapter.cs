using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Owns the WPF-only focus and tab presentation for the workflow panels.
    // Workflow policy remains in the Shell ViewModel and navigation adapter.
    internal sealed class WorkflowPanelFocusAdapter
    {
        private readonly WorkflowPanelFocusAdapterContext context;

        internal WorkflowPanelFocusAdapter(WorkflowPanelFocusAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void FocusYoloSettingsTab()
        {
            context.ShowYoloModelCenterWorkflowView?.Invoke();
            if (context.YoloModelCenterTaskTabs != null)
            {
                context.YoloModelCenterTaskTabs.SelectedItem = context.YoloModelCenterDataTaskTab;
            }

            CollapseYoloAdvancedSettingsForOverview();
            context.YoloDatasetReadinessQuickPanel?.SetCurrentValue(Expander.IsExpandedProperty, false);
            context.UpdateLayout?.Invoke();
            context.YoloSettingsScrollViewer?.ScrollToTop();
        }

        internal void FocusYoloModelSettingsTab()
        {
            context.ShowYoloModelCenterWorkflowView?.Invoke();
            context.SelectTrainingRuntimeTab?.Invoke();
            context.YoloModelSettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            context.UpdateLayout?.Invoke();
            context.YoloModelSettingsPanel?.BringIntoView();
        }

        internal void FocusYoloTrainingSettingsTab()
        {
            context.ShowYoloModelCenterWorkflowView?.Invoke();
            if (context.YoloModelCenterTaskTabs != null)
            {
                context.YoloModelCenterTaskTabs.SelectedItem = context.YoloModelCenterDataTaskTab;
            }

            CollapseYoloAdvancedSettingsForOverview();
            context.TrainingSettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, true);
            context.UpdateLayout?.Invoke();
            context.TrainingSettingsPanel?.BringIntoView();
        }

        internal void YoloModelCenterTaskTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e == null || !ReferenceEquals(e.OriginalSource, context.YoloModelCenterTaskTabs))
            {
                return;
            }

            // InitializeComponent can raise SelectionChanged before the model panels are composed.
            if (!context.AreModelWorkflowPanelsComposed())
            {
                return;
            }

            context.YoloSettingsScrollViewer?.ScrollToTop();
            context.TrainingSettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, context.YoloModelCenterDataTaskTab?.IsSelected != true);
            if (context.YoloModelCenterDataTaskTab?.IsSelected == true)
            {
                context.YoloDatasetReadinessQuickPanel?.SetCurrentValue(Expander.IsExpandedProperty, false);
            }
            else if (context.YoloModelCenterOverviewTaskTab?.IsSelected == true && !context.IsModelComparisonVisible())
            {
                try
                {
                    context.UpdateTrainingComparisonViewModel?.Invoke(context.BuildCurrentTrainingWeightsComparison?.Invoke());
                }
                catch (Exception ex)
                {
                    context.AppendLog?.Invoke($"모델 비교 요약 불러오기 실패: {ex.Message}");
                }
            }
        }

        internal void CollapseYoloAdvancedSettingsForOverview()
        {
            // Preparation keeps conditions visible in its summary; explicit settings routes expand the editor.
            context.YoloRuntimeDetailsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            context.ProjectConfigExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            context.YoloModelSettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
            context.TrainingSettingsExpander?.SetCurrentValue(Expander.IsExpandedProperty, false);
        }

        internal void FocusAnnotationToolsTab()
        {
            context.ShowGuideToolsWorkflowView?.Invoke(WpfShellWorkflowStage.Labeling);
            context.UpdateLayout?.Invoke();
            context.ShowAnnotationToolPalette?.Invoke();
        }

        internal void FocusCurrentStageGuideToolsTab()
        {
            if (context.IsDatasetStageActive())
            {
                FocusDatasetOnboardingTab();
                return;
            }

            FocusAnnotationToolsTab();
        }

        internal void FocusDatasetOnboardingTab()
        {
            context.ShowGuideToolsWorkflowView?.Invoke(WpfShellWorkflowStage.Dataset);
            context.UpdateLayout?.Invoke();
            context.ShowDatasetSetupStart?.Invoke();
        }

        internal void FocusDatasetOnboardingTabIfNoActiveImage()
        {
            if (context.HasActiveImage() && context.ImageQueueItemCount() > 0)
            {
                return;
            }

            FocusDatasetOnboardingTab();
        }

        internal void FocusLabelingSidePanelForTool(WpfAnnotationTool tool)
        {
            // Tool selection is low frequency; do not force tab changes in brush/ROI mouse paths.
            if (context.IsInferenceWorkflowActive())
            {
                return;
            }

            // The labeling workbench keeps the operator's class palette, saved
            // objects, and edit history visible while a tool is active. Tool
            // options are already rendered in the canvas toolbar; switching the
            // review dock to the learning guide on every P/B/E selection hides
            // the very context needed to continue editing the same object.
            if (!context.IsDatasetStageActive())
            {
                switch (tool)
                {
                    case WpfAnnotationTool.Rectangle:
                    case WpfAnnotationTool.Ellipse:
                    case WpfAnnotationTool.Polygon:
                    case WpfAnnotationTool.Brush:
                    case WpfAnnotationTool.Eraser:
                    case WpfAnnotationTool.PanZoom:
                    case WpfAnnotationTool.Select:
                    case WpfAnnotationTool.Delete:
                        context.ShowSavedLabelsWorkflowView?.Invoke();
                        return;
                }
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
                    if (context.HasCanvasLabelObjects())
                    {
                        context.ShowSavedLabelsWorkflowView?.Invoke();
                    }
                    else
                    {
                        FocusAnnotationToolsTab();
                    }
                    break;
            }
        }
    }

    internal sealed class WorkflowPanelFocusAdapterContext
    {
        internal Action ShowYoloModelCenterWorkflowView { get; init; }
        internal Action SelectTrainingRuntimeTab { get; init; }
        internal Action<WpfShellWorkflowStage> ShowGuideToolsWorkflowView { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Action UpdateLayout { get; init; }
        internal Action ShowAnnotationToolPalette { get; init; }
        internal Action ShowDatasetSetupStart { get; init; }
        internal Func<bool> IsInferenceWorkflowActive { get; init; }
        internal Func<bool> HasCanvasLabelObjects { get; init; }
        internal Func<bool> IsDatasetStageActive { get; init; }
        internal Func<bool> HasActiveImage { get; init; }
        internal Func<int> ImageQueueItemCount { get; init; }
        internal Func<bool> AreModelWorkflowPanelsComposed { get; init; }
        internal Func<bool> IsModelComparisonVisible { get; init; }
        internal Func<WpfTrainingWeightsComparison> BuildCurrentTrainingWeightsComparison { get; init; }
        internal Action<WpfTrainingWeightsComparison> UpdateTrainingComparisonViewModel { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal TabControl YoloModelCenterTaskTabs { get; init; }
        internal TabItem YoloModelCenterOverviewTaskTab { get; init; }
        internal TabItem YoloModelCenterDataTaskTab { get; init; }
        internal TabItem YoloModelCenterTrainingTaskTab { get; init; }
        internal Expander TrainingEnvironmentDetailsExpander { get; init; }
        internal ScrollViewer YoloSettingsScrollViewer { get; init; }
        internal Expander YoloDatasetReadinessQuickPanel { get; init; }
        internal Expander YoloRuntimeDetailsExpander { get; init; }
        internal Expander ProjectConfigExpander { get; init; }
        internal Expander YoloModelSettingsExpander { get; init; }
        internal Expander TrainingSettingsExpander { get; init; }
        internal FrameworkElement YoloModelSettingsPanel { get; init; }
        internal FrameworkElement TrainingSettingsPanel { get; init; }
    }
}
