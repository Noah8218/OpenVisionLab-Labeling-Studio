namespace MvcVisionSystem
{
    partial class FormClassList
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormClassList));
            this.rjPanel1 = new RJCodeUI_M1.RJControls.RJPanel();
            this.dgvImagesList = new RJCodeUI_M1.RJControls.RJDataGridView();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rjPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvImagesList)).BeginInit();
            this.SuspendLayout();
            // rjPanel1
            // 
            this.rjPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            this.rjPanel1.BorderRadius = 5;
            this.rjPanel1.Controls.Add(this.dgvImagesList);
            this.rjPanel1.Customizable = false;
            this.rjPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rjPanel1.Location = new System.Drawing.Point(0, 0);
            this.rjPanel1.Name = "rjPanel1";
            this.rjPanel1.Size = new System.Drawing.Size(534, 513);
            this.rjPanel1.TabIndex = 2144;
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
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.MediumPurple;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Padding = new System.Windows.Forms.Padding(15, 0, 0, 0);
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvImagesList.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgvImagesList.ColumnHeadersHeight = 40;
            this.dgvImagesList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvImagesList.ColumnHeaderTextColor = System.Drawing.Color.White;
            this.dgvImagesList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column2,
            this.Column1,
            this.Column3});
            this.dgvImagesList.ColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;
            this.dgvImagesList.Customizable = false;
            this.dgvImagesList.DgvBackColor = System.Drawing.Color.White;
            this.dgvImagesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvImagesList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgvImagesList.EnableHeadersVisualStyles = false;
            this.dgvImagesList.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.dgvImagesList.Location = new System.Drawing.Point(0, 0);
            this.dgvImagesList.Name = "dgvImagesList";
            this.dgvImagesList.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.dgvImagesList.RowHeaderColor = System.Drawing.Color.WhiteSmoke;
            this.dgvImagesList.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(213)))), ((int)(((byte)(199)))), ((int)(((byte)(241)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvImagesList.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.dgvImagesList.RowHeadersVisible = false;
            this.dgvImagesList.RowHeadersWidth = 30;
            this.dgvImagesList.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgvImagesList.RowHeight = 40;
            this.dgvImagesList.RowsColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(252)))), ((int)(((byte)(253)))));
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.Gray;
            dataGridViewCellStyle3.Padding = new System.Windows.Forms.Padding(15, 0, 0, 0);
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(213)))), ((int)(((byte)(199)))), ((int)(((byte)(241)))));
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.Gray;
            this.dgvImagesList.RowsDefaultCellStyle = dataGridViewCellStyle3;
            this.dgvImagesList.RowsTextColor = System.Drawing.Color.Gray;
            this.dgvImagesList.RowTemplate.Height = 40;
            this.dgvImagesList.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(213)))), ((int)(((byte)(199)))), ((int)(((byte)(241)))));
            this.dgvImagesList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvImagesList.SelectionTextColor = System.Drawing.Color.Gray;
            this.dgvImagesList.Size = new System.Drawing.Size(534, 513);
            this.dgvImagesList.TabIndex = 2156;
            // 
            // Column2
            // 
            this.Column2.HeaderText = "No";
            this.Column2.Name = "Column2";
            this.Column2.Width = 150;
            // 
            // Column1
            // 
            this.Column1.HeaderText = "클래스";
            this.Column1.Name = "Column1";
            this.Column1.Width = 150;
            // 
            // Column3
            // 
            this.Column3.HeaderText = "위치";
            this.Column3.Name = "Column3";
            this.Column3.Width = 150;
            // 
            // FormClassList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScroll = true;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.ClientSize = new System.Drawing.Size(534, 513);
            this.Controls.Add(this.rjPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "FormClassList";
            this.Text = "클래스 리스트";
            this.VisibleChanged += new System.EventHandler(this.Form_VisibleChanged);
            this.rjPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvImagesList)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private RJCodeUI_M1.RJControls.RJPanel rjPanel1;
        private RJCodeUI_M1.RJControls.RJDataGridView dgvImagesList;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
    }
}
