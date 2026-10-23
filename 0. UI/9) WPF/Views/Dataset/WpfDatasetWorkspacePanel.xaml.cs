using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public partial class WpfDatasetWorkspacePanel : UserControl
    {
        private ScrollViewer gridScrollViewer;

        public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(
            nameof(TileWidth), typeof(double), typeof(WpfDatasetWorkspacePanel), new PropertyMetadata(150D));
        public static readonly DependencyProperty TileImageHeightProperty = DependencyProperty.Register(
            nameof(TileImageHeight), typeof(double), typeof(WpfDatasetWorkspacePanel), new PropertyMetadata(100D));

        public WpfDatasetWorkspacePanel()
        {
            InitializeComponent();
            Loaded += OnLayoutLoaded;
            Unloaded += OnLayoutUnloaded;
            DatasetGrid.SizeChanged += OnGridSizeChanged;
        }

        public double TileWidth { get => (double)GetValue(TileWidthProperty); private set => SetValue(TileWidthProperty, value); }
        public double TileImageHeight { get => (double)GetValue(TileImageHeightProperty); private set => SetValue(TileImageHeightProperty, value); }

        private void OnLayoutLoaded(object sender, RoutedEventArgs e)
        {
            DatasetGrid.ApplyTemplate();
            gridScrollViewer = DatasetGrid.Template.FindName("PART_ContentHost", DatasetGrid) as ScrollViewer;
            if (gridScrollViewer != null) gridScrollViewer.ScrollChanged += OnGridScrollChanged;
            UpdateTileLayout();
        }

        private void OnLayoutUnloaded(object sender, RoutedEventArgs e)
        {
            if (gridScrollViewer != null) gridScrollViewer.ScrollChanged -= OnGridScrollChanged;
            gridScrollViewer = null;
        }

        private void OnSplitSettingsVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsLoaded) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (DatasetSplitSettingsOverlay.IsVisible) DatasetSplitValidationPercent.Focus();
                else DatasetSplitSettingsButton.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnGridScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange != 0 || e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) UpdateTileLayout();
        }
        private void OnGridSizeChanged(object sender, SizeChangedEventArgs e) => UpdateTileLayout();

        private void OnDatasetVersionBadgeSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border badge) badge.CornerRadius = new CornerRadius(Math.Min(e.NewSize.Width, e.NewSize.Height) / 2D);
        }

        private void OnTileSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ListBoxItem tile) tile.Clip = new RectangleGeometry(new Rect(0, 0, tile.ActualWidth, tile.ActualHeight), 8D, 8D);
        }

        private void OnTileContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ContentPresenter content) content.Clip = new RectangleGeometry(new Rect(e.NewSize), 6D, 6D);
        }

        private void UpdateTileLayout()
        {
            double scrollbarWidth = 0;
            if (gridScrollViewer?.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            {
                var scrollbar = gridScrollViewer.Template.FindName("PART_VerticalScrollBar", gridScrollViewer) as ScrollBar;
                scrollbarWidth = scrollbar?.ActualWidth > 0 ? scrollbar.ActualWidth : SystemParameters.VerticalScrollBarWidth;
            }
            double available = Math.Max(150D, DatasetGrid.ActualWidth - DatasetGrid.Padding.Left - DatasetGrid.Padding.Right - scrollbarWidth);
            int columns = Math.Max(1, (int)Math.Floor((available + 10D) / 160D));
            TileWidth = Math.Max(150D, (available - 10D * (columns - 1)) / columns);
            TileImageHeight = Math.Max(0D, TileWidth - 4D) * 2D / 3D;
        }
    }
}
