using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class ClassCatalogService
    {
        private static readonly Color[] Palette =
        {
            Color.FromArgb(34, 197, 94),
            Color.FromArgb(239, 68, 68),
            Color.FromArgb(245, 158, 11),
            Color.FromArgb(59, 130, 246),
            Color.FromArgb(168, 85, 247),
            Color.FromArgb(20, 184, 166),
            Color.FromArgb(236, 72, 153),
            Color.FromArgb(148, 163, 184)
        };

        public static IReadOnlyList<Color> DefaultPalette => Palette;

        public static bool TryAddClass(CData data, string className, out CClassItem classItem)
        {
            classItem = null;
            if (data == null)
            {
                return false;
            }

            data.ClassNamedList ??= new List<CClassItem>();

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

            data.ClassNamedList ??= new List<CClassItem>();

            string normalizedName = NormalizeClassName(className);
            int removed = data.ClassNamedList.RemoveAll(item => string.Equals(item.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            return removed > 0;
        }

        public static bool TryRenameClass(CData data, string currentName, string newName, out CClassItem classItem)
        {
            classItem = null;
            if (data == null)
            {
                return false;
            }

            data.ClassNamedList ??= new List<CClassItem>();

            string normalizedCurrentName = NormalizeClassName(currentName);
            string normalizedNewName = NormalizeClassName(newName);
            if (string.IsNullOrWhiteSpace(normalizedCurrentName)
                || string.IsNullOrWhiteSpace(normalizedNewName))
            {
                return false;
            }

            classItem = data.ClassNamedList.FirstOrDefault(item =>
                string.Equals(item?.Text, normalizedCurrentName, StringComparison.OrdinalIgnoreCase));
            if (classItem == null)
            {
                return false;
            }

            CClassItem targetItem = classItem;
            if (data.ClassNamedList.Any(item =>
                    !ReferenceEquals(item, targetItem)
                    && string.Equals(item?.Text, normalizedNewName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            classItem.Text = normalizedNewName;
            return true;
        }

        public static bool TrySetClassColor(CData data, string className, Color color, out CClassItem classItem)
        {
            classItem = null;
            if (data == null)
            {
                return false;
            }

            data.ClassNamedList ??= new List<CClassItem>();

            string normalizedName = NormalizeClassName(className);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            classItem = data.ClassNamedList.FirstOrDefault(item =>
                string.Equals(item?.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (classItem == null)
            {
                return false;
            }

            classItem.DrawColor = color;
            return true;
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
