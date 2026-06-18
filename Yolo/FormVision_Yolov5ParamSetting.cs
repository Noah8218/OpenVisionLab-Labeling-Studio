using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Lib.Common;
using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using RJCodeUI_M1.RJControls;
using RJCodeUI_M1.RJForms;
using RJCodeUI_M1.Settings;
using RJCodeUI_M1.Utils;
using static MvcVisionSystem._3._Communication.TCP.CCommunicationLearning;

namespace RJCodeUI_M1.RJForms
{
    public partial class FormVision_Yolov5ParamSetting : RJChildForm
    {
        private RJLabel lbValidationPercent;
        private RJLabel lbSplitSeed;
        private RJTextBox tbValidationPercent;
        private RJTextBox tbSplitSeed;
        private TabControl settingsTabs;
        private TabPage tabTraining;
        private TabPage tabPythonModel;
        private Panel trainingReadinessPanel;
        private RJLabel lblTrainingReadinessStatus;
        private RJLabel lblTrainingReadinessDetail;
        private RJTextBox tbPythonExecutablePath;
        private RJTextBox tbPythonProjectRootPath;
        private RJTextBox tbPythonClientScriptPath;
        private RJTextBox tbPythonWeightsPath;
        private RJTextBox tbPythonImageRootPath;
        private RJTextBox tbPythonMinimumConfidence;
        private RJTextBox tbPythonDetectionTimeoutSeconds;
        private CheckBox chkPythonAutoStart;
        private RJLabel lblPythonValidationStatus;

        /// <summary>
        /// This class inherits from the <see cref="RJChildForm"/>  class
        /// </summary>
        /// 

        #region -> Constructor

        public FormVision_Yolov5ParamSetting()
        {
            //This form was built by the designer.
            InitializeComponent();
            ApplyLabelingSettingsChrome();
            InitializeDatasetSplitControls();
            InitializeSettingsTabs();
            InitializePythonModelControls();
        }
        #endregion

        #region -> Event Methods

        private void lblRestartApp_Click(object sender, EventArgs e)
        {//Restart application

            Application.Restart();
            Environment.Exit(0);
        }
        #endregion

        private void FormVision_Yolov5ParamSetting_Load(object sender, EventArgs e)
        {
            CGlobal.Inst.Data.ProjectSettings ??= new LabelingProjectSettings();
            CGlobal.Inst.Data.ProjectSettings.EnsureDefaults();
            tbImageSize.Text = CGlobal.Inst.Data.TrainingParam.imageSize.ToString();
            tbbatch.Text = CGlobal.Inst.Data.TrainingParam.batch.ToString();
            tbepoch.Text = CGlobal.Inst.Data.TrainingParam.epoch.ToString();
            tbValidationPercent.Text = CGlobal.Inst.Data.ProjectSettings.YoloDataset.ValidationPercent.ToString();
            tbSplitSeed.Text = CGlobal.Inst.Data.ProjectSettings.YoloDataset.SplitSeed.ToString();
            PythonModelSettings pythonModel = CGlobal.Inst.Data.ProjectSettings.PythonModel;
            tbPythonExecutablePath.Text = pythonModel.PythonExecutablePath;
            tbPythonProjectRootPath.Text = pythonModel.ProjectRootPath;
            tbPythonClientScriptPath.Text = pythonModel.ClientScriptPath;
            tbPythonWeightsPath.Text = pythonModel.WeightsPath;
            tbPythonImageRootPath.Text = pythonModel.ImageRootPath;
            tbPythonMinimumConfidence.Text = pythonModel.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture);
            tbPythonDetectionTimeoutSeconds.Text = pythonModel.DetectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            chkPythonAutoStart.Checked = pythonModel.AutoStartClient;
            ApplyLabelingSettingsChrome();
            UpdateTrainingReadinessStatus();
            UpdatePythonValidationStatus(requireWeights: false);

            foreach (string item in Enum.GetNames(typeof(CYolov5TrainingParam.Cfg)))
            {
                cbcfg.Items.Add(item);
            }

            string enumString = Enum.GetName(typeof(CYolov5TrainingParam.Cfg), CGlobal.Inst.Data.TrainingParam.cfg);

