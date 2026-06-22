using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MvcVisionSystem
{
    public sealed class WpfClassCatalogPanelViewModel : WpfObservableViewModel
    {
        private string className = string.Empty;
        private string outputRootPath = string.Empty;
        private string statusText = "\uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC785\uB825\uD558\uACE0 \uCD94\uAC00\uB97C \uB204\uB974\uC138\uC694.";
        private WpfClassCatalogListItem selectedClass;

        public string ViewName => nameof(WpfClassCatalogPanel);

        public ObservableCollection<WpfClassCatalogListItem> Classes { get; } = new ObservableCollection<WpfClassCatalogListItem>();

        public string ClassName
        {
            get => className;
            set => SetProperty(ref className, value ?? string.Empty);
        }

        public string OutputRootPath
        {
            get => outputRootPath;
            set => SetProperty(ref outputRootPath, value ?? string.Empty);
        }

        public string StatusText
        {
            get => statusText;
            set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public WpfClassCatalogListItem SelectedClass
        {
            get => selectedClass;
            set
            {
                if (SetProperty(ref selectedClass, value) && value != null)
                {
                    ClassName = value.Text;
                }
            }
        }

        public void LoadOutputRoot(string path)
        {
            OutputRootPath = path ?? string.Empty;
        }

        public void SetClasses(IEnumerable<CClassItem> classItems, string selectedName = "")
        {
            string normalizedSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            WpfClassCatalogListItem selectedItem = null;

            SelectedClass = null;
            Classes.Clear();

            foreach (CClassItem classItem in (classItems ?? Array.Empty<CClassItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
            {
                var listItem = new WpfClassCatalogListItem(classItem);
                Classes.Add(listItem);
                if (!string.IsNullOrWhiteSpace(normalizedSelectedName)
                    && string.Equals(listItem.Text, normalizedSelectedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedItem = listItem;
                }
            }

            if (selectedItem != null)
            {
                SelectedClass = selectedItem;
            }
        }

        public void SelectClass(string name)
        {
            string normalizedName = ClassCatalogService.NormalizeClassName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            WpfClassCatalogListItem item = Classes.FirstOrDefault(candidate =>
                string.Equals(candidate.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                SelectedClass = item;
                return;
            }

            ClassName = normalizedName;
        }

        public void ClearClassName()
        {
            ClassName = string.Empty;
        }
    }

    public sealed class WpfClassCatalogListItem
    {
        public WpfClassCatalogListItem(CClassItem classItem)
        {
            Text = ClassCatalogService.NormalizeClassName(classItem?.Text);
            System.Drawing.Color drawColor = classItem?.DrawColor ?? System.Drawing.Color.LimeGreen;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(drawColor.R, drawColor.G, drawColor.B));
            brush.Freeze();
            DrawBrush = brush;
        }

        public string Text { get; }

        public string DisplayText => Text;

        public string ToolTip => $"\uD074\uB798\uC2A4: {Text}";

        public MediaBrush DrawBrush { get; }
    }
}
