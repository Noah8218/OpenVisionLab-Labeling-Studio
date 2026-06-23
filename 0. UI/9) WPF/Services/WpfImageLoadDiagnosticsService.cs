using System;
using System.Diagnostics;

namespace MvcVisionSystem
{
    public sealed class WpfImageLoadDiagnostics
    {
        public static readonly WpfImageLoadDiagnostics Empty = new WpfImageLoadDiagnostics(
            string.Empty,
            false,
            0D,
            0D,
            0D,
            0D,
            0D,
            0D,
            0D,
            0D,
            0D);

        public WpfImageLoadDiagnostics(
            string imagePath,
            bool cacheHit,
            double totalMilliseconds,
            double decodeMilliseconds,
            double canvasUploadMilliseconds,
            double canvasRefreshMilliseconds,
            double stateTransferMilliseconds,
            double annotationResetMilliseconds,
            double queuePopulateMilliseconds,
            double reviewRefreshMilliseconds,
            double preloadScheduleMilliseconds)
        {
            ImagePath = imagePath ?? string.Empty;
            CacheHit = cacheHit;
            TotalMilliseconds = totalMilliseconds;
            DecodeMilliseconds = decodeMilliseconds;
            CanvasUploadMilliseconds = canvasUploadMilliseconds;
            CanvasRefreshMilliseconds = canvasRefreshMilliseconds;
            StateTransferMilliseconds = stateTransferMilliseconds;
            AnnotationResetMilliseconds = annotationResetMilliseconds;
            QueuePopulateMilliseconds = queuePopulateMilliseconds;
            ReviewRefreshMilliseconds = reviewRefreshMilliseconds;
            PreloadScheduleMilliseconds = preloadScheduleMilliseconds;
        }

        public string ImagePath { get; }

        public bool CacheHit { get; }

        public double TotalMilliseconds { get; }

        public double DecodeMilliseconds { get; }

        public double CanvasUploadMilliseconds { get; }

        public double CanvasRefreshMilliseconds { get; }

        public double StateTransferMilliseconds { get; }

        public double AnnotationResetMilliseconds { get; }

        public double QueuePopulateMilliseconds { get; }

        public double ReviewRefreshMilliseconds { get; }

        public double PreloadScheduleMilliseconds { get; }
    }

    public static class WpfImageLoadDiagnosticsService
    {
        // Keep timing math outside the shell so image-load performance probes can evolve without touching view orchestration.
        public static double TakeElapsedMilliseconds(Stopwatch stopwatch, ref long previousTicks)
        {
            if (stopwatch == null)
            {
                throw new ArgumentNullException(nameof(stopwatch));
            }

            long currentTicks = stopwatch.ElapsedTicks;
            double elapsedMilliseconds = (currentTicks - previousTicks) * 1000D / Stopwatch.Frequency;
            previousTicks = currentTicks;
            return elapsedMilliseconds;
        }

        public static WpfImageLoadDiagnostics Create(
            string imagePath,
            bool cacheHit,
            double totalMilliseconds,
            double decodeMilliseconds,
            double canvasUploadMilliseconds,
            double canvasRefreshMilliseconds,
            double stateTransferMilliseconds,
            double annotationResetMilliseconds,
            double queuePopulateMilliseconds,
            double reviewRefreshMilliseconds,
            double preloadScheduleMilliseconds)
        {
            return new WpfImageLoadDiagnostics(
                imagePath,
                cacheHit,
                totalMilliseconds,
                decodeMilliseconds,
                canvasUploadMilliseconds,
                canvasRefreshMilliseconds,
                stateTransferMilliseconds,
                annotationResetMilliseconds,
                queuePopulateMilliseconds,
                reviewRefreshMilliseconds,
                preloadScheduleMilliseconds);
        }
    }
}
