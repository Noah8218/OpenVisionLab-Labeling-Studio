using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MvcVisionSystem
{
    public class PatchCoreHeatmapAvailability
    {
        public bool IsPatchCoreCandidate { get; init; }

        public bool CanOpen { get; init; }

        public string FullPath { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string ToolTip { get; init; } = string.Empty;
    }

    public class PatchCoreHeatmapLoadResult
    {
        public bool Succeeded { get; init; }

        public ImageSource ImageSource { get; init; }

        public PatchCoreHeatmapAvailability Availability { get; init; }

        public string StatusText { get; init; } = string.Empty;
    }

    public class PatchCoreHeatmapReviewService
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp",
            ".gif",
            ".jpeg",
            ".jpg",
            ".png",
            ".tif",
            ".tiff"
        };

        public PatchCoreHeatmapAvailability Inspect(YoloWorkerSmokeCandidate candidate)
        {
            if (!string.Equals(candidate?.PredictionType, "patchcore", StringComparison.OrdinalIgnoreCase))
            {
                return new PatchCoreHeatmapAvailability();
            }

            string configuredPath = candidate.HeatmapPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return Unavailable(
                    string.Empty,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.NoPath"));
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(configuredPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return Unavailable(
                    configuredPath,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.InvalidPath"));
            }

            string extension = Path.GetExtension(fullPath);
            if (!SupportedExtensions.Contains(extension))
            {
                return Unavailable(
                    fullPath,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.UnsupportedFormat"));
            }

            if (!File.Exists(fullPath))
            {
                return Unavailable(
                    fullPath,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.Missing"));
            }

            return new PatchCoreHeatmapAvailability
            {
                IsPatchCoreCandidate = true,
                CanOpen = true,
                FullPath = fullPath,
                FileName = Path.GetFileName(fullPath),
                StatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.Available"),
                ToolTip = fullPath
            };
        }

        public PatchCoreHeatmapLoadResult Load(YoloWorkerSmokeCandidate candidate)
        {
            PatchCoreHeatmapAvailability availability = Inspect(candidate);
            if (!availability.CanOpen)
            {
                return new PatchCoreHeatmapLoadResult
                {
                    Availability = availability,
                    StatusText = availability.StatusText
                };
            }

            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(
                    availability.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.DecodePixelWidth = 960;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                bitmap.Freeze();
                return new PatchCoreHeatmapLoadResult
                {
                    Succeeded = true,
                    ImageSource = bitmap,
                    Availability = availability,
                    StatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.Opened")
                };
            }
            catch (Exception ex) when (
                ex is IOException
                || ex is UnauthorizedAccessException
                || ex is NotSupportedException
                || ex is InvalidOperationException
                || ex is ArgumentException)
            {
                return new PatchCoreHeatmapLoadResult
                {
                    Availability = availability,
                    StatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.Status.ReadFailed")
                };
            }
        }

        private static PatchCoreHeatmapAvailability Unavailable(string path, string statusText)
        {
            return new PatchCoreHeatmapAvailability
            {
                IsPatchCoreCandidate = true,
                CanOpen = false,
                FullPath = path,
                FileName = string.IsNullOrWhiteSpace(path) ? OpenVisionLab.OpenVisionLanguageService.T("WpfPatchCoreHeatmap.File.None") : Path.GetFileName(path),
                StatusText = statusText,
                ToolTip = string.IsNullOrWhiteSpace(path) ? statusText : path
            };
        }
    }

    [Obsolete("Use PatchCoreHeatmapReviewService.", false)]
    public sealed class WpfPatchCoreHeatmapReviewService : PatchCoreHeatmapReviewService
    {
        public new WpfPatchCoreHeatmapAvailability Inspect(YoloWorkerSmokeCandidate candidate)
            => WpfPatchCoreHeatmapAvailability.FromCanonical(base.Inspect(candidate));

        public new WpfPatchCoreHeatmapLoadResult Load(YoloWorkerSmokeCandidate candidate)
            => WpfPatchCoreHeatmapLoadResult.FromCanonical(base.Load(candidate));
    }

    [Obsolete("Use PatchCoreHeatmapAvailability.", false)]
    public sealed class WpfPatchCoreHeatmapAvailability : PatchCoreHeatmapAvailability
    {
        internal static WpfPatchCoreHeatmapAvailability FromCanonical(PatchCoreHeatmapAvailability source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfPatchCoreHeatmapAvailability
            {
                IsPatchCoreCandidate = source.IsPatchCoreCandidate,
                CanOpen = source.CanOpen,
                FullPath = source.FullPath,
                FileName = source.FileName,
                StatusText = source.StatusText,
                ToolTip = source.ToolTip
            };
        }
    }

    [Obsolete("Use PatchCoreHeatmapLoadResult.", false)]
    public sealed class WpfPatchCoreHeatmapLoadResult : PatchCoreHeatmapLoadResult
    {
        internal static WpfPatchCoreHeatmapLoadResult FromCanonical(PatchCoreHeatmapLoadResult source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfPatchCoreHeatmapLoadResult
            {
                Succeeded = source.Succeeded,
                ImageSource = source.ImageSource,
                Availability = WpfPatchCoreHeatmapAvailability.FromCanonical(source.Availability),
                StatusText = source.StatusText
            };
        }
    }
}
