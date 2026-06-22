using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenVisionLab.ImageCanvas.ViewModels
{
	public sealed class RoiImageCanvasPolygonOverlay
	{
		public RoiImageCanvasPolygonOverlay(
			IEnumerable<Point> imagePoints,
			string label,
			Color color,
			bool isClosed,
			bool isDraft = false,
			bool isSelected = false,
			int selectedPointIndex = -1)
		{
			ImagePoints = imagePoints?.ToList() ?? new List<Point>();
			Label = label ?? string.Empty;
			Color = color;
			IsClosed = isClosed;
			IsDraft = isDraft;
			IsSelected = isSelected;
			SelectedPointIndex = selectedPointIndex;
		}

		public IReadOnlyList<Point> ImagePoints { get; }

		public string Label { get; }

		public Color Color { get; }

		public bool IsClosed { get; }

		public bool IsDraft { get; }

		public bool IsSelected { get; }

		public int SelectedPointIndex { get; }
	}
}
