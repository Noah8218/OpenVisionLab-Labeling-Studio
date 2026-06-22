using System.Drawing;

namespace OpenVisionLab.ImageCanvas.ViewModels
{
	public sealed class RoiImageCanvasMaskOverlay
	{
		public RoiImageCanvasMaskOverlay(
			string key,
			byte[] maskData,
			Size maskSize,
			Rectangle bounds,
			Color color,
			float opacity,
			int renderVersion,
			bool isSelected = false,
			string label = "",
			Rectangle dirtyBounds = default(Rectangle))
		{
			Key = string.IsNullOrWhiteSpace(key) ? "mask" : key;
			MaskData = maskData;
			MaskSize = maskSize;
			Bounds = bounds;
			Color = color;
			Opacity = opacity;
			RenderVersion = renderVersion;
			IsSelected = isSelected;
			Label = label ?? string.Empty;
			DirtyBounds = dirtyBounds;
		}

		public string Key { get; }

		public byte[] MaskData { get; }

		public Size MaskSize { get; }

		public Rectangle Bounds { get; }

		public Color Color { get; }

		public float Opacity { get; }

		public int RenderVersion { get; }

		public bool IsSelected { get; }

		public string Label { get; }

		public Rectangle DirtyBounds { get; }

		public bool IsValid =>
			MaskData != null
			&& MaskSize.Width > 0
			&& MaskSize.Height > 0
			&& MaskData.Length >= MaskSize.Width * MaskSize.Height
			&& Bounds.Width > 0
			&& Bounds.Height > 0;
	}
}
