using OpenVisionLab.Mvvm.Behaviors;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Object and candidate review wiring is grouped because both panels operate on the current label selection.
        private void ConfigureObjectReviewPanelCommands()
        {
            ObjectReviewViewModel.ConfigureCommands(
                ExecuteDeleteObjectCommand,
                ExecuteApplyObjectClassCommand,
                ExecuteMarkQualityUnreviewedCommand,
                ExecuteMarkQualityNeedsFixCommand,
                ExecuteMarkQualityReviewedCommand,
                ExecuteExportQualityReviewReportCommand,
                ExecuteObjectSelectionChangedCommand,
                ExecuteObjectPreviewKeyDownCommand);
            RefreshAttachedCommandBindings(
                ObjectListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }

        private void RegisterObjectReviewPanelNames()
        {
            ConfigureObjectReviewPanelCommands();
            RegisterObjectReviewName(nameof(ObjectReviewSummaryText), ObjectReviewSummaryText);
            RegisterObjectReviewName(nameof(ObjectReviewLabelSaveBadge), ObjectReviewLabelSaveBadge);
            RegisterObjectReviewName(nameof(ObjectReviewLabelSaveBadgeText), ObjectReviewLabelSaveBadgeText);
            RegisterObjectReviewName(nameof(ObjectReviewLabelSaveDetailText), ObjectReviewLabelSaveDetailText);
            RegisterObjectReviewName(nameof(DeleteObjectButton), DeleteObjectButton);
            RegisterObjectReviewName(nameof(ObjectClassBox), ObjectClassBox);
            RegisterObjectReviewName(nameof(ApplyObjectClassButton), ApplyObjectClassButton);
            RegisterObjectReviewName(nameof(ObjectListBox), ObjectListBox);
        }

        private void RegisterObjectReviewName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

        private void ConfigureCandidateReviewPanelCommands()
        {
            CandidateReviewViewModel.ConfigureCommands(
                ExecuteCandidateConfidenceChangedCommand,
                ExecuteConfirmSelectedCandidateCommand,
                ExecuteConfirmAllCandidatesCommand,
                ExecuteSkipSelectedCandidateCommand,
                ExecutePreviousCandidateCommand,
                ExecuteNextCandidateCommand,
                ExecuteFocusCandidateCommand,
                ExecuteFocusCurrentLabelCommand,
                ExecuteCandidateSelectionChangedCommand,
                ExecuteCandidatePreviewKeyDownCommand,
                ExecuteCompleteImageAndNextCommand,
                ExecuteOpenModelComparisonExampleCommand,
                ExecuteSaveModelCandidateCommand,
                ExecuteRejectModelCandidateCommand,
                ExecuteModelComparisonHistorySelectionChangedCommand);
            RefreshAttachedCommandBindings(CandidateConfidenceSlider, InputCommandBehaviors.ValueInputCommandProperty);
            RefreshAttachedCommandBindings(
                CandidateListBox,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.PreviewKeyInputCommandProperty);
        }

        private void RegisterCandidateReviewPanelNames()
        {
            ConfigureCandidateReviewPanelCommands();
            RegisterCandidateReviewName(nameof(CandidateConfidenceSlider), CandidateConfidenceSlider);
            RegisterCandidateReviewName(nameof(CandidateReviewRoleSplitPanel), CandidateReviewRoleSplitPanel);
            RegisterCandidateReviewName(nameof(CurrentImageCandidateRoleCard), CurrentImageCandidateRoleCard);
            RegisterCandidateReviewName(nameof(ModelValidationRoleCard), ModelValidationRoleCard);
            RegisterCandidateReviewName(nameof(CurrentImageReviewRoleTitleText), CurrentImageReviewRoleTitleText);
            RegisterCandidateReviewName(nameof(CurrentImageReviewRoleDetailText), CurrentImageReviewRoleDetailText);
            RegisterCandidateReviewName(nameof(CurrentImageReviewRoleResultText), CurrentImageReviewRoleResultText);
            RegisterCandidateReviewName(nameof(ModelValidationRoleTitleText), ModelValidationRoleTitleText);
            RegisterCandidateReviewName(nameof(ModelValidationRoleDetailText), ModelValidationRoleDetailText);
            RegisterCandidateReviewName(nameof(ModelValidationRoleResultText), ModelValidationRoleResultText);
            RegisterCandidateReviewName(nameof(ModelCandidateDecisionPanel), ModelCandidateDecisionPanel);
            RegisterCandidateReviewName(nameof(ModelCandidateDecisionStatusText), ModelCandidateDecisionStatusText);
            RegisterCandidateReviewName(nameof(ModelCandidateDecisionDetailText), ModelCandidateDecisionDetailText);
            RegisterCandidateReviewName(nameof(SaveModelCandidateButton), SaveModelCandidateButton);
            RegisterCandidateReviewName(nameof(RejectModelCandidateButton), RejectModelCandidateButton);
            RegisterCandidateReviewName(nameof(CandidateConfidenceText), CandidateConfidenceText);
            RegisterCandidateReviewName(nameof(CandidateDetailText), CandidateDetailText);
            RegisterCandidateReviewName(nameof(SelectedCandidateSummaryPanel), SelectedCandidateSummaryPanel);
            RegisterCandidateReviewName(nameof(SelectedCandidateSummaryText), SelectedCandidateSummaryText);
            RegisterCandidateReviewName(nameof(CandidateComparisonPanel), CandidateComparisonPanel);
            RegisterCandidateReviewName(nameof(CandidateCompareCandidateText), CandidateCompareCandidateText);
            RegisterCandidateReviewName(nameof(CandidateCompareCurrentText), CandidateCompareCurrentText);
            RegisterCandidateReviewName(nameof(CandidateCompareOverlapText), CandidateCompareOverlapText);
            RegisterCandidateReviewName(nameof(CandidateCompareDecisionText), CandidateCompareDecisionText);
            RegisterCandidateReviewName(nameof(ConfirmSelectedCandidateButton), ConfirmSelectedCandidateButton);
            RegisterCandidateReviewName(nameof(ConfirmAllCandidatesButton), ConfirmAllCandidatesButton);
            RegisterCandidateReviewName(nameof(SkipSelectedCandidateButton), SkipSelectedCandidateButton);
            RegisterCandidateReviewName(nameof(CompleteImageAndNextButton), CompleteImageAndNextButton);
            RegisterCandidateReviewName(nameof(PreviousCandidateButton), PreviousCandidateButton);
            RegisterCandidateReviewName(nameof(NextCandidateButton), NextCandidateButton);
            RegisterCandidateReviewName(nameof(FocusCandidateButton), FocusCandidateButton);
            RegisterCandidateReviewName(nameof(FocusCurrentLabelButton), FocusCurrentLabelButton);
            RegisterCandidateReviewName(nameof(CandidateListBox), CandidateListBox);
        }

        private void RegisterCandidateReviewName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

    }
}
