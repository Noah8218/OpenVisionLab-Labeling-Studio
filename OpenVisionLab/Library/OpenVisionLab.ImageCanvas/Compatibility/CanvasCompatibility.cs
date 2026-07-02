using OpenVisionLab.ImageCanvas.OpenGLRendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenVisionLab.ImageCanvas.CanvasShapes
{
	using OpenVisionLab.ImageCanvas.CanvasShapes;

	public enum EditingType
	{
		None,
		Left,
		Right,
		Top,
		Bottom,
		LeftTop,
		RightTop,
		RightBottom,
		LeftBottom,
		Move
	}

	public enum LineOverType
	{
		None,
		HSplit,
		VSplit,
		SizeNWSE,
		SizeNESE,
		SizeNESW,
		Move2D
	}

	public struct CanvasPoint<T>
	{
		public CanvasPoint(T x, T y)
		{
			X = x;
			Y = y;
		}

		public T X { get; set; }
		public T Y { get; set; }

		public PointF ToPointF() => new PointF(Convert.ToSingle(X), Convert.ToSingle(Y));
	}

	public struct CanvasSize<T>
	{
		public CanvasSize(T width, T height)
		{
			Width = width;
			Height = height;
		}

		public T Width { get; set; }
		public T Height { get; set; }
	}

	public abstract class CanvasShape : ICloneable
	{
		public string UniqueId { get; set; }
		public string GroupType { get; set; }
		// Caller-owned metadata used by higher layers, e.g. label class preservation across ROI copy/paste.
		public string UserTag { get; set; } = string.Empty;
		public bool IsChanged { get; set; } = true;
		public Action OnChanged { get; set; }
		public uint DisplayListId { get; set; }
		public List<DotInfo> ShapePoints { get; protected set; } = new List<DotInfo>();

		public virtual object Clone() => MemberwiseClone();
	}

	public enum CanvasRoiShapeKind
	{
		Rectangle,
		Ellipse
	}

	public sealed class CanvasRect<T> : CanvasShape
	{
		private float _left;
		private float _top;
		private float _right;
		private float _bottom;
		private const float BoundaryHitEpsilon = 0.001f;
		private const float MinimumHandleHitScreenPixels = 4.0f;
		private const float MaximumCornerHitScreenPixels = 18.0f;
		private const float MaximumEdgeHitScreenPixels = 12.0f;

		public CanvasRect()
		{
			UpdateRectangle(0, 0, 0, 0);
		}

		public CanvasRect(float left, float top, float right, float bottom)
		{
			UpdateRectangle(left, top, right, bottom);
		}

		public float Left => Math.Min(_left, _right);
		public float Right => Math.Max(_left, _right);
		public float Top => Math.Max(_top, _bottom);
		public float Bottom => Math.Min(_top, _bottom);
		public float Width => Math.Abs(_right - _left);
		public float Height => Math.Abs(_top - _bottom);
		public float LineWidth { get; set; } = 1.0f;
		public bool IsEditing { get; set; }
		public bool IsFill { get; set; }
		public CanvasRoiShapeKind ShapeKind { get; set; } = CanvasRoiShapeKind.Rectangle;
		public EditingType EditingType { get; private set; }
		public CanvasRect<T> ExtendedRectangle { get; set; }

		public CanvasPoint<float> LeftTop => new CanvasPoint<float>(Left, Top);
		public CanvasPoint<float> RightTop => new CanvasPoint<float>(Right, Top);
		public CanvasPoint<float> RightBottom => new CanvasPoint<float>(Right, Bottom);
		public CanvasPoint<float> LeftBottom => new CanvasPoint<float>(Left, Bottom);
		public CanvasPoint<float> Center => new CanvasPoint<float>((Left + Right) / 2.0f, (Top + Bottom) / 2.0f);
		public IEnumerable<CanvasPoint<float>> Points => new[] { LeftTop, RightTop, RightBottom, LeftBottom };

		public void UpdateRectangle(float left, float top, float right, float bottom)
		{
			UpdateRectangle(left, top, right, bottom, notify: true);
		}

		private void UpdateRectangle(float left, float top, float right, float bottom, bool notify)
		{
			_left = left;
			_top = top;
			_right = right;
			_bottom = bottom;
			RefreshDots(notify);
			IsChanged = true;
			if (notify) OnChanged?.Invoke();
		}

		public bool IsEmpty() => Width <= 0 || Height <= 0;
		public bool Contain(float x, float y) => x >= Left && x <= Right && y >= Bottom && y <= Top;
		public RectangleF ToRobotRectangle() => new RectangleF(Left, Bottom, Width, Height);
		public Rectangle ToImageArea() => Rectangle.Round(new RectangleF(Left, Bottom, Width, Height));
		public CanvasRect<T> ToCanvasRect() => new CanvasRect<T>(Left, Top, Right, Bottom) { UniqueId = UniqueId, GroupType = GroupType, UserTag = UserTag, LineWidth = LineWidth, IsFill = IsFill, ShapeKind = ShapeKind };
		public object DeepClone() => ToCanvasRect();

		public void OffsetMove(CanvasSize<float> size, bool notify = true)
		{
			_left += size.Width;
			_right += size.Width;
			_top += size.Height;
			_bottom += size.Height;
			RefreshDots(notify);
			IsChanged = true;
			if (notify) OnChanged?.Invoke();
		}

		public void Move(float x, float y, Size imageSize, bool notify = true)
		{
			switch (EditingType)
			{
				case EditingType.Left:
					_left = Clamp(x, 0, imageSize.Width);
					break;
				case EditingType.Right:
					_right = Clamp(x, 0, imageSize.Width);
					break;
				case EditingType.Top:
					_top = Clamp(y, 0, imageSize.Height);
					break;
				case EditingType.Bottom:
					_bottom = Clamp(y, 0, imageSize.Height);
					break;
				case EditingType.LeftTop:
					_left = Clamp(x, 0, imageSize.Width);
					_top = Clamp(y, 0, imageSize.Height);
					break;
				case EditingType.RightTop:
					_right = Clamp(x, 0, imageSize.Width);
					_top = Clamp(y, 0, imageSize.Height);
					break;
				case EditingType.RightBottom:
					_right = Clamp(x, 0, imageSize.Width);
					_bottom = Clamp(y, 0, imageSize.Height);
					break;
				case EditingType.LeftBottom:
					_left = Clamp(x, 0, imageSize.Width);
					_bottom = Clamp(y, 0, imageSize.Height);
					break;
			}

			RefreshDots(notify);
			IsChanged = true;
			if (notify) OnChanged?.Invoke();
		}

		public void SetEditingType(float x, float y, float zoomScale, float handleSize)
		{
			EditingType = GetEditingTypeAtPoint(x, y, zoomScale, handleSize, allowVisibleHandleOutside: true);
		}

		public LineOverType CheckHandleContainsPosition(float x, float y, float zoomScale, float handleSize)
		{
			// Overlay selection itself must not leak outside the labeled rectangle.
			// Selected ROI editing uses GetHandleContainsPoint so visible handles remain easy to grab.
			return GetLineOverTypeAtPoint(x, y, zoomScale, handleSize, allowVisibleHandleOutside: false);
		}

		public void InitializeHandleRects(float handleSize)
		{
			LineWidth = handleSize;
		}

		public LineOverType GetHandleContainsPoint(float x, float y, float zoomScale, float handleSize)
		{
			// A selected box draws handles centered on the outline, so the editor hit-zone
			// must include the visible outside half of those handles.
			return GetLineOverTypeAtPoint(x, y, zoomScale, handleSize, allowVisibleHandleOutside: true);
		}

		public void CreateExtendedRectangleFromSize(float offset = 20.0f)
		{
			ExtendedRectangle = new CanvasRect<T>(Left - offset, Top + offset, Right + offset, Bottom - offset)
			{
				UniqueId = UniqueId,
				GroupType = GroupType,
				UserTag = UserTag,
				LineWidth = LineWidth
			};
		}

		public override object Clone() => ToCanvasRect();

		private void RefreshDots(bool notifyExtended = true)
		{
			ShapePoints = new List<DotInfo>
			{
				new DotInfo(Left, Top),
				new DotInfo(Right, Top),
				new DotInfo(Right, Bottom),
				new DotInfo(Left, Bottom)
			};

			if (ExtendedRectangle != null && !ReferenceEquals(ExtendedRectangle, this) && !IsEmpty())
			{
				ExtendedRectangle.UpdateRectangle(Left - 20, Top + 20, Right + 20, Bottom - 20, notifyExtended);
			}
		}

		private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
		private static float Distance(float x1, float y1, float x2, float y2) => (float)Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
		private bool IsInsideRoiBody(float x, float y) => x > Left && x < Right && y > Bottom && y < Top;
		private bool IsWithinRoiBoundsForHit(float x, float y) => x >= Left - BoundaryHitEpsilon && x <= Right + BoundaryHitEpsilon && y >= Bottom - BoundaryHitEpsilon && y <= Top + BoundaryHitEpsilon;
		private bool IsWithinHorizontalRange(float x, float tolerance) => x >= Left - tolerance && x <= Right + tolerance;
		private bool IsWithinVerticalRange(float y, float tolerance) => y >= Bottom - tolerance && y <= Top + tolerance;

		private EditingType GetEditingTypeAtPoint(float x, float y, float zoomScale, float handleSize, bool allowVisibleHandleOutside)
		{
			if (IsEmpty()) return EditingType.None;

			float cornerTolerance = GetCornerHitTolerance(zoomScale, handleSize);
			float edgeTolerance = GetEdgeHitTolerance(zoomScale, handleSize);

			// Edges are checked before the body so an overlapped ROI can still be selected
			// intentionally by clicking its outline instead of the smaller box inside it.
			if (IsCornerHit(x, y, Left, Top, cornerTolerance, allowVisibleHandleOutside)) return EditingType.LeftTop;
			if (IsCornerHit(x, y, Right, Top, cornerTolerance, allowVisibleHandleOutside)) return EditingType.RightTop;
			if (IsCornerHit(x, y, Right, Bottom, cornerTolerance, allowVisibleHandleOutside)) return EditingType.RightBottom;
			if (IsCornerHit(x, y, Left, Bottom, cornerTolerance, allowVisibleHandleOutside)) return EditingType.LeftBottom;
			if (IsVerticalEdgeHit(x, y, Left, edgeTolerance, allowVisibleHandleOutside)) return EditingType.Left;
			if (IsVerticalEdgeHit(x, y, Right, edgeTolerance, allowVisibleHandleOutside)) return EditingType.Right;
			if (IsHorizontalEdgeHit(x, y, Top, edgeTolerance, allowVisibleHandleOutside)) return EditingType.Top;
			if (IsHorizontalEdgeHit(x, y, Bottom, edgeTolerance, allowVisibleHandleOutside)) return EditingType.Bottom;
			if (IsInsideRoiBody(x, y)) return EditingType.Move;
			return Contain(x, y) ? EditingType.Move : EditingType.None;
		}

		private LineOverType GetLineOverTypeAtPoint(float x, float y, float zoomScale, float handleSize, bool allowVisibleHandleOutside)
		{
			return GetLineOverTypeFromEditingType(GetEditingTypeAtPoint(x, y, zoomScale, handleSize, allowVisibleHandleOutside));
		}

		private bool IsCornerHit(float x, float y, float cornerX, float cornerY, float tolerance, bool allowVisibleHandleOutside)
		{
			if (Distance(x, y, cornerX, cornerY) > tolerance)
			{
				return false;
			}

			return allowVisibleHandleOutside
				? IsWithinHorizontalRange(x, tolerance) && IsWithinVerticalRange(y, tolerance)
				: IsWithinRoiBoundsForHit(x, y);
		}

		private bool IsVerticalEdgeHit(float x, float y, float edgeX, float tolerance, bool allowVisibleHandleOutside)
		{
			if (Math.Abs(x - edgeX) > tolerance || !IsWithinVerticalRange(y, allowVisibleHandleOutside ? tolerance : 0.0f))
			{
				return false;
			}

			return allowVisibleHandleOutside || IsWithinRoiBoundsForHit(x, y);
		}

		private bool IsHorizontalEdgeHit(float x, float y, float edgeY, float tolerance, bool allowVisibleHandleOutside)
		{
			if (Math.Abs(y - edgeY) > tolerance || !IsWithinHorizontalRange(x, allowVisibleHandleOutside ? tolerance : 0.0f))
			{
				return false;
			}

			return allowVisibleHandleOutside || IsWithinRoiBoundsForHit(x, y);
		}

		private float GetBaseHitTolerance(float zoomScale, float handleSize)
		{
			float effectiveHandleSize = Math.Max(handleSize, LineWidth);
			float safeZoomScale = Math.Max(zoomScale, 0.0001f);
			float scaleAdjustedSize = effectiveHandleSize * safeZoomScale;
			float desiredSize = Math.Max(MinimumHandleHitScreenPixels * safeZoomScale, scaleAdjustedSize);
			float roiLimitedSize = Math.Max(1.0f, Math.Min(Width, Height) * 0.22f);
			return Math.Min(desiredSize, roiLimitedSize);
		}

		private float GetCornerHitTolerance(float zoomScale, float handleSize)
		{
			return Math.Min(GetBaseHitTolerance(zoomScale, handleSize), ToWorldPixels(zoomScale, MaximumCornerHitScreenPixels));
		}

		private float GetEdgeHitTolerance(float zoomScale, float handleSize)
		{
			float roiLimitedSize = Math.Max(1.0f, Math.Min(Width, Height) * 0.12f);
			return Math.Min(Math.Min(GetBaseHitTolerance(zoomScale, handleSize), roiLimitedSize), ToWorldPixels(zoomScale, MaximumEdgeHitScreenPixels));
		}

		private static float ToWorldPixels(float zoomScale, float screenPixels)
		{
			return Math.Max(0.0001f, zoomScale) * screenPixels;
		}

		private static LineOverType GetLineOverTypeFromEditingType(EditingType type)
		{
			return type switch
			{
				EditingType.Top or EditingType.Bottom => LineOverType.HSplit,
				EditingType.Left or EditingType.Right => LineOverType.VSplit,
				EditingType.LeftTop or EditingType.RightBottom => LineOverType.SizeNWSE,
				EditingType.RightTop or EditingType.LeftBottom => LineOverType.SizeNESE,
				EditingType.Move => LineOverType.Move2D,
				_ => LineOverType.None
			};
		}
	}
}

