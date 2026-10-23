using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class LabelingPreferencesViewModel : WpfObservableViewModel, IDisposable
    {
        private readonly LabelingPreferencesService service;
        private LabelingPreferences settings;
        private Action applyPresentation = () => { };
        private string statusKey = string.Empty;

        public LabelingPreferencesViewModel() : this(new LabelingPreferencesService()) { }
        internal LabelingPreferencesViewModel(LabelingPreferencesService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            settings = service.Load(out bool restoredDefaults);
            statusKey = restoredDefaults ? "ShellSettings.RestoredDefaults" : string.Empty;
            ResetCommand = new RelayCommand(() => Save(new LabelingPreferences()));
            OpenVisionLanguageService.LanguageChanged += OnLanguageChanged;
        }

        public bool AutoSaveOnImageMove { get => settings.AutoSaveOnImageMove; set => Save(new LabelingPreferences { AutoSaveOnImageMove = value, ShowLabelNames = ShowLabelNames, BoxCrosshair = BoxCrosshair, KeepZoomOnImageMove = KeepZoomOnImageMove }); }
        public bool ShowLabelNames { get => settings.ShowLabelNames; set => Save(new LabelingPreferences { AutoSaveOnImageMove = AutoSaveOnImageMove, ShowLabelNames = value, BoxCrosshair = BoxCrosshair, KeepZoomOnImageMove = KeepZoomOnImageMove }); }
        public bool BoxCrosshair { get => settings.BoxCrosshair; set => Save(new LabelingPreferences { AutoSaveOnImageMove = AutoSaveOnImageMove, ShowLabelNames = ShowLabelNames, BoxCrosshair = value, KeepZoomOnImageMove = KeepZoomOnImageMove }); }
        public bool KeepZoomOnImageMove { get => settings.KeepZoomOnImageMove; set => Save(new LabelingPreferences { AutoSaveOnImageMove = AutoSaveOnImageMove, ShowLabelNames = ShowLabelNames, BoxCrosshair = BoxCrosshair, KeepZoomOnImageMove = value }); }
        public string StatusText => statusKey.Length == 0 ? string.Empty : OpenVisionLanguageService.T(statusKey);
        public ICommand ResetCommand { get; }

        internal void ConfigurePresentation(Action apply)
        {
            applyPresentation = apply ?? throw new ArgumentNullException(nameof(apply));
            applyPresentation();
        }

        private void Save(LabelingPreferences next)
        {
            if (next.AutoSaveOnImageMove == AutoSaveOnImageMove && next.ShowLabelNames == ShowLabelNames && next.BoxCrosshair == BoxCrosshair && next.KeepZoomOnImageMove == KeepZoomOnImageMove && statusKey.Length == 0) return;
            if (service.TrySave(next)) { settings = next; statusKey = string.Empty; applyPresentation(); }
            else statusKey = "ShellSettings.SaveFailed";
            OnPropertyChanged(string.Empty);
        }

        private void OnLanguageChanged(object sender, EventArgs e) => OnPropertyChanged(nameof(StatusText));
        public void Dispose() => OpenVisionLanguageService.LanguageChanged -= OnLanguageChanged;
    }
}
