using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Diagnostics;
using System.IO;

namespace MvcVisionSystem
{
    // Owns the training-guide command policy that used to live in the Shell
    // partial. The Shell supplies explicit UI and state callbacks; existing
    // workflow services and ViewModel command contracts remain unchanged.
    internal sealed class TrainingGuideCommandAdapter
    {
        private readonly TrainingGuideCommandAdapterContext context;

        internal TrainingGuideCommandAdapter(TrainingGuideCommandAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #region Commands
        internal void ExecuteFixYoloClassesCommand()
            => ExecuteYoloTrainingWorkflowStep(3);

        internal void ExecuteFixYoloLabelsCommand()
            => ExecuteYoloTrainingWorkflowStep(4);

        internal void ExecuteFixYoloDatasetCommand()
            => ExecuteYoloTrainingWorkflowStep(5);

        internal void ExecuteDatasetDashboardMetricCommand(WpfDatasetDashboardMetricItem metric)
        {
            if (IsCloseApproved() || metric == null)
            {
                return;
            }

            switch (metric.ActionKind)
            {
                case WpfDatasetDashboardActionKind.OpenImages:
                    ExecuteYoloTrainingWorkflowStep(2);
                    break;

                case WpfDatasetDashboardActionKind.OpenClassCatalog:
                    ExecuteYoloTrainingWorkflowStep(3);
                    break;

                case WpfDatasetDashboardActionKind.OpenLabelingProgress:
                case WpfDatasetDashboardActionKind.OpenLabelingTool:
                    context.SetInferenceMode?.Invoke(false);
                    WpfAnnotationTool primaryTool = ResolvePrimaryLabelingToolForCurrentPurpose();
                    context.SelectAnnotationTool?.Invoke(primaryTool, true);
                    context.FocusMainCanvas?.Invoke();
                    context.SetModelStatus?.Invoke("라벨링: 필요한 라벨 도구로 이동했습니다.");
                    context.AppendLog?.Invoke("Guide 데이터셋 카드: 라벨링 도구로 이동");
                    break;

                case WpfDatasetDashboardActionKind.CheckDataset:
                    ExecuteYoloTrainingWorkflowStep(5);
                    break;

                case WpfDatasetDashboardActionKind.ExportQualityAudit:
                    ExecuteExportDatasetQualityAuditCommand();
                    break;

                case WpfDatasetDashboardActionKind.ExportHistoricalSegmentationRemediationAudit:
                    context.ExecuteHistoricalSegmentationRemediationAudit?.Invoke();
                    break;

                case WpfDatasetDashboardActionKind.OpenDatasetSettings:
                    context.FocusYoloTrainingSettingsTab?.Invoke();
                    context.RefreshTrainingReadinessPanel?.Invoke(true);
                    context.SetModelStatus?.Invoke("데이터셋 분할/검증 설정을 확인하세요.");
                    context.AppendLog?.Invoke("Guide 데이터셋 카드: 분할/검증 설정으로 이동");
                    break;

                default:
                    ExecuteYoloTrainingWorkflowStep(5);
                    break;
            }
        }

        internal void ExecuteExportDatasetQualityAuditCommand()
        {
            if (IsCloseApproved())
            {
                return;
            }

            LabelingProjectData data = context.DataProvider?.Invoke();
            string outputPath = YoloDatasetQualityAuditExportService.ResolveDefaultOutputPath(
                data);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                context.SetModelStatus?.Invoke("품질 보고서 저장 실패: 데이터셋 저장 폴더를 먼저 지정하세요.");
                return;
            }

            try
            {
                YoloDatasetQualityAuditReport report = YoloDatasetQualityAuditService.Build(
                    data);
                YoloDatasetQualityAuditExportResult result = YoloDatasetQualityAuditExportService.ExportMarkdown(
                    report,
                    outputPath);
                context.SetModelStatus?.Invoke(
                    $"품질 보고서 저장: {Path.GetFileName(result.OutputPath)} / 누락 {result.MissingLabelCount}장 / 형식 오류 {result.InvalidLabelLineCount}줄");
                context.AppendLog?.Invoke(
                    $"Dataset quality audit saved: {result.OutputPath} / missing {result.MissingLabelCount} / invalid {result.InvalidLabelLineCount}");
            }
            catch (Exception ex)
            {
                context.SetModelStatus?.Invoke($"품질 보고서 저장 실패: {ex.Message}");
                context.AppendLog?.Invoke($"Dataset quality audit save failed: {ex.Message}");
            }
        }

