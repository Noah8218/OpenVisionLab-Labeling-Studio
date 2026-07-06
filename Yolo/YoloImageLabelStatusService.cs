using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloImageLabelStatus
    {
        public YoloImageLabelStatus(string labelPath, int objectCount, int invalidLineCount)
        {
            LabelPath = labelPath ?? string.Empty;
            ObjectCount = objectCount;
            InvalidLineCount = invalidLineCount;
        }

        public string LabelPath { get; }

        public int ObjectCount { get; }

        public int InvalidLineCount { get; }

        public bool HasLabelFile => !string.IsNullOrWhiteSpace(LabelPath);

        public bool HasObjects => ObjectCount > 0;

        public string Text
        {
            get
            {
                if (!HasLabelFile)
                {
                    return "No Label";
                }

                if (ObjectCount > 0)
                {
                    return InvalidLineCount > 0 ? $"Label {ObjectCount} / Invalid {InvalidLineCount}" : $"Label {ObjectCount}";
                }

                return InvalidLineCount > 0 ? $"Invalid {InvalidLineCount}" : "Empty Label";
            }
        }
    }

    public static class YoloImageLabelStatusService
    {
        public static YoloImageLabelStatus Build(string imagePath, Size imageSize, CData data)
        {
            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
            {
                return BuildSegmentationStatus(imagePath, imageSize, data);
            }

            string labelPath = YoloAnnotationService.GetCandidateLabelPaths(imagePath, data)
                .FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(labelPath))
            {
                return new YoloImageLabelStatus(string.Empty, 0, 0);
            }

            int objectCount = 0;
            int invalidLineCount = 0;
            int classCount = data?.ClassNamedList?.Count ?? 0;
            foreach (string line in File.ReadLines(labelPath))
            {
                if (!YoloAnnotationService.TryParseYoloLine(line, imageSize, out int classIndex, out _))
                {
                    invalidLineCount++;
                    continue;
                }

                if (classCount > 0 && (classIndex < 0 || classIndex >= classCount))
                {
                    invalidLineCount++;
                    continue;
                }

                objectCount++;
            }

            return new YoloImageLabelStatus(labelPath, objectCount, invalidLineCount);
        }

        private static YoloImageLabelStatus BuildSegmentationStatus(string imagePath, Size imageSize, CData data)
        {
            string segmentPath = YoloSegmentationAnnotationService.GetCandidateSegmentPaths(imagePath, data)
                .FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(segmentPath))
            {
                IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments =
                    YoloSegmentationAnnotationService.LoadSegmentationObjects(
                        segmentPath,
                        data?.ClassNamedList,
                        imageSize);
                int objectCount = segments?
                    .Values
                    .Where(list => list != null)
                    .SelectMany(list => list)
                    .Count(segment => segment != null && segment.Points != null && segment.Points.Count >= 3) ?? 0;
                return new YoloImageLabelStatus(segmentPath, objectCount, 0);
            }

            string labelPath = YoloAnnotationService.GetCandidateLabelPaths(imagePath, data)
                .FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(labelPath))
            {
                return new YoloImageLabelStatus(string.Empty, 0, 0);
            }

            int invalidLineCount = File.ReadLines(labelPath).Count(line => !string.IsNullOrWhiteSpace(line));
            return new YoloImageLabelStatus(labelPath, 0, invalidLineCount);
        }
    }
}
