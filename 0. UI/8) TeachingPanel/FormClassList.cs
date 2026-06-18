using MvcVisionSystem._1._Core;
using RJCodeUI_M1.RJControls;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    public partial class FormClassList : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        private enum ReviewPanelMode
        {
            Labels,
            Candidates,
            Classes
        }

        private CGlobal Global = CGlobal.Inst;
        private TableLayoutPanel reviewLayout;
        private Panel reviewHeaderPanel;
        private Label lblReviewTitle;
        private Label lblReviewSubtitle;
        private Label lblLabelCount;
        private Label lblCandidateCount;
        private Label lblClassCount;
        private Panel selectionDetailPanel;
        private Label lblSelectionType;
        private Label lblSelectionPrimary;
        private Label lblSelectionBounds;
        private Label lblSelectionState;
        private Panel reviewModePanel;
        private Panel reviewContentPanel;
        private Panel labelPagePanel;
        private Panel classPagePanel;
        private Label lblLabelEmptyState;
        private Label lblClassEmptyState;
        private Button btnReviewLabels;
        private Button btnReviewClasses;
        private RJDataGridView dgvCandidateList = null;
        private RJDataGridView dgvClassCatalog;
        private Label lblCandidateState = null;
        private Panel candidateCommandPanel;
        private RJButton btnConfirmSelectedCandidate;
        private RJButton btnConfirmAllCandidates;
        private RJButton btnSkipSelectedCandidate;
        private ReviewPanelMode currentReviewMode = ReviewPanelMode.Labels;
        private bool refreshingRows;

        public FormClassList()
        {
            InitializeComponent();

            Text = "라벨 패널";
            TabText = Text;
            ToolTipText = "현재 이미지의 라벨 객체, AI 후보, 클래스";
            CloseButton = false;
            CloseButtonVisible = false;
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.DockRight;
            Resize += ResizeEvent;
            FormClosed += FormClassList_FormClosed;
            InitializeReviewWorkspace();
            ApplyResponsiveLayout();

            Global.System.OnDataUpdated += System_OnDataUpdated;
            CDisplayManager.AnnotationSelectionChanged += CDisplayManager_AnnotationSelectionChanged;
        }

        private void FormClassList_FormClosed(object sender, FormClosedEventArgs e)
        {
            Global.System.OnDataUpdated -= System_OnDataUpdated;
            CDisplayManager.AnnotationSelectionChanged -= CDisplayManager_AnnotationSelectionChanged;
            if (dgvCandidateList != null)
            {
                dgvCandidateList.CellClick -= dgvImagesList_CellClick;
                dgvCandidateList.SelectionChanged -= dgvImagesList_SelectionChanged;
            }

            if (dgvImagesList != null)
            {
                dgvImagesList.SelectionChanged -= dgvLabelList_SelectionChanged;
            }

            if (dgvClassCatalog != null)
            {
                dgvClassCatalog.SelectionChanged -= dgvClassCatalog_SelectionChanged;
            }

            if (btnReviewLabels != null)
            {
                btnReviewLabels.Click -= reviewModeButton_Click;
            }


            if (btnReviewClasses != null)
            {
                btnReviewClasses.Click -= reviewModeButton_Click;
            }
            if (btnConfirmSelectedCandidate != null)
            {
                btnConfirmSelectedCandidate.Click -= btnConfirmSelectedCandidate_Click;
            }

            if (btnConfirmAllCandidates != null)
            {
                btnConfirmAllCandidates.Click -= btnConfirmAllCandidates_Click;
            }

            if (btnSkipSelectedCandidate != null)
            {
                btnSkipSelectedCandidate.Click -= btnSkipSelectedCandidate_Click;
            }
        }

        private void System_OnDataUpdated()
        {
            RefreshRowsOnUiThread();
        }

        private void DetectionResults_DetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            RefreshRowsOnUiThread(e);
        }

        private void CDisplayManager_AnnotationSelectionChanged(object sender, DisplayAnnotationSelectionChangedEventArgs e)
        {
            if (e?.Display == null || !string.Equals(e.Display.Text, "Main", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SelectMainLabelRowOnUiThread(e.Selection?.SelectedListIndex ?? -1);
        }

        private void SelectMainLabelRowOnUiThread(int selectedListIndex)
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(() => SelectMainLabelRowOnUiThread(selectedListIndex));
                return;
            }

            SelectReviewMode(ReviewPanelMode.Labels);
            RestoreSelectedLabelRow(selectedListIndex);
            UpdateSelectionDetail();
        }

        private void RefreshRowsOnUiThread(DetectionCandidatesUpdatedEventArgs candidateUpdate = null)
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                ShowClassItems();
                ApplyCandidateUpdateNavigation(candidateUpdate);
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(() =>
                {
                    ShowClassItems();
                    ApplyCandidateUpdateNavigation(candidateUpdate);
                });
                return;
            }

            ShowClassItems();
            ApplyCandidateUpdateNavigation(candidateUpdate);
        }

        public void ShowClassItems()
        {
            int selectedCandidateRowIndex = -1;
            int selectedLabelListIndex = Global.LabelingWorkflow.GetMainSelectedRoiListIndex();
            refreshingRows = true;
            var labelItems = Global.LabelingWorkflow.GetMainRoiItems().ToList();
            float minimumConfidence = Global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F;
            var candidateItems = new System.Collections.Generic.List<DetectionCandidateReviewItem>();
            try
            {
                dgvImagesList.Rows.Clear();
                if (dgvCandidateList != null)
                {
                    dgvCandidateList.Rows.Clear();
                }

                if (dgvClassCatalog != null)
                {
                    dgvClassCatalog.Rows.Clear();
                }

                foreach (LabelingRoiListItem item in labelItems)
                {
                    int rowIndex = dgvImagesList.Rows.Add(item.Index.ToString(), item.ClassName, item.RoiText);
                    dgvImagesList.Rows[rowIndex].Tag = item;
                    ApplyLabelRowStyle(dgvImagesList.Rows[rowIndex]);
                }

                foreach (DetectionCandidateReviewItem item in candidateItems)
                {
                    if (dgvCandidateList == null)
                    {
                        break;
                    }

                    int rowIndex = dgvCandidateList.Rows.Add(
                        FormatCandidateKind(item),
                        FormatCandidateClass(item, minimumConfidence),
                        FormatCandidateBounds(item, minimumConfidence));
                    dgvCandidateList.Rows[rowIndex].Tag = item.Index;
                    ApplyCandidateRowStyle(dgvCandidateList.Rows[rowIndex], item);
                    if (item.IsSelected)
                    {
                        selectedCandidateRowIndex = rowIndex;
                    }
                }

                PopulateClassRows();
            }
            finally
            {
                refreshingRows = false;
            }

            RestoreSelectedCandidateRow(selectedCandidateRowIndex);
            RestoreSelectedLabelRow(selectedLabelListIndex);
            UpdateReviewHeader(labelItems.Count, candidateItems.Count);
            UpdateCandidateState(candidateItems, minimumConfidence);
            UpdateEmptyStates(labelItems.Count, candidateItems.Count, Global.Data.ClassNamedList?.Count ?? 0);
            UpdateSelectionDetail(labelItems, candidateItems, minimumConfidence);
            RefreshCandidateCommandState();
        }

        private void ApplyCandidateUpdateNavigation(DetectionCandidatesUpdatedEventArgs candidateUpdate)
        {
            if (candidateUpdate == null)
            {
                return;
            }

            switch (candidateUpdate.Reason)
            {
                case DetectionCandidateUpdateReason.ResultCompleted:
                case DetectionCandidateUpdateReason.SelectionChanged:
                case DetectionCandidateUpdateReason.CandidatesChanged:
                    if (candidateUpdate.CandidateCount > 0)
                    {
                        SelectReviewMode(ReviewPanelMode.Candidates);
                    }
                    break;
            }
        }

        private void dgvImagesList_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView ?? dgvCandidateList;
            if (grid == null || e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count)
            {
                return;
            }

            if (grid.Rows[e.RowIndex].Tag is int candidateIndex)
            {
                SelectCandidate(candidateIndex);
                RefreshCandidateCommandState();
            }
        }

        private void dgvImagesList_SelectionChanged(object sender, EventArgs e)
        {
            if (refreshingRows)
            {
                return;
            }

            if (TryGetCurrentCandidateIndex(out int candidateIndex))
            {
                SelectCandidate(candidateIndex);
            }

            RefreshCandidateCommandState();
            UpdateSelectionDetail();
        }

        private void dgvLabelList_SelectionChanged(object sender, EventArgs e)
        {
            if (refreshingRows)
            {
                return;
            }

            if (dgvImagesList?.CurrentRow?.Tag is LabelingRoiListItem item)
            {
                Global.LabelingWorkflow.SelectMainRoiItem(item.Index);
            }

            UpdateSelectionDetail();
        }

        private void dgvClassCatalog_SelectionChanged(object sender, EventArgs e)
        {
            if (refreshingRows)
            {
                return;
            }

            UpdateSelectionDetail();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;

            if (modifiers == Keys.None)
            {
                if (key == Keys.Enter)
                {
                    return TryConfirmSelectedCandidate() || base.ProcessCmdKey(ref msg, keyData);
                }

                if (key == Keys.Delete || key == Keys.Back)
                {
                    return TryDeleteSelectedLabel() || TrySkipSelectedCandidate() || base.ProcessCmdKey(ref msg, keyData);
                }

                if (key == Keys.A)
                {
                    return TryConfirmAllCandidates() || base.ProcessCmdKey(ref msg, keyData);
                }
            }

            if (modifiers == Keys.Control && key == Keys.A)
            {
                return TryConfirmAllCandidates() || base.ProcessCmdKey(ref msg, keyData);
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool TryGetCurrentCandidateIndex(out int candidateIndex)
        {
            candidateIndex = 0;
            DataGridViewRow row = dgvCandidateList?.CurrentRow;
            if (row == null && dgvCandidateList != null && dgvCandidateList.SelectedRows.Count > 0)
            {
                row = dgvCandidateList.SelectedRows[0];
            }

            if (row?.Tag is int index)
            {
                candidateIndex = index;
                return true;
            }

            return false;
        }

        private bool SelectCandidate(int candidateIndex)
        {
            DetectionCandidateReviewItem selected = Global.DetectionResults
                .GetLastCandidateReviewItems(Global.Data, GetMinimumDetectionConfidence())
                .FirstOrDefault(item => item.IsSelected);
            if (selected?.Index == candidateIndex)
            {
                return true;
            }

            return Global.DetectionResults.SelectDetectionCandidate(candidateIndex, Global.Data);
        }

        private void RestoreSelectedCandidateRow(int selectedCandidateRowIndex)
        {
            if (dgvCandidateList == null || selectedCandidateRowIndex < 0 || selectedCandidateRowIndex >= dgvCandidateList.Rows.Count)
            {
                return;
            }

            refreshingRows = true;
            try
            {
                dgvCandidateList.ClearSelection();
                DataGridViewRow row = dgvCandidateList.Rows[selectedCandidateRowIndex];
                row.Selected = true;
                dgvCandidateList.CurrentCell = row.Cells["CandidateClassColumn"];
            }
            finally
            {
                refreshingRows = false;
            }
        }

        private void RestoreSelectedLabelRow(int selectedLabelListIndex)
        {
            if (dgvImagesList == null)
            {
                return;
            }

            refreshingRows = true;
            try
            {
                dgvImagesList.ClearSelection();
                foreach (DataGridViewRow row in dgvImagesList.Rows)
                {
                    if (row.Tag is LabelingRoiListItem item && item.Index == selectedLabelListIndex)
                    {
                        row.Selected = true;
                        dgvImagesList.CurrentCell = row.Cells.Count > 1 ? row.Cells[1] : row.Cells[0];
                        return;
                    }
                }
            }
            finally
            {
                refreshingRows = false;
            }
        }

        private void InitializeCandidateCommandBar()
        {
            if (candidateCommandPanel != null)
            {
                return;
            }

            candidateCommandPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(6, 6, 6, 5),
                BackColor = LabelingWorkbenchPalette.Panel
            };

            btnSkipSelectedCandidate = CreateCandidateCommandButton("스킵", FontAwesome.Sharp.IconChar.TimesCircle, LabelingWorkbenchPalette.Error);
            btnConfirmAllCandidates = CreateCandidateCommandButton("전체 확정", FontAwesome.Sharp.IconChar.CheckDouble, Color.FromArgb(74, 88, 101));
            btnConfirmSelectedCandidate = CreateCandidateCommandButton("선택 확정", FontAwesome.Sharp.IconChar.Check, LabelingWorkbenchPalette.Selection);

            btnConfirmSelectedCandidate.Click += btnConfirmSelectedCandidate_Click;
            btnConfirmAllCandidates.Click += btnConfirmAllCandidates_Click;
            btnSkipSelectedCandidate.Click += btnSkipSelectedCandidate_Click;

            candidateCommandPanel.Controls.Add(btnSkipSelectedCandidate);
            candidateCommandPanel.Controls.Add(btnConfirmAllCandidates);
            candidateCommandPanel.Controls.Add(btnConfirmSelectedCandidate);
            rjPanel1.Controls.Add(candidateCommandPanel);
            candidateCommandPanel.BringToFront();
        }

        private void InitializeReviewWorkspace()
        {
            if (reviewLayout != null)
            {
                return;
            }

            reviewHeaderPanel = CreateReviewHeaderPanel();

            if (dgvImagesList.Parent != null)
            {
                dgvImagesList.Parent.Controls.Remove(dgvImagesList);
            }

            ConfigureReviewGridBase(dgvImagesList);
            dgvClassCatalog = CreateReviewDataGridView(
                ("ClassIndexColumn", "No", 16F),
                ("ClassNameColumn", "클래스", 54F),
                ("ClassColorColumn", "색상", 30F));

            dgvImagesList.SelectionChanged += dgvLabelList_SelectionChanged;
            dgvClassCatalog.SelectionChanged += dgvClassCatalog_SelectionChanged;

            reviewModePanel = CreateReviewModePanel();
            reviewContentPanel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Surface,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            labelPagePanel = CreateReviewPagePanel();
            classPagePanel = CreateReviewPagePanel();
            lblLabelEmptyState = CreateEmptyStateLabel("라벨 객체 없음", "캔버스에서 ROI를 추가하거나 AI 후보를 확정하세요.");
            lblClassEmptyState = CreateEmptyStateLabel("클래스 없음", "클래스 설정에서 라벨링 클래스를 추가하세요.");

            dgvImagesList.Dock = DockStyle.Fill;
            dgvClassCatalog.Dock = DockStyle.Fill;
            labelPagePanel.Controls.Add(dgvImagesList);
            labelPagePanel.Controls.Add(lblLabelEmptyState);
            classPagePanel.Controls.Add(dgvClassCatalog);
            classPagePanel.Controls.Add(lblClassEmptyState);

            reviewContentPanel.Controls.Add(classPagePanel);
            reviewContentPanel.Controls.Add(labelPagePanel);

            Panel reviewBodyPanel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            reviewBodyPanel.Controls.Add(reviewContentPanel);
            reviewBodyPanel.Controls.Add(reviewModePanel);

            selectionDetailPanel = CreateSelectionDetailPanel();

            reviewLayout = new TableLayoutPanel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 3
            };
            reviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
            reviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));

            reviewHeaderPanel.Dock = DockStyle.Fill;
            selectionDetailPanel.Dock = DockStyle.Fill;

            reviewLayout.Controls.Add(reviewHeaderPanel, 0, 0);
            reviewLayout.Controls.Add(reviewBodyPanel, 0, 1);
            reviewLayout.Controls.Add(selectionDetailPanel, 0, 2);

            rjPanel1.Controls.Clear();
            rjPanel1.Controls.Add(reviewLayout);
            SelectReviewMode(ReviewPanelMode.Labels);
            UpdateReviewHeader(0, 0);
            UpdateEmptyStates(0, 0, Global.Data.ClassNamedList?.Count ?? 0);
            UpdateSelectionDetail();
        }

        private static Panel CreateReviewPagePanel()
        {
            return new Panel
            {
                BackColor = LabelingWorkbenchPalette.Surface,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        private Panel CreateReviewModePanel()
        {
            Panel panel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                Dock = DockStyle.Top,
                Height = 38,
                Margin = Padding.Empty,
                Padding = new Padding(8, 6, 8, 5)
            };

            btnReviewLabels = CreateReviewModeButton("라벨 객체", ReviewPanelMode.Labels);
            btnReviewClasses = CreateReviewModeButton("클래스", ReviewPanelMode.Classes);

            panel.Controls.Add(btnReviewClasses);
            panel.Controls.Add(btnReviewLabels);
            return panel;
        }

        private Button CreateReviewModeButton(string text, ReviewPanelMode mode)
        {
            Button button = new Button
            {
                BackColor = LabelingWorkbenchPalette.PanelHeader,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Size = new Size(92, 27),
                Tag = mode,
                Text = text,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderColor = LabelingWorkbenchPalette.Divider;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.SurfaceAlt;
            button.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.SurfaceAlt;
            button.Click += reviewModeButton_Click;
            return button;
        }

        private void reviewModeButton_Click(object sender, EventArgs e)
        {
            if (sender is Button button && button.Tag is ReviewPanelMode mode)
            {
                SelectReviewMode(mode);
            }
        }

        private void SelectReviewMode(ReviewPanelMode mode)
        {
            currentReviewMode = mode;
            ApplyReviewModeVisibility();
            ApplyResponsiveLayout();
            RefreshCandidateCommandState();
            UpdateSelectionDetail();
        }

        private void ApplyReviewModeVisibility()
        {
            if (labelPagePanel == null || classPagePanel == null)
            {
                return;
            }

            labelPagePanel.Visible = currentReviewMode == ReviewPanelMode.Labels;
            classPagePanel.Visible = currentReviewMode == ReviewPanelMode.Classes;

            switch (currentReviewMode)
            {
                case ReviewPanelMode.Classes:
                    classPagePanel.BringToFront();
                    dgvClassCatalog?.Focus();
                    break;
                default:
                    labelPagePanel.BringToFront();
                    dgvImagesList?.Focus();
                    break;
            }
        }

        private static RJDataGridView CreateReviewDataGridView(params (string Name, string Header, float FillWeight)[] columns)
        {
            var grid = new RJDataGridView();
            ConfigureReviewGridBase(grid);
            grid.Columns.Clear();
            foreach ((string name, string header, float fillWeight) in columns)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    FillWeight = fillWeight
                });
            }

            return grid;
        }

        private static void ConfigureReviewGridBase(RJDataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            grid.EnableHeadersVisualStyles = false;
            grid.MultiSelect = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        private Panel CreateReviewHeaderPanel()
        {
            var panel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.PanelHeader,
                Padding = new Padding(10, 8, 10, 6)
            };

            lblReviewTitle = CreateHeaderLabel("라벨 패널", 12F, FontStyle.Bold, Color.White);
            lblReviewSubtitle = CreateHeaderLabel("이미지를 선택하세요", 8.25F, FontStyle.Regular, LabelingWorkbenchPalette.MutedText);
            lblLabelCount = CreateMetricLabel();
            lblCandidateCount = CreateMetricLabel();
            lblClassCount = CreateMetricLabel();

            panel.Controls.Add(lblReviewTitle);
            panel.Controls.Add(lblReviewSubtitle);
            panel.Controls.Add(lblClassCount);
            panel.Controls.Add(lblCandidateCount);
            panel.Controls.Add(lblLabelCount);
            return panel;
        }

        private static Label CreateHeaderLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", size, style, GraphicsUnit.Point),
                ForeColor = color,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateMetricLabel()
        {
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.Text,
                Padding = new Padding(8, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private static Label CreateEmptyStateLabel(string title, string description)
        {
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                BackColor = LabelingWorkbenchPalette.Surface,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                Padding = new Padding(18, 28, 18, 18),
                Text = $"{title}{Environment.NewLine}{description}",
                TextAlign = ContentAlignment.TopLeft,
                Visible = false
            };
        }

        private Panel CreateSelectionDetailPanel()
        {
            var panel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Surface,
                Padding = new Padding(10, 7, 10, 8)
            };

            lblSelectionType = CreateHeaderLabel("선택 없음", 8.25F, FontStyle.Bold, LabelingWorkbenchPalette.MutedText);
            lblSelectionPrimary = CreateHeaderLabel("이미지 또는 후보를 선택하세요", 9.75F, FontStyle.Bold, Color.White);
            lblSelectionBounds = CreateHeaderLabel("", 8F, FontStyle.Regular, LabelingWorkbenchPalette.MutedText);
            lblSelectionState = CreateHeaderLabel("", 8F, FontStyle.Regular, Color.FromArgb(198, 232, 211));

            panel.Controls.Add(lblSelectionState);
            panel.Controls.Add(lblSelectionBounds);
            panel.Controls.Add(lblSelectionPrimary);
            panel.Controls.Add(lblSelectionType);
            return panel;
        }

        private void UpdateReviewHeader(int labelCount, int candidateCount)
        {
            if (lblReviewSubtitle == null)
            {
                return;
            }

            string imageName = string.IsNullOrWhiteSpace(Global.Data.LastSelectImageName)
                ? "이미지를 선택하세요"
                : Global.Data.LastSelectImageName;
            lblReviewSubtitle.Text = imageName;
            SetMetricText(lblLabelCount, $"라벨 {labelCount}");
            SetMetricText(lblCandidateCount, "검수 분리");
            SetMetricText(lblClassCount, $"클래스 {Global.Data.ClassNamedList?.Count ?? 0}");
        }

        private static void SetMetricText(Label label, string text)
        {
            if (label != null)
            {
                label.Text = text;
            }
        }

        private void UpdateCandidateState(System.Collections.Generic.IReadOnlyList<DetectionCandidateReviewItem> candidates, float minimumConfidence)
        {
            if (lblCandidateState == null)
            {
                return;
            }

            int totalCount = candidates?.Count ?? 0;
            if (totalCount == 0)
            {
                lblCandidateState.ForeColor = Color.FromArgb(178, 193, 215);
                lblCandidateState.Text = "AI 후보 없음";
                return;
            }

            int confirmableCount = candidates.Count(item => item.IsConfirmable);
            int rejectedCount = totalCount - confirmableCount;
            string selectedText = candidates.Any(item => item.IsSelected) ? " / 선택됨" : "";
            string rejectedText = rejectedCount > 0 ? $" / 제외 {rejectedCount}" : "";
            string thresholdText = $" / 기준 {FormatConfidenceThreshold(minimumConfidence)}";
            lblCandidateState.ForeColor = confirmableCount > 0
                ? Color.FromArgb(198, 232, 211)
                : Color.FromArgb(255, 205, 170);
            lblCandidateState.Text = $"AI 후보 {totalCount} / 확정 가능 {confirmableCount}{rejectedText}{selectedText}{thresholdText}";
        }

        private void UpdateEmptyStates(int labelCount, int candidateCount, int classCount)
        {
            SetEmptyStateVisible(lblLabelEmptyState, labelCount == 0);
            SetEmptyStateVisible(lblClassEmptyState, classCount == 0);
        }

        private static void SetEmptyStateVisible(Label label, bool visible)
        {
            if (label == null)
            {
                return;
            }

            label.Visible = visible;
            if (visible)
            {
                label.BringToFront();
            }
        }

        private void UpdateSelectionDetail()
        {
            var labelItems = Global.LabelingWorkflow.GetMainRoiItems().ToList();
            float minimumConfidence = GetMinimumDetectionConfidence();
            var candidateItems = new System.Collections.Generic.List<DetectionCandidateReviewItem>();
            UpdateSelectionDetail(labelItems, candidateItems, minimumConfidence);
        }

        private void UpdateSelectionDetail(
            System.Collections.Generic.IReadOnlyList<LabelingRoiListItem> labelItems,
            System.Collections.Generic.IReadOnlyList<DetectionCandidateReviewItem> candidateItems,
            float minimumConfidence)
        {
            if (lblSelectionType == null)
            {
                return;
            }

            if (currentReviewMode == ReviewPanelMode.Candidates)
            {
                DetectionCandidateReviewItem candidate = GetCurrentCandidateDetail(candidateItems);
                if (candidate == null)
                {
                    SetSelectionDetail("AI 후보", "후보 없음", "검출 결과 없음", "검출을 실행하면 후보가 표시됩니다.", Color.FromArgb(178, 193, 215));
                    return;
                }

                Rectangle bounds = candidate.ClippedBounds;
                string primary = $"{candidate.ClassName}  {candidate.Confidence:P1}";
                string boundsText = $"X {bounds.X}  Y {bounds.Y}  W {bounds.Width}  H {bounds.Height}";
                string state = FormatCandidateState(candidate, minimumConfidence);
                Color stateColor = candidate.IsConfirmable ? Color.FromArgb(198, 232, 211) : Color.FromArgb(255, 205, 170);
                SetSelectionDetail($"AI 후보 #{candidate.Index}", primary, boundsText, state, stateColor);
                return;
            }

            if (currentReviewMode == ReviewPanelMode.Classes)
            {
                Yolo.CClassItem classItem = GetCurrentClassDetail();
                if (classItem == null)
                {
                    SetSelectionDetail("클래스", "클래스 없음", "", "클래스 설정에서 추가할 수 있습니다.", Color.FromArgb(178, 193, 215));
                    return;
                }

                string colorText = ColorTranslator.ToHtml(classItem.DrawColor);
                SetSelectionDetail("클래스", classItem.Text, colorText, "라벨과 AI 후보 확정에 사용됩니다.", classItem.DrawColor);
                return;
            }

            LabelingRoiListItem label = GetCurrentLabelDetail(labelItems);
            if (label == null)
            {
                SetSelectionDetail("라벨 객체", "저장된 객체 없음", "", "후보를 확정하거나 캔버스에서 ROI를 추가하세요.", Color.FromArgb(178, 193, 215));
                return;
            }

            Rectangle roi = label.Roi;
            SetSelectionDetail(
                $"라벨 객체 #{label.Index}",
                label.ClassName,
                $"X {roi.X}  Y {roi.Y}  W {roi.Width}  H {roi.Height}",
                "YOLO 라벨 저장 대상",
                Color.FromArgb(198, 232, 211));
        }

        private DetectionCandidateReviewItem GetCurrentCandidateDetail(System.Collections.Generic.IReadOnlyList<DetectionCandidateReviewItem> candidateItems)
        {
            if (candidateItems == null || candidateItems.Count == 0)
            {
                return null;
            }

            if (TryGetCurrentCandidateIndex(out int currentIndex))
            {
                DetectionCandidateReviewItem current = candidateItems.FirstOrDefault(item => item.Index == currentIndex);
                if (current != null)
                {
                    return current;
                }
            }

            return candidateItems.FirstOrDefault(item => item.IsSelected) ?? candidateItems[0];
        }

        private LabelingRoiListItem GetCurrentLabelDetail(System.Collections.Generic.IReadOnlyList<LabelingRoiListItem> labelItems)
        {
            if (labelItems == null || labelItems.Count == 0)
            {
                return null;
            }

            if (dgvImagesList?.CurrentRow?.Tag is LabelingRoiListItem current)
            {
                return current;
            }

            return labelItems[0];
        }

        private Yolo.CClassItem GetCurrentClassDetail()
        {
            if (dgvClassCatalog?.CurrentRow?.Tag is Yolo.CClassItem current)
            {
                return current;
            }

            return Global.Data.ClassNamedList?.FirstOrDefault();
        }

        private void SetSelectionDetail(string type, string primary, string bounds, string state, Color stateColor)
        {
            lblSelectionType.Text = type ?? string.Empty;
            lblSelectionPrimary.Text = primary ?? string.Empty;
            lblSelectionBounds.Text = bounds ?? string.Empty;
            lblSelectionState.Text = state ?? string.Empty;
            lblSelectionState.ForeColor = stateColor.IsEmpty ? Color.FromArgb(198, 232, 211) : stateColor;
        }

        private void PopulateClassRows()
        {
            if (dgvClassCatalog == null)
            {
                return;
            }

            var classes = Global.Data.ClassNamedList;
            if (classes == null)
            {
                return;
            }

            for (int index = 0; index < classes.Count; index++)
            {
                Yolo.CClassItem classItem = classes[index];
                string colorText = ColorTranslator.ToHtml(classItem?.DrawColor ?? Color.Empty);
                int rowIndex = dgvClassCatalog.Rows.Add((index + 1).ToString(), classItem?.Text ?? string.Empty, colorText);
                dgvClassCatalog.Rows[rowIndex].Tag = classItem;
                ApplyClassRowStyle(dgvClassCatalog.Rows[rowIndex], classItem?.DrawColor ?? Color.FromArgb(92, 108, 132));
            }
        }

        private static RJButton CreateCandidateCommandButton(string text, FontAwesome.Sharp.IconChar icon, Color backColor)
        {
            var button = new RJButton
            {
                BackColor = backColor,
                BorderColor = LabelingWorkbenchPalette.Divider,
                BorderRadius = 4,
                BorderSize = 0,
                Cursor = Cursors.Hand,
                Design = ButtonDesign.IconButton,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.White,
                IconChar = icon,
                IconColor = Color.White,
                IconFont = FontAwesome.Sharp.IconFont.Auto,
                IconSize = 15,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Size = new Size(92, 30),
                Style = ControlStyle.Solid,
                Text = text,
                TextAlign = ContentAlignment.MiddleRight,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void btnConfirmSelectedCandidate_Click(object sender, EventArgs e)
        {
            TryConfirmSelectedCandidate();
        }

        private void btnConfirmAllCandidates_Click(object sender, EventArgs e)
        {
            TryConfirmAllCandidates();
        }

        private void btnSkipSelectedCandidate_Click(object sender, EventArgs e)
        {
            TrySkipSelectedCandidate();
        }

        private bool TryConfirmSelectedCandidate()
        {
            float minimumConfidence = GetMinimumDetectionConfidence();
            bool committed = Global.DetectionResults.CommitSelectedDetectionToMainLabels(Global.Data, Global.System, minimumConfidence, createSegmentationFromBoxes: true);
            if (committed)
            {
                RefreshRowsOnUiThread();
            }

            return committed;
        }

        private bool TryConfirmAllCandidates()
        {
            float minimumConfidence = GetMinimumDetectionConfidence();
            bool committed = Global.DetectionResults.CommitAllLastDetectionToMainLabels(Global.Data, Global.System, minimumConfidence, createSegmentationFromBoxes: true);
            if (committed)
            {
                RefreshRowsOnUiThread();
            }

            return committed;
        }

        private bool TrySkipSelectedCandidate()
        {
            bool skipped = Global.DetectionResults.SkipSelectedDetectionCandidate(Global.Data);
            if (skipped)
            {
                RefreshRowsOnUiThread();
            }

            return skipped;
        }

        private bool TryDeleteSelectedLabel()
        {
            if (currentReviewMode != ReviewPanelMode.Labels)
            {
                return false;
            }

            if (dgvImagesList?.CurrentRow?.Tag is not LabelingRoiListItem item)
            {
                return false;
            }

            Global.LabelingWorkflow.SelectMainRoiItem(item.Index);
            bool deleted = Global.LabelingWorkflow.DeleteMainSelectedAnnotation();
            if (deleted)
            {
                RefreshRowsOnUiThread();
            }

            return deleted;
        }

        private float GetMinimumDetectionConfidence()
        {
            Global.Data.ProjectSettings?.EnsureDefaults();
            return Global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F;
        }

        private void RefreshCandidateCommandState()
        {
            if (btnConfirmSelectedCandidate == null || btnConfirmAllCandidates == null || btnSkipSelectedCandidate == null)
            {
                return;
            }

            float minimumConfidence = GetMinimumDetectionConfidence();
            bool hasCandidates = Global.DetectionResults.GetLastCandidateReviewItems(Global.Data, minimumConfidence).Any();
            btnConfirmSelectedCandidate.Enabled = hasCandidates && Global.DetectionResults.CanCommitSelectedDetection(Global.Data, minimumConfidence);
            btnConfirmAllCandidates.Enabled = hasCandidates && Global.DetectionResults.CanCommitLastDetection(Global.Data, minimumConfidence);
            btnSkipSelectedCandidate.Enabled = hasCandidates && Global.DetectionResults.CanSkipSelectedDetectionCandidate(Global.Data);
        }

        // If the content won't display nicely, hide it
        private void ResizeEvent(object sender, EventArgs e)
        {
            this.Visible = this.Width > this.MinimumSize.Width && this.Height > this.MinimumSize.Height;
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            if (dgvImagesList == null)
            {
                return;
            }

            bool compact = Width < 390;
            Color panelBack = LabelingWorkbenchPalette.Panel;
            Color surfaceBack = LabelingWorkbenchPalette.Surface;
            Color headerBack = LabelingWorkbenchPalette.PanelHeader;
            Color rowBack = LabelingWorkbenchPalette.SurfaceAlt;
            Color selectionBack = LabelingWorkbenchPalette.Selection;

            BackColor = panelBack;
            rjPanel1.BackColor = panelBack;
            rjPanel1.BorderRadius = 0;
            if (reviewLayout != null)
            {
                reviewLayout.BackColor = panelBack;
                reviewLayout.RowStyles[0].Height = compact ? 58 : 66;
                if (reviewLayout.RowStyles.Count > 2)
                {
                    reviewLayout.RowStyles[2].Height = compact ? 70 : 84;
                }
            }

            LayoutReviewHeader(compact);
            LayoutSelectionDetail(compact);
            ApplyReviewModeStyle(compact);
            ApplyReviewGridVisualStyle(dgvClassCatalog, surfaceBack, headerBack, rowBack, selectionBack, compact);
            dgvImagesList.BackgroundColor = surfaceBack;
            dgvImagesList.DgvBackColor = surfaceBack;
            dgvImagesList.RowsColor = rowBack;
            dgvImagesList.RowsTextColor = Color.FromArgb(220, 230, 245);
            dgvImagesList.ColumnHeaderColor = headerBack;
            dgvImagesList.ColumnHeaderTextColor = Color.White;
            dgvImagesList.SelectionBackColor = selectionBack;
            dgvImagesList.SelectionTextColor = Color.White;
            dgvImagesList.BorderRadius = 0;
            dgvImagesList.AlternatingRowsColor = Color.FromArgb(24, 28, 34);
            dgvImagesList.AlternatingRowsColorApply = true;
            dgvImagesList.GridColor = Color.FromArgb(48, 54, 66);
            dgvImagesList.DefaultCellStyle.BackColor = rowBack;
            dgvImagesList.DefaultCellStyle.ForeColor = Color.FromArgb(220, 230, 245);
            dgvImagesList.DefaultCellStyle.SelectionBackColor = selectionBack;
            dgvImagesList.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvImagesList.RowsDefaultCellStyle.BackColor = rowBack;
            dgvImagesList.RowsDefaultCellStyle.ForeColor = Color.FromArgb(220, 230, 245);
            dgvImagesList.RowsDefaultCellStyle.SelectionBackColor = selectionBack;
            dgvImagesList.RowsDefaultCellStyle.SelectionForeColor = Color.White;
            dgvImagesList.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 28, 34);
            dgvImagesList.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(220, 230, 245);
            dgvImagesList.AlternatingRowsDefaultCellStyle.SelectionBackColor = selectionBack;
            dgvImagesList.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            dgvImagesList.ColumnHeadersDefaultCellStyle.BackColor = headerBack;
            dgvImagesList.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvImagesList.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            dgvImagesList.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dgvImagesList.ColumnHeaderHeight = compact ? 32 : 36;
            dgvImagesList.ColumnHeadersHeight = compact ? 32 : 36;
            dgvImagesList.RowTemplate.Height = compact ? 32 : 36;
            dgvImagesList.RowHeight = compact ? 32 : 36;
            dgvImagesList.ColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            if (candidateCommandPanel != null)
            {
                candidateCommandPanel.Height = compact ? 38 : 42;
                candidateCommandPanel.BackColor = panelBack;
            }

            if (lblCandidateState != null)
            {
                lblCandidateState.BackColor = LabelingWorkbenchPalette.Panel;
                lblCandidateState.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Regular, GraphicsUnit.Point);
                lblCandidateState.Height = compact ? 24 : 28;
            }

            ConfigureEmptyStateLabel(lblLabelEmptyState, compact);
            ConfigureEmptyStateLabel(lblClassEmptyState, compact);

            Column2.FillWeight = compact ? 28 : 20;
            Column2.HeaderText = "No";
            Column1.FillWeight = compact ? 72 : 40;
            Column1.HeaderText = "클래스";
            Column3.Visible = !compact;
            Column3.FillWeight = 40;
            Column3.HeaderText = "좌표";

        }

        private static void ConfigureEmptyStateLabel(Label label, bool compact)
        {
            if (label == null)
            {
                return;
            }

            label.BackColor = LabelingWorkbenchPalette.Surface;
            label.ForeColor = LabelingWorkbenchPalette.MutedText;
            label.Font = new Font("Segoe UI", compact ? 8.25F : 9F, FontStyle.Regular, GraphicsUnit.Point);
            label.Padding = compact ? new Padding(12, 18, 12, 12) : new Padding(18, 28, 18, 18);
        }

        private void ApplyReviewModeStyle(bool compact)
        {
            if (reviewModePanel == null)
            {
                return;
            }

            reviewModePanel.BackColor = LabelingWorkbenchPalette.Panel;
            reviewModePanel.Height = compact ? 34 : 36;
            int availableWidth = Math.Max(1, reviewModePanel.ClientSize.Width - reviewModePanel.Padding.Horizontal);
            int buttonWidth = Math.Max(78, Math.Min(compact ? 104 : 118, availableWidth / 2));
            ConfigureReviewModeButton(btnReviewLabels, buttonWidth, compact);
            ConfigureReviewModeButton(btnReviewClasses, buttonWidth, compact);
            ApplyReviewModeVisibility();
        }

        private void ConfigureReviewModeButton(Button button, int width, bool compact)
        {
            if (button == null)
            {
                return;
            }

            bool selected = button.Tag is ReviewPanelMode mode && mode == currentReviewMode;
            button.Width = width;
            button.Height = compact ? 25 : 27;
            button.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            button.ForeColor = selected ? Color.White : LabelingWorkbenchPalette.MutedText;
            button.BackColor = selected ? LabelingWorkbenchPalette.Selection : LabelingWorkbenchPalette.PanelHeader;
            button.FlatAppearance.BorderColor = selected ? LabelingWorkbenchPalette.Accent : LabelingWorkbenchPalette.Divider;
            button.FlatAppearance.BorderSize = selected ? 1 : 0;
        }

        private static void ApplyReviewGridVisualStyle(RJDataGridView grid, Color surfaceBack, Color headerBack, Color rowBack, Color selectionBack, bool compact)
        {
            if (grid == null)
            {
                return;
            }

            grid.BackgroundColor = surfaceBack;
            grid.DgvBackColor = surfaceBack;
            grid.RowsColor = rowBack;
            grid.RowsTextColor = LabelingWorkbenchPalette.Text;
            grid.ColumnHeaderColor = headerBack;
            grid.ColumnHeaderTextColor = Color.White;
            grid.SelectionBackColor = selectionBack;
            grid.SelectionTextColor = Color.White;
            grid.BorderRadius = 0;
            grid.AlternatingRowsColor = LabelingWorkbenchPalette.Surface;
            grid.AlternatingRowsColorApply = true;
            grid.GridColor = LabelingWorkbenchPalette.Divider;
            grid.DefaultCellStyle.BackColor = rowBack;
            grid.DefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            grid.DefaultCellStyle.SelectionBackColor = selectionBack;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.RowsDefaultCellStyle.BackColor = rowBack;
            grid.RowsDefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            grid.RowsDefaultCellStyle.SelectionBackColor = selectionBack;
            grid.RowsDefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = LabelingWorkbenchPalette.Surface;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = selectionBack;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = headerBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeaderHeight = compact ? 32 : 36;
            grid.ColumnHeadersHeight = compact ? 32 : 36;
            grid.RowTemplate.Height = compact ? 32 : 36;
            grid.RowHeight = compact ? 32 : 36;
            grid.ColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void LayoutReviewHeader(bool compact)
        {
            if (reviewHeaderPanel == null || lblReviewTitle == null)
            {
                return;
            }

            reviewHeaderPanel.BackColor = LabelingWorkbenchPalette.PanelHeader;
            int width = Math.Max(1, reviewHeaderPanel.ClientSize.Width);
            int padding = compact ? 8 : 10;
            int metricTop = compact ? 32 : 37;
            int metricHeight = compact ? 20 : 22;
            int metricGap = 5;
            int metricWidth = compact ? 82 : 88;
            int titleWidth = Math.Max(120, width - (padding * 2));

            lblReviewTitle.Font = new Font("Segoe UI", compact ? 10.5F : 12F, FontStyle.Bold, GraphicsUnit.Point);
            lblReviewTitle.SetBounds(padding, compact ? 3 : 5, titleWidth, compact ? 21 : 23);
            lblReviewSubtitle.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            lblReviewSubtitle.SetBounds(padding, compact ? 22 : 27, titleWidth, compact ? 16 : 18);

            int right = width - padding;
            lblClassCount.SetBounds(right - metricWidth, metricTop, metricWidth, metricHeight);
            right = lblClassCount.Left - metricGap;
            lblCandidateCount.SetBounds(right - metricWidth, metricTop, metricWidth, metricHeight);
            right = lblCandidateCount.Left - metricGap;
            lblLabelCount.SetBounds(right - metricWidth, metricTop, metricWidth, metricHeight);
        }

        private void LayoutSelectionDetail(bool compact)
        {
            if (selectionDetailPanel == null || lblSelectionType == null)
            {
                return;
            }

            selectionDetailPanel.BackColor = LabelingWorkbenchPalette.Surface;
            int width = Math.Max(1, selectionDetailPanel.ClientSize.Width);
            int padding = compact ? 8 : 10;
            int lineHeight = compact ? 15 : 17;
            int primaryHeight = compact ? 19 : 22;
            int y = compact ? 5 : 7;
            int contentWidth = Math.Max(40, width - (padding * 2));

            lblSelectionType.Font = new Font("Segoe UI", compact ? 7.5F : 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            lblSelectionPrimary.Font = new Font("Segoe UI", compact ? 8.75F : 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            lblSelectionBounds.Font = new Font("Segoe UI", compact ? 7.5F : 8F, FontStyle.Regular, GraphicsUnit.Point);
            lblSelectionState.Font = new Font("Segoe UI", compact ? 7.5F : 8F, FontStyle.Regular, GraphicsUnit.Point);

            lblSelectionType.SetBounds(padding, y, contentWidth, lineHeight);
            y += lineHeight + (compact ? 1 : 2);
            lblSelectionPrimary.SetBounds(padding, y, contentWidth, primaryHeight);
            y += primaryHeight + (compact ? 1 : 2);
            lblSelectionBounds.SetBounds(padding, y, contentWidth, lineHeight);
            y += lineHeight + 1;
            lblSelectionState.SetBounds(padding, y, contentWidth, lineHeight);
        }

        private static void ConfigureCandidateCommandButton(RJButton button, string text, int width)
        {
            if (button == null)
            {
                return;
            }

            button.Text = text;
            button.Width = width;
        }

        private static string FormatCandidateKind(DetectionCandidateReviewItem item)
        {
            if (item == null)
            {
                return "AI";
            }

            return item.IsSelected ? $"AI {item.Index} 선택" : $"AI {item.Index}";
        }

        private static string FormatCandidateClass(DetectionCandidateReviewItem item, float minimumConfidence)
        {
            string className = string.IsNullOrWhiteSpace(item?.ClassName) ? "(미지정)" : item.ClassName;
            if (item == null)
            {
                return className;
            }

            return $"{className} {item.Confidence:P1} / {FormatCandidateState(item, minimumConfidence)}";
        }

        private static string FormatCandidateBounds(DetectionCandidateReviewItem item, float minimumConfidence)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!item.IsInImageBounds)
            {
                return "이미지 범위 밖";
            }

            Rectangle bounds = item.ClippedBounds;
            string suffix = item.IsConfidenceAccepted ? string.Empty : $" / 기준 {FormatConfidenceThreshold(minimumConfidence)} 미만";
            return $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}{suffix}";
        }

        private static string FormatCandidateState(DetectionCandidateReviewItem item, float minimumConfidence)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!item.IsInImageBounds)
            {
                return "범위 밖";
            }

            if (!item.IsConfidenceAccepted)
            {
                return $"신뢰도 낮음 (기준 {FormatConfidenceThreshold(minimumConfidence)})";
            }

            string thresholdText = $"기준 {FormatConfidenceThreshold(minimumConfidence)}";
            return item.IsSelected ? $"선택됨 / {thresholdText}" : $"확정 가능 / {thresholdText}";
        }

        private static string FormatConfidenceThreshold(float minimumConfidence)
        {
            float safeConfidence = Math.Max(0F, Math.Min(1F, minimumConfidence));
            return safeConfidence.ToString("P0");
        }

        private static void ApplyLabelRowStyle(DataGridViewRow row)
        {
            if (row == null)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            row.DefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            row.DefaultCellStyle.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            row.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        private static void ApplyCandidateRowStyle(DataGridViewRow row, DetectionCandidateReviewItem item)
        {
            if (row == null)
            {
                return;
            }

            bool confirmable = item?.IsConfirmable == true;
            bool selected = item?.IsSelected == true;
            row.DefaultCellStyle.BackColor = selected
                ? Color.FromArgb(78, 84, 32)
                : confirmable ? Color.FromArgb(34, 48, 72) : Color.FromArgb(54, 40, 34);
            row.DefaultCellStyle.ForeColor = selected
                ? Color.FromArgb(255, 248, 184)
                : confirmable ? Color.FromArgb(229, 238, 252) : Color.FromArgb(255, 205, 170);
            row.DefaultCellStyle.SelectionBackColor = selected
                ? Color.FromArgb(115, 118, 41)
                : confirmable ? Color.FromArgb(57, 94, 154) : Color.FromArgb(111, 72, 45);
            row.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        private static void ApplyClassRowStyle(DataGridViewRow row, Color classColor)
        {
            if (row == null)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            row.DefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            row.DefaultCellStyle.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            row.DefaultCellStyle.SelectionForeColor = Color.White;

            if (row.Cells.Count > 2)
            {
                DataGridViewCell colorCell = row.Cells[2];
                Color swatch = classColor.IsEmpty ? Color.FromArgb(92, 108, 132) : classColor;
                colorCell.Style.BackColor = swatch;
                colorCell.Style.ForeColor = GetReadableTextColor(swatch);
                colorCell.Style.SelectionBackColor = swatch;
                colorCell.Style.SelectionForeColor = GetReadableTextColor(swatch);
            }
        }

        private static Color GetReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 140 ? Color.FromArgb(20, 24, 32) : Color.White;
        }

        private bool ChangeSize = false;

        private void Form_VisibleChanged(object sender, EventArgs e)
        {
            if (!ChangeSize)
            {
                if (DockHandler.FloatPane == null) { return; }
                DockHandler.FloatPane.FloatWindow.Bounds = new Rectangle(DockHandler.FloatPane.FloatWindow.Bounds.X, DockHandler.FloatPane.FloatWindow.Bounds.Y, 800, 400);
                this.Refresh();
                ChangeSize = true;
            }
        }

    }
}
