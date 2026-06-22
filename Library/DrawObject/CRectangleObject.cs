using OpenVisionLab.DrawObject;
using System;
using System.Drawing;
using static OpenVisionLab.DrawObject.DrawObjectEnums;

namespace MvcVisionSystem.DrawObject
{
    public class CRectangleObject : RectangleDrawObject
    {
        private const int DefaultHandleSize = 12;

        public Yolo.CClassItem cClassItem = new Yolo.CClassItem();

        public override DrawObjectBase Clone() => (CRectangleObject)MemberwiseClone();

        public override void SetParameter(Color color, Size size, Point location, bool isRotate = true, string text = "")
        {
            ApplyLabelStyle(size, location, isRotate);
        }

        public void SetParameter(Size size, Point location, bool isRotate = true)
        {
            ApplyLabelStyle(size, location, isRotate);
        }

        public PosSizableRect GetNodeSelectable(Point imagePoint, int handleSize = DefaultHandleSize)
        {
            if (Roi.IsEmpty)
            {
                return PosSizableRect.None;
            }

            foreach (PosSizableRect position in Enum.GetValues(typeof(PosSizableRect)))
            {
                if ((int)position < 9 && GetHandleRectangle(position, Roi, handleSize).Contains(imagePoint))
                {
                    return position;
                }
            }

            return Roi.Contains(imagePoint) ? PosSizableRect.SizeAll : PosSizableRect.None;
        }

        private void ApplyLabelStyle(Size size, Point location, bool isRotate)
        {
            Color = cClassItem?.DrawColor ?? Color.Green;
            Title = cClassItem?.Text ?? string.Empty;
            Size = size;
            Location = location;
            IsRotate = isRotate;
        }

        private static Rectangle GetHandleRectangle(PosSizableRect position, Rectangle rectangle, int handleSize)
        {
            return position switch
            {
                PosSizableRect.LeftUp => CreateHandle(rectangle.X, rectangle.Y, handleSize),
                PosSizableRect.LeftMiddle => CreateHandle(rectangle.X, rectangle.Y + rectangle.Height / 2, handleSize),
                PosSizableRect.LeftBottom => CreateHandle(rectangle.X, rectangle.Y + rectangle.Height, handleSize),
                PosSizableRect.BottomMiddle => CreateHandle(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height, handleSize),
                PosSizableRect.RightUp => CreateHandle(rectangle.X + rectangle.Width, rectangle.Y, handleSize),
                PosSizableRect.RightBottom => CreateHandle(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height, handleSize),
                PosSizableRect.RightMiddle => CreateHandle(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height / 2, handleSize),
                PosSizableRect.UpMiddle => CreateHandle(rectangle.X + rectangle.Width / 2, rectangle.Y, handleSize),
                PosSizableRect.Rotate => CreateRotateHandle(rectangle, handleSize),
                _ => Rectangle.Empty
            };
        }

        private static Rectangle CreateRotateHandle(Rectangle rectangle, int handleSize)
        {
            Rectangle handle = CreateHandle(rectangle.X + rectangle.Width / 2, rectangle.Y, handleSize);
            return new Rectangle(handle.Left, handle.Top - (handle.Height * 6) + 1, handle.Width + 1, handle.Height + 1);
        }

        private static Rectangle CreateHandle(int x, int y, int handleSize)
        {
            return new Rectangle(x - handleSize / 2, y - handleSize / 2, handleSize, handleSize);
        }
    }
}
