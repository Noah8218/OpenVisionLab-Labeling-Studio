using Lib.Common;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;

namespace MvcVisionSystem._3._Communication.TCP
{
    public class CCommunicationLearning : IDisposable
    {
        public enum CommandLearning
        {
            StartTraining,
            StopTraining,
            StartDefect,
            StopDefect,
        }

        private CTCPAsync PythonComm { get; } = new CTCPAsync();
        private readonly PythonMessageFramer receiveFramer = new PythonMessageFramer();
        private readonly object statusLock = new object();
        private readonly PythonCommunicationStatus status = new PythonCommunicationStatus();
        private bool isListening;
        private bool isClosing;
        
        public CCommunicationLearning(bool startListen = true, int port = 5000)
        {
            PythonComm.IsStringData = true;                    // 문자열로 데이터를 처리한다.
            PythonComm.IsStringUnicode = false;                 // UTF-8
            PythonComm.TextEncoding = Encoding.UTF8;
            PythonComm.IsAutoConnectTry = false;                // 서버 listener는 Python client의 재시도 접속만 수락한다.
            PythonComm.Port = port;
            status.ListenerEndpoint = $"{PythonComm.IP}:{PythonComm.Port}";
            status.ListenerPort = PythonComm.Port;

            PythonComm.nID = 2;                                 // 통신 ID는 2
            PythonComm.sName = "Python_Model_TCP";               // Python model server TCP/IP 통신
            
            PythonComm.SetCallbackReceive(OnServerReceiveFunction);
            PythonComm.SetCallbackConnect(OnServerConnectFunction);
            PythonComm.SetCallbackDisconnect(OnServerDisconnectFunction);

            if (startListen)
            {
                Start();
            }
        }

        public bool Start()
        {
            isClosing = false;
            if (isListening)
            {
                return true;
            }

            isListening = PythonComm.SetListen();
            UpdateStatus(item =>
            {
                item.IsListening = isListening;
                item.ListenerEndpoint = $"{PythonComm.IP}:{PythonComm.Port}";
                item.ListenerPort = PythonComm.Port;
                if (!isListening)
                {
                    item.LastError = $"TCP listener did not start on {PythonComm.IP}:{PythonComm.Port}.";
                }
                else
                {
                    item.LastWorkerState = "listening";
                    item.LastWorkerMessage = "Python TCP listener is waiting for a client.";
                    item.LastModelLoaded = false;
                }
            });
            return isListening;
        }

