using Lib.Common;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using RJCodeUI_M1.RJControls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using RJForms = RJCodeUI_M1.RJForms;

namespace MvcVisionSystem
{
    public sealed class FormTrainingPanel : DockContent
    {
        private readonly CGlobal Global = CGlobal.Inst;
        private readonly TableLayoutPanel layout;
        private readonly Panel commandPanel;
        private readonly Label lblTitle;
        private readonly Label lblSubtitle;
        private readonly Label lblDatasetState;
        private readonly Label lblDatasetDetail;
        private readonly Label lblPythonState;
        private readonly Label lblPythonDetail;
        private readonly Label lblIssueList;
        private readonly RJButton btnRefresh;
        private readonly RJButton btnHealthCheck;
        private readonly RJButton btnModelStatus;
        private readonly RJButton btnRestartPython;
        private readonly RJButton btnStopPython;
        private readonly RJButton btnOpenModelSettings;
        private readonly System.Windows.Forms.Timer statusRefreshTimer;
        private bool workerCommandInProgress;
        private bool cleanupCompleted;

        public FormTrainingPanel()
        {
            Text = "학습 준비";
            TabText = Text;
            ToolTipText = "데이터셋 준비 상태와 Python Worker 상태를 확인합니다.";
            CloseButton = false;
            CloseButtonVisible = false;
            DockAreas = DockAreas.DockRight;
            BackColor = LabelingWorkbenchPalette.Panel;

            layout = new TableLayoutPanel
            {
                BackColor = LabelingWorkbenchPalette.Panel,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel header = CreatePanel(LabelingWorkbenchPalette.PanelHeader, new Padding(12, 10, 12, 8));
            lblTitle = CreateLabel("학습 준비", 12.5F, FontStyle.Bold, Color.White);
            lblSubtitle = CreateLabel("데이터셋 / 클래스 / Python Worker 상태", 8.25F, FontStyle.Regular, LabelingWorkbenchPalette.MutedText);
            header.Controls.Add(lblSubtitle);
            header.Controls.Add(lblTitle);

            Panel datasetPanel = CreateSection("데이터셋", out lblDatasetState, out lblDatasetDetail);
            Panel pythonPanel = CreateSection("Python Worker / 모델", out lblPythonState, out lblPythonDetail);

            commandPanel = CreatePanel(LabelingWorkbenchPalette.Panel, new Padding(9, 8, 9, 8));
            btnRefresh = CreateButton("새로고침", FontAwesome.Sharp.IconChar.SyncAlt, LabelingWorkbenchPalette.Selection);
            btnHealthCheck = CreateButton("진단", FontAwesome.Sharp.IconChar.Exclamation, LabelingWorkbenchPalette.Selection);
            btnModelStatus = CreateButton("모델", FontAwesome.Sharp.IconChar.Cog, LabelingWorkbenchPalette.Selection);
            btnRestartPython = CreateButton("재시작", FontAwesome.Sharp.IconChar.PowerOff, LabelingWorkbenchPalette.SurfaceAlt);
            btnStopPython = CreateButton("종료", FontAwesome.Sharp.IconChar.TimesCircle, Color.FromArgb(114, 75, 82));
            btnOpenModelSettings = CreateButton("설정", FontAwesome.Sharp.IconChar.Tools, LabelingWorkbenchPalette.SurfaceAlt);
            btnRefresh.Click += btnRefresh_Click;
            btnHealthCheck.Click += btnHealthCheck_Click;
            btnModelStatus.Click += btnModelStatus_Click;
            btnRestartPython.Click += btnRestartPython_Click;
            btnStopPython.Click += btnStopPython_Click;
            btnOpenModelSettings.Click += btnOpenModelSettings_Click;
            commandPanel.Controls.Add(btnRefresh);
            commandPanel.Controls.Add(btnHealthCheck);
            commandPanel.Controls.Add(btnModelStatus);
            commandPanel.Controls.Add(btnRestartPython);
            commandPanel.Controls.Add(btnStopPython);
            commandPanel.Controls.Add(btnOpenModelSettings);

            Panel issuePanel = CreatePanel(LabelingWorkbenchPalette.Surface, new Padding(12, 10, 12, 10));
            lblIssueList = CreateLabel("데이터셋 상태를 계산 중입니다.", 8.5F, FontStyle.Regular, LabelingWorkbenchPalette.MutedText);
            lblIssueList.Dock = DockStyle.Fill;
            lblIssueList.TextAlign = ContentAlignment.TopLeft;
            issuePanel.Controls.Add(lblIssueList);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(datasetPanel, 0, 1);
            layout.Controls.Add(pythonPanel, 0, 2);
            layout.Controls.Add(commandPanel, 0, 3);
            layout.Controls.Add(issuePanel, 0, 4);
            Controls.Add(layout);

            statusRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            statusRefreshTimer.Tick += statusRefreshTimer_Tick;
            statusRefreshTimer.Start();

            Resize += FormTrainingPanel_Resize;
            FormClosed += FormTrainingPanel_FormClosed;
            Global.System.OnDataUpdated += System_OnDataUpdated;
            Global.DetectionResults.DetectionCandidatesUpdated += DetectionResults_DetectionCandidatesUpdated;

            ApplyResponsiveLayout();
            RefreshStatus();
        }

        private void FormTrainingPanel_FormClosed(object sender, FormClosedEventArgs e)
        {
            Cleanup();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cleanup();
            }

            base.Dispose(disposing);
        }

