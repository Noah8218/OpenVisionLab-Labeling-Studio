using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace MvcVisionSystem
{
    public class WorkspaceLayoutSettingsService
    {
        public const string SettingsPathEnvironmentVariable = "OPENVISIONLAB_LABELING_WORKSPACE_LAYOUT_PATH";
        private readonly string settingsPath;

        public bool HasValidSavedSettings { get; private set; }

        public WorkspaceLayoutSettingsService(string settingsPath = null)
        {
            this.settingsPath = ResolveSettingsPath(settingsPath);
        }

        public WpfWorkspaceLayoutSettings Load()
        {
            HasValidSavedSettings = false;
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return WpfWorkspaceLayoutSettings.CreateDefault();
                }

                WpfWorkspaceLayoutSettings settings = JsonConvert.DeserializeObject<WpfWorkspaceLayoutSettings>(
                    File.ReadAllText(settingsPath, Encoding.UTF8));
                HasValidSavedSettings = settings != null
                    && double.IsFinite(settings.WorkflowPaneWidth)
                    && settings.WorkflowPaneWidth > 0D
                    && double.IsFinite(settings.ImageQueuePaneWidth)
                    && settings.ImageQueuePaneWidth > 0D;
                return WpfWorkspaceLayoutSettings.Normalize(settings);
            }
            catch
            {
                HasValidSavedSettings = false;
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
                HasValidSavedSettings = true;
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

    [Obsolete("Use WorkspaceLayoutSettingsService.", false)]
    public sealed class WpfWorkspaceLayoutSettingsService : WorkspaceLayoutSettingsService
    {
        public WpfWorkspaceLayoutSettingsService(string settingsPath = null)
            : base(settingsPath)
        {
        }
    }

    public sealed class WpfWorkspaceLayoutSettings
    {
        public const double DefaultWorkflowPaneWidth = 300D;
        public const double DefaultImageQueuePaneWidth = 240D;
        public const double WideDefaultWorkflowPaneWidth = 330D;
        public const double WideDefaultImageQueuePaneWidth = 270D;
        public const double WideWorkspaceWidthThreshold = 1500D;
        public const double WideImageQueueWidthThreshold = 1500D;
        public const double MinimumWorkflowPaneWidth = 280D;
        public const double MinimumImageQueuePaneWidth = 180D;
        public const double MaximumPaneWidth = 640D;

        public double WorkflowPaneWidth { get; set; } = DefaultWorkflowPaneWidth;

        public double ImageQueuePaneWidth { get; set; } = DefaultImageQueuePaneWidth;

        public static WpfWorkspaceLayoutSettings CreateDefault()
            => CreateDefault(0D);

        public static WpfWorkspaceLayoutSettings CreateDefault(double workspaceWidth)
            => new WpfWorkspaceLayoutSettings
            {
                WorkflowPaneWidth = workspaceWidth > WideWorkspaceWidthThreshold ? WideDefaultWorkflowPaneWidth : DefaultWorkflowPaneWidth,
                ImageQueuePaneWidth = workspaceWidth > WideImageQueueWidthThreshold ? WideDefaultImageQueuePaneWidth : DefaultImageQueuePaneWidth
            };

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
