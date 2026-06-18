using System;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace MvcVisionSystem._1._Core
{
    internal sealed class DisplayDockHost
    {
        private Form owner;
        private Control displayPanel;
        private FormLayerDisplay activeDisplay;

        public DockPanel DockPanel { get; private set; }

        public string ActiveDocumentTitle
        {
            get
            {
                if (activeDisplay != null && !activeDisplay.IsDisposed)
                {
                    return activeDisplay.Text;
                }

                if (DockPanel?.ActiveDocument is FormLayerDisplay display)
                {
                    return display.Text;
                }

                return DockPanel?.ActiveDocument?.DockHandler?.TabText;
            }
        }

        public void SetOwner(Form form)
        {
            owner = form;
        }

        public void SetDockPanel(DockPanel dockPanel)
        {
            DockPanel = dockPanel;
        }

        public void SetDisplayPanel(Control panel)
        {
            displayPanel = panel;
        }

        public void ShowDisplay(FormLayerDisplay display)
        {
            if (display == null || display.IsDisposed)
            {
                return;
            }

            if (DockPanel != null && !DockPanel.IsDisposed)
            {
                display.Show(DockPanel, DockState.Document);
                activeDisplay = display;
                return;
            }

            if (displayPanel == null || displayPanel.IsDisposed)
            {
                return;
            }

            display.TopLevel = false;
            display.FormBorderStyle = FormBorderStyle.None;
            display.Dock = DockStyle.Fill;
            displayPanel.Controls.Add(display);
            display.Show();
            ActivateDisplay(display);
        }

        public void ActivateDisplay(FormLayerDisplay display)
        {
            if (display == null || display.IsDisposed)
            {
                return;
            }

            activeDisplay = display;

            if (display.TopLevel)
            {
                display.Activate();
                return;
            }

            display.BringToFront();
            display.Focus();
        }

        public void InvokeOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (owner == null || owner.IsDisposed || !owner.IsHandleCreated)
            {
                action();
                return;
            }

            if (owner.InvokeRequired)
            {
                try
                {
                    owner.Invoke((MethodInvoker)(() => action()));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            action();
        }

        public TResult InvokeOnUiThread<TResult>(Func<TResult> action)
        {
            if (action == null)
            {
                return default;
            }

            if (owner == null || owner.IsDisposed || !owner.IsHandleCreated)
            {
                return action();
            }

            if (owner.InvokeRequired)
            {
                try
                {
                    return (TResult)owner.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    return default;
                }
                catch (InvalidOperationException)
                {
                    return default;
                }
            }

            return action();
        }

        public bool IsInvokeRequired =>
            owner != null
            && !owner.IsDisposed
            && owner.IsHandleCreated
            && owner.InvokeRequired;
    }
}
