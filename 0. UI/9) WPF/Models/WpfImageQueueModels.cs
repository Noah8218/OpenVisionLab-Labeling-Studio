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

        private string labelStatus = "확인중";
        private string detectStatus = "대기";
        private string dimensions = string.Empty;
        private string detail = string.Empty;
        private string queueStatusSummary = "상태 확인 전";
        private string queueBadgeText = string.Empty;
        private PackIconMaterialKind queueIconKind = PackIconMaterialKind.ImageOutline;
        private Brush queueIconBrush = MutedBrush;
        private bool isLabeled;
        private YoloImageReviewState reviewState;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ImagePath { get; private set; } = string.Empty;

        public string FileName { get; private set; } = string.Empty;

        public string FolderName { get; private set; } = string.Empty;

        public string FileSize { get; private set; } = string.Empty;

        public string Modified { get; private set; } = string.Empty;

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

        public bool IsLabeled
        {
            get => isLabeled;
            set => SetField(ref isLabeled, value);
        }

        public YoloImageReviewState ReviewState
        {
            get => reviewState;
            set => SetField(ref reviewState, value);
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
            return true;
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
                WpfImageQueueFilter.Unlabeled => "미라벨",
                WpfImageQueueFilter.Requested => "요청중",
                WpfImageQueueFilter.Candidate => "후보",
                WpfImageQueueFilter.Confirmed => "확정",
                WpfImageQueueFilter.Skipped => "스킵",
                WpfImageQueueFilter.NoCandidate => "검출없음",
                WpfImageQueueFilter.Failed => "실패",
                _ => "전체"
            };
        }
    }

    public enum WpfImageQueueFilter
    {
        All,
        Unlabeled,
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
