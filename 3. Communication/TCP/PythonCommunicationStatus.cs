using System;

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
        public string LastMessage { get; set; } = "";
        public string LastError { get; set; } = "";
        public string LastWorkerState { get; set; } = "";
        public string LastWorkerMessage { get; set; } = "";
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
                LastMessage = LastMessage,
                LastError = LastError,
                LastWorkerState = LastWorkerState,
                LastWorkerMessage = LastWorkerMessage,
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
                LastTrainingStatusAtUtc = LastTrainingStatusAtUtc
            };
        }
    }
}
