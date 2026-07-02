using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Workflow command handlers are grouped away from the shell view plumbing so labeling flow changes are easier to audit.
        private void ExecuteLoadSampleCommand()
        {
            TryLoadStartupSampleImage();
        }

        private void ExecuteAddSampleRoiCommand()
        {
            if (activeImageSize.IsEmpty)
            {
                AppendLog("박스 라벨을 추가하려면 이미지를 먼저 불러오세요.");
                return;
            }

            int width = Math.Max(20, activeImageSize.Width / 5);
            int height = Math.Max(20, activeImageSize.Height / 5);
            int x = Math.Max(0, (activeImageSize.Width - width) / 2);
            int y = Math.Max(0, (activeImageSize.Height - height) / 2);
            var roi = new DrawingRectangle(x, y, width, height);

            RegisterAnnotationHistoryBeforeChange("가이드 박스 추가");
            manualRois.Add(roi);
            manualRoiClassNames.Add(FirstNonEmpty(GetSelectedClassName(), "Defect"));
            manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
            manualRoiOverlayIds.Add(string.Empty);
            RedrawReviewRois();
            RefreshObjectList();
            ShowSavedLabelsWorkflowView();
            AppendLog($"박스 라벨 추가: {roi.X},{roi.Y},{roi.Width},{roi.Height}");
        }

        private void ExecuteSaveAnnotationsCommand()
        {
            if (SaveCurrentAnnotations(out int savedCount))
            {
                MarkActiveImageConfirmed();
                AppendLog($"YOLO 라벨 저장. 객체:{savedCount}  {BuildLabelPathSummary()}");
                return;
            }

            AppendLog("저장할 박스 라벨 또는 확정 후보가 없습니다.");
        }

        private void ExecuteCompleteNoObjectAndNextCommand()
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                AppendLog("\uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD560 \uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC5F4\uC5B4\uC8FC\uC138\uC694.");
                return;
            }

            if (pendingDetectionCandidates.Count > 0)
            {
                ShowCandidateReviewWorkflowView();
                AppendLog($"\uB0A8\uC740 AI \uD6C4\uBCF4 {pendingDetectionCandidates.Count}\uAC1C\uB97C \uBA3C\uC800 \uD655\uC815\uD558\uAC70\uB098 \uC228\uAE34 \uB4A4 \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694.");
                return;
            }

            if (HasCanvasLabelObjects())
            {
                ShowSavedLabelsWorkflowView();
                AppendLog("\uD604\uC7AC \uC774\uBBF8\uC9C0\uC5D0 \uB77C\uBCA8\uB41C \uAC1D\uCCB4\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC\uD558\uB824\uBA74 \uAE30\uC874 \uB77C\uBCA8\uC744 \uBA3C\uC800 \uC0AD\uC81C\uD558\uC138\uC694.");
                return;
            }

            if (!SaveCurrentEmptyAnnotations())
            {
                AppendLog("\uAC1D\uCCB4 \uC5C6\uC74C \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC774\uBBF8\uC9C0\uC640 \uC800\uC7A5 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            MarkActiveImageNoCandidate();
            RefreshYoloTrainingStepCompletion();
            AppendLog($"\uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC644\uB8CC: {BuildLabelPathSummary()}");
            if (!TryOpenNextIncompleteQueueImage())
            {
                FinishQueueCompletionAndGuideDatasetCheck();
            }
        }
    }
}
