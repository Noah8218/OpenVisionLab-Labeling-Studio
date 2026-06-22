using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfObjectReviewPanelViewModel : WpfObservableViewModel
    {
        private string summaryText = "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uAC1D\uCCB4 \uC5C6\uC74C";
        private WpfObjectReviewListItem selectedObject;
        private string selectedClassName = string.Empty;
        private bool isDeleteEnabled;
        private bool isApplyClassEnabled;

        public string ViewName => nameof(WpfObjectReviewPanel);

        public WpfBulkObservableCollection<WpfObjectReviewListItem> Objects { get; } = new WpfBulkObservableCollection<WpfObjectReviewListItem>();

        public ObservableCollection<string> ClassNames { get; } = new ObservableCollection<string>();

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
        {
            if (item == null || objectRowIndex < 0 || objectRowIndex >= Objects.Count)
            {
                return false;
            }

            // ROI move/resize changes one object. Replacing the row keeps the side panel
            // aligned without rebuilding every object after each edit commit.
            Objects[objectRowIndex] = item;
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
