﻿namespace MvcVisionSystem
{
    partial class FormVision_ClassMenu
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.btnCancel = new RJCodeUI_M1.RJControls.RJButton();
            this.btnCreate = new RJCodeUI_M1.RJControls.RJButton();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.rjPanel1 = new RJCodeUI_M1.RJControls.RJPanel();
            this.btnExportPath = new RJCodeUI_M1.RJControls.RJButton();
            this.tbOutputPath = new RJCodeUI_M1.RJControls.RJTextBox();
            this.rjLabel2 = new RJCodeUI_M1.RJControls.RJLabel();
            this.dgvImagesList = new RJCodeUI_M1.RJControls.RJDataGridView();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnDelete = new RJCodeUI_M1.RJControls.RJButton();
            this.rjLabel1 = new RJCodeUI_M1.RJControls.RJLabel();
            this.txtNames = new RJCodeUI_M1.RJControls.RJTextBox();
            this.pnlClientArea.SuspendLayout();
            this.rjPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvImagesList)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlClientArea
            // 
            this.pnlClientArea.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.pnlClientArea.Controls.Add(this.rjPanel1);
            this.pnlClientArea.Location = new System.Drawing.Point(1, 41);
            this.pnlClientArea.Size = new System.Drawing.Size(400, 491);
            // 
            // 
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnCancel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(110)))), ((int)(((byte)(134)))));
            this.btnCancel.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(110)))), ((int)(((byte)(134)))));
            this.btnCancel.BorderRadius = 10;
            this.btnCancel.BorderSize = 1;
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.Design = RJCodeUI_M1.RJControls.ButtonDesign.Custom;
            this.btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(79)))), ((int)(((byte)(82)))));
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnCancel.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(97)))), ((int)(((byte)(103)))), ((int)(((byte)(125)))));
            this.btnCancel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(91)))), ((int)(((byte)(96)))), ((int)(((byte)(117)))));
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.ForeColor = System.Drawing.Color.White;
            this.btnCancel.IconChar = FontAwesome.Sharp.IconChar.TimesCircle;
            this.btnCancel.IconColor = System.Drawing.Color.White;
            this.btnCancel.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnCancel.IconSize = 24;
            this.btnCancel.Location = new System.Drawing.Point(307, 453);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 35);
            this.btnCancel.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnCancel.TabIndex = 2153;
            this.btnCancel.Text = "닫기";
            this.btnCancel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnCancel.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += new System.EventHandler(this.btnNewCancel_Click);
            // 
            // btnCreate
            // 
            this.btnCreate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnCreate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(159)))), ((int)(((byte)(113)))));
            this.btnCreate.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnCreate.BorderRadius = 10;
            this.btnCreate.BorderSize = 1;
            this.btnCreate.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCreate.Design = RJCodeUI_M1.RJControls.ButtonDesign.Custom;
            this.btnCreate.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(159)))), ((int)(((byte)(113)))));
            this.btnCreate.FlatAppearance.BorderSize = 0;
            this.btnCreate.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(149)))), ((int)(((byte)(106)))));
            this.btnCreate.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(139)))), ((int)(((byte)(99)))));
            this.btnCreate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCreate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCreate.ForeColor = System.Drawing.Color.White;
            this.btnCreate.IconChar = FontAwesome.Sharp.IconChar.Plus;
            this.btnCreate.IconColor = System.Drawing.Color.White;
            this.btnCreate.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnCreate.IconSize = 24;
            this.btnCreate.Location = new System.Drawing.Point(202, 126);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(90, 35);
            this.btnCreate.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnCreate.TabIndex = 2154;
            this.btnCreate.Text = "추가";
            this.btnCreate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnCreate.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnCreate.UseVisualStyleBackColor = false;
            this.btnCreate.Click += new System.EventHandler(this.btnNewCreate_Click);
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // rjPanel1
            // 
            this.rjPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            this.rjPanel1.BorderRadius = 0;
            this.rjPanel1.Controls.Add(this.btnExportPath);
            this.rjPanel1.Controls.Add(this.tbOutputPath);
            this.rjPanel1.Controls.Add(this.rjLabel2);
            this.rjPanel1.Controls.Add(this.dgvImagesList);
            this.rjPanel1.Controls.Add(this.btnDelete);
            this.rjPanel1.Controls.Add(this.rjLabel1);
            this.rjPanel1.Controls.Add(this.txtNames);
            this.rjPanel1.Controls.Add(this.btnCancel);
            this.rjPanel1.Controls.Add(this.btnCreate);
            this.rjPanel1.Customizable = false;
            this.rjPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rjPanel1.Location = new System.Drawing.Point(0, 0);
            this.rjPanel1.Name = "rjPanel1";
            this.rjPanel1.Size = new System.Drawing.Size(400, 491);
            this.rjPanel1.TabIndex = 2155;
            // 
            // btnExportPath
            // 
            this.btnExportPath.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnExportPath.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnExportPath.BorderRadius = 10;
            this.btnExportPath.BorderSize = 1;
            this.btnExportPath.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
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
            this.btnExportPath.Location = new System.Drawing.Point(12, 127);
            this.btnExportPath.Name = "btnExportPath";
            this.btnExportPath.Size = new System.Drawing.Size(90, 35);
            this.btnExportPath.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnExportPath.TabIndex = 2161;
            this.btnExportPath.Text = "경로 설정";
            this.btnExportPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnExportPath.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnExportPath.UseVisualStyleBackColor = false;
            this.btnExportPath.Click += new System.EventHandler(this.btnExportPath_Click);
            // 
            // tbOutputPath
            // 
            this.tbOutputPath._Customizable = false;
            this.tbOutputPath.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.tbOutputPath.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbOutputPath.BorderFocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(162)))), ((int)(((byte)(247)))));
            this.tbOutputPath.BorderRadius = 10;
            this.tbOutputPath.BorderSize = 1;
            this.tbOutputPath.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.tbOutputPath.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbOutputPath.Location = new System.Drawing.Point(12, 89);
            this.tbOutputPath.MultiLine = false;
            this.tbOutputPath.Name = "tbOutputPath";
            this.tbOutputPath.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.tbOutputPath.PasswordChar = false;
            this.tbOutputPath.PlaceHolderColor = System.Drawing.Color.DarkGray;
            this.tbOutputPath.PlaceHolderText = null;
            this.tbOutputPath.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.tbOutputPath.Size = new System.Drawing.Size(376, 31);
            this.tbOutputPath.Style = RJCodeUI_M1.RJControls.TextBoxStyle.MatteBorder;
            this.tbOutputPath.TabIndex = 2160;
            // 
            // rjLabel2
            // 
            this.rjLabel2.AutoSize = true;
            this.rjLabel2.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel2.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel2.LinkLabel = false;
            this.rjLabel2.Location = new System.Drawing.Point(9, 70);
            this.rjLabel2.Name = "rjLabel2";
            this.rjLabel2.Size = new System.Drawing.Size(57, 16);
            this.rjLabel2.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel2.TabIndex = 2159;
            this.rjLabel2.Text = "저장 경로";
            // 
            // dgvImagesList
            // 
            this.dgvImagesList.AllowUserToAddRows = false;
            this.dgvImagesList.AllowUserToDeleteRows = false;
            this.dgvImagesList.AllowUserToResizeRows = false;
            this.dgvImagesList.AlternatingRowsColor = System.Drawing.Color.Empty;
            this.dgvImagesList.AlternatingRowsColorApply = false;
            this.dgvImagesList.BackgroundColor = System.Drawing.Color.White;
            this.dgvImagesList.BorderRadius = 13;
            this.dgvImagesList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvImagesList.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            this.dgvImagesList.ColumnHeaderColor = System.Drawing.Color.MediumPurple;
            this.dgvImagesList.ColumnHeaderFont = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dgvImagesList.ColumnHeaderHeight = 40;
            this.dgvImagesList.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.Color.MediumPurple;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle4.Padding = new System.Windows.Forms.Padding(15, 0, 0, 0);
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvImagesList.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.dgvImagesList.ColumnHeadersHeight = 40;
            this.dgvImagesList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvImagesList.ColumnHeaderTextColor = System.Drawing.Color.White;
            this.dgvImagesList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column2,
            this.Column1});
            this.dgvImagesList.ColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;
            this.dgvImagesList.Customizable = false;
            this.dgvImagesList.DgvBackColor = System.Drawing.Color.White;
            this.dgvImagesList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgvImagesList.EnableHeadersVisualStyles = false;
            this.dgvImagesList.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.dgvImagesList.Location = new System.Drawing.Point(12, 167);
            this.dgvImagesList.Name = "dgvImagesList";
            this.dgvImagesList.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.dgvImagesList.RowHeaderColor = System.Drawing.Color.WhiteSmoke;
            this.dgvImagesList.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(213)))), ((int)(((byte)(199)))), ((int)(((byte)(241)))));
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvImagesList.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.dgvImagesList.RowHeadersVisible = false;
            this.dgvImagesList.RowHeadersWidth = 30;
            this.dgvImagesList.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgvImagesList.RowHeight = 40;
            this.dgvImagesList.RowsColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.Color.Gray;
            dataGridViewCellStyle6.Padding = new System.Windows.Forms.Padding(15, 0, 0, 0);
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(213)))), ((int)(((byte)(199)))), ((int)(((byte)(241)))));
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.Color.Gray;
            this.dgvImagesList.RowsDefaultCellStyle = dataGridViewCellStyle6;
            this.dgvImagesList.RowsTextColor = System.Drawing.Color.Gray;
            this.dgvImagesList.RowTemplate.Height = 40;
            this.dgvImagesList.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(213)))), ((int)(((byte)(199)))), ((int)(((byte)(241)))));
            this.dgvImagesList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvImagesList.SelectionTextColor = System.Drawing.Color.Gray;
            this.dgvImagesList.Size = new System.Drawing.Size(376, 280);
            this.dgvImagesList.TabIndex = 2158;
            this.dgvImagesList.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvImagesList_CellClick);
            this.dgvImagesList.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvImagesList_CellContentClick);
            // 
            // Column2
            // 
            this.Column2.HeaderText = "No";
            this.Column2.Name = "Column2";
            this.Column2.Width = 225;
            // 
            // Column1
            // 
            this.Column1.HeaderText = "클래스";
            this.Column1.Name = "Column1";
            this.Column1.Width = 225;
            // 
            // btnDelete
            // 
            this.btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDelete.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(79)))), ((int)(((byte)(82)))));
            this.btnDelete.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(79)))), ((int)(((byte)(82)))));
            this.btnDelete.BorderRadius = 10;
            this.btnDelete.BorderSize = 1;
            this.btnDelete.Design = RJCodeUI_M1.RJControls.ButtonDesign.Delete;
            this.btnDelete.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(79)))), ((int)(((byte)(82)))));
            this.btnDelete.FlatAppearance.BorderSize = 0;
            this.btnDelete.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(219)))), ((int)(((byte)(74)))), ((int)(((byte)(77)))));
            this.btnDelete.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(205)))), ((int)(((byte)(69)))), ((int)(((byte)(72)))));
            this.btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDelete.ForeColor = System.Drawing.Color.White;
            this.btnDelete.IconChar = FontAwesome.Sharp.IconChar.TrashAlt;
            this.btnDelete.IconColor = System.Drawing.Color.White;
            this.btnDelete.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnDelete.IconSize = 24;
            this.btnDelete.Location = new System.Drawing.Point(298, 126);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(90, 35);
            this.btnDelete.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnDelete.TabIndex = 2157;
            this.btnDelete.Text = "삭제";
            this.btnDelete.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnDelete.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnDelete.UseVisualStyleBackColor = false;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // rjLabel1
            // 
            this.rjLabel1.AutoSize = true;
            this.rjLabel1.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel1.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel1.LinkLabel = false;
            this.rjLabel1.Location = new System.Drawing.Point(9, 17);
            this.rjLabel1.Name = "rjLabel1";
            this.rjLabel1.Size = new System.Drawing.Size(56, 16);
            this.rjLabel1.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel1.TabIndex = 2156;
            this.rjLabel1.Text = "클래스";
            // 
            // txtNames
            // 
            this.txtNames._Customizable = false;
            this.txtNames.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.txtNames.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.txtNames.BorderFocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(162)))), ((int)(((byte)(247)))));
            this.txtNames.BorderRadius = 10;
            this.txtNames.BorderSize = 1;
            this.txtNames.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.txtNames.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.txtNames.Location = new System.Drawing.Point(12, 36);
            this.txtNames.MultiLine = false;
            this.txtNames.Name = "txtNames";
            this.txtNames.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.txtNames.PasswordChar = false;
            this.txtNames.PlaceHolderColor = System.Drawing.Color.DarkGray;
            this.txtNames.PlaceHolderText = null;
            this.txtNames.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.txtNames.Size = new System.Drawing.Size(376, 31);
            this.txtNames.Style = RJCodeUI_M1.RJControls.TextBoxStyle.MatteBorder;
            this.txtNames.TabIndex = 2155;
            // 
            // FormVision_ClassMenu
            // 
            this._DesktopPanelSize = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.BorderSize = 1;
            this.Caption = "클래스 설정";
            this.ClientSize = new System.Drawing.Size(402, 533);
            this.Name = "FormVision_ClassMenu";
            this.Padding = new System.Windows.Forms.Padding(1);
            this.Resizable = false;
            this.Text = "클래스 설정";
            this.Load += new System.EventHandler(this.FormSettings_Camera_Load);
            this.pnlClientArea.ResumeLayout(false);
            this.rjPanel1.ResumeLayout(false);
            this.rjPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvImagesList)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private RJCodeUI_M1.RJControls.RJButton btnCancel;
        private RJCodeUI_M1.RJControls.RJButton btnCreate;
        private System.Windows.Forms.Timer timer1;
        private RJCodeUI_M1.RJControls.RJPanel rjPanel1;
        private RJCodeUI_M1.RJControls.RJButton btnDelete;
        private RJCodeUI_M1.RJControls.RJLabel rjLabel1;
        private RJCodeUI_M1.RJControls.RJTextBox txtNames;
        private RJCodeUI_M1.RJControls.RJDataGridView dgvImagesList;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private RJCodeUI_M1.RJControls.RJTextBox tbOutputPath;
        private RJCodeUI_M1.RJControls.RJLabel rjLabel2;
        private RJCodeUI_M1.RJControls.RJButton btnExportPath;
    }
}
