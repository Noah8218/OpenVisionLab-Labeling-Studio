using System.Windows;
using OpenVisionLab.Logging;
using System.Linq;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public partial class WpfRecipeLibraryPanel : UserControl
    {
        private System.Windows.Threading.DispatcherOperation pendingLayoutMeasurement;
        public WpfRecipeLibraryPanel()
        {
            InitializeComponent();
            Unloaded += (_, _) => { pendingLayoutMeasurement?.Abort(); pendingLayoutMeasurement = null; };
        }

        private void OnRecipeLibrarySizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged) return;
            pendingLayoutMeasurement?.Abort();
            pendingLayoutMeasurement = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new System.Action(() =>
            {
                pendingLayoutMeasurement = null;
                if (!IsLoaded || !IsVisible || Dispatcher.HasShutdownStarted) return;
                ListBoxItem row = null;
                for (int index = 0; index < RecipeLibraryListBox.Items.Count && row == null; index++)
                    row = RecipeLibraryListBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                var presenter = (row?.Template?.FindName("RecipeRowBorder", row) as Border)?.Child as ContentPresenter;
                var grid = presenter != null && VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as Grid : null;
                var itemsHost = row == null ? null : VisualTreeHelper.GetParent(row) as Panel;
                double? beforeHost = itemsHost?.ActualWidth, beforeRow = row?.ActualWidth;
                double? beforeSlot = row == null ? null : System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(row).Width;
                double? maxDesiredBefore = itemsHost?.Children.Cast<UIElement>().Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max();
                ScrollViewer scroll = null; ScrollContentPresenter scrollPresenter = null;
                for (DependencyObject parent = row; parent != null && scroll == null; parent = VisualTreeHelper.GetParent(parent))
                {
                    scrollPresenter ??= parent as ScrollContentPresenter;
                    scroll = parent as ScrollViewer;
                }
                var maxChild = itemsHost?.Children.Cast<UIElement>().OrderByDescending(child => child.DesiredSize.Width).FirstOrDefault();
                int maxChildIndex = maxChild == null ? -1 : RecipeLibraryListBox.ItemContainerGenerator.IndexFromContainer(maxChild);
                string maxChildDataType = (maxChild as FrameworkElement)?.DataContext?.GetType().Name;
                bool? maxChildMeasureValid = maxChild?.IsMeasureValid;
                foreach (ListBoxItem container in itemsHost?.Children.OfType<ListBoxItem>().ToArray() ?? System.Array.Empty<ListBoxItem>())
                {
                    if (container.Template.FindName("RecipeRowBorder", container) is Border { Child: ContentPresenter itemPresenter }
                        && VisualTreeHelper.GetChildrenCount(itemPresenter) > 0
                        && VisualTreeHelper.GetChild(itemPresenter, 0) is FrameworkElement content) content.InvalidateMeasure();
                    container.InvalidateMeasure();
                    container.Measure(new Size(scrollPresenter.ActualWidth, double.PositiveInfinity));
                }
                itemsHost?.InvalidateMeasure();
                itemsHost?.InvalidateArrange();
                scrollPresenter?.InvalidateArrange();
                RecipeLibraryListBox.UpdateLayout();
                double? maxDesiredAfter = itemsHost?.Children.Cast<UIElement>().Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max();
                var binding = grid == null ? null : BindingOperations.GetBindingExpression(grid.ColumnDefinitions[2], ColumnDefinition.WidthProperty);
                var source = binding?.ResolvedSource as FrameworkElement;
                string headerColumns = string.Join(",", RecipeLibraryColumnHeaders.ColumnDefinitions.Select(column => column.ActualWidth));
                string rowColumns = grid == null ? "none" : string.Join(",", grid.ColumnDefinitions.Select(column => column.ActualWidth));
                OVLog.Write(LogCategory.Main, LogLevel.Info, System.FormattableString.Invariant($"HOME_LAYOUT beforeHost={beforeHost} beforeRow={beforeRow} beforeSlot={beforeSlot} maxDesiredBefore={maxDesiredBefore} maxChildIndex={maxChildIndex} maxChildDataType={maxChildDataType} maxChildMeasureValid={maxChildMeasureValid} maxChildDesiredAfter={maxChild?.DesiredSize.Width} afterHost={itemsHost?.ActualWidth} hostDesired={itemsHost?.DesiredSize.Width} afterSlot={(row == null ? (double?)null : System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(row).Width)} maxDesiredAfter={maxDesiredAfter} scrollPresenter={scrollPresenter?.ActualWidth} scrollPresenterDesired={scrollPresenter?.DesiredSize.Width} host={itemsHost?.GetType().Name} view={ActualWidth} window={Window.GetWindow(this)?.ActualWidth} list={RecipeLibraryListBox.ActualWidth} viewport={scroll?.ViewportWidth} extent={scroll?.ExtentWidth} row={row?.ActualWidth} rowDesired={row?.DesiredSize.Width} presenter={presenter?.ActualWidth} presenterDesired={presenter?.DesiredSize.Width} grid={grid?.ActualWidth} gridDesired={grid?.DesiredSize.Width} rowHca={row?.HorizontalContentAlignment} presenterHa={presenter?.HorizontalAlignment} binding={binding?.Status} source={binding?.ResolvedSource?.GetType().Name} sourceWidth={source?.ActualWidth} headers={headerColumns} columns={rowColumns}"));
            }));
        }

        private void OnPreviewFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border frame) frame.Clip = new RectangleGeometry(new Rect(e.NewSize), frame.CornerRadius.TopLeft, frame.CornerRadius.TopLeft);
        }

        private void CloseAdditionalActions(object sender, System.Windows.RoutedEventArgs e) => RecipeLibraryContextMenu.IsOpen = false;

        internal bool FocusSearch()
        {
            if (!IsVisible || !RecipeLibrarySearchBox.IsEnabled || !RecipeLibrarySearchBox.Focus()) return false;
            RecipeLibrarySearchBox.SelectAll();
            return true;
        }
    }
}
