using MvcVisionSystem._1._Core;
using Lib.Common;
using MvcVisionSystem.DrawObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using static MvcVisionSystem.DEFINE;

namespace MvcVisionSystem
{
    public partial class FormTeachingVision : Form
    {
        private CGlobal Global = CGlobal.Inst;
        private SplitContainer workspaceLogSplitContainer;
        private SplitContainer workspaceSplitContainer;
        private SplitContainer workbenchSplitContainer;
        private Panel datasetHostPanel;
        private Panel canvasHostPanel;
        private Panel inspectorHostPanel;
        private Panel logHostPanel;
        private Label datasetTitleLabel;
        private Label datasetStatusLabel;
        private Label inspectorTitleLabel;
        private Label inspectorStatusLabel;
        private Label logTitleLabel;
        private Label logStatusLabel;
        private Label canvasStatusLabel;
        private Label toolStateLabel;
        private FlowLayoutPanel canvasToolPanel;
        private Button btnToolMove;
        private Button btnToolRoi;
        private Button btnToolSegment;
        private Button btnToolBrush;
        private Button btnToolEraser;
        private Button btnToolAuto;
        private Button btnToolMerge;
        private Button btnToolDelete;
        private Button btnToolUndo;
        private Button btnToolRedo;
        private NumericUpDown nudBrushRadius;
        private ToolTip teachingToolTip;
        private string lastCanvasStatusText = string.Empty;
        private LabelingRoiMode selectedTeachingToolMode = LabelingRoiMode.Drag;
        private bool updatingTeachingToolState;
        private int PanelCount = 0;
        private DEFINE.LABELING_WORKSPACE_MODE currentWorkspaceMode = DEFINE.LABELING_WORKSPACE_MODE.TEACHING;

        #region Event Register                        
        public EventHandler<ClassItemEventArgs> EventUpdateImageItem;
        public EventHandler<EventArgs> EventUpdateClassItem;
        #endregion

        public Dictionary<VISION_DOCK_FORM, object> Forms = new Dictionary<VISION_DOCK_FORM, object>();

        public DEFINE.LABELING_WORKSPACE_MODE CurrentWorkspaceMode => currentWorkspaceMode;

        public FormTeachingVision()
        {
            InitializeComponent();
            TeachingPanel.Dock = DockStyle.Fill;
            label2.Visible = false;
            Resize += FormTeachingVision_Resize;
        }

        private void FormTeachingVision_Resize(object sender, EventArgs e)
        {
            ApplyDockLayout();
        }

        private void FormTeachingVision_Load(object sender, EventArgs e)
        {            
            InitEvent();
            InitUi();                        
        }

        private void btnNewPanel_Click(object sender, EventArgs e) => CDisplayManager.CreatePanel();

        // 최상위 keys 명령어 이기 때문에 
        // Datagridview 같은곳에 editmode f2번같은게 먹지 않는다.        
        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            Keys key = keyData & ~(Keys.Shift | Keys.Control);

            switch (key)
            {
                case Keys.Escape:
                    //if (CCommon.ShowMessageBox("Notice", "창을 닫으시겠습니까?"))
                    //{
                    //    this.DialogResult = DialogResult.Cancel;
                    //    this.Close();
                    //}
                    return true;
                case Keys.F5:
                    return true;
                case Keys.F7:
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool InitEvent()
        {
            try
            {
                CDisplayManager.EventUpdateResult += OnUpdateResult;
                EventUpdateImageItem+= OnUpdateImageItem;
                EventUpdateClassItem += OnUpdateClassItem;
                Global.Recipe.EventChagedRecipe += OnChangedRecipe;
                Global.System.OnDataUpdated += System_OnDataUpdated;
                Global.DetectionResults.DetectionCandidatesUpdated += DetectionResults_DetectionCandidatesUpdated;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name} Exception ==> {Desc.Message}");
                return false;
            }
            return true;
        }

        private void InitUi()
        {
            InitializeLabelingWorkspaceLayout();
            ApplyDockLayout();
            CDisplayManager.SetForm(this);
            CDisplayManager.SetDockPanel(null);
            CDisplayManager.SetDisplayPanel(canvasHostPanel);
            using (Bitmap placeholder = new Bitmap(10, 10))
            {
                CDisplayManager.CreateLayerDisplay(placeholder, "Main", false);
            }

            Forms[VISION_DOCK_FORM.IMAGELIST] = new FormImageList();
            Forms[VISION_DOCK_FORM.CLASSLIST] = new FormClassList();
            Forms[VISION_DOCK_FORM.LOG] = new FormLog();
            ShowVisionForms();
        }

        private void ShowVisionForms()
        {
            ApplyDockLayout();
            DockContent imageList = GetOrCreateDockContent(VISION_DOCK_FORM.IMAGELIST);
            DockContent log = GetOrCreateDockContent(VISION_DOCK_FORM.LOG);

            HostDockContent(datasetHostPanel, imageList);
            HostDockContent(logHostPanel, log);
            ApplyWorkspaceMode();

            CDisplayManager.ActivateLayer("Main");
        }

