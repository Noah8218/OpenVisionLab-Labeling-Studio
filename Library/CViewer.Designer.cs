
namespace MvcVisionSystem
{
    partial class CViewer
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeViewerResources();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 구성 요소 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.ddmImageMenu = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.ddmDelete = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.toolStripMenuItem9 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem10 = new System.Windows.Forms.ToolStripMenuItem();
            this.iconMenuItem1 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem2 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem3 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem6 = new FontAwesome.Sharp.IconMenuItem();
            this.ddmImageMenu.SuspendLayout();
            this.ddmDelete.SuspendLayout();
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 10;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // ddmImageMenu
            // 
            this.ddmImageMenu.ActiveMenuItem = false;
            this.ddmImageMenu.BackColor = System.Drawing.Color.White;
            this.ddmImageMenu.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddmImageMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.iconMenuItem1,
            this.iconMenuItem2,
            this.iconMenuItem3,
            this.iconMenuItem6});
            this.ddmImageMenu.Name = "ddmImageMenu";
            this.ddmImageMenu.OwnerIsMenuButton = false;
            this.ddmImageMenu.Size = new System.Drawing.Size(196, 136);
            // 
            // ddmDelete
            // 
            this.ddmDelete.ActiveMenuItem = false;
            this.ddmDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddmDelete.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem9,
            this.toolStripMenuItem10});
            this.ddmDelete.Name = "ddmDelete";
            this.ddmDelete.OwnerIsMenuButton = false;
            this.ddmDelete.RightToLeft = System.Windows.Forms.RightToLeft.Inherit;
            this.ddmDelete.Size = new System.Drawing.Size(124, 48);
            // 
            // toolStripMenuItem9
            // 
            this.toolStripMenuItem9.Name = "toolStripMenuItem9";
            this.toolStripMenuItem9.Size = new System.Drawing.Size(123, 22);
            this.toolStripMenuItem9.Text = "라벨 삭제";
            // 
            // toolStripMenuItem10
            // 
            this.toolStripMenuItem10.Name = "toolStripMenuItem10";
            this.toolStripMenuItem10.Size = new System.Drawing.Size(123, 22);
            this.toolStripMenuItem10.Text = "라벨 목록";
            // 
            // iconMenuItem1
            // 
            this.iconMenuItem1.IconChar = FontAwesome.Sharp.IconChar.FileImport;
            this.iconMenuItem1.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem1.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem1.Name = "iconMenuItem1";
            this.iconMenuItem1.Size = new System.Drawing.Size(195, 22);
            this.iconMenuItem1.Text = "이미지 열기";
            // 
            // iconMenuItem2
            // 
            this.iconMenuItem2.IconChar = FontAwesome.Sharp.IconChar.FileExport;
            this.iconMenuItem2.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem2.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem2.Name = "iconMenuItem2";
            this.iconMenuItem2.Size = new System.Drawing.Size(195, 22);
            this.iconMenuItem2.Text = "이미지 저장";
            // 
            // iconMenuItem3
            // 
            this.iconMenuItem3.IconChar = FontAwesome.Sharp.IconChar.FolderOpen;
            this.iconMenuItem3.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem3.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem3.Name = "iconMenuItem3";
            this.iconMenuItem3.Size = new System.Drawing.Size(195, 22);
            this.iconMenuItem3.Text = "폴더 열기";
            // 
            // iconMenuItem6
            // 
            this.iconMenuItem6.IconChar = FontAwesome.Sharp.IconChar.Plus;
            this.iconMenuItem6.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem6.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem6.Name = "iconMenuItem6";
            this.iconMenuItem6.Size = new System.Drawing.Size(195, 22);
            this.iconMenuItem6.Text = "십자선";
            // 
            this.ddmImageMenu.ResumeLayout(false);
            this.ddmDelete.ResumeLayout(false);

        }

        #endregion

        private RJCodeUI_M1.RJControls.RJDropdownMenu ddmImageMenu;
        private System.Windows.Forms.Timer timer1;
        private RJCodeUI_M1.RJControls.RJDropdownMenu ddmDelete;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem9;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem10;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem1;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem2;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem3;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem6;
    }
}
