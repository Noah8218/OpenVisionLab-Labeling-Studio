using MahApps.Metro.IconPacks;
using OpenVisionLab;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the immutable item catalogs used by the learning workflow panel.
    /// The ViewModel owns selection and runtime state; this service owns the
    /// static workflow content that makes the panel readable and testable.
    /// </summary>
    public static class LearningWorkflowCatalogService
    {
        public static IReadOnlyList<WpfLearningModeItem> BuildLearningModes()
        {
            return new[]
            {
                new WpfLearningModeItem(WpfLearningMode.LabelingBasics, "\uB77C\uBCA8\uB9C1", PackIconMaterialKind.SchoolOutline, "\uC815\uB2F5 \uB77C\uBCA8\uC744 \uADF8\uB9AC\uB294 \uD750\uB984"),
                new WpfLearningModeItem(WpfLearningMode.ObjectDetection, "\uAC1D\uCCB4 \uD0D0\uC9C0", PackIconMaterialKind.ShapeSquareRoundedPlus, T("WpfLearningWorkflow.Catalog.Mode.ObjectDetection.ToolTip")),
                new WpfLearningModeItem(WpfLearningMode.Segmentation, "\uC138\uADF8\uBA58\uD14C\uC774\uC158", PackIconMaterialKind.ViewListOutline, "\uD3F4\uB9AC\uACE4\uACFC \uB9C8\uC2A4\uD06C \uB77C\uBCA8"),
                new WpfLearningModeItem(WpfLearningMode.AnomalyDetection, "\uC774\uC0C1 \uD0D0\uC9C0", PackIconMaterialKind.AlertCircleOutline, "\uC774\uBBF8\uC9C0 \uC804\uCCB4 \uC815\uC0C1/\uC774\uC0C1 \uD310\uC815"),
                new WpfLearningModeItem(WpfLearningMode.Train, "\uD559\uC2B5", PackIconMaterialKind.PlayCircleOutline, "\uB370\uC774\uD130\uC14B \uC900\uBE44\uC640 \uD559\uC2B5"),
                new WpfLearningModeItem(WpfLearningMode.Infer, "\uCD94\uB860", PackIconMaterialKind.RobotIndustrial, T("WpfLearningWorkflow.Catalog.Mode.Infer.ToolTip")),
                new WpfLearningModeItem(WpfLearningMode.Review, T("WpfLearningWorkflow.Catalog.Mode.Review.Text"), PackIconMaterialKind.CheckAll, T("WpfLearningWorkflow.Catalog.Mode.Review.ToolTip"))
            };
        }

        public static IReadOnlyList<WpfAnnotationToolItem> BuildAnnotationTools()
        {
            return new[]
            {
                new WpfAnnotationToolItem(WpfAnnotationTool.Select, "\uC120\uD0DD", PackIconMaterialKind.CursorDefaultOutline, T("WpfLearningWorkflow.ToolTip.Select.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Rectangle, "\uBC15\uC2A4", PackIconMaterialKind.VectorRectangle, T("WpfLearningWorkflow.ToolTip.Rectangle.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Ellipse, "\uC6D0/\uD0C0\uC6D0", PackIconMaterialKind.VectorEllipse, T("WpfLearningWorkflow.ToolTip.Ellipse.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Polygon, "\uD3F4\uB9AC\uACE4", PackIconMaterialKind.VectorPolygon, T("WpfLearningWorkflow.Catalog.Tool.Polygon.ToolTip")),
                new WpfAnnotationToolItem(WpfAnnotationTool.SmartMask, "SmartMask", PackIconMaterialKind.AutoFix, T("WpfLearningWorkflow.Catalog.Tool.SmartMask.ToolTip")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Brush, "\uBE0C\uB7EC\uC2DC", PackIconMaterialKind.BrushVariant, T("WpfLearningWorkflow.ToolTip.Brush.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Eraser, "\uC9C0\uC6B0\uAC1C", PackIconMaterialKind.EraserVariant, T("WpfLearningWorkflow.ToolTip.Eraser.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.PanZoom, "\uC774\uB3D9", PackIconMaterialKind.CursorMove, T("WpfLearningWorkflow.ToolTip.PanZoom.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Undo, "\uB418\uB3CC\uB9AC\uAE30", PackIconMaterialKind.Refresh, T("WpfLearningWorkflow.ToolTip.Undo.Description")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Redo, T("WpfAnnotationTool.Redo"), PackIconMaterialKind.Reload, T("WpfLearningWorkflow.Catalog.Tool.Redo.ToolTip")),
                new WpfAnnotationToolItem(WpfAnnotationTool.Delete, "\uC0AD\uC81C", PackIconMaterialKind.TrashCanOutline, T("WpfLearningWorkflow.ToolTip.Delete.Description"))
            };
        }

        public static IReadOnlyList<WpfLearningStepItem> BuildLearningSteps()
        {
            return new[]
            {
                new WpfLearningStepItem(WpfLearningStep.Sample, "\uC0D8\uD50C", PackIconMaterialKind.FolderImage),
                new WpfLearningStepItem(WpfLearningStep.Label, "\uB77C\uBCA8", PackIconMaterialKind.ShapeSquareRoundedPlus),
                new WpfLearningStepItem(WpfLearningStep.Infer, "\uCD94\uB860", PackIconMaterialKind.RobotIndustrial),
                new WpfLearningStepItem(WpfLearningStep.Review, T("WpfCanvas.Workflow.Step.Review"), PackIconMaterialKind.CheckAll),
                new WpfLearningStepItem(WpfLearningStep.Save, "\uC800\uC7A5", PackIconMaterialKind.ContentSaveOutline)
            };
        }

        public static IReadOnlyList<WpfTemplateWorkflowStepItem> BuildTemplateWorkflowSteps()
        {
            return new[]
            {
                new WpfTemplateWorkflowStepItem(
                    1,
                    "\uAE30\uC900 \uB77C\uBCA8 \uC120\uD0DD",
                    T("WpfLearningWorkflow.Catalog.Template.Reference.Detail"),
                    "\uC800\uC7A5 \uB77C\uBCA8",
                    PackIconMaterialKind.CursorDefaultClickOutline),
                new WpfTemplateWorkflowStepItem(
                    2,
                    "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uB77C\uBCA8 \uCD08\uC548",
                    "\uB2E4\uB978 \uC774\uBBF8\uC9C0\uC5D0\uC11C \uAC19\uC740 \uBAA8\uC591\uC744 \uCC3E\uACE0 \uADF8 \uC704\uCE58\uC5D0 \uC800\uC7A5 \uC804 \uB77C\uBCA8 \uCD08\uC548\uC744 \uCD94\uAC00\uD569\uB2C8\uB2E4.",
                    "\uC0C1\uB2E8/\uC624\uB978\uCABD",
                    PackIconMaterialKind.SelectionSearch),
                new WpfTemplateWorkflowStepItem(
                    3,
                    "\uC804\uCCB4 \uC774\uBBF8\uC9C0 \uC790\uB3D9 \uC800\uC7A5",
                    "\uC774\uBBF8\uC9C0 \uBAA9\uB85D\uC744 \uD55C \uBC88\uC529 \uB3CC\uBA70 \uB77C\uBCA8\uC774 \uC5C6\uB294 \uD56D\uBAA9\uC5D0\uB9CC \uC800\uC7A5\uD569\uB2C8\uB2E4.",
                    T("WpfLearningWorkflow.Catalog.Template.Batch.Location"),
                    PackIconMaterialKind.PlaylistCheck),
                new WpfTemplateWorkflowStepItem(
                    4,
                    T("WpfLearningWorkflow.Catalog.Template.Review.Title"),
                    "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD08\uC548\uC740 \uC704\uCE58\uB97C \uD655\uC778\uD55C \uB4A4 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB20C\uB7EC\uC57C \uBC18\uC601\uB429\uB2C8\uB2E4.",
                    "\uC800\uC7A5 \uC804 \uCD08\uC548",
                    PackIconMaterialKind.ContentSaveCheckOutline)
            };
        }

        public static IReadOnlyList<WpfFirstRunChecklistItem> BuildFirstRunSamplePathItems()
        {
            return new[]
            {
                new WpfFirstRunChecklistItem(
                    1,
                    "\uB370\uC774\uD130\uC14B",
                    "\uC0C8\uB85C \uB9CC\uB4E4\uAE30 \uB610\uB294 \uAE30\uC874 \uC5F4\uAE30",
                    T("WpfLearningWorkflow.Catalog.FirstRunSample.Dataset.ToolTip"),
                    PackIconMaterialKind.DatabasePlusOutline,
                    shortcutWorkflowStepOrder: 1,
                    shortcutActionText: "\uC2DC\uC791"),
                new WpfFirstRunChecklistItem(
                    2,
                    "\uC774\uBBF8\uC9C0",
                    "\uD3F4\uB354 \uC5F4\uACE0 \uD050 \uD655\uC778",
                    "\uC774\uBBF8\uC9C0\uAC00 \uBCF4\uC774\uBA74 \uCCAB \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD574 \uC791\uC5C5\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4.",
                    PackIconMaterialKind.FolderImage,
                    shortcutWorkflowStepOrder: 2,
                    shortcutActionText: "\uC5F4\uAE30"),
                new WpfFirstRunChecklistItem(
                    3,
                    "\uCCAB \uB77C\uBCA8",
                    "\uBC15\uC2A4 \uADF8\uB9B0 \uB4A4 \uB77C\uBCA8 \uC800\uC7A5",
                    "\uC800\uC7A5\uD574\uC57C \uBC15\uC2A4 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uC0DD\uC131\uB418\uACE0 \uD559\uC2B5 \uC810\uAC80\uC5D0 \uBC18\uC601\uD569\uB2C8\uB2E4.",
                    PackIconMaterialKind.ShapeSquareRoundedPlus,
                    shortcutWorkflowStepOrder: 4,
                    shortcutActionText: "\uB77C\uBCA8\uB9C1"),
                new WpfFirstRunChecklistItem(
                    4,
                    "\uD6C4\uBCF4 \uD655\uC778",
                    T("WpfLearningWorkflow.Catalog.FirstRunSample.Candidates.ActionText"),
                    T("WpfLearningWorkflow.Catalog.FirstRunSample.Candidates.ToolTip"),
                    PackIconMaterialKind.RobotIndustrial,
                    shortcutWorkflowStepOrder: 7,
                    shortcutActionText: T("WpfLearningWorkflow.Catalog.FirstRunSample.Candidates.Shortcut")),
                new WpfFirstRunChecklistItem(
                    5,
                    "\uD559\uC2B5 \uC900\uBE44",
                    "\uC810\uAC80 \uD1B5\uACFC \uB4A4 \uD559\uC2B5 \uC2DC\uC791",
                    T("WpfLearningWorkflow.Catalog.FirstRunSample.Training.ToolTip"),
                    PackIconMaterialKind.CheckAll,
                    shortcutWorkflowStepOrder: 5,
                    shortcutActionText: "\uC810\uAC80")
            };
        }

        public static IReadOnlyList<WpfFirstRunChecklistItem> BuildFirstRunChecklistItems()
        {
            return new[]
            {
                new WpfFirstRunChecklistItem(
                    1,
                    "\uB370\uC774\uD130\uC14B",
                    "\uC0C8\uB85C \uB9CC\uB4E4\uAE30 \uB610\uB294 \uAE30\uC874 \uC5F4\uAE30",
                    T("WpfLearningWorkflow.Catalog.FirstRun.Dataset.ToolTip"),
                    PackIconMaterialKind.DatabasePlusOutline),
                new WpfFirstRunChecklistItem(
                    2,
                    "\uC774\uBBF8\uC9C0",
                    "\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354 \uD655\uC778",
                    T("WpfLearningWorkflow.Catalog.FirstRun.Images.ToolTip"),
                    PackIconMaterialKind.FolderImage),
                new WpfFirstRunChecklistItem(
                    3,
                    "\uD074\uB798\uC2A4",
                    "OK, NG \uB4F1 \uB77C\uBCA8 \uC774\uB984 \uD655\uC778",
                    "\uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uBA3C\uC800 \uC815\uD574 \uB450\uBA74 \uC800\uC7A5 \uD6C4 \uD63C\uB780\uC774 \uC904\uC5B4\uB4ED\uB2C8\uB2E4.",
                    PackIconMaterialKind.TagMultipleOutline),
                new WpfFirstRunChecklistItem(
                    4,
                    "\uCCAB \uBC15\uC2A4",
                    "\uBC15\uC2A4 \uB3C4\uAD6C\uB85C 1\uAC1C \uADF8\uB9AC\uAE30",
                    "\uAC1D\uCCB4\uB97C \uD3EC\uD568\uD558\uB294 \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uC62C\uBC14\uB978 \uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD569\uB2C8\uB2E4.",
                    PackIconMaterialKind.ShapeSquareRoundedPlus),
                new WpfFirstRunChecklistItem(
                    5,
                    "\uB77C\uBCA8 \uC800\uC7A5",
                    "\uC800\uC7A5 \uD6C4 \uB2E4\uC74C \uC774\uBBF8\uC9C0",
                    "\uB77C\uBCA8 \uC800\uC7A5 \uBC84\uD2BC\uC744 \uB20C\uB7EC \uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uC815\uB2F5\uC744 \uD30C\uC77C\uC5D0 \uBC18\uC601\uD569\uB2C8\uB2E4.",
                    PackIconMaterialKind.ContentSaveOutline),
                new WpfFirstRunChecklistItem(
                    6,
                    "\uD559\uC2B5 \uC900\uBE44",
                    "\uB370\uC774\uD130\uC14B \uC810\uAC80\uC73C\uB85C \uBD80\uC871\uD55C \uD56D\uBAA9 \uD655\uC778",
                    "\uD559\uC2B5 \uC2DC\uC791 \uC804\uC5D0 \uB77C\uBCA8, \uD074\uB798\uC2A4, \uBD84\uD560 \uC0C1\uD0DC\uB97C \uD55C \uBC88\uC5D0 \uD655\uC778\uD569\uB2C8\uB2E4.",
                    PackIconMaterialKind.CheckAll)
            };
        }

        public static IReadOnlyList<string> BuildTutorialChecklistItems()
        {
            return new[]
            {
                "\uB370\uC774\uD130\uC14B\uC744 \uBA3C\uC800 \uB9CC\uB4E4\uACE0 \uC800\uC7A5 \uC704\uCE58\uC640 \uD559\uC2B5 \uBAA9\uC801\uC744 \uC815\uD569\uB2C8\uB2E4.",
                T("WpfLearningWorkflow.Catalog.Tutorial.Images"),
                "\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD0ED\uC5D0\uC11C OK, NG\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uB4F1\uB85D\uD569\uB2C8\uB2E4.",
                "\uB77C\uBCA8\uB9C1 \uBAA8\uB4DC\uC5D0\uC11C \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uC800\uC7A5\uD558\uC5EC \uBC15\uC2A4 \uB77C\uBCA8 \uD30C\uC77C\uC744 \uB9CC\uB4ED\uB2C8\uB2E4.",
                "\uB370\uC774\uD130\uC14B \uC810\uAC80\uC73C\uB85C \uB77C\uBCA8, \uD074\uB798\uC2A4, \uD559\uC2B5 \uC124\uC815\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.",
                "\uBAA8\uB378 \uD559\uC2B5\uC744 \uC2E4\uD589\uD558\uACE0 \uC644\uB8CC \uD6C4 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC744 \uC801\uC6A9\uD569\uB2C8\uB2E4.",
                T("WpfLearningWorkflow.Catalog.Tutorial.Inference")
            };
        }

        public static IReadOnlyList<WpfYoloTrainingWorkflowStepItem> BuildYoloTrainingWorkflowSteps()
        {
            return new[]
            {
                new WpfYoloTrainingWorkflowStepItem(
                    1,
                    "\uB370\uC774\uD130\uC14B \uB9CC\uB4E4\uAE30",
                    "\uD559\uC2B5 \uBAA9\uC801, \uC800\uC7A5 \uC704\uCE58, \uAE30\uBCF8 \uD074\uB798\uC2A4\uB97C \uC815\uD574 \uB370\uC774\uD130\uC14B\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4.",
                    "\uB370\uC774\uD130\uC14B \uB9CC\uB4E4\uAE30 \uCC3D\uC5D0\uC11C \uD3F4\uB354 \uAD6C\uC870\uC640 \uD074\uB798\uC2A4\uB97C \uC900\uBE44\uD558\uC138\uC694.",
                    PackIconMaterialKind.FolderImage),
                new WpfYoloTrainingWorkflowStepItem(
                    2,
                    "\uC774\uBBF8\uC9C0 \uBD88\uB7EC\uC624\uAE30",
                    T("WpfLearningWorkflow.Catalog.YoloTraining.Images.Detail"),
                    T("WpfLearningWorkflow.Catalog.YoloTraining.Images.Completion"),
                    PackIconMaterialKind.ImageMultipleOutline),
                new WpfYoloTrainingWorkflowStepItem(
                    3,
                    "\uD074\uB798\uC2A4 \uB4F1\uB85D",
                    "OK, NG, defect\uCC98\uB7FC \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uBA3C\uC800 \uB9CC\uB4ED\uB2C8\uB2E4.",
                    "\uC624\uB978\uCABD \uD074\uB798\uC2A4 \uD0ED\uC5D0\uC11C \uBAA9\uB85D\uACFC \uC800\uC7A5 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.",
                    PackIconMaterialKind.TagMultipleOutline),
                new WpfYoloTrainingWorkflowStepItem(
                    4,
                    "\uBC15\uC2A4 \uB77C\uBCA8\uB9C1",
                    "\uAC01 \uC774\uBBF8\uC9C0\uC5D0\uC11C \uAC1D\uCCB4\uB97C \uBC15\uC2A4\uB85C \uADF8\uB9AC\uACE0 \uD074\uB798\uC2A4\uB97C \uBD99\uC785\uB2C8\uB2E4.",
                    "\uB77C\uBCA8 \uC218\uAC00 \uB298\uACE0 \uC800\uC7A5\uD558\uBA74 \uBC15\uC2A4 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uC0DD\uC131\uB429\uB2C8\uB2E4.",
                    PackIconMaterialKind.ShapeSquareRoundedPlus),
                new WpfYoloTrainingWorkflowStepItem(
                    5,
                    "\uC800\uC7A5\uACFC \uB370\uC774\uD130\uC14B \uC810\uAC80",
                    "\uB77C\uBCA8\uC774 \uBE60\uC9C4 \uC774\uBBF8\uC9C0, \uD074\uB798\uC2A4, \uD559\uC2B5 \uC124\uC815\uC744 \uAC80\uC0AC\uD569\uB2C8\uB2E4.",
                    T("WpfLearningWorkflow.Catalog.YoloTraining.Check.Completion"),
                    PackIconMaterialKind.CheckAll),
                new WpfYoloTrainingWorkflowStepItem(
                    6,
                    "YOLO \uBAA8\uB378 \uD559\uC2B5",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.Catalog.YoloTraining.Settings.Action"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.Catalog.YoloTraining.Train.Result"),
                    PackIconMaterialKind.PlayCircleOutline),
                new WpfYoloTrainingWorkflowStepItem(
                    7,
                    T("WpfLearningWorkflow.Catalog.YoloTraining.Review.Title"),
                    T("WpfLearningWorkflow.Catalog.YoloTraining.Review.Detail"),
                    "\uACB0\uACFC\uAC00 \uB9DE\uC73C\uBA74 \uB77C\uBCA8\uB85C \uD655\uC815\uD558\uACE0, \uD2C0\uB9AC\uBA74 \uB370\uC774\uD130\uB97C \uCD94\uAC00\uD569\uB2C8\uB2E4.",
                    PackIconMaterialKind.RobotIndustrial)
            };
        }

        public static IReadOnlyList<WpfYoloDatasetStructureItem> BuildYoloDatasetStructureItems()
        {
            return new[]
            {
                new WpfYoloDatasetStructureItem(
                    "data.yaml",
                    T("WpfLearningWorkflow.Structure.DataYaml.Value"),
                    T("WpfLearningWorkflow.Structure.DataYaml.Detail"),
                    PackIconMaterialKind.FileCodeOutline),
                new WpfYoloDatasetStructureItem(
                    "images",
                    T("WpfLearningWorkflow.Structure.Images.Value"),
                    T("WpfLearningWorkflow.Structure.Images.Detail"),
                    PackIconMaterialKind.FolderImage),
                new WpfYoloDatasetStructureItem(
                    "labels",
                    T("WpfLearningWorkflow.Structure.Labels.Value"),
                    T("WpfLearningWorkflow.Structure.Labels.Detail"),
                    PackIconMaterialKind.FileDocumentOutline),
                new WpfYoloDatasetStructureItem(
                    T("WpfLearningWorkflow.Structure.TxtLine.Title"),
                    T("WpfLearningWorkflow.Structure.TxtLine.Value"),
                    T("WpfLearningWorkflow.Structure.TxtLine.Detail"),
                    PackIconMaterialKind.FormatListNumbered)
            };
        }

        public static IReadOnlyList<WpfDatasetDashboardMetricItem> BuildInitialDatasetDashboardMetrics()
        {
            return new[]
            {
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Images.Title",
                    "-",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Before",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting",
                    PackIconMaterialKind.FolderImage,
                    isProblem: false,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenImages),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Progress.Title",
                    "-",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Before",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting",
                    PackIconMaterialKind.ProgressClock,
                    isProblem: false,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingProgress),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Initial.Labels.Title",
                    "-",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Before",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting",
                    PackIconMaterialKind.ShapeSquareRoundedPlus,
                    isProblem: false,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingTool),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Split.Title",
                    "-",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Before",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting",
                    PackIconMaterialKind.CheckAll,
                    isProblem: false,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Class.Title",
                    "-",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Before",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting",
                    PackIconMaterialKind.TagMultipleOutline,
                    isProblem: false,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenClassCatalog)
            };
        }

        private static string T(string key) => OpenVisionLanguageService.T(key);
    }

    [Obsolete("Use LearningWorkflowCatalogService.", false)]
    public static class WpfLearningWorkflowCatalogService
    {
        public static IReadOnlyList<WpfLearningModeItem> BuildLearningModes()
            => LearningWorkflowCatalogService.BuildLearningModes();

        public static IReadOnlyList<WpfAnnotationToolItem> BuildAnnotationTools()
            => LearningWorkflowCatalogService.BuildAnnotationTools();

        public static IReadOnlyList<WpfLearningStepItem> BuildLearningSteps()
            => LearningWorkflowCatalogService.BuildLearningSteps();

        public static IReadOnlyList<WpfTemplateWorkflowStepItem> BuildTemplateWorkflowSteps()
            => LearningWorkflowCatalogService.BuildTemplateWorkflowSteps();

        public static IReadOnlyList<WpfFirstRunChecklistItem> BuildFirstRunSamplePathItems()
            => LearningWorkflowCatalogService.BuildFirstRunSamplePathItems();

        public static IReadOnlyList<WpfFirstRunChecklistItem> BuildFirstRunChecklistItems()
            => LearningWorkflowCatalogService.BuildFirstRunChecklistItems();

        public static IReadOnlyList<string> BuildTutorialChecklistItems()
            => LearningWorkflowCatalogService.BuildTutorialChecklistItems();

        public static IReadOnlyList<WpfYoloTrainingWorkflowStepItem> BuildYoloTrainingWorkflowSteps()
            => LearningWorkflowCatalogService.BuildYoloTrainingWorkflowSteps();

        public static IReadOnlyList<WpfYoloDatasetStructureItem> BuildYoloDatasetStructureItems()
            => LearningWorkflowCatalogService.BuildYoloDatasetStructureItems();

        public static IReadOnlyList<WpfDatasetDashboardMetricItem> BuildInitialDatasetDashboardMetrics()
            => LearningWorkflowCatalogService.BuildInitialDatasetDashboardMetrics();
    }
}
