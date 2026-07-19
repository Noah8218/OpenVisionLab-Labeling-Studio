using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using DrawingColor = System.Drawing.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MvcVisionSystem
{
    public enum WpfDetectionOverlayStatus
    {
        Confirmable,
        Duplicate,
        Review
    }

    public enum WpfCanvasDisplayMode
    {
        LabelsOnly,
        InferenceOnly,
        Both
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
        private System.Windows.Visibility detectionOverlayActionsVisibility = System.Windows.Visibility.Collapsed;
        private string detectionOverlayTitleText = "\uAC80\uCD9C \uACB0\uACFC";
        private string detectionOverlaySummaryText = string.Empty;
        private string detectionOverlaySelectedText = string.Empty;
        private string detectionOverlayDetailText = string.Empty;
        private string detectionOverlayStatusKey = WpfDetectionOverlayStatus.Confirmable.ToString();
        private string currentWorkflowStepText = "샘플";
        private string currentWorkflowToolText = "선택";
        private string currentWorkflowActionText = "이미지 큐에서 작업할 이미지를 열고 첫 라벨을 시작하세요.";
        private string canvasLayerModeTitleText = "\uC791\uC5C5: \uC800\uC7A5 \uB77C\uBCA8 \uD3B8\uC9D1";
        private string canvasLayerModeDetailText = "AI \uD6C4\uBCF4\uB294 \uC228\uAE40. \uC800\uC7A5\uB41C \uB77C\uBCA8\uB9CC \uC120\uD0DD/\uC218\uC815/\uC800\uC7A5\uD569\uB2C8\uB2E4.";
        private string canvasLayerModeToolTip = "\uD604\uC7AC \uCEA0\uBC84\uC2A4\uAC00 \uC800\uC7A5 \uB77C\uBCA8 \uD3B8\uC9D1\uC778\uC9C0 AI \uD6C4\uBCF4 \uAC80\uD1A0\uC778\uC9C0 \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string canvasLabelLayerText = "\uB77C\uBCA8 0\uAC1C \uD45C\uC2DC";
        private string canvasInferenceLayerText = "AI \uD6C4\uBCF4 0\uAC1C \uC228\uAE40";
        private bool isLabelLayerVisible = true;
        private bool isInferenceLayerVisible;
        private System.Windows.Visibility annotationWorkspaceVisibility = System.Windows.Visibility.Visible;
        private System.Windows.GridLength annotationToolRailWidth = new System.Windows.GridLength(46);
        private WpfAnnotationToolItem selectedAnnotationTool;
        private WpfCanvasLabelClassItem selectedLabelClass;
        private WpfCanvasDisplayModeItem selectedDisplayMode;
        private WpfAnnotationToolItem undoAnnotationTool;
        private WpfAnnotationToolItem redoAnnotationTool;
        private WpfAnnotationToolItem deleteAnnotationTool;
        private bool isAnnotationSaveEnabled;
        private bool isNoObjectCompletionEnabled;
        private string annotationSaveActionText = "\uC800\uC7A5 \uB300\uAE30";
        private string annotationSaveToolTip = "\uC774\uBBF8\uC9C0\uB97C \uBD88\uB7EC\uC624\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string noObjectCompletionActionText = "\uAC1D\uCCB4 \uC5C6\uC74C";
        private string noObjectCompletionToolTip = "\uC774\uBBF8\uC9C0\uB97C \uBD88\uB7EC\uC624\uBA74 \uBC15\uC2A4 \uC5C6\uC774 \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
        private string annotationSaveStatusTitleText = "\uC800\uC7A5 \uB300\uAE30";
        private string annotationSaveStatusDetailText = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uD604\uC7AC \uB77C\uBCA8\uC758 \uD30C\uC77C \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string annotationSaveStatusKey = "Waiting";
        private string activeLabelClassTitleText = "\uB2E4\uC74C \uB77C\uBCA8 \uD074\uB798\uC2A4";
        private string activeLabelClassDetailText = "\uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uBA74 \uB2E4\uC74C\uC5D0 \uADF8\uB9AC\uB294 \uBC15\uC2A4/\uB9C8\uC2A4\uD06C\uC5D0 \uC801\uC6A9\uB429\uB2C8\uB2E4.";
        private string activeLabelClassActionText = "\uD074\uB798\uC2A4 \uAD00\uB9AC";
        private string activeLabelClassActionToolTip = "\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD328\uB110\uC744 \uC5F4\uC5B4 \uC0C8 \uB77C\uBCA8 \uC774\uB984\uC744 \uCD94\uAC00\uD558\uAC70\uB098 \uB2E4\uC74C \uB77C\uBCA8 \uD074\uB798\uC2A4\uB97C \uBC14\uAFC9\uB2C8\uB2E4.";
        private bool isLabelClassSetupMissing = true;
        private int brushSize = 12;
        private string brushSizeText = "12px";
        private System.Windows.Visibility maskBrushControlVisibility = System.Windows.Visibility.Collapsed;
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
        private ICommand labelClassSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand openClassCatalogCommand = new RelayCommand(NoOpCommand);
        private ICommand displayModeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand undoAnnotationCommand = new RelayCommand(NoOpCommand);
        private ICommand redoAnnotationCommand = new RelayCommand(NoOpCommand);
        private ICommand deleteAnnotationCommand = new RelayCommand(NoOpCommand);
        private ICommand saveAnnotationCommand = new RelayCommand(NoOpCommand);
        private ICommand completeNoObjectCommand = new RelayCommand(NoOpCommand);
        private ICommand decreaseBrushSizeCommand = new RelayCommand(NoOpCommand);
        private ICommand increaseBrushSizeCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfCanvasPanel);

        public string FirstLabelLoopText => "\uC21C\uC11C: \uADF8\uB9AC\uAE30 -> \uB77C\uBCA8 \uC800\uC7A5 -> \uB2E4\uC74C \uC774\uBBF8\uC9C0";

        public ObservableCollection<WpfAnnotationToolItem> AnnotationTools { get; } = new ObservableCollection<WpfAnnotationToolItem>();

        public ObservableCollection<WpfCanvasLabelClassItem> LabelClasses { get; } = new ObservableCollection<WpfCanvasLabelClassItem>();

        public ObservableCollection<WpfCanvasDisplayModeItem> DisplayModes { get; } = new ObservableCollection<WpfCanvasDisplayModeItem>
        {
            new WpfCanvasDisplayModeItem(
                WpfCanvasDisplayMode.LabelsOnly,
                "\uB77C\uBCA8 \uD3B8\uC9D1",
                "\uC800\uC7A5\uB41C \uB77C\uBCA8\uB9CC \uBCF4\uBA70 \uBC15\uC2A4/\uB9C8\uC2A4\uD06C\uB97C \uC218\uC815\uD569\uB2C8\uB2E4."),
            new WpfCanvasDisplayModeItem(
                WpfCanvasDisplayMode.InferenceOnly,
                "AI \uAC80\uD1A0",
                "AI \uD6C4\uBCF4\uB9CC \uBCF4\uBA70 \uD655\uC815/\uC2A4\uD0B5\uC744 \uACB0\uC815\uD569\uB2C8\uB2E4."),
            new WpfCanvasDisplayModeItem(
                WpfCanvasDisplayMode.Both,
                "\uBE44\uAD50",
                "\uC800\uC7A5 \uB77C\uBCA8\uACFC AI \uD6C4\uBCF4\uB97C \uACB9\uCCD0 \uBE44\uAD50\uD569\uB2C8\uB2E4.")
        };

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

        public ICommand LabelClassSelectionChangedCommand
        {
            get => labelClassSelectionChangedCommand;
            private set => SetProperty(ref labelClassSelectionChangedCommand, value);
        }

        public ICommand OpenClassCatalogCommand
        {
            get => openClassCatalogCommand;
            private set => SetProperty(ref openClassCatalogCommand, value);
        }

        public ICommand DisplayModeSelectionChangedCommand
        {
            get => displayModeSelectionChangedCommand;
            private set => SetProperty(ref displayModeSelectionChangedCommand, value);
        }

        public WpfAnnotationToolItem SelectedAnnotationTool
        {
            get => selectedAnnotationTool;
            set => SetProperty(ref selectedAnnotationTool, value);
        }

        public WpfCanvasLabelClassItem SelectedLabelClass
        {
            get => selectedLabelClass;
            set
            {
                if (SetProperty(ref selectedLabelClass, value))
                {
                    RefreshActiveLabelClassPresentation();
                }
            }
        }

        public WpfCanvasDisplayModeItem SelectedDisplayMode
        {
            get => selectedDisplayMode;
            set => SetProperty(ref selectedDisplayMode, value);
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

        public ICommand SaveAnnotationCommand
        {
            get => saveAnnotationCommand;
            private set => SetProperty(ref saveAnnotationCommand, value);
        }

        public ICommand CompleteNoObjectCommand
        {
            get => completeNoObjectCommand;
            private set => SetProperty(ref completeNoObjectCommand, value);
        }

        public ICommand DecreaseBrushSizeCommand
        {
            get => decreaseBrushSizeCommand;
            private set => SetProperty(ref decreaseBrushSizeCommand, value);
        }

        public ICommand IncreaseBrushSizeCommand
        {
            get => increaseBrushSizeCommand;
            private set => SetProperty(ref increaseBrushSizeCommand, value);
        }

        public int BrushSize
        {
            get => brushSize;
            private set => SetProperty(ref brushSize, value);
        }

        public string BrushSizeText
        {
            get => brushSizeText;
            private set => SetProperty(ref brushSizeText, value ?? string.Empty);
        }

        public System.Windows.Visibility MaskBrushControlVisibility
        {
            get => maskBrushControlVisibility;
            private set => SetProperty(ref maskBrushControlVisibility, value);
        }

        public bool IsAnnotationSaveEnabled
        {
            get => isAnnotationSaveEnabled;
            private set => SetProperty(ref isAnnotationSaveEnabled, value);
        }

        public bool IsNoObjectCompletionEnabled
        {
            get => isNoObjectCompletionEnabled;
            private set => SetProperty(ref isNoObjectCompletionEnabled, value);
        }

        public string AnnotationSaveActionText
        {
            get => annotationSaveActionText;
            private set => SetProperty(ref annotationSaveActionText, value ?? string.Empty);
        }

        public string AnnotationSaveToolTip
        {
            get => annotationSaveToolTip;
            private set => SetProperty(ref annotationSaveToolTip, value ?? string.Empty);
        }

        public string NoObjectCompletionActionText
        {
            get => noObjectCompletionActionText;
            private set => SetProperty(ref noObjectCompletionActionText, value ?? string.Empty);
        }

        public string NoObjectCompletionToolTip
        {
            get => noObjectCompletionToolTip;
            private set => SetProperty(ref noObjectCompletionToolTip, value ?? string.Empty);
        }

        public string AnnotationSaveStatusTitleText
        {
            get => annotationSaveStatusTitleText;
            private set => SetProperty(ref annotationSaveStatusTitleText, value ?? string.Empty);
        }

        public string AnnotationSaveStatusDetailText
        {
            get => annotationSaveStatusDetailText;
            private set => SetProperty(ref annotationSaveStatusDetailText, value ?? string.Empty);
        }

        public string AnnotationSaveStatusKey
        {
            get => annotationSaveStatusKey;
            private set => SetProperty(ref annotationSaveStatusKey, value ?? "Waiting");
        }

        public string ActiveLabelClassTitleText
        {
            get => activeLabelClassTitleText;
            private set => SetProperty(ref activeLabelClassTitleText, value ?? string.Empty);
        }

        public string ActiveLabelClassDetailText
        {
            get => activeLabelClassDetailText;
            private set => SetProperty(ref activeLabelClassDetailText, value ?? string.Empty);
        }

        public string ActiveLabelClassActionText
        {
            get => activeLabelClassActionText;
            private set => SetProperty(ref activeLabelClassActionText, value ?? string.Empty);
        }

        public string ActiveLabelClassActionToolTip
        {
            get => activeLabelClassActionToolTip;
            private set => SetProperty(ref activeLabelClassActionToolTip, value ?? string.Empty);
        }

        public bool IsLabelClassSetupMissing
        {
            get => isLabelClassSetupMissing;
            private set => SetProperty(ref isLabelClassSetupMissing, value);
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

        public System.Windows.Visibility DetectionOverlayActionsVisibility
        {
            get => detectionOverlayActionsVisibility;
            private set => SetProperty(ref detectionOverlayActionsVisibility, value);
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

        public string CanvasLayerModeTitleText
        {
            get => canvasLayerModeTitleText;
            private set => SetProperty(ref canvasLayerModeTitleText, value ?? string.Empty);
        }

        public string CanvasLayerModeDetailText
        {
            get => canvasLayerModeDetailText;
            private set => SetProperty(ref canvasLayerModeDetailText, value ?? string.Empty);
        }

        public string CanvasLayerModeToolTip
        {
            get => canvasLayerModeToolTip;
            private set => SetProperty(ref canvasLayerModeToolTip, value ?? string.Empty);
        }

        public string CanvasLabelLayerText
        {
            get => canvasLabelLayerText;
            private set => SetProperty(ref canvasLabelLayerText, value ?? string.Empty);
        }

        public string CanvasInferenceLayerText
        {
            get => canvasInferenceLayerText;
            private set => SetProperty(ref canvasInferenceLayerText, value ?? string.Empty);
        }

        public bool IsLabelLayerVisible
        {
            get => isLabelLayerVisible;
            private set => SetProperty(ref isLabelLayerVisible, value);
        }

        public bool IsInferenceLayerVisible
        {
            get => isInferenceLayerVisible;
            private set => SetProperty(ref isInferenceLayerVisible, value);
        }

        public System.Windows.Visibility AnnotationWorkspaceVisibility
        {
            get => annotationWorkspaceVisibility;
            private set => SetProperty(ref annotationWorkspaceVisibility, value);
        }

        public System.Windows.GridLength AnnotationToolRailWidth
        {
            get => annotationToolRailWidth;
            private set => SetProperty(ref annotationToolRailWidth, value);
        }

        public void SetAnomalyImageReviewMode(bool enabled)
        {
            AnnotationWorkspaceVisibility = enabled
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
            AnnotationToolRailWidth = enabled
                ? new System.Windows.GridLength(0)
                : new System.Windows.GridLength(46);
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

        public void ConfigureBrushSizeCommands(Action decreaseBrushSize, Action increaseBrushSize)
        {
            DecreaseBrushSizeCommand = new RelayCommand(decreaseBrushSize ?? NoOpCommand);
            IncreaseBrushSizeCommand = new RelayCommand(increaseBrushSize ?? NoOpCommand);
        }

        public void SetBrushSize(int size)
        {
            int normalized = Math.Clamp(size, 2, 64);
            BrushSize = normalized;
            BrushSizeText = $"{normalized}px";
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

        public void ConfigureLabelClassSelection(Action<object> labelClassSelectionChanged, Action openClassCatalog = null)
        {
            LabelClassSelectionChangedCommand = new RelayCommand<object>(labelClassSelectionChanged ?? NoOpSelectionCommand);
            OpenClassCatalogCommand = new RelayCommand(openClassCatalog ?? NoOpCommand);
        }

        public void ConfigureDisplayModeSelection(Action<object> displayModeSelectionChanged)
        {
            DisplayModeSelectionChangedCommand = new RelayCommand<object>(displayModeSelectionChanged ?? NoOpSelectionCommand);
            if (SelectedDisplayMode == null)
            {
                SetDisplayMode(WpfCanvasDisplayMode.LabelsOnly);
            }
        }

        public void SetDisplayMode(WpfCanvasDisplayMode mode)
        {
            WpfCanvasDisplayModeItem displayMode = DisplayModes.FirstOrDefault(item => item.Mode == mode)
                ?? DisplayModes.FirstOrDefault();
            if (displayMode != null)
            {
                SelectedDisplayMode = displayMode;
            }
        }

        public void SetLayerVisibilityState(
            WpfCanvasDisplayMode mode,
            int labelCount,
            int inferenceCandidateCount,
            bool hasUnsavedLabelChanges)
        {
            int normalizedLabelCount = Math.Max(0, labelCount);
            int normalizedCandidateCount = Math.Max(0, inferenceCandidateCount);
            bool showLabels = mode != WpfCanvasDisplayMode.InferenceOnly;
            bool showInference = mode != WpfCanvasDisplayMode.LabelsOnly;
            IsLabelLayerVisible = showLabels;
            IsInferenceLayerVisible = showInference;

            string unsavedSuffix = hasUnsavedLabelChanges
                ? " / \uC800\uC7A5 \uC804 \uBCC0\uACBD \uC788\uC74C"
                : string.Empty;
            CanvasLabelLayerText = showLabels
                ? $"\uB77C\uBCA8 {normalizedLabelCount}\uAC1C \uD45C\uC2DC{unsavedSuffix}"
                : $"\uB77C\uBCA8 {normalizedLabelCount}\uAC1C \uC228\uAE40{unsavedSuffix}";
            CanvasInferenceLayerText = showInference
                ? $"AI \uD6C4\uBCF4 {normalizedCandidateCount}\uAC1C \uD45C\uC2DC"
                : $"AI \uD6C4\uBCF4 {normalizedCandidateCount}\uAC1C \uC228\uAE40";

            switch (mode)
            {
                case WpfCanvasDisplayMode.InferenceOnly:
                    CanvasLayerModeTitleText = "\uC791\uC5C5: AI \uD6C4\uBCF4 \uAC80\uD1A0";
                    CanvasLayerModeDetailText = "\uC800\uC7A5 \uB77C\uBCA8\uC740 \uC228\uAE40. AI \uD6C4\uBCF4\uB97C \uD655\uC778\uD55C \uB4A4 \uB77C\uBCA8\uB85C \uD655\uC815\uD558\uAC70\uB098 \uC2A4\uD0B5\uD569\uB2C8\uB2E4.";
                    break;

                case WpfCanvasDisplayMode.Both:
                    CanvasLayerModeTitleText = "\uC791\uC5C5: \uB77C\uBCA8+AI \uBE44\uAD50";
                    CanvasLayerModeDetailText = "\uC800\uC7A5 \uB77C\uBCA8\uACFC AI \uD6C4\uBCF4\uB97C \uD568\uAED8 \uBCF4\uBA70 \uACB9\uCE68/\uB204\uB77D\uC744 \uBE44\uAD50\uD569\uB2C8\uB2E4.";
                    break;

                default:
                    CanvasLayerModeTitleText = "\uC791\uC5C5: \uC800\uC7A5 \uB77C\uBCA8 \uD3B8\uC9D1";
                    CanvasLayerModeDetailText = "AI \uD6C4\uBCF4\uB294 \uC228\uAE40. \uC800\uC7A5\uB41C \uB77C\uBCA8\uB9CC \uC120\uD0DD/\uC218\uC815/\uC800\uC7A5\uD569\uB2C8\uB2E4.";
                    break;
            }

            CanvasLayerModeToolTip = $"{CanvasLayerModeDetailText}\n{CanvasLabelLayerText}\n{CanvasInferenceLayerText}";
        }

        public void SetLabelClasses(IEnumerable<CClassItem> classItems, string selectedName = "")
        {
            string normalizedSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            WpfCanvasLabelClassItem selectedItem = null;

            LabelClasses.Clear();
            foreach (CClassItem classItem in (classItems ?? Enumerable.Empty<CClassItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase))
            {
                var labelItem = new WpfCanvasLabelClassItem(classItem);
                LabelClasses.Add(labelItem);
                if (!string.IsNullOrWhiteSpace(normalizedSelectedName)
                    && string.Equals(labelItem.Text, normalizedSelectedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedItem = labelItem;
                }
            }

            SelectedLabelClass = selectedItem ?? LabelClasses.FirstOrDefault();
            IsLabelClassSetupMissing = LabelClasses.Count == 0;
            RefreshActiveLabelClassPresentation();
        }

        public void SelectLabelClass(string className)
        {
            string normalizedName = ClassCatalogService.NormalizeClassName(className);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            WpfCanvasLabelClassItem labelItem = LabelClasses.FirstOrDefault(candidate =>
                string.Equals(candidate.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (labelItem != null)
            {
                SelectedLabelClass = labelItem;
            }
        }

        public void ConfigureAnnotationCommands(Action undo, Action redo, Action delete)
        {
            UndoAnnotationCommand = new RelayCommand(undo ?? NoOpCommand);
            RedoAnnotationCommand = new RelayCommand(redo ?? NoOpCommand);
            DeleteAnnotationCommand = new RelayCommand(delete ?? NoOpCommand);
        }

        public void ConfigureAnnotationSaveCommand(Action save)
        {
            // Save is exposed inside the canvas toolbar because operators decide to persist
            // immediately after drawing; the shell still owns the actual persistence command.
            SaveAnnotationCommand = new RelayCommand(save ?? NoOpCommand);
        }

        public void ConfigureNoObjectCompletionCommand(Action completeNoObject)
        {
            CompleteNoObjectCommand = new RelayCommand(completeNoObject ?? NoOpCommand);
        }

        public void SetNoObjectCompletionState(bool hasImage, bool hasLabelObjects, bool hasPendingCandidates)
        {
            NoObjectCompletionActionText = "\uAC1D\uCCB4 \uC5C6\uC74C";
            if (!hasImage)
            {
                IsNoObjectCompletionEnabled = false;
                NoObjectCompletionToolTip = "\uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC5F4\uBA74 \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                return;
            }

            if (hasLabelObjects)
            {
                IsNoObjectCompletionEnabled = false;
                NoObjectCompletionToolTip = "\uC774\uBBF8 \uB77C\uBCA8\uB41C \uAC1D\uCCB4\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD558\uB824\uBA74 \uAE30\uC874 \uB77C\uBCA8\uC744 \uBA3C\uC800 \uC0AD\uC81C\uD558\uC138\uC694.";
                return;
            }

            if (hasPendingCandidates)
            {
                IsNoObjectCompletionEnabled = false;
                NoObjectCompletionToolTip = "\uB0A8\uC740 AI \uD6C4\uBCF4\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uC228\uAE34 \uB4A4 \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694.";
                return;
            }

            IsNoObjectCompletionEnabled = true;
            NoObjectCompletionToolTip = "\uB77C\uBCA8\uC744 \uB9CC\uB4E4\uC9C0 \uC54A\uACE0 \uBE48 YOLO \uB77C\uBCA8 \uD30C\uC77C\uC744 \uC800\uC7A5\uD55C \uB4A4 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.";
        }

        public void SetAnnotationSaveState(bool isDirty, string actionText, string toolTip)
        {
            IsAnnotationSaveEnabled = isDirty;
            AnnotationSaveActionText = string.IsNullOrWhiteSpace(actionText)
                ? (isDirty ? "\uB77C\uBCA8 \uC800\uC7A5" : "\uC800\uC7A5 \uC644\uB8CC")
                : actionText;
            AnnotationSaveToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uC785\uB2C8\uB2E4."
                : toolTip;
            bool isWaiting = !isDirty
                && AnnotationSaveActionText.Contains("\uB300\uAE30", StringComparison.Ordinal);
            if (isDirty)
            {
                AnnotationSaveStatusKey = "Dirty";
                AnnotationSaveStatusTitleText = "\uC800\uC7A5 \uD544\uC694";
                AnnotationSaveStatusDetailText = "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8 \uD3B8\uC9D1\uC774 \uC544\uC9C1 \uD30C\uC77C\uC5D0 \uBC18\uC601\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            }
            else if (isWaiting)
            {
                AnnotationSaveStatusKey = "Waiting";
                AnnotationSaveStatusTitleText = "\uC774\uBBF8\uC9C0 \uB300\uAE30";
                AnnotationSaveStatusDetailText = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
            }
            else
            {
                AnnotationSaveStatusKey = "Saved";
                AnnotationSaveStatusTitleText = "\uD30C\uC77C \uC800\uC7A5\uB428";
                AnnotationSaveStatusDetailText = "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8\uC774 \uC800\uC7A5 \uD3F4\uB354\uC5D0 \uBC18\uC601\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
            }
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
                RefreshMaskBrushControlVisibility();
            }
        }

        private void RefreshMaskBrushControlVisibility()
        {
            WpfAnnotationTool? tool = SelectedAnnotationTool?.Tool;
            MaskBrushControlVisibility = tool == WpfAnnotationTool.Brush || tool == WpfAnnotationTool.Eraser
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
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

        private void RefreshActiveLabelClassPresentation()
        {
            string className = SelectedLabelClass?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                IsLabelClassSetupMissing = true;
                ActiveLabelClassTitleText = "\uD074\uB798\uC2A4 \uBA3C\uC800 \uB4F1\uB85D";
                ActiveLabelClassDetailText = "\uC624\uB978\uCABD \uD074\uB798\uC2A4\uC5D0\uC11C OK, NG\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uCD94\uAC00\uD55C \uB4A4 \uBC15\uC2A4\uB97C \uADF8\uB9AC\uC138\uC694.";
                ActiveLabelClassActionText = "\uD074\uB798\uC2A4 \uB4F1\uB85D";
                ActiveLabelClassActionToolTip = "\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD328\uB110\uC5D0\uC11C \uB77C\uBCA8 \uC774\uB984\uACFC \uC0C9\uC0C1\uC744 \uAD00\uB9AC\uD569\uB2C8\uB2E4.";
                return;
            }

            IsLabelClassSetupMissing = false;
            ActiveLabelClassTitleText = string.Format("\uB2E4\uC74C \uB77C\uBCA8: {0}", className);
            ActiveLabelClassDetailText = string.Format("\uC0C8\uB85C \uADF8\uB9AC\uB294 \uBC15\uC2A4/\uB9C8\uC2A4\uD06C\uB294 {0} \uD074\uB798\uC2A4\uB85C \uC800\uC7A5\uB429\uB2C8\uB2E4. \uBC14\uAFB8\uB824\uBA74 \uD074\uB798\uC2A4 \uAD00\uB9AC\uB97C \uC5EC\uC138\uC694.", className);
            ActiveLabelClassActionText = "\uD074\uB798\uC2A4 \uAD00\uB9AC";
            ActiveLabelClassActionToolTip = "\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD328\uB110\uC744 \uC5F4\uC5B4 \uC0C8 \uB77C\uBCA8 \uC774\uB984\uC744 \uCD94\uAC00\uD558\uAC70\uB098 \uB2E4\uC74C \uB77C\uBCA8 \uD074\uB798\uC2A4\uB97C \uBC14\uAFC9\uB2C8\uB2E4.";
        }

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
            DetectionOverlayActionsVisibility = System.Windows.Visibility.Collapsed;
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
            DetectionOverlayActionsVisibility = status == WpfDetectionOverlayStatus.Confirmable || status == WpfDetectionOverlayStatus.Duplicate
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
    }

    public sealed class WpfCanvasLabelClassItem
    {
        public WpfCanvasLabelClassItem(CClassItem classItem)
        {
            Text = ClassCatalogService.NormalizeClassName(classItem?.Text);
            DrawColor = classItem?.DrawColor ?? DrawingColor.LimeGreen;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(DrawColor.R, DrawColor.G, DrawColor.B));
            brush.Freeze();
            DrawBrush = brush;
        }

        public string Text { get; }

        public string DisplayText => Text;

        public string ToolTip => $"\uC774 \uB77C\uBCA8\uB85C \uBC15\uC2A4\uB97C \uADF8\uB9BD\uB2C8\uB2E4: {Text}";

        public DrawingColor DrawColor { get; }

        public MediaBrush DrawBrush { get; }
    }

    public sealed class WpfCanvasDisplayModeItem
    {
        public WpfCanvasDisplayModeItem(WpfCanvasDisplayMode mode, string text, string toolTip)
        {
            Mode = mode;
            Text = text ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
        }

        public WpfCanvasDisplayMode Mode { get; }

        public string Text { get; }

        public string DisplayText => Text;

        public string ToolTip { get; }
    }
}
