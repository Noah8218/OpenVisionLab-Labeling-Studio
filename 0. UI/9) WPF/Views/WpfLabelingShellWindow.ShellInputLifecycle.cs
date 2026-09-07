using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Views;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using OpenVisionLab.Mvvm.Behaviors;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUiApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfUiApplicationThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;
using WpfUiFluentWindow = Wpf.Ui.Controls.FluentWindow;
using WpfUiWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
using OpenVisionLab;
using OpenVisionLab.Wpf.MessageDialogs;

namespace MvcVisionSystem
{
    // Responsibility group: shell keyboard input and window lifecycle.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ShellInputCommands
        // Shell-level keyboard shortcuts stay outside the constructor/field file so command routing is easier to audit.
        private void ExecuteShellPreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null || IsTextEditingElement(e.OriginalSource))
            {
                return;
            }

            if (e.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
            {
                e.Handled = CancelFourPointBoxDraft(updateStatus: true);
                if (e.Handled)
                {
                    return;
                }
            }

            if (e.Modifiers == ModifierKeys.None && e.Key == Key.Back)
            {
                e.Handled = RemoveLastFourPointBoxPoint();
                if (e.Handled)
                {
                    return;
                }
            }

            if ((e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    bool isRedoShortcut = (e.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    e.Handled = isRedoShortcut ? RedoWpfAnnotationHistory() : UndoWpfAnnotationHistory();
                    return;
                }

                if (e.Key == Key.Y)
                {
                    e.Handled = RedoWpfAnnotationHistory();
                    return;
                }

                AnnotationShortcut controlShortcut = AnnotationProductivityService.ResolveShortcut(
                    e.Key,
                    e.Modifiers);
                if (controlShortcut.Kind == WpfAnnotationShortcutKind.DuplicateSelected)
                {
                    e.Handled = TryDuplicateSelectedAnnotation();
                }

                return;
            }

            if (e.Modifiers != ModifierKeys.None)
            {
                return;
            }

            AnnotationShortcut shortcut = AnnotationProductivityService.ResolveShortcut(e.Key, e.Modifiers);
            if (TryExecuteAnnotationProductivityShortcut(shortcut))
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down || e.Key == Key.Right)
            {
                e.Handled = TryOpenAdjacentQueueImage(1);
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Left)
            {
                e.Handled = TryOpenAdjacentQueueImage(-1);
            }
        }

        private bool TryExecuteAnnotationProductivityShortcut(AnnotationShortcut shortcut)
        {
            if (shortcut == null || shortcut.Kind == WpfAnnotationShortcutKind.None)
            {
                return false;
            }

            switch (shortcut.Kind)
            {
                case WpfAnnotationShortcutKind.SelectTool:
                    WpfAnnotationToolItem selectedTool = ResolveSelectableAnnotationTool(shortcut.Tool);
                    if (selectedTool == null)
                    {
                        return false;
                    }

                    ApplyAnnotationToolSelection(selectedTool);
                    return true;

                case WpfAnnotationShortcutKind.SelectClass:
                    if (CanvasPanelViewModel?.TrySelectLabelClassByShortcut(shortcut.ClassIndex) != true)
                    {
                        return false;
                    }

                    CanvasLabelClass_SelectionChanged(
                        CanvasLabelClassListBox,
                        CanvasPanelViewModel.SelectedLabelClass);
                    SetModelStatus($"클래스 단축키: {CanvasPanelViewModel.SelectedLabelClass.Text}");
                    return true;

                case WpfAnnotationShortcutKind.OpenClassCatalog:
                    ShowClassCatalogWorkflowView(WpfShellWorkflowStage.Labeling);
                    return true;

                case WpfAnnotationShortcutKind.RepeatLast:
                    return TryRepeatLastAnnotationToolAndClass();

                case WpfAnnotationShortcutKind.ToggleShortcutHelp:
                    CanvasPanelViewModel?.ToggleShortcutHelp();
                    return true;

                default:
                    return false;
            }
        }

        private bool TryRepeatLastAnnotationToolAndClass()
        {
            if (CanvasPanelViewModel?.TryGetRepeatSelection(out WpfAnnotationTool tool, out string className) != true)
            {
                return false;
            }

            WpfAnnotationToolItem selectedTool = ResolveSelectableAnnotationTool(tool);
            if (selectedTool == null)
            {
                return false;
            }

            CanvasPanelViewModel.SelectLabelClass(className);
            CanvasLabelClass_SelectionChanged(CanvasLabelClassListBox, CanvasPanelViewModel.SelectedLabelClass);
            ApplyAnnotationToolSelection(selectedTool);
            SetModelStatus($"마지막 라벨링 반복: {selectedTool.Text} / {className}");
            return true;
        }

        private static bool IsTextEditingElement(object source)
        {
            return source is TextBox
                || source is ComboBox
                || source is System.Windows.Controls.Primitives.RangeBase
                || source is System.Windows.Controls.Primitives.TextBoxBase;
        }
        #endregion

        #region ShellLifecycle
        // Window lifecycle is invoked through WpfLabelingShellViewModel commands, not XAML event handlers.
        private void ExecuteLoadedCommand()
        {
            Dispatcher.BeginInvoke(new Action(ApplyDeferredShellStartup), DispatcherPriority.ApplicationIdle);
        }

        private void ApplyDeferredShellStartup()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            RefreshYoloStatus();
            _ = RefreshYoloSettingsPanelAsync();
            if (!TryHandleCrashRecoveryOnStartup())
            {
                TryLoadStartupSampleImage();
            }
            SetPythonStatus(OpenVisionLanguageService.T("WpfShell.Status.InferenceWaiting"));
            AppendLog("시작 완료. 추론은 사용자가 명시적으로 실행할 때만 시작합니다.");
        }

        // The language event is subscribed and released with the Window. Its
        // fan-out is therefore part of this lifecycle adapter rather than a
        // second Shell partial or a speculative localization service.
        private void LanguageViewModel_LanguageChanged(object sender, EventArgs e)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(ApplyLanguageChangedOnUi));
                return;
            }

            ApplyLanguageChangedOnUi();
        }

        private void ApplyLanguageChangedOnUi()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            ShellViewModel?.RefreshLocalizedPresentation();
            ClassCatalogViewModel?.RefreshLocalizedPresentation();
            ImageQueueViewModel?.RefreshLocalizedPresentation(imageQueueItems);
            StatusBarViewModel?.RefreshLocalizedPresentation();
            CanvasPanelControl?.RefreshLocalizedViewerStatus();
            if (ImageQueueFilterBox?.ItemsSource is IEnumerable<WpfImageQueueFilterOption> filterOptions)
            {
                foreach (WpfImageQueueFilterOption filterOption in filterOptions)
                {
                    filterOption?.RefreshLocalizedPresentation();
                }
            }
            RefreshShellDatasetContext();
            UpdateImageQueueStatusText();
            UpdateYoloCommandButtons();
            LocalizationTextRuntimeService.RefreshAll();
            CanvasPanelControl?.RefreshLocalizedViewerStatus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!IsLoaded)
            {
                isApplicationCloseApproved = true;
                base.OnClosing(e);
                return;
            }

            if (!isApplicationCloseApproved && !TryApproveApplicationClose())
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private bool TryApproveApplicationClose()
        {
            if (isApplicationClosePromptOpen)
            {
                return false;
            }

            ApplicationClosePlan plan = BuildApplicationClosePlan();
            if (!plan.RequiresPrompt)
            {
                isApplicationCloseApproved = true;
                return true;
            }

            isApplicationClosePromptOpen = true;
            try
            {
                WpfApplicationCloseDecision decision = ShowApplicationClosePrompt(plan);
                return ApplyApplicationCloseDecision(decision);
            }
            finally
            {
                isApplicationClosePromptOpen = false;
            }
        }

        private ApplicationClosePlan BuildApplicationClosePlan()
        {
            return applicationClosePolicyService.Build(BuildApplicationCloseState());
        }

        private ApplicationCloseState BuildApplicationCloseState()
        {
            return new ApplicationCloseState
            {
                HasUnsavedAnnotations =
                    annotationDirtyState.IsDirty
                    || HasPendingMaskStrokeCommitWork(),
                UnsavedAnnotationReason = annotationDirtyState.Reason,
                PendingCandidateCount = candidateReviewState.PendingCount,
                ActiveWorkNames = applicationClosePolicyService.GetActiveWorkNames(new ApplicationCloseWorkState
                {
                    IsCreatingSmartMask = isCreatingSmartMask,
                    IsDetecting = isDetecting,
                    IsBatchDetectionRunning = isBatchDetectionRunning,
                    IsExternalYoloDatasetIntakeRunning = isExternalYoloDatasetIntakeRunning,
                    IsExternalEvaluationDataAuditRunning = isExternalEvaluationDataAuditRunning,
                    IsHistoricalSegmentationRemediationAuditRunning = isHistoricalSegmentationRemediationAuditRunning,
                    IsTrainingRunning = isTrainingCommandRunning
                        || isTrainingWorkflowRunning
                        || TrainingProgressPresentationService.IsTrainingStopAvailable(global.GetPythonCommunicationStatusSnapshot()),
                    IsYoloEnvironmentCommandRunning = isYoloEnvironmentCommandRunning,
                    IsModelComparisonRunning = isModelComparisonRunning,
                    IsSegmentationAdapterComparisonRunning = isSegmentationAdapterComparisonRunning,
                    IsAnomalyEvaluationRunning = isAnomalyEvaluationRunning
                }),
                ActiveImagePath = activeImagePath
            };
        }

        private WpfApplicationCloseDecision ShowApplicationClosePrompt(ApplicationClosePlan plan)
        {
            bool canSave = plan.PromptKind == WpfApplicationClosePromptKind.SaveDiscardCancel;
            WpfMessageDialogResult result = WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = plan.Title,
                Message = plan.Message,
                Details = plan.Details,
                Kind = WpfMessageDialogKind.Warning,
                Buttons = canSave
                    ? WpfMessageDialogButtons.YesNoCancel
                    : WpfMessageDialogButtons.OKCancel,
                DefaultResult = WpfMessageDialogResult.Cancel,
                PrimaryButtonText = plan.PrimaryButtonText,
                SecondaryButtonText = plan.SecondaryButtonText,
                TertiaryButtonText = plan.TertiaryButtonText,
                MaxWidth = 620D
            });

            if (canSave)
            {
                return result switch
                {
                    WpfMessageDialogResult.Yes => WpfApplicationCloseDecision.SaveAndClose,
                    WpfMessageDialogResult.No => WpfApplicationCloseDecision.DiscardAndClose,
                    _ => WpfApplicationCloseDecision.Cancel
                };
            }

            return result == WpfMessageDialogResult.OK
                ? WpfApplicationCloseDecision.DiscardAndClose
                : WpfApplicationCloseDecision.Cancel;
        }

        private bool ApplyApplicationCloseDecision(WpfApplicationCloseDecision decision)
        {
            if (decision == WpfApplicationCloseDecision.Cancel)
            {
                return false;
            }

            if (decision == WpfApplicationCloseDecision.SaveAndClose)
            {
                string failureDetails = string.Empty;
                bool saved;
                try
                {
                    saved = SaveCurrentAnnotations(out _);
                }
                catch (Exception ex)
                {
                    saved = false;
                    failureDetails = ex.Message;
                }

                if (!saved)
                {
                    WpfMessageDialog.Show(this, new WpfMessageDialogOptions
                    {
                        Title = "라벨을 저장하지 못했습니다",
                        Message = "현재 이미지의 라벨 저장에 실패하여 창을 닫지 않았습니다.",
                        Details = string.IsNullOrWhiteSpace(failureDetails)
                            ? "데이터셋 출력 경로와 파일 쓰기 권한을 확인한 뒤 다시 시도하세요."
                            : failureDetails,
                        Kind = WpfMessageDialogKind.Warning,
                        Buttons = WpfMessageDialogButtons.OK,
                        PrimaryButtonText = "확인"
                    });
                    return false;
                }
            }

            isApplicationCloseApproved = true;
            return true;
        }

        #endregion

        #region Dispose
        private void ExecuteClosedCommand()
        {
            // Invalidate queued queue-status workers before disposing the Shell-owned state.
            // A worker may still finish its label scan, but it must not persist or marshal
            // its result after the owning Shell has closed.
            imageQueueReviewStatusRefreshCoordinator.Dispose();
            DetachShellEventSubscriptions();
            DisposeShellPersistenceAndChildWindows();
            StopShellTimersAndQueueWorkers();
            DisposeShellOperationCancellations();
            ResetShellOperationState();
            global.StopPythonModelClientConnection();
            DisposeShellVisualState();
        }

        private void DetachShellEventSubscriptions()
        {
            viewModels.LanguageViewModel.LanguageChanged -= LanguageViewModel_LanguageChanged;
            LearningWorkflowViewModel.PropertyChanged -= LearningWorkflowViewModel_PropertyChanged;
            MainCanvasViewModel.RoiAdded -= MainCanvasViewModel_RoiAdded;
            MainCanvasViewModel.RoiEditingCompleted -= MainCanvasViewModel_RoiEditingCompleted;
            MainCanvasViewModel.RoiMouseUp -= MainCanvasViewModel_RoiMouseUp;
            MainCanvasViewModel.RemoveRoiRequested -= MainCanvasViewModel_RemoveRoiRequested;
            MainCanvasViewModel.DetectionOverlayClicked -= MainCanvasViewModel_DetectionOverlayClicked;
            MainCanvasViewModel.ImagePointClicked -= MainCanvasViewModel_ImagePointClicked;
            MainCanvasViewModel.ImagePointHovered -= MainCanvasViewModel_ImagePointHovered;
            MainCanvasViewModel.ImagePointMoved -= MainCanvasViewModel_ImagePointMoved;
            MainCanvasViewModel.ImagePointReleased -= MainCanvasViewModel_ImagePointReleased;
            MainCanvasViewModel.RenderDiagnosticsCaptured -= MainCanvasViewModel_RenderDiagnosticsCaptured;
            MainCanvasView.SizeChanged -= MainCanvasView_SizeChanged;
        }

        private void DisposeShellPersistenceAndChildWindows()
        {
            DiscardCrashRecoveryJournal();
            crashRecoveryJournalWriteCoordinator.Dispose();
            SaveWorkspaceLayoutSettings();
            CloseModelBenchmarkWindow();
            CloseDatasetHealthWindow();
            CloseDatasetInterchangeWindow();
            CloseEnvironmentSetupCenterWindow();
        }

        private void StopShellTimersAndQueueWorkers()
        {
            StopInferenceStatusPulse();
            StopTrainingStatusPolling();
            shellTimers.Dispose();
            imageDecodePreloadService.CancelAndWait(TimeSpan.FromSeconds(2));
            CancelImageQueueCatalogLoad(waitForCompletion: true);
            imageQueueCatalogLoadCoordinator.Dispose();
            CancelImageQueueDetailRefresh(waitForCompletion: true);
            imageQueueDetailRefreshCoordinator.Dispose();
        }

        private void DisposeShellOperationCancellations()
        {
            projectRecipeSessionCts.Cancel();
            projectRecipeSessionCts.Dispose();
            yoloSettingsRefreshCancellation.Cancel();
            yoloSettingsRefreshCancellation.Dispose();
            smartMaskCancellation?.Cancel();
            smartMaskCancellation?.Dispose();
            smartMaskCancellation = null;
            interactiveDetectionCts?.Cancel();
            interactiveDetectionCts?.Dispose();
            interactiveDetectionCts = null;
            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = null;
            modelComparisonCts?.Cancel();
            modelComparisonCts?.Dispose();
            modelComparisonCts = null;
            segmentationAdapterComparisonCts?.Cancel();
            segmentationAdapterComparisonCts?.Dispose();
            segmentationAdapterComparisonCts = null;
            anomalyEvaluationCts?.Cancel();
            anomalyEvaluationCts?.Dispose();
            anomalyEvaluationCts = null;
            externalYoloDatasetIntakeCts?.Cancel();
            externalYoloDatasetIntakeCts?.Dispose();
            externalYoloDatasetIntakeCts = null;
            externalEvaluationDataAuditCts?.Cancel();
            externalEvaluationDataAuditCts?.Dispose();
            externalEvaluationDataAuditCts = null;
            historicalSegmentationRemediationAuditCts?.Cancel();
            historicalSegmentationRemediationAuditCts?.Dispose();
            historicalSegmentationRemediationAuditCts = null;
            pythonWorkerOperationCts?.Cancel();
            pythonWorkerOperationCts?.Dispose();
            pythonWorkerOperationCts = null;
            trainingCommandCts?.Cancel();
            trainingCommandCts?.Dispose();
            trainingCommandCts = null;
        }

        private void ResetShellOperationState()
        {
            isCreatingSmartMask = false;
            isBatchDetectionRunning = false;
            isDetecting = false;
            isExternalYoloDatasetIntakeRunning = false;
            isExternalEvaluationDataAuditRunning = false;
            isHistoricalSegmentationRemediationAuditRunning = false;
            isSegmentationAdapterComparisonRunning = false;
            isModelComparisonRunning = false;
            isAnomalyEvaluationRunning = false;
            isYoloEnvironmentCommandRunning = false;
            isTrainingCommandRunning = false;
            isTrainingWorkflowRunning = false;
        }

        private void DisposeShellVisualState()
        {
            imageDecodeCacheService.Clear();
            activeImageBitmap?.Dispose();
            activeImageBitmap = null;
            viewModels.Dispose();
        }
        #endregion

    }
}
