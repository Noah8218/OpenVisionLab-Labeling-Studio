using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.Model;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Overlays;
using OpenVisionLab.ImageCanvas.OpenGLRendering;
using SharpGL;
using SharpGL.Enumerations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenVisionLab.ImageCanvas.Rendering
{
	public partial class ImageCanvasControl : UserControl
	{
		#region Events
		public event EventHandler<OpenVisionLab.ImageCanvas.Canvas.CanvasRenderEventArgs> Draw;
		public event EventHandler Resized = delegate { };
		public event EventHandler MouseDoubleClicked = delegate { };
		public event EventHandler MouseClicked = delegate { };

		public new event EventHandler Load = delegate { };
		public new event EventHandler MouseLeave = delegate { };
		public new event EventHandler<KeyEventArgs> KeyDown = delegate { };
		public new event EventHandler<KeyEventArgs> KeyUp = delegate { };
		public new event EventHandler<CanvasMouseEventArgs> MouseMove = delegate { };
		public new event EventHandler<CanvasMouseEventArgs> MouseDown = delegate { };
		public new event EventHandler<CanvasMouseEventArgs> MouseUp = delegate { };
		public new event EventHandler<CanvasMouseEventArgs> MouseWheel = delegate { };
		#endregion

		private OpenGLControl openGLControl;
		private ImageCanvasOpenGlHostAdapter openGlHostAdapter;

		#region Constants
		private const float MIN_ZOOM_SCALE = 5000F;

		#endregion

		#region DllImport
		[DllImport("opengl32.dll", SetLastError = true)]
		public static extern void glTexSubImage2D(
				uint target,
				int level,
				int xoffset,
				int yoffset,
				int width,
				int height,
				uint format,
				uint type,
				IntPtr pixels
				);

		[System.Runtime.InteropServices.DllImport("opengl32.dll", EntryPoint = "glTexImage2D", ExactSpelling = true)]
		internal extern static void glTexImage2D(int target, int level, int internalformat, Int32 width, Int32 height, int border, int format, int type, IntPtr pixels);

		[DllImport("opengl32.dll", SetLastError = true)] private static extern void glGetTexImage(uint target, int level, uint format, uint type, byte[] pixels);


		private List<EraserParameter> _eraserParameters = new List<EraserParameter>();

		private List<System.Drawing.PointF> _penPoints = new List<System.Drawing.PointF>();

		#endregion

		#region Variables


		/// <summary>
		/// 줌 관련 값입니다.
		/// </summary>
		public float ZoomScale
		{
			get => _zoom / GetControlMinSize();
		}

		public System.Drawing.RectangleF _fitRect = new System.Drawing.RectangleF();
		private float _zoom = 100000.0f;
		private float _aspectRatio;
		private float _xSpan;
		private float _ySpan;

		/// <summary>
		/// 텍스처 관련 값입니다.
		/// </summary>
		private ConcurrentDictionary<string, List<OpenGlTextureDrawingParam>> _textureAreas = new ConcurrentDictionary<string, List<OpenGlTextureDrawingParam>>();
		public ConcurrentDictionary<string, List<OpenGlTextureDrawingParam>> TextureAreas
		{
			get => _textureAreas;
			set => _textureAreas = value;
		}
		private List<string> _textureKeysOrder = new List<string>();
		public List<string> TextureKeysOrder
		{
			get => _textureKeysOrder;
			set => _textureKeysOrder = value;
		}

		/// <summary>
		/// 드로잉 관련 값입니다.
		/// </summary>
		private System.Drawing.SizeF _offsetSize = new System.Drawing.SizeF(0.0f, 0.0f);

		/// <summary>
		/// OpenGL은 직접 텍스트 렌더링을 지원하지 않으므로 폰트 비트맵을 display list로 만들어 사용합니다.
		/// </summary>
		private readonly List<OpenGlFontBitmapEntry> _fontBitmapEntries = new List<OpenGlFontBitmapEntry>();
		private readonly object _pixelDatalock = new object();
		private readonly byte[] _pixelReadbackBuffer = new byte[4];
		private static readonly long PixelReadbackIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 30);
		private static readonly long DragOverlayRefreshIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 20);
		private static readonly long MouseMoveRepaintIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 60);
		private const int MaxVisibleOverlayShapes = 10_000;

		private System.Drawing.PointF _pixelPos;
		private System.Drawing.PointF _imagePixelPos;
		private System.Drawing.Color _pixelColor = System.Drawing.Color.Black;
		private int _grayValue;
		private bool _isShowCrossLine = false;
		protected int _invertValue = 1; // Y축 방향이 좌하단 기준일 때 좌표를 반전하기 위한 값입니다.

		private CanvasInteractionMode _viewMode = CanvasInteractionMode.None;
		private System.Drawing.PointF _preMousePos;
		private System.Drawing.PointF _postMousePos;

		private CanvasOverlayManager _overlayManager = new CanvasOverlayManager();
		private List<CanvasShape> _shapesViewPort = new List<CanvasShape>();
		private bool _suppressRefresh;
		private readonly Timer _deferredRefreshTimer = new Timer { Interval = 16 };
		private readonly Timer _deferredViewportOverlayRefreshTimer = new Timer { Interval = 16 };
		private bool _deferredRefreshPending;
		private bool _deferredRefreshPosted;
		private bool _deferredRefreshDelayActive;
		private bool _deferredViewportOverlayRefreshPending;
		private bool _visibleOverlayCacheDirty = true;
		private bool _fastOverlaySceneDirty = true;
		private bool _isVisibleOverlayLodActive;
		private int _visibleOverlayShapeCount;
		private int _visibleOverlayShapeLimit = MaxVisibleOverlayShapes;
		private uint _fastOverlaySceneListId;
		private CanvasShape _fastOverlaySceneLiveShape;
		private long _lastPixelReadbackTicks;
		private long _lastDragOverlayRefreshTicks;
		private long _lastMouseMoveRepaintTicks;
		private System.Drawing.Point _lastPixelReadbackPoint = System.Drawing.Point.Empty;

		public float PixelPermm { get; set; } = 0.001f;
		public float HandleSize = 10; // 기본 핸들 크기
		public bool IsVisibleOverlayLodActive => _isVisibleOverlayLodActive;
		public int VisibleOverlayShapeCount => _visibleOverlayShapeCount;
		public int VisibleOverlayShapeLimit => _visibleOverlayShapeLimit;
		public event EventHandler VisibleOverlayLodChanged = delegate { };

		#endregion

		#region Properties
		public bool IsShowCrossLine
		{
			get => _isShowCrossLine;
			set => _isShowCrossLine = value;
		}

		public int GrayValue
		{
			get => _grayValue;
			set => _grayValue = value;
		}

		public System.Drawing.PointF PixelPos
		{
			get => _pixelPos;
			set => _pixelPos = value;
		}

		public System.Drawing.PointF ImagePixelPos
		{
			get => _imagePixelPos;
			set => _imagePixelPos = value;
		}

		public System.Drawing.Color PixelColor
		{
			get => _pixelColor;
			set => _pixelColor = value;
		}

		public bool InvertYAxis
		{
			get { return _invertValue == 1; }
			set
			{
				if (value == true)
					_invertValue = -1;
				else
					_invertValue = 1;
			}
		}
		public System.Drawing.PointF PreMousePos
		{
			get => _preMousePos;
			set => _preMousePos = value;
		}

		public System.Drawing.PointF PostMousePos
		{
			get => _postMousePos;
			set => _postMousePos = value;
		}

		#endregion

		#region Contructor
		public ImageCanvasControl()
		{
			InitializeOpenGlHost();
			_deferredRefreshTimer.Tick += OnDeferredRefreshTimerTick;
			_deferredViewportOverlayRefreshTimer.Tick += OnDeferredViewportOverlayRefreshTimerTick;

			DoubleBuffered = false;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_deferredRefreshTimer.Tick -= OnDeferredRefreshTimerTick;
				_deferredViewportOverlayRefreshTimer.Tick -= OnDeferredViewportOverlayRefreshTimerTick;
				_deferredRefreshTimer.Dispose();
				_deferredViewportOverlayRefreshTimer.Dispose();
				openGlHostAdapter?.Dispose();
				openGlHostAdapter = null;
			}

			base.Dispose(disposing);
		}

		private void InitializeOpenGlHost()
		{
			SuspendLayout();
			AutoScaleDimensions = new SizeF(7F, 12F);
			AutoScaleMode = AutoScaleMode.Font;
			Margin = new Padding(3, 2, 3, 2);
			Name = nameof(ImageCanvasControl);
			Size = new Size(859, 562);

			openGlHostAdapter = new ImageCanvasOpenGlHostAdapter(
				OnLoad,
				OnResized,
				OnMouseDoubleClick,
				OnDraw,
				OnKeyUp,
				OnKeyDown,
				OnMouseClick,
				OnMouseDown,
				OnMouseMove,
				OnMouseUp,
				OnMouseWheel,
				OnMouseLeave);
			openGLControl = openGlHostAdapter.Control;
			Controls.Add(openGLControl);
			ResumeLayout(false);
			PerformLayout();
		}
		#endregion

		#region Event Handlers
		protected void OnLoad(object sender, EventArgs e) => Load(sender, e);
		protected void OnResized(object sender, EventArgs e) => Resized(sender, e);
		protected void OnMouseDoubleClick(object sender, MouseEventArgs e)
		{
			ZoomToFit();
			MouseDoubleClicked(sender, e);
		}

		protected void OnDraw(object sender, SharpGL.RenderEventArgs args)
		{
			OpenGL gl = openGLControl.OpenGL;
			PrepareForDrawing(gl);
			//DrawContent(gl);
			Draw?.Invoke(sender, new OpenVisionLab.ImageCanvas.Canvas.CanvasRenderEventArgs(gl));
		}

		protected void OnKeyUp(object sender, KeyEventArgs e) => KeyUp(sender, e);
		protected void OnKeyDown(object sender, KeyEventArgs e) => KeyDown(sender, e);
		protected void OnMouseClick(object sender, MouseEventArgs e) => MouseClicked(sender, e);
		protected void OnMouseDown(object sender, MouseEventArgs e)
		{
			_lastMouseMoveRepaintTicks = 0;
			_lastDragOverlayRefreshTicks = 0;
			MouseDown(sender, new CanvasMouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
		}

		protected void OnMouseMove(object sender, MouseEventArgs e)
		{
			bool isDraggingView = _viewMode == CanvasInteractionMode.Drag;
			bool isManipulatingOverlay = IsOverlayMouseManipulationMode(_viewMode) && e.Button != MouseButtons.None;
			if (isDraggingView && e.Button != MouseButtons.None)
			{
				DragViewMovement(e);
				if (ShouldRecalculateVisibleOverlaysOnMouseMove(isDraggingView))
				{
					InvalidateVisibleOverlayCache();
					RebuildVisibleOverlayCacheIfNeeded();
				}
				if (ShouldRequestOpenGlRepaintOnMouseMove(isDraggingView, isPointerDown: true))
				{
					RequestOpenGlRepaint();
				}

				return;
			}
			if (isManipulatingOverlay)
			{
				// ROI drag/resize must not do status readback or overlay cache work per event.
				// The ViewModel updates only the active ROI; repaint is frame-limited below.
				MouseMove(sender, new CanvasMouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
				if (ShouldRefreshMouseMoveRepaint())
				{
					RequestOpenGlRepaint();
				}

				return;
			}

			bool shouldReadPixel = e.Button == MouseButtons.None && ShouldReadPixelStatus(e);
			UpdatePixelStatus(e, shouldReadPixel);
			MouseMove(sender, new CanvasMouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
			if (ShouldRecalculateVisibleOverlaysOnMouseMove(isDraggingView))
			{
				InvalidateVisibleOverlayCache();
				RebuildVisibleOverlayCacheIfNeeded();
			}
			if (ShouldRequestOpenGlRepaintOnMouseMove(isDraggingView, e.Button != MouseButtons.None))
			{
				RequestOpenGlRepaint();
			}
		}

		protected void OnMouseUp(object sender, MouseEventArgs e)
		{
			bool shouldRefreshVisibleOverlayCacheAfterMouseUp = IsOverlayGeometryInteractionMode(_viewMode);
			MouseUp(sender, new CanvasMouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
			ResetMousePositions();
			if (shouldRefreshVisibleOverlayCacheAfterMouseUp)
			{
				InvalidateVisibleOverlayCache();
			}
			RebuildVisibleOverlayCacheIfNeeded();
			RequestOpenGlRepaint();
		}

		protected void OnMouseLeave(object sender, EventArgs e) => MouseLeave(sender, e);

		protected void OnMouseWheel(object sender, MouseEventArgs e)
		{
			MouseWheel(sender, new CanvasMouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
			OpenGlDrawing.ZoomFactor = ZoomScale;
		}


		#endregion

		private void PrepareForDrawing(OpenGL gl)
		{
			gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);
			gl.LoadIdentity();
			gl.Translate(_offsetSize.Width, _offsetSize.Height, -0f);
			gl.Disable(OpenGL.GL_DEPTH_TEST);
		}

		#region // public methods
		public string GetNameGL() => openGLControl.Name;
		public void SetNameGL(string name) => openGLControl.Name = name;
		public System.Drawing.Size GetSize() => openGLControl.Size;
		public OpenGL GetOpenGL() => openGLControl.OpenGL;
		public OpenGLControl GetOpenGLControl() => openGLControl;
		public CanvasOverlayManager GetCanvasOverlayManager() => _overlayManager;
		public List<CanvasOverlayItem> FindInteractiveOverlaysNearPoint(PointF point, float hitRadius, bool includeGroupRectangles = false) => _overlayManager.FindInteractiveOverlaysNearPoint(point, hitRadius, includeGroupRectangles);
		public void VisitInteractiveOverlaysNearPoint(PointF point, float hitRadius, bool includeGroupRectangles, Action<CanvasOverlayItem> visitor) => _overlayManager.VisitInteractiveOverlaysNearPoint(point, hitRadius, includeGroupRectangles, visitor);
		public CanvasOverlayItem FindBestInteractiveRectAtPoint(PointF point, float hitRadius, bool includeGroupRectangles, bool groupOnly) => _overlayManager.FindBestInteractiveRectAtPoint(point, hitRadius, includeGroupRectangles, groupOnly, ZoomScale, HandleSize);
		public List<CanvasOverlayItem> GetVisibleOverlaysInBounds(RectangleF bounds) => _overlayManager.GetVisibleOverlaysInBounds(bounds);
		public int VisitVisibleOverlaysInBounds(RectangleF bounds, int maxCandidates, Action<CanvasOverlayItem> visitor) => _overlayManager.VisitVisibleOverlaysInBounds(bounds, maxCandidates, visitor);
		public void UpdateInteractiveOverlayIndex(CanvasShape shape) => _overlayManager.UpdateInteractiveOverlayIndex(shape);
		public void RebuildInteractiveOverlayIndex() => _overlayManager.RebuildInteractiveOverlayIndex();
		public void SetViewMode(CanvasInteractionMode enumViewMode) => _viewMode = enumViewMode;
		public CanvasInteractionMode GetViewMode() => _viewMode;
		public void SetToFitRect(System.Drawing.RectangleF rect) => _fitRect = rect;

		public OpenGlTextDrawOptions GetOpenGlTextDrawOptions()
		{
			return new OpenGlTextDrawOptions(_fontBitmapEntries, _xSpan, _ySpan, _offsetSize, ZoomScale);
		}

		public System.Drawing.Size GetMaxTextureSize()
		{
			int[] maxTextureSize = new int[1];
			Action action = delegate
			{
				OpenGL gl = GetOpenGL();
				gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxTextureSize);
			};

			if (!CanUseOpenGlControl())
			{
				return new System.Drawing.Size(0, 0);
			}

			if (openGLControl.InvokeRequired)
			{
				openGLControl.Invoke(action);
			}
			else
			{
				action();
			}

			return new System.Drawing.Size(maxTextureSize[0], maxTextureSize[0]);
		}

		public void RefreshGL()
		{
			if (_suppressRefresh)
			{
				return;
			}

			if (!CanUseOpenGlControl())
			{
				return;
			}

			try
			{
				if (openGLControl.InvokeRequired)
				{
					openGLControl.BeginInvoke(new MethodInvoker(RefreshGL));
					return;
				}

				CancelDeferredRefreshGL();
				CancelDeferredViewportOverlayRefresh();
				RebuildVisibleOverlayCacheIfNeeded();
				RequestOpenGlRepaint();
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			catch (InvalidAsynchronousStateException)
			{
			}
		}

		public void RefreshTransientOverlayGL()
		{
			if (_suppressRefresh)
			{
				return;
			}

			if (!CanUseOpenGlControl())
			{
				return;
			}

			try
			{
				if (openGLControl.InvokeRequired)
				{
					openGLControl.BeginInvoke(new MethodInvoker(RefreshTransientOverlayGL));
					return;
				}

				// Brush cursor/FBO preview frames do not change ROI geometry. Repaint the
				// current scene without rebuilding the visible-overlay cache, otherwise a
				// long labeling session can turn one hover frame into a 500K ROI walk.
				RequestOpenGlRepaint();
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			catch (InvalidAsynchronousStateException)
			{
			}
		}

		public void RefreshGLAfterViewportInput()
		{
			if (_suppressRefresh || !CanUseOpenGlControl())
			{
				return;
			}

			try
			{
				if (openGLControl.InvokeRequired)
				{
					openGLControl.BeginInvoke(new MethodInvoker(RefreshGLAfterViewportInput));
					return;
				}

				// Wheel zoom/pan must repaint the texture transform immediately. The ROI
				// visible cache can be rebuilt on the next tick so 500K labels do not block input.
				CancelDeferredRefreshGL();
				RequestOpenGlRepaint();
				_deferredViewportOverlayRefreshPending = true;
				_deferredViewportOverlayRefreshTimer.Stop();
				_deferredViewportOverlayRefreshTimer.Start();
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			catch (InvalidAsynchronousStateException)
			{
			}
		}

		public void QueueRefreshGLAfterInput(int delayMilliseconds = 0)
		{
			if (_suppressRefresh || !CanUseOpenGlControl())
			{
				return;
			}

			try
			{
				if (openGLControl.InvokeRequired)
				{
					openGLControl.BeginInvoke(new MethodInvoker(() => QueueRefreshGLAfterInput(delayMilliseconds)));
					return;
				}

				_deferredRefreshPending = true;
				if (delayMilliseconds > 0)
				{
					// Mask overlay refreshes are not the visible source while the FBO
					// preview is active. Delay them so immediate wheel/pan can cancel
					// the repaint instead of waiting behind a committed-mask upload.
					_deferredRefreshDelayActive = true;
					_deferredRefreshTimer.Stop();
					_deferredRefreshTimer.Interval = Math.Max(1, delayMilliseconds);
					_deferredRefreshTimer.Start();
					return;
				}

				// ROI delete only needs to yield the current input message. Coalesce one
				// BeginInvoke repaint instead of waiting a full frame.
				if (_deferredRefreshPosted)
				{
					return;
				}

				_deferredRefreshPosted = true;
				openGLControl.BeginInvoke(new MethodInvoker(OnDeferredRefreshPosted));
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			catch (InvalidAsynchronousStateException)
			{
			}
		}

		private void OnDeferredRefreshPosted()
		{
			_deferredRefreshPosted = false;
			if (!_deferredRefreshPending || _deferredRefreshDelayActive)
			{
				return;
			}

			_deferredRefreshPending = false;
			RefreshGL();
		}

		private void OnDeferredRefreshTimerTick(object sender, EventArgs e)
		{
			_deferredRefreshTimer.Stop();
			_deferredRefreshDelayActive = false;
			if (!_deferredRefreshPending)
			{
				return;
			}

			_deferredRefreshPending = false;
			RefreshGL();
		}

		private void OnDeferredViewportOverlayRefreshTimerTick(object sender, EventArgs e)
		{
			_deferredViewportOverlayRefreshTimer.Stop();
			if (!_deferredViewportOverlayRefreshPending)
			{
				return;
			}

			_deferredViewportOverlayRefreshPending = false;
			RefreshGL();
		}

		private void CancelDeferredRefreshGL()
		{
			_deferredRefreshPending = false;
			_deferredRefreshPosted = false;
			_deferredRefreshDelayActive = false;
			_deferredRefreshTimer.Stop();
		}

		private void CancelDeferredViewportOverlayRefresh()
		{
			_deferredViewportOverlayRefreshPending = false;
			_deferredViewportOverlayRefreshTimer.Stop();
		}
		private bool CanUseOpenGlControl()
		{
			return openGLControl != null
				&& !openGLControl.IsDisposed
				&& openGLControl.IsHandleCreated;
		}

		private void RequestOpenGlRepaint()
		{
			if (!CanUseOpenGlControl())
			{
				return;
			}

			try
			{
				openGLControl.Invalidate();
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException)
			{
			}
			catch (InvalidAsynchronousStateException)
			{
			}
		}

		public void InvalidateVisibleOverlayCache()
		{
			_visibleOverlayCacheDirty = true;
			InvalidateFastOverlayScene();
		}

		public void InvalidateFastOverlayScene()
		{
			_fastOverlaySceneDirty = true;
		}

		private void RebuildVisibleOverlayCacheIfNeeded()
		{
			if (!_visibleOverlayCacheDirty)
			{
				return;
			}

			CalculatorVisibleOverlays();
			_visibleOverlayCacheDirty = false;
			InvalidateFastOverlayScene();
		}

		public void AddVisibleOverlayIfInViewport(CanvasOverlayItem overlayItem)
		{
			if (_visibleOverlayCacheDirty)
			{
				return;
			}

			EnsureVisibleOverlayCapacityForIncrementalAdd();
			AddVisibleOverlayShapesIfInViewport(overlayItem, _shapesViewPort, MaxVisibleOverlayShapes);
			UpdateVisibleOverlayLodState(_shapesViewPort.Count, _shapesViewPort.Count >= MaxVisibleOverlayShapes);
			InvalidateFastOverlayScene();
		}

		public void RemoveVisibleOverlay(CanvasOverlayItem overlayItem)
		{
			if (overlayItem?.Shape == null)
			{
				return;
			}

			CanvasShape shape = overlayItem.Shape;
			_shapesViewPort.RemoveAll(candidate =>
				ReferenceEquals(candidate, shape)
				|| (shape is CanvasRect<float> rect && ReferenceEquals(candidate, rect.ExtendedRectangle)));
			UpdateVisibleOverlayLodState(_shapesViewPort.Count, _shapesViewPort.Count >= MaxVisibleOverlayShapes);
			InvalidateFastOverlayScene();
		}

		private void EnsureVisibleOverlayCapacityForIncrementalAdd()
		{
			if (_shapesViewPort == null)
			{
				_shapesViewPort = new List<CanvasShape>();
				return;
			}

			while (_shapesViewPort.Count >= MaxVisibleOverlayShapes)
			{
				_shapesViewPort.RemoveAt(0);
			}
		}

		private void UpdateVisibleOverlayLodState(int shapeCount, bool isLodActive)
		{
			if (_visibleOverlayShapeCount == shapeCount
				&& _isVisibleOverlayLodActive == isLodActive
				&& _visibleOverlayShapeLimit == MaxVisibleOverlayShapes)
			{
				return;
			}

			_visibleOverlayShapeCount = shapeCount;
			_visibleOverlayShapeLimit = MaxVisibleOverlayShapes;
			_isVisibleOverlayLodActive = isLodActive;
			VisibleOverlayLodChanged(this, EventArgs.Empty);
		}

		public uint AddODBTexture(IntPtr monoData, IntPtr colorData, IntPtr maskData, IntPtr maskSetColorData, int x, int y, int width, int height, int imageWidth, int imageHeight, int offsetHeight,
			uint bpp1, uint bpp2, string imageName, System.Drawing.Size fullScreen, System.Drawing.Size titleSize, bool useMaskBackgroundWhite = true)
		{
			OpenGL gl = openGLControl.OpenGL;
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int textureY = (offsetHeight - y);

			uint colorBpp = 4;

			// OpenGL 텍스처를 생성합니다.
			uint oriTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, bpp1);
			uint oriBackgroundTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, bpp2);
			uint maskTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, colorBpp);
			uint backgroundMaskTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, colorBpp);

			// OpenGL 텍스처를 생성합니다.
			UpdateTexture(monoData, width, height, bpp1, oriTextureId);
			UpdateTexture(colorData, width, height, bpp2, oriBackgroundTextureId);
			UpdateTexture(maskData, width, height, colorBpp, maskTextureId);
			UpdateTexture(maskSetColorData, width, height, colorBpp, backgroundMaskTextureId);

			OpenGlTextureDrawingParam glParam = new OpenGlTextureDrawingParam
			{
				OriTextureId = oriTextureId,
				OriBackgroundTextureId = oriBackgroundTextureId,
				MaskTextureId = maskTextureId,
				MaskBackgroundMaskTextureId = backgroundMaskTextureId,
				BppOriginal = bpp1,
				BppBackground = bpp2,
				ImageName = imageName,
				GLDrawingTextureArea = new System.Drawing.RectangleF(x, textureY, width, height * -1),
				GLTextureArea = new RectangleF(x, textureY - height, width, height),
				ImageTexutreArea = new Rectangle(x, y, width, height),
				IsVisible = true,
				TextureFullScreen = fullScreen,
				TitleSize = titleSize,
			};

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			var textureArea = glParam.GLTextureArea;
			var fullSize = glParam.TextureFullScreen;

			System.Drawing.Size tileSize = glParam.TitleSize;

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int offsetX = (int)(textureArea.X / tileSize.Width) * tileSize.Width;
			int offsetY = 0;
			if (tileSize.Height > textureArea.Y + textureArea.Height)
			{
				offsetY = (int)0;
			}
			else
			{
				offsetY = (int)textureArea.Y;
			}

			glParam.ImageTitleOffset = new DotInfo(offsetX, offsetY);
			if (!_textureAreas.ContainsKey(imageName))
			{
				List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();
				list.Add(glParam);
				_textureAreas.TryAdd(imageName, list);
				//_textureKeysOrder.Add(imageName);
				_textureKeysOrder.Insert(0, imageName);
			}
			else
			{
				List<OpenGlTextureDrawingParam> glTextureDrawingParams = new List<OpenGlTextureDrawingParam>();
				_textureAreas.TryGetValue(imageName, out glTextureDrawingParams);
				glTextureDrawingParams.Add(glParam);
				_textureAreas[imageName] = glTextureDrawingParams;
			}

			// 좌하단 기준으로 y 좌표를 계산합니다.
			System.Drawing.RectangleF imageArea = CalculateBoundingRectangle(_textureAreas);
			SetToFitRect(imageArea);
			return oriTextureId;
		}

		public uint AddTexture(IntPtr data, int x, int y, int width, int height, int imageWidth, int imageHeight, int offsetHeight,
			uint bpp, string imageName, System.Drawing.Size fullScreen, System.Drawing.Size titleSize)
		{
			OpenGL gl = openGLControl.OpenGL;
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int textureY = (offsetHeight - y);

			// OpenGL 텍스처를 생성합니다.
			uint oriTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, bpp);

			// OpenGL 텍스처를 생성합니다.
			UpdateTexture(data, width, height, bpp, oriTextureId);

			OpenGlTextureDrawingParam glParam = new OpenGlTextureDrawingParam
			{
				OriTextureId = oriTextureId,
				BppOriginal = bpp,
				ImageName = imageName,
				GLDrawingTextureArea = new System.Drawing.RectangleF(x, textureY, width, height * -1),
				GLTextureArea = new RectangleF(x, textureY - height, width, height),
				ImageTexutreArea = new Rectangle(x, y, width, height),
				IsVisible = true,
				TextureFullScreen = fullScreen,
				TitleSize = titleSize,
			};

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			var textureArea = glParam.GLTextureArea;
			var fullSize = glParam.TextureFullScreen;

			System.Drawing.Size tileSize = glParam.TitleSize;

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int offsetX = (int)(textureArea.X / tileSize.Width) * tileSize.Width;
			int offsetY = 0;
			if (tileSize.Height > textureArea.Y + textureArea.Height)
			{
				offsetY = (int)0;
			}
			else
			{
				offsetY = (int)textureArea.Y;
			}

			glParam.ImageTitleOffset = new DotInfo(offsetX, offsetY);
			if (!_textureAreas.ContainsKey(imageName))
			{
				List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();
				list.Add(glParam);
				_textureAreas.TryAdd(imageName, list);
				//_textureKeysOrder.Add(imageName);
				_textureKeysOrder.Insert(0, imageName);
			}
			else
			{
				List<OpenGlTextureDrawingParam> glTextureDrawingParams = new List<OpenGlTextureDrawingParam>();
				_textureAreas.TryGetValue(imageName, out glTextureDrawingParams);
				glTextureDrawingParams.Add(glParam);
				_textureAreas[imageName] = glTextureDrawingParams;
			}

			// 좌하단 기준으로 y 좌표를 계산합니다.
			System.Drawing.RectangleF imageArea = CalculateBoundingRectangle(_textureAreas);
			SetToFitRect(imageArea);
			return oriTextureId;
		}

		public bool TryReplaceSingleTexture(IntPtr data, int x, int y, int width, int height, int imageWidth, int imageHeight, int offsetHeight,
			uint bpp, string imageName, System.Drawing.Size fullScreen, System.Drawing.Size titleSize)
		{
			if (!CanUseOpenGlControl())
			{
				return false;
			}

			if (openGLControl.InvokeRequired)
			{
				bool updated = false;
				openGLControl.Invoke(new Action(() =>
				{
					updated = TryReplaceSingleTexture(data, x, y, width, height, imageWidth, imageHeight, offsetHeight, bpp, imageName, fullScreen, titleSize);
				}));
				return updated;
			}

			if (_textureAreas.Count != 1 || _textureKeysOrder.Count != 1)
			{
				return false;
			}

			string currentKey = _textureKeysOrder[0];
			if (!_textureAreas.TryGetValue(currentKey, out List<OpenGlTextureDrawingParam> currentTextures)
				|| currentTextures == null
				|| currentTextures.Count != 1)
			{
				return false;
			}

			OpenGlTextureDrawingParam param = currentTextures[0];
			if (param.BppOriginal != bpp
				|| param.ImageTexutreArea.Width != imageWidth
				|| param.ImageTexutreArea.Height != imageHeight)
			{
				return false;
			}

			UpdateTexture(data, width, height, bpp, param.OriTextureId);

			int textureY = offsetHeight - y;
			param.ImageName = imageName;
			param.GLDrawingTextureArea = new System.Drawing.RectangleF(x, textureY, width, height * -1);
			param.GLTextureArea = new RectangleF(x, textureY - height, width, height);
			param.ImageTexutreArea = new Rectangle(x, y, width, height);
			param.TextureFullScreen = fullScreen;
			param.TitleSize = titleSize;
			param.IsVisible = true;

			int offsetX = (int)(param.GLTextureArea.X / titleSize.Width) * titleSize.Width;
			int offsetY = titleSize.Height > param.GLTextureArea.Y + param.GLTextureArea.Height
				? 0
				: (int)param.GLTextureArea.Y;
			param.ImageTitleOffset = new DotInfo(offsetX, offsetY);

			_textureAreas.TryRemove(currentKey, out _);
			_textureAreas[imageName] = currentTextures;
			_textureKeysOrder.Remove(currentKey);
			_textureKeysOrder.Remove(imageName);
			_textureKeysOrder.Insert(0, imageName);

			System.Drawing.RectangleF imageArea = CalculateBoundingRectangle(_textureAreas);
			SetToFitRect(imageArea);
			return true;
		}

		public uint AddTexture(IntPtr monoData, IntPtr colorData, int x, int y, int width, int height, int imageWidth, int imageHeight, int offsetHeight,
			uint bpp1, uint bpp2, string imageName, System.Drawing.Size fullScreen, System.Drawing.Size titleSize, bool useMaskBackgroundWhite = true)
		{
			OpenGL gl = openGLControl.OpenGL;
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int textureY = (offsetHeight - y);

			// OpenGL 텍스처를 생성합니다.
			uint oriTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, bpp1);
			uint oriBackgroundTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, bpp2);
			uint maskTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, 3);
			uint backgroundMaskTextureId = GenerateOpenGLTexture(imageWidth, imageHeight, 3);

			// 원본 크기와 같은 Mat을 생성합니다.
			OpenCvSharp.Mat combinedMat = new OpenCvSharp.Mat(height, width, OpenCvSharp.MatType.CV_8UC1);

			// OpenGL 텍스처를 생성합니다.
			UpdateTexture(monoData, width, height, bpp1, oriTextureId);
			UpdateTexture(colorData, width, height, bpp2, oriBackgroundTextureId);
			UpdateTexture(combinedMat.Data, width, height, 1, maskTextureId);
			UpdateTexture(combinedMat.Data, width, height, 1, backgroundMaskTextureId);

			OpenGlTextureDrawingParam glParam = new OpenGlTextureDrawingParam
			{
				OriTextureId = oriTextureId,
				OriBackgroundTextureId = oriBackgroundTextureId,
				MaskTextureId = maskTextureId,
				MaskBackgroundMaskTextureId = backgroundMaskTextureId,
				BppOriginal = bpp1,
				BppBackground = bpp2,
				ImageName = imageName,
				GLDrawingTextureArea = new System.Drawing.RectangleF(x, textureY, width, height * -1),
				GLTextureArea = new RectangleF(x, textureY - height, width, height),
				ImageTexutreArea = new Rectangle(x, y, width, height),
				IsVisible = true,
				TextureFullScreen = fullScreen,
				TitleSize = titleSize,
			};

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			var textureArea = glParam.GLTextureArea;
			var fullSize = glParam.TextureFullScreen;

			System.Drawing.Size tileSize = glParam.TitleSize;

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int offsetX = (int)(textureArea.X / tileSize.Width) * tileSize.Width;
			int offsetY = 0;
			if (tileSize.Height > textureArea.Y + textureArea.Height)
			{
				offsetY = (int)0;
			}
			else
			{
				offsetY = (int)textureArea.Y;
			}

			glParam.ImageTitleOffset = new DotInfo(offsetX, offsetY);
			if (!_textureAreas.ContainsKey(imageName))
			{
				List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();
				list.Add(glParam);
				_textureAreas.TryAdd(imageName, list);
				//_textureKeysOrder.Add(imageName);
				_textureKeysOrder.Insert(0, imageName);
			}
			else
			{
				List<OpenGlTextureDrawingParam> glTextureDrawingParams = new List<OpenGlTextureDrawingParam>();
				_textureAreas.TryGetValue(imageName, out glTextureDrawingParams);
				glTextureDrawingParams.Add(glParam);
				_textureAreas[imageName] = glTextureDrawingParams;
			}

			// 좌하단 기준으로 y 좌표를 계산합니다.
			System.Drawing.RectangleF imageArea = CalculateBoundingRectangle(_textureAreas);
			SetToFitRect(imageArea);
			return oriTextureId;
		}

		public void DeleteTexture(string imageName)
		{
			if (!_textureAreas.ContainsKey(imageName))
			{
				return;
			}

			Action action = delegate
			{
				List<OpenGlTextureDrawingParam> glTextureDrawingParams = _textureAreas[imageName];
				if (CanUseOpenGlControl())
				{
					ImageCanvasTextureStore.DeleteTextures(openGLControl.OpenGL, ImageCanvasTextureStore.CollectTextureIds(glTextureDrawingParams));
				}

				_textureAreas.TryRemove(imageName, out _);
				_textureKeysOrder.Remove(imageName);
			};

			if (CanUseOpenGlControl() && openGLControl.InvokeRequired)
			{
				openGLControl.Invoke(action);
			}
			else
			{
				action();
			}

			RefreshGL();
		}

		public void DeleteTexture(List<uint> textureIds)
		{
			if (textureIds == null || textureIds.Count == 0 || !CanUseOpenGlControl())
			{
				return;
			}

			Action action = delegate
			{
				ImageCanvasTextureStore.DeleteTextures(openGLControl.OpenGL, textureIds);
			};

			if (openGLControl.InvokeRequired)
			{
				openGLControl.Invoke(action);
			}
			else
			{
				action();
			}
		}

		public uint? GetTextureIdAtPoint(int mouseX, int mouseY)
		{
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;
					if (textureArea.Contains(mouseX, mouseY))
					{
						return param.OriTextureId;
					}
				}
			}
			return null; // 해당 위치에 맞는 텍스처가 없으면 null을 반환합니다.
		}

		/// <summary>
		/// 텍스처 관련 값입니다.
		/// </summary>
		/// <param name="mouseX"></param>
		/// <param name="mouseY"></param>
		/// <returns></returns>
		public OpenGlTextureDrawingParam GetTextureParameterAtPoint(string textureName, int mouseX, int mouseY)
		{
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;
					if (textureArea.Contains(mouseX, mouseY) && kvp.Key == textureName)
					{
						return param;
					}
				}
			}
			return null; // 해당 위치에 맞는 텍스처가 없으면 null을 반환합니다.
		}

		public OpenGlTextureDrawingParam GetTextureParameterAtPoint(uint textureId)
		{
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;
					//System.Drawing.RectangleF rect = new RectangleF(textureArea.X, textureArea.Y, textureArea.Width, textureArea.Height * -1);
					if (param.OriBackgroundTextureId == textureId)
					{
						return param;
					}
				}
			}
			return null; // 해당 위치에 맞는 텍스처가 없으면 null을 반환합니다.
		}

		/// <summary>
		/// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
		/// </summary>
		/// <param name="mouseX"></param>
		/// <param name="mouseY"></param>
		/// <returns></returns>
		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtPoint(string textureName, int mouseX, int mouseY)
		{
			List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;
					if (textureArea.Contains(mouseX, mouseY) && textureName == kvp.Key)
					{
						list.Add(param);
					}
				}
			}
			return list; // 조건에 맞는 텍스처가 없으면 현재 목록을 반환합니다.
		}

		/// <summary>
		/// 텍스처 관련 값입니다.
		/// OpenGL 텍스처를 생성합니다.
		/// </summary>
		/// <param name="textureName"></param>
		/// <param name="mouseX">원의 중심 X 좌표</param>
		/// <param name="mouseY">원의 중심 Y 좌표</param>
		/// <param name="radius">원의 반경</param>
		/// <returns></returns>
		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtPoint(string textureName, int mouseX, int mouseY, float radius)
		{
			List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.

					var rect = GetRectangleFromCenterAndRadius(new PointF(mouseX, mouseY), radius);

					var textureArea = param.GLTextureArea;
					if (textureArea.IntersectsWith(rect) && textureName == kvp.Key)
					{
						list.Add(param);
					}
				}
			}

			return list;
		}

		public RectangleF GetRectangleFromCenterAndRadius(PointF center, float radius)
		{
			float left = center.X - radius;
			float top = center.Y - radius;
			float width = radius * 2;
			float height = radius * 2;

			return new RectangleF(left, top, width, height);
		}


		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtRect(string textureName, System.Drawing.RectangleF rectangle)
		{
			System.Drawing.Rectangle newRect = new Rectangle((int)rectangle.X, (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height);
			return GetTextureParameterListAtRect(textureName, newRect);
		}

		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtRect(System.Drawing.RectangleF rectangle)
		{
			System.Drawing.Rectangle newRect = new Rectangle((int)rectangle.X, (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height);
			return GetTextureParameterListAtRect(newRect);
		}

		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtPoints(string textureName, List<DotInfo> points)
		{
			List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;

					foreach (var point in points)
					{
						// OpenGL 텍스처를 생성합니다.
						if (textureArea.Contains(point.X, point.Y) && kvp.Key == textureName)
						{
							list.Add(param);
							break; // OpenGL 텍스처를 생성합니다.
						}
					}
				}
			}

			return list; // 조건에 맞는 텍스처가 없으면 현재 목록을 반환합니다.
		}

		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtPoints(List<DotInfo> points)
		{
			List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;

					foreach (var point in points)
					{
						// OpenGL 텍스처를 생성합니다.
						if (textureArea.Contains(point.X, point.Y))
						{
							list.Add(param);
							break; // OpenGL 텍스처를 생성합니다.
						}
					}
				}
			}

			return list; // 조건에 맞는 텍스처가 없으면 현재 목록을 반환합니다.
		}

		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtRect(string textureName, System.Drawing.Rectangle rectangle)
		{
			List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;
					if (textureArea.IntersectsWith(rectangle) && textureName == kvp.Key)
					{
						list.Add(param);
					}
				}
			}
			return list; // 조건에 맞는 텍스처가 없으면 현재 목록을 반환합니다.
		}

		public List<OpenGlTextureDrawingParam> GetTextureParameterListAtRect(System.Drawing.Rectangle rectangle)
		{
			List<OpenGlTextureDrawingParam> list = new List<OpenGlTextureDrawingParam>();
			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			foreach (var kvp in _textureAreas)
			{
				foreach (var param in kvp.Value)
				{
					// 텍스처 관련 값입니다.
					var textureArea = param.GLTextureArea;
					if (textureArea.IntersectsWith(rectangle))
					{
						list.Add(param);
					}
				}
			}
			return list; // 조건에 맞는 텍스처가 없으면 현재 목록을 반환합니다.
		}

		public List<OpenGlTextureDrawingParam> GetTextureParameters(string imageName)
		{
			List<OpenGlTextureDrawingParam> glTextureDrawingParams = new List<OpenGlTextureDrawingParam>();

			_textureAreas.TryGetValue(imageName, out glTextureDrawingParams);

			return glTextureDrawingParams;
		}

		public void SetVisibleTexture(string imageName, bool isVisible)
		{
			if (!_textureAreas.ContainsKey(imageName)) { return; }
			_textureAreas[imageName].ForEach(texture => texture.IsVisible = isVisible);
			System.Drawing.RectangleF imageArea = CalculateBoundingRectangle(_textureAreas);
			SetToFitRect(imageArea);
		}

		public void SetTransparencyToTexture(string imageName, float transparency = 1.0f)
		{
			// 텍스처 관련 값입니다.
			var matchingParams = _textureAreas
				.SelectMany(kv => kv.Value)
				.Where(param => param.ImageName == imageName);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			foreach (var param in matchingParams)
			{
				param.IsTransParency = true;
				param.TransParency = transparency;
			}
			RefreshGL();
		}

		public void SetRotateToTexture(string imageName, float rotate = 1.0f)
		{
			// 텍스처 관련 값입니다.
			var matchingParams = _textureAreas
				.SelectMany(kv => kv.Value)
				.Where(param => param.ImageName == imageName);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			foreach (var param in matchingParams)
			{
				param.IsRotated = true;
				param.RotationAngle = rotate;
			}
			RefreshGL();
		}

		public float GetRotateToTexture(string imageName)
		{
			return _textureAreas
				.SelectMany(kv => kv.Value)
				.Where(param => param.ImageName == imageName).FirstOrDefault().RotationAngle;
		}

		[Obsolete("Use GetRotateToTexture instead.")]
		public float GetRoateToTexture(string imageName)
		{
			return GetRotateToTexture(imageName);
		}

		public float GetTransparencyToTexture(string imageName)
		{
			return _textureAreas
			.SelectMany(kv => kv.Value)
			.Where(param => param.ImageName == imageName).FirstOrDefault().TransParency;
		}

		public System.Drawing.RectangleF CalculateBoundingRectangle(ConcurrentDictionary<string, List<OpenGlTextureDrawingParam>> textureAreas)
		{
			// 최소/최대 좌표를 갱신해 전체 이미지 영역을 계산합니다.
			float minX = float.MaxValue;
			float minY = float.MaxValue;
			float maxX = float.MinValue;
			float maxY = float.MinValue;

			foreach (var item in textureAreas.Values)
			{
				foreach (var param in item)
				{
					if (!param.IsVisible) { continue; }
					float newHeight = param.GLDrawingTextureArea.Height;
					if (param.GLDrawingTextureArea.Height < 0)
					{
						newHeight = newHeight * -1;
					}
					System.Drawing.RectangleF newItem = new System.Drawing.RectangleF(param.GLDrawingTextureArea.X, param.GLDrawingTextureArea.Y - newHeight, param.GLDrawingTextureArea.Width, newHeight);
					// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
					minX = Math.Min(minX, newItem.Left);
					minY = Math.Min(minY, newItem.Top);

					// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
					maxX = Math.Max(maxX, newItem.Right);
					maxY = Math.Max(maxY, newItem.Bottom);
				}
			}

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			return new System.Drawing.RectangleF(minX, minY, maxX - minX, maxY - minY);
		}

		public List<EraserParameter> GetEraserPoint()
		{
			return _eraserParameters;
		}

		#endregion

		#region // private methods
		private void UpdatePixelStatus(MouseEventArgs e, bool samplePixel, float scale = 1f)
		{
			lock (_pixelDatalock)
			{
				int x = (int)(e.X / scale);
				int y = (int)(e.Y / scale);
				if (samplePixel && TryReadScreenPixel(openGLControl.OpenGL, x, y, out System.Drawing.Color screenColor, out int grayValue))
				{
					GrayValue = grayValue;
					PixelColor = screenColor;
				}

				PixelPos = GetRoundPointF(GetCurrentRobotPos(x, y));
				ImagePixelPos = GetRoundPointF(ConvertOpenGlToImagePoint(PixelPos));

				//Console.WriteLine($"UpdatePixelStatus Ori: {e.X},{e.Y}");
				//Console.WriteLine($"UpdatePixelStatus Curr: {PixelPos.X},{PixelPos.Y}");
			}

		}

		private bool ShouldReadPixelStatus(MouseEventArgs e)
		{
			if (!IsPointInsideOpenGlControl(e.X, e.Y))
			{
				return false;
			}

			long now = Stopwatch.GetTimestamp();
			if (_lastPixelReadbackTicks != 0 && now - _lastPixelReadbackTicks < PixelReadbackIntervalTicks)
			{
				return false;
			}

			if (_lastPixelReadbackPoint.X == e.X
				&& _lastPixelReadbackPoint.Y == e.Y
				&& _lastPixelReadbackTicks != 0)
			{
				return false;
			}

			_lastPixelReadbackTicks = now;
			_lastPixelReadbackPoint = new System.Drawing.Point(e.X, e.Y);
			return true;
		}

		private bool ShouldRefreshDragOverlays()
		{
			long now = Stopwatch.GetTimestamp();
			if (_lastDragOverlayRefreshTicks != 0 && now - _lastDragOverlayRefreshTicks < DragOverlayRefreshIntervalTicks)
			{
				return false;
			}

			_lastDragOverlayRefreshTicks = now;
			return true;
		}

		private bool ShouldRecalculateVisibleOverlaysOnMouseMove(bool isDraggingView)
		{
			// Pan should keep newly visible ROI roughly in sync, but rebuilding the
			// visible cache for every raw MouseMove brings back the original lag.
			return isDraggingView && ShouldRefreshDragOverlays();
		}

		private bool ShouldRequestOpenGlRepaintOnMouseMove(bool isDraggingView, bool isPointerDown)
		{
			if (isDraggingView)
			{
				return ShouldRefreshMouseMoveRepaint();
			}

			if (!isPointerDown)
			{
				// Hover only updates cursor/status state. Repainting the OpenGL scene here
				// makes large labeling sessions feel slow even when no ROI geometry changed.
				return false;
			}

			if (_viewMode == CanvasInteractionMode.Drawing)
			{
				// Drawing preview is live feedback only; keep it frame-limited and do not
				// rebuild committed ROI display lists while the pointer is moving.
				return isPointerDown && ShouldRefreshMouseMoveRepaint();
			}

			return _viewMode != CanvasInteractionMode.None && ShouldRefreshMouseMoveRepaint();
		}

		private bool ShouldRefreshMouseMoveRepaint()
		{
			long now = Stopwatch.GetTimestamp();
			if (_lastMouseMoveRepaintTicks != 0 && now - _lastMouseMoveRepaintTicks < MouseMoveRepaintIntervalTicks)
			{
				return false;
			}

			_lastMouseMoveRepaintTicks = now;
			return true;
		}

		private static bool IsOverlayGeometryInteractionMode(CanvasInteractionMode viewMode)
		{
			return viewMode == CanvasInteractionMode.Edit
				|| viewMode == CanvasInteractionMode.Move
				|| viewMode == CanvasInteractionMode.Drag;
		}

		private static bool IsOverlayMouseManipulationMode(CanvasInteractionMode viewMode)
		{
			return viewMode == CanvasInteractionMode.Edit
				|| viewMode == CanvasInteractionMode.Move;
		}

		private bool IsPointInsideOpenGlControl(int x, int y)
		{
			return openGLControl != null
				&& x >= 0
				&& y >= 0
				&& x < openGLControl.Width
				&& y < openGLControl.Height;
		}

		private bool TryReadScreenPixel(OpenGL gl, int x, int y, out System.Drawing.Color color, out int grayValue)
		{
			color = System.Drawing.Color.Empty;
			grayValue = 0;
			if (gl == null || !IsPointInsideOpenGlControl(x, y))
			{
				return false;
			}

			// Status hover is throttled, but it still runs throughout long labeling
			// sessions. Reuse one 1px buffer so MouseMove does not allocate per readback.
			byte[] pixelData = _pixelReadbackBuffer;
			int invertedY = Math.Max(0, Math.Min(gl.RenderContextProvider.Height - 1, gl.RenderContextProvider.Height - y - 1));
			gl.ReadPixels(x, invertedY, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, pixelData);
			color = System.Drawing.Color.FromArgb(pixelData[3], pixelData[0], pixelData[1], pixelData[2]);
			grayValue = (pixelData[0] + pixelData[1] + pixelData[2]) / 3;
			return true;
		}

		private int GetGrayValue(OpenGL gl, int x, int y)
		{
			byte[] pixelData = new byte[4]; // RGBA

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int invertedY = gl.RenderContextProvider.Height - y;

			gl.ReadPixels(x, invertedY, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, pixelData);

			// RGB 값을 0에서 255 사이의 값으로 변환합니다.
			// 이미지 영역을 좌하단 기준으로 설정합니다.
			///int grayValue = (int)(0.299 * pixelData[0] + 0.587 * pixelData[1] + 0.114 * pixelData[2]);

			int grayValue = (int)(pixelData[0] + pixelData[1] + pixelData[2]) / 3;

			return grayValue;
		}

		public System.Drawing.Color GetScreenColor(OpenGL gl, int x, int y)
		{
			byte[] pixelData = new byte[4]; // RGBA

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			int invertedY = gl.RenderContextProvider.Height - y;

			gl.ReadPixels(x, invertedY, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, pixelData);

			return System.Drawing.Color.FromArgb(pixelData[3], pixelData[0], pixelData[1], pixelData[2]);
		}


		public byte[] ReadTextureRegionColors(OpenGL gl, uint textureId, int x, int y, int readWidth, int readHeight, int textureWidth, int textureHeight)
		{
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

			int[] widthArr = new int[1];
			int[] heightArr = new int[1];
			int[] formatArr = new int[1];
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

			int internalFormat = formatArr[0];

			// PBO를 생성하고 바인딩합니다.
			uint[] pbo = new uint[1];
			gl.GenBuffers(1, pbo);
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			int bufferSize = readWidth * readHeight * 4; // RGBA for each pixel
			gl.BufferData(OpenGL.GL_PIXEL_PACK_BUFFER, bufferSize, IntPtr.Zero, OpenGL.GL_STREAM_READ);

			// RGB 값을 0에서 255 사이의 값으로 변환합니다.
			// 일반적인 가중치 방식으로 그레이스케일 값을 계산합니다.
			gl.ReadPixels(x, (textureHeight - 1) - (y + readHeight - 1), readWidth, readHeight, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);

			// GPU에서 픽셀 데이터를 읽어옵니다.
			byte[] pixelData = new byte[bufferSize];
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			IntPtr ptr = gl.MapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, OpenGL.GL_READ_ONLY);
			if (ptr != IntPtr.Zero)
			{
				Marshal.Copy(ptr, pixelData, 0, bufferSize);
				gl.UnmapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER);
			}
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, 0);
			gl.DeleteBuffers(1, pbo);

			byte[] flippedPixels = new byte[pixelData.Length];
			int bytesPerPixel = 4; // RGBA 형식 기준입니다.
			int stride = readWidth * bytesPerPixel;

			for (int row = 0; row < readHeight; row++)
			{
				int sourceIndex = row * stride;
				int destIndex = (readHeight - 1 - row) * stride;
				Array.Copy(pixelData, sourceIndex, flippedPixels, destIndex, stride);
			}
			return flippedPixels;
		}
		public byte[] ReadTextureRegionColors(OpenGL gl, uint textureId, PointF centerPoint, int halfSize, int width, int height)
		{
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

			int minX = (int)centerPoint.X - halfSize;
			int minY = (int)centerPoint.Y - halfSize;

			int maxX = (int)Math.Min(centerPoint.X + halfSize, width);
			int maxY = (int)Math.Min(centerPoint.Y + halfSize, height);

			int readWidth = maxX - minX + 1;
			int readHeight = maxY - minY + 1;


			// PBO를 생성하고 바인딩합니다.
			uint[] pbo = new uint[1];
			gl.GenBuffers(1, pbo);
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			int bufferSize = readWidth * readHeight * 4; // RGBA for each pixel
			gl.BufferData(OpenGL.GL_PIXEL_PACK_BUFFER, bufferSize, IntPtr.Zero, OpenGL.GL_STREAM_READ);

			// glReadPixels 호출
			gl.ReadPixels(minX, (height - 1) - maxY, readWidth, readHeight, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);

			// GPU에서 픽셀 데이터를 읽어옵니다.
			byte[] pixelData = new byte[bufferSize];
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			IntPtr ptr = gl.MapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, OpenGL.GL_READ_ONLY);
			if (ptr != IntPtr.Zero)
			{
				Marshal.Copy(ptr, pixelData, 0, bufferSize);
				gl.UnmapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER);
			}
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, 0);
			gl.DeleteBuffers(1, pbo);

			byte[] flippedPixels = new byte[pixelData.Length];
			int bytesPerPixel = 4; // RGBA 형식 기준입니다.
			int stride = readWidth * bytesPerPixel;

			for (int y = 0; y < readHeight; y++)
			{
				int sourceIndex = y * stride;
				int destIndex = (readHeight - 1 - y) * stride;
				Array.Copy(pixelData, sourceIndex, flippedPixels, destIndex, stride);
			}
			return flippedPixels;
		}

		public System.Drawing.Color ReadTextureColor(OpenGL gl, uint textureId, int x, int y)
		{
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

			int[] widthArr = new int[1];
			int[] heightArr = new int[1];
			int[] formatArr = new int[1];
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

			int width = widthArr[0];
			int height = heightArr[0];
			int internalFormat = formatArr[0];

			uint[] pbo = new uint[1];
			gl.GenBuffers(1, pbo);
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			gl.BufferData(OpenGL.GL_PIXEL_PACK_BUFFER, 4, IntPtr.Zero, OpenGL.GL_STREAM_READ);
			int invertedY = height - 1 - y;

			// Use appropriate format based on the internal format
			if (internalFormat == OpenGL.GL_LUMINANCE)
			{
				gl.ReadPixels(x, invertedY, 1, 1, OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
			}
			else
			{
				gl.ReadPixels(x, invertedY, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
			}

			byte[] pixelData = new byte[4];
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			IntPtr ptr = gl.MapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, OpenGL.GL_READ_ONLY);
			if (ptr != IntPtr.Zero)
			{
				Marshal.Copy(ptr, pixelData, 0, 4);
				gl.UnmapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER);
			}
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, 0);
			gl.DeleteBuffers(1, pbo);

			// Interpret the pixel data based on the internal format
			if (internalFormat == OpenGL.GL_LUMINANCE)
			{
				// For RED format, we will return a grayscale color
				return System.Drawing.Color.FromArgb(pixelData[0], pixelData[0], pixelData[0]);
			}
			else
			{
				return System.Drawing.Color.FromArgb(pixelData[3], pixelData[0], pixelData[1], pixelData[2]);
			}
		}


		//public System.Drawing.Color ReadTextureColor(OpenGL gl, uint textureId, int x, int y)
		//{
		//	gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

		//	int[] widthArr = new int[1];
		//	int[] heightArr = new int[1];
		//	int[] formatArr = new int[1];
		//	gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
		//	gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
		//	gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

		//	int width = widthArr[0];
		//	int height = heightArr[0];

		//	//uint[] frameBuffer = new uint[1];
		//	//gl.GenFramebuffersEXT(1, frameBuffer);
		//	//gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBuffer[0]);
		//	//gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

		// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
		//	//uint[] renderBuffer = new uint[1];
		//	//gl.GenRenderbuffersEXT(1, renderBuffer);
		//	//gl.BindRenderbufferEXT(OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);
		//	//gl.RenderbufferStorageEXT(OpenGL.GL_RENDERBUFFER_EXT, OpenGL.GL_STENCIL_INDEX8_EXT, width, height);

		// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
		//	//gl.FramebufferRenderbufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_STENCIL_ATTACHMENT_EXT, OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);

		//	Stopwatch stopwatch = Stopwatch.StartNew();


		// PBO를 생성하고 바인딩합니다.
		//	uint[] pbo = new uint[1];
		//	gl.GenBuffers(1, pbo);
		//	gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
		//	gl.BufferData(OpenGL.GL_PIXEL_PACK_BUFFER, 4, IntPtr.Zero, OpenGL.GL_STREAM_READ);
		//	int invertedY = height - 1 - y;
		// glReadPixels 호출
		//	gl.ReadPixels(x, invertedY, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);

		// GPU에서 픽셀 데이터를 읽어옵니다.
		//	byte[] pixelData = new byte[4];
		//	gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
		//	IntPtr ptr = gl.MapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, OpenGL.GL_READ_ONLY);
		//	if (ptr != IntPtr.Zero)
		//	{
		//		Marshal.Copy(ptr, pixelData, 0, 4);
		//		gl.UnmapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER);
		//	}
		//	gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, 0);
		//	gl.DeleteBuffers(1, pbo);
		//	//gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

		//	//Console.WriteLine($"{stopwatch.ElapsedMilliseconds}");

		// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
		//	//gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
		//	//gl.DeleteFramebuffersEXT(1, frameBuffer);

		//	return System.Drawing.Color.FromArgb(pixelData[3], pixelData[0], pixelData[1], pixelData[2]);
		//}

		public System.Drawing.Color[] ReadTextureColors(uint textureId)
		{
			OpenGL gl = GetOpenGL();
			Stopwatch stopwatch = Stopwatch.StartNew();

			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);
			int[] widthArr = new int[1];
			int[] heightArr = new int[1];
			int[] formatArr = new int[1];
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

			int width = widthArr[0];
			int height = heightArr[0];

			OpenGlRenderer.InitializeOpenGLSettings(gl, width, height);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			uint[] ids = new uint[1];
			gl.GenFramebuffersEXT(1, ids);
			uint frameBufferID = ids[0];
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
			gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

			// PBO를 생성하고 바인딩합니다.
			uint[] pbo = new uint[1];
			gl.GenBuffers(1, pbo);
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			int bufferSize = width * height * 4; // RGBA for each pixel
			gl.BufferData(OpenGL.GL_PIXEL_PACK_BUFFER, bufferSize, IntPtr.Zero, OpenGL.GL_STREAM_READ);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			uint[] renderBuffer = new uint[1];
			gl.GenRenderbuffersEXT(1, renderBuffer);
			gl.BindRenderbufferEXT(OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);
			gl.RenderbufferStorageEXT(OpenGL.GL_RENDERBUFFER_EXT, OpenGL.GL_STENCIL_INDEX8_EXT, width, height);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.FramebufferRenderbufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_STENCIL_ATTACHMENT_EXT, OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);

			// OpenGL 텍스처를 생성합니다.
			gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

			// OpenGL 텍스처를 생성합니다.
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);

			// glReadPixels 호출
			gl.ReadPixels(0, 0, width, height, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);

			// GPU에서 픽셀 데이터를 읽어옵니다.
			byte[] pixelData = new byte[bufferSize];
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			IntPtr ptr = gl.MapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, OpenGL.GL_READ_ONLY);
			if (ptr != IntPtr.Zero)
			{
				Marshal.Copy(ptr, pixelData, 0, bufferSize);
				gl.UnmapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER);
			}
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, 0);
			gl.DeleteBuffers(1, pbo);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
			gl.DeleteFramebuffersEXT(1, ids);
			gl.DeleteRenderbuffersEXT(1, renderBuffer);
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

			ReshapeNonRefresh();

			int bytesPerPixel = 4; // RGBA 형식 기준입니다.
			int stride = width * bytesPerPixel;
			byte[] flippedPixels = new byte[pixelData.Length];

			for (int y = 0; y < height; y++)
			{
				int sourceIndex = y * stride;
				int destIndex = (height - 1 - y) * stride;
				Array.Copy(pixelData, sourceIndex, flippedPixels, destIndex, stride);
			}

			// 생성한 텍스처 데이터를 업데이트합니다.
			Array.Copy(flippedPixels, pixelData, pixelData.Length);

			System.Drawing.Color[] colors = new System.Drawing.Color[width * height];

			for (int y = 0; y < height; y++)
			{
				int baseIndex = y * width * 4;
				for (int x = 0; x < width; x++)
				{
					int i = baseIndex + (x * 4);
					colors[x + (y * width)] = System.Drawing.Color.FromArgb(flippedPixels[i + 3], flippedPixels[i], flippedPixels[i + 1], flippedPixels[i + 2]);
				}
			}

			//Console.WriteLine($"{stopwatch.ElapsedMilliseconds}ms");

			return colors;
		}

		public byte[] ReadTextureBuffers(uint textureId)
		{
			OpenGL gl = GetOpenGL();
			Stopwatch stopwatch = Stopwatch.StartNew();

			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);
			int[] widthArr = new int[1];
			int[] heightArr = new int[1];
			int[] formatArr = new int[1];
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
			gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

			int width = widthArr[0];
			int height = heightArr[0];

			OpenGlRenderer.InitializeOpenGLSettings(gl, width, height);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			uint[] ids = new uint[1];
			gl.GenFramebuffersEXT(1, ids);
			uint frameBufferID = ids[0];
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
			gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

			// PBO를 생성하고 바인딩합니다.
			uint[] pbo = new uint[1];
			gl.GenBuffers(1, pbo);
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			int bufferSize = width * height * 4; // RGBA for each pixel
			gl.BufferData(OpenGL.GL_PIXEL_PACK_BUFFER, bufferSize, IntPtr.Zero, OpenGL.GL_STREAM_READ);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			uint[] renderBuffer = new uint[1];
			gl.GenRenderbuffersEXT(1, renderBuffer);
			gl.BindRenderbufferEXT(OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);
			gl.RenderbufferStorageEXT(OpenGL.GL_RENDERBUFFER_EXT, OpenGL.GL_STENCIL_INDEX8_EXT, width, height);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.FramebufferRenderbufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_STENCIL_ATTACHMENT_EXT, OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);

			// OpenGL 텍스처를 생성합니다.
			gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

			// OpenGL 텍스처를 생성합니다.
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);

			// glReadPixels 호출
			gl.ReadPixels(0, 0, width, height, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);

			// GPU에서 픽셀 데이터를 읽어옵니다.
			byte[] pixelData = new byte[bufferSize];
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, pbo[0]);
			IntPtr ptr = gl.MapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, OpenGL.GL_READ_ONLY);
			if (ptr != IntPtr.Zero)
			{
				Marshal.Copy(ptr, pixelData, 0, bufferSize);
				gl.UnmapBuffer(OpenGL.GL_PIXEL_PACK_BUFFER);
			}
			gl.BindBuffer(OpenGL.GL_PIXEL_PACK_BUFFER, 0);
			gl.DeleteBuffers(1, pbo);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
			gl.DeleteFramebuffersEXT(1, ids);
			gl.DeleteRenderbuffersEXT(1, renderBuffer);
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

			ReshapeNonRefresh();

			int bytesPerPixel = 4; // RGBA 형식 기준입니다.
			int stride = width * bytesPerPixel;
			byte[] flippedPixels = new byte[pixelData.Length];

			for (int y = 0; y < height; y++)
			{
				int sourceIndex = y * stride;
				int destIndex = (height - 1 - y) * stride;
				Array.Copy(pixelData, sourceIndex, flippedPixels, destIndex, stride);
			}

			// 생성한 텍스처 데이터를 업데이트합니다.
			Array.Copy(flippedPixels, pixelData, pixelData.Length);

			return flippedPixels;
		}

		public byte[] ReadTextureByte(OpenGL gl, uint textureId, int width, int height)
		{
			// 뷰포트를 설정합니다.
			gl.Viewport(0, 0, width, height);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.MatrixMode(OpenGL.GL_PROJECTION);
			gl.LoadIdentity();
			// Y축의 시작과 끝을 반전합니다.
			gl.Ortho2D(0, width, height, 0);  // Y축의 시작과 끝을 반전합니다.

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.MatrixMode(OpenGL.GL_MODELVIEW);
			gl.LoadIdentity();

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			uint[] ids = new uint[1];
			gl.GenFramebuffersEXT(1, ids);
			uint frameBufferID = ids[0];
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
			gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			uint[] renderBuffer = new uint[1];
			gl.GenRenderbuffersEXT(1, renderBuffer);
			gl.BindRenderbufferEXT(OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);
			gl.RenderbufferStorageEXT(OpenGL.GL_RENDERBUFFER_EXT, OpenGL.GL_STENCIL_INDEX8_EXT, width, height);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.FramebufferRenderbufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_STENCIL_ATTACHMENT_EXT, OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);


			//	Bind our texture object (make it the current texture).
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);
			gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

			//  Set linear filtering mode.
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);

			// 생성한 텍스처 데이터를 업데이트합니다.
			byte[] pixelData = new byte[width * height * 4]; // RGBA

			// 생성한 텍스처 데이터를 업데이트합니다.
			gl.ReadPixels(0, 0, width, height, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, pixelData);

			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
			gl.DeleteFramebuffersEXT(1, ids);
			Reshape();
			return pixelData;
		}

		public System.Drawing.Color GetPixelColor(OpenGL gl, int x, int y)
		{
			byte[] pixelData = new byte[4]; // RGBA

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			var screen = GetScreenPosFromPixelCoordf(x, y);

			int invertedY = (int)(gl.RenderContextProvider.Height - Math.Round(screen.Y));

			gl.ReadPixels((int)Math.Round(screen.X), invertedY, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, pixelData);

			return System.Drawing.Color.FromArgb(pixelData[3], pixelData[0], pixelData[1], pixelData[2]);
		}

		public System.Drawing.Color[] GetAllPixelColors(OpenGL gl, int imageWidth, int imageHeight)
		{
			// RGB 값을 0에서 255 사이의 값으로 변환합니다.
			byte[] pixelData = new byte[imageWidth * imageHeight * 4];

			// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
			gl.ReadPixels(0, 0, imageWidth, imageHeight, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, pixelData);

			// 생성한 텍스처 데이터를 업데이트합니다.
			System.Drawing.Color[] colors = new System.Drawing.Color[imageWidth * imageHeight];

			// 생성한 텍스처 데이터를 업데이트합니다.
			for (int i = 0; i < imageWidth * imageHeight; i++)
			{
				int index = i * 4;
				colors[i] = System.Drawing.Color.FromArgb(pixelData[index + 3], pixelData[index], pixelData[index + 1], pixelData[index + 2]);
			}

			return colors;
		}

		/// <summary>
		/// 뷰포트를 설정합니다.
		/// </summary>
		public void Reshape()
		{
			if (!CanUseOpenGlControl())
			{
				return;
			}

			if (openGLControl.InvokeRequired)
			{
				openGLControl.BeginInvoke(new MethodInvoker(Reshape));
				return;
			}

			var gl = openGLControl.OpenGL;

			if (openGLControl.Width <= 0 || openGLControl.Height <= 0)
			{
				return;
			}

			if (_zoom == 100000) { return; }
			gl.SetDimensions(this.openGLControl.Width, this.openGLControl.Height);
			gl.Viewport(0, 0, this.openGLControl.Width, this.openGLControl.Height);

			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();

			_aspectRatio = ((float)this.openGLControl.Width) / this.openGLControl.Height;
			_xSpan = _zoom;
			_ySpan = _zoom;

			if (_aspectRatio > 1)
			{
				_xSpan *= _aspectRatio;
			}
			else
			{
				_ySpan /= _aspectRatio;
			}

			gl.Ortho2D(0, openGLControl.Width * ZoomScale, 0, openGLControl.Height * ZoomScale);

			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();

			InvalidateVisibleOverlayCache();
			RefreshGL();
		}

		public void ReshapeNonRefresh()
		{
			if (!CanUseOpenGlControl())
			{
				return;
			}

			if (openGLControl.InvokeRequired)
			{
				openGLControl.Invoke(new MethodInvoker(ReshapeNonRefresh));
				return;
			}

			var gl = openGLControl.OpenGL;

			if (openGLControl.Width <= 0 || openGLControl.Height <= 0)
			{
				return;
			}

			if (_zoom == 100000) { return; }
			gl.SetDimensions(this.openGLControl.Width, this.openGLControl.Height);
			gl.Viewport(0, 0, this.openGLControl.Width, this.openGLControl.Height);

			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();

			_aspectRatio = ((float)this.openGLControl.Width) / this.openGLControl.Height;
			_xSpan = _zoom;
			_ySpan = _zoom;

			if (_aspectRatio > 1)
			{
				_xSpan *= _aspectRatio;
			}
			else
			{
				_ySpan /= _aspectRatio;
			}

			//gl.Ortho2D(0, openGLControl.Width * ZoomScale, 0, openGLControl.Height * ZoomScale);

			gl.Ortho2D(0, openGLControl.Width * ZoomScale, 0, openGLControl.Height * ZoomScale);

			//gl.Ortho2D(0, _xSpan, 0, _ySpan);
			//gl.Ortho2D(0, 100, 0, 100);

			//  Back to the modelview.
			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
		}

		[Obsolete("Use ReshapeNonRefresh instead.")]
		public void ReshapeNonRefrsh()
		{
			ReshapeNonRefresh();
		}

		/// <summary>
		/// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
		/// </summary>
		/// <returns></returns>
		public int GetControlMinSize()
		{
			Func<int> func = delegate
			{
				return Math.Max(1, Math.Min(this.openGLControl.Height, this.openGLControl.Width));
			};
			int minSize = -1;
			if (!CanUseOpenGlControl())
			{
				return Math.Min(Math.Max(1, Height), Math.Max(1, Width));
			}

			if (openGLControl.InvokeRequired == true)
			{
				openGLControl.Invoke(new MethodInvoker(delegate
				{
					minSize = func();
				}));
				return minSize;
			}
			else
			{
				return func();
			}
		}

		public void ClearTexture()
		{
			ClearTexture(true);
		}

		public void ClearTextureStateOnly()
		{
			ClearTexture(false);
		}

		public void ClearTexture(bool deleteOpenGlTextures)
		{
			Action clearState = delegate
			{
				_textureAreas.Clear();
				_textureKeysOrder.Clear();
			};

			Action action = delegate
			{
				if (!deleteOpenGlTextures)
				{
					clearState();
					return;
				}

				if (_textureAreas.Count <= 0)
				{
					_textureKeysOrder.Clear();
					return;
				}

				List<uint> textureIds = ImageCanvasTextureStore.CollectTextureIds(_textureAreas.Values);

				if (textureIds.Count > 0 && openGLControl != null && !openGLControl.IsDisposed)
				{
					ImageCanvasTextureStore.DeleteTextures(openGLControl.OpenGL, textureIds);
				}

				clearState();
			};

			if (openGLControl == null || openGLControl.IsDisposed)
			{
				clearState();
				return;
			}

			try
			{
				if (openGLControl.InvokeRequired)
				{
					openGLControl.Invoke(new Action(action));
				}
				else
				{
					action();
				}
			}
			catch (ObjectDisposedException)
			{
				clearState();
			}
			catch (InvalidOperationException)
			{
				clearState();
			}
		}

		public IDisposable SuppressRefresh()
		{
			return new RefreshScope(this);
		}

		private sealed class RefreshScope : IDisposable
		{
			private readonly ImageCanvasControl owner;
			private readonly bool previousSuppressRefresh;

			public RefreshScope(ImageCanvasControl owner)
			{
				this.owner = owner;
				previousSuppressRefresh = owner._suppressRefresh;
				owner._suppressRefresh = true;
			}

			public void Dispose()
			{
				owner._suppressRefresh = previousSuppressRefresh;
			}
		}

		public void UpdateTexture(IntPtr data, int width, int height, uint bpp, uint textureId)
		{
			if (!CanUseOpenGlControl())
			{
				return;
			}

			if (openGLControl.InvokeRequired)
			{
				openGLControl.Invoke(new MethodInvoker(() => UpdateTexture(data, width, height, bpp, textureId)));
				return;
			}

			OpenGL gl = GetOpenGL();

			gl.BindTexture(SharpGL.OpenGL.GL_TEXTURE_2D, textureId);

			// OpenGL 텍스처를 생성합니다.
			if (bpp == 3)
			{
				// for colors
				glTexSubImage2D(SharpGL.OpenGL.GL_TEXTURE_2D, 0, 0, 0, width, height, SharpGL.OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data);
			}
			else if (bpp == 4)
			{
				// for RGBA colors
				glTexSubImage2D(SharpGL.OpenGL.GL_TEXTURE_2D, 0, 0, 0, width, height, SharpGL.OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, data);
			}
			else if (bpp == 1)
			{
				// for mono
				glTexSubImage2D(SharpGL.OpenGL.GL_TEXTURE_2D, 0, 0, 0, width, height, SharpGL.OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, data);
			}

			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP);
			gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP);

			gl.TexParameter(SharpGL.OpenGL.GL_TEXTURE_2D, SharpGL.OpenGL.GL_TEXTURE_MIN_FILTER, SharpGL.OpenGL.GL_LINEAR);
			gl.TexParameter(SharpGL.OpenGL.GL_TEXTURE_2D, SharpGL.OpenGL.GL_TEXTURE_MAG_FILTER, SharpGL.OpenGL.GL_NEAREST);

			gl.GenerateMipmapEXT(SharpGL.OpenGL.GL_TEXTURE_2D);

			gl.BindTexture(SharpGL.OpenGL.GL_TEXTURE_2D, textureId);
		}


		/// <summary>
		/// OpenGL 텍스처를 생성합니다.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="bpp"></param>
		/// <returns></returns>
		public uint GenerateOpenGLTexture(int width, int height, uint bpp)
		{
			if (!CanUseOpenGlControl())
			{
				return 0;
			}

			uint textureId = 0;
			Func<uint> action = delegate
			{
				OpenGL gl = GetOpenGL();
				uint[] gtexture = new uint[1];

				gl.GenTextures(1, gtexture); // 텍스처 관련 값입니다.
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, gtexture[0]); // 텍스처 관련 값입니다.

				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);

				// OpenGL 텍스처를 생성합니다.
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);

				gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);

				if (bpp == 3)
				{
					// for Color
					gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, width, height, 0, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
				}
				else if (bpp == 4)
				{
					// for Color
					gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, width, height, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
				}
				else if (bpp == 1)
				{
					// for Mono
					gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_LUMINANCE, width, height, 0, OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
				}

				gl.GenerateMipmapEXT(OpenGL.GL_TEXTURE_2D);

				int[] widthArr = new int[1];
				int[] heightArr = new int[1];
				int[] formatArr = new int[1];
				gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
				gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
				gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

				gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

				return gtexture[0];
			};
			if (openGLControl.InvokeRequired == true)
			{
				openGLControl.Invoke(new MethodInvoker(delegate
				{
					textureId = action();
				}));
				return textureId;
			}
			else
			{
				return action();
			}
		}

		//public uint GenerateOpenGLTexture(int width, int height, uint bpp)
		//{
		//	OpenGL gl = GetOpenGL();

		//	uint textureId = 0;
		//	Func<uint> action = delegate
		//	{
		//		uint[] gtexture = new uint[1];

		// 텍스처 관련 값입니다.
		//									 //CheckGLError(openGLControl.OpenGL, "GenTextures");

		// 텍스처 관련 값입니다.

		//		gl.TexParameter(SharpGL.OpenGL.GL_TEXTURE_2D, SharpGL.OpenGL.GL_TEXTURE_MIN_FILTER, SharpGL.OpenGL.GL_LINEAR);
		//		gl.TexParameter(SharpGL.OpenGL.GL_TEXTURE_2D, SharpGL.OpenGL.GL_TEXTURE_MAG_FILTER, SharpGL.OpenGL.GL_NEAREST);
		//		//CheckGLError(openGLControl.OpenGL, "TexParameter");

		// OpenGL 텍스처를 생성합니다.
		//		gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
		//		gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);

		//		if (bpp == 3)
		//		{
		//			// for Color
		//			gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
		//			gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, width, height, 0,
		//			 SharpGL.OpenGL.GL_BGR,
		//			 OpenGL.GL_UNSIGNED_BYTE,
		//			 IntPtr.Zero
		//			 );
		//			//CheckGLError(openGLControl.OpenGL, "TexImage2D");
		//		}
		//		if (bpp == 4)
		//		{
		//			// for Color
		//			gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
		//			gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, width, height, 0,
		//			 SharpGL.OpenGL.GL_BGRA,
		//			 OpenGL.GL_UNSIGNED_BYTE,
		//			 IntPtr.Zero
		//			 );
		//		}
		//		else if (bpp == 1)
		//		{
		//			// for Mono
		//			gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
		//			gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_LUMINANCE, width, height, 0,
		//				OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
		//		}

		//		gl.GenerateMipmapEXT(OpenGL.GL_TEXTURE_2D);

		//		int[] widthArr = new int[1];
		//		int[] heightArr = new int[1];
		//		int[] formatArr = new int[1];
		//		gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_WIDTH, widthArr);
		//		gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_HEIGHT, heightArr);
		//		gl.GetTexLevelParameter(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_TEXTURE_INTERNAL_FORMAT, formatArr);

		//		//openGLControl.OpenGL.BindTexture(1, gtexture[0]);
		//		gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);

		//		return gtexture[0];
		//	};
		//	if (openGLControl.InvokeRequired == true)
		//	{
		//		openGLControl.Invoke(new MethodInvoker(delegate
		//		{
		//			textureId = action();
		//		}));
		//		return textureId;
		//	}
		//	else
		//	{
		//		return action();
		//	}
		//}

		//private void CheckGLError(OpenGL gl, string location)
		//{
		//	uint error = gl.GetError();
		//	if (error != OpenGL.GL_NO_ERROR)
		//	{
		//		throw new Exception($"OpenGL error at {location}: {error}");
		//	}
		//}

		#endregion

		#region Draw Object
		public void DrawContent()
		{
			DrawContent(drawOverlays: true);
		}

		public void DrawContent(bool drawOverlays)
		{
			DrawContent(drawOverlays, liveOverlay: null);
		}

		public void DrawContent(bool drawOverlays, CanvasShape liveOverlay)
		{
			OpenGL gl = GetOpenGL();
			OpenGlDrawing.DrawTexture(gl, _textureAreas, _textureKeysOrder);
			if (drawOverlays)
			{
				DrawVisibleOverlaysFast(gl, liveOverlay);
			}
			if (IsShowCrossLine) { OpenGlDrawing.DrawCrossOfImage(gl, _textureAreas); }
		}

		public void DrawImagePointMarker(System.Drawing.Point imagePoint, int imageHeight)
		{
			if (imageHeight <= 0) { return; }

			OpenGL gl = GetOpenGL();
			if (gl == null) { return; }

			float x = imagePoint.X;
			float y = imageHeight - imagePoint.Y;
			float markerSize = Math.Max(8f, 14f * ZoomScale);
			float gap = Math.Max(2f, 3f * ZoomScale);

			gl.Disable(OpenGL.GL_TEXTURE_2D);
			gl.LineWidth(2.0f);
			gl.Color(1.0f, 0.85f, 0.05f, 1.0f);
			gl.Begin(OpenGL.GL_LINES);
			gl.Vertex(x - markerSize, y);
			gl.Vertex(x - gap, y);
			gl.Vertex(x + gap, y);
			gl.Vertex(x + markerSize, y);
			gl.Vertex(x, y - markerSize);
			gl.Vertex(x, y - gap);
			gl.Vertex(x, y + gap);
			gl.Vertex(x, y + markerSize);
			gl.End();

			gl.PointSize(5.0f);
			gl.Begin(OpenGL.GL_POINTS);
			gl.Vertex(x, y);
			gl.End();

			float boxSize = Math.Max(4f, 6f * ZoomScale);
			gl.LineWidth(1.0f);
			gl.Begin(OpenGL.GL_LINE_LOOP);
			gl.Vertex(x - boxSize, y - boxSize);
			gl.Vertex(x + boxSize, y - boxSize);
			gl.Vertex(x + boxSize, y + boxSize);
			gl.Vertex(x - boxSize, y + boxSize);
			gl.End();
			gl.Enable(OpenGL.GL_TEXTURE_2D);
		}

		public void DrawImagePixelMarker(System.Drawing.Point imagePoint, int imageHeight)
		{
			if (imageHeight <= 0) { return; }

			OpenGL gl = GetOpenGL();
			if (gl == null) { return; }

			System.Drawing.RectangleF pixelBounds = ImagePixelCoordinateMapper.ToOpenGlPixelBounds(imagePoint, imageHeight);
			if (pixelBounds.IsEmpty) { return; }

			System.Drawing.PointF pixelCenter = ImagePixelCoordinateMapper.ToOpenGlPixelCenter(imagePoint, imageHeight);
			float left = pixelBounds.Left;
			float right = pixelBounds.Right;
			float top = pixelBounds.Bottom;
			float bottom = pixelBounds.Top;
			float centerX = pixelCenter.X;
			float centerY = pixelCenter.Y;
			float markerSize = 14f * ZoomScale;
			float gap = 3f * ZoomScale;

			gl.Disable(OpenGL.GL_TEXTURE_2D);
			gl.LineWidth(1.0f);
			gl.Color(1.0f, 0.85f, 0.05f, 1.0f);

			gl.Begin(OpenGL.GL_LINE_LOOP);
			gl.Vertex(left, bottom);
			gl.Vertex(right, bottom);
			gl.Vertex(right, top);
			gl.Vertex(left, top);
			gl.End();

			gl.LineWidth(1.5f);
			gl.Begin(OpenGL.GL_LINES);
			gl.Vertex(centerX - markerSize, centerY);
			gl.Vertex(centerX - gap, centerY);
			gl.Vertex(centerX + gap, centerY);
			gl.Vertex(centerX + markerSize, centerY);
			gl.Vertex(centerX, centerY - markerSize);
			gl.Vertex(centerX, centerY - gap);
			gl.Vertex(centerX, centerY + gap);
			gl.Vertex(centerX, centerY + markerSize);
			gl.End();

			gl.PointSize(3.0f);
			gl.Begin(OpenGL.GL_POINTS);
			gl.Vertex(centerX, centerY);
			gl.End();
			gl.Enable(OpenGL.GL_TEXTURE_2D);
		}
		public void DrawODBTexture()
		{
			OpenGL gl = GetOpenGL();
			OpenGlDrawing.DrawODBTexture(gl, _textureAreas, _textureKeysOrder);
		}

		public void DrawMeasurement(OpenGL gl, Measurement measurement, OpenGlFontRenderOptions glFontRenderOptions)
		{
			OpenGlDrawing.DrawMeasurement(gl, measurement, glFontRenderOptions, _fontBitmapEntries, _xSpan, _ySpan, _fitRect, _offsetSize, PixelPermm);
		}

		public Bitmap RenderTextureToBitmap(string imageName, uint textureId, uint bpp)
		{
			Bitmap bitmap = new Bitmap(10, 10);
			if (this.InvokeRequired)
			{

				this.Invoke(new MethodInvoker(() =>
				{
					bitmap = RenderTextureToBitmap(imageName, textureId, bpp);
				}));
				return bitmap;
			}
			else
			{
				OpenGL gl = GetOpenGL();
				Bitmap bmp = OpenGlRenderer.TextureToBitmap(gl, textureId, bpp);

				//bmp.Save("D:\\ori.bmp");

				OpenGlRenderer.InitializeOpenGLSettings(gl, bmp.Width, bmp.Height);

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				uint[] ids = new uint[1];
				gl.GenFramebuffersEXT(1, ids);
				uint frameBufferID = ids[0];
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
				gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, textureId, 0);

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				uint[] renderBuffer = new uint[1];
				gl.GenRenderbuffersEXT(1, renderBuffer);
				gl.BindRenderbufferEXT(OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);
				gl.RenderbufferStorageEXT(OpenGL.GL_RENDERBUFFER_EXT, OpenGL.GL_STENCIL_INDEX8_EXT, bmp.Width, bmp.Height);

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				gl.FramebufferRenderbufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_STENCIL_ATTACHMENT_EXT, OpenGL.GL_RENDERBUFFER_EXT, renderBuffer[0]);

				uint[] gtexture = new uint[1];
				gtexture[0] = GenerateOpenGLTexture(bmp.Width, bmp.Height, bpp);

				//	Get the maximum texture size supported by OpenGL.
				int[] textureMaxSize = { 0 };
				gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, textureMaxSize);

				//  Ensure that the image does not exceed the maximum texture size.
				if (bmp.Width > textureMaxSize[0] || bmp.Height > textureMaxSize[0])
				{
					throw new InvalidOperationException("Image exceeds the maximum texture size.");
				}

				//	Bind our texture object (make it the current texture).
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, gtexture[0]);
				gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, gtexture[0], 0);

				//  Lock the image bits (so that we can pass them to OGL).
				BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
					ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

				//  Set the image data.
				gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA,
					bmp.Width, bmp.Height, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE,
					bitmapData.Scan0);

				//  Unlock the image.
				bmp.UnlockBits(bitmapData);

				//  Set linear filtering mode.
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, SharpGL.OpenGL.GL_TEXTURE_MIN_FILTER, SharpGL.OpenGL.GL_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, SharpGL.OpenGL.GL_TEXTURE_MAG_FILTER, SharpGL.OpenGL.GL_NEAREST);

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				gl.Enable(OpenGL.GL_STENCIL_TEST);

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				gl.ClearStencil(0);
				gl.Clear(OpenGL.GL_STENCIL_BUFFER_BIT);
				gl.StencilFunc(OpenGL.GL_ALWAYS, 1, 0xFF);
				gl.StencilOp(OpenGL.GL_KEEP, OpenGL.GL_KEEP, OpenGL.GL_REPLACE);

				// 이미지 영역을 좌하단 기준으로 설정합니다.
				gl.ColorMask(0, 0, 0, 0); // 화면에는 색을 쓰지 않습니다.

				var eraerPoints = GetEraserPoint();
				foreach (var eraerPoint in eraerPoints)
				{
					OpenGlDrawing.DrawWithPen(gl, eraerPoint.EraserPointfs, eraerPoint.EraserWidth, System.Windows.Media.Brushes.Yellow);
				}
				// 마우스 이동량을 계산합니다.
				gl.ColorMask(1, 1, 1, 1);
				gl.StencilFunc(OpenGL.GL_NOTEQUAL, 1, 0xFF);
				gl.StencilOp(OpenGL.GL_KEEP, OpenGL.GL_KEEP, OpenGL.GL_REPLACE);

				OpenGlTextureDrawingParam param = GetTextureParameterAtPoint(textureId);

				// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
				var textureArea = param.GLTextureArea;
				var fullSize = param.TextureFullScreen;

				System.Drawing.Size tileSize = param.TitleSize;

				// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
				int offsetX = (int)(textureArea.X / tileSize.Width) * tileSize.Width;
				int offsetY = 0;
				if (tileSize.Height > textureArea.Y + textureArea.Height)
				{
					offsetY = (int)0;
				}
				else
				{
					if (tileSize.Height > textureArea.Y)
					{
						offsetY = (int)textureArea.Y;
					}
					else
					{
						int qui = (int)textureArea.Y / tileSize.Height;
						offsetY = (int)(textureArea.Y);
					}
				}


				foreach (CanvasOverlayItem overlayItem in GetCanvasOverlayManager().GetOverlaysByGroupType(imageName))
				{
					if (overlayItem.Shape is LineInfo)
					{
						LineInfo lineInfo = overlayItem.Shape as LineInfo;
						// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
						System.Drawing.PointF start = new PointF(lineInfo.StartDot.X - offsetX, lineInfo.StartDot.Y - offsetY);
						System.Drawing.PointF end = new PointF(lineInfo.EndDot.X - offsetX, lineInfo.EndDot.Y - offsetY);

						// RGB 값을 0에서 255 사이의 값으로 변환합니다.
						byte red = (byte)(lineInfo.LineColor[0] * 255);
						byte green = (byte)(lineInfo.LineColor[1] * 255);
						byte blue = (byte)(lineInfo.LineColor[2] * 255);

						// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
						System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));

						OpenGlDrawing.DrawLine(gl, start, end, lineInfo.Width, brush);
					}
					if (overlayItem.Shape is RectInfo)
					{
						RectInfo rectInfo = overlayItem.Shape as RectInfo;

						// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
						System.Drawing.PointF start = new PointF(rectInfo.LeftBottom.X - offsetX, rectInfo.LeftBottom.Y - offsetY);
						System.Drawing.PointF end = new PointF(rectInfo.RightTop.X - offsetX, rectInfo.RightTop.Y - offsetY);

						// RGB 값을 0에서 255 사이의 값으로 변환합니다.
						byte red = (byte)(rectInfo.LineColor[0] * 255);
						byte green = (byte)(rectInfo.LineColor[1] * 255);
						byte blue = (byte)(rectInfo.LineColor[2] * 255);

						System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));

						OpenGlDrawing.DrawRectangle(gl, start, end, rectInfo.Width, rectInfo.IsFill, brush);
					}
					if (overlayItem.Shape is CircleInfo)
					{
						CircleInfo circleInfo = overlayItem.Shape as CircleInfo;

						// OpenGL은 좌하단을 원점으로 사용하므로 y 좌표를 계산하거나 반전합니다.
						System.Drawing.PointF start = new PointF(circleInfo.StartDot.X - offsetX, circleInfo.StartDot.Y - offsetY);
						System.Drawing.PointF end = new PointF(circleInfo.EndDot.X - offsetX, circleInfo.EndDot.Y - offsetY);

						//System.Drawing.PointF start = new PointF(circleInfo.StartDot.X, circleInfo.StartDot.Y);
						//System.Drawing.PointF end = new PointF(circleInfo.EndDot.X, circleInfo.EndDot.Y);

						// RGB 값을 0에서 255 사이의 값으로 변환합니다.
						byte red = (byte)(circleInfo.LineColor[0] * 255);
						byte green = (byte)(circleInfo.LineColor[1] * 255);
						byte blue = (byte)(circleInfo.LineColor[2] * 255);

						System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
						OpenGlDrawing.DrawCircle(gl, start, end, circleInfo.Width, brush, circleInfo.IsFill);
					}
					if (overlayItem.Shape is PensInfo)
					{
						PensInfo pensInfo = overlayItem.Shape as PensInfo;

						// RGB 값을 0에서 255 사이의 값으로 변환합니다.
						byte red = (byte)(pensInfo.LineColor[0] * 255);
						byte green = (byte)(pensInfo.LineColor[1] * 255);
						byte blue = (byte)(pensInfo.LineColor[2] * 255);

						List<DotInfo> newDots = new List<DotInfo>();
						var imageTitleOffset = pensInfo.ImageTitleOffset;
						foreach (DotInfo dot in pensInfo.Dots)
						{
							newDots.Add(new DotInfo(dot.X - offsetX, dot.Y - offsetY));
						}

						System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));

						OpenGlDrawing.DrawWithPen(gl, newDots, pensInfo.Width, brush);
					}
				}

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				gl.Disable(OpenGL.GL_STENCIL_TEST);

				Bitmap copy = OpenGlRenderer.TextureToBitmap(gl, gtexture[0], bpp);

				// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
				gl.DeleteTextures(1, gtexture);
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
				gl.DeleteFramebuffersEXT(1, ids);
				Reshape();

				return copy;
			}
		}

		/// <summary>
		/// 뷰포트를 설정합니다.
		/// </summary>
		/// <param name="gl"></param>
		public void DrawVisibleOverlays(OpenGL gl)
		{
			// 이미지 캔버스의 좌표와 텍스처 상태를 처리합니다.
			if (_shapesViewPort == null) { return; }
			foreach (CanvasShape shape in _shapesViewPort)
			{
				if (shape == null || shape.DisplayListId == 0) { continue; }
				gl.CallList(shape.DisplayListId);
			}
		}

		public void DrawVisibleOverlaysFast(OpenGL gl, CanvasShape liveOverlay)
		{
			if (_shapesViewPort == null || _shapesViewPort.Count == 0)
			{
				DrawLiveOverlay(gl, liveOverlay);
				return;
			}

			if (_fastOverlaySceneDirty || !ReferenceEquals(_fastOverlaySceneLiveShape, liveOverlay) || _fastOverlaySceneListId == 0)
			{
				CompileFastOverlayScene(gl, liveOverlay);
			}

			if (_fastOverlaySceneListId != 0)
			{
				gl.CallList(_fastOverlaySceneListId);
			}

			DrawLiveOverlay(gl, liveOverlay);
		}

		private void CompileFastOverlayScene(OpenGL gl, CanvasShape liveOverlay)
		{
			if (gl == null) { return; }

			// During ROI drag, static overlays are compiled once and the moving ROI is drawn live.
			// This avoids replaying every visible object on every MouseMove frame.
			if (_fastOverlaySceneListId != 0)
			{
				gl.DeleteLists(_fastOverlaySceneListId, 1);
				_fastOverlaySceneListId = 0;
			}

			_fastOverlaySceneListId = gl.GenLists(1);
			_fastOverlaySceneLiveShape = liveOverlay;
			gl.NewList(_fastOverlaySceneListId, OpenGL.GL_COMPILE);
			foreach (CanvasShape shape in _shapesViewPort)
			{
				if (shape == null || shape.DisplayListId == 0 || ReferenceEquals(shape, liveOverlay))
				{
					continue;
				}

				gl.CallList(shape.DisplayListId);
			}
			gl.EndList();
			_fastOverlaySceneDirty = false;
		}

		private static void DrawLiveOverlay(OpenGL gl, CanvasShape liveOverlay)
		{
			if (gl == null || liveOverlay == null)
			{
				return;
			}

			if (liveOverlay is CanvasRect<float> rect && !rect.IsEmpty())
			{
				// The active ROI geometry changes every MouseMove. Draw it directly and compile
				// its display list once on mouse-up, otherwise resize feels sticky.
				if (rect.ShapeKind == CanvasRoiShapeKind.Ellipse)
				{
					EnumFillMode fillMode = rect.IsFill ? EnumFillMode.InFill : EnumFillMode.None;
					OpenGlDrawing.DrawEllipse(gl, rect, rect.LineWidth, System.Windows.Media.Brushes.DeepSkyBlue, fillMode);
				}
				else
				{
					OpenGlDrawing.DrawShape(gl, rect, Color.DeepSkyBlue, false, false, rect.LineWidth);
				}
				return;
			}

			if (liveOverlay.DisplayListId != 0)
			{
				gl.CallList(liveOverlay.DisplayListId);
			}
		}

		/// <summary>
		/// 이미지 영역을 좌하단 기준으로 설정합니다.
		/// </summary>
		private void CalculatorVisibleOverlays()
		{
			List<CanvasShape> visibleShapes = new List<CanvasShape>(MaxVisibleOverlayShapes);
			// A zoomed-out labeling screen can contain hundreds of thousands of ROI.
			// Hit-testing still uses the full spatial index; the visual cache is capped
			// so repaint and pan/zoom never try to replay every label in one frame.
			int visitedCount = _overlayManager.VisitVisibleOverlaysInBounds(GetViewportBounds(), MaxVisibleOverlayShapes + 1, overlayItem =>
			{
				if (visibleShapes.Count < MaxVisibleOverlayShapes)
				{
					AddVisibleOverlayShapesIfInViewport(overlayItem, visibleShapes, MaxVisibleOverlayShapes, skipViewportCheck: true);
				}
			});

			_shapesViewPort = visibleShapes;
			UpdateVisibleOverlayLodState(visibleShapes.Count, visitedCount > MaxVisibleOverlayShapes);
		}

		private RectangleF GetViewportBounds()
		{
			float viewportLeft = 0 - _offsetSize.Width;
			float viewportRight = _xSpan - _offsetSize.Width;
			float viewportTop = _ySpan - _offsetSize.Height;
			float viewportBottom = 0 - _offsetSize.Height;
			return RectangleF.FromLTRB(viewportLeft, viewportBottom, viewportRight, viewportTop);
		}

		private void AddVisibleOverlayShapesIfInViewport(
			CanvasOverlayItem overlayItem,
			List<CanvasShape> visibleShapes,
			int maxShapes,
			bool skipViewportCheck = false)
		{
			CanvasShape shape = overlayItem?.Shape;
			// Viewport rebuilds arrive from CanvasOverlaySpatialIndex, which already filters by
			// bounds. Incremental adds still verify the viewport because they bypass that query.
			if (shape == null
				|| visibleShapes == null
				|| visibleShapes.Count >= maxShapes
				|| (!skipViewportCheck && !IsShapeInViewport(shape)))
			{
				return;
			}

			if (overlayItem.IsExtentionRectange && shape is CanvasRect<float> rect && rect.ExtendedRectangle != null)
			{
				visibleShapes.Add(rect.ExtendedRectangle);
				if (visibleShapes.Count >= maxShapes)
				{
					// Extended rectangles count toward the same visual LOD budget.
					// Selection and editing still query the full spatial index.
					return;
				}
			}

			visibleShapes.Add(shape);
		}

		private bool IsShapeInViewport(CanvasShape shape)
		{
			bool hasPoint = false;
			float shapeLeft = float.MaxValue;
			float shapeRight = float.MinValue;
			float shapeTop = float.MinValue;
			float shapeBottom = float.MaxValue;

			foreach (DotInfo dot in shape.ShapePoints)
			{
				hasPoint = true;
				if (dot.X < shapeLeft) { shapeLeft = dot.X; }
				if (dot.X > shapeRight) { shapeRight = dot.X; }
				if (dot.Y > shapeTop) { shapeTop = dot.Y; }
				if (dot.Y < shapeBottom) { shapeBottom = dot.Y; }
			}

			if (!hasPoint)
			{
				return false;
			}

			float viewportLeft = 0 - _offsetSize.Width;
			float viewportRight = _xSpan - _offsetSize.Width;
			float viewportTop = _ySpan - _offsetSize.Height;
			float viewportBottom = 0 - _offsetSize.Height;

			return shapeLeft < viewportRight
				&& shapeRight > viewportLeft
				&& shapeTop > viewportBottom
				&& shapeBottom < viewportTop;
		}
		#endregion
	}
}
