using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingColor = System.Drawing.Color;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Class-aware drawing policy stays in the shell because the canvas library should not know labeling class names.
        private bool ShouldDrawOverExistingRoiForCurrentClass(CanvasRect<float> roiRect)
        {
            if (roiRect == null || string.IsNullOrWhiteSpace(roiRect.UniqueId))
            {
                return false;
            }

            string currentClass = ClassCatalogService.NormalizeClassName(GetSelectedClassName());
            if (string.IsNullOrWhiteSpace(currentClass))
            {
                return false;
            }

            int index = FindManualRoiIndexByOverlayId(roiRect.UniqueId);
            if (index < 0 || index >= manualRoiClassNames.Count)
            {
                return false;
            }

            string existingClass = ClassCatalogService.NormalizeClassName(manualRoiClassNames[index]);
            return !string.IsNullOrWhiteSpace(existingClass)
                && !string.Equals(currentClass, existingClass, StringComparison.OrdinalIgnoreCase);
        }

        private DrawingColor GetClassDrawColor(string className)
        {
            CClassItem classItem = EnsureClassItem(FirstNonEmpty(className, "Defect"));
            return classItem?.DrawColor ?? DrawingColor.FromArgb(34, 197, 94);
        }

        private DrawingColor GetManualRoiDrawColor(int index)
            => GetClassDrawColor(GetManualRoiClassName(index));

        private string ResolveNewManualRoiClassName(CanvasRect<float> roiRect)
        {
            string copiedClassName = ClassCatalogService.NormalizeClassName(roiRect?.UserTag);
            return FirstNonEmpty(copiedClassName, GetSelectedClassName(), "Defect");
        }

        private void ApplyManualRoiOverlayColor(int index, bool refreshImmediately = false)
        {
            if (index < 0 || index >= manualRois.Count)
            {
                return;
            }

            string className = GetManualRoiClassName(index);
            MainCanvasViewModel?.SetRoiOverlayUserTag(
                GetManualRoiOverlayId(index),
                className);
            MainCanvasViewModel?.SetRoiOverlayColor(
                GetManualRoiOverlayId(index),
                GetClassDrawColor(className),
                refreshImmediately);
        }

        // Canvas annotation synchronization stays separate from tool input handling so ROI/overlay model mutations are easy to audit.
        private void MainCanvasViewModel_RoiAdded(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (e?.RoiRect == null || activeImageSize.IsEmpty)
            {
                return;
            }

            DrawingRectangle bounds = ConvertCanvasRectToImageBounds(e.RoiRect);
            if (bounds.IsEmpty)
            {
                return;
            }

            string overlayId = e.RoiRect.UniqueId ?? string.Empty;
            int existingIndex = FindManualRoiIndexByOverlayId(overlayId);
            if (existingIndex >= 0)
            {
                if (manualRois[existingIndex] != bounds || GetManualRoiShapeKind(existingIndex) != e.RoiRect.ShapeKind)
                {
                    RegisterAnnotationHistoryBeforeChange("박스 수정");
                }

                manualRois[existingIndex] = bounds;
                manualRoiShapeKinds[existingIndex] = e.RoiRect.ShapeKind;
                e.RoiRect.UserTag = GetManualRoiClassName(existingIndex);
                ApplyManualRoiOverlayColor(existingIndex);
            }
            else
            {
                RegisterAnnotationHistoryBeforeChange("박스 추가");
                string className = ResolveNewManualRoiClassName(e.RoiRect);
                e.RoiRect.UserTag = className;
                manualRois.Add(bounds);
                manualRoiClassNames.Add(className);
                manualRoiShapeKinds.Add(e.RoiRect.ShapeKind);
                manualRoiOverlayIds.Add(overlayId);
                ApplyManualRoiOverlayColor(manualRois.Count - 1);
            }

            RefreshObjectListWithSelection(CreateManualRoiSelection(e.RoiRect));
            ShowSavedLabelsWorkflowView();
            string shapeName = FormatManualRoiShapeName(e.RoiRect.ShapeKind);
            SetModelStatus($"라벨 추가: {shapeName} {WpfCandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            AppendLog($"라벨 추가({shapeName}): {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}");
        }

        private void MainCanvasViewModel_RoiEditingCompleted(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (e?.RoiRect == null || activeImageSize.IsEmpty)
            {
                return;
            }

            UpdateManualRoiFromCanvasRect(e.RoiRect);
        }

        private void MainCanvasViewModel_RoiMouseUp(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            WpfObjectReviewItemRef selectedManualRoi = null;
            bool updatedSingleObjectRow = false;
            if (e?.RoiRect != null)
            {
                UpdateManualRoiFromCanvasRect(e.RoiRect);
                selectedManualRoi = CreateManualRoiSelection(e.RoiRect);
                updatedSingleObjectRow = selectedManualRoi != null
                    && TryRefreshManualRoiObjectReviewRow(selectedManualRoi.Index, select: true);
            }

            activeRoiEditHistoryOverlayId = string.Empty;
            if (!updatedSingleObjectRow)
            {
                RefreshObjectListWithSelection(selectedManualRoi);
            }
        }


        private void MainCanvasViewModel_RemoveRoiRequested(object sender, CanvasRect<float> rect)
        {
            int index = FindManualRoiIndexByOverlayId(rect?.UniqueId);
            if (index < 0)
            {
                return;
            }

            PushAnnotationHistorySnapshot(CaptureManualRoiHistory("박스 삭제"));
            manualRois.RemoveAt(index);
            RemoveAtIfPresent(manualRoiClassNames, index);
            RemoveAtIfPresent(manualRoiShapeKinds, index);
            RemoveAtIfPresent(manualRoiOverlayIds, index);
            // Canvas ViewModel owns the OpenGL overlay removal after this event; the shell only updates model/review state here.
            RefreshObjectReviewAfterDelete(WpfObjectReviewSource.ManualRoi, index);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }



    }
}
