using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class ImageReviewRejectionViewModel : WpfObservableViewModel
    {
        private readonly string[] reasonKeys;
        private string selectedReasonKey = "Missing";
        private string noteText = string.Empty;
        private bool isCompleted;

        public ImageReviewRejectionViewModel(string imagePath, bool hasUnsavedChanges, bool isAnomalyPurpose = false)
        {
            FileName = Path.GetFileName(imagePath ?? string.Empty);
            HasUnsavedChanges = hasUnsavedChanges;
            IsAnnotationPurpose = !isAnomalyPurpose;
            reasonKeys = isAnomalyPurpose ? new[] { "Verdict", "ImageQuality", "Other" } : new[] { "Missing", "Position", "Class", "Extra", "Other" };
            selectedReasonKey = reasonKeys[0];
            SelectReasonCommand = new RelayCommand<string>(reason => SelectedReasonKey = reason, reason => !isCompleted && reasonKeys.Contains(reason));
            RejectCommand = new RelayCommand(() => Complete(true), () => !isCompleted);
            CancelCommand = new RelayCommand(() => Complete(false), () => !isCompleted);
        }

        public event Action<bool> CloseRequested;

        public string FileName { get; }
        public string FileDescriptionText => FileName + " · " + T("WpfReview.Reject.Explanation");
        public bool HasUnsavedChanges { get; }
        public string TitleText => T("WpfReview.Reject.Title");
        public string ReasonPromptText => T("WpfReview.Reject.ReasonPrompt");
        public bool IsAnnotationPurpose { get; }
        public string MissingReasonKey => reasonKeys[0];
        public string PositionReasonKey => reasonKeys[1];
        public string MissingReasonText => "1  " + ReasonText(MissingReasonKey);
        public string PositionReasonText => "2  " + ReasonText(PositionReasonKey);
        public string ClassReasonText => "3  " + ReasonText("Class");
        public string ExtraReasonText => "4  " + ReasonText("Extra");
        public string OtherReasonText => reasonKeys.Length + "  " + ReasonText("Other");
        public string NotePromptText => T("WpfReview.Reject.NotePrompt");
        public string DiscardWarningText => T("WpfReview.Reject.DiscardWarning");
        public string CancelText => T("WpfReview.Reject.Cancel");
        public string RejectText => T("WpfReview.Reject.Confirm");
        public int NoteMaxLength => Math.Max(0, YoloImageReviewStatusService.QualityReviewNoteMaxLength - ReasonText(SelectedReasonKey).Length - 3);
        public string RejectionNote => YoloImageReviewStatusService.NormalizeQualityReviewNote(ReasonText(SelectedReasonKey)
            + (string.IsNullOrWhiteSpace(NoteText) ? string.Empty : " — " + NoteText));
        public ICommand SelectReasonCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand CancelCommand { get; }

        public string SelectedReasonKey
        {
            get => selectedReasonKey;
            set
            {
                if (isCompleted || !reasonKeys.Contains(value)) return;
                if (!SetProperty(ref selectedReasonKey, value)) return;
                OnPropertyChanged(nameof(NoteMaxLength));
                NoteText = noteText;
                OnPropertyChanged(nameof(RejectionNote));
            }
        }

        public string NoteText
        {
            get => noteText;
            set
            {
                if (isCompleted) return;
                string normalized = value ?? string.Empty;
                if (normalized.Length > NoteMaxLength) normalized = normalized.Substring(0, NoteMaxLength);
                if (SetProperty(ref noteText, normalized)) OnPropertyChanged(nameof(RejectionNote));
            }
        }

        private void Complete(bool accepted)
        {
            if (isCompleted) return;
            isCompleted = true;
            CommandManager.InvalidateRequerySuggested();
            CloseRequested?.Invoke(accepted);
        }

        private static string ReasonText(string key) => T("WpfReview.Reject.Reason." + key);
        private static string T(string key) => OpenVisionLanguageService.T(key);
    }
}