namespace OpenVisionLab.ImageCanvas.CanvasShapes
{
	using OpenVisionLab.ImageCanvas.Canvas;
	using OpenVisionLab.ImageCanvas.CanvasShapes;

	public enum LineType
	{
		Solid,
		Dash
	}

	public class DotInfo
	{
		public DotInfo()
		{
		}

		public DotInfo(float x, float y)
		{
			X = x;
			Y = y;
		}

		public float X { get; set; }
		public float Y { get; set; }
		public PointF ToPointF() => new PointF(X, Y);
	}

	public class LineInfo : CanvasShape
	{
		public LineInfo(DotInfo startDot, DotInfo endDot, LineType lineType, float width)
		{
			StartDot = startDot;
			EndDot = endDot;
			LineType = lineType;
			Width = width;
			ShapePoints = new List<DotInfo> { startDot, endDot };
		}

		public DotInfo StartDot { get; set; }
		public DotInfo EndDot { get; set; }
		public LineType LineType { get; set; }
		public float Width { get; set; }
		public float[] LineColor { get; set; } = { 1, 1, 1, 1 };
	}

	public class RectInfo : CanvasShape
	{
		public RectInfo(DotInfo leftTop, DotInfo leftBottom, DotInfo rightTop, DotInfo rightBottom, LineType lineType, float width)
		{
			LeftTop = leftTop;
			LeftBottom = leftBottom;
			RightTop = rightTop;
			RightBottom = rightBottom;
			LineType = lineType;
			Width = width;
			ShapePoints = new List<DotInfo> { leftTop, rightTop, rightBottom, leftBottom };
		}

