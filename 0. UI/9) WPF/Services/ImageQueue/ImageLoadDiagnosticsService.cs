using System;
using System.Diagnostics;

namespace MvcVisionSystem
{
    public class ImageLoadDiagnostics
    {
        public static readonly ImageLoadDiagnostics Empty = new ImageLoadDiagnostics(
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

        public ImageLoadDiagnostics(
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

    public static class ImageLoadDiagnosticsService
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

        public static ImageLoadDiagnostics Create(
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
            return new ImageLoadDiagnostics(
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

    [Obsolete("Use ImageLoadDiagnostics.", false)]
    public sealed class WpfImageLoadDiagnostics : ImageLoadDiagnostics
    {
        public static new WpfImageLoadDiagnostics Empty = new WpfImageLoadDiagnostics(
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
            : base(
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
                preloadScheduleMilliseconds)
        {
        }
    }

    [Obsolete("Use ImageLoadDiagnosticsService.", false)]
    public static class WpfImageLoadDiagnosticsService
    {
        public static double TakeElapsedMilliseconds(Stopwatch stopwatch, ref long previousTicks)
            => ImageLoadDiagnosticsService.TakeElapsedMilliseconds(stopwatch, ref previousTicks);

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
            => new WpfImageLoadDiagnostics(
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
