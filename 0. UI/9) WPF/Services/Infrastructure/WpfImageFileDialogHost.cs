using OpenVisionLab.ImageCanvas.Infrastructure;
using System;
using System.Windows;

namespace MvcVisionSystem
{
    internal sealed class WpfImageFileDialogHost : IImageFileDialogHost
    {
        private const string ImageFilter =
            "Image files (*.bmp;*.jpg;*.jpeg;*.png;*.gif)|*.bmp;*.jpg;*.jpeg;*.png;*.gif|All files (*.*)|*.*";
        private readonly Window owner;
        private readonly WpfFileDialogService fileDialogService;

        public WpfImageFileDialogHost(Window owner, WpfFileDialogService fileDialogService)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        }

        public bool TryPickImageFile(out string selectedPath)
        {
            return fileDialogService.TryPickFile(owner, "이미지 열기", ImageFilter, string.Empty, out selectedPath);
        }
    }
}