		public DotInfo LeftTop { get; set; }
		public DotInfo LeftBottom { get; set; }
		public DotInfo RightTop { get; set; }
		public DotInfo RightBottom { get; set; }
		public LineType LineType { get; set; }
		public float Width { get; set; }
		public bool IsFill { get; set; }
		public float[] LineColor { get; set; } = { 1, 1, 1, 1 };
	}

	public class CircleInfo : CanvasShape
	{
		public CircleInfo(DotInfo centerDot, LineType lineType, float radius, float width)
		{
			CenterDot = centerDot;
			LineType = lineType;
			Radius = radius;
			Width = width;
			ShapePoints = BuildCircleDots(centerDot, radius);
		}

		public DotInfo CenterDot { get; set; }
		public DotInfo StartDot => new DotInfo(CenterDot.X - Radius, CenterDot.Y - Radius);
		public DotInfo EndDot => new DotInfo(CenterDot.X + Radius, CenterDot.Y + Radius);
		public LineType LineType { get; set; }
		public float Radius { get; set; }
		public float Width { get; set; }
		public bool IsFill { get; set; }
		public float[] LineColor { get; set; } = { 1, 1, 1, 1 };

		private static List<DotInfo> BuildCircleDots(DotInfo center, float radius)
		{
			return Enumerable.Range(0, 32)
				.Select(i =>
				{
					double angle = Math.PI * 2 * i / 32.0;
					return new DotInfo(center.X + (float)Math.Cos(angle) * radius, center.Y + (float)Math.Sin(angle) * radius);
				})
				.ToList();
		}
	}

	public class PensInfo : CanvasShape
	{
		public PensInfo(List<DotInfo> dots, LineType lineType, float width, float[] lineColor = null)
		{
			Dots = dots ?? new List<DotInfo>();
			LineType = lineType;
			Width = width;
			LineColor = lineColor ?? new[] { 1f, 1f, 1f, 1f };
			ShapePoints = Dots;
		}

		public List<DotInfo> Dots { get; set; }
		public LineType LineType { get; set; }
		public float Width { get; set; }
		public float[] LineColor { get; set; }
		public DotInfo ImageTitleOffset { get; set; } = new DotInfo();
	}

	public class TextInfo : CanvasShape
	{
		public string Text { get; set; }
		public DotInfo TextPositionDot { get; set; } = new DotInfo();
		public string FaceName { get; set; } = "Arial";
		public float BaseFontSize { get; set; } = 12;
		public float XSpan { get; set; }
		public float YSpan { get; set; }
		public SizeF OffsetSize { get; set; }
		public List<OpenGlFontBitmapEntry> FontBitmapEntries { get; set; } = new List<OpenGlFontBitmapEntry>();
		public float[] LineColor { get; set; } = { 1, 1, 1, 1 };
	}
}