        private void Cleanup()
        {
            if (cleanupCompleted)
            {
                return;
            }

            cleanupCompleted = true;
            statusRefreshTimer.Stop();
            statusRefreshTimer.Tick -= statusRefreshTimer_Tick;
            statusRefreshTimer.Dispose();
            Resize -= FormTrainingPanel_Resize;
            btnRefresh.Click -= btnRefresh_Click;
            btnHealthCheck.Click -= btnHealthCheck_Click;
            btnModelStatus.Click -= btnModelStatus_Click;
            btnRestartPython.Click -= btnRestartPython_Click;
            btnStopPython.Click -= btnStopPython_Click;
            btnOpenModelSettings.Click -= btnOpenModelSettings_Click;
            Global.System.OnDataUpdated -= System_OnDataUpdated;
            Global.DetectionResults.DetectionCandidatesUpdated -= DetectionResults_DetectionCandidatesUpdated;
        }

        private void FormTrainingPanel_Resize(object sender, EventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void statusRefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshStatusOnUiThread();
        }

        private void System_OnDataUpdated()
        {
            RefreshStatusOnUiThread();
        }

        private void DetectionResults_DetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            RefreshStatusOnUiThread();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshStatus();
        }

        private async void btnHealthCheck_Click(object sender, EventArgs e)
        {
            await SendWorkerCommandAsync(
                "Python Worker 진단",
                () => Global.DeepLearning.SendHealthCheck(CreateRequestId()),
                ensureClientReady: true).ConfigureAwait(false);
        }

        private async void btnModelStatus_Click(object sender, EventArgs e)
        {
            await SendWorkerCommandAsync(
                "Python 모델 상태 확인",
                () => Global.DeepLearning.SendModelStatus(CreateRequestId(), ensureLoaded: true),
                ensureClientReady: true).ConfigureAwait(false);
        }

        private async void btnRestartPython_Click(object sender, EventArgs e)
        {
            if (!BeginWorkerCommand("Python Worker 재시작"))
            {
                return;
            }

            try
            {
                bool connected = await Global.RestartPythonModelClientConnectionAsync(GetWorkerConnectTimeoutMilliseconds()).ConfigureAwait(true);
                if (connected)
                {
                    AppLog.COMM("Python Worker restarted and connected.");
                    Global.DeepLearning.SendHealthCheck(CreateRequestId());
                    Global.DeepLearning.SendModelStatus(CreateRequestId(), ensureLoaded: false);
                }
                else
                {
                    PythonCommunicationStatus status = Global.GetPythonCommunicationStatusSnapshot();
                    AppLog.ABNORMAL($"Python Worker restart did not connect. Listener:{status.IsListening}, Client:{status.IsClientConnected}, Error:{FirstNonEmpty(status.LastError, Global.PythonClientProcess.LastError, "none")}");
                }
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Python Worker restart failed: {ex.Message}");
            }
            finally
            {
                EndWorkerCommand();
            }
        }

