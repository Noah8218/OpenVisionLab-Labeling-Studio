using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.ComponentModel;
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
        private string newClassName = string.Empty;
        private string outputRootPath = string.Empty;
        private string statusText = T("WpfClassCatalog.Status.Initial");
        private bool hasExplicitStatusText;
        private bool hasClassAddFailure;
        private string statusKey;
        private object[] statusArguments = Array.Empty<object>();
        private WpfClassCatalogListItem selectedClass;
        private ICommand classNamePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand addClassCommand = new RelayCommand(NoOpCommand);
        private ICommand renameClassCommand = new RelayCommand(NoOpCommand);
        private ICommand archiveClassCommand = new RelayCommand(NoOpCommand);
        private ICommand applyClassColorCommand = new RelayCommand(NoOpCommand);
        private ICommand renameClassRowCommand = new RelayCommand<WpfClassCatalogListItem>(_ => { });
        private ICommand archiveClassRowCommand = new RelayCommand<WpfClassCatalogListItem>(_ => { });
        private ICommand applyClassColorRowCommand = new RelayCommand<WpfClassCatalogListItem>(_ => { });
        private ICommand classSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private WpfClassCatalogColorPreset selectedColorPreset;
        private ClassCatalogWorkflowService mutationWorkflowService;
        private Func<LabelingProjectData> mutationDataProvider;
        private Func<string> mutationRecipeNameProvider;
        private Func<bool> mutationCloseApprovedProvider;
        private Action cancelPendingDraft;
        private Action<string> selectCanvasLabelClass;
        private Action<string> refreshObjectClassOptions;

        public WpfClassCatalogPanelViewModel()
        {
            foreach (WpfClassCatalogColorPreset preset in BuildDefaultColorPresets())
            {
                ColorPresets.Add(preset);
            }

            SelectedColorPreset = ColorPresets.FirstOrDefault();
            Classes.CollectionChanged += (_, _) => NotifyClassCatalogSummaryChanged();
        }

        public event Action<WpfClassCatalogMutationKind, WpfClassCatalogMutationResult> MutationCompleted;

        public string ViewName => nameof(WpfClassCatalogPanel);

        public string PanelTitleText => T("WpfClassCatalog.Panel.Title");

        public string ClassSectionLabelText => T("WpfClassCatalog.Section.Classes");

        public string ClassNameToolTipText => T("WpfClassCatalog.ClassName.ToolTip");

        public string AddClassAutomationNameText => T("WpfClassCatalog.Add.Name");

        public string AddClassToolTipText => T("WpfClassCatalog.Add.ToolTip");

        public string AddClassButtonText => T("WpfClassCatalog.Add.Text");

        public string ArchiveClassAutomationNameText => T(SelectedClass?.IsArchived == true
            ? "WpfClassCatalog.Restore.Name"
            : "WpfClassCatalog.Archive.Name");

        public string ArchiveClassToolTipText => T(SelectedClass?.IsArchived == true
            ? "WpfClassCatalog.Restore.ToolTip"
            : "WpfClassCatalog.Archive.ToolTip");

        public string ArchiveClassButtonText => T(SelectedClass?.IsArchived == true
            ? "WpfClassCatalog.Restore.Text"
            : "WpfClassCatalog.Archive.Text");

        public bool IsArchiveClassEnabled => SelectedClass != null
            && (SelectedClass.IsArchived || Classes.Count(item => !item.IsArchived) > 1);

        public bool IsRenameClassEnabled => SelectedClass != null && !SelectedClass.IsArchived;

        public string RenameClassAutomationNameText => T("WpfClassCatalog.Rename.Name");

        public string RenameClassToolTipText => T("WpfClassCatalog.Rename.ToolTip");

        public string RenameClassButtonText => T("WpfClassCatalog.Rename.Text");

        public string RowRenameButtonText => T("WpfClassCatalog.Row.Rename.Text");

        public string RowColorApplyButtonText => T("WpfClassCatalog.Row.ColorApply.Text");

        public string RowColorInputNameText => T("WpfClassCatalog.Row.ColorInput.Name");

        public string RowColorInputHintText => T("WpfClassCatalog.Row.ColorInput.Hint");

        public string ClassColorAutomationNameText => T("WpfClassCatalog.Color.Name");

        public string ClassColorToolTipText => T("WpfClassCatalog.Color.ToolTip");

        public string ApplyClassColorAutomationNameText => T("WpfClassCatalog.Color.Apply.Name");

        public string ApplyClassColorToolTipText => T("WpfClassCatalog.Color.Apply.ToolTip");

        public string ApplyClassColorButtonText => T("WpfClassCatalog.Color.Apply.Text");

        public string ClassCatalogGuideTitleText => T("WpfClassCatalog.Guide.Title");

        public string ClassCatalogGuideDetailText => T("WpfClassCatalog.Guide.Detail");

        public string ClassCatalogSummaryText
        {
            get
            {
                string selected = SelectedClass?.CanonicalDisplayText;
                if (string.IsNullOrWhiteSpace(selected))
                {
                    selected = T("WpfClassCatalog.Selection.None");
                }

                return Format("WpfClassCatalog.Summary", Classes.Count, selected);
            }
        }

        public string CurrentDrawingClassTitleText => T("WpfClassCatalog.Current.Title");

        public string CurrentDrawingClassDetailText
        {
            get
            {
                if (SelectedClass?.IsArchived == true)
                {
                    return T("WpfClassCatalog.Current.ArchivedDetail");
                }

                string selected = SelectedClass?.CanonicalDisplayText;
                return string.IsNullOrWhiteSpace(selected)
                    ? T("WpfClassCatalog.Current.EmptyDetail")
                    : Format("WpfClassCatalog.Current.SelectedDetail", selected);
            }
        }

        public string ClassCatalogActionText
        {
            get
            {
                return Classes.Count <= 0
                    ? T("WpfClassCatalog.Action.Empty")
                    : T("WpfClassCatalog.Action.Populated");
            }
        }

        public string ClassColorSectionTitleText => T("WpfClassCatalog.Color.Section");

        public string RecipeClassListTitleText => T("WpfClassCatalog.Recipe.Title");

        public string RecipeClassListGuideText => T("WpfClassCatalog.Recipe.Guide");

        public string ClassIndexContractText => T("WpfClassCatalog.IndexContract");

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

        public ICommand ArchiveClassCommand
        {
            get => archiveClassCommand;
            private set => SetProperty(ref archiveClassCommand, value);
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

        public ICommand RenameClassRowCommand
        {
            get => renameClassRowCommand;
            private set => SetProperty(ref renameClassRowCommand, value);
        }

        public ICommand ArchiveClassRowCommand
        {
            get => archiveClassRowCommand;
            private set => SetProperty(ref archiveClassRowCommand, value);
        }

        public ICommand ApplyClassColorRowCommand
        {
            get => applyClassColorRowCommand;
            private set => SetProperty(ref applyClassColorRowCommand, value);
        }

        public string ClassName
        {
            get => className;
            set => SetProperty(ref className, value ?? string.Empty);
        }

        public string NewClassName
        {
            get => newClassName;
            set => SetProperty(ref newClassName, value ?? string.Empty);
        }

        public string OutputRootPath
        {
            get => outputRootPath;
            set => SetProperty(ref outputRootPath, value ?? string.Empty);
        }

        public string StatusText
        {
            get => statusKey == null ? statusText : Format(statusKey, statusArguments);
            set
            {
                hasExplicitStatusText = true;
                statusKey = null;
                statusArguments = Array.Empty<object>();
                SetProperty(ref statusText, value ?? string.Empty);
            }
        }

        public bool HasClassAddFailure
        {
            get => hasClassAddFailure;
            private set => SetProperty(ref hasClassAddFailure, value);
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
                        SelectedColorPreset = FindColorPreset(value.DrawColor);
                    }

                    NotifyClassCatalogSummaryChanged();
                }
            }
        }

        public void ConfigureCommands(
            Action<KeyInputCommandArgs> classNamePreviewKeyDown,
            Action addClass,
            Action renameClass,
            Action archiveClass,
            Action applyClassColor,
            Action<object> classSelectionChanged)
        {
            // Class catalog commands use DTO/value parameters so this ViewModel stays independent from WPF event args.
            ClassNamePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(classNamePreviewKeyDown ?? NoOpKeyCommand);
            AddClassCommand = new RelayCommand(addClass ?? NoOpCommand);
            RenameClassCommand = new RelayCommand(renameClass ?? NoOpCommand);
            ArchiveClassCommand = new RelayCommand(archiveClass ?? NoOpCommand);
            ApplyClassColorCommand = new RelayCommand(applyClassColor ?? NoOpCommand);
            ClassSelectionChangedCommand = new RelayCommand<object>(classSelectionChanged ?? NoOpSelectionCommand);
        }

        public void ConfigureClassNamePreviewKeyWorkflow()
        {
            ClassNamePreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(ExecuteClassNamePreviewKeyDown);
        }

        public void ConfigureMutationWorkflow(
            ClassCatalogWorkflowService workflowService,
            Func<LabelingProjectData> dataProvider,
            Func<string> recipeNameProvider,
            Func<bool> closeApprovedProvider)
        {
            mutationWorkflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            mutationDataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            mutationRecipeNameProvider = recipeNameProvider ?? throw new ArgumentNullException(nameof(recipeNameProvider));
            mutationCloseApprovedProvider = closeApprovedProvider ?? throw new ArgumentNullException(nameof(closeApprovedProvider));
            AddClassCommand = new RelayCommand(ExecuteAddClass);
            RenameClassCommand = new RelayCommand(ExecuteRenameClass);
            ArchiveClassCommand = new RelayCommand(ExecuteArchiveClass);
            ApplyClassColorCommand = new RelayCommand(ExecuteApplyClassColor);
            RenameClassRowCommand = new RelayCommand<WpfClassCatalogListItem>(ExecuteRenameClassRow,
                item => item != null && Classes.Contains(item) && !item.IsArchived);
            ArchiveClassRowCommand = new RelayCommand<WpfClassCatalogListItem>(ExecuteArchiveClassRow,
                item => item != null && Classes.Contains(item)
                    && (item.IsArchived || Classes.Count(candidate => !candidate.IsArchived) > 1));
            ApplyClassColorRowCommand = new RelayCommand<WpfClassCatalogListItem>(ExecuteApplyClassColorRow,
                item => item != null && Classes.Contains(item));
        }

        private void ExecuteClassNamePreviewKeyDown(KeyInputCommandArgs args)
        {
            if (args?.Key != Key.Enter)
            {
                return;
            }

            if (!args.IsRepeat) AddClassCommand.Execute(null);
            args.Handled = true;
        }

        public void ConfigureSelectionWorkflow(
            Action cancelPendingFourPointBoxDraft,
            Action<string> selectCanvasClass,
            Action<string> refreshObjectOptions)
        {
            cancelPendingDraft = cancelPendingFourPointBoxDraft ?? (() => { });
            selectCanvasLabelClass = selectCanvasClass ?? (_ => { });
            refreshObjectClassOptions = refreshObjectOptions ?? (_ => { });
            ClassSelectionChangedCommand = new RelayCommand<object>(ExecuteClassSelectionChanged);
        }

        public void LoadOutputRoot(string path)
        {
            OutputRootPath = path ?? string.Empty;
        }

        public void SetClasses(IEnumerable<LabelClass> classItems, string selectedName = "")
        {
            string normalizedSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            WpfClassCatalogListItem selectedItem = null;

            SelectedClass = null;
            Classes.Clear();

            int canonicalIndex = 0;
            foreach (LabelClass classItem in classItems ?? Array.Empty<LabelClass>())
            {
                int currentIndex = canonicalIndex++;
                if (classItem == null || string.IsNullOrWhiteSpace(classItem.Text))
                {
                    continue;
                }

                var listItem = new WpfClassCatalogListItem(classItem, currentIndex);
                listItem.SelectedColorPreset = FindColorPreset(listItem.DrawColor);
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
            CommandManager.InvalidateRequerySuggested();
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

        public void ClearNewClassName()
        {
            NewClassName = string.Empty;
        }

        public void PrepareClassAddDialog()
        {
            NewClassName = string.Empty;
            HasClassAddFailure = false;
            SetLocalizedStatus("WpfClassCatalog.Status.Initial");
        }

        public void RefreshLocalizedPresentation()
        {
            if (!hasExplicitStatusText)
            {
                statusText = T("WpfClassCatalog.Status.Initial");
            }

            foreach (WpfClassCatalogColorPreset preset in ColorPresets)
            {
                preset.RefreshLocalizedPresentation();
            }

            foreach (WpfClassCatalogListItem item in Classes)
            {
                item.RefreshLocalizedPresentation();
            }

            OnPropertyChanged(string.Empty);
        }

        public WpfClassCatalogColorPreset FindColorPreset(DrawingColor color)
        {
            return ColorPresets.FirstOrDefault(preset => preset.Matches(color));
        }

        private static IEnumerable<WpfClassCatalogColorPreset> BuildDefaultColorPresets()
        {
            yield return new WpfClassCatalogColorPreset("WpfClassCatalog.ColorPreset.Normal", DrawingColor.FromArgb(34, 197, 94));
            yield return new WpfClassCatalogColorPreset("WpfClassCatalog.ColorPreset.Defect", DrawingColor.FromArgb(239, 68, 68));
            yield return new WpfClassCatalogColorPreset("WpfClassCatalog.ColorPreset.Warning", DrawingColor.FromArgb(245, 158, 11));
            yield return new WpfClassCatalogColorPreset("WpfClassCatalog.ColorPreset.Review", DrawingColor.FromArgb(59, 130, 246));
            yield return new WpfClassCatalogColorPreset("WpfClassCatalog.ColorPreset.Segmentation", DrawingColor.FromArgb(168, 85, 247));
            yield return new WpfClassCatalogColorPreset("WpfClassCatalog.ColorPreset.ForeignMaterial", DrawingColor.FromArgb(20, 184, 166));
        }

        private void NotifyClassCatalogSummaryChanged()
        {
            OnPropertyChanged(nameof(ClassCatalogSummaryText));
            OnPropertyChanged(nameof(CurrentDrawingClassDetailText));
            OnPropertyChanged(nameof(ClassCatalogActionText));
            OnPropertyChanged(nameof(ArchiveClassAutomationNameText));
            OnPropertyChanged(nameof(ArchiveClassToolTipText));
            OnPropertyChanged(nameof(ArchiveClassButtonText));
            OnPropertyChanged(nameof(IsArchiveClassEnabled));
            OnPropertyChanged(nameof(IsRenameClassEnabled));
        }

        private void ExecuteAddClass()
        {
            if (IsMutationUnavailable())
            {
                return;
            }

            WpfClassCatalogMutationResult result = mutationWorkflowService.Add(
                mutationDataProvider(),
                mutationRecipeNameProvider(),
                NewClassName);
            HasClassAddFailure = !result.IsSuccess;
            MutationCompleted?.Invoke(WpfClassCatalogMutationKind.Add, result);
            SetMutationStatus(
                WpfClassCatalogMutationKind.Add,
                result,
                "WpfClassCatalog.Status.Added", result.ClassItem?.Text);
        }

        private void ExecuteClassSelectionChanged(object selectedItem)
        {
            cancelPendingDraft?.Invoke();
            WpfClassCatalogListItem selectedClass = selectedItem as WpfClassCatalogListItem;
            string className = selectedClass?.Text ?? SelectedClass?.Text;
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            if (selectedClass != null && !ReferenceEquals(SelectedClass, selectedClass))
            {
                SelectedClass = selectedClass;
            }
            else
            {
                ClassName = className;
            }

            if (selectedClass?.IsArchived == true)
            {
                SetLocalizedStatus("WpfClassCatalog.Status.ArchivedSelection", className);
                return;
            }

            selectCanvasLabelClass?.Invoke(className);
            refreshObjectClassOptions?.Invoke(className);
        }

        private void ExecuteRenameClass()
        {
            if (IsMutationUnavailable())
            {
                return;
            }

            string currentName = SelectedClass?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentName))
            {
                SetLocalizedStatus("WpfClassCatalog.Status.SelectClass");
                return;
            }

            WpfClassCatalogMutationResult result = mutationWorkflowService.Rename(
                mutationDataProvider(),
                mutationRecipeNameProvider(),
                currentName,
                ClassName);
            MutationCompleted?.Invoke(WpfClassCatalogMutationKind.Rename, result);
            SetMutationStatus(
                WpfClassCatalogMutationKind.Rename,
                result,
                "WpfClassCatalog.Status.Renamed", currentName, result.ClassItem?.Text);
        }

        private void ExecuteRenameClassRow(WpfClassCatalogListItem item)
        {
            if (item == null || !Classes.Contains(item))
            {
                return;
            }

            SelectedClass = item;
            ClassName = item.EditName;
            ExecuteRenameClass();
        }

        private void ExecuteArchiveClass()
        {
            if (IsMutationUnavailable())
            {
                return;
            }

            string className = SelectedClass?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                SetLocalizedStatus("WpfClassCatalog.Status.SelectClass");
                return;
            }

            WpfClassCatalogMutationResult result = mutationWorkflowService.ToggleArchive(
                mutationDataProvider(),
                mutationRecipeNameProvider(),
                className);
            MutationCompleted?.Invoke(WpfClassCatalogMutationKind.ToggleArchive, result);
            SetMutationStatus(
                WpfClassCatalogMutationKind.ToggleArchive,
                result,
                result.WasArchived ? "WpfClassCatalog.Status.Restored" : "WpfClassCatalog.Status.Archived", className);
        }

        private void ExecuteArchiveClassRow(WpfClassCatalogListItem item)
        {
            if (item == null || !Classes.Contains(item))
            {
                return;
            }

            SelectedClass = item;
            ExecuteArchiveClass();
        }

        private void ExecuteApplyClassColor()
        {
            if (IsMutationUnavailable())
            {
                return;
            }

            string className = SelectedClass?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                SetLocalizedStatus("WpfClassCatalog.Status.SelectClass");
                return;
            }

            DrawingColor color = SelectedColorPreset?.Color ?? DrawingColor.LimeGreen;
            WpfClassCatalogMutationResult result = mutationWorkflowService.SetColor(
                mutationDataProvider(),
                mutationRecipeNameProvider(),
                className,
                color);
            MutationCompleted?.Invoke(WpfClassCatalogMutationKind.SetColor, result);
            SetMutationStatus(
                WpfClassCatalogMutationKind.SetColor,
                result,
                "WpfClassCatalog.Status.ColorChanged", result.ClassItem?.Text);
        }

        private void ExecuteApplyClassColorRow(WpfClassCatalogListItem item)
        {
            if (item == null || !Classes.Contains(item))
            {
                return;
            }

            string hex = item.ColorHex?.Trim() ?? string.Empty;
            if (hex.Length != 7 || hex[0] != '#'
                || !int.TryParse(hex.Substring(1), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out int rgb))
            {
                SetLocalizedStatus("WpfClassCatalog.Color.InvalidHex");
                return;
            }

            SelectedClass = item;
            if (IsMutationUnavailable()) return;
            WpfClassCatalogMutationResult result = mutationWorkflowService.SetColor(
                mutationDataProvider(), mutationRecipeNameProvider(), item.Text,
                DrawingColor.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff));
            MutationCompleted?.Invoke(WpfClassCatalogMutationKind.SetColor, result);
            SetMutationStatus(WpfClassCatalogMutationKind.SetColor, result,
                "WpfClassCatalog.Status.ColorChanged", result.ClassItem?.Text);
        }

        private bool IsMutationUnavailable()
            => mutationWorkflowService == null
                || mutationDataProvider == null
                || mutationRecipeNameProvider == null
                || mutationCloseApprovedProvider?.Invoke() == true;

        private void SetMutationStatus(WpfClassCatalogMutationKind kind, WpfClassCatalogMutationResult result, string successKey, params object[] successArguments)
        {
            if (result?.IsSuccess == true)
            {
                SetLocalizedStatus(successKey, successArguments);
                return;
            }

            string failureKey = kind switch
            {
                WpfClassCatalogMutationKind.ToggleArchive when result?.Failure == WpfClassCatalogOperationFailure.LastActiveClass
                    => "WpfClassCatalog.Status.LastActiveClass",
                WpfClassCatalogMutationKind.ToggleArchive when result?.Failure == WpfClassCatalogOperationFailure.ClassNotFound
                    && mutationDataProvider()?.ClassNamedList?.Any(item =>
                        string.Equals(item?.Text, result.ClassName, StringComparison.OrdinalIgnoreCase) && item.IsArchived) == true
                    => "WpfClassCatalog.Status.RestoreNotFound",
                WpfClassCatalogMutationKind.ToggleArchive when result?.Failure == WpfClassCatalogOperationFailure.ClassNotFound
                    => "WpfClassCatalog.Status.ClassNotFound",
                WpfClassCatalogMutationKind.SetColor when result?.Failure == WpfClassCatalogOperationFailure.ClassNotFound
                    => "WpfClassCatalog.Status.ColorClassNotFound",
                _ when result?.Failure == WpfClassCatalogOperationFailure.InvalidClassName
                    => "WpfClassCatalog.Status.EnterName",
                _ when result?.Failure == WpfClassCatalogOperationFailure.ArchivedClass
                    => "WpfClassCatalog.Status.RestoreBeforeRename",
                _ when result?.Failure == WpfClassCatalogOperationFailure.InvalidOutputRoot
                    => "WpfClassCatalog.Status.EnterOutputRoot",
                _ when result?.Failure == WpfClassCatalogOperationFailure.Persistence
                    => "WpfClassCatalog.Status.SaveFailed",
                _ => "WpfClassCatalog.Status.NameUnavailable"
            };
            SetLocalizedStatus(failureKey, result?.Failure == WpfClassCatalogOperationFailure.Persistence ? result.ErrorMessage : result?.ClassName);
        }

        private void SetLocalizedStatus(string key, params object[] arguments)
        {
            StatusText = Format(key, arguments);
            statusKey = key;
            statusArguments = arguments;
        }
        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }
    }

    public sealed class WpfClassCatalogListItem : INotifyPropertyChanged
    {
        private string editName;
        private string colorHex;
        private WpfClassCatalogColorPreset selectedColorPreset;

        public WpfClassCatalogListItem(LabelClass classItem, int canonicalIndex = 0)
        {
            Text = ClassCatalogService.NormalizeClassName(classItem?.Text);
            editName = Text;
            CanonicalIndex = Math.Max(0, canonicalIndex);
            IsArchived = classItem?.IsArchived == true;
            DrawColor = classItem?.DrawColor ?? DrawingColor.LimeGreen;
            colorHex = $"#{DrawColor.R:X2}{DrawColor.G:X2}{DrawColor.B:X2}";
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(DrawColor.R, DrawColor.G, DrawColor.B));
            brush.Freeze();
            DrawBrush = brush;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Text { get; }

        public string EditName
        {
            get => editName;
            set
            {
                if (string.Equals(editName, value, StringComparison.Ordinal))
                {
                    return;
                }

                editName = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditName)));
            }
        }

        public WpfClassCatalogColorPreset SelectedColorPreset
        {
            get => selectedColorPreset;
            set
            {
                if (ReferenceEquals(selectedColorPreset, value))
                {
                    return;
                }

                selectedColorPreset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedColorPreset)));
                if (value != null) ColorHex = $"#{value.Color.R:X2}{value.Color.G:X2}{value.Color.B:X2}";
            }
        }

        public string ColorHex
        {
            get => colorHex;
            set
            {
                if (string.Equals(colorHex, value, StringComparison.Ordinal)) return;
                colorHex = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorHex)));
            }
        }

        public string RowArchiveButtonText => OpenVisionLanguageService.T(IsArchived
            ? "WpfClassCatalog.Row.Restore.Text"
            : "WpfClassCatalog.Row.Archive.Text");

        public int CanonicalIndex { get; }

        public bool IsArchived { get; }

        public string CanonicalDisplayText => $"{CanonicalIndex} \u00B7 {Text}";

        public string DisplayText => IsArchived
            ? $"{CanonicalDisplayText} \u00B7 {OpenVisionLanguageService.T("WpfClassCatalog.Item.Archived")}"
            : CanonicalDisplayText;

        public string ToolTip => string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            OpenVisionLanguageService.T(IsArchived
                ? "WpfClassCatalog.Item.ArchivedToolTip"
                : "WpfClassCatalog.Item.ToolTip"),
            CanonicalIndex,
            Text);

        public DrawingColor DrawColor { get; }

        public MediaBrush DrawBrush { get; }

        internal void RefreshLocalizedPresentation()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTip)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowArchiveButtonText)));
        }
    }

    public sealed class WpfClassCatalogColorPreset
    {
        public WpfClassCatalogColorPreset(string nameKey, DrawingColor color)
        {
            NameKey = nameKey ?? string.Empty;
            Color = color;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            Brush = brush;
        }

        public string NameKey { get; }

        public string Name => OpenVisionLanguageService.T(NameKey);

        public DrawingColor Color { get; }

        public MediaBrush Brush { get; }

        public bool Matches(DrawingColor color)
            => color.ToArgb() == Color.ToArgb();

        internal void RefreshLocalizedPresentation()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
