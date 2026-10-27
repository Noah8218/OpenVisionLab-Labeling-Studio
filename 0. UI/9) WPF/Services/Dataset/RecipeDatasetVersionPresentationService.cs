using Newtonsoft.Json;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class RecipeDatasetVersionPresentation
    {
        private string displayText;

        public string VersionText { get; init; } = OpenVisionLanguageService.T("WpfDatasetVersion.Pending");
        public string CapturedUtc { get; init; } = string.Empty;
        public int? ImageCount { get; init; }
        public bool IsCurrentInput { get; init; }
        public string DisplayText
        {
            get => ImageCount.HasValue
                ? IsCurrentInput ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfDatasetVersion.CurrentInput"), ImageCount.Value)
                    : RecipeDatasetVersionPresentationService.FormatLabel(CapturedUtc, ImageCount.Value)
                : displayText ?? OpenVisionLanguageService.T("WpfDatasetVersion.Pending");
            init => displayText = value;
        }

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
                        DisplayText = OpenVisionLanguageService.T("WpfDatasetVersion.Legacy"),
                        DetailText = OpenVisionLanguageService.T("WpfDatasetVersion.LegacyDetail")
                    };
                }

                string recipeDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                IReadOnlyList<RecipeDatasetVersionSnapshot> history = RecipeDatasetVersionService.LoadHistory(recipeDirectory);
                RecipeDatasetVersionSnapshot recorded = history.FirstOrDefault(item => item.DatasetVersionId == manifest.DatasetVersionId);
                return new RecipeDatasetVersionPresentation
                {
                    VersionText = manifest.DatasetVersionId,
                    CapturedUtc = recorded?.CapturedUtc ?? manifest.GeneratedUtc,
                    ImageCount = manifest.ContentIdentity.ImageFileCount,
                    DetailText = string.Format(OpenVisionLanguageService.T("WpfDatasetVersion.Detail"),
                        Shorten(manifest.ContentIdentity.ContentSha256), manifest.ContentIdentity.ImageFileCount,
                        manifest.ContentIdentity.AnnotationFileCount, history.Count)
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
                DisplayText = OpenVisionLanguageService.T(hasRecordedVersion ? "WpfDatasetVersion.LastRecorded" : "WpfDatasetVersion.NoRecordedVersion"),
                DetailText = unavailable
                    ? string.Format(OpenVisionLanguageService.T("WpfDatasetVersion.LastRecordedUnavailable"), recordedVersion)
                    : string.Format(OpenVisionLanguageService.T("WpfDatasetVersion.LastRecordedDetail"), recordedVersion, hasRecordedVersion ? recorded.DetailText : string.Empty)
            };
        }

        public static string FormatLabel(string capturedUtc, int imageCount)
        {
            string time = DateTime.TryParseExact(capturedUtc, new[] { "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" },
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime utc)
                ? utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
                : OpenVisionLanguageService.T("WpfDatasetVersion.TimeUnknown");
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfDatasetVersion.SavedLabel"), time, imageCount);
        }

        public static string MissingLabel => OpenVisionLanguageService.T("WpfDatasetVersion.MetadataUnknown");

        public static IReadOnlyDictionary<string, RecipeDatasetVersionPresentation> BuildLabels(string manifestPath, RecipeDatasetVersionSnapshot current = null)
        {
            var labels = new Dictionary<string, RecipeDatasetVersionPresentation>(StringComparer.Ordinal);
            try
            {
                foreach (RecipeDatasetVersionSnapshot snapshot in RecipeDatasetVersionService.LoadHistory(Path.GetDirectoryName(manifestPath ?? string.Empty)))
                    labels[snapshot.DatasetVersionId] = new RecipeDatasetVersionPresentation { VersionText = snapshot.DatasetVersionId, CapturedUtc = snapshot.CapturedUtc, ImageCount = snapshot.ImageFileCount };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Missing display metadata must not remove the original selectable version.
                AppLog.ABNORMAL("Dataset version display metadata unavailable: " + error.Message);
            }
            if (!string.IsNullOrEmpty(current?.DatasetVersionId) && !labels.ContainsKey(current.DatasetVersionId))
                labels[current.DatasetVersionId] = new RecipeDatasetVersionPresentation { VersionText = current.DatasetVersionId, ImageCount = current.ImageFileCount, IsCurrentInput = true };
            return labels;
        }

        private static RecipeDatasetVersionPresentation BuildReadFailure(string message)
            => new RecipeDatasetVersionPresentation
            {
                VersionText = OpenVisionLanguageService.T("WpfDatasetVersion.ReadFailed"),
                DisplayText = OpenVisionLanguageService.T("WpfDatasetVersion.ReadFailed"),
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
                    DisplayText = presentation.DisplayText,
                    DetailText = presentation.DetailText
                };
        }
    }

    [Obsolete("Use RecipeDatasetVersionPresentation.", false)]
    public sealed class WpfRecipeDatasetVersionPresentation : RecipeDatasetVersionPresentation
    {
    }
}
