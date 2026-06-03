using MvcVisionSystem._1._Core;
using System;
using System.Drawing;

namespace MvcVisionSystem
{
    public partial class FormTools : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        private CGlobal Global = CGlobal.Inst;
        private int PanelCount = 0;
        public FormTools()
        {
            InitializeComponent();

            CloseButton = false;
            CloseButtonVisible = false;
        }

        // If the content won't display nicely, hide it
        private void ResizeEvent(object sender, EventArgs e)
        {
            this.Visible = this.Width > this.MinimumSize.Width && this.Height > this.MinimumSize.Height;
        }

        private bool ChangeSize = false;

        private void Form_VisibleChanged(object sender, EventArgs e)
        {
            if (!ChangeSize)
            {
                if (DockHandler.FloatPane == null) { return; }
                DockHandler.FloatPane.FloatWindow.Bounds = new Rectangle(DockHandler.FloatPane.FloatWindow.Bounds.X, DockHandler.FloatPane.FloatWindow.Bounds.Y, 800, 400);
                this.Refresh();
                ChangeSize = true;
            }
        }

        private void Form_Load(object sender, EventArgs e)
        {            
            CDisplayManager.EventUpdateCam += OnCamUpdate;            
        }

        private void OnCamUpdate(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                //tbExposure.Text = Global.Device.CAMERAS[CDisplayManager.CameraIndex].Property.EXPOSURETIME_US.ToString();
                //tbGain.Text = Global.Device.CAMERAS[CDisplayManager.CameraIndex].Property.GAIN.ToString();
            });
        }     
    }
}
