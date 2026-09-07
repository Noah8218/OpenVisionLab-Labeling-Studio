using System;
using System.Collections.Generic;
using System.Linq;
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

        public WpfObjectReviewListItem(
            string displayText,
            string toolTip,
            string sourceKey,
            int sourceIndex,
            object payload,
            bool isEnabled = true,
            bool isManualPolygon = false)
        {
            DisplayText = displayText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            SourceKey = sourceKey ?? string.Empty;
            SourceIndex = sourceIndex;
            Payload = payload;
            IsEnabled = isEnabled;
            IsManualPolygon = isManualPolygon;
        }

        public string DisplayText { get; }

        public string Content => DisplayText;

        public string ToolTip { get; }

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
            sessionStateBadgeText = state.BadgeText;
            RefreshStateBadgeText();
            ObjectSessionStateText = state.IsDefault
                ? "\uC138\uC158 \uC0C1\uD0DC \uC5C6\uC74C \u00B7 \uC800\uC7A5/\uB0B4\uBCF4\uB0B4\uAE30 \uBE44\uC624\uC5FC"
                : $"{state.BadgeText} \u00B7 \uD604\uC7AC \uC774\uBBF8\uC9C0 \uC138\uC158\uB9CC";
            OnPropertyChanged(nameof(ContentOpacity));
            OnPropertyChanged(nameof(CanMergeSelect));
            OnPropertyChanged(nameof(IsMergeSelectionVisible));
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
                new[] { groupBadgeText, persistentMetadataBadgeText, sessionStateBadgeText }
                    .Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        public static WpfObjectReviewListItem Empty(string text)
            => new WpfObjectReviewListItem(text, string.Empty, string.Empty, -1, null, isEnabled: false);

        public override string ToString() => DisplayText;
    }
}
