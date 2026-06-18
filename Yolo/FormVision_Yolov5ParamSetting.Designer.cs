namespace RJCodeUI_M1.RJForms
{
    partial class FormVision_Yolov5ParamSetting
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
            this.rjLabel1 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel2 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel3 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel4 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel5 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel6 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel7 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel8 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel9 = new RJCodeUI_M1.RJControls.RJLabel();
            this.btnApplyChanges = new RJCodeUI_M1.RJControls.RJButton();
            this.rjLabel15 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjLabel16 = new RJCodeUI_M1.RJControls.RJLabel();
            this.rjButton1 = new RJCodeUI_M1.RJControls.RJButton();
            this.tbImageSize = new RJCodeUI_M1.RJControls.RJTextBox();
            this.tbbatch = new RJCodeUI_M1.RJControls.RJTextBox();
            this.tbepoch = new RJCodeUI_M1.RJControls.RJTextBox();
            this.cbweight = new RJCodeUI_M1.RJControls.RJComboBox();
            this.cbcfg = new RJCodeUI_M1.RJControls.RJComboBox();
            this.pnlClientArea.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlClientArea
            // 
            this.pnlClientArea.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.pnlClientArea.Controls.Add(this.cbcfg);
            this.pnlClientArea.Controls.Add(this.cbweight);
            this.pnlClientArea.Controls.Add(this.tbepoch);
            this.pnlClientArea.Controls.Add(this.tbbatch);
            this.pnlClientArea.Controls.Add(this.tbImageSize);
            this.pnlClientArea.Controls.Add(this.rjButton1);
            this.pnlClientArea.Controls.Add(this.rjLabel15);
            this.pnlClientArea.Controls.Add(this.rjLabel16);
            this.pnlClientArea.Controls.Add(this.btnApplyChanges);
            this.pnlClientArea.Controls.Add(this.rjLabel8);
            this.pnlClientArea.Controls.Add(this.rjLabel9);
            this.pnlClientArea.Controls.Add(this.rjLabel6);
            this.pnlClientArea.Controls.Add(this.rjLabel7);
            this.pnlClientArea.Controls.Add(this.rjLabel5);
            this.pnlClientArea.Controls.Add(this.rjLabel4);
            this.pnlClientArea.Controls.Add(this.rjLabel3);
            this.pnlClientArea.Controls.Add(this.rjLabel2);
            this.pnlClientArea.Controls.Add(this.rjLabel1);
            this.pnlClientArea.Location = new System.Drawing.Point(2, 42);
            this.pnlClientArea.Size = new System.Drawing.Size(591, 665);
            this.pnlClientArea.Text = "CustomTrackBar Enabled";
            // 
            // rjLabel1
            // 
            this.rjLabel1.AutoSize = true;
            this.rjLabel1.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel1.Font = new System.Drawing.Font("Verdana", 14F);
            this.rjLabel1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.rjLabel1.LinkLabel = false;
            this.rjLabel1.Location = new System.Drawing.Point(23, 26);
            this.rjLabel1.Name = "rjLabel1";
            this.rjLabel1.Size = new System.Drawing.Size(249, 23);
            this.rjLabel1.Style = RJCodeUI_M1.RJControls.LabelStyle.Title;
            this.rjLabel1.TabIndex = 1;
            this.rjLabel1.Text = "YOLO Training Settings";
            // 
            // rjLabel2
            // 
            this.rjLabel2.AutoSize = true;
            this.rjLabel2.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel2.Font = new System.Drawing.Font("Verdana", 12F);
            this.rjLabel2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(105)))), ((int)(((byte)(103)))), ((int)(((byte)(105)))));
            this.rjLabel2.LinkLabel = false;
            this.rjLabel2.Location = new System.Drawing.Point(24, 63);
            this.rjLabel2.Name = "rjLabel2";
            this.rjLabel2.Size = new System.Drawing.Size(100, 18);
            this.rjLabel2.Style = RJCodeUI_M1.RJControls.LabelStyle.Subtitle;
            this.rjLabel2.TabIndex = 2;
            this.rjLabel2.Text = "Image Size";
            // 
            // rjLabel3
            // 
            this.rjLabel3.AutoSize = true;
            this.rjLabel3.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel3.Font = new System.Drawing.Font("Verdana", 12F);
            this.rjLabel3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(105)))), ((int)(((byte)(103)))), ((int)(((byte)(105)))));
            this.rjLabel3.LinkLabel = false;
            this.rjLabel3.Location = new System.Drawing.Point(24, 154);
            this.rjLabel3.Name = "rjLabel3";
            this.rjLabel3.Size = new System.Drawing.Size(60, 18);
            this.rjLabel3.Style = RJCodeUI_M1.RJControls.LabelStyle.Subtitle;
            this.rjLabel3.TabIndex = 4;
            this.rjLabel3.Text = "Batch ";
            // 
            // rjLabel4
            // 
            this.rjLabel4.AutoSize = true;
            this.rjLabel4.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel4.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel4.LinkLabel = false;
            this.rjLabel4.Location = new System.Drawing.Point(27, 173);
            this.rjLabel4.MaximumSize = new System.Drawing.Size(500, 0);
            this.rjLabel4.Name = "rjLabel4";
            this.rjLabel4.Size = new System.Drawing.Size(493, 48);
            this.rjLabel4.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel4.TabIndex = 6;
            this.rjLabel4.Text = "배치 크기를 설정하며, 한 번에 처리할 이미지의 수를 나타냅니다. 배치 크기는 GPU 메모리와 성능에 영향을 미치며, 일반적으로 크기가 클수록 학" +
    "습은 더 빠르지만 메모리 요구사항도 더 많아집니다.";
            // 
            // rjLabel5
            // 
            this.rjLabel5.AutoSize = true;
            this.rjLabel5.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel5.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel5.LinkLabel = false;
            this.rjLabel5.Location = new System.Drawing.Point(27, 82);
            this.rjLabel5.MaximumSize = new System.Drawing.Size(500, 0);
            this.rjLabel5.Name = "rjLabel5";
            this.rjLabel5.Size = new System.Drawing.Size(490, 32);
            this.rjLabel5.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel5.TabIndex = 7;
            this.rjLabel5.Text = "모델이 학습하는 동안 사용할 입력 이미지의 크기를 설정합니다. 이 값은 너비와 높이를 동일하게 설정합니다 (이 경우에는 320x320 픽셀)";
            // 
            // rjLabel6
            // 
            this.rjLabel6.AutoSize = true;
            this.rjLabel6.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel6.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel6.LinkLabel = false;
            this.rjLabel6.Location = new System.Drawing.Point(27, 389);
            this.rjLabel6.MaximumSize = new System.Drawing.Size(500, 0);
            this.rjLabel6.Name = "rjLabel6";
            this.rjLabel6.Size = new System.Drawing.Size(498, 48);
            this.rjLabel6.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel6.TabIndex = 9;
            this.rjLabel6.Text = "사용할 모델 구조를 정의하는 설정 파일을 지정합니다. YOLOv5는 여러 버전의 모델 크기를 제공하며 (yolov5s, yolov5m, yolov" +
    "5l, yolov5x), yolov5m은 이들 중 중간 크기의 모델을 나타냅니다.";
            // 
            // rjLabel7
            // 
            this.rjLabel7.AutoSize = true;
            this.rjLabel7.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel7.Font = new System.Drawing.Font("Verdana", 12F);
            this.rjLabel7.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(105)))), ((int)(((byte)(103)))), ((int)(((byte)(105)))));
            this.rjLabel7.LinkLabel = false;
            this.rjLabel7.Location = new System.Drawing.Point(24, 370);
            this.rjLabel7.Name = "rjLabel7";
            this.rjLabel7.Size = new System.Drawing.Size(35, 18);
            this.rjLabel7.Style = RJCodeUI_M1.RJControls.LabelStyle.Subtitle;
            this.rjLabel7.TabIndex = 8;
            this.rjLabel7.Text = "Cfg";
            // 
            // rjLabel8
            // 
            this.rjLabel8.AutoSize = true;
            this.rjLabel8.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel8.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel8.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel8.LinkLabel = false;
            this.rjLabel8.Location = new System.Drawing.Point(27, 491);
            this.rjLabel8.MaximumSize = new System.Drawing.Size(500, 0);
            this.rjLabel8.Name = "rjLabel8";
            this.rjLabel8.Size = new System.Drawing.Size(492, 48);
            this.rjLabel8.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel8.TabIndex = 12;
            this.rjLabel8.Text = "학습을 시작하기 전에 로드할 가중치 파일을 지정합니다. 이렇게 하면 이전에 학습한 모델에서 학습을 계속하거나, 사전 학습된 모델을 미세 조정(fi" +
    "ne-tuning)하여 학습 시간을 줄일 수 있습니다.";
            // 
            // rjLabel9
            // 
            this.rjLabel9.AutoSize = true;
            this.rjLabel9.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel9.Font = new System.Drawing.Font("Verdana", 12F);
            this.rjLabel9.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(105)))), ((int)(((byte)(103)))), ((int)(((byte)(105)))));
            this.rjLabel9.LinkLabel = false;
            this.rjLabel9.Location = new System.Drawing.Point(24, 472);
            this.rjLabel9.Name = "rjLabel9";
            this.rjLabel9.Size = new System.Drawing.Size(81, 18);
            this.rjLabel9.Style = RJCodeUI_M1.RJControls.LabelStyle.Subtitle;
            this.rjLabel9.TabIndex = 11;
            this.rjLabel9.Text = "Weights ";
            // 
            // btnApplyChanges
            // 
            this.btnApplyChanges.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnApplyChanges.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.btnApplyChanges.BorderRadius = 15;
            this.btnApplyChanges.BorderSize = 1;
            this.btnApplyChanges.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnApplyChanges.Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton;
            this.btnApplyChanges.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.btnApplyChanges.FlatAppearance.BorderSize = 0;
            this.btnApplyChanges.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(84)))), ((int)(((byte)(137)))), ((int)(((byte)(231)))));
            this.btnApplyChanges.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(79)))), ((int)(((byte)(128)))), ((int)(((byte)(216)))));
            this.btnApplyChanges.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnApplyChanges.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnApplyChanges.ForeColor = System.Drawing.Color.White;
            this.btnApplyChanges.IconChar = FontAwesome.Sharp.IconChar.Check;
            this.btnApplyChanges.IconColor = System.Drawing.Color.White;
            this.btnApplyChanges.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.btnApplyChanges.IconSize = 24;
            this.btnApplyChanges.Location = new System.Drawing.Point(180, 596);
            this.btnApplyChanges.Name = "btnApplyChanges";
            this.btnApplyChanges.Size = new System.Drawing.Size(170, 40);
            this.btnApplyChanges.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.btnApplyChanges.TabIndex = 21;
            this.btnApplyChanges.Text = "Apply Changes";
            this.btnApplyChanges.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnApplyChanges.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnApplyChanges.UseVisualStyleBackColor = false;
            this.btnApplyChanges.Click += new System.EventHandler(this.btnApplyChanges_Click);
            // 
            // rjLabel15
            // 
            this.rjLabel15.AutoSize = true;
            this.rjLabel15.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel15.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.rjLabel15.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.rjLabel15.LinkLabel = false;
            this.rjLabel15.Location = new System.Drawing.Point(27, 283);
            this.rjLabel15.MaximumSize = new System.Drawing.Size(500, 0);
            this.rjLabel15.Name = "rjLabel15";
            this.rjLabel15.Size = new System.Drawing.Size(499, 48);
            this.rjLabel15.Style = RJCodeUI_M1.RJControls.LabelStyle.Normal;
            this.rjLabel15.TabIndex = 26;
            this.rjLabel15.Text = "학습을 위해 전체 데이터 세트를 반복하는 횟수(에폭 수)를 설정합니다. 이 값이 클수록 모델은 더 많은 시간을 학습에 사용하지만, 너무 많이 학습" +
    "하면 과적합(overfitting)이 발생할 수 있습니다.";
            // 
            // rjLabel16
            // 
            this.rjLabel16.AutoSize = true;
            this.rjLabel16.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rjLabel16.Font = new System.Drawing.Font("Verdana", 12F);
            this.rjLabel16.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(105)))), ((int)(((byte)(103)))), ((int)(((byte)(105)))));
            this.rjLabel16.LinkLabel = false;
            this.rjLabel16.Location = new System.Drawing.Point(24, 264);
            this.rjLabel16.Name = "rjLabel16";
            this.rjLabel16.Size = new System.Drawing.Size(65, 18);
            this.rjLabel16.Style = RJCodeUI_M1.RJControls.LabelStyle.Subtitle;
            this.rjLabel16.TabIndex = 25;
            this.rjLabel16.Text = "Epochs";
            // 
            // rjButton1
            // 
            this.rjButton1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.rjButton1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(110)))), ((int)(((byte)(134)))));
            this.rjButton1.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(110)))), ((int)(((byte)(134)))));
            this.rjButton1.BorderRadius = 10;
            this.rjButton1.BorderSize = 1;
            this.rjButton1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.rjButton1.Design = RJCodeUI_M1.RJControls.ButtonDesign.Custom;
            this.rjButton1.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(79)))), ((int)(((byte)(82)))));
            this.rjButton1.FlatAppearance.BorderSize = 0;
            this.rjButton1.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(97)))), ((int)(((byte)(103)))), ((int)(((byte)(125)))));
            this.rjButton1.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(91)))), ((int)(((byte)(96)))), ((int)(((byte)(117)))));
            this.rjButton1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rjButton1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rjButton1.ForeColor = System.Drawing.Color.White;
            this.rjButton1.IconChar = FontAwesome.Sharp.IconChar.TimesCircle;
            this.rjButton1.IconColor = System.Drawing.Color.White;
            this.rjButton1.IconFont = FontAwesome.Sharp.IconFont.Auto;
            this.rjButton1.IconSize = 24;
            this.rjButton1.Location = new System.Drawing.Point(356, 596);
            this.rjButton1.Name = "rjButton1";
            this.rjButton1.Size = new System.Drawing.Size(170, 40);
            this.rjButton1.Style = RJCodeUI_M1.RJControls.ControlStyle.Solid;
            this.rjButton1.TabIndex = 2155;
            this.rjButton1.Text = "Cancel";
            this.rjButton1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rjButton1.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.rjButton1.UseVisualStyleBackColor = false;
            this.rjButton1.Click += new System.EventHandler(this.rjButton1_Click);
            // 
            // tbImageSize
            // 
            this.tbImageSize._Customizable = false;
            this.tbImageSize.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.tbImageSize.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbImageSize.BorderFocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(162)))), ((int)(((byte)(247)))));
            this.tbImageSize.BorderRadius = 10;
            this.tbImageSize.BorderSize = 1;
            this.tbImageSize.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.tbImageSize.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbImageSize.Location = new System.Drawing.Point(27, 117);
            this.tbImageSize.MultiLine = false;
            this.tbImageSize.Name = "tbImageSize";
            this.tbImageSize.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.tbImageSize.PasswordChar = false;
            this.tbImageSize.PlaceHolderColor = System.Drawing.Color.DarkGray;
            this.tbImageSize.PlaceHolderText = null;
            this.tbImageSize.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.tbImageSize.Size = new System.Drawing.Size(241, 31);
            this.tbImageSize.Style = RJCodeUI_M1.RJControls.TextBoxStyle.MatteBorder;
            this.tbImageSize.TabIndex = 2156;
            // 
            // tbbatch
            // 
            this.tbbatch._Customizable = false;
            this.tbbatch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.tbbatch.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbbatch.BorderFocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(162)))), ((int)(((byte)(247)))));
            this.tbbatch.BorderRadius = 10;
            this.tbbatch.BorderSize = 1;
            this.tbbatch.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.tbbatch.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbbatch.Location = new System.Drawing.Point(27, 224);
            this.tbbatch.MultiLine = false;
            this.tbbatch.Name = "tbbatch";
            this.tbbatch.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.tbbatch.PasswordChar = false;
            this.tbbatch.PlaceHolderColor = System.Drawing.Color.DarkGray;
            this.tbbatch.PlaceHolderText = null;
            this.tbbatch.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.tbbatch.Size = new System.Drawing.Size(241, 31);
            this.tbbatch.Style = RJCodeUI_M1.RJControls.TextBoxStyle.MatteBorder;
            this.tbbatch.TabIndex = 2157;
            // 
            // tbepoch
            // 
            this.tbepoch._Customizable = false;
            this.tbepoch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.tbepoch.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbepoch.BorderFocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(162)))), ((int)(((byte)(247)))));
            this.tbepoch.BorderRadius = 10;
            this.tbepoch.BorderSize = 1;
            this.tbepoch.Font = new System.Drawing.Font("Verdana", 9.5F);
            this.tbepoch.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.tbepoch.Location = new System.Drawing.Point(27, 332);
            this.tbepoch.MultiLine = false;
            this.tbepoch.Name = "tbepoch";
            this.tbepoch.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.tbepoch.PasswordChar = false;
            this.tbepoch.PlaceHolderColor = System.Drawing.Color.DarkGray;
            this.tbepoch.PlaceHolderText = null;
            this.tbepoch.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.tbepoch.Size = new System.Drawing.Size(241, 31);
            this.tbepoch.Style = RJCodeUI_M1.RJControls.TextBoxStyle.MatteBorder;
            this.tbepoch.TabIndex = 2158;
            // 
            // cbweight
            // 
            this.cbweight.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.None;
            this.cbweight.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.None;
            this.cbweight.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.cbweight.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.cbweight.BorderRadius = 10;
            this.cbweight.BorderSize = 1;
            this.cbweight.Customizable = false;
            this.cbweight.DataSource = null;
            this.cbweight.DropDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            this.cbweight.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbweight.DropDownTextColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.cbweight.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.cbweight.Location = new System.Drawing.Point(27, 542);
            this.cbweight.Name = "cbweight";
            this.cbweight.Padding = new System.Windows.Forms.Padding(2);
            this.cbweight.SelectedIndex = -1;
            this.cbweight.Size = new System.Drawing.Size(241, 32);
            this.cbweight.Style = RJCodeUI_M1.RJControls.ControlStyle.Glass;
            this.cbweight.TabIndex = 2160;
            this.cbweight.Texts = "";
            // 
            // cbcfg
            // 
            this.cbcfg.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.None;
            this.cbcfg.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.None;
            this.cbcfg.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.cbcfg.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.cbcfg.BorderRadius = 10;
            this.cbcfg.BorderSize = 1;
            this.cbcfg.Customizable = false;
            this.cbcfg.DataSource = null;
            this.cbcfg.DropDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            this.cbcfg.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbcfg.DropDownTextColor = System.Drawing.Color.FromArgb(((int)(((byte)(132)))), ((int)(((byte)(129)))), ((int)(((byte)(132)))));
            this.cbcfg.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(146)))), ((int)(((byte)(246)))));
            this.cbcfg.Location = new System.Drawing.Point(27, 437);
            this.cbcfg.Name = "cbcfg";
            this.cbcfg.Padding = new System.Windows.Forms.Padding(2);
            this.cbcfg.SelectedIndex = -1;
            this.cbcfg.Size = new System.Drawing.Size(241, 32);
            this.cbcfg.Style = RJCodeUI_M1.RJControls.ControlStyle.Glass;
            this.cbcfg.TabIndex = 2161;
            this.cbcfg.Texts = "";
            // 
            // FormVision_Yolov5ParamSetting
            // 
            this._DesktopPanelSize = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(83)))), ((int)(((byte)(97)))), ((int)(((byte)(212)))));
            this.BorderSize = 2;
            this.Caption = "Settings";
            this.ClientSize = new System.Drawing.Size(595, 709);
            this.FormIcon = FontAwesome.Sharp.IconChar.Tools;
            this.MinimumSize = new System.Drawing.Size(300, 180);
            this.Name = "FormVision_Yolov5ParamSetting";
            this.Padding = new System.Windows.Forms.Padding(2);
            this.Text = "Settings";
            this.Load += new System.EventHandler(this.FormVision_Yolov5ParamSetting_Load);
            this.pnlClientArea.ResumeLayout(false);
            this.pnlClientArea.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private RJControls.RJLabel rjLabel1;
        private RJControls.RJLabel rjLabel2;
        private RJControls.RJLabel rjLabel3;
        private RJControls.RJLabel rjLabel4;
        private RJControls.RJLabel rjLabel5;
        private RJControls.RJLabel rjLabel6;
        private RJControls.RJLabel rjLabel7;
        private RJControls.RJLabel rjLabel8;
        private RJControls.RJLabel rjLabel9;
        private RJControls.RJButton btnApplyChanges;
        private RJControls.RJLabel rjLabel15;
        private RJControls.RJLabel rjLabel16;
        private RJControls.RJButton rjButton1;
        private RJControls.RJTextBox tbImageSize;
        private RJControls.RJTextBox tbepoch;
        private RJControls.RJTextBox tbbatch;
        private RJControls.RJComboBox cbweight;
        private RJControls.RJComboBox cbcfg;
    }
}
