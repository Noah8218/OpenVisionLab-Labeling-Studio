using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfObjectReviewPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private string summaryText = "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uAC1D\uCCB4 \uC5C6\uC74C";
        private WpfObjectReviewListItem selectedObject;
        private string selectedClassName = string.Empty;
        private bool isDeleteEnabled;
        private bool isApplyClassEnabled;
        private int selectionNotificationSuppressDepth;
        private ICommand deleteObjectCommand = new RelayCommand(NoOpCommand);
        private ICommand applyObjectClassCommand = new RelayCommand(NoOpCommand);
        private ICommand objectSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand objectPreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);

        public string ViewName => nameof(WpfObjectReviewPanel);

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

        public string SummaryText
        {
            get => summaryText;
            set => SetProperty(ref summaryText, value ?? string.Empty);
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

        public bool IsSelectionNotificationSuppressed => selectionNotificationSuppressDepth > 0;

        public void ConfigureCommands(
            Action deleteObject,
            Action applyObjectClass,
            Action<object> objectSelectionChanged,
            Action<KeyInputCommandArgs> objectPreviewKeyDown)
        {
            // The review panel exposes commands; the shell injects workflow actions without owning the view events.
            DeleteObjectCommand = new RelayCommand(deleteObject ?? NoOpCommand);
            ApplyObjectClassCommand = new RelayCommand(applyObjectClass ?? NoOpCommand);
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

        public void RefreshActionState()
        {
            bool hasSelectedObject = SelectedObject?.IsEnabled == true;
            IsDeleteEnabled = hasSelectedObject;
            IsApplyClassEnabled = hasSelectedObject && !string.IsNullOrWhiteSpace(SelectedClassName);
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
