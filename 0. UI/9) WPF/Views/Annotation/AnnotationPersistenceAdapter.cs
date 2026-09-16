using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Maps the Shell's live annotation state into the existing save workflow.
    /// File transactions remain owned by AnnotationSaveWorkflowService and the
    /// underlying LabelingAnnotationPersistence owner.
    /// </summary>
    internal sealed class AnnotationPersistenceAdapter
    {
        private readonly AnnotationPersistenceAdapterContext context;

        internal AnnotationPersistenceAdapter(AnnotationPersistenceAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.ActiveImageProvider);
            ArgumentNullException.ThrowIfNull(context.SaveTrainingEditorFields);
            ArgumentNullException.ThrowIfNull(context.AnnotationSaveWorkflowService);
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.TrySaveCurrentObjectMetadata);
            ArgumentNullException.ThrowIfNull(context.MarkAnnotationsSaved);
            ArgumentNullException.ThrowIfNull(context.DiscardCrashRecoveryJournal);
            ArgumentNullException.ThrowIfNull(context.UpdateApplicationData);
            ArgumentNullException.ThrowIfNull(context.CompleteMaskAnnotationStroke);
            ArgumentNullException.ThrowIfNull(context.FlushQueuedMaskStrokeCommits);
            ArgumentNullException.ThrowIfNull(context.AnnotationDirtyProvider);
            ArgumentNullException.ThrowIfNull(context.IsSegmentationDatasetPurposeActive);
            ArgumentNullException.ThrowIfNull(context.ActiveImageSizeProvider);
            ArgumentNullException.ThrowIfNull(context.ManualRois);
            ArgumentNullException.ThrowIfNull(context.ManualRoiClassNames);
            ArgumentNullException.ThrowIfNull(context.ManualSegments);
            ArgumentNullException.ThrowIfNull(context.ConfirmedDetectionCandidatesProvider);
            ArgumentNullException.ThrowIfNull(context.ClassCatalogWorkflowService);
        }

        internal bool SaveCurrentAnnotations(out int savedCount)
        {
            savedCount = 0;
            if (context.IsAnnotationSaveBlocked?.Invoke() == true)
            {
                context.AppendLog?.Invoke(
                    "라벨 저장 차단: 손상된 원본 라벨을 명시적으로 복구하기 전에는 덮어쓰지 않습니다.");
                return false;
            }

            context.CompleteMaskAnnotationStroke();
            context.FlushQueuedMaskStrokeCommits();
            context.AppendLog?.Invoke(
                $"라벨 저장 시작: 수동 박스:{context.ManualRois.Count} 세그먼트:{context.ManualSegments.Count}");
            LabelingImageSnapshot activeImage = context.ActiveImageProvider();
            if (activeImage.Image == null || activeImage.ImageSize.IsEmpty)
            {
                context.AppendLog?.Invoke(
                    $"라벨 저장 실패: 활성 이미지가 없습니다. 경로:{activeImage.ImagePath ?? string.Empty} 크기:{activeImage.ImageSize.Width}x{activeImage.ImageSize.Height}");
                return false;
            }

            context.SaveTrainingEditorFields();
            Dictionary<string, List<AnnotationRectangleObject>> roisByClass = BuildAnnotationRois();
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildAnnotationSegments();
            savedCount = CountAnnotationRois(roisByClass) + CountAnnotationSegments(segmentsByClass);
            if (savedCount == 0)
            {
                bool isDirty = context.AnnotationDirtyProvider();
                bool savedEmpty = isDirty && SaveCurrentEmptyAnnotations();
                if (!savedEmpty)
                {
                    context.AppendLog?.Invoke(
                        $"라벨 저장 실패: 저장 객체가 없습니다. dirty:{isDirty} 이미지:{activeImage.ImageName}");
                }

                return savedEmpty;
            }

            AnnotationSaveResult saveResult = context.AnnotationSaveWorkflowService.Save(
                new AnnotationSaveRequest(
                    activeImage,
                    roisByClass,
                    segmentsByClass,
                    context.DataProvider(),
                    savedCount,
                    () =>
                    {
                        bool metadataSaved = context.TrySaveCurrentObjectMetadata(activeImage.ImageName);
                        context.AppendLog?.Invoke(
                            $"라벨 저장 메타데이터 결과: {metadataSaved} 이미지:{activeImage.ImageName}");
                        return metadataSaved;
                    }));

            if (saveResult.IsSaved)
            {
                CompleteSave("라벨 저장 완료: 객체 " + savedCount + "개");
            }
            else
            {
                context.AppendLog?.Invoke(
                    $"라벨 저장 실패: 파일 트랜잭션이 완료되지 않았습니다. 객체:{savedCount} 이미지:{activeImage.ImageName}");
            }

            return saveResult.IsSaved;
        }

        internal bool SaveCurrentEmptyAnnotations()
        {
            if (context.IsAnnotationSaveBlocked?.Invoke() == true)
            {
                context.AppendLog?.Invoke(
                    "빈 라벨 저장 차단: 손상된 원본 라벨을 명시적으로 복구하기 전에는 덮어쓰지 않습니다.");
                return false;
            }

            context.CompleteMaskAnnotationStroke();
            context.FlushQueuedMaskStrokeCommits();
            LabelingImageSnapshot activeImage = context.ActiveImageProvider();
            if (activeImage.Image == null || activeImage.ImageSize.IsEmpty)
            {
                return false;
            }

            context.SaveTrainingEditorFields();
            AnnotationSaveResult saveResult = context.AnnotationSaveWorkflowService.SaveEmpty(
                activeImage,
                context.DataProvider(),
                () =>
                {
                    bool metadataSaved = context.TrySaveCurrentObjectMetadata(activeImage.ImageName);
                    context.AppendLog?.Invoke(
                        $"빈 라벨 저장 메타데이터 결과: {metadataSaved} 이미지:{activeImage.ImageName}");
                    return metadataSaved;
                });

            if (saveResult.IsSaved)
            {
                CompleteSave("빈 라벨 파일 저장 완료");
            }

            return saveResult.IsSaved;
        }

        internal Dictionary<string, List<AnnotationRectangleObject>> BuildAnnotationRois()
        {
            var roisByClass = new Dictionary<string, List<AnnotationRectangleObject>>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < context.ManualRois.Count; index++)
            {
                AddAnnotationRoi(
                    roisByClass,
                    ObjectReviewPresentationService.GetManualRoiClassName(
                        context.ManualRoiClassNames,
                        index),
                    context.ManualRois[index]);
            }

            foreach (YoloWorkerSmokeCandidate candidate in context.ConfirmedDetectionCandidatesProvider()
                ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                AddAnnotationRoi(
                    roisByClass,
                    candidate?.ClassName,
                    CandidateReviewPresentationService.ClipCandidateBounds(
                        candidate,
                        context.ActiveImageSizeProvider()));
            }

            return roisByClass;
        }

        internal Dictionary<string, List<LabelingSegmentationObject>> BuildAnnotationSegments()
        {
            var segmentsByClass = new Dictionary<string, List<LabelingSegmentationObject>>(
                StringComparer.OrdinalIgnoreCase);
            if (!context.IsSegmentationDatasetPurposeActive())
            {
                return segmentsByClass;
            }

            foreach (LabelingSegmentationObject segment in context.ManualSegments)
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

                LabelClass classItem = context.ClassCatalogWorkflowService.EnsureClassItem(
                    context.DataProvider(),
                    FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect"));
                segment.ClassItem = classItem;
                segment.ClassName = classItem?.Text ?? "Defect";
                AddSegment(segmentsByClass, segment.ClassName, segment);
            }

            foreach (YoloWorkerSmokeCandidate candidate in context.ConfirmedDetectionCandidatesProvider()
                ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                AddConfirmedCandidateSegment(segmentsByClass, candidate);
            }

            return segmentsByClass;
        }

        private void CompleteSave(string reason)
        {
            context.MarkAnnotationsSaved(reason);
            context.DiscardCrashRecoveryJournal();
            context.UpdateApplicationData();
        }

        private static int CountAnnotationRois(
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> roisByClass)
        {
            return roisByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(roi => roi != null && !roi.Roi.IsEmpty) ?? 0;
        }

        private static int CountAnnotationSegments(
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass)
        {
            return segmentsByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(segment => segment != null
                    && ((segment.Points != null && segment.Points.Count >= 3)
                        || (segment.IsRasterMask && !segment.Bounds.IsEmpty))) ?? 0;
        }

        private void AddConfirmedCandidateSegment(
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            YoloWorkerSmokeCandidate candidate)
        {
            Size activeImageSize = context.ActiveImageSizeProvider();
            if (segmentsByClass == null
                || candidate?.PolygonPoints == null
                || candidate.PolygonPoints.Count < 3
                || activeImageSize.IsEmpty)
            {
                return;
            }

            LabelClass classItem = context.ClassCatalogWorkflowService.EnsureClassItem(
                context.DataProvider(),
                FirstNonEmpty(candidate.ClassName, "Defect"));
            List<Point> points = SegmentationGeometry.NormalizePolygon(
                candidate.PolygonPoints.Select(point => new Point(
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
            AddSegment(segmentsByClass, segment.ClassName, segment);
        }

        private void AddAnnotationRoi(
            Dictionary<string, List<AnnotationRectangleObject>> roisByClass,
            string className,
            Rectangle bounds)
        {
            if (roisByClass == null || bounds.IsEmpty)
            {
                return;
            }

            LabelClass classItem = context.ClassCatalogWorkflowService.EnsureClassItem(
                context.DataProvider(),
                className);
            var roiObject = new AnnotationRectangleObject
            {
                Roi = bounds,
                cClassItem = classItem
            };

            string normalizedName = classItem?.Text ?? "Defect";
            if (!roisByClass.TryGetValue(normalizedName, out List<AnnotationRectangleObject> rois))
            {
                rois = new List<AnnotationRectangleObject>();
                roisByClass[normalizedName] = rois;
            }

            rois.Add(roiObject);
        }

        private static void AddSegment(
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            string className,
            LabelingSegmentationObject segment)
        {
            if (!segmentsByClass.TryGetValue(className, out List<LabelingSegmentationObject> segments))
            {
                segments = new List<LabelingSegmentationObject>();
                segmentsByClass[className] = segments;
            }

            segments.Add(segment);
        }

        private static string FirstNonEmpty(params string[] values)
            => values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    internal sealed class AnnotationPersistenceAdapterContext
    {
        internal Func<LabelingImageSnapshot> ActiveImageProvider { get; init; }
        internal Action SaveTrainingEditorFields { get; init; }
        internal AnnotationSaveWorkflowService AnnotationSaveWorkflowService { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string, bool> TrySaveCurrentObjectMetadata { get; init; }
        internal Action<string> MarkAnnotationsSaved { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action DiscardCrashRecoveryJournal { get; init; }
        internal Action UpdateApplicationData { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal Func<bool> AnnotationDirtyProvider { get; init; }
        internal Func<bool> IsSegmentationDatasetPurposeActive { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal IReadOnlyList<Rectangle> ManualRois { get; init; }
        internal IReadOnlyList<string> ManualRoiClassNames { get; init; }
        internal IReadOnlyList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> ConfirmedDetectionCandidatesProvider { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal Func<bool> IsAnnotationSaveBlocked { get; init; }
    }
}
