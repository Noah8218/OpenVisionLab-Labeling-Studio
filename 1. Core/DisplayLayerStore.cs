using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    internal sealed class DisplayLayerStore
    {
        private readonly Func<List<DisplayLayerDocument>> layersAccessor;

        public DisplayLayerStore(Func<List<DisplayLayerDocument>> layersAccessor)
        {
            this.layersAccessor = layersAccessor ?? throw new ArgumentNullException(nameof(layersAccessor));
        }

        private List<DisplayLayerDocument> Layers => layersAccessor() ?? new List<DisplayLayerDocument>();

        public int Count => Layers.Count;

        public IReadOnlyList<DisplayLayerInfo> GetInfos()
        {
            return Layers
                .Where(display => display != null && !display.IsDisposed)
                .Select((display, index) => new DisplayLayerInfo(index, display.Text))
                .ToList();
        }

        public string GetTitle(int index)
        {
            return GetOrNull(index)?.Text ?? string.Empty;
        }

        public int FindIndex(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return -1;
            }

            List<DisplayLayerDocument> layers = Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                DisplayLayerDocument display = layers[i];
                if (display != null && !display.IsDisposed && string.Equals(display.Text, title, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public DisplayLayerDocument Create(Bitmap imageSource, bool useClose, string title)
        {
            List<DisplayLayerDocument> layers = Layers;
            var display = new DisplayLayerDocument(imageSource, layers.Count, title);
            layers.Add(display);
            return display;
        }

        public void RemoveEmpty()
        {
            List<DisplayLayerDocument> layers = Layers;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i] == null || layers[i].IsDisposed || string.IsNullOrEmpty(layers[i].Text))
                {
                    layers.RemoveAt(i);
                }
            }
        }

        public DisplayLayerDocument GetOrNull(int index)
        {
            List<DisplayLayerDocument> layers = Layers;
            if (index < 0 || index >= layers.Count)
            {
                return null;
            }

            DisplayLayerDocument display = layers[index];
            return display == null || display.IsDisposed ? null : display;
        }

        public DisplayLayerDocument GetByTitleOrFirst(string title)
        {
            int index = FindIndex(title);
            if (index >= 0)
            {
                return GetOrNull(index);
            }

            return Layers.FirstOrDefault(display => display != null && !display.IsDisposed);
        }
    }
}
