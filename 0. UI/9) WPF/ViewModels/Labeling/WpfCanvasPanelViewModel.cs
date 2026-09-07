using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
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

    public sealed class WpfSmartMaskDetailItem
    {
        public WpfSmartMaskDetailItem(WpfSmartMaskPolygonDetail detail, string text)
        {
            Detail = detail;
            Text = text ?? string.Empty;
        }

        public WpfSmartMaskPolygonDetail Detail { get; }
        public string Text { get; }
    }

    public sealed class WpfBoxDrawingMethodItem
    {
        public WpfBoxDrawingMethodItem(LabelingBoxDrawingMethod method, string text, string toolTip)
        {
            Method = method;
            Text = text ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
        }

        public LabelingBoxDrawingMethod Method { get; }
        public string Text { get; }
        public string ToolTip { get; }
    }

    public sealed class WpfCanvasPanelViewModel : WpfObservableViewModel, IDisposable
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private bool isFitEnabled;
        private bool isActualSizeEnabled;
        private bool isPanEnabled;
        private bool isFocusCandidateEnabled;
        private bool isResetAiOverlayEnabled;
        private bool isDisplayAdjustmentEnabled;
        private bool isDisplayAdjustmentOpen;
        private int displayBrightness;
        private double displayContrastPercent = 100D;
        private double displayGamma = 1D;
        private bool isDisplayInverted;
        private bool isDisplayHistogramEqualized;
        private bool suppressDisplayAdjustmentNotification;
        private Action displayAdjustmentChanged = NoOpCommand;
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
        private string currentWorkflowActionText = OpenVisionLanguageService.T("WpfCanvas.Workflow.NoImageAction");
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
        private WpfCanvasDisplayMode layerDisplayMode = WpfCanvasDisplayMode.LabelsOnly;
        private int layerLabelCount;
        private int layerInferenceCandidateCount;
        private bool layerHasUnsavedLabelChanges;
        private string currentWorkflowStepSource = string.Empty;
        private string currentWorkflowToolSource = string.Empty;
        private string currentWorkflowActionSource = string.Empty;
        private bool isLabelClassSetupMissing = true;
        private int brushSize = 12;
        private string brushSizeText = "12px";
        private System.Windows.Visibility maskBrushControlVisibility = System.Windows.Visibility.Collapsed;
        private ICommand fitCommand = new RelayCommand(NoOpCommand);
        private ICommand actualSizeCommand = new RelayCommand(NoOpCommand);
        private ICommand panCommand = new RelayCommand(NoOpCommand);
        private ICommand focusCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand resetAiOverlayCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleDisplayAdjustmentCommand;
        private ICommand resetDisplayAdjustmentCommand;
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
        private ICommand createSmartMaskCommand = new RelayCommand(NoOpCommand);
        private ICommand addPositiveSmartMaskPointCommand = new RelayCommand(NoOpCommand);
        private ICommand addNegativeSmartMaskPointCommand = new RelayCommand(NoOpCommand);
        private ICommand undoSmartMaskPointCommand = new RelayCommand(NoOpCommand);
        private ICommand clearSmartMaskPointsCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelSmartMaskGenerationCommand = new RelayCommand(NoOpCommand);
        private ICommand nextSmartMaskInstanceCommand = new RelayCommand(NoOpCommand);
        private ICommand showInitialSmartMaskCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand showLatestSmartMaskCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleSmartMaskAutoContourCommand;
        private ICommand boxDrawingMethodSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand resetBoxDrawingMethodCommand;
        private ICommand toggleSmartMaskCorrectionOptionsCommand;
        private ICommand toggleShortcutHelpCommand;
        private System.Windows.Visibility smartMaskVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility smartMaskSessionActionVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility smartMaskSessionVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility smartMaskCorrectionOptionsVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility smartMaskCandidateComparisonVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility shortcutHelpVisibility = System.Windows.Visibility.Collapsed;
        private WpfAnnotationTool? lastDrawingTool;
        private string lastLabelClassName = string.Empty;
        private bool isSmartMaskEnabled;
        private bool isSmartMaskAutoContourEnabled;
        private bool isSmartMaskAutoContourToggleEnabled;
        private WpfBoxDrawingMethodItem selectedBoxDrawingMethod;
        private System.Windows.Visibility boxDrawingMethodVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility fourPointBoxProgressVisibility = System.Windows.Visibility.Collapsed;
        private string fourPointBoxProgressText = "4\uC810 \uADF9\uC810 \u00B7 \uC704 0/4";
        private Action<LabelingBoxDrawingMethod> boxDrawingMethodChanged = _ => { };
        private bool isRestoringBoxDrawingMethod;
        private string smartMaskActionText = "박스 → 스마트 마스크";
        private string smartMaskToolTip = "결함 둘레에 박스를 그린 뒤 MobileSAM 후보 마스크를 만듭니다.";
        private string smartMaskPromptSummaryText = "박스를 그려 첫 후보를 만드세요.";
        private string smartMaskCandidateComparisonText = string.Empty;
        private bool isSmartMaskPointActionEnabled;
        private bool isSmartMaskPointUndoEnabled;
        private bool isSmartMaskCancelEnabled;
        private bool isSmartMaskNextInstanceEnabled;
        private bool isShowInitialSmartMaskCandidateEnabled;
        private bool isShowLatestSmartMaskCandidateEnabled;
        private bool isSmartMaskCorrectionOptionsExpanded;
        private bool isPositiveSmartMaskPointMode;
        private bool isNegativeSmartMaskPointMode;
        private WpfSmartMaskDetailItem selectedSmartMaskDetail;
        private Action<bool> smartMaskAutoContourChanged = _ => { };
        private Action<WpfSmartMaskPolygonDetail> smartMaskDetailChanged = _ => { };
        private bool disposed;

        public WpfCanvasPanelViewModel()
        {
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

        public string ViewName => nameof(WpfCanvasPanel);

        public string FirstLabelLoopText => T("WpfCanvas.FirstLabelLoop");

        public string ShortcutSummaryText => TranslateExact(AnnotationProductivityService.ShortcutSummaryText);

        public string ShortcutHelpText => TranslateExact(AnnotationProductivityService.ShortcutHelpText);

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

        public ObservableCollection<WpfSmartMaskDetailItem> SmartMaskDetails { get; } = new ObservableCollection<WpfSmartMaskDetailItem>
        {
            new WpfSmartMaskDetailItem(WpfSmartMaskPolygonDetail.Fast, "빠름 · 48점"),
            new WpfSmartMaskDetailItem(WpfSmartMaskPolygonDetail.Balanced, "균형 · 96점"),
            new WpfSmartMaskDetailItem(WpfSmartMaskPolygonDetail.Detailed, "정밀 · 256점")
        };

        public ObservableCollection<WpfBoxDrawingMethodItem> BoxDrawingMethods { get; } =
            new ObservableCollection<WpfBoxDrawingMethodItem>
            {
                new WpfBoxDrawingMethodItem(
                    LabelingBoxDrawingMethod.TwoPointDrag,
                    "2\uC810 \uB4DC\uB798\uADF8",
                    "\uC2DC\uC791\uC810\uC5D0\uC11C \uB05D\uC810\uAE4C\uC9C0 \uB4DC\uB798\uADF8\uD574 \uBC15\uC2A4\uB97C \uB9CC\uB4ED\uB2C8\uB2E4."),
                new WpfBoxDrawingMethodItem(
                    LabelingBoxDrawingMethod.FourPointExtreme,
                    "4\uC810 \uADF9\uC810",
                    "\uC704, \uC544\uB798, \uC67C\uCABD, \uC624\uB978\uCABD \uADF9\uC810\uC744 \uC21C\uC11C\uB300\uB85C \uB20C\uB7EC \uBC15\uC2A4\uB97C \uB9CC\uB4ED\uB2C8\uB2E4.")
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

        public ICommand ToggleDisplayAdjustmentCommand
            => toggleDisplayAdjustmentCommand ??= new RelayCommand(
                () => IsDisplayAdjustmentOpen = IsDisplayAdjustmentEnabled && !IsDisplayAdjustmentOpen);

        public ICommand ResetDisplayAdjustmentCommand
            => resetDisplayAdjustmentCommand ??= new RelayCommand(ResetDisplayAdjustment);

        public bool IsDisplayAdjustmentOpen
        {
            get => isDisplayAdjustmentOpen;
            set => SetProperty(ref isDisplayAdjustmentOpen, value && IsDisplayAdjustmentEnabled);
        }

        public int DisplayBrightness
        {
            get => displayBrightness;
            set
            {
                int normalized = Math.Clamp(value, -100, 100);
                if (SetProperty(ref displayBrightness, normalized))
                {
                    OnPropertyChanged(nameof(DisplayBrightnessText));
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public string DisplayBrightnessText => $"{DisplayBrightness:+0;-0;0}";

        public double DisplayContrastPercent
        {
            get => displayContrastPercent;
            set
            {
                double normalized = Math.Clamp(value, 50D, 200D);
                if (SetProperty(ref displayContrastPercent, normalized))
                {
                    OnPropertyChanged(nameof(DisplayContrastText));
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public string DisplayContrastText => $"{DisplayContrastPercent:0}%";

        public double DisplayGamma
        {
            get => displayGamma;
            set
            {
                double normalized = Math.Clamp(value, 0.2D, 3D);
                if (SetProperty(ref displayGamma, normalized))
                {
                    OnPropertyChanged(nameof(DisplayGammaText));
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public string DisplayGammaText => $"{DisplayGamma:0.00}";

        public bool IsDisplayInverted
        {
            get => isDisplayInverted;
            set
            {
                if (SetProperty(ref isDisplayInverted, value))
                {
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public bool IsDisplayHistogramEqualized
        {
            get => isDisplayHistogramEqualized;
            set
            {
                if (SetProperty(ref isDisplayHistogramEqualized, value))
                {
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public bool IsDisplayAdjustmentActive
            => !GetDisplayAdjustmentOptions().IsDefault;

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
                    if (value != null)
                    {
                        lastLabelClassName = value.Text;
                    }
                    RefreshActiveLabelClassPresentation();
                }
            }
        }

        public ICommand ToggleShortcutHelpCommand
            => toggleShortcutHelpCommand ??= new RelayCommand(ToggleShortcutHelp);

        public System.Windows.Visibility ShortcutHelpVisibility
        {
            get => shortcutHelpVisibility;
            private set => SetProperty(ref shortcutHelpVisibility, value);
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

        public ICommand CreateSmartMaskCommand
        {
            get => createSmartMaskCommand;
            private set => SetProperty(ref createSmartMaskCommand, value);
        }

        public ICommand AddPositiveSmartMaskPointCommand => addPositiveSmartMaskPointCommand;
        public ICommand AddNegativeSmartMaskPointCommand => addNegativeSmartMaskPointCommand;
        public ICommand UndoSmartMaskPointCommand => undoSmartMaskPointCommand;
        public ICommand ClearSmartMaskPointsCommand => clearSmartMaskPointsCommand;
        public ICommand CancelSmartMaskGenerationCommand => cancelSmartMaskGenerationCommand;
        public ICommand NextSmartMaskInstanceCommand => nextSmartMaskInstanceCommand;
        public ICommand ShowInitialSmartMaskCandidateCommand => showInitialSmartMaskCandidateCommand;
        public ICommand ShowLatestSmartMaskCandidateCommand => showLatestSmartMaskCandidateCommand;
        public ICommand ToggleSmartMaskAutoContourCommand
            => toggleSmartMaskAutoContourCommand ??= new RelayCommand(
                () =>
                {
                    IsSmartMaskAutoContourEnabled = !IsSmartMaskAutoContourEnabled;
                    smartMaskAutoContourChanged(IsSmartMaskAutoContourEnabled);
                });

        public ICommand BoxDrawingMethodSelectionChangedCommand
        {
            get => boxDrawingMethodSelectionChangedCommand;
            private set => SetProperty(ref boxDrawingMethodSelectionChangedCommand, value);
        }

        public ICommand ResetBoxDrawingMethodCommand
            => resetBoxDrawingMethodCommand ??= new RelayCommand(
                () =>
                {
                    if (SelectedBoxDrawingMethod?.Method == LabelingBoxDrawingMethod.TwoPointDrag)
                    {
                        return;
                    }

                    RestoreBoxDrawingMethod(LabelingBoxDrawingMethod.TwoPointDrag);
                    boxDrawingMethodChanged(LabelingBoxDrawingMethod.TwoPointDrag);
                });

        public WpfBoxDrawingMethodItem SelectedBoxDrawingMethod
        {
            get => selectedBoxDrawingMethod;
            set
            {
                if (value == null || !BoxDrawingMethods.Contains(value))
                {
                    return;
                }

                if (SetProperty(ref selectedBoxDrawingMethod, value))
                {
                    OnPropertyChanged(nameof(BoxDrawingMethodToolTip));
                }
            }
        }

        public System.Windows.Visibility BoxDrawingMethodVisibility
        {
            get => boxDrawingMethodVisibility;
            private set => SetProperty(ref boxDrawingMethodVisibility, value);
        }

        public System.Windows.Visibility FourPointBoxProgressVisibility
        {
            get => fourPointBoxProgressVisibility;
            private set => SetProperty(ref fourPointBoxProgressVisibility, value);
        }

        public string FourPointBoxProgressText
        {
            get => fourPointBoxProgressText;
            private set => SetProperty(ref fourPointBoxProgressText, value ?? string.Empty);
        }

        public string BoxDrawingMethodToolTip
            => SelectedBoxDrawingMethod?.ToolTip
                ?? "\uBC15\uC2A4\uB97C \uB9CC\uB4DC\uB294 \uC785\uB825 \uBC29\uC2DD\uC744 \uC120\uD0DD\uD569\uB2C8\uB2E4.";

        public ICommand ToggleSmartMaskCorrectionOptionsCommand
            => toggleSmartMaskCorrectionOptionsCommand ??= new RelayCommand(
                () => IsSmartMaskCorrectionOptionsExpanded = !IsSmartMaskCorrectionOptionsExpanded);

        public System.Windows.Visibility SmartMaskVisibility
        {
            get => smartMaskVisibility;
            private set => SetProperty(ref smartMaskVisibility, value);
        }

        public System.Windows.Visibility SmartMaskSessionActionVisibility
        {
            get => smartMaskSessionActionVisibility;
            private set => SetProperty(ref smartMaskSessionActionVisibility, value);
        }

        public System.Windows.Visibility SmartMaskSessionVisibility
        {
            get => smartMaskSessionVisibility;
            private set => SetProperty(ref smartMaskSessionVisibility, value);
        }

        public System.Windows.Visibility SmartMaskCorrectionOptionsVisibility
        {
            get => smartMaskCorrectionOptionsVisibility;
            private set => SetProperty(ref smartMaskCorrectionOptionsVisibility, value);
        }

        public System.Windows.Visibility SmartMaskCandidateComparisonVisibility
        {
            get => smartMaskCandidateComparisonVisibility;
            private set => SetProperty(ref smartMaskCandidateComparisonVisibility, value);
        }

        public bool IsSmartMaskCorrectionOptionsExpanded
        {
            get => isSmartMaskCorrectionOptionsExpanded;
            private set
            {
                if (SetProperty(ref isSmartMaskCorrectionOptionsExpanded, value))
                {
                    SmartMaskCorrectionOptionsVisibility = value
                        ? System.Windows.Visibility.Visible
                        : System.Windows.Visibility.Collapsed;
                    OnPropertyChanged(nameof(SmartMaskCorrectionOptionsText));
                    OnPropertyChanged(nameof(SmartMaskCorrectionOptionsGlyph));
                }
            }
        }

        public string SmartMaskCorrectionOptionsText
            => IsSmartMaskCorrectionOptionsExpanded ? "보정 닫기" : "보정 옵션";

        public string SmartMaskCorrectionOptionsGlyph
            => IsSmartMaskCorrectionOptionsExpanded ? "⌃" : "⌄";

        public string SmartMaskPromptSummaryText
        {
            get => smartMaskPromptSummaryText;
            private set => SetProperty(ref smartMaskPromptSummaryText, value ?? string.Empty);
        }

        public string SmartMaskCandidateComparisonText
        {
            get => smartMaskCandidateComparisonText;
            private set => SetProperty(ref smartMaskCandidateComparisonText, value ?? string.Empty);
        }

        public bool IsSmartMaskPointActionEnabled
        {
            get => isSmartMaskPointActionEnabled;
            private set => SetProperty(ref isSmartMaskPointActionEnabled, value);
        }

        public bool IsSmartMaskPointUndoEnabled
        {
            get => isSmartMaskPointUndoEnabled;
            private set => SetProperty(ref isSmartMaskPointUndoEnabled, value);
        }

        public bool IsSmartMaskCancelEnabled
        {
            get => isSmartMaskCancelEnabled;
            private set => SetProperty(ref isSmartMaskCancelEnabled, value);
        }

        public bool IsSmartMaskNextInstanceEnabled
        {
            get => isSmartMaskNextInstanceEnabled;
            private set => SetProperty(ref isSmartMaskNextInstanceEnabled, value);
        }

        public bool IsShowInitialSmartMaskCandidateEnabled
        {
            get => isShowInitialSmartMaskCandidateEnabled;
            private set => SetProperty(ref isShowInitialSmartMaskCandidateEnabled, value);
        }

        public bool IsShowLatestSmartMaskCandidateEnabled
        {
            get => isShowLatestSmartMaskCandidateEnabled;
            private set => SetProperty(ref isShowLatestSmartMaskCandidateEnabled, value);
        }

        public bool IsPositiveSmartMaskPointMode
        {
            get => isPositiveSmartMaskPointMode;
            private set => SetProperty(ref isPositiveSmartMaskPointMode, value);
        }

        public bool IsNegativeSmartMaskPointMode
        {
            get => isNegativeSmartMaskPointMode;
            private set => SetProperty(ref isNegativeSmartMaskPointMode, value);
        }

        public WpfSmartMaskDetailItem SelectedSmartMaskDetail
        {
            get => selectedSmartMaskDetail;
            set
            {
                if (SetProperty(ref selectedSmartMaskDetail, value) && value != null)
                {
                    smartMaskDetailChanged(value.Detail);
                }
            }
        }

        public bool IsSmartMaskEnabled
        {
            get => isSmartMaskEnabled;
            private set => SetProperty(ref isSmartMaskEnabled, value);
        }

        public bool IsSmartMaskAutoContourEnabled
        {
            get => isSmartMaskAutoContourEnabled;
            private set
            {
                if (SetProperty(ref isSmartMaskAutoContourEnabled, value))
                {
                    OnPropertyChanged(nameof(SmartMaskAutoContourText));
                    OnPropertyChanged(nameof(SmartMaskAutoContourToolTip));
                }
            }
        }

        public bool IsSmartMaskAutoContourToggleEnabled
        {
            get => isSmartMaskAutoContourToggleEnabled;
            private set => SetProperty(ref isSmartMaskAutoContourToggleEnabled, value);
        }

        public string SmartMaskAutoContourText
            => IsSmartMaskAutoContourEnabled ? "자동 윤곽: 켜짐" : "자동 윤곽: 꺼짐";

        public string SmartMaskAutoContourToolTip
            => IsSmartMaskAutoContourEnabled
                ? "새 사각형을 완성하면 MobileSAM 윤곽 후보를 바로 만듭니다. 후보는 확인 전까지 저장되지 않습니다."
                : "한 번 켜 두면 새 사각형을 완성할 때마다 MobileSAM 윤곽 후보를 바로 만듭니다.";

        public void RestoreSmartMaskAutoContourMode(bool enabled)
        {
            IsSmartMaskAutoContourEnabled = enabled;
        }

        public void ConfigureBoxDrawingMethod(Action<LabelingBoxDrawingMethod> drawingMethodChanged)
        {
            boxDrawingMethodChanged = drawingMethodChanged ?? (_ => { });
            BoxDrawingMethodSelectionChangedCommand = new RelayCommand<object>(
                selected =>
                {
                    WpfBoxDrawingMethodItem item = selected as WpfBoxDrawingMethodItem
                        ?? SelectedBoxDrawingMethod;
                    if (item != null && !isRestoringBoxDrawingMethod)
                    {
                        boxDrawingMethodChanged(item.Method);
                    }
                });
            RestoreBoxDrawingMethod(
                SelectedBoxDrawingMethod?.Method ?? LabelingBoxDrawingMethod.TwoPointDrag);
        }

        public void RestoreBoxDrawingMethod(LabelingBoxDrawingMethod method)
        {
            LabelingBoxDrawingMethod normalized = Enum.IsDefined(typeof(LabelingBoxDrawingMethod), method)
                ? method
                : LabelingBoxDrawingMethod.TwoPointDrag;
            isRestoringBoxDrawingMethod = true;
            try
            {
                SelectedBoxDrawingMethod = BoxDrawingMethods.First(item => item.Method == normalized);
                SetFourPointBoxProgress(0);
            }
            finally
            {
                isRestoringBoxDrawingMethod = false;
            }
        }

        public void SetFourPointBoxProgress(int acceptedPointCount)
        {
            int normalized = Math.Clamp(acceptedPointCount, 0, 4);
            string nextRole = normalized switch
            {
                0 => "\uC704",
                1 => "\uC544\uB798",
                2 => "\uC67C\uCABD",
                3 => "\uC624\uB978\uCABD",
                _ => "\uC644\uB8CC"
            };
            FourPointBoxProgressText = $"4\uC810 \uADF9\uC810 \u00B7 {nextRole} {normalized}/4";
            FourPointBoxProgressVisibility =
                SelectedBoxDrawingMethod?.Method == LabelingBoxDrawingMethod.FourPointExtreme
                && BoxDrawingMethodVisibility == System.Windows.Visibility.Visible
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        }

        public string SmartMaskActionText
        {
            get => smartMaskActionText;
            private set => SetProperty(ref smartMaskActionText, value ?? string.Empty);
        }

        public string SmartMaskToolTip
        {
            get => smartMaskToolTip;
            private set => SetProperty(ref smartMaskToolTip, value ?? string.Empty);
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

        public bool IsDisplayAdjustmentEnabled
        {
            get => isDisplayAdjustmentEnabled;
            private set => SetProperty(ref isDisplayAdjustmentEnabled, value);
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

        public void ConfigureDisplayAdjustment(Action adjustmentChanged)
        {
            displayAdjustmentChanged = adjustmentChanged ?? NoOpCommand;
        }

        public ImageDisplayAdjustmentOptions GetDisplayAdjustmentOptions()
            => new ImageDisplayAdjustmentOptions
            {
                Brightness = DisplayBrightness,
                Contrast = DisplayContrastPercent / 100D,
                Gamma = DisplayGamma,
                Invert = IsDisplayInverted,
                EqualizeHistogram = IsDisplayHistogramEqualized
            };

        public void ResetDisplayAdjustment()
        {
            suppressDisplayAdjustmentNotification = true;
            try
            {
                DisplayBrightness = 0;
                DisplayContrastPercent = 100D;
                DisplayGamma = 1D;
                IsDisplayInverted = false;
                IsDisplayHistogramEqualized = false;
            }
            finally
            {
                suppressDisplayAdjustmentNotification = false;
            }

            NotifyDisplayAdjustmentChanged();
        }

        private void NotifyDisplayAdjustmentChanged()
        {
            OnPropertyChanged(nameof(IsDisplayAdjustmentActive));
            if (!suppressDisplayAdjustmentNotification)
            {
                displayAdjustmentChanged();
            }
        }

        public void ConfigureBrushSizeCommands(Action decreaseBrushSize, Action increaseBrushSize)
        {
            DecreaseBrushSizeCommand = new RelayCommand(decreaseBrushSize ?? NoOpCommand);
            IncreaseBrushSizeCommand = new RelayCommand(increaseBrushSize ?? NoOpCommand);
        }

        public void ConfigureSmartMaskCommand(Action createSmartMask)
        {
            CreateSmartMaskCommand = new RelayCommand(createSmartMask ?? NoOpCommand);
        }

        public void ConfigureSmartMaskCommands(
            Action createSmartMask,
            Action addPositivePoint,
            Action addNegativePoint,
            Action undoPoint,
            Action clearPoints,
            Action cancelGeneration,
            Action nextInstance,
            Action showInitialCandidate,
            Action showLatestCandidate,
            Action<bool> autoContourChanged,
            Action<WpfSmartMaskPolygonDetail> detailChanged)
        {
            ConfigureSmartMaskCommand(createSmartMask);
            addPositiveSmartMaskPointCommand = new RelayCommand(addPositivePoint ?? NoOpCommand);
            addNegativeSmartMaskPointCommand = new RelayCommand(addNegativePoint ?? NoOpCommand);
            undoSmartMaskPointCommand = new RelayCommand(undoPoint ?? NoOpCommand);
            clearSmartMaskPointsCommand = new RelayCommand(clearPoints ?? NoOpCommand);
            cancelSmartMaskGenerationCommand = new RelayCommand(cancelGeneration ?? NoOpCommand);
            nextSmartMaskInstanceCommand = new RelayCommand(nextInstance ?? NoOpCommand);
            showInitialSmartMaskCandidateCommand = new RelayCommand(showInitialCandidate ?? NoOpCommand);
            showLatestSmartMaskCandidateCommand = new RelayCommand(showLatestCandidate ?? NoOpCommand);
            smartMaskAutoContourChanged = autoContourChanged ?? (_ => { });
            smartMaskDetailChanged = detailChanged ?? (_ => { });
            SelectedSmartMaskDetail = SmartMaskDetails.First(item => item.Detail == WpfSmartMaskPolygonDetail.Balanced);
            OnPropertyChanged(nameof(AddPositiveSmartMaskPointCommand));
            OnPropertyChanged(nameof(AddNegativeSmartMaskPointCommand));
            OnPropertyChanged(nameof(UndoSmartMaskPointCommand));
            OnPropertyChanged(nameof(ClearSmartMaskPointsCommand));
            OnPropertyChanged(nameof(CancelSmartMaskGenerationCommand));
            OnPropertyChanged(nameof(NextSmartMaskInstanceCommand));
            OnPropertyChanged(nameof(ShowInitialSmartMaskCandidateCommand));
            OnPropertyChanged(nameof(ShowLatestSmartMaskCandidateCommand));
        }

        public void SetSmartMaskState(bool isVisible, bool isEnabled, bool isBusy, string detail, bool hasSession = false)
        {
            SmartMaskVisibility = isVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            SmartMaskSessionActionVisibility = isVisible && hasSession
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            IsSmartMaskEnabled = isVisible && isEnabled && !isBusy;
            IsSmartMaskAutoContourToggleEnabled = isVisible && !hasSession && !isBusy;
            SmartMaskActionText = isBusy
                ? "마스크 생성 중..."
                : hasSession
                    ? "후보 다시 생성"
                    : "박스 → 스마트 마스크";
            SmartMaskToolTip = string.IsNullOrWhiteSpace(detail)
                ? "결함 둘레에 박스를 그린 뒤 MobileSAM 후보 마스크를 만듭니다. 결과는 확정 전 후보로만 표시됩니다."
                : detail;
        }

        public void SetSmartMaskSessionState(
            bool isVisible,
            bool isBusy,
            int positivePointCount,
            int negativePointCount,
            WpfSmartMaskPointInputMode inputMode,
            bool hasProducedCandidate,
            bool canMoveToNextInstance,
            bool hasCandidateComparison = false,
            WpfSmartMaskCandidateVersion selectedCandidateVersion = WpfSmartMaskCandidateVersion.Latest)
        {
            SmartMaskSessionVisibility = isVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            if (!isVisible)
            {
                IsSmartMaskCorrectionOptionsExpanded = false;
            }
            IsSmartMaskPointActionEnabled = isVisible && !isBusy;
            IsSmartMaskPointUndoEnabled = isVisible && !isBusy && positivePointCount + negativePointCount > 0;
            IsSmartMaskCancelEnabled = isVisible && isBusy;
            IsSmartMaskNextInstanceEnabled = isVisible && !isBusy && canMoveToNextInstance;
            SmartMaskCandidateComparisonVisibility = isVisible && hasCandidateComparison
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            IsShowInitialSmartMaskCandidateEnabled = isVisible
                && !isBusy
                && hasCandidateComparison
                && selectedCandidateVersion != WpfSmartMaskCandidateVersion.Initial;
            IsShowLatestSmartMaskCandidateEnabled = isVisible
                && !isBusy
                && hasCandidateComparison
                && selectedCandidateVersion != WpfSmartMaskCandidateVersion.Latest;
            SmartMaskCandidateComparisonText = !isVisible || !hasCandidateComparison
                ? string.Empty
                : selectedCandidateVersion == WpfSmartMaskCandidateVersion.Initial
                    ? "이전 후보를 보고 있음 · 확정하면 이 후보만 저장"
                    : "현재 후보를 보고 있음 · 확정하면 이 후보만 저장";
            IsPositiveSmartMaskPointMode = inputMode == WpfSmartMaskPointInputMode.Positive;
            IsNegativeSmartMaskPointMode = inputMode == WpfSmartMaskPointInputMode.Negative;
            SmartMaskPromptSummaryText = !isVisible
                ? "박스를 그려 첫 후보를 만드세요."
                : isBusy
                    ? "자동 후보를 계산하고 있습니다."
                    : positivePointCount + negativePointCount > 0
                        ? $"+ 포함 {positivePointCount} · − 제외 {negativePointCount} · 한 점씩 다시 생성해 비교"
                        : hasProducedCandidate
                            ? "자동 후보 준비 · 그대로 확정하거나 필요할 때만 보정"
                            : "시작 박스로 자동 후보를 준비합니다.";
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
            layerDisplayMode = mode;
            layerLabelCount = labelCount;
            layerInferenceCandidateCount = inferenceCandidateCount;
            layerHasUnsavedLabelChanges = hasUnsavedLabelChanges;
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

        public void RefreshLocalizedPresentation()
        {
            OnPropertyChanged(nameof(FirstLabelLoopText));
            OnPropertyChanged(nameof(ShortcutSummaryText));
            OnPropertyChanged(nameof(ShortcutHelpText));
            CurrentWorkflowStepText = TranslateExact(
                string.IsNullOrWhiteSpace(currentWorkflowStepSource) ? "단계" : currentWorkflowStepSource);
            CurrentWorkflowToolText = TranslateExact(
                string.IsNullOrWhiteSpace(currentWorkflowToolSource) ? "선택" : currentWorkflowToolSource);
            CurrentWorkflowActionText = TranslateExact(
                string.IsNullOrWhiteSpace(currentWorkflowActionSource)
                    ? T("WpfCanvas.Workflow.NoImageAction")
                    : currentWorkflowActionSource);

            int normalizedLabelCount = Math.Max(0, layerLabelCount);
            int normalizedCandidateCount = Math.Max(0, layerInferenceCandidateCount);
            string unsavedSuffix = layerHasUnsavedLabelChanges
                ? T("WpfCanvas.Layer.UnsavedSuffix")
                : string.Empty;
            CanvasLabelLayerText = layerDisplayMode != WpfCanvasDisplayMode.InferenceOnly
                ? Format("WpfCanvas.Layer.Labels.Shown", normalizedLabelCount, unsavedSuffix)
                : Format("WpfCanvas.Layer.Labels.Hidden", normalizedLabelCount, unsavedSuffix);
            CanvasInferenceLayerText = layerDisplayMode != WpfCanvasDisplayMode.LabelsOnly
                ? Format("WpfCanvas.Layer.Candidates.Shown", normalizedCandidateCount)
                : Format("WpfCanvas.Layer.Candidates.Hidden", normalizedCandidateCount);
            CanvasLayerModeTitleText = layerDisplayMode switch
            {
                WpfCanvasDisplayMode.InferenceOnly => T("WpfCanvas.LayerMode.Inference.Title"),
                WpfCanvasDisplayMode.Both => T("WpfCanvas.LayerMode.Both.Title"),
                _ => T("WpfCanvas.LayerMode.Labels.Title")
            };
            CanvasLayerModeDetailText = layerDisplayMode switch
            {
                WpfCanvasDisplayMode.InferenceOnly => T("WpfCanvas.LayerMode.Inference.Detail"),
                WpfCanvasDisplayMode.Both => T("WpfCanvas.LayerMode.Both.Detail"),
                _ => T("WpfCanvas.LayerMode.Labels.Detail")
            };
            CanvasLayerModeToolTip = $"{CanvasLayerModeDetailText}\n{CanvasLabelLayerText}\n{CanvasInferenceLayerText}";
            RefreshActiveLabelClassPresentation();
            OnPropertyChanged(nameof(AnnotationSaveActionText));
            OnPropertyChanged(nameof(AnnotationSaveToolTip));
            OnPropertyChanged(nameof(NoObjectCompletionActionText));
            OnPropertyChanged(nameof(NoObjectCompletionToolTip));
            OnPropertyChanged(nameof(AnnotationSaveStatusTitleText));
            OnPropertyChanged(nameof(AnnotationSaveStatusDetailText));
            OnPropertyChanged(nameof(ActiveLabelClassTitleText));
            OnPropertyChanged(nameof(ActiveLabelClassDetailText));
            OnPropertyChanged(nameof(ActiveLabelClassActionText));
            OnPropertyChanged(nameof(ActiveLabelClassActionToolTip));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            RefreshLocalizedPresentation();
        }

        private static string T(string key)
        {
            return OpenVisionLanguageService.T(key);
        }

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }

        private static string TranslateExact(string value)
        {
            return LocalizationTextRuntimeService.Translate(value);
        }

        public void SetLabelClasses(IEnumerable<LabelClass> classItems, string selectedName = "")
        {
            string normalizedSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            WpfCanvasLabelClassItem selectedItem = null;

            LabelClasses.Clear();
            int shortcutIndex = 1;
            int canonicalIndex = 0;
            foreach (LabelClass classItem in classItems ?? Enumerable.Empty<LabelClass>())
            {
                int currentIndex = canonicalIndex++;
                if (!ClassCatalogService.IsActiveClass(classItem))
                {
                    continue;
                }

                var labelItem = new WpfCanvasLabelClassItem(classItem, currentIndex, shortcutIndex++);
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

        public bool TrySelectLabelClassByShortcut(int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= Math.Min(9, LabelClasses.Count))
            {
                return false;
            }

            SelectedLabelClass = LabelClasses[zeroBasedIndex];
            return true;
        }

        public bool TryGetRepeatSelection(out WpfAnnotationTool tool, out string className)
        {
            tool = lastDrawingTool ?? WpfAnnotationTool.Select;
            className = lastLabelClassName;
            return lastDrawingTool.HasValue && !string.IsNullOrWhiteSpace(className);
        }

        public void ToggleShortcutHelp()
        {
            ShortcutHelpVisibility = ShortcutHelpVisibility == System.Windows.Visibility.Visible
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
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
            if (selectedTool == null || AnnotationWorkflowService.IsOneShotCommandTool(selectedTool.Tool))
            {
                return;
            }

            if (AnnotationTools.Contains(selectedTool))
            {
                SelectedAnnotationTool = selectedTool;
                if (AnnotationProductivityService.IsRepeatableDrawingTool(selectedTool.Tool))
                {
                    lastDrawingTool = selectedTool.Tool;
                }
                RefreshMaskBrushControlVisibility();
                RefreshBoxDrawingMethodVisibility();
            }
        }

        private void RefreshMaskBrushControlVisibility()
        {
            WpfAnnotationTool? tool = SelectedAnnotationTool?.Tool;
            MaskBrushControlVisibility = tool == WpfAnnotationTool.Brush || tool == WpfAnnotationTool.Eraser
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void RefreshBoxDrawingMethodVisibility()
        {
            BoxDrawingMethodVisibility = SelectedAnnotationTool?.Tool == WpfAnnotationTool.Rectangle
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            SetFourPointBoxProgress(0);
        }

        public void SetWorkflowContext(string stepText, string toolText, string actionText)
        {
            // Keep this small status strip as ViewModel state so the canvas view does not
            // need to reach into the guide panel or shell to explain the current workflow.
            currentWorkflowStepSource = stepText ?? string.Empty;
            currentWorkflowToolSource = toolText ?? string.Empty;
            currentWorkflowActionSource = actionText ?? string.Empty;
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

        private void RefreshActiveLabelClassPresentation()
        {
            string className = SelectedLabelClass?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                IsLabelClassSetupMissing = true;
                ActiveLabelClassTitleText = T("WpfCanvas.ActiveClass.MissingTitle");
                ActiveLabelClassDetailText = T("WpfCanvas.ActiveClass.MissingDetail");
                ActiveLabelClassActionText = T("WpfCanvas.ActiveClass.MissingAction");
                ActiveLabelClassActionToolTip = T("WpfCanvas.ActiveClass.MissingAction.ToolTip");
                return;
            }

            IsLabelClassSetupMissing = false;
            string canonicalDisplayText = SelectedLabelClass.CanonicalDisplayText;
            ActiveLabelClassTitleText = Format("WpfCanvas.ActiveClass.Title", canonicalDisplayText);
            ActiveLabelClassDetailText = Format("WpfCanvas.ActiveClass.Detail", canonicalDisplayText);
            ActiveLabelClassActionText = T("WpfCanvas.ActiveClass.Action");
            ActiveLabelClassActionToolTip = T("WpfCanvas.ActiveClass.Action.ToolTip");
        }

        public void SetCommandAvailability(bool hasImage, bool hasSelectedCandidate, bool hasPendingCandidates)
        {
            IsFitEnabled = hasImage;
            IsActualSizeEnabled = hasImage;
            IsPanEnabled = hasImage;
            IsDisplayAdjustmentEnabled = hasImage;
            if (!hasImage)
            {
                IsDisplayAdjustmentOpen = false;
            }
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
        public WpfCanvasLabelClassItem(LabelClass classItem, int canonicalIndex = 0, int shortcutIndex = 0)
        {
            Text = ClassCatalogService.NormalizeClassName(classItem?.Text);
            CanonicalIndex = Math.Max(0, canonicalIndex);
            ShortcutIndex = shortcutIndex is >= 1 and <= 9 ? shortcutIndex : 0;
            DrawColor = classItem?.DrawColor ?? DrawingColor.LimeGreen;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(DrawColor.R, DrawColor.G, DrawColor.B));
            brush.Freeze();
            DrawBrush = brush;
        }

        public string Text { get; }

        public int CanonicalIndex { get; }

        public int ShortcutIndex { get; }

        public string DisplayText => ShortcutIndex > 0 ? $"{ShortcutIndex} {Text}" : Text;

        public string CanonicalDisplayText => $"{CanonicalIndex} \u00B7 {Text}";

        public string ToolTip => ShortcutIndex > 0
            ? $"\uB2E8\uCD95\uD0A4 {ShortcutIndex} \u00B7 YOLO \uC778\uB371\uC2A4 {CanonicalIndex} \u00B7 \uB2E4\uC74C \uBC15\uC2A4/\uB9C8\uC2A4\uD06C: {Text}"
            : $"YOLO \uC778\uB371\uC2A4 {CanonicalIndex} \u00B7 \uB2E4\uC74C \uBC15\uC2A4/\uB9C8\uC2A4\uD06C: {Text}";

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
