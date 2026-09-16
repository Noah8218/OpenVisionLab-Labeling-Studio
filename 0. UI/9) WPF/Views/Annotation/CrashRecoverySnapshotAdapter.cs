using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Captures the current editor state into the existing crash-recovery request.
    /// The Shell owns live WPF state; this adapter owns the snapshot mapping only.
    /// </summary>
    internal sealed class CrashRecoverySnapshotAdapter
    {
        private readonly CrashRecoverySnapshotAdapterContext context;

        internal CrashRecoverySnapshotAdapter(CrashRecoverySnapshotAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.SessionService);
            ArgumentNullException.ThrowIfNull(context.ApplicationVersionProvider);
            ArgumentNullException.ThrowIfNull(context.RecipeNameProvider);
            ArgumentNullException.ThrowIfNull(context.DatasetRootPathProvider);
            ArgumentNullException.ThrowIfNull(context.ActiveImagePathProvider);
            ArgumentNullException.ThrowIfNull(context.ActiveImageSizeProvider);
            ArgumentNullException.ThrowIfNull(context.DirtyReasonProvider);
            ArgumentNullException.ThrowIfNull(context.ClassOrderSha256Provider);
            ArgumentNullException.ThrowIfNull(context.ManualRois);
            ArgumentNullException.ThrowIfNull(context.ManualRoiClassNames);
            ArgumentNullException.ThrowIfNull(context.ManualRoiShapeKinds);
            ArgumentNullException.ThrowIfNull(context.ManualSegments);
            ArgumentNullException.ThrowIfNull(context.ConfirmedDetectionCandidatesProvider);
            ArgumentNullException.ThrowIfNull(context.ObjectMetadataStateService);
            ArgumentNullException.ThrowIfNull(context.BuildAnnotationSegments);
        }

        internal WpfCrashRecoveryDraft CaptureDraft()
        {
            List<WpfCrashRecoveryRoiSnapshot> roiSnapshots = CaptureCrashRecoveryRoiSnapshots();
            List<WpfCrashRecoveryCandidateSnapshot> candidateSnapshots = CaptureCrashRecoveryCandidateSnapshots();
            List<WpfCrashRecoverySegmentSnapshot> segmentSnapshots = CaptureCrashRecoverySegmentSnapshots();

            return context.SessionService.Capture(new WpfCrashRecoveryCaptureRequest(
                context.ApplicationVersionProvider(),
                context.RecipeNameProvider(),
                context.DatasetRootPathProvider(),
                context.ActiveImagePathProvider(),
                context.ActiveImageSizeProvider(),
                context.DirtyReasonProvider(),
                roiSnapshots,
                candidateSnapshots,
                segmentSnapshots,
                context.ClassOrderSha256Provider()));
        }

        private List<WpfCrashRecoveryRoiSnapshot> CaptureCrashRecoveryRoiSnapshots()
        {
            var snapshots = new List<WpfCrashRecoveryRoiSnapshot>();
            for (int index = 0; index < context.ManualRois.Count; index++)
            {
                Rectangle bounds = context.ManualRois[index];
                if (bounds.IsEmpty)
                {
                    continue;
                }

                snapshots.Add(new WpfCrashRecoveryRoiSnapshot(
                    bounds,
                    ObjectReviewPresentationService.GetManualRoiClassName(
                        context.ManualRoiClassNames,
                        index),
                    index < context.ManualRoiShapeKinds.Count
                        ? context.ManualRoiShapeKinds[index]
                        : CanvasRoiShapeKind.Rectangle,
                    context.ObjectMetadataStateService.GetManualRoiMetadata(index)));
            }

            return snapshots;
        }

        private List<WpfCrashRecoveryCandidateSnapshot> CaptureCrashRecoveryCandidateSnapshots()
        {
            var snapshots = new List<WpfCrashRecoveryCandidateSnapshot>();
            Size imageSize = context.ActiveImageSizeProvider();
            foreach (YoloWorkerSmokeCandidate candidate in context.ConfirmedDetectionCandidatesProvider()
                ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                Rectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, imageSize);
                snapshots.Add(new WpfCrashRecoveryCandidateSnapshot(
                    candidate?.ClassName,
                    bounds,
                    candidate?.PolygonPoints?.Count >= 3));
            }

            return snapshots;
        }

        private List<WpfCrashRecoverySegmentSnapshot> CaptureCrashRecoverySegmentSnapshots()
        {
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass =
                context.BuildAnnotationSegments();
            var snapshots = new List<WpfCrashRecoverySegmentSnapshot>();
            foreach (LabelingSegmentationObject segment in segmentsByClass
                .Values
                .Where(items => items != null)
                .SelectMany(items => items)
                .Where(item => item != null))
            {
                WpfPersistentObjectMetadata metadata = context.ManualSegments.Contains(segment)
                    ? context.ObjectMetadataStateService.GetManualSegmentMetadata(segment)
                    : WpfPersistentObjectMetadata.Default;
                snapshots.Add(new WpfCrashRecoverySegmentSnapshot(segment, metadata));
            }

            return snapshots;
        }
    }

    internal sealed class CrashRecoverySnapshotAdapterContext
    {
        internal CrashRecoverySessionService SessionService { get; init; }
        internal Func<string> ApplicationVersionProvider { get; init; }
        internal Func<string> RecipeNameProvider { get; init; }
        internal Func<string> DatasetRootPathProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<string> DirtyReasonProvider { get; init; }
        internal Func<string> ClassOrderSha256Provider { get; init; }
        internal IReadOnlyList<Rectangle> ManualRois { get; init; }
        internal IReadOnlyList<string> ManualRoiClassNames { get; init; }
        internal IReadOnlyList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal IReadOnlyList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> ConfirmedDetectionCandidatesProvider { get; init; }
        internal ObjectMetadataStateService ObjectMetadataStateService { get; init; }
        internal Func<Dictionary<string, List<LabelingSegmentationObject>>> BuildAnnotationSegments { get; init; }
    }
}
