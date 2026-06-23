using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLearningWorkflowPanel : UserControl
    {
        public WpfLearningWorkflowPanel()
        {
            InitializeComponent();
        }

        public WpfLearningWorkflowPanelViewModel ViewModel => DataContext as WpfLearningWorkflowPanelViewModel;

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
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Labeling tools are the operator's primary controls, so keep them
                // reachable without expanding the secondary learning concepts section.
                LearningWorkflowScrollViewer.ScrollToTop();
                AnnotationToolListBox.UpdateLayout();
                if (AnnotationToolListBox.SelectedItem != null)
                {
                    AnnotationToolListBox.ScrollIntoView(AnnotationToolListBox.SelectedItem);
                }

                AnnotationToolHeaderText.BringIntoView();
            }), DispatcherPriority.Background);
        }
    }
}