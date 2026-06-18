﻿using System;
using System.Windows.Forms;
using System.Reflection;
using Keys = System.Windows.Forms.Keys;
using RJCodeUI_M1.RJForms;
using Lib.Common;
using System.Linq;
using System.Drawing;
using static MvcVisionSystem.CSystem;
using MvcVisionSystem.Yolo;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public partial class FormVision_ClassMenu : RJChildForm
    {
        // UpdateControl delegate의 이벤트를 선언합니다.
        public event UpdateControl OnButtonClicked;

        public FormVision_ClassMenu()
        {
            InitializeComponent();                                    
            ApplyLabelingDialogTheme();
            this.TopLevel = true;
            this.TopMost = true;

            ShowImagesList();
        }

        private void ApplyLabelingDialogTheme()
        {
            Text = "클래스 설정";
            BackColor = LabelingWorkbenchPalette.Frame;
            pnlClientArea.BackColor = LabelingWorkbenchPalette.Panel;
            rjPanel1.BackColor = LabelingWorkbenchPalette.Panel;
            rjPanel1.BorderRadius = 0;

            rjLabel1.Text = "클래스";
            rjLabel2.Text = "저장 경로";
            rjLabel1.ForeColor = LabelingWorkbenchPalette.MutedText;
            rjLabel2.ForeColor = LabelingWorkbenchPalette.MutedText;

            StyleTextBox(txtNames);
            StyleTextBox(tbOutputPath);
            StyleDialogButton(btnCreate, "추가", LabelingWorkbenchPalette.Selection, LabelingWorkbenchPalette.AccentHover);
            StyleDialogButton(btnDelete, "삭제", LabelingWorkbenchPalette.Error, Color.FromArgb(179, 84, 60));
            StyleDialogButton(btnExportPath, "경로", LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            StyleDialogButton(btnCancel, "닫기", Color.FromArgb(82, 87, 96), LabelingWorkbenchPalette.PanelHeader);
            StyleClassGrid();
        }

        private static void StyleTextBox(RJCodeUI_M1.RJControls.RJTextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.BackColor = LabelingWorkbenchPalette.Surface;
            textBox.BorderColor = LabelingWorkbenchPalette.Divider;
            textBox.BorderFocusColor = LabelingWorkbenchPalette.Accent;
            textBox.ForeColor = LabelingWorkbenchPalette.Text;
            textBox.PlaceHolderColor = LabelingWorkbenchPalette.MutedText;
            textBox.BorderRadius = 4;
        }

        private static void StyleDialogButton(RJCodeUI_M1.RJControls.RJButton button, string text, Color backColor, Color hoverColor)
        {
            if (button == null)
            {
                return;
            }

            button.Text = text;
            button.BackColor = backColor;
            button.BorderColor = backColor;
            button.BorderRadius = 4;
            button.FlatAppearance.BorderColor = backColor;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.ForeColor = Color.White;
            button.IconColor = Color.White;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void StyleClassGrid()
        {
            dgvImagesList.BackgroundColor = LabelingWorkbenchPalette.Surface;
            dgvImagesList.DgvBackColor = LabelingWorkbenchPalette.Surface;
            dgvImagesList.ColumnHeaderColor = LabelingWorkbenchPalette.PanelHeader;
            dgvImagesList.ColumnHeaderTextColor = Color.White;
            dgvImagesList.RowsColor = LabelingWorkbenchPalette.SurfaceAlt;
            dgvImagesList.RowsTextColor = LabelingWorkbenchPalette.Text;
            dgvImagesList.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            dgvImagesList.SelectionTextColor = Color.White;
            dgvImagesList.GridColor = LabelingWorkbenchPalette.Divider;
            dgvImagesList.BorderRadius = 0;
            dgvImagesList.EnableHeadersVisualStyles = false;
            dgvImagesList.ColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            Column2.HeaderText = "No";
            Column1.HeaderText = "클래스";

            dgvImagesList.ColumnHeadersDefaultCellStyle.BackColor = LabelingWorkbenchPalette.PanelHeader;
            dgvImagesList.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvImagesList.DefaultCellStyle.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            dgvImagesList.DefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            dgvImagesList.DefaultCellStyle.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            dgvImagesList.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        private void ShowImagesList()
        {
            dgvImagesList.Rows.Clear();
            for (int i = 0; i < CGlobal.Inst.Data.ClassNamedList.Count; i++)
            {
                object[] row = new object[] { (i + 1), CGlobal.Inst.Data.ClassNamedList[i].Text };
                dgvImagesList.Rows.Add(row);
            }

            // 버튼이 클릭되면 OnButtonClicked 이벤트를 발생시킵니다.
            OnButtonClicked?.Invoke();
        }

        private void FormSettings_Camera_Load(object sender, EventArgs e)
        {
            CGlobal.Inst.Data.NormalizeOutputPaths();
            tbOutputPath.Text = CGlobal.Inst.Data.OutputRootPath;
            InitEvent();                        
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            Keys key = keyData & ~(Keys.Shift | Keys.Control);

            switch (key)
            {
                case Keys.Escape:
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    return true;
                case Keys.Enter:
                    btnNewCreate_Click(null, null);
                    return true;                               
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }


        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keys)e.KeyValue == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private bool InitEvent()
        {
            try
            {
                this.KeyPreview = true;
                this.KeyDown += Form_KeyDown;
                AppLog.NORMAL( $"[OK] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL( $"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }

            return true;
        }

        private void SaveClassConfigAndYaml()
        {
            CGlobal.Inst.Data.SaveYoloDataYaml();
            CGlobal.Inst.Data.SaveConfig(CGlobal.Inst.Recipe.Name);
            tbOutputPath.Text = CGlobal.Inst.Data.OutputRootPath;
        }

        private void btnNewCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();            
        }

        private void btnNewCreate_Click(object sender, EventArgs e)
        {
            string names = txtNames.Text.Trim();

            if (string.IsNullOrWhiteSpace(names))
            {
                return;
            }

            if (ClassCatalogService.TryAddClass(CGlobal.Inst.Data, names, out CClassItem _))
            {
                SaveClassConfigAndYaml();
            }

            ShowImagesList();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvImagesList.SelectedRows.Count == 0)
            {
                return;
            }

            DataGridViewRow row = dgvImagesList.SelectedRows[0]; //선택된 Row 값 가져옴.
            string targetText = row.Cells[1].Value.ToString(); // row의 컬럼
            if (ClassCatalogService.RemoveClass(CGlobal.Inst.Data, targetText))
            {
                SaveClassConfigAndYaml();
            }

            ShowImagesList();
        }

        private void dgvImagesList_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {            
            string targetText = dgvImagesList[1, e.RowIndex].Value.ToString();
            txtNames.Text = targetText;
        }

        private void dgvImagesList_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string targetText = dgvImagesList[1, e.RowIndex].Value.ToString();
            txtNames.Text = targetText;
        }

        private void btnExportPath_Click(object sender, EventArgs e)
        {
            LoadFolderPath(out string folderPath);
            if (folderPath != "")
            {
                CGlobal.Inst.Data.ConfigureOutputRoot(folderPath);
                SaveClassConfigAndYaml();
            }
        }

        private string lastPath = string.Empty;

        private bool LoadFolderPath(out string folderPath)
        {
            folderPath = "";
            try
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    // 이전에 저장된 경로가 있다면 사용합니다.
                    if (!string.IsNullOrEmpty(lastPath))
                    {
                        fbd.SelectedPath = lastPath;
                    }

                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        folderPath = fbd.SelectedPath;
                        lastPath = folderPath;  // 선택된 경로를 저장합니다.
                    }
                }

                AppLog.NORMAL($"[OK] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}");
                return true;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }
    }
 }

