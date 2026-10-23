using OpenVisionLab;
using System;
using System.Collections.Generic;
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
        private static readonly (string Korean, string English, string Key)[] KnownTerms =
        {
            ("현재 검사 모델과 같음", "same as current inspection model", null),
            ("학습 결과 없음", "no training result", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending")[0], "candidate selected - save settings", "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending"),
            ("현재 검사 모델로 사용 중", "in use as current inspection model", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review")[0], "review new candidate", "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review"),
            ("현재 모델 유지 권장", "keep current model recommended", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending")[0], "inspection model candidate", "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending"),
            ("현재 검사 모델", "current inspection model", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel")[0], "new training model candidate", "WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment")[0], "model adoption", "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment"),
            ("데이터셋 점검 후 학습을 시작하세요.", "Run Dataset Health, then start training.", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending")[0], "click Save as inspection model to store it in the recipe. This model will be used from the next inference.", "WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly")[0], "start a new training run if needed, or inspect with the current model.", "WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.Same")[0], "use Inspect current model to review inference results.", "WpfLearningWorkflow.TrainingModelLifecycle.Next.Same"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.Review")[0], "compare the candidate's final-validation results, then save it.", "WpfLearningWorkflow.TrainingModelLifecycle.Next.Review"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.Runtime")[0], "runtime", "WpfLearningWorkflow.TrainingModelLifecycle.Term.Runtime"),
            ("파일 없음", "file missing", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.SettingsNeedSaving")[0], "settings must be saved", "WpfLearningWorkflow.TrainingModelLifecycle.Term.SettingsNeedSaving"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.RuntimeCheckNeeded")[0], "readiness needs review", "WpfLearningWorkflow.TrainingModelLifecycle.Term.RuntimeCheckNeeded"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InferenceOnly")[0], "inspection ready / training unavailable", "WpfLearningWorkflow.TrainingModelLifecycle.Term.InferenceOnly"),
            ("학습 가능 / 검사 모델 필요", "training ready / inspection model required", null),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InferenceTrainingReady")[0], "inspection/training ready", "WpfLearningWorkflow.TrainingModelLifecycle.Term.InferenceTrainingReady"),
            (OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.SettingsNeedChecking")[0], "settings need review", "WpfLearningWorkflow.TrainingModelLifecycle.Term.SettingsNeedChecking"),
            ("설치/연결 필요", "installation/connection required", null),
            ("recipe 저장됨", "saved to recipe", null),
            ("recipe 저장 대기", "waiting to save to recipe", null),
            ("검증 후 저장 판단", "decide after validation", null),
            ("없음", "none", null)
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
            var entries = OpenVisionLanguageService.GetEntries();
            string[] prefixes =
            {
                "현재 검사 모델:",
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending")[0] + ":",
                "Current inspection model:",
                "Inspection model candidate:",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending").English + ":"
            };
            string body = StripPrefix(value, prefixes);
            object currentField = LocalizeCurrentField(value, entries, new[] { "WpfModelRuntimeUnavailable.CurrentModel.NotInstalled", "WpfModelRuntimeUnavailable.CurrentModel.Settings" }, prefixes);
            if (currentField != null) return Text("WpfLearningWorkflow.TrainingModelLifecycle.Current.Value", currentField);
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
            var entries = OpenVisionLanguageService.GetEntries();
            string[] prefixes =
            {
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel")[0] + ":",
                "Training candidate:",
                "New training model candidate:",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel").English + ":"
            };
            string body = StripPrefix(value, prefixes);
            object currentField = LocalizeCurrentField(value, entries, new[] { "WpfModelRuntimeUnavailable.Candidate.Unchecked" }, prefixes.Concat(new[] { "학습 모델:", "Trained model:" }).ToArray());
            if (currentField != null) return Text("WpfLearningWorkflow.TrainingModelLifecycle.Candidate.Value", currentField);
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
            var entries = OpenVisionLanguageService.GetEntries();
            string[] prefixes =
            {
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment")[0] + ":", "Model adoption:",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment").English + ":"
            };
            string body = StripPrefix(value, prefixes);
            object currentField = LocalizeCurrentField(value, entries, new[] { "WpfModelRuntimeUnavailable.Assignment.RuntimeNeeded" }, prefixes);
            if (currentField != null) return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Value", currentField);
            if (Matches(body, "학습 결과 비교 전", "before comparing training results"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Initial");
            }

            if (Matches(body, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending")[0], "candidate selected - save settings") || Matches(body, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending").Korean, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending").English))
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

            if (Matches(body, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review")[0], "review new candidate") || Matches(body, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review").Korean, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review").English))
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
            var entries = OpenVisionLanguageService.GetEntries();
            string[] prefixes =
            {
                "다음:", "Next:",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction").English + ":"
            };
            string body = StripPrefix(value, prefixes);
            object currentField = LocalizeCurrentField(body, entries, new[] { "WpfModelRuntimeUnavailable.NextActionFallback" }, Array.Empty<string>());
            if (currentField != null) return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Value", currentField);
            if (Matches(body, "데이터셋 점검 후 학습을 시작하세요.", "Run Dataset Health, then start training."))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial");
            }

            if (Matches(
                body,
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending")[0],
                "click Save as inspection model to store it in the recipe. This model will be used from the next inference.") || Matches(body, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending").Korean, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending").English))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending");
            }

            if (Matches(body, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly")[0], "start a new training run if needed, or inspect with the current model.") || Matches(body, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly").Korean, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly").English))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly");
            }

            if (Matches(body, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.Same")[0], "use Inspect current model to review inference results.") || Matches(body, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.Same").Korean, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.Same").English))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Same");
            }

            if (Matches(body, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Next.Review")[0], "compare the candidate's final-validation results, then save it.") || Matches(body, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.Review").Korean, entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Next.Review").English))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Next.Review");
            }

            return Text(
                "WpfLearningWorkflow.TrainingModelLifecycle.Next.Value",
                Localize(body));
        }

        private static object LocalizeCurrentField(string value, IReadOnlyList<OpenVisionLocalizationEntry> entries, string[] keys, string[] prefixes)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized)) return null;
            foreach (string key in keys)
            {
                OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                if (entry == null) continue;
                if (!string.Equals(normalized, entry.Korean, StringComparison.Ordinal)
                    && !string.Equals(normalized, entry.English, StringComparison.Ordinal)) continue;
                string korean = string.IsNullOrWhiteSpace(entry.Korean) ? entry.English : entry.Korean;
                string english = string.IsNullOrWhiteSpace(entry.English) ? entry.Korean : entry.English;
                return new TrainingModelLifecycleLocalizedArgument(StripPrefix(korean, prefixes), StripPrefix(english, prefixes));
            }

            return null;
        }

        private static object Localize(string value)
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
            if (found) return new TrainingModelLifecycleLocalizedArgument(korean, english);

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
                if (found) return new TrainingModelLifecycleLocalizedArgument(korean, english);
            }


            if (Matches(normalized, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Runtime.NotInstalled")[0], "Model runtime not installed")
                || Matches(normalized, "실행 환경 미설치", "Runtime environment not installed"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Runtime.NotInstalled");
            }

            if (Matches(normalized, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Runtime.ConnectionRequired")[0], "Model runtime connection required")
                || Matches(normalized, "실행 환경 연결 필요", "Runtime environment connection required"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Runtime.ConnectionRequired");
            }

            if (Matches(normalized, OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Runtime.SetupNeeded")[0], "Install the model runtime or connect an existing path")
                || Matches(normalized, "실행 환경 설치 또는 경로 연결 필요", "Install the runtime environment or connect an existing path"))
            {
                return Text("WpfLearningWorkflow.TrainingModelLifecycle.Runtime.SetupNeeded");
            }

            int pathSeparator = normalized.IndexOf(" / ", StringComparison.Ordinal);
            string pathPart = pathSeparator < 0 ? normalized : normalized.Substring(0, pathSeparator);
            string extension = System.IO.Path.GetExtension(pathPart);
            if (MvcVisionSystem._1._Core.PythonModelSettingsValidator.LooksLikePath(pathPart)
                || extension.Length > 1 && char.IsLetter(extension[1]) && !extension.Any(char.IsWhiteSpace))
            {
                string suffix = pathSeparator < 0 ? string.Empty : normalized.Substring(pathSeparator);
                return new TrainingModelLifecycleLocalizedArgument(
                    pathPart + ReplaceKnownTerms(suffix, toEnglish: false, entries: entries),
                    pathPart + ReplaceKnownTerms(suffix, toEnglish: true, entries: entries));
            }

            return new TrainingModelLifecycleLocalizedArgument(
                ReplaceKnownTerms(normalized, toEnglish: false, entries: entries),
                ReplaceKnownTerms(normalized, toEnglish: true, entries: entries));
        }

        private static string ReplaceKnownTerms(string value, bool toEnglish, IReadOnlyList<OpenVisionLocalizationEntry> entries)
        {
            string result = value ?? string.Empty;
            foreach ((string korean, string english, string key) in KnownTerms)
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
