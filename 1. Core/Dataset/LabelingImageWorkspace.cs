using OpenVisionLab.ImageSpace.Core;
using System.Drawing;

namespace MvcVisionSystem._1._Core
{
    public sealed class LabelingImageSnapshot
    {
        public static LabelingImageSnapshot Empty { get; } =
            new LabelingImageSnapshot(string.Empty, string.Empty, null);

        public LabelingImageSnapshot(string imageName, string imagePath, Bitmap image)
        {
            ImageName = imageName ?? string.Empty;
            ImagePath = imagePath ?? string.Empty;
            Image = image;
            ImageSize = image?.Size ?? Size.Empty;
        }

        public string ImageName { get; }

        public string ImagePath { get; }

        public Bitmap Image { get; }

        public Size ImageSize { get; }
    }

    public sealed class LabelingImageWorkspace
    {
        private const int MainImageIndex = 0;
        private const string MainImageTitle = "Main";
        private readonly IImageSpace imageSpace = new ImageSpaceService();
        private LabelingImageSnapshot activeImage = LabelingImageSnapshot.Empty;

        public string ActiveImageName => activeImage.ImageName;

        public string ActiveImagePath => activeImage.ImagePath;

        public Bitmap ActiveImage => activeImage.Image;

        public Size ActiveImageSize => activeImage.ImageSize;

        public bool MainImageChanged => imageSpace.IsImageChanged(MainImageTitle);

        public void SetActiveImage(string imageName, string imagePath, Bitmap image)
        {
            imageSpace.SetActiveImage(image);
            imageSpace.SetImage(MainImageIndex, MainImageTitle, image);
            imageSpace.MarkImageChanged(MainImageTitle, image != null);
            activeImage = new LabelingImageSnapshot(imageName, imagePath, image);
        }

        public LabelingImageSnapshot CaptureSnapshot()
            => activeImage;

        public void AcceptMainImageChange()
        {
            imageSpace.AcceptImageChanged(MainImageTitle);
        }

        public Bitmap GetMainImage()
        {
            return imageSpace.GetImage(MainImageTitle);
        }
    }
}
