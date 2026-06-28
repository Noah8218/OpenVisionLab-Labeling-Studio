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
        private void PopulateProjectConfigPanelFields()
        {
            string recipeName = GetCurrentRecipeName();
            string configPath = GetCurrentRecipeConfigPath();
            ProjectConfigViewModel?.LoadFrom(recipeName, GetRecipeRootDirectory());

            PopulateProjectRecipeList(recipeName);

            SetProjectConfigStatus(string.IsNullOrWhiteSpace(recipeName)
                ? "Recipe 이름이 아직 없습니다. 저장 전에 recipe를 선택하거나 생성해야 합니다."
                : $"현재 설정 파일: {Path.GetFileName(configPath)}");
            UpdateYoloCommandButtons();
        }




    }
}
