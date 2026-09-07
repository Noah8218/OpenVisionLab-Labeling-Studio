using System;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace MvcVisionSystem
{
    public partial class WpfDatasetSetupWizardWindow : Window
    {
        public WpfDatasetSetupWizardWindow()
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterWindow(this);
        }

        public WpfDatasetSetupWizardViewModel ViewModel => DataContext as WpfDatasetSetupWizardViewModel;

        protected override void OnClosed(EventArgs e)
        {
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

        public WpfLearningModeItem SelectedDatasetPurpose => WizardDatasetPurposeListBox.SelectedItem as WpfLearningModeItem;

        public string RecipeNameText => WizardRecipeNameBox.Text ?? string.Empty;

        public string OutputRootPathText => WizardOutputRootPathBox.Text ?? string.Empty;

        public string ClassNamesText => WizardClassNamesBox.Text ?? string.Empty;

        public string ImageRootPathText => WizardImageRootPathBox.Text ?? string.Empty;

        public string WeightsPathText => WizardWeightsPathBox.Text ?? string.Empty;

        public void CommitTextBindings()
        {
            WizardRecipeNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            WizardOutputRootPathBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            WizardImageRootPathBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            WizardWeightsPathBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            // ClassNamesText already updates on PropertyChanged. Do not force
            // UpdateSource here: multiline TextBox automation can expose a
            // newer visible value while TextBox.Text is still catching up.
        }
    }
}
