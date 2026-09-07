using OpenVisionLab;
using System;
using System.Globalization;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps the four visible model-lifecycle values as catalog descriptors so
    /// the same dashboard state can be rendered again after a language change.
    /// </summary>
    public sealed class TrainingModelLifecycleLocalizationSnapshot
    {
        private readonly TrainingModelLifecycleTextDescriptor currentText;
        private readonly TrainingModelLifecycleTextDescriptor candidateText;
        private readonly TrainingModelLifecycleTextDescriptor decisionText;
        private readonly TrainingModelLifecycleTextDescriptor nextActionText;

        internal TrainingModelLifecycleLocalizationSnapshot(
            TrainingModelLifecycleTextDescriptor currentText,
            TrainingModelLifecycleTextDescriptor candidateText,
            TrainingModelLifecycleTextDescriptor decisionText,
            TrainingModelLifecycleTextDescriptor nextActionText)
        {
            this.currentText = currentText ?? throw new ArgumentNullException(nameof(currentText));
            this.candidateText = candidateText ?? throw new ArgumentNullException(nameof(candidateText));
            this.decisionText = decisionText ?? throw new ArgumentNullException(nameof(decisionText));
            this.nextActionText = nextActionText ?? throw new ArgumentNullException(nameof(nextActionText));
        }

        public string CurrentText => currentText.Render();

        public string CandidateText => candidateText.Render();

        public string DecisionText => decisionText.Render();

        public string NextActionText => nextActionText.Render();
    }

    internal sealed class TrainingModelLifecycleTextDescriptor
    {
        private readonly string key;
        private readonly object[] arguments;

        internal TrainingModelLifecycleTextDescriptor(string key, params object[] arguments)
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
                TrainingModelLifecycleTextDescriptor descriptor => descriptor.Render(),
                TrainingModelLifecycleLocalizedArgument localizedArgument => localizedArgument.Render(),
                _ => argument ?? string.Empty
            };
        }
    }

    internal sealed class TrainingModelLifecycleLocalizedArgument
    {
        private readonly string korean;
        private readonly string english;

        internal TrainingModelLifecycleLocalizedArgument(string korean, string english)
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

    public static class TrainingModelLifecycleLocalizationService
    {
        private static readonly (string Korean, string English)[] KnownTerms =
        {
            ("현재 검사 모델과 같음", "same as current inspection model"),
            ("학습 결과 없음", "no training result"),
            ("후보 선택됨 - 설정 저장 필요", "candidate selected - save settings"),
            ("현재 검사 모델로 사용 중", "in use as current inspection model"),
            ("새 후보 검토 필요", "review new candidate"),
            ("현재 모델 유지 권장", "keep current model recommended"),
            ("검사 모델 후보", "inspection model candidate"),
            ("현재 검사 모델", "current inspection model"),
            ("새 학습 모델 후보", "new training model candidate"),
            ("모델 적용", "model adoption"),
            ("데이터셋 점검 후 학습을 시작하세요.", "Run Dataset Health, then start training."),
            ("검사 모델로 저장을 눌러 recipe에 저장하세요. 다음 추론부터 이 모델을 사용합니다.", "click Save as inspection model to store it in the recipe. This model will be used from the next inference."),
            ("필요하면 새 학습을 시작하거나 현재 모델로 검사하세요.", "start a new training run if needed, or inspect with the current model."),
            ("현재 검사 버튼으로 추론 검토를 진행하세요.", "use Inspect current model to review inference results."),
            ("후보 모델의 최종 검증 결과를 비교한 뒤 저장하세요.", "compare the candidate's final-validation results, then save it."),
            ("실행기", "runtime"),
            ("파일 없음", "file missing"),
            ("설정 저장 필요", "settings must be saved"),
            ("상태 확인 필요", "readiness needs review"),
            ("현재 검사 가능 / 학습 미지원", "inspection ready / training unavailable"),
            ("학습 가능 / 검사 모델 필요", "training ready / inspection model required"),
            ("검사/학습 가능", "inspection/training ready"),
            ("설정 확인 필요", "settings need review"),
            ("설치/연결 필요", "installation/connection required"),
            ("recipe 저장됨", "saved to recipe"),
            ("recipe 저장 대기", "waiting to save to recipe"),
            ("검증 후 저장 판단", "decide after validation"),
            ("없음", "none")
        };

        public static TrainingModelLifecycleLocalizationSnapshot CreateInitial()
        {
            return new TrainingModelLifecycleLocalizationSnapshot(
                Text("WpfLearningWorkflow.TrainingModelLifecycle.Current.Initial"),
                Text("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Initial"),
                Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Initial"),
                Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial"));
        }

        public static TrainingModelLifecycleLocalizationSnapshot Build(
            string currentModelText,
            string candidateModelText,
            string decisionText,
            string nextActionText)
        {
            return new TrainingModelLifecycleLocalizationSnapshot(
                BuildCurrent(currentModelText),
                BuildCandidate(candidateModelText),
                BuildDecision(decisionText),
                BuildNextAction(nextActionText));
        }

        private static TrainingModelLifecycleTextDescriptor BuildCurrent(string value)
        {
            string body = StripPrefix(
                value,
                "현재 검사 모델:",
                "검사 모델 후보:",
                "Current inspection model:",
                "Inspection model candidate:");
            if (string.IsNullOrWhiteSpace(body))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Current.Initial");
            }

            return Text(
                "WpfLearningWorkflow.TrainingModelLifecycle.Current.Value",
                Localize(body));
        }

        private static TrainingModelLifecycleTextDescriptor BuildCandidate(string value)
        {
            string body = StripPrefix(
                value,
                "새 학습 모델 후보:",
                "Training candidate:",
                "New training model candidate:");
            if (Matches(body, "학습 결과 없음", "no training result"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Initial");
            }

            if (Matches(body, "없음", "none"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.None");
            }

            if (Matches(body, "현재 검사 모델과 같음", "same as current inspection model"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Same");
            }

            return Text(
                "WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Value",
                Localize(body));
        }

        private static TrainingModelLifecycleTextDescriptor BuildDecision(string value)
        {
            string body = StripPrefix(value, "모델 적용:", "Model adoption:");
            if (Matches(body, "학습 결과 비교 전", "before comparing training results"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Initial");
            }

            if (Matches(body, "후보 선택됨 - 설정 저장 필요", "candidate selected - save settings"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending");
            }

            if (Matches(body, "학습 결과 없음", "no training result"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.NoResult");
            }

            if (Matches(body, "현재 검사 모델로 사용 중", "in use as current inspection model"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Current");
            }

            if (Matches(body, "새 후보 검토 필요", "review new candidate"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review");
            }

            if (Matches(body, "현재 모델 유지 권장", "keep current model recommended"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Keep");
            }

            return Text(
                "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Value",
                Localize(body));
        }

        private static TrainingModelLifecycleTextDescriptor BuildNextAction(string value)
        {
            string body = StripPrefix(value, "다음:", "Next:");
            if (Matches(body, "데이터셋 점검 후 학습을 시작하세요.", "Run Dataset Health, then start training."))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial");
            }

            if (Matches(
                body,
                "검사 모델로 저장을 눌러 recipe에 저장하세요. 다음 추론부터 이 모델을 사용합니다.",
                "click Save as inspection model to store it in the recipe. This model will be used from the next inference."))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending");
            }

            if (Matches(body, "필요하면 새 학습을 시작하거나 현재 모델로 검사하세요.", "start a new training run if needed, or inspect with the current model."))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly");
            }

            if (Matches(body, "현재 검사 버튼으로 추론 검토를 진행하세요.", "use Inspect current model to review inference results."))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Same");
            }

            if (Matches(body, "후보 모델의 최종 검증 결과를 비교한 뒤 저장하세요.", "compare the candidate's final-validation results, then save it."))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Review");
            }

            return Text(
                "WpfLearningWorkflow.TrainingModelLifecycle.Next.Value",
                Localize(body));
        }

        private static TrainingModelLifecycleLocalizedArgument Localize(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (Matches(normalized, "모델 실행기 미설치", "Model runtime not installed"))
            {
                return new TrainingModelLifecycleLocalizedArgument(
                    "모델 실행기 미설치",
                    "Model runtime not installed");
            }

            if (Matches(normalized, "모델 실행기 연결 필요", "Model runtime connection required"))
            {
                return new TrainingModelLifecycleLocalizedArgument(
                    "모델 실행기 연결 필요",
                    "Model runtime connection required");
            }

            if (Matches(
                normalized,
                "모델 실행기 설치 또는 경로 연결 필요",
                "Install the model runtime or connect an existing path"))
            {
                return new TrainingModelLifecycleLocalizedArgument(
                    "모델 실행기 설치 또는 경로 연결 필요",
                    "Install the model runtime or connect an existing path");
            }

            return new TrainingModelLifecycleLocalizedArgument(
                ReplaceKnownTerms(normalized, toEnglish: false),
                ReplaceKnownTerms(normalized, toEnglish: true));
        }

        private static string ReplaceKnownTerms(string value, bool toEnglish)
        {
            string result = value ?? string.Empty;
            foreach ((string korean, string english) in KnownTerms)
            {
                result = result.Replace(
                    toEnglish ? korean : english,
                    toEnglish ? english : korean,
                    StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        private static bool Matches(string value, string korean, string english)
        {
            string normalized = (value ?? string.Empty).Trim();
            return string.Equals(normalized, korean, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, english, StringComparison.OrdinalIgnoreCase);
        }

        private static string StripPrefix(string value, params string[] prefixes)
        {
            string normalized = (value ?? string.Empty).Trim();
            foreach (string prefix in prefixes ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(prefix)
                    && normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(prefix.Length).Trim();
                }
            }

            return normalized;
        }

        private static TrainingModelLifecycleTextDescriptor Text(string key, params object[] arguments)
        {
            return new TrainingModelLifecycleTextDescriptor(key, arguments);
        }
    }

    [Obsolete("Use TrainingModelLifecycleLocalizationSnapshot.", false)]
    public sealed class WpfTrainingModelLifecycleLocalizationSnapshot
    {
        private readonly TrainingModelLifecycleLocalizationSnapshot inner;

        private WpfTrainingModelLifecycleLocalizationSnapshot(TrainingModelLifecycleLocalizationSnapshot source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static WpfTrainingModelLifecycleLocalizationSnapshot FromCanonical(TrainingModelLifecycleLocalizationSnapshot source)
            => source == null ? null : new WpfTrainingModelLifecycleLocalizationSnapshot(source);

        public string CurrentText => inner.CurrentText;

        public string CandidateText => inner.CandidateText;

        public string DecisionText => inner.DecisionText;

        public string NextActionText => inner.NextActionText;
    }

    [Obsolete("Use TrainingModelLifecycleLocalizationService.", false)]
    public static class WpfTrainingModelLifecycleLocalizationService
    {
        public static WpfTrainingModelLifecycleLocalizationSnapshot CreateInitial()
            => WpfTrainingModelLifecycleLocalizationSnapshot.FromCanonical(TrainingModelLifecycleLocalizationService.CreateInitial());

        public static WpfTrainingModelLifecycleLocalizationSnapshot Build(
            string currentModelText,
            string candidateModelText,
            string decisionText,
            string nextActionText)
            => WpfTrainingModelLifecycleLocalizationSnapshot.FromCanonical(TrainingModelLifecycleLocalizationService.Build(currentModelText, candidateModelText, decisionText, nextActionText));
    }
}
