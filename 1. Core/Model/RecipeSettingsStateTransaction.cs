using System;
using System.Collections.Generic;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps mutable Recipe settings aligned with a later Recipe save.
    /// ModelRegistry has its own transaction because it owns a separate object
    /// graph and decision history; this transaction covers the settings and
    /// TrainingGuide state changed by WPF model/training and model-decision save
    /// commands.
    /// </summary>
    public sealed class RecipeSettingsStateTransaction : IDisposable
    {
        private readonly LabelingProjectData data;
        private readonly PythonModelSettings pythonModelSnapshot;
        private readonly AnomalyClassificationSettings anomalyClassificationSnapshot;
        private readonly TrainingSettings trainingSnapshot;
        private readonly YoloDatasetSettings yoloDatasetSnapshot;
        private readonly YoloV5TrainingParameters trainingParamSnapshot;
        private readonly YoloTrainingGuideHistory trainingGuideSnapshot;
        private bool completed;

        public RecipeSettingsStateTransaction(LabelingProjectData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.data.ProjectSettings ??= new LabelingProjectSettings();
            this.data.ProjectSettings.EnsureDefaults();
            this.data.TranningParam ??= new YoloV5TrainingParameters();

            pythonModelSnapshot = Clone(this.data.ProjectSettings.PythonModel);
            anomalyClassificationSnapshot = Clone(this.data.ProjectSettings.AnomalyClassification);
            trainingSnapshot = Clone(this.data.ProjectSettings.Training);
            yoloDatasetSnapshot = Clone(this.data.ProjectSettings.YoloDataset);
            trainingParamSnapshot = Clone(this.data.TranningParam);
            trainingGuideSnapshot = Clone(this.data.ProjectSettings.TrainingGuide);
        }

        public void Commit() => completed = true;

        public void Rollback()
        {
            if (completed)
            {
                return;
            }

            Restore(data.ProjectSettings.PythonModel, pythonModelSnapshot);
            Restore(data.ProjectSettings.AnomalyClassification, anomalyClassificationSnapshot);
            Restore(data.ProjectSettings.Training, trainingSnapshot);
            Restore(data.ProjectSettings.YoloDataset, yoloDatasetSnapshot);
            Restore(data.TranningParam, trainingParamSnapshot);
            Restore(data.ProjectSettings.TrainingGuide, trainingGuideSnapshot);
            completed = true;
        }

        public void Dispose() => Rollback();

        private static PythonModelSettings Clone(PythonModelSettings source)
        {
            return new PythonModelSettings
            {
                PythonExecutablePath = source?.PythonExecutablePath ?? string.Empty,
                ModelEngine = source?.ModelEngine ?? PythonModelSettings.EngineYoloV5,
                ProjectRootPath = source?.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source?.ClientScriptPath ?? string.Empty,
                WeightsPath = source?.WeightsPath ?? string.Empty,
                ImageRootPath = source?.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source?.MinimumDetectionConfidence ?? 0.25F,
                MaximumDetectionCandidates = source?.MaximumDetectionCandidates ?? 20,
                InferenceImageSize = source?.InferenceImageSize ?? 320,
                DetectionTimeoutSeconds = source?.DetectionTimeoutSeconds ?? 30,
                AutoStartClient = source?.AutoStartClient ?? true
            };
        }

        private static AnomalyClassificationSettings Clone(AnomalyClassificationSettings source)
        {
            return new AnomalyClassificationSettings
            {
                NormalClassNames = new List<string>(source?.NormalClassNames ?? new List<string>()),
                AbnormalClassNames = new List<string>(source?.AbnormalClassNames ?? new List<string>()),
                MinimumConfidence = source?.MinimumConfidence ?? 0D
            };
        }

        private static TrainingSettings Clone(TrainingSettings source)
        {
            return new TrainingSettings
            {
                ImageSize = source?.ImageSize ?? 320,
                Batch = source?.Batch ?? 16,
                Epoch = source?.Epoch ?? 50,
                Cfg = source?.Cfg ?? YoloV5TrainingParameters.Cfg.yolov5x.ToString(),
                Weight = source?.Weight ?? YoloV5TrainingParameters.Weight.yolov5x.ToString()
            };
        }

        private static YoloDatasetSettings Clone(YoloDatasetSettings source)
        {
            return new YoloDatasetSettings
            {
                OutputRootPath = source?.OutputRootPath ?? string.Empty,
                DataYamlFilePath = source?.DataYamlFilePath ?? string.Empty,
                ValidationPercent = source?.ValidationPercent ?? 20,
                TestPercent = source?.TestPercent ?? 0,
                SplitSeed = source?.SplitSeed ?? 17
            };
        }

        private static YoloV5TrainingParameters Clone(YoloV5TrainingParameters source)
        {
            return new YoloV5TrainingParameters
            {
                imageSize = source?.imageSize ?? 320,
                batch = source?.batch ?? 16,
                epoch = source?.epoch ?? 50,
                cfg = source?.cfg ?? YoloV5TrainingParameters.Cfg.yolov5x,
                weight = source?.weight ?? YoloV5TrainingParameters.Weight.yolov5x
            };
        }

        private static YoloTrainingGuideHistory Clone(YoloTrainingGuideHistory source)
        {
            return new YoloTrainingGuideHistory
            {
                LastDatasetCheckUtc = source?.LastDatasetCheckUtc ?? string.Empty,
                LastDatasetReady = source?.LastDatasetReady == true,
                LastDatasetIssueKind = source?.LastDatasetIssueKind ?? string.Empty,
                LastDatasetSummary = source?.LastDatasetSummary ?? string.Empty,
                LastTrainingUpdateUtc = source?.LastTrainingUpdateUtc ?? string.Empty,
                LastTrainingState = source?.LastTrainingState ?? string.Empty,
                LastTrainingProgressPercent = source?.LastTrainingProgressPercent ?? -1,
                LastTrainingMessage = source?.LastTrainingMessage ?? string.Empty,
                LastTrainingRunId = source?.LastTrainingRunId ?? string.Empty,
                LastTrainingRunName = source?.LastTrainingRunName ?? string.Empty,
                LastTrainingDatasetVersionId = source?.LastTrainingDatasetVersionId ?? string.Empty,
                LastTrainingDatasetContentSha256 = source?.LastTrainingDatasetContentSha256 ?? string.Empty,
                AppliedWeightsPath = source?.AppliedWeightsPath ?? string.Empty,
                AppliedWeightsUtc = source?.AppliedWeightsUtc ?? string.Empty,
                AppliedWeightsSavedToRecipe = source?.AppliedWeightsSavedToRecipe == true,
                RunHistory = CloneRunHistory(source?.RunHistory)
            };
        }

        private static List<YoloTrainingGuideRunRecord> CloneRunHistory(IEnumerable<YoloTrainingGuideRunRecord> source)
        {
            var result = new List<YoloTrainingGuideRunRecord>();
            foreach (YoloTrainingGuideRunRecord item in source ?? Array.Empty<YoloTrainingGuideRunRecord>())
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new YoloTrainingGuideRunRecord
                {
                    EventUtc = item.EventUtc ?? string.Empty,
                    EventKind = item.EventKind ?? string.Empty,
                    DatasetReady = item.DatasetReady,
                    DatasetIssueKind = item.DatasetIssueKind ?? string.Empty,
                    DatasetSummary = item.DatasetSummary ?? string.Empty,
                    TrainingState = item.TrainingState ?? string.Empty,
                    TrainingProgressPercent = item.TrainingProgressPercent,
                    TrainingMessage = item.TrainingMessage ?? string.Empty,
                    TrainingRunId = item.TrainingRunId ?? string.Empty,
                    TrainingRunName = item.TrainingRunName ?? string.Empty,
                    AppliedWeightsPath = item.AppliedWeightsPath ?? string.Empty,
                    AppliedWeightsSavedToRecipe = item.AppliedWeightsSavedToRecipe
                });
            }

            return result;
        }

        private static void Restore(PythonModelSettings target, PythonModelSettings source)
        {
            target ??= new PythonModelSettings();
            target.PythonExecutablePath = source.PythonExecutablePath;
            target.ModelEngine = source.ModelEngine;
            target.ProjectRootPath = source.ProjectRootPath;
            target.ClientScriptPath = source.ClientScriptPath;
            target.WeightsPath = source.WeightsPath;
            target.ImageRootPath = source.ImageRootPath;
            target.MinimumDetectionConfidence = source.MinimumDetectionConfidence;
            target.MaximumDetectionCandidates = source.MaximumDetectionCandidates;
            target.InferenceImageSize = source.InferenceImageSize;
            target.DetectionTimeoutSeconds = source.DetectionTimeoutSeconds;
            target.AutoStartClient = source.AutoStartClient;
        }

        private static void Restore(AnomalyClassificationSettings target, AnomalyClassificationSettings source)
        {
            target ??= new AnomalyClassificationSettings();
            target.NormalClassNames = new List<string>(source.NormalClassNames);
            target.AbnormalClassNames = new List<string>(source.AbnormalClassNames);
            target.MinimumConfidence = source.MinimumConfidence;
        }

        private static void Restore(TrainingSettings target, TrainingSettings source)
        {
            target ??= new TrainingSettings();
            target.ImageSize = source.ImageSize;
            target.Batch = source.Batch;
            target.Epoch = source.Epoch;
            target.Cfg = source.Cfg;
            target.Weight = source.Weight;
        }

        private static void Restore(YoloDatasetSettings target, YoloDatasetSettings source)
        {
            target ??= new YoloDatasetSettings();
            target.OutputRootPath = source.OutputRootPath;
            target.DataYamlFilePath = source.DataYamlFilePath;
            target.ValidationPercent = source.ValidationPercent;
            target.TestPercent = source.TestPercent;
            target.SplitSeed = source.SplitSeed;
        }

        private static void Restore(YoloV5TrainingParameters target, YoloV5TrainingParameters source)
        {
            target ??= new YoloV5TrainingParameters();
            target.imageSize = source.imageSize;
            target.batch = source.batch;
            target.epoch = source.epoch;
            target.cfg = source.cfg;
            target.weight = source.weight;
        }

        private static void Restore(YoloTrainingGuideHistory target, YoloTrainingGuideHistory source)
        {
            target ??= new YoloTrainingGuideHistory();
            target.LastDatasetCheckUtc = source.LastDatasetCheckUtc;
            target.LastDatasetReady = source.LastDatasetReady;
            target.LastDatasetIssueKind = source.LastDatasetIssueKind;
            target.LastDatasetSummary = source.LastDatasetSummary;
            target.LastTrainingUpdateUtc = source.LastTrainingUpdateUtc;
            target.LastTrainingState = source.LastTrainingState;
            target.LastTrainingProgressPercent = source.LastTrainingProgressPercent;
            target.LastTrainingMessage = source.LastTrainingMessage;
            target.LastTrainingRunId = source.LastTrainingRunId;
            target.LastTrainingRunName = source.LastTrainingRunName;
            target.LastTrainingDatasetVersionId = source.LastTrainingDatasetVersionId;
            target.LastTrainingDatasetContentSha256 = source.LastTrainingDatasetContentSha256;
            target.AppliedWeightsPath = source.AppliedWeightsPath;
            target.AppliedWeightsUtc = source.AppliedWeightsUtc;
            target.AppliedWeightsSavedToRecipe = source.AppliedWeightsSavedToRecipe;
            target.RunHistory = CloneRunHistory(source.RunHistory);
        }
    }
}
