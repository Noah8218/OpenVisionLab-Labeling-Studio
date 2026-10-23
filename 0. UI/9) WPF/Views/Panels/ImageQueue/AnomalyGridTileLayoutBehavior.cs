using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MvcVisionSystem
{
    // View-only sizing; selection, review state and image navigation remain with the queue owners.
    public static class AnomalyGridTileLayoutBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(AnomalyGridTileLayoutBehavior), new PropertyMetadata(false, OnEnabledChanged));
        public static readonly DependencyProperty TileWidthProperty = DependencyProperty.RegisterAttached(
            "TileWidth", typeof(double), typeof(AnomalyGridTileLayoutBehavior), new PropertyMetadata(150D));
        public static readonly DependencyProperty TileImageHeightProperty = DependencyProperty.RegisterAttached(
            "TileImageHeight", typeof(double), typeof(AnomalyGridTileLayoutBehavior), new PropertyMetadata(100D));
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
            "CornerRadius", typeof(double), typeof(AnomalyGridTileLayoutBehavior), new PropertyMetadata(0D, OnCornerRadiusChanged));
        private static readonly DependencyProperty ScrollViewerProperty = DependencyProperty.RegisterAttached(
            "ScrollViewer", typeof(ScrollViewer), typeof(AnomalyGridTileLayoutBehavior));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static double GetTileWidth(DependencyObject element) => (double)element.GetValue(TileWidthProperty);
        public static double GetTileImageHeight(DependencyObject element) => (double)element.GetValue(TileImageHeightProperty);
        public static double GetCornerRadius(DependencyObject element) => (double)element.GetValue(CornerRadiusProperty);
        public static void SetCornerRadius(DependencyObject element, double value) => element.SetValue(CornerRadiusProperty, value);

        private static void OnCornerRadiusChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not FrameworkElement tile) return;
            tile.SizeChanged -= OnTileSizeChanged;
            tile.Loaded -= OnTileLoaded;
            tile.Unloaded -= OnTileUnloaded;
            if ((double)e.NewValue <= 0) { tile.ClearValue(UIElement.ClipProperty); return; }
            tile.Loaded += OnTileLoaded;
            tile.Unloaded += OnTileUnloaded;
            tile.SizeChanged += OnTileSizeChanged;
            ClipTile(tile);
        }

        private static void OnTileLoaded(object sender, RoutedEventArgs e)
        {
            var tile = (FrameworkElement)sender;
            tile.SizeChanged -= OnTileSizeChanged;
            tile.SizeChanged += OnTileSizeChanged;
            ClipTile(tile);
        }

        private static void OnTileUnloaded(object sender, RoutedEventArgs e) => ((FrameworkElement)sender).SizeChanged -= OnTileSizeChanged;
        private static void OnTileSizeChanged(object sender, SizeChangedEventArgs e) => ClipTile((FrameworkElement)sender);
        private static void ClipTile(FrameworkElement tile) => tile.Clip = new RectangleGeometry(new Rect(tile.RenderSize), GetCornerRadius(tile), GetCornerRadius(tile));

        private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not ListBox grid) return;
            grid.Loaded -= OnLoaded;
            grid.Unloaded -= OnUnloaded;
            grid.SizeChanged -= OnSizeChanged;
            Detach(grid);
            if (e.NewValue is not true) return;
            grid.Loaded += OnLoaded;
            grid.Unloaded += OnUnloaded;
            grid.SizeChanged += OnSizeChanged;
            if (grid.IsLoaded) Attach(grid);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => Attach((ListBox)sender);
        private static void OnUnloaded(object sender, RoutedEventArgs e) => Detach((ListBox)sender);
        private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayout((ListBox)sender);

        private static void Attach(ListBox grid)
        {
            Detach(grid);
            grid.ApplyTemplate();
            if (grid.Template.FindName("AnomalyGridScrollViewer", grid) is not ScrollViewer scroll) return;
            grid.SetValue(ScrollViewerProperty, scroll);
            scroll.ScrollChanged += OnScrollChanged;
            UpdateLayout(grid);
        }

        private static void Detach(ListBox grid)
        {
            if (grid.GetValue(ScrollViewerProperty) is ScrollViewer scroll) scroll.ScrollChanged -= OnScrollChanged;
            grid.ClearValue(ScrollViewerProperty);
        }

        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ViewportWidthChange == 0 && e.ExtentHeightChange == 0) return;
            if (((ScrollViewer)sender).TemplatedParent is ListBox grid) UpdateLayout(grid);
        }

        private static void UpdateLayout(ListBox grid)
        {
            if (grid.GetValue(ScrollViewerProperty) is not ScrollViewer scroll || scroll.ViewportWidth <= 0) return;
            double available = Math.Max(150D, scroll.ViewportWidth);
            int columns = Math.Max(1, (int)Math.Floor((available + 10D) / 160D));
            double width = (available - 10D * (columns - 1)) / columns;
            grid.SetValue(TileWidthProperty, width);
            grid.SetValue(TileImageHeightProperty, (width - 4D) * 2D / 3D);
        }
    }
}
