using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.OpenCVSharp;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Overlays;
using SharpGL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace OpenVisionLab.ImageCanvas.OpenGLRendering
{
	public static partial class OpenGlDrawing
	{		public static void CompileOverlayShape(OpenGL gl, CanvasOverlayItem overlayItem)
		{
			AllocatDisplayId(gl, overlayItem.Shape);
			SetStencile(gl);
			gl.NewList(overlayItem.Shape.DisplayListId, OpenGL.GL_COMPILE);

			bool isDotted = (overlayItem.IsGroupRectangle && (overlayItem.Shape is CanvasRect<float>) && !(overlayItem.Shape as CanvasRect<float>).IsEmpty()) == true ? true : false;

			if (overlayItem.Shape is CanvasRect<float>)
			{
				CanvasRect<float> canvasRect = overlayItem.Shape as CanvasRect<float>;
				bool renderedAsEllipse = false;

				if (canvasRect.ShapeKind == CanvasRoiShapeKind.Ellipse && !overlayItem.IsGroupRectangle)
				{
					EnumFillMode fillMode = overlayItem.IsFill || canvasRect.IsFill
						? EnumFillMode.InFill
						: EnumFillMode.None;
					DrawEllipse(gl, canvasRect, canvasRect.LineWidth, new SolidColorBrush(ToMediaColor(overlayItem.Color)), fillMode);
					renderedAsEllipse = true;
				}
				else
				{
					DrawShape(gl, canvasRect, overlayItem.Color, isDotted, false, canvasRect.LineWidth);
				}

				if (canvasRect.ExtendedRectangle != null)
				{
					System.Drawing.Color extensionColor = overlayItem.Color;
					if (canvasRect.IsEditing)
					{
						extensionColor = System.Drawing.Color.DeepSkyBlue;
						DrawShape(gl, canvasRect.ExtendedRectangle, extensionColor, true, false);
					}
					else
					{
						if (overlayItem.IsExtensionRectangle)
						{
							DrawShape(gl, canvasRect.ExtendedRectangle, extensionColor, true, false);
						}
					}
				}
				if (overlayItem.IsFill && !renderedAsEllipse)
				{
					System.Drawing.PointF start = new System.Drawing.PointF(canvasRect.LeftBottom.X, canvasRect.LeftBottom.Y);
					System.Drawing.PointF end = new System.Drawing.PointF(canvasRect.RightTop.X, canvasRect.RightTop.Y);
					OpenGlDrawing.DrawRectangle(gl, start, end, 1, EnumFillMode.InFill, new SolidColorBrush(ToMediaColor(overlayItem.Color)), new SizeF());
				}
			}
			if (overlayItem.Shape is LineInfo)
			{
				LineInfo lineInfo = overlayItem.Shape as LineInfo;
				PointF startPoint = new PointF(lineInfo.StartDot.X, lineInfo.StartDot.Y);
				PointF endPoint = new PointF(lineInfo.EndDot.X, lineInfo.EndDot.Y);

				byte red = (byte)(lineInfo.LineColor[0] * 255);
				byte green = (byte)(lineInfo.LineColor[1] * 255);
				byte blue = (byte)(lineInfo.LineColor[2] * 255);

				System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(overlayItem.Color.R, overlayItem.Color.G, overlayItem.Color.B));

				DrawLine(gl, startPoint, endPoint, lineInfo.Width, brush);
			}

			if (overlayItem.Shape is RectInfo)
			{
				RectInfo rectInfo = overlayItem.Shape as RectInfo;
				System.Drawing.PointF start = new PointF(rectInfo.LeftBottom.X, rectInfo.LeftBottom.Y);
				System.Drawing.PointF end = new PointF(rectInfo.RightTop.X, rectInfo.RightTop.Y);

				byte red = (byte)(rectInfo.LineColor[0] * 255);
				byte green = (byte)(rectInfo.LineColor[1] * 255);
				byte blue = (byte)(rectInfo.LineColor[2] * 255);

				System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(overlayItem.Color.A, overlayItem.Color.R, overlayItem.Color.G, overlayItem.Color.B));

				DrawRectangle(gl, start, end, rectInfo.Width, rectInfo.IsFill, brush);
			}
			if (overlayItem.Shape is CircleInfo)
			{
				CircleInfo circleInfo = overlayItem.Shape as CircleInfo;
				System.Drawing.PointF center = new PointF(circleInfo.CenterDot.X, circleInfo.CenterDot.Y);
				float radius = circleInfo.Radius;
				byte red = (byte)(circleInfo.LineColor[0] * 255);
				byte green = (byte)(circleInfo.LineColor[1] * 255);
				byte blue = (byte)(circleInfo.LineColor[2] * 255);

				System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(overlayItem.Color.R, overlayItem.Color.G, overlayItem.Color.B));
				FastDrawCircle(gl, center, radius, circleInfo.Width, brush, circleInfo.IsFill);
			}
			if (overlayItem.Shape is PensInfo)
			{
				PensInfo pensInfo = overlayItem.Shape as PensInfo;

				byte red = (byte)(pensInfo.LineColor[0] * 255);
				byte green = (byte)(pensInfo.LineColor[1] * 255);
				byte blue = (byte)(pensInfo.LineColor[2] * 255);

				System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(overlayItem.Color.R, overlayItem.Color.G, overlayItem.Color.B));

				DrawShape(gl, pensInfo, overlayItem.Color, false, false);
			}
			if (overlayItem.Shape is OpenVisionLab.ImageCanvas.CanvasShapes.TextInfo)
			{
				OpenVisionLab.ImageCanvas.CanvasShapes.TextInfo textInfo = overlayItem.Shape as OpenVisionLab.ImageCanvas.CanvasShapes.TextInfo;

				float xSpan = textInfo.XSpan;
				float ySpan = textInfo.YSpan;
				SizeF offsetSize = textInfo.OffsetSize;
				float x = textInfo.TextPositionDot.X;
				float y = textInfo.TextPositionDot.Y;

				int r = (int)(textInfo.LineColor[0] * 255);
				int g = (int)(textInfo.LineColor[1] * 255);
				int b = (int)(textInfo.LineColor[2] * 255);

				System.Drawing.Color color = overlayItem.Color;

				string faceName = textInfo.FaceName;
				float baseFontSize = textInfo.BaseFontSize;
				if (textInfo.Text != null)
				{
					string text = textInfo.Text;

					DrawText(gl, textInfo.FontBitmapEntries, xSpan, ySpan, offsetSize, x, y, color, faceName, baseFontSize, text);
				}
			}

			gl.Disable(OpenGL.GL_LINES);
			gl.Disable(OpenGL.GL_LINE_STIPPLE);
			gl.EndList();
		}

		public static void SetStencile(OpenGL gl)
		{
			gl.Enable(OpenGL.GL_STENCIL_TEST);
			gl.ClearStencil(0);
			gl.StencilFunc(OpenGL.GL_ALWAYS, 1, 0xFF);
			gl.StencilOp(OpenGL.GL_KEEP, OpenGL.GL_KEEP, OpenGL.GL_REPLACE);
		}


		private static List<System.Drawing.PointF> ConvertDotInfoToPoints(List<DotInfo> listPoints)
		{
			List<System.Drawing.PointF> listDot = new List<System.Drawing.PointF>();
			foreach (var point in listPoints)
			{
				listDot.Add(new System.Drawing.PointF(point.X, point.Y));
			}
			return listDot;
		}


		private static void AllocatDisplayId(OpenGL gl, CanvasShape shape)
		{
			if (shape.DisplayListId != 0)
			{
				gl.DeleteLists(shape.DisplayListId, 1);
			}

			shape.DisplayListId = gl.GenLists(1);
		}
	}
}
