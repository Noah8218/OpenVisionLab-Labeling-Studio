using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeAdapterSupport
    {
        public PythonModelRuntimeAdapterSupport(
            bool isExecutionSupported,
            bool canTrain,
            bool canInspect,
            string summaryText,
            string detailText,
            string nextActionText)
        {
            IsExecutionSupported = isExecutionSupported;
            CanTrain = canTrain;
            CanInspect = canInspect;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
        }

        public bool IsExecutionSupported { get; }
        public bool CanTrain { get; }
        public bool CanInspect { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
    }

    public sealed class PythonModelRuntimeConnectionResult
    {
        public PythonModelRuntimeConnectionResult(
            PythonModelSettings settings,
            PythonModelRuntimeSelfTestReport selfTestReport,
            string summaryText,
            string detailText)
        {
            Settings = settings ?? new PythonModelSettings();
            SelfTestReport = selfTestReport ?? PythonModelRuntimeSelfTestService.BuildReport(Settings);
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
        }

        public PythonModelSettings Settings { get; }
        public PythonModelRuntimeSelfTestReport SelfTestReport { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
    }

    public sealed class PythonModelRuntimeExecutionSummary
    {
        public PythonModelRuntimeExecutionSummary(
            string titleText,
            string summaryText,
            string workerText,
            string trainingText,
            string inspectionText)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            WorkerText = workerText ?? string.Empty;
            TrainingText = trainingText ?? string.Empty;
            InspectionText = inspectionText ?? string.Empty;
        }

        public string TitleText { get; }
        public string SummaryText { get; }
        public string WorkerText { get; }
        public string TrainingText { get; }
        public string InspectionText { get; }
    }

    public sealed class PythonModelRuntimeInstallPlan
    {
        public PythonModelRuntimeInstallPlan(
            string engine,
            string titleText,
            string summaryText,
            string detailText,
            string targetEnvironmentText,
            string commandText,
            string installCommandText,
            string uninstallCommandText,
            bool isVisible,
            bool canPreviewCommand,
            bool canRunInstall,
            bool canRunUninstall,
            bool requiresInstallation,
            bool isAlreadyInstalled)
        {
            Engine = engine ?? string.Empty;
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            TargetEnvironmentText = targetEnvironmentText ?? string.Empty;
            CommandText = commandText ?? string.Empty;
            InstallCommandText = installCommandText ?? string.Empty;
            UninstallCommandText = uninstallCommandText ?? string.Empty;
            IsVisible = isVisible;
            CanPreviewCommand = canPreviewCommand;
            CanRunInstall = canRunInstall;
            CanRunUninstall = canRunUninstall;
            RequiresInstallation = requiresInstallation;
            IsAlreadyInstalled = isAlreadyInstalled;
        }

        public string Engine { get; }
        public string TitleText { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public string TargetEnvironmentText { get; }
        public string CommandText { get; }
        public string InstallCommandText { get; }
        public string UninstallCommandText { get; }
        public bool IsVisible { get; }
        public bool CanPreviewCommand { get; }
        public bool CanRunInstall { get; }
        public bool CanRunUninstall { get; }
        public bool RequiresInstallation { get; }
        public bool IsAlreadyInstalled { get; }
    }

    public sealed class PythonModelRuntimeProfile
    {
        public PythonModelRuntimeProfile(
            string engine,
            string displayName,
            string runtimeFamilyText,
            string statusText,
            string capabilityText,
            string detailText,
            string nextActionText,
            string primaryActionText,
            bool isSelected,
            bool isRuntimeConnected,
            bool canTrain,
            bool canInspect)
        {
            Engine = engine ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RuntimeFamilyText = runtimeFamilyText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            CapabilityText = capabilityText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            PrimaryActionText = primaryActionText ?? string.Empty;
            IsSelected = isSelected;
            IsRuntimeConnected = isRuntimeConnected;
            CanTrain = canTrain;
            CanInspect = canInspect;
        }

        public string Engine { get; }
        public string DisplayName { get; }
        public string RuntimeFamilyText { get; }
        public string StatusText { get; }
        public string CapabilityText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
        public string PrimaryActionText { get; }
        public bool IsSelected { get; }
        public bool IsRuntimeConnected { get; }
        public bool CanTrain { get; }
        public bool CanInspect { get; }
    }

    public sealed class PythonModelRuntimeSelfTestItem
    {
        public PythonModelRuntimeSelfTestItem(string labelText, string statusText, string detailText, bool isPassed, bool isWarning)
        {
            LabelText = labelText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            IsPassed = isPassed;
            IsWarning = isWarning;
        }

        public string LabelText { get; }
        public string StatusText { get; }
        public string DetailText { get; }
        public bool IsPassed { get; }
        public bool IsWarning { get; }
    }

    public sealed class PythonModelRuntimeSelfTestReport
    {
        public PythonModelRuntimeSelfTestReport(
            string titleText,
            string summaryText,
            string detailText,
            IEnumerable<PythonModelRuntimeSelfTestItem> items,
            bool canTrain,
            bool canInspect)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            Items = (items ?? Enumerable.Empty<PythonModelRuntimeSelfTestItem>()).ToList();
            CanTrain = canTrain;
            CanInspect = canInspect;
        }

        public string TitleText { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public IReadOnlyList<PythonModelRuntimeSelfTestItem> Items { get; }
        public bool CanTrain { get; }
        public bool CanInspect { get; }
    }

    public sealed class PythonModelRuntimeRepositoryCheck
    {
        public PythonModelRuntimeRepositoryCheck(
            string repositoryRootPath,
            IEnumerable<string> requiredRelativePaths,
            IEnumerable<string> missingRelativePaths)
        {
            RepositoryRootPath = repositoryRootPath ?? string.Empty;
            RequiredRelativePaths = (requiredRelativePaths ?? Enumerable.Empty<string>()).ToArray();
            MissingRelativePaths = (missingRelativePaths ?? Enumerable.Empty<string>()).ToArray();
        }

        public string RepositoryRootPath { get; }
        public IReadOnlyList<string> RequiredRelativePaths { get; }
        public IReadOnlyList<string> MissingRelativePaths { get; }
        public bool IsReady => MissingRelativePaths.Count == 0;
    }

    public enum PythonModelRuntimeStateKind
    {
        NotInstalled,
        Incomplete,
        Ready
    }

    public sealed class PythonModelRuntimeState
    {
        public PythonModelRuntimeState(
            PythonModelRuntimeStateKind state,
            bool canRunTraining,
            bool canRunInference,
            string summaryText,
            string detailText,
            string nextActionText)
        {
            State = state;
            CanRunTraining = canRunTraining;
            CanRunInference = canRunInference;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
        }

        public PythonModelRuntimeStateKind State { get; }
        public bool IsRuntimeInstalled => State != PythonModelRuntimeStateKind.NotInstalled;
        public bool CanRunTraining { get; }
        public bool CanRunInference { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
    }

    public sealed class PythonModelValidationResult
    {
        public PythonModelValidationResult(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
            Warnings = (warnings ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool IsValid => Errors.Count == 0;
        public string Summary => string.Join(Environment.NewLine, Errors.Concat(Warnings));
    }
}
