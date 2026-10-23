using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloDatasetValidationResult
    {
        public YoloDatasetValidationResult(IEnumerable<string> errors)
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
        public string Summary => string.Join(Environment.NewLine, Errors);
    }

    public sealed class YoloDatasetStatistics
    {
        private readonly Dictionary<string, int> objectCountByClass =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> segmentationObjectCountByClass =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyDictionary<string, int>> segmentationObjectCountBySplit =
            new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        public int TrainImageCount { get; internal set; }
        public int ValidImageCount { get; internal set; }
        public int TestImageCount { get; internal set; }
        public int TrainLabelCount { get; internal set; }
        public int ValidLabelCount { get; internal set; }
        public int TestLabelCount { get; internal set; }
        public int TrainEmptyLabelFileCount { get; internal set; }
        public int ValidEmptyLabelFileCount { get; internal set; }
        public int TestEmptyLabelFileCount { get; internal set; }
        public int TrainSegmentationBackgroundImageCount { get; internal set; }
        public int ValidSegmentationBackgroundImageCount { get; internal set; }
        public int TestSegmentationBackgroundImageCount { get; internal set; }
        public int TrainSegmentFileCount { get; internal set; }
        public int ValidSegmentFileCount { get; internal set; }
        public int TestSegmentFileCount { get; internal set; }
        public int TrainMaskFileCount { get; internal set; }
        public int ValidMaskFileCount { get; internal set; }
        public int TestMaskFileCount { get; internal set; }
        public int TrainValidImageNameOverlapCount { get; internal set; }
        public int TrainValidImageContentOverlapCount { get; internal set; }
        public string TrainValidImageOverlapExample { get; internal set; } = "";
        public int SplitImageContentOverlapCount { get; internal set; }
        public string SplitImageOverlapExample { get; internal set; } = "";
        public int AnomalyNormalImageCount { get; internal set; }
        public int AnomalyAbnormalImageCount { get; internal set; }
        public int AnomalyUnreviewedImageCount { get; internal set; }
        public int TotalImageCount => TrainImageCount + ValidImageCount + TestImageCount;
        public int TotalLabelFileCount => TrainLabelCount + ValidLabelCount + TestLabelCount;
        public int TotalEmptyLabelFileCount =>
            TrainEmptyLabelFileCount + ValidEmptyLabelFileCount + TestEmptyLabelFileCount;
        public int TotalSegmentFileCount => TrainSegmentFileCount + ValidSegmentFileCount + TestSegmentFileCount;
        public int TotalMaskFileCount => TrainMaskFileCount + ValidMaskFileCount + TestMaskFileCount;
        public int TotalSegmentationArtifactFileCount => TotalSegmentFileCount + TotalMaskFileCount;
        public int TotalObjectCount => objectCountByClass.Values.Sum();
        public int TotalSegmentationObjectCount => segmentationObjectCountByClass.Values.Sum();
        public int TotalAnnotationObjectCount => TotalObjectCount + TotalSegmentationObjectCount;
        public IReadOnlyDictionary<string, int> ObjectCountByClass => objectCountByClass;
        public IReadOnlyDictionary<string, int> SegmentationObjectCountByClass => segmentationObjectCountByClass;

        public int? GetSegmentationObjectCount(string split, string className)
        {
            return !string.IsNullOrWhiteSpace(split) && !string.IsNullOrWhiteSpace(className)
                && segmentationObjectCountBySplit.TryGetValue(split, out IReadOnlyDictionary<string, int> counts)
                && counts != null && counts.TryGetValue(className, out int count)
                ? count
                : null;
        }

        internal void SetSegmentationObjectCounts(string split, IReadOnlyDictionary<string, int> counts)
        {
            segmentationObjectCountBySplit[split] = counts;
        }

        internal void AddObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            objectCountByClass.TryGetValue(className, out int count);
            objectCountByClass[className] = count + 1;
        }

        internal void AddSegmentationObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            segmentationObjectCountByClass.TryGetValue(className, out int count);
            segmentationObjectCountByClass[className] = count + 1;
        }
    }
}
