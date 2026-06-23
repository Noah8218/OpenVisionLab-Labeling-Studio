using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MvcVisionSystem
{
    public sealed class WpfClassCatalogPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private string className = string.Empty;
        private string outputRootPath = string.Empty;
        private string statusText = "\uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC785\uB825\uD558\uACE0 \uCD94\uAC00\uB97C \uB204\uB974\uC138\uC694.";
        private WpfClassCatalogListItem selectedClass;
        private ICommand classNamePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand addClassCommand = new RelayCommand(NoOpCommand);
        private ICommand removeClassCommand = new RelayCommand(NoOpCommand);
        private ICommand browseOutputRootCommand = new RelayCommand(NoOpCommand);
        private ICommand saveOutputRootCommand = new RelayCommand(NoOpCommand);
        private ICommand classSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);

        public string ViewName => nameof(WpfClassCatalogPanel);

        public ObservableCollection<WpfClassCatalogListItem> Classes { get; } = new ObservableCollection<WpfClassCatalogListItem>();

        public ICommand ClassNamePreviewKeyDownCommand
        {
            get => classNamePreviewKeyDownCommand;
            private set => SetProperty(ref classNamePreviewKeyDownCommand, value);
        }

        public ICommand AddClassCommand
        {
            get => addClassCommand;
            private set => SetProperty(ref addClassCommand, value);
        }

        public ICommand RemoveClassCommand
        {
            get => removeClassCommand;
            private set => SetProperty(ref removeClassCommand, value);
        }

        public ICommand BrowseOutputRootCommand
        {
            get => browseOutputRootCommand;
            private set => SetProperty(ref browseOutputRootCommand, value);
        }

        public ICommand SaveOutputRootCommand
        {
            get => saveOutputRootCommand;
            private set => SetProperty(ref saveOutputRootCommand, value);
        }

        public ICommand ClassSelectionChangedCommand
        {
            get => classSelectionChangedCommand;
            private set => SetProperty(ref classSelectionChangedCommand, value);
        }

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

        public void ConfigureCommands(
            Action<KeyInputCommandArgs> classNamePreviewKeyDown,
            Action addClass,
            Action removeClass,
            Action browseOutputRoot,
            Action saveOutputRoot,
            Action<object> classSelectionChanged)
        {
            // Class catalog commands use DTO/value parameters so this ViewModel stays independent from WPF event args.
            ClassNamePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(classNamePreviewKeyDown ?? NoOpKeyCommand);
            AddClassCommand = new RelayCommand(addClass ?? NoOpCommand);
            RemoveClassCommand = new RelayCommand(removeClass ?? NoOpCommand);
            BrowseOutputRootCommand = new RelayCommand(browseOutputRoot ?? NoOpCommand);
            SaveOutputRootCommand = new RelayCommand(saveOutputRoot ?? NoOpCommand);
            ClassSelectionChangedCommand = new RelayCommand<object>(classSelectionChanged ?? NoOpSelectionCommand);
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