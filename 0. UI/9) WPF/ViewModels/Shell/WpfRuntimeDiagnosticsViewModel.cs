using OpenVisionLab.Mvvm;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfRuntimeDiagnosticsViewModel : WpfObservableViewModel
    {
        private readonly RuntimeDiagnosticsService diagnosticsService;
        private string statusTitleText = "환경 점검 전";
        private string statusDetailText = "지원 자료는 버튼을 눌러야 생성되며 이미지·라벨·가중치는 제외됩니다.";
        private RuntimeSelfTestCheck lastGraphicsCapabilityCheck;
        private Action openSetupCenterAction;
        private bool isBusy;

        public WpfRuntimeDiagnosticsViewModel()
            : this(new RuntimeDiagnosticsService())
        {
        }

        public WpfRuntimeDiagnosticsViewModel(RuntimeDiagnosticsService diagnosticsService)
        {
            this.diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            RunSelfTestCommand = new RelayCommand(RunSelfTest, () => !IsBusy);
            CreateSupportBundleCommand = new RelayCommand(CreateSupportBundle, () => !IsBusy);
            OpenSetupCenterCommand = new RelayCommand(OpenSetupCenter, () => !IsBusy && openSetupCenterAction != null);
        }

        public ICommand RunSelfTestCommand { get; }
        public ICommand CreateSupportBundleCommand { get; }
        public ICommand OpenSetupCenterCommand { get; }

        public string StatusTitleText
        {
            get => statusTitleText;
            private set => SetProperty(ref statusTitleText, value);
        }

        public string StatusDetailText
        {
            get => statusDetailText;
            private set => SetProperty(ref statusDetailText, value);
        }

        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (SetProperty(ref isBusy, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        internal RuntimeDiagnosticsService DiagnosticsService => diagnosticsService;

        public void AttachGraphicsCapabilityProvider(Func<RuntimeSelfTestCheck> provider)
        {
            diagnosticsService.SetGraphicsCapabilityProvider(provider);
            lastGraphicsCapabilityCheck = null;
        }

        public void ConfigureOpenSetupCenterAction(Action action)
        {
            openSetupCenterAction = action;
            CommandManager.InvalidateRequerySuggested();
        }

        public bool EnsureViewerReadyForImageLoad(out string detail)
        {
            RuntimeSelfTestCheck check = lastGraphicsCapabilityCheck
                ?? diagnosticsService.RunGraphicsCapabilityCheck();
            if (!string.Equals(check.Status, "warning", StringComparison.Ordinal))
            {
                lastGraphicsCapabilityCheck = check;
            }

            detail = check.Detail;
            if (!string.Equals(check.Status, "fail", StringComparison.Ordinal))
            {
                return true;
            }

            StatusTitleText = "이미지 뷰어 환경 확인 필요";
            StatusDetailText = check.Detail;
            return false;
        }

        private void RunSelfTest()
        {
            Execute(
                () =>
                {
                    RuntimeSelfTestResult result = diagnosticsService.RunSelfTest();
                    RememberGraphicsCapability(result.Checks.FirstOrDefault(check =>
                        string.Equals(
                            check.Name,
                            RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                            StringComparison.Ordinal)));
                    ApplySelfTestResult(result);
                });
        }

        private void CreateSupportBundle()
        {
            Execute(
                () =>
                {
                    SupportBundleResult result = diagnosticsService.CreateSupportBundle();
                    RememberGraphicsCapability(result.SelfTest?.Checks.FirstOrDefault(check =>
                        string.Equals(
                            check.Name,
                            RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                            StringComparison.Ordinal)));
                    StatusTitleText = result.SelfTest?.FailedCount > 0
                        ? $"지원 자료 생성 완료 · 환경 실패 {result.SelfTest.FailedCount}"
                        : "지원 자료 생성 완료";
                    StatusDetailText =
                        $"{Path.GetFileName(result.ArchivePath)} · 이미지/라벨/가중치/자격 증명 제외";
                });
        }

        private void OpenSetupCenter()
        {
            openSetupCenterAction?.Invoke();
        }

        private void RememberGraphicsCapability(RuntimeSelfTestCheck check)
        {
            lastGraphicsCapabilityCheck = check != null
                && !string.Equals(check.Status, "warning", StringComparison.Ordinal)
                    ? check
                    : null;
        }

        private void ApplySelfTestResult(RuntimeSelfTestResult result)
        {
            RuntimeSelfTestCheck failure = result.Checks.FirstOrDefault(check =>
                string.Equals(check.Status, "fail", StringComparison.Ordinal));
            RuntimeSelfTestCheck warning = result.Checks.FirstOrDefault(check =>
                string.Equals(check.Status, "warning", StringComparison.Ordinal));
            RuntimeSelfTestCheck graphics = result.Checks.FirstOrDefault(check =>
                string.Equals(
                    check.Name,
                    RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                    StringComparison.Ordinal));

            if (failure != null)
            {
                StatusTitleText = $"환경 점검 필요 · 실패 {result.FailedCount}";
                StatusDetailText = failure.Detail + " · 학습/추론은 실행하지 않았습니다.";
                return;
            }

            StatusTitleText = $"환경 점검 완료 · 통과 {result.PassedCount}";
            StatusDetailText = warning != null
                ? $"{graphics?.Detail} · 경고 {result.WarningCount}건: {warning.Detail} · 학습/추론은 실행하지 않았습니다."
                : "필수 파일, 사용자 쓰기 경로, 이미지 뷰어 그래픽이 정상입니다. 학습/추론은 실행하지 않았습니다.";
        }

        private void Execute(Action action)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                StatusTitleText = "진단 작업 실패";
                StatusDetailText = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
