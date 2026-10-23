using Newtonsoft.Json;
using OpenVisionLab;
using System;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class RecipeDatasetVersionPresentation
    {
        public string VersionText { get; init; } = OpenVisionLanguageService.T("WpfDatasetVersion.Pending");

        public string DetailText { get; init; } = OpenVisionLanguageService.T("WpfDatasetVersion.PendingDetail");
    }

    public static class RecipeDatasetVersionPresentationService
    {
        public static RecipeDatasetVersionPresentation Build(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                return new RecipeDatasetVersionPresentation();
            }

            try
            {
                LabelingDatasetManifest manifest =
                    JsonConvert.DeserializeObject<LabelingDatasetManifest>(File.ReadAllText(manifestPath));
                if (manifest == null
                    || string.IsNullOrWhiteSpace(manifest.DatasetVersionId)
                    || string.IsNullOrWhiteSpace(manifest.ContentIdentity?.ContentSha256))
                {
                    return new RecipeDatasetVersionPresentation
                    {
                        VersionText = OpenVisionLanguageService.T("WpfDatasetVersion.Legacy"),
                        DetailText = OpenVisionLanguageService.T("WpfDatasetVersion.LegacyDetail")
                    };
                }

                string recipeDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                int historyCount = RecipeDatasetVersionService.LoadHistory(recipeDirectory).Count;
                return new RecipeDatasetVersionPresentation
                {
                    VersionText = manifest.DatasetVersionId,
                    DetailText = string.Format(OpenVisionLanguageService.T("WpfDatasetVersion.Detail"),
                        Shorten(manifest.ContentIdentity.ContentSha256), manifest.ContentIdentity.ImageFileCount,
                        manifest.ContentIdentity.AnnotationFileCount, historyCount)
                };
            }
            catch (IOException ex)
            {
                return BuildReadFailure(ex.Message);
            }
            catch (JsonException ex)
            {
                return BuildReadFailure(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return BuildReadFailure(ex.Message);
            }
        }

        public static RecipeDatasetVersionPresentation BuildCurrent(string manifestPath, LabelingProjectData data)
        {
            RecipeDatasetVersionPresentation recorded = Build(manifestPath);
            if (data?.ProjectSettings?.AdditionalImageRootPaths?.Count is not > 0) return recorded;
            bool hasRecordedVersion = recorded.VersionText.StartsWith("dsv2-", StringComparison.Ordinal);
            bool unavailable = data.ProjectSettings.ResolveImageRootPaths().Any(root => !Directory.Exists(root));
            string recordedVersion = hasRecordedVersion ? recorded.VersionText : OpenVisionLanguageService.T("WpfDatasetVersion.NoRecordedVersion");
            return new RecipeDatasetVersionPresentation
            {
                VersionText = OpenVisionLanguageService.T(hasRecordedVersion ? "WpfDatasetVersion.LastRecorded" : "WpfDatasetVersion.NoRecordedVersion"),
                DetailText = unavailable
                    ? string.Format(OpenVisionLanguageService.T("WpfDatasetVersion.LastRecordedUnavailable"), recordedVersion)
                    : string.Format(OpenVisionLanguageService.T("WpfDatasetVersion.LastRecordedDetail"), recordedVersion, hasRecordedVersion ? recorded.DetailText : string.Empty)
            };
        }

        private static RecipeDatasetVersionPresentation BuildReadFailure(string message)
            => new RecipeDatasetVersionPresentation
            {
                VersionText = OpenVisionLanguageService.T("WpfDatasetVersion.ReadFailed"),
                DetailText = message ?? string.Empty
            };

        private static string Shorten(string value)
            => string.IsNullOrWhiteSpace(value) || value.Length <= 16
                ? value ?? string.Empty
                : value.Substring(0, 16) + "…";
    }

    [Obsolete("Use RecipeDatasetVersionPresentationService.", false)]
    public static class WpfRecipeDatasetVersionPresentationService
    {
        public static WpfRecipeDatasetVersionPresentation Build(string manifestPath)
        {
            RecipeDatasetVersionPresentation presentation = RecipeDatasetVersionPresentationService.Build(manifestPath);
            return presentation == null
                ? null
                : new WpfRecipeDatasetVersionPresentation
                {
                    VersionText = presentation.VersionText,
                    DetailText = presentation.DetailText
                };
        }
    }

    [Obsolete("Use RecipeDatasetVersionPresentation.", false)]
    public sealed class WpfRecipeDatasetVersionPresentation : RecipeDatasetVersionPresentation
    {
    }
}