        private DockContent GetOrCreateDockContent(VISION_DOCK_FORM form)
        {
            if (Forms.TryGetValue(form, out object existing) && existing is DockContent dockContent && !dockContent.IsDisposed)
            {
                return dockContent;
            }

            DockContent created = form switch
            {
                VISION_DOCK_FORM.IMAGELIST => new FormImageList(),
                VISION_DOCK_FORM.CLASSLIST => new FormClassList(),
                VISION_DOCK_FORM.DETECTIONREVIEW => new FormDetectionReviewPanel(),
                VISION_DOCK_FORM.TRAINING => new FormTrainingPanel(),
                VISION_DOCK_FORM.LOG => new FormLog(),
                _ => null
            };

            if (created != null)
            {
                Forms[form] = created;
            }

            return created;
        }

        public void SetWorkspaceMode(DEFINE.LABELING_WORKSPACE_MODE mode)
        {
            if (currentWorkspaceMode == mode)
            {
                ApplyWorkspaceMode();
                return;
            }

            currentWorkspaceMode = mode;
            ApplyWorkspaceMode();
            ApplyDockLayout();
        }

        private void ApplyWorkspaceMode()
        {
            if (inspectorHostPanel == null || Forms.Count == 0)
            {
                return;
            }

            DockContent inspector = currentWorkspaceMode switch
            {
                DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW => GetOrCreateDockContent(VISION_DOCK_FORM.DETECTIONREVIEW),
                DEFINE.LABELING_WORKSPACE_MODE.TRAINING => GetOrCreateDockContent(VISION_DOCK_FORM.TRAINING),
                _ => GetOrCreateDockContent(VISION_DOCK_FORM.CLASSLIST)
            };
            HostDockContent(inspectorHostPanel, inspector);
            UpdateWorkspaceRegionText();
            UpdateTeachingToolPanelVisibility();
            UpdateTeachingToolButtons();
        }

        private void UpdateWorkspaceRegionText()
        {
            if (datasetTitleLabel != null)
            {
                datasetTitleLabel.Text = "이미지 큐";
            }

            if (datasetStatusLabel != null)
            {
                datasetStatusLabel.Text = currentWorkspaceMode switch
                {
                    DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW => "검수 상태 필터 / 이미지 선택",
                    DEFINE.LABELING_WORKSPACE_MODE.TRAINING => "학습 데이터셋 / 이미지 선택",
                    _ => "라벨 상태 / 이미지 선택"
                };
            }

            if (inspectorTitleLabel == null || inspectorStatusLabel == null)
            {
                return;
            }

            switch (currentWorkspaceMode)
            {
                case DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW:
                    inspectorTitleLabel.Text = "AI 후보 검수";
                    inspectorStatusLabel.Text = "검출 / 후보 / 확정";
                    break;
                case DEFINE.LABELING_WORKSPACE_MODE.TRAINING:
                    inspectorTitleLabel.Text = "학습 준비";
                    inspectorStatusLabel.Text = "데이터셋 / Python / 모델";
                    break;
                default:
                    inspectorTitleLabel.Text = "라벨 패널";
                    inspectorStatusLabel.Text = "라벨 객체 / 클래스";
                    break;
            }

            if (logTitleLabel != null)
            {
                logTitleLabel.Text = currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TRAINING ? "학습 로그" : "로그";
            }

            if (logStatusLabel != null)
            {
                logStatusLabel.Text = currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TRAINING ? "학습 / Python" : "검출 / 저장 / Python";
            }

            if (canvasStatusLabel != null)
            {
                UpdateCanvasStatusText();
            }
        }

        private void ApplyDockLayout()
        {
            ApplyWorkspaceLayout();
        }

        private double GetLeftDockWidth()
        {
            return GetDatasetPanelWidth();
        }

        private double GetRightDockWidth()
        {
            return GetInspectorPanelWidth();
        }

        private int GetDatasetPanelWidth()
        {
            int width = ClientSize.Width > 0 ? ClientSize.Width : Screen.FromControl(this).WorkingArea.Width;
            int minimumWidth = width < 1400 ? 270 : 300;
            int maximumWidth = width < 1700 ? 350 : 380;
            return Math.Max(minimumWidth, Math.Min(maximumWidth, (int)(width * 0.18D)));
        }

        private int GetInspectorPanelWidth()
        {
            int width = ClientSize.Width > 0 ? ClientSize.Width : Screen.FromControl(this).WorkingArea.Width;
            int minimumWidth = width < 1400 ? 350 : 390;
            int maximumWidth = width < 1700 ? 460 : 520;
            return Math.Max(minimumWidth, Math.Min(maximumWidth, (int)(width * 0.24D)));
        }

