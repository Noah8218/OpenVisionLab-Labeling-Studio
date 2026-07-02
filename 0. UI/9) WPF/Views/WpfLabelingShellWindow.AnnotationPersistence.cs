using MvcVisionSystem.DrawObject;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;
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

            return segmentsByClass;
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
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }
    }
}
