using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Step completion is a lightweight UI projection used after image, label, training, and inference updates.
        private void RefreshYoloTrainingStepCompletion(YoloDatasetReadinessReport report = null)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            report ??= lastYoloTrainingReadinessReport;
            int classCount = global.Data?.ClassNamedList?.Count ?? 0;
            string recipeName = GetCurrentRecipeName();
            bool hasDatasetSetup = (!string.IsNullOrWhiteSpace(recipeName)
                    && File.Exists(LabelingDatasetManifestService.GetManifestPath(recipeName)))
                || (!string.IsNullOrWhiteSpace(global.Data?.OutputRootPath)
                    && Directory.Exists(global.Data.OutputRootPath));
            YoloDatasetStatistics statistics = report?.Statistics;
            int savedObjectCount = statistics?.TotalObjectCount ?? 0;
            int totalImageCount = statistics?.TotalImageCount > 0
                ? statistics.TotalImageCount
                : imageQueueItems.Count;
            int completedImageCount = statistics != null && totalImageCount > 0
                ? Math.Min(statistics.TotalLabelFileCount, totalImageCount)
                : imageQueueItems.Count(WpfImageQueueFilterService.IsCompletedQueueItem);
            bool hasImages = imageQueueItems.Count > 0 || !string.IsNullOrWhiteSpace(activeImagePath);
            bool hasClasses = classCount > 0;
            bool hasAnyLabelWork = manualRois.Count > 0
                || confirmedDetectionCandidates.Count > 0
                || savedObjectCount > 0
                || imageQueueItems.Any(item => item.IsLabeled);
            bool isLabelingComplete = hasImages && totalImageCount > 0
                ? completedImageCount >= totalImageCount
                : hasAnyLabelWork;
            string labelingStateText = isLabelingComplete
                ? "완료"
                : completedImageCount > 0 && totalImageCount > 0
                    ? $"{completedImageCount}/{totalImageCount}"
                    : "라벨 필요";
            bool datasetReady = report?.IsReady == true;

            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            string trainingState = status?.LastTrainingState?.Trim() ?? string.Empty;
            bool hasTrainingStatus = HasTrainingStatus(status);
            bool trainingCompletedFromWorker = WpfTrainingWeightsService.IsCompletedTrainingState(trainingState);
            bool hasCompletedCurrentDatasetTraining = !trainingCompletedFromWorker
                && BuildCurrentTrainingWeightsComparison()?.HasCompletedCurrentDatasetTraining == true;
            bool trainingCompleted = trainingCompletedFromWorker || hasCompletedCurrentDatasetTraining;
            bool trainingRunning = hasTrainingStatus && !trainingCompleted && !IsTerminalTrainingState(trainingState);
            bool hasInferenceResult = pendingDetectionCandidates.Count > 0
                || imageQueueItems.Any(item => item.ReviewState == YoloImageReviewState.Candidate);

            LearningWorkflowViewModel.SetYoloTrainingStepState(1, hasDatasetSetup, hasDatasetSetup ? "완료" : "데이터셋 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(2, hasImages, hasImages ? "완료" : "이미지 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(3, hasClasses, hasClasses ? "완료" : "클래스 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(4, isLabelingComplete, labelingStateText);
            LearningWorkflowViewModel.SetYoloTrainingStepState(5, datasetReady, datasetReady ? "완료" : "점검 필요");
            LearningWorkflowViewModel.SetYoloTrainingStepState(6, trainingCompleted, trainingCompleted ? "완료" : trainingRunning ? "진행 중" : "대기");
            LearningWorkflowViewModel.SetYoloTrainingStepState(7, hasInferenceResult, hasInferenceResult ? "후보 있음" : "추론 필요");
            LearningWorkflowViewModel.SetYoloFixActionAvailability(
                canFixClasses: true,
                canFixLabels: hasImages,
                canFixDataset: true);
        }
    }
}
