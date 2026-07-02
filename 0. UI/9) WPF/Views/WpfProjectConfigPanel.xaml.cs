using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfProjectConfigPanel : UserControl
    {
        public WpfProjectConfigPanel()
        {
            InitializeComponent();
        }

        public WpfProjectConfigPanelViewModel ViewModel => DataContext as WpfProjectConfigPanelViewModel;

        public Expander SettingsExpander => ProjectConfigExpander;
        public TextBox RecipeNameBox => ProjectRecipeNameBox;
        public ComboBox RecipeListBox => ProjectRecipeListBox;
        public TextBox ConfigPathBox => ProjectConfigPathBox;
        public TextBox ManifestPathBox => ProjectManifestPathBox;
        public TextBlock StatusTextBlock => ProjectConfigStatusText;
        public WpfUiButton ApplyRecipeButton => ApplyProjectRecipeButton;
        public WpfUiButton RefreshRecipeListButton => RefreshProjectRecipeListButton;
        public WpfUiButton SaveButton => SaveProjectConfigButton;
        public WpfUiButton OpenFolderButton => OpenProjectConfigFolderButton;
    }
}
