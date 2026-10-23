using MahApps.Metro.IconPacks;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public partial class WpfWindowTitleBar : UserControl
    {
        private HwndSource _windowSource;

        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(WpfWindowTitleBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleTextProperty =
            DependencyProperty.Register(nameof(SubtitleText), typeof(string), typeof(WpfWindowTitleBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconKindProperty =
            DependencyProperty.Register(nameof(IconKind), typeof(PackIconMaterialKind), typeof(WpfWindowTitleBar), new PropertyMetadata(PackIconMaterialKind.TagMultipleOutline));

        public static readonly DependencyProperty TitleFontSizeProperty =
            DependencyProperty.Register(nameof(TitleFontSize), typeof(double), typeof(WpfWindowTitleBar), new PropertyMetadata(13d));

        public static readonly DependencyProperty SubtitleFontSizeProperty =
            DependencyProperty.Register(nameof(SubtitleFontSize), typeof(double), typeof(WpfWindowTitleBar), new PropertyMetadata(12d));

        public WpfWindowTitleBar()
        {
            InitializeComponent();
            Loaded += TitleBar_Loaded;
            Unloaded += TitleBar_Unloaded;
        }

        // This localization path keeps runtime translation from replacing style-trigger bindings.
        public WpfLocalizationViewModel LanguageViewModel => WpfLocalizationViewModel.Instance;

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

        public double TitleFontSize
        {
            get => (double)GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        public double SubtitleFontSize
        {
            get => (double)GetValue(SubtitleFontSizeProperty);
            set => SetValue(SubtitleFontSizeProperty, value);
        }

        private Window OwnerWindow => Window.GetWindow(this);

        private void TitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            _windowSource = PresentationSource.FromVisual(this) as HwndSource;
            _windowSource?.AddHook(HandleWindowMessage);
            RefreshWindowStateButtons();
        }

        private void TitleBar_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_windowSource != null && !_windowSource.IsDisposed) _windowSource.RemoveHook(HandleWindowMessage);
            _windowSource = null;
        }

        private IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int getMinMaxInfo = 0x0024;
            if (message != getMinMaxInfo) return IntPtr.Zero;

            System.Windows.Forms.Screen monitor = System.Windows.Forms.Screen.FromHandle(hwnd);
            System.Drawing.Rectangle working = monitor.WorkingArea;
            WindowMinMaxInfo info = Marshal.PtrToStructure<WindowMinMaxInfo>(lParam);
            info.MaxSize = new System.Drawing.Point(working.Width, working.Height);
            info.MaxPosition = new System.Drawing.Point(working.Left - monitor.Bounds.Left, working.Top - monitor.Bounds.Top);
            Marshal.StructureToPtr(info, lParam, false);
            handled = true;
            return IntPtr.Zero;
        }

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

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowMinMaxInfo
        {
            public System.Drawing.Point Reserved;
            public System.Drawing.Point MaxSize;
            public System.Drawing.Point MaxPosition;
            public System.Drawing.Point MinTrackSize;
            public System.Drawing.Point MaxTrackSize;
        }

        private static bool CanResize(Window window)
            => window.ResizeMode == ResizeMode.CanResize
               || window.ResizeMode == ResizeMode.CanResizeWithGrip;
    }
}
