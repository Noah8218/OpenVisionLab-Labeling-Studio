using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly CanvasDisplayAdjustmentWorkflowService displayAdjustmentWorkflow =
            new CanvasDisplayAdjustmentWorkflowService();
        private readonly CanvasLayerPresentationWorkflowService layerPresentationWorkflow =
            new CanvasLayerPresentationWorkflowService();
        private readonly SmartMaskPresentationWorkflowService smartMaskPresentationWorkflow =
            new SmartMaskPresentationWorkflowService();
        private readonly BoxDrawingPresentationWorkflowService boxDrawingPresentationWorkflow =
            new BoxDrawingPresentationWorkflowService();
        private readonly CanvasLabelClassPresentationWorkflowService labelClassPresentationWorkflow =
            new CanvasLabelClassPresentationWorkflowService();
        private readonly CanvasLabelClassCatalogPresentationService labelClassCatalogPresentationWorkflow =
            new CanvasLabelClassCatalogPresentationService();
        private readonly CanvasAnnotationToolSelectionWorkflowService annotationToolSelectionWorkflow =
            new CanvasAnnotationToolSelectionWorkflowService();
        private readonly CanvasNoObjectCompletionPresentationService noObjectCompletionPresentationWorkflow =
            new CanvasNoObjectCompletionPresentationService();
        private readonly CanvasCommandAvailabilityService commandAvailabilityWorkflow =
            new CanvasCommandAvailabilityService();
        private readonly CanvasDetectionOverlayPresentationService detectionOverlayPresentationWorkflow =
            new CanvasDetectionOverlayPresentationService();
        private readonly CanvasAnomalyReviewPresentationService anomalyReviewPresentation =
            new CanvasAnomalyReviewPresentationService();
        private readonly CanvasAnnotationToolbarPresentationService annotationToolbarPresentation =
            new CanvasAnnotationToolbarPresentationService();
        private Action displayAdjustmentChanged = NoOpCommand;
        private Action<WpfCanvasDisplayMode> displayModeChanged = _ => { };
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
        private CanvasWorkflowContext currentWorkflowContext;
        private string brushSizeText = CanvasBrushSizePresentationService.Format(CanvasBrushSizePresentationService.DefaultSize);
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
        private System.Windows.Visibility shortcutHelpVisibility = System.Windows.Visibility.Collapsed;
        private bool isSmartMaskAutoContourEnabled;
        private Action<LabelingBoxDrawingMethod> boxDrawingMethodChanged = _ => { };
        private bool isRestoringBoxDrawingMethod;
        private WpfSmartMaskDetailItem selectedSmartMaskDetail;
        private Action<bool> smartMaskAutoContourChanged = _ => { };
        private Action<WpfSmartMaskPolygonDetail> smartMaskDetailChanged = _ => { };
        private Action cancelPendingLabelClassDraft = NoOpCommand;
        private Action<string> selectClassCatalog = _ => { };
        private Action<string> refreshObjectClassOptions = _ => { };
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
            get => displayAdjustmentWorkflow.IsOpen;
            set
            {
                if (displayAdjustmentWorkflow.SetOpen(value))
                {
                    OnPropertyChanged(nameof(IsDisplayAdjustmentOpen));
                }
            }
        }

        public int DisplayBrightness
        {
            get => displayAdjustmentWorkflow.Brightness;
            set
            {
                if (displayAdjustmentWorkflow.SetBrightness(value))
                {
                    OnPropertyChanged(nameof(DisplayBrightnessText));
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public string DisplayBrightnessText => $"{DisplayBrightness:+0;-0;0}";

        public double DisplayContrastPercent
        {
            get => displayAdjustmentWorkflow.ContrastPercent;
            set
            {
                if (displayAdjustmentWorkflow.SetContrastPercent(value))
                {
                    OnPropertyChanged(nameof(DisplayContrastText));
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public string DisplayContrastText => $"{DisplayContrastPercent:0}%";

        public double DisplayGamma
        {
            get => displayAdjustmentWorkflow.Gamma;
            set
            {
                if (displayAdjustmentWorkflow.SetGamma(value))
                {
                    OnPropertyChanged(nameof(DisplayGammaText));
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public string DisplayGammaText => $"{DisplayGamma:0.00}";

        public bool IsDisplayInverted
        {
            get => displayAdjustmentWorkflow.IsInverted;
            set
            {
                if (displayAdjustmentWorkflow.SetInverted(value))
                {
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public bool IsDisplayHistogramEqualized
        {
            get => displayAdjustmentWorkflow.IsHistogramEqualized;
            set
            {
                if (displayAdjustmentWorkflow.SetHistogramEqualized(value))
                {
                    NotifyDisplayAdjustmentChanged();
                }
            }
        }

        public bool IsDisplayAdjustmentActive
            => displayAdjustmentWorkflow.IsActive;

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
            set
            {
                if (!SetProperty(ref selectedAnnotationTool, value))
                {
                    return;
                }

                CanvasAnnotationToolSelectionSnapshot snapshot = value == null
                    ? annotationToolSelectionWorkflow.ClearSelectedTool()
                    : annotationToolSelectionWorkflow.SetSelectedTool(value.Tool);
                ApplyAnnotationToolSelectionState(snapshot);
            }
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
                        labelClassCatalogPresentationWorkflow.SelectByName(value.Text);
                        annotationToolSelectionWorkflow.SetSelectedLabelClass(value.Text);
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
            set
            {
                if (SetProperty(ref selectedDisplayMode, value) && value != null)
                {
                    ApplyLayerPresentation(layerPresentationWorkflow.SetDisplayMode(value.Mode));
                }
            }
        }

        public WpfCanvasDisplayMode CurrentDisplayMode
            => layerPresentationWorkflow.CurrentMode;

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
            get => BoxDrawingMethods.FirstOrDefault(
                item => item.Method == boxDrawingPresentationWorkflow.SelectedMethod);
            set
            {
                if (value == null || !BoxDrawingMethods.Contains(value))
                {
                    return;
                }

                if (value.Method != boxDrawingPresentationWorkflow.SelectedMethod)
                {
                    ApplyBoxDrawingPresentation(
                        boxDrawingPresentationWorkflow.SetSelectedMethod(value.Method));
                }
            }
        }

        public System.Windows.Visibility BoxDrawingMethodVisibility
            => boxDrawingPresentationWorkflow.GetSnapshot().IsMethodSelectorVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility FourPointBoxProgressVisibility
            => boxDrawingPresentationWorkflow.GetSnapshot().IsProgressVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public string FourPointBoxProgressText
            => boxDrawingPresentationWorkflow.GetSnapshot().ProgressText;

        public string BoxDrawingMethodToolTip
            => SelectedBoxDrawingMethod?.ToolTip
                ?? "\uBC15\uC2A4\uB97C \uB9CC\uB4DC\uB294 \uC785\uB825 \uBC29\uC2DD\uC744 \uC120\uD0DD\uD569\uB2C8\uB2E4.";

        public ICommand ToggleSmartMaskCorrectionOptionsCommand
            => toggleSmartMaskCorrectionOptionsCommand ??= new RelayCommand(
                () => ApplySmartMaskPresentation(
                    smartMaskPresentationWorkflow.SetCorrectionOptionsExpanded(
                        !IsSmartMaskCorrectionOptionsExpanded)));

        public System.Windows.Visibility SmartMaskVisibility
            => smartMaskPresentationWorkflow.GetSnapshot().IsVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility SmartMaskSessionActionVisibility
            => smartMaskPresentationWorkflow.GetSnapshot().IsVisible
                && smartMaskPresentationWorkflow.GetSnapshot().HasSession
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility SmartMaskSessionVisibility
            => smartMaskPresentationWorkflow.GetSnapshot().IsSessionVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility SmartMaskCorrectionOptionsVisibility
            => IsSmartMaskCorrectionOptionsExpanded
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility SmartMaskCandidateComparisonVisibility
            => smartMaskPresentationWorkflow.GetSnapshot().IsCandidateComparisonVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public bool IsSmartMaskCorrectionOptionsExpanded
            => smartMaskPresentationWorkflow.IsCorrectionOptionsExpanded;

        public string SmartMaskCorrectionOptionsText
            => IsSmartMaskCorrectionOptionsExpanded ? "보정 닫기" : "보정 옵션";

        public string SmartMaskCorrectionOptionsGlyph
            => IsSmartMaskCorrectionOptionsExpanded ? "⌃" : "⌄";

        public string SmartMaskPromptSummaryText
            => smartMaskPresentationWorkflow.GetSnapshot().PromptSummaryText;

        public string SmartMaskCandidateComparisonText
            => smartMaskPresentationWorkflow.GetSnapshot().CandidateComparisonText;

        public bool IsSmartMaskPointActionEnabled
            => smartMaskPresentationWorkflow.GetSnapshot().IsPointActionEnabled;

        public bool IsSmartMaskPointUndoEnabled
            => smartMaskPresentationWorkflow.GetSnapshot().IsPointUndoEnabled;

        public bool IsSmartMaskCancelEnabled
            => smartMaskPresentationWorkflow.GetSnapshot().IsCancelEnabled;

        public bool IsSmartMaskNextInstanceEnabled
            => smartMaskPresentationWorkflow.GetSnapshot().IsNextInstanceEnabled;

        public bool IsShowInitialSmartMaskCandidateEnabled
            => smartMaskPresentationWorkflow.GetSnapshot().IsShowInitialCandidateEnabled;

        public bool IsShowLatestSmartMaskCandidateEnabled
            => smartMaskPresentationWorkflow.GetSnapshot().IsShowLatestCandidateEnabled;

        public bool IsPositiveSmartMaskPointMode
            => smartMaskPresentationWorkflow.GetSnapshot().IsPositivePointMode;

        public bool IsNegativeSmartMaskPointMode
            => smartMaskPresentationWorkflow.GetSnapshot().IsNegativePointMode;

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
            => smartMaskPresentationWorkflow.GetSnapshot().IsEnabled;

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
            => smartMaskPresentationWorkflow.GetSnapshot().IsAutoContourToggleEnabled;

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
                ApplyBoxDrawingPresentation(
                    boxDrawingPresentationWorkflow.SetSelectedMethod(normalized));
            }
            finally
            {
                isRestoringBoxDrawingMethod = false;
            }
        }

        public void SetFourPointBoxProgress(int acceptedPointCount)
        {
            ApplyBoxDrawingPresentation(
                boxDrawingPresentationWorkflow.SetAcceptedPointCount(acceptedPointCount));
        }

        private void ApplyBoxDrawingPresentation(BoxDrawingPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedBoxDrawingMethod));
            OnPropertyChanged(nameof(BoxDrawingMethodVisibility));
            OnPropertyChanged(nameof(FourPointBoxProgressVisibility));
            OnPropertyChanged(nameof(FourPointBoxProgressText));
            OnPropertyChanged(nameof(BoxDrawingMethodToolTip));
        }

        public string SmartMaskActionText
            => smartMaskPresentationWorkflow.GetSnapshot().ActionText;

        public string SmartMaskToolTip
            => smartMaskPresentationWorkflow.GetSnapshot().ToolTip;

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
            => labelClassPresentationWorkflow.GetSnapshot().TitleText;

        public string ActiveLabelClassDetailText
            => labelClassPresentationWorkflow.GetSnapshot().DetailText;

        public string ActiveLabelClassActionText
            => labelClassPresentationWorkflow.GetSnapshot().ActionText;

        public string ActiveLabelClassActionToolTip
            => labelClassPresentationWorkflow.GetSnapshot().ActionToolTip;

        public bool IsLabelClassSetupMissing
            => labelClassPresentationWorkflow.GetSnapshot().IsSetupMissing;

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
            get => displayAdjustmentWorkflow.IsEnabled;
            private set
            {
                if (displayAdjustmentWorkflow.SetEnabled(value))
                {
                    OnPropertyChanged(nameof(IsDisplayAdjustmentEnabled));
                    OnPropertyChanged(nameof(IsDisplayAdjustmentOpen));
                }
            }
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
            CanvasAnomalyReviewPresentationSnapshot presentation = anomalyReviewPresentation.Build(enabled);
            AnnotationWorkspaceVisibility = presentation.IsWorkspaceVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            AnnotationToolRailWidth = new System.Windows.GridLength(presentation.AnnotationToolRailWidth);
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
            => displayAdjustmentWorkflow.GetOptions();

        public void ResetDisplayAdjustment()
        {
            displayAdjustmentWorkflow.Reset();
            OnPropertyChanged(nameof(DisplayBrightness));
            OnPropertyChanged(nameof(DisplayBrightnessText));
            OnPropertyChanged(nameof(DisplayContrastPercent));
            OnPropertyChanged(nameof(DisplayContrastText));
            OnPropertyChanged(nameof(DisplayGamma));
            OnPropertyChanged(nameof(DisplayGammaText));
            OnPropertyChanged(nameof(IsDisplayInverted));
            OnPropertyChanged(nameof(IsDisplayHistogramEqualized));
            NotifyDisplayAdjustmentChanged();
        }

        private void NotifyDisplayAdjustmentChanged()
        {
            OnPropertyChanged(nameof(IsDisplayAdjustmentActive));
            displayAdjustmentChanged();
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
            ApplySmartMaskPresentation(
                smartMaskPresentationWorkflow.SetState(isVisible, isEnabled, isBusy, detail, hasSession));
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
            ApplySmartMaskPresentation(
                smartMaskPresentationWorkflow.SetSessionState(
                    isVisible,
                    isBusy,
                    positivePointCount,
                    negativePointCount,
                    inputMode,
                    hasProducedCandidate,
                    canMoveToNextInstance,
                    hasCandidateComparison,
                    selectedCandidateVersion));
        }

        private void ApplySmartMaskPresentation(SmartMaskPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            OnPropertyChanged(nameof(SmartMaskVisibility));
            OnPropertyChanged(nameof(SmartMaskSessionActionVisibility));
            OnPropertyChanged(nameof(SmartMaskSessionVisibility));
            OnPropertyChanged(nameof(SmartMaskCorrectionOptionsVisibility));
            OnPropertyChanged(nameof(SmartMaskCandidateComparisonVisibility));
            OnPropertyChanged(nameof(IsSmartMaskCorrectionOptionsExpanded));
            OnPropertyChanged(nameof(SmartMaskCorrectionOptionsText));
            OnPropertyChanged(nameof(SmartMaskCorrectionOptionsGlyph));
            OnPropertyChanged(nameof(SmartMaskPromptSummaryText));
            OnPropertyChanged(nameof(SmartMaskCandidateComparisonText));
            OnPropertyChanged(nameof(IsSmartMaskPointActionEnabled));
            OnPropertyChanged(nameof(IsSmartMaskPointUndoEnabled));
            OnPropertyChanged(nameof(IsSmartMaskCancelEnabled));
            OnPropertyChanged(nameof(IsSmartMaskNextInstanceEnabled));
            OnPropertyChanged(nameof(IsShowInitialSmartMaskCandidateEnabled));
            OnPropertyChanged(nameof(IsShowLatestSmartMaskCandidateEnabled));
            OnPropertyChanged(nameof(IsPositiveSmartMaskPointMode));
            OnPropertyChanged(nameof(IsNegativeSmartMaskPointMode));
            OnPropertyChanged(nameof(IsSmartMaskEnabled));
            OnPropertyChanged(nameof(IsSmartMaskAutoContourToggleEnabled));
            OnPropertyChanged(nameof(SmartMaskActionText));
            OnPropertyChanged(nameof(SmartMaskToolTip));
        }

        public void SetBrushSize(int size)
        {
            BrushSizeText = CanvasBrushSizePresentationService.Format(size);
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
            CanvasAnnotationToolbarPresentationSnapshot snapshot = annotationToolbarPresentation.Build(tools);
            foreach (WpfAnnotationToolItem tool in snapshot.SelectableTools)
            {
                AnnotationTools.Add(tool);
            }

            UndoAnnotationTool = snapshot.UndoTool;
            RedoAnnotationTool = snapshot.RedoTool;
            DeleteAnnotationTool = snapshot.DeleteTool;
            SetSelectedAnnotationTool(selectedTool ?? AnnotationTools.FirstOrDefault());
            AnnotationToolSelectionChangedCommand = new RelayCommand<object>(annotationToolSelectionChanged ?? NoOpSelectionCommand);
        }

        public void ConfigureLabelClassSelection(Action<object> labelClassSelectionChanged, Action openClassCatalog = null)
        {
            LabelClassSelectionChangedCommand = new RelayCommand<object>(labelClassSelectionChanged ?? NoOpSelectionCommand);
            OpenClassCatalogCommand = new RelayCommand(openClassCatalog ?? NoOpCommand);
        }

        public void ConfigureLabelClassSelectionWorkflow(
            Action cancelPendingFourPointBoxDraft,
            Action<string> selectClassCatalogAction,
            Action<string> refreshObjectClassOptionsAction)
        {
            cancelPendingLabelClassDraft = cancelPendingFourPointBoxDraft ?? NoOpCommand;
            selectClassCatalog = selectClassCatalogAction ?? (_ => { });
            refreshObjectClassOptions = refreshObjectClassOptionsAction ?? (_ => { });
            LabelClassSelectionChangedCommand = new RelayCommand<object>(ExecuteLabelClassSelectionChanged);
        }

        public void ApplyLabelClassSelection(object selectedItem)
        {
            ExecuteLabelClassSelectionChanged(selectedItem);
        }

        private void ExecuteLabelClassSelectionChanged(object selectedItem)
        {
            cancelPendingLabelClassDraft?.Invoke();
            WpfCanvasLabelClassItem selectedClass = selectedItem as WpfCanvasLabelClassItem ?? SelectedLabelClass;
            string className = selectedClass?.Text;
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            selectClassCatalog?.Invoke(className);
            SelectLabelClass(className);
            refreshObjectClassOptions?.Invoke(className);
        }

        public void ConfigureDisplayModeSelection(Action<object> displayModeSelectionChanged)
        {
            DisplayModeSelectionChangedCommand = new RelayCommand<object>(displayModeSelectionChanged ?? NoOpSelectionCommand);
            if (SelectedDisplayMode == null)
            {
                SetDisplayMode(WpfCanvasDisplayMode.LabelsOnly);
            }
        }

        public void ConfigureDisplayModeSelectionWorkflow(Action<WpfCanvasDisplayMode> applyDisplayMode)
        {
            displayModeChanged = applyDisplayMode ?? (_ => { });
            DisplayModeSelectionChangedCommand = new RelayCommand<object>(ExecuteDisplayModeSelectionChanged);
        }

        private void ExecuteDisplayModeSelectionChanged(object selectedItem)
        {
            WpfCanvasDisplayModeItem displayModeItem = selectedItem as WpfCanvasDisplayModeItem
                ?? SelectedDisplayMode;
            if (displayModeItem == null)
            {
                return;
            }

            SetDisplayMode(displayModeItem.Mode);
            displayModeChanged?.Invoke(displayModeItem.Mode);
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
            ApplyLayerPresentation(layerPresentationWorkflow.SetState(
                mode,
                labelCount,
                inferenceCandidateCount,
                hasUnsavedLabelChanges));
        }

        public void RefreshLocalizedPresentation()
        {
            OnPropertyChanged(nameof(FirstLabelLoopText));
            OnPropertyChanged(nameof(ShortcutSummaryText));
            OnPropertyChanged(nameof(ShortcutHelpText));
            string workflowStepSource = currentWorkflowContext?.StepText;
            string workflowToolSource = currentWorkflowContext?.ToolText;
            string workflowActionSource = currentWorkflowContext?.ActionText;
            CurrentWorkflowStepText = TranslateExact(
                string.IsNullOrWhiteSpace(workflowStepSource) ? "단계" : workflowStepSource);
            CurrentWorkflowToolText = TranslateExact(
                string.IsNullOrWhiteSpace(workflowToolSource) ? "선택" : workflowToolSource);
            CurrentWorkflowActionText = TranslateExact(
                string.IsNullOrWhiteSpace(workflowActionSource)
                    ? T("WpfCanvas.Workflow.NoImageAction")
                    : workflowActionSource);

            ApplyLayerPresentation(layerPresentationWorkflow.GetSnapshot());
            RefreshActiveLabelClassPresentation();
            OnPropertyChanged(nameof(AnnotationSaveActionText));
            OnPropertyChanged(nameof(AnnotationSaveToolTip));
            OnPropertyChanged(nameof(NoObjectCompletionActionText));
            OnPropertyChanged(nameof(NoObjectCompletionToolTip));
            OnPropertyChanged(nameof(AnnotationSaveStatusTitleText));
            OnPropertyChanged(nameof(AnnotationSaveStatusDetailText));
        }

        private void ApplyLayerPresentation(CanvasLayerPresentationSnapshot snapshot)
        {
            IsLabelLayerVisible = snapshot.ShowLabels;
            IsInferenceLayerVisible = snapshot.ShowInference;
            CanvasLayerModeTitleText = snapshot.Title;
            CanvasLayerModeDetailText = snapshot.Detail;
            CanvasLabelLayerText = snapshot.LabelText;
            CanvasInferenceLayerText = snapshot.InferenceText;
            CanvasLayerModeToolTip = snapshot.ToolTip;
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

        private static string TranslateExact(string value)
        {
            return LocalizationTextRuntimeService.Translate(value);
        }

        public void SetLabelClasses(IEnumerable<LabelClass> classItems, string selectedName = "")
        {
            CanvasLabelClassCatalogSnapshot snapshot = labelClassCatalogPresentationWorkflow.SetClasses(
                classItems,
                selectedName);
            LabelClasses.Clear();
            foreach (CanvasLabelClassCatalogItem item in snapshot.Items)
            {
                LabelClasses.Add(new WpfCanvasLabelClassItem(item));
            }

            SelectedLabelClass = snapshot.SelectedIndex >= 0
                ? LabelClasses[snapshot.SelectedIndex]
                : null;
            RefreshActiveLabelClassPresentation();
        }

        public void SelectLabelClass(string className)
        {
            CanvasLabelClassCatalogSnapshot snapshot = labelClassCatalogPresentationWorkflow.SelectByName(className);
            if (snapshot.SelectedIndex >= 0 && snapshot.SelectedIndex < LabelClasses.Count)
            {
                SelectedLabelClass = LabelClasses[snapshot.SelectedIndex];
            }
        }

        public bool TrySelectLabelClassByShortcut(int zeroBasedIndex)
        {
            if (!labelClassCatalogPresentationWorkflow.TrySelectByShortcut(
                zeroBasedIndex,
                out CanvasLabelClassCatalogSnapshot snapshot))
            {
                return false;
            }

            SelectedLabelClass = LabelClasses[snapshot.SelectedIndex];
            return true;
        }

        public bool TryGetRepeatSelection(out WpfAnnotationTool tool, out string className)
            => annotationToolSelectionWorkflow.TryGetRepeatSelection(out tool, out className);

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
            ApplyNoObjectCompletionPresentation(noObjectCompletionPresentationWorkflow.SetState(
                hasImage,
                hasLabelObjects,
                hasPendingCandidates));
        }

        private void ApplyNoObjectCompletionPresentation(CanvasNoObjectCompletionPresentationSnapshot snapshot)
        {
            IsNoObjectCompletionEnabled = snapshot.IsEnabled;
            NoObjectCompletionActionText = snapshot.ActionText;
            NoObjectCompletionToolTip = snapshot.ToolTip;
        }

        public void ApplyAnnotationSaveStatePresentation(AnnotationSaveStatePresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            IsAnnotationSaveEnabled = presentation.IsDirty;
            AnnotationSaveActionText = presentation.CanvasActionText;
            AnnotationSaveToolTip = presentation.CanvasToolTip;
            AnnotationSaveStatusKey = presentation.CanvasStatusKey;
            AnnotationSaveStatusTitleText = presentation.CanvasStatusTitleText;
            AnnotationSaveStatusDetailText = presentation.CanvasStatusDetailText;
        }

        [Obsolete("Use ApplyAnnotationSaveStatePresentation.", false)]
        public void SetAnnotationSaveState(bool isDirty, string actionText, string toolTip)
        {
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildLegacyCanvasState(isDirty, actionText, toolTip));
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
                RefreshBoxDrawingMethodVisibility();
            }
        }

        private void ApplyAnnotationToolSelectionState(CanvasAnnotationToolSelectionSnapshot snapshot)
        {
            MaskBrushControlVisibility = snapshot != null && snapshot.IsMaskBrushControlVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void RefreshBoxDrawingMethodVisibility()
        {
            ApplyBoxDrawingPresentation(
                boxDrawingPresentationWorkflow.SetRectangleToolSelected(
                    SelectedAnnotationTool?.Tool == WpfAnnotationTool.Rectangle));
        }

        public void SetWorkflowContext(CanvasWorkflowContext context)
        {
            // Keep this small status strip as ViewModel state so the canvas view does not
            // need to reach into the guide panel or shell to explain the current workflow.
            currentWorkflowContext = context;
            CurrentWorkflowStepText = string.IsNullOrWhiteSpace(context?.StepText) ? "단계" : context.StepText;
            CurrentWorkflowToolText = string.IsNullOrWhiteSpace(context?.ToolText) ? "선택" : context.ToolText;
            CurrentWorkflowActionText = string.IsNullOrWhiteSpace(context?.ActionText) ? "다음 작업을 선택하세요." : context.ActionText;
        }

        public void SetWorkflowContext(string stepText, string toolText, string actionText)
        {
            SetWorkflowContext(new CanvasWorkflowContext(
                WpfLearningStep.Label,
                stepText,
                toolText,
                actionText));
        }

        private void RefreshActiveLabelClassPresentation()
        {
            string canonicalDisplayText = SelectedLabelClass?.CanonicalDisplayText ?? string.Empty;
            ApplyLabelClassPresentation(
                labelClassPresentationWorkflow.SetState(
                    !string.IsNullOrWhiteSpace(canonicalDisplayText),
                    canonicalDisplayText));
        }

        private void ApplyLabelClassPresentation(CanvasLabelClassPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            OnPropertyChanged(nameof(ActiveLabelClassTitleText));
            OnPropertyChanged(nameof(ActiveLabelClassDetailText));
            OnPropertyChanged(nameof(ActiveLabelClassActionText));
            OnPropertyChanged(nameof(ActiveLabelClassActionToolTip));
            OnPropertyChanged(nameof(IsLabelClassSetupMissing));
        }

        public void SetCommandAvailability(bool hasImage, bool hasSelectedCandidate, bool hasPendingCandidates)
        {
            ApplyCommandAvailability(commandAvailabilityWorkflow.SetImageState(
                hasImage,
                hasSelectedCandidate,
                hasPendingCandidates));
            IsDisplayAdjustmentEnabled = hasImage;
            if (!hasImage)
            {
                IsDisplayAdjustmentOpen = false;
            }
        }

        public void SetCandidateReviewState(
            bool canNavigatePrevious,
            bool canNavigateNext,
            bool canFocusCurrentLabel,
            bool canConfirmSelected,
            bool canSkipSelected)
        {
            ApplyCommandAvailability(commandAvailabilityWorkflow.SetCandidateReviewState(
                canNavigatePrevious,
                canNavigateNext,
                canFocusCurrentLabel,
                canConfirmSelected,
                canSkipSelected));
        }

        private void ApplyCommandAvailability(CanvasCommandAvailabilitySnapshot snapshot)
        {
            IsFitEnabled = snapshot.IsFitEnabled;
            IsActualSizeEnabled = snapshot.IsActualSizeEnabled;
            IsPanEnabled = snapshot.IsPanEnabled;
            IsFocusCandidateEnabled = snapshot.IsFocusCandidateEnabled;
            IsResetAiOverlayEnabled = snapshot.IsResetAiOverlayEnabled;
            IsPreviousCandidateEnabled = snapshot.IsPreviousCandidateEnabled;
            IsNextCandidateEnabled = snapshot.IsNextCandidateEnabled;
            IsFocusCurrentLabelEnabled = snapshot.IsFocusCurrentLabelEnabled;
            IsConfirmSelectedEnabled = snapshot.IsConfirmSelectedEnabled;
            IsSkipSelectedEnabled = snapshot.IsSkipSelectedEnabled;
        }

        public void ClearDetectionOverlay()
        {
            ApplyDetectionOverlay(detectionOverlayPresentationWorkflow.Clear());
        }

        public void SetDetectionOverlay(
            string title,
            string summary,
            string selected,
            string detail,
            WpfDetectionOverlayStatus status)
        {
            ApplyDetectionOverlay(detectionOverlayPresentationWorkflow.Set(
                title,
                summary,
                selected,
                detail,
                status));
        }

        private void ApplyDetectionOverlay(CanvasDetectionOverlayPresentationSnapshot snapshot)
        {
            DetectionOverlayVisibility = snapshot.IsVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            DetectionOverlayActionsVisibility = snapshot.ShowActions
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            if (snapshot.IsVisible)
            {
                DetectionOverlayTitleText = snapshot.Title;
            }

            DetectionOverlaySummaryText = snapshot.Summary;
            DetectionOverlaySelectedText = snapshot.SelectedText;
            DetectionOverlayDetailText = snapshot.Detail;
            DetectionOverlayStatusKey = snapshot.StatusKey;
        }
    }

    public sealed class WpfCanvasLabelClassItem
    {
        public WpfCanvasLabelClassItem(CanvasLabelClassCatalogItem item)
        {
            Text = item?.Text ?? string.Empty;
            CanonicalIndex = item?.CanonicalIndex ?? 0;
            ShortcutIndex = item?.ShortcutIndex ?? 0;
            DrawColor = item?.DrawColor ?? DrawingColor.LimeGreen;
            var brush = new MediaSolidColorBrush(MediaColor.FromRgb(DrawColor.R, DrawColor.G, DrawColor.B));
            brush.Freeze();
            DrawBrush = brush;
        }

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
