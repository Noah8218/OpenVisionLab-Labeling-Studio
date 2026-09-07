using MvcVisionSystem._1._Core;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private bool disposed;
        private string overallStatusText = "환경 확인 전";
        private string overallDetailText = "앱과 모델 실행 유틸리티를 읽기 전용으로 확인합니다.";
        private string selectedRuntimeText = "선택된 모델 실행기: 미설정";
        private string lastCheckedText = "마지막 확인: 없음";
        private int readyCount;
        private int attentionCount;
        private int optionalCount;
        private bool isBusy;

        public WpfEnvironmentSetupCenterViewModel()
            : this(
                new RuntimeDiagnosticsService(),
                () => LabelingApplicationState.Inst.Data.ProjectSettings?.PythonModel ?? new PythonModelSettings(),
                null)
        {
        }

        public WpfEnvironmentSetupCenterViewModel(
            RuntimeDiagnosticsService diagnosticsService,
            Func<PythonModelSettings> pythonSettingsProvider,
            Action openModelSettingsAction)
        {
            this.diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            this.pythonSettingsProvider = pythonSettingsProvider ?? throw new ArgumentNullException(nameof(pythonSettingsProvider));
            this.openModelSettingsAction = openModelSettingsAction;
            RefreshCommand = new RelayCommand(Refresh, () => !IsBusy && !disposed);
            OpenModelSettingsCommand = new RelayCommand(OpenModelSettings, () => !IsBusy && !disposed && this.openModelSettingsAction != null);
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
            Refresh();
        }

        public ObservableCollection<WpfEnvironmentSetupCenterItem> Items { get; }
            = new ObservableCollection<WpfEnvironmentSetupCenterItem>();

        public ICommand RefreshCommand { get; }
        public ICommand OpenModelSettingsCommand { get; }

        public string OverallStatusText
        {
            get => overallStatusText;
            private set => SetProperty(ref overallStatusText, value);
        }

        public string OverallDetailText
        {
            get => overallDetailText;
            private set => SetProperty(ref overallDetailText, value);
        }

        public string SelectedRuntimeText
        {
            get => selectedRuntimeText;
            private set => SetProperty(ref selectedRuntimeText, value);
        }

        public string LastCheckedText
        {
            get => lastCheckedText;
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
                ReadyCount = Items.Count(item => item.IsReady);
                AttentionCount = Items.Count(item => !item.IsReady && item.IsRequired);
                OptionalCount = Items.Count(item => !item.IsRequired);
                OverallStatusText = AttentionCount > 0
                    ? Format("WpfEnvironment.Overall.Required", AttentionCount)
                    : Items.Any(item => !item.IsReady)
                        ? T("WpfEnvironment.Overall.Optional")
                        : T("WpfEnvironment.Overall.Ready");
                OverallDetailText = AttentionCount > 0
                    ? T("WpfEnvironment.Overall.Detail.Required")
                    : T("WpfEnvironment.Overall.Detail.Ready");
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
                IsBusy = false;
            }
        }

        private void OpenModelSettings()
        {
            if (disposed)
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
                Items.Add(new WpfEnvironmentSetupCenterItem(
                    T("WpfEnvironment.Category.Application"),
                    FormatApplicationCheckName(check.Name),
                    warning ? T("WpfEnvironment.Requirement.Recommended") : T("WpfEnvironment.Requirement.Required"),
                    ready ? T("WpfEnvironment.Status.Ready") : warning ? T("WpfEnvironment.Status.ReviewNeeded") : T("WpfEnvironment.Status.ActionNeeded"),
                    LocalizeApplicationDetail(check.Name, check.Detail),
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

            string projectRoot = settings?.ProjectRootPath?.Trim() ?? string.Empty;
            string[] requiredRelativePaths =
            {
                "hubconf.py",
                "train.py",
                "detect.py",
                Path.Combine("models", "common.py")
            };
            var missing = new List<string>();
            foreach (string relativePath in requiredRelativePaths)
            {
                if (string.IsNullOrWhiteSpace(projectRoot)
                    || !File.Exists(Path.Combine(projectRoot, relativePath)))
                {
                    missing.Add(relativePath.Replace('\\', '/'));
                }
            }

            bool ready = missing.Count == 0;
            Items.Add(new WpfEnvironmentSetupCenterItem(
                T("WpfEnvironment.Category.ModelRuntime"),
                T("WpfEnvironment.Name.YoloV5Files"),
                T("WpfEnvironment.Requirement.Required"),
                ready ? T("WpfEnvironment.Status.Ready") : T("WpfEnvironment.Status.RecoveryNeeded"),
                ready
                    ? T("WpfEnvironment.Detail.YoloV5FilesReady")
                    : Format("WpfEnvironment.Detail.YoloV5FilesMissing", string.Join(", ", missing)),
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
            => LocalizationTextRuntimeService.Translate(value ?? string.Empty);

        private static string LocalizeApplicationDetail(string name, string detail)
        {
            string value = detail ?? string.Empty;
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean)
            {
                return value;
            }

            string translated = LocalizeEnvironmentText(value);
            if (!string.Equals(translated, value, StringComparison.Ordinal))
            {
                return translated;
            }

            if (string.Equals(name, "releaseManifest", StringComparison.Ordinal))
            {
                const string matchPrefix = "배포 manifest 제품/버전 일치 (";
                const string mismatchPrefix = "배포 manifest 불일치 (";
                const string readFailurePrefix = "배포 manifest 읽기 실패: ";
                if (value.StartsWith(matchPrefix, StringComparison.Ordinal)
                    && value.EndsWith(")", StringComparison.Ordinal))
                {
                    return "Release manifest product/version match ("
                        + value.Substring(matchPrefix.Length, value.Length - matchPrefix.Length - 1)
                        + ")";
                }

                if (value.StartsWith(mismatchPrefix, StringComparison.Ordinal))
                {
                    return "Release manifest mismatch ("
                        + value.Substring(mismatchPrefix.Length);
                }

                if (value.StartsWith(readFailurePrefix, StringComparison.Ordinal))
                {
                    return "Release manifest read failed: "
                        + value.Substring(readFailurePrefix.Length);
                }
            }

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
        {
            string value = detail ?? string.Empty;
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean)
            {
                return value;
            }

            string translated = LocalizeEnvironmentText(value);
            if (!string.Equals(translated, value, StringComparison.Ordinal))
            {
                return translated;
            }

            const string missingPathPrefix = "경로 미설정 / ";
            const string missingFilePrefix = "찾을 수 없음: ";
            if (value.StartsWith(missingPathPrefix, StringComparison.Ordinal))
            {
                return Format(
                    "WpfEnvironment.Detail.RuntimePathMissing",
                    GetRuntimePathGuidance(label));
            }

            if (value.StartsWith(missingFilePrefix, StringComparison.Ordinal))
            {
                int separatorIndex = value.IndexOf(" / ", missingFilePrefix.Length, StringComparison.Ordinal);
                string path = separatorIndex >= 0
                    ? value.Substring(missingFilePrefix.Length, separatorIndex - missingFilePrefix.Length)
                    : value.Substring(missingFilePrefix.Length);
                return Format("WpfEnvironment.Detail.RuntimePathNotFound", path);
            }

            if (value.StartsWith("ultralytics 패키지 없음: ", StringComparison.Ordinal))
            {
                int separatorIndex = value.IndexOf(" / ", StringComparison.Ordinal);
                string path = separatorIndex >= 0
                    ? value.Substring("ultralytics 패키지 없음: ".Length, separatorIndex - "ultralytics 패키지 없음: ".Length)
                    : value.Substring("ultralytics 패키지 없음: ".Length);
                return Format("WpfEnvironment.Detail.UltralyticsPackageMissing", path);
            }

            if (value.StartsWith("torch, torchvision, numpy 또는 Pillow가 없습니다: ", StringComparison.Ordinal))
            {
                return Format(
                    "WpfEnvironment.Detail.PyTorchPackageMissing",
                    value.Substring("torch, torchvision, numpy 또는 Pillow가 없습니다: ".Length));
            }

            if (string.Equals(label, "실행 연결", StringComparison.Ordinal)
                && value.StartsWith("PatchCore 학습과 검사는 ", StringComparison.Ordinal))
            {
                return T("WpfEnvironment.Detail.RuntimeExecutionBlocked");
            }

            if (string.Equals(label, "실행 연결", StringComparison.Ordinal)
                && value.StartsWith("YOLOv5 TCP worker로 학습과 현재 검사를 실행합니다.", StringComparison.Ordinal))
            {
                return T("WpfEnvironment.Detail.YoloV5ExecutionReady");
            }

            return value;
        }

        private static string GetRuntimePathGuidance(string label)
            => label switch
            {
                "프로젝트" => "Connect the YOLO project folder in Model Runtime Settings.",
                "모델 루트" => "Connect the YOLOv5 folder or review the model root path.",
                "실행 스크립트" => "Connect the script path used to run the model worker.",
                "검사 모델" => "Select a .pt/.onnx inspection model or save a training result as the inspection model.",
                "이미지" => "Select the image folder to inspect. Labeling can continue, but inspection requires an image folder.",
                "Python" => "Connect the Python executable or the venv Scripts folder.",
                _ => "Review the path in Model Runtime Settings."
            };
    }
}
