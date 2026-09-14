using OpenVisionLab;
using System;

namespace MvcVisionSystem
{
    // Composition-only adapter for YOLO status commands. Runtime policy and
    // process lifetime stay owned by the existing ViewModel/service.
    internal sealed class StatusPanelCommandWiring
    {
        private readonly StatusPanelCommandWiringContext context;

        internal StatusPanelCommandWiring(StatusPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureYoloStatusPanelCommands()
        {
            context.YoloStatusViewModel.ConfigureCommands(
                check: null,
                installRequirements: null,
                runSmoke: context.ExecuteRunYoloSmokeCommand,
                restartWorker: null,
                stopWorker: null);
            context.YoloStatusViewModel.ConfigureRuntimeWorkflow(
                context.YoloEnvironmentWorkflowService,
                context.CreateYoloEnvironmentCallbacks);
        }
    }

    internal sealed class StatusPanelCommandWiringContext
    {
        internal WpfYoloStatusPanelViewModel YoloStatusViewModel { get; init; }
        internal Action ExecuteRunYoloSmokeCommand { get; init; }
        internal YoloEnvironmentWorkflowService YoloEnvironmentWorkflowService { get; init; }
        internal Func<YoloEnvironmentCallbacks> CreateYoloEnvironmentCallbacks { get; init; }
    }
}
