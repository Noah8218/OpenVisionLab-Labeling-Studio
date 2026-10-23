using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
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
    public sealed class WpfImageQueueCatalogEntry
    {
        private WpfImageQueueCatalogEntry(
            string imagePath,
            string fileName,
            string folderName,
            string fileSize,
            string modified)
        {
            ImagePath = imagePath ?? string.Empty;
            FileName = fileName ?? string.Empty;
            FolderName = folderName ?? string.Empty;
            FileSize = fileSize ?? string.Empty;
            Modified = modified ?? string.Empty;
        }

        public string ImagePath { get; }

        public string FileName { get; }

        public string FolderName { get; }

        public string FileSize { get; }

        public string Modified { get; }

        public static WpfImageQueueCatalogEntry Create(string imagePath)
        {
            FileInfo fileInfo = new FileInfo(imagePath ?? string.Empty);
            return new WpfImageQueueCatalogEntry(
                imagePath,
                Path.GetFileName(imagePath),
                fileInfo.Directory?.Name,
                FormatFileSize(fileInfo.Exists ? fileInfo.Length : 0),
                fileInfo.Exists
                    ? fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : string.Empty);
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
    }

    public sealed class WpfImageQueueItem : INotifyPropertyChanged
    {
        internal static readonly Brush ErrorBrush = CreateFrozenBrush("#F06A6A");
        internal static readonly Brush InfoBrush = CreateFrozenBrush("#B9D3FF");
        internal static readonly Brush MutedBrush = CreateFrozenBrush("#6F7B8C");
        internal static readonly Brush SuccessBrush = CreateFrozenBrush("#3CCF8E");
        internal static readonly Brush WarningBrush = CreateFrozenBrush("#F2B84B");
        internal static readonly Brush ErrorBadgeBrush = CreateFrozenBrush("#3D1E22");
        internal static readonly Brush InfoBadgeBrush = CreateFrozenBrush("#1C2F4D");
        internal static readonly Brush MutedBadgeBrush = CreateFrozenBrush("#222A35");
        internal static readonly Brush SuccessBadgeBrush = CreateFrozenBrush("#16352A");
        internal static readonly Brush WarningBadgeBrush = CreateFrozenBrush("#3A2E17");
        internal static readonly Brush LabelCompletedBrush = CreateFrozenBrush("#9CC3FF");
        internal static readonly Brush NoObjectBrush = CreateFrozenBrush("#C7B2FF");
        internal static readonly Brush TransparentBrush = CreateFrozenBrush("#00000000");

        private bool hasInferenceResult;
        private bool? inferenceIsNg;
        private string inferenceStateKey = "WpfInference.Workspace.NotRun";
        private string labelStatus = "확인중";
        private string detectStatus = "대기";
        private string dimensions = string.Empty;
        private string detail = string.Empty;
        private string queueStatusSummary = "상태 확인 중";
        private string queueBadgeText = string.Empty;
        private string queuePositionText = string.Empty;
        private PackIconMaterialKind queueIconKind = PackIconMaterialKind.ImageOutline;
        private Brush queueIconBrush = MutedBrush;
        private Brush queueBadgeBackgroundBrush = TransparentBrush;
        private Brush queueRowAccentBrush = TransparentBrush;
        private bool isLabeled;
        private bool hasSavedLabelFile;
        private bool isSaveRequired;
        private YoloImageReviewState reviewState;
        private YoloImageQualityReviewState qualityReviewState;
        private AnomalyImageReviewState anomalyReviewState;
        private bool isAnomalyReviewItem;
        private ImageSource thumbnailSource;
        private bool thumbnailLoadAttempted;
        private bool isAnomalyGridSelected;
        private bool isDatasetGridSelected;
        private string datasetSplit = "train";
        private ImageSource gridThumbnailSource;
        private bool gridThumbnailLoadAttempted;
        private int sessionCandidateCount;
        private int labelSuspicionCount;
        private double? candidateUncertainty;
        private double? modelDisagreement;
        private AnomalyImageReviewState sessionCandidateAnomalyState;
        private double? sessionCandidateAnomalyScore;
        private bool isLabelSuspicionEvaluated;
        private string labelSuspicionUnavailableReason = string.Empty;

        public bool HasInferenceResult => hasInferenceResult;
        public bool? InferenceIsNg => inferenceIsNg;
        public string InferenceStateText => T(inferenceStateKey);
        public Brush InferenceStateBrush => !hasInferenceResult ? MutedBrush : inferenceIsNg == true ? ErrorBrush : inferenceIsNg == false ? SuccessBrush : WarningBrush;

        internal bool SetInferencePresentation(bool hasResult, bool? isNg, string stateKey)
        {
            bool changed = SetField(ref hasInferenceResult, hasResult, nameof(HasInferenceResult));
            changed |= SetField(ref inferenceIsNg, isNg, nameof(InferenceIsNg));
            changed |= SetField(ref inferenceStateKey, stateKey, nameof(InferenceStateText));
            if (changed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InferenceStateBrush)));
            return changed;
        }

        public int SessionCandidateCount { get => sessionCandidateCount; set { if (SetField(ref sessionCandidateCount, Math.Max(0, value))) RefreshCandidateReviewSummary(); } }
        public int LabelSuspicionCount { get => labelSuspicionCount; set { if (SetField(ref labelSuspicionCount, Math.Max(0, value))) RefreshCandidateReviewSummary(); } }
        public double? CandidateUncertainty { get => candidateUncertainty; set => SetField(ref candidateUncertainty, value); }
        public double? ModelDisagreement { get => modelDisagreement; set => SetField(ref modelDisagreement, value); }
        public AnomalyImageReviewState SessionCandidateAnomalyState { get => sessionCandidateAnomalyState; set { if (SetField(ref sessionCandidateAnomalyState, value)) RefreshCandidateReviewSummary(); } }
        public double? SessionCandidateAnomalyScore { get => sessionCandidateAnomalyScore; set { if (SetField(ref sessionCandidateAnomalyScore, value)) RefreshCandidateReviewSummary(); } }
        public bool IsLabelSuspicionEvaluated { get => isLabelSuspicionEvaluated; set { if (SetField(ref isLabelSuspicionEvaluated, value)) RefreshCandidateReviewSummary(); } }
        public string LabelSuspicionUnavailableReason { get => labelSuspicionUnavailableReason; set { if (SetField(ref labelSuspicionUnavailableReason, value ?? string.Empty)) RefreshCandidateReviewSummary(); } }
        public string CandidateReviewBadgeText => string.Join(" · ", new[] {
            sessionCandidateAnomalyState != AnomalyImageReviewState.Unreviewed && sessionCandidateAnomalyScore.HasValue
                ? string.Format(CultureInfo.CurrentCulture, T("WpfCandidate.Batch.AnomalyRow"), T(sessionCandidateAnomalyState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.NormalState" : "WpfAnomalyGrid.AbnormalState"), sessionCandidateAnomalyScore.Value.ToString("0.####", CultureInfo.CurrentCulture))
                : sessionCandidateCount > 0 ? string.Format(CultureInfo.CurrentCulture, T("WpfCandidate.Batch.RowCount"), sessionCandidateCount) : string.Empty,
            labelSuspicionCount > 0 ? string.Format(CultureInfo.CurrentCulture, T("WpfLabelSuspicion.RowCount"), labelSuspicionCount) : string.Empty
        }.Where(text => !string.IsNullOrEmpty(text)));
        public string LabelSuspicionUnavailableText => labelSuspicionCount == 0 && !string.IsNullOrEmpty(labelSuspicionUnavailableReason)
            ? T("WpfLabelSuspicion.Queue.Unavailable") : string.Empty;
        public string LabelSuspicionUnavailableToolTip => string.IsNullOrEmpty(LabelSuspicionUnavailableText) ? string.Empty : T("WpfLabelSuspicion.Unavailable." + labelSuspicionUnavailableReason);
        public string CandidateReviewSummary => string.Join(" · ", new[] { CandidateReviewBadgeText, LabelSuspicionUnavailableText }.Where(text => !string.IsNullOrEmpty(text)));

        public event PropertyChangedEventHandler PropertyChanged;

        public string ImagePath { get; private set; } = string.Empty;

        public string FileName { get; private set; } = string.Empty;

        public string DisplayFileName => FormatCompactFileName(FileName);

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

        public ImageSource GridThumbnailSource
        {
            get
            {
                if (!gridThumbnailLoadAttempted)
                {
                    gridThumbnailLoadAttempted = true;
                    gridThumbnailSource = CreateThumbnailSource(ImagePath, 150);
                }
                return gridThumbnailSource;
            }
        }

        public bool IsAnomalyGridSelected { get => isAnomalyGridSelected; set => SetField(ref isAnomalyGridSelected, value); }

        public bool IsDatasetGridSelected { get => isDatasetGridSelected; set => SetField(ref isDatasetGridSelected, value); }

        public string DatasetSplit
        {
            get => datasetSplit;
            internal set
            {
                if (!SetField(ref datasetSplit, value ?? "train")) return;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetSplitText)));
            }
        }

        public string DatasetSplitText => T("WpfDatasetWorkspace.Split." + datasetSplit);
        public bool HasAnomalyVerdict => isAnomalyReviewItem && anomalyReviewState != AnomalyImageReviewState.Unreviewed;
        public string DatasetAnnotationCountText => !HasAnomalyVerdict && !hasSavedLabelFile ? "—"
            : HasSavedNoObjectLabel ? T("WpfCanvas.Save.NoObject")
            : string.IsNullOrEmpty(AnnotationCountText) ? string.Empty : string.Format(CultureInfo.CurrentCulture, T("WpfDatasetWorkspace.ObjectCount" + (AnnotationCountText == "1" ? "One" : string.Empty)), AnnotationCountText);

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

        public string LocalizedLabelStatus => LocalizeStatus(labelStatus, "WpfImageQueue.Row.SavePrefix");

        public string LocalizedDetectStatus => LocalizeStatus(detectStatus, "WpfImageQueue.Row.InspectPrefix");

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

        public string LocalizedQueueRowToolTip => BuildLocalizedQueueRowText(Environment.NewLine);

        public string LocalizedQueueRowAccessibleName => BuildLocalizedQueueRowText(" / ");

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

        public string LocalizedQueueBadgeText => LocalizeBadgeText(queueBadgeText);

        public string LocalizedQueueStatusSummary => isAnomalyReviewItem
            ? string.Format(CultureInfo.InvariantCulture, T("WpfAnomalyGrid.QueueSummary"), T(anomalyReviewState switch
            {
                AnomalyImageReviewState.Normal => "WpfAnomalyGrid.Normal",
                AnomalyImageReviewState.Abnormal => "WpfAnomalyGrid.Abnormal",
                _ => "WpfAnomalyGrid.Unreviewed"
            }), LocalizedImageReviewStatusText) : LocalizeSummary(queueStatusSummary);

        public string LocalizedImageReviewStatusText
        {
            get
            {
                if (isSaveRequired) return T("WpfShell.SaveBadge.Unsaved");
                string status = qualityReviewState switch
                {
                    YoloImageQualityReviewState.NeedsFix => T("WpfImageQueue.Current.NeedsFix.Badge"),
                    YoloImageQualityReviewState.Reviewed => T("WpfImageQueue.Current.QualityReviewed.Badge"),
                    _ => isAnomalyReviewItem ? T(anomalyReviewState == AnomalyImageReviewState.Unreviewed ? "WpfReview.Status.Unworked" : "WpfReview.Filter.Pending") : anomalyReviewState switch
                    {
                        AnomalyImageReviewState.Normal => T("WpfImageQueue.Row.ImageReview.Normal"),
                        AnomalyImageReviewState.Abnormal => T("WpfImageQueue.Row.ImageReview.Abnormal"),
                        _ => T(hasSavedLabelFile ? "WpfReview.Status.Labeled" : "WpfReview.Status.Unworked")
                    }
                };
                return HasSavedNoObjectLabel ? status + " · " + T("WpfCanvas.Save.NoObject") : status;
            }
        }

        public string DatasetReviewStatusText => !HasSavedNoObjectLabel || isSaveRequired ? LocalizedImageReviewStatusText : qualityReviewState switch
        {
            YoloImageQualityReviewState.NeedsFix => T("WpfImageQueue.Current.NeedsFix.Badge"),
            YoloImageQualityReviewState.Reviewed => T("WpfImageQueue.Current.QualityReviewed.Badge"),
            _ => T("WpfReview.Status.Labeled")
        };

        public Brush DatasetReviewStatusBrush => HasSavedNoObjectLabel && !isSaveRequired && qualityReviewState == YoloImageQualityReviewState.Unreviewed ? LabelCompletedBrush : ImageReviewStatusBrush;

        public string AnnotationCountText => isAnomalyReviewItem || !hasSavedLabelFile ? string.Empty
            : HasSavedNoObjectLabel ? "∅" : labelStatus.EndsWith("개", StringComparison.Ordinal) ? labelStatus[..^1].Trim() : string.Empty;

        public string LocalizedAnomalyVerdictText => T(anomalyReviewState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.Normal" : "WpfAnomalyGrid.Abnormal");

        public Brush ImageReviewStatusBrush => isSaveRequired ? WarningBrush : qualityReviewState switch
        {
            YoloImageQualityReviewState.NeedsFix => ErrorBrush,
            YoloImageQualityReviewState.Reviewed => SuccessBrush,
            _ => HasSavedNoObjectLabel ? NoObjectBrush : hasSavedLabelFile || isAnomalyReviewItem && anomalyReviewState != AnomalyImageReviewState.Unreviewed ? LabelCompletedBrush : MutedBrush
        };

        public bool HasSavedNoObjectLabel => !isAnomalyReviewItem && hasSavedLabelFile
            && (string.Equals(labelStatus, "객체 없음", StringComparison.Ordinal) || string.Equals(labelStatus, "Empty Label", StringComparison.OrdinalIgnoreCase));

        public string QueuePositionText
        {
            get => queuePositionText;
            private set => SetField(ref queuePositionText, value ?? string.Empty);
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

        public bool HasSavedLabelFile
        {
            get => hasSavedLabelFile;
            set => SetField(ref hasSavedLabelFile, value);
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

        public AnomalyImageReviewState AnomalyReviewState
        {
            get => anomalyReviewState;
            set => SetField(ref anomalyReviewState, value);
        }

        public bool IsAnomalyReviewItem
        {
            get => isAnomalyReviewItem;
            set => SetField(ref isAnomalyReviewItem, value);
        }

        internal void RefreshLocalizedPresentation()
        {
            RefreshCandidateReviewSummary();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InferenceStateText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetSplitText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetAnnotationCountText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetReviewStatusText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetReviewStatusBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedLabelStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedDetectStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueBadgeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueStatusSummary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedImageReviewStatusText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedAnomalyVerdictText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageReviewStatusBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnnotationCountText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueRowToolTip)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueRowAccessibleName)));
        }

        internal void SetQueuePosition(int position, int total) => QueuePositionText = $"{position} / {total}";

        public static WpfImageQueueItem CreateShell(string imagePath)
        {
            return CreateShell(WpfImageQueueCatalogEntry.Create(imagePath));
        }

        public static WpfImageQueueItem CreateShell(WpfImageQueueCatalogEntry entry)
        {
            return new WpfImageQueueItem
            {
                ImagePath = entry?.ImagePath ?? string.Empty,
                FileName = entry?.FileName ?? string.Empty,
                FolderName = entry?.FolderName ?? string.Empty,
                FileSize = entry?.FileSize ?? string.Empty,
                Modified = entry?.Modified ?? string.Empty
            };
        }

        private void RefreshCandidateReviewSummary()
        {
            foreach (string property in new[] { nameof(CandidateReviewSummary), nameof(CandidateReviewBadgeText), nameof(LabelSuspicionUnavailableText), nameof(LabelSuspicionUnavailableToolTip), nameof(LocalizedQueueRowToolTip) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedLabelStatus)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedDetectStatus)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueStatusSummary)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedImageReviewStatusText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedAnomalyVerdictText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageReviewStatusBrush)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnnotationCountText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAnomalyVerdict)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSavedNoObjectLabel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetAnnotationCountText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetReviewStatusText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DatasetReviewStatusBrush)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueRowToolTip)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueRowAccessibleName)));
            }

            if (string.Equals(propertyName, nameof(QueueBadgeText), StringComparison.Ordinal))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedQueueBadgeText)));
            }

            return true;
        }

        private string BuildLocalizedQueueRowText(string separator)
        {
            string normalizedSeparator = string.IsNullOrEmpty(separator) ? " / " : separator;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FileName))
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", T("WpfImageQueue.Row.FilePrefix"), FileName.Trim()));
            }

            parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", T("WpfImageQueue.Row.SavePrefix"), LocalizedLabelStatus));
            parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", T("WpfImageQueue.Row.InspectPrefix"), LocalizedDetectStatus));
            if (!string.IsNullOrWhiteSpace(Dimensions))
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", T("WpfImageQueue.Row.SizePrefix"), Dimensions.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(LocalizedQueueStatusSummary))
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", T("WpfImageQueue.Row.StatusPrefix"), LocalizedQueueStatusSummary));
            }

            parts.Add(LocalizedImageReviewStatusText);
            if (!string.IsNullOrWhiteSpace(LabelSuspicionUnavailableToolTip)) parts.Add(LabelSuspicionUnavailableToolTip);

            if (!string.IsNullOrWhiteSpace(Detail))
            {
                string localizedDetail = LocalizeSummary(Detail, normalizedSeparator, HasSavedLabelFile && !IsSaveRequired && ReviewState == YoloImageReviewState.Confirmed);
                string generatedDirtyPrefix = FileName + Environment.NewLine + "파일에 반영하려면 라벨 저장을 눌러야 합니다." + Environment.NewLine;
                if (IsSaveRequired && !string.IsNullOrWhiteSpace(FileName) && Detail.StartsWith(generatedDirtyPrefix, StringComparison.Ordinal))
                {
                    string reason = Detail.Substring(generatedDirtyPrefix.Length);
                    localizedDetail = FileName + " / " + T("WpfImageQueue.SaveRequired.Instruction") + " / " + LocalizationTextRuntimeService.TranslateAnnotationEditReason(reason);
                }
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", T("WpfImageQueue.Row.DetailPrefix"), localizedDetail));
            }

            return string.Join(normalizedSeparator, parts);
        }

        private static string LocalizeStatus(string value, string prefixKey)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeInlineText(value);
            foreach (string key in new[] { "WpfLabeling.WpfImageQueuePresenter.Unsaved", "WpfLabeling.WpfImageQueuePresenter.Rejected", "WpfLabeling.WpfImageQueuePresenter.Running", "WpfImageQueue.FilterOption.Confirmed", "WpfReview.Status.Reviewed" })
            {
                if (LocalizationTextRuntimeService.TryGetCachedTextPair(key, out string korean, out string english)
                    && (string.Equals(normalized, korean, StringComparison.Ordinal) || string.Equals(normalized, english, StringComparison.Ordinal)))
                    return T(key);
            }
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfObjectReview.QualityReview.NeedsFix"))
                normalized = normalized.Replace(previous, "반려", StringComparison.Ordinal);
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.Current.Failed.Title"))
                if (string.Equals(normalized, previous, StringComparison.Ordinal)) normalized = "추론 실패";
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.FilterOption.Requested"))
                if (string.Equals(normalized, previous, StringComparison.Ordinal)) normalized = "추론 중";
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.Current.SaveRequired.Badge"))
                if (string.Equals(normalized, previous, StringComparison.Ordinal)) normalized = "저장 안 됨";
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.FilterOption.NoCandidate"))
                if (string.Equals(normalized, previous, StringComparison.Ordinal)) normalized = "객체 없음";
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean)
            {
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }

                return string.Equals(prefixKey, "WpfImageQueue.Row.SavePrefix", StringComparison.Ordinal)
                    ? "없음"
                    : "대기";
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Equals(prefixKey, "WpfImageQueue.Row.SavePrefix", StringComparison.Ordinal)
                    ? "None"
                    : "Waiting";
            }

            return TranslateKnownStatus(normalized);
        }

        private static string LocalizeBadgeText(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeInlineText(value);
            return normalized.Length == 0 ? normalized : LocalizeStatus(normalized, string.Empty);
        }

        private static string LocalizeSummary(string value, string detailSeparator = " / ", bool hasSavedLabels = false)
        {
            string normalized = NormalizeInlineText(value);
            System.Text.RegularExpressions.Match detail = System.Text.RegularExpressions.Regex.Match(normalized,
                @"^저장 라벨: (?<labels>[^/]+) / AI 후보: (?<detection>[^/]+) / 품질 검수: (?<quality>[^/]+?)(?<tail> / .*)?$");
            if (detail.Success)
            {
                string labels = LocalizeStatus(detail.Groups["labels"].Value, "WpfImageQueue.Row.SavePrefix");
                string detection = LocalizeStatus(detail.Groups["detection"].Value, "WpfImageQueue.Row.InspectPrefix");
                string qualityValue = detail.Groups["quality"].Value;
                bool unreviewedQuality = qualityValue == "미판정" || qualityValue == "Unjudged" || qualityValue == "Undecided";
                if (LocalizationTextRuntimeService.TryGetCachedTextPair("WpfDatasetHealth.Metric.Unreviewed", out string unreviewedKorean, out string unreviewedEnglish)
                    && (string.Equals(qualityValue, unreviewedKorean, StringComparison.Ordinal) || string.Equals(qualityValue, unreviewedEnglish, StringComparison.Ordinal)))
                    unreviewedQuality = true;
                string quality = unreviewedQuality ? T("WpfDatasetHealth.Metric.Unreviewed") : LocalizeStatus(qualityValue, string.Empty);
                string tail = detail.Groups["tail"].Value;
                if (hasSavedLabels && string.Equals(tail, " / Candidates confirmed.", StringComparison.Ordinal))
                    tail = " / " + T("WpfImageQueue.Current.Saved.Title");
                return string.Format(CultureInfo.CurrentCulture, "{0}: {1}{6}{2}: {3}{6}{4}: {5}",
                    T("WpfShell.Right.Tab.SavedLabels"), labels, T("WpfImageQueue.Row.InspectPrefix"), detection,
                    T("WpfObjectReview.QualityReview.Title"), quality, detailSeparator) + tail;
            }
            foreach (string key in new[] { "WpfLabeling.WpfImageQueuePresenter.UnsavedSummary", "WpfLabeling.WpfImageQueuePresenter.RejectedSummary", "WpfLabeling.WpfImageQueuePresenter.SuggestionsToCheck", "WpfLabeling.WpfImageQueuePresenter.ReviewedSummary" })
            {
                if (!LocalizationTextRuntimeService.TryGetCachedTextPair(key, out string korean, out string english)) continue;
                foreach (string template in new[] { korean, english })
                {
                    int placeholder = template.IndexOf("{0}", StringComparison.Ordinal);
                    if (placeholder < 0 || template.IndexOf("{0}", placeholder + 3, StringComparison.Ordinal) >= 0) continue;
                    string prefix = template.Substring(0, placeholder);
                    string suffix = template.Substring(placeholder + 3);
                    if (!normalized.StartsWith(prefix, StringComparison.Ordinal) || !normalized.EndsWith(suffix, StringComparison.Ordinal)
                        || normalized.Length <= prefix.Length + suffix.Length) continue;
                    string argument = normalized.Substring(prefix.Length, normalized.Length - prefix.Length - suffix.Length);
                    if (key == "WpfLabeling.WpfImageQueuePresenter.SuggestionsToCheck" && argument.Any(character => character < '0' || character > '9')) continue;
                    if (key == "WpfLabeling.WpfImageQueuePresenter.RejectedSummary" || key == "WpfLabeling.WpfImageQueuePresenter.ReviewedSummary")
                    {
                        if (LocalizationTextRuntimeService.TryGetCachedTextPair("WpfImageQueue.FilterOption.NoCandidate", out string noObjectsKorean, out string noObjectsEnglish)
                            && (string.Equals(argument, noObjectsKorean, StringComparison.Ordinal) || string.Equals(argument, noObjectsEnglish, StringComparison.Ordinal)))
                            argument = T("WpfImageQueue.FilterOption.NoCandidate");
                        else
                        {
                            System.Text.RegularExpressions.Match count = System.Text.RegularExpressions.Regex.Match(argument, @"^([0-9]+)(?:개| items| objects?)?$");
                            if (count.Success) argument = string.Format(CultureInfo.CurrentCulture, T("WpfDatasetWorkspace.ObjectCount" + (count.Groups[1].Value == "1" ? "One" : string.Empty)), count.Groups[1].Value);
                        }
                    }
                    else if (key == "WpfLabeling.WpfImageQueuePresenter.UnsavedSummary"
                        && LocalizationTextRuntimeService.TryGetCachedTextPair("WpfShell.Right.Title.SavedLabels", out string editKorean, out string editEnglish)
                        && (string.Equals(argument, editKorean, StringComparison.Ordinal) || string.Equals(argument, editEnglish, StringComparison.Ordinal)))
                        argument = T("WpfShell.Right.Title.SavedLabels");
                    else if (key == "WpfLabeling.WpfImageQueuePresenter.UnsavedSummary")
                        argument = LocalizationTextRuntimeService.TranslateAnnotationEditReason(argument);
                    return string.Format(CultureInfo.CurrentCulture, T(key), argument);
                }
            }
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfObjectReview.QualityReview.NeedsFix"))
                normalized = normalized.Replace(previous, "반려", StringComparison.Ordinal);
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.Current.Failed.Title"))
                normalized = normalized.Replace(previous, "추론 실패", StringComparison.Ordinal);
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.FilterOption.Requested"))
                normalized = normalized.Replace(previous, "추론 중", StringComparison.Ordinal);
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.Current.SaveRequired.Badge"))
                normalized = normalized.Replace(previous, "저장 안 됨", StringComparison.Ordinal);
            foreach (string previous in OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.FilterOption.NoCandidate"))
                normalized = normalized.Replace(previous, "객체 없음", StringComparison.Ordinal);
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean
                || string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            System.Text.RegularExpressions.Match savedCount = System.Text.RegularExpressions.Regex.Match(normalized, @"^(?<state>저장 완료|후보 숨김 완료): 라벨 (?<count>[0-9]+개)$");
            if (savedCount.Success)
            {
                string key = savedCount.Groups["state"].Value == "저장 완료" ? "WpfImageQueue.FilterOption.Confirmed" : "WpfImageQueue.FilterOption.Skipped";
                return string.Format(CultureInfo.CurrentCulture, "{0}: {1}", T(key), TranslateKnownStatus(savedCount.Groups["count"].Value));
            }

            System.Text.RegularExpressions.Match noCandidateCount = System.Text.RegularExpressions.Regex.Match(normalized, @"^저장 라벨 (?<count>[0-9]+개) / AI 후보 0$");
            if (noCandidateCount.Success)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0}: {1} / {2} 0", T("WpfShell.Right.Tab.SavedLabels"), TranslateKnownStatus(noCandidateCount.Groups["count"].Value), T("WpfImageQueue.FilterOption.Candidate"));
            }

            string localized = normalized
                .Replace("상태 확인 중", "Checking status", StringComparison.Ordinal)
                .Replace("반려: 라벨", "Rejected: labels", StringComparison.Ordinal)
                .Replace("검수 완료: 라벨", "Reviewed: labels", StringComparison.Ordinal)
                .Replace("저장 완료: 라벨", "Saved: labels", StringComparison.Ordinal)
                .Replace("객체 없음 완료: 라벨", "No object: labels", StringComparison.Ordinal)
                .Replace("후보 숨김 완료: 라벨", "Candidate hidden: labels", StringComparison.Ordinal)
                .Replace("저장 라벨", "Saved labels", StringComparison.Ordinal)
                .Replace("AI 후보", "AI suggestions", StringComparison.Ordinal)
                .Replace("추론 실패", "Inference failed", StringComparison.Ordinal)
                .Replace("추론 중", "Running inference", StringComparison.Ordinal)
                .Replace("저장 안 됨", "Unsaved", StringComparison.Ordinal)
                .Replace("반려", "Rejected", StringComparison.Ordinal)
                .Replace("검수 완료", "Reviewed", StringComparison.Ordinal)
                .Replace("객체 없음", "No object", StringComparison.Ordinal)
                .Replace("후보 숨김", "Candidate hidden", StringComparison.Ordinal)
                .Replace("완료", "Complete", StringComparison.Ordinal)
                .Replace("대기", "Waiting", StringComparison.Ordinal)
                .Replace("없음", "None", StringComparison.Ordinal)
                .Replace("확인 필요", "Check needed", StringComparison.Ordinal);
            return localized;
        }

        private static string TranslateKnownStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.StartsWith("AI 후보 ", StringComparison.Ordinal))
            {
                return "AI suggestions " + value.Substring("AI 후보 ".Length);
            }

            if (value.StartsWith("실패 x", StringComparison.Ordinal))
            {
                return "Failed x" + value.Substring("실패 x".Length);
            }

            if (value.StartsWith("실패 ", StringComparison.Ordinal))
            {
                return "Failed " + value.Substring("실패 ".Length);
            }

            if (value.EndsWith("개", StringComparison.Ordinal)
                && value.Length > 1
                && value.Substring(0, value.Length - 1).All(char.IsDigit))
            {
                string count = value.Substring(0, value.Length - 1);
                return string.Format(CultureInfo.CurrentCulture, T("WpfDatasetWorkspace.ObjectCount" + (count == "1" ? "One" : string.Empty)), count);
            }

            if (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.FilterOption.NoCandidate").Contains(value, StringComparer.Ordinal)) return "No objects";
            if (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.Current.SaveRequired.Badge").Contains(value, StringComparer.Ordinal)) return "Unsaved";
            if (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.FilterOption.Requested").Contains(value, StringComparer.Ordinal)) return "Running inference";
            if (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfImageQueue.Current.Failed.Title").Contains(value, StringComparer.Ordinal)) return "Inference failed";

            return value switch
            {
                "확인중" => "Checking",
                "대기" => "Waiting",
                "없음" => "None",
                "객체 없음" => "No objects",
                "저장 안 됨" => "Unsaved",
                "추론 중" => "Running inference",
                "추론 실패" => "Inference failed",
                "실패" => "Failed",
                "저장됨" => "Saved",
                "후보 숨김" => "Candidate hidden",
                "반려" => "Rejected",
                "검수 완료" => "Reviewed",
                "미판정" => "Unjudged",
                "완료" => "Complete",
                "확인 필요" => "Check needed",
                "작업" => "Work",
                _ => value
            };
        }

        private static string T(string key) => OpenVisionLanguageService.T(key);

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

        public static string FormatCompactFileName(string fileName, int maximumLength = 34)
        {
            string normalized = (fileName ?? string.Empty).Trim();
            if (normalized.Length <= maximumLength || maximumLength < 12)
            {
                return normalized;
            }

            string extension = Path.GetExtension(normalized);
            int tailLength = Math.Min(Math.Max(extension.Length + 8, 12), maximumLength / 2);
            int headLength = maximumLength - tailLength - 1;
            return normalized.Substring(0, headLength) + "…" + normalized.Substring(normalized.Length - tailLength);
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
                || string.Equals(propertyName, nameof(ReviewState), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(QualityReviewState), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(HasSavedLabelFile), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(IsSaveRequired), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(AnomalyReviewState), StringComparison.Ordinal)
                || string.Equals(propertyName, nameof(IsAnomalyReviewItem), StringComparison.Ordinal);
        }

        internal static ImageSource CreateThumbnailSource(string imagePath, int decodePixelWidth = 42)
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
                    bitmap.DecodePixelWidth = Math.Max(1, decodePixelWidth);
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

    public sealed class WpfImageQueueFilterOption : INotifyPropertyChanged
    {
        private int count;
        private bool isAnomalyLabeling;

        public WpfImageQueueFilter Filter { get; set; }

        public string TextKey { get; set; } = string.Empty;

        public int Count
        {
            get => count;
            internal set
            {
                if (count == Math.Max(0, value)) return;
                count = Math.Max(0, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }

        internal bool IsAnomalyLabeling
        {
            get => isAnomalyLabeling;
            set
            {
                if (isAnomalyLabeling == value) return;
                isAnomalyLabeling = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LabelingText)));
            }
        }

        public string LabelingText => Filter switch
        {
            WpfImageQueueFilter.All => OpenVisionLanguageService.T("WpfImageQueue.Filter.All"),
            WpfImageQueueFilter.ReviewPending => OpenVisionLanguageService.T(isAnomalyLabeling ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Classified" : "WpfReview.Status.Labeled"),
            _ => Text
        };

        public string Text => string.IsNullOrWhiteSpace(TextKey)
            ? string.Empty
            : OpenVisionLanguageService.T(TextKey);

        public event PropertyChangedEventHandler PropertyChanged;

        public static IReadOnlyList<WpfImageQueueFilterOption> CreateDefaults()
        {
            return Enum.GetValues(typeof(WpfImageQueueFilter))
                .Cast<WpfImageQueueFilter>()
                .Select(filter => new WpfImageQueueFilterOption
                {
                    Filter = filter,
                    TextKey = GetTextKey(filter)
                })
                .ToList();
        }

        internal void RefreshLocalizedPresentation()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LabelingText)));
        }

        private static string GetTextKey(WpfImageQueueFilter filter)
        {
            return filter switch
            {
                WpfImageQueueFilter.Unlabeled => "WpfImageQueue.FilterOption.Unfinished",
                WpfImageQueueFilter.NeedsFix => "WpfImageQueue.FilterOption.NeedsFix",
                WpfImageQueueFilter.ReviewPending => "WpfReview.Filter.Pending",
                WpfImageQueueFilter.Reviewed => "WpfReview.Status.Reviewed",
                WpfImageQueueFilter.Requested => "WpfImageQueue.FilterOption.Requested",
                WpfImageQueueFilter.Candidate => "WpfImageQueue.FilterOption.Candidate",
                WpfImageQueueFilter.Confirmed => "WpfImageQueue.FilterOption.Confirmed",
                WpfImageQueueFilter.Skipped => "WpfImageQueue.FilterOption.Skipped",
                WpfImageQueueFilter.NoCandidate => "WpfImageQueue.FilterOption.NoCandidate",
                WpfImageQueueFilter.Failed => "WpfImageQueue.FilterOption.Failed",
                WpfImageQueueFilter.LabelSuspicion => "WpfLabelSuspicion.Filter",
                _ => "WpfImageQueue.FilterOption.All"
            };
        }

        public static string GetDisplayName(WpfImageQueueFilter filter)
        {
            return OpenVisionLanguageService.T(GetTextKey(filter));
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
        Failed,
        ReviewPending,
        Reviewed,
        LabelSuspicion
    }

    public enum ImageQueueSort { File, Uncertain, Disagreement }
    public sealed record ImageQueueSortOption(ImageQueueSort Sort, string Text, bool IsEnabled, string Reason);

    public sealed class WpfImageQueueDetail
    {
        public DrawingSize ImageSize { get; set; }

        public YoloImageReviewStatus ReviewStatus { get; set; }
    }
}