        private void InitializeLabelingWorkspaceLayout()
        {
            if (workspaceLogSplitContainer != null)
            {
                return;
            }

            TeachingPanel.SuspendLayout();
            TeachingPanel.Controls.Clear();
            TeachingPanel.BackColor = LabelingWorkbenchPalette.Workspace;
            TeachingPanel.Padding = Padding.Empty;

            workspaceLogSplitContainer = new SplitContainer
            {
                BackColor = LabelingWorkbenchPalette.Divider,
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 25,
                Panel2MinSize = 25,
                SplitterWidth = 4
            };

            workspaceSplitContainer = new SplitContainer
            {
                BackColor = LabelingWorkbenchPalette.Divider,
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 25,
                Panel2MinSize = 25,
                SplitterWidth = 4
            };

            workbenchSplitContainer = new SplitContainer
            {
                BackColor = LabelingWorkbenchPalette.Divider,
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 25,
                Panel2MinSize = 25,
                SplitterWidth = 4
            };

            workspaceLogSplitContainer.Panel1.BackColor = LabelingWorkbenchPalette.Workspace;
            workspaceLogSplitContainer.Panel2.BackColor = LabelingWorkbenchPalette.Panel;
            workspaceSplitContainer.Panel1.BackColor = LabelingWorkbenchPalette.Panel;
            workspaceSplitContainer.Panel2.BackColor = LabelingWorkbenchPalette.Workspace;
            workbenchSplitContainer.Panel1.BackColor = LabelingWorkbenchPalette.Canvas;
            workbenchSplitContainer.Panel2.BackColor = LabelingWorkbenchPalette.Panel;

            Panel datasetRegion = CreateWorkbenchRegion("이미지 큐", "라벨 상태 / 이미지 선택", out datasetHostPanel, out datasetTitleLabel, out datasetStatusLabel);
            Panel canvasRegion = CreateWorkbenchRegion("캔버스", "이미지 없음 / 라벨 0 / AI 후보 0", out canvasHostPanel, out _, out canvasStatusLabel, true);
            AttachCanvasTeachingToolPanel(canvasRegion);
            Panel inspectorRegion = CreateWorkbenchRegion("라벨 패널", "라벨 객체 / 클래스", out inspectorHostPanel, out inspectorTitleLabel, out inspectorStatusLabel);
            Panel logRegion = CreateWorkbenchRegion("로그", "검출 / 저장 / Python", out logHostPanel, out logTitleLabel, out logStatusLabel);

            workspaceSplitContainer.Panel1.Controls.Add(datasetRegion);
            workspaceSplitContainer.Panel2.Controls.Add(workbenchSplitContainer);
            workbenchSplitContainer.Panel1.Controls.Add(canvasRegion);
            workbenchSplitContainer.Panel2.Controls.Add(inspectorRegion);
            workspaceLogSplitContainer.Panel1.Controls.Add(workspaceSplitContainer);
            workspaceLogSplitContainer.Panel2.Controls.Add(logRegion);
            TeachingPanel.Controls.Add(workspaceLogSplitContainer);
            TeachingPanel.ResumeLayout();
            UpdateCanvasStatusText();

            this.UIThreadBeginInvoke(ApplyDockLayout);
        }

        private static Panel CreateWorkbenchRegion(string title, string subTitle, out Panel contentPanel, bool isCanvas = false)
        {
            return CreateWorkbenchRegion(title, subTitle, out contentPanel, out _, out _, isCanvas);
        }

        private static Panel CreateWorkbenchRegion(string title, string subTitle, out Panel contentPanel, out Label subTitleControl, bool isCanvas = false)
        {
            return CreateWorkbenchRegion(title, subTitle, out contentPanel, out _, out subTitleControl, isCanvas);
        }

        private static Panel CreateWorkbenchRegion(string title, string subTitle, out Panel contentPanel, out Label titleControl, out Label subTitleControl, bool isCanvas = false)
        {
            Panel region = new Panel
            {
                BackColor = isCanvas ? LabelingWorkbenchPalette.Canvas : LabelingWorkbenchPalette.Panel,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            Panel header = new Panel
            {
                BackColor = isCanvas ? LabelingWorkbenchPalette.Status : LabelingWorkbenchPalette.PanelHeader,
                Dock = DockStyle.Top,
                Height = isCanvas ? 26 : 32,
                Padding = new Padding(isCanvas ? 8 : 10, 0, isCanvas ? 8 : 10, 0)
            };

            Label titleLabel = new Label
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", isCanvas ? 8.75F : 9.25F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                Text = title,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = isCanvas ? 90 : 150
            };
            titleControl = titleLabel;

            Label subTitleLabel = new Label
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                Text = subTitle,
                TextAlign = ContentAlignment.MiddleRight
            };
            subTitleControl = subTitleLabel;

            contentPanel = new Panel
            {
                BackColor = isCanvas ? LabelingWorkbenchPalette.Canvas : LabelingWorkbenchPalette.Surface,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            header.Controls.Add(subTitleLabel);
            header.Controls.Add(titleLabel);
            region.Controls.Add(contentPanel);
            region.Controls.Add(header);
            return region;
        }

        private void AttachCanvasTeachingToolPanel(Panel canvasRegion)
        {
            if (canvasRegion == null)
            {
                return;
            }

            Panel header = canvasRegion.Controls
                .OfType<Panel>()
                .FirstOrDefault(panel => panel.Dock == DockStyle.Top);
            if (header == null)
            {
                return;
            }

            teachingToolTip ??= new ToolTip
            {
                AutomaticDelay = 250,
                AutoPopDelay = 6000,
                InitialDelay = 250,
                ReshowDelay = 100
            };

            canvasToolPanel = new FlowLayoutPanel
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                Height = header.Height,
                Margin = Padding.Empty,
                Padding = new Padding(0, 2, 0, 0),
                Width = 694,
                WrapContents = false
            };

