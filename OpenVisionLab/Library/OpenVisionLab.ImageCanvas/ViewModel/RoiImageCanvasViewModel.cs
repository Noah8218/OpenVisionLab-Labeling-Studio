using OpenVisionLab.ImageCanvas.SharedViewModels;
using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.Infrastructure;
using OpenVisionLab.ImageCanvas.Commands;
using OpenVisionLab.ImageCanvas.Events;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Overlays;
using OpenVisionLab.ImageCanvas.OpenGLRendering;
using Microsoft.Win32;
using OpenCvSharp;
using SharpGL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Model = OpenVisionLab.ImageCanvas.Model;

namespace OpenVisionLab.ImageCanvas.ViewModels
{
	public partial class RoiImageCanvasViewModel : ObservableObject
	{
		internal const double DrawingRefreshIntervalMilliseconds = 16.0;
		private static readonly long PixelPropertyUpdateIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 30);
		private static readonly long BrushCursorPreviewRefreshIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 60);
		private static readonly ConcurrentDictionary<int, BrushPreviewStamp> BrushPreviewStampCache = new ConcurrentDictionary<int, BrushPreviewStamp>();

		#region Event
		public event EventHandler<object> LoadImageRequested = delegate { };
		public event EventHandler<CanvasRect<float>> RemoveRoiRequested = delegate { };

		public event EventHandler<Model.RoiChangedEventArgs> RoiAdded = delegate { };
		public event EventHandler<Model.RoiChangedEventArgs> RoiMouseUp = delegate { };
		public event EventHandler<Model.RoiChangedEventArgs> RoiGrouped = delegate { };
		public event EventHandler<Model.RoiChangedEventArgs> RoiEditingCompleted = delegate { };
		public event EventHandler<object> QuickTestRequest = delegate { };
		public event EventHandler<int> DetectionOverlayClicked = delegate { };
		public event EventHandler<CanvasImagePointEventArgs> ImagePointClicked = delegate { };
		public event EventHandler<CanvasImagePointEventArgs> ImagePointHovered = delegate { };
		public event EventHandler<CanvasImagePointEventArgs> ImagePointMoved = delegate { };
		public event EventHandler<CanvasImagePointEventArgs> ImagePointReleased = delegate { };

		// 모델?�리???�성???�시�??�니??
		public Action OnWindowsChanged { get; set; } // 콜백 추�?
		#endregion

		#region Fields
		private bool _isTeachingMode = false;
		private bool _isAddRoiArrayMode = false;
		private bool _isShowMeasure = false;
		private bool _useGroupMoveMode = false;
		private bool _isPreviewMode = false;
		private float _heightValue;
		private Measurement _measurement = new Measurement();
		private OpenGlFontRenderOptions _measureFontOption = new OpenGlFontRenderOptions(System.Drawing.Color.Red, "Arial", 20, "");
		protected CanvasRect<float> _selectedRect = new CanvasRect<float>();
		protected CanvasRect<float> _drawingRect = new CanvasRect<float>();
		protected CanvasRect<float> _copyRoiRect;
		private System.Drawing.Point _mouseDownCanvasPos = System.Drawing.Point.Empty;
		private bool _mouseDownOnExistingRoi;
		private bool _mouseDownOnDetectionOverlay;
		private System.Drawing.Size _imageSize = new System.Drawing.Size();
		private OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl _imageViewer = new OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl();
		public float[] AfData3D = new float[10];
		private AddRoiArrayViewModel _addRoiArrayVm = new AddRoiArrayViewModel();
		private System.Timers.Timer _refreshTimer;
		private System.Timers.Timer _reshapeTimer;
		private readonly List<RoiImageCanvasDetectionOverlay> _detectionOverlays = new List<RoiImageCanvasDetectionOverlay>();
		private readonly DetectionOverlaySpatialIndex _detectionOverlayHitIndex = new DetectionOverlaySpatialIndex();
		private readonly List<RoiImageCanvasPolygonOverlay> _polygonOverlays = new List<RoiImageCanvasPolygonOverlay>();
		private readonly List<RectangleF> _polygonOverlayBounds = new List<RectangleF>();
		private readonly OverlayRenderSpatialIndex _polygonOverlayRenderIndex = new OverlayRenderSpatialIndex();
		private readonly List<RoiImageCanvasMaskOverlay> _maskOverlays = new List<RoiImageCanvasMaskOverlay>();
		private readonly List<RectangleF> _maskOverlayBounds = new List<RectangleF>();
		private readonly List<int> _selectedMaskOverlayIndices = new List<int>();
		private readonly HashSet<string> _activeMaskOverlayKeys = new HashSet<string>(StringComparer.Ordinal);
		private readonly OverlayRenderSpatialIndex _maskOverlayRenderIndex = new OverlayRenderSpatialIndex();
		private readonly Dictionary<string, MaskOverlayTextureCache> _maskOverlayTextures = new Dictionary<string, MaskOverlayTextureCache>(StringComparer.Ordinal);
		private readonly MaskStrokePreviewLayer _maskStrokePreviewLayer = new MaskStrokePreviewLayer();
		private readonly Queue<MaskStrokePreviewCommand> _pendingMaskStrokePreviewCommands = new Queue<MaskStrokePreviewCommand>();
		private bool _maskStrokePreviewVisible;
		private System.Drawing.Size _maskStrokePreviewImageSize = System.Drawing.Size.Empty;
		private System.Drawing.Color _maskStrokePreviewColor = System.Drawing.Color.FromArgb(150, 44, 210, 110);
		private bool _maskStrokePreviewIsEraser;
		private long _lastMaskStrokePreviewRefreshTicks;
		private bool _brushCursorPreviewVisible;
		private System.Drawing.Point _brushCursorPreviewImagePoint = System.Drawing.Point.Empty;
		private int _brushCursorPreviewRadius;
		private System.Drawing.Color _brushCursorPreviewColor = System.Drawing.Color.FromArgb(80, 180, 255);
		private bool _brushCursorPreviewIsEraser;
		private RoiImageCanvasInputAdapter _inputAdapter;
		private long _lastPixelPropertyUpdateTicks;
		private long _lastBrushCursorPreviewRefreshTicks;
		#endregion

		#region Properties
		public OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl ImageViewer
		{
			get { return _imageViewer; }
		}

		public System.Windows.Controls.ContextMenu ContextMenu { get; set; }

		public int GrayValue
		{
			get
			{
				if (_imageViewer != null) { return _imageViewer.GrayValue; }
				return 0;
			}
		}

		public bool IsShowCrossLine
		{
			get
			{
				if (_imageViewer != null) { return _imageViewer.IsShowCrossLine; }
				return false;
			}
			set
			{
				if (_imageViewer != null) { _imageViewer.IsShowCrossLine = value; }
				_imageViewer.RefreshGL();
				OnPropertyChanged(nameof(IsShowCrossLine));
			}
		}

		public bool IsShowMeasure
		{
			get
			{
				if (_imageViewer != null) { return _isShowMeasure; }
				return false;
			}
			set
			{
				if (_imageViewer != null)
				{
					_isShowMeasure = value;
					_imageViewer.SetViewMode((_isShowMeasure == true) ? CanvasInteractionMode.Measure : CanvasInteractionMode.None);
				}
				if (!value)
				{
					_measurement = new Measurement();
				}
				OnPropertyChanged(nameof(IsShowMeasure));
			}
		}

		public bool IsTeachingMode
		{
			get
			{
				if (_imageViewer != null) { return _isTeachingMode; }
				OnPropertyChanged(nameof(IsTeachingMode));
				return false;
			}
			set
			{
				if (_imageViewer != null)
				{
					_isTeachingMode = value;
					_imageViewer.SetViewMode((_isTeachingMode == true) ? CanvasInteractionMode.Drawing : CanvasInteractionMode.None);
				}

				OnPropertyChanged(nameof(IsTeachingMode));
			}
		}

		public bool IsAddRoiArrayMode
		{
			get
			{
				if (_imageViewer != null) { return _isAddRoiArrayMode; }
				OnPropertyChanged(nameof(IsAddRoiArrayMode));
				return false;
			}
			set
			{
				if (_imageViewer != null)
				{
					_isAddRoiArrayMode = value;
					_imageViewer.SetViewMode((_isAddRoiArrayMode == true) ? CanvasInteractionMode.Drawing : CanvasInteractionMode.None);
				}
				OnPropertyChanged(nameof(IsAddRoiArrayMode));
			}
		}

		public bool IsPreviewMode
		{
			get
			{
				if (_imageViewer != null) { return _isPreviewMode; }
				OnPropertyChanged();
				return false;
			}
			set
			{
				if (_imageViewer != null) { _isPreviewMode = value; }
				_imageViewer.RefreshGL();
				OnPropertyChanged();
			}
		}

		public bool UseGroupMoveMode
		{
			get
			{
				if (_imageViewer != null) { return _useGroupMoveMode; }
				OnPropertyChanged();
				return false;
			}
			set
			{
				if (_imageViewer != null) { _useGroupMoveMode = value; }
				OnPropertyChanged();
			}
		}

		public bool ShowGroupNames { get; set; } = true;
		public bool ShowRoiItemNames { get; set; } = true;
		public bool ReplaceExistingRoiOnDraw { get; set; }
		public CanvasRoiShapeKind DrawingShapeKind { get; set; } = CanvasRoiShapeKind.Rectangle;
		public bool IsImagePointInputMode { get; set; }

		public bool ShowGroupBounds
		{
			get => _imageViewer.GetLastGroup()?.IsVisible ?? true;
			set
			{
				CanvasOverlayItem group = _imageViewer.GetLastGroup();
				if (group == null) { return; }

				if (value)
				{
					_imageViewer.ResizeGroupRectangle(group.GroupType);
					_imageViewer.InvalidateVisibleOverlayCache();
				}
				group.IsVisible = value;
				group.Shape.IsChanged = true;
				_imageViewer.RefreshGL();
			}
		}

		public System.Drawing.PointF RobotPos
		{
			get
			{
				if (_imageViewer != null) { return _imageViewer.PixelPos; }
				return new System.Drawing.PointF();
			}
		}

		public System.Drawing.PointF ImagePos
		{
			get
			{
				if (_imageViewer != null) { return _imageViewer.ImagePixelPos; }
				return new System.Drawing.PointF();
			}
		}

		public float HeightValue
		{
			get
			{
				if (_imageViewer != null) { return _heightValue; }
				return 0;
			}
			set
			{
				if (_imageViewer != null) { _heightValue = value; }
				OnPropertyChanged();
			}
		}

		public System.Drawing.Color PixelColor
		{
			get
			{
				if (_imageViewer != null) { return _imageViewer.PixelColor; }
				return new System.Drawing.Color();
			}
		}

		public bool IsOverlayDisplayLodActive => _imageViewer?.IsVisibleOverlayLodActive == true;

		public string OverlayDisplayStatusText
		{
			get
			{
				if (!IsOverlayDisplayLodActive)
				{
					return string.Empty;
				}

				return string.Format(
					System.Globalization.CultureInfo.InvariantCulture,
					"표시 ROI: {0:N0}+",
					_imageViewer.VisibleOverlayShapeLimit);
			}
		}

		public ObservableCollection<MenuItemViewModel> MenuItems { get; set; }

		public IReadOnlyList<RoiImageCanvasDetectionOverlay> DetectionOverlays => _detectionOverlays;

		public IReadOnlyList<RoiImageCanvasPolygonOverlay> PolygonOverlays => _polygonOverlays;

		public IReadOnlyList<RoiImageCanvasMaskOverlay> MaskOverlays => _maskOverlays;

		public bool IsMaskStrokePreviewVisible => _maskStrokePreviewVisible;

		public bool IsBrushCursorPreviewVisible => _brushCursorPreviewVisible;

		public System.Drawing.Point BrushCursorPreviewImagePoint => _brushCursorPreviewImagePoint;

		public int BrushCursorPreviewRadius => _brushCursorPreviewRadius;

		#endregion

		#region Command
		public ICommand LoadedCommand { get; set; }
		public ICommand RightClickCommand { get; private set; }
		public ICommand LoadImageCommand { get; set; }
		public ICommand SaveImageCommand { get; set; }
		public ICommand ShowCrossLineCommand { get; set; }
		public ICommand MeasureCommand { get; set; }
		public ICommand TeachingCommand { get; set; }
		public ICommand AddingArrayCommand { get; set; }
		public ICommand ShowPreviewCommand { get; set; }
		public ICommand PreviewKeyDownCommand { get; private set; }
		public ICommand KeyUpCommand { get; set; }
		#endregion
		public RoiImageCanvasViewModel(string name)
		{
			InitCommand();
			InitEvent();
			InitMenuItems();
			_imageViewer.SetNameGL(name);
			InitializeDefaultGroup();
		}

		private void InitializeDefaultGroup()
		{
			string groupType = EnumInspWindowType.Module.ToString();
			_imageViewer.AddOverlay("", groupType, new CanvasRect<float>(), Guid.NewGuid().ToString(), EnumInspWindowType.Module, EnumItemType.Group, false, true);
			_imageViewer.SetLastGroupType(groupType);
		}

		private void Loaded()
		{
			_imageViewer.InvertYAxis = true;
		}

		private void InitEvent()
		{
			_inputAdapter = new RoiImageCanvasInputAdapter(_imageViewer);
			_imageViewer.VisibleOverlayLodChanged += ImageViewer_VisibleOverlayLodChanged;
			_inputAdapter.Load += OnLoad;
			_inputAdapter.Resized += OnResized;
			_inputAdapter.MouseDoubleClicked += OnMouseDoubleClicked;
			_imageViewer.Draw += OnDraw;
			_inputAdapter.KeyDown += OnKeyDown;
			_inputAdapter.KeyUp += OnKeyUp;
			_inputAdapter.MouseClicked += OnMouseClicked;
			_inputAdapter.MouseDown += OnMouseDown;
			_inputAdapter.MouseMove += OnMouseMove;
			_inputAdapter.MouseUp += OnMouseUp;
			_inputAdapter.MouseLeave += OnMouseLeave;
			_inputAdapter.MouseWheel += OnMouseWheel;

			_refreshTimer = new System.Timers.Timer(DrawingRefreshIntervalMilliseconds);
			_refreshTimer.AutoReset = false;
			_refreshTimer.Elapsed += _refreshTimer_Elapsed;
			_reshapeTimer = new System.Timers.Timer(DrawingRefreshIntervalMilliseconds);
			_reshapeTimer.AutoReset = false;
			_reshapeTimer.Elapsed += _reshapeTimer_Elapsed;
		}

		private void ImageViewer_VisibleOverlayLodChanged(object sender, EventArgs e)
		{
			OnPropertyChanged(nameof(IsOverlayDisplayLodActive));
			OnPropertyChanged(nameof(OverlayDisplayStatusText));
		}

		private void InitMenuItems()
		{
			MenuItems = new ObservableCollection<MenuItemViewModel>();
			MenuItems.Add(new MenuItemViewModel { Header = MenuItemUtil.GetDescription(EnumImageCanvasItems.LoadImage), Command = LoadImageCommand, IconData = MaterialIconData.Image, IsVisible = true });
			MenuItems.Add(new MenuItemViewModel { Header = MenuItemUtil.GetDescription(EnumImageCanvasItems.SaveImage), Command = SaveImageCommand, IconData = MaterialIconData.ContentSave, IsVisible = true });
		}
		private void OnDraw(object sender, OpenVisionLab.ImageCanvas.Canvas.CanvasRenderEventArgs e)
		{
			OpenGL gl = e.GL;
			CanvasInteractionMode viewMode = _imageViewer.GetViewMode();
			bool isFastPanFrame = viewMode == CanvasInteractionMode.Drag;
			bool isFastRoiManipulationFrame = viewMode == CanvasInteractionMode.Edit || viewMode == CanvasInteractionMode.Move;
			bool isFastDrawingPreviewFrame = viewMode == CanvasInteractionMode.Drawing
				&& _drawingRect != null
				&& _drawingRect.IsEditing
				&& !_drawingRect.IsEmpty();
			CanvasShape liveOverlay = isFastRoiManipulationFrame
				? _selectedRect
				: isFastDrawingPreviewFrame ? _drawingRect : null;

			_imageViewer.DrawContent(drawOverlays: true, liveOverlay: liveOverlay);
			if (isFastPanFrame)
			{
				// Pan keeps the cached ROI scene visible. Expensive secondary overlays
				// are restored on the settled repaint so texture movement stays smooth.
				return;
			}
			if (isFastDrawingPreviewFrame)
			{
				// New-box feedback is drawn as one live shape. The ROI is committed and
				// compiled only on mouse-up, which keeps rectangle drawing responsive.
				return;
			}
			if (isFastRoiManipulationFrame)
			{
				// Keep ROI manipulation responsive: draw the cached static ROI scene plus the active ROI only.
				// Detection/mask/text overlays are restored on the full repaint after mouse-up.
				OpenGlDrawing.DrawRoiEditHandles(gl, _selectedRect, _imageViewer.ZoomScale, System.Windows.Media.Brushes.DeepSkyBlue);
				return;
			}

			if (!DrawMaskStrokePreviewLayer(gl))
			{
				DrawMaskOverlays(gl);
			}
			DrawDetectionOverlays(gl);
			DrawPolygonOverlays(gl);
			OpenGlDrawing.DrawRoiEditHandles(gl, GetOverlayRect(), _imageViewer.ZoomScale, System.Windows.Media.Brushes.DeepSkyBlue);
			DrawSelectedMaskOverlayMarkers(gl);
			DrawBrushCursorPreview(gl);
			if (ShowGroupNames)
			{
				OpenGlDrawing.DrawGroupName(gl, _imageViewer.GetCanvasOverlayManager(), _imageViewer.GetOpenGlTextDrawOptions());
			}
			if (ShowRoiItemNames)
			{
				OpenGlDrawing.DrawRoiItemName(gl, _imageViewer.GetCanvasOverlayManager(), _imageViewer.GetOpenGlTextDrawOptions());
			}
			if (IsShowMeasure) { _imageViewer.DrawMeasurement(gl, _measurement, _measureFontOption); }
		}

		private void DrawDetectionOverlays(OpenGL gl)
		{
			if (_detectionOverlays.Count == 0 || _imageSize.IsEmpty)
			{
				return;
			}

			OpenGlTextDrawOptions textOptions = _imageViewer.GetOpenGlTextDrawOptions();
			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			float screenWidth = viewport[2];
			float screenHeight = viewport[3];
			if (screenWidth <= 0F || screenHeight <= 0F)
			{
				return;
			}

			List<int> visibleOverlayIndices = GetVisibleDetectionOverlayIndicesForRendering(screenWidth, screenHeight);
			var screenOverlays = new List<DetectionScreenOverlay>();
			foreach (int overlayIndex in visibleOverlayIndices)
			{
				if (overlayIndex < 0 || overlayIndex >= _detectionOverlays.Count)
				{
					continue;
				}

				RoiImageCanvasDetectionOverlay overlay = _detectionOverlays[overlayIndex];
				if (overlay?.Bounds.IsEmpty != false || overlay.Bounds.Width <= 0 || overlay.Bounds.Height <= 0)
				{
					continue;
				}

				RectangleF screenBounds = _imageViewer.GetScreenRectFromImagePixelBounds(overlay.Bounds);
				if (screenBounds.Width < 1F || screenBounds.Height < 1F)
				{
					continue;
				}

				screenOverlays.Add(new DetectionScreenOverlay(
					screenBounds,
					string.IsNullOrWhiteSpace(overlay.Label) ? "AI" : overlay.Label,
					overlay.Color,
					overlay.IsSelected));
			}

			DrawDetectionScreenOverlays(gl, textOptions, screenOverlays);
		}

		private List<int> GetVisibleDetectionOverlayIndicesForRendering(float screenWidth, float screenHeight)
		{
			if (_detectionOverlays.Count == 0 || screenWidth <= 0F || screenHeight <= 0F)
			{
				return new List<int>();
			}

			RectangleF visibleImageBounds = GetVisibleImagePixelBounds(screenWidth, screenHeight);
			if (visibleImageBounds.Width <= 0F || visibleImageBounds.Height <= 0F)
			{
				return new List<int>();
			}

			// Detection overlays are indexed in image-pixel coordinates. Query the visible
			// image window first so rendering never walks every AI candidate each frame.
			return _detectionOverlayHitIndex.QueryBounds(visibleImageBounds);
		}

		private List<int> GetVisiblePolygonOverlayIndicesForRendering(float screenWidth, float screenHeight)
		{
			if (_polygonOverlays.Count == 0 || screenWidth <= 0F || screenHeight <= 0F)
			{
				return new List<int>();
			}

			RectangleF visibleImageBounds = GetVisibleImagePixelBounds(screenWidth, screenHeight);
			if (visibleImageBounds.Width <= 0F || visibleImageBounds.Height <= 0F)
			{
				return new List<int>();
			}

			// Polygon point projection is expensive; only convert polygons touching the viewport.
			return _polygonOverlayRenderIndex.QueryBounds(visibleImageBounds);
		}

		private List<int> GetVisibleMaskOverlayIndicesForRendering(float screenWidth, float screenHeight)
		{
			if (_maskOverlays.Count == 0 || screenWidth <= 0F || screenHeight <= 0F)
			{
				return new List<int>();
			}

			RectangleF visibleImageBounds = GetVisibleImagePixelBounds(screenWidth, screenHeight);
			if (visibleImageBounds.Width <= 0F || visibleImageBounds.Height <= 0F)
			{
				return new List<int>();
			}

			// Mask texture upload is the heaviest overlay path, so render only visible masks.
			return _maskOverlayRenderIndex.QueryBounds(visibleImageBounds);
		}

		private RectangleF GetVisibleImagePixelBounds(float screenWidth, float screenHeight)
		{
			PointF topLeft = ConvertCanvasPointToImagePoint(_imageViewer.GetCurrentRobotPos(0, 0));
			PointF topRight = ConvertCanvasPointToImagePoint(_imageViewer.GetCurrentRobotPos((int)Math.Ceiling(screenWidth), 0));
			PointF bottomLeft = ConvertCanvasPointToImagePoint(_imageViewer.GetCurrentRobotPos(0, (int)Math.Ceiling(screenHeight)));
			PointF bottomRight = ConvertCanvasPointToImagePoint(_imageViewer.GetCurrentRobotPos((int)Math.Ceiling(screenWidth), (int)Math.Ceiling(screenHeight)));

			float left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
			float right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
			float top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
			float bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
			left = Math.Max(0F, left);
			top = Math.Max(0F, top);
			right = Math.Min(_imageSize.Width, right);
			bottom = Math.Min(_imageSize.Height, bottom);
			if (right <= left || bottom <= top)
			{
				return RectangleF.Empty;
			}

			return RectangleF.FromLTRB(left, top, right, bottom);
		}

		private void DrawMaskOverlays(OpenGL gl)
		{
			if (_maskOverlays.Count == 0 || _imageSize.IsEmpty)
			{
				ReleaseStaleMaskOverlayTextures(gl, null);
				return;
			}

			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			float screenWidth = viewport[2];
			float screenHeight = viewport[3];
			if (screenWidth <= 0F || screenHeight <= 0F)
			{
				return;
			}

			List<int> visibleOverlayIndices = GetVisibleMaskOverlayIndicesForRendering(screenWidth, screenHeight);
			if (visibleOverlayIndices.Count == 0)
			{
				ReleaseStaleMaskOverlayTextures(gl, _activeMaskOverlayKeys);
				return;
			}

			gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_COLOR_BUFFER_BIT);
			try
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION);
				gl.PushMatrix();
				try
				{
					gl.LoadIdentity();
					gl.Ortho2D(0, screenWidth, screenHeight, 0);
					gl.MatrixMode(OpenGL.GL_MODELVIEW);
					gl.PushMatrix();
					try
					{
						gl.LoadIdentity();
						gl.Enable(OpenGL.GL_TEXTURE_2D);
						gl.Disable(OpenGL.GL_DEPTH_TEST);
						gl.Enable(OpenGL.GL_BLEND);
						gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
						gl.Color(1F, 1F, 1F, 1F);

						foreach (int overlayIndex in visibleOverlayIndices)
						{
							if (overlayIndex < 0 || overlayIndex >= _maskOverlays.Count)
							{
								continue;
							}

							RoiImageCanvasMaskOverlay overlay = _maskOverlays[overlayIndex];
							if (overlay?.IsValid != true)
							{
								continue;
							}

							Rectangle maskBounds = ClipMaskOverlayBounds(overlay);
							if (maskBounds.Width <= 0 || maskBounds.Height <= 0)
							{
								continue;
							}

							string key = overlay.Key;
							if (!_maskOverlayTextures.TryGetValue(key, out MaskOverlayTextureCache cache))
							{
								cache = new MaskOverlayTextureCache();
								_maskOverlayTextures[key] = cache;
							}

							if (!UpdateMaskOverlayTexture(gl, overlay, maskBounds, cache))
							{
								continue;
							}

							RectangleF screenBounds = _imageViewer.GetScreenRectFromImagePixelBounds(
								new RectangleF(maskBounds.X, maskBounds.Y, maskBounds.Width, maskBounds.Height));
							screenBounds = SnapDetectionBounds(NormalizeScreenRect(screenBounds));
							if (!IntersectsViewport(screenBounds, screenWidth, screenHeight))
							{
								continue;
							}

							DrawTexturedScreenRect(gl, cache.TextureId, screenBounds);
						}
					}
					finally
					{
						gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
						gl.MatrixMode(OpenGL.GL_MODELVIEW);
						gl.PopMatrix();
					}
				}
				finally
				{
					gl.MatrixMode(OpenGL.GL_PROJECTION);
					gl.PopMatrix();
				}
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_MODELVIEW);
				gl.PopAttrib();
			}

			ReleaseStaleMaskOverlayTextures(gl, _activeMaskOverlayKeys);
		}

		private void DrawSelectedMaskOverlayMarkers(OpenGL gl)
		{
			if (_maskOverlays.Count == 0 || _imageSize.IsEmpty)
			{
				return;
			}

			OpenGlTextDrawOptions textOptions = _imageViewer.GetOpenGlTextDrawOptions();
			var screenOverlays = new List<DetectionScreenOverlay>();
			foreach (int overlayIndex in _selectedMaskOverlayIndices)
			{
				if (overlayIndex < 0 || overlayIndex >= _maskOverlays.Count)
				{
					continue;
				}

				RoiImageCanvasMaskOverlay overlay = _maskOverlays[overlayIndex];
				if (overlay?.IsValid != true || !overlay.IsSelected)
				{
					continue;
				}

				Rectangle maskBounds = ClipMaskOverlayBounds(overlay);
				if (maskBounds.Width <= 0 || maskBounds.Height <= 0)
				{
					continue;
				}

				RectangleF screenBounds = _imageViewer.GetScreenRectFromImagePixelBounds(
					new RectangleF(maskBounds.X, maskBounds.Y, maskBounds.Width, maskBounds.Height));
				screenOverlays.Add(new DetectionScreenOverlay(
					screenBounds,
					string.IsNullOrWhiteSpace(overlay.Label) ? "MASK" : overlay.Label,
					overlay.Color,
					isSelected: true));
			}

			DrawDetectionScreenOverlays(gl, textOptions, screenOverlays);
		}

		private static Rectangle ClipMaskOverlayBounds(RoiImageCanvasMaskOverlay overlay)
		{
			Rectangle maskExtent = new Rectangle(0, 0, overlay.MaskSize.Width, overlay.MaskSize.Height);
			return Rectangle.Intersect(overlay.Bounds, maskExtent);
		}

		private static RectangleF GetPolygonImageBounds(IReadOnlyList<System.Drawing.Point> imagePoints)
		{
			if (imagePoints == null || imagePoints.Count == 0)
			{
				return RectangleF.Empty;
			}

			int left = imagePoints.Min(point => point.X);
			int right = imagePoints.Max(point => point.X);
			int top = imagePoints.Min(point => point.Y);
			int bottom = imagePoints.Max(point => point.Y);
			return RectangleF.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
		}

		private bool UpdateMaskOverlayTexture(
			OpenGL gl,
			RoiImageCanvasMaskOverlay overlay,
			Rectangle bounds,
			MaskOverlayTextureCache cache)
		{
			IntPtr renderContext = gl.RenderContextProvider.RenderContextHandle;
			byte opacity = (byte)Math.Max(1, Math.Min(255, (int)Math.Round(overlay.Opacity * 255F)));
			int colorArgb = overlay.Color.ToArgb();
			bool canReuseTexture = cache.TextureId != 0
				&& cache.RenderContext == renderContext
				&& cache.Width == bounds.Width
				&& cache.Height == bounds.Height
				&& cache.Left == bounds.Left
				&& cache.Top == bounds.Top
				&& cache.ColorArgb == colorArgb
				&& cache.Opacity == opacity;
			Rectangle dirtyBounds = Rectangle.Intersect(overlay.DirtyBounds, bounds);
			bool canPartialUpload = canReuseTexture
				&& cache.RenderVersion != overlay.RenderVersion
				&& !dirtyBounds.IsEmpty;
			bool needsUpload = cache.TextureId == 0
				|| cache.RenderContext != renderContext
				|| cache.Width != bounds.Width
				|| cache.Height != bounds.Height
				|| cache.Left != bounds.Left
				|| cache.Top != bounds.Top
				|| cache.RenderVersion != overlay.RenderVersion
				|| cache.ColorArgb != colorArgb
				|| cache.Opacity != opacity;

			if (!needsUpload)
			{
				overlay.NotifyDirtyBoundsUploaded();
				return true;
			}

			if (canPartialUpload && UpdateMaskOverlayTextureRegion(gl, overlay, dirtyBounds, bounds, opacity, cache))
			{
				cache.RenderVersion = overlay.RenderVersion;
				overlay.NotifyDirtyBoundsUploaded();
				return true;
			}

			if (cache.TextureId != 0 && cache.RenderContext != renderContext)
			{
				cache.TextureId = 0;
			}

			if (cache.TextureId == 0)
			{
				uint[] textures = new uint[1];
				gl.GenTextures(1, textures);
				cache.TextureId = textures[0];
				if (cache.TextureId == 0)
				{
					return false;
				}
			}

			int pixelCount = bounds.Width * bounds.Height;
			int byteCount = pixelCount * 4;
			if (cache.Pixels == null || cache.Pixels.Length != byteCount)
			{
				cache.Pixels = new byte[byteCount];
			}
			else
			{
				Array.Clear(cache.Pixels, 0, cache.Pixels.Length);
			}

			BuildMaskOverlayPixels(overlay, bounds, opacity, cache.Pixels);

			GCHandle handle = GCHandle.Alloc(cache.Pixels, GCHandleType.Pinned);
			try
			{
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, cache.TextureId);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexImage2D(
					OpenGL.GL_TEXTURE_2D,
					0,
					OpenGL.GL_RGBA,
					bounds.Width,
					bounds.Height,
					0,
					OpenGL.GL_RGBA,
					OpenGL.GL_UNSIGNED_BYTE,
					handle.AddrOfPinnedObject());
			}
			finally
			{
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}

			cache.RenderContext = renderContext;
			cache.Width = bounds.Width;
			cache.Height = bounds.Height;
			cache.Left = bounds.Left;
			cache.Top = bounds.Top;
			cache.RenderVersion = overlay.RenderVersion;
			cache.ColorArgb = colorArgb;
			cache.Opacity = opacity;
			overlay.NotifyDirtyBoundsUploaded();
			return true;
		}

		private bool UpdateMaskOverlayTextureRegion(
			OpenGL gl,
			RoiImageCanvasMaskOverlay overlay,
			Rectangle dirtyBounds,
			Rectangle textureBounds,
			byte opacity,
			MaskOverlayTextureCache cache)
		{
			if (dirtyBounds.Width <= 0 || dirtyBounds.Height <= 0 || cache.TextureId == 0)
			{
				return false;
			}

			int byteCount = dirtyBounds.Width * dirtyBounds.Height * 4;
			if (cache.DirtyPixels == null || cache.DirtyPixels.Length != byteCount)
			{
				cache.DirtyPixels = new byte[byteCount];
			}
			else
			{
				Array.Clear(cache.DirtyPixels, 0, cache.DirtyPixels.Length);
			}

			BuildMaskOverlayPixels(overlay, dirtyBounds, opacity, cache.DirtyPixels);

			GCHandle handle = GCHandle.Alloc(cache.DirtyPixels, GCHandleType.Pinned);
			try
			{
				int xOffset = dirtyBounds.Left - textureBounds.Left;
				int yOffset = textureBounds.Bottom - dirtyBounds.Bottom;
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, cache.TextureId);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
				OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl.glTexSubImage2D(
					OpenGL.GL_TEXTURE_2D,
					0,
					xOffset,
					yOffset,
					dirtyBounds.Width,
					dirtyBounds.Height,
					OpenGL.GL_RGBA,
					OpenGL.GL_UNSIGNED_BYTE,
					handle.AddrOfPinnedObject());
			}
			finally
			{
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}

			return true;
		}

		private static void BuildMaskOverlayPixels(RoiImageCanvasMaskOverlay overlay, Rectangle bounds, byte opacity, byte[] pixels)
		{
			System.Drawing.Color color = overlay.Color.IsEmpty
				? System.Drawing.Color.FromArgb(80, 180, 255)
				: overlay.Color;
			byte red = color.R;
			byte green = color.G;
			byte blue = color.B;
			int maskStride = overlay.MaskSize.Width;
			for (int y = 0; y < bounds.Height; y++)
			{
				int sourceY = bounds.Bottom - 1 - y;
				int maskOffset = (sourceY * maskStride) + bounds.Left;
				int textureOffset = y * bounds.Width * 4;
				for (int x = 0; x < bounds.Width; x++)
				{
					if (overlay.MaskData[maskOffset + x] == 0)
					{
						continue;
					}

					int pixelOffset = textureOffset + (x * 4);
					pixels[pixelOffset] = red;
					pixels[pixelOffset + 1] = green;
					pixels[pixelOffset + 2] = blue;
					pixels[pixelOffset + 3] = opacity;
				}
			}
		}

		private static void DrawTexturedScreenRect(OpenGL gl, uint textureId, RectangleF bounds)
		{
			if (textureId == 0)
			{
				return;
			}

			gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);
			gl.Begin(OpenGL.GL_QUADS);
			gl.TexCoord(0.0f, 1.0f); gl.Vertex(bounds.Left, bounds.Top);
			gl.TexCoord(1.0f, 1.0f); gl.Vertex(bounds.Right, bounds.Top);
			gl.TexCoord(1.0f, 0.0f); gl.Vertex(bounds.Right, bounds.Bottom);
			gl.TexCoord(0.0f, 0.0f); gl.Vertex(bounds.Left, bounds.Bottom);
			gl.End();
		}

		private void ReleaseStaleMaskOverlayTextures(OpenGL gl, ISet<string> liveKeys)
		{
			foreach (string key in _maskOverlayTextures.Keys.ToList())
			{
				if (liveKeys != null && liveKeys.Contains(key))
				{
					continue;
				}

				DeleteMaskOverlayTexture(gl, _maskOverlayTextures[key]);
				_maskOverlayTextures.Remove(key);
			}
		}

		private static void DeleteMaskOverlayTexture(OpenGL gl, MaskOverlayTextureCache cache)
		{
			if (cache == null || cache.TextureId == 0)
			{
				return;
			}

			uint[] textures = { cache.TextureId };
			gl.DeleteTextures(1, textures);
			cache.TextureId = 0;
		}

		private bool DrawMaskStrokePreviewLayer(OpenGL gl)
		{
			if (_imageSize.IsEmpty || _maskStrokePreviewImageSize.IsEmpty)
			{
				ReleaseMaskStrokePreviewLayer(gl);
				return false;
			}

			bool hasPreviewWork = _pendingMaskStrokePreviewCommands.Count > 0
				|| _maskStrokePreviewLayer.ClearRequested
				|| _maskStrokePreviewLayer.BaseRefreshRequested
				|| (_maskStrokePreviewVisible && _maskStrokePreviewLayer.TextureId == 0);
			if (!hasPreviewWork && !_maskStrokePreviewVisible)
			{
				return false;
			}

			ProcessMaskStrokePreviewCommands(gl);
			if (!_maskStrokePreviewVisible || _maskStrokePreviewLayer.TextureId == 0)
			{
				return false;
			}

			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			float screenWidth = viewport[2];
			float screenHeight = viewport[3];
			if (screenWidth <= 0F || screenHeight <= 0F)
			{
				return false;
			}

			RectangleF screenBounds = NormalizeScreenRect(_imageViewer.GetScreenRectFromImagePixelBounds(
				new RectangleF(0F, 0F, _maskStrokePreviewImageSize.Width, _maskStrokePreviewImageSize.Height)));
			if (!IntersectsViewport(screenBounds, screenWidth, screenHeight))
			{
				return false;
			}

			gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_COLOR_BUFFER_BIT);
			try
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION);
				gl.PushMatrix();
				try
				{
					gl.LoadIdentity();
					gl.Ortho2D(0, screenWidth, screenHeight, 0);
					gl.MatrixMode(OpenGL.GL_MODELVIEW);
					gl.PushMatrix();
					try
					{
						gl.LoadIdentity();
						gl.Enable(OpenGL.GL_TEXTURE_2D);
						gl.Disable(OpenGL.GL_DEPTH_TEST);
						gl.Enable(OpenGL.GL_BLEND);
						gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
						gl.Color(1F, 1F, 1F, 1F);
						DrawTexturedScreenRect(gl, _maskStrokePreviewLayer.TextureId, screenBounds);
					}
					finally
					{
						gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
						gl.MatrixMode(OpenGL.GL_MODELVIEW);
						gl.PopMatrix();
					}
				}
				finally
				{
					gl.MatrixMode(OpenGL.GL_PROJECTION);
					gl.PopMatrix();
				}
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_MODELVIEW);
				gl.PopAttrib();
			}

			return true;
		}

		private void ProcessMaskStrokePreviewCommands(OpenGL gl)
		{
			bool needsLayer = _maskStrokePreviewVisible
				|| _pendingMaskStrokePreviewCommands.Count > 0
				|| _maskStrokePreviewLayer.ClearRequested
				|| _maskStrokePreviewLayer.BaseRefreshRequested;
			if (!needsLayer)
			{
				return;
			}

			if (!_maskStrokePreviewLayer.Ensure(gl, _maskStrokePreviewImageSize))
			{
				_pendingMaskStrokePreviewCommands.Clear();
				_maskStrokePreviewVisible = false;
				return;
			}

			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_COLOR_BUFFER_BIT);
			try
			{
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _maskStrokePreviewLayer.FrameBufferId);
				gl.Viewport(0, 0, _maskStrokePreviewImageSize.Width, _maskStrokePreviewImageSize.Height);
				gl.MatrixMode(OpenGL.GL_PROJECTION);
				gl.PushMatrix();
				try
				{
					gl.LoadIdentity();
					gl.Ortho2D(0, _maskStrokePreviewImageSize.Width, _maskStrokePreviewImageSize.Height, 0);
					gl.MatrixMode(OpenGL.GL_MODELVIEW);
					gl.PushMatrix();
					try
					{
						gl.LoadIdentity();
						if (_maskStrokePreviewLayer.ClearRequested)
						{
							gl.ClearColor(0F, 0F, 0F, 0F);
							gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
							_maskStrokePreviewLayer.ClearRequested = false;
						}

						if (_maskStrokePreviewLayer.BaseRefreshRequested)
						{
							RenderMaskStrokePreviewBase(gl);
							_maskStrokePreviewLayer.BaseRefreshRequested = false;
						}

						while (_pendingMaskStrokePreviewCommands.Count > 0)
						{
							MaskStrokePreviewCommand command = _pendingMaskStrokePreviewCommands.Dequeue();
							gl.Disable(OpenGL.GL_TEXTURE_2D);
							gl.Disable(OpenGL.GL_DEPTH_TEST);
							gl.Disable(OpenGL.GL_BLEND);
							System.Drawing.Color stampColor = command.IsEraser
								? System.Drawing.Color.FromArgb(0, 0, 0, 0)
								: command.Color;
							DrawBrushStampPreview(gl, command.Centers, command.Radius, stampColor);
						}
					}
					finally
					{
						gl.MatrixMode(OpenGL.GL_MODELVIEW);
						gl.PopMatrix();
					}
				}
				finally
				{
					gl.MatrixMode(OpenGL.GL_PROJECTION);
					gl.PopMatrix();
				}
			}
			finally
			{
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
				gl.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
				gl.MatrixMode(OpenGL.GL_MODELVIEW);
				gl.PopAttrib();
			}
		}

		private void RenderMaskStrokePreviewBase(OpenGL gl)
		{
			gl.ClearColor(0F, 0F, 0F, 0F);
			gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
			if (_maskOverlays.Count == 0)
			{
				return;
			}

			gl.Enable(OpenGL.GL_TEXTURE_2D);
			gl.Disable(OpenGL.GL_DEPTH_TEST);
			gl.Enable(OpenGL.GL_BLEND);
			gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
			gl.Color(1F, 1F, 1F, 1F);
			foreach (RoiImageCanvasMaskOverlay overlay in _maskOverlays)
			{
				if (overlay?.IsValid != true)
				{
					continue;
				}

				Rectangle maskBounds = ClipMaskOverlayBounds(overlay);
				if (maskBounds.Width <= 0 || maskBounds.Height <= 0)
				{
					continue;
				}

				string key = overlay.Key;
				if (!_maskOverlayTextures.TryGetValue(key, out MaskOverlayTextureCache cache))
				{
					cache = new MaskOverlayTextureCache();
					_maskOverlayTextures[key] = cache;
				}

				if (!UpdateMaskOverlayTexture(gl, overlay, maskBounds, cache))
				{
					continue;
				}

				DrawTexturedScreenRect(
					gl,
					cache.TextureId,
					new RectangleF(maskBounds.X, maskBounds.Y, maskBounds.Width, maskBounds.Height));
			}

			ReleaseStaleMaskOverlayTextures(gl, _activeMaskOverlayKeys);
		}

		private void ReleaseMaskStrokePreviewLayer(OpenGL gl)
		{
			_pendingMaskStrokePreviewCommands.Clear();
			_maskStrokePreviewVisible = false;
			_maskStrokePreviewImageSize = System.Drawing.Size.Empty;
			_maskStrokePreviewLayer.Release(gl);
		}

		private static void DrawBrushStampPreview(OpenGL gl, IReadOnlyList<System.Drawing.Point> centers, int radius, System.Drawing.Color color)
		{
			if (gl == null || centers == null || centers.Count == 0)
			{
				return;
			}

			BrushPreviewStamp stamp = BrushPreviewStampCache.GetOrAdd(Math.Max(1, radius), CreateBrushPreviewStamp);
			SetGlColor(gl, color);
			gl.Begin(OpenGL.GL_QUADS);
			try
			{
				foreach (System.Drawing.Point center in centers)
				{
					foreach (BrushPreviewStampRow row in stamp.Rows)
					{
						float left = center.X + row.LeftDeltaX;
						float top = center.Y + row.DeltaY;
						float right = center.X + row.RightDeltaX + 1F;
						float bottom = top + 1F;
						gl.Vertex(left, top);
						gl.Vertex(right, top);
						gl.Vertex(right, bottom);
						gl.Vertex(left, bottom);
					}
				}
			}
			finally
			{
				gl.End();
			}
		}

		private static BrushPreviewStamp CreateBrushPreviewStamp(int radius)
		{
			int safeRadius = Math.Max(1, radius);
			int maxOffset = safeRadius + 1;
			double radiusSquared = safeRadius * safeRadius;
			var rows = new List<BrushPreviewStampRow>((maxOffset * 2) + 1);
			for (int dy = -maxOffset; dy <= maxOffset; dy++)
			{
				double yDistance = GetPreviewPixelCellDistanceFromBrushCenter(dy);
				double remaining = radiusSquared - (yDistance * yDistance);
				if (remaining < 0D)
				{
					continue;
				}

				int maxDx = Math.Min(maxOffset, (int)Math.Floor(Math.Sqrt(remaining) + 0.5D));
				rows.Add(new BrushPreviewStampRow(dy, -maxDx, maxDx));
			}

			return new BrushPreviewStamp(rows.ToArray());
		}

		private static double GetPreviewPixelCellDistanceFromBrushCenter(int offset)
			=> Math.Max(0D, Math.Abs(offset) - 0.5D);
		private void DrawBrushCursorPreview(OpenGL gl)
		{
			if (!_brushCursorPreviewVisible || _imageSize.IsEmpty || _brushCursorPreviewRadius <= 0)
			{
				return;
			}

			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			float screenWidth = viewport[2];
			float screenHeight = viewport[3];
			if (screenWidth <= 0F || screenHeight <= 0F)
			{
				return;
			}

			RectangleF imageBounds = new RectangleF(
				_brushCursorPreviewImagePoint.X - _brushCursorPreviewRadius,
				_brushCursorPreviewImagePoint.Y - _brushCursorPreviewRadius,
				_brushCursorPreviewRadius * 2F,
				_brushCursorPreviewRadius * 2F);
			RectangleF screenBounds = NormalizeScreenRect(_imageViewer.GetScreenRectFromImagePixelBounds(imageBounds));
			if (!IntersectsViewport(screenBounds, screenWidth, screenHeight))
			{
				return;
			}

			System.Drawing.Color baseColor = _brushCursorPreviewColor.IsEmpty
				? System.Drawing.Color.FromArgb(80, 180, 255)
				: _brushCursorPreviewColor;
			var fill = System.Drawing.Color.FromArgb(_brushCursorPreviewIsEraser ? 28 : 42, baseColor.R, baseColor.G, baseColor.B);
			var outline = System.Drawing.Color.FromArgb(235, baseColor.R, baseColor.G, baseColor.B);
			var halo = System.Drawing.Color.FromArgb(135, 0, 0, 0);
			var center = new PointF(
				(float)Math.Round((screenBounds.Left + screenBounds.Right) * 0.5F),
				(float)Math.Round((screenBounds.Top + screenBounds.Bottom) * 0.5F));
			float radiusX = Math.Max(2F, Math.Abs(screenBounds.Width) * 0.5F);
			float radiusY = Math.Max(2F, Math.Abs(screenBounds.Height) * 0.5F);

			gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_LINE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_COLOR_BUFFER_BIT);
			try
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION);
				gl.PushMatrix();
				try
				{
					gl.LoadIdentity();
					gl.Ortho2D(0, screenWidth, screenHeight, 0);
					gl.MatrixMode(OpenGL.GL_MODELVIEW);
					gl.PushMatrix();
					try
					{
						gl.LoadIdentity();
						gl.Disable(OpenGL.GL_TEXTURE_2D);
						gl.Disable(OpenGL.GL_DEPTH_TEST);
						gl.Enable(OpenGL.GL_BLEND);
						gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
						DrawFilledScreenEllipse(gl, center, radiusX, radiusY, fill);
						DrawScreenEllipse(gl, center, radiusX + 1.5F, radiusY + 1.5F, 4F, halo);
						DrawScreenEllipse(gl, center, radiusX, radiusY, 2F, outline);
						DrawHorizontalScreenStrip(gl, center.X - Math.Min(7F, radiusX), center.X + Math.Min(7F, radiusX), center.Y, 1F, outline);
						DrawVerticalScreenStrip(gl, center.X, center.Y - Math.Min(7F, radiusY), center.Y + Math.Min(7F, radiusY), 1F, outline);
					}
					finally
					{
						gl.MatrixMode(OpenGL.GL_MODELVIEW);
						gl.PopMatrix();
					}
				}
				finally
				{
					gl.MatrixMode(OpenGL.GL_PROJECTION);
					gl.PopMatrix();
				}
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_MODELVIEW);
				gl.PopAttrib();
			}
		}

		private void DrawPolygonOverlays(OpenGL gl)
		{
			if (_polygonOverlays.Count == 0 || _imageSize.IsEmpty)
			{
				return;
			}

			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			float screenWidth = viewport[2];
			float screenHeight = viewport[3];
			if (screenWidth <= 0F || screenHeight <= 0F)
			{
				return;
			}

			OpenGlTextDrawOptions textOptions = _imageViewer.GetOpenGlTextDrawOptions();
			List<int> visibleOverlayIndices = GetVisiblePolygonOverlayIndicesForRendering(screenWidth, screenHeight);
			if (visibleOverlayIndices.Count == 0)
			{
				return;
			}

			var labels = new List<DetectionScreenLabel>();
			gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_LINE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_COLOR_BUFFER_BIT);
			try
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION);
				gl.PushMatrix();
				try
				{
					gl.LoadIdentity();
					gl.Ortho2D(0, screenWidth, screenHeight, 0);
					gl.MatrixMode(OpenGL.GL_MODELVIEW);
					gl.PushMatrix();
					try
					{
						gl.LoadIdentity();
						gl.Disable(OpenGL.GL_TEXTURE_2D);
						gl.Disable(OpenGL.GL_DEPTH_TEST);
						gl.Enable(OpenGL.GL_BLEND);
						gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

						foreach (int overlayIndex in visibleOverlayIndices)
						{
							if (overlayIndex < 0 || overlayIndex >= _polygonOverlays.Count)
							{
								continue;
							}

							RoiImageCanvasPolygonOverlay overlay = _polygonOverlays[overlayIndex];
							if (overlay?.ImagePoints == null || overlay.ImagePoints.Count == 0)
							{
								continue;
							}

							List<PointF> screenPoints = overlay.ImagePoints
								.Select(GetScreenPointFromImagePixelPoint)
								.Where(IsFiniteScreenPoint)
								.ToList();
							if (screenPoints.Count == 0)
							{
								continue;
							}

							System.Drawing.Color color = overlay.Color.IsEmpty
								? System.Drawing.Color.FromArgb(80, 180, 255)
								: overlay.Color;
							bool isClosed = overlay.IsClosed && screenPoints.Count >= 3;
							bool isDraft = overlay.IsDraft;

							if (isClosed)
							{
								DrawFilledScreenPolygon(gl, screenPoints, System.Drawing.Color.FromArgb(isDraft ? 26 : 38, color.R, color.G, color.B));
							}

							if (screenPoints.Count >= 2)
							{
								DrawScreenPolyline(gl, screenPoints, isClosed, isDraft ? 5F : 6F, System.Drawing.Color.FromArgb(80, 0, 0, 0));
								DrawScreenPolyline(gl, screenPoints, isClosed, isDraft ? 2F : 3F, System.Drawing.Color.FromArgb(isDraft ? 220 : 245, color.R, color.G, color.B));
							}

							DrawPolygonPointMarkers(gl, screenPoints, color, isDraft, overlay.IsSelected, overlay.SelectedPointIndex);
							if (!string.IsNullOrWhiteSpace(overlay.Label))
							{
								DetectionScreenLabel label = DrawPolygonBadge(gl, screenPoints[0], overlay.Label, color, isDraft, screenWidth, screenHeight);
								labels.Add(label);
							}
						}
					}
					finally
					{
						gl.MatrixMode(OpenGL.GL_MODELVIEW);
						gl.PopMatrix();
					}
				}
				finally
				{
					gl.MatrixMode(OpenGL.GL_PROJECTION);
					gl.PopMatrix();
				}
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_MODELVIEW);
				gl.PopAttrib();
			}

			foreach (DetectionScreenLabel label in labels)
			{
				OpenGlDrawing.DrawTextAt(
					gl,
					textOptions.FontBitmapEntries,
					label.Text,
					label.TextPosition.X,
					label.TextPosition.Y,
					label.FontSize,
					System.Drawing.Color.White,
					originTop: true);
			}
		}

		private PointF GetScreenPointFromImagePixelPoint(System.Drawing.Point imagePoint)
		{
			RectangleF screenBounds = _imageViewer.GetScreenRectFromImagePixelBounds(
				new RectangleF(imagePoint.X, imagePoint.Y, 0F, 0F));
			return new PointF(
				(float)Math.Round(screenBounds.Left),
				(float)Math.Round(screenBounds.Top));
		}

		private static bool IsFiniteScreenPoint(PointF point)
		{
			return !float.IsNaN(point.X)
				&& !float.IsInfinity(point.X)
				&& !float.IsNaN(point.Y)
				&& !float.IsInfinity(point.Y);
		}

		private static void DrawFilledScreenPolygon(OpenGL gl, IReadOnlyList<PointF> points, System.Drawing.Color color)
		{
			if (points == null || points.Count < 3)
			{
				return;
			}

			SetGlColor(gl, color);
			gl.Begin(OpenGL.GL_TRIANGLE_FAN);
			foreach (PointF point in points)
			{
				gl.Vertex(point.X, point.Y);
			}
			gl.End();
		}

		private static void DrawScreenPolyline(OpenGL gl, IReadOnlyList<PointF> points, bool closed, float lineWidth, System.Drawing.Color color)
		{
			if (points == null || points.Count < 2)
			{
				return;
			}

			SetGlColor(gl, color);
			gl.LineWidth(Math.Max(1F, lineWidth));
			gl.Begin(closed ? OpenGL.GL_LINE_LOOP : OpenGL.GL_LINE_STRIP);
			foreach (PointF point in points)
			{
				gl.Vertex(point.X, point.Y);
			}
			gl.End();
		}

		private static void DrawPolygonPointMarkers(OpenGL gl, IReadOnlyList<PointF> points, System.Drawing.Color color, bool isDraft, bool isSelected = false, int selectedPointIndex = -1)
		{
			for (int i = 0; i < points.Count; i++)
			{
				bool isSelectedPoint = isSelected && i == selectedPointIndex;
				float size = isSelectedPoint ? 12F : (i == 0 ? 8F : 7F);
				float half = size * 0.5F;
				PointF point = points[i];
				var halo = RectangleF.FromLTRB(
					(float)Math.Round(point.X - half - 1F),
					(float)Math.Round(point.Y - half - 1F),
					(float)Math.Round(point.X + half + 1F),
					(float)Math.Round(point.Y + half + 1F));
				var fill = RectangleF.FromLTRB(
					(float)Math.Round(point.X - half),
					(float)Math.Round(point.Y - half),
					(float)Math.Round(point.X + half),
					(float)Math.Round(point.Y + half));
				DrawFilledScreenRect(gl, halo, System.Drawing.Color.FromArgb(isSelectedPoint ? 190 : 130, 0, 0, 0));
				DrawFilledScreenRect(gl, fill, isSelectedPoint
					? System.Drawing.Color.FromArgb(255, 255, 214, 10)
					: System.Drawing.Color.FromArgb(isDraft ? 230 : 248, color.R, color.G, color.B));
				if (isSelectedPoint)
				{
					DrawScreenRectangle(gl, fill, 1.5F, System.Drawing.Color.FromArgb(255, color.R, color.G, color.B));
				}
			}
		}

		private static DetectionScreenLabel DrawPolygonBadge(
			OpenGL gl,
			PointF anchor,
			string label,
			System.Drawing.Color color,
			bool isDraft,
			float screenWidth,
			float screenHeight)
		{
			string displayText = label.Trim();
			if (displayText.Length > 20)
			{
				displayText = displayText.Substring(0, 20);
			}

			float badgeWidth = Math.Min(150F, Math.Max(58F, displayText.Length * 7.2F + 18F));
			float badgeHeight = 20F;
			float left = Clamp(anchor.X + 8F, 4F, Math.Max(4F, screenWidth - badgeWidth - 4F));
			float top = Clamp(anchor.Y - badgeHeight - 8F, 4F, Math.Max(4F, screenHeight - badgeHeight - 4F));
			var badge = SnapDetectionBounds(new RectangleF(left, top, badgeWidth, badgeHeight));
			DrawFilledScreenRect(gl, badge, System.Drawing.Color.FromArgb(isDraft ? 184 : 206, 18, 18, 18));
			DrawFilledScreenRect(gl, new RectangleF(badge.Left, badge.Top, 4F, badge.Height), System.Drawing.Color.FromArgb(235, color.R, color.G, color.B));
			DrawScreenRectangle(gl, badge, 1F, System.Drawing.Color.FromArgb(190, color.R, color.G, color.B));
			return new DetectionScreenLabel(displayText, new PointF(badge.Left + 9F, badge.Top + 14.2F), 11);
		}

		private static void DrawDetectionScreenOverlays(OpenGL gl, OpenGlTextDrawOptions textOptions, IReadOnlyList<DetectionScreenOverlay> overlays)
		{
			if (overlays.Count == 0)
			{
				return;
			}

			int[] viewport = new int[4];
			gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
			float screenWidth = viewport[2];
			float screenHeight = viewport[3];
			if (screenWidth <= 0F || screenHeight <= 0F)
			{
				return;
			}

			var labels = new List<DetectionScreenLabel>();
			var occupiedBadgeBounds = new List<RectangleF>();
			gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_LINE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_COLOR_BUFFER_BIT);
			try
			{
				gl.MatrixMode(OpenGL.GL_PROJECTION);
				gl.PushMatrix();
				try
				{
					gl.LoadIdentity();
					gl.Ortho2D(0, screenWidth, screenHeight, 0);
					gl.MatrixMode(OpenGL.GL_MODELVIEW);
					gl.PushMatrix();
					try
					{
						gl.LoadIdentity();

						gl.Disable(OpenGL.GL_TEXTURE_2D);
						gl.Disable(OpenGL.GL_DEPTH_TEST);
						gl.Enable(OpenGL.GL_BLEND);
						gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
						gl.Disable(OpenGL.GL_LINE_SMOOTH);

						foreach (DetectionScreenOverlay overlay in overlays)
						{
							RectangleF bounds = NormalizeScreenRect(overlay.Bounds);
							if (bounds.Width < 1F || bounds.Height < 1F || !IntersectsViewport(bounds, screenWidth, screenHeight))
							{
								continue;
							}

							DetectionScreenLabel label = DrawDetectionScreenMarker(gl, bounds, overlay.Label, overlay.Color, overlay.IsSelected, screenWidth, screenHeight, occupiedBadgeBounds);
							labels.Add(label);
						}
					}
					finally
					{
						gl.MatrixMode(OpenGL.GL_MODELVIEW);
						gl.PopMatrix();
					}
				}
				finally
				{
					gl.MatrixMode(OpenGL.GL_PROJECTION);
					gl.PopMatrix();
				}
			}
			finally
			{
				gl.MatrixMode(OpenGL.GL_MODELVIEW);
				gl.PopAttrib();
			}

			foreach (DetectionScreenLabel label in labels)
			{
				OpenGlDrawing.DrawTextAt(
					gl,
					textOptions.FontBitmapEntries,
					label.Text,
					label.TextPosition.X,
					label.TextPosition.Y,
					label.FontSize,
					System.Drawing.Color.White,
					originTop: true);
			}
		}

		private static DetectionScreenLabel DrawDetectionScreenMarker(
			OpenGL gl,
			RectangleF bounds,
			string label,
			System.Drawing.Color color,
			bool isSelected,
			float screenWidth,
			float screenHeight,
			IList<RectangleF> occupiedBadgeBounds)
		{
			float minSide = Math.Min(bounds.Width, bounds.Height);
			float corner = isSelected
				? Math.Max(10F, Math.Min(minSide * 0.22F, 24F))
				: Math.Max(8F, Math.Min(minSide * 0.18F, 18F));
			float lineWidth = isSelected ? 3F : 2F;
			float haloWidth = lineWidth + 2F;
			var haloColor = System.Drawing.Color.FromArgb(55, 0, 0, 0);
			var fillColor = System.Drawing.Color.FromArgb(isSelected ? 14 : 7, color.R, color.G, color.B);
			var outlineColor = System.Drawing.Color.FromArgb(isSelected ? 150 : 92, color.R, color.G, color.B);
			var lineColor = System.Drawing.Color.FromArgb(isSelected ? 248 : 225, color.R, color.G, color.B);

			bounds = SnapDetectionBounds(bounds);
			DrawFilledScreenRect(gl, bounds, fillColor);
			DrawScreenRectangle(gl, bounds, 1F, outlineColor);
			DrawScreenCorners(gl, bounds, corner, haloWidth, haloColor);
			DrawScreenCorners(gl, bounds, corner, lineWidth, lineColor);

			string displayText = isSelected ? BuildSelectedDetectionLabel(label) : BuildCompactDetectionLabel(label);
			float badgeHeight = isSelected ? 21F : 17F;
			float badgeWidth = isSelected
				? Math.Min(150F, Math.Max(82F, displayText.Length * 7.4F + 20F))
				: Math.Min(70F, Math.Max(34F, displayText.Length * 6.8F + 14F));
			RectangleF badgeBounds = PlaceDetectionBadge(bounds, badgeWidth, badgeHeight, screenWidth, screenHeight, occupiedBadgeBounds);
			occupiedBadgeBounds?.Add(InflateDetectionBadge(badgeBounds));
			badgeBounds = SnapDetectionBounds(badgeBounds);
			DrawFilledScreenRect(gl, badgeBounds, System.Drawing.Color.FromArgb(isSelected ? 204 : 172, 18, 18, 18));
			DrawFilledScreenRect(gl, new RectangleF(badgeBounds.Left, badgeBounds.Top, isSelected ? 4F : 3F, badgeBounds.Height), System.Drawing.Color.FromArgb(230, color.R, color.G, color.B));
			DrawScreenRectangle(gl, badgeBounds, 1F, System.Drawing.Color.FromArgb(175, color.R, color.G, color.B));

			return new DetectionScreenLabel(
				displayText,
				new PointF(badgeBounds.Left + (isSelected ? 10F : 8F), badgeBounds.Top + (isSelected ? 15.2F : 12.8F)),
				isSelected ? 12 : 10);
		}

		private static RectangleF PlaceDetectionBadge(
			RectangleF bounds,
			float badgeWidth,
			float badgeHeight,
			float screenWidth,
			float screenHeight,
			IList<RectangleF> occupiedBadgeBounds)
		{
			const float viewportPadding = 6F;
			const float badgeGap = 8F;
			float left = Math.Max(viewportPadding, Math.Min(bounds.Left, screenWidth - badgeWidth - viewportPadding));
			float maxTop = Math.Max(viewportPadding, screenHeight - badgeHeight - viewportPadding);
			float top = bounds.Top - badgeHeight - badgeGap;
			if (top < viewportPadding)
			{
				top = bounds.Top + badgeGap;
			}

			RectangleF preferred = new RectangleF(left, Clamp(top, viewportPadding, maxTop), badgeWidth, badgeHeight);
			if (!IntersectsOccupiedBadge(preferred, occupiedBadgeBounds))
			{
				return preferred;
			}

			float step = badgeHeight + 4F;
			top = Clamp(bounds.Top + badgeGap, viewportPadding, maxTop);
			for (int attempt = 0; attempt < 24; attempt++)
			{
				RectangleF candidate = new RectangleF(left, top, badgeWidth, badgeHeight);
				if (!IntersectsOccupiedBadge(candidate, occupiedBadgeBounds))
				{
					return candidate;
				}

				top += step;
				if (top > maxTop)
				{
					top = viewportPadding;
				}
			}

			return preferred;
		}

		private static bool IntersectsViewport(RectangleF bounds, float screenWidth, float screenHeight)
		{
			return screenWidth > 0F
				&& screenHeight > 0F
				&& bounds.Right > 0F
				&& bounds.Bottom > 0F
				&& bounds.Left < screenWidth
				&& bounds.Top < screenHeight;
		}

		private static bool IntersectsOccupiedBadge(RectangleF badgeBounds, IEnumerable<RectangleF> occupiedBadgeBounds)
		{
			if (occupiedBadgeBounds == null)
			{
				return false;
			}

			RectangleF padded = InflateDetectionBadge(badgeBounds);
			return occupiedBadgeBounds.Any(occupied => occupied.IntersectsWith(padded));
		}

		private static RectangleF InflateDetectionBadge(RectangleF bounds)
		{
			RectangleF inflated = bounds;
			inflated.Inflate(3F, 2F);
			return inflated;
		}

		private static float Clamp(float value, float min, float max)
		{
			if (max < min)
			{
				return min;
			}

			return Math.Max(min, Math.Min(value, max));
		}

		private static RectangleF NormalizeScreenRect(RectangleF rect)
		{
			return RectangleF.FromLTRB(
				Math.Min(rect.Left, rect.Right),
				Math.Min(rect.Top, rect.Bottom),
				Math.Max(rect.Left, rect.Right),
				Math.Max(rect.Top, rect.Bottom));
		}

		private static RectangleF SnapDetectionBounds(RectangleF bounds)
		{
			float left = (float)Math.Round(bounds.Left);
			float top = (float)Math.Round(bounds.Top);
			float right = (float)Math.Round(bounds.Right);
			float bottom = (float)Math.Round(bounds.Bottom);
			if (right <= left)
			{
				right = left + 1F;
			}

			if (bottom <= top)
			{
				bottom = top + 1F;
			}

			return RectangleF.FromLTRB(left, top, right, bottom);
		}

		private static void DrawScreenCorners(OpenGL gl, RectangleF bounds, float corner, float lineWidth, System.Drawing.Color color)
		{
			float left = bounds.Left;
			float right = bounds.Right;
			float top = bounds.Top;
			float bottom = bounds.Bottom;
			DrawHorizontalScreenStrip(gl, left, Math.Min(left + corner, right), top, lineWidth, color);
			DrawVerticalScreenStrip(gl, left, top, Math.Min(top + corner, bottom), lineWidth, color);
			DrawHorizontalScreenStrip(gl, Math.Max(right - corner, left), right, top, lineWidth, color);
			DrawVerticalScreenStrip(gl, right, top, Math.Min(top + corner, bottom), lineWidth, color);
			DrawHorizontalScreenStrip(gl, left, Math.Min(left + corner, right), bottom, lineWidth, color);
			DrawVerticalScreenStrip(gl, left, Math.Max(bottom - corner, top), bottom, lineWidth, color);
			DrawHorizontalScreenStrip(gl, Math.Max(right - corner, left), right, bottom, lineWidth, color);
			DrawVerticalScreenStrip(gl, right, Math.Max(bottom - corner, top), bottom, lineWidth, color);
		}

		private static void DrawScreenRectangle(OpenGL gl, RectangleF bounds, float lineWidth, System.Drawing.Color color)
		{
			DrawHorizontalScreenStrip(gl, bounds.Left, bounds.Right, bounds.Top, lineWidth, color);
			DrawHorizontalScreenStrip(gl, bounds.Left, bounds.Right, bounds.Bottom, lineWidth, color);
			DrawVerticalScreenStrip(gl, bounds.Left, bounds.Top, bounds.Bottom, lineWidth, color);
			DrawVerticalScreenStrip(gl, bounds.Right, bounds.Top, bounds.Bottom, lineWidth, color);
		}

		private static void DrawHorizontalScreenStrip(OpenGL gl, float left, float right, float centerY, float lineWidth, System.Drawing.Color color)
		{
			float thickness = Math.Max(1F, (float)Math.Round(lineWidth));
			float half = thickness * 0.5F;
			DrawFilledScreenRect(gl, RectangleF.FromLTRB(
				(float)Math.Round(Math.Min(left, right)),
				(float)Math.Round(centerY - half),
				(float)Math.Round(Math.Max(left, right)),
				(float)Math.Round(centerY + half)),
				color);
		}

		private static void DrawVerticalScreenStrip(OpenGL gl, float centerX, float top, float bottom, float lineWidth, System.Drawing.Color color)
		{
			float thickness = Math.Max(1F, (float)Math.Round(lineWidth));
			float half = thickness * 0.5F;
			DrawFilledScreenRect(gl, RectangleF.FromLTRB(
				(float)Math.Round(centerX - half),
				(float)Math.Round(Math.Min(top, bottom)),
				(float)Math.Round(centerX + half),
				(float)Math.Round(Math.Max(top, bottom))),
				color);
		}

		private static void DrawFilledScreenRect(OpenGL gl, RectangleF bounds, System.Drawing.Color color)
		{
			SetGlColor(gl, color);
			gl.Begin(OpenGL.GL_QUADS);
			gl.Vertex(bounds.Left, bounds.Top);
			gl.Vertex(bounds.Right, bounds.Top);
			gl.Vertex(bounds.Right, bounds.Bottom);
			gl.Vertex(bounds.Left, bounds.Bottom);
			gl.End();
		}

		private static void DrawFilledScreenEllipse(OpenGL gl, PointF center, float radiusX, float radiusY, System.Drawing.Color color)
		{
			SetGlColor(gl, color);
			gl.Begin(OpenGL.GL_TRIANGLE_FAN);
			gl.Vertex(center.X, center.Y);
			for (int i = 0; i <= 72; i++)
			{
				double angle = (Math.PI * 2D * i) / 72D;
				gl.Vertex(
					center.X + (Math.Cos(angle) * radiusX),
					center.Y + (Math.Sin(angle) * radiusY));
			}
			gl.End();
		}

		private static void DrawScreenEllipse(OpenGL gl, PointF center, float radiusX, float radiusY, float lineWidth, System.Drawing.Color color)
		{
			SetGlColor(gl, color);
			gl.LineWidth(Math.Max(1F, lineWidth));
			gl.Begin(OpenGL.GL_LINE_LOOP);
			for (int i = 0; i < 96; i++)
			{
				double angle = (Math.PI * 2D * i) / 96D;
				gl.Vertex(
					center.X + (Math.Cos(angle) * radiusX),
					center.Y + (Math.Sin(angle) * radiusY));
			}
			gl.End();
		}

		private static void SetGlColor(OpenGL gl, System.Drawing.Color color)
		{
			gl.Color(color.R / 255F, color.G / 255F, color.B / 255F, color.A / 255F);
		}

		private static string BuildCompactDetectionLabel(string label)
		{
			if (string.IsNullOrWhiteSpace(label))
			{
				return "AI";
			}

			string[] parts = label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 3 && string.Equals(parts[0], "AI", StringComparison.OrdinalIgnoreCase))
			{
				return "#" + parts[1] + " " + parts[2];
			}

			return parts.Length >= 2 && string.Equals(parts[0], "AI", StringComparison.OrdinalIgnoreCase)
				? "#" + parts[1]
				: label.Substring(0, Math.Min(label.Length, 2));
		}

		private static string BuildSelectedDetectionLabel(string label)
		{
			if (string.IsNullOrWhiteSpace(label))
			{
				return "AI";
			}

			string[] parts = label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 4 && string.Equals(parts[0], "AI", StringComparison.OrdinalIgnoreCase))
			{
				return "#" + parts[1] + " " + parts[2] + " " + parts[3];
			}

			string trimmed = label.Trim();
			return trimmed.Length > 18 ? trimmed.Substring(0, 18) : trimmed;
		}

		private sealed class DetectionScreenOverlay
		{
			public DetectionScreenOverlay(RectangleF bounds, string label, System.Drawing.Color color, bool isSelected)
			{
				Bounds = bounds;
				Label = label;
				Color = color;
				IsSelected = isSelected;
			}

			public RectangleF Bounds { get; }
			public string Label { get; }
			public System.Drawing.Color Color { get; }
			public bool IsSelected { get; }
		}

		private sealed class DetectionScreenLabel
		{
			public DetectionScreenLabel(string text, PointF textPosition, int fontSize)
			{
				Text = text;
				TextPosition = textPosition;
				FontSize = fontSize;
			}

			public string Text { get; }
			public PointF TextPosition { get; }
			public int FontSize { get; }
		}

		private sealed class BrushPreviewStamp
		{
			public BrushPreviewStamp(BrushPreviewStampRow[] rows)
			{
				Rows = rows ?? Array.Empty<BrushPreviewStampRow>();
			}

			public BrushPreviewStampRow[] Rows { get; }
		}

		private readonly struct BrushPreviewStampRow
		{
			public BrushPreviewStampRow(int deltaY, int leftDeltaX, int rightDeltaX)
			{
				DeltaY = deltaY;
				LeftDeltaX = leftDeltaX;
				RightDeltaX = rightDeltaX;
			}

			public int DeltaY { get; }

			public int LeftDeltaX { get; }

			public int RightDeltaX { get; }
		}

		private sealed class MaskOverlayTextureCache
		{
			public uint TextureId { get; set; }
			public IntPtr RenderContext { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
			public int Left { get; set; }
			public int Top { get; set; }
			public int RenderVersion { get; set; }
			public int ColorArgb { get; set; }
			public byte Opacity { get; set; }
			public byte[] Pixels { get; set; }
			public byte[] DirtyPixels { get; set; }
		}

		private sealed class MaskStrokePreviewCommand
		{
			public MaskStrokePreviewCommand(IReadOnlyList<System.Drawing.Point> centers, int radius, System.Drawing.Color color, bool isEraser)
			{
				Centers = centers ?? Array.Empty<System.Drawing.Point>();
				Radius = Math.Max(1, radius);
				Color = color;
				IsEraser = isEraser;
			}

			public IReadOnlyList<System.Drawing.Point> Centers { get; }
			public int Radius { get; }
			public System.Drawing.Color Color { get; }
			public bool IsEraser { get; }
		}

		private sealed class MaskStrokePreviewLayer
		{
			public uint TextureId { get; private set; }
			public uint FrameBufferId { get; private set; }
			public IntPtr RenderContext { get; private set; }
			public int Width { get; private set; }
			public int Height { get; private set; }
			public bool ClearRequested { get; set; } = true;
			public bool BaseRefreshRequested { get; set; } = true;
			public bool HasTexture => TextureId != 0 || FrameBufferId != 0;

			public bool Ensure(OpenGL gl, System.Drawing.Size size)
			{
				if (gl == null || size.Width <= 0 || size.Height <= 0)
				{
					return false;
				}

				IntPtr renderContext = gl.RenderContextProvider.RenderContextHandle;
				if (TextureId != 0
					&& (RenderContext != renderContext || Width != size.Width || Height != size.Height))
				{
					Release(gl);
				}

				if (TextureId != 0 && FrameBufferId != 0)
				{
					return true;
				}

				uint[] textureIds = new uint[1];
				gl.GenTextures(1, textureIds);
				TextureId = textureIds[0];
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, TextureId);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexImage2D(
					OpenGL.GL_TEXTURE_2D,
					0,
					OpenGL.GL_RGBA,
					size.Width,
					size.Height,
					0,
					OpenGL.GL_RGBA,
					OpenGL.GL_UNSIGNED_BYTE,
					IntPtr.Zero);

				uint[] frameBufferIds = new uint[1];
				gl.GenFramebuffersEXT(1, frameBufferIds);
				FrameBufferId = frameBufferIds[0];
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, FrameBufferId);
				gl.FramebufferTexture2DEXT(
					OpenGL.GL_FRAMEBUFFER_EXT,
					OpenGL.GL_COLOR_ATTACHMENT0_EXT,
					OpenGL.GL_TEXTURE_2D,
					TextureId,
					0);
				uint status = gl.CheckFramebufferStatusEXT(OpenGL.GL_FRAMEBUFFER_EXT);
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
				if (status != OpenGL.GL_FRAMEBUFFER_COMPLETE_EXT)
				{
					Release(gl);
					return false;
				}

				RenderContext = renderContext;
				Width = size.Width;
				Height = size.Height;
				ClearRequested = false;
				BaseRefreshRequested = true;
				return true;
			}

			public void RequestBaseRefresh()
			{
				BaseRefreshRequested = true;
				ClearRequested = false;
			}

			public void RequestClear()
			{
				ClearRequested = true;
				BaseRefreshRequested = false;
			}

			public void Release(OpenGL gl)
			{
				if (gl != null && FrameBufferId != 0)
				{
					uint[] frameBufferIds = { FrameBufferId };
					gl.DeleteFramebuffersEXT(1, frameBufferIds);
				}

				if (gl != null && TextureId != 0)
				{
					uint[] textureIds = { TextureId };
					gl.DeleteTextures(1, textureIds);
				}

				TextureId = 0;
				FrameBufferId = 0;
				RenderContext = IntPtr.Zero;
				Width = 0;
				Height = 0;
				ClearRequested = true;
				BaseRefreshRequested = false;
			}
		}
		private sealed class OverlayRenderSpatialIndex
		{
			private const int CellSize = 64;
			private const int MaxCellsPerOverlay = 4096;

			private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
			private readonly List<int> _largeOverlays = new List<int>();
			private readonly List<RectangleF> _bounds = new List<RectangleF>();

			public void Rebuild(IReadOnlyList<RectangleF> overlayBounds)
			{
				_cells.Clear();
				_largeOverlays.Clear();
				_bounds.Clear();
				if (overlayBounds == null)
				{
					return;
				}

				for (int index = 0; index < overlayBounds.Count; index++)
				{
					RectangleF bounds = overlayBounds[index];
					_bounds.Add(bounds);
					if (bounds.Width <= 0F || bounds.Height <= 0F)
					{
						continue;
					}

					Add(index, bounds);
				}
			}

			public List<int> QueryBounds(RectangleF bounds)
			{
				var result = new List<int>();
				if (bounds.Width <= 0F || bounds.Height <= 0F)
				{
					return result;
				}

				var seen = new HashSet<int>();
				int left = ToCell(bounds.Left);
				int right = ToCell(bounds.Right);
				int top = ToCell(bounds.Top);
				int bottom = ToCell(bounds.Bottom);
				for (int cellX = left; cellX <= right; cellX++)
				{
					for (int cellY = top; cellY <= bottom; cellY++)
					{
						long key = MakeKey(cellX, cellY);
						if (!_cells.TryGetValue(key, out List<int> bucket))
						{
							continue;
						}

						AddIntersectingIndices(bucket, bounds, seen, result);
					}
				}

				AddIntersectingIndices(_largeOverlays, bounds, seen, result);
				return result;
			}

			private void Add(int index, RectangleF bounds)
			{
				int left = ToCell(bounds.Left);
				int right = ToCell(bounds.Right);
				int top = ToCell(bounds.Top);
				int bottom = ToCell(bounds.Bottom);
				int cellCount = Math.Max(1, right - left + 1) * Math.Max(1, bottom - top + 1);
				if (cellCount > MaxCellsPerOverlay)
				{
					_largeOverlays.Add(index);
					return;
				}

				for (int cellX = left; cellX <= right; cellX++)
				{
					for (int cellY = top; cellY <= bottom; cellY++)
					{
						long key = MakeKey(cellX, cellY);
						if (!_cells.TryGetValue(key, out List<int> bucket))
						{
							bucket = new List<int>();
							_cells[key] = bucket;
						}

						bucket.Add(index);
					}
				}
			}

			private void AddIntersectingIndices(IEnumerable<int> candidates, RectangleF queryBounds, ISet<int> seen, ICollection<int> result)
			{
				foreach (int index in candidates)
				{
					if (index < 0 || index >= _bounds.Count || !seen.Add(index))
					{
						continue;
					}

					if (_bounds[index].IntersectsWith(queryBounds))
					{
						result.Add(index);
					}
				}
			}

			private static int ToCell(float value)
			{
				return (int)Math.Floor(value / CellSize);
			}

			private static long MakeKey(int x, int y)
			{
				return ((long)x << 32) ^ (uint)y;
			}
		}

		private sealed class DetectionOverlaySpatialIndex
		{
			private const int CellSize = 64;
			private const int MaxCellsPerOverlay = 4096;

			private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
			private readonly List<int> _largeOverlays = new List<int>();

			public void Rebuild(IReadOnlyList<RoiImageCanvasDetectionOverlay> overlays)
			{
				_cells.Clear();
				_largeOverlays.Clear();
				if (overlays == null)
				{
					return;
				}

				for (int index = 0; index < overlays.Count; index++)
				{
					RoiImageCanvasDetectionOverlay overlay = overlays[index];
					if (overlay?.Bounds.IsEmpty != false || overlay.Bounds.Width <= 0 || overlay.Bounds.Height <= 0)
					{
						continue;
					}

					Add(index, overlay.Bounds);
				}
			}

			public List<int> QueryPoint(PointF imagePoint)
			{
				var result = new List<int>();
				long key = MakeKey(ToCell(imagePoint.X), ToCell(imagePoint.Y));
				if (_cells.TryGetValue(key, out List<int> bucket))
				{
					result.AddRange(bucket);
				}

				if (_largeOverlays.Count > 0)
				{
					result.AddRange(_largeOverlays);
				}

				return result;
			}

			public int FindTopmostContainingPoint(PointF imagePoint, IReadOnlyList<RoiImageCanvasDetectionOverlay> overlays)
			{
				if (overlays == null || overlays.Count == 0)
				{
					return -1;
				}

				int bestIndex = -1;
				long key = MakeKey(ToCell(imagePoint.X), ToCell(imagePoint.Y));
				if (_cells.TryGetValue(key, out List<int> bucket))
				{
					bestIndex = FindTopmostContainingPoint(bucket, imagePoint, overlays, bestIndex);
				}

				if (_largeOverlays.Count > 0)
				{
					bestIndex = FindTopmostContainingPoint(_largeOverlays, imagePoint, overlays, bestIndex);
				}

				return bestIndex;
			}

			private static int FindTopmostContainingPoint(IReadOnlyList<int> candidates, PointF imagePoint, IReadOnlyList<RoiImageCanvasDetectionOverlay> overlays, int bestIndex)
			{
				for (int i = candidates.Count - 1; i >= 0; i--)
				{
					int overlayIndex = candidates[i];
					if (overlayIndex <= bestIndex)
					{
						break;
					}

					if (overlayIndex >= overlays.Count)
					{
						continue;
					}

					RoiImageCanvasDetectionOverlay overlay = overlays[overlayIndex];
					if (overlay?.Bounds.IsEmpty != false)
					{
						continue;
					}

					if (ContainsInclusive(overlay.Bounds, imagePoint))
					{
						return overlayIndex;
					}
				}

				return bestIndex;
			}

			public List<int> QueryBounds(RectangleF bounds)
			{
				var result = new List<int>();
				if (bounds.Width <= 0F || bounds.Height <= 0F)
				{
					return result;
				}

				var seen = new HashSet<int>();
				int left = ToCell(bounds.Left);
				int right = ToCell(bounds.Right);
				int top = ToCell(bounds.Top);
				int bottom = ToCell(bounds.Bottom);
				for (int cellX = left; cellX <= right; cellX++)
				{
					for (int cellY = top; cellY <= bottom; cellY++)
					{
						long key = MakeKey(cellX, cellY);
						if (!_cells.TryGetValue(key, out List<int> bucket))
						{
							continue;
						}

						foreach (int index in bucket)
						{
							if (seen.Add(index))
							{
								result.Add(index);
							}
						}
					}
				}

				foreach (int index in _largeOverlays)
				{
					if (seen.Add(index))
					{
						result.Add(index);
					}
				}

				return result;
			}

			private void Add(int index, Rectangle bounds)
			{
				int left = ToCell(bounds.Left);
				int right = ToCell(bounds.Right);
				int top = ToCell(bounds.Top);
				int bottom = ToCell(bounds.Bottom);
				int cellCount = Math.Max(1, right - left + 1) * Math.Max(1, bottom - top + 1);
				if (cellCount > MaxCellsPerOverlay)
				{
					_largeOverlays.Add(index);
					return;
				}

				for (int cellX = left; cellX <= right; cellX++)
				{
					for (int cellY = top; cellY <= bottom; cellY++)
					{
						long key = MakeKey(cellX, cellY);
						if (!_cells.TryGetValue(key, out List<int> bucket))
						{
							bucket = new List<int>();
							_cells[key] = bucket;
						}

						bucket.Add(index);
					}
				}
			}

			private static int ToCell(float value)
			{
				return (int)Math.Floor(value / CellSize);
			}

			private static long MakeKey(int x, int y)
			{
				return ((long)x << 32) ^ (uint)y;
			}
		}

		private void OnMouseDown(object sender, CanvasMouseEventArgs e)
		{
			OpenGLControl openGLControl = (OpenGLControl)sender;
			_lastPixelPropertyUpdateTicks = 0;
			_mouseDownOnExistingRoi = false;
			_mouseDownOnDetectionOverlay = false;
			System.Drawing.PointF currentRobotyPos = _imageViewer.GetCurrentRobotPos(e.X, e.Y);
			if (IsImagePointInputMode)
			{
				RaiseImagePointClicked(e, currentRobotyPos);
				return;
			}

			switch (e.CanvasButton)
			{
				case CanvasPointerButton.Left:
					_mouseDownCanvasPos = new System.Drawing.Point(e.X, e.Y);
					if (TryBeginDrawingModeMouseDown(openGLControl, e, currentRobotyPos))
					{
						break;
					}
					if (TryBeginPanModeMouseDown(openGLControl, currentRobotyPos))
					{
						break;
					}

					var (hitRect, _) = RoiInteractionMouseDown.FindOverlayAtPosition(_imageViewer, currentRobotyPos);
					_mouseDownOnExistingRoi = hitRect != null && !hitRect.IsEmpty();
					if (!_mouseDownOnExistingRoi && TryGetDetectionOverlayIndexAtPosition(currentRobotyPos, out int detectionOverlayIndex))
					{
						_mouseDownOnExistingRoi = true;
						_mouseDownOnDetectionOverlay = true;
						_selectedRect = new CanvasRect<float>();
						_drawingRect = new CanvasRect<float>();
						DetectionOverlayClicked(this, detectionOverlayIndex);
						return;
					}

					RoiInteractionMouseDown.InitializeMouseDownState(ImageViewer, ref _selectedRect, openGLControl, e);
					switch (_imageViewer.GetViewMode())
					{
						case CanvasInteractionMode.Drawing:
							_drawingRect = new CanvasRect<float>();
							if (_mouseDownOnExistingRoi && _selectedRect != null && !_selectedRect.IsEmpty())
							{
								_selectedRect.IsEditing = true;
							}
							else
							{
								_drawingRect.ShapeKind = DrawingShapeKind;
								_drawingRect.IsEditing = true;
							}
							break;
						case CanvasInteractionMode.Edit:
						case CanvasInteractionMode.Move:
							_drawingRect = new CanvasRect<float>();
							if (_selectedRect != null) { _selectedRect.IsEditing = true; }

							break;
						case CanvasInteractionMode.Drag:
						case CanvasInteractionMode.Measure:
							_drawingRect = new CanvasRect<float>();
							if (_selectedRect != null) { _selectedRect.IsEditing = false; }
							break;
					}
					break;
				case CanvasPointerButton.Right:
					ClearSelection();
					OnMouseRightClick(CanvasContextMenuMode.Default);
					break;
			}
		}

		private bool TryBeginDrawingModeMouseDown(OpenGLControl openGLControl, CanvasMouseEventArgs e, System.Drawing.PointF currentRobotyPos)
		{
			if (_imageViewer.GetViewMode() != CanvasInteractionMode.Drawing)
			{
				return false;
			}

			if (!IsTeachingMode && !IsAddRoiArrayMode)
			{
				return false;
			}

			if (TryGetDetectionOverlayIndexAtPosition(currentRobotyPos, out int detectionOverlayIndex))
			{
				_mouseDownOnExistingRoi = true;
				_mouseDownOnDetectionOverlay = true;
				_selectedRect = new CanvasRect<float>();
				_drawingRect = new CanvasRect<float>();
				DetectionOverlayClicked(this, detectionOverlayIndex);
				return true;
			}

			_imageViewer.PreMousePos = currentRobotyPos;
			_imageViewer.PostMousePos = currentRobotyPos;

			// Existing ROI hit wins over starting a new box so selection handles appear on mouse-down.
			var (hitRect, _) = RoiInteractionMouseDown.FindOverlayAtPosition(_imageViewer, currentRobotyPos);
			if (hitRect != null && !hitRect.IsEmpty())
			{
				_mouseDownOnExistingRoi = true;
				_drawingRect = new CanvasRect<float>();
				_selectedRect = hitRect;
				_selectedRect.SetEditingType(currentRobotyPos.X, currentRobotyPos.Y, _imageViewer.ZoomScale, _imageViewer.HandleSize);
				_selectedRect.IsEditing = true;
				MarkRoiDisplayChanged(_selectedRect);
				_imageViewer.SetViewMode(_selectedRect.EditingType == EditingType.Move ? CanvasInteractionMode.Move : CanvasInteractionMode.Edit);
				openGLControl.Cursor = RoiInteractionCursor.GetCursorFromType(_selectedRect, currentRobotyPos, _imageViewer.ZoomScale, _imageViewer.HandleSize);
				_imageViewer.RefreshGL();
				return true;
			}

			if (IsSelectedRoiHit(currentRobotyPos))
			{
				_mouseDownOnExistingRoi = true;
				_drawingRect = new CanvasRect<float>();
				_selectedRect.SetEditingType(currentRobotyPos.X, currentRobotyPos.Y, _imageViewer.ZoomScale, _imageViewer.HandleSize);
				_selectedRect.IsEditing = true;
				MarkRoiDisplayChanged(_selectedRect);
				_imageViewer.SetViewMode(_selectedRect.EditingType == EditingType.Move ? CanvasInteractionMode.Move : CanvasInteractionMode.Edit);
				openGLControl.Cursor = RoiInteractionCursor.GetCursorFromType(_selectedRect, currentRobotyPos, _imageViewer.ZoomScale, _imageViewer.HandleSize);
				_imageViewer.RefreshGL();
				return true;
			}

			if (_selectedRect != null)
			{
				_selectedRect.IsEditing = false;
				MarkRoiDisplayChanged(_selectedRect);
			}

			_drawingRect = new CanvasRect<float>
			{
				ShapeKind = DrawingShapeKind,
				IsEditing = true
			};
			openGLControl.Cursor = System.Windows.Forms.Cursors.Cross;
			return true;
		}

		private bool TryBeginPanModeMouseDown(OpenGLControl openGLControl, System.Drawing.PointF currentRobotyPos)
		{
			if (_imageViewer.GetViewMode() != CanvasInteractionMode.Drag)
			{
				return false;
			}

			_imageViewer.PreMousePos = currentRobotyPos;
			_imageViewer.PostMousePos = currentRobotyPos;
			_drawingRect = new CanvasRect<float>();
			if (_selectedRect != null)
			{
				_selectedRect.IsEditing = false;
				_selectedRect.IsChanged = true;
			}

			openGLControl.Cursor = System.Windows.Forms.Cursors.SizeAll;
			return true;
		}

		private void RaiseImagePointClicked(CanvasMouseEventArgs e, System.Drawing.PointF canvasPoint)
		{
			if (_imageSize.Width <= 0 || _imageSize.Height <= 0)
			{
				return;
			}

			PointF imagePointF = _imageViewer.ConvertOpenGlToImagePoint(canvasPoint);
			bool insideImage = imagePointF.X >= 0F
				&& imagePointF.Y >= 0F
				&& imagePointF.X < _imageSize.Width
				&& imagePointF.Y < _imageSize.Height;
			if (!insideImage && e.CanvasButton != CanvasPointerButton.Right)
			{
				return;
			}

			var imagePoint = new System.Drawing.Point(
				(int)Clamp((float)Math.Round(imagePointF.X), 0F, _imageSize.Width - 1),
				(int)Clamp((float)Math.Round(imagePointF.Y), 0F, _imageSize.Height - 1));
			ImagePointClicked(this, new CanvasImagePointEventArgs(
				e.CanvasButton,
				e.Clicks,
				e.X,
				e.Y,
				imagePoint,
				canvasPoint));
		}

		private void RaiseImagePointHovered(CanvasMouseEventArgs e, System.Drawing.PointF canvasPoint)
		{
			if (_imageSize.Width <= 0 || _imageSize.Height <= 0)
			{
				ClearBrushCursorPreview();
				return;
			}

			PointF imagePointF = _imageViewer.ConvertOpenGlToImagePoint(canvasPoint);
			if (imagePointF.X < 0F || imagePointF.Y < 0F || imagePointF.X >= _imageSize.Width || imagePointF.Y >= _imageSize.Height)
			{
				ClearBrushCursorPreview();
				return;
			}

			var imagePoint = new System.Drawing.Point(
				(int)Clamp((float)Math.Round(imagePointF.X), 0F, _imageSize.Width - 1),
				(int)Clamp((float)Math.Round(imagePointF.Y), 0F, _imageSize.Height - 1));
			ImagePointHovered(this, new CanvasImagePointEventArgs(
				e.CanvasButton,
				e.Clicks,
				e.X,
				e.Y,
				imagePoint,
				canvasPoint));
		}

		private void RaiseImagePointMoved(CanvasMouseEventArgs e, System.Drawing.PointF canvasPoint)
		{
			if (_imageSize.Width <= 0 || _imageSize.Height <= 0)
			{
				return;
			}

			PointF imagePointF = _imageViewer.ConvertOpenGlToImagePoint(canvasPoint);
			if (imagePointF.X < 0F || imagePointF.Y < 0F || imagePointF.X >= _imageSize.Width || imagePointF.Y >= _imageSize.Height)
			{
				return;
			}

			var imagePoint = new System.Drawing.Point(
				(int)Clamp((float)Math.Round(imagePointF.X), 0F, _imageSize.Width - 1),
				(int)Clamp((float)Math.Round(imagePointF.Y), 0F, _imageSize.Height - 1));
			ImagePointMoved(this, new CanvasImagePointEventArgs(
				e.CanvasButton,
				e.Clicks,
				e.X,
				e.Y,
				imagePoint,
				canvasPoint));
		}

		private void RaiseImagePointReleased(CanvasMouseEventArgs e, System.Drawing.PointF canvasPoint)
		{
			if (_imageSize.Width <= 0 || _imageSize.Height <= 0)
			{
				return;
			}

			PointF imagePointF = _imageViewer.ConvertOpenGlToImagePoint(canvasPoint);
			if (imagePointF.X < 0F || imagePointF.Y < 0F || imagePointF.X >= _imageSize.Width || imagePointF.Y >= _imageSize.Height)
			{
				return;
			}

			var imagePoint = new System.Drawing.Point(
				(int)Clamp((float)Math.Round(imagePointF.X), 0F, _imageSize.Width - 1),
				(int)Clamp((float)Math.Round(imagePointF.Y), 0F, _imageSize.Height - 1));
			ImagePointReleased(this, new CanvasImagePointEventArgs(
				e.CanvasButton,
				e.Clicks,
				e.X,
				e.Y,
				imagePoint,
				canvasPoint));
		}

		private void OnMouseMove(object sender, CanvasMouseEventArgs e)
		{
			OpenGLControl openGLControl = (OpenGLControl)sender;
			System.Drawing.PointF currentRobotyPos = _imageViewer.GetCurrentRobotPos(e.X, e.Y);
			if (IsImagePointInputMode)
			{
				_imageViewer.PostMousePos = currentRobotyPos;
				RaiseImagePointHovered(e, currentRobotyPos);
				if (e.CanvasButton != CanvasPointerButton.None)
				{
					RaiseImagePointMoved(e, currentRobotyPos);
				}

				UpdatePixelProperty(throttle: true);
				return;
			}

			UpdateInteractionCursor(openGLControl, e.CanvasButton, currentRobotyPos);
			_imageViewer.PostMousePos = currentRobotyPos;
			if (_mouseDownOnDetectionOverlay)
			{
				UpdatePixelProperty(throttle: true);
				return;
			}

			switch (_imageViewer.GetViewMode())
			{
				case CanvasInteractionMode.Edit:
					RoiInteractionMouseMove.ResizeRoiRect(_imageViewer, _selectedRect, currentRobotyPos, _imageSize, OnRoiEditingCompleted, notifyEditingCompleted: false);
					break;
				case CanvasInteractionMode.Move:
					RoiInteractionMouseMove.MoveOverlay(_imageViewer, _selectedRect, currentRobotyPos, _imageSize, true, OnRoiEditingCompleted, UseGroupMoveMode, notifyEditingCompleted: false);
					break;
				case CanvasInteractionMode.Drawing:
					RoiInteractionMouseMove.UpdateReactangleToOverlay(_imageViewer, _drawingRect);
					break;
				case CanvasInteractionMode.Measure:
					RoiInteractionMouseMove.UpdateMeasurement(_imageViewer, ref _measurement);
					break;
			}

			UpdatePixelProperty(throttle: true);
		}

		private void OnMouseUp(object sender, CanvasMouseEventArgs e)
		{
			if (IsImagePointInputMode)
			{
				_mouseDownOnExistingRoi = false;
				_mouseDownOnDetectionOverlay = false;
				System.Drawing.PointF currentRobotyPos = _imageViewer.GetCurrentRobotPos(e.X, e.Y);
				RaiseImagePointReleased(e, currentRobotyPos);
				return;
			}

			CanvasInteractionMode mouseUpViewMode = _imageViewer.GetViewMode();
			bool isCommittingExistingRoiEdit = mouseUpViewMode == CanvasInteractionMode.Edit || mouseUpViewMode == CanvasInteractionMode.Move;
			_imageViewer.PostMousePos = _imageViewer.GetCurrentRobotPos(e.X, e.Y);
			if (_mouseDownOnDetectionOverlay)
			{
				_selectedRect = new CanvasRect<float>();
				_drawingRect = new CanvasRect<float>();
				_mouseDownOnExistingRoi = false;
				_mouseDownOnDetectionOverlay = false;
				UpdatePixelProperty();
				ResetViewMode();
				return;
			}

			CanvasRect<float> mouseUpRect = GetActiveInteractionRect();
			// Do not clear _selectedRect here. In labeling UX, a clicked ROI stays selected
			// until the operator clicks another ROI or an empty canvas area.
			if (_drawingRect != null)
			{
				_drawingRect.IsEditing = false;
				MarkRoiDisplayChanged(_drawingRect);
			}

			bool hasMouseDrag = e.CanvasButton == CanvasPointerButton.Left
				&& HasValidMouseDrag(_mouseDownCanvasPos, new System.Drawing.Point(e.X, e.Y));
			bool hasValidLeftDrag = hasMouseDrag
				&& (_mouseDownOnExistingRoi || isCommittingExistingRoiEdit || HasValidDrawingBounds(_imageViewer.PreMousePos, _imageViewer.PostMousePos));

			if (e.CanvasButton == CanvasPointerButton.Left && !_mouseDownOnExistingRoi && !hasValidLeftDrag && _imageViewer.GetViewMode() == CanvasInteractionMode.Drawing)
			{
				_drawingRect = new CanvasRect<float>();
				var (clickedRect, _) = RoiInteractionMouseDown.FindOverlayAtPosition(_imageViewer, _imageViewer.PostMousePos);
				if (clickedRect != null && !clickedRect.IsEmpty())
				{
					_selectedRect = clickedRect;
					_selectedRect.IsEditing = true;
					MarkRoiDisplayChanged(_selectedRect);
					_mouseDownOnExistingRoi = true;
					mouseUpRect = _selectedRect;
				}
				else
				{
					mouseUpRect = _selectedRect;
				}
			}

			if (hasValidLeftDrag && !_mouseDownOnExistingRoi)
			{
				if (IsAddRoiArrayMode)
				{
					RoiInteractionMouseUp.OpenAddRoiArrayView(_imageViewer, _addRoiArrayVm, OnRoiAdded);
					// ?�성???�료?�면 ?�당 모드�?종료?�다.
					IsAddRoiArrayMode = false;
				}
				else
				{
					if (_imageViewer.GetViewMode() == CanvasInteractionMode.Drawing && ReplaceExistingRoiOnDraw)
					{
						ReplaceWindowRoisForSingleDraw();
					}

					if (_imageViewer.GetViewMode() == CanvasInteractionMode.Drawing)
					{
						bool added = DrawingShapeKind == CanvasRoiShapeKind.Ellipse
							? RoiInteractionMouseUp.AddEllipseToOverlay(_imageViewer, _imageViewer.PreMousePos, _imageViewer.PostMousePos, ref _drawingRect, OnRoiAdded)
							: RoiInteractionMouseUp.AddRectangleToOverlay(_imageViewer, _imageViewer.PreMousePos, _imageViewer.PostMousePos, ref _drawingRect, OnRoiAdded);
						if (added)
						{
							_selectedRect = _drawingRect;
							_selectedRect.IsEditing = true;
							MarkRoiDisplayChanged(_selectedRect);
							mouseUpRect = _selectedRect;
						}
						_drawingRect = new CanvasRect<float>();
					}
				}
			}
			else if (_mouseDownOnExistingRoi && _selectedRect != null && !_selectedRect.IsEmpty())
			{
				_selectedRect.IsEditing = true;
				MarkRoiDisplayChanged(_selectedRect);
				mouseUpRect = _selectedRect;
			}
			if ((_mouseDownOnExistingRoi || isCommittingExistingRoiEdit) && hasValidLeftDrag)
			{
				RefreshGroupBoundsForCommittedRoi(mouseUpRect);
				OnRoiEditingCompleted(mouseUpRect);
			}
			OnRoiMouseUp(mouseUpRect);
			UpdatePixelProperty();
			ResetViewMode();
			_mouseDownOnExistingRoi = false;
			_mouseDownOnDetectionOverlay = false;
		}

		private bool TryGetDetectionOverlayIndexAtPosition(System.Drawing.PointF canvasPoint, out int detectionOverlayIndex)
		{
			detectionOverlayIndex = -1;
			if (_detectionOverlays.Count == 0 || _imageSize.Width <= 0 || _imageSize.Height <= 0)
			{
				return false;
			}

			System.Drawing.PointF imagePoint = ConvertCanvasPointToImagePoint(canvasPoint);
			int bestOverlayIndex = _detectionOverlayHitIndex.FindTopmostContainingPoint(imagePoint, _detectionOverlays);
			if (bestOverlayIndex < 0)
			{
				return false;
			}

			RoiImageCanvasDetectionOverlay bestOverlay = _detectionOverlays[bestOverlayIndex];
			detectionOverlayIndex = bestOverlay.Index >= 0 ? bestOverlay.Index : bestOverlayIndex;
			return true;
		}

		public bool TryGetDetectionOverlayIndexAtCanvasPoint(System.Drawing.PointF canvasPoint, out int detectionOverlayIndex)
			=> TryGetDetectionOverlayIndexAtPosition(canvasPoint, out detectionOverlayIndex);

		private System.Drawing.PointF ConvertCanvasPointToImagePoint(System.Drawing.PointF canvasPoint)
		{
			if (_imageViewer.TextureAreas.Count == 0 && _imageSize.Width > 0 && _imageSize.Height > 0)
			{
				return new System.Drawing.PointF(canvasPoint.X, _imageSize.Height - canvasPoint.Y);
			}

			return _imageViewer.ConvertOpenGlToImagePoint(canvasPoint);
		}

		private static bool ContainsInclusive(System.Drawing.Rectangle bounds, System.Drawing.PointF point)
		{
			return point.X >= bounds.Left
				&& point.X <= bounds.Right
				&& point.Y >= bounds.Top
				&& point.Y <= bounds.Bottom;
		}

		private void OnKeyDown(object sender, CanvasKeyboardEventArgs e)
		{
			switch (e.Key)
			{
				case CanvasKeyboardKey.Shift:
					break;
				case CanvasKeyboardKey.Control:

					break;
				case CanvasKeyboardKey.Enter:

					break;
				case CanvasKeyboardKey.Delete:
					RemoveSelectedOverlay();
					e.Handled = true;
					break;
			}

			if (e.IsControlPressed)
			{
				switch (e.Key)
				{
					case CanvasKeyboardKey.C:
						RoiInteractionKeyDown.CopyRectangle(_selectedRect, ref _copyRoiRect);
						e.Handled = true;
						break;
					case CanvasKeyboardKey.V:
						RoiInteractionKeyDown.PasteRectangle(ImageViewer, ref _copyRoiRect, OnRoiAdded, OnRoiGrouped);
						e.Handled = true;
						break;
				}
			}
		}
		private void OnMouseWheel(object sender, CanvasMouseEventArgs e)
		{
			// Keep WPF wheel zoom on the same viewport/cache invalidation path as toolbar zoom.
			_imageViewer.ZoomAt(e.Location, e.Delta);
		}

		private void OnMouseLeave(object sender, EventArgs e)
		{
			ClearBrushCursorPreview();
		}

		private void OnMouseClicked(object sender, EventArgs e)
		{

		}

		private void OnKeyUp(object sender, CanvasKeyboardEventArgs e)
		{

		}

		private void OnMouseDoubleClicked(object sender, EventArgs e)
		{

		}

		private void OnResized(object sender, EventArgs e)
		{
			StartReshapeTimer();
		}

		private void OnLoad(object sender, EventArgs e)
		{

		}

		private CanvasRect<float> GetActiveInteractionRect()
		{
			return _imageViewer.GetViewMode() == CanvasInteractionMode.Drawing ? _drawingRect : _selectedRect;
		}

		private CanvasRect<float> GetOverlayRect()
		{
			if (_imageViewer.GetViewMode() == CanvasInteractionMode.Drawing && _drawingRect != null && !_drawingRect.IsEmpty())
			{
				return _drawingRect;
			}

			if (_selectedRect != null && !_selectedRect.IsEmpty() && _selectedRect.IsEditing)
			{
				return _selectedRect;
			}

			return null;
		}

		private CanvasRect<float> GetCursorInteractionRect(System.Drawing.PointF currentRobotyPos)
		{
			switch (_imageViewer.GetViewMode())
			{
				case CanvasInteractionMode.Edit:
				case CanvasInteractionMode.Move:
					return _selectedRect;
				case CanvasInteractionMode.Drawing:
					if (_drawingRect != null && _drawingRect.IsEditing)
					{
						return _drawingRect;
					}
					break;
			}

			// Hover cursor stays on the selected ROI only. Selection hit-testing happens on MouseDown,
			// so moving the mouse over 500K unselected ROIs does not scan spatial candidates per event.
			return GetOverlayRect();
		}

		private void UpdateInteractionCursor(OpenGLControl openGLControl, CanvasPointerButton button, System.Drawing.PointF currentRobotyPos)
		{
			switch (_imageViewer.GetViewMode())
			{
				case CanvasInteractionMode.Drag:
					openGLControl.Cursor = System.Windows.Forms.Cursors.SizeAll;
					return;
				case CanvasInteractionMode.Drawing:
					if (IsSelectedRoiHit(currentRobotyPos))
					{
						openGLControl.Cursor = RoiInteractionCursor.GetCursorFromType(_selectedRect, currentRobotyPos, _imageViewer.ZoomScale, _imageViewer.HandleSize);
					}
					else
					{
						openGLControl.Cursor = System.Windows.Forms.Cursors.Cross;
					}
					return;
				case CanvasInteractionMode.Edit:
				case CanvasInteractionMode.Move:
					openGLControl.Cursor = RoiInteractionCursor.GetCursorFromType(_selectedRect, currentRobotyPos, _imageViewer.ZoomScale, _imageViewer.HandleSize);
					return;
			}

			if (button != CanvasPointerButton.None)
			{
				openGLControl.Cursor = System.Windows.Forms.Cursors.Default;
				return;
			}

			openGLControl.Cursor = RoiInteractionCursor.GetCursorFromType(GetCursorInteractionRect(currentRobotyPos), currentRobotyPos, _imageViewer.ZoomScale, _imageViewer.HandleSize);
		}

		private bool IsSelectedRoiHit(System.Drawing.PointF currentRobotyPos)
		{
			return _selectedRect != null
				&& !_selectedRect.IsEmpty()
				&& _selectedRect.CheckHandleContainsPosition(currentRobotyPos.X, currentRobotyPos.Y, _imageViewer.ZoomScale, _imageViewer.HandleSize) != LineOverType.None;
		}

		private static void MarkRoiDisplayChanged(CanvasRect<float> canvasRect)
		{
			if (canvasRect == null) { return; }
			canvasRect.IsChanged = true;
			canvasRect.OnChanged?.Invoke();
		}

		private void ClearSelection()
		{
			if (_selectedRect != null)
			{
				_selectedRect.IsEditing = false;
				MarkRoiDisplayChanged(_selectedRect);
			}

			if (_drawingRect != null)
			{
				_drawingRect.IsEditing = false;
				MarkRoiDisplayChanged(_drawingRect);
			}

			_selectedRect = new CanvasRect<float>();
			_drawingRect = new CanvasRect<float>();
		}

		public void ClearRoiSelection()
		{
			ClearSelection();
			_imageViewer.RefreshGL();
		}

		public bool ClearDeletedRoiSelection(string uniqueId)
		{
			bool selectedMatches = IsMatchingRoi(_selectedRect, uniqueId);
			bool drawingMatches = IsMatchingRoi(_drawingRect, uniqueId);
			if (!selectedMatches && !drawingMatches)
			{
				return false;
			}

			// The overlay has already been removed from the manager. Do not call the old
			// shape callback here, or it can recompile a deleted live-selection rectangle.
			if (selectedMatches)
			{
				_selectedRect = new CanvasRect<float>();
			}

			if (drawingMatches)
			{
				_drawingRect = new CanvasRect<float>();
			}

			ResetViewMode();
			_imageViewer.RefreshGL();
			return true;
		}

		private static bool IsMatchingRoi(CanvasRect<float> rect, string uniqueId)
		{
			return rect != null
				&& !rect.IsEmpty()
				&& !string.IsNullOrWhiteSpace(uniqueId)
				&& string.Equals(rect.UniqueId, uniqueId, StringComparison.Ordinal);
		}

		private void ResetViewMode()
		{
			if (_imageViewer.GetViewMode() == CanvasInteractionMode.Drag) { _imageViewer.SetViewMode(CanvasInteractionMode.None); }
			if (_imageViewer.GetViewMode() == CanvasInteractionMode.Move) { _imageViewer.SetViewMode(CanvasInteractionMode.None); }
			if (_imageViewer.GetViewMode() == CanvasInteractionMode.Edit) { _imageViewer.SetViewMode(CanvasInteractionMode.None); }
			if (IsTeachingMode && _imageViewer.GetViewMode() == CanvasInteractionMode.None) { _imageViewer.SetViewMode(CanvasInteractionMode.Drawing); }
		}

		private static bool HasValidDrawingBounds(System.Drawing.PointF preMousePos, System.Drawing.PointF postMousePos)
		{
			return Math.Abs(postMousePos.X - preMousePos.X) > 0 && Math.Abs(postMousePos.Y - preMousePos.Y) > 0;
		}

		private static bool HasValidMouseDrag(System.Drawing.Point startPoint, System.Drawing.Point endPoint)
		{
			const int minimumDrawingPixels = 2;
			// Edge resize is a valid one-axis drag; requiring both axes drops the mouse-up commit.
			return Math.Abs(endPoint.X - startPoint.X) >= minimumDrawingPixels
				|| Math.Abs(endPoint.Y - startPoint.Y) >= minimumDrawingPixels;
		}

		private void RemoveSelectedOverlay()
		{
			OnRemoveOverlay(ref _selectedRect);
		}

		private void ClearWindowRois()
		{
			var removableIds = _imageViewer.GetVisibleUnlockedOverlays()
				.Where(x => !x.IsGroupRectangle && x.ItemType == EnumItemType.Window && x.Shape != null)
				.Select(x => x.Shape.UniqueId)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();

			foreach (string uniqueId in removableIds)
			{
				_imageViewer.DeleteOverlay(uniqueId, _imageViewer.GetLastGroup()?.GroupType ?? string.Empty);
			}
		}

		private void ReplaceWindowRoisForSingleDraw()
		{
			ClearWindowRois();
			_selectedRect = new CanvasRect<float>();
		}

		public void UpdatePixelProperty()
		{
			NotifyPixelPropertiesChanged();
		}

		private void UpdatePixelProperty(bool throttle)
		{
			// MouseMove geometry must stay immediate, but status-bar bindings are UI noise.
			// Throttling these notifications keeps ROI resize/pan responsive under heavy overlay counts.
			if (throttle && !ShouldUpdatePixelPropertiesForMouseMove())
			{
				return;
			}

			NotifyPixelPropertiesChanged();
		}

		private bool ShouldUpdatePixelPropertiesForMouseMove()
		{
			long now = Stopwatch.GetTimestamp();
			if (_lastPixelPropertyUpdateTicks != 0 && now - _lastPixelPropertyUpdateTicks < PixelPropertyUpdateIntervalTicks)
			{
				return false;
			}

			_lastPixelPropertyUpdateTicks = now;
			return true;
		}

		private void NotifyPixelPropertiesChanged()
		{
			OnPropertyChanged(nameof(GrayValue));
			OnPropertyChanged(nameof(RobotPos));
			OnPropertyChanged(nameof(ImagePos));
			OnPropertyChanged(nameof(PixelColor));
			OnPropertyChanged(nameof(HeightValue));
		}

		[Obsolete("Use UpdatePixelProperty instead.")]
		public void UpdatePiexelProperty()
		{
			UpdatePixelProperty();
		}

		public void LoadImage(Mat mat, string fileName)
		{
			//Ex. Load Image
			//LoadImageRequested(this, mat);
			CanvasImageLoader.UploadMatAsTexture(_imageViewer, mat, fileName, ref _imageSize, replaceExistingTextures: true);
		}

		public int LoadedTextureGroupCount => _imageViewer.TextureAreas.Count;

		public void ClearRois()
		{
			ClearWindowRois();
			_selectedRect = new CanvasRect<float>();
			_drawingRect = new CanvasRect<float>();
			_imageViewer.RefreshGL();
		}

		public void BeginMaskStrokePreview(System.Drawing.Size imageSize, System.Drawing.Color color, bool isEraser)
		{
			if (imageSize.Width <= 0 || imageSize.Height <= 0)
			{
				ClearMaskStrokePreview();
				return;
			}

			bool wasVisible = _maskStrokePreviewVisible;
			_maskStrokePreviewVisible = true;
			_maskStrokePreviewImageSize = imageSize;
			_maskStrokePreviewColor = NormalizeMaskStrokePreviewColor(color, isEraser);
			_maskStrokePreviewIsEraser = isEraser;
			_pendingMaskStrokePreviewCommands.Clear();
			_maskStrokePreviewLayer.RequestBaseRefresh();
			_lastMaskStrokePreviewRefreshTicks = 0;
			if (!wasVisible)
			{
				OnPropertyChanged(nameof(IsMaskStrokePreviewVisible));
			}
		}

		public void AddMaskStrokePreview(IEnumerable<System.Drawing.Point> centers, int radius, System.Drawing.Color color, bool isEraser)
		{
			if (_imageSize.IsEmpty || radius <= 0 || centers == null)
			{
				return;
			}

			if (!_maskStrokePreviewVisible)
			{
				BeginMaskStrokePreview(_imageSize, color, isEraser);
			}

			int clampedRadius = Math.Max(1, Math.Min(512, radius));
			var previewCenters = new List<System.Drawing.Point>();
			foreach (System.Drawing.Point center in centers)
			{
				if (center.X < -clampedRadius
					|| center.Y < -clampedRadius
					|| center.X >= _maskStrokePreviewImageSize.Width + clampedRadius
					|| center.Y >= _maskStrokePreviewImageSize.Height + clampedRadius)
				{
					continue;
				}

				previewCenters.Add(new System.Drawing.Point(
					Math.Max(0, Math.Min(_maskStrokePreviewImageSize.Width - 1, center.X)),
					Math.Max(0, Math.Min(_maskStrokePreviewImageSize.Height - 1, center.Y))));
			}

			if (previewCenters.Count == 0)
			{
				return;
			}

			_maskStrokePreviewColor = NormalizeMaskStrokePreviewColor(color.IsEmpty ? _maskStrokePreviewColor : color, isEraser);
			_maskStrokePreviewIsEraser = isEraser;
			System.Drawing.Color commandColor = isEraser
				? System.Drawing.Color.FromArgb(255, _maskStrokePreviewColor.R, _maskStrokePreviewColor.G, _maskStrokePreviewColor.B)
				: _maskStrokePreviewColor;
			_pendingMaskStrokePreviewCommands.Enqueue(new MaskStrokePreviewCommand(previewCenters, clampedRadius, commandColor, isEraser));
			// CPU MaskData is committed on MouseUp for undo/save correctness. The FBO holds
			// the live full-mask preview so brush/eraser MouseMove avoids CPU texture rebuilds.
			if (ShouldRefreshMaskStrokePreviewFrame())
			{
				_refreshTimer?.Stop();
				_imageViewer.RefreshGL();
			}
			else
			{
				StartDrawingTimer();
			}
		}

		public void ClearMaskStrokePreview(bool refresh = true)
		{
			if (!_maskStrokePreviewVisible
				&& _pendingMaskStrokePreviewCommands.Count == 0
				&& !_maskStrokePreviewLayer.HasTexture)
			{
				return;
			}

			bool wasVisible = _maskStrokePreviewVisible;
			_maskStrokePreviewVisible = false;
			_pendingMaskStrokePreviewCommands.Clear();
			_maskStrokePreviewLayer.RequestClear();
			_lastMaskStrokePreviewRefreshTicks = 0;
			if (refresh)
			{
				_refreshTimer?.Stop();
				_imageViewer.RefreshGL();
			}

			if (wasVisible)
			{
				OnPropertyChanged(nameof(IsMaskStrokePreviewVisible));
			}
		}

		private bool ShouldRefreshMaskStrokePreviewFrame()
		{
			long now = Stopwatch.GetTimestamp();
			if (_lastMaskStrokePreviewRefreshTicks != 0 && now - _lastMaskStrokePreviewRefreshTicks < BrushCursorPreviewRefreshIntervalTicks)
			{
				return false;
			}

			_lastMaskStrokePreviewRefreshTicks = now;
			return true;
		}

		private static System.Drawing.Color NormalizeMaskStrokePreviewColor(System.Drawing.Color color, bool isEraser)
		{
			System.Drawing.Color fallback = isEraser
				? System.Drawing.Color.FromArgb(245, 158, 11)
				: System.Drawing.Color.FromArgb(44, 210, 110);
			System.Drawing.Color baseColor = color.IsEmpty ? fallback : color;
			return System.Drawing.Color.FromArgb(isEraser ? 135 : 150, baseColor.R, baseColor.G, baseColor.B);
		}
		public void SetBrushCursorPreview(System.Drawing.Point imagePoint, int radius, System.Drawing.Color color, bool isEraser)
		{
			if (_imageSize.IsEmpty || radius <= 0)
			{
				ClearBrushCursorPreview();
				return;
			}

			var clampedPoint = new System.Drawing.Point(
				(int)Clamp(imagePoint.X, 0F, _imageSize.Width - 1),
				(int)Clamp(imagePoint.Y, 0F, _imageSize.Height - 1));
			int clampedRadius = Math.Max(1, Math.Min(512, radius));
			System.Drawing.Color previewColor = color.IsEmpty ? System.Drawing.Color.FromArgb(80, 180, 255) : color;
			bool wasVisible = _brushCursorPreviewVisible;
			bool pointChanged = _brushCursorPreviewImagePoint != clampedPoint;
			bool styleChanged = !_brushCursorPreviewVisible
				|| _brushCursorPreviewRadius != clampedRadius
				|| _brushCursorPreviewColor.ToArgb() != previewColor.ToArgb()
				|| _brushCursorPreviewIsEraser != isEraser;
			if (!pointChanged && !styleChanged)
			{
				return;
			}

			_brushCursorPreviewVisible = true;
			_brushCursorPreviewImagePoint = clampedPoint;
			_brushCursorPreviewRadius = clampedRadius;
			_brushCursorPreviewColor = previewColor;
			_brushCursorPreviewIsEraser = isEraser;

			// Brush hover is another MouseMove path. Keep the latest cursor state, but
			// repaint/notify at frame rate instead of once per raw mouse event.
			if (ShouldRefreshBrushCursorPreviewFrame())
			{
				_refreshTimer?.Stop();
				_imageViewer.RefreshGL();
				NotifyBrushCursorPreviewProperties(wasVisible, notifyPosition: true, styleChanged: styleChanged);
			}
			else
			{
				StartDrawingTimer();
				NotifyBrushCursorPreviewProperties(wasVisible, notifyPosition: false, styleChanged: styleChanged);
			}
		}

		public void ClearBrushCursorPreview()
		{
			if (!_brushCursorPreviewVisible)
			{
				return;
			}

			_brushCursorPreviewVisible = false;
			_lastBrushCursorPreviewRefreshTicks = 0;
			_refreshTimer?.Stop();
			_imageViewer.RefreshGL();
			OnPropertyChanged(nameof(IsBrushCursorPreviewVisible));
		}

		private bool ShouldRefreshBrushCursorPreviewFrame()
		{
			long now = Stopwatch.GetTimestamp();
			if (_lastBrushCursorPreviewRefreshTicks != 0 && now - _lastBrushCursorPreviewRefreshTicks < BrushCursorPreviewRefreshIntervalTicks)
			{
				return false;
			}

			_lastBrushCursorPreviewRefreshTicks = now;
			return true;
		}

		private void NotifyBrushCursorPreviewProperties(bool wasVisible, bool notifyPosition, bool styleChanged)
		{
			if (!wasVisible)
			{
				OnPropertyChanged(nameof(IsBrushCursorPreviewVisible));
			}

			if (notifyPosition)
			{
				OnPropertyChanged(nameof(BrushCursorPreviewImagePoint));
			}

			if (styleChanged)
			{
				OnPropertyChanged(nameof(BrushCursorPreviewRadius));
			}
		}

		public void SetDetectionOverlays(IEnumerable<RoiImageCanvasDetectionOverlay> overlays)
		{
			_detectionOverlays.Clear();
			if (overlays != null)
			{
				_detectionOverlays.AddRange(overlays.Where(overlay => overlay?.Bounds.IsEmpty == false));
			}

			// AI candidates are clicked by image bounds. Build a point index once here so
			// canvas clicks do not linearly scan every detection overlay.
			_detectionOverlayHitIndex.Rebuild(_detectionOverlays);
			_imageViewer.RefreshGL();
			OnPropertyChanged(nameof(DetectionOverlays));
		}

		public void SetPolygonOverlays(IEnumerable<RoiImageCanvasPolygonOverlay> overlays)
		{
			ReplacePolygonOverlays(overlays);
			_imageViewer.RefreshGL();
			OnPropertyChanged(nameof(PolygonOverlays));
		}

		public void SetMaskOverlays(IEnumerable<RoiImageCanvasMaskOverlay> overlays)
		{
			ReplaceMaskOverlays(overlays);
			_imageViewer.RefreshGL();
			OnPropertyChanged(nameof(MaskOverlays));
		}

		public void SetSegmentationOverlays(IEnumerable<RoiImageCanvasPolygonOverlay> polygonOverlays, IEnumerable<RoiImageCanvasMaskOverlay> maskOverlays)
		{
			// Committed segmentation overlays are synchronized as a batch. Brush MouseMove
			// stays on the FBO preview path and reaches this texture path only on mouse-up.
			ReplaceMaskOverlays(maskOverlays);
			ReplacePolygonOverlays(polygonOverlays);
			_imageViewer.RefreshGL();
			OnPropertyChanged(nameof(MaskOverlays));
			OnPropertyChanged(nameof(PolygonOverlays));
		}
		public bool TryUpdateMaskOverlay(RoiImageCanvasMaskOverlay overlay)
		{
			if (overlay?.IsValid != true)
			{
				return false;
			}

			int existingIndex = _maskOverlays.FindIndex(current => string.Equals(current?.Key, overlay.Key, StringComparison.Ordinal));
			if (existingIndex < 0)
			{
				return false;
			}

			Rectangle maskBounds = ClipMaskOverlayBounds(overlay);
			if (maskBounds.Width <= 0 || maskBounds.Height <= 0)
			{
				return false;
			}

			_maskOverlays[existingIndex] = overlay;
			_maskOverlayBounds[existingIndex] = maskBounds;
			RebuildSelectedMaskOverlayIndices();
			_maskOverlayRenderIndex.Rebuild(_maskOverlayBounds);
			_imageViewer.RefreshGL();
			OnPropertyChanged(nameof(MaskOverlays));
			return true;
		}
		private void ReplacePolygonOverlays(IEnumerable<RoiImageCanvasPolygonOverlay> overlays)
		{
			_polygonOverlays.Clear();
			_polygonOverlayBounds.Clear();
			if (overlays != null)
			{
				foreach (RoiImageCanvasPolygonOverlay overlay in overlays)
				{
					if (overlay?.ImagePoints == null || overlay.ImagePoints.Count == 0)
					{
						continue;
					}

					RectangleF bounds = GetPolygonImageBounds(overlay.ImagePoints);
					if (bounds.Width <= 0F || bounds.Height <= 0F)
					{
						continue;
					}

					_polygonOverlays.Add(new RoiImageCanvasPolygonOverlay(
						overlay.ImagePoints,
						overlay.Label,
						overlay.Color,
						overlay.IsClosed,
						overlay.IsDraft,
						overlay.IsSelected,
						overlay.SelectedPointIndex));
					_polygonOverlayBounds.Add(bounds);
				}
			}

			_polygonOverlayRenderIndex.Rebuild(_polygonOverlayBounds);
		}
		private void ReplaceMaskOverlays(IEnumerable<RoiImageCanvasMaskOverlay> overlays)
		{
			_maskOverlays.Clear();
			_maskOverlayBounds.Clear();
			_selectedMaskOverlayIndices.Clear();
			_activeMaskOverlayKeys.Clear();
			if (overlays != null)
			{
				foreach (RoiImageCanvasMaskOverlay overlay in overlays)
				{
					AddMaskOverlayIfRenderable(overlay);
				}
			}

			_maskOverlayRenderIndex.Rebuild(_maskOverlayBounds);
		}

		private bool AddMaskOverlayIfRenderable(RoiImageCanvasMaskOverlay overlay)
		{
			if (overlay?.IsValid != true)
			{
				return false;
			}

			Rectangle maskBounds = ClipMaskOverlayBounds(overlay);
			if (maskBounds.Width <= 0 || maskBounds.Height <= 0)
			{
				return false;
			}

			int overlayIndex = _maskOverlays.Count;
			_maskOverlays.Add(overlay);
			_maskOverlayBounds.Add(maskBounds);
			_activeMaskOverlayKeys.Add(overlay.Key);
			if (overlay.IsSelected)
			{
				_selectedMaskOverlayIndices.Add(overlayIndex);
			}

			return true;
		}

		private void RebuildSelectedMaskOverlayIndices()
		{
			_selectedMaskOverlayIndices.Clear();
			for (int overlayIndex = 0; overlayIndex < _maskOverlays.Count; overlayIndex++)
			{
				if (_maskOverlays[overlayIndex]?.IsSelected == true)
				{
					_selectedMaskOverlayIndices.Add(overlayIndex);
				}
			}
		}
		public CanvasRect<float> AddInitialRoi(System.Drawing.Rectangle roi, CanvasRoiShapeKind shapeKind = CanvasRoiShapeKind.Rectangle)
		{
			if (roi.IsEmpty || roi.Width <= 0 || roi.Height <= 0) { return null; }

			CanvasOverlayItem parentOverlay = _imageViewer.GetLastGroup();
			if (parentOverlay == null) { return null; }

			int canvasTop = _imageSize.Height > 0 ? _imageSize.Height - roi.Top : roi.Top + roi.Height;
			int canvasBottom = _imageSize.Height > 0 ? _imageSize.Height - roi.Bottom : roi.Top;

			CanvasRect<float> rect = new CanvasRect<float>(roi.Left, canvasTop, roi.Right, canvasBottom)
			{
				UniqueId = Guid.NewGuid().ToString(),
				ShapeKind = shapeKind,
				IsFill = shapeKind == CanvasRoiShapeKind.Ellipse
			};

			_imageViewer.AddOverlay(parentOverlay.GroupType, parentOverlay.GroupType, rect, rect.UniqueId, parentOverlay.InspWindowType, EnumItemType.Window);
			_selectedRect = rect;
			_drawingRect = new CanvasRect<float>();
			_imageViewer.RefreshGL();
			return rect;
		}

		#region EventHandler
		public void OnRoiGrouped(Model.RoiChangedEventArgs e)
		{
			RoiGrouped?.Invoke(this, e);
		}

		public void OnRoiAdded(CanvasRect<float> canvasRect, CanvasOverlayItem parentOverlay)
		{
			Model.RoiChangedEventArgs argOverlay = CreateRoiChangedEventArgs(canvasRect);
			argOverlay.Group = parentOverlay;
			RoiAdded(this, argOverlay);
		}

		private void OnRoiMouseUp(CanvasRect<float> canvasRect)
		{
			if (IsTeachingMode && !_mouseDownOnExistingRoi) { return; }
			if (canvasRect == null) { return; }
			if (canvasRect.IsEmpty()) { return; }
			MarkRoiDisplayChanged(canvasRect);
			_imageViewer.RefreshGL();
			Model.RoiChangedEventArgs argOverlay = CreateRoiChangedEventArgs(canvasRect);
			RoiMouseUp(this, argOverlay);
		}

		private void RefreshGroupBoundsForCommittedRoi(CanvasRect<float> canvasRect)
		{
			if (canvasRect == null || canvasRect.IsEmpty()) { return; }

			CanvasOverlayItem group = _imageViewer.GetGroupToType(canvasRect.GroupType);
			if (group?.IsVisible == true)
			{
				_imageViewer.ResizeGroupRectangle(canvasRect.GroupType);
				_imageViewer.InvalidateVisibleOverlayCache();
			}
		}

		private void OnRemoveOverlay(ref CanvasRect<float> canvasRect)
		{
			if (canvasRect == null || string.IsNullOrWhiteSpace(canvasRect.UniqueId)) { return; }

			RemoveRoiRequested(this, canvasRect);
			_imageViewer.DeleteOverlay(canvasRect.UniqueId, canvasRect.GroupType, refreshImmediately: false);
			canvasRect = new CanvasRect<float>();
			_drawingRect = new CanvasRect<float>();
		}

		public void OnRoiEditingCompleted(CanvasRect<float> canvasRect)
		{
			if (canvasRect == null) { return; }

			Model.RoiChangedEventArgs argEdit = new Model.RoiChangedEventArgs
			{
				RobotPos = canvasRect.Points.Select(x => x.ToPointF()),
				RoiRect = canvasRect,
			};
			RoiEditingCompleted(this, argEdit);
		}

		private Model.RoiChangedEventArgs CreateRoiChangedEventArgs(CanvasRect<float> canvasRect)
		{
			Model.RoiChangedEventArgs arg = new Model.RoiChangedEventArgs();
			arg.RobotPos = canvasRect.Points.Select(x => x.ToPointF());
			arg.RoiRect = canvasRect;
			return arg;
		}

		#endregion
	}
}
