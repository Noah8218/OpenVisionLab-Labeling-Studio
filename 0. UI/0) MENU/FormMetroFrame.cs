using Lib.Common;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using RJCodeUI_M1.RJControls;
using RJCodeUI_M1.RJForms;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    public partial class FormMetroFrame : Form
    {
        private readonly CGlobal Global = CGlobal.Inst;
        private readonly FormInit formInit;
        private FormTeachingVision FrmVision;
        private FormVision_ClassMenu formVision_ClassMenu;

        public FormMetroFrame(FormInit formInit)
        {
            this.formInit = formInit;
            InitializeComponent();
        }

        private void FormMetroFrame_Load(object sender, EventArgs e)
        {
            InitUI();
        }

        private void FormMetroFrame_Shown(object sender, EventArgs e)
        {
            if (formInit != null)
            {
                formInit.Close = true;
            }
        }

        private void InitUI()
        {
            ApplyMainTheme();
            lbVersion.Text = $"VERSION : {CVersion.VERSION} - {CVersion.DATETIME_UPDATED} ({CVersion.MANAGER})";
            lbExportPath.Text = Global.Data.OutputDataImageAndTxtPath;

            var classNames = Global.Data.ClassNamedList.Select(x => x.Text).ToArray();
            cbClassMenu.Items.Clear();
            cbClassMenu.Items.AddRange(classNames);
            if (cbClassMenu.Items.Count > 0)
            {
                cbClassMenu.SelectedIndex = 0;
            }

            FrmVision = new FormTeachingVision();
            FrmVision.TopLevel = false;
            FrmVision.FormBorderStyle = FormBorderStyle.None;
            FrmVision.Dock = DockStyle.Fill;
            if (!pnMDI.Controls.Contains(pnFormMain))
            {
                pnMDI.Controls.Add(pnFormMain);
                pnFormMain.Dock = DockStyle.Fill;
            }

            OperatorPanel.Controls.Clear();
            OperatorPanel.Controls.Add(FrmVision);
            FrmVision.Show();
        }

        private void ApplyMainTheme()
        {
            SettingsManager.LoadApperanceSettings();

            Color frameColor = Color.FromArgb(64, 73, 108);
            Color accentColor = Color.FromArgb(83, 97, 212);
            Color buttonColor = frameColor;
            Color buttonDownColor = Color.FromArgb(54, 62, 92);
            Color buttonHoverColor = Color.FromArgb(76, 86, 126);

            BackColor = frameColor;
            pnlTitleBar.BackColor = frameColor;
            pnStatusBar.BackColor = frameColor;
            pnFormMain.BackColor = accentColor;
            OperatorPanel.BackColor = Color.Black;
            ApplyPanelTheme(pnlTitleBar, frameColor);

            ApplyButtonTheme(pnlTitleBar, buttonColor, buttonDownColor, buttonHoverColor);
        }

        private void ApplyPanelTheme(Control parent, Color backColor)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Panel)
                {
                    control.BackColor = backColor;
                }

                if (control.HasChildren)
                {
                    ApplyPanelTheme(control, backColor);
                }
            }
        }
        private void ApplyButtonTheme(Control parent, Color backColor, Color downColor, Color hoverColor)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is RJButton button)
                {
                    button.BackColor = backColor;
                    button.BorderColor = backColor;
                    button.FlatAppearance.BorderColor = backColor;
                    button.FlatAppearance.MouseDownBackColor = downColor;
                    button.FlatAppearance.MouseOverBackColor = hoverColor;
                    button.ForeColor = Color.White;
                    button.IconColor = Color.White;
                }

                if (control.HasChildren)
                {
                    ApplyButtonTheme(control, backColor, downColor, hoverColor);
                }
            }
        }

        private void timerAlarm_Tick(object sender, EventArgs e) { }
        private void timerConnection_Tick(object sender, EventArgs e)
        {
            lbSelectImageName.Text = Global.Data.LastSelectImageName;
        }

        private void FormMetroFrame_FormClosing(object sender, FormClosingEventArgs e)
        {
            Global.Close();
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnMinimizar_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void miSettings_Click(object sender, EventArgs e) { }
        private void btnUserOptions_Click(object sender, EventArgs e) { }
        private void btnScreenCapture_Click(object sender, EventArgs e)
        {
            try
            {
                int w = Screen.PrimaryScreen.Bounds.Width;
                int h = Screen.PrimaryScreen.Bounds.Height;

                System.Drawing.Size s = new System.Drawing.Size(w, h);
                Bitmap b = new Bitmap(w, h);
                Graphics g = Graphics.FromImage(b);

                g.CopyFromScreen(0, 0, 0, 0, s);

                string strSavePath = $"{System.Windows.Forms.Application.StartupPath}\\CAPTURE\\{this.Text}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.jpeg";

                b.Save(strSavePath);

                CLOG.NORMAL($"저장 경로 : {strSavePath}");
            }
            catch (Exception Desc)
            {
                CLOG.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Execption ==> {Desc.Message}");
            }
        }

        private void btnScreenCapture_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Control control = (Control)sender;
                ddmCapture.ItemClicked += new ToolStripItemClickedEventHandler(CaptureClicked);
                ddmCapture.Show(control, 0, control.Height);
            }
        }

        private void CaptureClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "Show Folder":
                    Process.Start(System.Windows.Forms.Application.StartupPath + "\\CAPTURE");
                    break;
                default:
                    break;
            }
        }

        private void btnClassMenu_Click(object sender, EventArgs e)
        {
            if (formVision_ClassMenu == null || formVision_ClassMenu.IsDisposed)
            {
                formVision_ClassMenu = new FormVision_ClassMenu();
                formVision_ClassMenu.OnButtonClicked += ClassMenuForm_OnButtonClicked;
            }

            formVision_ClassMenu.Show();
            formVision_ClassMenu.BringToFront();
        }

        private void ClassMenuForm_OnButtonClicked()
        {
            cbClassMenu.Items.Clear();
            cbClassMenu.Items.AddRange(Global.Data.ClassNamedList.Select(x => x.Text).ToArray());
            if (cbClassMenu.Items.Count > 0)
            {
                cbClassMenu.SelectedIndex = 0;
            }
        }

        private void cbClassMenu_OnSelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void btnClassSave_Click(object sender, EventArgs e)
        {
            Global.Data.SaveConfig(Global.Recipe.Name);
        }

        private void btnClassTrain_Click(object sender, EventArgs e)
        {
            string yaml = Global.Data.OutputDataYamlPath;
            string weight = "";
            Global.DeepLearning.SendTrainingData("StartTraining", "640", "16", "100", yaml, weight);
        }

        private void btnClassSetting_Click(object sender, EventArgs e)
        {
            RJCodeUI_M1.RJForms.FormVision_Yolov5ParamSetting formVision_Yolov5ParamSetting = new FormVision_Yolov5ParamSetting();
            formVision_Yolov5ParamSetting.TopLevel = true;
            formVision_Yolov5ParamSetting.TopMost = true;
            formVision_Yolov5ParamSetting.StartPosition = FormStartPosition.CenterParent;
            if (!CUtil.OpenCheckForm(formVision_Yolov5ParamSetting)) return;
            formVision_Yolov5ParamSetting.Show();
        }

        private void btnClassInfer_Click(object sender, EventArgs e)
        {
            if (!CDisplayManager.ImageSrc.Empty())
            {
                using (Bitmap bitmap = Lib.Common.CImageConverter.ToBitmap(CDisplayManager.ImageSrc))
                {
                    Global.DeepLearning.SendData("StartDefect", bitmap);
                }
            }
        }

        private void btnExportPath_Click(object sender, EventArgs e)
        {
            if (LoadFolderPath(out string folderPath))
            {
                Global.Data.OutputDataImageAndTxtPath = folderPath;
                Global.Data.OutputDataYamlPath = Path.Combine(folderPath, "data.yaml");
                lbExportPath.Text = folderPath;
                Global.Data.SaveConfig(Global.Recipe.Name);
            }
        }

        private bool LoadFolderPath(out string folderPath)
        {
            folderPath = "";
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    folderPath = fbd.SelectedPath;
                    return true;
                }
            }

            return false;
        }
    }
}
