using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DrawingColor = System.Drawing.Color;
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
        private ICommand renameClassCommand = new RelayCommand(NoOpCommand);
        private ICommand removeClassCommand = new RelayCommand(NoOpCommand);
        private ICommand applyClassColorCommand = new RelayCommand(NoOpCommand);
        private ICommand classSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private WpfClassCatalogColorPreset selectedColorPreset;

        public WpfClassCatalogPanelViewModel()
        {
            foreach (WpfClassCatalogColorPreset preset in BuildDefaultColorPresets())
            {
                ColorPresets.Add(preset);
            }

            SelectedColorPreset = ColorPresets.FirstOrDefault();
            Classes.CollectionChanged += (_, _) => NotifyClassCatalogSummaryChanged();
        }

        public string ViewName => nameof(WpfClassCatalogPanel);

        public string ClassCatalogGuideTitleText => "\uD074\uB798\uC2A4 \uAD00\uB9AC";

        public string ClassCatalogGuideDetailText => "\uB808\uC2DC\uD53C\uC758 \uD074\uB798\uC2A4 \uC774\uB984/\uC0C9\uC0C1\uB9CC \uAD00\uB9AC\uD569\uB2C8\uB2E4. \uC800\uC7A5 \uD3F4\uB354\uB294 \uB370\uC774\uD130\uC14B \uD648\uC5D0\uC11C \uD655\uC778\uD558\uC138\uC694.";

        public string ClassCatalogSummaryText
        {
            get
            {
                string selected = SelectedClass?.Text;
                if (string.IsNullOrWhiteSpace(selected))
                {
                    selected = "\uC5C6\uC74C";
                }

                return $"\uB4F1\uB85D \uD074\uB798\uC2A4 {Classes.Count}\uAC1C / \uC120\uD0DD: {selected}";
            }
        }

        public string CurrentDrawingClassTitleText => "\uD604\uC7AC \uADF8\uB9B4 \uD074\uB798\uC2A4";

        public string CurrentDrawingClassDetailText
        {
            get
            {
                string selected = SelectedClass?.Text;
                return string.IsNullOrWhiteSpace(selected)
                    ? "\uC120\uD0DD\uB41C \uD074\uB798\uC2A4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. OK, NG\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uD074\uB798\uC2A4\uB97C \uBA3C\uC800 \uCD94\uAC00\uD558\uC138\uC694."
                    : $"{selected} - \uCE94\uBC84\uC2A4\uC5D0\uC11C \uC0C8 \uBC15\uC2A4\uB97C \uADF8\uB9AC\uBA74 \uC774 \uD074\uB798\uC2A4\uAC00 \uAE30\uBCF8 \uC801\uC6A9\uB429\uB2C8\uB2E4.";
            }
        }

        public string ClassCatalogActionText
        {
            get
            {
                return Classes.Count <= 0
                    ? "\uBA3C\uC800 OK, NG\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uD074\uB798\uC2A4\uB97C \uCD94\uAC00\uD558\uC138\uC694."
                    : "\uC774\uB984 \uCD94\uAC00/\uBCC0\uACBD\uC774 \uC8FC \uC791\uC5C5\uC785\uB2C8\uB2E4. \uC0C9\uC0C1\uC740 \uD544\uC694\uD560 \uB54C\uB9CC \uD3BC\uCE58\uC138\uC694.";
            }
        }

        public string ClassColorSectionTitleText => "\uC120\uD0DD \uD074\uB798\uC2A4 \uC0C9\uC0C1(\uD544\uC694 \uC2DC)";

        public string RecipeClassListTitleText => "\uB808\uC2DC\uD53C \uD074\uB798\uC2A4";

        public string RecipeClassListGuideText => "\uC774 \uBAA9\uB85D\uC740 \uD604\uC7AC \uB808\uC2DC\uD53C\uC758 \uB77C\uBCA8 \uC2A4\uD0A4\uB9C8\uC785\uB2C8\uB2E4. \uB2E4\uC74C\uC5D0 \uADF8\uB9B4 \uB77C\uBCA8 \uD074\uB798\uC2A4\uC640 \uD559\uC2B5 \uD074\uB798\uC2A4\uAC00 \uC774 \uBAA9\uB85D\uC744 \uB530\uB985\uB2C8\uB2E4.";

        public ObservableCollection<WpfClassCatalogListItem> Classes { get; } = new ObservableCollection<WpfClassCatalogListItem>();

        public ObservableCollection<WpfClassCatalogColorPreset> ColorPresets { get; } = new ObservableCollection<WpfClassCatalogColorPreset>();

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

        public ICommand RenameClassCommand
        {
            get => renameClassCommand;
            private set => SetProperty(ref renameClassCommand, value);
        }

        public ICommand RemoveClassCommand
        {
            get => removeClassCommand;
            private set => SetProperty(ref removeClassCommand, value);
        }

        public ICommand ApplyClassColorCommand
        {
            get => applyClassColorCommand;
            private set => SetProperty(ref applyClassColorCommand, value);
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

        public WpfClassCatalogColorPreset SelectedColorPreset
        {
            get => selectedColorPreset;
            set => SetProperty(ref selectedColorPreset, value);
        }

        public WpfClassCatalogListItem SelectedClass
        {
            get => selectedClass;
            set
            {
                if (SetProperty(ref selectedClass, value))
                {
                    if (value != null)
                    {
                        ClassName = value.Text;
                        SelectedColorPreset = FindColorPreset(value.DrawColor) ?? SelectedColorPreset;
                    }

                    NotifyClassCatalogSummaryChanged();
                }
            }
        }

        public void ConfigureCommands(
            Action<KeyInputCommandArgs> classNamePreviewKeyDown,
            Action addClass,
            Action renameClass,
            Action removeClass,
            Action applyClassColor,
            Action<object> classSelectionChanged)
        {
            // Class catalog commands use DTO/value parameters so this ViewModel stays independent from WPF event args.
            ClassNamePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(classNamePreviewKeyDown ?? NoOpKeyCommand);
            AddClassCommand = new RelayCommand(addClass ?? NoOpCommand);
            RenameClassCommand = new RelayCommand(renameClass ?? NoOpCommand);
            RemoveClassCommand = new RelayCommand(removeClass ?? NoOpCommand);
            ApplyClassColorCommand = new RelayCommand(applyClassColor ?? NoOpCommand);
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

            NotifyClassCatalogSummaryChanged();
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

        public WpfClassCatalogColorPreset FindColorPreset(DrawingColor color)
        {
            return ColorPresets.FirstOrDefault(preset => preset.Matches(color));
        }

        private static IEnumerable<WpfClassCatalogColorPreset> BuildDefaultColorPresets()
        {
            yield return new WpfClassCatalogColorPreset("\uC815\uC0C1", DrawingColor.FromArgb(34, 197, 94));
            yield return new WpfClassCatalogColorPreset("\uBD88\uB7C9", DrawingColor.FromArgb(239, 68, 68));
            yield return new WpfClassCatalogColorPreset("\uC8FC\uC758", DrawingColor.FromArgb(245, 158, 11));
            yield return new WpfClassCatalogColorPreset("\uAC80\uD1A0", DrawingColor.FromArgb(59, 130, 246));
            yield return new WpfClassCatalogColorPreset("\uC138\uADF8", DrawingColor.FromArgb(168, 85, 247));
            yield return new WpfClassCatalogColorPreset("\uC774\uBB3C", DrawingColor.FromArgb(20, 184, 166));
        }

        private void NotifyClassCatalogSummaryChanged()
        {
            OnPropertyChanged(nameof(ClassCatalogSummaryText));
            OnPropertyChanged(nameof(CurrentDrawingClassDetailText));
            OnPropertyChanged(nameof(ClassCatalogActionText));
        }
    }

    public sealed class WpfClassCatalogListItem
    {
        public WpfClassCatalogListItem(CClassItem classItem)
        {
            Text = ClassCatalogService.NormalizeClassName(classItem?.Text);
            DrawColor = classItem?.DrawColor ?? DrawingColor.LimeGreen;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(DrawColor.R, DrawColor.G, DrawColor.B));
            brush.Freeze();
            DrawBrush = brush;
        }

        public string Text { get; }

        public string DisplayText => Text;

        public string ToolTip => $"\uD074\uB798\uC2A4: {Text}";

        public DrawingColor DrawColor { get; }

        public MediaBrush DrawBrush { get; }
    }

    public sealed class WpfClassCatalogColorPreset
    {
        public WpfClassCatalogColorPreset(string name, DrawingColor color)
        {
            Name = name ?? string.Empty;
            Color = color;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            Brush = brush;
        }

        public string Name { get; }

        public DrawingColor Color { get; }

        public MediaBrush Brush { get; }

        public bool Matches(DrawingColor color)
            => color.ToArgb() == Color.ToArgb();
    }
}
