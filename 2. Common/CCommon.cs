using OpenVisionLab.MessageDialogs;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

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
                DialogResult result = VisionMessageBox.Show(GetMessageBoxOwner(), CreateOptions(strHead, strMessage, type, MessageBoxButtons.OK));
                return result == DialogResult.OK || result == DialogResult.Yes;
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
                Form owner = GetMessageBoxOwner();

                void Show()
                {
                    VisionMessageBoxForm form = new VisionMessageBoxForm(CreateOptions(strHead, strMessage, type, MessageBoxButtons.OK));
                    if (owner != null && !owner.IsDisposed)
                    {
                        form.Show(owner);
                    }
                    else
                    {
                        form.Show();
                    }
                }

                if (owner != null && owner.InvokeRequired)
                {
                    owner.UIThreadBeginInvoke(Show);
                }
                else
                {
                    Show();
                }

                return true;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        private static VisionMessageOptions CreateOptions(string title, string message, MessageBoxType type, MessageBoxButtons buttons)
        {
            return new VisionMessageOptions
            {
                Title = title,
                Message = message,
                Kind = ToVisionMessageKind(type),
                Buttons = buttons,
                TopMost = true
            };
        }

        private static VisionMessageKind ToVisionMessageKind(MessageBoxType type)
        {
            return type switch
            {
                MessageBoxType.Info => VisionMessageKind.Info,
                MessageBoxType.Quit => VisionMessageKind.Question,
                MessageBoxType.Stop => VisionMessageKind.Stop,
                MessageBoxType.Waring => VisionMessageKind.Warning,
                _ => VisionMessageKind.Normal
            };
        }

        private static Form GetMessageBoxOwner()
        {
            return Form.ActiveForm
                ?? Application.OpenForms
                    .Cast<Form>()
                    .FirstOrDefault(form => form.Visible && !form.IsDisposed && !(form is VisionMessageBoxForm));
        }
    }
}
