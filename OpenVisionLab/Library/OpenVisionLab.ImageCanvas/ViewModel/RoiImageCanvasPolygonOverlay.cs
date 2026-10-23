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
			int selectedPointIndex = -1,
			string className = null,
			float opacity = 38F / 255F,
			bool showEditHandles = true,
			bool isBoxPointDraft = false)
		{
			ImagePoints = imagePoints?.ToList() ?? new List<Point>();
			Label = label ?? string.Empty;
			ClassName = className;
			Opacity = opacity;
			ShowEditHandles = showEditHandles;
			IsBoxPointDraft = isBoxPointDraft;
			Color = color;
			IsClosed = isClosed;
			IsDraft = isDraft;
			IsSelected = isSelected;
			SelectedPointIndex = selectedPointIndex;
		}

		public IReadOnlyList<Point> ImagePoints { get; }

		public string Label { get; }

		public string ClassName { get; }

		public Color Color { get; }

		public float Opacity { get; }

		public bool ShowEditHandles { get; }

		public bool IsBoxPointDraft { get; }

		public bool IsClosed { get; }

		public bool IsDraft { get; }

		public bool IsSelected { get; }

		public int SelectedPointIndex { get; }
	}
}
