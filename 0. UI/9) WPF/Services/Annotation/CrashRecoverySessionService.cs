using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns crash-recovery snapshot conversion. The Shell remains responsible for
    /// dialogs, live annotation state, and applying a validated restore plan.
    /// </summary>
    public class CrashRecoverySessionService
    {
        public WpfCrashRecoveryDraft Capture(WpfCrashRecoveryCaptureRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            FileInfo imageInfo = new FileInfo(request.ImagePath);
            var draft = new WpfCrashRecoveryDraft
            {
                CreatedUtc = DateTime.UtcNow,
                ApplicationVersion = request.ApplicationVersion,
                RecipeName = request.RecipeName,
                DatasetRootPath = request.DatasetRootPath,
                ImagePath = request.ImagePath,
                ImageLength = imageInfo.Length,
                ImageLastWriteUtcTicks = imageInfo.LastWriteTimeUtc.Ticks,
                ImageWidth = request.ImageSize.Width,
                ImageHeight = request.ImageSize.Height,
                DirtyReason = request.DirtyReason
            };

            foreach (WpfCrashRecoveryRoiSnapshot roi in request.ManualRois)
            {
                if (roi.Bounds.IsEmpty)
                {
                    continue;
                }

                draft.Boxes.Add(new WpfCrashRecoveryBox
                {
                    ClassName = roi.ClassName,
                    ShapeKind = roi.ShapeKind.ToString(),
                    X = roi.Bounds.X,
                    Y = roi.Bounds.Y,
                    Width = roi.Bounds.Width,
                    Height = roi.Bounds.Height,
                    Metadata = ToRecoveryMetadata(roi.Metadata)
                });
            }

            foreach (WpfCrashRecoveryCandidateSnapshot candidate in request.ConfirmedCandidates)
            {
                if (candidate.Bounds.IsEmpty || candidate.HasPolygon)
                {
                    continue;
                }

                draft.Boxes.Add(new WpfCrashRecoveryBox
                {
                    ClassName = string.IsNullOrWhiteSpace(candidate.ClassName)
                        ? "Defect"
                        : candidate.ClassName,
                    ShapeKind = CanvasRoiShapeKind.Rectangle.ToString(),
                    X = candidate.Bounds.X,
                    Y = candidate.Bounds.Y,
                    Width = candidate.Bounds.Width,
                    Height = candidate.Bounds.Height,
                    Metadata = new WpfCrashRecoveryMetadata()
                });
            }

            foreach (WpfCrashRecoverySegmentSnapshot segment in request.Segments)
            {
                draft.Segments.Add(ToRecoverySegment(segment));
            }

            return draft;
        }

        public WpfCrashRecoveryRestorePlan BuildRestorePlan(WpfCrashRecoveryDraft draft)
        {
            ArgumentNullException.ThrowIfNull(draft);

            return new WpfCrashRecoveryRestorePlan(
                (draft.Boxes ?? new List<WpfCrashRecoveryBox>())
                    .Where(box => box != null)
                    .Select(CloneBox)
                    .ToList(),
                (draft.Segments ?? new List<WpfCrashRecoverySegment>())
                    .Where(segment => segment != null)
                    .Select(CloneSegment)
                    .ToList());
        }

        public static WpfPersistentObjectMetadata ToPersistentMetadata(
            WpfCrashRecoveryMetadata metadata)
            => metadata == null
                ? WpfPersistentObjectMetadata.Default
                : new WpfPersistentObjectMetadata(
                    metadata.IsOccluded,
                    metadata.Tags,
                    metadata.GroupId);

        private static WpfCrashRecoverySegment ToRecoverySegment(
            WpfCrashRecoverySegmentSnapshot segment)
        {
            Rectangle maskBounds = segment.MaskBounds;
            return new WpfCrashRecoverySegment
            {
                ClassName = FirstNonEmpty(segment.ClassName, "Defect"),
                ObjectId = segment.ObjectId,
                ComponentIndex = segment.ComponentIndex,
                ZOrder = segment.ZOrder,
                LastStructuralOperation = segment.LastStructuralOperation,
                Points = segment.Points
                    .Select(point => new WpfCrashRecoveryPoint { X = point.X, Y = point.Y })
                    .ToList(),
                CutoutPolygons = segment.CutoutPolygons
                    .Select(cutout => cutout
                        .Select(point => new WpfCrashRecoveryPoint { X = point.X, Y = point.Y })
                        .ToList())
                    .ToList(),
                MaskData = segment.MaskData.ToArray(),
                MaskWidth = segment.MaskSize.Width,
                MaskHeight = segment.MaskSize.Height,
                MaskBoundsX = maskBounds.X,
                MaskBoundsY = maskBounds.Y,
                MaskBoundsWidth = maskBounds.Width,
                MaskBoundsHeight = maskBounds.Height,
                Metadata = ToRecoveryMetadata(segment.Metadata)
            };
        }

        private static WpfCrashRecoveryBox CloneBox(WpfCrashRecoveryBox source)
            => new WpfCrashRecoveryBox
            {
                ClassName = source.ClassName ?? string.Empty,
                ShapeKind = source.ShapeKind ?? CanvasRoiShapeKind.Rectangle.ToString(),
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                Metadata = CloneMetadata(source.Metadata)
            };

        private static WpfCrashRecoverySegment CloneSegment(WpfCrashRecoverySegment source)
            => new WpfCrashRecoverySegment
            {
                ClassName = source.ClassName ?? string.Empty,
                ObjectId = source.ObjectId ?? string.Empty,
                ComponentIndex = source.ComponentIndex,
                ZOrder = source.ZOrder,
                LastStructuralOperation = source.LastStructuralOperation ?? string.Empty,
                Points = (source.Points ?? new List<WpfCrashRecoveryPoint>())
                    .Where(point => point != null)
                    .Select(point => new WpfCrashRecoveryPoint { X = point.X, Y = point.Y })
                    .ToList(),
                CutoutPolygons = (source.CutoutPolygons
                    ?? new List<List<WpfCrashRecoveryPoint>>())
                    .Where(cutout => cutout != null)
                    .Select(cutout => cutout
                        .Where(point => point != null)
                        .Select(point => new WpfCrashRecoveryPoint { X = point.X, Y = point.Y })
                        .ToList())
                    .ToList(),
                MaskData = source.MaskData?.ToArray() ?? Array.Empty<byte>(),
                MaskWidth = source.MaskWidth,
                MaskHeight = source.MaskHeight,
                MaskBoundsX = source.MaskBoundsX,
                MaskBoundsY = source.MaskBoundsY,
                MaskBoundsWidth = source.MaskBoundsWidth,
                MaskBoundsHeight = source.MaskBoundsHeight,
                Metadata = CloneMetadata(source.Metadata)
            };

        private static WpfCrashRecoveryMetadata ToRecoveryMetadata(
            WpfPersistentObjectMetadata metadata)
        {
            WpfPersistentObjectMetadata normalized = metadata ?? WpfPersistentObjectMetadata.Default;
            return new WpfCrashRecoveryMetadata
            {
                IsOccluded = normalized.IsOccluded,
                Tags = normalized.Tags.ToList(),
                GroupId = normalized.GroupId
            };
        }

        private static WpfCrashRecoveryMetadata CloneMetadata(
            WpfCrashRecoveryMetadata metadata)
            => new WpfCrashRecoveryMetadata
            {
                IsOccluded = metadata?.IsOccluded == true,
                Tags = (metadata?.Tags ?? new List<string>()).ToList(),
                GroupId = metadata?.GroupId ?? string.Empty
            };

        private static string FirstNonEmpty(params string[] values)
            => values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    [Obsolete("Use CrashRecoverySessionService.", false)]
    public sealed class WpfCrashRecoverySessionService : CrashRecoverySessionService
    {
    }

    public sealed class WpfCrashRecoveryCaptureRequest
    {
        public WpfCrashRecoveryCaptureRequest(
            string applicationVersion,
            string recipeName,
            string datasetRootPath,
            string imagePath,
            Size imageSize,
            string dirtyReason,
            IReadOnlyList<WpfCrashRecoveryRoiSnapshot> manualRois,
            IReadOnlyList<WpfCrashRecoveryCandidateSnapshot> confirmedCandidates,
            IReadOnlyList<WpfCrashRecoverySegmentSnapshot> segments)
        {
            ApplicationVersion = applicationVersion ?? string.Empty;
            RecipeName = recipeName ?? string.Empty;
            DatasetRootPath = datasetRootPath ?? string.Empty;
            ImagePath = imagePath ?? string.Empty;
            ImageSize = imageSize;
            DirtyReason = dirtyReason ?? string.Empty;
            ManualRois = (manualRois ?? Array.Empty<WpfCrashRecoveryRoiSnapshot>()).ToList();
            ConfirmedCandidates = (confirmedCandidates
                ?? Array.Empty<WpfCrashRecoveryCandidateSnapshot>()).ToList();
            Segments = (segments ?? Array.Empty<WpfCrashRecoverySegmentSnapshot>()).ToList();
        }

        public string ApplicationVersion { get; }
        public string RecipeName { get; }
        public string DatasetRootPath { get; }
        public string ImagePath { get; }
        public Size ImageSize { get; }
        public string DirtyReason { get; }
        public IReadOnlyList<WpfCrashRecoveryRoiSnapshot> ManualRois { get; }
        public IReadOnlyList<WpfCrashRecoveryCandidateSnapshot> ConfirmedCandidates { get; }
        public IReadOnlyList<WpfCrashRecoverySegmentSnapshot> Segments { get; }
    }

    public sealed class WpfCrashRecoveryRoiSnapshot
    {
        public WpfCrashRecoveryRoiSnapshot(
            Rectangle bounds,
            string className,
            CanvasRoiShapeKind shapeKind,
            WpfPersistentObjectMetadata metadata)
        {
            Bounds = bounds;
            ClassName = className ?? string.Empty;
            ShapeKind = shapeKind;
            Metadata = metadata ?? WpfPersistentObjectMetadata.Default;
        }

        public Rectangle Bounds { get; }
        public string ClassName { get; }
        public CanvasRoiShapeKind ShapeKind { get; }
        public WpfPersistentObjectMetadata Metadata { get; }
    }

    public sealed class WpfCrashRecoveryCandidateSnapshot
    {
        public WpfCrashRecoveryCandidateSnapshot(
            string className,
            Rectangle bounds,
            bool hasPolygon)
        {
            ClassName = className ?? string.Empty;
            Bounds = bounds;
            HasPolygon = hasPolygon;
        }

        public string ClassName { get; }
        public Rectangle Bounds { get; }
        public bool HasPolygon { get; }
    }

    public sealed class WpfCrashRecoverySegmentSnapshot
    {
        public WpfCrashRecoverySegmentSnapshot(
            LabelingSegmentationObject segment,
            WpfPersistentObjectMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(segment);

            ClassName = segment.ClassName ?? segment.ClassItem?.Text ?? string.Empty;
            ObjectId = segment.ObjectId ?? string.Empty;
            ComponentIndex = segment.ComponentIndex;
            ZOrder = segment.ZOrder;
            LastStructuralOperation = segment.LastStructuralOperation ?? string.Empty;
            Points = (segment.Points ?? new List<Point>()).ToArray();
            CutoutPolygons = (segment.CutoutPolygons ?? new List<List<Point>>())
                .Where(cutout => cutout != null)
                .Select(cutout => (IReadOnlyList<Point>)cutout.ToArray())
                .ToArray();
            MaskData = segment.MaskData?.ToArray() ?? Array.Empty<byte>();
            MaskSize = segment.MaskSize;
            MaskBounds = segment.MaskBounds;
            Metadata = metadata ?? WpfPersistentObjectMetadata.Default;
        }

        public string ClassName { get; }
        public string ObjectId { get; }
        public int ComponentIndex { get; }
        public int ZOrder { get; }
        public string LastStructuralOperation { get; }
        public IReadOnlyList<Point> Points { get; }
        public IReadOnlyList<IReadOnlyList<Point>> CutoutPolygons { get; }
        public IReadOnlyList<byte> MaskData { get; }
        public Size MaskSize { get; }
        public Rectangle MaskBounds { get; }
        public WpfPersistentObjectMetadata Metadata { get; }
    }

    public sealed class WpfCrashRecoveryRestorePlan
    {
        public WpfCrashRecoveryRestorePlan(
            IReadOnlyList<WpfCrashRecoveryBox> boxes,
            IReadOnlyList<WpfCrashRecoverySegment> segments)
        {
            Boxes = boxes ?? Array.Empty<WpfCrashRecoveryBox>();
            Segments = segments ?? Array.Empty<WpfCrashRecoverySegment>();
        }

        public IReadOnlyList<WpfCrashRecoveryBox> Boxes { get; }
        public IReadOnlyList<WpfCrashRecoverySegment> Segments { get; }
    }
}
