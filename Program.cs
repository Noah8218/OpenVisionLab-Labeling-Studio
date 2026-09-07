using System;
using System.Threading;
using OpenVisionLab;

namespace MvcVisionSystem
{
    static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (HeadlessRuntimeCommandService.TryExecute(args, Console.Out, out int headlessExitCode))
            {
                Environment.ExitCode = headlessExitCode;
                return;
            }

            RuntimeDiagnosticsService.ConfigureApplicationStartup();
            OpenVisionLanguageService.Load();
            using (Mutex mutex = new Mutex(true, "OpenVisionLab.LabelingStudio", out bool bNew))
            {
                if (!bNew)
                {
                    MessageDialogService.ShowdialogMessageBox("Program Already Running", "Check Job Process");
                    return;
                }

                try
                {
                    RunApplication();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void RunApplication()
        {
            RunWpfApplication();
        }

        private static void RunWpfApplication()
        {
            System.Windows.Application application = System.Windows.Application.Current ?? new System.Windows.Application();
            application.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;

            try
            {
                application.Run(new WpfLabelingShellWindow());
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL("Ex ==> {0}", Desc.Message);
            }
            finally
            {
                LabelingApplicationState.Inst.StopPythonModelClientConnection();
            }
        }
    }
}
