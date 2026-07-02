using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfDatasetSelectionWindow : Window
    {
        public WpfDatasetSelectionWindow()
        {
            InitializeComponent();
        }

        public WpfDatasetSelectionWindowViewModel ViewModel => DataContext as WpfDatasetSelectionWindowViewModel;
    }
}
