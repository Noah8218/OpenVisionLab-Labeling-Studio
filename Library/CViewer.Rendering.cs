using MvcVisionSystem.DrawObject;
using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.OpenGLRendering;
using OpenVisionLab.ImageCanvas.Rendering;
using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Point = System.Drawing.Point;

namespace MvcVisionSystem
{
    public partial class CViewer
    {
        private const int OverlayLabelFontSize = 22;
        private const int OverlayLabelPaddingX = 10;
        private const int OverlayLabelPaddingY = 6;
        private const float OverlayLabelGap = 5F;
        private const int OverlayLabelAccentWidth = 5;
        private const string OverlayLabelFontFamily = "Segoe UI Semibold";

        private readonly Dictionary<LabelingSegmentationObject, RasterMaskTextureCache> rasterMaskTextureCaches = new Dictionary<LabelingSegmentationObject, RasterMaskTextureCache>();
        private readonly Dictionary<string, OverlayLabelTextureCache> overlayLabelTextureCaches = new Dictionary<string, OverlayLabelTextureCache>(StringComparer.Ordinal);

        private void OnCanvasDraw(object sender, CanvasRenderEventArgs e)
        {
            Canvas?.DrawContent();
            if (_currentImage == null)
            {
                return;
            }

            DrawRois(e.OpenGL);
            DrawDetectionOverlays(e.OpenGL);
            DrawCrosshair(e.OpenGL);
            DrawSegmentationPreview(e.OpenGL);
            DrawMeasurementOverlay(e.OpenGL);
        }

        private void DrawRois(OpenGL gl)
        {
            foreach (var roiGroup in _RoiDic)
            {
                for (int i = 0; i < roiGroup.Value.Count; i++)
                {
                    CRectangleObject roi = roiGroup.Value[i];
                    Color color = roi.Selected ? Color.Red : roi.cClassItem?.DrawColor ?? Color.LimeGreen;
                    DrawImageRectangle(gl, roi.Roi, color, roi.cClassItem?.Text ?? roiGroup.Key, roi.Selected);
                }
            }

            if (_Mode == LabelingRoiMode.Rectangle && !TempROI.IsEmpty)
            {
                DrawImageRectangle(gl, TempROI, _TempOb.cClassItem?.DrawColor ?? Color.LimeGreen, _TempOb.cClassItem?.Text ?? string.Empty, true);
            }
        }

        private void DrawImageRectangle(OpenGL gl, Rectangle roi, Color color, string title, bool selected)
        {
            if (roi.IsEmpty || _currentImage == null)
            {
                return;
            }

            RectangleF rect = ToOpenGlRectangle(roi);
            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, selected ? 0.32f : 0.18f);
            gl.Begin(OpenGL.GL_QUADS);
            gl.Vertex(rect.Left, rect.Top);
            gl.Vertex(rect.Right, rect.Top);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.End();

            gl.LineWidth(selected ? 2.5f : 1.6f);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
            gl.Begin(OpenGL.GL_LINE_LOOP);
            gl.Vertex(rect.Left, rect.Top);
            gl.Vertex(rect.Right, rect.Top);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.End();

            if (selected)
            {
                DrawHandles(gl, roi, color);
            }

            gl.PopAttrib();

            if (!string.IsNullOrWhiteSpace(title))
            {
                DrawLabel(gl, title, new PointF(roi.Left, _currentImage.Height - roi.Top + 3), color);
            }
        }

