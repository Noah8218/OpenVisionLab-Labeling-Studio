using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas label-class catalog projection and selection policy.
    /// WPF adapts the immutable result to binding items; class mutation remains in ClassCatalogService.
    /// </summary>
    public sealed class CanvasLabelClassCatalogPresentationService
    {
        private IReadOnlyList<CanvasLabelClassCatalogItem> items =
            Array.Empty<CanvasLabelClassCatalogItem>();
        private int selectedIndex = -1;

        public CanvasLabelClassCatalogSnapshot SetClasses(
            IEnumerable<LabelClass> classItems,
            string selectedName = "")
        {
            var projectedItems = new List<CanvasLabelClassCatalogItem>();
            string normalizedSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            int canonicalIndex = 0;
            int shortcutIndex = 1;

            foreach (LabelClass classItem in classItems ?? Enumerable.Empty<LabelClass>())
            {
                int currentCanonicalIndex = canonicalIndex++;
                if (!ClassCatalogService.IsActiveClass(classItem))
                {
                    continue;
                }

                projectedItems.Add(new CanvasLabelClassCatalogItem(
                    ClassCatalogService.NormalizeClassName(classItem.Text),
                    currentCanonicalIndex,
                    shortcutIndex++,
                    classItem.DrawColor));
            }

            items = projectedItems;
            selectedIndex = FindSelectedIndex(normalizedSelectedName);
            return BuildSnapshot();
        }

        public CanvasLabelClassCatalogSnapshot SelectByName(string className)
        {
            string normalizedName = ClassCatalogService.NormalizeClassName(className);
            int nextIndex = FindMatchingIndex(normalizedName);
            if (nextIndex >= 0)
            {
                selectedIndex = nextIndex;
            }

            return BuildSnapshot();
        }

        public bool TrySelectByShortcut(
            int zeroBasedIndex,
            out CanvasLabelClassCatalogSnapshot snapshot)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= Math.Min(9, items.Count))
            {
                snapshot = BuildSnapshot();
                return false;
            }

            selectedIndex = zeroBasedIndex;
            snapshot = BuildSnapshot();
            return true;
        }

        public CanvasLabelClassCatalogSnapshot GetSnapshot()
            => BuildSnapshot();

        private int FindSelectedIndex(string normalizedSelectedName)
        {
            int matchingIndex = FindMatchingIndex(normalizedSelectedName);
            if (matchingIndex >= 0)
            {
                return matchingIndex;
            }

            return items.Count > 0 ? 0 : -1;
        }

        private int FindMatchingIndex(string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return -1;
            }

            return items
                .Select((item, index) => new { item, index })
                .FirstOrDefault(candidate => string.Equals(
                    candidate.item.Text,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase))?.index ?? -1;
        }

        private CanvasLabelClassCatalogSnapshot BuildSnapshot()
        {
            return new CanvasLabelClassCatalogSnapshot(items, selectedIndex);
        }
    }

    public sealed class CanvasLabelClassCatalogSnapshot
    {
        public CanvasLabelClassCatalogSnapshot(
            IReadOnlyList<CanvasLabelClassCatalogItem> items,
            int selectedIndex)
        {
            Items = items ?? Array.Empty<CanvasLabelClassCatalogItem>();
            SelectedIndex = selectedIndex >= 0 && selectedIndex < Items.Count
                ? selectedIndex
                : -1;
        }

        public IReadOnlyList<CanvasLabelClassCatalogItem> Items { get; }

        public int SelectedIndex { get; }

        public CanvasLabelClassCatalogItem SelectedItem
            => SelectedIndex >= 0 ? Items[SelectedIndex] : null;
    }

    public sealed class CanvasLabelClassCatalogItem
    {
        public CanvasLabelClassCatalogItem(
            string text,
            int canonicalIndex,
            int shortcutIndex,
            Color drawColor)
        {
            Text = text ?? string.Empty;
            CanonicalIndex = Math.Max(0, canonicalIndex);
            ShortcutIndex = shortcutIndex is >= 1 and <= 9 ? shortcutIndex : 0;
            DrawColor = drawColor;
        }

        public string Text { get; }

        public int CanonicalIndex { get; }

        public int ShortcutIndex { get; }

        public Color DrawColor { get; }
    }
}
