using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfObjectReviewPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
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
        private string qualityReviewStatusText = "이미지 없음";
        private string qualityReviewDetailText = "이미지를 열면 품질 검수 상태를 표시합니다.";
        private string qualityReviewNoteText = string.Empty;
        private bool isQualityReviewEnabled;
        private bool isMarkQualityReviewedEnabled;
        private bool isQualityUnreviewedActive;
        private bool isQualityNeedsFixActive;
        private bool isQualityReviewedActive;
        private int selectionNotificationSuppressDepth;
        private ICommand deleteObjectCommand = new RelayCommand(NoOpCommand);
        private ICommand applyObjectClassCommand = new RelayCommand(NoOpCommand);
        private ICommand objectSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand objectPreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand markQualityUnreviewedCommand = new RelayCommand(NoOpCommand);
        private ICommand markQualityNeedsFixCommand = new RelayCommand(NoOpCommand);
        private ICommand markQualityReviewedCommand = new RelayCommand(NoOpCommand);
        private ICommand exportQualityReviewReportCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfObjectReviewPanel);

        public string PanelModeTitleText => "\uC800\uC7A5 \uB77C\uBCA8";

        public string PanelModeBadgeText => "\uC800\uC7A5 \uB77C\uBCA8\uB9CC";

        public string PanelModeScopeText => "\uBBF8\uD655\uC815 AI \uD6C4\uBCF4 \uD45C\uC2DC \uC548 \uD568";

        public string PanelModeDetailText => "\uC774 \uD328\uB110\uC740 \uD30C\uC77C\uC5D0 \uBC18\uC601\uB420 \uC800\uC7A5 \uB77C\uBCA8\uB9CC \uD3B8\uC9D1\uD569\uB2C8\uB2E4. \uBBF8\uD655\uC815 AI \uD6C4\uBCF4\uB294 \uD45C\uC2DC\uD558\uC9C0 \uC54A\uC73C\uBA70, AI \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uBA74 \uC800\uC7A5 \uB77C\uBCA8\uB85C \uC804\uD658\uB418\uC5B4 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";

        public string ActionGuideText => "\uC0AD\uC81C/\uD074\uB798\uC2A4 \uBCC0\uACBD\uC740 \uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0 \uBC14\uB85C \uBC18\uC601\uB418\uACE0 \uC800\uC7A5 \uD544\uC694 \uC0C1\uD0DC\uB85C \uBC14\uB00D\uB2C8\uB2E4. \uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uB824\uBA74 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB204\uB974\uC138\uC694.";

        public WpfBulkObservableCollection<WpfObjectReviewListItem> Objects { get; } = new WpfBulkObservableCollection<WpfObjectReviewListItem>();

        public ObservableCollection<string> ClassNames { get; } = new ObservableCollection<string>();

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
                    RefreshSelectedObjectTaskText();
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
            set
            {
                if (SetProperty(ref selectedObject, value))
                {
                    RefreshActionState();
                }
            }
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
            Action<KeyInputCommandArgs> objectPreviewKeyDown)
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
            => WpfObjectReviewSelectionService.TryResolveSelectedItem(
                SelectedObject,
                manualRoiOverlayIds,
                manualRoiCount,
                out item);

        public int GetSelectedRowIndex()
            => WpfObjectReviewSelectionService.GetSelectedRowIndex(Objects, SelectedObject);

        public bool IsSelectedSource(WpfObjectReviewSource source)
            => WpfObjectReviewSelectionService.IsSource(SelectedObject, source);

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
            SelectedObject = selected ?? Objects.FirstOrDefault(item => item.IsEnabled);
        }

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
            if (Objects.Count == 1 && Objects[0]?.IsEnabled != true)
            {
                Objects[0] = item;
            }
            else if (objectRowIndex < Objects.Count
                && string.Equals(Objects[objectRowIndex]?.SourceKey, item.SourceKey, StringComparison.OrdinalIgnoreCase)
                && Objects[objectRowIndex]?.SourceIndex == item.SourceIndex)
            {
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

            if (select)
            {
                SelectedObject = item;
            }

            RefreshActionState();
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
            Objects.RemoveAt(objectRowIndex);
            if (Objects.Count == 0)
            {
                SelectedObject = null;
            }
            else
            {
                int clampedSelection = Math.Max(0, Math.Min(selectedRowIndex, Objects.Count - 1));
                SelectedObject = Objects[clampedSelection];
            }

            RefreshActionState();
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
            SetClassNames(classNames, WpfObjectReviewEditService.NormalizeClassName(className));
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
        {
            bool hasSelectedObject = SelectedObject?.IsEnabled == true;
            IsDeleteEnabled = hasSelectedObject;
            IsApplyClassEnabled = hasSelectedObject && !string.IsNullOrWhiteSpace(SelectedClassName);
            RefreshSelectedObjectTaskText();
        }

        private void RefreshSelectedObjectTaskText()
        {
            if (SelectedObject?.IsEnabled == true)
            {
                SelectedObjectTaskTitleText = "\uC120\uD0DD \uB77C\uBCA8 \uC218\uC815";
                SelectedObjectTaskDetailText = string.IsNullOrWhiteSpace(SelectedObject.DisplayText)
                    ? SummaryText
                    : SelectedObject.DisplayText;
                SelectedObjectTaskActionText = "\uD074\uB798\uC2A4\uB97C \uBC14\uAFB8\uAC70\uB098 \uC0AD\uC81C\uD558\uBA74 \uC800\uC7A5 \uD544\uC694 \uC0C1\uD0DC\uAC00 \uB429\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5\uC73C\uB85C \uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uC138\uC694.";
                return;
            }

            bool hasAnyEnabledObject = Objects.Any(item => item?.IsEnabled == true);
            SelectedObjectTaskTitleText = hasAnyEnabledObject
                ? "\uC120\uD0DD \uB77C\uBCA8 \uC5C6\uC74C"
                : "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uB77C\uBCA8 \uC5C6\uC74C";
            SelectedObjectTaskDetailText = hasAnyEnabledObject
                ? "\uBAA9\uB85D\uC5D0\uC11C \uB77C\uBCA8\uC744 \uC120\uD0DD\uD558\uBA74 \uD074\uB798\uC2A4 \uBCC0\uACBD\uACFC \uC0AD\uC81C\uAC00 \uD65C\uC131\uD654\uB429\uB2C8\uB2E4."
                : SummaryText;
            SelectedObjectTaskActionText = hasAnyEnabledObject
                ? "\uC120\uD0DD \uD6C4 \uD544\uC694\uD55C \uBCC0\uACBD\uC744 \uD558\uACE0, \uB77C\uBCA8 \uC800\uC7A5\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694."
                : "\uAC1D\uCCB4\uAC00 \uC5C6\uB2E4\uBA74 \uB2E4\uC74C \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD558\uAC70\uB098 \uAC1D\uCCB4 \uC5C6\uC74C \uC791\uC5C5\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694.";
        }

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

    public sealed class WpfBulkObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (T item in items ?? Array.Empty<T>())
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public sealed class WpfObjectReviewListItem
    {
        public WpfObjectReviewListItem(string displayText, string toolTip, string sourceKey, int sourceIndex, object payload, bool isEnabled = true)
        {
            DisplayText = displayText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            SourceKey = sourceKey ?? string.Empty;
            SourceIndex = sourceIndex;
            Payload = payload;
            IsEnabled = isEnabled;
        }

        public string DisplayText { get; }

        public string Content => DisplayText;

        public string ToolTip { get; }

        public string SourceKey { get; }

        public int SourceIndex { get; }

        public object Payload { get; }

        public bool IsEnabled { get; }

        public static WpfObjectReviewListItem Empty(string text)
            => new WpfObjectReviewListItem(text, string.Empty, string.Empty, -1, null, isEnabled: false);

        public override string ToString() => DisplayText;
    }
}
