using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public enum WpfDetectionOverlayStatus
    {
        Confirmable,
        Duplicate,
        Review
    }

    public sealed class WpfCanvasPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private bool isFitEnabled;
        private bool isActualSizeEnabled;
        private bool isPanEnabled;
        private bool isFocusCandidateEnabled;
        private bool isResetAiOverlayEnabled;
        private bool isPreviousCandidateEnabled;
        private bool isNextCandidateEnabled;
        private bool isFocusCurrentLabelEnabled;
        private bool isConfirmSelectedEnabled;
        private bool isSkipSelectedEnabled;
        private System.Windows.Visibility detectionOverlayVisibility = System.Windows.Visibility.Collapsed;
        private string detectionOverlayTitleText = "\uAC80\uCD9C \uACB0\uACFC";
        private string detectionOverlaySummaryText = string.Empty;
        private string detectionOverlaySelectedText = string.Empty;
        private string detectionOverlayDetailText = string.Empty;
        private string detectionOverlayStatusKey = WpfDetectionOverlayStatus.Confirmable.ToString();
        private string currentWorkflowStepText = "샘플";
        private string currentWorkflowToolText = "선택";
        private string currentWorkflowActionText = "이미지를 열거나 왼쪽 큐에서 선택하세요.";
        private WpfAnnotationToolItem selectedAnnotationTool;
        private WpfAnnotationToolItem undoAnnotationTool;
        private WpfAnnotationToolItem redoAnnotationTool;
        private WpfAnnotationToolItem deleteAnnotationTool;
        private ICommand fitCommand = new RelayCommand(NoOpCommand);
        private ICommand actualSizeCommand = new RelayCommand(NoOpCommand);
        private ICommand panCommand = new RelayCommand(NoOpCommand);
        private ICommand focusCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand resetAiOverlayCommand = new RelayCommand(NoOpCommand);
        private ICommand previousCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand nextCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand focusCurrentLabelCommand = new RelayCommand(NoOpCommand);
        private ICommand confirmSelectedCommand = new RelayCommand(NoOpCommand);
        private ICommand skipSelectedCommand = new RelayCommand(NoOpCommand);
        private ICommand annotationToolSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand undoAnnotationCommand = new RelayCommand(NoOpCommand);
        private ICommand redoAnnotationCommand = new RelayCommand(NoOpCommand);
        private ICommand deleteAnnotationCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfCanvasPanel);

        public ObservableCollection<WpfAnnotationToolItem> AnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ICommand FitCommand
        {
            get => fitCommand;
            private set => SetProperty(ref fitCommand, value);
        }

        public ICommand ActualSizeCommand
        {
            get => actualSizeCommand;
            private set => SetProperty(ref actualSizeCommand, value);
        }

        public ICommand PanCommand
        {
            get => panCommand;
            private set => SetProperty(ref panCommand, value);
        }

        public ICommand FocusCandidateCommand
        {
            get => focusCandidateCommand;
            private set => SetProperty(ref focusCandidateCommand, value);
        }

        public ICommand ResetAiOverlayCommand
        {
            get => resetAiOverlayCommand;
            private set => SetProperty(ref resetAiOverlayCommand, value);
        }

        public ICommand PreviousCandidateCommand
        {
            get => previousCandidateCommand;
            private set => SetProperty(ref previousCandidateCommand, value);
        }

        public ICommand NextCandidateCommand
        {
            get => nextCandidateCommand;
            private set => SetProperty(ref nextCandidateCommand, value);
        }

        public ICommand FocusCurrentLabelCommand
        {
            get => focusCurrentLabelCommand;
            private set => SetProperty(ref focusCurrentLabelCommand, value);
        }

        public ICommand ConfirmSelectedCommand
        {
            get => confirmSelectedCommand;
            private set => SetProperty(ref confirmSelectedCommand, value);
        }

        public ICommand SkipSelectedCommand
        {
            get => skipSelectedCommand;
            private set => SetProperty(ref skipSelectedCommand, value);
        }

        public ICommand AnnotationToolSelectionChangedCommand
        {
            get => annotationToolSelectionChangedCommand;
            private set => SetProperty(ref annotationToolSelectionChangedCommand, value);
        }

        public WpfAnnotationToolItem SelectedAnnotationTool
        {
            get => selectedAnnotationTool;
            set => SetProperty(ref selectedAnnotationTool, value);
        }

        public WpfAnnotationToolItem UndoAnnotationTool
        {
            get => undoAnnotationTool;
            private set => SetProperty(ref undoAnnotationTool, value);
        }

        public WpfAnnotationToolItem RedoAnnotationTool
        {
            get => redoAnnotationTool;
            private set => SetProperty(ref redoAnnotationTool, value);
        }

        public WpfAnnotationToolItem DeleteAnnotationTool
        {
            get => deleteAnnotationTool;
            private set => SetProperty(ref deleteAnnotationTool, value);
        }

        public ICommand UndoAnnotationCommand
        {
            get => undoAnnotationCommand;
            private set => SetProperty(ref undoAnnotationCommand, value);
        }

        public ICommand RedoAnnotationCommand
        {
            get => redoAnnotationCommand;
            private set => SetProperty(ref redoAnnotationCommand, value);
        }

        public ICommand DeleteAnnotationCommand
        {
            get => deleteAnnotationCommand;
            private set => SetProperty(ref deleteAnnotationCommand, value);
        }

        public bool IsFitEnabled
        {
            get => isFitEnabled;
            private set => SetProperty(ref isFitEnabled, value);
        }

        public bool IsActualSizeEnabled
        {
            get => isActualSizeEnabled;
            private set => SetProperty(ref isActualSizeEnabled, value);
        }

        public bool IsPanEnabled
        {
            get => isPanEnabled;
            private set => SetProperty(ref isPanEnabled, value);
        }

        public bool IsFocusCandidateEnabled
        {
            get => isFocusCandidateEnabled;
            private set => SetProperty(ref isFocusCandidateEnabled, value);
        }

        public bool IsResetAiOverlayEnabled
        {
            get => isResetAiOverlayEnabled;
            private set => SetProperty(ref isResetAiOverlayEnabled, value);
        }

        public bool IsPreviousCandidateEnabled
        {
            get => isPreviousCandidateEnabled;
            private set => SetProperty(ref isPreviousCandidateEnabled, value);
        }

        public bool IsNextCandidateEnabled
        {
            get => isNextCandidateEnabled;
            private set => SetProperty(ref isNextCandidateEnabled, value);
        }

        public bool IsFocusCurrentLabelEnabled
        {
            get => isFocusCurrentLabelEnabled;
            private set => SetProperty(ref isFocusCurrentLabelEnabled, value);
        }

        public bool IsConfirmSelectedEnabled
        {
            get => isConfirmSelectedEnabled;
            private set => SetProperty(ref isConfirmSelectedEnabled, value);
        }

        public bool IsSkipSelectedEnabled
        {
            get => isSkipSelectedEnabled;
            private set => SetProperty(ref isSkipSelectedEnabled, value);
        }

        public System.Windows.Visibility DetectionOverlayVisibility
        {
            get => detectionOverlayVisibility;
            private set => SetProperty(ref detectionOverlayVisibility, value);
        }

        public string DetectionOverlayTitleText
        {
            get => detectionOverlayTitleText;
            private set => SetProperty(ref detectionOverlayTitleText, value ?? string.Empty);
        }

        public string DetectionOverlaySummaryText
        {
            get => detectionOverlaySummaryText;
            private set => SetProperty(ref detectionOverlaySummaryText, value ?? string.Empty);
        }

        public string DetectionOverlaySelectedText
        {
            get => detectionOverlaySelectedText;
            private set => SetProperty(ref detectionOverlaySelectedText, value ?? string.Empty);
        }

        public string DetectionOverlayDetailText
        {
            get => detectionOverlayDetailText;
            private set => SetProperty(ref detectionOverlayDetailText, value ?? string.Empty);
        }

        public string DetectionOverlayStatusKey
        {
            get => detectionOverlayStatusKey;
            private set => SetProperty(ref detectionOverlayStatusKey, value ?? WpfDetectionOverlayStatus.Confirmable.ToString());
        }

        public string CurrentWorkflowStepText
        {
            get => currentWorkflowStepText;
            private set => SetProperty(ref currentWorkflowStepText, value ?? string.Empty);
        }

        public string CurrentWorkflowToolText
        {
            get => currentWorkflowToolText;
            private set => SetProperty(ref currentWorkflowToolText, value ?? string.Empty);
        }

        public string CurrentWorkflowActionText
        {
            get => currentWorkflowActionText;
            private set => SetProperty(ref currentWorkflowActionText, value ?? string.Empty);
        }

        public void ConfigureCommands(
            Action fit,
            Action actualSize,
            Action pan,
            Action focusCandidate,
            Action resetAiOverlay)
        {
            // Shell actions stay injected at the ViewModel boundary so the panel view only declares bindings.
            FitCommand = new RelayCommand(fit ?? NoOpCommand);
            ActualSizeCommand = new RelayCommand(actualSize ?? NoOpCommand);
            PanCommand = new RelayCommand(pan ?? NoOpCommand);
            FocusCandidateCommand = new RelayCommand(focusCandidate ?? NoOpCommand);
            ResetAiOverlayCommand = new RelayCommand(resetAiOverlay ?? NoOpCommand);
        }

        public void ConfigureCandidateReviewCommands(
            Action previousCandidate,
            Action nextCandidate,
            Action focusCurrentLabel,
            Action confirmSelected,
            Action skipSelected)
        {
            // The canvas result card mirrors Candidate Review commands so first-time users
            // can act where the inference result appears instead of hunting the right panel.
            PreviousCandidateCommand = new RelayCommand(previousCandidate ?? NoOpCommand);
            NextCandidateCommand = new RelayCommand(nextCandidate ?? NoOpCommand);
            FocusCurrentLabelCommand = new RelayCommand(focusCurrentLabel ?? NoOpCommand);
            ConfirmSelectedCommand = new RelayCommand(confirmSelected ?? NoOpCommand);
            SkipSelectedCommand = new RelayCommand(skipSelected ?? NoOpCommand);
        }

        public void ConfigureAnnotationTools(
            IEnumerable<WpfAnnotationToolItem> tools,
            WpfAnnotationToolItem selectedTool,
            Action<object> annotationToolSelectionChanged)
        {
            // The canvas toolbar mirrors the guide palette but keeps one-shot commands out of the selected-tool list.
            AnnotationTools.Clear();
            UndoAnnotationTool = null;
            RedoAnnotationTool = null;
            DeleteAnnotationTool = null;
            foreach (WpfAnnotationToolItem tool in tools ?? Enumerable.Empty<WpfAnnotationToolItem>())
            {
                if (TryAssignCommandTool(tool))
                {
                    continue;
                }

                AnnotationTools.Add(tool);
            }

            SetSelectedAnnotationTool(selectedTool ?? AnnotationTools.FirstOrDefault());
            AnnotationToolSelectionChangedCommand = new RelayCommand<object>(annotationToolSelectionChanged ?? NoOpSelectionCommand);
        }

        public void ConfigureAnnotationCommands(Action undo, Action redo, Action delete)
        {
            UndoAnnotationCommand = new RelayCommand(undo ?? NoOpCommand);
            RedoAnnotationCommand = new RelayCommand(redo ?? NoOpCommand);
            DeleteAnnotationCommand = new RelayCommand(delete ?? NoOpCommand);
        }

        public void SetSelectedAnnotationTool(WpfAnnotationToolItem selectedTool)
        {
            if (selectedTool == null || IsOneShotCommandTool(selectedTool.Tool))
            {
                return;
            }

            if (AnnotationTools.Contains(selectedTool))
            {
                SelectedAnnotationTool = selectedTool;
            }
        }

        public void SetWorkflowContext(string stepText, string toolText, string actionText)
        {
            // Keep this small status strip as ViewModel state so the canvas view does not
            // need to reach into the guide panel or shell to explain the current workflow.
            CurrentWorkflowStepText = string.IsNullOrWhiteSpace(stepText) ? "단계" : stepText;
            CurrentWorkflowToolText = string.IsNullOrWhiteSpace(toolText) ? "선택" : toolText;
            CurrentWorkflowActionText = string.IsNullOrWhiteSpace(actionText) ? "다음 작업을 선택하세요." : actionText;
        }

        private bool TryAssignCommandTool(WpfAnnotationToolItem tool)
        {
            if (tool == null)
            {
                return false;
            }

            switch (tool.Tool)
            {
                case WpfAnnotationTool.Undo:
                    UndoAnnotationTool = tool;
                    return true;

                case WpfAnnotationTool.Redo:
                    RedoAnnotationTool = tool;
                    return true;

                case WpfAnnotationTool.Delete:
                    DeleteAnnotationTool = tool;
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsOneShotCommandTool(WpfAnnotationTool tool)
            => tool == WpfAnnotationTool.Undo
                || tool == WpfAnnotationTool.Redo
                || tool == WpfAnnotationTool.Delete;

        public void SetCommandAvailability(bool hasImage, bool hasSelectedCandidate, bool hasPendingCandidates)
        {
            IsFitEnabled = hasImage;
            IsActualSizeEnabled = hasImage;
            IsPanEnabled = hasImage;
            IsFocusCandidateEnabled = hasImage && hasSelectedCandidate;
            IsResetAiOverlayEnabled = hasImage && hasPendingCandidates;
        }

        public void SetCandidateReviewState(
            bool canNavigatePrevious,
            bool canNavigateNext,
            bool canFocusCurrentLabel,
            bool canConfirmSelected,
            bool canSkipSelected)
        {
            IsPreviousCandidateEnabled = canNavigatePrevious;
            IsNextCandidateEnabled = canNavigateNext;
            IsFocusCurrentLabelEnabled = canFocusCurrentLabel;
            IsConfirmSelectedEnabled = canConfirmSelected;
            IsSkipSelectedEnabled = canSkipSelected;
        }

        public void ClearDetectionOverlay()
        {
            DetectionOverlayVisibility = System.Windows.Visibility.Collapsed;
            DetectionOverlaySummaryText = string.Empty;
            DetectionOverlaySelectedText = string.Empty;
            DetectionOverlayDetailText = string.Empty;
            DetectionOverlayStatusKey = WpfDetectionOverlayStatus.Confirmable.ToString();
        }

        public void SetDetectionOverlay(
            string title,
            string summary,
            string selected,
            string detail,
            WpfDetectionOverlayStatus status)
        {
            DetectionOverlayVisibility = System.Windows.Visibility.Visible;
            DetectionOverlayTitleText = string.IsNullOrWhiteSpace(title) ? "\uAC80\uCD9C \uACB0\uACFC" : title;
            DetectionOverlaySummaryText = summary;
            DetectionOverlaySelectedText = selected;
            DetectionOverlayDetailText = detail;
            DetectionOverlayStatusKey = status.ToString();
        }
    }
}
