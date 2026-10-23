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
	{		public static void DrawGroupName(OpenGL gl, CanvasOverlayManager overlayManager, OpenGlTextDrawOptions glDrawTextOptions)
		{
			foreach (var overlayItem in overlayManager.GetVisibleOverlaysInBounds(GetTextViewportBounds(glDrawTextOptions)))
			{
				if (!overlayItem.IsGroupRectangle) { continue; }

				if ((overlayItem.Shape as CanvasRect<float>).Width == 0 || (overlayItem.Shape as CanvasRect<float>).Height == 0) { continue; }
				float midX = (overlayItem.Shape as CanvasRect<float>).LeftTop.X;
				float midY = (overlayItem.Shape as CanvasRect<float>).LeftTop.Y + 20;

				string faceName = "Arial";
				float fontSize = 15;

				DrawText(gl, glDrawTextOptions.FontBitmapEntries, glDrawTextOptions.XSpan, glDrawTextOptions.YSpan, glDrawTextOptions.OffsetSize, (int)midX, (int)midY, overlayItem.Color, faceName, fontSize, overlayItem.GroupType);
			}
		}

		public static void DrawRoiItemName(OpenGL gl, CanvasOverlayManager overlayManager, OpenGlTextDrawOptions glDrawTextOptions)
			=> DrawRoiItemName(gl, overlayManager, glDrawTextOptions, false, true);

		public static void DrawRoiItemName(OpenGL gl, CanvasOverlayManager overlayManager, OpenGlTextDrawOptions glDrawTextOptions, bool showClassNames, bool showOrdinals)
		{
			int[] viewport = new int[4], previousMatrixMode = new int[1];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			if (viewport[2] <= 0 || viewport[3] <= 0) return;
			var textures = glDrawTextOptions.RoiLabelTextures ?? new Dictionary<string, (uint TextureId, int Width, int Height, IntPtr RenderContext)>(StringComparer.Ordinal);
			var used = glDrawTextOptions.RoiLabelTextureKeys ?? new HashSet<string>(StringComparer.Ordinal);
			gl.GetInteger(OpenGL.GL_MATRIX_MODE, previousMatrixMode);
			gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
			gl.MatrixMode(OpenGL.GL_PROJECTION); gl.PushMatrix();
			try
			{
				gl.LoadIdentity(); gl.Ortho2D(0, glDrawTextOptions.XSpan, 0, glDrawTextOptions.YSpan);
				gl.Translate(glDrawTextOptions.OffsetSize.Width, glDrawTextOptions.OffsetSize.Height, 0F);
				gl.MatrixMode(OpenGL.GL_MODELVIEW); gl.PushMatrix();
				try
				{
					gl.LoadIdentity(); gl.Disable(OpenGL.GL_LIGHTING); gl.Disable(OpenGL.GL_DEPTH_TEST);
					gl.Enable(OpenGL.GL_TEXTURE_2D); gl.Enable(OpenGL.GL_BLEND); gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA); gl.Color(1F, 1F, 1F, 1F);
					int index = 1;
					foreach (var overlayItem in overlayManager.GetVisibleOverlaysInBounds(GetTextViewportBounds(glDrawTextOptions)))
					{
						if (overlayItem.IsGroupRectangle || overlayItem.Shape is not CanvasRect<float> rect || rect.Width == 0 || rect.Height == 0) continue;
						string label = showClassNames || rect.IsLabelAnnotation && rect.IsEditing ? rect.UserTag ?? string.Empty : string.Empty;
						if (showOrdinals) label = $"{index} {label}".TrimEnd();
						index++;
						if (label.Length == 0) continue;
						var texture = GetRoiLabelTexture(gl, textures, used, label, overlayItem.Color);
						float width = texture.Width * glDrawTextOptions.XSpan / viewport[2];
						float height = texture.Height * glDrawTextOptions.YSpan / viewport[3];
						float uMax = texture.Width / (float)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)texture.Width);
						float vMax = texture.Height / (float)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)texture.Height);
						float x = rect.LeftTop.X, y = rect.LeftTop.Y;
						gl.BindTexture(OpenGL.GL_TEXTURE_2D, texture.TextureId);
						gl.Begin(OpenGL.GL_QUADS);
						gl.TexCoord(0F, 0F); gl.Vertex(x, y);
						gl.TexCoord(uMax, 0F); gl.Vertex(x + width, y);
						gl.TexCoord(uMax, vMax); gl.Vertex(x + width, y + height);
						gl.TexCoord(0F, vMax); gl.Vertex(x, y + height);
						gl.End();
					}
				}
				finally { gl.MatrixMode(OpenGL.GL_MODELVIEW); gl.PopMatrix(); }
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION); gl.PopMatrix(); gl.PopAttrib(); gl.MatrixMode((uint)previousMatrixMode[0]);
				if (glDrawTextOptions.RoiLabelTextureKeys == null) ReleaseRoiLabelTextures(gl, textures, glDrawTextOptions.RoiLabelTextures == null ? null : used);
			}
		}

		public static void DrawRoiLabelAtScreen(OpenGL gl, OpenGlTextDrawOptions options, string text, System.Drawing.Color classColor, float left, float top, byte backgroundAlpha = 255, bool bold = false, bool inference = false, float screenScale = 1F)
		{
			if (string.IsNullOrEmpty(text)) return;
			int[] viewport = new int[4], previousMatrixMode = new int[1];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			if (viewport[2] <= 0 || viewport[3] <= 0) return;
			var textures = options.RoiLabelTextures ?? new Dictionary<string, (uint TextureId, int Width, int Height, IntPtr RenderContext)>(StringComparer.Ordinal);
			gl.GetInteger(OpenGL.GL_MATRIX_MODE, previousMatrixMode);
			gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
			gl.MatrixMode(OpenGL.GL_PROJECTION); gl.PushMatrix();
			try
			{
				gl.LoadIdentity(); gl.Ortho2D(0, viewport[2], viewport[3], 0);
				gl.MatrixMode(OpenGL.GL_MODELVIEW); gl.PushMatrix();
				try
				{
					var texture = GetRoiLabelTexture(gl, textures, options.RoiLabelTextureKeys, text, classColor, backgroundAlpha, bold, inference);
					gl.LoadIdentity(); gl.Disable(OpenGL.GL_LIGHTING); gl.Disable(OpenGL.GL_DEPTH_TEST);
					gl.Enable(OpenGL.GL_TEXTURE_2D); gl.Enable(OpenGL.GL_BLEND); gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA); gl.Color(1F, 1F, 1F, 1F);
					float uMax = texture.Width / (float)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)texture.Width);
					float vMax = texture.Height / (float)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)texture.Height);
					gl.BindTexture(OpenGL.GL_TEXTURE_2D, texture.TextureId);
					gl.Begin(OpenGL.GL_QUADS);
					gl.TexCoord(0F, vMax); gl.Vertex(left, top);
					gl.TexCoord(uMax, vMax); gl.Vertex(left + texture.Width * screenScale, top);
					gl.TexCoord(uMax, 0F); gl.Vertex(left + texture.Width * screenScale, top + texture.Height * screenScale);
					gl.TexCoord(0F, 0F); gl.Vertex(left, top + texture.Height * screenScale);
					gl.End();
				}
				finally { gl.MatrixMode(OpenGL.GL_MODELVIEW); gl.PopMatrix(); }
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION); gl.PopMatrix(); gl.PopAttrib(); gl.MatrixMode((uint)previousMatrixMode[0]);
				if (options.RoiLabelTextures == null) ReleaseRoiLabelTextures(gl, textures);
			}
		}

		private static (uint TextureId, int Width, int Height, IntPtr RenderContext) GetRoiLabelTexture(OpenGL gl, Dictionary<string, (uint TextureId, int Width, int Height, IntPtr RenderContext)> textures, ISet<string> used, string text, System.Drawing.Color classColor, byte backgroundAlpha = 255, bool bold = false, bool inference = false)
		{
			string key = System.Drawing.Color.FromArgb(backgroundAlpha, classColor).ToArgb().ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + (inference ? ":Inference14:" : bold ? ":Bold:" : ":") + text;
			used?.Add(key);
			IntPtr context = gl.RenderContextProvider.RenderContextHandle;
			if (!textures.TryGetValue(key, out var texture) || texture.RenderContext != context)
			{
				using Bitmap bitmap = CreateRoiLabelBitmap(text, classColor, backgroundAlpha, bold, inference);
				bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
				// GDI Generic1.1에서도 같은 라벨을 업로드하도록 저장 크기만 확장한다.
				using var storage = new Bitmap((int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)bitmap.Width), (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)bitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				using (Graphics graphics = Graphics.FromImage(storage))
				{
					graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
					graphics.Clear(System.Drawing.Color.Transparent); graphics.DrawImageUnscaled(bitmap, 0, 0);
				}
				int wrapMode = gl.GetString(OpenGL.GL_VERSION).StartsWith("1.1", StringComparison.Ordinal) ? (int)OpenGL.GL_CLAMP : (int)OpenGL.GL_CLAMP_TO_EDGE;
				texture = (CreateTextTexture(gl, storage, wrapMode), bitmap.Width, bitmap.Height, context);
				textures[key] = texture;
			}
			return texture;
		}

		internal static Bitmap CreateRoiLabelBitmap(string text, System.Drawing.Color classColor, byte backgroundAlpha = 255, bool bold = false, bool inference = false)
		{
			using var font = new Font(bold ? "Segoe UI" : "Segoe UI Semibold", inference ? 14F : 12F, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
			int height = inference ? 20 : 18, inset = inference ? 7 : 6;
			using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
			format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
			SizeF measured;
			using (var sample = new Bitmap(1, 1))
			using (Graphics graphics = Graphics.FromImage(sample)) measured = graphics.MeasureString(text, font, int.MaxValue, format);
			int estimatedWidth = inference ? text.Length * 11 + 14 : 12 + text.EnumerateRunes().Sum(character => character.Value > 0x2E80 ? 12 : 7);
			var bitmap = new Bitmap(Math.Max(estimatedWidth, (int)Math.Ceiling(measured.Width) + inset * 2), height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			using (var background = new SolidBrush(System.Drawing.Color.FromArgb(backgroundAlpha, classColor)))
			using (var foreground = new SolidBrush(System.Drawing.Color.FromArgb(11, 14, 18)))
			using (var shape = new System.Drawing.Drawing2D.GraphicsPath())
			{
				graphics.Clear(System.Drawing.Color.Transparent);
				graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				shape.AddArc(0, 0, 6, 6, 180, 90); shape.AddArc(bitmap.Width - 6, 0, 6, 6, 270, 90);
				shape.AddArc(bitmap.Width - 6, height - 6, 6, 6, 0, 90); shape.AddArc(0, height - 6, 6, 6, 90, 90); shape.CloseFigure();
				graphics.FillPath(background, shape);
				graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
				graphics.DrawString(text, font, foreground, new PointF(inset, Math.Max(0F, (height - font.GetHeight(graphics)) / 2F)), format);
			}
			return bitmap;
		}

		internal static void ReleaseRoiLabelTextures(OpenGL gl, Dictionary<string, (uint TextureId, int Width, int Height, IntPtr RenderContext)> textures, ISet<string> liveKeys = null)
		{
			IntPtr context = gl.RenderContextProvider?.RenderContextHandle ?? IntPtr.Zero;
			foreach (string key in textures.Keys.ToArray())
			{
				if (liveKeys != null && liveKeys.Contains(key)) continue;
				var texture = textures[key];
				if (texture.RenderContext == context && texture.TextureId != 0) gl.DeleteTextures(1, new[] { texture.TextureId });
				textures.Remove(key);
			}
		}

		private static RectangleF GetTextViewportBounds(OpenGlTextDrawOptions glDrawTextOptions)
		{
			float viewportLeft = 0 - glDrawTextOptions.OffsetSize.Width;
			float viewportRight = glDrawTextOptions.XSpan - glDrawTextOptions.OffsetSize.Width;
			float viewportTop = glDrawTextOptions.YSpan - glDrawTextOptions.OffsetSize.Height;
			float viewportBottom = 0 - glDrawTextOptions.OffsetSize.Height;
			return RectangleF.FromLTRB(viewportLeft, viewportBottom, viewportRight, viewportTop);
		}

		public static uint CreateTextTexture(OpenGL gl, string text, Font font, System.Drawing.Color textColor)
		{
			SizeF textSize;
			using (var tempBitmap = new Bitmap(1, 1))
			{
				using (var g = Graphics.FromImage(tempBitmap))
				{
					textSize = g.MeasureString(text, font);
				}
			}

			int width = (int)Math.Ceiling(textSize.Width);
			int height = (int)Math.Ceiling(font.GetHeight());

			using (Bitmap bitmap = new Bitmap(width, height))
			{
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					graphics.Clear(System.Drawing.Color.Transparent);

					using (System.Drawing.Brush brush = new SolidBrush(textColor))
					{
						PointF position = new PointF(0, 0);
						graphics.DrawString(text, font, brush, position);
					}
				}
				bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

				return CreateTextTexture(gl, bitmap);
			}
		}
		internal static uint CreateTextTexture(OpenGL gl, Bitmap bitmap, int wrapMode = (int)OpenGL.GL_CLAMP_TO_EDGE)
		{
			uint[] texture = new uint[1]; gl.GenTextures(1, texture); gl.BindTexture(OpenGL.GL_TEXTURE_2D, texture[0]);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, wrapMode);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, wrapMode);
			BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			try { gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA, bitmap.Width, bitmap.Height, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, data.Scan0); }
			finally { bitmap.UnlockBits(data); }
			return texture[0];
		}
	}
}