            toolStateLabel = CreateTeachingToolStateLabel();
            btnToolMove = CreateTeachingToolButton("이동", LabelingRoiMode.Drag, 48);
            btnToolRoi = CreateTeachingToolButton("ROI", LabelingRoiMode.Rectangle, 48);
            btnToolSegment = CreateTeachingToolButton("폴리곤", LabelingRoiMode.Segmentation, 62);
            btnToolBrush = CreateTeachingToolButton("브러시", LabelingRoiMode.SegmentationBrush, 62);
            btnToolEraser = CreateTeachingToolButton("지우개", LabelingRoiMode.SegmentationEraser, 62);
            btnToolAuto = CreateTeachingCommandButton("자동", 48, "AutoSegment", btnToolAuto_Click);
            btnToolMerge = CreateTeachingCommandButton("병합", 48, "MergeSegments", btnToolMerge_Click);
            btnToolDelete = CreateTeachingCommandButton("삭제", 48, "DeleteAnnotation", btnToolDelete_Click);
            btnToolUndo = CreateTeachingCommandButton("취소", 48, "UndoAnnotation", btnToolUndo_Click);
            btnToolRedo = CreateTeachingCommandButton("복구", 48, "RedoAnnotation", btnToolRedo_Click);
            nudBrushRadius = CreateBrushRadiusControl();

            canvasToolPanel.Controls.Add(toolStateLabel);
            canvasToolPanel.Controls.Add(btnToolMove);
            canvasToolPanel.Controls.Add(btnToolRoi);
            canvasToolPanel.Controls.Add(btnToolSegment);
            canvasToolPanel.Controls.Add(btnToolBrush);
            canvasToolPanel.Controls.Add(btnToolEraser);
            canvasToolPanel.Controls.Add(nudBrushRadius);
            canvasToolPanel.Controls.Add(btnToolAuto);
            canvasToolPanel.Controls.Add(btnToolMerge);
            canvasToolPanel.Controls.Add(btnToolDelete);
            canvasToolPanel.Controls.Add(btnToolUndo);
            canvasToolPanel.Controls.Add(btnToolRedo);
            header.Controls.Add(canvasToolPanel);
            canvasToolPanel.BringToFront();

            teachingToolTip.SetToolTip(btnToolMove, "캔버스 이동 모드 (1, V, Space)");
            teachingToolTip.SetToolTip(btnToolRoi, "YOLO 사각 ROI 작성 모드 (2, R)");
            teachingToolTip.SetToolTip(btnToolSegment, "세그먼트 폴리곤 작성 모드 (3, S)");
            teachingToolTip.SetToolTip(btnToolBrush, "세그먼트 브러시 작성 모드 (B)");
            teachingToolTip.SetToolTip(btnToolEraser, "세그먼트 지우개 모드 (E)");
            teachingToolTip.SetToolTip(toolStateLabel, "현재 캔버스 라벨링 도구입니다.");
            teachingToolTip.SetToolTip(nudBrushRadius, "브러시/지우개 반경입니다.");
            teachingToolTip.SetToolTip(btnToolAuto, "선택된 ROI 내부의 어두운 결함 영역을 세그먼트로 추출합니다.");
            teachingToolTip.SetToolTip(btnToolMerge, "현재 클래스의 여러 세그먼트를 하나의 외곽 폴리곤으로 병합합니다.");
            teachingToolTip.SetToolTip(btnToolDelete, "선택된 ROI/세그먼트 객체를 삭제합니다. (Delete)");
            teachingToolTip.SetToolTip(btnToolUndo, "마지막 ROI/세그먼트 편집을 되돌립니다.");
            teachingToolTip.SetToolTip(btnToolRedo, "되돌린 ROI/세그먼트 편집을 다시 적용합니다.");
            UpdateTeachingToolPanelVisibility();
            UpdateTeachingToolButtons();
        }

        private static Label CreateTeachingToolStateLabel()
        {
            return new Label
            {
                AutoEllipsis = true,
                BackColor = LabelingWorkbenchPalette.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                Height = 21,
                Margin = new Padding(0, 0, 4, 0),
                Padding = new Padding(6, 0, 4, 0),
                Text = "도구 이동",
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 126
            };
        }

        private Button CreateTeachingToolButton(string text, LabelingRoiMode mode, int width)
        {
            var button = new Button
            {
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                Height = 21,
                Margin = new Padding(2, 0, 0, 0),
                Tag = mode,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Width = width
            };
            button.FlatAppearance.BorderColor = LabelingWorkbenchPalette.Divider;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Selection;
            button.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.PanelHeader;
            button.Click += TeachingToolButton_Click;
            return button;
        }

        private Button CreateTeachingCommandButton(string text, int width, string tag, EventHandler clickHandler)
        {
            Button button = CreateTeachingToolButton(text, LabelingRoiMode.Drag, width);
            button.Tag = tag;
            button.Click -= TeachingToolButton_Click;
            button.Click += clickHandler;
            button.BackColor = Color.FromArgb(45, 86, 94);
            button.FlatAppearance.BorderColor = Color.FromArgb(78, 130, 140);
            return button;
        }

        private NumericUpDown CreateBrushRadiusControl()
        {
            var control = new NumericUpDown
            {
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                Height = 21,
                Margin = new Padding(4, 0, 0, 0),
                Minimum = 2,
                Maximum = 48,
                Value = 8,
                TextAlign = HorizontalAlignment.Center,
                Width = 46
            };
            control.ValueChanged += nudBrushRadius_ValueChanged;
            return control;
        }

