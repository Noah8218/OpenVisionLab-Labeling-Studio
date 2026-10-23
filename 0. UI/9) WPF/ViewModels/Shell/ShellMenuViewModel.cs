using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace MvcVisionSystem
{
    // Menu presentation and admission only. Existing commands retain all mutations and persistence.
    public sealed class ShellMenuViewModel : WpfObservableViewModel
    {
        private readonly WpfLabelingShellViewModels views;
        private Func<bool> isBusy = () => false;
        private Func<bool> hasDraft = () => false;
        private Func<bool> hasHistoryDraft = () => false;
        private Func<bool> hasPendingCandidates = () => false;
        private Func<bool> hasRecipe = () => false;
        private Func<bool> canBrowseCapturedTraining = () => false;
        private Action duplicate;
        private ICommand decisionRulesCommand;
        private ICommand labelSuspicionCommand;
        private bool executing;
        private bool isCommandPaletteOpen;
        private bool isShortcutHelpOpen;
        private bool isPreferencesOpen;
        private bool isEnvironmentOpen;
        private bool isRecipeImportOpen;
        private readonly List<ShellMenuEntry> commandEntries = new List<ShellMenuEntry>();
        private IReadOnlyList<ShellMenuEntry> baseCommandEntries;
        private Func<IEnumerable<WpfImageQueueItem>> paletteImages = () => Array.Empty<WpfImageQueueItem>();
        private Func<WpfImageQueueItem, bool> openPaletteImage = _ => false;
        private string commandQuery = string.Empty;
        private ShellMenuEntry selectedCommand;

        public ShellMenuViewModel(WpfLabelingShellViewModels views)
        {
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            Groups = new[]
            {
                Group("File", "New", "Open", "OpenDatasetFolder", "Save", "CompleteLabeling", "CancelLabeling", "Close", "Import", "Export", "Exchange"),
                Group("Edit", "Undo", "Redo", "Duplicate", "Delete"),
                Group("View", "Panels", "ResetLayout", "Log"),
                Group("Settings", "Preferences", "Environment", "Locations", "Model", "Classes"),
                Group("Help", "Guide", "Shortcuts", "Diagnostics", "Support")
            };
            OperationSaveEntries = Entries("Save", "Open", "Close");
            OperationEnvironmentEntries = Entries("Environment", "Locations", "Model");
            OperationExchangeEntries = Entries("Import", "Export", "Exchange");
            OperationImageEntries = new[] { "LoadImageRoot", "BrowseImageFolder", "RefreshImageQueue", "LoadSample" }
                .Select(id => new ShellMenuEntry(id, () => GetReason(id), () => Execute(id))).ToArray();
            RefreshCommand = new RelayCommand(Refresh);
            HomeCommand = new RelayCommand(() => Execute("StageHome"), () => GetReason("StageHome").Length == 0);
            baseCommandEntries = Groups.SelectMany(group => group.Children).Concat(OperationImageEntries)
                .Concat(new[] { "StageDataset", "StageLabeling", "StageTraining", "StageComparison", "StageInference", "DecisionRules", "CandidateBatch", "LabelSuspicion" }.Select(id => new ShellMenuEntry(id, () => GetReason(id), () => Execute(id))))
                .Select(entry => new ShellMenuEntry(entry.Id, () => GetReason(entry.Id, fromPalette: true), () => ExecutePaletteEntry(entry.Id))).ToArray();
            commandEntries.AddRange(baseCommandEntries);
            CommandView = new ListCollectionView(commandEntries) { Filter = item => MatchesQuery((ShellMenuEntry)item) };
            OpenCommandPaletteCommand = new RelayCommand(OpenCommandPalette, () => !IsCommandPaletteOpen && !IsShellDialogOpen && !executing && !views.ModelComparisonWorkspaceViewModel.IsAdoptionConfirmationOpen);
            CloseCommandPaletteCommand = new RelayCommand(CloseCommandPalette);
            ExecuteSelectedCommand = new RelayCommand(() => { if (SelectedCommand?.Command?.CanExecute(null) == true) SelectedCommand.Command.Execute(null); }, () => IsCommandPaletteOpen && SelectedCommand?.Command?.CanExecute(null) == true);
            NextCommand = new RelayCommand(() => MoveSelection(1));
            PreviousCommand = new RelayCommand(() => MoveSelection(-1));
            SelectRecipeArchiveCommand = new RelayCommand(() =>
            {
                if (!IsRecipeImportOpen || isBusy() || hasDraft() || hasPendingCandidates() || executing || !views.ProjectConfigViewModel.IsProjectArchiveCommandEnabled) return;
                CloseShellDialogCommand.Execute(null);
                if (GetReason("Import").Length != 0) return;
                ICommand import = views.ProjectConfigViewModel.ImportProjectArchiveCommand;
                if (import.CanExecute(null)) import.Execute(null);
            }, () => IsRecipeImportOpen && !isBusy() && !hasDraft() && !hasPendingCandidates() && !executing && views.ProjectConfigViewModel.IsProjectArchiveCommandEnabled);
            CloseShellDialogCommand = new RelayCommand(() => { IsShortcutHelpOpen = false; IsPreferencesOpen = false; IsEnvironmentOpen = false; IsRecipeImportOpen = false; Refresh(); });
            OpenEnvironmentSetupCommand = new RelayCommand(() =>
            {
                if (!views.RuntimeDiagnosticsViewModel.OpenSetupCenterCommand.CanExecute(null)) return;
                CloseShellDialogCommand.Execute(null);
                Execute("Locations");
            }, () => !isBusy() && !hasDraft() && views.RuntimeDiagnosticsViewModel.OpenSetupCenterCommand.CanExecute(null));
        }

        public IReadOnlyList<ShellMenuEntry> Groups { get; }
        public IReadOnlyList<ShellMenuEntry> OperationSaveEntries { get; }
        public IReadOnlyList<ShellMenuEntry> OperationEnvironmentEntries { get; }
        public IReadOnlyList<ShellMenuEntry> OperationExchangeEntries { get; }
        public IReadOnlyList<ShellMenuEntry> OperationImageEntries { get; }
        public ShellMenuEntry ImportEntry => Groups.SelectMany(group => group.Children).Single(entry => entry.Id == "Import");
        public ShellMenuEntry SaveEntry => Groups.SelectMany(group => group.Children).Single(entry => entry.Id == "Save");
        public ShellMenuEntry EnvironmentEntry => Groups.SelectMany(group => group.Children).Single(entry => entry.Id == "Environment");
        public ShellMenuEntry ShortcutsEntry => Groups.SelectMany(group => group.Children).Single(entry => entry.Id == "Shortcuts");
        public ShellMenuEntry PreferencesEntry => Groups.SelectMany(group => group.Children).Single(entry => entry.Id == "Preferences");
        public ICommand RefreshCommand { get; }
        public ICommand HomeCommand { get; }
        public IReadOnlyList<ShellMenuEntry> CommandEntries => commandEntries;
        public System.ComponentModel.ICollectionView CommandView { get; }
        public ICommand OpenCommandPaletteCommand { get; }
        public ICommand CloseCommandPaletteCommand { get; }
        public ICommand ExecuteSelectedCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public bool IsCommandPaletteOpen { get => isCommandPaletteOpen; private set { if (SetProperty(ref isCommandPaletteOpen, value)) CommandManager.InvalidateRequerySuggested(); } }
        public string CommandQuery { get => commandQuery; set { if (SetProperty(ref commandQuery, value ?? string.Empty)) { CommandView.Refresh(); SelectFirstCommand(); } } }
        public ShellMenuEntry SelectedCommand { get => selectedCommand; set { if (SetProperty(ref selectedCommand, value)) CommandManager.InvalidateRequerySuggested(); } }
        public bool HasMatchingCommands => !CommandView.IsEmpty;
        public bool IsShortcutHelpOpen { get => isShortcutHelpOpen; private set { if (SetProperty(ref isShortcutHelpOpen, value)) NotifyDialogState(); } }
        public bool IsPreferencesOpen { get => isPreferencesOpen; private set { if (SetProperty(ref isPreferencesOpen, value)) NotifyDialogState(); } }
        public bool IsEnvironmentOpen { get => isEnvironmentOpen; private set { if (SetProperty(ref isEnvironmentOpen, value)) NotifyDialogState(); } }
        public bool IsRecipeImportOpen { get => isRecipeImportOpen; private set { if (SetProperty(ref isRecipeImportOpen, value)) NotifyDialogState(); } }
        public bool IsShellDialogOpen => IsShortcutHelpOpen || IsPreferencesOpen || IsEnvironmentOpen || IsRecipeImportOpen;
        public bool HasActiveRecipe => hasRecipe();
        public bool CanChangePreferences => !isBusy() && !hasDraft() && !executing;
        public string EnvironmentSummaryText => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + " · " + (Environment.Is64BitProcess ? "x64" : "x86");
        public ICommand CloseShellDialogCommand { get; }
        public ICommand OpenEnvironmentSetupCommand { get; }
        public ICommand SelectRecipeArchiveCommand { get; }
        public IReadOnlyList<ShellShortcutGroup> ShortcutGroups => ShellShortcutGroup.CreateSupportedGroups();

        public void ConfigurePaletteImages(Func<IEnumerable<WpfImageQueueItem>> images, Func<WpfImageQueueItem, bool> open)
        {
            paletteImages = images ?? throw new ArgumentNullException(nameof(images));
            openPaletteImage = open ?? throw new ArgumentNullException(nameof(open));
        }

        private void NotifyDialogState() { OnPropertyChanged(nameof(IsShellDialogOpen)); CommandManager.InvalidateRequerySuggested(); }

        public void Configure(Func<bool> busy, Func<bool> draft, Func<bool> pendingCandidates, Func<bool> recipe, Action duplicateSelected, Func<bool> canBrowseTraining = null, Func<bool> historyDraft = null)
        {
            isBusy = busy ?? throw new ArgumentNullException(nameof(busy));
            hasDraft = draft ?? throw new ArgumentNullException(nameof(draft));
            hasHistoryDraft = historyDraft ?? hasDraft;
            hasPendingCandidates = pendingCandidates ?? throw new ArgumentNullException(nameof(pendingCandidates));
            hasRecipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            duplicate = duplicateSelected ?? throw new ArgumentNullException(nameof(duplicateSelected));
            canBrowseCapturedTraining = canBrowseTraining ?? (() => false);
            Refresh();
        }

        public void Refresh()
        {
            foreach (ShellMenuEntry group in Groups)
            {
                group.Refresh();
                foreach (ShellMenuEntry item in group.Children) item.Refresh();
            }
            foreach (ShellMenuEntry entry in OperationImageEntries) entry.Refresh();
            foreach (ShellMenuEntry entry in CommandEntries) entry.Refresh();
            CommandView.Refresh();
            if (!CommandView.Cast<ShellMenuEntry>().Contains(SelectedCommand)) SelectFirstCommand();
            OnPropertyChanged(nameof(HasActiveRecipe));
            OnPropertyChanged(nameof(CanChangePreferences));
            OnPropertyChanged(nameof(ShortcutGroups));
            CommandManager.InvalidateRequerySuggested();
        }

        internal void ConfigureReviewCommands(ICommand decisionRules, ICommand labelSuspicion)
        {
            decisionRulesCommand = decisionRules;
            labelSuspicionCommand = labelSuspicion;
            Refresh();
        }

        internal string GetReason(string id, bool fromPalette = false)
        {
            if (executing) return Text("Busy");
            if (IsCommandPaletteOpen && !fromPalette) return Text("PaletteOpen");
            if (IsShellDialogOpen) return Text("ConfirmationOpen");
            if (views.ModelComparisonWorkspaceViewModel.IsAdoptionConfirmationOpen) return Text("ConfirmationOpen");
            if (id == "Guide" || id == "Log" || id == "ResetLayout") return string.Empty;
            if (id == "Panels") return views.ShellViewModel.IsRightWorkflowDockToggleVisible ? string.Empty : Text("Unavailable");
            if (id == "Shortcuts" || id == "Preferences") return string.Empty;
            if (id is "StageDataset" or "StageTraining" or "StageComparison" or "StageInference" && hasRecipe() && !hasDraft() && canBrowseCapturedTraining()) return string.Empty;
            if (id is "CompleteLabeling" or "CancelLabeling")
            {
                if (!views.ShellViewModel.IsLabelingStageActive || views.ShellViewModel.IsRecipeLibraryVisible) return Text("LabelingOnly");
                ICommand action = id == "CompleteLabeling" ? views.ShellViewModel.CompleteLabelingCommand : views.ShellViewModel.CancelLabelingCommand;
                return action?.CanExecute(null) == true ? string.Empty : Text("Unavailable");
            }
            if (isBusy()) return Text("Busy");
            if ((id is "Undo" or "Redo" ? hasHistoryDraft : hasDraft)()) return Text("Draft");
            if (id is "DecisionRules" or "CandidateBatch" or "LabelSuspicion")
                return !hasRecipe() ? Text("NoRecipe") : GetImageCommand(id)?.CanExecute(null) == true ? string.Empty : Text("Unavailable");
            if (id == "StageHome") return hasPendingCandidates() ? Text("Candidates") : string.Empty;
            if (id == "Close")
            {
                if (!hasRecipe()) return Text("NoRecipe");
                return hasPendingCandidates() ? Text("Candidates") : string.Empty;
            }
            if ((id == "New" || id == "Open") && hasPendingCandidates()) return Text("Candidates");
            if (id == "New") return views.LearningWorkflowViewModel.DatasetSetupStartCommand.CanExecute(null) ? string.Empty : Text("Unavailable");
            if (id == "Open") return views.ShellViewModel.ChangeDatasetCommand.CanExecute(null) ? string.Empty : Text("Unavailable");
            if (id == "Environment" || id == "Locations" || id == "Diagnostics" || id == "Support")
                return views.RuntimeDiagnosticsViewModel.IsBusy ? Text("Busy") : string.Empty;
            if (id == "Import") return views.ProjectConfigViewModel.IsProjectArchiveCommandEnabled ? string.Empty : Text("Unavailable");
            if (!hasRecipe()) return Text("NoRecipe");
            if (GetImageCommand(id) is ICommand imageCommand) return imageCommand.CanExecute(null) ? string.Empty : Text("Unavailable");
            if (id == "OpenDatasetFolder") return views.ShellViewModel.IsOpenDatasetFolderEnabled ? string.Empty : Text("Unavailable");
            if (id == "Save")
            {
                if (views.ShellViewModel.IsInferenceStageActive) return Text("CandidateSave");
                if (views.ShellViewModel.IsTrainingModelStageActive) return Text("TrainingSaveBoundary");
                if (views.ShellViewModel.IsModelComparisonStageActive) return Text("ComparisonSaveBoundary");
                if (views.ShellViewModel.IsLabelingStageActive)
                    return views.CanvasPanelViewModel.IsAnnotationSaveEnabled ? string.Empty : Text("NoChanges");
                return views.ProjectConfigViewModel.IsSaveProjectConfigEnabled ? string.Empty : Text("Unavailable");
            }
            if (id == "Undo" || id == "Redo" || id == "Duplicate" || id == "Delete")
            {
                if (!views.ShellViewModel.IsLabelingStageActive) return Text("LabelingOnly");
                if (id == "Undo") return views.CanvasPanelViewModel.UndoAnnotationTool?.IsActionEnabled == true ? string.Empty : Text("NoUndo");
                if (id == "Redo") return views.CanvasPanelViewModel.RedoAnnotationTool?.IsActionEnabled == true ? string.Empty : Text("NoRedo");
                if (!views.ObjectReviewViewModel.IsDeleteEnabled) return Text("NoEditableSelection");
                if (id == "Duplicate" && views.ObjectReviewViewModel.SelectedObject?.SourceKey != WpfObjectReviewSource.ManualRoi.ToString()
                    && views.ObjectReviewViewModel.SelectedObject?.SourceKey != WpfObjectReviewSource.ManualSegment.ToString())
                    return Text("ManualSelection");
            }
            if ((id == "Import" || id == "Export") && !views.ProjectConfigViewModel.IsProjectArchiveCommandEnabled)
                return Text("Unavailable");
            return string.Empty;
        }

        internal void Execute(string id)
        {
            // RelayCommand.Execute does not check CanExecute; recheck at the mutation boundary.
            if (GetReason(id).Length != 0) return;
            executing = true;
            try
            {
                ICommand command = id switch
                {
                    "New" => views.LearningWorkflowViewModel.DatasetSetupStartCommand,
                    "Open" => views.ShellViewModel.ChangeDatasetCommand,
                    "StageHome" => views.ShellViewModel.ChangeDatasetCommand,
                    "Close" => views.ShellViewModel.CloseRecipeCommand,
                    "CompleteLabeling" => views.ShellViewModel.CompleteLabelingCommand,
                    "CancelLabeling" => views.ShellViewModel.CancelLabelingCommand,
                    "OpenDatasetFolder" => views.ShellViewModel.OpenDatasetFolderCommand,
                    "Save" => views.ShellViewModel.IsLabelingStageActive ? views.CanvasPanelViewModel.SaveAnnotationCommand : views.ProjectConfigViewModel.SaveProjectConfigCommand,
                    "Import" => views.ProjectConfigViewModel.ImportProjectArchiveCommand,
                    "Export" => views.ProjectConfigViewModel.ExportProjectArchiveCommand,
                    "Exchange" => views.ShellViewModel.OpenDatasetInterchangeCommand,
                    "Undo" => views.CanvasPanelViewModel.UndoAnnotationCommand,
                    "Redo" => views.CanvasPanelViewModel.RedoAnnotationCommand,
                    "Delete" => views.CanvasPanelViewModel.DeleteAnnotationCommand,
                    "Panels" => views.ShellViewModel.ToggleRightWorkflowDockCommand,
                    "ResetLayout" => views.ShellViewModel.ResetWorkspaceLayoutCommand,
                    "Log" => views.ShellLogViewModel.ToggleLogPaneCommand,
                    "Environment" or "Locations" => views.RuntimeDiagnosticsViewModel.OpenSetupCenterCommand,
                    "Model" => views.ShellViewModel.TrainingModelCenterCommand,
                    "StageDataset" => views.ShellViewModel.DatasetHomeCommand,
                    "StageLabeling" => views.ShellViewModel.LabelingWorkbenchCommand,
                    "StageTraining" => views.ShellViewModel.TrainingModelCenterCommand,
                    "StageComparison" => views.ShellViewModel.ModelComparisonCommand,
                    "StageInference" => views.ShellViewModel.InferenceReviewCommand,
                    "Classes" => views.ShellViewModel.ShowClassCatalogViewCommand,
                    "Guide" => views.LearningWorkflowViewModel.TutorialOpenHtmlGuideCommand,
                    "Diagnostics" => views.RuntimeDiagnosticsViewModel.RunSelfTestCommand,
                    "Support" => views.RuntimeDiagnosticsViewModel.CreateSupportBundleCommand,
                    _ => GetImageCommand(id)
                };
                if (id == "Duplicate") duplicate?.Invoke();
                else if (id == "Import" && views.ShellViewModel.IsRecipeLibraryVisible) IsRecipeImportOpen = true;
                else if (id == "Shortcuts") IsShortcutHelpOpen = true;
                else if (id == "Preferences") IsPreferencesOpen = true;
                else if (id == "Environment" && views.ShellViewModel.IsRecipeLibraryVisible) IsEnvironmentOpen = true;
                else if (command?.CanExecute(null) == true)
                {
                    if (id is "Open" or "Close" or "Import" or "Export" or "Exchange" or "Environment" or "Locations" or "Model" || GetImageCommand(id) != null)
                        views.ShellViewModel.CloseClassCatalogDialogCommand.Execute(null);
                    command.Execute(null);
                }
            }
            finally
            {
                executing = false;
                Refresh();
            }
        }

        private ICommand GetImageCommand(string id) => id switch
        {
            "LoadImageRoot" => views.ImageQueueViewModel.LoadImageRootCommand,
            "BrowseImageFolder" => views.ImageQueueViewModel.BrowseImageFolderCommand,
            "RefreshImageQueue" => views.ImageQueueViewModel.RefreshImageQueueCommand,
            "LoadSample" => views.ShellViewModel.LoadSampleCommand,
            "DecisionRules" => decisionRulesCommand,
            "CandidateBatch" => views.ImageQueueViewModel.GenerateCandidatesCommand,
            "LabelSuspicion" => labelSuspicionCommand,
            _ => null
        };

        private void OpenCommandPalette()
        {
            if (!OpenCommandPaletteCommand.CanExecute(null)) return;
            CommandQuery = string.Empty;
            RebuildPaletteTargets();
            Refresh();
            SelectFirstCommand();
            IsCommandPaletteOpen = true;
        }

        private void CloseCommandPalette() => IsCommandPaletteOpen = false;
        private void ExecutePaletteEntry(string id)
        {
            if (!IsCommandPaletteOpen || GetReason(id, fromPalette: true).Length != 0) return;
            CloseCommandPalette();
            Execute(id);
        }
        private bool MatchesQuery(ShellMenuEntry entry) => string.IsNullOrWhiteSpace(CommandQuery)
            || entry.Caption.Contains(CommandQuery.Trim(), StringComparison.CurrentCultureIgnoreCase)
            || entry.Id.Contains(CommandQuery.Trim(), StringComparison.OrdinalIgnoreCase)
            || entry.Shortcut.Contains(CommandQuery.Trim(), StringComparison.CurrentCultureIgnoreCase);

        private void RebuildPaletteTargets()
        {
            commandEntries.Clear();
            commandEntries.AddRange(baseCommandEntries);
            foreach (WpfDatasetSelectionItem recipe in views.DatasetSelectionViewModel.Datasets)
                commandEntries.Add(PaletteTarget("Recipe:" + recipe.RecipeName, "Open", () => FormatTarget("Recipe", recipe.RecipeName), () =>
                {
                    views.DatasetSelectionViewModel.ResetFiltersCommand.Execute(null);
                    views.DatasetSelectionViewModel.SelectedDataset = recipe;
                    views.DatasetSelectionViewModel.OpenCommand.Execute(null);
                }, recipe.ProductCode));
            if (!hasRecipe()) return;
            foreach (WpfImageQueueItem item in paletteImages())
                commandEntries.Add(PaletteTarget("Image:" + item.ImagePath, "StageLabeling", () => FormatTarget("Image", item.FileName), () => openPaletteImage(item), string.Empty));
            foreach (WpfClassCatalogListItem item in views.ClassCatalogViewModel.Classes.Where(item => !item.IsArchived))
                commandEntries.Add(PaletteTarget("Class:" + item.Text, "Classes", () => FormatTarget("Class", item.Text), () =>
                {
                    views.ShellViewModel.ShowClassCatalogViewCommand.Execute(null);
                    views.ClassCatalogViewModel.ClassSelectionChangedCommand.Execute(item);
                }, string.Empty));
        }

        private ShellMenuEntry PaletteTarget(string id, string admissionId, Func<string> caption, Action action, string detail)
        {
            string Reason() => hasPendingCandidates() ? Text("Candidates") : GetReason(admissionId, fromPalette: true);
            return new ShellMenuEntry(id, Reason, () =>
            {
                if (!IsCommandPaletteOpen || Reason().Length != 0) return;
                CloseCommandPalette();
                executing = true;
                try { action(); }
                finally { executing = false; Refresh(); }
            }, caption: caption, shortcut: detail);
        }

        private static string FormatTarget(string kind, string name) => string.Format(OpenVisionLanguageService.T("ShellMenu.Target." + kind), name);
        private void SelectFirstCommand() { SelectedCommand = CommandView.Cast<ShellMenuEntry>().FirstOrDefault(); OnPropertyChanged(nameof(HasMatchingCommands)); }
        private void MoveSelection(int delta)
        {
            if (!IsCommandPaletteOpen) return;
            ShellMenuEntry[] entries = CommandView.Cast<ShellMenuEntry>().ToArray();
            if (entries.Length > 0) SelectedCommand = entries[(Math.Max(0, Array.IndexOf(entries, SelectedCommand)) + delta + entries.Length) % entries.Length];
        }

        private ShellMenuEntry Group(string id, params string[] children)
            => new ShellMenuEntry(id, null, null, children.Select(child => new ShellMenuEntry(child, () => GetReason(child), () => Execute(child))).ToArray());

        private IReadOnlyList<ShellMenuEntry> Entries(params string[] ids)
            => ids.Select(id => Groups.SelectMany(group => group.Children).Single(entry => entry.Id == id)).ToArray();

        private static string Text(string key) => OpenVisionLanguageService.T("ShellMenu.Reason." + key);
    }

    public sealed class ShellMenuEntry : WpfObservableViewModel
    {
        private readonly Func<string> reason;
        private readonly Func<string> caption;
        private readonly string shortcut;

        internal ShellMenuEntry(string id, Func<string> reason, Action execute, IReadOnlyList<ShellMenuEntry> children = null, Func<string> caption = null, string shortcut = null)
        {
            Id = id;
            this.reason = reason;
            this.caption = caption;
            this.shortcut = shortcut;
            Children = children ?? Array.Empty<ShellMenuEntry>();
            Command = execute == null ? null : new RelayCommand(execute, () => Reason.Length == 0);
        }

        public string Id { get; }
        public string Caption => caption?.Invoke() ?? OpenVisionLanguageService.T(Id switch
        {
            "LoadImageRoot" => "WpfImageQueue.LoadConfigured.ToolTip",
            "BrowseImageFolder" => "WpfImageQueue.Browse.ToolTip",
            "RefreshImageQueue" => "WpfImageQueue.Refresh.ToolTip",
            "LoadSample" => "WpfImageQueue.Sample.Load.Text",
            "CompleteLabeling" => "WpfLabeling.Footer.Finish",
            "CancelLabeling" => "WpfLabeling.Footer.Cancel",
            "StageDataset" => "WpfShell.Stage.Dataset.Text",
            "StageLabeling" => "WpfShell.Stage.Labeling.Text",
            "StageTraining" => "WpfShell.Stage.Training.Text",
            "StageComparison" => "WpfShell.Stage.Comparison.Text",
            "StageInference" => "WpfShell.Stage.Inference.Text",
            _ => "ShellMenu." + Id
        });
        public string Reason => reason?.Invoke() ?? string.Empty;
        public string Shortcut => shortcut ?? (Id switch { "Save" => "Ctrl+S", "Undo" => "Ctrl+Z", "Redo" => "Ctrl+Y", "Duplicate" => "Ctrl+D", "Delete" => "Delete", "Guide" => "F1", "Shortcuts" => "?", _ => string.Empty });
        public IReadOnlyList<ShellMenuEntry> Children { get; }
        public ICommand Command { get; }
        internal void Refresh() => OnPropertyChanged(string.Empty);
    }

    public sealed class ShellShortcutGroup
    {
        public ShellShortcutGroup(string key, params ShellShortcutRow[] rows) { Key = key; Rows = rows; }
        public string Key { get; }
        public string Title => OpenVisionLanguageService.T("ShellKeys.Group." + Key);
        public IReadOnlyList<ShellShortcutRow> Rows { get; }
        public int Row => Key is "Ai" or "Anomaly" or "Global" ? 1 : 0;
        public int Column => Key is "Objects" or "Anomaly" ? 1 : Key is "SaveMove" or "Global" ? 2 : 0;

        internal static IReadOnlyList<ShellShortcutGroup> CreateSupportedGroups() => new[]
        {
            new ShellShortcutGroup("Tools", new("Select", "V"), new("Box", "R"), new("Polygon", "P"), new("BrushEraser", "B / E"), new("SmartMask", "S"), new("Pan", "H / Space"), new("BrushSize", "[ / ]")),
            new ShellShortcutGroup("Objects", new("Class", "1–9"), new("Objects", "Tab / Shift+Tab"), new("Delete", "Delete"), new("CopyPaste", "Ctrl+C / Ctrl+V"), new("ImportPrevious", "Ctrl+Shift+V"), new("Duplicate", "Ctrl+D"), new("NoObject", "X")),
            new ShellShortcutGroup("SaveMove", new("Save", "Ctrl+S"), new("SaveNext", "Ctrl+Enter"), new("Adjacent", "A / D · ← / →"), new("UndoRedo", "Ctrl+Z / Ctrl+Y"), new("ConfirmCancel", "Enter / Esc"), new("Fit", "F"), new("Original", "O")),
            new ShellShortcutGroup("Ai", new("Infer", "Ctrl+I"), new("ConfirmCandidate", "Enter"), new("ConfirmAll", "Shift+Enter")),
            new ShellShortcutGroup("Anomaly", new("Normal", "1"), new("Abnormal", "2"), new("AnomalyCandidate", "Enter"), new("Grid", "G")),
            new ShellShortcutGroup("Global", new("Palette", "Ctrl+K"), new("Stages", "Alt+1–5"), new("Help", "?"))
        };
    }

    public sealed class ShellShortcutRow
    {
        public ShellShortcutRow(string key, string shortcut) { Key = key; Shortcut = shortcut; }
        public string Key { get; }
        public string Caption => OpenVisionLanguageService.T("ShellKeys." + Key);
        public string Shortcut { get; }
    }
}
