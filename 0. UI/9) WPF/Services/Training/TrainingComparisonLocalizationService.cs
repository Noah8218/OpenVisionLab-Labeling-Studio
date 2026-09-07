using OpenVisionLab;
using System;
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

        // Keep complete phrases before their shorter terms so dynamic metrics,
        // paths, and counts survive the bounded presentation translation.
        private static readonly (string Korean, string English)[] KnownTerms =
        {
            ("최근 학습 이력: 아직 없습니다.", "No recent training history."),
            ("학습 결과 비교: 아직 비교할 학습 결과가 없습니다.", "Training result comparison: no training result has been compared yet."),
            ("최종 검증 라벨", "final-validation labels"),
            ("최종 검증 이미지", "final-validation images"),
            ("최종 검증(test)", "final validation (test)"),
            ("최종 검증", "final validation"),
            ("학습 검증(val, 교체 판단 아님)", "training validation (val, not an adoption decision)"),
            ("학습 검증", "training validation"),
            ("교체 판단 아님", "not an adoption decision"),
            ("모델 비교 실행 불가", "Cannot run model comparison"),
            ("모델 비교 실행 중", "Running model comparison"),
            ("모델 비교 완료", "Model comparison complete"),
            ("모델 비교 실패", "Model comparison failed"),
            ("YOLO 엔진 비교는 객체탐지 데이터셋에서만 실행할 수 있습니다.", "YOLO engine comparison can run only for an object-detection dataset."),
            ("엔진 비교", "engine comparison"),
            ("학습 결과 모델 후보 없음", "No trained model candidate"),
            ("현재 데이터셋 학습 완료", "Current dataset training complete"),
            ("현재 검사 모델 유지", "Keep current inspection model"),
            ("새 학습 모델 후보", "New training model candidate"),
            ("학습 모델 후보 사용 불가", "Training model candidate unavailable"),
            ("교체 판단: 학습 결과 비교 전", "Adoption decision: before comparing training results"),
            ("교체 판단: 학습 결과 없음", "Adoption decision: no training result"),
            ("교체 판단: 이미 현재 검사 모델로 사용 중", "Adoption decision: already in use as the current inspection model"),
            ("교체 판단: 보류 - 학습 지표가 없어 채택 판단 불가", "Adoption decision: on hold - cannot decide without training metrics"),
            ("교체 판단: 비교 필요 - 현재 모델 지표가 부족합니다", "Adoption decision: comparison needed - current-model metrics are insufficient"),
            ("교체 판단: 새 모델 후보 우세 - 최종 검증 예시 확인 후 저장", "Adoption decision: new candidate is stronger - review final-validation examples, then save"),
            ("새 모델 후보 우세", "new candidate is stronger"),
            ("최종 검증 예시", "final-validation examples"),
            ("확인 후 저장", "review, then save"),
            ("교체 판단: 새 모델 지표 우세 - 파일 상태 확인 필요", "Adoption decision: new-model metrics are stronger - review file status"),
            ("교체 판단: 현재 모델 유지", "Adoption decision: keep current model"),
            ("교체 판단: 보류 - 차이가 작아 예시 확인 필요", "Adoption decision: on hold - review examples because the difference is small"),
            ("교체 판단: 보류 - 최종 검증 비교 필요", "Adoption decision: on hold - final-validation comparison is needed"),
            ("엔진 비교: 준비 필요", "Engine comparison: preparation required"),
            ("엔진 비교: 실행 중", "Engine comparison: running"),
            ("엔진 비교: 실패", "Engine comparison: failed"),
            ("엔진 비교: 결과 확인 필요", "Engine comparison: review the result"),
            ("엔진 비교: 예시 확인 필요", "Engine comparison: review examples"),
            ("Candidate Review의 모델 차이 예시를 클릭해 이미지 위치를 확인하세요.", "click the model-difference examples in Candidate Review to inspect image locations."),
            ("비교 기준:", "Comparison basis:"),
            ("비교 기준", "Comparison basis"),
            ("비교 결과", "comparison result"),
            ("지표 비교", "Metric comparison"),
            ("새 후보 지표", "New candidate metrics"),
            ("지표 없음", "No metrics"),
            ("학습 실패 아님", "not a training failure"),
            ("후보 검증 후 저장 판단", "decide after candidate validation"),
            ("최종 검증 비교 불가", "final-validation comparison unavailable"),
            ("최종 검증 비교", "final-validation comparison"),
            ("비교 후 교체 판단 가능", "replacement can be decided after comparison"),
            ("비교 가능", "comparison possible"),
            ("교체 근거는 약함", "replacement evidence is weak"),
            ("권장", "recommended"),
            ("기존 모델과 새 학습 모델을 비교합니다.", "compares the current and new training models."),
            ("기존 모델과 새 학습 모델을 비교하는 중입니다.", "is comparing the current and new training models."),
            ("기존 모델과 새 모델의 차이를 계산 중입니다.", "is calculating the difference between the current and new models."),
            ("최종 검증 이미지로 기존 모델과 새 학습 모델을 비교하는 중입니다.", "Compare the current and new training models on final-validation images."),
            ("현재 다른 명령이 실행 중이므로 완료 후 비교할 수 있습니다.", "another command is running, so comparison can run after it completes."),
            ("진행 중인 명령이 완료된 뒤 최종 검증 상태를 다시 확인합니다.", "review final-validation status again after the running command completes."),
            ("다른 명령이 실행 중이므로 완료 후 비교할 수 있습니다.", "another command is running, so comparison can run after it completes."),
            ("데이터셋 점검으로 학습/검증/최종 검증 상태를 먼저 확인하세요.", "review training/validation/final-validation status in Dataset Health first."),
            ("비교 기준: 데이터셋 점검 후 최종 검증 이미지와 정답 라벨 수를 표시합니다.", "Comparison basis: Dataset Health shows the final-validation image and ground-truth label counts after the dataset check."),
            ("비교 기준: 학습 가능 상태가 된 후 최종 검증 라벨로 교체 판단을 합니다.", "Comparison basis: decide on replacement with final-validation labels after training becomes ready."),
            ("모델 비교 전에 데이터셋 학습 불가 항목을 먼저 해결하세요.", "resolve the dataset training blockers before model comparison."),
            ("모델 교체 판단 전에", "before deciding on model replacement"),
            ("최종 검증 이미지를 1장 이상 확보하세요.", "secure at least one final-validation image."),
            ("최종 검증 이미지는 있지만 정답 라벨 파일이 없습니다.", "final-validation images exist, but no ground-truth label files exist."),
            ("비교 전 최종 검증 라벨을 저장하세요.", "save final-validation labels before comparison."),
            ("비교는 가능하지만 교체 근거가 약합니다.", "comparison is possible, but replacement evidence is weak."),
            ("확보한 뒤 적용하세요.", "collect them, then apply."),
            ("최종 검증 이미지로", "on final-validation images"),
            ("최종 검증 이미지에서", "on final-validation images"),
            ("최종 검증 라벨로", "using final-validation labels"),
            ("객체탐지 분석", "object-detection analysis"),
            ("객체탐지 분석 중", "object-detection analysis in progress"),
            ("객체탐지 분석 완료", "object-detection analysis complete"),
            ("객체탐지", "object detection"),
            ("동일한", "same"),
            ("이미지에서", "on images"),
            ("정확도와", "accuracy and"),
            ("모델 Takt를", "model takt"),
            ("정확도와 모델 Takt를 측정하는 중입니다.", "is measuring accuracy and model takt."),
            ("측정하는 중입니다.", "is being measured."),
            ("이미지별 차이", "image-by-image differences"),
            ("Candidate Review에서 정확도, 모델 Takt, 이미지별 차이를 확인하세요.", "review accuracy, model takt, and image-by-image differences in Candidate Review."),
            ("분석 실행 불가", "analysis cannot run"),
            ("분석 결과를 읽지 못했습니다.", "could not read the analysis result."),
            ("분석 완료", "analysis complete"),
            ("분석 실패", "analysis failed"),
            ("예시 확인 필요", "review examples"),
            ("결과 확인 필요", "review the result"),
            ("준비 필요", "preparation required"),
            ("학습 불가", "training unavailable"),
            ("최종 라벨 필요", "final labels required"),
            ("정답 라벨", "ground-truth labels"),
            ("비교 실행 중", "comparison running"),
            ("대기", "waiting"),
            ("실행 중", "running"),
            ("완료", "complete"),
            ("실패", "failed"),
            ("오류", "error"),
            ("진행 중", "in progress"),
            ("시작 중", "starting"),
            ("중지됨", "stopped"),
            ("명령 수락됨", "accepted"),
            ("상태 미확인", "status unknown"),
            ("학습 가능", "training ready"),
            ("확인 필요", "review needed"),
            ("점검", "check"),
            ("최근 이력", "Recent history"),
            ("학습", "training"),
            ("recipe 저장됨", "saved to recipe"),
            ("recipe 미저장", "not saved to recipe"),
            ("results.csv 없음", "results.csv missing"),
            ("weight", "weight"),
            ("교체 판단:", "Adoption decision:"),
            ("엔진 비교:", "Engine comparison:"),
            ("모델 비교", "Compare models"),
            ("비교", "comparison"),
            ("교체", "replacement"),
            ("확인하세요.", "review it."),
            ("확인", "review"),
            ("필요", "needed"),
            ("없음", "none")
        };

        private static readonly (string Korean, string English)[] KnownTermsByLength = KnownTerms
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
            return new TrainingComparisonLocalizedArgument(
                ReplaceKnownTerms(normalized, toEnglish: false),
                ReplaceKnownTerms(normalized, toEnglish: true));
        }

        private static string ReplaceKnownTerms(string value, bool toEnglish)
        {
            string result = value ?? string.Empty;
            foreach ((string korean, string english) in KnownTermsByLength)
            {
                result = result.Replace(
                    toEnglish ? korean : english,
                    toEnglish ? english : korean,
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