            for (int i = 0; i < cbcfg.Items.Count; i++)
            {
                if (cbcfg.Items[i].ToString() == enumString)
                {
                    cbcfg.SelectedIndex = i;
                    break;
                }
            }

            foreach (string item in Enum.GetNames(typeof(CYolov5TrainingParam.Weight)))
            {
                cbweight.Items.Add(item);
            }

            enumString = Enum.GetName(typeof(CYolov5TrainingParam.Weight), CGlobal.Inst.Data.TrainingParam.weight);

            for (int i = 0; i < cbweight.Items.Count; i++)
            {
                if (cbweight.Items[i].ToString() == enumString)
                {
                    cbweight.SelectedIndex = i;
                    break;
                }
            }
        }

        private void btnApplyChanges_Click(object sender, EventArgs e)
        {
            try
            {
                int img = int.Parse(tbImageSize.Text);
                int batch = int.Parse(tbbatch.Text);
                int epoch = int.Parse(tbepoch.Text);
                int validationPercent = int.Parse(tbValidationPercent.Text);
                int splitSeed = int.Parse(tbSplitSeed.Text);
                float minimumConfidence = float.Parse(tbPythonMinimumConfidence.Text, CultureInfo.InvariantCulture);
                int detectionTimeoutSeconds = int.Parse(tbPythonDetectionTimeoutSeconds.Text, CultureInfo.InvariantCulture);
                var cfg = Lib.Common.CUtil.ParseEnum<CYolov5TrainingParam.Cfg>(cbcfg.SelectedItem.ToString());
                var weight = Lib.Common.CUtil.ParseEnum<CYolov5TrainingParam.Weight>(cbweight.SelectedItem.ToString());

                if (validationPercent < 0 || validationPercent > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(validationPercent), "Validation percent must be between 0 and 100.");
                }

                if (minimumConfidence < 0F || minimumConfidence > 1F)
                {
                    throw new ArgumentOutOfRangeException(nameof(minimumConfidence), "Minimum confidence must be between 0 and 1.");
                }

                if (detectionTimeoutSeconds < 1 || detectionTimeoutSeconds > 600)
                {
                    throw new ArgumentOutOfRangeException(nameof(detectionTimeoutSeconds), "Detection timeout must be between 1 and 600 seconds.");
                }

                CGlobal.Inst.Data.TrainingParam.imageSize = img;
                CGlobal.Inst.Data.TrainingParam.batch = batch;
                CGlobal.Inst.Data.TrainingParam.epoch = epoch;
                CGlobal.Inst.Data.TrainingParam.cfg = cfg;
                CGlobal.Inst.Data.TrainingParam.weight = weight;
                CGlobal.Inst.Data.ProjectSettings.EnsureDefaults();
                CGlobal.Inst.Data.ProjectSettings.YoloDataset.ValidationPercent = validationPercent;
                CGlobal.Inst.Data.ProjectSettings.YoloDataset.SplitSeed = splitSeed;
                PythonModelSettings pythonModel = CGlobal.Inst.Data.ProjectSettings.PythonModel;
                pythonModel.PythonExecutablePath = tbPythonExecutablePath.Text.Trim();
                pythonModel.ProjectRootPath = tbPythonProjectRootPath.Text.Trim();
                pythonModel.ClientScriptPath = tbPythonClientScriptPath.Text.Trim();
                pythonModel.WeightsPath = tbPythonWeightsPath.Text.Trim();
                pythonModel.ImageRootPath = tbPythonImageRootPath.Text.Trim();
                pythonModel.MinimumDetectionConfidence = minimumConfidence;
                pythonModel.DetectionTimeoutSeconds = detectionTimeoutSeconds;
                pythonModel.AutoStartClient = chkPythonAutoStart.Checked;
                pythonModel.EnsureDefaults();
                CGlobal.Inst.Data.SaveConfig(CGlobal.Inst.Recipe.Name);

                PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(pythonModel, requireWeights: false);
                foreach (string warning in validation.Warnings)
                {
                    AppLog.COMM(warning);
                }

                foreach (string error in validation.Errors)
                {
                    AppLog.ABNORMAL(error);
                }

                if (!validation.IsValid)
                {
                    return;
                }

                CGlobal.Inst.System.UpdateData();
                UpdateTrainingReadinessStatus();
                UpdatePythonValidationStatus(requireWeights: false);
                AppLog.NORMAL("YOLO 모델 설정을 저장했습니다.");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");                
            }
        }

        private void rjButton1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void InitializeDatasetSplitControls()
        {
            ClientSize = new Size(ClientSize.Width, 805);
            pnlClientArea.Size = new Size(pnlClientArea.Width, 761);
            btnApplyChanges.Location = new Point(btnApplyChanges.Left, 692);
            rjButton1.Location = new Point(rjButton1.Left, 692);

            lbValidationPercent = CreateSettingsLabel("검증 비율 (%)", new Point(27, 582));
            tbValidationPercent = CreateSettingsTextBox("tbValidationPercent", new Point(27, 606));
            lbSplitSeed = CreateSettingsLabel("분할 시드", new Point(304, 582));
            tbSplitSeed = CreateSettingsTextBox("tbSplitSeed", new Point(304, 606));

            pnlClientArea.Controls.Add(lbValidationPercent);
            pnlClientArea.Controls.Add(tbValidationPercent);
            pnlClientArea.Controls.Add(lbSplitSeed);
            pnlClientArea.Controls.Add(tbSplitSeed);
        }

        private void InitializeSettingsTabs()
        {
            tabTraining = new TabPage("학습 설정");
            tabPythonModel = new TabPage("검출 연동");
            tabTraining.BackColor = LabelingWorkbenchPalette.Panel;
            tabPythonModel.BackColor = LabelingWorkbenchPalette.Panel;
            tabTraining.AutoScroll = true;
            tabPythonModel.AutoScroll = true;

            settingsTabs = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(571, 660),
                Font = new Font("Verdana", 9F),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(118, 28),
                SizeMode = TabSizeMode.Fixed,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            settingsTabs.DrawItem += SettingsTabs_DrawItem;

            List<Control> trainingControls = pnlClientArea.Controls
                .Cast<Control>()
                .Where(control => control != btnApplyChanges && control != rjButton1)
                .ToList();

            foreach (Control control in trainingControls)
            {
                pnlClientArea.Controls.Remove(control);
                tabTraining.Controls.Add(control);
            }

            ShiftTrainingControls(92);
            InitializeTrainingReadinessPanel();
            settingsTabs.TabPages.Add(tabTraining);
            settingsTabs.TabPages.Add(tabPythonModel);
            pnlClientArea.Controls.Add(settingsTabs);
            btnApplyChanges.BringToFront();
            rjButton1.BringToFront();
        }

        private void ShiftTrainingControls(int yOffset)
        {
            foreach (Control control in tabTraining.Controls.Cast<Control>().ToList())
            {
                control.Top += yOffset;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ApplyLabelingSettingsChrome();
        }

        private void InitializeTrainingReadinessPanel()
        {
            trainingReadinessPanel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                Location = new Point(23, 24),
                Size = new Size(510, 74),
                Padding = new Padding(12, 8, 12, 8)
            };

            RJLabel title = new RJLabel
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Cursor = Cursors.Arrow,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                LinkLabel = false,
                Location = new Point(12, 7),
                Size = new Size(486, 20),
                Style = LabelStyle.Subtitle,
                Text = "학습 데이터 준비 상태"
            };

            lblTrainingReadinessStatus = new RJLabel
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Arrow,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                LinkLabel = false,
                Location = new Point(12, 29),
                Size = new Size(486, 18),
                Style = LabelStyle.Normal,
                Text = "데이터셋 확인 대기"
            };

            lblTrainingReadinessDetail = new RJLabel
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Arrow,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                LinkLabel = false,
                Location = new Point(12, 49),
                Size = new Size(486, 18),
                Style = LabelStyle.Normal,
                Text = ""
            };

            trainingReadinessPanel.Controls.Add(title);
            trainingReadinessPanel.Controls.Add(lblTrainingReadinessStatus);
            trainingReadinessPanel.Controls.Add(lblTrainingReadinessDetail);
            tabTraining.Controls.Add(trainingReadinessPanel);
        }

        private void InitializePythonModelControls()
        {
            tabPythonModel.Controls.Add(CreateSettingsTitle("Python 검출 연동", new Point(23, 26)));

            chkPythonAutoStart = new CheckBox
            {
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = LabelingWorkbenchPalette.Text,
                Location = new Point(27, 68),
                Name = "chkPythonAutoStart",
                Text = "검출 요청 시 Python 클라이언트 자동 시작",
                UseVisualStyleBackColor = true
            };
            tabPythonModel.Controls.Add(chkPythonAutoStart);

            AddPythonPathRow("YOLOv5 프로젝트", "tbPythonProjectRootPath", new Point(27, 115), BrowsePythonProjectRoot);
            AddPythonPathRow("통신 스크립트", "tbPythonClientScriptPath", new Point(27, 205), BrowsePythonClientScript);
            AddPythonPathRow("검출 가중치", "tbPythonWeightsPath", new Point(27, 295), BrowsePythonWeights);
            AddPythonPathRow("이미지 루트", "tbPythonImageRootPath", new Point(27, 385), BrowsePythonImageRoot);
            AddPythonPathRow("Python 실행 파일", "tbPythonExecutablePath", new Point(27, 475), BrowsePythonExecutable);
            AddMinimumConfidenceRow(new Point(27, 565));
            AddDetectionTimeoutRow(new Point(180, 565));
            AddPythonValidationStatusRow(new Point(27, 635));
            WirePythonValidationRefresh();
        }

        private void SettingsTabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
            {
                return;
            }

            bool selected = e.Index == tabControl.SelectedIndex;
            Rectangle bounds = e.Bounds;
            using (var brush = new SolidBrush(selected ? LabelingWorkbenchPalette.Selection : LabelingWorkbenchPalette.PanelHeader))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            TextRenderer.DrawText(
                e.Graphics,
                tabControl.TabPages[e.Index].Text,
                new Font("Segoe UI", 8.75F, selected ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
                bounds,
                selected ? Color.White : LabelingWorkbenchPalette.MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void AddMinimumConfidenceRow(Point labelLocation)
        {
            RJLabel label = CreateSettingsLabel("확정 기준 신뢰도 (0~1)", labelLocation);
            tbPythonMinimumConfidence = CreateSettingsTextBox("tbPythonMinimumConfidence", new Point(labelLocation.X, labelLocation.Y + 24));
            tbPythonMinimumConfidence.Size = new Size(120, 31);
            tbPythonMinimumConfidence.Text = "0.25";

            tabPythonModel.Controls.Add(label);
            tabPythonModel.Controls.Add(tbPythonMinimumConfidence);
        }

        private void AddDetectionTimeoutRow(Point labelLocation)
        {
            RJLabel label = CreateSettingsLabel("검출 제한 시간 (초)", labelLocation);
            tbPythonDetectionTimeoutSeconds = CreateSettingsTextBox("tbPythonDetectionTimeoutSeconds", new Point(labelLocation.X, labelLocation.Y + 24));
            tbPythonDetectionTimeoutSeconds.Size = new Size(120, 31);
            tbPythonDetectionTimeoutSeconds.Text = "30";

            tabPythonModel.Controls.Add(label);
            tabPythonModel.Controls.Add(tbPythonDetectionTimeoutSeconds);
        }

        private void AddPythonValidationStatusRow(Point location)
        {
            lblPythonValidationStatus = new RJLabel
            {
                AutoEllipsis = true,
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                Cursor = Cursors.Arrow,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                LinkLabel = false,
                Location = location,
                Padding = new Padding(8, 4, 8, 0),
                Size = new Size(486, 38),
                Style = LabelStyle.Normal,
                Text = "설정 확인 대기"
            };

            tabPythonModel.Controls.Add(lblPythonValidationStatus);
        }

        private void AddPythonPathRow(string labelText, string textBoxName, Point labelLocation, EventHandler browseHandler)
        {
            RJLabel label = CreateSettingsLabel(labelText, labelLocation);
            RJTextBox textBox = CreateSettingsTextBox(textBoxName, new Point(labelLocation.X, labelLocation.Y + 24));
            textBox.Size = new Size(446, 31);

            RJButton browseButton = CreateBrowseButton(new Point(textBox.Right + 8, textBox.Top), browseHandler);

            tabPythonModel.Controls.Add(label);
            tabPythonModel.Controls.Add(textBox);
            tabPythonModel.Controls.Add(browseButton);

            switch (textBoxName)
            {
                case "tbPythonExecutablePath":
                    tbPythonExecutablePath = textBox;
                    break;
                case "tbPythonProjectRootPath":
                    tbPythonProjectRootPath = textBox;
                    break;
                case "tbPythonClientScriptPath":
                    tbPythonClientScriptPath = textBox;
                    break;
                case "tbPythonWeightsPath":
                    tbPythonWeightsPath = textBox;
                    break;
                case "tbPythonImageRootPath":
                    tbPythonImageRootPath = textBox;
                    break;
            }
        }

        private void BrowsePythonProjectRoot(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(tbPythonProjectRootPath.Text))
                {
                    dialog.SelectedPath = tbPythonProjectRootPath.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    tbPythonProjectRootPath.Text = dialog.SelectedPath;
                    if (string.IsNullOrWhiteSpace(tbPythonClientScriptPath.Text))
                    {
                        tbPythonClientScriptPath.Text = Path.Combine(dialog.SelectedPath, "labelling_tcp_client.py");
                    }

                    UpdatePythonValidationStatus(requireWeights: false);
                }
            }
        }

        private void BrowsePythonClientScript(object sender, EventArgs e)
        {
            BrowseFile(tbPythonClientScriptPath, "Python script (*.py)|*.py|All files (*.*)|*.*", tbPythonProjectRootPath.Text);
            UpdatePythonValidationStatus(requireWeights: false);
        }

        private void BrowsePythonWeights(object sender, EventArgs e)
        {
            BrowseFile(tbPythonWeightsPath, "PyTorch weights (*.pt)|*.pt|All files (*.*)|*.*", tbPythonProjectRootPath.Text);
            UpdatePythonValidationStatus(requireWeights: false);
        }

        private void BrowsePythonImageRoot(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(tbPythonImageRootPath.Text))
                {
                    dialog.SelectedPath = tbPythonImageRootPath.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    tbPythonImageRootPath.Text = dialog.SelectedPath;
                    UpdatePythonValidationStatus(requireWeights: false);
                }
            }
        }

        private void BrowsePythonExecutable(object sender, EventArgs e)
        {
            BrowseFile(tbPythonExecutablePath, "Python executable (python.exe;py.exe)|python.exe;py.exe|All files (*.*)|*.*", "");
            UpdatePythonValidationStatus(requireWeights: false);
        }

        private void BrowseFile(RJTextBox target, string filter, string initialDirectory)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = filter;
                if (Directory.Exists(initialDirectory))
                {
                    dialog.InitialDirectory = initialDirectory;
                }
                else if (!string.IsNullOrWhiteSpace(target.Text))
                {
                    string directoryName = Path.GetDirectoryName(target.Text);
                    if (Directory.Exists(directoryName))
                    {
                        dialog.InitialDirectory = directoryName;
                    }
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = dialog.FileName;
                }
            }
        }

        private static RJLabel CreateSettingsTitle(string text, Point location)
        {
            return new RJLabel
            {
                AutoSize = true,
                Cursor = Cursors.Arrow,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Accent,
                LinkLabel = false,
                Location = location,
                Style = LabelStyle.Title,
                Text = text
            };
        }

        private void ApplyLabelingSettingsChrome()
        {
            Text = "모델 설정";
            Caption = "모델 설정";
            BackColor = LabelingWorkbenchPalette.Frame;
            BorderColor = LabelingWorkbenchPalette.Frame;
            Control titleBar = Controls.Find("pnlTitleBar", searchAllChildren: false).FirstOrDefault();
            if (titleBar != null)
            {
                titleBar.BackColor = LabelingWorkbenchPalette.PanelHeader;
            }

            pnlClientArea.BackColor = LabelingWorkbenchPalette.Panel;
            btnApplyChanges.Text = "설정 저장";
            btnApplyChanges.BorderRadius = 8;
            btnApplyChanges.BackColor = LabelingWorkbenchPalette.Selection;
            btnApplyChanges.BorderColor = LabelingWorkbenchPalette.Selection;
            btnApplyChanges.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            btnApplyChanges.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.AccentHover;
            rjButton1.Text = "닫기";
            rjButton1.BorderRadius = 8;
            rjButton1.BackColor = Color.FromArgb(82, 87, 96);
            rjButton1.BorderColor = Color.FromArgb(82, 87, 96);

            rjLabel1.Text = "YOLO 학습 설정";
            rjLabel2.Text = "이미지 크기";
            rjLabel3.Text = "배치";
            rjLabel7.Text = "모델 구조";
            rjLabel9.Text = "초기 가중치";
            rjLabel16.Text = "에폭";
            ApplyLegacySettingsLabelTheme(pnlClientArea);
            ApplyComboTheme(cbcfg);
            ApplyComboTheme(cbweight);
            if (settingsTabs != null)
            {
                settingsTabs.BackColor = LabelingWorkbenchPalette.Panel;
                settingsTabs.ForeColor = LabelingWorkbenchPalette.Text;
                settingsTabs.Invalidate();
            }

            if (chkPythonAutoStart != null)
            {
                chkPythonAutoStart.BackColor = LabelingWorkbenchPalette.Panel;
                chkPythonAutoStart.ForeColor = LabelingWorkbenchPalette.Text;
                chkPythonAutoStart.FlatStyle = FlatStyle.Flat;
                chkPythonAutoStart.UseVisualStyleBackColor = false;
            }
        }

        private static void ApplyLegacySettingsLabelTheme(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is RJLabel label)
                {
                    label.ForeColor = label.Style == LabelStyle.Title
                        ? LabelingWorkbenchPalette.Accent
                        : label.Style == LabelStyle.Subtitle
                            ? LabelingWorkbenchPalette.Text
                            : LabelingWorkbenchPalette.MutedText;
                    label.Font = new Font("Segoe UI", label.Style == LabelStyle.Title ? 13F : label.Style == LabelStyle.Subtitle ? 10F : 8.75F,
                        label.Style == LabelStyle.Subtitle ? FontStyle.Bold : FontStyle.Regular,
                        GraphicsUnit.Point);
                }

                if (control is RJTextBox textBox)
                {
                    ApplyTextBoxTheme(textBox);
                }

                if (control is RJComboBox comboBox)
                {
                    ApplyComboTheme(comboBox);
                }

                if (control.HasChildren)
                {
                    ApplyLegacySettingsLabelTheme(control);
                }
            }
        }

        private void WirePythonValidationRefresh()
        {
            tbPythonProjectRootPath.TextChanged += PythonSettingsInputChanged;
            tbPythonClientScriptPath.TextChanged += PythonSettingsInputChanged;
            tbPythonWeightsPath.TextChanged += PythonSettingsInputChanged;
            tbPythonImageRootPath.TextChanged += PythonSettingsInputChanged;
            tbPythonExecutablePath.TextChanged += PythonSettingsInputChanged;
            tbPythonMinimumConfidence.TextChanged += PythonSettingsInputChanged;
            tbPythonDetectionTimeoutSeconds.TextChanged += PythonSettingsInputChanged;
            chkPythonAutoStart.CheckedChanged += PythonSettingsInputChanged;
        }

        private void PythonSettingsInputChanged(object sender, EventArgs e)
        {
            UpdatePythonValidationStatus(requireWeights: false);
        }

        private void UpdateTrainingReadinessStatus()
        {
            if (lblTrainingReadinessStatus == null || lblTrainingReadinessDetail == null)
            {
                return;
            }

            CGlobal.Inst.Data.ProjectSettings ??= new LabelingProjectSettings();
            CGlobal.Inst.Data.ProjectSettings.EnsureDefaults();
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(CGlobal.Inst.Data, refreshYaml: false);
            if (report.IsReady)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                lblTrainingReadinessStatus.ForeColor = Color.FromArgb(190, 232, 212);
                lblTrainingReadinessStatus.Text = "학습 가능";
                lblTrainingReadinessDetail.Text =
                    $"학습 {statistics.TrainImageCount} / 검증 {statistics.ValidImageCount} / 객체 {statistics.TotalObjectCount} / 클래스 {CGlobal.Inst.Data.ClassNamedList.Count}";
                return;
            }

            lblTrainingReadinessStatus.ForeColor = Color.FromArgb(255, 178, 160);
            lblTrainingReadinessStatus.Text = "학습 전 데이터셋 확인 필요";
            lblTrainingReadinessDetail.Text = TranslateDatasetReadinessMessage(report.Errors.FirstOrDefault());
        }

        private static string TranslateDatasetReadinessMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "세부 메시지 없음";
            }

            if (message.Contains("class", StringComparison.OrdinalIgnoreCase))
            {
                return "클래스를 하나 이상 등록해야 합니다.";
            }

            if (message.Contains("data.yaml", StringComparison.OrdinalIgnoreCase))
            {
                return "data.yaml 생성 또는 저장 상태를 확인하세요.";
            }

            if (message.Contains("train", StringComparison.OrdinalIgnoreCase)
                || message.Contains("valid", StringComparison.OrdinalIgnoreCase))
            {
                return "학습/검증 이미지와 라벨 파일 구성을 확인하세요.";
            }

            if (message.Contains("output", StringComparison.OrdinalIgnoreCase))
            {
                return "YOLO 데이터셋 출력 경로를 확인하세요.";
            }

            if (message.Contains("image size", StringComparison.OrdinalIgnoreCase))
            {
                return "이미지 크기는 0보다 커야 합니다.";
            }

            if (message.Contains("batch", StringComparison.OrdinalIgnoreCase))
            {
                return "배치 값은 0보다 커야 합니다.";
            }

            if (message.Contains("epoch", StringComparison.OrdinalIgnoreCase))
            {
                return "에폭 값은 0보다 커야 합니다.";
            }

            return message;
        }

        private void UpdatePythonValidationStatus(bool requireWeights)
        {
            if (lblPythonValidationStatus == null
                || tbPythonProjectRootPath == null
                || tbPythonClientScriptPath == null
                || tbPythonWeightsPath == null
                || tbPythonImageRootPath == null
                || tbPythonExecutablePath == null
                || tbPythonMinimumConfidence == null
                || tbPythonDetectionTimeoutSeconds == null)
            {
                return;
            }

            PythonModelSettings settings = BuildPythonModelSettingsFromUi();
            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(settings, requireWeights);
            lblPythonValidationStatus.Text = BuildPythonValidationStatusText(validation);
            lblPythonValidationStatus.ForeColor = validation.IsValid
                ? validation.Warnings.Count > 0 ? Color.FromArgb(244, 199, 134) : Color.FromArgb(190, 232, 212)
                : Color.FromArgb(255, 178, 160);
        }

        private PythonModelSettings BuildPythonModelSettingsFromUi()
        {
            float minimumConfidence = 0.25F;
            if (float.TryParse(tbPythonMinimumConfidence.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedConfidence))
            {
                minimumConfidence = parsedConfidence;
            }
            else
            {
                minimumConfidence = float.NaN;
            }

            int detectionTimeoutSeconds = 30;
            if (!int.TryParse(tbPythonDetectionTimeoutSeconds.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out detectionTimeoutSeconds))
            {
                detectionTimeoutSeconds = -1;
            }

            return new PythonModelSettings
            {
                PythonExecutablePath = tbPythonExecutablePath.Text.Trim(),
                ProjectRootPath = tbPythonProjectRootPath.Text.Trim(),
                ClientScriptPath = tbPythonClientScriptPath.Text.Trim(),
                WeightsPath = tbPythonWeightsPath.Text.Trim(),
                ImageRootPath = tbPythonImageRootPath.Text.Trim(),
                MinimumDetectionConfidence = minimumConfidence,
                DetectionTimeoutSeconds = detectionTimeoutSeconds,
                AutoStartClient = chkPythonAutoStart.Checked
            };
        }

        private static string BuildPythonValidationStatusText(PythonModelValidationResult validation)
        {
            if (validation == null)
            {
                return "설정 확인 대기";
            }

            if (!validation.IsValid)
            {
                return $"확인 필요: {TranslatePythonValidationMessage(validation.Errors.FirstOrDefault())}";
            }

            if (validation.Warnings.Count > 0)
            {
                return $"사용 가능 / 경고: {TranslatePythonValidationMessage(validation.Warnings.FirstOrDefault())}";
            }

            return "검출 연동 설정 정상";
        }

        private static string TranslatePythonValidationMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "세부 메시지 없음";
            }

            if (message.Contains("project root", StringComparison.OrdinalIgnoreCase))
            {
                return "YOLOv5 프로젝트 경로를 확인하세요.";
            }

            if (message.Contains("client script", StringComparison.OrdinalIgnoreCase))
            {
                return "통신 스크립트 경로를 확인하세요.";
            }

            if (message.Contains("Python executable", StringComparison.OrdinalIgnoreCase))
            {
                return "Python 실행 파일 경로를 확인하세요.";
            }

            if (message.Contains("weight file", StringComparison.OrdinalIgnoreCase))
            {
                return "검출 가중치 파일 경로를 확인하세요.";
            }

            if (message.Contains("Image root", StringComparison.OrdinalIgnoreCase))
            {
                return "이미지 루트 경로를 확인하세요.";
            }

            if (message.Contains("Minimum detection confidence", StringComparison.OrdinalIgnoreCase))
            {
                return "확정 기준 신뢰도는 0~1 사이여야 합니다.";
            }

            if (message.Contains("Detection timeout", StringComparison.OrdinalIgnoreCase))
            {
                return "검출 제한 시간은 1~600초 사이여야 합니다.";
            }

            return message;
        }

        private static RJLabel CreateSettingsLabel(string text, Point location)
        {
            return new RJLabel
            {
                AutoSize = true,
                Cursor = Cursors.Arrow,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                LinkLabel = false,
                Location = location,
                Style = LabelStyle.Subtitle,
                Text = text
            };
        }

        private static RJButton CreateBrowseButton(Point location, EventHandler clickHandler)
        {
            var button = new RJButton
            {
                BackColor = LabelingWorkbenchPalette.Selection,
                BorderColor = LabelingWorkbenchPalette.Selection,
                BorderRadius = 8,
                BorderSize = 1,
                Cursor = Cursors.Hand,
                Design = ButtonDesign.IconButton,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                IconChar = FontAwesome.Sharp.IconChar.FolderOpen,
                IconColor = Color.White,
                IconFont = FontAwesome.Sharp.IconFont.Auto,
                IconSize = 18,
                Location = location,
                Size = new Size(40, 31),
                Style = ControlStyle.Solid,
                Text = "",
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.AccentHover;
            button.Click += clickHandler;
            return button;
        }

        private static RJTextBox CreateSettingsTextBox(string name, Point location)
        {
            var textBox = new RJTextBox
            {
                _Customizable = false,
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                BorderColor = LabelingWorkbenchPalette.Divider,
                BorderFocusColor = LabelingWorkbenchPalette.Accent,
                BorderRadius = 6,
                BorderSize = 1,
                Font = new Font("Segoe UI", 9F),
                ForeColor = LabelingWorkbenchPalette.Text,
                Location = location,
                MultiLine = false,
                Name = name,
                Padding = new Padding(10, 7, 10, 7),
                PasswordChar = false,
                PlaceHolderColor = Color.DarkGray,
                ScrollBars = ScrollBars.None,
                Size = new Size(241, 31),
                Style = TextBoxStyle.MatteBorder
            };
            ApplyTextBoxTheme(textBox);
            return textBox;
        }

        private static void ApplyTextBoxTheme(RJTextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            textBox.BorderColor = LabelingWorkbenchPalette.Divider;
            textBox.BorderFocusColor = LabelingWorkbenchPalette.Accent;
            textBox.ForeColor = LabelingWorkbenchPalette.Text;
            textBox.PlaceHolderColor = LabelingWorkbenchPalette.MutedText;
        }

        private static void ApplyComboTheme(RJComboBox comboBox)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            comboBox.BorderColor = LabelingWorkbenchPalette.Divider;
            comboBox.BorderRadius = 6;
            comboBox.BorderSize = 1;
            comboBox.DropDownBackColor = LabelingWorkbenchPalette.SurfaceAlt;
            comboBox.DropDownTextColor = LabelingWorkbenchPalette.Text;
            comboBox.DropDownSelectedBackColor = LabelingWorkbenchPalette.Selection;
            comboBox.DropDownSelectedTextColor = Color.White;
            comboBox.IconColor = LabelingWorkbenchPalette.Accent;
            comboBox.ForeColor = LabelingWorkbenchPalette.Text;
            comboBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
