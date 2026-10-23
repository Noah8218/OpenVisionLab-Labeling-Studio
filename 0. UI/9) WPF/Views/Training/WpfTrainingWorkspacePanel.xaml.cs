using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public partial class WpfTrainingWorkspacePanel : UserControl
    {
        public static readonly DependencyProperty SettingsContentProperty = DependencyProperty.Register(nameof(SettingsContent), typeof(WpfYoloModelSettingsPanel), typeof(WpfTrainingWorkspacePanel), new PropertyMetadata(null, OnSettingsContentChanged));

        public WpfTrainingWorkspacePanel() => InitializeComponent();
        public WpfYoloModelSettingsPanel SettingsContent { get => (WpfYoloModelSettingsPanel)GetValue(SettingsContentProperty); set => SetValue(SettingsContentProperty, value); }

        private static void OnSettingsContentChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
        {
            if (args.OldValue is WpfYoloModelSettingsPanel previous) BindingOperations.ClearBinding(previous, WpfYoloModelSettingsPanel.TrainingWorkspaceProperty);
            if (args.NewValue is WpfYoloModelSettingsPanel current)
                BindingOperations.SetBinding(current, WpfYoloModelSettingsPanel.TrainingWorkspaceProperty, new Binding(nameof(DataContext)) { Source = owner, Mode = BindingMode.OneWay });
        }
    }
}
