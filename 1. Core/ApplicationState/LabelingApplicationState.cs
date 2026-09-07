using MvcVisionSystem._1._Core;
using System;
using System.Reflection;
using MvcVisionSystem._3._Communication.TCP;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public static class ProductVersionInfo
    {
        public static string VERSION { get; set; } = "1.5.0";
        public static string DATETIME_UPDATED { get; set; } = "2026/06/03 /*20:00*/";
        public static string MANAGER { get; set; } = "NOAH";
    }

    public class LabelingApplicationState
    {
        // 싱글톤(객체 접근시에만 객체를 생성)->지연 생성
        private static readonly Lazy<LabelingApplicationState> instance = new Lazy<LabelingApplicationState>(() => new LabelingApplicationState());

        public static LabelingApplicationState Inst
        {
            get { return instance.Value; }
        }

        // 레시피 관리 클래스(애플리케이션 실행 경로/RECIPE)
        public Recipe Recipe { get; set; } = new Recipe();
        // 모드, 권한, 창 변경 등 System 관련 클래스
        public ApplicationRuntimeState System { get; set; } = new ApplicationRuntimeState();
        // 라벨링 데이터와 YOLO 학습 설정 관리
        public LabelingProjectData Data { get; set; } = new LabelingProjectData();

        public LabelingImageWorkspace ImageWorkspace { get; } = new LabelingImageWorkspace();

        public LabelingWorkflowService LabelingWorkflow { get; }

        public DetectionResultApplicationService DetectionResults { get; }

        public DetectionTransportService DetectionTransport { get; }

        private readonly Lazy<ModelRuntimeComposition> modelRuntime;

        public ModelRuntimeComposition ModelRuntime => modelRuntime.Value;

        public bool IsModelRuntimeCreated => modelRuntime.IsValueCreated;

        public YoloDetectionWorkflowService DetectionWorkflow => ModelRuntime.DetectionWorkflow;

        public YoloTrainingWorkflowService TrainingWorkflow => ModelRuntime.TrainingWorkflow;

        public YoloPythonClientProcessService PythonClientProcess => ModelRuntime.PythonClientProcess;

        public PythonModelCommunication DeepLearning
        {
            get => ModelRuntime.DeepLearning;
            set => ModelRuntime.SetDeepLearning(value);
        }

        public PythonCommunicationStatus GetPythonCommunicationStatusSnapshot()
        {
            return modelRuntime.IsValueCreated
                ? modelRuntime.Value.GetPythonCommunicationStatusSnapshot()
                : new PythonCommunicationStatus();
        }

        public bool EnsurePythonModelClientStarted()
        {
            return ModelRuntime.EnsurePythonModelClientStarted();
        }

        public bool StartPythonModelClientConnection(int timeoutMilliseconds = 5000)
        {
            return ModelRuntime.StartPythonModelClientConnection(timeoutMilliseconds);
        }

        public Task<bool> StartPythonModelClientConnectionAsync(int timeoutMilliseconds = 5000)
        {
            return StartPythonModelClientConnectionAsync(timeoutMilliseconds, CancellationToken.None);
        }

        public Task<bool> StartPythonModelClientConnectionAsync(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return ModelRuntime.StartPythonModelClientConnectionAsync(timeoutMilliseconds, cancellationToken);
        }

        public void StopPythonModelClientConnection()
        {
            if (modelRuntime.IsValueCreated)
            {
                modelRuntime.Value.StopPythonModelClientConnection();
            }
        }

        public Task StopPythonModelClientConnectionAsync()
        {
            return StopPythonModelClientConnectionAsync(CancellationToken.None);
        }

        public Task StopPythonModelClientConnectionAsync(CancellationToken cancellationToken)
        {
            return modelRuntime.IsValueCreated
                ? modelRuntime.Value.StopPythonModelClientConnectionAsync(cancellationToken)
                : Task.CompletedTask;
        }

        public bool RestartPythonModelClientConnection(int timeoutMilliseconds = 5000)
        {
            return ModelRuntime.RestartPythonModelClientConnection(timeoutMilliseconds);
        }

        public Task<bool> RestartPythonModelClientConnectionAsync(int timeoutMilliseconds = 5000)
        {
            return RestartPythonModelClientConnectionAsync(timeoutMilliseconds, CancellationToken.None);
        }

        public Task<bool> RestartPythonModelClientConnectionAsync(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return ModelRuntime.RestartPythonModelClientConnectionAsync(timeoutMilliseconds, cancellationToken);
        }

        public bool EnsurePythonModelClientReady(int timeoutMilliseconds = 5000)
        {
            return ModelRuntime.EnsurePythonModelClientReady(timeoutMilliseconds);
        }

        public Task<bool> EnsurePythonModelClientReadyAsync(int timeoutMilliseconds = 5000)
        {
            return EnsurePythonModelClientReadyAsync(timeoutMilliseconds, CancellationToken.None);
        }

        public Task<bool> EnsurePythonModelClientReadyAsync(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return ModelRuntime.EnsurePythonModelClientReadyAsync(timeoutMilliseconds, cancellationToken);
        }

        public LabelingApplicationState()
        {
            LabelingWorkflow = new LabelingWorkflowService(ImageWorkspace);
            DetectionTransport = new DetectionTransportService(
                () => Data,
                () => ImageWorkspace.CaptureSnapshot());
            DetectionResults = new DetectionResultApplicationService(DetectionTransport, LabelingWorkflow, () => Data);
            modelRuntime = new Lazy<ModelRuntimeComposition>(
                () => new ModelRuntimeComposition(
                    () => Data,
                    value => Data = value,
                    (defects, requestId, imageId) => DetectionResults.ApplyToDetectLayer(defects, requestId, imageId)));
        }

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
