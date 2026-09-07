using MahApps.Metro.IconPacks;
using System;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateReviewListItem
    {
        public WpfCandidateReviewListItem(
            string title,
            string secondaryText,
            string toolTip,
            object payload,
            PackIconMaterialKind iconKind,
            MediaBrush stateBrush,
            bool isEnabled = true)
        {
            Title = title ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            Payload = payload;
            IconKind = iconKind;
            StateBrush = stateBrush ?? MediaBrushes.Transparent;
            IsEnabled = isEnabled;
        }

        public string Title { get; }

        public string SecondaryText { get; }

        public string ToolTip { get; }

        public object Payload { get; }

        public PackIconMaterialKind IconKind { get; }

        public MediaBrush StateBrush { get; }

        public bool IsEnabled { get; }

        public string Content => Title;

        public static WpfCandidateReviewListItem Empty(string title, string toolTip)
            => new WpfCandidateReviewListItem(
                title,
                string.Empty,
                toolTip,
                null,
                PackIconMaterialKind.InformationOutline,
                MediaBrushes.Gray,
                isEnabled: false);

        public override string ToString() => Title;
    }
}
