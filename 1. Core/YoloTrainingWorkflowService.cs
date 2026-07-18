using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloTrainingWorkflowService
    {
        public string LastPreparationFailureMessage { get; private set; } = string.Empty;

        public bool TryStartTraining(CData data, CCommunicationLearning communication, string runName = "")
        {
            if (!TryPrepareTrainingDataset(data, out TrainingDatasetRequest trainingRequest))
            {
                return false;
            }

            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 학습 통신이 초기화되지 않았습니다.");
                return false;
            }

            TrainingSettings training = data.GetTrainingSettings();
            string model = data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5";
            string weightFile = ResolveTrainingWeightFile(training.Weight, model, trainingRequest.Task);
            bool sent = communication.SendTrainingData(
                CCommunicationLearning.CommandLearning.StartTraining.ToString(),
                training.ImageSize.ToString(),
                training.Batch.ToString(),
                training.Epoch.ToString(),
                $"{training.Cfg}.yaml",
                weightFile,
                trainingRequest.DataPath,
                model,
                trainingRequest.Task,
                runName);

            if (!sent)
            {
                AppLog.ABNORMAL("Python 모델 클라이언트가 연결되지 않아 학습 시작 명령을 보내지 못했습니다.");
            }
            else if (trainingRequest.IsExternalSource)
            {
                YoloExternalDatasetIntakeService.RecordTrainingRequest(
                    data?.ProjectSettings?.ExternalYoloDataset,
                    data?.ProjectSettings?.PythonModel,
                    model,
                    trainingRequest.Task,
                    weightFile,
                    runName,
                    trainingRequest.SourceFingerprintSha256);
            }

            return sent;
        }

        public bool TryStopTraining(CCommunicationLearning communication)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 학습 통신이 초기화되지 않았습니다.");
                return false;
            }

            bool sent = communication.Send(CCommunicationLearning.CommandLearning.StopTraining.ToString());
            if (!sent)
            {
                AppLog.ABNORMAL("Python 모델 클라이언트가 연결되지 않아 학습 중지 명령을 보내지 못했습니다.");
            }

            return sent;
        }

        public bool TryPrepareTrainingDataset(CData data)
        {
            return TryPrepareTrainingDataset(data, out _);
        }

        private bool TryPrepareTrainingDataset(CData data, out TrainingDatasetRequest trainingRequest)
        {
            LastPreparationFailureMessage = string.Empty;
            trainingRequest = new TrainingDatasetRequest
            {
                DataPath = data?.DataYamlFilePath ?? string.Empty,
                Task = ResolveTrainingTask(data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection)
            };

            data?.ProjectSettings?.EnsureDefaults();
            ExternalYoloDatasetSettings externalDataset = data?.ProjectSettings?.ExternalYoloDataset;
            if (externalDataset?.RequiresExplicitReactivation == true)
            {
                LastPreparationFailureMessage = string.IsNullOrWhiteSpace(externalDataset.LastValidationSummary)
                    ? "External YOLO data.yaml requires explicit revalidation and activation before training."
                    : externalDataset.LastValidationSummary;
                AppLog.ABNORMAL($"External YOLO training dataset requires explicit reactivation: {LastPreparationFailureMessage}");
                return false;
            }

            if (externalDataset?.UseForTraining == true)
            {
                return TryPrepareExternalYoloTrainingDataset(externalDataset, out trainingRequest);
            }

            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                return TryPrepareAnomalyClassificationTrainingDataset(data, out trainingRequest);
            }

            YoloSegmentationTrainingLabelExportResult segmentationExportResult = null;
            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
            {
                segmentationExportResult = YoloSegmentationTrainingLabelService.Export(data);
                foreach (string error in segmentationExportResult.Errors)
                {
                    AppLog.ABNORMAL($"YOLO segmentation label export failed: {error}");
                }

                if (!segmentationExportResult.IsReady)
                {
                    LastPreparationFailureMessage = string.Join(Environment.NewLine, segmentationExportResult.Errors);
                    return false;
                }
            }

            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml: true);
            foreach (string error in report.Errors)
            {
                AppLog.ABNORMAL($"YOLO 학습 준비 점검 실패: {error}");
            }

            if (!report.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, report.Errors);
                return false;
            }

            foreach (string line in report.SummaryLines)
            {
                AppLog.NORMAL(line);
            }

            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
            {
                AppLog.NORMAL($"YOLO segmentation labels ready. Images:{segmentationExportResult.ImageCount}, LabelFiles:{segmentationExportResult.LabelFileCount}, Polygons:{segmentationExportResult.PolygonCount}, Backgrounds:{segmentationExportResult.BackgroundImageCount}");
            }

            return true;
        }

        private bool TryPrepareExternalYoloTrainingDataset(
            ExternalYoloDatasetSettings externalDataset,
            out TrainingDatasetRequest trainingRequest)
        {
            YoloExternalDatasetIntakeReport report = YoloExternalDatasetIntakeService.Build(
                externalDataset?.DataYamlFilePath,
                externalDataset?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            if (!report.IsReady)
            {
                YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
                LastPreparationFailureMessage = string.Join(Environment.NewLine, report.Errors);
                foreach (string error in report.Errors)
                {
                    AppLog.ABNORMAL($"External YOLO training dataset validation failed: {error}");
                }

                trainingRequest = new TrainingDatasetRequest();
                return false;
            }

            if (!YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(externalDataset, report, out string identityError))
            {
                YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
                YoloExternalDatasetIntakeService.MarkSourceIdentityRequiresReactivation(externalDataset, identityError);
                LastPreparationFailureMessage = identityError;
                AppLog.ABNORMAL($"External YOLO training dataset source identity changed: {identityError}");
                trainingRequest = new TrainingDatasetRequest();
                return false;
            }

            YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);

            trainingRequest = new TrainingDatasetRequest
            {
                DataPath = report.DataYamlFilePath,
                Task = ResolveTrainingTask(report.Purpose),
                IsExternalSource = true,
                SourceFingerprintSha256 = report.SourceFingerprintSha256
            };
            AppLog.NORMAL($"External native YOLO dataset ready. {report.Summary} / Path:{report.DataYamlFilePath}");
            return true;
        }

        private bool TryPrepareAnomalyClassificationTrainingDataset(CData data, out TrainingDatasetRequest trainingRequest)
        {
            trainingRequest = new TrainingDatasetRequest
            {
                DataPath = string.Empty,
                Task = "classify"
            };

            AnomalyClassificationTrainingReadinessReport readiness =
                AnomalyClassificationTrainingReadinessService.Build(data);
            if (!readiness.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, readiness.Errors);
                foreach (string error in readiness.Errors)
                {
                    AppLog.ABNORMAL($"YOLO anomaly classification training failed: {error}");
                }

                return false;
            }

            AnomalyClassificationDatasetExportResult result;
            try
            {
                var exportService = new AnomalyClassificationDatasetExportService();
                result = exportService.Export(data, readiness.SourceImagePaths);
            }
            catch (Exception ex)
            {
                LastPreparationFailureMessage = $"classification dataset export failed. {ex.Message}";
                AppLog.ABNORMAL($"YOLO anomaly classification training failed: {LastPreparationFailureMessage}");
                return false;
            }
            if (result.NormalImageCount == 0 || result.AbnormalImageCount == 0)
            {
                LastPreparationFailureMessage = $"{AnomalyClassificationTrainingReadinessService.NeedsReviewedNormalAndAbnormalError}. Normal:{result.NormalImageCount}, Abnormal:{result.AbnormalImageCount}";
                AppLog.ABNORMAL($"YOLO anomaly classification training failed: {LastPreparationFailureMessage}");
                return false;
            }

            AppLog.NORMAL($"YOLO anomaly classification dataset ready. Normal:{result.NormalImageCount}, Abnormal:{result.AbnormalImageCount}, Skipped:{result.SkippedImageCount}, Path:{result.DatasetRootPath}");
            trainingRequest.DataPath = result.DatasetRootPath;
            return true;
        }

        private static string ResolveTrainingTask(LabelingDatasetPurpose datasetPurpose)
        {
            return datasetPurpose == LabelingDatasetPurpose.Segmentation
                ? "segment"
                : "detect";
        }

        private static string ResolveTrainingWeightFile(string weight, string model, string task)
        {
            string normalizedModel = (model ?? string.Empty).Trim().ToLowerInvariant();
            string normalizedTask = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedModel == "yolo11")
            {
                return normalizedTask == "segment"
                    ? "yolo11n-seg.pt"
                    : normalizedTask == "classify"
                        ? "yolo11n-cls.pt"
                        : "yolo11n.pt";
            }

            if (normalizedModel == "yolov8")
            {
                return normalizedTask == "segment"
                    ? "yolov8n-seg.pt"
                    : normalizedTask == "classify"
                        ? "yolov8n-cls.pt"
                        : "yolov8n.pt";
            }

            return string.IsNullOrWhiteSpace(weight) ? "yolov5s.pt" : $"{weight}.pt";
        }

        private sealed class TrainingDatasetRequest
        {
            public string DataPath { get; set; } = string.Empty;

            public string Task { get; set; } = "detect";

            public bool IsExternalSource { get; set; }

            public string SourceFingerprintSha256 { get; set; } = string.Empty;
        }
    }
}