        private async void btnStopPython_Click(object sender, EventArgs e)
        {
            if (!BeginWorkerCommand("Python Worker 종료"))
            {
                return;
            }

            try
            {
                await Global.StopPythonModelClientConnectionAsync().ConfigureAwait(true);
                AppLog.COMM("Python Worker/listener stopped by training panel.");
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Python Worker stop failed: {ex.Message}");
            }
            finally
            {
                EndWorkerCommand();
            }
        }

        private void btnOpenModelSettings_Click(object sender, EventArgs e)
        {
            using var setting = new RJForms.FormVision_Yolov5ParamSetting();
            setting.ShowDialog(FindForm());
            RefreshStatus();
        }

        private async Task SendWorkerCommandAsync(string actionName, Func<bool> send, bool ensureClientReady)
        {
            if (!BeginWorkerCommand(actionName))
            {
                return;
            }

            try
            {
                if (ensureClientReady)
                {
                    bool ready = await Global.EnsurePythonModelClientReadyAsync(GetWorkerConnectTimeoutMilliseconds()).ConfigureAwait(true);
                    if (!ready)
                    {
                        PythonCommunicationStatus status = Global.GetPythonCommunicationStatusSnapshot();
                        AppLog.ABNORMAL($"{actionName} skipped because Python Worker is not connected. Listener:{status.IsListening}, Client:{status.IsClientConnected}, Error:{FirstNonEmpty(status.LastError, Global.PythonClientProcess.LastError, "none")}");
                        return;
                    }
                }

                bool sent = send?.Invoke() == true;
                if (sent)
                {
                    AppLog.COMM($"{actionName} request sent.");
                    return;
                }

                AppLog.ABNORMAL($"{actionName} request was not sent. Python Worker is not connected.");
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"{actionName} failed: {ex.Message}");
            }
            finally
            {
                EndWorkerCommand();
            }
        }

        private bool BeginWorkerCommand(string actionName)
        {
            if (workerCommandInProgress || IsDisposed)
            {
                return false;
            }

            workerCommandInProgress = true;
            SetCommandButtonsEnabled(false);
            UseWaitCursor = true;
            lblPythonState.Text = $"{actionName} 중";
            lblPythonState.ForeColor = Color.FromArgb(224, 218, 157);
            return true;
        }

        private void EndWorkerCommand()
        {
            if (IsDisposed)
            {
                return;
            }

            workerCommandInProgress = false;
            SetCommandButtonsEnabled(true);
            UseWaitCursor = false;
            RefreshStatusOnUiThread();
        }

        private void SetCommandButtonsEnabled(bool enabled)
        {
            foreach (RJButton button in GetCommandButtons())
            {
                button.Enabled = enabled;
            }
        }

        private IEnumerable<RJButton> GetCommandButtons()
        {
            yield return btnRefresh;
            yield return btnHealthCheck;
            yield return btnModelStatus;
            yield return btnRestartPython;
            yield return btnStopPython;
            yield return btnOpenModelSettings;
        }

        private void RefreshStatusOnUiThread()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                RefreshStatus();
                return;
            }

            if (InvokeRequired)
            {
                this.UIThreadBeginInvoke(RefreshStatus);
                return;
            }

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (workerCommandInProgress)
            {
                return;
            }

            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(Global.Data, refreshYaml: false);
            PythonCommunicationStatus python = Global.GetPythonCommunicationStatusSnapshot();
            PythonModelValidationResult model = PythonModelSettingsValidator.Validate(Global.Data?.ProjectSettings?.PythonModel, requireWeights: false);
            int classCount = Global.Data?.ClassNamedList?.Count ?? 0;

            lblDatasetState.Text = report.IsReady ? "준비 완료" : "준비 필요";
            lblDatasetState.ForeColor = report.IsReady ? Color.FromArgb(190, 232, 212) : Color.FromArgb(244, 199, 134);
            lblDatasetDetail.Text = $"클래스 {classCount} / 학습 이미지 {report.Statistics.TrainImageCount} / 검증 이미지 {report.Statistics.ValidImageCount} / 객체 {report.Statistics.TotalObjectCount}";

            lblPythonState.Text = BuildPythonStateText(python, model);
            lblPythonState.ForeColor = GetPythonStateColor(python, model);
            lblPythonDetail.Text = BuildPythonDetailText(python);

            string[] issues = report.Errors
                .Concat(model.Errors)
                .Concat(model.Warnings)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Take(8)
                .ToArray();
            lblIssueList.Text = issues.Length == 0
                ? "데이터셋과 모델 설정에 치명 오류가 없습니다. 진단 또는 모델 버튼으로 Python Worker 상태를 확인하세요."
                : string.Join(Environment.NewLine, issues.Select((item, index) => $"{index + 1}. {item}"));
        }

        private string BuildPythonStateText(PythonCommunicationStatus status, PythonModelValidationResult model)
        {
            if (model?.IsValid == false)
            {
                return "모델 설정 확인 필요";
            }

            string error = FirstNonEmpty(status?.LastError, Global.PythonClientProcess.LastError);
            if (!string.IsNullOrWhiteSpace(error))
            {
                return $"YOLO 오류 | {TrimStatus(error, 34)}";
            }

            if (status?.IsClientConnected == true)
            {
                if (status.LastModelStatusAtUtc.HasValue)
                {
                    string loaded = status.LastModelLoaded ? "로드됨" : "미로드";
                    string state = string.IsNullOrWhiteSpace(status.LastModelState) ? "상태 확인" : status.LastModelState;
                    return $"Python 연결 / 모델 {state} ({loaded})";
                }

                if (status.LastHealthCheckAtUtc.HasValue && !string.IsNullOrWhiteSpace(status.LastWorkerState))
                {
                    return $"Python 연결 / Worker {status.LastWorkerState}";
                }

                return "Python 연결됨 / 모델 확인 필요";
            }

            if (Global.PythonClientProcess.IsRunning)
            {
                int? pid = Global.PythonClientProcess.ProcessId;
                return pid.HasValue ? $"Python 실행중 / 연결 대기 PID {pid.Value}" : "Python 실행중 / 연결 대기";
            }

            if (status?.IsListening == true)
            {
                return "Listener 대기 / Python 미연결";
            }

            return "Python 미시작";
        }

        private Color GetPythonStateColor(PythonCommunicationStatus status, PythonModelValidationResult model)
        {
            if (model?.IsValid == false || !string.IsNullOrWhiteSpace(status?.LastError) || !string.IsNullOrWhiteSpace(Global.PythonClientProcess.LastError))
            {
                return Color.FromArgb(255, 196, 176);
            }

            if (status?.IsClientConnected == true && status.LastModelLoaded)
            {
                return Color.FromArgb(190, 232, 212);
            }

            if (status?.IsClientConnected == true)
            {
                return Color.FromArgb(224, 218, 157);
            }

            return Color.FromArgb(244, 199, 134);
        }

        private string BuildPythonDetailText(PythonCommunicationStatus status)
        {
            status ??= new PythonCommunicationStatus();
            string endpoint = !string.IsNullOrWhiteSpace(status.ListenerEndpoint)
                ? status.ListenerEndpoint
                : status.ListenerPort > 0 ? $"127.0.0.1:{status.ListenerPort}" : "127.0.0.1:5000";
            int? pid = Global.PythonClientProcess.ProcessId;
            var parts = new List<string>
            {
                status.IsListening ? $"Listener {endpoint}" : "Listener 닫힘",
                status.IsClientConnected ? "TCP 연결" : "TCP 미연결",
                Global.PythonClientProcess.IsRunning
                    ? pid.HasValue ? $"내장 PID {pid.Value}" : "내장 실행"
                    : "내장 중지"
            };

            if (!string.IsNullOrWhiteSpace(status.LastWorkerState))
            {
                parts.Add($"Worker {status.LastWorkerState}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastModelState))
            {
                parts.Add($"모델 {status.LastModelState}");
            }

            DateTime? lastStatusAt = Max(status.LastModelStatusAtUtc, status.LastHealthCheckAtUtc, status.LastReceivedAtUtc);
            if (lastStatusAt.HasValue)
            {
                parts.Add($"최근 {FormatLocalClock(lastStatusAt.Value)}");
            }

            return string.Join("  |  ", parts);
        }

        private void ApplyResponsiveLayout()
        {
            bool compact = Width < 390;
            int columns = compact ? 2 : 3;
            int gap = 8;
            int buttonHeight = 34;
            int left = 9;
            int top = 9;
            int availableWidth = Math.Max(220, commandPanel.Width - (left * 2));
            int buttonWidth = Math.Max(92, (availableWidth - (gap * (columns - 1))) / columns);
            RJButton[] buttons = GetCommandButtons().ToArray();

            for (int index = 0; index < buttons.Length; index++)
            {
                int row = index / columns;
                int column = index % columns;
                buttons[index].SetBounds(
                    left + (column * (buttonWidth + gap)),
                    top + (row * (buttonHeight + gap)),
                    buttonWidth,
                    buttonHeight);
            }
        }

        private int GetWorkerConnectTimeoutMilliseconds()
        {
            int detectionTimeoutSeconds = Global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30;
            return Math.Clamp(detectionTimeoutSeconds * 1000, 1500, 8000);
        }

        private static Panel CreateSection(string title, out Label stateLabel, out Label detailLabel)
        {
            Panel panel = CreatePanel(LabelingWorkbenchPalette.Surface, new Padding(12, 10, 12, 10));
            Label titleLabel = CreateLabel(title, 8.25F, FontStyle.Bold, LabelingWorkbenchPalette.MutedText);
            stateLabel = CreateLabel("확인 중", 13F, FontStyle.Bold, Color.White);
            detailLabel = CreateLabel("-", 8.5F, FontStyle.Regular, LabelingWorkbenchPalette.Text);

            panel.Controls.Add(detailLabel);
            panel.Controls.Add(stateLabel);
            panel.Controls.Add(titleLabel);

            titleLabel.SetBounds(12, 10, 360, 18);
            stateLabel.SetBounds(12, 33, 360, 26);
            detailLabel.SetBounds(12, 64, 360, 44);
            return panel;
        }

        private static Panel CreatePanel(Color backColor, Padding padding)
        {
            return new Panel
            {
                BackColor = backColor,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = padding
            };
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", size, style, GraphicsUnit.Point),
                ForeColor = color,
                Height = 24,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static RJButton CreateButton(string text, FontAwesome.Sharp.IconChar icon, Color backColor)
        {
            var button = new RJButton
            {
                BackColor = backColor,
                BorderColor = backColor,
                BorderRadius = 4,
                BorderSize = 0,
                Design = RJCodeUI_M1.RJControls.ButtonDesign.IconButton,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.White,
                IconChar = icon,
                IconColor = Color.White,
                IconFont = FontAwesome.Sharp.IconFont.Auto,
                IconSize = 16,
                Style = RJCodeUI_M1.RJControls.ControlStyle.Solid,
                Text = text,
                TextAlign = ContentAlignment.MiddleRight,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = LabelingWorkbenchPalette.Surface;
            button.FlatAppearance.MouseOverBackColor = LabelingWorkbenchPalette.AccentHover;
            return button;
        }

        private static string CreateRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static DateTime? Max(params DateTime?[] values)
        {
            return values
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .DefaultIfEmpty()
                .Max() is DateTime value && value != default ? value : null;
        }

        private static string FormatLocalClock(DateTime utc)
        {
            return utc.ToLocalTime().ToString("HH:mm:ss");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private static string TrimStatus(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value ?? "";
            }

            return value.Substring(0, Math.Max(0, maxLength - 1)) + "...";
        }
    }
}
