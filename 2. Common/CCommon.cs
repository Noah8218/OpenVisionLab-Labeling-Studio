using System;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfWindow = System.Windows.Window;

namespace MvcVisionSystem
{
    public class CCommon
    {
        public enum MessageBoxType
        {
            Normal = 0,
            Info,
            Quit,
            Stop,
            Waring,
            Warning = Waring
        }

        public static bool ShowdialogMessageBox(string strHead, string strMessage, MessageBoxType type = MessageBoxType.Normal)
        {
            try
            {
                AppLog.NORMAL($"[{strHead}] ==> {strMessage}");
                WpfMessageBoxResult result = ShowBlockingMessageBox(strHead, strMessage, type);
                return result == WpfMessageBoxResult.OK || result == WpfMessageBoxResult.Yes;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        public static bool ShowMessageBox(string strHead, string strMessage, MessageBoxType type = MessageBoxType.Normal)
        {
            try
            {
                AppLog.NORMAL($"[{strHead}] ==> {strMessage}");
                BeginShowMessageBox(strHead, strMessage, type);
                return true;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        private static WpfMessageBoxResult ShowBlockingMessageBox(string title, string message, MessageBoxType type)
        {
            Dispatcher dispatcher = WpfApplication.Current?.Dispatcher;
            if (dispatcher != null
                && !dispatcher.CheckAccess()
                && !dispatcher.HasShutdownStarted
                && !dispatcher.HasShutdownFinished)
            {
                return dispatcher.Invoke(() => ShowMessageBoxOnCurrentThread(title, message, type));
            }

            return ShowMessageBoxOnCurrentThread(title, message, type);
        }

        private static void BeginShowMessageBox(string title, string message, MessageBoxType type)
        {
            Dispatcher dispatcher = WpfApplication.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                ShowMessageBoxOnCurrentThread(title, message, type);
                return;
            }

            dispatcher.BeginInvoke(
                new Action(() => ShowMessageBoxOnCurrentThread(title, message, type)),
                DispatcherPriority.Normal);
        }

        private static WpfMessageBoxResult ShowMessageBoxOnCurrentThread(string title, string message, MessageBoxType type)
        {
            WpfWindow owner = GetMessageBoxOwner();
            WpfMessageBoxImage image = ToWpfMessageBoxImage(type);
            if (owner != null)
            {
                return WpfMessageBox.Show(
                    owner,
                    message ?? string.Empty,
                    title ?? string.Empty,
                    WpfMessageBoxButton.OK,
                    image);
            }

            return WpfMessageBox.Show(
                message ?? string.Empty,
                title ?? string.Empty,
                WpfMessageBoxButton.OK,
                image);
        }

        private static WpfMessageBoxImage ToWpfMessageBoxImage(MessageBoxType type)
        {
            return type switch
            {
                MessageBoxType.Info => WpfMessageBoxImage.Information,
                MessageBoxType.Quit => WpfMessageBoxImage.Question,
                MessageBoxType.Stop => WpfMessageBoxImage.Stop,
                MessageBoxType.Waring => WpfMessageBoxImage.Warning,
                _ => WpfMessageBoxImage.None
            };
        }

        private static WpfWindow GetMessageBoxOwner()
        {
            WpfApplication application = WpfApplication.Current;
            return application?.Windows
                .OfType<WpfWindow>()
                .FirstOrDefault(window => window.IsActive)
                ?? application?.MainWindow;
        }
    }
}
