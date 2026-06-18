using Lib.Common;
using Lib.Line;
using System;
using System.Collections.Generic;
using System.Drawing;
using CvPoint = OpenCvSharp.Point;

namespace MvcVisionSystem
{
    public static class MeasurementGeometry
    {
        public static double Distance(Point start, Point end)
        {
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static bool TryCalculateVerticalMeasurement(
            Point lineStart,
            Point lineEnd,
            Point basePoint,
            Size imageSize,
            out Point verticalPoint,
            out double pixelDistance)
        {
            verticalPoint = Point.Empty;
            pixelDistance = 0;

            if (lineStart == lineEnd || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            var ptStart = new CvPoint(lineStart.X, lineStart.Y);
            var ptEnd = new CvPoint(lineEnd.X, lineEnd.Y);
            var ptBase = new CvPoint(basePoint.X, basePoint.Y);
            var ptImageSize = new CvPoint(imageSize.Width, imageSize.Height);

            CLineVertical.GetLineCoef(ptStart, ptEnd, ptBase, ptImageSize, out List<CvPoint> verticalCandidates);
            if (verticalCandidates == null || verticalCandidates.Count == 0)
            {
                return false;
            }

            if (!TryFindVerticalIntersection(ptStart, ptEnd, ptBase, verticalCandidates, out CvPoint intersection))
            {
                return false;
            }

            verticalPoint = new Point(intersection.X, intersection.Y);
            pixelDistance = new CLine(ptBase, intersection).Distance();
            return true;
        }

        private static bool TryFindVerticalIntersection(
            CvPoint lineStart,
            CvPoint lineEnd,
            CvPoint basePoint,
            List<CvPoint> verticalCandidates,
            out CvPoint intersection)
        {
            intersection = new CvPoint();

            int lastIndex = verticalCandidates.Count - 1;
            if (TryIntersect(lineStart, lineEnd, basePoint, verticalCandidates[0], out intersection))
            {
                return true;
            }

            if (lastIndex > 0 && TryIntersect(lineStart, lineEnd, basePoint, verticalCandidates[lastIndex], out intersection))
            {
                return true;
            }

            for (int i = 0; i < lastIndex; i++)
            {
                if (TryIntersect(lineStart, lineEnd, basePoint, verticalCandidates[i], out intersection))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryIntersect(
            CvPoint lineStart,
            CvPoint lineEnd,
            CvPoint basePoint,
            CvPoint verticalCandidate,
            out CvPoint intersection)
        {
            intersection = new CvPoint();
            if (!CFormula.CrossCheck(lineStart, lineEnd, basePoint, verticalCandidate))
            {
                return false;
            }

            var line = new CLine(lineStart, lineEnd);
            var verticalLine = new CLine(basePoint, verticalCandidate);
            CFormula.FindIntersection(verticalLine, line, out intersection);
            return true;
        }
    }
}
