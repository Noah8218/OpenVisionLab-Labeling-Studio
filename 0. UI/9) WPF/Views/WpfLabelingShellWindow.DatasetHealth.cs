using System;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private WpfDatasetHealthWindow datasetHealthWindow;

        private void ExecuteOpenDatasetHealthCommand()
        {
            if (datasetHealthWindow == null)
            {
                datasetHealthWindow = new WpfDatasetHealthWindow(new WpfDatasetHealthViewModel(global.Data))
                {
                    Owner = this
                };
                datasetHealthWindow.Closed += DatasetHealthWindow_Closed;
                datasetHealthWindow.ApplyThemeFrom(this);
                datasetHealthWindow.Show();
            }
            else
            {
                datasetHealthWindow.ViewModel?.Refresh(global.Data);
                datasetHealthWindow.ApplyThemeFrom(this);
                if (datasetHealthWindow.WindowState == WindowState.Minimized)
                {
                    datasetHealthWindow.WindowState = WindowState.Normal;
                }
            }

            datasetHealthWindow.Activate();
        }

        private void DatasetHealthWindow_Closed(object sender, EventArgs e)
        {
            if (datasetHealthWindow != null)
            {
                datasetHealthWindow.Closed -= DatasetHealthWindow_Closed;
                datasetHealthWindow = null;
            }
        }

        private void CloseDatasetHealthWindow()
        {
            datasetHealthWindow?.Close();
        }

        private void RefreshDatasetHealthWindowTheme()
        {
            datasetHealthWindow?.ApplyThemeFrom(this);
        }
    }
}