        private void TeachingToolButton_Click(object sender, EventArgs e)
        {
            if (sender is Control control && control.Tag is LabelingRoiMode mode)
            {
                SetMainLabelingMode(mode);
            }
        }

        private static FormLayerDisplay GetAvailableMainDisplayOrNull()
        {
            FormLayerDisplay mainDisplay = CDisplayManager.GetMainDisplayOrNull();
            return mainDisplay == null || mainDisplay.IsDisposed ? null : mainDisplay;
        }

        private void btnToolAuto_Click(object sender, EventArgs e)
        {
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            int added = mainDisplay?.AddAutoSegmentationFromRois(null, onlySelected: true) ?? 0;
            if (added > 0)
            {
                CGlobal.Inst.LabelingWorkflow.CommitMainAnnotations(CGlobal.Inst.Data, CGlobal.Inst.System);
                AppLog.NORMAL($"Auto segmentation created {added} segment(s) from ROI.");
            }
            else
            {
                AppLog.NORMAL("Auto segmentation skipped. Select one ROI first.");
            }

            SetMainLabelingMode(LabelingRoiMode.Segmentation);
            UpdateCanvasStatusText();
        }

        private void btnToolMerge_Click(object sender, EventArgs e)
        {
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            int mergedCount = mainDisplay?.MergeSegmentationSegments() ?? 0;
            if (mergedCount > 0)
            {
                CGlobal.Inst.LabelingWorkflow.CommitMainAnnotations(CGlobal.Inst.Data, CGlobal.Inst.System);
                AppLog.NORMAL($"Merged {mergedCount} segment(s) into one polygon.");
            }
            else
            {
                AppLog.Debug("Segmentation merge skipped because there were not enough segments.");
            }

            SetMainLabelingMode(LabelingRoiMode.Segmentation);
            UpdateCanvasStatusText();
        }

        private void btnToolDelete_Click(object sender, EventArgs e)
        {
            bool deleted = CGlobal.Inst.LabelingWorkflow.DeleteMainSelectedAnnotation();
            if (deleted)
            {
                AppLog.NORMAL("Selected annotation was deleted.");
            }
            else
            {
                AppLog.Debug("Annotation delete skipped because no label object is selected.");
            }

            UpdateCanvasStatusText();
        }

        private void btnToolUndo_Click(object sender, EventArgs e)
        {
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            if (mainDisplay?.UndoAnnotationChange() == true)
            {
                CGlobal.Inst.LabelingWorkflow.CommitMainAnnotations(CGlobal.Inst.Data, CGlobal.Inst.System);
                AppLog.NORMAL("Annotation edit was undone.");
            }

            UpdateCanvasStatusText();
        }

        private void btnToolRedo_Click(object sender, EventArgs e)
        {
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            if (mainDisplay?.RedoAnnotationChange() == true)
            {
                CGlobal.Inst.LabelingWorkflow.CommitMainAnnotations(CGlobal.Inst.Data, CGlobal.Inst.System);
                AppLog.NORMAL("Annotation edit was redone.");
            }

            UpdateCanvasStatusText();
        }

        private void nudBrushRadius_ValueChanged(object sender, EventArgs e)
        {
            if (updatingTeachingToolState || nudBrushRadius == null)
            {
                return;
            }

            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            if (mainDisplay != null)
            {
                mainDisplay.SegmentationBrushRadius = (int)nudBrushRadius.Value;
            }

            UpdateTeachingToolButtons();
            UpdateCanvasStatusText();
        }

        private void SetMainLabelingMode(LabelingRoiMode mode)
        {
            selectedTeachingToolMode = mode;
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            if (mainDisplay != null && nudBrushRadius != null)
            {
                mainDisplay.SegmentationBrushRadius = (int)nudBrushRadius.Value;
            }
            mainDisplay?.SetLabelingMode(mode);
            UpdateTeachingToolButtons();
            UpdateCanvasStatusText();
        }

        private LabelingRoiMode GetMainLabelingMode()
        {
            return GetAvailableMainDisplayOrNull()?.CurrentLabelingMode ?? selectedTeachingToolMode;
        }

        private void UpdateTeachingToolButtons()
        {
            LabelingRoiMode currentMode = GetMainLabelingMode();
            selectedTeachingToolMode = currentMode;
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();

            updatingTeachingToolState = true;
            try
            {
                ApplyTeachingToolButtonState(btnToolMove, currentMode == LabelingRoiMode.Drag);
                ApplyTeachingToolButtonState(btnToolRoi, currentMode == LabelingRoiMode.Rectangle);
                ApplyTeachingToolButtonState(btnToolSegment, currentMode == LabelingRoiMode.Segmentation);
                ApplyTeachingToolButtonState(btnToolBrush, currentMode == LabelingRoiMode.SegmentationBrush);
                ApplyTeachingToolButtonState(btnToolEraser, currentMode == LabelingRoiMode.SegmentationEraser);

                if (nudBrushRadius != null)
                {
                    int radius = mainDisplay?.SegmentationBrushRadius ?? 8;
                    decimal clampedRadius = Math.Max(nudBrushRadius.Minimum, Math.Min(nudBrushRadius.Maximum, radius));
                    if (nudBrushRadius.Value != clampedRadius)
                    {
                        nudBrushRadius.Value = clampedRadius;
                    }
                    nudBrushRadius.Enabled = currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING;
                }

                UpdateTeachingToolStateLabel(currentMode);
                SetCommandButtonEnabled(btnToolAuto, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING && mainDisplay?.GetCurrentImage() != null);
                SetCommandButtonEnabled(btnToolMerge, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING && mainDisplay?.GetCurrentImage() != null);
                SetCommandButtonEnabled(btnToolDelete, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING && (mainDisplay?.SelectedAnnotationListIndex ?? -1) > 0);
                SetCommandButtonEnabled(btnToolUndo, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING && mainDisplay?.CanUndoAnnotationChange == true);
                SetCommandButtonEnabled(btnToolRedo, currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING && mainDisplay?.CanRedoAnnotationChange == true);
            }
            finally
            {
                updatingTeachingToolState = false;
            }
        }

