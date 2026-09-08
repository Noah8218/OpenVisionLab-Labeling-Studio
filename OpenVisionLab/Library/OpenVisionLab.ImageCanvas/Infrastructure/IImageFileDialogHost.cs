namespace OpenVisionLab.ImageCanvas.Infrastructure
{
	public interface IImageFileDialogHost
	{
		bool TryPickImageFile(out string selectedPath);
	}
}
