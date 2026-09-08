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
                return Blocked("현재 이미지의 라벨을 먼저 `라벨 저장`으로 반영하세요. 아카이브가 라벨 저장을 대신하지 않습니다.");
            }

            if (state.PendingCandidateCount > 0)
            {
                return Blocked("미확정 AI 후보를 먼저 확정하거나 스킵하세요. 아카이브가 후보를 자동 확정하지 않습니다.");
            }

            string activeWork = (state.ActiveWorkNames ?? Array.Empty<string>())
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            if (!string.IsNullOrWhiteSpace(activeWork))
            {
                return Blocked($"진행 중인 작업을 완료하거나 중지한 뒤 다시 시도하세요: {activeWork.Trim()}");
            }

            if (operation == WpfProjectArchiveOperation.Export)
            {
                if (!ProjectRecipeService.IsValidRecipeName(recipeName))
                {
                    return Blocked("내보낼 Recipe를 먼저 선택하세요.");
                }

                if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                {
                    return Blocked("마지막으로 명시 저장된 Recipe 설정이 없습니다. `설정 저장` 후 다시 시도하세요.");
                }

                if (string.IsNullOrWhiteSpace(datasetRootPath) || !Directory.Exists(datasetRootPath))
                {
                    return Blocked("저장된 Recipe가 가리키는 데이터셋 폴더를 찾을 수 없습니다.");
                }
            }

            return new WpfProjectArchivePreflightResult
            {
                CanProceed = true,
                StatusText = operation == WpfProjectArchiveOperation.Export
                    ? "마지막으로 저장된 Recipe와 데이터셋을 아카이브할 수 있습니다."
                    : "아카이브를 새 Recipe와 새 데이터셋 폴더로 가져올 수 있습니다."
            };
        }

        private static void EnsureCanProceed(WpfProjectArchivePreflightResult preflight)
        {
            if (preflight == null || !preflight.CanProceed)
            {
                throw new InvalidOperationException(
                    preflight?.StatusText ?? "프로젝트 아카이브 작업을 시작할 수 없습니다.");
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
