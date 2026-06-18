using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloTrainingWorkflowService
    {
        public bool TryStartTraining(CData data, CCommunicationLearning communication)
        {
            if (!TryPrepareTrainingDataset(data))
            {
                return false;
            }

            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO training communication is not initialized.");
                return false;
            }

            TrainingSettings training = data.GetTrainingSettings();
            bool sent = communication.SendTrainingData(
                CCommunicationLearning.CommandLearning.StartTraining.ToString(),
                training.ImageSize.ToString(),
                training.Batch.ToString(),
                training.Epoch.ToString(),
                $"{training.Cfg}.yaml",
                $"{training.Weight}.pt",
                data.DataYamlFilePath);

            if (!sent)
            {
                AppLog.ABNORMAL("StartTraining was not sent because the Python model client is not connected.");
            }

            return sent;
        }

        public bool TryStopTraining(CCommunicationLearning communication)
        {
            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO training communication is not initialized.");
                return false;
            }

            bool sent = communication.Send(CCommunicationLearning.CommandLearning.StopTraining.ToString());
            if (!sent)
            {
                AppLog.ABNORMAL("StopTraining was not sent because the Python model client is not connected.");
            }

            return sent;
        }

        public bool TryPrepareTrainingDataset(CData data)
        {
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml: true);
            foreach (string error in report.Errors)
            {
                AppLog.ABNORMAL($"YOLO training validation failed: {error}");
            }

            if (!report.IsReady)
            {
                return false;
            }

            foreach (string line in report.SummaryLines)
            {
                AppLog.NORMAL(line);
            }

            return true;
        }
    }
}
