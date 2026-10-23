using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps the visible training-history/comparison values as descriptors so a
    /// language change can re-render the same held-out evidence and command state.
    /// </summary>
    public sealed class TrainingComparisonLocalizationSnapshot
    {
        private readonly TrainingComparisonTextDescriptor historyText;
        private readonly TrainingComparisonTextDescriptor summaryText;
        private readonly TrainingComparisonTextDescriptor comparisonText;
        private readonly TrainingComparisonTextDescriptor adoptionDecisionText;
        private readonly TrainingComparisonTextDescriptor runActionText;
        private readonly TrainingComparisonTextDescriptor runToolTipText;
        private readonly TrainingComparisonTextDescriptor comparisonBasisText;

        internal TrainingComparisonLocalizationSnapshot(
            TrainingComparisonTextDescriptor historyText,
            TrainingComparisonTextDescriptor summaryText,
            TrainingComparisonTextDescriptor comparisonText,
            TrainingComparisonTextDescriptor adoptionDecisionText,
            TrainingComparisonTextDescriptor runActionText,
            TrainingComparisonTextDescriptor runToolTipText,
            TrainingComparisonTextDescriptor comparisonBasisText)
        {
            this.historyText = historyText ?? throw new ArgumentNullException(nameof(historyText));
            this.summaryText = summaryText ?? throw new ArgumentNullException(nameof(summaryText));
            this.comparisonText = comparisonText ?? throw new ArgumentNullException(nameof(comparisonText));
            this.adoptionDecisionText = adoptionDecisionText ?? throw new ArgumentNullException(nameof(adoptionDecisionText));
            this.runActionText = runActionText ?? throw new ArgumentNullException(nameof(runActionText));
            this.runToolTipText = runToolTipText ?? throw new ArgumentNullException(nameof(runToolTipText));
            this.comparisonBasisText = comparisonBasisText ?? throw new ArgumentNullException(nameof(comparisonBasisText));
        }

        public string HistoryText => historyText.Render();

        public string SummaryText => summaryText.Render();

        public string ComparisonText => comparisonText.Render();

        public string AdoptionDecisionText => adoptionDecisionText.Render();

        public string RunActionText => runActionText.Render();

        public string RunToolTipText => runToolTipText.Render();

        public string ComparisonBasisText => comparisonBasisText.Render();
    }

    internal sealed class TrainingComparisonTextDescriptor
    {
        private readonly string key;
        private readonly object[] arguments;

        internal TrainingComparisonTextDescriptor(string key, params object[] arguments)
        {
            this.key = key ?? string.Empty;
            this.arguments = arguments ?? Array.Empty<object>();
        }

        internal string Render()
        {
            object[] renderedArguments = arguments
                .Select(RenderArgument)
                .ToArray();
            return string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T(key),
                renderedArguments);
        }

        private static object RenderArgument(object argument)
        {
            return argument switch
            {
                TrainingComparisonTextDescriptor descriptor => descriptor.Render(),
                TrainingComparisonLocalizedArgument localizedArgument => localizedArgument.Render(),
                _ => argument ?? string.Empty
            };
        }
    }

    internal sealed class TrainingComparisonLocalizedArgument
    {
        private readonly string korean;
        private readonly string english;

        internal TrainingComparisonLocalizedArgument(string korean, string english)
        {
            this.korean = korean ?? string.Empty;
            this.english = english ?? string.Empty;
        }

        internal string Render()
        {
            return OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
                ? english
                : korean;
        }
    }

    public static class TrainingComparisonLocalizationService
    {
        private static readonly Regex KoreanCountSuffix = new(
            @"(?<count>\d+)\s*(?:장|개|건)(?:으로|로)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex KoreanCountThresholdSuffix = new(
            @"(?<count>\d+)\s*(?:장|개|건)\s*이상",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnglishCountSuffix = new(
            @"(?<count>\d+)\s*(?:items?|images?|labels?|cases?|samples?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Keep complete phrases before shorter terms in the original source order.
        // Matching retains its existing substring behavior in opaque text.
        private static readonly (string Korean, string English, string Key)[] KnownTerms =
        {
            ("최근 학습 이력: 아직 없습니다.", "No recent training history.", null),
            ("학습 결과 비교: 아직 비교할 학습 결과가 없습니다.", "Training result comparison: no training result has been compared yet.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestLabels")[0], "final-validation labels", "WpfLearningWorkflow.TrainingComparison.Term.TestLabels"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestImages")[0], "final-validation images", "WpfLearningWorkflow.TrainingComparison.Term.TestImages"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonWorkflowService.TestBasis")[0], "final validation (test)", "WpfComparison.Text.ModelComparisonWorkflowService.TestBasis"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestSplit")[0], "final validation", "WpfLearningWorkflow.TrainingComparison.Term.TestSplit"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonWorkflowService.ValidationBasis")[0], "training validation (val, not an adoption decision)", "WpfComparison.Text.ModelComparisonWorkflowService.ValidationBasis"),
            ("학습 검증", "training validation", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.NotInspectionDecision")[0], "not an adoption decision", "WpfLearningWorkflow.TrainingComparison.Term.NotInspectionDecision"),
            ("모델 비교 실행 불가", "Cannot run model comparison", null),
            ("모델 비교 실행 중", "Running model comparison", null),
            ("모델 비교 완료", "Model comparison complete", null),
            ("모델 비교 실패", "Model comparison failed", null),
            ("YOLO 엔진 비교는 객체탐지 데이터셋에서만 실행할 수 있습니다.", "YOLO engine comparison can run only for an object-detection dataset.", null),
            ("엔진 비교", "engine comparison", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.NoTrainedModel")[0], "No trained model candidate", "WpfLearningWorkflow.TrainingComparison.Term.NoTrainedModel"),
            ("현재 데이터셋 학습 완료", "Current dataset training complete", null),
            ("현재 검사 모델 유지", "Keep current inspection model", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.NewTrainedModel")[0], "New training model candidate", "WpfLearningWorkflow.TrainingComparison.Term.NewTrainedModel"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TrainedModelUnavailable")[0], "Training model candidate unavailable", "WpfLearningWorkflow.TrainingComparison.Term.TrainedModelUnavailable"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Adoption.Initial")[0], "Adoption decision: before comparing training results", "WpfLearningWorkflow.TrainingComparison.Adoption.Initial"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.NoResult")[0], "Adoption decision: no training result", "WpfComparison.Text.TrainingComparisonPresentationService.NoResult"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.Current")[0], "Adoption decision: already in use as the current inspection model", "WpfComparison.Text.TrainingComparisonPresentationService.Current"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.NoMetrics")[0], "Adoption decision: on hold - cannot decide without training metrics", "WpfComparison.Text.TrainingComparisonPresentationService.NoMetrics"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.CurrentMetrics")[0], "Adoption decision: comparison needed - current-model metrics are insufficient", "WpfComparison.Text.TrainingComparisonPresentationService.CurrentMetrics"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.NewStronger")[0], "Adoption decision: new candidate is stronger - review final-validation examples, then save", "WpfComparison.Text.TrainingComparisonPresentationService.NewStronger"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.NewModelStronger")[0], "new candidate is stronger", "WpfLearningWorkflow.TrainingComparison.Term.NewModelStronger"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestExamples")[0], "final-validation examples", "WpfLearningWorkflow.TrainingComparison.Term.TestExamples"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.CheckThenSave")[0], "review, then save", "WpfLearningWorkflow.TrainingComparison.Term.CheckThenSave"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.FilesNeeded")[0], "Adoption decision: new-model metrics are stronger - review file status", "WpfComparison.Text.TrainingComparisonPresentationService.FilesNeeded"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.KeepCurrent")[0], "Adoption decision: keep current model", "WpfComparison.Text.TrainingComparisonPresentationService.KeepCurrent"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.Tie")[0], "Adoption decision: on hold - review examples because the difference is small", "WpfComparison.Text.TrainingComparisonPresentationService.Tie"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.TrainingComparisonPresentationService.TestNeeded")[0], "Adoption decision: on hold - final-validation comparison is needed", "WpfComparison.Text.TrainingComparisonPresentationService.TestNeeded"),
            ("엔진 비교: 준비 필요", "Engine comparison: preparation required", null),
            ("엔진 비교: 실행 중", "Engine comparison: running", null),
            ("엔진 비교: 실패", "Engine comparison: failed", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonWorkflowService.EngineResult")[0], "Engine comparison: review the result", "WpfComparison.Text.ModelComparisonWorkflowService.EngineResult"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonWorkflowService.EngineExamples")[0], "Engine comparison: review examples", "WpfComparison.Text.ModelComparisonWorkflowService.EngineExamples"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.ModelDifferenceLocationHelp")[0], "click the model-difference examples in Candidate Review to inspect image locations.", "WpfLearningWorkflow.TrainingComparison.Term.ModelDifferenceLocationHelp"),
            ("비교 기준:", "Comparison basis:", null),
            ("비교 기준", "Comparison basis", null),
            ("비교 결과", "comparison result", null),
            ("지표 비교", "Metric comparison", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.NewModelMetrics")[0], "New candidate metrics", "WpfLearningWorkflow.TrainingComparison.Term.NewModelMetrics"),
            ("지표 없음", "No metrics", null),
            ("학습 실패 아님", "not a training failure", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.DecisionAfterModelCheck")[0], "decide after candidate validation", "WpfLearningWorkflow.TrainingComparison.Term.DecisionAfterModelCheck"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestComparisonUnavailable")[0], "final-validation comparison unavailable", "WpfLearningWorkflow.TrainingComparison.Term.TestComparisonUnavailable"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestComparison")[0], "final-validation comparison", "WpfLearningWorkflow.TrainingComparison.Term.TestComparison"),
            ("선택 모델 비교", "Compare selected model", null),
            ("추론 화면에서 비교할 학습 모델을 선택하세요.", "Select a trained model to compare in Inference.", null),
            ("선택한 학습 모델이 현재 검사 모델과 같은 실행 환경을 사용하지 않습니다.", "The selected trained model does not use the same runtime as the inspection model.", null),
            ("비교하려면 같은 모델 실행 환경에서 학습한 모델을 선택하세요.", "Select a model trained in the same model runtime to compare.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.CompareModelsWithLabels")[0], "Compare the current inspection model with the selected trained model using", "WpfLearningWorkflow.TrainingComparison.Term.CompareModelsWithLabels"),
            ("비교해도 검사 모델은 바뀌지 않습니다.", "The comparison does not change the inspection model.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.DecisionAfterComparison")[0], "replacement can be decided after comparison", "WpfLearningWorkflow.TrainingComparison.Term.DecisionAfterComparison"),
            ("비교 가능", "comparison possible", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.WeakDecisionEvidence")[0], "replacement evidence is weak", "WpfLearningWorkflow.TrainingComparison.Term.WeakDecisionEvidence"),
            ("권장", "recommended", null),
            ("기존 모델과 새 학습 모델을 비교합니다.", "compares the current and new training models.", null),
            ("기존 모델과 새 학습 모델을 비교하는 중입니다.", "is comparing the current and new training models.", null),
            ("기존 모델과 새 모델의 차이를 계산 중입니다.", "is calculating the difference between the current and new models.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonCommandStateService.RunningHelp")[0], "Compare the current and new training models on final-validation images.", "WpfComparison.Text.ModelComparisonCommandStateService.RunningHelp"),
            ("현재 다른 명령이 실행 중이므로 완료 후 비교할 수 있습니다.", "another command is running, so comparison can run after it completes.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.RecheckTestAfterCommand")[0], "review final-validation status again after the running command completes.", "WpfLearningWorkflow.TrainingComparison.Term.RecheckTestAfterCommand"),
            ("다른 명령이 실행 중이므로 완료 후 비교할 수 있습니다.", "another command is running, so comparison can run after it completes.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonCommandStateService.CheckHelp")[0], "review training/validation/final-validation status in Dataset Health first.", "WpfComparison.Text.ModelComparisonCommandStateService.CheckHelp"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonCommandStateService.CheckBasis")[0], "Comparison basis: Dataset Health shows the final-validation image and ground-truth label counts after the dataset check.", "WpfComparison.Text.ModelComparisonCommandStateService.CheckBasis"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonCommandStateService.BlockedBasis")[0], "Comparison basis: decide on replacement with final-validation labels after training becomes ready.", "WpfComparison.Text.ModelComparisonCommandStateService.BlockedBasis"),
            ("모델 비교 전에 데이터셋 학습 불가 항목을 먼저 해결하세요.", "resolve the dataset training blockers before model comparison.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.BeforeInspectionDecision")[0], "before deciding on model replacement", "WpfLearningWorkflow.TrainingComparison.Term.BeforeInspectionDecision"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.NeedTestImage")[0], "secure at least one final-validation image.", "WpfLearningWorkflow.TrainingComparison.Term.NeedTestImage"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TestLabelsMissing")[0], "final-validation images exist, but no ground-truth label files exist.", "WpfLearningWorkflow.TrainingComparison.Term.TestLabelsMissing"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.SaveTestLabels")[0], "save final-validation labels before comparison.", "WpfLearningWorkflow.TrainingComparison.Term.SaveTestLabels"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.WeakDecisionHelp")[0], "comparison is possible, but replacement evidence is weak.", "WpfLearningWorkflow.TrainingComparison.Term.WeakDecisionHelp"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.CollectThenSet")[0], "collect them, then apply.", "WpfLearningWorkflow.TrainingComparison.Term.CollectThenSet"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.OnTestImagesWith")[0], "on final-validation images", "WpfLearningWorkflow.TrainingComparison.Term.OnTestImagesWith"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.OnTestImagesAt")[0], "on final-validation images", "WpfLearningWorkflow.TrainingComparison.Term.OnTestImagesAt"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.UsingTestLabels")[0], "using final-validation labels", "WpfLearningWorkflow.TrainingComparison.Term.UsingTestLabels"),
            ("객체탐지 분석", "object-detection analysis", null),
            ("객체탐지 분석 중", "object-detection analysis in progress", null),
            ("객체탐지 분석 완료", "object-detection analysis complete", null),
            ("객체탐지", "object detection", null),
            ("동일한", "same", null),
            ("이미지에서", "on images", null),
            ("정확도와", "accuracy and", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.InferenceTimeObject")[0], "model takt", "WpfLearningWorkflow.TrainingComparison.Term.InferenceTimeObject"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.TimingRunning")[0], "is measuring accuracy and model takt.", "WpfLearningWorkflow.TrainingComparison.Term.TimingRunning"),
            ("측정하는 중입니다.", "is being measured.", null),
            ("이미지별 차이", "image-by-image differences", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.ModelDifferencesHelp")[0], "review accuracy, model takt, and image-by-image differences in Candidate Review.", "WpfLearningWorkflow.TrainingComparison.Term.ModelDifferencesHelp"),
            ("분석 실행 불가", "analysis cannot run", null),
            ("분석 결과를 읽지 못했습니다.", "could not read the analysis result.", null),
            ("분석 완료", "analysis complete", null),
            ("분석 실패", "analysis failed", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.ExamplesNeedCheck")[0], "review examples", "WpfLearningWorkflow.TrainingComparison.Term.ExamplesNeedCheck"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfComparison.Text.ModelComparisonWorkflowService.ResultNeeded")[0], "review the result", "WpfComparison.Text.ModelComparisonWorkflowService.ResultNeeded"),
            ("준비 필요", "preparation required", null),
            ("학습 불가", "training unavailable", null),
            ("최종 라벨 필요", "final labels required", null),
            ("정답 라벨", "ground-truth labels", null),
            ("비교 실행 중", "comparison running", null),
            ("대기", "waiting", null),
            ("실행 중", "running", null),
            ("완료", "complete", null),
            ("실패", "failed", null),
            ("오류", "error", null),
            ("진행 중", "in progress", null),
            ("시작 중", "starting", null),
            ("중지됨", "stopped", null),
            ("명령 수락됨", "accepted", null),
            ("상태 미확인", "status unknown", null),
            ("학습 가능", "training ready", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.CheckNeeded")[0], "review needed", "WpfLearningWorkflow.TrainingComparison.Term.CheckNeeded"),
            ("점검", "check", null),
            ("최근 이력", "Recent history", null),
            ("학습", "training", null),
            ("recipe 저장됨", "saved to recipe", null),
            ("recipe 미저장", "not saved to recipe", null),
            ("results.csv 없음", "results.csv missing", null),
            ("weight", "weight", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.InspectionDecisionPrefix")[0], "Adoption decision:", "WpfLearningWorkflow.TrainingComparison.Term.InspectionDecisionPrefix"),
            ("엔진 비교:", "Engine comparison:", null),
            ("모델 비교", "Compare models", null),
            ("비교", "comparison", null),
            ("교체", "replacement", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.CheckImperative")[0], "review it.", "WpfLearningWorkflow.TrainingComparison.Term.CheckImperative"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.CheckWord")[0], "review", "WpfLearningWorkflow.TrainingComparison.Term.CheckWord"),
            ("필요", "needed", null),
            ("없음", "none", null)
        };

        private static readonly (string Korean, string English, string Key)[] KnownTermsByLength = KnownTerms
            .OrderByDescending(term => Math.Max(term.Korean.Length, term.English.Length))
            .ToArray();

        public static TrainingComparisonLocalizationSnapshot CreateInitial()
        {
            return new TrainingComparisonLocalizationSnapshot(
                Text("WpfLearningWorkflow.TrainingComparison.History.Initial"),
                Text("WpfLearningWorkflow.TrainingComparison.Summary.Initial"),
                Text("WpfLearningWorkflow.TrainingComparison.Comparison.Initial"),
                Text("WpfLearningWorkflow.TrainingComparison.Adoption.Initial"),
                Text("WpfLearningWorkflow.TrainingComparison.Run.Action.Initial"),
                Text("WpfLearningWorkflow.TrainingComparison.Run.Tooltip.Initial"),
                Text("WpfLearningWorkflow.TrainingComparison.Basis.Initial"));
        }

        public static TrainingComparisonLocalizationSnapshot Build(
            string historyText,
            string summaryText,
            string comparisonText,
            string adoptionDecisionText,
            string runActionText,
            string runToolTipText,
            string comparisonBasisText)
        {
            return new TrainingComparisonLocalizationSnapshot(
                BuildValue(historyText, "WpfLearningWorkflow.TrainingComparison.History.Initial", "WpfLearningWorkflow.TrainingComparison.History.Value"),
                BuildValue(summaryText, "WpfLearningWorkflow.TrainingComparison.Summary.Initial", "WpfLearningWorkflow.TrainingComparison.Summary.Value"),
                BuildValue(comparisonText, "WpfLearningWorkflow.TrainingComparison.Comparison.Initial", "WpfLearningWorkflow.TrainingComparison.Comparison.Value"),
                BuildValue(adoptionDecisionText, "WpfLearningWorkflow.TrainingComparison.Adoption.Initial", "WpfLearningWorkflow.TrainingComparison.Adoption.Value"),
                BuildRunValue(runActionText, "WpfLearningWorkflow.TrainingComparison.Run.Action.Initial", "WpfLearningWorkflow.TrainingComparison.Run.Action.Value"),
                BuildValue(runToolTipText, "WpfLearningWorkflow.TrainingComparison.Run.Tooltip.Initial", "WpfLearningWorkflow.TrainingComparison.Run.Tooltip.Value"),
                BuildValue(comparisonBasisText, "WpfLearningWorkflow.TrainingComparison.Basis.Initial", "WpfLearningWorkflow.TrainingComparison.Basis.Value"));
        }

        private static TrainingComparisonTextDescriptor BuildRunValue(
            string value,
            string initialKey,
            string valueKey)
        {
            if (Matches(value, "모델 비교", "Compare models"))
            {
                return Text(initialKey);
            }

            return BuildValue(value, initialKey, valueKey);
        }

        private static TrainingComparisonTextDescriptor BuildValue(
            string value,
            string initialKey,
            string valueKey)
        {
            string normalized = value?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalized)
                || Matches(normalized, "최근 학습 이력: 아직 없습니다.", "No recent training history.")
                || Matches(normalized, "학습 결과 비교: 아직 비교할 학습 결과가 없습니다.", "Training result comparison: no training result has been compared yet.")
                ? Text(initialKey)
                : Text(valueKey, Localize(normalized));
        }

        private static TrainingComparisonLocalizedArgument Localize(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            IReadOnlyList<OpenVisionLocalizationEntry> entries = OpenVisionLanguageService.GetEntries();
            string korean = string.Empty;
            string english = string.Empty;
            bool found = false;
            bool exactAmbiguous = false;
            foreach (OpenVisionLocalizationEntry entry in entries)
            {
                if (!entry.Key.StartsWith("WpfComparison.Text.", StringComparison.Ordinal)
                    || (!string.Equals(normalized, entry.Korean, StringComparison.Ordinal)
                        && !string.Equals(normalized, entry.English, StringComparison.Ordinal))) continue;
                string currentKorean = string.IsNullOrWhiteSpace(entry.Korean) ? entry.English : entry.Korean;
                string currentEnglish = string.IsNullOrWhiteSpace(entry.English) ? entry.Korean : entry.English;
                if (found && (!string.Equals(korean, currentKorean, StringComparison.Ordinal)
                    || !string.Equals(english, currentEnglish, StringComparison.Ordinal)))
                {
                    exactAmbiguous = true;
                    found = false;
                    break;
                }
                korean = currentKorean;
                english = currentEnglish;
                found = true;
            }
            if (found) return new TrainingComparisonLocalizedArgument(korean, english);

            if (!exactAmbiguous)
            {
                foreach ((string oldKorean, string oldEnglish, string key) in KnownTerms)
                {
                    if (key == null) continue;
                    OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                    if (entry == null || (string.IsNullOrWhiteSpace(entry.Korean) && string.IsNullOrWhiteSpace(entry.English))) continue;
                    if (!string.Equals(normalized, oldKorean, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(normalized, oldEnglish, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(normalized, entry.Korean, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(normalized, entry.English, StringComparison.OrdinalIgnoreCase)) continue;
                    string currentKorean = string.IsNullOrWhiteSpace(entry.Korean) ? entry.English : entry.Korean;
                    string currentEnglish = string.IsNullOrWhiteSpace(entry.English) ? entry.Korean : entry.English;
                    if (found && (!string.Equals(korean, currentKorean, StringComparison.Ordinal)
                        || !string.Equals(english, currentEnglish, StringComparison.Ordinal)))
                    {
                        found = false;
                        break;
                    }
                    korean = currentKorean;
                    english = currentEnglish;
                    found = true;
                }
                if (found) return new TrainingComparisonLocalizedArgument(korean, english);
            }


            foreach (string key in new[]
            {
                "WpfComparison.Text.ModelComparisonCommandStateService.LabelsEmpty",
                "WpfComparison.Text.ModelComparisonCommandStateService.WeakHelp",
                "WpfComparison.Text.ModelComparisonCommandStateService.WeakBasis",
                "WpfComparison.Text.ModelComparisonCommandStateService.ReadyHelp",
                "WpfComparison.Text.ModelComparisonCommandStateService.ReadyBasis",
                "WpfComparison.Text.ModelComparisonCommandStateService.SelectedHelp"
            })
            {
                OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                if (entry == null) continue;
                string koreanTemplate = string.IsNullOrWhiteSpace(entry.Korean) ? entry.English : entry.Korean;
                string englishTemplate = string.IsNullOrWhiteSpace(entry.English) ? koreanTemplate : entry.English;
                foreach (string template in new[] { koreanTemplate, englishTemplate }.Concat(OpenVisionLanguageService.GetPreviousKoreanDefaults(key)))
                {
                    int first = template.IndexOf("{0}", StringComparison.Ordinal);
                    int second = template.IndexOf("{1}", StringComparison.Ordinal);
                    if (first < 0 || (second >= 0 && second <= first)) continue;
                    string prefix = template.Substring(0, first);
                    string separator = second < 0 ? string.Empty : template.Substring(first + 3, second - first - 3);
                    string suffix = template.Substring((second < 0 ? first : second) + 3);
                    if (!normalized.StartsWith(prefix, StringComparison.Ordinal) || !normalized.EndsWith(suffix, StringComparison.Ordinal)
                        || normalized.Length < prefix.Length + suffix.Length) continue;
                    string body = normalized.Substring(prefix.Length, normalized.Length - prefix.Length - suffix.Length);
                    int boundary = second < 0 ? -1 : body.IndexOf(separator, StringComparison.Ordinal);
                    if (second >= 0 && boundary < 0) continue;
                    string firstText = second < 0 ? body : body.Substring(0, boundary);
                    string secondText = second < 0 ? string.Empty : body.Substring(boundary + separator.Length);
                    if (!int.TryParse(firstText, NumberStyles.Integer, CultureInfo.CurrentCulture, out _)
                        || (second >= 0 && !int.TryParse(secondText, NumberStyles.Integer, CultureInfo.CurrentCulture, out _))) continue;
                    object[] counts = second < 0 ? new object[] { firstText }
                        : new object[] { firstText, secondText };
                    return new TrainingComparisonLocalizedArgument(
                        string.Format(CultureInfo.CurrentCulture, koreanTemplate, counts),
                        string.Format(CultureInfo.CurrentCulture, englishTemplate, counts));
                }
            }

            int historyWeightMarker = normalized.IndexOf(" · weight ", StringComparison.Ordinal);
            int historyWeightStart = normalized.StartsWith("최근 이력: weight ", StringComparison.Ordinal) ? "최근 이력: weight ".Length
                : normalized.StartsWith("Recent history: weight ", StringComparison.Ordinal) ? "Recent history: weight ".Length
                : historyWeightMarker >= 0 ? historyWeightMarker + " · weight ".Length : -1;
            int historySuffixStart = normalized.LastIndexOf(" / ", StringComparison.Ordinal);
            string historyPrefix = historyWeightStart < 0 ? string.Empty : normalized.Substring(0, historyWeightStart);
            if ((normalized.StartsWith("최근 이력: ", StringComparison.Ordinal) || normalized.StartsWith("Recent history: ", StringComparison.Ordinal))
                && historyWeightStart >= 0 && historySuffixStart > historyWeightStart
                && normalized.IndexOf(" · weight ", historyWeightStart, StringComparison.Ordinal) < 0
                && !MvcVisionSystem._1._Core.PythonModelSettingsValidator.LooksLikePath(historyPrefix)
                && !System.IO.Path.HasExtension(historyPrefix))
            {
                string historySuffix = normalized.Substring(historySuffixStart);
                if (historySuffix == " / recipe 저장됨" || historySuffix == " / recipe 미저장"
                    || historySuffix == " / saved to recipe" || historySuffix == " / not saved to recipe")
                {
                    string historyPath = normalized.Substring(historyWeightStart, historySuffixStart - historyWeightStart);
                    return new TrainingComparisonLocalizedArgument(
                        ReplaceKnownTerms(historyPrefix, toEnglish: false, entries: entries) + historyPath + ReplaceKnownTerms(historySuffix, toEnglish: false, entries: entries),
                        ReplaceKnownTerms(historyPrefix, toEnglish: true, entries: entries) + historyPath + ReplaceKnownTerms(historySuffix, toEnglish: true, entries: entries));
                }
            }

            int pathSeparator = normalized.IndexOf(" / ", StringComparison.Ordinal);
            string pathPart = pathSeparator < 0 ? normalized : normalized.Substring(0, pathSeparator);
            string extension = System.IO.Path.GetExtension(pathPart);
            if (MvcVisionSystem._1._Core.PythonModelSettingsValidator.LooksLikePath(pathPart)
                || extension.Length > 1 && char.IsLetter(extension[1]) && !extension.Any(char.IsWhiteSpace))
            {
                string suffix = pathSeparator < 0 ? string.Empty : normalized.Substring(pathSeparator);
                return new TrainingComparisonLocalizedArgument(
                    pathPart + ReplaceKnownTerms(suffix, toEnglish: false, entries: entries),
                    pathPart + ReplaceKnownTerms(suffix, toEnglish: true, entries: entries));
            }

            return new TrainingComparisonLocalizedArgument(
                ReplaceKnownTerms(normalized, toEnglish: false, entries: entries),
                ReplaceKnownTerms(normalized, toEnglish: true, entries: entries));
        }

        private static string ReplaceKnownTerms(string value, bool toEnglish, IReadOnlyList<OpenVisionLocalizationEntry> entries)
        {
            string result = value ?? string.Empty;
            foreach ((string korean, string english, string key) in KnownTermsByLength)
            {
                string replacement = toEnglish ? english : korean;
                if (key != null)
                {
                    OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                    string current = entry == null ? string.Empty : toEnglish ? entry.English : entry.Korean;
                    if (entry != null && string.IsNullOrWhiteSpace(current)) current = toEnglish ? entry.Korean : entry.English;
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        replacement = current;
                        if (!toEnglish) result = result.Replace(korean, current, StringComparison.OrdinalIgnoreCase);
                    }
                }
                result = result.Replace(
                    toEnglish ? korean : english,
                    replacement,
                    StringComparison.OrdinalIgnoreCase);
            }

            result = toEnglish
                ? KoreanCountSuffix.Replace(
                    KoreanCountThresholdSuffix.Replace(result, "${count} items or more"),
                    "${count} items")
                : EnglishCountSuffix.Replace(result, "${count}장");

            return result;
        }

        private static bool Matches(string value, string korean, string english)
        {
            string normalized = (value ?? string.Empty).Trim();
            return string.Equals(normalized, korean, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, english, StringComparison.OrdinalIgnoreCase);
        }

        private static TrainingComparisonTextDescriptor Text(string key, params object[] arguments)
        {
            return new TrainingComparisonTextDescriptor(key, arguments);
        }
    }

    [Obsolete("Use TrainingComparisonLocalizationSnapshot.", false)]
    public sealed class WpfTrainingComparisonLocalizationSnapshot
    {
        private readonly TrainingComparisonLocalizationSnapshot inner;

        private WpfTrainingComparisonLocalizationSnapshot(TrainingComparisonLocalizationSnapshot source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static WpfTrainingComparisonLocalizationSnapshot FromCanonical(TrainingComparisonLocalizationSnapshot source)
            => source == null ? null : new WpfTrainingComparisonLocalizationSnapshot(source);

        internal TrainingComparisonLocalizationSnapshot ToCanonical() => inner;

        public string HistoryText => inner.HistoryText;

        public string SummaryText => inner.SummaryText;

        public string ComparisonText => inner.ComparisonText;

        public string AdoptionDecisionText => inner.AdoptionDecisionText;

        public string RunActionText => inner.RunActionText;

        public string RunToolTipText => inner.RunToolTipText;

        public string ComparisonBasisText => inner.ComparisonBasisText;
    }

    [Obsolete("Use TrainingComparisonLocalizationService.", false)]
    public static class WpfTrainingComparisonLocalizationService
    {
        public static WpfTrainingComparisonLocalizationSnapshot CreateInitial()
            => WpfTrainingComparisonLocalizationSnapshot.FromCanonical(TrainingComparisonLocalizationService.CreateInitial());

        public static WpfTrainingComparisonLocalizationSnapshot Build(
            string historyText,
            string summaryText,
            string comparisonText,
            string adoptionDecisionText,
            string runActionText,
            string runToolTipText,
            string comparisonBasisText)
            => WpfTrainingComparisonLocalizationSnapshot.FromCanonical(TrainingComparisonLocalizationService.Build(
                historyText,
                summaryText,
                comparisonText,
                adoptionDecisionText,
                runActionText,
                runToolTipText,
                comparisonBasisText));
    }
}
