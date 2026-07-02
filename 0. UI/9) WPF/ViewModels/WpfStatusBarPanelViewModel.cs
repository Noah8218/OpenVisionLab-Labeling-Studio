namespace MvcVisionSystem
{
    public sealed class WpfStatusBarPanelViewModel : WpfObservableViewModel
    {
        private string datasetStatusText = "Dataset: waiting";
        private string workflowStageText = "단계: 준비";
        private string workflowProgressText = "진행: 이미지 없음";
        private string workflowNextActionText = "다음: 이미지 선택";
        private string pythonStatusText = "\uCD94\uB860: \uC810\uAC80 \uC804";
        private string inspectionModelStatusText = "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C";
        private string inspectionModelStatusToolTip = "\uD604\uC7AC \uCD94\uB860\uC5D0 \uC0AC\uC6A9\uD560 \uBAA8\uB378\uC744 \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
        private string modelStatusText = "Model: waiting";
        private string modelStatusAutomationText = string.Empty;
        private bool isAnnotationDirty;
        private string annotationSaveStatusText = "\uB77C\uBCA8 \uB300\uAE30";
        private string annotationSaveStatusToolTip = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";

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
            InspectionModelStatusText = string.IsNullOrWhiteSpace(text)
                ? "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : text.Trim();
            InspectionModelStatusToolTip = string.IsNullOrWhiteSpace(toolTip)
                ? InspectionModelStatusText
                : toolTip.Trim();
        }

        public void SetModelStatus(string text)
        {
            ModelStatusText = text;
        }

        public void SetModelStatusAutomationText(string text)
        {
            // Keep machine-readable diagnostics separate from visible status text so
            // fast tool switches do not hide the commit signal used by real EXE smoke.
            ModelStatusAutomationText = text;
        }
    }
}
