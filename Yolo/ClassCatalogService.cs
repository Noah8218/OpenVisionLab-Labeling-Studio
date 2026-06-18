using System;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class ClassCatalogService
    {
        private static readonly Color[] Palette =
        {
            Color.Green,
            Color.Red,
            Color.Blue,
            Color.Orange,
            Color.Pink,
            Color.Purple,
            Color.Navy,
            Color.LightSkyBlue
        };

        public static bool TryAddClass(CData data, string className, out CClassItem classItem)
        {
            classItem = null;
            if (data == null)
            {
                return false;
            }

            string normalizedName = NormalizeClassName(className);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            if (data.ClassNamedList.Any(item => string.Equals(item.Text, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            classItem = new CClassItem
            {
                Text = normalizedName,
                DrawColor = GetNextColor(data)
            };

            data.ClassNamedList.Add(classItem);
            return true;
        }

        public static bool RemoveClass(CData data, string className)
        {
            if (data == null)
            {
                return false;
            }

            string normalizedName = NormalizeClassName(className);
            int removed = data.ClassNamedList.RemoveAll(item => string.Equals(item.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            return removed > 0;
        }

        public static string NormalizeClassName(string className)
        {
            return (className ?? string.Empty).Trim();
        }

        private static Color GetNextColor(CData data)
        {
            foreach (Color color in Palette)
            {
                if (!data.ClassNamedList.Any(item => item.DrawColor == color))
                {
                    return color;
                }
            }

            return Color.LimeGreen;
        }
    }
}
