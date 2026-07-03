using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetImageRootResolver
    {
        public string Resolve(CData data, string configuredRoot, Func<string, bool> hasQueueImages)
        {
            configuredRoot ??= string.Empty;
            if (Directory.Exists(configuredRoot) && !IsImplicitDefaultImageRoot(configuredRoot))
            {
                return configuredRoot;
            }

            foreach (string datasetImageRoot in EnumerateDatasetImageRoots(data))
            {
                if (hasQueueImages?.Invoke(datasetImageRoot) == true)
                {
                    return datasetImageRoot;
                }
            }

            if (Directory.Exists(configuredRoot))
            {
                return configuredRoot;
            }

            return EnumerateDatasetImageRoots(data).FirstOrDefault(Directory.Exists) ?? configuredRoot;
        }

        public IEnumerable<string> EnumerateDatasetImageRoots(CData data)
        {
            if (data == null)
            {
                yield break;
            }

            data.NormalizeOutputPaths();
            foreach (string imageRoot in new[]
            {
                data.TrainImagesPath,
                data.ValidImagesPath,
                data.TestImagesPath
            })
            {
                if (!string.IsNullOrWhiteSpace(imageRoot))
                {
                    yield return imageRoot;
                }
            }
        }

        public bool IsImplicitDefaultImageRoot(string imageRoot)
        {
            string defaultRoot = PythonModelSettings.GetDefaultImageRootPath();
            if (string.IsNullOrWhiteSpace(imageRoot) || string.IsNullOrWhiteSpace(defaultRoot))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(imageRoot),
                    Path.GetFullPath(defaultRoot),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) when (imageRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || defaultRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }
        }
    }
}
