using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class DatasetImageRootResolver
    {
        public string Resolve(LabelingProjectData data, string configuredRoot, Func<string, bool> hasQueueImages)
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

        public IEnumerable<string> EnumerateDatasetImageRoots(LabelingProjectData data)
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
            string defaultRoot = _1._Core.PythonModelRuntimePathResolver.GetDefaultImageRootPath();
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

    [Obsolete("Use DatasetImageRootResolver.", false)]
    public sealed class WpfDatasetImageRootResolver
    {
        private readonly DatasetImageRootResolver inner = new DatasetImageRootResolver();

        public string Resolve(LabelingProjectData data, string configuredRoot, Func<string, bool> hasQueueImages)
            => inner.Resolve(data, configuredRoot, hasQueueImages);

        public IEnumerable<string> EnumerateDatasetImageRoots(LabelingProjectData data)
            => inner.EnumerateDatasetImageRoots(data);

        public bool IsImplicitDefaultImageRoot(string imageRoot)
            => inner.IsImplicitDefaultImageRoot(imageRoot);
    }
}
