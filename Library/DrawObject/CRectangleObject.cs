using OpenVisionLab.DrawObject;
using System;
using System.Drawing;
using System.Windows.Forms;
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

        public Cursor ChangeCursor(Point imagePoint, int handleSize = DefaultHandleSize)
        {
            return AnchorToCursor(GetNodeSelectable(imagePoint, handleSize), Angle);
        }

        private void ApplyLabelStyle(Size size, Point location, bool isRotate)
        {
            Color = cClassItem?.DrawColor ?? Color.Green;
            Title = cClassItem?.Text ?? string.Empty;
            Size = size;
            Location = location;
            IsRotate = isRotate;
        }

        private static Cursor AnchorToCursor(PosSizableRect handle, double angle)
        {
            double cursorAngle = angle;

            switch (handle)
            {
                case PosSizableRect.Rotate:
                    return Cursors.NoMove2D;
                case PosSizableRect.SizeAll:
                    return Cursors.SizeAll;
                case PosSizableRect.None:
                    return Cursors.Default;
                case PosSizableRect.LeftUp:
                case PosSizableRect.RightBottom:
                    cursorAngle += 45;
                    break;
                case PosSizableRect.UpMiddle:
                case PosSizableRect.BottomMiddle:
                    cursorAngle += 90;
                    break;
                case PosSizableRect.LeftBottom:
                case PosSizableRect.RightUp:
                    cursorAngle += 135;
                    break;
            }

            if (cursorAngle > 360)
            {
                cursorAngle -= 360;
            }

            return cursorAngle switch
            {
                > 26 and < 68 or > 204 and < 248 => Cursors.SizeNWSE,
                > 69 and < 113 or > 249 and < 293 => Cursors.SizeNS,
                > 114 and < 158 or > 294 and < 338 => Cursors.SizeNESW,
                _ => Cursors.SizeWE
            };
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
