using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class ApplicationClosePolicyService
    {
        public IReadOnlyList<string> GetActiveWorkNames(ApplicationCloseWorkState state)
        {
            state ??= new ApplicationCloseWorkState();

            var names = new List<string>();
            if (state.IsCreatingSmartMask)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.SmartMask"));
            }
            if (state.IsDetecting)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.CurrentInference"));
            }
            if (state.IsBatchDetectionRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.BatchInference"));
            }
            if (state.IsExternalYoloDatasetIntakeRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.ExternalDataset"));
            }
            if (state.IsExternalEvaluationDataAuditRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.ExternalEvaluation"));
            }
            if (state.IsHistoricalSegmentationRemediationAuditRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.SegmentationCorrection"));
            }
            if (state.IsTrainingRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.Training"));
            }
            if (state.IsYoloEnvironmentCommandRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.Environment"));
            }
            if (state.IsModelComparisonRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.Comparison"));
            }
            if (state.IsSegmentationAdapterComparisonRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.SegmentationComparison"));
            }
            if (state.IsAnomalyEvaluationRunning)
            {
                names.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfClose.ActiveWork.AnomalyEvaluation"));
            }

            return names;
        }

        public ApplicationClosePlan Build(ApplicationCloseState state)
        {
            state ??= new ApplicationCloseState();

            string[] activeWorkNames = (state.ActiveWorkNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            int pendingCandidateCount = Math.Max(0, state.PendingCandidateCount);
            bool hasUnsavedAnnotations = state.HasUnsavedAnnotations;
            bool hasPendingTrainingResult = !string.IsNullOrWhiteSpace(state.PendingTrainingWeightsPath);
            if (!hasUnsavedAnnotations && pendingCandidateCount == 0 && activeWorkNames.Length == 0 && !hasPendingTrainingResult)
            {
                return ApplicationClosePlan.NoPrompt;
            }

            var messageLines = new List<string>();
            if (hasUnsavedAnnotations)
            {
                messageLines.Add(OpenVisionLanguageService.T("ApplicationClose.UnsavedLabelsMessage"));
            }

            if (pendingCandidateCount > 0)
            {
                messageLines.Add(
                    string.Format(OpenVisionLanguageService.T("ApplicationClose.CandidateNotSaved"), pendingCandidateCount));
            }

            if (activeWorkNames.Length > 0)
            {
                messageLines.Add(
                    string.Format(OpenVisionLanguageService.T("ApplicationClose.ActiveWorkStops"), activeWorkNames.Length));
            }

            if (hasPendingTrainingResult) messageLines.Add(OpenVisionLanguageService.T("ApplicationClose.PendingTrainingMessage"));

            messageLines.Add(
                hasUnsavedAnnotations
                    ? OpenVisionLanguageService.T("ApplicationClose.SaveChoice")
                    : pendingCandidateCount > 0 && activeWorkNames.Length > 0
                        ? OpenVisionLanguageService.T("ApplicationClose.StopWorkChoice")
                        : activeWorkNames.Length > 0
                            ? OpenVisionLanguageService.T("ApplicationClose.StopChoice")
                            : hasPendingTrainingResult
                                ? OpenVisionLanguageService.T("ApplicationClose.PendingTrainingChoice")
                                : OpenVisionLanguageService.T("ApplicationClose.CloseWithoutSavingChoice"));

            var detailLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(state.ActiveImagePath))
            {
                detailLines.Add(string.Format(OpenVisionLanguageService.T("ApplicationClose.ImageDetail"), Path.GetFileName(state.ActiveImagePath)));
            }

            if (hasUnsavedAnnotations && !string.IsNullOrWhiteSpace(state.UnsavedAnnotationReason))
            {
                detailLines.Add(string.Format(OpenVisionLanguageService.T("ApplicationClose.UnsavedEditsDetail"), state.UnsavedAnnotationReason.Trim()));
            }

            if (pendingCandidateCount > 0)
            {
                detailLines.Add(string.Format(OpenVisionLanguageService.T("ApplicationClose.PendingCandidatesDetail"), pendingCandidateCount));
            }

            if (activeWorkNames.Length > 0)
            {
                detailLines.Add(string.Format(OpenVisionLanguageService.T("ApplicationClose.StopWorkDetail"), string.Join(", ", activeWorkNames)));
            }

            if (hasPendingTrainingResult) detailLines.Add(OpenVisionLanguageService.T("ApplicationClose.RetainedTrainingFile") + ": " + state.PendingTrainingWeightsPath);

            return new ApplicationClosePlan
            {
                PromptKind = hasUnsavedAnnotations
                    ? WpfApplicationClosePromptKind.SaveDiscardCancel
                    : WpfApplicationClosePromptKind.DiscardCancel,
                Title = hasUnsavedAnnotations
                    ? OpenVisionLanguageService.T("ApplicationClose.UnsavedLabelsTitle")
                    : pendingCandidateCount > 0
                        ? OpenVisionLanguageService.T("ApplicationClose.PendingCandidatesTitle")
                        : hasPendingTrainingResult
                            ? OpenVisionLanguageService.T("ApplicationClose.PendingTrainingTitle")
                            : OpenVisionLanguageService.T("ApplicationClose.ActiveWorkTitle"),
                Message = string.Join(Environment.NewLine, messageLines),
                Details = string.Join(Environment.NewLine, detailLines),
                PrimaryButtonText = hasUnsavedAnnotations
                    ? OpenVisionLanguageService.T("ApplicationClose.SaveAndClose")
                    : pendingCandidateCount > 0 && activeWorkNames.Length > 0
                        ? OpenVisionLanguageService.T("ApplicationClose.StopWorkAndClose")
                        : activeWorkNames.Length > 0
                            ? OpenVisionLanguageService.T("ApplicationClose.StopWorkAndClose")
                            : hasPendingTrainingResult && pendingCandidateCount == 0
                                ? OpenVisionLanguageService.T("ApplicationClose.KeepTrainingFileAndClose")
                                : OpenVisionLanguageService.T("ApplicationClose.CloseWithoutSaving"),
                SecondaryButtonText = hasUnsavedAnnotations ? OpenVisionLanguageService.T("ApplicationClose.CloseWithoutSaving") : OpenVisionLanguageService.T("ApplicationClose.ContinueWorking"),
                TertiaryButtonText = hasUnsavedAnnotations ? OpenVisionLanguageService.T("ApplicationClose.ContinueWorking") : string.Empty
            };
        }
    }

    [Obsolete("Use ApplicationClosePolicyService.", false)]
    public sealed class WpfApplicationClosePolicyService : ApplicationClosePolicyService
    {
        public WpfApplicationClosePlan Build(WpfApplicationCloseState state)
            => new WpfApplicationClosePlan(base.Build(state));
    }

    public class ApplicationCloseWorkState
    {
        public bool IsCreatingSmartMask { get; set; }

        public bool IsDetecting { get; set; }

        public bool IsBatchDetectionRunning { get; set; }

        public bool IsExternalYoloDatasetIntakeRunning { get; set; }

        public bool IsExternalEvaluationDataAuditRunning { get; set; }

        public bool IsHistoricalSegmentationRemediationAuditRunning { get; set; }

        public bool IsTrainingRunning { get; set; }

        public bool IsYoloEnvironmentCommandRunning { get; set; }

        public bool IsModelComparisonRunning { get; set; }

        public bool IsSegmentationAdapterComparisonRunning { get; set; }

        public bool IsAnomalyEvaluationRunning { get; set; }
    }

    public class ApplicationCloseState
    {
        public bool HasUnsavedAnnotations { get; set; }

        public string UnsavedAnnotationReason { get; set; } = string.Empty;

        public int PendingCandidateCount { get; set; }

        public IReadOnlyList<string> ActiveWorkNames { get; set; } = Array.Empty<string>();

        public string ActiveImagePath { get; set; } = string.Empty;

        public string PendingTrainingWeightsPath { get; set; } = string.Empty;
    }

    public enum WpfApplicationClosePromptKind
    {
        None,
        SaveDiscardCancel,
        DiscardCancel
    }

    public enum WpfApplicationCloseDecision
    {
        Cancel,
        SaveAndClose,
        DiscardAndClose
    }

    public class ApplicationClosePlan
    {
        public static ApplicationClosePlan NoPrompt { get; } = new ApplicationClosePlan();

        public WpfApplicationClosePromptKind PromptKind { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public string PrimaryButtonText { get; set; } = string.Empty;

        public string SecondaryButtonText { get; set; } = string.Empty;

        public string TertiaryButtonText { get; set; } = string.Empty;

        public bool RequiresPrompt => PromptKind != WpfApplicationClosePromptKind.None;
    }

    [Obsolete("Use ApplicationCloseWorkState.", false)]
    public sealed class WpfApplicationCloseWorkState : ApplicationCloseWorkState
    {
    }

    [Obsolete("Use ApplicationCloseState.", false)]
    public sealed class WpfApplicationCloseState : ApplicationCloseState
    {
    }

    [Obsolete("Use ApplicationClosePlan.", false)]
    public sealed class WpfApplicationClosePlan : ApplicationClosePlan
    {
        public static new WpfApplicationClosePlan NoPrompt { get; } = new WpfApplicationClosePlan();

        public WpfApplicationClosePlan()
        {
        }

        public WpfApplicationClosePlan(ApplicationClosePlan source)
        {
            if (source == null)
            {
                return;
            }

            PromptKind = source.PromptKind;
            Title = source.Title;
            Message = source.Message;
            Details = source.Details;
            PrimaryButtonText = source.PrimaryButtonText;
            SecondaryButtonText = source.SecondaryButtonText;
            TertiaryButtonText = source.TertiaryButtonText;
        }
    }
}
