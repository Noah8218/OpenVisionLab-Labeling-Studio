using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Cv2 = OpenCvSharp.Cv2;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using CvPoint = OpenCvSharp.Point;
using CvScalar = OpenCvSharp.Scalar;

namespace MvcVisionSystem
{
    /// <summary>
    /// Converts persisted polygon or raster segmentation geometry into the common
    /// full-image binary mask used by structural editing commands.
    /// </summary>
    public static class SegmentationMaskGeometryService
    {
        public static bool TryRasterize(
            LabelingSegmentationObject source,
            Size imageSize,
            out byte[] maskData,
            out Rectangle maskBounds)
        {
            maskData = imageSize.Width > 0 && imageSize.Height > 0
                ? new byte[imageSize.Width * imageSize.Height]
                : System.Array.Empty<byte>();
            maskBounds = Rectangle.Empty;
            if (source == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            if (source.IsRasterMask)
            {
                CopyRasterMask(source, maskData, imageSize);
            }
            else
            {
                RasterizePolygon(source, maskData, imageSize);
            }

            maskBounds = SegmentationGeometry.GetMaskBounds(maskData, imageSize);
            return !maskBounds.IsEmpty;
        }

        private static void CopyRasterMask(
            LabelingSegmentationObject source,
            byte[] targetMask,
            Size imageSize)
        {
            Rectangle sourceBounds = source.MaskBounds.IsEmpty ? source.Bounds : source.MaskBounds;
            Rectangle clipped = Rectangle.Intersect(
                sourceBounds,
                new Rectangle(
                    0,
                    0,
                    System.Math.Min(imageSize.Width, source.MaskSize.Width),
                    System.Math.Min(imageSize.Height, source.MaskSize.Height)));
            if (clipped.IsEmpty)
            {
                return;
            }

            for (int y = clipped.Top; y < clipped.Bottom; y++)
            {
                int sourceOffset = (y * source.MaskSize.Width) + clipped.Left;
                int targetOffset = (y * imageSize.Width) + clipped.Left;
                for (int x = 0; x < clipped.Width; x++)
                {
                    if (source.MaskData[sourceOffset + x] != 0)
                    {
                        targetMask[targetOffset + x] = 255;
                    }
                }
            }
        }

        private static void RasterizePolygon(
            LabelingSegmentationObject source,
            byte[] targetMask,
            Size imageSize)
        {
            List<Point> outer = SegmentationGeometry.NormalizePolygon(
                source.Points,
                imageSize,
                minimumDistance: 1);
            if (outer.Count < 3)
            {
                return;
            }

            Rectangle workBounds = Rectangle.Intersect(
                SegmentationGeometry.GetBounds(outer),
                new Rectangle(Point.Empty, imageSize));
            if (workBounds.Width <= 0 || workBounds.Height <= 0)
            {
                return;
            }

            using var localMask = new CvMat(
                workBounds.Height,
                workBounds.Width,
                CvMatType.CV_8UC1,
                CvScalar.Black);
            Cv2.FillPoly(localMask, new[] { ToLocalPoints(outer, workBounds) }, CvScalar.White);
            foreach (List<Point> cutout in source.CutoutPolygons ?? new List<List<Point>>())
            {
                List<Point> normalizedCutout = SegmentationGeometry.NormalizePolygon(
                    cutout,
                    imageSize,
                    minimumDistance: 1);
                if (normalizedCutout.Count >= 3)
                {
                    Cv2.FillPoly(
                        localMask,
                        new[] { ToLocalPoints(normalizedCutout, workBounds) },
                        CvScalar.Black);
                }
            }

            for (int localY = 0; localY < workBounds.Height; localY++)
            {
                int targetOffset = ((workBounds.Top + localY) * imageSize.Width) + workBounds.Left;
                for (int localX = 0; localX < workBounds.Width; localX++)
                {
                    if (localMask.At<byte>(localY, localX) != 0)
                    {
                        targetMask[targetOffset + localX] = 255;
                    }
                }
            }
        }

        private static CvPoint[] ToLocalPoints(
            IEnumerable<Point> points,
            Rectangle workBounds)
            => points
                .Select(point => new CvPoint(point.X - workBounds.Left, point.Y - workBounds.Top))
                .ToArray();
    }

    [Obsolete("Use SegmentationMaskGeometryService.", false)]
    public static class WpfSegmentationMaskGeometryService
    {
        public static bool TryRasterize(
            LabelingSegmentationObject source,
            Size imageSize,
            out byte[] maskData,
            out Rectangle maskBounds)
            => SegmentationMaskGeometryService.TryRasterize(source, imageSize, out maskData, out maskBounds);
    }
}
