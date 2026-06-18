using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    internal sealed class DisplayLayerStore
    {
        private readonly Func<List<FormLayerDisplay>> layersAccessor;

        public DisplayLayerStore(Func<List<FormLayerDisplay>> layersAccessor)
        {
            this.layersAccessor = layersAccessor ?? throw new ArgumentNullException(nameof(layersAccessor));
        }

        private List<FormLayerDisplay> Layers => layersAccessor() ?? new List<FormLayerDisplay>();

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

            List<FormLayerDisplay> layers = Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                FormLayerDisplay display = layers[i];
                if (display != null && !display.IsDisposed && string.Equals(display.Text, title, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public FormLayerDisplay Create(Bitmap imageSource, bool useClose, string title)
        {
            List<FormLayerDisplay> layers = Layers;
            var display = new FormLayerDisplay(imageSource, layers.Count, layers, useClose, title);
            layers.Add(display);
            return display;
        }

        public void RemoveEmpty()
        {
            List<FormLayerDisplay> layers = Layers;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i] == null || layers[i].IsDisposed || string.IsNullOrEmpty(layers[i].Text))
                {
                    layers.RemoveAt(i);
                }
            }
        }

        public FormLayerDisplay GetOrNull(int index)
        {
            List<FormLayerDisplay> layers = Layers;
            if (index < 0 || index >= layers.Count)
            {
                return null;
            }

            FormLayerDisplay display = layers[index];
            return display == null || display.IsDisposed ? null : display;
        }

        public FormLayerDisplay GetByTitleOrFirst(string title)
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
