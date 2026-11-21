using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    /// <summary>
    /// Owns host-dependent model path discovery and applies the results to
    /// persisted settings at explicit runtime and persistence composition points.
    /// </summary>
    public static class PythonModelRuntimePathResolver
    {
        private const string ProjectRootPathDefault = @"C:\Git\yolov5";
        private const string BundledTrainImageRootPathDefault = @"C:\Git\yolov5\data\train\images";
        private const string BundledValidImageRootPathDefault = @"C:\Git\yolov5\data\valid\images";
        private const string LegacyTrainedImageRootPathDefault = @"C:\Git\py\data\train\images";
        private const string LegacyImageRootPathDefault = @"C:\Git\py\KtemData";
        private const string RetiredProjectRootPath = @"C:\Git\새 폴더\yolov5";
        private const string RetiredImageRootPath = @"C:\Git\새 폴더\py\KtemData";
        private const string SmokeImageRootEnvironmentVariable = "LABELING_SMOKE_IMAGE_ROOT";

        public static void ApplyDefaults(LabelingProjectSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.EnsureDefaults();
            PythonModelSettings modelSettings = settings.PythonModel;
            ApplyDefaults(modelSettings);

            string canonicalImageRootPath = settings.ImageRootPath?.Trim() ?? string.Empty;
            string resolvedDefaultImageRootPath = GetDefaultImageRootPath();
            if (string.IsNullOrWhiteSpace(canonicalImageRootPath)
                && !string.IsNullOrWhiteSpace(modelSettings.ImageRootPath)
                && !string.Equals(
                    modelSettings.ImageRootPath,
                    resolvedDefaultImageRootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.ImageRootPath = modelSettings.ImageRootPath;
            }
            else if (!string.IsNullOrWhiteSpace(canonicalImageRootPath))
            {
                settings.ImageRootPath = canonicalImageRootPath;
            }
        }

        public static void ApplyDefaults(PythonModelSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            ApplyPathDefaults(settings);
            settings.EnsureDefaults();
        }

        internal static void ApplyPathDefaults(PythonModelSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            MigrateRetiredDefaults(settings);
            settings.ModelEngine = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine);
            if (settings.ModelEngine != PythonModelSettings.EnginePatchCore)
            {
                RepairPortableYoloPaths(settings);
            }

            if (string.IsNullOrWhiteSpace(settings.ProjectRootPath))
            {
                settings.ProjectRootPath = settings.ModelEngine == PythonModelSettings.EnginePatchCore
                    ? GetDefaultPatchCoreProjectRootPath()
                    : GetDefaultProjectRootPath();
            }

            if (string.IsNullOrWhiteSpace(settings.ClientScriptPath))
            {
                settings.ClientScriptPath = settings.ModelEngine == PythonModelSettings.EngineUnet
                    ? PythonModelRuntimeBundledWorkerService.ResolveUnetWorkerScriptPath()
                    : settings.ModelEngine == PythonModelSettings.EnginePatchCore
                        ? PythonModelRuntimeBundledWorkerService.ResolvePatchCoreWorkerScriptPath()
                        : IsUltralyticsEngine(settings.ModelEngine)
                            ? PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath()
                            : Path.Combine(settings.ProjectRootPath, "labelling_tcp_client.py");
            }

            if (string.IsNullOrWhiteSpace(settings.WeightsPath))
            {
                settings.WeightsPath = settings.ModelEngine == PythonModelSettings.EngineUnet
                    ? GetDefaultUnetWeightsPath(settings.ProjectRootPath)
                    : settings.ModelEngine == PythonModelSettings.EnginePatchCore
                        ? GetDefaultPatchCoreWeightsPath(settings.ProjectRootPath)
                        : Path.Combine(settings.ProjectRootPath, "best.pt");
            }

            if (string.IsNullOrWhiteSpace(settings.ImageRootPath))
            {
                settings.ImageRootPath = GetDefaultImageRootPath();
            }
        }

        // YOLOv8 and YOLO11 run in the same Ultralytics worker, so one connected runtime serves both.
        public static bool IsUltralyticsEngine(string modelEngine)
        {
            string engine = PythonModelSettings.NormalizeModelEngine(modelEngine);
            return engine == PythonModelSettings.EngineYoloV8 || engine == PythonModelSettings.EngineYolo11;
        }

        // A new recipe on a PC that already has a working runtime starts from that runtime instead of
        // placeholder paths. Only the connection fields are copied; the model file stays per recipe.
        // Engines must match, or both be Ultralytics engines.
        public static bool TryInheritRuntime(PythonModelSettings target, PythonModelSettings source)
        {
            if (target == null || source == null || string.IsNullOrWhiteSpace(source.PythonExecutablePath))
            {
                return false;
            }

            string from = PythonModelSettings.NormalizeModelEngine(source.ModelEngine);
            string to = PythonModelSettings.NormalizeModelEngine(target.ModelEngine);
            if (from != to && !(IsUltralyticsEngine(from) && IsUltralyticsEngine(to)))
            {
                return false;
            }

            target.PythonExecutablePath = source.PythonExecutablePath;
            target.ProjectRootPath = source.ProjectRootPath ?? string.Empty;
            target.ClientScriptPath = source.ClientScriptPath ?? string.Empty;
            target.AutoStartClient = source.AutoStartClient;
            return true;
        }

        public static string GetDefaultProjectRootPath()
        {
            string siblingRoot = ResolveSiblingPath("yolov5");
            if (Directory.Exists(siblingRoot))
            {
                return siblingRoot;
            }

            return Directory.Exists(ProjectRootPathDefault) ? ProjectRootPathDefault : siblingRoot;
        }

        public static string GetDefaultUnetProjectRootPath()
        {
            string siblingRoot = ResolveSiblingPath("unet");
            return Directory.Exists(siblingRoot) ? siblingRoot : Path.Combine(@"C:\Git", "unet");
        }

        public static string GetDefaultUnetWeightsPath(string projectRootPath = "")
        {
            string root = string.IsNullOrWhiteSpace(projectRootPath)
                ? GetDefaultUnetProjectRootPath()
                : projectRootPath.Trim();
            return Path.Combine(root, "runs", "segment", "openvisionlab-unet-segmentation", "weights", "best.pt");
        }

        public static string GetDefaultPatchCoreProjectRootPath()
        {
            string dataDriveRoot = @"D:\OpenVisionLab_Runtime\PatchCore";
            return Directory.Exists(@"D:\") ? dataDriveRoot : Path.Combine(AppContext.BaseDirectory, "PatchCoreRuntime");
        }

        public static string GetDefaultPatchCoreWeightsPath(string projectRootPath = "")
        {
            string root = string.IsNullOrWhiteSpace(projectRootPath)
                ? GetDefaultPatchCoreProjectRootPath()
                : projectRootPath.Trim();
            return Path.Combine(root, "runs", "anomaly", "openvisionlab-patchcore", "weights", "best.pt");
        }

        public static string GetDefaultImageRootPath()
        {
            string smokeImageRootPath = Environment.GetEnvironmentVariable(SmokeImageRootEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(smokeImageRootPath)
                && Directory.Exists(smokeImageRootPath))
            {
                return Path.GetFullPath(smokeImageRootPath);
            }

            string projectRootPath = GetDefaultProjectRootPath();
            string siblingPyRoot = ResolveSiblingPath("py");
            foreach (string candidate in new[]
            {
                Path.Combine(projectRootPath, "data", "train", "images"),
                Path.Combine(projectRootPath, "data", "valid", "images"),
                Path.Combine(projectRootPath, "data", "images"),
                Path.Combine(projectRootPath, "yolov5Master", "data", "images"),
                BundledTrainImageRootPathDefault,
                BundledValidImageRootPathDefault,
                Path.Combine(siblingPyRoot, "data", "train", "images"),
                Path.Combine(siblingPyRoot, "KtemData"),
                LegacyTrainedImageRootPathDefault,
                LegacyImageRootPathDefault
            })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "";
        }

        internal static IReadOnlyList<string> EnumerateRuntimeSearchRoots()
        {
            return new[]
            {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory,
                FindRepositoryRoot(AppContext.BaseDirectory),
                FindRepositoryRoot(Environment.CurrentDirectory)
            }
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        internal static string FindRepositoryRoot(string startPath)
        {
            string current;
            try
            {
                current = string.IsNullOrWhiteSpace(startPath)
                    ? string.Empty
                    : Path.GetFullPath(startPath);
            }
            catch
            {
                return string.Empty;
            }

            if (File.Exists(current))
            {
                current = Path.GetDirectoryName(current) ?? string.Empty;
            }

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.sln"))
                    || File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.csproj"))
                    || File.Exists(Path.Combine(current, "MvcVisionSystem.sln"))
                    || File.Exists(Path.Combine(current, "MvcVisionSystem.csproj")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName ?? string.Empty;
            }

            return string.Empty;
        }

        private static void RepairPortableYoloPaths(PythonModelSettings settings)
        {
            string defaultProjectRootPath = GetDefaultProjectRootPath();
            bool repairedProjectRoot = false;
            if (string.IsNullOrWhiteSpace(settings.ProjectRootPath)
                && IsUsableYoloProjectRoot(defaultProjectRootPath))
            {
                settings.ProjectRootPath = defaultProjectRootPath;
                repairedProjectRoot = true;
            }

            string preferredScriptPath = ResolvePreferredClientScriptPath(settings.ProjectRootPath);
            if (!string.IsNullOrWhiteSpace(preferredScriptPath)
                && (repairedProjectRoot || string.IsNullOrWhiteSpace(settings.ClientScriptPath)))
            {
                settings.ClientScriptPath = preferredScriptPath;
            }

            string preferredWeightsPath = string.IsNullOrWhiteSpace(settings.ProjectRootPath)
                ? string.Empty
                : Path.Combine(settings.ProjectRootPath, "best.pt");
            if (!string.IsNullOrWhiteSpace(preferredWeightsPath)
                && File.Exists(preferredWeightsPath)
                && (repairedProjectRoot || string.IsNullOrWhiteSpace(settings.WeightsPath)))
            {
                settings.WeightsPath = preferredWeightsPath;
            }

            string preferredImageRootPath = GetDefaultImageRootPath();
            if (!string.IsNullOrWhiteSpace(preferredImageRootPath)
                && Directory.Exists(preferredImageRootPath)
                && (repairedProjectRoot || string.IsNullOrWhiteSpace(settings.ImageRootPath)))
            {
                settings.ImageRootPath = preferredImageRootPath;
            }
        }

        private static bool IsUsableYoloProjectRoot(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath) || !Directory.Exists(projectRootPath))
            {
                return false;
            }

            string clientScriptPath = ResolvePreferredClientScriptPath(projectRootPath);
            return File.Exists(clientScriptPath)
                && (File.Exists(Path.Combine(projectRootPath, "requirements.txt"))
                    || Directory.Exists(Path.Combine(projectRootPath, "yolov5Master")));
        }

        private static string ResolvePreferredClientScriptPath(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return string.Empty;
            }

            foreach (string fileName in new[] { "labelling_tcp_client.py", "labeling_tcp_client.py" })
            {
                string candidate = Path.Combine(projectRootPath, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(projectRootPath, "labelling_tcp_client.py");
        }

        private static string ResolveSiblingPath(string directoryName)
        {
            string repositoryRoot = FindLabelingRepositoryRoot();
            string parent = string.IsNullOrWhiteSpace(repositoryRoot)
                ? Directory.GetParent(AppContext.BaseDirectory)?.FullName
                : Directory.GetParent(repositoryRoot)?.FullName;

            return string.IsNullOrWhiteSpace(parent)
                ? Path.Combine(AppContext.BaseDirectory, directoryName)
                : Path.Combine(parent, directoryName);
        }

        private static string FindLabelingRepositoryRoot()
        {
            foreach (string startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                string repositoryRoot = FindRepositoryRoot(startPath);
                if (!string.IsNullOrWhiteSpace(repositoryRoot))
                {
                    return repositoryRoot;
                }
            }

            return string.Empty;
        }

        private static void MigrateRetiredDefaults(PythonModelSettings settings)
        {
            string defaultProjectRootPath = GetDefaultProjectRootPath();
            if (string.Equals(settings.ProjectRootPath, RetiredProjectRootPath, StringComparison.OrdinalIgnoreCase))
            {
                settings.ProjectRootPath = defaultProjectRootPath;
            }

            string retiredScriptPath = Path.Combine(RetiredProjectRootPath, "labelling_tcp_client.py");
            if (string.Equals(settings.ClientScriptPath, retiredScriptPath, StringComparison.OrdinalIgnoreCase))
            {
                settings.ClientScriptPath = Path.Combine(defaultProjectRootPath, "labelling_tcp_client.py");
            }

            string retiredWeightsPath = Path.Combine(RetiredProjectRootPath, "best.pt");
            if (string.Equals(settings.WeightsPath, retiredWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                settings.WeightsPath = Path.Combine(defaultProjectRootPath, "best.pt");
            }

            if (string.Equals(settings.ImageRootPath, RetiredImageRootPath, StringComparison.OrdinalIgnoreCase))
            {
                settings.ImageRootPath = GetDefaultImageRootPath();
            }
        }

    }
}
