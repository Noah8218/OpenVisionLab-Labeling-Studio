using System.Drawing;
using System.Windows.Forms;

namespace OpenVisionLab
{
    partial class FormLocalizationEditor
    {
        private System.ComponentModel.IContainer components = null;
        private TableLayoutPanel rootLayout;
        private Panel topPanel;
        private Label lblLanguage;
        private ComboBox cbLanguage;
        private Label lblSearch;
        private TextBox tbSearch;
        private CheckBox chkMissingOnly;
        private DataGridView gridCatalog;
        private DataGridViewTextBoxColumn colKey;
        private DataGridViewTextBoxColumn colKorean;
        private DataGridViewTextBoxColumn colEnglish;
        private Panel footerPanel;
        private Button btnSave;
        private Button btnReload;
        private Button btnClose;
        private Label lblSummary;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rootLayout = new TableLayoutPanel();
            topPanel = new Panel();
            lblLanguage = new Label();
            cbLanguage = new ComboBox();
            lblSearch = new Label();
            tbSearch = new TextBox();
            chkMissingOnly = new CheckBox();
            gridCatalog = new DataGridView();
            colKey = new DataGridViewTextBoxColumn();
            colKorean = new DataGridViewTextBoxColumn();
            colEnglish = new DataGridViewTextBoxColumn();
            footerPanel = new Panel();
            btnSave = new Button();
            btnReload = new Button();
            btnClose = new Button();
            lblSummary = new Label();
            rootLayout.SuspendLayout();
            topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridCatalog).BeginInit();
            footerPanel.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(topPanel, 0, 0);
            rootLayout.Controls.Add(gridCatalog, 0, 1);
            rootLayout.Controls.Add(footerPanel, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(12);
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            rootLayout.Size = new Size(920, 620);
            rootLayout.TabIndex = 0;
            // 
            // topPanel
            // 
            topPanel.Controls.Add(lblLanguage);
            topPanel.Controls.Add(cbLanguage);
            topPanel.Controls.Add(lblSearch);
            topPanel.Controls.Add(tbSearch);
            topPanel.Controls.Add(chkMissingOnly);
            topPanel.Dock = DockStyle.Fill;
            topPanel.Location = new Point(15, 15);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(890, 38);
            topPanel.TabIndex = 0;
            // 
            // lblLanguage
            // 
            lblLanguage.AutoSize = false;
            lblLanguage.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblLanguage.ForeColor = Color.FromArgb(35, 85, 132);
            lblLanguage.Location = new Point(0, 7);
            lblLanguage.Name = "lblLanguage";
            lblLanguage.Size = new Size(86, 22);
            lblLanguage.TabIndex = 0;
            lblLanguage.Text = "현재 언어";
            lblLanguage.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // cbLanguage
            // 
            cbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            cbLanguage.FlatStyle = FlatStyle.Flat;
            cbLanguage.FormattingEnabled = true;
            cbLanguage.Location = new Point(92, 7);
            cbLanguage.Name = "cbLanguage";
            cbLanguage.Size = new Size(130, 23);
            cbLanguage.TabIndex = 1;
            cbLanguage.SelectedIndexChanged += cbLanguage_SelectedIndexChanged;
            // 
            // lblSearch
            // 
            lblSearch.AutoSize = false;
            lblSearch.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblSearch.ForeColor = Color.FromArgb(35, 85, 132);
            lblSearch.Location = new Point(244, 7);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(46, 22);
            lblSearch.TabIndex = 2;
            lblSearch.Text = "검색";
            lblSearch.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // tbSearch
            // 
            tbSearch.BorderStyle = BorderStyle.FixedSingle;
            tbSearch.Location = new Point(296, 7);
            tbSearch.Name = "tbSearch";
            tbSearch.Size = new Size(360, 23);
            tbSearch.TabIndex = 3;
            tbSearch.TextChanged += tbSearch_TextChanged;
            // 
            // chkMissingOnly
            // 
            chkMissingOnly.AutoSize = true;
            chkMissingOnly.FlatStyle = FlatStyle.Flat;
            chkMissingOnly.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkMissingOnly.ForeColor = Color.FromArgb(35, 85, 132);
            chkMissingOnly.Location = new Point(674, 8);
            chkMissingOnly.Name = "chkMissingOnly";
            chkMissingOnly.Size = new Size(108, 19);
            chkMissingOnly.TabIndex = 4;
            chkMissingOnly.Text = "Missing only";
            chkMissingOnly.CheckedChanged += chkMissingOnly_CheckedChanged;
            // 
            // gridCatalog
            // 
            gridCatalog.AllowUserToResizeRows = false;
            gridCatalog.BackgroundColor = Color.White;
            gridCatalog.BorderStyle = BorderStyle.FixedSingle;
            gridCatalog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridCatalog.Columns.AddRange(new DataGridViewColumn[] { colKey, colKorean, colEnglish });
            gridCatalog.Dock = DockStyle.Fill;
            gridCatalog.Location = new Point(15, 59);
            gridCatalog.Name = "gridCatalog";
            gridCatalog.RowHeadersWidth = 24;
            gridCatalog.RowTemplate.Height = 24;
            gridCatalog.Size = new Size(890, 500);
            gridCatalog.TabIndex = 1;
            // 
            // colKey
            // 
            colKey.HeaderText = "키";
            colKey.Name = "colKey";
            colKey.Width = 240;
            // 
            // colKorean
            // 
            colKorean.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colKorean.FillWeight = 50F;
            colKorean.HeaderText = "한국어";
            colKorean.Name = "colKorean";
            // 
            // colEnglish
            // 
            colEnglish.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colEnglish.FillWeight = 50F;
            colEnglish.HeaderText = "English";
            colEnglish.Name = "colEnglish";
            // 
            // footerPanel
            // 
            footerPanel.Controls.Add(btnSave);
            footerPanel.Controls.Add(btnReload);
            footerPanel.Controls.Add(btnClose);
            footerPanel.Controls.Add(lblSummary);
            footerPanel.Dock = DockStyle.Fill;
            footerPanel.Location = new Point(15, 565);
            footerPanel.Name = "footerPanel";
            footerPanel.Size = new Size(890, 40);
            footerPanel.TabIndex = 2;
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Location = new Point(578, 6);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(96, 28);
            btnSave.TabIndex = 0;
            btnSave.Text = "저장";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnReload
            // 
            btnReload.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnReload.FlatStyle = FlatStyle.Flat;
            btnReload.Location = new Point(680, 6);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(96, 28);
            btnReload.TabIndex = 1;
            btnReload.Text = "다시 읽기";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Location = new Point(782, 6);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(96, 28);
            btnClose.TabIndex = 2;
            btnClose.Text = "닫기";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // lblSummary
            // 
            lblSummary.AutoSize = false;
            lblSummary.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblSummary.ForeColor = Color.FromArgb(62, 82, 112);
            lblSummary.Location = new Point(0, 9);
            lblSummary.Name = "lblSummary";
            lblSummary.Size = new Size(540, 22);
            lblSummary.TabIndex = 3;
            lblSummary.Text = "Rows: 0";
            lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // FormLocalizationEditor
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(240, 245, 249);
            ClientSize = new Size(920, 620);
            Controls.Add(rootLayout);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            MinimumSize = new Size(820, 520);
            Name = "FormLocalizationEditor";
            StartPosition = FormStartPosition.CenterParent;
            Text = "다국어 편집기";
            Load += FormLocalizationEditor_Load;
            rootLayout.ResumeLayout(false);
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridCatalog).EndInit();
            footerPanel.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
