using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public sealed class CanvasDisplayAdjustmentWindow : Window, IDisposable
    {
        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(CanvasDisplayAdjustmentWindow),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

        private readonly Window owner;
        private readonly FrameworkElement ownerContent;
        private bool disposed;

        public CanvasDisplayAdjustmentWindow(Window owner, FrameworkElement ownerContent)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.ownerContent = ownerContent ?? throw new ArgumentNullException(nameof(ownerContent));
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SizeToContent = SizeToContent.Manual;
            owner.LocationChanged += OnOwnerBoundsChanged;
            owner.SizeChanged += OnOwnerSizeChanged;
            owner.StateChanged += OnOwnerStateChanged;
            owner.IsVisibleChanged += OnOwnerVisibleChanged;
            owner.Closed += OnOwnerClosed;
            owner.DpiChanged += OnOwnerOrHostDpiChanged;
            ownerContent.SizeChanged += OnOwnerSizeChanged;
            DpiChanged += OnOwnerOrHostDpiChanged;
            UpdateBounds();
        }

        public event EventHandler Opening;

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public void Dispose()
        {
            if (disposed) return;
            SetCurrentValue(IsOpenProperty, false);
            disposed = true;
            Close();
        }

        private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
            => ((CanvasDisplayAdjustmentWindow)sender).ApplyVisibility();

        private void ApplyVisibility()
        {
            if (disposed) return;
            if (IsOpen && owner.IsVisible && owner.WindowState != WindowState.Minimized)
            {
                if (Owner == null) Owner = owner;
                UpdateBounds();
                if (!IsVisible)
                {
                    Opening?.Invoke(this, EventArgs.Empty);
                    Show();
                }
            }
            else if (IsVisible) Hide();
        }

        private void UpdateBounds()
        {
            if (disposed || ownerContent.ActualWidth <= 0 || ownerContent.ActualHeight <= 0) return;
            Width = ownerContent.ActualWidth;
            Height = ownerContent.ActualHeight;
            PresentationSource source = PresentationSource.FromVisual(ownerContent);
            if (source?.CompositionTarget == null) return;
            var physicalOrigin = ownerContent.PointToScreen(new System.Windows.Point());
            var origin = source.CompositionTarget.TransformFromDevice.Transform(physicalOrigin);
            Left = origin.X;
            Top = origin.Y;
        }

        private void OnOwnerBoundsChanged(object sender, EventArgs e) => UpdateBounds();
        private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e) => UpdateBounds();
        private void OnOwnerStateChanged(object sender, EventArgs e) => ApplyVisibility();
        private void OnOwnerVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => ApplyVisibility();
        private void OnOwnerOrHostDpiChanged(object sender, System.Windows.DpiChangedEventArgs e) => UpdateBounds();
        private void OnOwnerClosed(object sender, EventArgs e) => Dispose();

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (!disposed && IsOpen && owner.IsVisible && owner.WindowState != WindowState.Minimized)
                SetCurrentValue(IsOpenProperty, false);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!disposed)
            {
                e.Cancel = true;
                SetCurrentValue(IsOpenProperty, false);
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            disposed = true;
            SetCurrentValue(IsOpenProperty, false);
            owner.LocationChanged -= OnOwnerBoundsChanged;
            owner.SizeChanged -= OnOwnerSizeChanged;
            owner.StateChanged -= OnOwnerStateChanged;
            owner.IsVisibleChanged -= OnOwnerVisibleChanged;
            owner.Closed -= OnOwnerClosed;
            owner.DpiChanged -= OnOwnerOrHostDpiChanged;
            ownerContent.SizeChanged -= OnOwnerSizeChanged;
            DpiChanged -= OnOwnerOrHostDpiChanged;
            Content = null;
            BindingOperations.ClearAllBindings(this);
            Resources.MergedDictionaries.Clear();
            NameScope.SetNameScope(this, null);
            base.OnClosed(e);
        }
    }
}
