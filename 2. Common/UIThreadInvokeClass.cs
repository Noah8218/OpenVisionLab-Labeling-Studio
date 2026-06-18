using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    public static class UIThreadInvokeClass
    {
        static public bool UIThreadBeginInvoke(this Control control, Action code)
        {
            if (control == null || code == null || control.IsDisposed || control.Disposing)
            {
                return false;
            }

            try
            {
                if (control.InvokeRequired)
                {
                    if (!control.IsHandleCreated)
                    {
                        return false;
                    }

                    control.BeginInvoke((MethodInvoker)(() =>
                    {
                        if (control.IsDisposed || control.Disposing)
                        {
                            return;
                        }

                        code.Invoke();
                    }));
                    return true;
                }

                code.Invoke();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        static public bool UIThreadInvoke(this Control control, Action code)
        {
            if (control == null || code == null || control.IsDisposed || control.Disposing)
            {
                return false;
            }

            try
            {
                if (control.InvokeRequired)
                {
                    if (!control.IsHandleCreated)
                    {
                        return false;
                    }

                    control.Invoke((MethodInvoker)(() =>
                    {
                        if (control.IsDisposed || control.Disposing)
                        {
                            return;
                        }

                        code.Invoke();
                    }));
                    return true;
                }

                code.Invoke();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
