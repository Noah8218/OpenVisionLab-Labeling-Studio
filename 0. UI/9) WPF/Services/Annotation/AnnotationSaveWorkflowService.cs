using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns annotation save intent and active-image validation while the Shell
    /// keeps live-state capture and post-save UI/recovery side effects.
    /// LabelingAnnotationPersistence remains the durable file transaction owner.
    /// </summary>
    public sealed class AnnotationSaveWorkflowService
    {
        public AnnotationSaveResult Save(AnnotationSaveRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!HasActiveImage(request.ActiveImage))
            {
                return AnnotationSaveResult.Failed(request.SavedObjectCount);
            }

            bool saved = LabelingAnnotationPersistence.SaveCurrentWithAdditionalArtifacts(
                request.ActiveImage,
                request.RoisByClass,
                request.SegmentsByClass,
                request.Data,
                request.SaveAdditionalArtifacts);
            return new AnnotationSaveResult(saved, request.SavedObjectCount);
        }

        public AnnotationSaveResult SaveEmpty(
            LabelingImageSnapshot activeImage,
            LabelingProjectData data,
            Func<bool> saveAdditionalArtifacts)
            => Save(AnnotationSaveRequest.CreateEmpty(
                activeImage,
                data,
                saveAdditionalArtifacts));

        private static bool HasActiveImage(LabelingImageSnapshot activeImage)
            => activeImage?.Image != null && !activeImage.ImageSize.IsEmpty;
    }

    public sealed class AnnotationSaveRequest
    {
        public AnnotationSaveRequest(
            LabelingImageSnapshot activeImage,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> roisByClass,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            LabelingProjectData data,
            int savedObjectCount,
            Func<bool> saveAdditionalArtifacts)
        {
            ActiveImage = activeImage;
            RoisByClass = roisByClass;
            SegmentsByClass = segmentsByClass;
            Data = data;
            SavedObjectCount = Math.Max(0, savedObjectCount);
            SaveAdditionalArtifacts = saveAdditionalArtifacts;
        }

        public LabelingImageSnapshot ActiveImage { get; }

        public IReadOnlyDictionary<string, List<AnnotationRectangleObject>> RoisByClass { get; }

        public IReadOnlyDictionary<string, List<LabelingSegmentationObject>> SegmentsByClass { get; }

        public LabelingProjectData Data { get; }

        public int SavedObjectCount { get; }

        public Func<bool> SaveAdditionalArtifacts { get; }

        internal static AnnotationSaveRequest CreateEmpty(
            LabelingImageSnapshot activeImage,
            LabelingProjectData data,
            Func<bool> saveAdditionalArtifacts)
            => new AnnotationSaveRequest(
                activeImage,
                new Dictionary<string, List<AnnotationRectangleObject>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase),
                data,
                savedObjectCount: 0,
                saveAdditionalArtifacts);
    }

    public sealed class AnnotationSaveResult
    {
        internal AnnotationSaveResult(bool isSaved, int savedObjectCount)
        {
            IsSaved = isSaved;
            SavedObjectCount = Math.Max(0, savedObjectCount);
        }

        public bool IsSaved { get; }

        public int SavedObjectCount { get; }

        internal static AnnotationSaveResult Failed(int savedObjectCount)
            => new AnnotationSaveResult(false, savedObjectCount);
    }
}
