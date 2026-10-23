using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    internal sealed class DialogBackdropScope : IDisposable
    {
        private readonly Window dialog;
        private readonly Window owner;
        private readonly FrameworkElement ownerContent;
        private readonly bool managesOwnerInput;
        private readonly IntPtr ownerHandle;
        private readonly bool ownerWasEnabled;
        private readonly IInputElement previousFocus;
        private Window backdrop;
        private bool restoreFocus;
        private bool disposed;

        internal DialogBackdropScope(Window dialog, FrameworkElement ownerContent, bool managesOwnerInput)
        {
            this.dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            owner = dialog.Owner ?? throw new ArgumentException("The dialog must retain its existing owner.", nameof(dialog));
            this.ownerContent = ownerContent ?? throw new ArgumentNullException(nameof(ownerContent));
            this.managesOwnerInput = managesOwnerInput;
            if (!owner.IsVisible || ownerContent.ActualWidth <= 0 || ownerContent.ActualHeight <= 0
                || PresentationSource.FromVisual(ownerContent)?.CompositionTarget == null)
                throw new InvalidOperationException("A dialog backdrop requires a visible owner viewport.");
            ownerHandle = new WindowInteropHelper(owner).Handle;
            if (ownerHandle == IntPtr.Zero) throw new InvalidOperationException("A dialog backdrop requires an owner HWND.");
            ownerWasEnabled = IsWindowEnabled(ownerHandle);
            previousFocus = managesOwnerInput ? Keyboard.FocusedElement : null;
            try
            {
                Brush background = (Brush)ownerContent.FindResource("DialogBackdropBrush");
                backdrop = new Window
                {
                    Name = "DialogBackdropWindow",
                    Owner = owner,
                    DataContext = ownerContent.DataContext,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    AllowsTransparency = true,
                    Background = background,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    Focusable = false,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };
                dialog.Closing += OnDialogClosing;
                dialog.Closed += OnDialogClosed;
                owner.LocationChanged += OnOwnerBoundsChanged;
                owner.SizeChanged += OnOwnerBoundsChanged;
                owner.StateChanged += OnOwnerBoundsChanged;
                owner.DpiChanged += OnOwnerBoundsChanged;
                owner.IsVisibleChanged += OnOwnerVisibleChanged;
                owner.Closed += OnOwnerClosed;
                ownerContent.SizeChanged += OnOwnerBoundsChanged;
                backdrop.DpiChanged += OnOwnerBoundsChanged;
                UpdateBounds();
                if (managesOwnerInput)
                {
                    // Gate HWND input; modal callers keep ShowDialog's own gating.
                    EnableWindow(ownerHandle, false);
                    if (IsWindowEnabled(ownerHandle))
                        throw new InvalidOperationException("The dialog owner input could not be disabled.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            dialog.Closing -= OnDialogClosing;
            dialog.Closed -= OnDialogClosed;
            owner.LocationChanged -= OnOwnerBoundsChanged;
            owner.SizeChanged -= OnOwnerBoundsChanged;
            owner.StateChanged -= OnOwnerBoundsChanged;
            owner.DpiChanged -= OnOwnerBoundsChanged;
            owner.IsVisibleChanged -= OnOwnerVisibleChanged;
            owner.Closed -= OnOwnerClosed;
            ownerContent.SizeChanged -= OnOwnerBoundsChanged;
            if (managesOwnerInput && ownerWasEnabled && new WindowInteropHelper(owner).Handle == ownerHandle)
                EnableWindow(ownerHandle, true);
            Window releasedBackdrop = backdrop;
            backdrop = null;
            if (releasedBackdrop != null)
            {
                releasedBackdrop.DpiChanged -= OnOwnerBoundsChanged;
                releasedBackdrop.DataContext = null;
                releasedBackdrop.Close();
            }

            if (!managesOwnerInput || !ownerWasEnabled || !restoreFocus) return;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (!owner.IsVisible || owner.WindowState == WindowState.Minimized
                    || new WindowInteropHelper(owner).Handle != ownerHandle || !IsWindowEnabled(ownerHandle)
                    || owner.OwnedWindows.Cast<Window>().Any(window => window.IsVisible && window.IsActive)) return;
                if (owner.Activate() && previousFocus is UIElement element && element.IsVisible && element.IsEnabled
                    && ReferenceEquals(Window.GetWindow(element), owner)) Keyboard.Focus(element);
            }));
        }

        private void OnDialogClosing(object sender, CancelEventArgs e) => restoreFocus = dialog.IsActive;

        private void OnDialogClosed(object sender, EventArgs e) => Dispose();

        private void OnOwnerClosed(object sender, EventArgs e) => Dispose();

        private void OnOwnerBoundsChanged(object sender, EventArgs e) => UpdateBounds();

        private void OnOwnerVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateBounds();

        private void UpdateBounds()
        {
            if (disposed || backdrop == null) return;
            if (!owner.IsVisible || owner.WindowState == WindowState.Minimized)
            {
                if (backdrop.IsVisible) backdrop.Hide();
                return;
            }

            PresentationSource source = PresentationSource.FromVisual(ownerContent);
            if (source?.CompositionTarget == null || ownerContent.ActualWidth <= 0 || ownerContent.ActualHeight <= 0) return;
            Point origin = source.CompositionTarget.TransformFromDevice.Transform(ownerContent.PointToScreen(new Point()));
            backdrop.Left = origin.X;
            backdrop.Top = origin.Y;
            backdrop.Width = ownerContent.ActualWidth;
            backdrop.Height = ownerContent.ActualHeight;
            if (!backdrop.IsVisible) backdrop.Show();
            // This passive owned window covers native airspace without becoming a keyboard/click target.
            EnableWindow(new WindowInteropHelper(backdrop).Handle, false);
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool enable);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);
    }
}
