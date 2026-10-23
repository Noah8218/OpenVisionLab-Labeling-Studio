using OpenVisionLab;
using OpenVisionLab.ImageCanvas.Views;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfCanvasPanel : UserControl
    {
        private readonly CanvasViewerStatusLocalizationAdapter viewerStatusLocalizationAdapter = new();
        private System.Windows.IInputElement displayAdjustmentPreviousFocus;
        private bool displayAdjustmentPreviousMoreExpanded;
        private CanvasDisplayAdjustmentWindow DisplayAdjustmentPopup;
        private WpfCanvasPanelViewModel observedViewModel;
        private WpfLabelingShellViewModel observedShellViewModel;
        private System.Windows.Forms.Control nativeShortcutControl;
        private readonly System.Collections.Generic.HashSet<System.Windows.Forms.Keys> nativeShortcutKeys = new();

        public WpfCanvasPanel()
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterRoot(this);
            LocalizationTextRuntimeService.RegisterRoot(MainCanvasView);
            LocalizationTextRuntimeService.RegisterRoot(CanvasSmartMaskPromptBar);
            Loaded += WpfCanvasPanel_Loaded;
            Unloaded += WpfCanvasPanel_Unloaded;
            DataContextChanged += WpfCanvasPanel_DataContextChanged;
            MainCanvasView.SizeChanged += MainCanvasView_SizeChanged;
            MainCanvasView.Loaded += (_, _) => AttachNativeShortcutInput();
            foreach (System.Windows.FrameworkElement control in new System.Windows.FrameworkElement[] { CanvasViewerZoomControls, ZoomOutCanvasButton, ZoomInCanvasButton, FitCanvasButton, ActualSizeCanvasButton, CanvasKeepZoomCheckBox, CanvasOverlayVisibilityCheckBox, CanvasHoldOriginalHintText, DisplayAdjustmentCanvasButton })
                control.SizeChanged += CanvasViewerFooter_SizeChanged;
            DetectionOverlayTitleText.SetBinding(
                TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(WpfCanvasPanelViewModel.DetectionOverlayTitleText)));
        }

        public WpfCanvasPanelViewModel ViewModel => DataContext as WpfCanvasPanelViewModel;

        public RoiImageCanvasView MainCanvas => MainCanvasView;
        public ListBox AnnotationToolList => CanvasAnnotationToolListBox;
        public ListBox LabelClassList => CanvasLabelClassListBox;
        public ListBox DisplayModeList => CanvasDisplayModeListBox;
        public WpfUiButton TemporaryOriginalViewButton => CanvasTemporaryOriginalViewButton;
        public Border WorkflowContextStrip => CanvasWorkflowContextStrip;
        public TextBlock CurrentStepText => CanvasCurrentStepText;
        public TextBlock CurrentToolText => CanvasCurrentToolText;
        public TextBlock NextActionText => CanvasNextActionText;
        public Border LayerVisibilityStrip => CanvasLayerVisibilityStrip;
        public TextBlock LayerModeTitleText => CanvasLayerModeTitleText;
        public TextBlock LayerModeDetailText => CanvasLayerModeDetailText;
        public TextBlock LabelLayerText => CanvasLabelLayerText;
        public TextBlock InferenceLayerText => CanvasInferenceLayerText;
        public Border AnnotationSaveStateCard => CanvasAnnotationSaveStateCard;
        public TextBlock AnnotationSaveStatusTitleTextBlock => CanvasAnnotationSaveStatusTitleText;
        public TextBlock AnnotationSaveStatusDetailTextBlock => CanvasAnnotationSaveStatusDetailText;
        public Border ActiveLabelClassCard => CanvasActiveLabelClassCard;
        public TextBlock ActiveLabelClassTitleTextBlock => CanvasActiveLabelClassTitleText;
        public TextBlock ActiveLabelClassDetailTextBlock => CanvasActiveLabelClassDetailText;
        public WpfUiButton OpenClassCatalogButton => CanvasOpenClassCatalogButton;
        public WpfUiButton ShortcutHelpButton => CanvasShortcutHelpButton;
        public Border ShortcutHelpCard => CanvasShortcutHelpCard;
        public TextBlock ShortcutHelpTextBlock => CanvasShortcutHelpText;
        public Border ResultOverlay => DetectionResultOverlay;
        public TextBlock OverlayTitleText => DetectionOverlayTitleText;
        public TextBlock OverlaySummaryText => DetectionOverlaySummaryText;
        public Border OverlaySelectedBorder => DetectionOverlaySelectedBorder;
        public TextBlock OverlaySelectedText => DetectionOverlaySelectedText;
        public TextBlock OverlayDetailText => DetectionOverlayDetailText;
        public WpfUiButton SaveAnnotationButton => CanvasSaveAnnotationButton;
        public WpfUiButton CreateSmartMaskButton => CanvasCreateSmartMaskButton;
        public WpfUiButton CompleteNoObjectButton => CanvasCompleteNoObjectButton;
        public WpfUiButton FitButton => FitCanvasButton;
        public WpfUiButton ActualSizeButton => ActualSizeCanvasButton;
        public WpfUiButton PanButton => PanCanvasButton;
        public WpfUiButton DisplayAdjustmentButton => DisplayAdjustmentCanvasButton;
        public CanvasDisplayAdjustmentWindow DisplayAdjustmentFlyout
        {
            get { EnsureDisplayAdjustmentWindow(); return DisplayAdjustmentPopup; }
        }
        public WpfUiButton FocusCandidateButton => FocusCandidateCanvasButton;
        public WpfUiButton ResetAiOverlayButton => ResetAiOverlayCanvasButton;

        public void RefreshLocalizedViewerStatus()
        {
            viewerStatusLocalizationAdapter.Refresh(MainCanvasView);
        }

        private void WpfCanvasPanel_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AttachViewModel(DataContext as WpfCanvasPanelViewModel);
            AttachShellViewModel();
            RefreshLocalizedViewerStatus();
            ApplyCanvasOverlayVisibility();
            EnsureDisplayAdjustmentWindow();
        }

        private void CanvasViewerFooter_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            double available = CanvasViewerZoomControls.ActualWidth;
            if (available <= 0D) return;
            double lineWidth = 0D, freeSpace = 0D;
            bool spacerInLine = false;
            foreach (System.Windows.FrameworkElement child in CanvasViewerZoomControls.Children)
            {
                if (child.Visibility != System.Windows.Visibility.Visible) continue;
                double width = ReferenceEquals(child, CanvasFooterFlexibleSpace) ? child.Margin.Left + child.Margin.Right : child.DesiredSize.Width;
                if (lineWidth > 0D && lineWidth + width > available)
                {
                    if (spacerInLine) { freeSpace = System.Math.Max(0D, available - lineWidth); break; }
                    lineWidth = 0D;
                }
                lineWidth += width;
                if (ReferenceEquals(child, CanvasFooterFlexibleSpace)) spacerInLine = true;
                freeSpace = spacerInLine ? System.Math.Max(0D, available - lineWidth) : 0D;
            }
            if (System.Math.Abs(CanvasFooterFlexibleSpace.Width - freeSpace) > .01D)
                CanvasFooterFlexibleSpace.SetCurrentValue(WidthProperty, freeSpace);
        }

        private void OnCanvasFooterCaptionTargetUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {
            if (sender is TextBlock caption && e.Property == TextBlock.TextProperty
                && ReferenceEquals(e.TargetObject, caption) && System.Windows.Media.VisualTreeHelper.GetParent(caption) is StackPanel content)
            {
                for (System.Windows.DependencyObject current = content; current != null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
                {
                    if (current is System.Windows.FrameworkElement owner) owner.InvalidateMeasure();
                    if (current is ButtonBase) break;
                }
            }
        }

        private void AttachNativeShortcutInput()
        {
            var host = MainCanvasView.FindName("imageBoxCameraTwoD") as System.Windows.Forms.Integration.WindowsFormsHost;
            var control = (host?.Child as OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl)?.GetOpenGLControl();
            if (ReferenceEquals(control, nativeShortcutControl)) return;
            DetachNativeShortcutInput();
            if (control == null || control.IsDisposed) return;
            nativeShortcutControl = control;
            OpenVisionLab.Logging.OVLog.Write(OpenVisionLab.Logging.LogCategory.Main, OpenVisionLab.Logging.LogLevel.Info, $"NATIVE_SHORTCUT_ATTACH type={control.GetType().Name} name={control.Name} handle={(control.IsHandleCreated ? control.Handle.ToInt64() : 0)} containsFocus={control.ContainsFocus} hostChild={host.Child?.GetType().Name} visible={IsVisible}");
            control.KeyDown += OnNativeShortcutKeyDown;
            control.KeyUp += OnNativeShortcutKeyUp;
            control.Leave += OnNativeShortcutFocusLeft;
        }

        private void DetachNativeShortcutInput()
        {
            if (nativeShortcutControl != null)
            {
                nativeShortcutControl.KeyDown -= OnNativeShortcutKeyDown;
                nativeShortcutControl.KeyUp -= OnNativeShortcutKeyUp;
                nativeShortcutControl.Leave -= OnNativeShortcutFocusLeft;
                nativeShortcutControl = null;
            }
            nativeShortcutKeys.Clear();
        }

        private void OnNativeShortcutKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Z || e.KeyCode == System.Windows.Forms.Keys.Y)
                OpenVisionLab.Logging.OVLog.Write(OpenVisionLab.Logging.LogCategory.Main, OpenVisionLab.Logging.LogLevel.Info, $"NATIVE_SHORTCUT_DOWN key={e.KeyCode} ctrl={e.Control} shift={e.Shift} handled={e.Handled} senderSame={ReferenceEquals(sender, nativeShortcutControl)} handle={(nativeShortcutControl?.IsHandleCreated == true ? nativeShortcutControl.Handle.ToInt64() : 0)} focus={nativeShortcutControl?.ContainsFocus} viewVisible={IsVisible}");
            if (!ReferenceEquals(sender, nativeShortcutControl) || nativeShortcutControl?.ContainsFocus != true || !IsVisible
                || e.KeyCode != System.Windows.Forms.Keys.Z && e.KeyCode != System.Windows.Forms.Keys.Y) return;
            bool repeat = !nativeShortcutKeys.Add(e.KeyCode);
            if (e.Handled || !e.Control) return;
            var owner = System.Windows.Window.GetWindow(this);
            if (owner == null) return;
            var command = OpenVisionLab.Mvvm.Behaviors.InputCommandBehaviors.GetPreviewKeyInputCommand(owner);
            ModifierKeys modifiers = ModifierKeys.Control | (e.Shift ? ModifierKeys.Shift : ModifierKeys.None)
                | (e.Alt ? ModifierKeys.Alt : ModifierKeys.None) | (Keyboard.Modifiers & ModifierKeys.Windows);
            var input = new OpenVisionLab.Mvvm.KeyInputCommandArgs(e.KeyCode == System.Windows.Forms.Keys.Z ? Key.Z : Key.Y, modifiers, repeat, MainCanvasView);
            bool allowed = command?.CanExecute(input) == true;
            OpenVisionLab.Logging.OVLog.Write(OpenVisionLab.Logging.LogCategory.Main, OpenVisionLab.Logging.LogLevel.Info, $"NATIVE_SHORTCUT_FORWARD key={input.Key} modifiers={input.Modifiers} repeat={input.IsRepeat} command={command?.GetType().Name} allowed={allowed}");
            if (!allowed) return;
            command.Execute(input);
            OpenVisionLab.Logging.OVLog.Write(OpenVisionLab.Logging.LogCategory.Main, OpenVisionLab.Logging.LogLevel.Info, $"NATIVE_SHORTCUT_RESULT key={input.Key} handled={input.Handled}");
            if (input.Handled) { e.Handled = true; e.SuppressKeyPress = true; }
        }

        private void OnNativeShortcutKeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            nativeShortcutKeys.Remove(e.KeyCode);
            if (e.KeyCode == System.Windows.Forms.Keys.Z || e.KeyCode == System.Windows.Forms.Keys.Y)
                OpenVisionLab.Logging.OVLog.Write(OpenVisionLab.Logging.LogCategory.Main, OpenVisionLab.Logging.LogLevel.Info, $"NATIVE_SHORTCUT_UP key={e.KeyCode} ctrl={e.Control} focus={nativeShortcutControl?.ContainsFocus}");
        }
        private void OnNativeShortcutFocusLeft(object sender, System.EventArgs e)
        {
            nativeShortcutKeys.Clear();
            OpenVisionLab.Logging.OVLog.Write(OpenVisionLab.Logging.LogCategory.Main, OpenVisionLab.Logging.LogLevel.Info, $"NATIVE_SHORTCUT_LEAVE handle={(nativeShortcutControl?.IsHandleCreated == true ? nativeShortcutControl.Handle.ToInt64() : 0)}");
        }

        private void WpfCanvasPanel_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            AttachViewModel(e.NewValue as WpfCanvasPanelViewModel);
            AttachShellViewModel();
        }

        private void EnsureDisplayAdjustmentWindow()
        {
            var owner = System.Windows.Window.GetWindow(this);
            if (owner == null || DisplayAdjustmentPopup != null) return;
            DisplayAdjustmentPopup = new CanvasDisplayAdjustmentWindow(owner, (System.Windows.FrameworkElement)owner.Content);
            System.Windows.NameScope.SetNameScope(DisplayAdjustmentPopup, System.Windows.NameScope.GetNameScope(this));
            DisplayAdjustmentPopup.Resources.MergedDictionaries.Add(owner.Resources);
            DisplayAdjustmentPopup.Resources.MergedDictionaries.Add(Resources);
            DisplayAdjustmentPopup.SetBinding(System.Windows.FrameworkElement.DataContextProperty, new System.Windows.Data.Binding(nameof(Tag)) { Source = this });
            DisplayAdjustmentPopup.SetBinding(System.Windows.Window.TopmostProperty, new System.Windows.Data.Binding(nameof(System.Windows.Window.Topmost)) { Source = owner });
            DisplayAdjustmentPopup.SetBinding(System.Windows.Window.TitleProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = DisplayAdjustmentDialogTitle });
            DisplayAdjustmentContentSlot.Child = null;
            DisplayAdjustmentPopup.Content = DisplayAdjustmentBackdrop;
            RegisterName("DisplayAdjustmentPopup", DisplayAdjustmentPopup);
            DisplayAdjustmentPopup.Opening += OnDisplayAdjustmentOpened;
            DisplayAdjustmentPopup.IsVisibleChanged += OnDisplayAdjustmentVisibilityChanged;
            BindDisplayAdjustmentWindow(DataContext as WpfCanvasPanelViewModel);
        }

        private void BindDisplayAdjustmentWindow(WpfCanvasPanelViewModel viewModel)
        {
            if (DisplayAdjustmentPopup == null) return;
            DisplayAdjustmentPopup.SetCurrentValue(CanvasDisplayAdjustmentWindow.IsOpenProperty, false);
            if (viewModel == null) System.Windows.Data.BindingOperations.ClearBinding(DisplayAdjustmentPopup, CanvasDisplayAdjustmentWindow.IsOpenProperty);
            else DisplayAdjustmentPopup.SetBinding(CanvasDisplayAdjustmentWindow.IsOpenProperty,
                new System.Windows.Data.Binding(nameof(WpfCanvasPanelViewModel.IsDisplayAdjustmentOpen)) { Source = viewModel, Mode = System.Windows.Data.BindingMode.TwoWay });
        }

        private void OnDisplayAdjustmentVisibilityChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) OnDisplayAdjustmentClosed(sender, System.EventArgs.Empty);
        }

        private void WpfCanvasPanel_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            DetachNativeShortcutInput();
            if (DisplayAdjustmentPopup == null) return;
            DisplayAdjustmentPopup.Dispose();
            DisplayAdjustmentPopup.Opening -= OnDisplayAdjustmentOpened;
            DisplayAdjustmentPopup.IsVisibleChanged -= OnDisplayAdjustmentVisibilityChanged;
            UnregisterName("DisplayAdjustmentPopup");
            DisplayAdjustmentPopup = null;
            DisplayAdjustmentContentSlot.Child = DisplayAdjustmentBackdrop;
        }

        private void OnDisplayAdjustmentOpened(object sender, System.EventArgs e)
        {
            displayAdjustmentPreviousFocus = Keyboard.FocusedElement;
            displayAdjustmentPreviousMoreExpanded = CanvasAdditionalActionsExpander.IsExpanded;
            CanvasAdditionalActionsExpander.SetCurrentValue(Expander.IsExpandedProperty, false);
            DisplayAdjustmentBackdrop.UpdateLayout();
            var openingWindow = DisplayAdjustmentPopup;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
            {
                if (ReferenceEquals(openingWindow, DisplayAdjustmentPopup) && openingWindow?.IsVisible == true) Keyboard.Focus(DisplayAdjustmentFooterCloseButton);
            }));
        }

        private void OnDisplayAdjustmentClosed(object sender, System.EventArgs e)
        {
            CanvasAdditionalActionsExpander.SetCurrentValue(Expander.IsExpandedProperty, displayAdjustmentPreviousMoreExpanded);
            var restoreFocus = displayAdjustmentPreviousFocus as System.Windows.UIElement ?? DisplayAdjustmentCanvasButton;
            displayAdjustmentPreviousFocus = null;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
            {
                if (System.Windows.Window.GetWindow(this)?.IsActive == true && restoreFocus.IsVisible && restoreFocus.IsEnabled) Keyboard.Focus(restoreFocus);
            }));
        }

        private void OnDisplayAdjustmentReset(object sender, System.Windows.RoutedEventArgs e)
        {
            DisplayAdjustmentPopup?.SetCurrentValue(CanvasDisplayAdjustmentWindow.IsOpenProperty, false);
        }

        private void MainCanvasView_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            ViewModel?.SetPolygonProgressCanvasWidth(e.NewSize.Width);
            UpdateCanvasOverlayPlacement();
        }

        private void AttachViewModel(WpfCanvasPanelViewModel viewModel)
        {
            if (ReferenceEquals(observedViewModel, viewModel))
            {
                ApplySmartMaskPopupVisibility();
                ApplyCanvasOverlayVisibility();
                return;
            }

            if (observedViewModel != null)
            {
                observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            observedViewModel = viewModel;
            // Popups are separate presentation windows, so their binding
            // source must be assigned explicitly instead of relying on the
            // parent UserControl DataContext inheritance.
            CanvasPolygonProgressPopup.DataContext = viewModel;
            CanvasPolygonProgress.DataContext = viewModel;
            BindDisplayAdjustmentWindow(viewModel);
            CanvasPolygonProgressCountText.Text = viewModel?.PolygonProgressCountText ?? string.Empty;
            if (observedViewModel != null)
            {
                observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            ApplySmartMaskPopupVisibility();
            ApplyCanvasOverlayVisibility();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.PropertyName)
                || e.PropertyName == nameof(WpfCanvasPanelViewModel.SmartMaskSessionVisibility)
                || e.PropertyName == nameof(WpfCanvasPanelViewModel.SmartMaskVisibility)
                || e.PropertyName == nameof(WpfCanvasPanelViewModel.AnnotationWorkspaceVisibility)
                || e.PropertyName == nameof(WpfCanvasPanelViewModel.PolygonProgressVisibility)
                || e.PropertyName == nameof(WpfCanvasPanelViewModel.PolygonProgressHorizontalOffset))
            {
                CanvasPolygonProgressCountText.Text = ViewModel?.PolygonProgressCountText ?? string.Empty;
                ApplySmartMaskPopupVisibility();
                ApplyCanvasOverlayVisibility();
            }
        }

        private void ApplySmartMaskPopupVisibility()
        {
            bool showPopup = ViewModel?.SmartMaskSessionVisibility == System.Windows.Visibility.Visible;
            CanvasSmartMaskOverlayLayer.SetCurrentValue(Popup.IsOpenProperty, showPopup);
            CanvasSmartMaskPromptBar.SetCurrentValue(
                Border.VisibilityProperty,
                showPopup ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed);
        }

        private void ApplyCanvasOverlayVisibility()
        {
            bool showWorkspace = observedShellViewModel?.IsLabelingStageActive == true
                && ViewModel?.AnnotationWorkspaceVisibility == System.Windows.Visibility.Visible;
            bool showPolygonProgress = showWorkspace
                && ViewModel?.PolygonProgressVisibility == System.Windows.Visibility.Visible;
            CanvasPolygonProgressPopup.SetCurrentValue(Popup.IsOpenProperty, showPolygonProgress);
            UpdateCanvasOverlayPlacement();
        }

        private void AttachShellViewModel()
        {
            WpfLabelingShellViewModel shellViewModel =
                System.Windows.Window.GetWindow(this)?.DataContext is WpfLabelingShellViewModels viewModels
                    ? viewModels.ShellViewModel
                    : null;
            if (ReferenceEquals(observedShellViewModel, shellViewModel))
            {
                return;
            }

            if (observedShellViewModel != null)
            {
                observedShellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            }

            observedShellViewModel = shellViewModel;
            if (observedShellViewModel != null)
            {
                observedShellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            }
        }

        private void ShellViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.PropertyName)
                || e.PropertyName == nameof(WpfLabelingShellViewModel.IsLabelingStageActive))
            {
                ApplyCanvasOverlayVisibility();
            }
        }

        private void UpdateCanvasOverlayPlacement()
        {
            double canvasWidth = MainCanvasView?.ActualWidth ?? 0D;
            double canvasHeight = MainCanvasView?.ActualHeight ?? 0D;
            if (canvasWidth <= 0D || canvasHeight <= 0D)
            {
                return;
            }

            CanvasPolygonProgressPopup.SetCurrentValue(
                Popup.HorizontalOffsetProperty,
                System.Math.Max(12D, canvasWidth - 202D));
            double smartMaskOffset = CanvasSmartMaskOverlayLayer.VerticalOffset;
            CanvasSmartMaskPromptBar.SetCurrentValue(MaxHeightProperty, System.Math.Min(220D, System.Math.Max(0D, canvasHeight - smartMaskOffset)));
            if (CanvasSmartMaskOverlayLayer.IsOpen)
            {
                CanvasSmartMaskOverlayLayer.SetCurrentValue(Popup.VerticalOffsetProperty, smartMaskOffset + 1D);
                CanvasSmartMaskOverlayLayer.SetCurrentValue(Popup.VerticalOffsetProperty, smartMaskOffset);
            }
        }

    }
}
