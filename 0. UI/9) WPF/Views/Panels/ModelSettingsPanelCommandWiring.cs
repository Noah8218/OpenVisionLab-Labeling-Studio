using MvcVisionSystem._1._Core;
using System;

namespace MvcVisionSystem
{
    // Composition-only adapter for the YOLO model settings panel. The existing
    // ViewModel owns command entry and path-selection policy; the Shell only
    // supplies WPF/runtime callbacks at the composition boundary.
    internal sealed class ModelSettingsPanelCommandWiring
    {
        private readonly ModelSettingsPanelCommandWiringContext context;

        internal ModelSettingsPanelCommandWiring(ModelSettingsPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureModelSettingsPanelCommands()
        {
            context.ModelSettingsViewModel.ConfigureCommands(
                browsePython: null,
                browseProjectRoot: null,
                browseClientScript: null,
                browseWeights: null,
                browseImageRoot: null,
                saveSettings: context.ExecuteSaveSettingsCommand,
                resetSettings: context.ExecuteResetSettingsCommand,
                runtimeProfileAction: context.ExecuteRuntimeProfileActionCommand,
                runtimeInstallPackageAction: context.ExecuteRuntimeInstallPackageCommand,
                runtimeUninstallPackageAction: context.ExecuteRuntimeUninstallPackageCommand,
                cancelChanges: context.CancelSettingsChanges);
            context.ModelSettingsViewModel.ConfigurePathSelectionWorkflow(context.CreatePathSelectionCallbacks);
        }
    }

    internal sealed class ModelSettingsPanelCommandWiringContext
    {
        internal WpfYoloModelSettingsPanelViewModel ModelSettingsViewModel { get; init; }
        internal Action ExecuteSaveSettingsCommand { get; init; }
        internal Action ExecuteResetSettingsCommand { get; init; }
        internal Action<string> ExecuteRuntimeProfileActionCommand { get; init; }
        internal Action ExecuteRuntimeInstallPackageCommand { get; init; }
        internal Action ExecuteRuntimeUninstallPackageCommand { get; init; }
        internal Action CancelSettingsChanges { get; init; }
        internal Func<YoloModelSettingsPathCallbacks> CreatePathSelectionCallbacks { get; init; }
    }
}
