using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLearningWorkflowPanel : UserControl
    {
        public WpfLearningWorkflowPanel()
        {
            DataContext = ViewModel;
            InitializeComponent();
        }

        public WpfLearningWorkflowPanelViewModel ViewModel { get; } = new WpfLearningWorkflowPanelViewModel();

        public event SelectionChangedEventHandler LearningModeSelectionChanged;

        public event SelectionChangedEventHandler AnnotationToolSelectionChanged;

        public event SelectionChangedEventHandler LearningStepSelectionChanged;

        public event EventHandler<WpfYoloTrainingWorkflowStepEventArgs> YoloTrainingWorkflowStepRequested;

        public event RoutedEventHandler TutorialOpenHtmlGuideRequested;

        public event RoutedEventHandler YoloFixClassesRequested;

        public event RoutedEventHandler YoloFixLabelsRequested;

        public event RoutedEventHandler YoloFixDatasetRequested;

        public ListBox ModeList => LearningModeListBox;

        public ListBox ToolList => AnnotationToolListBox;

        public ListBox StepList => LearningStepListBox;

        public ScrollViewer WorkflowScrollViewer => LearningWorkflowScrollViewer;

        public Expander LearningConcepts => LearningConceptsExpander;

        public TextBlock GroundTruthTextBlock => GroundTruthChipText;

        public TextBlock PredictionTextBlock => PredictionChipText;

        public ItemsControl YoloTrainingWorkflowList => YoloTrainingWorkflowItemsControl;

        public TextBlock YoloTrainingWorkflowSummary => YoloTrainingWorkflowSummaryText;

        public TextBlock YoloTrainingChecklistStatus => YoloTrainingChecklistStatusText;

        public TextBlock YoloTrainingChecklistDetail => YoloTrainingChecklistDetailText;

        public TextBlock YoloTrainingChecklistAction => YoloTrainingChecklistActionText;

        public TextBlock YoloTrainingHistory => YoloTrainingHistoryText;

        public ItemsControl YoloTrainingRunHistoryList => YoloTrainingRunHistoryItemsControl;

        public Button YoloFixClasses => YoloFixClassesButton;

        public Button YoloFixLabels => YoloFixLabelsButton;

        public Button YoloFixDataset => YoloFixDatasetButton;

        public Button TutorialOpenHtmlGuide => TutorialOpenHtmlGuideButton;

        public void ShowAnnotationToolPalette()
        {
            LearningConceptsExpander.IsExpanded = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                AnnotationToolListBox.UpdateLayout();
                if (AnnotationToolListBox.SelectedItem != null)
                {
                    AnnotationToolListBox.ScrollIntoView(AnnotationToolListBox.SelectedItem);
                }

                AnnotationToolHeaderText.BringIntoView();
            }), DispatcherPriority.Background);
        }

        private void LearningModeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => LearningModeSelectionChanged?.Invoke(sender, e);

        private void AnnotationToolListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => AnnotationToolSelectionChanged?.Invoke(sender, e);

        private void LearningStepListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => LearningStepSelectionChanged?.Invoke(sender, e);

        private void YoloTrainingWorkflowStep_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is WpfYoloTrainingWorkflowStepItem step)
            {
                YoloTrainingWorkflowStepRequested?.Invoke(this, new WpfYoloTrainingWorkflowStepEventArgs(step));
                e.Handled = true;
            }
        }

        private void ScrollableChild_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (LearningWorkflowScrollViewer == null)
            {
                return;
            }

            e.Handled = true;
            LearningWorkflowScrollViewer.ScrollToVerticalOffset(LearningWorkflowScrollViewer.VerticalOffset - e.Delta);
        }

        private void TutorialOpenHtmlGuideButton_Click(object sender, RoutedEventArgs e)
            => TutorialOpenHtmlGuideRequested?.Invoke(sender, e);

        private void YoloFixClassesButton_Click(object sender, RoutedEventArgs e)
            => YoloFixClassesRequested?.Invoke(sender, e);

        private void YoloFixLabelsButton_Click(object sender, RoutedEventArgs e)
            => YoloFixLabelsRequested?.Invoke(sender, e);

        private void YoloFixDatasetButton_Click(object sender, RoutedEventArgs e)
            => YoloFixDatasetRequested?.Invoke(sender, e);
    }

    public sealed class WpfYoloTrainingWorkflowStepEventArgs : EventArgs
    {
        public WpfYoloTrainingWorkflowStepEventArgs(WpfYoloTrainingWorkflowStepItem step)
        {
            Step = step;
        }

        public WpfYoloTrainingWorkflowStepItem Step { get; }
    }
}
