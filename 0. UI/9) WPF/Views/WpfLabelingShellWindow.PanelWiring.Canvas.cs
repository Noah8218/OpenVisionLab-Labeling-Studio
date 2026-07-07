using OpenVisionLab.Mvvm.Behaviors;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Canvas panel wiring owns toolbar commands and workflow context text only.
        private void ConfigureCanvasPanelCommands()
        {
            CanvasPanelViewModel.ConfigureCommands(
                ExecuteFitCanvasCommand,
                ExecuteActualSizeCanvasCommand,
                ExecutePanCanvasCommand,
                ExecuteFocusCandidateCommand,
                ExecuteResetAiOverlayCommand);
            CanvasPanelViewModel.ConfigureCandidateReviewCommands(
                ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand,
                ExecuteFocusCurrentLabelCommand,
                ExecuteConfirmSelectedCandidateCommand,
                ExecuteSkipSelectedCandidateCommand);
            CanvasPanelViewModel.ConfigureAnnotationTools(
                LearningWorkflowViewModel.VisibleAnnotationTools,
                LearningWorkflowViewModel.SelectedTool,
                ExecuteCanvasAnnotationToolSelectionChanged);
            CanvasPanelViewModel.ConfigureAnnotationCommands(
                ExecuteUndoAnnotationCommand,
                ExecuteRedoAnnotationCommand,
                ExecuteDeleteObjectCommand);
            CanvasPanelViewModel.ConfigureAnnotationSaveCommand(
                ExecuteSaveAnnotationsCommand);
            CanvasPanelViewModel.ConfigureNoObjectCompletionCommand(
                ExecuteCompleteNoObjectAndNextCommand);
            CanvasPanelViewModel.ConfigureLabelClassSelection(
                selected => CanvasLabelClass_SelectionChanged(CanvasLabelClassListBox, selected),
                () => ShowClassCatalogWorkflowView(WpfShellWorkflowStage.Labeling));
            CanvasPanelViewModel.ConfigureDisplayModeSelection(
                ExecuteCanvasDisplayModeSelectionChanged);
            CanvasPanelViewModel.ConfigureBrushSizeCommands(
                ExecuteDecreaseBrushSizeCommand,
                ExecuteIncreaseBrushSizeCommand);
            SyncCanvasBrushSizeFromWorkflow();
            RefreshCanvasWorkflowContext();
            RefreshAttachedCommandBindings(
                CanvasAnnotationToolListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(
                CanvasLabelClassListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(
                CanvasDisplayModeListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RefreshCanvasAnnotationToolScope()
        {
            CanvasPanelViewModel?.ConfigureAnnotationTools(
                LearningWorkflowViewModel?.VisibleAnnotationTools,
                LearningWorkflowViewModel?.SelectedTool,
                ExecuteCanvasAnnotationToolSelectionChanged);
        }

        private void RefreshCanvasWorkflowContext()
        {
            // The selected guide step can lag behind direct canvas tool changes, so the
            // strip is composed from the live canvas state that the operator is using.
            WpfLearningStepItem selectedStep = LearningWorkflowViewModel?.SelectedStep;
            WpfAnnotationToolItem selectedTool = CanvasPanelViewModel?.SelectedAnnotationTool
                ?? LearningWorkflowViewModel?.SelectedTool;
            WpfLearningStep effectiveStep = ResolveEffectiveCanvasWorkflowStep(selectedStep, selectedTool);
            string stepText = BuildCanvasWorkflowStepText(effectiveStep, selectedStep);
            string toolText = BuildCanvasWorkflowToolText(effectiveStep, selectedTool);
            string actionText = BuildCanvasWorkflowActionText(effectiveStep, selectedTool);
            CanvasPanelViewModel?.SetWorkflowContext(
                stepText,
                toolText,
                actionText);
            LearningWorkflowViewModel?.SetLiveLabelingTask(stepText, toolText, actionText);
        }

        private WpfLearningStep ResolveEffectiveCanvasWorkflowStep(WpfLearningStepItem selectedStep, WpfAnnotationToolItem selectedTool)
        {
            if (currentWorkflowMode == WorkflowMode.Inference)
            {
                return pendingDetectionCandidates.Count > 0
                    ? WpfLearningStep.Review
                    : WpfLearningStep.Infer;
            }

            if (activeImageSize.IsEmpty)
            {
                return WpfLearningStep.Sample;
            }

            WpfAnnotationTool? selectedToolKind = selectedTool?.Tool;
            if (IsCanvasLabelingTool(selectedToolKind) || IsCanvasLabelingTool(activeAnnotationTool))
            {
                return WpfLearningStep.Label;
            }

            if (!string.IsNullOrWhiteSpace(annotationDirtyReason) || HasCanvasLabelObjects())
            {
                return WpfLearningStep.Save;
            }

            return selectedStep?.Step == WpfLearningStep.Sample
                ? WpfLearningStep.Label
                : selectedStep?.Step ?? WpfLearningStep.Label;
        }

        private static string BuildCanvasWorkflowStepText(WpfLearningStep effectiveStep, WpfLearningStepItem selectedStep)
        {
            if (selectedStep?.Step == effectiveStep && !string.IsNullOrWhiteSpace(selectedStep.Text))
            {
                return selectedStep.Text;
            }

            return effectiveStep switch
            {
                WpfLearningStep.Label => "라벨",
                WpfLearningStep.Infer => "추론",
                WpfLearningStep.Review => "검토",
                WpfLearningStep.Save => "저장",
                _ => "샘플"
            };
        }

        private static string BuildCanvasWorkflowToolText(WpfLearningStep effectiveStep, WpfAnnotationToolItem selectedTool)
        {
            return effectiveStep switch
            {
                WpfLearningStep.Sample => "\uC774\uBBF8\uC9C0 \uD050",
                WpfLearningStep.Infer => "\uD604\uC7AC \uAC80\uC0AC",
                WpfLearningStep.Review => "AI \uD6C4\uBCF4 \uAC80\uD1A0",
                _ => selectedTool?.Text
            };
        }

        private string BuildCanvasWorkflowActionText(WpfLearningStep effectiveStep, WpfAnnotationToolItem selectedTool)
        {
            if (activeImageSize.IsEmpty)
            {
                return "이미지 큐에서 작업할 이미지를 열고 첫 라벨을 시작하세요.";
            }

            if (effectiveStep == WpfLearningStep.Label)
            {
                return selectedTool?.Tool switch
                {
                    WpfAnnotationTool.Rectangle => "박스 라벨링: 캔버스에서 드래그하고 클래스가 맞는지 확인한 뒤 라벨 저장을 누르세요.",
                    WpfAnnotationTool.Ellipse => "원형 라벨링: 드래그해 영역을 만들고 클래스와 위치를 확인한 뒤 라벨 저장을 누르세요.",
                    WpfAnnotationTool.Polygon => "폴리곤 라벨링: 꼭짓점을 찍고 마지막 점에서 마무리하세요.",
                    WpfAnnotationTool.Brush => "마스크 칠하기: 드래그하고 놓은 뒤 결과를 확인한 다음 라벨 저장을 누르세요.",
                    WpfAnnotationTool.Eraser => "마스크 편집: 마스크 위를 드래그해 지울 영역을 정리하세요.",
                    WpfAnnotationTool.PanZoom => "화면 이동: 이미지를 끌어 위치를 맞춘 뒤 라벨 도구로 돌아가세요.",
                    _ when !string.IsNullOrWhiteSpace(annotationDirtyReason) => "저장 필요: 라벨 저장 버튼을 눌러 현재 이미지의 라벨을 저장하세요.",
                    _ when HasCanvasLabelObjects() => "객체 검토: 라벨 위치와 클래스를 확인한 뒤 라벨 저장을 누르세요.",
                    _ => "라벨링 시작: 빠른 도구에서 박스나 브러시를 선택하고 캔버스에 그리세요."
                };
            }

            return effectiveStep switch
            {
                WpfLearningStep.Sample => "이미지 선택: 이미지 큐에서 작업할 이미지를 여세요.",
                WpfLearningStep.Infer => "추론 실행: 현재 검사로 AI 후보를 만들고 검토 탭에서 확인하세요.",
                WpfLearningStep.Review => "AI 후보 검토: 확정, 전체 확정, 또는 스킵하세요.",
                WpfLearningStep.Save when !string.IsNullOrWhiteSpace(annotationDirtyReason) => "저장 필요: 라벨 저장 버튼으로 현재 라벨을 파일에 반영하세요.",
                WpfLearningStep.Save => "저장 완료: 이미지 큐의 다음 버튼으로 이어서 작업하세요.",
                _ => "다음 작업을 선택하세요."
            };
        }

        private static bool IsCanvasLabelingTool(WpfAnnotationTool? tool)
            => tool == WpfAnnotationTool.Rectangle
                || tool == WpfAnnotationTool.Ellipse
                || tool == WpfAnnotationTool.Polygon
                || tool == WpfAnnotationTool.Brush
                || tool == WpfAnnotationTool.Eraser;

        private bool HasCanvasLabelObjects()
            => GetCanvasLabelObjectCount() > 0;

        private int GetCanvasLabelObjectCount()
            => manualRois.Count + GetVisibleManualSegmentCount() + confirmedDetectionCandidates.Count;

        private void RegisterCanvasPanelNames()
        {
            ConfigureCanvasPanelCommands();
            RegisterCanvasName(nameof(MainCanvasView), MainCanvasView);
            RegisterCanvasName(nameof(CanvasAnnotationToolListBox), CanvasAnnotationToolListBox);
            RegisterCanvasName(nameof(CanvasLabelClassListBox), CanvasLabelClassListBox);
            RegisterCanvasName(nameof(CanvasDisplayModeListBox), CanvasDisplayModeListBox);
            RegisterCanvasName(nameof(CanvasWorkflowContextStrip), CanvasWorkflowContextStrip);
            RegisterCanvasName(nameof(CanvasCurrentStepText), CanvasCurrentStepText);
            RegisterCanvasName(nameof(CanvasCurrentToolText), CanvasCurrentToolText);
            RegisterCanvasName(nameof(CanvasNextActionText), CanvasNextActionText);
            RegisterCanvasName(nameof(CanvasLayerVisibilityStrip), CanvasLayerVisibilityStrip);
            RegisterCanvasName(nameof(CanvasLayerModeTitleText), CanvasLayerModeTitleText);
            RegisterCanvasName(nameof(CanvasLayerModeDetailText), CanvasLayerModeDetailText);
            RegisterCanvasName(nameof(CanvasLabelLayerText), CanvasLabelLayerText);
            RegisterCanvasName(nameof(CanvasInferenceLayerText), CanvasInferenceLayerText);
            RegisterCanvasName(nameof(CanvasSaveAnnotationButton), CanvasSaveAnnotationButton);
            RegisterCanvasName(nameof(CanvasCompleteNoObjectButton), CanvasCompleteNoObjectButton);
            RegisterCanvasName(nameof(CanvasAnnotationSaveStateCard), CanvasAnnotationSaveStateCard);
            RegisterCanvasName(nameof(CanvasAnnotationSaveStatusTitleText), CanvasAnnotationSaveStatusTitleText);
            RegisterCanvasName(nameof(CanvasAnnotationSaveStatusDetailText), CanvasAnnotationSaveStatusDetailText);
            RegisterCanvasName(nameof(CanvasActiveLabelClassCard), CanvasActiveLabelClassCard);
            RegisterCanvasName(nameof(CanvasActiveLabelClassTitleText), CanvasActiveLabelClassTitleText);
            RegisterCanvasName(nameof(CanvasActiveLabelClassDetailText), CanvasActiveLabelClassDetailText);
            RegisterCanvasName(nameof(CanvasOpenClassCatalogButton), CanvasOpenClassCatalogButton);
            RegisterCanvasName(nameof(FitCanvasButton), FitCanvasButton);
            RegisterCanvasName(nameof(ActualSizeCanvasButton), ActualSizeCanvasButton);
            RegisterCanvasName(nameof(PanCanvasButton), PanCanvasButton);
            RegisterCanvasName(nameof(FocusCandidateCanvasButton), FocusCandidateCanvasButton);
            RegisterCanvasName(nameof(ResetAiOverlayCanvasButton), ResetAiOverlayCanvasButton);
            RegisterCanvasName(nameof(DetectionResultOverlay), DetectionResultOverlay);
            RegisterCanvasName(nameof(DetectionOverlayTitleText), DetectionOverlayTitleText);
            RegisterCanvasName(nameof(DetectionOverlaySummaryText), DetectionOverlaySummaryText);
            RegisterCanvasName(nameof(DetectionOverlaySelectedBorder), DetectionOverlaySelectedBorder);
            RegisterCanvasName(nameof(DetectionOverlaySelectedText), DetectionOverlaySelectedText);
            RegisterCanvasName(nameof(DetectionOverlayDetailText), DetectionOverlayDetailText);
        }

        private void RegisterCanvasName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }
    }
}
