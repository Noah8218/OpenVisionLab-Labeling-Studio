using System.Globalization;
using System.IO;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetContextPresentation
    {
        public WpfDatasetContextPresentation(
            string datasetName,
            string purposeText,
            string storagePathText,
            string imageRootText,
            string sourceText,
            string combinedPathText,
            string tooltip)
        {
            DatasetName = datasetName ?? string.Empty;
            PurposeText = purposeText ?? string.Empty;
            StoragePathText = storagePathText ?? string.Empty;
            ImageRootText = imageRootText ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
            CombinedPathText = combinedPathText ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
        }

        public string DatasetName { get; }

        public string PurposeText { get; }

        public string StoragePathText { get; }

        public string ImageRootText { get; }

        public string SourceText { get; }

        public string CombinedPathText { get; }

        public string Tooltip { get; }
    }

    public static class WpfDatasetContextPresentationService
    {
        public static string BuildDatasetName(string recipeName, string outputRootPath)
        {
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                return recipeName.Trim();
            }

            string outputRootName = GetPathLeafName(outputRootPath);
            return string.IsNullOrWhiteSpace(outputRootName)
                ? "\uB370\uC774\uD130\uC14B \uBBF8\uC120\uD0DD"
                : outputRootName;
        }

        public static string FormatPurposeName(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "\uC138\uADF8\uBA58\uD14C\uC774\uC158",
                LabelingDatasetPurpose.AnomalyDetection => "\uC774\uC0C1 \uD0D0\uC9C0",
                _ => "\uAC1D\uCCB4 \uD0D0\uC9C0"
            };
        }

        public static WpfDatasetContextPresentation Build(
            string datasetName,
            string purposeText,
            string outputRootPath,
            string imageRootPath,
            int classCount = 0)
        {
            string normalizedName = string.IsNullOrWhiteSpace(datasetName)
                ? "\uB370\uC774\uD130\uC14B \uBBF8\uC120\uD0DD"
                : datasetName.Trim();
            string normalizedPurpose = string.IsNullOrWhiteSpace(purposeText)
                ? "\uBAA9\uC801 \uBBF8\uC120\uD0DD"
                : purposeText.Trim();
            string outputText = string.IsNullOrWhiteSpace(outputRootPath)
                ? "\uB370\uC774\uD130\uC14B\uC744 \uC5F4\uAC70\uB098 \uC0C8\uB85C \uB9CC\uB4DC\uC138\uC694"
                : ShortenPath(outputRootPath.Trim());
            string imageText = string.IsNullOrWhiteSpace(imageRootPath)
                ? "\uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694"
                : ShortenPath(imageRootPath.Trim());

            string storagePathText = $"\uB77C\uBCA8/\uB808\uC2DC\uD53C \uC800\uC7A5: {outputText}";
            string imageRootText = $"\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354: {imageText}";
            string classSourceText = classCount > 0
                ? string.Format(CultureInfo.InvariantCulture, "\uD074\uB798\uC2A4: \uB808\uC2DC\uD53C {0}\uAC1C", classCount)
                : "\uD074\uB798\uC2A4: \uB808\uC2DC\uD53C \uD655\uC778 \uD544\uC694";
            string labelSourceText = string.IsNullOrWhiteSpace(outputRootPath)
                ? "\uB77C\uBCA8: \uC800\uC7A5 \uD3F4\uB354 \uBBF8\uC124\uC815"
                : "\uB77C\uBCA8: \uC800\uC7A5 \uD3F4\uB354 \uAE30\uC900";
            string sourceText = $"{classSourceText} / {labelSourceText}";
            string combinedPathText = $"{storagePathText}  /  {imageRootText}  /  {sourceText}";
            string tooltip =
                $"\uB370\uC774\uD130\uC14B: {normalizedName}\n" +
                $"\uBAA9\uC801: {normalizedPurpose}\n" +
                $"\uB77C\uBCA8/\uB808\uC2DC\uD53C \uC800\uC7A5 \uD3F4\uB354: {outputRootPath}\n" +
                $"\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354: {imageRootPath}\n" +
                "\uC791\uC5C5 \uAE30\uC900: \uD074\uB798\uC2A4\uB294 Recipe, \uB77C\uBCA8\uC740 \uC800\uC7A5 \uD3F4\uB354 \uAE30\uC900\uC785\uB2C8\uB2E4.\n" +
                "\uD074\uB798\uC2A4 \uBAA9\uB85D: \uD604\uC7AC Recipe\uC5D0 \uC800\uC7A5\uB429\uB2C8\uB2E4.\n" +
                "\uB77C\uBCA8 \uD30C\uC77C: \uC800\uC7A5 \uD3F4\uB354(data/*/labels)\uC5D0\uC11C \uC77D\uACE0 \uC501\uB2C8\uB2E4.\n" +
                "\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uBCC0\uACBD: \uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uBAA9\uB85D\uB9CC \uBC14\uAFB8\uBA70 \uC800\uC7A5 \uD3F4\uB354\uC758 \uAE30\uC874 \uB77C\uBCA8\uC740 \uC720\uC9C0\uB429\uB2C8\uB2E4.";

            return new WpfDatasetContextPresentation(
                normalizedName,
                normalizedPurpose,
                storagePathText,
                imageRootText,
                sourceText,
                combinedPathText,
                tooltip);
        }

        private static string ShortenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length <= 54)
            {
                return path ?? string.Empty;
            }

            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fileName = Path.GetFileName(normalizedPath);
            string root = Path.GetPathRoot(path) ?? string.Empty;
            return string.IsNullOrWhiteSpace(fileName)
                ? path
                : $"{root}...\\{fileName}";
        }

        private static string GetPathLeafName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
