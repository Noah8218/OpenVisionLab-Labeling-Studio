using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace MvcVisionSystem
{
    public partial class WpfShellLogPanel : UserControl
    {
        public WpfShellLogPanel()
        {
            InitializeComponent();
        }

        public WpfShellLogPanelViewModel ViewModel => DataContext as WpfShellLogPanelViewModel;

        public FrameworkElement LogPanel => ShellLogPanel;
    }
}
