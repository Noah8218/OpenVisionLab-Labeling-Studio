using System.Windows.Controls;

namespace MvcVisionSystem
{
    public partial class WpfStatusBarPanel : UserControl
    {
        public WpfStatusBarPanel()
        {
            InitializeComponent();
        }

        public WpfStatusBarPanelViewModel ViewModel => DataContext as WpfStatusBarPanelViewModel;

        public TextBlock DatasetStatusTextBlock => DatasetStatusText;
        public TextBlock PythonStatusTextBlock => PythonStatusText;
        public TextBlock AnnotationSaveStatusTextBlock => AnnotationSaveStatusText;
        public TextBlock ModelStatusTextBlock => ModelStatusText;
    }
}
