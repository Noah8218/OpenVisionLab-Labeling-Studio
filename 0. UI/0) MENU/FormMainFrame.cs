using Lib.Common;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using RJCodeUI_M1.RJControls;
using RJCodeUI_M1.RJForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MvcVisionSystem._3._Communication.TCP.CCommunicationLearning;

namespace MvcVisionSystem
{
    public partial class FormMainFrame : Form
    {
        private readonly CGlobal Global = CGlobal.Inst;
        private readonly FormInit formInit;
        private FormTeachingVision FrmVision;
        private FormVision_ClassMenu formVision_ClassMenu;
        private RJButton btnAcceptDetection;
        private RJButton btnYoloConnect;
        private RJButton btnYoloStop;
        private Label lblWorkbenchTitle;
        private Label lblWorkbenchSubtitle;
        private RJLabel lbTrainingStatus;
        private Panel classSelectorPanel;
        private Panel workspaceModePanel;
        private Panel workflowCommandPanel;
        private Panel utilityCommandPanel;
        private Panel statusBarTopLine;
        private RJButton btnTeachingWorkspace;
        private RJButton btnDetectionReviewWorkspace;
        private RJButton btnTrainingWorkspace;
        private ToolTip mainCommandToolTip;
        private RJLabel lbYoloListenerStatus;
        private RJLabel lbYoloClientStatus;
        private RJLabel lbYoloModelStatus;
        private int statusRefreshTick;
        private bool refreshingClassSelector;
        private bool yoloOperationInProgress;
        private bool isClosing;
        private bool shutdownCleanupStarted;
        private bool shutdownCleanupCompleted;
        private DEFINE.LABELING_WORKSPACE_MODE currentWorkspaceMode = DEFINE.LABELING_WORKSPACE_MODE.TEACHING;
        private DateTime? trainingStopRequestedAtUtc;
        private const int TrainingStopPendingSeconds = 15;

        private sealed class YoloStatusView
        {
            public string StatusText { get; set; } = "";
            public string ButtonText { get; set; } = "";
            public string ToolTip { get; set; } = "";
            public Color StatusColor { get; set; }
            public Color ButtonColor { get; set; }
            public Color ButtonHoverColor { get; set; }
            public bool StopEnabled { get; set; }
        }

        public FormMainFrame(FormInit formInit)
        {
            this.formInit = formInit;
            InitializeComponent();
            InitializeRuntimeToolbarButtons();
            KeyPreview = true;
            KeyDown += FormMainFrame_KeyDown;
            Global.System.OnDataUpdated += System_OnDataUpdated;
            Global.DetectionResults.DetectionCandidatesUpdated += DetectionResults_DetectionCandidatesUpdated;
            FormScreenPlacement.MaximizeOnPreferredScreen(this);
            Resize += FormMainFrame_Resize;
        }

        private void FormMainFrame_Load(object sender, EventArgs e)
        {
            InitUI();
        }

        private void FormMainFrame_Shown(object sender, EventArgs e)
        {
            if (formInit != null)
            {
                formInit.OnInitEnd();
            }
        }

        private void InitUI()
        {
            ApplyMainTheme();
            lbVersion.Text = $"VERSION : {CVersion.VERSION} - {CVersion.DATETIME_UPDATED} ({CVersion.MANAGER})";
            Global.Data.NormalizeOutputPaths();
            UpdateOutputPathDisplay();
            StartPythonCommunicationListener();

            FrmVision = new FormTeachingVision();
            FrmVision.TopLevel = false;
            FrmVision.FormBorderStyle = FormBorderStyle.None;
            FrmVision.Dock = DockStyle.Fill;
            if (!mainWorkspacePanel.Controls.Contains(mainContentPanel))
            {
                mainWorkspacePanel.Controls.Add(mainContentPanel);
                mainContentPanel.Dock = DockStyle.Fill;
            }

            teachingHostPanel.Controls.Clear();
            teachingHostPanel.Controls.Add(FrmVision);
            FrmVision.Show();
            SetWorkbenchMode(currentWorkspaceMode);
            RefreshClassSelector();
            ApplyResponsiveLayout();
            RefreshStatusBar(includeDataset: true);
        }

        private void StartPythonCommunicationListener()
        {
            try
            {
                _ = Global.DeepLearning;
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Python TCP listener startup failed: {ex.Message}");
            }
        }

        private async void btnYoloConnect_Click(object sender, EventArgs e)
        {
            if (isClosing || yoloOperationInProgress)
            {
                return;
            }

            SetYoloOperationState(true, "연결중", Color.FromArgb(85, 105, 55), Color.FromArgb(112, 132, 68));
            try
            {
                AppLog.COMM("YOLO client connection requested by operator.");
                bool connected = await Global.StartPythonModelClientConnectionAsync(8000);
                PythonCommunicationStatus status = Global.GetPythonCommunicationStatusSnapshot();
                if (connected)
                {
                    string processMode = Global.PythonClientProcess.IsRunning ? "internal process" : "external/manual client";
                    AppLog.COMM($"YOLO client connected. Mode:{processMode}, Listener:{status.IsListening}, Client:{status.IsClientConnected}");
                }
                else
                {
                    AppLog.ABNORMAL($"YOLO client is not connected. Listener:{status.IsListening}, Client:{status.IsClientConnected}, Error:{FirstNonEmpty(status.LastError, Global.PythonClientProcess.LastError, "none")}");
                }
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLO client connection failed: {ex.Message}");
            }
            finally
            {
                if (!isClosing && !IsDisposed)
                {
                    SetYoloOperationState(false);
                    RefreshStatusBar(includeDataset: false);
                }
            }
        }

        private async void btnYoloStop_Click(object sender, EventArgs e)
        {
            if (isClosing || yoloOperationInProgress)
            {
                return;
            }

            SetYoloOperationState(true, "중지중", Color.FromArgb(114, 75, 82), Color.FromArgb(146, 83, 91));
            try
            {
                AppLog.COMM("YOLO client/listener stop requested by operator.");
                await Global.StopPythonModelClientConnectionAsync();
                AppLog.COMM("YOLO client/listener stopped.");
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLO client/listener stop failed: {ex.Message}");
            }
            finally
            {
                if (!isClosing && !IsDisposed)
                {
                    SetYoloOperationState(false);
                    RefreshStatusBar(includeDataset: false);
                }
            }
        }

        private async Task<bool> EnsurePythonClientReadyForUiAsync(int timeoutMilliseconds, string requestName)
        {
            if (yoloOperationInProgress)
            {
                AppLog.COMM($"{requestName} skipped because a YOLO operation is already running.");
                return false;
            }

            SetYoloOperationState(true, "연결중", Color.FromArgb(85, 105, 55), Color.FromArgb(112, 132, 68));
            try
            {
                bool ready = await Global.EnsurePythonModelClientReadyAsync(timeoutMilliseconds);
                if (!ready)
                {
                    PythonCommunicationStatus status = Global.GetPythonCommunicationStatusSnapshot();
                    AppLog.ABNORMAL($"{requestName} skipped because YOLO client is not connected. Listener:{status.IsListening}, Client:{status.IsClientConnected}, Error:{FirstNonEmpty(status.LastError, Global.PythonClientProcess.LastError, "none")}");
                }

                return ready;
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"{requestName} failed while waiting for YOLO client: {ex.Message}");
                return false;
            }
            finally
            {
                SetYoloOperationState(false);
                RefreshStatusBar(includeDataset: false);
            }
        }

        private void SetYoloOperationState(bool inProgress, string statusText = null, Color? backColor = null, Color? hoverColor = null)
        {
            if (isClosing || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(() => SetYoloOperationState(inProgress, statusText, backColor, hoverColor));
                return;
            }

            yoloOperationInProgress = inProgress;
            bool enabled = !inProgress;

            if (btnYoloConnect != null)
            {
                btnYoloConnect.Enabled = enabled;
            }

            if (btnYoloStop != null)
            {
                btnYoloStop.Enabled = enabled;
            }

            if (btnDetectCurrentImage != null)
            {
                btnDetectCurrentImage.Enabled = enabled;
            }

            if (btnStartTraining != null)
            {
                btnStartTraining.Enabled = enabled;
            }

            UseWaitCursor = inProgress;
            Cursor = inProgress ? Cursors.AppStarting : Cursors.Default;

            if (inProgress && !string.IsNullOrWhiteSpace(statusText))
            {
                SetYoloStatusButton(
                    statusText,
                    backColor ?? Color.FromArgb(85, 105, 55),
                    hoverColor ?? Color.FromArgb(112, 132, 68));
                if (lbPythonStatus != null)
                {
                    lbPythonStatus.ForeColor = Color.FromArgb(224, 218, 157);
                    lbPythonStatus.Text = $"YOLO {statusText}...";
                    mainCommandToolTip?.SetToolTip(lbPythonStatus, $"YOLO {statusText} 작업이 백그라운드에서 진행 중입니다.");
                }
            }
        }

