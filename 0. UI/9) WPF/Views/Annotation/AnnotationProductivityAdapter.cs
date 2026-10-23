using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OpenVisionLab;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns manual annotation duplication and previous-image label import.
    /// Geometry and cloning stay in existing services; the Shell supplies live state and presentation callbacks.
    /// </summary>
    internal sealed class AnnotationProductivityAdapter
    {
        private readonly AnnotationProductivityAdapterContext context;
        private bool isPreviousLabelImportRunning;
        private LabelingProjectData clipboardData;
        private Size clipboardImageSize;
        private Rectangle? clipboardRectangle;
        private string clipboardClassName;
        private LabelingSegmentationObject clipboardSegment;
        private bool isPasteRunning;

        internal AnnotationProductivityAdapter(AnnotationProductivityAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal bool TryDuplicateSelectedAnnotation()
        {
            context.CompleteMaskAnnotationStroke?.Invoke();
            context.FlushQueuedMaskStrokeCommits?.Invoke();
            WpfObjectReviewItemRef selectedItem = context.SelectedObjectReviewItemProvider?.Invoke();
            if (selectedItem == null)
            {
                context.SetYoloCommandStatus?.Invoke("복제할 저장 라벨을 먼저 선택하세요.", false);
                return false;
            }

            return selectedItem.Source switch
            {
                WpfObjectReviewSource.ManualRoi => TryDuplicateManualRoi(selectedItem.Index),
                WpfObjectReviewSource.ManualSegment => TryDuplicateManualSegment(selectedItem.Index),
                _ => RejectUnsupportedSelection()
            };
        }

        internal bool TryCopySelectedAnnotation()
        {
            if (isPasteRunning) return false;
            clipboardData = null; clipboardRectangle = null; clipboardSegment = null;
            if (context.IsLabelingStageActive?.Invoke() != true || context.IsOperationRunning?.Invoke() == true
                || context.HasWorkflowDraft?.Invoke() == true || context.IsAnnotationLoadBlocked?.Invoke() == true) return false;
            WpfObjectReviewItemRef item = context.SelectedObjectReviewItemProvider?.Invoke();
            if (item == null || !TryValidateMutation(item)) return false;
            LabelingProjectData data = context.DataProvider?.Invoke();
            Size size = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            if (data == null || size.IsEmpty) return false;
            if (item.Source == WpfObjectReviewSource.ManualRoi && item.Index >= 0 && item.Index < context.ManualRois.Count
                && ObjectReviewPresentationService.GetManualRoiShapeKind(context.ManualRoiShapeKinds, item.Index) == CanvasRoiShapeKind.Rectangle)
            {
                clipboardRectangle = context.ManualRois[item.Index];
                clipboardClassName = ObjectReviewPresentationService.GetManualRoiClassName(context.ManualRoiClassNames, item.Index);
            }
            else if (item.Source == WpfObjectReviewSource.ManualSegment && item.Index >= 0 && item.Index < context.ManualSegments.Count)
            {
                clipboardSegment = AnnotationHistoryService.CloneSegment(context.ManualSegments[item.Index]);
                clipboardClassName = FirstNonEmpty(clipboardSegment.ClassName, clipboardSegment.ClassItem?.Text);
            }
            else return false;
            clipboardData = data; clipboardImageSize = size;
            context.SetYoloCommandStatus?.Invoke(OpenVisionLanguageService.T("ShellClipboard.Copied"), false);
            return true;
        }

        internal bool TryPasteAnnotation()
        {
            if (isPasteRunning || clipboardData == null || context.IsLabelingStageActive?.Invoke() != true
                || context.IsOperationRunning?.Invoke() == true || context.HasWorkflowDraft?.Invoke() == true
                || context.IsAnnotationLoadBlocked?.Invoke() == true || context.IsAnomalyDatasetPurpose?.Invoke() == true) return false;
            LabelingProjectData data = context.DataProvider?.Invoke();
            Size size = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            LabelClass labelClass = data?.ClassNamedList?.FirstOrDefault(item => string.Equals(item.Text, clipboardClassName, StringComparison.OrdinalIgnoreCase));
            if (!ReferenceEquals(data, clipboardData) || size != clipboardImageSize || labelClass == null
                || (context.IsSegmentationDatasetPurpose?.Invoke() == true) != (clipboardSegment != null))
            {
                context.SetYoloCommandStatus?.Invoke(OpenVisionLanguageService.T("ShellClipboard.Incompatible"), false);
                return false;
            }
            LabelingSegmentationObject segment = clipboardSegment == null ? null
                : AnnotationProductivityService.CreateOffsetSegment(clipboardSegment, size, context.MaskAnnotationService);
            if (clipboardRectangle == null && segment == null) return false;
            isPasteRunning = true;
            try
            {
                context.RegisterAnnotationHistoryBeforeChange?.Invoke(OpenVisionLanguageService.T("ShellClipboard.Paste"));
                WpfObjectReviewItemRef added;
                if (clipboardRectangle.HasValue)
                {
                    context.ManualRois.Add(AnnotationProductivityService.CreateOffsetRectangle(clipboardRectangle.Value, size));
                    context.ManualRoiClassNames.Add(labelClass.Text); context.ManualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle); context.ManualRoiOverlayIds.Add(string.Empty);
                    added = WpfObjectReviewItemRef.Manual(context.ManualRois.Count - 1);
                    context.RedrawReviewRois?.Invoke();
                }
                else
                {
                    segment.ClassItem = labelClass; segment.ClassName = labelClass.Text;
                    segment.ZOrder = SegmentationZOrderService.GetNextZOrder(context.ManualSegments); context.ManualSegments.Add(segment);
                    added = WpfObjectReviewItemRef.ManualSegment(context.ManualSegments.Count - 1);
                    context.RefreshPolygonOverlays?.Invoke();
                }
                context.RefreshObjectListWithSelection?.Invoke(added); context.ShowSavedLabelsWorkflowView?.Invoke();
                context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);
                context.SetYoloCommandStatus?.Invoke(OpenVisionLanguageService.T("ShellClipboard.Pasted"), false);
                return true;
            }
            finally { isPasteRunning = false; }
        }

        internal bool TryImportPreviousLabels()
        {
            if (isPreviousLabelImportRunning || context.IsLabelingStageActive?.Invoke() != true) return false;
            if (context.IsOperationRunning?.Invoke() == true || context.HasWorkflowDraft?.Invoke() == true)
                return ReportImportFailure("WpfCanvas.PreviousLabels.FinishWork");
            if (context.IsAnnotationLoadBlocked?.Invoke() == true)
                return ReportImportFailure("WpfCanvas.PreviousLabels.LoadBlocked");
            if (context.IsAnomalyDatasetPurpose?.Invoke() == true)
                return ReportImportFailure("WpfCanvas.PreviousLabels.AnnotationOnly");

            Size imageSize = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            LabelingProjectData data = context.DataProvider?.Invoke();
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || data == null)
                return ReportImportFailure("WpfCanvas.PreviousLabels.OpenImage");

            isPreviousLabelImportRunning = true;
            try
            {
                string previousPath;
                var rectangles = new List<KeyValuePair<string, Rectangle>>();
                var segments = new List<LabelingSegmentationObject>();
                try
                {
                    previousPath = context.PreviousImagePathProvider?.Invoke() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(previousPath))
                        return ReportImportFailure("WpfCanvas.PreviousLabels.Empty");

                    if (context.IsSegmentationDatasetPurpose?.Invoke() == true)
                    {
                        IReadOnlyDictionary<string, List<LabelingSegmentationObject>> savedSegments =
                            YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(previousPath, data.ClassNamedList, data, imageSize);
                        foreach (KeyValuePair<string, List<LabelingSegmentationObject>> group in savedSegments)
                        {
                            LabelClass classItem = data.ClassNamedList?.FirstOrDefault(item => string.Equals(item?.Text, group.Key, StringComparison.OrdinalIgnoreCase));
                            if (classItem == null) throw new InvalidDataException(OpenVisionLanguageService.T("WpfCanvas.PreviousLabels.UnknownClass"));
                            foreach (LabelingSegmentationObject saved in group.Value)
                            {
                                LabelingSegmentationObject segment = AnnotationHistoryService.CloneSegment(saved);
                                segment.ObjectId = string.Empty;
                                segment.ComponentIndex = -1;
                                segment.LastStructuralOperation = string.Empty;
                                segment.ClassItem = classItem;
                                segment.ClassName = classItem.Text;
                                segment.Selected = false;
                                if (segment.IsRasterMask)
                                {
                                    segment.RenderVersion = Math.Max(1, segment.RenderVersion);
                                    segment.RenderDirtyBounds = segment.Bounds;
                                }
                                segments.Add(segment);
                            }
                        }
                        segments = segments.OrderBy(segment => segment.ZOrder).ToList();
                    }
                    else
                    {
                        YoloAnnotationLoadResult saved = YoloAnnotationService.LoadAnnotationRectanglesForImageWithDiagnostics(previousPath, data.ClassNamedList, data, imageSize);
                        if (saved.HasErrors) throw new InvalidDataException(saved.ErrorSummary);
                        rectangles.AddRange(saved.Annotations.SelectMany(group => group.Value.Select(rectangle => new KeyValuePair<string, Rectangle>(group.Key, rectangle))));
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is InvalidDataException
                    || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException
                    || exception is YoloImageIdentityCollisionException || exception is System.Runtime.InteropServices.ExternalException)
                {
                    string message = string.Format(OpenVisionLanguageService.T("WpfCanvas.PreviousLabels.ReadFailed"), exception.Message);
                    context.SetYoloCommandStatus?.Invoke(message, false);
                    context.AppendLog?.Invoke(message);
                    return false;
                }

                int count = rectangles.Count + segments.Count;
                if (count == 0) return ReportImportFailure("WpfCanvas.PreviousLabels.Empty");

                context.RegisterAnnotationHistoryBeforeChange?.Invoke(OpenVisionLanguageService.T("WpfCanvas.PreviousLabels.Name"));
                foreach (KeyValuePair<string, Rectangle> rectangle in rectangles)
                {
                    context.ManualRois.Add(rectangle.Value);
                    context.ManualRoiClassNames.Add(rectangle.Key);
                    context.ManualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
                    context.ManualRoiOverlayIds.Add(string.Empty);
                }
                int nextZOrder = SegmentationZOrderService.GetNextZOrder(context.ManualSegments);
                foreach (LabelingSegmentationObject segment in segments)
                {
                    segment.ZOrder = nextZOrder++;
                    context.ManualSegments.Add(segment);
                }

                WpfObjectReviewItemRef imported = rectangles.Count > 0
                    ? WpfObjectReviewItemRef.Manual(context.ManualRois.Count - rectangles.Count)
                    : WpfObjectReviewItemRef.ManualSegment(context.ManualSegments.Count - segments.Count);
                context.ShowSavedLabelsWorkflowView?.Invoke();
                if (rectangles.Count > 0) context.RedrawReviewRois?.Invoke();
                if (segments.Count > 0) context.RefreshPolygonOverlays?.Invoke();
                context.RefreshObjectListWithSelection?.Invoke(imported);
                context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);
                string status = string.Format(OpenVisionLanguageService.T("WpfCanvas.PreviousLabels.Imported"), Path.GetFileName(previousPath), count);
                context.SetYoloCommandStatus?.Invoke(status, false);
                context.AppendLog?.Invoke(status);
                return true;
            }
            finally
            {
                isPreviousLabelImportRunning = false;
            }
        }

        private bool ReportImportFailure(string key)
        {
            string message = OpenVisionLanguageService.T(key);
            context.SetYoloCommandStatus?.Invoke(message, false);
            context.AppendLog?.Invoke(message);
            return false;
        }

        internal bool TryDuplicateManualRoi(int sourceIndex)
        {
            if (context.ManualRois == null || sourceIndex < 0 || sourceIndex >= context.ManualRois.Count)
            {
                return false;
            }

            if (!TryValidateMutation(WpfObjectReviewItemRef.Manual(sourceIndex)))
            {
                return false;
            }

            if (ObjectReviewPresentationService.GetManualRoiShapeKind(context.ManualRoiShapeKinds, sourceIndex)
                != CanvasRoiShapeKind.Rectangle)
            {
                context.SetYoloCommandStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.AnnotationProductivityAdapter.DuplicateSupportedShapes"), false);
                return false;
            }

            context.RegisterAnnotationHistoryBeforeChange?.Invoke("라벨 복제");
            Rectangle duplicate = AnnotationProductivityService.CreateOffsetRectangle(
                context.ManualRois[sourceIndex],
                context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty);
            string className = ObjectReviewPresentationService.GetManualRoiClassName(
                context.ManualRoiClassNames,
                sourceIndex);
            context.ManualRois.Add(duplicate);
            context.ManualRoiClassNames?.Add(className);
            context.ManualRoiShapeKinds?.Add(ObjectReviewPresentationService.GetManualRoiShapeKind(
                context.ManualRoiShapeKinds,
                sourceIndex));
            context.ManualRoiOverlayIds?.Add(string.Empty);

            int duplicateIndex = context.ManualRois.Count - 1;
            context.RedrawReviewRois?.Invoke();
            context.RefreshObjectListWithSelection?.Invoke(WpfObjectReviewItemRef.Manual(duplicateIndex));
            context.ShowSavedLabelsWorkflowView?.Invoke();
            context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);
            context.SetYoloCommandStatus?.Invoke(
                $"라벨 복제: {className} / x={duplicate.X}, y={duplicate.Y}, w={duplicate.Width}, h={duplicate.Height}",
                false);
            context.AppendLog?.Invoke($"Duplicated manual ROI: source={sourceIndex + 1}, target={duplicateIndex + 1}, class={className}");
            return true;
        }

        internal bool TryDuplicateManualSegment(int sourceIndex)
        {
            if (context.ManualSegments == null || sourceIndex < 0 || sourceIndex >= context.ManualSegments.Count)
            {
                return false;
            }

            if (!TryValidateMutation(WpfObjectReviewItemRef.ManualSegment(sourceIndex)))
            {
                return false;
            }

            LabelingSegmentationObject duplicate = AnnotationProductivityService.CreateOffsetSegment(
                context.ManualSegments[sourceIndex],
                context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty,
                context.MaskAnnotationService);
            if (duplicate == null)
            {
                return false;
            }

            context.RegisterAnnotationHistoryBeforeChange?.Invoke("라벨 복제");
            duplicate.ZOrder = SegmentationZOrderService.GetNextZOrder(context.ManualSegments);
            context.ManualSegments.Add(duplicate);
            int duplicateIndex = context.ManualSegments.Count - 1;
            context.RefreshObjectListWithSelection?.Invoke(WpfObjectReviewItemRef.ManualSegment(duplicateIndex));
            context.RefreshPolygonOverlays?.Invoke();
            context.ShowSavedLabelsWorkflowView?.Invoke();
            context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);

            string shapeName = duplicate.IsRasterMask ? "마스크" : "폴리곤";
            string className = FirstNonEmpty(duplicate.ClassName, duplicate.ClassItem?.Text, "Defect");
            context.SetYoloCommandStatus?.Invoke($"라벨 복제: {shapeName} / {className}", false);
            context.AppendLog?.Invoke($"Duplicated manual segment: source={sourceIndex + 1}, target={duplicateIndex + 1}, shape={shapeName}, class={className}");
            return true;
        }

        private bool TryValidateMutation(WpfObjectReviewItemRef item)
        {
            string error = context.MutationErrorProvider?.Invoke(item, true) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(error))
            {
                return true;
            }

            context.SetYoloCommandStatus?.Invoke(error, false);
            return false;
        }

        private bool RejectUnsupportedSelection()
        {
            context.SetYoloCommandStatus?.Invoke("수동 박스, 폴리곤, 브러시 마스크만 복제할 수 있습니다.", false);
            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }

    internal sealed class AnnotationProductivityAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> PreviousImagePathProvider { get; init; }
        internal Func<bool> IsLabelingStageActive { get; init; }
        internal Func<bool> IsOperationRunning { get; init; }
        internal Func<bool> HasWorkflowDraft { get; init; }
        internal Func<bool> IsAnnotationLoadBlocked { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Func<bool> IsSegmentationDatasetPurpose { get; init; }
        internal Func<WpfObjectReviewItemRef> SelectedObjectReviewItemProvider { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal List<Rectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal MaskAnnotationService MaskAnnotationService { get; init; }
        internal Func<WpfObjectReviewItemRef, bool, string> MutationErrorProvider { get; init; }
        internal Action<string> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
