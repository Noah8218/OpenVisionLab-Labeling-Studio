using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// UI-facing orchestration for a deliberately narrow comparison: one U-Net checkpoint
    /// and one Ultralytics segmentation checkpoint, evaluated against the same immutable
    /// canonical segmentation export.  It never changes the inspection-model selection.
    /// </summary>
    public class SegmentationAdapterComparisonRunService
    {
        public SegmentationAdapterComparisonContext BuildContext(
            LabelingProjectData data,
            ModelRegistrySettings registry,
            PythonModelSettings currentSettings)
        {
            currentSettings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(currentSettings);
            string selectedYoloEngine = ResolveDefaultYoloEngine(registry, currentSettings);
            string unetWeightsPath = ResolveCheckpointPath(
                PythonModelSettings.EngineUnet,
                registry,
                currentSettings);
            string yoloWeightsPath = ResolveCheckpointPath(
                selectedYoloEngine,
                registry,
                currentSettings);
            bool isSegmentationRecipe = data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            ExternalYoloDatasetSettings externalDataset = data?.ProjectSettings?.ExternalYoloDataset;
            bool usesExternalNativeYolo = externalDataset?.UseForTraining == true;
            YoloExternalDatasetIntakeReport externalReport = usesExternalNativeYolo
                ? YoloExternalDatasetIntakeService.Build(externalDataset.DataYamlFilePath, LabelingDatasetPurpose.Segmentation)
                : null;
            int testImageCount = usesExternalNativeYolo
                ? externalReport?.Test?.ImageCount ?? 0
                : CountTestImages(data?.OutputRootPath);
            int classCount = usesExternalNativeYolo
                ? externalReport?.ClassNames?.Count ?? 0
                : data?.ClassNamedList?.Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;
            bool externalSourceReady = !usesExternalNativeYolo
                || (externalReport?.IsReady == true
                    && !externalDataset.RequiresExplicitReactivation
                    && YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(externalDataset, externalReport, out _));

            string canonicalDatasetText = isSegmentationRecipe
                ? $"공통 입력: 현재 레시피의 저장 마스크 → app-owned canonical export / test {testImageCount}장 / 클래스 {classCount}개"
                : "이 비교는 Segmentation 레시피에서만 사용할 수 있습니다. 객체탐지 mAP와 U-Net mask 점수는 섞지 않습니다.";
            string statusText = !isSegmentationRecipe
                ? "세그멘테이션 레시피 필요"
                : testImageCount <= 0
                    ? "최종 검증(test) 이미지 필요"
                    : string.IsNullOrWhiteSpace(unetWeightsPath) || string.IsNullOrWhiteSpace(yoloWeightsPath)
                        ? "두 checkpoint 선택 필요"
                        : "비교 준비";
            string actionText = !isSegmentationRecipe
                ? "데이터셋 목적을 세그멘테이션으로 설정하고 저장 마스크가 있는 train/valid/test 분할을 준비하세요."
                : testImageCount <= 0
                    ? "동일 레시피에 test 이미지와 저장된 마스크를 준비한 뒤 비교하세요."
                    : "U-Net과 YOLO-seg checkpoint를 확인한 뒤 비교 실행을 누르세요. 결과는 artifact에만 저장되며 검사 모델은 자동 교체하지 않습니다.";

            if (isSegmentationRecipe && usesExternalNativeYolo)
            {
                canonicalDatasetText = $"공통 입력: 외부 native YOLO segmentation data.yaml을 원본 변경 없이 canonical mask export로 변환 / test {testImageCount}장 / 클래스 {classCount}개";
                if (!externalSourceReady)
                {
                    statusText = "외부 native YOLO source 재검증·명시적 활성화 필요";
                    actionText = "외부 data.yaml의 원본 파일이 바뀌었거나 아직 활성화되지 않았습니다. Dataset 화면에서 검증 후 명시적으로 사용을 확정하세요.";
                }
            }

            return new SegmentationAdapterComparisonContext
            {
                IsVisible = isSegmentationRecipe,
                CanonicalDatasetText = canonicalDatasetText,
                StatusText = statusText,
                DetailText = "동일한 raw raster mask를 Dice/IoU 및 결함 component TP/FP/FN으로 비교합니다. YOLO mAP 보고서와 직접 합산하지 않습니다.",
                ActionText = actionText,
                UnetWeightsPath = unetWeightsPath,
                YoloWeightsPath = yoloWeightsPath,
                SelectedYoloEngine = selectedYoloEngine,
                CanRun = isSegmentationRecipe
                    && externalSourceReady
                    && testImageCount > 0
                    && File.Exists(unetWeightsPath)
                    && File.Exists(yoloWeightsPath)
            };
        }

        public SegmentationAdapterComparisonRunRequest BuildRequest(
            LabelingProjectData data,
            PythonModelSettings currentSettings,
            string unetWeightsPath,
            string yoloWeightsPath,
            string yoloEngine)
        {
            currentSettings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(currentSettings);
            string normalizedYoloEngine = NormalizeUltralyticsEngine(yoloEngine);
            PythonModelSettings unetSettings = BuildUnetSettings(currentSettings, unetWeightsPath);
            PythonModelSettings yoloSettings = BuildYoloSettings(currentSettings, normalizedYoloEngine, yoloWeightsPath);
            return new SegmentationAdapterComparisonRunRequest
            {
                Data = data,
                UnetSettings = unetSettings,
                YoloSettings = yoloSettings,
                YoloEngine = normalizedYoloEngine,
                OutputParentDirectory = Path.Combine(
                    data?.OutputRootPath?.Trim() ?? string.Empty,
                    "artifacts",
                    "segmentation-adapter-comparison")
            };
        }

        public IReadOnlyList<string> ValidateRequest(SegmentationAdapterComparisonRunRequest request)
        {
            var errors = new List<string>();
            if (request?.Data == null)
            {
                errors.Add("세그멘테이션 비교에 사용할 레시피가 없습니다.");
                return errors;
            }

            if (request.Data.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                errors.Add("U-Net vs YOLO-seg 비교는 세그멘테이션 레시피에서만 실행할 수 있습니다.");
            }

            ValidateRuntime(request.UnetSettings, "U-Net", PythonModelSettings.EngineUnet, errors);
            ValidateRuntime(request.YoloSettings, "YOLO-seg", request.YoloEngine, errors);
            if (string.IsNullOrWhiteSpace(request.OutputParentDirectory))
            {
                errors.Add("세그멘테이션 비교 artifact 저장 위치가 없습니다.");
            }

            return errors;
        }

        public Task<SegmentationAdapterComparisonRunResult> RunAsync(SegmentationAdapterComparisonRunRequest request)
            => RunAsync(request, CancellationToken.None);

        public Task<SegmentationAdapterComparisonRunResult> RunAsync(
            SegmentationAdapterComparisonRunRequest request,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => Run(request, cancellationToken), cancellationToken);
        }

        public SegmentationAdapterComparisonRunResult Run(SegmentationAdapterComparisonRunRequest request)
            => Run(request, CancellationToken.None);

        public SegmentationAdapterComparisonRunResult Run(
            SegmentationAdapterComparisonRunRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> validationErrors = ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                return SegmentationAdapterComparisonRunResult.Failed(string.Join(Environment.NewLine, validationErrors));
            }

            cancellationToken.ThrowIfCancellationRequested();
            UnetSegmentationDatasetExportResult export = BuildCanonicalExport(request.Data, cancellationToken);
            if (!export.IsReady)
            {
                return SegmentationAdapterComparisonRunResult.Failed(
                    "공통 canonical segmentation export를 만들 수 없습니다: " + string.Join(" / ", export.Errors));
            }

            string runRoot = BuildNewRunRoot(request.OutputParentDirectory, export.DatasetFingerprint);
            SegmentationPredictionExportRequest unetPredictionRequest = SegmentationPredictionExportService.BuildRequest(
                SegmentationPredictionExportService.AdapterUnet,
                request.UnetSettings,
                export.OutputRootPath,
                Path.Combine(runRoot, "unet"));
            SegmentationPredictionExportRequest yoloPredictionRequest = SegmentationPredictionExportService.BuildRequest(
                SegmentationPredictionExportService.AdapterUltralytics,
                request.YoloSettings,
                export.OutputRootPath,
                Path.Combine(runRoot, "yolo-seg"));

            cancellationToken.ThrowIfCancellationRequested();
            SegmentationPredictionExportResult unetPrediction =
                SegmentationPredictionExportService.Run(unetPredictionRequest, cancellationToken);
            if (!unetPrediction.Succeeded)
            {
                return SegmentationAdapterComparisonRunResult.Failed(
                    "U-Net raw mask export에 실패했습니다: " + FirstLine(unetPrediction.Error),
                    export.OutputRootPath,
                    runRoot);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SegmentationPredictionExportResult yoloPrediction =
                SegmentationPredictionExportService.Run(yoloPredictionRequest, cancellationToken);
            if (!yoloPrediction.Succeeded)
            {
                return SegmentationAdapterComparisonRunResult.Failed(
                    "YOLO-seg raw mask export에 실패했습니다: " + FirstLine(yoloPrediction.Error),
                    export.OutputRootPath,
                    runRoot,
                    unetPrediction.PredictionManifestPath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SegmentationMaskComparisonResult comparison = SegmentationMaskComparisonService.Evaluate(
                new SegmentationMaskComparisonRequest
                {
                    DatasetExportRootPath = export.OutputRootPath,
                    BaselinePredictionManifestPath = unetPrediction.PredictionManifestPath,
                    CandidatePredictionManifestPath = yoloPrediction.PredictionManifestPath,
                    Split = "test",
                    OutputRootPath = Path.Combine(runRoot, "comparison"),
                    ComponentIouThreshold = 0.5D
                });
            cancellationToken.ThrowIfCancellationRequested();
            if (!comparison.IsReady)
            {
                return SegmentationAdapterComparisonRunResult.Failed(
                    "공통 마스크 비교를 완료하지 못했습니다: " + string.Join(" / ", comparison.Errors),
                    export.OutputRootPath,
                    runRoot,
                    unetPrediction.PredictionManifestPath,
                    yoloPrediction.PredictionManifestPath);
            }

            return SegmentationAdapterComparisonRunResult.Success(
                export.OutputRootPath,
                runRoot,
                unetPrediction.PredictionManifestPath,
                yoloPrediction.PredictionManifestPath,
                comparison);
        }

        private static UnetSegmentationDatasetExportResult BuildCanonicalExport(
            LabelingProjectData data,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExternalYoloDatasetSettings externalDataset = data?.ProjectSettings?.ExternalYoloDataset;
            if (externalDataset?.UseForTraining != true)
            {
                UnetSegmentationDatasetExportResult export = UnetSegmentationDatasetExportService.Export(data);
                cancellationToken.ThrowIfCancellationRequested();
                return export;
            }

            var blocked = new UnetSegmentationDatasetExportResult();
            if (externalDataset.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                blocked.Errors.Add("Selected external data.yaml is not activated as a segmentation source.");
                return blocked;
            }
            if (externalDataset.RequiresExplicitReactivation)
            {
                blocked.Errors.Add(string.IsNullOrWhiteSpace(externalDataset.LastValidationSummary)
                    ? "External data.yaml requires revalidation and explicit activation."
                    : externalDataset.LastValidationSummary);
                return blocked;
            }

            YoloExternalDatasetIntakeReport report = YoloExternalDatasetIntakeService.Build(
                externalDataset.DataYamlFilePath,
                LabelingDatasetPurpose.Segmentation,
                cancellationToken);
            if (!report.IsReady)
            {
                blocked.Errors.AddRange(report.Errors);
                return blocked;
            }
            if (!YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(externalDataset, report, out string identityError))
            {
                blocked.Errors.Add(identityError);
                return blocked;
            }

            cancellationToken.ThrowIfCancellationRequested();
            UnetSegmentationDatasetExportResult canonicalExport = ExternalYoloSegmentationCanonicalExportService.Export(data, report.DataYamlFilePath);
            cancellationToken.ThrowIfCancellationRequested();
            return canonicalExport;
        }

        private static PythonModelSettings BuildUnetSettings(PythonModelSettings currentSettings, string weightsPath)
        {
            string projectRootPath = string.Equals(
                currentSettings?.ModelEngine,
                PythonModelSettings.EngineUnet,
                StringComparison.Ordinal)
                && Directory.Exists(currentSettings.ProjectRootPath)
                ? currentSettings.ProjectRootPath
                : PythonModelRuntimePathResolver.GetDefaultUnetProjectRootPath();
            PythonModelRuntimeConnectionResult connection = PythonModelRuntimeConnectionService.BuildUnetFolderConnection(
                currentSettings,
                projectRootPath);
            PythonModelSettings settings = CloneSettings(connection.Settings);
            settings.WeightsPath = weightsPath?.Trim() ?? string.Empty;
            settings.InferenceImageSize = Math.Max(64, currentSettings?.InferenceImageSize ?? 320);
            settings.MinimumDetectionConfidence = currentSettings?.MinimumDetectionConfidence ?? 0.25F;
            return settings;
        }

        private static PythonModelSettings BuildYoloSettings(
            PythonModelSettings currentSettings,
            string yoloEngine,
            string weightsPath)
        {
            string normalizedEngine = NormalizeUltralyticsEngine(yoloEngine);
            string projectRootPath = ResolveUltralyticsProjectRoot(currentSettings, normalizedEngine);
            PythonModelRuntimeConnectionResult connection = string.Equals(normalizedEngine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal)
                ? PythonModelRuntimeConnectionService.BuildYolo11FolderConnection(
                    currentSettings,
                    projectRootPath,
                    LabelingDatasetPurpose.Segmentation)
                : PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection(
                    currentSettings,
                    projectRootPath,
                    LabelingDatasetPurpose.Segmentation);
            PythonModelSettings settings = CloneSettings(connection.Settings);
            settings.ModelEngine = normalizedEngine;
            settings.WeightsPath = weightsPath?.Trim() ?? string.Empty;
            settings.InferenceImageSize = Math.Max(64, currentSettings?.InferenceImageSize ?? 320);
            settings.MinimumDetectionConfidence = currentSettings?.MinimumDetectionConfidence ?? 0.25F;
            return settings;
        }

        private static void ValidateRuntime(
            PythonModelSettings settings,
            string displayName,
            string expectedEngine,
            List<string> errors)
        {
            string normalizedExpectedEngine = PythonModelSettings.NormalizeModelEngine(expectedEngine);
            if (!string.Equals(PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine), normalizedExpectedEngine, StringComparison.Ordinal))
            {
                errors.Add(displayName + " runtime profile could not be resolved.");
                return;
            }

            string pythonPath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (string.IsNullOrWhiteSpace(pythonPath) || !File.Exists(pythonPath))
            {
                errors.Add(displayName + " Python 실행 파일을 찾을 수 없습니다: " + pythonPath);
            }

            string weightsPath = settings?.WeightsPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                errors.Add(displayName + " checkpoint를 찾을 수 없습니다: " + weightsPath);
            }
        }

        private static string ResolveDefaultYoloEngine(ModelRegistrySettings registry, PythonModelSettings currentSettings)
        {
            string current = PythonModelSettings.NormalizeModelEngine(currentSettings?.ModelEngine);
            if (current == PythonModelSettings.EngineYoloV8 || current == PythonModelSettings.EngineYolo11)
            {
                return current;
            }

            return !string.IsNullOrWhiteSpace(FindLatestCandidateCheckpoint(registry, PythonModelSettings.EngineYoloV8))
                ? PythonModelSettings.EngineYoloV8
                : PythonModelSettings.EngineYolo11;
        }

        private static string ResolveCheckpointPath(
            string engine,
            ModelRegistrySettings registry,
            PythonModelSettings currentSettings)
        {
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            if (string.Equals(
                    PythonModelSettings.NormalizeModelEngine(currentSettings?.ModelEngine),
                    normalizedEngine,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(currentSettings?.WeightsPath))
            {
                return currentSettings.WeightsPath.Trim();
            }

            return FindLatestCandidateCheckpoint(registry, normalizedEngine);
        }

        private static string FindLatestCandidateCheckpoint(ModelRegistrySettings registry, string engine)
        {
            registry?.EnsureDefaults();
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            return (registry?.Candidates ?? new List<ModelCandidate>())
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.WeightsPath))
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Profile = (registry?.Profiles ?? new List<ModelProfile>())
                        .FirstOrDefault(profile => profile != null
                            && string.Equals(profile.ProfileId, candidate.ProfileId, StringComparison.Ordinal))
                })
                .Where(item => string.Equals(
                    PythonModelSettings.NormalizeModelEngine(item.Profile?.ModelEngine),
                    normalizedEngine,
                    StringComparison.Ordinal))
                .OrderByDescending(item => item.Candidate.LastSeenUtc ?? string.Empty, StringComparer.Ordinal)
                .ThenByDescending(item => item.Candidate.CreatedUtc ?? string.Empty, StringComparer.Ordinal)
                .Select(item => item.Candidate.WeightsPath.Trim())
                .FirstOrDefault() ?? string.Empty;
        }

        private static string ResolveUltralyticsProjectRoot(PythonModelSettings currentSettings, string engine)
        {
            string normalizedEngine = NormalizeUltralyticsEngine(engine);
            string currentRoot = currentSettings?.ProjectRootPath?.Trim() ?? string.Empty;
            string currentEngine = PythonModelSettings.NormalizeModelEngine(currentSettings?.ModelEngine);
            if (string.Equals(currentEngine, normalizedEngine, StringComparison.Ordinal)
                && Directory.Exists(currentRoot))
            {
                return currentRoot;
            }

            // YOLO11 is an Ultralytics model family, not a separate required repository.
            string runtimeFolderName = "yolov8";
            string resolved = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(currentRoot, runtimeFolderName);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            string runtimeAnchorRoot = Directory.Exists(currentRoot)
                ? currentRoot
                : PythonModelRuntimePathResolver.GetDefaultUnetProjectRootPath();
            string parent = Directory.GetParent(runtimeAnchorRoot)?.FullName ?? string.Empty;
            string sibling = string.IsNullOrWhiteSpace(parent) ? string.Empty : Path.Combine(parent, runtimeFolderName);
            return Directory.Exists(sibling) ? sibling : string.Empty;
        }

        private static PythonModelSettings CloneSettings(PythonModelSettings source)
        {
            source ??= new PythonModelSettings();
            return new PythonModelSettings
            {
                PythonExecutablePath = source.PythonExecutablePath ?? string.Empty,
                ModelEngine = PythonModelSettings.NormalizeModelEngine(source.ModelEngine),
                ProjectRootPath = source.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source.ClientScriptPath ?? string.Empty,
                WeightsPath = source.WeightsPath ?? string.Empty,
                ImageRootPath = source.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source.MinimumDetectionConfidence,
                MaximumDetectionCandidates = source.MaximumDetectionCandidates,
                InferenceImageSize = source.InferenceImageSize,
                DetectionTimeoutSeconds = source.DetectionTimeoutSeconds,
                AutoStartClient = source.AutoStartClient
            };
        }

        private static int CountTestImages(string outputRootPath)
        {
            string testImageRoot = Path.Combine(outputRootPath?.Trim() ?? string.Empty, "data", "test", "images");
            if (!Directory.Exists(testImageRoot))
            {
                return 0;
            }

            return Directory.EnumerateFiles(testImageRoot, "*", SearchOption.AllDirectories)
                .Count(path => IsImageExtension(Path.GetExtension(path)));
        }

        private static bool IsImageExtension(string extension)
        {
            return string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildNewRunRoot(string parentDirectory, string datasetFingerprint)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string fingerprintPrefix = (datasetFingerprint ?? string.Empty).Substring(0, Math.Min(16, (datasetFingerprint ?? string.Empty).Length));
            string root = Path.Combine(parentDirectory, "run-" + fingerprintPrefix + "-" + timestamp);
            return root;
        }

        private static string NormalizeUltralyticsEngine(string engine)
        {
            string normalized = PythonModelSettings.NormalizeModelEngine(engine);
            return normalized == PythonModelSettings.EngineYolo11
                ? PythonModelSettings.EngineYolo11
                : PythonModelSettings.EngineYoloV8;
        }

        private static string FirstLine(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "no process detail";
        }
    }

    public class SegmentationAdapterComparisonContext
    {
        public bool IsVisible { get; set; }
        public string CanonicalDatasetText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public string UnetWeightsPath { get; set; } = string.Empty;
        public string YoloWeightsPath { get; set; } = string.Empty;
        public string SelectedYoloEngine { get; set; } = PythonModelSettings.EngineYoloV8;
        public bool CanRun { get; set; }
    }

    public class SegmentationAdapterComparisonRunRequest
    {
        public LabelingProjectData Data { get; set; }
        public PythonModelSettings UnetSettings { get; set; } = new PythonModelSettings();
        public PythonModelSettings YoloSettings { get; set; } = new PythonModelSettings();
        public string YoloEngine { get; set; } = PythonModelSettings.EngineYoloV8;
        public string OutputParentDirectory { get; set; } = string.Empty;
    }

    public class SegmentationAdapterComparisonRunResult
    {
        protected SegmentationAdapterComparisonRunResult(
            bool succeeded,
            string error,
            string canonicalExportRootPath,
            string runRootPath,
            string unetPredictionManifestPath,
            string yoloPredictionManifestPath,
            SegmentationMaskComparisonResult comparison)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
            CanonicalExportRootPath = canonicalExportRootPath ?? string.Empty;
            RunRootPath = runRootPath ?? string.Empty;
            UnetPredictionManifestPath = unetPredictionManifestPath ?? string.Empty;
            YoloPredictionManifestPath = yoloPredictionManifestPath ?? string.Empty;
            Comparison = comparison;
        }

        public bool Succeeded { get; }
        public string Error { get; }
        public string CanonicalExportRootPath { get; }
        public string RunRootPath { get; }
        public string UnetPredictionManifestPath { get; }
        public string YoloPredictionManifestPath { get; }
        public SegmentationMaskComparisonResult Comparison { get; }

        public static SegmentationAdapterComparisonRunResult Failed(
            string error,
            string canonicalExportRootPath = "",
            string runRootPath = "",
            string unetPredictionManifestPath = "",
            string yoloPredictionManifestPath = "")
        {
            return new SegmentationAdapterComparisonRunResult(
                false,
                error,
                canonicalExportRootPath,
                runRootPath,
                unetPredictionManifestPath,
                yoloPredictionManifestPath,
                comparison: null);
        }

        public static SegmentationAdapterComparisonRunResult Success(
            string canonicalExportRootPath,
            string runRootPath,
            string unetPredictionManifestPath,
            string yoloPredictionManifestPath,
            SegmentationMaskComparisonResult comparison)
        {
            return new SegmentationAdapterComparisonRunResult(
                true,
                error: string.Empty,
                canonicalExportRootPath,
                runRootPath,
                unetPredictionManifestPath,
                yoloPredictionManifestPath,
                comparison);
        }
    }

    [Obsolete("Use SegmentationAdapterComparisonContext.", false)]
    public sealed class WpfSegmentationAdapterComparisonContext : SegmentationAdapterComparisonContext
    {
        internal static WpfSegmentationAdapterComparisonContext FromCanonical(SegmentationAdapterComparisonContext source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfSegmentationAdapterComparisonContext
            {
                IsVisible = source.IsVisible,
                CanonicalDatasetText = source.CanonicalDatasetText,
                StatusText = source.StatusText,
                DetailText = source.DetailText,
                ActionText = source.ActionText,
                UnetWeightsPath = source.UnetWeightsPath,
                YoloWeightsPath = source.YoloWeightsPath,
                SelectedYoloEngine = source.SelectedYoloEngine,
                CanRun = source.CanRun
            };
        }
    }

    [Obsolete("Use SegmentationAdapterComparisonRunRequest.", false)]
    public sealed class WpfSegmentationAdapterComparisonRunRequest : SegmentationAdapterComparisonRunRequest
    {
        internal static WpfSegmentationAdapterComparisonRunRequest FromCanonical(SegmentationAdapterComparisonRunRequest source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfSegmentationAdapterComparisonRunRequest
            {
                Data = source.Data,
                UnetSettings = source.UnetSettings,
                YoloSettings = source.YoloSettings,
                YoloEngine = source.YoloEngine,
                OutputParentDirectory = source.OutputParentDirectory
            };
        }
    }

    [Obsolete("Use SegmentationAdapterComparisonRunResult.", false)]
    public sealed class WpfSegmentationAdapterComparisonRunResult : SegmentationAdapterComparisonRunResult
    {
        private WpfSegmentationAdapterComparisonRunResult(
            bool succeeded,
            string error,
            string canonicalExportRootPath,
            string runRootPath,
            string unetPredictionManifestPath,
            string yoloPredictionManifestPath,
            SegmentationMaskComparisonResult comparison)
            : base(
                succeeded,
                error,
                canonicalExportRootPath,
                runRootPath,
                unetPredictionManifestPath,
                yoloPredictionManifestPath,
                comparison)
        {
        }

        internal static WpfSegmentationAdapterComparisonRunResult FromCanonical(SegmentationAdapterComparisonRunResult source)
        {
            return source == null
                ? null
                : new WpfSegmentationAdapterComparisonRunResult(
                    source.Succeeded,
                    source.Error,
                    source.CanonicalExportRootPath,
                    source.RunRootPath,
                    source.UnetPredictionManifestPath,
                    source.YoloPredictionManifestPath,
                    source.Comparison);
        }

        public static new WpfSegmentationAdapterComparisonRunResult Failed(
            string error,
            string canonicalExportRootPath = "",
            string runRootPath = "",
            string unetPredictionManifestPath = "",
            string yoloPredictionManifestPath = "")
            => new WpfSegmentationAdapterComparisonRunResult(
                false,
                error,
                canonicalExportRootPath,
                runRootPath,
                unetPredictionManifestPath,
                yoloPredictionManifestPath,
                comparison: null);

        public static new WpfSegmentationAdapterComparisonRunResult Success(
            string canonicalExportRootPath,
            string runRootPath,
            string unetPredictionManifestPath,
            string yoloPredictionManifestPath,
            SegmentationMaskComparisonResult comparison)
            => new WpfSegmentationAdapterComparisonRunResult(
                true,
                error: string.Empty,
                canonicalExportRootPath,
                runRootPath,
                unetPredictionManifestPath,
                yoloPredictionManifestPath,
                comparison);
    }

    [Obsolete("Use SegmentationAdapterComparisonRunService.", false)]
    public sealed class WpfSegmentationAdapterComparisonRunService : SegmentationAdapterComparisonRunService
    {
        public new WpfSegmentationAdapterComparisonContext BuildContext(
            LabelingProjectData data,
            ModelRegistrySettings registry,
            PythonModelSettings currentSettings)
            => WpfSegmentationAdapterComparisonContext.FromCanonical(base.BuildContext(data, registry, currentSettings));

        public new WpfSegmentationAdapterComparisonRunRequest BuildRequest(
            LabelingProjectData data,
            PythonModelSettings currentSettings,
            string unetWeightsPath,
            string yoloWeightsPath,
            string yoloEngine)
            => WpfSegmentationAdapterComparisonRunRequest.FromCanonical(base.BuildRequest(data, currentSettings, unetWeightsPath, yoloWeightsPath, yoloEngine));

        public IReadOnlyList<string> ValidateRequest(WpfSegmentationAdapterComparisonRunRequest request)
            => base.ValidateRequest(request);

        public async Task<WpfSegmentationAdapterComparisonRunResult> RunAsync(WpfSegmentationAdapterComparisonRunRequest request)
            => WpfSegmentationAdapterComparisonRunResult.FromCanonical(await base.RunAsync(request).ConfigureAwait(false));

        public async Task<WpfSegmentationAdapterComparisonRunResult> RunAsync(
            WpfSegmentationAdapterComparisonRunRequest request,
            CancellationToken cancellationToken)
            => WpfSegmentationAdapterComparisonRunResult.FromCanonical(await base.RunAsync(request, cancellationToken).ConfigureAwait(false));

        public WpfSegmentationAdapterComparisonRunResult Run(WpfSegmentationAdapterComparisonRunRequest request)
            => WpfSegmentationAdapterComparisonRunResult.FromCanonical(base.Run(request));

        public WpfSegmentationAdapterComparisonRunResult Run(
            WpfSegmentationAdapterComparisonRunRequest request,
            CancellationToken cancellationToken)
            => WpfSegmentationAdapterComparisonRunResult.FromCanonical(base.Run(request, cancellationToken));
    }
}
