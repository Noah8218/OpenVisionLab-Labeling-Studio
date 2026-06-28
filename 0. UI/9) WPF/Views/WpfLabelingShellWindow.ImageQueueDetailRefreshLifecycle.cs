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
        // Queue detail cancellation is lifecycle plumbing; it should not obscure shell composition.
        private void CancelImageQueueDetailRefresh(bool waitForCompletion)
        {
            CancellationTokenSource cts = imageQueueDetailLoadCts;
            Task detailTask = imageQueueDetailLoadTask;
            if (cts == null)
            {
                return;
            }

            cts.Cancel();
            if (waitForCompletion)
            {
                WaitForImageQueueDetailRefresh(detailTask);
            }

            if (detailTask == null || detailTask.IsCompleted)
            {
                cts.Dispose();
            }
            else
            {
                detailTask.ContinueWith(_ => cts.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            if (ReferenceEquals(cts, imageQueueDetailLoadCts))
            {
                imageQueueDetailLoadCts = null;
            }

            if (ReferenceEquals(detailTask, imageQueueDetailLoadTask))
            {
                imageQueueDetailLoadTask = Task.CompletedTask;
            }
        }

        private void WaitForImageQueueDetailRefresh(Task detailTask)
        {
            if (detailTask == null || detailTask.IsCompleted)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    detailTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                }

                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!detailTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                // Detail refresh resumes on the UI dispatcher; pump briefly so close can release image file handles.
                var frame = new DispatcherFrame();
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            if (detailTask.IsFaulted)
            {
                _ = detailTask.Exception;
            }
        }
    }
}
