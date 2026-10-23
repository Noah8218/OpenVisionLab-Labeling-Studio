using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;

namespace MvcVisionSystem
{
    // Draws read-only evidence in display coordinates; image loading and review state stay with their existing owners.
    public sealed class ComparisonImageView : FrameworkElement
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ComparisonImageView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ReviewProperty = DependencyProperty.Register(nameof(Review), typeof(ModelComparisonImageReview), typeof(ComparisonImageView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ShowGroundTruthProperty = DependencyProperty.Register(nameof(ShowGroundTruth), typeof(bool), typeof(ComparisonImageView), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ClassColorsProperty = DependencyProperty.Register(nameof(ClassColors), typeof(IReadOnlyList<Color>), typeof(ComparisonImageView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FalsePositiveTextProperty = DependencyProperty.Register(nameof(FalsePositiveText), typeof(string), typeof(ComparisonImageView), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FalseNegativeTextProperty = DependencyProperty.Register(nameof(FalseNegativeText), typeof(string), typeof(ComparisonImageView), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(ComparisonImageView), new FrameworkPropertyMetadata(new FontFamily("Segoe UI, Malgun Gothic"), FrameworkPropertyMetadataOptions.AffectsRender));

        public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
        public ModelComparisonImageReview Review { get => (ModelComparisonImageReview)GetValue(ReviewProperty); set => SetValue(ReviewProperty, value); }
        public bool ShowGroundTruth { get => (bool)GetValue(ShowGroundTruthProperty); set => SetValue(ShowGroundTruthProperty, value); }
        public IReadOnlyList<Color> ClassColors { get => (IReadOnlyList<Color>)GetValue(ClassColorsProperty); set => SetValue(ClassColorsProperty, value); }
        public string FalsePositiveText { get => (string)GetValue(FalsePositiveTextProperty); set => SetValue(FalsePositiveTextProperty, value); }
        public string FalseNegativeText { get => (string)GetValue(FalseNegativeTextProperty); set => SetValue(FalseNegativeTextProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            ImageSource source = Source;
            if (source == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            double width = source is BitmapSource bitmap ? bitmap.PixelWidth : source.Width;
            double height = source is BitmapSource bitmapSource ? bitmapSource.PixelHeight : source.Height;
            if (width <= 0 || height <= 0) return;
            double scale = Math.Min(ActualWidth / width, ActualHeight / height);
            Rect image = new((ActualWidth - width * scale) / 2, (ActualHeight - height * scale) / 2, width * scale, height * scale);
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            dc.DrawImage(source, image);
            if (Review != null)
            {
                if (ShowGroundTruth) foreach (WpfModelBenchmarkNormalizedBox box in Review.GroundTruth) DrawBox(dc, image, scale, box, Colors.White, 2, new double[] { 6, 4 }, null);
                foreach (WpfModelBenchmarkNormalizedBox box in Review.TruePositives)
                    DrawBox(dc, image, scale, box, ClassColors?.Count > 0 ? ClassColors[box.ClassId % ClassColors.Count] : Color.FromRgb(0x4C, 0x8D, 0xFF), 3, null, null);
                foreach (WpfModelBenchmarkNormalizedBox box in Review.FalsePositives) DrawBox(dc, image, scale, box, Color.FromRgb(0xF0, 0x6A, 0x6A), 3, new double[] { 8, 4 }, FalsePositiveText);
                foreach (WpfModelBenchmarkNormalizedBox box in Review.FalseNegatives) DrawBox(dc, image, scale, box, Color.FromRgb(0xF2, 0xB8, 0x4B), 4, null, FalseNegativeText);
            }
            dc.Pop();
        }

        private void DrawBox(DrawingContext dc, Rect image, double scale, WpfModelBenchmarkNormalizedBox box, Color color, double thickness, double[] dash, string label)
        {
            Rect bounds = new(image.X + box.XMin * image.Width, image.Y + box.YMin * image.Height, (box.XMax - box.XMin) * image.Width, (box.YMax - box.YMin) * image.Height);
            SolidColorBrush brush = new(color);
            Pen pen = new(brush, thickness * scale) { DashCap = PenLineCap.Flat };
            // SVG stroke and dash metrics are image units; WPF dash ratios inherit the scaled pen thickness.
            if (dash != null) pen.DashStyle = new DashStyle(new double[] { dash[0] / thickness, dash[1] / thickness }, 0);
            dc.DrawRectangle(null, pen, bounds);
            if (string.IsNullOrEmpty(label)) return;
            FormattedText text = new(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 14, new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x12)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double tagWidth = label.Length * 11 + 14;
            double left = (bounds.Left - image.Left) / scale;
            double top = Math.Max(18, (bounds.Top - image.Top) / scale) - 20;
            Rect tag = new(left, top, tagWidth, 20);
            // SVG tags keep their image-unit coordinates and are clipped only by the outer viewport.
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, image.Left, image.Top));
            dc.DrawRoundedRectangle(brush, null, tag, 3, 3);
            dc.DrawText(text, new Point(left + 7, top + 14 - text.Baseline));
            dc.Pop();
        }
    }
}
