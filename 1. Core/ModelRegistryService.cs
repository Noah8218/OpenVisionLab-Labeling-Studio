using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MvcVisionSystem
{
    public static class ModelRegistryService
    {
        public const int ProfileHistoryLimit = 16;
        public const int TrainingRunHistoryLimit = 32;
        public const int CandidateHistoryLimit = 32;
        public const int CandidateDecisionHistoryLimit = 64;
        public const int AdoptionHistoryLimit = 32;
        public const string CandidateDecisionPending = "Pending";
        public const string CandidateDecisionAdopted = "Adopted";
        public const string CandidateDecisionRejected = "Rejected";

        public static ModelCandidate RecordTrainingCandidate(
            ModelRegistrySettings registry,
            PythonModelSettings settings,
            LabelingDatasetPurpose datasetPurpose,
            string outputRootPath,
            string candidateWeightsPath,
            string baselineWeightsPath,
            string metricsSummary,
            string trainingState,
            int trainingProgressPercent,
            string trainingMessage,
            bool savedToRecipe)
        {
            if (registry == null)
            {
                return null;
            }

            registry.EnsureDefaults();
            ModelProfile profile = EnsureProfile(registry, settings, datasetPurpose);
            string candidatePath = NormalizePath(candidateWeightsPath);
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return null;
            }

            string now = UtcNowText();
            string baselinePath = NormalizePath(baselineWeightsPath);
            EnsureBaselineInspectionCandidate(registry, profile, baselinePath, now);

            TrainingRun run = UpsertTrainingRun(
                registry,
                profile,
                outputRootPath,
                candidatePath,
                baselinePath,
                metricsSummary,
                trainingState,
                trainingProgressPercent,
                trainingMessage,
                now);

            ModelCandidate candidate = UpsertCandidate(
                registry,
                profile,
                run,
                candidatePath,
                baselinePath,
                metricsSummary,
                savedToRecipe,
                now);

            registry.CurrentProfileId = profile.ProfileId;
            registry.LatestTrainingRunId = run.TrainingRunId;
            registry.LatestCandidateId = candidate.CandidateId;

            if (savedToRecipe)
            {
                SetCandidateDecisionState(candidate, CandidateDecisionAdopted, "검사 모델로 저장됨", now, savedToRecipe: true);
                MarkCurrentInspectionModel(registry, candidate);
                UpsertAdoption(registry, profile, candidate, baselinePath, metricsSummary, now);
                UpsertCandidateDecision(
                    registry,
                    profile,
                    candidate,
                    baselinePath,
                    CandidateDecisionAdopted,
                    metricsSummary,
                    "검사 모델로 저장됨",
                    savedToRecipe: true,
                    now);
            }
            else
            {
                SetCandidateDecisionState(candidate, CandidateDecisionPending, "검토 대기", now, savedToRecipe: false);
                if (string.Equals(registry.CurrentInspectionModelId, candidate.CandidateId, StringComparison.Ordinal))
                {
                    RestoreBaselineInspectionModel(registry, baselinePath);
                }
            }

            TrimRegistry(registry);
            return candidate;
        }

        public static ModelCandidate RecordCandidateDecision(
            ModelRegistrySettings registry,
            PythonModelSettings settings,
            LabelingDatasetPurpose datasetPurpose,
            string outputRootPath,
            string candidateWeightsPath,
            string baselineWeightsPath,
            string metricsSummary,
            string decision,
            string decisionSummary,
            bool savedToRecipe)
        {
            string normalizedDecision = NormalizeCandidateDecision(decision);
            bool adopt = string.Equals(normalizedDecision, CandidateDecisionAdopted, StringComparison.Ordinal);
            ModelCandidate candidate = RecordTrainingCandidate(
                registry,
                settings,
                datasetPurpose,
                outputRootPath,
                candidateWeightsPath,
                baselineWeightsPath,
                metricsSummary,
                "completed",
                100,
                decisionSummary,
                savedToRecipe: adopt && savedToRecipe);
            if (registry == null || candidate == null)
            {
                return candidate;
            }

            registry.EnsureDefaults();
            ModelProfile profile = registry.Profiles
                .FirstOrDefault(item => string.Equals(item.ProfileId, candidate.ProfileId, StringComparison.Ordinal))
                ?? EnsureProfile(registry, settings, datasetPurpose);
            string now = UtcNowText();
            string baselinePath = NormalizePath(baselineWeightsPath);
            string summary = string.IsNullOrWhiteSpace(decisionSummary)
                ? FormatCandidateDecisionSummary(normalizedDecision)
                : decisionSummary.Trim();
            SetCandidateDecisionState(candidate, normalizedDecision, summary, now, adopt && savedToRecipe);
            UpsertCandidateDecision(
                registry,
                profile,
                candidate,
                baselinePath,
                normalizedDecision,
                metricsSummary,
                summary,
                savedToRecipe: adopt && savedToRecipe,
                now);

            if (string.Equals(normalizedDecision, CandidateDecisionRejected, StringComparison.Ordinal))
            {
                RestoreBaselineInspectionModel(registry, baselinePath);
            }

            TrimRegistry(registry);
            return candidate;
        }

        public static ModelProfile EnsureProfile(
            ModelRegistrySettings registry,
            PythonModelSettings settings,
            LabelingDatasetPurpose datasetPurpose)
        {
            if (registry == null)
            {
                return null;
            }

            registry.EnsureDefaults();
            settings ??= new PythonModelSettings();
            string engine = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine);
            string adapterKey = settings.GetProtocolModelName();
            string projectRoot = NormalizePath(settings.ProjectRootPath);
            string purpose = datasetPurpose.ToString();
            string profileId = BuildStableId("profile", $"{engine}|{adapterKey}|{purpose}|{projectRoot}");
            string now = UtcNowText();

            ModelProfile profile = registry.Profiles
                .FirstOrDefault(item => string.Equals(item.ProfileId, profileId, StringComparison.Ordinal));
            if (profile == null)
            {
                profile = new ModelProfile
                {
                    ProfileId = profileId,
                    CreatedUtc = now
                };
                registry.Profiles.Add(profile);
            }

            profile.DisplayName = BuildProfileDisplayName(engine, datasetPurpose);
            profile.AdapterKey = adapterKey;
            profile.ModelEngine = engine;
            profile.DatasetPurpose = purpose;
            profile.ProjectRootPath = projectRoot;
            profile.LastUsedUtc = now;
            registry.CurrentProfileId = profile.ProfileId;
            TrimProfiles(registry);
            return profile;
        }

        public static ModelCandidate FindCurrentInspectionModel(ModelRegistrySettings registry)
        {
            registry?.EnsureDefaults();
            string currentId = registry?.CurrentInspectionModelId ?? string.Empty;
            return string.IsNullOrWhiteSpace(currentId)
                ? null
                : registry.Candidates.FirstOrDefault(item => string.Equals(item.CandidateId, currentId, StringComparison.Ordinal));
        }

        public static ModelCandidate FindLatestCandidate(ModelRegistrySettings registry)
        {
            registry?.EnsureDefaults();
            string candidateId = registry?.LatestCandidateId ?? string.Empty;
            return string.IsNullOrWhiteSpace(candidateId)
                ? null
                : registry.Candidates.FirstOrDefault(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal));
        }

        public static TrainingRun FindLatestTrainingRun(ModelRegistrySettings registry)
        {
            registry?.EnsureDefaults();
            string runId = registry?.LatestTrainingRunId ?? string.Empty;
            return string.IsNullOrWhiteSpace(runId)
                ? null
                : registry.TrainingRuns.FirstOrDefault(item => string.Equals(item.TrainingRunId, runId, StringComparison.Ordinal));
        }

        public static ModelCandidateDecision FindLatestCandidateDecision(ModelRegistrySettings registry)
        {
            registry?.EnsureDefaults();
            return registry?.CandidateDecisions?
                .OrderByDescending(item => ParseUtc(item?.DecidedUtc))
                .FirstOrDefault();
        }

        private static TrainingRun UpsertTrainingRun(
            ModelRegistrySettings registry,
            ModelProfile profile,
            string outputRootPath,
            string candidateWeightsPath,
            string baselineWeightsPath,
            string metricsSummary,
            string trainingState,
            int trainingProgressPercent,
            string trainingMessage,
            string now)
        {
            string runId = BuildStableId("run", $"{candidateWeightsPath}|{NormalizePath(outputRootPath)}");
            TrainingRun run = registry.TrainingRuns
                .FirstOrDefault(item => string.Equals(item.TrainingRunId, runId, StringComparison.Ordinal));
            if (run == null)
            {
                run = new TrainingRun
                {
                    TrainingRunId = runId
                };
                registry.TrainingRuns.Add(run);
            }

            run.ProfileId = profile?.ProfileId ?? string.Empty;
            run.EventUtc = now;
            run.OutputRootPath = NormalizePath(outputRootPath);
            run.State = trainingState ?? string.Empty;
            run.ProgressPercent = trainingProgressPercent;
            run.Message = trainingMessage ?? string.Empty;
            run.CandidateWeightsPath = candidateWeightsPath;
            run.BaselineWeightsPath = baselineWeightsPath;
            run.MetricsSummary = metricsSummary ?? string.Empty;
            return run;
        }

        private static ModelCandidate UpsertCandidate(
            ModelRegistrySettings registry,
            ModelProfile profile,
            TrainingRun run,
            string candidateWeightsPath,
            string baselineWeightsPath,
            string metricsSummary,
            bool savedToRecipe,
            string now)
        {
            string candidateId = BuildStableId("candidate", candidateWeightsPath);
            ModelCandidate candidate = registry.Candidates
                .FirstOrDefault(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal));
            if (candidate == null)
            {
                candidate = new ModelCandidate
                {
                    CandidateId = candidateId,
                    CreatedUtc = now
                };
                registry.Candidates.Add(candidate);
            }

            candidate.ProfileId = profile?.ProfileId ?? string.Empty;
            candidate.TrainingRunId = run?.TrainingRunId ?? string.Empty;
            candidate.WeightsPath = candidateWeightsPath;
            candidate.BaselineWeightsPath = baselineWeightsPath;
            candidate.MetricsSummary = metricsSummary ?? string.Empty;
            candidate.LastSeenUtc = now;
            candidate.SavedToRecipe = savedToRecipe;
            candidate.IsCurrentInspectionModel = false;
            return candidate;
        }

        private static void EnsureBaselineInspectionCandidate(
            ModelRegistrySettings registry,
            ModelProfile profile,
            string baselineWeightsPath,
            string now)
        {
            if (string.IsNullOrWhiteSpace(baselineWeightsPath))
            {
                return;
            }

            string baselineCandidateId = BuildStableId("candidate", baselineWeightsPath);
            ModelCandidate baseline = registry.Candidates
                .FirstOrDefault(item => string.Equals(item.CandidateId, baselineCandidateId, StringComparison.Ordinal));
            if (baseline == null)
            {
                baseline = new ModelCandidate
                {
                    CandidateId = baselineCandidateId,
                    CreatedUtc = now,
                    WeightsPath = baselineWeightsPath
                };
                registry.Candidates.Add(baseline);
            }

            baseline.ProfileId = profile?.ProfileId ?? string.Empty;
            baseline.LastSeenUtc = now;
            baseline.SavedToRecipe = true;
            if (string.IsNullOrWhiteSpace(baseline.Decision))
            {
                SetCandidateDecisionState(baseline, CandidateDecisionAdopted, "기존 검사 모델", now, savedToRecipe: true);
            }

            if (string.IsNullOrWhiteSpace(registry.CurrentInspectionModelId))
            {
                MarkCurrentInspectionModel(registry, baseline);
            }
        }

        private static void RestoreBaselineInspectionModel(ModelRegistrySettings registry, string baselineWeightsPath)
        {
            if (string.IsNullOrWhiteSpace(baselineWeightsPath))
            {
                registry.CurrentInspectionModelId = string.Empty;
                return;
            }

            string baselineCandidateId = BuildStableId("candidate", baselineWeightsPath);
            ModelCandidate baseline = registry.Candidates
                .FirstOrDefault(item => string.Equals(item.CandidateId, baselineCandidateId, StringComparison.Ordinal));
            if (baseline == null)
            {
                registry.CurrentInspectionModelId = string.Empty;
                return;
            }

            MarkCurrentInspectionModel(registry, baseline);
        }

        private static void MarkCurrentInspectionModel(ModelRegistrySettings registry, ModelCandidate current)
        {
            foreach (ModelCandidate candidate in registry.Candidates)
            {
                candidate.IsCurrentInspectionModel = false;
            }

            current.IsCurrentInspectionModel = true;
            current.SavedToRecipe = true;
            registry.CurrentInspectionModelId = current.CandidateId;
        }

        private static void SetCandidateDecisionState(
            ModelCandidate candidate,
            string decision,
            string decisionSummary,
            string now,
            bool savedToRecipe)
        {
            if (candidate == null)
            {
                return;
            }

            candidate.Decision = NormalizeCandidateDecision(decision);
            candidate.DecisionUtc = now ?? string.Empty;
            candidate.DecisionSummary = decisionSummary ?? string.Empty;
            candidate.SavedToRecipe = savedToRecipe;
        }

        private static ModelCandidateDecision UpsertCandidateDecision(
            ModelRegistrySettings registry,
            ModelProfile profile,
            ModelCandidate candidate,
            string previousWeightsPath,
            string decision,
            string metricsSummary,
            string decisionSummary,
            bool savedToRecipe,
            string now)
        {
            if (registry == null || candidate == null)
            {
                return null;
            }

            string normalizedDecision = NormalizeCandidateDecision(decision);
            string decisionId = BuildStableId("candidate-decision", $"{candidate.CandidateId}|{normalizedDecision}|{previousWeightsPath}");
            ModelCandidateDecision record = registry.CandidateDecisions
                .FirstOrDefault(item => string.Equals(item.DecisionId, decisionId, StringComparison.Ordinal));
            if (record == null)
            {
                record = new ModelCandidateDecision
                {
                    DecisionId = decisionId
                };
                registry.CandidateDecisions.Add(record);
            }

            record.ProfileId = profile?.ProfileId ?? string.Empty;
            record.CandidateId = candidate.CandidateId ?? string.Empty;
            record.WeightsPath = candidate.WeightsPath ?? string.Empty;
            record.PreviousWeightsPath = previousWeightsPath ?? string.Empty;
            record.Decision = normalizedDecision;
            record.DecidedUtc = now ?? string.Empty;
            record.SavedToRecipe = savedToRecipe;
            record.MetricsSummary = metricsSummary ?? string.Empty;
            record.DecisionSummary = decisionSummary ?? string.Empty;
            return record;
        }

        private static void UpsertAdoption(
            ModelRegistrySettings registry,
            ModelProfile profile,
            ModelCandidate candidate,
            string previousWeightsPath,
            string decisionSummary,
            string now)
        {
            string adoptionId = BuildStableId("adoption", $"{candidate?.CandidateId}|{candidate?.WeightsPath}|{previousWeightsPath}");
            InspectionModelAdoption adoption = registry.AdoptionHistory
                .FirstOrDefault(item => string.Equals(item.AdoptionId, adoptionId, StringComparison.Ordinal));
            if (adoption == null)
            {
                adoption = new InspectionModelAdoption
                {
                    AdoptionId = adoptionId
                };
                registry.AdoptionHistory.Add(adoption);
            }

            adoption.ProfileId = profile?.ProfileId ?? string.Empty;
            adoption.CandidateId = candidate?.CandidateId ?? string.Empty;
            adoption.WeightsPath = candidate?.WeightsPath ?? string.Empty;
            adoption.PreviousWeightsPath = previousWeightsPath ?? string.Empty;
            adoption.AdoptedUtc = now;
            adoption.SavedToRecipe = true;
            adoption.DecisionSummary = decisionSummary ?? string.Empty;
        }

        private static void TrimRegistry(ModelRegistrySettings registry)
        {
            TrimProfiles(registry);
            TrimByUtc(registry.TrainingRuns, TrainingRunHistoryLimit, item => item.EventUtc);
            TrimByUtc(registry.Candidates, CandidateHistoryLimit, item => item.LastSeenUtc);
            TrimByUtc(registry.CandidateDecisions, CandidateDecisionHistoryLimit, item => item.DecidedUtc);
            TrimByUtc(registry.AdoptionHistory, AdoptionHistoryLimit, item => item.AdoptedUtc);
        }

        private static void TrimProfiles(ModelRegistrySettings registry)
            => TrimByUtc(registry.Profiles, ProfileHistoryLimit, item => item.LastUsedUtc);

        private static void TrimByUtc<T>(System.Collections.Generic.List<T> items, int limit, Func<T, string> getUtc)
        {
            if (items == null || limit <= 0)
            {
                return;
            }

            while (items.Count > limit)
            {
                T oldest = items
                    .OrderBy(item => ParseUtc(getUtc(item)))
                    .First();
                items.Remove(oldest);
            }
        }

        private static string BuildProfileDisplayName(string engine, LabelingDatasetPurpose datasetPurpose)
        {
            string task = datasetPurpose switch
            {
                LabelingDatasetPurpose.Segmentation => "segmentation",
                LabelingDatasetPurpose.AnomalyDetection => "anomaly",
                _ => "object-detection"
            };

            return $"{engine} {task}";
        }

        private static string NormalizeCandidateDecision(string decision)
        {
            string text = (decision ?? string.Empty).Trim();
            if (string.Equals(text, CandidateDecisionAdopted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Saved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Save", StringComparison.OrdinalIgnoreCase))
            {
                return CandidateDecisionAdopted;
            }

            if (string.Equals(text, CandidateDecisionRejected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Reject", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "RejectedCandidate", StringComparison.OrdinalIgnoreCase))
            {
                return CandidateDecisionRejected;
            }

            return CandidateDecisionPending;
        }

        private static string FormatCandidateDecisionSummary(string decision)
        {
            return NormalizeCandidateDecision(decision) switch
            {
                CandidateDecisionAdopted => "검사 모델로 저장됨",
                CandidateDecisionRejected => "후보 거절, 기존 검사 모델 유지",
                _ => "검토 대기"
            };
        }

        private static string BuildStableId(string prefix, string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return $"{prefix}-{Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant()}";
        }

        private static string NormalizePath(string path)
        {
            string trimmed = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(trimmed);
            }
            catch
            {
                return trimmed;
            }
        }

        private static string UtcNowText()
            => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        private static DateTime ParseUtc(string value)
            => DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc)
                ? utc
                : DateTime.MinValue;
    }
}
