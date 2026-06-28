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
            ObjectsReviewTab.IsSelected = true;
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
    }
}
