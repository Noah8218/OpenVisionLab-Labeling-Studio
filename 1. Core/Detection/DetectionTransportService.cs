using OpenCvSharp.Extensions;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using Timer = System.Threading.Timer;

namespace MvcVisionSystem._1._Core
{
    /// <summary>
    /// Owns detection request transport state without applying candidates to labels.
    /// </summary>
    public sealed class DetectionTransportService
    {
        private readonly object sync = new object();
        private readonly Func<LabelingProjectData> dataAccessor;
        private readonly Func<LabelingImageSnapshot> imageSnapshotAccessor;
        private DetectionRequestContext pendingDetectionContext = DetectionRequestContext.Empty;
        private bool pendingDetectionCanceled;
        private bool hasPendingDetection;
        private DetectionRequestContext lastCompletedDetectionContext = DetectionRequestContext.Empty;
        private bool hasCompletedDetection;
        private Timer pendingDetectionTimeoutTimer;
        private int pendingDetectionTimeoutGeneration;

        internal Action<DetectionRequestContext> RequestStarted { get; set; }

        internal Action<DetectionRequestContext, int> RequestTimedOut { get; set; }

        public DetectionTransportService(
            Func<LabelingProjectData> dataAccessor,
            Func<LabelingImageSnapshot> imageSnapshotAccessor)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.imageSnapshotAccessor = imageSnapshotAccessor ?? throw new ArgumentNullException(nameof(imageSnapshotAccessor));
        }

