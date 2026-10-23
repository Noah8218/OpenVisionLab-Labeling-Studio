using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    public sealed class ActionContentWrapPanel : WrapPanel
    {
        public static readonly DependencyProperty CenterLinesProperty = DependencyProperty.Register(
            nameof(CenterLines), typeof(bool), typeof(ActionContentWrapPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

        public bool CenterLines
        {
            get => (bool)GetValue(CenterLinesProperty);
            set => SetValue(CenterLinesProperty, value);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!CenterLines || Orientation != System.Windows.Controls.Orientation.Horizontal)
                return base.ArrangeOverride(finalSize);

            double lineWidth = 0D, lineHeight = 0D, top = 0D;
            int first = 0;
            for (int index = 0; index <= InternalChildren.Count; index++)
            {
                UIElement child = index < InternalChildren.Count ? InternalChildren[index] : null;
                double width = child == null ? 0D : double.IsNaN(ItemWidth) ? child.DesiredSize.Width : ItemWidth;
                double height = child == null ? 0D : double.IsNaN(ItemHeight) ? child.DesiredSize.Height : ItemHeight;
                // WPF 기본 측정과 같은 부동소수 경계로 줄을 나누되 시작 위치만 바꾼다.
                double epsilon = (Math.Abs(lineWidth + width) + Math.Abs(finalSize.Width) + 10D) * 2.2204460492503131e-16;
                if (child == null || lineWidth > 0D && lineWidth + width - finalSize.Width > epsilon)
                {
                    double left = (finalSize.Width - lineWidth) / 2D;
                    for (int item = first; item < index; item++)
                    {
                        UIElement lineChild = InternalChildren[item];
                        double childWidth = double.IsNaN(ItemWidth) ? lineChild.DesiredSize.Width : ItemWidth;
                        lineChild.Arrange(new Rect(left, top, childWidth, lineHeight));
                        left += childWidth;
                    }
                    top += lineHeight; lineWidth = 0D; lineHeight = 0D; first = index;
                }
                if (child != null)
                {
                    lineWidth += width;
                    lineHeight = Math.Max(lineHeight, height);
                }
            }
            return finalSize;
        }
    }
}
