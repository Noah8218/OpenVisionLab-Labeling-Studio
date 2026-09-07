using OpenVisionLab.Mvvm;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetVisualQaItem : WpfObservableViewModel
    {
        private readonly Func<ImageSource> previewFactory;
        private bool previewLoadAttempted;
        private ImageSource previewSource;

        internal WpfDatasetVisualQaItem(
            string imagePath,
            string splitText,
            string statusText,
            string detailText,
            int objectCount,
            bool isProblem,
            IEnumerable<int> classIndexes,
            Func<ImageSource> previewFactory)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = Path.GetFileName(ImagePath);
            SplitText = splitText ?? string.Empty;
            DisplaySplitText = LocalizeSplit(splitText);
            StatusText = LocalizeStatus(statusText);
            DetailText = LocalizeDetail(detailText);
            ObjectCountText = objectCount > 0
                ? Format("WpfDatasetHealth.VisualQa.ObjectCount", objectCount)
                : T("WpfDatasetHealth.VisualQa.NoObjects");
            IsProblem = isProblem;
            ClassIndexes = (classIndexes ?? Enumerable.Empty<int>())
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            this.previewFactory = previewFactory;
        }

        public string ImagePath { get; }
        public string ImageName { get; }
        public string SplitText { get; }
        public string DisplaySplitText { get; }
        public string StatusText { get; }
        public string DetailText { get; }
        public string ObjectCountText { get; }
        public bool IsProblem { get; }
        public IReadOnlyList<int> ClassIndexes { get; }

        public bool ContainsClassIndex(int classIndex)
            => classIndex >= 0 && ClassIndexes.Contains(classIndex);

        public ImageSource PreviewSource
        {
            get
            {
                if (!previewLoadAttempted)
                {
                    previewLoadAttempted = true;
                    previewSource = previewFactory?.Invoke();
                }

                return previewSource;
            }
        }

        public bool HasPreview => PreviewSource != null;

        private static string LocalizeSplit(string value)
        {
            string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
            return normalized switch
            {
                "train" => T("WpfDatasetHealth.Split.Train"),
                "valid" => T("WpfDatasetHealth.Split.Valid"),
                "test" => T("WpfDatasetHealth.Split.Test"),
                "원본" => T("WpfDatasetHealth.Metric.OriginalImages"),
                _ => value ?? string.Empty
            };
        }

        private static string LocalizeStatus(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            return normalized switch
            {
                "라벨 누락" => T("WpfDatasetHealth.VisualQa.LabelMissing"),
                "라벨 오류" => T("WpfDatasetHealth.VisualQa.LabelError"),
                "빈 라벨 검토" => T("WpfDatasetHealth.VisualQa.EmptyLabelReview"),
                "저장 라벨 표본" => T("WpfDatasetHealth.VisualQa.SavedLabelSample"),
                "미검토" => T("WpfDatasetHealth.Metric.Unreviewed"),
                _ => LocalizationTextRuntimeService.Translate(value ?? string.Empty)
            };
        }

        private static string LocalizeDetail(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized.StartsWith("해석할 수 없는 라벨 ", StringComparison.Ordinal)
                && normalized.EndsWith("줄", StringComparison.Ordinal))
            {
                string count = normalized.Substring("해석할 수 없는 라벨 ".Length);
                count = count.Substring(0, count.Length - 1);
                return Format("WpfDatasetHealth.VisualQa.InvalidLines", count);
            }

            return normalized switch
            {
                "이미지와 짝이 되는 저장 annotation이 없습니다." => T("WpfDatasetHealth.VisualQa.MissingAnnotation"),
                "배경 이미지로 의도한 빈 라벨인지 확인하세요." => T("WpfDatasetHealth.VisualQa.EmptyLabelHint"),
                "저장된 geometry를 읽기 전용으로 확인합니다." => T("WpfDatasetHealth.VisualQa.ReadOnlyGeometry"),
                "저장된 이미지 판정 표본입니다." => T("WpfDatasetHealth.VisualQa.ReviewedSample"),
                "학습 전에 정상 또는 이상으로 판정하세요." => T("WpfDatasetHealth.VisualQa.ReviewBeforeTraining"),
                _ => LocalizationTextRuntimeService.Translate(value ?? string.Empty)
            };
        }

        private static string T(string key)
            => DatasetHealthTextFormatter.Translate(key);

        private static string Format(string key, params object[] arguments)
            => DatasetHealthTextFormatter.Format(key, arguments);
    }

    public sealed class WpfDatasetVisualQaCatalog
    {
        public WpfDatasetVisualQaCatalog(
            IReadOnlyList<WpfDatasetVisualQaItem> items,
            int scannedImageCount,
            int matchedImageCount,
            int problemCount,
            bool isTruncated)
        {
            Items = items ?? Array.Empty<WpfDatasetVisualQaItem>();
            ScannedImageCount = Math.Max(0, scannedImageCount);
            MatchedImageCount = Math.Max(0, matchedImageCount);
            ProblemCount = Math.Max(0, problemCount);
            IsTruncated = isTruncated;
        }

        public IReadOnlyList<WpfDatasetVisualQaItem> Items { get; }
        public int ScannedImageCount { get; }
        public int MatchedImageCount { get; }
        public int ProblemCount { get; }
        public bool IsTruncated { get; }
    }

    public sealed class WpfDatasetVisualQaClassFilterItem
    {
        public WpfDatasetVisualQaClassFilterItem(int? classIndex, string className)
        {
            ClassIndex = classIndex;
            ClassName = className?.Trim() ?? string.Empty;
            Text = classIndex.HasValue
                ? $"{classIndex.Value} · {ClassName}"
                : T("WpfDatasetHealth.VisualQa.AllClasses");
        }

        public int? ClassIndex { get; }
        public string ClassName { get; }
        public string Text { get; }

        public bool HasSameIdentity(WpfDatasetVisualQaClassFilterItem other)
            => other != null
                && ClassIndex == other.ClassIndex
                && string.Equals(ClassName, other.ClassName, StringComparison.Ordinal);

        private static string T(string key)
            => DatasetHealthTextFormatter.Translate(key);
    }
}
