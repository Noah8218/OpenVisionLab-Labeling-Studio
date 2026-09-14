using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns project-level class catalog mutation and persistence. The Shell keeps
    /// control events, live annotation updates, and presentation refresh.
    /// </summary>
    public class ClassCatalogWorkflowService
    {
        private readonly ProjectRecipeSessionService projectRecipeSessionService;

        public ClassCatalogWorkflowService(ProjectRecipeSessionService projectRecipeSessionService)
        {
            this.projectRecipeSessionService = projectRecipeSessionService
                ?? throw new ArgumentNullException(nameof(projectRecipeSessionService));
        }

        public LabelClass EnsureClassItem(LabelingProjectData data, string className)
        {
            ArgumentNullException.ThrowIfNull(data);
            data.ClassNamedList ??= new List<LabelClass>();

            string normalizedName = ClassCatalogService.NormalizeClassName(className);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "Defect";
            }

            LabelClass existing = data.ClassNamedList.FirstOrDefault(item =>
                string.Equals(item?.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            if (ClassCatalogService.TryAddClass(data, normalizedName, out LabelClass added))
            {
                return added;
            }

            return new LabelClass
            {
                Text = normalizedName,
                DrawColor = Color.FromArgb(34, 197, 94)
            };
        }

        public WpfClassCatalogMutationResult Add(
            LabelingProjectData data,
            string recipeName,
            string requestedClassName)
        {
            ArgumentNullException.ThrowIfNull(data);
            string className = ClassCatalogService.NormalizeClassName(requestedClassName);
            if (string.IsNullOrWhiteSpace(className))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.InvalidClassName,
                    className);
            }

            if (!ClassCatalogService.TryAddClass(data, className, out LabelClass addedClass))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ClassAlreadyExists,
                    className);
            }

            if (!TrySave(data, recipeName, updateYoloDataYaml: true, out string errorMessage))
            {
                data.ClassNamedList.Remove(addedClass);
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.Persistence,
                    className,
                    errorMessage);
            }

            return WpfClassCatalogMutationResult.CreateSuccess(addedClass);
        }

        public WpfClassCatalogMutationResult Rename(
            LabelingProjectData data,
            string recipeName,
            string currentName,
            string requestedNewName)
        {
            ArgumentNullException.ThrowIfNull(data);
            string normalizedCurrentName = ClassCatalogService.NormalizeClassName(currentName);
            string normalizedNewName = ClassCatalogService.NormalizeClassName(requestedNewName);
            if (string.IsNullOrWhiteSpace(normalizedCurrentName)
                || string.IsNullOrWhiteSpace(normalizedNewName))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.InvalidClassName,
                    normalizedNewName);
            }

            LabelClass currentClass = data.ClassNamedList?.FirstOrDefault(item =>
                string.Equals(item?.Text, normalizedCurrentName, StringComparison.OrdinalIgnoreCase));
            if (currentClass == null)
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ClassNotFound,
                    normalizedCurrentName);
            }

            if (currentClass.IsArchived)
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ArchivedClass,
                    normalizedCurrentName);
            }

            string previousName = currentClass.Text;
            if (!ClassCatalogService.TryRenameClass(data, previousName, normalizedNewName, out LabelClass renamedClass))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ClassAlreadyExists,
                    normalizedNewName);
            }

            if (!TrySave(data, recipeName, updateYoloDataYaml: true, out string errorMessage))
            {
                ClassCatalogService.TryRenameClass(data, renamedClass.Text, previousName, out _);
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.Persistence,
                    previousName,
                    errorMessage);
            }

            return WpfClassCatalogMutationResult.CreateSuccess(renamedClass, previousName);
        }

        public WpfClassCatalogMutationResult ToggleArchive(
            LabelingProjectData data,
            string recipeName,
            string requestedClassName)
        {
            ArgumentNullException.ThrowIfNull(data);
            string className = ClassCatalogService.NormalizeClassName(requestedClassName);
            if (string.IsNullOrWhiteSpace(className))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.InvalidClassName,
                    className);
            }

            LabelClass classItem = data.ClassNamedList?.FirstOrDefault(item =>
                string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
            if (classItem == null)
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ClassNotFound,
                    className);
            }

            bool wasArchived = classItem.IsArchived;
            bool changed = wasArchived
                ? ClassCatalogService.TryRestoreClass(data, className, out _)
                : ClassCatalogService.TryArchiveClass(data, className, out _);
            if (!changed)
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    wasArchived
                        ? WpfClassCatalogOperationFailure.ClassNotFound
                        : WpfClassCatalogOperationFailure.LastActiveClass,
                    className);
            }

            if (!TrySave(data, recipeName, updateYoloDataYaml: false, out string errorMessage))
            {
                classItem.IsArchived = wasArchived;
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.Persistence,
                    className,
                    errorMessage);
            }

            return WpfClassCatalogMutationResult.CreateSuccess(classItem, wasArchived: wasArchived);
        }

        public WpfClassCatalogMutationResult SetColor(
            LabelingProjectData data,
            string recipeName,
            string requestedClassName,
            Color color)
        {
            ArgumentNullException.ThrowIfNull(data);
            string className = ClassCatalogService.NormalizeClassName(requestedClassName);
            if (string.IsNullOrWhiteSpace(className))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.InvalidClassName,
                    className);
            }

            LabelClass classItem = data.ClassNamedList?.FirstOrDefault(item =>
                string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
            if (classItem == null)
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ClassNotFound,
                    className);
            }

            Color previousColor = classItem.DrawColor;
            if (!ClassCatalogService.TrySetClassColor(data, className, color, out LabelClass changedClass))
            {
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.ClassNotFound,
                    className);
            }

            if (!TrySave(data, recipeName, updateYoloDataYaml: true, out string errorMessage))
            {
                changedClass.DrawColor = previousColor;
                return WpfClassCatalogMutationResult.CreateFailure(
                    WpfClassCatalogOperationFailure.Persistence,
                    className,
                    errorMessage);
            }

            return WpfClassCatalogMutationResult.CreateSuccess(changedClass);
        }

        public WpfClassCatalogOutputRootResult SaveOutputRoot(
            LabelingProjectData data,
            string recipeName,
            string requestedOutputRootPath)
        {
            ArgumentNullException.ThrowIfNull(data);
            string outputRootPath = (requestedOutputRootPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return WpfClassCatalogOutputRootResult.CreateFailure(
                    WpfClassCatalogOperationFailure.InvalidOutputRoot,
                    string.Empty,
                    "저장 경로를 입력하거나 선택하세요.");
            }

            string previousOutputRootPath = data.OutputRootPath ?? string.Empty;
            try
            {
                data.ConfigureOutputRoot(outputRootPath);
                if (!TrySave(data, recipeName, updateYoloDataYaml: true, out string errorMessage))
                {
                    data.ConfigureOutputRoot(previousOutputRootPath);
                    return WpfClassCatalogOutputRootResult.CreateFailure(
                        WpfClassCatalogOperationFailure.Persistence,
                        previousOutputRootPath,
                        errorMessage);
                }

                return WpfClassCatalogOutputRootResult.CreateSuccess(
                    previousOutputRootPath,
                    data.OutputRootPath);
            }
            catch (Exception error)
            {
                data.ConfigureOutputRoot(previousOutputRootPath);
                return WpfClassCatalogOutputRootResult.CreateFailure(
                    WpfClassCatalogOperationFailure.Persistence,
                    previousOutputRootPath,
                    error.Message);
            }
        }

        private bool TrySave(
            LabelingProjectData data,
            string recipeName,
            bool updateYoloDataYaml,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                RecipeConfigurationSaveResult result = projectRecipeSessionService.SaveConfiguration(
                    data,
                    recipeName,
                    updateYoloDataYaml);
                if (!result.IsSuccess)
                {
                    errorMessage = result.ErrorMessage;
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                errorMessage = error.Message;
                return false;
            }
        }
    }

    public enum WpfClassCatalogOperationFailure
    {
        None,
        InvalidClassName,
        ClassAlreadyExists,
        ClassNotFound,
        ArchivedClass,
        LastActiveClass,
        InvalidOutputRoot,
        Persistence
    }

    public enum WpfClassCatalogMutationKind
    {
        Add,
        Rename,
        ToggleArchive,
        SetColor
    }

    [Obsolete("Use ClassCatalogWorkflowService.", false)]
    public sealed class WpfClassCatalogWorkflowService : ClassCatalogWorkflowService
    {
        public WpfClassCatalogWorkflowService(ProjectRecipeSessionService projectRecipeSessionService)
            : base(projectRecipeSessionService)
        {
        }
    }

    public sealed class WpfClassCatalogMutationResult
    {
        private WpfClassCatalogMutationResult(
            WpfClassCatalogOperationFailure failure,
            LabelClass classItem,
            string className,
            string previousClassName,
            string errorMessage,
            bool wasArchived)
        {
            Failure = failure;
            ClassItem = classItem;
            ClassName = className ?? string.Empty;
            PreviousClassName = previousClassName ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            WasArchived = wasArchived;
        }

        public bool IsSuccess => Failure == WpfClassCatalogOperationFailure.None;

        public WpfClassCatalogOperationFailure Failure { get; }

        public LabelClass ClassItem { get; }

        public string ClassName { get; }

        public string PreviousClassName { get; }

        public string ErrorMessage { get; }

        public bool WasArchived { get; }

        internal static WpfClassCatalogMutationResult CreateSuccess(
            LabelClass classItem,
            string previousClassName = "",
            bool wasArchived = false)
        {
            return new WpfClassCatalogMutationResult(
                WpfClassCatalogOperationFailure.None,
                classItem,
                classItem?.Text,
                previousClassName,
                string.Empty,
                wasArchived);
        }

        internal static WpfClassCatalogMutationResult CreateFailure(
            WpfClassCatalogOperationFailure failure,
            string className,
            string errorMessage = "")
        {
            return new WpfClassCatalogMutationResult(
                failure,
                null,
                className,
                string.Empty,
                errorMessage,
                false);
        }
    }

    public sealed class WpfClassCatalogOutputRootResult
    {
        private WpfClassCatalogOutputRootResult(
            WpfClassCatalogOperationFailure failure,
            string previousOutputRootPath,
            string outputRootPath,
            string errorMessage)
        {
            Failure = failure;
            PreviousOutputRootPath = previousOutputRootPath ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsSuccess => Failure == WpfClassCatalogOperationFailure.None;

        public WpfClassCatalogOperationFailure Failure { get; }

        public string PreviousOutputRootPath { get; }

        public string OutputRootPath { get; }

        public string ErrorMessage { get; }

        internal static WpfClassCatalogOutputRootResult CreateSuccess(
            string previousOutputRootPath,
            string outputRootPath)
        {
            return new WpfClassCatalogOutputRootResult(
                WpfClassCatalogOperationFailure.None,
                previousOutputRootPath,
                outputRootPath,
                string.Empty);
        }

        internal static WpfClassCatalogOutputRootResult CreateFailure(
            WpfClassCatalogOperationFailure failure,
            string previousOutputRootPath,
            string errorMessage)
        {
            return new WpfClassCatalogOutputRootResult(
                failure,
                previousOutputRootPath,
                previousOutputRootPath,
                errorMessage);
        }
    }
}
