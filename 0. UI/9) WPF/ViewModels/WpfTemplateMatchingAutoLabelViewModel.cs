using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public interface IWpfTemplateMatchingAutoLabelHost
    {
        bool IsAutoLabelBusy { get; }
        bool HasActiveAutoLabelImage { get; }
        Bitmap ActiveAutoLabelImage { get; }
        string ActiveAutoLabelImagePath { get; }
        CData AutoLabelData { get; }
        int MaximumTemplateMatchingCandidateCount { get; }

        bool TryResolveTemplateMatchingSource(out Rectangle templateBounds, out string className);
        bool TryResolveTemplateMatchingSourceSegment(out IReadOnlyList<Point> points, out IReadOnlyList<IReadOnlyList<Point>> cutouts);
        bool TryResolveTemplateMatchingSourceMask(out byte[] maskData, out Size maskSize, out Rectangle maskBounds);
        CClassItem EnsureAutoLabelClassItem(string className);
        IReadOnlyList<WpfImageQueueItem> GetVisibleAutoLabelQueueItems();
        IReadOnlyList<WpfImageQueueItem> GetAllAutoLabelQueueItems();
        IReadOnlyList<WpfImageQueueItem> BuildAutoLabelBatchQueue(IEnumerable<WpfImageQueueItem> items);
        void AppendAutoLabelLog(string message);
        void ShowAutoLabelGuide(string title, string message);
        int ApplyAutoLabelCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded);
        void SetAutoLabelPythonStatus(string text);
        void SetAutoLabelCommandStatus(string text, bool isBusy);
        void SetAutoLabelGlobalInferenceStatus(string text, bool isBusy, bool isWarning = false);
        CancellationToken StartAutoLabelBatch(int totalCount, string scopeText);
        void MarkAutoLabelBatchItemRequested(WpfImageQueueItem item);
        void UpdateAutoLabelBatchProgress(string scopeText, string currentFileName, int completedCount, int totalCount);
        void ApplyAutoLabelBatchResult(WpfImageQueueItem item, TemplateMatchingBatchAutoLabelItemResult result, bool saveReviewStatus);
        void SaveAutoLabelReviewStatus();
        void CompleteAutoLabelBatch(bool canceled, int completedCount, int totalCount, string scopeText);
        void NotifyAutoLabelDataChanged();
        Task YieldAutoLabelBatchFrameAsync(CancellationToken token);
    }

    public sealed class WpfTemplateMatchingAutoLabelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private readonly TemplateMatchingAutoLabelService templateMatchingAutoLabelService;
        private readonly TemplateMatchingBatchAutoLabelService templateMatchingBatchAutoLabelService;
        private readonly WpfTemplateMatchingAutoLabelPresentationService presentationService;
        private IWpfTemplateMatchingAutoLabelHost host;
        private Bitmap registeredTemplateImage;
        private string registeredTemplateClassName = string.Empty;
        private string registeredTemplateSourceImagePath = string.Empty;
        private Rectangle registeredTemplateSourceBounds = Rectangle.Empty;
        private IReadOnlyList<Point> registeredTemplateSourceSegmentPoints = Array.Empty<Point>();
        private IReadOnlyList<IReadOnlyList<Point>> registeredTemplateSourceSegmentCutouts = Array.Empty<IReadOnlyList<Point>>();
        private byte[] registeredTemplateSourceMaskData = Array.Empty<byte>();
        private Size registeredTemplateSourceMaskSize = Size.Empty;
        private Rectangle registeredTemplateSourceMaskBounds = Rectangle.Empty;
        private ICommand runCurrentImageCommand = new RelayCommand(NoOpCommand);
        private ICommand runBatchCommand = new RelayCommand(NoOpCommand);

        public WpfTemplateMatchingAutoLabelViewModel()
            : this(new TemplateMatchingAutoLabelService(), new TemplateMatchingBatchAutoLabelService())
        {
        }

        public WpfTemplateMatchingAutoLabelViewModel(
            TemplateMatchingAutoLabelService templateMatchingAutoLabelService,
            TemplateMatchingBatchAutoLabelService templateMatchingBatchAutoLabelService)
            : this(
                templateMatchingAutoLabelService,
                templateMatchingBatchAutoLabelService,
                new WpfTemplateMatchingAutoLabelPresentationService())
        {
        }

        public WpfTemplateMatchingAutoLabelViewModel(
            TemplateMatchingAutoLabelService templateMatchingAutoLabelService,
            TemplateMatchingBatchAutoLabelService templateMatchingBatchAutoLabelService,
            WpfTemplateMatchingAutoLabelPresentationService presentationService)
        {
            this.templateMatchingAutoLabelService = templateMatchingAutoLabelService ?? new TemplateMatchingAutoLabelService();
            this.templateMatchingBatchAutoLabelService = templateMatchingBatchAutoLabelService ?? new TemplateMatchingBatchAutoLabelService();
            this.presentationService = presentationService ?? new WpfTemplateMatchingAutoLabelPresentationService();
            RunCurrentImageCommand = new RelayCommand(RunCurrentImage);
            RunBatchCommand = new RelayCommand(RunBatch);
        }

        public ICommand RunCurrentImageCommand
        {
            get => runCurrentImageCommand;
            private set => SetProperty(ref runCurrentImageCommand, value);
        }

        public ICommand RunBatchCommand
        {
            get => runBatchCommand;
            private set => SetProperty(ref runBatchCommand, value);
        }

        private bool HasRegisteredTemplate => registeredTemplateImage != null && !string.IsNullOrWhiteSpace(registeredTemplateClassName);

        public void ConfigureHost(IWpfTemplateMatchingAutoLabelHost host)
        {
            this.host = host;
        }

        public void RunCurrentImage()
        {
            IWpfTemplateMatchingAutoLabelHost currentHost = host;
            if (currentHost == null)
            {
                return;
            }

            if (!currentHost.HasActiveAutoLabelImage)
            {
                ShowTemplateGuide(
                    currentHost,
                    "이미지가 필요합니다",
                    "템플릿을 등록하거나 적용하려면 먼저 이미지 큐에서 이미지를 열어야 합니다.");
                return;
            }

            bool hasSourceBox = currentHost.TryResolveTemplateMatchingSource(out Rectangle templateBounds, out string className);
            bool isRegisteredSourceImage = HasRegisteredTemplate
                && !string.IsNullOrWhiteSpace(currentHost.ActiveAutoLabelImagePath)
                && string.Equals(currentHost.ActiveAutoLabelImagePath, registeredTemplateSourceImagePath, StringComparison.OrdinalIgnoreCase);

            if (hasSourceBox && (!HasRegisteredTemplate || isRegisteredSourceImage))
            {
                RegisterCurrentTemplate(currentHost, templateBounds, className);
                return;
            }

            if (!HasRegisteredTemplate)
            {
                ShowTemplateGuide(
                    currentHost,
                    "기준 라벨이 필요합니다",
                    "먼저 기준 이미지에서 라벨 박스 1개를 선택하고 도구 > 현재 이미지 라벨 초안 생성을 눌러 기준 템플릿으로 등록하세요. 그 다음 다른 이미지에서 같은 버튼을 누르면 라벨 초안을 생성합니다.");
                return;
            }

            ApplyRegisteredTemplateToCurrentImage(currentHost);
        }

        private void RegisterCurrentTemplate(
            IWpfTemplateMatchingAutoLabelHost currentHost,
            Rectangle templateBounds,
            string className)
        {
            Bitmap templateImage = templateMatchingAutoLabelService.CloneTemplateImage(
                currentHost.ActiveAutoLabelImage,
                templateBounds,
                out string cloneError);
            if (templateImage == null)
            {
                ShowTemplateGuide(
                    currentHost,
                    "기준 라벨을 사용할 수 없습니다",
                    $"선택한 라벨 박스를 템플릿으로 만들 수 없습니다. {cloneError}");
                return;
            }

            registeredTemplateImage?.Dispose();
            registeredTemplateImage = templateImage;
            registeredTemplateClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
            registeredTemplateSourceImagePath = currentHost.ActiveAutoLabelImagePath ?? string.Empty;
            registeredTemplateSourceBounds = templateBounds;
            StoreTemplateSourceSegment(currentHost);

            string status = presentationService.BuildTemplateRegisteredStatus(registeredTemplateClassName, templateBounds);
            currentHost.SetAutoLabelGlobalInferenceStatus(status, isBusy: false);
            currentHost.SetAutoLabelCommandStatus(status, isBusy: false);
            currentHost.SetAutoLabelPythonStatus("Auto label: template registered");
            currentHost.AppendAutoLabelLog($"Template registered: class={registeredTemplateClassName}, source={templateBounds.X},{templateBounds.Y},{templateBounds.Width},{templateBounds.Height}, image={registeredTemplateSourceImagePath}");
        }

        private void ApplyRegisteredTemplateToCurrentImage(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            TemplateMatchingAutoLabelResult result = templateMatchingAutoLabelService.MatchImageWithTemplate(
                currentHost.ActiveAutoLabelImage,
                registeredTemplateImage,
                registeredTemplateClassName,
                BuildCurrentImageApplyOptions(currentHost));

            if (!result.Succeeded)
            {
                currentHost.AppendAutoLabelLog($"Template matching failed: {result.Message}");
                currentHost.SetAutoLabelGlobalInferenceStatus(presentationService.BuildApplyFailureStatus(result.Message), isBusy: false, isWarning: true);
                currentHost.SetAutoLabelPythonStatus("Auto label: template matching failed");
                currentHost.ApplyAutoLabelCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), succeeded: false);
                return;
            }

            int addedCount = currentHost.ApplyAutoLabelCandidates(result.Candidates, succeeded: true);
            string status = presentationService.BuildApplyResultStatus(addedCount, result.Candidates.Count);
            currentHost.SetAutoLabelGlobalInferenceStatus(status, isBusy: false, isWarning: addedCount == 0);
            currentHost.SetAutoLabelCommandStatus(status, isBusy: false);
            currentHost.SetAutoLabelPythonStatus($"Auto label: template labels {addedCount}");
            currentHost.AppendAutoLabelLog($"Template applied: labels={addedCount}, candidates={result.Candidates.Count}, registeredSource={registeredTemplateSourceBounds.X},{registeredTemplateSourceBounds.Y},{registeredTemplateSourceBounds.Width},{registeredTemplateSourceBounds.Height}, elapsed={result.Elapsed.TotalMilliseconds:0.0}ms");
        }

        public async void RunBatch()
        {
            IWpfTemplateMatchingAutoLabelHost currentHost = host;
            if (currentHost == null)
            {
                return;
            }

            if (currentHost.IsAutoLabelBusy)
            {
                currentHost.AppendAutoLabelLog("Template batch skipped: another detection task is running.");
                return;
            }

            if (HasRegisteredTemplate)
            {
                await RunRegisteredTemplateBatchAsync(currentHost).ConfigureAwait(true);
                return;
            }

            if (!currentHost.HasActiveAutoLabelImage)
            {
                ShowTemplateGuide(
                    currentHost,
                    "이미지가 필요합니다",
                    "전체 이미지 자동 저장을 실행하려면 먼저 기준 이미지를 열고, 그 이미지에서 기준 라벨 박스 1개를 선택해야 합니다.");
                return;
            }

            if (!currentHost.TryResolveTemplateMatchingSource(out Rectangle templateBounds, out string className))
            {
                ShowTemplateGuide(
                    currentHost,
                    "기준 라벨이 필요합니다",
                    "전체 이미지 자동 저장은 선택한 라벨 박스 모양을 라벨 없는 이미지에 바로 저장합니다. 먼저 기준 이미지에서 라벨 박스 1개를 선택하세요.");
                return;
            }

            using Bitmap templateImage = templateMatchingAutoLabelService.CloneTemplateImage(
                currentHost.ActiveAutoLabelImage,
                templateBounds,
                out string cloneError);
            if (templateImage == null)
            {
                ShowTemplateGuide(
                    currentHost,
                    "기준 라벨을 사용할 수 없습니다",
                    $"선택한 라벨 박스를 템플릿으로 만들 수 없습니다. {cloneError}");
                return;
            }

            CClassItem classItem = currentHost.EnsureAutoLabelClassItem(className);
            string normalizedClassName = classItem?.Text ?? className;
            IReadOnlyList<WpfImageQueueItem> queue = BuildBatchQueue(currentHost);
            currentHost.TryResolveTemplateMatchingSourceSegment(
                out IReadOnlyList<Point> sourceSegmentPoints,
                out IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts);
            currentHost.TryResolveTemplateMatchingSourceMask(
                out byte[] sourceMaskData,
                out Size sourceMaskSize,
                out Rectangle sourceMaskBounds);
            await RunBatchAsync(
                currentHost,
                queue,
                templateImage,
                classItem,
                normalizedClassName,
                templateBounds,
                sourceSegmentPoints,
                sourceSegmentCutouts,
                sourceMaskData,
                sourceMaskSize,
                sourceMaskBounds).ConfigureAwait(true);
        }

        private async Task RunRegisteredTemplateBatchAsync(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            if (registeredTemplateImage == null)
            {
                ShowTemplateGuide(
                    currentHost,
                    "템플릿 등록이 필요합니다",
                    "먼저 기준 이미지에서 라벨 박스 1개를 선택하고 도구 > 현재 이미지 라벨 초안 생성으로 기준 템플릿을 등록하세요.");
                return;
            }

            using Bitmap templateImage = (Bitmap)registeredTemplateImage.Clone();
            CClassItem classItem = currentHost.EnsureAutoLabelClassItem(registeredTemplateClassName);
            string normalizedClassName = classItem?.Text ?? registeredTemplateClassName;
            IReadOnlyList<WpfImageQueueItem> queue = BuildRegisteredTemplateBatchQueue(currentHost);
            await RunBatchAsync(
                currentHost,
                queue,
                templateImage,
                classItem,
                normalizedClassName,
                registeredTemplateSourceBounds,
                registeredTemplateSourceSegmentPoints,
                registeredTemplateSourceSegmentCutouts,
                registeredTemplateSourceMaskData,
                registeredTemplateSourceMaskSize,
                registeredTemplateSourceMaskBounds).ConfigureAwait(true);
        }

        private async Task RunBatchAsync(
            IWpfTemplateMatchingAutoLabelHost currentHost,
            IReadOnlyList<WpfImageQueueItem> queue,
            Bitmap templateImage,
            CClassItem classItem,
            string className,
            Rectangle sourceBounds,
            IReadOnlyList<Point> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            Size sourceMaskSize,
            Rectangle sourceMaskBounds)
        {
            if (queue == null || queue.Count == 0)
            {
                ShowTemplateGuide(
                    currentHost,
                    "처리할 이미지가 없습니다",
                    "전체 이미지 자동 저장은 현재 기준 이미지를 제외한 라벨 없는 이미지에만 적용됩니다. 이미지 큐에 라벨 없는 이미지가 있는지 확인하세요.");
                return;
            }

            const string scopeText = "template";
            CancellationToken token = currentHost.StartAutoLabelBatch(queue.Count, scopeText);
            currentHost.SetAutoLabelCommandStatus(presentationService.BuildBatchStartCommandStatus(queue.Count), isBusy: true);
            currentHost.SetAutoLabelGlobalInferenceStatus(presentationService.BuildBatchStartGlobalStatus(queue.Count), isBusy: true);
            currentHost.SetAutoLabelPythonStatus("Auto label: template batch running");

            var batchStopwatch = Stopwatch.StartNew();
            int savedImageCount = 0;
            int savedObjectCount = 0;
            int noCandidateCount = 0;
            int failedCount = 0;
            int pendingReviewStatusSaves = 0;
            int completedCount = 0;

            try
            {
                foreach (WpfImageQueueItem item in queue)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    string fileName = Path.GetFileName(item.ImagePath);
                    currentHost.MarkAutoLabelBatchItemRequested(item);
                    currentHost.UpdateAutoLabelBatchProgress(scopeText, fileName, completedCount, queue.Count);
                    currentHost.SetAutoLabelGlobalInferenceStatus(presentationService.BuildBatchItemGlobalStatus(completedCount + 1, queue.Count, fileName), isBusy: true);

                    TemplateMatchingBatchAutoLabelItemResult result = await Task
                        .Run(() => templateMatchingBatchAutoLabelService.MatchAndSaveImage(
                            item.ImagePath,
                            templateImage,
                            classItem,
                            className,
                            currentHost.AutoLabelData,
                            BuildBatchOptions(currentHost),
                            token,
                            sourceBounds,
                            sourceSegmentPoints,
                            sourceSegmentCutouts,
                            sourceMaskData,
                            sourceMaskSize,
                            sourceMaskBounds))
                        .ConfigureAwait(true);

                    currentHost.ApplyAutoLabelBatchResult(item, result, saveReviewStatus: false);
                    if (result.Saved)
                    {
                        savedImageCount++;
                        savedObjectCount += result.CandidateCount;
                        currentHost.AppendAutoLabelLog($"Template batch saved: {completedCount + 1}/{queue.Count} {fileName} objects={result.CandidateCount} / {result.Elapsed.TotalMilliseconds:0.0}ms");
                    }
                    else if (result.NoCandidate)
                    {
                        noCandidateCount++;
                        currentHost.AppendAutoLabelLog($"Template batch no candidate: {completedCount + 1}/{queue.Count} {fileName} / {result.Elapsed.TotalMilliseconds:0.0}ms");
                    }
                    else if (!token.IsCancellationRequested)
                    {
                        failedCount++;
                        currentHost.AppendAutoLabelLog($"Template batch failed: {completedCount + 1}/{queue.Count} {fileName} / {result.Message}");
                    }

                    completedCount++;
                    pendingReviewStatusSaves++;
                    if (pendingReviewStatusSaves >= 10)
                    {
                        currentHost.SaveAutoLabelReviewStatus();
                        pendingReviewStatusSaves = 0;
                    }

                    currentHost.SetAutoLabelPythonStatus($"Auto label: template batch {completedCount}/{queue.Count}");
                    await currentHost.YieldAutoLabelBatchFrameAsync(token).ConfigureAwait(true);
                }
            }
            finally
            {
                bool canceled = token.IsCancellationRequested;
                if (pendingReviewStatusSaves > 0 || completedCount > 0)
                {
                    currentHost.SaveAutoLabelReviewStatus();
                }

                currentHost.CompleteAutoLabelBatch(canceled, completedCount, queue.Count, scopeText);
                currentHost.SetAutoLabelPythonStatus(canceled ? "Auto label: template batch canceled" : "Auto label: template batch complete");
                currentHost.SetAutoLabelCommandStatus(
                    presentationService.BuildBatchCompletionCommandStatus(
                        canceled,
                        savedImageCount,
                        savedObjectCount,
                        noCandidateCount,
                        failedCount),
                    isBusy: false);
                currentHost.SetAutoLabelGlobalInferenceStatus(
                    presentationService.BuildBatchCompletionGlobalStatus(canceled, completedCount, queue.Count, batchStopwatch.Elapsed),
                    isBusy: false,
                    isWarning: failedCount > 0 || canceled);
                currentHost.AppendAutoLabelLog($"Template batch {(canceled ? "canceled" : "complete")}: processed={completedCount}/{queue.Count}, saved images={savedImageCount}, objects={savedObjectCount}, no candidate={noCandidateCount}, failed={failedCount}, elapsed={batchStopwatch.Elapsed.TotalSeconds:0.0}s");
                currentHost.NotifyAutoLabelDataChanged();
            }
        }

        private void StoreTemplateSourceSegment(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            registeredTemplateSourceSegmentPoints = Array.Empty<Point>();
            registeredTemplateSourceSegmentCutouts = Array.Empty<IReadOnlyList<Point>>();
            registeredTemplateSourceMaskData = Array.Empty<byte>();
            registeredTemplateSourceMaskSize = Size.Empty;
            registeredTemplateSourceMaskBounds = Rectangle.Empty;

            if (currentHost.TryResolveTemplateMatchingSourceSegment(
                    out IReadOnlyList<Point> points,
                    out IReadOnlyList<IReadOnlyList<Point>> cutouts)
                && points?.Count >= 3)
            {
                registeredTemplateSourceSegmentPoints = points.Select(point => point).ToList();
                registeredTemplateSourceSegmentCutouts = (cutouts ?? Array.Empty<IReadOnlyList<Point>>())
                    .Select(cutout => (IReadOnlyList<Point>)(cutout?.Select(point => point).ToList() ?? new List<Point>()))
                    .ToList();
                return;
            }

            if (currentHost.TryResolveTemplateMatchingSourceMask(
                    out byte[] maskData,
                    out Size maskSize,
                    out Rectangle maskBounds)
                && maskData != null
                && maskSize.Width > 0
                && maskSize.Height > 0
                && maskData.Length == maskSize.Width * maskSize.Height
                && !maskBounds.IsEmpty)
            {
                registeredTemplateSourceMaskData = maskData.ToArray();
                registeredTemplateSourceMaskSize = maskSize;
                registeredTemplateSourceMaskBounds = maskBounds;
            }
        }

        private IReadOnlyList<WpfImageQueueItem> BuildBatchQueue(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            IReadOnlyList<WpfImageQueueItem> queueItems = currentHost.BuildAutoLabelBatchQueue(currentHost.GetVisibleAutoLabelQueueItems());
            var processPaths = new HashSet<string>(
                templateMatchingBatchAutoLabelService.BuildUnlabeledImagePathQueue(
                    queueItems.Select(item => item.ImagePath),
                    currentHost.AutoLabelData,
                    currentHost.ActiveAutoLabelImagePath),
                StringComparer.OrdinalIgnoreCase);

            return queueItems
                .Where(item => processPaths.Contains(item.ImagePath))
                .ToList();
        }

        private IReadOnlyList<WpfImageQueueItem> BuildRegisteredTemplateBatchQueue(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            IReadOnlyList<WpfImageQueueItem> allQueueItems = currentHost.GetAllAutoLabelQueueItems() ?? Array.Empty<WpfImageQueueItem>();
            if (allQueueItems.Count == 0)
            {
                allQueueItems = currentHost.GetVisibleAutoLabelQueueItems() ?? Array.Empty<WpfImageQueueItem>();
            }

            IReadOnlyList<WpfImageQueueItem> queueItems = currentHost.BuildAutoLabelBatchQueue(allQueueItems);
            var processPaths = new HashSet<string>(
                templateMatchingBatchAutoLabelService.BuildUnlabeledImagePathQueue(
                    queueItems.Select(item => item.ImagePath),
                    currentHost.AutoLabelData,
                    registeredTemplateSourceImagePath),
                StringComparer.OrdinalIgnoreCase);

            return queueItems
                .Where(item => processPaths.Contains(item.ImagePath))
                .ToList();
        }

        private static TemplateMatchingAutoLabelOptions BuildCurrentImageApplyOptions(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            return new TemplateMatchingAutoLabelOptions
            {
                MinimumScore = 0.7D,
                MaximumCandidates = Math.Max(1, currentHost.MaximumTemplateMatchingCandidateCount),
                ExcludeSourceRegion = false
            };
        }

        private static TemplateMatchingAutoLabelOptions BuildBatchOptions(IWpfTemplateMatchingAutoLabelHost currentHost)
        {
            return new TemplateMatchingAutoLabelOptions
            {
                MinimumScore = 0.7D,
                MaximumCandidates = currentHost.MaximumTemplateMatchingCandidateCount,
                ExcludeSourceRegion = false
            };
        }

        private void ShowTemplateGuide(
            IWpfTemplateMatchingAutoLabelHost currentHost,
            string title,
            string message)
        {
            string guide = presentationService.BuildGuideBody(message);
            currentHost.SetAutoLabelGlobalInferenceStatus(title, isBusy: false, isWarning: true);
            currentHost.SetAutoLabelPythonStatus($"Template guide: {title}");
            currentHost.AppendAutoLabelLog($"Template guide: {title} / {message}");
            currentHost.ShowAutoLabelGuide(title, guide);
        }
    }
}
