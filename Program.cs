﻿using Lib.Common;
using OpenVisionLab;
using RJCodeUI_M1;
using RJCodeUI_M1.Settings;
using System;
using System.Threading;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(true, "MvcVisionSystem", out bool bNew))
            {
                if (!bNew)
                {
                    CCommon.ShowdialogMessageBox("Program Already Running", "Check Job Process");
                    Application.Exit();
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
            if (null == System.Windows.Application.Current)
            {
                new System.Windows.Application().ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            }

            Application.ThreadException += Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.Automatic);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                SettingsManager.LoadApperanceSettings();

                using (StartupSplashHost splash = new StartupSplashHost(
                    $"VERSION : {CVersion.VERSION} - {CVersion.DATETIME_UPDATED} ({CVersion.MANAGER})"))
                {
                    splash.Show();
                    using (FormMainFrame mainForm = new FormMainFrame(null)
                    {
                        Opacity = 0D,
                        ShowInTaskbar = false
                    })
                    {
                        mainForm.Shown += (_, _) =>
                        {
                            mainForm.BeginInvoke((MethodInvoker)(() =>
                            {
                                splash.CloseAndWait();
                                mainForm.ShowInTaskbar = true;
                                mainForm.Opacity = 1D;
                                mainForm.Activate();
                            }));
                        };
                        Application.Run(mainForm);
                    }
                }
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL("Ex ==> {0}", Desc.Message);
            }
        }

        private sealed class StartupSplashHost : IDisposable
        {
            private readonly Thread thread;
            private readonly string versionText;
            private readonly ManualResetEventSlim shownSignal = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim closedSignal = new ManualResetEventSlim(false);
            private FormInit form;
            private volatile bool closeRequested;

            public StartupSplashHost(string versionText)
            {
                this.versionText = versionText;
                thread = new Thread(ThreadMain)
                {
                    IsBackground = true,
                    Name = "Labeling startup splash"
                };
                thread.SetApartmentState(ApartmentState.STA);
            }

            public void Show()
            {
                thread.Start();
                shownSignal.Wait(1500);
            }

            public void Close()
            {
                closeRequested = true;
                FormInit splashForm = form;
                if (splashForm == null || splashForm.IsDisposed)
                {
                    return;
                }

                try
                {
                    if (splashForm.IsHandleCreated)
                    {
                        splashForm.BeginInvoke((MethodInvoker)(() => splashForm.OnInitEnd()));
                    }
                }
                catch
                {
                    // The splash is best-effort only; main application shutdown must not depend on it.
                }
            }

            public void CloseAndWait()
            {
                Close();
                closedSignal.Wait(1000);
            }

            public void Dispose()
            {
                Close();
                if (thread.IsAlive)
                {
                    thread.Join(1000);
                }

                shownSignal.Dispose();
                closedSignal.Dispose();
            }

            private void ThreadMain()
            {
                try
                {
                    using (FormInit splashForm = new FormInit
                    {
                        VersionText = versionText,
                        VersionLogAction = message => AppLog.NORMAL(message)
                    })
                    {
                        form = splashForm;
                        splashForm.Shown += (_, _) =>
                        {
                            shownSignal.Set();
                            if (closeRequested)
                            {
                                splashForm.BeginInvoke((MethodInvoker)(() => splashForm.OnInitEnd()));
                            }
                        };
                        FormScreenPlacement.CenterOnPreferredScreen(splashForm);

                        if (closeRequested)
                        {
                            return;
                        }

                        Application.Run(splashForm);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.ABNORMAL($"Startup splash failed: {ex.Message}");
                }
                finally
                {
                    form = null;
                    shownSignal.Set();
                    closedSignal.Set();
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //CLog.Error( "Ex ==> {0}", e.ToString());
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            //CLog.Error( "Ex ==> {0}", e.ToString());
        }
    }
}
