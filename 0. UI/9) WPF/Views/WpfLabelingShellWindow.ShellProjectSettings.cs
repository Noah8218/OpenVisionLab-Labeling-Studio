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
        // Project settings helpers are shared by detection, training, and save flows.
        private string BuildLabelPathSummary()
        {
            IReadOnlyList<string> labelPaths = YoloAnnotationService.GetTargetLabelPaths(global.Data.LastSelectImageName, global.Data);
            return labelPaths.Count == 0
                ? "라벨 경로: 확인 안 됨"
                : $"라벨: {labelPaths[0]}";
        }

        private void EnsureProjectSettings()
        {
            global.Data.ProjectSettings ??= new LabelingProjectSettings();
            global.Data.ProjectSettings.EnsureDefaults();
        }

        private void ApplyProjectDatasetPurposeToWorkflow()
        {
            EnsureProjectSettings();
            LearningWorkflowViewModel?.ApplyDatasetPurpose(global.Data.ProjectSettings.DatasetPurpose);
            RefreshCanvasAnnotationToolScope();
            ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
            RefreshCanvasWorkflowContext();
            RefreshAnnotationVisibilityForDatasetPurpose();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            RefreshYoloTrainingStepCompletion();
        }

        private void ApplyWorkflowDatasetPurposeToProjectSettings()
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.DatasetPurpose = LearningWorkflowViewModel?.GetSelectedDatasetPurpose()
                ?? global.Data.ProjectSettings.DatasetPurpose;
        }

        private void ApplyDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
        {
            SynchronizeDatasetPurposeToCurrentProject(purpose);

            // CRecipe.Name replaces Data synchronously, but the previous
            // ListBox selection can still raise a queued WPF SelectionChanged
            // callback afterwards. Apply the same canonical recipe purpose once
            // bindings have settled so a previous recipe cannot repaint the new
            // recipe as segmentation/anomaly by mistake.
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => SynchronizeDatasetPurposeToCurrentProject(purpose)));
        }

        private void SynchronizeDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.DatasetPurpose = purpose;
            LearningWorkflowViewModel?.ApplyDatasetPurpose(purpose);

            // Recipe creation can run while the dataset-purpose ListBox still
            // holds the previous recipe's selection. Reconcile the view adapter
            // with the ViewModel before its SelectionChanged command can write
            // that stale purpose back into the newly created recipe.
            if (DatasetPurposeListBox != null)
            {
                BindingOperations.GetBindingExpression(
                    DatasetPurposeListBox,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.UpdateTarget();
            }

            RefreshCanvasAnnotationToolScope();
            ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
            RefreshCanvasWorkflowContext();
            RefreshAnnotationVisibilityForDatasetPurpose();
            RefreshShellDatasetContext();
        }
    }
}
