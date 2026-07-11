using MvcVisionSystem.DrawObject;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Persistence converts the current viewer labels into YOLO save models; callers should enter through SaveCurrentAnnotations.
        private bool SaveCurrentAnnotations(out int savedCount)
        {
            savedCount = 0;
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return false;
            }

            SaveTrainingEditorFields();
            Dictionary<string, List<CRectangleObject>> roisByClass = BuildAnnotationRois();
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildAnnotationSegments();
            savedCount = CountAnnotationRois(roisByClass) + CountAnnotationSegments(segmentsByClass);
            if (savedCount == 0)
            {
                return !string.IsNullOrWhiteSpace(annotationDirtyReason)
                    && SaveCurrentEmptyAnnotations();
            }

            bool saved = LabelingAnnotationPersistence.SaveCurrent(activeImageBitmap, roisByClass, segmentsByClass, global.Data);
            if (saved)
            {
                MarkAnnotationsSaved($"라벨 저장 완료: 객체 {savedCount}개");
                global.System?.UpdateData();
            }

            return saved;
        }

        private bool SaveCurrentEmptyAnnotations()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return false;
            }

            SaveTrainingEditorFields();
            // Object-detection datasets still need an empty label file for reviewed normal images.
            bool saved = LabelingAnnotationPersistence.SaveCurrent(
                activeImageBitmap,
                new Dictionary<string, List<CRectangleObject>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase),
                global.Data);
            if (saved)
            {
                MarkAnnotationsSaved("\uBE48 \uB77C\uBCA8 \uD30C\uC77C \uC800\uC7A5 \uC644\uB8CC");
                global.System?.UpdateData();
            }

            return saved;
        }

        private static int CountAnnotationRois(IReadOnlyDictionary<string, List<CRectangleObject>> roisByClass)
        {
            return roisByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(roi => roi != null && !roi.Roi.IsEmpty) ?? 0;
        }

        private static int CountAnnotationSegments(IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass)
        {
            return segmentsByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(segment => segment != null && ((segment.Points != null && segment.Points.Count >= 3) || (segment.IsRasterMask && !segment.Bounds.IsEmpty))) ?? 0;
        }

        private Dictionary<string, List<CRectangleObject>> BuildAnnotationRois()
        {
            var roisByClass = new Dictionary<string, List<CRectangleObject>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manualRois.Count; i++)
            {
                AddAnnotationRoi(roisByClass, GetManualRoiClassName(i), manualRois[i]);
            }

            foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
            {
                AddAnnotationRoi(roisByClass, candidate.ClassName, GetClippedCandidateBounds(candidate));
            }

            return roisByClass;
        }

        private Dictionary<string, List<LabelingSegmentationObject>> BuildAnnotationSegments()
        {
            var segmentsByClass = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
            if (!IsSegmentationDatasetPurposeActive())
            {
                return segmentsByClass;
            }

            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment == null)
                {
                    continue;
                }

                bool hasRasterMask = segment.IsRasterMask && !segment.Bounds.IsEmpty;
                bool hasPolygon = segment.Points != null && segment.Points.Count >= 3;
                if (!hasRasterMask && !hasPolygon)
                {
                    continue;
                }

                CClassItem classItem = EnsureClassItem(FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect"));
                segment.ClassItem = classItem;
                segment.ClassName = classItem?.Text ?? "Defect";
                if (!segmentsByClass.TryGetValue(segment.ClassName, out List<LabelingSegmentationObject> segments))
                {
                    segments = new List<LabelingSegmentationObject>();
                    segmentsByClass[segment.ClassName] = segments;
                }

                segments.Add(segment);
            }

            foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
            {
                AddConfirmedCandidateSegment(segmentsByClass, candidate);
            }

            return segmentsByClass;
        }

        private void AddConfirmedCandidateSegment(
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            YoloWorkerSmokeCandidate candidate)
        {
            if (segmentsByClass == null || candidate?.PolygonPoints == null || candidate.PolygonPoints.Count < 3 || activeImageSize.IsEmpty)
            {
                return;
            }

            CClassItem classItem = EnsureClassItem(FirstNonEmpty(candidate.ClassName, "Defect"));
            List<DrawingPoint> points = SegmentationGeometry.NormalizePolygon(
                candidate.PolygonPoints.Select(point => new DrawingPoint(
                    Math.Clamp((int)Math.Round(point.X), 0, activeImageSize.Width - 1),
                    Math.Clamp((int)Math.Round(point.Y), 0, activeImageSize.Height - 1))),
                activeImageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);
            if (points.Count < 3)
            {
                return;
            }

            var segment = new LabelingSegmentationObject(points, classItem)
            {
                ClassName = classItem?.Text ?? "Defect"
            };
            if (!segmentsByClass.TryGetValue(segment.ClassName, out List<LabelingSegmentationObject> segments))
            {
                segments = new List<LabelingSegmentationObject>();
                segmentsByClass[segment.ClassName] = segments;
            }

            segments.Add(segment);
        }

        private void AddAnnotationRoi(
            Dictionary<string, List<CRectangleObject>> roisByClass,
            string className,
            DrawingRectangle bounds)
        {
            if (roisByClass == null || bounds.IsEmpty)
            {
                return;
            }

            CClassItem classItem = EnsureClassItem(className);
            var roiObject = new CRectangleObject
            {
                Roi = bounds,
                cClassItem = classItem
            };

            string normalizedName = classItem?.Text ?? "Defect";
            if (!roisByClass.TryGetValue(normalizedName, out List<CRectangleObject> rois))
            {
                rois = new List<CRectangleObject>();
                roisByClass[normalizedName] = rois;
            }

            rois.Add(roiObject);
        }

        private void MarkAnnotationsDirty(string reason)
        {
            annotationDirtyReason = string.IsNullOrWhiteSpace(reason) ? "Edit" : reason;
            ApplyAnnotationDirtyPresentation();
        }

        private void MarkMaskStrokeAnnotationsDirty(string reason)
        {
            annotationDirtyReason = string.IsNullOrWhiteSpace(reason) ? "Mask edit" : reason;
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: true,
                text: "\uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694",
                toolTip: $"\uC544\uC9C1 \uD30C\uC77C\uC5D0 \uC800\uC7A5\uB418\uC9C0 \uC54A\uC740 \uD3B8\uC9D1: {annotationDirtyReason}");
            if (!string.Equals(annotationDirtyReason, "\0", StringComparison.Ordinal))
            {
                return;
            }
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: true,
                text: "?쇰꺼 ????꾩슂",
                toolTip: $"?꾩쭅 ?뚯씪????λ릺吏 ?딆? ?몄쭛: {annotationDirtyReason}");
        }

        private void RefreshDeferredMaskStrokeDirtyPresentation()
        {
            if (!string.IsNullOrWhiteSpace(annotationDirtyReason))
            {
                ApplyAnnotationDirtyPresentation();
            }
        }

        private void ApplyAnnotationDirtyPresentation()
        {
            InvalidateActiveImageQualityReviewAfterEdit();
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: true,
                text: "라벨 저장 필요",
                toolTip: $"아직 파일에 저장되지 않은 편집: {annotationDirtyReason}");
            CanvasPanelViewModel?.SetAnnotationSaveState(
                true,
                "\uB77C\uBCA8 \uC800\uC7A5",
                "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uBC15\uC2A4\uC640 \uC120\uD0DD\uD55C \uD074\uB798\uC2A4\uB97C \uC800\uC7A5\uD569\uB2C8\uB2E4.");
            ObjectReviewViewModel?.SetLabelSaveState(
                "Dirty",
                "\uC800\uC7A5 \uD544\uC694",
                $"\uD30C\uC77C \uBBF8\uBC18\uC601: {annotationDirtyReason}");
            ApplyActiveImageQueueSaveRequiredStatus(annotationDirtyReason);
            RefreshActiveImageQualityReviewPresentation();
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }

        private void MarkAnnotationsSaved(string reason)
        {
            annotationDirtyReason = string.Empty;
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: false,
                text: "라벨 저장됨",
                toolTip: string.IsNullOrWhiteSpace(reason)
                    ? "현재 라벨이 파일에 저장되었습니다."
                    : reason);
            CanvasPanelViewModel?.SetAnnotationSaveState(
                false,
                "\uC800\uC7A5 \uC644\uB8CC",
                "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8\uC774 \uC800\uC7A5\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.");
            ObjectReviewViewModel?.SetLabelSaveState(
                "Saved",
                "\uC800\uC7A5\uB428",
                string.IsNullOrWhiteSpace(reason)
                    ? "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uB77C\uBCA8\uC774 \uD30C\uC77C\uC5D0 \uBC18\uC601\uB418\uC5C8\uC2B5\uB2C8\uB2E4."
                    : reason);
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }

        private void SetAnnotationSaveStatusWaiting()
        {
            annotationDirtyReason = string.Empty;
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: false,
                text: "라벨 대기",
                toolTip: "이미지를 열면 라벨 저장 상태를 표시합니다.");
            CanvasPanelViewModel?.SetAnnotationSaveState(
                false,
                "\uC800\uC7A5 \uB300\uAE30",
                "\uC774\uBBF8\uC9C0\uB97C \uBD88\uB7EC\uC624\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
            ObjectReviewViewModel?.SetLabelSaveState(
                "Waiting",
                "\uB77C\uBCA8 \uB300\uAE30",
                "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
            ObjectReviewViewModel?.SetQualityReviewState(
                YoloImageQualityReviewState.Unreviewed,
                hasActiveImage: false,
                canMarkReviewed: false);
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }
    }
}