        private void FormMainFrame_Resize(object sender, EventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void btnTeachingWorkspace_Click(object sender, EventArgs e)
        {
            SetWorkbenchMode(DEFINE.LABELING_WORKSPACE_MODE.TEACHING);
        }

        private void btnDetectionReviewWorkspace_Click(object sender, EventArgs e)
        {
            SetWorkbenchMode(DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW);
        }

        private void btnTrainingWorkspace_Click(object sender, EventArgs e)
        {
            SetWorkbenchMode(DEFINE.LABELING_WORKSPACE_MODE.TRAINING);
        }

        private void SetWorkbenchMode(DEFINE.LABELING_WORKSPACE_MODE mode)
        {
            currentWorkspaceMode = mode;
            FrmVision?.SetWorkspaceMode(mode);
            UpdateWorkspaceModeChrome();
            ApplyResponsiveLayout();
        }

        private void UpdateWorkspaceModeChrome()
        {
            if (lblWorkbenchTitle == null || lblWorkbenchSubtitle == null)
            {
                return;
            }

            switch (currentWorkspaceMode)
            {
                case DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW:
                    lblWorkbenchTitle.Text = "Review Studio";
                    lblWorkbenchSubtitle.Text = "AI detection / candidate review";
                    break;
                case DEFINE.LABELING_WORKSPACE_MODE.TRAINING:
                    lblWorkbenchTitle.Text = "Training Studio";
                    lblWorkbenchSubtitle.Text = "Dataset readiness / learning";
                    break;
                default:
                    lblWorkbenchTitle.Text = "Labeling Studio";
                    lblWorkbenchSubtitle.Text = "Manual ROI / segmentation";
                    break;
            }

            ApplyWorkspaceModeButtonTheme(btnTeachingWorkspace, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING);
            ApplyWorkspaceModeButtonTheme(btnDetectionReviewWorkspace, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW);
            ApplyWorkspaceModeButtonTheme(btnTrainingWorkspace, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TRAINING);
        }

        private static void ApplyWorkspaceModeButtonTheme(RJButton button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Color backColor = selected ? LabelingWorkbenchPalette.Selection : LabelingWorkbenchPalette.SurfaceAlt;
            Color hoverColor = selected ? LabelingWorkbenchPalette.AccentHover : LabelingWorkbenchPalette.PanelHeader;
            button.BackColor = backColor;
            button.BorderColor = selected ? LabelingWorkbenchPalette.Accent : backColor;
            button.FlatAppearance.BorderColor = button.BorderColor;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
        }

        private void ApplyResponsiveLayout()
        {
            if (pnlTitleBar == null || pnlTitleBar.ClientSize.Width <= 0)
            {
                return;
            }

            int width = pnlTitleBar.ClientSize.Width;
            bool compact = width <= 1700;
            bool tight = width <= 1400;
            int margin = compact ? 6 : 8;
            int titleHeight = compact ? 62 : 66;
            int groupTop = 6;
            int groupHeight = Math.Max(44, titleHeight - 12);
            int buttonHeight = compact ? 42 : 44;
            int primaryButtonWidth = tight ? 72 : compact ? 78 : 88;
            int secondaryButtonWidth = tight ? 56 : compact ? 60 : 66;
            int classButtonWidth = tight ? 62 : compact ? 66 : 72;
            int groupPadding = 6;
            int commandGap = compact ? 5 : 6;

            pnlTitleBar.SuspendLayout();
            outputPathPanel?.SuspendLayout();
            imageNavigatorPanel?.SuspendLayout();
            classSelectorPanel?.SuspendLayout();
            workspaceModePanel?.SuspendLayout();
            workflowCommandPanel?.SuspendLayout();
            utilityCommandPanel?.SuspendLayout();

            try
            {
                pnlTitleBar.Height = titleHeight;
                pnStatusBar.Height = compact ? 30 : 32;
                EnsureMainFrameChrome();
                LayoutStatusBarChrome();

                lbVersion.Font = new Font("Segoe UI", compact ? 8.5f : 9.5f, FontStyle.Regular);
                lbVersion.AutoEllipsis = true;
                lbVersion.Padding = new Padding(0, 5, 10, 0);
                ConfigureStatusLabel(lbDatasetStatus, compact ? 360 : 460, compact);
                ConfigureStatusLabel(lbPythonStatus, compact ? 300 : 390, compact);
                if (lbTrainingStatus != null)
                {
                    ConfigureStatusLabel(lbTrainingStatus, compact ? 210 : 280, compact);
                }

                ConfigureStatusChip(lbYoloListenerStatus, compact ? 102 : 118, compact);
                ConfigureStatusChip(lbYoloClientStatus, compact ? 100 : 116, compact);
                ConfigureStatusChip(lbYoloModelStatus, compact ? 112 : 132, compact);

                ConfigureToolbarButton(btnClassSettings, classButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnSaveProject, primaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnStartTraining, secondaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnYoloConnect, secondaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnYoloStop, secondaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnDetectCurrentImage, primaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnAcceptDetection, primaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnUserOptions, secondaryButtonWidth, buttonHeight, compact);
                ConfigureToolbarButton(btnScreenCapture, secondaryButtonWidth, buttonHeight, compact);

                int right = width - margin;
                btnClose.SetBounds(right - 30, compact ? 16 : 18, 30, 30);
                right = btnClose.Left - 2;
                btnMinimize.SetBounds(right - 30, compact ? 16 : 18, 30, 30);
                right = btnMinimize.Left - margin;

                int utilityPanelWidth = GetCommandPanelWidth(5, secondaryButtonWidth, groupPadding, commandGap);
                int workflowPanelWidth = GetCommandPanelWidth(3, primaryButtonWidth, groupPadding, commandGap);
                utilityCommandPanel.SetBounds(right - utilityPanelWidth, groupTop, utilityPanelWidth, groupHeight);
                LayoutCommandPanel(utilityCommandPanel, secondaryButtonWidth, buttonHeight, groupPadding, commandGap,
                    btnYoloConnect, btnYoloStop, btnStartTraining, btnUserOptions, btnScreenCapture);
                right = utilityCommandPanel.Left - margin;

                workflowCommandPanel.SetBounds(right - workflowPanelWidth, groupTop, workflowPanelWidth, groupHeight);
                LayoutCommandPanel(workflowCommandPanel, primaryButtonWidth, buttonHeight, groupPadding, commandGap,
                    btnDetectCurrentImage, btnAcceptDetection, btnSaveProject);
                right = workflowCommandPanel.Left - margin;

                int titleWidth = tight ? 166 : compact ? 200 : 238;
                lblWorkbenchTitle.Font = new Font("Segoe UI", compact ? 14.25F : 15.5F, FontStyle.Bold, GraphicsUnit.Point);
                lblWorkbenchTitle.SetBounds(14, compact ? 7 : 8, titleWidth, compact ? 25 : 27);
                lblWorkbenchSubtitle.Font = new Font("Segoe UI", compact ? 8F : 8.5F, FontStyle.Regular, GraphicsUnit.Point);
                lblWorkbenchSubtitle.SetBounds(16, compact ? 33 : 36, titleWidth, 18);

                int left = lblWorkbenchTitle.Right + margin;
                int leftLimit = Math.Max(left, right);
                int modeButtonWidth = tight ? 54 : compact ? 60 : 66;
                int modePanelWidth = GetCommandPanelWidth(3, modeButtonWidth, groupPadding, commandGap);
                workspaceModePanel.Visible = left + modePanelWidth < leftLimit;
                if (workspaceModePanel.Visible)
                {
                    workspaceModePanel.SetBounds(left, groupTop, modePanelWidth, groupHeight);
                    LayoutCommandPanel(workspaceModePanel, modeButtonWidth, buttonHeight, groupPadding, commandGap,
                        btnTeachingWorkspace, btnDetectionReviewWorkspace, btnTrainingWorkspace);
                    left = workspaceModePanel.Right + margin;
                }

                int classPanelAvailable = Math.Max(0, leftLimit - left);
                int classPanelWidth = Math.Min(tight ? 190 : compact ? 226 : 248, classPanelAvailable);
                classSelectorPanel.Visible = classPanelWidth >= 160;
                if (classSelectorPanel.Visible)
                {
                    classSelectorPanel.SetBounds(left, groupTop, classPanelWidth, groupHeight);
                    LayoutClassSelectorPanel(classSelectorPanel, classButtonWidth, buttonHeight, compact);
                    left = classSelectorPanel.Right + margin;
                }

                int available = Math.Max(0, leftLimit - left);
                int pathMinWidth = tight ? 180 : compact ? 220 : 260;
                int pathMaxWidth = tight ? 260 : compact ? 320 : 390;
                int navigatorMinWidth = compact ? 210 : 240;
                int navigatorMaxWidth = compact ? 300 : 360;
                bool showPath = available >= pathMinWidth;
                bool showImageNavigator = available >= pathMinWidth + margin + navigatorMinWidth;
                int outputPathPanelWidth = 0;
                int imageNavigatorPanelWidth = 0;

                if (showPath)
                {
                    if (showImageNavigator)
                    {
                        outputPathPanelWidth = Math.Min(pathMaxWidth, Math.Max(pathMinWidth, available - margin - navigatorMinWidth));
                        imageNavigatorPanelWidth = Math.Min(navigatorMaxWidth, Math.Max(navigatorMinWidth, available - outputPathPanelWidth - margin));
                    }
                    else
                    {
                        outputPathPanelWidth = Math.Min(pathMaxWidth, available);
                    }
                }

                outputPathPanel.Visible = showPath;
                if (showPath)
                {
                    outputPathPanel.SetBounds(left, groupTop, outputPathPanelWidth, groupHeight);
                    LayoutOutputPathPanel(outputPathPanel, secondaryButtonWidth, buttonHeight, compact);
                    left = outputPathPanel.Right + margin;
                }

                imageNavigatorPanel.Visible = showImageNavigator && imageNavigatorPanelWidth >= navigatorMinWidth;
                if (imageNavigatorPanel.Visible)
                {
                    imageNavigatorPanel.SetBounds(left, compact ? 13 : 15, imageNavigatorPanelWidth, 36);
                }

                lbExportPath.AutoEllipsis = true;
                lbExportPath.TextAlign = ContentAlignment.MiddleLeft;
                lbExportPath.Font = new Font("Segoe UI", compact ? 8.25f : 9f, FontStyle.Regular);
                lbExportPath.Padding = new Padding(10, 0, 4, 0);

                lbSelectImageName.AutoEllipsis = true;
                lbSelectImageName.Font = new Font("Segoe UI", compact ? 9f : 10f, FontStyle.Regular);
                lbSelectImageName.Padding = new Padding(6, 0, 6, 0);

                btnClose.BringToFront();
                btnMinimize.BringToFront();
            }
            finally
            {
                utilityCommandPanel?.ResumeLayout();
                workflowCommandPanel?.ResumeLayout();
                workspaceModePanel?.ResumeLayout();
                classSelectorPanel?.ResumeLayout();
                imageNavigatorPanel?.ResumeLayout();
                outputPathPanel?.ResumeLayout();
                pnlTitleBar.ResumeLayout();
            }
        }

        private void ConfigureToolbarButton(RJButton button, int width, int height, bool compact)
        {
            if (button == null)
            {
                return;
            }

            button.Size = new Size(width, height);
            button.Dock = DockStyle.None;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            button.Margin = Padding.Empty;
            button.IconSize = compact ? 20 : 23;
            button.Font = new Font("Segoe UI", compact ? 7f : 7.5f, FontStyle.Regular);
            button.BorderRadius = 4;
            button.Padding = new Padding(0);
        }

        private static int GetCommandPanelWidth(int buttonCount, int buttonWidth, int padding, int gap)
        {
            return (padding * 2) + (buttonCount * buttonWidth) + (Math.Max(0, buttonCount - 1) * gap);
        }

        private static void LayoutCommandPanel(Panel panel, int buttonWidth, int buttonHeight, int padding, int gap, params RJButton[] buttons)
        {
            if (panel == null)
            {
                return;
            }

            panel.BackColor = LabelingWorkbenchPalette.Frame;
            int x = padding;
            int y = Math.Max(0, (panel.Height - buttonHeight) / 2);
            foreach (RJButton button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                button.SetBounds(x, y, buttonWidth, buttonHeight);
                button.BringToFront();
                x += buttonWidth + gap;
            }
        }

        private void LayoutClassSelectorPanel(Panel panel, int buttonWidth, int buttonHeight, bool compact)
        {
            if (panel == null)
            {
                return;
            }

            panel.BackColor = LabelingWorkbenchPalette.Frame;
            int gap = compact ? 5 : 6;
            int comboWidth = Math.Max(70, panel.Width - buttonWidth - gap);
            lblClassSelector.AutoSize = false;
            lblClassSelector.ForeColor = Color.FromArgb(198, 211, 237);
            lblClassSelector.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Regular);
            lblClassSelector.Text = "현재 클래스";
            lblClassSelector.SetBounds(0, compact ? 2 : 3, comboWidth, 16);
            cbClassMenu.SetBounds(0, compact ? 23 : 25, comboWidth, 29);
            btnClassSettings.SetBounds(comboWidth + gap, Math.Max(0, (panel.Height - buttonHeight) / 2), buttonWidth, buttonHeight);
            btnClassSettings.BringToFront();
        }

