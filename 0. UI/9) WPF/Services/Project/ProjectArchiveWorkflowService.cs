using System;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public enum WpfProjectArchiveOperation
    {
        Export,
        Import
    }

    public sealed class WpfProjectArchivePreflightResult
    {
        public bool CanProceed { get; init; }

        public string StatusText { get; init; } = string.Empty;
    }

    public sealed class ProjectArchiveExportRequest
    {
        public ApplicationCloseState CloseState { get; init; }

        public string RecipeName { get; init; } = string.Empty;

        public string ConfigPath { get; init; } = string.Empty;

        public string DatasetRootPath { get; init; } = string.Empty;

        public string RecipeDirectory { get; init; } = string.Empty;

        public string ArchivePath { get; init; } = string.Empty;
    }

    public sealed class ProjectArchiveImportRequest
    {
        public ApplicationCloseState CloseState { get; init; }

        public string ArchivePath { get; init; } = string.Empty;

        public string RecipeRootDirectory { get; init; } = string.Empty;

        public string DatasetParentDirectory { get; init; } = string.Empty;
    }

    /// <summary>
    /// Owns project archive preflight and operation-request dispatch. The Shell
    /// keeps file pickers, dialogs, status presentation, and post-import UI
    /// refresh while the portable archive service keeps the file transaction.
    /// </summary>
    public sealed class ProjectArchiveWorkflowService
    {
        private readonly PortableProjectArchiveService portableProjectArchiveService;

        public ProjectArchiveWorkflowService(PortableProjectArchiveService portableProjectArchiveService = null)
        {
            this.portableProjectArchiveService = portableProjectArchiveService
                ?? new PortableProjectArchiveService();
        }

        public WpfProjectArchivePreflightResult CheckExport(
            ApplicationCloseState state,
            string recipeName,
            string configPath,
            string datasetRootPath)
            => Check(
                WpfProjectArchiveOperation.Export,
                state,
                recipeName,
                configPath,
                datasetRootPath);

        public WpfProjectArchivePreflightResult CheckImport(ApplicationCloseState state)
            => Check(WpfProjectArchiveOperation.Import, state);

        public WpfProjectArchiveExportResult Export(ProjectArchiveExportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            EnsureCanProceed(CheckExport(
                request.CloseState,
                request.RecipeName,
                request.ConfigPath,
                request.DatasetRootPath));
            return portableProjectArchiveService.Export(
                request.RecipeName,
                request.RecipeDirectory,
                request.DatasetRootPath,
                request.ArchivePath);
        }

        public WpfProjectArchiveImportResult Import(ProjectArchiveImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            EnsureCanProceed(CheckImport(request.CloseState));
            return portableProjectArchiveService.Import(
                request.ArchivePath,
                request.RecipeRootDirectory,
                request.DatasetParentDirectory);
        }

        private static WpfProjectArchivePreflightResult Check(
            WpfProjectArchiveOperation operation,
            ApplicationCloseState state,
            string recipeName = "",
            string configPath = "",
            string datasetRootPath = "")
        {
            state ??= new ApplicationCloseState();
            if (state.HasUnsavedAnnotations)
            {
                return Blocked(OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.UnsavedLabels"));
            }

            if (state.PendingCandidateCount > 0)
            {
                return Blocked(OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.PendingSuggestions"));
            }

            string activeWork = (state.ActiveWorkNames ?? Array.Empty<string>())
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            if (!string.IsNullOrWhiteSpace(activeWork))
            {
                return Blocked(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.ActiveWork"), activeWork.Trim()));
            }

            if (operation == WpfProjectArchiveOperation.Export)
            {
                if (!ProjectRecipeService.IsValidRecipeName(recipeName))
                {
                    return Blocked(OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.SelectRecipe"));
                }

                if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                {
                    return Blocked(OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.SettingsMissing"));
                }

                if (string.IsNullOrWhiteSpace(datasetRootPath) || !Directory.Exists(datasetRootPath))
                {
                    return Blocked(OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.DatasetMissing"));
                }
            }

            return new WpfProjectArchivePreflightResult
            {
                CanProceed = true,
                StatusText = operation == WpfProjectArchiveOperation.Export
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.ExportReady")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.ImportReady")
            };
        }

        private static void EnsureCanProceed(WpfProjectArchivePreflightResult preflight)
        {
            if (preflight == null || !preflight.CanProceed)
            {
                throw new InvalidOperationException(
                    preflight?.StatusText ?? OpenVisionLab.OpenVisionLanguageService.T("WpfArchive.Status.CannotStart"));
            }
        }

        private static WpfProjectArchivePreflightResult Blocked(string statusText)
            => new WpfProjectArchivePreflightResult
            {
                CanProceed = false,
                StatusText = statusText ?? string.Empty
            };
    }
}
