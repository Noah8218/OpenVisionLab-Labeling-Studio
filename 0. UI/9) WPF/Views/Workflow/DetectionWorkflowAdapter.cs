using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Connects the Shell's existing runtime state to the Window-free single-image
    /// detection service. The service owns worker execution and disposal; this
    /// adapter only supplies callbacks and admission rules.
    /// </summary>
    internal sealed class DetectionWorkflowAdapter
    {
        private readonly DetectionWorkflowAdapterContext context;

        internal DetectionWorkflowAdapter(DetectionWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.SettingsAccessor);
            ArgumentNullException.ThrowIfNull(context.DetectionResultsAccessor);
            ArgumentNullException.ThrowIfNull(context.EnsureReadyAsync);
            ArgumentNullException.ThrowIfNull(context.TryStartDetection);
            ArgumentNullException.ThrowIfNull(context.WorkerFailureAccessor);
            ArgumentNullException.ThrowIfNull(context.RequestErrorAccessor);
            ArgumentNullException.ThrowIfNull(context.ActiveImagePathProvider);
            ArgumentNullException.ThrowIfNull(context.IsApplicationCloseApproved);
            ArgumentNullException.ThrowIfNull(context.IsBatchDetectionRunning);

            WorkflowService = new ImageDetectionWorkflowService(
                context.SettingsAccessor,
                context.DetectionResultsAccessor,
                context.EnsureReadyAsync,
                context.TryStartDetection,
                context.WorkerFailureAccessor,
                context.RequestErrorAccessor);
        }

        internal ImageDetectionWorkflowService WorkflowService { get; }

        internal ImageDetectionCallbacks CreateImageDetectionCallbacks()
        {
            return new ImageDetectionCallbacks
            {
                PrepareCanvasImage = (path, populateQueue) =>
                {
                    // Same-image inference must preserve in-progress manual labels.
                    bool shouldLoadTargetImage = !string.Equals(
                        path,
                        context.ActiveImagePathProvider(),
                        StringComparison.OrdinalIgnoreCase);
                    return !shouldLoadTargetImage || context.TryLoadImage(path, populateQueue)
                        ? context.ActiveImageSizeProvider()
                        : null;
                },
                IsCurrentImage = path => AreSameImagePath(path, context.ActiveImagePathProvider()),
                ApplyCandidates = context.ApplyCandidates,
                RefreshActions = context.RefreshActions,
                SetPythonStatus = context.SetPythonStatus,
                SetCommandStatus = context.SetCommandStatus,
                SetInferenceStatus = context.SetInferenceStatus,
                AppendLog = context.AppendLog
            };
        }

        internal Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
        {
            if (context.IsApplicationCloseApproved() || context.IsBatchDetectionRunning())
            {
                return Task.CompletedTask;
            }

            return WorkflowService.RunInteractiveAsync(
                imagePath,
                context.ActiveImagePathProvider(),
                allowSmokeFallback,
                CreateImageDetectionCallbacks());
        }

        internal Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken)
        {
            return WorkflowService.RunSmokeAsync(
                imagePath,
                applyToCanvas,
                cancellationToken,
                CreateImageDetectionCallbacks());
        }

        internal Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
        {
            return WorkflowService.RunWorkerAsync(
                imagePath,
                applyToCanvas,
                cancellationToken,
                connectTimeoutMilliseconds,
                CreateImageDetectionCallbacks(),
                workerReadyAlreadyChecked);
        }

        private static bool AreSameImagePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    internal sealed class DetectionWorkflowAdapterContext
    {
        internal Func<PythonModelSettings> SettingsAccessor { get; init; }
        internal Func<DetectionResultApplicationService> DetectionResultsAccessor { get; init; }
        internal Func<int, CancellationToken, Task<bool>> EnsureReadyAsync { get; init; }
        internal Func<bool, string, Size, bool> TryStartDetection { get; init; }
        internal Func<string> WorkerFailureAccessor { get; init; }
        internal Func<string> RequestErrorAccessor { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<string, bool, bool> TryLoadImage { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyCandidates { get; init; }
        internal Action RefreshActions { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string, bool> SetCommandStatus { get; init; }
        internal Action<string, bool, bool> SetInferenceStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsBatchDetectionRunning { get; init; }
    }

}