        public bool Send(String data )
        {
            try
            {
                PythonComm.Send(data);
                return true;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }


        public bool SendTrainingData(
            string command,
            string imgSize,
            string batch,
            string epoch,
            string cfg,
            string weight,
            string dataYaml = "",
            string model = "yolov5",
            string task = "detect")
        {
            try
            {
                return SendPacket(command, LearningProtocol.BuildTrainingPacket(command, imgSize, batch, epoch, cfg, weight, dataYaml, model, task));
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        public bool SendHealthCheck(string requestId = "")
        {
            return SendPacket("HealthCheck", LearningProtocol.BuildHealthCheckPacket(requestId));
        }

        public bool SendModelStatus(string requestId = "", bool ensureLoaded = false)
        {
            return SendPacket("ModelStatus", LearningProtocol.BuildModelStatusPacket(requestId, ensureLoaded));
        }

        public bool SendDetectImage(string requestId, string imageId, string imagePath, float confidence, string model = "yolov5")
        {
            try
            {
                return SendPacket("DetectImage", LearningProtocol.BuildDetectImagePacket(requestId, imageId, imagePath, confidence, model));
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        public bool SendData(String data, Bitmap bitmap)
        {
            try
            {
                if (bitmap == null) return false;

                return SendPacket(data, LearningProtocol.BuildImagePacket(data, bitmap));
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL($"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }

        private bool SendPacket(string command, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;

            if (!PythonComm.SendBytesAsync(payload))
            {
                AppLog.COMM($"[Send skipped] {command}");
                return false;
            }

            return true;
        }

        public void Close()
        {
            isClosing = true;
            try
            {
                isListening = false;
                PythonComm.StopListen();
                UpdateStatus(item =>
                {
                    item.IsListening = false;
                    item.IsClientConnected = false;
                    item.LastWorkerState = "stopped";
                    item.LastWorkerMessage = "Python TCP listener stopped.";
                    item.LastModelState = "stopped";
                    item.LastModelMessage = "";
                    item.LastModelLoaded = false;
                    item.LastDetectionState = "";
                    item.LastDetectionMessage = "";
                });
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Python TCP listener close failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Close();
        }

        public PythonCommunicationStatus GetStatusSnapshot()
        {
            lock (statusLock)
            {
                return status.Clone();
            }
        }

        public void SetLastError(string error)
        {
            UpdateStatus(item =>
            {
                item.LastError = error ?? "";
                if (!string.IsNullOrWhiteSpace(error))
                {
                    item.LastWorkerState = "error";
                    item.LastWorkerMessage = error;
                }
            });
        }

        public void DropActiveClient(string reason)
        {
            PythonComm.DropActiveClient();
            UpdateStatus(item =>
            {
                item.IsClientConnected = false;
                item.LastDisconnectedAtUtc = DateTime.UtcNow;
                item.LastModelStatusRequestId = "";
                item.LastModelEngine = "";
                item.LastModelWeightsPath = "";
                item.LastModelLoaded = false;
                item.LastError = reason ?? "";
            });
        }

        public static List<DetectionOverlayItem> BuildDetectionOverlays(IEnumerable<DefectInfo> defects)
        {
            return PythonDetectionResultProtocol.BuildDetectionOverlays(defects);
        }


        private void OnServerReceiveFunction(IAsyncResult ar)
        {
            if (isClosing)
            {
                return;
            }

            byte[] byData;
            string sMsg;
            try
            {
                while (!isClosing && PythonComm.GetByteData(out byData))
                {
                    sMsg = Encoding.UTF8.GetString(byData, 0, byData.Length);
                    foreach (string message in receiveFramer.Append(sMsg))
                    {
                        HandleServerMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!isClosing)
                {
                    AppLog.ABNORMAL($"Python TCP receive failed: {ex.Message}");
                    UpdateStatus(item => item.LastError = ex.Message);
                }
            }
        }

        private void HandleServerMessage(string message)
        {
            if (isClosing)
            {
                return;
            }

            AppLog.COMM($"[Receive] {message}");
            UpdateStatus(item =>
            {
                item.LastReceivedAtUtc = DateTime.UtcNow;
                item.LastMessage = message ?? "";
            });

            if (TryHandleDefectResult(message))
            {
                return;
            }

            if (TryHandleModelStatus(message))
            {
                return;
            }

            switch (message)
            {
                case nameof(CommandLearning.StartTraining):
                    break;
                case nameof(CommandLearning.StopTraining):
                    break;
                case nameof(CommandLearning.StartDefect):
                    break;
                case nameof(CommandLearning.StopDefect):
                    break;
                default:
                    AppLog.COMM($"Unknown learning command: {message}");
                    break;
            }
        }

        private bool TryHandleDefectResult(string message)
        {
            DetectionResultParseResult result = PythonDetectionResultProtocol.Parse(message);
            if (!result.IsDetectionResult)
            {
                return false;
            }

            if (result.Status == DetectionResultParseStatus.EmptyPayload)
            {
                AppLog.COMM(result.ErrorMessage);
                UpdateStatus(item => item.LastError = result.ErrorMessage);
                return true;
            }

            if (result.Status == DetectionResultParseStatus.InvalidPayload)
            {
                AppLog.ABNORMAL($"ResultDefect parse failed: {result.ErrorMessage}");
                UpdateStatus(item => item.LastError = result.ErrorMessage);
                return true;
            }

            UpdateStatus(item =>
            {
                item.LastDetectionResultAtUtc = DateTime.UtcNow;
                item.LastDetectionCount = result.Defects.Count;
                item.LastDetectionState = "completed";
                item.LastDetectionMessage = result.Defects.Count > 0
                    ? $"Candidates found: {result.Defects.Count}"
                    : "No candidates found.";
                item.LastError = "";
            });

            CGlobal.Inst.DetectionResults.ApplyToDetectLayer(result.Defects, result.RequestId, result.ImageId);
            return true;
        }

        private bool TryHandleModelStatus(string message)
        {
            PythonModelStatusParseResult result = PythonModelStatusProtocol.Parse(message);
            if (!result.IsStatus)
            {
                return false;
            }

            if (result.Status == PythonModelStatusParseStatus.InvalidPayload)
            {
                AppLog.ABNORMAL($"Python model status parse failed: {result.ErrorMessage}");
                UpdateStatus(item => item.LastError = result.ErrorMessage);
                return true;
            }

            PythonModelStatusMessage statusMessage = result.Message;
            string summary = BuildModelStatusSummary(statusMessage);
            if (statusMessage.IsError)
            {
                AppLog.ABNORMAL(summary);
            }
            else
            {
                AppLog.COMM(summary);
            }

            UpdateStatus(item =>
            {
                if (string.Equals(statusMessage.Type, PythonModelStatusProtocol.DetectionStatusType, StringComparison.OrdinalIgnoreCase))
                {
                    item.LastDetectionStatusAtUtc = DateTime.UtcNow;
                    item.LastDetectionState = statusMessage.State ?? "";
                    item.LastDetectionMessage = statusMessage.Message ?? "";
                    item.LastDetectionProgressPercent = statusMessage.ProgressPercent;
                }
                else if (string.Equals(statusMessage.Type, PythonModelStatusProtocol.HealthCheckResultType, StringComparison.OrdinalIgnoreCase))
                {
                    item.LastHealthCheckAtUtc = DateTime.UtcNow;
                    item.LastWorkerState = statusMessage.State ?? "";
                    item.LastWorkerMessage = statusMessage.Message ?? "";
                    UpdateWorkerCapabilities(item, statusMessage);
                }
                else if (string.Equals(statusMessage.Type, PythonModelStatusProtocol.ModelStatusResultType, StringComparison.OrdinalIgnoreCase))
                {
                    item.LastModelStatusAtUtc = DateTime.UtcNow;
                    item.LastModelStatusRequestId = statusMessage.RequestId ?? "";
                    item.LastModelEngine = statusMessage.ModelEngine ?? "";
                    item.LastModelWeightsPath = statusMessage.ModelWeightsPath ?? "";
                    item.LastModelState = statusMessage.State ?? "";
                    item.LastModelMessage = statusMessage.Message ?? "";
                    item.LastModelLoaded = statusMessage.Loaded == true;
                    UpdateWorkerCapabilities(item, statusMessage);
                    if (statusMessage.HasEmbeddedTrainingStatus)
                    {
                        item.LastTrainingStatusAtUtc = DateTime.UtcNow;
                        item.LastTrainingState = statusMessage.EmbeddedTrainingState ?? "";
                        item.LastTrainingMessage = statusMessage.EmbeddedTrainingMessage ?? "";
                        item.LastTrainingProgressPercent = statusMessage.EmbeddedTrainingProgressPercent;
                        item.LastTrainingEpoch = statusMessage.EmbeddedTrainingEpoch;
                        item.LastTrainingTotalEpochs = statusMessage.EmbeddedTrainingTotalEpochs;
                        item.LastTrainingWeightsPath = statusMessage.TrainingWeights ?? "";
                    }
                }
                else
                {
                    item.LastTrainingStatusAtUtc = DateTime.UtcNow;
                    item.LastTrainingState = statusMessage.State ?? "";
                    item.LastTrainingMessage = statusMessage.Message ?? "";
                    item.LastTrainingProgressPercent = statusMessage.ProgressPercent;
                    item.LastTrainingEpoch = statusMessage.Epoch;
                    item.LastTrainingTotalEpochs = statusMessage.TotalEpochs;
                    item.LastTrainingWeightsPath = statusMessage.TrainingWeights ?? "";
                }

                item.LastError = statusMessage.IsError
                    ? (!string.IsNullOrWhiteSpace(statusMessage.Error) ? statusMessage.Error : statusMessage.Message)
                    : "";
            });

            return true;
        }

        private static void UpdateWorkerCapabilities(PythonCommunicationStatus item, PythonModelStatusMessage statusMessage)
        {
            if (item == null || statusMessage == null)
            {
                return;
            }

            item.WorkerSupportedModels = new List<string>(statusMessage.SupportedModels ?? new List<string>());
            item.WorkerTrainingModels = new List<string>(statusMessage.TrainingModels ?? new List<string>());
            item.WorkerDetectionModels = new List<string>(statusMessage.DetectionModels ?? new List<string>());
            item.WorkerSegmentationModels = new List<string>(statusMessage.SegmentationModels ?? new List<string>());
            item.WorkerClassificationModels = new List<string>(statusMessage.ClassificationModels ?? new List<string>());
            item.WorkerRuntimeWarnings = new List<string>(statusMessage.RuntimeWarnings ?? new List<string>());
            item.WorkerCachedTrainingWeights = new List<string>(statusMessage.CachedTrainingWeights ?? new List<string>());
            item.WorkerMissingTrainingWeights = new List<string>(statusMessage.MissingTrainingWeights ?? new List<string>());
            item.WorkerRuntimeReadyTrainingWeights = new List<string>(statusMessage.RuntimeReadyTrainingWeights ?? new List<string>());
            item.WorkerRuntimeBlockedTrainingWeights = new List<string>(statusMessage.RuntimeBlockedTrainingWeights ?? new List<string>());
            item.WorkerDownloadRequiredTrainingWeights = new List<string>(statusMessage.DownloadRequiredTrainingWeights ?? new List<string>());
            item.WorkerRuntimeBlockedMissingTrainingWeights = new List<string>(statusMessage.RuntimeBlockedMissingTrainingWeights ?? new List<string>());
        }

        private static string BuildModelStatusSummary(PythonModelStatusMessage statusMessage)
        {
            if (statusMessage == null)
            {
                return "Python model status: empty";
            }

            string progress = statusMessage.ProgressPercent.HasValue ? $" {statusMessage.ProgressPercent.Value}%" : "";
            string epoch = statusMessage.Epoch.HasValue && statusMessage.TotalEpochs.HasValue
                ? $" epoch:{statusMessage.Epoch.Value}/{statusMessage.TotalEpochs.Value}"
                : "";
            string detail = !string.IsNullOrWhiteSpace(statusMessage.Error)
                ? statusMessage.Error
                : statusMessage.Message;
            return $"Python {statusMessage.Type} {statusMessage.State}{progress}{epoch} {detail}".Trim();
        }

        // Client가 연결에 성공했을 때
        private void OnServerConnectFunction(IAsyncResult ar)
        {
            if (isClosing)
            {
                return;
            }

            if (ar.AsyncState is CTCPAsync socket)
            {
                AppLog.COMM($"---- Server connect success ID:{socket.nID}, Name:{socket.sName}");
            }

            UpdateStatus(item =>
            {
                item.IsClientConnected = true;
                item.LastConnectedAtUtc = DateTime.UtcNow;
                item.LastModelStatusRequestId = "";
                item.LastModelEngine = "";
                item.LastModelWeightsPath = "";
                item.LastModelLoaded = false;
                item.LastError = "";
            });
        }

        // Client가 연결이 끊어졌을 때
        private void OnServerDisconnectFunction(IAsyncResult ar)
        {
            if (isClosing)
            {
                return;
            }

            if (ar.AsyncState is CTCPAsync socket)
            {
                AppLog.COMM($"**** Server Disconnect ID:{socket.nID}, Name:{socket.sName}");
            }

            // m_log2.Write($"**** Server Disconnect ID:{socket.nID}, Name:{socket.sName}");
            UpdateStatus(item =>
            {
                item.IsClientConnected = false;
                item.LastDisconnectedAtUtc = DateTime.UtcNow;
            });
        }

        private void UpdateStatus(Action<PythonCommunicationStatus> update)
        {
            if (update == null)
            {
                return;
            }

            lock (statusLock)
            {
                update(status);
            }
        }
    }
}
