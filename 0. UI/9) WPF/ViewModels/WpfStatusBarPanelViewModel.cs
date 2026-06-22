namespace MvcVisionSystem
{
    public sealed class WpfStatusBarPanelViewModel : WpfObservableViewModel
    {
        private string datasetStatusText = "Dataset: waiting";
        private string pythonStatusText = "Python: unchecked";
        private string modelStatusText = "Model: waiting";
        private bool isAnnotationDirty;
        private string annotationSaveStatusText = "\uB77C\uBCA8 \uB300\uAE30";
        private string annotationSaveStatusToolTip = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uBA74 \uB77C\uBCA8 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";

        public string ViewName => nameof(WpfStatusBarPanel);

        public string DatasetStatusText
        {
            get => datasetStatusText;
            private set => SetProperty(ref datasetStatusText, value ?? string.Empty);
        }

        public string PythonStatusText
        {
            get => pythonStatusText;
            private set => SetProperty(ref pythonStatusText, value ?? string.Empty);
        }

        public string ModelStatusText
        {
            get => modelStatusText;
            private set => SetProperty(ref modelStatusText, value ?? string.Empty);
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

        public void SetPythonStatus(string text)
        {
            PythonStatusText = text;
        }

        public void SetModelStatus(string text)
        {
            ModelStatusText = text;
        }
    }
}
