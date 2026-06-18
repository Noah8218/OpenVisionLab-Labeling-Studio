﻿namespace MvcVisionSystem
{
    partial class FormMainFrame
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMainFrame));
            this.timerAlarm = new System.Windows.Forms.Timer(this.components);
            this.mainWorkspacePanel = new System.Windows.Forms.Panel();
            this.mainContentPanel = new System.Windows.Forms.Panel();
            this.teachingHostPanel = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.timerConnection = new System.Windows.Forms.Timer(this.components);
            this.pnStatusBar = new RJCodeUI_M1.RJControls.RJPanel();
            this.lbVersion = new RJCodeUI_M1.RJControls.RJLabel();
            this.lbDatasetStatus = new RJCodeUI_M1.RJControls.RJLabel();
            this.lbPythonStatus = new RJCodeUI_M1.RJControls.RJLabel();
            this.pnlTitleBar = new RJCodeUI_M1.RJControls.RJPanel();
            this.outputPathPanel = new System.Windows.Forms.Panel();
            this.lbExportPath = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnOutputPath = new RJCodeUI_M1.RJControls.RJButton();
            this.dmUserOptions = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.miMyProfile = new FontAwesome.Sharp.IconMenuItem();
            this.miSettings = new FontAwesome.Sharp.IconMenuItem();
            this.miTermsCond = new FontAwesome.Sharp.IconMenuItem();
            this.miHelp = new FontAwesome.Sharp.IconMenuItem();
            this.miLogout = new FontAwesome.Sharp.IconMenuItem();
            this.miExit = new FontAwesome.Sharp.IconMenuItem();
            this.btnSaveProject = new RJCodeUI_M1.RJControls.RJButton();
            this.btnClassSettings = new RJCodeUI_M1.RJControls.RJButton();
            this.btnDetectCurrentImage = new RJCodeUI_M1.RJControls.RJButton();
            this.btnStartTraining = new RJCodeUI_M1.RJControls.RJButton();
            this.imageNavigatorPanel = new System.Windows.Forms.Panel();
            this.lbSelectImageName = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnPreviousPage = new RJCodeUI_M1.RJControls.RJButton();
            this.btnNextPage = new RJCodeUI_M1.RJControls.RJButton();
            this.cbClassMenu = new RJCodeUI_M1.RJControls.RJComboBox();
            this.lblClassSelector = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnUserOptions = new RJCodeUI_M1.RJControls.RJButton();
            this.btnScreenCapture = new RJCodeUI_M1.RJControls.RJButton();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.ddmCapture = new RJCodeUI_M1.RJControls.RJDropdownMenu(this.components);
            this.iconMenuItem1 = new FontAwesome.Sharp.IconMenuItem();
            this.mainWorkspacePanel.SuspendLayout();
            this.mainContentPanel.SuspendLayout();
            this.pnStatusBar.SuspendLayout();
            this.pnlTitleBar.SuspendLayout();
            this.outputPathPanel.SuspendLayout();
            this.dmUserOptions.SuspendLayout();
            this.imageNavigatorPanel.SuspendLayout();
            this.ddmCapture.SuspendLayout();
            this.SuspendLayout();
            // 
            // timerAlarm
            // 
            this.timerAlarm.Interval = 500;
            this.timerAlarm.Tick += new System.EventHandler(this.timerAlarm_Tick);
            // 
            // mainWorkspacePanel
            // 
            this.mainWorkspacePanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.mainWorkspacePanel.Controls.Add(this.mainContentPanel);
            this.mainWorkspacePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainWorkspacePanel.Location = new System.Drawing.Point(0, 49);
            this.mainWorkspacePanel.Margin = new System.Windows.Forms.Padding(0);
            this.mainWorkspacePanel.Name = "mainWorkspacePanel";
            this.mainWorkspacePanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 33);
            this.mainWorkspacePanel.Size = new System.Drawing.Size(1924, 997);
            this.mainWorkspacePanel.TabIndex = 1258;
            // 
            // mainContentPanel
            // 
            this.mainContentPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.mainContentPanel.Controls.Add(this.teachingHostPanel);
            this.mainContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainContentPanel.Location = new System.Drawing.Point(0, 0);
            this.mainContentPanel.Margin = new System.Windows.Forms.Padding(0);
            this.mainContentPanel.Name = "mainContentPanel";
            this.mainContentPanel.Size = new System.Drawing.Size(1924, 964);
            this.mainContentPanel.TabIndex = 895;
            // 
            // teachingHostPanel
            // 
            this.teachingHostPanel.BackColor = System.Drawing.Color.Black;
            this.teachingHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.teachingHostPanel.Location = new System.Drawing.Point(0, 0);
            this.teachingHostPanel.Name = "teachingHostPanel";
            this.teachingHostPanel.Size = new System.Drawing.Size(1924, 964);
            this.teachingHostPanel.TabIndex = 2138;
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
            this.timerConnection.Interval = 1000;
            this.timerConnection.Tick += new System.EventHandler(this.timerConnection_Tick);
            // 
            // pnStatusBar
            // 
            this.pnStatusBar.BackColor = System.Drawing.Color.Black;
            this.pnStatusBar.BorderRadius = 0;
            this.pnStatusBar.Controls.Add(this.lbPythonStatus);
            this.pnStatusBar.Controls.Add(this.lbDatasetStatus);
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
            // lbDatasetStatus
            // 
            this.lbDatasetStatus.BackColor = System.Drawing.Color.Transparent;
            this.lbDatasetStatus.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbDatasetStatus.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbDatasetStatus.Font = new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbDatasetStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(220)))), ((int)(((byte)(235)))));
            this.lbDatasetStatus.LinkLabel = false;
            this.lbDatasetStatus.Location = new System.Drawing.Point(0, 0);
            this.lbDatasetStatus.Name = "lbDatasetStatus";
            this.lbDatasetStatus.Padding = new System.Windows.Forms.Padding(8, 6, 4, 0);
            this.lbDatasetStatus.Size = new System.Drawing.Size(420, 33);
            this.lbDatasetStatus.Style = RJCodeUI_M1.RJControls.LabelStyle.Custom;
            this.lbDatasetStatus.TabIndex = 2123;
            this.lbDatasetStatus.Text = "데이터셋 확인 필요";
            // 
            // lbPythonStatus
            // 
            this.lbPythonStatus.BackColor = System.Drawing.Color.Transparent;
            this.lbPythonStatus.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lbPythonStatus.Dock = System.Windows.Forms.DockStyle.Left;
            this.lbPythonStatus.Font = new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbPythonStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(220)))), ((int)(((byte)(235)))));
            this.lbPythonStatus.LinkLabel = false;
            this.lbPythonStatus.Location = new System.Drawing.Point(420, 0);
            this.lbPythonStatus.Name = "lbPythonStatus";
            this.lbPythonStatus.Padding = new System.Windows.Forms.Padding(8, 6, 4, 0);
            this.lbPythonStatus.Size = new System.Drawing.Size(340, 33);
            this.lbPythonStatus.Style = RJCodeUI_M1.RJControls.LabelStyle.Custom;
            this.lbPythonStatus.TabIndex = 2124;
            this.lbPythonStatus.Text = "PYTHON IDLE/NO CLIENT";
            // 
            // pnlTitleBar
            // 
            this.pnlTitleBar.BackColor = System.Drawing.Color.Black;
            this.pnlTitleBar.BorderRadius = 0;
            this.pnlTitleBar.Controls.Add(this.outputPathPanel);
            this.pnlTitleBar.Controls.Add(this.btnDetectCurrentImage);
            this.pnlTitleBar.Controls.Add(this.btnStartTraining);
            this.pnlTitleBar.Controls.Add(this.imageNavigatorPanel);
            this.pnlTitleBar.Controls.Add(this.cbClassMenu);
            this.pnlTitleBar.Controls.Add(this.lblClassSelector);
            this.pnlTitleBar.Controls.Add(this.btnUserOptions);
            this.pnlTitleBar.Controls.Add(this.btnScreenCapture);
            this.pnlTitleBar.Controls.Add(this.btnClose);
            this.pnlTitleBar.Controls.Add(this.btnMinimize);
            this.pnlTitleBar.Customizable = true;
            this.pnlTitleBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitleBar.Location = new System.Drawing.Point(0, 0);
            this.pnlTitleBar.Name = "pnlTitleBar";
            this.pnlTitleBar.Size = new System.Drawing.Size(1924, 49);
            this.pnlTitleBar.TabIndex = 1260;
            // 
            // outputPathPanel
            // 
            this.outputPathPanel.Controls.Add(this.lbExportPath);
            this.outputPathPanel.Controls.Add(this.btnOutputPath);
            this.outputPathPanel.Controls.Add(this.btnSaveProject);
            this.outputPathPanel.Controls.Add(this.btnClassSettings);
            this.outputPathPanel.Location = new System.Drawing.Point(177, 3);
            this.outputPathPanel.Name = "outputPathPanel";
            this.outputPathPanel.Size = new System.Drawing.Size(528, 45);
            this.outputPathPanel.TabIndex = 2641;
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
            // btnOutputPath
            // 
            this.btnOutputPath.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnOutputPath.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnOutputPath.BorderRadius = 0;
            this.btnOutputPath.BorderSize = 1;
            this.btnOutputPath.ContextMenuStrip = this.dmUserOptions;
            this.btnOutputPath.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnOutputPath.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnOutputPath.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnOutputPath.FlatAppearance.BorderSize = 0;
            this.btnOutputPath.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnOutputPath.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnOutputPath.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOutputPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnOutputPath.ForeColor = System.Drawing.Color.White;
            this.btnOutputPath.IconChar = FontAwesome.Sharp.IconChar.FileExport;
            this.btnOutputPath.IconColor = System.Drawing.Color.White;
            this.btnOutputPath.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnOutputPath.IconSize = 25;
            this.btnOutputPath.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnOutputPath.Location = new System.Drawing.Point(126, 0);
            this.btnOutputPath.Name = "btnOutputPath";
            this.btnOutputPath.Size = new System.Drawing.Size(63, 45);
            this.btnOutputPath.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnOutputPath.TabIndex = 11;
            this.btnOutputPath.Text = "경로 설정";
            this.btnOutputPath.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnOutputPath.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnOutputPath.UseVisualStyleBackColor = false;
            this.btnOutputPath.Click += new System.EventHandler(this.btnOutputPath_Click);
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
            // btnSaveProject
            // 
            this.btnSaveProject.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnSaveProject.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnSaveProject.BorderRadius = 0;
            this.btnSaveProject.BorderSize = 1;
            this.btnSaveProject.ContextMenuStrip = this.dmUserOptions;
            this.btnSaveProject.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnSaveProject.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnSaveProject.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnSaveProject.FlatAppearance.BorderSize = 0;
            this.btnSaveProject.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnSaveProject.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnSaveProject.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveProject.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSaveProject.ForeColor = System.Drawing.Color.White;
            this.btnSaveProject.IconChar = FontAwesome.Sharp.IconChar.Save;
            this.btnSaveProject.IconColor = System.Drawing.Color.White;
            this.btnSaveProject.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnSaveProject.IconSize = 25;
            this.btnSaveProject.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnSaveProject.Location = new System.Drawing.Point(63, 0);
            this.btnSaveProject.Name = "btnSaveProject";
            this.btnSaveProject.Size = new System.Drawing.Size(63, 45);
            this.btnSaveProject.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnSaveProject.TabIndex = 10;
            this.btnSaveProject.Text = "저장";
            this.btnSaveProject.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnSaveProject.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnSaveProject.UseVisualStyleBackColor = false;
            this.btnSaveProject.Click += new System.EventHandler(this.btnSaveProject_Click);
            // 
            // btnClassSettings
            // 
            this.btnClassSettings.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassSettings.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnClassSettings.BorderRadius = 0;
            this.btnClassSettings.BorderSize = 1;
            this.btnClassSettings.ContextMenuStrip = this.dmUserOptions;
            this.btnClassSettings.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnClassSettings.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnClassSettings.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnClassSettings.FlatAppearance.BorderSize = 0;
            this.btnClassSettings.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnClassSettings.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnClassSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClassSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClassSettings.ForeColor = System.Drawing.Color.White;
            this.btnClassSettings.IconChar = FontAwesome.Sharp.IconChar.ClipboardList;
            this.btnClassSettings.IconColor = System.Drawing.Color.White;
            this.btnClassSettings.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnClassSettings.IconSize = 25;
            this.btnClassSettings.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnClassSettings.Location = new System.Drawing.Point(0, 0);
            this.btnClassSettings.Name = "btnClassSettings";
            this.btnClassSettings.Size = new System.Drawing.Size(63, 45);
            this.btnClassSettings.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnClassSettings.TabIndex = 10;
            this.btnClassSettings.Text = "클래스 설정";
            this.btnClassSettings.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnClassSettings.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnClassSettings.UseVisualStyleBackColor = false;
            this.btnClassSettings.Click += new System.EventHandler(this.btnClassSettings_Click);
            // 
            // btnDetectCurrentImage
            // 
            this.btnDetectCurrentImage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnDetectCurrentImage.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnDetectCurrentImage.BorderRadius = 0;
            this.btnDetectCurrentImage.BorderSize = 1;
            this.btnDetectCurrentImage.ContextMenuStrip = this.dmUserOptions;
            this.btnDetectCurrentImage.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnDetectCurrentImage.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnDetectCurrentImage.FlatAppearance.BorderSize = 0;
            this.btnDetectCurrentImage.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnDetectCurrentImage.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnDetectCurrentImage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDetectCurrentImage.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDetectCurrentImage.ForeColor = System.Drawing.Color.White;
            this.btnDetectCurrentImage.IconChar = FontAwesome.Sharp.IconChar.Exclamation;
            this.btnDetectCurrentImage.IconColor = System.Drawing.Color.White;
            this.btnDetectCurrentImage.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnDetectCurrentImage.IconSize = 25;
            this.btnDetectCurrentImage.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnDetectCurrentImage.Location = new System.Drawing.Point(1286, 1);
            this.btnDetectCurrentImage.Name = "btnDetectCurrentImage";
            this.btnDetectCurrentImage.Size = new System.Drawing.Size(63, 45);
            this.btnDetectCurrentImage.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnDetectCurrentImage.TabIndex = 12;
            this.btnDetectCurrentImage.Text = "추론";
            this.btnDetectCurrentImage.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnDetectCurrentImage.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnDetectCurrentImage.UseVisualStyleBackColor = false;
            this.btnDetectCurrentImage.Click += new System.EventHandler(this.btnDetectCurrentImage_Click);
            // 
            // btnStartTraining
            // 
            this.btnStartTraining.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnStartTraining.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnStartTraining.BorderRadius = 0;
            this.btnStartTraining.BorderSize = 1;
            this.btnStartTraining.ContextMenuStrip = this.dmUserOptions;
            this.btnStartTraining.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnStartTraining.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnStartTraining.FlatAppearance.BorderSize = 0;
            this.btnStartTraining.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnStartTraining.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnStartTraining.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStartTraining.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnStartTraining.ForeColor = System.Drawing.Color.White;
            this.btnStartTraining.IconChar = FontAwesome.Sharp.IconChar.Clock;
            this.btnStartTraining.IconColor = System.Drawing.Color.White;
            this.btnStartTraining.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnStartTraining.IconSize = 25;
            this.btnStartTraining.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnStartTraining.Location = new System.Drawing.Point(1222, 1);
            this.btnStartTraining.Name = "btnStartTraining";
            this.btnStartTraining.Size = new System.Drawing.Size(63, 45);
            this.btnStartTraining.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnStartTraining.TabIndex = 11;
            this.btnStartTraining.Text = "훈련";
            this.btnStartTraining.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnStartTraining.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.btnStartTraining.UseVisualStyleBackColor = false;
            this.btnStartTraining.Click += new System.EventHandler(this.btnStartTraining_Click);
            // 
            // imageNavigatorPanel
            // 
            this.imageNavigatorPanel.Controls.Add(this.lbSelectImageName);
            this.imageNavigatorPanel.Controls.Add(this.btnPreviousPage);
            this.imageNavigatorPanel.Controls.Add(this.btnNextPage);
            this.imageNavigatorPanel.Location = new System.Drawing.Point(711, 7);
            this.imageNavigatorPanel.Name = "imageNavigatorPanel";
            this.imageNavigatorPanel.Size = new System.Drawing.Size(450, 36);
            this.imageNavigatorPanel.TabIndex = 2640;
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
            // lblClassSelector
            // 
            this.lblClassSelector.AutoSize = true;
            this.lblClassSelector.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.lblClassSelector.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.lblClassSelector.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.lblClassSelector.LinkLabel = false;
            this.lblClassSelector.Location = new System.Drawing.Point(13, 1);
            this.lblClassSelector.Name = "lblClassSelector";
            this.lblClassSelector.Size = new System.Drawing.Size(67, 16);
            this.lblClassSelector.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.lblClassSelector.TabIndex = 16;
            this.lblClassSelector.Text = "클래스 메뉴";
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
            this.btnUserOptions.Click += new System.EventHandler(this.btnPythonSettings_Click);
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
            // btnClose
            // 
            this.btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Image = ((System.Drawing.Image)(resources.GetObject("btnClose.Image")));
            this.btnClose.Location = new System.Drawing.Point(1888, 8);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(30, 30);
            this.btnClose.TabIndex = 2638;
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnMinimize
            // 
            this.btnMinimize.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnMinimize.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.btnMinimize.FlatAppearance.BorderSize = 0;
            this.btnMinimize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMinimize.ForeColor = System.Drawing.Color.White;
            this.btnMinimize.Image = ((System.Drawing.Image)(resources.GetObject("btnMinimize.Image")));
            this.btnMinimize.Location = new System.Drawing.Point(1859, 8);
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.Size = new System.Drawing.Size(30, 30);
            this.btnMinimize.TabIndex = 2639;
            this.btnMinimize.UseVisualStyleBackColor = true;
            this.btnMinimize.Click += new System.EventHandler(this.btnMinimize_Click);
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
            this.iconMenuItem1.Text = "캡처 폴더 열기";
            // 
            // FormMainFrame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(1924, 1046);
            this.Controls.Add(this.pnStatusBar);
            this.Controls.Add(this.mainWorkspacePanel);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.Name = "FormMainFrame";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMainFrame_FormClosing);
            this.Load += new System.EventHandler(this.FormMainFrame_Load);
            this.Shown += new System.EventHandler(this.FormMainFrame_Shown);
            this.mainWorkspacePanel.ResumeLayout(false);
            this.mainContentPanel.ResumeLayout(false);
            this.pnStatusBar.ResumeLayout(false);
            this.pnStatusBar.PerformLayout();
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.outputPathPanel.ResumeLayout(false);
            this.dmUserOptions.ResumeLayout(false);
            this.imageNavigatorPanel.ResumeLayout(false);
            this.ddmCapture.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Timer timerAlarm;
        private System.Windows.Forms.Panel mainWorkspacePanel;
        private System.Windows.Forms.Panel panel3;
        private RJCodeUI_M1.RJControls.RJDropdownMenu ddmCapture;
        private System.Windows.Forms.Panel mainContentPanel;
        private RJCodeUI_M1.RJControls.RJPanel pnlTitleBar;
        private RJCodeUI_M1.RJControls.RJButton btnScreenCapture;
        private System.Windows.Forms.Panel teachingHostPanel;
        private RJCodeUI_M1.RJControls.RJLabel lbVersion;
        private RJCodeUI_M1.RJControls.RJLabel lbDatasetStatus;
        private RJCodeUI_M1.RJControls.RJLabel lbPythonStatus;
        private RJCodeUI_M1.RJControls.RJPanel pnStatusBar;
        private System.Windows.Forms.Timer timerConnection;
        private RJCodeUI_M1.RJControls.RJDropdownMenu dmUserOptions;
        private FontAwesome.Sharp.IconMenuItem miMyProfile;
        private FontAwesome.Sharp.IconMenuItem miSettings;
        private FontAwesome.Sharp.IconMenuItem miTermsCond;
        private FontAwesome.Sharp.IconMenuItem miHelp;
        private FontAwesome.Sharp.IconMenuItem miLogout;
        private FontAwesome.Sharp.IconMenuItem miExit;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnMinimize;
        private FontAwesome.Sharp.IconMenuItem iconMenuItem1;
        private RJCodeUI_M1.RJControls.RJButton btnUserOptions;
        private RJCodeUI_M1.RJControls.RJButton btnPreviousPage;
        private RJCodeUI_M1.RJControls.RJButton btnNextPage;
        private RJCodeUI_M1.RJControls.RJButton btnClassSettings;
        private RJCodeUI_M1.RJControls.RJLabel lblClassSelector;
        private RJCodeUI_M1.RJControls.RJComboBox cbClassMenu;
        private RJCodeUI_M1.RJControls.RJButton btnSaveProject;
        private System.Windows.Forms.Panel imageNavigatorPanel;
        private RJCodeUI_M1.RJControls.RJLabel lbSelectImageName;
        private RJCodeUI_M1.RJControls.RJButton btnDetectCurrentImage;
        private RJCodeUI_M1.RJControls.RJButton btnStartTraining;
        private RJCodeUI_M1.RJControls.RJButton btnOutputPath;
        private System.Windows.Forms.Panel outputPathPanel;
        private RJCodeUI_M1.RJControls.RJLabel lbExportPath;
    }
}

