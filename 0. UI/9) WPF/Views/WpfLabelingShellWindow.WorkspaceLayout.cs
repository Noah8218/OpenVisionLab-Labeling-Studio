using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void LeftWorkspaceSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ShellViewModel?.SetRightWorkflowExpandedPaneWidth(RightWorkflowColumn.ActualWidth);
            BindingOperations.SetBinding(
                RightWorkflowColumn,
                ColumnDefinition.WidthProperty,
                new System.Windows.Data.Binding("ShellViewModel.RightWorkflowPaneGridLength")
                {
                    Source = this,
                    Mode = BindingMode.OneWay
                });
            SaveWorkspaceLayoutSettings();
        }

        private void RightWorkspaceSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            SaveWorkspaceLayoutSettings();
        }

        private void RestoreWorkspaceLayoutSettings()
        {
            ApplyWorkspaceLayoutSettings(workspaceLayoutSettingsService.Load());
        }

        private void ExecuteResetWorkspaceLayoutCommand()
        {
            WpfWorkspaceLayoutSettings defaults = WpfWorkspaceLayoutSettings.CreateDefault();
            ApplyWorkspaceLayoutSettings(defaults);
            PersistWorkspaceLayoutSettings(defaults);
            AppendLog("\uD328\uB110 \uB108\uBE44 \uCD08\uAE30\uD654: \uC791\uC5C5 \uD328\uB110 340px / \uC774\uBBF8\uC9C0 \uD050 320px");
        }

        private void ApplyWorkspaceLayoutSettings(WpfWorkspaceLayoutSettings settings)
        {
            WpfWorkspaceLayoutSettings normalized = WpfWorkspaceLayoutSettings.Normalize(settings);
            ShellViewModel?.SetRightWorkflowExpandedPaneWidth(normalized.WorkflowPaneWidth);
            ImageQueueColumn.Width = new GridLength(normalized.ImageQueuePaneWidth);
        }

        private void SaveWorkspaceLayoutSettings()
        {
            double queueWidth = ImageQueueColumn.Width.IsAbsolute
                ? ImageQueueColumn.Width.Value
                : ImageQueueColumn.ActualWidth;
            var settings = new WpfWorkspaceLayoutSettings
            {
                WorkflowPaneWidth = ShellViewModel?.RightWorkflowExpandedPaneWidth
                    ?? WpfWorkspaceLayoutSettings.DefaultWorkflowPaneWidth,
                ImageQueuePaneWidth = queueWidth
            };
            PersistWorkspaceLayoutSettings(settings);
        }

        private void PersistWorkspaceLayoutSettings(WpfWorkspaceLayoutSettings settings)
        {
            if (!workspaceLayoutSettingsService.TrySave(settings, out string error))
            {
                AppendLog("\uD328\uB110 \uB108\uBE44 \uC800\uC7A5 \uC2E4\uD328: " + error);
            }
        }
    }
}
