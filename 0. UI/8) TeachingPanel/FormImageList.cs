using Lib.Common;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using RJCodeUI_M1.RJControls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    public partial class FormImageList : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        private readonly List<string> currentImagePaths = new List<string>();
        private readonly Dictionary<string, Image> thumbnailCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataGridViewRow> imageRowByPath = new Dictionary<string, DataGridViewRow>(StringComparer.OrdinalIgnoreCase);
        private readonly YoloImageReviewStatusService imageReviewStatus = new YoloImageReviewStatusService();
        private int thumbnailSize = 72;
        private bool imageGridConfigured;
        private bool ChangeSize;
        private bool? lastCompactGridLayout;
        private string lastPath = string.Empty;
        private Panel datasetCommandPanel;
        private FlowLayoutPanel datasetCommandFlow;
        private Panel batchStatusPanel;
        private Label batchStatusTitleLabel;
        private Label batchStatusDetailLabel;
        private Panel batchProgressTrack;
        private Panel batchProgressFill;
        private RJButton btnBrowseImageFolder;
        private RJButton btnOpenImageRoot;
        private RJButton btnNextUnlabeled;
        private RJButton btnDetectSelected;
        private RJButton btnDetectBatch;
        private RJButton btnStopBatchDetection;
        private RJButton btnReviewFilter;
        private RJButton btnThumbnailSize;
        private ContextMenuStrip batchDetectionMenu;
        private ContextMenuStrip reviewFilterMenu;
        private ContextMenuStrip thumbnailSizeMenu;
        private ToolTip datasetCommandToolTip;
        private ToolStripStatusLabel selectedReviewStatusLabel;
        private ImageReviewFilter selectedReviewFilter = ImageReviewFilter.All;
        private readonly Queue<string> batchDetectionQueue = new Queue<string>();
        private System.Windows.Forms.Timer batchDetectionTimer;
        private CancellationTokenSource imageGridDetailLoadCts;
        private CancellationTokenSource startupImageRootLoadCts;
        private bool isImageGridDetailLoading;
        private int imageGridDetailTotalCount;
        private int imageGridDetailLoadedCount;
        private bool isBatchDetectionRunning;
        private bool isDetectionClientStarting;
        private string batchDetectionCurrentPath = string.Empty;
        private DateTime batchDetectionDeadlineUtc;
        private int batchDetectionTotalCount;
        private int batchDetectionCompletedCount;
        private string batchDetectionScopeText = string.Empty;
        private const int DefaultDetectionTimeoutSeconds = 30;

        public FormImageList()
        {
            InitializeComponent();

            Text = "데이터셋";
            TabText = Text;
            ToolTipText = "라벨링 이미지 큐";
            CloseButton = false;
            CloseButtonVisible = false;
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft;
            Resize += ResizeEvent;
            CGlobal.Inst.System.OnDataUpdated += System_OnDataUpdated;
            CGlobal.Inst.DetectionResults.DetectionCandidatesUpdated += DetectionResults_DetectionCandidatesUpdated;
            InitializeRuntimeToolbarItems();
            InitializeRuntimeStatusItems();
            InitializeBatchDetectionTimer();
            ApplyResponsiveLayout();
        }

        private void ResizeEvent(object sender, EventArgs e)
        {
            ApplyResponsiveLayout();
            Refresh();
        }

        private void InitializeRuntimeToolbarItems()
        {
            if (toolStripContainer1 == null || datasetCommandPanel != null)
            {
                return;
            }

            toolStripContainer1.TopToolStripPanelVisible = false;
            toolStrip1.Visible = false;

            datasetCommandToolTip = new ToolTip();
            batchDetectionMenu = CreateBatchDetectionMenu();
            reviewFilterMenu = CreateReviewFilterMenu();
            thumbnailSizeMenu = CreateThumbnailSizeMenu();

            datasetCommandPanel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(7, 6, 7, 4)
            };

            datasetCommandFlow = new FlowLayoutPanel
            {
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = true
            };

            btnBrowseImageFolder = CreateDatasetCommandButton("폴더", FontAwesome.Sharp.IconChar.FolderOpen, LabelingWorkbenchPalette.SurfaceAlt);
            btnBrowseImageFolder.Click += btnOpenFolder_Click;
            datasetCommandToolTip.SetToolTip(btnBrowseImageFolder, "이미지 폴더 선택");

            btnOpenImageRoot = CreateDatasetCommandButton("경로", FontAwesome.Sharp.IconChar.FolderOpen, LabelingWorkbenchPalette.SurfaceAlt);
            btnOpenImageRoot.Click += btnOpenImageRoot_Click;
            datasetCommandToolTip.SetToolTip(btnOpenImageRoot, "설정된 이미지 루트 열기");

            btnReviewFilter = CreateDatasetCommandButton("전체", FontAwesome.Sharp.IconChar.ClipboardList, LabelingWorkbenchPalette.SurfaceAlt);
            btnReviewFilter.Click += btnReviewFilter_Click;
            datasetCommandToolTip.SetToolTip(btnReviewFilter, "이미지 상태 필터");

            btnNextUnlabeled = CreateDatasetCommandButton("다음", FontAwesome.Sharp.IconChar.StepForward, LabelingWorkbenchPalette.SurfaceAlt);
            btnNextUnlabeled.Click += btnNextUnlabeled_Click;
            datasetCommandToolTip.SetToolTip(btnNextUnlabeled, "다음 미라벨 이미지로 이동");

            btnDetectSelected = CreateDatasetCommandButton("검출", FontAwesome.Sharp.IconChar.Exclamation, LabelingWorkbenchPalette.Selection);
            btnDetectSelected.Click += btnDetectSelected_Click;
            datasetCommandToolTip.SetToolTip(btnDetectSelected, "선택 이미지 AI 검출");

            btnDetectBatch = CreateDatasetCommandButton("배치", FontAwesome.Sharp.IconChar.CheckDouble, Color.FromArgb(74, 88, 101));
            btnDetectBatch.Click += btnDetectBatch_ButtonClick;
            btnDetectBatch.MouseUp += btnDetectBatch_MouseUp;
            datasetCommandToolTip.SetToolTip(btnDetectBatch, "표시 중인 이미지 일괄 검출. 오른쪽 클릭으로 실패 재시도 선택");

            btnStopBatchDetection = CreateDatasetCommandButton("중지", FontAwesome.Sharp.IconChar.TimesCircle, LabelingWorkbenchPalette.Error);
            btnStopBatchDetection.Enabled = false;
            btnStopBatchDetection.Visible = false;
            btnStopBatchDetection.Click += btnStopBatchDetection_Click;
            datasetCommandToolTip.SetToolTip(btnStopBatchDetection, "일괄 검출 중지");

            btnThumbnailSize = CreateDatasetCommandButton("크기", FontAwesome.Sharp.IconChar.ObjectGroup, LabelingWorkbenchPalette.SurfaceAlt);
            btnThumbnailSize.Click += btnThumbnailSize_Click;
            datasetCommandToolTip.SetToolTip(btnThumbnailSize, "썸네일 크기");

            datasetCommandFlow.Controls.Add(btnBrowseImageFolder);
            datasetCommandFlow.Controls.Add(btnOpenImageRoot);
            datasetCommandFlow.Controls.Add(btnReviewFilter);
            datasetCommandFlow.Controls.Add(btnNextUnlabeled);
            datasetCommandFlow.Controls.Add(btnThumbnailSize);
            datasetCommandPanel.Controls.Add(datasetCommandFlow);
            batchStatusPanel = CreateBatchStatusPanel();
            toolStripContainer1.ContentPanel.Controls.Add(batchStatusPanel);
            toolStripContainer1.ContentPanel.Controls.Add(datasetCommandPanel);
            datasetCommandPanel.BringToFront();
            UpdateReviewFilterButtonText();
            UpdateBatchStatusPanel();
        }

        private Panel CreateBatchStatusPanel()
        {
            var panel = new Panel
            {
                BackColor = LabelingWorkbenchPalette.SurfaceAlt,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 5, 8, 5),
                Visible = false
            };

            batchStatusTitleLabel = new Label
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(215, 235, 229),
                Text = "일괄 검출",
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 84
            };

            batchStatusDetailLabel = new Label
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = LabelingWorkbenchPalette.MutedText,
                TextAlign = ContentAlignment.MiddleLeft
            };

            batchProgressTrack = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Divider,
                Dock = DockStyle.Bottom,
                Height = 4,
                Margin = Padding.Empty
            };

            batchProgressFill = new Panel
            {
                BackColor = LabelingWorkbenchPalette.Accent,
                Dock = DockStyle.Left,
                Margin = Padding.Empty,
                Width = 0
            };

            batchProgressTrack.SizeChanged += batchProgressTrack_SizeChanged;
            batchProgressTrack.Controls.Add(batchProgressFill);
            panel.Controls.Add(batchStatusDetailLabel);
            panel.Controls.Add(batchStatusTitleLabel);
            panel.Controls.Add(batchProgressTrack);
            return panel;
        }

        private static RJButton CreateDatasetCommandButton(string text, FontAwesome.Sharp.IconChar icon, Color backColor)
        {
            var button = new RJButton
            {
                BackColor = backColor,
                BorderColor = LabelingWorkbenchPalette.Divider,
                BorderRadius = 4,
                BorderSize = 0,
                Cursor = Cursors.Hand,
                Design = ButtonDesign.IconButton,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.75F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.White,
                IconChar = icon,
                IconColor = Color.White,
                IconFont = FontAwesome.Sharp.IconFont.Auto,
                IconSize = 14,
                Margin = new Padding(0, 0, 4, 4),
                Padding = Padding.Empty,
                Size = new Size(64, 29),
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

        private ContextMenuStrip CreateBatchDetectionMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("표시 행")
            {
                Tag = BatchDetectionMode.VisibleRows
            });
            menu.Items.Add(new ToolStripMenuItem("실패 재시도")
            {
                Tag = BatchDetectionMode.FailedRows
            });
            menu.ItemClicked += btnDetectBatch_DropDownItemClicked;
            return menu;
        }

        private ContextMenuStrip CreateReviewFilterMenu()
        {
            var menu = new ContextMenuStrip();
            foreach (ImageReviewFilter filter in Enum.GetValues(typeof(ImageReviewFilter)))
            {
                menu.Items.Add(new ToolStripMenuItem(GetReviewFilterDisplayName(filter))
                {
                    Tag = filter
                });
            }

            menu.ItemClicked += btnReviewFilter_DropDownItemClicked;
            return menu;
        }

        private ContextMenuStrip CreateThumbnailSizeMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(CreateThumbnailSizeMenuItem("72x72", 72));
            menu.Items.Add(CreateThumbnailSizeMenuItem("96x96", 96));
            menu.Items.Add(CreateThumbnailSizeMenuItem("120x120", 120));
            menu.Items.Add(CreateThumbnailSizeMenuItem("200x200", 200));
            return menu;
        }

        private ToolStripMenuItem CreateThumbnailSizeMenuItem(string text, int size)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag = size
            };
            item.Click += thumbnailSizeMenuItem_Click;
            return item;
        }

        private void ShowCommandMenu(Control owner, ContextMenuStrip menu)
        {
            if (owner == null || menu == null)
            {
                return;
            }

            menu.Show(owner, new Point(0, owner.Height + 2));
        }

        private void btnReviewFilter_Click(object sender, EventArgs e)
        {
            ShowCommandMenu(btnReviewFilter, reviewFilterMenu);
        }

        private void btnDetectBatch_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowCommandMenu(btnDetectBatch, batchDetectionMenu);
            }
        }

        private void btnThumbnailSize_Click(object sender, EventArgs e)
        {
            ShowCommandMenu(btnThumbnailSize, thumbnailSizeMenu);
        }

        private void thumbnailSizeMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is int size)
            {
                SetThumbnailSize(size);
            }
        }

        private void InitializeRuntimeStatusItems()
        {
            if (StatusStrip == null || selectedReviewStatusLabel != null)
            {
                return;
            }

            selectedReviewStatusLabel = new ToolStripStatusLabel
            {
                Alignment = ToolStripItemAlignment.Right,
                AutoToolTip = true,
                Name = "ReviewDetailStatusLabel",
                Spring = true,
                TextAlign = ContentAlignment.MiddleRight
            };

            StatusStrip.Items.Add(selectedReviewStatusLabel);
            UpdateSelectedReviewStatusText();
        }

        private void InitializeBatchDetectionTimer()
        {
            if (batchDetectionTimer != null)
            {
                return;
            }

            batchDetectionTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            batchDetectionTimer.Tick += batchDetectionTimer_Tick;
        }

        private void ApplyResponsiveLayout()
        {
            if (btnOpenFileList == null || uiSplitContainer1 == null || toolStripDropDownButton1 == null || imageGridView == null)
            {
                return;
            }

            int panelWidth = ClientSize.Width > 0 ? ClientSize.Width : Width;
            bool compact = Width < 520 || panelWidth < 520;
            Color panelBack = LabelingWorkbenchPalette.Panel;
            Color surfaceBack = LabelingWorkbenchPalette.Surface;
            Color textColor = LabelingWorkbenchPalette.Text;

            BackColor = panelBack;
            rjPanel1.BackColor = panelBack;
            rjPanel1.BorderRadius = 0;
            rjPanel2.BackColor = panelBack;
            rjPanel2.BorderRadius = 0;
            toolStripContainer1.TopToolStripPanelVisible = false;
            toolStrip1.Visible = false;
            StatusStrip.BackColor = panelBack;
            StatusStrip.ForeColor = textColor;
            StatusLabel.ForeColor = textColor;
            if (selectedReviewStatusLabel != null)
            {
                selectedReviewStatusLabel.ForeColor = Color.FromArgb(185, 201, 225);
            }

            imageGridView.BackgroundColor = surfaceBack;
            imageGridView.BackColor = surfaceBack;
            imageGridView.DgvBackColor = surfaceBack;
            imageGridView.RowsColor = LabelingWorkbenchPalette.SurfaceAlt;
            imageGridView.RowsTextColor = LabelingWorkbenchPalette.Text;
            imageGridView.ColumnHeaderColor = LabelingWorkbenchPalette.PanelHeader;
            imageGridView.ColumnHeaderTextColor = Color.White;
            imageGridView.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            imageGridView.SelectionTextColor = Color.White;
            imageGridView.BorderRadius = 0;
            imageGridView.AlternatingRowsColor = LabelingWorkbenchPalette.Surface;
            imageGridView.AlternatingRowsColorApply = true;
            imageGridView.GridColor = LabelingWorkbenchPalette.Divider;
            imageGridView.DefaultCellStyle.BackColor = surfaceBack;
            imageGridView.DefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            imageGridView.DefaultCellStyle.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            imageGridView.DefaultCellStyle.SelectionForeColor = Color.White;
            imageGridView.RowsDefaultCellStyle.BackColor = LabelingWorkbenchPalette.SurfaceAlt;
            imageGridView.RowsDefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            imageGridView.RowsDefaultCellStyle.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            imageGridView.RowsDefaultCellStyle.SelectionForeColor = Color.White;
            imageGridView.AlternatingRowsDefaultCellStyle.BackColor = LabelingWorkbenchPalette.Surface;
            imageGridView.AlternatingRowsDefaultCellStyle.ForeColor = LabelingWorkbenchPalette.Text;
            imageGridView.AlternatingRowsDefaultCellStyle.SelectionBackColor = LabelingWorkbenchPalette.Selection;
            imageGridView.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            imageGridView.ColumnHeadersDefaultCellStyle.BackColor = LabelingWorkbenchPalette.PanelHeader;
            imageGridView.ColumnHeadersDefaultCellStyle.ForeColor = textColor;
            ApplyImageGridColumnLayout(compact);
            ApplyImageGridColumnHeaderStyle();
            if (lastCompactGridLayout != compact)
            {
                lastCompactGridLayout = compact;
                RefreshGridStatusTextFormatting();
            }

            btnOpenFileList.Visible = false;
            if (!uiSplitContainer1.Panel1Collapsed)
            {
                uiSplitContainer1.Panel1Collapsed = true;
            }

            uiSplitContainer1.SplitterWidth = 1;

            ApplyDatasetCommandBarLayout(compact);
            UpdateReviewFilterButtonText();
        }

        private void ApplyDatasetCommandBarLayout(bool compact)
        {
            if (datasetCommandPanel == null)
            {
                return;
            }

            datasetCommandPanel.BackColor = LabelingWorkbenchPalette.Panel;
            datasetCommandPanel.Height = compact ? 64 : 70;
            int buttonWidth = compact ? 56 : 62;
            int buttonHeight = compact ? 25 : 27;
            ConfigureDatasetCommandButton(btnBrowseImageFolder, buttonWidth, buttonHeight, compact);
            ConfigureDatasetCommandButton(btnOpenImageRoot, buttonWidth, buttonHeight, compact);
            ConfigureDatasetCommandButton(btnReviewFilter, buttonWidth, buttonHeight, compact);
            ConfigureDatasetCommandButton(btnNextUnlabeled, buttonWidth, buttonHeight, compact);
            ConfigureDatasetCommandButton(btnThumbnailSize, buttonWidth, buttonHeight, compact);

            if (btnThumbnailSize != null)
            {
                btnThumbnailSize.Text = compact ? "크기" : "썸네일";
            }

            if (batchStatusPanel != null)
            {
                batchStatusPanel.Height = compact ? 36 : 40;
            }

            if (batchStatusTitleLabel != null)
            {
                batchStatusTitleLabel.Width = compact ? 72 : 84;
                batchStatusTitleLabel.Font = new Font("Segoe UI", compact ? 7.75F : 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            }

            if (batchStatusDetailLabel != null)
            {
                batchStatusDetailLabel.Font = new Font("Segoe UI", compact ? 7.5F : 8F, FontStyle.Regular, GraphicsUnit.Point);
            }

            UpdateBatchStatusPanel();
        }

        private static void ConfigureDatasetCommandButton(RJButton button, int width, int height, bool compact)
        {
            if (button == null)
            {
                return;
            }

            button.Width = width;
            button.Height = height;
            button.IconSize = compact ? 13 : 14;
            button.Font = new Font("Segoe UI", compact ? 7.25F : 7.75F, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void Form_VisibleChanged(object sender, EventArgs e)
        {
            if (!ChangeSize)
            {
                if (DockHandler.FloatPane == null) { return; }
                DockHandler.FloatPane.FloatWindow.Bounds = new Rectangle(DockHandler.FloatPane.FloatWindow.Bounds.X, DockHandler.FloatPane.FloatWindow.Bounds.Y, 800, 400);
                Refresh();
                ChangeSize = true;
            }
        }

        private void Form_Load(object sender, EventArgs e)
        {
            ConfigureImageGrid();
            ShowImageDgvCore(new List<string>(), loadInitialImage: false);
            this.UIThreadBeginInvoke(() => _ = LoadConfiguredImageRootOnStartupAsync());
        }

        private void ConfigureImageGrid()
        {
            if (imageGridConfigured)
            {
                return;
            }

            imageGridView.AllowUserToAddRows = false;
            imageGridView.AllowUserToDeleteRows = false;
            imageGridView.AllowUserToResizeRows = false;
            imageGridView.AutoGenerateColumns = false;
            imageGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            imageGridView.BorderStyle = BorderStyle.None;
            imageGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            imageGridView.ColumnHeadersHeight = 30;
            imageGridView.MultiSelect = false;
            imageGridView.ReadOnly = true;
            imageGridView.RowHeadersVisible = false;
            imageGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            imageGridView.RowTemplate.Height = thumbnailSize + 16;
            imageGridView.SelectionChanged += imageGridView_SelectionChanged;

            imageGridView.Columns.Clear();
            imageGridView.Columns.Add(new DataGridViewImageColumn
            {
                Name = "ThumbnailColumn",
                HeaderText = string.Empty,
                Width = thumbnailSize + 20,
                ImageLayout = DataGridViewImageCellLayout.Zoom
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NameColumn",
                HeaderText = "이미지",
                Width = 180
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LabelStatusColumn",
                HeaderText = "라벨",
                Width = 92
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DetectStatusColumn",
                HeaderText = "검출",
                Width = 92
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DimensionsColumn",
                HeaderText = "크기",
                Width = 88
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FileSizeColumn",
                HeaderText = "용량",
                Width = 76
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FolderColumn",
                HeaderText = "폴더",
                Width = 110
            });
            imageGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ModifiedColumn",
                HeaderText = "수정",
                Width = 126
            });

            imageGridConfigured = true;
            ApplyImageGridColumnLayout(Width < 520);
        }

        private void ApplyImageGridColumnLayout(bool compact)
        {
            if (!imageGridConfigured || imageGridView.Columns.Count == 0)
            {
                return;
            }

            imageGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            imageGridView.Columns["NameColumn"].HeaderText = compact ? "파일" : "이미지";
            imageGridView.Columns["LabelStatusColumn"].HeaderText = compact ? "라벨" : "라벨 상태";
            imageGridView.Columns["DetectStatusColumn"].HeaderText = compact ? "AI" : "AI 검출";
            if (compact)
            {
                int gridWidth = imageGridView.ClientSize.Width > 0 ? imageGridView.ClientSize.Width : Width;
                int availableWidth = Math.Max(170, gridWidth - SystemInformation.VerticalScrollBarWidth - 22);
                int thumbnailColumnWidth = Math.Max(38, Math.Min(46, (int)Math.Round(availableWidth * 0.17D)));
                int labelColumnWidth = Math.Max(54, Math.Min(64, (int)Math.Round(availableWidth * 0.22D)));
                int detectColumnWidth = Math.Max(66, Math.Min(82, (int)Math.Round(availableWidth * 0.28D)));
                int nameColumnWidth = Math.Max(56, availableWidth - thumbnailColumnWidth - labelColumnWidth - detectColumnWidth);

                imageGridView.Columns["ThumbnailColumn"].Width = thumbnailColumnWidth;
                imageGridView.Columns["NameColumn"].Width = nameColumnWidth;
                imageGridView.Columns["LabelStatusColumn"].Width = labelColumnWidth;
                imageGridView.Columns["DetectStatusColumn"].Width = detectColumnWidth;
            }
            else
            {
                imageGridView.Columns["ThumbnailColumn"].Width = thumbnailSize + 20;
                imageGridView.Columns["NameColumn"].Width = 180;
                imageGridView.Columns["LabelStatusColumn"].Width = 104;
                imageGridView.Columns["DetectStatusColumn"].Width = 108;
            }

            SetColumnVisible("DimensionsColumn", !compact);
            SetColumnVisible("FileSizeColumn", !compact);
            SetColumnVisible("FolderColumn", !compact);
            SetColumnVisible("ModifiedColumn", !compact);
            ApplyImageGridColumnHeaderStyle();
        }

        private void ApplyImageGridColumnHeaderStyle()
        {
            if (imageGridView?.Columns == null)
            {
                return;
            }

            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle
            {
                BackColor = LabelingWorkbenchPalette.PanelHeader,
                ForeColor = LabelingWorkbenchPalette.Text,
                SelectionBackColor = LabelingWorkbenchPalette.PanelHeader,
                SelectionForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };

            foreach (DataGridViewColumn column in imageGridView.Columns)
            {
                column.HeaderCell.Style = headerStyle;
                column.DefaultCellStyle.Alignment =
                    column.Name == "LabelStatusColumn" || column.Name == "DetectStatusColumn"
                        ? DataGridViewContentAlignment.MiddleCenter
                        : DataGridViewContentAlignment.MiddleLeft;
            }

            SetColumnHeaderToolTip("LabelStatusColumn", "YOLO 라벨 저장 상태");
            SetColumnHeaderToolTip("DetectStatusColumn", "AI 검출 후보 상태");
            SetColumnHeaderToolTip("NameColumn", "이미지 파일명");
        }

        private void SetColumnHeaderToolTip(string columnName, string toolTipText)
        {
            if (imageGridView?.Columns.Contains(columnName) == true)
            {
                imageGridView.Columns[columnName].ToolTipText = toolTipText;
                imageGridView.Columns[columnName].HeaderCell.ToolTipText = toolTipText;
            }
        }

        private void ApplyImageGridRowVisualStyle(DataGridViewRow row)
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

        private void SetColumnVisible(string columnName, bool visible)
        {
            if (imageGridView.Columns.Contains(columnName))
            {
                imageGridView.Columns[columnName].Visible = visible;
            }
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            LoadFolderPath(out string folderPath);
            if (folderPath != "")
            {
                ShowImageDgv(GetImageFiles(folderPath));
            }
        }

        private void btnOpenImageRoot_Click(object sender, EventArgs e)
        {
            if (!TryLoadConfiguredImageRoot())
            {
                CGlobal.Inst.Data.ProjectSettings?.EnsureDefaults();
                string imageRootPath = CGlobal.Inst.Data.ProjectSettings?.PythonModel?.ImageRootPath;
                AppLog.ABNORMAL($"Configured image root does not exist: {imageRootPath}");
            }
        }

        private async void btnDetectSelected_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = GetSelectedImageRow();
            string imagePath = row?.Tag as string;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            LoadImageToMainDisplay(imagePath);
            ApplyStatusToRow(row, imageReviewStatus.SetDetectionRequested(imagePath));

            if (!await EnsurePythonClientReadyForDatasetAsync("Selected image detection"))
            {
                YoloImageReviewStatus failed = imageReviewStatus.SetDetectionFailed(imagePath, string.Empty, "YOLO client is not connected.");
                ApplyStatusToRow(row, failed);
                imageReviewStatus.SaveReviewStatus(CGlobal.Inst.Data);
                return;
            }

            bool started = CGlobal.Inst.DetectionWorkflow.TryStartCurrentImageDetection(
                CGlobal.Inst.Data,
                CGlobal.Inst.DeepLearning,
                CGlobal.Inst.DetectionResults,
                () => true);

            if (!started)
            {
                YoloImageReviewStatus failed = imageReviewStatus.SetDetectionFailed(imagePath, string.Empty, "Detection request failed.");
                ApplyStatusToRow(row, failed);
                imageReviewStatus.SaveReviewStatus(CGlobal.Inst.Data);
            }
        }

        private void btnDetectBatch_ButtonClick(object sender, EventArgs e)
        {
            StartBatchDetection(GetVisibleImagePaths(), "표시 행");
        }

        private void btnDetectBatch_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!(e.ClickedItem?.Tag is BatchDetectionMode mode))
            {
                return;
            }

            switch (mode)
            {
                case BatchDetectionMode.FailedRows:
                    StartBatchDetection(GetFailedImagePaths(), "실패 재시도");
                    break;
                default:
                    StartBatchDetection(GetVisibleImagePaths(), "표시 행");
                    break;
            }
        }

        private void btnStopBatchDetection_Click(object sender, EventArgs e)
        {
            StopBatchDetection(markCurrentFailed: true, reason: "Batch detection stopped by operator.");
        }

        private void batchDetectionTimer_Tick(object sender, EventArgs e)
        {
            if (!isBatchDetectionRunning || string.IsNullOrWhiteSpace(batchDetectionCurrentPath))
            {
                return;
            }

            if (DateTime.UtcNow < batchDetectionDeadlineUtc)
            {
                return;
            }

            string timeoutPath = batchDetectionCurrentPath;
            CGlobal.Inst.DetectionResults.CancelPendingDetection();
            MarkBatchDetectionFailed(timeoutPath, "Batch detection timed out.");
            StopBatchDetection(markCurrentFailed: false, reason: $"Batch detection timed out: {Path.GetFileName(timeoutPath)}");
        }

        private void batchProgressTrack_SizeChanged(object sender, EventArgs e)
        {
            UpdateBatchStatusPanel();
        }

        private List<string> GetVisibleImagePaths()
        {
            var imagePaths = new List<string>();
            foreach (DataGridViewRow row in imageGridView.Rows)
            {
                string imagePath = row.Tag as string;
                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                {
                    imagePaths.Add(imagePath);
                }
            }

            return imagePaths;
        }

        private List<string> GetFailedImagePaths()
        {
            return currentImagePaths
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Where(path => imageReviewStatus.GetOrCreate(path)?.ReviewState == YoloImageReviewState.Failed)
                .ToList();
        }

        private async Task<bool> EnsurePythonClientReadyForDatasetAsync(string requestName, int timeoutMilliseconds = 5000)
        {
            if (isDetectionClientStarting)
            {
                AppLog.COMM($"{requestName} skipped because a YOLO connection request is already running.");
                return false;
            }

            isDetectionClientStarting = true;
            UpdateBatchDetectionControls();
            Cursor = Cursors.AppStarting;

            try
            {
                bool ready = await CGlobal.Inst.EnsurePythonModelClientReadyAsync(timeoutMilliseconds);
                if (!ready)
                {
                    PythonCommunicationStatus status = CGlobal.Inst.GetPythonCommunicationStatusSnapshot();
                    string error = !string.IsNullOrWhiteSpace(status.LastError)
                        ? status.LastError
                        : CGlobal.Inst.PythonClientProcess.LastError;
                    AppLog.ABNORMAL($"{requestName} skipped because YOLO client is not connected. Listener:{status.IsListening}, Client:{status.IsClientConnected}, Error:{(string.IsNullOrWhiteSpace(error) ? "none" : error)}");
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
                isDetectionClientStarting = false;
                Cursor = Cursors.Default;
                UpdateBatchDetectionControls();
            }
        }

        private int GetDetectionTimeoutSeconds()
        {
            CGlobal.Inst.Data.ProjectSettings?.EnsureDefaults();
            return Math.Clamp(
                CGlobal.Inst.Data.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? DefaultDetectionTimeoutSeconds,
                1,
                600);
        }

        private async void StartBatchDetection(IEnumerable<string> imagePaths, string scopeText)
        {
            if (isBatchDetectionRunning)
            {
                AppLog.COMM("Batch detection is already running.");
                return;
            }

            List<string> paths = (imagePaths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                AppLog.COMM($"Batch detection skipped because no images matched the scope: {scopeText}");
                return;
            }

            if (!await EnsurePythonClientReadyForDatasetAsync("Batch detection", 8000))
            {
                return;
            }

            batchDetectionQueue.Clear();
            foreach (string path in paths)
            {
                batchDetectionQueue.Enqueue(path);
            }

            isBatchDetectionRunning = true;
            batchDetectionCurrentPath = string.Empty;
            batchDetectionTotalCount = paths.Count;
            batchDetectionCompletedCount = 0;
            batchDetectionScopeText = scopeText ?? string.Empty;
            batchDetectionTimer?.Start();
            UpdateBatchDetectionControls();
            UpdateBatchStatusPanel();
            AppLog.NORMAL($"Batch detection started. Count:{paths.Count}, Scope:{batchDetectionScopeText}, Filter:{GetReviewFilterDisplayName(selectedReviewFilter)}");
            StartNextBatchDetectionItem();
        }

        private void StartNextBatchDetectionItem()
        {
            if (!isBatchDetectionRunning)
            {
                return;
            }

            if (batchDetectionQueue.Count == 0)
            {
                FinishBatchDetection();
                return;
            }

            string imagePath = batchDetectionQueue.Dequeue();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                batchDetectionCompletedCount++;
                StartNextBatchDetectionItem();
                return;
            }

            batchDetectionCurrentPath = imagePath;
            batchDetectionDeadlineUtc = DateTime.UtcNow.AddSeconds(GetDetectionTimeoutSeconds());
            UpdateBatchStatusPanel();
            SelectImageRowIfVisible(imagePath);
            LoadImageToMainDisplay(imagePath);

            YoloImageReviewStatus requested = imageReviewStatus.SetDetectionRequested(imagePath);
            ApplyStatusToVisibleRow(imagePath, requested);
            imageReviewStatus.SaveReviewStatus(CGlobal.Inst.Data);
            RefreshFilteredGridAfterStatusChange(imagePath);
            UpdateListStatusText();

            bool started = CGlobal.Inst.DetectionWorkflow.TryStartCurrentImageDetection(
                CGlobal.Inst.Data,
                CGlobal.Inst.DeepLearning,
                CGlobal.Inst.DetectionResults,
                () => true);

            if (!started)
            {
                MarkBatchDetectionFailed(imagePath, "Batch detection request failed.");
                StopBatchDetection(markCurrentFailed: false, reason: $"Batch detection request failed: {Path.GetFileName(imagePath)}");
            }
        }

        private void CompleteCurrentBatchDetectionItem(DetectionCandidatesUpdatedEventArgs e)
        {
            if (!isBatchDetectionRunning || string.IsNullOrWhiteSpace(batchDetectionCurrentPath))
            {
                return;
            }

            string resultPath = e?.ImagePath ?? string.Empty;
            if (!string.Equals(resultPath, batchDetectionCurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            batchDetectionCompletedCount++;
            batchDetectionCurrentPath = string.Empty;
            UpdateBatchStatusPanel();
            UpdateListStatusText();
            StartNextBatchDetectionItem();
        }

        private void FinishBatchDetection()
        {
            isBatchDetectionRunning = false;
            batchDetectionCurrentPath = string.Empty;
            batchDetectionQueue.Clear();
            batchDetectionTimer?.Stop();
            UpdateBatchDetectionControls();
            UpdateBatchStatusPanel();
            UpdateListStatusText();
            AppLog.NORMAL($"Batch detection completed. Count:{batchDetectionCompletedCount}/{batchDetectionTotalCount}");
            batchDetectionScopeText = string.Empty;
            UpdateBatchStatusPanel();
        }

        private void StopBatchDetection(bool markCurrentFailed, string reason)
        {
            if (!isBatchDetectionRunning)
            {
                return;
            }

            if (CGlobal.Inst.DeepLearning?.GetStatusSnapshot().IsClientConnected == true)
            {
                CGlobal.Inst.DeepLearning.Send(MvcVisionSystem._3._Communication.TCP.CCommunicationLearning.CommandLearning.StopDefect.ToString());
            }

            string currentPath = batchDetectionCurrentPath;
            CGlobal.Inst.DetectionResults.CancelPendingDetection();
            if (markCurrentFailed && !string.IsNullOrWhiteSpace(currentPath))
            {
                MarkBatchDetectionFailed(currentPath, reason);
            }

            isBatchDetectionRunning = false;
            batchDetectionCurrentPath = string.Empty;
            batchDetectionQueue.Clear();
            batchDetectionTimer?.Stop();
            UpdateBatchDetectionControls();
            UpdateBatchStatusPanel();
            UpdateListStatusText();
            batchDetectionScopeText = string.Empty;
            UpdateBatchStatusPanel();

            if (!string.IsNullOrWhiteSpace(reason))
            {
                AppLog.COMM(reason);
            }
        }

        private void MarkBatchDetectionFailed(string imagePath, string reason)
        {
            YoloImageReviewStatus failed = imageReviewStatus.SetDetectionFailed(imagePath, string.Empty, reason);
            ApplyStatusToVisibleRow(imagePath, failed);
            imageReviewStatus.SaveReviewStatus(CGlobal.Inst.Data);
            RefreshFilteredGridAfterStatusChange(imagePath);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                AppLog.ABNORMAL($"{reason} Image:{Path.GetFileName(imagePath)}");
            }
        }

        private void UpdateBatchDetectionControls()
        {
            bool idle = !isBatchDetectionRunning && !isDetectionClientStarting;

            if (btnDetectSelected != null)
            {
                btnDetectSelected.Enabled = idle;
            }

            if (btnOpenImageRoot != null)
            {
                btnOpenImageRoot.Enabled = idle;
            }

            if (btnBrowseImageFolder != null)
            {
                btnBrowseImageFolder.Enabled = idle;
            }

            if (btnNextUnlabeled != null)
            {
                btnNextUnlabeled.Enabled = idle;
            }

            if (btnDetectBatch != null)
            {
                btnDetectBatch.Enabled = idle;
            }

            if (btnStopBatchDetection != null)
            {
                btnStopBatchDetection.Enabled = isBatchDetectionRunning;
                btnStopBatchDetection.Visible = isBatchDetectionRunning;
            }

            if (btnReviewFilter != null)
            {
                btnReviewFilter.Enabled = idle;
            }

            UpdateBatchStatusPanel();
        }

        private void UpdateBatchStatusPanel()
        {
            if (batchStatusPanel == null || batchStatusTitleLabel == null || batchStatusDetailLabel == null)
            {
                return;
            }

            batchStatusPanel.Visible = isBatchDetectionRunning;
            if (!isBatchDetectionRunning)
            {
                batchStatusTitleLabel.Text = "일괄 검출";
                batchStatusDetailLabel.Text = string.Empty;
                SetBatchProgressFill(0);
                return;
            }

            int total = Math.Max(0, batchDetectionTotalCount);
            int completed = Math.Max(0, Math.Min(batchDetectionCompletedCount, total));
            int currentOrdinal = string.IsNullOrWhiteSpace(batchDetectionCurrentPath)
                ? completed
                : Math.Min(total, completed + 1);

            batchStatusTitleLabel.Text = total > 0
                ? $"{currentOrdinal}/{total}"
                : "일괄 검출";
            batchStatusDetailLabel.Text = BuildBatchStatusDetailText();
            SetBatchProgressFill(total == 0 ? 0 : completed / (double)total);
        }

        private string BuildBatchStatusDetailText()
        {
            if (!isBatchDetectionRunning)
            {
                return string.Empty;
            }

            string scope = string.IsNullOrWhiteSpace(batchDetectionScopeText)
                ? "표시 행"
                : batchDetectionScopeText;
            string currentImage = string.IsNullOrWhiteSpace(batchDetectionCurrentPath)
                ? "다음 이미지 준비"
                : Path.GetFileName(batchDetectionCurrentPath);
            int remaining = Math.Max(0, batchDetectionTotalCount - batchDetectionCompletedCount);
            return $"진행 중 · {scope} · 현재 {currentImage} · 남음 {remaining}";
        }

        private void SetBatchProgressFill(double ratio)
        {
            if (batchProgressTrack == null || batchProgressFill == null)
            {
                return;
            }

            double safeRatio = Math.Max(0D, Math.Min(1D, ratio));
            int width = (int)Math.Round(batchProgressTrack.ClientSize.Width * safeRatio);
            batchProgressFill.Width = Math.Max(0, Math.Min(batchProgressTrack.ClientSize.Width, width));
        }

        private void btnReviewFilter_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (isBatchDetectionRunning)
            {
                AppLog.COMM("Review filter cannot be changed while batch detection is running.");
                return;
            }

            if (!(e.ClickedItem?.Tag is ImageReviewFilter filter) || selectedReviewFilter == filter)
            {
                return;
            }

            selectedReviewFilter = filter;
            UpdateReviewFilterButtonText();
            ReloadImageGrid();
            LoadInitialImageToMainDisplay();
        }

        private void UpdateReviewFilterButtonText()
        {
            if (btnReviewFilter == null)
            {
                return;
            }

            bool compact = IsCompactImageListLayout();
            string displayName = GetReviewFilterDisplayName(selectedReviewFilter);
            btnReviewFilter.Text = compact ? displayName : $"상태 {displayName}";

            if (reviewFilterMenu == null)
            {
                return;
            }

            foreach (ToolStripItem item in reviewFilterMenu.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is ImageReviewFilter filter)
                {
                    menuItem.Checked = filter == selectedReviewFilter;
                }
            }
        }

        private void btnNextUnlabeled_Click(object sender, EventArgs e)
        {
            string currentImagePath = GetSelectedImageRow()?.Tag as string;
            if (string.IsNullOrWhiteSpace(currentImagePath))
            {
                currentImagePath = CGlobal.Inst.Data.LastSelectImagePath;
            }

            if (!imageReviewStatus.TryFindNextUnlabeled(currentImagePaths, currentImagePath, out string nextImagePath))
            {
                AppLog.NORMAL("No unlabeled image remains in the current image list.");
                return;
            }

            DataGridViewRow nextRow = FindImageRow(nextImagePath);
            if (nextRow != null)
            {
                imageGridView.ClearSelection();
                nextRow.Selected = true;
                imageGridView.CurrentCell = nextRow.Cells["NameColumn"];
            }

            LoadImageToMainDisplay(nextImagePath);
        }

        private DataGridViewRow GetSelectedImageRow()
        {
            if (imageGridView.SelectedRows.Count > 0)
            {
                return imageGridView.SelectedRows[0];
            }

            return imageGridView.CurrentRow;
        }

        private void SelectImageRowIfVisible(string imagePath)
        {
            DataGridViewRow row = FindImageRow(imagePath);
            if (row == null)
            {
                return;
            }

            imageGridView.ClearSelection();
            row.Selected = true;
            imageGridView.CurrentCell = row.Cells["NameColumn"];
        }

        private void ApplyStatusToVisibleRow(string imagePath, YoloImageReviewStatus status)
        {
            DataGridViewRow row = FindImageRow(imagePath);
            if (row != null)
            {
                ApplyStatusToRow(row, status);
            }
        }

        private bool LoadFolderPath(out string folderPath)
        {
            folderPath = "";
            try
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    if (string.IsNullOrWhiteSpace(lastPath))
                    {
                        CGlobal.Inst.Data.ProjectSettings?.EnsureDefaults();
                        string configuredImageRoot = CGlobal.Inst.Data.ProjectSettings?.PythonModel?.ImageRootPath;
                        if (Directory.Exists(configuredImageRoot))
                        {
                            lastPath = configuredImageRoot;
                        }
                    }

                    if (!string.IsNullOrEmpty(lastPath))
                    {
                        fbd.SelectedPath = lastPath;
                    }

                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        folderPath = fbd.SelectedPath;
                        lastPath = folderPath;
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

        private bool TryLoadConfiguredImageRoot()
        {
            CGlobal.Inst.Data.ProjectSettings?.EnsureDefaults();
            string imageRootPath = CGlobal.Inst.Data.ProjectSettings?.PythonModel?.ImageRootPath;
            if (string.IsNullOrWhiteSpace(imageRootPath) || !Directory.Exists(imageRootPath))
            {
                return false;
            }

            lastPath = imageRootPath;
            ShowImageDgv(GetImageFiles(imageRootPath));
            return true;
        }

        private async Task LoadConfiguredImageRootOnStartupAsync()
        {
            startupImageRootLoadCts?.Cancel();
            startupImageRootLoadCts?.Dispose();
            startupImageRootLoadCts = new CancellationTokenSource();
            CancellationToken token = startupImageRootLoadCts.Token;

            CGlobal.Inst.Data.ProjectSettings?.EnsureDefaults();
            string imageRootPath = CGlobal.Inst.Data.ProjectSettings?.PythonModel?.ImageRootPath;
            if (string.IsNullOrWhiteSpace(imageRootPath) || !Directory.Exists(imageRootPath))
            {
                return;
            }

            lastPath = imageRootPath;
            StatusLabel.Text = $"이미지 루트 스캔 중: {imageRootPath}";

            List<string> imagePaths;
            try
            {
                imagePaths = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    return GetImageFiles(imageRootPath);
                }, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Configured image root scan failed: {ex.Message}");
                return;
            }

            if (token.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            ShowImageDgvCore(imagePaths, loadInitialImage: false);
            this.UIThreadBeginInvoke(LoadInitialImageToMainDisplay);
        }

        private void ShowImageDgv(List<string> imagePaths)
        {
            ShowImageDgvCore(imagePaths, loadInitialImage: true);
        }

        private void ShowImageDgvCore(List<string> imagePaths, bool loadInitialImage)
        {
            currentImagePaths.Clear();
            if (imagePaths != null)
            {
                currentImagePaths.AddRange(imagePaths);
            }

            imageReviewStatus.SetImages(currentImagePaths);
            imageReviewStatus.LoadReviewStatus(CGlobal.Inst.Data, currentImagePaths);
            ReloadImageGrid();
            if (loadInitialImage)
            {
                LoadInitialImageToMainDisplay();
            }
        }

        private void LoadInitialImageToMainDisplay()
        {
            if (imageGridView.Rows.Count == 0)
            {
                return;
            }

            DataGridViewRow row = FindImageRow(CGlobal.Inst.Data.LastSelectImagePath) ?? imageGridView.Rows[0];
            string imagePath = row.Tag as string;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            imageGridView.ClearSelection();
            row.Selected = true;
            imageGridView.CurrentCell = row.Cells["NameColumn"];
            LoadImageToMainDisplay(imagePath);
        }

        public List<string> GetImageFiles(string folderPath)
        {
            string[] supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
            HashSet<string> supported = new HashSet<string>(supportedExtensions, StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(folderPath))
            {
                AppLog.ABNORMAL($"Image folder does not exist: {folderPath}");
                return new List<string>();
            }

            return Directory.EnumerateFiles(folderPath)
                .Where(file => supported.Contains(Path.GetExtension(file)))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ReloadImageGrid()
        {
            ConfigureImageGrid();
            ResetImageGridDetailLoading();
            List<string> visibleImagePaths = new List<string>();

            imageGridView.SuspendLayout();
            try
            {
                imageGridView.Rows.Clear();
                imageRowByPath.Clear();
                ClearThumbnails();
                imageGridView.RowTemplate.Height = thumbnailSize + 16;
                ApplyImageGridColumnLayout(Width < 520 || uiSplitContainer1.Panel1.ClientSize.Width < 520);

                foreach (string imagePath in currentImagePaths)
                {
                    if (!ShouldShowImagePath(imagePath))
                    {
                        continue;
                    }

                    AddImageRow(imagePath);
                    visibleImagePaths.Add(imagePath);
                }
            }
            finally
            {
                imageGridView.ResumeLayout();
            }

            UpdateListStatusText();
            StartImageGridDetailLoading(visibleImagePaths);
        }

        private void UpdateListStatusText()
        {
            int labeledCount = imageReviewStatus.GetLabeledCount();
            string statusText = BuildListStatusText(
                imageGridView.Rows.Count,
                currentImagePaths.Count,
                labeledCount,
                selectedReviewFilter,
                isBatchDetectionRunning,
                batchDetectionCompletedCount,
                batchDetectionTotalCount,
                batchDetectionScopeText);
            if (isImageGridDetailLoading && imageGridDetailTotalCount > 0)
            {
                statusText += $" / 썸네일 {Math.Max(0, imageGridDetailLoadedCount)}/{Math.Max(0, imageGridDetailTotalCount)}";
            }

            StatusLabel.Text = statusText;
            UpdateSelectedReviewStatusText();
        }

        private static string BuildListStatusText(
            int visibleCount,
            int totalCount,
            int labeledCount,
            ImageReviewFilter filter,
            bool isBatchRunning,
            int batchCompletedCount,
            int batchTotalCount,
            string batchScopeText)
        {
            string visibleText = filter == ImageReviewFilter.All
                ? $"이미지 {Math.Max(0, visibleCount)}"
                : $"이미지 {Math.Max(0, visibleCount)}/{Math.Max(0, totalCount)}";
            string filterText = filter == ImageReviewFilter.All
                ? string.Empty
                : $" / 필터 {GetReviewFilterDisplayName(filter)}";
            string batchText = isBatchRunning
                ? $" / 일괄 검출 {Math.Max(0, batchCompletedCount)}/{Math.Max(0, batchTotalCount)} {batchScopeText ?? string.Empty}".TrimEnd()
                : string.Empty;

            return $"{visibleText} / 라벨 {Math.Max(0, labeledCount)}{filterText}{batchText}";
        }

        private void UpdateSelectedReviewStatusText()
        {
            if (selectedReviewStatusLabel == null)
            {
                return;
            }

            DataGridViewRow row = GetSelectedImageRow();
            string imagePath = row?.Tag as string;
            YoloImageReviewStatus status = imageReviewStatus.GetOrCreate(imagePath);
            string detail = BuildSelectedReviewStatusText(status);
            selectedReviewStatusLabel.Text = detail;
            selectedReviewStatusLabel.ToolTipText = string.IsNullOrWhiteSpace(detail)
                ? string.Empty
                : detail;
        }

        private string BuildSelectedReviewStatusText(YoloImageReviewStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            string labelDetail = FormatLabelStatusDetailForDisplay(status.LabelText);
            string detectionDetail = FormatDetectionDetailForDisplay(status);
            return $"라벨: {labelDetail} / AI: {detectionDetail}";
        }

        private void System_OnDataUpdated()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(RefreshActiveImageLabelStatus);
                return;
            }

            RefreshActiveImageLabelStatus();
        }

        private void DetectionResults_DetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            void update() => RefreshDetectionCandidateStatus(e);
            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(update);
                return;
            }

            update();
        }

        private void RefreshActiveImageLabelStatus()
        {
            string activeImagePath = CGlobal.Inst.Data.LastSelectImagePath;
            if (string.IsNullOrWhiteSpace(activeImagePath) || imageGridView?.Rows == null)
            {
                return;
            }

            DataGridViewRow row = FindImageRow(activeImagePath);
            if (row != null)
            {
                bool hasActiveCandidates = CGlobal.Inst.DetectionResults
                    .GetLastCandidateReviewItems(CGlobal.Inst.Data)
                    .Any();
                YoloImageReviewStatus reviewStatus = imageReviewStatus.GetOrCreate(activeImagePath);
                Size? activeImageSize = GetMainDisplayImageSize();
                if (activeImageSize.HasValue)
                {
                    reviewStatus = imageReviewStatus.RefreshLabelStatusAndReviewState(
                        activeImagePath,
                        activeImageSize.Value,
                        CGlobal.Inst.Data,
                        hasActiveCandidates);
                }
                else if (File.Exists(activeImagePath))
                {
                    using (Bitmap image = AppImageLoader.LoadBitmap(activeImagePath))
                    {
                        reviewStatus = imageReviewStatus.RefreshLabelStatusAndReviewState(
                            activeImagePath,
                            image.Size,
                            CGlobal.Inst.Data,
                            hasActiveCandidates);
                    }
                }

                ApplyStatusToRow(row, reviewStatus);
                imageReviewStatus.SaveReviewStatus(CGlobal.Inst.Data);
                RefreshFilteredGridAfterStatusChange(activeImagePath);
                UpdateListStatusText();
                return;
            }
        }

        private static Size? GetMainDisplayImageSize()
        {
            Bitmap currentImage = CDisplayManager.GetMainDisplayOrNull()?.GetCurrentImage();
            return currentImage?.Size;
        }

        private void RefreshDetectionCandidateStatus(DetectionCandidatesUpdatedEventArgs e)
        {
            string targetPath = !string.IsNullOrWhiteSpace(e?.ImagePath) ? e.ImagePath : CGlobal.Inst.Data.LastSelectImagePath;
            string targetName = !string.IsNullOrWhiteSpace(e?.ImageName) ? e.ImageName : CGlobal.Inst.Data.LastSelectImageName;
            int candidateCount = e?.CandidateCount ?? 0;
            YoloImageReviewStatus status;
            switch (e?.Reason ?? DetectionCandidateUpdateReason.CandidatesChanged)
            {
                case DetectionCandidateUpdateReason.RequestStarted:
                    status = imageReviewStatus.SetDetectionRequested(targetPath, targetName);
                    break;
                case DetectionCandidateUpdateReason.ResultCompleted:
                    status = candidateCount > 0
                        ? imageReviewStatus.SetDetectionCandidates(targetPath, targetName, candidateCount)
                        : imageReviewStatus.SetDetectionNoCandidates(targetPath, targetName);
                    break;
                case DetectionCandidateUpdateReason.CandidatesChanged:
                    status = candidateCount > 0
                        ? imageReviewStatus.SetDetectionCandidates(targetPath, targetName, candidateCount)
                        : imageReviewStatus.ClearDetectionStatus(targetPath, targetName);
                    status = RefreshSavedLabelStateForDetectionEvent(status, hasActiveCandidates: candidateCount > 0);
                    break;
                case DetectionCandidateUpdateReason.CandidatesCleared:
                    status = imageReviewStatus.ClearDetectionStatus(targetPath, targetName);
                    status = RefreshSavedLabelStateForDetectionEvent(status, hasActiveCandidates: false);
                    break;
                case DetectionCandidateUpdateReason.CandidatesConfirmed:
                    status = imageReviewStatus.MarkConfirmed(targetPath, targetName);
                    status = RefreshSavedLabelStateForDetectionEvent(status, hasActiveCandidates: false);
                    break;
                case DetectionCandidateUpdateReason.CandidateSkipped:
                    status = imageReviewStatus.MarkSkipped(targetPath, targetName);
                    break;
                case DetectionCandidateUpdateReason.RequestTimedOut:
                    status = imageReviewStatus.SetDetectionFailed(targetPath, targetName, "Detection timed out.");
                    break;
                default:
                    status = candidateCount > 0
                        ? imageReviewStatus.SetDetectionCandidates(targetPath, targetName, candidateCount)
                        : imageReviewStatus.ClearDetectionStatus(targetPath, targetName);
                    break;
            }

            if (status == null)
            {
                return;
            }

            bool rowUpdated = false;
            DataGridViewRow row = FindImageRow(status.ImagePath);
            if (row != null)
            {
                ApplyStatusToRow(row, status);
                rowUpdated = true;
            }

            imageReviewStatus.SaveReviewStatus(CGlobal.Inst.Data);
            if (!rowUpdated || selectedReviewFilter != ImageReviewFilter.All)
            {
                RefreshFilteredGridAfterStatusChange(status.ImagePath);
            }
            else
            {
                UpdateListStatusText();
            }

            if (e?.Reason == DetectionCandidateUpdateReason.ResultCompleted)
            {
                CompleteCurrentBatchDetectionItem(e);
            }
        }

        private YoloImageReviewStatus RefreshSavedLabelStateForDetectionEvent(YoloImageReviewStatus status, bool hasActiveCandidates)
        {
            if (status == null || string.IsNullOrWhiteSpace(status.ImagePath) || !File.Exists(status.ImagePath))
            {
                return status;
            }

            try
            {
                using (Bitmap image = AppImageLoader.LoadBitmap(status.ImagePath))
                {
                    return imageReviewStatus.RefreshLabelStatusAndReviewState(
                        status.ImagePath,
                        image.Size,
                        CGlobal.Inst.Data,
                        hasActiveCandidates) ?? status;
                }
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Label status refresh failed after detection event. Image:{Path.GetFileName(status.ImagePath)}, Error:{ex.Message}");
                return status;
            }
        }

        private void UpdateImageRowLabelStatus(DataGridViewRow row)
        {
            string imagePath = row?.Tag as string;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            using (Bitmap image = AppImageLoader.LoadBitmap(imagePath))
            {
                ApplyStatusToRow(row, imageReviewStatus.RefreshLabelStatus(imagePath, image.Size, CGlobal.Inst.Data));
            }
        }

        private void RefreshFilteredGridAfterStatusChange(string preferredImagePath)
        {
            if (selectedReviewFilter == ImageReviewFilter.All)
            {
                UpdateListStatusText();
                return;
            }

            ReloadImageGrid();
            if (isBatchDetectionRunning)
            {
                UpdateListStatusText();
                return;
            }

            SelectVisibleImageAfterFilterChange(preferredImagePath);
        }

        private void SelectVisibleImageAfterFilterChange(string preferredImagePath)
        {
            DataGridViewRow row = FindImageRow(preferredImagePath);
            if (row == null && imageGridView.Rows.Count > 0)
            {
                row = imageGridView.Rows[0];
            }

            if (row == null)
            {
                return;
            }

            string imagePath = row.Tag as string;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            imageGridView.ClearSelection();
            row.Selected = true;
            imageGridView.CurrentCell = row.Cells["NameColumn"];
            LoadImageToMainDisplay(imagePath);
        }

        private bool ShouldShowImagePath(string imagePath)
        {
            if (selectedReviewFilter == ImageReviewFilter.All)
            {
                return true;
            }

            YoloImageReviewStatus status = imageReviewStatus.GetOrCreate(imagePath);
            if (RequiresFreshLabelStatus(selectedReviewFilter))
            {
                RefreshImageReviewStatusForFilter(imagePath);
                status = imageReviewStatus.GetOrCreate(imagePath);
            }

            return MatchesReviewFilter(status, selectedReviewFilter);
        }

        private static bool MatchesReviewFilter(YoloImageReviewStatus status, ImageReviewFilter filter)
        {
            if (filter == ImageReviewFilter.All)
            {
                return true;
            }

            if (status == null)
            {
                return false;
            }

            switch (filter)
            {
                case ImageReviewFilter.Unlabeled:
                    return !status.IsLabeled;
                case ImageReviewFilter.Requested:
                    return status.ReviewState == YoloImageReviewState.Requested;
                case ImageReviewFilter.Candidate:
                    return status.ReviewState == YoloImageReviewState.Candidate;
                case ImageReviewFilter.Confirmed:
                    return status.ReviewState == YoloImageReviewState.Confirmed;
                case ImageReviewFilter.Skipped:
                    return status.ReviewState == YoloImageReviewState.Skipped;
                case ImageReviewFilter.NoCandidate:
                    return status.ReviewState == YoloImageReviewState.NoCandidate;
                case ImageReviewFilter.Failed:
                    return status.ReviewState == YoloImageReviewState.Failed;
                default:
                    return true;
            }
        }

        private static bool RequiresFreshLabelStatus(ImageReviewFilter filter)
        {
            return filter == ImageReviewFilter.Unlabeled || filter == ImageReviewFilter.Confirmed;
        }

        private void RefreshImageReviewStatusForFilter(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            try
            {
                using (Bitmap image = AppImageLoader.LoadBitmap(imagePath))
                {
                    imageReviewStatus.RefreshLabelStatus(imagePath, image.Size, CGlobal.Inst.Data);
                }
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Failed to refresh review status for '{imagePath}': {ex.Message}");
            }
        }

        private void AddImageRow(string imagePath)
        {
            try
            {
                ImageGridItem item = CreateImageGridShellItem(imagePath);

                int rowIndex = imageGridView.Rows.Add(
                    item.Thumbnail,
                    item.Name,
                    item.LabelStatus,
                    item.DetectStatus,
                    item.Dimensions,
                    item.FileSize,
                    item.FolderName,
                    item.Modified);

                DataGridViewRow row = imageGridView.Rows[rowIndex];
                row.Tag = imagePath;
                imageRowByPath[imagePath] = row;
                row.Height = thumbnailSize + 16;
                ApplyImageGridRowVisualStyle(row);
                ApplyStatusToRow(row, imageReviewStatus.GetOrCreate(imagePath));
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Failed to load image list item '{imagePath}': {ex.Message}");
            }
        }

        private ImageGridItem CreateImageGridItem(string imagePath)
        {
            FileInfo fileInfo = new FileInfo(imagePath);
            using (Bitmap image = AppImageLoader.LoadBitmap(imagePath))
            {
                return new ImageGridItem
                {
                    Thumbnail = CreateThumbnail(image, thumbnailSize),
                    Name = Path.GetFileName(imagePath),
                    LabelStatus = "확인중",
                    DetectStatus = string.Empty,
                    Dimensions = $"{image.Width}x{image.Height}",
                    FileSize = FormatFileSize(fileInfo.Length),
                    FolderName = fileInfo.Directory?.Name ?? string.Empty,
                    Modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    ImageSize = image.Size
                };
            }
        }

        private ImageGridItem CreateImageGridShellItem(string imagePath)
        {
            FileInfo fileInfo = new FileInfo(imagePath);
            return new ImageGridItem
            {
                Thumbnail = null,
                Name = Path.GetFileName(imagePath),
                LabelStatus = "확인중",
                DetectStatus = string.Empty,
                Dimensions = string.Empty,
                FileSize = FormatFileSize(fileInfo.Length),
                FolderName = fileInfo.Directory?.Name ?? string.Empty,
                Modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                ImageSize = Size.Empty
            };
        }

        private void ResetImageGridDetailLoading()
        {
            imageGridDetailLoadCts?.Cancel();
            imageGridDetailLoadCts = new CancellationTokenSource();
            isImageGridDetailLoading = false;
            imageGridDetailTotalCount = 0;
            imageGridDetailLoadedCount = 0;
        }

        private void StartImageGridDetailLoading(List<string> imagePaths)
        {
            if (imagePaths == null || imagePaths.Count == 0 || imageGridDetailLoadCts == null)
            {
                return;
            }

            isImageGridDetailLoading = true;
            imageGridDetailTotalCount = imagePaths.Count;
            imageGridDetailLoadedCount = 0;
            if (!isImageGridDetailLoading
                || imageGridDetailLoadedCount == 1
                || imageGridDetailLoadedCount % 10 == 0)
            {
                UpdateListStatusText();
            }

            CancellationToken token = imageGridDetailLoadCts.Token;
            _ = LoadImageGridDetailsAsync(imagePaths, token);
        }

        private async Task LoadImageGridDetailsAsync(List<string> imagePaths, CancellationToken token)
        {
            try
            {
                foreach (string imagePath in imagePaths)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        ImageGridItem item = await Task.Run(() => CreateImageGridItem(imagePath), token).ConfigureAwait(false);
                        if (token.IsCancellationRequested)
                        {
                            item.Thumbnail?.Dispose();
                            return;
                        }

                        ApplyImageGridItemOnUiThread(imagePath, item, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        AppLog.ABNORMAL($"Image grid detail loading failed for '{Path.GetFileName(imagePath)}': {ex.Message}");
                        ReportImageGridDetailProgressOnUiThread(token);
                    }
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Image grid detail loading failed: {ex.Message}");
                CompleteImageGridDetailLoadingOnUiThread(token);
            }
        }

        private void ApplyImageGridItemOnUiThread(string imagePath, ImageGridItem item, CancellationToken token)
        {
            if (item == null)
            {
                return;
            }

            if (IsDisposed || !IsHandleCreated)
            {
                item.Thumbnail?.Dispose();
                return;
            }

            try
            {
                if (!this.UIThreadBeginInvoke(() => ApplyImageGridItem(imagePath, item, token)))
                {
                    item.Thumbnail?.Dispose();
                }
            }
            catch
            {
                item.Thumbnail?.Dispose();
            }
        }

        private void ApplyImageGridItem(string imagePath, ImageGridItem item, CancellationToken token)
        {
            if (token.IsCancellationRequested || item == null)
            {
                item?.Thumbnail?.Dispose();
                return;
            }

            DataGridViewRow row = FindImageRow(imagePath);
            if (row == null)
            {
                item.Thumbnail?.Dispose();
                AdvanceImageGridDetailProgress(token);
                return;
            }

            if (thumbnailCache.TryGetValue(imagePath, out Image oldThumbnail))
            {
                oldThumbnail.Dispose();
            }

            if (item.Thumbnail != null)
            {
                thumbnailCache[imagePath] = item.Thumbnail;
            }

            row.Cells["ThumbnailColumn"].Value = item.Thumbnail;
            row.Cells["NameColumn"].Value = item.Name;
            row.Cells["DimensionsColumn"].Value = item.Dimensions;
            row.Cells["FileSizeColumn"].Value = item.FileSize;
            row.Cells["FolderColumn"].Value = item.FolderName;
            row.Cells["ModifiedColumn"].Value = item.Modified;

            YoloImageReviewStatus status = imageReviewStatus.RefreshLabelStatus(imagePath, item.ImageSize, CGlobal.Inst.Data);
            ApplyStatusToRow(row, status);
            AdvanceImageGridDetailProgress(token);
        }

        private void ReportImageGridDetailProgressOnUiThread(CancellationToken token)
        {
            if (token.IsCancellationRequested || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                this.UIThreadBeginInvoke(() => AdvanceImageGridDetailProgress(token));
            }
            catch
            {
            }
        }

        private void CompleteImageGridDetailLoadingOnUiThread(CancellationToken token)
        {
            if (token.IsCancellationRequested || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                this.UIThreadBeginInvoke(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    imageGridDetailLoadedCount = imageGridDetailTotalCount;
                    isImageGridDetailLoading = false;
                    UpdateListStatusText();
                });
            }
            catch
            {
            }
        }

        private void AdvanceImageGridDetailProgress(CancellationToken token)
        {
            if (token.IsCancellationRequested || !isImageGridDetailLoading || imageGridDetailTotalCount <= 0)
            {
                return;
            }

            imageGridDetailLoadedCount = Math.Min(imageGridDetailTotalCount, imageGridDetailLoadedCount + 1);
            if (imageGridDetailLoadedCount >= imageGridDetailTotalCount)
            {
                isImageGridDetailLoading = false;
            }

            UpdateListStatusText();
        }

        private void ApplyStatusToRow(DataGridViewRow row, YoloImageReviewStatus status)
        {
            if (row == null || status == null)
            {
                return;
            }

            string labelText = FormatLabelStatusForGrid(status.LabelText);
            string labelDetail = FormatLabelStatusDetailForDisplay(status.LabelText);
            string detectionText = FormatDetectionStatusForGrid(status);
            string detectionDetail = FormatDetectionDetailForDisplay(status);

            row.Cells["LabelStatusColumn"].Value = labelText;
            row.Cells["LabelStatusColumn"].ToolTipText = $"라벨: {labelDetail}";
            row.Cells["DetectStatusColumn"].Value = detectionText;
            row.Cells["DetectStatusColumn"].ToolTipText = $"AI: {detectionDetail}";
            row.Cells["NameColumn"].ToolTipText = BuildImageRowStatusToolTip(row, labelDetail, detectionDetail);
            ApplyStatusCellStyle(row, status);
            if (row.Selected || row == imageGridView?.CurrentRow)
            {
                UpdateSelectedReviewStatusText();
            }
        }

        private string BuildImageRowStatusToolTip(DataGridViewRow row, string labelDetail, string detectionDetail)
        {
            string fileName = row?.Cells["NameColumn"]?.Value?.ToString();
            List<string> lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                lines.Add(fileName);
            }

            lines.Add($"라벨: {labelDetail}");
            lines.Add($"AI: {detectionDetail}");
            return string.Join(Environment.NewLine, lines);
        }

        private void ApplyStatusCellStyle(DataGridViewRow row, YoloImageReviewStatus status)
        {
            if (row == null || status == null)
            {
                return;
            }

            DataGridViewCell labelCell = row.Cells["LabelStatusColumn"];
            labelCell.Style.BackColor = status.IsLabeled
                ? Color.FromArgb(32, 62, 44)
                : Color.FromArgb(42, 47, 55);
            labelCell.Style.ForeColor = status.IsLabeled
                ? Color.FromArgb(183, 238, 194)
                : Color.FromArgb(204, 214, 228);
            labelCell.Style.SelectionBackColor = status.IsLabeled
                ? Color.FromArgb(52, 103, 71)
                : Color.FromArgb(64, 74, 90);
            labelCell.Style.SelectionForeColor = Color.White;

            DataGridViewCell detectionCell = row.Cells["DetectStatusColumn"];
            string detectionText = status.DetectionText ?? string.Empty;
            if (detectionText.StartsWith("Candidate ", StringComparison.OrdinalIgnoreCase))
            {
                detectionCell.Style.BackColor = Color.FromArgb(34, 58, 92);
                detectionCell.Style.ForeColor = Color.FromArgb(212, 232, 255);
                detectionCell.Style.SelectionBackColor = Color.FromArgb(57, 94, 154);
                detectionCell.Style.SelectionForeColor = Color.White;
            }
            else if (string.Equals(detectionText, "Requested", StringComparison.OrdinalIgnoreCase))
            {
                detectionCell.Style.BackColor = Color.FromArgb(70, 62, 36);
                detectionCell.Style.ForeColor = Color.FromArgb(255, 230, 166);
                detectionCell.Style.SelectionBackColor = Color.FromArgb(105, 89, 43);
                detectionCell.Style.SelectionForeColor = Color.White;
            }
            else if (string.Equals(detectionText, "No Candidate", StringComparison.OrdinalIgnoreCase))
            {
                detectionCell.Style.BackColor = Color.FromArgb(32, 38, 48);
                detectionCell.Style.ForeColor = Color.FromArgb(170, 184, 205);
                detectionCell.Style.SelectionBackColor = Color.FromArgb(54, 66, 86);
                detectionCell.Style.SelectionForeColor = Color.White;
            }
            else if (string.Equals(detectionText, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                detectionCell.Style.BackColor = Color.FromArgb(74, 40, 40);
                detectionCell.Style.ForeColor = Color.FromArgb(255, 197, 197);
                detectionCell.Style.SelectionBackColor = Color.FromArgb(112, 54, 54);
                detectionCell.Style.SelectionForeColor = Color.White;
            }
            else if (string.Equals(detectionText, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                detectionCell.Style.BackColor = Color.FromArgb(36, 72, 50);
                detectionCell.Style.ForeColor = Color.FromArgb(183, 238, 194);
                detectionCell.Style.SelectionBackColor = Color.FromArgb(52, 103, 71);
                detectionCell.Style.SelectionForeColor = Color.White;
            }
            else if (string.Equals(detectionText, "Skipped", StringComparison.OrdinalIgnoreCase))
            {
                detectionCell.Style.BackColor = Color.FromArgb(56, 48, 38);
                detectionCell.Style.ForeColor = Color.FromArgb(232, 208, 170);
                detectionCell.Style.SelectionBackColor = Color.FromArgb(83, 70, 52);
                detectionCell.Style.SelectionForeColor = Color.White;
            }
            else
            {
                detectionCell.Style.BackColor = row.DefaultCellStyle.BackColor;
                detectionCell.Style.ForeColor = row.DefaultCellStyle.ForeColor;
                detectionCell.Style.SelectionBackColor = row.DefaultCellStyle.SelectionBackColor;
                detectionCell.Style.SelectionForeColor = row.DefaultCellStyle.SelectionForeColor;
            }
        }

        private void RefreshGridStatusTextFormatting()
        {
            if (!imageGridConfigured || imageGridView?.Rows == null)
            {
                return;
            }

            foreach (DataGridViewRow row in imageGridView.Rows)
            {
                ApplyStatusToRow(row, imageReviewStatus.GetOrCreate(row.Tag as string));
            }
        }

        private string FormatLabelStatusForGrid(string labelText)
        {
            bool compact = IsCompactImageListLayout();
            if (string.Equals(labelText, "No Label", StringComparison.OrdinalIgnoreCase))
            {
                return compact ? "없음" : "라벨 없음";
            }

            if (string.Equals(labelText, "Empty Label", StringComparison.OrdinalIgnoreCase))
            {
                return compact ? "빈값" : "빈 라벨";
            }

            if (labelText.StartsWith("Label ", StringComparison.OrdinalIgnoreCase))
            {
                string count = labelText.Substring("Label ".Length).Split('/')[0].Trim();
                string invalidPart = ExtractInvalidCount(labelText);
                if (compact)
                {
                    return string.IsNullOrWhiteSpace(invalidPart)
                        ? $"{count}개"
                        : $"{count}개 / 오류 {invalidPart}";
                }

                return string.IsNullOrWhiteSpace(invalidPart)
                    ? $"라벨 {count}개"
                    : $"라벨 {count}개 / 오류 {invalidPart}";
            }

            if (labelText.StartsWith("Invalid ", StringComparison.OrdinalIgnoreCase))
            {
                return $"오류 {labelText.Substring("Invalid ".Length).Trim()}";
            }

            return labelText;
        }

        private string FormatLabelStatusDetailForDisplay(string labelText)
        {
            if (string.Equals(labelText, "No Label", StringComparison.OrdinalIgnoreCase))
            {
                return "라벨 없음";
            }

            if (string.Equals(labelText, "Empty Label", StringComparison.OrdinalIgnoreCase))
            {
                return "빈 라벨 파일";
            }

            if (labelText.StartsWith("Label ", StringComparison.OrdinalIgnoreCase))
            {
                string count = labelText.Substring("Label ".Length).Split('/')[0].Trim();
                string invalidPart = ExtractInvalidCount(labelText);
                return string.IsNullOrWhiteSpace(invalidPart)
                    ? $"라벨 {count}개"
                    : $"라벨 {count}개 / 오류 {invalidPart}개";
            }

            if (labelText.StartsWith("Invalid ", StringComparison.OrdinalIgnoreCase))
            {
                return $"라벨 오류 {labelText.Substring("Invalid ".Length).Trim()}개";
            }

            return string.IsNullOrWhiteSpace(labelText) ? "라벨 상태 없음" : labelText;
        }

        private static string ExtractInvalidCount(string labelText)
        {
            const string invalidToken = "Invalid ";
            int index = labelText?.IndexOf(invalidToken, StringComparison.OrdinalIgnoreCase) ?? -1;
            return index < 0 ? string.Empty : labelText.Substring(index + invalidToken.Length).Trim();
        }

        private string FormatDetectionStatusForGrid(YoloImageReviewStatus status)
        {
            string detectionText = status?.DetectionText ?? string.Empty;
            bool compact = IsCompactImageListLayout();

            if (detectionText.StartsWith("Candidate ", StringComparison.OrdinalIgnoreCase))
            {
                string count = detectionText.Substring("Candidate ".Length).Trim();
                return compact ? $"후보 {count}" : $"AI 후보 {count}";
            }

            if (string.Equals(detectionText, "Requested", StringComparison.OrdinalIgnoreCase))
            {
                if (status?.DetectionAttemptCount > 1)
                {
                    return $"요청중 x{status.DetectionAttemptCount}";
                }

                return "요청중";
            }

            if (string.Equals(detectionText, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                if (status?.DetectionAttemptCount > 1)
                {
                    return $"실패 x{status.DetectionAttemptCount}";
                }

                return "실패";
            }

            if (string.Equals(detectionText, "No Candidate", StringComparison.OrdinalIgnoreCase))
            {
                return compact ? "없음" : "후보 없음";
            }

            if (string.Equals(detectionText, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return "확정";
            }

            if (string.Equals(detectionText, "Skipped", StringComparison.OrdinalIgnoreCase))
            {
                return "스킵";
            }

            return string.IsNullOrWhiteSpace(detectionText)
                ? (compact ? "대기" : "검출 전")
                : detectionText;
        }

        private string FormatDetectionDetailForDisplay(YoloImageReviewStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            List<string> details = new List<string>();
            string stateText = FormatDetectionStatusForGrid(status);
            if (!string.IsNullOrWhiteSpace(stateText))
            {
                details.Add(stateText);
            }

            if (status.DetectionAttemptCount > 0)
            {
                details.Add($"시도 {status.DetectionAttemptCount}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastDetectionMessage))
            {
                details.Add(TranslateDetectionMessage(status.LastDetectionMessage));
            }

            return details.Count > 0 ? string.Join(" / ", details) : "검출 전";
        }

        private static string TranslateDetectionMessage(string message)
        {
            if (string.Equals(message, "Detection requested.", StringComparison.OrdinalIgnoreCase))
            {
                return "검출 요청됨";
            }

            if (string.Equals(message, "Detection failed.", StringComparison.OrdinalIgnoreCase))
            {
                return "검출 실패";
            }

            if (string.Equals(message, "Detection timed out.", StringComparison.OrdinalIgnoreCase))
            {
                return "검출 시간 초과";
            }

            if (string.Equals(message, "No candidates found.", StringComparison.OrdinalIgnoreCase))
            {
                return "검출 후보 없음";
            }

            if (string.Equals(message, "Candidates confirmed.", StringComparison.OrdinalIgnoreCase))
            {
                return "후보 확정됨";
            }

            if (string.Equals(message, "Candidate skipped.", StringComparison.OrdinalIgnoreCase))
            {
                return "후보 스킵됨";
            }

            if (message.StartsWith("Candidates found: ", StringComparison.OrdinalIgnoreCase))
            {
                return $"후보 {message.Substring("Candidates found: ".Length).Trim()}개 발견";
            }

            return message;
        }

        private static string GetReviewFilterDisplayName(ImageReviewFilter filter)
        {
            switch (filter)
            {
                case ImageReviewFilter.Unlabeled:
                    return "미라벨";
                case ImageReviewFilter.Requested:
                    return "요청중";
                case ImageReviewFilter.Candidate:
                    return "후보";
                case ImageReviewFilter.Confirmed:
                    return "확정";
                case ImageReviewFilter.Skipped:
                    return "스킵";
                case ImageReviewFilter.NoCandidate:
                    return "검출없음";
                case ImageReviewFilter.Failed:
                    return "실패";
                default:
                    return "전체";
            }
        }

        private bool IsCompactImageListLayout()
        {
            return Width < 520 || (uiSplitContainer1 != null && uiSplitContainer1.Panel1.ClientSize.Width < 520);
        }

        private DataGridViewRow FindImageRow(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || imageGridView?.Rows == null)
            {
                return null;
            }

            if (imageRowByPath.TryGetValue(imagePath, out DataGridViewRow cachedRow)
                && cachedRow != null
                && !cachedRow.IsNewRow
                && cachedRow.DataGridView == imageGridView
                && string.Equals(cachedRow.Tag as string, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                return cachedRow;
            }

            foreach (DataGridViewRow row in imageGridView.Rows)
            {
                if (string.Equals(row.Tag as string, imagePath, StringComparison.OrdinalIgnoreCase))
                {
                    imageRowByPath[imagePath] = row;
                    return row;
                }
            }

            imageRowByPath.Remove(imagePath);
            return null;
        }

        private static Image CreateThumbnail(Image source, int maxSize)
        {
            Size thumbnailBounds = GetThumbnailBounds(source.Size, maxSize);
            return source.GetThumbnailImage(thumbnailBounds.Width, thumbnailBounds.Height, () => false, IntPtr.Zero);
        }

        internal static Size GetThumbnailBounds(Size sourceSize, int maxSize)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
            {
                return new Size(maxSize, maxSize);
            }

            float scale = Math.Min(maxSize / (float)sourceSize.Width, maxSize / (float)sourceSize.Height);
            int width = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
            int height = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
            return new Size(width, height);
        }

        internal static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            double value = bytes / 1024D;
            if (value < 1024)
            {
                return $"{value:0.#} KB";
            }

            return $"{value / 1024D:0.#} MB";
        }

        private void SetThumbnailSize(int size)
        {
            if (thumbnailSize == size)
            {
                return;
            }

            thumbnailSize = size;
            ReloadImageGrid();
        }

        private void x96ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetThumbnailSize(96);
        }

        private void x120ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetThumbnailSize(120);
        }

        private void x200ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetThumbnailSize(200);
        }

        private void imageGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= imageGridView.Rows.Count)
            {
                return;
            }

            string imagePath = imageGridView.Rows[e.RowIndex].Tag as string;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            LoadImageToMainDisplay(imagePath);
            UpdateSelectedReviewStatusText();
        }

        private void imageGridView_SelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectedReviewStatusText();
        }

        private static void LoadImageToMainDisplay(string imagePath)
        {
            string imageName = Path.GetFileNameWithoutExtension(imagePath);
            using (Bitmap bitmap = AppImageLoader.LoadBitmap(imagePath))
            using (Bitmap displayBitmap = new Bitmap(bitmap))
            {
                CGlobal.Inst.Data.LastSelectImageName = imageName;
                CGlobal.Inst.Data.LastSelectImagePath = imagePath;
                CDisplayManager.ImageSrc = CImageConverter.ToMat(bitmap);
                CDisplayManager.CreateLayerDisplay(displayBitmap, "Main", false);
                FormLayerDisplay mainDisplay = CDisplayManager.GetMainDisplayOrNull();
                if (mainDisplay?.GetCurrentImage() != null)
                {
                    CGlobal.Inst.ImageWorkspace.SetActiveImage(imageName, imagePath, mainDisplay.GetCurrentImage());
                }

                CGlobal.Inst.LabelingWorkflow.LoadSavedAnnotationsToMainDisplay(imagePath, bitmap.Size, CGlobal.Inst.Data);
                CDisplayManager.ZoomToFit("Main");
                CGlobal.Inst.System.UpdateData();
            }
        }

        private void DisposeImageListResources()
        {
            Resize -= ResizeEvent;
            if (btnBrowseImageFolder != null)
            {
                btnBrowseImageFolder.Click -= btnOpenFolder_Click;
            }

            if (btnOpenImageRoot != null)
            {
                btnOpenImageRoot.Click -= btnOpenImageRoot_Click;
            }

            if (btnDetectSelected != null)
            {
                btnDetectSelected.Click -= btnDetectSelected_Click;
            }

            if (btnDetectBatch != null)
            {
                btnDetectBatch.Click -= btnDetectBatch_ButtonClick;
                btnDetectBatch.MouseUp -= btnDetectBatch_MouseUp;
            }

            if (btnStopBatchDetection != null)
            {
                btnStopBatchDetection.Click -= btnStopBatchDetection_Click;
            }

            if (btnNextUnlabeled != null)
            {
                btnNextUnlabeled.Click -= btnNextUnlabeled_Click;
            }

            if (btnReviewFilter != null)
            {
                btnReviewFilter.Click -= btnReviewFilter_Click;
            }

            if (btnThumbnailSize != null)
            {
                btnThumbnailSize.Click -= btnThumbnailSize_Click;
            }

            if (batchDetectionMenu != null)
            {
                batchDetectionMenu.ItemClicked -= btnDetectBatch_DropDownItemClicked;
                batchDetectionMenu.Dispose();
                batchDetectionMenu = null;
            }

            if (reviewFilterMenu != null)
            {
                reviewFilterMenu.ItemClicked -= btnReviewFilter_DropDownItemClicked;
                reviewFilterMenu.Dispose();
                reviewFilterMenu = null;
            }

            if (thumbnailSizeMenu != null)
            {
                foreach (ToolStripItem item in thumbnailSizeMenu.Items)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        menuItem.Click -= thumbnailSizeMenuItem_Click;
                    }
                }

                thumbnailSizeMenu.Dispose();
                thumbnailSizeMenu = null;
            }

            if (datasetCommandToolTip != null)
            {
                datasetCommandToolTip.Dispose();
                datasetCommandToolTip = null;
            }

            if (batchDetectionTimer != null)
            {
                batchDetectionTimer.Stop();
                batchDetectionTimer.Tick -= batchDetectionTimer_Tick;
                batchDetectionTimer.Dispose();
                batchDetectionTimer = null;
            }

            if (batchProgressTrack != null)
            {
                batchProgressTrack.SizeChanged -= batchProgressTrack_SizeChanged;
            }

            CGlobal.Inst.System.OnDataUpdated -= System_OnDataUpdated;
            CGlobal.Inst.DetectionResults.DetectionCandidatesUpdated -= DetectionResults_DetectionCandidatesUpdated;
            if (imageGridView != null)
            {
                imageGridView.SelectionChanged -= imageGridView_SelectionChanged;
            }

            imageGridDetailLoadCts?.Cancel();
            imageGridDetailLoadCts?.Dispose();
            imageGridDetailLoadCts = null;
            startupImageRootLoadCts?.Cancel();
            startupImageRootLoadCts?.Dispose();
            startupImageRootLoadCts = null;
            isImageGridDetailLoading = false;
            imageGridDetailTotalCount = 0;
            imageGridDetailLoadedCount = 0;
            imageGridView?.Rows.Clear();
            imageRowByPath.Clear();
            ClearThumbnails();
        }

        private void ClearThumbnails()
        {
            foreach (Image thumbnail in thumbnailCache.Values)
            {
                thumbnail?.Dispose();
            }

            thumbnailCache.Clear();
        }

        private sealed class ImageGridItem
        {
            public Image Thumbnail { get; set; }
            public string Name { get; set; }
            public string LabelStatus { get; set; }
            public string DetectStatus { get; set; }
            public string Dimensions { get; set; }
            public string FileSize { get; set; }
            public string FolderName { get; set; }
            public string Modified { get; set; }
            public Size ImageSize { get; set; }
        }

        private enum ImageReviewFilter
        {
            All,
            Unlabeled,
            Requested,
            Candidate,
            Confirmed,
            Skipped,
            NoCandidate,
            Failed
        }

        private enum BatchDetectionMode
        {
            VisibleRows,
            FailedRows
        }
    }
}
