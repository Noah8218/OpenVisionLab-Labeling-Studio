using System;
using System.Diagnostics;
using System.IO;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Training-guide workflow commands navigate the operator through dataset and YOLO setup steps.
        private void ExecuteFixYoloClassesCommand()
        {
            ExecuteYoloTrainingWorkflowStep(3, LearningWorkflowPanelControl);
        }

        private void ExecuteFixYoloLabelsCommand()
        {
            ExecuteYoloTrainingWorkflowStep(4, LearningWorkflowPanelControl);
        }

        private void ExecuteFixYoloDatasetCommand()
        {
            ExecuteYoloTrainingWorkflowStep(5, LearningWorkflowPanelControl);
        }

        private void ExecuteDatasetDashboardMetricCommand(WpfDatasetDashboardMetricItem metric)
        {
            if (metric == null)
            {
                return;
            }

            // Dashboard cards are shortcuts into the already-verified guide workflow.
            // Keep the routing here so card titles can change without breaking navigation.
            switch (metric.ActionKind)
            {
                case WpfDatasetDashboardActionKind.OpenImages:
                    ExecuteYoloTrainingWorkflowStep(2, LearningWorkflowPanelControl);
                    break;

                case WpfDatasetDashboardActionKind.OpenClassCatalog:
                    ExecuteYoloTrainingWorkflowStep(3, LearningWorkflowPanelControl);
                    break;

                case WpfDatasetDashboardActionKind.OpenLabelingProgress:
                case WpfDatasetDashboardActionKind.OpenLabelingTool:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    SelectAnnotationTool(ResolvePrimaryLabelingToolForCurrentPurpose(), revealInGuide: true);
                    MainCanvasView?.Focus();
                    SetModelStatus("\uB77C\uBCA8\uB9C1: \uD544\uC694\uD55C \uB77C\uBCA8 \uB3C4\uAD6C\uB85C \uC774\uB3D9\uD588\uC2B5\uB2C8\uB2E4.");
                    AppendLog("Guide \uB370\uC774\uD130\uC14B \uCE74\uB4DC: \uB77C\uBCA8\uB9C1 \uB3C4\uAD6C\uB85C \uC774\uB3D9");
                    break;

                case WpfDatasetDashboardActionKind.CheckDataset:
                    ExecuteYoloTrainingWorkflowStep(5, LearningWorkflowPanelControl);
                    break;

                case WpfDatasetDashboardActionKind.OpenDatasetSettings:
                    FocusYoloTrainingSettingsTab();
                    RefreshTrainingReadinessPanel(refreshYaml: true);
                    SetModelStatus("\uB370\uC774\uD130\uC14B \uBD84\uD560/\uAC80\uC99D \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694.");
                    AppendLog("Guide \uB370\uC774\uD130\uC14B \uCE74\uB4DC: \uBD84\uD560/\uAC80\uC99D \uC124\uC815\uC73C\uB85C \uC774\uB3D9");
                    break;

                default:
                    ExecuteYoloTrainingWorkflowStep(5, LearningWorkflowPanelControl);
                    break;
            }
        }

        private void ExecuteFirstRunSamplePathCommand(WpfFirstRunChecklistItem item)
        {
            if (item == null || item.ShortcutWorkflowStepOrder <= 0)
            {
                AppendLog("Guide first-run shortcut was ignored because no workflow target was defined.");
                return;
            }

            ExecuteYoloTrainingWorkflowStep(item.ShortcutWorkflowStepOrder, LearningWorkflowPanelControl);
        }

        private void ExecuteOpenTutorialHtmlGuideCommand()
        {
            string path = ResolveTutorialHtmlGuidePath();
            if (!File.Exists(path))
            {
                SetModelStatus("\uD29C\uD1A0\uB9AC\uC5BC HTML\uC744 \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
                AppendLog($"\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5C6\uC74C: {path}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                SetModelStatus("\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30");
                AppendLog($"\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30: {path}");
            }
            catch (Exception ex)
            {
                SetModelStatus("\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30 \uC2E4\uD328");
                AppendLog($"\uD29C\uD1A0\uB9AC\uC5BC HTML \uC5F4\uAE30 \uC2E4\uD328: {ex.Message}");
            }
        }

        private static string ResolveTutorialHtmlGuidePath()
        {
            string[] searchRoots =
            {
                Environment.CurrentDirectory,
                AppContext.BaseDirectory
            };

            foreach (string root in searchRoots)
            {
                string path = FindRelativeFileFromAncestor(root, TutorialHtmlGuideRelativePath);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, TutorialHtmlGuideRelativePath));
        }

        private static string FindRelativeFileFromAncestor(string startPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(startPath) || string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(Path.GetFullPath(startPath));
            }
            catch
            {
                return string.Empty;
            }

            if (!directory.Exists && directory.Parent != null)
            {
                directory = directory.Parent;
            }

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        private void ExecuteYoloTrainingWorkflowStep(int order, object sender)
        {
            switch (order)
            {
                case 1:
                    ExecuteStartDatasetSetupCommand(LearningWorkflowViewModel?.SelectedDatasetPurposeMode);
                    break;

                case 2:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 2\uB2E8\uACC4: \uD559\uC2B5 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC120\uD0DD\uD569\uB2C8\uB2E4.");
                    ExecuteBrowseImageFolderCommand();
                    break;

                case 3:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusClassCatalogTab();
                    ClassNameBox?.Focus();
                    SetModelStatus("학습 준비: 클래스를 등록하세요");
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 3\uB2E8\uACC4: \uD074\uB798\uC2A4 \uD0ED\uC5D0\uC11C \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uB4F1\uB85D\uD558\uC138\uC694.");
                    break;

                case 4:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    WpfAnnotationTool primaryLabelingTool = ResolvePrimaryLabelingToolForCurrentPurpose();
                    SelectAnnotationTool(primaryLabelingTool, revealInGuide: true);
                    if (primaryLabelingTool == WpfAnnotationTool.Rectangle)
                    {
                        MainCanvasViewModel.IsTeachingMode = true;
                    }
                    MainCanvasView?.Focus();
                    SetModelStatus("라벨링: 박스 도구");
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 4\uB2E8\uACC4: \uBC15\uC2A4 \uB3C4\uAD6C\uB85C \uAC1D\uCCB4 \uC601\uC5ED\uC744 \uB9CC\uB4E4\uACE0 \uD074\uB798\uC2A4\uB97C \uD655\uC778\uD558\uC138\uC694.");
                    break;

                case 5:
                    TrySaveActiveAnnotationsForTrainingCheck();
                    RefreshTrainingReadinessPanel(refreshYaml: true);
                    FocusAnnotationToolsTab();
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 5\uB2E8\uACC4: \uC800\uC7A5\uB41C \uB77C\uBCA8\uACFC \uD559\uC2B5 \uC124\uC815\uC744 \uC810\uAC80\uD588\uC2B5\uB2C8\uB2E4.");
                    break;

                case 6:
                    FocusYoloTrainingSettingsTab();
                    RefreshTrainingReadinessPanel(refreshYaml: true);
                    StartTrainingButton?.Focus();
                    SetModelStatus("학습: 설정 확인 후 시작");
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 6\uB2E8\uACC4: \uD559\uC2B5 \uC124\uC815\uC744 \uD655\uC778\uD558\uACE0 \uC2DC\uC791 \uBC84\uD2BC\uC744 \uB204\uB974\uC138\uC694.");
                    break;

                case 7:
                    TryApplyLatestTrainingWeightsFromProject(logIfUnchanged: true);
                    SetWorkflowMode(WorkflowMode.Inference);
                    ShowCandidateReviewWorkflowView();
                    DetectButton?.Focus();
                    SetYoloCommandStatus("\uD559\uC2B5 \uACB0\uACFC \uCD94\uB860 \uC900\uBE44: \uD604\uC7AC \uAC80\uC0AC \uBC84\uD2BC\uC73C\uB85C \uBAA8\uB378 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 7\uB2E8\uACC4: \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uB85C \uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uACE0 \uD6C4\uBCF4\uB97C \uAC80\uD1A0\uD558\uC138\uC694.");
                    break;

                default:
                    AppendLog("\uBAA8\uB378 \uD559\uC2B5 \uB2E8\uACC4\uAC00 \uC120\uD0DD\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.");
                    break;
            }
        }

        private WpfAnnotationTool ResolvePrimaryLabelingToolForCurrentPurpose()
        {
            // The guide's "label" shortcut should open the primary tool for the
            // selected dataset purpose, not always the object-detection box tool.
            return GetCurrentDatasetPurpose() switch
            {
                LabelingDatasetPurpose.Segmentation => WpfAnnotationTool.Brush,
                _ => WpfAnnotationTool.Rectangle
            };
        }

        private void TrySaveActiveAnnotationsForTrainingCheck()
        {
            bool hasObjects = manualRois.Count > 0 || GetVisibleManualSegmentCount() > 0 || confirmedDetectionCandidates.Count > 0;
            if (activeImageBitmap == null || !hasObjects)
            {
                AppendLog("저장할 현재 라벨이 없어 데이터셋 평가만 실행합니다.");
                return;
            }

            if (SaveCurrentAnnotations(out int savedCount))
            {
                MarkActiveImageConfirmed();
                AppendLog($"\uBAA8\uB378 \uD559\uC2B5 \uB77C\uBCA8 \uC800\uC7A5 \uC810\uAC80 \uC644\uB8CC. \uAC1D\uCCB4:{savedCount}  {BuildLabelPathSummary()}");
            }
        }

    }
}
