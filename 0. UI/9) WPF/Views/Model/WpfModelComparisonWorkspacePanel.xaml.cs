using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public partial class WpfModelComparisonWorkspacePanel : UserControl
    {
        private IInputElement previousFocus;
        public WpfModelComparisonWorkspacePanel() => InitializeComponent();
        private void OnImageFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border frame) frame.Clip = new RectangleGeometry(new Rect(e.NewSize), 8D, 8D);
        }

        private void OnModelRoleTargetUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {
            if (sender is TextBlock role && e.Property == TextBlock.TextProperty
                && ReferenceEquals(e.TargetObject, role) && VisualTreeHelper.GetParent(role) is Grid grid)
            {
                grid.InvalidateMeasure();
            }
        }

        private void OnAdoptionOverlayVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (AdoptionOverlay.IsVisible)
            {
                previousFocus = Keyboard.FocusedElement;
                Dispatcher.BeginInvoke(new Action(() => { if (AdoptionOverlay.IsVisible) WorkspaceAdoptionReason.Focus(); }));
            }
            else if (previousFocus != null)
            {
                Keyboard.Focus(previousFocus);
                previousFocus = null;
            }
        }
    }
}
