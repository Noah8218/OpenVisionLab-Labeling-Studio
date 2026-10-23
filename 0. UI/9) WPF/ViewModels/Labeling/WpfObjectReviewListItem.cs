using System;
using System.Collections.Generic;
using System.Linq;
using OpenVisionLab;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfObjectReviewListItem : WpfObservableViewModel
    {
        private bool isMergeSelected;
        private bool isHidden;
        private bool isLocked;
        private bool isPinned;
        private bool isOccluded;
        private bool isMetadataFilterMatch = true;
        private bool isGroupSelectionVisible;
        private bool isGroupSelected;
        private string objectSessionStateText = "\uC138\uC158 \uC0C1\uD0DC \uC5C6\uC74C";
        private string sessionStateBadgeText = string.Empty;
        private string persistentMetadataBadgeText = string.Empty;
        private string groupBadgeText = string.Empty;
        private string groupId = string.Empty;
        private string groupDisplayText = string.Empty;
        private string stateBadgeText = string.Empty;
        private IReadOnlyList<string> metadataTags = Array.Empty<string>();
        private Action<WpfObjectReviewListItem, string> requestClassChange;
        private System.Windows.Media.Brush classBrush;
        private readonly string objectKindText;
        private readonly int displayIndex;
        private readonly string displayText;
        private readonly string toolTip;
        private readonly System.Drawing.Rectangle? manualBounds;
        private bool isLocalizedEmptyRow;

        public WpfObjectReviewListItem(
            string displayText,
            string toolTip,
            string sourceKey,
            int sourceIndex,
            object payload,
            bool isEnabled = true,
            bool isManualPolygon = false,
            string className = "",
            string objectKindText = "",
            int displayIndex = 0,
            System.Drawing.Rectangle? manualBounds = null)
        {
            this.displayText = displayText ?? string.Empty;
            this.toolTip = toolTip ?? string.Empty;
            this.manualBounds = manualBounds;
            SourceKey = sourceKey ?? string.Empty;
            SourceIndex = sourceIndex;
            Payload = payload;
            IsEnabled = isEnabled;
            IsManualPolygon = isManualPolygon;
            ClassName = className ?? string.Empty;
            this.objectKindText = objectKindText ?? string.Empty;
            this.displayIndex = displayIndex;
        }

        public string DisplayText => isLocalizedEmptyRow ? ObjectReviewPresenter.EmptyText
            : manualBounds.HasValue ? ObjectReviewPresenter.FormatManualSummary(displayIndex, ClassName, manualBounds.Value, objectKindText) : displayText;

        public string EmptyHintDisplayText => DisplayText.Replace("Ctrl+I", "Ctrl\u2060+\u2060I");

        public string Content => DisplayText;

        public string ClassName { get; }

        public string ObjectKindText
        {
            get
            {
                string kind = ObjectReviewPresenter.FormatShapeName(objectKindText);
                return displayIndex > 0 ? $"#{displayIndex} {kind}" : kind;
            }
        }

        public string SelectedClassName
        {
            get => ClassName;
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, ClassName, StringComparison.OrdinalIgnoreCase))
                {
                    requestClassChange?.Invoke(this, value);
                    OnPropertyChanged(nameof(SelectedClassName));
                }
            }
        }

        public System.Windows.Media.Brush ClassBrush
        {
            get => classBrush;
            private set => SetProperty(ref classBrush, value);
        }

        public bool IsInlineEditEnabled => ObjectReviewActionAvailabilityService.CanEditRow(this);

        public void ConfigureInlineClassChange(Action<WpfObjectReviewListItem, string> request)
            => requestClassChange = request;

        public void RefreshInlineClassSelection()
            => OnPropertyChanged(nameof(SelectedClassName));

        public void RefreshLocalizedPresentation()
        {
            sessionStateBadgeText = string.Join(" · ", new[]
            {
                IsHidden ? OpenVisionLanguageService.T("WpfObjectReview.Row.Hidden") : string.Empty,
                IsLocked ? OpenVisionLanguageService.T("WpfObjectReview.Row.Locked") : string.Empty,
                IsPinned ? OpenVisionLanguageService.T("WpfObjectReview.Row.Pinned") : string.Empty
            }.Where(text => !string.IsNullOrWhiteSpace(text)));
            RefreshStateBadgeText();
            ObjectSessionStateText = string.IsNullOrWhiteSpace(sessionStateBadgeText)
                ? OpenVisionLanguageService.T("WpfObjectReview.Session.None")
                : $"{sessionStateBadgeText} · {OpenVisionLanguageService.T("WpfObjectReview.Session.CurrentImageOnly")}";
            OnPropertyChanged(nameof(ObjectKindText));
            if (isLocalizedEmptyRow || manualBounds.HasValue)
            {
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(EmptyHintDisplayText));
                OnPropertyChanged(nameof(Content));
                if (manualBounds.HasValue) OnPropertyChanged(nameof(ToolTip));
            }
        }

        public void ApplyClassBrush(System.Windows.Media.Brush brush)
            => ClassBrush = brush;

        public string ToolTip => manualBounds.HasValue
            ? ObjectReviewPresenter.FormatManualDetail(ClassName, manualBounds.Value, objectKindText) : toolTip;

        public string SourceKey { get; }

        public int SourceIndex { get; }

        public object Payload { get; }

        public bool IsEnabled { get; }

        public bool IsManualSegment => IsEnabled
            && string.Equals(SourceKey, WpfObjectReviewSource.ManualSegment.ToString(), StringComparison.OrdinalIgnoreCase);

        public bool IsManualPolygon { get; }

        public bool SupportsSessionState => IsEnabled
            && (string.Equals(SourceKey, WpfObjectReviewSource.ManualRoi.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(SourceKey, WpfObjectReviewSource.ManualSegment.ToString(), StringComparison.OrdinalIgnoreCase));

        public bool SupportsPersistentMetadata => SupportsSessionState;

        public string GroupId
        {
            get => groupId;
            private set => SetProperty(ref groupId, value ?? string.Empty);
        }

        public string GroupDisplayText
        {
            get => groupDisplayText;
            private set => SetProperty(ref groupDisplayText, value ?? string.Empty);
        }

        public bool IsGroupSelectionVisible
        {
            get => isGroupSelectionVisible;
            private set
            {
                if (SetProperty(ref isGroupSelectionVisible, value))
                {
                    OnPropertyChanged(nameof(IsMergeSelectionVisible));
                }
            }
        }

        public bool CanGroupSelect => SupportsPersistentMetadata
            && string.IsNullOrWhiteSpace(GroupId);

        public bool IsGroupSelected
        {
            get => isGroupSelected;
            set => SetProperty(ref isGroupSelected, value);
        }

        public bool IsHidden
        {
            get => isHidden;
            private set => SetProperty(ref isHidden, value);
        }

        public bool IsLocked
        {
            get => isLocked;
            private set => SetProperty(ref isLocked, value);
        }

        public bool IsPinned
        {
            get => isPinned;
            private set => SetProperty(ref isPinned, value);
        }

        public string ObjectSessionStateText
        {
            get => objectSessionStateText;
            private set => SetProperty(ref objectSessionStateText, value ?? string.Empty);
        }

        public string StateBadgeText
        {
            get => stateBadgeText;
            private set => SetProperty(ref stateBadgeText, value ?? string.Empty);
        }

        public string MetadataBadgeText => string.Join(" \u00B7 ", new[] { groupBadgeText, persistentMetadataBadgeText }.Where(text => !string.IsNullOrWhiteSpace(text)));

        public double ContentOpacity => IsHidden ? 0.5D : IsLocked ? 0.72D : 1D;

        public bool IsOccluded
        {
            get => isOccluded;
            private set => SetProperty(ref isOccluded, value);
        }

        public IReadOnlyList<string> MetadataTags
        {
            get => metadataTags;
            private set => SetProperty(ref metadataTags, value ?? Array.Empty<string>());
        }

        public string MetadataTagsText => string.Join(", ", MetadataTags);

        public bool IsMetadataFilterMatch
        {
            get => isMetadataFilterMatch;
            private set => SetProperty(ref isMetadataFilterMatch, value);
        }

        public bool CanMergeSelect => IsManualSegment && !IsHidden && !IsLocked;

        public bool IsMergeSelectionVisible => CanMergeSelect && !IsGroupSelectionVisible;

        public bool IsMergeSelected
        {
            get => isMergeSelected;
            set => SetProperty(ref isMergeSelected, value);
        }

        public void ApplySessionState(WpfObjectSessionState state)
        {
            state ??= WpfObjectSessionState.Default;
            IsHidden = state.IsHidden;
            IsLocked = state.IsLocked;
            IsPinned = state.IsPinned;
            RefreshLocalizedPresentation();
            OnPropertyChanged(nameof(ContentOpacity));
            OnPropertyChanged(nameof(CanMergeSelect));
            OnPropertyChanged(nameof(IsMergeSelectionVisible));
            OnPropertyChanged(nameof(IsInlineEditEnabled));
            if (!CanMergeSelect)
            {
                IsMergeSelected = false;
            }
        }

        public void ApplyPersistentMetadata(WpfPersistentObjectMetadata metadata)
        {
            metadata ??= WpfPersistentObjectMetadata.Default;
            IsOccluded = metadata.IsOccluded;
            MetadataTags = metadata.Tags.ToList();
            GroupId = metadata.GroupId;
            persistentMetadataBadgeText = metadata.BadgeText;
            OnPropertyChanged(nameof(MetadataTagsText));
            OnPropertyChanged(nameof(CanGroupSelect));
            RefreshStateBadgeText();
        }

        public void ApplyGroupPresentation(string displayText, int memberCount)
        {
            GroupDisplayText = displayText;
            groupBadgeText = string.IsNullOrWhiteSpace(displayText)
                ? string.Empty
                : displayText.Replace($" ({Math.Max(0, memberCount)}\uAC1C)", string.Empty);
            OnPropertyChanged(nameof(CanGroupSelect));
            RefreshStateBadgeText();
        }

        public void SetGroupSelectionMode(bool active)
        {
            IsGroupSelectionVisible = active && SupportsPersistentMetadata;
            if (!IsGroupSelectionVisible || !CanGroupSelect)
            {
                IsGroupSelected = false;
            }
        }

        public void ApplyMetadataFilter(bool matches)
            => IsMetadataFilterMatch = matches;

        private void RefreshStateBadgeText()
        {
            StateBadgeText = string.Join(
                " \u00B7 ",
                new[] { MetadataBadgeText, sessionStateBadgeText }
                    .Where(text => !string.IsNullOrWhiteSpace(text)));
            OnPropertyChanged(nameof(MetadataBadgeText));
        }

        public static WpfObjectReviewListItem Empty(string text)
            => new WpfObjectReviewListItem(text, string.Empty, string.Empty, -1, null, isEnabled: false)
            {
                isLocalizedEmptyRow = string.Equals(text, ObjectReviewPresenter.EmptyText, StringComparison.Ordinal)
            };

        public override string ToString() => DisplayText;
    }
}
