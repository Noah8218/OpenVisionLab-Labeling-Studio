using OpenVisionLab.ImageCanvas.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Stroke geometry is previewed by the GPU/FBO; status text is throttled so WPF
        // bindings do not compete with high-frequency brush MouseMove input.
        private static readonly long MaskStrokeStatusUpdateIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 8);

        private void ApplyMaskAnnotationStroke(CanvasImagePointEventArgs e, bool resetStroke)
        {
            if (e == null || activeImageSize.IsEmpty)
            {
                return;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                CompleteMaskAnnotationStroke();
                CancelMaskStrokePreviewCommitSwap();
                lastMaskStrokePoint = null;
                MainCanvasViewModel?.ClearMaskStrokePreview();
                SetYoloCommandStatus("마스크 스트로크를 초기화했습니다. 다시 드래그해 편집을 이어가세요.", isBusy: false);
                return;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return;
            }

            int radius = GetMaskBrushRadius();
            IReadOnlyList<System.Drawing.Point> centers = maskAnnotationService.BuildStrokeCenters(
                resetStroke ? null : lastMaskStrokePoint,
                e.ImagePoint,
                radius);
            lastMaskStrokePoint = e.ImagePoint;

            string actionName = activeAnnotationTool == WpfAnnotationTool.Brush ? "마스크 칠하기" : "마스크 지우기";
            if (resetStroke && activeMaskStrokeInProgress)
            {
                CompleteMaskAnnotationStroke();
            }

            if (!activeMaskStrokeInProgress)
            {
                CancelMaskStrokePreviewCommitSwap();
                // Match the old Viewer2D brush flow: MouseMove only feeds the GPU/FBO
                // edit preview, while MouseUp enqueues CPU MaskData/history work between strokes.
                activeMaskStrokeInProgress = true;
                activeMaskStrokeActionName = actionName;
                activeMaskStrokeSegmentIndices.Clear();
                activeMaskStrokeNeedsFullObjectRefresh = false;
                activeMaskStrokeCommitSession.Begin(
                    radius,
                    activeAnnotationTool,
                    FirstNonEmpty(GetSelectedClassName(), "Defect"));
                MainCanvasViewModel?.BeginMaskStrokePreview(
                    activeImageSize,
                    GetMaskCursorPreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                    activeAnnotationTool == WpfAnnotationTool.Eraser);
            }

            IReadOnlyList<System.Drawing.Point> previewCenters = AppendMaskStrokeCommitCenters(centers);
            if (previewCenters.Count == 0)
            {
                return;
            }

            MainCanvasViewModel?.AddMaskStrokePreview(
                previewCenters,
                radius,
                GetMaskCursorPreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                activeAnnotationTool == WpfAnnotationTool.Eraser);
            TryUpdateMaskStrokePreviewStatus(force: false);
        }

        private void TryUpdateMaskStrokePreviewStatus(bool force)
        {
            long now = Stopwatch.GetTimestamp();
            if (!force
                && lastMaskStrokeStatusUpdateTicks != 0
                && now - lastMaskStrokeStatusUpdateTicks < MaskStrokeStatusUpdateIntervalTicks)
            {
                return;
            }

            lastMaskStrokeStatusUpdateTicks = now;
            string action = activeAnnotationTool == WpfAnnotationTool.Brush ? "마스크 칠하기 미리보기" : "마스크 지우기 미리보기";
            SetModelStatus($"{action}: 스트로크 {activeMaskStrokeCommitSession.Count}점");
        }
    }
}