        public bool TrySendCurrentImageForDetection(
            PythonModelCommunication communication,
            int detectionTimeoutSeconds = 30)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 검사 통신이 초기화되지 않았습니다.");
                return false;
            }

            if (DisplayManager.ImageSrc == null || DisplayManager.ImageSrc.Empty())
            {
                const string message = "현재 이미지가 비어 있어 검사 요청을 보낼 수 없습니다.";
                communication.SetLastError(message);
                AppLog.COMM(message);
                return false;
            }

            using (Bitmap bitmap = BitmapConverter.ToBitmap(DisplayManager.ImageSrc))
            {
                LabelingProjectData data = dataAccessor();
                LabelingImageSnapshot image = CaptureCurrentImageSnapshot();
                DetectionRequestContext context = DetectionRequestContext.Capture(image, bitmap.Size);
                string requestId = Guid.NewGuid().ToString("N");
                string imageId = BuildImageId(context);
                context = DetectionRequestContext.Capture(image, bitmap.Size, requestId, imageId);
                RegisterPendingDetectionContext(context, detectionTimeoutSeconds);

                PythonModelSettings modelSettings = data?.ProjectSettings?.PythonModel;
                bool sent = !string.IsNullOrWhiteSpace(context.ImagePath) && File.Exists(context.ImagePath)
                    ? communication.SendDetectImage(
                        requestId,
                        imageId,
                        context.ImagePath,
                        modelSettings?.MinimumDetectionConfidence ?? 0.25F,
                        modelSettings?.GetProtocolModelName() ?? "yolov5")
                    : communication.SendData(PythonModelCommunication.CommandLearning.StartDefect.ToString(), bitmap);
                if (!sent)
                {
                    ClearPendingDetectionContext();
                    const string message = "Python 모델 클라이언트가 연결되지 않아 현재 검사 요청을 보내지 못했습니다.";
                    communication.SetLastError(message);
                    AppLog.ABNORMAL(message);
                    return false;
                }

                communication.SetLastError("");
            }

            return true;
        }

        public bool TrySendImagePathForDetection(
            PythonModelCommunication communication,
            LabelingProjectData data,
            string imagePath,
            Size imageSize,
            int detectionTimeoutSeconds = 30)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 검사 통신이 초기화되지 않았습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                string message = $"검사 이미지 파일을 찾을 수 없습니다: {imagePath}";
                communication.SetLastError(message);
                AppLog.COMM(message);
                return false;
            }

            if (imageSize.IsEmpty)
            {
                string message = $"검사 이미지 크기를 확인할 수 없습니다: {imagePath}";
                communication.SetLastError(message);
                AppLog.COMM(message);
                return false;
            }

            string requestId = Guid.NewGuid().ToString("N");
            var context = new DetectionRequestContext(
                Path.GetFileNameWithoutExtension(imagePath),
                imagePath,
                imageSize,
                requestId,
                Path.GetFileNameWithoutExtension(imagePath));
            RegisterPendingDetectionContext(context, detectionTimeoutSeconds);

            bool sent = communication.SendDetectImage(
                requestId,
                context.ImageId,
                imagePath,
                data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25F,
                data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5");
            if (!sent)
            {
                ClearPendingDetectionContext();
                const string message = "Python 모델 클라이언트가 연결되지 않아 현재 검사 요청을 보내지 못했습니다.";
                communication.SetLastError(message);
                AppLog.ABNORMAL(message);
                return false;
            }

            communication.SetLastError("");
            return true;
        }

        public void RegisterPendingDetectionImage(
            LabelingImageSnapshot image,
            Size imageSize,
            int detectionTimeoutSeconds = 30,
            string requestId = "",
            string imageId = "")
        {
            DetectionRequestContext context = DetectionRequestContext.Capture(
                image,
                imageSize,
                requestId,
                imageId);
            RegisterPendingDetectionContext(context, detectionTimeoutSeconds);
        }

        internal LabelingImageSnapshot CaptureCurrentImageSnapshot()
            => imageSnapshotAccessor() ?? LabelingImageSnapshot.Empty;

        internal DetectionRequestContext CaptureCurrentContext(
            Size imageSize,
            string requestId = "",
            string imageId = "")
            => DetectionRequestContext.Capture(
                CaptureCurrentImageSnapshot(),
                imageSize,
                requestId,
                imageId);

        public void CancelPendingDetection()
        {
            lock (sync)
            {
                if (hasPendingDetection)
                {
                    lastCompletedDetectionContext = pendingDetectionContext ?? DetectionRequestContext.Empty;
                    hasCompletedDetection = true;
                }

                pendingDetectionContext = DetectionRequestContext.Empty;
                hasPendingDetection = false;
                pendingDetectionCanceled = true;
                ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
            }
        }

        internal bool HasPendingDetectionContext
        {
            get
            {
                lock (sync)
                {
                    return hasPendingDetection;
                }
            }
        }

        internal bool HasCompletedDetectionContext
        {
            get
            {
                lock (sync)
                {
                    return hasCompletedDetection;
                }
            }
        }

        internal bool TryTakePendingDetectionContext(
            string requestId,
            string imageId,
            out DetectionRequestContext context)
        {
            lock (sync)
            {
                context = pendingDetectionContext ?? DetectionRequestContext.Empty;
                if (!hasPendingDetection || !context.MatchesResponse(requestId, imageId))
                {
                    context = DetectionRequestContext.Empty;
                    return false;
                }

                pendingDetectionContext = DetectionRequestContext.Empty;
                hasPendingDetection = false;
                lastCompletedDetectionContext = context;
                hasCompletedDetection = true;
                ResetPendingDetectionTimeoutTimerLocked();
                return true;
            }
        }

        internal DetectionRequestContext TakePendingDetectionContext()
        {
            lock (sync)
            {
                DetectionRequestContext context = pendingDetectionContext ?? DetectionRequestContext.Empty;
                pendingDetectionContext = DetectionRequestContext.Empty;
                hasPendingDetection = false;
                lastCompletedDetectionContext = context;
                hasCompletedDetection = !ReferenceEquals(context, DetectionRequestContext.Empty);
                ResetPendingDetectionTimeoutTimerLocked();
                return context;
            }
        }

        internal bool TakePendingDetectionCanceled()
        {
            lock (sync)
            {
                bool canceled = pendingDetectionCanceled;
                pendingDetectionCanceled = false;
                return canceled;
            }
        }

        internal void ClearPendingDetectionContext()
        {
            lock (sync)
            {
                if (hasPendingDetection)
                {
                    lastCompletedDetectionContext = pendingDetectionContext ?? DetectionRequestContext.Empty;
                    hasCompletedDetection = true;
                }

                pendingDetectionContext = DetectionRequestContext.Empty;
                hasPendingDetection = false;
                ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
            }
        }

        private void RegisterPendingDetectionContext(
            DetectionRequestContext context,
            int detectionTimeoutSeconds)
        {
            int generation;
            DetectionRequestContext safeContext = context ?? DetectionRequestContext.Empty;
            lock (sync)
            {
                pendingDetectionContext = safeContext;
                hasPendingDetection = true;
                hasCompletedDetection = false;
                lastCompletedDetectionContext = DetectionRequestContext.Empty;
                pendingDetectionCanceled = false;
                generation = ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
                int safeTimeoutSeconds = Math.Clamp(detectionTimeoutSeconds, 1, 600);
                pendingDetectionTimeoutTimer = new Timer(
                    _ => HandlePendingDetectionTimeout(safeContext, generation, safeTimeoutSeconds),
                    null,
                    TimeSpan.FromSeconds(safeTimeoutSeconds),
                    Timeout.InfiniteTimeSpan);
            }

            RequestStarted?.Invoke(safeContext);
        }

        private void HandlePendingDetectionTimeout(
            DetectionRequestContext context,
            int generation,
            int timeoutSeconds)
        {
            DetectionRequestContext timedOutContext = null;
            lock (sync)
            {
                if (generation != pendingDetectionTimeoutGeneration
                    || !ReferenceEquals(pendingDetectionContext, context)
                    || !hasPendingDetection)
                {
                    return;
                }

                pendingDetectionContext = DetectionRequestContext.Empty;
                hasPendingDetection = false;
                pendingDetectionCanceled = true;
                timedOutContext = context;
                lastCompletedDetectionContext = context;
                hasCompletedDetection = true;
                ++pendingDetectionTimeoutGeneration;
                ResetPendingDetectionTimeoutTimerLocked();
            }

            RequestTimedOut?.Invoke(timedOutContext, timeoutSeconds);
        }

        private void ResetPendingDetectionTimeoutTimerLocked()
        {
            Timer timer = pendingDetectionTimeoutTimer;
            pendingDetectionTimeoutTimer = null;
            timer?.Dispose();
        }

        private static string BuildImageId(DetectionRequestContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(context.ImagePath))
            {
                return Path.GetFileNameWithoutExtension(context.ImagePath);
            }

            return Path.GetFileNameWithoutExtension(context.ImageName ?? string.Empty);
        }
    }
}
