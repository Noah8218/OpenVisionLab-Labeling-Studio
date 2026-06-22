using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace MvcVisionSystem
{
    public static class ScreenCaptureService
    {
        private const int SrcCopy = 0x00CC0020;
        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;

        public static Bitmap CapturePrimaryScreen()
        {
            int width = GetSystemMetrics(SmCxScreen);
            int height = GetSystemMetrics(SmCyScreen);
            return Capture(new Rectangle(0, 0, width, height));
        }

        public static string GetCaptureDirectory(string startupPath)
        {
            return Path.Combine(startupPath ?? string.Empty, "CAPTURE");
        }

        public static string CreateCaptureFilePath(string startupPath, string windowTitle, DateTime timestamp)
        {
            string captureDirectory = GetCaptureDirectory(startupPath);
            Directory.CreateDirectory(captureDirectory);

            string title = SanitizeFileName(string.IsNullOrWhiteSpace(windowTitle) ? "Capture" : windowTitle);
            return Path.Combine(captureDirectory, $"{title}_{timestamp:yyyyMMdd_HHmmss}.jpeg");
        }

        internal static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] chars = (fileName ?? string.Empty).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Capture" : sanitized;
        }

        private static Bitmap Capture(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), "Capture bounds must have a positive size.");
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to get screen device context.");
            }

            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmapHandle = IntPtr.Zero;
            IntPtr oldObject = IntPtr.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create memory device context.");
                }

                bitmapHandle = CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);
                if (bitmapHandle == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create capture bitmap.");
                }

                oldObject = SelectObject(memoryDc, bitmapHandle);
                if (oldObject == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to select capture bitmap.");
                }

                if (!BitBlt(memoryDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.Left, bounds.Top, SrcCopy))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Screen capture failed.");
                }

                using (Bitmap captured = Image.FromHbitmap(bitmapHandle))
                {
                    return new Bitmap(captured);
                }
            }
            finally
            {
                if (oldObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
                {
                    SelectObject(memoryDc, oldObject);
                }

                if (bitmapHandle != IntPtr.Zero)
                {
                    DeleteObject(bitmapHandle);
                }

                if (memoryDc != IntPtr.Zero)
                {
                    DeleteDC(memoryDc);
                }

                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rasterOperation);
    }
}
