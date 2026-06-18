using System.Drawing;
using System.IO;

namespace MvcVisionSystem
{
    public static class AppImageLoader
    {
        public static Bitmap LoadBitmap(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image image = Image.FromStream(stream))
            {
                return new Bitmap(image);
            }
        }
    }
}
