using Newtonsoft.Json;
using System;
using System.IO;

namespace MvcVisionSystem
{
    // User preferences for this application. Recipe-specific anomaly advance retains its existing owner.
    internal sealed class LabelingPreferencesService
    {
        internal const string PathEnvironmentVariable = "OPENVISIONLAB_LABELING_PREFERENCES_PATH";
        private readonly string path;

        internal LabelingPreferencesService(string path = null)
        {
            this.path = path ?? Environment.GetEnvironmentVariable(PathEnvironmentVariable)
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenVisionLab", "LabelingStudio", "labeling-preferences.json");
        }

        internal LabelingPreferences Load(out bool restoredDefaults)
        {
            restoredDefaults = false;
            try
            {
                if (!File.Exists(path)) return new LabelingPreferences();
                LabelingPreferences settings = JsonConvert.DeserializeObject<LabelingPreferences>(File.ReadAllText(path));
                if (settings?.SchemaVersion == 1) return settings;
            }
            catch (Exception) { }
            restoredDefaults = true;
            return new LabelingPreferences();
        }

        internal bool TrySave(LabelingPreferences settings)
        {
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(temporary, JsonConvert.SerializeObject(settings, Formatting.Indented));
                File.Move(temporary, path, overwrite: true);
                return true;
            }
            catch (Exception)
            {
                try { File.Delete(temporary); } catch (Exception) { }
                return false;
            }
        }
    }

    internal sealed class LabelingPreferences
    {
        public int SchemaVersion { get; set; } = 1;
        public bool AutoSaveOnImageMove { get; set; }
        public bool ShowLabelNames { get; set; } = true;
        public bool BoxCrosshair { get; set; } = true;
        public bool KeepZoomOnImageMove { get; set; }
    }
}
