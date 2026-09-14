using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF-only localization projection for the external canvas viewer
    /// status labels. It does not own canvas state, persistence, or panel lifetime.
    /// </summary>
    internal sealed class CanvasViewerStatusLocalizationAdapter
    {
        internal void Refresh(DependencyObject root)
        {
            foreach (Label label in EnumerateVisualChildren<Label>(root))
            {
                if (TryResolveFormatKey(label.ContentStringFormat, out string key))
                {
                    label.SetCurrentValue(
                        ContentControl.ContentStringFormatProperty,
                        OpenVisionLanguageService.T(key));
                }
            }
        }

        internal static bool TryResolveFormatKey(string format, out string key)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                key = string.Empty;
                return false;
            }

            if (format.IndexOf("좌표(좌하)", StringComparison.Ordinal) >= 0
                || format.IndexOf("Coordinates (bottom-left)", StringComparison.Ordinal) >= 0)
            {
                key = "WpfCanvas.ViewerStatus.RobotPosition";
                return true;
            }

            if (format.IndexOf("이미지좌표(좌상)", StringComparison.Ordinal) >= 0
                || format.IndexOf("Image coordinates (top-left)", StringComparison.Ordinal) >= 0)
            {
                key = "WpfCanvas.ViewerStatus.ImagePosition";
                return true;
            }

            if (format.IndexOf("색상", StringComparison.Ordinal) >= 0
                || format.IndexOf("Color:", StringComparison.Ordinal) >= 0)
            {
                key = "WpfCanvas.ViewerStatus.PixelColor";
                return true;
            }

            key = string.Empty;
            return false;
        }

        private static IEnumerable<T> EnumerateVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T descendant in EnumerateVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