        private void DrawHandles(OpenGL gl, Rectangle roi, Color color)
        {
            int size = GetImageHandleSize();
            foreach (Point point in GetHandlePoints(roi))
            {
                Rectangle handle = new Rectangle(point.X - size / 2, point.Y - size / 2, size, size);
                RectangleF rect = ToOpenGlRectangle(handle);
                gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, 0.8f);
                gl.Begin(OpenGL.GL_QUADS);
                gl.Vertex(rect.Left, rect.Top);
                gl.Vertex(rect.Right, rect.Top);
                gl.Vertex(rect.Right, rect.Bottom);
                gl.Vertex(rect.Left, rect.Bottom);
                gl.End();
            }
        }

        private void DrawLabel(OpenGL gl, string title, PointF glPoint, Color color)
        {
            if (string.IsNullOrWhiteSpace(title) || Canvas == null)
            {
                return;
            }

            try
            {
                PointF screenPoint = Canvas.GetScreenPosFromPixelCoordf((int)Math.Round(glPoint.X), (int)Math.Round(glPoint.Y));
                DrawScreenLabel(gl, title.Trim(), screenPoint, color);
            }
            catch
            {
                try
                {
                    var options = Canvas.GetOpenGlTextDrawOptions();
                    OpenGlDrawing.DrawText(gl, options.FontBitmapEntries, options.XSpan, options.YSpan, options.OffsetSize, glPoint.X, glPoint.Y, Color.White, "Arial", OverlayLabelFontSize, title);
                }
                catch
                {
                    // Text rendering depends on the active OpenGL font context. ROI geometry remains valid if it is unavailable.
                }
            }
        }

        private void DrawScreenLabel(OpenGL gl, string title, PointF anchorScreenPoint, Color accentColor)
        {
            OverlayLabelTextureCache cache = GetOrCreateOverlayLabelTexture(gl, title, accentColor);
            if (cache?.TextureId == 0 || cache?.Width <= 0 || cache?.Height <= 0)
            {
                return;
            }

            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            float viewportWidth = Math.Max(1F, viewport[2]);
            float viewportHeight = Math.Max(1F, viewport[3]);

            float labelWidth = Math.Min(viewportWidth - 4F, cache.Width);
            float labelHeight = cache.Height;
            float x = Clamp(anchorScreenPoint.X, 2F, Math.Max(2F, viewportWidth - labelWidth - 2F));
            float y = anchorScreenPoint.Y - labelHeight - OverlayLabelGap;
            if (y < 2F)
            {
                y = anchorScreenPoint.Y + OverlayLabelGap;
            }

            y = Clamp(y, 2F, Math.Max(2F, viewportHeight - labelHeight - 2F));

            DrawScreenTexture(gl, cache.TextureId, new RectangleF(x, y, labelWidth, labelHeight));
        }

        private OverlayLabelTextureCache GetOrCreateOverlayLabelTexture(OpenGL gl, string title, Color accentColor)
        {
            Color accent = EnsureReadableOverlayColor(accentColor);
            string key = $"{title}\u001F{accent.ToArgb()}";
            IntPtr renderContext = gl.RenderContextProvider.RenderContextHandle;
            if (overlayLabelTextureCaches.TryGetValue(key, out OverlayLabelTextureCache cached)
                && cached.TextureId != 0
                && cached.RenderContext == renderContext)
            {
                return cached;
            }

            if (cached?.TextureId != 0)
            {
                DeleteOverlayLabelTexture(gl, cached);
            }

            if (overlayLabelTextureCaches.Count > 256)
            {
                ClearOverlayLabelTextureCache(gl);
            }

            OverlayLabelTextureCache texture = CreateOverlayLabelTexture(gl, title, accent);
            overlayLabelTextureCaches[key] = texture;
            return texture;
        }

        private static OverlayLabelTextureCache CreateOverlayLabelTexture(OpenGL gl, string title, Color accent)
        {
            using Font font = CreateOverlayLabelFont();
            Size textSize = TextRenderer.MeasureText(title, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int width = Math.Max(1, textSize.Width + (OverlayLabelPaddingX * 2) + OverlayLabelAccentWidth);
            int height = Math.Max(OverlayLabelFontSize + (OverlayLabelPaddingY * 2), textSize.Height + (OverlayLabelPaddingY * 2));

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                graphics.Clear(Color.Transparent);

                using var backgroundBrush = new SolidBrush(Color.FromArgb(235, 7, 10, 13));
                using var accentBrush = new SolidBrush(accent);
                using var borderPen = new Pen(Color.FromArgb(220, accent), 1F);
                graphics.FillRectangle(backgroundBrush, 0, 0, width, height);
                graphics.FillRectangle(accentBrush, 0, 0, OverlayLabelAccentWidth, height);
                graphics.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);

                Rectangle textRect = new Rectangle(
                    OverlayLabelAccentWidth + OverlayLabelPaddingX,
                    0,
                    Math.Max(1, width - OverlayLabelAccentWidth - (OverlayLabelPaddingX * 2)),
                    height);

                TextRenderer.DrawText(
                    graphics,
                    title,
                    font,
                    textRect,
                    Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }

            uint textureId = CreateTextureFromBitmap(gl, bitmap);
            return new OverlayLabelTextureCache
            {
                TextureId = textureId,
                Width = width,
                Height = height,
                RenderContext = gl.RenderContextProvider.RenderContextHandle
            };
        }

        private static Font CreateOverlayLabelFont()
        {
            try
            {
                return new Font(OverlayLabelFontFamily, OverlayLabelFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, OverlayLabelFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            }
        }

        private static uint CreateTextureFromBitmap(OpenGL gl, Bitmap bitmap)
        {
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                uint[] texture = new uint[1];
                gl.GenTextures(1, texture);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, texture[0]);
                gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 4);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA, bitmap.Width, bitmap.Height, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, data.Scan0);
                return texture[0];
            }
            finally
            {
                bitmap.UnlockBits(data);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            }
        }

        private static void DrawScreenTexture(OpenGL gl, uint textureId, RectangleF rect)
        {
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            float viewportWidth = Math.Max(1F, viewport[2]);
            float viewportHeight = Math.Max(1F, viewport[3]);

            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho2D(0, viewportWidth, viewportHeight, 0);

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Color(1F, 1F, 1F, 1F);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);
            gl.Begin(OpenGL.GL_QUADS);
            gl.TexCoord(0F, 0F);
            gl.Vertex(rect.Left, rect.Top);
            gl.TexCoord(1F, 0F);
            gl.Vertex(rect.Right, rect.Top);
            gl.TexCoord(1F, 1F);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.TexCoord(0F, 1F);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.End();
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PopAttrib();
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void DrawCrosshair(OpenGL gl)
        {
            if (_currentImage == null)
            {
                return;
            }

            if (_ViewCross)
            {
                DrawImageLine(gl, new Point(0, _currentImage.Height / 2), new Point(_currentImage.Width, _currentImage.Height / 2), Color.Yellow, 1.5f);
                DrawImageLine(gl, new Point(_currentImage.Width / 2, 0), new Point(_currentImage.Width / 2, _currentImage.Height), Color.Yellow, 1.5f);
            }

            if (!_Position.IsEmpty && _Position.X > 0 && _Position.Y > 0 && _Position.X < _currentImage.Width && _Position.Y < _currentImage.Height)
            {
                DrawImageLine(gl, new Point(0, _Position.Y), new Point(_currentImage.Width, _Position.Y), Color.Yellow, 1.0f);
                DrawImageLine(gl, new Point(_Position.X, 0), new Point(_Position.X, _currentImage.Height), Color.Yellow, 1.0f);
            }
        }

        private void DrawSegmentationPreview(OpenGL gl)
        {
            if (_currentImage == null)
            {
                return;
            }

            PruneRasterMaskTextureCache(gl);
            foreach (var segmentGroup in _SegmentationDic)
            {
                foreach (LabelingSegmentationObject segment in segmentGroup.Value)
                {
                    Color color = EnsureVisibleAnnotationColor(segment?.ClassItem?.DrawColor ?? segment?.Color ?? Color.GreenYellow);
                    if (segment?.IsRasterMask == true)
                    {
                        DrawRasterMask(gl, segment, color, segment.Selected ? 0.30f : 0.18f);
                        continue;
                    }

                    DrawPolygon(gl, segment?.Points, color, segment?.Selected == true ? 0.30f : 0.16f, segment?.Selected == true ? 3.8f : 2.4f);
                    foreach (List<Point> cutout in segment?.CutoutPolygons ?? new List<List<Point>>())
                    {
                        DrawPolygon(gl, cutout, Color.Black, 0.76f, 1.4f);
                        DrawPolygonOutline(gl, cutout, Color.OrangeRed, segment?.Selected == true ? 2.4f : 1.6f);
                    }
                }
            }

            if (_Mode == LabelingRoiMode.Segmentation && isDrawing)
            {
                DrawPointLine(gl, currentPoints, Color.GreenYellow, 6f);
            }

            if ((_Mode == LabelingRoiMode.SegmentationBrush || _Mode == LabelingRoiMode.SegmentationEraser) && !_Position.IsEmpty)
            {
                Color brushColor = _Mode == LabelingRoiMode.SegmentationEraser ? Color.OrangeRed : Color.GreenYellow;
                List<Point> brushPreview = SegmentationGeometry.CircleToPolygon(_Position, SegmentationBrushRadius, _currentImage.Size);
                DrawPolygon(gl, brushPreview, brushColor, 0.10f, 1.8f);
            }
        }

        private void DrawRasterMask(OpenGL gl, LabelingSegmentationObject segment, Color color, float fillAlpha)
        {
            if (segment?.IsRasterMask != true || segment.Bounds.IsEmpty)
            {
                return;
            }

            if (DrawRasterMaskTexture(gl, segment, color, fillAlpha))
            {
                return;
            }

            Rectangle bounds = segment.Bounds;
            int renderStep = GetRasterMaskRenderStep(bounds);
            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, fillAlpha);
            gl.Begin(OpenGL.GL_QUADS);
            for (int y = bounds.Top; y < bounds.Bottom; y += renderStep)
            {
                int rowOffset = y * segment.MaskSize.Width;
                int x = bounds.Left;
                while (x < bounds.Right)
                {
                    while (x < bounds.Right && segment.MaskData[rowOffset + x] == 0)
                    {
                        x += renderStep;
                    }

                    int start = x;
                    while (x < bounds.Right && segment.MaskData[rowOffset + x] != 0)
                    {
                        x += renderStep;
                    }

                    if (x <= start)
                    {
                        continue;
                    }

                    RectangleF rect = ToOpenGlRectangle(Rectangle.FromLTRB(start, y, Math.Min(bounds.Right, x), Math.Min(bounds.Bottom, y + renderStep)));
                    gl.Vertex(rect.Left, rect.Top);
                    gl.Vertex(rect.Right, rect.Top);
                    gl.Vertex(rect.Right, rect.Bottom);
                    gl.Vertex(rect.Left, rect.Bottom);
                }
            }

            gl.End();
            gl.PopAttrib();
        }

        private bool DrawRasterMaskTexture(OpenGL gl, LabelingSegmentationObject segment, Color color, float fillAlpha)
        {
            Rectangle bounds = segment.Bounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            if (!rasterMaskTextureCaches.TryGetValue(segment, out RasterMaskTextureCache cache))
            {
                cache = new RasterMaskTextureCache();
                rasterMaskTextureCaches[segment] = cache;
            }

            int colorKey = color.ToArgb();
            byte alpha = (byte)Math.Clamp((int)Math.Round(fillAlpha * 255D), 0, 255);
            bool textureChanged = cache.TextureId == 0
                || cache.Version != segment.RenderVersion
                || cache.Bounds != bounds
                || cache.ColorKey != colorKey
                || cache.Alpha != alpha;

            if (textureChanged && !UpdateRasterMaskTexture(gl, segment, bounds, color, alpha, cache))
            {
                return false;
            }

            RectangleF rect = ToOpenGlRectangle(bounds);
            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Color(1f, 1f, 1f, 1f);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, cache.TextureId);
            gl.Begin(OpenGL.GL_QUADS);
            gl.TexCoord(0.0f, 0.0f);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.TexCoord(1.0f, 0.0f);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.TexCoord(1.0f, 1.0f);
            gl.Vertex(rect.Right, rect.Top);
            gl.TexCoord(0.0f, 1.0f);
            gl.Vertex(rect.Left, rect.Top);
            gl.End();
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.PopAttrib();
            return true;
        }

        private bool UpdateRasterMaskTexture(OpenGL gl, LabelingSegmentationObject segment, Rectangle bounds, Color color, byte alpha, RasterMaskTextureCache cache)
        {
            if (segment.MaskData == null || segment.MaskSize.Width <= 0 || segment.MaskSize.Height <= 0)
            {
                return false;
            }

            byte[] textureData = RentRasterMaskTextureData(segment, bounds, color, alpha, cache);
            if (textureData.Length == 0)
            {
                return false;
            }

            GCHandle handle = GCHandle.Alloc(textureData, GCHandleType.Pinned);
            try
            {
                gl.Enable(OpenGL.GL_TEXTURE_2D);
                if (cache.TextureId == 0 || cache.Width != bounds.Width || cache.Height != bounds.Height)
                {
                    if (cache.TextureId != 0)
                    {
                        uint[] oldTexture = { cache.TextureId };
                        gl.DeleteTextures(1, oldTexture);
                    }

                    uint[] texture = new uint[1];
                    gl.GenTextures(1, texture);
                    cache.TextureId = texture[0];
                    cache.Width = bounds.Width;
                    cache.Height = bounds.Height;
                    gl.BindTexture(OpenGL.GL_TEXTURE_2D, cache.TextureId);
                    gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
                    gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
                    gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
                    gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
                    gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
                    gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA, bounds.Width, bounds.Height, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
                }
                else
                {
                    gl.BindTexture(OpenGL.GL_TEXTURE_2D, cache.TextureId);
                    gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA, bounds.Width, bounds.Height, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
                }
            }
            finally
            {
                handle.Free();
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            }

            cache.Version = segment.RenderVersion;
            cache.Bounds = bounds;
            cache.ColorKey = color.ToArgb();
            cache.Alpha = alpha;
            return true;
        }

        private static byte[] RentRasterMaskTextureData(LabelingSegmentationObject segment, Rectangle bounds, Color color, byte alpha, RasterMaskTextureCache cache)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return Array.Empty<byte>();
            }

            int requiredLength = bounds.Width * bounds.Height * 4;
            if (cache.TexturePixels == null || cache.TexturePixels.Length != requiredLength)
            {
                cache.TexturePixels = new byte[requiredLength];
            }
            else
            {
                Array.Clear(cache.TexturePixels, 0, cache.TexturePixels.Length);
            }

            byte[] pixels = cache.TexturePixels;
            for (int y = 0; y < bounds.Height; y++)
            {
                int sourceY = bounds.Top + y;
                int sourceOffset = sourceY * segment.MaskSize.Width + bounds.Left;
                int destinationOffset = y * bounds.Width * 4;
                for (int x = 0; x < bounds.Width; x++)
                {
                    if (segment.MaskData[sourceOffset + x] == 0)
                    {
                        continue;
                    }

                    int offset = destinationOffset + (x * 4);
                    pixels[offset] = color.R;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.B;
                    pixels[offset + 3] = alpha;
                }
            }

            return pixels;
        }

        private void ClearRasterMaskTextureCache()
        {
            OpenGL gl = null;
            try
            {
                gl = Canvas?.GetOpenGL();
            }
            catch
            {
                // The GL context may already be disposed during application shutdown.
            }

            ClearOverlayLabelTextureCache(gl);

            if (rasterMaskTextureCaches.Count == 0)
            {
                return;
            }

            try
            {
                if (gl != null)
                {
                    foreach (RasterMaskTextureCache cache in rasterMaskTextureCaches.Values)
                    {
                        if (cache.TextureId == 0)
                        {
                            continue;
                        }

                        uint[] texture = { cache.TextureId };
                        gl.DeleteTextures(1, texture);
                    }
                }
            }
            catch
            {
                // The GL context may already be disposed during application shutdown.
            }

            rasterMaskTextureCaches.Clear();
        }

        private void ClearOverlayLabelTextureCache(OpenGL gl)
        {
            if (overlayLabelTextureCaches.Count == 0)
            {
                return;
            }

            if (gl != null)
            {
                foreach (OverlayLabelTextureCache cache in overlayLabelTextureCaches.Values)
                {
                    DeleteOverlayLabelTexture(gl, cache);
                }
            }

            overlayLabelTextureCaches.Clear();
        }

        private static void DeleteOverlayLabelTexture(OpenGL gl, OverlayLabelTextureCache cache)
        {
            if (gl == null || cache == null || cache.TextureId == 0)
            {
                return;
            }

            try
            {
                uint[] texture = { cache.TextureId };
                gl.DeleteTextures(1, texture);
                cache.TextureId = 0;
            }
            catch
            {
                // The GL context may already be disposed during application shutdown.
            }
        }

        private void PruneRasterMaskTextureCache(OpenGL gl)
        {
            if (rasterMaskTextureCaches.Count == 0)
            {
                return;
            }

            var activeSegments = new HashSet<LabelingSegmentationObject>();
            foreach (List<LabelingSegmentationObject> segments in _SegmentationDic.Values)
            {
                if (segments == null)
                {
                    continue;
                }

                foreach (LabelingSegmentationObject segment in segments)
                {
                    if (segment?.IsRasterMask == true)
                    {
                        activeSegments.Add(segment);
                    }
                }
            }

            foreach (LabelingSegmentationObject staleSegment in rasterMaskTextureCaches.Keys.Where(segment => !activeSegments.Contains(segment)).ToList())
            {
                RasterMaskTextureCache cache = rasterMaskTextureCaches[staleSegment];
                if (cache.TextureId != 0)
                {
                    uint[] texture = { cache.TextureId };
                    gl.DeleteTextures(1, texture);
                }

                rasterMaskTextureCaches.Remove(staleSegment);
            }
        }

        private sealed class RasterMaskTextureCache
        {
            public uint TextureId { get; set; }

            public int Version { get; set; } = -1;

            public Rectangle Bounds { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public int ColorKey { get; set; }

            public byte Alpha { get; set; }

            public byte[] TexturePixels { get; set; }
        }

        private sealed class OverlayLabelTextureCache
        {
            public uint TextureId { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public IntPtr RenderContext { get; set; }
        }

        private int GetRasterMaskRenderStep(Rectangle bounds)
        {
            if (!isDrawing || (_Mode != LabelingRoiMode.SegmentationBrush && _Mode != LabelingRoiMode.SegmentationEraser))
            {
                return 1;
            }

            int area = Math.Max(0, bounds.Width * bounds.Height);
            if (area >= 250000)
            {
                return 4;
            }

            if (area >= 80000)
            {
                return 2;
            }

            return 1;
        }

        private void DrawPolygon(OpenGL gl, List<Point> points, Color color, float fillAlpha, float lineWidth)
        {
            if (points == null || points.Count < 3)
            {
                return;
            }

            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, fillAlpha);
            gl.Begin(OpenGL.GL_POLYGON);
            foreach (Point point in points)
            {
                PointF glPoint = ToOpenGlPoint(point);
                gl.Vertex(glPoint.X, glPoint.Y);
            }
            gl.End();

            gl.LineWidth(lineWidth);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
            gl.Begin(OpenGL.GL_LINE_LOOP);
            foreach (Point point in points)
            {
                PointF glPoint = ToOpenGlPoint(point);
                gl.Vertex(glPoint.X, glPoint.Y);
            }
            gl.End();
            gl.PopAttrib();
        }

        private void DrawPolygonOutline(OpenGL gl, List<Point> points, Color color, float lineWidth)
        {
            if (points == null || points.Count < 3)
            {
                return;
            }

            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LineWidth(lineWidth);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
            gl.Begin(OpenGL.GL_LINE_LOOP);
            foreach (Point point in points)
            {
                PointF glPoint = ToOpenGlPoint(point);
                gl.Vertex(glPoint.X, glPoint.Y);
            }
            gl.End();
            gl.PopAttrib();
        }

        private void DrawPointLine(OpenGL gl, List<Point> points, Color color, float width)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LineWidth(width);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, 0.35f);
            gl.Begin(OpenGL.GL_LINE_STRIP);
            foreach (Point point in points)
            {
                PointF glPoint = ToOpenGlPoint(point);
                gl.Vertex(glPoint.X, glPoint.Y);
            }
            gl.End();
            gl.PopAttrib();
        }

        private void DrawImageLine(OpenGL gl, Point start, Point end, Color color, float width)
        {
            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.LineWidth(width);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            gl.Begin(OpenGL.GL_LINES);
            PointF glStart = ToOpenGlPoint(start);
            PointF glEnd = ToOpenGlPoint(end);
            gl.Vertex(glStart.X, glStart.Y);
            gl.Vertex(glEnd.X, glEnd.Y);
            gl.End();
            gl.PopAttrib();
        }

        private void DrawDetectionOverlays(OpenGL gl)
        {
            if (_detectionOverlays.Count == 0 || _currentImage == null)
            {
                return;
            }

            foreach (DetectionOverlayItem overlay in _detectionOverlays)
            {
                Rectangle roi = Rectangle.Round(overlay.Bounds);
                if (roi.Width <= 0 || roi.Height <= 0)
                {
                    continue;
                }

                DrawDetectionOverlay(gl, roi, overlay);
            }
        }

        private void DrawDetectionOverlay(OpenGL gl, Rectangle roi, DetectionOverlayItem overlay)
        {
            if (roi.IsEmpty || overlay == null || _currentImage == null)
            {
                return;
            }

            Color baseColor = overlay.IsSelected ? Color.Yellow : EnsureReadableOverlayColor(overlay.Color);
            RectangleF rect = ToOpenGlRectangle(roi);

            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            gl.Color(baseColor.R / 255f, baseColor.G / 255f, baseColor.B / 255f, overlay.IsSelected ? 0.22f : 0.08f);
            gl.Begin(OpenGL.GL_QUADS);
            gl.Vertex(rect.Left, rect.Top);
            gl.Vertex(rect.Right, rect.Top);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.End();

            gl.LineWidth(overlay.IsSelected ? 3.2f : 2.1f);
            gl.Enable(OpenGL.GL_LINE_STIPPLE);
            gl.LineStipple(1, overlay.IsSelected ? (ushort)0x0FFF : (ushort)0x00FF);
            gl.Color(baseColor.R / 255f, baseColor.G / 255f, baseColor.B / 255f, 1f);
            gl.Begin(OpenGL.GL_LINE_LOOP);
            gl.Vertex(rect.Left, rect.Top);
            gl.Vertex(rect.Right, rect.Top);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.End();
            gl.Disable(OpenGL.GL_LINE_STIPPLE);

            if (overlay.IsSelected)
            {
                DrawCandidateCornerMarkers(gl, rect, baseColor);
            }

            gl.PopAttrib();

            DrawLabel(gl, overlay.Label, new PointF(roi.Left, _currentImage.Height - roi.Top + 3), baseColor);
        }

        private static Color EnsureReadableOverlayColor(Color color)
        {
            if (color.IsEmpty || (color.R + color.G + color.B) < 96)
            {
                return Color.FromArgb(72, 190, 255);
            }

            return color;
        }

        private static void DrawCandidateCornerMarkers(OpenGL gl, RectangleF rect, Color color)
        {
            float marker = Math.Max(8F, Math.Min(rect.Width, rect.Height) * 0.16F);
            gl.LineWidth(4F);
            gl.Color(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
            gl.Begin(OpenGL.GL_LINES);

            gl.Vertex(rect.Left, rect.Top);
            gl.Vertex(rect.Left + marker, rect.Top);
            gl.Vertex(rect.Left, rect.Top);
            gl.Vertex(rect.Left, rect.Top + marker);

            gl.Vertex(rect.Right, rect.Top);
            gl.Vertex(rect.Right - marker, rect.Top);
            gl.Vertex(rect.Right, rect.Top);
            gl.Vertex(rect.Right, rect.Top + marker);

            gl.Vertex(rect.Right, rect.Bottom);
            gl.Vertex(rect.Right - marker, rect.Bottom);
            gl.Vertex(rect.Right, rect.Bottom);
            gl.Vertex(rect.Right, rect.Bottom - marker);

            gl.Vertex(rect.Left, rect.Bottom);
            gl.Vertex(rect.Left + marker, rect.Bottom);
            gl.Vertex(rect.Left, rect.Bottom);
            gl.Vertex(rect.Left, rect.Bottom - marker);

            gl.End();
        }

        private void DrawMeasurementOverlay(OpenGL gl)
        {
            if (_currentImage == null)
            {
                return;
            }

            if (_measureLineStart.HasValue && _measureLineEnd.HasValue && _measureLineStart.Value != _measureLineEnd.Value)
            {
                DrawImageLine(gl, _measureLineStart.Value, _measureLineEnd.Value, Color.Red, 2F);
            }

            if (!_measureDistanceStart.HasValue || !_measureDistanceEnd.HasValue || _measureDistanceStart.Value == _measureDistanceEnd.Value)
            {
                return;
            }

            try
            {
                Canvas.PixelPermm = _measurePixelPermm;
                var fontOptions = new OpenGlFontRenderOptions(Color.Yellow, "Arial", 12F, string.Empty);
                Canvas.DrawMeasurement(gl, ToMeasurement(_measureDistanceStart.Value, _measureDistanceEnd.Value), fontOptions);
            }
            catch
            {
                DrawImageLine(gl, _measureDistanceStart.Value, _measureDistanceEnd.Value, Color.LimeGreen, 2F);
            }
        }

        private Measurement ToMeasurement(Point start, Point end)
        {
            return new Measurement
            {
                StartPoint = ToOpenGlPoint(start),
                EndPoint = ToOpenGlPoint(end)
            };
        }

        private RectangleF ToOpenGlRectangle(Rectangle imageRectangle)
        {
            return OpenGlImageGeometry.ToOpenGlRectangle(imageRectangle, _currentImage.Height);
        }

        private PointF ToOpenGlPoint(Point imagePoint)
        {
            return OpenGlImageGeometry.ToOpenGlPoint(imagePoint, _currentImage.Height);
        }

        private IEnumerable<Point> GetHandlePoints(Rectangle roi)
        {
            return OpenGlImageGeometry.GetHandlePoints(roi);
        }

        private int GetImageHandleSize()
        {
            return OpenGlImageGeometry.GetHandleSize(Canvas?.ZoomScale ?? 1F);
        }
    }
}
