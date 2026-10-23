using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MvcVisionSystem
{
    // The final child is the editor: wrap it when needed, then fill the remaining row.
    public sealed class ClassEntryPanel : Panel
    {
        private const double RowGap = 6;
        private const double EntryPadding = 7;

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            if (e.NewFocus is FrameworkElement focused && IsAncestorOf(focused)) BringFocusedChildIntoView(focused);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (IsKeyboardFocusWithin && Keyboard.FocusedElement is FrameworkElement focused && IsAncestorOf(focused)) BringFocusedChildIntoView(focused);
        }

        private void BringFocusedChildIntoView(FrameworkElement focused)
        {
            Rect bounds = focused.TransformToAncestor(this).TransformBounds(new Rect(focused.RenderSize));
            bounds.Inflate(EntryPadding, EntryPadding);
            BringIntoView(bounds);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = availableSize.Width, x = 0, y = 0, rowHeight = 0, desiredWidth = 0;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];
                double measureWidth = i == InternalChildren.Count - 1
                    ? ((FrameworkElement)child).MinWidth : width;
                child.Measure(new Size(measureWidth, double.PositiveInfinity));
                if (x > 0 && x + child.DesiredSize.Width > width)
                {
                    desiredWidth = Math.Max(desiredWidth, x);
                    y += rowHeight + RowGap; x = 0; rowHeight = 0;
                }
                x += child.DesiredSize.Width;
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }
            return new Size(Math.Max(desiredWidth, x), y + rowHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double x = 0, y = 0, rowHeight = 0;
            int rowStart = 0;
            for (int i = 0; i <= InternalChildren.Count; i++)
            {
                bool end = i == InternalChildren.Count;
                if (end || (x > 0 && x + InternalChildren[i].DesiredSize.Width > finalSize.Width))
                {
                    double left = 0;
                    for (int j = rowStart; j < i; j++)
                    {
                        double childWidth = j == InternalChildren.Count - 1
                            ? Math.Max(0, finalSize.Width - left) : InternalChildren[j].DesiredSize.Width;
                        InternalChildren[j].Arrange(new Rect(left, y, childWidth, rowHeight));
                        left += childWidth;
                    }
                    y += rowHeight + RowGap; x = 0; rowHeight = 0; rowStart = i;
                }
                if (!end)
                {
                    x += InternalChildren[i].DesiredSize.Width;
                    rowHeight = Math.Max(rowHeight, InternalChildren[i].DesiredSize.Height);
                }
            }
            return finalSize;
        }
    }
}
