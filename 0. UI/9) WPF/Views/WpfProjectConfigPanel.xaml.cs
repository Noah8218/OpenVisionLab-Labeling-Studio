using System.Windows;
using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfProjectConfigPanel : UserControl
    {
        public WpfProjectConfigPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfProjectConfigPanelViewModel ViewModel { get; } = new WpfProjectConfigPanelViewModel();

        public event RoutedEventHandler SaveRequested;
        public event RoutedEventHandler OpenFolderRequested;
        public event RoutedEventHandler ApplyRecipeRequested;
        public event SelectionChangedEventHandler RecipeSelectionChanged;
        public event RoutedEventHandler RefreshRecipeListRequested;

        public Expander SettingsExpander => ProjectConfigExpander;
        public TextBox RecipeNameBox => ProjectRecipeNameBox;
        public ComboBox RecipeListBox => ProjectRecipeListBox;
        public TextBox ConfigPathBox => ProjectConfigPathBox;
        public TextBlock StatusTextBlock => ProjectConfigStatusText;
        public WpfUiButton ApplyRecipeButton => ApplyProjectRecipeButton;
        public WpfUiButton RefreshRecipeListButton => RefreshProjectRecipeListButton;
        public WpfUiButton SaveButton => SaveProjectConfigButton;
        public WpfUiButton OpenFolderButton => OpenProjectConfigFolderButton;

        private void ApplyProjectRecipeButton_Click(object sender, RoutedEventArgs e) => ApplyRecipeRequested?.Invoke(sender, e);
        private void ProjectRecipeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RecipeSelectionChanged?.Invoke(sender, e);
        private void RefreshProjectRecipeListButton_Click(object sender, RoutedEventArgs e) => RefreshRecipeListRequested?.Invoke(sender, e);
        private void SaveProjectConfigButton_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(sender, e);
        private void OpenProjectConfigFolderButton_Click(object sender, RoutedEventArgs e) => OpenFolderRequested?.Invoke(sender, e);
    }
}
