using System.Windows;
using System.Windows.Input;

namespace OpenVisionLab.Mvvm.Behaviors
{
    /// <summary>
    /// Routes Window lifecycle events to ICommand so views do not need direct Loaded/Closed handlers.
    /// </summary>
    public static class WindowLifecycleCommandBehavior
    {
        public static readonly DependencyProperty LoadedCommandProperty =
            DependencyProperty.RegisterAttached(
                "LoadedCommand",
                typeof(ICommand),
                typeof(WindowLifecycleCommandBehavior),
                new PropertyMetadata(null, OnLoadedCommandChanged));

        public static readonly DependencyProperty ClosedCommandProperty =
            DependencyProperty.RegisterAttached(
                "ClosedCommand",
                typeof(ICommand),
                typeof(WindowLifecycleCommandBehavior),
                new PropertyMetadata(null, OnClosedCommandChanged));

        public static ICommand GetLoadedCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(LoadedCommandProperty);
        }

        public static void SetLoadedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(LoadedCommandProperty, value);
        }

        public static ICommand GetClosedCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(ClosedCommandProperty);
        }

        public static void SetClosedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(ClosedCommandProperty, value);
        }

        private static void OnLoadedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not FrameworkElement element)
            {
                return;
            }

            element.Loaded -= OnLoaded;
            if (e.NewValue != null)
            {
                element.Loaded += OnLoaded;
            }
        }

        private static void OnClosedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not Window window)
            {
                return;
            }

            window.Closed -= OnClosed;
            if (e.NewValue != null)
            {
                window.Closed += OnClosed;
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            Execute(GetLoadedCommand((DependencyObject)sender), e);
        }

        private static void OnClosed(object sender, System.EventArgs e)
        {
            Execute(GetClosedCommand((DependencyObject)sender), e);
        }

        private static void Execute(ICommand command, object parameter)
        {
            if (command?.CanExecute(parameter) == true)
            {
                command.Execute(parameter);
            }
        }
    }
}