        private void LayoutOutputPathPanel(Panel panel, int buttonWidth, int buttonHeight, bool compact)
        {
            if (panel == null)
            {
                return;
            }

            panel.BackColor = LabelingWorkbenchPalette.Frame;
            panel.Padding = Padding.Empty;
            btnOutputPath.Dock = DockStyle.Left;
            btnOutputPath.Width = buttonWidth;
            btnOutputPath.Height = buttonHeight;
            btnOutputPath.IconSize = compact ? 20 : 23;
            btnOutputPath.Font = new Font("Segoe UI", compact ? 7F : 7.5F, FontStyle.Regular);
            btnOutputPath.BorderRadius = 4;
            lbExportPath.Dock = DockStyle.Fill;
            lbExportPath.BringToFront();
            btnOutputPath.BringToFront();
        }

        private void InitializeRuntimeToolbarButtons()
        {
            btnAcceptDetection = CreateRuntimeToolbarButton(
                "btnAcceptDetection",
                "확정",
                FontAwesome.Sharp.IconChar.Check,
                btnDetectCurrentImage.TabIndex + 1,
                enabled: false);
            btnAcceptDetection.Click += btnAcceptDetection_Click;
            pnlTitleBar.Controls.Add(btnAcceptDetection);
            btnAcceptDetection.BringToFront();

            btnYoloConnect = CreateRuntimeToolbarButton(
                "btnYoloConnect",
                "YOLO",
                FontAwesome.Sharp.IconChar.PowerOff,
                btnStartTraining.TabIndex + 1,
                enabled: true);
            btnYoloConnect.Click += btnYoloConnect_Click;
            pnlTitleBar.Controls.Add(btnYoloConnect);
            btnYoloConnect.BringToFront();

            btnYoloStop = CreateRuntimeToolbarButton(
                "btnYoloStop",
                "중지",
                FontAwesome.Sharp.IconChar.TimesCircle,
                btnStartTraining.TabIndex + 2,
                enabled: true);
            btnYoloStop.Click += btnYoloStop_Click;
            pnlTitleBar.Controls.Add(btnYoloStop);
            btnYoloStop.BringToFront();
        }