        internal void ExecuteFirstRunSamplePathCommand(WpfFirstRunChecklistItem item)
        {
            if (IsCloseApproved())
            {
                return;
            }

            if (item == null || item.ShortcutWorkflowStepOrder <= 0)
            {
                context.AppendLog?.Invoke("Guide first-run shortcut was ignored because no workflow target was defined.");
                return;
            }

            ExecuteYoloTrainingWorkflowStep(item.ShortcutWorkflowStepOrder);
        }

        internal void ExecuteOpenTutorialHtmlGuideCommand()
        {
            if (IsCloseApproved())
            {
                return;
            }

            string path = TutorialGuidePathService.ResolveTutorialHtmlGuidePath();
            if (!File.Exists(path))
            {
                context.SetModelStatus?.Invoke("튜토리얼 HTML을 찾지 못했습니다.");
                context.AppendLog?.Invoke($"튜토리얼 HTML 없음: {path}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                context.SetModelStatus?.Invoke("튜토리얼 HTML 열기");
                context.AppendLog?.Invoke($"튜토리얼 HTML 열기: {path}");
            }
            catch (Exception ex)
            {
                context.SetModelStatus?.Invoke("튜토리얼 HTML 열기 실패");
                context.AppendLog?.Invoke($"튜토리얼 HTML 열기 실패: {ex.Message}");
            }
        }

        internal void ExecuteYoloTrainingWorkflowStep(int order)
        {
            if (IsCloseApproved())
            {
                return;
            }

            switch (order)
            {
                case 1:
                    context.ExecuteStartDatasetSetupCommand?.Invoke(
                        context.SelectedDatasetPurposeProvider?.Invoke());
                    break;

                case 2:
                    context.SetInferenceMode?.Invoke(false);
                    context.AppendLog?.Invoke("모델 학습 2단계: 학습 이미지 폴더를 선택합니다.");
                    context.ExecuteBrowseImageFolderCommand?.Invoke();
                    break;

                case 3:
                    context.SetInferenceMode?.Invoke(false);
                    context.FocusClassCatalogTab?.Invoke();
                    context.FocusClassNameBox?.Invoke();
                    context.SetModelStatus?.Invoke("학습 준비: 클래스를 등록하세요");
                    context.AppendLog?.Invoke("모델 학습 3단계: 클래스 탭에서 모델이 배울 이름을 등록하세요.");
                    break;

                case 4:
                    context.SetInferenceMode?.Invoke(false);
                    WpfAnnotationTool primaryLabelingTool = ResolvePrimaryLabelingToolForCurrentPurpose();
                    context.SelectAnnotationTool?.Invoke(primaryLabelingTool, true);
                    context.SetCanvasTeachingMode?.Invoke(primaryLabelingTool == WpfAnnotationTool.Rectangle);
                    context.FocusMainCanvas?.Invoke();
                    context.SetModelStatus?.Invoke("라벨링: 박스 도구");
                    context.AppendLog?.Invoke("모델 학습 4단계: 박스 도구로 객체 영역을 만들고 클래스를 확인하세요.");
                    break;

                case 5:
                    TrySaveActiveAnnotationsForTrainingCheck();
                    context.RefreshTrainingReadinessPanel?.Invoke(true);
                    context.FocusAnnotationToolsTab?.Invoke();
                    context.AppendLog?.Invoke("모델 학습 5단계: 저장된 라벨과 학습 설정을 점검했습니다.");
                    break;

                case 6:
                    context.FocusYoloTrainingSettingsTab?.Invoke();
                    context.RefreshTrainingReadinessPanel?.Invoke(true);
                    context.FocusStartTrainingButton?.Invoke();
                    context.SetModelStatus?.Invoke("학습: 설정 확인 후 시작");
                    context.AppendLog?.Invoke("모델 학습 6단계: 학습 설정을 확인하고 시작 버튼을 누르세요.");
                    break;

                case 7:
                    context.TryApplyLatestTrainingWeightsFromProject?.Invoke(true);
                    context.SetInferenceMode?.Invoke(true);
                    context.ShowCandidateReviewWorkflowView?.Invoke();
                    context.FocusDetectButton?.Invoke();
                    context.SetYoloCommandStatus?.Invoke(
                        "학습 결과 추론 준비: 현재 검사 버튼으로 모델 결과를 확인하세요.",
                        false);
                    context.AppendLog?.Invoke("모델 학습 7단계: 학습 결과 모델로 현재 이미지를 검사하고 후보를 검토하세요.");
                    break;

                default:
                    context.AppendLog?.Invoke("모델 학습 단계가 선택되지 않았습니다.");
                    break;
            }
        }

        internal WpfAnnotationTool ResolvePrimaryLabelingToolForCurrentPurpose()
        {
            return context.CurrentDatasetPurposeProvider?.Invoke() == LabelingDatasetPurpose.Segmentation
                ? WpfAnnotationTool.Brush
                : WpfAnnotationTool.Rectangle;
        }

        internal void TrySaveActiveAnnotationsForTrainingCheck()
        {
            bool hasObjects = (context.ManualRoiCountProvider?.Invoke() ?? 0) > 0
                || (context.VisibleManualSegmentCountProvider?.Invoke() ?? 0) > 0
                || (context.ConfirmedCandidateCountProvider?.Invoke() ?? 0) > 0;
            if (!(context.HasActiveImage?.Invoke() ?? false) || !hasObjects)
            {
                context.AppendLog?.Invoke("저장할 현재 라벨이 없어 데이터셋 평가만 실행합니다.");
                return;
            }

            (bool succeeded, int savedCount) result = context.SaveCurrentAnnotations?.Invoke()
                ?? (false, 0);
            if (result.succeeded)
            {
                context.MarkActiveImageConfirmed?.Invoke();
                context.AppendLog?.Invoke(
                    $"모델 학습 라벨 저장 점검 완료. 객체:{result.savedCount}  {context.BuildLabelPathSummary?.Invoke()}");
            }
        }

        internal void ApplyLearningStepWorkflowAction(WpfLearningStepWorkflowAction action)
        {
            switch (action)
            {
                case WpfLearningStepWorkflowAction.LoadSample:
                    context.ExecuteLoadSampleCommand?.Invoke();
                    break;

                case WpfLearningStepWorkflowAction.StartBoxLabeling:
                    context.SetInferenceMode?.Invoke(false);
                    context.SelectAnnotationTool?.Invoke(WpfAnnotationTool.Rectangle, true);
                    context.ExecuteAddSampleRoiCommand?.Invoke();
                    break;

                case WpfLearningStepWorkflowAction.Inference:
                    context.SetInferenceMode?.Invoke(true);
                    break;

                case WpfLearningStepWorkflowAction.ShowCandidateReview:
                    context.ShowCandidateReviewWorkflowView?.Invoke();
                    break;

                case WpfLearningStepWorkflowAction.SaveAnnotations:
                    context.ExecuteSaveAnnotationsCommand?.Invoke();
                    break;
            }

            context.RefreshCanvasWorkflowContext?.Invoke();
        }

        private bool IsCloseApproved()
            => context.IsApplicationCloseApproved?.Invoke() == true;
        #endregion

    }

