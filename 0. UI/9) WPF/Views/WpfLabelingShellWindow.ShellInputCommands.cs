using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Views;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using OpenVisionLab.Mvvm.Behaviors;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUiApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme;
using WpfUiApplicationThemeManager = Wpf.Ui.Appearance.ApplicationThemeManager;
using WpfUiFluentWindow = Wpf.Ui.Controls.FluentWindow;
using WpfUiWindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Shell-level keyboard shortcuts stay outside the constructor/field file so command routing is easier to audit.
        private void ExecuteShellPreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null
                || (e.Modifiers & ModifierKeys.Control) != ModifierKeys.Control
                || IsTextEditingElement(e.OriginalSource))
            {
                return;
            }

            if (e.Key == Key.Z)
            {
                e.Handled = UndoWpfAnnotationHistory();
                return;
            }

            if (e.Key == Key.Y)
            {
                e.Handled = RedoWpfAnnotationHistory();
            }
        }

        private static bool IsTextEditingElement(object source)
        {
            return source is TextBox
                || source is ComboBox
                || source is System.Windows.Controls.Primitives.TextBoxBase;
        }
    }
}
