using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Threading;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloTrainingWorkflowService
    {
        private readonly YoloTrainingDatasetPreparationService datasetPreparationService =
            new YoloTrainingDatasetPreparationService();

        public const string AnomalyClassificationRuntimeError =
            YoloTrainingDatasetPreparationService.AnomalyClassificationRuntimeError;

        public string LastPreparationFailureMessage { get; private set; } = string.Empty;

        public bool TryStartTraining(
            LabelingProjectData data,
            PythonModelCommunication communication,
            string runName = "",
            string recipeName = "")
        {
            if (!TryPrepareTrainingDataset(data, out YoloTrainingDatasetRequest trainingRequest))
            {
                return false;
            }

            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 학습 통신이 초기화되지 않았습니다.");
                return false;
            }

            RecipeDatasetVersionSnapshot recipeSnapshot = null;
            if (!trainingRequest.IsExternalSource)
            {
                try
                {
                    recipeSnapshot = RecipeDatasetVersionService.CreateSnapshot(data);
                    trainingRequest.DatasetVersionId = recipeSnapshot.DatasetVersionId;
                    trainingRequest.DatasetContentSha256 = recipeSnapshot.ContentSha256;
                    if (!string.IsNullOrWhiteSpace(recipeName))
                    {
                        LabelingDatasetManifestService.Save(data, recipeName, recipeSnapshot);
                    }
                }
                catch (Exception ex) when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is InvalidDataException)
                {
                    LastPreparationFailureMessage = "Recipe Dataset Version v2 capture failed: " + ex.Message;
                    AppLog.ABNORMAL(LastPreparationFailureMessage);
                    return false;
                }
            }

            TrainingSettings training = data.GetTrainingSettings();
            string model = data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5";
            string weightFile = ResolveTrainingWeightFile(training.Weight, model, trainingRequest.Task);
            bool sent = communication.SendTrainingData(
                PythonModelCommunication.CommandLearning.StartTraining.ToString(),
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
            else
            {
                data.ProjectSettings.TrainingGuide.LastTrainingDatasetVersionId =
                    trainingRequest.DatasetVersionId ?? string.Empty;
                data.ProjectSettings.TrainingGuide.LastTrainingDatasetContentSha256 =
                    trainingRequest.DatasetContentSha256 ?? string.Empty;
                if (trainingRequest.IsExternalSource)
                {
                    YoloExternalDatasetIntakeService.RecordTrainingRequest(
                        data?.ProjectSettings?.ExternalYoloDataset,
                        data?.ProjectSettings?.PythonModel,
                        model,
                        trainingRequest.Task,
                        weightFile,
                        runName,
                        trainingRequest.SourceFingerprintSha256,
                        trainingRequest.RuntimeDataYamlFilePath);
                }
            }

            return sent;
        }

        public bool TryStopTraining(
            PythonModelCommunication communication,
            YoloPythonClientProcessService processService = null,
            CancellationToken cancellationToken = default)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO 학습 통신이 초기화되지 않았습니다.");
                return false;
            }

            communication.MarkTrainingStopRequested();
            bool sent = communication.SendStopTraining();
            if (!sent)
            {
                AppLog.ABNORMAL("Python 모델 클라이언트가 연결되지 않아 학습 중지 명령을 보내지 못했습니다.");
                return false;
            }

            if (communication.WaitForTrainingStop(TimeSpan.FromSeconds(30), cancellationToken))
            {
                return true;
            }

            if (processService == null)
            {
                AppLog.ABNORMAL("Python 모델 클라이언트가 학습 중지 확인을 반환하지 않았습니다.");
                return false;
            }

            bool processStopped = processService.StopAndWait(TimeSpan.FromSeconds(5));
            if (processStopped)
            {
                communication.MarkTrainingStopped("학습 중지 응답 시간 초과 후 Python 프로세스를 종료했습니다.");
            }
            else
            {
                AppLog.ABNORMAL("Python 모델 클라이언트 프로세스의 종료를 확인하지 못했습니다.");
            }

            return processStopped;
        }

        public bool TryPrepareTrainingDataset(LabelingProjectData data)
        {
            return TryPrepareTrainingDataset(data, out _);
        }

        private bool TryPrepareTrainingDataset(LabelingProjectData data, out YoloTrainingDatasetRequest trainingRequest)
        {
            bool prepared = datasetPreparationService.TryPrepare(data, out trainingRequest);
            LastPreparationFailureMessage = datasetPreparationService.LastPreparationFailureMessage;
            return prepared;
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

            if (normalizedModel == "unet")
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(weight) ? "yolov5s.pt" : $"{weight}.pt";
        }

    }
}
