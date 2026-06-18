using MvcVisionSystem._1._Core;
using RJCodeUI_M1.RJControls;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace MvcVisionSystem
{
    public sealed class FormDetectionReviewPanel : DockContent
    {
        private readonly CGlobal Global = CGlobal.Inst;
        private readonly TableLayoutPanel layout;
        private readonly Panel headerPanel;
        private readonly Panel commandPanel;
        private readonly Panel detailPanel;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblSummary;
        private readonly Label lblEmptyState;
        private Label lblSelectionType;
        private Label lblSelectionPrimary;
        private Label lblSelectionBounds;
        private Label lblSelectionState;
        private readonly RJDataGridView dgvCandidateList;
        private readonly RJButton btnDetectCurrentImage;
        private readonly RJButton btnConfirmSelectedCandidate;
        private readonly RJButton btnConfirmAllCandidates;
        private readonly RJButton btnSkipSelectedCandidate;
        private bool refreshingRows;
        private bool detectionRequestInProgress;

        public FormDetectionReviewPanel()
        {
            Text = "AI 후보 검수";
            TabText = Text;
            ToolTipText = "AI 검출 후보를 확인하고 라벨로 확정합니다.";
            CloseButton = false;
            CloseButtonVisible = false;
            DockAreas = DockAreas.DockRight;
            BackColor = LabelingWorkbenchPalette.Panel;

            headerPanel = CreateHeaderPanel(out lblTitle, out lblSubtitle, out lblSummary);
            commandPanel = CreateCommandPanel();
            detailPanel = CreateDetailPanel();
            dgvCandidateList = CreateCandidateGrid();
            lblEmptyState = CreateEmptyStateLabel();

            btnDetectCurrentImage = CreateCommandButton("현재 검출", FontAwesome.Sharp.IconChar.Exclamation, LabelingWorkbenchPalette.Selection);
            btnConfirmSelectedCandidate = CreateCommandButton("선택 확정", FontAwesome.Sharp.IconChar.Check, Color.FromArgb(38, 112, 104));
            btnConfirmAllCandidates = CreateCommandButton("전체 확정", FontAwesome.Sharp.IconChar.CheckDouble, Color.FromArgb(74, 88, 101));
            btnSkipSelectedCandidate = CreateCommandButton("스킵", FontAwesome.Sharp.IconChar.TimesCircle, LabelingWorkbenchPalette.Error);

            btnDetectCurrentImage.Click += btnDetectCurrentImage_Click;
            btnConfirmSelectedCandidate.Click += btnConfirmSelectedCandidate_Click;
            btnConfirmAllCandidates.Click += btnConfirmAllCandidates_Click;
            btnSkipSelectedCandidate.Click += btnSkipSelectedCandidate_Click;
            dgvCandidateList.CellClick += dgvCandidateList_CellClick;
            dgvCandidateList.SelectionChanged += dgvCandidateList_SelectionChanged;

            commandPanel.Controls.Add(btnSkipSelectedCandidate);
            commandPanel.Controls.Add(btnConfirmAllCandidates);
            commandPanel.Controls.Add(btnConfirmSelectedCandidate);
            commandPanel.Controls.Add(btnDetectCurrentImage);

            Panel gridPanel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Surface,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            gridPanel.Controls.Add(dgvCandidateList);
            gridPanel.Controls.Add(lblEmptyState);

            layout = new TableLayoutPanel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            layout.Controls.Add(headerPanel, 0, 0);
            layout.Controls.Add(commandPanel, 0, 1);
            layout.Controls.Add(gridPanel, 0, 2);
            layout.Controls.Add(detailPanel, 0, 3);
            Controls.Add(layout);

            Resize += FormDetectionReviewPanel_Resize;
            FormClosed += FormDetectionReviewPanel_FormClosed;
            Global.System.OnDataUpdated += System_OnDataUpdated;
            Global.DetectionResults.DetectionCandidatesUpdated += DetectionResults_DetectionCandidatesUpdated;

            ApplyResponsiveLayout();
            RefreshRowsOnUiThread();
        }

        private void FormDetectionReviewPanel_FormClosed(object sender, FormClosedEventArgs e)
        {
            Resize -= FormDetectionReviewPanel_Resize;
            Global.System.OnDataUpdated -= System_OnDataUpdated;
            Global.DetectionResults.DetectionCandidatesUpdated -= DetectionResults_DetectionCandidatesUpdated;
            btnDetectCurrentImage.Click -= btnDetectCurrentImage_Click;
            btnConfirmSelectedCandidate.Click -= btnConfirmSelectedCandidate_Click;
            btnConfirmAllCandidates.Click -= btnConfirmAllCandidates_Click;
            btnSkipSelectedCandidate.Click -= btnSkipSelectedCandidate_Click;
            dgvCandidateList.CellClick -= dgvCandidateList_CellClick;
            dgvCandidateList.SelectionChanged -= dgvCandidateList_SelectionChanged;
        }

        private void FormDetectionReviewPanel_Resize(object sender, EventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void System_OnDataUpdated()
        {
            RefreshRowsOnUiThread();
        }

        private void DetectionResults_DetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            RefreshRowsOnUiThread();
        }

        private void RefreshRowsOnUiThread()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                RefreshRows();
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(RefreshRows);
                return;
            }

            RefreshRows();
        }

        private void RefreshRows()
        {
            int selectedCandidateRowIndex = -1;
            float minimumConfidence = GetMinimumDetectionConfidence();
            var candidateItems = Global.DetectionResults.GetLastCandidateReviewItems(Global.Data, minimumConfidence).ToList();

            refreshingRows = true;
            try
            {
                dgvCandidateList.Rows.Clear();
                foreach (DetectionCandidateReviewItem item in candidateItems)
                {
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
            }
            finally
            {
                refreshingRows = false;
            }

            RestoreSelectedCandidateRow(selectedCandidateRowIndex);
            UpdateHeader(candidateItems.Count, minimumConfidence);
            UpdateSelectionDetail(candidateItems, minimumConfidence);
            RefreshCommandState();
        }

        private void RestoreSelectedCandidateRow(int selectedCandidateRowIndex)
        {
            if (selectedCandidateRowIndex < 0 || selectedCandidateRowIndex >= dgvCandidateList.Rows.Count)
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

        private void UpdateHeader(int candidateCount, float minimumConfidence)
        {
            string imageName = string.IsNullOrWhiteSpace(Global.Data.LastSelectImageName)
                ? "이미지를 선택하세요"
                : Global.Data.LastSelectImageName;
            int confirmableCount = Global.DetectionResults
                .GetLastCandidateReviewItems(Global.Data, minimumConfidence)
                .Count(item => item.IsConfirmable);
            lblSubtitle.Text = imageName;
            lblSummary.Text = candidateCount == 0
                ? "AI 후보 없음"
                : $"후보 {candidateCount} / 확정 가능 {confirmableCount} / 기준 {FormatConfidenceThreshold(minimumConfidence)}";
            lblSummary.ForeColor = confirmableCount > 0
                ? Color.FromArgb(198, 232, 211)
                : candidateCount > 0 ? Color.FromArgb(255, 205, 170) : LabelingWorkbenchPalette.MutedText;
            lblEmptyState.Visible = candidateCount == 0;
            if (lblEmptyState.Visible)
            {
                lblEmptyState.BringToFront();
            }
        }

        private void UpdateSelectionDetail(System.Collections.Generic.IReadOnlyList<DetectionCandidateReviewItem> candidateItems, float minimumConfidence)
        {
            DetectionCandidateReviewItem candidate = GetCurrentCandidateDetail(candidateItems);
            if (candidate == null)
            {
                SetSelectionDetail("AI 후보", "후보 없음", "검출 결과 없음", "현재 이미지에서 AI 검출을 실행하세요.", LabelingWorkbenchPalette.MutedText);
                return;
            }

            Rectangle bounds = candidate.ClippedBounds;
            string primary = $"{candidate.ClassName}  {candidate.Confidence:P1}";
            string boundsText = $"X {bounds.X}  Y {bounds.Y}  W {bounds.Width}  H {bounds.Height}";
            string state = FormatCandidateState(candidate, minimumConfidence);
            Color stateColor = candidate.IsConfirmable ? Color.FromArgb(198, 232, 211) : Color.FromArgb(255, 205, 170);
            SetSelectionDetail($"AI 후보 #{candidate.Index}", primary, boundsText, state, stateColor);
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

        private bool TryGetCurrentCandidateIndex(out int candidateIndex)
        {
            candidateIndex = 0;
            DataGridViewRow row = dgvCandidateList.CurrentRow;
            if (row == null && dgvCandidateList.SelectedRows.Count > 0)
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

        private async void btnDetectCurrentImage_Click(object sender, EventArgs e)
        {
            if (detectionRequestInProgress)
            {
                return;
            }

            await RequestCurrentImageDetectionAsync();
        }

        private async Task RequestCurrentImageDetectionAsync()
        {
            detectionRequestInProgress = true;
            UseWaitCursor = true;
            RefreshCommandState();
            try
            {
                bool ready = await Global.EnsurePythonModelClientReadyAsync(5000);
                if (!ready)
                {
                    AppLog.ABNORMAL("Detection review request skipped because YOLO client is not connected.");
                    return;
                }

                Global.DetectionWorkflow.TryStartCurrentImageDetection(
                    Global.Data,
                    Global.DeepLearning,
                    Global.DetectionResults,
                    () => true);
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Detection review request failed: {ex.Message}");
            }
            finally
            {
                detectionRequestInProgress = false;
                UseWaitCursor = false;
                RefreshRowsOnUiThread();
            }
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

        private void dgvCandidateList_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCandidateList.Rows.Count)
            {
                return;
            }

            if (dgvCandidateList.Rows[e.RowIndex].Tag is int candidateIndex)
            {
                SelectCandidate(candidateIndex);
                RefreshCommandState();
            }
        }

        private void dgvCandidateList_SelectionChanged(object sender, EventArgs e)
        {
            if (refreshingRows)
            {
                return;
            }

            if (TryGetCurrentCandidateIndex(out int candidateIndex))
            {
                SelectCandidate(candidateIndex);
            }

            RefreshCommandState();
            RefreshRowsOnUiThread();
        }

        private bool TryConfirmSelectedCandidate()
        {
            float minimumConfidence = GetMinimumDetectionConfidence();
            bool committed = Global.DetectionResults.CommitSelectedDetectionToMainLabels(
                Global.Data,
                Global.System,
                minimumConfidence,
                createSegmentationFromBoxes: true);
            if (committed)
            {
                RefreshRowsOnUiThread();
            }

            return committed;
        }

        private bool TryConfirmAllCandidates()
        {
            float minimumConfidence = GetMinimumDetectionConfidence();
            bool committed = Global.DetectionResults.CommitAllLastDetectionToMainLabels(
                Global.Data,
                Global.System,
                minimumConfidence,
                createSegmentationFromBoxes: true);
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

        private void RefreshCommandState()
        {
            float minimumConfidence = GetMinimumDetectionConfidence();
            bool hasCandidates = Global.DetectionResults.GetLastCandidateReviewItems(Global.Data, minimumConfidence).Any();
            btnDetectCurrentImage.Enabled = !detectionRequestInProgress;
            btnConfirmSelectedCandidate.Enabled = hasCandidates && Global.DetectionResults.CanCommitSelectedDetection(Global.Data, minimumConfidence);
            btnConfirmAllCandidates.Enabled = hasCandidates && Global.DetectionResults.CanCommitLastDetection(Global.Data, minimumConfidence);
            btnSkipSelectedCandidate.Enabled = hasCandidates && Global.DetectionResults.CanSkipSelectedDetectionCandidate(Global.Data);
        }

        private float GetMinimumDetectionConfidence()
        {
            Global.Data.ProjectSettings?.EnsureDefaults();
            return Global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F;
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
                    return TrySkipSelectedCandidate() || base.ProcessCmdKey(ref msg, keyData);
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

        private static Panel CreateHeaderPanel(out Label title, out Label subtitle, out Label summary)
        {
            var panel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.PanelHeader,
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 8, 10, 6)
            };

            title = CreateLabel("AI 후보 검수", 12F, FontStyle.Bold, Color.White);
            subtitle = CreateLabel("이미지를 선택하세요", 8.25F, FontStyle.Regular, LabelingWorkbenchPalette.MutedText);
            summary = CreateLabel("AI 후보 없음", 8.25F, FontStyle.Bold, LabelingWorkbenchPalette.MutedText);

            panel.Controls.Add(summary);
            panel.Controls.Add(subtitle);
            panel.Controls.Add(title);
            return panel;
        }

        private Panel CreateCommandPanel()
        {
            return new Panel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                Dock = DockStyle.Fill,
                Padding = new Padding(7, 7, 7, 6)
            };
        }

        private Panel CreateDetailPanel()
        {
            var panel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Surface,
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 8, 10, 8)
            };

            lblSelectionType = CreateLabel("AI 후보", 8.25F, FontStyle.Bold, LabelingWorkbenchPalette.MutedText);
            lblSelectionPrimary = CreateLabel("후보 없음", 9.75F, FontStyle.Bold, Color.White);
            lblSelectionBounds = CreateLabel("검출 결과 없음", 8F, FontStyle.Regular, LabelingWorkbenchPalette.MutedText);
            lblSelectionState = CreateLabel("현재 이미지에서 AI 검출을 실행하세요.", 8F, FontStyle.Regular, Color.FromArgb(198, 232, 211));

            panel.Controls.Add(lblSelectionState);
            panel.Controls.Add(lblSelectionBounds);
            panel.Controls.Add(lblSelectionPrimary);
            panel.Controls.Add(lblSelectionType);
            return panel;
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
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

        private static RJDataGridView CreateCandidateGrid()
        {
            var grid = new RJDataGridView();
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.BackgroundColor = LabelingWorkbenchPalette.Surface;
            grid.BorderRadius = 0;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Dock = DockStyle.Fill;
            grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            grid.EnableHeadersVisualStyles = false;
            grid.MultiSelect = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CandidateKindColumn", HeaderText = "후보", FillWeight = 24F });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CandidateClassColumn", HeaderText = "클래스 / 신뢰도", FillWeight = 46F });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CandidateBoundsColumn", HeaderText = "위치", FillWeight = 30F });
            return grid;
        }

        private static Label CreateEmptyStateLabel()
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
                Text = $"AI 후보 없음{Environment.NewLine}현재 이미지에서 AI 검출을 실행하면 후보가 표시됩니다.",
                TextAlign = ContentAlignment.TopLeft,
                Visible = true
            };
        }

        private static RJButton CreateCommandButton(string text, FontAwesome.Sharp.IconChar icon, Color backColor)
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
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Size = new Size(92, 30),
                Style = ControlStyle.Solid,
                Text = text,
                TextAlign = ContentAlignment.MiddleRight,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.PanelHeader;
            return button;
        }

        private void ApplyResponsiveLayout()
        {
            bool compact = Width < 390;
            layout.RowStyles[0].Height = compact ? 62 : 70;
            layout.RowStyles[1].Height = compact ? 42 : 48;
            layout.RowStyles[3].Height = compact ? 72 : 88;

            int width = Math.Max(1, headerPanel.ClientSize.Width);
            int padding = compact ? 8 : 10;
            int contentWidth = Math.Max(60, width - (padding * 2));
            lblTitle.Font = new Font("Segoe UI", compact ? 10.5F : 12F, FontStyle.Bold, GraphicsUnit.Point);
            lblTitle.SetBounds(padding, compact ? 4 : 6, contentWidth, compact ? 21 : 23);
            lblSubtitle.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            lblSubtitle.SetBounds(padding, compact ? 25 : 30, contentWidth, compact ? 16 : 18);
            lblSummary.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            lblSummary.SetBounds(padding, compact ? 42 : 49, contentWidth, compact ? 16 : 18);

            int buttonWidth = compact ? 70 : 92;
            ConfigureCommandButton(btnDetectCurrentImage, compact ? "검출" : "현재 검출", buttonWidth);
            ConfigureCommandButton(btnConfirmSelectedCandidate, compact ? "확정" : "선택 확정", compact ? 66 : 92);
            ConfigureCommandButton(btnConfirmAllCandidates, compact ? "전체" : "전체 확정", compact ? 62 : 92);
            ConfigureCommandButton(btnSkipSelectedCandidate, "스킵", compact ? 54 : 70);

            ApplyGridStyle(dgvCandidateList, compact);
            LayoutSelectionDetail(compact);
        }

        private static void ConfigureCommandButton(RJButton button, string text, int width)
        {
            if (button == null)
            {
                return;
            }

            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.IconSize = width < 70 ? 13 : 15;
            button.Font = new Font("Segoe UI", width < 70 ? 7.25F : 8F, FontStyle.Regular);
        }

        private static void ApplyGridStyle(RJDataGridView grid, bool compact)
        {
            if (grid == null)
            {
                return;
            }

            Color rowBack = LabelingWorkbenchPalette.SurfaceAlt;
            Color headerBack = LabelingWorkbenchPalette.PanelHeader;
            Color selectionBack = LabelingWorkbenchPalette.Selection;
            grid.BackgroundColor = LabelingWorkbenchPalette.Surface;
            grid.DgvBackColor = LabelingWorkbenchPalette.Surface;
            grid.RowsColor = rowBack;
            grid.RowsTextColor = LabelingWorkbenchPalette.Text;
            grid.ColumnHeaderColor = headerBack;
            grid.ColumnHeaderTextColor = Color.White;
            grid.SelectionBackColor = selectionBack;
            grid.SelectionTextColor = Color.White;
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
        }

        private void LayoutSelectionDetail(bool compact)
        {
            int width = Math.Max(1, detailPanel.ClientSize.Width);
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

        private void SetSelectionDetail(string type, string primary, string bounds, string state, Color stateColor)
        {
            lblSelectionType.Text = type ?? string.Empty;
            lblSelectionPrimary.Text = primary ?? string.Empty;
            lblSelectionBounds.Text = bounds ?? string.Empty;
            lblSelectionState.Text = state ?? string.Empty;
            lblSelectionState.ForeColor = stateColor.IsEmpty ? Color.FromArgb(198, 232, 211) : stateColor;
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
    }
}
