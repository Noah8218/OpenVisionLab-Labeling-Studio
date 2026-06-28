using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void NotifyYoloPathSelected(string label, string selectedPath)
        {
            SetYoloCommandStatus($"{label} 선택됨. 저장 후 첫 점검을 누르세요.", isBusy: false);
            AppendLog($"{label} 선택됨: {selectedPath}");
        }

        private void RefreshYoloStatus()
        {
            EnsureProjectSettings();
            PythonModelValidationResult result = PythonModelSettingsValidator.Validate(global.Data.ProjectSettings.PythonModel, requireWeights: true);
            SetPythonStatus(result.IsValid ? "\uCD94\uB860: \uC900\uBE44 \uC644\uB8CC" : "\uCD94\uB860: \uC810\uAC80 \uD544\uC694");
            SetModelStatus(File.Exists(global.Data.ProjectSettings.PythonModel.WeightsPath)
                ? $"모델: {Path.GetFileName(global.Data.ProjectSettings.PythonModel.WeightsPath)}"
                : "모델: 가중치 없음");
        }

        private void SaveYoloEditorFields()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            YoloModelSettingsViewModel?.ApplyTo(settings);
            YoloModelSettingsViewModel?.LoadFrom(settings);
            CandidateConfidenceSlider.Value = Math.Clamp(settings.MinimumDetectionConfidence, 0F, 1F);
        }

        private void SaveTrainingEditorFields()
        {
            EnsureProjectSettings();
            TrainingSettings training = global.Data.ProjectSettings.Training;
            TrainingSettingsViewModel?.ApplyTo(training, global.Data.ProjectSettings.YoloDataset, global.Data.TrainingParam);
            PopulateTrainingEditorFields();
        }



    }
}
