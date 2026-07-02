using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Class-editor synchronization is separated from object-list construction to keep MVVM binding issues easier to trace.
        private void UpdateObjectReviewActionState()
        {
            ObjectReviewViewModel?.RefreshActionState();
        }

        private void SyncObjectClassEditorToSelection()
        {
            if (ObjectReviewViewModel == null)
            {
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                ObjectReviewViewModel.SelectedClassName = string.Empty;
                return;
            }

            string className = WpfObjectReviewEditService.GetClassName(
                item,
                manualRoiClassNames,
                manualSegments,
                confirmedDetectionCandidates);
            ObjectReviewViewModel.SetSelectedObjectClass(GetClassNames(), className);
        }

        private void RefreshObjectClassOptions(string selectedName = "")
        {
            if (ObjectReviewViewModel == null)
            {
                return;
            }

            string viewModelSelection = string.IsNullOrWhiteSpace(selectedName)
                ? ObjectReviewViewModel.SelectedClassName
                : selectedName;
            ObjectReviewViewModel.SetClassNames(GetClassNames(), viewModelSelection);
        }

        private IReadOnlyList<string> GetClassNames()
        {
            if (global.Data.ClassNamedList == null
                || !global.Data.ClassNamedList.Any(item => item != null && !string.IsNullOrWhiteSpace(item.Text)))
            {
                EnsureClassItem("Defect");
            }

            return global.Data.ClassNamedList
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
