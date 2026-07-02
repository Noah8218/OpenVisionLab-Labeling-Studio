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
                AppLog.ABNORMAL("YOLO 학습 통신이 초기화되지 않았습니다.");
                return false;
            }

            TrainingSettings training = data.GetTrainingSettings();
            string model = data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5";
            bool sent = communication.SendTrainingData(
                CCommunicationLearning.CommandLearning.StartTraining.ToString(),
                training.ImageSize.ToString(),
                training.Batch.ToString(),
                training.Epoch.ToString(),
                $"{training.Cfg}.yaml",
                $"{training.Weight}.pt",
                data.DataYamlFilePath,
                model);

            if (!sent)
            {
                AppLog.ABNORMAL("Python 모델 클라이언트가 연결되지 않아 학습 시작 명령을 보내지 못했습니다.");
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
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml: true);
            foreach (string error in report.Errors)
            {
                AppLog.ABNORMAL($"YOLO 학습 준비 점검 실패: {error}");
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
