using System;
using System.Globalization;
using OpenVisionLab;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public sealed class WpfStatusBarPanelViewModel : WpfObservableViewModel, IDisposable
    {
        private string datasetStatusText = "Dataset: waiting";
        private string workflowStageText = "단계: 준비";
        private string workflowProgressText = "진행: 이미지 없음";
        private string workflowNextActionText = "다음: 이미지 선택";
        private string pythonStatusText = OpenVisionLanguageService.T("WpfShell.Status.InferenceWaiting");
        private string rawPythonStatusText;
        private string rawModelStatusText;
        private string completedPythonModelSourceText;
        private int? completedPythonCandidateCount;
        private string inspectionModelStatusText = "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C";
        private string inspectionModelStatusToolTip = "\uD604\uC7AC \uCD94\uB860\uC5D0 \uC0AC\uC6A9\uD560 \uBAA8\uB378\uC744 \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string modelStatusText = "Model: waiting";
        private YoloWorkerSmokeCandidate presentedModelCandidate;
        private System.Drawing.Rectangle presentedModelCandidateBounds;
        private string modelStatusAutomationText = string.Empty;
        private bool disposed;
        private bool isAnnotationDirty;
        private string annotationSaveStatusKey = "Waiting";
        private AnomalyImageReviewState? anomalyReviewState;
        private string annotationSaveStatusText = "\uB77C\uBCA8 \uB300\uAE30";
        private string annotationSaveStatusToolTip = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";

        public WpfStatusBarPanelViewModel()
        {
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

        public string ViewName => nameof(WpfStatusBarPanel);

        public string DatasetStatusText
        {
            get => datasetStatusText;
            private set => SetProperty(ref datasetStatusText, value ?? string.Empty);
        }

        public string WorkflowStageText
        {
            get => workflowStageText;
            private set => SetProperty(ref workflowStageText, value ?? string.Empty);
        }

        public string WorkflowProgressText
        {
            get => workflowProgressText;
            private set => SetProperty(ref workflowProgressText, value ?? string.Empty);
        }

        public string WorkflowNextActionText
        {
            get => workflowNextActionText;
            private set => SetProperty(ref workflowNextActionText, value ?? string.Empty);
        }

        public string PythonStatusText
        {
            get => pythonStatusText;
            private set => SetProperty(ref pythonStatusText, value ?? string.Empty);
        }

        public string InspectionModelStatusText
        {
            get => inspectionModelStatusText;
            private set => SetProperty(ref inspectionModelStatusText, value ?? string.Empty);
        }

        public string InspectionModelStatusToolTip
        {
            get => TryLocalizeCurrentInferenceModelText(inspectionModelStatusToolTip, true, out string localized)
                ? localized : LocalizationTextRuntimeService.Translate(inspectionModelStatusToolTip);
            private set => SetProperty(ref inspectionModelStatusToolTip, value ?? string.Empty);
        }

        public string ModelStatusText
        {
            get => modelStatusText;
            private set => SetProperty(ref modelStatusText, value ?? string.Empty);
        }

        public string ModelStatusAutomationText
        {
            get => modelStatusAutomationText;
            private set => SetProperty(ref modelStatusAutomationText, value ?? string.Empty);
        }

        public bool IsAnnotationDirty
        {
            get => isAnnotationDirty;
            private set => SetProperty(ref isAnnotationDirty, value);
        }

        public string AnnotationSaveStatusText
        {
            get => annotationSaveStatusText;
            private set => SetProperty(ref annotationSaveStatusText, value ?? string.Empty);
        }

        public string AnnotationSaveStatusToolTip
        {
            get => annotationSaveStatusToolTip;
            private set => SetProperty(ref annotationSaveStatusToolTip, value ?? string.Empty);
        }

        public string AnnotationSaveStatusKey => annotationSaveStatusKey;

        private bool IsAnomalySavePresentation => anomalyReviewState.HasValue && !IsAnnotationDirty && annotationSaveStatusKey != "SaveError" && annotationSaveStatusKey != "LoadError";

        public string AnnotationSaveBadgeKey => IsAnomalySavePresentation
            ? anomalyReviewState == AnomalyImageReviewState.Unreviewed ? "Unreviewed" : "Saved"
            : annotationSaveStatusKey;

        public string AnnotationSaveBadgeToolTip => IsAnomalySavePresentation ? OpenVisionLanguageService.T("WpfAnomalyGrid.SaveHint") : AnnotationSaveStatusToolTip;

        public string AnnotationSaveBadgeText => AnnotationSaveBadgeKey == "SaveError" || AnnotationSaveBadgeKey == "LoadError"
            ? LocalizationTextRuntimeService.Translate(AnnotationSaveStatusText)
            : AnnotationSaveBadgeKey == "Unreviewed"
                ? OpenVisionLanguageService.T("WpfAnomalyGrid.Unreviewed")
                : IsAnnotationDirty || AnnotationSaveBadgeKey == "Unlabeled"
                ? OpenVisionLanguageService.T("WpfShell.SaveBadge.Unsaved")
                : AnnotationSaveBadgeKey == "NoObjectSaved"
                    ? OpenVisionLanguageService.T("WpfShell.SaveBadge.NoObject")
                    : AnnotationSaveBadgeKey == "Saved"
                    ? OpenVisionLanguageService.T("WpfShell.SaveBadge.Saved")
                    : LocalizationTextRuntimeService.Translate(AnnotationSaveStatusText);

        public string AnnotationSaveErrorDetailText
        {
            get
            {
                const string preservedPrefix = "원본 라벨을 보존했습니다. ";
                return annotationSaveStatusKey == "LoadError" && AnnotationSaveStatusToolTip.StartsWith(preservedPrefix, StringComparison.Ordinal)
                    ? Format("WpfShell.SaveBadge.LoadPreservedDetail", AnnotationSaveStatusToolTip.Substring(preservedPrefix.Length))
                    : LocalizationTextRuntimeService.Translate(AnnotationSaveStatusToolTip);
            }
        }

        public void SetAnnotationSaveStatus(bool isDirty, string text, string toolTip, string statusKey = null)
        {
            SetProperty(ref annotationSaveStatusKey, statusKey ?? (isDirty ? "Dirty" : "Waiting"), nameof(AnnotationSaveStatusKey));
            IsAnnotationDirty = isDirty;
            AnnotationSaveStatusText = text;
            AnnotationSaveStatusToolTip = toolTip;
            NotifySaveBadgeChanged();
            OnPropertyChanged(nameof(AnnotationSaveErrorDetailText));
        }

        public void SetAnomalyReviewSaveState(AnomalyImageReviewState? state)
        {
            if (anomalyReviewState == state) return;
            anomalyReviewState = state;
            NotifySaveBadgeChanged();
        }

        private void NotifySaveBadgeChanged()
        {
            OnPropertyChanged(nameof(AnnotationSaveBadgeKey));
            OnPropertyChanged(nameof(AnnotationSaveBadgeText));
            OnPropertyChanged(nameof(AnnotationSaveBadgeToolTip));
        }

        public void SetDatasetStatus(string text)
        {
            DatasetStatusText = text;
        }

        public void SetWorkflowStatus(string stageText, string progressText, string nextActionText)
        {
            WorkflowStageText = stageText;
            WorkflowProgressText = progressText;
            WorkflowNextActionText = nextActionText;
        }

        public void SetPythonStatus(string text)
        {
            completedPythonModelSourceText = null;
            completedPythonCandidateCount = null;
            rawPythonStatusText = text ?? string.Empty;
            PythonStatusText = LocalizePythonStatus(rawPythonStatusText);
        }

        public void SetPythonCompletedStatus(string modelSourceText, int candidateCount)
        {
            completedPythonModelSourceText = modelSourceText;
            completedPythonCandidateCount = candidateCount;
            PythonStatusText = InferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(modelSourceText, candidateCount);
        }

        public void SetInspectionModelStatus(string text, string toolTip)
        {
            string statusText = string.IsNullOrWhiteSpace(text)
                ? "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : text.Trim();
            InspectionModelStatusText = LocalizeInspectionModelStatus(statusText);
            InspectionModelStatusToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? InspectionModelStatusText
                : LocalizationTextRuntimeService.Translate(toolTip.Trim());
        }

        public void SetModelStatus(string text)
        {
            presentedModelCandidate = null;
            rawModelStatusText = text ?? string.Empty;
            ModelStatusText = LocalizeModelStatus(rawModelStatusText);
        }

        public void SetCandidateModelStatus(YoloWorkerSmokeCandidate candidate, System.Drawing.Rectangle bounds)
        {
            presentedModelCandidate = new YoloWorkerSmokeCandidate
            {
                ClassName = candidate.ClassName, CandidateType = candidate.CandidateType,
                Confidence = candidate.Confidence, ImageLevel = candidate.ImageLevel
            };
            presentedModelCandidateBounds = bounds;
            ModelStatusText = CandidateReviewPresenter.BuildSelectedCandidateStatus(presentedModelCandidate, bounds);
        }

        public void SetModelStatusAutomationText(string text)
        {
            // Keep machine-readable diagnostics separate from visible status text so
            // fast tool switches do not hide the commit signal used by real EXE smoke.
            ModelStatusAutomationText = text;
        }

        public void RefreshLocalizedPresentation()
        {
            DatasetStatusText = LocalizationTextRuntimeService.Translate(DatasetStatusText);
            PythonStatusText = completedPythonCandidateCount.HasValue
                ? InferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(completedPythonModelSourceText, completedPythonCandidateCount.Value)
                : LocalizePythonStatus(rawPythonStatusText ?? PythonStatusText);
            InspectionModelStatusText = LocalizeInspectionModelStatus(InspectionModelStatusText);
            InspectionModelStatusToolTip = LocalizationTextRuntimeService.Translate(InspectionModelStatusToolTip);
            ModelStatusText = presentedModelCandidate != null
                ? CandidateReviewPresenter.BuildSelectedCandidateStatus(presentedModelCandidate, presentedModelCandidateBounds)
                : LocalizeModelStatus(rawModelStatusText ?? ModelStatusText);
            AnnotationSaveStatusText = LocalizationTextRuntimeService.Translate(AnnotationSaveStatusText);
            AnnotationSaveStatusToolTip = LocalizationTextRuntimeService.Translate(AnnotationSaveStatusToolTip);
            NotifySaveBadgeChanged();
            OnPropertyChanged(nameof(AnnotationSaveErrorDetailText));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
        }

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            RefreshLocalizedPresentation();
        }

        private static string LocalizePythonStatus(string text)
        {
            System.Text.RegularExpressions.Match counts = System.Text.RegularExpressions.Regex.Match(text ?? string.Empty,
                @"\A(?:추론: 대기|Inference: pending) (?<pending>[0-9]+) / (?:확정|accepted) (?<accepted>[0-9]+)\z",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            return counts.Success
                ? Format("WpfShell.Status.CandidateCounts", counts.Groups["pending"].Value, counts.Groups["accepted"].Value)
                : LocalizationTextRuntimeService.Translate(text);
        }

        private static string LocalizeModelStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            string value = text.Trim();
            System.Text.RegularExpressions.Match added = System.Text.RegularExpressions.Regex.Match(value,
                @"\A(?:라벨 추가:|Label added:) (?<shape>박스|Box|타원|Ellipse) (?:크기|Size) (?<width>[0-9]+)x(?<height>[0-9]+) / (?:위치|Position) x=(?<x>-?[0-9]+), y=(?<y>-?[0-9]+)\z",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (added.Success)
            {
                string shapeKey = added.Groups["shape"].Value is "박스" or "Box" ? "WpfAnnotationTool.Rectangle" : "WpfObjectReview.Row.Ellipse";
                string bounds = Format("WpfObjectReview.Bounds.CompactFormat", added.Groups["width"].Value, added.Groups["height"].Value, added.Groups["x"].Value, added.Groups["y"].Value);
                return Format("WpfShell.Status.LabelAdded", OpenVisionLanguageService.T(shapeKey), bounds);
            }
            if (TryLocalizeCurrentInferenceModelText(value, false, out string currentText)) return currentText;
            if (value.StartsWith("검사 모델: ", StringComparison.Ordinal)
                || value.StartsWith("검사 후보: ", StringComparison.Ordinal)
                || value.StartsWith("Inspection model: ", StringComparison.Ordinal)
                || value.StartsWith("Inspection candidate: ", StringComparison.Ordinal))
            {
                return LocalizeInspectionModelStatus(text);
            }

            const string koreanPrefix = "모델: ";
            const string englishPrefix = "Model: ";
            if (value.StartsWith(koreanPrefix, StringComparison.Ordinal))
            {
                return Format("WpfShell.Status.Model", LocalizeModelState(value.Substring(koreanPrefix.Length)));
            }

            if (value.StartsWith(englishPrefix, StringComparison.Ordinal))
            {
                return Format("WpfShell.Status.Model", LocalizeModelState(value.Substring(englishPrefix.Length)));
            }

            if (string.Equals(value, "\uB3C4\uAD6C: \uC120\uD0DD", StringComparison.Ordinal)
                || string.Equals(value, "Tool: selected", StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfShell.Status.ToolSelected");
            }

            return LocalizationTextRuntimeService.Translate(text);
        }

        private static string LocalizeInspectionModelStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            string value = text.Trim();
            if (TryLocalizeCurrentInferenceModelText(value, false, out string currentText)) return currentText;
            if (string.Equals(value, "검사 모델: 없음", StringComparison.Ordinal)
                || string.Equals(value, "Inspection model: none", StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfShell.Status.ModelNone");
            }

            const string koreanCapabilityPrefix = "모델 기능: ";
            const string englishCapabilityPrefix = "Model capability: ";
            if (value.StartsWith(koreanCapabilityPrefix, StringComparison.Ordinal))
            {
                return Format(
                    "WpfShell.Status.ModelCapability",
                    LocalizeModelState(value.Substring(koreanCapabilityPrefix.Length)));
            }

            if (value.StartsWith(englishCapabilityPrefix, StringComparison.Ordinal))
            {
                return Format(
                    "WpfShell.Status.ModelCapability",
                    LocalizeModelState(value.Substring(englishCapabilityPrefix.Length)));
            }

            string key;
            string prefix;
            if (value.StartsWith("검사 후보: ", StringComparison.Ordinal))
            {
                key = "WpfShell.Status.InspectionCandidate";
                prefix = "검사 후보: ";
            }
            else if (value.StartsWith("Inspection candidate: ", StringComparison.Ordinal))
            {
                key = "WpfShell.Status.InspectionCandidate";
                prefix = "Inspection candidate: ";
            }
            else if (value.StartsWith("검사 모델: ", StringComparison.Ordinal))
            {
                key = "WpfShell.Status.InspectionModel";
                prefix = "검사 모델: ";
            }
            else if (value.StartsWith("Inspection model: ", StringComparison.Ordinal))
            {
                key = "WpfShell.Status.InspectionModel";
                prefix = "Inspection model: ";
            }
            else
            {
                return LocalizationTextRuntimeService.Translate(text);
            }

            string[] parts = value.Substring(prefix.Length).Split(new[] { " / " }, 2, StringSplitOptions.None);
            return parts.Length == 2
                ? Format(key, parts[0], parts[1])
                : LocalizationTextRuntimeService.Translate(text);
        }

        private static bool TryLocalizeCurrentInferenceModelText(string text, bool isToolTip, out string localized)
        {
            localized = text;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] keys = isToolTip
                ? new[] { "WpfInference.Text.InferenceStatusPresentationService.PendingModelToolTip", "WpfInference.Text.InferenceStatusPresentationService.InspectionModelToolTip" }
                : new[] { "WpfShell.Status.InspectionCandidate" };
            foreach (string key in keys)
            {
                if (!LocalizationTextRuntimeService.TryGetCachedTextPair(key, out string korean, out string english)) continue;
                foreach (string template in new[] { korean, english })
                {
                    int first = template.IndexOf("{0}", StringComparison.Ordinal), second = template.IndexOf("{1}", StringComparison.Ordinal);
                    if (first < 0 || second <= first || template.IndexOf("{0}", first + 3, StringComparison.Ordinal) >= 0
                        || template.IndexOf("{1}", second + 3, StringComparison.Ordinal) >= 0) continue;
                    string prefix = template.Substring(0, first), separator = template.Substring(first + 3, second - first - 3), suffix = template.Substring(second + 3);
                    if (separator.Length == 0 || !text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal)
                        || text.Length < prefix.Length + suffix.Length) continue;
                    string body = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
                    int boundary = body.IndexOf(separator, StringComparison.Ordinal);
                    if (boundary < 0) continue;
                    localized = Format(key, body.Substring(0, boundary), body.Substring(boundary + separator.Length));
                    return true;
                }
            }
            return false;
        }

        private static string LocalizeModelState(string value)
        {
            return value?.Trim() switch
            {
                "미설치" or "Not installed" => OpenVisionLanguageService.T("WpfShell.Status.ModelState.NotInstalled"),
                "설정 확인 필요" or "Configuration required" => OpenVisionLanguageService.T("WpfShell.Status.ModelState.ConfigurationRequired"),
                _ => LocalizationTextRuntimeService.Translate(value)
            };
        }

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T(key),
                arguments ?? Array.Empty<object>());
        }
    }
}
