using Lib.Common;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Saving/applying a recipe reloads dependent panels so stale model state cannot survive a recipe switch.
        private bool SaveProjectConfigFromPanel()
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus("Recipe 이름이 없어 설정을 저장하지 않았습니다.");
                return false;
            }

            try
            {
                CRecipe.InitDirectory(recipeName);
                global.Data.SaveConfig(recipeName);
                PopulateProjectConfigPanelFields();
                string configPath = GetCurrentRecipeConfigPath();
                SetProjectConfigStatus($"설정 저장 완료: {DateTime.Now:HH:mm:ss}");
                SetDatasetStatus($"데이터셋: 설정 저장 {Path.GetFileName(configPath)}");
                AppendLog($"프로젝트 설정 저장: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"설정 저장 실패: {ex.Message}");
                AppendLog($"프로젝트 설정 저장 실패: {ex.Message}");
                return false;
            }
        }

        private void ApplyProjectRecipeFromPanel()
        {
            string recipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                SetProjectConfigStatus("적용할 recipe 이름을 입력하세요.");
                return;
            }

            if (!WpfProjectRecipeService.IsValidRecipeName(recipeName))
            {
                SetProjectConfigStatus("Recipe 이름에 사용할 수 없는 문자가 있습니다.");
                return;
            }

            try
            {
                string previousRecipeName = GetCurrentRecipeName();
                global.Recipe.Name = recipeName;
                RememberLastOpenedDatasetRecipe(recipeName);
                EnsureProjectSettings();
                ApplyProjectDatasetPurposeToWorkflow();
                // Recipe changes reload every dependent panel so stale labels, weights, and class lists do not survive the switch.
                PopulateProjectConfigPanelFields();
                PopulateYoloEditorFields();
                PopulateTrainingEditorFields();
                PopulateClassList();
                RefreshCandidateList();
                RefreshObjectList();
                RefreshTrainingReadinessPanel(refreshYaml: false);
                SetDatasetStatus($"데이터셋: recipe {recipeName}");
                SetProjectConfigStatus(string.Equals(previousRecipeName, recipeName, StringComparison.OrdinalIgnoreCase)
                    ? $"Recipe 재적용: {recipeName}"
                    : $"Recipe 적용: {recipeName}");
                AppendLog($"Recipe 적용: {recipeName}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"Recipe 적용 실패: {ex.Message}");
                AppendLog($"Recipe 적용 실패: {ex.Message}");
            }
        }
    }
}