        private RJButton CreateRuntimeToolbarButton(
            string name,
            string text,
            FontAwesome.Sharp.IconChar icon,
            int tabIndex,
            bool enabled)
        {
            var button = new RJButton
            {
                BackColor = LabelingWorkbenchPalette.Selection,
                BorderColor = LabelingWorkbenchPalette.Accent,
                BorderRadius = 4,
                BorderSize = 1,
                ContextMenuStrip = dmUserOptions,
                Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.White,
                IconChar = icon,
                IconColor = Color.White,
                IconFont = FontAwesome.Sharp.IconFont.Auto,
                IconSize = 25,
                ImageAlign = ContentAlignment.TopCenter,
                Name = name,
                Size = new Size(63, 45),
                Style = RJCodeUI_M1.RJControls.ControlStyle.Solid,
                TabIndex = tabIndex,
                Text = text,
                TextAlign = ContentAlignment.BottomCenter,
                TextImageRelation = TextImageRelation.ImageAboveText,
                UseVisualStyleBackColor = false,
                Enabled = enabled
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.AccentHover;
            return button;
        }

        private void ConfigureStatusLabel(RJLabel label, int width, bool compact)
        {
            if (label == null)
            {
                return;
            }

            label.Width = width;
            label.Font = new Font("Segoe UI", compact ? 8f : 8.75f, FontStyle.Regular);
            label.Padding = new Padding(10, compact ? 6 : 7, 4, 0);
            label.AutoEllipsis = true;
        }

        private void ConfigureStatusChip(RJLabel label, int width, bool compact)
        {
            if (label == null)
            {
                return;
            }

            label.Width = width;
            label.Height = pnStatusBar.Height;
            label.Font = new Font("Segoe UI", compact ? 7.5F : 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label.Padding = new Padding(8, compact ? 6 : 7, 4, 0);
            label.AutoEllipsis = true;
        }

        private void ApplyMainTheme()
        {
            SettingsManager.LoadApperanceSettings();

            Color frameColor = LabelingWorkbenchPalette.Frame;
            Color statusColor = LabelingWorkbenchPalette.Status;
            Color workspaceColor = LabelingWorkbenchPalette.Workspace;
            Color buttonColor = LabelingWorkbenchPalette.SurfaceAlt;
            Color buttonDownColor = LabelingWorkbenchPalette.Surface;
            Color buttonHoverColor = LabelingWorkbenchPalette.PanelHeader;

            BackColor = frameColor;
            pnlTitleBar.BackColor = frameColor;
            pnStatusBar.BackColor = statusColor;
            mainWorkspacePanel.BackColor = workspaceColor;
            mainContentPanel.BackColor = workspaceColor;
            teachingHostPanel.BackColor = workspaceColor;
            EnsureMainFrameChrome();
            ApplyPanelTheme(pnlTitleBar, frameColor);
            ApplyButtonTheme(pnlTitleBar, buttonColor, buttonDownColor, buttonHoverColor);
            ApplyMainFrameTextTheme();
            LayoutStatusBarChrome();
        }

        private void EnsureMainFrameChrome()
        {
            mainCommandToolTip ??= new ToolTip(components)
            {
                AutomaticDelay = 250,
                AutoPopDelay = 8000,
                InitialDelay = 250,
                ReshowDelay = 100,
                ShowAlways = true
            };

            if (lblWorkbenchTitle == null)
            {
                lblWorkbenchTitle = new Label
                {
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 15.5F, FontStyle.Bold, GraphicsUnit.Point),
                    Text = "Labeling Studio",
                    TextAlign = ContentAlignment.MiddleLeft
                };
                pnlTitleBar.Controls.Add(lblWorkbenchTitle);
            }

            if (lblWorkbenchSubtitle == null)
            {
                lblWorkbenchSubtitle = new Label
                {
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    ForeColor = LabelingWorkbenchPalette.MutedText,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                    Text = "Dataset labeling / AI review",
                    TextAlign = ContentAlignment.MiddleLeft
                };
                pnlTitleBar.Controls.Add(lblWorkbenchSubtitle);
            }

            if (classSelectorPanel == null)
            {
                classSelectorPanel = new Panel
                {
                    BackColor = LabelingWorkbenchPalette.Frame,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                pnlTitleBar.Controls.Add(classSelectorPanel);
            }

            if (workspaceModePanel == null)
            {
                workspaceModePanel = new Panel
                {
                    BackColor = LabelingWorkbenchPalette.Frame,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                pnlTitleBar.Controls.Add(workspaceModePanel);
            }

            if (btnTeachingWorkspace == null)
            {
                btnTeachingWorkspace = CreateRuntimeToolbarButton(
                    "btnTeachingWorkspace",
                    "티칭",
                    FontAwesome.Sharp.IconChar.DrawPolygon,
                    100,
                    enabled: true);
                btnTeachingWorkspace.Click += btnTeachingWorkspace_Click;
            }

            if (btnDetectionReviewWorkspace == null)
            {
                btnDetectionReviewWorkspace = CreateRuntimeToolbarButton(
                    "btnDetectionReviewWorkspace",
                    "검수",
                    FontAwesome.Sharp.IconChar.Search,
                    101,
                    enabled: true);
                btnDetectionReviewWorkspace.Click += btnDetectionReviewWorkspace_Click;
            }

            if (btnTrainingWorkspace == null)
            {
                btnTrainingWorkspace = CreateRuntimeToolbarButton(
                    "btnTrainingWorkspace",
                    "학습",
                    FontAwesome.Sharp.IconChar.ChartLine,
                    102,
                    enabled: true);
                btnTrainingWorkspace.Click += btnTrainingWorkspace_Click;
            }

            if (workflowCommandPanel == null)
            {
                workflowCommandPanel = new Panel
                {
                    BackColor = LabelingWorkbenchPalette.Frame,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                pnlTitleBar.Controls.Add(workflowCommandPanel);
            }

            if (utilityCommandPanel == null)
            {
                utilityCommandPanel = new Panel
                {
                    BackColor = LabelingWorkbenchPalette.Frame,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                pnlTitleBar.Controls.Add(utilityCommandPanel);
            }

            if (lbTrainingStatus == null)
            {
                lbTrainingStatus = new RJLabel
                {
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Arrow,
                    Dock = DockStyle.Left,
                    Font = new Font("Segoe UI", 8.75F, FontStyle.Regular, GraphicsUnit.Point),
                    ForeColor = LabelingWorkbenchPalette.MutedText,
                    LinkLabel = false,
                    Name = "lbTrainingStatus",
                    Padding = new Padding(10, 7, 4, 0),
                    Size = new Size(320, pnStatusBar.Height),
                    Style = RJCodeUI_M1.RJControls.LabelStyle.Custom,
                    Text = "학습 대기"
                };
                pnStatusBar.Controls.Add(lbTrainingStatus);
            }

            lbYoloListenerStatus ??= CreateStatusChip("Listener -");
            lbYoloClientStatus ??= CreateStatusChip("Client -");
            lbYoloModelStatus ??= CreateStatusChip("Model -");
            EnsureStatusChipParent(lbYoloListenerStatus);
            EnsureStatusChipParent(lbYoloClientStatus);
            EnsureStatusChipParent(lbYoloModelStatus);

            ReparentToolbarControl(lblClassSelector, classSelectorPanel);
            ReparentToolbarControl(cbClassMenu, classSelectorPanel);
            ReparentToolbarControl(btnClassSettings, classSelectorPanel);
            ReparentToolbarControl(btnTeachingWorkspace, workspaceModePanel);
            ReparentToolbarControl(btnDetectionReviewWorkspace, workspaceModePanel);
            ReparentToolbarControl(btnTrainingWorkspace, workspaceModePanel);
            ReparentToolbarControl(btnDetectCurrentImage, workflowCommandPanel);
            ReparentToolbarControl(btnAcceptDetection, workflowCommandPanel);
            ReparentToolbarControl(btnSaveProject, workflowCommandPanel);
            ReparentToolbarControl(btnYoloConnect, utilityCommandPanel);
            ReparentToolbarControl(btnYoloStop, utilityCommandPanel);
            ReparentToolbarControl(btnStartTraining, utilityCommandPanel);
            ReparentToolbarControl(btnUserOptions, utilityCommandPanel);
            ReparentToolbarControl(btnScreenCapture, utilityCommandPanel);

            if (statusBarTopLine == null)
            {
                statusBarTopLine = new Panel
                {
                    BackColor = LabelingWorkbenchPalette.Accent,
                    Height = 1
                };
                pnStatusBar.Controls.Add(statusBarTopLine);
            }

            lblWorkbenchTitle.BringToFront();
            lblWorkbenchSubtitle.BringToFront();
            workspaceModePanel.BringToFront();
            classSelectorPanel.BringToFront();
            outputPathPanel.BringToFront();
            imageNavigatorPanel.BringToFront();
            workflowCommandPanel.BringToFront();
            utilityCommandPanel.BringToFront();
            statusBarTopLine.BringToFront();
        }

        private RJLabel CreateStatusChip(string text)
        {
            return new RJLabel
            {
                BackColor = Color.Transparent,
                Cursor = Cursors.Arrow,
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                LinkLabel = false,
                Padding = new Padding(8, 7, 4, 0),
                Size = new Size(118, pnStatusBar.Height),
                Style = RJCodeUI_M1.RJControls.LabelStyle.Custom,
                Text = text
            };
        }

        private void EnsureStatusChipParent(RJLabel label)
        {
            if (label == null)
            {
                return;
            }

            if (label.Parent != pnStatusBar)
            {
                label.Parent?.Controls.Remove(label);
                pnStatusBar.Controls.Add(label);
            }

            label.Dock = DockStyle.Left;
        }

        private static void ReparentToolbarControl(Control control, Control parent)
        {
            if (control == null || parent == null)
            {
                return;
            }

            if (control.Parent != parent)
            {
                control.Parent?.Controls.Remove(control);
                parent.Controls.Add(control);
            }

            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.Margin = Padding.Empty;
        }

        private void ApplyMainFrameTextTheme()
        {
            Text = "OpenVisionLab Labeling Studio";
            btnClassSettings.Text = "클래스";
            btnSaveProject.Text = "저장";
            btnOutputPath.Text = "데이터";
            btnStartTraining.Text = "학습";
            btnYoloConnect.Text = "YOLO";
            btnYoloStop.Text = "중지";
            btnDetectCurrentImage.Text = "AI 검출";
            btnScreenCapture.Text = "캡처";
            btnUserOptions.Text = "모델";
            if (btnTeachingWorkspace != null)
            {
                btnTeachingWorkspace.Text = "티칭";
            }

            if (btnDetectionReviewWorkspace != null)
            {
                btnDetectionReviewWorkspace.Text = "검수";
            }

            if (btnTrainingWorkspace != null)
            {
                btnTrainingWorkspace.Text = "학습";
            }

            lbVersion.ForeColor = LabelingWorkbenchPalette.Text;
            lbDatasetStatus.ForeColor = LabelingWorkbenchPalette.MutedText;
            lbPythonStatus.ForeColor = LabelingWorkbenchPalette.MutedText;
            if (lbTrainingStatus != null)
            {
                lbTrainingStatus.ForeColor = LabelingWorkbenchPalette.MutedText;
            }
            lbExportPath.ForeColor = LabelingWorkbenchPalette.Text;
            lbSelectImageName.ForeColor = LabelingWorkbenchPalette.Text;

            cbClassMenu.BackColor = Color.FromArgb(248, 251, 254);
            cbClassMenu.BorderColor = LabelingWorkbenchPalette.Accent;
            cbClassMenu.BorderRadius = 4;
            cbClassMenu.DropDownBackColor = Color.FromArgb(248, 251, 254);
            cbClassMenu.DropDownTextColor = Color.FromArgb(20, 38, 58);
            cbClassMenu.IconColor = LabelingWorkbenchPalette.Accent;
            cbClassMenu.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            ApplyCommandButtonTheme(btnDetectCurrentImage, LabelingWorkbenchPalette.Selection, LabelingWorkbenchPalette.AccentHover);
            ApplyCommandButtonTheme(btnAcceptDetection, LabelingWorkbenchPalette.Selection, LabelingWorkbenchPalette.AccentHover);
            ApplyCommandButtonTheme(btnSaveProject, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            ApplyCommandButtonTheme(btnClassSettings, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            ApplyCommandButtonTheme(btnOutputPath, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            ApplyCommandButtonTheme(btnYoloConnect, Color.FromArgb(37, 128, 116), LabelingWorkbenchPalette.AccentHover);
            ApplyCommandButtonTheme(btnYoloStop, Color.FromArgb(114, 75, 82), Color.FromArgb(146, 83, 91));
            ApplyCommandButtonTheme(btnStartTraining, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            ApplyCommandButtonTheme(btnUserOptions, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            ApplyCommandButtonTheme(btnScreenCapture, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            UpdateWorkspaceModeChrome();

            if (btnAcceptDetection != null)
            {
                btnAcceptDetection.Text = "후보 확정";
            }

            mainCommandToolTip?.SetToolTip(btnDetectCurrentImage, "현재 이미지에서 AI 검출을 실행합니다.");
            mainCommandToolTip?.SetToolTip(btnAcceptDetection, "확정 가능한 AI 후보를 현재 라벨로 저장합니다.");
            mainCommandToolTip?.SetToolTip(btnSaveProject, "프로젝트 설정과 라벨 데이터를 저장합니다.");
            mainCommandToolTip?.SetToolTip(btnClassSettings, "라벨링 클래스 목록을 관리합니다.");
            mainCommandToolTip?.SetToolTip(btnOutputPath, "YOLO 데이터셋 출력 경로를 선택합니다.");
            mainCommandToolTip?.SetToolTip(btnYoloConnect, "YOLO TCP 클라이언트를 실행하거나 재접속합니다.");
            mainCommandToolTip?.SetToolTip(btnYoloStop, "앱에서 시작한 YOLO 프로세스와 TCP listener를 중지합니다.");
            mainCommandToolTip?.SetToolTip(btnStartTraining, "클래스와 학습 데이터셋이 준비된 뒤 학습 워크플로를 시작합니다.");
            mainCommandToolTip?.SetToolTip(btnUserOptions, "YOLO/Python 모델 설정을 엽니다.");
            mainCommandToolTip?.SetToolTip(btnScreenCapture, "현재 화면을 캡처합니다.");
            mainCommandToolTip?.SetToolTip(btnTeachingWorkspace, "직접 ROI와 세그먼트를 작성하는 티칭 공간으로 전환합니다.");
            mainCommandToolTip?.SetToolTip(btnDetectionReviewWorkspace, "AI 검출 후보를 확인하고 라벨로 확정하는 검수 공간으로 전환합니다.");
            mainCommandToolTip?.SetToolTip(btnTrainingWorkspace, "데이터셋 준비 상태와 학습 로그를 확인하는 학습 공간으로 전환합니다.");
        }

        private static void ApplyCommandButtonTheme(RJButton button, Color backColor, Color hoverColor)
        {
            if (button == null)
            {
                return;
            }

            button.BackColor = backColor;
            button.BorderColor = backColor;
            button.FlatAppearance.BorderColor = backColor;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.ForeColor = Color.White;
            button.IconColor = Color.White;
        }

        private void LayoutStatusBarChrome()
        {
            if (statusBarTopLine != null)
            {
                statusBarTopLine.SetBounds(0, 0, pnStatusBar.ClientSize.Width, 1);
            }
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
            if (isClosing || IsDisposed)
            {
                return;
            }

            lbSelectImageName.Text = Global.Data.LastSelectImageName;
            statusRefreshTick++;
            RefreshStatusBar(includeDataset: statusRefreshTick % 5 == 0);
        }

        private void RefreshStatusBar(bool includeDataset)
        {
            if (isClosing || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(() => RefreshStatusBar(includeDataset));
                return;
            }

            if (includeDataset)
            {
                UpdateDatasetStatus();
            }

            UpdatePythonStatus();
        }

        private void DetectionResults_DetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            if (isClosing || IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(() => RefreshStatusBar(includeDataset: false));
                return;
            }

            RefreshStatusBar(includeDataset: false);
        }

        private void UpdateDatasetStatus()
        {
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(Global.Data, refreshYaml: false);
            if (report.IsReady)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                lbDatasetStatus.ForeColor = Color.FromArgb(190, 232, 212);
                lbDatasetStatus.Text = $"데이터셋 준비 완료  학습:{statistics.TrainImageCount}  검증:{statistics.ValidImageCount}  객체:{statistics.TotalObjectCount}";
                return;
            }

            lbDatasetStatus.ForeColor = Color.FromArgb(244, 199, 134);
            lbDatasetStatus.Text = $"데이터셋 확인 필요  {TrimStatus(report.Errors.FirstOrDefault() ?? "not ready", 58)}";
        }

        private void UpdatePythonStatus()
        {
            PythonCommunicationStatus status = Global.GetPythonCommunicationStatusSnapshot();
            YoloStatusView yoloView = BuildYoloStatusView(status);
            lbPythonStatus.ForeColor = yoloView.StatusColor;
            lbPythonStatus.Text = yoloView.StatusText;
            mainCommandToolTip?.SetToolTip(lbPythonStatus, yoloView.ToolTip);
            UpdateYoloStatusChips(status);
            UpdateTrainingStatus(status);

            if (!yoloOperationInProgress)
            {
                ApplyYoloConnectionStateToButtons(yoloView);
            }

            if (btnAcceptDetection != null)
            {
                Global.Data.ProjectSettings?.EnsureDefaults();
                float minimumConfidence = Global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F;
                btnAcceptDetection.Enabled = Global.DetectionResults.CanCommitLastDetection(Global.Data, minimumConfidence);
            }
        }

        private YoloStatusView BuildYoloStatusView(PythonCommunicationStatus status)
        {
            status ??= new PythonCommunicationStatus();

            bool processRunning = Global.PythonClientProcess.IsRunning;
            int? processId = Global.PythonClientProcess.ProcessId;
            string endpoint = !string.IsNullOrWhiteSpace(status.ListenerEndpoint)
                ? status.ListenerEndpoint
                : status.ListenerPort > 0
                    ? $"127.0.0.1:{status.ListenerPort}"
                    : "127.0.0.1:5000";
            string error = FirstNonEmpty(status.LastError, Global.PythonClientProcess.LastError);
            string processText = processRunning
                ? processId.HasValue ? $"내장 PID {processId.Value}" : "내장 실행"
                : "내장 중지";
            string clientText = status.IsClientConnected
                ? processRunning ? processText : "외부 클라이언트"
                : "Python 미연결";
            string activityText = BuildYoloActivityText(status);
            bool stopEnabled = status.IsListening || status.IsClientConnected || processRunning;

            if (!string.IsNullOrWhiteSpace(error))
            {
                string text = $"YOLO 오류 | {TrimStatus(error, 44)}";
                return CreateYoloStatusView(
                    text,
                    "오류",
                    Color.FromArgb(255, 196, 176),
                    Color.FromArgb(154, 74, 54),
                    Color.FromArgb(179, 84, 60),
                    stopEnabled,
                    BuildYoloToolTip("오류", endpoint, status, processText, activityText, error));
            }

            if (status.IsClientConnected)
            {
                string text = $"YOLO 연결됨 | {clientText}{activityText}";
                return CreateYoloStatusView(
                    TrimStatus(text, 78),
                    "연결됨",
                    Color.FromArgb(190, 232, 212),
                    Color.FromArgb(37, 128, 116),
                    Color.FromArgb(0, 165, 151),
                    stopEnabled,
                    BuildYoloToolTip("연결됨", endpoint, status, processText, activityText, ""));
            }

            if (processRunning)
            {
                string text = $"YOLO 실행중 | {processText} | 연결 대기";
                return CreateYoloStatusView(
                    text,
                    "실행중",
                    Color.FromArgb(224, 218, 157),
                    Color.FromArgb(86, 101, 52),
                    Color.FromArgb(112, 127, 68),
                    stopEnabled,
                    BuildYoloToolTip("Python 실행중 / TCP 연결 대기", endpoint, status, processText, activityText, ""));
            }

            if (status.IsListening)
            {
                string text = $"YOLO 대기 | listener {endpoint} | Python 미연결";
                return CreateYoloStatusView(
                    TrimStatus(text, 78),
                    "대기",
                    Color.FromArgb(244, 199, 134),
                    Color.FromArgb(105, 89, 43),
                    Color.FromArgb(128, 108, 50),
                    stopEnabled,
                    BuildYoloToolTip("Listener 대기 / Python 미연결", endpoint, status, processText, activityText, ""));
            }

            return CreateYoloStatusView(
                "YOLO 미시작 | 시작 버튼 대기",
                "시작",
                LabelingWorkbenchPalette.MutedText,
                Color.FromArgb(37, 128, 116),
                Color.FromArgb(0, 165, 151),
                stopEnabled,
                BuildYoloToolTip("미시작", endpoint, status, processText, activityText, ""));
        }

        private void UpdateYoloStatusChips(PythonCommunicationStatus status)
        {
            status ??= new PythonCommunicationStatus();
            string endpoint = !string.IsNullOrWhiteSpace(status.ListenerEndpoint)
                ? status.ListenerEndpoint
                : status.ListenerPort > 0
                    ? $"127.0.0.1:{status.ListenerPort}"
                    : "127.0.0.1:5000";

            SetStatusChip(
                lbYoloListenerStatus,
                status.IsListening ? "수신 ON" : "수신 OFF",
                status.IsListening ? Color.FromArgb(190, 232, 212) : Color.FromArgb(244, 199, 134));
            mainCommandToolTip?.SetToolTip(
                lbYoloListenerStatus,
                status.IsListening ? $"TCP listener가 열려 있습니다. {endpoint}" : "TCP listener가 열려 있지 않습니다.");

            SetStatusChip(
                lbYoloClientStatus,
                status.IsClientConnected ? "Python 연결" : "Python 미연결",
                status.IsClientConnected ? Color.FromArgb(190, 232, 212) : Color.FromArgb(244, 199, 134));
            mainCommandToolTip?.SetToolTip(
                lbYoloClientStatus,
                status.IsClientConnected ? "Python YOLO TCP 클라이언트가 라벨링 앱에 연결되어 있습니다." : "Python YOLO TCP 클라이언트 연결이 없습니다.");

            string modelText;
            Color modelColor;
            if (!string.IsNullOrWhiteSpace(status.LastError) || !string.IsNullOrWhiteSpace(Global.PythonClientProcess.LastError))
            {
                modelText = "모델 오류";
                modelColor = Color.FromArgb(255, 196, 176);
            }
            else if (status.LastModelStatusAtUtc.HasValue)
            {
                modelText = status.LastModelLoaded ? "모델 로드" : "모델 미로드";
                modelColor = status.LastModelLoaded
                    ? Color.FromArgb(190, 232, 212)
                    : Color.FromArgb(224, 218, 157);
            }
            else if (status.LastHealthCheckAtUtc.HasValue && !string.IsNullOrWhiteSpace(status.LastWorkerState))
            {
                modelText = "Worker 응답";
                modelColor = Color.FromArgb(224, 218, 157);
            }
            else if (status.LastDetectionResultAtUtc.HasValue || status.LastDetectionStatusAtUtc.HasValue)
            {
                modelText = "모델 응답";
                modelColor = Color.FromArgb(190, 232, 212);
            }
            else if (Global.PythonClientProcess.IsRunning)
            {
                modelText = "모델 로딩";
                modelColor = Color.FromArgb(224, 218, 157);
            }
            else
            {
                modelText = "모델 대기";
                modelColor = LabelingWorkbenchPalette.MutedText;
            }

            SetStatusChip(lbYoloModelStatus, modelText, modelColor);
            mainCommandToolTip?.SetToolTip(lbYoloModelStatus, BuildYoloModelChipToolTip(status, modelText));
        }

        private string BuildYoloModelChipToolTip(PythonCommunicationStatus status, string modelText)
        {
            status ??= new PythonCommunicationStatus();
            string lastStatus = status?.LastDetectionMessage;
            string lastError = FirstNonEmpty(status?.LastError, Global.PythonClientProcess.LastError);
            if (!string.IsNullOrWhiteSpace(lastError))
            {
                return $"{modelText}: {TrimStatus(lastError, 120)}";
            }

            if (status.LastModelStatusAtUtc.HasValue)
            {
                var lines = new List<string>
                {
                    $"{modelText}: {(status.LastModelLoaded ? "로드됨" : "미로드")}",
                    $"모델 상태: {FirstNonEmpty(status.LastModelState, "unknown")}",
                    $"최근 모델 확인: {FormatLocalClock(status.LastModelStatusAtUtc)}"
                };

                if (!string.IsNullOrWhiteSpace(status.LastModelMessage))
                {
                    lines.Add($"메시지: {TrimStatus(status.LastModelMessage, 120)}");
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (status.LastHealthCheckAtUtc.HasValue)
            {
                var lines = new List<string>
                {
                    $"{modelText}: HealthCheck 응답",
                    $"Worker 상태: {FirstNonEmpty(status.LastWorkerState, "unknown")}",
                    $"최근 진단: {FormatLocalClock(status.LastHealthCheckAtUtc)}"
                };

                if (!string.IsNullOrWhiteSpace(status.LastWorkerMessage))
                {
                    lines.Add($"메시지: {TrimStatus(status.LastWorkerMessage, 120)}");
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (!string.IsNullOrWhiteSpace(lastStatus))
            {
                return $"{modelText}: {TrimStatus(lastStatus, 120)}";
            }

            return modelText;
        }

        private static void SetStatusChip(RJLabel label, string text, Color color)
        {
            if (label == null)
            {
                return;
            }

            label.Text = text;
            label.ForeColor = color;
        }

        private static YoloStatusView CreateYoloStatusView(
            string statusText,
            string buttonText,
            Color statusColor,
            Color buttonColor,
            Color buttonHoverColor,
            bool stopEnabled,
            string toolTip)
        {
            return new YoloStatusView
            {
                StatusText = statusText,
                ButtonText = buttonText,
                StatusColor = statusColor,
                ButtonColor = buttonColor,
                ButtonHoverColor = buttonHoverColor,
                StopEnabled = stopEnabled,
                ToolTip = toolTip
            };
        }

        private string BuildYoloToolTip(
            string state,
            string endpoint,
            PythonCommunicationStatus status,
            string processText,
            string activityText,
            string error)
        {
            var lines = new List<string>
            {
                $"상태: {state}",
                $"Listener: {(status.IsListening ? "열림" : "중지")} ({endpoint})",
                $"Client: {(status.IsClientConnected ? "연결됨" : "미연결")}",
                $"Process: {processText}"
            };

            if (status.LastConnectedAtUtc.HasValue)
            {
                lines.Add($"최근 연결: {FormatLocalClock(status.LastConnectedAtUtc)}");
            }

            if (status.LastDisconnectedAtUtc.HasValue)
            {
                lines.Add($"최근 끊김: {FormatLocalClock(status.LastDisconnectedAtUtc)}");
            }

            if (status.LastReceivedAtUtc.HasValue)
            {
                lines.Add($"최근 수신: {FormatLocalClock(status.LastReceivedAtUtc)}");
            }

            if (!string.IsNullOrWhiteSpace(activityText))
            {
                lines.Add($"작업: {activityText.Trim().TrimStart('|').Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                lines.Add($"오류: {error.Trim()}");
            }

            lines.Add("YOLO 버튼: 실행 또는 재접속");
            lines.Add("중지 버튼: listener와 앱에서 시작한 Python 프로세스 중지");
            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildYoloActivityText(PythonCommunicationStatus status)
        {
            bool hasNewerDetectionStatus = status.LastDetectionStatusAtUtc.HasValue
                && (!status.LastDetectionResultAtUtc.HasValue || status.LastDetectionStatusAtUtc.Value > status.LastDetectionResultAtUtc.Value);

            if (hasNewerDetectionStatus && !string.IsNullOrWhiteSpace(status.LastDetectionState))
            {
                string progress = status.LastDetectionProgressPercent.HasValue
                    ? $" {status.LastDetectionProgressPercent.Value}%"
                    : "";
                return $" | 검출 {status.LastDetectionState}{progress}";
            }

            if (status.LastDetectionResultAtUtc.HasValue)
            {
                string result = status.LastDetectionCount > 0
                    ? $"후보 {status.LastDetectionCount}"
                    : "후보 없음";
                return $" | 검출 {result} {FormatLocalClock(status.LastDetectionResultAtUtc)}";
            }

            if (status.LastModelStatusAtUtc.HasValue)
            {
                string loaded = status.LastModelLoaded ? "로드됨" : "미로드";
                string state = string.IsNullOrWhiteSpace(status.LastModelState) ? "상태 확인" : status.LastModelState;
                return $" | 모델 {state} {loaded}";
            }

            if (status.LastHealthCheckAtUtc.HasValue && !string.IsNullOrWhiteSpace(status.LastWorkerState))
            {
                return $" | Worker {status.LastWorkerState}";
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingState))
            {
                string progress = status.LastTrainingProgressPercent.HasValue
                    ? $" {status.LastTrainingProgressPercent.Value}%"
                    : "";
                return $" | 학습 {status.LastTrainingState}{progress}";
            }

            return "";
        }

        private void UpdateTrainingStatus(PythonCommunicationStatus status)
        {
            status ??= new PythonCommunicationStatus();
            DateTime nowUtc = DateTime.UtcNow;
            if (IsTrainingTerminalState(status.LastTrainingState))
            {
                trainingStopRequestedAtUtc = null;
            }

            bool stopPending = IsTrainingStopPending(trainingStopRequestedAtUtc, nowUtc);
            if (!stopPending && trainingStopRequestedAtUtc.HasValue)
            {
                trainingStopRequestedAtUtc = null;
            }

            if (lbTrainingStatus != null)
            {
                lbTrainingStatus.Text = BuildTrainingStatusText(status, trainingStopRequestedAtUtc, nowUtc);
                lbTrainingStatus.ForeColor = GetTrainingStatusColor(status, stopPending);
                mainCommandToolTip?.SetToolTip(lbTrainingStatus, BuildTrainingToolTip(status, stopPending));
            }

            if (!yoloOperationInProgress)
            {
                ApplyTrainingStateToButton(status, stopPending);
            }
        }

        private void ApplyTrainingStateToButton(PythonCommunicationStatus status, bool stopPending)
        {
            if (btnStartTraining == null)
            {
                return;
            }

            bool isActive = IsTrainingActive(status);
            if (stopPending)
            {
                btnStartTraining.Enabled = false;
                btnStartTraining.Text = "중지중";
                btnStartTraining.IconChar = FontAwesome.Sharp.IconChar.TimesCircle;
                ApplyCommandButtonTheme(btnStartTraining, Color.FromArgb(114, 75, 82), Color.FromArgb(146, 83, 91));
                mainCommandToolTip?.SetToolTip(btnStartTraining, "Python 학습 중지를 요청했습니다.");
                return;
            }

            btnStartTraining.Enabled = true;
            if (isActive)
            {
                btnStartTraining.Text = "중지";
                btnStartTraining.IconChar = FontAwesome.Sharp.IconChar.TimesCircle;
                ApplyCommandButtonTheme(btnStartTraining, Color.FromArgb(114, 75, 82), Color.FromArgb(146, 83, 91));
                mainCommandToolTip?.SetToolTip(btnStartTraining, "진행 중인 Python 학습을 중지합니다.");
                return;
            }

            btnStartTraining.Text = "학습";
            btnStartTraining.IconChar = FontAwesome.Sharp.IconChar.Clock;
            ApplyCommandButtonTheme(btnStartTraining, LabelingWorkbenchPalette.SurfaceAlt, LabelingWorkbenchPalette.PanelHeader);
            mainCommandToolTip?.SetToolTip(btnStartTraining, "클래스와 학습 데이터셋이 준비된 뒤 학습 워크플로를 시작합니다.");
        }

        private static bool IsTrainingActive(PythonCommunicationStatus status)
        {
            if (status == null || string.IsNullOrWhiteSpace(status.LastTrainingState))
            {
                return false;
            }

            string state = status.LastTrainingState.Trim();
            if (IsTrainingTerminalState(state))
            {
                return false;
            }

            if (status.LastTrainingProgressPercent.HasValue && status.LastTrainingProgressPercent.Value >= 100)
            {
                return false;
            }

            return string.Equals(state, "started", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "running", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "training", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "epoch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "queued", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "stopping", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrainingTerminalState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return false;
            }

            return string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "complete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "done", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "finished", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "canceled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrainingStopPending(DateTime? requestedAtUtc, DateTime nowUtc)
        {
            return requestedAtUtc.HasValue
                && nowUtc >= requestedAtUtc.Value
                && nowUtc - requestedAtUtc.Value < TimeSpan.FromSeconds(TrainingStopPendingSeconds);
        }

        private static string BuildTrainingStatusText(PythonCommunicationStatus status, DateTime? stopRequestedUtc, DateTime nowUtc)
        {
            status ??= new PythonCommunicationStatus();
            if (IsTrainingStopPending(stopRequestedUtc, nowUtc))
            {
                return "학습 중지 요청됨";
            }

            if (string.IsNullOrWhiteSpace(status.LastTrainingState))
            {
                return "학습 대기";
            }

            string progress = status.LastTrainingProgressPercent.HasValue
                ? $" {status.LastTrainingProgressPercent.Value}%"
                : "";
            string epoch = status.LastTrainingEpoch.HasValue && status.LastTrainingTotalEpochs.HasValue
                ? $" epoch {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}"
                : "";
            string message = !string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                ? $" | {TrimStatus(status.LastTrainingMessage, 42)}"
                : "";

            return TrimStatus($"학습 {status.LastTrainingState}{progress}{epoch}{message}", 86);
        }

        private static Color GetTrainingStatusColor(PythonCommunicationStatus status, bool stopPending)
        {
            if (stopPending)
            {
                return Color.FromArgb(244, 199, 134);
            }

            if (status == null || string.IsNullOrWhiteSpace(status.LastTrainingState))
            {
                return LabelingWorkbenchPalette.MutedText;
            }

            if (IsTrainingTerminalState(status.LastTrainingState))
            {
                return string.Equals(status.LastTrainingState, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status.LastTrainingState, "error", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(255, 196, 176)
                    : Color.FromArgb(190, 232, 212);
            }

            return Color.FromArgb(224, 218, 157);
        }

        private static string BuildTrainingToolTip(PythonCommunicationStatus status, bool stopPending)
        {
            status ??= new PythonCommunicationStatus();
            var lines = new List<string>
            {
                stopPending ? "상태: 중지 요청됨" : $"상태: {FirstNonEmpty(status.LastTrainingState, "대기")}"
            };

            if (status.LastTrainingProgressPercent.HasValue)
            {
                lines.Add($"진행률: {status.LastTrainingProgressPercent.Value}%");
            }

            if (status.LastTrainingEpoch.HasValue && status.LastTrainingTotalEpochs.HasValue)
            {
                lines.Add($"Epoch: {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}");
            }

            if (status.LastTrainingStatusAtUtc.HasValue)
            {
                lines.Add($"최근 상태: {FormatLocalClock(status.LastTrainingStatusAtUtc)}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingMessage))
            {
                lines.Add($"메시지: {status.LastTrainingMessage}");
            }

            lines.Add("학습 버튼: 시작 또는 진행 중 학습 중지");
            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatLocalClock(DateTime? utc)
        {
            return utc.HasValue ? utc.Value.ToLocalTime().ToString("HH:mm:ss") : "";
        }

        private void ApplyYoloConnectionStateToButtons(PythonCommunicationStatus status)
        {
            ApplyYoloConnectionStateToButtons(BuildYoloStatusView(status));
        }

        private void ApplyYoloConnectionStateToButtons(YoloStatusView yoloView)
        {
            if (btnYoloConnect == null)
            {
                return;
            }

            SetYoloStatusButton(yoloView.ButtonText, yoloView.ButtonColor, yoloView.ButtonHoverColor);
            mainCommandToolTip?.SetToolTip(btnYoloConnect, yoloView.ToolTip);

            if (btnYoloStop != null)
            {
                btnYoloStop.Enabled = yoloView.StopEnabled;
                mainCommandToolTip?.SetToolTip(btnYoloStop, yoloView.StopEnabled
                    ? "YOLO listener와 앱에서 시작한 Python 프로세스를 중지합니다."
                    : "중지할 YOLO listener 또는 Python 프로세스가 없습니다.");
            }
        }

        private void SetYoloStatusButton(string text, Color backColor, Color hoverColor)
        {
            if (btnYoloConnect == null)
            {
                return;
            }

            btnYoloConnect.Text = text;
            btnYoloConnect.BackColor = backColor;
            btnYoloConnect.BorderColor = backColor;
            btnYoloConnect.FlatAppearance.BorderColor = backColor;
            btnYoloConnect.FlatAppearance.MouseOverBackColor = hoverColor;
            btnYoloConnect.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            btnYoloConnect.ForeColor = Color.White;
            btnYoloConnect.IconColor = Color.White;
        }

        private static string TrimStatus(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string trimmed = value.Trim().Replace(Environment.NewLine, " ");
            return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength - 1) + "...";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return "";
        }

        private async void FormMainFrame_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (shutdownCleanupCompleted)
            {
                return;
            }

            e.Cancel = true;
            if (shutdownCleanupStarted)
            {
                return;
            }

            shutdownCleanupStarted = true;
            isClosing = true;
            try
            {
                timerConnection?.Stop();
                timerAlarm?.Stop();
                Global.System.OnDataUpdated -= System_OnDataUpdated;
                Global.DetectionResults.DetectionCandidatesUpdated -= DetectionResults_DetectionCandidatesUpdated;
                SetShutdownUiState();
                Task<bool> closeTask = Global.CloseAsync();
                Task completedTask = await Task.WhenAny(closeTask, Task.Delay(3000));
                if (completedTask != closeTask)
                {
                    AppLog.Warn("Labeling app shutdown cleanup exceeded 3000ms. Continuing UI close; background cleanup may still finish.");
                }
                else if (!closeTask.Result)
                {
                    AppLog.ABNORMAL("Labeling app shutdown cleanup returned failure.");
                }
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Labeling app shutdown failed: {ex.Message}");
            }
            finally
            {
                shutdownCleanupCompleted = true;
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((System.Windows.Forms.MethodInvoker)Close);
                    }
                    else
                    {
                        Close();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                    Close();
                }
            }
        }

        private void SetShutdownUiState()
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(SetShutdownUiState);
                return;
            }

            yoloOperationInProgress = true;
            UseWaitCursor = true;
            Cursor = Cursors.AppStarting;

            if (btnYoloConnect != null)
            {
                btnYoloConnect.Enabled = false;
            }

            if (btnYoloStop != null)
            {
                btnYoloStop.Enabled = false;
            }

            if (btnDetectCurrentImage != null)
            {
                btnDetectCurrentImage.Enabled = false;
            }

            if (btnStartTraining != null)
            {
                btnStartTraining.Enabled = false;
            }

            if (lbPythonStatus != null)
            {
                lbPythonStatus.ForeColor = Color.FromArgb(224, 218, 157);
                lbPythonStatus.Text = "종료 정리중";
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void miSettings_Click(object sender, EventArgs e) { }
        private void btnUserOptions_Click(object sender, EventArgs e) { }
        private void btnScreenCapture_Click(object sender, EventArgs e)
        {
            try
            {
                using Bitmap capture = ScreenCaptureService.CapturePrimaryScreen();
                string strSavePath = ScreenCaptureService.CreateCaptureFilePath(Application.StartupPath, Text, DateTime.Now);
                capture.Save(strSavePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                AppLog.NORMAL($"Screen capture saved: {strSavePath}");
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
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
                case "캡처 폴더 열기":
                    string captureDirectory = ScreenCaptureService.GetCaptureDirectory(Application.StartupPath);
                    Directory.CreateDirectory(captureDirectory);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = captureDirectory,
                        UseShellExecute = true
                    });
                    break;
                default:
                    break;
            }
        }

        private void btnClassSettings_Click(object sender, EventArgs e)
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
            RefreshClassSelector(GetSelectedClassName());
            RefreshStatusBar(includeDataset: true);
        }

        private void System_OnDataUpdated()
        {
            if (isClosing || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(() => RefreshClassSelector(GetSelectedClassName()));
                return;
            }

            RefreshClassSelector(GetSelectedClassName());
        }

        private void RefreshClassSelector(string preferredClassName = null)
        {
            if (cbClassMenu == null)
            {
                return;
            }

            string selectedClassName = !string.IsNullOrWhiteSpace(preferredClassName)
                ? preferredClassName
                : GetSelectedClassName();
            string[] classNames = Global.Data.ClassNamedList.Select(x => x.Text).ToArray();

            refreshingClassSelector = true;
            try
            {
                cbClassMenu.Items.Clear();
                cbClassMenu.Items.AddRange(classNames);

                int selectedIndex = -1;
                if (!string.IsNullOrWhiteSpace(selectedClassName))
                {
                    for (int i = 0; i < cbClassMenu.Items.Count; i++)
                    {
                        if (string.Equals(cbClassMenu.Items[i]?.ToString(), selectedClassName, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }

                cbClassMenu.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (cbClassMenu.Items.Count > 0 ? 0 : -1);
            }
            finally
            {
                refreshingClassSelector = false;
            }

            ApplySelectedClassFromCombo();
        }

        private string GetSelectedClassName()
        {
            return cbClassMenu?.SelectedItem?.ToString() ?? cbClassMenu?.Texts ?? string.Empty;
        }

        private void ApplySelectedClassFromCombo()
        {
            string selectedClassName = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(selectedClassName))
            {
                return;
            }

            CClassItem classItem = Global.Data.ClassNamedList
                .FirstOrDefault(item => string.Equals(item.Text, selectedClassName, StringComparison.OrdinalIgnoreCase));
            if (classItem == null)
            {
                return;
            }

            Global.LabelingWorkflow.ApplySelectedClass(classItem);
        }

        private void cbClassMenu_OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (refreshingClassSelector)
            {
                return;
            }

            ApplySelectedClassFromCombo();
        }

        private void btnSaveProject_Click(object sender, EventArgs e)
        {
            Global.Data.SaveConfig(Global.Recipe.Name);
            LogDatasetDiagnostics(refreshYaml: false);
            RefreshStatusBar(includeDataset: true);
        }

        private async void btnStartTraining_Click(object sender, EventArgs e)
        {
            try
            {
                PythonCommunicationStatus currentStatus = Global.GetPythonCommunicationStatusSnapshot();
                if (IsTrainingActive(currentStatus) || IsTrainingStopPending(trainingStopRequestedAtUtc, DateTime.UtcNow))
                {
                    RequestStopTraining();
                    return;
                }

                if (!ValidatePythonModelSettings(requireWeights: false))
                {
                    RefreshStatusBar(includeDataset: true);
                    return;
                }

                CCommunicationLearning communication = Global.DeepLearning;
                if (!await EnsurePythonClientReadyForUiAsync(5000, "YOLO training"))
                {
                    RefreshStatusBar(includeDataset: true);
                    return;
                }

                if (Global.TrainingWorkflow.TryStartTraining(Global.Data, communication))
                {
                    trainingStopRequestedAtUtc = null;
                }

                RefreshStatusBar(includeDataset: true);
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLO training request failed: {ex.Message}");
                RefreshStatusBar(includeDataset: true);
            }
        }

        private void RequestStopTraining()
        {
            try
            {
                CCommunicationLearning communication = Global.DeepLearning;
                PythonCommunicationStatus status = Global.GetPythonCommunicationStatusSnapshot();
                if (!status.IsClientConnected)
                {
                    AppLog.ABNORMAL("YOLO training stop skipped because Python client is not connected.");
                    RefreshStatusBar(includeDataset: false);
                    return;
                }

                if (Global.TrainingWorkflow.TryStopTraining(communication))
                {
                    trainingStopRequestedAtUtc = DateTime.UtcNow;
                    AppLog.COMM("YOLO training stop requested by operator.");
                }

                RefreshStatusBar(includeDataset: false);
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLO training stop request failed: {ex.Message}");
                RefreshStatusBar(includeDataset: false);
            }
        }

        private void btnPythonSettings_Click(object sender, EventArgs e)
        {
            RJCodeUI_M1.RJForms.FormVision_Yolov5ParamSetting formVision_Yolov5ParamSetting = new FormVision_Yolov5ParamSetting();
            formVision_Yolov5ParamSetting.TopLevel = true;
            formVision_Yolov5ParamSetting.TopMost = true;
            formVision_Yolov5ParamSetting.StartPosition = FormStartPosition.CenterParent;
            if (!CUtil.OpenCheckForm(formVision_Yolov5ParamSetting)) return;
            formVision_Yolov5ParamSetting.Show();
        }

        private async void btnDetectCurrentImage_Click(object sender, EventArgs e)
        {
            try
            {
                if (!await EnsurePythonClientReadyForUiAsync(5000, "YOLO detection"))
                {
                    RefreshStatusBar(includeDataset: false);
                    return;
                }

                Global.DetectionWorkflow.TryStartCurrentImageDetection(
                    Global.Data,
                    Global.DeepLearning,
                    Global.DetectionResults,
                    () => true);
                RefreshStatusBar(includeDataset: false);
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLO detection request failed: {ex.Message}");
                RefreshStatusBar(includeDataset: false);
            }
        }

        private void btnAcceptDetection_Click(object sender, EventArgs e)
        {
            Global.Data.ProjectSettings?.EnsureDefaults();
            float minimumConfidence = Global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F;
            Global.DetectionResults.CommitLastDetectionToMainLabels(Global.Data, Global.System, minimumConfidence, createSegmentationFromBoxes: true);
            RefreshStatusBar(includeDataset: true);
        }

        private void FormMainFrame_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                btnAcceptDetection_Click(sender, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private bool ValidatePythonModelSettings(bool requireWeights)
        {
            Global.Data.ProjectSettings ??= new LabelingProjectSettings();
            Global.Data.ProjectSettings.EnsureDefaults();

            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(
                Global.Data.ProjectSettings.PythonModel,
                requireWeights);

            foreach (string warning in validation.Warnings)
            {
                AppLog.COMM(warning);
            }

            foreach (string error in validation.Errors)
            {
                AppLog.ABNORMAL(error);
            }

            return validation.IsValid;
        }

        private void btnOutputPath_Click(object sender, EventArgs e)
        {
            if (LoadFolderPath(out string folderPath))
            {
                Global.Data.ConfigureOutputRoot(folderPath);
                Global.Data.SaveYoloDataYaml();
                UpdateOutputPathDisplay();
                Global.Data.SaveConfig(Global.Recipe.Name);
                LogDatasetDiagnostics(refreshYaml: false);
                RefreshStatusBar(includeDataset: true);
            }
        }

        private void UpdateOutputPathDisplay()
        {
            string outputRootPath = Global.Data.OutputRootPath ?? string.Empty;
            lbExportPath.Tag = outputRootPath;
            lbExportPath.Text = FormatCompactPath(outputRootPath);
        }

        private static string FormatCompactPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "데이터 경로 미설정";
            }

            string normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaf = Path.GetFileName(normalized);
            string parent = Path.GetFileName(Path.GetDirectoryName(normalized) ?? string.Empty);

            if (string.IsNullOrWhiteSpace(leaf))
            {
                return "데이터 경로 미설정";
            }

            return string.IsNullOrWhiteSpace(parent)
                ? leaf
                : $"{parent}\\{leaf}";
        }

        private void LogDatasetDiagnostics(bool refreshYaml)
        {
            foreach (string line in YoloDatasetDiagnosticsService.BuildOperatorReport(Global.Data, refreshYaml))
            {
                AppLog.NORMAL(line);
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

