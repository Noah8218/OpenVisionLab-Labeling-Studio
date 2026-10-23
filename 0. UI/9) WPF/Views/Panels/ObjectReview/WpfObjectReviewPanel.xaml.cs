using System.Windows.Controls;
using System.Windows;
using Border = System.Windows.Controls.Border;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfObjectReviewPanel : UserControl
    {
        public static readonly DependencyProperty CandidateReviewViewModelProperty = DependencyProperty.Register(nameof(CandidateReviewViewModel), typeof(WpfCandidateReviewPanelViewModel), typeof(WpfObjectReviewPanel));

        public WpfObjectReviewPanel()
        {
            InitializeComponent();
            var classDisplay = (ObjectReviewClassDisplayConverter)Resources["ObjectReviewClassDisplayConverter"];
            System.Windows.Data.BindingOperations.SetBinding(classDisplay, ObjectReviewClassDisplayConverter.ClassesProperty, new System.Windows.Data.Binding("Tag.LabelClasses") { Source = this, Mode = System.Windows.Data.BindingMode.OneWay });
        }

        public WpfObjectReviewPanelViewModel ViewModel => DataContext as WpfObjectReviewPanelViewModel;

        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel
        {
            get => (WpfCandidateReviewPanelViewModel)GetValue(CandidateReviewViewModelProperty);
            set => SetValue(CandidateReviewViewModelProperty, value);
        }

        public TextBlock SummaryTextBlock => ObjectReviewSummaryText;
        public ListBox InlineCandidateList => InlineCandidateListBox;
        public Border LabelSaveBadge => ObjectReviewLabelSaveBadge;
        public TextBlock LabelSaveBadgeTextBlock => ObjectReviewLabelSaveBadgeText;
        public TextBlock LabelSaveDetailTextBlock => ObjectReviewLabelSaveDetailText;
        public WpfUiButton DeleteButton => DeleteObjectButton;
        public ComboBox ClassBox => ObjectClassBox;
        public WpfUiButton ApplyClassButton => ApplyObjectClassButton;
        public TextBlock MergeSelectionTextBlock => MergeSelectionText;
        public WpfUiButton MergeSegmentsButton => MergeSelectedSegmentsButton;
        public WpfUiButton VerticalSplitButton => BeginVerticalSplitButton;
        public WpfUiButton HorizontalSplitButton => BeginHorizontalSplitButton;
        public WpfUiButton SplitCancelButton => CancelSplitButton;
        public TextBlock SplitStatusTextBlock => SplitStatusText;
        public WpfUiButton AddHoleButton => BeginAddHoleButton;
        public WpfUiButton RemoveHoleButton => BeginRemoveHoleButton;
        public WpfUiButton HoleEditCancelButton => CancelHoleEditButton;
        public TextBlock HoleEditStatusTextBlock => HoleEditStatusText;
        public WpfUiButton InsertVertexButton => BeginInsertVertexButton;
        public WpfUiButton DeleteVertexButton => BeginDeleteVertexButton;
        public WpfUiButton VertexEditCancelButton => CancelVertexEditButton;
        public TextBlock VertexEditStatusTextBlock => VertexEditStatusText;
        public WpfUiButton IntelligentScissorsBeginButton => BeginIntelligentScissorsButton;
        public WpfUiButton IntelligentScissorsApplyButton => ApplyIntelligentScissorsButton;
        public WpfUiButton IntelligentScissorsCancelButton => CancelIntelligentScissorsButton;
        public TextBlock IntelligentScissorsStatusTextBlock => IntelligentScissorsStatusText;
        public WpfUiButton SegmentationToBackButton => SendSegmentationToBackButton;
        public WpfUiButton SegmentationBackwardButton => SendSegmentationBackwardButton;
        public WpfUiButton SegmentationForwardButton => BringSegmentationForwardButton;
        public WpfUiButton SegmentationToFrontButton => BringSegmentationToFrontButton;
        public TextBlock SegmentationZOrderStatusTextBlock => ZOrderStatusText;
        public WpfUiButton RemoveUnderlyingPreviewButton => PreviewRemoveUnderlyingButton;
        public WpfUiButton RemoveUnderlyingApplyButton => ApplyRemoveUnderlyingButton;
        public WpfUiButton RemoveUnderlyingCancelButton => CancelRemoveUnderlyingButton;
        public TextBlock RemoveUnderlyingStatusTextBlock => RemoveUnderlyingStatusText;
        public WpfUiButton ObjectHiddenButton => ToggleObjectHiddenButton;
        public WpfUiButton ObjectLockedButton => ToggleObjectLockedButton;
        public WpfUiButton ObjectPinnedButton => ToggleObjectPinnedButton;
        public TextBlock ObjectSessionStateStatusTextBlock => ObjectSessionStateStatusText;
        public Expander ObjectMetadataEditor => ObjectMetadataExpander;
        public WpfUiButton PersistentOccludedButton => TogglePersistentOccludedButton;
        public ComboBox MetadataTagBox => ObjectMetadataTagBox;
        public WpfUiButton PersistentTagButton => TogglePersistentTagButton;
        public WpfUiButton OccludedFilterButton => ToggleOccludedFilterButton;
        public ComboBox MetadataTagFilterBox => ObjectMetadataTagFilterBox;
        public WpfUiButton MetadataFilterResetButton => ResetObjectMetadataFilterButton;
        public WpfUiButton RecipeMetadataTagsResetButton => ResetRecipeMetadataTagsButton;
        public WpfUiButton GroupSelectionBeginButton => BeginObjectGroupSelectionButton;
        public WpfUiButton GroupCreateButton => CreateObjectGroupButton;
        public WpfUiButton GroupSelectionCancelButton => CancelObjectGroupSelectionButton;
        public TextBlock GroupSelectionStatusTextBlock => ObjectGroupSelectionStatusText;
        public TextBlock SelectedGroupTextBlock => SelectedObjectGroupText;
        public WpfUiButton GroupMemberRemoveButton => RemoveObjectFromGroupButton;
        public WpfUiButton GroupDissolveButton => DissolveObjectGroupButton;
        public WpfUiButton GroupOccludedButton => ToggleObjectGroupOccludedButton;
        public WpfUiButton GroupTagButton => ToggleObjectGroupTagButton;
        public ComboBox GroupFilterBox => ObjectGroupFilterBox;
        public Expander QualityReviewEditor => ObjectQualityReviewExpander;
        public Expander SegmentationAdvancedEditor => SegmentationAdvancedEditExpander;
        public ListBox ObjectList => ObjectListBox;
    }
}
