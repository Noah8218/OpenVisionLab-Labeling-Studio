using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public sealed class WpfImageQueueItem : INotifyPropertyChanged
    {
        internal static readonly Brush ErrorBrush = CreateFrozenBrush("#FF5A5F");
        internal static readonly Brush InfoBrush = CreateFrozenBrush("#4EA1FF");
        internal static readonly Brush MutedBrush = CreateFrozenBrush("#7A8491");
        internal static readonly Brush SuccessBrush = CreateFrozenBrush("#57C785");
        internal static readonly Brush WarningBrush = CreateFrozenBrush("#FFC857");
        internal static readonly Brush ErrorBadgeBrush = CreateFrozenBrush("#3D1F25");
        internal static readonly Brush InfoBadgeBrush = CreateFrozenBrush("#17314A");
        internal static readonly Brush MutedBadgeBrush = CreateFrozenBrush("#242B35");
        internal static readonly Brush SuccessBadgeBrush = CreateFrozenBrush("#173B29");
        internal static readonly Brush WarningBadgeBrush = CreateFrozenBrush("#403316");
        internal static readonly Brush TransparentBrush = CreateFrozenBrush("#00000000");

        private string labelStatus = "확인중";
        private string detectStatus = "대기";
        private string dimensions = string.Empty;
        private string detail = string.Empty;
        private string queueStatusSummary = "상태 확인 중";
        private string queueBadgeText = string.Empty;
        private PackIconMaterialKind queueIconKind = PackIconMaterialKind.ImageOutline;
        private Brush queueIconBrush = MutedBrush;
        private Brush queueBadgeBackgroundBrush = TransparentBrush;
        private Brush queueRowAccentBrush = TransparentBrush;
        private bool isLabeled;
        private bool isSaveRequired;
        private YoloImageReviewState reviewState;
        private YoloImageQualityReviewState qualityReviewState;
        private ImageSource thumbnailSource;
        private bool thumbnailLoadAttempted;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ImagePath { get; private set; } = string.Empty;

        public string FileName { get; private set; } = string.Empty;

        public string FolderName { get; private set; } = string.Empty;

        public string FileSize { get; private set; } = string.Empty;

        public string Modified { get; private set; } = string.Empty;

        public ImageSource ThumbnailSource
        {
            get
            {
                if (!thumbnailLoadAttempted)
                {
                    thumbnailLoadAttempted = true;
                    thumbnailSource = CreateThumbnailSource(ImagePath);
                }

                return thumbnailSource;
            }
        }

        public string LabelStatus
        {
            get => labelStatus;
            set => SetField(ref labelStatus, value ?? string.Empty);
        }

        public string DetectStatus
        {
            get => detectStatus;
            set => SetField(ref detectStatus, value ?? string.Empty);
        }

        public string Dimensions
        {
            get => dimensions;
            set => SetField(ref dimensions, value ?? string.Empty);
        }

        public string Detail
        {
            get => detail;
            set => SetField(ref detail, value ?? string.Empty);
        }

        public string QueueRowToolTip => BuildQueueRowText(Environment.NewLine);

        public string QueueRowAccessibleName => BuildQueueRowText(" / ");

        public string QueueStatusSummary
        {
            get => queueStatusSummary;
            set => SetField(ref queueStatusSummary, value ?? string.Empty);
        }

        public string QueueBadgeText
        {
            get => queueBadgeText;
            set => SetField(ref queueBadgeText, value ?? string.Empty);
        }

        public PackIconMaterialKind QueueIconKind
        {
            get => queueIconKind;
            set => SetField(ref queueIconKind, value);
        }

        public Brush QueueIconBrush
        {
            get => queueIconBrush;
            set => SetField(ref queueIconBrush, value ?? MutedBrush);
        }

        public Brush QueueBadgeBackgroundBrush
        {
            get => queueBadgeBackgroundBrush;
            set => SetField(ref queueBadgeBackgroundBrush, value ?? TransparentBrush);
        }

        public Brush QueueRowAccentBrush
        {
            get => queueRowAccentBrush;
            set => SetField(ref queueRowAccentBrush, value ?? TransparentBrush);
        }

        public bool IsLabeled
        {
            get => isLabeled;
            set => SetField(ref isLabeled, value);
        }

        public bool IsSaveRequired
        {
            get => isSaveRequired;
            set => SetField(ref isSaveRequired, value);
        }

        public YoloImageReviewState ReviewState
        {
            get => reviewState;
            set => SetField(ref reviewState, value);
        }

        public YoloImageQualityReviewState QualityReviewState
        {
            get => qualityReviewState;
            set => SetField(ref qualityReviewState, value);
        }

        public static WpfImageQueueItem CreateShell(string imagePath)
        {
            FileInfo fileInfo = new FileInfo(imagePath);
            return new WpfImageQueueItem
            {
                ImagePath = imagePath ?? string.Empty,
                FileName = Path.GetFileName(imagePath),
                FolderName = fileInfo.Directory?.Name ?? string.Empty,
                FileSize = FormatFileSize(fileInfo.Exists ? fileInfo.Length : 0),
                Modified = fileInfo.Exists ? fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty
            };
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (AffectsQueueRowText(propertyName))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueRowToolTip)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueRowAccessibleName)));
            }

            return true;
        }

        private string BuildQueueRowText(string separator)
        {
            string normalizedSeparator = string.IsNullOrEmpty(separator) ? " / " : separator;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FileName))
            {
                parts.Add($"파일: {FileName.Trim()}");
            }

            parts.Add($"저장: {NormalizeStatusText(LabelStatus, "없음")}");
            parts.Add($"검사: {NormalizeStatusText(DetectStatus, "대기")}");
            if (!string.IsNullOrWhiteSpace(Dimensions))
            {
                parts.Add($"크기: {Dimensions.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(QueueStatusSummary))
            {
                parts.Add($"상태: {NormalizeInlineText(QueueStatusSummary)}");
            }

            if (!string.IsNullOrWhiteSpace(Detail))
            {
                parts.Add($"상세: {NormalizeInlineText(Detail)}");
            }

            return string.Join(normalizedSeparator, parts);
        }

        private static string NormalizeStatusText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : NormalizeInlineText(value);
        }

        private static string NormalizeInlineText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string[] lines = value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" / ", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
        }

        private static bool AffectsQueueRowText(string propertyName)
        {
            return string.Equals(propertyName, nameof(LabelStatus), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(DetectStatus), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(Dimensions), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(Detail), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(QueueStatusSummary), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(QualityReviewState), StringComparison.Ordinal);
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / 1024D / 1024D:0.#} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024D:0.#} KB";
            }

            return $"{Math.Max(0, bytes)} B";
        }

        private static ImageSource CreateThumbnailSource(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.DecodePixelWidth = 42;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is NotSupportedException
                || ex is InvalidOperationException
                || ex is ArgumentException)
            {
                return null;
            }
        }

        private static Brush CreateFrozenBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }

    public sealed class WpfImageQueueFilterOption
    {
        public WpfImageQueueFilter Filter { get; set; }

        public string Text { get; set; } = string.Empty;

        public static IReadOnlyList<WpfImageQueueFilterOption> CreateDefaults()
        {
            return Enum.GetValues(typeof(WpfImageQueueFilter))
                .Cast<WpfImageQueueFilter>()
                .Select(filter => new WpfImageQueueFilterOption
                {
                    Filter = filter,
                    Text = GetDisplayName(filter)
                })
                .ToList();
        }

        public static string GetDisplayName(WpfImageQueueFilter filter)
        {
            return filter switch
            {
                WpfImageQueueFilter.Unlabeled => "작업 필요",
                WpfImageQueueFilter.NeedsFix => "수정 필요",
                WpfImageQueueFilter.Requested => "검사중",
                WpfImageQueueFilter.Candidate => "AI 후보",
                WpfImageQueueFilter.Confirmed => "저장됨",
                WpfImageQueueFilter.Skipped => "숨김",
                WpfImageQueueFilter.NoCandidate => "객체없음",
                WpfImageQueueFilter.Failed => "실패",
                _ => "전체"
            };
        }
    }

    public enum WpfImageQueueFilter
    {
        All,
        Unlabeled,
        NeedsFix,
        Requested,
        Candidate,
        Confirmed,
        Skipped,
        NoCandidate,
        Failed
    }

    public sealed class WpfImageQueueDetail
    {
        public DrawingSize ImageSize { get; set; }

        public YoloImageReviewStatus ReviewStatus { get; set; }
    }
}
