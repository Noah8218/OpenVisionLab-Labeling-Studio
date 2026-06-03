namespace MvcVisionSystem
{
    partial class FormMetroFrame
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        { 
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMetroFrame));
            this.timerAlarm = new System.Windows.Forms.Timer(this.components);
            this.pnMDI = new System.Windows.Forms.Panel();
            this.pnFormMain = new MetroFramework.Controls.MetroPanel();
            this.OperatorPanel = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.timerConnection = new System.Windows.Forms.Timer(this.components);
            this.pnStatusBar = new RJCodeUI_M1.RJControls.RJPanel();
            this.lbVersion = new RJCodeUI_M1.RJControls.RJLabel();
            this.pnlTitleBar = new RJCodeUI_M1.RJControls.RJPanel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.lbExportPath = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnExportPath = new RJCodeUI_M1.RJControls.RJButton();
            this.dmUserOptions = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.miMyProfile = new FontAwesome.Sharp.IconMenuItem();
            this.miSettings = new FontAwesome.Sharp.IconMenuItem();
            this.miTermsCond = new FontAwesome.Sharp.IconMenuItem();
            this.miHelp = new FontAwesome.Sharp.IconMenuItem();
            this.miLogout = new FontAwesome.Sharp.IconMenuItem();
            this.miExit = new FontAwesome.Sharp.IconMenuItem();
            this.btnClassSave = new RJCodeUI_M1.RJControls.RJButton();
            this.btnClassMenu = new RJCodeUI_M1.RJControls.RJButton();
            this.btnClassInfer = new RJCodeUI_M1.RJControls.RJButton();
            this.btnClassTrain = new RJCodeUI_M1.RJControls.RJButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lbSelectImageName = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnPreviousPage = new RJCodeUI_M1.RJControls.RJButton();
            this.btnNextPage = new RJCodeUI_M1.RJControls.RJButton();
            this.cbClassMenu = new RJCodeUI_M1.RJControls.RJComboBox();
            this.rjLabel13 = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnUserOptions = new RJCodeUI_M1.RJControls.RJButton();
            this.btnScreenCapture = new RJCodeUI_M1.RJControls.RJButton();
            this.btnCerrar = new System.Windows.Forms.Button();
            this.btnMinimizar = new System.Windows.Forms.Button();
            this.ddmDevice = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.iconMenuItem2 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem3 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem4 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem5 = new FontAwesome.Sharp.IconMenuItem();
            this.iconMenuItem6 = new FontAwesome.Sharp.IconMenuItem();
            this.ddmCapture = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.iconMenuItem1 = new FontAwesome.Sharp.IconMenuItem();
            this.pnMDI.SuspendLayout();
            this.pnFormMain.SuspendLayout();
            this.pnStatusBar.SuspendLayout();
            this.pnlTitleBar.SuspendLayout();
            this.panel2.SuspendLayout();
            this.dmUserOptions.SuspendLayout();
            this.panel1.SuspendLayout();
            this.ddmDevice.SuspendLayout();
            this.ddmCapture.SuspendLayout();
            this.SuspendLayout();
            // 
            // timerAlarm
            // 
            this.timerAlarm.Enabled = true;
            this.timerAlarm.Interval = 500;
            this.timerAlarm.Tick += new System.EventHandler(this.timerAlarm_Tick);
            // 
            // pnMDI
            // 
            this.pnMDI.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.pnMDI.Controls.Add(this.pnFormMain);
            this.pnMDI.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnMDI.Location = new System.Drawing.Point(0, 49);
            this.pnMDI.Margin = new System.Windows.Forms.Padding(0);
            this.pnMDI.Name = "pnMDI";
            this.pnMDI.Padding = new System.Windows.Forms.Padding(0, 0, 0, 33);
            this.pnMDI.Size = new System.Drawing.Size(1924, 997);
            this.pnMDI.TabIndex = 1258;
            // 
            // pnFormMain
            // 
            this.pnFormMain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.pnFormMain.Controls.Add(this.OperatorPanel);
            this.pnFormMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnFormMain.HorizontalScrollbarBarColor = true;
            this.pnFormMain.HorizontalScrollbarHighlightOnWheel = false;
            this.pnFormMain.HorizontalScrollbarSize = 10;
            this.pnFormMain.Location = new System.Drawing.Point(0, 0);
            this.pnFormMain.Margin = new System.Windows.Forms.Padding(0);
            this.pnFormMain.Name = "pnFormMain";
            this.pnFormMain.Size = new System.Drawing.Size(1924, 964);
            this.pnFormMain.TabIndex = 895;
            this.pnFormMain.VerticalScrollbarBarColor = true;
            this.pnFormMain.VerticalScrollbarHighlightOnWheel = false;
            this.pnFormMain.VerticalScrollbarSize = 10;
            // 
            // OperatorPanel
            // 
            this.OperatorPanel.BackColor = System.Drawing.Color.Black;
            this.OperatorPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OperatorPanel.Location = new System.Drawing.Point(0, 0);
            this.OperatorPanel.Name = "OperatorPanel";
            this.OperatorPanel.Size = new System.Drawing.Size(1924, 964);
            this.OperatorPanel.TabIndex = 2138;
            // 
            // panel3
            // 
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Margin = new System.Windows.Forms.Padding(2, 1, 2, 1);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1910, 47);
            this.panel3.TabIndex = 1949;
            // 
            // timerConnection
            // 
            this.timerConnection.Enabled = true;
            this.timerConnection.Tick += new System.EventHandler(this.timerConnection_Tick);
            // 
            // pnStatusBar
            // 
            this.pnStatusBar.BackColor = System.Drawing.Color.Black;
            this.pnStatusBar.BorderRadius = 0;
            this.pnStatusBar.Controls.Add(this.lbVersion);
            this.pnStatusBar.Customizable = true;
            this.pnStatusBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnStatusBar.Location = new System.Drawing.Point(0, 1013);
            this.pnStatusBar.Name = "pnStatusBar";
            this.pnStatusBar.Size = new System.Drawing.Size(1924, 33);
            this.pnStatusBar.TabIndex = 0;
            // 
            // lbVersion
            // 
            this.lbVersion.AutoSize = true;
            this.lbVersion.BackColor = System.Drawing.Color.Transparent;
            this.lbVersion.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbVersion.Dock = System.Windows.Forms.DockStyle.Right;
            this.lbVersion.Font = new System.Drawing.Font("Verdana", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbVersion.ForeColor = System.Drawing.Color.White;
            this.lbVersion.LinkLabel = false;
            this.lbVersion.Location = new System.Drawing.Point(1745, 0);
            this.lbVersion.Name = "lbVersion";
            this.lbVersion.Size = new System.Drawing.Size(179, 18);
            this.lbVersion.Style = RJCodeUI_M1.RJControls.LabelStyle.Custom;
            this.lbVersion.TabIndex = 2122;
            this.lbVersion.Text = "Version 2.5 - 211007";
            // 
            // pnlTitleBar
            // 
            this.pnlTitleBar.BackColor = System.Drawing.Color.Black;
            this.pnlTitleBar.BorderRadius = 0;
            this.pnlTitleBar.Controls.Add(this.panel2);
            this.pnlTitleBar.Controls.Add(this.btnClassInfer);
            this.pnlTitleBar.Controls.Add(this.btnClassTrain);
            this.pnlTitleBar.Controls.Add(this.panel1);
            this.pnlTitleBar.Controls.Add(this.cbClassMenu);
            this.pnlTitleBar.Controls.Add(this.rjLabel13);
            this.pnlTitleBar.Controls.Add(this.btnUserOptions);
            this.pnlTitleBar.Controls.Add(this.btnScreenCapture);
            this.pnlTitleBar.Controls.Add(this.btnCerrar);
            this.pnlTitleBar.Controls.Add(this.btnMinimizar);
            this.pnlTitleBar.Customizable = true;
            this.pnlTitleBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitleBar.Location = new System.Drawing.Point(0, 0);
            this.pnlTitleBar.Name = "pnlTitleBar";
            this.pnlTitleBar.Size = new System.Drawing.Size(1924, 49);
            this.pnlTitleBar.TabIndex = 1260;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.lbExportPath);
            this.panel2.Controls.Add(this.btnExportPath);
            this.panel2.Controls.Add(this.btnClassSave);
            this.panel2.Controls.Add(this.btnClassMenu);
            this.panel2.Location = new System.Drawing.Point(177, 3);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(528, 45);
            this.panel2.TabIndex = 2641;
            // 
            // lbExportPath
            // 
            this.lbExportPath.BackColor = System.Drawing.Color.Transparent;
            this.lbExportPath.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbExportPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lbExportPath.Font = new System.Drawing.Font("Verdana", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbExportPath.ForeColor = System.Drawing.Color.White;
            this.lbExportPath.LinkLabel = false;
            this.lbExportPath.Location = new System.Drawing.Point(189, 0);
            this.lbExportPath.Name = "lbExportPath";
            this.lbExportPath.Size = new System.Drawing.Size(339, 45);
            this.lbExportPath.Style = RJCodeUI_M1.RJControls.LabelStyle.Custom;
            this.lbExportPath.TabIndex = 2136;
            this.lbExportPath.Text = "----------------------------------";
            this.lbExportPath.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnExportPath
            // 
            this.btnExportPath.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnExportPath.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnExportPath.BorderRadius = 0;
            this.btnExportPath.BorderSize = 1;
            this.btnExportPath.ContextMenuStrip = this.dmUserOptions;
            this.btnExportPath.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnExportPath.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnExportPath.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnExportPath.FlatAppearance.BorderSize = 0;
            this.btnExportPath.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnExportPath.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnExportPath.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnExportPath.ForeColor = System.Drawing.Color.White;
            this.btnExportPath.IconChar = FontAwesome.Sharp.IconChar.FileExport;
            this.btnExportPath.IconColor = System.Drawing.Color.White;
            this.btnExportPath.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnExportPath.IconSize = 25;
            this.btnExportPath.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnExportPath.Location = new System.Drawing.Point(126, 0);
            this.btnExportPath.Name = "btnExportPath";
            this.btnExportPath.Size = new System.Drawing.Size(63, 45);
            this.btnExportPath.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnExportPath.TabIndex = 11;
            this.btnExportPath.Text = "경로 설정";
            this.btnExportPath.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnExportPath.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnExportPath.UseVisualStyleBackColor = false;
            this.btnExportPath.Click += new System.EventHandler(this.btnExportPath_Click);
            // 
            // dmUserOptions
            // 
            this.dmUserOptions.ActiveMenuItem = false;
            this.dmUserOptions.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dmUserOptions.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMyProfile,
            this.miSettings,
            this.miTermsCond,
            this.miHelp,
            this.miLogout,
            this.miExit});
            this.dmUserOptions.Name = "dmUserOptions";
            this.dmUserOptions.OwnerIsMenuButton = false;
            this.dmUserOptions.Size = new System.Drawing.Size(182, 136);
            // 
            // miMyProfile
            // 
            this.miMyProfile.IconChar = FontAwesome.Sharp.IconChar.UserAlt;
            this.miMyProfile.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(85)))), ((int)(((byte)(230)))));
            this.miMyProfile.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.miMyProfile.IconSize = 21;
            this.miMyProfile.Name = "miMyProfile";
            this.miMyProfile.Size = new System.Drawing.Size(181, 22);
            this.miMyProfile.Text = "My Profile";
            // 
            // miSettings
            // 
            this.miSettings.IconChar = FontAwesome.Sharp.IconChar.Tools;
            this.miSettings.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(47)))), ((int)(((byte)(168)))), ((int)(((byte)(210)))));
            this.miSettings.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.miSettings.IconSize = 21;
            this.miSettings.Name = "miSettings";
            this.miSettings.Size = new System.Drawing.Size(181, 22);
            this.miSettings.Text = "Settings";
            this.miSettings.Click += new System.EventHandler(this.miSettings_Click);
            // 
            // miTermsCond
            // 
            this.miTermsCond.IconChar = FontAwesome.Sharp.IconChar.ShieldAlt;
            this.miTermsCond.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(132)))), ((int)(((byte)(235)))));
            this.miTermsCond.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.miTermsCond.IconSize = 21;
            this.miTermsCond.Name = "miTermsCond";
            this.miTermsCond.Size = new System.Drawing.Size(181, 22);
            this.miTermsCond.Text = "Terms and Cond";
            // 
            // miHelp
            // 
            this.miHelp.IconChar = FontAwesome.Sharp.IconChar.QuestionCircle;
            this.miHelp.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(96)))), ((int)(((byte)(112)))));
            this.miHelp.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.miHelp.IconSize = 21;
            this.miHelp.Name = "miHelp";
            this.miHelp.Size = new System.Drawing.Size(181, 22);
            this.miHelp.Text = "Help";
            // 
            // miLogout
            // 
            this.miLogout.IconChar = FontAwesome.Sharp.IconChar.SignOutAlt;
            this.miLogout.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(73)))), ((int)(((byte)(84)))), ((int)(((byte)(228)))));
            this.miLogout.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.miLogout.IconSize = 21;
            this.miLogout.Name = "miLogout";
            this.miLogout.Size = new System.Drawing.Size(181, 22);
            this.miLogout.Text = "Logout";
            // 
            // miExit
            // 
            this.miExit.IconChar = FontAwesome.Sharp.IconChar.PowerOff;
            this.miExit.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(37)))), ((int)(((byte)(118)))));
            this.miExit.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.miExit.IconSize = 21;
            this.miExit.Name = "miExit";
            this.miExit.Size = new System.Drawing.Size(181, 22);
            this.miExit.Text = "Exit";
            // 
            // btnClassSave
            // 
            this.btnClassSave.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassSave.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassSave.BorderRadius = 0;
            this.btnClassSave.BorderSize = 1;
            this.btnClassSave.ContextMenuStrip = this.dmUserOptions;
            this.btnClassSave.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnClassSave.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnClassSave.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnClassSave.FlatAppearance.BorderSize = 0;
            this.btnClassSave.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnClassSave.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnClassSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClassSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClassSave.ForeColor = System.Drawing.Color.White;
            this.btnClassSave.IconChar = FontAwesome.Sharp.IconChar.Save;
            this.btnClassSave.IconColor = System.Drawing.Color.White;
            this.btnClassSave.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnClassSave.IconSize = 25;
            this.btnClassSave.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnClassSave.Location = new System.Drawing.Point(63, 0);
            this.btnClassSave.Name = "btnClassSave";
            this.btnClassSave.Size = new System.Drawing.Size(63, 45);
            this.btnClassSave.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnClassSave.TabIndex = 10;
            this.btnClassSave.Text = "저장";
            this.btnClassSave.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnClassSave.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnClassSave.UseVisualStyleBackColor = false;
            this.btnClassSave.Click += new System.EventHandler(this.btnClassSave_Click);
            // 
            // btnClassMenu
            // 
            this.btnClassMenu.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassMenu.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassMenu.BorderRadius = 0;
            this.btnClassMenu.BorderSize = 1;
            this.btnClassMenu.ContextMenuStrip = this.dmUserOptions;
            this.btnClassMenu.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnClassMenu.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnClassMenu.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnClassMenu.FlatAppearance.BorderSize = 0;
            this.btnClassMenu.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnClassMenu.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnClassMenu.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClassMenu.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClassMenu.ForeColor = System.Drawing.Color.White;
            this.btnClassMenu.IconChar = FontAwesome.Sharp.IconChar.ClipboardList;
            this.btnClassMenu.IconColor = System.Drawing.Color.White;
            this.btnClassMenu.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnClassMenu.IconSize = 25;
            this.btnClassMenu.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnClassMenu.Location = new System.Drawing.Point(0, 0);
            this.btnClassMenu.Name = "btnClassMenu";
            this.btnClassMenu.Size = new System.Drawing.Size(63, 45);
            this.btnClassMenu.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnClassMenu.TabIndex = 10;
            this.btnClassMenu.Text = "클래스 설정";
            this.btnClassMenu.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnClassMenu.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnClassMenu.UseVisualStyleBackColor = false;
            this.btnClassMenu.Click += new System.EventHandler(this.btnClassMenu_Click);
            // 
            // btnClassInfer
            // 
            this.btnClassInfer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassInfer.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassInfer.BorderRadius = 0;
            this.btnClassInfer.BorderSize = 1;
            this.btnClassInfer.ContextMenuStrip = this.dmUserOptions;
            this.btnClassInfer.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnClassInfer.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnClassInfer.FlatAppearance.BorderSize = 0;
            this.btnClassInfer.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnClassInfer.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnClassInfer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClassInfer.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClassInfer.ForeColor = System.Drawing.Color.White;
            this.btnClassInfer.IconChar = FontAwesome.Sharp.IconChar.Exclamation;
            this.btnClassInfer.IconColor = System.Drawing.Color.White;
            this.btnClassInfer.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnClassInfer.IconSize = 25;
            this.btnClassInfer.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnClassInfer.Location = new System.Drawing.Point(1286, 1);
            this.btnClassInfer.Name = "btnClassInfer";
            this.btnClassInfer.Size = new System.Drawing.Size(63, 45);
            this.btnClassInfer.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnClassInfer.TabIndex = 12;
            this.btnClassInfer.Text = "추론";
            this.btnClassInfer.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnClassInfer.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnClassInfer.UseVisualStyleBackColor = false;
            this.btnClassInfer.Click += new System.EventHandler(this.btnClassInfer_Click);
            // 
            // btnClassTrain
            // 
            this.btnClassTrain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassTrain.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassTrain.BorderRadius = 0;
            this.btnClassTrain.BorderSize = 1;
            this.btnClassTrain.ContextMenuStrip = this.dmUserOptions;
            this.btnClassTrain.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnClassTrain.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnClassTrain.FlatAppearance.BorderSize = 0;
            this.btnClassTrain.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnClassTrain.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnClassTrain.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClassTrain.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClassTrain.ForeColor = System.Drawing.Color.White;
            this.btnClassTrain.IconChar = FontAwesome.Sharp.IconChar.Clock;
            this.btnClassTrain.IconColor = System.Drawing.Color.White;
            this.btnClassTrain.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnClassTrain.IconSize = 25;
            this.btnClassTrain.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnClassTrain.Location = new System.Drawing.Point(1222, 1);
            this.btnClassTrain.Name = "btnClassTrain";
            this.btnClassTrain.Size = new System.Drawing.Size(63, 45);
            this.btnClassTrain.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnClassTrain.TabIndex = 11;
            this.btnClassTrain.Text = "훈련";
            this.btnClassTrain.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnClassTrain.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnClassTrain.UseVisualStyleBackColor = false;
            this.btnClassTrain.Click += new System.EventHandler(this.btnClassTrain_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lbSelectImageName);
            this.panel1.Controls.Add(this.btnPreviousPage);
            this.panel1.Controls.Add(this.btnNextPage);
            this.panel1.Location = new System.Drawing.Point(711, 7);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(450, 36);
            this.panel1.TabIndex = 2640;
            // 
            // lbSelectImageName
            // 
            this.lbSelectImageName.BackColor = System.Drawing.Color.Transparent;
            this.lbSelectImageName.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbSelectImageName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lbSelectImageName.Font = new System.Drawing.Font("Verdana", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbSelectImageName.ForeColor = System.Drawing.Color.White;
            this.lbSelectImageName.LinkLabel = false;
            this.lbSelectImageName.Location = new System.Drawing.Point(40, 0);
            this.lbSelectImageName.Name = "lbSelectImageName";
            this.lbSelectImageName.Size = new System.Drawing.Size(370, 36);
            this.lbSelectImageName.Style = RJCodeUI_M1.RJControls.LabelStyle.Custom;
            this.lbSelectImageName.TabIndex = 2136;
            this.lbSelectImageName.Text = "----------------";
            this.lbSelectImageName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnPreviousPage
            // 
            this.btnPreviousPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnPreviousPage.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnPreviousPage.BorderRadius = 10;
            this.btnPreviousPage.BorderSize = 1;
            this.btnPreviousPage.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnPreviousPage.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnPreviousPage.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnPreviousPage.FlatAppearance.BorderSize = 0;
            this.btnPreviousPage.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnPreviousPage.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnPreviousPage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPreviousPage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnPreviousPage.ForeColor = System.Drawing.Color.White;
            this.btnPreviousPage.IconChar = FontAwesome.Sharp.IconChar.StepBackward;
            this.btnPreviousPage.IconColor = System.Drawing.Color.White;
            this.btnPreviousPage.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnPreviousPage.IconSize = 20;
            this.btnPreviousPage.Location = new System.Drawing.Point(0, 0);
            this.btnPreviousPage.Name = "btnPreviousPage";
            this.btnPreviousPage.Size = new System.Drawing.Size(40, 36);
            this.btnPreviousPage.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnPreviousPage.TabIndex = 11;
            this.btnPreviousPage.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnPreviousPage.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnPreviousPage.UseVisualStyleBackColor = false;
            // 
            // btnNextPage
            // 
            this.btnNextPage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnNextPage.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnNextPage.BorderRadius = 10;
            this.btnNextPage.BorderSize = 1;
            this.btnNextPage.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnNextPage.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnNextPage.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnNextPage.FlatAppearance.BorderSize = 0;
            this.btnNextPage.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnNextPage.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnNextPage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNextPage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnNextPage.ForeColor = System.Drawing.Color.White;
            this.btnNextPage.IconChar = FontAwesome.Sharp.IconChar.StepForward;
            this.btnNextPage.IconColor = System.Drawing.Color.White;
            this.btnNextPage.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnNextPage.IconSize = 20;
            this.btnNextPage.Location = new System.Drawing.Point(410, 0);
            this.btnNextPage.Name = "btnNextPage";
            this.btnNextPage.Size = new System.Drawing.Size(40, 36);
            this.btnNextPage.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnNextPage.TabIndex = 10;
            this.btnNextPage.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnNextPage.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnNextPage.UseVisualStyleBackColor = false;
            // 
            // cbClassMenu
            // 
            this.cbClassMenu.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.None;
            this.cbClassMenu.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.None;
            this.cbClassMenu.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.cbClassMenu.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.cbClassMenu.BorderRadius = 10;
            this.cbClassMenu.BorderSize = 1;
            this.cbClassMenu.Customizable = false;
            this.cbClassMenu.DataSource = null;
            this.cbClassMenu.DropDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            this.cbClassMenu.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbClassMenu.DropDownTextColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.cbClassMenu.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.cbClassMenu.Location = new System.Drawing.Point(7, 16);
            this.cbClassMenu.Name = "cbClassMenu";
            this.cbClassMenu.Padding = new System.Windows.Forms.Padding(2);
            this.cbClassMenu.SelectedIndex = -1;
            this.cbClassMenu.Size = new System.Drawing.Size(164, 32);
            this.cbClassMenu.Style = RJCodeUI_M1.RJControls.ControlStyle.Glass;
            this.cbClassMenu.TabIndex = 2;
            this.cbClassMenu.Texts = "";
            this.cbClassMenu.OnSelectedIndexChanged += new System.EventHandler(this.cbClassMenu_OnSelectedIndexChanged);
            // 
            // rjLabel13
            // 
            this.rjLabel13.AutoSize = true;
            this.rjLabel13.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel13.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel13.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel13.LinkLabel = false;
            this.rjLabel13.Location = new System.Drawing.Point(13, 1);
            this.rjLabel13.Name = "rjLabel13";
            this.rjLabel13.Size = new System.Drawing.Size(67, 16);
            this.rjLabel13.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel13.TabIndex = 16;
            this.rjLabel13.Text = "클래스 매뉴";
            // 
            // btnUserOptions
            // 
            this.btnUserOptions.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnUserOptions.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnUserOptions.BorderRadius = 0;
            this.btnUserOptions.BorderSize = 1;
            this.btnUserOptions.ContextMenuStrip = this.dmUserOptions;
            this.btnUserOptions.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnUserOptions.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnUserOptions.FlatAppearance.BorderSize = 0;
            this.btnUserOptions.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnUserOptions.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnUserOptions.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnUserOptions.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnUserOptions.ForeColor = System.Drawing.Color.White;
            this.btnUserOptions.IconChar = FontAwesome.Sharp.IconChar.Cog;
            this.btnUserOptions.IconColor = System.Drawing.Color.White;
            this.btnUserOptions.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnUserOptions.IconSize = 25;
            this.btnUserOptions.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnUserOptions.Location = new System.Drawing.Point(1728, 2);
            this.btnUserOptions.Name = "btnUserOptions";
            this.btnUserOptions.Size = new System.Drawing.Size(63, 45);
            this.btnUserOptions.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnUserOptions.TabIndex = 9;
            this.btnUserOptions.Text = "설정";
            this.btnUserOptions.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnUserOptions.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnUserOptions.UseVisualStyleBackColor = false;
            this.btnUserOptions.Click += new System.EventHandler(this.btnClassSetting_Click);
            // 
            // btnScreenCapture
            // 
            this.btnScreenCapture.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnScreenCapture.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnScreenCapture.BorderRadius = 0;
            this.btnScreenCapture.BorderSize = 1;
            this.btnScreenCapture.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnScreenCapture.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnScreenCapture.FlatAppearance.BorderSize = 0;
            this.btnScreenCapture.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnScreenCapture.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnScreenCapture.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnScreenCapture.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnScreenCapture.ForeColor = System.Drawing.Color.White;
            this.btnScreenCapture.IconChar = FontAwesome.Sharp.IconChar.Camera;
            this.btnScreenCapture.IconColor = System.Drawing.Color.White;
            this.btnScreenCapture.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnScreenCapture.IconSize = 25;
            this.btnScreenCapture.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnScreenCapture.Location = new System.Drawing.Point(1792, 2);
            this.btnScreenCapture.Name = "btnScreenCapture";
            this.btnScreenCapture.Size = new System.Drawing.Size(63, 45);
            this.btnScreenCapture.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnScreenCapture.TabIndex = 8;
            this.btnScreenCapture.Text = "화면 캡쳐";
            this.btnScreenCapture.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnScreenCapture.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnScreenCapture.UseVisualStyleBackColor = false;
            this.btnScreenCapture.Click += new System.EventHandler(this.btnScreenCapture_Click);
            this.btnScreenCapture.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnScreenCapture_MouseUp);
            // 
            // btnCerrar
            // 
            this.btnCerrar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCerrar.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.btnCerrar.FlatAppearance.BorderSize = 0;
            this.btnCerrar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCerrar.ForeColor = System.Drawing.Color.White;
            this.btnCerrar.Image = ((System.Drawing.Image)(resources.GetObject("btnCerrar.Image")));
            this.btnCerrar.Location = new System.Drawing.Point(1888, 8);
            this.btnCerrar.Name = "btnCerrar";
            this.btnCerrar.Size = new System.Drawing.Size(30, 30);
            this.btnCerrar.TabIndex = 2638;
            this.btnCerrar.UseVisualStyleBackColor = true;
            this.btnCerrar.Click += new System.EventHandler(this.btnCerrar_Click);
            // 
            // btnMinimizar
            // 
            this.btnMinimizar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnMinimizar.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.btnMinimizar.FlatAppearance.BorderSize = 0;
            this.btnMinimizar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMinimizar.ForeColor = System.Drawing.Color.White;
            this.btnMinimizar.Image = ((System.Drawing.Image)(resources.GetObject("btnMinimizar.Image")));
            this.btnMinimizar.Location = new System.Drawing.Point(1859, 8);
            this.btnMinimizar.Name = "btnMinimizar";
            this.btnMinimizar.Size = new System.Drawing.Size(30, 30);
            this.btnMinimizar.TabIndex = 2639;
            this.btnMinimizar.UseVisualStyleBackColor = true;
            this.btnMinimizar.Click += new System.EventHandler(this.btnMinimizar_Click);
            // 
            // ddmDevice
            // 
            this.ddmDevice.ActiveMenuItem = false;
            this.ddmDevice.BackColor = System.Drawing.Color.White;
            this.ddmDevice.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddmDevice.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.iconMenuItem2,
            this.iconMenuItem3,
            this.iconMenuItem4,
            this.iconMenuItem5,
            this.iconMenuItem6});
            this.ddmDevice.Name = "ddmDevice";
            this.ddmDevice.OwnerIsMenuButton = false;
            this.ddmDevice.Size = new System.Drawing.Size(134, 114);
            // 
            // iconMenuItem2
            // 
            this.iconMenuItem2.IconChar = FontAwesome.Sharp.IconChar.Camera;
            this.iconMenuItem2.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem2.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem2.Name = "iconMenuItem2";
            this.iconMenuItem2.Size = new System.Drawing.Size(133, 22);
            this.iconMenuItem2.Text = "CAMERA";
            // 
            // iconMenuItem3
            // 
            this.iconMenuItem3.IconChar = FontAwesome.Sharp.IconChar.Lightbulb;
            this.iconMenuItem3.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem3.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem3.Name = "iconMenuItem3";
            this.iconMenuItem3.Size = new System.Drawing.Size(133, 22);
            this.iconMenuItem3.Text = "LIGHT";
            // 
            // iconMenuItem4
            // 
            this.iconMenuItem4.IconChar = FontAwesome.Sharp.IconChar.None;
            this.iconMenuItem4.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem4.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem4.Name = "iconMenuItem4";
            this.iconMenuItem4.Size = new System.Drawing.Size(133, 22);
            this.iconMenuItem4.Text = "PLC";
            this.iconMenuItem4.Visible = false;
            // 
            // iconMenuItem5
            // 
            this.iconMenuItem5.IconChar = FontAwesome.Sharp.IconChar.None;
            this.iconMenuItem5.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem5.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem5.Name = "iconMenuItem5";
            this.iconMenuItem5.Size = new System.Drawing.Size(133, 22);
            this.iconMenuItem5.Text = "I/O";
            this.iconMenuItem5.Visible = false;
            // 
            // iconMenuItem6
            // 
            this.iconMenuItem6.IconChar = FontAwesome.Sharp.IconChar.Cog;
            this.iconMenuItem6.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem6.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem6.Name = "iconMenuItem6";
            this.iconMenuItem6.Size = new System.Drawing.Size(133, 22);
            this.iconMenuItem6.Text = "UTIL";
            // 
            // ddmCapture
            // 
            this.ddmCapture.ActiveMenuItem = false;
            this.ddmCapture.BackColor = System.Drawing.Color.White;
            this.ddmCapture.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddmCapture.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.iconMenuItem1});
            this.ddmCapture.Name = "ddmCapture";
            this.ddmCapture.OwnerIsMenuButton = false;
            this.ddmCapture.Size = new System.Drawing.Size(155, 26);
            // 
            // iconMenuItem1
            // 
            this.iconMenuItem1.IconChar = FontAwesome.Sharp.IconChar.FolderOpen;
            this.iconMenuItem1.IconColor = System.Drawing.Color.Black;
            this.iconMenuItem1.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.iconMenuItem1.Name = "iconMenuItem1";
            this.iconMenuItem1.Size = new System.Drawing.Size(154, 22);
            this.iconMenuItem1.Text = "Show Folder";
            // 
            // FormMetroFrame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(1924, 1046);
            this.Controls.Add(this.pnStatusBar);
            this.Controls.Add(this.pnMDI);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.Name = "FormMetroFrame";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMetroFrame_FormClosing);
            this.Load += new System.EventHandler(this.FormMetroFrame_Load);
            this.Shown += new System.EventHandler(this.FormMetroFrame_Shown);
            this.pnMDI.ResumeLayout(false);
            this.pnFormMain.ResumeLayout(false);
            this.pnStatusBar.ResumeLayout(false);
            this.pnStatusBar.PerformLayout();
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.dmUserOptions.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ddmDevice.ResumeLayout(false);
            this.ddmCapture.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Timer timerAlarm;
        private System.Windows.Forms.Panel pnMDI;
        private System.Windows.Forms.Panel panel3;
        private RJCodeUI_M1.RJControls.RJDropdownMenu ddmDevice;
        private RJCodeUI_M1.RJControls.RJDropdownMenu ddmCapture;
        private MetroFramework.Controls.MetroPanel pnFormMain;
        private RJCodeUI_M1.RJControls.RJPanel pnlTitleBar;
        private RJCodeUI_M1.RJControls.RJButton btnScreenCapture;
        private System.Windows.Forms.Panel OperatorPanel;
        private RJCodeUI_M1.RJControls.RJLabel lbVersion;
        private RJCodeUI_M1.RJControls.RJPanel pnStatusBar;
        private System.Windows.Forms.Timer timerConnection;
        private RJCodeUI_M1.RJControls.RJDropdownMenu dmUserOptions;
        private FontAwesome.Sharp.IconMenuItem miMyProfile;
        private FontAwesome.Sharp.IconMenuItem miSettings;
        private FontAwesome.Sharp.IconMenuItem miTermsCond;
        private FontAwesome.Sharp.IconMenuItem miHelp;
        private FontAwesome.Sharp.IconMenuItem miLogout;
        private FontAwesome.Sharp.IconMenuItem miExit;
        private System.Windows.Forms.Button btnCerrar;
        private System.Windows.Forms.Button btnMinimizar;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem1;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem2;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem3;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem4;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem5;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem6;
        private RJCodeUI_M1.RJControls.RJButton btnUserOptions;
        private RJCodeUI_M1.RJControls.RJButton btnPreviousPage;
        private RJCodeUI_M1.RJControls.RJButton btnNextPage;
        private RJCodeUI_M1.RJControls.RJButton btnClassMenu;
        private RJCodeUI_M1.RJControls.RJLabel rjLabel13;
        private RJCodeUI_M1.RJControls.RJComboBox cbClassMenu;
        private RJCodeUI_M1.RJControls.RJButton btnClassSave;
        private System.Windows.Forms.Panel panel1;
        private RJCodeUI_M1.RJControls.RJLabel lbSelectImageName;
        private RJCodeUI_M1.RJControls.RJButton btnClassInfer;
        private RJCodeUI_M1.RJControls.RJButton btnClassTrain;
        private RJCodeUI_M1.RJControls.RJButton btnExportPath;
        private System.Windows.Forms.Panel panel2;
        private RJCodeUI_M1.RJControls.RJLabel lbExportPath;
    }
}