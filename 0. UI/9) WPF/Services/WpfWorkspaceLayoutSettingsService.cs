using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace MvcVisionSystem
{
    public sealed class WpfWorkspaceLayoutSettingsService
    {
        public const string SettingsPathEnvironmentVariable = "OPENVISIONLAB_LABELING_WORKSPACE_LAYOUT_PATH";
        private readonly string settingsPath;

        public WpfWorkspaceLayoutSettingsService(string settingsPath = null)
        {
            this.settingsPath = ResolveSettingsPath(settingsPath);
        }

        public WpfWorkspaceLayoutSettings Load()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return WpfWorkspaceLayoutSettings.CreateDefault();
                }

                WpfWorkspaceLayoutSettings settings = JsonConvert.DeserializeObject<WpfWorkspaceLayoutSettings>(
                    File.ReadAllText(settingsPath, Encoding.UTF8));
                return WpfWorkspaceLayoutSettings.Normalize(settings);
            }
            catch
            {
                return WpfWorkspaceLayoutSettings.CreateDefault();
            }
        }

        public bool TrySave(WpfWorkspaceLayoutSettings settings, out string error)
        {
            try
            {
                string directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(
                    WpfWorkspaceLayoutSettings.Normalize(settings),
                    Formatting.Indented);
                File.WriteAllText(settingsPath, json, new UTF8Encoding(false));
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string ResolveSettingsPath(string overridePath)
        {
            string requestedPath = !string.IsNullOrWhiteSpace(overridePath)
                ? overridePath
                : Environment.GetEnvironmentVariable(SettingsPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(requestedPath))
            {
                return Path.GetFullPath(requestedPath);
            }

            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApplicationData, "OpenVisionLab", "LabelingStudio", "workspace-layout.json");
        }
    }

    public sealed class WpfWorkspaceLayoutSettings
    {
        public const double DefaultWorkflowPaneWidth = 340D;
        public const double DefaultImageQueuePaneWidth = 320D;
        public const double MinimumWorkflowPaneWidth = 280D;
        public const double MinimumImageQueuePaneWidth = 260D;
        public const double MaximumPaneWidth = 640D;

        public double WorkflowPaneWidth { get; set; } = DefaultWorkflowPaneWidth;

        public double ImageQueuePaneWidth { get; set; } = DefaultImageQueuePaneWidth;

        public static WpfWorkspaceLayoutSettings CreateDefault()
            => new WpfWorkspaceLayoutSettings();

        public static WpfWorkspaceLayoutSettings Normalize(WpfWorkspaceLayoutSettings settings)
        {
            settings ??= CreateDefault();
            return new WpfWorkspaceLayoutSettings
            {
                WorkflowPaneWidth = NormalizeWidth(
                    settings.WorkflowPaneWidth,
                    MinimumWorkflowPaneWidth,
                    DefaultWorkflowPaneWidth),
                ImageQueuePaneWidth = NormalizeWidth(
                    settings.ImageQueuePaneWidth,
                    MinimumImageQueuePaneWidth,
                    DefaultImageQueuePaneWidth)
            };
        }

        private static double NormalizeWidth(double width, double minimum, double fallback)
        {
            if (!double.IsFinite(width) || width <= 0D)
            {
                return fallback;
            }

            return Math.Clamp(width, minimum, MaximumPaneWidth);
        }
    }
}
