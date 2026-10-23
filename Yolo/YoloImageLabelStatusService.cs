using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloImageLabelStatusService
    {
        public static YoloImageLabelStatus Build(string imagePath, Size imageSize, LabelingProjectData data)
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

        private static YoloImageLabelStatus BuildSegmentationStatus(string imagePath, Size imageSize, LabelingProjectData data)
        {
            string segmentPath = YoloSegmentationAnnotationService.GetCandidateSegmentPaths(imagePath, data)
                .FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(segmentPath))
            {
                IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segments;
                try
                {
                    segments = YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(
                        imagePath,
                        data?.ClassNamedList,
                        data,
                        imageSize);
                }
                catch (InvalidDataException)
                {
                    return new YoloImageLabelStatus(segmentPath, 0, 1);
                }

                int objectCount = segments?
                    .Values
                    .Where(list => list != null)
                    .SelectMany(list => list)
                    .Count(segment => segment != null) ?? 0;
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
