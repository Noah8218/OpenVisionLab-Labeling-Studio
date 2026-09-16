using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class DatasetInterchangeRequest
    {
        public LabelingProjectData Data { get; set; }

        public string RecipeName { get; set; } = string.Empty;

        public string FormatKey { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public string ImageRoot { get; set; } = string.Empty;

        public string TargetPath { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = YoloDatasetSplitService.TrainMode;

        // Exporters do not silently discard semantic data. The caller must
        // carry an explicit acknowledgement when the preflight manifest has
        // conditional or lost fields.
        public bool ConfirmLossyExport { get; set; }
    }

    public sealed class DatasetInterchangeLossManifest
    {
        public int Version { get; set; } = 1;

        public string FormatKey { get; set; } = string.Empty;

        public string TargetPath { get; set; } = string.Empty;

        public List<DatasetInterchangeLossManifestEntry> Entries { get; } =
            new List<DatasetInterchangeLossManifestEntry>();

        [JsonIgnore]
        public bool RequiresConfirmation => Entries.Any(entry =>
            !string.Equals(entry?.Level, nameof(SegmentationPreservationLevel.Preserved), StringComparison.Ordinal));

        [JsonIgnore]
        public bool IsLossless => !RequiresConfirmation;
    }

    public sealed class DatasetInterchangeLossManifestEntry
    {
        public string Semantic { get; set; } = string.Empty;

        public string Level { get; set; } = string.Empty;

        public int AffectedCount { get; set; }

        public string Detail { get; set; } = string.Empty;
    }

    public sealed class DatasetInterchangePreflightReport
    {
        public DatasetExportCapability Capability { get; set; }

        public bool IsDryRun { get; set; }

        public bool WasApplied { get; set; }

        public bool CanApply { get; set; }

        public bool SourceUnchanged { get; set; }

        public bool RequestedTargetUnchanged { get; set; }

        public int ImageCount { get; set; }

        public int AnnotationCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedCount { get; set; }

        public string SourceFingerprint { get; set; } = string.Empty;

        public string RequestedTargetFingerprint { get; set; } = string.Empty;

        public string StatusText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public List<string> Issues { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public DatasetInterchangeLossManifest LossManifest { get; set; } =
            new DatasetInterchangeLossManifest();

        public string LossManifestPath { get; set; } = string.Empty;

        public bool LossConfirmationAccepted { get; set; }

        public bool RequiresLossConfirmation => string.Equals(
                Capability?.Direction,
                "export",
                StringComparison.OrdinalIgnoreCase)
            && (LossManifest?.RequiresConfirmation == true || Warnings.Count > 0);
    }

    public sealed class DatasetInterchangePreflightService
    {
        private static readonly string[] SupportedFormatKeys =
        {
            "coco-detection-json",
            "pascal-voc-detection",
            "label-studio-detection-json",
            "cvat-images-archive",
            "coco-segmentation-json",
            "label-studio-segmentation-json",
            "cvat-segmentation-archive",
            "coco-detection-import",
            "pascal-voc-detection-import",
            "label-studio-detection-import",
            "cvat-detection-import",
            "coco-segmentation-import",
            "label-studio-segmentation-import",
            "cvat-segmentation-import"
        };

        public IReadOnlyList<DatasetExportCapability> BuildSupportedCapabilities()
            => DatasetExportCapabilityService.BuildImplementedCapabilities()
                .Where(item => SupportedFormatKeys.Contains(item.FormatKey, StringComparer.Ordinal))
                .ToList();

        public DatasetInterchangePreflightReport DryRun(DatasetInterchangeRequest request)
            => Execute(request, isDryRun: true);

        public DatasetInterchangePreflightReport Apply(DatasetInterchangeRequest request)
            => Execute(request, isDryRun: false);

        private DatasetInterchangePreflightReport Execute(DatasetInterchangeRequest request, bool isDryRun)
        {
            DatasetExportCapability capability = ResolveCapability(request?.FormatKey);
            var report = new DatasetInterchangePreflightReport
            {
                Capability = capability,
                IsDryRun = isDryRun,
                WasApplied = !isDryRun
            };

            ValidateRequest(request, capability, report);
            if (report.Issues.Count > 0)
            {
                CompleteReport(report);
                return report;
            }

            bool isImport = string.Equals(capability.Direction, "import", StringComparison.OrdinalIgnoreCase);
            string sourceBefore = ComputeSourceFingerprint(request, capability);
            string requestedTargetEvidencePath = isImport
                ? request.Data.OutputRootPath
                : request.TargetPath;
            string requestedTargetBefore = ComputePathFingerprint(requestedTargetEvidencePath);
            report.LossConfirmationAccepted = request?.ConfirmLossyExport == true;

            if (!isImport)
            {
                report.LossManifestPath = BuildLossManifestPath(request.TargetPath, capability);
                try
                {
                    report.LossManifest = BuildLossManifest(request, capability);
                    AppendManifestWarnings(report);
                }
                catch (Exception ex)
                {
                    report.Issues.Add("내보내기 보존 manifest를 계산하지 못했습니다: " + ex.GetBaseException().Message);
                }

                if (!isDryRun
                    && report.RequiresLossConfirmation
                    && !report.LossConfirmationAccepted)
                {
                    report.WasApplied = false;
                    report.SourceFingerprint = sourceBefore;
                    report.RequestedTargetFingerprint = requestedTargetBefore;
                    report.SourceUnchanged = true;
                    report.RequestedTargetUnchanged = true;
                    report.Issues.Add(
                        "보존되지 않거나 변환 조건이 있는 annotation 의미가 있습니다. manifest를 확인하고 손실 내보내기를 명시적으로 승인하거나 다른 형식을 선택하세요.");
                    CompleteReport(report);
                    return report;
                }
            }

            string temporaryRoot = string.Empty;
            LabelingProjectData stagedImportData = null;

            try
            {
                object conversionResult = null;
                if (isDryRun)
                {
                    temporaryRoot = Path.Combine(
                        Path.GetTempPath(),
                        "OpenVisionLab.LabelingStudio",
                        "interchange-preflight-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(temporaryRoot);
                    conversionResult = isImport
                        ? ExecuteImport(CloneForOutputRoot(request.Data, temporaryRoot), request, capability)
                        : ExecuteExport(request.Data, BuildDryRunTargetPath(temporaryRoot, request.TargetPath, capability), capability);
                }
                else if (isImport)
                {
                    temporaryRoot = BuildImportStageRoot(request.Data.OutputRootPath);
                    Directory.CreateDirectory(temporaryRoot);
                    stagedImportData = CloneForOutputRoot(request.Data, temporaryRoot);
                    conversionResult = ExecuteImport(stagedImportData, request, capability);
                }
                else
                {
                    conversionResult = ExecuteExport(request.Data, request.TargetPath, capability);
                }

                PopulateCounts(report, conversionResult);
                AppendResultWarnings(report, conversionResult);
                if (!isDryRun && isImport && report.SkippedCount == 0)
                {
                    PromoteStagedImport(stagedImportData, request.Data, request.RecipeName);
                }
                else if (!isDryRun && !isImport && report.Issues.Count == 0)
                {
                    WriteLossManifest(report.LossManifest, report.LossManifestPath);
                }
            }
            catch (Exception ex)
            {
                report.Issues.Add(ex.GetBaseException().Message);
            }
            finally
            {
                report.SourceFingerprint = sourceBefore;
                report.RequestedTargetFingerprint = requestedTargetBefore;
                report.SourceUnchanged = string.Equals(
                    sourceBefore,
                    ComputeSourceFingerprint(request, capability),
                    StringComparison.Ordinal);
                report.RequestedTargetUnchanged = !isDryRun
                    || string.Equals(
                        requestedTargetBefore,
                        ComputePathFingerprint(requestedTargetEvidencePath),
                        StringComparison.Ordinal);

                if (!report.SourceUnchanged)
                {
                    report.Issues.Add("\uBCC0\uD658 \uC2E4\uD589 \uC911 \uC6D0\uBCF8 \uB370\uC774\uD130 \uBCC0\uACBD\uC774 \uAC10\uC9C0\uB418\uC5C8\uC2B5\uB2C8\uB2E4.");
                }

                if (isDryRun && !report.RequestedTargetUnchanged)
                {
                    report.Issues.Add("Dry-run\uC774 \uC694\uCCAD\uD55C \uB300\uC0C1 \uACBD\uB85C\uB97C \uBCC0\uACBD\uD588\uC2B5\uB2C8\uB2E4.");
                }

                if (!string.IsNullOrWhiteSpace(temporaryRoot) && Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
            }

            if (report.SkippedCount > 0)
            {
                report.Issues.Add(
                    $"\uC798\uBABB\uB418\uC5C8\uAC70\uB098 \uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 \uC774\uBBF8\uC9C0/\uC5B4\uB178\uD14C\uC774\uC158 {report.SkippedCount}\uAC74\uC774 \uAC74\uB108\uB6F0\uC5B4\uC9D1\uB2C8\uB2E4. \uC801\uC6A9 \uC804\uC5D0 \uC218\uC815\uD558\uC138\uC694.");
            }

            CompleteReport(report);
            return report;
        }

        private static DatasetExportCapability ResolveCapability(string formatKey)
            => DatasetExportCapabilityService.BuildImplementedCapabilities()
                .FirstOrDefault(item =>
                    SupportedFormatKeys.Contains(item.FormatKey, StringComparer.Ordinal)
                    && string.Equals(item.FormatKey, formatKey, StringComparison.Ordinal));

        private static void ValidateRequest(
            DatasetInterchangeRequest request,
            DatasetExportCapability capability,
            DatasetInterchangePreflightReport report)
        {
            if (request?.Data == null)
            {
                report.Issues.Add("\uD604\uC7AC \uB370\uC774\uD130\uC14B\uC744 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.");
                return;
            }

            if (capability == null)
            {
                report.Issues.Add("\uC9C0\uC6D0\uD558\uB294 \uBCC0\uD658 \uD615\uC2DD\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            LabelingDatasetPurpose purpose = request.Data.ProjectSettings?.DatasetPurpose
                ?? LabelingDatasetPurpose.ObjectDetection;
            if (!string.Equals(capability.DatasetPurpose, purpose.ToString(), StringComparison.Ordinal))
            {
                report.Issues.Add(
                    $"\uC120\uD0DD\uD55C \uC791\uC5C5\uC740 {capability.DatasetPurpose} \uBAA9\uC801\uC774 \uD544\uC694\uD558\uC9C0\uB9CC \uD604\uC7AC \uB370\uC774\uD130\uC14B \uBAA9\uC801\uC740 {purpose}\uC785\uB2C8\uB2E4.");
            }

            bool isImport = string.Equals(capability.Direction, "import", StringComparison.OrdinalIgnoreCase);
            if (isImport)
            {
                bool sourceMustBeDirectory = string.Equals(
                    capability.FormatKey,
                    "pascal-voc-detection-import",
                    StringComparison.Ordinal);
                bool sourceExists = sourceMustBeDirectory
                    ? Directory.Exists(request.SourcePath)
                    : File.Exists(request.SourcePath);
                if (!sourceExists)
                {
                    report.Issues.Add(sourceMustBeDirectory
                        ? "\uC874\uC7AC\uD558\uB294 \uC5B4\uB178\uD14C\uC774\uC158 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694."
                        : "\uC874\uC7AC\uD558\uB294 \uC5B4\uB178\uD14C\uC774\uC158 \uB610\uB294 \uC555\uCD95 \uD30C\uC77C\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                }

                if (RequiresImageRoot(capability.FormatKey) && !Directory.Exists(request.ImageRoot))
                {
                    report.Issues.Add("\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                }

                if (!IsSupportedSplit(request.TargetSplit))
                {
                    report.Issues.Add("\uB300\uC0C1 \uBD84\uD560\uC740 train, valid, test \uC911 \uD558\uB098\uC5EC\uC57C \uD569\uB2C8\uB2E4.");
                }
            }
            else
            {
                if (!Directory.Exists(request.Data.OutputRootPath))
                {
                    report.Issues.Add("\uD604\uC7AC \uB370\uC774\uD130\uC14B \uB8E8\uD2B8\uAC00 \uC874\uC7AC\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.");
                }

                if (string.IsNullOrWhiteSpace(request.TargetPath))
                {
                    report.Issues.Add("\uB0B4\uBCF4\uB0B4\uAE30 \uB300\uC0C1\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                }
                else if (IsPathInside(request.TargetPath, request.Data.OutputRootPath))
                {
                    report.Issues.Add("\uC6D0\uBCF8 \uB370\uC774\uD130\uC14B \uB8E8\uD2B8 \uBC16\uC758 \uB0B4\uBCF4\uB0B4\uAE30 \uB300\uC0C1\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
                }
            }
        }

        private static object ExecuteExport(LabelingProjectData data, string targetPath, DatasetExportCapability capability)
            => capability.FormatKey switch
            {
                "coco-detection-json" => CocoDetectionExportService.ExportDataset(data, targetPath),
                "pascal-voc-detection" => PascalVocDetectionExportService.ExportDataset(data, targetPath),
                "label-studio-detection-json" => LabelStudioDetectionExportService.ExportDataset(data, targetPath),
                "cvat-images-archive" => CvatImageTaskArchiveExportService.ExportDataset(data, targetPath),
                "coco-segmentation-json" => CocoSegmentationExportService.ExportDataset(data, targetPath),
                "label-studio-segmentation-json" => LabelStudioSegmentationExportService.ExportDataset(data, targetPath),
                "cvat-segmentation-archive" => CvatSegmentationArchiveExportService.ExportDataset(data, targetPath),
                _ => throw new NotSupportedException($"Unsupported export format: {capability.FormatKey}")
            };

        private static object ExecuteImport(
            LabelingProjectData data,
            DatasetInterchangeRequest request,
            DatasetExportCapability capability)
            => capability.FormatKey switch
            {
                "coco-detection-import" => CocoDetectionImportService.ImportDataset(
                    data, request.SourcePath, request.ImageRoot, request.TargetSplit),
                "pascal-voc-detection-import" => PascalVocDetectionImportService.ImportDirectory(
                    data, request.SourcePath, request.ImageRoot, request.TargetSplit),
                "label-studio-detection-import" => LabelStudioDetectionImportService.ImportTasks(
                    data, request.SourcePath, request.ImageRoot, request.TargetSplit),
                "cvat-detection-import" => CvatDetectionImportService.ImportArchive(
                    data, request.SourcePath, request.TargetSplit),
                "coco-segmentation-import" => CocoSegmentationImportService.ImportDataset(
                    data, request.SourcePath, request.ImageRoot, request.TargetSplit),
                "label-studio-segmentation-import" => LabelStudioSegmentationImportService.ImportTasks(
                    data, request.SourcePath, request.ImageRoot, request.TargetSplit),
                "cvat-segmentation-import" => CvatSegmentationImportService.ImportArchive(
                    data, request.SourcePath, request.TargetSplit),
                _ => throw new NotSupportedException($"Unsupported import format: {capability.FormatKey}")
            };

        private static LabelingProjectData CloneForOutputRoot(LabelingProjectData source, string outputRoot)
        {
            var clone = new LabelingProjectData
            {
                ProjectSettings = new LabelingProjectSettings
                {
                    DatasetPurpose = source.ProjectSettings?.DatasetPurpose
                        ?? LabelingDatasetPurpose.ObjectDetection
                }
            };
            clone.ConfigureOutputRoot(outputRoot);
            clone.ClassNamedList = CloneClasses(source.ClassNamedList);
            return clone;
        }

        private static string BuildImportStageRoot(string targetRoot)
        {
            string normalizedTarget = Path.GetFullPath(targetRoot ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.GetDirectoryName(normalizedTarget)
                ?? throw new InvalidOperationException("Dataset root must have a parent directory.");
            string name = Path.GetFileName(normalizedTarget);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Dataset root must not be a drive root.");
            }

            Directory.CreateDirectory(parent);
            return Path.Combine(parent, $".{name}.interchange-stage-{Guid.NewGuid():N}");
        }

        private static void PromoteStagedImport(
            LabelingProjectData stagedData,
            LabelingProjectData targetData,
            string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                throw new InvalidOperationException("An active Recipe is required before imported classes can be applied.");
            }

            string stageRoot = Path.GetFullPath(stagedData.OutputRootPath);
            string targetRoot = Path.GetFullPath(targetData.OutputRootPath);
            List<string> stagedFiles = Directory.EnumerateFiles(stageRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(
                    Path.GetRelativePath(stageRoot, path),
                    "data.yaml",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetRelativePath(stageRoot, path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> targetDirectories = new[] { targetRoot }
                .Concat(Directory.EnumerateDirectories(stageRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetFullPath(Path.Combine(targetRoot, Path.GetRelativePath(stageRoot, path)))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path.Length)
                .ToList();
            List<string> missingDirectories = targetDirectories
                .Where(path => !Directory.Exists(path))
                .OrderByDescending(path => path.Length)
                .ToList();
            targetData.ClassNamedList ??= new List<LabelClass>();
            int originalClassCount = targetData.ClassNamedList.Count;

            try
            {
                foreach (string targetDirectory in targetDirectories)
                {
                    if (!IsPathInside(targetDirectory, targetRoot))
                    {
                        throw new InvalidDataException("Staged import produced a directory outside the dataset root.");
                    }

                    Directory.CreateDirectory(targetDirectory);
                }

                RecipeConfigurationSaveResult pairResult = null;
                bool committed = AnnotationFilePersistence.ExecuteTransaction(() =>
                {
                    foreach (string stagedPath in stagedFiles)
                    {
                        string relativePath = Path.GetRelativePath(stageRoot, stagedPath);
                        string targetPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                        if (!IsPathInside(targetPath, targetRoot))
                        {
                            throw new InvalidDataException("Staged import produced a path outside the dataset root.");
                        }

                        AnnotationFilePersistence.WriteAtomically(
                            targetPath,
                            temporaryPath => File.Copy(stagedPath, temporaryPath, overwrite: true));
                    }

                    AppendImportedClasses(targetData, stagedData, originalClassCount);
                    pairResult = targetData.SaveConfigAndYoloDataYaml(
                        recipeName,
                        refreshDatasetVersion: false);
                    return pairResult.IsSuccess;
                });

                if (!committed)
                {
                    throw new IOException(
                        pairResult?.ErrorMessage
                        ?? "Imported classes could not be saved to the active Recipe.");
                }
            }
            catch
            {
                while (targetData.ClassNamedList.Count > originalClassCount)
                {
                    targetData.ClassNamedList.RemoveAt(targetData.ClassNamedList.Count - 1);
                }

                DeleteEmptyDirectories(missingDirectories);
                throw;
            }
        }

        private static void AppendImportedClasses(LabelingProjectData targetData, LabelingProjectData stagedData, int originalClassCount)
        {
            List<LabelClass> stagedClasses = stagedData.ClassNamedList ?? new List<LabelClass>();
            if (stagedClasses.Count < originalClassCount)
            {
                throw new InvalidDataException("Staged import removed an existing class.");
            }

            for (int index = 0; index < originalClassCount; index++)
            {
                if (!string.Equals(
                    targetData.ClassNamedList[index]?.Text,
                    stagedClasses[index]?.Text,
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Staged import changed an existing class.");
                }
            }

            for (int index = originalClassCount; index < stagedClasses.Count; index++)
            {
                LabelClass item = stagedClasses[index];
                targetData.ClassNamedList.Add(new LabelClass
                {
                    Text = item?.Text ?? string.Empty,
                    DrawColor = item?.DrawColor ?? System.Drawing.Color.Black
                });
            }
        }

        private static void DeleteEmptyDirectories(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path);
                }
            }
        }

        private static List<LabelClass> CloneClasses(IEnumerable<LabelClass> classes)
            => (classes ?? Enumerable.Empty<LabelClass>())
                .Select(item => new LabelClass
                {
                    Text = item?.Text ?? string.Empty,
                    DrawColor = item?.DrawColor ?? System.Drawing.Color.Black
                })
                .ToList();

        private static string BuildDryRunTargetPath(
            string temporaryRoot,
            string requestedTargetPath,
            DatasetExportCapability capability)
        {
            if (string.Equals(capability.FormatKey, "pascal-voc-detection", StringComparison.Ordinal))
            {
                return Path.Combine(temporaryRoot, "voc");
            }

            string extension = Path.GetExtension(requestedTargetPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = capability.FormatKey.Contains("archive", StringComparison.Ordinal)
                    ? ".zip"
                    : ".json";
            }

            return Path.Combine(temporaryRoot, "dry-run" + extension);
        }

        private static void PopulateCounts(DatasetInterchangePreflightReport report, object result)
        {
            report.ImageCount = ReadFirstInt(
                result,
                "ImageCount",
                "TaskCount",
                "ImportedImageCount",
                "ImportedTaskCount");
            report.AnnotationCount = ReadFirstInt(
                result,
                "AnnotationCount",
                "ObjectCount",
                "BoxCount",
                "PolygonCount",
                "ResultCount",
                "ImportedAnnotationCount",
                "ImportedObjectCount",
                "ImportedBoxCount",
                "ImportedPolygonCount",
                "ImportedResultCount");
            report.CategoryCount = ReadFirstInt(result, "CategoryCount");
            report.SkippedCount = result?.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property =>
                    property.PropertyType == typeof(int)
                    && property.Name.StartsWith("Skipped", StringComparison.Ordinal))
                .Sum(property => (int)(property.GetValue(result) ?? 0))
                ?? 0;
        }

        private static int ReadFirstInt(object result, params string[] propertyNames)
        {
            if (result == null)
            {
                return 0;
            }

            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = result.GetType().GetProperty(propertyName);
                if (property?.PropertyType == typeof(int))
                {
                    return (int)(property.GetValue(result) ?? 0);
                }
            }

            return 0;
        }

        private static void AppendResultWarnings(DatasetInterchangePreflightReport report, object result)
        {
            PropertyInfo warningsProperty = result?.GetType().GetProperty("Warnings");
            if (!(warningsProperty?.GetValue(result) is IEnumerable<string> warnings))
            {
                return;
            }

            foreach (string warning in warnings.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                bool alreadyRepresentedByManifest = HasManifestWarning(report.LossManifest, warning);
                if (!alreadyRepresentedByManifest && !report.Warnings.Contains(warning))
                {
                    report.Warnings.Add(warning);
                }

                AppendManifestWarning(report.LossManifest, warning);
            }
        }

        private static bool HasManifestWarning(
            DatasetInterchangeLossManifest manifest,
            string warning)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(warning))
            {
                return false;
            }

            return manifest.Entries.Any(entry =>
                !string.IsNullOrWhiteSpace(entry?.Semantic)
                && warning.StartsWith(
                    $"{entry.Semantic}: {entry.Level} - ",
                    StringComparison.Ordinal));
        }

        private static DatasetInterchangeLossManifest BuildLossManifest(
            DatasetInterchangeRequest request,
            DatasetExportCapability capability)
        {
            var manifest = new DatasetInterchangeLossManifest
            {
                FormatKey = capability?.FormatKey ?? string.Empty,
                TargetPath = request?.TargetPath ?? string.Empty
            };
            if (request?.Data == null || capability == null)
            {
                return manifest;
            }

            if (TryResolveSegmentationAuditTarget(capability.FormatKey, out SegmentationInterchangeTarget target))
            {
                foreach (string mode in YoloDatasetSplitService.StandardModes)
                {
                    string segmentDirectory = Path.Combine(
                        request.Data.OutputRootPath,
                        "data",
                        mode,
                        "segments");
                    if (!Directory.Exists(segmentDirectory))
                    {
                        continue;
                    }

                    foreach (string segmentPath in Directory.EnumerateFiles(segmentDirectory, "*.json")
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        SegmentationAnnotationFile annotation;
                        try
                        {
                            annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(
                                File.ReadAllText(segmentPath));
                        }
                        catch (Exception ex)
                        {
                            AddManifestEntry(
                                manifest,
                                "Conversion",
                                nameof(SegmentationPreservationLevel.Lost),
                                1,
                                $"segment JSON {Path.GetFileName(segmentPath)}를 읽지 못했습니다: {ex.GetBaseException().Message}");
                            continue;
                        }

                        SegmentationInterchangeAuditResult audit =
                            SegmentationInterchangeContractService.AuditAnnotation(target, annotation);
                        foreach (SegmentationInterchangeCapability capabilityItem in audit.Capabilities)
                        {
                            AddManifestEntry(
                                manifest,
                                capabilityItem.Semantic.ToString(),
                                capabilityItem.Level.ToString(),
                                1,
                                capabilityItem.Explanation);
                        }
                    }
                }
            }

            AppendObjectMetadataLosses(request.Data.OutputRootPath, manifest);
            return manifest;
        }

        private static void AppendObjectMetadataLosses(
            string outputRootPath,
            DatasetInterchangeLossManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath) || !Directory.Exists(outputRootPath))
            {
                return;
            }

            foreach (string metadataPath in Directory.EnumerateFiles(
                outputRootPath,
                "*.json",
                SearchOption.AllDirectories)
                .Where(path => string.Equals(
                    new DirectoryInfo(Path.GetDirectoryName(path) ?? string.Empty).Name,
                    "object-metadata",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    JObject document = JObject.Parse(File.ReadAllText(metadataPath));
                    JArray objects = FindProperty(document, "Objects") as JArray;
                    foreach (JToken item in objects ?? new JArray())
                    {
                        JArray tags = FindProperty(item, "Tags") as JArray;
                        if (tags?.Any(tag => !string.IsNullOrWhiteSpace(tag?.ToString())) == true)
                        {
                            AddManifestEntry(
                                manifest,
                                "ObjectMetadataTags",
                                nameof(SegmentationPreservationLevel.Lost),
                                1,
                                "현재 외부 exporter는 객체 tag를 기록하지 않습니다.");
                        }

                        if (!string.IsNullOrWhiteSpace(FindProperty(item, "GroupId")?.ToString()))
                        {
                            AddManifestEntry(
                                manifest,
                                "ObjectMetadataGroups",
                                nameof(SegmentationPreservationLevel.Lost),
                                1,
                                "현재 외부 exporter는 객체 group id를 기록하지 않습니다.");
                        }

                        if (bool.TryParse(FindProperty(item, "IsOccluded")?.ToString(), out bool isOccluded)
                            && isOccluded)
                        {
                            AddManifestEntry(
                                manifest,
                                "ObjectMetadataOcclusion",
                                nameof(SegmentationPreservationLevel.Lost),
                                1,
                                "현재 외부 exporter는 객체 occlusion 상태를 기록하지 않습니다.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddManifestEntry(
                        manifest,
                        "ObjectMetadataInspection",
                        nameof(SegmentationPreservationLevel.Lost),
                        1,
                        $"객체 metadata {Path.GetFileName(metadataPath)}를 읽지 못했습니다: {ex.GetBaseException().Message}");
                }
            }
        }

        private static JToken FindProperty(JToken token, string name)
            => token?.Children<JProperty>()
                .FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                ?.Value;

        private static void AppendManifestWarnings(DatasetInterchangePreflightReport report)
        {
            foreach (DatasetInterchangeLossManifestEntry entry in report.LossManifest?.Entries
                ?? Enumerable.Empty<DatasetInterchangeLossManifestEntry>())
            {
                if (string.Equals(entry?.Level, nameof(SegmentationPreservationLevel.Preserved), StringComparison.Ordinal))
                {
                    continue;
                }

                string warning = FormatManifestWarning(entry);
                if (!report.Warnings.Contains(warning))
                {
                    report.Warnings.Add(warning);
                }
            }
        }

        private static void AppendManifestWarning(
            DatasetInterchangeLossManifest manifest,
            string warning)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            string semantic = "Conversion";
            string level = nameof(SegmentationPreservationLevel.Lost);
            string detail = warning.Trim();
            int colon = warning.IndexOf(':');
            int separator = warning.IndexOf(" - ", StringComparison.Ordinal);
            if (colon > 0)
            {
                semantic = warning.Substring(0, colon).Trim();
                if (separator > colon)
                {
                    level = warning.Substring(colon + 1, separator - colon - 1).Trim();
                    detail = warning.Substring(separator + 3).Trim();
                }
            }

            AddManifestEntry(manifest, semantic, level, 1, detail);
        }

        private static void AddManifestEntry(
            DatasetInterchangeLossManifest manifest,
            string semantic,
            string level,
            int affectedCount,
            string detail)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(semantic))
            {
                return;
            }

            DatasetInterchangeLossManifestEntry existing = manifest.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Semantic, semantic, StringComparison.Ordinal)
                && string.Equals(entry.Level, level, StringComparison.Ordinal)
                && string.Equals(entry.Detail, detail ?? string.Empty, StringComparison.Ordinal));
            if (existing != null)
            {
                existing.AffectedCount += Math.Max(1, affectedCount);
                return;
            }

            manifest.Entries.Add(new DatasetInterchangeLossManifestEntry
            {
                Semantic = semantic,
                Level = level ?? string.Empty,
                AffectedCount = Math.Max(1, affectedCount),
                Detail = detail ?? string.Empty
            });
        }

        private static string FormatManifestWarning(DatasetInterchangeLossManifestEntry entry)
            => $"{entry?.Semantic}: {entry?.Level} - {entry?.Detail} (영향 {entry?.AffectedCount ?? 0}건)";

        private static bool TryResolveSegmentationAuditTarget(
            string formatKey,
            out SegmentationInterchangeTarget target)
        {
            target = default;
            if (string.Equals(formatKey, "coco-segmentation-json", StringComparison.Ordinal)
                || string.Equals(formatKey, "label-studio-segmentation-json", StringComparison.Ordinal))
            {
                target = SegmentationInterchangeTarget.CocoPolygonSegmentation;
                return true;
            }

            if (string.Equals(formatKey, "cvat-segmentation-archive", StringComparison.Ordinal))
            {
                target = SegmentationInterchangeTarget.CvatPolygon;
                return true;
            }

            return false;
        }

        private static string BuildLossManifestPath(
            string targetPath,
            DatasetExportCapability capability)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return string.Empty;
            }

            if (string.Equals(capability?.FormatKey, "pascal-voc-detection", StringComparison.Ordinal))
            {
                return Path.Combine(targetPath, "interchange-loss-manifest.json");
            }

            return targetPath + ".loss-manifest.json";
        }

        private static void WriteLossManifest(
            DatasetInterchangeLossManifest manifest,
            string manifestPath)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifestPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AnnotationFilePersistence.WriteAtomically(
                manifestPath,
                temporaryPath => File.WriteAllText(
                    temporaryPath,
                    JsonConvert.SerializeObject(manifest, Formatting.Indented),
                    new UTF8Encoding(false)));
        }

        private static void CompleteReport(DatasetInterchangePreflightReport report)
        {
            report.CanApply = report.IsDryRun
                && report.Issues.Count == 0
                && report.SourceUnchanged
                && report.RequestedTargetUnchanged
                && (!report.RequiresLossConfirmation || report.LossConfirmationAccepted);
            if (report.Issues.Count > 0)
            {
                report.StatusText = report.IsDryRun ? "Dry-run blocked" : "Apply failed";
                report.DetailText = string.Join(" ", report.Issues);
            }
            else if (report.IsDryRun)
            {
                report.StatusText = report.RequiresLossConfirmation && !report.LossConfirmationAccepted
                    ? "Loss confirmation required"
                    : report.Warnings.Count > 0
                        ? "Ready to apply with warnings"
                        : "Ready to apply";
                report.DetailText =
                    $"\uC774\uBBF8\uC9C0 {report.ImageCount}, \uC5B4\uB178\uD14C\uC774\uC158 {report.AnnotationCount}, \uD074\uB798\uC2A4 {report.CategoryCount}. "
                    + "\uC6D0\uBCF8\uACFC \uC694\uCCAD \uB300\uC0C1\uC740 \uBCC0\uACBD\uB418\uC9C0 \uC54C\uC558\uC2B5\uB2C8\uB2E4."
                    + (string.IsNullOrWhiteSpace(report.LossManifestPath)
                        ? string.Empty
                        : $" manifest: {report.LossManifestPath}");
            }
            else
            {
                report.StatusText = "Conversion applied";
                report.DetailText =
                    $"\uC774\uBBF8\uC9C0 {report.ImageCount}, \uC5B4\uB178\uD14C\uC774\uC158 {report.AnnotationCount}, \uD074\uB798\uC2A4 {report.CategoryCount}.";
            }
        }

        private static string ComputeSourceFingerprint(
            DatasetInterchangeRequest request,
            DatasetExportCapability capability)
        {
            bool isImport = string.Equals(capability.Direction, "import", StringComparison.OrdinalIgnoreCase);
            if (!isImport)
            {
                return ComputePathFingerprint(request.Data.OutputRootPath);
            }

            string annotationFingerprint = ComputePathFingerprint(request.SourcePath);
            if (!RequiresImageRoot(capability.FormatKey))
            {
                return annotationFingerprint;
            }

            string imageRootFingerprint = ComputePathFingerprint(request.ImageRoot);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                annotationFingerprint + "|" + imageRootFingerprint)));
        }

        private static bool RequiresImageRoot(string formatKey)
            => !string.Equals(formatKey, "cvat-detection-import", StringComparison.Ordinal)
                && !string.Equals(formatKey, "cvat-segmentation-import", StringComparison.Ordinal);

        private static bool IsSupportedSplit(string split)
            => string.Equals(split, YoloDatasetSplitService.TrainMode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(split, YoloDatasetSplitService.ValidMode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(split, YoloDatasetSplitService.TestMode, StringComparison.OrdinalIgnoreCase);

        private static bool IsPathInside(string candidatePath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            string candidate = Path.GetFullPath(candidatePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
        }

        internal static string ComputePathFingerprint(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "missing";
            }

            if (File.Exists(path))
            {
                using SHA256 fileSha = SHA256.Create();
                using FileStream stream = File.OpenRead(path);
                return Convert.ToHexString(fileSha.ComputeHash(stream));
            }

            if (!Directory.Exists(path))
            {
                return "missing";
            }

            using SHA256 treeSha = SHA256.Create();
            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                string relativePath = Path.GetRelativePath(path, filePath).Replace('\\', '/');
                byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
                treeSha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
                using FileStream stream = File.OpenRead(filePath);
                byte[] buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    treeSha.TransformBlock(buffer, 0, read, null, 0);
                }
            }

            treeSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(treeSha.Hash ?? Array.Empty<byte>());
        }
    }
}
