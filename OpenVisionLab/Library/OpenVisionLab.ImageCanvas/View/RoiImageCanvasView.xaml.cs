using OpenVisionLab.ImageCanvas.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenVisionLab.ImageCanvas.Views
{
	public partial class RoiImageCanvasView : UserControl
	{
		public static readonly DependencyProperty IsViewerToolBarVisibleProperty = DependencyProperty.Register(
			nameof(IsViewerToolBarVisible),
			typeof(bool),
			typeof(RoiImageCanvasView),
			new PropertyMetadata(true));

		public static readonly DependencyProperty IsViewerStatusBarVisibleProperty = DependencyProperty.Register(
			nameof(IsViewerStatusBarVisible),
			typeof(bool),
			typeof(RoiImageCanvasView),
			new PropertyMetadata(true));

		public static readonly DependencyProperty ShowBackgroundGridProperty = DependencyProperty.Register(
			nameof(ShowBackgroundGrid),
			typeof(bool),
			typeof(RoiImageCanvasView),
			new PropertyMetadata(false, OnShowBackgroundGridChanged));

		public RoiImageCanvasView()
		{
			InitializeComponent();

			Loaded += ImageCanvasView_Loaded;
			PreviewKeyDown += ImageCanvasView_PreviewKeyDown;
			KeyUp += ImageCanvasView_KeyUp;
		}

		public bool IsViewerToolBarVisible
		{
			get => (bool)GetValue(IsViewerToolBarVisibleProperty);
			set => SetValue(IsViewerToolBarVisibleProperty, value);
		}

		public bool IsViewerStatusBarVisible
		{
			get => (bool)GetValue(IsViewerStatusBarVisibleProperty);
			set => SetValue(IsViewerStatusBarVisibleProperty, value);
		}

		public bool ShowBackgroundGrid
		{
			get => (bool)GetValue(ShowBackgroundGridProperty);
			set => SetValue(ShowBackgroundGridProperty, value);
		}

		private static void OnShowBackgroundGridChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			if (dependencyObject is RoiImageCanvasView view
				&& view.DataContext is RoiImageCanvasViewModel viewModel
				&& viewModel.ImageViewer != null)
			{
				viewModel.ImageViewer.ShowBackgroundGrid = e.NewValue is bool isVisible && isVisible;
			}
		}

		private void ImageCanvasView_Loaded(object sender, RoutedEventArgs e)
		{
			if (DataContext is RoiImageCanvasViewModel viewModel && viewModel.ImageViewer != null)
			{
				imageBoxCameraTwoD.Child = viewModel.ImageViewer;
				viewModel.ImageViewer.ShowBackgroundGrid = ShowBackgroundGrid;
				viewModel.ConfigureContextMenuHost(new ImageCanvasContextMenuHost(MainGrid.ContextMenu));
				MainGrid.ContextMenu.DataContext = viewModel;

				if (viewModel.LoadedCommand?.CanExecute(null) == true)
				{
					viewModel.LoadedCommand.Execute(null);
				}
			}
		}

		private void ImageCanvasView_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (DataContext is RoiImageCanvasViewModel viewModel && viewModel.PreviewKeyDownCommand?.CanExecute(e) == true)
			{
				viewModel.PreviewKeyDownCommand.Execute(e);
			}
		}

		private void ImageCanvasView_KeyUp(object sender, KeyEventArgs e)
		{
			if (DataContext is RoiImageCanvasViewModel viewModel && viewModel.KeyUpCommand?.CanExecute(e) == true)
			{
				viewModel.KeyUpCommand.Execute(e);
			}
		}
	}
}
