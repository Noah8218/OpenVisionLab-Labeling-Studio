using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.Mvvm;

namespace OpenVisionLab.Mvvm.Behaviors
{
    public enum TextInputFilterMode
    {
        None,
        Integer,
        Decimal
    }

    /// <summary>
    /// Small WPF behaviors for view-only input wiring so panels can bind commands without code-behind event relays.
    /// </summary>
    public static class InputCommandBehaviors
    {
        private static readonly MouseButtonEventHandler MouseClickInputEventHandler = MouseClickInputHandler;

        public static readonly DependencyProperty SelectionChangedCommandProperty =
            DependencyProperty.RegisterAttached(
                "SelectionChangedCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnSelectionChangedCommandChanged));
        // Value-only command variants keep ViewModels free of WPF EventArgs while older panels migrate incrementally.
        public static readonly DependencyProperty SelectedItemChangedCommandProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItemChangedCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnSelectedItemChangedCommandChanged));

        public static readonly DependencyProperty PreviewKeyDownCommandProperty =
            DependencyProperty.RegisterAttached(
                "PreviewKeyDownCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnPreviewKeyDownCommandChanged));
        public static readonly DependencyProperty PreviewKeyInputCommandProperty =
            DependencyProperty.RegisterAttached(
                "PreviewKeyInputCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnPreviewKeyInputCommandChanged));

        public static readonly DependencyProperty TextChangedCommandProperty =
            DependencyProperty.RegisterAttached(
                "TextChangedCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnTextChangedCommandChanged));
        public static readonly DependencyProperty TextInputCommandProperty =
            DependencyProperty.RegisterAttached(
                "TextInputCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnTextInputCommandChanged));

        public static readonly DependencyProperty MouseDoubleClickCommandProperty =
            DependencyProperty.RegisterAttached(
                "MouseDoubleClickCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnMouseDoubleClickCommandChanged));
        public static readonly DependencyProperty MouseDoubleClickInputCommandProperty =
            DependencyProperty.RegisterAttached(
                "MouseDoubleClickInputCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnMouseDoubleClickInputCommandChanged));
        public static readonly DependencyProperty MouseClickInputCommandProperty =
            DependencyProperty.RegisterAttached(
                "MouseClickInputCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnMouseClickInputCommandChanged));

        public static readonly DependencyProperty ValueChangedCommandProperty =
            DependencyProperty.RegisterAttached(
                "ValueChangedCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnValueChangedCommandChanged));
        public static readonly DependencyProperty ValueInputCommandProperty =
            DependencyProperty.RegisterAttached(
                "ValueInputCommand",
                typeof(ICommand),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(null, OnValueInputCommandChanged));

        public static readonly DependencyProperty TextInputFilterProperty =
            DependencyProperty.RegisterAttached(
                "TextInputFilter",
                typeof(TextInputFilterMode),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(TextInputFilterMode.None, OnTextInputFilterChanged));

        public static readonly DependencyProperty ForwardMouseWheelToAncestorScrollViewerProperty =
            DependencyProperty.RegisterAttached(
                "ForwardMouseWheelToAncestorScrollViewer",
                typeof(bool),
                typeof(InputCommandBehaviors),
                new PropertyMetadata(false, OnForwardMouseWheelToAncestorScrollViewerChanged));

        public static ICommand GetSelectionChangedCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(SelectionChangedCommandProperty);
        }

        public static void SetSelectionChangedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(SelectionChangedCommandProperty, value);
        }
        public static ICommand GetSelectedItemChangedCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(SelectedItemChangedCommandProperty);
        }

        public static void SetSelectedItemChangedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(SelectedItemChangedCommandProperty, value);
        }

        public static ICommand GetPreviewKeyDownCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(PreviewKeyDownCommandProperty);
        }

        public static void SetPreviewKeyDownCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(PreviewKeyDownCommandProperty, value);
        }
        public static ICommand GetPreviewKeyInputCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(PreviewKeyInputCommandProperty);
        }

        public static void SetPreviewKeyInputCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(PreviewKeyInputCommandProperty, value);
        }

