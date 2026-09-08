using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the pending state and geometry policy for polygon boundary editing.
    /// The Shell remains responsible for input, history, rendering and status
    /// projection; this owner keeps the edit lifecycle independent of WPF.
    /// </summary>
    public sealed class PolygonBoundaryEditWorkflowService
    {
        private readonly IntelligentScissorsService intelligentScissorsService;
        private LabelingSegmentationObject intelligentScissorsSource;
        private int intelligentScissorsSourceIndex = -1;
        private WpfIntelligentScissorsPlan intelligentScissorsPlan;
        private LabelingSegmentationObject polygonVertexSource;
        private int polygonVertexSourceIndex = -1;
        private WpfPolygonVertexEditMode? polygonVertexEditMode;

        public PolygonBoundaryEditWorkflowService(IntelligentScissorsService intelligentScissorsService = null)
        {
            this.intelligentScissorsService = intelligentScissorsService ?? new IntelligentScissorsService();
        }

        public bool IsIntelligentScissorsPending => intelligentScissorsSource != null;

        public bool HasIntelligentScissorsPreview => intelligentScissorsPlan != null;

        public int IntelligentScissorsSourceIndex => intelligentScissorsSourceIndex;

        public LabelingSegmentationObject IntelligentScissorsSource => intelligentScissorsSource;

        public WpfIntelligentScissorsPlan IntelligentScissorsPlan => intelligentScissorsPlan;

        public bool IsPolygonVertexEditPending => polygonVertexEditMode.HasValue;

        public int PolygonVertexSourceIndex => polygonVertexSourceIndex;

        public LabelingSegmentationObject PolygonVertexSource => polygonVertexSource;

        public WpfPolygonVertexEditMode? PolygonVertexEditMode => polygonVertexEditMode;

        public void BeginIntelligentScissors(int sourceIndex, LabelingSegmentationObject source)
        {
            if (source == null || source.IsRasterMask || sourceIndex < 0)
            {
                throw new ArgumentException("A valid manual polygon source is required.", nameof(source));
            }

            CancelIntelligentScissors();
            intelligentScissorsSourceIndex = sourceIndex;
            intelligentScissorsSource = source;
            intelligentScissorsPlan = null;
        }

        public bool TryPreviewIntelligentScissors(
            Bitmap image,
            Point edgeHitPoint,
            Size imageSize,
            int edgeHitTolerancePixels,
            out WpfIntelligentScissorsPlan plan,
            out string error)
        {
            plan = null;
            if (!IsIntelligentScissorsPending)
            {
                error = "경계 추종 편집을 먼저 시작하세요.";
                return false;
            }

            if (!intelligentScissorsService.TryBuildPlan(
                image,
                intelligentScissorsSource,
                edgeHitPoint,
                imageSize,
                edgeHitTolerancePixels,
                out plan,
                out error))
            {
                intelligentScissorsPlan = null;
                return false;
            }

            intelligentScissorsPlan = plan;
            return true;
        }

        public bool TryApplyIntelligentScissors(
            IReadOnlyList<LabelingSegmentationObject> currentSegments,
            Size imageSize,
            out int sourceIndex,
            out LabelingSegmentationObject source,
            out Rectangle changedBounds,
            out string error)
        {
            sourceIndex = -1;
            source = null;
            changedBounds = Rectangle.Empty;
            if (!TryResolveIntelligentScissorsSource(currentSegments, out sourceIndex, out source))
            {
                error = "선택한 객체가 변경되어 경계 추종을 취소했습니다.";
                return false;
            }

            if (intelligentScissorsPlan == null)
            {
                error = "적용할 경계 미리보기가 없습니다.";
                return false;
            }

            return intelligentScissorsService.TryApplyPlan(
                source,
                intelligentScissorsPlan,
                imageSize,
                out changedBounds,
                out error);
        }

        public bool TryResolveIntelligentScissorsSource(
            IReadOnlyList<LabelingSegmentationObject> currentSegments,
            out int sourceIndex,
            out LabelingSegmentationObject source)
        {
            sourceIndex = intelligentScissorsSourceIndex;
            source = intelligentScissorsSource;
            return source?.IsRasterMask == false
                && currentSegments != null
                && sourceIndex >= 0
                && sourceIndex < currentSegments.Count
                && ReferenceEquals(currentSegments[sourceIndex], source);
        }

        public bool CancelIntelligentScissors()
        {
            bool wasPending = IsIntelligentScissorsPending;
            intelligentScissorsSource = null;
            intelligentScissorsSourceIndex = -1;
            intelligentScissorsPlan = null;
            return wasPending;
        }

        public void BeginPolygonVertexEdit(
            int sourceIndex,
            LabelingSegmentationObject source,
            WpfPolygonVertexEditMode mode)
        {
            if (source == null || source.IsRasterMask || sourceIndex < 0)
            {
                throw new ArgumentException("A valid manual polygon source is required.", nameof(source));
            }

            CancelPolygonVertexEdit();
            polygonVertexSourceIndex = sourceIndex;
            polygonVertexSource = source;
            polygonVertexEditMode = mode;
        }

        public bool TryApplyPolygonVertexEdit(
            IReadOnlyList<LabelingSegmentationObject> currentSegments,
            Point imagePoint,
            Size imageSize,
            int hitTolerancePixels,
            out int sourceIndex,
            out LabelingSegmentationObject source,
            out WpfPolygonVertexEditMode mode,
            out Rectangle changedBounds,
            out string error)
        {
            sourceIndex = -1;
            source = null;
            mode = default;
            changedBounds = Rectangle.Empty;
            if (!TryResolvePolygonVertexSource(currentSegments, out sourceIndex, out source)
                || !polygonVertexEditMode.HasValue)
            {
                error = "선택한 객체가 변경되어 정점 편집을 취소했습니다.";
                return false;
            }

            mode = polygonVertexEditMode.Value;
            if (mode == WpfPolygonVertexEditMode.Insert)
            {
                return PolygonAnnotationService.TryInsertPoint(
                    source,
                    imagePoint,
                    imageSize,
                    hitTolerancePixels,
                    out int _,
                    out changedBounds,
                    out error);
            }

            return PolygonAnnotationService.TryDeletePoint(
                source,
                imagePoint,
                imageSize,
                hitTolerancePixels,
                out int _,
                out changedBounds,
                out error);
        }

        public bool TryResolvePolygonVertexSource(
            IReadOnlyList<LabelingSegmentationObject> currentSegments,
            out int sourceIndex,
            out LabelingSegmentationObject source)
        {
            sourceIndex = polygonVertexSourceIndex;
            source = polygonVertexSource;
            return source?.IsRasterMask == false
                && currentSegments != null
                && sourceIndex >= 0
                && sourceIndex < currentSegments.Count
                && ReferenceEquals(currentSegments[sourceIndex], source);
        }

        public bool CancelPolygonVertexEdit()
        {
            bool wasPending = IsPolygonVertexEditPending;
            polygonVertexSource = null;
            polygonVertexSourceIndex = -1;
            polygonVertexEditMode = null;
            return wasPending;
        }
    }
}
