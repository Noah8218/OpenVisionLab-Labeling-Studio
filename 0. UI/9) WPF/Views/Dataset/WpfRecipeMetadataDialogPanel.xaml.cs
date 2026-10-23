using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfRecipeMetadataDialogPanel : UserControl
    {
        public WpfRecipeMetadataDialogPanel()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    if (IsVisible)
                    {
                        RecipeMetadataProductCodeTextBox.Focus();
                        Keyboard.Focus(RecipeMetadataProductCodeTextBox);
                    }
                }));
            }
        }
    }
}