        public static ICommand GetTextChangedCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(TextChangedCommandProperty);
        }

        public static void SetTextChangedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(TextChangedCommandProperty, value);
        }
        public static ICommand GetTextInputCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(TextInputCommandProperty);
        }

        public static void SetTextInputCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(TextInputCommandProperty, value);
        }

        public static ICommand GetMouseDoubleClickCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(MouseDoubleClickCommandProperty);
        }

        public static void SetMouseDoubleClickCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(MouseDoubleClickCommandProperty, value);
        }
        public static ICommand GetMouseDoubleClickInputCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(MouseDoubleClickInputCommandProperty);
        }

        public static void SetMouseDoubleClickInputCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(MouseDoubleClickInputCommandProperty, value);
        }
        public static ICommand GetMouseClickInputCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(MouseClickInputCommandProperty);
        }

        public static void SetMouseClickInputCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(MouseClickInputCommandProperty, value);
        }

        public static ICommand GetValueChangedCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(ValueChangedCommandProperty);
        }

        public static void SetValueChangedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(ValueChangedCommandProperty, value);
        }
        public static ICommand GetValueInputCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(ValueInputCommandProperty);
        }

        public static void SetValueInputCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(ValueInputCommandProperty, value);
        }

        public static TextInputFilterMode GetTextInputFilter(DependencyObject target)
        {
            return (TextInputFilterMode)target.GetValue(TextInputFilterProperty);
        }

        public static void SetTextInputFilter(DependencyObject target, TextInputFilterMode value)
        {
            target.SetValue(TextInputFilterProperty, value);
        }

        public static bool GetForwardMouseWheelToAncestorScrollViewer(DependencyObject target)
        {
            return (bool)target.GetValue(ForwardMouseWheelToAncestorScrollViewerProperty);
        }

        public static void SetForwardMouseWheelToAncestorScrollViewer(DependencyObject target, bool value)
        {
            target.SetValue(ForwardMouseWheelToAncestorScrollViewerProperty, value);
        }

        private static void OnSelectionChangedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not Selector selector)
            {
                return;
            }

            selector.SelectionChanged -= Selector_SelectionChanged;
            if (e.NewValue != null)
            {
                selector.SelectionChanged += Selector_SelectionChanged;
            }
        }

        private static void Selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetSelectionChangedCommand(target), e);
        }
        private static void OnSelectedItemChangedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not Selector selector)
            {
                return;
            }

            selector.SelectionChanged -= Selector_SelectedItemChanged;
            if (e.NewValue != null)
            {
                selector.SelectionChanged += Selector_SelectedItemChanged;
            }
        }

        private static void Selector_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not Selector selector || sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetSelectedItemChangedCommand(target), selector.SelectedItem);
        }

        private static void OnPreviewKeyDownCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not UIElement element)
            {
                return;
            }

            element.PreviewKeyDown -= Element_PreviewKeyDown;
            if (e.NewValue != null)
            {
                element.PreviewKeyDown += Element_PreviewKeyDown;
            }
        }

        private static void Element_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetPreviewKeyDownCommand(target), e);
        }
        private static void OnPreviewKeyInputCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not UIElement element)
            {
                return;
            }

            element.PreviewKeyDown -= Element_PreviewKeyInput;
            if (e.NewValue != null)
            {
                element.PreviewKeyDown += Element_PreviewKeyInput;
            }
        }

        private static void Element_PreviewKeyInput(object sender, KeyEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            var args = new KeyInputCommandArgs(key, Keyboard.Modifiers, e.IsRepeat, e.OriginalSource);
            Execute(GetPreviewKeyInputCommand(target), args);
            if (args.Handled)
            {
                e.Handled = true;
            }
        }

        private static void OnTextChangedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not TextBox textBox)
            {
                return;
            }

            textBox.TextChanged -= TextBox_TextChanged;
            if (e.NewValue != null)
            {
                textBox.TextChanged += TextBox_TextChanged;
            }
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetTextChangedCommand(target), e);
        }
        private static void OnTextInputCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not TextBox textBox)
            {
                return;
            }

            textBox.TextChanged -= TextBox_TextInputChanged;
            if (e.NewValue != null)
            {
                textBox.TextChanged += TextBox_TextInputChanged;
            }
        }

        private static void TextBox_TextInputChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox || sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetTextInputCommand(target), textBox.Text ?? string.Empty);
        }

        private static void OnMouseDoubleClickCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not Control control)
            {
                return;
            }

            control.MouseDoubleClick -= Control_MouseDoubleClick;
            if (e.NewValue != null)
            {
                control.MouseDoubleClick += Control_MouseDoubleClick;
            }
        }

        private static void Control_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetMouseDoubleClickCommand(target), e);
        }
        private static void OnMouseDoubleClickInputCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not Control control)
            {
                return;
            }

            control.MouseDoubleClick -= Control_MouseDoubleClickInput;
            if (e.NewValue != null)
            {
                control.MouseDoubleClick += Control_MouseDoubleClickInput;
            }
        }

        private static void Control_MouseDoubleClickInput(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetMouseDoubleClickInputCommand(target), null);
        }

        private static void OnMouseClickInputCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not UIElement element)
            {
                return;
            }

            element.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, MouseClickInputEventHandler);
            if (e.NewValue != null)
            {
                element.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, MouseClickInputEventHandler, handledEventsToo: true);
            }
        }

        private static void MouseClickInputHandler(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            ICommand command = GetMouseClickInputCommand(target);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
                e.Handled = true;
            }
        }

        private static void OnValueChangedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not RangeBase rangeBase)
            {
                return;
            }

            rangeBase.ValueChanged -= RangeBase_ValueChanged;
            if (e.NewValue != null)
            {
                rangeBase.ValueChanged += RangeBase_ValueChanged;
            }
        }

        private static void RangeBase_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetValueChangedCommand(target), e);
        }
        private static void OnValueInputCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not RangeBase rangeBase)
            {
                return;
            }

            rangeBase.ValueChanged -= RangeBase_ValueInputChanged;
            if (e.NewValue != null)
            {
                rangeBase.ValueChanged += RangeBase_ValueInputChanged;
            }
        }

        private static void RangeBase_ValueInputChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            Execute(GetValueInputCommand(target), e.NewValue);
        }

        private static void Execute(ICommand command, object parameter)
        {
            if (command?.CanExecute(parameter) == true)
            {
                command.Execute(parameter);
            }
        }

        private static void OnForwardMouseWheelToAncestorScrollViewerChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not UIElement element)
            {
                return;
            }

            element.PreviewMouseWheel -= Element_ForwardMouseWheelToAncestorScrollViewer;
            if ((bool)e.NewValue)
            {
                element.PreviewMouseWheel += Element_ForwardMouseWheelToAncestorScrollViewer;
            }
        }

        private static void Element_ForwardMouseWheelToAncestorScrollViewer(object sender, MouseWheelEventArgs e)
        {
            if (sender is not DependencyObject target)
            {
                return;
            }

            ScrollViewer scrollViewer = FindAncestorScrollViewer(target);
            if (scrollViewer == null)
            {
                return;
            }

            e.Handled = true;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        }

        private static ScrollViewer FindAncestorScrollViewer(DependencyObject target)
        {
            DependencyObject current = GetVisualParent(target);
            while (current != null)
            {
                if (current is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }

                current = GetVisualParent(current);
            }

            return null;
        }

        private static DependencyObject GetVisualParent(DependencyObject target)
        {
            try
            {
                return VisualTreeHelper.GetParent(target);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static void OnTextInputFilterChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is not TextBox textBox)
            {
                return;
            }

            textBox.PreviewTextInput -= TextBox_PreviewTextInput;
            DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
            if ((TextInputFilterMode)e.NewValue != TextInputFilterMode.None)
            {
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                DataObject.AddPastingHandler(textBox, TextBox_Pasting);
            }
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = sender is not TextBox textBox || !IsAcceptedInput(textBox, e.Text);
        }

        private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!IsAcceptedInput(textBox, pastedText))
            {
                e.CancelCommand();
            }
        }

        private static bool IsAcceptedInput(TextBox textBox, string input)
        {
            string proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, input ?? string.Empty);

            return GetTextInputFilter(textBox) switch
            {
                TextInputFilterMode.Integer => proposed.All(char.IsDigit),
                TextInputFilterMode.Decimal => proposed.Count(ch => ch == '.') <= 1
                    && proposed.All(ch => char.IsDigit(ch) || ch == '.'),
                _ => true
            };
        }
    }
}
