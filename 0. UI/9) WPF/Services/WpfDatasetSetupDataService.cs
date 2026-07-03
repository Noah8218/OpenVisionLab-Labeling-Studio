using System;
using System.Collections.Generic;
using System.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSetupDataService
    {
        public string ApplyOutputRootAndClasses(CData data, string outputRootPath, IEnumerable<string> classNames)
        {
            if (data == null)
            {
                return string.Empty;
            }

            IReadOnlyList<string> normalizedClassNames = NormalizeClassNames(classNames);
            data.ConfigureOutputRoot(outputRootPath);
            data.ClassNamedList.Clear();

            foreach (string className in normalizedClassNames)
            {
                if (!ClassCatalogService.TryAddClass(data, className, out _)
                    && !data.ClassNamedList.Any(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase)))
                {
                    data.ClassNamedList.Add(new CClassItem
                    {
                        Text = className,
                        DrawColor = System.Drawing.Color.FromArgb(34, 197, 94)
                    });
                }
            }

            data.EnsureYoloOutputDirectories();
            return normalizedClassNames.FirstOrDefault() ?? "Defect";
        }

        public IReadOnlyList<string> NormalizeClassNames(IEnumerable<string> classNames)
        {
            List<string> normalized = (classNames ?? Array.Empty<string>())
                .Select(ClassCatalogService.NormalizeClassName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return normalized.Count == 0 ? new[] { "Defect" } : normalized;
        }
    }
}
