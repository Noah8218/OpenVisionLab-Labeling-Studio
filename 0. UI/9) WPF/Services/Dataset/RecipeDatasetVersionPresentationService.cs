using Newtonsoft.Json;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public class RecipeDatasetVersionPresentation
    {
        public string VersionText { get; init; } = "저장 후 생성";

        public string DetailText { get; init; } = "Recipe를 저장하면 이미지·라벨·클래스·분할의 SHA-256 버전을 기록합니다.";
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
                        VersionText = "이전 manifest",
                        DetailText = "설정을 저장하면 Recipe Dataset Version v2로 갱신됩니다."
                    };
                }

                string recipeDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                int historyCount = RecipeDatasetVersionService.LoadHistory(recipeDirectory).Count;
                return new RecipeDatasetVersionPresentation
                {
                    VersionText = manifest.DatasetVersionId,
                    DetailText =
                        $"SHA-256 {Shorten(manifest.ContentIdentity.ContentSha256)} · "
                        + $"이미지 {manifest.ContentIdentity.ImageFileCount:N0} · "
                        + $"라벨 {manifest.ContentIdentity.AnnotationFileCount:N0} · "
                        + $"불변 이력 {historyCount:N0}개"
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

        private static RecipeDatasetVersionPresentation BuildReadFailure(string message)
            => new RecipeDatasetVersionPresentation
            {
                VersionText = "버전 확인 실패",
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
