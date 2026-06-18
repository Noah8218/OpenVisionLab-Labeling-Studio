namespace MvcVisionSystem
{
    partial class FormLayerDisplay
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Activated -= this.FormLayerDisplay_Activated;
                viewer?.Dispose();
                viewer = null;
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormLayerDisplay));
            this.timePixelData = new System.Windows.Forms.Timer(this.components);
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.lbZOOM = new RJCodeUI_M1.RJControls.RJLabel();
            this.lbGV = new RJCodeUI_M1.RJControls.RJLabel();
            this.lbXY = new RJCodeUI_M1.RJControls.RJLabel();
            this.lbRGB = new RJCodeUI_M1.RJControls.RJLabel();
            this.lbMODE = new RJCodeUI_M1.RJControls.RJLabel();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // timePixelData
            // 
            this.timePixelData.Enabled = true;
            this.timePixelData.Interval = 50;
            this.timePixelData.Tick += new System.EventHandler(this.timePixelData_Tick);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.panel3);
            this.panel1.Controls.Add(this.panel2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1255, 573);
            this.panel1.TabIndex = 1952;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.Black;
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1255, 549);
            this.panel3.TabIndex = 1954;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(23)))), ((int)(((byte)(32)))));
            this.panel2.Controls.Add(this.lbZOOM);
            this.panel2.Controls.Add(this.lbGV);
            this.panel2.Controls.Add(this.lbXY);
            this.panel2.Controls.Add(this.lbRGB);
            this.panel2.Controls.Add(this.lbMODE);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 549);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(7, 3, 7, 3);
            this.panel2.Size = new System.Drawing.Size(1255, 24);
            this.panel2.TabIndex = 1953;
            // 
            // lbZOOM
            // 
            this.lbZOOM.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(34)))), ((int)(((byte)(48)))));
            this.lbZOOM.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lbZOOM.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbZOOM.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbZOOM.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lbZOOM.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(238)))), ((int)(((byte)(248)))));
            this.lbZOOM.LinkLabel = false;
            this.lbZOOM.Location = new System.Drawing.Point(681, 3);
            this.lbZOOM.Name = "lbZOOM";
            this.lbZOOM.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.lbZOOM.Size = new System.Drawing.Size(180, 18);
            this.lbZOOM.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.lbZOOM.TabIndex = 1954;
            this.lbZOOM.Text = "줌 0%";
            this.lbZOOM.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lbGV
            // 
            this.lbGV.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(34)))), ((int)(((byte)(48)))));
            this.lbGV.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lbGV.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbGV.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbGV.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lbGV.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(238)))), ((int)(((byte)(248)))));
            this.lbGV.LinkLabel = false;
            this.lbGV.Location = new System.Drawing.Point(591, 3);
            this.lbGV.Name = "lbGV";
            this.lbGV.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.lbGV.Size = new System.Drawing.Size(90, 18);
            this.lbGV.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.lbGV.TabIndex = 1953;
            this.lbGV.Text = "GRAY 0";
            this.lbGV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lbXY
            // 
            this.lbXY.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(34)))), ((int)(((byte)(48)))));
            this.lbXY.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lbXY.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbXY.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbXY.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lbXY.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(238)))), ((int)(((byte)(248)))));
            this.lbXY.LinkLabel = false;
            this.lbXY.Location = new System.Drawing.Point(381, 3);
            this.lbXY.Name = "lbXY";
            this.lbXY.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.lbXY.Size = new System.Drawing.Size(210, 18);
            this.lbXY.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.lbXY.TabIndex = 1949;
            this.lbXY.Text = "좌표 0,0";
            this.lbXY.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lbRGB
            // 
            this.lbRGB.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(34)))), ((int)(((byte)(48)))));
            this.lbRGB.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lbRGB.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbRGB.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbRGB.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lbRGB.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(238)))), ((int)(((byte)(248)))));
            this.lbRGB.LinkLabel = false;
            this.lbRGB.Location = new System.Drawing.Point(177, 3);
            this.lbRGB.Name = "lbRGB";
            this.lbRGB.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.lbRGB.Size = new System.Drawing.Size(204, 18);
            this.lbRGB.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.lbRGB.TabIndex = 1948;
            this.lbRGB.Text = "RGB 0,0,0";
            this.lbRGB.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lbMODE
            // 
            this.lbMODE.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(34)))), ((int)(((byte)(48)))));
            this.lbMODE.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lbMODE.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbMODE.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbMODE.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold);
            this.lbMODE.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(238)))), ((int)(((byte)(248)))));
            this.lbMODE.LinkLabel = false;
            this.lbMODE.Location = new System.Drawing.Point(7, 3);
            this.lbMODE.Name = "lbMODE";
            this.lbMODE.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.lbMODE.Size = new System.Drawing.Size(170, 18);
            this.lbMODE.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.lbMODE.TabIndex = 1955;
            this.lbMODE.Text = "모드 이동";
            this.lbMODE.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // FormLayerDisplay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(1255, 573);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "FormLayerDisplay";
            this.Text = "LabelingCanvas";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LayerDisplay_FormClosed);
            this.VisibleChanged += new System.EventHandler(this.FormLayerDisplay_VisibleChanged);
            this.panel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Timer timePixelData;
        private RJCodeUI_M1.RJControls.RJLabel lbRGB;
        private RJCodeUI_M1.RJControls.RJLabel lbXY;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private RJCodeUI_M1.RJControls.RJLabel lbZOOM;
        private RJCodeUI_M1.RJControls.RJLabel lbGV;
        private RJCodeUI_M1.RJControls.RJLabel lbMODE;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Timer timer1;
    }
}
