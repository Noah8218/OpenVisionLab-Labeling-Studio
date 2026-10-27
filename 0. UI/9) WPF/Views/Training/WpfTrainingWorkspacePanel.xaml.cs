using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public partial class WpfTrainingWorkspacePanel : UserControl
    {
        public static readonly DependencyProperty SettingsContentProperty = DependencyProperty.Register(nameof(SettingsContent), typeof(WpfYoloModelSettingsPanel), typeof(WpfTrainingWorkspacePanel), new PropertyMetadata(null, OnSettingsContentChanged));

        public WpfTrainingWorkspacePanel() => InitializeComponent();
        public WpfYoloModelSettingsPanel SettingsContent { get => (WpfYoloModelSettingsPanel)GetValue(SettingsContentProperty); set => SetValue(SettingsContentProperty, value); }

        private void OnStartCaptionTargetUpdated(object sender, DataTransferEventArgs args)
        {
            if (sender is not TextBlock caption || args.Property != TextBlock.TextProperty || !ReferenceEquals(args.TargetObject, caption)) return;
            // Recompute the Auto column after the localized caption changes its measured width.
            DependencyObject container = TrainingStartButton.Parent;
            for (DependencyObject current = caption; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement element) element.InvalidateMeasure();
                if (ReferenceEquals(current, container)) break;
            }
        }

        private static void OnSettingsContentChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
        {
            if (args.OldValue is WpfYoloModelSettingsPanel previous) BindingOperations.ClearBinding(previous, WpfYoloModelSettingsPanel.TrainingWorkspaceProperty);
            if (args.NewValue is WpfYoloModelSettingsPanel current)
                BindingOperations.SetBinding(current, WpfYoloModelSettingsPanel.TrainingWorkspaceProperty, new Binding(nameof(DataContext)) { Source = owner, Mode = BindingMode.OneWay });
        }
    }
}
