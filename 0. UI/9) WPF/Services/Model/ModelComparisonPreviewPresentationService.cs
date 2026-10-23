using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;

namespace MvcVisionSystem
{
    // Frozen images own their bytes; no source stream survives the preview operation.
    public static class ModelComparisonPreviewPresentationService
    {
        public static ModelComparisonPreviewPresentation Build(ModelComparisonImagePair pair, IReadOnlyList<Color> classColors)
        {
            if (pair == null) return new(null, null, string.Empty);
            try
            {
                BitmapSource image = LoadVerifiedImage(pair.Baseline, 0);
                return new(image, image, string.Empty) { ClassColors = new List<Color>(classColors ?? Array.Empty<Color>()).AsReadOnly() };
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is FileFormatException || ex is ArgumentException)
            {
                return new(null, null, string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfComparisonWorkspace.PreviewFailed"), ex.Message));
            }
        }

        internal static ImageSource BuildThumbnail(ModelComparisonImageReview image)
        {
            try { return LoadVerifiedImage(image, 96); }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is FileFormatException || ex is ArgumentException) { return null; }
        }

        private static BitmapSource LoadVerifiedImage(ModelComparisonImageReview review, int decodePixelWidth)
        {
            using FileStream stream = new(review.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (review.ImageSha256.Length != 64 || hash != review.ImageSha256) throw new InvalidDataException(OpenVisionLanguageService.T("WpfComparisonWorkspace.ImageChanged"));
            stream.Position = 0;
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0) image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        public static ModelComparisonPreviewPresentation Build(WpfModelComparisonReviewExample example)
        {
            if (example == null) return new(null, null, string.Empty);
            try
            {
                using FileStream stream = File.OpenRead(example.ImagePath);
                BitmapImage image = new();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return new(Draw(image, example.BaselinePredictions, Colors.DeepSkyBlue), Draw(image, example.CandidatePredictions, Colors.Orange), string.Empty);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is FileFormatException || ex is ArgumentException)
            {
                return new(null, null, string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfTraining.Comparison.PreviewFailed"), ex.Message));
            }
        }

        private static ImageSource Draw(BitmapSource image, IReadOnlyList<ModelComparisonPrediction> predictions, Color color)
        {
            double width = image.PixelWidth;
            double height = image.PixelHeight;
            DrawingGroup group = new() { ClipGeometry = new RectangleGeometry(new Rect(0, 0, width, height)) };
            using (DrawingContext dc = group.Open())
            {
                dc.DrawImage(image, new Rect(0, 0, width, height));
                Pen pen = new(new SolidColorBrush(color), Math.Max(1, width / 250));
                foreach (ModelComparisonPrediction prediction in predictions ?? Array.Empty<ModelComparisonPrediction>())
                {
                    if (!double.IsFinite(prediction.Left) || !double.IsFinite(prediction.Top) || !double.IsFinite(prediction.Right) || !double.IsFinite(prediction.Bottom)) continue;
                    double left = Math.Clamp(prediction.Left, 0, 1) * width;
                    double top = Math.Clamp(prediction.Top, 0, 1) * height;
                    double right = Math.Clamp(prediction.Right, 0, 1) * width;
                    double bottom = Math.Clamp(prediction.Bottom, 0, 1) * height;
                    if (right <= left || bottom <= top) continue;
                    dc.DrawRectangle(null, pen, new Rect(left, top, right - left, bottom - top));
                    FormattedText label = new(prediction.ClassName + " " + prediction.Confidence.ToString("P0", CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), Math.Max(10, width / 35), Brushes.White, 1);
                    double labelTop = Math.Max(0, top - label.Height);
                    dc.DrawRectangle(Brushes.Black, null, new Rect(left, labelTop, Math.Min(label.Width, width - left), label.Height));
                    dc.DrawText(label, new Point(left, labelTop));
                }
            }
            group.Freeze();
            DrawingImage result = new(group);
            result.Freeze();
            return result;
        }
    }

    public sealed record ModelComparisonPreviewPresentation(ImageSource Baseline, ImageSource Candidate, string Error)
    {
        public IReadOnlyList<Color> ClassColors { get; init; } = Array.Empty<Color>();
    }
}
