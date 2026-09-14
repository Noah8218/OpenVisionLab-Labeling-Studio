using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    /// <summary>
    /// Owns model-only workflow composition while delegating Python client
    /// process and communication lifetime to ModelRuntimeClientLifecycle.
    /// </summary>
    public sealed class ModelRuntimeComposition
    {
        private readonly Lazy<YoloDetectionWorkflowService> detectionWorkflow =
            new Lazy<YoloDetectionWorkflowService>(() => new YoloDetectionWorkflowService());
        private readonly Lazy<YoloTrainingWorkflowService> trainingWorkflow =
            new Lazy<YoloTrainingWorkflowService>(() => new YoloTrainingWorkflowService());
        private readonly ModelRuntimeClientLifecycle clientLifecycle;

        public ModelRuntimeComposition(
            Func<LabelingProjectData> dataAccessor,
            Action<LabelingProjectData> dataSetter,
            Action<IReadOnlyList<DefectInfo>, string, string> detectionResultSink = null)
        {
            clientLifecycle = new ModelRuntimeClientLifecycle(dataAccessor, dataSetter, detectionResultSink);
        }

        public YoloDetectionWorkflowService DetectionWorkflow => detectionWorkflow.Value;

        public YoloTrainingWorkflowService TrainingWorkflow => trainingWorkflow.Value;

        public YoloPythonClientProcessService PythonClientProcess => clientLifecycle.PythonClientProcess;

        public PythonModelCommunication DeepLearning => clientLifecycle.DeepLearning;

        public bool IsPythonClientProcessCreated => clientLifecycle.IsPythonClientProcessCreated;

        public bool IsCommunicationCreated => clientLifecycle.IsCommunicationCreated;

        public PythonCommunicationStatus GetPythonCommunicationStatusSnapshot()
        {
            return clientLifecycle.GetPythonCommunicationStatusSnapshot();
        }

        public bool EnsurePythonModelClientStarted()
        {
            return clientLifecycle.EnsurePythonModelClientStarted();
        }

        public bool EnsurePythonModelClientStarted(CancellationToken cancellationToken)
        {
            return clientLifecycle.EnsurePythonModelClientStarted(cancellationToken);
        }

        public bool StartPythonModelClientConnection(int timeoutMilliseconds = 5000)
        {
            return clientLifecycle.StartPythonModelClientConnection(timeoutMilliseconds);
        }

        public bool StartPythonModelClientConnection(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return clientLifecycle.StartPythonModelClientConnection(timeoutMilliseconds, cancellationToken);
        }

        public Task<bool> StartPythonModelClientConnectionAsync(int timeoutMilliseconds = 5000)
        {
            return clientLifecycle.StartPythonModelClientConnectionAsync(timeoutMilliseconds);
        }

        public Task<bool> StartPythonModelClientConnectionAsync(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return clientLifecycle.StartPythonModelClientConnectionAsync(timeoutMilliseconds, cancellationToken);
        }

        public void StopPythonModelClientConnection()
        {
            clientLifecycle.StopPythonModelClientConnection();
        }

        public Task StopPythonModelClientConnectionAsync()
        {
            return clientLifecycle.StopPythonModelClientConnectionAsync();
        }

        public Task StopPythonModelClientConnectionAsync(CancellationToken cancellationToken)
        {
            return clientLifecycle.StopPythonModelClientConnectionAsync(cancellationToken);
        }

        public bool RestartPythonModelClientConnection(int timeoutMilliseconds = 5000)
        {
            return clientLifecycle.RestartPythonModelClientConnection(timeoutMilliseconds);
        }

        public bool RestartPythonModelClientConnection(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return clientLifecycle.RestartPythonModelClientConnection(timeoutMilliseconds, cancellationToken);
        }

        public Task<bool> RestartPythonModelClientConnectionAsync(int timeoutMilliseconds = 5000)
        {
            return clientLifecycle.RestartPythonModelClientConnectionAsync(timeoutMilliseconds);
        }

        public Task<bool> RestartPythonModelClientConnectionAsync(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return clientLifecycle.RestartPythonModelClientConnectionAsync(timeoutMilliseconds, cancellationToken);
        }

        public bool EnsurePythonModelClientReady(int timeoutMilliseconds = 5000)
        {
            return clientLifecycle.EnsurePythonModelClientReady(timeoutMilliseconds);
        }

        public bool EnsurePythonModelClientReady(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return clientLifecycle.EnsurePythonModelClientReady(timeoutMilliseconds, cancellationToken);
        }

        public Task<bool> EnsurePythonModelClientReadyAsync(int timeoutMilliseconds = 5000)
        {
            return clientLifecycle.EnsurePythonModelClientReadyAsync(timeoutMilliseconds);
        }

        public Task<bool> EnsurePythonModelClientReadyAsync(
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return clientLifecycle.EnsurePythonModelClientReadyAsync(timeoutMilliseconds, cancellationToken);
        }

        public void SetDeepLearning(PythonModelCommunication communication)
        {
            clientLifecycle.SetDeepLearning(communication);
        }
    }
}
