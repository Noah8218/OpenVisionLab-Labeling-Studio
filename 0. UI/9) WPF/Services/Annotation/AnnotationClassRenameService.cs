using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps in-memory annotation projections aligned after a project class rename.
    /// The project class catalog persistence remains owned by ClassCatalogWorkflowService.
    /// </summary>
    public sealed class AnnotationClassRenameService
    {
        public int Rename(
            string oldName,
            string newName,
            LabelClass classItem,
            IList<string> manualRoiClassNames,
            IList<LabelingSegmentationObject> manualSegments,
            IEnumerable<YoloWorkerSmokeCandidate> confirmedDetectionCandidates)
        {
            string normalizedOldName = ClassCatalogService.NormalizeClassName(oldName);
            string normalizedNewName = ClassCatalogService.NormalizeClassName(newName);
            if (string.IsNullOrWhiteSpace(normalizedOldName)
                || string.IsNullOrWhiteSpace(normalizedNewName))
            {
                return 0;
            }

            int changedCount = 0;
            if (manualRoiClassNames != null)
            {
                for (int i = 0; i < manualRoiClassNames.Count; i++)
                {
                    if (string.Equals(manualRoiClassNames[i], normalizedOldName, StringComparison.OrdinalIgnoreCase))
                    {
                        manualRoiClassNames[i] = normalizedNewName;
                        changedCount++;
                    }
                }
            }

            if (manualSegments != null)
            {
                foreach (LabelingSegmentationObject segment in manualSegments)
                {
                    if (segment == null)
                    {
                        continue;
                    }

                    bool matches = string.Equals(segment.ClassName, normalizedOldName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(segment.ClassItem?.Text, normalizedOldName, StringComparison.OrdinalIgnoreCase);
                    if (matches)
                    {
                        segment.ClassName = normalizedNewName;
                        segment.ClassItem = classItem;
                        changedCount++;
                    }
                }
            }

            if (confirmedDetectionCandidates != null)
            {
                foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
                {
                    if (string.Equals(candidate?.ClassName, normalizedOldName, StringComparison.OrdinalIgnoreCase))
                    {
                        candidate.ClassName = normalizedNewName;
                        changedCount++;
                    }
                }
            }

            return changedCount;
        }
    }
}