    internal sealed class TrainingGuideCommandAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<object> SelectedDatasetPurposeProvider { get; init; }
        internal Func<LabelingDatasetPurpose> CurrentDatasetPurposeProvider { get; init; }
        internal Action<object> ExecuteStartDatasetSetupCommand { get; init; }
        internal Action ExecuteBrowseImageFolderCommand { get; init; }
        internal Action ExecuteLoadSampleCommand { get; init; }
        internal Action ExecuteAddSampleRoiCommand { get; init; }
        internal Action ExecuteSaveAnnotationsCommand { get; init; }
        internal Action ExecuteHistoricalSegmentationRemediationAudit { get; init; }
        internal Action<bool> SetInferenceMode { get; init; }
        internal Action<WpfAnnotationTool, bool> SelectAnnotationTool { get; init; }
        internal Action<bool> SetCanvasTeachingMode { get; init; }
        internal Action FocusMainCanvas { get; init; }
        internal Action FocusClassCatalogTab { get; init; }
        internal Action FocusClassNameBox { get; init; }
        internal Action FocusAnnotationToolsTab { get; init; }
        internal Action FocusYoloTrainingSettingsTab { get; init; }
        internal Action FocusStartTrainingButton { get; init; }
        internal Action FocusDetectButton { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action<bool> TryApplyLatestTrainingWeightsFromProject { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<bool> HasActiveImage { get; init; }
        internal Func<int> ManualRoiCountProvider { get; init; }
        internal Func<int> VisibleManualSegmentCountProvider { get; init; }
        internal Func<int> ConfirmedCandidateCountProvider { get; init; }
        internal Func<(bool Succeeded, int SavedCount)> SaveCurrentAnnotations { get; init; }
        internal Action MarkActiveImageConfirmed { get; init; }
        internal Func<string> BuildLabelPathSummary { get; init; }
    }
}
