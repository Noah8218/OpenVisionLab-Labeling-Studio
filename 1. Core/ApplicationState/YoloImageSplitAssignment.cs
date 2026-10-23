namespace MvcVisionSystem
{
    public sealed class YoloImageSplitAssignment
    {
        // The annotation format already identifies an image by its filename without extension.
        public string ImageName { get; set; } = string.Empty;

        public string Split { get; set; } = string.Empty;

        public bool IsAutomatic { get; set; }

        public string SourceSha256 { get; set; } = string.Empty;
    }
}
