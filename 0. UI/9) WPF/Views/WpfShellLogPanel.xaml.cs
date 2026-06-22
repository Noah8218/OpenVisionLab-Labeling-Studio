using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    public partial class WpfShellLogPanel : UserControl
    {
        public WpfShellLogPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfShellLogPanelViewModel ViewModel { get; } = new WpfShellLogPanelViewModel();

        public FrameworkElement LogPanel => ShellLogPanel;
    }
}
