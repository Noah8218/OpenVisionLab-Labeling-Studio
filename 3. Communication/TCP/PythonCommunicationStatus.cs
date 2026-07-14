using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem._3._Communication.TCP
{
    public sealed class PythonCommunicationStatus
    {
        public bool IsListening { get; set; }
        public bool IsClientConnected { get; set; }
        public string ListenerEndpoint { get; set; } = "";
        public int ListenerPort { get; set; }
        public DateTime? LastConnectedAtUtc { get; set; }
        public DateTime? LastDisconnectedAtUtc { get; set; }
        public DateTime? LastReceivedAtUtc { get; set; }
        public DateTime? LastDetectionResultAtUtc { get; set; }
        public DateTime? LastDetectionStatusAtUtc { get; set; }
        public DateTime? LastHealthCheckAtUtc { get; set; }
        public DateTime? LastModelStatusAtUtc { get; set; }
        public string LastModelStatusRequestId { get; set; } = "";
        public string LastModelEngine { get; set; } = "";
        public string LastModelWeightsPath { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public string LastError { get; set; } = "";
        public string LastWorkerState { get; set; } = "";
        public string LastWorkerMessage { get; set; } = "";
        public List<string> WorkerSupportedModels { get; set; } = new List<string>();
        public List<string> WorkerTrainingModels { get; set; } = new List<string>();
        public List<string> WorkerDetectionModels { get; set; } = new List<string>();
        public List<string> WorkerSegmentationModels { get; set; } = new List<string>();
        public List<string> WorkerClassificationModels { get; set; } = new List<string>();
        public List<string> WorkerRuntimeWarnings { get; set; } = new List<string>();
        public List<string> WorkerCachedTrainingWeights { get; set; } = new List<string>();
        public List<string> WorkerMissingTrainingWeights { get; set; } = new List<string>();
        public List<string> WorkerRuntimeReadyTrainingWeights { get; set; } = new List<string>();
        public List<string> WorkerRuntimeBlockedTrainingWeights { get; set; } = new List<string>();
        public List<string> WorkerDownloadRequiredTrainingWeights { get; set; } = new List<string>();
        public List<string> WorkerRuntimeBlockedMissingTrainingWeights { get; set; } = new List<string>();
        public string LastModelState { get; set; } = "";
        public string LastModelMessage { get; set; } = "";
        public bool LastModelLoaded { get; set; }
        public int LastDetectionCount { get; set; }
        public string LastDetectionState { get; set; } = "";
        public string LastDetectionMessage { get; set; } = "";
        public int? LastDetectionProgressPercent { get; set; }
        public string LastTrainingState { get; set; } = "";
        public string LastTrainingMessage { get; set; } = "";
        public int? LastTrainingProgressPercent { get; set; }
        public int? LastTrainingEpoch { get; set; }
        public int? LastTrainingTotalEpochs { get; set; }
        public string LastTrainingWeightsPath { get; set; } = "";
        public DateTime? LastTrainingStatusAtUtc { get; set; }

        public PythonCommunicationStatus Clone()
        {
            return new PythonCommunicationStatus
            {
                IsListening = IsListening,
                IsClientConnected = IsClientConnected,
                ListenerEndpoint = ListenerEndpoint,
                ListenerPort = ListenerPort,
                LastConnectedAtUtc = LastConnectedAtUtc,
                LastDisconnectedAtUtc = LastDisconnectedAtUtc,
                LastReceivedAtUtc = LastReceivedAtUtc,
                LastDetectionResultAtUtc = LastDetectionResultAtUtc,
                LastDetectionStatusAtUtc = LastDetectionStatusAtUtc,
                LastHealthCheckAtUtc = LastHealthCheckAtUtc,
                LastModelStatusAtUtc = LastModelStatusAtUtc,
                LastModelStatusRequestId = LastModelStatusRequestId,
                LastModelEngine = LastModelEngine,
                LastModelWeightsPath = LastModelWeightsPath,
                LastMessage = LastMessage,
                LastError = LastError,
                LastWorkerState = LastWorkerState,
                LastWorkerMessage = LastWorkerMessage,
                WorkerSupportedModels = WorkerSupportedModels?.ToList() ?? new List<string>(),
                WorkerTrainingModels = WorkerTrainingModels?.ToList() ?? new List<string>(),
                WorkerDetectionModels = WorkerDetectionModels?.ToList() ?? new List<string>(),
                WorkerSegmentationModels = WorkerSegmentationModels?.ToList() ?? new List<string>(),
                WorkerClassificationModels = WorkerClassificationModels?.ToList() ?? new List<string>(),
                WorkerRuntimeWarnings = WorkerRuntimeWarnings?.ToList() ?? new List<string>(),
                WorkerCachedTrainingWeights = WorkerCachedTrainingWeights?.ToList() ?? new List<string>(),
                WorkerMissingTrainingWeights = WorkerMissingTrainingWeights?.ToList() ?? new List<string>(),
                WorkerRuntimeReadyTrainingWeights = WorkerRuntimeReadyTrainingWeights?.ToList() ?? new List<string>(),
                WorkerRuntimeBlockedTrainingWeights = WorkerRuntimeBlockedTrainingWeights?.ToList() ?? new List<string>(),
                WorkerDownloadRequiredTrainingWeights = WorkerDownloadRequiredTrainingWeights?.ToList() ?? new List<string>(),
                WorkerRuntimeBlockedMissingTrainingWeights = WorkerRuntimeBlockedMissingTrainingWeights?.ToList() ?? new List<string>(),
                LastModelState = LastModelState,
                LastModelMessage = LastModelMessage,
                LastModelLoaded = LastModelLoaded,
                LastDetectionCount = LastDetectionCount,
                LastDetectionState = LastDetectionState,
                LastDetectionMessage = LastDetectionMessage,
                LastDetectionProgressPercent = LastDetectionProgressPercent,
                LastTrainingState = LastTrainingState,
                LastTrainingMessage = LastTrainingMessage,
                LastTrainingProgressPercent = LastTrainingProgressPercent,
                LastTrainingEpoch = LastTrainingEpoch,
                LastTrainingTotalEpochs = LastTrainingTotalEpochs,
                LastTrainingWeightsPath = LastTrainingWeightsPath,
                LastTrainingStatusAtUtc = LastTrainingStatusAtUtc
            };
        }
    }
}
