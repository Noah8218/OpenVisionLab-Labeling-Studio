using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfObjectReviewPanelViewModel : WpfObservableViewModel
    {
        public const string AllMetadataTagsFilter = ObjectReviewMetadataFilterPresentationService.AllMetadataTagsFilter;
        public const string AllGroupsFilter = ObjectReviewMetadataFilterPresentationService.AllGroupsFilter;
        public const string UngroupedFilter = ObjectReviewMetadataFilterPresentationService.UngroupedFilter;

        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private static readonly Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> EmptyObjectReviewSnapshots =
            () => Array.Empty<WpfObjectReviewObjectSnapshot>();
        private static readonly Func<WpfObjectReviewMutationResult, bool> RejectObjectReviewMutation =
            _ => false;
        private readonly List<string> recipeMetadataTags = new List<string>();
        private string summaryText = "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uAC1D\uCCB4 \uC5C6\uC74C";
        private string selectedObjectTaskTitleText = "\uC120\uD0DD \uB77C\uBCA8 \uC5C6\uC74C";
        private string selectedObjectTaskDetailText = "\uCE94\uBC84\uC2A4\uB098 \uBAA9\uB85D\uC5D0\uC11C \uC218\uC815\uD560 \uB77C\uBCA8\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
        private string selectedObjectTaskActionText = "\uB77C\uBCA8\uC744 \uADF8\uB9B0 \uD6C4\uC5D0\uB294 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB20C\uB7EC \uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uC138\uC694.";
        private string labelSaveStateKey = "Waiting";
        private string labelSaveBadgeText = "\uB77C\uBCA8 \uB300\uAE30";
        private string labelSaveDetailText = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private WpfObjectReviewListItem selectedObject;
        private string selectedClassName = string.Empty;
        private bool isDeleteEnabled;
        private bool isApplyClassEnabled;
        private bool isMergeSelectedSegmentsEnabled;
        private string mergeSelectionText = "\uBCD1\uD569 \uC120\uD0DD 0\uAC1C \u00B7 \uAC19\uC740 \uD074\uB798\uC2A4 2\uAC1C \uC774\uC0C1";
        private bool isSplitEnabled;
        private bool isSplitPending;
        private string splitStatusText = "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uB97C \uC138\uB85C \uB610\uB294 \uAC00\uB85C\uB85C \uC808\uB2E8\uD569\uB2C8\uB2E4.";
        private bool isHoleEditEnabled;
        private bool isHoleEditPending;
        private string holeEditStatusText = "\uB0B4\uBD80 \uAD6C\uBA4D\uC744 \uB2E4\uAC01\uD615\uC73C\uB85C \uCD94\uAC00\uD558\uAC70\uB098 \uD074\uB9AD\uD574 \uCC44\uC6C1\uB2C8\uB2E4.";
        private bool isPolygonVertexContextVisible;
        private bool isVertexEditEnabled;
        private bool isVertexEditPending;
        private string vertexEditStatusText = "\uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC\uC5D0 \uC815\uC810\uC744 \uCD94\uAC00\uD558\uAC70\uB098 \uAE30\uC874 \uC815\uC810\uC744 \uC0AD\uC81C\uD569\uB2C8\uB2E4.";
        private bool isIntelligentScissorsEnabled;
        private bool isIntelligentScissorsPending;
        private bool hasIntelligentScissorsPreview;
        private string intelligentScissorsStatusText = "\uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC\uB97C \uC120\uD0DD\uD574 \uC774\uBBF8\uC9C0 \uACBD\uACC4 \uACBD\uB85C\uB97C \uBBF8\uB9AC\uBCF4\uAE30\uD569\uB2C8\uB2E4.";
        private bool isSendToBackEnabled;
        private bool isSendBackwardEnabled;
        private bool isBringForwardEnabled;
        private bool isBringToFrontEnabled;
        private string zOrderStatusText = "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uC758 \uC55E\uB4A4 \uD45C\uC2DC \uC21C\uC11C\uB97C \uBCC0\uACBD\uD569\uB2C8\uB2E4.";
        private bool isRemoveUnderlyingPreviewEnabled;
        private bool isRemoveUnderlyingPreviewPending;
        private string removeUnderlyingStatusText = "\uC704\uCABD \uAC1D\uCCB4\uC640 \uACB9\uCE58\uB294 \uB4A4\uCABD geometry\uB97C \uBD84\uC11D\uD55C \uD6C4 \uD655\uC778\uD574 \uC81C\uAC70\uD569\uB2C8\uB2E4.";
        private bool isObjectSessionStateEnabled;
        private bool isSelectedObjectHidden;
        private bool isSelectedObjectLocked;
        private bool isSelectedObjectPinned;
        private string objectSessionStateStatusText = "\uC228\uAE40/\uC7A0\uAE08/\uC774\uB3D9 \uACE0\uC815\uC740 \uD604\uC7AC \uC774\uBBF8\uC9C0 \uC138\uC158\uC5D0\uB9CC \uC801\uC6A9\uB429\uB2C8\uB2E4.";
        private bool isPersistentMetadataEnabled;
        private bool isSelectedObjectOccluded;
        private string selectedObjectTagsText = "\uD0DC\uADF8 \uC5C6\uC74C";
        private string selectedMetadataTag = string.Empty;
        private string selectedMetadataTagFilter = AllMetadataTagsFilter;
        private bool isOccludedFilterActive;
        private bool isRefreshingMetadataTagCatalog;
        private string metadataFilterSummaryText = "\uBA54\uD0C0\uB370\uC774\uD130 \uD544\uD130 \uC5C6\uC74C";
        private int metadataFilterEnabledCount;
        private int metadataFilterVisibleCount;
        private string selectedGroupFilter = AllGroupsFilter;
        private bool isRefreshingGroupCatalog;
        private string selectedObjectGroupText = "\uADF8\uB8F9 \uC5C6\uC74C";
        private bool isGroupSelectionMode;
        private int groupSelectionCount;
        private string groupSelectionStatusText = "\uADF8\uB8F9 \uAD6C\uC131\uC744 \uC2DC\uC791\uD558\uBA74 \uC800\uC7A5 \uAC1D\uCCB4\uB97C 2\uAC1C \uC774\uC0C1 \uC120\uD0DD\uD569\uB2C8\uB2E4.";
        private bool isCreateGroupEnabled;
        private bool isSelectedObjectGrouped;
        private bool isSegmentContextVisible;
        private bool isSegmentAdvancedEditorOpen;
        private string qualityReviewStatusText = "이미지 없음";
        private string qualityReviewDetailText = "이미지를 열면 품질 검수 상태를 표시합니다.";
        private string qualityReviewNoteText = string.Empty;
        private bool isQualityReviewEnabled;
        private bool isMarkQualityReviewedEnabled;
        private bool isQualityUnreviewedActive;
        private bool isQualityNeedsFixActive;
        private bool isQualityReviewedActive;
        private int selectionNotificationSuppressDepth;
        private readonly ObjectReviewActionAvailabilityService actionAvailabilityService =
            new ObjectReviewActionAvailabilityService();
        private readonly ObjectReviewGroupSelectionPresentationService groupSelectionPresentationService =
            new ObjectReviewGroupSelectionPresentationService();
        private readonly ObjectReviewMetadataFilterPresentationService metadataFilterPresentationService =
            new ObjectReviewMetadataFilterPresentationService();
        private readonly ObjectReviewSelectedObjectPresentationService selectedObjectPresentationService =
            new ObjectReviewSelectedObjectPresentationService();
        private ObjectReviewWorkflowService groupSelectionWorkflowService;
        private Func<bool> deleteSelectedObject = () => false;
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> capturePersistentOccludedSnapshots = EmptyObjectReviewSnapshots;
        private Func<WpfObjectReviewMutationResult, bool> applyPersistentOccludedMutation = RejectObjectReviewMutation;
        private Action<WpfObjectReviewMutationResult> reportPersistentOccludedError = _ => { };
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> capturePersistentTagSnapshots = EmptyObjectReviewSnapshots;
        private Func<LabelingProjectData> capturePersistentTagData = () => null;
        private Func<string> capturePersistentTagRecipeName = () => string.Empty;
        private Func<WpfObjectReviewMutationResult, bool> applyPersistentTagMutation = RejectObjectReviewMutation;
        private Action<WpfObjectReviewMutationResult> reportPersistentTagError = _ => { };
        private Func<LabelingProjectData> captureRecipeMetadataResetData = () => null;
        private Func<string> captureRecipeMetadataResetRecipeName = () => string.Empty;
        private Func<WpfObjectReviewMutationResult, bool> applyRecipeMetadataResetMutation = RejectObjectReviewMutation;
        private Action<WpfObjectReviewMutationResult> reportRecipeMetadataResetError = _ => { };
        private Func<WpfObjectReviewItemRef, WpfObjectSessionStateKind, WpfObjectSessionState> toggleObjectSessionState = (_, __) => null;
        private Action<string> reportObjectSessionStateError = _ => { };
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> captureGroupCreationSnapshots = EmptyObjectReviewSnapshots;
        private Func<WpfObjectReviewMutationResult, bool> applyGroupCreationMutation = RejectObjectReviewMutation;
        private Action<string> reportGroupCreationError = _ => { };
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> captureGroupOccludedSnapshots = EmptyObjectReviewSnapshots;
        private Func<WpfObjectReviewMutationResult, bool> applyGroupOccludedMutation = RejectObjectReviewMutation;
        private Action<string> reportGroupOccludedError = _ => { };
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> captureGroupMemberRemovalSnapshots = EmptyObjectReviewSnapshots;
        private Func<WpfObjectReviewMutationResult, bool> applyGroupMemberRemovalMutation = RejectObjectReviewMutation;
        private Action<string> reportGroupMemberRemovalError = _ => { };
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> captureGroupTagSnapshots = EmptyObjectReviewSnapshots;
        private Func<LabelingProjectData> captureGroupTagData = () => null;
        private Func<string> captureGroupTagRecipeName = () => string.Empty;
        private Func<WpfObjectReviewMutationResult, bool> applyGroupTagMutation = RejectObjectReviewMutation;
        private Action<string> reportGroupTagError = _ => { };
        private Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> captureGroupDissolveSnapshots = EmptyObjectReviewSnapshots;
        private Func<int, bool> confirmGroupDissolve = _ => false;
        private Func<WpfObjectReviewMutationResult, bool> applyGroupDissolveMutation = RejectObjectReviewMutation;
        private Action<string> reportGroupDissolveError = _ => { };
        private ICommand deleteObjectCommand = new RelayCommand(NoOpCommand);
        private ICommand applyObjectClassCommand = new RelayCommand(NoOpCommand);
        private ICommand mergeSelectedSegmentsCommand = new RelayCommand(NoOpCommand);
        private ICommand mergeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand beginVerticalSplitCommand = new RelayCommand(NoOpCommand);
        private ICommand beginHorizontalSplitCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelSplitCommand = new RelayCommand(NoOpCommand);
        private ICommand beginAddHoleCommand = new RelayCommand(NoOpCommand);
        private ICommand beginRemoveHoleCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelHoleEditCommand = new RelayCommand(NoOpCommand);
        private ICommand beginInsertVertexCommand = new RelayCommand(NoOpCommand);
        private ICommand beginDeleteVertexCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelVertexEditCommand = new RelayCommand(NoOpCommand);
        private ICommand beginIntelligentScissorsCommand = new RelayCommand(NoOpCommand);
        private ICommand applyIntelligentScissorsCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelIntelligentScissorsCommand = new RelayCommand(NoOpCommand);
        private ICommand sendToBackCommand = new RelayCommand(NoOpCommand);
        private ICommand sendBackwardCommand = new RelayCommand(NoOpCommand);
        private ICommand bringForwardCommand = new RelayCommand(NoOpCommand);
        private ICommand bringToFrontCommand = new RelayCommand(NoOpCommand);
        private ICommand previewRemoveUnderlyingCommand = new RelayCommand(NoOpCommand);
        private ICommand applyRemoveUnderlyingCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelRemoveUnderlyingCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleObjectHiddenCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleObjectLockedCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleObjectPinnedCommand = new RelayCommand(NoOpCommand);
        private ICommand togglePersistentOccludedCommand = new RelayCommand(NoOpCommand);
        private ICommand togglePersistentTagCommand = new RelayCommand(NoOpCommand);
        private ICommand resetRecipeMetadataTagsCommand = new RelayCommand(NoOpCommand);
        private ICommand beginGroupSelectionCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelGroupSelectionCommand = new RelayCommand(NoOpCommand);
        private ICommand createGroupCommand = new RelayCommand(NoOpCommand);
        private ICommand groupSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand removeSelectedFromGroupCommand = new RelayCommand(NoOpCommand);
        private ICommand dissolveSelectedGroupCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleGroupOccludedCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleGroupTagCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleOccludedFilterCommand;
        private ICommand resetMetadataFilterCommand;
        private ICommand objectSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand objectPreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand markQualityUnreviewedCommand = new RelayCommand(NoOpCommand);
        private ICommand markQualityNeedsFixCommand = new RelayCommand(NoOpCommand);
        private ICommand markQualityReviewedCommand = new RelayCommand(NoOpCommand);
        private ICommand exportQualityReviewReportCommand = new RelayCommand(NoOpCommand);

        public event Action<string> WorkflowStatusChanged;

        public WpfObjectReviewPanelViewModel()
        {
            MetadataTagFilters.Add(AllMetadataTagsFilter);
            GroupFilters.Add(AllGroupsFilter);
            GroupFilters.Add(UngroupedFilter);
            toggleOccludedFilterCommand = new RelayCommand(() =>
            {
                IsOccludedFilterActive = !IsOccludedFilterActive;
            });
            resetMetadataFilterCommand = new RelayCommand(() =>
            {
                IsOccludedFilterActive = false;
                SelectedMetadataTagFilter = AllMetadataTagsFilter;
                SelectedGroupFilter = AllGroupsFilter;
            });
        }

        public string ViewName => nameof(WpfObjectReviewPanel);

        public string PanelModeTitleText => "\uC800\uC7A5 \uB77C\uBCA8";

        public string PanelModeBadgeText => "\uC800\uC7A5 \uB77C\uBCA8\uB9CC";

        public string PanelModeScopeText => "\uBBF8\uD655\uC815 AI \uD6C4\uBCF4 \uD45C\uC2DC \uC548 \uD568";

        public string PanelModeDetailText => "\uC774 \uD328\uB110\uC740 \uD30C\uC77C\uC5D0 \uBC18\uC601\uB420 \uC800\uC7A5 \uB77C\uBCA8\uB9CC \uD3B8\uC9D1\uD569\uB2C8\uB2E4. \uBBF8\uD655\uC815 AI \uD6C4\uBCF4\uB294 \uD45C\uC2DC\uD558\uC9C0 \uC54A\uC73C\uBA70, AI \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uBA74 \uC800\uC7A5 \uB77C\uBCA8\uB85C \uC804\uD658\uB418\uC5B4 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";

        public string ActionGuideText => "\uC0AD\uC81C/\uD074\uB798\uC2A4 \uBCC0\uACBD\uC740 \uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0 \uBC14\uB85C \uBC18\uC601\uB418\uACE0 \uC800\uC7A5 \uD544\uC694 \uC0C1\uD0DC\uB85C \uBC14\uB00D\uB2C8\uB2E4. \uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uB824\uBA74 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB204\uB974\uC138\uC694.";

        public BulkObservableCollection<WpfObjectReviewListItem> Objects { get; } = new BulkObservableCollection<WpfObjectReviewListItem>();

        public ObservableCollection<string> ClassNames { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> MetadataTagOptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> MetadataTagFilters { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> GroupFilters { get; } = new ObservableCollection<string>();

        public ICommand DeleteObjectCommand
        {
            get => deleteObjectCommand;
            private set => SetProperty(ref deleteObjectCommand, value);
        }

        public ICommand ApplyObjectClassCommand
        {
            get => applyObjectClassCommand;
            private set => SetProperty(ref applyObjectClassCommand, value);
        }

        public ICommand MergeSelectedSegmentsCommand
        {
            get => mergeSelectedSegmentsCommand;
            private set => SetProperty(ref mergeSelectedSegmentsCommand, value);
        }

        public ICommand MergeSelectionChangedCommand
        {
            get => mergeSelectionChangedCommand;
            private set => SetProperty(ref mergeSelectionChangedCommand, value);
        }

        public ICommand BeginVerticalSplitCommand
        {
            get => beginVerticalSplitCommand;
            private set => SetProperty(ref beginVerticalSplitCommand, value);
        }

        public ICommand BeginHorizontalSplitCommand
        {
            get => beginHorizontalSplitCommand;
            private set => SetProperty(ref beginHorizontalSplitCommand, value);
        }

        public ICommand CancelSplitCommand
        {
            get => cancelSplitCommand;
            private set => SetProperty(ref cancelSplitCommand, value);
        }

        public ICommand BeginAddHoleCommand
        {
            get => beginAddHoleCommand;
            private set => SetProperty(ref beginAddHoleCommand, value);
        }

        public ICommand BeginRemoveHoleCommand
        {
            get => beginRemoveHoleCommand;
            private set => SetProperty(ref beginRemoveHoleCommand, value);
        }

        public ICommand CancelHoleEditCommand
        {
            get => cancelHoleEditCommand;
            private set => SetProperty(ref cancelHoleEditCommand, value);
        }

        public ICommand BeginInsertVertexCommand
        {
            get => beginInsertVertexCommand;
            private set => SetProperty(ref beginInsertVertexCommand, value);
        }

        public ICommand BeginDeleteVertexCommand
        {
            get => beginDeleteVertexCommand;
            private set => SetProperty(ref beginDeleteVertexCommand, value);
        }

        public ICommand CancelVertexEditCommand
        {
            get => cancelVertexEditCommand;
            private set => SetProperty(ref cancelVertexEditCommand, value);
        }

        public ICommand BeginIntelligentScissorsCommand
        {
            get => beginIntelligentScissorsCommand;
            private set => SetProperty(ref beginIntelligentScissorsCommand, value);
        }

        public ICommand ApplyIntelligentScissorsCommand
        {
            get => applyIntelligentScissorsCommand;
            private set => SetProperty(ref applyIntelligentScissorsCommand, value);
        }

        public ICommand CancelIntelligentScissorsCommand
        {
            get => cancelIntelligentScissorsCommand;
            private set => SetProperty(ref cancelIntelligentScissorsCommand, value);
        }

        public ICommand SendToBackCommand
        {
            get => sendToBackCommand;
            private set => SetProperty(ref sendToBackCommand, value);
        }

        public ICommand SendBackwardCommand
        {
            get => sendBackwardCommand;
            private set => SetProperty(ref sendBackwardCommand, value);
        }

        public ICommand BringForwardCommand
        {
            get => bringForwardCommand;
            private set => SetProperty(ref bringForwardCommand, value);
        }

        public ICommand BringToFrontCommand
        {
            get => bringToFrontCommand;
            private set => SetProperty(ref bringToFrontCommand, value);
        }

        public ICommand PreviewRemoveUnderlyingCommand
        {
            get => previewRemoveUnderlyingCommand;
            private set => SetProperty(ref previewRemoveUnderlyingCommand, value);
        }

        public ICommand ApplyRemoveUnderlyingCommand
        {
            get => applyRemoveUnderlyingCommand;
            private set => SetProperty(ref applyRemoveUnderlyingCommand, value);
        }

        public ICommand CancelRemoveUnderlyingCommand
        {
            get => cancelRemoveUnderlyingCommand;
            private set => SetProperty(ref cancelRemoveUnderlyingCommand, value);
        }

        public ICommand ToggleObjectHiddenCommand
        {
            get => toggleObjectHiddenCommand;
            private set => SetProperty(ref toggleObjectHiddenCommand, value);
        }

        public ICommand ToggleObjectLockedCommand
        {
            get => toggleObjectLockedCommand;
            private set => SetProperty(ref toggleObjectLockedCommand, value);
        }

        public ICommand ToggleObjectPinnedCommand
        {
            get => toggleObjectPinnedCommand;
            private set => SetProperty(ref toggleObjectPinnedCommand, value);
        }

        public ICommand TogglePersistentOccludedCommand
        {
            get => togglePersistentOccludedCommand;
            private set => SetProperty(ref togglePersistentOccludedCommand, value);
        }

        public ICommand TogglePersistentTagCommand
        {
            get => togglePersistentTagCommand;
            private set => SetProperty(ref togglePersistentTagCommand, value);
        }

        public ICommand ResetRecipeMetadataTagsCommand
        {
            get => resetRecipeMetadataTagsCommand;
            private set => SetProperty(ref resetRecipeMetadataTagsCommand, value);
        }

        public ICommand BeginGroupSelectionCommand
        {
            get => beginGroupSelectionCommand;
            private set => SetProperty(ref beginGroupSelectionCommand, value);
        }

        public ICommand CancelGroupSelectionCommand
        {
            get => cancelGroupSelectionCommand;
            private set => SetProperty(ref cancelGroupSelectionCommand, value);
        }

        public ICommand CreateGroupCommand
        {
            get => createGroupCommand;
            private set => SetProperty(ref createGroupCommand, value);
        }

        public ICommand GroupSelectionChangedCommand
        {
            get => groupSelectionChangedCommand;
            private set => SetProperty(ref groupSelectionChangedCommand, value);
        }

        public ICommand RemoveSelectedFromGroupCommand
        {
            get => removeSelectedFromGroupCommand;
            private set => SetProperty(ref removeSelectedFromGroupCommand, value);
        }

        public ICommand DissolveSelectedGroupCommand
        {
            get => dissolveSelectedGroupCommand;
            private set => SetProperty(ref dissolveSelectedGroupCommand, value);
        }

        public ICommand ToggleGroupOccludedCommand
        {
            get => toggleGroupOccludedCommand;
            private set => SetProperty(ref toggleGroupOccludedCommand, value);
        }

        public ICommand ToggleGroupTagCommand
        {
            get => toggleGroupTagCommand;
            private set => SetProperty(ref toggleGroupTagCommand, value);
        }

        public ICommand ToggleOccludedFilterCommand => toggleOccludedFilterCommand;

        public ICommand ResetMetadataFilterCommand => resetMetadataFilterCommand;

        public ICommand ObjectSelectionChangedCommand
        {
            get => objectSelectionChangedCommand;
            private set => SetProperty(ref objectSelectionChangedCommand, value);
        }

        public ICommand ObjectPreviewKeyDownCommand
        {
            get => objectPreviewKeyDownCommand;
            private set => SetProperty(ref objectPreviewKeyDownCommand, value);
        }

        public ICommand MarkQualityUnreviewedCommand
        {
            get => markQualityUnreviewedCommand;
            private set => SetProperty(ref markQualityUnreviewedCommand, value);
        }

        public ICommand MarkQualityNeedsFixCommand
        {
            get => markQualityNeedsFixCommand;
            private set => SetProperty(ref markQualityNeedsFixCommand, value);
        }

        public ICommand MarkQualityReviewedCommand
        {
            get => markQualityReviewedCommand;
            private set => SetProperty(ref markQualityReviewedCommand, value);
        }

        public ICommand ExportQualityReviewReportCommand
        {
            get => exportQualityReviewReportCommand;
            private set => SetProperty(ref exportQualityReviewReportCommand, value);
        }

        public string SummaryText
        {
            get => summaryText;
            set
            {
                if (SetProperty(ref summaryText, value ?? string.Empty))
                {
                    ApplySelectedObjectTaskPresentation();
                }
            }
        }

        public string SelectedObjectTaskTitleText
        {
            get => selectedObjectTaskTitleText;
            private set => SetProperty(ref selectedObjectTaskTitleText, value ?? string.Empty);
        }

        public string SelectedObjectTaskDetailText
        {
            get => selectedObjectTaskDetailText;
            private set => SetProperty(ref selectedObjectTaskDetailText, value ?? string.Empty);
        }

        public string SelectedObjectTaskActionText
        {
            get => selectedObjectTaskActionText;
            private set => SetProperty(ref selectedObjectTaskActionText, value ?? string.Empty);
        }

        public string LabelSaveStateKey
        {
            get => labelSaveStateKey;
            private set => SetProperty(ref labelSaveStateKey, value ?? string.Empty);
        }

        public string LabelSaveBadgeText
        {
            get => labelSaveBadgeText;
            private set => SetProperty(ref labelSaveBadgeText, value ?? string.Empty);
        }

        public string LabelSaveDetailText
        {
            get => labelSaveDetailText;
            private set => SetProperty(ref labelSaveDetailText, value ?? string.Empty);
        }

        public WpfObjectReviewListItem SelectedObject
        {
            get => selectedObject;
            set => SetSelectedObject(value, refreshSegmentCollectionState: true);
        }

        public string SelectedClassName
        {
            get => selectedClassName;
            set
            {
                if (SetProperty(ref selectedClassName, value ?? string.Empty))
                {
                    RefreshActionState();
                }
            }
        }

        public bool IsDeleteEnabled
        {
            get => isDeleteEnabled;
            private set => SetProperty(ref isDeleteEnabled, value);
        }

        public bool IsApplyClassEnabled
        {
            get => isApplyClassEnabled;
            private set => SetProperty(ref isApplyClassEnabled, value);
        }

        public bool IsMergeSelectedSegmentsEnabled
        {
            get => isMergeSelectedSegmentsEnabled;
            private set => SetProperty(ref isMergeSelectedSegmentsEnabled, value);
        }

        public string MergeSelectionText
        {
            get => mergeSelectionText;
            private set => SetProperty(ref mergeSelectionText, value ?? string.Empty);
        }

        public bool IsSplitEnabled
        {
            get => isSplitEnabled;
            private set => SetProperty(ref isSplitEnabled, value);
        }

        public bool IsSplitPending
        {
            get => isSplitPending;
            private set => SetProperty(ref isSplitPending, value);
        }

        public string SplitStatusText
        {
            get => splitStatusText;
            private set => SetProperty(ref splitStatusText, value ?? string.Empty);
        }

        public bool IsHoleEditEnabled
        {
            get => isHoleEditEnabled;
            private set => SetProperty(ref isHoleEditEnabled, value);
        }

        public bool IsHoleEditPending
        {
            get => isHoleEditPending;
            private set => SetProperty(ref isHoleEditPending, value);
        }

        public string HoleEditStatusText
        {
            get => holeEditStatusText;
            private set => SetProperty(ref holeEditStatusText, value ?? string.Empty);
        }

        public bool IsPolygonVertexContextVisible
        {
            get => isPolygonVertexContextVisible;
            private set => SetProperty(ref isPolygonVertexContextVisible, value);
        }

        public bool IsVertexEditEnabled
        {
            get => isVertexEditEnabled;
            private set => SetProperty(ref isVertexEditEnabled, value);
        }

        public bool IsVertexEditPending
        {
            get => isVertexEditPending;
            private set => SetProperty(ref isVertexEditPending, value);
        }

        public string VertexEditStatusText
        {
            get => vertexEditStatusText;
            private set => SetProperty(ref vertexEditStatusText, value ?? string.Empty);
        }

        public bool IsIntelligentScissorsEnabled
        {
            get => isIntelligentScissorsEnabled;
            private set => SetProperty(ref isIntelligentScissorsEnabled, value);
        }

        public bool IsIntelligentScissorsPending
        {
            get => isIntelligentScissorsPending;
            private set => SetProperty(ref isIntelligentScissorsPending, value);
        }

        public bool HasIntelligentScissorsPreview
        {
            get => hasIntelligentScissorsPreview;
            private set => SetProperty(ref hasIntelligentScissorsPreview, value);
        }

        public string IntelligentScissorsStatusText
        {
            get => intelligentScissorsStatusText;
            private set => SetProperty(ref intelligentScissorsStatusText, value ?? string.Empty);
        }

        public bool IsSendToBackEnabled
        {
            get => isSendToBackEnabled;
            private set => SetProperty(ref isSendToBackEnabled, value);
        }

        public bool IsSendBackwardEnabled
        {
            get => isSendBackwardEnabled;
            private set => SetProperty(ref isSendBackwardEnabled, value);
        }

        public bool IsBringForwardEnabled
        {
            get => isBringForwardEnabled;
            private set => SetProperty(ref isBringForwardEnabled, value);
        }

        public bool IsBringToFrontEnabled
        {
            get => isBringToFrontEnabled;
            private set => SetProperty(ref isBringToFrontEnabled, value);
        }

        public string ZOrderStatusText
        {
            get => zOrderStatusText;
            private set => SetProperty(ref zOrderStatusText, value ?? string.Empty);
        }

        public bool IsRemoveUnderlyingPreviewEnabled
        {
            get => isRemoveUnderlyingPreviewEnabled;
            private set => SetProperty(ref isRemoveUnderlyingPreviewEnabled, value);
        }

        public bool IsRemoveUnderlyingPreviewPending
        {
            get => isRemoveUnderlyingPreviewPending;
            private set => SetProperty(ref isRemoveUnderlyingPreviewPending, value);
        }

        public string RemoveUnderlyingStatusText
        {
            get => removeUnderlyingStatusText;
            private set => SetProperty(ref removeUnderlyingStatusText, value ?? string.Empty);
        }

        public bool IsObjectSessionStateEnabled
        {
            get => isObjectSessionStateEnabled;
            private set => SetProperty(ref isObjectSessionStateEnabled, value);
        }

        public bool IsSelectedObjectHidden
        {
            get => isSelectedObjectHidden;
            private set => SetProperty(ref isSelectedObjectHidden, value);
        }

        public bool IsSelectedObjectLocked
        {
            get => isSelectedObjectLocked;
            private set => SetProperty(ref isSelectedObjectLocked, value);
        }

        public bool IsSelectedObjectPinned
        {
            get => isSelectedObjectPinned;
            private set => SetProperty(ref isSelectedObjectPinned, value);
        }

        public string ObjectSessionStateStatusText
        {
            get => objectSessionStateStatusText;
            private set => SetProperty(ref objectSessionStateStatusText, value ?? string.Empty);
        }

        public bool IsPersistentMetadataEnabled
        {
            get => isPersistentMetadataEnabled;
            private set
            {
                if (SetProperty(ref isPersistentMetadataEnabled, value))
                {
                    OnPropertyChanged(nameof(IsGroupStartVisible));
                }
            }
        }

        public bool IsSelectedObjectOccluded
        {
            get => isSelectedObjectOccluded;
            private set => SetProperty(ref isSelectedObjectOccluded, value);
        }

        public string SelectedObjectTagsText
        {
            get => selectedObjectTagsText;
            private set => SetProperty(ref selectedObjectTagsText, value ?? string.Empty);
        }

        public string SelectedMetadataTag
        {
            get => selectedMetadataTag;
            set => SetProperty(
                ref selectedMetadataTag,
                ObjectMetadataStateService.NormalizeTag(value));
        }

        public string SelectedMetadataTagFilter
        {
            get => selectedMetadataTagFilter;
            set
            {
                if (isRefreshingMetadataTagCatalog && string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string normalized = string.IsNullOrWhiteSpace(value)
                    ? AllMetadataTagsFilter
                    : value.Trim();
                if (SetProperty(ref selectedMetadataTagFilter, normalized))
                {
                    RefreshMetadataFilter();
                }
            }
        }

        public bool IsOccludedFilterActive
        {
            get => isOccludedFilterActive;
            private set
            {
                if (SetProperty(ref isOccludedFilterActive, value))
                {
                    RefreshMetadataFilter();
                }
            }
        }

        public string MetadataFilterSummaryText
        {
            get => metadataFilterSummaryText;
            private set => SetProperty(ref metadataFilterSummaryText, value ?? string.Empty);
        }

        public string SelectedGroupFilter
        {
            get => selectedGroupFilter;
            set
            {
                if (isRefreshingGroupCatalog && string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string normalized = string.IsNullOrWhiteSpace(value)
                    ? AllGroupsFilter
                    : value.Trim();
                if (SetProperty(ref selectedGroupFilter, normalized))
                {
                    RefreshMetadataFilter();
                }
            }
        }

        public string SelectedObjectGroupText
        {
            get => selectedObjectGroupText;
            private set => SetProperty(ref selectedObjectGroupText, value ?? string.Empty);
        }

        public bool IsGroupSelectionMode
        {
            get => isGroupSelectionMode;
            private set
            {
                if (SetProperty(ref isGroupSelectionMode, value))
                {
                    OnPropertyChanged(nameof(IsGroupStartVisible));
                    OnPropertyChanged(nameof(IsGroupSelectionActionsVisible));
                    OnPropertyChanged(nameof(IsSelectedGroupActionsVisible));
                }
            }
        }

        public int GroupSelectionCount
        {
            get => groupSelectionCount;
            private set => SetProperty(ref groupSelectionCount, Math.Max(0, value));
        }

        public string GroupSelectionStatusText
        {
            get => groupSelectionStatusText;
            private set => SetProperty(ref groupSelectionStatusText, value ?? string.Empty);
        }

        public bool IsCreateGroupEnabled
        {
            get => isCreateGroupEnabled;
            private set => SetProperty(ref isCreateGroupEnabled, value);
        }

        public bool IsSelectedObjectGrouped
        {
            get => isSelectedObjectGrouped;
            private set
            {
                if (SetProperty(ref isSelectedObjectGrouped, value))
                {
                    OnPropertyChanged(nameof(IsGroupStartVisible));
                    OnPropertyChanged(nameof(IsSelectedGroupActionsVisible));
                }
            }
        }

        public bool IsGroupStartVisible
            => IsPersistentMetadataEnabled
                && !IsGroupSelectionMode
                && !IsSelectedObjectGrouped;

        public bool IsGroupSelectionActionsVisible => IsGroupSelectionMode;

        public bool IsSelectedGroupActionsVisible
            => !IsGroupSelectionMode && IsSelectedObjectGrouped;

        public bool IsSegmentContextVisible
        {
            get => isSegmentContextVisible;
            private set => SetProperty(ref isSegmentContextVisible, value);
        }

        public bool IsSegmentAdvancedEditorOpen
        {
            get => isSegmentAdvancedEditorOpen;
            set => SetProperty(ref isSegmentAdvancedEditorOpen, value);
        }

        public string QualityReviewStatusText
        {
            get => qualityReviewStatusText;
            private set => SetProperty(ref qualityReviewStatusText, value ?? string.Empty);
        }

        public string QualityReviewDetailText
        {
            get => qualityReviewDetailText;
            private set => SetProperty(ref qualityReviewDetailText, value ?? string.Empty);
        }

        public string QualityReviewNoteText
        {
            get => qualityReviewNoteText;
            set
            {
                string note = value ?? string.Empty;
                if (note.Length > YoloImageReviewStatusService.QualityReviewNoteMaxLength)
                {
                    note = note.Substring(0, YoloImageReviewStatusService.QualityReviewNoteMaxLength);
                }

                SetProperty(ref qualityReviewNoteText, note);
            }
        }

        public bool IsQualityReviewEnabled
        {
            get => isQualityReviewEnabled;
            private set => SetProperty(ref isQualityReviewEnabled, value);
        }

        public bool IsMarkQualityReviewedEnabled
        {
            get => isMarkQualityReviewedEnabled;
            private set => SetProperty(ref isMarkQualityReviewedEnabled, value);
        }

        public bool IsQualityUnreviewedActive
        {
            get => isQualityUnreviewedActive;
            private set => SetProperty(ref isQualityUnreviewedActive, value);
        }

        public bool IsQualityNeedsFixActive
        {
            get => isQualityNeedsFixActive;
            private set => SetProperty(ref isQualityNeedsFixActive, value);
        }

        public bool IsQualityReviewedActive
        {
            get => isQualityReviewedActive;
            private set => SetProperty(ref isQualityReviewedActive, value);
        }

        public bool IsSelectionNotificationSuppressed => selectionNotificationSuppressDepth > 0;

        public void ConfigureCommands(
            Action deleteObject,
            Action applyObjectClass,
            Action markQualityUnreviewed,
            Action markQualityNeedsFix,
            Action markQualityReviewed,
            Action exportQualityReviewReport,
            Action<object> objectSelectionChanged,
            Action<KeyInputCommandArgs> objectPreviewKeyDown,
            Action mergeSelectedSegments = null,
            Action<object> mergeSelectionChanged = null,
            Action beginVerticalSplit = null,
            Action beginHorizontalSplit = null,
            Action cancelSplit = null,
            Action beginAddHole = null,
            Action beginRemoveHole = null,
            Action cancelHoleEdit = null,
            Action beginInsertVertex = null,
            Action beginDeleteVertex = null,
            Action cancelVertexEdit = null,
            Action beginIntelligentScissors = null,
            Action applyIntelligentScissors = null,
            Action cancelIntelligentScissors = null,
            Action sendToBack = null,
            Action sendBackward = null,
            Action bringForward = null,
            Action bringToFront = null,
            Action previewRemoveUnderlying = null,
            Action applyRemoveUnderlying = null,
            Action cancelRemoveUnderlying = null,
            Action toggleObjectHidden = null,
            Action toggleObjectLocked = null,
            Action toggleObjectPinned = null,
            Action togglePersistentOccluded = null,
            Action<string> togglePersistentTag = null,
            Action resetRecipeMetadataTags = null,
            Action beginGroupSelection = null,
            Action cancelGroupSelection = null,
            Action createGroup = null,
            Action<object> groupSelectionChanged = null,
            Action removeSelectedFromGroup = null,
            Action dissolveSelectedGroup = null,
            Action toggleGroupOccluded = null,
            Action<string> toggleGroupTag = null)
        {
            // The review panel exposes commands; the shell injects workflow actions without owning the view events.
            DeleteObjectCommand = new RelayCommand(deleteObject ?? NoOpCommand);
            ApplyObjectClassCommand = new RelayCommand(applyObjectClass ?? NoOpCommand);
            MarkQualityUnreviewedCommand = new RelayCommand(markQualityUnreviewed ?? NoOpCommand);
            MarkQualityNeedsFixCommand = new RelayCommand(markQualityNeedsFix ?? NoOpCommand);
            MarkQualityReviewedCommand = new RelayCommand(markQualityReviewed ?? NoOpCommand);
            ExportQualityReviewReportCommand = new RelayCommand(exportQualityReviewReport ?? NoOpCommand);
            ObjectSelectionChangedCommand = new RelayCommand<object>(objectSelectionChanged ?? NoOpSelectionCommand);
            ObjectPreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(objectPreviewKeyDown ?? NoOpKeyCommand);
            MergeSelectedSegmentsCommand = new RelayCommand(mergeSelectedSegments ?? NoOpCommand);
            MergeSelectionChangedCommand = new RelayCommand<object>(item =>
            {
                (mergeSelectionChanged ?? NoOpSelectionCommand)(item);
                RefreshActionState();
            });
            BeginVerticalSplitCommand = new RelayCommand(beginVerticalSplit ?? NoOpCommand);
            BeginHorizontalSplitCommand = new RelayCommand(beginHorizontalSplit ?? NoOpCommand);
            CancelSplitCommand = new RelayCommand(cancelSplit ?? NoOpCommand);
            BeginAddHoleCommand = new RelayCommand(beginAddHole ?? NoOpCommand);
            BeginRemoveHoleCommand = new RelayCommand(beginRemoveHole ?? NoOpCommand);
            CancelHoleEditCommand = new RelayCommand(cancelHoleEdit ?? NoOpCommand);
            BeginInsertVertexCommand = new RelayCommand(beginInsertVertex ?? NoOpCommand);
            BeginDeleteVertexCommand = new RelayCommand(beginDeleteVertex ?? NoOpCommand);
            CancelVertexEditCommand = new RelayCommand(cancelVertexEdit ?? NoOpCommand);
            BeginIntelligentScissorsCommand = new RelayCommand(beginIntelligentScissors ?? NoOpCommand);
            ApplyIntelligentScissorsCommand = new RelayCommand(applyIntelligentScissors ?? NoOpCommand);
            CancelIntelligentScissorsCommand = new RelayCommand(cancelIntelligentScissors ?? NoOpCommand);
            SendToBackCommand = new RelayCommand(sendToBack ?? NoOpCommand);
            SendBackwardCommand = new RelayCommand(sendBackward ?? NoOpCommand);
            BringForwardCommand = new RelayCommand(bringForward ?? NoOpCommand);
            BringToFrontCommand = new RelayCommand(bringToFront ?? NoOpCommand);
            PreviewRemoveUnderlyingCommand = new RelayCommand(previewRemoveUnderlying ?? NoOpCommand);
            ApplyRemoveUnderlyingCommand = new RelayCommand(applyRemoveUnderlying ?? NoOpCommand);
            CancelRemoveUnderlyingCommand = new RelayCommand(cancelRemoveUnderlying ?? NoOpCommand);
            ToggleObjectHiddenCommand = new RelayCommand(toggleObjectHidden ?? NoOpCommand);
            ToggleObjectLockedCommand = new RelayCommand(toggleObjectLocked ?? NoOpCommand);
            ToggleObjectPinnedCommand = new RelayCommand(toggleObjectPinned ?? NoOpCommand);
            TogglePersistentOccludedCommand = new RelayCommand(togglePersistentOccluded ?? NoOpCommand);
            TogglePersistentTagCommand = new RelayCommand(
                () => (togglePersistentTag ?? (_ => { }))(SelectedMetadataTag));
            ResetRecipeMetadataTagsCommand = new RelayCommand(resetRecipeMetadataTags ?? NoOpCommand);
            BeginGroupSelectionCommand = new RelayCommand(beginGroupSelection ?? NoOpCommand);
            CancelGroupSelectionCommand = new RelayCommand(cancelGroupSelection ?? NoOpCommand);
            CreateGroupCommand = new RelayCommand(createGroup ?? NoOpCommand);
            GroupSelectionChangedCommand = new RelayCommand<object>(item =>
            {
                (groupSelectionChanged ?? NoOpSelectionCommand)(item);
                RefreshGroupSelectionPresentation();
            });
            RemoveSelectedFromGroupCommand = new RelayCommand(removeSelectedFromGroup ?? NoOpCommand);
            DissolveSelectedGroupCommand = new RelayCommand(dissolveSelectedGroup ?? NoOpCommand);
            ToggleGroupOccludedCommand = new RelayCommand(toggleGroupOccluded ?? NoOpCommand);
            ToggleGroupTagCommand = new RelayCommand(
                () => (toggleGroupTag ?? (_ => { }))(SelectedMetadataTag));
        }

        public void ConfigureGroupSelectionWorkflow(ObjectReviewWorkflowService workflowService)
        {
            groupSelectionWorkflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            BeginGroupSelectionCommand = new RelayCommand(ExecuteBeginGroupSelection);
            CancelGroupSelectionCommand = new RelayCommand(ExecuteCancelGroupSelection);
            GroupSelectionChangedCommand = new RelayCommand<object>(ExecuteGroupSelectionChanged);
        }

        public void ConfigurePersistentOccludedWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<WpfObjectReviewMutationResult> reportError = null)
        {
            capturePersistentOccludedSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            applyPersistentOccludedMutation = applyMutation ?? RejectObjectReviewMutation;
            reportPersistentOccludedError = reportError ?? (_ => { });
            TogglePersistentOccludedCommand = new RelayCommand(ExecuteTogglePersistentOccluded);
        }

        public void ConfigurePersistentTagWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<LabelingProjectData> dataProvider,
            Func<string> recipeNameProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<WpfObjectReviewMutationResult> reportError = null)
        {
            capturePersistentTagSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            capturePersistentTagData = dataProvider ?? (() => null);
            capturePersistentTagRecipeName = recipeNameProvider ?? (() => string.Empty);
            applyPersistentTagMutation = applyMutation ?? RejectObjectReviewMutation;
            reportPersistentTagError = reportError ?? (_ => { });
            TogglePersistentTagCommand = new RelayCommand(ExecuteTogglePersistentTag);
        }

        public void ConfigureRecipeMetadataResetWorkflow(
            Func<LabelingProjectData> dataProvider,
            Func<string> recipeNameProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<WpfObjectReviewMutationResult> reportError = null)
        {
            captureRecipeMetadataResetData = dataProvider ?? (() => null);
            captureRecipeMetadataResetRecipeName = recipeNameProvider ?? (() => string.Empty);
            applyRecipeMetadataResetMutation = applyMutation ?? RejectObjectReviewMutation;
            reportRecipeMetadataResetError = reportError ?? (_ => { });
            ResetRecipeMetadataTagsCommand = new RelayCommand(ExecuteResetRecipeMetadataTags);
        }

        public void ConfigureObjectSessionStateWorkflow(
            Func<WpfObjectReviewItemRef, WpfObjectSessionStateKind, WpfObjectSessionState> toggleState,
            Action<string> reportError = null)
        {
            toggleObjectSessionState = toggleState ?? ((_, __) => null);
            reportObjectSessionStateError = reportError ?? (_ => { });
            ToggleObjectHiddenCommand = new RelayCommand(
                () => ExecuteToggleObjectSessionState(WpfObjectSessionStateKind.Hidden));
            ToggleObjectLockedCommand = new RelayCommand(
                () => ExecuteToggleObjectSessionState(WpfObjectSessionStateKind.Locked));
            ToggleObjectPinnedCommand = new RelayCommand(
                () => ExecuteToggleObjectSessionState(WpfObjectSessionStateKind.Pinned));
        }

        public void ConfigureObjectDeleteShortcutWorkflow(Func<bool> deleteAction)
        {
            deleteSelectedObject = deleteAction ?? (() => false);
            ObjectPreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(ExecuteObjectPreviewKeyDown);
        }

        public void ConfigureGroupCreationWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<string> reportError = null)
        {
            captureGroupCreationSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            applyGroupCreationMutation = applyMutation ?? RejectObjectReviewMutation;
            reportGroupCreationError = reportError ?? (_ => { });
            CreateGroupCommand = new RelayCommand(ExecuteCreateGroup);
        }

        public void ConfigureGroupOccludedWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<string> reportError = null)
        {
            captureGroupOccludedSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            applyGroupOccludedMutation = applyMutation ?? RejectObjectReviewMutation;
            reportGroupOccludedError = reportError ?? (_ => { });
            ToggleGroupOccludedCommand = new RelayCommand(ExecuteToggleGroupOccluded);
        }

        public void ConfigureGroupMemberRemovalWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<string> reportError = null)
        {
            captureGroupMemberRemovalSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            applyGroupMemberRemovalMutation = applyMutation ?? RejectObjectReviewMutation;
            reportGroupMemberRemovalError = reportError ?? (_ => { });
            RemoveSelectedFromGroupCommand = new RelayCommand(ExecuteRemoveSelectedFromGroup);
        }

        public void ConfigureGroupTagWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<LabelingProjectData> dataProvider,
            Func<string> recipeNameProvider,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<string> reportError = null)
        {
            captureGroupTagSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            captureGroupTagData = dataProvider ?? (() => null);
            captureGroupTagRecipeName = recipeNameProvider ?? (() => string.Empty);
            applyGroupTagMutation = applyMutation ?? RejectObjectReviewMutation;
            reportGroupTagError = reportError ?? (_ => { });
            ToggleGroupTagCommand = new RelayCommand(ExecuteToggleGroupTag);
        }

        public void ConfigureGroupDissolveWorkflow(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Func<int, bool> confirmMutation,
            Func<WpfObjectReviewMutationResult, bool> applyMutation,
            Action<string> reportError = null)
        {
            captureGroupDissolveSnapshots = snapshotProvider ?? EmptyObjectReviewSnapshots;
            confirmGroupDissolve = confirmMutation ?? (_ => false);
            applyGroupDissolveMutation = applyMutation ?? RejectObjectReviewMutation;
            reportGroupDissolveError = reportError ?? (_ => { });
            DissolveSelectedGroupCommand = new RelayCommand(ExecuteDissolveSelectedGroup);
        }

        private void ExecuteTogglePersistentOccluded()
        {
            if (groupSelectionWorkflowService == null
                || !TryResolveSelectedSnapshot(
                    capturePersistentOccludedSnapshots,
                    out WpfObjectReviewObjectSnapshot snapshot))
            {
                ReportPersistentOccludedError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            WpfObjectReviewMutationResult workflowResult =
                groupSelectionWorkflowService.ToggleOccluded(snapshot);
            if (!workflowResult.IsApplicable)
            {
                ReportPersistentOccludedError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            if (!applyPersistentOccludedMutation(workflowResult))
            {
                ReportPersistentOccludedError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            string status = workflowResult.Metadata.IsOccluded
                ? "\uAC00\uB9BC \uAC1D\uCCB4\uB85C \uD45C\uC2DC\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uC2DC \uBA54\uD0C0\uB370\uC774\uD130\uC5D0 \uBC18\uC601\uB429\uB2C8\uB2E4."
                : "\uAC00\uB9BC \uD45C\uC2DC\uB97C \uD574\uC81C\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uC2DC \uBC18\uC601\uB429\uB2C8\uB2E4.";
            WorkflowStatusChanged?.Invoke(status);
        }

        private void ExecuteTogglePersistentTag()
        {
            string requestedTag = SelectedMetadataTag;
            if (groupSelectionWorkflowService == null
                || string.IsNullOrWhiteSpace(requestedTag?.Trim())
                || !TryResolveSelectedSnapshot(
                    capturePersistentTagSnapshots,
                    out WpfObjectReviewObjectSnapshot snapshot))
            {
                ReportPersistentTagError("\uC218\uB3D9 \uBC15\uC2A4/\uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uACE0 \uD0DC\uADF8\uB97C \uC785\uB825\uD558\uC138\uC694.");
                return;
            }

            WpfObjectReviewMutationResult workflowResult = groupSelectionWorkflowService?.ToggleTag(
                snapshot,
                requestedTag,
                capturePersistentTagData(),
                capturePersistentTagRecipeName());
            if (workflowResult == null)
            {
                ReportPersistentTagError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uACE0 \uD0DC\uADF8\uB97C \uC785\uB825\uD558\uC138\uC694.");
                return;
            }

            if (!workflowResult.IsApplicable)
            {
                if (!string.IsNullOrWhiteSpace(workflowResult.ErrorMessage))
                {
                    reportPersistentTagError(workflowResult);
                }
                else
                {
                    ReportPersistentTagError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uC5D0\uB9CC \uD0DC\uADF8\uB97C \uC801\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.");
                }

                return;
            }

            if (!applyPersistentTagMutation(workflowResult))
            {
                ReportPersistentTagError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uC5D0\uB9CC \uD0DC\uADF8\uB97C \uC801\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.");
                return;
            }

            string tag = workflowResult.Tag;
            bool isApplied = workflowResult.Metadata.Tags.Any(value =>
                string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
            WorkflowStatusChanged?.Invoke(
                isApplied
                    ? $"\uD0DC\uADF8 \uC801\uC6A9: {tag} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694"
                    : $"\uD0DC\uADF8 \uD574\uC81C: {tag} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694");
        }

        private void ExecuteResetRecipeMetadataTags()
        {
            if (groupSelectionWorkflowService == null)
            {
                ReportRecipeMetadataResetError("Recipe \uD0DC\uADF8 \uBAA9\uB85D\uC744 \uCD08\uAE30\uD654\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.");
                return;
            }

            WpfObjectReviewMutationResult workflowResult =
                groupSelectionWorkflowService.ResetRecipeMetadataTags(
                    captureRecipeMetadataResetData(),
                    captureRecipeMetadataResetRecipeName());
            if (!workflowResult.IsApplicable)
            {
                reportRecipeMetadataResetError(workflowResult);
                return;
            }

            if (!applyRecipeMetadataResetMutation(workflowResult))
            {
                ReportRecipeMetadataResetError("Recipe \uD0DC\uADF8 \uBAA9\uB85D \uBCC0\uACBD\uC744 \uD654\uBA74\uC5D0 \uBC18\uC601\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
                return;
            }

            WorkflowStatusChanged?.Invoke(
                "Recipe \uD0DC\uADF8 \uBAA9\uB85D\uC744 \uAE30\uBCF8\uAC12(\uBE48 \uBAA9\uB85D)\uC73C\uB85C \uB418\uB3CC\uB838\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uAC1D\uCCB4\uC5D0 \uC800\uC7A5\uB41C \uD0DC\uADF8\uB294 \uC0AD\uC81C\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.");
        }

        private bool TryResolveSelectedSnapshot(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            out WpfObjectReviewObjectSnapshot snapshot)
        {
            WpfObjectReviewItemRef selected = SelectedObject?.Payload as WpfObjectReviewItemRef;
            snapshot = (snapshotProvider?.Invoke() ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
                .FirstOrDefault(candidate =>
                    candidate?.Item != null
                    && selected != null
                    && candidate.Item.Source == selected.Source
                    && candidate.Item.Index == selected.Index);
            return snapshot != null;
        }

        private void ReportPersistentOccludedError(string error)
        {
            reportPersistentOccludedError(
                WpfObjectReviewMutationResult.Failure(string.Empty, error));
        }

        private void ReportPersistentTagError(string error)
        {
            reportPersistentTagError(
                WpfObjectReviewMutationResult.Failure(string.Empty, error));
        }

        private void ReportRecipeMetadataResetError(string error)
        {
            reportRecipeMetadataResetError(
                WpfObjectReviewMutationResult.Failure(string.Empty, error));
        }

        private void ExecuteToggleObjectSessionState(WpfObjectSessionStateKind kind)
        {
            WpfObjectReviewItemRef item = SelectedObject?.Payload as WpfObjectReviewItemRef;
            if (item == null)
            {
                reportObjectSessionStateError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            WpfObjectSessionState state = toggleObjectSessionState(item, kind);
            if (state == null)
            {
                reportObjectSessionStateError("\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            string action = FormatObjectSessionStateKind(kind);
            string stateText = kind switch
            {
                WpfObjectSessionStateKind.Hidden => state.IsHidden ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0",
                WpfObjectSessionStateKind.Locked => state.IsLocked ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0",
                WpfObjectSessionStateKind.Pinned => state.IsPinned ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0",
                _ => string.Empty
            };
            WorkflowStatusChanged?.Invoke($"{action}: {stateText} \u00B7 \uD604\uC7AC \uC774\uBBF8\uC9C0 \uC138\uC158\uB9CC");
        }

        private void ExecuteObjectPreviewKeyDown(KeyInputCommandArgs args)
        {
            if (args == null || (args.Key != Key.Delete && args.Key != Key.Back))
            {
                return;
            }

            args.Handled = deleteSelectedObject();
        }

        private static string FormatObjectSessionStateKind(WpfObjectSessionStateKind kind)
            => kind switch
            {
                WpfObjectSessionStateKind.Hidden => "\uC228\uAE40",
                WpfObjectSessionStateKind.Locked => "\uC7A0\uAE08",
                WpfObjectSessionStateKind.Pinned => "\uC774\uB3D9 \uACE0\uC815",
                _ => "\uAC1D\uCCB4 \uC138\uC158 \uC0C1\uD0DC"
            };

        private void ExecuteBeginGroupSelection()
        {
            groupSelectionWorkflowService?.BeginGroupSelection();
            SetGroupSelectionMode(true);
            const string status = "그룹 구성 선택 시작: 목록에서 미그룹 저장 객체를 2개 이상 선택하세요.";
            RefreshGroupSelectionPresentation(status);
            WorkflowStatusChanged?.Invoke(status);
        }

        private void ExecuteCancelGroupSelection()
        {
            bool wasActive = groupSelectionWorkflowService?.IsGroupSelectionActive == true;
            groupSelectionWorkflowService?.CancelGroupSelection();
            SetGroupSelectionMode(false);
            if (wasActive)
            {
                const string status = "그룹 구성 선택을 취소했습니다.";
                RefreshGroupSelectionPresentation(status);
                WorkflowStatusChanged?.Invoke(status);
            }
        }

        private void ExecuteGroupSelectionChanged(object value)
        {
            if (groupSelectionWorkflowService == null
                || value is not WpfObjectReviewListItem row
                || row.Payload is not WpfObjectReviewItemRef item)
            {
                return;
            }

            if (!groupSelectionWorkflowService.SetGroupSelection(
                item,
                row.IsGroupSelected,
                out string error))
            {
                row.IsGroupSelected = false;
                RefreshGroupSelectionPresentation(error);
                WorkflowStatusChanged?.Invoke(error);
                return;
            }

            RefreshGroupSelectionPresentation();
        }

        private void ExecuteCreateGroup()
        {
            if (groupSelectionWorkflowService == null)
            {
                return;
            }

            WpfObjectReviewMutationResult workflowResult = groupSelectionWorkflowService.CreateGroup(
                captureGroupCreationSnapshots());
            if (!workflowResult.IsApplicable)
            {
                string error = string.IsNullOrWhiteSpace(workflowResult.ErrorMessage)
                    ? "그룹을 만들 저장 객체를 2개 이상 선택하세요."
                    : workflowResult.ErrorMessage;
                RefreshGroupSelectionPresentation(error);
                reportGroupCreationError(error);
                return;
            }

            if (!applyGroupCreationMutation(workflowResult))
            {
                const string applyError = "그룹 구성원 적용 중 객체가 변경되어 전체 작업을 되돌렸습니다.";
                RefreshGroupSelectionPresentation(applyError);
                reportGroupCreationError(applyError);
                return;
            }

            int memberCount = workflowResult.MetadataChanges.Count;
            groupSelectionWorkflowService.CancelGroupSelection();
            SetGroupSelectionMode(false);
            RefreshGroupSelectionPresentation();
            WorkflowStatusChanged?.Invoke($"검수 그룹 생성: {memberCount}개 · 라벨 저장 필요");
        }

        private void ExecuteToggleGroupOccluded()
        {
            if (groupSelectionWorkflowService == null
                || !TryResolveSelectedGroupMembers(
                    captureGroupOccludedSnapshots,
                    reportGroupOccludedError,
                    out WpfObjectReviewItemRef selected,
                    out IReadOnlyList<WpfObjectReviewObjectSnapshot> members))
            {
                return;
            }

            WpfObjectReviewMutationResult workflowResult =
                groupSelectionWorkflowService.ToggleGroupOccluded(members);
            if (!workflowResult.IsApplicable)
            {
                reportGroupOccludedError("\uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            if (!applyGroupOccludedMutation(workflowResult))
            {
                reportGroupOccludedError("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            string state = workflowResult.AppliedValue ? "\uAC00\uB9BC \uC801\uC6A9" : "\uAC00\uB9BC \uD574\uC81C";
            WorkflowStatusChanged?.Invoke($"\uADF8\uB8F9 {members.Count}\uAC1C \uAC1D\uCCB4 {state} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694");
        }

        private void ExecuteRemoveSelectedFromGroup()
        {
            if (groupSelectionWorkflowService == null)
            {
                return;
            }

            WpfObjectReviewItemRef selected = SelectedObject?.Payload as WpfObjectReviewItemRef;
            if (selected == null)
            {
                reportGroupMemberRemovalError("\uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            IReadOnlyList<WpfObjectReviewObjectSnapshot> snapshots = captureGroupMemberRemovalSnapshots();
            if (groupSelectionWorkflowService.GetGroupMembers(snapshots, selected).Count < 2)
            {
                reportGroupMemberRemovalError("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            WpfObjectReviewMutationResult workflowResult = groupSelectionWorkflowService.RemoveSelectedFromGroup(
                snapshots,
                selected);
            if (!workflowResult.IsApplicable)
            {
                reportGroupMemberRemovalError("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            if (!applyGroupMemberRemovalMutation(workflowResult))
            {
                reportGroupMemberRemovalError("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            string status = workflowResult.DissolvedGroupCount > 0
                ? "\uADF8\uB8F9\uC5D0\uC11C \uC81C\uAC70\uD588\uACE0 1\uAC1C\uB9CC \uB0A8\uC740 \uADF8\uB8F9\uC740 \uC790\uB3D9 \uD574\uC81C\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694"
                : "\uC120\uD0DD \uAC1D\uCCB4\uB97C \uADF8\uB8F9\uC5D0\uC11C \uC81C\uAC70\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            WorkflowStatusChanged?.Invoke(status);
        }

        private void ExecuteToggleGroupTag()
        {
            if (groupSelectionWorkflowService == null)
            {
                return;
            }

            string requestedTag = SelectedMetadataTag;
            WpfObjectReviewItemRef selected = SelectedObject?.Payload as WpfObjectReviewItemRef;
            if (string.IsNullOrWhiteSpace(requestedTag?.Trim()) || selected == null)
            {
                reportGroupTagError("\uADF8\uB8F9\uACFC Recipe \uD0DC\uADF8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            IReadOnlyList<WpfObjectReviewObjectSnapshot> snapshots = captureGroupTagSnapshots();
            IReadOnlyList<WpfObjectReviewObjectSnapshot> members =
                groupSelectionWorkflowService.GetGroupMembers(snapshots, selected);
            if (members.Count < 2)
            {
                reportGroupTagError("\uADF8\uB8F9\uACFC Recipe \uD0DC\uADF8\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            WpfObjectReviewMutationResult workflowResult = groupSelectionWorkflowService.ToggleGroupTag(
                members,
                requestedTag,
                captureGroupTagData(),
                captureGroupTagRecipeName());
            if (!workflowResult.IsApplicable)
            {
                string error = string.IsNullOrWhiteSpace(workflowResult.ErrorMessage)
                    ? "\uADF8\uB8F9\uACFC Recipe \uD0DC\uADF8\uB97C \uC120\uD0DD\uD558\uC138\uC694."
                    : workflowResult.ErrorMessage;
                reportGroupTagError(error);
                return;
            }

            if (!applyGroupTagMutation(workflowResult))
            {
                reportGroupTagError("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            string state = workflowResult.AppliedValue ? "\uC801\uC6A9" : "\uD574\uC81C";
            WorkflowStatusChanged?.Invoke(
                $"\uADF8\uB8F9 {members.Count}\uAC1C \uAC1D\uCCB4 \uD0DC\uADF8 {state}: {workflowResult.Tag} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694");
        }

        private void ExecuteDissolveSelectedGroup()
        {
            if (groupSelectionWorkflowService == null)
            {
                return;
            }

            WpfObjectReviewItemRef selected = SelectedObject?.Payload as WpfObjectReviewItemRef;
            if (selected == null)
            {
                reportGroupDissolveError("\uD574\uC81C\uD560 \uAC80\uC218 \uADF8\uB8F9\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            IReadOnlyList<WpfObjectReviewObjectSnapshot> members =
                groupSelectionWorkflowService.GetGroupMembers(captureGroupDissolveSnapshots(), selected);
            if (members.Count < 2)
            {
                reportGroupDissolveError("\uD574\uC81C\uD560 \uAC80\uC218 \uADF8\uB8F9\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            if (!confirmGroupDissolve(members.Count))
            {
                return;
            }

            WpfObjectReviewMutationResult workflowResult = groupSelectionWorkflowService.ClearGroup(members);
            if (!workflowResult.IsApplicable || !applyGroupDissolveMutation(workflowResult))
            {
                reportGroupDissolveError("\uC120\uD0DD\uD55C \uAC80\uC218 \uADF8\uB8F9\uC744 \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            WorkflowStatusChanged?.Invoke(
                $"\uAC80\uC218 \uADF8\uB8F9 \uD574\uC81C: {members.Count}\uAC1C \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694");
        }

        private bool TryResolveSelectedGroupMembers(
            Func<IReadOnlyList<WpfObjectReviewObjectSnapshot>> snapshotProvider,
            Action<string> reportError,
            out WpfObjectReviewItemRef selected,
            out IReadOnlyList<WpfObjectReviewObjectSnapshot> members)
        {
            selected = (SelectedObject?.Payload as WpfObjectReviewItemRef);
            members = Array.Empty<WpfObjectReviewObjectSnapshot>();
            if (selected == null)
            {
                reportError("\uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                return false;
            }

            members = groupSelectionWorkflowService.GetGroupMembers(snapshotProvider(), selected);
            if (members.Count >= 2)
            {
                return true;
            }

            reportError("\uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
            return false;
        }

        public void SetSplitPending(WpfSegmentationSplitOrientation? orientation)
        {
            IsSplitPending = orientation.HasValue;
            if (orientation.HasValue)
            {
                IsSegmentAdvancedEditorOpen = true;
            }
            SplitStatusText = orientation switch
            {
                WpfSegmentationSplitOrientation.Vertical
                    => "\uC138\uB85C \uC808\uB2E8 \uC704\uCE58\uB97C \uCE94\uBC84\uC2A4\uC5D0\uC11C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.",
                WpfSegmentationSplitOrientation.Horizontal
                    => "\uAC00\uB85C \uC808\uB2E8 \uC704\uCE58\uB97C \uCE94\uBC84\uC2A4\uC5D0\uC11C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.",
                _ => "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uB97C \uC138\uB85C \uB610\uB294 \uAC00\uB85C\uB85C \uC808\uB2E8\uD569\uB2C8\uB2E4."
            };
            RefreshActionState();
        }

        public void SetHoleEditPending(WpfSegmentationHoleEditMode? mode)
        {
            IsHoleEditPending = mode.HasValue;
            if (mode.HasValue)
            {
                IsSegmentAdvancedEditorOpen = true;
            }
            HoleEditStatusText = mode switch
            {
                WpfSegmentationHoleEditMode.Add
                    => "\uAC1D\uCCB4 \uC548\uCABD\uC5D0 \uAD6C\uBA4D \uB2E4\uAC01\uD615\uC744 \uADF8\uB9AC\uACE0 \uCCAB \uC810\uC744 \uD074\uB9AD\uD558\uAC70\uB098 \uB354\uBE14\uD074\uB9AD\uD558\uC138\uC694.",
                WpfSegmentationHoleEditMode.Remove
                    => "\uCC44\uC6B8 \uB0B4\uBD80 \uAD6C\uBA4D\uC744 \uCE94\uBC84\uC2A4\uC5D0\uC11C \uD074\uB9AD\uD558\uC138\uC694.",
                _ => "\uB0B4\uBD80 \uAD6C\uBA4D\uC744 \uB2E4\uAC01\uD615\uC73C\uB85C \uCD94\uAC00\uD558\uAC70\uB098 \uD074\uB9AD\uD574 \uCC44\uC6C1\uB2C8\uB2E4."
            };
            RefreshActionState();
        }

        public void SetVertexEditPending(WpfPolygonVertexEditMode? mode)
        {
            IsVertexEditPending = mode.HasValue;
            if (mode.HasValue)
            {
                IsSegmentAdvancedEditorOpen = true;
            }

            VertexEditStatusText = mode switch
            {
                WpfPolygonVertexEditMode.Insert
                    => "\uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.",
                WpfPolygonVertexEditMode.Delete
                    => "\uC0AD\uC81C\uD560 \uD3F4\uB9AC\uACE4 \uC815\uC810 \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.",
                _ => "\uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC\uC5D0 \uC815\uC810\uC744 \uCD94\uAC00\uD558\uAC70\uB098 \uAE30\uC874 \uC815\uC810\uC744 \uC0AD\uC81C\uD569\uB2C8\uB2E4."
            };
            RefreshActionState();
        }

        public void SetIntelligentScissorsState(bool pending, bool hasPreview, string statusText = "")
        {
            IsIntelligentScissorsPending = pending;
            HasIntelligentScissorsPreview = pending && hasPreview;
            if (pending)
            {
                IsSegmentAdvancedEditorOpen = true;
            }

            IntelligentScissorsStatusText = !string.IsNullOrWhiteSpace(statusText)
                ? statusText.Trim()
                : "\uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC\uB97C \uC120\uD0DD\uD574 \uC774\uBBF8\uC9C0 \uACBD\uACC4 \uACBD\uB85C\uB97C \uBBF8\uB9AC\uBCF4\uAE30\uD569\uB2C8\uB2E4.";
            RefreshActionState();
        }

        public void SetRemoveUnderlyingPreview(bool pending, string statusText = "")
        {
            IsRemoveUnderlyingPreviewPending = pending;
            if (pending)
            {
                IsSegmentAdvancedEditorOpen = true;
            }
            RemoveUnderlyingStatusText = pending && !string.IsNullOrWhiteSpace(statusText)
                ? statusText.Trim()
                : "\uC704\uCABD \uAC1D\uCCB4\uC640 \uACB9\uCE58\uB294 \uB4A4\uCABD geometry\uB97C \uBD84\uC11D\uD55C \uD6C4 \uD655\uC778\uD574 \uC81C\uAC70\uD569\uB2C8\uB2E4.";
            RefreshActionState();
        }

        public IDisposable SuppressSelectionNotifications()
        {
            // WPF raises SelectionChanged while a large review list is rebound; those transient null selections
            // should not clear canvas handles or polygon edit state.
            selectionNotificationSuppressDepth++;
            return new SelectionNotificationScope(this);
        }

        public bool TryResolveSelectedItem(
            IReadOnlyList<string> manualRoiOverlayIds,
            int manualRoiCount,
            out WpfObjectReviewItemRef item)
            => ObjectReviewSelectionService.TryResolveSelectedItem(
                SelectedObject,
                manualRoiOverlayIds,
                manualRoiCount,
                out item);

        public int GetSelectedRowIndex()
            => ObjectReviewSelectionService.GetSelectedRowIndex(Objects, SelectedObject);

        public bool IsSelectedSource(WpfObjectReviewSource source)
            => ObjectReviewSelectionService.IsSource(SelectedObject, source);

        public void SetObjects(IEnumerable<WpfObjectReviewListItem> objects, string summary, string selectedSourceKey = "", int selectedIndex = -1)
        {
            WpfObjectReviewListItem selected = null;

            SummaryText = summary;
            SelectedObject = null;
            List<WpfObjectReviewListItem> rows = (objects ?? Array.Empty<WpfObjectReviewListItem>()).ToList();
            foreach (WpfObjectReviewListItem item in rows)
            {
                if (selected == null
                    && item.IsEnabled
                    && string.Equals(item.SourceKey, selectedSourceKey, StringComparison.OrdinalIgnoreCase)
                    && item.SourceIndex == selectedIndex)
                {
                    selected = item;
                }
            }

            // Large labeling sessions can have thousands of rows. Publish one Reset instead
            // of one CollectionChanged event per object so the side panel stays responsive.
            Objects.ReplaceAll(rows);
            ApplyGroupSelectionModeToRows();
            RefreshMetadataTagCatalog();
            RefreshGroupCatalog();
            RefreshMetadataFilter(selectFirstMatch: false);
            SelectedObject = selected?.IsMetadataFilterMatch == true
                ? selected
                : Objects.FirstOrDefault(item => item.IsEnabled && item.IsMetadataFilterMatch);
            RefreshActionState();
        }

        public void SetMetadataTagDefinitions(IEnumerable<string> tags)
        {
            recipeMetadataTags.Clear();
            recipeMetadataTags.AddRange(ObjectMetadataStateService.NormalizeTags(tags));
            RefreshMetadataTagCatalog();
        }

        public void SetGroupSelectionMode(bool active)
        {
            IsGroupSelectionMode = active;
            if (!active)
            {
                foreach (WpfObjectReviewListItem item in Objects)
                {
                    if (item != null)
                    {
                        item.IsGroupSelected = false;
                    }
                }
            }

            ApplyGroupSelectionModeToRows();
            RefreshGroupSelectionPresentation();
        }

        public void RefreshGroupSelectionPresentation(string statusText = "")
        {
            ObjectReviewGroupSelectionPresentationSnapshot snapshot =
                groupSelectionPresentationService.Build(Objects, IsGroupSelectionMode, statusText);
            GroupSelectionCount = snapshot.SelectedCount;
            IsCreateGroupEnabled = snapshot.IsCreateGroupEnabled;
            GroupSelectionStatusText = snapshot.StatusText;
        }

        public string GetSelectedObjectGroupId()
            => SelectedObject?.GroupId ?? string.Empty;

        public void RefreshSelectedObjectSessionState()
            => ApplySelectedObjectPresentation();

        public IReadOnlyList<int> GetMergeSelectedManualSegmentIndices()
            => Objects
                .Where(item => item?.IsManualSegment == true
                    && item.IsMergeSelected
                    && !item.IsHidden
                    && !item.IsLocked)
                .Select(item => item.SourceIndex)
                .Distinct()
                .OrderBy(index => index)
                .ToList();

        public bool TryReplaceObject(int objectRowIndex, WpfObjectReviewListItem item, bool select)
            => TryUpsertObject(objectRowIndex, item, summary: null, select);

        public bool TryUpsertObject(int objectRowIndex, WpfObjectReviewListItem item, string summary, bool select)
        {
            if (item == null || objectRowIndex < 0 || objectRowIndex > Objects.Count)
            {
                return false;
            }

            if (summary != null)
            {
                SummaryText = summary;
            }

            // Brush/mask commits can insert the first segment row before AI rows. Keep
            // that path as one Replace/Insert event instead of resetting the whole list.
            bool segmentCollectionStateChanged = true;
            bool groupCatalogChanged = !string.IsNullOrWhiteSpace(item.GroupId);
            WpfObjectReviewListItem replacedItem = null;
            bool reuseMetadataProjection = false;
            if (Objects.Count == 1 && Objects[0]?.IsEnabled != true)
            {
                Objects[0] = item;
            }
            else if (objectRowIndex < Objects.Count
                && string.Equals(Objects[objectRowIndex]?.SourceKey, item.SourceKey, StringComparison.OrdinalIgnoreCase)
                && Objects[objectRowIndex]?.SourceIndex == item.SourceIndex)
            {
                replacedItem = Objects[objectRowIndex];
                groupCatalogChanged |= !string.IsNullOrWhiteSpace(replacedItem?.GroupId);
                segmentCollectionStateChanged =
                    replacedItem?.IsManualSegment == true
                    || item.IsManualSegment;
                reuseMetadataProjection = CanReuseMetadataProjection(replacedItem, item);
                Objects[objectRowIndex] = item;
            }
            else if (objectRowIndex <= Objects.Count)
            {
                Objects.Insert(objectRowIndex, item);
            }
            else
            {
                return false;
            }

            bool actionStateRefreshed = false;
            if (select)
            {
                actionStateRefreshed = SetSelectedObject(
                    item,
                    refreshSegmentCollectionState: segmentCollectionStateChanged);
            }

            if (!actionStateRefreshed)
            {
                RefreshActionState(segmentCollectionStateChanged);
            }
            item.SetGroupSelectionMode(IsGroupSelectionMode);
            if (reuseMetadataProjection)
            {
                item.ApplyMetadataFilter(replacedItem.IsMetadataFilterMatch);
            }
            else
            {
                RefreshMetadataTagCatalog();
                if (groupCatalogChanged)
                {
                    RefreshGroupCatalog();
                }

                RefreshMetadataFilter();
            }
            return true;
        }

        public bool TryRemoveObject(int objectRowIndex, string summary, int selectedRowIndex)
        {
            if (objectRowIndex < 0 || objectRowIndex >= Objects.Count)
            {
                return false;
            }

            // Large object lists must emit one Remove event, not a Reset that forces WPF to
            // rebuild every row after a single ROI delete.
            SummaryText = summary;
            bool segmentCollectionStateChanged = Objects[objectRowIndex]?.IsManualSegment == true;
            bool groupCatalogChanged =
                !string.IsNullOrWhiteSpace(Objects[objectRowIndex]?.GroupId);
            WpfObjectReviewListItem removedItem = Objects[objectRowIndex];
            bool reuseMetadataProjection = CanReuseMetadataProjectionAfterRemoval(removedItem);
            Objects.RemoveAt(objectRowIndex);
            bool actionStateRefreshed;
            if (Objects.Count == 0)
            {
                actionStateRefreshed = SetSelectedObject(
                    null,
                    refreshSegmentCollectionState: segmentCollectionStateChanged);
            }
            else
            {
                int clampedSelection = Math.Max(0, Math.Min(selectedRowIndex, Objects.Count - 1));
                actionStateRefreshed = SetSelectedObject(
                    Objects[clampedSelection],
                    refreshSegmentCollectionState: segmentCollectionStateChanged);
            }

            if (!actionStateRefreshed)
            {
                RefreshActionState(segmentCollectionStateChanged);
            }
            if (reuseMetadataProjection)
            {
                if (removedItem.IsEnabled)
                {
                    metadataFilterEnabledCount = Math.Max(0, metadataFilterEnabledCount - 1);
                    if (removedItem.IsMetadataFilterMatch)
                    {
                        metadataFilterVisibleCount = Math.Max(0, metadataFilterVisibleCount - 1);
                    }
                }

                MetadataFilterSummaryText = metadataFilterPresentationService.BuildFilterSummaryText(
                    metadataFilterEnabledCount,
                    metadataFilterVisibleCount,
                    SelectedMetadataTagFilter,
                    SelectedGroupFilter,
                    IsOccludedFilterActive);
            }
            else
            {
                RefreshMetadataTagCatalog();
                if (groupCatalogChanged)
                {
                    RefreshGroupCatalog();
                }

                RefreshMetadataFilter();
            }
            return true;
        }

        public void SetClassNames(IEnumerable<string> classNames, string selectedName = "")
        {
            string normalizedSelection = selectedName?.Trim() ?? string.Empty;
            ClassNames.Clear();

            foreach (string className in classNames ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(className))
                {
                    ClassNames.Add(className);
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedSelection))
            {
                SelectedClassName = ClassNames.FirstOrDefault(item =>
                    string.Equals(item, normalizedSelection, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            }
            else if (!ClassNames.Contains(SelectedClassName))
            {
                SelectedClassName = string.Empty;
            }
        }

        public void SetSelectedObjectClass(IEnumerable<string> classNames, string className)
        {
            SetClassNames(classNames, ObjectReviewEditService.NormalizeClassName(className));
        }

        public void SetLabelSaveState(string stateKey, string badgeText, string detailText)
        {
            LabelSaveStateKey = string.IsNullOrWhiteSpace(stateKey) ? "Waiting" : stateKey.Trim();
            LabelSaveBadgeText = string.IsNullOrWhiteSpace(badgeText) ? "\uB77C\uBCA8 \uB300\uAE30" : badgeText.Trim();
            LabelSaveDetailText = string.IsNullOrWhiteSpace(detailText)
                ? "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4."
                : detailText.Trim();
        }

        public void SetQualityReviewState(
            YoloImageQualityReviewState state,
            bool hasActiveImage,
            bool canMarkReviewed,
            string qualityReviewNote = "")
        {
            IsQualityReviewEnabled = hasActiveImage;
            IsMarkQualityReviewedEnabled = hasActiveImage && canMarkReviewed;
            IsQualityUnreviewedActive = hasActiveImage && state == YoloImageQualityReviewState.Unreviewed;
            IsQualityNeedsFixActive = hasActiveImage && state == YoloImageQualityReviewState.NeedsFix;
            IsQualityReviewedActive = hasActiveImage && state == YoloImageQualityReviewState.Reviewed;
            QualityReviewNoteText = hasActiveImage
                ? YoloImageReviewStatusService.NormalizeQualityReviewNote(qualityReviewNote)
                : string.Empty;

            if (!hasActiveImage)
            {
                QualityReviewStatusText = "이미지 없음";
                QualityReviewDetailText = "Detection/Segmentation 이미지를 열면 품질 검수 상태를 표시합니다.";
                return;
            }

            switch (state)
            {
                case YoloImageQualityReviewState.NeedsFix:
                    QualityReviewStatusText = "수정 필요";
                    QualityReviewDetailText = "사유를 고치면 수정 필요를 다시 눌러 저장하세요.";
                    break;
                case YoloImageQualityReviewState.Reviewed:
                    QualityReviewStatusText = "검수 완료";
                    QualityReviewDetailText = "현재 저장 라벨이 품질 검수를 통과했습니다.";
                    break;
                default:
                    QualityReviewStatusText = "미검토";
                    QualityReviewDetailText = canMarkReviewed
                        ? "저장 라벨을 확인한 뒤 수정 필요 또는 검수 완료를 선택하세요."
                        : "라벨 저장 또는 객체 없음 완료 후 검수 완료를 선택할 수 있습니다.";
                    break;
            }
        }

        public void RefreshActionState()
            => RefreshActionState(refreshSegmentCollectionState: true);

        private bool SetSelectedObject(
            WpfObjectReviewListItem value,
            bool refreshSegmentCollectionState)
        {
            if (!SetProperty(ref selectedObject, value, nameof(SelectedObject)))
            {
                return false;
            }

            RefreshActionState(refreshSegmentCollectionState);
            return true;
        }

        private void RefreshActionState(bool refreshSegmentCollectionState)
        {
            ObjectReviewActionAvailabilitySnapshot snapshot = actionAvailabilityService.Build(
                SelectedObject,
                Objects,
                SelectedClassName,
                IsSplitPending,
                IsHoleEditPending,
                IsVertexEditPending,
                IsIntelligentScissorsPending,
                IsRemoveUnderlyingPreviewPending,
                refreshSegmentCollectionState);

            IsSegmentContextVisible = snapshot.IsSegmentContextVisible;
            IsPolygonVertexContextVisible = snapshot.IsPolygonVertexContextVisible;
            if (!snapshot.IsSegmentContextVisible)
            {
                IsSegmentAdvancedEditorOpen = false;
            }
            IsDeleteEnabled = snapshot.IsDeleteEnabled;
            IsApplyClassEnabled = snapshot.IsApplyClassEnabled;
            IsSplitEnabled = snapshot.IsSplitEnabled;
            IsHoleEditEnabled = snapshot.IsHoleEditEnabled;
            IsVertexEditEnabled = snapshot.IsVertexEditEnabled;
            IsIntelligentScissorsEnabled = snapshot.IsIntelligentScissorsEnabled;
            IsSendToBackEnabled = snapshot.IsSendToBackEnabled;
            IsSendBackwardEnabled = snapshot.IsSendBackwardEnabled;
            IsBringForwardEnabled = snapshot.IsBringForwardEnabled;
            IsBringToFrontEnabled = snapshot.IsBringToFrontEnabled;
            ZOrderStatusText = snapshot.ZOrderStatusText;
            IsRemoveUnderlyingPreviewEnabled = snapshot.IsRemoveUnderlyingPreviewEnabled;
            if (refreshSegmentCollectionState)
            {
                IsMergeSelectedSegmentsEnabled = snapshot.IsMergeSelectedSegmentsEnabled;
                MergeSelectionText = snapshot.MergeSelectionText;
            }
            ApplySelectedObjectPresentation();
        }

        private void ApplySelectedObjectPresentation()
        {
            ApplySelectedObjectPresentation(
                selectedObjectPresentationService.Build(SelectedObject, SummaryText, Objects));
        }

        private void ApplySelectedObjectPresentation(
            ObjectReviewSelectedObjectPresentationSnapshot snapshot)
        {
            IsObjectSessionStateEnabled = snapshot.IsObjectSessionStateEnabled;
            IsSelectedObjectHidden = snapshot.IsSelectedObjectHidden;
            IsSelectedObjectLocked = snapshot.IsSelectedObjectLocked;
            IsSelectedObjectPinned = snapshot.IsSelectedObjectPinned;
            ObjectSessionStateStatusText = snapshot.ObjectSessionStateStatusText;
            IsPersistentMetadataEnabled = snapshot.IsPersistentMetadataEnabled;
            IsSelectedObjectOccluded = snapshot.IsSelectedObjectOccluded;
            SelectedObjectTagsText = snapshot.SelectedObjectTagsText;
            IsSelectedObjectGrouped = snapshot.IsSelectedObjectGrouped;
            SelectedObjectGroupText = snapshot.SelectedObjectGroupText;
            ApplySelectedObjectTaskPresentation(snapshot);
        }

        private void ApplySelectedObjectTaskPresentation()
            => ApplySelectedObjectTaskPresentation(
                selectedObjectPresentationService.Build(SelectedObject, SummaryText, Objects));

        private void ApplySelectedObjectTaskPresentation(
            ObjectReviewSelectedObjectPresentationSnapshot snapshot)
        {
            SelectedObjectTaskTitleText = snapshot.SelectedObjectTaskTitleText;
            SelectedObjectTaskDetailText = snapshot.SelectedObjectTaskDetailText;
            SelectedObjectTaskActionText = snapshot.SelectedObjectTaskActionText;
        }

        private void RefreshMetadataTagCatalog()
        {
            ObjectReviewMetadataTagCatalogSnapshot snapshot = metadataFilterPresentationService.BuildTagCatalog(
                recipeMetadataTags,
                Objects,
                SelectedMetadataTag,
                SelectedMetadataTagFilter);
            isRefreshingMetadataTagCatalog = true;
            try
            {
                MetadataTagOptions.Clear();
                foreach (string tag in snapshot.Tags)
                {
                    MetadataTagOptions.Add(tag);
                }

                MetadataTagFilters.Clear();
                foreach (string filter in snapshot.Filters)
                {
                    MetadataTagFilters.Add(filter);
                }
            }
            finally
            {
                isRefreshingMetadataTagCatalog = false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SelectedTag))
            {
                SelectedMetadataTag = snapshot.SelectedTag;
            }

            selectedMetadataTagFilter = snapshot.SelectedFilter;
            OnPropertyChanged(nameof(SelectedMetadataTagFilter));
        }

        private void RefreshGroupCatalog()
        {
            ObjectReviewGroupCatalogSnapshot snapshot = metadataFilterPresentationService.BuildGroupCatalog(
                Objects,
                SelectedGroupFilter);

            isRefreshingGroupCatalog = true;
            try
            {
                GroupFilters.Clear();
                foreach (string filter in snapshot.Filters)
                {
                    GroupFilters.Add(filter);
                }

                foreach (ObjectReviewGroupPresentation group in snapshot.Groups)
                {
                    foreach (WpfObjectReviewListItem item in Objects.Where(item =>
                        item?.IsEnabled == true
                        && string.Equals(item.GroupId, group.GroupId, StringComparison.Ordinal)))
                    {
                        item.ApplyGroupPresentation(group.DisplayText, group.MemberCount);
                    }
                }

                foreach (WpfObjectReviewListItem item in Objects.Where(item =>
                    item?.IsEnabled == true && string.IsNullOrWhiteSpace(item.GroupId)))
                {
                    item.ApplyGroupPresentation(string.Empty, 0);
                }
            }
            finally
            {
                isRefreshingGroupCatalog = false;
            }

            selectedGroupFilter = snapshot.SelectedFilter;
            OnPropertyChanged(nameof(SelectedGroupFilter));
            ApplySelectedObjectPresentation();
        }

        private void ApplyGroupSelectionModeToRows()
        {
            foreach (WpfObjectReviewListItem item in Objects)
            {
                item?.SetGroupSelectionMode(IsGroupSelectionMode);
            }
        }

        private void RefreshMetadataFilter(bool selectFirstMatch = true)
        {
            ObjectReviewMetadataFilterSnapshot snapshot = metadataFilterPresentationService.BuildFilter(
                Objects,
                SelectedMetadataTagFilter,
                SelectedGroupFilter,
                IsOccludedFilterActive,
                selectFirstMatch);
            for (int index = 0; index < Objects.Count; index++)
            {
                Objects[index]?.ApplyMetadataFilter(snapshot.Matches[index]);
            }

            metadataFilterEnabledCount = snapshot.EnabledCount;
            metadataFilterVisibleCount = snapshot.VisibleCount;
            if (selectFirstMatch
                && SelectedObject?.IsMetadataFilterMatch != true)
            {
                SelectedObject = snapshot.FirstMatchingIndex >= 0
                    ? Objects[snapshot.FirstMatchingIndex]
                    : null;
            }
            MetadataFilterSummaryText = snapshot.SummaryText;
        }

        private static bool CanReuseMetadataProjection(
            WpfObjectReviewListItem current,
            WpfObjectReviewListItem replacement)
            => current != null
                && replacement != null
                && current.IsEnabled == replacement.IsEnabled
                && current.IsOccluded == replacement.IsOccluded
                && string.IsNullOrWhiteSpace(current.GroupId)
                && string.IsNullOrWhiteSpace(replacement.GroupId)
                && current.MetadataTags.SequenceEqual(replacement.MetadataTags, StringComparer.OrdinalIgnoreCase);

        private bool CanReuseMetadataProjectionAfterRemoval(WpfObjectReviewListItem removedItem)
            => removedItem != null
                && string.IsNullOrWhiteSpace(removedItem.GroupId)
                && removedItem.MetadataTags.Count == 0
                && !IsOccludedFilterActive
                && string.Equals(SelectedMetadataTagFilter, AllMetadataTagsFilter, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SelectedGroupFilter, AllGroupsFilter, StringComparison.OrdinalIgnoreCase);

        private void ReleaseSelectionNotificationSuppression()
        {
            if (selectionNotificationSuppressDepth > 0)
            {
                selectionNotificationSuppressDepth--;
            }
        }

        private sealed class SelectionNotificationScope : IDisposable
        {
            private WpfObjectReviewPanelViewModel owner;

            public SelectionNotificationScope(WpfObjectReviewPanelViewModel owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                WpfObjectReviewPanelViewModel currentOwner = owner;
                if (currentOwner == null)
                {
                    return;
                }

                owner = null;
                currentOwner.ReleaseSelectionNotificationSuppression();
                currentOwner.RefreshActionState();
            }
        }
    }
}
