using System;
using System.Linq;
using System.Windows;

namespace MvcVisionSystem
{
    public static class WpfLabelingShellLauncher
    {
        public static WpfLabelingShellWindow ShowClassCatalog()
            => ShowShell(window => window.FocusClassCatalogTab());

        public static WpfLabelingShellWindow ShowYoloSettings()
            => ShowShell(window => window.FocusYoloSettingsTab());

        private static WpfLabelingShellWindow ShowShell(Action<WpfLabelingShellWindow> focusAction)
        {
            WpfLabelingShellWindow window = ShowShell();
            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.Invoke(() => focusAction(window));
                return window;
            }

            focusAction(window);
            return window;
        }

        private static WpfLabelingShellWindow ShowShell()
        {
            Application application = Application.Current;
            if (application == null)
            {
                application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
            }

            if (!application.Dispatcher.CheckAccess())
            {
                return (WpfLabelingShellWindow)application.Dispatcher.Invoke(ShowShell);
            }

            WpfLabelingShellWindow window = application.Windows
                .OfType<WpfLabelingShellWindow>()
                .FirstOrDefault();

            if (window == null)
            {
                window = new WpfLabelingShellWindow();
            }

            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            return window;
        }
    }
}
