using MvcVisionSystem._1._Core;
using Lib.Common;
using System;
using System.Reflection;
using MvcVisionSystem._3._Communication.TCP;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public static class CVersion
    {        
        public static string VERSION { get; set; } = "1.5.0";
        public static string DATETIME_UPDATED { get; set; } = "2026/06/03 /*20:00*/";
        public static string MANAGER { get; set; } = "NOAH";
    }
    
    public class CGlobal
    {               
        // 싱글톤(객체 접근시에만 객체를 생성)->지연 생성
        private static readonly Lazy<CGlobal> instance = new Lazy<CGlobal>(() => new CGlobal());

        public static CGlobal Inst
        {
            get { return instance.Value; }
        }
        
        // 레시피 관리 클래스(애플리케이션 실행 경로/RECIPE)
        public CRecipe Recipe { get; set; } = new CRecipe();
        // 모드, 권한, 창 변경 등 System 관련 클래스
        public CSystem System { get; set; } = new CSystem();
        // 라벨링 데이터와 YOLO 학습 설정 관리
        public CData Data { get; set; } = new CData();

        public LabelingImageWorkspace ImageWorkspace { get; } = new LabelingImageWorkspace();

        public LabelingWorkflowService LabelingWorkflow { get; } = new LabelingWorkflowService();

        public DetectionResultApplicationService DetectionResults { get; } = new DetectionResultApplicationService();

        public YoloDetectionWorkflowService DetectionWorkflow { get; } = new YoloDetectionWorkflowService();

        public YoloTrainingWorkflowService TrainingWorkflow { get; } = new YoloTrainingWorkflowService();

        public YoloPythonClientProcessService PythonClientProcess { get; } = new YoloPythonClientProcessService();

        private CCommunicationLearning deepLearning;

        public CCommunicationLearning DeepLearning
        {
            get
            {
                deepLearning ??= new CCommunicationLearning();
                return deepLearning;
            }
            set => deepLearning = value;
        }

        public PythonCommunicationStatus GetPythonCommunicationStatusSnapshot()
        {
            return deepLearning?.GetStatusSnapshot() ?? new PythonCommunicationStatus();
        }

        public bool EnsurePythonModelClientStarted()
        {
            Data ??= new CData();
            Data.ProjectSettings ??= new LabelingProjectSettings();
            Data.ProjectSettings.EnsureDefaults();

            PythonModelSettings settings = Data.ProjectSettings.PythonModel;
            if (!settings.AutoStartClient)
            {
                return true;
            }

            return PythonClientProcess.EnsureStarted(settings);
        }

        public bool StartPythonModelClientConnection(int timeoutMilliseconds = 5000)
        {
            DeepLearning.Start();
            return EnsurePythonModelClientReady(timeoutMilliseconds);
        }

        public Task<bool> StartPythonModelClientConnectionAsync(int timeoutMilliseconds = 5000)
        {
            return Task.Run(() => StartPythonModelClientConnection(timeoutMilliseconds));
        }

        public void StopPythonModelClientConnection()
        {
            deepLearning?.Close();
            PythonClientProcess.Stop();
        }

        public Task StopPythonModelClientConnectionAsync()
        {
            return Task.Run(StopPythonModelClientConnection);
        }

        public bool RestartPythonModelClientConnection(int timeoutMilliseconds = 5000)
        {
            StopPythonModelClientConnection();
            return StartPythonModelClientConnection(timeoutMilliseconds);
        }

        public Task<bool> RestartPythonModelClientConnectionAsync(int timeoutMilliseconds = 5000)
        {
            return Task.Run(() => RestartPythonModelClientConnection(timeoutMilliseconds));
        }

        public bool EnsurePythonModelClientReady(int timeoutMilliseconds = 5000)
        {
            Data ??= new CData();
            Data.ProjectSettings ??= new LabelingProjectSettings();
            Data.ProjectSettings.EnsureDefaults();
            DeepLearning.Start();

            bool autoStartClient = Data?.ProjectSettings?.PythonModel?.AutoStartClient != false;
            if (autoStartClient && !EnsurePythonModelClientStarted())
            {
                return false;
            }

            DateTime? requiredConnectionUtc = autoStartClient ? PythonClientProcess.LastStartedAtUtc : null;
            int safeTimeoutMilliseconds = Math.Max(0, timeoutMilliseconds);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(safeTimeoutMilliseconds);
            string pendingStatusRequestId = "";
            DateTime? probedConnectionUtc = null;
            while (DateTime.UtcNow <= deadline)
            {
                PythonCommunicationStatus status = GetPythonCommunicationStatusSnapshot();
                bool connectedAfterClientStart = !requiredConnectionUtc.HasValue
                    || (status.LastConnectedAtUtc.HasValue && status.LastConnectedAtUtc.Value >= requiredConnectionUtc.Value);
                if (status.IsClientConnected && connectedAfterClientStart)
                {
                    if (!string.IsNullOrWhiteSpace(pendingStatusRequestId)
                        && string.Equals(status.LastModelStatusRequestId, pendingStatusRequestId, StringComparison.Ordinal))
                    {
                        if (PythonModelIdentity.Matches(Data.ProjectSettings.PythonModel, status.LastModelEngine, status.LastModelWeightsPath))
                        {
                            return true;
                        }

                        string mismatch = $"Connected YOLO worker does not match the current model settings. Expected:{Data.ProjectSettings.PythonModel.ModelEngine} / {Data.ProjectSettings.PythonModel.WeightsPath}, Actual:{FirstNonEmpty(status.LastModelEngine, "unknown")} / {FirstNonEmpty(status.LastModelWeightsPath, "unknown")}";
                        AppLog.ABNORMAL(mismatch);
                        DeepLearning.DropActiveClient(mismatch);
                        pendingStatusRequestId = "";
                        probedConnectionUtc = null;
                        Thread.Sleep(100);
                        continue;
                    }

                    if (probedConnectionUtc != status.LastConnectedAtUtc)
                    {
                        pendingStatusRequestId = Guid.NewGuid().ToString("N");
                        if (DeepLearning.SendModelStatus(pendingStatusRequestId, ensureLoaded: false))
                        {
                            probedConnectionUtc = status.LastConnectedAtUtc;
                        }
                        else
                        {
                            pendingStatusRequestId = "";
                        }
                    }
                }

                Thread.Sleep(100);
            }

            PythonCommunicationStatus finalStatus = GetPythonCommunicationStatusSnapshot();
            string error = FirstNonEmpty(finalStatus.LastError, PythonClientProcess.LastError, "none");
            string message = $"YOLO Python client did not connect with the configured engine and weights within {safeTimeoutMilliseconds}ms. Listener:{finalStatus.IsListening}, Client:{finalStatus.IsClientConnected}, ProcessRunning:{PythonClientProcess.IsRunning}, Error:{error}";
            DeepLearning.SetLastError(message);
            AppLog.ABNORMAL(message);
            return false;
        }

        public Task<bool> EnsurePythonModelClientReadyAsync(int timeoutMilliseconds = 5000)
        {
            return Task.Run(() => EnsurePythonModelClientReady(timeoutMilliseconds));
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

        public CGlobal() { }        

        public bool Close()
        {
            try
            {
                StopPythonModelClientConnection();
                System.Close();
                return true;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL( $"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        public Task<bool> CloseAsync()
        {
            return Task.Run(Close);
        }
    }
}
