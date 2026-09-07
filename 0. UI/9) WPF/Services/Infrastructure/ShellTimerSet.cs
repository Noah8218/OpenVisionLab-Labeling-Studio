using System;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell's DispatcherTimer instances and their event subscriptions.
    /// The Window still owns the callback behavior; this owner makes creation and
    /// deterministic close-time disposal explicit.
    /// </summary>
    public sealed class ShellTimerSet : IDisposable
    {
        private readonly EventHandler inferenceStatusPulseHandler;
        private readonly EventHandler trainingStatusPollHandler;
        private readonly EventHandler maskStrokePreviewCommitSwapHandler;
        private readonly EventHandler maskStrokeCommitQueueHandler;
        private readonly EventHandler displayAdjustmentRefreshHandler;
        private readonly EventHandler annotationVisibilityRefreshHandler;
        private bool disposed;

        public ShellTimerSet(
            Dispatcher dispatcher,
            EventHandler inferenceStatusPulseHandler,
            EventHandler trainingStatusPollHandler,
            EventHandler maskStrokePreviewCommitSwapHandler,
            EventHandler maskStrokeCommitQueueHandler,
            EventHandler displayAdjustmentRefreshHandler,
            EventHandler annotationVisibilityRefreshHandler)
        {
            ArgumentNullException.ThrowIfNull(dispatcher);
            this.inferenceStatusPulseHandler = inferenceStatusPulseHandler ?? throw new ArgumentNullException(nameof(inferenceStatusPulseHandler));
            this.trainingStatusPollHandler = trainingStatusPollHandler ?? throw new ArgumentNullException(nameof(trainingStatusPollHandler));
            this.maskStrokePreviewCommitSwapHandler = maskStrokePreviewCommitSwapHandler ?? throw new ArgumentNullException(nameof(maskStrokePreviewCommitSwapHandler));
            this.maskStrokeCommitQueueHandler = maskStrokeCommitQueueHandler ?? throw new ArgumentNullException(nameof(maskStrokeCommitQueueHandler));
            this.displayAdjustmentRefreshHandler = displayAdjustmentRefreshHandler ?? throw new ArgumentNullException(nameof(displayAdjustmentRefreshHandler));
            this.annotationVisibilityRefreshHandler = annotationVisibilityRefreshHandler ?? throw new ArgumentNullException(nameof(annotationVisibilityRefreshHandler));

            InferenceStatusPulse = CreateTimer(
                dispatcher,
                DispatcherPriority.Render,
                TimeSpan.FromMilliseconds(33),
                this.inferenceStatusPulseHandler);
            TrainingStatusPoll = CreateTimer(
                dispatcher,
                DispatcherPriority.Background,
                TimeSpan.FromMilliseconds(800),
                this.trainingStatusPollHandler);
            MaskStrokePreviewCommitSwap = CreateTimer(
                dispatcher,
                DispatcherPriority.ApplicationIdle,
                TimeSpan.FromMilliseconds(90),
                this.maskStrokePreviewCommitSwapHandler);
            MaskStrokeCommitQueue = CreateTimer(
                dispatcher,
                DispatcherPriority.ApplicationIdle,
                TimeSpan.FromMilliseconds(MaskEditStateService.CommitQueueQuietMilliseconds),
                this.maskStrokeCommitQueueHandler);
            DisplayAdjustmentRefresh = CreateTimer(
                dispatcher,
                DispatcherPriority.Background,
                TimeSpan.FromMilliseconds(120),
                this.displayAdjustmentRefreshHandler);
            AnnotationVisibilityRefresh = CreateTimer(
                dispatcher,
                DispatcherPriority.ApplicationIdle,
                TimeSpan.FromMilliseconds(220),
                this.annotationVisibilityRefreshHandler);
        }

        public DispatcherTimer InferenceStatusPulse { get; }

        public DispatcherTimer TrainingStatusPoll { get; }

        public DispatcherTimer MaskStrokePreviewCommitSwap { get; }

        public DispatcherTimer MaskStrokeCommitQueue { get; }

        public DispatcherTimer DisplayAdjustmentRefresh { get; }

        public DispatcherTimer AnnotationVisibilityRefresh { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopAndDetach(InferenceStatusPulse, inferenceStatusPulseHandler);
            StopAndDetach(TrainingStatusPoll, trainingStatusPollHandler);
            StopAndDetach(MaskStrokePreviewCommitSwap, maskStrokePreviewCommitSwapHandler);
            StopAndDetach(MaskStrokeCommitQueue, maskStrokeCommitQueueHandler);
            StopAndDetach(DisplayAdjustmentRefresh, displayAdjustmentRefreshHandler);
            StopAndDetach(AnnotationVisibilityRefresh, annotationVisibilityRefreshHandler);
        }

        private static DispatcherTimer CreateTimer(
            Dispatcher dispatcher,
            DispatcherPriority priority,
            TimeSpan interval,
            EventHandler handler)
        {
            var timer = new DispatcherTimer(priority, dispatcher)
            {
                Interval = interval
            };
            timer.Tick += handler;
            return timer;
        }

        private static void StopAndDetach(DispatcherTimer timer, EventHandler handler)
        {
            timer.Stop();
            timer.Tick -= handler;
        }
    }
}
