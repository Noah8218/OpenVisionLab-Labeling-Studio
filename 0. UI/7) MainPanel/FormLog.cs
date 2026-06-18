using OpenVisionLab.Logging.Controls.View;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace MvcVisionSystem
{
    public partial class FormLog : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        private const string DockTitle = "로그";
        private ElementHost logHost;

        public bool ChangeSize { get; set; } = false;

        public FormLog()
        {
            InitializeComponent();

            Text = DockTitle;
            TabText = DockTitle;
            ToolTipText = DockTitle;
            MinimumSize = new Size(300, 80);
            BackColor = Color.FromArgb(18, 22, 29);
            CloseButton = false;
            CloseButtonVisible = false;
            HideOnClose = true;
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.DockBottom;

            logHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = new LogPanelView()
            };

            pnLog.Controls.Add(logHost);
        }

        private void FormLayerDisplay_VisibleChanged(object sender, EventArgs e)
        {
            if (!ChangeSize)
            {
                if (DockHandler.FloatPane == null) { return; }
                DockHandler.FloatPane.FloatWindow.Bounds = new Rectangle(DockHandler.FloatPane.FloatWindow.Bounds.X, DockHandler.FloatPane.FloatWindow.Bounds.Y, 800, 400);
                Refresh();
                ChangeSize = true;
            }
        }
    }
}