        private static void ApplyTeachingToolButtonState(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.BackColor = selected ? LabelingWorkbenchPalette.Selection : LabelingWorkbenchPalette.SurfaceAlt;
            button.ForeColor = selected ? Color.White : LabelingWorkbenchPalette.Text;
            button.FlatAppearance.BorderColor = selected ? LabelingWorkbenchPalette.Accent : LabelingWorkbenchPalette.Divider;
            button.FlatAppearance.BorderSize = selected ? 2 : 1;
            button.FlatAppearance.MouseOverBackColor = selected ? LabelingWorkbenchPalette.AccentHover : LabelingWorkbenchPalette.PanelHeader;
        }

        private static void SetCommandButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.Enabled = enabled;
            button.ForeColor = enabled ? LabelingWorkbenchPalette.Text : LabelingWorkbenchPalette.MutedText;
            button.BackColor = enabled ? Color.FromArgb(45, 86, 94) : LabelingWorkbenchPalette.PanelHeader;
            button.FlatAppearance.BorderColor = enabled ? Color.FromArgb(78, 130, 140) : LabelingWorkbenchPalette.Divider;
            button.FlatAppearance.BorderSize = 1;
        }

        private void UpdateTeachingToolStateLabel(LabelingRoiMode currentMode)
        {
            if (toolStateLabel == null)
            {
                return;
            }

            toolStateLabel.Text = BuildTeachingToolStateText(currentMode);
            bool enabled = currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING;
            toolStateLabel.ForeColor = enabled ? LabelingWorkbenchPalette.Text : LabelingWorkbenchPalette.MutedText;
            toolStateLabel.BackColor = enabled ? LabelingWorkbenchPalette.Surface : LabelingWorkbenchPalette.PanelHeader;
        }

        private string BuildTeachingToolStateText(LabelingRoiMode mode)
        {
            string text = FormatTeachingToolMode(mode);
            if (mode == LabelingRoiMode.SegmentationBrush || mode == LabelingRoiMode.SegmentationEraser)
            {
                int radius = nudBrushRadius != null
                    ? (int)nudBrushRadius.Value
                    : GetAvailableMainDisplayOrNull()?.SegmentationBrushRadius ?? 8;
                return $"{text} / 반경 {radius}";
            }

            return text;
        }

        private void UpdateTeachingToolPanelVisibility()
        {
            if (canvasToolPanel != null)
            {
                canvasToolPanel.Visible = currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TEACHING;
            }
        }

        private void ApplyWorkspaceLayout()
        {
            if (workspaceLogSplitContainer == null || workspaceSplitContainer == null || workbenchSplitContainer == null)
            {
                return;
            }

            int totalWidth = Math.Max(0, TeachingPanel.ClientSize.Width);
            if (totalWidth <= 0)
            {
                return;
            }

            int totalHeight = Math.Max(0, TeachingPanel.ClientSize.Height);
            if (totalHeight > 0)
            {
                int logHeight = GetLogPanelHeight(totalHeight);
                int workbenchHeight = totalHeight - logHeight - workspaceLogSplitContainer.SplitterWidth;
                SetHorizontalSplitterDistance(workspaceLogSplitContainer, workbenchHeight, 420, 110);
            }

            int datasetWidth = GetDatasetPanelWidth();
            SetVerticalSplitterDistance(workspaceSplitContainer, datasetWidth, 270, 620);

            int mainWidth = Math.Max(0, workspaceSplitContainer.Panel2.ClientSize.Width);
            if (mainWidth <= 0)
            {
                return;
            }

            int inspectorWidth = GetInspectorPanelWidth();
            int canvasWidth = mainWidth - inspectorWidth - workbenchSplitContainer.SplitterWidth;
            SetVerticalSplitterDistance(workbenchSplitContainer, canvasWidth, 680, 350);
        }

        private int GetLogPanelHeight(int totalHeight)
        {
            if (currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TRAINING)
            {
                int trainingTarget = (int)(totalHeight * 0.2D);
                return Math.Max(160, Math.Min(260, trainingTarget));
            }

            if (currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW)
            {
                int reviewTarget = (int)(totalHeight * 0.15D);
                return Math.Max(118, Math.Min(176, reviewTarget));
            }

            int target = (int)(totalHeight * 0.13D);
            return Math.Max(96, Math.Min(150, target));
        }

        private static void SetVerticalSplitterDistance(SplitContainer splitContainer, int targetDistance, int panel1MinWidth, int panel2MinWidth)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
            {
                return;
            }

            int totalWidth = splitContainer.ClientSize.Width;
            if (totalWidth <= splitContainer.SplitterWidth + 50)
            {
                return;
            }

            int safePanel1Min = Math.Max(25, Math.Min(panel1MinWidth, Math.Max(25, totalWidth / 2)));
            int safePanel2Min = Math.Max(25, Math.Min(panel2MinWidth, Math.Max(25, totalWidth - safePanel1Min - splitContainer.SplitterWidth)));
            int minDistance = safePanel1Min;
            int maxDistance = Math.Max(minDistance, totalWidth - safePanel2Min - splitContainer.SplitterWidth);
            int distance = Math.Max(minDistance, Math.Min(targetDistance, maxDistance));

            try
            {
                splitContainer.Panel1MinSize = 25;
                splitContainer.Panel2MinSize = 25;
                if (splitContainer.SplitterDistance != distance)
                {
                    splitContainer.SplitterDistance = distance;
                }

                splitContainer.Panel1MinSize = safePanel1Min;
                splitContainer.Panel2MinSize = safePanel2Min;
            }
            catch (InvalidOperationException ex)
            {
                AppLog.Debug($"Workspace splitter layout skipped. {ex.Message}");
            }
        }

        private static void SetHorizontalSplitterDistance(SplitContainer splitContainer, int targetDistance, int panel1MinHeight, int panel2MinHeight)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
            {
                return;
            }

            int totalHeight = splitContainer.ClientSize.Height;
            if (totalHeight <= splitContainer.SplitterWidth + 50)
            {
                return;
            }

            int safePanel1Min = Math.Max(25, Math.Min(panel1MinHeight, Math.Max(25, totalHeight / 2)));
            int safePanel2Min = Math.Max(25, Math.Min(panel2MinHeight, Math.Max(25, totalHeight - safePanel1Min - splitContainer.SplitterWidth)));
            int minDistance = safePanel1Min;
            int maxDistance = Math.Max(minDistance, totalHeight - safePanel2Min - splitContainer.SplitterWidth);
            int distance = Math.Max(minDistance, Math.Min(targetDistance, maxDistance));

            try
            {
                splitContainer.Panel1MinSize = 25;
                splitContainer.Panel2MinSize = 25;
                if (splitContainer.SplitterDistance != distance)
                {
                    splitContainer.SplitterDistance = distance;
                }

                splitContainer.Panel1MinSize = safePanel1Min;
                splitContainer.Panel2MinSize = safePanel2Min;
            }
            catch (InvalidOperationException ex)
            {
                AppLog.Debug($"Workspace log splitter layout skipped. {ex.Message}");
            }
        }

        private static void HostDockContent(Panel hostPanel, DockContent content)
        {
            if (hostPanel == null || content == null)
            {
                return;
            }

            content.SuspendLayout();
            try
            {
                content.TopLevel = false;
                content.FormBorderStyle = FormBorderStyle.None;
                content.Dock = DockStyle.Fill;
                hostPanel.Controls.Clear();
                hostPanel.Controls.Add(content);
                content.Show();
            }
            finally
            {
                content.ResumeLayout();
            }
        }

        private void OnChangedRecipe(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
            });
        }

        private void OnUpdateResult(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                //lbTackTime.Text = CDisplayManager.TackTime;
                UpdateCanvasStatusText();
            });          
        }

        private void System_OnDataUpdated()
        {
            RefreshCanvasStatusOnUiThread();
        }

        private void DetectionResults_DetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            RefreshCanvasStatusOnUiThread();
        }

        private void RefreshCanvasStatusOnUiThread()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                UpdateCanvasStatusText();
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(UpdateCanvasStatusText);
                return;
            }

            UpdateCanvasStatusText();
        }

        private void UpdateCanvasStatusText()
        {
            if (canvasStatusLabel == null)
            {
                return;
            }

            UpdateTeachingToolPanelVisibility();
            UpdateTeachingToolButtons();

            string statusText = BuildCanvasStatusText();
            if (string.Equals(lastCanvasStatusText, statusText, StringComparison.Ordinal))
            {
                return;
            }

            lastCanvasStatusText = statusText;
            canvasStatusLabel.Text = statusText;
            canvasStatusLabel.ForeColor = GetAvailableMainDisplayOrNull()?.GetCurrentImage() == null
                ? LabelingWorkbenchPalette.MutedText
                : LabelingWorkbenchPalette.Text;
        }

        private string BuildCanvasStatusText()
        {
            FormLayerDisplay mainDisplay = GetAvailableMainDisplayOrNull();
            Bitmap currentImage = mainDisplay?.GetCurrentImage();
            if (currentImage == null)
            {
                return currentWorkspaceMode switch
                {
                    DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW => "검수 대기 / 이미지 없음 / AI 후보 0",
                    DEFINE.LABELING_WORKSPACE_MODE.TRAINING => "학습 준비 / 이미지 없음 / 데이터셋 확인 필요",
                    _ => $"티칭 대기 / 이미지 없음 / {FormatTeachingToolMode(GetMainLabelingMode())}"
                };
            }

            string imageName = FirstNonEmpty(
                Global.Data?.LastSelectImageName,
                Global.ImageWorkspace.ActiveImageName,
                "이미지");
            int labelCount = Global.LabelingWorkflow.GetMainRoiItems().Count;
            float minimumConfidence = Global.Data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F;
            var candidates = Global.DetectionResults.GetLastCandidateReviewItems(Global.Data, minimumConfidence);
            int candidateCount = candidates.Count;
            int confirmableCount = candidates.Count(item => item.IsConfirmable);
            string sizeText = $"{currentImage.Width}x{currentImage.Height}";

            if (currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.DETECTION_REVIEW)
            {
                return candidateCount > 0
                    ? $"검수 / {imageName} / {sizeText} / AI 후보 {candidateCount} / 확정 가능 {confirmableCount}"
                    : $"검수 / {imageName} / {sizeText} / AI 후보 없음";
            }

            if (currentWorkspaceMode == DEFINE.LABELING_WORKSPACE_MODE.TRAINING)
            {
                Yolo.YoloDatasetReadinessReport report = Yolo.YoloDatasetReadinessService.Build(Global.Data, refreshYaml: false);
                return report.IsReady
                    ? $"학습 준비 완료 / {imageName} / 라벨 {labelCount} / 객체 {report.Statistics.TotalObjectCount}"
                    : $"학습 준비 필요 / {imageName} / 라벨 {labelCount} / {FirstNonEmpty(report.Errors.FirstOrDefault(), "데이터셋 확인")}";
            }

            if (candidateCount > 0)
            {
                return $"티칭 / {imageName} / {sizeText} / {FormatTeachingToolMode(GetMainLabelingMode())} / 라벨 {labelCount} / AI 후보 {candidateCount}";
            }

            return $"티칭 / {imageName} / {sizeText} / {FormatTeachingToolMode(GetMainLabelingMode())} / 라벨 {labelCount} / AI 후보 0";
        }

        private static string FormatTeachingToolMode(LabelingRoiMode mode)
        {
            return mode switch
            {
                LabelingRoiMode.Rectangle => "도구 ROI",
                LabelingRoiMode.Segmentation => "도구 폴리곤",
                LabelingRoiMode.SegmentationBrush => "도구 브러시",
                LabelingRoiMode.SegmentationEraser => "도구 지우개",
                _ => "도구 이동"
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private void OnUpdateClassItem(object sender, EventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
            });
        }

        private void OnUpdateImageItem(object sender, ClassItemEventArgs e)
        {
            this.UIThreadBeginInvoke(() =>
            {
                Global.LabelingWorkflow.ApplySelectedClass(e.cClassItem);
            });
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                if (PanelCount != CDisplayManager.LayerCount) { PanelCount = CDisplayManager.LayerCount; }
                
                FormLayerDisplay selectedDisplay = CDisplayManager.GetSelectedDisplayOrNull();
                if (selectedDisplay?.ImageChanged == true)
                {
                    Bitmap currentImage = selectedDisplay.GetCurrentImage();
                    if (currentImage != null)
                    {
                        CDisplayManager.ImageSrc = Lib.Common.CImageConverter.ToMat(currentImage);
                    }
                    selectedDisplay.AcceptImageChanged();
                }

                UpdateCanvasStatusText();
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
            }
        }

        private void DisposeTeachingVisionResources()
        {
            Resize -= FormTeachingVision_Resize;
            CDisplayManager.EventUpdateResult -= OnUpdateResult;
            EventUpdateImageItem -= OnUpdateImageItem;
            EventUpdateClassItem -= OnUpdateClassItem;
            Global.Recipe.EventChagedRecipe -= OnChangedRecipe;
            Global.System.OnDataUpdated -= System_OnDataUpdated;
            Global.DetectionResults.DetectionCandidatesUpdated -= DetectionResults_DetectionCandidatesUpdated;
            if (btnToolMove != null)
            {
                btnToolMove.Click -= TeachingToolButton_Click;
            }

            if (btnToolRoi != null)
            {
                btnToolRoi.Click -= TeachingToolButton_Click;
            }

            if (btnToolSegment != null)
            {
                btnToolSegment.Click -= TeachingToolButton_Click;
            }

            if (btnToolBrush != null)
            {
                btnToolBrush.Click -= TeachingToolButton_Click;
            }

            if (btnToolEraser != null)
            {
                btnToolEraser.Click -= TeachingToolButton_Click;
            }

            if (btnToolAuto != null)
            {
                btnToolAuto.Click -= btnToolAuto_Click;
            }

            if (btnToolMerge != null)
            {
                btnToolMerge.Click -= btnToolMerge_Click;
            }

            if (btnToolDelete != null)
            {
                btnToolDelete.Click -= btnToolDelete_Click;
            }

            if (btnToolUndo != null)
            {
                btnToolUndo.Click -= btnToolUndo_Click;
            }

            if (btnToolRedo != null)
            {
                btnToolRedo.Click -= btnToolRedo_Click;
            }

            if (nudBrushRadius != null)
            {
                nudBrushRadius.ValueChanged -= nudBrushRadius_ValueChanged;
            }

            teachingToolTip?.Dispose();
            teachingToolTip = null;
        }
    }   
}
