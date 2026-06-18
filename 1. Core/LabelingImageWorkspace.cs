using OpenVisionLab.ImageSpace.Core;
using System.Drawing;

namespace MvcVisionSystem._1._Core
{
    public sealed class LabelingImageWorkspace
    {
        private const int MainImageIndex = 0;
        private const string MainImageTitle = "Main";
        private readonly IImageSpace imageSpace = new ImageSpaceService();

        public string ActiveImageName { get; private set; } = string.Empty;

        public string ActiveImagePath { get; private set; } = string.Empty;

        public Bitmap ActiveImage => imageSpace.GetActiveImage();

        public bool MainImageChanged => imageSpace.IsImageChanged(MainImageTitle);

        public void SetActiveImage(string imageName, string imagePath, Bitmap image)
        {
            ActiveImageName = imageName ?? string.Empty;
            ActiveImagePath = imagePath ?? string.Empty;

            imageSpace.SetActiveImage(image);
            imageSpace.SetImage(MainImageIndex, MainImageTitle, image);
            imageSpace.MarkImageChanged(MainImageTitle, image != null);
        }

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
