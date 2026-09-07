using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the semantic brush values shared by the labeling shell and its
    /// child windows. The shell still owns when the palette is applied.
    /// </summary>
    internal static class ThemePalette
    {
        internal static IReadOnlyList<string> SharedResourceKeys { get; } =
            new[]
            {
                "AppBackgroundBrush",
                "FrameBrush",
                "PanelBrush",
                "PanelHeaderBrush",
                "CanvasBrush",
                "StatusBarBrush",
                "BorderBrushDark",
                "PrimaryTextBrush",
                "SecondaryTextBrush",
                "AccentBrush",
                "ModelCenterCandidateBrush",
                "ModelCenterDecisionBrush",
                "ModelCenterCandidatePanelBrush",
                "ModelCenterDecisionPanelBrush",
                "ToolbarButtonBrush",
                "ToolbarButtonBorderBrush",
                "ToolbarButtonHoverBrush",
                "ToolbarButtonPressedBrush",
                "ToolbarButtonDisabledBrush",
                "ToolbarButtonDisabledBorderBrush",
                "DisabledTextBrush",
                "InputBrush",
                "InputBorderBrush",
                "GridLineBrush",
                "GridHeaderBrush",
                "RowHoverBrush",
                "SelectedRowBrush",
                "SelectedRowTextBrush",
                "DetectionOverlayBackgroundBrush",
                "DetectionOverlayBorderBrush",
                "DetectionOverlayTitleTextBrush",
                "DetectionOverlaySummaryTextBrush",
                "DetectionOverlaySelectedBackgroundBrush",
                "DetectionOverlaySelectedTextBrush",
                "DetectionOverlayDetailTextBrush"
            };

        private static readonly IReadOnlyList<KeyValuePair<string, string>> DarkBrushes =
            new[]
            {
                new KeyValuePair<string, string>("AppBackgroundBrush", "#0C0D0F"),
                new KeyValuePair<string, string>("FrameBrush", "#0A0B0D"),
                new KeyValuePair<string, string>("PanelBrush", "#171717"),
                new KeyValuePair<string, string>("PanelHeaderBrush", "#1F1F1F"),
                new KeyValuePair<string, string>("CanvasBrush", "#101820"),
                new KeyValuePair<string, string>("StatusBarBrush", "#0F1115"),
                new KeyValuePair<string, string>("BorderBrushDark", "#303030"),
                new KeyValuePair<string, string>("PrimaryTextBrush", "#F7F7F7"),
                new KeyValuePair<string, string>("SecondaryTextBrush", "#B7B7B7"),
                new KeyValuePair<string, string>("AccentBrush", "#3B82F6"),
                new KeyValuePair<string, string>("InfoBrush", "#3B82F6"),
                new KeyValuePair<string, string>("SuccessBrush", "#22C55E"),
                new KeyValuePair<string, string>("WarningBrush", "#F59E0B"),
                new KeyValuePair<string, string>("ErrorBrush", "#EF4444"),
                new KeyValuePair<string, string>("ToolbarButtonBrush", "#252525"),
                new KeyValuePair<string, string>("ToolbarButtonBorderBrush", "#3A3A3A"),
                new KeyValuePair<string, string>("ToolbarButtonHoverBrush", "#333333"),
                new KeyValuePair<string, string>("ToolbarButtonPressedBrush", "#1D1D1D"),
                new KeyValuePair<string, string>("ToolbarButtonDisabledBrush", "#20242A"),
                new KeyValuePair<string, string>("ToolbarButtonDisabledBorderBrush", "#2B3038"),
                new KeyValuePair<string, string>("DisabledTextBrush", "#69707A"),
                new KeyValuePair<string, string>("InputBrush", "#242424"),
                new KeyValuePair<string, string>("InputBorderBrush", "#3A3A3A"),
                new KeyValuePair<string, string>("GridLineBrush", "#2A2A2A"),
                new KeyValuePair<string, string>("GridHeaderBrush", "#202020"),
                new KeyValuePair<string, string>("RowHoverBrush", "#222A33"),
                new KeyValuePair<string, string>("SelectedRowBrush", "#26384F"),
                new KeyValuePair<string, string>("SelectedRowTextBrush", "#FFFFFF"),
                new KeyValuePair<string, string>("DetectionOverlayBackgroundBrush", "#F00B1320"),
                new KeyValuePair<string, string>("DetectionOverlayBorderBrush", "#5524D366"),
                new KeyValuePair<string, string>("DetectionOverlayTitleTextBrush", "#FFFFFF"),
                new KeyValuePair<string, string>("DetectionOverlaySummaryTextBrush", "#BEEBD0"),
                new KeyValuePair<string, string>("DetectionOverlaySelectedBackgroundBrush", "#1F24D366"),
                new KeyValuePair<string, string>("DetectionOverlaySelectedTextBrush", "#FFFFFF"),
                new KeyValuePair<string, string>("DetectionOverlayDetailTextBrush", "#C9D4E2")
            };

        public static void ApplyDark(ResourceDictionary windowResources, ResourceDictionary applicationResources)
        {
            if (windowResources == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> brush in DarkBrushes)
            {
                SolidColorBrush value = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(brush.Value));
                windowResources[brush.Key] = value;
                if (applicationResources != null)
                {
                    applicationResources[brush.Key] = value;
                }
            }
        }
    }
}
