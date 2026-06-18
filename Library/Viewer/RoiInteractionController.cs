using MvcVisionSystem.DrawObject;
using System.Collections.Generic;
using System.Drawing;
using static OpenVisionLab.DrawObject.DrawObjectEnums;

namespace MvcVisionSystem
{
    public static class RoiInteractionController
    {
        public static bool TrySelect(
            IList<CRectangleObject> rois,
            Point imagePoint,
            int handleSize,
            out int selectedIndex,
            out PosSizableRect operation)
        {
            selectedIndex = -1;
            operation = PosSizableRect.None;

            if (rois == null)
            {
                return false;
            }

            for (int i = rois.Count - 1; i >= 0; i--)
            {
                PosSizableRect candidate = rois[i].GetNodeSelectable(imagePoint, handleSize);
                if (candidate == PosSizableRect.None)
                {
                    continue;
                }

                selectedIndex = i;
                operation = candidate;
                rois[i].Selected = true;
                return true;
            }

            return false;
        }

        public static void ClearSelection(IDictionary<string, List<CRectangleObject>> roiGroups)
        {
            if (roiGroups == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in roiGroups)
            {
                for (int i = 0; i < roiGroup.Value.Count; i++)
                {
                    roiGroup.Value[i].Selected = false;
                }
            }
        }

        public static bool TryRemoveSelected(
            IDictionary<string, List<CRectangleObject>> roiGroups,
            string className)
        {
            if (roiGroups == null || !roiGroups.TryGetValue(className, out List<CRectangleObject> rois))
            {
                return false;
            }

            int index = rois.FindIndex(x => x.Selected);
            if (index < 0)
            {
                return false;
            }

            rois.RemoveAt(index);
            return true;
        }

        public static void ApplyImageBounds(
            IDictionary<string, List<CRectangleObject>> roiGroups,
            Size imageSize)
        {
            if (roiGroups == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<CRectangleObject>> roiGroup in roiGroups)
            {
                for (int i = 0; i < roiGroup.Value.Count; i++)
                {
                    roiGroup.Value[i]._MaxX = imageSize.Width;
                    roiGroup.Value[i]._MaxY = imageSize.Height;
                    roiGroup.Value[i].OriginalSize = imageSize;
                }
            }
        }
    }
}
