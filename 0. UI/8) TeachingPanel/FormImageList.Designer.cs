namespace MvcVisionSystem
{
    partial class FormImageList
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeImageListResources();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormImageList));
            this.rjPanel2 = new RJCodeUI_M1.RJControls.RJPanel();
            this.rjPanel1 = new RJCodeUI_M1.RJControls.RJPanel();
            this.toolStripContainer1 = new System.Windows.Forms.ToolStripContainer();
            this.StatusStrip = new System.Windows.Forms.StatusStrip();
            this.StatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.uiSplitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnOpenFileList = new RJCodeUI_M1.RJControls.RJButton();
            this.imageGridView = new RJCodeUI_M1.RJControls.RJDataGridView();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
            this.x96ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.x120ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.x200ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rjPanel2.SuspendLayout();
            this.rjPanel1.SuspendLayout();
            this.toolStripContainer1.BottomToolStripPanel.SuspendLayout();
            this.toolStripContainer1.ContentPanel.SuspendLayout();
            this.toolStripContainer1.TopToolStripPanel.SuspendLayout();
            this.toolStripContainer1.SuspendLayout();
            this.StatusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.uiSplitContainer1)).BeginInit();
            this.uiSplitContainer1.Panel1.SuspendLayout();
            this.uiSplitContainer1.Panel2.SuspendLayout();
            this.uiSplitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageGridView)).BeginInit();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // rjPanel2
            // 
            this.rjPanel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(27)))), ((int)(((byte)(34)))));
            this.rjPanel2.BorderRadius = 0;
            this.rjPanel2.Controls.Add(this.rjPanel1);
            this.rjPanel2.Customizable = false;
            this.rjPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rjPanel2.Location = new System.Drawing.Point(0, 0);
            this.rjPanel2.Name = "rjPanel2";
            this.rjPanel2.Size = new System.Drawing.Size(554, 513);
            this.rjPanel2.TabIndex = 2145;
            // 
            // rjPanel1
            // 
            this.rjPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(27)))), ((int)(((byte)(34)))));
            this.rjPanel1.BorderRadius = 5;
            this.rjPanel1.Controls.Add(this.toolStripContainer1);
            this.rjPanel1.Customizable = false;
            this.rjPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rjPanel1.Location = new System.Drawing.Point(0, 0);
            this.rjPanel1.Name = "rjPanel1";
            this.rjPanel1.Size = new System.Drawing.Size(554, 513);
            this.rjPanel1.TabIndex = 2144;
            // 
            // toolStripContainer1
            // 
            this.toolStripContainer1.BottomToolStripPanel.Controls.Add(this.StatusStrip);
            this.toolStripContainer1.ContentPanel.Controls.Add(this.uiSplitContainer1);
            this.toolStripContainer1.ContentPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.toolStripContainer1.ContentPanel.Size = new System.Drawing.Size(554, 466);
            this.toolStripContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolStripContainer1.LeftToolStripPanelVisible = false;
            this.toolStripContainer1.Location = new System.Drawing.Point(0, 0);
            this.toolStripContainer1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.toolStripContainer1.Name = "toolStripContainer1";
            this.toolStripContainer1.RightToolStripPanelVisible = false;
            this.toolStripContainer1.Size = new System.Drawing.Size(554, 513);
            this.toolStripContainer1.TabIndex = 2155;
            this.toolStripContainer1.Text = "toolStripContainer1";
            this.toolStripContainer1.TopToolStripPanel.Controls.Add(this.toolStrip1);
            // 
            // StatusStrip
            // 
            this.StatusStrip.Dock = System.Windows.Forms.DockStyle.None;
            this.StatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.StatusLabel});
            this.StatusStrip.Location = new System.Drawing.Point(0, 0);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Size = new System.Drawing.Size(554, 22);
            this.StatusStrip.TabIndex = 0;
            // 
            // StatusLabel
            // 
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(39, 17);
            this.StatusLabel.Text = "Ready";
            // 
            // uiSplitContainer1
            // 
            this.uiSplitContainer1.Cursor = System.Windows.Forms.Cursors.Default;
            this.uiSplitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiSplitContainer1.Location = new System.Drawing.Point(0, 0);
            this.uiSplitContainer1.MinimumSize = new System.Drawing.Size(20, 20);
            this.uiSplitContainer1.Name = "uiSplitContainer1";
            this.uiSplitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.uiSplitContainer1.Panel1.Controls.Add(this.btnOpenFileList);
            this.uiSplitContainer1.Panel2.Controls.Add(this.imageGridView);
            this.uiSplitContainer1.Size = new System.Drawing.Size(554, 466);
            this.uiSplitContainer1.SplitterDistance = 65;
            this.uiSplitContainer1.SplitterWidth = 8;
            this.uiSplitContainer1.TabIndex = 2155;
            // 
            // btnOpenFileList
            // 
            this.btnOpenFileList.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(108)))), ((int)(((byte)(202)))));
            this.btnOpenFileList.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(108)))), ((int)(((byte)(202)))));
            this.btnOpenFileList.BorderRadius = 6;
            this.btnOpenFileList.BorderSize = 0;
            this.btnOpenFileList.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnOpenFileList.Design = RJCodeUI_M1.RJControls.ButtonDesign.Normal;
            this.btnOpenFileList.FlatAppearance.BorderSize = 0;
            this.btnOpenFileList.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpenFileList.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F);
            this.btnOpenFileList.ForeColor = System.Drawing.Color.White;
            this.btnOpenFileList.IconChar = FontAwesome.Sharp.IconChar.FolderOpen;
            this.btnOpenFileList.IconColor = System.Drawing.Color.White;
            this.btnOpenFileList.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnOpenFileList.IconSize = 34;
            this.btnOpenFileList.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnOpenFileList.Location = new System.Drawing.Point(6, 6);
            this.btnOpenFileList.Name = "btnOpenFileList";
            this.btnOpenFileList.Size = new System.Drawing.Size(542, 52);
            this.btnOpenFileList.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnOpenFileList.TabIndex = 2154;
            this.btnOpenFileList.Text = "이미지 폴더 열기";
            this.btnOpenFileList.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnOpenFileList.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnOpenFileList.UseVisualStyleBackColor = false;
            this.btnOpenFileList.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // imageGridView
            // 
            this.imageGridView.AllowUserToAddRows = false;
            this.imageGridView.AllowUserToDeleteRows = false;
            this.imageGridView.AllowUserToResizeRows = false;
            this.imageGridView.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(18)))), ((int)(((byte)(22)))), ((int)(((byte)(29)))));
            this.imageGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.imageGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.imageGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageGridView.Location = new System.Drawing.Point(0, 0);
            this.imageGridView.MultiSelect = false;
            this.imageGridView.Name = "imageGridView";
            this.imageGridView.ReadOnly = true;
            this.imageGridView.RowHeadersVisible = false;
            this.imageGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.imageGridView.Size = new System.Drawing.Size(554, 393);
            this.imageGridView.TabIndex = 2138;
            this.imageGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.imageGridView_CellClick);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButton1});
            this.toolStrip1.Location = new System.Drawing.Point(3, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(106, 25);
            this.toolStrip1.TabIndex = 1;
            // 
            // toolStripDropDownButton1
            // 
            this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.x96ToolStripMenuItem,
            this.x120ToolStripMenuItem,
            this.x200ToolStripMenuItem});
            this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            this.toolStripDropDownButton1.Size = new System.Drawing.Size(103, 22);
            this.toolStripDropDownButton1.Text = "Thumbnail Size";
            // 
            // x96ToolStripMenuItem
            // 
            this.x96ToolStripMenuItem.Name = "x96ToolStripMenuItem";
            this.x96ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.x96ToolStripMenuItem.Text = "96x96";
            this.x96ToolStripMenuItem.Click += new System.EventHandler(this.x96ToolStripMenuItem_Click);
            // 
            // x120ToolStripMenuItem
            // 
            this.x120ToolStripMenuItem.Name = "x120ToolStripMenuItem";
            this.x120ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.x120ToolStripMenuItem.Text = "120x120";
            this.x120ToolStripMenuItem.Click += new System.EventHandler(this.x120ToolStripMenuItem_Click);
            // 
            // x200ToolStripMenuItem
            // 
            this.x200ToolStripMenuItem.Name = "x200ToolStripMenuItem";
            this.x200ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.x200ToolStripMenuItem.Text = "200x200";
            this.x200ToolStripMenuItem.Click += new System.EventHandler(this.x200ToolStripMenuItem_Click);
            // 
            // FormImageList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScroll = true;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(27)))), ((int)(((byte)(34)))));
            this.ClientSize = new System.Drawing.Size(554, 513);
            this.Controls.Add(this.rjPanel2);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimumSize = new System.Drawing.Size(240, 260);
            this.Name = "FormImageList";
            this.Text = "이미지 리스트";
            this.Load += new System.EventHandler(this.Form_Load);
            this.VisibleChanged += new System.EventHandler(this.Form_VisibleChanged);
            this.rjPanel2.ResumeLayout(false);
            this.rjPanel1.ResumeLayout(false);
            this.toolStripContainer1.BottomToolStripPanel.ResumeLayout(false);
            this.toolStripContainer1.BottomToolStripPanel.PerformLayout();
            this.toolStripContainer1.ContentPanel.ResumeLayout(false);
            this.toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
            this.toolStripContainer1.TopToolStripPanel.PerformLayout();
            this.toolStripContainer1.ResumeLayout(false);
            this.toolStripContainer1.PerformLayout();
            this.StatusStrip.ResumeLayout(false);
            this.StatusStrip.PerformLayout();
            this.uiSplitContainer1.Panel1.ResumeLayout(false);
            this.uiSplitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.uiSplitContainer1)).EndInit();
            this.uiSplitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imageGridView)).EndInit();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private RJCodeUI_M1.RJControls.RJPanel rjPanel2;
        private RJCodeUI_M1.RJControls.RJPanel rjPanel1;
        private System.Windows.Forms.ToolStripContainer toolStripContainer1;
        private System.Windows.Forms.StatusStrip StatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel StatusLabel;
        private System.Windows.Forms.SplitContainer uiSplitContainer1;
        private RJCodeUI_M1.RJControls.RJButton btnOpenFileList;
        private RJCodeUI_M1.RJControls.RJDataGridView imageGridView;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripMenuItem x96ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem x120ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem x200ToolStripMenuItem;
    }
}
