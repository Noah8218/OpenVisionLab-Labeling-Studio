using System.Drawing;
using System.Globalization;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class WpfImageLoadPresentationService
    {
        // Image-load wording is separated from decode/canvas work so the hot load path stays easy to profile.
        public string BuildStartupSampleMissingDatasetStatus()
        {
            return "\uB370\uC774\uD130\uC14B: \uC0D8\uD50C \uC774\uBBF8\uC9C0 \uC5C6\uC74C";
        }

        public string BuildStartupSampleMissingLog()
        {
            return "\uC0D8\uD50C \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. Python \uBAA8\uB378 \uC774\uBBF8\uC9C0 \uB8E8\uD2B8\uB97C \uD655\uC778\uD558\uC138\uC694.";
        }

        public string BuildMissingImageLog(string imagePath)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC774\uBBF8\uC9C0 \uC5C6\uC74C: {0}", imagePath);
        }

        public string BuildLoadedDatasetStatus(string imagePath, Size imageSize)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uB370\uC774\uD130\uC14B: {0}  {1}x{2}",
                Path.GetFileName(imagePath),
                imageSize.Width,
                imageSize.Height);
        }

        public string BuildModelStatus(string weightsPath)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uBAA8\uB378: {0}", Path.GetFileName(weightsPath ?? string.Empty));
        }

        public string BuildAnnotationLoadedStatus()
        {
            return "\uC774\uBBF8\uC9C0 \uB85C\uB4DC: \uD604\uC7AC \uB77C\uBCA8\uC740 \uC800\uC7A5\uB41C \uC0C1\uD0DC\uB85C \uC2DC\uC791\uD569\uB2C8\uB2E4.";
        }

        public string BuildLoadLog(string imagePath)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC774\uBBF8\uC9C0 \uB85C\uB4DC: {0}", imagePath);
        }

        public string BuildLoadFailureDatasetStatus()
        {
            return "\uB370\uC774\uD130\uC14B: \uC774\uBBF8\uC9C0 \uB85C\uB4DC \uC2E4\uD328";
        }

        public string BuildLoadFailureLog(string message)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC774\uBBF8\uC9C0 \uB85C\uB4DC \uC2E4\uD328: {0}", message);
        }
    }
}
