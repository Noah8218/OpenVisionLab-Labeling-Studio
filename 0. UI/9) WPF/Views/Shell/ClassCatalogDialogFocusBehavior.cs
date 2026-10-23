using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public static class ClassCatalogDialogFocusBehavior
    {
        public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
            "Target", typeof(IInputElement), typeof(ClassCatalogDialogFocusBehavior), new PropertyMetadata(null, OnTargetChanged));

        public static IInputElement GetTarget(DependencyObject element) => (IInputElement)element.GetValue(TargetProperty);
        public static void SetTarget(DependencyObject element, IInputElement value) => element.SetValue(TargetProperty, value);

        private static void OnTargetChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not FrameworkElement view) return;
            view.IsVisibleChanged -= OnVisibilityChanged;
            if (e.NewValue == null) return;
            view.IsVisibleChanged += OnVisibilityChanged;
            if (view.IsVisible) FocusTarget(view);
        }

        private static void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true) FocusTarget((FrameworkElement)sender);
        }

        private static void FocusTarget(FrameworkElement view)
        {
            view.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (view.IsVisible && GetTarget(view) is UIElement target && target.IsVisible && target.IsEnabled)
                    Keyboard.Focus(target);
            }));
        }
    }
}
