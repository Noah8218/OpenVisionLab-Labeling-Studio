using System;
using System.Globalization;
using OpenVisionLab;

namespace MvcVisionSystem
{
    public sealed class WpfStatusBarPanelViewModel : WpfObservableViewModel, IDisposable
    {
        private string datasetStatusText = "Dataset: waiting";
        private string workflowStageText = "단계: 준비";
        private string workflowProgressText = "진행: 이미지 없음";
        private string workflowNextActionText = "다음: 이미지 선택";
        private string pythonStatusText = OpenVisionLanguageService.T("WpfShell.Status.InferenceWaiting");
        private string inspectionModelStatusText = "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C";
        private string inspectionModelStatusToolTip = "\uD604\uC7AC \uCD94\uB860\uC5D0 \uC0AC\uC6A9\uD560 \uBAA8\uB378\uC744 \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string modelStatusText = "Model: waiting";
        private string modelStatusAutomationText = string.Empty;
        private bool disposed;
        private bool isAnnotationDirty;
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
            get => inspectionModelStatusToolTip;
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

        public void SetAnnotationSaveStatus(bool isDirty, string text, string toolTip)
        {
            IsAnnotationDirty = isDirty;
            AnnotationSaveStatusText = text;
            AnnotationSaveStatusToolTip = toolTip;
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
            PythonStatusText = text;
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
            ModelStatusText = LocalizeModelStatus(text);
        }

        public void SetModelStatusAutomationText(string text)
        {
            // Keep machine-readable diagnostics separate from visible status text so
            // fast tool switches do not hide the commit signal used by real EXE smoke.
            ModelStatusAutomationText = text;
        }

        public void RefreshLocalizedPresentation()
        {
            PythonStatusText = LocalizationTextRuntimeService.Translate(PythonStatusText);
            InspectionModelStatusText = LocalizeInspectionModelStatus(InspectionModelStatusText);
            InspectionModelStatusToolTip = LocalizationTextRuntimeService.Translate(InspectionModelStatusToolTip);
            ModelStatusText = LocalizeModelStatus(ModelStatusText);
            AnnotationSaveStatusText = LocalizationTextRuntimeService.Translate(AnnotationSaveStatusText);
            AnnotationSaveStatusToolTip = LocalizationTextRuntimeService.Translate(AnnotationSaveStatusToolTip);
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

        private static string LocalizeModelStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            string value = text.Trim();
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
