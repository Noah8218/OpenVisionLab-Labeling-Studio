using System.IO;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSetupPresentationService
    {
        public string BuildInvalidRecipeNameMessage()
            => "Recipe \uC774\uB984\uC5D0 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4.";

        public string BuildDuplicateOutputRootMessage(string existingRecipeName)
        {
            string displayName = string.IsNullOrWhiteSpace(existingRecipeName)
                ? "\uAE30\uC874 \uB370\uC774\uD130\uC14B"
                : existingRecipeName.Trim();
            return $"\uB370\uC774\uD130\uC14B \uC800\uC7A5 \uACBD\uB85C\uAC00 \uC774\uBBF8 '{displayName}'\uC5D0\uC11C \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4. \uC0C8 \uB370\uC774\uD130\uC14B\uC740 \uB2E4\uB978 \uBE48 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
        }

        public string BuildSamplePresetFailureMessage(string sampleError)
            => string.IsNullOrWhiteSpace(sampleError)
                ? "\uC0D8\uD50C \uB370\uC774\uD130\uC14B\uC744 \uC900\uBE44\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4."
                : sampleError.Trim();

        public string BuildReadyStatus(
            string recipeName,
            LabelingDatasetPurpose purpose,
            string manifestPath,
            WpfDatasetSamplePresetApplyResult sampleResult)
        {
            string sampleStatus = sampleResult?.Applied == true && !string.IsNullOrWhiteSpace(sampleResult.SummaryText)
                ? $" / {sampleResult.SummaryText.Trim()}"
                : string.Empty;
            return $"\uC900\uBE44 \uC644\uB8CC: {recipeName} / {purpose} / {Path.GetFileName(manifestPath)}{sampleStatus}";
        }

        public string BuildDatasetReadyStatus(string outputRootPath)
            => $"\uB370\uC774\uD130\uC14B: {Path.GetFileName(outputRootPath)} \uC900\uBE44 \uC644\uB8CC";

        public string BuildMissingImageRootStatus()
            => "\uB370\uC774\uD130\uC14B: \uC774\uBBF8\uC9C0 \uD3F4\uB354 \uD655\uC778 \uD544\uC694";

        public string BuildMissingImageRootLog(string imageRootPath)
            => $"\uB370\uC774\uD130\uC14B \uC804\uD658: \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. root={imageRootPath}";

        public string BuildMissingOutputRootStatus()
            => "\uB370\uC774\uD130\uC14B: \uC800\uC7A5 \uACBD\uB85C \uBBF8\uC124\uC815";

        public string BuildMissingOutputRootLog()
            => "\uC5F4 \uB370\uC774\uD130\uC14B \uD3F4\uB354\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uB370\uC774\uD130\uC14B\uC744 \uB9CC\uB4E4\uAC70\uB098 \uC800\uC7A5 \uACBD\uB85C\uB97C \uC120\uD0DD\uD558\uC138\uC694.";

        public string BuildOpenDatasetFolderFailedStatus()
            => "\uB370\uC774\uD130\uC14B: \uD3F4\uB354 \uC5F4\uAE30 \uC2E4\uD328";

        public string BuildOpenDatasetFolderFailedLog(string errorMessage)
            => $"\uB370\uC774\uD130\uC14B \uD3F4\uB354 \uC5F4\uAE30 \uC2E4\uD328: {errorMessage}";

        public string BuildCreationLog(
            string recipeName,
            LabelingDatasetPurpose purpose,
            string outputRootPath,
            string manifestPath)
            => $"\uB370\uC774\uD130\uC14B \uC0DD\uC131 \uC644\uB8CC: recipe={recipeName}, purpose={purpose}, output={outputRootPath}, manifest={manifestPath}";
    }
}
