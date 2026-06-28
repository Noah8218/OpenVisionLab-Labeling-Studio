using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public partial class WpfWindowTitleBar : UserControl
    {
        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(WpfWindowTitleBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleTextProperty =
            DependencyProperty.Register(nameof(SubtitleText), typeof(string), typeof(WpfWindowTitleBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconKindProperty =
            DependencyProperty.Register(nameof(IconKind), typeof(PackIconMaterialKind), typeof(WpfWindowTitleBar), new PropertyMetadata(PackIconMaterialKind.TagMultipleOutline));

        public WpfWindowTitleBar()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshWindowStateButtons();
        }

        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }

        public string SubtitleText
        {
            get => (string)GetValue(SubtitleTextProperty);
            set => SetValue(SubtitleTextProperty, value);
        }

        public PackIconMaterialKind IconKind
        {
            get => (PackIconMaterialKind)GetValue(IconKindProperty);
            set => SetValue(IconKindProperty, value);
        }

        private Window OwnerWindow => Window.GetWindow(this);

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Window window = OwnerWindow;
            if (window == null)
            {
                return;
            }

            if (e.ClickCount == 2 && CanResize(window))
            {
                ToggleMaximize(window);
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                window.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = OwnerWindow;
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = OwnerWindow;
            if (window != null && CanResize(window))
            {
                ToggleMaximize(window);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            OwnerWindow?.Close();
        }

        private void ToggleMaximize(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            RefreshWindowStateButtons();
        }

        private void RefreshWindowStateButtons()
        {
            Window window = OwnerWindow;
            if (window == null)
            {
                return;
            }

            bool canResize = CanResize(window);
            MaximizeButton.Visibility = canResize ? Visibility.Visible : Visibility.Collapsed;
            MaximizeIcon.Kind = window.WindowState == WindowState.Maximized
                ? PackIconMaterialKind.WindowRestore
                : PackIconMaterialKind.WindowMaximize;
        }

        private static bool CanResize(Window window)
            => window.ResizeMode == ResizeMode.CanResize
               || window.ResizeMode == ResizeMode.CanResizeWithGrip;
    }
}
