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
                "SuspicionBrush",
                "SuspicionBackgroundBrush",
                "BadBackgroundBrush",
                "ProgressTrackBrush",
                "SliderTrackBrush",
                "SliderTrackBorderBrush",
                "InputBrush",
                "InputBorderBrush",
                "GridLineBrush",
                "GridHeaderBrush",
                "RowHoverBrush",
                "SelectedRowBrush",
                "SelectedRowTextBrush",
                "RecipeSelectedCardBrush",
                "RecipeSelectedCardBorderBrush",
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
                new KeyValuePair<string, string>("AppBackgroundBrush", "#0E1116"),
                new KeyValuePair<string, string>("FrameBrush", "#0B0E13"),
                new KeyValuePair<string, string>("PanelBrush", "#161B22"),
                new KeyValuePair<string, string>("PanelHeaderBrush", "#1B212A"),
                new KeyValuePair<string, string>("CanvasBrush", "#0B0E12"),
                new KeyValuePair<string, string>("StatusBarBrush", "#0B0E13"),
                new KeyValuePair<string, string>("BorderBrushDark", "#283040"),
                new KeyValuePair<string, string>("PrimaryTextBrush", "#E6EBF2"),
                new KeyValuePair<string, string>("SecondaryTextBrush", "#9AA6B6"),
                new KeyValuePair<string, string>("AccentBrush", "#4C8DFF"),
                new KeyValuePair<string, string>("InfoBrush", "#4C8DFF"),
                new KeyValuePair<string, string>("SuccessBrush", "#3CCF8E"),
                new KeyValuePair<string, string>("WarningBrush", "#F2B84B"),
                new KeyValuePair<string, string>("SuspicionBrush", "#FFD24D"),
                new KeyValuePair<string, string>("SuspicionBackgroundBrush", "#262012"),
                new KeyValuePair<string, string>("ErrorBrush", "#F06A6A"),
                new KeyValuePair<string, string>("ToolbarButtonBrush", "#222A35"),
                new KeyValuePair<string, string>("ToolbarButtonBorderBrush", "#364152"),
                new KeyValuePair<string, string>("ToolbarButtonHoverBrush", "#2A3341"),
                new KeyValuePair<string, string>("ToolbarButtonPressedBrush", "#1D1D1D"),
                new KeyValuePair<string, string>("ToolbarButtonDisabledBrush", "#1B212A"),
                new KeyValuePair<string, string>("ToolbarButtonDisabledBorderBrush", "#283040"),
                new KeyValuePair<string, string>("DisabledTextBrush", "#6F7B8C"),
                new KeyValuePair<string, string>("BadBackgroundBrush", "#3D1E22"),
                new KeyValuePair<string, string>("ProgressTrackBrush", "#262E3A"),
                new KeyValuePair<string, string>("SliderTrackBrush", "#3B3B3B"),
                new KeyValuePair<string, string>("SliderTrackBorderBrush", "#858585"),
                new KeyValuePair<string, string>("InputBrush", "#11161D"),
                new KeyValuePair<string, string>("InputBorderBrush", "#364152"),
                new KeyValuePair<string, string>("GridLineBrush", "#283040"),
                new KeyValuePair<string, string>("GridHeaderBrush", "#161B22"),
                new KeyValuePair<string, string>("RowHoverBrush", "#2A3341"),
                new KeyValuePair<string, string>("SelectedRowBrush", "#1A2738"),
                new KeyValuePair<string, string>("SelectedRowTextBrush", "#FFFFFF"),
                new KeyValuePair<string, string>("RecipeSelectedCardBrush", "#182436"),
                new KeyValuePair<string, string>("RecipeSelectedCardBorderBrush", "#4C74B5"),
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
