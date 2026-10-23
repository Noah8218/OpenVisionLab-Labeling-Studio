using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    public enum MaskRegionOperation
    {
        Add,
        Subtract
    }

    public sealed class MaskRegionAlgebraResult
    {
        public MaskRegionAlgebraResult(byte[] maskData, Rectangle changedBounds, bool changed, bool isEmpty)
        {
            MaskData = maskData ?? Array.Empty<byte>();
            ChangedBounds = changedBounds;
            Changed = changed;
            IsEmpty = isEmpty;
        }

        public byte[] MaskData { get; }

        public Rectangle ChangedBounds { get; }

        public bool Changed { get; }

        public bool IsEmpty { get; }
    }

    public sealed class MaskCandidateEditSnapshot
    {
        internal MaskCandidateEditSnapshot(byte[] maskData, Size maskSize, Rectangle maskBounds, int renderVersion)
        {
            MaskData = maskData ?? Array.Empty<byte>();
            MaskSize = maskSize;
            MaskBounds = maskBounds;
            RenderVersion = renderVersion;
            IsEmpty = maskBounds.IsEmpty;
        }

        public byte[] MaskData { get; }

        public Size MaskSize { get; }

        public Rectangle MaskBounds { get; }

        public int RenderVersion { get; }

        public bool IsEmpty { get; }
    }

    public static class MaskRegionAlgebraService
    {
        public static MaskRegionAlgebraResult Apply(
            byte[] targetMask,
            byte[] regionMask,
            Size imageSize,
            MaskRegionOperation operation)
        {
            int pixelCount = checked(imageSize.Width * imageSize.Height);
            if (imageSize.Width <= 0
                || imageSize.Height <= 0
                || targetMask?.Length != pixelCount
                || regionMask?.Length != pixelCount)
            {
                throw new ArgumentException("Target and region masks must match the positive image size.");
            }

            byte[] result = (byte[])targetMask.Clone();
            bool changed = false;
            bool any = false;
            int minX = imageSize.Width;
            int minY = imageSize.Height;
            int maxX = -1;
            int maxY = -1;
            for (int index = 0; index < result.Length; index++)
            {
                bool before = result[index] != 0;
                bool region = regionMask[index] != 0;
                bool after = operation == MaskRegionOperation.Add
                    ? before || region
                    : before && !region;
                result[index] = after ? (byte)255 : (byte)0;
                any |= after;
                if (before == after)
                {
                    continue;
                }

                changed = true;
                int x = index % imageSize.Width;
                int y = index / imageSize.Width;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            Rectangle changedBounds = changed
                ? Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)
                : Rectangle.Empty;
            return new MaskRegionAlgebraResult(result, changedBounds, changed, !any);
        }
    }

    public sealed class MaskCandidateEditSessionToken
    {
        internal MaskCandidateEditSessionToken(long generation, string imagePath, string recipeName, string objectId)
        {
            Generation = generation;
            ImagePath = imagePath ?? string.Empty;
            RecipeName = recipeName ?? string.Empty;
            ObjectId = objectId ?? string.Empty;
        }

        public long Generation { get; }

        public string ImagePath { get; }

        public string RecipeName { get; }

        public string ObjectId { get; }
    }

    /// <summary>
    /// Owns a session-only copy of one target mask. Canonical annotations are
    /// read only until the caller accepts TryBuildApplyResult as one history/save transaction.
    /// </summary>
    public sealed class MaskCandidateEditBufferService
    {
        /// <summary>
        /// Bounds session memory while keeping enough local edits for the normal
        /// candidate-correction workflow. The canonical annotation is never
        /// changed by entries in these stacks.
        /// </summary>
        public const int MaxHistoryEntries = 32;

        private readonly Stack<byte[]> undo = new Stack<byte[]>();
        private readonly Stack<byte[]> redo = new Stack<byte[]>();
        private long generation;
        private byte[] sourceMask = Array.Empty<byte>();
        private byte[] buffer = Array.Empty<byte>();
        private Size imageSize;
        private string imagePath = string.Empty;
        private string recipeName = string.Empty;
        private string objectId = string.Empty;
        private byte[] strokeOrigin = Array.Empty<byte>();
        private bool strokeActive;
        private int renderVersion;

        public bool HasSession { get; private set; }

        public bool CanUndo => undo.Count > 0;

        public bool CanRedo => redo.Count > 0;

        public MaskCandidateEditSessionToken Begin(
            string currentImagePath,
            string currentRecipeName,
            string targetObjectId,
            byte[] sourceMask,
            Size sourceImageSize,
            bool targetIsEditable)
        {
            int pixelCount = checked(sourceImageSize.Width * sourceImageSize.Height);
            if (!targetIsEditable
                || string.IsNullOrWhiteSpace(targetObjectId)
                || sourceImageSize.Width <= 0
                || sourceImageSize.Height <= 0
                || sourceMask?.Length != pixelCount)
            {
                return null;
            }

            generation++;
            HasSession = true;
            imagePath = currentImagePath ?? string.Empty;
            recipeName = currentRecipeName ?? string.Empty;
            objectId = targetObjectId.Trim();
            imageSize = sourceImageSize;
            this.sourceMask = NormalizeMask(sourceMask);
            buffer = (byte[])this.sourceMask.Clone();
            undo.Clear();
            redo.Clear();
            strokeOrigin = Array.Empty<byte>();
            strokeActive = false;
            renderVersion = 1;
            return CaptureToken();
        }

        public MaskRegionAlgebraResult ApplyRegion(byte[] regionMask, MaskRegionOperation operation)
            => ApplyRegionCore(regionMask, operation, recordHistory: true);

        public MaskRegionAlgebraResult ApplyBrushStroke(
            IEnumerable<Point> centers,
            int radius,
            MaskRegionOperation operation)
        {
            if (!HasSession)
            {
                return new MaskRegionAlgebraResult(Array.Empty<byte>(), Rectangle.Empty, changed: false, isEmpty: true);
            }

            byte[] region = BuildBrushRegion(centers, radius, imageSize);
            return strokeActive
                ? ApplyRegionCore(region, operation, recordHistory: false)
                : ApplyRegionCore(region, operation, recordHistory: true);
        }

        public bool BeginStroke()
        {
            if (!HasSession || strokeActive)
            {
                return false;
            }

            strokeOrigin = (byte[])buffer.Clone();
            strokeActive = true;
            return true;
        }

        public bool CompleteStroke()
        {
            if (!strokeActive)
            {
                return false;
            }

            bool changed = !MasksEqual(strokeOrigin, buffer);
            if (changed)
            {
                PushUndo(strokeOrigin);
                redo.Clear();
            }

            strokeOrigin = Array.Empty<byte>();
            strokeActive = false;
            return changed;
        }

        public bool CancelStroke()
        {
            if (!strokeActive)
            {
                return false;
            }

            bool changed = !MasksEqual(strokeOrigin, buffer);
            if (changed)
            {
                buffer = strokeOrigin;
                renderVersion = NextRenderVersion();
            }

            strokeOrigin = Array.Empty<byte>();
            strokeActive = false;
            return changed;
        }

        private MaskRegionAlgebraResult ApplyRegionCore(
            byte[] regionMask,
            MaskRegionOperation operation,
            bool recordHistory)
        {
            if (!HasSession)
            {
                return new MaskRegionAlgebraResult(Array.Empty<byte>(), Rectangle.Empty, changed: false, isEmpty: true);
            }

            MaskRegionAlgebraResult result = MaskRegionAlgebraService.Apply(buffer, regionMask, imageSize, operation);
            if (result.Changed)
            {
                if (recordHistory)
                {
                    PushUndo(buffer);
                    redo.Clear();
                }

                buffer = result.MaskData;
                renderVersion = NextRenderVersion();
            }

            return result;
        }

        public MaskRegionAlgebraResult ApplyPolygon(
            IReadOnlyList<Point> points,
            IReadOnlyList<IReadOnlyList<Point>> cutouts,
            MaskRegionOperation operation)
        {
            if (!HasSession)
            {
                return new MaskRegionAlgebraResult(Array.Empty<byte>(), Rectangle.Empty, changed: false, isEmpty: true);
            }

            var polygon = new LabelingSegmentationObject
            {
                Points = points == null ? new List<Point>() : new List<Point>(points),
                CutoutPolygons = BuildCutouts(cutouts)
            };
            if (!SegmentationMaskGeometryService.TryRasterize(polygon, imageSize, out byte[] region, out _))
            {
                return new MaskRegionAlgebraResult((byte[])buffer.Clone(), Rectangle.Empty, changed: false, isEmpty: IsEmpty(buffer));
            }

            return ApplyRegion(region, operation);
        }

        public bool Undo()
        {
            if (!HasSession || strokeActive || undo.Count == 0)
            {
                return false;
            }

            PushHistoryEntry(redo, buffer);
            buffer = undo.Pop();
            renderVersion = NextRenderVersion();
            return true;
        }

        public bool Redo()
        {
            if (!HasSession || strokeActive || redo.Count == 0)
            {
                return false;
            }

            PushUndo(buffer);
            buffer = redo.Pop();
            renderVersion = NextRenderVersion();
            return true;
        }

        public bool TryGetSnapshot(out MaskCandidateEditSnapshot snapshot)
        {
            snapshot = null;
            if (!HasSession)
            {
                return false;
            }

            byte[] copy = (byte[])buffer.Clone();
            Rectangle bounds = SegmentationGeometry.GetMaskBounds(copy, imageSize);
            snapshot = new MaskCandidateEditSnapshot(copy, imageSize, bounds, renderVersion);
            return true;
        }

        public bool TryBuildApplyResult(
            MaskCandidateEditSessionToken token,
            string currentImagePath,
            string currentRecipeName,
            string currentObjectId,
            out byte[] result)
        {
            result = Array.Empty<byte>();
            if (!Matches(token, currentImagePath, currentRecipeName, currentObjectId))
            {
                return false;
            }

            result = (byte[])buffer.Clone();
            return true;
        }

        public bool TryBuildCanonicalReplacement(
            MaskCandidateEditSessionToken token,
            string currentImagePath,
            string currentRecipeName,
            LabelingSegmentationObject currentSource,
            out LabelingSegmentationObject replacement)
        {
            replacement = null;
            if (currentSource == null
                || !Matches(token, currentImagePath, currentRecipeName, currentSource.ObjectId)
                || !SegmentationMaskGeometryService.TryRasterize(currentSource, imageSize, out byte[] currentMask, out _)
                || !MasksEqual(sourceMask, NormalizeMask(currentMask)))
            {
                return false;
            }

            Rectangle bounds = SegmentationGeometry.GetMaskBounds(buffer, imageSize);
            if (bounds.IsEmpty)
            {
                return true;
            }

            replacement = new LabelingSegmentationObject
            {
                ClassName = currentSource.ClassName,
                ClassItem = currentSource.ClassItem,
                ObjectId = currentSource.ObjectId,
                ComponentIndex = currentSource.ComponentIndex,
                ZOrder = currentSource.ZOrder,
                LastStructuralOperation = "CandidateApply",
                MaskData = (byte[])buffer.Clone(),
                MaskSize = imageSize,
                MaskBounds = bounds,
                RenderVersion = currentSource.RenderVersion + 1,
                RenderDirtyBounds = Rectangle.Union(currentSource.Bounds, bounds),
                Selected = currentSource.Selected
            };
            return true;
        }

        public bool Matches(
            MaskCandidateEditSessionToken token,
            string currentImagePath,
            string currentRecipeName,
            string currentObjectId)
            => token != null
                && HasSession
                && token.Generation == generation
                && string.Equals(imagePath, currentImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(recipeName, currentRecipeName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(objectId, currentObjectId?.Trim() ?? string.Empty, StringComparison.Ordinal);

        public MaskCandidateEditSessionToken CaptureToken()
            => HasSession
                ? new MaskCandidateEditSessionToken(generation, imagePath, recipeName, objectId)
                : null;

        public void Complete() => Reset();

        public void Cancel() => Reset();

        private void Reset()
        {
            generation++;
            HasSession = false;
            imagePath = string.Empty;
            recipeName = string.Empty;
            objectId = string.Empty;
            imageSize = Size.Empty;
            sourceMask = Array.Empty<byte>();
            buffer = Array.Empty<byte>();
            strokeOrigin = Array.Empty<byte>();
            strokeActive = false;
            renderVersion = 0;
            undo.Clear();
            redo.Clear();
        }

        private static byte[] BuildBrushRegion(IEnumerable<Point> centers, int radius, Size size)
        {
            int pixelCount = checked(size.Width * size.Height);
            byte[] region = new byte[pixelCount];
            int safeRadius = Math.Max(1, radius);
            long radiusSquared = (long)safeRadius * safeRadius;
            foreach (Point center in centers ?? Array.Empty<Point>())
            {
                int top = Math.Max(0, center.Y - safeRadius);
                int bottom = Math.Min(size.Height - 1, center.Y + safeRadius);
                int left = Math.Max(0, center.X - safeRadius);
                int right = Math.Min(size.Width - 1, center.X + safeRadius);
                for (int y = top; y <= bottom; y++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        long dx = x - center.X;
                        long dy = y - center.Y;
                        if ((dx * dx) + (dy * dy) <= radiusSquared)
                        {
                            region[(y * size.Width) + x] = 255;
                        }
                    }
                }
            }

            return region;
        }

        private int NextRenderVersion()
            => renderVersion == int.MaxValue ? 1 : renderVersion + 1;

        private void PushUndo(byte[] mask)
            => PushHistoryEntry(undo, mask);

        private static void PushHistoryEntry(Stack<byte[]> history, byte[] mask)
        {
            if (history == null || mask == null)
            {
                return;
            }

            history.Push((byte[])mask.Clone());
            while (history.Count > MaxHistoryEntries)
            {
                byte[][] entries = history.ToArray();
                history.Clear();
                int retainedCount = Math.Min(MaxHistoryEntries, entries.Length);
                for (int index = retainedCount - 1; index >= 0; index--)
                {
                    history.Push(entries[index]);
                }
            }
        }

        private static byte[] NormalizeMask(byte[] source)
        {
            byte[] result = new byte[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                result[index] = source[index] == 0 ? (byte)0 : (byte)255;
            }

            return result;
        }

        private static List<List<Point>> BuildCutouts(IReadOnlyList<IReadOnlyList<Point>> cutouts)
        {
            var result = new List<List<Point>>();
            if (cutouts == null)
            {
                return result;
            }

            foreach (IReadOnlyList<Point> cutout in cutouts)
            {
                result.Add(cutout == null ? new List<Point>() : new List<Point>(cutout));
            }

            return result;
        }

        private static bool IsEmpty(byte[] mask)
        {
            foreach (byte value in mask ?? Array.Empty<byte>())
            {
                if (value != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MasksEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
