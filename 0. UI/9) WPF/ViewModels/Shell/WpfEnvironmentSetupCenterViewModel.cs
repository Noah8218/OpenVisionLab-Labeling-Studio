using MvcVisionSystem._1._Core;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfEnvironmentSetupCenterItem
    {
        public WpfEnvironmentSetupCenterItem(
            string categoryText,
            string nameText,
            string requirementText,
            string statusText,
            string detailText,
            string nextActionText,
            bool isReady,
            bool isWarning,
            bool isRequired)
        {
            CategoryText = categoryText ?? string.Empty;
            NameText = nameText ?? string.Empty;
            RequirementText = requirementText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            IsReady = isReady;
            IsWarning = isWarning;
            IsRequired = isRequired;
        }

        public string CategoryText { get; }
        public string NameText { get; }
        public string RequirementText { get; }
        public string StatusText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
        public bool IsReady { get; }
        public bool IsWarning { get; }
        public bool IsRequired { get; }
    }

    public sealed class WpfEnvironmentSetupCenterViewModel : WpfObservableViewModel, IDisposable
    {
        private readonly RuntimeDiagnosticsService diagnosticsService;
        private readonly Func<PythonModelSettings> pythonSettingsProvider;
        private readonly Action openModelSettingsAction;
        private readonly Func<string> modelSettingsUnavailableReasonProvider;
        private bool disposed;
        private string overallStatusText = "점검 전";
        private string overallDetailText = "앱과 모델 실행 환경을 읽기 전용으로 점검합니다.";
        private string selectedRuntimeText = "선택한 실행 환경: 미설정";
        private string lastCheckedText = "마지막 점검: 없음";
        private int readyCount;
        private int attentionCount;
        private int optionalCount;
        private bool isBusy;

        public WpfEnvironmentSetupCenterViewModel()
            : this(
                new RuntimeDiagnosticsService(),
                () => new PythonModelSettings(),
                null)
        {
        }

        public WpfEnvironmentSetupCenterViewModel(
            RuntimeDiagnosticsService diagnosticsService,
            Func<PythonModelSettings> pythonSettingsProvider,
            Action openModelSettingsAction)
            : this(diagnosticsService, pythonSettingsProvider, openModelSettingsAction, null)
        {
        }

        public WpfEnvironmentSetupCenterViewModel(
            RuntimeDiagnosticsService diagnosticsService,
            Func<PythonModelSettings> pythonSettingsProvider,
            Action openModelSettingsAction,
            Func<string> modelSettingsUnavailableReasonProvider)
        {
            this.diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            this.pythonSettingsProvider = pythonSettingsProvider ?? throw new ArgumentNullException(nameof(pythonSettingsProvider));
            this.openModelSettingsAction = openModelSettingsAction;
            this.modelSettingsUnavailableReasonProvider = modelSettingsUnavailableReasonProvider;
            RefreshCommand = new RelayCommand(Refresh, () => !IsBusy && !disposed);
            OpenModelSettingsCommand = new RelayCommand(OpenModelSettings, () => !IsBusy && !disposed && this.openModelSettingsAction != null && ModelSettingsUnavailableReason.Length == 0);
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            Refresh();
        }

        public ObservableCollection<WpfEnvironmentSetupCenterItem> Items { get; }
            = new ObservableCollection<WpfEnvironmentSetupCenterItem>();

        public ICommand RefreshCommand { get; }
        public ICommand OpenModelSettingsCommand { get; }

        public string OverallStatusText
        {
            get => LocalizeEnvironmentText(overallStatusText);
            private set => SetProperty(ref overallStatusText, value);
        }

        public string OverallDetailText
        {
            get => LocalizeEnvironmentText(overallDetailText);
            private set => SetProperty(ref overallDetailText, value);
        }

        public string SelectedRuntimeText
        {
            get => LocalizeEnvironmentText(selectedRuntimeText);
            private set => SetProperty(ref selectedRuntimeText, value);
        }

        public string LastCheckedText
        {
            get => LocalizeEnvironmentText(lastCheckedText);
            private set => SetProperty(ref lastCheckedText, value);
        }

        public int ReadyCount
        {
            get => readyCount;
            private set => SetProperty(ref readyCount, value);
        }

        public int AttentionCount
        {
            get => attentionCount;
            private set => SetProperty(ref attentionCount, value);
        }

        public int OptionalCount
        {
            get => optionalCount;
            private set => SetProperty(ref optionalCount, value);
        }

        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (SetProperty(ref isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ModelSettingsUnavailableReason => modelSettingsUnavailableReasonProvider?.Invoke() ?? string.Empty;

        public string InstallGuideText
            => T("WpfEnvironment.InstallGuide");

        public string SafetyBoundaryText
            => T("WpfEnvironment.SafetyBoundary");

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OpenVisionLanguageService.LanguageChanged -= OpenVisionLanguageService_LanguageChanged;
            CommandManager.InvalidateRequerySuggested();
        }

        public void Refresh()
        {
            if (disposed || IsBusy)
            {
                return;
            }

            IsBusy = true;
            SelectedRuntimeText = string.Empty;
            try
            {
                Items.Clear();
                RuntimeSelfTestResult applicationReport = diagnosticsService.RunReadOnlySelfTest();
                AddApplicationItems(applicationReport);

                PythonModelSettings settings = pythonSettingsProvider() ?? new PythonModelSettings();
                PythonModelRuntimeSelfTestReport runtimeReport = PythonModelRuntimeSelfTestService.BuildReport(settings);
                AddRuntimeItems(runtimeReport);
                AddEngineSpecificUtilityItems(settings);
                AddOptionalUtilityItems();

                SelectedRuntimeText = Format("WpfEnvironment.SelectedRuntime", FormatEngine(settings.ModelEngine));
                int requiredAttentionCount = Items.Count(item => !item.IsReady && item.IsRequired);
                OverallStatusText = requiredAttentionCount > 0
                    ? Format("WpfEnvironment.Overall.Required", requiredAttentionCount)
                    : Items.Any(item => !item.IsReady)
                        ? T("WpfEnvironment.Overall.Optional")
                        : T("WpfEnvironment.Overall.Ready");
                OverallDetailText = requiredAttentionCount > 0
                    ? T("WpfEnvironment.Overall.Detail.Required")
                    : string.Empty;
                LastCheckedText = Format(
                    "WpfEnvironment.LastChecked",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                OverallStatusText = T("WpfEnvironment.Overall.Failed");
                OverallDetailText = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                ReadyCount = Items.Count(item => item.IsReady);
                AttentionCount = Items.Count(item => !item.IsReady && item.IsRequired);
                OptionalCount = Items.Count(item => !item.IsRequired);
                OnPropertyChanged(nameof(ModelSettingsUnavailableReason));
                IsBusy = false;
            }
        }

        private void OpenModelSettings()
        {
            if (!OpenModelSettingsCommand.CanExecute(null))
            {
                return;
            }

            openModelSettingsAction?.Invoke();
        }

        private void AddApplicationItems(RuntimeSelfTestResult report)
        {
            foreach (RuntimeSelfTestCheck check in report?.Checks ?? Array.Empty<RuntimeSelfTestCheck>())
            {
                bool ready = string.Equals(check.Status, "pass", StringComparison.Ordinal);
                bool warning = string.Equals(check.Status, "warning", StringComparison.Ordinal);
                string detailText = LocalizeApplicationDetail(check.Name, check.Detail);
                if (string.Equals(check.Name, "releaseManifest", StringComparison.Ordinal)
                    && !detailText.Contains(RuntimeDiagnosticsService.ReleaseManifestFileName, StringComparison.Ordinal))
                {
                    detailText += " · " + RuntimeDiagnosticsService.ReleaseManifestFileName;
                }
                Items.Add(new WpfEnvironmentSetupCenterItem(
                    T("WpfEnvironment.Category.Application"),
                    FormatApplicationCheckName(check.Name),
                    warning ? T("WpfEnvironment.Requirement.Recommended") : T("WpfEnvironment.Requirement.Required"),
                    ready ? T("WpfEnvironment.Status.Ready") : warning ? T("WpfEnvironment.Status.ReviewNeeded") : T("WpfEnvironment.Status.ActionNeeded"),
                    detailText,
                    ready ? T("WpfEnvironment.NextAction.None") : FormatApplicationNextAction(check.Name),
                    ready,
                    warning,
                    isRequired: !warning));
            }
        }

        private void AddRuntimeItems(PythonModelRuntimeSelfTestReport report)
        {
            foreach (PythonModelRuntimeSelfTestItem item in report?.Items ?? Array.Empty<PythonModelRuntimeSelfTestItem>())
            {
                Items.Add(new WpfEnvironmentSetupCenterItem(
                    T("WpfEnvironment.Category.ModelRuntime"),
                    FormatRuntimeLabel(item.LabelText),
                    item.IsWarning ? T("WpfEnvironment.Requirement.WhenFeatureUsed") : T("WpfEnvironment.Requirement.Required"),
                    item.IsPassed ? T("WpfEnvironment.Status.Ready") : item.IsWarning ? T("WpfEnvironment.Status.ReviewNeeded") : T("WpfEnvironment.Status.SetupNeeded"),
                    LocalizeRuntimeDetail(item.LabelText, item.DetailText),
                    item.IsPassed ? T("WpfEnvironment.NextAction.None") : FormatRuntimeNextAction(item.LabelText),
                    item.IsPassed,
                    item.IsWarning,
                    isRequired: !item.IsWarning));
            }
        }

        private void AddOptionalUtilityItems()
        {
            Items.Add(new WpfEnvironmentSetupCenterItem(
                T("WpfEnvironment.Category.Optional"),
                T("WpfEnvironment.Name.GpuCuda"),
                T("WpfEnvironment.Requirement.Optional"),
                T("WpfEnvironment.Status.OptionalCheck"),
                T("WpfEnvironment.Detail.GpuCuda"),
                T("WpfEnvironment.NextAction.GpuCuda"),
                isReady: false,
                isWarning: true,
                isRequired: false));
        }

        private void AddEngineSpecificUtilityItems(PythonModelSettings settings)
        {
            if (!string.Equals(
                PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine),
                PythonModelSettings.EngineYoloV5,
                StringComparison.Ordinal))
            {
                return;
            }

            PythonModelRuntimeRepositoryCheck repositoryCheck =
                PythonModelRuntimeSelfTestService.CheckYoloV5Repository(settings?.ProjectRootPath);
            bool ready = repositoryCheck.IsReady;
            Items.Add(new WpfEnvironmentSetupCenterItem(
                T("WpfEnvironment.Category.ModelRuntime"),
                T("WpfEnvironment.Name.YoloV5Files"),
                T("WpfEnvironment.Requirement.Required"),
                ready ? T("WpfEnvironment.Status.Ready") : T("WpfEnvironment.Status.RecoveryNeeded"),
                ready
                    ? T("WpfEnvironment.Detail.YoloV5FilesReady")
                    : Format("WpfEnvironment.Detail.YoloV5FilesMissing", string.Join(", ", repositoryCheck.MissingRelativePaths)),
                ready
                    ? T("WpfEnvironment.NextAction.None")
                    : T("WpfEnvironment.NextAction.YoloV5Files"),
                ready,
                isWarning: false,
                isRequired: true));
        }

        private static string FormatApplicationCheckName(string name)
            => name switch
            {
                "productIdentity" => T("WpfEnvironment.Name.ProductVersion"),
                "productBinary" => T("WpfEnvironment.Name.ProductFiles"),
                "applicationExecutable" => T("WpfEnvironment.Name.Executable"),
                "releaseManifest" => T("WpfEnvironment.Name.ReleaseIntegrity"),
                "diagnosticsPath" => T("WpfEnvironment.Name.DiagnosticsPath"),
                "supportBundlePath" => T("WpfEnvironment.Name.SupportBundlePath"),
                "logIsolation" => T("WpfEnvironment.Name.LogIsolation"),
                RuntimeDiagnosticsService.ViewerGraphicsCheckName => T("WpfEnvironment.Name.ViewerGraphics"),
                _ => LocalizeEnvironmentText(name)
            };

        private static string FormatApplicationNextAction(string name)
            => name switch
            {
                "productBinary" or "applicationExecutable" => T("WpfEnvironment.NextAction.ProductFiles"),
                "releaseManifest" => T("WpfEnvironment.NextAction.ReleaseManifest"),
                "diagnosticsPath" or "supportBundlePath" or "logIsolation" => T("WpfEnvironment.NextAction.UserPaths"),
                RuntimeDiagnosticsService.ViewerGraphicsCheckName => T("WpfEnvironment.NextAction.ViewerGraphics"),
                _ => T("WpfEnvironment.NextAction.Generic")
            };

        private static string FormatRuntimeNextAction(string label)
            => label switch
            {
                "Python" => T("WpfEnvironment.NextAction.Python"),
                "Ultralytics" or "PyTorch U-Net" or "PyTorch PatchCore" => T("WpfEnvironment.NextAction.RuntimePackage"),
                "검사 모델" or "Inspection model" => T("WpfEnvironment.NextAction.InspectionModel"),
                "이미지" or "Image" => T("WpfEnvironment.NextAction.Image"),
                _ => T("WpfEnvironment.NextAction.RuntimeGeneric")
            };

        private static string FormatRuntimeLabel(string label)
            => label switch
            {
                "프로젝트" or "Project" => T("WpfEnvironment.Name.Project"),
                "모델 루트" or "Model root" => T("WpfEnvironment.Name.ModelRoot"),
                "실행 스크립트" or "Execution script" => T("WpfEnvironment.Name.ExecutionScript"),
                "검사 모델" or "Inspection model" => T("WpfEnvironment.Name.InspectionModel"),
                "이미지" or "Image" => T("WpfEnvironment.Name.Images"),
                "실행 연결" or "Execution connection" => T("WpfEnvironment.Name.ExecutionConnection"),
                _ => LocalizeEnvironmentText(label)
            };

        private static string FormatEngine(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV5 => "YOLOv5",
                PythonModelSettings.EngineYoloV8 => "YOLOv8",
                PythonModelSettings.EngineYolo11 => "YOLO11",
                PythonModelSettings.EngineUnet => "U-Net",
                PythonModelSettings.EnginePatchCore => "PatchCore",
                PythonModelSettings.EngineOnnx => "ONNX",
                _ => T("WpfEnvironment.Engine.Unconfigured")
            };

        private void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            if (disposed)
            {
                return;
            }

            Refresh();
            OnPropertyChanged(nameof(InstallGuideText));
            OnPropertyChanged(nameof(SafetyBoundaryText));
        }

        private static string T(string key)
            => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] arguments)
            => string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());

        private static string LocalizeEnvironmentText(string value)
        {
            value ??= string.Empty;
            IReadOnlyList<OpenVisionLocalizationEntry> entries = OpenVisionLanguageService.GetEntries();
            foreach (string key in new[]
            {
                "WpfEnvironment.Initial.Status",
                "WpfEnvironment.Initial.Detail",
                "WpfEnvironment.Initial.LastChecked",
                "WpfEnvironment.SelectedRuntime",
                "WpfEnvironment.LastChecked",
                "WpfEnvironment.Detail.ProductDllFound",
                "WpfEnvironment.Detail.ProductDllMissing",
                "WpfEnvironment.Detail.ExecutableFound",
                "WpfEnvironment.Detail.ExecutableMissing",
                "WpfEnvironment.Detail.ReleaseManifestMissing",
                "WpfEnvironment.Detail.PathExistsReadOnly",
                "WpfEnvironment.Detail.PathMissingReadOnly",
                "WpfEnvironment.Detail.LogIsolated",
                "WpfEnvironment.Detail.LogInsideApplication",
                "WpfEnvironment.Detail.ViewerGraphicsNotConnected",
                "WpfEnvironment.Detail.ViewerGraphicsNoResult",
                "WpfEnvironment.Detail.UnetVenvMissing",
                "WpfEnvironment.Detail.UnetPackageMissing",
                "WpfEnvironment.Detail.ReleaseManifestMatch",
                "WpfEnvironment.Detail.ReleaseManifestMismatch",
                "WpfEnvironment.Detail.ReleaseManifestReadFailed",
                "WpfEnvironment.Detail.UltralyticsPackageMissing",
                "WpfModelDetails.Runtime.109",
                "WpfModelDetails.Runtime.112",
                "WpfModelDetails.Runtime.117",
                "WpfModelDetails.Runtime.120",
                "WpfModelDetails.Runtime.137",
                "WpfModelDetails.Runtime.138",
                "WpfModelDetails.Runtime.170",
                "WpfModelDetails.Runtime.172",
                "WpfModelDetails.Runtime.174",
                "WpfModelDetails.Runtime.182",
                "WpfModelDetails.Runtime.183",
                "WpfModelDetails.Runtime.187",
                "WpfModelDetails.Runtime.190",
                "WpfModelDetails.Runtime.193",
                "WpfModelDetails.Runtime.196",
                "WpfModelDetails.Runtime.199",
                "WpfModelDetails.Path.124Empty",
                "WpfModelDetails.Path.124Missing",
                "WpfModelDetails.Path.126Empty",
                "WpfModelDetails.Path.126Missing",
                "WpfModelDetails.Path.128Empty",
                "WpfModelDetails.Path.128Missing",
                "WpfModelDetails.Path.129Empty",
                "WpfModelDetails.Path.129Missing",
                "WpfModelDetails.Path.133Empty",
                "WpfModelDetails.Path.133Missing",
                "WpfModelDetails.Path.135Empty",
                "WpfModelDetails.Path.135Missing",
                "WpfModelDetails.Path.136Empty",
                "WpfModelDetails.Path.136Missing",
                "WpfModelDetails.Support.Flags205",
                "WpfModelDetails.Support.Flags206",
                "WpfModelDetails.Support.Flags207",
                "WpfModelDetails.Support.Flags208",
            })
            {
                OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                if (entry == null) continue;
                string korean = entry.Korean;
                string english = entry.English;
                string target = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean ? korean : english;
                IEnumerable<string> previousDefaults = OpenVisionLanguageService.GetPreviousKoreanDefaults(key);
                if (key == "WpfEnvironment.Detail.ReleaseManifestMatch"
                    || key == "WpfEnvironment.Detail.ReleaseManifestMismatch"
                    || key == "WpfEnvironment.Detail.ReleaseManifestReadFailed")
                {
                    previousDefaults = previousDefaults.Skip(1);
                }
                foreach (string source in new[] { korean, english }.Concat(previousDefaults))
                {
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    if (string.Equals(value, source, StringComparison.Ordinal)) return string.IsNullOrWhiteSpace(target) ? value : target;
                    string[] parts = source.Split(new[] { "{0}" }, StringSplitOptions.None);
                    if (parts.Length < 2) continue;
                    string pattern = @"\A" + Regex.Escape(parts[0]) + "(?<argument>.*?)"
                        + string.Concat(parts.Skip(1).Select((part, index) => Regex.Escape(part) + (index < parts.Length - 2 ? @"\k<argument>" : string.Empty))) + @"\z";
                    Match match;
                    try { match = Regex.Match(value, pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(250)); }
                    catch (RegexMatchTimeoutException) { return value; }
                    if (!match.Success) continue;
                    if (string.IsNullOrWhiteSpace(target)) return value;
                    try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, target, match.Groups["argument"].Value); }
                    catch (FormatException) { return value; }
                }
            }
            return LocalizationTextRuntimeService.Translate(value);
        }

        private static string LocalizeApplicationDetail(string name, string detail)
        {
            string value = detail ?? string.Empty;
            string translated = LocalizeEnvironmentText(value);
            if (!string.Equals(translated, value, StringComparison.Ordinal))
            {
                return translated;
            }

            if (string.Equals(name, "releaseManifest", StringComparison.Ordinal))
            {
                string matchPrefix = OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfEnvironment.Detail.ReleaseManifestMatch")[0].Split(new[] { "{0}" }, StringSplitOptions.None)[0];
                string mismatchPrefix = OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfEnvironment.Detail.ReleaseManifestMismatch")[0].Split(new[] { "{0}" }, StringSplitOptions.None)[0];
                string readFailurePrefix = OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfEnvironment.Detail.ReleaseManifestReadFailed")[0].Split(new[] { "{0}" }, StringSplitOptions.None)[0];
                if (value.StartsWith(matchPrefix, StringComparison.Ordinal)
                    && value.EndsWith(")", StringComparison.Ordinal))
                {
                    return Format("WpfEnvironment.Detail.ReleaseManifestMatch", value.Substring(matchPrefix.Length, value.Length - matchPrefix.Length - 1));
                }

                if (value.StartsWith(mismatchPrefix, StringComparison.Ordinal))
                {
                    return value.EndsWith(")", StringComparison.Ordinal)
                        ? Format("WpfEnvironment.Detail.ReleaseManifestMismatch", value.Substring(mismatchPrefix.Length, value.Length - mismatchPrefix.Length - 1))
                        : value;
                }

                if (value.StartsWith(readFailurePrefix, StringComparison.Ordinal))
                {
                    return Format("WpfEnvironment.Detail.ReleaseManifestReadFailed", value.Substring(readFailurePrefix.Length));
                }
            }

            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean) return value;

            if (string.Equals(name, RuntimeDiagnosticsService.ViewerGraphicsCheckName, StringComparison.Ordinal))
            {
                const string notAvailablePrefix = "이미지 뷰어 사용 불가";
                const string availablePrefix = "이미지 뷰어 사용 가능";
                const string failurePrefix = "이미지 뷰어 그래픽 점검 실패 · ";
                const string missingFunctionsMarker = " · 지원되지 않는 필수 기능: ";
                const string requiredFunctionsMarker = " · 필수 framebuffer 함수";
                const string retrySuffix = " · 지원되는 GPU 드라이버가 설치된 로컬 PC/VM 콘솔에서 다시 실행하세요.";

                if (value.StartsWith(notAvailablePrefix, StringComparison.Ordinal))
                {
                    string remainder = value.Substring(notAvailablePrefix.Length);
                    int markerIndex = remainder.IndexOf(missingFunctionsMarker, StringComparison.Ordinal);
                    if (markerIndex >= 0)
                    {
                        string identity = remainder.Substring(0, markerIndex);
                        string functions = remainder.Substring(markerIndex + missingFunctionsMarker.Length);
                        if (functions.EndsWith(retrySuffix, StringComparison.Ordinal))
                        {
                            functions = functions.Substring(0, functions.Length - retrySuffix.Length);
                        }

                        return "Image viewer unavailable"
                            + identity
                            + " · Unsupported required functions: "
                            + functions
                            + " · Run again on a local PC/VM console with a supported GPU driver.";
                    }
                }

                if (value.StartsWith(availablePrefix, StringComparison.Ordinal))
                {
                    string remainder = value.Substring(availablePrefix.Length);
                    int markerIndex = remainder.IndexOf(requiredFunctionsMarker, StringComparison.Ordinal);
                    if (markerIndex >= 0)
                    {
                        string identity = remainder.Substring(0, markerIndex);
                        string count = remainder.Substring(markerIndex + requiredFunctionsMarker.Length);
                        const string countSuffix = "개 확인";
                        if (count.EndsWith(countSuffix, StringComparison.Ordinal))
                        {
                            count = count.Substring(0, count.Length - countSuffix.Length);
                        }

                        return "Image viewer available"
                            + identity
                            + " · Required framebuffer functions "
                            + count
                            + " verified";
                    }
                }

                if (value.StartsWith(failurePrefix, StringComparison.Ordinal))
                {
                    return "Image viewer graphics check failed · "
                        + value.Substring(failurePrefix.Length)
                        + " · Run again on a local PC/VM console with a supported GPU driver.";
                }
            }

            return value;
        }

        private static string LocalizeRuntimeDetail(string label, string detail)
            => LocalizeEnvironmentText(detail ?? string.Empty);
    }
}
